#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
特色机制原子挖掘 —— 从五个参考游戏全卡数据中验证候选原子的出现频次。

数据源：
    万智牌_效果列表.md / 炉石传说_效果列表.md   —— markdown 表（| 卡名 | 费用 | 效果描述 |）
    影之诗_data/影之诗_卡牌数据.json            —— name + skill_text_clean / evo_skill_text
    百闻牌_data/百闻牌_卡牌数据_全量.json        —— name + desc + keyword
    yugioh_data/{monster,spell,trap}/content.txt —— 【N】卡名 / 效果 文本块

输出：stdout 摘要 + 效果分析结果/特色机制原子_候选.md 证据报告（含每游戏卡数与样例卡名）。

用法:
    python mine_atoms.py
"""
import json
import re
from collections import OrderedDict
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent.resolve()
OUT_MD = SCRIPT_DIR / "效果分析结果" / "特色机制原子_候选.md"

# ---------------------------------------------------------------- 数据源加载

def load_md_cards(path: Path):
    """markdown 效果表 → [(卡名, 效果文本)]"""
    cards = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) < 3 or cells[0] in ("卡名", "------") or set(cells[0]) <= {"-", " "}:
            continue
        cards.append((cells[0], cells[2]))
    return cards


def load_sv_cards(path: Path):
    data = json.loads(path.read_text(encoding="utf-8"))
    out = []
    for c in data:
        text = " ".join(filter(None, [c.get("skill_text_clean"), c.get("evo_skill_text"), " ".join(c.get("tribe_names") or [])]))
        out.append((c.get("name", "?"), text))
    return out


def load_bwp_cards(path: Path):
    data = json.loads(path.read_text(encoding="utf-8"))
    out = []
    for c in data:
        text = " ".join(filter(None, [c.get("desc"), c.get("keyword"), c.get("type")]))
        out.append((c.get("name", "?"), text))
    return out


def load_ygo_cards(path: Path):
    """yugioh content.txt：按 ====== 分隔线切块，块内第一行 【N】 后为卡名"""
    raw = path.read_text(encoding="utf-8", errors="replace")
    cards = []
    for block in re.split(r"={20,}", raw):
        block = block.strip()
        m = re.search(r"【\d+】\s*\n+(.+)", block)
        if m:
            cards.append((m.group(1).strip().splitlines()[0].strip(), block))
    return cards


# ---------------------------------------------------------------- 候选定义
# pattern: 每游戏一组正则（re.search 命中一次即计该卡）；缺省用 default。
#          无该游戏数据写 None 表示机制不存在/不适用。

CANDIDATES = OrderedDict([
    # --- 特色簇：收录候选 ---
    ("AddCountdown", {
        "label": "倒计时指示物（吟唱/倒计时/休眠/闸延）",
        "default": [r"吟唱[＿_ ]?\d*", r"倒计时", r"休眠", r"闸延"],
        "mtg": [r"闸延", r"计时指示物"],
        "hs": [r"休眠", r"倒计时"],
        "sv": [r"吟唱[＿_ ]?\d*"],
        "bwp": [r"倒计时"],
        "ygo": None,
    }),
    ("ConsumeGraveyard", {
        "label": "墓地消耗（唤灵/墓地除外作代价/逸脱）",
        "default": [r"唤灵", r"将墓地", r"墓地.{0,8}除外", r"从墓地.{0,6}除外", r"逸脱"],
        "sv": [r"唤灵", r"除外自己墓地"],
        "ygo": [r"墓地.{0,12}除外", r"将.{0,12}从游戏中除外"],
        "mtg": [r"逸脱", r"从你的坟墓场放逐"],
    }),
    ("RecoverFromExile", {
        "label": "除外区回收（游戏王）",
        "default": [r"除外的卡", r"被除外", r"从除外"],
        "ygo": [r"被除外的卡", r"除外的.{0,8}卡", r"从除外"],
        "others": None,
    }),
    ("SetFaceDown", {
        "label": "覆盖/设陷（陷阱/奥秘/响应）",
        "hs": [r"奥秘"],
        "bwp": [r"响应[：:]"],
        "ygo": [r"覆盖", r"里侧"],
        "mtg": None, "sv": None,
    }),
    ("GrantAwakened", {
        "label": "觉醒状态（百闻牌觉醒牌）",
        "bwp": [r"觉醒[：:·]", r"^觉醒"],
        "sv": [r"觉醒"],
        "others": None,
    }),
    ("DiscoverCard", {
        "label": "发现/三选一（炉石）",
        "hs": [r"发现[一一个张张]?|[（(]?\d+选1", r"三选一"],
        "others": None,
    }),
    ("OverloadResource", {
        "label": "过载/资源预支（炉石）",
        "hs": [r"过载"],
        "others": None,
    }),
    ("ModifyLevel", {
        "label": "等级操纵（游戏王）",
        "ygo": [r"等级.{0,6}(上升|下降|提升|降低|上升\d|下降\d|变为|变成)", r"等级\d+以下", r"等级可以让"],
        "others": None,
    }),
    ("SummonFromDeck", {
        "label": "从牌库/牌组直接召唤",
        "ygo": [r"从牌组.{0,12}(特殊)?召唤"],
        "hs": [r"招募"],
        "mtg": [r"从你牌库.{0,15}(战场|进场)", r"搜寻一张.{0,10}生物牌.{0,6}战场"],
        "sv": [r"牌组.{0,10}(召唤|特殊召唤)"],
    }),
    ("CopyCard", {
        "label": "复制卡牌/单位",
        "default": [r"复制"],
        "ygo": [r"复制", r"同名卡"],
    }),
    ("SurveilCards", {
        "label": "刺探（万智：查看顶若干，任意入墓，余回底）",
        "mtg": [r"刺探"],
        "others": None,
    }),
    # --- 备选 ---
    ("GrantEvolved", {
        "label": "进化（影之诗，备选：≈+2/+2 可组合）",
        "sv": [r"进化时", r"超进化"],
        "others": None,
    }),
    ("GainElement", {
        "label": "直接获得法术力/资源（万智加费，备选）",
        "mtg": [r"加到你的法术力池", r"加\d+点.{0,2}法术力", r"加一点.{0,3}法术力"],
        "others": None,
    }),
    ("CascadeReveal", {
        "label": "倾曳（万智，备选：语义复杂）",
        "mtg": [r"倾曳"],
        "others": None,
    }),
    # --- 跳过项证据（实现同现有原子，仅记录频次供报告） ---
    ("SKIP_突袭族", {
        "label": "【跳过】突袭/疾驰/敏捷/冲锋 ≈ 同一实现不同名",
        "hs": [r"突袭|冲锋"], "sv": [r"疾驰|突进"], "mtg": [r"敏捷"], "bwp": [r"瞬发|迅捷"], "ygo": None,
    }),
    ("SKIP_风怒", {
        "label": "【跳过】风怒 ≈ 连击 GrantDoubleStrike",
        "hs": [r"风怒"], "others": None,
    }),
    ("SKIP_屏障", {
        "label": "【跳过】屏障 ≈ 圣盾 GrantDivineShield",
        "sv": [r"屏障"], "others": None,
    }),
])


def match_count(cards, patterns):
    if not patterns:
        return 0, []
    hits = []
    for name, text in cards:
        for p in patterns:
            if re.search(p, text):
                hits.append(name)
                break
    return len(hits), hits


def main():
    sources = {
        "mtg": load_md_cards(SCRIPT_DIR / "万智牌_效果列表.md"),
        "hs": load_md_cards(SCRIPT_DIR / "炉石传说_效果列表.md"),
        "sv": load_sv_cards(SCRIPT_DIR / "影之诗_data" / "影之诗_卡牌数据.json"),
        "bwp": load_bwp_cards(SCRIPT_DIR / "百闻牌_data" / "百闻牌_卡牌数据_全量.json"),
        "ygo": (
            load_ygo_cards(SCRIPT_DIR / "yugioh_data" / "monster" / "content.txt")
            + load_ygo_cards(SCRIPT_DIR / "yugioh_data" / "spell" / "content.txt")
            + load_ygo_cards(SCRIPT_DIR / "yugioh_data" / "trap" / "content.txt")
        ),
    }
    print({k: len(v) for k, v in sources.items()})

    lines = ["# 特色机制原子候选证据（五游戏全卡扫描）", "",
             f"数据源卡数：{'，'.join(f'{k}={len(v)}' for k, v in sources.items())}", ""]
    for key, spec in CANDIDATES.items():
        lines += [f"## {key} — {spec['label']}", "", "| 游戏 | 命中卡数 | 样例 |", "|------|---------|------|"]
        total = 0
        for game in ("mtg", "hs", "sv", "bwp", "ygo"):
            pats = spec.get(game, spec.get("default"))
            if game not in spec and "default" not in spec:
                continue
            if spec.get(game, spec.get("default")) is None:
                continue
            n, names = match_count(sources[game], pats)
            total += n
            sample = "、".join(names[:6]) + (f" 等{n}张" if n > 6 else "")
            lines.append(f"| {game} | {n} | {sample} |")
        lines += ["", f"**合计：{total} 张**", ""]
        print(f"{key:<22} {spec['label']:<30} total={total}")

    OUT_MD.parent.mkdir(exist_ok=True)
    OUT_MD.write_text("\n".join(lines), encoding="utf-8")
    print(f"\n报告已写入: {OUT_MD}")


if __name__ == "__main__":
    main()
