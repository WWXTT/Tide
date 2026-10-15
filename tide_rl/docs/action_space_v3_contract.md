# Tide 动作空间 v3 契约（精确对齐期：卡区容量/位置/新决策点/动作数守卫）

定案日期：2026-10-14。本文档是 C#（Unity）与 Python（训练）两侧实现的**唯一权威契约**，
两侧任何常量/布局改动必须先改这里再改代码。版本号 `LAYOUT_VERSION = 3`。
前置文档：`action_space_v2_contract.md`（v2，2026-09-30，已被本版取代）；
对账依据：仓库根 `AI训练准备_v3对账报告_2026-10-14.md`。

## 0. 目标与范围

效果组合定案 + 8×7 缩盘定案后的**精确对齐**：槽位数贴引擎真实容量、补位置特征、
补两个漏掉的决策点、补动作数守卫。不动的：动作特征 8 维布局、身份三通路（[15..64]）、
奖励塑形口径（λ=0.005）、训练桥协议形态。

明确不做：Exile 逐卡编码（计数先行）；地牌锁色独立特征（格坐标即颜色，见 §4）；
网络对战协议增补。

## 1. 动作类型 TideActionType（v2 的 12 类 + 2 新类）

| 值 | 类型 | 语义 | Apply 映射 |
|---|---|---|---|
| 0 | PlayCard | 手牌打出（含抉择模式+目标展开） | `GameActions.PlayCard` |
| 1 | PlayLand | 手牌放入元素池（modeIndex=锁色 ManaType 枚举值，-1=缺省主色） | `GameActions.AddToElementPool` |
| 2 | （保留缺口） | 墓地出牌，2026-09-09 移除，勿复用 | — |
| 3 | Activate | 战场卡激活式能力（含目标展开） | `ActivateWithTargets`（PendingEffect.SelectedTargets） |
| 4 | Attack | 攻击宣言（attacker→对方随从/玩家；HeroAttacker=角色弹药攻击） | `GameActions.DeclareAttack` |
| 5 | EndTurn | 结束回合（恒排 Main 表末位） | `GameActions.EndTurn` |
| 6 | HeroSkill | 己方回合发动英雄技能；**卡牌化后 modeIndex 恒 0**（v2 文档的 skillId 口径已随 2026-10-07 卡牌化退役） | `GameActions.ActivateHeroSkill` |
| 7 | VoluntaryTrigger | 发动自愿桶触发效果 | `StackEngine.PlayerActivateVoluntary` |
| 8 | PassPriority | 响应窗口让过（恒排响应表末位） | `GameActions.PassPriority` |
| 9 | RespondPlay | 响应出牌（带目标展开） | `GameActions.PlayCardInResponse` |
| 10 | RespondActivate | 响应发动（带目标） | 同 3 口径 |
| 11 | Guard | 守卫宣言 | `StackEngine.PushGuardDeclaration` |
| **12** | **TapCreature** | **横置带地牌特性生物产 1 元素进临时钱包**（主阶段；产色自动=费用首色，无分支）；资格=己方控制+存活+未横置+`HasKeyword(LandTrait)` | `GameActions.TapCreatureForElement` |
| **13** | **CommitAwaken** | **苏醒立约**（主阶段；手牌+灰时长自我沉睡+未立约+未锁定）；此后施放灰份额全免转沉睡层数 | `GameActions.CommitAwaken` |

不建模裁定（维持 v2）：SkipElementPool=阶段推进（driver 自动）；PlayCardFromGraveyard（RL 池无来源卡）；
TransferEquipment（装备退役）；DrainStack（driver 自动）。

## 2. 动作特征 8 维（不变，值域扩展）

```
[0] valid        恒 1
[1] type         0..13
[2] sourceIndex  cards_ 92 槽下标（-1=无；发动区/技能卡格已编码，反制/技能行可解析）
[3] targetIndex  cards_ 92 槽下标（-1=无目标或玩家目标）
[4] modeIndex    PlayCard 抉择下标 / PlayLand=锁色枚举值 / 其余 0
[5] actionCost   费用总和浮点（Activate/Voluntary/HeroSkill 现推；TapCreature/CommitAwaken=0）
[6] effectIdentity 效果定义哈希注册下标 / 1024.0（PlayCard/PlayLand/Attack/EndTurn/PassPriority/
                  TapCreature/CommitAwaken = 0）
[7] targetKind   0=无/区域自结算，1=卡目标，2=对方玩家，3=己方玩家
```

## 3. 卡槽布局（每人 40 → 46，总 80 → 92）

```
[0..11]   Battlefield   12  = BattlefieldCapacityPerPlayer（引擎容量 2×6）
[12..25]  ElementPool   14  = 地牌槽编码容量护栏（2026-10-15 起规则硬顶退役：自然封顶 10 + 成长无上限，
                             超过 14 张仅槽位不可编码，规则照常——容量 14 本身不变、契约不破；按色叠格，黑白永不入池）
[26..37]  Hand          12  = 回合中溢出护栏（回合末上限 7 由 g 表达；第 13+ 张不可编码，动作仍可执行）
[38..41]  Graveyard     4   = 新死优先（slot38=最新入墓；更早的只进计数 g[11]/g[12]）
[42..44]  Activation    3   = 发动区（公开、反制指向点）；第 4+ 张不可编码（计数不入 g，极端情形罕见）
[45]      FieldZone     1   = 英雄技能卡（结界卡）；空=无技能
```

ZoneLayout(zone) 返回 (offset, cap) 单源供 CardIndex/TryZoneSlot 复用；WriteZone 的硬编码副本清除。
对方手牌仍只写 valid+controller（不开图）。

## 4. 卡特征 65 → 68 维

```
[0..13]  状态特征（不变）：valid/controller/超类/力量/生命/费用/横置/可攻击/失调/毒素/沉默/
         费用层净量/嘲讽/潜行
[14]     LandTrait 0/1（复用神佑死维；层评估 HasKeyword——TapCreature 资格与对手威胁预测）
[15..20] 原子精确内容哈希下标（不变）
[21]     组合结构哈希下标（不变）
[22]     关键词/tag/光环组合哈希下标（不变）
[23..28] 原子类型下标（不变）
[29..64] 原子参数浮点块（不变）
[65]     AwakenCommitted 0/1（苏醒已立约）
[66]     格坐标 x / 8（BoardLayout.Width；未落格 = -1：手牌/牌库/发动区/技能卡/墓地）
[67]     格坐标 z / 7（BoardLayout.Height；未落格 = -1）
```

位置语义（对账报告 §4）：单位格=落位钉子优先+first-free，**槽序≠格序**，必须显式编码；
地牌格=按色固定（LandCellOfElement），格坐标等价锁色 one-hot——无需独立色彩特征。
观测访问：`TideObservation.Build(core, seat, responseContext, board = null)` 追加可选
BoardState 参数（driver 传 `_board`；BattleController 镜像局传其棋盘）；board=null 时位置维全 -1
（不炸、降级可观测）。BoardState.TryGetCell 取单位格；地牌格经 LandCellOfElement(player, 锁色)
——锁色读元素池包装（PooledCard.LockedColor）。

## 5. globals 49 → 50 维（重排序 + 归一化 + 2 新维）

```
g[0]      me 生命 /30
g[1]      opp 生命 /30
g[2]      回合数 /40
g[3]      相位 0/1/2
g[4]      is_my_turn 0/1（决策座次视角；响应窗口停靠可为 0）
g[5]      me 手牌数
g[6]      opp 手牌数
g[7]      me 战场单位数
g[8]      opp 战场单位数
g[9]      me 元素池地牌数
g[10]     opp 元素池地牌数
g[11]     me 墓地数 /30
g[12]     opp 墓地数 /30
g[13]     me 牌库数 /60
g[14]     opp 牌库数 /60
g[15]     me 放逐数          ← 新增（v2 完全缺失）
g[16]     opp 放除数         ← 新增
g[17..22] me 临时钱包 6 色
g[23..28] opp 临时钱包 6 色
g[29]     栈占用 0/1
g[30]     me 势能 Φ /30（FieldValueReward.TotalValue, includeHand=true 口径不变）
g[31]     opp 势能 Φ /30
g[32]     me 地牌上限 /14（>14 饱和 1——2026-10-15 起上限可超 14）
g[33]     opp 地牌上限 /14（>14 饱和 1）
g[34..37] 黑白本回合已获得（me黑/me白/opp黑/opp白）/14（同饱和）
g[38..41] P1 英雄技能块 [has, tapped, uses/10, 角色攻击弹药/5]
g[42..45] P2 同上（绝对座次序；弹药=HeroAttackCounter 层数——v2 预留维启用）
g[46]     栈深度 /16
g[47]     栈顶来源卡槽下标 /92（无栈=0；发动区编码后施放中的卡可解析）
g[48]     栈顶 effectIdentity /1024
g[49]     决策上下文（0=己方 Main / 1=响应窗口）
```

（v2 的 g[48]"决策座次是否回合玩家"与 g[4] 重复，删除。）

## 6. 动作数守卫（C# 侧新增，修复 v2 截断隐患）

- `MAX_ACTIONS = 512` 三处同步不变（Python `tide_features.py` / C# `OnnxTidePolicy.MaxActions` / TCP）。
- **新增 C# 侧守卫**：`LegalActionEnumerator` 在 BuildFeatures 前若 `Actions.Count > 512`，
  保留前 511 行 + 恒保末位兜底行（Main=EndTurn / 响应=PassPriority）——Python
  `pad_or_truncate_actions` 的头截断永不会切掉兜底行（v2 该路径存在"无法结束回合"理论死锁）。
- `MaxActionsPerTurn = 256`（driver 防死循环）与每效果组合上限 24 不变。

## 7. TCP 协议版本握手

reset/step 响应 `info`：

```json
{"layoutVersion": 3, "dims": {"nCard": 68, "nGlobal": 50, "nAction": 8,
 "maxActions": 512, "maxCards": 92}, "decisionSeat": 0}
```

Python reset 逐值断言；C# `LayoutVersion=3` 常量回显（TideObservation 静态属性）。

## 8. 口径决策

1. 旧 checkpoint / fixture / onnx 全部作废（含 2026-09-30 后未重训的 36/49 代），新训练标签 `v3a`。
2. manifest（card_identity_manifest.json）机制不动（追加式；表指纹变更=全体换血既定口径）。
3. 奖励 λ=0.005 维持；g[30]/g[31] 只是显示归一化，塑形仍用原始 Φ（driver 侧口径不变）。
4. 归一化引入（生命/30、回合/40、势能/30、地牌上限/14、弹药/5）只为数值健康度，不改变信息量（地牌上限 2026-10-15 起可超 14——超出饱和到 1，分母不变以保训练分布）。
5. CardCore 触碰仅一处：`GameActions.HasSelfSleepEffect` private→public（枚举器资格预检复用，零行为变化）。

## 9. 双侧同步检查清单（验收用）

| 项 | C# | Python | 期望值 |
|---|---|---|---|
| 布局版本 | TideHeadlessServer 回显 | `LAYOUT_VERSION` | 3 |
| 卡特征维 | `TideObservation.NCard` | `N_CARD_FEATURES` | 68 |
| 卡槽总数 | `TideObservation.MaxCardsTotal` | `MAX_CARDS` | 92 |
| 全局维 | `TideObservation.NGlobal` | `N_GLOBAL_FEATURES` | 50 |
| 动作特征维 | `TideObservation.NAction` | `N_ACTION_FEATURES` | 8 |
| 动作上限 | `OnnxTidePolicy.MaxActions` / 枚举器守卫 | `MAX_ACTIONS` | 512 |
| 每回合动作上限 | `TideHeadlessDriver.MaxActionsPerTurn` | — | 256 |
| 动作类型值域 | `TideActionType` | 审计脚本对照表 | 0..13 见 §1 |
| 位置分母 | BoardLayout.Width/Height | 常量 8/7 | 一致 |

## 10. 实施任务清单（本批）

- **C1 常量与布局**：TideObservation 常量重排（46/92/68/50）+ ZoneLayout 单源 + WritePlayer 副本清除 + LayoutVersion=3。
- **C2 新槽位编码**：Graveyard 新死优先×4、Activation×3、FieldZone×1；WriteCard 增 [14]/[65]/[66]/[67]；
  Build 增 board 参数（单位格 TryGetCell / 地牌格 LandCellOfElement）。
- **C3 globals 重排**：§5 全表 + 归一化 + 放逐计数 + 弹药维。
- **C4 枚举器**：TapCreature/CommitAwaken 两类行 + Apply 分派 + 动作数守卫（§6）。
  GameActions.HasSelfSleepEffect 公开化。
- **C5 跟随**：OnnxTidePolicy 形状常量、TideHeadlessDriver（传 board 进 Build）、NeuralAI/
  OnnxBatchBattle/AiBattleE2E 编译、TideHeadlessServer info 回显 v3。
- **P1 Python 常量与握手**：tide_features.py（65→68、80→92、49→50、LAYOUT_VERSION=3、断言表）。
- **P2 模型形状**：tide_encoder / tide_agent / export_onnx 输入签名跟随常量（应自动）。
- **P3 审计脚本**：audit_action_space.py 对照表更新（类型 0..13）。
- **V 验收**：随机权重 export→Sentis fixture 对拍→audit→随机策略冒烟对局→dotnet verify quick 基线一致。
