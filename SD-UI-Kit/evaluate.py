# -*- coding: utf-8 -*-
"""形状保持量化评估：生成图 vs 形状模板的结构相关分。

皮尔逊相关（下采样 64x64 灰度、零均值归一）：
  r > 0.85  形状完全保持（风格弱）
  0.60~0.85 形状在、风格化的目标区间
  < 0.60    形状开始跑偏
用法: python evaluate.py output/<task>
"""
import glob
import json
import os
import sys

import numpy as np
from PIL import Image


def to_grid(img):
    g = np.asarray(img.convert("L").resize((64, 64), Image.LANCZOS), dtype=np.float64)
    g -= g.mean()
    n = np.linalg.norm(g)
    return g / n if n > 1e-6 else g


def corr(a, b):
    return float((a * b).sum())


def main():
    task_dir = sys.argv[1]
    report = []
    for png in sorted(glob.glob(os.path.join(task_dir, "*_dn*.png")) + glob.glob(os.path.join(task_dir, "*_v*.png"))):
        name = os.path.basename(png)
        cid = name.split("_dn")[0].split("_v")[0]
        tmpl = os.path.join(task_dir, cid + "__template.png")
        if not os.path.exists(tmpl):
            continue
        r = corr(to_grid(Image.open(png)), to_grid(Image.open(tmpl)))
        report.append((name, r))
    for name, r in report:
        bar = "#" * int(max(0.0, r) * 40)
        print(f"{r:5.2f}  {bar:<42} {name}")
    out = os.path.join(task_dir, "shape_scores.json")
    with open(out, "w", encoding="utf-8") as fh:
        json.dump({n: round(r, 3) for n, r in report}, fh, indent=1)
    print("\n→", out)


if __name__ == "__main__":
    main()
