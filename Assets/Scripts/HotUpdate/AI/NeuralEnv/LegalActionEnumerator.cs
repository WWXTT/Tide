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
    /// 指向性目标 = 同表逐目标展开（动作行=(卡,模式,目标)，与攻击动作同构，单步 argmax）。</summary>
    public enum TideActionType : int
    {
        PlayCard = 0,            // 手牌打出（含抉择模式 + 指向目标展开）
        PlayLand = 1,            // 手牌放入元素池（当地产元素）
        Activate = 3,            // 效果发动：战场卡的激活式（Activate_*）能力（含目标展开）
        Attack = 4,              // 攻击宣言（attacker → 对方随从 / 对方玩家）
        EndTurn = 5,             // 结束回合
        HeroSkill = 6,           // 己方回合主阶段发动英雄技能（2026-10-07 卡牌化：技能=结界卡，Card 承载）
        VoluntaryTrigger = 7,    // 发动自愿桶待发效果（触发式可选项；响应窗口内同类型复用）
        PassPriority = 8,        // 响应窗口让过/放弃（仅响应窗口出现，Main 阶段不出现）
        RespondPlay = 9,         // 响应出牌（带目标展开；速度门/费用/优先权全检）
        RespondActivate = 10,    // 响应发动（激活式；镜像 NetworkIntentApplier.ActivateWithTargets）
        Guard = 11,              // 守卫宣言（拦截栈上的攻击宣言对象）
        // 2 = 墓地出牌（已移除 2026-09-09）：RL 标准池不含可从墓地打出的来源卡，
        //     枚举出的动作引擎必拒（口径漂移白耗动作配额）。引擎侧 GameActions.PlayCardFromGraveyard
        //     保留（引擎能力/UI 走它），只是不再进动作空间。
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
        /// <summary>响应窗口动作（签名区分上下文：同一动作在 Main 与响应窗口不互相摘除）。</summary>
        public bool ResponseContext;

        public override string ToString()
        {
            string t = Target is Card tc ? tc.ID : (Target is Player ? "(player)" : "-");
            return $"{Type}({Card?.ID ?? "-"} → {t}, mode={ModeIndex}, n={Targets?.Count ?? 0})";
        }

        /// <summary>
        /// 动作签名（Type|来源卡|效果Id|目标集|模式|上下文）：跨枚举稳定（EffectDefinition 每次枚举都是新对象，
        /// 引用判等不可用；Id 是转换自卡表的稳定键）。供「本回合摘除无效动作」判重。
        /// </summary>
        public string Signature
        {
            get
            {
                string t = TargetsKey();
                string ctx = ResponseContext ? "R" : "M";
                return $"{(int)Type}|{Card?.ID ?? "-"}|{Effect?.Id ?? "-"}|{t}|{ModeIndex}|{ctx}";
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
    ///    映射（守卫/自愿桶/激活式）+ 手牌响应卡（RespondPlay，速度门全检）+ PassPriority 恒末位。
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
            EnumerateAttack(core, me, opp, zm);
            EnumerateHeroSkill(core, me);
            EnumerateVoluntaryTriggers(core, me);
            Actions.Add(EndTurnAction());

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

            BuildFeatures();
        }

        /// <summary>把动作映射回引擎调用。返回 false 表示引擎拒绝（枚举口径漂移时的保守兜底，driver 据此重观测）。</summary>
        public static bool Apply(GameCore core, Player me, TideAction a)
        {
            switch (a.Type)
            {
                case TideActionType.PlayCard:
                    return GameActions.PlayCard(core, me, a.Card, a.Targets, Zone.Hand, a.ModeIndex);
                case TideActionType.PlayLand:
                    return GameActions.AddToElementPool(core, me, a.Card);
                case TideActionType.Activate:
                case TideActionType.RespondActivate:
                    return ActivateWithTargets(core, me, a.Effect, a.Card, a.Targets);
                case TideActionType.Attack:
                    return GameActions.DeclareAttack(core, me, a.Card, a.Target);
                case TideActionType.EndTurn:
                    return GameActions.EndTurn(core, me);
                case TideActionType.HeroSkill:
                    // 不走栈（HeroSkillSystem.ActivateAsync 直执行）；async 在无头双方 IsAI 下同步收敛
                    //（SimpleAI.UseHeroSkill 同款 GetResult 先例）
                    return GameActions.ActivateHeroSkill(core, me).GetAwaiter().GetResult();
                case TideActionType.VoluntaryTrigger:
                    return a.Pending != null && core.StackEngine.PlayerActivateVoluntary(a.Pending);
                case TideActionType.PassPriority:
                    return GameActions.PassPriority(core, me);
                case TideActionType.RespondPlay:
                    return GameActions.PlayCardInResponse(core, me, a.Card, a.Targets, a.ModeIndex);
                case TideActionType.Guard:
                    return a.Card != null && a.AttackInstance != null
                        && core.StackEngine.PushGuardDeclaration(a.Card, a.AttackInstance, me);
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

            bool isTurn = p == engine.ActivePlayer;
            var hand = zm.GetCards(p, Zone.Hand) ?? new List<Card>();
            foreach (var c in hand)
            {
                if (c == null || !RuleHooks.CanPlay(core, p, c, Zone.Hand)) continue;
                if (GameActions.GetPayloadGateReject(core, p, c) != null) continue; // 代价门槛镜像（2026-10-04：全价过地牌上限/代价无目标）
                if (!engine.SpeedCounter.CanActivate(
                        SpeedCalculator.GetCardCastSpeed(c), isTurn, EffectActivationType.Voluntary)) continue;
                if (!IsSpell(c) && !zm.HasBattlefieldSpace(p)) continue;
                int modes = ModeCount(c);
                for (int m = 0; m < modes; m++)
                    if (GameActions.CanAfford(core, p, GameActions.GetCardCost(c, m))) return true;
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
        /// 带目标的启动式激活（镜像 NetworkIntentApplier.ActivateWithTargets）：targets 空 → 公共入口
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
            int cap = core.ElementPool.GetLandCap(me);
            if (core.ElementPool.GetPooledCards(me).Count >= cap) return;
            var hand = zm.GetCards(me, Zone.Hand);
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (!ElementPoolSystem.CanServeAsLand(c)) continue;
                Actions.Add(new TideAction
                {
                    Type = TideActionType.PlayLand,
                    Card = c,
                    SourceIndex = TideObservation.CardIndex(me, me, Zone.Hand, i),
                    TargetIndex = -1,
                });
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
                    if (!GameActions.CanAfford(core, me, GameActions.GetCardCost(c, m))) continue;
                    if (!IsSpell(c) && !zm.HasBattlefieldSpace(me)) continue;
                    if (!TargetDomainService.HasPlayableTargets(core, me, c, m)) continue; // 域空不可出（镜像 PlayCard 预检）

                    int src = TideObservation.CardIndex(me, me, Zone.Hand, i);
                    foreach (var targets in ExpandCardTargets(core, me, c, m))
                        Actions.Add(BuildTargetedRow(TideActionType.PlayCard, c, m, src, targets));
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
                foreach (var def in GetEffectDefinitions(src))
                {
                    if (!def.IsActivatedEffect) continue;
                    if (!executor.CanActivate(def, src, me, me, phase, turn)) continue;

                    int slot = TideObservation.CardIndex(me, me, Zone.Battlefield, i);
                    AddActivatedRows(core, me, src, def, TideActionType.Activate, slot);
                }
            }
        }

        /// <summary>激活式动作行（带逐目标展开）：Main=Activate / 响应窗口=RespondActivate 共用。
        /// 展开口径：def 自身 SelectionMode/组合域（激活式无抉择模式，modeIndex 恒 0）。</summary>
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
        }

        /// <summary>英雄技能行（2026-10-07 卡牌化：技能=构筑标记的结界卡）：技能卡在场/未横置/
        /// 未沉默 + 唯一主动效果 CanActivate 预检（时点/费用可付/目标域——ActivateAsync 守卫镜像）。
        /// ModeIndex=0（卡牌化后无技能枚举可编码）；技能卡在 FieldZone（未编码区）→ SourceIndex=-1。
        /// 单行（目标选择在结算期解析）。</summary>
        private void EnumerateHeroSkill(GameCore core, Player me)
        {
            var skillCard = HeroSkillSystem.ResolveSkillCard(core, me);
            if (skillCard == null) return;                        // 不在场（未标记/被摧毁/弹回）→ 无技能
            if (!core.ZoneManager.GetCards(me, Zone.FieldZone).Contains(skillCard)) return;
            if (skillCard.IsTapped()) return;                     // 一回合一次闸门（回合开始重置）
            if (skillCard.GetCounterCount(CounterRules.SilenceCounter) > 0) return; // 沉默不可发动主动效果
            var effect = HeroSkillSystem.SkillEffectOf(skillCard);
            if (effect == null) return;

            var executor = core.StackEngine.GetExecutor();
            if (executor == null || !executor.CanActivate(effect, skillCard, me, me,
                    core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                    core.TurnEngine.TurnNumber)) return;

            Actions.Add(new TideAction
            {
                Type = TideActionType.HeroSkill,
                Card = skillCard,
                ModeIndex = 0,
                SourceIndex = -1, // FieldZone 未编码区
                TargetIndex = -1,
            });
        }

        /// <summary>自愿桶待发效果行（v2）：栈机器待发队列中可发动的自愿效果（触发式可选项——
        /// 「响应期自愿触发」决策点）。激活式不在此（走 EnumerateActivate 的卡面扫描口径）。
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
        /// 预检镜像 GameActions.PlayCardInResponse/PushCardCast：速度门（回合方 ≥ / 非回合方严格 &gt;
        /// 记速器）+ RuleHooks + 战场容量 + 费用（含 pending 承诺）+ 目标域；目标逐张展开。</summary>
        private void EnumerateRespondPlay(GameCore core, Player me)
        {
            var engine = core.StackEngine;
            bool isTurn = me == engine.ActivePlayer;
            var hand = core.ZoneManager.GetCards(me, Zone.Hand);
            for (int i = 0; i < hand.Count; i++)
            {
                var c = hand[i];
                if (!RuleHooks.CanPlay(core, me, c, Zone.Hand)) continue;
                if (GameActions.GetPayloadGateReject(core, me, c) != null) continue; // 代价门槛镜像（2026-10-04：全价过地牌上限/代价无目标）
                if (!engine.SpeedCounter.CanActivate(
                        SpeedCalculator.GetCardCastSpeed(c), isTurn, EffectActivationType.Voluntary)) continue;
                if (!IsSpell(c) && !core.ZoneManager.HasBattlefieldSpace(me)) continue;

                int modes = ModeCount(c);
                for (int m = 0; m < modes; m++)
                {
                    if (!GameActions.CanAfford(core, me, GameActions.GetCardCost(c, m))) continue;
                    if (!TargetDomainService.HasPlayableTargets(core, me, c, m)) continue;

                    int src = TideObservation.CardIndex(me, me, Zone.Hand, i);
                    foreach (var targets in ExpandCardTargets(core, me, c, m))
                        Actions.Add(BuildTargetedRow(TideActionType.RespondPlay, c, m, src, targets, response: true));
                }
            }
        }

        /// <summary>带目标组合的动作行构造（PlayCard/RespondPlay/Activate/RespondActivate）：
        /// 填 Target 兼容别名、槽位与目标类别特征。</summary>
        private TideAction BuildTargetedRow(TideActionType type, Card card, int modeIndex, int sourceSlot,
            List<Entity> targets, bool response = false, EffectDefinition effect = null)
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
                Features[b + 1] = (int)a.Type;                               // type 0..11
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
                    return GameActions.GetCardCost(a.Card, a.ModeIndex).Total;
                case TideActionType.Activate:
                case TideActionType.RespondActivate:
                case TideActionType.VoluntaryTrigger:
                    return CostDerivationService.DeriveElementCosts(a.Effect, 0).Total; // 效果费现推（启动式/自愿桶发动时现付——L1 现推口径）
                case TideActionType.HeroSkill:
                    // 卡牌化（2026-10-07）：技能=结界卡唯一主动效果——效果费现推（与 Activate 同口径）
                    return a.Card != null && HeroSkillSystem.SkillEffectOf(a.Card) is { } fx
                        ? CostDerivationService.DeriveElementCosts(fx, 0).Total
                        : 0f;
                default:
                    return 0f;
            }
        }

        // ===================================================== 辅助谓词（SimpleAI 同源口径） =====================================================

        private static int ModeCount(Card c)
            => c is CardWrapper w ? Math.Max(1, CostDerivationService.GetModeCount(w.GetData())) : 1;

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

        /// <summary>第一个带组合域的非激活式效果定义（施放即结算才会随出牌自动跑）；
        /// None 模式（区域自结算类）跳过——不为其选目标。</summary>
        private static EffectDefinition FirstDomainEffect(Card card, int modeIndex)
        {
            foreach (var def in GetEffectDefinitions(card))
            {
                if (def.IsActivatedEffect) continue;
                if (def.SelectionMode == SelectionMode.None) continue;
                var domain = DomainOfMode(def, modeIndex);
                if (domain != null && domain.Count > 0) return def;
            }
            return null;
        }

        /// <summary>def 在指定模式的组合域（抉择 per-mode 优先，回落主序列域）。</summary>
        private static List<int> DomainOfMode(EffectDefinition def, int modeIndex)
        {
            return def.ChoiceDomains != null && modeIndex >= 0 && modeIndex < def.ChoiceDomains.Length
                ? def.ChoiceDomains[modeIndex]
                : def.TargetDomain;
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
