using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 控制变更记录
    /// </summary>
    public class ControlChange
    {
        public Entity ChangedEntity { get; set; }
        public Player OriginalController { get; set; }
        public Player CurrentController { get; set; }
        public DateTime ChangeTime { get; set; }
        public Effect Source { get; set; }
        public uint SequenceNumber { get; set; }
        public bool IsTemporary { get; set; } // 是否为临时控制
        public DateTime? EndTime { get; set; } // 临时控制的结束时间
    }

    /// <summary>
    /// 控制变更层
    /// 负责追踪控制权变更历史和控制权来源
    /// </summary>
    public class ControlChangeLayer
    {
        private Dictionary<Entity, Stack<ControlChange>> _controlHistory =
            new Dictionary<Entity, Stack<ControlChange>>();

        private Dictionary<Player, HashSet<Entity>> _controlledEntities =
            new Dictionary<Player, HashSet<Entity>>();

        /// <summary>
        /// 初始化控制变更层
        /// </summary>
        public ControlChangeLayer()
        {
        }

        /// <summary>
        /// 初始化玩家
        /// </summary>
        public void InitializePlayer(Player player)
        {
            if (!_controlledEntities.ContainsKey(player))
            {
                _controlledEntities[player] = new HashSet<Entity>();
            }
        }

        /// <summary>
        /// 更改实体控制权
        /// </summary>
        public bool ChangeControl(Entity entity, Player newController, Effect source, bool isTemporary = false, DateTime? endTime = null)
        {
            Player currentController = GetController(entity);

            // 相同控制者，无需变更
            if (currentController == newController)
                return false;

            // 从原控制者的控制集中移除
            if (currentController != null && _controlledEntities.ContainsKey(currentController))
            {
                _controlledEntities[currentController].Remove(entity);
            }

            // 添加到新控制者的控制集中
            if (!_controlledEntities.ContainsKey(newController))
            {
                _controlledEntities[newController] = new HashSet<Entity>();
            }
            _controlledEntities[newController].Add(entity);

            // 记录控制变更
            var change = new ControlChange
            {
                ChangedEntity = entity,
                OriginalController = currentController,
                CurrentController = newController,
                ChangeTime = DateTime.Now,
                Source = source,
                SequenceNumber = TimestampSystem.NextSequence,
                IsTemporary = isTemporary,
                EndTime = endTime
            };

            if (!_controlHistory.ContainsKey(entity))
            {
                _controlHistory[entity] = new Stack<ControlChange>();
            }
            _controlHistory[entity].Push(change);

            // 触发控制权变更事件
            PublishEvent(new ControlChangeEvent
            {
                ChangedEntity = entity,
                OldController = currentController,
                NewController = newController,
                Source = source
            });

            return true;
        }

        /// <summary>
        /// 恢复控制权（临时控制结束时）
        /// </summary>
        public bool RestoreControl(Entity entity)
        {
            if (!_controlHistory.ContainsKey(entity))
                return false;

            var stack = _controlHistory[entity];
            if (stack.Count < 2)
                return false;

            // 弹出当前控制变更
            stack.Pop();

            // 获取上一个控制变更
            var previousChange = stack.Peek();

            // 恢复控制权
            Player previousController = previousChange.CurrentController;

            // 从当前控制者移除
            var currentController = GetController(entity);
            if (currentController != null && _controlledEntities.ContainsKey(currentController))
            {
                _controlledEntities[currentController].Remove(entity);
            }

            // 添加回上一个控制者
            if (!_controlledEntities.ContainsKey(previousController))
            {
                _controlledEntities[previousController] = new HashSet<Entity>();
            }
            _controlledEntities[previousController].Add(entity);

            // 触发控制权变更事件
            PublishEvent(new ControlChangeEvent
            {
                ChangedEntity = entity,
                OldController = currentController,
                NewController = previousController,
                Source = previousChange.Source
            });

            return true;
        }

        /// <summary>
        /// 获取实体当前控制者
        /// </summary>
        public Player GetController(Entity entity)
        {
            if (!_controlHistory.ContainsKey(entity))
                return null;

            var stack = _controlHistory[entity];
            if (stack.Count == 0)
                return null;

            return stack.Peek().CurrentController;
        }

        /// <summary>
        /// 获取控制变更历史
        /// </summary>
        public List<ControlChange> GetControlHistory(Entity entity)
        {
            if (!_controlHistory.ContainsKey(entity))
                return new List<ControlChange>();

            return _controlHistory[entity].Reverse().ToList();
        }

        /// <summary>
        /// 获取控制权来源效果
        /// </summary>
        public Effect GetControlSource(Entity entity)
        {
            if (!_controlHistory.ContainsKey(entity))
                return null;

            return _controlHistory[entity].Peek().Source;
        }

        /// <summary>
        /// 获取原始控制者（最初的控制者）
        /// </summary>
        public Player GetOriginalController(Entity entity)
        {
            if (!_controlHistory.ContainsKey(entity))
                return null;

            var allChanges = _controlHistory[entity].Reverse().ToList();
            return allChanges.LastOrDefault()?.OriginalController;
        }

        /// <summary>
        /// 检查玩家是否控制指定实体
        /// </summary>
        public bool DoesPlayerControl(Player player, Entity entity)
        {
            if (!_controlledEntities.ContainsKey(player))
                return false;

            return _controlledEntities[player].Contains(entity);
        }

        /// <summary>
        /// 获取玩家控制的所有实体
        /// </summary>
        public List<Entity> GetControlledEntities(Player player)
        {
            if (!_controlledEntities.ContainsKey(player))
                return new List<Entity>();

            return _controlledEntities[player].ToList();
        }

        /// <summary>
        /// 检查临时控制是否已结束
        /// </summary>
        public List<Entity> GetExpiredTemporaryControls()
        {
            var result = new List<Entity>();

            foreach (var kvp in _controlHistory)
            {
                var entity = kvp.Key;
                var currentChange = kvp.Value.Peek();

                if (currentChange.IsTemporary && currentChange.EndTime.HasValue)
                {
                    if (DateTime.Now >= currentChange.EndTime.Value)
                    {
                        result.Add(entity);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 处理所有过期的临时控制
        /// </summary>
        public void ProcessExpiredTemporaryControls()
        {
            var expired = GetExpiredTemporaryControls();

            foreach (var entity in expired)
            {
                RestoreControl(entity);
            }
        }

        /// <summary>
        /// 获取控制时间戳（用于依赖解析）
        /// </summary>
        public uint GetControlTimestamp(Entity entity)
        {
            if (!_controlHistory.ContainsKey(entity))
                return 0;

            return _controlHistory[entity].Peek().SequenceNumber;
        }

        /// <summary>
        /// 比较两个实体的控制时间戳
        /// 更老的（先控制的）优先
        /// </summary>
        public int CompareControlTimestamps(Entity a, Entity b)
        {
            if (!_controlHistory.ContainsKey(a) || !_controlHistory.ContainsKey(b))
                return 0;

            uint timestampA = GetControlTimestamp(a);
            uint timestampB = GetControlTimestamp(b);

            return timestampB.CompareTo(timestampA); // 更大的时间戳表示更老
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void ClearAll()
        {
            _controlHistory.Clear();
            _controlledEntities.Clear();
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            EventManager.Instance.Publish(e);
        }
    }

    /// <summary>
    /// 控制变更层扩展方法
    /// </summary>
    public static class ControlChangeExtensions
    {
        /// <summary>
        /// 检查实体是否由指定玩家控制
        /// </summary>
        public static bool IsControlledBy(this Entity entity, Player player)
        {
            // TODO: 通过 ControlChangeLayer 检查
            return false;
        }

        /// <summary>
        /// 检查实体是否由对手控制
        /// </summary>
        public static bool IsControlledByOpponent(this Entity entity, Player currentPlayer)
        {
            if (currentPlayer == null || currentPlayer.Opponent == null)
                return false;

            // TODO: 通过 ControlChangeLayer 检查
            return false;
        }
    }
}
