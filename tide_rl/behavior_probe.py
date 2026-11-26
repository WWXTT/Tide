"""绿行为门探针（Stage 3 配套·三专精→教师→蒸馏框架 §8.2，第二批工具 2026-11-26）。

跑真局产出 teacher_behavior.json——teacher_gate 绿行为门的四项证据：
  t5_agros_rate（mAgrosTurn ∈ 1..10 的局 / 总局）、chain_rate（mChain>0 的局 / 总局）、
  full_sac_rate（单次献祭目标数 ≥11 的局 / 总局）、wotu_per_game（mGrow 合计 / 总局）。
**键名与 teacher_gate.DEFAULT_BEHAVIOR_THRESHOLDS 逐字一致**（本文件 import 该表取
thresholds_used，不复制常量）。红/蓝也可跑（诊断用），但行为门语义只对绿定义。

口径：
  - 动作 = **categorical 采样**（与教师矩阵/评估协议 v2 同式，caliber="sample"）——
    argmax 系统性高估到达率（U5/E2），两口径不可互换，禁止改 argmax；
  - 种子方案照抄 eval_matrix（pair_seed_offset + seat*500 + ep，同 --seed 基）——
    与教师矩阵同批对局；采样 rng 每局按局种子 fold_in（同 evaluate_greedy P1 口径）；
  - 行为计数读终局 info（C# TideHeadlessDriver.TrackModelBehavior 权威：mSac/mRec/
    mChain/mAgros/mAgrosTurn/mGrow——Apply 成功才计，「当地落」不算）；
  - 「每次献祭目标数」= 选中动作行 type=5（HeroSkill 牺牲）**连续同源**（[2]source 相同）
    分组的行长（A6 逐目标单选：每目标一行）——行流启发式：中断在其他动作/异源即断组，
    同源两次独立发动会被并组 → 只会低估，字段 lower_bound=true；mSac 是 C# 权威计数。
  - --opponent script：脚本臂对照（§8.2 的 20–50 局标定；reset 即整局跑完，无决策步，
    献祭直方图为空），输出 source="script"，只作对照不并进模型行为门。

用法（须在训练副本 tide_rl 目录跑，Unity 真局一进程一局、与训练互斥；
--project-path 指该副本工程根——从主仓脚本跑副本局时必传）：
  python behavior_probe.py --run logs/<专精run> [--split last] [--deck green]
      [--opponent simpleai|script] [--games-per-cell 96] [--seed 20261122]
      [--out <run>/teacher_behavior.json] [--quiet]
      [--unity-path ...] [--project-path ...]

输出：控制台摘要 + teacher_behavior.json（四项门键 + sac_hist 直方图 + thresholds_used
+ per_game 逐局明细 + deck/run/step/opponent/games/seed/caliber/source 戳）。
退出码：0=正常产出（行为过不过门由 teacher_gate 判，本工具不打分）。
"""
import argparse
import json
import time
from pathlib import Path

import jax
import flax.serialization

from tide_agent import create_tide_agent, merge_params_by_shape
from tide_features import (
    init_rstate, sample_input, ACTION_F_TYPE, ACTION_F_SOURCE,
)
from tide_env import TideEnv

from train_tide_complete import Args, THEME_KEYS
from eval_checkpoint import resolve_run_dir, pick_ckpt, args_from_run
from eval_matrix import pair_seed_offset
from teacher_gate import DEFAULT_BEHAVIOR_THRESHOLDS

DECKS = ("red", "green", "blue")
CN = {"red": "红", "green": "绿", "blue": "蓝"}
SAC_TYPE = 5           # HeroSkill 牺牲（逐目标单选后每目标一行，A6）
FULL_SAC_TARGETS = 11  # 满献：单次献祭目标数 ≥11（§8.2 阈值口径，非判据常量——仅计数分组）


def behavior_of(info):
    """终局 info → 行为计数（老协议无字段按 0 处理，口径同 rollout_common._behavior_of
    但只取本工具消费的六键）。"""
    if not isinstance(info, dict):
        return {}
    return {"mSac": int(info.get("mSac", 0)), "mRec": int(info.get("mRec", 0)),
            "mChain": int(info.get("mChain", 0)), "mAgros": int(info.get("mAgros", 0)),
            "mAgrosTurn": int(info.get("mAgrosTurn", 0)),
            "mGrow": int(info.get("mGrow", 0))}


def run_model_game(jit_apply, params, env, args, deck, opp, seat, seed, rng, quiet=True):
    """模型臂单局：采样决策（caliber=sample），返回 (per_game 记录, 行为计数, 献祭组)。
    循环体口径镜像 train_tide_complete.evaluate_greedy（P1 采样版）——同式重写以捕获
    逐步选中动作行（evaluate_greedy 不暴露行流，无法复用其内部循环）。"""
    obs, info = env.reset(options={"opponent": "simpleai", "theme": opp,
                                   "modelTheme": deck, "modelSeat": seat,
                                   "seed": seed})
    # env 层已断言 modelSeat/seed 回显一致（协议 v2）；theme 再对一遍，响亮失败
    if str(info.get("modelTheme", "")) != deck:
        raise RuntimeError(f"[probe] modelTheme 回显 {info.get('modelTheme')!r} ≠ 请求 {deck!r}")
    rstate = init_rstate(args.rnn_type)
    sac_groups, cur = [], None     # cur=(source,count)：连续同源 type=5 行分组
    steps = 0
    done = False
    while not done and steps < args.max_episode_steps:
        actions = obs["actions_"]
        obs_batch = jax.tree.map(lambda x: x[None, ...] if x is not None else None, obs)
        rstate, logits, _ = jit_apply(params, rstate, obs_batch)
        rng, sub = jax.random.split(rng)
        a = int(jax.random.categorical(sub, logits[0]))
        row = actions[a]
        if int(row[0]) > 0 and int(row[ACTION_F_TYPE]) == SAC_TYPE:   # 合法牺牲行
            src = int(row[ACTION_F_SOURCE])
            if cur is not None and cur[0] == src:
                cur = (src, cur[1] + 1)
            else:
                if cur is not None:
                    sac_groups.append(cur[1])
                cur = (src, 1)
        else:
            if cur is not None:
                sac_groups.append(cur[1])
                cur = None
        obs, _r, done, truncated, info = env.step(a)
        steps += 1
        done = done or truncated
    if cur is not None:
        sac_groups.append(cur[1])
    winner = str(info.get("winner", "") or "")
    model_seat = int(info.get("modelSeat", -1))
    model_win = bool(winner) and model_seat in (0, 1) and int(winner) == model_seat
    rec = {"deck": deck, "opp": opp, "seat": f"P{seat + 1}", "seed": seed,
           "turns": int(info.get("turn", 0) or 0), "steps": steps,
           "winner": winner, "model_win": model_win,
           "sac_sizes": sac_groups, **behavior_of(info)}
    if not quiet:
        print(f"  [game] {deck}|{opp}/P{seat + 1} seed={seed} "
              f"{'WIN' if model_win else ('TIMEOUT' if info.get('timeout') else 'LOSS')} "
              f"steps={steps} mSac={rec['mSac']} mChain={rec['mChain']} "
              f"mAgrosTurn={rec['mAgrosTurn']} mGrow={rec['mGrow']}")
    return rec


def run_script_game(env, deck, opp, seat, seed):
    """脚本臂单局：opponent=script，reset 即整局跑完（script_baseline 同口径，
    含其 +1 种子偏移），无决策步 → 献祭直方图为空，只取终局行为计数。"""
    obs, info = env.reset(options={"opponent": "script", "theme": opp,
                                   "modelTheme": deck, "modelSeat": seat,
                                   "seed": seed + 1})
    winner = str(info.get("winner", "") or "")
    model_seat = int(info.get("modelSeat", -1))
    return {"deck": deck, "opp": opp, "seat": f"P{seat + 1}", "seed": seed,
            "turns": int(info.get("turn", 0) or 0), "steps": 0,
            "winner": winner,
            "model_win": bool(winner) and model_seat in (0, 1) and int(winner) == model_seat,
            "sac_sizes": [], **behavior_of(info)}


def summarize(per_game, thresholds):
    games = len(per_game)
    t5 = sum(1 for g in per_game if 1 <= g["mAgrosTurn"] <= 10)
    chain = sum(1 for g in per_game if g["mChain"] > 0)
    full = sum(1 for g in per_game if any(s >= FULL_SAC_TARGETS for s in g["sac_sizes"]))
    grow = sum(g["mGrow"] for g in per_game)
    hist = {}
    for g in per_game:
        for s in g["sac_sizes"]:
            hist[str(s)] = hist.get(str(s), 0) + 1
    return {
        "t5_agros_rate": round(t5 / games, 4) if games else None,
        "chain_rate": round(chain / games, 4) if games else None,
        "full_sac_rate": round(full / games, 4) if games else None,
        "wotu_per_game": round(grow / games, 4) if games else None,
        "_thresholds": thresholds,
    }, hist


def main():
    ap = argparse.ArgumentParser(
        description="绿行为门探针：跑真局产出 teacher_behavior.json（§8.2 四项 + 献祭直方图；"
                    "动作=采样口径；--opponent script 做脚本对照臂）")
    ap.add_argument("--run", required=True, help="run 名（相对 logs/）或 run 目录")
    ap.add_argument("--split", default="last", choices=["best", "last"])
    ap.add_argument("--deck", default="", help="red/green/blue；缺省读 run config 的 model_theme")
    ap.add_argument("--opponent", default="simpleai", choices=["simpleai", "script"],
                    help="simpleai=模型臂（行为门证据）；script=脚本臂对照（§8.2 标定）")
    ap.add_argument("--games-per-cell", type=int, default=96,
                    help="每个 (对手主题 × 座位) 格局数（默认 96 → 6 格 × 96 = 576 局；"
                         "脚本臂标定用 20–50 总局时传小值，如 4 → 24 局）")
    ap.add_argument("--seed", type=int, default=20261122, help="配对种子基（同 eval_matrix）")
    ap.add_argument("--out", default="", help="输出 json（默认 <run>/teacher_behavior.json）")
    ap.add_argument("--quiet", action="store_true", help="不打印逐局行")
    ap.add_argument("--unity-path", default="", help="Unity.exe 路径（默认自动发现）")
    ap.add_argument("--project-path", default="",
                    help="Unity 工程根（默认脚本父目录；从主仓脚本跑副本局时必传副本根）")
    cli = ap.parse_args()

    run_dir = resolve_run_dir(cli.run)
    ckpt, meta = pick_ckpt(run_dir, cli.split)
    args, _ = args_from_run(run_dir)
    if cli.unity_path:
        args.unity_path = cli.unity_path
    if cli.project_path:
        args.project_path = cli.project_path

    cfg = {}
    cfg_p = run_dir / "config.json"
    if cfg_p.exists():
        cfg = json.loads(cfg_p.read_text(encoding="utf-8"))
    deck = cli.deck or cfg.get("model_theme", "")
    if deck not in THEME_KEYS:
        raise SystemExit(f"[!] --deck 缺省且 config model_theme={deck!r} 不可用"
                         f"（须 red/green/blue 专精 run）")
    if deck != "green":
        print(f"[注意] deck={deck}：行为门语义只对绿定义（§8.2），本次产出仅作诊断对照")

    # ===== 建网络 + 载权重（eval_checkpoint 同口径）=====
    agent = create_tide_agent(channels=args.channels, rnn_channels=args.rnn_channels,
                              rnn_type=args.rnn_type)
    dummy_obs = sample_input()
    dummy_obs = jax.tree.map(lambda x: x[None, ...] if x is not None else None, dummy_obs)
    params = agent.init(jax.random.PRNGKey(args.seed), init_rstate(args.rnn_type), dummy_obs)
    with open(ckpt, "rb") as f:
        # 跨树加载（2026-11-26 用户定，决策③）：strict from_bytes 在参数树变更（S3-3 aux 2→3 等）
        # 后拒载旧 ckpt——与 eval_checkpoint/train --init-params 同策 merge_params_by_shape
        # （同名同形状继承，形状变化/new 模块保留本次初始化）。
        old_params = flax.serialization.from_bytes(None, f.read())
        params, _merge_report = merge_params_by_shape(params, old_params)
    jit_apply = jax.jit(agent.apply)
    print(f"[ckpt] {ckpt}（split={meta.get('_split')} step={meta.get('step')}）", flush=True)

    env = TideEnv(
        unity_path=args.unity_path,
        project_path=args.project_path,
        max_steps=args.max_episode_steps,
        reward_lambda=args.reward_lambda,
        opponent=cli.opponent,
        model_theme=deck,
        model_doped=args.model_doped,
        startup_timeout=args.startup_timeout,
        step_timeout=args.step_timeout,
    )

    per_game = []
    t0 = time.time()
    script_arm = cli.opponent == "script"
    total = len(DECKS) * 2 * cli.games_per_cell
    print(f"\n行为探针：deck={deck} · {'脚本臂（对照，不进门）' if script_arm else '模型臂（采样口径）'}"
          f" × 3 对手 × 2 座位 × {cli.games_per_cell} 局 = {total} 局"
          f"（种子基 {cli.seed}，与教师矩阵同批）\n", flush=True)
    try:
        for opp in DECKS:
            off = pair_seed_offset((deck,), DECKS, deck, opp)
            for seat in (0, 1):
                for ep in range(cli.games_per_cell):
                    seed = cli.seed + off + seat * 500 + ep
                    if script_arm:
                        per_game.append(run_script_game(env, deck, opp, seat, seed))
                    else:
                        rng = jax.random.fold_in(jax.random.PRNGKey(0xE0A1),
                                                 seed & 0xFFFFFFFF)   # evaluate_greedy P1 同式
                        per_game.append(run_model_game(jit_apply, params, env, args,
                                                       deck, opp, seat, seed, rng,
                                                       quiet=cli.quiet))
                    if not cli.quiet and (len(per_game) % 24 == 0):
                        print(f"  [progress] {len(per_game)}/{total}", flush=True)
    finally:
        env.close()

    rates, hist = summarize(per_game, DEFAULT_BEHAVIOR_THRESHOLDS)
    thresholds = rates.pop("_thresholds")
    doc = {
        "deck": deck,
        "run": run_dir.name,
        "step": meta.get("step"),
        "opponent": cli.opponent,
        "source": "script" if script_arm else "model",
        "games": len(per_game),
        "seed": cli.seed,
        "caliber": "none" if script_arm else "sample",
        "t5_agros_rate": rates["t5_agros_rate"],
        "chain_rate": rates["chain_rate"],
        "full_sac_rate": rates["full_sac_rate"],
        "wotu_per_game": rates["wotu_per_game"],
        "sac_target_hist": hist,
        "sac_hist_lower_bound": True,
        "sac_hist_note": "按选中动作行 type=5 连续同源分组的行流近似（A6 后每目标一行）；"
                         "同源两次独立发动会并组 → 只低估。mSac 是 C# 权威计数。",
        "thresholds_used": dict(thresholds),
        "wins": sum(1 for g in per_game if g["model_win"]),
        "elapsed_sec": round(time.time() - t0, 1),
        "per_game": per_game,
    }
    out = Path(cli.out) if cli.out else run_dir / "teacher_behavior.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(doc, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\n===== 行为摘要（deck={deck} · {doc['games']} 局 · source={doc['source']}）=====")
    print(f"  T5 阿格洛斯率 {doc['t5_agros_rate']}（阈值 ≥{thresholds['t5_agros_rate']}）"
          f" · 链率 {doc['chain_rate']}（≥{thresholds['chain_rate']}）"
          f" · 满献率 {doc['full_sac_rate']}（≥{thresholds['full_sac_rate']}）"
          f" · 沃土/局 {doc['wotu_per_game']}（≥{thresholds['wotu_per_game']}）")
    print(f"  献祭目标数直方图（下界）：{hist or '—'}")
    print(f"[out] {out}")


if __name__ == "__main__":
    main()
