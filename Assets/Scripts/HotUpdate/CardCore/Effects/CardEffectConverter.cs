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

            // 回合开始（2026-11-14 档位制）：OnTurnStart 恢复为可挂载的一回合一次时点，
            // 与「准备阶段」并列（先后不同：回合开始=系统处理前的起点，准备阶段=系统处理后）——
            // 旧 2026-10-20「归一为 OnStandbyPhase」的兜底删除。

            // 确定 ActivationType：未指定(0) 时根据 TriggerTiming 取默认值
            EffectActivationType activationType = data.ActivationType > 0
                ? (EffectActivationType)data.ActivationType
                : TriggerTimingDefaults.GetDefaultActivationType(timing);

            // 主动⇔Activate_* 时机双向钉死（2026-10-02 定案；2026-10-16 称谓统一：效果只分**主动/自动**
            // 两种——旧「启动式/激活式」称谓全部统一为「主动」，其承载时机=Activate_* 三档）：
            // 主动=玩家轮询发动（横置+现付锚价，时机恒 Activate_* 主阶段/瞬间/响应）、
            // 自动=事件触发（条件达成弹窗询问）；强制=系统内部无条件触发（不开放、不动）。
            // 主动声明带真实触发时机（合成器旧 bug 只写 ActivationType 不写 Activate 时机/手写 JSON）
            // → 告警并钉时机 Activate_Active（主阶段主动档；反向"Activate_* 非主动"由下方对称校验收口）。
            //（2026-10-16 解绑曾对倒计时载荷豁免——主动+真实时机保留；2026-10-20 倒计时固定触发定案
            //  收回：倒计时声明值一律被下方引擎归一覆写为 自动+回合开始。）
            if (activationType == EffectActivationType.Voluntary
                && timing != TriggerTiming.Activate_Active
                && timing != TriggerTiming.Activate_Instant
                && timing != TriggerTiming.Activate_Response)
            {
                TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 发动方式=主动但时机={timing}" +
                                 "（主动效果不设真实触发时机——2026-10-02 定案；2026-10-16 称谓统一），时机已钉 Activate_Active");
                timing = TriggerTiming.Activate_Active;
            }

            // 主动（Activate_*，2026-09-08 定案）：构筑期不占卡费（L2 只占挂载口），
            // 元素锚价运行时现付——发动 = 横置 + 扣锚价元素 + 选定目标。
            // 非主动（触发式）照旧：元素费已随卡牌档位费收讫，执行器跳过防双计。
            bool isActivated = timing == TriggerTiming.Activate_Active
                            || timing == TriggerTiming.Activate_Instant
                            || timing == TriggerTiming.Activate_Response;

            // 时机/发动方式对称校验（2026-09-08）：非主动必须设具体发动时机
            // （登场/死亡/离场/受攻击等——越界 int 已在上面回退 OnPlay 并告警）；
            // 主动只能是主动发动（玩家轮询选择），声明为强制/自动属数据错误——告警并按主动处理。
            if (isActivated && activationType != EffectActivationType.Voluntary)
            {
                TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 为主动（{timing}）" +
                                 $"但 ActivationType={activationType}（主动只能主动发动），按主动处理");
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
                // 哨兵 -1=未声明回退 Once（绝对计价锚）；ForTurns 回合数 N 随行（2026-10-06 复挂）
                Duration = data.Duration >= 0 ? (DurationType)data.Duration : DurationType.Once,
                DurationValue = data.DurationValue,
                SummonDropZone = (Zone)data.SummonDropZone,
                SelectionMode = data.SelectionMode >= 0 ? (SelectionMode)data.SelectionMode : SelectionMode.None,
                RandomTarget = data.RandomTarget != 0,
                SourceCardId = sourceCardId,
                ElementCostPrepaid = !isActivated,
            };

            // 锚价钉死（2026-10-20）：手作行 cost 列为权威锚价——null 列=0 费钉死
            if (data.AnchorCostPinned != 0)
            {
                def.AnchorCostPinned = true;
                def.AnchorCostPin = data.AnchorCost != null ? data.AnchorCost.Clone() : new ElementCost();
            }

            // 关键词型原子不得作主动（2026-10-02 定案；2026-11-07 关键词特判后对现行关键词恒不触发）：
            // 旧自指 Grant 原子（=给自己加关键词——横置换静态身份无意义）仍告警剔除；现行关键词行
            // 域=己方战场:生物（赋予通道）不命中——本闸保留给旧档自指行/未来例外。
            // 判定委托 ComposerCatalog.IsKeywordStyleGrant（与合成器 UI 同源）。
            if (isActivated)
            {
                int removed = RemoveKeywordStyleGrants(data);
                if (removed > 0)
                    TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 为主动：" +
                                 $"已剔除 {removed} 个关键词型 Grant 原子（关键词不得作为主动效果；赋予型不受限）");
            }

            // 主序列通道裁决（2026-10-05 双通道翻倍修复）：Steps 非空=节点化主序列为唯一来源——
            // AtomicEffects 只是它的扁平投影（合成器 BuildCardEffect 同源双写），两通道并载时
            // 下方折叠会把主干原子再 Add 一遍 → 计价/执行双倍。Steps 空（纯扁平旧卡）才走 AtomicEffects。
            bool stepsOwnMainSequence = data.Steps != null && data.Steps.Count > 0;

            // 转换原子效果列表（两槽定案 2026-10-05：主序列=平铺主干原子，各可带槽级 Branch 载荷）
            if (!stepsOwnMainSequence && data.AtomicEffects != null)
            {
                foreach (var entry in data.AtomicEffects)
                {
                    var instance = ConvertAtomicEffect(entry);
                    if (instance != null)
                        def.Effects.Add(instance);
                }
            }

            // 转换节点化步骤（遗留路径）。内嵌抉择（kind==2）已随 2026-10-31 P12 抉择默认化退役：
            // 法术模式=非充能主动效果数（GetCastableEffects），遗留 choices 数据→告警跳过
            //（不再生成 RuntimeStepKind.Choice）；纯原子/分支步骤 → 平铺折叠进 def.Effects
            //（kind=1 门折入前一个原子的 Branch 载荷——产出条件族归 Outcome、局面门归 Gate；
            // 光环形态的规则光环原子步同样平铺执行）。
            if (stepsOwnMainSequence)
            {
                if (data.Steps.Any(s => s != null && s.kind == 2))
                    TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 含内嵌抉择分支（kind==2）" +
                                 "——2026-10-31 P12 退役：抉择=法术多主动效果默认择一，内嵌分支已忽略");
                // 词条步展开（G5 2026-11-08）：合成器编辑图携带 @词条 引用步——转换（计价/校验消费口）
                // 前先物化展开，创作面同守「展开先于计价」；不改入参（保存写回 @ 引用步不变形）。
                // 运行装载路径（EffectsLibrary.Resolve→ToCardEffect）已在更早处展开，此口对它为快路直通。
                FoldBranchSteps(CompositeExpander.ExpandEditingSteps(
                    data.Steps, $"convert:{sourceCardId}·{data.Id}"), def, sourceCardId);
            }

            // 倒计时载荷换算（Steps/扁平两通道统一，2026-10-22 修复）：初值=Then 推导费 1费=1回合
            //（声明 0/缺省时）；此前只覆盖扁平通道，Steps 形态的倒计时 CountdownTurns 恒 0 → 入场不挂计数。
            foreach (var atom in def.Effects)
            {
                if (atom?.Branch?.EngineKind == BranchEngineKind.Countdown && atom.Branch.CountdownTurns <= 0)
                    atom.Branch.CountdownTurns = CostDerivationService.CountdownTurnsOf(
                        atom.Branch.EngineParam, atom.Branch.Then, atom.Branch.EffectId,
                        def.TriggerTiming); // 公式与 UI 短文案同源（单原子费×宿主时机系数）
            }

            // ---- 组合层域预计算（2026-09-10 目标域模型）----
            // 效果级作用范围（2026-10-04 相同目标定案）：header 声明优先，未声明回落原子域交集；
            // 组合 filter = 成员带域原子 Filter token 之 AND；TargetCount 哨兵 -2 回落表级。
            // （引擎头字段通道已随两槽定案退役——引擎条件挂槽级原子 Branch 载荷，倒计时换算见上方。）
            PrecomputeDomains(def, data.TargetKinds);

            // 胜利宣判闸（2026-09-16 定案）：含 DeclareVictory 原子的效果一律强制——纯强制批
            // 合成双 Pass 直接结算（宣判即终局，见 StackEngine.FinishResolution 拆轮）。
            // 主动（玩家主动发动）与此语义互斥：主动声明 DeclareVictory 属数据错误，
            // 告警跳闸（上方守卫已按主动处理——玩家自发宣胜利的卡不该存在）。
            if (ContainsAtom(def, AtomicEffectType.DeclareVictory))
            {
                if (isActivated)
                    TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {def.Id} 主动声明 DeclareVictory" +
                                     "（数据错误——胜利宣判不可主动发动），按主动处理保留");
                else if (activationType != EffectActivationType.Mandatory)
                {
                    TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {def.Id} 含 DeclareVictory 原子，" +
                                     $"ActivationType={activationType} 覆写为 Mandatory（胜利宣判一律强制）");
                    activationType = EffectActivationType.Mandatory;
                    def.ActivationType = activationType;
                }
            }

            // 历史字段解析（2026-10-30 起无运行时/计价消费方）：未声明(0)=1、显式值原样透传——
            // 只为 EffectSlim 往返/内容哈希稳定保留（效果 ID 含本列），不再产生任何闸门。
            def.TriggerLimitPerTurn = data.TriggerLimitPerTurn == 0 ? 1 : data.TriggerLimitPerTurn;

            def.TargetCount = data.TargetCount != -2 ? data.TargetCount : FallbackTargetCount(def);

            // SelectionMode 推导（2026-10-05 通用属性七项定案）：效果级作用范围已声明时，
            // 选择模式不再独立生效——由 作用范围域宽 × 目标数 推导（随机目标=正交标志不入推导）：
            // 域单值 → 单范围档（0/1/2）；域多值 → 多范围档（3/4/5）；数量 1=选一、N>1/任意(-1)=选多、0=全取。
            // 未声明作用范围（存量数据）沿用存储值——两代数据同链共存；
            // 逐原子目标制（2026-10-09）：未声明=逐原子解析，存储值仅作该模式档位提示
            //（全取档→各原子全取；None→按选一），实际目标由各原子自身域解析。
            if (data.TargetKinds != null && data.TargetKinds.Count > 0)
            {
                bool union = def.TargetDomain != null && def.TargetDomain.Count > 1;
                if (def.TargetCount == 0)
                    def.SelectionMode = union ? SelectionMode.WholeUnion : SelectionMode.Whole;
                else if (def.TargetCount == 1)
                    def.SelectionMode = union ? SelectionMode.SingleUnion : SelectionMode.Single;
                else
                    def.SelectionMode = union ? SelectionMode.MultipleUnion : SelectionMode.Multiple;
            }

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
                    if (costEntry == null) continue;
                    // 效果型代价（2026-09-11；2026-10-31 代价指向化：效果级行=指向本效果，主动/自动同构）：
                    // 付费步强制执行的原子序列（执行在 CostCompensationService.PayPayloadsAsync）。
                    // 内容契约（2026-10-28 P1b 定案）：代价收 **p≠0 且锁在受益方反面** 的原子——
                    // p<0（有害）锁己方（自伤）、p>0（有益）锁对方（资敌）；p=0（中性）一律剔除。
                    // 合成器放置口经 CostDerivationService.PayloadCostDomain 收窄/镜像（双侧域→锁定侧），
                    // 装载期按条目有效域校验（存储 kinds 已收窄——表行默认域为双侧）。
                    // 空 payload（refId 为空——非 Payload 代价的占位/迁移残留）不作 Payload 处理
                    var payloadList = costEntry.AllPayloads();
                    if (payloadList.Count > 0)
                    {
                        var payloadAtoms = new List<AtomicEffectInstance>();
                        foreach (var pe in payloadList)
                        {
                            // 逐原子契约过滤（ConvertPayloadForDisplay 单源——违约原子剔除不炸行）
                            var payloadAtom = ConvertPayloadForDisplay(pe,
                                $"卡 {sourceCardId} 效果 {data?.Id}");
                            if (payloadAtom == null)
                            {
                                TideLog.Error($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data?.Id} 代价栏 Payload" +
                                               $"[{pe?.refId}] 违反内容契约：代价只能挂 p≠0 且锁在受益方反面的原子" +
                                               "（p<0 锁己方 / p>0 锁对方，2026-10-28 P1b；极性0/无目标不可入代价栏——2026-11-02），已剔除该原子");
                                continue;
                            }
                            payloadAtoms.Add(payloadAtom);
                        }
                        if (payloadAtoms.Count > 0)
                        {
                            def.Costs.Add(new CostInstance
                            {
                                Type = CostType.Payload, // 带 payload 原子的条目恒为效果型代价（声明值不参与）
                                Value = costEntry.Value,
                                ManaType = (ManaType)costEntry.ManaType,
                                Payloads = payloadAtoms, // 行内原子序列（legacy 单原子经 AllPayloads 折入同口径）
                            });
                        }
                        continue; // 有 payload 的条目：全原子违约=空行（不落元素代价透传）
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

            // 引擎效果头兜底归一（2026-10-09 自由分支九项定案）：效果含引擎载荷（引擎=单原子形态）→
            // 发动方式/时机/速度/生效次数按 ComposerCatalog.EngineHeaderPresetOf 归一——合成器已锁定，
            // 此处收口旧数据/手写数据（引擎运行时本就无视效果头，自订阅事件；归一保证显示/计价/数据三面一致）。
            // 2026-11-08 规则地基定案：双选组随 EngineActivationFree 整删——发动方式按
            // EngineActivationPinOf 单值钉（主动行=主动、其余引擎行=自动）；
            // 2026-11-08 阶段3：固定三（钉死组）声明值一律预设归一；自动三（自然缺省组）一律自动
            // +时机=奖励效果时机（解析失败回落自然缺省并告警）——「声明主动运势」旧路径退役。
            var engineKind = EngineKindOfDefinition(def);
            if (ComposerCatalog.EngineHeaderPresetOf(engineKind) is { } ehp)
            {
                if (ehp.FreeTiming)
                {
                    // 自然缺省组（运势/拼枪/团结·2026-11-10 R4 双通道修订）：一律自动；时机=效果头
                    // **声明值**（2026-11-14 档位制起 OnTurnStart 不再归一，按声明值走）——外层时机=
                    // 引擎评估窗口由玩家在合成器选（BranchEngines.RewardTimingOf 运行时同源读此值）。
                    // 不再随奖励效果时机反钉（2026-11-08 阶段3 旧口径）：R4 允许主动效果作奖励——
                    // 引用时机反钉会令事件类型永不匹配（主动档 Activate_* 无事件锚=引擎永不评估）。
                    // 主动三档声明值收口（R4 配套）：评估须有事件锚——Activate_* 无事件=死档，
                    // 声明值回落自然缺省（同旧 RewardTimingOfEngine 解析失败路径）并告警可见
                    //（合成器时机下拉不含主动档，此口收直改 JSON/旧夹具数据）。
                    def.ActivationType = EffectActivationType.Automatic;
                    if (def.TriggerTiming == TriggerTiming.Activate_Active
                        || def.TriggerTiming == TriggerTiming.Activate_Instant
                        || def.TriggerTiming == TriggerTiming.Activate_Response)
                    {
                        TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {data.Id} 自动引擎"
                                     + $"（{ComposerCatalog.EngineZhOf(engineKind)}）外层时机={def.TriggerTiming}"
                                     + "（主动档无事件锚=引擎永不评估）——已回落自然缺省 " + ehp.Timing);
                        def.TriggerTiming = ehp.Timing;
                    }
                }
                else
                {
                    // 钉死组：倒计时/死亡计数/元素充盈=自动+固定时机；附加诅咒/祝福=主动+主动钉档
                    //（2026-11-08 起声明值不再放行——旧双选声明一律预设归一）。
                    def.ActivationType = ehp.Activation;
                    def.TriggerTiming = ehp.Activation == EffectActivationType.Voluntary
                        ? TriggerTiming.Activate_Active : ehp.Timing;
                }
                def.BaseSpeed = ehp.Speed;
                // 历史字段归一（2026-10-30 起无消费方）：引擎预设兜底仍保留存档值（哈希稳定）。
                // 自由分支 2026-11-13 起无每回合上限，本列不再读。
                def.TriggerLimitPerTurn = data.TriggerLimitPerTurn > 0 ? data.TriggerLimitPerTurn : ehp.Limit;
                def.ElementCostPrepaid = !(def.TriggerTiming == TriggerTiming.Activate_Active
                                           || def.TriggerTiming == TriggerTiming.Activate_Instant
                                           || def.TriggerTiming == TriggerTiming.Activate_Response);
            }

            //（充能形态契约段已随 2026-11-05 充能系统动作化改版删除：充能不再是卡上效果，
            //  原子表行已删——不会再有 Charge 原子进转换链。）

            return def;
        }

        /// <summary>效果内首个引擎载荷种类（扁平 Effects 与节点化 Steps 原子双扫；无=None）。
        /// 触发管线排除口（2026-10-20 定案）：RegisterEffect 用它识别引擎主干——不入触发队列/栈。</summary>
        internal static BranchEngineKind EngineKindOfDefinition(EffectDefinition def)
        {
            // 双判口径（2026-11-08 对齐 EffectExecutionEngine.IsEngineTrunk）：Branch.Settle==Engine
            // ∨ 原子行类型即引擎（EngineCountdown 族）——Engine 载荷经 ConvertBranchPayload 折叠
            //（Then 空∧无 EffectId）后 Branch=null，行类型是引擎主干身份的最后事实源；
            // 消费口（奖励/载荷/光环资格、效果头归一、触发管线排除）与运行时主序列同语义。
            BranchEngineKind KindOf(AtomicEffectInstance atom)
            {
                var b = atom?.Branch;
                if (b != null && b.Settle == BranchSettleKind.Engine) return b.EngineKind;
                return atom != null ? ComposerCatalog.EngineKindOf(atom.Type) : BranchEngineKind.None;
            }
            if (def.Effects != null)
                foreach (var atom in def.Effects)
                    if (KindOf(atom) != BranchEngineKind.None) return KindOf(atom);
            if (def.Steps != null)
                foreach (var step in def.Steps)
                {
                    var k = KindOf(step?.Atomic);
                    if (k != BranchEngineKind.None) return k;
                }
            return BranchEngineKind.None;
        }

        //（自动三奖励效果时机 RewardTimingOfEngine 已随 2026-11-10 R4 删除退役：自动三时机=效果头
        //  声明值（玩家在合成器选引擎评估窗口），不再随奖励效果时机反钉——主动效果作奖励时
        //  引用时机反钉会令事件类型永不匹配（Activate_* 无事件锚=引擎永不评估）。）

        // ======================================== 关键词型原子主动校验（2026-10-02 定案） ========================================

        /// <summary>剔除主动效果内的关键词型 Grant 条目：AtomicEffects（含引擎奖励落点）+ Steps 全树
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

        /// <summary>条目是否关键词型 Grant（2026-11-07 关键词特判后按**表行域**判）：Grant 族原子，
        /// 且该表行目标域恰为 {Self}。2026-11-07 起关键词行改「己方战场:生物」（效果层=赋予通道），
        /// 不再有自指行——本判定对现行关键词恒 false，旧自指行（若有）仍按关键词型处理。
        /// 实例 kinds [0] 不再视为自指（NarrowByKinds 对纯 Self kinds 不收窄，按行域走）。</summary>
        private static bool IsKeywordStyleEntry(AtomicEffectEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.refId)) return false;
            var row = Attribute.AtomicEffectTable.GetByHashId(entry.refId);
            if (row == null || string.IsNullOrEmpty(row.EnumName)) return false;
            if (!row.EnumName.StartsWith("Grant")) return false;
            var kinds = row.GetTargetKindList();
            return kinds != null && kinds.Count == 1 && kinds[0] == (int)TargetKind.Self;
        }

        /// <summary>UI 编辑出口（2026-09-14 合成器重做）：原子条目 → 运行时实例（内容契约照常生效——
        /// 错边剔除/主干守卫同装载转换）。供合成器预算校验（RewardDerivedCost）与描述预览消费。</summary>
        public static AtomicEffectInstance ConvertAtomForUI(AtomicEffectEntry entry)
            => ConvertAtomicEffect(entry);

        /// <summary>胜利宣判闸扫描：主序列原子 / 载荷 Then 奖励 / 步骤全形态（原子+抉择全模式）中
        /// 是否含指定类型（SubEffects 递归）。</summary>
        private static bool ContainsAtom(EffectDefinition def, AtomicEffectType type)
        {
            if (AtomsContain(def.Effects, type)) return true;
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
            if (AtomsContain(atom.Branch?.Then, type)) return true;
            return AtomsContain(atom.SubEffects, type);
        }

        /// <summary>动态值表达式白名单（步6 §4.1 v1）：仅 ref.power|ref.life|ref.cost——
        /// 不接运算符；白名单外告警置空（回落旧路径 GetRolledValue，效果可结算不炸）。</summary>
        private static bool IsValidValueExpr(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return true;
            if (expr == "ref.power" || expr == "ref.life" || expr == "ref.cost") return true;
            TideLog.Warn($"[CardEffectConverter] 动态值表达式 '{expr}' 超白名单（v1 仅 ref.power|ref.life|ref.cost）——置空回落名义值路径");
            return false;
        }

        /// <summary>引用型唯一转换口（2026-09-14 彻底引用化）：refId → 表行 → 运行时实例。
        /// 枚举/默认域/Filter/极性/MountKinds 全部从行解析；增量只读 value/str/amp/kinds/branch。
        /// branch 载荷递归转换（Then 奖励同引用化口径；引擎倒计时初值换算在 ConvertOne 收口）。</summary>
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

            // 目标域（**B2 2026-11-05 硬切：Domain 新语法唯一入口，旧 EffectDomain 词表已删**）：
            // kinds = entry.kinds（实例显式收窄）?? 行 Domain 派生视图（GetTargetKindList——SideLock/
            // 隐藏区等区级消费口用）；候选一律走 axis（下方产轴）。filter = 行 TargetFilter（属性 token）。
            // entry.domain 旧词表 token（历史授予域数据）不再解析——数据保留仅为卡 ID 不换血（重导另任务）。
            List<int> kinds;
            string filter = config.TargetFilter ?? "";
            // 场地光环行（2026-11-06 加 Domain 列）：作用方向走 RuleAuraScope（entry.value→投放），
            // 不参与目标解析——kinds 恒空（保持「空 Domain=无目标」的既有运行时行为，
            // 防光环原子凭行域生成候选弹目标窗）。
            bool auraRow = RuleAuraComponents.IsAuraLike(type);
            // 参照域行（步4 §2.4）：Domain=受体域、RefDomain=参照域各自独立产轴——entry.kinds 不再
            // 作为目标/参照域收窄源（仍照旧填 TargetKinds 供 SideLock/隐藏区等区级消费口）。
            bool refRow = !auraRow && !string.IsNullOrEmpty(config.RefDomain);
            kinds = auraRow ? new List<int>()
                : entry.kinds != null && entry.kinds.Count > 0
                ? new List<int>(entry.kinds)
                : config.GetTargetKindList();

            float polarity = Math.Clamp(config.Polarity, -1f, 1f);

            // 目标域产轴（**B2：行/实例 Domain 新语法唯一域源**；空 Domain = 无目标）：
            // ① entry.domain 为新语法（实例级覆盖）优先；② 行 Domain → 产轴；
            // entry.kinds 只做区域收窄（NarrowByKinds：Both 落单侧改写、无交删 selector、
            // 角色 selector 只由行 Domain 提供）。FilterConflicts → 告警不产轴（防御口）。
            // 例外：场地光环行（2026-11-06 加 Domain 列）不产轴——「只有场地可同时作用于双方」
            // 的产轴豁免落点：行 Domain 仅声明/展示（缺省双方=表价），方向由 RuleAuraScope 承载。
            TargetAxis axis = null;
            string domainDecl = null;
            if (!auraRow && TargetAxisRules.TryParseDomain(entry.domain, out _)) domainDecl = entry.domain;
            else if (!auraRow && TargetAxisRules.TryParseDomain(config.Domain, out _)) domainDecl = config.Domain;
            if (domainDecl != null)
            {
                TargetAxisRules.TryParseDomain(domainDecl, out var parsedAxis); // 已验证必成功
                if (TargetAxisRules.FilterConflicts(filter))
                {
                    TideLog.Warn($"[CardEffectConverter] 原子 {type}（行 {entry.refId}）新语法 Domain='{domainDecl}'"
                                 + $" 与 TargetFilter='{filter}' 含类别性 token 冲突（契约规则 5）"
                                 + "——不产轴（表数据错误，B1 后应无）");
                }
                else
                {
                    parsedAxis.Filter = filter;
                    // 参照域行（步4）：受体轴不受 entry.kinds 收窄（§2.4 忽略 kinds 作为域源）；
                    // 非参照行照旧 NarrowByKinds（实例收窄/代价锁侧）。
                    var narrowed = TargetAxisRules.NarrowByKinds(parsedAxis, refRow ? null : entry.kinds);
                    // 极性收窄（2026-11-06 用户定案：非场地效果一律按极性定作用侧——行域「双方」
                    // 只是双极性并集声明，p<0→对方侧/p>0→己方侧；只有场地（仪典/场地光环，不经
                    // 产轴路径）可同时作用于双方）。kinds 未显式声明（含参照域行——其 kinds 不承载
                    // 受体域语义）的条目收窄；实例 kinds（手动收窄/代价锁侧）优先，不经此口。
                    if (refRow || entry.kinds == null || entry.kinds.Count == 0)
                        narrowed = TargetAxisRules.NarrowByPolarity(narrowed, polarity);
                    // 步4（2026-11-08 目标选择重构）：删「收窄后空 selector 挂 null 回落 kinds 选参照」
                    // 特例——参照域已由 RefDomain 独立承载（ReferenceAxis），Domain 恒为受体轴；
                    // 非参照行 kinds ⊄ Domain 并集属数据错误，保留空轴（无候选，错误可见不静默回落）。
                    axis = narrowed;
                }
            }
            else if (!auraRow && !string.IsNullOrEmpty(config.Domain))
                TideLog.Warn($"[CardEffectConverter] 原子 {type}（行 {entry.refId}）行 Domain='{config.Domain}'"
                             + " 非合法新语法（旧词表残留？）——无轴，该原子无候选");

            // 内容契约（2026-09-11 定案；2026-10-03 用户定案收缩）：效果栏错边剔除只保留
            // 「有益原子锁对方域」（对对手有益）——仍只能进代价栏（Payload）；
            // 「有害原子锁己方域」（负面效果指向自己——自缚式设计）在效果栏放行，
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

            // 数量装载守卫（2026-11-07 定案：按**作用域**，不按原子族）：
            // ① 行标「不可全体」（NoWhole——操控/牺牲/宣言/反制/变形/跳回合等）禁 count=0。
            // ② count=0（全域）仅允许**战场/手牌**（AllowsAllTarget）——这两处目标数量可预测（3~5），
            //    1/2/3/全部合理；牌库/墓地/除外区/元素池取全域严重超模（如磨牌全磨对手牌库）→ 收 1。
            // ③ count=2/3 任意非 Self 域允许；Self 域收 1。
            int effectiveCount = entry.count;
            var mounts = MountKindExtensions.ParseCsv(config.MountKinds);
            if (effectiveCount == 0 && mounts.Contains(MountKind.NoWhole))
            {
                TideLog.Warn($"[CardEffectConverter] 原子 {type}（行 {entry.refId}）行标「不可全体」（NoWhole）" +
                             "——count=0（域内全取）已收为 1（全取严重违背定价/规则）");
                effectiveCount = 1;
            }
            bool allowsAll = !mounts.Contains(MountKind.NoWhole)
                             && TargetAxisRules.AllowsAllTarget(axis);
            if (effectiveCount == 0 && !allowsAll)
            {
                TideLog.Warn($"[CardEffectConverter] 原子 {type}（行 {entry.refId}）count=0（全域）但域 '"
                             + (axis?.Zh ?? "空") + "' 非「战场/手牌」（2026-11-07 定案：全域仅战场/手牌，"
                             + "牌库/墓地/除外区/元素池/角色/Self 不收）——已收为 1");
                effectiveCount = 1;
            }
            bool singleOnly = axis == null
                              || (axis.Selectors.Count > 0
                                  && axis.Selectors.All(s => s != null
                                      && (s.Region == TargetRegion.Self
                                          || TargetRegionRules.IsCharacter(s.Region))));
            if ((effectiveCount == 2 || effectiveCount == 3) && singleOnly)
            {
                TideLog.Warn($"[CardEffectConverter] 原子 {type}（行 {entry.refId}）域仅角色/Self"
                             + "——目标数量已收为 1");
                effectiveCount = 1;
            }

            // 参照域三列落库（2026-11-08 目标选择重构步2 §2.1：**只读填充**——目标/参照来源尚未切换，
            // 旧 kinds 路径照旧；ReferenceAxis != null 即「分段原子」标记，步3 引擎据此分流 Refs/Targets）。
            // 参照轴可见性过滤之外可挂 RefFilter 属性条件（护盾猛击「具有护甲的」——2026-11-08 参照化）。
            TargetAxis refAxis = null;
            var refPick = Attribute.RefPick.Cast;
            bool refRequired = true;
            if (!auraRow && !string.IsNullOrEmpty(config.RefDomain))
            {
                if (TargetAxisRules.TryParseDomain(config.RefDomain, out var refParsed))
                {
                    refAxis = refParsed;
                    // 参照侧属性过滤（2026-11-08 护盾猛击参照化）：RefDomain 表结构、RefFilter 表状态
                    //（「具有护甲的」），挂参照轴随候选解析生效（TargetAxis 管线 :626 属性过滤段）。
                    refAxis.Filter = config.RefFilter;
                }
                else
                {
                    TideLog.Warn($"[CardEffectConverter] 原子 {type}（行 {entry.refId}）RefDomain='"
                                 + config.RefDomain + "' 非法——参照轴不挂（参照回落旧 kinds 路径）");
                }
                if (!string.IsNullOrEmpty(config.RefPick)
                    && Enum.TryParse(config.RefPick, out Attribute.RefPick parsedPick))
                    refPick = parsedPick;
                refRequired = config.RefRequired;
            }

            return new AtomicEffectInstance
            {
                Type = type,
                // 光环·极性 0 行存量旧档值（三选/钉侧时代）矫正为双方=表价档（与 handler/UI 同口径）
                Value = auraRow && config.Polarity > -0.5f && config.Polarity < 0.5f
                    ? (int)RuleAuraScope.Both
                    : entry.value,
                RandomAmplitude = Math.Clamp(entry.amp, 0f, 1f),
                ValueMax = Math.Max(0, entry.valueMax),   // 数值上界（步6）：只告警不拦（装载不读表价）
                ValueExpr = IsValidValueExpr(entry.valueExpr) ? entry.valueExpr : null, // 白名单外置空+告警
                StringValue = entry.str ?? "",
                RowHashId = entry.refId, // 来源行身份（2026-10-03：同枚举多行各自锚价——计价按行取锚）
                Mana = null, // ManaList 已随彻底引用化删除（全数据 0 使用）
                TargetKinds = kinds,
                TargetCount = effectiveCount, // 每原子数量覆盖（-2=未声明回落效果级；NoWhole 行 0 收 1）
                RandomTarget = entry.rand,   // 每原子随机覆盖（-1=未声明回落效果级）
                DeferSelect = entry.defer,   // 每原子目标选择方式（0=预选=默认/1=效果处理时选）
                Filter = filter,             // 域词表双读后的合并 filter（无域声明=表行 TargetFilter ?? ""，与旧口径一致）
                Axis = axis,                 // 两轴（P1-b）：行声明 Region 且实例无显式域时挂载；null=回落旧口径
                ReferenceAxis = refAxis,     // 参照轴（步2 只读落库）：行 RefDomain 产轴；null=非分段原子
                ReferenceKinds = refAxis != null ? config.GetReferenceKindList() : null,
                RefPick = refPick,
                RefRequired = refRequired,
                Polarity = polarity,
                Branch = ConvertBranchPayload(entry.branch, entry.str),
            };
        }

        /// <summary>槽级分支载荷转换（两槽定案）：条目 → BranchPayload；Then 奖励递归引用化。
        /// settle 越界/条件空 → 告警丢弃（防幽灵载荷空转）。
        /// 附加诅咒/祝福载荷效果化（2026-11-06）：引擎主干原子 str=效果库条目 id → payload.EffectId。
        /// 分支奖励效果化（2026-11-07）：BranchEntryData.effectId=奖励组合效果引用，三 settle 通用
        /// ——Then 原子列与 EffectId 二通道并存（先效果后原子），任一非空即不折叠。</summary>
        private static BranchPayload ConvertBranchPayload(BranchEntryData data, string atomStr = null)
        {
            if (data == null) return null;
            var settle = (BranchSettleKind)data.settle;
            if (!Enum.IsDefined(typeof(BranchSettleKind), settle))
            {
                TideLog.Warn($"[CardEffectConverter] 分支载荷 settle={data.settle} 越界，已丢弃");
                return null;
            }

            var payload = new BranchPayload { Settle = settle };
            if (!string.IsNullOrEmpty(data.effectId)) payload.EffectId = data.effectId; // 奖励效果引用（2026-11-07）：三 settle 通用直落
            switch (settle)
            {
                case BranchSettleKind.Gate:
                    if (string.IsNullOrEmpty(data.gateId))
                    {
                        TideLog.Warn("[CardEffectConverter] 有限分支载荷缺局面条件 id（gateId），已丢弃");
                        return null;
                    }
                    payload.GateId = data.gateId;
                    break;
                case BranchSettleKind.Outcome:
                    if (string.IsNullOrEmpty(data.outcomeId))
                    {
                        TideLog.Warn("[CardEffectConverter] 自由分支·产出条件载荷缺条件 id（outcomeId），已丢弃");
                        return null;
                    }
                    payload.OutcomeId = data.outcomeId;
                    payload.ConditionParam = data.condParam;
                    payload.ConditionStringParam = data.condStr ?? "";
                    break;
                case BranchSettleKind.Engine:
                    var engine = (BranchEngineKind)data.engine;
                    if (!Enum.IsDefined(typeof(BranchEngineKind), engine) || engine == BranchEngineKind.None)
                    {
                        TideLog.Warn($"[CardEffectConverter] 自由分支·引擎载荷 engine={data.engine} 越界，已丢弃");
                        return null;
                    }
                    payload.EngineKind = engine;
                    payload.EngineParam = data.engineParam;
                    payload.CountdownTurns = data.engineParam; // 声明初值；0=ConvertOne 按 Then 推导换算
                    payload.ThenActivation = data.tactivate;  // 奖励发动方式（2026-10-20：-1=未声明随主干）
                    // 载荷效果引用（2026-11-06 附加诅咒/祝福载荷效果化）：str=效果库条目 id
                    //（2026-11-07 分支奖励效果化：直落通道 effectId 优先，str 仅附加诅咒/祝福旧手写兼容）
                    if (string.IsNullOrEmpty(payload.EffectId)
                        && (engine == BranchEngineKind.CurseOnDraw || engine == BranchEngineKind.BlessingOnDraw)
                        && !string.IsNullOrEmpty(atomStr))
                        payload.EffectId = atomStr;
                    break;
            }

            if (data.then != null)
            {
                foreach (var reward in data.then)
                {
                    var inst = ConvertAtomicEffect(reward);
                    if (inst != null) payload.Then.Add(inst);
                }
            }
            // 2026-11-10 死亡计数改版：DeathToll 是效果原子上的 value 增幅标记，
            // 不再需要 Then/effectId——空载荷也要保留（否则增幅标记被折叠掉）。
            if (payload.Settle == BranchSettleKind.Engine
                && payload.EngineKind == BranchEngineKind.DeathToll)
                return payload;
            // 无载荷的条件空转——折叠为无分支（EffectId 引用通道同计：2026-11-06 附加诅咒/祝福
            // 载荷=组合效果引用，Then 空但有引用不折叠）
            return payload.Then.Count > 0 || !string.IsNullOrEmpty(payload.EffectId) ? payload : null;
        }

        /// <summary>代价栏 Payload 条目 → 原子实例（2026-10-28 P1b 契约同步：p≠0 且锁在受益方反面——
        /// p<0 锁己方 / p>0 锁对方；p=0 与锁错侧条目 Error 剔除返回 null，与 ConvertOne 代价转换同口径；
        /// 表行缺失同 null）。
        /// 消费面：CardCostService.SumPayloadPrice（代价抵扣）/ GameActions.CollectCardSpecialCosts（付费步）。</summary>
        public static AtomicEffectInstance ConvertPayloadForDisplay(AtomicEffectEntry entry, string contextLabel = null)
        {
            var atom = entry == null ? null : ConvertAtomicEffect(entry, allowWrongSide: true);
            if (atom == null) return null;
            // A2 定案（2026-11-05）：代价不支持全域——count=0（域内全取）只允许单目标多选（1/2/3）。
            if (entry.count == 0)
            {
                TideLog.Error($"[CardEffectConverter] 代价栏 Payload 契约拒绝：原子[{entry?.refId}] count=0（全域）"
                              + "——代价只允许单目标多选（A2 定案 2026-11-05），已剔除");
                return null;
            }
            int requiredSide = CostDerivationService.PayloadCostRequiredSideLock(atom.Polarity);
            if (requiredSide != 0 && CostDerivationService.SideLock(atom.TargetKinds) == requiredSide) return atom;
            // 2026-11-02 代价栏翻转限制：报错须可定位（contextLabel=「卡 id 效果 id」——缺省调用面
            // CardCostService/GameActions 无法提供，退回原子行 id pe.refId）
            string where = string.IsNullOrEmpty(contextLabel)
                ? $"原子[{entry?.refId}]"
                : $"{contextLabel} 原子[{entry?.refId}]";
            string why;
            if (requiredSide == 0)
                why = "极性=0（中性原子不能进代价栏——2026-11-02 定案）";
            else if (atom.TargetKinds == null || atom.TargetKinds.Count == 0)
                why = "无目标参数（没有 {target} 的原子不能翻转成代价——2026-11-02 定案）";
            else
                why = $"锁侧不符（SideLock={CostDerivationService.SideLock(atom.TargetKinds)}≠{requiredSide}；代价只收锁在受益方反面的原子）";
            TideLog.Error($"[CardEffectConverter] 代价栏 Payload 契约拒绝：{where} {why}——已剔除");
            return null;
        }

        /// <summary>
        /// 组合域预计算：效果级作用范围声明优先（2026-10-04 相同目标定案——并列全体原子共享）；
        /// 未声明（2026-10-09 逐原子目标制）=逐原子模式——组合域改为主序列原子域**并集**
        /// （含抉择 per-mode），仅供展示/可发动预检/AI；结算期各原子按自身极性过滤域独立解析。
        /// 无目标原子（域空）不参与并集；分支奖励原子不参与（奖励目标结算期各自解析）。
        /// </summary>
        private static void PrecomputeDomains(EffectDefinition def, List<int> headerKinds)
        {
            // 逐原子判别（2026-10-09）：header 无有效声明 → 逐原子目标制；声明的旧数据保持共享单选
            def.PerAtomTargets = headerKinds == null
                                 || !headerKinds.Any(k => Enum.IsDefined(typeof(TargetKind), k));
            def.TargetDomain = DomainOfMode(def, 0, headerKinds);
            def.TargetFilter = CombinedFilterOfMode(def, 0);

            // ChoiceDomains 已随 2026-10-31 P12 抉择退役恒 null（运行时不再生成 RuntimeStepKind.Choice；
            // 遗留字段仅存档兼容，无读写消费方）
        }

        /// <summary>某模式的主序列原子（Steps 非空走步骤枚举，否则扁平 Effects）。</summary>
        private static IEnumerable<AtomicEffectInstance> MainSequence(EffectDefinition def, int modeIndex)
        {
            if (def.Steps != null && def.Steps.Count > 0)
                return EnumerateMainSequenceAtoms(def.Steps, modeIndex);
            return def.Effects;
        }

        private static List<int> DomainOfMode(EffectDefinition def, int modeIndex, List<int> headerKinds)
        {
            // 效果级作用范围（2026-10-04 相同目标定案）：header 声明优先——并列全体原子共享同一份
            // 选中目标；无效值滤除后为空视同未声明，回落原子域并集（2026-10-09 逐原子目标制——
            // 旧交集口径随断链拦截退役；并集仅供展示/预检/AI，结算期逐原子自解析）。
            if (headerKinds != null)
            {
                var declared = headerKinds
                    .Where(k => Enum.IsDefined(typeof(TargetKind), k))
                    .Distinct().OrderBy(k => k).ToList();
                if (declared.Count > 0) return declared;
            }
            List<int> domain = null;
            foreach (var atom in MainSequence(def, modeIndex))
            {
                if (atom?.TargetKinds == null || atom.TargetKinds.Count == 0) continue;
                domain = TargetKindRules.Union(domain, atom.TargetKinds);
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
        /// 分支奖励原子不扫——主序列才在声明期选目标，奖励目标由结算期各自解析；
        /// 引擎主干原子（Settle==Engine）为条件载体非效果，同样跳过（2026-10-05 回表）。
        /// </summary>
        public static IEnumerable<AtomicEffectInstance> EnumerateMainSequenceAtoms(
            List<RuntimeEffectStep> steps, int modeIndex)
        {
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.Kind == RuntimeStepKind.Atomic && step.Atomic != null)
                {
                    // 引擎主干行（行级+载荷级双判）：条件载体非效果，不进声明期目标扫描
                    if (step.Atomic.Branch?.Settle == BranchSettleKind.Engine
                        || ComposerCatalog.EngineKindOf(step.Atomic.Type) != BranchEngineKind.None)
                        continue;
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

        /// <summary>遗留步骤平铺折叠（两槽定案 2026-10-05）：kind=0 原子步 → def.Effects 主干；
        /// kind=1 门步 → 折入前一个原子的 Branch 载荷——产出条件族（DmgKillsTarget/DeclareHit 等）归
        /// 自由分支 Outcome，局面门（SituationGates）归有限分支 Gate（达标奖励/不达标无事）；
        /// 其余条件 id——告警剔除。落单门步（无前置原子）告警剔除；
        /// elseSteps 无新模型对应（条件不达成不发奖励也不惩罚）——非空告警丢弃。</summary>
        private static void FoldBranchSteps(List<EffectStepData> steps, EffectDefinition def, string sourceCardId)
        {
            AtomicEffectInstance prev = null;
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.kind == 0)
                {
                    var atom = step.atomic != null ? ConvertAtomicEffect(step.atomic) : null;
                    if (atom == null) { prev = null; continue; }
                    def.Effects.Add(atom);
                    prev = atom;
                }
                else if (step.kind == 1)
                {
                    if (prev == null)
                    {
                        TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {def.Id} 分支步骤无前置主干原子，已剔除");
                        continue;
                    }
                    if (step.elseSteps != null && step.elseSteps.Count > 0)
                        TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {def.Id} 分支步骤携带 else 奖励" +
                                     "（两槽定案：条件不达成不发奖励），else 已丢弃");
                    bool outcome = ComposerCatalog.IsOutcomeCondition(step.conditionId);
                    bool situation = ComposerCatalog.IsSituationCondition(step.conditionId);
                    if (!outcome && !situation)
                    {
                        TideLog.Warn($"[CardEffectConverter] 卡 {sourceCardId} 效果 {def.Id} 分支步骤条件 {step.conditionId}" +
                                     " 非产出/局面条件，已剔除");
                        continue;
                    }

                    var payload = new BranchPayload
                    {
                        Settle = outcome ? BranchSettleKind.Outcome : BranchSettleKind.Gate,
                        OutcomeId = outcome ? step.conditionId : null,
                        GateId = outcome ? null : step.conditionId,
                        ConditionParam = step.conditionParam,
                        ConditionStringParam = step.conditionStringParam ?? "",
                    };
                    if (step.thenSteps != null)
                    {
                        foreach (var entry in step.thenSteps)
                        {
                            var inst = ConvertAtomicEffect(entry);
                            if (inst != null) payload.Then.Add(inst);
                        }
                    }
                    // 倒计时引擎经由载荷声明（新数据不走 steps）；门载荷不涉及 CountdownTurns
                    prev.Branch = payload.Then.Count > 0 ? payload : null;
                }
            }
        }

        private static RuntimeEffectStep ConvertStep(EffectStepData step)
        {            if (step == null) return null;

            // kind: 0=原子, 1=条件分支（2=抉择已随 2026-10-31 P12 退役——遗留数据告警跳过）
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

            if (step.kind == 2)
            {
                TideLog.Warn("[CardEffectConverter] 抉择步骤（kind==2）已随 2026-10-31 P12 退役——整步跳过"
                             + "（法术多主动效果在施放时择一，内嵌分支不再支持）");
                return null;
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
