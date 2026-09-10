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
            var duration = context.Duration != DurationType.Once
                ? context.Duration
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
    /// 修改攻击力（三轨制定案 2026-09-09）：按来源经 StatGrantRouter 分轨——
    /// 生物来源=指示物（Duration=Permanent 走 Permanent 层换区不清，否则换区清层
    /// 「攻击力增加/减少」加时回写、离场反向回写）；魔法卡来源（=角色）=设置类永久直改
    /// （跨区保留、净化不清，视同本体）。
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
                StatGrantRouter.ModifyPower(card, amount, context.Source, context.Duration);
                PublishEvent(new StatModifyEvent
                {
                    Target = target,
                    StatType = StatType.Power,
                    OldValue = oldPower,
                    NewValue = target.GetPower(),
                    Delta = amount,
                    Duration = context.Duration,
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

    // ============ 战斗底盘（2026-09-10 攻击/守卫效果化） ============

    /// <summary>
    /// 攻击原子（1 速主动效果，横置发动）：底盘能力的计价/组合锚。
    /// 战斗流程由 CombatSystem 消费（宣言→响应窗口→结算），不走常规原子执行——
    /// 本 handler 仅满足注册完整性（VerifyHandlerCoverage 契约），直调为防御性空转。
    /// 附带「竖直参战至结算完成」由默认攻击能力的明文组合承载（Untap 自身，持续到攻击结算）。
    /// </summary>
    public class AttackHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Attack;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            UnityEngine.Debug.LogWarning("[Attack] 攻击原子不应经常规原子路径执行（战斗流程走 CombatSystem）");
        }

        public override string GetDescription(AtomicEffectInstance effect) => "攻击（1速·横置发动）";
    }

    /// <summary>
    /// 守卫原子（2 速响应拦截）：底盘能力的计价/组合锚。
    /// 拦截流程由 CombatSystem 目标确认段消费（友方被指→横置自身→转移目标），
    /// 本 handler 仅满足注册完整性，直调为防御性空转。
    /// </summary>
    public class GuardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Guard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            UnityEngine.Debug.LogWarning("[Guard] 守卫原子不应经常规原子路径执行（拦截走 CombatSystem）");
        }

        public override string GetDescription(AtomicEffectInstance effect) => "守卫（2速·响应拦截）";
    }
}
