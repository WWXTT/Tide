using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 持续效果记录
    /// </summary>
    public class ContinuousEffectEntry
    {
        public Effect SourceEffect { get; set; }
        public Entity SourceEntity { get; set; }
        public Entity TargetEntity { get; set; } // 可以为空（全局效果）
        public DurationType Duration { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public object EndCondition { get; set; } // 条件结束时失效
        public int? ExpiryTurn { get; set; } // UntilNextTurn/ForTurns 的到期全局回合戳（该回合 TurnEnd 时失效）
        public List<Entity> AffectedTargets { get; set; } = new List<Entity>();
        public uint SequenceNumber { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 检查效果是否仍然有效
        /// </summary>
        public bool IsStillValid()
        {
            if (!IsActive)
                return false;

            // 检查条件是否满足
            if (Duration == DurationType.WhileCondition && !CheckCondition())
                return false;

            // 检查时间是否到期
            if (EndTime.HasValue && DateTime.Now >= EndTime.Value)
                return false;

            // 检查目标是否仍然在场
            if (TargetEntity != null && !IsEntityInPlay(TargetEntity))
                return false;

            return true;
        }

        /// <summary>
        /// 检查条件
        /// EndCondition 约定为「结束谓词」Func&lt;bool&gt;：返回 true 表示结束条件已满足 → 效果失效。
        /// 无显式谓词时视为条件持续成立。
        /// </summary>
        private bool CheckCondition()
        {
            if (EndCondition is Func<bool> endPredicate)
                return !endPredicate();
            return true;
        }

        /// <summary>
        /// 检查实体是否在场（位于其控制者的战场区域）
        /// </summary>
        private bool IsEntityInPlay(Entity entity)
        {
            if (entity == null) return false;

            // 非卡牌实体（如玩家）无战场归属，退化为存活判定
            if (!(entity is Card card))
                return entity.IsAlive;

            var controller = card.GetController();
            var zm = GameCore.Instance?.ZoneManager;
            if (controller == null || zm == null)
                return card.IsAlive;

            return zm.IsCardInZone(card, controller, Zone.Battlefield);
        }
    }

    /// <summary>
    /// 持续效果时长追踪系统
    /// 负责追踪持续效果的时长，处理永久/直到/只要等持续时间类型
    /// </summary>
    public class ContinuousEffectDurationTracker
    {
        private List<ContinuousEffectEntry> _effects = new List<ContinuousEffectEntry>();

        /// <summary>
        /// 所有追踪的持续效果
        /// </summary>
        public List<ContinuousEffectEntry> AllEffects => _effects;

        /// <summary>
        /// 所有活跃的效果
        /// </summary>
        public List<ContinuousEffectEntry> ActiveEffects => _effects.Where(e => e.IsActive).ToList();

        /// <summary>
        /// 初始化持续效果时长追踪
        /// </summary>
        public ContinuousEffectDurationTracker()
        {
        }

        /// <summary>
        /// 添加持续效果
        /// </summary>
        public bool AddEffect(
            Effect sourceEffect,
            Entity sourceEntity,
            Entity targetEntity,
            DurationType duration,
            object endCondition = null,
            int durationTurns = 0)
        {
            var entry = new ContinuousEffectEntry
            {
                SourceEffect = sourceEffect,
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Duration = duration,
                StartTime = DateTime.Now,
                EndTime = CalculateEndTime(duration),
                EndCondition = endCondition,
                ExpiryTurn = ComputeExpiryTurn(duration, durationTurns),
                SequenceNumber = TimestampSystem.NextSequence,
                IsActive = true
            };

            _effects.Add(entry);

            // 触发效果添加事件
            PublishEvent(new EffectActivatedEvent
            {
                ActivatedEffect = sourceEffect,
                Activator = GetEffectController(sourceEffect),
                Targets = targetEntity != null ? new List<Entity> { targetEntity } : null
            });

            return true;
        }

        /// <summary>
        /// 添加指定目标的持续效果
        /// </summary>
        public bool AddEffectWithTargets(
            Effect sourceEffect,
            Entity sourceEntity,
            List<Entity> targets,
            DurationType duration,
            object endCondition = null,
            int durationTurns = 0)
        {
            var entry = new ContinuousEffectEntry
            {
                SourceEffect = sourceEffect,
                SourceEntity = sourceEntity,
                TargetEntity = null,
                Duration = duration,
                StartTime = DateTime.Now,
                EndTime = CalculateEndTime(duration),
                EndCondition = endCondition,
                ExpiryTurn = ComputeExpiryTurn(duration, durationTurns),
                SequenceNumber = TimestampSystem.NextSequence,
                IsActive = true,
                AffectedTargets = targets ?? new List<Entity>()
            };

            _effects.Add(entry);

            // 触发效果添加事件
            PublishEvent(new EffectActivatedEvent
            {
                ActivatedEffect = sourceEffect,
                Activator = GetEffectController(sourceEffect),
                Targets = targets
            });

            return true;
        }

        /// <summary>
        /// 移除持续效果
        /// </summary>
        public bool RemoveEffect(Effect effect, Entity targetEntity = null)
        {
            var toRemove = _effects
                .Where(e => e.SourceEffect == effect &&
                              (targetEntity == null || e.TargetEntity == targetEntity))
                .ToList();

            if (toRemove.Count == 0)
                return false;

            foreach (var entry in toRemove)
            {
                entry.IsActive = false;
            }

            // 触发效果结束事件
            foreach (var entry in toRemove)
            {
                PublishEvent(new EffectResolveEvent
                {
                    ResolvedEffect = effect,
                    Context = new EffectResolutionContext() // TODO: 需要完善上下文
                });
            }

            return true;
        }

        /// <summary>
        /// 计算结束时间。
        /// 本引擎为回合制：UntilEndOfTurn / UntilLeaveBattlefield 的失效由
        /// <see cref="OnTurnEnd"/> / <see cref="OnPhaseEnd"/> 等回合/阶段事件驱动，
        /// 而非挂钟时间（DateTime）。因此除非将来引入真实计时效果，
        /// 这些时长均返回 null（无挂钟到期时间），由事件钩子负责失效。
        /// </summary>
        private DateTime? CalculateEndTime(DurationType duration)
        {
            // 所有时长均不使用挂钟到期：永久=无到期；回合/阶段=事件驱动；条件=谓词驱动。
            return null;
        }

        /// <summary>
        /// 更新效果持续时间
        /// </summary>
        public bool UpdateDuration(Effect effect, DurationType newDuration, object newEndCondition = null)
        {
            var entry = _effects.FirstOrDefault(e =>
                e.SourceEffect == effect && e.IsActive);

            if (entry == null)
                return false;

            entry.Duration = newDuration;
            entry.EndCondition = newEndCondition;
            entry.EndTime = CalculateEndTime(newDuration);

            return true;
        }

        /// <summary>
        /// 检查并清理过期效果
        /// </summary>
        public void CheckAndCleanExpired()
        {
            var expired = new List<ContinuousEffectEntry>();

            foreach (var effect in _effects)
            {
                if (!effect.IsStillValid())
                {
                    expired.Add(effect);
                }
            }

            foreach (var effect in expired)
            {
                effect.IsActive = false;

                // 触发效果结束事件
                PublishEvent(new EffectResolveEvent
                {
                    ResolvedEffect = effect.SourceEffect,
                    Context = new EffectResolutionContext()
                });
            }
        }

        /// <summary>
        /// 阶段结束时清理对应效果
        /// </summary>
        public void OnPhaseEnd(PhaseType phase)
        {
            foreach (var effect in _effects)
            {
                if (effect.Duration == DurationType.UntilLeaveBattlefield && effect.IsActive)
                {
                    effect.IsActive = false;

                    // 触发效果结束事件
                    PublishEvent(new EffectResolveEvent
                    {
                        ResolvedEffect = effect.SourceEffect,
                        Context = new EffectResolutionContext()
                    });
                }
            }
        }

        /// <summary>
        /// 回合开始：推进全局回合计数（GameCore.OnTurnStarted 推送）。
        /// UntilNextTurn / ForTurns 的到期戳以创建时刻的当前全局回合数为基准。
        /// </summary>
        public void OnTurnStart(int globalTurn)
        {
            if (globalTurn > _currentGlobalTurn)
                _currentGlobalTurn = globalTurn;
        }

        private int _currentGlobalTurn;

        /// <summary>
        /// 计算到期全局回合戳（回合单位＝玩家回合，与全局回合数同源）：
        /// UntilNextTurn 于创建回合 +1（对手回合）结束失效；
        /// ForTurns(N) 创建回合记第 1 回合，于第 N 个回合结束失效（N=1 同 UntilEndOfTurn、N=2 同 UntilNextTurn）。
        /// 其余档位返回 null（事件/条件/离场驱动）。
        /// </summary>
        private int? ComputeExpiryTurn(DurationType duration, int turns)
        {
            switch (duration)
            {
                case DurationType.UntilNextTurn:
                    return _currentGlobalTurn + 1;
                case DurationType.ForTurns:
                    return _currentGlobalTurn + Math.Max(1, turns) - 1;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 回合结束时清理对应效果
        /// </summary>
        public void OnTurnEnd(Player player, int globalTurn = 0)
        {
            foreach (var effect in _effects)
            {
                if (!effect.IsActive)
                    continue;

                if (effect.Duration == DurationType.UntilEndOfTurn)
                {
                    // 仅结束归属于该回合玩家的「直到回合结束」效果
                    var controller = GetEffectController(effect.SourceEffect)
                        ?? effect.SourceEntity?.GetController();
                    if (player != null && controller != null && controller != player)
                        continue;

                    Expire(effect);
                }
                else if ((effect.Duration == DurationType.UntilNextTurn || effect.Duration == DurationType.ForTurns)
                         && effect.ExpiryTurn.HasValue
                         && globalTurn > 0
                         && globalTurn >= effect.ExpiryTurn.Value)
                {
                    // 到期戳失效：跨回合持续档在 stamped 回合的 TurnEnd 统一失效（不区分归属玩家）
                    Expire(effect);
                }
            }
        }

        private void Expire(ContinuousEffectEntry effect)
        {
            effect.IsActive = false;

            // 触发效果结束事件
            PublishEvent(new EffectResolveEvent
            {
                ResolvedEffect = effect.SourceEffect,
                Context = new EffectResolutionContext()
            });
        }

        /// <summary>
        /// 获取指定实体的所有效果
        /// </summary>
        public List<ContinuousEffectEntry> GetEffectsForEntity(Entity entity)
        {
            return _effects
                .Where(e => e.IsActive && (e.TargetEntity == entity || e.SourceEntity == entity))
                .ToList();
        }

        /// <summary>
        /// 获取指定来源的所有效果
        /// </summary>
        public List<ContinuousEffectEntry> GetEffectsFromSource(Effect effect)
        {
            return _effects
                .Where(e => e.IsActive && e.SourceEffect == effect)
                .ToList();
        }

        /// <summary>
        /// 检查实体是否受指定效果影响
        /// </summary>
        public bool IsAffectedBy(Effect effect, Entity entity)
        {
            return _effects.Any(e =>
                e.IsActive &&
                e.SourceEffect == effect &&
                e.AffectedTargets.Contains(entity));
        }

        /// <summary>
        /// 获取效果控制者
        /// </summary>
        private Player GetEffectController(Effect effect)
        {
            return null;
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void ClearAll()
        {
            _effects.Clear();
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            // 优先经 GameCore 统一路由，使 Trigger/Layer 引擎能观察到持续效果生命周期事件
            if (GameCore.Instance != null)
                GameCore.Instance.PublishEvent(e);
            else
                EventManager.Instance.Publish(e);
        }
    }

    /// <summary>
    /// 持续效果时长追踪扩展方法
    /// </summary>
    public static class DurationExtensions
    {
        /// <summary>
        /// 检查实体是否有指定持续效果
        /// </summary>
        public static bool HasEffectType(this Entity entity, Effect effect)
        {
            // TODO: 通过 DurationTracker 检查
            return false;
        }

        /// <summary>
        /// 获取实体受影响的所有效果
        /// </summary>
        public static List<Effect> GetActiveEffects(this Entity entity, ContinuousEffectDurationTracker tracker)
        {
            if (tracker == null)
                return new List<Effect>();

            return tracker.GetEffectsForEntity(entity)
                .Where(e => e.IsActive)
                .Select(e => e.SourceEffect)
                .ToList();
        }
    }
}
