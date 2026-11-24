"""
矩阵评估：模型执三预组 × 对手三套脚本 × 先/后手 的 18 格胜率矩阵（fork 自
eval_checkpoint.py，2026-11-21；2026-11-22 升评估协议 v2）。

为什么需要它：训练内评估（evaluate_greedy_multi）只落盘两个聚合切片——
cell_stats（模型卡组 × 座位，对手三主题**合并**）与 theme_stats（对手主题，模型
卡组**合并**）——「模型执色 × 对手主题」联合维度在聚合时被丢弃，日志无法还原。
本脚本逐 (model_theme, opponent_theme) 对直接调 evaluate_greedy 并保留该对的
cells（分座位胜负），产出 3×3×2 完整矩阵 + 3×3 汇总。

评估协议 v2（2026-11-22 配对终版；起因：48 局格实测 ±10pp 抽样噪声——1c 红|红
0/48 vs 1b 26/48 被"重复性/跨日漂移"质疑，同检查点跨日读数漂 10.4pp）：
  - 座位 24/24 均分：每对拆两半，modelSeat=0/1 服务端钉死座次各评 episodes//2 局
    （奇数时 P1 少 1；默认 48 → 24/24），不再靠掷币凑均分；
  - 种子钉死：逐局 seed = --seed + 对偏移*1000 + 座次*500 + ep——服务端重播
    DeckRng + 引擎双随机流（GameRng 效果随机 + ZoneContainer 洗牌）。同种子表
    跨检查点重跑 = 两模型打**同一批对局**（真配对：SimpleAI 无随机源、技能卡
    挑选确定性，对局差异只来自模型决策）；
  - avg_len 落盘：每格分座位平均局长 + 合计加权平均写入 json（环境病灶签名用，
    例：绿|红恒 ~13 步被冲死 = headless 环境锅的指纹之一）；
  - Wilson 95% 区间（B3，2026-11-22）：每对合计格 ci95 落盘+打印，不再裸报点估计——
    n=200/对半宽 ≤±6.9pp（p=0.5 最坏）；48 局时 ±13.6pp 只够看趋势。
  env 层断言 info.modelSeat/info.seed 回显一致——对端 Unity 未升级时响亮失败。

用法（必须在训练副本 tide_rl 目录跑；主工程 Library 被编辑器占着，起不了第二个
batchmode；训练占用时同理——本脚本与训练互斥，一进程一局）：
    python eval_matrix.py --run logs/<run名> --split last --episodes 200 --seed 20261122

--episodes = 每个 (模型卡组, 对手主题) 组合的局数（B3 起默认 200 → 每座位 100；
48 = 旧行为，只够看趋势）。总局数 = 3 卡组 × 3 主题 × episodes。
配对复评：两个 run 用同一 --seed 跑。

输出：控制台矩阵表 + <run>/matrix_eval_<split>.json（--out 可改路径；v2 含
protocol/seed_base/avg_len/ci95 字段）。
"""

import argparse
import json
import time
from pathlib import Path

import jax
import flax.serialization

from tide_agent import create_tide_agent
from tide_features import init_rstate, sample_input
from tide_env import TideEnv

from train_tide_complete import (
    Args, THEME_KEYS, evaluate_greedy, parse_eval_themes,
)
from eval_checkpoint import resolve_run_dir, pick_ckpt, args_from_run

DECK_ORDER = ("red", "green", "blue")
CN = {"red": "红", "green": "绿", "blue": "蓝"}
PROTOCOL = "v2-seat-balanced-seeded"


def pair_seed_offset(model_themes, eval_themes, mt, opp):
    """(mt, opp) 对的种子偏移（*1000）——稳定于主题组合，与枚举顺序解耦。

    座次偏移再 +500（见 run_matrix），保证 9 对 × 2 座次 × 各 500 局内无种子碰撞。
    """
    pi = model_themes.index(mt)
    oi = eval_themes.index(opp)
    return (pi * len(eval_themes) + oi) * 1000


def wilson_ci(w, n, z=1.96):
    """Wilson 95% 区间（B3，2026-11-22）：小 n 下比正态近似稳（p=0 或 1 也有有限宽度）。

    n=0 返回 (0, 0)。配套门槛：n=200/对 → 合计格半宽 ≤ ±6.9pp（p=0.5 最坏），
    满足「单格 95% 区间 ≤ ±7pp」验收；分座位格（n/2）约 ±9.7pp，比较分座位格时须知此宽度。
    """
    if n <= 0:
        return 0.0, 0.0
    p = w / n
    denom = 1 + z * z / n
    center = (p + z * z / (2 * n)) / denom
    half = z * (p * (1 - p) / n + z * z / (4 * n * n)) ** 0.5 / denom
    return round(max(0.0, center - half), 4), round(min(1.0, center + half), 4)


def run_matrix(jit_apply, params, env, args, model_themes, eval_themes, per_pair, seed_base):
    """逐 (模型卡组, 对手主题) 对评估并组装矩阵（评估协议 v2）。

    每对拆两半：seat=0（P1 先手）评 per_pair//2 局、seat=1（P2 后手）评余量局
    （默认 48 → 24/24），服务端 modelSeat 钉死座次；每局 seed = seed_base +
    对偏移 + 座次*500 + ep。同 (seed_base, 对, 座次, ep) 跨检查点 = 同一批对局。
    返回 matrix = {(mt, opp): {seats/avg_len/avg_len_total/total/win_rate/seed_offset}}。
    """
    matrix = {}
    for mt in model_themes:
        for opp in eval_themes:
            off = pair_seed_offset(model_themes, eval_themes, mt, opp)
            seat_cells, seat_lens = {}, {}
            for seat in (0, 1):
                n = per_pair // 2 if seat == 0 else per_pair - per_pair // 2
                if n <= 0:
                    continue
                wr, w, l, d, avg_len, _ts, cells = evaluate_greedy(
                    jit_apply, params, env, args, num_episodes=n,
                    opponent="simpleai", theme=opp, model_theme=mt,
                    seat=seat, seed_base=seed_base + off + seat * 500,
                )
                # 座位钉死兜底：本半局 cells 只允许出现请求座次（env 层已断言回显，
                # 这里防聚合错位）；固定 model_theme 时 preset 恒等于 mt
                bad = [k for k in cells if k[1] != seat]
                if bad:
                    raise RuntimeError(f"[matrix] 座位钉死失效：请求 seat={seat} 却出现格 {bad}")
                c = cells.get((mt, seat), [0, 0, 0])
                if c[1] != n:
                    raise RuntimeError(f"[matrix] 座次计数异常：seat={seat} 期望 {n} 局实得 {c[1]} 局")
                seat_cells[seat] = list(c[:2])  # cells 三元（B2 含 len_sum）→ seats 保持 [胜,局]
                seat_lens[seat] = avg_len
            total_w = sum(v[0] for v in seat_cells.values())
            total_n = sum(v[1] for v in seat_cells.values())
            avg_total = (sum(seat_lens[s] * seat_cells[s][1] for s in seat_lens)
                         / max(total_n, 1))
            matrix[(mt, opp)] = {
                "seats": seat_cells,
                "avg_len": seat_lens,
                "avg_len_total": avg_total,
                "total": [total_w, total_n],
                "win_rate": total_w / max(total_n, 1),
                "ci95": wilson_ci(total_w, max(total_n, 1)),
                "seed_offset": off,
            }
            p1 = seat_cells.get(0, [0, 0])
            p2 = seat_cells.get(1, [0, 0])
            lo, hi = matrix[(mt, opp)]["ci95"]
            print(f"[matrix] 模型{CN[mt]} vs {CN[opp]}脚本: {total_w}/{total_n} "
                  f"({matrix[(mt, opp)]['win_rate'] * 100:.1f}% CI95 {lo*100:.1f}~{hi*100:.1f}pp) "
                  f"先手 {p1[0]}/{p1[1]} (len {seat_lens.get(0, 0.0):.1f}) "
                  f"后手 {p2[0]}/{p2[1]} (len {seat_lens.get(1, 0.0):.1f})", flush=True)
    return matrix


def print_matrix(matrix, model_themes, eval_themes):
    print("\n" + "=" * 76)
    print(f"18 格矩阵（协议 v2：座位钉死均分；格内 先手P1/后手P2/合计%，L=合计平均局长）")
    print("-" * 76)
    header = f"{'模型\\对手':<10}" + "".join(f"{CN[o] + '脚本':>20}" for o in eval_themes) + f"{'执色合计':>20}"
    print(header)
    deck_tot = {m: [0, 0] for m in model_themes}
    opp_tot = {o: [0, 0] for o in eval_themes}
    for mt in model_themes:
        cells = []
        for opp in eval_themes:
            e = matrix.get((mt, opp))
            if not e:
                cells.append(f"{'—':>20}")
                continue
            p1 = e["seats"].get(0, [0, 0])
            p2 = e["seats"].get(1, [0, 0])
            t = e["total"]
            cells.append(
                f"{p1[0]}/{p2[0]}/{t[0]} L{e['avg_len_total']:.0f}".rjust(20))
            deck_tot[mt][0] += t[0]; deck_tot[mt][1] += t[1]
            opp_tot[opp][0] += t[0]; opp_tot[opp][1] += t[1]
        row_tot = deck_tot[mt]
        print(f"{CN[mt]:<12}" + "".join(cells) +
              f"  {row_tot[0]}/{row_tot[1]} ({row_tot[0]/max(row_tot[1],1)*100:.1f}%)")
    print("-" * 76)
    print(f"{'对手合计':<12}" + "".join(
        f"  {opp_tot[o][0]}/{opp_tot[o][1]} ({opp_tot[o][0]/max(opp_tot[o][1],1)*100:.1f}%)"
        for o in eval_themes))
    print("=" * 76 + "\n", flush=True)


def main():
    ap = argparse.ArgumentParser(
        description="模型执色 × 对手主题 × 先后手 18 格矩阵评估（不训练；协议 v2=座位钉死均分+种子钉死+avg_len 落盘）")
    ap.add_argument("--run", default="", help="run 名（相对 logs/）或 run 目录")
    ap.add_argument("--ckpt", default="", help="params.msgpack 路径（直接指定权重，与 --run 二选一）")
    ap.add_argument("--split", default="best", choices=["best", "last"])
    ap.add_argument("--episodes", type=int, default=200,
                    help="每个 (模型卡组, 对手主题) 组合局数（B3 2026-11-22 起 48→200："
                         "合计格 Wilson95 半宽 ≤±6.9pp；总 9 对 × 200 = 1800 局）")
    ap.add_argument("--model-themes", default="all", help="all=三预组（默认）；也可 red,green 子集")
    ap.add_argument("--themes", default="", help="对手主题：空=三主题（默认）")
    ap.add_argument("--seed", type=int, default=20261122,
                    help="配对种子基（协议 v2）：逐局 seed=base+对偏移*1000+座次*500+ep；"
                         "两个 run 用同一 --seed 重跑 = 打同一批对局（真配对）")
    ap.add_argument("--out", default="", help="结果 json 路径（默认 <run>/matrix_eval_<split>.json）")
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
    eval_themes = parse_eval_themes(cli.themes)
    mt_spec = (cli.model_themes or "").strip().lower()
    model_themes = DECK_ORDER if mt_spec == "all" else tuple(
        t.strip() for t in mt_spec.split(",") if t.strip())
    bad = [t for t in model_themes if t not in THEME_KEYS]
    if bad or not model_themes:
        raise SystemExit(f"[!] --model-themes 非法：{bad or mt_spec!r}")

    # ===== 建网络 + 载权重（与 eval_checkpoint 同口径）=====
    agent = create_tide_agent(channels=args.channels, rnn_channels=args.rnn_channels,
                              rnn_type=args.rnn_type)
    dummy_obs = sample_input()
    dummy_obs = jax.tree.map(lambda x: x[None, ...], dummy_obs)
    params = agent.init(jax.random.PRNGKey(args.seed), init_rstate(args.rnn_type), dummy_obs)
    with open(ckpt, "rb") as f:
        params = flax.serialization.from_bytes(params, f.read())
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
    print(f"\n矩阵评估（{PROTOCOL}）：模型卡组 {model_themes} × 对手 {eval_themes} × 每对 {cli.episodes} 局"
          f"（座位均分 {cli.episodes // 2}/{cli.episodes - cli.episodes // 2}，"
          f"种子基 {cli.seed}，总 {len(model_themes) * len(eval_themes) * cli.episodes} 局）\n", flush=True)
    try:
        matrix = run_matrix(jit_apply, params, env, args, model_themes, eval_themes,
                            cli.episodes, cli.seed)
    finally:
        env.close()
    print_matrix(matrix, model_themes, eval_themes)

    out = Path(cli.out) if cli.out else run_dir / f"matrix_eval_{meta.get('_split', 'x')}.json"
    dump = {
        "ckpt": str(ckpt), "split": meta.get("_split"), "step": meta.get("step"),
        "episodes_per_pair": cli.episodes,
        "protocol": PROTOCOL,
        "seed_base": cli.seed,
        "elapsed_sec": round(time.time() - t0, 1),
        "matrix": {f"{mt}|{opp}": {
            "P1": e["seats"].get(0, [0, 0]), "P2": e["seats"].get(1, [0, 0]),
            "avg_len": {"P1": round(e["avg_len"].get(0, 0.0), 1),
                        "P2": round(e["avg_len"].get(1, 0.0), 1),
                        "total": round(e["avg_len_total"], 1)},
            "seed_offset": e["seed_offset"],
            "total": e["total"], "win_rate": round(e["win_rate"], 4),
            "ci95": e.get("ci95") or wilson_ci(e["total"][0], max(e["total"][1], 1)),
        } for (mt, opp), e in matrix.items()},
    }
    out.write_text(json.dumps(dump, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"[out] {out}", flush=True)


if __name__ == "__main__":
    main()
