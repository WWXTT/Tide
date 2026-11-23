# -*- coding: utf-8 -*-
"""Tide 曲谱(JSON) -> MIDI。

为什么要它：FL Trial 存不了工程，MusicScore/ 里的 JSON 才是曲谱真源；
MIDI 是从 JSON 派生的通用格式，拖进 FL 或任何 DAW 就能看到全部音符。

用法（任意装有 mido 的 Python；FL MCP 的 venv 自带）：
    C:\\Users\\Administrator\\Tools\\flstudio-mcp\\.venv\\Scripts\\python.exe MusicScore\\build_midi.py
    ... build_midi.py a.json b.json    只转指定曲谱
    ... build_midi.py --archive        连 archive/ 里的历史版本一起转

输出到 MusicScore/midi/：
    <曲谱文件基名>.mid      每份曲谱一个（每个乐器槽位一条 MIDI 轨）
    Tide_All_in_One.mid     score/ 里现行的段落按 SONG_ORDER 首尾相接，段间留 GAP_BARS 小节

曲谱 JSON 约定（schema tide-musicscore/1）：
    bpm, bars, tracks[] ；每条 track 有 slot(乐器槽位) 和 notes[]；
    notes 元素 = [MIDI 音高, 起始(小节), 时值(小节), 力度 0..1]，4/4 拍。
"""
import glob
import io
import json
import os
import sys

import mido

PPQ = 480
BEATS_PER_BAR = 4
TICKS_PER_BAR = PPQ * BEATS_PER_BAR
GAP_BARS = 2
DRUM_CHANNEL = 9
# 「像一首歌」的试听顺序：主界面 -> 组合界面 -> 战斗三段 -> 胜利 -> 失败
SONG_ORDER = ["HOME_Lazy", "BROWSE_Calm", "BATTLE_A_Early", "BATTLE_B_Mid",
              "BATTLE_C_Late", "END_Victory", "END_Defeat"]

HERE = os.path.dirname(os.path.abspath(__file__))
SCORE_DIR = os.path.join(HERE, "score")
ARCHIVE_DIR = os.path.join(HERE, "archive")
MIDI_DIR = os.path.join(HERE, "midi")


def load(path):
    with io.open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def clamp_velocity(v):
    return max(1, min(127, int(round(float(v) * 127))))


class Part(object):
    """一个乐器槽位的全部音符；可以在拼接时多次追加（带小节偏移）。"""

    def __init__(self, name, channel):
        self.name = name
        self.channel = channel
        self.events = []      # (tick, 排序键, message)；排序键让同刻的 note_off 先于 note_on
        self.notes = 0
        self.last_tick = 0

    def add_notes(self, notes, offset_bars=0.0):
        for item in notes:
            pitch = int(item[0])
            if not 0 <= pitch <= 127:
                raise ValueError("pitch out of range: %r" % (item,))
            start = int(round((float(item[1]) + offset_bars) * TICKS_PER_BAR))
            end = int(round((float(item[1]) + float(item[2]) + offset_bars) * TICKS_PER_BAR))
            end = max(end, start + 1)
            on = mido.Message("note_on", channel=self.channel, note=pitch,
                              velocity=clamp_velocity(item[3]))
            off = mido.Message("note_off", channel=self.channel, note=pitch, velocity=0)
            self.events.append((start, 1, on))
            self.events.append((end, 0, off))
            self.notes += 1
            self.last_tick = max(self.last_tick, end)

    def to_mido(self):
        out = mido.MidiTrack()
        out.append(mido.MetaMessage("track_name", name=self.name, time=0))
        last = 0
        for tick, _order, msg in sorted(self.events, key=lambda e: (e[0], e[1])):
            out.append(msg.copy(time=tick - last))
            last = tick
        out.append(mido.MetaMessage("end_of_track", time=0))
        return out


def free_channel(used):
    c = 0
    while c == DRUM_CHANNEL or c in used:
        c += 1
    return c


def render(parts, bpm, title, total_bars):
    mf = mido.MidiFile(type=1, ticks_per_beat=PPQ)
    conductor = mido.MidiTrack()
    conductor.append(mido.MetaMessage("track_name", name=title, time=0))
    conductor.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(bpm), time=0))
    conductor.append(mido.MetaMessage("time_signature", numerator=BEATS_PER_BAR,
                                      denominator=4, time=0))
    # 结束标记放在整段长度处，拼接/导入时保留段尾的留白
    conductor.append(mido.MetaMessage("end_of_track", time=int(total_bars * TICKS_PER_BAR)))
    mf.tracks.append(conductor)
    for part in parts:
        mf.tracks.append(part.to_mido())
    return mf


def build_one(doc, path):
    parts, used = [], set()
    for track in doc.get("tracks", []):
        if not track.get("notes"):
            continue                  # 没有音符的槽位不出空轨
        slot = track.get("slot", "part%d" % (len(parts) + 1))
        if slot == "drums":
            channel = DRUM_CHANNEL
        else:
            channel = free_channel(used)
        used.add(channel)
        part = Part(slot, channel)
        part.add_notes(track.get("notes", []))
        parts.append(part)
    bars = int(doc["bars"])
    for part in parts:
        over = part.last_tick - bars * TICKS_PER_BAR
        if over > 0:
            sys.stderr.write("warning: %s slot '%s' 超出 %d 小节 %.2f 拍\n"
                             % (doc["pattern"], part.name, bars, float(over) / PPQ))
    name = os.path.splitext(os.path.basename(path))[0]
    if not any(p.notes for p in parts):
        # 只有文字记录(和声/节奏型)、没有音符的曲谱不导出，免得留下空 MIDI 误导人
        print("%-26s 仅文字记录，无音符，未导出" % (name + ".mid"))
        return doc
    out = os.path.join(MIDI_DIR, name + ".mid")
    render(parts, doc["bpm"], doc["pattern"], bars).save(out)
    sec = bars * BEATS_PER_BAR * 60.0 / doc["bpm"]
    print("%-26s %2d 小节 %5.1fs  %s" % (
        name + ".mid", bars, sec,
        "  ".join("%s:%d" % (p.name, p.notes) for p in parts) or "(无音符)"))
    return doc


def build_all_in_one():
    parts, offset, bpm = {}, 0.0, None
    used_segments = []
    for pattern in SONG_ORDER:
        path = os.path.join(SCORE_DIR, pattern + ".json")
        if not os.path.exists(path):
            continue
        doc = load(path)
        if bpm is None:
            bpm = doc["bpm"]
        elif doc["bpm"] != bpm:
            raise ValueError("%s 的 bpm=%s 与串烧的 %s 不一致" % (pattern, doc["bpm"], bpm))
        for track in doc.get("tracks", []):
            slot = track.get("slot", "misc")
            if slot not in parts:
                parts[slot] = Part(slot, DRUM_CHANNEL if slot == "drums" else free_channel(
                    set(p.channel for p in parts.values())))
            parts[slot].add_notes(track.get("notes", []), offset)
        used_segments.append("%s@%d" % (pattern, int(offset)))
        offset += int(doc["bars"]) + GAP_BARS
    if not used_segments:
        print("Tide_All_in_One.mid: score/ 里还没有曲谱，跳过")
        return
    total = int(offset - GAP_BARS)
    render([parts[k] for k in sorted(parts)], bpm, "Tide (all in one)", total).save(
        os.path.join(MIDI_DIR, "Tide_All_in_One.mid"))
    print("Tide_All_in_One.mid       %2d 小节 %5.1fs  %s" % (
        total, total * BEATS_PER_BAR * 60.0 / bpm, "  ".join(used_segments)))


def main(argv):
    flags = [a for a in argv if a.startswith("--")]
    paths = [a for a in argv if not a.startswith("--")]
    if paths:
        files = paths
    else:
        files = sorted(glob.glob(os.path.join(SCORE_DIR, "*.json")))
        if "--archive" in flags:
            files += sorted(glob.glob(os.path.join(ARCHIVE_DIR, "*.json")))
    if not os.path.isdir(MIDI_DIR):
        os.makedirs(MIDI_DIR)
    if not files:
        print("没有找到曲谱 JSON")
        return 1
    for path in files:
        build_one(load(path), path)
    if not paths:
        build_all_in_one()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
