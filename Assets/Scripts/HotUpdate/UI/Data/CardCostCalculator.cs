using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌算费器 —— 统一计价的 UI 薄委托。
    /// 唯一公式在 CardCore.CardCostService（规则一·平衡：锚价 + 身材灰 + 关键词固定费 + 底盘组合，
    /// 全额计价无延迟折——2026-10-31 阶梯减价删除），
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

            /// <summary>档位=推导档 Ĉ（round(S+E+K) 所在档；恒推导，无声明。仅作 UI 卡面费数字——延迟折已删）。</summary>
            public int ManaCost;

            /// <summary>规则一缺口 Req = max(0, D_total − Ĉ)（2026-09-11 简化：D≤Ĉ 直判，无当量抵扣）。</summary>
            public int OffsetRequirement;

            /// <summary>黑白元素获得（错边原子转化；结算时发放，封顶地牌上限）。键=枚举 int。</summary>
            public Dictionary<int, int> Grants = new Dictionary<int, int>();

            /// <summary>推导费用分布（=DerivedCost；2026-10-22 恒推导定案起无声明回显分支）。</summary>
            public ElementCost CostDict = new ElementCost();

            /// <summary>纯内容推导分布（=DerivedCost；2026-10-16 自动算费直写定案沿用）——
            /// 卡组合界面费用恒取本值回写 _card.Cost。</summary>
            public ElementCost SuggestedCost = new ElementCost();

            /// <summary>代价抵扣（P 行）**前**的逐色推导费（P4 2026-10-29：合成器净费三段式 X 段）。
            /// 无代价抵扣时与 SuggestedCost 相同。</summary>
            public ElementCost PrePayloadCost = new ElementCost();

            /// <summary>卡层代价价合计（P4 2026-10-29：合成器净费三段式 Y 段——生成面，可能大于实抵）。</summary>
            public int PayloadPrice;

            /// <summary>实际抵扣点数（2026-10-31 色锁定案）：净费公式显示值 = min(价合计, 可产出色容量)。</summary>
            public int PayloadApplied;

            /// <summary>抵扣色中文名（如「红」；无代价/无抵扣面=空串）。</summary>
            public string PayloadScopeZh = "";

            public readonly List<BreakdownLine> Breakdown = new List<BreakdownLine>();
        }

        public static Result Calculate(CardData card)
        {
            var r = CardCostService.Derive(card);
            var result = new Result
            {
                Total = r.DerivedTotal,
                ManaCost = r.DerivedTier,
                OffsetRequirement = r.OffsetRequirement,
                Grants = r.Grants.ToColorDictInt().ToDictionary(kv => (int)kv.Key, kv => kv.Value),
                CostDict = r.DerivedCost.Clone(),
                SuggestedCost = r.DerivedCost.Clone(),
                PrePayloadCost = r.PrePayloadCost?.Clone() ?? r.DerivedCost.Clone(),
                PayloadPrice = r.PayloadPrice,
                PayloadApplied = r.PayloadApplied,
                PayloadScopeZh = r.PayloadScopeZh ?? "",
            };

            foreach (var line in r.Breakdown)
            {
                result.Breakdown.Add(new BreakdownLine($"[{line.Stage}] {line.Label}", line.Value));
            }
            return result;
        }

        /// <summary>CostType → 中文名（供 UI 与明细展示）。2026-09-14 代价原子化后仅剩两值。</summary>
        public static string CostTypeName(int costType)
        {
            return (CostType)costType switch
            {
                CostType.ElementConsume => "元素消耗",
                CostType.Payload => "代价效果",
                _ => costType.ToString(),
            };
        }
    }
}
