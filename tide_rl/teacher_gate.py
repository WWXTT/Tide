"""教师门判定（Stage 3·三专精→教师→蒸馏框架 §8，第一批工具 2026-11-26）。

对专精 run 的 matrix_eval 结果做**本套 6 格**（deck × 3 对手 × 2 座位）教师门判定：
  - T1（教师最低线）：常规格全 pass 且 Δ_agg ≥ +5pp；饱和格不退步
    （wr ≥ 基线 − SATURATED_TOL）；
  - 常规格判定顺序（2026-11-26 第二批修补）：饱和格 → saturated_pass/fail；
    否则 95% 区间跨 0（lo≤0≤hi）→ needs_more；否则 Δ≥0 → pass；否则 fail——
    **「区间跨 0」优先于「Δ≥0」**：Δ≥0 但证据不足的格同样先加测，不当 pass
    （副作用：Δ=+5pp 量级的格在基线侧 n=200 的合成区间半宽 ≈6–9pp 内几乎必然
    needs_more——这正是 §8.1「先加测再判」的本意，不是 bug）；
  - 绿套行为证据缺失（deck=green 且 teacher_behavior.json 未提供）→ 胜率线达标也
    强制 tier="win-only"（2026-11-26 第二批修补——§8.2 绿教师必须展示目标线，无证据
    即未证明）；manifest 增 behavior_gate 字段（applied | missing | skipped-not-green）；
  - T2（推荐教师线）：常规格每格 Δ ≥ +5pp 且 Δ_agg ≥ +10pp；饱和格不退步；
  - deck=green 且 teacher_behavior.json 存在时合并绿行为门（§8.2，阈值 2026-11-25
    用户定「不变」；--behavior-thresholds 可覆盖）：T5 阿格洛斯 ≥20%、牺牲→回收链 ≥50%、
    满献 ≥30%、沃土 ≥1.5 次/局；胜率过而行为不过 → tier="win-only"。
判据常量全部 import 自 phase1_criterion（不改值）；T1/T2 线是教师门自身口径（§8.1 定义）。

硬闸复用 certify_phase1.check_gates（opponent=simpleai / criterion_era=BASELINE_ERA /
layout_version=LAYOUT_VERSION）：不符**拒绝出证**（退出码 2，不写任何产物）。
**run 目录名不再参与教师门硬闸（2026-11-27 修）**：口径以 config.opponent 为权威——
本批教师是 `*_teacher_600k__42__<ts>` 续训名（不含 simpleai），旧名字闸会把合法教师
全部误拒；学生认证（certify_phase1）仍默认要求目录名带 `simpleai`。名字不含 simpleai
的教师会被记 `run_name_waived=true`（gate json + manifest），只作口径提示、不阻断。
矩阵 json 格式 = eval_matrix.py 产出（matrix["mt|opp"] 带 P1/P2 [胜,局]）。

用法（tide_rl 目录，--run 同 certify：run 名（相对 logs/）或 run 目录）：
  python teacher_gate.py --run logs/tide_ppo_simpleai_green_teacher__42__<ts>
      [--deck green] [--matrix <run>/matrix_eval_teacher.json]
      [--behavior <run>/teacher_behavior.json] [--behavior-thresholds '{"wotu_per_game":2.0}']
      [--out <run>/teacher_gate.json] [--manifest <run>/teacher_manifest.json]

退出码：0=T2；1=T1；2=其余（win-only / fail / needs_more / 覆盖不齐 / 硬闸拒绝 / 用法错误）。
产物：teacher_gate.json（逐格判定明细）+ teacher_manifest.json（§8.4 schema，硬闸通过即写，
tier 字段供蒸馏侧消费）。
"""
import argparse
import hashlib
import json
import statistics
import sys
from datetime import date, datetime
from pathlib import Path

from phase1_criterion import (
    SCRIPT_MATRIX_BASELINES, BASELINE_ERA, SATURATED_TOL,
    is_saturated, delta_ci,
)
from certify_phase1 import check_gates, cells_from_matrix_json
from tide_features import LAYOUT_VERSION

DECKS = ("red", "green", "blue")
OPP_ORDER = ("red", "green", "blue")
SEAT_LABEL = {0: "P1", 1: "P2"}

# §8.1 教师门自身口径（phase1_criterion 判据常量不动——T1/T2 线是教师门新增定义）
T1_AGG = 0.05    # T1：常规格全 Δ≥0 且 Δ_agg ≥ +5pp
T2_CELL = 0.05   # T2：常规格每格 Δ ≥ +5pp（点估计，边界含等号）
T2_AGG = 0.10    # T2：Δ_agg ≥ +10pp（边界含等号）

# §8.2 绿行为门阈值（2026-11-25 用户定「行为门阈不变」；--behavior-thresholds 可部分覆盖）
DEFAULT_BEHAVIOR_THRESHOLDS = {
    "t5_agros_rate": 0.20,   # T5 阿格洛斯（作为卡打出·全局 ≤10 回合）≥ 20% 绿局
    "chain_rate": 0.50,      # 牺牲→回收链（mChain）≥ 50% 绿局
    "full_sac_rate": 0.30,   # 满献（单次牺牲目标数 ≥11）≥ 30% 绿局
    "wotu_per_game": 1.5,    # 沃土打出 ≥ 1.5 次/绿局
}

EXIT_CODES = {"T2": 0, "T1": 1}   # 其余 tier（win-only/fail/needs_more）与硬闸/用法错误 = 2


def cell_name(key):
    return f"{key[0]}|{key[1]}/{SEAT_LABEL[key[2]]}"


def judge_teacher(cells, deck, behavior=None, behavior_thresholds=None):
    """本套 6 格教师门判定（纯函数；单测直接消费，CLI 只做 IO 包装）。

    cells: {(model_preset, opp_theme, seat): (wins, n)}——18 格全量矩阵也直接给，
    只取本套 6 格（deck × 3 对手 × 2 座位），别套 12 格不参与（§8.1）。
    behavior: teacher_behavior.json 的 dict（None=未提供→绿套也不合并行为门）；
    behavior_thresholds: 覆盖 DEFAULT_BEHAVIOR_THRESHOLDS 的 dict（None=默认）。
    返回判定 dict（teacher_gate.json 主体 schema）。
    """
    per, regular, needs_more, missing, fails = [], [], [], [], []
    wrs, bases = [], []
    for opp in OPP_ORDER:
        for seat in (0, 1):
            key = (deck, opp, seat)
            base = SCRIPT_MATRIX_BASELINES.get(key)
            got = (cells or {}).get(key)
            if base is None or not got or not got[1]:
                missing.append(cell_name(key))
                continue
            w, n = got
            wr = w / n
            delta = wr - base
            _, lo, hi = delta_ci(w, n, base)
            sat = is_saturated(base)
            if sat:
                verdict = "saturated_pass" if wr >= base - SATURATED_TOL else "saturated_fail"
            elif lo <= 0 <= hi:
                verdict = "needs_more"   # 区间跨 0 优先于 Δ≥0（A1：证据不足先加测，不当 pass）
            elif delta >= 0:
                verdict = "pass"
            else:
                verdict = "fail"
            if verdict == "needs_more":
                needs_more.append(cell_name(key))
            elif verdict in ("fail", "saturated_fail"):
                fails.append(f"{cell_name(key)}={verdict}")
            if not sat:
                regular.append(delta)
            wrs.append(wr)
            bases.append(base)
            per.append({"deck": deck, "opp": opp, "seat": SEAT_LABEL[seat],
                        "wins": w, "games": n, "wr": round(wr, 4), "baseline": base,
                        "delta": round(delta, 4), "ci95": [round(lo, 4), round(hi, 4)],
                        "verdict": verdict, "saturated": sat})
    complete = not missing
    delta_agg = (statistics.mean(wrs) - statistics.mean(bases)) if len(wrs) == 6 else None
    sat_ok = all(p["verdict"] != "saturated_fail" for p in per)
    # A1 后 needs_more 可与 Δ≥0 共存 → T1 改按 verdict 判（全 pass），不再用 d>=0 点估计
    all_regular_pass = all(p["verdict"] == "pass" for p in per if not p["saturated"])
    t1 = (complete and sat_ok and all_regular_pass
          and delta_agg is not None and delta_agg >= T1_AGG)
    t2 = (complete and sat_ok and all(d >= T2_CELL for d in regular)
          and delta_agg is not None and delta_agg >= T2_AGG)
    win_tier = "T2" if t2 else ("T1" if t1 else None)

    thresholds_eff = dict(DEFAULT_BEHAVIOR_THRESHOLDS)
    if behavior_thresholds:
        thresholds_eff.update(behavior_thresholds)
    behavior_detail = None
    if deck == "green":
        if behavior is not None:
            metrics = {}
            for m, th in thresholds_eff.items():
                v = behavior.get(m)
                numeric = isinstance(v, (int, float)) and not isinstance(v, bool)
                ok = numeric and v >= th
                verdict_m = "pass" if ok else ("missing" if v is None
                                               else ("invalid" if not numeric else "below"))
                metrics[m] = {"value": v, "threshold": th, "verdict": verdict_m, "passed": ok}
            behavior_detail = {"gate": "applied", "thresholds": thresholds_eff,
                               "metrics": metrics,
                               "pass": all(x["passed"] for x in metrics.values())}
        else:
            # A2（2026-11-26）：绿套无行为证据 = 未证明目标线——胜率线达标也强制 win-only
            behavior_detail = {"gate": "missing", "thresholds": thresholds_eff,
                               "metrics": None, "pass": False}
    elif behavior is not None:
        behavior_detail = {"gate": "skipped-not-green", "metrics": None, "pass": None}

    reasons = []
    if missing:
        tier = "fail"
        reasons.append(f"覆盖不齐：缺 {len(missing)} 格（{'、'.join(missing)}）——本套 6 格必须齐全")
    elif fails:
        tier = "fail"
        reasons.append("存在明确未达标格：" + "、".join(fails))
    elif needs_more:
        tier = "needs_more"
        reasons.append("需加测（95% 区间跨 0，不当 pass）：" + "、".join(needs_more))
    elif win_tier is None:
        tier = "fail"
        reasons.append(f"Δ_agg={delta_agg:+.4f} 低于 T1 线 +{T1_AGG:.2f}"
                       "（常规格点估计已全 Δ≥0——聚合未达标）")
    elif behavior_detail and behavior_detail.get("gate") == "missing":
        tier = "win-only"
        reasons.append("行为证据缺失（teacher_behavior.json 未提供），按 §8.2 不得当教师"
                       f"（胜率线 {win_tier}）")
    elif behavior_detail and behavior_detail.get("gate") == "applied" and behavior_detail["pass"] is False:
        tier = "win-only"
        reasons.append(f"绿行为门未过（胜率线 {win_tier}）：T5/链/满献/沃土 至少一项低于阈值")
    else:
        tier = win_tier

    thresholds_used = {
        "t1_regular_delta_min": 0.0,
        "t1_delta_agg_min": T1_AGG,
        "t2_regular_delta_min": T2_CELL,
        "t2_delta_agg_min": T2_AGG,
        "saturated_tol": SATURATED_TOL,
        "behavior": dict(thresholds_eff) if deck == "green" else None,
    }
    return {"deck": deck, "per_cell": per,
            "delta_agg": None if delta_agg is None else round(delta_agg, 4),
            "tier": tier, "win_tier": win_tier, "needs_more": needs_more,
            "reasons": reasons, "behavior": behavior_detail,
            "thresholds_used": thresholds_used, "covered": len(per), "expected": 6}


def sha256_ckpt(ckpt, run_dir):
    """矩阵 json 的 ckpt 路径 → params.msgpack 的 sha256；找不到返回 None。
    相对路径按「相对 run 目录」与「相对 tide_rl 根（logs/<run>/... 形态）」两处找。"""
    if not ckpt:
        return None
    p = Path(ckpt)
    cands = [p] if p.is_absolute() else [run_dir / p, run_dir.parent / p, p]
    for c in cands:
        if c.is_file():
            h = hashlib.sha256()
            with open(c, "rb") as f:
                for chunk in iter(lambda: f.read(65536), b""):
                    h.update(chunk)
            return h.hexdigest()
    return None


def build_manifest(run_dir, deck, mdata, matrix_p, result, opponent, run_name_waived=False):
    """teacher_manifest.json（框架 §8.4 schema 全字段）。behavior=绿套且行为门已合并时
    给扁平 rates dict（§8.4 示例形态），否则 None；behavior_gate 记行为门状态
    （applied / missing / skipped-not-green，A2 2026-11-26）。
    run_name_waived（2026-11-27）：run 目录名不含 simpleai 但 config.opponent=simpleai，
    教师门按 config 放行——记 true 留痕，蒸馏侧可见「名字未带口径」。"""
    bhd = result["behavior"]
    behavior_out = None
    if bhd and bhd.get("gate") == "applied":
        behavior_out = {m: v["value"] for m, v in bhd["metrics"].items()}
    behavior_gate = bhd["gate"] if bhd else "skipped-not-green"
    return {
        "deck": deck,
        "run": run_dir.name,
        "checkpoint": mdata.get("ckpt"),
        "step": mdata.get("step"),
        "layout_version": LAYOUT_VERSION,
        "criterion_era": BASELINE_ERA,
        "opponent": opponent,
        "run_name_waived": bool(run_name_waived),
        "matrix_eval": matrix_p.name,
        "cells": {f"{deck}|{c['opp']}|{c['seat']}": [c["wins"], c["games"]]
                  for c in result["per_cell"]},
        "delta_agg": result["delta_agg"],
        "tier": result["tier"],
        "behavior": behavior_out,
        "behavior_gate": behavior_gate,
        "sha256": sha256_ckpt(mdata.get("ckpt"), run_dir),
        "date": date.today().isoformat(),
    }


def print_report(result, matrix_p):
    print(f"\n===== 教师门判定（deck={result['deck']} · 矩阵 {matrix_p.name}）=====")
    print(f"{'格':<18}{'基线':>7}{'模型':>7}{'Δ(pp)':>9}{'95%区间(pp)':>18}{'判定':>16}")
    for c in result["per_cell"]:
        ci = f"{c['ci95'][0] * 100:+.1f}~{c['ci95'][1] * 100:+.1f}"
        print(f"{result['deck']}|{c['opp']}/{c['seat']:<8}{c['baseline'] * 100:>6.1f}"
              f"{c['wr'] * 100:>7.1f}{c['delta'] * 100:>+9.1f}{ci:>18}{c['verdict']:>16}")
    print("-" * 76)
    agg = result["delta_agg"]
    agg_s = f"{agg * 100:+.1f}pp" if agg is not None else "N/A"
    print(f"覆盖 {result['covered']}/{result['expected']} 格；Δ_agg = {agg_s}"
          f"（T1 线 ≥+{T1_AGG * 100:.0f}pp / T2 线 ≥+{T2_AGG * 100:.0f}pp）")
    if result["needs_more"]:
        print(f"需加测（95% 区间跨 0，先加测再判，不当 pass）：{'、'.join(result['needs_more'])}")
    bhd = result["behavior"]
    if bhd and bhd.get("gate") == "applied":
        parts = []
        label = {"t5_agros_rate": "T5阿格洛斯", "chain_rate": "牺牲回收链",
                 "full_sac_rate": "满献", "wotu_per_game": "沃土/局"}
        for m, v in bhd["metrics"].items():
            val = f"{v['value']}" if v["value"] is not None else "缺"
            parts.append(f"{label.get(m, m)} {val}≥{v['threshold']} {'✓' if v['passed'] else '✗'}")
        print(f"绿行为门：{' · '.join(parts)} → {'过' if bhd['pass'] else '未过'}")
    for r in result["reasons"]:
        print(f"  · {r}")
    tail = f"tier = {result['tier']}"
    if result["tier"] == "win-only":
        tail += f"（胜率线 {result['win_tier']}，行为门未过——不推荐直接蒸馏，§8.5）"
    print(f"结论：{tail}")


def main(argv=None):
    ap = argparse.ArgumentParser(
        description="教师门判定（本套 6 格胜率门 + 绿行为门；判据复用 phase1_criterion，硬闸复用 certify_phase1）")
    ap.add_argument("--run", required=True, help="run 名（相对 logs/）或 run 目录")
    ap.add_argument("--deck", default="", help="red/green/blue；缺省读 run config.json 的 model_theme")
    ap.add_argument("--matrix", default="", help="matrix_eval json（默认 <run>/matrix_eval_teacher.json）")
    ap.add_argument("--behavior", default="", help="teacher_behavior.json（默认 <run>/teacher_behavior.json，可缺）")
    ap.add_argument("--behavior-thresholds", default="",
                    help='JSON 覆盖绿行为门阈值，如 \'{"wotu_per_game": 2.0}\'（键须在默认四键内）')
    ap.add_argument("--out", default="", help="判定 json 输出（默认 <run>/teacher_gate.json）")
    ap.add_argument("--manifest", default="", help="教师清单输出（默认 <run>/teacher_manifest.json）")
    cli = ap.parse_args(argv)

    run_dir = Path(cli.run)
    if not run_dir.is_absolute() and not run_dir.exists():
        cand = Path("logs") / cli.run
        run_dir = cand if cand.exists() else run_dir
    if not run_dir.is_dir():
        print(f"[!] run 目录不存在：{cli.run}", file=sys.stderr)
        return 2

    # ===== 硬闸（复用 certify_phase1.check_gates 三条；2026-11-27 起名字不参与）=====
    gates_ok, gate_fails = check_gates(run_dir, require_simpleai_name=False)
    run_name_waived = "simpleai" not in run_dir.name
    print(f"===== 硬闸（run={run_dir.name}）=====")
    if gates_ok:
        print("  ✓ opponent=simpleai / criterion_era / layout_version 三项全过"
              "（教师门不看目录名，口径以 config.opponent 为准）")
        if run_name_waived:
            print(f"  ⚠ run 目录名不含 'simpleai' → run_name_waived=true（可当教师；"
                  f"不能直接作为学生认证对象——certify 仍要求名字带口径）")
    else:
        for f in gate_fails:
            print(f"  ✗ {f}")
        print("  ——拒绝出证（教师门同样只认 simpleai 口径 + 当前基线纪元 + 当前布局）")
        return 2

    cfg_p = run_dir / "config.json"
    cfg = json.loads(cfg_p.read_text(encoding="utf-8")) if cfg_p.exists() else {}
    theme = cfg.get("model_theme")
    if cli.deck:
        deck = cli.deck.strip().lower()
        if deck not in DECKS:
            print(f"[!] --deck 非法：{deck!r}（须 red/green/blue）", file=sys.stderr)
            return 2
        if theme and theme != "all" and theme != deck:
            print(f"[!] --deck={deck} 与 run config model_theme={theme!r} 不符"
                 f"（专精 run 的矩阵行属其训练套——别套格是 off-distribution，不判）", file=sys.stderr)
            return 2
    elif theme in DECKS:
        deck = theme
    else:
        print(f"[!] --deck 缺省且 config model_theme={theme!r} 不可用"
              f"（教师门只对专精 run：model_theme 须 red/green/blue）", file=sys.stderr)
        return 2

    matrix_p = Path(cli.matrix) if cli.matrix else run_dir / "matrix_eval_teacher.json"
    if not matrix_p.exists():
        print(f"[!] 矩阵 json 不存在：{matrix_p}（先跑 eval_matrix --model-themes {deck}）", file=sys.stderr)
        return 2
    cells, mdata = cells_from_matrix_json(matrix_p)

    behavior = None
    behavior_path = Path(cli.behavior) if cli.behavior else run_dir / "teacher_behavior.json"
    if behavior_path.exists():
        try:
            behavior = json.loads(behavior_path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError) as e:
            print(f"[!] teacher_behavior.json 解析失败：{behavior_path}（{e}）", file=sys.stderr)
            return 2
        if not isinstance(behavior, dict):
            print(f"[!] teacher_behavior.json 顶层须为 dict：{behavior_path}", file=sys.stderr)
            return 2
    elif cli.behavior:
        print(f"[!] --behavior 指定的文件不存在：{behavior_path}", file=sys.stderr)
        return 2

    bh_th = None
    if cli.behavior_thresholds:
        try:
            bh_th = json.loads(cli.behavior_thresholds)
        except json.JSONDecodeError as e:
            print(f"[!] --behavior-thresholds 不是合法 JSON：{e}", file=sys.stderr)
            return 2
        if not isinstance(bh_th, dict):
            print("[!] --behavior-thresholds 顶层须为 dict", file=sys.stderr)
            return 2
        bad = [k for k in bh_th if k not in DEFAULT_BEHAVIOR_THRESHOLDS]
        if bad:
            print(f"[!] --behavior-thresholds 含未知指标键：{bad}"
                  f"（合法键：{sorted(DEFAULT_BEHAVIOR_THRESHOLDS)}）", file=sys.stderr)
            return 2
        for k, v in bh_th.items():
            if not isinstance(v, (int, float)) or isinstance(v, bool):
                print(f"[!] --behavior-thresholds[{k}]={v!r} 须为数值", file=sys.stderr)
                return 2

    result = judge_teacher(cells, deck, behavior, bh_th)
    print_report(result, matrix_p)

    out_p = Path(cli.out) if cli.out else run_dir / "teacher_gate.json"
    manifest_p = Path(cli.manifest) if cli.manifest else run_dir / "teacher_manifest.json"
    gate_doc = {
        "deck": deck,
        "run": run_dir.name,
        "matrix": str(matrix_p),
        "criterion_era": BASELINE_ERA,
        "run_name_waived": run_name_waived,
        "generated_at": datetime.now().isoformat(timespec="seconds"),
        "per_cell": result["per_cell"],
        "delta_agg": result["delta_agg"],
        "tier": result["tier"],
        "win_tier": result["win_tier"],
        "needs_more": result["needs_more"],
        "reasons": result["reasons"],
        "behavior": result["behavior"],
        "thresholds_used": result["thresholds_used"],
        "covered": result["covered"],
        "expected": result["expected"],
        "manifest": str(manifest_p),
    }
    out_p.parent.mkdir(parents=True, exist_ok=True)
    out_p.write_text(json.dumps(gate_doc, ensure_ascii=False, indent=1), encoding="utf-8")
    manifest = build_manifest(run_dir, deck, mdata, matrix_p, result,
                              opponent=cfg.get("opponent", "simpleai"),
                              run_name_waived=run_name_waived)
    manifest_p.parent.mkdir(parents=True, exist_ok=True)
    manifest_p.write_text(json.dumps(manifest, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"[out] {out_p}")
    print(f"[out] {manifest_p}")
    return EXIT_CODES.get(result["tier"], 2)


if __name__ == "__main__":
    sys.exit(main())
