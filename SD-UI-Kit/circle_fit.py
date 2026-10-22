# -*- coding: utf-8 -*-
"""圆形收拢度量：验证"主体包含在中心圆形区域"性质，服务圆裁扣图。

对 flatten 后的 <name>_flat.png（背景已纯色化）计算：
  - mask     = 与背景中位色距离 > tol 的像素（主体）
  - contain  = mask 落在内切圆（r=0.5*min(w,h)，可 --shrink 略缩）内的比例
  - centroid = 主体质心相对图心的偏移（归一化）
  - r99      = 主体像素到图心距离的 99 分位 / 内切圆半径（>1 即伸出圆外）
判定：contain>=0.98 且 r99<=1.02 视为"一个圆基本扣出来"。

用法: python circle_fit.py output/<task> [--tol 30] [--shrink 0.97] [--json]
"""
import argparse
import glob
import io
import json
import os

import numpy as np
from PIL import Image


def circle_metrics(path, tol=30.0, shrink=0.97):
    img = np.asarray(Image.open(path).convert("RGB")).astype(np.float64)
    h, w = img.shape[:2]
    # 背景色 = 四角 8x8 中位（flatten 后为精确纯色）
    corners = np.vstack([
        img[:8, :8].reshape(-1, 3), img[:8, -8:].reshape(-1, 3),
        img[-8:, :8].reshape(-1, 3), img[-8:, -8:].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    mask = np.sqrt(((img - bg) ** 2).sum(axis=2)) > tol
    if not mask.any():
        return None
    yy, xx = np.nonzero(mask)
    cy, cx = (h - 1) / 2.0, (w - 1) / 2.0
    r_circle = 0.5 * min(w, h) * shrink
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    contain = float((dist <= r_circle).mean())
    r99 = float(np.percentile(dist, 99) / r_circle)
    cen = (float(xx.mean() - cx) / r_circle, float(yy.mean() - cy) / r_circle)
    return {
        "contain": round(contain, 4), "r99": round(r99, 3),
        "centroid": [round(c, 3) for c in cen],
        "mask_frac": round(float(mask.mean()), 4),
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("task_dir")
    ap.add_argument("--tol", type=float, default=30.0)
    ap.add_argument("--shrink", type=float, default=0.97)
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args()
    rows, fails = {}, []
    for p in sorted(glob.glob(os.path.join(args.task_dir, "*_v*_flat.png"))):
        m = circle_metrics(p, args.tol, args.shrink)
        if m is None:
            continue
        name = os.path.basename(p).replace("_flat.png", "")
        rows[name] = m
        ok = m["contain"] >= 0.98 and m["r99"] <= 1.02
        if not ok:
            fails.append(name)
        print(f"{'PASS' if ok else 'FAIL'} {name:24s} contain={m['contain']*100:6.2f}% "
              f"r99={m['r99']:5.3f} centroid=({m['centroid'][0]:+.3f},{m['centroid'][1]:+.3f}) "
              f"mask={m['mask_frac']*100:5.2f}%")
    with io.open(os.path.join(args.task_dir, "circle_fit.json"), "w", encoding="utf-8") as fh:
        json.dump(rows, fh, ensure_ascii=False, indent=1)
    print(f"\n{len(rows)} imgs, {len(fails)} fail -> {fails if fails else 'none'}")


if __name__ == "__main__":
    main()
