using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌算费器 —— 统一计价的 UI 薄委托。
    /// 唯一公式在 CardCore.CardCostService（规则一·平衡：锚价 + 身材灰 + 关键词固定费 + 挂载延迟折扣 d(C)），
    /// UI 建议价 / 装载校验 / verifier 共用同一推导；历史启发式（log2 衰减、类型基值、协同折扣）已废弃。
    /// </summary>
    public static class CardCostCalculator
    {
        /// <summary>一行拆解明细：标签 + 数值。</summary>
        public struct BreakdownLine
        {
            public string Label;
            public float Value;

            public BreakdownLine(string label, float value)
            {
                Label = label;
                Value = value;
            }
        }

        public sealed class Result
        {
            /// <summary>推导价 D_total（逐色取整后总和）。</summary>
            public float Total;

            /// <summary>档位：声明优先，未声明取建议 Ĉ。</summary>
            public int ManaCost;

            /// <summary>规则一缺口 Req = max(0, D_total − C_total)（2026-09-11 简化：D≤C 直判，无当量抵扣）。</summary>
            public int OffsetRequirement;

            /// <summary>黑白元素获得（错边原子转化；结算时发放，封顶地牌上限）。</summary>
            public Dictionary<int, int> Grants = new Dictionary<int, int>();

            /// <summary>建议采纳的费用分布（多色；已有声明时回显当前声明构成）。</summary>
            public Dictionary<int, float> CostDict = new Dictionary<int, float>();

            public readonly List<BreakdownLine> Breakdown = new List<BreakdownLine>();
        }

        public static Result Calculate(CardData card)
        {
            var r = CardCostService.Derive(card);
            var result = new Result
            {
                Total = r.DerivedTotal,
                ManaCost = r.DeclaredTier > 0 ? r.DeclaredTier : r.SuggestedTier,
                OffsetRequirement = r.OffsetRequirement,
                Grants = r.Grants.ToDictionary(kv => (int)kv.Key, kv => kv.Value),
                CostDict = r.DeclaredTier > 0 && card != null && card.Cost != null
                    ? card.Cost.Where(kv => kv.Value > 0f).ToDictionary(kv => kv.Key, kv => kv.Value)
                    : r.SuggestedCost.ToDictionary(kv => (int)kv.Key, kv => (float)kv.Value),
            };

            foreach (var line in r.Breakdown)
            {
                result.Breakdown.Add(new BreakdownLine($"[{line.Stage}] {line.Label}", line.Value));
            }
            return result;
        }

        /// <summary>CostType → 中文名（供 UI 与明细展示）。</summary>
        public static string CostTypeName(int costType)
        {
            return (CostType)costType switch
            {
                CostType.ElementConsume => "元素消耗",
                CostType.DiscardCard => "弃牌",
                CostType.LifePayment => "支付生命（扣上限）",
                CostType.Sleep => "沉睡",
                CostType.SummonMaterial => "召唤素材",
                CostType.MillDeck => "送墓（本组）",
                CostType.SendExtraDeck => "送额外组",
                CostType.SelfSickness => "自身紊乱",
                CostType.OpponentBuff => "对手增益",
                CostType.Payload => "代价效果",
                _ => costType.ToString(),
            };
        }
    }
}
