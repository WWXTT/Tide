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
    /// 当前规则（2026-09-21 修订）：
    /// ①底盘预算（ChassisAdjust，2026-09-10 攻/守效果化）——免费额度 3 灰覆盖 攻(1)+守(1)+效果槽(1/个)；
    ///   **抉择分支计槽**：一个效果含 N 分支 Choice = N 个效果槽（抉择装两个效果，收两次槽位费）。
    /// ②抉择价差溢价已废（2026-09-21）——防套利职责由分支计槽 + 地牌按所选模式产元素承担
    ///   （生物带抉择当地牌时玩家自选模式，与出牌一致——不再有「带高费抉择当地牌、实战只用低费」的套利面）。
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
        /// · 攻/守 = 速度0/速度1主动效果（2026-09-16）（不占槽位），生物默认自带（NoAttack/NoGuard opt-out 退额度）；
        /// · 法术无攻守（恒退 2：即「法术减两费」）；瞬间法术 SurplusToSpeed=true 时
        ///   盈余转 BaseSpeed+1（转换层授予，见 GameActions.GetCardEffectDefinitions），不退费（返回 0）。
        /// · 效果槽按 CountEffectSlots 计（2026-09-21 抉择分支计槽）——一个效果含 N 分支 Choice = N 槽。
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
            int effects = CostDerivationService.CountEffectSlots(card);

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

        // 抉择价差溢价已废（2026-09-21）——抉择分支计槽（ChassisAdjust）+ 地牌按所选模式产元素承担防套利。
    }
}
