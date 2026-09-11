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
    /// 元素支付验证器
    /// </summary>
    public static class ElementPaymentValidator
    {
        /// <summary>纯色（受浓度上限约束）：红/蓝/绿/黑/白（黑白 2026-09-11 转正，与三色同权）。灰不受限。</summary>
        private static readonly ManaType[] PureColors =
            { ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Black, ManaType.White };

        private static bool IsPureColor(ManaType type)
            => PureColors.Contains(type);

        /// <summary>
        /// 验证是否可以支付指定倾向的代价
        /// </summary>
        /// <param name="affinity">元素倾向</param>
        /// <param name="availableMana">可用元素（按颜色分类）</param>
        /// <param name="amount">需要支付的数量</param>
        /// <param name="pureColorCap">纯色浓度上限（每种纯色单次可支付量 ≤ 此值；null = 不限制）</param>
        /// <returns>是否可以支付</returns>
        public static bool CanPay(ElementAffinity affinity, Dictionary<ManaType, int> availableMana, int amount, int? pureColorCap = null)
        {
            if (amount <= 0) return true;

            // 灰色效果：可用任意颜色支付；纯色（含黑白）贡献各受浓度上限约束，灰不受限
            if (affinity.IsGeneric)
            {
                int total = 0;
                foreach (var kv in availableMana)
                {
                    total += IsPureColor(kv.Key) && pureColorCap.HasValue
                        ? Math.Min(kv.Value, pureColorCap.Value)
                        : kv.Value;
                }
                return total >= amount;
            }

            // 指定颜色：必须用该颜色支付；纯色另受浓度上限约束
            if (pureColorCap.HasValue && IsPureColor(affinity.PrimaryColor) && amount > pureColorCap.Value)
                return false;
            return availableMana.TryGetValue(affinity.PrimaryColor, out int count) && count >= amount;
        }

        /// <summary>
        /// 尝试支付指定倾向的代价
        /// </summary>
        /// <param name="affinity">元素倾向</param>
        /// <param name="availableMana">可用元素（会被修改）</param>
        /// <param name="amount">需要支付的数量</param>
        /// <param name="pureColorCap">纯色浓度上限（每种纯色单次可支付量 ≤ 此值；null = 不限制）</param>
        /// <returns>是否支付成功</returns>
        public static bool TryPay(ElementAffinity affinity, Dictionary<ManaType, int> availableMana, int amount, int? pureColorCap = null)
        {
            if (!CanPay(affinity, availableMana, amount, pureColorCap))
                return false;

            if (amount <= 0) return true;

            if (affinity.IsGeneric)
            {
                // 灰色效果：从任意颜色扣除，优先使用非主要颜色
                int remaining = amount;

                // 优先使用灰色（不受浓度上限约束）
                if (availableMana.TryGetValue(ManaType.Gray, out int grayCount) && grayCount > 0)
                {
                    int toUse = Math.Min(grayCount, remaining);
                    availableMana[ManaType.Gray] -= toUse;
                    remaining -= toUse;
                }

                // 然后按顺序使用纯色（红蓝绿黑白）：每种纯色本次贡献 ≤ 浓度上限
                foreach (var color in PureColors)
                {
                    if (remaining <= 0) break;
                    if (availableMana.TryGetValue(color, out int count) && count > 0)
                    {
                        int toUse = Math.Min(count, remaining);
                        if (pureColorCap.HasValue) toUse = Math.Min(toUse, pureColorCap.Value);
                        availableMana[color] -= toUse;
                        remaining -= toUse;
                    }
                }

                return remaining == 0;
            }
            else
            {
                // 指定颜色：从该颜色扣除
                availableMana[affinity.PrimaryColor] -= amount;
                return true;
            }
        }

        /// <summary>
        /// 获取支付指定倾向代价所需的最优元素组合
        /// </summary>
        /// <param name="affinity">元素倾向</param>
        /// <param name="availableMana">可用元素</param>
        /// <param name="amount">需要支付的数量</param>
        /// <param name="pureColorCap">纯色浓度上限（每种纯色单次可支付量 ≤ 此值；null = 不限制）</param>
        /// <returns>支付方案（颜色到数量的映射），null表示无法支付</returns>
        public static Dictionary<ManaType, int> GetPaymentPlan(ElementAffinity affinity, Dictionary<ManaType, int> availableMana, int amount, int? pureColorCap = null)
        {
            if (!CanPay(affinity, availableMana, amount, pureColorCap))
                return null;

            var plan = new Dictionary<ManaType, int>();

            if (amount <= 0) return plan;

            if (affinity.IsGeneric)
            {
                int remaining = amount;

                // 优先使用灰色（不受浓度上限约束）
                if (availableMana.TryGetValue(ManaType.Gray, out int grayCount) && grayCount > 0)
                {
                    int toUse = Math.Min(grayCount, remaining);
                    plan[ManaType.Gray] = toUse;
                    remaining -= toUse;
                }

                // 然后按顺序使用纯色：每种纯色本次贡献 ≤ 浓度上限
                foreach (var color in PureColors)
                {
                    if (remaining <= 0) break;
                    if (availableMana.TryGetValue(color, out int count) && count > 0)
                    {
                        int toUse = Math.Min(count, remaining);
                        if (pureColorCap.HasValue) toUse = Math.Min(toUse, pureColorCap.Value);
                        plan[color] = toUse;
                        remaining -= toUse;
                    }
                }
            }
            else
            {
                plan[affinity.PrimaryColor] = amount;
            }

            return plan;
        }
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
