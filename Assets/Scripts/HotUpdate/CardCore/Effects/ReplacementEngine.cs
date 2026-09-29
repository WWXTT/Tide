using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 替代效果接口
    /// 可以拦截和修改事件的效果
    /// </summary>
    public interface IReplacementEffect : IEffect
    {
        /// <summary>
        /// 替代事件类型
        /// </summary>
        Type ReplacedEventType { get; }

        /// <summary>
        /// 检查是否可以替代此事件
        /// </summary>
        bool CanReplace(IGameEvent e);

        /// <summary>
        /// 创建替代事件
        /// </summary>
        IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect);
    }

    /// <summary>
    /// 替代上下文
    /// </summary>
    public class ReplacementContext
    {
        public IGameEvent OriginalEvent { get; set; }
        public IGameEvent ReplacementEvent { get; set; }
        public List<ReplacementApplication> AppliedReplacements { get; set; }
            = new List<ReplacementApplication>();

        /// <summary>
        /// 是否已被替代
        /// </summary>
        public bool IsReplaced => ReplacementEvent != null;

        /// <summary>
        /// 获取最终事件（可能是原事件或替代事件）
        /// </summary>
        public IGameEvent GetFinalEvent()
        {
            return ReplacementEvent ?? OriginalEvent;
        }
    }

    /// <summary>
    /// 替代应用记录
    /// </summary>
    public class ReplacementApplication
    {
        public Effect SourceEffect { get; set; }
        public IGameEvent ReplacedEvent { get; set; }
        public IGameEvent ReplacementEvent { get; set; }
        public DateTime ApplicationTime { get; set; }
    }

    /// <summary>
    /// 替代引擎
    /// 在事件发生前拦截和处理替代
    /// </summary>
    public class ReplacementEngine
    {
        private Dictionary<Type, List<IReplacementEffect>> _replacementEffects =
            new Dictionary<Type, List<IReplacementEffect>>();

        /// <summary>
        /// 注册替代效果
        /// </summary>
        public void RegisterReplacementEffect(IReplacementEffect effect, Type eventType)
        {
            if (!_replacementEffects.ContainsKey(eventType))
            {
                _replacementEffects[eventType] = new List<IReplacementEffect>();
            }

            _replacementEffects[eventType].Add(effect);
        }

        /// <summary>
        /// 注销替代效果
        /// </summary>
        public void UnregisterReplacementEffect(IReplacementEffect effect, Type eventType)
        {
            if (_replacementEffects.ContainsKey(eventType))
            {
                _replacementEffects[eventType].Remove(effect);
            }
        }

        /// <summary>
        /// 处理事件的替代检查
        /// 返回替代上下文，如果可以替代则创建替代事件
        /// </summary>
        public ReplacementContext CheckReplacements(IGameEvent originalEvent)
        {
            var context = new ReplacementContext
            {
                OriginalEvent = originalEvent
            };

            // 查找所有可以替代此事件的效果
            var applicableEffects = FindApplicableReplacements(originalEvent);

            if (applicableEffects.Count == 0)
            {
                return context; // 没有替代效果
            }

            // 按照应用顺序应用替代
            ApplyReplacements(context, applicableEffects, originalEvent);

            return context;
        }

        /// <summary>
        /// 查找所有可以替代指定事件的效果
        /// </summary>
        private List<IReplacementEffect> FindApplicableReplacements(IGameEvent e)
        {
            var result = new List<IReplacementEffect>();
            Type eventType = e.GetType();

            if (!_replacementEffects.ContainsKey(eventType))
                return result;

            foreach (var effect in _replacementEffects[eventType])
            {
                if (effect.ReplacedEventType == eventType && effect.CanReplace(e))
                {
                    result.Add(effect);
                }
            }

            return result;
        }

        /// <summary>
        /// 应用替代效果
        /// </summary>
        private void ApplyReplacements(
            ReplacementContext context,
            List<IReplacementEffect> effects,
            IGameEvent originalEvent)
        {
            IGameEvent currentEvent = originalEvent;

            foreach (var effect in effects)
            {
                // 创建替代事件
                IGameEvent replacementEvent = effect.CreateReplacement(currentEvent, effect as Effect);

                if (replacementEvent == null)
                {
                    // 效果选择不替代，跳过
                    continue;
                }

                // 记录应用
                context.AppliedReplacements.Add(new ReplacementApplication
                {
                    SourceEffect = effect as Effect,
                    ReplacedEvent = currentEvent,
                    ReplacementEvent = replacementEvent,
                    ApplicationTime = DateTime.Now
                });

                // 新的事件成为当前事件
                currentEvent = replacementEvent;

                // 检查是否还有更多替代
                var moreReplacements = FindApplicableReplacements(currentEvent);

                if (moreReplacements.Count == 0)
                {
                    break; // 没有更多替代，完成
                }

                // 有更多替代，继续处理
                effects = moreReplacements;
            }

            // 设置最终替代事件
            context.ReplacementEvent = currentEvent;

            // 触发替代事件
            if (context.IsReplaced)
            {
                PublishEvent(new ReplacementEvent
                {
                    OriginalEvent = originalEvent,
                    ReplacementEffect = context.AppliedReplacements[0].SourceEffect,
                    AppliedReplacements = context.AppliedReplacements
                });
            }
        }

        /// <summary>
        /// 是否有可以替代指定类型的效果
        /// </summary>
        public bool HasReplacementEffect(Type eventType)
        {
            return _replacementEffects.ContainsKey(eventType) &&
                   _replacementEffects[eventType].Count > 0;
        }

        /// <summary>
        /// 获取指定类型的所有替代效果
        /// </summary>
        public List<IReplacementEffect> GetReplacementEffects(Type eventType)
        {
            return _replacementEffects.ContainsKey(eventType)
                ? _replacementEffects[eventType]
                : new List<IReplacementEffect>();
        }

        /// <summary>
        /// 清空所有替代效果
        /// </summary>
        public void ClearAll()
        {
            _replacementEffects.Clear();
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
    /// 替代效果基础类
    /// </summary>
    public abstract class ReplacementEffectBase : IReplacementEffect
    {
        public Type ReplacedEventType { get; private set; }
        public string ReplacementId { get; private set; }

        protected ReplacementEffectBase(Type eventType, string id)
        {
            ReplacedEventType = eventType;
            ReplacementId = id;
        }

        public abstract bool CanReplace(IGameEvent e);

        public abstract IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect);

        /// <summary>
        /// 检查事件是否匹配类型
        /// </summary>
        protected bool CheckEventType<T>(IGameEvent e)
        {
            return e is T;
        }
    }

    /// <summary>
    /// 销毁替代效果示例
    /// </summary>
    public class DestructionReplacement : ReplacementEffectBase
    {
        public DestructionReplacement() : base(typeof(CardDestroyEvent), "DestructionReplacement") { }

        public override bool CanReplace(IGameEvent e)
        {
            if (!CheckEventType<CardDestroyEvent>(e))
                return false;

            var destroyEvent = e as CardDestroyEvent;
            if (destroyEvent == null) return false;

            // 检查原因
            return destroyEvent.Reason == DestroyReason.Destroyed;
        }

        public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
        {
            var destroyEvent = originalEvent as CardDestroyEvent;
            if (destroyEvent == null) return null;

            // 替代为流放
            return new CardBanishEvent
            {
                BanishedCard = destroyEvent.DestroyedCard,
                Source = destroyEvent.Source
            };
        }
    }

    /// <summary>
    /// 攻击替代效果示例
    /// </summary>
    public class AttackReplacement : ReplacementEffectBase
    {
        public AttackReplacement() : base(typeof(AttackDeclarationEvent), "AttackReplacement") { }

        public override bool CanReplace(IGameEvent e)
        {
            return CheckEventType<AttackDeclarationEvent>(e);
        }

        public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
        {
            var attackEvent = originalEvent as AttackDeclarationEvent;
            if (attackEvent == null) return null;

            // 替代攻击目标
            // TODO: 根据具体效果实现
            return new AttackDeclarationEvent
            {
                Attacker = attackEvent.Attacker,
                Target = SelectNewTarget(attackEvent),
                AttackingPlayer = attackEvent.AttackingPlayer
            };
        }

        private Entity SelectNewTarget(AttackDeclarationEvent originalAttack)
        {
            // TODO: 实现目标选择逻辑
            return originalAttack.Target;
        }
    }

    /// <summary>
    /// 伤害替代效果示例
    /// </summary>
    public class DamageReplacement : ReplacementEffectBase
    {
        public DamageReplacement() : base(typeof(DamageEvent), "DamageReplacement") { }

        public override bool CanReplace(IGameEvent e)
        {
            if (!CheckEventType<DamageEvent>(e))
                return false;

            var damageEvent = e as DamageEvent;
            if (damageEvent == null) return false;

            // 检查目标类型
            // TODO: 根据具体效果实现
            return true;
        }

        public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
        {
            var damageEvent = originalEvent as DamageEvent;
            if (damageEvent == null) return null;

            // 替代伤害量
            // TODO: 根据具体效果实现
            return new DamageEvent
            {
                Source = damageEvent.Source,
                Target = damageEvent.Target,
                Amount = ModifyDamage(damageEvent.Amount)
            };
        }

        private int ModifyDamage(int originalAmount)
        {
            // TODO: 实现伤害修改逻辑
            return originalAmount;
        }
    }
}
