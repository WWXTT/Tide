# -*- coding: utf-8 -*-
"""联络表重建：从 PNG tEXt parameters 块恢复 seed（dispatch 重跑轮会把缓存图 seed 冲成 '?'）。

每个概念一节：中文名+机制注释（取自 manifest note）· 原图（带 seed）· 纯色背景版对照。

用法: python rebuild_index.py output/<task> [manifests/<task>.json]
"""
import glob
import io
import json
import os
import re
import sys

from PIL import Image


def main():
    task_dir = sys.argv[1]
    manifest = sys.argv[2] if len(sys.argv) > 2 else None
    notes = {}
    if manifest and os.path.exists(manifest):
        man = json.load(io.open(manifest, encoding='utf-8'))
        notes = {it['id']: it.get('note', '') for it in man.get('items', [])}

    groups = {}
    for p in sorted(glob.glob(os.path.join(task_dir, '*_v*.png'))):
        base = os.path.splitext(os.path.basename(p))[0]
        if base.endswith('_flat') or '__' in base:
            continue
        m = re.search(r'Seed: (\d+)', Image.open(p).info.get('parameters', ''))
        seed = m.group(1) if m else '?'
        groups.setdefault(re.sub(r'_v\d+$', '', base), []).append((base, seed))

    rows = []
    for mid, members in groups.items():
        members.sort()
        figs = ''.join(
            f'<figure><img src="{n}.png"><figcaption>{n} seed={s}</figcaption></figure>'
            for n, s in members)
        flat = ''.join(
            f'<figure><img src="{n}_flat.png"><figcaption>{n}</figcaption></figure>'
            for n, s in members)
        rows.append(
            f'<section><h2>{mid} <small>{notes.get(mid, "")}</small></h2>'
            f'<div class="grid">{figs}</div><h3>纯色背景版</h3><div class="grid">{flat}</div></section>')

    html = ("<!DOCTYPE html><html><head><meta charset='utf-8'><title>"
            + os.path.basename(task_dir.rstrip('/\\')) + "</title><style>"
            "body{background:#181818;color:#ddd;font-family:sans-serif;margin:24px}"
            "h2 small{color:#999;font-weight:normal;font-size:14px;margin-left:10px}"
            "h3{color:#aaa;font-size:14px;margin:10px 0 4px}"
            "section{margin:28px 0;border-bottom:1px solid #333;padding-bottom:12px}"
            ".grid{display:flex;flex-wrap:wrap;gap:10px}"
            "figure{margin:0;text-align:center}img{width:200px;height:200px;border:1px solid #444;background:#000}"
            "figcaption{color:#777;font-size:12px}</style></head><body>"
            '<h1>' + os.path.basename(task_dir.rstrip('/\\')) + ' · seed 自 PNG 元数据恢复</h1>'
            + ''.join(rows) + "</body></html>")
    out = os.path.join(task_dir, 'index.html')
    io.open(out, 'w', encoding='utf-8').write(html)
    total = sum(len(v) for v in groups.values())
    n_seed = sum(1 for v in groups.values() for _, s in v if s != '?')
    print(f'{out}: {len(groups)} items, {n_seed}/{total} seeds recovered')


if __name__ == '__main__':
    main()
