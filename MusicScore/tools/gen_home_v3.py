# -*- coding: utf-8 -*-
"""一次性生成 HOME_Lazy v3.0 曲谱（按用户的响度标尺与三层结构）。

用法: python musicscore_gen_home_v3.py <Tide 工程根>
产物 MusicScore/score/HOME_Lazy.json 是静态真源，本脚本只是它的来历。

用户定义(2026-11-23)：
  - 响度标尺 0..1，1 = 后续战斗段最响处；主界面这段在 0..0.4。
  - 两组不同音高的 FLEX 各是一层起伏，每层最高 0.2，此起彼伏，只在中段叠满到 0.4。
  - 音调最低的那组 FLEX 延绵不断，当底。
  - 最亮的那组和钢琴撑起最响的部分。
  - 整体是一次大起伏(正弦式)：开头最轻 -> 第 16 小节最响 -> 结尾最轻。

构造：
  E(t) = sin(pi t / 32)                      大包络，峰在第 16 小节
  u(t) = (1 - cos(2 pi t / 8)) / 2            8 小节一次的小涌浪
  a(t) = 对齐度：12..20 小节为 1，向两侧 4 小节线性降到 0
  管风琴层力度 = 0.2 * E * (a + (1-a) * (0.5 + 0.5 u))
  弦乐层力度 = 0.2 * E * (a + (1-a) * (0.5 + 0.5 u(t-4)))     与管风琴反相
  两层之和：两头 = 0.30 E (一层高时另一层低)，中段 = 0.40 E (叠满)。
"""
import io
import json
import math
import os
import sys

BARS = 32
PEAK = 0.2          # 每层最高
MIN_VEL = 0.02


def E(t):
    return math.sin(math.pi * t / BARS)


def u(t):
    return (1.0 - math.cos(2.0 * math.pi * t / 8.0)) / 2.0


def a(t):
    if 12.0 <= t <= 20.0:
        return 1.0
    if 8.0 < t < 12.0:
        return (t - 8.0) / 4.0
    if 20.0 < t < 24.0:
        return (24.0 - t) / 4.0
    return 0.0


def r3(x):
    return round(x, 3)


def layer_vel(start, length, phase):
    tc = start + length / 2.0
    al = a(tc)
    # 小涌浪幅度取 0.5：够听出一高一低，换音时的力度台阶又不会太陡
    return r3(max(MIN_VEL, PEAK * E(tc) * (al + (1.0 - al) * (0.5 + 0.5 * u(tc - phase)))))


def piano_vel(base, start, length):
    tc = start + length / 2.0
    return r3(min(0.42, (0.22 + 0.18 * E(tc)) * base / 0.42))


def piano():
    # (音高, 起始小节, 时值小节, 乐句里的相对力度) —— 乐句素材沿用用户认可的「潮汐动机」与双声部高潮句
    phrase = [
        (69, 0.62, 2.2, 0.38), (76, 2.56, 1.6, 0.34),
        (76, 4.52, 0.52, 0.42), (74, 5.03, 0.27, 0.30), (72, 5.31, 2.4, 0.40),
        (76, 8.54, 0.52, 0.44), (72, 9.05, 0.27, 0.32), (69, 9.33, 2.0, 0.44),
        (74, 10.56, 0.52, 0.42), (76, 11.07, 0.27, 0.30), (74, 11.35, 1.3, 0.44),
        # 高潮(峰顶在第 16 小节)：两遍「高音长音 + 中音回应」
        (83, 12.5, 0.52, 0.50), (81, 13.01, 0.27, 0.38), (79, 13.29, 1.7, 0.48),
        (69, 14.56, 0.52, 0.40), (71, 15.07, 0.27, 0.30), (72, 15.35, 1.7, 0.42),
        (76, 16.5, 0.52, 0.48), (74, 17.01, 0.27, 0.36), (72, 17.29, 1.7, 0.46),
        (69, 18.56, 0.52, 0.40), (71, 19.07, 0.27, 0.30), (72, 19.35, 1.9, 0.42),
        (76, 22.6, 1.2, 0.34),
        (72, 24.5, 0.52, 0.40), (71, 25.01, 0.27, 0.30), (69, 25.29, 2.2, 0.40),
        (69, 28.62, 2.2, 0.36), (71, 31.0, 0.6, 0.24),
    ]
    return [[p, r3(s), r3(l), piano_vel(b, s, l)] for p, s, l, b in phrase]


def organ():
    # 层 1：偶数小节起音，每 2 小节换一个和弦音，相邻音重叠 0.3 小节所以不断开
    line = [(0, 57), (2, 64), (4, 60), (6, 57), (8, 53), (10, 60), (12, 62), (14, 57),
            (16, 64), (18, 57), (20, 60), (22, 57), (24, 59), (26, 62), (28, 64), (30, 57)]
    out = []
    for s, p in line:
        length = 2.0 if s == 30 else 2.3
        out.append([p, s, length, layer_vel(s, length, 0.0)])
    return out


def strings():
    # 层 2：奇数小节起音(与管风琴错开一格)，音区更高更亮，峰顶附近冲到 A5
    line = [(1, 72), (3, 71), (5, 69), (7, 74), (9, 76), (11, 72), (13, 74), (15, 79),
            (17, 81), (19, 76), (21, 79), (23, 71), (25, 74), (27, 71), (29, 72), (31, 76)]
    out = []
    for s, p in line:
        if s == 31:
            length = 1.0
        elif s == 9:
            s, length = 9.12, 1.88   # 晚一点进、略短：避免和钢琴同一个音同时响
        elif s == 23:
            length = 2.0             # 略短一点，避免和钢琴同一个音同时响
        else:
            length = 2.3
        out.append([p, s, length, layer_vel(s, length, 4.0)])
    return out


def pad():
    # 底：延绵不断。低音 A 是横跨整段的一个长音；上方每 4 小节换两个和弦音，首尾相接不留缝
    chords = [(0, [57, 64]), (4, [53, 60]), (8, [53, 60]), (12, [57, 62]),
              (16, [57, 64]), (20, [53, 60]), (24, [55, 62]), (28, [57, 64])]
    out = [[45, 0, float(BARS), 0.08]]
    for s, ch in chords:
        out.extend([p, s, 4.0, 0.08] for p in ch)
    return out


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "."
    tracks = [
        {"slot": "piano", "instrument": "Compressed Piano (通道 1)",
         "role": "主角，跟着同一条大起伏走：开头结尾约 0.2，峰顶附近约 0.4",
         "notes": piano()},
        {"slot": "organ", "instrument": "Church organ (通道 4)",
         "role": "起伏层 1(中低音区)：偶数小节起音，每 2 小节换一个和弦音，力度 = 0.2·E·(对齐度 + 错落的小涌浪)",
         "notes": organ()},
        {"slot": "strings", "instrument": "Slow PWM Strings (通道 5)",
         "role": "起伏层 2(高音区，最亮)：奇数小节起音与层 1 错开，小涌浪与层 1 反相；中段两层对齐叠满 0.4",
         "notes": strings()},
        {"slot": "pad", "instrument": "PAD Blicloud (通道 3)",
         "role": "底(音调最低的一组)：延绵不断，低音 A 是横跨整段的一个长音，力度恒定 0.08",
         "notes": pad()},
        {"slot": "bass", "instrument": "(这段不用 BooBass，低音由 pad 的长音承担)",
         "notes": []},
    ]
    doc = {
        "schema": "tide-musicscore/1",
        "pattern": "HOME_Lazy",
        "name_cn": "主界面",
        "mood": "慵懒",
        "bpm": 130,
        "key": "A minor",
        "bars": BARS,
        "status": "current",
        "note": "notes 元素 = [MIDI音高, 起始(小节), 时值(小节), 力度0..1]。slot 是乐器槽位，重放时按 README 的通道映射换算成通道号。",
        "direction": "《星际穿越》去掉急促滴答声后的氛围——持续和声、稀疏钢琴、大空间感。只借手法，和声进行与旋律动机全部原创。",
        "revision": "v3.0 (2026-11-23)：按用户给的响度标尺重做。用户否了 v2.3(「先停，全部断开也不对」)，并定下：最低的一组 FLEX 连续不断；两组不同音高的 FLEX 像鱼鳞片此起彼伏；最亮的一组和钢琴撑最响处；整体是一次正弦式大起伏。",
        "loudness_scale": "0..1，1 = 后续战斗段最响处。这段最响 0.4(两层各 0.2 叠满)，所有音符力度不超过 0.42。",
        "envelope": "E(t)=sin(pi t/32)，峰在第 16 小节。两层之和：两头 0.30·E(一高一低错着来)，第 12-20 小节 0.40·E(叠满)。",
        "harmony": "4 小节/和弦，全程压在低音 A 上、不解决：Am(add9)  Fmaj7/A  Dm9/A  E7sus4/A  Am(add9)  Fmaj7/A  Gadd9/A  Am(add9)",
        "motif": "钢琴「潮汐动机」= 长—短—长 的级进下行，再接一句中音区轻轻回升的回应；起音带 ±0.02~0.07 小节的手弹偏移，不卡拍。",
        "layers": [
            {"bars": "0-8",   "piano": "两个单音 -> 动机", "organ_strings": "两层错落，一高一低", "pad": "连续"},
            {"bars": "8-12",  "piano": "变奏 + 回应", "organ_strings": "两层开始对齐", "pad": "连续"},
            {"bars": "12-20", "piano": "双声部高潮两遍(峰顶 16)", "organ_strings": "两层叠满 0.4", "pad": "连续"},
            {"bars": "20-24", "piano": "回应收尾 -> 稀疏", "organ_strings": "两层散开", "pad": "连续"},
            {"bars": "24-32", "piano": "动机回落 -> 两个单音", "organ_strings": "两层错落，渐轻", "pad": "连续"},
        ],
        "tracks": tracks,
    }
    path = os.path.join(root, "MusicScore", "score", "HOME_Lazy.json")
    with io.open(path, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
    print("wrote", path, {t["slot"]: len(t["notes"]) for t in tracks})


if __name__ == "__main__":
    main()
