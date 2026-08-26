using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardCore
{
    public static class CardEffectConverter
    {
        public static List<EffectDefinition> ConvertAll(List<CardEffectData> effectDataList, string sourceCardId)
        {
            var results = new List<EffectDefinition>();
            if (effectDataList == null) return results;

            foreach (var data in effectDataList)
            {
                var def = ConvertOne(data, sourceCardId);
                if (def != null) results.Add(def);
            }
            return results;
        }

        public static EffectDefinition ConvertOne(CardEffectData data, string sourceCardId)
        {
            if (data == null) return null;

            // 解析 TriggerTiming
            TriggerTiming timing = data.TriggerTiming >= 0
                ? (TriggerTiming)data.TriggerTiming
                : TriggerTiming.Activate_Active;

            // 确定 ActivationType：未指定(0) 时根据 TriggerTiming 取默认值
            EffectActivationType activationType = data.ActivationType > 0
                ? (EffectActivationType)data.ActivationType
                : TriggerTimingDefaults.GetDefaultActivationType(timing);

            var def = new EffectDefinition
            {
                Id = string.IsNullOrEmpty(data.Id) ? $"EFF_{sourceCardId}" : data.Id,
                DisplayName = data.DisplayName ?? "",
                Description = data.Description ?? "",
                TriggerTiming = timing,
                ActivationType = activationType,
                BaseSpeed = data.BaseSpeed,
                IsOptional = data.IsOptional,
                Duration = data.Duration > 0 ? (DurationType)data.Duration : DurationType.Permanent,
                SourceCardId = sourceCardId,
            };

            // 转换原子效果列表
            if (data.AtomicEffects != null)
            {
                foreach (var entry in data.AtomicEffects)
                {
                    var instance = ConvertAtomicEffect(entry);
                    if (instance != null)
                        def.Effects.Add(instance);
                }
            }

            // 转换节点化步骤（原子 + per-target 条件分支）。
            // 非空时执行引擎走步骤遍历；为空时退化为扁平 Effects（向后兼容）。
            if (data.Steps != null && data.Steps.Count > 0)
            {
                foreach (var step in data.Steps)
                {
                    var runtimeStep = ConvertStep(step);
                    if (runtimeStep != null)
                        def.Steps.Add(runtimeStep);
                }
            }

            // 转换代价列表
            if (data.Costs != null)
            {
                foreach (var costEntry in data.Costs)
                {
                    def.Costs.Add(new CostInstance
                    {
                        Type = (CostType)costEntry.CostType,
                        Value = costEntry.Value,
                        ManaType = (ManaType)costEntry.ManaType,
                    });
                }
            }

            // 转换发动条件
            if (data.ActivationConditions != null)
            {
                foreach (var cond in data.ActivationConditions)
                {
                    def.ActivationConditions.Add(ConvertCondition(cond));
                }
            }

            // 转换触发条件
            if (data.TriggerConditions != null)
            {
                foreach (var cond in data.TriggerConditions)
                {
                    def.TriggerConditions.Add(ConvertCondition(cond));
                }
            }

            // 标签
            if (data.Tags != null)
                def.Tags = new List<string>(data.Tags);

            return def;
        }

        private static AtomicEffectInstance ConvertAtomicEffect(AtomicEffectEntry entry)
        {
            if (string.IsNullOrEmpty(entry.EffectType))
            {
                Debug.LogWarning("[CardEffectConverter] AtomicEffectEntry.EffectType 为空，跳过");
                return null;
            }

            if (!Enum.TryParse<AtomicEffectType>(entry.EffectType, out var type))
            {
                Debug.LogWarning($"[CardEffectConverter] 无法解析 AtomicEffectType: {entry.EffectType}，跳过");
                return null;
            }

            // 从 AtomicEffectTable 获取默认 Duration
            DurationType duration = DurationType.Once;
            var config = CardCore.Attribute.AtomicEffectTable.GetByType(type);
            if (config != null)
                duration = (DurationType)config.DurationType;

            // 如果条目显式指定了 Duration，覆盖默认值
            if (entry.Duration > 0)
                duration = (DurationType)entry.Duration;

            return new AtomicEffectInstance
            {
                Type = type,
                Value = entry.Value,
                Value2 = entry.Value2,
                StringValue = entry.StringValue ?? "",
                ManaTypeParam = (ManaType)entry.ManaTypeParam,
                ZoneParam = (Zone)entry.ZoneParam,
                Duration = duration,
                TargetTypeOverride = entry.TargetTypeOverride,
                TargetFilterOverride = entry.TargetFilterOverride ?? "",
                TargetCountOverride = entry.TargetCountOverride,
                DynamicTargetCount = entry.DynamicTargetCount,
                Drawbacks = entry.Drawbacks != null ? new List<string>(entry.Drawbacks) : new List<string>(),
            };
        }

        private static RuntimeEffectStep ConvertStep(EffectStepData step)
        {
            if (step == null) return null;

            // kind: 0=原子, 1=条件分支
            if (step.kind == 0)
            {
                var atomic = step.atomic != null ? ConvertAtomicEffect(step.atomic) : null;
                if (atomic == null) return null;
                return new RuntimeEffectStep
                {
                    Kind = RuntimeStepKind.Atomic,
                    Atomic = atomic,
                };
            }

            // 条件分支（OutcomeGate）：then/else 为扁平原子列表（单层）
            var branch = new RuntimeEffectStep
            {
                Kind = RuntimeStepKind.Branch,
                ConditionId = step.conditionId,
                ConditionParam = step.conditionParam,
                ConditionStringParam = step.conditionStringParam,
                Negate = false,
            };

            if (step.thenSteps != null)
            {
                foreach (var entry in step.thenSteps)
                {
                    var inst = ConvertAtomicEffect(entry);
                    if (inst != null) branch.Then.Add(inst);
                }
            }
            if (step.elseSteps != null)
            {
                foreach (var entry in step.elseSteps)
                {
                    var inst = ConvertAtomicEffect(entry);
                    if (inst != null) branch.Else.Add(inst);
                }
            }
            return branch;
        }

        private static ActivationCondition ConvertCondition(ActivationConditionData data)
        {
            return new ActivationCondition
            {
                Type = (ConditionType)data.Type,
                Value = data.Value,
                Value2 = data.Value2,
                Negate = data.Negate,
                StringValue = data.StringValue,
            };
        }
    }
}
