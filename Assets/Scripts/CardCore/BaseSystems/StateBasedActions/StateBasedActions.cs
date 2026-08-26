using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 状态动作类型
    /// </summary>
    public enum SBAActionType
    {
        /// <summary>
        /// 生命值归零：实体进入坟墓
        /// </summary>
        ZeroToughness,

        /// <summary>
        /// 玩家生命值归零：游戏结束
        /// </summary>
        ZeroLife,

        /// <summary>
        /// 区域变更：区域离开触发
        /// </summary>
        ZoneChange,

        /// <summary>
        /// 文本变更：文本变化触发
        /// </summary>
        TextChange,

        /// <summary>
        /// 特征变更：特征变化触发
        /// </summary>
        CharacteristicChange
    }

    /// <summary>
    /// 状态记录
    /// </summary>
    public class SBAActionRecord
    {
        public SBAActionType Type { get; set; }
        public Entity AffectedEntity { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public DateTime CheckTime { get; set; }
        public bool Executed { get; set; }
    }

    /// <summary>
    /// 状态动作系统（SBA）
    /// 自动检查器，效果结算后和状态变化后执行检测
    /// 参考MTG：循环执行直到状态稳定
    /// </summary>
    public class StateBasedActions
    {
        private List<SBAActionRecord> _pendingActions = new List<SBAActionRecord>();
        private List<SBAActionRecord> _history = new List<SBAActionRecord>();
        private List<ISBAChecker> _checkers = new List<ISBAChecker>();
        private GameCore _gameCore;
        private int _stabilityCheckCount = 0;
        private const int MAX_STABILITY_CHECKS = 100; // 防止死循环

        /// <summary>
        /// 是否有待执行的状态动作
        /// </summary>
        public bool HasPendingActions => _pendingActions.Count > 0;

        /// <summary>
        /// 初始化SBA系统
        /// </summary>
        public void Initialize(GameCore gameCore)
        {
            _gameCore = gameCore;
        }

        /// <summary>
        /// 注册检查器
        /// </summary>
        public void RegisterChecker(ISBAChecker checker)
        {
            _checkers.Add(checker);
        }

        /// <summary>
        /// 添加状态动作到待处理列表
        /// </summary>
        public void AddAction(SBAActionRecord action)
        {
            // 检查是否已经存在相同的动作
            if (!_pendingActions.Any(a =>
                a.Type == action.Type &&
                a.AffectedEntity == action.AffectedEntity))
            {
                action.CheckTime = DateTime.Now;
                _pendingActions.Add(action);
            }
        }

        /// <summary>
        /// 执行所有待处理的状态动作
        /// </summary>
        public void ExecuteAll()
        {
            _stabilityCheckCount = 0;

            // 循环执行，直到状态稳定
            do
            {
                if (!CheckAndExecute())
                    break;

                _stabilityCheckCount++;

                // 防止死循环
                if (_stabilityCheckCount > MAX_STABILITY_CHECKS)
                {
                    break;
                }
            } while (HasPendingActions);
        }

        /// <summary>
        /// 检查并执行状态动作
        /// 返回是否执行了任何动作
        /// </summary>
        public bool CheckAndExecute()
        {
            // 调用所有注册的检查器
            foreach (var checker in _checkers)
            {
                checker.Check(this, _gameCore);
            }

            // 如果有待执行动作，执行并返回true
            if (_pendingActions.Count > 0)
            {
                ExecutePendingActions();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 执行待处理的状态动作
        /// </summary>
        private void ExecutePendingActions()
        {
            foreach (var action in _pendingActions)
            {
                ExecuteAction(action);
            }

            _pendingActions.Clear();
        }

        /// <summary>
        /// 执行单个状态动作
        /// </summary>
        private void ExecuteAction(SBAActionRecord action)
        {
            switch (action.Type)
            {
                case SBAActionType.ZeroToughness:
                    ExecuteZeroToughness(action);
                    break;
                case SBAActionType.ZeroLife:
                    ExecuteZeroLife(action);
                    break;
                case SBAActionType.ZoneChange:
                    ExecuteZoneChange(action);
                    break;
                case SBAActionType.TextChange:
                    ExecuteTextChange(action);
                    break;
                case SBAActionType.CharacteristicChange:
                    ExecuteCharacteristicChange(action);
                    break;
            }

            action.Executed = true;
            _history.Add(action);
        }

        /// <summary>
        /// 执行生命值归零动作
        /// </summary>
        private void ExecuteZeroLife(SBAActionRecord action)
        {
            var player = action.AffectedEntity as Player;
            if (player == null) return;

            // 触发游戏结束事件
            PublishEvent(new GameOverEvent
            {
                Winner = player.Opponent,
                Loser = player,
                Reason = GameOverReason.LifeZero
            });
        }

        /// <summary>
        /// 执行防御力归零动作（单位死亡）
        /// </summary>
        private void ExecuteZeroToughness(SBAActionRecord action)
        {
            var card = action.AffectedEntity as Card;
            if (card == null) return;

            card.IsAlive = false;

            // 移动到坟墓场
            _gameCore?.ZoneManager.MoveCard(card, _gameCore.GetCurrentTurnPlayer(), Zone.Battlefield, Zone.Graveyard);

            // 触发单位死亡事件
            PublishEvent(new CardDestroyEvent
            {
                DestroyedCard = card,
                Reason = DestroyReason.Combat,
                Source = null
            });
        }

        /// <summary>
        /// 执行区域变更动作：补发区域变更事件，使离开/进入区域触发式可观察。
        /// </summary>
        private void ExecuteZoneChange(SBAActionRecord action)
        {
            var card = action.AffectedEntity as Card;
            if (card == null) return;

            Zone oldZone = action.OldValue is Zone oz ? oz : Zone.None;
            Zone newZone = action.NewValue is Zone nz ? nz : Zone.None;

            PublishEvent(new CardZoneChangeEvent
            {
                Card = card,
                OldZone = oldZone,
                NewZone = newZone,
                Controller = card.GetController()
            });
        }

        /// <summary>
        /// 执行文本变更动作。当前数据模型无文本源，留空（对应 TextChangeChecker 的空实现）。
        /// </summary>
        private void ExecuteTextChange(SBAActionRecord action)
        {
        }

        /// <summary>
        /// 执行特征变化动作：+1/+1 与 -1/-1 指示物成对湮灭（MTG 704.5q）。
        /// 两类指示物在加入时已分别把 P/T 净改抵消，移除成对指示物对 P/T 无净影响，
        /// 仅需递减计数并补发移除事件。
        /// </summary>
        private void ExecuteCharacteristicChange(SBAActionRecord action)
        {
            var card = action.AffectedEntity as Card;
            if (card == null) return;

            int annihilate = Math.Min(card.GetCounterCount("+1/+1"), card.GetCounterCount("-1/-1"));
            if (annihilate <= 0) return;

            card.RemoveCounters("+1/+1", annihilate);
            card.RemoveCounters("-1/-1", annihilate);

            PublishEvent(new CounterEvent
            {
                Target = card,
                CounterType = "+1/+1",
                Amount = annihilate,
                IsAdd = false,
                Source = null
            });
            PublishEvent(new CounterEvent
            {
                Target = card,
                CounterType = "-1/-1",
                Amount = annihilate,
                IsAdd = false,
                Source = null
            });
        }

        /// <summary>
        /// 发布事件：优先经由 GameCore 统一路由（使 Trigger/Layer 引擎能观察到 SBA 产生的
        /// 销毁/区域/指示物事件），未注入时退化为直接走事件总线。
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            if (_gameCore != null)
                _gameCore.PublishEvent(e);
            else
                EventManager.Instance.Publish(e);
        }

        /// <summary>
        /// 清空历史记录
        /// </summary>
        public void ClearHistory()
        {
            _pendingActions.Clear();
            _history.Clear();
            _stabilityCheckCount = 0;
        }
    }

    /// <summary>
    /// 扩展方法：获取最近的状态变化
    /// </summary>
    public static class SBAExtensions
    {
        /// <summary>
        /// 获取最近的状态变化
        /// </summary>
        public static List<StateChangeEvent> GetRecentStateChanges(this List<StateChangeEvent> events, int count = 10)
        {
            return events.TakeLast(count).ToList();
        }
    }
}
