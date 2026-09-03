# 影之诗：超凡世界（Shadowverse: Worlds Beyond）卡牌数据

数据来源：<https://shadowverse-wb.com/chs/deck/cardslist/>（Cygames 官方卡库）。

## 关键结论

与百闻牌不同，这个站卡数据不在 JS bundle 里，而是走**后端 JSON 接口**：

- 接口：`https://shadowverse-wb.com/web/CardList/cardList`
- **必须带请求头 `Lang: chs`**（否则返回日文）、`X-Requested-With: XMLHttpRequest`
- 参数：`offset`（分页，每页 30）、`include_token=1`（含衍生卡）
- 返回 `{data: {count, card_details, card_set_names, skill_names, tribe_names, ...}}`

## 文件清单

| 文件 | 说明 |
|------|------|
| `影之诗_卡牌数据.json` | 归一化后的完整卡表（904 张，含 93 张衍生卡） |
| `原始数据.json` | 接口原始响应（含 cards 关联关系、全部映射表） |
| `影之诗_效果列表.md` | 按职业分组的可读效果表 |
| `关键词列表.txt` | 关键词（skill_names 映射表，id + 名称） |

## 字段说明（影之诗_卡牌数据.json）

| 字段 | 含义 |
|------|------|
| `id` / `card_id` | 卡牌 id（衍生卡 id 以 9 开头，如 90014330） |
| `name` | 卡名（中文） |
| `type` / `type_en` | 卡牌类型：随从 follower / 护符 amulet / 法术 spell |
| `class` / `class_en` | 职业：中立/精灵/皇家/巫师/龙族/梦魇/主教/复仇者 |
| `cost` | 费用 |
| `atk` / `life` | 攻击力 / 生命值（随从才有，护符法术为 0） |
| `rarity` / `rarity_en` | 稀有度：铜卡/银卡/金卡/虹卡 |
| `card_set_id` / `card_set_name` | 卡包 |
| `tribes` / `tribe_names` | 种族（如 兵士/妖精/土之印…） |
| `skill_text` | 效果原文（含 `<color=Keyword>` 等标记） |
| `skill_text_clean` | 效果纯文本（标记已清理） |
| `flavour_text` | 卡面风味文字 |
| `evo_skill_text` | 进化形态效果（若不同） |
| `cv` / `illustrator` | 声优 / 画师 |
| `is_token` | 是否衍生卡 |
| `is_include_rotation` | 是否在轮换赛制 |
| `deck_enabled_num` | 牌组可投入上限（通常 3） |
| `card_image_hash` / `card_banner_image_hash` / `evo_card_image_hash` | 卡图哈希（用于图片 URL） |
| `related_card_ids` / `specific_effect_card_ids` | 关联卡牌 / 特指效果卡牌 id |

## 类型数值映射（来自前端配置）

```js
cardTypes : {1:"follower", 2:"amulet", 3:"amulet", 4:"spell"}
classNames: {0:"neutral",1:"elf",2:"royal",3:"witch",4:"dragon",5:"nightmare",6:"bishop",7:"nemesis"}
rarity    : {1:"bronze", 2:"silver", 3:"gold", 4:"legend"}
```

> 注意：type 2 与 type 3 都映射为「护符」，区别是——
> type 2 = 带【启动/模式】能力的护符，type 3 = 带【吟唱】(倒计时) 的护符。

## skill_text 标记语法

| 标记 | 含义 |
|------|------|
| `<color=Keyword>xxx</color>` | 关键词高亮 |
| `<hr>` | 分隔线（主能力与追加能力之间） |
| `<ev>xxx</ev>` | 进化时能力 |
| `<sev>xxx</sev>` | 超进化时能力 |
| `<ridx=N>xxx</ridx>` | 能力序号引用 |
| `<nobr>` | 不换行 |
