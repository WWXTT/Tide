using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;

namespace CardCore
{
    public partial class StackEngine
    {
        /// <summary>
        /// 玩家 Pass（让出优先权）。
        /// 双方连续 Pass 触发结算，结算可能 await UI（目标选择弹窗）。
        /// </summary>
        public async UniTask PlayerPass(Player player)
        {
            if (player != _priorityHolder) return;

            _consecutivePassCount++;
            GameActions.Crumb($"CF-pass by={player?.Name} count={_consecutivePassCount} begin={_consecutivePassCount >= 2}");

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
            GameActions.Crumb($"CF-voluntary-requeue def={chosen.Effect?.Id} speed={chosen.ActivationSpeed} ctr={_speedCounter.CurrentSpeed} active={chosen.Controller == _activePlayer}");
            _pendingQueue.AddPendingEffect(chosen);
            return false;
        }

        /// <summary>
        /// 获取可发动的速度效果列表（2026-10-18 定案：门槛=全严格大于；空栈首动免门槛——
        /// 主阶段空栈发动 0 速可选触发靠此豁免，stackEmpty 由本引擎真值传入）
        /// </summary>
        public List<PendingEffect> GetActivatableVoluntaryEffects(Player activator)
        {
            return _pendingQueue.GetVoluntaryEffects(_stack.Count == 0);
        }

        /// <summary>
        /// 开始结算
        /// </summary>
        private async UniTask BeginResolution()
        {
            GameActions.Crumb($"CF-res-begin stack={_stack.Count} items={string.Join(",", _stack.Select(DescribeStackObject))}");
            _speedCounter.BeginResolution();
            _waitingForPlayer = false;

            // 真连锁判定（2026-11-04 连锁日志定案）：结算开始时栈上真实对象 ≥2 =有人跟了连锁。
            // 阶段窗伪对象不计——主阶段窗下出牌栈=[阶段窗,卡]，StackSize=2 但只是普通处理。
            int realCount = _stack.Count(it => !it.IsPhaseWindow);

            EventManager.Instance.Publish(new StackResolutionStartEvent
            {
                StackSize = _stack.Count,
                RealCount = realCount,
                TopItem = _stack.Count > 0 ? _stack.Peek() : null // LIFO 首结算项（2026-11-03 连锁日志「逆处理」行）
            });

            await ResolveStack(realCount);
        }

        /// <summary>
        /// 结算栈（LIFO）
        /// 结算中不轮询，不插入任何效果
        /// 结算期间产生的新条件触发效果存入延迟队列。
        /// 原子效果异步执行：交互效果可 await UI，期间 IsResolving=true 阻止主循环重入。
        /// </summary>
        private async UniTask ResolveStack(int realCountAtStart)
        {
            _isResolving = true;
            try
            {
                int guardResolve = 0;
                bool resolvedRealItems = false; // 本次结算是否含真实栈对象（2026-11-03 连锁日志——纯阶段窗排干不出「连锁结束」行）
                while (_stack.Count > 0)
                {
                    var top = _stack.Pop();
                    GameActions.Crumb($"resolve top #{guardResolve} cast={top.IsCardCast} sba={top.IsSBA} id={top.Definition?.Id ?? (top.Source as Card)?.ID ?? "?"}");
                    if (++guardResolve > 200) { GameActions.Crumb("resolve GUARD-200 break"); break; }
                    if (!top.IsPhaseWindow) resolvedRealItems = true; // 阶段窗伪对象=窗口折返非连锁
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
                    else if (top.IsPhaseWindow)
                    {
                        // 0 速阶段窗伪对象（2026-11-01）：到点自动消失——不结算、不重查、不发事件。
                        // 窗口本身（双方 Pass 让路）已在上面的双 Pass 流程完成；阶段推进在窗口外
                        //（窗①=GameActions.EndTurn 续链、窗②=帧泵/驱动器折返口）。
                        GameActions.Crumb("CF-phase-window-vanish");
                    }
                    else if (top.IsGuardDeclaration)
                        GameCore.Instance?.CombatSystem?.ResolveGuardDeclaration(top);
                    else if (top.IsAttackDeclaration)
                        GameCore.Instance?.CombatSystem?.ResolveAttackDeclaration(top);
                    else if (top.IsChargeDeclaration)
                        await GameActions.ResolveChargeDeclarationAsync(top);
                    else if (top.IsCardCast)
                        await GameActions.ResolveCardCastAsync(top);
                    else
                        await _executor.ExecuteAsync(top);
                    // 2026-09-13 定案：记速器=本连锁最高速度，逐个结算不递减——归 0 统一在 FinishResolution

                    EventManager.Instance.Publish(new StackResolutionEndEvent
                    {
                        StackSize = _stack.Count,
                        ResolvedItem = top // 2026-10-19 逆结算步进：表现层按步回放的步载荷
                    });
                }

                await FinishResolution(resolvedRealItems, realCountAtStart);
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
        private async UniTask FinishResolution(bool resolvedRealItems, int realCountAtStart)
        {
            _speedCounter.Reset();
            _waitingForPlayer = false;

            EventManager.Instance.Publish(new StackEmptyEvent
            {
                LastPriorityHolder = _priorityHolder,
                HadRealItems = resolvedRealItems, // 2026-11-03 连锁日志：纯阶段窗排干不出「连锁结束」行
                WasChain = realCountAtStart >= 2 // 2026-11-04 连锁日志定案：「连锁结束」只在真连锁窗口出——单件窗口是普通处理无收尾行
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
                // 取证探针（2026-11-05 SBA 全灭排查）：本行只在「本轮走到 SBA 检查点」时出现在
                // Logs/EngineCrumb.txt——文件里没有 `[SBA] check` = 循环在更早的分支 return 了；
                // 有行但 crit=False = checker 没产出 ZeroToughness/ZeroLife 记录。
                bool sbaCritical = core.SBAEngine.HasCriticalPendingAfterCheck();
                GameActions.Crumb($"[SBA] check crit={sbaCritical} stack={_stack.Count}");
                if (sbaCritical)
                {
                    GameActions.Crumb("[SBA] critical pending → push 栈对象"); // 取证：检出了没
                    core.SBAEngine.ClearCriticalPending();
                    PushStateAction();
                    return; // SBA 轮：双 Pass → BeginResolution → 消费 → 回到这里再查触发式/SBA
                }

                core.SBAEngine.ExecuteAll();   // 无关键 SBA：记账型照旧自发执行（ZoneChange 事件补发等）
                core.CheckLifeGameOver();      // 兜底（关键判负已走 SBA 轮；幂等）
                if (core.IsGameOver) return;

                if (!_pendingQueue.HasAutoEffects)
                {
                    // 效果结算后 flush（2026-11-06 判据七·充盈改造）：连锁终点稳定点统一补发——
                    // 充盈奖励（直发）与 CardCastSettledEvent 观察族触发可能产生新待发/关键 SBA，
                    // continue 再走一轮（guard 16 兜底）；无待补发才算真稳定。
                    if (BranchEngines.HasPendingCastSettles)
                    {
                        GameActions.Crumb("[CastSettled] flush at chain end");
                        BranchEngines.FlushCardCastSettled();
                        continue;
                    }
                    break; // 稳定：无新动作、无新触发
                }
            }
        }

        public void OnTurnStart(Player newTurnPlayer)
        {
            _activePlayer = newTurnPlayer;
            _priorityHolder = newTurnPlayer;
            _consecutivePassCount = 0;
            _waitingForPlayer = false;
            _chargedThisTurn.Clear(); // 充能每回合规额清零（2026-11-05：一张卡一回合只能发动一次）
        }

        public void Clear()
        {
            _stack.Clear();
            _speedCounter.Reset();
            _pendingQueue.Clear();
            _chargedThisTurn.Clear();
            _waitingForPlayer = false;
            _consecutivePassCount = 0;
        }

        /// <summary>CF 取证：栈对象一行描述（类型+身份，自顶向下）。</summary>
        private static string DescribeStackObject(EffectInstance o)
            => o == null ? "null"
             : o.IsAttackDeclaration ? $"ATK:{(o.Source as Card)?.ID ?? o.Source?.ToString()}"
             : o.IsGuardDeclaration ? $"GRD:{(o.Source as Card)?.ID ?? o.Source?.ToString()}"
             : o.IsChargeDeclaration ? $"CHG:{(o.Source as Card)?.ID}"
             : o.IsCardCast ? $"CAST:{(o.Source as Card)?.ID}"
             : o.IsSBA ? "SBA"
             : o.IsPhaseWindow ? "PHASE-WIN"
             : $"{o.Definition?.Id ?? "?"}";

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
                    else if (top.IsChargeDeclaration)
                        await GameActions.ResolveChargeDeclarationAsync(top);
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

        /// <summary>栈上是否还有 SBA 伪对象（2026-11-05：SBA 救回窗口判定——GetStackContents 会滤掉
        /// 伪对象，不能用来判窗口；这里直查原始栈）。</summary>
        public bool HasPendingSba()
        {
            foreach (var o in _stack)
                if (o != null && o.IsSBA) return true;
            return false;
        }

        /// <summary>
        /// 重定向栈顶效果的目标（RedirectTarget 原子效果使用）。
        /// 栈顶无对象时返回 false。
        /// </summary>
        public bool RetargetTopStackObject(List<Entity> newTargets)
        {
            if (_stack.Count == 0 || newTargets == null) return false;
            _stack.Peek().Targets = new List<Entity>(newTargets);
            return true;
        }

        public EffectExecutor GetExecutor() => _executor;
    }
}
