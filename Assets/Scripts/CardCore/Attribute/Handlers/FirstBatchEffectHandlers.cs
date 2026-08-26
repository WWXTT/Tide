using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 第一批基础效果 — 补齐的 handler（与 BuiltinEffectHandlers 配套）
    // 复用 EntityEffectExtensions / ZoneManagerExtensions 原语
    // ================================================================

    // ---------------- 伤害类 ----------------

    /// <summary>不可防止伤害（无视护甲/防止，直接扣血）</summary>
    public class DamageCannotBePreventedHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DamageCannotBePrevented;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dmg = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int lifeBefore = target.GetLife();
                target.TakeDamage(dmg);
                context.LastOutcome.RecordDamage(target, lifeBefore, dmg);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source,
                    Target = target,
                    Damage = dmg,
                    IsCombatDamage = false,
                    DamageType = DamageType.Normal
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"造成 {effect.Value} 点不可防止的伤害";
    }

    /// <summary>吸取生命（对目标造成伤害，控制者回复等量生命）</summary>
    public class DrainLifeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DrainLife;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dmg = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int lifeBefore = target.GetLife();
                target.TakeDamage(dmg);
                context.LastOutcome.RecordDamage(target, lifeBefore, dmg);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source,
                    Target = target,
                    Damage = dmg,
                    IsCombatDamage = false,
                    DamageType = DamageType.LifeLoss
                });
            }

            if (context.Controller != null)
            {
                context.Controller.Heal(dmg);
                PublishEvent(new HealEvent { Target = context.Controller, Amount = dmg, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"吸取 {effect.Value} 点生命";
    }

    /// <summary>剧毒伤害（造成伤害并附加剧毒关键词）</summary>
    public class PoisonousDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.PoisonousDamage;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dmg = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int lifeBefore = target.GetLife();
                if (dmg > 0)
                {
                    target.TakeDamage(dmg);
                    PublishEvent(new AtomicDamageEvent
                    {
                        Source = context.Source,
                        Target = target,
                        Damage = dmg,
                        IsCombatDamage = false,
                        DamageType = DamageType.Poison
                    });
                }
                // 任何受到剧毒伤害的生物直接死亡（炉石/万智的剧毒语义）
                if (target is Card card && card.IsAlive)
                {
                    target.IsAlive = false;
                    var controller = card.GetController();
                    if (context.ZoneManager != null && controller != null)
                        context.ZoneManager.MoveCard(card, controller, Zone.Battlefield, Zone.Graveyard);
                    PublishEvent(new CardDestroyEvent
                    {
                        DestroyedCard = card,
                        Reason = DestroyReason.Destroyed,
                        Source = context.Source
                    });
                }
                // 死亡检测在强制致死后进行，剧毒目标计入 KilledTargets
                context.LastOutcome.RecordDamage(target, lifeBefore, dmg);
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "造成剧毒伤害";
    }

    /// <summary>恢复满生命</summary>
    public class RestoreToFullLifeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RestoreToFullLife;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                int lifeBefore = target.GetLife();
                int missing = target.GetMaxLife() - lifeBefore;
                if (missing > 0)
                {
                    target.Heal(missing);
                    PublishEvent(new HealEvent { Target = target, Amount = missing, Source = context.Source });
                }
                // 恢复满生命必回满 → 申请量取 missing，溢出恒为 0；仍记录 HealApplied / AffectedTargets
                context.LastOutcome.RecordHeal(target, lifeBefore, missing > 0 ? missing : 0);
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "恢复满生命";
    }

    // ---------------- 卡牌移动 / 牌库操作 ----------------

    /// <summary>弃牌（从控制者手牌弃 N 张到坟墓场）</summary>
    public class DiscardCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DiscardCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager == null || context.Controller == null) return;

            var hand = context.ZoneManager.GetCards(context.Controller, Zone.Hand);
            for (int i = 0; i < count && i < hand.Count; i++)
            {
                var card = hand[i];
                context.ZoneManager.GetZoneContainer(context.Controller).Move(card, Zone.Hand, Zone.Graveyard);
                PublishEvent(new CardDiscardEvent { Player = context.Controller, Card = card, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"弃掉 {effect.Value} 张牌";
    }

    /// <summary>除外（将目标移入流放区）</summary>
    public class ExileHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Exile;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    var fromZone = card.GetZone();
                    var controller = card.GetController();
                    if (context.ZoneManager != null && controller != null)
                        context.ZoneManager.GetZoneContainer(controller).Move(card, fromZone, Zone.Exile);
                    PublishEvent(new CardExileEvent { Card = card, Source = context.Source, FromZone = fromZone });
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "将目标除外";
    }

    /// <summary>洗入牌库（将目标随机洗回拥有者牌库）</summary>
    public class ShuffleIntoDeckHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ShuffleIntoDeck;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    var owner = card.GetOwner() ?? card.GetController();
                    var fromZone = card.GetZone();
                    if (context.ZoneManager != null && owner != null)
                    {
                        context.ZoneManager.GetZoneContainer(owner).Move(card, fromZone, Zone.Deck);
                        context.ZoneManager.ShuffleDeck(owner);
                    }
                    PublishEvent(new CardShuffleIntoDeckEvent { Card = card, Source = context.Source });
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "将目标洗入牌库";
    }

    /// <summary>检索牌库（查看牌库顶 N 张并洗牌；UI 选择后续实现）</summary>
    public class SearchDeckHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SearchDeck;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager == null || context.Controller == null) return;

            var seen = context.ZoneManager.GetTopCards(context.Controller, count);
            // 暂以抽到手牌作为检索的占位结算（实际选择交互后续接入 UI）
            for (int i = 0; i < count; i++)
            {
                var drawn = context.ZoneManager.DrawCard(context.Controller);
                if (drawn == null) break;
                PublishEvent(new CardDrawEvent { Player = context.Controller, DrawnCard = drawn, DrawCount = 1 });
            }
            PublishEvent(new RevealCardsEvent { Player = context.Controller, Cards = seen, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"从牌库检索 {effect.Value} 张牌";
    }

    /// <summary>弹回牌库顶</summary>
    public class BounceToTopHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.BounceToTop;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    var owner = card.GetOwner() ?? card.GetController();
                    var fromZone = card.GetZone();
                    if (context.ZoneManager != null && owner != null)
                        context.ZoneManager.GetZoneContainer(owner).Move(card, fromZone, Zone.Deck, DeckPosition.Top);
                    PublishEvent(new CardReturnToHandEvent { Card = card, Source = context.Source });
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "将目标放回牌库顶";
    }

    /// <summary>弹回牌库底</summary>
    public class BounceToBottomHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.BounceToBottom;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    var owner = card.GetOwner() ?? card.GetController();
                    var fromZone = card.GetZone();
                    if (context.ZoneManager != null && owner != null)
                        context.ZoneManager.GetZoneContainer(owner).Move(card, fromZone, Zone.Deck, DeckPosition.Bottom);
                    PublishEvent(new CardReturnToHandEvent { Card = card, Source = context.Source });
                }
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "将目标放回牌库底";
    }

    /// <summary>墓地返回（将目标从坟墓场放回战场）</summary>
    public class ReturnFromGraveyardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ReturnFromGraveyard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            int count = effect.Value > 0 ? context.GetValueAfterModifiers(effect.Value) : 1;
            // 仅「正式召唤过」的随从可被复活；被弃/磨/送墓的不可复活。
            var revivable = context.ZoneManager.GetCards(context.Controller, Zone.Graveyard)
                .Where(c => c.WasFormallySummoned)
                .ToList();
            for (int i = 0; i < count && i < revivable.Count; i++)
            {
                var card = revivable[i];
                context.ZoneManager.GetZoneContainer(context.Controller).Move(card, Zone.Graveyard, Zone.Battlefield);
                card.SetController(context.Controller);
                card.SetZone(Zone.Battlefield);
                card.WasFormallySummoned = true; // 复活也是一次正式入场

                PublishEvent(new CardPutToBattlefieldEvent
                {
                    Card = card,
                    Controller = context.Controller,
                    Tapped = false
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "从墓地返回到战场";
    }

    /// <summary>墓地回收到手牌（将控制者墓地前 N 张放回手牌；费用锚点：回收 1 张 = 1 费）。</summary>
    public class RecoverToHandHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RecoverToHand;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            int count = effect.Value > 0 ? context.GetValueAfterModifiers(effect.Value) : 1;
            // 优先回收已选目标卡；无目标时取控制者墓地前 N 张（快照避免迭代中移动）
            var targeted = context.Targets?.OfType<Card>().ToList();
            List<Card> toRecover = (targeted != null && targeted.Count > 0)
                ? targeted.Take(count).ToList()
                : context.ZoneManager.GetCards(context.Controller, Zone.Graveyard).Take(count).ToList();

            foreach (var card in toRecover)
            {
                context.ZoneManager.MoveCard(card, context.Controller, Zone.Graveyard, Zone.Hand);
                PublishEvent(new CardReturnToHandEvent { Card = card, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "从墓地回收到手牌";
    }

    /// <summary>查看牌库顶（不移动，仅展示）</summary>
    public class LookAtTopCardsHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.LookAtTopCards;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager == null || context.Controller == null) return;

            var cards = context.ZoneManager.GetTopCards(context.Controller, count);
            PublishEvent(new ScryEvent { Player = context.Controller, Cards = cards, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"查看牌库顶 {effect.Value} 张牌";
    }

    /// <summary>展示手牌（展示对手手牌）</summary>
    public class RevealHandHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RevealHand;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 目标解析为对手玩家；若无目标则退化为控制者对手
            var targetPlayer = context.Targets.OfType<Player>().FirstOrDefault()
                ?? context.Controller?.Opponent;
            if (context.ZoneManager == null || targetPlayer == null) return;

            var hand = context.ZoneManager.GetCards(targetPlayer, Zone.Hand);
            PublishEvent(new RevealHandEvent { Player = targetPlayer, Cards = hand, Source = context.Source });
        }

        public override string GetDescription(AtomicEffectInstance effect) => "展示对手手牌";
    }

    // ---------------- 状态变更 ----------------

    /// <summary>修改生命值</summary>
    public class ModifyLifeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyLife;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int oldLife = target.GetLife();
                target.ModifyLife(amount);
                if (target is Card card && amount > 0) card._maxLife += amount;
                PublishEvent(new StatModifyEvent
                {
                    Target = target,
                    StatType = StatType.Life,
                    OldValue = oldLife,
                    NewValue = target.GetLife(),
                    Delta = amount,
                    Duration = effect.Duration,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            string sign = effect.Value >= 0 ? "+" : "";
            return $"生命值 {sign}{effect.Value}";
        }
    }

    /// <summary>设置攻击力</summary>
    public class SetPowerHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SetPower;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int value = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int oldPower = target.GetPower();
                target.SetPower(value);
                PublishEvent(new StatSetEvent
                {
                    Target = target,
                    StatType = StatType.Power,
                    OldValue = oldPower,
                    NewValue = value,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"将攻击力设为 {effect.Value}";
    }

    /// <summary>设置生命值</summary>
    public class SetLifeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SetLife;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int value = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int oldLife = target.GetLife();
                target.SetLife(value);
                if (target is Card card && value > card._maxLife) card._maxLife = value;
                PublishEvent(new StatSetEvent
                {
                    Target = target,
                    StatType = StatType.Life,
                    OldValue = oldLife,
                    NewValue = value,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"将生命值设为 {effect.Value}";
    }

    /// <summary>修改费用</summary>
    public class ModifyCostHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyCost;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int oldCost = target.GetCost();
                target.ModifyCost(amount);
                PublishEvent(new CostModifyEvent
                {
                    Target = target,
                    OldCost = oldCost,
                    NewCost = target.GetCost(),
                    Delta = amount,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            string sign = effect.Value >= 0 ? "+" : "";
            return $"费用 {sign}{effect.Value}";
        }
    }
}
