using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 信息族（蓝色）：宣言（当前猜当前验证）/ 预言（隐藏押注，延迟验证）
    // 宣言对象必须是有限明确范围的枚举域——类型/颜色/费用奇偶/费用精确值/箭头方向；
    // 卡名等开放集不可作为宣言对象（用户定案）。
    // StringValue 编码 "维度:值"，如 Type:Creature / Color:Red / CostParity:Odd / CostExact:3 / LinkArrow:NE。
    // ================================================================

    /// <summary>
    /// 宣言维度注册表：维度名 + 域校验 + 对卡匹配置值。
    /// 新增维度 = 加一段 case（域必须有限可枚举）；卡名维度被明确排除。
    /// </summary>
    public static class ProphecyDimension
    {
        public const string Type = "Type";                 // Cardtype（Creature/Spell/...）
        public const string Color = "Color";               // ManaType（Red/Blue/Green/Gray）
        public const string CostParity = "CostParity";     // Odd / Even
        public const string CostExact = "CostExact";       // 0..9（费用受曲线封顶，天然有限）
        public const string LinkArrow = "LinkArrow";       // CardCore.HexDirection 单方向

        private const int CostExactMax = 9; // 与地牌槽上限 9 同源的封顶值

        /// <summary>解析 "维度:值" 编码（无冒号或空串返回 false）</summary>
        public static bool TryParse(string encoded, out string dimension, out string value)
        {
            dimension = value = null;
            if (string.IsNullOrEmpty(encoded)) return false;
            int idx = encoded.IndexOf(':');
            if (idx <= 0 || idx >= encoded.Length - 1) return false;
            dimension = encoded.Substring(0, idx).Trim();
            value = encoded.Substring(idx + 1).Trim();
            return dimension.Length > 0 && value.Length > 0;
        }

        /// <summary>值是否属于该维度的有限域（目录/UI 下拉与运行时共用）</summary>
        public static bool IsValidValue(string dimension, string value)
        {
            switch (dimension)
            {
                case Type:
                    return Enum.TryParse<Cardtype>(value, true, out _);
                case Color:
                    return Enum.TryParse<ManaType>(value, true, out _);
                case CostParity:
                    return value.Equals("Odd", StringComparison.OrdinalIgnoreCase)
                           || value.Equals("Even", StringComparison.OrdinalIgnoreCase);
                case CostExact:
                    return int.TryParse(value, out int n) && n >= 0 && n <= CostExactMax;
                case LinkArrow:
                    return Enum.TryParse<HexDirection>(value, true, out var dir)
                           && dir != HexDirection.None && dir != HexDirection.All
                           && (dir & (dir - 1)) == 0; // 单方向（Flags 枚举取单 bit）
                default:
                    return false;
            }
        }

        /// <summary>卡是否命中宣言（维度:值）。未知维度/非法值恒 false。</summary>
        public static bool Matches(Card card, string dimension, string value)
        {
            if (card == null || !IsValidValue(dimension, value)) return false;

            switch (dimension)
            {
                case Type:
                    return card is IHasSupertype st && st.Supertype == ParseEnum<Cardtype>(value);

                case Color:
                    if (!(card is IHasCost hasCost) || hasCost.Cost == null) return false;
                    var color = ParseEnum<ManaType>(value);
                    return hasCost.Cost.TryGetValue((int)color, out float amount) && amount > 0;

                case CostParity:
                {
                    int total = TotalCost(card);
                    bool odd = total % 2 == 1;
                    return value.Equals("Odd", StringComparison.OrdinalIgnoreCase) ? odd : !odd;
                }

                case CostExact:
                    return TotalCost(card) == int.Parse(value);

                case LinkArrow:
                    return card is CardWrapper wrapper
                           && wrapper.GetData().ArrowDirections.HasFlag(ParseEnum<HexDirection>(value));

                default:
                    return false;
            }
        }

        /// <summary>卡总费用（与 GameActions.GetCardCost 同默认：无费用按灰 1）</summary>
        private static int TotalCost(Card card)
        {
            if (card is IHasCost hasCost && hasCost.Cost != null)
            {
                int sum = 0;
                foreach (var kv in hasCost.Cost) sum += (int)kv.Value;
                return sum;
            }
            return 1;
        }

        private static T ParseEnum<T>(string value) where T : struct
        {
            Enum.TryParse<T>(value, true, out var result);
            return result;
        }
    }

    /// <summary>宣言·确认对手全手牌（即时验证，揭示面 = 全手牌，YGO 确认手札式）</summary>
    public class DeclareHandHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DeclareHand;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            context.LastOutcome.Declaration = effect.StringValue;
            var hand = ProphecyHandlerUtil.OpponentHand(context);
            bool hit = ProphecyHandlerUtil.MatchIn(effect.StringValue, hand, out _);

            context.LastOutcome.DeclareHit = hit;
            PublishEvent(new DeclareResolvedEvent
            {
                Declaration = effect.StringValue,
                Hit = hit,
                Controller = context.Controller,
                RevealedCards = hand != null ? new List<Card>(hand) : new List<Card>(),
                Sampled = false,
            });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"宣言{effect.StringValue}并确认对手全手牌";
    }

    /// <summary>宣言·探查对手手牌（即时验证，揭示面 = 随机 3 张样本，炉石检视式；揭示面小故费用更低）</summary>
    public class DeclareHandSampledHandler : AtomicEffectHandlerBase
    {
        private static readonly Random _rng = new Random();

        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DeclareHandSampled;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            context.LastOutcome.Declaration = effect.StringValue;
            var hand = ProphecyHandlerUtil.OpponentHand(context);
            if (hand == null || hand.Count == 0)
            {
                context.LastOutcome.DeclareHit = false;
                return;
            }

            // 随机取至多 3 张样本，仅在样本内验证
            var pool = new List<Card>(hand);
            int take = Math.Min(3, pool.Count);
            var sample = new List<Card>();
            while (sample.Count < take)
            {
                var pick = pool[_rng.Next(pool.Count)];
                pool.Remove(pick);
                sample.Add(pick);
            }

            bool hit = sample.Any(c => ProphecyHandlerUtil.MatchesDeclaration(effect.StringValue, c));
            context.LastOutcome.DeclareHit = hit;
            PublishEvent(new DeclareResolvedEvent
            {
                Declaration = effect.StringValue,
                Hit = hit,
                Controller = context.Controller,
                RevealedCards = sample,
                Sampled = true,
            });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"宣言{effect.StringValue}并探查对手3张手牌";
    }

    /// <summary>宣言·验牌库顶（即时验证：展示己方牌库顶比对；展示 = 公开）</summary>
    public class DeclareDeckTopHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DeclareDeckTop;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            context.LastOutcome.Declaration = effect.StringValue;

            var deck = context.ZoneManager?.GetCards(context.Controller, Zone.Deck);
            if (deck == null || deck.Count == 0)
            {
                context.LastOutcome.DeclareHit = false; // 无牌可验 = 未命中
                return;
            }

            var top = deck[0]; // index 0 = 牌库顶（ZoneContainer 约定）
            bool hit = ProphecyHandlerUtil.MatchesDeclaration(effect.StringValue, top);
            context.LastOutcome.DeclareHit = hit;
            PublishEvent(new DeclareResolvedEvent
            {
                Declaration = effect.StringValue,
                Hit = hit,
                Controller = context.Controller,
                RevealedCards = new List<Card> { top }, // 展示牌库顶
                Sampled = false,
            });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"宣言{effect.StringValue}并展示己方牌库顶";
    }

    /// <summary>宣言·箭头（即时验证：私密确认对手额外卡组中是否存在带该方向箭头的连接卡，内容不公开）</summary>
    public class DeclareArrowHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DeclareArrow;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            context.LastOutcome.Declaration = effect.StringValue;

            var extra = context.ZoneManager?.GetCards(context.Controller?.Opponent, Zone.ExtraDeck);
            bool hit = extra != null && extra.Any(c => ProphecyHandlerUtil.MatchesDeclaration(effect.StringValue, c));

            context.LastOutcome.DeclareHit = hit;
            PublishEvent(new DeclareResolvedEvent
            {
                Declaration = effect.StringValue,
                Hit = hit,
                Controller = context.Controller,
                RevealedCards = null, // 私密确认：不揭示卡面
                Sampled = false,
            });
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"宣言{effect.StringValue}箭头并确认对手额外卡组";
    }

    /// <summary>
    /// 预言·对手下回合首张（隐藏押注）：本 handler 只登记押注（校验并暂存宣言），
    /// 不写 DeclareHit。紧随其后的分支步骤由执行引擎识别为延迟验证，打包成
    /// PendingProphecy 注册到 ProphecySystem——对手下回合首张出牌时验证，
    /// 整回合未出牌作未命中走 else（用户定案）。
    /// </summary>
    public class ProphecyNextCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ProphecyNextCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 校验宣言域；非法宣言也照常登记（验证时刻按未命中结算，避免组装期垃圾卡死结算）
            if (!ProphecyDimension.TryParse(effect.StringValue, out var dim, out var val)
                || !ProphecyDimension.IsValidValue(dim, val))
            {
                UnityEngine.Debug.LogWarning(
                    $"[Prophecy] 非法宣言编码 '{effect.StringValue}'（应为 维度:值，且值在有限域内），预言将按未命中结算");
            }

            context.LastOutcome.Declaration = effect.StringValue;
        }

        public override string GetDescription(AtomicEffectInstance effect) => $"预言对手下回合首张卡为{effect.StringValue}";
    }

    // ---------------- 共用小工具 ----------------

    internal static class ProphecyHandlerUtil
    {
        internal static List<Card> OpponentHand(EffectExecutionContext context)
        {
            var opponent = context.Controller?.Opponent;
            if (opponent == null || context.ZoneManager == null) return null;
            try { return context.ZoneManager.GetCards(opponent, Zone.Hand); }
            catch (KeyNotFoundException) { return null; }
        }

        internal static bool MatchIn(string encoded, List<Card> cards, out List<Card> hits)
        {
            hits = new List<Card>();
            if (cards == null || !ProphecyDimension.TryParse(encoded, out var dim, out var val)) return false;
            foreach (var c in cards)
                if (ProphecyDimension.Matches(c, dim, val)) hits.Add(c);
            return hits.Count > 0;
        }

        internal static bool MatchesDeclaration(string encoded, Card card)
        {
            return ProphecyDimension.TryParse(encoded, out var dim, out var val)
                   && ProphecyDimension.Matches(card, dim, val);
        }
    }
}
