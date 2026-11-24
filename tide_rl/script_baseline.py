"""脚本基线重测工具（A5，2026-11-24 回到一期训练目标·B2 配套）：全脚本局 18 格矩阵。

口径：
  - 每格 = 「脚本执该预组(mt) × 该座位 × 对脚本主题(opp)」胜率——与 SCRIPT_MATRIX_BASELINES
    同构（9 对 × 2 座位）；opponent="script"（驱动器 scriptVsScript：reset 即整局跑完）；
  - 种子方案与 eval_matrix.py **相同**（同基座 + 对偏移×1000 + 座次×500 + ep）——保证可复现、
    与模型矩阵评估可比。**注意这不是配对发牌**：模型行动会改变抽牌流，判 Δ 时仍按两侧独立
    算（σ_Δ≈5pp@200/格）——phase1_criterion.delta_ci 已按此口径合成；
  - 产出：控制台矩阵 + json（cells 18 格 [胜,局] + Wilson95）+ 可直接回填
    phase1_criterion.SCRIPT_MATRIX_BASELINES 的 dict 字面量打印。

用法（训练副本 tide_rl 目录——主工程 Library 被编辑器占着起不了 batchmode）：
  python script_baseline.py --episodes 200 --seed 20261122
B2 重测后：把打印的 dict 回填 phase1_criterion.py 两表并升 BASELINE_ERA（同批改
train_tide_complete 转发注释与 AI训练说明 §0.2）。
"""
import argparse
import json
import os
import time
from pathlib import Path

from tide_env import TideEnv

DECKS = ("red", "green", "blue")
MAX_STEPS = 2000  # 与训练默认一致（超时判负口径下脚本局很少触顶）
CN = {"red": "红", "green": "绿", "blue": "蓝"}


def pair_seed_offset(decks, mt, opp):
    """与 eval_matrix.pair_seed_offset 同构：(对下标)*1000——两侧枚举一致即同偏移。"""
    return (decks.index(mt) * len(decks) + decks.index(opp)) * 1000


def wilson_ci(w, n, z=1.96):
    """Wilson 95% 区间（与 eval_matrix/phase1_criterion 同式；n=0 → (0,0)）。"""
    if n <= 0:
        return 0.0, 0.0
    p = w / n
    denom = 1 + z * z / n
    center = (p + z * z / (2 * n)) / denom
    half = z * (p * (1 - p) / n + z * z / (4 * n * n)) ** 0.5 / denom
    return round(max(0.0, center - half), 4), round(min(1.0, center + half), 4)


def run_baseline(env, per_pair, seed_base, decks):
    cells = {}
    lens = {}
    for mt in decks:
        for opp in decks:
            off = pair_seed_offset(decks, mt, opp)
            for seat in (0, 1):
                n = per_pair // 2 if seat == 0 else per_pair - per_pair // 2
                w = 0
                len_sum = 0
                for ep in range(n):
                    obs, info = env.reset(options={
                        "opponent": "script", "theme": opp, "modelTheme": mt,
                        "modelSeat": seat, "seed": seed_base + off + seat * 500 + ep + 1,
                    })
                    # 脚本局：reset 即整局终态；winner "0"/"1"，空=超时/平局
                    winner = str(info.get("winner", ""))
                    if winner and int(winner) == seat:
                        w += 1
                    len_sum += int(info.get("avgLen") or info.get("episodeLen")
                                   or info.get("turn") or 0)
                cells[(mt, opp, seat)] = [w, n]
                lens[(mt, opp, seat)] = len_sum / max(n, 1)
                print(f"[base] {CN[mt]}|{CN[opp]}/P{seat + 1}: {w}/{n}"
                      f"（seed 基 {seed_base + off + seat * 500 + 1}）", flush=True)
    return cells, lens


def main():
    ap = argparse.ArgumentParser(description="全脚本局 18 格基线重测（B2）——opponent=script，"
                                             "种子方案与 eval_matrix 同构")
    ap.add_argument("--episodes", type=int, default=200, help="每对局数（默认 200 → 每格 100）")
    ap.add_argument("--seed", type=int, default=20261122)
    ap.add_argument("--out", default="", help="结果 json（默认 logs/script_baseline_<seed>.json）")
    cli = ap.parse_args()

    env = TideEnv(
        unity_path=os.environ.get("UNITY_PATH") or None,       # 空=Hub 扫描自动匹配工程版本
        project_path=os.environ.get("TIDE_PROJECT_PATH") or None,
        max_steps=MAX_STEPS,
        reward_lambda=0.0,  # 基线局无模型——λ 不参与
        opponent="script",
    )
    t0 = time.time()
    print(f"脚本基线重测：9 对 × 2 座位 × {cli.episodes // 2}/{cli.episodes - cli.episodes // 2} 局"
          f"（种子基 {cli.seed}，协议=eval_matrix v2 同构）", flush=True)
    try:
        cells, lens = run_baseline(env, cli.episodes, cli.seed, DECKS)
    finally:
        env.close()

    print("\n" + "=" * 60)
    print("18 格基线（可直接回填 phase1_criterion.SCRIPT_MATRIX_BASELINES）：")
    print("SCRIPT_MATRIX_BASELINES = {")
    for (mt, opp, seat), (w, n) in sorted(cells.items()):
        wr = w / max(n, 1)
        lo, hi = wilson_ci(w, n)
        print(f"    (\"{mt}\", \"{opp}\", {seat}): {wr:.3f},   # {w}/{n} CI95 {lo*100:.1f}~{hi*100:.1f}pp")
    print("}")

    out = Path(cli.out) if cli.out else Path("logs") / f"script_baseline_{cli.seed}.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps({
        "protocol": "script-baseline-v1(eval_matrix-seed-compatible)",
        "seed_base": cli.seed,
        "episodes_per_pair": cli.episodes,
        "elapsed_sec": round(time.time() - t0, 1),
        "cells": {f"{mt}|{opp}|P{seat + 1}": {"wins": w, "n": n, "win_rate": round(w / max(n, 1), 4),
                                              "ci95": wilson_ci(w, n),
                                              "avg_len": round(lens.get((mt, opp, seat), 0.0), 1)}
                  for (mt, opp, seat), (w, n) in sorted(cells.items())},
    }, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"[out] {out}", flush=True)


if __name__ == "__main__":
    main()
