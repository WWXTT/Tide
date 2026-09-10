#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""校验：xlsm 回读 == JSON；原 83 行与备份一致；ID 唯一。"""
import hashlib
import json
import importlib.util
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent.resolve()

spec = importlib.util.spec_from_file_location("e", SCRIPT_DIR / "export_to_json.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
parsed = m.parse_excel_to_json(str(SCRIPT_DIR / "Attribute.xlsm"))

new = json.loads((SCRIPT_DIR.parent / "Assets" / "Configs" / "AttributeValueConfig.json").read_text(encoding="utf-8"))
old = json.loads((SCRIPT_DIR.parent / "Assets" / "Configs" / "AttributeValueConfig.json.bak".replace(".json.bak", "")).read_text(encoding="utf-8")) if False else json.loads(Path(str(SCRIPT_DIR.parent / "Assets" / "Configs" / "AttributeValueConfig.json") + ".bak").read_text(encoding="utf-8"))

got = parsed["AttributeValueConfig"]
print(f"JSON rows: {len(old)} -> {len(new)}; xlsm rows: {len(got)}")

# 1) xlsm round-trip
diff = 0
for i, (a, b) in enumerate(zip(new, got)):
    for k in a:
        if a[k] != b.get(k):
            print("DIFF", i, k, repr(a[k]), repr(b.get(k)))
            diff += 1
print("round-trip diffs:", diff, "| keys match:", all(a.keys() == b.keys() for a, b in zip(new, got)))

# 2) original 83 unchanged
print("first 83 unchanged:", new[:83] == old)

# 3) ID uniqueness & algorithm
ids = [r["ID"] for r in new]
assert len(ids) == len(set(ids)), "ID collision!"
bad = [r["ID"] for r in new if r["ID"] != hashlib.sha256(r["DisplayName"].strip().encode("utf-8")).hexdigest()[:8]]
print("ID unique:", len(ids) == len(set(ids)), "| ID=sha256(DisplayName)[:8] mismatches:", bad or "none")

# 4) new EffectTypes not in C# enum (expected: runtime skip warning until implemented)
ENUM = set("""DealDamage DealCombatDamage LifeLoss Heal PierceDamage DrainLife DrawCard DiscardCard MillCard ReturnToHand Exile ShuffleIntoDeck SearchDeck BounceToTop BounceToBottom Tap Untap ModifyPower ModifyLife SetPower SetLife SetCost FreezePermanent Purify Weaken Inspire Smash GainControl NegateActivation Silence RedirectTarget ScryCards GrantDoubleStrike GrantCannotBeTargeted GrantSpellShield Morph ModifyGameRule OverrideRestriction ReturnFromGraveyard RecoverToHand LookAtTopCards ChangeOwner ModifyCost GrantPoisonSting GrantLifesteal GrantStealth GrantTaunt GrantDivineShield GrantOverwhelm GrantArmor GrantFirstStrike GrantDisarm Photosynthesis GrantVigilance GrantRegeneration GrantGrowth TakeExtraTurn SkipTurn DeclareHand DeclareDeckTop DeclareArrow ProphecyNextCard GrantReborn GrantIndestructible GrantLifelink AddArmor AddToxin Poison RushSickness AddVulnerable AddPowerUp AddPowerDown AddLifeUp AddLifeDown AddPlusOne AddMinusOne AddCostUp AddCostDown Sacrifice Devour Annihilate KnockDown SummonToken Mine AddNullify""".split())
pending = [r["EffectType"] for r in new if r["EffectType"] not in ENUM]
print(f"EffectType in C# enum: {len(new) - len(pending)}/{len(new)}; 待实现(加载时会告警跳过): {pending}")
