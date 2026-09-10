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
        /// 底盘预算（2026-09-10 攻/守效果化定案，取代旧挂载口 Baseline 曲线）：
        /// 免费额度 ChassisBudget(3 灰) 覆盖 攻击(1) + 守卫(1) + 效果槽(1/个)。
        /// 净调整 = 预算 − (攻在 + 守在 + 效果数) × 费率——正数退费、负数加价。
        /// · 攻/守 = 1速/2速主动效果（不占槽位），生物默认自带（NoAttack/NoGuard opt-out 退额度）；
        /// · 法术无攻守（恒退 2：即「法术减两费」）；瞬间法术 SurplusToSpeed=true 时
        ///   盈余转 BaseSpeed+1（转换层授予，见 GameActions.GetCardEffectDefinitions），不退费（返回 0）。
        /// 退费落位（先灰、灰不足逐点退最高费用色）由 ApplyChassisRefund 承担。
        /// </summary>
        public static int ChassisAdjust(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int budget = (int)Math.Max(0f, cfg.ChassisBudget);
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            bool isCreature = card != null && card.Supertype == Cardtype.Creature;
            int atk = isCreature && !card.NoAttack ? 1 : 0;
            int grd = isCreature && !card.NoGuard ? 1 : 0;
            int effects = card?.Effects?.Count ?? 0;

            // 瞬间富余转速度：不退费（速度 +1 在效果定义层授予）
            if (card != null && card.SurplusToSpeed && IsInstantSpell(card)) return 0;

            return budget - (atk + grd + effects) * rate;
        }

        /// <summary>瞬间法术 = 法术且无任何效果声明 Permanent 持续（发动完进墓地）。</summary>
        private static bool IsInstantSpell(CardData card)
        {
            if (card == null || card.Supertype != Cardtype.Spell) return false;
            if (card.Effects == null) return true;
            foreach (var e in card.Effects)
                if (e != null && e.Duration >= 0 && (DurationType)e.Duration == DurationType.Permanent)
                    return false;
            return true;
        }

        /// <summary>
        /// 底盘退费落位：先扣灰桶（≥1 才扣），灰不足（法术常无灰分量）逐点从最高费用色桶扣
        /// （并列取枚举序靠前者）。全桶空则退无可退（免费卡）。
        /// </summary>
        public static void ApplyChassisRefund(Dictionary<ManaType, int> mounted, int refund)
        {
            for (int i = 0; i < refund; i++)
            {
                if (mounted.TryGetValue(ManaType.Gray, out var g) && g >= 1)
                {
                    mounted[ManaType.Gray] = g - 1;
                    continue;
                }
                ManaType best = default;
                int bestV = 0;
                foreach (var kv in mounted)
                    if (kv.Key != ManaType.Gray && kv.Value > bestV) { bestV = kv.Value; best = kv.Key; }
                if (bestV <= 0) break;
                mounted[best] = bestV - 1;
            }
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
