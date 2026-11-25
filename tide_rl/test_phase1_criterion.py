"""phase1_criterion 单测（A2，2026-11-24 回到一期训练目标）：合成矩阵覆盖四种判定形态
+ best 选点 Wilson 下界 + 候选门 + 通过率合成验证（真实每格 Δ=+10pp 跑 1000 次，
「17 格全过且中位数 ≥ +15pp」的通过率应落在 60–75%——偏离说明判据实现与口径不一致）。
纯标准库；直接 `python test_phase1_criterion.py`，退出码 0=全过。
"""
import random
import statistics
import sys

from phase1_criterion import (
    SCRIPT_MATRIX_BASELINES, SATURATED_CELLS, judge, select_value, candidate_gate,
    training_gate, cell_threshold, delta_ci, wilson_lower,
)

FAIL = 0


def check(ok, what):
    global FAIL
    print(f"  {'✓' if ok else '✗'} {what}")
    if not ok:
        FAIL += 1


def cells_with(delta_by_key=None, n=200, default_delta=0.0, sat_delta=-0.02):
    """按 SCRIPT_MATRIX_BASELINES 构造合成 cells：常规格胜率=base+delta，饱和格=base+sat_delta。"""
    cells = {}
    for key, base in SCRIPT_MATRIX_BASELINES.items():
        d = sat_delta if key in SATURATED_CELLS else default_delta
        if delta_by_key and key in delta_by_key:
            d = delta_by_key[key]
        wr = min(max(base + d, 0.0), 1.0)
        cells[key] = (round(wr * n), n)
    return cells


def run():
    print("【phase1_criterion 单测】")

    # ---- ① 全过：每格 Δ=+20pp、饱和格 −2pp（不退步） ----
    r = judge(cells_with(default_delta=0.20))
    check(r["pass"] is True, f"全过：pass={r['pass']}（中位数 {r['median_delta']:.3f}）")
    check(r["covered"] == 18 and not r["needs_more"], "全过：18 格覆盖、无加测")

    # ---- ② 覆盖不齐：只给 6 格 ----
    partial = dict(list(cells_with(default_delta=0.20).items())[:6])
    r = judge(partial)
    check(r["pass"] is False and r["median_delta"] is None, "覆盖不齐：不判达标、中位数=None")
    g = training_gate(partial)
    check(g["pass"] is False and g["reason"] == "cover-incomplete", "覆盖不齐：训练门 cover-incomplete")

    # ---- ③ 骑线：单格 Δ=−3pp（n=200、σ_Δ≈5pp → 95% 区间跨 0）→ needs_more 不算输 ----
    key = next(k for k in SCRIPT_MATRIX_BASELINES if k not in SATURATED_CELLS)
    r = judge(cells_with(delta_by_key={key: -0.03}, default_delta=0.20))
    verdict = next(c["verdict"] for c in r["per_cell"] if c["key"] == key)
    check(verdict == "needs_more" and r["pass"] is False and r["needs_more"] == [key],
          f"骑线：verdict={verdict}（区间跨 0 → 先加测，不直接算输）")
    # 同格 Δ=−12pp → 区间离开 0 → fail
    r = judge(cells_with(delta_by_key={key: -0.12}, default_delta=0.20))
    verdict = next(c["verdict"] for c in r["per_cell"] if c["key"] == key)
    check(verdict == "fail", f"明显未达：verdict={verdict}（区间不跨 0 → fail）")

    # ---- ④ 饱和格（绿镜先手 base=0.965，B2 重测表值）：不退步 ≥ 基线−5pp ----
    sat = SATURATED_CELLS[0]
    r = judge(cells_with(sat_delta=-0.02))
    v_ok = next(c["verdict"] for c in r["per_cell"] if c["key"] == sat)
    r = judge(cells_with(sat_delta=-0.08))
    v_bad = next(c["verdict"] for c in r["per_cell"] if c["key"] == sat)
    check(v_ok == "saturated_pass" and v_bad == "saturated_fail",
          f"饱和格：−2pp={v_ok}、−8pp={v_bad}（界=基线−5pp）")

    # ---- ⑤ best 选点（P1 2026-11-25 改版）= 17 常规格 Δ 中位数；结构性零格不拖动 ----
    c = cells_with(default_delta=0.10)
    val, worst = select_value(c)
    ds = sorted(w / n - SCRIPT_MATRIX_BASELINES[k] for k, (w, n) in c.items()
                if k not in SATURATED_CELLS)
    check(val is not None and abs(val - statistics.median(ds)) < 1e-9,
          f"选点：=17 格 Δ 中位数（{val:.4f}）")
    check(worst is not None and worst[0] in SCRIPT_MATRIX_BASELINES,
          "选点：worst_key 随行（诊断口径）")
    # 结构性零格（F1 病根）：单格 0 胜 → 旧 Wilson 下界口径被钉死，中位数口径不动
    key0 = next(k for k in SCRIPT_MATRIX_BASELINES if k not in SATURATED_CELLS)
    n0 = c[key0][1]
    c2 = dict(c)
    c2[key0] = (0, n0)
    val2, worst2 = select_value(c2)
    check(val2 is not None and abs(val2 - val) < 1e-9,
          f"选点：单格 0 胜不拖动中位数（{val2:.4f} vs {val:.4f}）")
    check(worst2 is not None and worst2[0] == key0, "选点：worst=零胜格（诊断口径）")
    # 覆盖不齐 / 空表 → (None, None)（调用方回落旧链）
    partial = dict(list(c.items())[:6])
    check(select_value(partial) == (None, None), "选点：覆盖不齐 → (None, None)")
    check(select_value({}) == (None, None), "选点：空表 → (None, None)")

    # ---- ⑥ 候选门：Δ=−8pp 的格进 below（预警） ----
    cand = candidate_gate(cells_with(delta_by_key={key: -0.08}))
    check(key in cand["below"] and cand["worst"][0] == key,
          f"候选门：Δ=−8pp 计入 below（{len(cand['below'])} 格）")

    # ---- ⑦ 6 格门槛同步：= 基线（Δ≥0），不再是基线+15pp；基线值随 B2 重测更新 ----
    check(abs(cell_threshold("red", 0) - 0.602) < 1e-9,
          f"6 格门槛=基线（实际 {cell_threshold('red', 0)}，B2 前为 0.745=基线+15pp）")

    # ---- ⑧ 统计原语 sanity ----
    d, lo, hi = delta_ci(110, 200, 0.5)
    check(abs(d - 0.05) < 1e-9
          and abs((hi - lo) / 2 - 1.96 * (((0.55 * 0.45) / 200 + (0.5 * 0.5) / 200) ** 0.5)) < 1e-9,
          "delta_ci：点估计=Δ、半宽=1.96·σ_合成")
    check(abs(wilson_lower(0, 200) - 0.0) < 1e-9 and wilson_lower(200, 200) > 0.97,
          "wilson_lower 边界（0/200→0、200/200→≈0.981）")

    # ---- ⑨ 通过率合成验证：真实每格 Δ=+10pp，双侧各 200 局抽样（σ_Δ≈5pp 口径） ----
    # 【2026-11-24 实测发现·待用户拍板】计划原文的「17 格全过且中位数 ≥+15pp → 60–75%」
    # 在联合条件下数学上 ≈0%：中位数 17 格抽样 σ≈1.5pp，真实 +10pp 时 P(中位数≥15pp)≈5e-4。
    # 60–75% 这个带只对应**单项**「17 格全过」（0.977^17≈67%）。故本测试：
    #   ① 断言「17 格全过」落在 60–75%（计划的算式按此项成立）；
    #   ② 联合通过率只打印存证（≈0%）——「中位数 ≥+15pp」作为独立门槛实际要求真实水平
    #     ≈+16pp 才有 60–75% 联合通过率。两项口径如何取舍（中位数线降/联合定义改）= 拍板项。
    rng = random.Random(20261124)
    trials, hit_all, hit_joint = 1000, 0, 0
    regular_keys = [k for k in SCRIPT_MATRIX_BASELINES if k not in SATURATED_CELLS]
    for _ in range(trials):
        deltas = []
        all_ok = True
        for k in regular_keys:
            base = SCRIPT_MATRIX_BASELINES[k]
            w_model = rng.binomialvariate(200, min(base + 0.10, 1.0))
            w_base = rng.binomialvariate(200, base)
            d = w_model / 200 - w_base / 200
            deltas.append(d)
            if d < 0:
                all_ok = False
        if all_ok:
            hit_all += 1
            if statistics.median(deltas) >= 0.15:
                hit_joint += 1
    check(0.60 <= hit_all / trials <= 0.78,
          f"通过率合成验证①：真实 Δ=+10pp → 17 格全过 {hit_all / trials:.1%}"
          f"（带 60–78%：均匀 σ_Δ≈5pp 近似给 Φ(2)^17≈67%；**异方差精确期望 ≈75.5%**——低基线格"
          f"σ_Δ 更小单格更稳，2026-11-24 标定；再偏离=实现与口径不一致）")
    print(f"    · 存证②：联合（全过 ∧ 中位数≥+15pp）通过率 = {hit_joint / trials:.2%}"
          f"（真实 +10pp 下数学期望 ≈0%——中位数项要求真实 ≈+16pp，见上方待拍板注）")

    print("【phase1_criterion 单测】" + ("全部通过" if FAIL == 0 else f"{FAIL} 项失败"))
    return 0 if FAIL == 0 else 1


if __name__ == "__main__":
    sys.exit(run())
