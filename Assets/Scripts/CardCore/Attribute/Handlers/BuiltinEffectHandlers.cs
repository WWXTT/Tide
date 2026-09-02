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
                target.TakeDamage(dmg, context.Source); // 关键词管线：圣盾/护甲/坚韧/剧毒/吸血
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

    /// <summary>消灭</summary>
    public class DestroyHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Destroy;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                // 死亡决策表统一裁决（不灭/仪式回手/复生/落墓/事件全在 DeathRules 内定案）
                if (target is Card card)
                    DeathRules.TryKill(card, DeathCause.DestroyEffect, context.Source, context.ZoneManager);
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            return "消灭目标";
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
                target.Freeze(duration);
                target.Tap();
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

    /// <summary>增益攻击力</summary>
    public class ModifyPowerHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyPower;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int oldPower = target.GetPower();
                target.ModifyPower(amount);
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
            return $"攻击力 {sign}{effect.Value}";
        }
    }

    /// <summary>创建衍生物</summary>
    public class CreateTokenHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.CreateToken;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            string templateId = effect.StringValue;

            for (int i = 0; i < count; i++)
            {
                var token = new Card { ID = templateId ?? "Token_Generic" };

                // 入场容量闸门：满则衍生物直接入墓并发失败事件（不再视为生成成功）
                if (context.ZoneManager == null || context.Controller == null ||
                    !context.ZoneManager.TryAddToBattlefield(token, context.Controller))
                    continue;

                PublishEvent(new TokenCreatedEvent
                {
                    TokenTemplateId = templateId,
                    Controller = context.Controller,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            return $"创建 {effect.Value} 个衍生物";
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
