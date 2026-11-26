using System;
using System.Collections.Generic;
using System.IO;
using CardCore;
using CardCore.Attribute;
using GameBoard;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// 状态快照：把 GameCore 当前局面编码成「当前决策座次」视角的固定维度特征向量，
    /// 契约对齐 ygo-agent 的 cards_/global_ 表格（(实体数 × 特征维)，不是棋盘网格）。
    /// 纯读状态，不改任何核心。
    ///
    /// v3 契约（2026-10-14，tide_rl/docs/action_space_v3_contract.md，对账依据=仓库根
    /// AI训练准备_v3对账报告_2026-10-14.md）：
    ///
    /// 槽位分配（每玩家 77 槽，共 154 槽 + 尾块 35 + 场地 2 = 191；容量=引擎真值精确对齐）：
    ///   [0..8]   战场 Battlefield (9 = BattlefieldCapacityPerPlayer，单位区 4+5；P11 盘面重塑)
    ///   [9..22]  元素池 ElementPool (14 = 地牌槽编码容量护栏——2026-10-15 规则硬顶退役后仅编码上限；按色叠格)
    ///   [23..34] 手牌 Hand (12 = 回合中溢出护栏；回合末上限 7 由 globals 表达)
    ///   [35..64] 墓地 Graveyard (30 = v10 4→30 全量，新死优先：slot35=最新入墓；超出 30 截断)
    ///   [65..67] 发动区 Activation (3，公开区/反制指向点；第 4+ 张不可编码)
    ///   [68]     场地区 FieldZone (1，英雄技能卡)
    ///   [69..76] 除外区 Exile (8 = v10 逐卡入册，区内时序 index0=最先放逐；超出 8 截断)
    /// v9（2026-11-02 牌库双通道）：尾接己方牌库块 [154..183] 共 30 槽（仅决策座次方，无序集合
    /// 按内容哈希排序编码——见 WriteOwnDeck；对方牌库不进 cards_，走 g[50..57] 开局标签通道）。
    /// v21（2026-11-24 卡组分布与策略分界）：牌库块语义改「每种唯一卡一槽 + 卡特征 [157] 张数」
    ///（30 槽=唯一卡种类上限，总张数不再影响可见性）；己方牌库开局标签进 g[88..95]、
    /// 牌库唯一卡超 30 种的截断标记 g[96]。
    /// v10（2026-11-02 观测欠账四件套）：+牌库顶槽 [184]（光环命中才编码，防泄露下一抽）
    /// +对方牌库被展示卡块 [185..188]（RevealRules 信息轴）；对方手牌被展示卡豁免为全特征开图。
    /// 对方手牌是隐藏信息：只写 valid+controller（不开图），张数在 globals g[6]。
    ///
    /// 位置特征 [66][67]：单位格=落位钉子+first-free（槽序≠格序，必须显式编码；
    /// 摧压溅射=左右同排）；地牌格=按色固定（LandCellOfElement——格坐标即锁色）。
    /// board=null 时单位位置降级 -1（不炸）；地牌格是纯几何不需 board。
    /// 地牌横置态在 PooledCard 包装上（Card.IsTapped 恒 0，v2 失效维——本版从包装读）。
    /// </summary>
    public sealed class TideObservation
    {
        // ---- 维度常量（Python 侧只改这些，模型不动）----
        // v11（2026-11-05 充能系统动作化）：动作空间语义变更——Charge 行从「带充能效果的卡逐色
        // 一行（Color 承载选色）」改为「任意手牌卡每卡一行（免元素、选色移交引擎结算期反问，
        // AI 代取最大余量色）」；充能从主动效果入栈改为速度0系统动作。观测布局（槽位/特征数）不变，
        // 旧 v10 模型对充能行的语义不符作废（python LAYOUT_VERSION 同步升 11）。
        // v10（2026-11-02 观测欠账四件套）：卡槽 116→189——墓地区 4→30 全量（新死优先保留）、
        // 除外区逐卡 8 槽入册（连带修复 LAE 目标槽位断链：除外回收/深墓地卡 IndexOfCard 原恒 -1）、
        // 对方手牌被展示（Exposed）卡豁免为全特征开图、尾块 +牌库顶槽 1（TopDeckAsHandAura
        // 光环命中才编码——防泄露下一抽）+对方牌库被展示卡 4（RevealRules 信息轴接入）。
        // 每玩家槽 43→77、DeckBase 86→154。NCard/NGlobal 不变（zoneKind 值扩 7/8/9）。
        // 旧 v9 模型布局不符作废（python MAX_CARDS/LAYOUT_VERSION 同步升 10）。
        // v9（2026-11-02 牌库双通道）：卡槽 86→116（尾接己方牌库块 [86..115] 共 30 槽，仅决策
        // 座次方、按内容哈希排序编码防泄露洗牌顺序）+ 卡特征 115→116（尾加 [115] zoneKind——
        // 逐卡编码器无槽位位置嵌入，不加则牌库卡与手牌卡特征同形不可区分）+ globals 50→58
        //（尾加 g[50..57] 对方牌库开局标签：GameCore.OriginalDeckOf 快照，整局恒定）。
        // 旧 v8 模型布局不符作废（python MAX_CARDS/N_CARD_FEATURES/N_GLOBAL_FEATURES/LAYOUT_VERSION 同步升 9）。
        // v8（2026-11-02 LAE 多目标动作空间）：动作特征 8→11 维——追加 [8]target2Index/[9]target3Index/
        // [10]nTargets（多目标组合行次目标可见；逐原子 effCount 选 N 与 EmitCombinations 组合行可区分）。
        // 旧 v7 模型布局不符作废（python LAYOUT_VERSION 同步升 8、N_ACTION_FEATURES 8→11）。
        // v7（2026-10-29 对座镜像修订）：盘面坐标轴平移——双方六格行同列带 x1..x6、P2 改
        // 上下镜像像（槽位数/容量与 v6 一致，位置特征 posX/posZ 分布位移；python LAYOUT_VERSION 同步升 7）。
        // v6（2026-10-29 P11 盘面重塑）：战场槽 12→9（单位区 4+5）——每玩家 46→43 槽、
        // 后续区域槽位整体前移 3；位置特征归一化分母随 BoardLayout.Width 8→7。
        // 旧模型权重作废（python LAYOUT_VERSION 同步升 6）。
        // v5（2026-10-29）：删卡特征 [113]——NCard 116→115、N_GLOBAL 不变。
        // v12（2026-11-06 墓碑清理）：TideActionType 空洞移除连续重排——动作特征[1] type 数值漂移，
        // 旧模型作废（python LAYOUT_VERSION 同步升 12）。
        // v13（2026-11-08 目标选择重构步5）：动作特征 11→12 维——[11]referenceIndex（参照角色选位
        // 镜像，Reference Enumerate spec 进动作空间）；旧模型作废（python LAYOUT_VERSION 同步升 13）。
        // v14（2026-11-15 目标区域共享规格 v0.2 §6）：动作特征 12→14 维——[12]targetZoneRank
        //（首目标区域序，TargetZoneGrouping.ZoneRankOf 单源）/ [13]targetSideRank（归属，枚举者视角）；
        // 训练未开始、无兼容负担；旧 ONNX/fixture 作废（python LAYOUT_VERSION/N_ACTION_FEATURES 同步升 14）。
        // v15（2026-11-18 场地特殊化 §P7）：场地卡拆出战场单位槽——卡槽 189→191（尾部专用块
        // [189]=己方场地卡/[190]=对方场地卡，zoneKind=10）；战场槽 [0..8] 只编码单位（剔场地，
        // 槽序=单位序）；g[7]/g[8] 只数单位。旧 checkpoint/ONNX/fixture 全部作废（python
        // MAX_CARDS/LAYOUT_VERSION 同步升 15）。
        // v16（2026-11-19 P-1 稳定 ID 派生下标 C 方案）：身份下标语义换血——卡身份池
        // 1024→16384（TideCardIndex 稳定派生）、EffectType 池 256→4096（CardIdentityService
        // 稳定派生）、「未登记=0」语义废弃（0 只表示 hash=0）。观测/动作特征**维度不变**
        //（卡 116/动作 14/globals 58），但下标取值分布与 embedding 表大小变化——旧
        // checkpoint/ONNX/fixture 全部作废；OnnxTidePolicy 按 pool size 拒旧模型（P-1 §0.4.3）。
        // v17（2026-11-19 P1 v0.3 移动/宣言）：动作特征 14→**17** 维——+[14]moveCellX/[15]moveCellZ
        ///（Interaction.MoveX/Z，-1=无）、[16]declaredNameIndex（宣言候选序号 0..15，-1=无）。
        // 移动行=目标×合法空格（MaxMoveRows=18）、宣言行=候选名（MaxDeclareRows=16，只读已读到的
        // 卡名——DeclarationCandidates 单源）；UnsupportedForAi 对 MoveUnit/SearchDeck/DeclareHand/
        // DeclareDeck 的裁剪删除。旧 checkpoint/ONNX/fixture 全部作废（v0.3 原文的 v16 号已被 P-1 占用）。
        // v18（2026-11-19 P2 永久物落格）：动作特征 17→**18** 维——+[17]placementCell（己方
        // UnitCells 格下标 0..8，-1=非落格行）。永久物（生物/结界）PlayCard/RespondPlay 行×空格
        // 逐格展开（MaxPlaceRows=9），Apply 声明成功即 PinCard（镜像 SimpleAI.TryPinPlacement 时点）。
        // 旧 checkpoint/ONNX/fixture 全部作废。
        // v19（2026-11-20 绿套补盲，docs/AI绿套学习与观测补盲_修改计划.md）：globals 58→**70**
        //——尾加 g[58..69] 剩余可用元素块（己方四色/Any/总量 + 对方四色/Any/总量——「还差几点出
        // 大哥」「对手能不能响应」可算；牺牲/蛰伏的 +1 Any 收益首次可见）；卡特征 116→152
        //（原子参数块 AtomParamDim 6→9：+settleKind/branchParam/branchBudget/costTotal 四槽，
        // 删恒 0 的 Mana 两死维并修掉 dim=6/写 7 值的错位；分支身份进原子哈希）；同日晚批
        //死亡计数原子属性化（分支挂载退役）再补死亡计数维 [152]→卡特征 **153**。
        // 旧 checkpoint/ONNX/fixture 全部作废（v18 模型按 dims 拒载→回落 SimpleAI，重训后恢复）。
        // v20（2026-11-20 AI 召唤落位+团结观测，docs/AI召唤落位与团结观测_修改计划.md §3.3/§3.4/§5）：
        // 动作 18→**20**——[17]placementCell 扩义「召唤计划行=计划首格（含 Activate 行——类型闸
        // 修复，此前 9 个锚点行被 FoldEquivalentRows 误并）」；+[18]summonPlan（0=默认/1..9=锚点/-1）、
        // +[19]summonUnityHits（该计划落位后团结达标数 0..6/-1，PredictUnityHits 单源；拍板保留）。
        // 卡特征 153→**157**——+[153] 邻接己方生物数/6（团结口径=只数活生物，与
        // BoardState.AdjacentOccupantCount 同过滤）、+[154..156] 引擎奖励摘要
        // （CardIdentityService.EngineRewardOf：载荷数/首奖励原子类型/首奖励值——**不进身份哈希**）。
        // globals 70→**88**——+g[70..78] 己方 9 单位格占用码（0=空/1=己/2=敌，/2）、
        // +g[79..87] 各格 6 邻己方生物数/6（团结口径；SummonPlacementRules.AdjacentOwnCreatures 单源）。
        // 无棋盘（服务器/纯核心）各块写 0 不炸。Python LAYOUT_VERSION/N_CARD_FEATURES/
        // N_GLOBAL_FEATURES/N_ACTION_FEATURES 同步 20/157/88/20（副本随刷新）；旧 checkpoint/
        // ONNX/fixture 全部作废（按 dims 拒载回落 SimpleAI）。
        // v21（2026-11-24 观测·卡组分布与策略分界，docs/观测_卡组分布与策略分界_实施方案.md）：
        // 己方牌库块 [154..183] 语义换血——由「每张物理卡一槽」改「**每种唯一卡一槽 + 张数**」：
        // DeckSlots=30 仍是槽总数，但口径变**唯一卡种类上限**（总张数不再影响可见性——此前绿 60 张
        // 经 30 槽截断只编码出 2 种卡）；同内容副本合并为一组，组内张数写卡特征 [157] countInZone
        //（其它区恒 1）；唯一卡 >30 种按排序键截断并在 g[96] 标记（不静默）。
        // 卡特征 157→**158**（+[157] countInZone）；globals 88→**97**——+g[88..95] 己方牌库开局
        // 标签（DeckTagsOf(OriginalDeckOf(me))，与对方 g[50..57] 同函数同口径）+g[96] deckTruncated。
        // 动作特征 20 不变。旧 checkpoint/ONNX/fixture 全部作废（按 dims 拒载回落 SimpleAI）；
        // 同批（维度不变、身份换血）：生物攻/守资格按「带发动条件的主动效果」进内容身份——原子槽
        // [10]=攻击/[11]=守卫、结构哈希含其骨架、卡特征 [7] 可攻击粗判加攻资格闸（此前火灵·攻/守
        // 仅攻守资格不同、观测逐位相同——牌库块按特征行分组时会被并成一种卡，红·快攻只见 4 种）。
        // 死亡计数 [152] 同批改原始层数（2026-11-24：手牌累计无上限 + 计价改基价 +2 开启费——
        // 旧 /9 归一化与 ×4 计价口径随满值口径退役）。
        // Python LAYOUT_VERSION/N_CARD_FEATURES/N_GLOBAL_FEATURES 同步 21/158/100（v21 定稿 2026-11-24：
        // globals 97→100，+手牌上限三槽 g[97..99]——见 NGlobal 尾加注释）。
        // A6（2026-11-24 逐目标单选·同日，docs/回到一期训练目标.md 阶段 A6）：动作特征 **20 维不变、
        // 语义变更**——[8]/[9] 次目标维废止恒 -1（组合行退役）；[10] 由 nTargets 改 **PicksRemaining
        // 剩余待选数**（0=无目标/提交行、1=单步行/最后一步、N=纯固定计划剩余步数、-1=计划含可变
        // 数量 spec）。多目标效果改「每步选 1 个目标」（TargetPickGate 累积，提交按计划序拼全集）。
        // v22（2026-11-26 二期 S2a，docs/二期训练架构_实施计划.md §4——时域/粒度修复）：动作特征
        // 20→**26**——+[20]pickMode（0=普通行/1=选项提交行〔macro：多步拾取计划的一次性完成行，
        // Targets=空、[10] 语义保持，TargetPickGate.OpenAndAutoComplete 按共享序选满提交〕/
        // 2=闸内选项步〔step 路径诊断〕；进 LAE.Signature 防误摘）+[21..24] 效果前 4 个原子的
        // AtomTypeIndex/4096 +[25] 原子数/16；同批 PlayLand 折叠键并来源卡身份（C8）。模式开关
        // reset 选项 picksMode=macro（默认）/step/both。卡特征 158/卡槽 191/globals 100 不变；
        // 动作编码器输入 276→282——旧 checkpoint/ONNX/fixture 全部作废（热启动走
        // merge_params_by_shape 并树，v21 动作编码器 kernel 变 reshaped）。
        public const int LayoutVersion = 22;
        public const int MaxCardsPerPlayer = 77; // v10：43→77（墓地 4→30、除外 8 入册）
        // v9 己方牌库块（尾接全局块，不走 ZoneLayout 每玩家偏移语义——专用 DeckBase+排序名次路径）：
        // v21 起槽=**唯一卡种类**上限（每种唯一卡一槽、[157] 写张数）——超过 30 种按排序键截断
        //（g[96]=1 标记）；v20 及以前=物理卡截断护栏（初始整组 30 恰满；衍生物入库超出不编码）。
        public const int DeckSlots = 30;
        public const int DeckBase = MaxCardsPerPlayer * 2;                    // v10：86→154
        public const int DeckTopSlot = DeckBase + DeckSlots;                  // v10 [184]：牌库顶虚拟手牌（光环命中才编码）
        public const int OppDeckExposedSlots = 4;                             // v10：对方牌库被展示卡截断护栏
        public const int OppDeckExposedBase = DeckTopSlot + 1;                // v10 [185..188]
        // v15 场地专用块（尾部追加）：每方 1 槽——[189]=枚举者视角己方场地卡 / [190]=对方场地卡。
        public const int FieldBase = OppDeckExposedBase + OppDeckExposedSlots; // v15 [189]
        public const int FieldSlots = 2;                                       // 己方/对方各 1（无场地=空槽恒 0）
        public const int MaxCardsTotal = FieldBase + FieldSlots;               // v15：189→191
        // 卡特征布局（内容身份三通路，[15..] 由 CardIdentityService 推导；槽位下标全部由
        // AtomIdSlots 派生——扩容只改 AtomIdSlots 一处，python N_CARD_FEATURES 同步）：
        //   [0..13]  状态特征（valid/controller/超类/力量/生命/费用/横置/关键词/指示物…实时值）
        //   [14]     LandTrait（v3 复用神佑死维：TapCreature 资格与威胁预测）
        //   [15..26] 原子精确内容哈希下标（跨效果按执行序展平，超出 12 个截断；参数敏感。
        //            容量依据 2026-10-16：正式卡满配 9 = 代价1 + 主动/自动组合各按2 + 关键词3，留3槽余量。
        //            v21：生物的 [10]=攻击资格原子 / [11]=守卫资格原子（战斗底盘——卡面声明的攻/守
        //            资格按「带发动条件的主动效果」入身份，CardIdentityService.ChassisAttackSlot/GuardSlot
        //            预留位，未声明=0），效果原子占 [0..9]；非生物两槽恒 0）
        //   [27]     组合结构哈希下标（时点/条件/代价/Steps 骨架 =「怎么组合」；v21 起含生物攻/守
        //            资格的合成效果骨架——攻击=主要阶段主动、守卫=响应拦截）
        //   [28]     关键词/tag/光环组合哈希下标
        //   [29..40] 原子 EffectType 下标（原子表内排名，参数无关；[10]/[11]=Attack/Guard 类型行，
        //            有攻击/守卫资格的生物各共享同一行）
        //   [41..148] 原子参数浮点块（12 槽 × CardIdentityService.AtomParamDim=9 维；v19 布局见其头注释：
        //            value(引擎门槛x)/settleKind/branchParam/branchBudget/kinds三槽/amp/costTotal）
        //   [149][150] 格坐标 x/Width、z/Height（未落格 = -1）
        //   [151] zoneKind；[152] 死亡计数原始层数（2026-11-20 原子属性化补 1 维；2026-11-24 手牌累计
        //            无上限、去 /9 归一化——旧满值口径退役；关/无=0）
        //   [157] countInZone（v21：该槽代表的同特征卡张数；己方牌库块=组内张数，其余区恒 1）
        // 精确哈希 = 冻结的查表身份；类型 + 参数 = 泛化通路。0 = 无该成分 / token / 未登记。
        public const int HashIdStart = 15;
        public const int AtomIdSlots = CardIdentityService.ChassisGuardSlot + 1; // =12。2026-10-16 6→12（正式卡满配9+余量3）；v21 经底盘预留槽派生（[10]攻击/[11]守卫），观测必覆盖到它们
        public const int StructureSlot = HashIdStart + AtomIdSlots;            // [27] 组合结构哈希
        public const int CompositionSlot = StructureSlot + 1;                  // [28] 关键词/tag/光环组合哈希
        public const int TypeIdStart = CompositionSlot + 1;                    // [29..40] 类型下标槽
        public const int ParamStart = TypeIdStart + AtomIdSlots;               // [41..] 参数块
        public const int PosXSlot = ParamStart + AtomIdSlots * CardIdentityService.AtomParamDim; // [113]
        public const int PosZSlot = PosXSlot + 1;                              // [114]
        // v9 区域种类 [151]：显式映射（不依 Zone 枚举序，防重排）——0=战场 1=元素池 2=手牌 3=墓地
        // 4=发动区 5=场地区 6=己方牌库块；v10 扩 7=牌库顶虚拟手牌 8=对方牌库被展示 9=除外区；
        // v15 扩 10=场地卡（场地专用槽 WriteFieldCard 直写；战场槽仍 0=只含单位）。
        // 对方隐藏手牌=2（WriteConcealedCard；被展示卡豁免为全特征，仍写 2=手牌）。
        // v19：[115]→[151]（参数块 6→9 维整体后移；语义不变）。
        public const int ZoneKindSlot = PosZSlot + 1;                          // [151]
        // 死亡计数维 [152]（2026-11-20 原子属性化补 1 维；2026-11-24 口径更新）：该卡
        // DeathTollCounter **原始层数**（指示物口径，与 state 段毒素 [9] 同族——v21 起手牌累计
        // 无上限，旧 /9 归一化随满值口径退役）。死亡计数增幅=原子属性 deathToll（与 rand/defer
        // 同级），手牌累计、进战场冻结、离场清零——计数实时值此前完全不可见（模型只能从伤害反推）。
        public const int DeathTollSlot = ZoneKindSlot + 1;                     // [152]
        // 邻接维 [153]（v20 2026-11-20 计划 §3.4 团结观测）：战场卡所在格的 6 邻**己方生物数** /6
        //（团结口径——只数 IsAlive∧IsLivingUnit∧同 controller，结界/角色格/地牌格/敌方不计；
        // SummonPlacementRules.AdjacentOwnCreatures 与 BoardState.AdjacentOccupantCount 同过滤）。
        // 团结卡参数块 branchParam=x 已在，本维减 x=触发差可算；非战场卡=0。分母 6=六邻上限。
        public const int AdjacencySlot = DeathTollSlot + 1;                    // [153]
        // 引擎奖励摘要 [154..156]（v20 计划 §3.4，CardIdentityService.EngineRewardOf 只读——**不进
        // AtomHash/ContentId**，身份哈希仍 v19 口径）：[154] 奖励载荷数（branch.then 原子数+effectId
        // 计 1，钳 3）；[155] 首奖励原子 AtomTypeIndex（effectId 引用=0；无=0）；
        // [156] 首奖励原子 value（钳 99）。团结/引擎卡「达标后给什么」首次可见。
        public const int EngineRewardCountSlot = AdjacencySlot + 1;            // [154]
        public const int EngineRewardType0Slot = EngineRewardCountSlot + 1;    // [155]
        public const int EngineRewardValue0Slot = EngineRewardType0Slot + 1;   // [156]
        // 同区张数维 [157]（v21 2026-11-24 卡组分布与策略分界）：该槽代表的**特征行全同**卡张数——
        // 己方牌库块=该唯一卡组内张数（原始计数不归一化，钳 99，与费用 [5] 同族；v21 起牌库块
        // 每种唯一卡占一槽，张数在此读）；其余各区（手牌/战场/墓地/牌库顶/…）恒 1。
        // 不变量：valid=1 ⇔ countInZone≥1；空槽/对方隐藏手牌以外恒 0 的槽只有空槽。
        public const int CountSlot = EngineRewardValue0Slot + 1;               // [157]
        public const int NCard = CountSlot + 1; // v19：116→153（+死亡计数维）；v20：→157（+邻接/奖励摘要 4 维）；v21：→158（+同区张数维）；python N_CARD_FEATURES 同步
        // globals v3（50 维，契约 §5——重排序+归一化+放逐计数+弹药维；v2 g[48] 冗余删除）：
        //   g[0..16] 双方生命/30、回合/40、相位、座次、各区计数（墓地/30、牌库/60、放逐新）；
        //   g[17..28] 临时钱包 12 维；g[29] 栈占用；g[30..31] 势能 Φ/30；g[32..33] 地牌上限/14；
        //   g[34..37] 黑白已获得/14；g[38..45] 双方英雄技能块（P1 绝对序）[has,tapped,uses/10,弹药/5]；
        //   g[46..48] 栈深度/16、栈顶来源/MaxCardsTotal、栈顶 effectIdentity/1024；g[49] 决策上下文。
        // v9 尾加 g[50..57]（对方牌库开局标签快照，DeckTagsOf——随卡组创建保留、整局恒定）。
        // v19 尾加 g[58..69] 剩余可用元素块（2026-11-20 绿套补盲；视角与 g[0]/g[1] 一致 me/opp）：
        //   g[58..61] 己方剩余可用元素 灰/红/蓝/绿（/10；=该色钱包余额+该色未横置地牌张数）
        //   g[62]     己方 Any 彩元素（/10；牺牲/蛰伏入账的临时彩，单独成维——收益首次可见）
        //   g[63]     己方剩余可用总量（/20；GetRemainingElementTotal=四色+Any 钱包+未横置地牌）
        //   g[64..67] 对方剩余可用元素（同四色，/10）；g[68] 对方 Any（/10）；g[69] 对方总量（/20）
        //   ——「还差 2 点就能出 14 费大哥」「对手能不能响应/斩杀」可算；归一化上限参照
        //   充盈差值封顶 6/地牌上限 14 的既有量级。Python N_GLOBAL_FEATURES 同步 70。
        // v20 尾加（2026-11-20 计划 §3.4 召唤落位/团结观测，Python 同步 88）：
        //   g[70..78] 己方 9 单位格（UnitCells 下标序）占用码（0=空/1=己方/2=敌方，/2；无棋盘=0）
        //   g[79..87] 上述 9 格各自的 6 邻**己方生物数** /6（团结口径——只数活生物；
        //             空格邻接不可从卡特征导出，落格/召唤计划行（[17] 格下标）价值直读）
        // v21 尾加（2026-11-24 卡组分布与策略分界；Python 同步 97）：
        //   g[88..95] 己方牌库开局标签（DeckTagsOf(OriginalDeckOf(me))——与对方 g[50..57] 同函数
        //             同口径、同为整局恒定的入局快照；「我是哪套」显式可见，此前只有对方有）
        //   g[96]    deckTruncated（己方牌库唯一卡种类 >30、牌库块被截断时=1；否则 0——不静默）
        // v21 定稿尾加（2026-11-24 回到一期训练目标 B0.3 拍板：globals 100，不再讨论 v22；Python 同步 100）：
        //   g[97]    己方手牌上限（RuleHooks.GetHandLimit 单源线性计数——修改链如纳川仪典的
        //            额外上限直接可见；A0 逐张弃配套：弃到几时停可算）
        //   g[98]    对方手牌上限（同上）
        //   g[99]    己方超上限**剩余待弃张数**（弃牌进行中=HandLimitDiscardGate 快照剩余；
        //            否则己方回合 max(0, 有效手牌−上限)；对手回合恒 0——对手回合超限走
        //            ZoneContainer 入手闸改投除外区，不产生弃牌步）
        public const int NGlobal = 100; // v19：58→70（+12 元素块）；v20：→88（+9 占用码+9 格邻接）；v21：→97（+8 己方牌库标签+1 截断标记）；v21 定稿（2026-11-24）：→100（+3 手牌上限三槽 g[97..99]）
        public const int DeckTagStart = 50; // v9 g[50..57] = DeckTagsOf（对方牌库开局标签）
        public const int OwnDeckTagStart = 88; // v21 g[88..95] = DeckTagsOf（己方牌库开局标签）
        public const int DeckTruncatedSlot = OwnDeckTagStart + 8; // v21 g[96]：己方牌库块唯一卡超 DeckSlots 种被截断=1
        // v21 定稿（2026-11-24 B0.3）手牌上限三槽（A0 逐张弃配套，见上方尾加注释）
        public const int HandLimitSelfSlot = DeckTruncatedSlot + 1;  // g[97]
        public const int HandLimitOppSlot = DeckTruncatedSlot + 2;   // g[98]
        public const int PendingDiscardSlot = DeckTruncatedSlot + 3; // g[99]
        // 动作特征槽（v8 8→11 维；v13 11→12；v14 12→14；v17 14→17=P1 v0.3 移动/宣言）：
        // [0]valid [1]type [2]sourceIndex [3]targetIndex [4]modeIndex
        // [5]actionCost [6]effectIdentity [7]targetKind + v8 多目标追加：
        // [8]target2Index [9]target3Index（Targets[1]/[2] 槽位，-1=无/玩家目标/>3 截断不编码）[10]nTargets（0..16 钳；P0 2026-11-19 由 0..8 提升为 0..16，prefix-k 行 k=9..16 可分）
        // + v13 参照追加：[11]referenceIndex（Reference 角色选位镜像槽位，-1=无/玩家参照）
        // + v14 目标区域追加：[12]targetZoneRank（-1=无目标；角色0→其他9，ZoneRankOf 单源）
        // [13]targetSideRank（-1=无目标；0=己方/1=对方，枚举者视角）——只编码首目标
        //（多目标经 target2/3 槽位+观测 zoneKind 分辨，与「只编码前 3 目标」同口径）。
        // + v17 P1 v0.3 追加：[14]moveCellX / [15]moveCellZ（Interaction.MoveX/Z，-1=无/非展开行）
        // [16]declaredNameIndex（宣言候选序号 0..15，同效果内可比；-1=非宣言行）。
        // + v18 P2 追加：[17]placementCell（永久物落格=己方 UnitCells 格下标 0..8；-1=非落格行）。
        // + v20（2026-11-20 计划 §3.3/§5）扩义+两维：[17] 兼**召唤计划行=计划首格**（含
        //   Activate/RespondActivate 行——类型闸修复）；+[18]summonPlan（0=默认/1..9=锚点/-1=非召唤）、
        //   +[19]summonUnityHits（该计划落位后团结达标数 0..6/-1——与 [5]actionCost 同层非观测泄露，
        //   2026-11-20 拍板保留）。
        // + v22（2026-11-26 二期 S2a）动作语义补盲三组：[20]pickMode（0=普通行/1=选项提交行〔macro，
        //   选中后 OpenAndAutoComplete 按共享序一次选满提交〕/2=闸内选项步〔诊断〕——多步拾取折叠
        //   为选项行 = 时域/粒度修复：链折叠后跨度 35→22 步、每局步数 40.4→28.4）、
        //   [21..24]=效果前 4 个原子的 AtomTypeIndex/4096（不足补 0；无效果行全 0——[6]effectIdentity
        //   是 1024 池标量，玩家自组卡组时近似随机数，原子类型让「这条行在做什么」对未见卡可读）、
        //   [25]=原子数/16（无效果=0）。
        public const int NAction = 26;

        public float[] Cards = new float[MaxCardsTotal * NCard];
        public float[] Globals = new float[NGlobal];

        private Player _me;

        /// <summary>从核心构建快照（me = 当前回合玩家；Main 决策点视角——无棋盘降级入口）。</summary>
        public static TideObservation Build(GameCore core)
            => Build(core, core?.TurnEngine?.TurnPlayer, false);

        /// <summary>从核心构建快照（v3 视角参数化）：seat = 当前决策座次——
        /// Main 决策点 = 回合玩家；响应窗口停靠 = 优先权持有方（可能是非回合玩家）。
        /// 隐藏信息（对方手牌）按该 seat 的对手计算；g[0..35] 语义全部相对 seat。
        /// responseContext：停靠类型（g[49]，0=己方 Main / 1=响应窗口）。
        /// board：棋盘占用层（v3 位置特征；null=单位格降级 -1，地牌格仍可编码——纯几何）。</summary>
        public static TideObservation Build(GameCore core, Player seat, bool responseContext = false,
            BoardState board = null)
        {
            var obs = new TideObservation();
            if (core == null || core.TurnEngine == null || seat == null)
                return obs;
            var opp = seat.Opponent;
            if (opp == null) return obs;
            obs._me = seat;

            obs.WritePlayer(core, seat, 0, concealHand: false, board);
            obs.WritePlayer(core, opp, MaxCardsPerPlayer, concealHand: true, board);
            obs.WriteOwnDeck(core, seat);        // v9：己方牌库块（仅决策座次方）
            obs.WriteDeckTop(core, seat);        // v10：牌库顶虚拟手牌槽（光环命中才编码——防泄露下一抽）
            obs.WriteOppDeckExposed(core, opp);  // v10：对方牌库被展示卡尾块（RevealRules 信息轴）
            obs.WriteFieldCard(core, seat, FieldBase);      // v15：场地专用槽 [189]=己方（无场地=空槽）
            obs.WriteFieldCard(core, opp, FieldBase + 1);   // v15：[190]=对方
            obs.WriteGlobals(core, seat, opp, responseContext, board); // v20：格邻接块带板
            return obs;
        }

        /// <summary>场地卡专用槽下标（v15 拆场地）：controller 相对 me——己方 [189] / 对方 [190]。
        /// LAE 攻击场地行/IndexOfCard 反查共用本口。</summary>
        public static int FieldSlotOf(Player me, Player controller)
            => controller != null && !ReferenceEquals(controller, me) ? FieldBase + 1 : FieldBase;

        /// <summary>某卡在 cards_ 中的槽位下标（controller 相对 me；未编码区/超出区域容量返回 -1）。</summary>
        public static int CardIndex(Player me, Player controller, Zone zone, int indexInZone)
        {
            if (!ZoneLayout(zone, out int off, out int cap) || indexInZone < 0 || indexInZone >= cap) return -1;
            int baseSlot = ReferenceEquals(controller, me) ? 0 : MaxCardsPerPlayer;
            return baseSlot + off + indexInZone;
        }

        /// <summary>卡实例 → cards_ 槽位下标：动作行目标/栈顶来源的反查——扫描双方全部已编码区
        ///（v9 起含己方牌库块、v10 起含除外区；未编码区如对方牌库/衍生物临时区返回 -1）。
        /// v15 拆场地：战场场地卡先路由到场地专用槽（先于区扫描）；战场单位序=剔场地后名次。
        /// v21：牌库块=唯一卡分组槽——同组（特征行全同）任一张都映射到该组槽位（见 TryDeckSlot）。
        /// 仅枚举/观测辅助用，非热路径权威。</summary>
        public static int IndexOfCard(GameCore core, Player me, Card c)
        {
            if (core?.ZoneManager == null || me == null || c == null) return -1;
            if (c.IsFieldCard() && c.GetZone() == Zone.Battlefield)
                return FieldSlotOf(me, c.GetController()); // v15：场地专用槽（不占战场单位槽）
            int slot = TryZoneSlot(core.ZoneManager, me, c, 0);
            if (slot >= 0) return slot;
            slot = TryDeckSlot(core, me, c); // v9：己方牌库块（v21：组序对齐编码序）
            if (slot >= 0) return slot;
            var opp = me.Opponent;
            return opp != null ? TryZoneSlot(core.ZoneManager, opp, c, MaxCardsPerPlayer) : -1;
        }

        /// <summary>己方牌库块反查（v9 起；v21 改分组语义）：牌库卡在 [DeckBase..] 的槽位 =
        /// 它所在唯一卡组的组序（与 WriteOwnDeck 同享 GroupDeckForEncoding 单源——同组任一张
        /// 映射同一槽；所在组超出 DeckSlots 种被截断或对方牌库卡恒 -1）。
        /// 快速路径：c 不在己方牌库列表即返回（对方卡/其它区卡的反查不必跑整库分组——
        /// IndexOfCard 对每个对方目标都会走到这里，分组要算整库特征行，不能无脑跑）。</summary>
        private static int TryDeckSlot(GameCore core, Player me, Card c)
        {
            var cards = core.ZoneManager.GetCards(me, Zone.Deck);
            if (cards == null) return -1;
            int at = cards.IndexOf(c);
            if (at < 0) return -1;
            GroupDeckForEncoding(core, me, cards, cards[at], out int group);
            return group >= 0 && group < DeckSlots ? DeckBase + group : -1;
        }

        private static readonly Zone[] EncodedZones =
            { Zone.Battlefield, Zone.ElementPool, Zone.Hand, Zone.Graveyard, Zone.Activation, Zone.FieldZone,
              Zone.Exile }; // v10：除外区入册（IndexOfCard/CardIndex 自动覆盖——除外回收目标槽位修复）

        private static int TryZoneSlot(ZoneManager zm, Player p, Card c, int baseSlot)
        {
            foreach (var zone in EncodedZones)
            {
                var cards = zm.GetCards(p, zone);
                if (cards == null) continue;
                int idx;
                if (zone == Zone.Battlefield)
                {
                    // v15 拆场地：战场槽=单位序（剔场地——与 WriteZone 编码序对齐；IndexOf 会算进场地）
                    idx = -1;
                    int unitIdx = 0;
                    foreach (var card in cards)
                    {
                        if (ReferenceEquals(card, c)) { idx = unitIdx; break; }
                        if (card != null && !card.IsFieldCard()) unitIdx++;
                    }
                }
                else idx = cards.IndexOf(c);
                if (idx >= 0 && ZoneLayout(zone, out int off, out int cap) && idx < cap)
                    return baseSlot + off + idx;
            }
            return -1;
        }

        /// <summary>区域 → (槽位基址, 容量)（未编码区返回 false）。v6 对齐引擎容量（P11 盘面重塑）：
        /// 战场 9=BattlefieldCapacityPerPlayer（单位区 4+5）、元素池 14=地牌槽编码容量护栏（2026-10-15 规则硬顶退役后
        /// 仅编码上限，超出部分照常参与规则只是不可编码）、手牌 12=回合中溢出护栏
        ///（回合末上限 7 由 globals 表达；第 13+ 张动作仍可执行，只是来源槽不可编码——防串区）、
        /// 墓地 30（v10 4→30 全量，新死优先；超出截断）、发动区 3、场地区 1（技能卡）、
        /// 除外 8（v10 入册，区内时序 index0=最先放逐=UI Region 面板同序；超出截断）。
        /// 容量变更须先改契约文档再改这里。</summary>
        public static bool ZoneLayout(Zone zone, out int offset, out int cap)
        {
            switch (zone)
            {
                case Zone.Battlefield: offset = 0; cap = 9; return true;
                case Zone.ElementPool: offset = 9; cap = 14; return true;
                case Zone.Hand: offset = 23; cap = 12; return true;
                case Zone.Graveyard: offset = 35; cap = 30; return true; // v10：4→30
                case Zone.Activation: offset = 65; cap = 3; return true; // v11-B（S3b 删发动区）：槽保留恒零——布局不变免重训
                case Zone.FieldZone: offset = 68; cap = 1; return true;  // v10：42→68
                case Zone.Exile: offset = 69; cap = 8; return true;      // v10 新增
                default: offset = -1; cap = 0; return false;
            }
        }

        // =====================================================

        private void WritePlayer(GameCore core, Player p, int baseSlot, bool concealHand, BoardState board)
        {
            // 布局归属（LandCellOfElement 的 player 下标）：Player1=下半场 0
            int playerIdx = ReferenceEquals(p, core.Player1) ? 0 : 1;

            foreach (var zone in EncodedZones)
            {
                if (!ZoneLayout(zone, out int off, out int cap)) continue;
                bool conceal = concealHand && zone == Zone.Hand;
                WriteZone(core, p, baseSlot + off, zone, cap, conceal, board, playerIdx);
            }
        }

        private void WriteZone(GameCore core, Player p, int baseSlot, Zone zone, int cap,
            bool conceal, BoardState board, int playerIdx)
        {
            var zm = core.ZoneManager;
            var layer = core.LayerEngine;
            var cards = zm.GetCards(p, zone);
            if (cards == null) return;

            // v15 拆场地：战场槽只编码单位（剔场地后按原序——槽序=单位序，槽数恒 9）；
            // 场地卡走 WriteFieldCard 专用槽（zoneKind=10）。
            if (zone == Zone.Battlefield)
            {
                var units = new List<Card>(cards.Count);
                foreach (var c in cards)
                    if (c != null && !c.IsFieldCard()) units.Add(c);
                cards = units;
            }

            // 元素池地牌：横置态与锁色在 PooledCard 包装上（Card.IsTapped 恒 0——v2 失效维，v3 从包装读）
            Dictionary<Card, PooledCard> pooledOf = null;
            if (zone == Zone.ElementPool)
            {
                pooledOf = new Dictionary<Card, PooledCard>();
                var pooled = core.ElementPool.GetPooledCards(p);
                if (pooled != null)
                    foreach (var pc in pooled)
                        if (pc?.SourceCard != null) pooledOf[pc.SourceCard] = pc;
            }

            for (int i = 0; i < cap; i++)
            {
                // 墓地新死优先（slot0=最新入墓）——最近的死亡最影响决策（苏生源/威胁推理）
                int idx = zone == Zone.Graveyard ? cards.Count - 1 - i : i;
                if (idx < 0 || idx >= cards.Count) continue; // 空槽保持全 0（valid=0）

                var c = cards[idx];
                // v10 展示豁免（RevealRules 信息轴）：对方手牌被展示（Exposed 指示物，换区清除）的卡
                // 对双方公开——全特征开图（玩家口径：UI 翻正面/网络快照同源下发）；未展示保持隐藏
                if (conceal && !RevealRules.IsExposed(c)) { WriteConcealedCard(baseSlot + i); continue; }

                float posX = -1f, posZ = -1f;
                bool tappedOverride = false;
                float adjOwn = 0f; // v20 团结观测 [153]：战场卡 6 邻己方**生物**数/6（非战场=0）
                if (zone == Zone.Battlefield && board != null && board.TryGetCell(c, out int x, out int z))
                {
                    posX = (float)x / BoardLayout.Width;
                    posZ = (float)z / BoardLayout.Height;
                    adjOwn = SummonPlacementRules.AdjacentOwnCreatures(board, x, z, c.GetController()) / 6f;
                }
                else if (zone == Zone.ElementPool && pooledOf != null)
                {
                    pooledOf.TryGetValue(c, out var pc);
                    tappedOverride = pc?.IsTapped ?? false;
                    var color = pc?.LockedColor ?? ManaType.Gray;
                    var (lx, lz) = BoardLayout.LandCellOfElement(playerIdx, color);
                    posX = (float)lx / BoardLayout.Width;
                    posZ = (float)lz / BoardLayout.Height;
                }

                WriteCard(layer, c, baseSlot + i, tappedOverride, posX, posZ, ZoneKindOf(zone), adjOwn);
            }
        }

        // =====================================================

        /// <summary>己方牌库块 [DeckBase..]（v9 牌库双通道·己侧；v21 改「唯一卡 + 张数」分布编码）：
        /// 牌库完整卡表**无序集合**按内容哈希排序（SortDeckForEncoding 单源，位置无关——
        /// **不泄露洗牌顺序**）后分组（GroupDeckForEncoding 单源）：**每种唯一卡一槽**，写该组
        /// 代表卡全特征（身份/类型/参数三通路全吃；posX/posZ=-1、zoneKind=6），组内张数写
        /// 卡特征 [157] countInZone。30 槽=唯一卡**种类**上限——总张数不再影响可见性（绿 60 张
        /// 4 种=4 槽完整表达；v20 及以前每张物理卡一槽，30 槽截断后只剩 2 种）；剩余总张数照走 g[13]。
        /// 唯一卡 &gt;30 种：按排序键截断（只编码前 30 组），g[96] deckTruncated=1 标记不静默。
        /// 对方牌库不进 cards_——走 g[50..57] 开局标签通道（真实玩家口径=开局展示的卡组标签）；
        /// 己方开局标签 v21 起在 g[88..95]（WriteGlobals）。</summary>
        private void WriteOwnDeck(GameCore core, Player seat)
        {
            var cards = core.ZoneManager.GetCards(seat, Zone.Deck);
            if (cards == null || cards.Count == 0) return;
            var groups = GroupDeckForEncoding(core, seat, cards, null, out _);
            int n = Math.Min(DeckSlots, groups.Count);
            for (int i = 0; i < n; i++)
                WriteCard(core.LayerEngine, groups[i].Rep, DeckBase + i, false, -1f, -1f, 6, 0f, groups[i].Count);
            Globals[DeckTruncatedSlot] = groups.Count > DeckSlots ? 1f : 0f;
        }

        /// <summary>牌库唯一卡组（v21）：Rep=组内排序首张（该组特征行由它写出）、Count=组内张数、
        /// Row=组特征行（合并判据，见 GroupDeckForEncoding）。</summary>
        private sealed class DeckGroup
        {
            public Card Rep;
            public int Count;
            public float[] Row;
        }

        /// <summary>己方牌库分组（v21 单源——WriteOwnDeck 写入与 TryDeckSlot 反查同享，保证「写进去的
        /// 槽」与「反查出的槽」逐组一致）：先按 SortDeckForEncoding 排序，再把**内容键
        ///（Structure→Composition 哈希）相同且编码特征行逐位相等**的卡合并为一组。
        /// 判据为何不是「只看内容键」：费用增减/指示物/赠予关键词等是逐卡实例态、在卡特征内公开
        ///（v9 起既有口径——「诅咒/祝福标记差异在卡特征内公开呈现」），只按内容键合并会把组内
        /// 副本的实例态差异吞成首张一份。收紧到「模型看到的一样」=特征行全同才合并，countInZone
        /// 即「呈现这一行的物理卡张数」。排序保证同内容键相邻，故合并只在同键段内查找
        ///（特征行相等必同内容键——[27][28] 由内容哈希派生——不会跨段漏并）。
        /// track 非 null 时 out trackGroup=它落入的组序（不在列表=-1）。
        /// 代价=整库逐卡算一遍特征行（≤牌库张数次 WriteCardRow；仅牌库块写入与牌库卡反查走这里）。</summary>
        private static List<DeckGroup> GroupDeckForEncoding(GameCore core, Player me, List<Card> cards,
            Card track, out int trackGroup)
        {
            var layer = core.LayerEngine;
            var groups = new List<DeckGroup>();
            var probe = new float[NCard]; // 逐卡特征行探针（每次先清零：下标类特征只写存在的槽）
            int runStart = 0;
            ulong runStructure = 0UL, runComposition = 0UL;
            bool hasRun = false;
            trackGroup = -1;
            foreach (var c in SortDeckForEncoding(cards))
            {
                if (c == null) continue;
                var data = c is CardWrapper w ? w.GetData() : null;
                ulong structure = data != null ? data.StructureContentHash : 0UL;
                ulong composition = data != null ? data.CompositionContentHash : 0UL;
                if (!hasRun || structure != runStructure || composition != runComposition)
                {
                    hasRun = true; // 新内容键段（排序保证同键相邻）——可合并组只在本段内
                    runStart = groups.Count;
                    runStructure = structure;
                    runComposition = composition;
                }
                Array.Clear(probe, 0, NCard);
                WriteCardRow(probe, 0, me, layer, c, false, -1f, -1f, 6, 0f, 1);
                int hit = -1;
                for (int i = runStart; i < groups.Count; i++)
                    if (RowEquals(groups[i].Row, probe)) { hit = i; break; }
                if (hit < 0)
                {
                    hit = groups.Count;
                    groups.Add(new DeckGroup { Rep = c, Row = (float[])probe.Clone() });
                }
                groups[hit].Count++;
                if (ReferenceEquals(c, track)) trackGroup = hit;
            }
            return groups;
        }

        /// <summary>两条卡特征行逐位相等（观测特征均为有限值，无 NaN）。</summary>
        private static bool RowEquals(float[] a, float[] b)
        {
            for (int i = 0; i < NCard; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>牌库顶虚拟手牌槽 [DeckTopSlot]（v10）：TopDeckAsHandAura 光环命中座次方且
        /// 牌库非空时编码 deck[0] 全特征（zoneKind=7）。**未命中恒空槽——绝不无件编码牌库顶
        ///（防泄露下一抽，验收硬断言）**。口径同源 BattleScreen.TopDeckVirtualCard（光环命中
        /// 己方+牌库非空；不计手牌上限/不可弃/放地——LAE 只枚举出牌行）。</summary>
        private void WriteDeckTop(GameCore core, Player seat)
        {
            if (!RuleAuraSystem.ScopeHits(RuleAuraComponents.TopDeckAsHandAura, seat)) return;
            var deck = core.ZoneManager.GetCards(seat, Zone.Deck);
            if (deck == null || deck.Count == 0) return;
            WriteCard(core.LayerEngine, deck[0], DeckTopSlot, false, -1f, -1f, 7);
        }

        /// <summary>对方牌库被展示卡尾块 [OppDeckExposedBase..]（v10）：RevealRules.GetExposedCards
        /// 前 4 张全特征（zoneKind=8，区内序=玩家点卡组面板所见——面板即按此序列出，无额外位置泄露）。
        /// cap=4 截断护栏。己方牌库被展示卡不另设块：身份已在己方牌库多重集块内（无增量信息）。</summary>
        private void WriteOppDeckExposed(GameCore core, Player opp)
        {
            var exposed = RevealRules.GetExposedCards(core.ZoneManager, opp, Zone.Deck);
            int n = Math.Min(OppDeckExposedSlots, exposed.Count);
            for (int i = 0; i < n; i++)
                WriteCard(core.LayerEngine, exposed[i], OppDeckExposedBase + i, false, -1f, -1f, 8);
        }

        /// <summary>场地卡专用槽（v15 拆场地·每方 1 槽）：该侧战场区场地卡（与 BoardState.FieldCardOf
        /// 同口径——异常多张取首张）全特征编码（zoneKind=10，posX/posZ=己方场地格坐标）。
        /// 无场地=空槽恒 0（valid=0 即「该侧无场地」信号）；场地不占战场单位槽（WriteZone 已滤）。</summary>
        private void WriteFieldCard(GameCore core, Player p, int slot)
        {
            Card field = null;
            var cards = core.ZoneManager.GetCards(p, Zone.Battlefield);
            if (cards != null)
                foreach (var c in cards)
                    if (c != null && c.IsFieldCard()) { field = c; break; }
            if (field == null) return;
            int playerIdx = ReferenceEquals(p, core.Player1) ? 0 : 1;
            var (fx, fz) = BoardLayout.FieldCell(playerIdx);
            WriteCard(core.LayerEngine, field, slot, false,
                (float)fx / BoardLayout.Width, (float)fz / BoardLayout.Height, 10);
        }

        /// <summary>牌库编码序（单源）：StructureContentHash → CompositionContentHash → RuntimeId。
        /// 位置无关的确定性全序——编码槽序与区内序（洗牌序）解耦；WriteOwnDeck 写入与
        /// TryDeckSlot 反查同享此序。token/未装载卡哈希 0 排最前（确定即可，无语义）。
        /// v21：前两键=内容键，同键必相邻——分组合并只在同键段内进行；RuntimeId 只定段内次序
        ///（=组代表卡的选取），副本特征行全同时合并结果与段内次序无关。</summary>
        private static List<Card> SortDeckForEncoding(List<Card> cards)
        {
            var sorted = new List<Card>(cards);
            sorted.Sort(CompareDeckCards);
            return sorted;
        }

        private static int CompareDeckCards(Card a, Card b)
        {
            var da = a is CardWrapper wa ? wa.GetData() : null;
            var db = b is CardWrapper wb ? wb.GetData() : null;
            int cmp = (da != null ? da.StructureContentHash : 0UL)
                .CompareTo(db != null ? db.StructureContentHash : 0UL);
            if (cmp != 0) return cmp;
            cmp = (da != null ? da.CompositionContentHash : 0UL)
                .CompareTo(db != null ? db.CompositionContentHash : 0UL);
            if (cmp != 0) return cmp;
            return a.RuntimeId.CompareTo(b.RuntimeId);
        }

        /// <summary>区域 → zoneKind 下标值（[151]；显式映射不依 Zone 枚举序，防重排）。
        /// 己方牌库块=6 / 牌库顶=7 / 对方牌库被展示=8 由专用写入方法直传；
        /// 对方隐藏手牌=2 由 WriteConcealedCard 直传。</summary>
        private static int ZoneKindOf(Zone zone)
        {
            switch (zone)
            {
                case Zone.Battlefield: return 0;
                case Zone.ElementPool: return 1;
                case Zone.Hand: return 2;
                case Zone.Graveyard: return 3;
                case Zone.Activation: return 4;
                case Zone.FieldZone: return 5;
                case Zone.Exile: return 9; // v10
                default: return 0;
            }
        }

        /// <summary>牌库开局标签 8 维（v9 牌库双通道·对侧 g[50..57]；v21 起己方 g[88..95] 同函数同口径）：
        /// [0..3] 四色张数（灰/红/蓝/绿=ManaType 0..3 序；逐色多计 Cost[色]&gt;0——与
        /// CardSorter.HasCostColor 同口径，多色卡在构成色各计一次）、[4..6] 费用三档张数
        ///（档值=Clamp(Round(TotalCost),1,9)：0 费并入 1 档、&gt;9 并入 9+——与 DeckBuilder
        /// 费用曲线 BuildCostCurve 同口径；[4]=1-2 [5]=3-6 [6]=7-9）、[7] 卡组总数。
        /// 输入=GameCore.OriginalDeckOf（随卡组创建保留的入局卡表快照），故整局恒定——
        /// 不随抽牌/洗回变化（真实玩家口径=开局展示的标签，非剩余构成）。
        /// 原始计数不归一化（与 g[5..10] 手牌/战场计数同族）。null/空卡表返回全 0。</summary>
        public static float[] DeckTagsOf(IReadOnlyList<CardData> deck)
        {
            var tags = new float[8];
            if (deck == null) return tags;
            int total = 0;
            foreach (var d in deck)
            {
                if (d == null) continue;
                total++;
                var cost = d.Cost;
                if (cost != null)
                    for (int i = 0; i <= (int)ManaType.Green; i++)
                        if (cost[(ManaType)i] > 0f) tags[i]++;
                // 费用档=Clamp(Round(TotalCost),1,9)，Round 为 .5 取偶（Banker's）——
                // 与 DeckBuilder BuildCostCurve 的 Mathf.RoundToInt 逐值同款（正域手写，不引 UnityEngine）
                float v = d.TotalCost;
                int fl = (int)Math.Floor(v);
                float frac = v - fl;
                int rounded = frac > 0.5f ? fl + 1 : frac < 0.5f ? fl : (fl % 2 == 0 ? fl : fl + 1);
                int tier = Math.Max(1, Math.Min(9, rounded));
                if (tier <= 2) tags[4]++;
                else if (tier <= 6) tags[5]++;
                else tags[6]++;
            }
            tags[7] = total;
            return tags;
        }

        /// <summary>隐藏信息区（对方手牌）：只写存在性与归属，其余特征（含身份槽/位置）全 0 或 -1
        /// ——模型不开图（张数在 global_）。zoneKind=2（手牌，契约 §4 v9）。</summary>
        private void WriteConcealedCard(int slot)
        {
            int b = slot * NCard;
            Cards[b + 0] = 1f; // valid（这里有一张未知卡）
            Cards[b + 1] = 1f; // controller = 对方（该半区恒 opp）
            Cards[b + ZoneKindSlot] = 2f; // zoneKind=手牌
            Cards[b + PosXSlot] = -1f; // 未落格（手牌不在棋盘上）
            Cards[b + PosZSlot] = -1f;
            Cards[b + CountSlot] = 1f; // v21：一槽一卡（不变量 valid=1 ⇔ countInZone≥1）
        }

        /// <summary>写一张卡的全特征到观测槽（countInZone 缺省 1=一槽一卡；v21 己方牌库块由
        /// WriteOwnDeck 传组内张数）。实际写入在 WriteCardRow——静态、缓冲区无关，牌库分组
        ///（GroupDeckForEncoding）用同一函数复算特征行，写入与合并判据不会两套。</summary>
        private void WriteCard(LayerEngine layer, Card c, int slot, bool tappedOverride, float posX, float posZ,
            int zoneKind, float adjacencyOwn = 0f, int countInZone = 1)
            => WriteCardRow(Cards, slot * NCard, _me, layer, c, tappedOverride, posX, posZ, zoneKind, adjacencyOwn,
                countInZone);

        /// <summary>卡特征行写入（v21 自 WriteCard 抽出）：buf[b..b+NCard) = 卡 c 的全特征。
        /// **不清行**——调用方保证目标行已是零（新观测数组，或 GroupDeckForEncoding 的已清探针）；
        /// 下标类特征（原子哈希/类型/参数块）只写实际存在的槽。</summary>
        private static void WriteCardRow(float[] buf, int b, Player me, LayerEngine layer, Card c,
            bool tappedOverride, float posX, float posZ, int zoneKind, float adjacencyOwn, int countInZone)
        {
            int power = layer.CalculatePower(c);
            int life = layer.CalculateToughness(c);
            float cost = GameActions.GetCardCost(c).Total;

            buf[b + 0] = 1f; // valid
            buf[b + 1] = ReferenceEquals(c.GetController(), me) ? 0f : 1f; // controller
            buf[b + 2] = c is IHasSupertype s ? (int)s.Supertype : 0f;       // supertype
            buf[b + 3] = Clamp(power, -99f, 999f);                          // 结算力量
            buf[b + 4] = Clamp(life, -99f, 999f);                           // 结算防御/生命
            buf[b + 5] = Clamp(cost, 0f, 99f);                              // 声明费用和
            buf[b + 6] = (c.IsTapped() || tappedOverride) ? 1f : 0f;       // 横置（地牌读包装）
            // 可攻击粗判（v21：加攻资格闸——此前只看「未横置且力量>0」，守卫专用生物如火灵·守被误标
            // 为可攻击；口径同 CombatSystem.DeclareAttack 的 HasAttackAbility 资格闸）
            buf[b + 7] = (!c.IsTapped() && power > 0 && c.HasAttackAbility()) ? 1f : 0f;
            buf[b + 8] = KeywordRules.HasRushSickness(c) ? 1f : 0f;        // 召唤失调
            buf[b + 9] = c.GetCounterCount(CounterRules.ToxinCounter);   // 毒素层数
            buf[b + 10] = c.GetCounterCount(CounterRules.SilenceCounter) > 0 ? 1f : 0f;
            buf[b + 11] = c.GetCounterCount(CounterRules.CostUpCounter)
                          - (c.GetCounterCount(CounterRules.CostDownGrayCounter)   // 2026-10-31 P12 四色
                          + c.GetCounterCount(CounterRules.CostDownRedCounter)    // 费用降低指示物投放，逐色只减对应费
                          + c.GetCounterCount(CounterRules.CostDownBlueCounter)
                          + c.GetCounterCount(CounterRules.CostDownGreenCounter)); // 费用层净量（合计）
            buf[b + 12] = c.HasKeyword(KeywordRules.Taunt) ? 1f : 0f;
            buf[b + 13] = (c.GetCounterCount(CounterRules.StealthCounter) > 0 || c.HasKeyword(KeywordRules.Concealed)) ? 1f : 0f; // 潜行
            buf[b + 14] = c.HasKeyword(KeywordRules.LandTrait) ? 1f : 0f; // 地牌特性（v3：TapCreature 资格）
            // 内容身份（原子级拆散，CardIdentityService 与费用同管线推导），三通路：
            // 1) 精确哈希 [15..26] 原子 + [27] 结构 + [28] 组合——内容寻址查表，同单元跨卡共享行；
            // 2) 类型下标 [29..40]（参数无关，同 EffectType 共享行）；
            // 3) 参数块 [41..]（每原子 AtomParamDim 维浮点，线性投影读幅值）。
            var data = c is CardWrapper wrapper ? wrapper.GetData() : null;
            var atomHashes = data?.AtomContentHashes;
            for (int e = 0; e < AtomIdSlots && e < (atomHashes?.Length ?? 0); e++)
            {
                int idx = TideCardIndex.IndexOf(atomHashes[e]);
                // P-1 §0.4.4：写入值域断言（Release 零开销——Debug.Assert 按 DEBUG 条件编译剥除）
                System.Diagnostics.Debug.Assert(idx >= 0 && idx < TideCardIndex.Capacity, $"hash 下标越池 {idx}");
                buf[b + HashIdStart + e] = idx;
            }
            buf[b + StructureSlot] = TideCardIndex.IndexOf(data != null ? data.StructureContentHash : 0UL);
            buf[b + CompositionSlot] = TideCardIndex.IndexOf(data != null ? data.CompositionContentHash : 0UL);
            var typeIdx = data?.AtomTypeIndexes;
            for (int e = 0; e < AtomIdSlots && e < (typeIdx?.Length ?? 0); e++)
            {
                int t = typeIdx[e];
                System.Diagnostics.Debug.Assert(t >= 0 && t <= CardIdentityService.MaxTypeIndex, $"type 下标越池 {t}");
                buf[b + TypeIdStart + e] = t;
            }
            var atomParams = data?.AtomParams;
            int dim = CardIdentityService.AtomParamDim;
            int nParams = Math.Min(AtomIdSlots, (atomParams?.Length ?? 0) / dim);
            for (int e = 0; e < nParams; e++)
                for (int p = 0; p < dim; p++)
                    buf[b + ParamStart + e * dim + p] = atomParams[e * dim + p];
            buf[b + PosXSlot] = posX;
            buf[b + PosZSlot] = posZ;
            buf[b + ZoneKindSlot] = zoneKind; // v9 区域种类（契约 §4）
            // 死亡计数维：DeathTollCounter 原始层数（2026-11-24：累计无上限、去 /9 归一化——
            // 指示物 state 口径；进战场冻结后保持读数，配原子 deathToll 哈希段可见增幅资格）
            buf[b + DeathTollSlot] = c.GetCounterCount(CounterRules.DeathTollCounter);
            // v20 团结观测 [153]：邻接己方生物数/6（战场卡由 WriteZoneCards 计入；其余区恒 0）
            buf[b + AdjacencySlot] = adjacencyOwn;
            // v20 引擎奖励摘要 [154..156]（EngineRewardOf 只读——不进身份哈希）：
            // 引擎分支卡「达标后给什么」首次可见（团结/倒计时/充盈通用）。
            CardIdentityService.EngineRewardOf(data, out int rwCount, out int rwType0, out float rwValue0);
            buf[b + EngineRewardCountSlot] = rwCount;
            buf[b + EngineRewardType0Slot] = rwType0;
            buf[b + EngineRewardValue0Slot] = rwValue0;
            // v21 同区张数 [157]：己方牌库块=该唯一卡组内张数（WriteOwnDeck 传入），其余区恒 1
            //（WriteConcealedCard 直写）；原始计数不归一化、钳 99（与费用 [5] 同族）。
            buf[b + CountSlot] = Clamp(countInZone, 0f, 99f);
        }

        private void WriteGlobals(GameCore core, Player me, Player opp, bool responseContext,
            BoardState board = null)
        {
            var g = Globals;
            var zm = core.ZoneManager;
            var ep = core.ElementPool;

            g[0] = me.GetLife() / 30f;
            g[1] = opp.GetLife() / 30f;
            g[2] = core.TurnEngine.TurnNumber / 40f;
            g[3] = (int)(core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby); // 0/1/2
            g[4] = ReferenceEquals(me, core.TurnEngine.TurnPlayer) ? 1f : 0f; // is_my_turn（决策座次视角）
            // S3b（2026-11-04 删发动区）：手牌计数剔除施放中的卡（IsPendingCast 单口全域剔除）——
            // 卡留手牌但不可用（弃牌/上限/展示已剔），观测口径与可用手牌对齐。
            var se = core.StackEngine;
            g[5] = se != null ? zm.GetCards(me, Zone.Hand).FindAll(c => !se.IsPendingCast(c)).Count : zm.GetCards(me, Zone.Hand).Count;
            g[6] = se != null ? zm.GetCards(opp, Zone.Hand).FindAll(c => !se.IsPendingCast(c)).Count : zm.GetCards(opp, Zone.Hand).Count;
            // v15 拆场地：战场计数只数单位（剔场地——场地存在性由专用槽 valid+zoneKind=10 表达）
            g[7] = BattlefieldUnitCount(zm, me);
            g[8] = BattlefieldUnitCount(zm, opp);
            g[9] = ep.GetPooledCards(me).Count;
            g[10] = ep.GetPooledCards(opp).Count;
            g[11] = zm.GetCards(me, Zone.Graveyard).Count / 30f;
            g[12] = zm.GetCards(opp, Zone.Graveyard).Count / 30f;
            g[13] = zm.GetCards(me, Zone.Deck).Count / 60f;
            g[14] = zm.GetCards(opp, Zone.Deck).Count / 60f;
            g[15] = zm.GetCards(me, Zone.Exile).Count;  // v3 新增：放逐计数（v2 完全缺失）
            g[16] = zm.GetCards(opp, Zone.Exile).Count;
            // g[17..28] 临时钱包（现取现用：效果授予元素，回合末清空）——原六色块（维度 v3 冻结）：
            // 2026-10-28 P2 席位收缩删白/黑后实际四色（Gray/Red/Blue/Green=枚举 0..3），
            // g[21][22]/g[27][28] 死位恒 0（同 g[34..37] 口径）；
            // Any 彩元素（2026-10-23 起）**已进观测**：v19 剩余可用元素块 g[62]/g[68] 单独成维
            //（牺牲/蛰伏入账的临时彩——旧口径「不进观测」2026-11-20 作废，绿套补盲定案）
            for (int i = 0; i <= (int)ManaType.Green; i++) g[17 + i] = ep.GetPool(me).TempElements[(ManaType)i];
            for (int i = 0; i <= (int)ManaType.Green; i++) g[23 + i] = ep.GetPool(opp).TempElements[(ManaType)i];
            g[29] = core.StackEngine.IsEmpty ? 0f : 1f; // 栈占用（二值）
            g[30] = FieldValueReward.TotalValue(core, me) / 30f;  // 己方全资源价值（观测口径含手牌；归一化只为数值健康）
            g[31] = FieldValueReward.TotalValue(core, opp) / 30f;
            // 地牌上限（2026-10-15：自然封顶 10+成长无上限——归一化基准 14 保持 v3 训练分布不变，超出饱和到 1）
            g[32] = Math.Min(ep.GetLandCap(me) / 14f, 1f);
            g[33] = Math.Min(ep.GetLandCap(opp) / 14f, 1f);
            // g[34]/g[35]（2026-11-20，AI训练说明 §4.6）：角色护甲层数 /5——后手开局 +5 甲必须可见，
            // 否则模型只能从「莫名少掉的血」反推。视角与 g[0]/g[1] 生命一致（me/opp）。
            // g[36]/g[37] 仍为死位（原黑白台账，2026-10-28 四色重构后恒 0）——维度不变，旧权重可载。
            g[34] = me != null ? me.GetCounterCount(Attribute.KeywordRules.ArmorCounter) / 5f : 0f;
            g[35] = opp != null ? opp.GetCounterCount(Attribute.KeywordRules.ArmorCounter) / 5f : 0f;
            g[36] = 0f;
            g[37] = 0f;

            // 英雄技能块（绝对座次序：P1=38 / P2=42）
            WriteHeroSkillBlock(core, core.Player1, 38);
            WriteHeroSkillBlock(core, core.Player2, 42);

            var stack = core.StackEngine.GetStackContents(); // Reverse 序（栈顶在末位）
            g[46] = Math.Min(stack.Count, 16) / 16f;          // 栈深度（归一化 /16）
            var top = stack.Count > 0 ? stack[stack.Count - 1] : null;
            g[47] = top?.Source is Card topCard ? Math.Max(0, IndexOfCard(core, me, topCard)) / MaxCardsTotal : 0f;
            g[48] = top?.Definition != null
                ? TideCardIndex.IndexOfEffectDefinition(top.Definition) / 1024f
                : 0f; // 同动作特征 [6] 口径（cast/攻击/SBA 栈顶无效果定义 = 0）
            g[49] = responseContext ? 1f : 0f;                // 决策上下文（0=己方 Main / 1=响应窗口）
            // v9 对方牌库开局标签（g[50..57]，契约 §5）：GameCore.OriginalDeckOf 入局快照计算，
            // 整局恒定（真实玩家口径=开局展示的标签，不随抽牌变化）
            var deckTags = DeckTagsOf(core.OriginalDeckOf(opp));
            for (int i = 0; i < deckTags.Length; i++)
                g[DeckTagStart + i] = deckTags[i];
            // v21 己方牌库开局标签（g[88..95]，卡组分布与策略分界）：与对方块同函数同口径
            //（DeckTagsOf 单源、入局快照、整局恒定）——「我是哪套」显式可见（此前只有对方有，
            // 己方只能从牌库块反推）。g[96] deckTruncated 由 WriteOwnDeck 写（牌库块截断标记）。
            var ownDeckTags = DeckTagsOf(core.OriginalDeckOf(me));
            for (int i = 0; i < ownDeckTags.Length; i++)
                g[OwnDeckTagStart + i] = ownDeckTags[i];

            // v21 定稿（2026-11-24 回到一期目标 B0.3）手牌上限三槽：上限走 RuleHooks.GetHandLimit
            // 单源（线性计数，与 g[5]/g[6] 手牌计数同族）；g[99]=己方超上限剩余待弃——弃牌进行中
            //（停靠步：回合可能已切换到对方，以门快照归属为准）取 HandLimitDiscardGate 剩余；
            // 否则己方回合按 g[5] 同口径有效手牌（已剔施放中）算差；对手回合恒 0（超限走除外区闸）。
            g[HandLimitSelfSlot] = RuleHooks.GetHandLimit(me);
            g[HandLimitOppSlot] = RuleHooks.GetHandLimit(opp);
            var pendingSnap = HandLimitDiscardGate.Snapshot();
            int pendingOver = ReferenceEquals(pendingSnap.player, me) ? pendingSnap.over : 0;
            int overIfOwnTurn = ReferenceEquals(me, core.TurnEngine.TurnPlayer)
                ? Math.Max(0, (int)g[5] - (int)g[HandLimitSelfSlot]) : 0;
            g[PendingDiscardSlot] = Math.Max(pendingOver, overIfOwnTurn);

            // v19 剩余可用元素块（g[58..63]=me / g[64..69]=opp，绿套补盲）：
            // 逐色「钱包余额 + 未横置地牌张数」+ Any 单独成维 + 总量——「还差几点出大哥」
            //「对手能不能响应/斩杀」可算；牺牲/蛰伏的 +1 Any 收益首次可见。
            WriteRemainingElements(ep, me, 58);
            WriteRemainingElements(ep, opp, 64);

            // v20 己方格块（g[70..87]，计划 §3.4 召唤落位/团结观测）：UnitCells 下标序 9 格——
            // [70..78] 占用码（0=空/1=己方/2=敌方，/2）；[79..87] 各格 6 邻己方**生物**数/6
            //（团结口径单源 SummonPlacementRules.AdjacentOwnCreatures；空格邻接不可从卡特征导出，
            // 落格/召唤计划行的价值直读输入）。无棋盘（无头降级/服务器）=全 0 不炸。
            if (board != null && me != null)
            {
                var cells = BoardLayout.UnitCells(board.PlayerIndex(me));
                for (int i = 0; i < cells.Count && i < 9; i++)
                {
                    var occupant = board.CardAt(cells[i].x, cells[i].z);
                    g[70 + i] = (occupant == null ? 0f
                        : ReferenceEquals(occupant.GetController(), me) ? 1f : 2f) / 2f;
                    g[79 + i] = SummonPlacementRules.AdjacentOwnCreatures(board, cells[i].x, cells[i].z, me) / 6f;
                }
            }
        }

        /// <summary>v19 剩余可用元素 6 维块（g[58..63]=me / g[64..69]=opp，2026-11-20 绿套补盲）：
        /// [b..b+3] 四色剩余（=该色钱包余额 + 该色未横置地牌张数，/10）、[b+4] Any 彩元素（/10，
        /// 地牌不锁 Any——只在钱包）、[b+5] 剩余总量（/20，GetRemainingElementTotal 同源）。
        /// 与脚本侧判据（BlueControlStrategy/RedAggroStrategy 的 GetRemainingElementTotal 族）
        /// 同资源口径；观测不另除钳（钱包持有 2026-11-21 起引擎侧封顶地牌上限——CreditTempWallet
        /// 单源；特征保持线性计数，与手牌计数 g[5]/g[6] 同族）。</summary>
        private void WriteRemainingElements(ElementPoolSystem ep, Player p, int b)
        {
            var g = Globals;
            var pool = ep.GetPool(p);
            for (int i = 0; i <= (int)ManaType.Green; i++)
            {
                int wallet = pool.TempElements.ContainsKey((ManaType)i) ? pool.TempElements[(ManaType)i] : 0;
                int untapped = 0;
                if (pool.PooledCards != null)
                    foreach (var pc in pool.PooledCards)
                        if (pc != null && !pc.IsTapped && pc.LockedColor == (ManaType)i) untapped++;
                g[b + i] = (wallet + untapped) / 10f;
            }
            g[b + 4] = (pool.TempElements.ContainsKey(ManaType.Any) ? pool.TempElements[ManaType.Any] : 0) / 10f;
            g[b + 5] = ep.GetRemainingElementTotal(p) / 20f;
        }

        /// <summary>英雄技能 4 维块（契约 g[38..41]=P1 / g[42..45]=P2，绝对座次序）：
        /// [hasSkill, 本回合已用(tapped), 累计使用/10, 角色攻击弹药/5（v3 启用原预留维——
        /// HeroAttackCounter 层数即角色攻击资格与反伤力度）]。技能卡本体在场地区槽 [45] 逐卡编码。</summary>
        private void WriteHeroSkillBlock(GameCore core, Player p, int b)
        {
            var g = Globals;
            var card = p != null ? HeroSkillSystem.ResolveSkillCard(core, p) : null;
            bool has = card != null;
            g[b] = has ? 1f : 0f;
            g[b + 1] = has && card.IsTapped() ? 1f : 0f; // 本回合已用（回合开始重置）
            g[b + 2] = has ? HeroSkillSystem.GetTotalUses(core, p) / 10f : 0f;
            g[b + 3] = p != null ? p.GetCounterCount(Attribute.CounterRules.HeroAttackCounter) / 5f : 0f;
        }

        private static float Clamp(float v, float min, float max)
            => v < min ? min : (v > max ? max : v);

        /// <summary>战场单位数（v15：剔场地——与战场槽 [0..8] 编码口径一致）。</summary>
        private static int BattlefieldUnitCount(ZoneManager zm, Player p)
        {
            int n = 0;
            var cards = zm.GetCards(p, Zone.Battlefield);
            if (cards == null) return 0;
            foreach (var c in cards)
                if (c != null && !c.IsFieldCard()) n++;
            return n;
        }
    }

    /// <summary>
    /// 内容身份 → 稳定下标（P-1 2026-11-19 C 方案，docs/AI动作空间补全_修改计划.md §0.4）：
    /// index = 1 + SplitMix64(hash) % (N_CARD_POOL - 1)；hash 0 → 0。**纯函数派生**——
    /// 与池/生成顺序/manifest 无关，main/train 跨进程同号；未登记 hash 不再返回 0（直接落稳定桶），
    /// 0 只表示 hash=0（无该成分/token）。派生原语单源在 CardIdentityService（SplitMix64/
    /// Fnv1a64/StableIndex——Python tide_features 同式对拍）。
    /// 键仍是 ulong 内容哈希（CardIdentityService：原子哈希/组合结构哈希/关键词+tag+光环组合哈希，
    /// 2026-11-05 ID 冻结收口后直接锚定冻结 ID）。拆散到原子级：同原子（如 造成伤害4）跨卡共享
    /// 同一行。容量 16384 与 Python N_CARD_POOL 对齐（碰撞 ≈ 1.7 个/239 身份，见 §0.4.5——
    /// 碰撞=共享 embedding 行可接受，报告出 CollisionReport；不做按插入序探测）。
    /// 动作 effectIdentity：FNV1a64(def.Id)+盐 → StableIndex(1024)（与卡身份哈希族隔离）。
    /// 旧「追加式登记簿 + card_identity_manifest.json 持久化」已废弃（2026-11-19）——
    /// manifest 文件仅存档，不参与训练/导出；旧 1024/256 模型作废（当前 LayoutVersion 拒载，
    /// v16 起另按 cardPool/typePool 尺寸校验）。
    /// </summary>
    public static class TideCardIndex
    {
        /// <summary>卡身份池（P-1：1024→16384；Python N_CARD_POOL 同步。embedding 表随池扩 ~16×）。</summary>
        public const int Capacity = 16384;

        /// <summary>动作 effectIdentity 池（P-1 不变；特征 [6] 仍 / 1024）。</summary>
        public const int EffectIdentityPool = 1024;

        /// <summary>已登记身份数——**legacy 恒 0**：稳定派生无登记簿（旧日志口径作废，
        /// 调用方改打 pool sizes；见 NeuralAiDriver/TideHeadlessServer 2026-11-19 日志）。</summary>
        public static int Count => 0;

        /// <summary>绑定清单文件并载入既有分配——**legacy no-op**（P-1 已废弃 manifest 分配；
        /// 旧调用点保留签名编译通过，返回 0=无载入）。文件仅存档不参与训练/导出。</summary>
        public static int ConfigureManifest(string path) => 0;

        /// <summary>登记卡池——**legacy no-op**（P-1：下标纯派生无需登记；身份推导
        /// （CardIdentityService.EnsureIdentity）由装载管线 CardCostService.EnsureCost 承担）。</summary>
        public static int Register(IEnumerable<CardData> pool) => 0;

        /// <summary>内容哈希 → 稳定下标（0 = hash 0/无成分）。</summary>
        public static int IndexOf(ulong hash)
            => CardIdentityService.StableIndex(hash, Capacity);

        /// <summary>哈希 → 下标——legacy 别名（P-1 派生无「登记」语义，恒等于 IndexOf）。</summary>
        public static int IndexOfOrRegister(ulong hash)
            => IndexOf(hash);

        /// <summary>效果定义 → 身份下标（动作特征 [6] / globals g[48] 的 effectIdentity）：
        /// 键 = EffectDefinition.Id（转换自卡表的内容哈希稳定键）的 FNV-1a 64 + 效果空间盐
        /// ——与 CardIdentityService 的卡身份哈希族隔离（64 位空间 + 盐，碰撞概率可忽略；
        /// 即撞也只共用行不崩）。P-1：盐化哈希 → StableIndex(1024) 纯派生，同一效果定义
        /// 跨进程同号（embedding 不稀释）。空 Id（动态授予的效果）= 0。</summary>
        public static int IndexOfEffectDefinition(EffectDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return 0;
            ulong h = 0x9E3779B97F4A7C15UL; // 效果定义空间盐（区别于卡身份哈希族）
            foreach (var ch in def.Id)
            {
                h ^= ch;
                h *= 1099511628211UL;
            }
            return CardIdentityService.StableIndex(h, EffectIdentityPool);
        }
    }
}
