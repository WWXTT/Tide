using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>动作类型（离散下标 → 桥接映射回 GameActions.*）。
    /// v2（2026-09-30 动作空间二期契约）：新增 HeroSkill/VoluntaryTrigger/PassPriority/
    /// RespondPlay/RespondActivate/Guard——模型获得效果发动时机（自愿桶/英雄技能）
    /// 与对方回合响应窗口（守卫/响应出牌/响应发动/让过）的决策点；
    /// 指向性目标 = 同表逐目标展开（动作行=(卡,模式,目标)，与攻击动作同构，单步 argmax）。
    /// v3（2026-10-14 契约）：补漏 TapCreature 横置地牌特性生物产元素。
    /// 2026-11-06 墓碑清理：历史空洞（墓地出牌 2 / 立约 13）移除，枚举连续重排 0..12——
    /// 动作类型号全线漂移，旧模型权重作废（与 LAYOUT_VERSION 12 同批重训）。</summary>
    public enum TideActionType : int
    {
        PlayCard = 0,            // 手牌打出（含抉择模式 + 指向目标展开）
        PlayLand = 1,            // 手牌放入元素池（当地产元素）
        Activate = 2,            // 效果发动：战场卡的主动（Activate_*）能力（含目标展开）
        Attack = 3,              // 攻击宣言（attacker → 对方随从 / 对方玩家）
        EndTurn = 4,             // 结束回合
        HeroSkill = 5,           // 己方回合主阶段发动英雄技能（2026-10-07 卡牌化：技能=结界卡，Card 承载）
        VoluntaryTrigger = 6,    // 发动自愿桶待发效果（触发式可选项；响应窗口内同类型复用）
        PassPriority = 7,        // 响应窗口让过/放弃（仅响应窗口出现，Main 阶段不出现）
        RespondPlay = 8,         // 响应出牌（带目标展开；速度门/费用/优先权全检）
        RespondActivate = 9,     // 响应发动（主动；镜像 NetworkIntentApplier.ActivateWithTargets）
        Guard = 10,              // 守卫宣言（拦截栈上的攻击宣言对象）
        TapCreature = 11,        // 横置带地牌特性(LandTrait)的生物产 1 元素进临时钱包（主阶段；产色自动=费用首色）
        Charge = 12,             // 墓碑位（2026-11-15 充能整链删除，不复用编号；枚举器不再产生本动作）
        DiscardChoice = 13,      // 手牌上限弃牌（P3 2026-11-19 §4.1：回合末停靠决策，Targets=选中张；
                                 // 独立枚举口 EnumerateDiscardChoices，随 HandLimitDiscardGate 生命周期）
    }

    /// <summary>一个可执行动作：模型输出离散下标 → 桥接按此结构调用 GameActions.*。</summary>
    public struct TideAction
    {
        public TideActionType Type;
        public Card Card;                 // 来源卡（PlayCard/PlayLand/Activate/Guard 等；EndTurn/PassPriority 为 null）
        public Entity Target;             // 攻击目标（=Targets[0] 的兼容别名）；其余为 null
        public List<Entity> Targets;      // 展开的目标组合（null = 无目标/随机 → 引擎自动解析；任意数量/动态数量现在也展开成显式目标集）
        /// <summary>动态数量（2026-11-15 目标选择对齐）：下标=效果原子序，值=本次所选目标数（0=该原子非动态）。
        /// null=非动态。Apply 时随 targets 一起交给引擎（base×N 计价 + 按 N 消费预选）。
        /// 任意数量（-1）不需要它——目标集本身就是选择结果。</summary>
        public List<int> DynamicCounts;
        /// <summary>声明期交互参数（P1 2026-11-19 v0.3）：移动落格/宣言卡名——含移动/宣言原子的
        /// 行逐参数展开（null=普通行）。Apply 原样交给引擎（随 cast/发动入栈，结算期 handler 消费）；
        /// 宣言候选序号在 Interaction.DeclaredIndex（特征 [16]）。</summary>
        public InteractionParams Interaction;
        /// <summary>永久物落格（P2 2026-11-19：BoardLayout.UnitCells(己方) 的格下标 0..8，-1=无/非落格行）。
        /// 构造点（BuildTargetedRow）显式置 -1（struct 默认 0 会误读为格 0）；特征 [17]。
        /// Apply：声明成功后 PinCard（镜像 SimpleAI.TryPinPlacement 时点——入场 Resync 钉优先生效）。
        /// v20 扩义（计划 §3.3）：召唤计划行复用承载「计划首格」（含 Activate/RespondActivate 行）。</summary>
        public int PlacementCell;
        /// <summary>召唤落位计划（v20 2026-11-20 计划 §3.3，仅特征/签名用——Apply 不读，
        /// 落位实际走 Interaction.SummonCells）：0=默认计划 / 1..9=锚点计划（首格 UnitCells 下标+1）/
        /// -1=非召唤行。构造点显式置 -1。特征 [18]。</summary>
        public int SummonPlan;
        /// <summary>该计划落位后己方团结达标数 0..6（SummonPlacementRules.PredictUnityHits 单源；
        /// -1=非召唤/不可估）。动作特征 [19]——与 [5]actionCost 同层非观测泄露（2026-11-20 拍板保留）。</summary>
        public int SummonUnityHits;
        public EffectDefinition Effect;   // Activate/RespondActivate/VoluntaryTrigger 的效果定义
        public int ModeIndex;             // 抉择模式下标（非抉择恒 0；HeroSkill 行恒 0——技能卡在 Card 上）
        public PendingEffect Pending;     // VoluntaryTrigger/RespondActivate 的待发对象（引擎队列实例）
        public EffectInstance AttackInstance; // Guard 行：被拦截的攻击宣言栈对象
        /// <summary>守卫者（2026-11-15 审计 §2.2 角色守卫入动作空间）：生物=SourceCard；
        /// 角色守卫=Player（Card 为 null、SourceIndex=-1——TargetKindCode 已表达类别）。</summary>
        public Entity Guarder;

        /// <summary>来源卡在 cards_ 中的槽位（-1 = 无 / 未编码区如 FieldZone 技能卡）。</summary>
        public int SourceIndex;
        /// <summary>目标在 cards_ 中的槽位（-1 = 无 / 玩家目标——由 TargetKindCode 区分）。</summary>
        public int TargetIndex;
        /// <summary>目标类别特征（动作特征 [7]）：0=无/区域自结算，1=卡目标，2=对方玩家，3=己方玩家。</summary>
        public int TargetKindCode;
        /// <summary>次目标槽位（v8 多目标，动作特征 [8]/[9]）：Targets[1]/[Targets[2] 的 cards_ 槽位。
        /// 仅目标展开四类动作（PlayCard/RespondPlay/Activate/RespondActivate，经 BuildTargetedRow）有效——
        /// 其余类型构造点不设（struct 默认 0 会被 BuildFeatures 的类型闸强制 -1，防误读为槽 0）。
        /// -1 = 无次目标/玩家目标/超出编码上限（行内 Targets 仍完整，特征截断）。</summary>
        public int TargetIndex2;
        public int TargetIndex3;
        /// <summary>参照单位（步5 §3.5 参照 Enumerate spec 的选择结果镜像——参照=数值基准锚，
        /// 与受体 Targets 分角色编码；v1 单参照=行内首个 Reference 角色选位）。null=本动作无参照。</summary>
        public Entity Reference;
        /// <summary>参照槽位（动作特征 [11]，LayoutVersion 13）：参照卡的 cards_ 槽位；
        /// -1 = 无参照/玩家参照（与 TargetIndex 同口径）。</summary>
        public int ReferenceIndex;
        /// <summary>攻击者=自己角色（2026-10-10 角色参战遗留收口）：Card 恒 null、SourceIndex 恒 -1，
        /// Apply 分流 DeclareAttack(core, me, me, target)——弹药结算后引擎烧光。</summary>
        public bool HeroAttacker;
        /// <summary>响应窗口动作（签名区分上下文：同一动作在 Main 与响应窗口不互相摘除）。</summary>
        public bool ResponseContext;

        public override string ToString()
        {
            string t = Target is Card tc ? tc.ID : (Target is Player ? "(player)" : "-");
            return $"{Type}({Card?.ID ?? "-"} → {t}, mode={ModeIndex}, n={Targets?.Count ?? 0})";
        }

        /// <summary>
        /// 动作签名（Type|来源卡|效果Id|目标集|模式|上下文|尾段恒 -）：跨枚举稳定（EffectDefinition 每次枚举都是新对象，
        /// 引用判等不可用；Id 是转换自卡表的稳定键）。供「本回合摘除无效动作」判重。
        /// </summary>
        public string Signature
        {
            get
            {
                string t = TargetsKey();
                string ctx = ResponseContext ? "R" : "M";
                string d = DynamicCounts == null || DynamicCounts.Count == 0
                    ? "-" : string.Join(".", DynamicCounts);
                // P1/P2 修复（2026-11-19 反馈）：Interaction（宣言名/移动格）与 PlacementCell 入键——
                // 否则同目标集的不同宣言/落格行同键（拒一禁全、去重误并），强夺 3 候选行塌缩成 1。
                // v20（计划 §3.3）：SummonCells/SummonPlan 入键——否则不同落位计划同键被
                // FoldEquivalentRows 误并成 1 行（[17] 特征闸修复同批）。
                string i = Interaction == null
                    ? "-"
                    : $"{Interaction.DeclaredName ?? "-"}@{Interaction.MoveX},{Interaction.MoveZ}"
                      + (Interaction.HasSummonCells ? "@" + string.Join(".", Interaction.SummonCells) : "");
                return $"{(int)Type}|{Card?.ID ?? "-"}|{Effect?.Id ?? "-"}|{t}|{ModeIndex}|{ctx}|{d}|{i}|{PlacementCell}|{SummonPlan}";
            }
        }

        private string TargetsKey()
        {
            if (Targets == null || Targets.Count == 0)
                return Target is Card tc0 ? tc0.ID : (Target is Player ? "P" : "-");
            var sb = new System.Text.StringBuilder();
            foreach (var e in Targets)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(e is Card tc ? tc.ID : "P");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 合法动作枚举器：把 Tide 的动作来源 + 响应窗口收敛成扁平离散列表
    /// （每步枚举 → 固定下标 → 模型输出离散 int）。合法性谓词复用引擎权威判定，与 SimpleAI 同口径。
    ///
    /// 两个枚举入口（v2，2026-09-30 契约）：
    ///  - <see cref="Enumerate"/>：己方回合主阶段（回合玩家）——PlayLand/PlayCard/Activate/Attack/
    ///    HeroSkill/VoluntaryTrigger + EndTurn 恒末位；
    ///  - <see cref="EnumerateResponses"/>：响应窗口（优先权持有者，栈非空）——GameActions.CollectAvailableResponses
    ///    映射（守卫/自愿桶/主动）+ 手牌响应卡（RespondPlay，速度门全检）+ PassPriority 恒末位。
    ///
    /// 指向性目标逐目标展开（镜像 SimpleAI.ChooseTargets 的 ResolveCandidates 口径）：
    ///  - Single/SingleUnion：逐候选一行；Multiple/MultipleUnion（TargetCount&gt;0）：按引擎候选序组合枚举，
    ///    每效果上限 24 组截断；None/Whole/WholeUnion/Random/动态数量：单行（targets=null，引擎自动解析）。
    ///  - v8（2026-11-02 多目标定案对齐）：逐原子口径（PerAtomTargets）按 CollectAtomTargetSpecs 单源
    ///    规格展开——数量=每原子 effCount（>1 压过选一模式，如洗回跨双墓地选 N）；特征 8→11 维追加
    ///    target2/target3/nTargets（组合行次目标可见，Python N_ACTION_FEATURES 同步）。
    ///
    /// 文档化简化（v1 沿袭）：横置地牌由 driver 回合初自动完成（LandTapPolicy），不建模。
    /// </summary>
    public sealed class LegalActionEnumerator
    {
        /// <summary>多目标（Multiple/MultipleUnion）组合枚举上限（契约第 4 节：每效果 24 组截断）。</summary>
        private const int MaxTargetCombos = 24;

        /// <summary>任意数量/动态数量的子集行上限（2026-11-15 目标选择对齐）：每 spec 最多 64 行
        /// （1..N 按数量升序、同数量按候选序字典序）——覆盖小数量全组合 + 全取行。</summary>
        private const int MaxSubsetRows = 64;

        /// <summary>prefix-k 前缀行上限（P0 2026-11-19 无域原子目标）：无域牺牲/蛰伏候选 N 超上限时
        /// 只发 1..23 + 全取行（与 MaxSubsetRows 的「小数量 + 全取」截断取舍同口径）。</summary>
        private const int MaxPrefixRows = 24;

        /// <summary>移动行上限（P1 2026-11-19 v0.3 §3.1）：每效果 目标×合法空格 ≤18（9 格×2 目标内）。</summary>
        private const int MaxMoveRows = 18;

        /// <summary>宣言行上限（P1 2026-11-19 v0.3 §4.2）：每效果候选名 ≤16（超出按候选序截断）。</summary>
        private const int MaxDeclareRows = 16;

        /// <summary>落格行上限（P2 2026-11-19 §3.1）：每卡×模式×目标集×交互变体的落格维 ≤9
        ///（= 单位区格数；总行数继续走 MaxActions 硬上限）。</summary>
        private const int MaxPlaceRows = 9;

        /// <summary>单效果展开总行上限（多 spec 笛卡尔合并后）——防组合爆炸拖垮动作表/掩码。</summary>
        private const int MaxExpansionRows = 128;

        /// <summary>动作表硬上限（v3 契约 §6：与 Python MAX_ACTIONS / OnnxTidePolicy.MaxActions 三处同步）。
        /// 超限时保留前 MaxActions-1 行 + 恒保末位兜底行（Main=EndTurn / 响应=PassPriority）——
        /// Python pad_or_truncate_actions 是头截断，无此守卫会把兜底行切掉（模型无法结束回合，
        /// v2 空表冻结死循环的同族风险）。</summary>
        public const int MaxActions = 512;

        public List<TideAction> Actions = new List<TideAction>();
        /// <summary>动作特征张量（Actions.Count × TideObservation.NAction），供模型输入。
        /// 11 维布局（v8 契约）：[0]valid [1]type [2]sourceIndex [3]targetIndex
        /// [4]modeIndex [5]actionCost [6]effectIdentity [7]targetKind
        /// [8]target2Index [9]target3Index [10]nTargets（多目标组合行次目标可见）。</summary>
        public float[] Features = Array.Empty<float>();

        public int Count => Actions.Count;

        // 枚举上下文缓存（BuildFeatures 的槽位反查 / RemoveAll 后重建特征用）
        private GameCore _core;
        private Player _me;

        /// <summary>枚举当前回合玩家的全部合法动作（Main 阶段；含 EndTurn 兜底）。</summary>
        public void Enumerate(GameCore core, Player me)
        {
            Actions.Clear();
            Features = Array.Empty<float>();
            if (core == null || me == null || core.TurnEngine.TurnPlayer != me) return;
            if (core.IsGameOver) return;
            _core = core;
            _me = me;

            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            {
                // 非主阶段：driver 不应在此刻询问，兜底只给 EndTurn。
                Actions.Add(EndTurnAction());
                BuildFeatures();
                return;
            }

            var opp = me.Opponent;
            var zm = core.ZoneManager;

            EnumeratePlayLand(core, me, zm);
            EnumeratePlayCard(core, me, zm);
            EnumerateActivate(core, me, zm);
            EnumerateAttack(core, me, opp, zm);
            EnumerateHeroSkill(core, me);
            EnumerateVoluntaryTriggers(core, me);
            EnumerateTapCreatures(core, me, zm);
            Actions.Add(EndTurnAction());

            FoldEquivalentRows();                       // L3-B 折叠（2026-11-18 循环治理 §5.5）
            EnforceActionCap(TideActionType.EndTurn);
            BuildFeatures();
        }

        /// <summary>枚举响应窗口候选（优先权持有者，栈非空）：守卫/自愿桶/响应激活/响应出牌 + PassPriority 恒末位。
        /// 候选映射 GameActions.CollectAvailableResponses（引擎权威收集）+ 手牌响应卡（速度门镜像
        /// PlayCardInResponse/PushCardCast 预检）。候选=0 时调用方应自动 Pass（不停靠）。</summary>
        public void EnumerateResponses(GameCore core, Player me)
        {
            Actions.Clear();
            Features = Array.Empty<float>();
            if (core == null || me == null || core.IsGameOver) return;
            var engine = core.StackEngine;
            if (engine == null || engine.IsEmpty || engine.IsResolving) return;
            if (engine.CurrentPriorityHolder != me) return;
            _core = core;
            _me = me;

            foreach (var opt in GameActions.CollectAvailableResponses(core, me))
            {
                if (opt == null) continue;
                switch (opt.Kind)
                {
                    case ResponseOption.ResponseKind.GuardAbility:
                        // 角色守卫（2026-11-15 审计 §2.2）：SourcePlayer 候选此前被滤掉——AI 拿攻击弹药
                        // 也不会用角色拦截，与本地人类能力不对齐。
                        if (opt.AttackInstance != null && (opt.SourceCard != null || opt.SourcePlayer != null))
                            Actions.Add(BuildGuardAction(core, me, opt));
                        break;
                    case ResponseOption.ResponseKind.VoluntaryEffect:
                        if (opt.Pending?.Effect != null)
                            Actions.Add(BuildVoluntaryAction(core, me, opt.Pending));
                        break;
                    case ResponseOption.ResponseKind.ActivatedAbility:
                        if (opt.Definition != null && opt.SourceCard != null)
                            AddActivatedRows(core, me, opt.SourceCard, opt.Definition, TideActionType.RespondActivate);
                        break;
                    //（充能响应档已随 2026-11-05 充能系统动作化改版删除：充能=速度0系统动作，
                    //  只能己方主阶段空栈首动宣入，不经响应窗口。）
                }
            }

            EnumerateRespondPlay(core, me);
            Actions.Add(new TideAction
            {
                Type = TideActionType.PassPriority,
                ResponseContext = true,
                SourceIndex = -1,
                TargetIndex = -1,
            });

            EnforceActionCap(TideActionType.PassPriority);
            BuildFeatures();
        }

        // ===================================================== L3-B 动作表折叠（2026-11-18 循环治理 §5.5） =====

        // static 字段而非 const = 编辑器反射可翻转（A/B 验证/紧急回退免改源码重编译）；默认恒开/关。
        private static bool EquivalentFoldEnabled = true;  // 折叠开关（回退用）
        private static bool EquivalentFoldLog = false;     // [ActionFold] 日志开关（验收取证用，防训练刷屏）

        /// <summary>等价行动作折叠（方案 §5.5，仅 Enumerate 主表——响应表本就小）：
        /// 键 = 动作特征行去掉 [2]sourceIndex——对模型而言唯一不可区分差异是「来源卡在
        /// cards_ 的第几格」（同名同费同模式同目标的第 2..N 张手牌）。同键只留第一行：
        /// 深潭碑类局面 177 行 → 约 8 行，EndTurn 概率不再被稀释到 1/177，同时省算力。
        /// 费用/效果身份/目标槽/参照/区域特征全在键内——费用指示物不同的同名牌不会被误折。
        /// 位于 EnforceActionCap 之前（折叠后通常远离 512 上限）。</summary>
        private void FoldEquivalentRows()
        {
            if (!EquivalentFoldEnabled || Actions.Count <= 1) return;
            BuildFeatures(); // 先按全表建特征——键语义=真实特征行（避免另写一套键计算与特征漂移）
            var seen = new HashSet<string>(Actions.Count);
            int w = 0;
            for (int k = 0; k < Actions.Count; k++)
            {
                if (!seen.Add(FeatureKeyExceptSource(k))) continue; // 等价重复行：丢弃（保第一行）
                if (w != k) Actions[w] = Actions[k];
                w++;
            }
            if (w == Actions.Count) return;
            int raw = Actions.Count;
            Actions.RemoveRange(w, Actions.Count - w);
            BuildFeatures(); // 特征张量必须按折叠后新表同步重建（错位=直接毁训练）
            if (EquivalentFoldLog)
                Console.WriteLine($"[ActionFold] raw={raw} folded={w}");
        }

        /// <summary>动作 k 的特征行去掉 sourceIndex（[2]）后的等价键（float 用 R 往返格式）。</summary>
        private string FeatureKeyExceptSource(int k)
        {
            int b = k * TideObservation.NAction;
            var parts = new string[TideObservation.NAction - 1];
            for (int j = 0, i = 0; j < TideObservation.NAction; j++)
            {
                if (j == 2) continue;
                parts[i++] = Features[b + j].ToString("R");
            }
            return string.Join(",", parts);
        }

        /// <summary>动作表硬上限守卫（v3 契约 §6）：超过 MaxActions 时保留前 MaxActions-1 行，
        /// 末位兜底行（EndTurn/PassPriority）恒不被截断——Python 侧 pad_or_truncate_actions
        /// 头截断永不会切掉「回合/窗口必然流动」的那一行。截断只丢可选性（与契约 §4 口径一致）。</summary>
        private void EnforceActionCap(TideActionType fallbackType)
        {
            if (Actions.Count <= MaxActions) return;
            int keep = MaxActions - 1;
            // 末位兜底行必然存在（Enumerate/EnumerateResponses 恒以 EndTurn/PassPriority 收尾）
            var fallback = Actions[Actions.Count - 1];
            if (fallback.Type != fallbackType)
            {
                // 防御：末位不是预期兜底（不应发生）——补一行，保证流动
                fallback = fallbackType == TideActionType.PassPriority
                    ? new TideAction { Type = TideActionType.PassPriority, ResponseContext = true, SourceIndex = -1, TargetIndex = -1 }
                    : EndTurnAction();
            }
            Actions.RemoveRange(keep, Actions.Count - keep);
            Actions.Add(fallback);
        }

        /// <summary>把动作映射回引擎调用。返回 false 表示引擎拒绝（枚举口径漂移时的保守兜底，driver 据此重观测）。</summary>
        public static bool Apply(GameCore core, Player me, TideAction a)
        {
            switch (a.Type)
            {
                case TideActionType.PlayCard:
                    // v10 牌库顶变体行：卡在牌库（TopDeckAsHandAura 授权）→ fromZone=Deck
                    //（引擎 PlayCard 内 TopDeckPlaySource 终审，未授权安全拒）；否则照旧手牌源
                    // 2026-11-15 目标选择对齐：预选目标 + 动态数量原样交给引擎（结算期不再反问）
                {
                    // P1 v0.3：交互参数随声明入栈
                    var played = GameActions.PlayCard(core, me, a.Card, a.Targets,
                        core.ZoneManager.IsCardInZone(a.Card, me, Zone.Deck) ? Zone.Deck : Zone.Hand,
                        a.ModeIndex, null, a.DynamicCounts, a.Interaction, out _);
                    if (played) TryPinPlacementCell(core, me, a); // P2：声明成功即 Pin（入场 Resync 钉优先）
                    return played;
                }
                case TideActionType.PlayLand:
                    // ModeIndex 复用为锁色选择（2026-10-11：-1=缺省主色，否则=ManaType 枚举值）
                    return GameActions.AddToElementPool(core, me, a.Card,
                        a.ModeIndex >= 0 ? (ManaType)a.ModeIndex : (ManaType?)null);
                case TideActionType.Activate:
                case TideActionType.RespondActivate:
                    return ActivateWithTargets(core, me, a.Effect, a.Card, a.Targets, a.DynamicCounts,
                        a.Type == TideActionType.RespondActivate, a.Interaction);
                case TideActionType.Attack:
                    return a.HeroAttacker
                        ? GameActions.DeclareAttack(core, me, me, a.Target) // 角色=攻击者（弹药即攻击力）
                        : GameActions.DeclareAttack(core, me, a.Card, a.Target);
                case TideActionType.EndTurn:
                    return GameActions.EndTurn(core, me);
                case TideActionType.HeroSkill:
                    // 2026-11-15 目标选择对齐：英雄技能走带目标/动态数量的官方口（预选随
                    // PendingEffect 入栈，结算期按预选队列消费，不再依赖 AiOrder 代选）。
                    // ModeIndex=效果抉择序号（多效果技能）；targets/counts 为 null 时行为同旧口。
                    return GameActions.ActivateHeroSkill(core, me, a.ModeIndex, a.Targets, a.DynamicCounts,
                        a.Interaction, out _); // P1 v0.3：交互参数随发动入栈
                case TideActionType.VoluntaryTrigger:
                    return a.Pending != null && core.StackEngine.PlayerActivateVoluntary(a.Pending);
                case TideActionType.PassPriority:
                    return GameActions.PassPriority(core, me);
                case TideActionType.RespondPlay:
                {
                    var played = GameActions.PlayCardInResponse(core, me, a.Card, a.Targets, a.ModeIndex,
                        null, a.DynamicCounts, a.Interaction, out _); // P1 v0.3：交互参数随声明入栈
                    if (played) TryPinPlacementCell(core, me, a); // P2：声明成功即 Pin
                    return played;
                }
                case TideActionType.Guard:
                    // 角色守卫（2026-11-15）：Guarder 承载生物/角色（Apply 收口单源）
                    return a.Guarder != null && a.AttackInstance != null
                        && core.StackEngine.PushGuardDeclaration(a.Guarder, a.AttackInstance, me);
                case TideActionType.TapCreature:
                    return GameActions.TapCreatureForElement(core, me, a.Card);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 响应窗口停靠预检：手牌是否存在任一可响应卡（速度门 + 基础可玩性 + 任一模式可付）。
        /// 与 EnumerateResponses 的 RespondPlay 预检同口径（早退版）——driver 据此判定
        /// 「候选 &gt; 0 停靠」，避免停靠后枚举出空表（契约第 4 节：停靠仅在候选&gt;0 时发生）。
        /// </summary>
        public static bool HasRespondableHandCard(GameCore core, Player p)
        {
            var engine = core?.StackEngine;
            var zm = core?.ZoneManager;
            if (engine == null || zm == null || p == null || engine.IsEmpty || engine.IsResolving) return false;
            if (engine.CurrentPriorityHolder != p) return false;

            var hand = zm.GetCards(p, Zone.Hand) ?? new List<Card>();
            if (core.StackEngine != null) hand = hand.Where(c => !core.StackEngine.IsPendingCast(c)).ToList(); // S3b
            foreach (var c in hand)
            {
                if (c == null || !RuleHooks.CanPlay(core, p, c, Zone.Hand)) continue;
                if (GameActions.GetPayloadGateReject(core, p, c) != null) continue; // 代价门槛镜像（2026-10-04：全价过地牌上限/代价无目标）
                if (!engine.SpeedCounter.CanActivate(
                        SpeedCalculator.GetCardCastSpeed(c), EffectActivationType.Voluntary, engine.IsEmpty)) continue;
                if (!IsSpell(c) && !c.IsFieldCard() && !zm.HasBattlefieldSpace(p)) continue; // 场地卡落场地格不计容量（P8）
                int modes = ModeCount(c);
                for (int m = 0; m < modes; m++)
                    if (GameActions.CanAffordCast(core, p, c, m)) return true;
            }
            return false;
        }

        /// <summary>
        /// 按签名摘除动作（本回合内引擎已拒绝/无进展的动作不再出现在选项表里——「只给可以操作的选项」），
        /// 特征张量同步重建。EndTurn/PassPriority 永远不会被摘除（driver 只对兜底动作之外的记签名）。
        /// </summary>
        public void RemoveAll(HashSet<string> banned)
        {
            if (banned == null || banned.Count == 0 || Actions.Count == 0) return;
            Actions.RemoveAll(a => a.Type != TideActionType.EndTurn && a.Type != TideActionType.PassPriority
                                    && banned.Contains(a.Signature));
            BuildFeatures();
        }

        /// <summary>
        /// 空表兜底（2026-09-30 v2 修复，当晚复发修正）：动作表为空时补一行保证回合/窗口必然流动——
        /// 响应停靠 = PassPriority，Main 停靠 = EndTurn。空表曾致自对弈整局冻结：
        /// 零合法行 → 模型全掩码下均匀采样 → 下标越界 → driver 无操作兜底 → 同状态死循环
        /// （实测熵恒 ln512、每步随引擎态变慢、最终单步超时杀死训练）。
        /// 空表成因 = 停靠判定（TryResolveResponseWindow 候选&gt;0，不查 IsResolving）与
        /// EnumerateResponses 早退守卫（栈空/解析中/优先权人≠停靠者）读到不一致的引擎瞬态。
        /// **特征必须就地手写、不能走 BuildFeatures**：早退路径下 _core/_me 为 null，BuildFeatures
        /// 的空上下文守卫会写全 0 特征（valid=0），兜底行对模型仍是掩码位——首版兜底因此无效复发。
        /// </summary>
        public void AppendFallback(bool responseStop)
        {
            if (Actions.Count > 0) return;
            var type = responseStop ? TideActionType.PassPriority : TideActionType.EndTurn;
            Actions.Add(responseStop
                ? new TideAction { Type = TideActionType.PassPriority, ResponseContext = true, SourceIndex = -1, TargetIndex = -1 }
                : new TideAction { Type = TideActionType.EndTurn });
            Features = new float[TideObservation.NAction];
            Features[0] = 1f;          // valid —— 兜底行必须可选，否则全掩码均匀采样照旧
            Features[1] = (int)type;
        }

        // ===================================================== Apply 辅助 =====================================================

        /// <summary>
        /// 带目标的主动激活（镜像 NetworkIntentApplier.ActivateWithTargets）：targets 空 → 公共入口
        /// ActivateEffect（响应窗口走 ActivateEffectInResponse——主阶段闸不适用于连锁）；
        /// 有预选目标 → PendingEffect.Create 携带 SelectedTargets 走 PlayerActivateVoluntary
        /// （速度门/记速器在栈机器内；横置消耗同口径）。
        /// 2026-11-15 目标选择对齐：dynamicCounts 先落效果原子（base×N 计价；null=无动态）。
        /// </summary>
        /// <summary>P2（2026-11-19 §3.1）：永久物落格——声明成功后 PinCard（镜像
        /// SimpleAI.TryPinPlacement 时点：声明成功即 Pin，入场 Resync 钉优先生效）。
        /// 无棋盘（无头/服务器未装配 MoveUnitRules）/非法下标/格已占（竞态）=自然跳过，
        /// 引擎 first-free 兜底——SimpleAI 行为不变（其自带 TryPinPlacement 不经此口）。</summary>
        private static void TryPinPlacementCell(GameCore core, Player me, TideAction a)
        {
            if (a.PlacementCell < 0 || a.Card == null || a.Card.IsSpellCard() || a.Card.IsFieldCard()) return;
            var board = GameBoard.MoveUnitRules.Current;
            if (board == null) return;
            var cells = GameBoard.BoardLayout.UnitCells(board.PlayerIndex(me));
            if (a.PlacementCell >= cells.Count) return;
            var cell = cells[a.PlacementCell];
            if (board.CardAt(cell.x, cell.z) != null) return; // 竞态占用：不 Pin，引擎兜底
            board.PinCard(a.Card, cell.x, cell.z);
        }

        private static bool ActivateWithTargets(GameCore core, Player player, EffectDefinition effect,
            Card source, List<Entity> targets, IReadOnlyList<int> dynamicCounts, bool responseContext,
            InteractionParams interaction = null)
        {
            if (effect == null) return false;
            // 动态数量：预检/扣费/执行同表（幂等；null 无操作）
            CostDerivationService.ApplyDynamicCounts(effect, dynamicCounts);
            if (targets == null || targets.Count == 0)
            {
                // P1 v0.3：无目标行（AutoTake 免弹档/无域效果）也要带交互参数——
                // 移动格随声明入栈，结算期 handler 消费（此前早退丢参 → 移动空转）；
                // 修复（2026-11-19 反馈）：响应位改走 ActivateEffectInResponse 正口带参
                ///（此前借道 ActivateEffect 会错过响应门/优先权判定）
                if (responseContext)
                    return GameActions.ActivateEffectInResponse(core, player, effect, source, interaction, out _);
                return GameActions.ActivateEffect(core, player, effect, source, null, null, 0, interaction, out _);
            }

            var pending = PendingEffect.Create(
                effect, source, player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby);
            pending.SelectedTargets = targets;
            if (interaction != null) pending.Interaction = interaction; // P1 v0.3：交互参数随待发入栈
            var activated = core.StackEngine.PlayerActivateVoluntary(pending);
            if (activated && effect.IsActivatedEffect && source != null && Attribute.KeywordRules.ShouldTap(source))
                source.Tap();
            return activated;
        }

        // ===================================================== Main 枚举 =====================================================

        private static TideAction EndTurnAction() => new TideAction { Type = TideActionType.EndTurn };

        private void EnumeratePlayLand(GameCore core, Player me, ZoneManager zm)
        {
            // 各自地牌槽上限（2026-10-11：个人曲线，共享计数器废止——与 AddCardToPool 权威校验同口径）
            if (!core.ElementPool.CanAddLand(me)) return;
            var hand = zm.GetCards(me, Zone.Hand);
            if (core.StackEngine != null) hand = hand.Where(c => !core.StackEngine.IsPendingCast(c)).ToList(); // S3b
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                // 可作地牌（唯一判定口，2026-11-17 收口：三型/非衍生物/非临时复制/非抉择/非0费/有可锁色）
                if (!ElementPoolSystem.CanPlaceAsLand(c)) continue;
                // 锁色维度展开（2026-10-11）：每候选色一行（单色卡=1 行不变；ModeIndex=锁色枚举值）
                var colors = ElementPoolSystem.GetLandColorChoices(c);
                foreach (var color in colors)
                {
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.PlayLand,
                        Card = c,
                        SourceIndex = TideObservation.CardIndex(me, me, Zone.Hand, i),
                        TargetIndex = -1,
                        ModeIndex = (int)color,
                    });
                }
            }
        }

        private void EnumeratePlayCard(GameCore core, Player me, ZoneManager zm)
        {
            var hand = zm.GetCards(me, Zone.Hand);
            if (core.StackEngine != null) hand = hand.Where(c => !core.StackEngine.IsPendingCast(c)).ToList(); // S3b
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (!RuleHooks.CanPlay(core, me, c, Zone.Hand)) continue;
                if (GameActions.GetPayloadGateReject(core, me, c) != null) continue; // 代价门槛镜像（①可支付②无目标③按色地牌——2026-10-31 代价地牌统一定案）

                int modes = ModeCount(c);
                var castDefs = GameActions.GetCastableEffectDefinitions(c); // 模式序=Effects 声明序（modeIndex 同源）
                for (int m = 0; m < modes; m++)
                {
                    if (!IsSpell(c) && !c.IsFieldCard() && !zm.HasBattlefieldSpace(me)) continue; // 场地卡落场地格不计容量（P8）
                    if (!TargetDomainService.HasPlayableTargets(core, me, c, m)) continue; // 域空不可出（镜像 PlayCard 预检）
                    // 2026-11-15 审计 §1：所选模式含 AI 无法执行的原子（选格/无预置卡名）→ 不枚举
                    if (m < castDefs.Count && UnsupportedForAi(castDefs[m])) continue;

                    int src = TideObservation.CardIndex(me, me, Zone.Hand, i);
                    // 普通行：全净费可付（动态数量行按 base×N 预过滤，见 ExpandCardTargets）；
                    // P1（2026-11-19 v0.3）：含移动/宣言原子的效果经 AddRowsWithInteraction 逐参数展开
                    if (GameActions.CanAffordCast(core, me, c, m))
                        foreach (var set in ExpandCardTargets(core, me, c, m))
                            AddRowsWithInteraction(core, me, m < castDefs.Count ? castDefs[m] : null,
                                TideActionType.PlayCard, c, m, src, set.Targets,
                                reference: ReferencePickOf(core, me, c, m, set.Targets),
                                dynamicCounts: set.Counts);
                }
            }

            // v10（2026-11-02）牌库顶虚拟手牌：TopDeckAsHandAura 光环命中己方且牌库非空 → deck[0]
            // 与手牌出牌同构枚举（镜像引擎 PlayCard fromZone=Deck 路径；授权口=TopDeckPlaySource，
            // 引擎侧终审）。不放地/不充能（虚拟手牌规则：不计手牌上限/不可弃/放地）；响应窗口不枚举
            //（RespondPlay 恒手牌源，与引擎 PlayCardInResponse 口径一致）。
            // sourceIndex=观测牌库顶槽 [184]（zoneKind=7 同卡同槽，模型可分）。
            if (!RuleAuraSystem.ScopeHits(RuleAuraComponents.TopDeckAsHandAura, me)) return;
            var deck = zm.GetCards(me, Zone.Deck);
            if (deck == null || deck.Count == 0) return;
            var top = deck[0];
            if (!RuleHooks.CanPlay(core, me, top, Zone.Deck)) return;
            if (GameActions.GetPayloadGateReject(core, me, top) != null) return;
            int topModes = ModeCount(top);
            var topDefs = GameActions.GetCastableEffectDefinitions(top);
            for (int m = 0; m < topModes; m++)
            {
                if (!IsSpell(top) && !top.IsFieldCard() && !zm.HasBattlefieldSpace(me)) continue;
                if (!TargetDomainService.HasPlayableTargets(core, me, top, m)) continue;
                if (!GameActions.CanAffordCast(core, me, top, m)) continue;
                if (m < topDefs.Count && UnsupportedForAi(topDefs[m])) continue; // 2026-11-15 审计 §1 同手牌口径
                foreach (var set in ExpandCardTargets(core, me, top, m))
                    AddRowsWithInteraction(core, me, m < topDefs.Count ? topDefs[m] : null,
                        TideActionType.PlayCard, top, m, TideObservation.DeckTopSlot, set.Targets,
                        reference: ReferencePickOf(core, me, top, m, set.Targets),
                        dynamicCounts: set.Counts);
            }
        }

        private void EnumerateActivate(GameCore core, Player me, ZoneManager zm)
        {
            var executor = core.StackEngine.GetExecutor();
            var phase = core.TurnEngine.CurrentPhase.Phase;
            int turn = core.TurnEngine.TurnNumber;

            var bf = BattlefieldUnits(zm, me); // v15 拆场地：i 与观测战场单位槽序对齐（场地不占单位槽）
            for (int i = 0; i < bf.Count; i++)
            {
                var src = bf[i];
                // 结界禁主动效果（2026-10-15 改版）——镜像引擎 ActivateEffect 来源闸，直接跳过
                if (Attribute.CounterRules.UsesDurabilityEconomy(src)) continue;
                // §11 预检：过载卡主动效果失效——不入动作空间（不记账；运行时闸在 GameActions）
                if (Attribute.SuppressRules.IsOverloaded(src)) continue;
                foreach (var def in GetEffectDefinitions(src))
                {
                    if (!def.IsActivatedEffect) continue;
                    // 移动原子（2026-10-31 场面轴3c）+ 卡名宣言（2026-11-15 审计 §1.2）：不枚举——
                    // 枚举器的动作行无选格/卡名参数，AI 发动=白付代价空转
                    if (UnsupportedForAi(def)) continue;
                    //（充能跳过已随 2026-11-05 充能系统动作化改版删除——充能不再是卡上效果。）
                    if (!executor.CanActivate(def, src, me, me, phase, turn)) continue;

                    int slot = TideObservation.CardIndex(me, me, Zone.Battlefield, i);
                    AddActivatedRows(core, me, src, def, TideActionType.Activate, slot);
                }
            }
        }

        /// <summary>与 SimpleAI.UnsupportedForAi 同口径（2026-11-15 审计 §1 立，P1/P3 2026-11-19 清空）：
        /// MoveUnit/SearchDeck/DeclareHand/DeclareDeck/ProphecyNextCard 已全部接线
        /// InteractionParams（逐参数展开，见 InteractionVariants），本闸现无条目——保留给未来
        /// 新增交互原子（接线前照旧整效果裁剪防白付）。</summary>
        private static bool UnsupportedForAi(EffectDefinition def)
        {
            // P1（2026-11-19 v0.3）：移动选格与宣言卡名已接线（InteractionParams 透传 + 本枚举器
            // 逐参数展开，见 InteractionVariants），原「MoveUnit / 无预置 SearchDeck·DeclareHand·
            ///DeclareDeck」两条裁剪删除——本闸当前无条目，保留口子给未来新增交互原子（接线前照旧裁剪防白付）。
            return false;
        }

        /// <summary>主动动作行（带逐目标展开）：Main=Activate / 响应窗口=RespondActivate 共用。
        /// 展开口径：def 自身 SelectionMode/组合域（主动无抉择模式，modeIndex 恒 0）。
        /// 2026-11-15：任意数量（-1）与动态数量（-3）也展开成显式目标集行（不再交给引擎代选）。</summary>
        private void AddActivatedRows(GameCore core, Player me, Card src, EffectDefinition def,
            TideActionType type, int sourceSlot = -2)
        {
            if (UnsupportedForAi(def)) return; // 2026-11-15 审计 §1：交互原子不支持（主动/响应共用闸）
            if (sourceSlot == -2) sourceSlot = TideObservation.IndexOfCard(core, me, src);
            var expansions = new List<TargetSet>();
            ExpandDefinitionTargets(core, me, src, def, 0, expansions);
            if (expansions.Count == 0) expansions.Add(default);
            bool response = type == TideActionType.RespondActivate;
            foreach (var set in expansions)
                if (set.Counts == null || DynamicCountAffordable(core, me, def, set.Counts))
                    AddRowsWithInteraction(core, me, def, type, src, 0, sourceSlot, set.Targets,
                        response, dynamicCounts: set.Counts, effect: def); // P1 v0.3：移动/宣言逐参数展开
        }

        private void EnumerateAttack(GameCore core, Player me, Player opp, ZoneManager zm)
        {
            // 2026-09-16 战斗接入栈机器：攻击=速度0栈对象逐攻击开窗——动作语义不变
            //（Apply→GameActions.DeclareAttack 上栈，随后响应窗口/DrainStack 结算）。
            // v15 拆场地：攻方/对方单位列表剔场地（i/j 与观测战场单位槽序对齐）；
            // 场地可被攻击（2026-11-18 场地特殊化拍板）——目标槽=场地专用槽 [189]/[190]。
            var bf = BattlefieldUnits(zm, me);
            var oppBf = BattlefieldUnits(zm, opp);
            var oppField = FieldCardOf(zm, opp);
            for (int i = 0; i < bf.Count; i++)
            {
                var atk = bf[i];
                if (Attribute.SuppressRules.IsOverloaded(atk)) continue; // §11 预检：过载卡拒绝宣言——不入动作空间（不记账）
                if (!core.CombatSystem.CanDeclareAttack(atk, me)) continue;

                for (int j = 0; j < oppBf.Count; j++)
                {
                    var t = oppBf[j];
                    if (!core.CombatSystem.CanAttackTarget(atk, t, me)) continue;
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Attack,
                        Card = atk,
                        Target = t,
                        SourceIndex = TideObservation.CardIndex(me, me, Zone.Battlefield, i),
                        TargetIndex = TideObservation.CardIndex(me, opp, Zone.Battlefield, j),
                        TargetKindCode = 1,
                    });
                }

                // 攻击对方场地（每次结算固定 −1 耐久——引擎侧 FieldRules 口径）：
                // TargetIndex=场地专用槽（FieldSlotOf），TargetKindCode=1（卡目标——角色攻击行同）。
                if (oppField != null && core.CombatSystem.CanAttackTarget(atk, oppField, me))
                {
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Attack,
                        Card = atk,
                        Target = oppField,
                        SourceIndex = TideObservation.CardIndex(me, me, Zone.Battlefield, i),
                        TargetIndex = TideObservation.FieldSlotOf(me, opp),
                        TargetKindCode = 1,
                    });
                }

                if (core.CombatSystem.CanAttackTarget(atk, opp, me))
                {
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Attack,
                        Card = atk,
                        Target = opp, // 对方玩家（无卡槽 → TargetIndex = -1）
                        SourceIndex = TideObservation.CardIndex(me, me, Zone.Battlefield, i),
                        TargetIndex = -1,
                        TargetKindCode = 2,
                    });
                }
            }

            // 角色攻击行（2026-10-10 角色参战遗留收口）：弹药=HeroAttackCounter 层数（CanDeclareAttack
            // 的 GetPower 分支同门），无卡槽 → Card=null/SourceIndex=-1；Apply 按 HeroAttacker 分流。
            if (core.CombatSystem.CanDeclareAttack(me, me))
            {
                for (int j = 0; j < oppBf.Count; j++)
                {
                    var t = oppBf[j];
                    if (!core.CombatSystem.CanAttackTarget(me, t, me)) continue;
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Attack,
                        Card = null,
                        HeroAttacker = true,
                        Target = t,
                        SourceIndex = -1,
                        TargetIndex = TideObservation.CardIndex(me, opp, Zone.Battlefield, j),
                        TargetKindCode = 1,
                    });
                }

                if (core.CombatSystem.CanAttackTarget(me, opp, me))
                {
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Attack,
                        Card = null,
                        HeroAttacker = true,
                        Target = opp,
                        SourceIndex = -1,
                        TargetIndex = -1,
                        TargetKindCode = 2,
                    });
                }

                // 角色攻击对方场地（v15：目标=场地专用槽，与单位攻击行同口径）
                if (oppField != null && core.CombatSystem.CanAttackTarget(me, oppField, me))
                {
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Attack,
                        Card = null,
                        HeroAttacker = true,
                        Target = oppField,
                        SourceIndex = -1,
                        TargetIndex = TideObservation.FieldSlotOf(me, opp),
                        TargetKindCode = 1,
                    });
                }
            }
        }

        /// <summary>战场单位列表（v15 拆场地）：剔场地卡按原序——枚举器循环下标与观测
        /// 战场槽 [0..8] 的单位序编码对齐（CardIndex(zoneIndex)=单位序号）。</summary>
        private static List<Card> BattlefieldUnits(ZoneManager zm, Player p)
        {
            var cards = zm.GetCards(p, Zone.Battlefield);
            var units = new List<Card>(cards.Count);
            foreach (var c in cards)
                if (c != null && !c.IsFieldCard()) units.Add(c);
            return units;
        }

        /// <summary>该侧场地卡（v15：与 BoardState.FieldCardOf 同口径——异常多张取首张）。</summary>
        private static Card FieldCardOf(ZoneManager zm, Player p)
        {
            var cards = zm.GetCards(p, Zone.Battlefield);
            foreach (var c in cards)
                if (c != null && c.IsFieldCard()) return c;
            return null;
        }

        /// <summary>英雄技能行（2026-10-15 改版：技能=技能区法术、每回合一次=横置闸）：技能卡在场/
        /// 未横置/未沉默 + 逐效果 CanActivate 预检（ActivateAsync 守卫镜像）。
        /// 挂 1~3 效果——**逐效果展开目标集**（ModeIndex=效果序号，发动时抉择）；技能卡在场地区——
        /// v3 起场地区槽 [45] 已编码，SourceIndex 可解析。
        /// 2026-11-15 目标选择对齐：与人类的「结算帧选择窗」同源展开（CollectSpecPlan 候选）——
        /// 选一逐候选、选 N 组合、任意数量/动态数量按子集行展开；targets 随动作行下发，
        /// 结算期按预选队列消费（不再靠 AiOrder 代选）。目标源=技能卡（与声明收集/结算同源）。</summary>
        private void EnumerateHeroSkill(GameCore core, Player me)
        {
            var skillCard = HeroSkillSystem.ResolveSkillCard(core, me);
            if (skillCard == null) return;                        // 不在场（未标记/被摧毁/弹回）→ 无技能
            if (!core.ZoneManager.GetCards(me, Zone.FieldZone).Contains(skillCard)) return;
            if (skillCard.IsTapped()) return;                     // 每回合一次闸（回合开始 ResetSkillForTurn 解横置）
            if (skillCard.GetEffectiveCounterCount(CounterRules.SilenceCounter) > 0) return; // 沉默不可发动主动效果（效果口径读数——绝缘体同引擎口径）
            var effects = HeroSkillSystem.SkillEffectsOf(skillCard);
            if (effects.Count == 0) return;

            var executor = core.StackEngine.GetExecutor();
            if (executor == null) return;
            var phase = core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby;
            int turn = core.TurnEngine.TurnNumber;

            for (int e = 0; e < effects.Count; e++)
            {
                var effect = effects[e];
                if (UnsupportedForAi(effect)) continue; // 2026-11-15 审计 §1：技能效果含不支持原子 → 该效果行不枚举
                if (!executor.CanActivate(effect, skillCard, me, me, phase, turn)) continue;
                var slots = new List<TargetSet>();
                ExpandDefinitionTargets(core, me, skillCard, effect, 0, slots, sourceOverride: skillCard);
                if (slots.Count == 0) slots.Add(default);
                int slot = TideObservation.IndexOfCard(core, me, skillCard); // 场地区槽（v3 已编码）
                foreach (var set in slots)
                {
                    if (set.Counts != null && !DynamicCountAffordable(core, me, effect, set.Counts)) continue;
                    AddRowsWithInteraction(core, me, effect, TideActionType.HeroSkill, skillCard, e, slot,
                        set.Targets, dynamicCounts: set.Counts, effect: effect); // P1 v0.3：移动/宣言逐参数展开
                }
            }
        }

        /// <summary>自愿桶待发效果行（v2）：栈机器待发队列中可发动的自愿效果（触发式可选项——
        /// 「响应期自愿触发」决策点）。主动不在此（走 EnumerateActivate 的卡面扫描口径）。
        /// Apply → StackEngine.PlayerActivateVoluntary（速度门在栈机器内）。</summary>
        private void EnumerateVoluntaryTriggers(GameCore core, Player me)
        {
            foreach (var pe in core.StackEngine.GetActivatableVoluntaryEffects(me))
            {
                if (pe?.Effect == null) continue;
                if (pe.Effect.IsActivatedEffect) continue;
                if (UnsupportedForAi(pe.Effect)) continue; // 未来交互原子闸（镜像 SimpleAI；P1 后现无条目）
                Actions.Add(BuildVoluntaryAction(core, me, pe));
            }
        }

        /// <summary>横置产元素行（v3 契约 §1 TapCreature=12）：带地牌特性(LandTrait)的未横置生物，
        /// 横置产 1 元素进临时钱包（产色自动=费用首色，无分支——只决策"哪只/何时"）。
        /// 资格镜像 GameActions.TapCreatureForElement 守卫（教学闸在 RL 为 null 不检）。</summary>
        private void EnumerateTapCreatures(GameCore core, Player me, ZoneManager zm)
        {
            var bf = BattlefieldUnits(zm, me); // v15 拆场地：i 与观测战场单位槽序对齐
            for (int i = 0; i < bf.Count; i++)
            {
                var c = bf[i];
                if (!c.IsAlive || c.IsTapped()) continue;
                if (!c.HasKeyword(Attribute.KeywordRules.LandTrait)) continue;
                if (!ReferenceEquals(c.GetController(), me)) continue;
                Actions.Add(new TideAction
                {
                    Type = TideActionType.TapCreature,
                    Card = c,
                    SourceIndex = TideObservation.CardIndex(me, me, Zone.Battlefield, i),
                    TargetIndex = -1,
                });
            }
        }

        // ===================================================== 响应窗口枚举辅助 =====================================================

        private static TideAction BuildGuardAction(GameCore core, Player me, ResponseOption opt)
        {
            var attackTarget = opt.AttackInstance.Targets != null && opt.AttackInstance.Targets.Count > 0
                ? opt.AttackInstance.Targets[0]
                : null;
            return new TideAction
            {
                Type = TideActionType.Guard,
                Card = opt.SourceCard,
                Guarder = opt.SourceCard != null ? (Entity)opt.SourceCard : (Entity)opt.SourcePlayer,
                Target = attackTarget,
                Targets = attackTarget != null ? new List<Entity> { attackTarget } : null,
                AttackInstance = opt.AttackInstance,
                SourceIndex = TideObservation.IndexOfCard(core, me, opt.SourceCard),
                TargetIndex = attackTarget is Card tc ? TideObservation.IndexOfCard(core, me, tc) : -1,
                TargetKindCode = attackTarget is Player ? 2 : (attackTarget is Card ? 1 : 0),
                ResponseContext = true,
            };
        }

        private static TideAction BuildVoluntaryAction(GameCore core, Player me, PendingEffect pe)
        {
            var srcCard = pe.Source as Card;
            return new TideAction
            {
                Type = TideActionType.VoluntaryTrigger,
                Card = srcCard,
                Effect = pe.Effect,
                Pending = pe,
                SourceIndex = srcCard != null ? TideObservation.IndexOfCard(core, me, srcCard) : -1,
                TargetIndex = -1, // 触发式目标由定义解析，不展开
                ResponseContext = true,
            };
        }

        /// <summary>响应出牌行（v2）：手牌卡在响应窗口打出（发动无效/效果无效等反制）。
        /// 预检镜像 GameActions.PlayCardInResponse/PushCardCast：速度门（2026-11-01 统一口径：
        /// 速≥1 且 速≥记速器——同速可接；响应窗口恒非空栈，空栈首动豁免不触发）
        /// + RuleHooks + 战场容量 + 费用（含 pending 承诺）+ 目标域；目标逐张展开。</summary>
        private void EnumerateRespondPlay(GameCore core, Player me)
        {
            var engine = core.StackEngine;
            var hand = core.ZoneManager.GetCards(me, Zone.Hand);
            if (core.StackEngine != null) hand = hand.Where(c => !core.StackEngine.IsPendingCast(c)).ToList(); // S3b
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (!RuleHooks.CanPlay(core, me, c, Zone.Hand)) continue;
                if (GameActions.GetPayloadGateReject(core, me, c) != null) continue; // 代价门槛镜像（①可支付②无目标③按色地牌——2026-10-31 代价地牌统一定案）
                int castSpeed = SpeedCalculator.GetCardCastSpeed(c);
                if (castSpeed < 1 || castSpeed < engine.SpeedCounter.CurrentSpeed) continue; // 同速可接（镜像 PushCardCast/CollectAvailableResponses）
                if (!IsSpell(c) && !c.IsFieldCard() && !core.ZoneManager.HasBattlefieldSpace(me)) continue; // 场地卡落场地格不计容量（P8）

                int modes = ModeCount(c);
                var castDefs = GameActions.GetCastableEffectDefinitions(c);
                for (int m = 0; m < modes; m++)
                {
                    if (!GameActions.CanAffordCast(core, me, c, m)) continue;
                    if (!TargetDomainService.HasPlayableTargets(core, me, c, m)) continue;
                    if (m < castDefs.Count && UnsupportedForAi(castDefs[m])) continue; // 2026-11-15 审计 §1：镜像 EnumeratePlayCard 闸

                    int src = TideObservation.CardIndex(me, me, Zone.Hand, i);
                    foreach (var set in ExpandCardTargets(core, me, c, m))
                        AddRowsWithInteraction(core, me, m < castDefs.Count ? castDefs[m] : null,
                            TideActionType.RespondPlay, c, m, src, set.Targets, response: true,
                            reference: ReferencePickOf(core, me, c, m, set.Targets),
                            dynamicCounts: set.Counts); // P1 v0.3：移动/宣言逐参数展开
                }
            }
        }

        /// <summary>带目标组合的动作行构造（PlayCard/RespondPlay/Activate/RespondActivate）：
        /// 填 Target 兼容别名、槽位与目标类别特征。reference=参照角色选位镜像（步5 §3.5——
        /// Reference Enumerate spec 的选择结果带进动作行，特征 [11] 编码；null=无参照）。</summary>
        private TideAction BuildTargetedRow(TideActionType type, Card card, int modeIndex, int sourceSlot,
            List<Entity> targets, bool response = false, EffectDefinition effect = null, Entity reference = null,
            List<int> dynamicCounts = null)
        {
            var a = new TideAction
            {
                Type = type,
                Card = card,
                ModeIndex = modeIndex,
                Targets = targets,
                DynamicCounts = dynamicCounts,
                Effect = effect,
                SourceIndex = sourceSlot,
                ResponseContext = response,
                Reference = reference,
                ReferenceIndex = reference is Card rc ? TideObservation.IndexOfCard(_core, _me, rc) : -1,
            };
            a.PlacementCell = -1; // P2：落格行由展开层写格下标（struct 默认 0 须显式哨兵化）
            a.SummonPlan = -1;    // v20：召唤计划行由 EmitSummonPlanRows 写（哨兵化同理）
            a.SummonUnityHits = -1;
            if (targets != null && targets.Count > 0)
            {
                a.Target = targets[0];
                a.TargetIndex = targets[0] is Card tc ? TideObservation.IndexOfCard(_core, _me, tc) : -1;
                a.TargetKindCode = targets[0] is Card ? 1 : (ReferenceEquals(targets[0], _me) ? 3 : 2);
                a.TargetIndex2 = TargetSlotOf(targets, 1); // v8 多目标：次目标槽位（超出 3 个截断不编码）
                a.TargetIndex3 = TargetSlotOf(targets, 2);
            }
            else
            {
                a.TargetIndex = -1;
                a.TargetKindCode = 0;
                a.TargetIndex2 = -1;
                a.TargetIndex3 = -1;
            }
            return a;
        }

        /// <summary>targets[i] 的 cards_ 槽位（v8 多目标）：非卡目标/越界 = -1（与 TargetIndex 同口径）。</summary>
        private int TargetSlotOf(List<Entity> targets, int i)
            => i < targets.Count && targets[i] is Card tc ? TideObservation.IndexOfCard(_core, _me, tc) : -1;

        // ===================================================== 目标展开（镜像 SimpleAI.ChooseTargets 口径） =====================================================

        /// <summary>目标集合行（2026-11-15 目标选择对齐）：Targets=预选目标集（null=引擎自动解析），
        /// Counts=每原子动态数量（下标=效果原子序；null=非动态）。</summary>
        private readonly struct TargetSet
        {
            public readonly List<Entity> Targets;
            public readonly List<int> Counts;
            public TargetSet(List<Entity> targets, List<int> counts = null) { Targets = targets; Counts = counts; }
        }

        /// <summary>出牌目标展开（PlayCard/RespondPlay 共用）：FirstDomainEffect 找带组合域的施放即结算效果，
        /// 按其 SelectionMode 展开；无目标效果返回 [null]（单行，引擎自动解析）。</summary>
        private static List<TargetSet> ExpandCardTargets(GameCore core, Player me, Card card, int modeIndex)
        {
            var result = new List<TargetSet>();
            var def = FirstDomainEffect(card, modeIndex);
            if (def == null)
            {
                result.Add(default); // 无需选目标 → 引擎自动
                return result;
            }
            ExpandDefinitionTargets(core, me, card, def, modeIndex, result);
            result.RemoveAll(s => s.Counts != null && !DynamicCountAffordable(core, me, def, s.Counts));
            if (result.Count == 0) result.Add(default);
            return result;
        }

        /// <summary>按效果定义的目标模式展开（v8 多目标 2026-11-02 两口径分流；2026-11-15 任意/动态对齐）：
        /// <para>逐原子口径（PerAtomTargets=true，header 无 kinds 声明）→ <see cref="ExpandPerAtomTargets"/>：
        /// 候选域=atom.kinds/filter、数量=每原子 effCount（>1 压过选一模式）——与人类声明期收集器/
        /// 结算路径 ResolveAtomTargetsAsync 完全同口径（CollectAtomTargetSpecs 单源）。</para>
        /// 组合层口径（header 域共享）：无域/随机 → 单行 null（引擎自动）；Whole/WholeUnion → 单行全取；
        /// Single/SingleUnion → 逐候选一行；任意数量（TargetCount=-1）→ 1..N 子集行；
        /// Multiple/MultipleUnion（TargetCount&gt;0）→ 引擎候选序组合枚举，上限 MaxTargetCombos 截断。
        /// 候选= ResolveCandidates（与引擎 ResolveCompositionTargetsAsync / SimpleAI.ChooseTargets 同源）。</summary>
        private static void ExpandDefinitionTargets(GameCore core, Player me, Card card, EffectDefinition def,
            int modeIndex, List<TargetSet> sink, Entity sourceOverride = null)
        {
            if (def.PerAtomTargets)
            {
                ExpandPerAtomTargets(core, me, card, def, sink, sourceOverride);
                if (sink.Count == 0) sink.Add(default); // 全部原子自动/免弹 → 单行 null（引擎自解析）
                return;
            }

            var domain = DomainOfMode(def, modeIndex);
            if (domain == null || domain.Count == 0 || def.SelectionMode == SelectionMode.None)
            {
                sink.Add(default);
                return;
            }

            var ctx = BuildTargetCtx(core, me, card, sourceOverride);
            var candidates = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(domain, def.TargetFilter, ctx);
            if (candidates == null || candidates.Count == 0)
            {
                sink.Add(default);
                return;
            }

            switch (def.SelectionMode)
            {
                case SelectionMode.Whole:
                case SelectionMode.WholeUnion:
                    sink.Add(new TargetSet(new List<Entity>(candidates))); // 全取档单行（不弹选择）
                    return;
            }
            if (def.RandomTarget)
            {
                sink.Add(default); // 随机自动抽取：单行交引擎
                return;
            }

            if (SelectionModeRules.IsPickOne(def.SelectionMode))
            {
                foreach (var t in candidates)
                    sink.Add(new TargetSet(new List<Entity> { t }));
                return;
            }

            // 任意数量（-1，2026-11-15 对齐人类「任意数量」窗）：1..N 任意子集 → 显式子集行
            if (def.TargetCount == -1)
            {
                AddSubsetRows(candidates, -1, 0, sink);
                return;
            }

            // 选多（Multiple/MultipleUnion）：固定 TargetCount 组合
            int need = def.TargetCount;
            if (need <= 0 || need >= candidates.Count)
            {
                sink.Add(new TargetSet(new List<Entity>(candidates))); // 未声明/超出候选 → 全取兜底
                return;
            }
            var combos = new List<List<Entity>>();
            EmitCombinations(candidates, need, MaxTargetCombos, combos);
            foreach (var combo in combos) sink.Add(new TargetSet(combo));
        }

        /// <summary>参照角色选位（步5 §3.5）：效果 spec 计划首个 Enumerate spec 为 Reference 时，
        /// 行内 Targets 首个选位即参照（v1 单参照）——镜像进 TideAction.Reference/ReferenceIndex
        /// 供特征 [11] 编码（AI 枚举了参照，特征里必须看得到，否则等于没学）。
        /// null=非参照先行卡（普通目标行）。</summary>
        private static Entity ReferencePickOf(GameCore core, Player me, Card card, int modeIndex, List<Entity> targets)
        {
            if (targets == null || targets.Count == 0) return null;
            var def = FirstDomainEffect(card, modeIndex);
            if (def == null || !def.PerAtomTargets) return null;
            var ctx = BuildTargetCtx(core, me, card);
            var plan = CardCore.Attribute.EffectHandlerRegistry.CollectSpecPlan(def, ctx);
            var first = plan.FirstOrDefault(s => s.AiMode == CardCore.Attribute.AiPickMode.Enumerate);
            return first != null && first.Role == CardCore.Attribute.TargetSpecRole.Reference
                ? targets[0] : null;
        }

        /// <summary>逐原子目标展开（v8 多目标 2026-11-02；步5 2026-11-08 切 spec 计划单源；
        /// 2026-11-15 任意/动态数量对齐人类窗口；P0 2026-11-19 无域 prefix-k）：
        /// 按 CollectSpecPlan 规格**仅展开 AiMode=Enumerate 的 spec**（参照 spec=逐参照候选一行；
        /// 分段受体 spec=Auto 不展开——结算期引擎/AiOrder 代选，不与参照维做笛卡尔积 §7 坑③；
        /// 同域共享组=单 spec 单选位——同域并列原子共享目标）；候选已完成两道过滤（与结算路径
        /// 交互分支同序）；**任意数量（-1）与动态数量（-3）展开 1..N 子集行**（人类任意数量窗的
        /// 等价选择；动态行同时写每原子数量——base×N 计价与 N 消费预选同表）；
        /// 无域牺牲/蛰伏（NoDomainTargetScopes.IsPrefixAtom）例外：候选过 HeroSkillTargetPolicy
        /// 共享序后展开 **prefix-k 前缀行**（k=1..N 取前 k 张，AddPrefixRows——「祭几张」型决策）；
        /// 单目标免弹档
        /// （AutoTake∧effCount&lt;=1）不预选；选 N 档被免弹自动取（候选≤需求）仍显式展开唯一组合
        /// =全候选；选一逐候选一行；选 N（effCount&gt;1）组合枚举；多规格=笛卡尔串接（返回序=计划序
        /// =预选队列有序消费对齐），总行数 cap=MaxExpansionRows 截断。行内 Targets 完整
        /// （特征只编码前 3 个目标 + 目标数）。</summary>
        private static void ExpandPerAtomTargets(GameCore core, Player me, Card card, EffectDefinition def,
            List<TargetSet> sink, Entity sourceOverride = null)
        {
            var ctx = BuildTargetCtx(core, me, card, sourceOverride);
            var specs = CardCore.Attribute.EffectHandlerRegistry.CollectSpecPlan(def, ctx)
                .Where(s => s.AiMode == CardCore.Attribute.AiPickMode.Enumerate)
                .ToList();
            if (specs.Count == 0) return;

            int atomCount = def.Effects?.Count ?? 0;
            var rows = new List<TargetSet> { new TargetSet(new List<Entity>()) }; // 笛卡尔种子（空行）
            foreach (var spec in specs)
            {
                var atomRows = new List<TargetSet>();
                bool dynamic = spec.EffCount == AtomicEffectInstance.DynamicTargetCount;
                if (spec.EffCount == -1 || dynamic)
                {
                    if (NoDomainTargetScopes.IsPrefixAtom(spec.Atom, def))
                    {
                        // P0（2026-11-19 无域原子目标）：牺牲/蛰伏走 prefix-k 前缀行（k=1..N 取前 k 张，
                        // 不枚举 2^N）——候选先过共享 policy（排除归根/场上阿格洛斯、蛰伏滤未横置池地），
                        // 顺序单源 CardCore.AI.HeroSkillTargetPolicy（GreenRampStrategy 同口）。
                        var ordered = spec.Atom.Type == AtomicEffectType.Sacrifice
                            ? HeroSkillTargetPolicy.OrderSacrifice(core, me, spec.Candidates)
                            : HeroSkillTargetPolicy.OrderDormancy(core, me, spec.Candidates);
                        AddPrefixRows(ordered, atomRows);
                    }
                    else
                    {
                        // 任意数量/动态数量：1..N 子集行 = 人类窗口的任意子集选择；
                        // 动态行写每原子数量（-3 → 引擎按 base×N 计价、按 N 消费预选）
                        AddSubsetRows(spec.Candidates, dynamic ? spec.AtomIndex : -1, atomCount, atomRows);
                    }
                }
                else if (spec.AutoTake && spec.EffCount <= 1)
                {
                    continue; // 单目标免弹档：不预选（引擎自动取）
                }
                else if (spec.AutoTake)
                {
                    var forced = new List<Entity>(); // 候选≤需求的选 N 档：唯一组合=全候选（与引擎 AutoTake 同取值）
                    for (int i = 0; i < spec.Candidates.Count && i < spec.EffCount; i++) forced.Add(spec.Candidates[i]);
                    atomRows.Add(new TargetSet(forced));
                }
                else if (spec.EffCount <= 1)
                {
                    foreach (var t in spec.Candidates)
                        atomRows.Add(new TargetSet(new List<Entity> { t })); // 选一：逐候选一行
                }
                else
                {
                    // 选 N：候选≤需求已由 AutoTake 吸收（ShouldAutoTake），此处恒组合枚举
                    var combos = new List<List<Entity>>();
                    EmitCombinations(spec.Candidates, spec.EffCount, MaxTargetCombos, combos);
                    foreach (var combo in combos) atomRows.Add(new TargetSet(combo));
                }

                if (atomRows.Count == 0) continue;
                rows = MergeCartesian(rows, atomRows, atomCount);
            }
            foreach (var r in rows)
                if (r.Targets.Count > 0) sink.Add(r);
        }

        /// <summary>目标集笛卡尔串接（多 spec 合并；Counts 按原子下标合并——各 spec 只写自己的原子位）。</summary>
        private static List<TargetSet> MergeCartesian(List<TargetSet> rows, List<TargetSet> atomRows, int atomCount)
        {
            var next = new List<TargetSet>();
            foreach (var prefix in rows)
            {
                foreach (var tail in atomRows)
                {
                    var merged = new List<Entity>(prefix.Targets);
                    merged.AddRange(tail.Targets);
                    List<int> counts = null;
                    if (prefix.Counts != null || tail.Counts != null)
                    {
                        counts = new List<int>(atomCount);
                        for (int i = 0; i < atomCount; i++) counts.Add(0);
                        if (prefix.Counts != null)
                            for (int i = 0; i < atomCount && i < prefix.Counts.Count; i++) counts[i] = prefix.Counts[i];
                        if (tail.Counts != null)
                            for (int i = 0; i < atomCount && i < tail.Counts.Count; i++) counts[i] = tail.Counts[i];
                    }
                    next.Add(new TargetSet(merged, counts));
                    if (next.Count >= MaxExpansionRows) return next;
                }
            }
            return next;
        }

        /// <summary>任意子集行（2026-11-15 目标选择对齐）：k=1..N 的候选组合，上限 MaxSubsetRows；
        /// 末尾恒补「全取」行（大数量选择：一次祭光全部候选）。atomIndex&lt;0=非动态（不写数量）。
        /// 取舍说明：平坦动作空间无法枚举 2^N 全部子集（与固定 N 档的 24 组上限同一取舍）——
        /// 这里覆盖「小数量全组合 + 全取」，训练可学「祭几张、祭哪几张」的核心决策；
        /// 超出上限的大数量组合不在动作空间内（可走「全取」行或策略代选行）。</summary>
        private static void AddSubsetRows(List<Entity> candidates, int atomIndex, int atomCount, List<TargetSet> sink)
        {
            int n = candidates.Count;
            int start = sink.Count;
            for (int k = 1; k <= n; k++)
            {
                int remain = MaxSubsetRows - (sink.Count - start);
                if (remain <= 0) break;
                var combos = new List<List<Entity>>();
                EmitCombinations(candidates, k, remain, combos);
                foreach (var combo in combos)
                    sink.Add(new TargetSet(combo, atomIndex >= 0 ? NewCounts(atomCount, atomIndex, combo.Count) : null));
            }
            if (n > 1)
            {
                bool hasAll = false;
                for (int i = start; i < sink.Count; i++)
                    if (sink[i].Targets.Count == n) { hasAll = true; break; }
                if (!hasAll)
                    sink.Add(new TargetSet(new List<Entity>(candidates),
                        atomIndex >= 0 ? NewCounts(atomCount, atomIndex, n) : null));
            }
        }

        /// <summary>prefix-k 前缀行（P0 2026-11-19 无域原子目标）：k=1..N，每行 Targets=候选前 k 张
        ///（候选序=HeroSkillTargetPolicy 共享序；k=Targets 长度本身，TideAction 不加字段）。
        /// 上限 MaxPrefixRows；N 超上限时只发 1..MaxPrefixRows-1 + 全取行（与 AddSubsetRows 的
        /// 「小数量 + 全取」取舍同口径）。不写 Counts（任意数量 -1：目标集即选择结果，无动态计价）。</summary>
        private static void AddPrefixRows(List<Entity> candidates, List<TargetSet> sink)
        {
            int n = candidates.Count;
            if (n <= MaxPrefixRows)
            {
                for (int k = 1; k <= n; k++)
                    sink.Add(new TargetSet(candidates.Take(k).ToList()));
                return;
            }
            for (int k = 1; k <= MaxPrefixRows - 1; k++)
                sink.Add(new TargetSet(candidates.Take(k).ToList()));
            sink.Add(new TargetSet(new List<Entity>(candidates))); // 全取行（k=n 超上限，兜底补上）
        }

        /// <summary>手牌上限弃牌行上限（P3 2026-11-19 §4.1）：over 张组合 ≤24；
        /// over&gt;3 时前组合截断 + 恒补默认组（前 over 张）防死锁（停靠决策必至少一行）。</summary>
        private const int MaxDiscardRows = 24;

        /// <summary>P3（2026-11-19 §4.1）手牌上限弃牌行——**独立枚举口**（非主动作流）：
        /// 驱动（TideHeadlessDriver）在 HandLimitDiscardGate 挂起时停靠调用。行=over 张组合
        ///（over=1 逐张 / over=2 C(n,2) / over≥3 组合截断+默认组），Targets=选中卡组，
        /// 特征 [10]=张数、[3]=首卡槽、[14..17]=-1。恒至少一行（默认组兜底）。</summary>
        public static LegalActionEnumerator EnumerateDiscardChoices(GameCore core, Player me,
            IReadOnlyList<Card> hand, int over)
        {
            var lae = new LegalActionEnumerator { _core = core, _me = me };
            var cards = new List<Card>();
            if (hand != null)
                foreach (var c in hand)
                    if (c != null) cards.Add(c); // 不滤 IsAlive——手牌卡 IsAlive=false 是常态（仅战场/在场为 true，池地牌同例）
            if (cards.Count == 0 || over <= 0)
            {
                lae.Actions.Add(new TideAction { Type = TideActionType.DiscardChoice, SourceIndex = -1, TargetIndex = -1 });
                return lae;
            }

            void Emit(List<Entity> combo)
            {
                var row = new TideAction
                {
                    Type = TideActionType.DiscardChoice,
                    Targets = combo,
                    SourceIndex = -1,
                    TargetIndex = combo[0] is Card tc ? TideObservation.IndexOfCard(core, me, tc) : -1,
                    TargetKindCode = 1,
                };
                lae.Actions.Add(row);
            }

            int n = cards.Count;
            if (n <= over)
            {
                // 候选不足 over（快照竞态/防御）：单行全取（默认组语义，恒可解）
                var all = new List<Entity>();
                foreach (var c in cards) all.Add(c);
                Emit(all);
            }
            else if (over == 1)
            {
                foreach (var c in cards) Emit(new List<Entity> { c });
            }
            else
            {
                var combos = new List<List<Entity>>();
                EmitCombinations(cards.Cast<Entity>().ToList(), over, MaxDiscardRows - 1, combos);
                foreach (var combo in combos) Emit(combo);
                // 默认组兜底（组合截断/候选不足）：前 over 张——已含于组合集时不重复补
                //（C(n,over) 全量枚举时默认组=字典序首组合；2026-11-19 反馈：重复行致 over=2 出 11 行）
                if (n > 1)
                {
                    var def = new List<Entity>();
                    for (int i = 0; i < n && def.Count < over; i++) def.Add(cards[i]);
                    bool already = false;
                    foreach (var r in lae.Actions)
                    {
                        if (r.Targets.Count != def.Count) continue;
                        var ra = r.Targets.ToList();
                        var da = def.ToList();
                        bool same = true;
                        for (int i = 0; i < da.Count; i++)
                            if (!ReferenceEquals(ra[i], da[i])) { same = false; break; }
                        if (same) { already = true; break; }
                    }
                    if (!already) Emit(def);
                }
            }
            lae.BuildFeatures();
            return lae;
        }

        // ===================================================== P1（2026-11-19 v0.3）移动/宣言交互行 =====================================================

        /// <summary>含移动/宣言原子的效果的交互参数展开（v0.3 §3.1/§4.2）：
        /// 返回 null=效果无这两类原子（行原样单行）；空表=不可用（无空格/候选空/多目标移动——
        /// 该效果不产生行，AI 不发动，不白付）；非空=逐参数一行。
        /// 移动=单目标 v1（targets 中恰 1 个己方战场单位；多目标/无目标不枚举，v0.3 §8），
        /// 行=目标×合法空格（MoveUnitRules 单源，≤MaxMoveRows）；
        /// 宣言=候选名一行（DeclarationCandidates 单源——只读已读到的，≤MaxDeclareRows）；
        /// 两类并存取笛卡尔。declaredNames 回传候选表（特征 [16] 序号同源）。</summary>
        private static List<InteractionParams> InteractionVariants(GameCore core, Player me, Card card,
            EffectDefinition def, List<Entity> targets, out List<string> declaredNames)
        {
            declaredNames = null;
            AtomicEffectInstance moveAtom = null, declareAtom = null, prophecyAtom = null;
            if (def?.Effects != null)
                foreach (var a in def.Effects)
                {
                    if (a == null) continue;
                    if (a.Type == AtomicEffectType.MoveUnit && moveAtom == null) moveAtom = a;
                    if ((a.Type == AtomicEffectType.SearchDeck
                         || a.Type == AtomicEffectType.DeclareHand
                         || a.Type == AtomicEffectType.DeclareDeck)
                        && string.IsNullOrWhiteSpace(a.StringValue) && declareAtom == null) declareAtom = a;
                    // P3（2026-11-19 §4.2）预言类别：封闭三选一（Type:Creature/Spell/Enchantment——
                    // ProphecyHandlerUtil.TypeOptions 同款编码），消费口 AskDimensionAsync 读 DeclaredName
                    if (a.Type == AtomicEffectType.ProphecyNextCard && prophecyAtom == null) prophecyAtom = a;
                }
            if (moveAtom == null && declareAtom == null && prophecyAtom == null) return null;

            // P3-4.2：预言类别三选一行（Type:Creature/Spell/Enchantment——
            // ProphecyHandlerUtil.TypeOptions 同款编码；消费口 AskDimensionAsync 读 DeclaredName，
            /// 特征 [16]=0/1/2）。与移动/宣言并存时在文末统一取笛卡尔（现表无并存卡，防御路径）。
            List<InteractionParams> prophecyVariants = null;
            List<string> prophecyNames = null;
            if (prophecyAtom != null)
            {
                prophecyNames = new List<string> { "Type:Creature", "Type:Spell", "Type:Enchantment" };
                prophecyVariants = new List<InteractionParams>();
                foreach (var n in prophecyNames) prophecyVariants.Add(new InteractionParams { DeclaredName = n });
                if (moveAtom == null && declareAtom == null)
                {
                    declaredNames = prophecyNames;
                    return prophecyVariants;
                }
            }

            // 移动：单目标 v1——targets 恰 1 个己方战场单位才有落格可言
            List<(int x, int z)> cells = null;
            if (moveAtom != null)
            {
                Card unit = null;
                int units = 0;
                if (targets != null)
                    foreach (var t in targets)
                        if (t is Card c && c.IsAlive && c.GetZone() == Zone.Battlefield
                            && ReferenceEquals(c.GetController() ?? c.GetOwner(), me))
                        {
                            unit = c;
                            units++;
                        }
                if (units == 0)
                {
                    // 单目标免弹档（AutoTake）：行 Targets=null（引擎自动取首候选）——从 spec 计划
                    // 取该原子候选定落格单位（AutoTake=候选≤需求，need=1 时恰 1 个；行内 Targets
                    // 保持 null，结算期引擎自动解析出同一单位 + Interaction 落格）
                    var ctx = BuildTargetCtx(core, me, card);
                    var spec = CardCore.Attribute.EffectHandlerRegistry.CollectSpecPlan(def, ctx)
                        .FirstOrDefault(s => ReferenceEquals(s.Atom, moveAtom));
                    if (spec?.Candidates != null)
                        foreach (var cand in spec.Candidates)
                            if (cand is Card c2 && c2.IsAlive && c2.GetZone() == Zone.Battlefield
                                && ReferenceEquals(c2.GetController() ?? c2.GetOwner(), me))
                            {
                                unit = c2;
                                units++;
                            }
                }
                if (units != 1)
                {
                    // 修复（2026-11-19 反馈）：多目标/无目标移动不展开落格（v0.3 §8）——但**宣言/预言
                    // 变体不受牵连**：无移动维继续走宣言/预言路径（此前整效果弃行=同卡宣言被移动拖死）；
                    // 纯移动效果维持不产生行（无格可指=不发动，不白付）。
                    if (declareAtom == null && prophecyAtom == null) return new List<InteractionParams>();
                    cells = null;
                }
                else
                {
                    cells = GameBoard.MoveUnitRules.LegalDestinations(unit);
                    if (cells.Count == 0) return new List<InteractionParams>(); // 无空格不发动
                    if (cells.Count > MaxMoveRows) cells = cells.Take(MaxMoveRows).ToList();
                }
            }

            // 宣言：候选名单源（只读已读到的）；空集不发动
            List<string> names = null;
            if (declareAtom != null)
            {
                names = DeclarationCandidates.For(core, me, declareAtom);
                if (names.Count == 0) return new List<InteractionParams>();
                if (names.Count > MaxDeclareRows) names = names.Take(MaxDeclareRows).ToList();
                declaredNames = names;
            }

            var variants = new List<InteractionParams>();
            if (cells != null && names != null)
                foreach (var n in names)
                    foreach (var cell in cells)
                        variants.Add(new InteractionParams { DeclaredName = n, MoveX = cell.x, MoveZ = cell.z });
            else if (cells != null)
                foreach (var cell in cells)
                    variants.Add(new InteractionParams { MoveX = cell.x, MoveZ = cell.z });
            else if (names != null)
                foreach (var n in names)
                    variants.Add(new InteractionParams { DeclaredName = n });

            // P3-4.2：预言类别与移动/宣言并存（现表无，防御）——笛卡尔（宣言名位给类别档）
            if (prophecyVariants != null)
            {
                var mixed = new List<InteractionParams>();
                if (variants.Count == 0) variants.Add(new InteractionParams());
                foreach (var v in variants)
                    foreach (var pv in prophecyVariants)
                        mixed.Add(new InteractionParams
                        {
                            DeclaredName = pv.DeclaredName,
                            MoveX = v.MoveX, MoveZ = v.MoveZ,
                        });
                declaredNames = prophecyNames;
                return mixed;
            }
            return variants;
        }

        /// <summary>（P2 2026-11-19 §3.1）永久物落格候选：BoardLayout.UnitCells(己方) 中**空格**的
        /// 格下标（0..8，≤MaxPlaceRows）。仅生物/结界（法术/场地不占单位区格）。
        /// 棋盘未装配（无头/服务器）=几何全格（占用不可知——声明侧不裁，引擎首格兜底；
        /// Apply 侧无棋盘自然不 Pin）。返回 null=非落格行（法术/场地，单行直发）。
        /// v20：空格判定改走单源 SummonPlacementRules.FreeCells（与召唤落位计划共用口径）。</summary>
        private static List<int> PlacementCellChoices(GameCore core, Player me, Card card)
        {
            if (card == null || card.IsSpellCard() || card.IsFieldCard()) return null;
            var board = GameBoard.MoveUnitRules.Current;
            if (board == null)
            {
                // 无棋盘：几何全格（占用不可知——声明侧不裁，≤MaxPlaceRows）
                int ownerIdx = ReferenceEquals(core.Player1, me) ? 0 : 1;
                var geo = GameBoard.BoardLayout.UnitCells(ownerIdx);
                var all = new List<int>();
                for (int i = 0; i < geo.Count && all.Count < MaxPlaceRows; i++) all.Add(i);
                return all;
            }
            var free = GameBoard.SummonPlacementRules.FreeCells(board, me);
            if (free.Count > MaxPlaceRows) free = free.Take(MaxPlaceRows).ToList();
            return free;
        }

        /// <summary>主序列含召唤衍生物原子（v20 2026-11-20 计划 §3.3）：该动作的召唤带落位计划——
        /// 召唤是独立原子（独占一条效果），主序列恰一只；计划行展开见 EmitSummonPlanRows，
        /// 结算期 SummonTokenHandler 消费 Interaction.SummonCells。</summary>
        private static bool HasMainSummon(EffectDefinition def)
            => def?.Effects != null
               && def.Effects.Any(a => a != null && a.Type == AtomicEffectType.SummonToken);

        /// <summary>（P1 v0.3 / P2 / v20 计划 §3.3）按目标集出行：无交互原子的效果单行直发；
        /// 含移动/宣言原子的效果逐交互参数一行（变体空集=不产生行）；**永久物（生物/结界）再乘
        /// 落格维一行一格**（PlacementCell=UnitCells 下标，特征 [17]；无空格=该行不产生——上层
        /// HasBattlefieldSpace 已闸满区）。
        /// v20 召唤落位计划行（2026-11-20 计划）：主序列含召唤原子的**法术出牌/主动发动**效果
        ///（非永久物出牌——永久物召唤以自身钉格为聚集核走默认计划）按「默认计划 1 行 + 每空格
        /// 锚点计划各 1 行」展开（合计 ≤MaxSummonRows=12；去重同序列留首；候选空=0 行=不发动）。
        /// 每行带 Interaction.SummonCells（结算期 SummonTokenHandler 逐只按序落位）、
        /// SummonPlan（0=默认/1..9=锚点，特征 [18]）、SummonUnityHits（PredictUnityHits，
        /// 特征 [19]）、PlacementCell=计划首格（特征 [17]——含 Activate 行，防 FoldEquivalentRows 误并）。
        /// 特征 [14]/[15]/[16]/[17]/[18]/[19] 由 BuildFeatures 读取。</summary>
        private void AddRowsWithInteraction(GameCore core, Player me, EffectDefinition def,
            TideActionType type, Card card, int modeIndex, int sourceSlot, List<Entity> targets,
            bool response = false, Entity reference = null, List<int> dynamicCounts = null,
            EffectDefinition effect = null)
        {
            var variants = InteractionVariants(core, me, card, def, targets, out var declaredNames);
            if (variants != null && variants.Count == 0) return; // 交互不可用（空格/候选空/多目标移动）

            // v20 召唤计划行：独立分流（不入 InteractionVariants——不污染宣言/移动/预言路径；
            // 召唤为独立原子独占效果，恒无宣言/移动变体）。永久物出牌仍走 P2 落格行（下方）。
            bool permanentPlay = (type == TideActionType.PlayCard || type == TideActionType.RespondPlay)
                                 && card != null && !card.IsSpellCard() && !card.IsFieldCard();
            if (HasMainSummon(def) && !permanentPlay)
            {
                EmitSummonPlanRows(core, me, def, type, card, modeIndex, sourceSlot, targets,
                    response, reference, dynamicCounts, effect);
                return;
            }

            // P2：落格维（仅 PlayCard/RespondPlay 的永久物行；Activate/技能不落格）
            List<int> placeCells = null;
            if ((type == TideActionType.PlayCard || type == TideActionType.RespondPlay)
                && (variants == null || variants.Count <= 1))
                placeCells = PlacementCellChoices(core, me, card);
            if (placeCells != null && placeCells.Count == 0) return; // 满区无空格（防御——上层已闸）

            void Emit(InteractionParams inter)
            {
                if (placeCells == null)
                {
                    var row0 = BuildTargetedRow(type, card, modeIndex, sourceSlot, targets, response, effect,
                        reference, dynamicCounts);
                    row0.Interaction = inter;
                    if (inter != null && declaredNames != null)
                        inter.DeclaredIndex = Math.Min(declaredNames.IndexOf(inter.DeclaredName), 15);
                    Actions.Add(row0);
                    return;
                }
                foreach (var ci in placeCells)
                {
                    var row = BuildTargetedRow(type, card, modeIndex, sourceSlot, targets, response, effect,
                        reference, dynamicCounts);
                    row.Interaction = inter;
                    if (inter != null && declaredNames != null)
                        inter.DeclaredIndex = Math.Min(declaredNames.IndexOf(inter.DeclaredName), 15);
                    row.PlacementCell = ci; // P2：格下标 0..8（特征 [17]）
                    Actions.Add(row);
                }
            }

            if (variants == null) Emit(null);
            else foreach (var v in variants) Emit(v);
        }

        /// <summary>召唤计划行总数上限（计划 §3.3：默认 1 + 锚点 ≤9 + 去重余量）。</summary>
        private const int MaxSummonRows = 12;

        /// <summary>召唤计划行展开（v20 计划 §3.3）：默认计划 1 行（SummonCells=null=结算期
        /// DefaultCell）+ 每空格锚点计划 1 行（BuildAnchorPlan 确定性填充）。行特征：
        /// [17]=计划首格（默认行=-1）、[18]=SummonPlan、[19]=SummonUnityHits；Signature 含
        /// plan/cells（防折叠误并）。无空格=0 行（不发动）。count 取召唤原子 value（Max(1,·)）。 </summary>
        private void EmitSummonPlanRows(GameCore core, Player me, EffectDefinition def,
            TideActionType type, Card card, int modeIndex, int sourceSlot, List<Entity> targets,
            bool response, Entity reference, List<int> dynamicCounts, EffectDefinition effect)
        {
            var board = GameBoard.MoveUnitRules.Current;
            var free = GameBoard.SummonPlacementRules.FreeCells(board, me);
            if (free.Count == 0) return; // 计划 §3.3：候选空=0 行=不发动

            var summonAtom = def.Effects.First(a => a.Type == AtomicEffectType.SummonToken);
            int count = Math.Max(1, summonAtom.Value);
            var battlefield = core.ZoneManager.GetCards(me, Zone.Battlefield);
            // 计划格衍生物模板的团结门槛（模板自带团结卡时预测含新格自身）
            int tplThreshold = 0;
            if (!string.IsNullOrEmpty(summonAtom.StringValue))
            {
                var tpl = Attribute.Handlers.SummonTokenHandler.ResolveTemplate?.Invoke(summonAtom.StringValue);
                if (tpl != null) tplThreshold = GameBoard.SummonPlacementRules.UnityThresholdOf(
                    new CardWrapper(tpl) { ID = summonAtom.StringValue });
            }

            int emittedRows = 0;
            void AddRow(List<int> cells, int planId)
            {
                var row = BuildTargetedRow(type, card, modeIndex, sourceSlot, targets, response, effect,
                    reference, dynamicCounts);
                if (cells != null)
                {
                    row.Interaction = new InteractionParams { SummonCells = new List<int>(cells) };
                    row.PlacementCell = cells[0];
                }
                row.SummonPlan = planId; // 0=默认 / 1..9=锚点（锚点 UnitCells 下标+1）
                // 默认行 [19]=-1（未估——2026-11-20 验收定案）：默认计划=结算期引擎自适应
                //（母巢 2×3 块/贪心逐格现算），声明期格序未定、预测必低估（只算现有团结卡不含
                // 新衍生物）——置 -1 不造假信号；锚点行才给真实预测（站位选择的学习信号）。
                row.SummonUnityHits = cells != null
                    ? GameBoard.SummonPlacementRules.PredictUnityHits(board, me, battlefield, cells, tplThreshold)
                    : -1;
                Actions.Add(row);
                emittedRows++;
            }

            AddRow(null, 0); // 默认计划行（无 SummonCells → 结算期 DefaultCell）
            var emitted = new HashSet<string> { "-" }; // 序列去重（默认行哨兵）
            foreach (var anchor in free)
            {
                if (emittedRows >= MaxSummonRows) break; // 上限兜底（默认 1 + 锚点 ≤9 + 余量）
                var plan = GameBoard.SummonPlacementRules.BuildAnchorPlan(board, me, anchor, count);
                if (plan == null || plan.Count == 0) continue;
                string key = string.Join(".", plan);
                if (!emitted.Add(key)) continue; // 相同序列只留首
                AddRow(plan, anchor + 1);
            }
        }

        private static List<int> NewCounts(int atomCount, int atomIndex, int value)
        {
            var counts = new List<int>(atomCount);
            for (int i = 0; i < atomCount; i++) counts.Add(0);
            if (atomIndex >= 0 && atomIndex < counts.Count) counts[atomIndex] = value;
            return counts;
        }

        /// <summary>动态数量行预过滤（2026-11-15）：按 base×N 推费用粗检（总费 ≤ 当前可用总量）——
        /// 精确逐色可付性仍由 Apply 时引擎终审；此闸只去掉明显付不起的行（降动作掩码噪声）。</summary>
        private static bool DynamicCountAffordable(GameCore core, Player me, EffectDefinition def,
            IReadOnlyList<int> counts)
        {
            if (counts == null || def == null) return true;
            var cost = CostDerivationService.DeriveElementCosts(def, 0, counts);
            if (cost == null || cost.Total <= 0f) return true;
            return cost.Total <= core.ElementPool.GetTotalAvailableMana(me) + 0.001f;
        }

        /// <summary>候选序组合枚举（字典序前 cap 组）：候选序=引擎 ResolveCandidates 返回序（契约口径）。</summary>
        private static void EmitCombinations(List<Entity> cands, int k, int cap, List<List<Entity>> sink)
        {
            int n = cands.Count;
            if (k <= 0 || k > n) return; // k>n 无组合（调用方兜底）——防 idx 越界（2026-11-19 audit 第10局崩点）
            var idx = new int[k];
            for (int i = 0; i < k; i++) idx[i] = i;
            int emitted = 0;
            while (emitted < cap)
            {
                var combo = new List<Entity>(k);
                for (int i = 0; i < k; i++) combo.Add(cands[idx[i]]);
                sink.Add(combo);
                emitted++;

                int p = k - 1;
                while (p >= 0 && idx[p] == n - k + p) p--;
                if (p < 0) return;
                idx[p]++;
                for (int i = p + 1; i < k; i++) idx[i] = idx[i - 1] + 1;
            }
        }

        // ===================================================== 特征构造 =====================================================

        private void BuildFeatures()
        {
            int n = Actions.Count;
            if (_core == null || _me == null)
            {
                // 无枚举上下文（不应发生）——按全 0 特征兜底，维度保持正确
                Features = new float[n * TideObservation.NAction];
                return;
            }
            Features = new float[n * TideObservation.NAction];
            for (int k = 0; k < n; k++)
            {
                var a = Actions[k];
                int b = k * TideObservation.NAction;
                Features[b + 0] = 1f;                                        // valid
                Features[b + 1] = (int)a.Type;                               // type 0..12（2026-11-06 重排后连续无洞）
                Features[b + 2] = a.SourceIndex;                             // cards_ 槽下标（-1=无）
                Features[b + 3] = a.TargetIndex;                             // cards_ 槽下标（-1=无/玩家目标）
                Features[b + 4] = a.ModeIndex;                               // 抉择模式 / HeroSkill=skillId
                Features[b + 5] = ActionCost(a);                             // 费用总和（含 Activate/Voluntary/HeroSkill）
                Features[b + 6] = a.Effect != null                           // 效果定义哈希注册下标 / 1024
                    ? TideCardIndex.IndexOfEffectDefinition(a.Effect) / 1024f
                    : 0f;
                Features[b + 7] = a.TargetKindCode;                          // 0 无/1 卡/2 对方玩家/3 己方玩家
                // v8 多目标（2026-11-02 动作空间扩维）：次目标槽位 + 目标数——只有走 BuildTargetedRow
                // 目标展开的四类动作才有次目标；其余类型（Attack/Guard 恒单目标、无目标类）恒 -1/按
                // TargetKindCode 单目标计数（构造点不设字段，struct 默认 0 在此闸强制归位，防误读为槽 0）
                bool expanded = a.Type == TideActionType.PlayCard || a.Type == TideActionType.RespondPlay
                    || a.Type == TideActionType.Activate || a.Type == TideActionType.RespondActivate
                    || a.Type == TideActionType.HeroSkill // 2026-11-15：英雄技能也按目标集展开（目标数/次目标可见）
                    || a.Type == TideActionType.DiscardChoice; // P3 2026-11-19：弃牌行 [10]=选中张数
                Features[b + 8] = expanded ? a.TargetIndex2 : -1;            // Targets[1] 槽位（-1=无/截断）
                Features[b + 9] = expanded ? a.TargetIndex3 : -1;            // Targets[2] 槽位（-1=无/截断）
                Features[b + 10] = expanded                                  // 目标数（0..16 钳；全取行=候选数）
                    ? Math.Min(a.Targets?.Count ?? 0, 16) // P0 2026-11-19：0..8→0..16（prefix-k 行 k=9..16 须可分）
                    : (a.TargetKindCode > 0 ? 1 : 0);                        // Attack/Guard 单目标=1，无目标=0
                // v13 参照槽位（步5 2026-11-08 参照 Enumerate 进动作空间）：Reference 角色选位镜像——
                // AI 枚举了参照（活体炸弹的己方生物参照），特征必须可分（否则策略看不到选了哪个参照）。
                // -1=无参照/玩家参照（与 TargetIndex 同口径）。
                Features[b + 11] = expanded ? a.ReferenceIndex : -1;
                // v14 目标区域（2026-11-15 共享规格 v0.2 §6）：首目标的区域序/归属——判定走
                // TargetZoneGrouping 公开单源（枚举者视角=me），LAE 不另写规则；无目标=-1。
                // 多目标只编码首目标（与 target2/3 截断同口径，其余经观测 zoneKind 可分辨）。
                var firstTarget = a.Targets != null && a.Targets.Count > 0 ? a.Targets[0] : a.Target;
                Features[b + 12] = firstTarget is Card tc1
                    ? TargetZoneGrouping.ZoneRankOf(tc1.GetZone())
                    : firstTarget != null ? 0 : -1; // Player 候选=角色组（rank0）
                Features[b + 13] = firstTarget != null ? TargetZoneGrouping.SideRankOf(firstTarget, _me) : -1;
                // P1（2026-11-19 v0.3）移动/宣言参数维：[14]/[15]=Interaction.MoveX/Z（-1=无/非展开行），
                // [16]=宣言候选序号（同效果候选表内 0..15；-1=非宣言行）。移动行=目标×合法空格、
                // 宣言行=候选名逐行——非法格/空候选不产生行（Apply 前已过滤）。
                Features[b + 14] = expanded && a.Interaction != null ? a.Interaction.MoveX : -1;
                Features[b + 15] = expanded && a.Interaction != null ? a.Interaction.MoveZ : -1;
                Features[b + 16] = expanded && a.Interaction != null ? a.Interaction.DeclaredIndex : -1;
                // P2（2026-11-19 §3.1）落格维：永久物 PlayCard/RespondPlay 行=UnitCells(己方) 格下标
                ///（0..8；-1=非落格行）。声明成功即 Pin（TryPinPlacementCell），入场 Resync 钉优先生效。
                // v20（计划 §3.3）闸修复+扩义：召唤计划行（含 Activate/RespondActivate——战斗母巢
                /// 主动召唤）也写「计划首格」——此前类型闸漏 Activate 致 9 个锚点行 [17] 恒 -1，
                /// 被 FoldEquivalentRows 误并成 1 行（AI 看不到站位选择）。
                Features[b + 17] = (a.Type == TideActionType.PlayCard || a.Type == TideActionType.RespondPlay
                                    || a.Type == TideActionType.Activate || a.Type == TideActionType.RespondActivate)
                    ? a.PlacementCell
                    : -1;
                // v20 召唤计划维（计划 §5，NAction 18→20）：[18]=计划 id（0=默认/1..9=锚点/-1=非召唤）；
                // [19]=该计划落位后团结达标数（0..6/-1=默认行未估/非召唤）——站位计划与团结收益
                // 直接关联（拍板保留）。**类型闸必加**（2026-11-20 验收修复）：Attack/Guard/PlayLand/
                // TapCreature/EndTurn/DiscardChoice 等构造点不走 BuildTargetedRow，struct 默认 0
                // 会被误读为「默认召唤计划」——与 [17] 同款类型闸 + BuildTargetedRow 哨兵 -1 双保险。
                bool summonRow = a.SummonPlan >= 0
                    && (a.Type == TideActionType.PlayCard || a.Type == TideActionType.RespondPlay
                        || a.Type == TideActionType.Activate || a.Type == TideActionType.RespondActivate);
                Features[b + 18] = summonRow ? a.SummonPlan : -1;
                Features[b + 19] = summonRow ? a.SummonUnityHits : -1;
            }
        }

        private static float ActionCost(TideAction a)
        {
            switch (a.Type)
            {
                case TideActionType.PlayCard:
                case TideActionType.RespondPlay:
                    return GameActions.GetCardCost(a.Card, a.ModeIndex, a.DynamicCounts).Total; // 动态数量：base×N 同表
                case TideActionType.Activate:
                case TideActionType.RespondActivate:
                case TideActionType.VoluntaryTrigger:
                case TideActionType.HeroSkill: // 技能发动费（2026-11-01 锚价现付定案）：与主动效果同价源现推
                    // 2026-11-15：动态数量行按本次所选 N 现推（base×N），与 Apply 的实际扣费同表
                    return CostDerivationService.DeriveElementCosts(a.Effect, 0, a.DynamicCounts).Total; // 效果费现推（主动/自愿桶发动时现付——L1 现推口径）
                default:
                    return 0f;
            }
        }

        // ===================================================== 辅助谓词（SimpleAI 同源口径） =====================================================

        private static int ModeCount(Card c)
        {
            if (!(c is CardWrapper w)) return 1;
            // 法术模式数（CostDerivationService 单源——P12 充能排除已随 2026-11-05 充能系统动作化改版失效）
            return Math.Max(1, CostDerivationService.GetModeCount(w.GetData()));
        }

        private static bool IsSpell(Card c)
            => c is IHasSupertype s && s.Supertype == Cardtype.Spell;

        // 费用可付性：GameActions.CanAfford（含 pending 声明承诺 + 统一混付口径）——消除动作掩码/引擎门槛分叉。

        private static List<EffectDefinition> GetEffectDefinitions(Card card)
        {
            if (!(card is CardWrapper wrapper)) return new List<EffectDefinition>();
            var data = wrapper.GetData();
            if (data?.Effects == null || data.Effects.Count == 0) return new List<EffectDefinition>();
            return CardEffectConverter.ConvertAll(data.Effects, data.ID);
        }

        // ---- 目标展开的 SimpleAI 镜像（ChooseTargets/FirstDomainEffect/DomainOfMode/BuildTargetCtx 同源） ----

        /// <summary>第一个带组合域的非主动效果定义（施放即结算才会随出牌自动跑）；
        /// None 模式（区域自结算类）跳过——不为其选目标。
        /// 法术（2026-10-15 改版）：效果全为主动、施放时抉择其一——modeIndex=效果序号，
        /// 目标展开按所选那条（其余效果本局不发动）。
        /// v8（2026-11-02）：None 过滤对逐原子口径（PerAtomTargets）豁免——选择参数在原子上
        ///（atom.kinds/effCount），header None 不代表无交互原子。</summary>
        private static EffectDefinition FirstDomainEffect(Card card, int modeIndex)
        {
            var defs = GetEffectDefinitions(card);
            if (card.IsSpellCard())
            {
                // P12：modeIndex 索引非充能可施放列表（充能不算法术模式——与引擎
                // SpellCastAnchor/ResolveSpellEffects 同序；旧按全量 defs 取模式会错位到充能行）
                var castable = GameActions.GetCastableEffectDefinitions(card);
                if (castable.Count == 0) return null;
                var spellDef = castable[Math.Clamp(modeIndex, 0, castable.Count - 1)];
                if (spellDef == null) return null;
                if (spellDef.SelectionMode == SelectionMode.None && !spellDef.PerAtomTargets) return null;
                return spellDef.TargetDomain != null && spellDef.TargetDomain.Count > 0 ? spellDef : null;
            }
            foreach (var def in defs)
            {
                if (def.IsActivatedEffect) continue;
                if (def.SelectionMode == SelectionMode.None && !def.PerAtomTargets) continue;
                var domain = DomainOfMode(def, modeIndex);
                if (domain != null && domain.Count > 0) return def;
            }
            return null;
        }

        /// <summary>def 的组合域（ChoiceDomains 已随 2026-10-31 P12 抉择退役恒 null——恒 TargetDomain）。</summary>
        private static List<int> DomainOfMode(EffectDefinition def, int modeIndex)
        {
            return def.TargetDomain;
        }

        /// <summary>目标解析上下文（镜像引擎来源归因：法术效果来源=角色、场上卡效果来源=该卡）。
        /// sourceOverride：英雄技能=技能卡本体（与声明期收集 CollectEffectDeclarationTargetsAsync /
        /// 结算 PendingEffect.Source 同源——技能卡是法术卡，但效果来源不是角色）。</summary>
        private static EffectExecutionContext BuildTargetCtx(GameCore core, Player me, Card card,
            Entity sourceOverride = null)
        {
            return new EffectExecutionContext
            {
                Source = sourceOverride ?? (card.IsSpellCard() ? (Entity)me : card),
                Controller = me,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = 0,
            };
        }
    }
}
