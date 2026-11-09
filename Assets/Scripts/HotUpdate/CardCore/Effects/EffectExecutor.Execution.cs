using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;

namespace CardCore
{
    public partial class EffectExecutor
    {
        /// <summary>
        /// 扁平主序列结算（两槽定案 2026-10-05 主路径）：主干原子平铺（构筑校验 ≤2）——
        /// 带 Outcome 载荷的主干按 per-target 粒度执行并逐目标评估产出条件（对齐旧步骤路径粒度，
        /// 三阶段事件 per-target 发布，不进全局收尾发布）；其余主干整列表一次执行（旧扁平口径保持）；
        /// Gate（局面门）载荷在主干原子执行完毕后评估一次——达标→奖励，不达标→无事（2026-10-09 还原定案）。
        /// </summary>
        private async UniTask ExecuteFlatAsync(EffectDefinition effect, EffectExecutionContext context,
            List<string> fragments)
        {
            if (effect.Effects == null || effect.Effects.Count == 0)
                return;

            bool HasOutcome(AtomicEffectInstance a) => a?.Branch?.Settle == BranchSettleKind.Outcome;
            // 引擎主干行（2026-10-05 回表）：条件载体，非效果——不执行、不发三阶段事件。
            // 行级判定（EngineKindOf）兜底载荷被折叠（Then 空转）的引擎原子
            bool IsEngineTrunk(AtomicEffectInstance a)
                => a != null && (a.Branch?.Settle == BranchSettleKind.Engine
                                 || ComposerCatalog.EngineKindOf(a.Type) != BranchEngineKind.None);

            // 三阶段事件：发动（全体）
            foreach (var atomicEffect in effect.Effects)
            {
                if (IsEngineTrunk(atomicEffect)) continue;
                PublishPhase(atomicEffect, AtomicEffectPhase.Activation, context);
            }

            // 三阶段事件：开始作用（首个非产出条件主干；产出条件主干 per-target 发布）
            var firstPlain = effect.Effects.FirstOrDefault(a => a != null && !HasOutcome(a) && !IsEngineTrunk(a));
            if (firstPlain != null)
                PublishPhase(firstPlain, AtomicEffectPhase.StartApplying, context);

            // 组合目标序列（快照防遍历中被改；无目标效果 = 单次 null 目标执行）。
            // originalTargets=施放/解析期原始目标——产出条件原子改写 context.Targets 后，
            // 后续普通主干按它恢复（旧扁平口径：普通原子整列表一次执行）。
            var originalTargets = context.Targets != null ? new List<Entity>(context.Targets) : new List<Entity>();
            var compositionTargets = originalTargets.Count > 0
                ? new List<Entity>(originalTargets)
                : new List<Entity> { null };
            // 声明期预选队列（2026-10-20 有序消费）：逐原子共享可消耗——交互原子按序吃队归属，
            // defer/全取/随机原子不吃（见 ResolveAtomTargetsAsync）；每轮重放重建（预选只在首轮有效）
            var preselectQueue = new List<Entity>(originalTargets);
            // spec 计划（步5 §3.2）：同域共享组只解析**一次**，组内原子共享同一目标
            //（沉默+消灭同域并列→单窗同目标）；分段参照/受体 spec 亦入计划（ApprovedSpecKeys 同源）。
            var specPlan = EffectHandlerRegistry.CollectSpecPlan(effect, context);
            var specMemo = new Dictionary<string, List<Entity>>();

            for (int ai = 0; ai < effect.Effects.Count; ai++)
            {
                var atomicEffect = effect.Effects[ai];
                if (atomicEffect == null) continue;
                if (IsEngineTrunk(atomicEffect))
                {
                    // 主动引擎主干（2026-11-08 规则地基定案，双选组整删）：主动声明可达面=附加诅咒/祝福
                    //（主动钉死——手动发动提示性空转，施放时已部署）+ 存量主动运势（自然缺省组保留声明
                    //——掷骰一步点击）；其余引擎（倒计时/拼点/死亡计数/元素充盈/团结）自动钉死，转换期
                    // 已归一不会走到主动档。自动声明仍条件载体跳过（BranchEngines 事件驱动；倒计时直发）。
                    if (effect.ActivationType == EffectActivationType.Voluntary)
                    {
                        var activeKind = atomicEffect.Branch?.EngineKind ?? BranchEngineKind.None;
                        if (activeKind == BranchEngineKind.LuckRoll)
                            await ApplyActiveLuckTrunkAsync(atomicEffect.Branch, context, fragments);
                        else if (activeKind == BranchEngineKind.CurseOnDraw || activeKind == BranchEngineKind.BlessingOnDraw)
                            await ApplyActiveEngineTrunkAsync(atomicEffect.Branch, activeKind, context, fragments);
                    }
                    continue;
                }
                var payload = atomicEffect.Branch;

                // 逐原子目标制（2026-10-09）：该原子按自身极性过滤域独立解析（弹窗/AI 代选落结算期）；
                // 声明期预选目标（originalTargets）先经该原子域归属过滤（域内沿用/域外自解析）；
                // 解析空（无域/全隐藏区/无候选）=空目标自结算（ExecuteEffectAsync 内原子级兜底，与奖励口径同）。
                // 共享口径照旧吃组合目标快照（originalTargets/compositionTargets）。
                // 步5 spec memo：同域共享组成员复用首解析结果（共享单窗单目标）。
                List<Entity> ownTargets = null;
                if (effect.PerAtomTargets)
                {
                    var spec = EffectHandlerRegistry.SpecOfAtom(specPlan, ai);
                    if (spec != null && specMemo.TryGetValue(spec.SpecKey, out var memoTargets))
                        ownTargets = new List<Entity>(memoTargets);
                    else
                    {
                        ownTargets = await EffectHandlerRegistry.ResolveAtomTargetsAsync(
                            effect, atomicEffect, context, preselectQueue);
                        if (spec != null) specMemo[spec.SpecKey] = new List<Entity>(ownTargets);
                    }
                }

                // 分段原子换轨（2026-11-08 目标选择重构步3）：参照（旧 kinds 路径解析结果）写
                // context.Refs["ref"]；受体（受害者/落点）由引擎从行 Domain 轴解析——第二窗从
                // handler 搬到引擎，次数/顺序/过滤口径不变。TargetsResolvedBySpec 封口防 kinds 兜底
                //（作用域收紧到本原子 ExecuteEffectAsync 期间——防泄漏进同上下文的 gate/奖励解析）。
                bool segAtom = atomicEffect.ReferenceAxis != null;
                if (segAtom)
                    ownTargets = await ResolveSegmentedTargetsAsync(atomicEffect, context, ownTargets, ai);

                if (HasOutcome(atomicEffect))
                {
                    // 产出条件（自由分支·Outcome）：per-target 执行 + 逐目标评估（LastOutcome 按目标重置）
                    var loopTargets = effect.PerAtomTargets
                        ? (ownTargets.Count > 0 ? ownTargets : new List<Entity> { null }) // 空解析=单次 null 自结算
                        : compositionTargets;
                    foreach (var target in loopTargets)
                    {
                        context.Targets = target != null ? new List<Entity> { target } : new List<Entity>();
                        context.LastOutcome.Reset();
                        context.TargetsResolvedBySpec = segAtom;

                        PublishPhase(atomicEffect, AtomicEffectPhase.StartApplying, context);
                        await EffectHandlerRegistry.ExecuteEffectAsync(atomicEffect, context);
                        context.TargetsResolvedBySpec = false;
                        CaptureDescription(atomicEffect, context, fragments);
                        PublishPhase(atomicEffect, AtomicEffectPhase.ResolutionComplete, context);

                        await ApplyOutcomeRewardsAsync(payload, context, fragments);
                    }
                }
                else
                {
                    // 逐原子模式：目标=该原子自身解析结果（空=自结算）；共享口径恢复原始目标
                    context.Targets = effect.PerAtomTargets
                        ? new List<Entity>(ownTargets)
                        : new List<Entity>(originalTargets); // 恢复原始目标（产出条件原子可能改写）
                    context.TargetsResolvedBySpec = segAtom;
                    await EffectHandlerRegistry.ExecuteEffectAsync(atomicEffect, context);
                    context.TargetsResolvedBySpec = false;
                    CaptureDescription(atomicEffect, context, fragments);

                    if (payload != null && payload.Settle == BranchSettleKind.Gate)
                        await ApplySituationGateAsync(payload, context, fragments);
                }
            }

            // 三阶段事件：结算完成（产出条件主干已 per-target 发布，跳过）
            foreach (var atomicEffect in effect.Effects)
            {
                if (HasOutcome(atomicEffect) || IsEngineTrunk(atomicEffect)) continue;
                PublishPhase(atomicEffect, AtomicEffectPhase.ResolutionComplete, context);
            }
        }

        /// <summary>
        /// 节点化 per-target 步骤遍历（单层）。
        /// 原子步骤先解析候选目标，对每个目标单独执行原子并写 LastOutcome；
        /// 若紧随其后是 OutcomeGate 分支步骤，则在同一目标循环体内立即评估并执行 then/else（奖励免费）。
        /// 抉择（Choice）步骤：按 context.ModeIndex 只执行所选模式的子序列（converter 已保证不嵌套）。
        /// </summary>
        private async UniTask ExecuteStepsAsync(EffectDefinition effect, EffectExecutionContext context,
            List<string> fragments)
        {
            await ExecuteStepSequenceAsync(effect.Steps, effect, context, fragments);
        }

        /// <summary>
        /// 步骤序列执行（主序列与抉择模式子序列共用）：原子 per-target 遍历 +
        /// 紧邻 OutcomeGate 前瞻配对（奖励免费）；Choice 步骤按 ModeIndex 分派。
        /// 2026-09-10 目标域模型：组合目标已在 ExecuteAsync 统一解析（context.Targets），
        /// 步骤层不再逐原子解析——全部原子共享同一份目标序列（与扁平路径语义统一）。
        /// </summary>
        private async UniTask ExecuteStepSequenceAsync(List<RuntimeEffectStep> steps, EffectDefinition effect,
            EffectExecutionContext context, List<string> fragments)
        {
            // 组合目标序列（快照防遍历中被改；无目标效果 = 单次 null 目标执行）
            var compositionTargets = context.Targets != null && context.Targets.Count > 0
                ? new List<Entity>(context.Targets)
                : new List<Entity> { null };
            // 声明期预选队列（2026-10-20 有序消费，与扁平路径同口径）：序列内逐原子共享可消耗
            var preselectQueue = compositionTargets[0] == null && compositionTargets.Count == 1
                ? new List<Entity>()
                : new List<Entity>(compositionTargets);

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];

                // 抉择：只执行所选模式（越界 Clamp——多 Choice 步骤共享卡级 ModeIndex，数量不一致时各自钳制）
                if (step.Kind == RuntimeStepKind.Choice)
                {
                    var chosen = step.Choices != null && step.Choices.Count > 0
                        ? step.Choices[System.Math.Max(0, System.Math.Min(context.ModeIndex, step.Choices.Count - 1))]
                        : null;
                    if (chosen != null)
                        await ExecuteStepSequenceAsync(chosen, effect, context, fragments); // 模式内原子共享同一份组合目标
                    continue;
                }

                if (step.Kind != RuntimeStepKind.Atomic || step.Atomic == null)
                    continue; // 落单分支步骤无前置原子，跳过（正常由原子步骤前瞻消费）

                var atomic = step.Atomic;

                // 前瞻：下一步是否为 OutcomeGate 分支
                RuntimeEffectStep gate =
                    (i + 1 < steps.Count && steps[i + 1].Kind == RuntimeStepKind.Branch)
                        ? steps[i + 1]
                        : null;

                // 逐原子目标制（2026-10-09）：该原子按自身极性过滤域独立解析（弹窗/AI 代选落结算期）；
                // 声明期预选目标经有序队列归属消费（2026-10-20）；空解析=单次 null 自结算。
                var stepTargets = compositionTargets;
                if (effect.PerAtomTargets)
                {
                    var resolved = await EffectHandlerRegistry.ResolveAtomTargetsAsync(
                        effect, atomic, context, preselectQueue);
                    // 分段原子换轨（步3）：参照→Refs，受体=行 Domain 引擎侧解析；空=单次 null 空转
                    //（gate 照常评估——与旧 handler 早退后 gate 仍评估的口径一致）。
                    // TargetsResolvedBySpec 作用域收紧到本原子 ExecuteEffectAsync 期间（防泄漏进 gate 奖励）。
                    if (atomic.ReferenceAxis != null)
                    {
                        var victims = await ResolveSegmentedTargetsAsync(atomic, context, resolved, i);
                        stepTargets = victims.Count > 0 ? victims : new List<Entity> { null };
                    }
                    else
                    {
                        stepTargets = resolved.Count > 0 ? resolved : new List<Entity> { null };
                    }
                }

                foreach (var target in stepTargets)
                {
                    context.Targets = target != null ? new List<Entity> { target } : new List<Entity>();
                    context.LastOutcome.Reset();
                    context.TargetsResolvedBySpec = atomic.ReferenceAxis != null;

                    PublishPhase(atomic, AtomicEffectPhase.Activation, context);
                    PublishPhase(atomic, AtomicEffectPhase.StartApplying, context);
                    await EffectHandlerRegistry.ExecuteEffectAsync(atomic, context);
                    context.TargetsResolvedBySpec = false;
                    CaptureDescription(atomic, context, fragments);
                    PublishPhase(atomic, AtomicEffectPhase.ResolutionComplete, context);

                    if (gate != null)
                        await ApplyGateRewardsAsync(gate, context, fragments);
                }

                if (gate != null)
                    i++; // 消费已配对的分支步骤
            }
        }

        // ======================================== 分段原子换轨（2026-11-08 目标选择重构步3） ========================================

        /// <summary>分段原子（ReferenceAxis != null，活体炸弹/护甲）引擎侧解析：参照（旧 kinds 路径
        /// 解析结果首位）写 context.Refs["ref"]；受体（受害者/落点）从**行 Domain 轴**解析——第二窗
        /// 从 handler 搬到引擎，窗口次数/顺序/过滤口径与 handler 旧实现完全同源
        ///（ResolveCandidates→ApplyTauntRestriction(edict 豁免=false)→ExcludeUnselectable；
        /// 候选&gt;1 弹选、=1 取首、空=空转；PromptValue=参照攻/命）。SpecKey 写 ApprovedSpecKeys。
        /// 返回受体列表（空=空转）；调用方须置 TargetsResolvedBySpec 防 kinds 兜底误解析。</summary>
        private static async UniTask<List<Entity>> ResolveSegmentedTargetsAsync(
            AtomicEffectInstance atomic, EffectExecutionContext context,
            List<Entity> resolvedReferences, int atomIndex)
        {
            var none = new List<Entity>();
            var reference = resolvedReferences != null ? resolvedReferences.FirstOrDefault() : null;
            if (reference == null || !reference.IsAlive)
            {
                context.Refs.Remove("ref"); // 清上一轮/上一原子残留（重放重入防读到过期参照）
                return none; // 参照空/亡=空转（旧 handler 早退口径）
            }

            context.Refs["ref"] = reference;
            context.ApprovedSpecKeys.Add($"atom:{atomIndex}/spec:0/role:reference");

            var row = AtomicEffectTable.GetByHashId(atomic.RowHashId);
            if (row == null || !TargetAxisRules.TryParseDomain(row.Domain, out var victimAxis))
            {
                TideLog.Warn($"[EffectExecutor] 分段原子 {atomic.Type}（行 {atomic.RowHashId}）行 Domain='"
                             + (row != null ? row.Domain : "行缺失") + "' 不可产轴——受体空转");
                return none;
            }

            var candidates = EffectHandlerRegistry.ResolveCandidates(
                row.GetTargetKindList(), "", context, victimAxis);
            candidates = TargetResolver.ExcludeUnselectable(
                TargetResolver.ApplyTauntRestriction(candidates, context, false), context.Controller);
            if (candidates.Count == 0) return none; // 受体域空=空转（旧 handler 口径）

            Entity victim = candidates[0];
            if (candidates.Count > 1)
            {
                // G6 收口（2026-11-08）：反问标题/预览值按行数据判定（行极性）——参照族正极性=
                // 贴附（落点·预览参照命）；负极性=直伤（受害者·预览参照攻）。不再按枚举点名；
                // 非参照族不会进本窗（分段原子才有反问段）。
                bool attachRow = row.Polarity > 0f;
                var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = candidates,
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = context.Controller,
                    Title = "选择" + row.DisplayName + "的" + (attachRow ? "落点" : "受害者"),
                    PromptSource = (Entity)context.CastCard ?? context.Source,
                    PromptAtom = atomic.Type,
                    PromptValue = attachRow
                        ? Math.Max(0, EntityEffectExtensions.GetLife(reference))
                        : Math.Max(0, EntityEffectExtensions.GetPower(reference)),
                    SpecKey = $"atom:{atomIndex}/spec:1/role:target",
                    SpecRole = Attribute.TargetSpecRole.Target, // 步5 AI 分派（受体窗打脸优先）
                });
                if (picked != null && picked.Count > 0) victim = picked[0];
            }
            if (victim == null || !victim.IsAlive) return none;

            context.ApprovedSpecKeys.Add($"atom:{atomIndex}/spec:1/role:target");
            return new List<Entity> { victim };
        }

        // 错边黑白发放（CollectWrongSideHits/FlushWrongSideGrants/SnapshotHealInverted/TargetSide）
        // 已随黑白获得经济整体删除（2026-10-28 P1 定案：代价=构筑期抵扣，不再产出元素）。

        /// <summary>
        /// [遗留步骤路径专用] 评估产出条件并执行对应的 then/else 奖励（免费、各自解析目标）。
        /// 评估读取的是「当前目标」刚写入的 LastOutcome。
        /// 预言族条件（延迟验证）在此拦截：不即时评估，把分支打包成 PendingProphecy
        /// 注册到 ProphecySystem——对手下回合首张出牌时验证，到期未验证作未命中走 else。
        /// 条件 id 不被评估器识别时恒假走 else。
        /// </summary>
        private async UniTask ApplyGateRewardsAsync(RuntimeEffectStep gate, EffectExecutionContext context,
            List<string> fragments)
        {
            if (BranchConditionEvaluator.IsDelayedCondition(gate.ConditionId))
            {
                ProphecySystem.Register(new PendingProphecy
                {
                    Declarer = context.Controller,
                    Source = context.Source,
                    Declaration = context.LastOutcome.Declaration ?? gate.ConditionStringParam,
                    ConditionId = gate.ConditionId,
                    ConditionParam = gate.ConditionParam,
                    ConditionStringParam = gate.ConditionStringParam,
                    Then = gate.Then,
                    Else = gate.Else,
                    ZoneManager = context.ZoneManager,
                    ElementPool = context.ElementPool,
                });
                return;
            }

            bool pass = BranchConditionEvaluator.Evaluate(
                gate.ConditionId, context.LastOutcome, gate.ConditionParam, gate.ConditionStringParam, context);
            if (gate.Negate) pass = !pass;

            var rewards = pass ? gate.Then : gate.Else;
            if (rewards == null || rewards.Count == 0)
                return;

            await ExecuteThenRewardsAsync(rewards, context, fragments);
        }

        // ======================================== 槽级分支载荷评估（两槽定案 2026-10-05） ========================================

        /// <summary>产出条件评估（自由分支·Outcome）：读当前目标 LastOutcome（per-target 调用）——
        /// 预言族（延迟验证）拦截为 PendingProphecy（ProphecySystem 在验证时刻结算，Else 恒空）；
        /// 命中 → Then 奖励强制结算（依次弹选目标）。</summary>
        private static async UniTask ApplyOutcomeRewardsAsync(BranchPayload payload,
            EffectExecutionContext context, List<string> fragments)
        {
            if (BranchConditionEvaluator.IsDelayedCondition(payload.OutcomeId))
            {
                ProphecySystem.Register(new PendingProphecy
                {
                    Declarer = context.Controller,
                    Source = context.Source,
                    Declaration = context.LastOutcome.Declaration ?? payload.ConditionStringParam,
                    ConditionId = payload.OutcomeId,
                    ConditionParam = payload.ConditionParam,
                    ConditionStringParam = payload.ConditionStringParam,
                    Then = payload.Then,
                    Else = null,
                    ZoneManager = context.ZoneManager,
                    ElementPool = context.ElementPool,
                });
                return;
            }

            bool pass = BranchConditionEvaluator.Evaluate(
                payload.OutcomeId, context.LastOutcome, payload.ConditionParam, payload.ConditionStringParam, context);
            if (!pass) return;
            await ExecuteBranchRewardsAsync(payload, context, fragments); // 双通道（效果引用+原子，2026-11-07）
        }

        /// <summary>局面门评估（有限分支·Gate，2026-10-09 还原定案）：与原子产出无关，效果结算时评估一次；
        /// 达标 → Then 奖励（自己弹选目标）；不达标 → 不奖励也不惩罚（纯条件奖励——对赌惩罚面不还原）。</summary>
        private static async UniTask ApplySituationGateAsync(BranchPayload payload,
            EffectExecutionContext context, List<string> fragments)
        {
            if (BranchConditionEvaluator.IsDelayedCondition(payload.GateId)) return; // 预言族不在局面门目录——防御
            bool pass = BranchConditionEvaluator.Evaluate(
                payload.GateId, context.LastOutcome, payload.ConditionParam, payload.ConditionStringParam, context);
            if (!pass) return;
            await ExecuteBranchRewardsAsync(payload, context, fragments); // 双通道（效果引用+原子，2026-11-07）
        }

        /// <summary>主动运势掷骰（2026-10-20 一步点击定案：点击→选目标→执行期先掷骰，达标则分支
        /// 效果适用、未达标落空）：掷双骰判 d1,d2 ≥ x，成败都播报（战报白名单放行"运势"）；达标 →
        /// Then 奖励**作用于组合目标**——效果级作用范围（kinds）在结算期首已弹选共享（单候选自动取），
        /// 未声明域/无候选（组合目标空）回落 ExecuteThenRewardsAsync 弹选口径。奖励原子免费
        /// （卡价收讫，同 FireRewards 口径）。</summary>
        private static async UniTask ApplyActiveLuckTrunkAsync(BranchPayload payload,
            EffectExecutionContext context, List<string> fragments)
        {
            if (BranchPayloadEmpty(payload)) return;
            int x = Math.Max(1, Math.Min(6, payload.EngineParam));
            int d1 = GameRng.Next(1, 7), d2 = GameRng.Next(1, 7);
            bool hit = d1 >= x && d2 >= x;
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = context.Source, Keyword = "运势",
                Detail = hit ? $"运势 {d1}+{d2} ≥ {x}×2：分支效果适用" : $"运势 {d1}+{d2} < {x}×2：未达标",
            });
            // 表现层结构化观察口（成败都发）：来源单位头顶浮字「投掷结果 d1+d2，效果通过/不通过」
            EventManager.Instance.Publish(new LuckRolledEvent
            {
                EngineCard = context.Source as Card,
                Controller = context.Controller,
                D1 = d1, D2 = d2, Threshold = x, Success = hit,
            });
            if (!hit) return;
            await ApplyThenToSharedTargetsAsync(payload, context, fragments);
        }

        /// <summary>主动引擎主干（2026-10-30 双选组主动路径——拼点/死亡计数/团结/附加诅咒·祝福；
        /// 运势走掷骰专用口 ApplyActiveLuckTrunkAsync）：点击 → 立即评估引擎条件
        ///（BranchEngines.EvaluateActiveCondition——副作用与自动路径同源：拼点照常展示比攻/发
        /// ClashResolvedEvent，死亡计数只读不清，团结读注入邻接数）→ 达标发放 Then 奖励
        ///（组合目标逐个适用，无组合目标回落奖励弹选），未达标落空播报。
        /// 附加诅咒·祝福=施放时已部署（OnCardCastResolved），手动发动为提示性空转（不重复部署）。
        /// 不走每卡每回合引擎发射账——主动发动受横置/次数档自理（运势主动同口径）。</summary>
        private static async UniTask ApplyActiveEngineTrunkAsync(BranchPayload payload, BranchEngineKind kind,
            EffectExecutionContext context, List<string> fragments)
        {
            if (BranchPayloadEmpty(payload)) return;
            string zh = ComposerCatalog.EngineZhOf(kind);
            if (kind == BranchEngineKind.CurseOnDraw || kind == BranchEngineKind.BlessingOnDraw)
            {
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = context.Source, Keyword = zh,
                    Detail = $"{zh}在施放时已部署（抽到挂载卡时自动执行）——手动发动无额外效果",
                });
                return;
            }
            bool hit = BranchEngines.EvaluateActiveCondition(kind, payload,
                context.Source as Card, context.Controller, out var detail);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = context.Source, Keyword = zh,
                Detail = hit ? $"{detail}：分支效果适用" : $"{detail}：未达标",
            });
            if (!hit) return;
            await ApplyThenToSharedTargetsAsync(payload, context, fragments);
        }

        /// <summary>主动引擎达标后的奖励适用（运势/通用主动主干共用尾段）：奖励效果引用先按自身域结算
        ///（2026-11-07 双通道——引用效果不吃组合目标，原子域独立弹选）；Then 原子组合目标非空→逐目标适用
        ///（效果级作用范围在结算期首已弹选共享）；空→ExecuteThenRewardsAsync 弹选口径。执行完恢复
        /// 原目标列表。奖励原子免费（卡价收讫，同 FireRewards 口径）。</summary>
        private static async UniTask ApplyThenToSharedTargetsAsync(BranchPayload payload,
            EffectExecutionContext context, List<string> fragments)
        {
            if (!string.IsNullOrEmpty(payload?.EffectId))
                await ExecuteReferencedEffectAsync(payload.EffectId, context, fragments);
            if (payload.Then == null || payload.Then.Count == 0) return;
            if (context.Targets == null || context.Targets.Count == 0)
            {
                await ExecuteThenRewardsAsync(payload.Then, context, fragments);
                return;
            }
            var shared = new List<Entity>(context.Targets);
            foreach (var reward in payload.Then)
            {
                if (reward == null) continue;
                foreach (var rt in shared)
                {
                    context.Targets = rt != null ? new List<Entity> { rt } : new List<Entity>();
                    await EffectHandlerRegistry.ExecuteEffectAsync(reward, context);
                    CaptureDescription(reward, context, fragments);
                }
            }
            context.Targets = shared;
        }

        /// <summary>分支载荷双通道皆空（Then 原子列空 ∧ 效果引用空）=无奖励可发
        ///（2026-11-07 分支奖励效果化——发射口统一闸）。</summary>
        private static bool BranchPayloadEmpty(BranchPayload payload)
            => (payload?.Then == null || payload.Then.Count == 0)
               && string.IsNullOrEmpty(payload?.EffectId);

        /// <summary>Then 奖励强制结算（两槽定案，槽级载荷/引擎共用）：依次执行——
        /// 有目标域的奖励按合法范围弹窗选 1（2026-10-07 晚定案：Chooser=效果控制者，AI/无头自动选首）；
        /// 无域/隐藏区按单次 null 目标执行（handler 自结算）。执行完恢复原目标列表。</summary>
        public static async UniTask ExecuteThenRewardsAsync(List<AtomicEffectInstance> then,
            EffectExecutionContext context, List<string> fragments, string title = "选择奖励目标")
        {
            if (then == null || then.Count == 0) return;
            var saved = context.Targets != null ? new List<Entity>(context.Targets) : new List<Entity>();
            foreach (var reward in then)
            {
                if (reward == null) continue;
                var rTargets = await EffectHandlerRegistry.ResolveRewardTargetsAsync(reward, context, title);
                var rIter = rTargets.Count > 0 ? rTargets : new List<Entity> { null };
                foreach (var rt in rIter)
                {
                    context.Targets = rt != null ? new List<Entity> { rt } : new List<Entity>();
                    await EffectHandlerRegistry.ExecuteEffectAsync(reward, context);
                    CaptureDescription(reward, context, fragments);
                }
            }
            context.Targets = saved;
        }

        // ======================================== 分支奖励效果引用（2026-11-07 分支奖励效果化） ========================================

        /// <summary>奖励效果自引用环解析深度闸（A 的奖励引用 A → 奖励执行 A → 门再达标 → …）：
        /// 资格谓词已拦引擎主干、选择器排除自身 id，本闸只兜直改 JSON 的环——4 层足够任何合法链
        ///（与计价侧 _refCostDepth 同惯例）。</summary>
        [ThreadStatic] private static int _rewardFxDepth;

        /// <summary>分支奖励统一结算口（2026-11-07 分支奖励效果化——Gate/Outcome/Engine 三 settle 共用）：
        /// 先结算奖励效果引用（payload.EffectId → 组合效果定义遍历），后 Then 原子列（弹选口径）——
        /// 「组合后的效果+原子」双通道并存；单通道为空即跳过该段。双通道皆空=no-op。</summary>
        public static async UniTask ExecuteBranchRewardsAsync(BranchPayload payload,
            EffectExecutionContext context, List<string> fragments)
        {
            if (payload == null) return;
            if (!string.IsNullOrEmpty(payload.EffectId))
                await ExecuteReferencedEffectAsync(payload.EffectId, context, fragments);
            if (payload.Then != null && payload.Then.Count > 0)
                await ExecuteThenRewardsAsync(payload.Then, context, fragments);
        }

        /// <summary>奖励效果引用结算：EffectsLibrary.Resolve → ConvertOne → 按 Steps（Atomic 执行/
        /// Branch 局面门评估，产出族门无上游产出恒否）/扁平 Effects 遍历——原子按**自身域**解析目标
        ///（逐原子前清空 ctx.Targets，与诅咒引擎载荷 2026-11-06 晚定案同口径：防预置目标劫持域解析），
        /// 目标由控制者结算期弹选（AI/无头自动选首）。缺失/解析失败=告警跳过（装载校验已点名）。</summary>
        private static async UniTask ExecuteReferencedEffectAsync(string effectId,
            EffectExecutionContext context, List<string> fragments)
        {
            if (_rewardFxDepth > 4)
            {
                TideLog.Warn($"[EffectExecutor] 分支奖励效果引用深度超限（疑似自引用环）：{effectId}——已截断");
                return;
            }
            var data = EffectsLibrary.Resolve(effectId);
            var def = data != null ? CardEffectConverter.ConvertOne(data, effectId) : null;
            if (def == null)
            {
                TideLog.Warn($"[EffectExecutor] 分支奖励效果缺失: {effectId}（效果库无此条）——跳过");
                return;
            }

            var saved = context.Targets != null ? new List<Entity>(context.Targets) : new List<Entity>();
            _rewardFxDepth++;
            try
            {
                async UniTask ExecuteAtomAsync(AtomicEffectInstance atom)
                {
                    if (atom == null) return;
                    context.Targets = new List<Entity>(); // 逐原子按自身域解析（免预置劫持）
                    await EffectHandlerRegistry.ExecuteEffectAsync(atom, context);
                    CaptureDescription(atom, context, fragments);
                }

                if (def.Steps != null && def.Steps.Count > 0)
                {
                    foreach (var step in def.Steps)
                    {
                        if (step == null) continue;
                        if (step.Kind == RuntimeStepKind.Atomic)
                        {
                            await ExecuteAtomAsync(step.Atomic);
                        }
                        else if (step.Kind == RuntimeStepKind.Branch)
                        {
                            // 局面状态族门可评估（读 ctx）；产出族门无上游产出恒否（与 CurseSystem 同取舍）
                            bool hit = BranchConditionEvaluator.Evaluate(
                                step.ConditionId, context.LastOutcome, step.ConditionParam, step.ConditionStringParam, context);
                            if (step.Negate) hit = !hit;
                            var branch = hit ? step.Then : step.Else;
                            if (branch == null) continue;
                            foreach (var atom in branch)
                                await ExecuteAtomAsync(atom);
                        }
                    }
                    return;
                }
                foreach (var atom in def.Effects)
                    await ExecuteAtomAsync(atom);
            }
            finally
            {
                _rewardFxDepth--;
                context.Targets = saved;
            }
        }

        /// <summary>
        /// 原子片段捕获（2026-09-16 描述接口化）：在原子执行**后**调用——此刻 context.Targets=当前目标、
        /// LastOutcome=handler 写入的真实产出（描述不重掷随机）。经 handler 接口生成片段入聚合列表。
        /// </summary>
        private static void CaptureDescription(AtomicEffectInstance atom, EffectExecutionContext context,
            List<string> fragments)
        {
            if (atom == null || fragments == null) return;
            var handler = EffectHandlerRegistry.GetHandler(atom.Type);
            if (handler == null) return;
            fragments.Add(handler.GetDescription(atom, context));
        }

        private static void PublishPhase(AtomicEffectInstance atomic, AtomicEffectPhase phase, EffectExecutionContext context)
        {
            // 时点接线定案：原子三阶段经 GameCore 统一路由（直发总线会让 TriggerEngine 永远收不到——
            // 这是场上效果发动的可观察时点，双泳道交叉点；路由=总线一次+Trigger/Layer 推送，无双发）
            var e = new AtomicEffectPhaseEvent
            {
                EffectType = atomic.Type,
                Phase = phase,
                Source = context.Source,
                Targets = context.Targets,
                EffectInstance = atomic,
                Context = context
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
            else EventManager.Instance.Publish(e);
        }


        /// <summary>
        /// 新回合开始
        /// </summary>
        public void OnNewTurn(int turnNumber)
        {
            _usageTracker.OnNewTurn(turnNumber);
            // 整账重置播报（EffectId=null 语义）：UI (x/y) 读数随回合归零，订阅方全量重查
            EventManager.Instance.Publish(new EffectUsageChangedEvent());
        }

        /// <summary>
        /// 重置（新游戏）
        /// </summary>
        public void Reset()
        {
            _usageTracker.Reset();
            // 关键词生效次数闸已随坚韧指示物化（2026-10-08）整体退役——无全局关键词态需清
        }

        /// <summary>
        /// 重放开启口·先闸后付（2026-11-02 改版：对玩家指示物，蓝5 表行 93648aa7/GrantReplay；
        /// 同日用户定案=代价支付失败时重复无效）：**发起方玩家**（Controller）使用的法术施放
        /// （CastCard=法术卡——法术效果 Source=角色归因定案）或主动效果发动（非触发式·源卡在场
        /// 存活）结算后，持重放层 → 先过代价可付闸（CostPayability.AllPayableForReplay 溢出制：
        /// 库存≥1 即可付；弃牌手牌0/磨牌牌库0/域候选空/自伤不足量=无支付目标）——
        /// **闸不过 → 返回 false：重复无效、层保留**（生效自减=真正生效才消耗，层等下一张）；
        /// 闸过 → 消耗 1 层+播报，返回 true（外层再执行一轮——代价溢出执行+效果行）。
        /// 重放轮由外层 replayed 标记拦截，不再到达此口（「重放不触发重放」）。
        /// 触发式/自动效果/攻击守卫/技能/手牌主动（充能）不触发；诅咒载荷/预言验证/延迟效果/
        /// 代价栏旁路 executor=设计内不重放。无回合限制（Exception 不倒数）——层跨回合存续直至消耗。
        /// </summary>
        private bool TryBeginReplayRerun(EffectInstance instance, List<CostInstance> specialCosts)
        {
            if (instance?.Controller == null) return false;
            bool isSpellCast = instance.CastCard != null && instance.CastCard.IsSpellCard();
            bool isActiveUse = instance.TriggeringEvent == null
                && instance.Source is Card src && src.IsAlive
                && src.GetZone() == Zone.Battlefield;
            if (!isSpellCast && !isActiveUse) return false;

            var player = instance.Controller;
            if (player.GetEffectiveCounterCount(Attribute.CounterRules.ReplayCounter) <= 0) return false;

            var rows = ReplayCostRows(instance, specialCosts);
            if (rows.Count > 0 && !CostPayability.AllPayableForReplay(rows, ReplayCostContext(instance)))
            {
                TideLog.Info("[EffectExecutor] 重放轮代价无支付目标——重复无效（层保留，等下一张法术/主动效果）");
                return false;
            }

            player.RemoveCounters(Attribute.CounterRules.ReplayCounter, 1);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = player,
                Keyword = Attribute.CounterRules.ReplayCounter,
                Detail = $"重放发作：{EffectText.Name(player)} 使用的法术/主动效果再次执行（消耗 1 层，重放不触发重放）",
                Source = instance.Source,
            });
            return true;
        }

        /// <summary>重放轮代价行全集（闸与付共源）：效果级代价行（specialCosts——法术所选主动行/
        /// 单位主动行/非主动行的原班）。卡级行已随 2026-11-08 代价效果级化整删——
        /// 施放期代价已并入 specialCosts 通道，重放轮不再另收。</summary>
        private List<CostInstance> ReplayCostRows(EffectInstance instance, List<CostInstance> specialCosts)
        {
            var rows = new List<CostInstance>();
            if (specialCosts != null) rows.AddRange(specialCosts);
            return rows;
        }

        /// <summary>重放轮代价上下文（闸=CostPayability 纯读、付=PayPayloadsAsync 执行，共用）。</summary>
        private CostContext ReplayCostContext(EffectInstance instance) => new CostContext
        {
            Payer = instance.Controller,
            ZoneManager = _zoneManager,
            ElementPool = _elementPool,
            Source = instance.Source,
        };

        /// <summary>
        /// 重放轮代价重执行（2026-11-02 改版「包括效果和代价」·2026-11-08 效果级化）：
        /// 效果级代价行（specialCosts 全集）各再执行一轮；元素费用不重复（重放只重复代价，不重复费用）。
        /// 只执行不产出（P1 口径）；可付闸已过（TryBeginReplayRerun），此处按溢出执行
        ///（弃 N 而手牌 &lt;N 弃光现有——执行层天然口径）；返回 false 仅理论兜底（闸后同帧无状态窗）。
        /// </summary>
        private async UniTask PayReplayRoundCostsAsync(EffectInstance instance, List<CostInstance> specialCosts)
        {
            var rows = ReplayCostRows(instance, specialCosts);
            if (rows.Count == 0) return;
            if (!await CostCompensationService.PayPayloadsAsync(rows, ReplayCostContext(instance)))
                TideLog.Warn("[EffectExecutor] 重放轮代价行执行失败——空转（可付闸已过，效果行照常执行）");
        }

        private bool CheckTiming(
            EffectDefinition effect,
            PhaseType currentPhase,
            Player activator,
            Player activePlayer)
        {
            // 检查阶段限制
            if (effect.TriggerTiming == TriggerTiming.Activate_Active)
            {
                if (currentPhase != PhaseType.Main)
                    return false;
            }

            // 检查回合限制条件
            foreach (var condition in effect.ActivationConditions)
            {
                if (condition.Type == ConditionType.OnlyOwnTurn && activator != activePlayer)
                    return false;

                if (condition.Type == ConditionType.OnlyOpponentTurn && activator == activePlayer)
                    return false;

                if (condition.Type == ConditionType.OnlyMainPhase && currentPhase != PhaseType.Main)
                    return false;
            }

            return true;
        }
    }
}
