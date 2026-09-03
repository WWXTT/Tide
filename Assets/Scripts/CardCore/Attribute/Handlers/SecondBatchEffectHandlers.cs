using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 第二批规则原语 handler — 状态/控制/反制/战斗/特殊类（2026-09-03 原子表整体修正后存留）
    // 已删除：UntapAll/AddCounters/DoubleCounters/ModifyAllStats/AddKeyword/RemoveKeyword/
    //         SwapStats/StealControl/SwapController/SetController/CounterSpell/CounterTargetSpell/
    //         PreventDamage/FightTarget/TrampleDamage/CopyCard（表行随枚举一并移除）
    // Nullify → Purify（净化：去除目标全部关键词和指示物）
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

        /// <summary>+1/+1 与 -1/-1 指示物对基础 P/T 的即时影响（成长等路径复用；清除时由 CounterRules 反向回写）</summary>
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

        /// <summary>
        /// 变更控制者：跨玩家迁移战场容器 + 改写控制者 + 发布事件。
        /// 定案：控制权变更 = 场内迁移，不产生新占用——不经入场容量闸门
        /// （满场偷取仍生效，卡从旧控制者半场迁入新控制者名下；棋盘层按列表顺序派生呈现）。
        /// </summary>
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

    /// <summary>
    /// 蓄能（Recharging，定案）：横置目标单位（经警戒抵扣；已横置 = 代价不可支付，不产元素），
    /// 控制者获得 {value} 点灰色元素（默认 1）。可重复的产元素引擎——
    /// 代价 = 该单位本回合不可攻/不可发动效果。
    /// </summary>
    public class RechargingHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Recharging;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.Controller == null) return;
            int amount = context.GetValueAfterModifiers(effect.Value > 0 ? effect.Value : 1);
            foreach (var target in context.Targets)
            {
                if (!(target is Card unit) || !unit.IsAlive) continue;
                if (unit.IsTapped()) continue; // 已横置：代价不可支付，也不可被警戒抵消
                if (KeywordRules.ShouldTap(unit))
                    unit.Tap();

                var pool = context.ElementPool?.GetPool(context.Controller);
                if (pool == null) continue;
                pool.AvailableMana[ManaType.Gray] =
                    pool.AvailableMana.TryGetValue(ManaType.Gray, out var gray) ? gray + amount : amount;
                PublishEvent(new ElementPoolGainEvent
                {
                    Player = context.Controller,
                    FromCard = unit,
                    GainedType = ManaType.Gray,
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
            => $"蓄能：横置自身获得 {(effect.Value > 0 ? effect.Value : 1)} 点灰色元素";
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

    // ======================= 净化 =======================

    /// <summary>
    /// 净化（定案，原"无效化"改名改语义）：去除目标全部关键词和指示物。
    /// 指向玩家会剥掉神佑（神佑是可被效果移除的真实状态——效果死亡族对其生效的唯一通路）；
    /// 属性指示物清除前反向回写（CounterRules.PurgeAll）。事件：CleanseEvent 首次起用。
    /// </summary>
    public class PurifyHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Purify;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target == null) continue;

                target._keywords.Clear();
                CounterRules.PurgeAll(target);

                // 净化总是播报（即使目标本就干净，也确认净化时点）
                PublishEvent(new CleanseEvent
                {
                    Target = target,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "净化目标：移除其全部关键词与指示物";
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
                target.TakeDamage(dmg, context.Source, isCombat: true);
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

    // ======================= 特殊 =======================

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
                new RechargingHandler(),

                // 控制
                new GainControlHandler(),

                // 净化（原 Nullify）
                new PurifyHandler(),

                // 战斗 / 伤害
                new DealCombatDamageHandler(),
                new LifeLossHandler(),

                // 特殊
                new TakeExtraTurnHandler(),
                new SkipTurnHandler(),
            };
        }
    }
}
