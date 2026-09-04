using System;
using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 费用自动推导服务 —— 效果「锚价」（即时价，法术定价：第 1 回合立刻打出来的价格）。
    /// 由效果配置表（AttributeValueConfig.json 的 BaseCost/EffectColor）推导出「元素消耗代价」，
    /// 使费用成为配置表唯一权威：费用 = round(BaseCost × CostMultiplier × 效果值) 个 EffectColor 元素。
    /// 卡牌仍可在 effect.Costs 中显式声明非元素特殊代价（Sleep/SummonMaterial/弃牌 等）。
    /// 生物挂载折扣（落地延迟 d(C) 与存活期望的额外回合折）不在此处 —— 在 CardCostService 的组合层计价。
    /// </summary>
    public static class CostDerivationService
    {
        /// <summary>
        /// 推导一个效果定义的元素消耗代价（不含卡牌显式声明的特殊代价）。
        /// 同色多笔代价会按颜色合并为一笔，便于上层抵消按颜色聚合处理。
        /// </summary>
        public static List<CostInstance> DeriveElementCosts(EffectDefinition effect)
        {
            var byColor = new Dictionary<ManaType, int>();
            if (effect == null)
                return new List<CostInstance>();

            // 节点化：仅主序列原子（Kind==Atomic）计费；分支 then/else 奖励一律免费。
            // 退化：未配 Steps 的旧卡仍按扁平 Effects 线性计费（向后兼容）。
            if (effect.Steps != null && effect.Steps.Count > 0)
            {
                foreach (var step in effect.Steps)
                {
                    if (step == null) continue;
                    if (step.Kind == RuntimeStepKind.Atomic && step.Atomic != null)
                        AccumulateElementCost(step.Atomic, byColor);
                    // Kind==Branch：OutcomeGate 奖励免费，不计费。
                }
            }
            else if (effect.Effects != null)
            {
                foreach (var atom in effect.Effects)
                {
                    if (atom == null) continue;
                    AccumulateElementCost(atom, byColor);
                }
            }

            var list = new List<CostInstance>();
            foreach (var kv in byColor)
            {
                if (kv.Value <= 0) continue;
                list.Add(new CostInstance
                {
                    Type = CostType.ElementConsume,
                    Value = kv.Value,
                    ManaType = kv.Key
                });
            }
            return list;
        }

        private static void AccumulateElementCost(AtomicEffectInstance atom, Dictionary<ManaType, int> byColor)
        {
            var cfg = AtomicEffectTable.GetByType(atom.Type);

            // 动态数量原子：费用计 0（代价 = 该卡不可作地牌产元素，见 ElementPool.AddCardToPool）。
            if (atom.DynamicTargetCount)
            {
                AccumulateSubEffects(atom, byColor);
                return;
            }

            int amount = ComputeAtomCost(atom, cfg);
            if (amount > 0)
            {
                var color = ElementAffinities.GetAffinityForEffect(atom.Type).PrimaryColor;
                byColor.TryGetValue(color, out var prev);
                byColor[color] = prev + amount;
            }

            AccumulateSubEffects(atom, byColor);
        }

        /// <summary>
        /// 单个原子的元素费用：
        /// 检索＝筛选维度系数；其余＝round(BaseCost×CostMultiplier×max(1,Value)×持续折扣)，
        /// 固定数量再 ×N；抽牌按所挂减费缺陷累减（下限 0）。
        ///
        /// 持续时间计价（时间换费用）：折扣取「相对折扣」＝ D(实际持续)/D(表内默认持续)。
        /// 实际持续 = 实例显式指定（Duration≠Once）？实例 : 表默认。
        /// 伤害等瞬发原子（表默认 Once、实例也 Once）系数恒为 1，锚点（1 伤害=1 元素）不漂移；
        /// 把本可永续的效果改短（如 buff 配 UntilEndOfTurn）即打折，拉长（永续化）即加价。
        /// 系数表见 ValueSystemRuntimeConfig.AttributeValueConfig（凹函数，P3 可调）。
        /// </summary>
        private static int ComputeAtomCost(AtomicEffectInstance atom, AtomicEffectConfig cfg)
        {
            // 检索：按筛选维度档计费，替代通用公式。维度档存于 atom.StringValue（默认单一维度）。
            if (atom.Type == AtomicEffectType.SearchDeck)
                return FilterPrecisionCost(atom);

            if (cfg == null || cfg.BaseCost <= 0f)
                return 0;

            // CostMultiplier 在表加载时默认 1.0；目标范围等可在配置中放大费用。
            float multiplier = cfg.CostMultiplier > 0f ? cfg.CostMultiplier : 1f;
            int magnitude = Math.Max(1, atom.Value);

            // 持续折扣（相对）：实例 Once(0) 视为未指定 → 取表默认，此时分子分母相同 → 1。
            var attrCfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().AttributeValueConfig;
            DurationType tableDefault = cfg.DurationType;
            DurationType actual = atom.Duration != DurationType.Once ? atom.Duration : tableDefault;
            float durationFactor = attrCfg.GetDurationDiscount(actual, atom.DurationValue)
                                   / attrCfg.GetDurationDiscount(tableDefault);

            int amount = (int)Math.Round(cfg.BaseCost * multiplier * magnitude * durationFactor, MidpointRounding.AwayFromZero);

            // 衍生物落区系数（P1 定案）：按落区分档计价（战场基准/手牌溢价/牌组微溢价）。
            // 落区取实例级 atom.ZoneParam（注意 Zone.Hand==0：合成界面必须显式填落区，计价不做默认猜测）。
            if (atom.Type == AtomicEffectType.SummonToken)
            {
                var dropCfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().SummonDropConfig;
                amount = (int)Math.Round(amount * dropCfg.GetFactor(atom.ZoneParam), MidpointRounding.AwayFromZero);
            }

            // 固定数量：费用 ×N（N=有效目标数；全部/任意语义无法在构建期确定，按 1）。
            int n = EffectiveTargetCountForCost(atom, cfg);
            if (n > 1) amount *= n;

            // 抽牌减费缺陷：每挂一个按 BranchConfig.json 给的减免累减，下限 0。
            if (atom.Type == AtomicEffectType.DrawCard && atom.Drawbacks != null)
            {
                foreach (var dbId in atom.Drawbacks)
                {
                    if (string.IsNullOrEmpty(dbId)) continue;
                    var db = BranchConfigTable.GetDrawback(dbId);
                    if (db != null) amount -= db.CostReduction;
                }
                if (amount < 0) amount = 0;
            }

            return amount;
        }

        private static int FilterPrecisionCost(AtomicEffectInstance atom)
        {
            // 检索改宣言卡名（2026-09-03 定案）：StringValue = 宣言的卡名（构筑期预置），
            // 精度恒为按名称检索（ExactCard 单档=3）；未预置宣言名视为最低档 1。
            if (string.IsNullOrEmpty(atom.StringValue)) return 1;
            var tier = BranchConfigTable.GetFilterTier("ExactCard");
            return tier != null ? tier.Cost : 3;
        }

        private static int EffectiveTargetCountForCost(AtomicEffectInstance atom, AtomicEffectConfig cfg)
        {
            int count = cfg != null ? cfg.TargetCount : 0;
            if (atom.TargetCountOverride != -2) count = atom.TargetCountOverride;
            return count;
        }

        private static void AccumulateSubEffects(AtomicEffectInstance atom, Dictionary<ManaType, int> byColor)
        {
            // 元/复合效果的子效果同样计入费用。
            if (atom.SubEffects == null) return;
            foreach (var sub in atom.SubEffects)
            {
                if (sub == null) continue;
                AccumulateElementCost(sub, byColor);
            }
        }

        /// <summary>
        /// 卡牌是否含「动态数量」原子（主序列或分支 then/else 任一）。
        /// 含动态数量原子的卡费用计 0 且不可作地牌产元素（灵活使用的代价）。
        /// </summary>
        public static bool HasDynamicTargetEffect(CardData card)
        {
            if (card?.Effects == null) return false;
            foreach (var eff in card.Effects)
            {
                if (eff == null) continue;
                if (eff.Steps != null && eff.Steps.Count > 0)
                {
                    foreach (var step in eff.Steps)
                    {
                        if (step == null) continue;
                        if (step.atomic != null && step.atomic.DynamicTargetCount) return true;
                        if (AnyDynamic(step.thenSteps)) return true;
                        if (AnyDynamic(step.elseSteps)) return true;
                    }
                }
                else if (AnyDynamic(eff.AtomicEffects))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AnyDynamic(List<AtomicEffectEntry> entries)
        {
            if (entries == null) return false;
            foreach (var e in entries)
                if (e != null && e.DynamicTargetCount) return true;
            return false;
        }
    }
}
