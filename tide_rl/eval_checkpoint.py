"""
对已有训练 checkpoint 跑一次 **6 格评估**（模型执三套预组 × 先/后手），只评估、不训练。

起因（2026-11-21）：v20 绿 bootcamp（`--model-theme green --eval-theme green`）每 50k 只评出
green/P1、green/P2 两格，红/蓝四格全空。`train_tide_complete.py` 已把评估模型卡组解耦成
`--eval-model-theme`（默认 all），但在跑的训练进程改不了参数——本脚本给这种「已经在跑的
单主题 run」补一次完整 6 格评估，也可在训练跑完后对 best/last 复核。

注意（很重要的口径）：单主题训练（绿 bootcamp）里模型从没训过红/蓝两套，这四格评出来是
**未训练策略的 floor**，只作参照，不能拿去当验收；验收只认该模型训练覆盖的格子。

6 格定义（`docs/AI训练说明.md` §0.2 / §3.4.1）：每格 = 模型执该预组 × 该座位（P1 先手 /
P2 后手），对手 = 脚本三预组各 `--episodes` 局（座次随机），门槛 = 该预组该座位脚本基线
+15pp 封顶 100%。多模型卡组时每组合局数 = max(10, episodes // 卡组数)，故 3 卡组 × 3 主题
× 16 = **144 局**（每格约 24 局，与训练内评估同口径）。

必须在**训练副本**目录跑（Unity 工程 = 脚本父目录）：
    cd C:\\UnityProject\\Tide_train\\tide_rl
    python eval_checkpoint.py --run logs/tide_ppo_v20_green_bootcamp__42__1795178705
主工程 `Desktop\\UnityProject\\Tide` 的 Library 被编辑器占着，在那里起第二个 batchmode 会抢锁。

输出：控制台 6 格表 + `<run>/six_cell_eval.json`（`--out` 可改路径）。
"""

import argparse
import json
import time
from dataclasses import fields as dataclass_fields
from pathlib import Path

import jax
import flax.serialization

from tide_agent import create_tide_agent, merge_params_by_shape
from tide_features import init_rstate, sample_input
from tide_env import TideEnv

from train_tide_complete import (
    Args, THEME_KEYS, cell_threshold, eval_model_themes,
    evaluate_greedy_multi, parse_eval_themes,
)


def resolve_run_dir(spec: str) -> Path:
    """`--run` 可以是 run 名（相对 logs/）或 run 目录路径。"""
    p = Path(spec)
    if p.is_dir():
        return p
    p2 = Path("logs") / spec
    if p2.is_dir():
        return p2
    raise SystemExit(f"[!] 找不到 run 目录：{spec}（也不是 logs/{spec}）")


def pick_ckpt(run_dir: Path, split: str) -> tuple[Path, dict]:
    """返回 (params.msgpack 路径, meta 字典)。split=best 缺失时回落 last。

    baseline_era 闸（2026-11-22 用户定案）：best 的 meta 未盖当前 BASELINE_ERA 戳 =
    D2a（正牌技能透传）之前的病态环境里选出，**作废拒载**（提示改 --split last）；
    last 不受影响（权重本身可用，只是当时口径的选点不可信）。"""
    from train_tide_complete import BASELINE_ERA
    order = ["best", "last"] if split == "best" else ["last", "best"]
    for sub in order:
        ck = run_dir / sub / "params.msgpack"
        if ck.exists():
            meta = {}
            meta_file = run_dir / sub / "meta.json"
            if meta_file.exists():
                try:
                    meta = json.loads(meta_file.read_text(encoding="utf-8"))
                except json.JSONDecodeError:
                    meta = {}
            meta["_split"] = sub
            if sub == "best" and meta.get("baseline_era") != BASELINE_ERA:
                raise SystemExit(
                    f"[!] {run_dir}/best 作废（baseline_era={meta.get('baseline_era')!r} ≠ "
                    f"{BASELINE_ERA!r}——D2a 之前的检查点在病态环境〔AutoPick 技能/无策略响应〕"
                    f"里选出）。请改用 --split last，或用新纪元重训后再取 best。")
            return ck, meta
    raise SystemExit(f"[!] {run_dir} 下没有 best/ 或 last/ 的 params.msgpack")


def args_from_run(run_dir: Path) -> tuple[Args, Path | None]:
    """用 run 的 config.json 还原网络结构/环境参数（结构必须与训练时一致才能载权重）。"""
    args = Args()
    cfg_path = run_dir / "config.json"
    loaded = None
    if cfg_path.exists():
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
        valid = {f.name for f in dataclass_fields(Args)}
        for k, v in cfg.items():
            if k not in valid:
                continue
            if k == "pool_weights":
                v = tuple(v)
            setattr(args, k, v)
        loaded = cfg_path
        print(f"[cfg] 已按 run 配置还原：channels={args.channels} rnn={args.rnn_channels}"
              f"/{args.rnn_type} max_steps={args.max_episode_steps}（{cfg_path}）")
    else:
        print("[cfg] 无 config.json → 用 Args 默认值建网络（尺寸必须与训练一致，否则载权重会报形状错误）")
    return args, loaded


def main():
    ap = argparse.ArgumentParser(description="对已有 checkpoint 跑 6 格评估（模型执三套 × 先/后手，不训练）")
    ap.add_argument("--run", default="", help="run 名（相对 logs/）或 run 目录；与 --ckpt 二选一")
    ap.add_argument("--ckpt", default="", help="params.msgpack 路径（直接指定权重）")
    ap.add_argument("--split", default="best", choices=["best", "last"], help="取 best 还是 last（默认 best，缺则回落另一个）")
    ap.add_argument("--episodes", type=int, default=50, help="每个对手主题的局数（默认 50 → 3 卡组×3 主题×16=144 局）")
    ap.add_argument("--model-themes", default="all",
                    help="评估局模型卡组：all=三预组（默认）；也可 red / red,green")
    ap.add_argument("--themes", default="", help="对手主题：空=三主题（默认）；也可 red 或 red,green")
    ap.add_argument("--out", default="", help="结果 json 路径（默认 <run>/six_cell_eval.json）")
    ap.add_argument("--unity-path", default="", help="Unity.exe 路径（默认自动发现，同训练）")
    ap.add_argument("--project-path", default="", help="Unity 工程路径（默认脚本父目录 = 训练副本）")
    cli = ap.parse_args()

    if not cli.run and not cli.ckpt:
        raise SystemExit("[!] 需要 --run <run名|目录> 或 --ckpt <params.msgpack>")

    if cli.run:
        run_dir = resolve_run_dir(cli.run)
        ckpt, meta = pick_ckpt(run_dir, cli.split)
    else:
        run_dir = Path(cli.ckpt).resolve().parent.parent
        ckpt, meta = Path(cli.ckpt), {"_split": "custom"}

    args, _ = args_from_run(run_dir)
    if cli.unity_path:
        args.unity_path = cli.unity_path
    if cli.project_path:
        args.project_path = cli.project_path

    eval_themes = parse_eval_themes(cli.themes)
    mt_spec = (cli.model_themes or "").strip()
    if mt_spec == "all":
        model_themes = ("red", "green", "blue")
    else:
        model_themes = tuple(t.strip() for t in mt_spec.split(",") if t.strip())
        bad = [t for t in model_themes if t not in THEME_KEYS]
        if bad or not model_themes:
            raise SystemExit(f"[!] --model-themes 非法：{bad or mt_spec!r}（可选 {list(THEME_KEYS)} 或 all）")

    # ===== 建网络 + 载权重（照 train() 的 L555-580 口径：dummy obs/rstate 只用于 init 出参数树）=====
    agent = create_tide_agent(channels=args.channels, rnn_channels=args.rnn_channels, rnn_type=args.rnn_type)
    dummy_obs = sample_input()
    dummy_obs = jax.tree.map(lambda x: x[None, ...], dummy_obs)
    params = agent.init(jax.random.PRNGKey(args.seed), init_rstate(args.rnn_type), dummy_obs)
    # 跨树加载（2026-11-26 用户定）：strict from_bytes 在参数树变更（如 S3-3 aux 2→3）后拒载旧 ckpt；
    # 改为与训练 --init-params 同策的 merge_params_by_shape——同名同形状继承，形状变化/new 模块保留本次
    # 初始化（aux 头 fresh 不影响 logits），并打印 loaded/fresh 清单。
    with open(ckpt, "rb") as f:
        old_params = flax.serialization.from_bytes(None, f.read())
    params, _merge_report = merge_params_by_shape(params, old_params)
    jit_apply = jax.jit(agent.apply)
    print(f"[ckpt] {ckpt}（split={meta.get('_split')} step={meta.get('step')} "
          f"update={meta.get('update')} time={meta.get('time')}）", flush=True)

    env = TideEnv(
        unity_path=args.unity_path,
        project_path=args.project_path,
        max_steps=args.max_episode_steps,
        reward_lambda=args.reward_lambda,
        opponent="simpleai",
        model_theme=args.model_theme,
        model_doped=args.model_doped,
        startup_timeout=args.startup_timeout,
        step_timeout=args.step_timeout,
    )

    t0 = time.time()
    print(f"\n6 格评估：模型卡组 {model_themes} × 对手主题 {eval_themes} × 每组合 "
          f"{max(10, cli.episodes // len(model_themes)) if len(model_themes) > 1 else cli.episodes} 局\n", flush=True)
    try:
        # 2026-11-21：evaluate_greedy_multi 返回值扩为 8 元组（末位=矩阵口径，本脚本未用；
        # 要矩阵明细用 eval_matrix.py）
        win_rate, wins, losses, draws, avg_len, theme_stats, cell_stats, _matrix = evaluate_greedy_multi(
            jit_apply, params, env, args, num_episodes=cli.episodes,
            opponent="simpleai", themes=eval_themes, model_themes=model_themes,
        )
    finally:
        env.close()
    elapsed = time.time() - t0
    games = wins + losses + draws

    # ===== 6 格表 =====
    rows = {}
    n_pass = n_fail = n_missing = 0
    print(f"{'格':<10}{'胜率':>8}{'k/n':>12}{'门槛':>9}{'gap':>9}  判定")
    print("-" * 58)
    for key in THEME_KEYS:
        for seat in (0, 1):
            label = f"{key}/P{seat + 1}"
            c = (cell_stats or {}).get((key, seat))
            thr = cell_threshold(key, seat)
            if not c or c[1] == 0:
                n_missing += 1
                print(f"{label:<10}{'—':>8}{'—':>12}{thr * 100:>8.1f}%{'—':>9}  缺格（本卡组未评）")
                rows[label] = {"wins": 0, "games": 0, "win_rate": None, "threshold": thr,
                               "gap_pp": None, "pass": None}
                continue
            wr = c[0] / c[1]
            gap = None if thr is None else wr - thr
            ok = None if thr is None else wr >= thr
            n_pass += 1 if ok else 0
            n_fail += 0 if ok else 1
            rows[label] = {"wins": c[0], "games": c[1], "win_rate": round(wr, 4),
                           "threshold": None if thr is None else round(thr, 4),
                           "gap_pp": None if gap is None else round(gap * 100, 1),
                           "pass": ok}
            mark = "达标" if ok else "未达"
            thr_s = "—" if thr is None else f"{thr * 100:.1f}%"
            gap_s = "—" if gap is None else f"{gap * 100:+.1f}pp"
            print(f"{label:<10}{wr * 100:>7.1f}%{f'{c[0]}/{c[1]}':>12}{thr_s:>9}{gap_s:>9}  {mark}")
    print("-" * 58)
    print(f"按格计数：达标 {n_pass} / 未达 {n_fail} / 缺格 {n_missing}")
    if n_missing == 0 and n_pass == len(rows):
        print("→ 6 格全达标（单次评估，仅作过程信号；验收按 §3.4.1 池化三态）")

    result = {
        "run": run_dir.name,
        "ckpt": str(ckpt),
        "split": meta.get("_split"),
        "step": meta.get("step"),
        "update": meta.get("update"),
        "ckpt_time": meta.get("time"),
        "created": time.strftime("%Y-%m-%d %H:%M:%S"),
        "model_themes": list(model_themes),
        "opponent_themes": list(eval_themes),
        "episodes_arg": cli.episodes,
        "games": games,
        "win_rate_overall": round(win_rate, 4),
        "wins": wins, "losses": losses, "draws": draws,
        "avg_len": round(avg_len, 1),
        "elapsed_s": round(elapsed, 1),
        "theme_win_rates": {k: round(t[0] / t[3], 4) for k, t in theme_stats.items() if t[3] > 0},
        "cells": rows,
        "cells_pass": None if n_missing or n_pass != len(rows) else True,
    }
    out = Path(cli.out) if cli.out else (run_dir / "six_cell_eval.json")
    out.parent.mkdir(parents=True, exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
    print(f"\n[done] {games} 局 / {elapsed:.0f}s（{games / max(elapsed, 1e-9):.2f} 局/秒）→ {out}")
    if n_missing:
        print("[注意] 有缺格：--model-themes 或 --themes 覆盖不全，别当 6 格结论用")


if __name__ == "__main__":
    main()
