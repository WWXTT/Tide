# Tide 动作空间 v3 契约（精确对齐期：卡区容量/位置/新决策点/动作数守卫）

定案日期：2026-10-14。本文档是 C#（Unity）与 Python（训练）两侧实现的**唯一权威契约**，
两侧任何常量/布局改动必须先改这里再改代码。版本号 `LAYOUT_VERSION = 21`。
前置文档：`action_space_v2_contract.md`（v2，2026-09-30，已被本版取代）；
对账依据：仓库根 `AI训练准备_v3对账报告_2026-10-14.md`。

> **2026-11-24 v21 修订（观测·卡组分布与策略分界，docs/观测_卡组分布与策略分界_实施方案.md）**：
> ①**己方牌库块 [154..183] 语义换血**：由「每张物理卡一槽」改「**每种唯一卡一槽 + 张数**」——
> 同内容键且**编码特征行逐位相等**的副本并组（实例态不同的同种卡不并），组内张数写卡特征
> **[157] countInZone**（其余区恒 1；valid=1 ⇔ ≥1）→ **NCard 157→158**；30 槽=唯一卡**种类**上限
>（绿 60 张 4 种=4 槽完整表达——此前 30 槽物理截断只剩 2 种），>30 种按排序键截断并置
> **g[96] deckTruncated**；`IndexOfCard` 同组任一张映射同一槽（`GroupDeckForEncoding` 单源）。
> ②**globals 88→97**：+**g[88..95] 己方牌库开局标签**（`DeckTagsOf(OriginalDeckOf(me))`，与对方
> g[50..57] 同函数同口径、整局恒定）+g[96] 截断标记。
> **v21 定稿增补（2026-11-24 回到一期训练目标 B0.3，globals 97→100，不再讨论 v22）**：
> +**g[97]/g[98] 双方手牌上限**（`RuleHooks.GetHandLimit` 单源线性计数——修改链如纳川仪典的
> 额外上限直接可见）+**g[99] 己方超上限剩余待弃张数**（弃牌进行中=HandLimitDiscardGate 快照
> 剩余；否则己方回合 max(0, 有效手牌−上限)；对手回合恒 0——对手回合超限走入手闸改投除外区）。
> A0 逐张弃配套：弃到几时停、要不要打牌腾手可算。
> ③**攻/守资格入内容身份**（维度不变、身份换血；用户定案：攻/守=带发动条件的主动效果）：
> 生物的效果原子占 [0..9]，**[10]=攻击资格原子（Activate_Active）/[11]=守卫资格原子
>（Activate_Response）**（`CardIdentityService.ChassisAttackSlot/GuardSlot`；冻结表行 cc6d592f /
> 23a06182，未声明=0），结构哈希含其骨架，卡特征 [7] 可攻击粗判加攻资格闸——此前火灵·攻/守
> 仅攻守资格不同、观测逐位相同（红·快攻只见 4 种卡）。
> ④**死亡计数 [152] 改原始层数**：手牌累计**无上限**（旧满 6/满 9 与 /9 归一化退役）、计价改
> **基价 +2 开启费**（`DeathTollEnableFee`，旧 ×4 总费口径退役）、执行期增幅 floor(计数/单价)
> 不封顶——与 2026-11-20 原子属性化（deathToll 布尔）同族收口。
> 动作 20 维不变。**旧 checkpoint/ONNX/fixture 全部作废**（按 dims 拒载回落 SimpleAI；
> 库内 v20 模型加载期被 `ValidateModelLayout` 拒——重训重导出前难度 2 模型 AI 不可用）。

> **2026-11-18 v15 修订（场地特殊化·拆槽）**：场地卡从战场单位槽拆出——
> ①**尾部场地专用块 2 槽**：[189]=枚举者视角己方场地卡 / [190]=对方场地卡
>（`TideObservation.FieldBase/FieldSlots`，每方 1 槽；无场地=空槽恒 0；zoneKind=**10**；
> posX/posZ=己方场地格坐标）；MaxCardsTotal 189→**191**（FieldBase=189/FieldSlots=2）。
> ②**战场槽 [0..8] 只编码单位**（剔场地，槽序=单位序与 `CardIndex(zoneIndex)` 对齐）；
> g[7]/g[8] 战场计数只数单位（场地存在性由专用槽 valid+zoneKind=10 表达）。
> ③**LAE 追加「攻击对方场地」行**（单位攻击与角色攻击各一组：TargetIndex=场地专用槽
> `[190]`、TargetKindCode=1——场地可被攻击、每次受伤固定 −1 耐久）；效果目标经
> IndexOfCard 自动路由场地槽（`双方场地/己方场地/对方场地` 区域词元）。
> ④zoneKind 扩 10=场地卡（战场槽 0 仍只含单位）。
> **旧 checkpoint/ONNX/fixture 全部作废**（MAX_CARDS/LAYOUT_VERSION 同步升 15；
> manifest 不动——身份哈希族未变；NeuralDeployGuard.VerifyFixtureLayout 拦旧 fixture）。

> **2026-11-02 v10 修订（补齐"玩家可见、AI 不可见"四笔欠账）**：
> ①**墓地扩槽 4→30**（新死优先保留，全量；超出 30 截断护栏，计数 g[11]/g[12] 照走）。
> ②**除外区逐卡入册**：ZoneLayout/EncodedZones 新增 Exile cap=8（区内时序 index0=最先放逐，
> 与 UI Region 面板同序）；超出截断（g[15]/g[16] 照走）。连带修复 LAE 动作行目标槽位断链
> （除外回收/深墓地卡此前 IndexOfCard=-1 → targetIndex 断，现在可解析）。
> ③**被展示卡（RevealRules 信息轴接入观测）**：对方手牌被展示（Exposed 指示物）卡由
> WriteConcealedCard 改走全特征 WriteCard（zoneKind=2，身份槽非零与隐藏卡天然可分）；
> 新尾块 4 槽 [185..188] 编码**对方牌库被展示卡**（RevealCard 域 7/8 / 发现等效果可挂，
> 区内序=玩家点卡组面板所见，无额外泄露）；己方牌库被展示卡不另设标记（身份已在己方
> 牌库多重集块内，无增量信息）。
> ④**牌库顶虚拟手牌（观测+动作行）**：尾块 1 槽 [184]，`RuleAuraSystem.ScopeHits(
> TopDeckAsHandAura, seat)` 命中且牌库非空时编码 deck[0] 全特征 zoneKind=7——
> **未命中恒空槽（防泄露下一抽，验收硬断言）**；LAE 同步枚举 deck[0] 的 PlayCard
> 变体行（sourceIndex=[184]，模式/目标/苏醒变体与手牌同构；不放地/不充能——虚拟手牌
> 规则；Apply 按"卡在 Zone.Deck"分派 fromZone=Deck，TopDeckPlaySource 授权口校验）。
> 每玩家槽 43→77（墓地 +26、除外 +8）、MaxCardsTotal 116→189、DeckBase 86→154。
> NCard 116/NGlobal 58 不变（zoneKind 值扩 7=牌库顶 8=对方牌库被展示 9=除外区）。
> **旧模型权重作废**（MAX_CARDS/LAYOUT_VERSION 同步升 10；manifest 不动）。

> **2026-11-02 v9 修订（牌库双通道）**：双方牌库信息补进观测（此前牌库仅 g[13]/g[14] 计数），
> 两侧分通道、位置固定：①**己方牌库块 [86..115] 共 30 槽**尾接 cards_（MaxCardsTotal
> 86→116），逐卡全特征编码（身份/类型/参数三通路），**无序集合**——按内容哈希排序写入
> （排序键=Structure→Composition→RuntimeId，位置无关防泄露洗牌顺序），仅编码决策座次方
> （对方侧 30 槽不编码）；cap=30 截断护栏（衍生物入库可超，超出不编码，计数仍走 g[13]）。
> ②**卡特征 115→116**：尾加 [115] zoneKind（0=战场/1=元素池/2=手牌/3=墓地/4=发动区/
> 5=场地区/6=己方牌库；对方隐藏手牌=2）——逐卡编码器无槽位位置嵌入，不加则牌库卡与
> 手牌卡特征同形不可区分（v8 起潜伏缺口，本次顺带修复全部区域的区域可分性）。
> ③**globals 50→58**：尾加 g[50..57] 对方牌库**开局标签快照**（随卡组创建保留，整局恒定，
> 不随抽牌变化）——[50..53] 四色张数（灰/红/蓝/绿，逐色多计：`Cost[色] > 0`，
> 与 CardSorter.HasCostColor 同口径）、[54..56] 费用三档张数（1-2/3-6/7-9，档值=
> `Clamp(Round(TotalCost),1,9)` 后归档，与 DeckBuilder 费用曲线同口径）、[57] 卡组总数。
> 原始计数不归一化。g[47] 栈顶来源分母随 MaxCardsTotal 变 116。
> 数据源：`GameCore.OriginalDeckOf(player)`（InitGame 装载点留存的入局前卡表）。
> **旧模型权重作废**（MAX_CARDS/N_CARD_FEATURES/N_GLOBAL_FEATURES/LAYOUT_VERSION 同步升 9，
> TCP 握手逐值断言拦截混跑；manifest 不动——身份哈希族未变）。

> **2026-11-02 v8 修订（LAE 多目标动作空间）**：动作特征 8→11 维，追加 `[8] target2Index` /
> `[9] target3Index`（多目标组合行 Targets[1]/[2] 的 cards_ 槽位，-1=无/玩家目标/>3 截断不编码）
> 与 `[10] nTargets`（0..8 钳；全取行=候选数）。动因：引擎 2026-11-02 多目标定案（每原子
> effCount>1 压过选一模式——如洗回跨双墓地选 N）后，LAE 旧版按效果级 count 展开单目标行，
> 且 Multiple 组合行特征只编码 targets[0]（同锚组合不可区分，argmax 由噪声决定）。v8 逐原子
> 口径（PerAtomTargets）按 CollectAtomTargetSpecs 单源规格展开（候选/数量与人类声明期收集器/
> 结算路径同口径），多交互原子组合笛卡尔串接；组合行数 cap 仍 24/效果。
> **旧模型权重作废**（N_ACTION_FEATURES/LAYOUT_VERSION 同步升 8，TCP 握手逐值断言拦截混跑）。

> **2026-10-29 v6/v7 修订（P11 盘面重塑 + 对座镜像）**：v6 战场槽 12→9（单位区 4+5），
> 每人 46→43 槽、总 92→86，后续区域偏移整体前移 3（§3）；位置特征归一化分母随
> BoardLayout.Width 8→7（§4/§9）。v7 盘面坐标轴平移（双方六格行同列带 x1..x6、
> P2 改上下镜像），槽位/容量与 v6 一致，仅 posX/posZ 分布位移。
> **旧模型权重作废**（MAX_CARDS/LAYOUT_VERSION 同步升 7，TCP 握手逐值断言拦截混跑）。

> **2026-10-29 v5 修订（P6 苏醒改版）**：①动作类型 13 `CommitAwaken`（苏醒立约）退役——
> 立约决策删除，苏醒并入 PlayCard 的 **awaken 变体行**（动作行=(卡,模式,目标,awaken)，
> 免净费灰+绿份额转入场沉睡层数、横置登场；灰+绿=0 不提供变体）。序号 13 不回收勿复用。
> ②卡特征删 [113] AwakenCommitted：116→115 维，坐标位前移至 [113][114]，
> **旧模型权重作废**（N_CARD_FEATURES/LayoutVersion 同步升 5，TCP 握手逐值断言拦截混跑）。

> **2026-10-16 v4 修订**：原子槽 6 → 12（正式卡满配 9 = 代价1 + 主动/自动组合各按 2 + 关键词3，
> 留 3 槽余量），卡特征 68 → 116 维（见 §4）。v3a 之后未产出正式 checkpoint/onnx，
> 直接升版作废旧产出（§8 口径 1）。

## 0. 目标与范围

效果组合定案 + 8×7 缩盘定案后的**精确对齐**：槽位数贴引擎真实容量、补位置特征、
补两个漏掉的决策点、补动作数守卫。不动的：动作特征 8 维布局、身份三通路结构（槽位布局见 §4）、
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
| **13** | （保留缺口） | CommitAwaken 苏醒立约，2026-10-29 P6 移除，勿复用。苏醒并入 PlayCard 的 awaken 变体行（`(卡,模式,目标,awaken)`；免净费灰+绿转沉睡层数、横置登场，`GameActions.PlayCard(..., awakenCast:true)`） | — |

不建模裁定（维持 v2）：SkipElementPool=阶段推进（driver 自动）；PlayCardFromGraveyard（RL 池无来源卡）；
TransferEquipment（装备退役）；DrainStack（driver 自动）。

## 2. 动作特征 11 维（v8：8 维 + 多目标三槽）

```
[0] valid        恒 1
[1] type         0..12（13 已退役——v5 起苏醒并入 PlayCard 变体行）
[2] sourceIndex  cards_ 191 槽下标（-1=无；发动区/技能卡格已编码，反制/技能行可解析；
                v10 牌库顶变体行=固定槽 [184] zoneKind=7）
[3] targetIndex  cards_ 191 槽下标（-1=无目标或玩家目标；v15 攻击场地行=场地专用槽 [189]/[190]）
[4] modeIndex    PlayCard 抉择下标 / PlayLand=锁色枚举值 / 其余 0
[5] actionCost   费用总和浮点（Activate/Voluntary/HeroSkill 现推；PlayCard 苏醒变体=豁免后账单；
                TapCreature=0）
[6] effectIdentity 效果定义哈希注册下标 / 1024.0（PlayCard/PlayLand/Attack/EndTurn/PassPriority/
                  TapCreature = 0）
[7] targetKind   0=无/区域自结算，1=卡目标，2=对方玩家，3=己方玩家
[8] target2Index v8 多目标：Targets[1] 槽位（-1=无/玩家目标/组合超出 3 个截断不编码）
[9] target3Index v8 多目标：Targets[2] 槽位（同上）
[10] nTargets    v8 多目标：行内目标数（0..8 钳；全取行=候选数）
```

> [8..10] 仅目标展开四类动作（PlayCard/RespondPlay/Activate/RespondActivate）有效；
> 其余类型恒 -1 / 按 targetKind 计单目标（Attack/Guard 恒单目标）。行内 `TideAction.Targets`
> 完整（预选按原子执行序串接，引擎结算期预选队列消费），特征只编码前 3 个目标。

> **2026-11-24 定稿（方案 A；A6，未实现）**：`[10]` 将由 `nTargets` 改为「**剩余待选数（含本步）**」——
> 0=非目标选择行、1=单目标/最后一步、N=选完还差 N 个；每行 `Targets` 恒 **1 个**，`[8]/[9]` 恒 -1；
> 组合行 / prefix-k / `MaxSubsetRows` / `MaxPrefixRows` / `MaxDiscardRows` 退役（AI 逐目标单选，玩家侧仍一次提交）。
> 动作维仍 20、`LAYOUT_VERSION` 不变，但属语义变更：旧 ONNX / checkpoint / 夹具不得复用。见 `docs/回到一期训练目标.md` A6。

> v5 苏醒变体行的区分信号：同 (source,target,mode) 下 actionCost 更低的那行=苏醒施放
>（特征无独立 awaken 位；C# 侧按行序普通行在前、苏醒变体在后）。

> v10 牌库顶变体行（2026-11-02）：TopDeckAsHandAura 光环命中己方且牌库非空时，deck[0]
> 追加与手牌出牌同构的 PlayCard 行（模式/目标/苏醒变体共用同一展开构建器，sourceIndex=
> 固定槽 [184]；不放地/不充能——虚拟手牌不计上限、不可弃/放地）。Apply 不新增动作类型：
> 卡在 Zone.Deck → fromZone=Deck（TopDeckPlaySource 授权口在 GameActions.PlayCard 内校验）。
> SimpleAI（脚本对手）不消费该行。

## 3. 卡槽布局（v15：每人 77 槽（墓地 30+除外 8 扩容），总 154 + 尾块 37 = 191）

```
[0..8]   Battlefield   9  = BattlefieldCapacityPerPlayer（v6 P11 盘面重塑：单位区 4+5）
                             v15 拆场地：只编码**单位**（剔场地，槽序=单位序）；场地走尾部专用块
[9..22]  ElementPool   14  = 地牌槽编码容量护栏（2026-10-15 起规则硬顶退役：自然封顶 10 + 成长无上限，
                             超过 14 张仅槽位不可编码，规则照常——容量 14 本身不变、契约不破；按色叠格，黑白永不入池）
[23..34] Hand          12  = 回合中溢出护栏（回合末上限 7 由 g 表达；第 13+ 张不可编码，动作仍可执行）
[35..64] Graveyard     30  = 新死优先（slot35=最新入墓；v10 4→30 全量——墓地可堆到整卡组规模+衍生物,
                             超出 30 截断护栏，计数 g[11]/g[12] 照走）
[65..67] Activation    3   = 发动区（公开、反制指向点）；第 4+ 张不可编码（计数不入 g，极端情形罕见）
[68]     FieldZone     1   = 英雄技能卡（结界卡）；空=无技能
[69..76] Exile         8   = 除外区（v10 新增逐卡；区内时序 index0=最先放逐=UI Region 面板同序；
                             超出 8 截断护栏，计数 g[15]/g[16] 照走）
—— 以上每玩家 77 槽 × 2 = 154（对座布局：座次方 [0..76]，对方 [77..153]）——
[154..183] OwnDeck     30  = 己方牌库（v9；**仅决策座次方**，对方牌库走 g[50..57] 标签通道）。
                             无序集合：按 (StructureContentHash → CompositionContentHash →
                             RuntimeId) 排序写入前 30 张，排序键位置无关——**不泄露洗牌顺序**。
                             cap=30 截断护栏（剩余计数 g[13] 照走）。
[184]     DeckTop      1   = 牌库顶虚拟手牌（v10）：TopDeckAsHandAura 光环命中座次方且牌库非空时
                             编码 deck[0] 全特征（zoneKind=7）；**未命中恒空槽（防泄露下一抽）**。
[185..188] OppDeckExposed 4 = 对方牌库被展示卡（v10；RevealRules.GetExposedCards 区内序=玩家点卡组
                             面板所见；己方牌库被展示卡不另设——身份已在 OwnDeck 块内）。cap=4 截断。
[189]     Field(own)    1   = 场地专用槽·己方（v15 拆场地）：该侧战场区场地卡全特征（zoneKind=10，
                             posX/posZ=场地格坐标）；无场地=空槽恒 0（valid=0 即无场地信号）。
[190]     Field(opp)    1   = 场地专用槽·对方（同上；座次镜像——Build(seat) 时 [189]=seat 方场地）。
```

（v3 起 ZoneLayout(zone) 返回 (offset, cap) 单源供 CardIndex/TryZoneSlot 复用；WriteZone 的
硬编码副本清除。v9 起 OwnDeck 及 v10 尾块为尾接全局块，不走 ZoneLayout 每玩家偏移语义——
专用基址常量（DeckBase/DeckTopSlot/OppDeckExposedBase）+ 排序名次路径，编码与 IndexOfCard
同享一套排序。）
对方手牌只写 valid+controller（不开图）——**v10 起被展示（Exposed 指示物）的对方手牌卡
豁免为全特征开图**（玩家口径：UI 翻正面/网络快照同源下发）。

## 4. 卡特征 68 → 116 维（2026-10-16 v4：原子槽 6 → 12；v5 2026-10-29 删苏醒位 → 115；v9 尾加 zoneKind → 116）

```
[0..13]   状态特征（不变）：valid/controller/超类/力量/生命/费用/横置/可攻击/失调/毒素/沉默/
          费用层净量/嘲讽/潜行
[14]      LandTrait 0/1（复用神佑死维；层评估 HasKeyword——TapCreature 资格与对手威胁预测）
[15..26]  原子精确内容哈希下标（12 槽；跨效果按执行序展平，超出 12 个截断；参数敏感。
          容量依据：正式卡满配 9 = 代价1 + 主动/自动组合各按 2 + 关键词3，留 3 槽余量）
[27]      组合结构哈希下标
[28]      关键词/tag/光环组合哈希下标
[29..40]  原子类型下标（12 槽）
[41..112] 原子参数浮点块（12 槽 × 6 维）
[113]     格坐标 x / 7（BoardLayout.Width，v6 盘面重塑 8→7；未落格 = -1：手牌/牌库/发动区/技能卡/墓地）
[114]     格坐标 z / 7（BoardLayout.Height；未落格 = -1）
[115]     zoneKind 0..10（v9 新增；显式映射防枚举重排：0=战场 1=元素池 2=手牌 3=墓地 4=发动区
          5=场地区 6=己方牌库块；v10 扩 7=牌库顶虚拟手牌 8=对方牌库被展示 9=除外区；
          对方隐藏手牌=2；v15 扩 10=场地卡（场地专用槽 [189]/[190] 直写，战场槽 0 仍只含单位）。
          动因：编码器逐卡独立无槽位位置嵌入，此维是区域可分性的唯一信号）
（旧 [113] AwakenCommitted 已删——2026-10-29 v5：立约决策退役，坐标位前移一格；
  v7 对座镜像：posX/posZ 分布轴平移，下标/分母不变）
```

（v3 原布局 [15..20] 哈希 / [21][22] 结构+组合 / [23..28] 类型 / [29..64] 参数 / [65..67] 尾部，
2026-10-16 起整体右移扩容，语义顺序不变。）

位置语义（对账报告 §4）：单位格=落位钉子优先+first-free，**槽序≠格序**，必须显式编码；
地牌格=按色固定（LandCellOfElement），格坐标等价锁色 one-hot——无需独立色彩特征。
观测访问：`TideObservation.Build(core, seat, responseContext, board = null)` 追加可选
BoardState 参数（driver 传 `_board`；BattleController 镜像局传其棋盘）；board=null 时位置维全 -1
（不炸、降级可观测）。BoardState.TryGetCell 取单位格；地牌格经 LandCellOfElement(player, 锁色)
——锁色读元素池包装（PooledCard.LockedColor）。

## 5. globals 49 → 50 维（重排序 + 归一化 + 2 新维）；v9：尾加对方牌库标签 8 维 → 58

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
g[47]     栈顶来源卡槽下标 /189（无栈=0；发动区编码后施放中的卡可解析；v9 起分母随 MaxCardsTotal，v10=189）
g[48]     栈顶 effectIdentity /1024
g[49]     决策上下文（0=己方 Main / 1=响应窗口）
g[50..53] 对方牌库四色张数（灰/红/蓝/绿=ManaType 0..3 序；逐色多计：Cost[色] > 0，
          与 CardSorter.HasCostColor 同口径——多色卡在构成色各计一次）   ← v9 新增
g[54..56] 对方牌库费用三档张数（档值=Clamp(Round(TotalCost),1,9)：0 费并入 1 档、>9 并入
          9+，与 DeckBuilder 费用曲线同口径；[54]=1-2 [55]=3-6 [56]=7-9）   ← v9 新增
g[57]     对方牌组总数（创建时）                                        ← v9 新增
```

（v2 的 g[48]"决策座次是否回合玩家"与 g[4] 重复，删除。
v9 [50..57] = 对方牌库**开局标签快照**：`GameCore.OriginalDeckOf(opponent)`（InitGame 装载点
留存的入局前卡表）计算，随卡组创建保留、整局恒定——不随抽牌/洗回变化（真实玩家口径=开局
展示的卡组标签）。原始计数不归一化（与 g[5..10] 手牌/战场计数同族）。）

## 6. 动作数守卫（C# 侧新增，修复 v2 截断隐患）

- `MAX_ACTIONS = 512` 三处同步不变（Python `tide_features.py` / C# `OnnxTidePolicy.MaxActions` / TCP）。
- **新增 C# 侧守卫**：`LegalActionEnumerator` 在 BuildFeatures 前若 `Actions.Count > 512`，
  保留前 511 行 + 恒保末位兜底行（Main=EndTurn / 响应=PassPriority）——Python
  `pad_or_truncate_actions` 的头截断永不会切掉兜底行（v2 该路径存在"无法结束回合"理论死锁）。
- `MaxActionsPerTurn = 256`（driver 防死循环）与每效果组合上限 24 不变
  （v8 语义：逐原子选 N 组合枚举与多交互原子笛卡尔串接共用该 cap；组合行数=候选数级别）。

## 7. TCP 协议版本握手

reset/step 响应 `info`：

```json
{"layoutVersion": 10, "dims": {"nCard": 116, "nGlobal": 58, "nAction": 11,
 "maxActions": 512, "maxCards": 189}, "decisionSeat": 0}
```

Python reset 逐值断言；C# `TideObservation.LayoutVersion=10` 常量回显（v8 起 TideHeadlessServer 直接引用该常量，不再本地复制）。
2026-10-30 扩展：reset 请求可带 `modelTheme`（vs 脚本口径下模型侧卡组固定该主题整组，
红·快攻镜像训练口径），响应 `info.modelTheme` 回显实际命中的 key（缺省/未命中/自对弈空串）。

## 8. 口径决策

1. 旧 checkpoint / fixture / onnx 全部作废（含 2026-09-30 后未重训的 36/49 代），新训练标签 `v3a`；**v5（2026-10-29 P6）再作废一轮**：删卡特征 [113] 苏醒位 + 动作 13 退役，`v3a`/`v4` 产出不可加载；**v6/v7（2026-10-29 P11+对座镜像）又作废一轮**：卡槽 92→86、战场 12→9（v6）、盘面坐标平移（v7），`v3a`~`v5` 产出不可加载；**v8（2026-11-02 LAE 多目标）再作废一轮**：动作特征 8→11 维（+target2/target3/nTargets），`v3a`~`v7` 产出不可加载；**v9（2026-11-02 牌库双通道）再作废一轮**：卡槽 86→116（+己方牌库块）、卡特征 115→116（+zoneKind）、globals 50→58（+对方标签），`v3a`~`v8` 产出不可加载（Sentis 加载期 ValidateModelLayout 拦截，NeuralAiDriver 回落 SimpleAI）；**v10（2026-11-02 观测欠账四件套）再作废一轮**：卡槽 116→189（墓地区 4→30、除外 8 入册、尾块牌库顶+对方牌库展示），`v3a`~`v9` 产出不可加载；**v11~v18 见 §1 版本链（动作空间补全 P-1/P0/P1/P2/P3）**；**v19（2026-11-20 绿套补盲）再作废一轮**：globals 58→70（+剩余可用元素块 g[58..69]）、卡特征 116→152（原子参数块 6→9：+settleKind/branchParam/branchBudget/costTotal）、原子身份哈希追加分支身份段（稳定下标重键）——v18 及以前产出不可加载，动作 18 维/14 类型/卡槽 191 不变（计划=docs/AI绿套学习与观测补盲_修改计划.md）。
2. manifest（card_identity_manifest.json）机制不动（追加式；表指纹变更=全体换血既定口径）。**（P-1 注：2026-11-19 起追加式分配退役、纯派生；manifest 仅存档。本行为历史口径。）**
3. 奖励 λ=0.005 维持；g[30]/g[31] 只是显示归一化，塑形仍用原始 Φ（driver 侧口径不变）。
4. 归一化引入（生命/30、回合/40、势能/30、地牌上限/14、弹药/5）只为数值健康度，不改变信息量（地牌上限 2026-10-15 起可超 14——超出饱和到 1，分母不变以保训练分布）。
5. CardCore 触碰仅一处：`GameActions.HasSelfSleepEffect` private→public（枚举器资格预检复用，零行为变化）。**（v5 注：HasSelfSleepEffect 已随 P6 苏醒改版整体删除——立约资格预检不复存在）**

## 9. 双侧同步检查清单（验收用）

| 项 | C# | Python | 期望值 |
|---|---|---|---|
| 布局版本 | TideHeadlessServer 回显 | `LAYOUT_VERSION` | 21 |
| 卡特征维 | `TideObservation.NCard` | `N_CARD_FEATURES` | 158 |
| 卡槽总数 | `TideObservation.MaxCardsTotal` | `MAX_CARDS` | 191 |
| 全局维 | `TideObservation.NGlobal` | `N_GLOBAL_FEATURES` | 97 |
| 动作特征维 | `TideObservation.NAction` | `N_ACTION_FEATURES` | 20 |
| 动作上限 | `OnnxTidePolicy.MaxActions` / 枚举器守卫 | `MAX_ACTIONS` | 512 |
| 每回合动作上限 | `TideHeadlessDriver.MaxActionsPerTurn` | — | 256 |
| 动作类型值域 | `TideActionType` | 审计脚本对照表 | 0..13 见 §1 |
| 位置分母 | BoardLayout.Width/Height | 常量 7/7 | 一致（v6 起 8→7） |
| 己方牌库块 | `TideObservation.DeckBase=154 / DeckSlots=30` | §3 布局 | [154..183] **唯一卡分组**（v21） |
| 牌库块张数 | `TideObservation.CountSlot=157` | `N_CARD_FEATURES` 派生 | 组内张数（其余区恒 1） |
| 牌库顶槽 | `TideObservation.DeckTopSlot=184` | §3 布局 | 光环命中才编码（反泄露硬断言） |
| 对方牌库展示块 | `TideObservation.OppDeckExposedBase=185` | §3 布局 | cap=4 区内序 |
| 对方标签快照 | `GameCore.OriginalDeckOf` → g[50..57] | §5 布局 | 8 维静态 |
| 己方标签快照（v21） | `GameCore.OriginalDeckOf` → g[88..95] | §5 布局 | 8 维静态（同函数同口径） |
| 牌库截断标记（v21） | `TideObservation.DeckTruncatedSlot=96` | §5 布局 | 唯一卡 >30 种=1 |
| 剩余可用元素块（v19） | `WriteRemainingElements` → g[58..69] | §5 布局 | 12 维（双方四色/Any/总量） |
| 原子参数块（v19） | `CardIdentityService.AtomParamDim=9` → [41..148] | `ATOM_PARAM_DIM=9` | 12 槽 × 9 维 |
| 战斗底盘槽（v21） | `CardIdentityService.ChassisAttackSlot=10 / ChassisGuardSlot=11` | `N_TYPE_SLOTS=12` 派生 | [10]=攻击/[11]=守卫（未声明=0） |

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
