using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using UnityEngine;

namespace CardCore
{
    /// <summary>一行计价明细：标签 + 数值 + 颜色桶 + 阶段（S/K/E/f/Req/O）。</summary>
    public sealed class CostBreakdownLine
    {
        public string Label;
        public float Value;
        public ManaType? Color;
        public string Stage;

        public CostBreakdownLine(string stage, string label, float value, ManaType? color = null)
        {
            Stage = stage;
            Label = label;
            Value = value;
            Color = color;
        }
    }

    /// <summary>统一计价结果 —— 规则一·平衡的完整输出。</summary>
    public sealed class CardCostResult
    {
        /// <summary>声明档位 C（costList 总和；0=未声明）。</summary>
        public int DeclaredTier;

        /// <summary>建议档位 Ĉ = min{C∈[1..MaxTier] : D(C) ≤ C}（无满足则 MaxTier）。</summary>
        public int SuggestedTier;

        /// <summary>推导价 D(C)，逐色 AwayFromZero 取整。</summary>
        public Dictionary<ManaType, int> DerivedCost = new Dictionary<ManaType, int>();

        /// <summary>建议采纳的费用分布 = D(Ĉ) 逐色取整（总和 ≤ Ĉ，按实际价值采纳不强行补齐）。</summary>
        public Dictionary<ManaType, int> SuggestedCost = new Dictionary<ManaType, int>();

        /// <summary>身材费 S（灰桶）。</summary>
        public int S;

        /// <summary>关键词费 K（Grant 原子固定费合计）。</summary>
        public int K;

        /// <summary>效果锚价 E（DeriveElementCosts 口径合计）。</summary>
        public int EAnchor;

        /// <summary>挂载折扣因子 f（法术恒 1）。</summary>
        public float Factor;

        /// <summary>挂载效果定义数 N_active。</summary>
        public int ActiveEffectCount;

        /// <summary>D 逐色取整后的总和。</summary>
        public int DerivedTotal;

        /// <summary>抵扣需求 Req = max(0, D_total − C_total)。无"超模"态：不足即不符合规则一。</summary>
        public int OffsetRequirement;

        /// <summary>卡上代价提供的元素当量 O（按 CardCostConfig 当量表换算）。</summary>
        public float OffsetProvided;

        /// <summary>符合规则一 ⟺ O ≥ Req。</summary>
        public bool Conformant;

        /// <summary>逐行明细（S / K 逐关键词 / E 逐效果 / f / d(C) / Req / O）。</summary>
        public List<CostBreakdownLine> Breakdown = new List<CostBreakdownLine>();
    }

    /// <summary>
    /// 统一卡牌计价服务 —— 规则一·平衡的唯一推导入口（UI 建议价与校验共用，不再有第二套公式）。
    ///
    ///   锚价（即时价，法术定价）= CostDerivationService.DeriveElementCosts（原样复用，含持续折扣）。
    ///   身材费 S(灰)  = (攻+血) / StatUnit（StatUnit=2：1费=2属性）。
    ///   关键词费 K    = Σ 关键词对应 Grant 原子的 BaseCost（固定费，颜色=原子亲和；White/Black 归灰）。
    ///   挂载折扣 f    = 法术恒 1；随从 max(0, d(C) − ExtraActivationSlope×(N_active−1))。
    ///                  ——落地时间（费用C=最早第C回合落地，延迟C−1）与存活期望（每多一回合发动再折）两个来源。
    ///   D(C) = S灰 + 挂载包×f（减免发生在组合层：包总额一次取整，最大余数法分色）；D 只算一次，不迭代。
    ///   档位 C = costList 总和，是推导的输入；支付/地牌指示物/UI/召唤门槛照读声明值，不受 D 影响。
    ///   抵扣需求 Req = max(0, D_total − C_total)，由卡上代价在构筑期抵消（O ≥ Req 即符合规则一）。
    /// </summary>
    public static class CardCostService
    {
        /// <summary>主入口：以声明档位 C 为输入推导完整结果。</summary>
        public static CardCostResult Derive(CardData card)
        {
            var result = new CardCostResult();
            if (card == null) return result;

            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig();
            var cc = cfg.CardCostConfig;
            var dd = cfg.DelayDiscountConfig;

            result.DeclaredTier = Mathf.RoundToInt(card.Cost?.Values.Sum() ?? 0f);
            bool isSpell = card.Supertype == Cardtype.Spell;

            // ---- 1) 身材费 S（灰桶；身材不是挂载效果，不参与 f）----
            int statPoints = (card.Power ?? 0) + (card.Life ?? 0);
            float statValue = statPoints / Mathf.Max(1f, cc.StatUnit);
            result.Breakdown.Add(new CostBreakdownLine("S", $"身材费 (攻{card.Power ?? 0}/血{card.Life ?? 0})", statValue, ManaType.Gray));

            // ---- 2) 关键词费 K（Grant 原子固定费，无 value 缩放）----
            float keywordTotal = 0f;
            var kwBuckets = new Dictionary<ManaType, float>();
            if (card.Keywords != null)
            {
                CardLoader.LoadKeywords(); // 惰性建目录（GetKeywordDefinition 不自动建）
                foreach (var kwId in card.Keywords)
                {
                    if (string.IsNullOrEmpty(kwId)) continue;
                    var def = CardLoader.GetKeywordDefinition(kwId);
                    if (def == null || string.IsNullOrEmpty(def.atomicEffect)
                        || !Enum.TryParse<AtomicEffectType>(def.atomicEffect, out var grantType))
                    {
                        result.Breakdown.Add(new CostBreakdownLine("K", $"关键词 {kwId}（未登记 Grant 原子，计 0）", 0f));
                        continue;
                    }

                    var atomCfg = AtomicEffectTable.GetByType(grantType);
                    float baseCost = atomCfg?.BaseCost ?? 0f;
                    var color = ElementAffinities.GetAffinityForEffect(grantType).PrimaryColor;
                    kwBuckets.TryGetValue(color, out var prev);
                    kwBuckets[color] = prev + baseCost;
                    keywordTotal += baseCost;
                    result.Breakdown.Add(new CostBreakdownLine("K", $"关键词 {kwId} (Grant 固定费)", baseCost, color));
                }
            }

            // ---- 3) 效果锚价 E（经 CardEffectConverter，与运行时执行同口径）----
            float effectTotal = 0f;
            var effBuckets = new Dictionary<ManaType, float>();
            var effectDefs = CardEffectConverter.ConvertAll(card.Effects, card.ID);
            foreach (var def in effectDefs)
            {
                if (def == null) continue;
                result.ActiveEffectCount++;

                float defTotal = 0f;
                foreach (var cost in CostDerivationService.DeriveElementCosts(def))
                {
                    effBuckets.TryGetValue(cost.ManaType, out var prev);
                    effBuckets[cost.ManaType] = prev + cost.Value;
                    effectTotal += cost.Value;
                    defTotal += cost.Value;
                }
                result.Breakdown.Add(new CostBreakdownLine("E", $"效果 {def.DisplayName ?? def.Id} (锚价)", defTotal));
            }

            // ---- 4) 挂载折扣 f（法术不折；档位未声明时按上限档计，供建议档位推导用）----
            int tierForFactor = result.DeclaredTier > 0
                ? Mathf.Clamp(result.DeclaredTier, 1, cc.MaxTier)
                : cc.MaxTier;
            float d = isSpell ? 1f : dd.At(tierForFactor);
            int extraActivations = Mathf.Max(0, result.ActiveEffectCount - 1);
            float factor = isSpell ? 1f : Mathf.Max(0f, d - dd.ExtraActivationSlope * extraActivations);
            result.Factor = factor;
            result.Breakdown.Add(new CostBreakdownLine("f", $"挂载折扣 f（d({tierForFactor})={d:0.###}，N_active={result.ActiveEffectCount}）", factor));

            // ---- 5) 组合层计价：挂载包(E+K) 总额×f 一次性取整（减免发生在组合层，非逐效果），
            //           最大余数法分摊回颜色桶；D = S灰 + 挂载实付 ----
            var kwMultiplier = cc.KeywordsShareDelayDiscount ? factor : 1f;
            int statGray = (int)Math.Round(statValue, MidpointRounding.AwayFromZero);
            var mounted = ApportionMounted(effBuckets, kwBuckets, kwMultiplier, factor);
            if (statGray > 0)
            {
                mounted.TryGetValue(ManaType.Gray, out var mountedGray);
                mounted[ManaType.Gray] = statGray + mountedGray;
            }
            foreach (var kv in mounted)
            {
                result.DerivedCost[kv.Key] = kv.Value;
                result.Breakdown.Add(new CostBreakdownLine("D", $"推导费 {kv.Key} ×{kv.Value}", kv.Value, kv.Key));
            }
            result.S = (int)Math.Round(statValue, MidpointRounding.AwayFromZero);
            result.K = (int)Math.Round(keywordTotal, MidpointRounding.AwayFromZero);
            result.EAnchor = (int)Math.Round(effectTotal, MidpointRounding.AwayFromZero);
            result.DerivedTotal = result.DerivedCost.Values.Sum();

            // ---- 6) 抵扣需求与代价当量 ----
            result.OffsetRequirement = Mathf.Max(0, result.DerivedTotal - result.DeclaredTier);
            result.OffsetProvided = OffsetProvided(card, cc);
            result.Conformant = result.OffsetProvided >= result.OffsetRequirement;
            result.Breakdown.Add(new CostBreakdownLine("Req", $"抵扣需求 max(0, D{result.DerivedTotal} − C{result.DeclaredTier})", result.OffsetRequirement));
            result.Breakdown.Add(new CostBreakdownLine("O", $"代价当量合计（已提供）", result.OffsetProvided));

            // ---- 7) 建议档位 Ĉ 与建议分布 ----
            result.SuggestedTier = FindSuggestedTier(card, cc, dd, isSpell, statValue, kwBuckets, effBuckets, result.ActiveEffectCount);
            result.SuggestedCost = BuildCostAtTier(result.SuggestedTier, cc, dd, isSpell, statValue, kwBuckets, effBuckets, result.ActiveEffectCount);
            return result;
        }

        /// <summary>缺省建议：建议档位 Ĉ 的多色分布（CardLoader 兜底 / 重生成菜单用）。</summary>
        public static Dictionary<ManaType, int> DeriveSuggestedCost(CardData card)
        {
            var r = Derive(card);
            return r.SuggestedCost;
        }

        /// <summary>
        /// 幂等兜底：Cost 为空且非超量（费用由素材推导）→ 写入建议档位分布；非空一律不动。
        /// D=0（无身材无效果无关键词）保持空 —— PlayCard 的 {Gray:1} 默认兜底行为不变。
        /// </summary>
        public static void EnsureCost(CardData card)
        {
            if (card == null) return;
            if (card.Cost != null && card.Cost.Count > 0) return;      // 严格非空即返：禁止覆盖声明费用
            if ((card.Subtype & CardSubtype.Xyz) != 0) return;
            if ((card.Subtype & CardSubtype.Ritual) != 0) return;      // 仪式：0 费说明书卡，保持空 Cost（打出免费）

            var suggested = DeriveSuggestedCost(card);
            if (suggested.Count == 0) return;

            card.Cost = suggested.ToDictionary(kv => (int)kv.Key, kv => (float)kv.Value);
            card.ResetCache();
        }

        // ======================================== 内部 ========================================

        /// <summary>卡上代价条目 → 元素当量（构筑期抵扣换算；ElementConsume/Mill/SendExtra 是费用/游戏内机制，不当量）。</summary>
        private static float OffsetProvided(CardData card, CardCostConfig cc)
        {
            float total = 0f;
            if (card.Effects == null) return total;
            foreach (var effect in card.Effects)
            {
                if (effect?.Costs == null) continue;
                foreach (var cost in effect.Costs)
                {
                    if (cost == null) continue;
                    switch ((CostType)cost.CostType)
                    {
                        case CostType.DiscardCard: total += cc.DiscardCardValue * Mathf.Max(1, cost.Value); break;
                        case CostType.LifePayment: total += cc.LifeValuePerPoint * Mathf.Max(1, cost.Value); break;
                        case CostType.Sleep: total += cc.SleepValuePerTurn * Mathf.Max(1, cost.TurnDuration); break;
                        case CostType.SummonMaterial: total += cc.SummonMaterialValue * Mathf.Max(1, cost.Value); break;
                    }
                }
            }
            return total;
        }

        /// <summary>Ĉ = min{C∈[1..MaxTier] : D(C) ≤ C}；D(C) 随 C 单调不增（d 递减），从 1 向上搜；无满足取 MaxTier。</summary>
        private static int FindSuggestedTier(CardData card, CardCostConfig cc, DelayDiscountConfig dd,
            bool isSpell, float statValue, Dictionary<ManaType, float> kwBuckets, Dictionary<ManaType, float> effBuckets, int nActive)
        {
            // 无身材无效果无关键词 → D=0，无需建议；返回 0，调用方按「保持空」处理。
            // （关键词存在但未登记 Grant 原子时 kwBuckets 为空 —— 仍参与搜索，D≈S。）
            if (statValue <= 0f && kwBuckets.Count == 0 && effBuckets.Count == 0
                && (card.Keywords == null || card.Keywords.Count == 0))
                return 0;

            for (int tier = 1; tier <= cc.MaxTier; tier++)
            {
                var costAt = BuildCostAtTier(tier, cc, dd, isSpell, statValue, kwBuckets, effBuckets, nActive);
                if (costAt.Values.Sum() <= tier) return tier;
            }
            return cc.MaxTier;
        }

        /// <summary>按指定档位构建 D(tier) 的逐色分布（组合层取整；供建议档位搜索与采纳写入）。</summary>
        private static Dictionary<ManaType, int> BuildCostAtTier(int tier, CardCostConfig cc, DelayDiscountConfig dd,
            bool isSpell, float statValue, Dictionary<ManaType, float> kwBuckets, Dictionary<ManaType, float> effBuckets, int nActive)
        {
            float d = isSpell ? 1f : dd.At(Mathf.Clamp(tier, 1, cc.MaxTier));
            float factor = isSpell ? 1f : Mathf.Max(0f, d - dd.ExtraActivationSlope * Mathf.Max(0, nActive - 1));

            var kwMultiplier = cc.KeywordsShareDelayDiscount ? factor : 1f;
            int statGray = (int)Math.Round(statValue, MidpointRounding.AwayFromZero);
            var result = ApportionMounted(effBuckets, kwBuckets, kwMultiplier, factor);
            if (statGray > 0)
            {
                result.TryGetValue(ManaType.Gray, out var mountedGray);
                result[ManaType.Gray] = statGray + mountedGray;
            }
            return result;
        }

        /// <summary>
        /// 挂载包组合层计价：效果桶×f + 关键词桶×kwMultiplier 汇总为包总额，乘法已含在桶内——
        /// 包总额一次性 AwayFromZero 取整（减免发生在组合层，不做逐效果取整），
        /// 最大余数法把整数实付分摊回颜色（余数大者优先，并列取桶值大者，再并列按枚举序）。
        /// </summary>
        private static Dictionary<ManaType, int> ApportionMounted(Dictionary<ManaType, float> effBuckets,
            Dictionary<ManaType, float> kwBuckets, float kwMultiplier, float factor)
        {
            var mounted = new Dictionary<ManaType, float>();
            foreach (var kv in effBuckets)
            {
                mounted.TryGetValue(kv.Key, out var prev);
                mounted[kv.Key] = prev + kv.Value * factor;
            }
            foreach (var kv in kwBuckets)
            {
                mounted.TryGetValue(kv.Key, out var prev);
                mounted[kv.Key] = prev + kv.Value * kwMultiplier;
            }

            float total = 0f;
            foreach (var kv in mounted) total += kv.Value;
            int payInt = (int)Math.Round(total, MidpointRounding.AwayFromZero);

            var result = new Dictionary<ManaType, int>();
            int floorSum = 0;
            var fracs = new List<(ManaType color, float frac, float bucket)>();
            foreach (var kv in mounted)
            {
                int fl = (int)Math.Floor(kv.Value);
                result[kv.Key] = fl;
                floorSum += fl;
                fracs.Add((kv.Key, kv.Value - fl, kv.Value));
            }

            int remainder = payInt - floorSum;
            foreach (var t in fracs.OrderByDescending(x => x.frac).ThenByDescending(x => x.bucket).ThenBy(x => (int)x.color))
            {
                if (remainder <= 0 || t.frac <= 0f) break;
                result[t.color] += 1;
                remainder--;
            }
            if (remainder > 0) // 兜底（浮点尾差理论不可达）：加给最大桶
            {
                var maxKey = mounted.OrderByDescending(x => x.Value).First().Key;
                result[maxKey] += remainder;
            }

            foreach (var k in result.Where(x => x.Value <= 0).Select(x => x.Key).ToList()) result.Remove(k);
            return result;
        }
    }
}
