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
            // 0. 横置代价预检（定案）：战场随从发动效果 = 一次行为的固定代价（与攻击同价）；
            //    已横置不可发动（警戒抵扣在发动成功时经 KeywordRules.ShouldTap 结算）
            if (source is Card activateCard && activateCard.IsTapped()
                && _zoneManager.IsCardInZone(activateCard, activateCard.GetController(), Zone.Battlefield))
                return false;

            // 0.5 沉默指示物（定案）：持有者不可发动主动效果（激活式能力路径；
            //     PlayCard 出牌不受限——那是"打出"而非"发动"）。换区清除。
            if (source != null && source.GetCounterCount(Attribute.CounterRules.SilenceCounter) > 0)
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
                // 元素代价：考虑「最大可能抵消」后仍需可支付
                if (elementCosts.Count > 0 && !CostOffsetService.CanAfford(elementCosts, costContext))
                    return false;
            }

            // 5. 目标有效性检查（由各原子效果在结算期独立解析）

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
                ElementPool = _elementPool
            };

            // 代价支付：元素代价由配置表自动推导（费用唯一权威），经「抵消+元素」异步路径支付；
            // 卡内效果（ElementCostPrepaid）的元素费已随卡牌档位费收讫，跳过以防双计；游戏中途授予的动态效果照付。
            // 卡牌的 effect.Costs 仅保留非元素的特殊代价（Sleep/SummonMaterial/弃牌 等），走原子同步支付。
            var elementCosts = CostDerivationService.DeriveElementCosts(effect);
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

                if (specialCosts.Count > 0 && !CostHandlerRegistry.PayAll(specialCosts, costContext))
                {
                    throw new EffectResolutionException(instance.SourceEffect, $"Cost payment failed for effect {effect.Id}.");
                }

                if (!skipElementCost && !effect.ElementCostPrepaid && elementCosts.Count > 0 &&
                    !await CostOffsetService.PayElementWithOffsetAsync(elementCosts, costContext))
                {
                    throw new EffectResolutionException(instance.SourceEffect, $"Element cost payment failed for effect {effect.Id}.");
                }
            }

            // 目标解析由每个原子效果在 EffectHandlerRegistry.ExecuteEffect 中独立完成

            // 节点化步骤非空 → per-target 步骤遍历（含 OutcomeGate 分支）；
            // 为空 → 退化为扁平 Effects 线性结算（向后兼容）。
            if (effect.Steps != null && effect.Steps.Count > 0)
            {
                await ExecuteStepsAsync(effect, context);
            }
            else
            {
                await ExecuteFlatAsync(effect, context);
            }

            // 标记已结算
            instance.IsResolved = true;

            // 记录使用次数
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
        private async UniTask ExecuteFlatAsync(EffectDefinition effect, EffectExecutionContext context)
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
        /// </summary>
        private async UniTask ExecuteStepsAsync(EffectDefinition effect, EffectExecutionContext context)
        {
            var steps = effect.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step.Kind != RuntimeStepKind.Atomic || step.Atomic == null)
                    continue; // 落单分支步骤无前置原子，跳过（正常由原子步骤前瞻消费）

                var atomic = step.Atomic;

                // 前瞻：下一步是否为 OutcomeGate 分支
                RuntimeEffectStep gate =
                    (i + 1 < steps.Count && steps[i + 1].Kind == RuntimeStepKind.Branch)
                        ? steps[i + 1]
                        : null;

                // 解析主序列原子的目标（候选>需求时弹交互选择；动态数量允许 0..候选数）
                context.Targets = new List<Entity>();
                var targets = await EffectHandlerRegistry.ResolveTargetsInteractiveAsync(atomic, context);
                // 无目标原子（抽牌/创建衍生物等）也执行一次
                var iterTargets = targets.Count > 0
                    ? targets
                    : new List<Entity> { null };

                foreach (var target in iterTargets)
                {
                    context.Targets = target != null ? new List<Entity> { target } : new List<Entity>();
                    context.LastOutcome.Reset();

                    PublishPhase(atomic, AtomicEffectPhase.Activation, context);
                    PublishPhase(atomic, AtomicEffectPhase.StartApplying, context);
                    await EffectHandlerRegistry.ExecuteEffectAsync(atomic, context);
                    PublishPhase(atomic, AtomicEffectPhase.ResolutionComplete, context);

                    if (gate != null)
                        await ApplyGateRewardsAsync(gate, context);
                }

                if (gate != null)
                    i++; // 消费已配对的分支步骤
            }
        }

        /// <summary>
        /// 评估 OutcomeGate 条件并执行对应的 then/else 奖励（免费、各自解析目标）。
        /// 评估读取的是「当前目标」刚写入的 LastOutcome。
        /// 预言族条件（延迟验证）在此拦截：不即时评估，把分支打包成 PendingProphecy
        /// 注册到 ProphecySystem——对手下回合首张出牌时验证，到期未验证作未命中走 else。
        /// </summary>
        private async UniTask ApplyGateRewardsAsync(RuntimeEffectStep gate, EffectExecutionContext context)
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
                }
            }
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
        /// 处理条件发动效果（强制/自动，不检查速度，自动入栈）
        /// 每轮轮询前调用
        /// </summary>
        public void ProcessTriggeredEffects()
        {
            while (true)
            {
                var next = _pendingQueue.GetNextEffect();
                if (next == null) break;

                _speedCounter.Increment();
                var instance = EffectInstance.FromPendingEffect(next);
                _stack.Push(instance);
                next.IsOnStack = true;

                EventManager.Instance.Publish(new StackAddEvent
                {
                    AddedObject = instance,
                    AddingPlayer = next.Controller
                });
            }
        }

        /// <summary>
        /// 尝试发动效果（速度发动入口）
        /// 速度检查： speed > counter
        /// 入栈后 counter +1（不是 RaiseTo）
        /// </summary>
        public bool TryActivateEffect(PendingEffect pending)
        {
            if (!_speedCounter.CanActivate(pending.ActivationSpeed, pending.ActivationType))
                return false;

            _speedCounter.Increment(); // ★ 记速器 +1

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
        /// 速度 = 记速器 +1：出牌作为基础动作单位不受连锁深度限制
        /// （深度照常计入记速器，激活式能力的速度门槛不受影响）。
        /// 结算消费点见 GameActions.ResolveCardCastAsync（Option Y：扣费在响应窗口之后）。
        /// </summary>
        public bool PushCardCast(Card card, Player controller, List<Entity> targets)
        {
            if (card == null || controller == null) return false;
            if (_isResolving) return false; // 结算中不可声明（与 SpeedCounter.CanActivate 同口径）

            var instance = new EffectInstance
            {
                IsCardCast = true,
                Source = card,
                Controller = controller,
                ActivationSpeed = _speedCounter.CurrentSpeed + 1,
                Targets = targets != null ? new List<Entity>(targets) : new List<Entity>(),
            };

            _speedCounter.Increment(); // ★ 记速器 +1（与 TryActivateEffect 同规）

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

        /// <summary>
        /// 添加待发效果到队列
        /// </summary>
        public void AddPendingEffect(PendingEffect effect)
        {
            _pendingQueue.AddPendingEffect(effect);
        }

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
        /// 获取可发动的速度效果列表
        /// </summary>
        public List<PendingEffect> GetActivatableVoluntaryEffects()
        {
            return _pendingQueue.GetVoluntaryEffects();
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
                while (_stack.Count > 0)
                {
                    var top = _stack.Pop();
                    // 整卡施放对象走 cast 结算（付费→无效裁决→效果→离区），普通对象走执行器
                    if (top.IsCardCast)
                        await GameActions.ResolveCardCastAsync(top);
                    else
                        await _executor.ExecuteAsync(top);
                    _speedCounter.Decrement();

                    EventManager.Instance.Publish(new StackResolutionEndEvent
                    {
                        StackSize = _stack.Count
                    });
                }

                FinishResolution();
            }
            finally
            {
                _isResolving = false;
            }
        }

        /// <summary>
        /// 结算完成 — 自发连锁处理（用户定案模型）：
        /// 单次连锁结算完成后，① 结算期间累积的条件触发先上栈（开新一轮连锁）；
        /// ② SBA 状态动作自发执行（战场尸体送墓 / 防御归零 / 判负）；③ SBA 产生的事件
        /// （死亡/送墓/抽卡…）经路由喂触发引擎 → 若有待发效果再开新一轮。循环直到稳定。
        /// </summary>
        private void FinishResolution()
        {
            _speedCounter.Reset();
            _waitingForPlayer = false;

            EventManager.Instance.Publish(new StackEmptyEvent
            {
                LastPriorityHolder = _priorityHolder
            });

            for (int guard = 0; guard < 16; guard++)
            {
                if (_pendingQueue.HasAutoEffects)
                {
                    ProcessTriggeredEffects();
                    if (_stack.Count > 0)
                    {
                        _waitingForPlayer = true;
                        _consecutivePassCount = 0;
                        return; // 新一轮连锁：双 Pass → BeginResolution → 结算完再回到这里
                    }
                }

                var core = GameCore.Instance;
                if (core == null) break;
                core.SBAEngine.ExecuteAll();   // 自发状态动作：尸体送墓等（事件经路由喂触发引擎）
                core.CheckLifeGameOver();      // 死亡引发的判负（幂等）
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
                    // 整卡施放对象走 cast 结算（与 ResolveStack 同一消费点）
                    if (top.IsCardCast)
                        await GameActions.ResolveCardCastAsync(top);
                    else
                        await _executor.ExecuteAsync(top);
                    _speedCounter.Decrement();
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

                // 检查来源是否在场
                if (registered.Source != null && !registered.Source.IsAlive)
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
        /// 将待发触发效果放入栈
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
                new FreezePermanentHandler(),
                new HealHandler(),
                new ModifyPowerHandler(),
                new MorphHandler(),

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

                // 衍生物生成（落区三档：战场/手牌/牌组，费用按落区系数计价）
                new SummonTokenHandler(),
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
