#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
特色机制原子补充 —— 依据 mine_atoms.py 的五游戏证据，将 12 条特色原子追加到
AttributeValueConfig.json（随后由 import_from_json.py 同步回 Attribute.xlsm）。

筛选规则（用户确认）：
    能用现有 83 原子组合的忽略；实现相同仅名字不同的跳过（突袭/疾驰、风怒、屏障等）；
    只收各游戏特色机制中具有独立原子语义的。
ID = sha256(DisplayName.strip())[:8]，与 gen_effect_ids.py 一致；冲突即中止不落盘。
"""
import hashlib
import json
import shutil
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent.resolve()
JSON_PATH = SCRIPT_DIR.parent / "Assets" / "Configs" / "AttributeValueConfig.json"

# (EffectType, EnumName, DisplayName, EffectFunction, EffectColor, BaseCost, TargetKinds, TargetFilter, Polarity, 依据)
NEW_ATOMS = [
    # 影之诗吟唱(24)/百闻牌倒计时(100)/炉石休眠(67)/万智计时指示物(73)：跨游戏倒计时簇
    ("AddCountdown", "倒计时", "为{target}放置{value}个倒计时指示物（计数归零时触发）",
     "Counter", "Blue", 0.5, "0,1,2,3", None, 0.0,
     "sv=24,bwp=100,hs=67,mtg=73"),
    # 游戏王墓地除外(2321)/万智坟场放逐·逸脱(317)/影之诗唤灵(10)：现有 Exile 仅场上域(0,1)
    ("ExileFromGraveyard", "墓地除外", "将墓地中的{target}除外",
     "Movement", "Gray", 0.5, "8,9", None, 0.0,
     "ygo=2321,mtg=317,sv=10"),
    # 游戏王除外区回收(75)：除外区(10,11)域目前无任何原子
    ("RecoverFromExile", "除外回收", "将除外区的{target}收回手牌",
     "Movement", "Gray", 1.5, "10,11", None, 1.0,
     "ygo=75"),
    # 游戏王覆盖(310)/炉石奥秘(176)/百闻牌响应(64)：跨游戏设陷簇，现有原子无"背面置入发动区"
    ("SetFaceDown", "覆盖", "将{target}背面朝上置于发动区（陷阱/奥秘/响应，条件满足时发动）",
     "Movement", "Blue", 0.5, "4", None, 0.0,
     "ygo=310,hs=176,bwp=64"),
    # 百闻牌觉醒(223)：式神永久强化状态标志，不可由现有原子组合
    ("GrantAwakened", "觉醒", "{target}觉醒（获得永久强化状态标志）",
     "Status", "White", 2.0, "0", "NoRole", 1.0,
     "bwp=223"),
    # 影之诗进化(292)：进化状态标志（进化时触发器/超进化解锁的载体），本体+2/+2可组合但状态不可
    ("GrantEvolved", "进化", "使{target}进化（获得进化状态标志）",
     "Status", "Blue", 1.0, "0", "NoRole", 1.0,
     "sv=292"),
    # 炉石发现(477)：随机三选一，与 SearchDeck（宣言检索）实现不同
    ("DiscoverCard", "发现", "从{stringvalue}域展示3张随机卡牌，选1张加入手牌",
     "Movement", "Blue", 1.5, "0,1", None, 1.0,
     "hs=477"),
    # 炉石过载(111)：资源预支惩罚，作用于自身元素池(12)
    ("OverloadResource", "过载", "下回合锁定自身{value}点可用资源（过载）",
     "Resource", "Red", 0.5, "12", None, -1.0,
     "hs=111"),
    # 游戏王等级操纵(584)：攻/命/费均有 Modify，等级缺失
    ("ModifyLevel", "等级操纵", "为{target}添加{value}个等级指示物",
     "Counter", "Blue", 1.0, "0,1", "NoRole", 0.0,
     "ygo=584"),
    # 游戏王从牌组特殊召唤(202)/万智 tutor-to-battlefield(81)/炉石招募(10)：检索后直接上场，非入手
    ("SummonFromDeck", "牌库召唤", "从牌库将{target}直接召唤至战场（不经过手牌）",
     "Movement", "White", 1.5, "6", None, 1.0,
     "ygo=202,mtg=81,hs=10"),
    # 复制(五游戏共1412)：Morph 是变形现有单位，复制=生成副本卡，现有原子无法组合
    ("CopyCard", "复制", "复制{target}，生成一张相同卡牌的副本至手牌",
     "Movement", "Blue", 1.5, "0,1", "NoRole", 1.0,
     "mtg=744,hs=451,ygo=181,bwp=19,sv=17"),
    # 万智刺探(190)：部分入墓+余回底，与 Scry（任意排列回顶/底）实现不同
    ("SurveilCards", "刺探", "查看牌库顶{value}张，将其中任意张置入墓地，其余放回牌库底",
     "Information", "Blue", 1.0, "0,1", None, 0.0,
     "mtg=190"),
]


def effect_id(desc: str) -> str:
    return hashlib.sha256(desc.strip().encode("utf-8")).hexdigest()[:8]


def main():
    rows = json.loads(JSON_PATH.read_text(encoding="utf-8"))
    existing_types = {r["EffectType"] for r in rows}
    existing_ids = {r["ID"] for r in rows}
    existing_desc = {r["DisplayName"] for r in rows}

    added = []
    for etype, ename, dname, fn, color, cost, tk, tf, pol, basis in NEW_ATOMS:
        assert etype not in existing_types, f"EffectType 已存在: {etype}"
        assert dname not in existing_desc, f"DisplayName 重复: {dname}"
        eid = effect_id(dname)
        assert eid not in existing_ids, f"ID 冲突: {eid} ({dname})"
        row = {"ID": eid, "EnumName": ename, "DisplayName": dname, "EffectFunction": fn,
               "EffectColor": color, "BaseCost": float(cost), "EffectType": etype,
               "TargetKinds": tk, "TargetFilter": tf, "Polarity": float(pol)}
        rows.append(row)
        added.append(row)
        existing_types.add(etype)
        existing_ids.add(eid)
        existing_desc.add(dname)
        print(f"  + {etype:<22} {ename:<4} id={eid} cost={cost} tk={tk} pol={pol}  [{basis}]")

    # 新增行之间 ID 互查（上面循环已并入 existing_ids，冲突会 assert）
    shutil.copy2(JSON_PATH, JSON_PATH.with_suffix(".json.bak"))
    JSON_PATH.write_text(json.dumps(rows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"\n已备份 → {JSON_PATH.with_suffix('.json.bak')}")
    print(f"已写入 {len(added)} 条新原子，总行数 {len(rows)} → {JSON_PATH}")


if __name__ == "__main__":
    sys.exit(main())
