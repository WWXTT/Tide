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
        /// </summary>
        public async UniTask ExecuteAsync(EffectInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (instance.Definition == null)
                throw new EffectResolutionException(instance.SourceEffect, "Effect instance has no definition");

            var effect = instance.Definition;

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

                if (elementCosts.Count > 0 &&
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
        /// </summary>
        private async UniTask ApplyGateRewardsAsync(RuntimeEffectStep gate, EffectExecutionContext context)
        {
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
            EventManager.Instance.Publish(new AtomicEffectPhaseEvent
            {
                EffectType = atomic.Type,
                Phase = phase,
                Source = context.Source,
                Targets = context.Targets,
                EffectInstance = atomic,
                Context = context
            });
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
        /// 玩家选择发动速度效果
        /// </summary>
        public bool PlayerActivateVoluntary(PendingEffect effect)
        {
            if (effect.ActivationType != EffectActivationType.Voluntary)
                return false;

            var chosen = _pendingQueue.PlayerChooseEffect(effect);
            if (chosen == null) return false;

            if (TryActivateEffect(chosen))
            {
                _consecutivePassCount = 0;
                return true;
            }

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
        /// 结算完成 — 检查延迟触发，可能开始新一轮
        /// </summary>
        private void FinishResolution()
        {
            _speedCounter.Reset();
            _waitingForPlayer = false;

            EventManager.Instance.Publish(new StackEmptyEvent
            {
                LastPriorityHolder = _priorityHolder
            });

            // 结算期间产生的条件触发效果 → 新一轮
            if (_pendingQueue.HasAutoEffects)
            {
                ProcessTriggeredEffects();
                if (_stack.Count > 0)
                {
                    _waitingForPlayer = true;
                    _consecutivePassCount = 0;
                    return;
                }
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
        /// 注册效果
        /// </summary>
        public void RegisterEffect(EffectDefinition effect, Entity source, Player controller)
        {
            if (!effect.IsTriggeredEffect)
                return;

            _registeredEffects.Add(new RegisteredEffect
            {
                Effect = effect,
                Source = source,
                Controller = controller
            });
        }

        /// <summary>
        /// 注销效果
        /// </summary>
        public void UnregisterEffect(EffectDefinition effect)
        {
            _registeredEffects.RemoveAll(r => r.Effect == effect);
        }

        /// <summary>
        /// 注销实体的所有效果
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
                new DestroyHandler(),
                new GrantHasteHandler(),
                new DrawCardHandler(),
                new ReturnToHandHandler(),
                new FreezePermanentHandler(),
                new HealHandler(),
                new ModifyPowerHandler(),
                new CreateTokenHandler(),

                // 第一批补齐 — 伤害类
                new DamageCannotBePreventedHandler(),
                new DrainLifeHandler(),
                new PoisonousDamageHandler(),
                new RestoreToFullLifeHandler(),

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
                new RevealHandHandler(),

                // 第一批补齐 — 状态变更
                new ModifyLifeHandler(),
                new SetPowerHandler(),
                new SetLifeHandler(),
                new ModifyCostHandler(),
            };
            foreach (var handler in handlers)
                EffectHandlerRegistry.Register(handler);

            // 注册所有关键词授予处理器
            foreach (var handler in GrantKeywordHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册第二批规则原语处理器（状态/控制/反制/战斗/特殊）
            foreach (var handler in SecondBatchHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册第三批长尾处理器（牌库/移动/资源/无效化/复制等）
            foreach (var handler in ThirdBatchHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册元/合成效果处理器（重复/随机/选其一/延迟）
            foreach (var handler in MetaEffectHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册高级处理器（栈重定向/能力复制）
            foreach (var handler in AdvancedEffectHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册内置代价处理器
            BuiltinCostHandlers.RegisterAll();

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
                UnityEngine.Debug.LogError(
                    $"[BuiltinHandlerBootstrap] 缺失原子效果处理器 {missing.Count} 种：" +
                    string.Join(", ", missing));
            }
        }
    }

    #endregion
}
