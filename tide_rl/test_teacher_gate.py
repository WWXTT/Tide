"""teacher_gate 单测（第一批工具，2026-11-26 三专精→教师→蒸馏框架 §8）：合成专精 run
（config/matrix/behavior/params 全套临时目录）走 CLI 全链——T2/T1/win-only/fail/needs_more、
Δ_agg 与单格 +5pp 边界（含等号）、饱和格 pass/fail、覆盖不齐、绿行为门阈值覆盖与缺指标、
非绿套忽略行为门、硬闸拒绝（era / run 名）不落任何产物、manifest §8.4 字段齐全、
退出码与 tier 一致（0=T2、1=T1、2=其余）。judge 边界另走纯函数直测。
纯标准库（经 certify_phase1 间接 import tide_features→numpy，主仓/副本环境均可用）；
直接 `python test_teacher_gate.py`，退出码 0=全过。风格对齐 test_phase1_criterion.py。
"""
import contextlib
import hashlib
import io
import json
import sys
import tempfile
from pathlib import Path

from phase1_criterion import SCRIPT_MATRIX_BASELINES, BASELINE_ERA
from tide_features import LAYOUT_VERSION

import teacher_gate
from teacher_gate import judge_teacher, main, EXIT_CODES

FAIL = 0
LAST_OUT = ""   # run_cli 最近一次捕获输出（失败排查用）


def check(ok, what):
    global FAIL
    print(f"  {'✓' if ok else '✗'} {what}")
    if not ok:
        FAIL += 1


# ==================== 合成数据 ====================

def deck_cells(deck, off, n=200):
    """本套 6 格合成 cells：off={(opp,seat): 整数胜数偏移}——六格基线×200 全整数
    （green: 120/50/193/5/125/115；red: 68/133/151/77/142/126），偏移后 wr=base+Δ 无舍入。"""
    cells = {}
    for key, base in SCRIPT_MATRIX_BASELINES.items():
        if key[0] != deck:
            continue
        w = round(base * n) + off.get((key[1], key[2]), 0)
        cells[key] = (max(0, min(n, w)), n)
    return cells


def matrix_json(cells):
    """cells → eval_matrix 产出同构 json（matrix["mt|opp"] 带 P1/P2 [胜,局]）。"""
    m = {}
    for (d, opp, seat), (w, n) in cells.items():
        m.setdefault(f"{d}|{opp}", {})[f"P{seat + 1}"] = [w, n]
    return {"ckpt": "last/params.msgpack", "step": 600000, "matrix": m}


def make_run(root, name, deck, cells=None, behavior=None, era=BASELINE_ERA,
             layout=LAYOUT_VERSION, opponent="simpleai"):
    """合成专精 run 目录：config.json（过 A1 硬闸的四键）+ 矩阵 + 行为 + last/params.msgpack。"""
    run = root / name
    run.mkdir(parents=True)
    cfg = {"opponent": opponent, "criterion_era": era, "layout_version": layout,
           "model_theme": deck}
    (run / "config.json").write_text(json.dumps(cfg), encoding="utf-8")
    if cells is not None:
        (run / "matrix_eval_teacher.json").write_text(
            json.dumps(matrix_json(cells)), encoding="utf-8")
    if behavior is not None:
        (run / "teacher_behavior.json").write_text(
            json.dumps(behavior), encoding="utf-8")
    (run / "last").mkdir()
    (run / "last" / "params.msgpack").write_bytes(b"dummy")
    return run


def run_cli(args):
    """进程内调 teacher_gate.main（捕获 stdout 保持单测输出干净），返回退出码。"""
    global LAST_OUT
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        rc = main(args)
    LAST_OUT = buf.getvalue()
    return rc


def load(p):
    return json.loads(Path(p).read_text(encoding="utf-8"))


# 常用构型
OFF_T2 = {("red", 0): 24, ("red", 1): 24, ("blue", 0): 24, ("blue", 1): 24,
          ("green", 1): 24}                          # 5 常规格各 Δ=+12pp、饱和格 0 → Δ_agg=+10pp
BEH_PASS = {"t5_agros_rate": 0.25, "chain_rate": 0.60, "full_sac_rate": 0.35,
            "wotu_per_game": 2.0}
BEH_BELOW = {"t5_agros_rate": 0.10, "chain_rate": 0.40, "full_sac_rate": 0.20,
             "wotu_per_game": 1.0}
GATE_FIELDS = {"deck", "opp", "seat", "wins", "games", "wr", "baseline", "delta",
               "ci95", "verdict"}
MANIFEST_FIELDS = {"deck", "run", "checkpoint", "step", "layout_version",
                   "criterion_era", "opponent", "matrix_eval", "cells", "delta_agg",
                   "tier", "behavior", "behavior_gate", "sha256", "date"}


def run():
    print("【teacher_gate 单测】")
    tmp = tempfile.TemporaryDirectory()
    root = Path(tmp.name)

    # ---- ① T2 全链：6 格齐 + 行为门过 → tier=T2、退出码 0、manifest 字段齐全 ----
    r1 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999001", "green",
                  cells=deck_cells("green", OFF_T2), behavior=BEH_PASS)
    rc = run_cli(["--run", str(r1)])                       # 不传 --deck：走 config model_theme
    g, mf = load(r1 / "teacher_gate.json"), load(r1 / "teacher_manifest.json")
    check(rc == 0 and g["tier"] == "T2", f"T2：rc={rc} tier={g['tier']}（退出码 0=T2）")
    check(g["deck"] == "green" and len(g["per_cell"]) == 6 and not g["needs_more"],
          "T2：deck 取自 config model_theme、6 格齐、无加测")
    check(all(GATE_FIELDS <= set(c) for c in g["per_cell"]),
          "T2：逐格字段齐全（deck/opp/seat/wins/games/wr/baseline/delta/ci95/verdict）")
    check(abs(g["delta_agg"] - 0.10) < 1e-9 and g["criterion_era"] == BASELINE_ERA
          and "generated_at" in g and "thresholds_used" in g and g["behavior"]["pass"] is True,
          "T2：Δ_agg=+10pp、criterion_era/generated_at/thresholds_used/behavior 齐")
    check(MANIFEST_FIELDS <= set(mf) and mf["deck"] == "green"
          and mf["layout_version"] == LAYOUT_VERSION and mf["opponent"] == "simpleai"
          and mf["step"] == 600000 and len(mf["cells"]) == 6
          and "green|red|P1" in mf["cells"],
          "manifest：§8.4 字段齐全（deck/run/checkpoint/step/layout_version/criterion_era/"
          "opponent/cells/delta_agg/tier/behavior/sha256/date）")
    check(mf["sha256"] == hashlib.sha256(b"dummy").hexdigest()
          and mf["behavior"] == BEH_PASS,
          "manifest：sha256=params.msgpack 实测、behavior=扁平 rates（§8.4 形态）")

    # ---- ② T1：常规格全 pass（A1 后按 verdict）、Δ_agg=+7.4pp ∈ [+5,+10)、单格 +4.5pp 阻断 T2 ----
    # （旧构型 Δ=+2pp/+4pp 的格在 A1「区间跨 0 优先」下变 needs_more → 已按新口径换夹具：
    #   低基线格 green|green/P2（base 2.5%）+4.5pp 的 CI 下界仍 >0 → pass 且 <T2 线）
    off = {("green", 1): 9, ("red", 0): 20, ("red", 1): 20, ("blue", 0): 20,
           ("blue", 1): 20}   # Δ = 0.045/0.10×4/0（饱和）→ agg = 0.445/6 ≈ +7.4pp
    r2 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999002", "green",
                  cells=deck_cells("green", off), behavior=BEH_PASS)
    rc = run_cli(["--run", str(r2)])
    g = load(r2 / "teacher_gate.json")
    check(rc == 1 and g["tier"] == "T1" and g["win_tier"] == "T1",
          f"T1：rc={rc} tier={g['tier']}（全格 pass、Δ_agg=+7.4pp∈[+5,+10)、+4.5pp 格阻 T2）")
    check(abs(g["delta_agg"] - round(0.445 / 6, 4)) < 1e-9,
          f"T1：Δ_agg={g['delta_agg']}（+7.4pp）")

    # ---- ③ win-only：胜率 T2 而绿行为门未过 → tier=win-only、退出码 2 ----
    r3 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999003", "green",
                  cells=deck_cells("green", OFF_T2), behavior=BEH_BELOW)
    rc = run_cli(["--run", str(r3)])
    g = load(r3 / "teacher_gate.json")
    check(rc == 2 and g["tier"] == "win-only" and g["win_tier"] == "T2",
          f"win-only：rc={rc} tier={g['tier']}（胜率线 {g['win_tier']}、行为四项全低于阈值）")
    check(g["behavior"]["metrics"]["wotu_per_game"]["verdict"] == "below",
          "win-only：行为指标明细落盘（below/pass 逐项可查）")

    # ---- ④ fail：单常规格 Δ=−15pp（95% 区间离 0）→ tier=fail、退出码 2（manifest 照写）----
    r4 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999004", "green",
                  cells=deck_cells("green", {**OFF_T2, ("red", 1): -30}),
                  behavior=BEH_PASS)
    rc = run_cli(["--run", str(r4)])
    g, mf = load(r4 / "teacher_gate.json"), load(r4 / "teacher_manifest.json")
    cell = next(c for c in g["per_cell"] if c["opp"] == "red" and c["seat"] == "P2")
    check(rc == 2 and g["tier"] == "fail" and cell["verdict"] == "fail",
          f"fail：rc={rc} tier={g['tier']}（green|red/P2 Δ=−15pp 区间不跨 0）")
    check(mf["tier"] == "fail" and len(mf["cells"]) == 6,
          "fail：硬闸过则 manifest 照写（tier=fail 供蒸馏侧拒用）")

    # ---- ⑤ needs_more：单格 Δ=−3pp、95% 区间跨 0 → tier=needs_more、不当 pass ----
    r5 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999005", "green",
                  cells=deck_cells("green", {**OFF_T2, ("red", 1): -6}),
                  behavior=BEH_PASS)
    rc = run_cli(["--run", str(r5)])
    g = load(r5 / "teacher_gate.json")
    check(rc == 2 and g["tier"] == "needs_more" and g["needs_more"] == ["green|red/P2"],
          f"needs_more：rc={rc} tier={g['tier']} 加测格={g['needs_more']}（不当 pass）")

    # ---- ⑥ 饱和格（绿镜 P1 基线 0.965）：不退步界=基线−5pp ----
    j = judge_teacher(deck_cells("green", {**OFF_T2, ("green", 0): -16}), "green", BEH_PASS)
    sat = next(c for c in j["per_cell"] if c["saturated"])
    check(sat["verdict"] == "saturated_fail" and j["tier"] == "fail",
          f"饱和格：−8pp={sat['verdict']}（<基线−5pp → 整门 fail，其余格 T2 级也救不回）")
    j = judge_teacher(deck_cells("green", {("red", 0): 26, ("red", 1): 26,
                                           ("blue", 0): 26, ("blue", 1): 26,
                                           ("green", 1): 26, ("green", 0): -4}),
                      "green", BEH_PASS)
    sat = next(c for c in j["per_cell"] if c["saturated"])
    check(sat["verdict"] == "saturated_pass" and j["tier"] == "T2",
          f"饱和格：−2pp={sat['verdict']}（≥基线−5pp 不退步 → 不阻断 T2；"
          f"常规格 +13pp×5 → Δ_agg={j['delta_agg']} ≥ +10pp）")

    # ---- ⑦ 硬闸拒绝：criterion_era 失配 → 退出码 2、不写任何产物 ----
    r7 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999007", "green",
                  cells=deck_cells("green", OFF_T2), behavior=BEH_PASS,
                  era="v15frozen-20261110-script-200")
    rc = run_cli(["--run", str(r7)])
    check(rc == 2 and not (r7 / "teacher_gate.json").exists()
          and not (r7 / "teacher_manifest.json").exists(),
          f"硬闸：era 失配 rc={rc} 拒绝出证、零产物（复用 certify check_gates）")

    # ---- ⑧ 硬闸拒绝：run 名不含 simpleai（自对弈旧名一眼可辨）----
    r8 = make_run(root, "tide_ppo_selfplay_x__42__1799999008", "green",
                  cells=deck_cells("green", OFF_T2), behavior=BEH_PASS)
    rc = run_cli(["--run", str(r8)])
    check(rc == 2 and not (r8 / "teacher_manifest.json").exists(),
          f"硬闸：run 名无 simpleai rc={rc} 拒绝出证")

    # ---- ⑨ 非绿套：红专精 T2 + 行为文件存在但被忽略（§8.2 仅绿教师设行为门）----
    r9 = make_run(root, "tide_ppo_simpleai_red_teacher__42__1799999009", "red",
                  cells=deck_cells("red", {k: 24 for k in
                                           [("red", 0), ("red", 1), ("green", 0),
                                            ("green", 1), ("blue", 0), ("blue", 1)]}),
                  behavior=BEH_BELOW)
    rc = run_cli(["--run", str(r9)])
    g, mf = load(r9 / "teacher_gate.json"), load(r9 / "teacher_manifest.json")
    check(rc == 0 and g["tier"] == "T2" and g["behavior"]["gate"] == "skipped-not-green",
          f"红套：rc={rc} tier={g['tier']} 行为门 gate={g['behavior']['gate']}（不并入 tier）")
    check(mf["behavior"] is None and abs(g["delta_agg"] - 0.12) < 1e-9,
          "红套：manifest behavior=None（无饱和格，6 常规格各 +12pp → Δ_agg=+12pp）")

    # ---- ⑩ 覆盖不齐：矩阵缺 green|blue 一对 → tier=fail、退出码 2 ----
    partial = {k: v for k, v in deck_cells("green", OFF_T2).items() if k[1] != "blue"}
    r10 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999010", "green",
                   cells=partial, behavior=BEH_PASS)
    rc = run_cli(["--run", str(r10)])
    g = load(r10 / "teacher_gate.json")
    check(rc == 2 and g["tier"] == "fail" and g["covered"] == 4
          and any("覆盖不齐" in x for x in g["reasons"]),
          f"覆盖不齐：rc={rc} tier={g['tier']} covered={g['covered']}/6（缺 blue 两格）")

    # ---- ⑪ 行为门阈值覆盖：BEH_BELOW 在放宽阈值下全过 → tier 翻回 T2 ----
    r11 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999011", "green",
                   cells=deck_cells("green", OFF_T2), behavior=BEH_BELOW)
    rc = run_cli(["--run", str(r11), "--behavior-thresholds",
                  json.dumps({"t5_agros_rate": 0.10, "chain_rate": 0.40,
                              "full_sac_rate": 0.20, "wotu_per_game": 1.0})])
    g = load(r11 / "teacher_gate.json")
    check(rc == 0 and g["tier"] == "T2"
          and g["thresholds_used"]["behavior"]["wotu_per_game"] == 1.0,
          f"阈值覆盖：rc={rc} tier={g['tier']}（thresholds_used 记生效值）")

    # ---- ⑫ Δ_agg 下侧构型：小 Δ 格（+2/+4pp）先被 A1 区间拦下 → tier=needs_more ----
    #（A1 后「全格 pass 且 Δ_agg<+5pp」在 base_n=200 噪声下数学不可达：每格 pass 要求
    #  Δ>CI 半宽≈6–9pp，5 格下限已把 Δ_agg 顶到 ≈+6.7pp——agg 兜底分支保留为防御代码）
    j = judge_teacher(deck_cells("green", {("green", 1): 4, ("red", 0): 8,
                                           ("red", 1): 8, ("blue", 0): 8,
                                           ("blue", 1): 31}), "green", BEH_PASS)
    check(j["tier"] == "needs_more" and j["win_tier"] is None
          and abs(j["delta_agg"] - round(0.295 / 6, 4)) < 1e-12,
          f"聚合下侧：Δ_agg={j['delta_agg']:.4f}（<+5pp），但小 Δ 格先判 needs_more"
          f"（区间跨 0 优先）→ tier={j['tier']}——不因聚合差而放过证据不足")

    # ---- ⑬ T2 单格门槛：最低格 +5.5pp（CI 下界>0 的 pass）+ 其余高 Δ、agg=+10.5pp → T2 ----
    #（恰 +5pp 的等号含在 A1 下不可达：中基线格 CI 必跨 0 → needs_more；低基线格
    #  15/200−0.025=0.0499… 浮点尘埃。代码比较仍是 >=，此处用 +5.5pp 验证门槛语义。）
    j = judge_teacher(deck_cells("green", {("green", 1): 11, ("red", 0): 43,
                                           ("red", 1): 24, ("blue", 0): 24,
                                           ("blue", 1): 24}), "green", BEH_PASS)
    check(j["tier"] == "T2" and abs(j["delta_agg"] - round(0.63 / 6, 4)) < 1e-9
          and all(p["verdict"] in ("pass", "saturated_pass") for p in j["per_cell"]),
          f"T2 单格门槛：最低格 +5.5pp 计入 pass、Δ_agg=+10.5pp → tier={j['tier']}（全格无扣分项）")

    # ---- ⑭ deck 解析：--deck 与 config 冲突拒绝；model_theme=all 无 --deck 报错 ----
    rc = run_cli(["--run", str(r1), "--deck", "red"])
    check(rc == 2, f"deck 冲突：config=green 而 --deck=red → rc={rc}（别套格 off-distribution）")
    r14 = make_run(root, "tide_ppo_simpleai__42__1799999014", "all",
                   cells=deck_cells("red", {}))
    rc = run_cli(["--run", str(r14)])
    check(rc == 2, f"deck 缺省：config model_theme='all'（共享 run）无 --deck → rc={rc} 报错")

    # ---- ⑮ 绿套行为文件缺指标：该指标记 missing、行为门不过 → win-only ----
    r15 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999015", "green",
                   cells=deck_cells("green", OFF_T2),
                   behavior={"t5_agros_rate": 0.25, "chain_rate": 0.60,
                             "full_sac_rate": 0.35})     # 缺 wotu_per_game
    rc = run_cli(["--run", str(r15)])
    g = load(r15 / "teacher_gate.json")
    check(rc == 2 and g["tier"] == "win-only"
          and g["behavior"]["metrics"]["wotu_per_game"]["verdict"] == "missing",
          f"行为缺指标：rc={rc} tier={g['tier']}（未展示的行为=未证明，不当 pass）")

    # ---- ⑯ A3-①：Δ=+3pp、n=100（95% 区间跨 0）→ needs_more 优先于 Δ≥0 ----
    off100 = {("red", 0): 3, ("red", 1): 12, ("green", 1): 12, ("blue", 0): 13,
              ("blue", 1): 13}   # 目标格 +3pp；其余格 +12~13.5pp（CI 下界 >0，n=100 下仍 pass）
    r16 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999016", "green",
                   cells=deck_cells("green", off100, n=100), behavior=BEH_PASS)
    rc = run_cli(["--run", str(r16)])
    g = load(r16 / "teacher_gate.json")
    cell = next(c for c in g["per_cell"] if c["opp"] == "red" and c["seat"] == "P1")
    check(rc == 2 and g["tier"] == "needs_more" and cell["verdict"] == "needs_more"
          and g["needs_more"] == ["green|red/P1"],
          f"A1 区间跨0优先：Δ=+3pp/n=100 → verdict={cell['verdict']} tier={g['tier']} rc={rc}"
          f"（Δ≥0 但证据不足也先加测，不当 pass）")

    # ---- ⑰ A3-②：green 无 teacher_behavior.json 且胜率线 T2 → 强制 win-only ----
    r17 = make_run(root, "tide_ppo_simpleai_green_teacher__42__1799999017", "green",
                   cells=deck_cells("green", OFF_T2))
    rc = run_cli(["--run", str(r17)])
    g, mf = load(r17 / "teacher_gate.json"), load(r17 / "teacher_manifest.json")
    check(rc == 2 and g["tier"] == "win-only" and g["win_tier"] == "T2"
          and g["behavior"]["gate"] == "missing"
          and any("行为证据缺失" in x for x in g["reasons"]),
          f"A2 缺行为证据：rc={rc} tier={g['tier']}（胜率线 {g['win_tier']} 也不得当教师，§8.2）")
    check(mf["behavior_gate"] == "missing" and mf["behavior"] is None,
          "A2 缺行为证据：manifest.behavior_gate=missing、behavior=null")

    # ---- ⑱ 退出码映射与 tier 一致 ----
    check(EXIT_CODES == {"T2": 0, "T1": 1},
          "退出码映射：0=T2、1=T1、2=其余（win-only/fail/needs_more/硬闸/用法）"
          "——①②③④⑤⑦⑧⑩⑮⑯⑰ 已逐项实证")

    tmp.cleanup()
    print("【teacher_gate 单测】" + ("全部通过" if FAIL == 0 else f"{FAIL} 项失败"))
    return 0 if FAIL == 0 else 1


if __name__ == "__main__":
    sys.exit(run())
