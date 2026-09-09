using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 第三批 handler（2026-09-03 原子表整体修正后存留）
    // 已删除：PutOnBottomOfDeck/MoveCard/MoveToAnyZone/DrawThenDiscard/SearchAndReveal/
    //         SearchAndPlay/RevealCards/AddMana/ConsumeMana/DamageBasedOnStat/DestroyRandom/
    //         CopyExact/ExchangePosition（表行随枚举一并移除）
    // NegateEffect → Silence（沉默指示物：持有者不可发动主动效果）
    // MillCard 改名"送墓"（表 EnumName/DisplayName 已改，EffectType 不变）
    // ================================================================

    // ---------------- 牌库 ----------------

    /// <summary>送墓（原名"磨牌"；控制者牌库顶 N 张 → 坟墓场）</summary>
    public class MillCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.MillCard;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager == null || context.Controller == null) return;

            var top = context.ZoneManager.GetTopCards(context.Controller, count);
            var container = context.ZoneManager.GetZoneContainer(context.Controller);
            foreach (var card in top)
            {
                container.Move(card, Zone.Deck, Zone.Graveyard);
                card.SetZone(Zone.Graveyard);
                PublishEvent(new CardMillEvent { Player = context.Controller, Card = card, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"送墓：牌库顶 {effect.Value} 张入墓地";
    }

    /// <summary>占卜（查看牌库顶 N 张，仅展示，不移动）</summary>
    public class ScryCardsHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ScryCards;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager == null || context.Controller == null) return;

            var cards = context.ZoneManager.GetTopCards(context.Controller, count);
            PublishEvent(new ScryEvent { Player = context.Controller, Cards = cards, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"占卜 {effect.Value} 张";
    }

    /// <summary>变更拥有者（各目标的 owner 设为控制者）</summary>
    public class ChangeOwnerHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ChangeOwner;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.Controller == null) return;
            foreach (var target in context.Targets)
                if (target is Card card)
                    card.SetOwner(context.Controller);
        }

        public override string GetDescription(AtomicEffectInstance effect) => "变更卡牌拥有者";
    }

    // ---------------- 死亡原子（牺牲 / 吞噬 / 湮灭） ----------------

    /// <summary>牺牲：控制者主动将己方生物置入坟墓场（来源=控制者）</summary>
    public class SacrificeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Sacrifice;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                    DeathRules.TryKill(card, DeathCause.Sacrifice, context.Controller, context.ZoneManager);
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "牺牲目标生物";
    }

    /// <summary>吞噬：先消灭裁决（不灭/复生等替代在 DeathRules 定案，拦下即无吸收），成功才吸收——复制目标关键词 + 回复目标当前生命（来源=吞噬者）</summary>
    public class DevourHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Devour;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var devourer = context.Source as Card;
            foreach (var target in context.Targets)
            {
                if (!(target is Card victim) || victim == devourer) continue;

                // 消灭裁决先行：被不灭拦下 / 复生替代 → 无吸收（护盾矩阵拦 Devour，DeathRules 内定案）
                if (!DeathRules.TryKill(victim, DeathCause.Devour, context.Source, context.ZoneManager))
                    continue;

                // 吸收：复制目标形态关键词（Printed+Setting 轨，临时不随形态——定案⑨；
                // 继承落 Setting 轨=吸收后视同本体）+ 回复目标当前生命（吞噬者 = context.Source）
                if (devourer != null)
                {
                    KeywordRules.CopyFormKeywords(victim, devourer);
                    devourer.Heal(victim.GetLife());
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "吞噬目标生物";
    }

    /// <summary>湮灭：彻底移除，直送除外区，不可复生（DeathRules 内定案）</summary>
    public class AnnihilateHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Annihilate;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                    DeathRules.TryKill(card, DeathCause.Annihilate, context.Source, context.ZoneManager);
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "湮灭目标生物";
    }

    /// <summary>
    /// 摧毁（新增原子，红3）：与"消灭"的区别——作用于**无生命值单位**（地牌/结界）。
    /// 地牌 = 双方元素池中的卡（Zone.ElementPool）：先出池（余量写回卡，不设耗尽标记——
    /// 摧毁≠资源枯竭，回收后可再作地牌），再直送拥有者墓地；结界 = 战场非生物持久物。
    /// 不经死亡决策表（无生命值者无"死亡"），发 CardDestroyEvent（Reason=Smashed）。
    /// </summary>
    public class SmashHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Smash;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;

                // 出池：从双方元素池移除（余量写回卡上，由拥有者的池持有）
                if (context.ElementPool != null)
                {
                    var owner = card.GetOwner() ?? card.GetController();
                    if (owner == null) continue;
                    context.ElementPool.RemoveCardFromPool(card, owner);
                }

                // 直送墓地（无生命值单位不走 DeathRules）
                var cardOwner = card.GetOwner() ?? card.GetController();
                if (context.ZoneManager != null && cardOwner != null)
                {
                    var from = card.GetZone();
                    if (from != Zone.Graveyard)
                        context.ZoneManager.MoveCard(card, cardOwner, from, Zone.Graveyard);
                }

                PublishEvent(new CardDestroyEvent
                {
                    DestroyedCard = card,
                    Reason = DestroyReason.Smashed,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "摧毁目标（无生命值单位：地牌/结界）";
    }

    // ---------------- 反制 / 沉默 ----------------

    /// <summary>
    /// 打落（软打断）：把发动区中的卡直接送墓。该卡的 cast 结算时因「已不在发动区」中止——
    /// 不付费、不结算（消费点在 GameActions.ResolveCardCastAsync，Option Y 定案）。
    /// </summary>
    public class KnockDownHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.KnockDown;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;
                var owner = card.GetController() ?? context.Controller;
                if (!context.ZoneManager.IsCardInZone(card, owner, Zone.Activation)) continue;

                context.ZoneManager.MoveCard(card, owner, Zone.Activation, Zone.Graveyard);
                PublishEvent(new CardLeaveActivationEvent
                {
                    Card = card,
                    Controller = owner,
                    ToZone = Zone.Graveyard
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "把发动中的卡打落入墓（不付费即中止）";
    }

    /// <summary>无效发动（标记目标无效，IsActivation）</summary>
    public class NegateActivationHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.NegateActivation;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                target.Negate();
                PublishEvent(new NegateEvent { Target = target, IsActivation = true, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "无效目标的发动";
    }

    /// <summary>
    /// 沉默指示物（定案，原"无效效果"改名改语义）：对目标附加沉默指示物——
    /// 持有者不可发动主动效果（激活式能力，挂 EffectExecutionEngine.CanActivate 第 0 步）；
    /// 未写持续时间 = 换区清除。
    /// </summary>
    public class SilenceHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Silence;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.SilenceCounter, 1);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.SilenceCounter,
                    Amount = 1,
                    Source = context.Source
                });
                PublishEvent(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = "沉默",
                    Detail = "沉默：持有者不可发动主动效果",
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "附加沉默指示物（不可发动主动效果）";
    }

    /// <summary>第三批 handler 工厂</summary>
    public static class ThirdBatchHandlerFactory
    {
        public static IAtomicEffectHandler[] CreateAll()
        {
            return new IAtomicEffectHandler[]
            {
                // 牌库
                new MillCardHandler(),
                new ScryCardsHandler(),
                new ChangeOwnerHandler(),

                // 死亡原子
                new SacrificeHandler(),
                new DevourHandler(),
                new AnnihilateHandler(),

                // 摧毁（无生命值单位：地牌/结界）
                new SmashHandler(),

                // 反制 / 沉默
                new KnockDownHandler(),
                new NegateActivationHandler(),
                new SilenceHandler(),
            };
        }
    }
}
