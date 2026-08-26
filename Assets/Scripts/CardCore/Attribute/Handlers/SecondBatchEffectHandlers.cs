using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 第二批规则原语 handler — 补齐状态/控制/反制/战斗/特殊类
    // 复用 EntityEffectExtensions / LayerEngine / ZoneManager 原语
    // 连续 P/T 增益走 LayerEngine（不污染基础属性）；
    // 永久变更（指示物、转化）走基础字段。
    // ================================================================

    /// <summary>本批 handler 共享的辅助方法</summary>
    internal static class HandlerHelpers
    {
        /// <summary>按层引擎计算当前攻击力（无层引擎时退化为基础值）</summary>
        internal static int CurrentPower(Entity e)
        {
            var le = GameCore.Instance?.LayerEngine;
            if (le != null) return le.CalculatePower(e);
            return e.GetPower();
        }

        /// <summary>+1/+1 与 -1/-1 指示物对基础 P/T 的即时影响</summary>
        internal static void ApplyCounterStat(Entity target, string counterType, int amount)
        {
            if (!(target is Card card) || amount == 0) return;
            if (counterType == "+1/+1")
            {
                card._power += amount;
                card._life += amount;
                if (amount > 0) card._maxLife += amount;
            }
            else if (counterType == "-1/-1")
            {
                card._power -= amount;
                card._life -= amount;
                if (card._life <= 0) card.IsAlive = false;
            }
        }

        /// <summary>变更控制者：跨玩家迁移战场容器 + 改写控制者 + 发布事件</summary>
        internal static void ChangeControl(EffectExecutionContext context, Card card, Player newController, bool permanent)
        {
            if (card == null || newController == null) return;
            var oldController = card.GetController();
            if (oldController == newController) return;

            if (context.ZoneManager != null && oldController != null)
                context.ZoneManager.GetZoneContainer(oldController)?.Remove(card, Zone.Battlefield);

            card.SetController(newController);
            card.SetZone(Zone.Battlefield);

            if (context.ZoneManager != null)
                context.ZoneManager.GetZoneContainer(newController)?.Add(card, Zone.Battlefield);

            EventManager.Instance.Publish(new AttrControlChangeEvent
            {
                Target = card,
                OldController = oldController,
                NewController = newController,
                IsPermanent = permanent,
                Source = context.Source
            });
            EventManager.Instance.Publish(new ControlChangeEvent
            {
                ChangedEntity = card,
                OldController = oldController,
                NewController = newController
            });
        }
    }

    // ======================= 状态变更 =======================

    /// <summary>横置</summary>
    public class TapHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Tap;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                target.Tap();
                PublishEvent(new TapEvent { TappedEntity = target, IsUntapping = false });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "横置目标";
    }

    /// <summary>重置（解除横置）</summary>
    public class UntapHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Untap;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                target.Untap();
                PublishEvent(new UntapEvent { UntappedEntity = target });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "重置目标";
    }

    /// <summary>全体重置（解除控制者战场上所有卡牌的横置）</summary>
    public class UntapAllHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.UntapAll;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;
            foreach (var card in context.ZoneManager.GetCards(context.Controller, Zone.Battlefield))
            {
                if (card.IsTapped())
                {
                    card.Untap();
                    PublishEvent(new UntapEvent { UntappedEntity = card });
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "重置所有卡牌";
    }

    /// <summary>添加指示物（StringValue 指定类型，默认 +1/+1）</summary>
    public class AddCountersHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCounters;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            string ct = string.IsNullOrEmpty(effect.StringValue) ? "+1/+1" : effect.StringValue;
            int amt = context.GetValueAfterModifiers(effect.Value);
            if (amt == 0) amt = 1;

            foreach (var target in context.Targets)
            {
                target.AddCounters(ct, amt);
                HandlerHelpers.ApplyCounterStat(target, ct, amt);
                PublishEvent(new CounterEvent
                {
                    Target = target,
                    CounterType = ct,
                    Amount = amt,
                    IsAdd = true,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"添加 {effect.Value} 个指示物";
    }

    /// <summary>指示物翻倍</summary>
    public class DoubleCountersHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DoubleCounters;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;
                foreach (var kvp in card._counters.ToList())
                {
                    int add = kvp.Value; // 翻倍 = 再加等量
                    if (add == 0) continue;
                    card.AddCounters(kvp.Key, add);
                    HandlerHelpers.ApplyCounterStat(card, kvp.Key, add);
                    PublishEvent(new CounterEvent
                    {
                        Target = card,
                        CounterType = kvp.Key,
                        Amount = add,
                        IsAdd = true,
                        Source = context.Source
                    });
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "指示物数量翻倍";
    }

    /// <summary>整体增益 P/T（Value=攻击, Value2=生命）。
    /// 非永久 → 经 LayerEngine 连续效果；永久 → 改写基础属性。</summary>
    public class ModifyAllStatsHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyAllStats;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dP = effect.Value;
            int dT = effect.Value2;
            var le = GameCore.Instance?.LayerEngine;
            bool continuous = effect.Duration != DurationType.Permanent && le != null;

            foreach (var target in context.Targets)
            {
                if (continuous)
                {
                    le.AddPowerToughnessBuff(target, dP, dT, effect.Duration, context.Source);
                }
                else
                {
                    int oldP = target.GetPower();
                    target.ModifyPower(dP);
                    int oldL = target.GetLife();
                    target.ModifyLife(dT);
                    if (target is Card card && dT > 0) card._maxLife += dT;

                    PublishEvent(new StatModifyEvent
                    {
                        Target = target, StatType = StatType.Power,
                        OldValue = oldP, NewValue = target.GetPower(), Delta = dP,
                        Duration = effect.Duration, Source = context.Source
                    });
                    PublishEvent(new StatModifyEvent
                    {
                        Target = target, StatType = StatType.Life,
                        OldValue = oldL, NewValue = target.GetLife(), Delta = dT,
                        Duration = effect.Duration, Source = context.Source
                    });
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"全体 +{effect.Value}/+{effect.Value2}";
    }

    /// <summary>添加关键词（StringValue 指定）</summary>
    public class AddKeywordHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddKeyword;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            string kw = effect.StringValue;
            if (string.IsNullOrEmpty(kw)) return;
            foreach (var target in context.Targets)
            {
                target.AddKeyword(kw, effect.Duration);
                PublishEvent(new KeywordEvent
                {
                    Target = target, Keyword = kw, IsAdd = true,
                    Duration = effect.Duration, Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"获得 {effect.StringValue}";
    }

    /// <summary>移除关键词（StringValue 指定）</summary>
    public class RemoveKeywordHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RemoveKeyword;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            string kw = effect.StringValue;
            if (string.IsNullOrEmpty(kw)) return;
            foreach (var target in context.Targets)
            {
                target.RemoveKeyword(kw);
                PublishEvent(new KeywordEvent
                {
                    Target = target, Keyword = kw, IsAdd = false,
                    Duration = effect.Duration, Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"失去 {effect.StringValue}";
    }

    /// <summary>交换攻击力与生命值（基础属性）</summary>
    public class SwapStatsHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SwapStats;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;
                int p = card._power;
                int l = card._life;
                card._power = l;
                card._life = p;
                PublishEvent(new SwapStatsEvent
                {
                    Target = card,
                    OldPower = p, NewPower = l,
                    OldLife = l, NewLife = p,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "交换攻击力与生命值";
    }

    // ======================= 控制相关 =======================

    /// <summary>获得控制权（永久）</summary>
    public class GainControlHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GainControl;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            bool permanent = effect.Duration == DurationType.Permanent;
            foreach (var target in context.Targets)
                if (target is Card card)
                    HandlerHelpers.ChangeControl(context, card, context.Controller, permanent);
        }

        public override string GetDescription(AtomicEffectInstance effect) => "获得目标的控制权";
    }

    /// <summary>夺取控制权（默认临时，依 Duration）</summary>
    public class StealControlHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.StealControl;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            bool permanent = effect.Duration == DurationType.Permanent;
            foreach (var target in context.Targets)
                if (target is Card card)
                    HandlerHelpers.ChangeControl(context, card, context.Controller, permanent);
        }

        public override string GetDescription(AtomicEffectInstance effect) => "夺取目标的控制权";
    }

    /// <summary>交换两个目标的控制者</summary>
    public class SwapControllerHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SwapController;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context)
            => context?.Targets != null && context.Targets.Count >= 2;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var a = context.Targets[0] as Card;
            var b = context.Targets[1] as Card;
            if (a == null || b == null) return;

            var ca = a.GetController();
            var cb = b.GetController();
            // 经 ChangeControl 迁移战场容器；先 a→cb 再 b→ca
            HandlerHelpers.ChangeControl(context, a, cb, true);
            HandlerHelpers.ChangeControl(context, b, ca, true);

            PublishEvent(new SwapControllerEvent { Target1 = a, Target2 = b, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => "交换两个目标的控制者";
    }

    /// <summary>设置控制者为效果控制者（永久）</summary>
    public class SetControllerHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SetController;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
                if (target is Card card)
                    HandlerHelpers.ChangeControl(context, card, context.Controller, true);
        }

        public override string GetDescription(AtomicEffectInstance effect) => "设置控制者";
    }

    // ======================= 反制 / 防止 =======================

    /// <summary>反制法术（标记目标为无效，结算时不生效）</summary>
    public class CounterSpellHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.CounterSpell;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                target.Negate();
                PublishEvent(new CounterSpellEvent { Target = target, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "反制目标法术";
    }

    /// <summary>反制目标法术（指向性）</summary>
    public class CounterTargetSpellHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.CounterTargetSpell;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                target.Negate();
                PublishEvent(new CounterSpellEvent { Target = target, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "反制指定法术";
    }

    /// <summary>伤害防止</summary>
    public class PreventDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.PreventDamage;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                target.AddDamagePrevention(amount, effect.Duration);
                PublishEvent(new PreventDamageEvent
                {
                    Target = target, Amount = amount,
                    Duration = effect.Duration, Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"防止 {effect.Value} 点伤害";
    }

    /// <summary>无效化卡牌</summary>
    public class NullifyHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Nullify;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                target.Nullify();
                PublishEvent(new NullifyEvent { Target = target, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "无效化目标";
    }

    // ======================= 战斗 / 伤害 =======================

    /// <summary>造成战斗伤害</summary>
    public class DealCombatDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DealCombatDamage;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dmg = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                target.TakeDamage(dmg);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source, Target = target, Damage = dmg,
                    IsCombatDamage = true, DamageType = DamageType.Combat
                });
                PublishEvent(new CombatDamageEvent
                {
                    Attacker = context.Source, Defender = target, Damage = dmg
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"造成 {effect.Value} 点战斗伤害";
    }

    /// <summary>失去生命（不可防止，非伤害）</summary>
    public class LifeLossHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.LifeLoss;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                target.TakeDamage(amount);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source, Target = target, Damage = amount,
                    IsCombatDamage = false, DamageType = DamageType.LifeLoss
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"失去 {effect.Value} 点生命";
    }

    /// <summary>与目标交手（互相造成等同攻击力的伤害）</summary>
    public class FightTargetHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.FightTarget;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var src = context.Source;
            if (src == null) return;
            int srcPow = HandlerHelpers.CurrentPower(src);

            foreach (var target in context.Targets)
            {
                int tgtPow = HandlerHelpers.CurrentPower(target);
                target.TakeDamage(srcPow);
                src.TakeDamage(tgtPow);
                PublishEvent(new FightEvent
                {
                    Attacker = src, Defender = target,
                    DamageToAttacker = tgtPow, DamageToDefender = srcPow,
                    Source = src
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "与目标交手";
    }

    /// <summary>践踏伤害（溢出伤害直击玩家）</summary>
    public class TrampleDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.TrampleDamage;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dmg = context.GetValueAfterModifiers(effect.Value);
            if (dmg <= 0) return;
            var target = context.PrimaryTarget ?? (Entity)context.Controller?.Opponent;
            if (target == null) return;

            target.TakeDamage(dmg);
            PublishEvent(new AtomicDamageEvent
            {
                Source = context.Source, Target = target, Damage = dmg,
                IsCombatDamage = true, DamageType = DamageType.Combat
            });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"践踏造成 {effect.Value} 点伤害";
    }

    // ======================= 特殊 =======================

    /// <summary>复制卡牌（在控制者战场创建属性副本衍生物）</summary>
    public class CopyCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.CopyCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.Controller == null) return;
            foreach (var target in context.Targets)
            {
                if (!(target is Card orig)) continue;

                var copy = new Card { ID = orig.ID };
                copy._power = orig._power;
                copy._life = orig._life;
                copy._maxLife = orig._maxLife;
                copy._baseCost = orig._baseCost;
                foreach (var kw in orig._keywords) copy._keywords.Add(kw);
                copy.SetController(context.Controller);
                copy.SetZone(Zone.Battlefield);

                context.ZoneManager?.GetZoneContainer(context.Controller)?.Add(copy, Zone.Battlefield);

                PublishEvent(new CardCopiedEvent
                {
                    OriginalCard = orig, Controller = context.Controller, Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "复制目标卡牌";
    }

    /// <summary>额外回合</summary>
    public class TakeExtraTurnHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.TakeExtraTurn;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var player = (context.PrimaryTarget as Player) ?? context.Controller;
            if (player == null) return;
            player.AddExtraTurn();
            PublishEvent(new ExtraTurnEvent { Player = player, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => "获得额外回合";
    }

    /// <summary>跳过回合</summary>
    public class SkipTurnHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SkipTurn;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var player = (context.PrimaryTarget as Player) ?? context.Controller?.Opponent;
            if (player == null) return;
            player.SkipNextTurn();
            PublishEvent(new SkipTurnEvent { Player = player, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => "跳过回合";
    }

    /// <summary>第二批 handler 工厂</summary>
    public static class SecondBatchHandlerFactory
    {
        public static IAtomicEffectHandler[] CreateAll()
        {
            return new IAtomicEffectHandler[]
            {
                // 状态
                new TapHandler(),
                new UntapHandler(),
                new UntapAllHandler(),
                new AddCountersHandler(),
                new DoubleCountersHandler(),
                new ModifyAllStatsHandler(),
                new AddKeywordHandler(),
                new RemoveKeywordHandler(),
                new SwapStatsHandler(),

                // 控制
                new GainControlHandler(),
                new StealControlHandler(),
                new SwapControllerHandler(),
                new SetControllerHandler(),

                // 反制 / 防止
                new CounterSpellHandler(),
                new CounterTargetSpellHandler(),
                new PreventDamageHandler(),
                new NullifyHandler(),

                // 战斗 / 伤害
                new DealCombatDamageHandler(),
                new LifeLossHandler(),
                new FightTargetHandler(),
                new TrampleDamageHandler(),

                // 特殊
                new CopyCardHandler(),
                new TakeExtraTurnHandler(),
                new SkipTurnHandler(),
            };
        }
    }
}
