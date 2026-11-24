# -*- coding: utf-8 -*-
"""6 格标尺统计：池化 Wilson 三态判定（2026-11-20 定案，AI训练说明 §3.4.1）。

背景：`evaluate_greedy_multi` 每次评估 144 局、6 格平分 ≈ 24 局/格。24 局下真值
恰在门槛时仍判"达标"的概率约 49%（= 翻硬币），单次评估的 `pass` 字段不能当决策
依据。本模块把同一门槛代的所有 `vs_script_cell` 行池化，用 95% Wilson 区间给出三态：

    pass       区间整体在门槛之上   → 确认达标
    fail       区间整体在门槛之下   → 确认不达标
    undecided  门槛落在区间内       → 骑线，判不了（**不是**失败）

调用方：
  - `monitor_training.py`：训练中显示池化三态（只读 `eval.jsonl`，不改训练）；
  - 验收/汇报：按本模块口径，不按单次评估的 `pass` 字段。

口径细节（改动须同批同步 `docs/AI训练说明.md` §3.4.1）：
  - **只池化同一门槛代**：基线重测会改 `threshold`，跨代相加是错账；同一 label
    门槛变过 → 只取最新代的行（`generation_mixed=True` 时在展示层标注）；
  - 门槛缺失（11-17 之前的旧 run）→ `state="no_gate"`，只报胜率不判；
  - 区间用 Wilson（n≈24 时比正态近似稳，不会出负下界）；
  - 池化回答的是"这一段训练的平均水平有没有够到门槛"，**不替代**末态估计——
    参数一直在变，池化样本跨多个策略版本（见 §3.4.1 的用途边界）。
"""

from __future__ import annotations

import json
import math
from pathlib import Path

WILSON_Z = 1.959964  # 95%

STATE_LABEL = {"pass": "✅ 确认达标", "fail": "❌ 确认不达标",
               "undecided": "~ 骑线（判不了）", "no_gate": "— 无门槛"}
STATE_MARK = {"pass": "✅", "fail": "❌", "undecided": "~", "no_gate": "—"}


def wilson(k: int, n: int, z: float = WILSON_Z):
    """二项比例的 Wilson 区间 → (p, lo, hi)；n=0 返回 (0,0,0)。"""
    if n <= 0:
        return (0.0, 0.0, 0.0)
    p = k / n
    d = 1.0 + z * z / n
    c = p + z * z / (2 * n)
    h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n))
    return (p, max(0.0, (c - h) / d), min(1.0, (c + h) / d))


def classify(lo: float, hi: float, threshold: float | None) -> str:
    """三态判定：门槛是否整体落在区间外一侧。"""
    if threshold is None:
        return "no_gate"
    if lo > threshold:
        return "pass"
    if hi < threshold:
        return "fail"
    return "undecided"


def load_cell_rows(path):
    """读 `eval.jsonl` 里 `metric=vs_script_cell` 的行（坏行跳过）。"""
    rows = []
    path = Path(path)
    if not path.exists():
        return rows
    for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            r = json.loads(line)
        except json.JSONDecodeError:
            continue
        if r.get("metric") != "vs_script_cell":
            continue
        label = str(r.get("theme") or "")
        n = int(r.get("episodes") or 0)
        if "/P" not in label or n <= 0:
            continue
        rows.append({
            "step": int(r.get("step") or 0),
            "label": label,           # "red/P1"（1 基座次，写侧已 +1）
            "k": int(r.get("wins") or 0),
            "n": n,
            "thr": r.get("threshold"),
            "passed": r.get("pass"),  # 单次标签，仅供展示"单次达标次数"
        })
    rows.sort(key=lambda x: (x["step"], x["label"]))
    return rows


def pooled(rows):
    """按格池化（同代过滤 + Wilson 三态）。

    返回 {label: {thr, k, n, p, lo, hi, state, evals, pass_count, last_step,
                  generation_mixed}}；evals=该格参与池化的评估次数，
    pass_count=其中单次判达标的次数（对照用，不参与判定）。
    """
    by_label = {}
    for r in rows:
        by_label.setdefault(r["label"], []).append(r)
    out = {}
    for label, lst in sorted(by_label.items()):
        with_thr = [x for x in lst if x["thr"] is not None]
        thr = None
        mixed = False
        used = with_thr
        if with_thr:
            thr = with_thr[-1]["thr"]                      # 最新代门槛
            used = [x for x in with_thr if x["thr"] == thr]
            mixed = len(used) != len(with_thr) or len(used) != len(lst)
        k = sum(x["k"] for x in used)
        n = sum(x["n"] for x in used)
        p, lo, hi = wilson(k, n)
        out[label] = {
            "thr": thr, "k": k, "n": n, "p": p, "lo": lo, "hi": hi,
            "state": classify(lo, hi, thr),
            "evals": len(used),
            "pass_count": sum(1 for x in used if x.get("passed") is True),
            "last_step": used[-1]["step"] if used else 0,
            "generation_mixed": mixed,
        }
    return out


def state_counts(cells):
    """三态汇总 {pass, fail, undecided, no_gate}。"""
    counts = {"pass": 0, "fail": 0, "undecided": 0, "no_gate": 0}
    for c in cells.values():
        counts[c["state"]] = counts.get(c["state"], 0) + 1
    return counts


# ---------------------------------------------------------------- 标尺能力（文档数值单源）

def _binom_tail(k, n, p):
    """P(X >= k | n, p)，精确二项。"""
    if k <= 0:
        return 1.0
    if k > n:
        return 0.0
    return min(1.0, sum(math.comb(n, i) * p ** i * (1 - p) ** (n - i)
                        for i in range(k, n + 1)))


def single_pass_prob(true_p: float, threshold: float, n: int) -> float:
    """真值 true_p、n 局时，单次评估判"达标"（win_rate >= threshold）的概率。"""
    return _binom_tail(math.ceil(threshold * n - 1e-9), n, true_p)


def mde(threshold: float, n: int, power: float = 0.8):
    """n 局下单侧 α=5% 能达 power 功放的最小真差距（相对门槛，pp）；达不到返回 None。"""
    hi = 1.0 - threshold
    if hi <= 1e-9 or single_pass_prob(threshold + hi, threshold, n) < power:
        return None
    lo = 1e-6
    for _ in range(60):
        mid = (lo + hi) / 2
        if single_pass_prob(threshold + mid, threshold, n) >= power:
            hi = mid
        else:
            lo = mid
    return hi * 100


def two_sample_n(p: float, delta: float, power: float = 0.8, alpha: float = 0.05) -> int:
    """两个独立评估点比较、分辨 delta 差距所需**每个评估点**的局数（正态近似、双侧 α）。

    只用于回答"看趋势要多少局"这类预算问题；不参与达标判定（判定用 `pooled`）。
    """
    za = 1.959964 if alpha == 0.05 else 1.644854
    zb = 0.841621 if power == 0.8 else 1.2816
    return int(math.ceil(((za + zb) ** 2 * 2 * p * (1 - p)) / (delta ** 2)))
