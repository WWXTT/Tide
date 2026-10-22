# -*- coding: utf-8 -*-
"""2026-10-11 一次性构建两个横评 manifest：icons-cosmic5 / cardbg-shapes5（5 LoRA 全量）。"""
import io
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))

LORAS = [
    ("cosmic-space", "space"),
    ("cosmic-galaxy", "galaxy"),
    ("cosmic-winter", "winter"),
    ("cosmic-entity", "entity"),
    ("cosmic-k2", "k2"),
]

# 图标 v2：LoRA 降权 0.35 的 icon 专用条目（意象为主语，星空降点缀）
LORAS_ICON = [
    ("cosmic-space-icon", "space"),
    ("cosmic-galaxy-icon", "galaxy"),
    ("cosmic-winter-icon", "winter"),
    ("cosmic-entity-icon", "entity"),
    ("cosmic-k2-icon", "k2"),
]

ELEM_TINTS = {
    "gray": [176, 176, 182],
    "red": [226, 72, 58],
    "blue": [86, 148, 236],
    "green": [96, 208, 124],
    "white": [244, 242, 232],
    "black": [138, 96, 210],
}

BADGE_NEG = ("badge, crest, heraldry, coat of arms, medallion, "
             "multiple objects, duplicate")

# ---------- 图标批次 ----------
ICON_SEMANTICS = [
    ("stat-attack", 44, 40,
     "(a single upright sword with glowing sharp blade:1.3), centered weapon icon",
     BADGE_NEG, "攻击力·剑"),
    ("stat-life", 44, 40,
     "(a single glowing heart-shaped vitality ruby:1.3), soft warm pulse glow",
     BADGE_NEG, "生命值·心"),
    ("stat-durability", 44, 40,
     "(a single round shield with concentric layered plates:1.3), sturdy",
     BADGE_NEG, "耐久·层盾"),
    ("stat-speed", 44, 40,
     "(a single swift feathered wing with motion streaks:1.3)",
     BADGE_NEG, "速度·翼"),
    ("arrow-up", 30, 30,
     "(a single bold arrow pointing straight up, thick shaft, wide arrowhead:1.3), clean silhouette",
     BADGE_NEG + ", tilted, diagonal, curved", "方向箭头·向上，其余方向 Unity 旋转"),
    ("kind-creature", 44, 40,
     "(a square beast sigil with claw marks on sturdy totem corners:1.3), square emblem",
     BADGE_NEG, "发动类型·生物=方形"),
    ("kind-enchant", 44, 40,
     "(a circular magic seal ring with orbiting runes:1.3), round emblem",
     BADGE_NEG, "发动类型·结界=圆形"),
    ("kind-spell", 44, 40,
     "(a triangular arcane burst sigil with sharp corners:1.3), triangle emblem",
     BADGE_NEG, "发动类型·法术=三角"),
]

ELEM_PROMPT = ("(a single arcane elemental gem emblem, faceted crystal core "
               "with orbiting rune shards:1.3), centered, glowing")

icon_items = []
for sem, w, h, addon, neg, note in ICON_SEMANTICS:
    for style, tag in LORAS_ICON:
        icon_items.append({
            "id": f"{sem}__{tag}", "type": "kwicon", "w": w, "h": h,
            "style": style, "flat": False,
            "prompt_addon": addon, "negative_addon": neg,
            "note": f"{note} · {style}",
        })
for elem, tint in ELEM_TINTS.items():
    for style, tag in LORAS_ICON:
        icon_items.append({
            "id": f"elem-{elem}__{tag}", "type": "kwgem", "w": 14, "h": 14,
            "style": style, "flat": False, "denoise": 0.7,
            "tint_face": [int(c * 0.45) for c in tint], "tint_edge": tint,
            "seed": 20261011,
            "prompt_addon": ELEM_PROMPT, "negative_addon": BADGE_NEG,
            "note": f"元素·{elem} tint={tint} · 同 seed 同构图 · {style}",
        })

icons_manifest = {
    "task": "icons-cosmic5-v3",
    "style": "cosmic-space-icon",
    "server": "http://127.0.0.1:7860",
    "variants": 2,
    "items": icon_items,
}

# ---------- 卡背景批次 ----------
FAMILIES = {
    "creature": ("frame_square",
        "creature card type family, square sturdy motifs, corner totem studs, "
        "bone and hide textures, slightly organically warped square shape"),
    "enchant": ("frame_ring",
        "enchantment card type family, circular orbit motifs, engraved ring runes, "
        "soft radial halo, slightly warped circular shape"),
    "spell": ("frame_tri",
        "spell card type family, triangular motifs, sharp beveled corners, "
        "arcane burst ornaments, slightly warped triangle shape"),
}

COMPONENTS = [
    ("artframe", None, 742, 742, "trading card art window frame, hollow center for illustration"),
    ("effectpanel", "row", 550, 150, "recessed effect text panel strip, plain dark interior for text"),
    ("namebanner", "banner", 420, 64, "card name plate banner, plain center reserved for name text"),
]

card_items = []
for ctype, (frame_spec, family) in FAMILIES.items():
    for comp, ctype_override, w, h, comp_addon in COMPONENTS:
        itype = frame_spec if comp == "artframe" else ctype_override
        for style, tag in LORAS:
            card_items.append({
                "id": f"{ctype}-{comp}__{tag}", "type": itype, "w": w, "h": h,
                "style": style,
                "prompt_addon": f"{family}, {comp_addon}",
                "note": f"{ctype}/{comp} · {style}",
            })

card_manifest = {
    "task": "cardbg-shapes5",
    "style": "cosmic-space",
    "server": "http://127.0.0.1:7860",
    "variants": 3,
    "items": card_items,
}

for name, man in [("icons_cosmic5.json", icons_manifest), ("cardbg_shapes5.json", card_manifest)]:
    path = os.path.join(HERE, name)
    with io.open(path, "w", encoding="utf-8") as fh:
        json.dump(man, fh, ensure_ascii=False, indent=1)
    print(name, len(man["items"]), "items x", man["variants"], "variants")
