#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
M1 目标域模型 — 配置数据迁移（2026-09-10 重构 P2）

分阶段执行（--stage）：
  derive   读现行 AttributeValueConfig.json（真相源）按旧→新映射推导每行
           TargetKinds/TargetFilter(属性 tokens)/SelectionMode → derived_schema.json + target_kinds_review.csv
  cards    读 derived_schema + TestDecks/*.json → 原子删 11 旧字段/override 折算 TargetKinds/
           组合层回填（Duration/SummonDropZone/SelectionMode）→ 改写 JSON + migration_report.json
  write    反写 Attribute.xlsm 三 sheet（新 schema）+ __enums__.xlsx 补 TriggerTiming/TargetKind/SelectionMode
  verify   加载计数锁（83 表行/278 卡/184 原子）+ 新 schema 键集断言 + 拆分行数守恒

映射定案见仓库根 网络协议.md 同级的重构计划；要点：
  Target+Creature        → Manual {0,1} filter "Creature"（角色仍被排除——CardTypeFilter 天然只过卡）
  Target+Creature,Player → Manual {0,1} filter ""（作者意图含角色；Player 语义修复为 kind 层）
  Target+Player          → Manual {0,1} filter "Player"（Player 升级为真过滤=仅角色，行为修复点）
  Target+Hand/Activation/Graveyard/ElementPool → Manual {4,5}/{14,15}/{8,9}/{12,13}
  Target+NoLife          → Manual {2,3,12,13}（旧摧毁域跨域）
  Target+Creature,Friendly[,Tapped] → Manual {0} filter Creature[,Tapped]
  Target+Creature,Friendly,ElementPool → Manual {12} filter Creature
  None                   → None 空集（无目标，不参与交集）
  Opponent               → Full {1} filter Player
  Self                   → Self {0}
"""

import argparse
import csv
import json
import io
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent          # E:\UnityProject\Tide
CONFIG = ROOT / "Config"
ASSET_CFG = ROOT / "Assets" / "Configs"
DECKS = ASSET_CFG / "TestDecks"

ZONE_OF_TOKEN = {
    "Hand": (4, 5), "Deck": (6, 7), "Graveyard": (8, 9), "Exile": (10, 11),
    "ElementPool": (12, 13), "Activation": (14, 15),
}
UNIT_KINDS = (0, 1)            # 有生命单位（生物+角色）——Creature 族默认
NONLIFE_KINDS = (2, 3, 12, 13) # 旧 NoLife 摧毁域：无生命单位+元素池地牌
ZONE_TOKENS = set(ZONE_OF_TOKEN)
DURATION_ALIAS = {"Instant": "Once", "UntilCondition": "WhileCondition", "UntilEndOfPhase": "UntilEndOfTurn"}


def load_json(path):
    return json.load(io.open(path, encoding="utf-8"))


def save_json(path, data):
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)


# --------------------------------------------------------------- derive

def derive_row(row):
    tt = str(row.get("TargetType") or "None")
    tf = str(row.get("TargetFilter") or "")
    tokens = [t.strip() for t in tf.split(",") if t.strip() and t.strip().lower() != "none"]

    zone_pair = None
    attr = []
    friendly = nolife = has_player = has_creature = False
    for t in tokens:
        if t in ZONE_TOKENS:
            zone_pair = ZONE_OF_TOKEN[t]
        elif t == "Friendly":
            friendly = True
        elif t == "Enemy":
            pass  # 表内无 Enemy 行，防御性忽略
        elif t == "NoLife":
            nolife = True
        elif t == "Player":
            has_player = True
        else:
            attr.append(t)
        if t == "Creature":
            has_creature = True

    notes = []
    if nolife:
        kinds = list(NONLIFE_KINDS)
    elif zone_pair is not None:
        kinds = list(zone_pair)
    else:
        kinds = list(UNIT_KINDS)

    # 归属收窄（单位域 / 卡域同规则：Friendly=己方侧）
    if friendly:
        kinds = [k for k in kinds if k % 2 == 0]

    # Creature 属性语义：保留 = 排除角色；Creature,Player 同现 = 作者意图含角色 → 去掉 Creature
    if has_creature and has_player:
        attr = [t for t in attr if t != "Creature"]
        notes.append("Creature+Player 同现：按作者意图含角色（去 Creature 过滤）")
    elif has_creature:
        pass  # 保留 Creature（角色仍被排除，保行为）
    elif has_player:
        attr.append("Player")  # Player 升级为真过滤（仅角色）——行为修复点
        notes.append("Player 过滤语义修复：旧 no-op → 仅角色")

    mode = {"Target": "Manual", "None": "None", "Self": "Self", "Opponent": "Full",
            "All": "Full", "AllEnemies": "Full", "AllAllies": "Full", "Random": "Full",
            "Owner": "Full", "Controller": "Full"}.get(tt, "Manual")
    if tt == "Self" and kinds == list(UNIT_KINDS):
        kinds = [0]
    if tt == "Opponent":
        kinds = [1]
        attr = [t for t in attr if t != "Creature"] + (["Player"] if "Player" not in attr else [])
        notes.append("Opponent 行：域=对方有生命单位 + Player 过滤（保旧行为=仅对方角色）")

    return {
        "EffectType": row.get("EffectType"),
        "EnumName": row.get("EnumName"),
        "Old": {"TargetType": tt, "TargetFilter": tf,
                "DurationType": row.get("DurationType"), "Turns": row.get("Turns"),
                "TargetCount": row.get("TargetCount")},
        "TargetKinds": sorted(set(kinds)),
        "Filter": ",".join(attr),
        "SelectionMode": mode,
        "Notes": "; ".join(notes),
    }


# ---- 极性初值（2026-09-10 定案 proposal，复核 CSV 里过目；0 组标注可核改）----
POLARITY_NEG = {"DealDamage","DealCombatDamage","LifeLoss","PierceDamage","DrainLife","Poison","AddToxin",
    "FreezePermanent","Weaken","Smash","Tap","MillCard","DiscardCard","Exile","BounceToTop","BounceToBottom",
    "ReturnToHand","GainControl","NegateActivation","Silence","RedirectTarget","KnockDown","Devour","Annihilate",
    "AddVulnerable","AddPowerDown","AddLifeDown","AddMinusOne","RushSickness","SkipTurn"}
POLARITY_POS = {"Heal","Inspire","Untap","DrawCard","SearchDeck","SummonToken","AddArmor","AddPowerUp",
    "AddLifeUp","AddPlusOne","TakeExtraTurn","RecoverToHand","ReturnFromGraveyard","Photosynthesis"}
# Grant* 一律 +1；其余 0（信息族/Morph/Scry/Modify*/Set*/Sacrifice/Purify 等）

def derive_polarity(effect_type):
    if effect_type in POLARITY_NEG: return -1.0
    if effect_type in POLARITY_POS: return 1.0
    if effect_type and effect_type.startswith("Grant"): return 1.0
    return 0.0


def stage_derive():
    rows = load_json(ASSET_CFG / "AttributeValueConfig.json")
    # 幂等：新 schema 行（已含 TargetKinds/SelectionMode）直通保留，仅补极性；旧 schema 行走推导
    if rows and "TargetKinds" in rows[0]:
        derived = []
        for r in rows:
            kinds = [int(x) for x in str(r.get("TargetKinds") or "").split(",") if x.strip()]
            derived.append({
                "EffectType": r.get("EffectType"), "EnumName": r.get("EnumName"),
                "Old": {"TargetType": "(已迁移)", "TargetFilter": r.get("TargetFilter") or "",
                        "DurationType": "(已上移)", "Turns": None, "TargetCount": r.get("TargetCount")},
                "TargetKinds": sorted(set(kinds)),
                "Filter": r.get("TargetFilter") or "",
                "SelectionMode": r.get("SelectionMode") or "None",
                "Polarity": derive_polarity(r.get("EffectType")),
                "Notes": "幂等重推（极性补值）",
            })
    else:
        derived = []
        for r in rows:
            d = derive_row(r)
            d["Polarity"] = derive_polarity(r.get("EffectType"))
            derived.append(d)
    save_json(CONFIG / "derived_schema.json", derived)

    with io.open(CONFIG / "target_kinds_review.csv", "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f)
        w.writerow(["EffectType", "EnumName", "旧TargetType", "旧TargetFilter", "旧DurationType", "旧Turns",
                    "新TargetKinds", "新Filter", "新SelectionMode", "Polarity", "备注"])
        for d in derived:
            w.writerow([d["EffectType"], d["EnumName"], d["Old"]["TargetType"], d["Old"]["TargetFilter"],
                        d["Old"]["DurationType"], d["Old"]["Turns"],
                        ",".join(map(str, d["TargetKinds"])) or "(空=无目标)",
                        d["Filter"] or "(空)", d["SelectionMode"], d["Polarity"], d["Notes"]])
    print(f"derive: {len(derived)} 行 → derived_schema.json + target_kinds_review.csv")


# --------------------------------------------------------------- cards

OLD_ATOM_KEYS = ["Value2", "ManaTypeParam", "ZoneParam", "Duration", "DurationValue",
                 "TargetTypeOverride", "TargetFilterOverride", "TargetCountOverride",
                 "TargetScopeOverride", "DynamicTargetCount", "Drawbacks"]


def parse_duration_enum():
    src = io.open(ROOT / "Assets/Scripts/CardCore/Enums/Zones.cs", encoding="utf-8").read()
    m = re.search(r"enum DurationType(.*?)\n    \}", src, re.S)
    out, implicit = {}, 0
    for hit in re.finditer(r"^\s*(\w+)(?:\s*=\s*(-?\d+))?,", m.group(1), re.M):
        out[hit.group(1)] = int(hit.group(2)) if hit.group(2) is not None else implicit
        implicit = out[hit.group(1)] + 1
    if not out:
        raise SystemExit("DurationType 解析失败（Zones.cs 结构变化？）")
    return out


def override_kinds(tto):
    """原子级 TargetTypeOverride 折算（旧值→种类集；Self 的模式贡献由上层处理）。"""
    return {"Self": [0], "AllAllies": [0], "Owner": [0], "Controller": [0],
            "Opponent": [1], "AllEnemies": [1], "All": [0, 1], "Random": [0, 1]}.get(tto)


def walk_steps(steps, fn):
    for st in steps or []:
        if st.get("atomic") is not None:
            fn(st["atomic"])
        for key in ("thenSteps", "elseSteps"):
            for a in st.get(key) or []:
                fn(a)
        for ch in st.get("choices") or []:
            walk_steps(ch.get("steps"), fn)


def stage_cards():
    derived = {d["EffectType"]: d for d in load_json(CONFIG / "derived_schema.json")}
    dur_enum = parse_duration_enum()
    report = {"files": {}, "atoms_total": 0, "effects_total": 0,
              "special": [], "duration_conflicts": [], "schema_errors": []}

    for path in sorted(DECKS.glob("*.json")):
        data = load_json(path)
        cards = data if isinstance(data, list) else data.get("cards")
        fstat = {"cards": len(cards), "atoms": 0, "effects": 0, "self_overrides": 0, "mode_conflicts": 0}

        for card in cards:
            for eff in card.get("effects") or []:
                fstat["effects"] += 1
                atoms = []

                def collect(atom):
                    atoms.append(atom)

                for a in eff.get("AtomicEffects") or []:
                    collect(a)
                walk_steps(eff.get("Steps"), collect)

                # ---- 原子层改写 ----
                kind_atom_count, self_kind_count, first_mode = 0, 0, "None"
                for atom in atoms:
                    if not atom.get("EffectType"):
                        report["special"].append(f"{path.name}/{card.get('cardName')}: 空 EffectType 原子（converter 将跳过）")
                    d = derived.get(atom.get("EffectType"))
                    if d and d["SelectionMode"] != "None":
                        if first_mode == "None":
                            first_mode = d["SelectionMode"]

                    tto = atom.get("TargetTypeOverride", -1)
                    if isinstance(tto, str):
                        tto = {"Self": 0, "Target": 2, "AllEnemies": 3, "AllAllies": 4, "All": 5,
                               "Random": 6, "Owner": 7, "Controller": 8, "Opponent": 9}.get(tto, -1)
                    if tto is None:
                        tto = -1
                    eff_kinds = None
                    if 0 <= tto:
                        names = ["Self", "Target", "AllEnemies", "AllAllies", "All", "Random", "Owner", "Controller", "Opponent", "None"]
                        eff_kinds = override_kinds(names[tto] if tto < len(names) else None)
                    elif d and d["TargetKinds"]:
                        eff_kinds = d["TargetKinds"]
                    if eff_kinds:
                        kind_atom_count += 1
                        if 0 <= tto and names[tto] == "Self":
                            self_kind_count += 1
                        if 0 <= tto:  # 仅 override 才写原子级收窄（null=用表默认）
                            atom["TargetKinds"] = eff_kinds
                    for k in OLD_ATOM_KEYS:
                        atom.pop(k, None)
                    if "StringValue" in atom:  # 键名随用户改名对齐
                        atom["ID"] = atom.pop("StringValue")
                    fstat["atoms"] += 1

                # ---- 组合层回填 ----
                durations, turns_value = [], 0
                for atom in atoms:
                    d = derived.get(atom.get("EffectType"))
                    if not d:
                        continue
                    dt = DURATION_ALIAS.get(d["Old"]["DurationType"], d["Old"]["DurationType"]) or "Once"
                    if dt == "ForTurns" and d["Old"].get("Turns"):
                        try:
                            turns_value = int(d["Old"]["Turns"])
                        except (TypeError, ValueError):
                            pass
                    durations.append(dt)
                distinct = [x for x in dict.fromkeys(durations)]
                if len(distinct) > 1:
                    pick = next((x for x in distinct if x != "Once"), distinct[0])
                    report["duration_conflicts"].append(
                        f"{path.name}/{card.get('cardName')}/{eff.get('Id') or eff.get('DisplayName')}: {distinct} → 取 {pick}")
                else:
                    pick = distinct[0] if distinct else "Once"
                eff["Duration"] = dur_enum.get(pick, dur_enum.get("Once", 0))
                eff["DurationValue"] = turns_value if pick == "ForTurns" else 0

                # SummonDropZone：现存数据全默认（audit 证实 ZoneParam 零使用）→ 统一 Battlefield(=1)
                eff["SummonDropZone"] = 1

                if kind_atom_count == 0:
                    eff["SelectionMode"] = -1       # None（无目标原子）
                elif self_kind_count == kind_atom_count:
                    eff["SelectionMode"] = 0        # Self：全部带域原子都是 Self → 保旧行为（目标=源卡）
                elif self_kind_count > 0:
                    fstat["mode_conflicts"] += 1
                    eff["SelectionMode"] = 1 if first_mode == "Manual" else 2
                    report["special"].append(
                        f"{path.name}/{card.get('cardName')}/{eff.get('Id')}: Self 与非 Self 原子混排 → 效果级模式取 {first_mode}（行为面，人工过目）")
                else:
                    eff["SelectionMode"] = {"Manual": 1, "Full": 2, "Self": 0}.get(first_mode, 1)
                eff["TargetCount"] = -2
                eff["DynamicTargetCount"] = False
                eff.setdefault("Drawbacks", [])

        save_json(path, data)
        report["files"][path.name] = fstat
        report["atoms_total"] += fstat["atoms"]
        report["effects_total"] += fstat["effects"]

    save_json(CONFIG / "migration_report.json", report)
    print(f"cards: {report['atoms_total']} 原子 / {report['effects_total']} 效果 已迁移 → migration_report.json")
    for s in report["special"][:10]:
        print("  [special]", s)
    for s in report["duration_conflicts"][:10]:
        print("  [duration]", s)


# --------------------------------------------------------------- write xlsm / enums

def parse_cs_enum(path, name):
    src = io.open(path, encoding="utf-8").read()
    m = re.search(rf"enum {name}\b(.*?)\n    \}}", src, re.S)
    out = []
    for hit in re.finditer(r"///\s*<summary>(.*?)</summary>\s*\n\s*(\w+)\s*=\s*(-?\d+)", m.group(1), re.S):
        out.append((hit.group(2), int(hit.group(3)), " ".join(hit.group(1).split())))
    if not out:  # 无注释格式兜底（含隐式编号）
        implicit = 0
        for hit in re.finditer(r"^\s*(\w+)(?:\s*=\s*(-?\d+))?,", m.group(1), re.M):
            val = int(hit.group(2)) if hit.group(2) is not None else implicit
            out.append((hit.group(1), val, ""))
            implicit = val + 1
    return out


def write_sheet(ws, columns, types, rows):
    for c, (name, typ) in enumerate(zip(columns, types), start=1):
        ws.cell(1, c, name)
        ws.cell(2, c, typ)
    for r, row in enumerate(rows, start=3):
        for c, v in enumerate(row, start=1):
            ws.cell(r, c, v)


def stage_write():
    import openpyxl

    derived = load_json(CONFIG / "derived_schema.json")
    old_rows = load_json(ASSET_CFG / "AttributeValueConfig.json")
    vs_rows = load_json(ASSET_CFG / "ValueSystemConfig.json")
    co_rows = load_json(ASSET_CFG / "CostOffsetConfig.json")

    # ---- Attribute.xlsm 三 sheet 重写 ----
    wb = openpyxl.load_workbook(CONFIG / "Attribute.xlsm", keep_vba=True)

    ws = wb["AttributeValueConfig"]
    ws.delete_rows(1, ws.max_row + 1)
    # 2026-09-10 编排列删除定案：EffectTier（零消费）/TargetCount/SelectionMode/ActivationType（组合层声明）
    columns = ["ID", "EnumName", "DisplayName", "EffectFunction", "EffectColor", "BaseCost", "EffectType",
               "TargetKinds", "TargetFilter", "Polarity"]
    types = ["string", "string", "string", "enum", "enum", "float", "string",
             "string", "string", "float"]
    dmap = {d["EffectType"]: d for d in derived}
    rows = []
    for r in old_rows:
        d = dmap[r["EffectType"]]
        rows.append([r.get("ID"), r.get("EnumName"), r.get("DisplayName"), r.get("EffectFunction"),
                     r.get("EffectColor"), r.get("BaseCost"), r.get("EffectType"),
                     ",".join(map(str, d["TargetKinds"])) or "", d["Filter"], d["Polarity"]])
    write_sheet(ws, columns, types, rows)

    ws = wb["ValueSystemConfig"]
    ws.delete_rows(1, ws.max_row + 1)
    # 2026-09-10 收窄定案：CardCost（当量）留 CostOffsetConfig；CardComposition（组合计价）回归本表；
    # OpponentDrawValue/OpponentHealValuePerPoint 随机制删除（被 Polarity 错边折价顶替）。
    KEEP_IN_CO = {"CardCost"}
    DELETED_KV = {"OpponentDrawValue", "OpponentHealValuePerPoint"}
    DELETED_MECH = {"OpponentHeal", "OpponentDraw"}
    vs_keep = [r for r in vs_rows if r["Category"] not in KEEP_IN_CO and r["Category"] != "CardComposition"]
    # CardComposition 回归源：本表既有（重跑容错）∪ CO 现有 KV
    comp_back, comp_keys = [], set()  # 文件内也去重（防历史重复行固化）
    for r in vs_rows:
        if r["Category"] == "CardComposition" and (r["Category"], r["Key"]) not in comp_keys:
            comp_back.append(r); comp_keys.add((r["Category"], r["Key"]))
    for r in co_rows:
        if r.get("Category") == "CardComposition" and (r["Category"], r["Key"]) not in comp_keys:
            comp_back.append(r)
            comp_keys.add((r["Category"], r["Key"]))
    keep = vs_keep + comp_back
    write_sheet(ws, ["Category", "Key", "Value", "Des"], ["string", "string", "float", "string"],
                [[r["Category"], r["Key"], r["Value"], r.get("Des", "")] for r in keep])

    ws = wb["CostOffsetConfig"]
    ws.delete_rows(1, ws.max_row + 1)
    cols = ["Mechanism", "DisplayName", "ResourcePerOffset", "MaxOffsetPerGame",
            "Category", "Key", "Value", "Des"]
    typs = ["enum", "string", "int", "int", "string", "string", "float", "string"]
    rows = [[r["Mechanism"], r["DisplayName"], r["ResourcePerOffset"], r["MaxOffsetPerGame"],
             None, None, None, None] for r in co_rows
            if r.get("Mechanism") and r["Mechanism"] not in DELETED_MECH]
    # KV 行（幂等）：现有 CO 的 CardCost KV 继承 + VS 里未拆分的并入；删项跳过
    kv = {}
    for r in co_rows:
        if r.get("Category") in KEEP_IN_CO and r.get("Key") not in DELETED_KV:
            kv[(r["Category"], r["Key"])] = r
    for r in vs_rows:
        if r.get("Category") in KEEP_IN_CO and r.get("Key") not in DELETED_KV:
            kv.setdefault((r["Category"], r["Key"]), r)
    rows += [[None, None, None, None, r["Category"], r["Key"], r["Value"], r.get("Des", "")]
             for r in kv.values()]
    write_sheet(ws, cols, typs, rows)
    wb.save(CONFIG / "Attribute.xlsm")
    print(f"write: Attribute.xlsm 三 sheet 重写（AVC {len(old_rows)} 行 / VS {len(keep)} 行 / CO {len(rows)} 行）")

    # ---- __enums__.xlsx 补枚举表 ----
    wb = openpyxl.load_workbook(CONFIG / "__enums__.xlsx")
    ws = wb["Sheet1"]
    existing = {ws.cell(r, 1).value for r in range(3, ws.max_row + 1)}
    plans = [
        ("TriggerTiming", "触发时机（时点系统重排后全集）",
         ROOT / "Assets/Scripts/CardCore/Effects/TriggerTiming.cs", "TriggerTiming"),
        ("TargetKind", "目标种类（M1 目标域模型）",
         ROOT / "Assets/Scripts/CardCore/Effects/TargetKind.cs", "TargetKind"),
        ("SelectionMode", "组合层目标选择模式",
         ROOT / "Assets/Scripts/CardCore/Effects/TargetKind.cs", "SelectionMode"),
    ]
    r = ws.max_row + 1
    for full, comment, path, name in plans:
        if full in existing:
            continue
        for val, num, des in parse_cs_enum(path, name):
            ws.cell(r, 1, full)
            ws.cell(r, 2, comment)
            ws.cell(r, 3, val)
            ws.cell(r, 4, des or val)
            r += 1
    wb.save(CONFIG / "__enums__.xlsx")
    print(f"write: __enums__.xlsx 枚举表补充至第 {r - 1} 行")


# --------------------------------------------------------------- verify

def stage_verify():
    problems = []

    total_atoms = total_effects = total_cards = 0
    for path in sorted(DECKS.glob("*.json")):
        data = load_json(path)
        cards = data if isinstance(data, list) else data.get("cards")
        total_cards += len(cards)
        for card in cards:
            for eff in card.get("effects") or []:
                total_effects += 1
                for key in ("Duration", "DurationValue", "SummonDropZone", "SelectionMode",
                            "TargetCount", "DynamicTargetCount", "Drawbacks"):
                    if key not in eff:
                        problems.append(f"{path.name}: 效果缺组合层字段 {key}")
                atoms = list(eff.get("AtomicEffects") or [])
                walk_steps(eff.get("Steps"), atoms.append)
                for a in atoms:
                    total_atoms += 1
                    for k in OLD_ATOM_KEYS:
                        if k in a:
                            problems.append(f"{path.name}: 原子残留旧字段 {k}")
                    if "StringValue" in a:
                        problems.append(f"{path.name}: 原子残留旧键 StringValue")

    avc = load_json(ASSET_CFG / "AttributeValueConfig.json")
    for r in avc:
        for k in ("TargetType", "TargetScope", "DurationType", "Turns", "EffectTier", "TargetCount", "SelectionMode", "ActivationType"):
            if k in r:
                problems.append(f"AttributeValueConfig: 残留旧列 {k}")
        # TargetFilter token 白名单（枚举目录 __enums__.xlsx TargetFilter；比较式 Power>N/Life<=N 另算）
        _tf = r.get("TargetFilter") or ""
        if _tf.strip():
            _valid = {"NoRole","Mortal","Stealth","Untargetable","Tapped","Untapped","Damaged","Friendly","Enemy"}
            for _tok in [t.strip() for t in _tf.split(",") if t.strip()]:
                if "=" not in _tok and _tok not in _valid:
                    problems.append(f"AttributeValueConfig: {r.get('EffectType')} 未知 TargetFilter token '{_tok}'（查 __enums__ 目录）")
        pol = r.get("Polarity")
        if pol is None or not (-1.0 <= float(pol) <= 1.0):
            problems.append(f"AttributeValueConfig: {r.get('EffectType')} Polarity 越界/缺失 = {pol}")

    # TargetCount 回填锁：组合层不再有 -2 哨兵（表级回落已删，-2 会退化成"全选"）
    for path in sorted(DECKS.glob("*.json")):
        data2 = load_json(path)
        cards2 = data2 if isinstance(data2, list) else data2.get("cards")
        for card in cards2:
            for eff in card.get("effects") or []:
                if eff.get("TargetCount") == -2:
                    problems.append(f"{path.name}: 效果 TargetCount 仍为 -2 哨兵（未回填）")

    print(f"verify: 卡 {total_cards} / 效果 {total_effects} / 原子 {total_atoms}")
    if total_atoms != 184:
        problems.append(f"原子计数锁失败：{total_atoms} != 184")
    if len(avc) != 83:
        problems.append(f"表行计数锁失败：{len(avc)} != 83")

    vs = load_json(ASSET_CFG / "ValueSystemConfig.json")
    co = load_json(ASSET_CFG / "CostOffsetConfig.json")
    kv_cardcost = sum(1 for r in co if r.get("Category") == "CardCost")
    mech = sum(1 for r in co if r.get("Mechanism"))
    # 2026-09-10 收窄定案：CO = 4 固有资源机制 + 9 CardCost 当量；VS = 52 乘法 + 5 CardComposition 回归 = 57；
    # 总行数 4+9+57 = 原 68 - 2 机制 - 2 当量（OpponentHeal/OpponentDraw 全链删除）
    if mech != 4 or kv_cardcost != 9:
        problems.append(f"CostOffset 结构异常：机制 {mech} 行（期望 4 固有资源）/ CardCost {kv_cardcost} 行（期望 9）")
    if len(vs) != 57:
        problems.append(f"ValueSystemConfig 行数 {len(vs)} != 57（52 乘法 + 5 CardComposition 回归）")
    for r in co:
        if r.get("Mechanism") in ("OpponentHeal", "OpponentDraw") or r.get("Key") in ("OpponentDrawValue", "OpponentHealValuePerPoint"):
            problems.append(f"已删机制残留：{r}")
    if any(r.get("Category") == "CardComposition" for r in co):
        problems.append("CardComposition 残留在 CostOffsetConfig（应已回归 ValueSystemConfig）")

    if problems:
        print(f"verify: {len(problems)} 个问题")
        for p in problems[:20]:
            print("  [FAIL]", p)
        sys.exit(1)
    print("verify: 全部通过 ✅")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", required=True,
                    choices=["derive", "cards", "write", "verify", "all"])
    args = ap.parse_args()
    stages = ["derive", "cards", "write", "verify"] if args.stage == "all" else [args.stage]
    for s in stages:
        globals()[f"stage_{s}"]()


if __name__ == "__main__":
    main()
