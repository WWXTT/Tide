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
    /// 元素支付验证器（2026-09-14 统一混付定案）：**账单级规划器**——出牌与效果费
    /// （ElementCostPayment）共用同一算法。2026-10-11 现取现用重做：主规划器移驻
    /// ElementPool.PlanBill（钱包+地牌横置合成模型），本类保留纯函数账单规划（钱包口径）
    /// 供预检/校验复用，链定义同源。
    /// 支付链（2026-10-11 收窄定案）：红/蓝/绿**仅本色**；灰=[灰,黑,白,红,蓝,绿]
    /// （黑白只垫灰、三色垫灰沿用；黑先于白，确定性）。单向：红蓝绿灰不可付黑白费。
    /// 浓度上限（2026-10-16 临时元素超上限改版）：本类纯钱包口径——钱包贡献**不受浓度钳**
    /// （获取=对应地块可用+1、可用可超上限），旧「每货币贡献 ≤ cap / 纯色需求超帽拒付」
    /// 随 cap 参数一并退役；地牌侧的浓度钳在 ElementPool.PlanBill 单独保留。
    /// </summary>
    public static class ElementPaymentValidator
    {
        /// <summary>四色需求（红蓝绿灰）的固定处理序。
        /// 公开给 ElementPool.PlanBill（现取现用统一规划器）——分配轨迹须与账单规划器同序。</summary>
        public static readonly ManaType[] FourColorOrder =
            { ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Gray };

        /// <summary>int/float 账单归一为 ManaType→int（跳过 ≤0 项）。
        /// 2026-10-12 小数直通定案：效果级费用带小数（如锚 0.6绿），此处=支付终点——
        /// 四舍五入（约等于），原 (int) 截断会把 0.x 账单变成免费。</summary>
        public static Dictionary<ManaType, int> NormalizeBill(Dictionary<int, float> cost)
        {
            var bill = new Dictionary<ManaType, int>();
            if (cost == null) return bill;
            foreach (var kv in cost)
            {
                int amount = (int)Math.Round(kv.Value, MidpointRounding.AwayFromZero);
                if (amount > 0) bill[(ManaType)kv.Key] = amount;
            }
            return bill;
        }

        /// <summary>ElementCost 账单归一为 ManaType→int（2026-10-04 位置数组化主入口；跳过 ≤0 项）。
        /// 取整口径同上——支付终点约等于。</summary>
        public static Dictionary<ManaType, int> NormalizeBill(ElementCost cost)
        {
            var bill = new Dictionary<ManaType, int>();
            if (cost == null) return bill;
            foreach (var color in cost.NonzeroColors())
            {
                int amount = (int)Math.Round(cost[color], MidpointRounding.AwayFromZero);
                if (amount > 0) bill[color] = amount;
            }
            return bill;
        }

        /// <summary>填链：需求色的候选货币按支付序排列（2026-10-11 收窄定案）：
        /// 红/蓝/绿=[本色]（黑白不再垫三色、灰本就不垫三色——三色缺口只有本色能付）；
        /// 灰=[灰,黑,白,红,蓝,绿]（黑白只垫灰；三色垫灰沿用；黑先于白，确定性）；
        /// 黑/白=[本色]（单向）。
        /// 公开给 ElementPool.PlanBill——横置产色候选须沿同一填链评估。</summary>
        public static ManaType[] ChainFor(ManaType need) => need switch
        {
            ManaType.Red => new[] { ManaType.Red },
            ManaType.Blue => new[] { ManaType.Blue },
            ManaType.Green => new[] { ManaType.Green },
            ManaType.Gray => new[] { ManaType.Gray, ManaType.Black, ManaType.White, ManaType.Red, ManaType.Blue, ManaType.Green },
            _ => new[] { need },
        };

        /// <summary>
        /// 整账单支付规划（非破坏——不改 availableMana；纯钱包口径，地牌横置参与的综合规划在
        /// ElementPool.PlanBill）。处理序：黑→白（仅本色）**先行预留**（否则贪心会吃掉黑白本色费
        /// 所需的货币），再按固定序处理红蓝绿灰，每需求沿填链扣减。钱包贡献不受浓度钳
        /// （2026-10-16 超上限改版——cap 参数随旧口径退役）。
        /// </summary>
        /// <returns>实际扣款组合（货币→数量）；null = 不可付。</returns>
        public static Dictionary<ManaType, int> GetBillPaymentPlan(
            Dictionary<ManaType, int> bill, Dictionary<ManaType, int> availableMana)
        {
            if (bill == null || bill.Count == 0) return new Dictionary<ManaType, int>();

            var working = new Dictionary<ManaType, int>(availableMana ?? new Dictionary<ManaType, int>());
            var plan = new Dictionary<ManaType, int>();
            foreach (ManaType c in Enum.GetValues(typeof(ManaType)))
                if (!working.ContainsKey(c)) working[c] = 0;

            // 黑白本色费先行预留（黑白只来自钱包、不受浓度钳——余额定生死）
            foreach (var bw in new[] { ManaType.Black, ManaType.White })
            {
                if (!bill.TryGetValue(bw, out var bwNeed) || bwNeed <= 0) continue;
                if (working[bw] < bwNeed) return null;
                working[bw] -= bwNeed;
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
                    int take = Math.Min(have, remaining); // 钱包无浓度钳（可超上限直付）
                    if (take <= 0) continue;
                    working[currency] = have - take;
                    plan[currency] = plan.TryGetValue(currency, out var prev) ? prev + take : take;
                    remaining -= take;
                }
                if (remaining > 0) return null;
            }

            return plan;
        }

        /// <summary>整账单可付性预检（非破坏）。= GetBillPaymentPlan(...) != null。</summary>
        public static bool CanPayBill(Dictionary<ManaType, int> bill, Dictionary<ManaType, int> availableMana)
            => GetBillPaymentPlan(bill, availableMana) != null;
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

            // 2026-10-06 中文化：表色由 BuildConfig 追加中文色名（ManaTypeNames 词表），扫描改走
            // TryParseZh 反查（白/黑/灰全覆盖，词表外 token 忽略）。黑白转正（2026-09-11）口径不变：
            // 黑白不由地牌产出，获取通道唯一=卡结算（错边原子/代价补偿），见 CostDerivation/EffectExecutionEngine。
            foreach (var tag in config.GetTagList())
            {
                if (ManaTypeNames.TryParseZh(tag, out var color))
                    return color == ManaType.Gray ? Generic : ElementAffinity.Single(color);
            }
            return Generic;
        }
    }
}
