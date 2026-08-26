using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌算费器 —— 把 ValueSystem 既有权重原语组装成「建议法力费用 + 拆解明细」。
    /// 项目原本无任何算费公式（CardData.TotalCost 只是被动求和），本类是唯一新增的编排层。
    ///
    /// 公式（启发式估值，非平衡定论；故 UI 允许手动覆盖）：
    ///   total = 类型基值 + 属性值(攻/血)
    ///         + Σ效果 Σ原子( 基础值(type,value)
    ///                       * 目标修正 * 多目标修正 * 时机修正 * 条件修正 * 持续修正 )
    ///         → 同类递减 / 多样协同
    ///   manaCost = ceil(total / ManaValueCoefficient)
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
            public float Total;
            public int ManaCost;
            public readonly List<BreakdownLine> Breakdown = new List<BreakdownLine>();
        }

        public static Result Calculate(CardData card)
        {
            var result = new Result();
            if (card == null)
            {
                return result;
            }

            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig();

            // 1) 卡类型基值
            float typeBase = cfg.CardTypeValueConfig.GetSupertypeBaseValue(card.Supertype);
            result.Breakdown.Add(new BreakdownLine($"类型基值 ({card.Supertype})", typeBase));
            float total = typeBase;

            // 2) 属性值（攻/血）。常驻类（非 Spell）享永久加成。
            int power = card.Power ?? 0;
            int life = card.Life ?? 0;
            bool isPermanent = card.Supertype != Cardtype.Spell;
            float statValue = cfg.AttributeValueConfig.CalculateStatValue(power, life, isPermanent);
            result.Breakdown.Add(new BreakdownLine($"属性值 (攻{power}/血{life}{(isPermanent ? " 永久" : "")})", statValue));
            total += statValue;

            // 3) 效果值
            var effectTypes = new HashSet<AtomicEffectType>();
            var typeCounts = new Dictionary<AtomicEffectType, int>();

            if (card.Effects != null)
            {
                foreach (var effect in card.Effects)
                {
                    if (effect == null)
                    {
                        continue;
                    }

                    var timing = (TriggerTiming)effect.TriggerTiming;
                    float timingMod = cfg.TimingModifierConfig.GetTimingModifier(timing);
                    var condLabels = ConditionLabels(effect.ActivationConditions);
                    float condMod = cfg.TimingModifierConfig.GetConditionModifier(condLabels);

                    // 节点化：仅主序列原子（Steps Kind==0）计费；分支 then/else 奖励免费，与运行时一致。
                    // 退化：未配 Steps 的旧卡按扁平 AtomicEffects（向后兼容）。
                    foreach (var atomic in MainSequenceAtomics(effect))
                    {
                        if (atomic == null || string.IsNullOrEmpty(atomic.EffectType))
                        {
                            continue;
                        }
                        if (!Enum.TryParse<AtomicEffectType>(atomic.EffectType, out var type))
                        {
                            continue;
                        }

                        effectTypes.Add(type);
                        typeCounts[type] = typeCounts.TryGetValue(type, out var c) ? c + 1 : 1;

                        // 动态数量：费用计 0（与运行时一致）。
                        if (atomic.DynamicTargetCount)
                        {
                            result.Breakdown.Add(new BreakdownLine($"效果 {type} (动态数量·计0)", 0f));
                            continue;
                        }

                        float baseValue = cfg.EffectValueConfig.GetAtomicEffectBaseValue(type, atomic.Value);

                        var atomicCfg = AtomicEffectTable.GetByType(type);
                        float targetMod = atomicCfg != null
                            ? cfg.TargetModifierConfig.GetTargetModifier(atomicCfg.TargetType)
                            : 1.0f;
                        float multiMod = atomicCfg != null
                            ? cfg.TargetModifierConfig.GetMultiTargetModifier(atomicCfg.TargetCount)
                            : 1.0f;

                        float durMod = 1.0f;
                        if (atomic.Duration > 0)
                        {
                            durMod = cfg.AttributeValueConfig.GetDurationDiscount((DurationType)atomic.Duration);
                        }

                        float atomicValue = baseValue * targetMod * multiMod * timingMod * condMod * durMod;

                        // 固定数量：费用 ×N（有效目标数 >1 时），与运行时一致。
                        int n = EffectiveCount(atomic, atomicCfg);
                        if (n > 1)
                        {
                            atomicValue *= n;
                        }

                        // 抽牌减费缺陷：每挂一个按目录减免累减（下限 0），与运行时一致。
                        if (type == AtomicEffectType.DrawCard && atomic.Drawbacks != null)
                        {
                            float reduce = 0f;
                            foreach (var dbId in atomic.Drawbacks)
                            {
                                if (string.IsNullOrEmpty(dbId)) continue;
                                var db = BranchConfigTable.GetDrawback(dbId);
                                if (db != null) reduce += db.CostReduction;
                            }
                            if (reduce > 0f)
                            {
                                atomicValue = Mathf.Max(0f, atomicValue - reduce);
                            }
                        }

                        result.Breakdown.Add(new BreakdownLine($"效果 {type} x{atomic.Value}", atomicValue));
                        total += atomicValue;
                    }
                }
            }

            // 4) 同类递减 + 多样协同
            foreach (var kv in typeCounts)
            {
                if (kv.Value >= cfg.SynergyConfig.DiscountStartsAt)
                {
                    float before = total;
                    total = cfg.SynergyConfig.CalculateSameTypeDiscount(kv.Value, total);
                    result.Breakdown.Add(new BreakdownLine($"同类递减 ({kv.Key} x{kv.Value})", total - before));
                }
            }
            if (effectTypes.Count >= 2)
            {
                float before = total;
                total = cfg.SynergyConfig.CalculateSynergyBonus(effectTypes, total);
                result.Breakdown.Add(new BreakdownLine($"多样协同 ({effectTypes.Count} 种)", total - before));
            }

            // 4.5) 代价抵扣 —— 用户为效果添加的额外代价（弃牌/支付生命/沉睡/召唤素材）
            //      抵扣部分费用（启发式权重）。元素消耗是效果默认费用的一部分，不在此重复抵扣。
            float offset = 0f;
            if (card.Effects != null)
            {
                foreach (var effect in card.Effects)
                {
                    if (effect?.Costs == null)
                    {
                        continue;
                    }
                    foreach (var cost in effect.Costs)
                    {
                        if (cost == null)
                        {
                            continue;
                        }
                        float o = CostOffset(cost);
                        if (o > 0f)
                        {
                            offset += o;
                            result.Breakdown.Add(new BreakdownLine($"代价抵扣 ({CostTypeName(cost.CostType)})", -o));
                        }
                    }
                }
            }
            total = Mathf.Max(0f, total - offset);

            // 5) 折算法力费用
            float coeff = cfg.CostValueConfig.ManaValueCoefficient;
            if (coeff <= 0f)
            {
                coeff = 1.0f;
            }
            result.Total = total;
            result.ManaCost = Mathf.Max(0, Mathf.CeilToInt(total / coeff));
            return result;
        }

        // 单条代价的费用抵扣（启发式权重）。元素消耗(0)不抵扣（已是效果默认费用）。
        private static float CostOffset(CostEntry cost)
        {
            switch ((CostType)cost.CostType)
            {
                case CostType.DiscardCard: return 1.5f * Mathf.Max(1, cost.Value);
                case CostType.LifePayment: return 0.5f * Mathf.Max(1, cost.Value);
                case CostType.Sleep: return 1.0f * Mathf.Max(1, cost.TurnDuration);
                case CostType.SummonMaterial: return 1.0f * Mathf.Max(1, cost.Value);
                default: return 0f; // ElementConsume 等
            }
        }

        /// <summary>CostType → 中文名（供 UI 与明细展示）。</summary>
        public static string CostTypeName(int costType)
        {
            return (CostType)costType switch
            {
                CostType.ElementConsume => "元素消耗",
                CostType.DiscardCard => "弃牌",
                CostType.LifePayment => "支付生命",
                CostType.Sleep => "沉睡",
                CostType.SummonMaterial => "召唤素材",
                _ => costType.ToString(),
            };
        }

        // 主序列原子：Steps 非空时取 Kind==0 原子（分支 then/else 奖励免费，不计）；否则退化为扁平 AtomicEffects。
        private static List<AtomicEffectEntry> MainSequenceAtomics(CardEffectData effect)
        {
            var list = new List<AtomicEffectEntry>();
            if (effect.Steps != null && effect.Steps.Count > 0)
            {
                foreach (var step in effect.Steps)
                {
                    if (step == null) continue;
                    if (step.kind == 0 && step.atomic != null)
                    {
                        list.Add(step.atomic);
                    }
                    // kind==1：then/else 奖励免费，不计费。
                }
            }
            else if (effect.AtomicEffects != null)
            {
                list.AddRange(effect.AtomicEffects);
            }
            return list;
        }

        // 有效目标数：实例覆盖优先（-2=用配置；-1=任意、0=全部 为合法语义，按 1 处理）。
        private static int EffectiveCount(AtomicEffectEntry atomic, AtomicEffectConfig cfg)
        {
            int count = cfg != null ? cfg.TargetCount : 0;
            if (atomic.TargetCountOverride != -2)
            {
                count = atomic.TargetCountOverride;
            }
            return count;
        }

        // 把发动条件转成字符串列表（GetConditionModifier 只关心数量）。
        private static List<string> ConditionLabels(List<ActivationConditionData> conditions)
        {
            var labels = new List<string>();
            if (conditions != null)
            {
                foreach (var c in conditions)
                {
                    if (c != null)
                    {
                        labels.Add(((ConditionType)c.Type).ToString());
                    }
                }
            }
            return labels;
        }
    }
}
