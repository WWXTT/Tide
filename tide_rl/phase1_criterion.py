"""一期判据单源（A2，2026-11-24 回到一期训练目标·docs/回到一期训练目标.md「验收口径」节）。

**纯标准库**（math/statistics only）——供训练循环、eval_checkpoint、certify_phase1 共用，
不允许 numpy/jax 依赖（单测与认证工具要在无训练环境跑）。

口径（2026-11-24 DSH 修订·用户定案「全格压制」）：
  - 18 格 = 模型执色(3) × 对手主题(3) × 座位(2)；Δ = 模型胜率 − 同格脚本基线；
  - **认证达标** = 17 常规格每格 Δ ≥ 0（点估计先看；某格 95% 区间跨 0 → **先加测该格**
    （+200 局）再判，不直接算输）+ 17 格中位数 ≥ +15pp；
  - **饱和格**（基线 +15pp > 100%，现只有绿镜先手 0.978）：不退步判——模型胜率 ≥ 基线 −5pp；
  - **best 选点** = 17 格 Δ 的**最小 Wilson 下界**（不是最小点估计：每格 24 局时 σ_Δ≈14pp，
    取最小点估计=在噪声格里挑最幸运的那个）；
  - **训练内候选门**（每格 24 局，432 局/次）：常规格 Δ ≥ −5pp——只用于选点与预警，
    **不作达标依据**；「连中 3 次即停」在 σ_Δ≈14pp 下几乎不触发（0.64^17≈5e-4）；
  - **训练内早停门**（2026-11-22 用户拍板稳健版，原值迁入）：覆盖齐 18 格且 Δ 中位数 ≥ +5pp
    且 Δ≥−5pp 格数 ≥ 16/18——容忍 ≤2 格恶化，对 24 局/格 ±12pp 单格噪声鲁棒；
  - 噪声账：认证每格 200 局时胜率 1σ≈3.5pp、σ_Δ≈5pp、95% 区间≈±10pp；真实 Δ=+15pp 的格
    几乎必过（z=3），真实 +10pp 每格 2.3% 概率测负（17 格全过≈67%）。

基线表/BASELINE_ERA 自 train_tide_complete.py 迁入（2026-11-24 A2）：**值不动**——B2 按
新卡价（死亡计数 +2 开启费时代）每格 200 局重测后回填，并同步升 BASELINE_ERA。
"""
import math
import statistics

# ==================== 基线表（迁移，值不动；B2 重测后回填） ====================

# 6 格口径（模型执三套预组 × 先/后手）——由 18 格矩阵**等权平均**得到（C7-d ② 还原互证口径），
# 2026-11-24 B2 全量重测后回填（每格 = 三对 × 200 局 = 600 局合计）。
PRESET_SEAT_BASELINES = {("red", 0): 0.602, ("green", 0): 0.730, ("blue", 0): 0.418,
                         ("red", 1): 0.560, ("green", 1): 0.283, ("blue", 1): 0.453}
# 兼容旧名（外部脚本/旧文档引用）
RED_CELL_BASELINES = PRESET_SEAT_BASELINES

# 18 格矩阵脚本基线（**2026-11-24 B2 全量重测**）：headless 执行器「脚本执该预组 × 该座位 ×
# 对脚本主题」胜率，9 对 × 400 局 = **每格 200 局**（旧表每格仅 88~116 局，σ 约 4.8pp）。
# 产物：Tide_train/tide_rl/logs/script_baseline_20261122.json（seed 20261122，协议 eval_matrix v2 同构）。
SCRIPT_MATRIX_BASELINES = {
    ("blue", "blue", 0): 0.410,   # 82/200, CI95 0.344-0.479
    ("blue", "blue", 1): 0.605,   # 121/200, CI95 0.536-0.670
    ("blue", "green", 0): 0.430,  # 86/200, CI95 0.363-0.499
    ("blue", "green", 1): 0.405,  # 81/200, CI95 0.339-0.474
    ("blue", "red", 0): 0.415,    # 83/200, CI95 0.349-0.484
    ("blue", "red", 1): 0.350,    # 70/200, CI95 0.287-0.418
    ("green", "blue", 0): 0.625,  # 125/200, CI95 0.556-0.689
    ("green", "blue", 1): 0.575,  # 115/200, CI95 0.506-0.641
    ("green", "green", 0): 0.965, # 193/200, CI95 0.929-0.983
    ("green", "green", 1): 0.025, # 5/200, CI95 0.011-0.057（极性已复 TideSim）
    ("green", "red", 0): 0.600,   # 120/200, CI95 0.531-0.665
    ("green", "red", 1): 0.250,   # 50/200, CI95 0.195-0.314
    ("red", "blue", 0): 0.710,    # 142/200, CI95 0.644-0.768
    ("red", "blue", 1): 0.630,    # 126/200, CI95 0.561-0.694
    ("red", "green", 0): 0.755,   # 151/200, CI95 0.691-0.809
    ("red", "green", 1): 0.385,   # 77/200, CI95 0.320-0.454
    ("red", "red", 0): 0.340,     # 68/200, CI95 0.278-0.408
    ("red", "red", 1): 0.665,     # 133/200, CI95 0.597-0.727
}

# 基线纪元：基线表与判据绑定执行器代码版本——D2a（正牌技能透传）之前的 best 检查点全部作废。
# save_model("best") 盖 era 戳；eval_checkpoint.pick_ckpt 拒载旧 era 的 best。
# **v21frozen-20261124-script-400**：B2 全量重测（每格 200 局）已回填，判据与基线同时换纪元；
# 旧值（每格 88~116 局）与新城值差最多 8pp，属小样本噪声量级（n≈100 时 σ≈4.8pp），
# 但新城精度翻倍，认证 Δ 的合成区间更可信。旧 era 的 run 一律拒证（certify 硬闸②）。
BASELINE_ERA = "v21frozen-20261124-script-400"

# ==================== 口径常量 ====================

MARGIN_TARGET = 0.15      # 认证：17 常规格 Δ 中位数 ≥ +15pp
SATURATED_TOL = 0.05      # 饱和格不退步容差：模型 ≥ 基线 −5pp
CANDIDATE_GATE = -0.05    # 训练内候选门：常规格 Δ ≥ −5pp（预警/选点，不作达标）
CI_Z = 1.96               # 95% 区间
CERT_N_PER_CELL = 200     # 认证每格局数（σ_Δ≈5pp、95% 区间≈±10pp 的前提）
BASE_N = 200              # 基线侧每格局数（B2 实测口径；Δ 区间合成用）
# 训练内早停门（2026-11-22 稳健版，原值迁入——与认证门分离）
TRAIN_GATE_MEDIAN = 0.05  # 覆盖齐 18 格且 Δ 中位数 ≥ +5pp
TRAIN_GATE_ROBUST = 16    # Δ ≥ −5pp 的格数 ≥ 16/18
TRAIN_GATE_TOL = -0.05

SATURATED_CELLS = tuple(k for k, v in SCRIPT_MATRIX_BASELINES.items()
                        if v + MARGIN_TARGET > 1.0)  # 现只有绿镜先手


# ==================== 统计原语 ====================

def wilson_interval(w, n, z=CI_Z):
    """Wilson 评分区间（胜率 w/n 的 95% 置信）；n<=0 返回 (0,1) 全开（不参与硬判）。"""
    if n is None or n <= 0:
        return 0.0, 1.0
    p = w / n
    denom = 1 + z * z / n
    center = (p + z * z / (2 * n)) / denom
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / denom
    return center - half, center + half


def wilson_lower(w, n, z=CI_Z):
    lo, _ = wilson_interval(w, n, z)
    return lo


def is_saturated(base):
    """饱和格判定：基线 +15pp > 100%（不退步口径，不参与中位数）。"""
    return base is not None and base + MARGIN_TARGET > 1.0


def delta_ci(w, n, base, base_n=BASE_N):
    """Δ = p_model − p_base 的点估计与近似 95% 区间（正态合成两侧独立 σ；基线侧按
    base_n 局计——判 Δ 跨 0 用，比 Wilson-on-model 单侧更保守一点点，量级一致）。"""
    if n is None or n <= 0 or base is None:
        return None, None, None
    p = w / n
    delta = p - base
    var = p * (1 - p) / n + base * (1 - base) / base_n
    half = CI_Z * math.sqrt(var)
    return delta, delta - half, delta + half


# ==================== 判定 / 选点 ====================

def _iter_cells(cells):
    """cells: {(model_preset, opp_theme, seat): (wins, n)} → [(key, w, n, base)]（基线齐的格）。"""
    out = []
    for key, (w, n) in (cells or {}).items():
        base = SCRIPT_MATRIX_BASELINES.get(key)
        if base is None or not n:
            continue
        out.append((key, w, n, base))
    return out


def judge(cells):
    """认证判定（每格 CERT_N_PER_CELL=200 局口径；certify_phase1 消费）。

    返回 dict：
      per_cell: [{key, base, wr, delta, ci_lo, ci_hi, verdict}]，verdict ∈
        pass / fail / needs_more（95% 区间跨 0——先加测 +200 局再判）/
        saturated_pass / saturated_fail / no_base；
      median_delta：17 常规格 Δ 中位数（覆盖不足时为 None）；
      pass_：bool（点级判定：17 格全 Δ≥0（或 needs_more 视为待定不算输）且中位数达标
        且饱和格不退步）——**needs_more 的最终裁决在加测后重跑 judge**；
      needs_more / covered / expected。
    """
    per, regular = [], []
    for key, w, n, base in _iter_cells(cells):
        wr = w / n
        rec = {"key": key, "base": base, "wr": wr, "n": n}
        if is_saturated(base):
            ok = wr >= base - SATURATED_TOL
            rec.update(delta=wr - base, verdict="saturated_pass" if ok else "saturated_fail")
        else:
            d, lo, hi = delta_ci(w, n, base)
            rec.update(delta=d, ci_lo=lo, ci_hi=hi)
            if d >= 0:
                rec["verdict"] = "pass"
            elif lo <= 0 <= hi:
                rec["verdict"] = "needs_more"  # 区间跨 0：先加测再判，不直接算输
            else:
                rec["verdict"] = "fail"
            regular.append(d)
        per.append(rec)
    median_delta = statistics.median(regular) if len(regular) == len(SCRIPT_MATRIX_BASELINES) - len(SATURATED_CELLS) and regular else None
    needs_more = [r["key"] for r in per if r.get("verdict") == "needs_more"]
    # 只对常规格要求 Δ≥0（饱和行走不退步判，不进本条——否则 saturated_pass 恒拖垮全过）
    all_regular_ok = all(r.get("verdict") in ("pass", "needs_more")
                         for r in per if not is_saturated(r["base"]))
    saturated_ok = all(r.get("verdict") != "saturated_fail" for r in per)
    pass_ = (median_delta is not None and median_delta >= MARGIN_TARGET
             and all_regular_ok and saturated_ok and not needs_more)
    return {"per_cell": per, "median_delta": median_delta, "pass": pass_,
            "needs_more": needs_more, "covered": len(per), "expected": len(SCRIPT_MATRIX_BASELINES)}


def select_value(cells):
    """best 选点值 = 常规格 Δ 的最小 Wilson 下界（wilson_lower(w,n) − base；基线视作常数——
    选点只比大小，同向保守不改变排序语义）。返回 (value, worst_key)；无常规格 → (None, None)。"""
    vals = []
    for key, w, n, base in _iter_cells(cells):
        if is_saturated(base):
            continue
        vals.append((wilson_lower(w, n) - base, key))
    if not vals:
        return None, None
    val, key = min(vals, key=lambda vk: vk[0])
    return val, key


def candidate_gate(cells):
    """训练内候选门（每格 24 局）：常规格 Δ ≥ −5pp——只用于选点与预警，不作达标依据。
    返回 {below: [key...]（Δ<−5pp 的格）, worst: (key, delta) or None}。"""
    below, worst = [], None
    for key, w, n, base in _iter_cells(cells):
        if is_saturated(base):
            continue
        d = w / n - base
        if worst is None or d < worst[1]:
            worst = (key, d)
        if d < CANDIDATE_GATE:
            below.append(key)
    return {"below": below, "worst": worst}


def training_gate(cells):
    """训练内早停门（2026-11-22 用户拍板稳健版，原值自训练循环迁入）：覆盖齐 18 格且
    Δ 中位数 ≥ +5pp 且 Δ ≥ −5pp 格数 ≥ 16/18。返回 {pass, reason, median_delta, robust_cells}。"""
    ds = []
    for key, w, n, base in _iter_cells(cells):
        if not is_saturated(base):
            ds.append(w / n - base)
    full_cover = len(_iter_cells(cells)) == len(SCRIPT_MATRIX_BASELINES) and len(ds) == len(SCRIPT_MATRIX_BASELINES) - len(SATURATED_CELLS)
    if not full_cover:
        return {"pass": False, "reason": "cover-incomplete",
                "median_delta": None, "robust_cells": None}
    med = statistics.median(ds)
    robust = sum(1 for d in ds if d >= TRAIN_GATE_TOL)
    ok = med >= TRAIN_GATE_MEDIAN and robust >= TRAIN_GATE_ROBUST
    return {"pass": ok, "reason": ("median>=+5pp & >=%d/18 cells>=-5pp" % TRAIN_GATE_ROBUST) if ok else "gate-not-met",
            "median_delta": med, "robust_cells": robust}


# ==================== 兼容门槛（旧调用方） ====================

def matrix_cell_threshold(model_preset, opp_theme, seat):
    """18 格 legacy margin 门槛（基线 +15pp）——A2（2026-11-24）起 selector 改用
    select_value 的 Wilson 下界 Δ，本函数仅供旧报表/对账口径保留。未覆盖格返回 None。"""
    base = SCRIPT_MATRIX_BASELINES.get((model_preset, opp_theme, seat))
    return None if base is None else base + MARGIN_TARGET


def cell_threshold(preset_key, seat):
    """6 格行门槛（A2 2026-11-24 同步）：**= 同格脚本基线**（Δ ≥ 0 语义——认证口径「每格
    Δ≥0」的 6 格报表版）。旧「基线 +15pp」随「每格 +15pp」口径废弃（2026-11-24 DSH 修订：
    高基线格结构性不可达）。未知格返回 None（不参与判定）。"""
    base = PRESET_SEAT_BASELINES.get((preset_key, seat))
    return base
