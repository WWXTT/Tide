using System;
using System.Collections.Generic;
using System.Linq;

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
                    TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 的 TriggerTiming={data.TriggerTiming} 越界（枚举收敛后重排），回退 OnPlay");
                timing = data.TriggerTiming < 0 ? TriggerTiming.Activate_Active : TriggerTiming.OnPlay;
            }

            // 确定 ActivationType：未指定(0) 时根据 TriggerTiming 取默认值
            EffectActivationType activationType = data.ActivationType > 0
                ? (EffectActivationType)data.ActivationType
                : TriggerTimingDefaults.GetDefaultActivationType(timing);

            // 主动⇔启动式双向钉死（2026-10-02 定案）：发动方式三值——主动=启动式（玩家轮询发动，
            // 横置+现付锚价）、自动=可选触发式（条件达成弹窗询问）、强制=无条件触发。
            // 主动声明带触发时机（合成器旧 bug 只写 ActivationType 不写 Activate 时机/手写 JSON）
            // → 告警并钉时机 Activate_Active（主阶段主动档；反向"启动式非主动"由下方对称校验收口）。
            if (activationType == EffectActivationType.Voluntary
                && timing != TriggerTiming.Activate_Active
                && timing != TriggerTiming.Activate_Instant
                && timing != TriggerTiming.Activate_Response)
            {
                TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 发动方式=主动但时机={timing}" +
                                 "（主动即启动式——2026-10-02 定案），时机已钉 Activate_Active");
                timing = TriggerTiming.Activate_Active;
            }

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
                TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 为启动式（{timing}）" +
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
                SummonDropZone = (Zone)data.SummonDropZone,
                SelectionMode = data.SelectionMode >= 0 ? (SelectionMode)data.SelectionMode : SelectionMode.None,
                RandomTarget = data.RandomTarget != 0,
                SourceCardId = sourceCardId,
                ElementCostPrepaid = !isActivated,
            };

            // 关键词型原子不得作启动式（2026-10-02 定案）：启动式效果内的纯自指 Grant 原子
            //（=给自己加关键词——横置换静态身份无意义）告警剔除（内容契约"错边剔除"同款先例）；
            // 赋予型 Grant（域含其他目标）不受限。判定委托 ComposerCatalog.IsKeywordStyleGrant（与合成器 UI 同源）。
            if (isActivated)
            {
                int removed = RemoveKeywordStyleGrants(data);
                if (removed > 0)
                    TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 为启动式：" +
                                 $"已剔除 {removed} 个关键词型 Grant 原子（关键词不得作为主动效果；赋予型不受限）");
            }

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

            // 拦截式改写门配对守卫（2026-09-22 定案）：改写门（DmgRewrite*）必须紧跟伤害族主干原子
            //（DealDamage/PierceDamage/DrainLife）——失配（手写 JSON/旧数据）告警剔除，
            // 防改写拦截在非伤害原子上空转。倒序遍历防索引漂移。
            for (int i = def.Steps.Count - 1; i >= 0; i--)
            {
                var s = def.Steps[i];
                if (s?.Kind != RuntimeStepKind.Branch || !BranchConditionEvaluator.IsRewriteCondition(s.ConditionId))
                    continue;
                bool paired = i > 0 && def.Steps[i - 1]?.Kind == RuntimeStepKind.Atomic
                              && Array.IndexOf(ComposerCatalog.DamageProducers,
                                  def.Steps[i - 1].Atomic?.Type.ToString() ?? "") >= 0;
                if (paired) continue;
                TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {def.Id} 改写门 {s.ConditionId}" +
                                 " 未紧跟伤害族主干原子，已剔除（改写门只挂 DealDamage/PierceDamage/DrainLife）");
                def.Steps.RemoveAt(i);
            }

            // ---- 组合层域预计算（2026-09-10 目标域模型）----
            // 主序列原子域交集 → TargetDomain（无 Choice）/ ChoiceDomains（per-mode）；
            // 组合 filter = 成员带域原子 Filter token 之 AND；TargetCount 哨兵 -2 回落表级。
            PrecomputeDomains(def);
            // 动态分支引擎（2026-09-13 分支体系正规化）：主效果=条件引擎——
            // AtomicEffects 整批转存 RewardAtoms（不作即时主序列；计价 0：倒计时延迟即付费/运势机制费=灰x）。
            if (data.EngineKind != (int)BranchEngineKind.None)
            {
                def.EngineKind = (BranchEngineKind)data.EngineKind;
                def.EngineParam = data.EngineParam;
                def.RewardAtoms = def.Effects;
                def.Effects = new List<AtomicEffectInstance>();
                if (def.EngineKind == BranchEngineKind.Countdown)
                    def.CountdownTurns = data.EngineParam > 0
                        ? data.EngineParam
                        : Math.Max(1, (int)Math.Ceiling(CostDerivationService.RewardDerivedCost(def.RewardAtoms)));
            }

            // 胜利宣判闸（2026-09-16 定案）：含 DeclareVictory 原子的效果一律强制——纯强制批
            // 合成双 Pass 直接结算（宣判即终局，见 StackEngine.FinishResolution 拆轮）。
            // 启动式（玩家主动发动）与此语义互斥：启动式声明 DeclareVictory 属数据错误，
            // 告警跳闸（上方守卫已按主动处理——玩家自发宣胜利的卡不该存在）。
            if (ContainsAtom(def, AtomicEffectType.DeclareVictory))
            {
                if (isActivated)
                    TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {def.Id} 启动式声明 DeclareVictory" +
                                     "（数据错误——胜利宣判不可主动发动），按主动处理保留");
                else if (activationType != EffectActivationType.Mandatory)
                {
                    TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {def.Id} 含 DeclareVictory 原子，" +
                                     $"ActivationType={activationType} 覆写为 Mandatory（胜利宣判一律强制）");
                    activationType = EffectActivationType.Mandatory;
                    def.ActivationType = activationType;
                }
            }

            // 触发上限解析（2026-09-13 定案）：含 TriggerCapImmutable(8) 原子（少数，如坚韧）→ 恒 -1
            //（不可修改——声明被覆写，CardLoader 同步告警）；其余原子 → 未声明(0)=1（一回合一次，默认可修改）。
            bool capImmutable =
                def.Effects.Any(a => a != null && MountHasFlag(a.Type, (int)MountKind.TriggerCapImmutable))
                || (def.Steps != null && EnumerateMainSequenceAtoms(def.Steps, 0)
                        .Any(a => a != null && MountHasFlag(a.Type, (int)MountKind.TriggerCapImmutable)));
            def.TriggerLimitPerTurn = capImmutable
                ? -1
                : (data.TriggerLimitPerTurn == 0 ? 1 : data.TriggerLimitPerTurn);

            def.TargetCount = data.TargetCount != -2 ? data.TargetCount : FallbackTargetCount(def);

            // Self 域找回（2026-09-11；2026-09-16 Self 溶解为 Single）：组合域恰为 {Self}
            //（关键词/关键词型效果）且未显式声明选择模式 → 自动 Single（选一=源卡自身，不弹交互）。
            if (def.TargetDomain != null && def.TargetDomain.Count == 1
                && def.TargetDomain[0] == (int)TargetKind.Self && data.SelectionMode < 0)
            {
                def.SelectionMode = SelectionMode.Single;
            }

            // 强制类目标闸（2026-09-16 三类弹窗口径定案）：强制类（Mandatory）自动入栈自动执行、
            // **无目标选择窗口**——目标必须构筑期明确。声明选一/选多（执行期弹选）属数据错误：
            // 告警并强制 WholeUnion（域内全取，与固有全域原子同口径）。
            // 构筑期可解析的例外放行：域={Self} 的 Single（解析=源卡自身）、RandomTarget（种子自动抽取）、全取档。
            if (activationType == EffectActivationType.Mandatory
                && (SelectionModeRules.IsPickOne(def.SelectionMode) || SelectionModeRules.IsPickMany(def.SelectionMode))
                && !def.RandomTarget
                && !(def.SelectionMode == SelectionMode.Single && def.TargetDomain != null
                     && def.TargetDomain.Count == 1 && def.TargetDomain[0] == (int)TargetKind.Self))
            {
                TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {def.Id} 为强制类但声明选一/选多" +
                                 "（强制类无目标选择窗口，目标须构筑期明确）——已覆写为 WholeUnion");
                def.SelectionMode = SelectionMode.WholeUnion;
            }

            // 固有全域原子特判已删（2026-09-21 退役）——全域语义由组合期 TargetKinds+全取档表达；
            // 普通原子挂全取档照常计价（期望 4）。

            // 转换代价列表
            if (data.Costs != null)
            {
                foreach (var costEntry in data.Costs)
                {
                    // 效果型代价（2026-09-11）：付费步强制执行的原子（执行与补偿在 CostCompensationService）。
                    // 内容契约：代价只能挂对自己有害 / 对对手有益——p≠0 必须错边；p=0 须单侧域锁定（方向随域）。
                    // 双侧域/无域 = 中性，既非代价也非收益 → 拒。
                    // 2026-10-04 定案：任意单向效果不限价（09-21「等价1」退役）——合成器放置口经
                    // CostDerivationService.PayloadCostDomain 逆转选择范围（双侧收窄/正确侧镜像），
                    // 镜像域可超出表行域；装载期只校验上述错边契约（对镜像域天然成立），不再限价。
                    // 空 payload（refId 为空——非 Payload 代价的占位/迁移残留）不作 Payload 处理
                    if (costEntry.payload != null && !string.IsNullOrEmpty(costEntry.payload.refId))
                    {
                        var payloadAtom = ConvertAtomicEffect(costEntry.payload, allowWrongSide: true);
                        bool wrongSide = payloadAtom != null && payloadAtom.Polarity != 0f
                            && CostDerivationService.WrongSide(payloadAtom.Polarity, payloadAtom.TargetKinds);
                        bool sideLockedNeutral = payloadAtom != null && payloadAtom.Polarity == 0f
                            && CostDerivationService.SideLock(payloadAtom.TargetKinds) != 0;
                        if (payloadAtom == null || !(wrongSide || sideLockedNeutral))
                        {
                            TideLog.Error($"[CardEffectConverter] 卡 {sourceCardId} 代价栏 Payload 违反内容契约：" +
                                           "代价只能挂对自己有害或对对手有益的原子（错边），应当剔除该代价");
                            continue;
                        }
                        def.Costs.Add(new CostInstance
                        {
                            Type = CostType.Payload, // 带 payload 原子的条目恒为效果型代价（声明值不参与）
                            Value = costEntry.Value,
                            ManaType = (ManaType)costEntry.ManaType,
                            Payload = payloadAtom,
                        });
                        continue;
                    }

                    // 2026-09-14 代价原子化：仅剩 ElementConsume/Payload 两值——无 payload 的条目按元素代价透传
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

        // ======================================== 关键词型原子启动式校验（2026-10-02 定案） ========================================

        /// <summary>剔除启动式效果内的关键词型 Grant 条目：AtomicEffects（含引擎奖励落点）+ Steps 全树
        ///（原子步骤整步移除、then/else 列表移除、抉择分支递归）。返回剔除数。</summary>
        private static int RemoveKeywordStyleGrants(CardEffectData data)
        {
            if (data == null) return 0;
            int removed = data.AtomicEffects?.RemoveAll(IsKeywordStyleEntry) ?? 0;
            removed += RemoveKeywordStyleGrantsFromSteps(data.Steps);
            return removed;
        }

        private static int RemoveKeywordStyleGrantsFromSteps(List<EffectStepData> steps)
        {
            if (steps == null) return 0;
            int removed = steps.RemoveAll(s => s != null && s.kind == 0 && IsKeywordStyleEntry(s.atomic));
            foreach (var step in steps)
            {
                if (step == null) continue;
                removed += step.thenSteps?.RemoveAll(IsKeywordStyleEntry) ?? 0;
                removed += step.elseSteps?.RemoveAll(IsKeywordStyleEntry) ?? 0;
                if (step.choices == null) continue;
                foreach (var choice in step.choices)
                    removed += RemoveKeywordStyleGrantsFromSteps(choice?.steps);
            }
            return removed;
        }

        /// <summary>条目是否关键词型 Grant：refId → 表行 → 英文枚举名 → ComposerCatalog.IsKeywordStyleGrant
        ///（与合成器 UI 同源）。行缺失/枚举解析失败不判关键词型（交由后续装载校验点名）。</summary>
        private static bool IsKeywordStyleEntry(AtomicEffectEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.refId)) return false;
            var row = Attribute.AtomicEffectTable.GetByHashId(entry.refId);
            if (row == null || string.IsNullOrEmpty(row.EnumName)) return false;
            return Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)
                   && ComposerCatalog.IsKeywordStyleGrant(type);
        }

        /// <summary>UI 编辑出口（2026-09-14 合成器重做）：原子条目 → 运行时实例（内容契约照常生效——
        /// 错边剔除/主干守卫同装载转换）。供合成器预算校验（RewardDerivedCost）与描述预览消费。</summary>
        public static AtomicEffectInstance ConvertAtomForUI(AtomicEffectEntry entry)
            => ConvertAtomicEffect(entry);

        /// <summary>原子表 MountKinds 是否含指定位（触发上限解析用）。</summary>
        private static bool MountHasFlag(AtomicEffectType type, int flag)
        {
            var csv = Attribute.AtomicEffectTable.GetByType(type)?.MountKinds ?? "";
            foreach (var tok in csv.Split(','))
                if (tok.Trim() == flag.ToString()) return true;
            return false;
        }

        /// <summary>胜利宣判闸扫描：主序列原子 / 步骤全形态（原子+分支 Then/Else+抉择全模式）/
        /// 引擎奖励原子中是否含指定类型（SubEffects 递归）。</summary>
        private static bool ContainsAtom(EffectDefinition def, AtomicEffectType type)
        {
            if (AtomsContain(def.Effects, type) || AtomsContain(def.RewardAtoms, type)) return true;
            if (def.Steps == null) return false;
            foreach (var step in def.Steps)
                if (StepContainsAtom(step, type)) return true;
            return false;
        }

        private static bool StepContainsAtom(RuntimeEffectStep step, AtomicEffectType type)
        {
            if (step == null) return false;
            if (step.Kind == RuntimeStepKind.Atomic)
                return AtomContains(step.Atomic, type);
            if (step.Kind == RuntimeStepKind.Choice)
            {
                if (step.Choices == null) return false;
                foreach (var mode in step.Choices)
                    if (mode != null)
                        foreach (var s in mode)
                            if (StepContainsAtom(s, type)) return true;
                return false;
            }
            // Branch：Then/Else 为扁平原子列表
            return AtomsContain(step.Then, type) || AtomsContain(step.Else, type);
        }

        private static bool AtomsContain(List<AtomicEffectInstance> atoms, AtomicEffectType type)
        {
            if (atoms == null) return false;
            foreach (var a in atoms)
                if (AtomContains(a, type)) return true;
            return false;
        }

        private static bool AtomContains(AtomicEffectInstance atom, AtomicEffectType type)
        {
            if (atom == null) return false;
            if (atom.Type == type) return true;
            return AtomsContain(atom.SubEffects, type);
        }

        /// <summary>引用型唯一转换口（2026-09-14 彻底引用化）：refId → 表行 → 运行时实例。
        /// 枚举/默认域/Filter/极性/MountKinds 全部从行解析；增量只读 value/str/amp/kinds。</summary>
        private static AtomicEffectInstance ConvertAtomicEffect(AtomicEffectEntry entry, bool allowWrongSide = false)
        {
            if (entry == null || string.IsNullOrEmpty(entry.refId))
            {
                TideLog.Warn("[CardEffectConverter] 原子引用为空（refId 缺失——旧格式数据？），跳过");
                return null;
            }

            var config = CardCore.Attribute.AtomicEffectTable.GetByHashId(entry.refId);
            if (config == null || !Enum.TryParse<AtomicEffectType>(config.EnumName, out var type))
            {
                TideLog.Warn($"[CardEffectConverter] 原子表引用缺失: {entry.refId}（表无此行或枚举错名），跳过");
                return null;
            }

            // 引擎主干守卫（2026-09-14 合成器重做）：自由分支主干经 header.EngineKind 声明——
            // 不可作为普通原子/分支奖励/payload 挂载（表行 MountKinds 漏配或手写 JSON 误用在此拦截）。
            if (ComposerCatalog.IsEngineTrunk(type))
            {
                // 数据质量诊断（主干守卫是既定拦截，负面测试会故意触发——告警级与契约剔除同级）
                TideLog.Warn($"[CardEffectConverter] 引擎主干原子 {type} 不可作为普通原子挂载" +
                               "（自由分支经 header.EngineKind 声明），已剔除");
                return null;
            }

            // 目标域：条目显式收窄 kinds ?? 表行默认域（解析为有效域存实例，运行时零查表）
            List<int> kinds = entry.kinds != null && entry.kinds.Count > 0
                ? new List<int>(entry.kinds)
                : config.GetTargetKindList();

            float polarity = Math.Clamp(config.Polarity, -1f, 1f);

            // 内容契约（2026-09-11 定案；2026-10-03 用户定案收缩）：效果栏错边剔除只保留
            // 「有益原子锁对方域」（对对手有益）——仍只能进代价栏（Payload）；
            // 「有害原子锁己方域」（负面效果指向自己——苏醒式自缚设计）在效果栏放行，
            // 错边限制完整保留在代价栏（ConvertPayloadForDisplay 的 allowWrongSide 路径）。
            // 中性（p=0）与双侧域不受限（双侧「同时作用双方」由专用原子/组合表达）。
            if (!allowWrongSide && polarity > 0f
                && CostDerivationService.SideLock(kinds) == 1)
            {
                // 数据质量诊断（与 WarnCostNonConformance 同级）：错边原子被剔除——装载可见不炸
                TideLog.Warn($"[CardEffectConverter] 原子 {type}（极性 {polarity:0.#}，域 [{string.Join(",", kinds)}]）" +
                               "违反内容契约：效果栏不可挂「有益锁对方域」原子（对对手有益只能进代价栏），已剔除该效果");
                return null;
            }

            return new AtomicEffectInstance
            {
                Type = type,
                Value = entry.value,
                RandomAmplitude = Math.Clamp(entry.amp, 0f, 1f),
                StringValue = entry.str ?? "",
                RowHashId = entry.refId, // 来源行身份（2026-10-03：同枚举多行各自锚价——计价按行取锚）
                Mana = null, // ManaList 已随彻底引用化删除（全数据 0 使用）
                TargetKinds = kinds,
                Filter = config.TargetFilter ?? "",
                Polarity = polarity,
            };
        }

        /// <summary>构筑期显示用：代价栏 Payload 条目 → 原子实例（允许错边——契约校验在代价转换处；
        /// 供 CardCostService 计算"获得白16"显示行）。</summary>
        public static AtomicEffectInstance ConvertPayloadForDisplay(AtomicEffectEntry entry)
            => entry == null ? null : ConvertAtomicEffect(entry, allowWrongSide: true);

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
                    TideLog.Warn("[CardEffectConverter] 抉择步骤 choices 缺失或 <2，跳过该步骤");
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
                                TideLog.Warn("[CardEffectConverter] 抉择不可嵌套，跳过内层抉择");
                                continue;
                            }
                            seq.Add(rs);
                        }
                    }
                    choice.Choices.Add(seq);
                }
                if (choice.Choices.Count < 2)
                {
                    TideLog.Warn("[CardEffectConverter] 抉择步骤有效模式 <2，跳过该步骤");
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
