using System;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 游戏主循环控制器
    /// 负责每帧更新逻辑：优先权处理、栈结算、触发器收集、阶段推进
    /// </summary>
    public class GameLoopController
    {
        private readonly TurnEngine _turnEngine;
        private readonly StackEngine _stackEngine;
        private readonly TriggerEngine _triggerEngine;
        private readonly StateBasedActions _sbaEngine;
        private readonly LayerEngine _layerEngine;
        private readonly CombatSystem _combatSystem;
        private readonly ContinuousEffectDurationTracker _durationTracker;
        private readonly ControlChangeLayer _controlChangeLayer;
        private readonly TextChangeLayer _textChangeLayer;

        public GameLoopController(
            TurnEngine turnEngine,
            StackEngine stackEngine,
            TriggerEngine triggerEngine,
            StateBasedActions sbaEngine,
            LayerEngine layerEngine,
            CombatSystem combatSystem,
            ContinuousEffectDurationTracker durationTracker,
            ControlChangeLayer controlChangeLayer,
            TextChangeLayer textChangeLayer)
        {
            _turnEngine = turnEngine ?? throw new ArgumentNullException(nameof(turnEngine));
            _stackEngine = stackEngine ?? throw new ArgumentNullException(nameof(stackEngine));
            _triggerEngine = triggerEngine ?? throw new ArgumentNullException(nameof(triggerEngine));
            _sbaEngine = sbaEngine ?? throw new ArgumentNullException(nameof(sbaEngine));
            _layerEngine = layerEngine ?? throw new ArgumentNullException(nameof(layerEngine));
            _combatSystem = combatSystem ?? throw new ArgumentNullException(nameof(combatSystem));
            _durationTracker = durationTracker ?? throw new ArgumentNullException(nameof(durationTracker));
            _controlChangeLayer = controlChangeLayer ?? throw new ArgumentNullException(nameof(controlChangeLayer));
            _textChangeLayer = textChangeLayer ?? throw new ArgumentNullException(nameof(textChangeLayer));
        }

        /// <summary>
        /// 主循环 Update
        /// </summary>
        public void Update()
        {
            // 0. 结算进行中（异步原子效果可能在 await UI）→ 本帧不再驱动，避免重入
            if (_stackEngine.IsResolving)
                return;

            // 1. 检查并处理过期的持续效果
            _durationTracker.CheckAndCleanExpired();

            // 2. 处理过期的临时控制
            _controlChangeLayer.ProcessExpiredTemporaryControls();

            // 3. 检查文本修改
            _textChangeLayer.CheckTextModifications();

            // 4. 战斗阶段处理
            // 战斗系统有自己的状态机，这里不做额外处理

            // 5. 检查是否有可行动作（栈为空且处于可行动阶段）
            if (_stackEngine.IsEmpty && _turnEngine.CanActivateEffect())
            {
                // 6. 收集触发式并放入栈
                _triggerEngine.PutTriggersOnStack();

                // 7. 如果栈上有对象，处理优先权
                if (_stackEngine.StackSize > 0)
                {
                    ProcessPriority().Forget();
                }
                else
                {
                    // 8. 栈为空，尝试推进阶段
                    _turnEngine.CheckPhaseTransition();
                }
            }
        }

        /// <summary>
        /// 处理优先权。
        /// 自动 Pass 可能触发结算，而结算含异步原子效果（await UI）→ 本方法异步。
        /// </summary>
        public async UniTask ProcessPriority()
        {
            if (_stackEngine.IsEmpty)
                return;

            Player currentHolder = _stackEngine.CurrentPriorityHolder;
            bool hasAction = currentHolder != null && currentHolder.HasAvailableAction();

            if (hasAction)
                return; // 等待玩家决策

            // 没有可用动作，Pass优先权
            if (currentHolder != null)
                await _stackEngine.PassPriority(currentHolder);
        }

        /// <summary>
        /// 结算栈。
        /// 替代效果（Replacement）已在事件发布路径 GameCore.PublishEvent 处统一拦截，
        /// 不在此处重复检查。
        /// </summary>
        public async UniTask ResolveStack()
        {
            while (_stackEngine.StackSize > 0)
            {
                var top = _stackEngine.Peek();
                await _stackEngine.ResolveTop();

                if (top == null)
                    break;

                // 状态动作检查
                _sbaEngine.CheckAndExecute();

                // 收集触发式并放入栈
                _triggerEngine.PutTriggersOnStack();
            }

            // 栈清空后，检查状态稳定性
            _sbaEngine.ExecuteAll();
        }
    }
}
