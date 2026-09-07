using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 卡层组合费用（「效果→卡」组合层，2026-09-07 定案）——独立的第三层计价：
    /// · 原子层（CostDerivationService）：原子→效果锚价（并发取总 / 条件奖励免费 / 抉择 per-mode）；
    /// · 规则一（CardCostService.Derive）：声明档位推导 D(C) 与代价抵扣校验；
    /// · 本层：只对「卡的结构性弹性」收额外费——玩家为灵活性付费，与效果本身的强度无关。
    ///
    /// 当前规则：
    /// ①抉择价差溢价——可选模式最高/最低费用相等 → 整体 +0；相差 Step 费 → 整体 +1；
    ///   相差 Step×2 及以上 → 整体 +Cap 封顶（防快攻带高费抉择当地牌、实战只用低费模式的套利）。
    /// ②效果挂载口——每卡默认 Baseline(2) 口：空置退费（UnusedRate/口）、超出加价（ExtraRate/口）。
    ///   例：2-2 白板两口全空 −2 → 0 费；1-1 挂三效果超一口 +1 → 原有基础加 1 费。
    ///   （原挂载折扣 ExtraActivationSlope「选发每多1回合的额外折」已删——与本规则重复。）
    ///
    /// 后续规则在本类追加——不与原子层费用计算混合。
    /// 应用点：CardCostService.Derive/DeriveModeCosts 在组合层锚价组装完成后统一附加（灰色分量）。
    /// 参数见 ValueSystemRuntimeConfig.CardCompositionConfig（表 Category=CardComposition）。
    /// </summary>
    public static class CardCompositionCost
    {
        /// <summary>
        /// 效果挂载口调整：clamp 到 Baseline 的差 × 对应费率（空置为负、超出为正）。
        /// 计数 = CardData.Effects 条数（单效果内的原子并发组合由原子层计价承担，不重复计口）。
        /// 返回灰分量增量（可为负）；调用方套用后灰下限 0（退费不把支付变负）。
        /// </summary>
        public static int MountSlotAdjust(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int baseline = (int)Math.Max(0f, cfg.MountBaseline);
            int count = card?.Effects?.Count ?? 0;

            if (count > baseline)
                return (int)Math.Max(0f, cfg.MountExtraRate) * (count - baseline);
            if (count < baseline)
                return -(int)Math.Max(0f, cfg.MountUnusedRate) * (baseline - count);
            return 0;
        }

        /// <summary>
        /// 抉择价差溢价（分布版入口）：从各模式费用分布提取总额后走总额版。
        /// </summary>
        public static int ChoiceSpreadPremium(IReadOnlyList<Dictionary<int, float>> modeCosts)
        {
            if (modeCosts == null || modeCosts.Count < 2) return 0;
            var totals = new List<float>();
            foreach (var mode in modeCosts)
            {
                float total = 0f;
                if (mode != null)
                    foreach (var v in mode.Values) total += v;
                totals.Add(total);
            }
            return ChoiceSpreadPremium((IReadOnlyList<float>)totals);
        }

        /// <summary>
        /// 抉择价差溢价（总额版，核心）：clamp(floor((最高模式费 − 最低模式费) / Step), 0, Cap)。
        /// 价差为 0（各模式等价）→ +0；价差 ≥ Step×Cap → +Cap 封顶。加在整体（灰色、所有模式同加）。
        /// 输入应传**未加本溢价**的模式锚价（DeriveModeCosts 第一遍的原始锚桶总额）。
        /// </summary>
        public static int ChoiceSpreadPremium(IReadOnlyList<float> modeTotals)
        {
            if (modeTotals == null || modeTotals.Count < 2) return 0;

            float max = 0f, min = float.MaxValue;
            foreach (var t in modeTotals)
            {
                if (t > max) max = t;
                if (t < min) min = t;
            }

            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            float step = cfg.ChoiceSpreadStep > 0f ? cfg.ChoiceSpreadStep : 3f;
            int cap = (int)Math.Max(0f, cfg.ChoiceSpreadCap);
            int premium = (int)Math.Floor((max - min) / step);
            return Math.Min(Math.Max(premium, 0), cap);
        }
    }
}
