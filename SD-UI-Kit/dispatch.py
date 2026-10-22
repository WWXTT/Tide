# -*- coding: utf-8 -*-
"""SD-UI-Kit 批量调度器：manifest(组件清单) → 形状模板底图 → A1111 img2img → 候选 PNG + 联络表。

用法:
  python dispatch.py manifests/xxx.json              # 跑全部（已存在的跳过，断点续跑）
  python dispatch.py manifests/xxx.json --only btn   # 只跑 id 含 btn 的项
  python dispatch.py manifests/xxx.json --force      # 覆盖重跑
  python dispatch.py manifests/xxx.json --dry-run    # 只算尺寸/模板不调 API
  python dispatch.py manifests/xxx.json --scan 0.55,0.65,0.75,0.85  # denoise 扫描（校准用）

manifest 格式见 manifests/calibration.json。风格在 styles.json，组件类型在 component_specs.json。
"""
import argparse
import base64
import io
import json
import math
import os
import sys
import time

import numpy as np
import requests
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))


def load_json(path):
    with io.open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


# ---------- 尺寸换算 ----------
def gen_size(w, h, spec):
    """Unity 尺寸 → 生图尺寸：外扩 pad、长宽比 clamp、面积基准、64 对齐、边限 [256,1024]。
    spec["exact"]=True 时跳过比率钳制与面积归一，按真实长宽比以长边 1024 直出（8 对齐）。"""
    pad = spec.get("pad", 1.10)
    base = spec.get("base_px", 512)
    w2, h2 = w * pad, h * pad
    if spec.get("exact"):
        s = spec.get("exact_base", 1024.0) / max(w2, h2)
        gw = max(64, min(1024, int(round(w2 * s / 8)) * 8))
        gh = max(64, min(1024, int(round(h2 * s / 8)) * 8))
        return gw, gh
    ratio = w2 / h2
    lo, hi = 1 / 2.8, 2.8
    if ratio > hi:
        w2 = h2 * hi
    elif ratio < lo:
        h2 = w2 / lo
    scale = (base * base / (w2 * h2)) ** 0.5
    gw, gh = w2 * scale, h2 * scale
    gw = int(min(1024, max(256, round(gw / 64) * 64)))
    gh = int(min(1024, max(256, round(gh / 64) * 64)))
    return gw, gh


# ---------- 形状模板底图 ----------
def add_noise(img, amp=8):
    arr = np.asarray(img).astype(np.int16)
    noise = np.random.default_rng(7).integers(-amp, amp + 1, arr.shape[:2])
    arr[..., :3] += noise[..., None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def _offset_poly(pts, d):
    """凸多边形各边向质心方向平行内缩 d 后求相邻边交点——等宽带的正确做法（同态缩放在角处变窄）。"""
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    n = len(pts)
    lines = []
    for i in range(n):
        x1, y1 = pts[i]
        x2, y2 = pts[(i + 1) % n]
        ex, ey = x2 - x1, y2 - y1
        L = (ex * ex + ey * ey) ** 0.5
        ex, ey = ex / L, ey / L
        nx, ny = -ey, ex
        mx, my = (x1 + x2) / 2.0, (y1 + y2) / 2.0
        if (cx - mx) * nx + (cy - my) * ny < 0:
            nx, ny = -nx, -ny
        lines.append(((mx + nx * d, my + ny * d), (ex, ey)))
    out = []
    for i in range(n):
        p1, d1 = lines[i - 1]
        p2, d2 = lines[i]
        den = d1[0] * d2[1] - d1[1] * d2[0]
        t = ((p2[0] - p1[0]) * d2[1] - (p2[1] - p1[1]) * d2[0]) / den
        out.append((p1[0] + d1[0] * t, p1[1] + d1[1] * t))
    return out


def _in_poly(x, y, pts):
    inside = False
    j = len(pts) - 1
    for i in range(len(pts)):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


# ---------- 六元素线条图案（2026-10-12：红火焰/蓝水滴/绿幼苗/白太阳/黑月牙/灰星星）----------
# 单位坐标系（中心原点，y 向下=屏幕系），由 elem_glyph 分支缩放到画布。
def _bez(p0, p1, p2, p3, n=28):
    """三次贝塞尔采样。"""
    out = []
    for i in range(n + 1):
        t = i / float(n)
        mt = 1.0 - t
        out.append((
            mt * mt * mt * p0[0] + 3 * mt * mt * t * p1[0] + 3 * mt * t * t * p2[0] + t * t * t * p3[0],
            mt * mt * mt * p0[1] + 3 * mt * mt * t * p1[1] + 3 * mt * t * t * p2[1] + t * t * t * p3[1]))
    return out


def _arc(cx, cy, r, a0, a1, n=48):
    """圆弧采样（角度制；y 向下系里 90°=屏下方）。a1<a0 即反向，跨任意区间。"""
    out = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / float(n))
        out.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return out


def _circ_xc(cA, rA, cB, rB):
    """两圆交点（无交点返回 None）。"""
    dx, dy = cB[0] - cA[0], cB[1] - cA[1]
    d = math.hypot(dx, dy)
    if d <= 0 or d > rA + rB or d < abs(rA - rB):
        return None
    a = (rA * rA - rB * rB + d * d) / (2 * d)
    h2 = rA * rA - a * a
    if h2 < 0:
        return None
    h = math.sqrt(h2)
    xm, ym = cA[0] + a * dx / d, cA[1] + a * dy / d
    return [(xm + h * dy / d, ym - h * dx / d), (xm - h * dy / d, ym + h * dx / d)]


def _arc_between(C, r, pa, pb, ref, toward=False):
    """圆 C 上从角 pa 画到 pb：两条弧里选中点离 ref 较远（toward=True 较近）的一条。"""
    d_short = (pb - pa + 180.0) % 360.0 - 180.0
    best_dd, best_d = None, None
    for dd in (d_short, d_short - math.copysign(360.0, d_short or 1.0)):
        mid = pa + dd / 2.0
        mx = C[0] + r * math.cos(math.radians(mid))
        my = C[1] + r * math.sin(math.radians(mid))
        dist = math.hypot(mx - ref[0], my - ref[1])
        if best_d is None or (dist < best_d if toward else dist > best_d):
            best_dd, best_d = dd, dist
    return _arc(C[0], C[1], r, pa, pa + best_dd, 60)


def _glyph_flame():
    main = []
    main += _bez((0.55, 0.30), (0.58, 0.62), (0.26, 0.82), (0.0, 0.82))
    main += _bez((0.0, 0.82), (-0.26, 0.82), (-0.55, 0.60), (-0.52, 0.32))
    main += _bez((-0.52, 0.32), (-0.50, 0.02), (-0.34, -0.32), (-0.13, -0.55))
    main += _bez((-0.13, -0.55), (-0.02, -0.68), (0.06, -0.80), (0.02, -0.95))
    main += _bez((0.02, -0.95), (0.14, -0.72), (0.34, -0.44), (0.44, -0.10))
    main += _bez((0.44, -0.10), (0.53, 0.08), (0.55, 0.18), (0.55, 0.30))
    tongue = _bez((0.10, 0.42), (0.30, 0.20), (0.30, -0.02), (0.10, -0.22))
    return [(True, main), (False, tongue)]


def _glyph_droplet():
    # 抽象水滴（2026-10-12 r3 用户裁决：太写实→一笔开放逗号弧不闭合，变宽笔锋：尖端细/腹部厚/收笔小钩）
    P = []
    P += _bez((0.02, -0.88), (-0.05, -0.55), (-0.30, -0.25), (-0.50, 0.10))
    P += _bez((-0.50, 0.10), (-0.62, 0.38), (-0.35, 0.72), (0.0, 0.78))
    P += _bez((0.0, 0.78), (0.32, 0.72), (0.50, 0.45), (0.47, 0.10))
    P += _bez((0.47, 0.10), (0.45, -0.15), (0.30, -0.30), (0.06, -0.36))
    # (开放, 路径, 端宽, 中宽, 末宽)——路径中点恰落底部圆腹
    return [(False, P, 0.45, 1.25, 0.65)]


def _glyph_sprout():
    # 抽象幼苗（r3）：茎带卷顶+两叶各一主弧一 echo 弧（开放弧替代闭合叶形），全线变宽笔锋
    stem = _bez((0.0, 0.85), (-0.10, 0.52), (-0.08, 0.18), (0.02, -0.08))
    stem += _bez((0.02, -0.08), (0.10, -0.20), (0.06, -0.32), (-0.06, -0.30))
    leaf_l = _bez((-0.03, 0.12), (-0.34, -0.02), (-0.58, -0.10), (-0.72, -0.30))
    leaf_l2 = _bez((-0.03, 0.02), (-0.26, -0.08), (-0.44, -0.14), (-0.55, -0.30))
    leaf_r = _bez((0.02, -0.02), (0.30, -0.14), (0.52, -0.30), (0.64, -0.58))
    leaf_r2 = _bez((0.02, -0.12), (0.24, -0.22), (0.38, -0.34), (0.46, -0.52))
    return [(False, stem, 1.15, None, 0.55),
            (False, leaf_l, 0.95, None, 0.50), (False, leaf_l2, 0.80, None, 0.45),
            (False, leaf_r, 0.95, None, 0.50), (False, leaf_r2, 0.80, None, 0.45)]


def _glyph_sun():
    # 抽象太阳（r4）：开口圆弧（右上留气口，腹粗端细）+7 根不等距弯芒（内粗外尖）
    import math as _m
    face = _arc(0.0, 0.0, 0.42, 340.0, 640.0, 72)  # 300° 开弧，缺口~60° 居右上 310°
    rays = []
    for ang, r0, r1 in [(80, 0.60, 0.95), (128, 0.62, 0.88), (172, 0.60, 1.00), (224, 0.62, 0.92),
                        (268, 0.60, 0.98), (318, 0.64, 0.86), (18, 0.62, 0.96)]:
        a = _m.radians(ang)
        p0 = (r0 * _m.cos(a), r0 * _m.sin(a))
        p3 = (r1 * _m.cos(a), r1 * _m.sin(a))
        px, py = -_m.sin(a), _m.cos(a)  # 法向=微弯（笔意）
        c1 = (p0[0] + (p3[0] - p0[0]) * 0.33 + px * 0.06, p0[1] + (p3[1] - p0[1]) * 0.33 + py * 0.06)
        c2 = (p0[0] + (p3[0] - p0[0]) * 0.66 + px * 0.06, p0[1] + (p3[1] - p0[1]) * 0.66 + py * 0.06)
        rays.append((False, _bez(p0, c1, c2, p3, 10), 0.90, 0.35))
    return [(False, face, 0.55, 1.20, 0.55)] + rays


def _glyph_moon():
    # 抽象月牙（r4）：两道开放弧（角端各留气口），变宽笔锋=中部厚角端细
    A, rA = (-0.10, 0.04), 0.62
    B, rB = (0.26, 0.18), 0.55
    xcs = _circ_xc(A, rA, B, rB)
    if not xcs:
        raise ValueError("moon: 两圆无交点，参数需重选")
    a1 = math.degrees(math.atan2(xcs[0][1] - A[1], xcs[0][0] - A[0]))
    a2 = math.degrees(math.atan2(xcs[1][1] - A[1], xcs[1][0] - A[0]))
    b1 = math.degrees(math.atan2(xcs[0][1] - B[1], xcs[0][0] - B[0]))
    b2 = math.degrees(math.atan2(xcs[1][1] - B[1], xcs[1][0] - B[0]))
    outer = _arc_between(A, rA, a1, a2, B, toward=False)
    inner = _arc_between(B, rB, b2, b1, A, toward=True)
    k = max(2, int(len(outer) * 0.09))  # 两端各截 ~9% = 角口气口
    ko = max(2, int(len(inner) * 0.12))
    return [(False, outer[k:-k], 0.50, 1.15, 0.50),
            (False, inner[ko:-ko], 0.45, 1.00, 0.45)]


def _glyph_star():
    # 抽象星星（r4）：一笔开放五角线（末边留 35% 气口），转角由圆点串自然圆化
    pts = []
    for k in range(10):
        ang = math.radians(-90.0 + k * 36.0)
        rr = 0.88 if k % 2 == 0 else 0.38
        pts.append((rr * math.cos(ang), rr * math.sin(ang)))
    v9, v0 = pts[-1], pts[0]
    pts.append((v9[0] + (v0[0] - v9[0]) * 0.65, v9[1] + (v0[1] - v9[1]) * 0.65))
    return [(False, pts, 0.50, 1.20, 0.55)]


_GLYPHS = {
    "flame": _glyph_flame, "droplet": _glyph_droplet, "sprout": _glyph_sprout,
    "sun": _glyph_sun, "moon": _glyph_moon, "star": _glyph_star,
}


# ---------- 甲骨文字形（2026-10-12 r5 用户裁决：不走具体图像，从甲骨文字形生成+元素色）----------
def _glyph_huo():
    # 甲骨「火」：中锋主焰+两侧升臂+两枚点刻
    mid = _bez((0.0, 0.55), (0.1, 0.15), (-0.06, -0.3), (0.0, -0.72))
    la = _bez((-0.06, 0.52), (-0.4, 0.42), (-0.58, 0.05), (-0.38, -0.4))
    ra = _bez((0.06, 0.52), (0.4, 0.42), (0.58, 0.05), (0.38, -0.4))
    d1 = _bez((-0.2, -0.02), (-0.25, -0.06), (-0.3, -0.1), (-0.34, -0.14))
    d2 = _bez((0.2, -0.02), (0.25, -0.06), (0.3, -0.1), (0.34, -0.14))
    return [(False, mid, 0.9, 0.45), (False, la, 0.85, 0.4), (False, ra, 0.85, 0.4),
            (False, d1, 0.5, 0.3), (False, d2, 0.5, 0.3)]


def _glyph_shui():
    # 甲骨「水」：中流曲线+两侧斜点
    c = _bez((0.12, 0.8), (0.2, 0.25), (-0.14, -0.25), (0.0, -0.8))
    l1 = _bez((-0.5, 0.45), (-0.38, 0.4), (-0.26, 0.35), (-0.16, 0.3))
    l2 = _bez((-0.44, -0.05), (-0.33, -0.1), (-0.22, -0.14), (-0.12, -0.18))
    r1 = _bez((0.48, 0.32), (0.37, 0.28), (0.26, 0.23), (0.16, 0.19))
    r2 = _bez((0.5, -0.18), (0.39, -0.22), (0.28, -0.26), (0.18, -0.3))
    return [(False, c, 0.9, 0.5), (False, l1, 0.55, 0.35), (False, l2, 0.55, 0.35),
            (False, r1, 0.55, 0.35), (False, r2, 0.55, 0.35)]


def _glyph_mu():
    # 甲骨「木」：主干+上枝下根（各两出）
    trunk = _bez((0.0, 0.88), (-0.05, 0.3), (0.05, -0.3), (0.0, -0.82))
    bl = _bez((0.0, 0.08), (-0.25, 0.02), (-0.48, -0.12), (-0.62, -0.45))
    br = _bez((0.0, -0.06), (0.24, -0.12), (0.46, -0.24), (0.6, -0.52))
    rl = _bez((0.0, 0.42), (-0.22, 0.5), (-0.42, 0.58), (-0.56, 0.7))
    rr = _bez((0.0, 0.42), (0.22, 0.5), (0.42, 0.58), (0.56, 0.72))
    return [(False, trunk, 1.0, 0.5), (False, bl, 0.75, 0.35), (False, br, 0.75, 0.35),
            (False, rl, 0.7, 0.35), (False, rr, 0.7, 0.35)]


def _glyph_ri():
    # 甲骨「日」：微歪圆角方框（超椭圆 n=4+手作波扰）+中横点
    pts = []
    for i in range(80):
        t = 2 * math.pi * i / 80
        ct, st = math.cos(t), math.sin(t)
        x = math.copysign(abs(ct) ** 0.5 * 0.42, ct) + 0.012 * math.sin(3 * t)
        y = math.copysign(abs(st) ** 0.5 * 0.62, st) + 0.012 * math.cos(2 * t)
        pts.append((x, y))
    bar = _bez((-0.24, 0.04), (-0.08, 0.01), (0.1, 0.0), (0.26, -0.04))
    return [(True, pts, 0.5, 0.9, 0.5), (False, bar, 0.45, 0.3)]


def _glyph_yue():
    # 甲骨「月」：细弯月（双弧闭合尖角）+内一短竖
    A, rA = (-0.16, 0.0), 0.68
    B, rB = (0.14, 0.02), 0.64
    xcs = _circ_xc(A, rA, B, rB)
    if not xcs:
        raise ValueError("yue: 两圆无交点")
    a1 = math.degrees(math.atan2(xcs[0][1] - A[1], xcs[0][0] - A[0]))
    a2 = math.degrees(math.atan2(xcs[1][1] - A[1], xcs[1][0] - A[0]))
    b1 = math.degrees(math.atan2(xcs[0][1] - B[1], xcs[0][0] - B[0]))
    b2 = math.degrees(math.atan2(xcs[1][1] - B[1], xcs[1][0] - B[0]))
    outer = _arc_between(A, rA, a1, a2, B, toward=False)
    inner = _arc_between(B, rB, b2, b1, A, toward=True)
    tick = _bez((-0.38, -0.2), (-0.35, 0.0), (-0.31, 0.18), (-0.25, 0.34))
    return [(True, outer + inner, 0.5, 0.95, 0.5), (False, tick, 0.5, 0.35)]


def _glyph_xing():
    # 甲骨「星」：生（地平线+茎+对叶）+两枚小方星
    ground = _bez((-0.5, 0.62), (-0.17, 0.6), (0.17, 0.58), (0.5, 0.55))
    stem = _bez((0.02, 0.6), (-0.02, 0.3), (0.02, -0.1), (0.0, -0.32))
    ll = _bez((0.0, -0.22), (-0.18, -0.3), (-0.32, -0.38), (-0.44, -0.6))
    lr = _bez((0.0, -0.22), (0.18, -0.28), (0.32, -0.36), (0.46, -0.56))

    def box(cx, cy, s):
        return [(cx - s, cy - s), (cx + s, cy - s), (cx + s, cy + s), (cx - s, cy + s)]
    return [(False, ground, 0.6, 0.45), (False, stem, 0.9, 0.5),
            (False, ll, 0.6, 0.35), (False, lr, 0.6, 0.35),
            (True, box(-0.14, -0.78, 0.10), 0.45, 0.45),
            (True, box(0.34, -0.7, 0.08), 0.4, 0.4)]


# 黑元素意象族（2026-10-12 r6 用户裁决：弃月牙，用 黑/暗/混乱 等甲骨意象）
def _glyph_hei():
    # 甲骨「黑」：正面人形+面目墨纹（烟熏黥面之象）
    head = []
    for i in range(48):
        t = 2 * math.pi * i / 48
        ct, st = math.cos(t), math.sin(t)
        head.append((math.copysign(abs(ct) ** 0.5 * 0.11, ct),
                     -0.68 + math.copysign(abs(st) ** 0.5 * 0.13, st)))
    mark = _bez((-0.04, -0.72), (0.0, -0.68), (0.0, -0.64), (0.04, -0.6))
    body = _bez((0.0, -0.54), (0.03, -0.1), (-0.03, 0.1), (0.0, 0.3))
    al = _bez((-0.08, -0.48), (-0.3, -0.42), (-0.45, -0.25), (-0.5, -0.02))
    ar = _bez((0.08, -0.48), (0.3, -0.42), (0.45, -0.25), (0.5, -0.02))
    ll = _bez((0.0, 0.28), (-0.12, 0.45), (-0.24, 0.62), (-0.3, 0.8))
    lr = _bez((0.0, 0.28), (0.12, 0.45), (0.24, 0.62), (0.3, 0.8))
    d1 = _bez((-0.22, -0.78), (-0.24, -0.8), (-0.26, -0.82), (-0.28, -0.84))
    d2 = _bez((0.22, -0.78), (0.24, -0.8), (0.26, -0.82), (0.28, -0.84))
    return [(True, head, 0.5, 0.8, 0.5), (False, mark, 0.4, 0.3), (False, body, 0.9, 0.5),
            (False, al, 0.7, 0.4), (False, ar, 0.7, 0.4),
            (False, ll, 0.8, 0.45), (False, lr, 0.8, 0.45),
            (False, d1, 0.4, 0.3), (False, d2, 0.4, 0.3)]


def _glyph_ming():
    # 甲骨「冥」：双手掩日（光被覆灭=昏暗）
    sun = []
    for i in range(40):
        t = 2 * math.pi * i / 40
        ct, st = math.cos(t), math.sin(t)
        sun.append((math.copysign(abs(ct) ** 0.5 * 0.15, ct),
                    -0.1 + math.copysign(abs(st) ** 0.5 * 0.19, st)))
    bar = _bez((-0.08, -0.09), (0.0, -0.1), (0.05, -0.11), (0.09, -0.12))
    hand = []
    for s in (1, -1):
        hand += [_bez((s * -0.6, -0.32), (s * -0.48, -0.3), (s * -0.36, -0.28), (s * -0.22, -0.26)),
                 _bez((s * -0.66, -0.13), (s * -0.52, -0.12), (s * -0.38, -0.12), (s * -0.2, -0.11)),
                 _bez((s * -0.6, 0.06), (s * -0.48, 0.04), (s * -0.36, 0.02), (s * -0.22, -0.02))]
    return [(True, sun, 0.5, 0.85, 0.5), (False, bar, 0.4, 0.3)] + \
           [(False, p, 0.6, 0.35) for p in hand]


def _glyph_luan():
    # 「乱」源字 𤔔 的缠丝：三线交缠+中环松口（纯缠结=乱）
    t1 = _bez((-0.16, 0.6), (-0.3, 0.2), (0.28, -0.15), (0.14, -0.6))
    t2 = _bez((0.16, 0.6), (0.3, 0.2), (-0.28, -0.15), (-0.14, -0.6))
    t3 = _bez((0.0, 0.65), (-0.2, 0.3), (0.2, -0.3), (0.0, -0.65))
    loop = _arc(0.0, 0.0, 0.13, 140.0, 430.0, 40)
    return [(False, t1, 0.8, 0.45), (False, t2, 0.8, 0.45), (False, t3, 0.8, 0.45),
            (False, loop, 0.55, 0.55)]


def _glyph_xuan():
    # 甲骨「玄」：绞丝下垂（幽远之黑）
    toploop = _arc(0.0, -0.6, 0.1, 320.0, 620.0, 32)
    thread = _bez((0.0, -0.5), (0.16, -0.2), (-0.16, 0.15), (0.0, 0.7))
    cl = _arc(-0.15, 0.0, 0.055, 120.0, 420.0, 20)
    cr = _arc(0.15, -0.08, 0.05, 120.0, 420.0, 20)
    return [(False, toploop, 0.6, 0.6), (False, thread, 0.85, 0.5),
            (False, cl, 0.5, 0.5), (False, cr, 0.5, 0.5)]


# ---------- 战斗动作图标（2026-10-15 用户配方：攻击/守卫/发动，银色主体+金色描边+黑丝背景）----------
def _glyph_sword():
    # 「攻击」：直立长剑——双刃微弧收尖+弧形护手+短柄+圆首（笔锋=尖细根厚）
    bl = _bez((0.0, -0.88), (-0.03, -0.50), (-0.09, -0.08), (-0.13, 0.28))
    br = _bez((0.0, -0.88), (0.03, -0.50), (0.09, -0.08), (0.13, 0.28))
    guard = _bez((-0.36, 0.30), (-0.14, 0.37), (0.14, 0.37), (0.36, 0.30))
    grip = _bez((0.0, 0.40), (0.01, 0.50), (-0.01, 0.58), (0.0, 0.64))
    pommel = _arc(0.0, 0.74, 0.075, 0, 360, 28)
    return [(False, bl, 0.40, 0.55, 0.75), (False, br, 0.40, 0.55, 0.75),
            (False, guard, 0.60, 0.90, 0.60),
            (False, grip, 0.62, 0.62), (True, pommel, 0.55, 0.55)]


def _glyph_shield():
    # 「守卫」：鸢盾轮廓（顶边微弓+双侧内收至底尖）+中脊线
    top = _bez((-0.52, -0.60), (-0.20, -0.66), (0.20, -0.66), (0.52, -0.60))
    right = _bez((0.52, -0.60), (0.58, -0.18), (0.40, 0.30), (0.0, 0.78))
    left = _bez((0.0, 0.78), (-0.40, 0.30), (-0.58, -0.18), (-0.52, -0.60))
    spine = _bez((0.0, -0.46), (0.02, -0.10), (-0.02, 0.25), (0.0, 0.56))
    return [(True, top + right + left, 0.95, 0.85, 0.95),
            (False, spine, 0.62, 0.45)]


def _glyph_burst():
    # 「发动」：中心芯点+八芒不等长微弯射线（能量迸发；区别于「日」的大开环——射线直贴芯点出射）
    core = _arc(0.0, 0.0, 0.13, 0, 360, 28)
    rays = []
    for ang, r1 in [(88, 0.90), (42, 0.64), (-4, 0.92), (-48, 0.62),
                    (-92, 0.88), (-136, 0.66), (178, 0.90), (134, 0.62)]:
        a = math.radians(ang)
        p0 = (0.26 * math.cos(a), 0.26 * math.sin(a))
        p3 = (r1 * math.cos(a), r1 * math.sin(a))
        px, py = -math.sin(a), math.cos(a)  # 法向微弯=笔意
        c1 = (p0[0] + (p3[0] - p0[0]) * 0.33 + px * 0.05, p0[1] + (p3[1] - p0[1]) * 0.33 + py * 0.05)
        c2 = (p0[0] + (p3[0] - p0[0]) * 0.66 + px * 0.05, p0[1] + (p3[1] - p0[1]) * 0.66 + py * 0.05)
        rays.append((False, _bez(p0, c1, c2, p3, 10), 0.85, 0.30))
    return [(True, core, 0.75, 0.75)] + rays


_GLYPHS.update({
    "huo": _glyph_huo, "shui": _glyph_shui, "mu": _glyph_mu,
    "ri": _glyph_ri, "yue": _glyph_yue, "xing": _glyph_xing,
    "hei": _glyph_hei, "ming": _glyph_ming, "luan": _glyph_luan, "xuan": _glyph_xuan,
    "sword": _glyph_sword, "shield": _glyph_shield, "burst": _glyph_burst,
})


def draw_template(spec, w, h, tint=None):
    """tint: (bg, face, edge) 三色 [(r,g,b),...]；低 denoise 下出图低频色来自模板，
    风格底色必须画进模板而非指望提示词。缺省灰阶。"""
    shape = spec.get("shape", "rounded_solid")
    corner = spec.get("corner", 0.2)
    bg, face, edge = ((58, 58, 62), (110, 110, 114), (176, 176, 180)) if not tint else \
        (tuple(tint[0]), tuple(tint[1]), tuple(tint[2]))
    inner = tuple(max(0, int(c * 0.55)) for c in face)
    img = Image.new("RGBA", (w, h), bg + (255,))
    d = ImageDraw.Draw(img)
    r = int(min(w, h) * corner)
    m = max(2, int(min(w, h) * 0.02))  # 边距

    if shape == "plain":
        pass
    elif shape == "plain_glow":
        # 图示类 icon3d 专用（2026-10-15）：中心银白亮度质量块→四角黑丝径向衰减——
        # 近黑无特征 init 会被 img2img 低频压成全黑（主体 <2% 亮像素实测），init 必须自带亮度骨架，
        # SD 从亮团雕出主体；edge=光团色/bg=丝底，prompt 决定主体形状。
        glow_c = edge
        grad = Image.new("RGB", (96, 96))
        gp = grad.load()
        cc = 47.5
        for yy in range(96):
            for xx in range(96):
                dist = math.hypot(xx - cc, yy - cc) / 55.0  # 归一半径（四角≈1.23）
                t = max(0.0, 1.0 - dist) ** 1.5             # 偏心集中的线性衰减
                gp[xx, yy] = tuple(int(bg[i] + (glow_c[i] - bg[i]) * (0.9 * t)) for i in range(3))
        img.paste(grad.resize((w, h), Image.BICUBIC), (0, 0))
    elif shape == "badge_glow":
        # 圆形徽章（2026-10-15 用户裁决：背景纯黑+圆形徽章，反插画化）：金环几何画进 init（权威），
        # 盘内银白径向光团=SD 雕主体的亮度骨架，环外纯黑；face=金环色/edge=盘内光团色/bg=纯黑。
        cx, cy = w / 2.0, h / 2.0
        R_ring = min(w, h) * 0.435           # 环中线半径
        ring_w = max(10, int(min(w, h) * 0.05))  # 粗金环带宽
        R_disc = R_ring - ring_w * 0.9       # 盘内径
        grad = Image.new("RGB", (96, 96))
        gp = grad.load()
        cc, rr = 47.5, 46.0
        for yy in range(96):
            for xx in range(96):
                dist = math.hypot(xx - cc, yy - cc) / rr
                t = max(0.0, 1.0 - dist) ** 1.35  # 中心亮→盘缘暗（留黑给徽章内衬）
                gp[xx, yy] = tuple(int(bg[i] + (edge[i] - bg[i]) * (0.78 * t)) for i in range(3))
        mask = Image.new("L", (w, h), 0)
        ImageDraw.Draw(mask).ellipse([cx - R_disc, cy - R_disc, cx + R_disc, cy + R_disc], fill=255)
        img.paste(grad.resize((w, h), Image.BICUBIC), (0, 0), mask)
        # 细内金线 + 粗外金环（双描边家族语言）
        d.ellipse([cx - R_disc * 0.92, cy - R_disc * 0.92, cx + R_disc * 0.92, cy + R_disc * 0.92],
                  outline=face + (255,), width=max(2, ring_w // 10))
        d.ellipse([cx - R_ring, cy - R_ring, cx + R_ring, cy + R_ring],
                  outline=face + (255,), width=ring_w)
    elif shape == "rounded_solid":  # 凸面件：按钮/面板
        d.rounded_rectangle([m, m, w - m, h - m], r, fill=face + (255,),
                            outline=edge + (255,), width=max(2, r // 8))
        d.rounded_rectangle([m + max(2, r // 8), m + max(2, r // 8),
                             w - m - max(2, r // 8), h - m - max(2, r // 8)],
                            max(0, r - r // 6), outline=inner + (255,), width=2)
    elif shape == "rounded_inset":  # 凹面件：槽/输入条/行（暗于凸面但仍带风格色）
        d.rounded_rectangle([m, m, w - m, h - m], r, fill=tuple(max(0, int(c * 0.7)) for c in face) + (255,),
                            outline=edge + (255,), width=3)
        d.rounded_rectangle([m + 4, m + 4, w - m - 4, h - m - 4],
                            max(0, r - 4), outline=tuple(max(0, int(c * 0.45)) for c in face) + (255,), width=2)
    elif shape == "card_face":  # 卡面版式：厚边框+顶名牌带+上中部立绘窗+底部凹文本板
        t = max(6, int(min(w, h) * 0.09))
        d.rounded_rectangle([m, m, w - m, h - m], r, fill=face + (255,),
                            outline=edge + (255,), width=max(2, t // 3))
        d.rounded_rectangle([m + t, m + t + int(h * 0.10), w - m - t, m + t + int(h * 0.62)],
                            max(0, r - t), fill=tuple(min(255, int(c * 1.25)) for c in face) + (255,),
                            outline=inner + (255,), width=2)
        d.rounded_rectangle([m + t, m + t + int(h * 0.66), w - m - t, h - m - t],
                            max(0, r - t), fill=tuple(max(0, int(c * 0.55)) for c in face) + (255,),
                            outline=inner + (255,), width=2)
    elif shape == "frame_hollow":  # 空心边框
        t = max(6, int(min(w, h) * 0.08))
        d.rounded_rectangle([m, m, w - m, h - m], r, fill=tuple(max(0, int(c * 0.85)) for c in bg) + (255,))
        d.rounded_rectangle([m + t, m + t, w - m - t, h - m - t],
                            max(0, r - t), fill=tuple(max(0, int(c * 0.5)) for c in bg) + (255,),
                            outline=edge + (255,), width=3)
        d.rounded_rectangle([m, m, w - m, h - m], r, outline=edge + (255,), width=4)
    elif shape == "ring_hollow":  # 圆环空心（结界形插画框）
        t = max(6, int(min(w, h) * 0.08))
        cx, cy, R = w // 2, h // 2, min(w, h) // 2 - m
        d.ellipse([cx - R, cy - R, cx + R, cy + R], fill=face + (255,),
                  outline=edge + (255,), width=max(3, t // 6))
        d.ellipse([cx - R + t, cy - R + t, cx + R - t, cy + R - t],
                  fill=tuple(max(0, int(c * 0.5)) for c in bg) + (255,),
                  outline=edge + (255,), width=3)
    elif shape == "tri_hollow":  # 三角空心（法术形插画框；v2 锐角版：内外多边形+鎏金双描边；flip_v=倒三角）
        t = max(6, int(min(w, h) * 0.08))
        if spec.get("flip_v"):
            outer = [(int(m + w * 0.06), int(m + h * 0.02)), (int(w - m - w * 0.06), int(m + h * 0.02)),
                     (int(w * 0.5), int(h - m))]
        else:
            outer = [(int(w * 0.5), int(m + h * 0.02)), (int(m + w * 0.06), int(h - m)),
                     (int(w - m - w * 0.06), int(h - m))]
        cx = sum(p[0] for p in outer) / 3.0
        cy = sum(p[1] for p in outer) / 3.0
        k = max(0.3, 1.0 - 2.6 * t / min(w, h))
        inner = [(int(cx + (px - cx) * k), int(cy + (py - cy) * k)) for px, py in outer]
        d.polygon(outer, fill=face + (255,))
        d.polygon(inner, fill=tuple(max(0, int(c * 0.5)) for c in bg) + (255,))
        d.line(outer + [outer[0]], fill=edge + (255,), width=max(2, t // 8))
        d.line(inner + [inner[0]], fill=edge + (255,), width=max(2, t // 8))
    elif shape in ("ring_dblstroke", "rect_dblstroke", "tri_dblstroke"):
        # 双描边镂空框（2026-10-11 深夜用户定版）：内描边细/外描边粗=金属，两描边之间=等宽星空带（星点种子），外描边朝外留溢出区给拉丝浮雕
        # 等宽=平行偏移轮廓（圆=同心、方=内缩矩形、三角=_offset_poly），非同态缩放
        tot = max(20, int(min(w, h) * 0.10))
        # v4：星带份额 0.38→0.50（19→26px@512）——窄带里 SD1.5 铺不出可辨识星场（0.70 档还会把细星点重噪声掉）
        w_out, w_gap, w_in = int(tot * 0.40), int(tot * 0.50), max(2, int(tot * 0.12))
        spill = max(6, int(min(w, h) * 0.03))  # 外描边朝外衍生（拉丝/浮雕）的预留区
        m2 = m + spill
        cx, cy = w / 2.0, h / 2.0

        if shape == "ring_dblstroke":
            R = min(w, h) / 2 - m - spill

            def fig(dd):
                r = R - dd
                return ("ellipse", (cx - r, cy - r, cx + r, cy + r))
        elif shape == "rect_dblstroke":

            def fig(dd):
                return ("poly", [(m2 + dd, m2 + dd), (w - m2 - dd, m2 + dd),
                                 (w - m2 - dd, h - m2 - dd), (m2 + dd, h - m2 - dd)])
        else:
            # 正三角（v6：旧 base=高瘦等腰 顶宽400.6/高481.8，正三角应为347——用户"不是正三角全军覆没"）
            # 外接圆构造与石板 9 触点同口径：apex=底点、两顶点 30°/150°，三边恒等；bbox 居中
            # 路径=理想轮廓向外偏移 w_out/2（_offset_poly 负值=外扩），补偿描边内缩让外缘精确落理想边
            Rc = min((w / 2.0 - m2 - w_out / 2.0) / 0.8660254, (h / 2.0 - m - w_out / 2.0) / 0.75)
            if spec.get("flip_v"):
                ccy = h / 2.0 - Rc * 0.25
                ideal = [(w / 2.0 - Rc * 0.8660254, ccy - Rc * 0.5),
                         (w / 2.0 + Rc * 0.8660254, ccy - Rc * 0.5),
                         (w / 2.0, ccy + Rc)]
            else:
                ccy = h / 2.0 + Rc * 0.25
                ideal = [(w / 2.0 - Rc * 0.8660254, ccy + Rc * 0.5),
                         (w / 2.0 + Rc * 0.8660254, ccy + Rc * 0.5),
                         (w / 2.0, ccy - Rc)]
            base = _offset_poly(ideal, -(w_out / 2.0))

            def fig(dd):
                return ("poly", _offset_poly(base, dd))

        def draw_fig(dd, fill=None, outline=None, width=1):
            kind, geom = fig(dd)
            if kind == "ellipse":
                d.ellipse(geom, fill=fill, outline=outline, width=width)
            else:
                d.polygon(geom, fill=fill)
                if outline:
                    d.line(geom + [geom[0]], fill=outline, width=width, joint="curve")

        gapfill = tuple(min(255, int(c * 2.6)) for c in bg)  # 星空带底=深空色（v5 修层级：带区真正显示带底而非画布底）
        center = tuple(max(0, int(c * 0.55)) for c in bg)    # 中心抠图区=最深
        draw_fig(w_out, fill=gapfill)          # 铺到带区（含内描边+中心底）
        draw_fig(w_out + w_gap, fill=center)   # 中心压深 → 可见带底=带区 [w_out, w_out+w_gap]
        # 描边语义按形状统一到设计区间（圆=[内向描边]路径在区间远端，多边形=[跨中描边]路径在区间中点），否则多边形星带宽 26≠19
        if shape == "ring_dblstroke":
            outer_off, inner_off = 0, w_out + w_gap
        else:
            outer_off, inner_off = w_out / 2.0, w_out + w_gap + w_in / 2.0
        draw_fig(inner_off, outline=edge, width=w_in)       # 内描边=细金属
        draw_fig(outer_off, outline=edge, width=w_out)      # 外描边=粗金属
        # v5 星场直接画进 init（SD 在 26px 窄带里铺不出星场，实测带亮 0.2-6.9%；init 才是权威）：
        # ~600 颗三档星（暗白 2px/金 3px/亮白 4px）+每 40 颗一颗十字亮星
        hi = fig(w_out)[1]
        lo = fig(w_out + w_gap)[1]
        if shape == "ring_dblstroke":
            x0, y0, x1, y1 = hi
        else:
            x0, y0 = min(p[0] for p in hi), min(p[1] for p in hi)
            x1, y1 = max(p[0] for p in hi), max(p[1] for p in hi)
        rng = np.random.default_rng(11)
        placed, tries = 0, 0
        want = 600
        while placed < want and tries < 40000:
            tries += 1
            x = float(rng.uniform(x0, x1))
            y = float(rng.uniform(y0, y1))
            if shape == "ring_dblstroke":
                r = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
                ok = (R - w_out - w_gap) <= r <= (R - w_out)
            else:
                ok = _in_poly(x, y, hi) and not _in_poly(x, y, lo)
            if ok:
                placed += 1
                k = placed % 40
                if k == 0:      # 十字亮星
                    col = (255, 255, 248)
                    d.line([(x - 4, y), (x + 4, y)], fill=col + (255,), width=1)
                    d.line([(x, y - 4), (x, y + 4)], fill=col + (255,), width=1)
                elif placed % 3 == 0:
                    col, e2 = edge, 3
                    d.ellipse([x, y, x + e2, y + e2], fill=col + (255,))
                elif placed % 3 == 1:
                    col, e2 = (205, 205, 195), 2
                    d.ellipse([x, y, x + e2, y + e2], fill=col + (255,))
                else:
                    col, e2 = (255, 252, 245), 4
                    d.ellipse([x, y, x + e2, y + e2], fill=col + (255,))
    elif shape == "gem_blob":  # 居中宝珠（元素徽记等：tint_edge 是唯一区分度，必须画进模板）
        R = int(min(w, h) * 0.34)
        cx, cy = w // 2, h // 2
        d.ellipse([cx - R, cy - R, cx + R, cy + R], fill=face + (255,),
                  outline=edge + (255,), width=max(3, int(min(w, h) * 0.03)))
        hr = int(R * 0.45)
        d.ellipse([cx - hr, cy - hr - int(R * 0.3), cx + hr, cy - hr + int(R * 0.3)],
                  fill=tuple(min(255, int(c * 1.5) + 30) for c in face) + (255,))
    elif shape == "plaque_paper":  # 纸面文字板（2026-10-11 v3 用户裁决：修饰只走边缘）：边带=face+金描边、内芯=bg（覆盖为羊皮纸色）
        t = max(8, int(min(w, h) * 0.14))
        d.rounded_rectangle([m, m, w - m, h - m], r, fill=face + (255,),
                            outline=edge + (255,), width=max(2, r // 6))
        d.rounded_rectangle([m + t, m + t, w - m - t, h - m - t], max(0, r - t),
                            fill=bg + (255,),
                            outline=tuple(max(0, int(c * 0.55)) for c in face) + (255,), width=2)
    elif shape == "plaque_paper_vine":  # 纸面文字板·金丝藤蔓版（2026-10-11 v6 用户裁决：鎏金=树枝藤蔓金丝勾勒，非金属板）：通体羊皮纸，边缘=外金线+内细金线勾出藤蔓区，角/边中点金点=生长锚；藤蔓有机形态由提示词沿线生成
        t = max(8, int(min(w, h) * 0.14))
        d.rounded_rectangle([m, m, w - m, h - m], r, fill=bg + (255,),
                            outline=edge + (255,), width=max(3, r // 6))
        d.rounded_rectangle([m + t, m + t, w - m - t, h - m - t], max(0, r - t),
                            outline=tuple(max(0, int(c * 0.75)) for c in edge) + (255,), width=2)
        dot = max(3, int(min(w, h) * 0.018))
        for cx, cy in [(m + r * 0.5, m + r * 0.5), (w - m - r * 0.5, m + r * 0.5),
                       (m + r * 0.5, h - m - r * 0.5), (w - m - r * 0.5, h - m - r * 0.5),
                       (w // 2, m), (w // 2, h - m), (m, h // 2), (w - m, h // 2)]:
            d.ellipse([cx - dot, cy - dot, cx + dot, cy + dot], fill=edge + (255,))
    elif shape == "sigil_grooves":  # 发动区石板（2026-10-11）：大圆→内接方→内接倒三角三环凿槽，槽留空（元素池后期引擎灌入）
        gw = max(4, int(min(w, h) * 0.045))
        dark = tuple(max(0, int(c * 0.3)) for c in face)
        lit = tuple(min(255, int(c * 1.45) + 30) for c in face)
        d.rounded_rectangle([m, m, w - m, h - m], r, fill=face + (255,),
                            outline=edge + (255,), width=max(2, r // 4))
        cx, cy = w / 2.0, h / 2.0
        R = min(w, h) / 2 - m - max(6, int(min(w, h) * 0.1))
        a = R / 2 ** 0.5  # 内接方半边长（四角触圆）
        d.ellipse([cx - R, cy - R, cx + R, cy + R], outline=dark + (255,), width=gw)
        d.ellipse([cx - R, cy - R + gw, cx + R, cy + R + gw], outline=lit + (255,), width=2)
        d.rectangle([cx - a, cy - a, cx + a, cy + a], outline=dark + (255,), width=gw)
        d.rectangle([cx - a, cy - a + gw, cx + a, cy + a + gw], outline=lit + (255,), width=2)
        # 倒三角顶点触圆（用户 2026-10-11 裁决：两多边形顶点均须准确接圆，非内接于方）：apex=圆底点、两顶点 30°/150°；与方互锁交叉
        tri = [(cx - R * 0.8660254, cy - R * 0.5), (cx + R * 0.8660254, cy - R * 0.5),
               (cx, cy + R), (cx - R * 0.8660254, cy - R * 0.5)]
        d.line(tri, fill=dark + (255,), width=gw, joint="curve")
        d.line([(px, py + gw) for px, py in tri], fill=lit + (255,), width=2, joint="curve")
    elif shape == "elem_glyph":  # 六元素线条图案（2026-10-12）：face=晕光环色/edge=核心线条色/bg=暗宇宙底；粗晕打底+细芯双描边
        glyph = spec.get("glyph", "star")
        S = min(w, h) * 0.36
        cx, cy = w / 2.0, h / 2.0
        paths = _GLYPHS[glyph]()
        sw = max(6, int(min(w, h) * 0.045))
        # 路径归一：(闭合, 像素点列, 逐点宽度系数)；r3 起 glyph 可带变宽笔锋元组
        # (closed, pts) 均匀 1.0 / (closed, pts, wa, wb) 线性 / (closed, pts, wa, wmid, wb) 两段（wmid=None=线性）
        norm = []
        for tup in paths:
            closed, pts = tup[0], tup[1]
            n = len(pts) + (1 if closed else 0)
            if len(tup) < 3:
                wm = [1.0] * n
            elif len(tup) == 3:
                wm = [tup[2]] * n
            elif len(tup) == 4:
                wa, wb = tup[2], tup[3]
                wm = [wa + (wb - wa) * i / max(1, n - 1) for i in range(n)]
            else:
                wa, wmid, wb = tup[2], tup[3], tup[4]
                if wmid is None:
                    wmid = wa + (wb - wa) * 0.5
                h = (n - 1) / 2.0
                wm = [wa + (wmid - wa) * min(i / h, 1.0) if i <= h
                      else wmid + (wb - wmid) * (i - h) / max(1.0, h) for i in range(n)]
            P = [(cx + x * S, cy + y * S) for x, y in pts]
            if closed:
                P.append(P[0])
            norm.append((P, wm))
        for mul, col in ((2.0, face), (1.0, edge)):  # 先晕环（2 倍宽）后芯线，变宽同步缩放
            for P, wm in norm:
                for i in range(len(P) - 1):
                    d.line([P[i], P[i + 1]], fill=col + (255,),
                           width=max(2, int(round(sw * mul * wm[i]))))
                for i, (px, py) in enumerate(P):  # 圆点串=平滑变宽管+圆头收笔
                    r = max(1.0, sw * mul * wm[i] / 2.0)
                    d.ellipse([px - r, py - r, px + r, py + r], fill=col + (255,))
    elif shape == "card_back":  # 统一卡背（2026-10-12）：金饰边带+深暗内场+中心符号槽（细线圆角方+四角金点=后期符号对位标）
        t = max(8, int(min(w, h) * 0.075))
        d.rounded_rectangle([m, m, w - m, h - m], r, fill=face + (255,),
                            outline=edge + (255,), width=max(3, int(t * 0.35)))
        d.rounded_rectangle([m + t, m + t, w - m - t, h - m - t], max(0, r - t // 2),
                            fill=tuple(max(0, int(c * 0.5)) for c in bg) + (255,),
                            outline=tuple(max(0, int(c * 0.55)) for c in face) + (255,), width=2)
        s = int(w * 0.44)  # 中心符号槽 ≈44% 宽（地牌图标方形构图）
        cx, cy = w // 2, h // 2
        rs = int(s * 0.12)
        d.rounded_rectangle([cx - s // 2, cy - s // 2, cx + s // 2, cy + s // 2], rs,
                            fill=bg + (255,),
                            outline=tuple(max(0, int(c * 0.7)) for c in face) + (255,), width=2)
        tk = max(2, int(w * 0.012))
        for px, py in [(cx - s // 2, cy - s // 2), (cx + s // 2, cy - s // 2),
                       (cx - s // 2, cy + s // 2), (cx + s // 2, cy + s // 2)]:
            d.line([(px - tk * 2, py), (px + tk * 2, py)], fill=edge + (255,), width=tk)
            d.line([(px, py - tk * 2), (px, py + tk * 2)], fill=edge + (255,), width=tk)
    else:
        raise ValueError("unknown shape: " + shape)

    if shape != "plain":
        img = img.filter(ImageFilter.GaussianBlur(1.0))
    return add_noise(img)


def draw_flat_strip(tint):
    """界限规则：短边<50 的文字条不生成，程序化素面。
    规范化单图 128x32：纯色=背景色提亮一档；左右两端 20px 向背景色渐变。
    Unity 侧一张图左右九切片共用：过渡带宽固定不变，中段纯色横向伸缩。"""
    bg = tuple(tint[0]) if tint else (42, 45, 50)
    fill = tuple(min(255, int(c * 1.3) + 10) for c in bg)
    w, h, margin = 128, 32, 20
    img = Image.new("RGBA", (w, h), fill + (255,))
    px = img.load()
    for x in range(w):
        t = min(x, w - 1 - x)
        if t < margin:
            f = t / max(1, margin)
            c = tuple(int(bg[i] + (fill[i] - bg[i]) * f) for i in range(3))
            for y in range(h):
                px[x, y] = c + (255,)
    return add_noise(img, 2)


# ---------- API ----------
def b64png(img):
    buf = io.BytesIO()
    img.convert("RGB").save(buf, "PNG")
    return base64.b64encode(buf.getvalue()).decode()


def call_img2img(server, payload, timeout=900):
    """gfx1030+SDXL fp32(--no-half) 单张约 3 分钟：超时须远大于 180s；
    兼容 gfx1030 半精度时代偶发 Unet NaN 与 webui 短暂重启：0s/3s/30s 三级重试。"""
    last = None
    for delay in (0, 3, 30):
        if delay:
            time.sleep(delay)
        try:
            # 本地服务强制绕过系统代理（Clash 7890 会截胡 127.0.0.1 请求导致读超时）
            r = requests.post(server.rstrip("/") + "/sdapi/v1/img2img", json=payload,
                              timeout=timeout, proxies={"http": None, "https": None})
            r.raise_for_status()
            return r.json()
        except Exception as e:
            last = e
    raise last


# ---------- 主流程 ----------
def build_items(man, styles, specs):
    style = styles[man["style"]]
    out = []
    for it in man["items"]:
        # item 级 style 覆盖：一个 manifest 可横评多 LoRA（2026-10-11）
        st_name = it.get("style", man["style"])
        st = styles[st_name]
        spec = dict(specs[it["type"]])
        # 行级 spec 覆盖：exact_base=exact 模式长边基准（512 半幅快出候选档）；glyph=elem_glyph 的图案名
        for k in ("exact", "exact_base", "corner", "flip_v", "pad", "denoise", "base_px", "glyph"):
            if k in it:
                spec[k] = it[k]
        gw, gh = gen_size(it["w"], it["h"], spec)
        denoise = it.get("denoise", spec.get("denoise", 0.66))
        if st.get("denoise_base"):
            denoise += st["denoise_base"]
        # item 级 tint 覆盖：六色元素/逐件换点缀色（缺省回落风格三色）
        tint = None
        if st.get("tint_face") or any(k in it for k in ("tint_bg", "tint_face", "tint_edge")):
            tint = (it.get("tint_bg", st.get("tint_bg")),
                    it.get("tint_face", st.get("tint_face")),
                    it.get("tint_edge", st.get("tint_edge")))
        out.append({
            "id": it["id"], "type": it["type"], "note": it.get("note", ""),
            "unity_size": (it["w"], it["h"]), "gen_size": (gw, gh),
            "spec": spec, "denoise": min(0.95, denoise),
            "prompt": st["prefix"] + ", " + spec["prompt"] + ", " + it.get("prompt_addon", ""),
            "negative_addon": it.get("negative_addon", ""),
            "seed": it.get("seed", man.get("seed", -1)),
            "variants": it.get("variants", man.get("variants", 3)),
            "style_name": st_name, "style": st, "tint": tint,
            # 界限规则默认短边<50 走素面条；图标类小件用 "flat": false 强制 SD 出图
            "flat": bool(it.get("flat", min(it["w"], it["h"]) < 50)),
        })
    return style, out


def write_index(path, man, style, results):
    rows = []
    for res in results:
        imgs = "".join(
            f'<figure><img src="{p}"><figcaption>#{i + 1} seed={seed}</figcaption></figure>'
            for i, (p, seed) in enumerate(zip(res["files"], res["seeds"])))
        rows.append(
            f'<section><h2>{res["id"]} <small>{res.get("style", "")} · {res["type"]} · Unity {res["unity"][0]}x{res["unity"][1]}'
            f' · gen {res["gen"][0]}x{res["gen"][1]} · dn {res["denoise"]}</small></h2>'
            f'<p class="note">{res["note"]}</p><div class="grid">{imgs}</div></section>')
    html = (
        "<!DOCTYPE html><html><head><meta charset='utf-8'><title>" + man["task"] + "</title><style>"
        "body{background:#181818;color:#ddd;font-family:sans-serif;margin:24px}"
        "h2 small{color:#888;font-weight:normal;font-size:14px;margin-left:12px}"
        ".note{color:#aaa;font-size:13px}section{margin:32px 0;border-bottom:1px solid #333;padding-bottom:16px}"
        ".grid{display:flex;flex-wrap:wrap;gap:12px}"
        "figure{margin:0;text-align:center}img{max-width:340px;max-height:240px;border:1px solid #444;background:#000}"
        "figcaption{color:#777;font-size:12px}</style></head><body>"
        f"<h1>{man['task']} · style={man['style']} · {time.strftime('%Y-%m-%d %H:%M')}</h1>"
        + "".join(rows) + "</body></html>")
    with io.open(path, "w", encoding="utf-8") as fh:
        fh.write(html)


def write_snapshot(path, manifest_name, man, style, items, scan, server):
    """当轮参数全量快照：换风格实验后每个 task 目录自带可复现配置。"""
    snap = {
        "time": time.strftime("%Y-%m-%d %H:%M:%S"),
        "manifest": manifest_name,
        "style_name": man["style"],
        "style": style,
        "item_styles": {it["id"]: it["style_name"] for it in items},
        "item_tints": {it["id"]: it["tint"] for it in items if it["tint"]},
        "specs_used": {it["type"]: it["spec"] for it in items},
        "scan": scan,
        "server": server,
    }
    with io.open(path, "w", encoding="utf-8") as fh:
        json.dump(snap, fh, ensure_ascii=False, indent=2)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("manifest")
    ap.add_argument("--only", default=None)
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--scan", default=None, help="denoise 扫描, 逗号分隔, 覆盖每项 denoise")
    args = ap.parse_args()

    man = load_json(args.manifest)
    styles = load_json(os.path.join(HERE, "styles.json"))
    specs = load_json(os.path.join(HERE, "component_specs.json"))
    server = man.get("server", "http://127.0.0.1:7860")
    out_dir = os.path.join(HERE, "output", man["task"])
    os.makedirs(out_dir, exist_ok=True)

    style, items = build_items(man, styles, specs)
    scan = [float(x) for x in args.scan.split(",")] if args.scan else None

    results, n_total, n_done = [], 0, 0
    for it in items:
        if args.only and args.only not in it["id"]:
            continue
        st, tint, flat = it["style"], it["tint"], it["flat"]
        flat_name = "flat-strip_v1.png"
        if flat:
            draw_flat_strip(tint).save(os.path.join(out_dir, flat_name))
        tmpl = draw_template(it["spec"], *it["gen_size"], tint=tint)
        tmpl_path = os.path.join(out_dir, it["id"] + "__template.png")
        tmpl.save(tmpl_path)
        dn_list = scan if scan else [it["denoise"]]
        files, seeds, labels = [], [], []
        for k, dn in enumerate(dn_list):
            for v in range(it["variants"]):
                tag = f"{it['id']}_dn{dn:.2f}_v{v + 1}" if scan else f"{it['id']}_v{v + 1}"
                png = os.path.join(out_dir, tag + ".png")
                labels.append(tag)
                n_total += 1
                if os.path.exists(png) and not args.force:
                    files.append(os.path.relpath(png, out_dir).replace("\\", "/"))
                    seeds.append("?")
                    n_done += 1
                    continue
                if args.dry_run:
                    print(f"[dry] {tag} gen={it['gen_size']} dn={dn}" + (" flat" if flat else ""))
                    continue
                if flat:
                    files.append(flat_name)
                    seeds.append("flat")
                    n_done += 1
                    print(f"[ok] {tag} (flat-strip 共享)")
                    continue
                payload = {
                    "init_images": [b64png(tmpl)],
                    "prompt": it["prompt"],
                    "negative_prompt": st["negative"] + (
                        ", " + it["negative_addon"] if it.get("negative_addon") else ""),
                    "denoising_strength": dn, "steps": st["steps"],
                    "cfg_scale": st["cfg"], "sampler_name": st["sampler"],
                    "width": it["gen_size"][0], "height": it["gen_size"][1],
                    # 逐项 seed：仅 v1 用钉死 seed（构图基因延续），其余变体随机
                    "seed": (it["seed"] if (it.get("seed", -1) != -1 and v == 0) else -1),
                    "batch_size": 1, "n_iter": 1,
                    "save_images": False,
                }
                if st.get("checkpoint"):
                    payload["override_settings"] = {"sd_model_checkpoint": st["checkpoint"]}
                if st.get("scheduler"):  # 新版 webui 采样器/调度器拆分（如 DPM++ 2M + karras）
                    payload["scheduler"] = st["scheduler"]
                t0 = time.time()
                try:
                    resp = call_img2img(server, payload)
                except Exception as e:
                    print(f"[ERR] {tag}: {e}", file=sys.stderr)
                    continue
                with open(png, "wb") as fh:
                    fh.write(base64.b64decode(resp["images"][0]))
                files.append(os.path.relpath(png, out_dir).replace("\\", "/"))
                seeds.append(resp.get("info", "") and json.loads(resp["info"]).get("seed", "?") or "?")
                n_done += 1
                print(f"[ok] {tag} ({time.time() - t0:.1f}s)")
        results.append({**it, "files": files, "seeds": seeds, "labels": labels})

    if not args.dry_run:
        write_index(os.path.join(out_dir, "index.html"), man, style, [
            {"id": r["id"], "type": r["type"], "note": r["note"], "style": r["style_name"],
             "unity": r["unity_size"], "gen": r["gen_size"], "denoise": r["denoise"],
             "files": r["files"], "seeds": r["seeds"]} for r in results])
        write_snapshot(os.path.join(out_dir, "style_snapshot.json"), os.path.basename(args.manifest),
                       man, style, items, scan, server)
        print(f"\ndone {n_done}/{n_total} → {out_dir}/index.html")


if __name__ == "__main__":
    main()
