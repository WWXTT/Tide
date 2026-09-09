using System;
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
        /// <summary>发布游戏事件——经 GameCore 统一路由（静态辅助类版，与 handler 基类 PublishEvent 同口径）</summary>
        internal static void PublishRouted<T>(T gameEvent) where T : IGameEvent
        {
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(gameEvent);
            else EventManager.Instance.Publish(gameEvent);
        }

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

            // 时点接线定案：控制权变更事件改走统一路由（直发总线会让 TriggerEngine 收不到），
            // 并补发入场事件（Source=ControlChange）——OnSummon/OnOtherCreatureEnter 观察者可见。
            // 不重置入场状态：卡已在场，保持横置状态与关键词现状。
            PublishRouted(new AttrControlChangeEvent
            {
                Target = card,
                OldController = oldController,
                NewController = newController,
                IsPermanent = permanent,
                Source = context.Source
            });
            PublishRouted(new ControlChangeEvent
            {
                ChangedEntity = card,
                OldController = oldController,
                NewController = newController
            });
            PublishRouted(new CardPutToBattlefieldEvent
            {
                Card = card,
                Controller = newController,
                Tapped = card._isTapped,
                FromZone = Zone.Battlefield,
                Source = EnterSource.ControlChange
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

    // ======================= 资源转化 =======================

    /// <summary>
    /// 采掘（2026-09-08 新增原子）：以一张己方地牌（元素池）为目标——
    /// 去除 3 个同类型元素指示物，获得 1 点对应元素入 bank。
    /// 净效果 = 牺牲该色 2 个指示物换 1 个即时元素：突破「每地牌每回合产出一次」的节流提前变现，
    /// 代价是加速耗尽（指示物扣完即进墓）。目标资格由表行 TargetFilter=ElementPool 保证（兜底校验区域）。
    /// 选色（同类型 ≥3 才可采）：剩余最多者，并列取枚举序靠前（与结束阶段自动产色同口径，确定性）。
    /// </summary>
    public class MineHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Mine;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ElementPool == null || context.ZoneManager == null) return;

            foreach (var target in context.Targets)
            {
                if (!(target is Card land)) continue;
                var owner = land.GetController() ?? context.Controller;
                if (owner == null) continue;
                if (!context.ZoneManager.IsCardInZone(land, owner, Zone.ElementPool)) continue; // 兜底：非地牌区不可采

                var pooled = context.ElementPool.GetPooledCards(owner)
                    .FirstOrDefault(pc => pc.SourceCard == land);
                if (pooled == null) continue;

                // 选色：指示物 ≥3 的颜色中取剩余最多（并列取枚举序靠前）
                ManaType? pick = null;
                int best = 2; // 阈值 3 个（> 2）
                foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
                {
                    if (pooled.Tokens.TryGetValue(type, out int n) && n > best)
                    {
                        best = n;
                        pick = type;
                    }
                }
                if (pick == null) continue; // 无同类型 3 个指示物：本目标不可采（目标层已过滤的兜底）

                for (int i = 0; i < 3; i++) pooled.RemoveToken(pick.Value);
                context.ElementPool.AddMana(owner, pick.Value, land);
            }

            // 采掘导致的耗尽统一移入墓地（与 GameActions.GainElementFromToken 同口径）
            var owners = context.Targets
                .OfType<Card>()
                .Select(t => t.GetController() ?? context.Controller)
                .Where(p => p != null)
                .Distinct();
            foreach (var owner in owners)
                context.ElementPool.CheckDepletedCards(owner, context.ZoneManager);
        }

        public override string GetDescription(AtomicEffectInstance effect)
            => "采掘地牌：去除3个同类型元素指示物，获得1点对应元素";
    }

    /// <summary>
    /// 光合作用（原蓄能，2026-09-08 更名改造，绿3）：横置自身（经警戒抵扣；已横置 = 代价不可支付，不产元素），
    /// 控制者获得 {value} 点绿色元素（默认 1）。可重复的产元素引擎——
    /// 代价 = 该单位本回合不可攻/不可发动启动式能力。
    /// </summary>
    public class PhotosynthesisHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Photosynthesis;

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
                pool.AvailableMana[ManaType.Green] =
                    pool.AvailableMana.TryGetValue(ManaType.Green, out var green) ? green + amount : amount;
                PublishEvent(new ElementPoolGainEvent
                {
                    Player = context.Controller,
                    FromCard = unit,
                    GainedType = ManaType.Green,
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
            => $"光合作用：横置自身获得 {(effect.Value > 0 ? effect.Value : 1)} 点绿色元素";
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
    /// 净化（2026-09-09 语义重定义：变回生物原有状态）：
    /// 保留卡面本体关键词（Printed）与设置类（Setting——设置后即「原本属性效果」）；
    /// 清除临时关键词（Temp）、生物赋的永久关键词（GrantedPermanent）、可移除状态（Status），
    /// 以及全部指示物（CounterRules.PurgeAll 含永久层，属性先反向回写）。
    /// 神佑对净化有抗性（PurgeProtectedKeywords 豁免）——净化剥神佑+剧毒的组合无法计价平衡，
    /// 移除神佑留给未来专用效果。事件：CleanseEvent。
    /// </summary>
    public class PurifyHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Purify;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target == null) continue;

                KeywordRules.PurifyKeywords(target); // 关键词按轨别清（保留 Printed/Setting，豁免神佑）
                CounterRules.PurgeAll(target);       // 指示物全清（含永久类，属性层先反向回写）

                // 净化总是播报（即使目标本就干净，也确认净化时点）
                PublishEvent(new CleanseEvent
                {
                    Target = target,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "净化目标：变回原有状态（清临时赋予与指示物，保留本体与设置）";
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
                // 生命流失（术语定案）：非伤害、不可防止——不走伤害管线（圣盾/护甲/坚韧不挡），
                // 无伤害来源。死因=LifeLoss（归零族，神佑不拦），死亡来源=效果来源（归因到引发流失的效果卡）。
                // 卡归零走即时决策表（效果驱动路径先例：牺牲/吞噬/湮灭/剧毒）；角色扣血补发 LifeChangeEvent
                // （伤害管线同口径；游戏结束由既有终局链裁决）。
                if (target is Card card)
                {
                    card._life -= amount;
                    if (card._life <= 0)
                    {
                        card._life = 0;
                        Attribute.DeathRules.TryKill(card, Attribute.DeathCause.LifeLoss,
                            context.Source, context.ZoneManager);
                    }
                }
                else if (target is Player player)
                {
                    int oldLife = player.Life;
                    player.Life = oldLife - amount;
                    PublishEvent(new LifeChangeEvent
                    {
                        Player = player,
                        OldLife = oldLife,
                        NewLife = player.Life,
                        Source = context.Source
                    });
                }

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
                new PhotosynthesisHandler(),

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
