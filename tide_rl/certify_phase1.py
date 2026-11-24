"""一期认证工具（A5，2026-11-24 回到一期训练目标·阶段 D 入口）：读矩阵评估结果，
按 phase1_criterion.judge 出证——逐格 Δ、Wilson 区间、判定、需加测清单。

**硬闸（A1，2026-11-24 DSH 定案：一期只认 opponent=simpleai）**——不满足直接拒绝出证：
  ① run 的 config.json `opponent == "simpleai"`；
  ② config `criterion_era == phase1_criterion.BASELINE_ERA`（B2 重测换纪元后，旧 run 即刻失配）；
  ③ config `layout_version == tide_features.LAYOUT_VERSION`；
  ④ run 目录名含 `simpleai`（A1 起 run 名带口径；旧自对弈 run 名直接可辨）。

用法（副本 tide_rl 目录）：
  python certify_phase1.py --run logs/<run名> --matrix-best <run>/matrix_eval_best.json \
      [--matrix-last <run>/matrix_eval_last.json]      # last 只报告、不作达标依据
  python certify_phase1.py --run logs/<run名> --gate-only                # 只查硬闸
  python certify_phase1.py --matrix-best xx.json --no-gate               # 裸判（诊断用）

矩阵 json = eval_matrix.py 产出（matrix["mt|opp"] 带 P1/P2 [胜,局]）。
退出码：0=达标出证；1=未达标/硬闸拒绝；2=使用错误。
"""
import argparse
import json
import sys
from pathlib import Path

from phase1_criterion import (
    SCRIPT_MATRIX_BASELINES, SATURATED_CELLS, BASELINE_ERA,
    judge, delta_ci, wilson_interval,
)
from tide_features import LAYOUT_VERSION

GATE_ONLY, NO_GATE = "gate-only", "no-gate"


def check_gates(run_dir: Path):
    """A1 硬闸四条；返回 (ok, [失败项描述])。"""
    fails = []
    cfg_p = run_dir / "config.json"
    if not cfg_p.exists():
        return False, [f"config.json 不存在（A1 之前的旧 run 无判据块——不能认证）"]
    cfg = json.loads(cfg_p.read_text(encoding="utf-8"))
    if cfg.get("opponent") != "simpleai":
        fails.append(f"opponent={cfg.get('opponent')!r} ≠ 'simpleai'（一期只认 vs 脚本口径）")
    if cfg.get("criterion_era") != BASELINE_ERA:
        fails.append(f"criterion_era={cfg.get('criterion_era')!r} ≠ 当前 BASELINE_ERA {BASELINE_ERA!r}"
                     f"（基线纪元换了——本 run 训于旧基线时代，不能按当前判据认证）")
    if cfg.get("layout_version") != LAYOUT_VERSION:
        fails.append(f"layout_version={cfg.get('layout_version')!r} ≠ 当前 LAYOUT_VERSION {LAYOUT_VERSION}"
                     f"（环境布局已换，旧 checkpoint 不能认证）")
    if "simpleai" not in run_dir.name:
        fails.append(f"run 目录名 {run_dir.name!r} 不含 'simpleai'（A1 起 run 名带口径——"
                     f"自对弈/旧名 run 一眼可辨）")
    return not fails, fails


def cells_from_matrix_json(path: Path):
    """eval_matrix json → {(mt,opp,seat): (wins,n)}。"""
    data = json.loads(path.read_text(encoding="utf-8"))
    cells = {}
    for pair, e in data.get("matrix", {}).items():
        mt, opp = pair.split("|")
        for pkey, seat in (("P1", 0), ("P2", 1)):
            w, n = e.get(pkey, [0, 0])
            if n:
                cells[(mt, opp, seat)] = (w, n)
    return cells, data


def report(tag, cells, as_verdict):
    r = judge(cells)
    print(f"\n===== 认证判定（{tag}；{'达标依据' if as_verdict else '只报告·不作达标依据'}）=====")
    print(f"{'格':<18}{'基线':>7}{'模型':>7}{'Δ(pp)':>9}{'95%区间(pp)':>18}{'判定':>10}")
    for c in sorted(r["per_cell"], key=lambda c: c["delta"]):
        k = c["key"]
        ci = (f"{c['ci_lo']*100:+.1f}~{c['ci_hi']*100:+.1f}" if c.get("ci_lo") is not None else "—")
        print(f"{k[0]}|{k[1]}/P{k[2]+1:<6}{c['base']*100:>6.1f}{c['wr']*100:>7.1f}"
              f"{c['delta']*100:>+9.1f}{ci:>18}{c['verdict']:>12}")
    med = r["median_delta"]
    print("-" * 70)
    print(f"覆盖 {r['covered']}/{r['expected']} 格；17 常规格中位数 Δ = "
          f"{med*100:+.1f}pp（达标线 ≥ +15pp）")
    if r["needs_more"]:
        print(f"需加测（95% 区间跨 0，先 +200 局/格再判）：{len(r['needs_more'])} 格——")
        for k in r["needs_more"]:
            print(f"  {k[0]}|{k[1]}/P{k[2]+1}")
    print(f"结论：{'**达标**' if r['pass'] else '未达标'}"
          + ("" if r["pass"] else f"（needs_more 待加测 {len(r['needs_more'])} 格——加测后重跑本工具）"))
    return r


def main():
    ap = argparse.ArgumentParser(description="一期认证（judge 出证 + A1 硬闸）")
    ap.add_argument("--run", default="", help="run 名（相对 logs/）或 run 目录（查硬闸/找矩阵）")
    ap.add_argument("--matrix-best", default="", help="best 检查点的 matrix_eval json（达标依据）")
    ap.add_argument("--matrix-last", default="", help="last 检查点的 matrix_eval json（只报告）")
    ap.add_argument("--gate-only", action="store_true", help="只查硬闸不判矩阵")
    ap.add_argument("--no-gate", action="store_true", help="跳过硬闸（诊断用——出证无效）")
    cli = ap.parse_args()

    if not cli.run and not (cli.matrix_best or cli.matrix_last):
        print("[!] 需要 --run 和/或 --matrix-best/--matrix-last", file=sys.stderr)
        return 2

    gates_ok, gate_fails = True, []
    run_dir = None
    if cli.run and not cli.no_gate:
        run_dir = Path(cli.run)
        if not run_dir.is_absolute():
            cand = Path("logs") / cli.run
            run_dir = cand if cand.exists() else run_dir
        if not run_dir.exists():
            print(f"[!] run 目录不存在：{run_dir}", file=sys.stderr)
            return 2
        gates_ok, gate_fails = check_gates(run_dir)
        print(f"===== A1 硬闸（run={run_dir.name}）=====")
        if gates_ok:
            print("  ✓ opponent=simpleai / criterion_era / layout_version / run 名 四项全过")
        else:
            for f in gate_fails:
                print(f"  ✗ {f}")
            print("  ——拒绝出证（一期只认 simpleai 口径 + 当前基线纪元 + 当前布局）")
            if cli.gate_only or cli.matrix_best:
                return 1
        if cli.gate_only:
            return 0 if gates_ok else 1

    if not cli.no_gate and not gates_ok:
        return 1  # 无矩阵也要拦

    verdict = None
    if cli.matrix_best:
        cells, data = cells_from_matrix_json(Path(cli.matrix_best))
        verdict = report(f"best · {cli.matrix_best}", cells, as_verdict=True)
    if cli.matrix_last:
        cells, data = cells_from_matrix_json(Path(cli.matrix_last))
        report(f"last · {cli.matrix_last}", cells, as_verdict=False)

    if cli.matrix_best:
        return 0 if (verdict and verdict["pass"]) else 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
