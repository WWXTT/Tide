using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;

namespace CardCore
{
    /// <summary>
    /// 效果执行器
    /// 负责解析和执行效果
    /// </summary>
    public partial class EffectExecutor
    {
        private ZoneManager _zoneManager;
        private ElementPoolSystem _elementPool;
        private ConditionChecker _conditionChecker;
        private EffectUsageTracker _usageTracker;

        public EffectExecutor(
            ZoneManager zoneManager,
            ElementPoolSystem elementPool)
        {
            _zoneManager = zoneManager;
            _elementPool = elementPool;
            _conditionChecker = new ConditionChecker(zoneManager);
            _usageTracker = new EffectUsageTracker();
        }

        /// <summary>自馈闸门（2026-10-30 用户定案：循环效果上限 30 次）：同一来源实例 + effect.Id
        /// 每回合最多入栈 N 次（表 GameRules.MaxTriggersPerTurnPerEffect，默认 30）——常规触发远低于此，
        /// 只截断自循环（抽牌时抽牌/受伤时造成伤害等）。引擎载荷不叠闸（2026-11-13 起引擎奖励也无每回合上限）；
        /// 结界耐久经济不叠闸（耐久即次数池，耗尽自毁）。记账走既有效果用量台账（回合开始清零）。</summary>
        public bool TriggerLoopGuardReached(EffectDefinition effect, Entity source = null)
        {
            if (effect == null) return false;
            if (source is Card host && Attribute.CounterRules.UsesDurabilityEconomy(host)) return false;
            if (CostDerivationService.HasEnginePayload(effect)) return false;
            return _usageTracker.GetTurnUsage(UsageKey(effect, source)) >= TriggerLoopGuardLimit;
        }

        /// <summary>自馈闸门上限（表 GameRules.MaxTriggersPerTurnPerEffect；下限 1）。</summary>
        public static int TriggerLoopGuardLimit
        {
            get
            {
                float n = ValueSystemConfigManager.Instance.GetOrCreateConfig()
                    .GameRulesConfig.MaxTriggersPerTurnPerEffect;
                return Math.Max(1, (int)Math.Round(n, MidpointRounding.AwayFromZero));
            }
        }

        /// <summary>触发式入栈即记账（2026-09-13 修复：结算期记账在同批多触发下读账滞后）；
        /// 2026-10-30 起台账只作读数 + 自馈闸门计数（不再有「一回合一次」限制——
        /// 自由分支引擎奖励 2026-11-13 起也取消每回合上限）。</summary>
        public void RecordQueuedActivation(EffectDefinition effect, Entity source = null)
            => RecordUse(effect, source);

        /// <summary>限流台账键：effect.Id + 来源实例（null 源回落纯 Id——主动等无源语境）。</summary>
        private static string UsageKey(EffectDefinition effect, Entity source)
            => UsageKeyOf(effect.Id, source);

        /// <summary>限流台账键（字符串口径）：与上面同键同账，供 UI 只读查询复用。</summary>
        private static string UsageKeyOf(string effectId, Entity source)
            => source != null ? effectId + "@" + source.RuntimeId : effectId;

        /// <summary>只读查询（2026-10-13 手牌卡面 (x/y) 显示口径）：效果在来源实例上的本回合已作用次数。
        /// 与触发上限闸门同键同账；无记录返回 0，回合重置自然归零（View 订阅 EffectUsageChangedEvent 刷新）。</summary>
        public int GetTurnUsageOf(string effectId, Entity source)
            => string.IsNullOrEmpty(effectId) ? 0 : _usageTracker.GetTurnUsage(UsageKeyOf(effectId, source));

        /// <summary>记账并播报（各 RecordActivation 调用点统一走此口——UI (x/y) 读数的事件源）。</summary>
        private void RecordUse(EffectDefinition effect, Entity source)
        {
            _usageTracker.RecordActivation(UsageKey(effect, source));
            EventManager.Instance.Publish(new EffectUsageChangedEvent { EffectId = effect?.Id, Source = source });
        }

        /// <summary>退账并播报（发动层无效退回入栈记账——读数须同步回退）。
        /// §11：每卡效果预算同步退款（发动无效不计入 30 计数）。</summary>
        private void RefundUse(EffectDefinition effect, Entity source)
        {
            _usageTracker.RefundActivation(UsageKey(effect, source));
            if (source is Card budgetCard)
                CardEffectBudget.Instance.Refund(budgetCard);
            EventManager.Instance.Publish(new EffectUsageChangedEvent { EffectId = effect?.Id, Source = source });
        }

        /// <summary>
        /// 检查效果是否可以发动
        /// </summary>
        public bool CanActivate(
            EffectDefinition effect,
            Entity source,
            Player activator,
            Player activePlayer,
            PhaseType currentPhase,
            int turnNumber)
            => TryActivate(effect, source, activator, activePlayer, currentPhase, turnNumber, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：与 CanActivate 同路径单源，逐闸给用户可读文案
        ///（净费闸透传 EffectActivationGate.RejectReason；主消费者=技能预检/UI/网络）。</summary>
        public bool TryActivate(
            EffectDefinition effect,
            Entity source,
            Player activator,
            Player activePlayer,
            PhaseType currentPhase,
            int turnNumber,
            out string reason)
        {
            reason = null;
            // §11 过载（2026-11-10）：过载源主动效果失效——单源预检（CanActivate 同路径；
            // 主消费者=技能预检/UI/网络/AI 候选）。运行时硬闸在 GameActions（入栈前 CanCommit）。
            if (source is Card olCard && CardEffectBudget.Instance.IsOverloaded(olCard))
            {
                reason = "过载：本回合主动/自动效果已失效";
                return false;
            }
            // 0.45 技能效果数值上限（2026-11-02 定案）：技能源（技能栏法术）效果只允许 value=1——
            // value≥2 的效果不可经技能栏发动。构筑期校验（CanBeSkillCard/SkillIneligibleReason）
            // 同口径先行；此闸兜底数据被运行时改写/网络路径绕过构筑闸的情形，拒绝时报卡名+效果。
            if (HeroSkillSystem.IsSkillSource(source))
            {
                var valueViolation = HeroSkillSystem.EffectValueViolation(effect);
                if (valueViolation != null)
                {
                    reason = $"技能效果数值超限：{valueViolation}";
                    TideLog.Error($"[HeroSkill] 技能发动被拒：{(source as CardWrapper)?.GetData()?.CardName} {valueViolation}");
                    return false;
                }
            }

            // 0. 发动代价预检（仅主动）：按宿主分货币（2026-10-15 深夜三类卡改版）——
            //    结界（战场/技能栏）= **拒绝**——结界禁主动效果，只挂自动/光环（运行时临摹/变形
            //    塞进来的主动行也在此拦）；其余（生物/技能区法术=英雄技能载体）= 未横置
            //    （横置为固定代价，发动成功经 KeywordRules.ShouldTap 支付——恒横置，警戒不抵扣）。
            //    触发式发动不消耗横置也不受此闸（触发消耗在入栈口，见 ProcessTriggeredEffects）。
            if (effect.IsActivatedEffect
                && source is Card activateCard
                && (_zoneManager.IsCardInZone(activateCard, activateCard.GetController(), Zone.Battlefield)
                    || _zoneManager.IsCardInZone(activateCard, activateCard.GetController(), Zone.FieldZone)))
            {
                if (Attribute.CounterRules.UsesDurabilityEconomy(activateCard))
                { reason = "结界不能主动发动效果"; return false; } // 结界禁主动效果（2026-10-15 深夜改版）
                if (activateCard.IsTapped())
                { reason = "该卡已横置（本回合已发动过）"; return false; }
            }

            // 0.5 沉默指示物（定案）：持有者不可发动主动效果（主动能力路径；
            //     PlayCard 出牌不受限——那是"打出"而非"发动"）。换区清除。
            //     效果口径读数（2026-10-31 绝缘体裁定）：沉默层对绝缘体单位不生效。
            if (source != null && source.GetEffectiveCounterCount(Attribute.CounterRules.SilenceCounter) > 0)
            { reason = "该卡被沉默——不能发动主动效果"; return false; }

            // 0.55 禁自动令闸 2026-11-06 改版退役（原禁发令=拦主动发动）：主动发动放开——
            // 改拦自动效果（触发式全拦），闸移至 TriggerEngine.FindMatchingEffects（与失效同位）。

            // 0.6 沉睡指示物（2026-09-11 定案）：沉睡期间效果无效——主动同样不可发动
            //（触发式在 TriggerEngine.FindMatchingEffects 拦，双口合流）。
            // 2026-11-09：效果口径读数——绝缘体单位的沉睡层是死层（层照常落账），不拦主动。
            if (source != null && source.GetEffectiveCounterCount(Attribute.KeywordRules.SleepCounter) > 0)
            { reason = "该卡在沉睡中——不能发动"; return false; }

            // 1. 速度检查（由 SpeedCounter 处理，这里不重复）

            // 2. 时点检查
            if (!CheckTiming(effect, currentPhase, activator, activePlayer))
            { reason = "时点不符（当前阶段不可发动该效果）"; return false; }

            // 3. 发动条件检查
            var context = new ConditionCheckContext
            {
                Activator = activator,
                ActivePlayer = activePlayer,
                CurrentPhase = currentPhase,
                TurnNumber = turnNumber,
                Source = source,
                ZoneManager = _zoneManager,
                ActivationsThisTurn = _usageTracker.GetTurnUsage(UsageKey(effect, source)),
                ActivationsThisGame = _usageTracker.GetGameUsage(UsageKey(effect, source))
            };
            context.FillDamageAggregates(); // P2a：伤害聚合死条件修复（服务取本回合口径）

            if (!_conditionChecker.CheckAll(effect.ActivationConditions, context))
            { reason = "发动条件未满足"; return false; }

            // 4. 费用检查：元素代价自动推导（含抵消可达性），特殊代价走原子预检
            //（充能豁免段已随 2026-11-05 充能系统动作化改版删除——充能不再是卡上效果）
            var elementCosts = CostDerivationService.DeriveElementCosts(effect);
            // 技能发动费（2026-11-01 定案，推翻 2026-10-18「免锚价+抉择另收1灰」）：
            // 技能源照走锚价预检（与法术施放/主动效果同价源；UI 技能栏费用显示同为锚价口径）；
            // 特殊代价（牺牲等效果机制）不属「费用」，照收——技能卡禁带代价由资格闸拦（CanBeSkillCard）
            // 临摹/变形发动补价（2026-10-23 开放元素限制——并入灰档=彩元素可补差；灰档支付链六色皆可垫付）：
            // 被变形卡的主动效果按变形前后声明费差额补差——预检与 ExecuteAsync 现付段同口径（付不出=不可发动）
            {
                int morphFee = Attribute.MorphSystem.ActivationSurcharge(source);
                if (morphFee > 0) elementCosts[ManaType.Gray] += morphFee;
            }
            var specialCosts = effect.Costs != null
                ? effect.Costs.Where(c => c.Type != CostType.ElementConsume).ToList()
                : new List<CostInstance>();
            if (!elementCosts.IsZero || specialCosts.Count > 0)
            {
                var costContext = new CostContext
                {
                    Payer = activator,
                    ZoneManager = _zoneManager,
                    ElementPool = _elementPool,
                    Source = source
                };
                // P5（2026-10-29）：主动效果带效果级代价行 → 发动闸接管费用预检
                //（净费=发动费−Σ代价价可付 ∧ CostPayability 代价可支付——资敌白嫖/手牌不足在此拦）；
                // 技能栏不开放（IsSkillSource 分流保底；技能卡禁带代价由资格闸 CanBeSkillCard 拦）。
                if (EffectActivationGate.Applies(effect, source, specialCosts))
                {
                    var gateReason = EffectActivationGate.RejectReason(effect, source, elementCosts, specialCosts, costContext);
                    if (gateReason != null)
                    { reason = gateReason; return false; }
                }
                else
                {
                    // 特殊代价：必须可原子支付
                    if (specialCosts.Count > 0 && !CostHandlerRegistry.CanPayAll(specialCosts, costContext))
                    { reason = "代价不可支付（手牌/生命/牌库等资源不足）"; return false; }
                    // 元素代价：当前 bank 可付（浓度上限口径；黑白获取只经卡结算，无兑换通道）
                    if (!elementCosts.IsZero && !ElementCostPayment.CanPay(elementCosts, costContext))
                    { reason = "费用不足——元素不足以支付发动费"; return false; }
                }
            }

            // 5. 目标有效性检查（2026-09-10 目标域模型：组合域候选空 → 不可发动；
            //    结算期仍各自解析目标——预检拦死局，不替代执行）
            // CanActivate 的 context 是精简构造（无 ZoneManager），此处用完整域预检需要核心句柄——
            // 由调用方（SimpleAI/LegalActionEnumerator 已走 CanActivate）间接覆盖，引擎内先以 def 判域。
            if (effect.TargetDomain != null && effect.TargetDomain.Count > 0 && _zoneManager != null)
            {
                var probe = new EffectExecutionContext
                {
                    Source = source,
                    Controller = activator,
                    ZoneManager = _zoneManager,
                    ElementPool = _elementPool,
                    ModeIndex = 0,
                };
                if (EffectHandlerRegistry.ResolveCandidates(effect.TargetDomain, effect.TargetFilter, probe).Count == 0)
                { reason = "无合法目标（场上没有该效果可选的对象）"; return false; }
            }

            return true;
        }

        /// <summary>
        /// 异步执行效果。
        /// 原子效果通过 EffectHandlerRegistry.ExecuteEffectAsync 解析，交互 handler 可 await UI；
        /// 非交互 handler 即时完成，整体行为与原同步路径一致。
        /// skipElementCost：调用方已付过元素费时跳过执行器的元素支付（防双计费）——
        /// PlayCard 打出法术即此情形（卡费已在打出时支付）；特殊代价仍由执行器支付。
        /// </summary>
        public async UniTask ExecuteAsync(EffectInstance instance, bool skipElementCost = false)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (instance.Definition == null)
                throw new EffectResolutionException(instance.SourceEffect, "Effect instance has no definition");

            var effect = instance.Definition;

            // 发动层无效（2026-10-04 两层无效定案，NegateActivation 标记）：发动本身作废——净零成本跳过：
            // 不移除潜行（未真正发动过效果）、不扣元素费/特殊代价（主动=不扣费，横置为声明期代价不重置）、
            // 不结算原子；触发式退还入栈时的发动次数记账（不计入发动次数）。
            if (instance.IsActivationNegated)
            {
                instance.IsResolved = true;
                if (instance.TriggeringEvent != null)
                    RefundUse(effect, instance.Source);
                EventManager.Instance.Publish(new EffectResolveEvent
                {
                    ResolvedEffect = instance.SourceEffect,
                    Context = instance.EContext ?? new EffectResolutionContext()
                });
                return;
            }

            // 潜行：主动发动效果后消耗 1 层（2026-11-02 改版三失效口之一——宣言攻击结算/守卫拦截
            // 在 CombatSystem.ResolveAttackResolution / ResolveGuardDeclaration；实际受伤不消耗；
            // 触发式（TriggeringEvent != null）非主动发动、不消耗）。
            // 效果口径读数（2026-10-31 绝缘体裁定）：潜行层对绝缘体单位无效果、不消耗
            if (instance.TriggeringEvent == null
                && instance.Source is Card sourceCard
                && sourceCard.GetEffectiveCounterCount(Attribute.CounterRules.StealthCounter) > 0)
            {
                sourceCard.AddCounters(Attribute.CounterRules.StealthCounter, -1);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = sourceCard,
                    Keyword = Attribute.CounterRules.StealthCounter,
                    Detail = "主动发动效果后潜行失效（消耗 1 层）",
                    Source = instance.Source
                });
            }

            // 解析上下文：承载无效化(Negate)/逻辑替换(ReplaceLogic)/撤销(Undo) 状态，
            // 贯穿本次结算并随结算事件发布。
            var resolution = instance.EContext ?? new EffectResolutionContext();
            instance.EContext = resolution;

            // 创建执行上下文
            var context = new EffectExecutionContext
            {
                Source = instance.Source,
                Controller = instance.Controller,
                Targets = instance.Targets,
                TriggeringEvent = instance.TriggeringEvent,
                ZoneManager = _zoneManager,
                ElementPool = _elementPool,
                ModeIndex = instance.ModeIndex, // 抉择：执行引擎按声明期选定的模式分派
                Duration = effect.Duration,               // 组合层编排属性随 context 下发（参照 ModeIndex 先例）
                TriggerLimitPerTurn = effect.TriggerLimitPerTurn, // 次数档随授予传入关键词台账（2026-10-07 深夜）
                SummonDropZone = effect.SummonDropZone,
                CastCard = instance.CastCard, // 施放链路宿主卡（状态门 DrawnInStandbyThisTurn 用）
                ResolvingEffect = effect, // P9b：引用卡槽按 (来源卡ID, 效果序号) 解析卡组期绑定
                RespondedEntry = instance.RespondedEntry, // E1-1 响应绑定透传：反制原子读它打标（跟谁入栈反制谁）
            };

            // 参照显式透传（步5 §3.3）：声明期 Reference spec 选位随实例入栈（网络/重放按 SpecKey
            // 对齐，不依赖预选队列域推导）——种进 context.Refs；分段原子结算时引擎 spec 计划的
            // 解析结果为权威（Cast 档预选两者一致），此处为显式来源。
            if (instance.Refs != null && instance.Refs.Count > 0 && instance.Refs[0] != null)
                context.Refs["ref"] = instance.Refs[0];

            // 参照**在发动时取**（2026-11-13 定案）：参照族（活体炸弹/活体护甲）在代价执行前先定参照
            // 并快照攻/命——代价把参照自己消灭/移走后，本次结算照常进行、数值回落快照，不再空转。
            // 声明期 References（instance.Refs）已给的直接用；AI/无头路径（只传声明期预选目标）在此
            // 用与结算同源的 ResolveAtomTargetsAsync 提前解析参照 atom（只解析参照、不碰受体窗）。
            await PreResolveActivationRefAsync(effect, instance, context);

            // 代价支付：元素代价由配置表自动推导（费用唯一权威），经 ElementCostPayment 纯支付
            // （浓度上限校验 + 混付；兑换通道已随抵消系统退役——黑白获取只经卡结算）；
            // 卡内效果（ElementCostPrepaid）的元素费已随卡牌档位费收讫，跳过以防双计；游戏中途授予的动态效果照付。
            // 卡牌的 effect.Costs 仅保留非元素的特殊代价（Payload 原子），走付费步异步执行+补偿。
            // 抉择卡按所选模式推导（per-mode 独立计价定案）。
            var elementCosts = CostDerivationService.DeriveElementCosts(effect, instance.ModeIndex);
            //（充能锚价豁免段已随 2026-11-05 充能系统动作化改版删除——充能不再是卡上效果）
            // 技能发动费（2026-11-01 定案）：锚价现付——照走通用 DeriveElementCosts（无专属豁免），
            // 与 CanActivate 预检同口径；抉择技能 instance.ModeIndex=所选效果序号，per-mode 计价天然成立
            //（结界发动费抵扣臂已随 2026-10-15 拆分定案退役：免费点今折免费耐久底抵卡价
            //——2026-11-04 碑族0费改版，旧 DurabilityBonusOf/EnchantSpeedBonusOf 两向落位均退役。）
            // 临摹/变形发动补价（2026-10-23 开放元素限制——并入灰档=彩元素可补差，落在底盘抵扣之后——底盘属原卡，
            // 不抵变形溢价）：被变形卡的主动效果按变形前后声明费差额补差并入本次账单；
            // 触发式（ElementCostPrepaid）不经此段；付不出沿用空转口径
            if (effect.IsActivatedEffect && !effect.ElementCostPrepaid)
            {
                int morphFee = Attribute.MorphSystem.ActivationSurcharge(instance.Source);
                if (morphFee > 0) elementCosts[ManaType.Gray] += morphFee;
            }
            var specialCosts = effect.Costs != null
                ? effect.Costs.Where(c => c.Type != CostType.ElementConsume).ToList()
                : new List<CostInstance>();

            // P5（2026-10-29）：主动效果代价行=发动期抵扣——净费 = 发动费 − Σ代价价
            //（ReduceBuckets 先灰后最高费用色、逐色 ≥0、可归零；只读不改推导费）。
            // 净费先付、代价后执行（与卡层 cast 同序）；技能栏不套抵扣（IsSkillSource 分流保底；
            // 技能卡禁带代价由资格闸拦——2026-11-01）、触发式（ElementCostPrepaid）不经此段、
            // 法术 cast（skipElementCost）元素费已在卡费扣讫。
            var payBill = elementCosts;
            if (EffectActivationGate.Applies(effect, instance.Source, specialCosts))
                payBill = EffectActivationGate.NetBill(elementCosts, specialCosts);

            if (!elementCosts.IsZero || specialCosts.Count > 0)
            {
                var costContext = new CostContext
                {
                    Payer = instance.Controller,
                    ZoneManager = _zoneManager,
                    ElementPool = _elementPool,
                    Source = instance.Source
                };

                // 顺序（2026-10-16 恢复 2026-10-04 旧序——对齐 GameActions.ResolveCardCastAsync，
                // 回调 10-11「补偿先付垫本卡」定案）：**元素费先于代价补偿**——
                // 将生成的黑/白不提前计入（本次支付以现有钱包+地牌结算；补偿在效果发动成功、
                // 代价强制执行完成后才入账，垫不了本次账单）。
                //
                // 特殊代价（2026-09-14 代价强制定案：付代价=得黑/白，补偿跟代价走）：
                // 卡牌 cast 的特殊代价已在付费步**强制支付并补偿**（ResolveCardCastAsync，ElementCostPrepaid 标记）；
                // 主动/动态效果在此现付+补偿（同为强制路径 PayWithCompensationAsync——无选择窗口；
                // 快照在执行前取、生效后立即获得黑白；代价无有效目标→不执行不补偿）。
                // 2026-09-16 结算失败口径定案：费用付不出/目标为0 → **空转+日志，不回滚不断链**
                //（入栈期已过滤可付性，此处失败=窗口内状态变化；已付不退，结算链继续）——
                // 旧 EffectResolutionException 会中断整条 ResolveStack，已退役。
                if (!skipElementCost && !effect.ElementCostPrepaid && !elementCosts.IsZero &&
                    !ElementCostPayment.Pay(payBill, costContext))
                {
                    TideLog.Warn($"[EffectExecutor] 效果 {effect.Id} 元素费付不出——空转（不回滚不断链）");
                    instance.IsResolved = true;
                    return;
                }

                if (specialCosts.Count > 0 && !effect.ElementCostPrepaid)
                {
                    if (!await CostCompensationService.PayPayloadsAsync(specialCosts, costContext))
                    {
                        TideLog.Warn($"[EffectExecutor] 效果 {effect.Id} 特殊代价付不出——空转（不回滚不断链）");
                        instance.IsResolved = true;
                        return;
                    }
                    // [代价] 取证（2026-10-21 教学二破釜沉舟排查；2026-10-28 P1：只执行不补偿）：主动效果的
                    // 效果级代价唯一真执行点（cast 付费步按设计跳过主动效果）——此行发出即证明
                    // 装载（EffectsLibrary）→转换（CardEffectConverter）→执行链全通；不发=数据层未带上。
                    TideLog.Info($"[代价] 效果 {effect.Id} 特殊代价已执行（{specialCosts.Count} 条·不产出元素）");
                }
            }

            // 效果层无效（2026-10-04 两层无效定案，NegateEffect 标记）：发动照常——计费已付（不返还）、
            // 潜行已移除、横置不重置（声明期代价）；效果不结算，主动发动次数照记
            //（触发式入栈时已记、不退——计入发动次数）。强制桶同走此门（受效果无效管制）。
            if (instance.IsEffectNegated)
            {
                instance.IsResolved = true;
                RecordUse(effect, instance.Source);
                EventManager.Instance.Publish(new EffectResolveEvent
                {
                    ResolvedEffect = instance.SourceEffect,
                    Context = resolution
                });
                return;
            }

            // 逐原子目标制（2026-10-09）：PerAtomTargets=无效果级作用范围声明——不做组合级共享解析；
            // 声明期预选目标保留透传（响应窗口指向性卡/教学与 AI 预选），由各原子在自身域内择取归属
            //（域外原子结算期自解析弹窗/AI 代选——见两路径循环与 ResolveAtomTargetsAsync 预选归属段）。
            //
            // 重放指示物（2026-11-02 改版：对玩家指示物，蓝5 表行 93648aa7）：**发起方玩家**使用的
            // 法术施放（CastCard=法术卡）或主动效果发动（非触发式·源卡在场）结算后，持重放层 →
            // **先闸后付**再执行一轮——代价可付闸（CostPayability.AllPayableForReplay 溢出制：
            // 库存≥1 即可付，无支付目标=失败）不过 → 重复无效、**层保留**等下一张；过 → 消耗 1 层、
            // 重放轮含代价行溢出执行（「包括效果和代价」，元素费用不重复；共享口径重解析组合目标；
            // PerAtomTargets 各原子循环内自解析）。重放轮不再消耗层（「重放不触发重放」）——
            // 每次真实发动至多重放一次，多层=留给后续发动。循环体只含闸+消耗+代价重执行+目标重置+
            // 原子序列执行：元素费/潜行/记账/结算事件全在外层，保持只走一次；
            // 重放轮 fragments 续加（战报可见两次发动）。
            bool replayed = false;
            // 片段收集（2026-09-16 描述接口化）：逐原子在执行后经 handler.GetDescription(atom, context)
            // 捕获片段（此刻目标=当前目标、LastOutcome=真实产出），末尾聚合为效果级完整文本。
            // 重放轮续加同一份（战报可见两次发动）。
            var fragments = new List<string>();
            while (true)
            {
                if (!effect.PerAtomTargets
                    && (context.Targets == null || context.Targets.Count == 0))
                {
                    context.Targets = await EffectHandlerRegistry.ResolveCompositionTargetsAsync(effect, context);
                }

                // 节点化步骤非空 → per-target 步骤遍历（含 OutcomeGate 分支）；
                // 为空 → 退化为扁平 Effects 线性结算（向后兼容）。
                if (effect.Steps != null && effect.Steps.Count > 0)
                {
                    await ExecuteStepsAsync(effect, context, fragments);
                }
                else
                {
                    await ExecuteFlatAsync(effect, context, fragments);
                }

                if (replayed) break; // 重放轮不再消耗（重放不触发重放）
                if (!TryBeginReplayRerun(instance, specialCosts)) break; // 先闸后付：层∧代价可付——闸不过=重复无效、层保留
                replayed = true;
                // 重放轮含代价（2026-11-02 改版「包括效果和代价」）：效果级代价行 + 法术卡级
                // PayloadCosts 行再执行一轮（元素费用不重复）；可付闸已过，此处溢出执行（弃不满弃光）
                await PayReplayRoundCostsAsync(instance, specialCosts);
                context.Targets = new List<Entity>(); // 清空触发下一轮组合目标重解析
            }

            // 标记已结算
            instance.IsResolved = true;

            // 效果级完整描述（2026-09-16 定案，仅效果级）：聚合写入栈对象 + 发布
            //（战报/UI 栈显示/回放的唯一文本源；原子片段不单独出口）
            if (fragments.Count > 0)
            {
                var who = context.Controller != null ? $"{EffectText.Name(context.Controller)} 的 " : "";
                var name = string.IsNullOrEmpty(effect.DisplayName) ? effect.Id : effect.DisplayName;
                instance.ExecutionSummary = $"{who}{name}：{string.Join("；", fragments)}";
                EventManager.Instance.Publish(new EffectExecutionSummaryEvent
                {
                    Instance = instance,
                    Description = instance.ExecutionSummary,
                    EffectId = effect.Id,
                    Source = instance.Source,
                    Controller = context.Controller
                });
            }

            // 记录使用次数（触发式已在 ProcessTriggeredEffects 入栈时记账，此处防双记；
            // 键含来源实例（2026-10-03）——同名卡/临时复制各记各的。
            //（充能跳过段已随 2026-11-05 充能系统动作化改版删除——充能不再是卡上效果，
            // 每回合规额台账在 StackEngine 充能专账。）
            if (instance.TriggeringEvent == null)
                RecordUse(effect, instance.Source);

            // 眠蚀光环主动结算点直调块已删（2026-10-31 整链退役——见 RuleAuraSystem 墓碑注）

            // 触发效果结算事件（关联来源 Effect 与解析上下文）
            EventManager.Instance.Publish(new EffectResolveEvent
            {
                ResolvedEffect = instance.SourceEffect,
                Context = resolution
            });
        }

        /// <summary>参照**发动时取**（2026-11-13 定案）：分段原子（活体炸弹/活体护甲）在代价执行前
        /// 先定参照并快照攻/命。三条来源按优先级：
        /// ① 声明期 References（instance.Refs，网络/重放显式上送）——已在 context.Refs；
        /// ② 声明期预选目标（context.Targets，本地 AI/人类声明期收集器下发）；
        /// ③ 与结算同源的 ResolveAtomTargetsAsync 现解析（参照 atom 自身域，不触碰受体窗）。
        /// 之后代价即使把参照消灭/移走，结算照常（数值回落快照），不再走「参照亡=空转」。
        /// 非参照族（ReferenceAxis==null）零开销直返。</summary>
        private async Cysharp.Threading.Tasks.UniTask PreResolveActivationRefAsync(
            EffectDefinition effect, EffectInstance instance, EffectExecutionContext context)
        {
            if (effect == null || context == null) return;
            var refAtom = effect.Effects?.FirstOrDefault(a => a != null && a.ReferenceAxis != null);
            if (refAtom == null) return;

            if (context.Ref == null && instance?.Refs != null && instance.Refs.Count > 0)
                context.Refs["ref"] = instance.Refs[0];

            if (context.Ref == null && effect.PerAtomTargets)
            {
                // 声明期预选（本地上游）→ 引擎现解析（AI 无预选时的权威路径）
                var queue = context.Targets != null ? new List<Entity>(context.Targets) : new List<Entity>();
                var picked = await EffectHandlerRegistry.ResolveAtomTargetsAsync(effect, refAtom, context, queue);
                var reference = picked != null ? picked.FirstOrDefault() : null;
                if (reference != null && reference.IsAlive)
                    context.Refs["ref"] = reference;
            }

            context.CaptureRefSnapshot(); // 只在参照存活时写快照（死参照不覆盖）
        }

    }
}
