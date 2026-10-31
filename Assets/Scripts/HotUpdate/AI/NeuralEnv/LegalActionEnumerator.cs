using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>动作类型（离散下标 → 桥接映射回 GameActions.*）。
    /// v2（2026-09-30 动作空间二期契约）：新增 HeroSkill/VoluntaryTrigger/PassPriority/
    /// RespondPlay/RespondActivate/Guard（6..11）——模型获得效果发动时机（自愿桶/英雄技能）
    /// 与对方回合响应窗口（守卫/响应出牌/响应发动/让过）的决策点；
    /// 指向性目标 = 同表逐目标展开（动作行=(卡,模式,目标)，与攻击动作同构，单步 argmax）。
    /// v3（2026-10-14 契约）：补漏的两个主阶段决策点——TapCreature(12) 横置地牌特性生物产元素、
    /// CommitAwaken(13) 苏醒立约（对账报告 D10/D11）。
    /// v4（2026-10-29 P6 苏醒改版）：CommitAwaken(13) 退役（立约决策删除）——苏醒并入
    /// PlayCard 的 awaken 变体行（动作行=(卡,模式,目标,awaken)，免灰净费份额转入场沉睡层数；P6c 只免灰）。</summary>
    public enum TideActionType : int
    {
        PlayCard = 0,            // 手牌打出（含抉择模式 + 指向目标展开 + 苏醒施放变体 AwakenCast）
        PlayLand = 1,            // 手牌放入元素池（当地产元素）
        Activate = 3,            // 效果发动：战场卡的主动（Activate_*）能力（含目标展开）
        Attack = 4,              // 攻击宣言（attacker → 对方随从 / 对方玩家）
        EndTurn = 5,             // 结束回合
        HeroSkill = 6,           // 己方回合主阶段发动英雄技能（2026-10-07 卡牌化：技能=结界卡，Card 承载）
        VoluntaryTrigger = 7,    // 发动自愿桶待发效果（触发式可选项；响应窗口内同类型复用）
        PassPriority = 8,        // 响应窗口让过/放弃（仅响应窗口出现，Main 阶段不出现）
        RespondPlay = 9,         // 响应出牌（带目标展开；速度门/费用/优先权全检）
        RespondActivate = 10,    // 响应发动（主动；镜像 NetworkIntentApplier.ActivateWithTargets）
        Guard = 11,              // 守卫宣言（拦截栈上的攻击宣言对象）
        TapCreature = 12,        // 横置带地牌特性(LandTrait)的生物产 1 元素进临时钱包（主阶段；产色自动=费用首色）
        Charge = 14,             // 充能（2026-10-31 P12：手牌带充能效果的卡逐色充能——速度 0 主动效果入栈；
                                  // Color 字段承载所选色；CanCharge 单源闸）。13 见下（勿复用）。
        // 2 = 墓地出牌（已移除 2026-09-09）：RL 标准池不含可从墓地打出的来源卡，
        //     枚举出的动作引擎必拒（口径漂移白耗动作配额）。引擎侧墓地出牌口已随
        //     2026-10-30 归土改版整删（归土=回合末自动复活，非出牌来源）。
        // 13 = CommitAwaken 苏醒立约（已移除 2026-10-29 P6 苏醒改版）：立约退役，苏醒并入
        //     PlayCard 的 awaken 变体（AwakenCast 布尔列）。序号不回收、勿复用（旧契约/模型留档）。
    }

    /// <summary>一个可执行动作：模型输出离散下标 → 桥接按此结构调用 GameActions.*。</summary>
    public struct TideAction
    {
        public TideActionType Type;
        public Card Card;                 // 来源卡（PlayCard/PlayLand/Activate/Guard 等；EndTurn/PassPriority 为 null）
        public Entity Target;             // 攻击目标（=Targets[0] 的兼容别名）；其余为 null
        public List<Entity> Targets;      // 展开的目标组合（null = 无目标/随机/任意数量 → 引擎自动解析）
        public EffectDefinition Effect;   // Activate/RespondActivate/VoluntaryTrigger 的效果定义
        public int ModeIndex;             // 抉择模式下标（非抉择恒 0；HeroSkill 行恒 0——技能卡在 Card 上）
        public PendingEffect Pending;     // VoluntaryTrigger/RespondActivate 的待发对象（引擎队列实例）
        public EffectInstance AttackInstance; // Guard 行：被拦截的攻击宣言栈对象

        /// <summary>来源卡在 cards_ 中的槽位（-1 = 无 / 未编码区如 FieldZone 技能卡）。</summary>
        public int SourceIndex;
        /// <summary>目标在 cards_ 中的槽位（-1 = 无 / 玩家目标——由 TargetKindCode 区分）。</summary>
        public int TargetIndex;
        /// <summary>目标类别特征（动作特征 [7]）：0=无/区域自结算，1=卡目标，2=对方玩家，3=己方玩家。</summary>
        public int TargetKindCode;
        /// <summary>攻击者=自己角色（2026-10-10 角色参战遗留收口）：Card 恒 null、SourceIndex 恒 -1，
        /// Apply 分流 DeclareAttack(core, me, me, target)——弹药结算后引擎烧光。</summary>
        public bool HeroAttacker;
        /// <summary>响应窗口动作（签名区分上下文：同一动作在 Main 与响应窗口不互相摘除）。</summary>
        public bool ResponseContext;
        /// <summary>苏醒施放变体（2026-10-29 P6；P6c 只免灰）：PlayCard 专属——免净费灰份额转入场沉睡层数、
        /// 横置登场（与普通施放同行不同签名；其余动作类型恒 false）。</summary>
        public bool AwakenCast;
        /// <summary>充能色（2026-10-31 P12）：Charge 行专属——灰/红/蓝/绿四选一
        ///（其余动作类型无意义）；签名带色防同卡异色行判重坍缩。</summary>
        public ManaType Color;

        public override string ToString()
        {
            string t = Target is Card tc ? tc.ID : (Target is Player ? "(player)" : "-");
            return $"{Type}({Card?.ID ?? "-"} → {t}, mode={ModeIndex}, n={Targets?.Count ?? 0})";
        }

        /// <summary>
        /// 动作签名（Type|来源卡|效果Id|目标集|模式|上下文|苏醒）：跨枚举稳定（EffectDefinition 每次枚举都是新对象，
        /// 引用判等不可用；Id 是转换自卡表的稳定键）。供「本回合摘除无效动作」判重。
        /// 苏醒位列（2026-10-29 P6）：同卡同模式同目标的普通/苏醒施放是两行不同签名。
        /// </summary>
        public string Signature
        {
            get
            {
                string t = TargetsKey();
                string ctx = ResponseContext ? "R" : "M";
                string awaken = AwakenCast ? "A" : "-";
                string color = Type == TideActionType.Charge ? ((int)Color).ToString() : "-";
                return $"{(int)Type}|{Card?.ID ?? "-"}|{Effect?.Id ?? "-"}|{t}|{ModeIndex}|{ctx}|{awaken}|{color}";
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
    ///
    /// 文档化简化（v1 沿袭）：横置地牌由 driver 回合初自动完成（LandTapPolicy），不建模。
    /// </summary>
    public sealed class LegalActionEnumerator
    {
        /// <summary>多目标（Multiple/MultipleUnion）组合枚举上限（契约第 4 节：每效果 24 组截断）。</summary>
        private const int MaxTargetCombos = 24;

        /// <summary>动作表硬上限（v3 契约 §6：与 Python MAX_ACTIONS / OnnxTidePolicy.MaxActions 三处同步）。
        /// 超限时保留前 MaxActions-1 行 + 恒保末位兜底行（Main=EndTurn / 响应=PassPriority）——
        /// Python pad_or_truncate_actions 是头截断，无此守卫会把兜底行切掉（模型无法结束回合，
        /// v2 空表冻结死循环的同族风险）。</summary>
        public const int MaxActions = 512;

        public List<TideAction> Actions = new List<TideAction>();
        /// <summary>动作特征张量（Actions.Count × TideObservation.NAction），供模型输入。
        /// 8 维布局（契约第 2 节）：[0]valid [1]type [2]sourceIndex [3]targetIndex
        /// [4]modeIndex [5]actionCost [6]effectIdentity [7]targetKind。</summary>
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
            EnumerateCharge(core, me, zm);
            EnumerateAttack(core, me, opp, zm);
            EnumerateHeroSkill(core, me);
            EnumerateVoluntaryTriggers(core, me);
            EnumerateTapCreatures(core, me, zm);
            Actions.Add(EndTurnAction());

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
                        if (opt.SourceCard != null && opt.AttackInstance != null)
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
                    return GameActions.PlayCard(core, me, a.Card, a.Targets, Zone.Hand, a.ModeIndex, a.AwakenCast);
                case TideActionType.PlayLand:
                    // ModeIndex 复用为锁色选择（2026-10-11：-1=缺省主色，否则=ManaType 枚举值）
                    return GameActions.AddToElementPool(core, me, a.Card,
                        a.ModeIndex >= 0 ? (ManaType)a.ModeIndex : (ManaType?)null);
                case TideActionType.Activate:
                case TideActionType.RespondActivate:
                    return ActivateWithTargets(core, me, a.Effect, a.Card, a.Targets);
                case TideActionType.Attack:
                    return a.HeroAttacker
                        ? GameActions.DeclareAttack(core, me, me, a.Target) // 角色=攻击者（弹药即攻击力）
                        : GameActions.DeclareAttack(core, me, a.Card, a.Target);
                case TideActionType.EndTurn:
                    return GameActions.EndTurn(core, me);
                case TideActionType.HeroSkill:
                    // 不走栈（HeroSkillSystem.ActivateAsync 直执行）；async 在无头双方 IsAI 下同步收敛
                    //（SimpleAI.UseHeroSkill 同款 GetResult 先例）；ModeIndex=效果抉择序号（多效果技能）
                    return GameActions.ActivateHeroSkill(core, me, a.ModeIndex).GetAwaiter().GetResult();
                case TideActionType.VoluntaryTrigger:
                    return a.Pending != null && core.StackEngine.PlayerActivateVoluntary(a.Pending);
                case TideActionType.PassPriority:
                    return GameActions.PassPriority(core, me);
                case TideActionType.RespondPlay:
                    return GameActions.PlayCardInResponse(core, me, a.Card, a.Targets, a.ModeIndex);
                case TideActionType.Guard:
                    return a.Card != null && a.AttackInstance != null
                        && core.StackEngine.PushGuardDeclaration(a.Card, a.AttackInstance, me);
                case TideActionType.TapCreature:
                    return GameActions.TapCreatureForElement(core, me, a.Card);
                case TideActionType.Charge:
                    // 充能（2026-10-31 P12）：手牌带充能效果的卡逐色充能——CanCharge 单源终审
                    return GameActions.ActivateCharge(core, me, a.Card, a.Color, out _);
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
        /// ActivateEffect；有预选目标 → PendingEffect.Create 携带 SelectedTargets 走 PlayerActivateVoluntary
        /// （速度门/记速器在栈机器内；横置消耗同口径）。
        /// </summary>
        private static bool ActivateWithTargets(GameCore core, Player player, EffectDefinition effect,
            Card source, List<Entity> targets)
        {
            if (effect == null) return false;
            if (targets == null || targets.Count == 0)
                return GameActions.ActivateEffect(core, player, effect, source);

            var pending = PendingEffect.Create(
                effect, source, player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby);
            pending.SelectedTargets = targets;
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
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (!ElementPoolSystem.CanServeAsLand(c)) continue;
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
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (!RuleHooks.CanPlay(core, me, c, Zone.Hand)) continue;
                if (GameActions.GetPayloadGateReject(core, me, c) != null) continue; // 代价门槛镜像（2026-10-04：全价过地牌上限/代价无目标）

                int modes = ModeCount(c);
                for (int m = 0; m < modes; m++)
                {
                    if (!IsSpell(c) && !c.IsFieldCard() && !zm.HasBattlefieldSpace(me)) continue; // 场地卡落场地格不计容量（P8）
                    if (!TargetDomainService.HasPlayableTargets(core, me, c, m)) continue; // 域空不可出（镜像 PlayCard 预检）

                    int src = TideObservation.CardIndex(me, me, Zone.Hand, i);
                    // 普通行：全净费可付（苏醒可付但全价付不起时只出苏醒变体——穷时先醒的通路）
                    if (GameActions.CanAffordCast(core, me, c, m))
                        foreach (var targets in ExpandCardTargets(core, me, c, m))
                            Actions.Add(BuildTargetedRow(TideActionType.PlayCard, c, m, src, targets));

                    // 苏醒施放变体（2026-10-29 P6；P6c 只免灰；2026-10-31 P12 关键词化：
                    // GetAwakenSleepLayers 内含 Awaken 关键词闸——**无关键词生物不出苏醒行**，此处不另判）
                    //：生物净费灰份额 ≥1 → 同目标展开各加一行
                    //（免灰转入场沉睡层数、横置登场、彩色含绿照付；灰=0 不提供——无免可免不给白睡；
                    // 2026-10-31 地牌门槛镜像 PlayCard 终审——灰份额>当前地牌上限不出苏醒行）
                    if (GameActions.GetAwakenSleepLayers(c, m) > 0
                        && !GameActions.AwakenExceedsLandCap(core, me, c, m)
                        && GameActions.CanAffordCast(core, me, c, m, awakenCast: true))
                        foreach (var targets in ExpandCardTargets(core, me, c, m))
                            Actions.Add(BuildTargetedRow(TideActionType.PlayCard, c, m, src, targets, awaken: true));
                }
            }
        }

        private void EnumerateActivate(GameCore core, Player me, ZoneManager zm)
        {
            var executor = core.StackEngine.GetExecutor();
            var phase = core.TurnEngine.CurrentPhase.Phase;
            int turn = core.TurnEngine.TurnNumber;

            var bf = zm.GetCards(me, Zone.Battlefield);
            for (int i = 0; i < bf.Count; i++)
            {
                var src = bf[i];
                // 结界禁主动效果（2026-10-15 改版）——镜像引擎 ActivateEffect 来源闸，直接跳过
                if (Attribute.CounterRules.UsesDurabilityEconomy(src)) continue;
                foreach (var def in GetEffectDefinitions(src))
                {
                    if (!def.IsActivatedEffect) continue;
                    // 移动原子（2026-10-31 场面轴3c）：本阶段不枚举——枚举器的动作行无选格参数，
                    // AI 发动=白付代价空转（handler Warn 后不动盘面）
                    if (ContainsMoveUnitAtom(def)) continue;
                    // 充能（2026-10-31 P12）：充能效果不走战场发动路径（引擎 CanActivate 限手牌源）
                    //——枚举口=EnumerateCharge，此处跳过防死行
                    if (ComposerCatalog.IsChargeEffect(def)) continue;
                    if (!executor.CanActivate(def, src, me, me, phase, turn)) continue;

                    int slot = TideObservation.CardIndex(me, me, Zone.Battlefield, i);
                    AddActivatedRows(core, me, src, def, TideActionType.Activate, slot);
                }
            }
        }

        /// <summary>充能枚举（2026-10-31 P12）：己方手牌 × 带充能效果 × 各可充色——每色一行
        ///（CanCharge 单源闸：回合方/主阶段/手牌/色资格/上限/元素余量全检；Charge 行 Color 承载所选色）。
        /// 充能=速度 0 主动效果入栈（ActivateCharge），可发动性含速度门（栈非空时引擎拒——枚举即省行）。</summary>
        private void EnumerateCharge(GameCore core, Player me, ZoneManager zm)
        {
            var hand = zm.GetCards(me, Zone.Hand);
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (c == null) continue;
                var data = c is CardWrapper wrapper ? wrapper.GetData() : null;
                if (data == null || !ComposerCatalog.HasChargeEffect(data)) continue;
                int src = TideObservation.CardIndex(me, me, Zone.Hand, i);
                foreach (ManaType color in new[] { ManaType.Gray, ManaType.Red, ManaType.Blue, ManaType.Green })
                {
                    if (!GameActions.CanCharge(core, me, c, color, out _)) continue;
                    Actions.Add(new TideAction
                    {
                        Type = TideActionType.Charge,
                        Card = c,
                        Color = color,
                        SourceIndex = src,
                        TargetIndex = -1,
                        TargetKindCode = 0,
                    });
                }
            }
        }

        /// <summary>效果含移动原子（MoveUnit——2026-10-31 场面轴3c）：AI 枚举本阶段跳过
        ///（动作行无选格参数，发动=白付代价空转）；接线选格参数后移除本闸。</summary>
        private static bool ContainsMoveUnitAtom(EffectDefinition def)
        {
            if (def == null || def.Effects == null) return false;
            for (int i = 0; i < def.Effects.Count; i++)
                if (def.Effects[i] != null && def.Effects[i].Type == AtomicEffectType.MoveUnit) return true;
            return false;
        }

        /// <summary>主动动作行（带逐目标展开）：Main=Activate / 响应窗口=RespondActivate 共用。
        /// 展开口径：def 自身 SelectionMode/组合域（主动无抉择模式，modeIndex 恒 0）。</summary>
        private void AddActivatedRows(GameCore core, Player me, Card src, EffectDefinition def,
            TideActionType type, int sourceSlot = -2)
        {
            if (sourceSlot == -2) sourceSlot = TideObservation.IndexOfCard(core, me, src);
            var expansions = new List<List<Entity>>();
            ExpandDefinitionTargets(core, me, src, def, 0, expansions);
            if (expansions.Count == 0) expansions.Add(null);
            bool response = type == TideActionType.RespondActivate;
            foreach (var targets in expansions)
                Actions.Add(BuildTargetedRow(type, src, 0, sourceSlot, targets, response, def));
        }

        private void EnumerateAttack(GameCore core, Player me, Player opp, ZoneManager zm)
        {
            // 2026-09-16 战斗接入栈机器：攻击=速度0栈对象逐攻击开窗——动作语义不变
            //（Apply→GameActions.DeclareAttack 上栈，随后响应窗口/DrainStack 结算）。
            var bf = zm.GetCards(me, Zone.Battlefield);
            var oppBf = zm.GetCards(opp, Zone.Battlefield);
            for (int i = 0; i < bf.Count; i++)
            {
                var atk = bf[i];
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
            }
        }

        /// <summary>英雄技能行（2026-10-15 改版：技能=技能区法术、每回合一次=横置闸）：技能卡在场/
        /// 未横置/未沉默 + 逐效果 CanActivate 预检（ActivateAsync 守卫镜像）。
        /// 挂 1~3 效果——**逐效果一行**（ModeIndex=效果序号，发动时抉择）；技能卡在场地区——
        /// v3 起场地区槽 [45] 已编码，SourceIndex 可解析。目标选择在结算期解析（单行）。</summary>
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
                if (!executor.CanActivate(effect, skillCard, me, me, phase, turn)) continue;
                Actions.Add(new TideAction
                {
                    Type = TideActionType.HeroSkill,
                    Card = skillCard,
                    Effect = effect,       // 特征[6] 效果身份哈希按所选效果编码
                    ModeIndex = e,         // 效果抉择序号（单效果卡恒 0）
                    SourceIndex = TideObservation.IndexOfCard(core, me, skillCard), // 场地区槽（v3 已编码）
                    TargetIndex = -1,
                });
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
                Actions.Add(BuildVoluntaryAction(core, me, pe));
            }
        }

        /// <summary>横置产元素行（v3 契约 §1 TapCreature=12）：带地牌特性(LandTrait)的未横置生物，
        /// 横置产 1 元素进临时钱包（产色自动=费用首色，无分支——只决策"哪只/何时"）。
        /// 资格镜像 GameActions.TapCreatureForElement 守卫（教学闸在 RL 为 null 不检）。</summary>
        private void EnumerateTapCreatures(GameCore core, Player me, ZoneManager zm)
        {
            var bf = zm.GetCards(me, Zone.Battlefield);
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

        // EnumerateCommitAwakens 已删（2026-10-29 P6 苏醒改版）：苏醒立约动作退役——
        // 苏醒决策并入 EnumeratePlayCard 的 awaken 变体行（TideAction.AwakenCast）。

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
        /// 预检镜像 GameActions.PlayCardInResponse/PushCardCast：速度门（2026-10-18 全严格大于，
        /// 响应窗口恒非空栈——空栈首动豁免不触发）+ RuleHooks + 战场容量 + 费用（含 pending 承诺）
        /// + 目标域；目标逐张展开。</summary>
        private void EnumerateRespondPlay(GameCore core, Player me)
        {
            var engine = core.StackEngine;
            var hand = core.ZoneManager.GetCards(me, Zone.Hand);
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (!RuleHooks.CanPlay(core, me, c, Zone.Hand)) continue;
                if (GameActions.GetPayloadGateReject(core, me, c) != null) continue; // 代价门槛镜像（2026-10-04：全价过地牌上限/代价无目标）
                int castSpeed = SpeedCalculator.GetCardCastSpeed(c);
                if (!engine.SpeedCounter.CanActivate(castSpeed, EffectActivationType.Voluntary, engine.IsEmpty)) continue;
                if (!IsSpell(c) && !c.IsFieldCard() && !core.ZoneManager.HasBattlefieldSpace(me)) continue; // 场地卡落场地格不计容量（P8）

                int modes = ModeCount(c);
                for (int m = 0; m < modes; m++)
                {
                    if (!GameActions.CanAffordCast(core, me, c, m)) continue;
                    if (!TargetDomainService.HasPlayableTargets(core, me, c, m)) continue;

                    int src = TideObservation.CardIndex(me, me, Zone.Hand, i);
                    foreach (var targets in ExpandCardTargets(core, me, c, m))
                        Actions.Add(BuildTargetedRow(TideActionType.RespondPlay, c, m, src, targets, response: true));
                }
            }
        }

        /// <summary>带目标组合的动作行构造（PlayCard/RespondPlay/Activate/RespondActivate）：
        /// 填 Target 兼容别名、槽位与目标类别特征。awaken（2026-10-29 P6）：PlayCard 苏醒施放变体行。</summary>
        private TideAction BuildTargetedRow(TideActionType type, Card card, int modeIndex, int sourceSlot,
            List<Entity> targets, bool response = false, EffectDefinition effect = null, bool awaken = false)
        {
            var a = new TideAction
            {
                Type = type,
                Card = card,
                ModeIndex = modeIndex,
                Targets = targets,
                Effect = effect,
                SourceIndex = sourceSlot,
                ResponseContext = response,
                AwakenCast = awaken,
            };
            if (targets != null && targets.Count > 0)
            {
                a.Target = targets[0];
                a.TargetIndex = targets[0] is Card tc ? TideObservation.IndexOfCard(_core, _me, tc) : -1;
                a.TargetKindCode = targets[0] is Card ? 1 : (ReferenceEquals(targets[0], _me) ? 3 : 2);
            }
            else
            {
                a.TargetIndex = -1;
                a.TargetKindCode = 0;
            }
            return a;
        }

        // ===================================================== 目标展开（镜像 SimpleAI.ChooseTargets 口径） =====================================================

        /// <summary>出牌目标展开（PlayCard/RespondPlay 共用）：FirstDomainEffect 找带组合域的施放即结算效果，
        /// 按其 SelectionMode 展开；无目标效果返回 [null]（单行，引擎自动解析）。</summary>
        private static List<List<Entity>> ExpandCardTargets(GameCore core, Player me, Card card, int modeIndex)
        {
            var result = new List<List<Entity>>();
            var def = FirstDomainEffect(card, modeIndex);
            if (def == null)
            {
                result.Add(null); // 无需选目标 → 引擎自动
                return result;
            }
            ExpandDefinitionTargets(core, me, card, def, modeIndex, result);
            if (result.Count == 0) result.Add(null);
            return result;
        }

        /// <summary>按效果定义的目标模式展开：
        /// None/无域/随机/任意数量 → 单行 null（引擎自动——随机档不弹选择自动抽取）；
        /// Whole/WholeUnion → 单行全取候选；
        /// Single/SingleUnion → 逐候选一行；
        /// Multiple/MultipleUnion（TargetCount&gt;0）→ 引擎候选序组合枚举，上限 MaxTargetCombos 截断。
        /// 候选= ResolveCandidates（与引擎 ResolveCompositionTargetsAsync / SimpleAI.ChooseTargets 同源）。</summary>
        private static void ExpandDefinitionTargets(GameCore core, Player me, Card card, EffectDefinition def,
            int modeIndex, List<List<Entity>> sink)
        {
            var domain = DomainOfMode(def, modeIndex);
            if (domain == null || domain.Count == 0 || def.SelectionMode == SelectionMode.None)
            {
                sink.Add(null);
                return;
            }

            var ctx = BuildTargetCtx(core, me, card);
            var candidates = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(domain, def.TargetFilter, ctx);
            if (candidates == null || candidates.Count == 0)
            {
                sink.Add(null);
                return;
            }

            switch (def.SelectionMode)
            {
                case SelectionMode.Whole:
                case SelectionMode.WholeUnion:
                    sink.Add(new List<Entity>(candidates)); // 全取档单行（不弹选择）
                    return;
            }
            if (def.RandomTarget || def.DynamicTargetCount)
            {
                sink.Add(null); // 随机自动抽取 / 运行时自选数量：单行交引擎（AI 应答代选）
                return;
            }

            if (SelectionModeRules.IsPickOne(def.SelectionMode))
            {
                foreach (var t in candidates)
                    sink.Add(new List<Entity> { t });
                return;
            }

            // 选多（Multiple/MultipleUnion）：固定 TargetCount 组合
            int need = def.TargetCount;
            if (need <= 0 || need >= candidates.Count)
            {
                sink.Add(new List<Entity>(candidates)); // 未声明/超出候选 → 全取兜底
                return;
            }
            EmitCombinations(candidates, need, MaxTargetCombos, sink);
        }

        /// <summary>候选序组合枚举（字典序前 cap 组）：候选序=引擎 ResolveCandidates 返回序（契约口径）。</summary>
        private static void EmitCombinations(List<Entity> cands, int k, int cap, List<List<Entity>> sink)
        {
            int n = cands.Count;
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
                Features[b + 1] = (int)a.Type;                               // type 0..12（13 已退役）
                Features[b + 2] = a.SourceIndex;                             // cards_ 槽下标（-1=无）
                Features[b + 3] = a.TargetIndex;                             // cards_ 槽下标（-1=无/玩家目标）
                Features[b + 4] = a.ModeIndex;                               // 抉择模式 / HeroSkill=skillId
                Features[b + 5] = ActionCost(a);                             // 费用总和（含 Activate/Voluntary/HeroSkill）
                Features[b + 6] = a.Effect != null                           // 效果定义哈希注册下标 / 1024
                    ? TideCardIndex.IndexOfEffectDefinition(a.Effect) / 1024f
                    : 0f;
                Features[b + 7] = a.TargetKindCode;                          // 0 无/1 卡/2 对方玩家/3 己方玩家
            }
        }

        private static float ActionCost(TideAction a)
        {
            switch (a.Type)
            {
                case TideActionType.PlayCard:
                case TideActionType.RespondPlay:
                    return GameActions.GetCardCost(a.Card, a.ModeIndex, a.AwakenCast).Total; // 苏醒变体=豁免后账单
                case TideActionType.Activate:
                case TideActionType.RespondActivate:
                case TideActionType.VoluntaryTrigger:
                    return CostDerivationService.DeriveElementCosts(a.Effect, 0).Total; // 效果费现推（主动/自愿桶发动时现付——L1 现推口径）
                case TideActionType.HeroSkill:
                    // 技能发动费（2026-10-18 深夜修订口径）：免锚价 + 抉择技能每次 1 灰——
                    // ActivationBill 单源（效果锚价已不是技能实付口径，勿再按锚价现推）
                    return HeroSkillSystem.ActivationBill(a.Card).Total;
                case TideActionType.Charge:
                    return 1f; // 充能（P12）：现付 1 点所选色元素（ChargeHandler 结算期扣）
                default:
                    return 0f;
            }
        }

        // ===================================================== 辅助谓词（SimpleAI 同源口径） =====================================================

        private static int ModeCount(Card c)
        {
            if (!(c is CardWrapper w)) return 1;
            // P12（2026-10-31）：法术模式数=非充能主动效果数（CostDerivationService 单源——
            // 充能走特殊使用入口不算法术模式；旧 data.Effects.Count 会把充能算进模式）
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
        /// 目标展开按所选那条（其余效果本局不发动）。</summary>
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
                if (spellDef == null || spellDef.SelectionMode == SelectionMode.None) return null;
                return spellDef.TargetDomain != null && spellDef.TargetDomain.Count > 0 ? spellDef : null;
            }
            foreach (var def in defs)
            {
                if (def.IsActivatedEffect) continue;
                if (def.SelectionMode == SelectionMode.None) continue;
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

        /// <summary>目标解析上下文（镜像引擎来源归因：法术效果来源=角色、场上卡效果来源=该卡）。</summary>
        private static EffectExecutionContext BuildTargetCtx(GameCore core, Player me, Card card)
        {
            return new EffectExecutionContext
            {
                Source = card.IsSpellCard() ? (Entity)me : card,
                Controller = me,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = 0,
            };
        }
    }
}
