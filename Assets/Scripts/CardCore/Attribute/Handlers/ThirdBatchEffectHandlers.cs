using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 第三批 handler — 配置驱动长尾的即实装分（与设计整合，已剔除土地/装备/变身等不符设定项）
    // 复用 EntityEffectExtensions / ZoneManager / ElementPoolSystem / HandlerHelpers 原语
    // ================================================================

    /// <summary>本批 handler 共享的辅助方法</summary>
    internal static class ThirdBatchHelpers
    {
        /// <summary>破坏：标记死亡 + 移入坟墓场 + 发布 CardDestroyEvent</summary>
        internal static void DestroyToGraveyard(Card card, EffectExecutionContext context)
        {
            if (card == null) return;
            card.IsAlive = false;

            var controller = card.GetController();
            if (context.ZoneManager != null && controller != null)
            {
                var from = card.GetZone();
                if (from != Zone.Graveyard)
                    context.ZoneManager.GetZoneContainer(controller).Move(card, from, Zone.Graveyard);
                card.SetZone(Zone.Graveyard);
            }

            EventManager.Instance.Publish(new CardDestroyEvent
            {
                DestroyedCard = card,
                Reason = DestroyReason.Destroyed,
                Source = context.Source
            });
        }

        /// <summary>将各 target 迁入指定区域（经各自控制者容器），发布 CardMoveEvent</summary>
        internal static void MoveTargetsToZone(EffectExecutionContext context, Zone toZone)
        {
            if (context.ZoneManager == null) return;
            foreach (var target in context.Targets.ToList())
            {
                if (!(target is Card card)) continue;
                var controller = card.GetController();
                if (controller == null) continue;

                var from = card.GetZone();
                if (from == toZone) continue;

                context.ZoneManager.GetZoneContainer(controller).Move(card, from, toZone);
                card.SetZone(toZone);
                EventManager.Instance.Publish(new CardMoveEvent
                {
                    MovedCard = card, From = from, To = toZone, Controller = controller
                });
            }
        }
    }

    // ---------------- 牌库 / 移动 ----------------

    /// <summary>磨牌（控制者牌库顶 N 张 → 坟墓场）</summary>
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

        public override string GetDescription(AtomicEffectInstance effect) => $"磨掉牌库顶 {effect.Value} 张牌";
    }

    /// <summary>放置牌库底（各目标 → 拥有者牌库底）</summary>
    public class PutOnBottomOfDeckHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.PutOnBottomOfDeck;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null) return;
            foreach (var target in context.Targets.ToList())
            {
                if (!(target is Card card)) continue;
                var owner = card.GetOwner() ?? card.GetController();
                if (owner == null) continue;

                var from = card.GetZone();
                context.ZoneManager.GetZoneContainer(owner).Move(card, from, Zone.Deck, DeckPosition.Bottom);
                card.SetZone(Zone.Deck);
                PublishEvent(new CardMoveEvent { MovedCard = card, From = from, To = Zone.Deck, Controller = owner });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "放置到牌库底";
    }

    /// <summary>移动卡牌（各目标 → effect.ZoneParam）</summary>
    public class MoveCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.MoveCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
            => ThirdBatchHelpers.MoveTargetsToZone(context, effect.ZoneParam);

        public override string GetDescription(AtomicEffectInstance effect) => $"移动到 {effect.ZoneParam}";
    }

    /// <summary>移动到任意区域（各目标 → effect.ZoneParam）</summary>
    public class MoveToAnyZoneHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.MoveToAnyZone;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
            => ThirdBatchHelpers.MoveTargetsToZone(context, effect.ZoneParam);

        public override string GetDescription(AtomicEffectInstance effect) => $"移动到 {effect.ZoneParam}";
    }

    /// <summary>抽牌后弃牌（抽 Value 张，再弃 Value2/Value 张）</summary>
    public class DrawThenDiscardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DrawThenDiscard;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            int draw = context.GetValueAfterModifiers(effect.Value);
            int discard = effect.Value2 > 0 ? effect.Value2 : draw;

            for (int i = 0; i < draw; i++)
            {
                var drawn = context.ZoneManager.DrawCard(context.Controller);
                if (drawn == null) break;
                PublishEvent(new CardDrawEvent { Player = context.Controller, DrawnCard = drawn, DrawCount = 1 });
            }

            var hand = context.ZoneManager.GetCards(context.Controller, Zone.Hand);
            var container = context.ZoneManager.GetZoneContainer(context.Controller);
            for (int i = 0; i < discard && i < hand.Count; i++)
            {
                var card = hand[i];
                container.Move(card, Zone.Hand, Zone.Graveyard);
                PublishEvent(new CardDiscardEvent { Player = context.Controller, Card = card, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"抽 {effect.Value} 张后弃牌";
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

    /// <summary>检索并展示（展示牌库顶 N 张，不抽取）</summary>
    public class SearchAndRevealHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SearchAndReveal;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager == null || context.Controller == null) return;

            var cards = context.ZoneManager.GetTopCards(context.Controller, count);
            PublishEvent(new RevealCardsEvent { Player = context.Controller, Cards = cards, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"检索并展示 {effect.Value} 张";
    }

    /// <summary>检索并使用（牌库顶 1 张 → 战场）</summary>
    public class SearchAndPlayHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SearchAndPlay;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            var top = context.ZoneManager.GetTopCards(context.Controller, 1);
            if (top.Count == 0) return;

            var card = top[0];
            context.ZoneManager.GetZoneContainer(context.Controller).Move(card, Zone.Deck, Zone.Battlefield);
            card.SetController(context.Controller);
            card.SetZone(Zone.Battlefield);
            card.WasFormallySummoned = true; // 检索直接上场＝正式入场
            PublishEvent(new CardPutToBattlefieldEvent { Card = card, Controller = context.Controller, Tapped = false });
        }

        public override string GetDescription(AtomicEffectInstance effect) => "检索并使用牌库顶卡";
    }

    /// <summary>展示卡牌（指定 target，或退化为控制者手牌）</summary>
    public class RevealCardsHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RevealCards;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var cards = context.Targets.OfType<Card>().ToList();
            if (cards.Count == 0 && context.ZoneManager != null && context.Controller != null)
                cards = context.ZoneManager.GetCards(context.Controller, Zone.Hand);

            PublishEvent(new RevealCardsEvent { Player = context.Controller, Cards = cards, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => "展示卡牌";
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

    // ---------------- 资源（元素） ----------------

    /// <summary>添加元素（目标/控制者元素池对应颜色 += Value）</summary>
    public class AddManaHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddMana;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            var player = (context.PrimaryTarget as Player) ?? context.Controller;
            if (context.ElementPool == null || player == null || amount == 0) return;

            var pool = context.ElementPool.GetPool(player);
            var mt = effect.ManaTypeParam;
            pool.AvailableMana.TryGetValue(mt, out var cur);
            pool.AvailableMana[mt] = cur + amount;
            PublishEvent(new AddManaEvent { Player = player, ManaType = mt, Amount = amount, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"添加 {effect.Value} 点 {effect.ManaTypeParam} 元素";
    }

    /// <summary>消耗元素（目标/控制者元素池对应颜色 -= Value，下限 0）</summary>
    public class ConsumeManaHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ConsumeMana;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            var player = (context.PrimaryTarget as Player) ?? context.Controller;
            if (context.ElementPool == null || player == null || amount == 0) return;

            var pool = context.ElementPool.GetPool(player);
            var mt = effect.ManaTypeParam;
            pool.AvailableMana.TryGetValue(mt, out var cur);
            int consumed = Math.Min(cur, amount);
            pool.AvailableMana[mt] = cur - consumed;
            PublishEvent(new AddManaEvent { Player = player, ManaType = mt, Amount = -consumed, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"消耗 {effect.Value} 点 {effect.ManaTypeParam} 元素";
    }

    // ---------------- 伤害 / 破坏 ----------------

    /// <summary>基于属性的伤害（以来源当前攻击力对各目标造成伤害）</summary>
    public class DamageBasedOnStatHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DamageBasedOnStat;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dmg = context.Source != null
                ? HandlerHelpers.CurrentPower(context.Source)
                : context.GetValueAfterModifiers(effect.Value);
            if (dmg <= 0) return;

            foreach (var target in context.Targets)
            {
                target.TakeDamage(dmg);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source, Target = target, Damage = dmg,
                    IsCombatDamage = false, DamageType = DamageType.Normal
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "造成等同来源攻击力的伤害";
    }

    /// <summary>随机破坏（从候选目标随机选 Value 个破坏）</summary>
    public class DestroyRandomHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DestroyRandom;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var candidates = context.Targets.OfType<Card>().ToList();
            if (candidates.Count == 0) return;

            int n = effect.Value > 0 ? context.GetValueAfterModifiers(effect.Value) : 1;
            var rng = new Random();
            for (int i = 0; i < n && candidates.Count > 0; i++)
            {
                int idx = rng.Next(candidates.Count);
                var card = candidates[idx];
                candidates.RemoveAt(idx);
                ThirdBatchHelpers.DestroyToGraveyard(card, context);
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"随机破坏 {effect.Value} 个目标";
    }

    // ---------------- 反制 / 无效 ----------------

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

    /// <summary>无效效果（无效化目标）</summary>
    public class NegateEffectHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.NegateEffect;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                target.Nullify();
                PublishEvent(new NullifyEvent { Target = target, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "无效目标的效果";
    }

    // ---------------- 特殊 ----------------

    /// <summary>完全复制（含指示物的属性副本衍生物）</summary>
    public class CopyExactHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.CopyExact;

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
                foreach (var kv in orig._counters) copy._counters[kv.Key] = kv.Value;
                copy.SetController(context.Controller);
                copy.SetZone(Zone.Battlefield);

                context.ZoneManager?.GetZoneContainer(context.Controller)?.Add(copy, Zone.Battlefield);
                PublishEvent(new CardCopiedEvent
                {
                    OriginalCard = orig, Controller = context.Controller, Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "完全复制目标卡牌";
    }

    /// <summary>交换位置（两个目标的控制者互换）</summary>
    public class ExchangePositionHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ExchangePosition;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context)
            => context?.Targets != null && context.Targets.Count >= 2;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var a = context.Targets[0] as Card;
            var b = context.Targets[1] as Card;
            if (a == null || b == null) return;

            var ca = a.GetController();
            var cb = b.GetController();
            HandlerHelpers.ChangeControl(context, a, cb, true);
            HandlerHelpers.ChangeControl(context, b, ca, true);

            PublishEvent(new SwapControllerEvent { Target1 = a, Target2 = b, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => "交换两个目标的位置";
    }

    /// <summary>第三批 handler 工厂</summary>
    public static class ThirdBatchHandlerFactory
    {
        public static IAtomicEffectHandler[] CreateAll()
        {
            return new IAtomicEffectHandler[]
            {
                // 牌库 / 移动
                new MillCardHandler(),
                new PutOnBottomOfDeckHandler(),
                new MoveCardHandler(),
                new MoveToAnyZoneHandler(),
                new DrawThenDiscardHandler(),
                new ScryCardsHandler(),
                new SearchAndRevealHandler(),
                new SearchAndPlayHandler(),
                new RevealCardsHandler(),
                new ChangeOwnerHandler(),

                // 资源
                new AddManaHandler(),
                new ConsumeManaHandler(),

                // 伤害 / 破坏
                new DamageBasedOnStatHandler(),
                new DestroyRandomHandler(),

                // 反制 / 无效
                new NegateActivationHandler(),
                new NegateEffectHandler(),

                // 特殊
                new CopyExactHandler(),
                new ExchangePositionHandler(),
            };
        }
    }
}
