# -*- coding: utf-8 -*-
"""组件抠图（2026-10-11）：output/ 根的 6 张定版组件 → RGBA 透明通道。

规则（数据层，无 ML）：
- 外区：与画布边缘连通的、颜色接近边框中位色的区域 → 透明（帧类暗底 tol=55 / 纸板类 tol=45 自动判）。
- 中心：镂空框类（frame_*）另做中心种子泛洪 → 中心透明（后期叠插画）。
- 孤岛：外区中被亮像素挡住的碎块（星点/辉光）→ 清除（只保留连通到主体的组件）。
- 羽化：alpha 高斯 1px。

用法: python matte.py            # 处理 output/ 根全部定版 PNG
      python matte.py --check    # 只跑验收不落盘
"""
import argparse
import glob
import os

import numpy as np
from PIL import Image

try:
    from scipy import ndimage
except ImportError:
    ndimage = None

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "output")
MATTE = os.path.join(OUT, "matted")


def border_ring(arr, frac=0.02):
    h, w = arr.shape[:2]
    t = max(2, int(min(h, w) * frac))
    return np.vstack([arr[:t].reshape(-1, 3), arr[-t:].reshape(-1, 3),
                      arr[t:-t, :t].reshape(-1, 3), arr[t:-t, -t:].reshape(-1, 3)])


def flood_from_border(mask):
    lbl, n = ndimage.label(mask)
    border_labels = set(np.unique(np.concatenate(
        [lbl[0], lbl[-1], lbl[:, 0], lbl[:, -1]])))
    border_labels.discard(0)
    return np.isin(lbl, list(border_labels)), lbl


def band_mask(kind, w, h):
    """帧类带区几何掩码（与 dispatch._offset_poly 同口径）——验收带区不透明/监测描边渗漏。"""
    import dispatch as D
    from PIL import ImageDraw
    m = max(2, int(min(w, h) * 0.02)); spill = max(6, int(min(w, h) * 0.03)); m2 = m + spill
    tot = int(min(w, h) * 0.10); w_out = int(tot * 0.40); w_gap = int(tot * 0.50)
    pad = 4  # 带区内缩 4px，容忍 SD 漂移
    def poly_m(pts):
        img = Image.new("L", (w, h), 0); ImageDraw.Draw(img).polygon(pts, fill=1)
        return np.asarray(img, bool)
    if kind == "ring":
        R = min(w, h) / 2 - m - spill
        yy, xx = np.mgrid[0:h, 0:w]
        r = np.sqrt((xx - w / 2.0) ** 2 + (yy - h / 2.0) ** 2)
        return (r >= R - w_out - pad) & (r <= R - pad)
    if kind == "square":
        hi = [(m2 + w_out + pad, m2 + w_out + pad), (w - m2 - w_out - pad, m2 + w_out + pad),
              (w - m2 - w_out - pad, h - m2 - w_out - pad), (m2 + w_out + pad, h - m2 - w_out - pad)]
        lo = [(m2 + w_out + w_gap - pad, m2 + w_out + w_gap - pad), (w - m2 - w_out - w_gap + pad, m2 + w_out + w_gap - pad),
              (w - m2 - w_out - w_gap + pad, h - m2 - w_out - w_gap + pad), (m2 + w_out + w_gap - pad, h - m2 - w_out - w_gap + pad)]
        return poly_m(hi) & ~poly_m(lo)
    # tri（正三角 v6 外接圆构造）
    Rc = min((w / 2.0 - m2 - w_out / 2.0) / 0.8660254, (h / 2.0 - m - w_out / 2.0) / 0.75)
    ccy = h / 2.0 - Rc * 0.25
    ideal = [(w / 2.0 - Rc * 0.8660254, ccy - Rc * 0.5),
             (w / 2.0 + Rc * 0.8660254, ccy - Rc * 0.5),
             (w / 2.0, ccy + Rc)]
    hi = D._offset_poly(ideal, w_out + pad)
    lo = D._offset_poly(ideal, w_out + w_gap - pad)
    return poly_m(hi) & ~poly_m(lo)


def matte(path):
    img = Image.open(path).convert("RGB")
    arr = np.asarray(img).astype(np.int16)
    h, w = arr.shape[:2]
    med = np.median(border_ring(arr), axis=0)
    tol = 45.0 if med.mean() > 100 else 55.0  # 纸板类（羊皮纸底）紧 / 帧类（暗底）松
    dist = np.sqrt(((arr - med) ** 2).sum(axis=2))
    bg_like = dist < tol
    outside, lbl = flood_from_border(bg_like)
    transparent = outside.copy()
    is_frame = "frame_" in os.path.basename(path)
    if is_frame:  # 中心种子泛洪（镂空中心与外区不连通，单独清）
        cl = lbl[h // 2, w // 2]
        if cl != 0:
            transparent |= (lbl == cl)
    # 孤岛清除：只保留连通到最大不透明组件的块
    opaque = ~transparent
    olbl, on = ndimage.label(opaque)
    if on > 1:
        sizes = ndimage.sum(opaque, olbl, range(1, on + 1))
        keep = int(np.argmax(sizes)) + 1
        for i in range(1, on + 1):
            if i != keep and sizes[i - 1] > sizes[keep - 1] * 0.05:
                print(f"  [warn] {os.path.basename(path)}: 次大组件 {int(sizes[i-1])}px 未清除（可能是真实结构）")
        opaque = olbl == keep
    # 羽化 1px
    alpha = (opaque * 255).astype(np.float32)
    alpha = ndimage.gaussian_filter(alpha, 1.0)
    alpha = np.clip(alpha, 0, 255).astype(np.uint8)
    rgba = np.dstack([arr.astype(np.uint8), alpha[..., None]])
    # 验收
    corners = np.mean([alpha[:6, :6].mean(), alpha[:6, -6:].mean(), alpha[-6:, :6].mean(), alpha[-6:, -6:].mean()])
    center = alpha[h // 2 - 12:h // 2 + 12, w // 2 - 12:w // 2 + 12].mean()
    checks = {"角落均alpha": round(float(corners), 1)}
    if is_frame:
        checks["中心均alpha"] = round(float(center), 1)
        kind = "ring" if "ring" in path else ("square" if "square" in path else "tri")
        bm = band_mask(kind, w, h)
        checks["带区不透明率%"] = round(float((alpha[bm] > 200).mean() * 100), 1)
    else:
        checks["中心不透明率%"] = round(float((alpha[h // 2 - 12:h // 2 + 12, w // 2 - 12:w // 2 + 12] > 200).mean() * 100), 1)
    checks["透明占比%"] = round(float((alpha < 10).mean() * 100), 1)
    return Image.fromarray(rgba, "RGBA"), checks


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    if not args.check:
        os.makedirs(MATTE, exist_ok=True)
    files = [p for p in sorted(glob.glob(os.path.join(OUT, "*.png")))
             if "_matte" not in p and "slab_sigil_v1" not in p]
    for p in files:
        img, checks = matte(p)
        name = os.path.splitext(os.path.basename(p))[0] + "_matte.png"
        status = []
        if checks.get("角落均alpha", 255) > 30: status.append("角落未透!")
        if "frame_" in p and checks.get("中心均alpha", 0) > 30: status.append("中心未透!")
        if "带区不透明率%" in checks and checks["带区不透明率%"] < 95: status.append("带区渗漏!")
        print(f"[matte] {os.path.basename(p):36s} {checks} {' '.join(status) or 'OK'}")
        if not args.check:
            img.save(os.path.join(MATTE, name))
    if not args.check:
        print("done ->", MATTE)


if __name__ == "__main__":
    main()
