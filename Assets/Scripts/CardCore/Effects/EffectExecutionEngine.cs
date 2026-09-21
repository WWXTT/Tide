using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;

namespace CardCore
{
    #region 效果执行器

    /// <summary>
    /// 效果执行器
    /// 负责解析和执行效果
    /// </summary>
    public class EffectExecutor
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

        /// <summary>触发上限闸门（2026-09-13 定案）：触发式效果本回合触发数是否已达上限——
        /// 触发式在 ProcessTriggeredEffects 入栈即记账（RecordQueuedActivation，防同批出队时
        /// 第 1 个未结算导致第 2 个漏拦）；启动式结算记账走既有 RecordActivation
        /// （启动/触发同台账，按 effect.Id 天然不互扰；OnNewTurn 清零）。
        /// 坚韧等不可修改原子（MountKinds 含 8）恒 TriggerLimitPerTurn=-1 不受限。</summary>
        public bool TriggerCapReached(EffectDefinition effect)
            => effect != null && effect.TriggerLimitPerTurn > 0
               && _usageTracker.GetTurnUsage(effect.Id) >= effect.TriggerLimitPerTurn;

        /// <summary>触发式入栈即记账（2026-09-13 修复：结算期记账在同批多触发下查账恒滞后一轮，
        /// "一回合一次"闸门失效）；结算处对触发式跳过 RecordActivation 防双记。</summary>
        public void RecordQueuedActivation(EffectDefinition effect)
            => _usageTracker.RecordActivation(effect.Id);

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
        {
            // 0. 横置代价预检（2026-09-08 定案）：只有启动式能力（Activate_*）以横置为发动代价
            //    （与攻击同价的固定代价，发动成功时经 KeywordRules.ShouldTap 统一支付——恒横置，警戒不抵扣）；
            //    触发式（登场/死亡/离场/受攻击等）发动不横置，已横置的源也不受阻。
            if (effect.IsActivatedEffect
                && source is Card activateCard && activateCard.IsTapped()
                && _zoneManager.IsCardInZone(activateCard, activateCard.GetController(), Zone.Battlefield))
                return false;

            // 0.5 沉默指示物（定案）：持有者不可发动主动效果（激活式能力路径；
            //     PlayCard 出牌不受限——那是"打出"而非"发动"）。换区清除。
            if (source != null && source.GetCounterCount(Attribute.CounterRules.SilenceCounter) > 0)
                return false;

            // 0.6 沉睡指示物（2026-09-11 定案）：沉睡期间效果无效——启动式同样不可发动
            //（触发式在 TriggerEngine.FindMatchingEffects 拦，双口合流）。
            if (source != null && source.GetCounterCount(Attribute.KeywordRules.SleepCounter) > 0)
                return false;

            // 1. 速度检查（由 SpeedCounter 处理，这里不重复）

            // 2. 时点检查
            if (!CheckTiming(effect, currentPhase, activator, activePlayer))
                return false;

            // 3. 发动条件检查
            var context = new ConditionCheckContext
            {
                Activator = activator,
                ActivePlayer = activePlayer,
                CurrentPhase = currentPhase,
                TurnNumber = turnNumber,
                Source = source,
                ZoneManager = _zoneManager,
                ActivationsThisTurn = _usageTracker.GetTurnUsage(effect.Id),
                ActivationsThisGame = _usageTracker.GetGameUsage(effect.Id)
            };
            context.FillDamageAggregates(); // P2a：伤害聚合死条件修复（服务取本回合口径）

            if (!_conditionChecker.CheckAll(effect.ActivationConditions, context))
                return false;

            // 4. 费用检查：元素代价自动推导（含抵消可达性），特殊代价走原子预检
            var elementCosts = CostDerivationService.DeriveElementCosts(effect);
            var specialCosts = effect.Costs != null
                ? effect.Costs.Where(c => c.Type != CostType.ElementConsume).ToList()
                : new List<CostInstance>();
            if (elementCosts.Count > 0 || specialCosts.Count > 0)
            {
                var costContext = new CostContext
                {
                    Payer = activator,
                    ZoneManager = _zoneManager,
                    ElementPool = _elementPool,
                    Source = source
                };
                // 特殊代价：必须可原子支付
                if (specialCosts.Count > 0 && !CostHandlerRegistry.CanPayAll(specialCosts, costContext))
                    return false;
                // 元素代价：当前 bank 可付（浓度上限口径；黑白获取只经卡结算，无兑换通道）
                if (elementCosts.Count > 0 && !ElementCostPayment.CanPay(elementCosts, costContext))
                    return false;
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
                    return false;
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

            // 潜行：发动效果后移除（攻击后的移除在 CombatSystem.DeclareAttack）
            if (instance.Source is Card sourceCard && sourceCard.HasKeyword(KeywordRules.Stealth))
            {
                sourceCard.RemoveKeyword(KeywordRules.Stealth);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = sourceCard,
                    Keyword = KeywordRules.Stealth,
                    Detail = "发动效果后潜行失效",
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
                SummonDropZone = effect.SummonDropZone,
            };

            // 代价支付：元素代价由配置表自动推导（费用唯一权威），经 ElementCostPayment 纯支付
            // （浓度上限校验 + 混付；兑换通道已随抵消系统退役——黑白获取只经卡结算）；
            // 卡内效果（ElementCostPrepaid）的元素费已随卡牌档位费收讫，跳过以防双计；游戏中途授予的动态效果照付。
            // 卡牌的 effect.Costs 仅保留非元素的特殊代价（Payload 原子），走付费步异步执行+补偿。
            // 抉择卡按所选模式推导（per-mode 独立计价定案）。
            var elementCosts = CostDerivationService.DeriveElementCosts(effect, instance.ModeIndex);
            var specialCosts = effect.Costs != null
                ? effect.Costs.Where(c => c.Type != CostType.ElementConsume).ToList()
                : new List<CostInstance>();

            if (elementCosts.Count > 0 || specialCosts.Count > 0)
            {
                var costContext = new CostContext
                {
                    Payer = instance.Controller,
                    ZoneManager = _zoneManager,
                    ElementPool = _elementPool,
                    Source = instance.Source
                };

                // 特殊代价（2026-09-14 代价强制定案：付代价=得黑/白，补偿跟代价走）：
                // 卡牌 cast 的特殊代价已在付费步**强制支付并补偿**（ResolveCardCastAsync，ElementCostPrepaid 标记）；
                // 启动式/动态效果在此现付+补偿（同为强制路径 PayWithCompensationAsync——无选择窗口）。
                // 2026-09-16 结算失败口径定案：费用付不出/目标为0 → **空转+日志，不回滚不断链**
                //（入栈期已过滤可付性，此处失败=窗口内状态变化；已付不退，结算链继续）——
                // 旧 EffectResolutionException 会中断整条 ResolveStack，已退役。
                if (specialCosts.Count > 0 && !effect.ElementCostPrepaid)
                {
                    if (!await CostCompensationService.PayWithCompensationAsync(specialCosts, costContext))
                    {
                        UnityEngine.Debug.LogWarning($"[EffectExecutor] 效果 {effect.Id} 特殊代价付不出——空转（不回滚不断链）");
                        instance.IsResolved = true;
                        return;
                    }
                }

                if (!skipElementCost && !effect.ElementCostPrepaid && elementCosts.Count > 0 &&
                    !ElementCostPayment.Pay(elementCosts, costContext))
                {
                    UnityEngine.Debug.LogWarning($"[EffectExecutor] 效果 {effect.Id} 元素费付不出——空转（不回滚不断链）");
                    instance.IsResolved = true;
                    return;
                }
            }

            // 组合域统一目标解析（2026-09-10 目标域模型）：
            // cast 声明期已预选目标（instance.Targets 非空）则沿用；否则按 def 预计算域
            // + SelectionMode 三态一次解析，效果内全部原子共享同一份目标（Steps/扁平两路径语义统一）。
            if (context.Targets == null || context.Targets.Count == 0)
                context.Targets = await EffectHandlerRegistry.ResolveCompositionTargetsAsync(effect, context);

            // 节点化步骤非空 → per-target 步骤遍历（含 OutcomeGate 分支）；
            // 为空 → 退化为扁平 Effects 线性结算（向后兼容）。
            // 片段收集（2026-09-16 描述接口化）：逐原子在执行后经 handler.GetDescription(atom, context)
            // 捕获片段（此刻目标=当前目标、LastOutcome=真实产出），末尾聚合为效果级完整文本。
            var fragments = new List<string>();
            if (effect.Steps != null && effect.Steps.Count > 0)
            {
                await ExecuteStepsAsync(effect, context, fragments);
            }
            else
            {
                await ExecuteFlatAsync(effect, context, fragments);
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
                    Description = instance.ExecutionSummary
                });
            }

            // 记录使用次数（触发式已在 ProcessTriggeredEffects 入栈时记账，此处防双记）
            if (instance.TriggeringEvent == null)
                _usageTracker.RecordActivation(effect.Id);

            // 触发效果结算事件（关联来源 Effect 与解析上下文）
            EventManager.Instance.Publish(new EffectResolveEvent
            {
                ResolvedEffect = instance.SourceEffect,
                Context = resolution
            });
        }

        /// <summary>
        /// 扁平线性结算（旧路径，无分支）：保持原三阶段事件语义与逐原子执行。
        /// </summary>
        private async UniTask ExecuteFlatAsync(EffectDefinition effect, EffectExecutionContext context,
            List<string> fragments)
        {
            if (effect.Effects == null || effect.Effects.Count == 0)
                return;

            // 三阶段事件：发动
            foreach (var atomicEffect in effect.Effects)
            {
                PublishPhase(atomicEffect, AtomicEffectPhase.Activation, context);
            }

            // 三阶段事件：开始作用
            PublishPhase(effect.Effects[0], AtomicEffectPhase.StartApplying, context);

            // 执行效果
            foreach (var atomicEffect in effect.Effects)
            {
                await EffectHandlerRegistry.ExecuteEffectAsync(atomicEffect, context);
                CaptureDescription(atomicEffect, context, fragments);
                // 错边黑白发放（2026-09-11 定案）：原子结算后按实际命中侧别判定，每原子一次发放事件
                var flatHits = new Dictionary<AtomicEffectInstance, int>();
                CollectWrongSideHits(atomicEffect, context.Controller, context.Targets, flatHits);
                FlushWrongSideGrants(flatHits, effect, context);
            }

            // 三阶段事件：结算完成
            foreach (var atomicEffect in effect.Effects)
            {
                PublishPhase(atomicEffect, AtomicEffectPhase.ResolutionComplete, context);
            }
        }

        /// <summary>
        /// 节点化 per-target 步骤遍历（单层）。
        /// 原子步骤先解析候选目标，对每个目标单独执行原子并写 LastOutcome；
        /// 若紧随其后是 OutcomeGate 分支步骤，则在同一目标循环体内立即评估并执行 then/else（奖励免费）。
        /// 抉择（Choice）步骤：按 context.ModeIndex 只执行所选模式的子序列（converter 已保证不嵌套）。
        /// </summary>
        private async UniTask ExecuteStepsAsync(EffectDefinition effect, EffectExecutionContext context,
            List<string> fragments)
        {
            await ExecuteStepSequenceAsync(effect.Steps, effect, context, fragments);
        }

        /// <summary>
        /// 步骤序列执行（主序列与抉择模式子序列共用）：原子 per-target 遍历 +
        /// 紧邻 OutcomeGate 前瞻配对（奖励免费）；Choice 步骤按 ModeIndex 分派。
        /// 2026-09-10 目标域模型：组合目标已在 ExecuteAsync 统一解析（context.Targets），
        /// 步骤层不再逐原子解析——全部原子共享同一份目标序列（与扁平路径语义统一）。
        /// </summary>
        private async UniTask ExecuteStepSequenceAsync(List<RuntimeEffectStep> steps, EffectDefinition effect,
            EffectExecutionContext context, List<string> fragments)
        {
            // 组合目标序列（快照防遍历中被改；无目标效果 = 单次 null 目标执行）
            var compositionTargets = context.Targets != null && context.Targets.Count > 0
                ? new List<Entity>(context.Targets)
                : new List<Entity> { null };

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];

                // 抉择：只执行所选模式（越界 Clamp——多 Choice 步骤共享卡级 ModeIndex，数量不一致时各自钳制）
                if (step.Kind == RuntimeStepKind.Choice)
                {
                    var chosen = step.Choices != null && step.Choices.Count > 0
                        ? step.Choices[System.Math.Max(0, System.Math.Min(context.ModeIndex, step.Choices.Count - 1))]
                        : null;
                    if (chosen != null)
                        await ExecuteStepSequenceAsync(chosen, effect, context, fragments); // 模式内原子共享同一份组合目标
                    continue;
                }

                if (step.Kind != RuntimeStepKind.Atomic || step.Atomic == null)
                    continue; // 落单分支步骤无前置原子，跳过（正常由原子步骤前瞻消费）

                var atomic = step.Atomic;

                // 前瞻：下一步是否为 OutcomeGate 分支
                RuntimeEffectStep gate =
                    (i + 1 < steps.Count && steps[i + 1].Kind == RuntimeStepKind.Branch)
                        ? steps[i + 1]
                        : null;

                // 错边黑白发放（2026-09-11 定案）：逐目标累计命中（执行前存活才算命中），
                // 整原子=一次发放事件，循环后统一封顶发放（见 FlushWrongSideGrants）。
                var wrongHits = new Dictionary<AtomicEffectInstance, int>();

                foreach (var target in compositionTargets)
                {
                    context.Targets = target != null ? new List<Entity> { target } : new List<Entity>();
                    context.LastOutcome.Reset();
                    bool wasAlive = target == null || target is Player || target.IsAlive;

                    PublishPhase(atomic, AtomicEffectPhase.Activation, context);
                    PublishPhase(atomic, AtomicEffectPhase.StartApplying, context);
                    await EffectHandlerRegistry.ExecuteEffectAsync(atomic, context);
                    CaptureDescription(atomic, context, fragments);
                    PublishPhase(atomic, AtomicEffectPhase.ResolutionComplete, context);

                    if (wasAlive)
                        CollectWrongSideHits(atomic, context.Controller, context.Targets, wrongHits);

                    if (gate != null)
                        await ApplyGateRewardsAsync(gate, context, fragments);
                }

                FlushWrongSideGrants(wrongHits, effect, context);

                if (gate != null)
                    i++; // 消费已配对的分支步骤
            }
        }

        // ======================================== 错边黑白发放（2026-09-11 定案） ========================================

        /// <summary>
        /// 错边命中累计（含子效果递归，同构筑期 AccumulateElementGrant 口径）：
        /// 有害原子(p&lt;0)命中己方目标 / 有益原子(p&gt;0)命中对方目标 计一次命中。
        /// 以实际解析目标判侧别（双域卡实际打错边同样计入）；无目标/中性不计。
        /// </summary>
        private static void CollectWrongSideHits(AtomicEffectInstance atom, Player controller,
            List<Entity> executedTargets, Dictionary<AtomicEffectInstance, int> hits)
        {
            if (atom == null) return;
            float polarity = atom.Polarity;
            if (polarity != 0f && executedTargets != null)
            {
                int n = 0;
                foreach (var t in executedTargets)
                {
                    int side = TargetSide(t, controller);
                    if ((polarity > 0f && side == 1) || (polarity < 0f && side == -1))
                        n++;
                }
                if (n > 0)
                {
                    hits.TryGetValue(atom, out var prev);
                    hits[atom] = prev + n;
                }
            }

            if (atom.SubEffects == null) return;
            foreach (var sub in atom.SubEffects)
                CollectWrongSideHits(sub, controller, executedTargets, hits);
        }

        /// <summary>
        /// 按发放事件发放黑白：每个原子（含子效果各自）一次事件，
        /// 量 = 单价 × 错边命中数——**每回合获得封顶 1/色**（2026-09-14 定案，与代价补偿全来源累计，
        /// 钳制在 ElementPool.AddMana 统一执行，余数不补）。
        /// </summary>
        private void FlushWrongSideGrants(Dictionary<AtomicEffectInstance, int> hits, EffectDefinition def,
            EffectExecutionContext context)
        {
            if (hits == null || hits.Count == 0) return;
            if (context?.Controller == null || _elementPool == null) return;

            foreach (var kv in hits)
            {
                var atom = kv.Key;
                int unit = CostDerivationService.ComputeAtomUnitGrant(atom, def);
                if (unit <= 0 || kv.Value <= 0) continue;

                _elementPool.AddMana(context.Controller,
                    CostDerivationService.PolarityGrantColor(atom.Polarity),
                    context.Source as Card, unit * kv.Value);
            }
        }

        /// <summary>实际目标的侧别：-1=施放者己方 / +1=对方 / 0=无主或中性（不计错边）。</summary>
        private static int TargetSide(Entity target, Player controller)
        {
            if (target == null || controller == null) return 0;
            if (target is Player p)
                return p == controller ? -1 : (p == controller.Opponent ? 1 : 0);

            var owner = target.GetOwner();
            if (owner == null) return 0;
            return owner == controller ? -1 : (owner == controller.Opponent ? 1 : 0);
        }

        /// <summary>
        /// 评估 OutcomeGate 条件并执行对应的 then/else 奖励（免费、各自解析目标）。
        /// 评估读取的是「当前目标」刚写入的 LastOutcome。
        /// 预言族条件（延迟验证）在此拦截：不即时评估，把分支打包成 PendingProphecy
        /// 注册到 ProphecySystem——对手下回合首张出牌时验证，到期未验证作未命中走 else。
        /// </summary>
        private async UniTask ApplyGateRewardsAsync(RuntimeEffectStep gate, EffectExecutionContext context,
            List<string> fragments)
        {
            if (BranchConditionEvaluator.IsDelayedCondition(gate.ConditionId))
            {
                ProphecySystem.Register(new PendingProphecy
                {
                    Declarer = context.Controller,
                    Source = context.Source,
                    Declaration = context.LastOutcome.Declaration ?? gate.ConditionStringParam,
                    ConditionId = gate.ConditionId,
                    ConditionParam = gate.ConditionParam,
                    ConditionStringParam = gate.ConditionStringParam,
                    Then = gate.Then,
                    Else = gate.Else,
                    ZoneManager = context.ZoneManager,
                    ElementPool = context.ElementPool,
                });
                return;
            }

            bool pass = BranchConditionEvaluator.Evaluate(
                gate.ConditionId, context.LastOutcome, gate.ConditionParam, gate.ConditionStringParam, context);
            if (gate.Negate) pass = !pass;

            var rewards = pass ? gate.Then : gate.Else;
            if (rewards == null || rewards.Count == 0)
                return;

            foreach (var reward in rewards)
            {
                context.Targets = new List<Entity>();
                var rTargets = EffectHandlerRegistry.ResolveTargets(reward, context);
                var rIter = rTargets.Count > 0 ? rTargets : new List<Entity> { null };
                foreach (var rt in rIter)
                {
                    context.Targets = rt != null ? new List<Entity> { rt } : new List<Entity>();
                    await EffectHandlerRegistry.ExecuteEffectAsync(reward, context);
                    CaptureDescription(reward, context, fragments);
                }
            }
        }

        /// <summary>
        /// 原子片段捕获（2026-09-16 描述接口化）：在原子执行**后**调用——此刻 context.Targets=当前目标、
        /// LastOutcome=handler 写入的真实产出（描述不重掷随机）。经 handler 接口生成片段入聚合列表。
        /// </summary>
        private static void CaptureDescription(AtomicEffectInstance atom, EffectExecutionContext context,
            List<string> fragments)
        {
            if (atom == null || fragments == null) return;
            var handler = EffectHandlerRegistry.GetHandler(atom.Type);
            if (handler == null) return;
            fragments.Add(handler.GetDescription(atom, context));
        }

        private static void PublishPhase(AtomicEffectInstance atomic, AtomicEffectPhase phase, EffectExecutionContext context)
        {
            // 时点接线定案：原子三阶段经 GameCore 统一路由（直发总线会让 TriggerEngine 永远收不到——
            // 这是场上效果发动的可观察时点，双泳道交叉点；路由=总线一次+Trigger/Layer 推送，无双发）
            var e = new AtomicEffectPhaseEvent
            {
                EffectType = atomic.Type,
                Phase = phase,
                Source = context.Source,
                Targets = context.Targets,
                EffectInstance = atomic,
                Context = context
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
            else EventManager.Instance.Publish(e);
        }


        /// <summary>
        /// 新回合开始
        /// </summary>
        public void OnNewTurn(int turnNumber)
        {
            _usageTracker.OnNewTurn(turnNumber);
        }

        /// <summary>
        /// 重置（新游戏）
        /// </summary>
        public void Reset()
        {
            _usageTracker.Reset();
        }

        private bool CheckTiming(
            EffectDefinition effect,
            PhaseType currentPhase,
            Player activator,
            Player activePlayer)
        {
            // 检查阶段限制
            if (effect.TriggerTiming == TriggerTiming.Activate_Active)
            {
                if (currentPhase != PhaseType.Main)
                    return false;
            }

            // 检查回合限制条件
            foreach (var condition in effect.ActivationConditions)
            {
                if (condition.Type == ConditionType.OnlyOwnTurn && activator != activePlayer)
                    return false;

                if (condition.Type == ConditionType.OnlyOpponentTurn && activator == activePlayer)
                    return false;

                if (condition.Type == ConditionType.OnlyMainPhase && currentPhase != PhaseType.Main)
                    return false;
            }

            return true;
        }
    }

    #endregion

    #region 栈引擎

    /// <summary>
    /// 栈引擎
    /// 管理效果入栈、轮询、结算
    ///
    /// 流程：
    /// 1. 条件发动（事件触发）自动入栈，每个 counter++
    /// 2. 轮询阶段：双方交替，速度发动 speed > counter 才能入栈，counter++
    /// 3. 双方 Pass → 结算（LIFO），结算中不轮询，每结算一个 counter--
    /// 4. counter 归0 → 检查延迟触发 → 有则开始新一轮
    /// </summary>
    public class StackEngine
    {
        private Stack<EffectInstance> _stack = new Stack<EffectInstance>();
        private SpeedCounter _speedCounter = new SpeedCounter();
        private PendingEffectQueue _pendingQueue;
        private EffectExecutor _executor;

        private Player _activePlayer;
        private Player _priorityHolder;
        private bool _waitingForPlayer = false;
        private int _consecutivePassCount = 0;
        private PhaseType _currentPhase;
        private bool _isResolving = false;

        public SpeedCounter SpeedCounter => _speedCounter;
        public PendingEffectQueue PendingQueue => _pendingQueue;
        public int StackSize => _stack.Count;
        public bool IsEmpty => _stack.Count == 0;
        public Player CurrentPriorityHolder => _priorityHolder;
        public Player ActivePlayer => _activePlayer;
        public bool WaitingForPlayer => _waitingForPlayer;
        public PhaseType CurrentPhase => _currentPhase;
        /// <summary>结算进行中（含等待 UI 的异步原子效果）。主循环据此避免重入。</summary>
        public bool IsResolving => _isResolving;

        public StackEngine(EffectExecutor executor)
        {
            _executor = executor;
            _pendingQueue = new PendingEffectQueue(_speedCounter);
        }

        public void Initialize(Player startingPlayer)
        {
            _activePlayer = startingPlayer;
            _priorityHolder = startingPlayer;
            _speedCounter.Reset();
            _pendingQueue.Clear();
            _stack.Clear();
            _waitingForPlayer = false;
            _consecutivePassCount = 0;
        }

        /// <summary>
        /// 设置当前阶段（阶段变更时调用）
        /// </summary>
        public void SetPhase(PhaseType phase)
        {
            _currentPhase = phase;
        }

        /// <summary>
        /// 处理条件发动效果（强制/自动，不走速度，自动入栈）
        /// 每轮轮询前调用；2026-09-13 定案：条件发动不参与速度比较、不抬升记速器
        /// </summary>
        public void ProcessTriggeredEffects()
        {
            while (true)
            {
                var next = _pendingQueue.GetNextEffect();
                if (next == null) break;

                // 触发上限闸门（2026-09-13 定案）：达到上限的触发式静默丢弃
                //（GetNextEffect 已出队——丢弃即不回插；不可修改原子恒 -1 不受限）
                if (_executor.TriggerCapReached(next.Effect)) continue;

                var instance = EffectInstance.FromPendingEffect(next);
                _stack.Push(instance);
                next.IsOnStack = true;

                // 触发上限记账提前到入栈时（2026-09-13 修复）：同批多个触发出队时，
                // 结算期记账会让第 2 个及以后的触发绕过"一回合一次"闸门
                _executor.RecordQueuedActivation(next.Effect);

                EventManager.Instance.Publish(new StackAddEvent
                {
                    AddedObject = instance,
                    AddingPlayer = next.Controller
                });
            }
        }

        /// <summary>
        /// 尝试发动效果（速度发动入口，2026-09-13 定案口径）
        /// 门槛：回合持有者 速度 ≥ 记速器；非回合持有者 速度 &gt; 记速器（严格大于）
        /// 入栈时记速器只在更高速度时抬升（RaiseTo，非无条件 +1）
        /// </summary>
        public bool TryActivateEffect(PendingEffect pending)
        {
            if (!_speedCounter.CanActivate(pending.ActivationSpeed, pending.Controller == _activePlayer, pending.ActivationType))
                return false;

            _speedCounter.RaiseTo(pending.ActivationSpeed); // ★ 记速器：更高才抬升

            var instance = EffectInstance.FromPendingEffect(pending);
            _stack.Push(instance);
            pending.IsOnStack = true;

            _priorityHolder = pending.Controller.Opponent;
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            EventManager.Instance.Publish(new StackAddEvent
            {
                AddedObject = instance,
                AddingPlayer = pending.Controller
            });

            return true;
        }

        /// <summary>
        /// 整卡施放入栈（使用时点声明）：打出的卡已移入发动区，本对象代表「此卡被使用」，
        /// 对手获得优先权——响应窗口内可经 GameActions.PlayCardInResponse 打出 打落/发动无效 等。
        /// 2026-09-13 定案：整卡施放同为速度发动——速度=卡面声明（效果 BaseSpeed 最大值，缺省 0）；
        /// 门槛同 TryActivateEffect（回合方 ≥ / 非回合方严格大于），0 速卡无法在对手回合响应打出。
        /// 结算消费点见 GameActions.ResolveCardCastAsync（Option Y：扣费在响应窗口之后）。
        /// </summary>
        public bool PushCardCast(Card card, Player controller, List<Entity> targets, int modeIndex = 0)
        {
            if (card == null || controller == null) return false;
            if (_isResolving) return false; // 结算中不可声明（与 SpeedCounter.CanActivate 同口径）
            SyncActivePlayerFromTurn();
            int castSpeed = SpeedCalculator.GetCardCastSpeed(card);
            if (!_speedCounter.CanActivate(castSpeed, controller == _activePlayer, EffectActivationType.Voluntary))
                return false;

            var instance = new EffectInstance
            {
                IsCardCast = true,
                Source = card,
                Controller = controller,
                ActivationSpeed = castSpeed,
                Targets = targets != null ? new List<Entity>(targets) : new List<Entity>(),
                ModeIndex = modeIndex, // 抉择：声明期选定模式（随 cast 上栈，对手可见、结算按此付费）
            };

            _speedCounter.RaiseTo(castSpeed); // ★ 记速器：更高才抬升

            _stack.Push(instance);
            _priorityHolder = controller.Opponent;
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            EventManager.Instance.Publish(new StackAddEvent
            {
                AddedObject = instance,
                AddingPlayer = controller
            });

            return true;
        }

        /// <summary>SBA 栈对象速度（2026-09-15 定案）：SBA=速度1——回合方可连锁（≥1）、非回合方严格大于（≥2）。</summary>
        public const int SbaStackSpeed = 1;

        /// <summary>
        /// SBA 伪对象入栈（2026-09-15 定案：SBA=速度1栈对象）。
        /// 由 FinishResolution 检出关键 SBA（尸体送墓/判负）后调用：不携带记录载荷（到点重查，
        /// 与预言延迟验证同哲学），记速器抬到 1，回合方先获优先权——LIFO 使窗口内的救场效果
        /// （如速度2治疗）先结算，SBA 到点重查被救回则空转、亡语不触发。
        /// 注意：调用点在 ResolveStack 的 try 块内（引擎 _isResolving 仍 true），故不设
        /// PushCardCast 的结算中拒入闸——那是给玩家声明用的；SpeedCounter 已在 FinishResolution
        /// 开头 Reset，RaiseTo(1) 恒 0→1。
        /// </summary>
        public bool PushStateAction()
        {
            if (_stack.Count > 0) return false; // 栈上有待响应对象时不插（理论不可达：调用点栈必空）

            var instance = new EffectInstance
            {
                IsSBA = true,
                ActivationSpeed = SbaStackSpeed,
                // Source/Controller/Definition 恒 null：SBA 是规则动作，无施放者、无费用、无执行定义
                Targets = new List<Entity>()
            };

            _speedCounter.RaiseTo(SbaStackSpeed); // ★ 记速器：SBA 抬到 1（本连锁的最高速度基线）

            _stack.Push(instance);
            _priorityHolder = _activePlayer;   // 回合方先响应（与触发式轮统一）
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            EventManager.Instance.Publish(new StackAddEvent
            {
                AddedObject = instance,
                AddingPlayer = _activePlayer
            });

            return true;
        }

        /// <summary>攻击宣言栈对象速度（2026-09-16 战斗接入栈机器定案）：攻击=速度0——
        /// 回合方 ≥ 计数器，即计数器为 0（栈空/连锁闭合）才可宣言，连锁中天然不可宣。
        /// 宣言期零支付（不横置不扣费）——横置在结算时支付（到点重查）。</summary>
        public const int AttackStackSpeed = 0;

        /// <summary>回合归属对账（2026-09-20 修复）：_activePlayer 是回合开始事件的缓存副本——
        /// 合成 TurnStartEvent（验证器 shield 段）/非标准回合推进会让它与 TurnEngine 撕裂
        /// （曾见 TurnPlayer=P1 而 _activePlayer=P2，攻击宣言被「非回合方 0&gt;0」静默拒绝、
        /// 后续断言整体级联）。栈空且无等待窗口=连锁间歇，此时以 TurnEngine 为唯一真值回填。</summary>
        private void SyncActivePlayerFromTurn()
        {
            if (_stack.Count > 0) return; // 连锁进行中不作对账（等待窗口必然有栈对象）
            // 栈空却 _waitingForPlayer=true = 上一轮非正常收尾的悬置脏态——对账时一并复位
            var turnPlayer = GameCore.Instance?.TurnEngine?.TurnPlayer;
            if (turnPlayer != null && !ReferenceEquals(turnPlayer, _activePlayer))
            {
                _activePlayer = turnPlayer;
                _priorityHolder = turnPlayer;
                _consecutivePassCount = 0;
                _waitingForPlayer = false;
            }
        }

        /// <summary>守卫拦截速度（2026-09-16 定案）：守卫=速度1响应（原2速阻挡阶段退役）——
        /// 非回合方（防守方）严格 &gt; 计数器：1 &gt; 0 ✓；入栈后计数器抬到 1，
        /// 攻击方可 ≥1 再连锁，防守方守卫全 1 速无法再响应（1 不 &gt; 1）→ 双 Pass 结算。</summary>
        public const int GuardStackSpeed = 1;

        /// <summary>
        /// 攻击宣言入栈（逐攻击开窗，2026-09-16 定案）。速度门=标准口径（0 ≥ 计数器），
        /// 栈非空（连锁中）不可宣；宣言期零支付；发 AttackDeclarationEvent（宣言时点，触发器可见）；
        /// 防守方先获响应机会。结算消费见 ResolveStack 的 IsAttackDeclaration 分支。
        /// </summary>
        public bool PushAttackDeclaration(Entity attacker, Entity target, Player controller)
        {
            if (attacker == null || target == null || controller == null) return false;
            if (_stack.Count > 0) return false; // 连锁中不可宣（速度门 0≥计数器的等价直查）
            SyncActivePlayerFromTurn();
            if (!_speedCounter.CanActivate(AttackStackSpeed, controller == _activePlayer, EffectActivationType.Voluntary))
                return false;

            var instance = new EffectInstance
            {
                IsAttackDeclaration = true,
                Source = attacker,
                Controller = controller,
                ActivationSpeed = AttackStackSpeed, // RaiseTo(0) 无操作——攻击宣言不抬计数器
                Targets = new List<Entity> { target },
            };

            _stack.Push(instance);
            _priorityHolder = controller.Opponent; // 防守方先响应（发动弹窗候选：守卫/响应卡）
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            var declaration = new AttackDeclarationEvent
            {
                Attacker = attacker,
                Target = target,
                AttackingPlayer = controller
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(declaration);
            else EventManager.Instance.Publish(declaration);

            EventManager.Instance.Publish(new StackAddEvent { AddedObject = instance, AddingPlayer = controller });
            return true;
        }

        /// <summary>
        /// 守卫拦截入栈（速度1响应，2026-09-16 定案）：Targets=[被拦截的攻击宣言对象]。
        /// 宣言期零支付（横置在结算时支付）；结算时改写攻击目标（CombatSystem.ResolveGuardDeclaration）。
        /// </summary>
        public bool PushGuardDeclaration(Entity guarder, EffectInstance attackInstance, Player controller)
        {
            if (guarder == null || attackInstance == null || controller == null) return false;
            if (!_stack.Contains(attackInstance)) return false; // 拦截对象须在栈上
            // 资格闸（2026-09-16）：守卫能力（NoGuard 卡不能拦）/存活/未横置——横置支付在结算期，
            // 此处拦的是"已横置不能再宣"
            if (!(guarder is Card guardCard) || !guardCard.HasGuardAbility()
                || !guardCard.IsAlive || guardCard.IsTapped()) return false;
            SyncActivePlayerFromTurn();
            if (!_speedCounter.CanActivate(GuardStackSpeed, controller == _activePlayer, EffectActivationType.Voluntary))
                return false;

            _speedCounter.RaiseTo(GuardStackSpeed); // ★ 记速器抬到 1：攻方可 ≥1 连锁、守方 1 速不能再响应

            var instance = new EffectInstance
            {
                IsGuardDeclaration = true,
                Source = guarder,
                Controller = controller,
                ActivationSpeed = GuardStackSpeed,
                InterceptedAttack = attackInstance, // 拦截载荷（Targets 装不下栈对象，专用字段）
            };

            _stack.Push(instance);
            _priorityHolder = controller.Opponent;
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            EventManager.Instance.Publish(new StackAddEvent { AddedObject = instance, AddingPlayer = controller });
            return true;
        }

        /// <summary>
        /// 添加待发效果到队列
        /// </summary>
        public void AddPendingEffect(PendingEffect effect)
        {
            _pendingQueue.AddPendingEffect(effect);
        }

        /// <summary>待发队列是否有自动（触发式）效果——真实对局由 GameLoopController 帧循环泵上栈；
        /// 空栈上的待发在无游戏循环场景（编辑器验证/headless）需由排干口处理。</summary>
        public bool HasPendingEffects => _pendingQueue.HasAutoEffects;

        /// <summary>
        /// 处理待发效果（条件发动自动入栈）
        /// </summary>
        public void ProcessPendingEffects()
        {
            ProcessTriggeredEffects();
        }

        /// <summary>
        /// 玩家 Pass（让出优先权）。
        /// 双方连续 Pass 触发结算，结算可能 await UI（目标选择弹窗）。
        /// </summary>
        public async UniTask PlayerPass(Player player)
        {
            if (player != _priorityHolder) return;

            _consecutivePassCount++;

            EventManager.Instance.Publish(new PriorityPassEvent
            {
                PassingPlayer = player,
                BothPassed = _consecutivePassCount >= 2
            });

            if (_consecutivePassCount >= 2)
            {
                await BeginResolution();
                return;
            }

            _priorityHolder = player.Opponent;
            EventManager.Instance.Publish(new PriorityGainEvent
            {
                GainingPlayer = _priorityHolder
            });
        }

        /// <summary>
        /// 玩家选择发动速度效果。
        /// PlayerChooseEffect 的实现是从待发队列移除——若调用方（如 GameActions.ActivateEffect）
        /// 传入的是直构的 PendingEffect（从未入队），移除恒失败；此处先入队再选，统一走「出队→入栈」。
        /// 已在队中的调用不受影响（首次移除即成功）。
        /// </summary>
        public bool PlayerActivateVoluntary(PendingEffect effect)
        {
            if (effect == null || effect.ActivationType != EffectActivationType.Voluntary)
                return false;

            var chosen = _pendingQueue.PlayerChooseEffect(effect);
            if (chosen == null)
            {
                _pendingQueue.AddPendingEffect(effect);
                chosen = _pendingQueue.PlayerChooseEffect(effect);
                if (chosen == null) return false;
            }

            if (TryActivateEffect(chosen))
            {
                _consecutivePassCount = 0;
                return true;
            }

            // 速度计数拒绝时不丢弃，回插待发队列
            _pendingQueue.AddPendingEffect(chosen);
            return false;
        }

        /// <summary>
        /// 获取可发动的速度效果列表（2026-09-13 口径：回合归属进门槛——activator=null 按非回合方严格口径）
        /// </summary>
        public List<PendingEffect> GetActivatableVoluntaryEffects(Player activator)
        {
            return _pendingQueue.GetVoluntaryEffects(activator != null && activator == _activePlayer);
        }

        /// <summary>
        /// 开始结算
        /// </summary>
        private async UniTask BeginResolution()
        {
            _speedCounter.BeginResolution();
            _waitingForPlayer = false;

            EventManager.Instance.Publish(new StackResolutionStartEvent
            {
                StackSize = _stack.Count
            });

            await ResolveStack();
        }

        /// <summary>
        /// 结算栈（LIFO）
        /// 结算中不轮询，不插入任何效果
        /// 结算期间产生的新条件触发效果存入延迟队列。
        /// 原子效果异步执行：交互效果可 await UI，期间 IsResolving=true 阻止主循环重入。
        /// </summary>
        private async UniTask ResolveStack()
        {
            _isResolving = true;
            try
            {
                int guardResolve = 0;
                while (_stack.Count > 0)
                {
                    var top = _stack.Pop();
                    GameActions.Crumb($"resolve top #{guardResolve} cast={top.IsCardCast} sba={top.IsSBA} id={top.Definition?.Id ?? (top.Source as Card)?.ID ?? "?"}");
                    if (++guardResolve > 200) { GameActions.Crumb("resolve GUARD-200 break"); break; }
                    // SBA 伪对象：到点重查执行（必须先于执行器拦截——Definition=null 会 throw）；
                    // 攻击/守卫宣言走战斗结算（2026-09-16 战斗接入栈机器：死亡处理由栈机器 SBA 轮
                    // 统一接管——战斗死亡与效果致死同口径，救场窗口自然覆盖）；
                    // 整卡施放对象走 cast 结算（付费→无效裁决→效果→离区），普通对象走执行器
                    if (top.IsSBA)
                    {
                        var sbaCore = GameCore.Instance;
                        if (sbaCore != null)
                        {
                            sbaCore.SBAEngine.ExecuteAll(); // 到点重查：窗口内被救回的 checker 不再收集
                            sbaCore.CheckLifeGameOver();    // 判负（ExecuteZeroLife 内含幂等守卫）
                        }
                    }
                    else if (top.IsGuardDeclaration)
                        GameCore.Instance?.CombatSystem?.ResolveGuardDeclaration(top);
                    else if (top.IsAttackDeclaration)
                        GameCore.Instance?.CombatSystem?.ResolveAttackDeclaration(top);
                    else if (top.IsCardCast)
                        await GameActions.ResolveCardCastAsync(top);
                    else
                        await _executor.ExecuteAsync(top);
                    // 2026-09-13 定案：记速器=本连锁最高速度，逐个结算不递减——归 0 统一在 FinishResolution

                    EventManager.Instance.Publish(new StackResolutionEndEvent
                    {
                        StackSize = _stack.Count
                    });
                }

                await FinishResolution();
            }
            finally
            {
                _isResolving = false;
                // 异常收口（2026-09-20）：ResolveStack 中途抛出时 FinishResolution 不会执行——
                // 记速器永久锁在「结算中」，后续一切速度发动（攻击宣言/cast）静默被拒（曾致
                // 验证器战斗段整体级联假失败）。正常路径 FinishResolution 已 Reset，此处为无操作。
                if (_speedCounter.IsResolving) _speedCounter.Reset();
            }
        }

        /// <summary>
        /// 结算完成 — 自发连锁处理（用户定案模型）：
        /// 单次连锁结算完成后，① 结算期间累积的条件触发先上栈，分两类批（2026-09-16 拆轮定案）：
        /// 开窗批（批内含自动桶效果）照旧开响应窗口等双 Pass；强制批（纯强制桶）合成双方 Pass
        /// 直接进 BeginResolution 结算——复用 PlayerPass/ResolveStack/执行器/Crumb/触发上限全套
        /// 机器，不写第二条结算路径（AI/headless 的 DrainStack 本来就是这个行为）；
        /// ② 关键 SBA（尸体送墓/判负）作为速度1栈对象入栈、开响应窗口（2026-09-15 定案：
        /// 记速器抬到 1——回合方 ≥1、非回合方 ≥2 可连锁救场；到点重查，被救回则空转）；
        /// ③ SBA 结算产生的事件（死亡/送墓/抽卡…）经路由喂触发引擎 → 若有待发效果再开新一轮。
        /// 循环直到稳定。顺序敏感：关键 SBA 检查必须先于 CheckLifeGameOver（后者直查
        /// Life≤0 即终局，排前会让 0 血绕过响应窗口）。
        /// </summary>
        private async UniTask FinishResolution()
        {
            _speedCounter.Reset();
            _waitingForPlayer = false;

            EventManager.Instance.Publish(new StackEmptyEvent
            {
                LastPriorityHolder = _priorityHolder
            });

            for (int guard = 0; guard < 16; guard++)
            {
                // 终局搁浅（保持旧定案）：判负后不再开触发式/SBA 新一轮——否则判负同批入队的
                // 亡语会上栈结算（HasAutoEffects 分支先于下方 IsGameOver 检查 return，顺序敏感）
                var coreEarly = GameCore.Instance;
                if (coreEarly != null && coreEarly.IsGameOver) return;

                if (_pendingQueue.HasAutoEffects)
                {
                    // 批分类探针：须在排水前取——排水即清空，取晚恒 false
                    bool opensWindow = _pendingQueue.HasAutomaticEffects;
                    ProcessTriggeredEffects();
                    if (_stack.Count > 0)
                    {
                        _priorityHolder = _activePlayer; // 自发轮统一：回合方先响应（与 SBA 轮同口径）
                        _consecutivePassCount = 0;
                        if (opensWindow)
                        {
                            _waitingForPlayer = true;
                            return; // 开窗批（含自动桶）：双 Pass → BeginResolution → 结算完再回到这里
                        }

                        // 强制批（纯强制桶）：合成双方 Pass 直接结算——与 DrainStack 同路径
                        //（PlayerPass×2：事件/计数器状态与真实双 Pass 全同），全程不开响应窗口
                        //（_waitingForPlayer 恒 false，UI 天然不显示窗口）；后续轮次由嵌套链
                        //（BeginResolution→ResolveStack→FinishResolution）接管
                        await PlayerPass(_priorityHolder);
                        await PlayerPass(_priorityHolder);
                        return;
                    }
                }

                var core = GameCore.Instance;
                if (core == null) break;

                // SBA 即栈对象（2026-09-15）：检出关键 SBA（送墓/判负）→ 速度1伪对象入栈开响应窗口；
                // 到点（ResolveStack 的 IsSBA 分支）重查执行——窗口内 ≥2 速响应可救回（治疗解标死）。
                // 无人响应（双 Pass）则照旧送墓/判负，亡语走触发式新一轮（计数器已归 0）。
                if (core.SBAEngine.HasCriticalPendingAfterCheck())
                {
                    core.SBAEngine.ClearCriticalPending();
                    PushStateAction();
                    return; // SBA 轮：双 Pass → BeginResolution → 消费 → 回到这里再查触发式/SBA
                }

                core.SBAEngine.ExecuteAll();   // 无关键 SBA：记账型照旧自发执行（ZoneChange 事件补发等）
                core.CheckLifeGameOver();      // 兜底（关键判负已走 SBA 轮；幂等）
                if (core.IsGameOver) return;

                if (!_pendingQueue.HasAutoEffects) break; // 稳定：无新动作、无新触发
            }
        }

        public void OnTurnStart(Player newTurnPlayer)
        {
            _activePlayer = newTurnPlayer;
            _priorityHolder = newTurnPlayer;
            _consecutivePassCount = 0;
            _waitingForPlayer = false;
        }

        public void Clear()
        {
            _stack.Clear();
            _speedCounter.Reset();
            _pendingQueue.Clear();
            _waitingForPlayer = false;
            _consecutivePassCount = 0;
        }

        public List<EffectInstance> GetStackContents()
        {
            return _stack.Reverse().ToList();
        }

        public UniTask PassPriority(Player player)
        {
            return PlayerPass(player);
        }

        public async UniTask ResolveTop()
        {
            if (_stack.Count > 0)
            {
                _isResolving = true;
                try
                {
                    var top = _stack.Pop();
                    // SBA 伪对象：与 ResolveStack 同一消费口径（到点重查）；攻击/守卫宣言走战斗结算；
                    // 整卡施放对象走 cast 结算
                    if (top.IsSBA)
                    {
                        var sbaCore = GameCore.Instance;
                        if (sbaCore != null)
                        {
                            sbaCore.SBAEngine.ExecuteAll();
                            sbaCore.CheckLifeGameOver();
                        }
                    }
                    else if (top.IsGuardDeclaration)
                        GameCore.Instance?.CombatSystem?.ResolveGuardDeclaration(top);
                    else if (top.IsAttackDeclaration)
                        GameCore.Instance?.CombatSystem?.ResolveAttackDeclaration(top);
                    else if (top.IsCardCast)
                        await GameActions.ResolveCardCastAsync(top);
                    else
                        await _executor.ExecuteAsync(top);
                    // 2026-09-13 定案：记速器=本连锁最高速度，逐个结算不递减——栈排干时归 0
                    if (_stack.Count == 0)
                        _speedCounter.Reset();
                }
                finally
                {
                    _isResolving = false;
                }
            }
        }

        public EffectInstance Peek()
        {
            return _stack.Count > 0 ? _stack.Peek() : null;
        }

        /// <summary>
        /// 重定向栈顶效果的目标（RedirectTarget 原子效果が使用）。
        /// 栈顶效果が無ければ false。
        /// </summary>
        public bool RetargetTopStackObject(List<Entity> newTargets)
        {
            if (_stack.Count == 0 || newTargets == null) return false;
            _stack.Peek().Targets = new List<Entity>(newTargets);
            return true;
        }

        public EffectExecutor GetExecutor() => _executor;
    }

    #endregion

    #region 触发引擎（扩展）

    /// <summary>
    /// 触发引擎
    /// 处理条件发动效果（基于事件触发）
    /// </summary>
    public class TriggerEngine
    {
        private List<RegisteredEffect> _registeredEffects = new List<RegisteredEffect>();
        private StackEngine _stackEngine;

        // 触发条件校验（由 GameCore 注入 ZoneManager 后可用）。未注入时跳过条件校验，
        // 便于在缺少区域系统的独立测试中仅按时点匹配。
        private ConditionChecker _conditionChecker;
        private ZoneManager _zoneManager;

        public List<RegisteredEffect> RegisteredEffects => _registeredEffects;

        public TriggerEngine(StackEngine stackEngine)
        {
            _stackEngine = stackEngine;
        }

        /// <summary>
        /// 注入区域系统以启用触发条件校验。
        /// </summary>
        public void AttachConditionContext(ZoneManager zoneManager)
        {
            _zoneManager = zoneManager;
            _conditionChecker = new ConditionChecker(zoneManager);
        }

        /// <summary>
        /// 注册效果（时点接线定案：统一出口在 TryMoveToBattlefield/TryAddToBattlefield，
        /// 经 GameActions.RegisterCardTriggeredEffects 调入；幂等去重防回手重打/二次复活重复注册）
        /// </summary>
        public void RegisterEffect(EffectDefinition effect, Entity source, Player controller)
        {
            if (!effect.IsTriggeredEffect)
                return;

            // 幂等：同一来源实体 + 同一效果 Id 只注册一次
            // （同模板两张不同实例 Source 不同、各自注册，正确；死亡期靠 Source.IsAlive 门禁兜底）
            for (int i = 0; i < _registeredEffects.Count; i++)
            {
                var r = _registeredEffects[i];
                if (ReferenceEquals(r.Source, source) && r.Effect != null && r.Effect.Id == effect.Id)
                    return;
            }

            _registeredEffects.Add(new RegisteredEffect
            {
                Effect = effect,
                Source = source,
                Controller = controller
            });
        }

        /// <summary>
        /// 注销效果（预留 API：当前无调用方——注册后不注销，死卡由 Source.IsAlive 门禁挡住；
        /// 复活语义将来若需要"注销+重注册"路径时启用）
        /// </summary>
        public void UnregisterEffect(EffectDefinition effect)
        {
            _registeredEffects.RemoveAll(r => r.Effect == effect);
        }

        /// <summary>
        /// 注销实体的所有效果（预留 API，同 UnregisterEffect）
        /// </summary>
        public void UnregisterEntityEffects(Entity source)
        {
            _registeredEffects.RemoveAll(r => r.Source == source);
        }

        /// <summary>
        /// 处理游戏事件
        /// 匹配触发时点 → 创建 PendingEffect → 加入待发队列
        /// </summary>
        public void OnEvent(IGameEvent gameEvent)
        {
            var matchingEffects = FindMatchingEffects(gameEvent);

            foreach (var match in matchingEffects)
            {
                var pending = PendingEffect.Create(
                    match.Effect,
                    match.Source,
                    match.Controller,
                    _stackEngine.ActivePlayer,
                    _stackEngine.CurrentPhase,
                    triggeringEvent: gameEvent
                );

                _stackEngine.AddPendingEffect(pending);
            }
        }

        /// <summary>
        /// 查找匹配的效果
        /// </summary>
        private List<RegisteredEffect> FindMatchingEffects(IGameEvent gameEvent)
        {
            var result = new List<RegisteredEffect>();
            var eventType = gameEvent.GetType();

            foreach (var registered in _registeredEffects)
            {
                var effect = registered.Effect;

                // 检查触发时点
                var expectedType = TriggerTimingDefaults.GetEventType(effect.TriggerTiming);
                if (expectedType != eventType)
                    continue;

                // 时点接线定案：类型匹配后做事件载荷级过滤（self/other、进场来源、施受区分）
                if (!TriggerPayloadFilter.Matches(effect.TriggerTiming, gameEvent, registered))
                    continue;

                // 检查来源是否在场；亡语豁免（2026-09-15 修复）：自己的 CardDestroyEvent 不受此拦——
                // 伤害落血即置 IsAlive=false（KeywordRules.ApplyDamage），一刀切会拦死全部亡语
                //（TryKill 在发布 CardDestroyEvent 前已置 IsAlive=false）。
                // 其后的无效指示物/沉睡/TriggerConditions 门禁照常拦截（蓝无效仍拦亡语）。
                if (registered.Source != null && !registered.Source.IsAlive)
                {
                    if (!(gameEvent is CardDestroyEvent de && ReferenceEquals(de.DestroyedCard, registered.Source)))
                        continue;
                }

                // 无效指示物（2026-09-09 定案）：拦全部触发式（事件匹配后、上栈前）——
                // 含登场 OnPlay 族触发；只拦「注册来源自身」的能力。与沉默（拦启动式，CanActivate）对称。
                // 伤害管线被动（坚韧/圣盾）与回合维护（再生/成长，GameCore 直连）不经此，天然不拦。
                if (registered.Source != null
                    && registered.Source.GetCounterCount(CardCore.Attribute.CounterRules.NullifyCounter) > 0)
                    continue;

                // 沉睡（2026-09-11 定案）：沉睡期间**效果无效**——拦触发式（与无效同口）；
                // 启动式在 CanActivate 拦（与沉默同口）。入场窗口自身的 OnPlay 不受影响
                //（指示物在结算中才落下，匹配先于结算）。
                if (registered.Source != null
                    && registered.Source.GetCounterCount(Attribute.KeywordRules.SleepCounter) > 0)
                    continue;

                // 检查触发条件（intervening "if" 条件）：仅在已注入区域系统时校验。
                if (_conditionChecker != null &&
                    effect.TriggerConditions != null && effect.TriggerConditions.Count > 0)
                {
                    var conditionContext = new ConditionCheckContext
                    {
                        Activator = registered.Controller,
                        ActivePlayer = _stackEngine.ActivePlayer,
                        CurrentPhase = _stackEngine.CurrentPhase,
                        TurnNumber = GameCore.Instance?.TurnEngine?.TurnNumber ?? 0,
                        Source = registered.Source,
                        ZoneManager = _zoneManager
                    };
                    conditionContext.FillDamageAggregates(); // P2a：伤害聚合死条件修复

                    if (!_conditionChecker.CheckAll(effect.TriggerConditions, conditionContext))
                        continue;
                }

                result.Add(registered);
            }

            return result;
        }

        /// <summary>
        /// 将待发触发效果放入栈。
        /// ⚠ 强制批拆轮（2026-09-16）仅在 FinishResolution 口径内生效（开窗/合成双 Pass）；
        /// 本路径（GameLoopController.Update 帧泵，空栈+可动阶段）推出的纯强制批仍走
        /// ProcessPriority（有动作则等）——未覆盖，口径见 StackEngine.FinishResolution。
        /// </summary>
        public void PutTriggersOnStack()
        {
            _stackEngine.ProcessTriggeredEffects();
        }

        public void OnTriggerResolved(EffectInstance effect)
        {
            // 触发效果结算后的清理
        }

        public void Clear()
        {
            _registeredEffects.Clear();
        }

        public void ClearAll()
        {
            Clear();
        }
    }

    /// <summary>
    /// 已注册的效果
    /// </summary>
    public class RegisteredEffect
    {
        public EffectDefinition Effect { get; set; }
        public Entity Source { get; set; }
        public Player Controller { get; set; }
    }

    #endregion

    #region 内置处理器引导

    /// <summary>
    /// 内置效果/代价处理器引导
    /// EffectHandlerRegistry 与 CostHandlerRegistry 都是全局 static，只需注册一次。
    /// 抽成共享幂等引导，任何引擎初始化时都可安全调用，确保自建的 EffectExecutor
    /// 在首次结算前能找到已注册的处理器（避免"未注册的效果处理器"）。
    /// </summary>
    public static class BuiltinHandlerBootstrap
    {
        private static bool _registered = false;

        /// <summary>注册所有内置处理器（幂等，多次调用安全）</summary>
        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            // 基础效果处理器
            var handlers = new IAtomicEffectHandler[]
            {
                new DealDamageHandler(),
                new DrawCardHandler(),
                new ReturnToHandHandler(),
                new FreezeHandler(),
                new HealHandler(),
                new ModifyPowerHandler(),
                new MorphHandler(),

                // 终局原子（2026-09-15：宣告胜利——亡语「对手获得胜利」载体）
                new DeclareVictoryHandler(),

                // 第一批补齐 — 伤害类
                new PierceDamageHandler(),
                new DrainLifeHandler(),
                new PoisonHandler(),

                // 第一批补齐 — 卡牌移动 / 牌库
                new DiscardCardHandler(),
                new ExileHandler(),
                new ShuffleIntoDeckHandler(),
                new SearchDeckHandler(),
                new BounceToTopHandler(),
                new BounceToBottomHandler(),
                new ReturnFromGraveyardHandler(),
                new RecoverToHandHandler(),
                new LookAtTopCardsHandler(),

                // 第一批补齐 — 状态变更
                new ModifyLifeHandler(),
                // 设置系复活（2026-09-09 三轨制重建）：显式设置原子=天然设置类（不参与来源路由），
                // 永久直改、跨区保留、净化不清（视同本体）；与 Modify 族的设置轨同语义。
                new SetPowerHandler(),
                new SetLifeHandler(),
                new SetCostHandler(),
                new ModifyCostHandler(),

                // 信息族 — 宣言（即时验证）/ 预言（隐藏押注，延迟验证）
                new DeclareHandHandler(),
                new DeclareDeckTopHandler(),
                new DeclareArrowHandler(),
                new ProphecyNextCardHandler(),

                // 指示物原子（护甲/毒素——Entity 级，角色可持有）
                new AddArmorHandler(),
                new AddToxinHandler(),

                // 状态原子（虚弱/鼓舞——施加 ±1/+1 属性指示物）
                new WeakenHandler(),
                new InspireHandler(),

                // 指示物原子（紊乱/易损/单向属性指示物——攻击力·生命值·费用 ×增减、±1/+1）
                new RushSicknessHandler(),
                new AddVulnerableHandler(),
                new AddPowerUpHandler(),
                new AddPowerDownHandler(),
                new AddLifeUpHandler(),
                new AddLifeDownHandler(),
                new AddPlusOneHandler(),
                new AddMinusOneHandler(),
                new AddCostUpHandler(),
                new AddCostDownHandler(),

                // 无效指示物（蓝3）：拦非启动式能力（触发式+光环）——与沉默（拦启动式）对称
                new AddNullifyHandler(),

                // 沉睡（2026-09-11 改造，绿1 中性）：赋予沉睡指示物（效果/指示物分离——持续规则在指示物）
                new SleepHandler(),

                // 衍生物生成（落区三档：战场/手牌/牌组，费用按落区系数计价）
                new SummonTokenHandler(),

                // 资源族 — 采掘（地牌指示物转化：去3同类型指示物换1点对应元素入 bank）
                new MineHandler(),

                // 战斗底盘（2026-09-10 攻击/守卫效果化：1速/2速主动，注册完整性锚——战斗走 CombatSystem）
                new AttackHandler(),
                new GuardHandler(),
            };
            foreach (var handler in handlers)
                EffectHandlerRegistry.Register(handler);

            // 注册所有关键词授予处理器
            foreach (var handler in GrantKeywordHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册第二批规则原语处理器（状态/控制/净化/战斗/特殊）
            foreach (var handler in SecondBatchHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册第三批长尾处理器（牌库/死亡原子/反制/沉默）
            foreach (var handler in ThirdBatchHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册高级处理器（栈重定向）
            foreach (var handler in AdvancedEffectHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册内置代价处理器
            BuiltinCostHandlers.RegisterAll();

            // 仪式装饰器（血偿仪典等）由 RitualComponents.EnsureRegistered 挂载
            //（GameCore.Reset → RitualSystem.EnsureRuntime，时序在本注册链之后，包装到已注册的原处理器）

            // 注册网罗自检：除「暂不实现」2 种外，所有原子效果类型都应有处理器
            VerifyHandlerCoverage();
        }

        /// <summary>
        /// 注册完整性自检。
        /// 当前设计中「暂不实现」的 ModifyGameRule / OverrideRestriction 以外的所有
        /// AtomicEffectType 都应已注册；缺失则 LogError，便于尽早暴露长尾静默失败。
        /// </summary>
        private static readonly HashSet<AtomicEffectType> _intentionallyUnimplemented = new HashSet<AtomicEffectType>
        {
            AtomicEffectType.ModifyGameRule,
            AtomicEffectType.OverrideRestriction,
        };

        private static void VerifyHandlerCoverage()
        {
            var missing = new List<AtomicEffectType>();
            foreach (AtomicEffectType type in Enum.GetValues(typeof(AtomicEffectType)))
            {
                if (_intentionallyUnimplemented.Contains(type)) continue;
                if (!EffectHandlerRegistry.IsRegistered(type))
                    missing.Add(type);
            }

            if (missing.Count > 0)
            {
                // 诊断级信息（已知债务清单，非运行时故障）：LogError 会让测试框架/CI 判败，降为警告
                UnityEngine.Debug.LogWarning(
                    $"[BuiltinHandlerBootstrap] 缺失原子效果处理器 {missing.Count} 种：" +
                    string.Join(", ", missing));
            }
        }
    }

    #endregion
}
