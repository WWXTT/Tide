using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 展示原子（信息轴，2026-10-02 定案）：为目标的卡挂「展示」指示物（CounterRules.ExposedCounter）——
    /// 被展示的卡持续暴露，双方可点击对应区域查看（UI 读快照 RevealedZoneCards / RevealRules 查询口）；
    /// 换区即失效（CounterRules 换区清统一口径——二值状态不走层计时）。
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
    /// 诅咒原子（信息轴，2026-10-02 定案；2026-10-05 有限分支定案；**2026-10-08 表行退役**——
    /// 附加诅咒改走自由分支·引擎主干行 EngineCurseOnDraw（BranchEngines 施放结算部署，指示物归系统），
    /// 本 handler 保留代码侧兼容手写数据（无表行不生效，AdditionalEnergy 同款先例）：
    /// 为对手的卡附加「诅咒」指示物（CurseCounter——Exception 生效自减档，活过牌库→手牌的换区清除）
    /// 并登记载荷（CurseSystem.Attach）。对手抽到该卡时自动执行载荷分支效果并消层（一次性，CurseSystem 驱动）。
    /// 载荷=str = 载荷效果 id（Effects.json 条目引用——手写数据兼容）。
    /// Value = 附加数量（≤0 取 1）；目标已预选则逐卡附加（如点名已展示的对手手牌卡——信息轴联动），
    /// None 自结算 = 随机对手牌库一张/次（炉石式——不给施放者看对方牌库）。
    /// 效果/指示物分离定案：本原子只负责投放，分支规则由指示物 + CurseSystem 承载。
    /// </summary>
    public class AddCurseHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddCurse;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            string payloadId = effect.StringValue;
            if (string.IsNullOrEmpty(payloadId))
            {
                TideLog.Warn("[AddCurseHandler] 缺少载荷（str 未引用 Effects.json 条目），空转");
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
                CurseSystem.Attach(card, payloadId, context.Controller, sourceCard);
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
            => $"附加诅咒（抽到该卡时执行分支效果：{effect.StringValue}）";
    }

    /// <summary>
    /// 筛选展示（信息轴·维度筛选，2026-10-29 阶段3c，百闻牌读心参考）：按宣言维度
    /// （StringValue="维度:值"，ProphecyDimension 编码）展示{target}手牌中**所有命中**的牌——
    /// 命中者挂「展示」指示物（ExposedCounter，当前展示态，换区清）并按张发布
    /// CounterChangedEvent + RevealCardsEvent（RevealCardHandler 同款口径，二值不叠层）。
    /// 与 RevealCard（随机一张）的分野：本原子按维度圈定全集（"展示对方手牌中所有生物"）。
    /// 维度全量可用（Type/Color/CostParity/CostExact/LinkArrow/Name——Name=卡名精确匹配，
    /// 读心「展示某式神全部专属牌」的载体）；str 空/非法 → 空转并告警。
    /// 域=隐藏区手牌（5/6）：None 自结算不弹选——按原子域取对应侧手牌全量筛选。
    /// </summary>
    public class RevealByDimensionHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RevealByDimension;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            if (!ProphecyDimension.TryParse(effect.StringValue, out var dim, out var val)
                || !ProphecyDimension.IsValidValue(dim, val))
            {
                TideLog.Warn(
                    $"[RevealByDimension] 非法筛选编码 '{effect.StringValue}'（应为 维度:值，且值在该维度域内），空转");
                return;
            }

            foreach (var target in ResolveTargets(effect, context))
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                if (!ProphecyDimension.Matches(card, dim, val)) continue;
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

        /// <summary>目标解析：预选目标沿用（点名已展示卡等信息轴联动）；None 模式按原子域自结算
        ///——手牌域取对应侧全量（隐藏区不给施放者选择权，维度筛选即全部结果）。</summary>
        private static IEnumerable<Entity> ResolveTargets(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.Targets != null && context.Targets.Count > 0) return context.Targets;

            var picked = new List<Entity>();
            var kinds = effect.TargetKinds ?? new List<int>();
            foreach (var kind in kinds)
            {
                var (zone, own) = TargetKindRules.ZoneOf(kind);
                if (zone != Zone.Hand) continue; // 筛选展示=手牌信息原子（表行域=对方手牌；其他域跳过）
                var player = own ? context.Controller : context.Controller.Opponent;
                var cards = context.ZoneManager.GetCards(player, zone);
                if (cards != null) picked.AddRange(cards);
            }
            return picked;
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"展示{{target}}手牌中所有符合「{effect.StringValue}」的牌";
    }
}
