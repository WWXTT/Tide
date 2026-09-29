using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    /// <summary>
    /// 元素倾向 - 效果与元素颜色的关联
    /// </summary>
    [Serializable]
    public class ElementAffinity
    {
        /// <summary>主要元素颜色（Red/Blue/Green/Gray）</summary>
        public ManaType PrimaryColor { get; set; }

        /// <summary>是否可用任意颜色支付（灰色效果）</summary>
        public bool IsGeneric => PrimaryColor == ManaType.Gray;

        /// <summary>是否为黑白（2026-09-11 转正为真实颜色：获取=卡结算产生，不由地牌产出）</summary>
        public bool IsSpecialColor => PrimaryColor == ManaType.Black || PrimaryColor == ManaType.White;

        /// <summary>创建单色倾向</summary>
        public static ElementAffinity Single(ManaType color) => new ElementAffinity { PrimaryColor = color };

        /// <summary>创建通用倾向（灰色，可用任意颜色支付）</summary>
        public static ElementAffinity Generic => new ElementAffinity { PrimaryColor = ManaType.Gray };

        /// <summary>
        /// 获取颜色显示名称
        /// </summary>
        public string GetColorName()
        {
            return PrimaryColor switch
            {
                ManaType.Red => "红",
                ManaType.Blue => "蓝",
                ManaType.Green => "绿",
                ManaType.Gray => "通用",
                ManaType.White => "白",
                ManaType.Black => "黑",
                _ => PrimaryColor.ToString()
            };
        }
    }

    /// <summary>
    /// 元素支付验证器（2026-09-14 统一混付定案）：**账单级规划器**——出牌（ElementPool.CanPayCost/PayCost）
    /// 与效果费（ElementCostPayment）共用同一算法，消灭「出牌精确扣款/效果混付」双轨。
    /// 统一支付序：同色 → 灰 → 黑 → 白（黑白=万用色，可替代红蓝绿灰；黑先于白，确定性）。
    /// 单向：红蓝绿灰不可付黑白费，黑不可付白费（反之亦然）。
    /// 浓度上限：每种货币（**含灰**与作万用的黑白）单次支付总贡献 ≤ cap；
    /// 纯色**需求量** > cap 时不论货币直接不可付（灰需求无需求侧帽）。
    /// </summary>
    public static class ElementPaymentValidator
    {
        /// <summary>纯色（受需求侧浓度上限约束）：红/蓝/绿/黑/白。</summary>
        private static readonly ManaType[] PureColors =
            { ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Black, ManaType.White };

        /// <summary>四色需求（红蓝绿灰——黑白万用可垫）的固定处理序。</summary>
        private static readonly ManaType[] FourColorOrder =
            { ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Gray };

        private static bool IsPureColor(ManaType type)
            => PureColors.Contains(type);

        /// <summary>int/float 账单归一为 ManaType→int（跳过 ≤0 项）。</summary>
        public static Dictionary<ManaType, int> NormalizeBill(Dictionary<int, float> cost)
        {
            var bill = new Dictionary<ManaType, int>();
            if (cost == null) return bill;
            foreach (var kv in cost)
            {
                int amount = (int)kv.Value;
                if (amount > 0) bill[(ManaType)kv.Key] = amount;
            }
            return bill;
        }

        /// <summary>填链：需求色的候选货币按支付序排列。红/蓝/绿=[本色,灰,黑,白]；灰=[灰,黑,白]；黑/白=[本色]（单向）。</summary>
        private static ManaType[] ChainFor(ManaType need) => need switch
        {
            ManaType.Red => new[] { ManaType.Red, ManaType.Gray, ManaType.Black, ManaType.White },
            ManaType.Blue => new[] { ManaType.Blue, ManaType.Gray, ManaType.Black, ManaType.White },
            ManaType.Green => new[] { ManaType.Green, ManaType.Gray, ManaType.Black, ManaType.White },
            ManaType.Gray => new[] { ManaType.Gray, ManaType.Black, ManaType.White },
            _ => new[] { need },
        };

        /// <summary>
        /// 整账单支付规划（非破坏——不改 availableMana）。
        /// 处理序：黑→白（仅本色）**先行预留**（否则四色贪心会吃掉黑白本色费所需的货币），
        /// 再按固定序处理红蓝绿灰，每需求沿填链（同色→灰→黑→白）扣减。
        /// </summary>
        /// <returns>实际扣款组合（货币→数量）；null = 不可付。</returns>
        public static Dictionary<ManaType, int> GetBillPaymentPlan(
            Dictionary<ManaType, int> bill, Dictionary<ManaType, int> availableMana, int? cap)
        {
            if (bill == null || bill.Count == 0) return new Dictionary<ManaType, int>();

            // 需求侧浓度上限：纯色需求量超帽，混付也救不了
            if (cap.HasValue)
            {
                foreach (var kv in bill)
                    if (IsPureColor(kv.Key) && kv.Value > cap.Value)
                        return null;
            }

            var working = new Dictionary<ManaType, int>(availableMana ?? new Dictionary<ManaType, int>());
            var used = new Dictionary<ManaType, int>();
            var plan = new Dictionary<ManaType, int>();
            foreach (ManaType c in Enum.GetValues(typeof(ManaType)))
            {
                used[c] = 0;
                if (!working.ContainsKey(c)) working[c] = 0;
            }

            // 黑白本色费先行预留（需求侧检查已保证 ≤ cap；used 从 0 起故贡献即合规）
            foreach (var bw in new[] { ManaType.Black, ManaType.White })
            {
                if (!bill.TryGetValue(bw, out var bwNeed) || bwNeed <= 0) continue;
                if (working[bw] < bwNeed) return null;
                working[bw] -= bwNeed;
                used[bw] += bwNeed;
                plan[bw] = bwNeed;
            }

            foreach (var need in FourColorOrder)
            {
                if (!bill.TryGetValue(need, out var remaining) || remaining <= 0) continue;
                foreach (var currency in ChainFor(need))
                {
                    if (remaining <= 0) break;
                    int have = working[currency];
                    if (have <= 0) continue;
                    int room = cap.HasValue ? cap.Value - used[currency] : int.MaxValue;
                    int take = Math.Min(Math.Min(have, remaining), room);
                    if (take <= 0) continue;
                    working[currency] = have - take;
                    used[currency] += take;
                    plan[currency] = plan.TryGetValue(currency, out var prev) ? prev + take : take;
                    remaining -= take;
                }
                if (remaining > 0) return null;
            }

            return plan;
        }

        /// <summary>整账单可付性预检（非破坏）。= GetBillPaymentPlan(...) != null。</summary>
        public static bool CanPayBill(Dictionary<ManaType, int> bill, Dictionary<ManaType, int> availableMana, int? cap)
            => GetBillPaymentPlan(bill, availableMana, cap) != null;
    }

    /// <summary>
    /// 预定义的元素倾向（仅红蓝绿灰）
    /// 颜色来源：配置表 AttributeValueConfig.json 的 EffectColor（经 AtomicEffectTable 加载，存于 Tags；
    /// 2026-09-11 起 Tags 只含 EffectColor——EffectFunction 列已删）。
    /// </summary>
    public static class ElementAffinities
    {
        /// <summary>通用效果（可用任意颜色支付）</summary>
        public static ElementAffinity Generic => ElementAffinity.Generic;

        /// <summary>
        /// 根据原子效果类型获取默认元素倾向（颜色由配置表驱动）
        /// </summary>
        public static ElementAffinity GetAffinityForEffect(AtomicEffectType effectType)
        {
            var config = CardCore.Attribute.AtomicEffectTable.GetByType(effectType);
            if (config == null) return Generic;

            foreach (var tag in config.GetTagList())
            {
                switch (tag)
                {
                    case "Red": return ElementAffinity.Single(ManaType.Red);
                    case "Blue": return ElementAffinity.Single(ManaType.Blue);
                    case "Green": return ElementAffinity.Single(ManaType.Green);
                    // 黑白转正（2026-09-11）：表内 White/Black 行计价/支付落回本色，不再归一为灰。
                    // 黑白不由地牌产出，获取通道唯一=卡结算（错边原子/代价补偿），见 CostDerivation/EffectExecutionEngine。
                    case "White": return ElementAffinity.Single(ManaType.White);
                    case "Black": return ElementAffinity.Single(ManaType.Black);
                    case "Gray": return ElementAffinity.Generic;
                }
            }
            return Generic;
        }
    }
}
