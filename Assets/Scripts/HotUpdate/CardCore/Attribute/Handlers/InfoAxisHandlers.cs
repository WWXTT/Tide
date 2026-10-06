using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 展示原子（信息轴，2026-10-02 定案）：为目标的卡挂「展示」指示物（CounterRules.ExposedCounter）——
    /// 被展示的卡持续暴露，双方可点击对应区域查看（UI 读快照 RevealedZoneCards / RevealRules 查询口）；
    /// 换区即失效（UntilLeaveBattlefield，CounterRules 换区清除统一口径）。
    /// 作用域四域全开：己/对方手牌（5/6）、己/对方牌库（7/8）——表行默认，卡设计可收窄。
    /// 目标已预选（组合层解析，如点名自己手牌/已展示的对手手牌卡）则逐卡展示；
    /// None 自结算按原子域取卡：牌库=顶一张（index 0 = 顶，容器约定）、手牌=随机一张
    /// （隐藏区不向施放者给选择权——随机即结果，不泄露信息）。
    /// </summary>
    public class RevealCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RevealCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            foreach (var target in ResolveTargets(effect, context))
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                if (card.GetCounterCount(CounterRules.ExposedCounter) > 0) continue; // 已展示不叠层（二值状态）

                card.AddCounters(CounterRules.ExposedCounter, 1, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = card,
                    CounterType = CounterRules.ExposedCounter,
                    Amount = 1,
                    Source = context.Source,
                });
                PublishEvent(new RevealCardsEvent
                {
                    Player = card.GetController(),
                    Cards = new List<Card> { card },
                    Source = context.Source,
                });
            }
        }

        /// <summary>目标解析：预选目标沿用；None 模式按原子域自结算（牌库顶/手牌随机）。</summary>
        private static IEnumerable<Entity> ResolveTargets(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.Targets != null && context.Targets.Count > 0) return context.Targets;

            var picked = new List<Entity>();
            var kinds = effect.TargetKinds ?? new List<int>();
            foreach (var kind in kinds)
            {
                var (zone, own) = TargetKindRules.ZoneOf(kind);
                if (zone == Zone.None) continue;
                var player = own ? context.Controller : context.Controller.Opponent;
                var cards = context.ZoneManager.GetCards(player, zone);
                if (cards == null || cards.Count == 0) continue;
                picked.Add(zone == Zone.Deck ? cards[0] : cards[GameRng.Next(0, cards.Count)]);
            }
            return picked;
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "展示{target}（持续暴露，双方可查看，换区失效）";
    }

    /// <summary>
    /// 诅咒原子（信息轴，2026-10-02 定案；2026-10-05 有限分支定案）：为对手的卡附加「诅咒」指示物
    /// （CurseCounter，Permanent——活过牌库→手牌的换区清除）并登记载荷（CurseSystem.Attach）。
    /// 对手抽到该卡时自动执行载荷分支效果并消层（一次性，CurseSystem 驱动；时机固定=抽到时）。
    /// 载荷两形态：①inline——槽级 Branch 载荷（有限分支 Gate 特例 CurseOnDraw，Then 原子 ≤2 费预算，
    /// 优先消费）；②str = 载荷效果 id（Effects.json 条目引用——手写数据兼容）。
    /// Value = 附加数量（≤0 取 1）；目标已预选则逐卡附加（如点名已展示的对手手牌卡——信息轴联动），
    /// None 自结算 = 随机对手牌库一张/次（炉石式——不给施放者看对方牌库）。
    /// 效果/指示物分离定案：本原子只负责投放，分支规则由指示物 + CurseSystem 承载。
    /// </summary>
    public class AddCurseHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCurse;

        /// <summary>inline 载荷 = 槽级 CurseOnDraw 门（有限分支特例）的 Then 原子列。</summary>
        private static List<AtomicEffectInstance> InlinePayloadOf(AtomicEffectInstance effect)
            => effect.Branch != null
               && effect.Branch.Settle == BranchSettleKind.Gate
               && effect.Branch.GateId == ComposerCatalog.CurseGateId
               && effect.Branch.Then != null && effect.Branch.Then.Count > 0
                ? effect.Branch.Then
                : null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            var inlineSteps = InlinePayloadOf(effect);
            string payloadId = effect.StringValue;
            if (inlineSteps == null && string.IsNullOrEmpty(payloadId))
            {
                TideLog.Warn("[AddCurseHandler] 缺少载荷（CurseOnDraw 门 Then 空且 str 未引用 Effects.json 条目），空转");
                return;
            }

            int count = context.GetValueAfterModifiers(effect.Value);
            if (count <= 0) count = 1;

            var targets = new List<Card>();
            if (context.Targets != null && context.Targets.Count > 0)
                targets.AddRange(context.Targets.OfType<Card>());
            else
            {
                // None 自结算：随机对手牌库一张/次
                var deck = context.ZoneManager.GetCards(context.Controller.Opponent, Zone.Deck);
                if (deck == null || deck.Count == 0) return;
                for (int i = 0; i < count; i++)
                    targets.Add(deck[GameRng.Next(0, deck.Count)]);
            }

            var sourceCard = context.CastCard ?? context.Source as Card;
            foreach (var card in targets)
            {
                if (card == null || !card.IsAlive) continue;
                card.AddCounters(CounterRules.CurseCounter, 1, context.Controller);
                if (inlineSteps != null) CurseSystem.Attach(card, inlineSteps, context.Controller, sourceCard);
                else CurseSystem.Attach(card, payloadId, context.Controller, sourceCard);
                PublishEvent(new CounterChangedEvent
                {
                    Target = card,
                    CounterType = CounterRules.CurseCounter,
                    Amount = 1,
                    Source = context.Controller,
                });
                PublishEvent(new KeywordAppliedEvent
                {
                    Target = card,
                    Keyword = "诅咒",
                    Detail = "诅咒附加：抽到该卡时自动执行诅咒分支效果（一次性）",
                    Source = context.Controller,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => InlinePayloadOf(effect) != null
                ? $"附加诅咒（抽到该卡时执行 ≤2 费专属载荷，一次性）"
                : $"附加诅咒（抽到该卡时执行分支效果：{effect.StringValue}）";
    }
}
