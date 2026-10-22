# -*- coding: utf-8 -*-
"""背景纯色化：对 output/<task>/ 下的候选图做「边框连通区域泛洪 → 替换为边框中位色」。

只处理与图像边缘连通的背景，不碰徽记内部；产出 <name>_flat.png 与 index_flat.html。
用途：SD 出图背景近似纯色但有低频噪染，扣图（BiRefNet）与视觉一致性都受益于数学纯色背景。

用法:
  python flatten_bg.py output/<task> [--tol 40] [--force]
"""
import argparse
import glob
import io
import json
import os
import re

import numpy as np
from PIL import Image

try:
    from scipy import ndimage
except ImportError:
    ndimage = None


def border_ring(arr, frac=0.02):
    h, w = arr.shape[:2]
    t = max(2, int(min(h, w) * frac))
    top = arr[:t].reshape(-1, 3)
    bot = arr[-t:].reshape(-1, 3)
    left = arr[t:-t, :t].reshape(-1, 3)
    right = arr[t:-t, -t:].reshape(-1, 3)
    return np.vstack([top, bot, left, right])


def flood_from_border(mask):
    """与图像边缘连通的 True 区域。scipy 可用时走 label，否则退化为 BFS。"""
    if ndimage is not None:
        lbl, n = ndimage.label(mask)
        border_labels = set(np.unique(np.concatenate(
            [lbl[0], lbl[-1], lbl[:, 0], lbl[:, -1]])))
        border_labels.discard(0)
        return np.isin(lbl, list(border_labels))
    from collections import deque
    h, w = mask.shape
    visited = np.zeros((h, w), dtype=bool)
    dq = deque()
    for y in range(h):
        for x in (0, w - 1):
            if mask[y, x] and not visited[y, x]:
                visited[y, x] = True
                dq.append((y, x))
    for x in range(w):
        for y in (0, h - 1):
            if mask[y, x] and not visited[y, x]:
                visited[y, x] = True
                dq.append((y, x))
    while dq:
        y, x = dq.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not visited[ny, nx]:
                visited[ny, nx] = True
                dq.append((ny, nx))
    return visited


def flatten(img, tol):
    arr = np.asarray(img.convert("RGB")).astype(np.int16)
    ring = border_ring(arr)
    med = np.median(ring, axis=0)
    std = float(np.std(ring.astype(np.float64)))
    dist = np.sqrt(((arr - med) ** 2).sum(axis=2))
    bg = flood_from_border(dist < tol)
    out = arr.copy()
    out[bg] = med.astype(np.int16)
    return Image.fromarray(out.astype(np.uint8)), {
        "bg_median": [int(c) for c in med],
        "border_std": round(std, 2),
        "flattened_frac": round(float(bg.mean()), 4),
    }


def write_index(path, task, entries):
    groups = {}
    for name, meta in entries.items():
        mid = re.sub(r"_v\d+$", "", name)
        groups.setdefault(mid, []).append((name, meta))
    rows = []
    for mid, members in groups.items():
        figs = "".join(
            f'<figure><img src="{n}_flat.png"><figcaption>{n} · '
            f"bg={meta['bg_median']} · flat={meta['flattened_frac']*100:.1f}%</figcaption></figure>"
            for n, meta in sorted(members))
        rows.append(f'<section><h2>{mid}</h2><div class="grid">{figs}</div></section>')
    html = (
        "<!DOCTYPE html><html><head><meta charset='utf-8'><title>" + task +
        " · flat bg</title><style>body{background:#181818;color:#ddd;font-family:sans-serif;margin:24px}"
        "section{margin:24px 0}.grid{display:flex;flex-wrap:wrap;gap:12px}"
        "figure{margin:0;text-align:center}img{width:220px;height:220px;border:1px solid #444}"
        "figcaption{color:#777;font-size:12px}</style></head><body>"
        "<h1>" + task + " · 纯色化背景（泛洪=边框连通区→边框中位色）</h1>"
        + "".join(rows) + "</body></html>")
    with io.open(path, "w", encoding="utf-8") as fh:
        fh.write(html)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("task_dir")
    ap.add_argument("--tol", type=float, default=40.0)
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()
    entries = {}
    metrics = {}
    for p in sorted(glob.glob(os.path.join(args.task_dir, "*_v*.png"))):
        base = os.path.splitext(os.path.basename(p))[0]
        if base.endswith("_flat") or "__" in base:
            continue
        out_p = os.path.join(args.task_dir, base + "_flat.png")
        if os.path.exists(out_p) and not args.force:
            continue
        img = Image.open(p)
        flat, meta = flatten(img, args.tol)
        flat.save(out_p)
        entries[base] = meta
        metrics[base] = meta
        print(f"[flat] {base}: bg={meta['bg_median']} std={meta['border_std']} frac={meta['flattened_frac']}")
    if entries:
        write_index(os.path.join(args.task_dir, "index_flat.html"),
                    os.path.basename(args.task_dir.rstrip("/\\")), entries)
        with io.open(os.path.join(args.task_dir, "flat_metrics.json"), "w", encoding="utf-8") as fh:
            json.dump(metrics, fh, ensure_ascii=False, indent=1)
        print("done -> index_flat.html")


if __name__ == "__main__":
    main()
