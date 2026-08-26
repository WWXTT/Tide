using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 特征定义能力（CDA）记录
    /// </summary>
    public class CharacteristicDefiningAbilityRecord
    {
        public Entity Source { get; set; }
        public Effect SourceEffect { get; set; }
        public CharacteristicDefiningType DefinitionType { get; set; }
        public Entity DefinedEntity { get; set; }
        public object DefinedValue { get; set; }
        public DateTime CreationTime { get; set; }
        public int SequenceNumber { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// 特征定义能力系统
    /// 负责追踪那些定义卡牌特征的特殊能力
    /// CDAs具有最高优先级
    /// </summary>
    public class CharacteristicDefiningAbilities
    {
        private Dictionary<Entity, List<CharacteristicDefiningAbilityRecord>> _entityCDAs =
            new Dictionary<Entity, List<CharacteristicDefiningAbilityRecord>>();

        private Dictionary<Entity, Dictionary<CharacteristicDefiningType, CharacteristicDefiningAbilityRecord>> _activeCDAs =
            new Dictionary<Entity, Dictionary<CharacteristicDefiningType, CharacteristicDefiningAbilityRecord>>();

        /// <summary>
        /// 初始化特征定义能力系统
        /// </summary>
        public CharacteristicDefiningAbilities()
        {
        }

        /// <summary>
        /// 注册特征定义能力
        /// </summary>
        public void RegisterCDA(
            Entity source,
            Entity target,
            Effect sourceEffect,
            CharacteristicDefiningType type,
            object definedValue)
        {
            var record = new CharacteristicDefiningAbilityRecord
            {
                Source = source,
                SourceEffect = sourceEffect,
                DefinitionType = type,
                DefinedEntity = target,
                DefinedValue = definedValue,
                CreationTime = DateTime.Now,
                SequenceNumber = (int)TimestampSystem.NextSequence,
                IsActive = true
            };

            if (!_entityCDAs.ContainsKey(source))
            {
                _entityCDAs[source] = new List<CharacteristicDefiningAbilityRecord>();
            }
            _entityCDAs[source].Add(record);

            // 激活CDA
            ActivateCDA(target, type, record);
        }

        /// <summary>
        /// 注销特征定义能力
        /// </summary>
        public bool UnregisterCDA(Entity source, Entity target, CharacteristicDefiningType type)
        {
            if (!_entityCDAs.ContainsKey(source))
                return false;

            var toRemove = _entityCDAs[source]
                .Where(cda => cda.DefinedEntity == target && cda.DefinitionType == type)
                .ToList();

            if (toRemove.Count == 0)
                return false;

            foreach (var cda in toRemove)
            {
                cda.IsActive = false;

                // 从激活CDA中移除
                DeactivateCDA(target, type);
            }

            return true;
        }

        /// <summary>
        /// 激活CDA
        /// </summary>
        private void ActivateCDA(Entity target, CharacteristicDefiningType type, CharacteristicDefiningAbilityRecord record)
        {
            if (!_activeCDAs.ContainsKey(target))
            {
                _activeCDAs[target] = new Dictionary<CharacteristicDefiningType, CharacteristicDefiningAbilityRecord>();
            }

            _activeCDAs[target][type] = record;
        }

        /// <summary>
        /// 停用CDA
        /// </summary>
        private void DeactivateCDA(Entity target, CharacteristicDefiningType type)
        {
            if (_activeCDAs.ContainsKey(target) && _activeCDAs[target].ContainsKey(type))
            {
                _activeCDAs[target].Remove(type);

                if (_activeCDAs[target].Count == 0)
                {
                    _activeCDAs.Remove(target);
                }
            }
        }

        /// <summary>
        /// 获取实体的定义值
        /// </summary>
        public T GetDefinedValue<T>(Entity entity, CharacteristicDefiningType type)
        {
            if (!_activeCDAs.ContainsKey(entity) || !_activeCDAs[entity].ContainsKey(type))
            {
                return default;
            }

            var record = _activeCDAs[entity][type];
            if (record.DefinedValue is T value)
            {
                return value;
            }

            return default;
        }

        /// <summary>
        /// 检查实体是否有指定类型的CDA
        /// </summary>
        public bool HasCDA(Entity entity, CharacteristicDefiningType type)
        {
            return _activeCDAs.ContainsKey(entity) && _activeCDAs[entity].ContainsKey(type);
        }

        /// <summary>
        /// 获取实体的所有CDA
        /// </summary>
        public List<CharacteristicDefiningAbilityRecord> GetAllCDAs(Entity entity)
        {
            if (!_entityCDAs.ContainsKey(entity))
                return new List<CharacteristicDefiningAbilityRecord>();

            return _entityCDAs[entity].ToList();
        }

        /// <summary>
        /// 获取实体的所有激活CDA
        /// </summary>
        public Dictionary<CharacteristicDefiningType, CharacteristicDefiningAbilityRecord> GetActiveCDAs(Entity entity)
        {
            if (!_activeCDAs.ContainsKey(entity))
                return new Dictionary<CharacteristicDefiningType, CharacteristicDefiningAbilityRecord>();

            return _activeCDAs[entity];
        }

        /// <summary>
        /// 获取定义指定实体的所有源
        /// </summary>
        public List<Entity> GetDefiningSources(Entity target)
        {
            var result = new HashSet<Entity>();

            foreach (var kvp in _entityCDAs)
            {
                foreach (var cda in kvp.Value)
                {
                    if (cda.DefinedEntity == target)
                    {
                        result.Add(cda.Source);
                    }
                }
            }

            return result.ToList();
        }

        /// <summary>
        /// 获取指定类型的最高优先级CDA（最新时间戳）
        /// </summary>
        public CharacteristicDefiningAbilityRecord GetHighestPriorityCDA(Entity entity, CharacteristicDefiningType type)
        {
            if (!_activeCDAs.ContainsKey(entity) || !_activeCDAs[entity].ContainsKey(type))
                return null;

            return _activeCDAs[entity][type];
        }

        /// <summary>
        /// 检查是否存在CDA冲突（多个CDA定义同一特征）
        /// </summary>
        public bool HasCDACollision(Entity entity, CharacteristicDefiningType type)
        {
            if (!_entityCDAs.ContainsKey(entity))
                return false;

            var matchingCDAs = _entityCDAs[entity]
                .Where(cda => cda.DefinitionType == type && cda.IsActive)
                .ToList();

            return matchingCDAs.Count > 1;
        }

        /// <summary>
        /// 解决CDA冲突（根据时间戳，最新的生效）
        /// </summary>
        public void ResolveCDACollision(Entity entity, CharacteristicDefiningType type)
        {
            if (!_activeCDAs.ContainsKey(entity))
                return;

            var matchingCDAs = _entityCDAs[entity]
                .Where(cda => cda.DefinitionType == type && cda.IsActive)
                .ToList();

            if (matchingCDAs.Count <= 1)
                return;

            // 按时间戳排序，最新（最大序列号）的生效
            var latest = matchingCDAs
                .OrderByDescending(cda => cda.SequenceNumber)
                .First();

            // 停用其他CDA
            foreach (var cda in matchingCDAs)
            {
                if (cda != latest)
                {
                    cda.IsActive = false;
                }
            }

            // 只保留最新的CDA
            _activeCDAs[entity][type] = latest;

            // 通知特征因 CDA 冲突解决而被重新决定（最新时间戳的 CDA 生效）
            PublishEvent(new StateChangeEvent
            {
                Type = MapToStateChangeType(type),
                Target = entity,
                OldValue = null,
                NewValue = latest.DefinedValue
            });
        }

        /// <summary>
        /// 将 CDA 定义类型映射到状态变更类型。
        /// </summary>
        private static StateChangeType MapToStateChangeType(CharacteristicDefiningType type)
        {
            switch (type)
            {
                case CharacteristicDefiningType.DefinesColor: return StateChangeType.Color;
                case CharacteristicDefiningType.DefinesType: return StateChangeType.Type;
                case CharacteristicDefiningType.DefinesSubtype: return StateChangeType.Type;
                case CharacteristicDefiningType.DefinesStats: return StateChangeType.Power;
                default: return StateChangeType.Power;
            }
        }

        /// <summary>
        /// 检查并解决所有CDA冲突
        /// </summary>
        public void ResolveAllCDACollisions()
        {
            var allEntities = new HashSet<Entity>();

            foreach (var entity in _activeCDAs.Keys)
            {
                allEntities.Add(entity);
            }

            foreach (var entity in allEntities)
            {
                var cdas = _activeCDAs[entity];
                var grouped = cdas.GroupBy(cda => cda.Value.DefinitionType)
                    .Where(g => g.Count() > 1)
                    .ToList();

                foreach (var group in grouped)
                {
                    ResolveCDACollision(entity, group.Key);
                }
            }
        }

        /// <summary>
        /// 获取特征定义来源效果
        /// </summary>
        public Effect GetCDAEffect(Entity entity, CharacteristicDefiningType type)
        {
            if (!_activeCDAs.ContainsKey(entity) || !_activeCDAs[entity].ContainsKey(type))
                return null;

            return _activeCDAs[entity][type].SourceEffect;
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void ClearAll()
        {
            _entityCDAs.Clear();
            _activeCDAs.Clear();
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            // 优先经 GameCore 统一路由，使 Trigger/Layer 引擎能观察到 CDA 冲突解决事件
            if (GameCore.Instance != null)
                GameCore.Instance.PublishEvent(e);
            else
                EventManager.Instance.Publish(e);
        }
    }

    /// <summary>
    /// 特征定义能力扩展方法
    /// </summary>
    public static class CDAExtensions
    {
        /// <summary>
        /// 检查实体是否有特征定义
        /// </summary>
        public static bool HasCharacteristicDefinition(this Entity entity)
        {
            // TODO: 通过 CharacteristicDefiningAbilities 检查
            return false;
        }

        /// <summary>
        /// 检查实体是否有指定的特征定义
        /// </summary>
        public static bool HasCharacteristicDefinition<T>(this Entity entity) where T : IHasCharacteristic
        {
            // TODO: 实现
            return false;
        }
    }

    /// <summary>
    /// 特征接口（用于检查CDA）
    /// </summary>
    public interface IHasCharacteristic
    {
        // 标记接口，具体实现由 IHasColor, IHasType 等提供
    }
}
