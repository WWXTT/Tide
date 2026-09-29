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
                    // 8.（2026-09-24 回合推进口径收敛）**只做 End→Standby 折返**，且等回合末
                    //    异步链（手牌上限弃牌等反问）收口——旧"空栈即自由滑当前阶段"会把主要
                    //    阶段滑进结束（同进程双驱动时整局相位空转、每圈多抽一张牌）；
                    //    准备→主已由引擎自动推进（TurnEngine.StartNewTurn），主→结束仅 IntentEndTurn。
                    if (_turnEngine.CurrentPhase?.Phase == PhaseType.End
                        && CardCore.TargetSelectionService.PendingRequests == 0)
                    {
                        _turnEngine.CheckPhaseTransition();
                    }
                }
            }
        }

        /// <summary>
        /// 处理优先权。
        /// 自动 Pass 可能触发结算，而结算含异步原子效果（await UI）→ 本方法异步。
        /// 2026-09-16：旧 HasAvailableAction 桩（手牌含效果卡即等待——假阳性干等）替换为
        /// 响应窗口收集口（GameActions.CollectAvailableResponses：速度门+费用+代价双过滤）——
        /// 有真实候选 → 等待决策（UI/AI 泵驱动）；0 候选 → 自动 Pass（跳过弹窗口径）。
        /// </summary>
        public async UniTask ProcessPriority()
        {
            if (_stackEngine.IsEmpty)
                return;

            Player currentHolder = _stackEngine.CurrentPriorityHolder;
            bool hasAction = currentHolder != null
                && GameActions.CollectAvailableResponses(GameCore.Instance, currentHolder).Count > 0;

            if (hasAction)
                return; // 等待决策（响应窗口泵/弹窗）

            // 没有可用候选，Pass优先权
            if (currentHolder != null)
                await _stackEngine.PassPriority(currentHolder);
        }

        /// <summary>
        /// 结算栈。
        /// 替代效果（Replacement）已在事件发布路径 GameCore.PublishEvent 处统一拦截，
        /// 不在此处重复检查。
        /// ⚠ 零调用预留路径：现行活路径中 SBA 是速度1栈对象、经双 Pass 在
        /// StackEngine.ResolveStack 的 IsSBA 分支结算（见 FinishResolution/PushStateAction）；
        /// 本方法若将来接线需补 IsSBA 消费分支，且每步直调 CheckAndExecute 的语义已过时。
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
