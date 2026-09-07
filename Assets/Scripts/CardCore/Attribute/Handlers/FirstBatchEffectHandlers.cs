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

    /// <summary>
    /// 穿透伤害（定案，原"不可防止伤害"改名）：越过关键词和指示物计算伤害——
    /// 跳过圣盾/护甲指示物/坚韧，但受光环限制（替代引擎/层效果照走），事件链/吸血照常。
    /// </summary>
    public class PierceDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.PierceDamage;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dmg = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int lifeBefore = target.GetLife();
                int actual = KeywordRules.ApplyDamage(context.Source, target, dmg, false, pierce: true);
                context.LastOutcome.RecordDamage(target, lifeBefore, actual);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source,
                    Target = target,
                    Damage = actual,
                    IsCombatDamage = false,
                    DamageType = DamageType.Normal
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"造成 {effect.Value} 点穿透伤害（无视关键词与指示物）";
    }

    /// <summary>吸取生命（对目标造成伤害，控制者回复等量生命）</summary>
    public class DrainLifeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DrainLife;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int dmg = context.GetValueAfterModifiers(effect.Value);
            int drained = 0;
            foreach (var target in context.Targets)
            {
                int lifeBefore = target.GetLife();
                target.TakeDamage(dmg, context.Source); // 关键词管线
                int actual = System.Math.Max(0, lifeBefore - target.GetLife());
                drained += actual;
                context.LastOutcome.RecordDamage(target, lifeBefore, actual);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source,
                    Target = target,
                    Damage = dmg,
                    IsCombatDamage = false,
                    DamageType = DamageType.LifeLoss
                });
            }

            if (context.Controller != null && drained > 0)
            {
                int lifeBefore = context.Controller.GetLife();
                context.Controller.Heal(drained);
                int overfill = System.Math.Max(0, drained - (context.Controller.GetLife() - lifeBefore));
                PublishEvent(new HealEvent { Target = context.Controller, Amount = drained, Overfill = overfill, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"吸取 {effect.Value} 点生命";
    }

    /// <summary>
    /// 剧毒指示物（定案，原"剧毒伤害"改语义）：对目标附加剧毒指示物——
    /// 持续 1 回合，回合结束时持有者死亡（效果死亡、无伤害来源，CounterRules 统一裁决；
    /// 神佑经决策表拦截，指示物照常到期消失）。不再造成即时伤害。
    /// </summary>
    public class PoisonHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Poison;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.PoisonCounter, 1);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.PoisonCounter,
                    Amount = 1,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => "附加剧毒指示物（回合结束时死亡）";
    }

    // ---------------- 卡牌移动 / 牌库操作 ----------------

    /// <summary>
    /// 弃牌（定案改语义）：对手从手牌中自选弃掉 {value} 张牌。
    /// 选择器 = 被弃方（对手）；P1 以可插拔启发式代选（AI=按价值升序弃最差，人类 UI 后续接入）。
    /// </summary>
    public class DiscardCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DiscardCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager == null || context.Controller == null) return;

            // 目标 = 对手（表 TargetType=Opponent；无解析目标时退化取控制者对手）
            var victim = context.Targets.OfType<Player>().FirstOrDefault()
                ?? context.Controller.Opponent;
            if (victim == null) return;

            var hand = context.ZoneManager.GetCards(victim, Zone.Hand).ToList();
            // 自选启发式（代弃方视角）：价值升序弃最差——费用低→攻击低 优先
            var picks = hand.OrderBy(c => c.GetCost()).ThenBy(c => c.GetPower()).Take(count);
            foreach (var card in picks)
            {
                context.ZoneManager.GetZoneContainer(victim).Move(card, Zone.Hand, Zone.Graveyard);
                PublishEvent(new CardDiscardEvent { Player = victim, Card = card, Source = context.Source });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"对手从手牌中自选弃掉 {effect.Value} 张牌";
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

    /// <summary>
    /// 检索牌库（定案改语义）：宣言一个卡名（StringValue 构筑期预置），从牌库检索对应的卡入手。
    /// 命中 = 卡名或卡 ID 匹配宣言文本；未命中 = 空手而归（宣言分支可挂 DeclareHit/Miss——
    /// 命中写 LastOutcome.DeclareHit 供紧邻分支判定）。
    /// </summary>
    public class SearchDeckHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SearchDeck;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            string declared = effect.StringValue;
            if (string.IsNullOrEmpty(declared)) return; // 未预置宣言名：不检索

            var deck = context.ZoneManager.GetCards(context.Controller, Zone.Deck).ToList();
            var found = deck.FirstOrDefault(c => MatchesDeclaration(c, declared));
            if (found == null)
            {
                context.LastOutcome.DeclareHit = false;
                context.LastOutcome.Declaration = declared;
                return;
            }

            context.ZoneManager.MoveCard(found, context.Controller, Zone.Deck, Zone.Hand);
            PublishEvent(new RevealCardsEvent
            {
                Player = context.Controller,
                Cards = new List<Card> { found },
                Source = context.Source
            });
            context.LastOutcome.DeclareHit = true;
            context.LastOutcome.Declaration = declared;
        }

        /// <summary>宣言匹配：卡名（CardData.CardName）或卡 ID 等值（构筑期预置的宣言文本）。</summary>
        private static bool MatchesDeclaration(Card card, string declared)
        {
            if (card.ID == declared) return true;
            if (card is CardWrapper wrapper && wrapper.GetData()?.CardName == declared) return true;
            return false;
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"宣言「{effect.StringValue}」并从牌库检索对应的卡";
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

    /// <summary>苏生（原名"墓地返回"，2026-09-03 改名）：将目标从坟墓场放回战场</summary>
    public class ReturnFromGraveyardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ReturnFromGraveyard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            int count = effect.Value > 0 ? context.GetValueAfterModifiers(effect.Value) : 1;
            // 仅「正式召唤过」的随从可被复活；被弃/送墓的不可复活。
            var revivable = context.ZoneManager.GetCards(context.Controller, Zone.Graveyard)
                .Where(c => c.WasFormallySummoned)
                .ToList();
            for (int i = 0; i < count && i < revivable.Count; i++)
            {
                var card = revivable[i];
                // 经入场容量闸门：满则失败——卡留在墓地（来源即墓地，无移动），发失败事件
                if (!context.ZoneManager.TryMoveToBattlefield(card, context.Controller, Zone.Graveyard))
                    continue;
                card.SetController(context.Controller);
                card.WasFormallySummoned = true; // 复活也是一次正式入场
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

    // 展示手牌原子已删除（2026-09-03 原子表整体修正）——
    // 手牌可见性收敛为宣言确认手牌的单张验证（ProphecyHandlers.DeclareHandHandler，按张发 RevealHandEvent）。

    // ---------------- 状态变更 ----------------

    /// <summary>
    /// 修改生命值（指示物形式定案）：正值加"生命值增加"层（上限与当前同加）、
    /// 负值加"生命值减少"层（减上限、归零标死交 SBA）；仅场上存在，离场消失（反向回写）。
    /// </summary>
    public class ModifyLifeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyLife;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                int oldLife = target.GetLife();
                if (amount >= 0) CounterRules.AddStatCounter(card, CounterRules.LifeUpCounter, amount);
                else CounterRules.AddStatCounter(card, CounterRules.LifeDownCounter, -amount);
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
            return $"生命值 {sign}{effect.Value}（指示物）";
        }
    }

    /// <summary>设置攻击力</summary>
    // ==================== 设置系（2026-09-07 定案：归为规则系能力，暂时下线） ====================
    // 同一文本描述的赋予将按【来源分轨】（大改动另行计划）：生物效果=临时指示物 / 魔法卡=设置类（永久直改）
    // / 连接箭头=光环。统一指示物是为了让增益长在实体身上、可被净化等效果交互——直改原属性绕过该体系，
    // 故设置系 handler 先下线（#if false），枚举与表行保留（数据兼容），待三轨制重建时恢复。
#if false
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

    /// <summary>
    /// 设置费用（新增原子）：直接改卡的基本费用（_baseCost），持续到游戏结束——
    /// 与设置攻击力/生命值同族（直改基本属性、跨区保留），区别于费用指示物（仅手牌、离手消失）。
    /// 目标=手牌卡。
    /// </summary>
    public class SetCostHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SetCost;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int value = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;
                int oldCost = target.GetCost();
                target.SetCost(value);
                PublishEvent(new CostModifyEvent
                {
                    Target = target,
                    OldCost = oldCost,
                    NewCost = target.GetCost(),
                    Delta = value - oldCost,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"将费用设为 {effect.Value}";
    }
#endif // 设置系暂时下线（规则系能力，三轨制重建时恢复）

    /// <summary>
    /// 修改费用（指示物形式定案）：正值加"费用增加"层、负值加"费用减少"层（单向粒度）——
    /// 加时即回写 _costModifier（GetCost = _baseCost + _costModifier），仅手牌生效、离手消失。
    /// </summary>
    public class ModifyCostHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyCost;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || card.GetZone() != Zone.Hand) continue; // 费用指示物仅存在手牌
                int oldCost = target.GetCost();
                if (amount >= 0) CounterRules.AddStatCounter(card, CounterRules.CostUpCounter, amount);
                else CounterRules.AddStatCounter(card, CounterRules.CostDownCounter, -amount);
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
            return $"费用 {sign}{effect.Value}（指示物）";
        }
    }
}
