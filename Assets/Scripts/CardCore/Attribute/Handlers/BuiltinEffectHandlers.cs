using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 红色效果 - 伤害与破坏
    // ================================================================

    /// <summary>造成伤害</summary>
    public class DealDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DealDamage;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dmg = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int lifeBefore = target.GetLife();
                target.TakeDamage(dmg, context.Source); // 关键词管线：圣盾/护甲/坚韧/吸血
                int actual = System.Math.Max(0, lifeBefore - target.GetLife());
                context.LastOutcome.RecordDamage(target, lifeBefore, actual);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source,
                    Target = target,
                    Damage = dmg,
                    IsCombatDamage = false
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            return $"造成 {effect.Value} 点伤害";
        }
    }

    // ================================================================
    // 蓝色效果 - 控制与知识
    // ================================================================

    /// <summary>抽牌</summary>
    public class DrawCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DrawCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager != null && context.Controller != null)
            {
                for (int i = 0; i < count; i++)
                {
                    var drawn = ZoneManagerExtensions.DrawCard(context.ZoneManager, context.Controller);
                    if (drawn != null)
                    {
                        PublishEvent(new CardDrawEvent
                        {
                            Player = context.Controller,
                            DrawnCard = drawn,
                            DrawCount = 1
                        });
                    }
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            return $"抽 {effect.Value} 张牌";
        }
    }

    /// <summary>弹回手牌</summary>
    public class ReturnToHandHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ReturnToHand;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    var controller = card.GetController();
                    if (context.ZoneManager != null && controller != null)
                        context.ZoneManager.MoveCard(card, controller, Zone.Battlefield, Zone.Hand);

                    PublishEvent(new CardReturnToHandEvent
                    {
                        Card = card,
                        Source = context.Source
                    });
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            return "将目标移回手牌";
        }
    }

    /// <summary>冻结</summary>
    public class FreezePermanentHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.FreezePermanent;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var duration = effect.Duration != DurationType.Once
                ? effect.Duration
                : DurationType.UntilEndOfTurn;

            foreach (var target in context.Targets)
            {
                // Freeze 定案：强制横置 + 一个冻结指示物（回合开始移除指示物而不重置）
                target.Freeze(duration);
                PublishEvent(new FreezeEvent
                {
                    Target = target,
                    Duration = duration,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            return "冻结目标";
        }
    }

    // ================================================================
    // 绿色效果 - 成长与恢复
    // ================================================================

    /// <summary>治疗</summary>
    public class HealHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Heal;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int lifeBefore = target.GetLife();
                target.Heal(amount);
                int overfill = System.Math.Max(0, amount - (target.GetLife() - lifeBefore)); // 超上限截断部分
                context.LastOutcome.RecordHeal(target, lifeBefore, amount);
                PublishEvent(new HealEvent
                {
                    Target = target,
                    Amount = amount,
                    Overfill = overfill,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            return $"恢复 {effect.Value} 点生命";
        }
    }

    /// <summary>
    /// 修改攻击力（指示物形式定案）：正值加"攻击力增加"层、负值加"攻击力减少"层（单向粒度指示物）——
    /// 加时即回写 _power、换区/净化清除时反向回写（仅场上存在，离场消失）。
    /// </summary>
    public class ModifyPowerHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyPower;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                int oldPower = card.GetPower();
                if (amount >= 0) CounterRules.AddStatCounter(card, CounterRules.PowerUpCounter, amount, context.Source);
                else CounterRules.AddStatCounter(card, CounterRules.PowerDownCounter, -amount, context.Source);
                PublishEvent(new StatModifyEvent
                {
                    Target = target,
                    StatType = StatType.Power,
                    OldValue = oldPower,
                    NewValue = target.GetPower(),
                    Delta = amount,
                    Duration = effect.Duration,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            string sign = effect.Value >= 0 ? "+" : "";
            return $"攻击力 {sign}{effect.Value}（指示物）";
        }
    }

    // ================================================================
    // 灰色效果 - 通用与变化
    // ================================================================

    /// <summary>变形：变成另一张卡（StringValue=目标卡 ID），不触发死亡；离开战场时解除变回原随从</summary>
    public class MorphHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Morph;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 目标形态经组合根注入的解析器查 CardData（StringValue = 目标卡 ID）
            var targetData = MorphSystem.ResolveMorphTarget?.Invoke(effect.StringValue);
            if (targetData == null) return;

            foreach (var t in context.Targets)
            {
                if (!(t is Card card)) continue;
                if (card.GetZone() != Zone.Battlefield) continue; // 定案：变形仅包含场上目标
                if (card.MorphInto(targetData))
                {
                    PublishEvent(new KeywordAppliedEvent
                    {
                        Target = card,
                        Keyword = "Morph",
                        Detail = $"变形为 {targetData.CardName}",
                        Source = context.Source
                    });
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "变形为另一张卡";
    }
}
