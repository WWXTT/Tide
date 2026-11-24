"""一期起点差距表（A2，2026-11-24 回到一期训练目标）：用新判据（phase1_criterion.judge）
**离线回放**历史 run 的评估行（只读，不改任何 run 数据），产出各 run 终点对一期判据的差距。

用法（在 Tide_train/tide_rl 或主仓 tide_rl 下）：
  python replay_phase1_gap.py                      # 默认四组（1b/1c/D2d/自对弈对照）
  python replay_phase1_gap.py --runs 标签=目录名 ...  # 显式指定（目录名=tide_rl/logs/ 下 run 目录）
  python replay_phase1_gap.py --pick best          # 取 selector 最优更新点（默认 last=最后一个矩阵点）

产物：stdout + <主仓>/.ztmp/phase1_gap_replay.md（只读回放，不作为任何判据依据——历史 run
全在旧环境/旧口径上训出，本表只回答「起点差多少」）。
"""
import argparse
import io
import json
import statistics
import sys
from pathlib import Path

from phase1_criterion import (
    PRESET_SEAT_BASELINES, SCRIPT_MATRIX_BASELINES, SATURATED_CELLS,
    judge, select_value, candidate_gate,
)

DEFAULT_RUNS = [
    ("1b(v20全量重训)", "tide_ppo_selfplay_v2c__42__1795195536"),
    ("1c(续训红镜回退)", "tide_ppo_selfplay_v2c__42__1795260108"),
    ("D2d早期(1795362097)", "tide_ppo_selfplay_v2c__42__1795362097"),
    ("自对弈长跑(1795370857)", "tide_ppo_selfplay_v2c__42__1795370857"),
]


def load_rows(run_dir):
    rows = []
    p = Path(run_dir) / "eval.jsonl"
    if not p.exists():
        return rows
    for line in io.open(p, encoding="utf-8"):
        line = line.strip()
        if line:
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                continue  # 尾部残行（崩溃截断）
    return rows


def matrix_cells_at(rows, pick):
    """{(mt,opp): row} 取矩阵行的目标更新点 → {(mt,opp,seat): (w,n)}。"""
    matrix = [r for r in rows if r.get("metric") == "vs_script_matrix"]
    if not matrix:
        return {}, None
    by_update = {}
    for r in matrix:
        by_update.setdefault(r.get("update"), []).append(r)
    if pick == "best":
        # selector 行（旧口径值）取最大者为该 run 的「最优更新点」
        sels = [r for r in rows if r.get("metric") == "selector" and r.get("win_rate") is not None]
        update = max(sels, key=lambda r: r["win_rate"])["update"] if sels else max(by_update)
    else:
        update = max(by_update)
    cells = {}
    for r in by_update.get(update, []):
        mt, opp = r["theme"].split("|")
        for pkey, seat in (("p1", 0), ("p2", 1)):
            w, n = r.get(pkey, [0, 0])
            if n:
                cells[(mt, opp, seat)] = (w, n)
    return cells, update


def six_cells_at(rows):
    """6 格行（旧 metric=vs_script_cell，theme='red/P1'）→ {(preset, seat): (w, n)}。"""
    cells = {}
    last = {}
    for r in rows:
        if r.get("metric") == "vs_script_cell" and "/" in str(r.get("theme", "")):
            last[r["theme"]] = r
    for theme, r in last.items():
        tk, p = theme.split("/")
        seat = int(p[1:]) - 1
        n = r.get("episodes") or (r.get("wins", 0) + r.get("losses", 0) + r.get("draws", 0))
        if n:
            cells[(tk, seat)] = (r.get("wins", 0), n)
    return cells


def fmt_cells(cells):
    ds = {}
    for key, (w, n) in cells.items():
        base = SCRIPT_MATRIX_BASELINES.get(key)
        if base is not None:
            ds[key] = w / n - base
    if not ds:
        return "—"
    regular = [d for k, d in ds.items() if k not in SATURATED_CELLS]
    med = statistics.median(regular) if regular else float("nan")
    return (f"min Δ {min(regular)*100:+.1f}pp / 中位 {med*100:+.1f}pp / "
            f"Δ≥0 {sum(1 for d in regular if d >= 0)}/{len(regular)}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", nargs="*", default=[f"{a}={b}" for a, b in DEFAULT_RUNS],
                    help="标签=run目录名（tide_rl/logs/ 下；默认四组历史对照）")
    ap.add_argument("--pick", default="last", choices=("last", "best"))
    args = ap.parse_args()

    # logs 根：主仓与 Tide_train 副本可能同名各有一份（历史 run 大多在副本、主仓是旧拷贝）——
    # 两根都读，取 eval 行数多的那份（行数少=被截断/同步前的残拷贝）。
    roots = [Path("logs"),
             Path(r"C:/UnityProject/Tide_train/tide_rl/logs")]
    out = ["# 一期起点差距表（新判据离线回放·只读）",
           "",
           "> 判据 = phase1_criterion.judge（每格 Δ≥0+中位数 ≥+15pp+饱和格不退步；骑线=needs_more）。",
           "> 历史 run 全部训于旧环境/旧口径——本表只回答「起点差多少」，不作任何判据依据。",
           ""]
    print(out[0])
    for spec in args.runs:
        label, dirname = spec.split("=", 1)
        cands = [(r / dirname) for r in roots if (r / dirname).exists()]
        if not cands:
            out.append(f"## {label}\n\n- run 目录不存在：{dirname}\n")
            print(f"{label}: run 目录不存在 {dirname}")
            continue
        run_dir, rows = max(((d, load_rows(d)) for d in cands), key=lambda dr: len(dr[1]))
        cells, update = matrix_cells_at(rows, args.pick)
        lines = [f"## {label}", "",
                 f"- run：`{dirname}`（{len(rows)} 行 eval）"]
        if cells:
            r = judge(cells)
            sel, worst = select_value(cells)
            cand = candidate_gate(cells)
            lines.append(f"- 评估点：update {update}（--pick {args.pick}）；矩阵覆盖 {r['covered']}/18 格")
            lines.append(f"- 概况：{fmt_cells(cells)}")
            lines.append(f"- judge：{'**达标**' if r['pass'] else '未达标'}（中位数 "
                         f"{r['median_delta']*100:+.1f}pp；needs_more {len(r['needs_more'])} 格）")
            if worst:
                lines.append(f"- 最差格：{worst[0]}|{worst[1]}/P{worst[2] + 1}"
                             f"（Wilson 下界 Δ {sel * 100:+.1f}pp）")
            fails = [c for c in r["per_cell"] if c.get("verdict") == "fail"]
            if fails:
                worst3 = sorted(fails, key=lambda c: c["delta"])[:3]
                lines.append("- 明显未达（区间不跨 0）："
                             + "、".join(f"{c['key'][0]}|{c['key'][1]}/P{c['key'][2]+1} "
                                         f"{c['delta']*100:+.0f}pp" for c in worst3))
        else:
            six = six_cells_at(rows)
            if six:
                ds = {k: w / n - PRESET_SEAT_BASELINES.get(k, float("nan")) for k, (w, n) in six.items()}
                regular = [d for d in ds.values() if d == d]
                med = statistics.median(regular) if regular else float("nan")
                lines.append(f"- 评估点：矩阵行缺失（早于 C3 矩阵口径）→ 6 格回放："
                             f"min Δ {min(regular)*100:+.1f}pp / 中位 {med*100:+.1f}pp / "
                             f"Δ≥0 {sum(1 for d in regular if d >= 0)}/{len(regular)}")
            else:
                lines.append("- 无可用评估行（vs_script_matrix / vs_script_cell 均缺失）")
        cfg_p = run_dir / "config.json"
        if cfg_p.exists():
            try:
                cfg = json.load(open(cfg_p, encoding="utf-8"))
                lines.append(f"- 口径：opponent={cfg.get('opponent')} / model_theme={cfg.get('model_theme')}"
                             f" / criterion_era={cfg.get('criterion_era', '（旧 run 无判据块——A1 之前）')}")
            except Exception:
                pass
        lines.append("")
        out.extend(lines)
        print("\n".join(lines[1:]))
    dst = Path(r"C:/Users/Administrator/Desktop/UnityProject/Tide/.ztmp/phase1_gap_replay.md")
    dst.parent.mkdir(parents=True, exist_ok=True)
    dst.write_text("\n".join(out), encoding="utf-8")
    print(f"\n[已写] {dst}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
