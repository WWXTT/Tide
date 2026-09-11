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
                // 持续唯一真相在效果级（2026-09-10 上移定案）：卡数据显式携带（0=Once），
                // 哨兵 -1=未声明回退 Once（绝对计价锚）
                Duration = data.Duration >= 0 ? (DurationType)data.Duration : DurationType.Once,
                DurationValue = data.DurationValue,
                SummonDropZone = (Zone)data.SummonDropZone,
                SelectionMode = data.SelectionMode >= 0 ? (SelectionMode)data.SelectionMode : SelectionMode.None,
                DynamicTargetCount = data.DynamicTargetCount,
                SourceCardId = sourceCardId,
                ElementCostPrepaid = !isActivated,
            };
            if (data.Drawbacks != null)
                def.Drawbacks = new List<string>(data.Drawbacks);

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

            // ---- 组合层域预计算（2026-09-10 目标域模型）----
            // 主序列原子域交集 → TargetDomain（无 Choice）/ ChoiceDomains（per-mode）；
            // 组合 filter = 成员带域原子 Filter token 之 AND；TargetCount 哨兵 -2 回落表级。
            PrecomputeDomains(def);
            def.TargetCount = data.TargetCount != -2 ? data.TargetCount : FallbackTargetCount(def);

            // Self 域找回（2026-09-11）：组合域恰为 {Self}（关键词/关键词型效果）且未显式声明选择模式
            // → 自动 SelectionMode=Self（解析=源卡自身，不弹交互）。
            if (def.TargetDomain != null && def.TargetDomain.Count == 1
                && def.TargetDomain[0] == (int)TargetKind.Self && data.SelectionMode < 0)
            {
                def.SelectionMode = SelectionMode.Self;
            }

            // 转换代价列表
            if (data.Costs != null)
            {
                foreach (var costEntry in data.Costs)
                {
                    // 效果型代价（2026-09-11）：付费步强制执行的原子（执行与补偿在 CostCompensationService）。
                    // 内容契约：代价只能挂对自己有害 / 对对手有益——p≠0 必须错边；p=0 须单侧域锁定（方向随域）。
                    // 双侧域/无域 = 中性，既非代价也非收益 → 拒。
                    if (costEntry.payload != null)
                    {
                        var payloadAtom = ConvertAtomicEffect(costEntry.payload, allowWrongSide: true);
                        bool wrongSide = payloadAtom != null && payloadAtom.Polarity != 0f
                            && CostDerivationService.WrongSide(payloadAtom.Polarity, payloadAtom.TargetKinds);
                        bool sideLockedNeutral = payloadAtom != null && payloadAtom.Polarity == 0f
                            && CostDerivationService.SideLock(payloadAtom.TargetKinds) != 0;
                        if (payloadAtom == null || !(wrongSide || sideLockedNeutral))
                        {
                            Debug.LogError($"[CardEffectConverter] 卡 {sourceCardId} 代价栏 Payload 违反内容契约：" +
                                           "代价只能挂对自己有害或对对手有益的原子（错边），应当剔除该代价");
                            continue;
                        }
                        def.Costs.Add(new CostInstance
                        {
                            Type = (CostType)costEntry.CostType,
                            Value = costEntry.Value,
                            ManaType = (ManaType)costEntry.ManaType,
                            TurnDuration = costEntry.TurnDuration,
                            Payload = payloadAtom,
                        });
                        continue;
                    }

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

        private static AtomicEffectInstance ConvertAtomicEffect(AtomicEffectEntry entry, bool allowWrongSide = false)
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

            // 目标域：条目显式收窄 ?? 表级默认（解析为有效域存实例，运行时零查表）。
            // 空列表 = 未收窄（迁移脚本写 []，语义同缺省）——只有表级默认为空才是真无目标原子
            var config = CardCore.Attribute.AtomicEffectTable.GetByType(type);
            List<int> kinds = entry.TargetKinds != null && entry.TargetKinds.Count > 0
                ? new List<int>(entry.TargetKinds)
                : config?.GetTargetKindList() ?? new List<int>();

            float polarity = config != null ? UnityEngine.Mathf.Clamp(config.Polarity, -1f, 1f) : 0f;

            // 内容契约（2026-09-11 定案）：效果栏（主动/被动效果）只能挂对自己有益或中性的原子——
            // 错边锁定（有益锁对方域 / 有害锁己方域 = 对自己有害或对对手有益）只能进代价栏（Payload）。
            // 中性（p=0）与双侧域不受限（双侧「同时作用双方」不支持——需要时制作专用原子，先不管）。
            if (!allowWrongSide && polarity != 0f && CostDerivationService.WrongSide(polarity, kinds))
            {
                Debug.LogError($"[CardEffectConverter] 原子 {type}（极性 {polarity:0.#}，域 [{string.Join(",", kinds)}]）" +
                               "违反内容契约：效果栏不可挂错边原子（对自己有害/对对手有益只能进代价栏），应当剔除该效果");
                return null;
            }

            return new AtomicEffectInstance
            {
                Type = type,
                Value = entry.Value,
                StringValue = entry.ID ?? "",
                Mana = BuildMana(entry.ManaList),
                TargetKinds = kinds,
                Filter = config?.TargetFilter ?? "",
                Polarity = polarity,
            };
        }

        /// <summary>构筑期显示用：代价栏 Payload 条目 → 原子实例（允许错边——契约校验在代价转换处；
        /// 供 CardCostService 计算"获得白16"显示行）。</summary>
        public static AtomicEffectInstance ConvertPayloadForDisplay(AtomicEffectEntry entry)
            => entry == null ? null : ConvertAtomicEffect(entry, allowWrongSide: true);

        /// <summary>ManaList（costList 同款条目）→ 字典；null/空 → null（无 Mana 参数语义）。</summary>
        private static Dictionary<ManaType, float> BuildMana(List<ManaAmountEntry> list)
        {
            if (list == null || list.Count == 0) return null;
            var dict = new Dictionary<ManaType, float>();
            foreach (var m in list)
                if (m.amount > 0)
                    dict[(ManaType)m.manaType] = m.amount;
            return dict.Count > 0 ? dict : null;
        }

        /// <summary>
        /// 组合域预计算：主序列（含抉择 per-mode）原子域交集 + 组合属性 filter（成员 AND）。
        /// 无目标原子（域空）不参与约束；分支奖励原子不参与（奖励目标结算期各自解析）。
        /// </summary>
        private static void PrecomputeDomains(EffectDefinition def)
        {
            def.TargetDomain = DomainOfMode(def, 0);
            def.TargetFilter = CombinedFilterOfMode(def, 0);

            // 抉择卡：per-mode 域（与 Choices 平行）；无 Choice 时 ChoiceDomains 保持 null
            int modeCount = 0;
            foreach (var step in def.Steps)
            {
                if (step?.Kind == RuntimeStepKind.Choice && step.Choices != null)
                {
                    modeCount = step.Choices.Count;
                    break;
                }
            }
            if (modeCount > 0)
            {
                def.ChoiceDomains = new List<int>[modeCount];
                for (int m = 0; m < modeCount; m++)
                    def.ChoiceDomains[m] = DomainOfMode(def, m);
            }
        }

        /// <summary>某模式的主序列原子（Steps 非空走步骤枚举，否则扁平 Effects）。</summary>
        private static IEnumerable<AtomicEffectInstance> MainSequence(EffectDefinition def, int modeIndex)
        {
            if (def.Steps != null && def.Steps.Count > 0)
                return EnumerateMainSequenceAtoms(def.Steps, modeIndex);
            return def.Effects;
        }

        private static List<int> DomainOfMode(EffectDefinition def, int modeIndex)
        {
            List<int> domain = null;
            foreach (var atom in MainSequence(def, modeIndex))
            {
                if (atom?.TargetKinds == null || atom.TargetKinds.Count == 0) continue;
                domain = domain == null
                    ? new List<int>(atom.TargetKinds)
                    : TargetKindRules.Intersect(domain, atom.TargetKinds);
            }
            return domain ?? new List<int>();
        }

        /// <summary>组合 filter：成员带域原子 Filter token 的并集去重（token 间 AND 语义，多原子合并同款）。</summary>
        private static string CombinedFilterOfMode(EffectDefinition def, int modeIndex)
        {
            var tokens = new List<string>();
            foreach (var atom in MainSequence(def, modeIndex))
            {
                if (atom?.TargetKinds == null || atom.TargetKinds.Count == 0) continue;
                foreach (var t in (atom.Filter ?? "").Split(','))
                {
                    var trimmed = t.Trim();
                    if (trimmed.Length > 0 && !tokens.Contains(trimmed))
                        tokens.Add(trimmed);
                }
            }
            return string.Join(",", tokens);
        }

        /// <summary>TargetCount 哨兵回落（2026-09-10 表级列删除后）：未声明 = 1（卡数据已全量回填真值，
        /// 此口仅为手写新卡的兜底，不再查表）。</summary>
        private static int FallbackTargetCount(EffectDefinition def)
        {
            return 1;
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
