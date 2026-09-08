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

            // 解析 TriggerTiming（防线：越界 int 强转会得到幽灵枚举值、触发式静默哑火——告警并回退登场）
            TriggerTiming timing;
            if (data.TriggerTiming >= 0 && Enum.IsDefined(typeof(TriggerTiming), data.TriggerTiming))
            {
                timing = (TriggerTiming)data.TriggerTiming;
            }
            else
            {
                if (data.TriggerTiming >= 0)
                    Debug.LogWarning($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 的 TriggerTiming={data.TriggerTiming} 越界（枚举收敛后重排），回退 OnPlay");
                timing = data.TriggerTiming < 0 ? TriggerTiming.Activate_Active : TriggerTiming.OnPlay;
            }

            // 确定 ActivationType：未指定(0) 时根据 TriggerTiming 取默认值
            EffectActivationType activationType = data.ActivationType > 0
                ? (EffectActivationType)data.ActivationType
                : TriggerTimingDefaults.GetDefaultActivationType(timing);

            // 启动式（Activate_*，2026-09-08 定案）：构筑期不占卡费（L2 只占挂载口），
            // 元素锚价运行时现付——发动 = 横置 + 扣锚价元素 + 选定目标。
            // 非启动式（触发式）照旧：元素费已随卡牌档位费收讫，执行器跳过防双计。
            bool isActivated = timing == TriggerTiming.Activate_Active
                            || timing == TriggerTiming.Activate_Instant
                            || timing == TriggerTiming.Activate_Response;

            // 时机/发动方式对称校验（2026-09-08）：非启动式必须设具体发动时机
            // （登场/死亡/离场/受攻击等——越界 int 已在上面回退 OnPlay 并告警）；
            // 启动式只能是主动发动（玩家轮询选择），声明为强制/自动属数据错误——告警并按主动处理。
            if (isActivated && activationType != EffectActivationType.Voluntary)
            {
                Debug.LogWarning($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 为启动式（{timing}）" +
                                 $"但 ActivationType={activationType}（启动式只能主动发动），按主动处理");
                activationType = EffectActivationType.Voluntary;
            }

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
                ElementCostPrepaid = !isActivated,
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
                        TurnDuration = costEntry.TurnDuration, // 沉睡/自身紊乱等按回合计持续的代价
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
            // （表侧 DurationType 已统一为运行时 DurationType，直接赋值即可；
            //  旧实现按 int 跨枚举强转，Permanent(4) 会被错映射成 WhileCondition(4)，已修复）
            DurationType duration = DurationType.Once;
            var config = CardCore.Attribute.AtomicEffectTable.GetByType(type);
            if (config != null)
                duration = config.DurationType;

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
                DurationValue = entry.DurationValue,
                TargetTypeOverride = entry.TargetTypeOverride,
                TargetFilterOverride = entry.TargetFilterOverride ?? "",
                TargetCountOverride = entry.TargetCountOverride,
                DynamicTargetCount = entry.DynamicTargetCount,
                Drawbacks = entry.Drawbacks != null ? new List<string>(entry.Drawbacks) : new List<string>(),
            };
        }

        /// <summary>
        /// 主序列原子枚举（声明期目标扫描用，UI/AI 共用）：直行原子 + 抉择步骤所选模式内的原子。
        /// 分支奖励原子不扫——主序列才在声明期选目标，奖励目标由结算期各自解析。
        /// </summary>
        public static IEnumerable<AtomicEffectInstance> EnumerateMainSequenceAtoms(
            List<RuntimeEffectStep> steps, int modeIndex)
        {
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.Kind == RuntimeStepKind.Atomic && step.Atomic != null)
                {
                    yield return step.Atomic;
                }
                else if (step.Kind == RuntimeStepKind.Choice)
                {
                    var chosen = step.Choices != null && step.Choices.Count > 0
                        ? step.Choices[Math.Max(0, Math.Min(modeIndex, step.Choices.Count - 1))]
                        : null;
                    if (chosen == null) continue;
                    foreach (var s in chosen)
                        if (s != null && s.Kind == RuntimeStepKind.Atomic && s.Atomic != null)
                            yield return s.Atomic;
                }
            }
        }

        private static RuntimeEffectStep ConvertStep(EffectStepData step)
        {
            if (step == null) return null;

            // kind: 0=原子, 1=条件分支, 2=抉择
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

            // 抉择（Choice）：≥2 选发模式，每模式=原子+紧邻分支的子序列（不可嵌套）。
            // 容错定案：choices 缺失/有效模式 <2 → 整步跳过（等价不存在），费用按无抉择推导。
            if (step.kind == 2)
            {
                if (step.choices == null || step.choices.Count < 2)
                {
                    UnityEngine.Debug.LogWarning("[CardEffectConverter] 抉择步骤 choices 缺失或 <2，跳过该步骤");
                    return null;
                }

                var choice = new RuntimeEffectStep { Kind = RuntimeStepKind.Choice };
                choice.Choices = new List<List<RuntimeEffectStep>>();
                foreach (var c in step.choices)
                {
                    var seq = new List<RuntimeEffectStep>();
                    if (c?.steps != null)
                    {
                        foreach (var s in c.steps)
                        {
                            var rs = ConvertStep(s); // 复用：原子/紧邻分支照旧转换
                            if (rs == null) continue;
                            if (rs.Kind == RuntimeStepKind.Choice)
                            {
                                UnityEngine.Debug.LogWarning("[CardEffectConverter] 抉择不可嵌套，跳过内层抉择");
                                continue;
                            }
                            seq.Add(rs);
                        }
                    }
                    choice.Choices.Add(seq);
                }
                if (choice.Choices.Count < 2)
                {
                    UnityEngine.Debug.LogWarning("[CardEffectConverter] 抉择步骤有效模式 <2，跳过该步骤");
                    return null;
                }
                return choice;
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
