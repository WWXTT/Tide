# -*- coding: utf-8 -*-
"""主题测试卡组生成器（Assets/Configs/TestDecks/Deck_{Red,Blue,Green}.json）。

用途：测试集——训练用 TestCreatureCards.json，胜率验证改用这三套主题卡组，考察
「同原子表、新组合/新参数」的迁移（这正是内容寻址身份设计的目标场景）。

生成规则（三色一致，改动规则请三色同步）：
  1. 每套 60 张 = 45 生物 + 15 法术；id DECK_{R|B|G}_01..60（生物 01-45，法术 46-60）。
  2. 效果一律 OnPlay（TriggerTiming 0）+ 强制（ActivationType 0），不挂效果级 Costs，
     无条件/Steps（扁平 AtomicEffects）——与训练池（TestCreatureCards）口径一致。
  3. costList 留空 → CardCostService.EnsureCost 装载期按原子表自动计价（配置表唯一权威，
     颜色由 EffectColor 亲和决定；生物作地牌进元素池时按费用构成产对应色元素，自洽）。
  4. 目标覆盖仅用四种：默认（引擎自动解析，对敌控制类）/ Self=0（自身增益、激励自身）/
     Owner=6（回复/护甲给自己英雄）/ Opponent=8（打脸）/ AllAllies=3（群体友方增益）。
  5. keywords 只用已验证的印刷关键词 ID（与 TestCreatureCards 同源）。
  6. 主题：
     - 红：激励（Untap，冲锋式登场）+ 临时加攻（ModifyPower 默认 UntilEndOfTurn）+
           快速打脸（DealDamage/PierceDamage/DrainLife→Opponent），小体型快曲线。
     - 蓝：弹回（ReturnToHand/BounceToTop）+ 冻结/横置（FreezePermanent/Tap）控制，
           让对方生物动不了、自己单方面输出；中曲线 + 警戒/法盾/潜行防御。
     - 绿：成长（Growth 关键词/AddPlusOne/+1+1 指示物）+ 恢复（Heal/AddArmor/再生/复生），
           大生物后期碾压；高曲线。
"""

import json
import os
import random
import sys
from collections import OrderedDict

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Configs", "TestDecks")

SELF, ALL_ALLIES, OWNER, OPPONENT = 0, 3, 6, 8


def atom(effect_type, value=0, value2=0, tto=-1, filter_override="", duration=0):
    """原子条目（哨兵=沿用原子表默认）。tto = TargetTypeOverride。"""
    return {
        "EffectType": effect_type,
        "Value": value,
        "Value2": value2,
        "StringValue": "",
        "ManaTypeParam": 0,
        "ZoneParam": 0,
        "Duration": duration,
        "DurationValue": 0,
        "TargetTypeOverride": tto,
        "TargetFilterOverride": filter_override,
        "TargetCountOverride": -2,
        "TargetScopeOverride": -1,
        "DynamicTargetCount": False,
        "Drawbacks": [],
    }


def effect(entry_id, display, desc, atoms):
    return {
        "Id": entry_id,
        "DisplayName": display,
        "Description": desc,
        "TriggerTiming": 0,
        "ActivationType": 0,
        "BaseSpeed": 0,
        "IsOptional": False,
        "Duration": 0,
        "ActivationConditions": [],
        "TriggerConditions": [],
        "AtomicEffects": atoms,
        "Costs": [],
        "Tags": [],
        "Steps": [],
    }


def card(num, name, supertype, power, life, keywords, effects):
    return {
        "id": "",  # 由调用方填
        "cardName": name,
        "supertype": supertype,
        "power": power,
        "life": life,
        "costList": [],
        "keywords": keywords or [],
        "tags": ["test-deck"],
        "effects": effects,
        "subtype": "",
        "level": -1,
        "rank": -1,
        "linkRating": -1,
        "arrows": "",
        "linkAuras": [],
   }


def _content_sig(c):
    """内容签名（镜像 C# ContentId 口径）：排除 id/卡名/效果展示三件套，其余全量。"""
    scrubbed = json.loads(json.dumps(c, ensure_ascii=False))
    scrubbed.pop("id", None); scrubbed.pop("cardName", None)
    for e in scrubbed.get("effects", []):
        for k in ("Id", "DisplayName", "Description"):
            e.pop(k, None)
    return json.dumps(scrubbed, ensure_ascii=False, sort_keys=True)


def _bump_variant(supertype, power, life, effects, bump):
    """撞车微调：生物按 生命+1 → 攻击+1 轮转递增；法术无属性，递增首个原子 Value。"""
    if supertype != "Spell":
        return power + bump // 2, life + (bump + 1) // 2, effects
    eff = [(d, desc, [dict(a) for a in atoms]) for d, desc, atoms in effects]
    if eff and eff[0][2]:
        eff[0][2][0] = dict(eff[0][2][0])
        eff[0][2][0]["Value"] = eff[0][2][0].get("Value", 0) + bump
    return power, life, eff


def dedupe_specs(specs):
    """同名不同字的「·变体」若内容撞车，微调到全池内容唯一（内容 ID = 内容哈希，重复 =
    同一张卡两份；测试集要求 60 张内容互异，覆盖最大化）。"""
    seen = set()
    out = []
    for name, supertype, power, life, keywords, effects in specs:
        bump = 0
        while True:
            p, l, eff = _bump_variant(supertype, power, life, effects, bump)
            c = card(0, name, supertype, p, l, keywords,
                     [effect("_", d, desc, atoms) for d, desc, atoms in eff])
            sig = _content_sig(c)
            if sig not in seen:
                break
            bump += 1
        seen.add(sig)
        out.append((name, supertype, p, l, keywords, eff))
    return out


def build_deck(prefix, color_name, specs):
    """specs: [(name, supertype, power, life, keywords, [(display, desc, [atoms])])]，按序编号。"""
    specs = dedupe_specs(specs)
    cards = []
    for i, (name, supertype, power, life, keywords, effects) in enumerate(specs, start=1):
        c = card(i, name, supertype, power, life, keywords,
                 [effect(f"DECK_{prefix}_{i:02d}_{k:02d}", d, desc, atoms)
                  for k, (d, desc, atoms) in enumerate(effects)])
        c["id"] = f"DECK_{prefix}_{i:02d}"
        cards.append(c)
    assert len(cards) == 60, f"{color_name} 卡数 {len(cards)} != 60"
    sigs = [_content_sig(c) for c in cards]
    assert len(set(sigs)) == 60, f"{color_name} 仍有 {60 - len(set(sigs))} 张内容重复"
    return {"cards": cards, "deckConfig": {"copiesPerCard": 1}}


def C(name, power, life, keywords=None, effects=None):
    return (name, "Creature", power, life, keywords or [], effects or [])


def S(name, effects):
    return (name, "Spell", 0, 0, [], effects)


# ============================================================ 红：激励 + 临时加攻 + 打脸 ============================================================

def red_creatures():
    specs = []
    # 激励兵（登场激励自身 = 冲锋，当回合可行动）
    for n, p, l in [("赤先锋", 1, 1), ("赤先锋·烈", 2, 1), ("赤突袭兵", 2, 1),
                    ("赤突击手", 2, 2), ("赤疾行兵", 3, 2), ("赤疾风统领", 3, 3)]:
        specs.append(C(n, p, l, [], [("激励", f"登场：激励自身——当回合即可行动", [atom("Untap", 1, tto=SELF)])]))
    # 打脸兵（登场直接对敌方英雄造成伤害）
    for n, p, l, v in [("赤火星", 1, 1, 1), ("赤火星·迸", 2, 1, 1), ("赤灼前锋", 2, 2, 2),
                       ("赤灼前锋·炽", 2, 2, 2), ("赤焰矛手", 3, 2, 3), ("赤焰矛手·烈", 3, 2, 3),
                       ("赤燎原先锋", 4, 3, 4)]:
        specs.append(C(n, p, l, [], [("燃面", f"登场：对敌方英雄造成 {v} 点伤害", [atom("DealDamage", v, tto=OPPONENT)])]))
    # 自强化（登场临时 +攻，默认持续到回合末）
    for n, p, l, v in [("赤怒战士", 2, 1, 2), ("赤怒战士·狂", 2, 2, 2), ("赤斗殴者", 2, 2, 2),
                       ("赤斗殴者·狠", 3, 2, 3), ("赤暴走兵", 3, 2, 3)]:
        specs.append(C(n, p, l, [], [("怒焰", f"登场：自身攻击力本回合 +{v}", [atom("ModifyPower", v, tto=SELF)])]))
    # 战号（全体友方临时加攻）
    for n, p, l, v in [("赤战号手", 2, 2, 1), ("赤战号手·昂", 2, 2, 1), ("赤鼓手", 3, 2, 2), ("赤鼓手·振", 3, 2, 2)]:
        specs.append(C(n, p, l, [], [("战号", f"登场：全体友方生物攻击力本回合 +{v}", [atom("ModifyPower", v, tto=ALL_ALLIES)])]))
    # 穿透兵（无视护甲直伤英雄）
    for n, p, l, v in [("赤穿甲兵", 2, 2, 2), ("赤穿甲兵·锐", 2, 2, 2), ("赤贯甲者", 3, 3, 3), ("赤破城锤", 4, 3, 4)]:
        specs.append(C(n, p, l, [], [("贯穿", f"登场：对敌方英雄造成 {v} 点穿透伤害", [atom("PierceDamage", v, tto=OPPONENT)])]))
    # 吸血兵（打脸同时回血）
    for n, p, l, v in [("赤血食者", 2, 2, 2), ("赤血食者·贪", 2, 2, 2), ("赤噬血兽", 3, 3, 3)]:
        specs.append(C(n, p, l, [], [("噬血", f"登场：对敌方英雄造成 {v} 点伤害并回复等量生命", [atom("DrainLife", v, tto=OPPONENT)])]))
    # 易损标记（目标本回合受伤 +）
    for n in ["赤标记者", "赤标记者·显"]:
        specs.append(C(n, 2, 2, [], [("标记", "登场：令目标易损，本回合受到的伤害增加", [atom("AddVulnerable", 1)])]))
    # 印刷关键词体型（先攻/连击/碾压/缴械）
    for n, p, l, kw in [("赤先攻剑士", 3, 2, "FirstStrike"), ("赤先攻剑士·疾", 3, 2, "FirstStrike"),
                        ("赤连击斗士", 3, 2, "DoubleStrike"), ("赤碾压蛮兵", 4, 3, "Overwhelm"),
                        ("赤碾压蛮兵·凶", 4, 3, "Overwhelm"), ("赤缴械武者", 4, 4, "Disarm"),
                        ("赤缴械武者·缚", 4, 4, "Disarm"), ("赤先攻统领", 4, 3, "FirstStrike")]:
        specs.append(C(n, p, l, [kw]))
    # 收尾大冲锋（激励自身 + 临时大加攻）
    for n, p, l, v in [("赤暴冲锋兽", 5, 5, 2), ("赤暴冲锋兽·王", 5, 5, 2), ("赤狂战领主", 6, 5, 3)]:
        specs.append(C(n, p, l, [], [
            ("激励", "登场：激励自身——当回合即可行动", [atom("Untap", 1, tto=SELF)]),
            ("狂暴", f"登场：自身攻击力本回合 +{v}", [atom("ModifyPower", v, tto=SELF)]),
        ]))
    # 元素素材（带先攻关键词——白板 1/1、2/2 计价取整为零费，白送不合规则口径）
    specs.append(C("赤军犬", 1, 1, ["FirstStrike"])); specs.append(C("赤巡哨", 2, 2, ["FirstStrike"])); specs.append(C("赤老兵", 3, 3))
    assert len(specs) == 45, len(specs)
    return specs


def red_spells():
    specs = []
    for n, v in [("赤火刃", 2), ("赤火刃·强", 3), ("赤烈焰矢", 3), ("赤烈焰矢·猛", 4)]:
        specs.append(S(n, [("火刃", f"对目标造成 {v} 点伤害", [atom("DealDamage", v)])]))
    for n, v in [("赤焚面", 3), ("赤焚面·炽", 4), ("赤毁灭之焰", 5)]:
        specs.append(S(n, [("焚面", f"对敌方英雄造成 {v} 点伤害", [atom("DealDamage", v, tto=OPPONENT)])]))
    for n, v in [("赤突进令", 2), ("赤突进令·急", 2), ("赤战斗怒吼", 3)]:
        specs.append(S(n, [("突进", f"激励一个友方生物并使其攻击力本回合 +{v}",
                        [atom("Untap", 1, tto=SELF), atom("ModifyPower", v, tto=SELF)])]))
    specs.append(S("赤裂甲咒", [("裂甲", "令目标易损，本回合受到的伤害增加", [atom("AddVulnerable", 1)])]))
    for n, v in [("赤贯穿枪", 4), ("赤贯穿枪·贯", 4)]:
        specs.append(S(n, [("贯穿", f"对敌方英雄造成 {v} 点穿透伤害", [atom("PierceDamage", v, tto=OPPONENT)])]))
    for n, v in [("赤吸血涌动", 3), ("赤噬血术", 3)]:
        specs.append(S(n, [("噬血", f"对敌方英雄造成 {v} 点伤害并回复等量生命", [atom("DrainLife", v, tto=OPPONENT)])]))
    assert len(specs) == 15, len(specs)
    return specs


# ============================================================ 蓝：弹回 + 冻结控制 ============================================================

def blue_creatures():
    specs = []
    # 冻结师
    for n, p, l in [("湛冰法师", 2, 2), ("湛冰法师·凛", 2, 2), ("湛寒霜术士", 2, 3), ("湛寒霜术士·凝", 2, 3),
                    ("湛冻雾行者", 3, 3), ("湛冻雾行者·深", 3, 3), ("湛极冰使者", 3, 4)]:
        specs.append(C(n, p, l, [], [("冻结", "登场：冻结一个敌方生物，其无法行动", [atom("FreezePermanent", 1)])]))
    # 横置师
    for n, p, l in [("湛锁链师", 2, 3), ("湛锁链师·缠", 2, 3), ("湛定身术士", 3, 3),
                    ("湛定身术士·固", 3, 3), ("湛缚影者", 3, 4)]:
        specs.append(C(n, p, l, [], [("定身", "登场：横置一个敌方生物", [atom("Tap", 1)])]))
    # 弹回师
    for n, p, l in [("湛回流术士", 2, 2), ("湛回流术士·涌", 2, 2), ("湛逆潮行者", 3, 2),
                    ("湛逆潮行者·卷", 3, 2), ("湛退浪师", 3, 3)]:
        specs.append(C(n, p, l, [], [("回流", "登场：将一个生物移回其拥有者手牌", [atom("ReturnToHand", 1)])]))
    # 沉默师
    for n, p, l in [("湛禁言官", 2, 3), ("湛禁言官·肃", 2, 3), ("湛缄默审判者", 3, 4), ("湛缄默官·深", 3, 4)]:
        specs.append(C(n, p, l, [], [("缄默", "登场：沉默一个生物，其失去所有效果", [atom("Silence", 1)])]))
    # 智慧师
    for n, p, l in [("湛卷轴师", 1, 2), ("湛卷轴师·览", 1, 2), ("湛学者", 2, 2), ("湛学者·博", 2, 2),
                    ("湛星象师", 2, 3), ("湛星象师·观", 3, 3)]:
        specs.append(C(n, p, l, [], [("汲智", "登场：抽一张牌", [atom("DrawCard", 1)])]))
    # 警戒卫（不横置攻击）
    for n, p, l in [("湛警戒卫", 2, 4), ("湛警戒卫·稳", 2, 4), ("湛哨卫", 3, 4), ("湛哨卫·坚", 3, 4),
                    ("湛壁垒卫", 3, 5), ("湛壁垒卫·牢", 3, 5)]:
        specs.append(C(n, p, l, ["Vigilance"]))
    # 法盾卫
    for n, p, l in [("湛法盾卫", 2, 3), ("湛法盾卫·御", 2, 3), ("湛咒盾手", 3, 4), ("湛咒盾手·护", 3, 4)]:
        specs.append(C(n, p, l, ["SpellShield"]))
    # 潜行刺客（单方面输出）
    for n, p, l in [("湛潜影刺客", 3, 2), ("湛潜影刺客·匿", 3, 2), ("湛暗流杀手", 4, 3), ("湛暗流杀手·影", 4, 3)]:
        specs.append(C(n, p, l, ["Stealth"]))
    # 辟邪守望
    for n in ["湛辟邪守望", "湛辟邪守望·遥"]:
        specs.append(C(n, 4, 5, ["Untargetable"]))
    # 时空领主（大收尾）
    for n in ["湛时空领主", "湛时空领主·恒"]:
        specs.append(C(n, 5, 6, ["Vigilance", "SpellShield"]))
    assert len(specs) == 45, len(specs)
    return specs


def blue_spells():
    specs = []
    for n in ["湛回手术", "湛回手术·速", "湛强制回流"]:
        specs.append(S(n, [("回流", "将一个生物移回其拥有者手牌", [atom("ReturnToHand", 1)])]))
    for n in ["湛顶回咒", "湛顶回咒·深"]:
        specs.append(S(n, [("顶回", "将一个生物移回其拥有者牌库顶", [atom("BounceToTop", 1)])]))
    for n in ["湛冰封", "湛冰封·锁", "湛寒冰牢"]:
        specs.append(S(n, [("冰封", "冻结一个敌方生物", [atom("FreezePermanent", 1)])]))
    specs.append(S("湛定身咒", [("定身", "横置一个敌方生物", [atom("Tap", 1)])]))
    for n in ["湛汲智", "湛汲智·渊", "湛灵感潮"]:
        specs.append(S(n, [("汲智", "抽两张牌", [atom("DrawCard", 2)])]))
    for n in ["湛微光检索", "湛回响阅读"]:
        specs.append(S(n, [("检索", "抽一张牌", [atom("DrawCard", 1)])]))
    specs.append(S("湛封口", [("封口", "沉默一个生物", [atom("Silence", 1)])]))
    assert len(specs) == 15, len(specs)
    return specs


# ============================================================ 绿：成长 + 恢复 + 大生物 ============================================================

def green_creatures():
    specs = []
    # 成长苗（Growth 关键词：随时间长大）
    for n, p, l in [("翠嫩芽", 1, 2), ("翠嫩芽·青", 1, 2), ("翠藤苗", 2, 2), ("翠藤苗·伸", 2, 2),
                    ("翠生长期", 2, 3), ("翠繁茂苗", 2, 3)]:
        specs.append(C(n, p, l, ["Growth"]))
    # 治愈者（登场回复英雄）
    for n, p, l, v in [("翠治愈者", 1, 3, 2), ("翠治愈者·泽", 1, 3, 2), ("翠甘霖祭司", 2, 3, 3),
                       ("翠甘霖祭司·霖", 2, 3, 3), ("翠生命守望", 3, 4, 4)]:
        specs.append(C(n, p, l, [], [("治愈", f"登场：回复己方英雄 {v} 点生命", [atom("Heal", v, tto=OWNER)])]))
    # 增殖者（全体友方 +1/+1 指示物）
    for n, p, l in [("翠增殖者", 2, 2), ("翠增殖者·衍", 2, 2), ("翠播种人", 2, 3),
                    ("翠播种人·撒", 2, 3), ("翠繁荣使者", 3, 3)]:
        specs.append(C(n, p, l, [], [("增殖", "登场：全体友方生物各获得一个 +1/+1 指示物", [atom("AddPlusOne", 1, tto=ALL_ALLIES)])]))
    # 壁垒（嘲讽）
    for n, p, l in [("翠古木壁", 1, 5), ("翠古木壁·厚", 1, 5), ("翠藤墙", 2, 5), ("翠藤墙·密", 2, 5), ("翠巨石守", 2, 6)]:
        specs.append(C(n, p, l, ["Taunt"]))
    # 护甲体（效果版 + 关键词版）
    for n, p, l in [("翠岩壳兽", 2, 4), ("翠岩壳兽·坚", 2, 4), ("翠晶甲虫", 3, 4)]:
        specs.append(C(n, p, l, [], [("岩壳", "登场：己方英雄获得 2 点护甲", [atom("AddArmor", 2, tto=OWNER)])]))
    for n, p, l in [("翠硬壳龟", 3, 4), ("翠硬壳龟·固", 3, 4), ("翠甲胄兽", 4, 4)]:
        specs.append(C(n, p, l, ["Armor"]))
    # 系命者
    for n in ["翠系命藤", "翠系命藤·缠"]:
        specs.append(C(n, 3, 3, ["Lifesteal"]))
    for n in ["翠生命相连", "翠生命相连·系"]:
        specs.append(C(n, 3, 4, ["Lifelink"]))
    # 再生 / 复生
    for n in ["翠再生蜥", "翠再生蜥·活"]:
        specs.append(C(n, 3, 4, ["Regeneration"]))
    for n in ["翠轮回芽", "翠轮回芽·归"]:
        specs.append(C(n, 4, 4, ["Reborn"]))
    # 巨兽（白板大体型）
    for n, p, l in [("翠巨熊", 5, 6), ("翠巨蟒", 6, 6), ("翠古树灵", 6, 7), ("翠山岳兽", 7, 7), ("翠太古巨木", 7, 8)]:
        specs.append(C(n, p, l))
    # 圣盾守卫
    for n in ["翠圣盾卫", "翠圣盾卫·辉"]:
        specs.append(C(n, 3, 4, ["DivineShield"]))
    # 苏生祭
    for n in ["翠苏生祭司", "翠苏生祭司·唤"]:
        specs.append(C(n, 3, 3, [], [("苏生", "登场：从墓地复苏一个生物", [atom("ReturnFromGraveyard", 1)])]))
    # 碾压巨木
    specs.append(C("翠碾压巨木", 6, 6, ["Overwhelm"]))
    assert len(specs) == 45, len(specs)
    return specs


def green_spells():
    specs = []
    for n, v in [("翠治愈浪潮", 4), ("翠治愈浪潮·沛", 5), ("翠生命之泉", 6)]:
        specs.append(S(n, [("治愈", f"回复己方英雄 {v} 点生命", [atom("Heal", v, tto=OWNER)])]))
    for n in ["翠成长赐福", "翠成长赐福·沃", "翠繁荣仪式"]:
        specs.append(S(n, [("赐福", "全体友方生物各获得一个 +1/+1 指示物", [atom("AddPlusOne", 1, tto=ALL_ALLIES)])]))
    for n, v in [("翠巨力术", 2), ("翠巨力术·壮", 3)]:
        specs.append(S(n, [("巨力", f"全体友方生物攻击力 +{v}、生命值 +{v}",
                        [atom("AddPowerUp", v, tto=ALL_ALLIES), atom("AddLifeUp", v, tto=ALL_ALLIES)])]))
    for n in ["翠岩甲术", "翠岩甲术·厚"]:
        specs.append(S(n, [("岩甲", "己方英雄获得 3 点护甲", [atom("AddArmor", 3, tto=OWNER)])]))
    for n in ["翠光合", "翠光合·盛"]:
        specs.append(S(n, [("光合", "获得元素加速", [atom("Photosynthesis", 1)])]))
    specs.append(S("翠采掘", [("采掘", "从元素池采掘元素", [atom("Mine", 1)])]))
    # （回收 RecoverToHand 0.5 基价×1 取整为零费，换成有价成长/护甲——墓地回收由苏生祭司承担）
    specs.append(S("翠成长赐福·泽", [("赐福", "全体友方生物各获得一个 +1/+1 指示物", [atom("AddPlusOne", 1, tto=ALL_ALLIES)])]))
    specs.append(S("翠岩甲术·固", [("岩甲", "己方英雄获得 3 点护甲", [atom("AddArmor", 3, tto=OWNER)])]))
    assert len(specs) == 15, len(specs)
    return specs


# ============================================================ 输出 + 自检 ============================================================

KNOWN_ATOMS = {
    a["EffectType"] for a in json.load(open(
        os.path.join(OUT_DIR, "..", "AttributeValueConfig.json"), encoding="utf-8"))
}
KNOWN_KEYWORDS = {"Taunt", "DoubleStrike", "PoisonSting", "Lifesteal", "Lifelink", "Stealth",
                  "DivineShield", "Overwhelm", "Armor", "Vigilance", "FirstStrike", "SpellShield",
                  "Untargetable", "Reborn", "Indestructible", "Regeneration", "Growth", "Disarm"}


def validate(decks):
    for color, deck in decks.items():
        cards = deck["cards"]
        ids = [c["id"] for c in cards]
        assert len(ids) == len(set(ids)) == 60
        names = [c["cardName"] for c in cards]
        assert len(names) == len(set(names)), f"{color} 卡名重复"
        n_creature = sum(1 for c in cards if c["supertype"] == "Creature")
        assert n_creature == 45, f"{color} 生物 {n_creature} != 45（法术 {60 - n_creature}）"
        for c in cards:
            for kw in c["keywords"]:
                assert kw in KNOWN_KEYWORDS, f"{c['id']} 未知关键词 {kw}"
            for e in c["effects"]:
                assert e["TriggerTiming"] == 0 and not e["Costs"] and not e["Steps"]
                for a in e["AtomicEffects"]:
                    assert a["EffectType"] in KNOWN_ATOMS, f"{c['id']} 未知原子 {a['EffectType']}"
                    assert a["TargetTypeOverride"] in (-1, SELF, ALL_ALLIES, OWNER, OPPONENT)
        print(f"[{color}] 60 张（45 生物 + 15 法术）✔ 原子/关键词全部在表 ✔")


def export_training_sample(n_per_color=20, seed=20260909):
    """验证阶段扩池：从三套主题卡组各抽 n 张追加进 TestCreatureCards.json 参与训练。

    导入副本加 tag "deck-import"：既作幂等重导标记（剔除含该 tag 的卡再追加），
    也进内容 ID 哈希（Tools/重推导测试卡表费用与ID 之后，副本 ID = hash(内容+tag)，
    与 Deck_*.json 原卡天然区分）。跑完本函数记得执行一次重推导工具统一费用与 ID。
    固定 seed：抽取结果可复现（换抽取重跑用 --reseed N）。
    """
    rng = random.Random(seed + (int(sys.argv[2]) if len(sys.argv) > 1 and sys.argv[1] == "--reseed" else 0))
    tc_path = os.path.normpath(os.path.join(OUT_DIR, "TestCreatureCards.json"))
    data = json.load(open(tc_path, encoding="utf-8"))
    kept = [c for c in data["cards"] if "deck-import" not in (c.get("tags") or [])]
    added = []
    for color in ("Red", "Blue", "Green"):
        pool = json.load(open(os.path.normpath(os.path.join(OUT_DIR, f"Deck_{color}.json")),
                              encoding="utf-8"))["cards"]
        picked = rng.sample(pool, n_per_color)
        for c in picked:
            copy = json.loads(json.dumps(c, ensure_ascii=False))
            copy["tags"] = sorted(set((copy.get("tags") or []) + ["deck-import"]))
            added.append(copy)
        print(f"[扩池] {color} 抽 {n_per_color}: {[c['cardName'] for c in picked][:5]}...")
    data["cards"] = kept + added
    with open(tc_path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    print(f"[扩池] TestCreatureCards: {len(kept)} 原有 + {len(added)} 导入 = {len(data['cards'])} 张 → {tc_path}")


def main():
    if len(sys.argv) > 1 and sys.argv[1] in ("--export-training-sample", "--reseed"):
        export_training_sample()
        return
    decks = {
        "Red": build_deck("RED", "红", red_creatures() + red_spells()),
        "Blue": build_deck("BLUE", "蓝", blue_creatures() + blue_spells()),
        "Green": build_deck("GREEN", "绿", green_creatures() + green_spells()),
    }
    validate(decks)
    for color, deck in decks.items():
        path = os.path.normpath(os.path.join(OUT_DIR, f"Deck_{color}.json"))
        with open(path, "w", encoding="utf-8") as f:
            json.dump(deck, f, ensure_ascii=False, indent=1)
        print(f"已写出 {path}")


if __name__ == "__main__":
    main()
