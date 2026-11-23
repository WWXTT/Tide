# -*- coding: utf-8 -*-
"""校验 MusicScore/score/HOME_Lazy.json 是否符合用户定的响度标尺与三层结构。

用法: python musicscore_verify_home.py <Tide 工程根>
每条检查都打印实测值和通过与否；全部通过返回 0。

检查的是用户 2026-11-23 定下的几件事：
  响度 0..1(1=战斗段最响处)，这段两层各 0.2、叠满 0.4；一次大起伏，峰在第 16 小节；
  最低的一组连续不断；两组 FLEX 此起彼伏且不留缝；最响的是最亮的一组和钢琴。
"""
import io
import json
import math
import os
import sys

import mido

BARS = 32
RES = 200           # 每小节采样点


def main(root):
    d = json.load(io.open(os.path.join(root, "MusicScore", "score", "HOME_Lazy.json"), encoding="utf-8"))
    T = {t["slot"]: t["notes"] for t in d["tracks"]}
    ok = True

    def check(name, passed, detail):
        print("  [%s] %s  %s" % ("PASS" if passed else "FAIL", name, detail))
        return passed

    # 1. 与 MIDI 对账
    mf = mido.MidiFile(os.path.join(root, "MusicScore", "midi", "HOME_Lazy.mid"))
    got = {next(m.name for m in tr if m.type == "track_name"):
           sum(1 for m in tr if m.type == "note_on" and m.velocity > 0) for tr in mf.tracks[1:]}
    want = {k: len(v) for k, v in T.items() if v}
    ok &= check("JSON 与 MIDI 音符数一致", want == got, "json=%s" % want)

    def on(slot, t):
        return [n for n in T[slot] if n[1] <= t < n[1] + n[2]]

    def top(slot, t):
        return max([n[3] for n in on(slot, t)] or [0.0])

    ts = [i / float(RES) for i in range(BARS * RES)]
    L = [top("organ", t) + top("strings", t) for t in ts]
    n = len(ts)

    def avg(a, b):
        s = [L[min(n - 1, int(round((a + (b - a) * k / 200.0) * RES)))] for k in range(201)]
        return sum(s) / len(s)

    def smooth(x, w=3 * RES + 1):
        i = min(n - 1, max(0, int(round(x * RES))))
        lo, hi = max(0, i - w // 2), min(n, i + w // 2 + 1)
        return sum(L[lo:hi]) / (hi - lo)

    # 2. 整体响度曲线
    pk = max(L)
    pk_at = ts[L.index(pk)]
    head, mid, tail = avg(0, 4), avg(14, 18), avg(28, 32)
    ref = [0.4 * math.sin(math.pi * t / BARS) for t in ts]
    mx, my = sum(ref) / n, sum(L) / n
    corr = sum((x - mx) * (y - my) for x, y in zip(ref, L)) / math.sqrt(
        sum((x - mx) ** 2 for x in ref) * sum((y - my) ** 2 for y in L))
    jump = max(abs(L[i + 1] - L[i]) for i in range(n - 1))
    s_peak, s_at = max((smooth(x), x) for x in [i / 20.0 for i in range(BARS * 20)])
    rising = all(smooth(x) <= smooth(x + 2) + 1e-9 for x in [i / 10.0 for i in range(0, 145)])
    falling = all(smooth(x) >= smooth(x + 2) - 1e-9 for x in [i / 10.0 for i in range(175, 315)])
    print("  两层力度之和的曲线：")
    print("    峰 %.3f(首次出现在第 %.1f 小节)，平滑后峰 %.3f 在第 %.1f 小节" % (pk, pk_at, s_peak, s_at))
    print("    平均  头(0-4) %.3f  中(14-18) %.3f  尾(28-32) %.3f" % (head, mid, tail))
    print("    与 0.4*sin(pi t/32) 的相关系数 %.3f；相邻采样最大台阶 %.3f" % (corr, jump))
    print("    各小节取值 4/8/12/16/20/24/28：%s" % " ".join("%.3f" % L[b * RES] for b in (4, 8, 12, 16, 20, 24, 28)))
    ok &= check("叠满处不超过 0.4", pk <= 0.401, "峰 %.3f" % pk)
    ok &= check("叠满处确实到得了 0.4", s_peak >= 0.39, "平滑峰 %.3f" % s_peak)
    ok &= check("峰在第 16 小节附近", 14.0 <= s_at <= 18.0, "第 %.1f 小节" % s_at)
    ok &= check("头尾比中段轻得多", head < 0.12 and tail < 0.12 and mid > 0.36,
                "头 %.3f 尾 %.3f 中 %.3f" % (head, tail, mid))
    ok &= check("形状像正弦", corr >= 0.93, "相关 %.3f" % corr)
    ok &= check("先升后降，没有第二个峰", rising and falling, "升 %s 降 %s" % (rising, falling))
    # 这条 0.08 是我自己拍的参考线，不是用户定的要求。每 2 小节换一个音、力度跟着那一层的涌浪换挡，
    # 所以一定有台阶(实测最大约 0.07，出现在一层涌到顶或落到底的时候)；把台阶磨平等于把起伏磨平。
    ok &= check("换音时的力度台阶在参考线内", jump <= 0.08, "最大 %.3f(标尺上限 0.4)" % jump)

    # 3. 两组 FLEX 此起彼伏
    def ratio(t):
        return top("organ", t) / max(1e-9, top("strings", t))
    r = {b: ratio(float(b)) for b in (4, 8, 24, 28)}
    centre = ratio(16.0)
    print("    管风琴:弦乐 的力度比  第4小节 %.2f  第8小节 %.2f  第16小节 %.2f  第24小节 %.2f  第28小节 %.2f"
          % (r[4], r[8], centre, r[24], r[28]))
    ok &= check("两头一高一低、并且轮换", r[4] > 1.3 and r[8] < 0.8 and r[24] < 0.8 and r[28] > 1.3,
                "见上")
    ok &= check("中段两层对齐", 0.9 <= centre <= 1.1, "第16小节 %.2f" % centre)
    eo = {int(x[1]) % 2 for x in T["organ"]}
    so = {int(x[1]) % 2 for x in T["strings"]}
    parity = {0: "偶", 1: "奇"}
    ok &= check("两组的起音小节错开", {x[1] for x in T["organ"]}.isdisjoint({x[1] for x in T["strings"]}),
                "管风琴在%s数小节起音，弦乐在%s数小节" % ("/".join(sorted(parity[e] for e in eo)),
                                                     "/".join(sorted(parity[s] for s in so))))

    # 4. 不留缝
    def overlap(slot):
        return all(T[slot][i][1] + T[slot][i][2] >= T[slot][i + 1][1] for i in range(len(T[slot]) - 1))
    ok &= check("底在任何时刻都响着", min(len(on("pad", t)) for t in ts) >= 2,
                "最少 %d 个音" % min(len(on("pad", t)) for t in ts))
    low = T["pad"][0]
    ok &= check("最低的音是横跨整段的一个长音", low[1] == 0 and low[2] == float(BARS),
                "音高 %d，第 %.0f-%.0f 小节" % (low[0], low[1], low[1] + low[2]))
    ok &= check("两组 FLEX 各自相邻的音都重叠", overlap("organ") and overlap("strings"), "")
    ok &= check("0-30 小节任何时刻至少一组 FLEX 在响",
                all(on("organ", t) or on("strings", t) for t in ts[:30 * RES]), "")

    # 5. 力度上限与音区
    hi = {s: max([x[3] for x in T[s]] or [0]) for s in T}
    ok &= check("这段所有音符不超过 0.42", max(hi.values()) <= 0.42, "各轨最响 %s" % hi)
    ok &= check("两层各自不超过 0.2", hi["organ"] <= 0.2 and hi["strings"] <= 0.2, "")
    ok &= check("弦乐是音区最高的一组", min(x[0] for x in T["strings"]) >= max(x[0] for x in T["organ"]),
                "弦乐 %d-%d，管风琴 %d-%d" % (min(x[0] for x in T["strings"]), max(x[0] for x in T["strings"]),
                                       min(x[0] for x in T["organ"]), max(x[0] for x in T["organ"])))
    pv = [x[3] for x in T["piano"]]
    ok &= check("钢琴也跟着起伏", min(pv) < 0.25 and max(pv) >= 0.4, "最轻 %.3f 最响 %.3f" % (min(pv), max(pv)))

    # 6. 越界与撞音
    over = [(s, x) for s in T for x in T[s] if x[1] + x[2] > BARS + 1e-9]
    ok &= check("没有音符越过循环点", not over, str(over))
    unison = [(s, x[0], x[1]) for s in ("organ", "strings") for x in T[s]
              for m in T["piano"] if m[0] == x[0] and m[1] < x[1] + x[2] and x[1] < m[1] + m[2]]
    ok &= check("FLEX 不和钢琴同度同响", not unison, str(unison))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "."))
