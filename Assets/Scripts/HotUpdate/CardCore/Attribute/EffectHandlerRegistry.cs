using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace CardCore.Attribute
{
    /// <summary>
    /// 效果处理器注册表
    /// 管理所有原子效果处理器的注册和查找
    /// </summary>
    public static class EffectHandlerRegistry
    {
        private static readonly Dictionary<AtomicEffectType, IAtomicEffectHandler> _handlers
            = new Dictionary<AtomicEffectType, IAtomicEffectHandler>();

        /// <summary>
        /// 注册处理器
        /// </summary>
        public static void Register(IAtomicEffectHandler handler)
        {
            if (handler == null) return;

            _handlers[handler.EffectType] = handler;

            var config = AtomicEffectTable.GetByType(handler.EffectType);
       
        }

        /// <summary>
        /// 获取处理器
        /// </summary>
        public static IAtomicEffectHandler GetHandler(AtomicEffectType type)
        {
            return _handlers.TryGetValue(type, out var handler) ? handler : null;
        }

        /// <summary>
        /// 尝试获取处理器
        /// </summary>
        public static bool TryGetHandler(AtomicEffectType type, out IAtomicEffectHandler handler)
        {
            return _handlers.TryGetValue(type, out handler);
        }

        /// <summary>
        /// 执行原子效果
        /// 每个原子效果根据自身的 AtomicEffectConfig 独立解析目标
        /// </summary>
        public static bool ExecuteEffect(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (!PrepareForExecution(effect, context, out var handler))
                return false;

            handler.Execute(effect, context);
            return true;
        }

        /// <summary>
        /// 异步执行原子效果。
        /// 目标解析与 ExecuteEffect 相同（同步），仅最终 handler 调用走 ExecuteAsync 以支持 UI 等待。
        /// 非交互 handler 经基类默认实现即时完成，行为与同步路径一致。
        /// </summary>
        public static async UniTask<bool> ExecuteEffectAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect == null || context == null) return false;

            // 调用方已预选目标（玩家施放指定 / per-target 步骤逐个传入）则沿用；否则走交互解析（候选>需求时弹选择）。
            bool hasPreselectedTargets = context.Targets != null && context.Targets.Count > 0;
            if (!hasPreselectedTargets)
                context.Targets = await ResolveTargetsInteractiveAsync(effect, context);

            if (!PrepareForExecution(effect, context, out var handler, skipTargetResolution: true))
                return false;

            await handler.ExecuteAsync(effect, context);
            return true;
        }

        /// <summary>
        /// 共享前置：查找 handler、解析目标、CanExecute 校验。
        /// 成功时 handler 非 null 且 context.Targets 已就绪。
        /// </summary>
        private static bool PrepareForExecution(AtomicEffectInstance effect, EffectExecutionContext context,
            out IAtomicEffectHandler handler, bool skipTargetResolution = false)
        {
            handler = null;
            if (effect == null || context == null) return false;

            if (!TryGetHandler(effect.Type, out handler))
            {
                TideLog.Warn($"未注册的效果处理器: {effect.Type}");
                return false;
            }

            // 目标解析：调用方已预选目标（如玩家施放法术时指定，或 per-target 步骤遍历逐个传入）
            // 则直接沿用，否则按原子效果配置自动解析。触发式效果不预选 → 走自动解析。
            // skipTargetResolution=true 时表示异步路径已完成（可能含交互/动态 0 目标）解析，不再覆盖。
            bool hasPreselectedTargets = context.Targets != null && context.Targets.Count > 0;
            if (!skipTargetResolution && !hasPreselectedTargets)
            {
                context.Targets = ResolveTargets(effect, context);
            }

            // 法术护盾（2026-10-09 指示物化）：每层抵消一次对手效果对自身的作用
            //（移出目标 + 消耗 1 层法术护盾指示物；多层=多次抵消）
            KeywordRules.ConsumeSpellShields(context.Targets, context.Source);

            if (!handler.CanExecute(effect, context))
            {
                TideLog.Warn($"效果无法执行: {effect.Type}");
                return false;
            }

            return true;
        }

        /// <summary>每实例有效目标参数（域模型）：实例解析域 + 实例 filter + 计数恒 1
        /// （2026-09-10 表级 TargetCount 列删除——分支奖励等免编排路径的单原子按 1 个目标结算；
        /// 多目标需求由组合层 TargetCount 声明）。</summary>
        private static (List<int> kinds, string filter, int count) GetEffectiveDomain(AtomicEffectInstance effect)
        {
            // 免编排路径无 def 可回落：实例每原子声明优先（2026-10-09），未声明恒 1（旧口径）
            return (effect.TargetKinds ?? new List<int>(),
                    effect.Filter ?? "",
                    effect.TargetCount != -2 ? effect.TargetCount : 1);
        }

        /// <summary>
        /// 组合效果的目标选择（执行引擎统一入口，2026-09-10 目标域模型）：
        /// 域 = def 预计算交集（per-mode）；按 SelectionMode 六值（2026-09-16 定案）三维出目标——
        /// 运行时只实现三个行为（单/多的差别是构筑期数据契约，不是运行时分支）：
        /// 选一（Single/SingleUnion）：need=1（域={Self} 时=源卡自身不弹；其余候选非空一律弹选——
        /// 2026-10-16 定案：单候选也要玩家确认落点）；
        /// 选多（Multiple/MultipleUnion）：TargetCount/DynamicTargetCount 管数量（固定档候选非空弹选）；
        /// 全取（Whole/WholeUnion）：候选全取。RandomTarget 标志：绕过选择从完整候选域按种子随机抽取。
        /// </summary>
        public static async UniTask<List<Entity>> ResolveCompositionTargetsAsync(
            EffectDefinition def, EffectExecutionContext context)
        {
            if (def == null || context == null) return new List<Entity>();

            var domain = def.TargetDomain;
            if (def.ChoiceDomains != null && context.ModeIndex >= 0
                && context.ModeIndex < def.ChoiceDomains.Length
                && def.ChoiceDomains[context.ModeIndex] != null
                && def.ChoiceDomains[context.ModeIndex].Count > 0)
            {
                domain = def.ChoiceDomains[context.ModeIndex];
            }
            // 无目标效果（区域自结算类：抽卡/磨牌/看顶等，2026-09-11 语义修正）——
            // 域非空也不解析候选、不弹选（牌库是隐藏信息；handler 按域自结算）
            // 相同目标定案（2026-10-04）：None 但组合域含**可见区**且调用方未预选目标（触发式）不再早退——
            // 按选一共享解析一次：并列全体原子共享同一份选中目标（候选非空弹选——2026-10-16 起
            // 单候选也弹；RandomTarget 随机 1），
            // 消灭逐原子独立解析的退化兜底。域空/全隐藏区维持早退（隐藏区每原子兜底=首候选，不弹窗）。
            // 施放路径声明期已预选目标（instance.Targets 非空），不进本函数——域校验口径不变。
            if (def.SelectionMode == SelectionMode.None
                && (domain == null || domain.Count == 0 || TargetKindRules.AllHiddenZone(domain)))
                return new List<Entity>();
            bool noneAsSingle = def.SelectionMode == SelectionMode.None; // None+可见域：按选一共享
            if (domain == null || domain.Count == 0) return new List<Entity>(); // 无目标效果

            var candidates = ResolveCandidates(domain, def.TargetFilter, context);
            GameActions.Crumb($"comp-targets def={def.Id} mode={def.SelectionMode} dom={TargetKindRules.Format(domain)} cands={candidates.Count} sel={context.Controller?.Name}");
            if (candidates.Count == 0)
            {
                // Self 域空转诊断（原 Self 模式口径）：源不在候选（瞬间在发动区不在单位域）
                if (domain.Count == 1 && domain[0] == (int)TargetKind.Self)
                    TideLog.Warn(
                        $"[TargetDomain] 域={{Self}} 但源不在候选内（瞬间在发动区？）——效果空转: {def.Id}");
                return candidates;
            }

            // edict 豁免（2026-09-13）：牺牲类选择权在目标方——帷幕只约束对手的选择，不管持有者自选
            bool edictExempt = def.Effects != null
                && def.Effects.Any(a => a != null && TargetResolver.IsEdict(a.Type));

            // 全取（Whole/WholeUnion）：候选全取——潜行照常命中（范围波及），不受帷幕收窄。
            if (SelectionModeRules.IsTakeAll(def.SelectionMode))
                return candidates;

            // 目标随机（RandomTarget 正交标志，2026-09-16 自 SelectionMode 移出）：不弹窗，随机种子自动抽取；
            // 从完整候选域抽（对方侧潜行可被随机命中——绕过选择），
            // 但受帷幕收窄约束（"效果只能以帷幕卡为目标"对指定与随机生效；全域/范围不受限）。
            if (def.RandomTarget)
            {
                var pool = TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt);
                int take = noneAsSingle || SelectionModeRules.IsPickOne(def.SelectionMode) ? 1
                    : (def.TargetCount > 0 ? def.TargetCount : pool.Count);
                if (take >= pool.Count) return pool;
                return GameRng.PickN(pool, take);
            }

            // 选一/选多：交互选取——选择层两道过滤（2026-09-13）：①帷幕收窄（对方侧仅帷幕卡；不拦攻击；edict 豁免）
            // ②弹窗显示域（对方侧潜行隐藏；AI/无头自动取前 N 同口径）。
            candidates = TargetResolver.ExcludeUnselectable(
                TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt), context.Controller);
            if (candidates.Count == 0) return candidates;

            if (SelectionModeRules.IsPickOne(def.SelectionMode))
            {
                // 选一个（2026-10-16 晚定案）：候选 ≤ 需求（无可选余地）自动取不弹窗——
                // 落点可见性走历史记录；纯 Self 域=源卡自身同自动；AI/无头 Service 选首不变
                if (ShouldAutoTake(domain, candidates.Count, need: 1, allSemantics: false))
                    return AutoTake(candidates, 1, domain, context.Controller, allSemantics: false);
                return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = candidates,
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = context.Controller,
                    Title = "选择目标",
                });
            }

            // 选多个（Multiple/MultipleUnion）
            if (def.TargetCount == -1) // 任意（2026-09-14 并入 DynamicTargetCount）：玩家自选数量
            {
                return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = candidates,
                    MinCount = 0,
                    MaxCount = candidates.Count,
                    Chooser = context.Controller,
                    Title = "选择目标（任意数量）",
                });
            }
            int need = noneAsSingle ? 1 : (def.TargetCount > 0 ? def.TargetCount : candidates.Count);
            // 固定数量档（None 共享选一 / TargetCount>0）：候选 ≤ 需求自动取（2026-10-16 晚定案
            //——反转当日早间「一律弹选」口径，落点可见性走历史记录）；数量≤0=全取维持自动
            if (ShouldAutoTake(domain, candidates.Count, need,
                    allSemantics: def.TargetCount <= 0 && !noneAsSingle))
                return AutoTake(candidates, need, domain, context.Controller,
                    allSemantics: def.TargetCount <= 0 && !noneAsSingle);
            return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = candidates,
                MinCount = Math.Min(need, candidates.Count), // 候选不足需求数按实有收口（否则确认门永不满）
                MaxCount = need,
                Chooser = context.Controller,
                Title = "选择目标",
            });
        }

        /// <summary>固定数量选目标是否免弹自动取（2026-10-16 晚定案·反转当日早间口径）：
        /// **操作越省越好**——目标集被收窄到代码可自动执行（候选 ≤ 需求数，无任何选择余地）
        /// 即自动取，不弹确认窗；「效果作用到哪儿」的可见性交给历史记录，不通过复杂化操作。
        /// 纯 Self 域（唯一候选=源卡自身——守护类关键词效果）与全取档（数量声明 ≤0=全部）
        /// 维持自动；AI/无头 Service 选首同结果。</summary>
        private static bool ShouldAutoTake(List<int> kinds, int candidateCount, int need, bool allSemantics)
            => allSemantics || candidateCount == 0 || candidateCount <= need
               || (kinds != null && kinds.Count == 1 && kinds[0] == (int)TargetKind.Self);

        /// <summary>结算路径免弹自动取收敛口（2026-10-22 玩家反馈 B5）：ShouldAutoTake 命中处统一
        /// 经此口取前 need 个，并经 RuleHooks.OnAutoTargetTaken 抛出 (chooser, 取定目标)——UI 侧补
        /// 「已自动选定目标」反馈，消除单目标自动选定的无感知（玩家原话「发动好诡异」）。
        /// 仅在「确有选择余地被省略」（候选数 ∈ (0, need]）时抛——全体档/纯 Self 域/零候选不抛
        /// （全体效果逐次弹提示=刷屏；Self 域多见于入场即自动的关键词赋予，同为噪音）。
        /// 取值口径与原各分支 Take(need) 逐一相同，行为零变化。</summary>
        private static List<Entity> AutoTake(List<Entity> candidates, int need,
            List<int> kinds, Player chooser, bool allSemantics)
        {
            var taken = candidates.Take(need).ToList();
            bool selfDomain = kinds != null && kinds.Count == 1 && kinds[0] == (int)TargetKind.Self;
            if (!allSemantics && !selfDomain && candidates.Count > 0 && candidates.Count <= need)
                RuleHooks.OnAutoTargetTaken?.Invoke(chooser, taken);
            return taken;
        }

        /// <summary>
        /// 按原子效果配置解析目标候选列表（同步，不弹交互；headless/触发路径用）。
        /// per-target 步骤遍历用它取得候选后逐个执行。
        /// </summary>
        public static List<Entity> ResolveTargets(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect == null || context == null) return new List<Entity>();

            var (kinds, filter, count) = GetEffectiveDomain(effect);
            if (kinds.Count == 0) return new List<Entity>(); // 无目标原子

            var candidates = ResolveCandidates(kinds, filter, context);
            // TargetCount: 0=全部, <0=任意（全部）, >0=取前 N 个
            return count > 0 ? candidates.Take(count).ToList() : candidates;
        }

        /// <summary>
        /// 分支 Then 奖励的目标解析（两槽定案 2026-10-05；2026-10-07 晚定案=合法范围内弹窗选 1；
        /// 2026-10-09 每原子下沉：奖励原子声明数量档→弹选 N、随机档→帷幕收窄完整候选池种子抽取
        /// 「潜行可命中」；未声明（-2/-1）维持弹选 1）。
        /// 候选按手动选择口径两道过滤（帷幕收窄 + 弹窗显示域滤对方侧潜行/隐密）后
        /// 由 Chooser=效果控制者（自己）弹选（候选非空一律弹——2026-10-16 定案，单候选也须
        /// 玩家确认落点；纯 Self 域自指奖励自动取；AI/无头由 Service 自动选首）；
        /// 隐藏区域/无域=空列表（调用方按单次 null 目标执行——handler 自结算）。
        /// </summary>
        public static async UniTask<List<Entity>> ResolveRewardTargetsAsync(
            AtomicEffectInstance reward, EffectExecutionContext context, string title = "选择奖励目标")
        {
            if (reward == null || context == null) return new List<Entity>();
            var kinds = reward.TargetKinds ?? new List<int>();
            if (kinds.Count == 0 || TargetKindRules.AllHiddenZone(kinds))
                return new List<Entity>(); // 无目标/隐藏区自结算

            int need = reward.TargetCount > 0 ? reward.TargetCount : 1; // 未声明=弹选 1（2026-10-07 口径）
            var full = TargetResolver.ApplyTauntRestriction(
                ResolveCandidates(kinds, reward.Filter ?? "", context), context);
            if (reward.RandomTarget == 1)
                return need >= full.Count ? full : GameRng.PickN(full, need); // 随机=完整候选池抽取

            var pool = TargetResolver.ExcludeUnselectable(full, context.Controller);
            // 候选 ≤ 需求自动取（2026-10-16 晚定案——奖励落点可见性走历史记录，不弹确认）；
            // 纯 Self 域自指奖励维持自动；AI/无头 Service 选首结果不变
            if (ShouldAutoTake(kinds, pool.Count, need, allSemantics: false))
                return AutoTake(pool, need, kinds, context.Controller, allSemantics: false);
            return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = pool,
                MinCount = Math.Min(need, pool.Count),
                MaxCount = need,
                Chooser = context.Controller,
                Title = title,
                PromptSource = (Entity)context.CastCard ?? context.Source, // 奖励落点选择同报宿主（2026-10-16 lbl-toast 定案）
            });
        }

        /// <summary>
        /// 异步目标解析（分支奖励等免编排路径）：候选 > 需求数时弹选择。
        /// 动态数量已上移组合层——原子级路径恒为固定数量（表级 TargetCount）。
        /// AI / 无头 / 超时由 Service 自动取前 N。
        /// </summary>
        public static async UniTask<List<Entity>> ResolveTargetsInteractiveAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect == null || context == null) return new List<Entity>();

            var (kinds, filter, count) = GetEffectiveDomain(effect);
            if (kinds.Count == 0) return new List<Entity>(); // 无目标原子

            // 全隐藏区（2026-10-04 相同目标定案）：对方手牌/双方牌库=隐藏信息——不进交互选择，
            // 自动取首候选（与 AI/无头代选同口径）：DrawCard 等自结算原子无视目标；
            // RevealCard/AddCurse 等隐藏区消费原子拿首候选照常结算（防"从牌库选目标"弹窗）。
            if (TargetKindRules.AllHiddenZone(kinds))
            {
                var hidden = ResolveCandidates(kinds, filter, context);
                return hidden.Count > 0 ? new List<Entity> { hidden[0] } : new List<Entity>();
            }

            var candidates = ResolveCandidates(kinds, filter, context);
            if (candidates.Count == 0) return candidates;

            // 固定数量：count<=0 视为全部
            int need = count > 0 ? count : candidates.Count;
            if (candidates.Count <= need)
                return candidates.Take(need).ToList();

            // 选择层两道过滤（2026-09-13）：帷幕收窄 + 弹窗显示域（潜行隐藏）——
            // 全域/随机不经此处（随机池在组合层收窄）；edict 原子（牺牲类）豁免帷幕
            candidates = TargetResolver.ExcludeUnselectable(
                TargetResolver.ApplyTauntRestriction(candidates, context, TargetResolver.IsEdict(effect.Type)), context.Controller);
            if (candidates.Count == 0) return candidates;

            return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = candidates,
                MinCount = need,
                MaxCount = need,
                Chooser = context.Controller,
                Title = "选择目标",
                PromptSource = (Entity)context.CastCard ?? context.Source, // 免编排路径同报宿主（2026-10-16 lbl-toast 定案）
            });
        }

        /// <summary>
        /// 逐原子目标解析（2026-10-09 逐原子目标制定案，PerAtomTargets 主序列专用）：
        /// 域=该原子实例域**原样**（表默认或显式收窄——运行时不做极性侧别过滤：与旧共享口径一致，
        /// 双域原子可选两侧、跨侧命中走错边黑白发放经济；极性收窄是合成器 UI 的选择集口径，
        /// 「苏醒式自缚」类显式锁己域数据照常生效）；数量档/随机档为**每原子声明**
        ///（2026-10-09 下沉：未声明 -2/-1 回落效果级 def，效果级对未声明原子统一生效）。
        /// 声明期预选目标归属（2026-10-20 有序消费改案）：**可消耗队列**（按声明期收集序=原子执行序，
        /// 调用方执行器线程共享，逐原子消费移除）——仅交互选一/选多原子消费；defer（效果处理时选）/
        /// 全取/强制/随机原子自解析不吃队列，防同域多原子互抢预选（缄默处决=缄默+消灭同域双原子，
        /// 旧「预选∩域内全沿用」口径会双双沿用同一预选）。消费=从队首按需求量取域内项；
        /// 队内域内无可消费项 → 回落自解析。先于隐藏区早退——「点名已展示的对方手牌卡」等
        /// 信息轴联动依赖预选透传。
        /// 行为口径与组合层解析（ResolveCompositionTargetsAsync）对齐：
        /// 全隐藏区=自结算空列表（隐藏信息不进选择）；全取档/数量"全部"=候选全取不弹窗；
        /// 强制类（Mandatory）无选择窗口=自动全取；随机=帷幕收窄池内种子抽取（潜行可命中）；
        /// 选一/选多=两道过滤（帷幕收窄+显示域）后候选非空一律弹选（2026-10-16 定案——
        /// 单候选也须玩家确认落点；纯 Self 域/全取档自动取，AI/无头 Service 代选同结果）。
        /// 无域原子返回空列表（调用方按单次 null 目标自结算执行）。
        /// </summary>
        public static async UniTask<List<Entity>> ResolveAtomTargetsAsync(
            EffectDefinition def, AtomicEffectInstance atom, EffectExecutionContext context,
            List<Entity> preselectQueue = null, string title = "选择目标")
        {
            if (def == null || atom == null || context == null) return new List<Entity>();

            var kinds = atom.TargetKinds ?? new List<int>();
            if (kinds.Count == 0) return new List<Entity>(); // 无域原子：handler 自结算

            bool noWindow = def.ActivationType == EffectActivationType.Mandatory; // 强制类无目标选择窗口
            bool randomPick = atom.EffectiveRandomTarget(def);
            int effCount = atom.EffectiveTargetCount(def); // 每原子有效数量（未声明=效果级）

            // 预选归属（有序消费）：仅交互原子吃队列（豁免口径见头注）
            if (atom.DeferSelect != 1 && !randomPick && effCount != 0 && !noWindow
                && !SelectionModeRules.IsTakeAll(def.SelectionMode)
                && preselectQueue != null && preselectQueue.Count > 0)
            {
                var pool = ResolveCandidates(kinds, atom.Filter ?? "", context);
                int quota = effCount == -1 ? int.MaxValue : Math.Max(1, effCount);
                var kept = new List<Entity>();
                for (int i = 0; i < preselectQueue.Count && kept.Count < quota; i++)
                {
                    if (!pool.Contains(preselectQueue[i])) continue;
                    kept.Add(preselectQueue[i]);
                    preselectQueue.RemoveAt(i); // 已消费——后续原子不再可见（有序归属）
                    i--;
                }
                if (kept.Count > 0) return kept;
            }

            // 全隐藏区（对方手牌/双方牌库）：隐藏信息不进选择——自结算（与组合层早退口径一致）
            if (TargetKindRules.AllHiddenZone(kinds)) return new List<Entity>();

            var candidates = ResolveCandidates(kinds, atom.Filter ?? "", context);
            GameActions.Crumb($"atom-targets {def.Id}/{atom.Type} kinds={TargetKindRules.Format(kinds)} "
                + $"pre={preselectQueue?.Count ?? 0} cands={candidates.Count} mode={def.SelectionMode} cnt={atom.EffectiveTargetCount(def)}");
            if (candidates.Count == 0) return candidates;

            bool edictExempt = TargetResolver.IsEdict(atom.Type);
            bool noneAsSingle = def.SelectionMode == SelectionMode.None; // 未声明模式=按选一（与组合层同口径）

            // 全取：候选全取——潜行照常命中，不受帷幕收窄；强制类无窗口同走全取（构筑期目标明确）
            if (SelectionModeRules.IsTakeAll(def.SelectionMode) || effCount == 0 || noWindow)
                return candidates;

            // 目标随机：不弹窗，帷幕收窄后的完整候选域按种子抽取
            if (atom.EffectiveRandomTarget(def))
            {
                var pool = TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt);
                int take = noneAsSingle || SelectionModeRules.IsPickOne(def.SelectionMode) ? 1
                    : (effCount > 0 ? effCount : pool.Count);
                if (take >= pool.Count) return pool;
                return GameRng.PickN(pool, take);
            }

            // 选一/选多：交互选取——两道过滤（帷幕收窄 + 弹窗显示域；AI/无头自动取前 N 同口径）
            candidates = TargetResolver.ExcludeUnselectable(
                TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt), context.Controller);
            if (candidates.Count == 0) return candidates;

            // 结算期问选取消闸（2026-10-20 定案）：法术 cast 上下文（CastCard 非空）=类自动效果
            // 必须选目标不可取消——费已付卡已承诺无落空逃跑（可取消撤销只在声明期收集器）；
            // 生物/技能/触发效果照旧 Voluntary 口径。
            bool allowCancelDecl = context.CastCard == null
                && def.ActivationType == EffectActivationType.Voluntary;

            if (noneAsSingle || SelectionModeRules.IsPickOne(def.SelectionMode))
            {
                // 选一个（2026-10-16 晚定案，同组合层口径）：候选 ≤ 需求/纯 Self 域自动取
                //（落点可见性走历史记录）；AI/无头 Service 选首不变
                if (ShouldAutoTake(kinds, candidates.Count, need: 1, allSemantics: false))
                    return AutoTake(candidates, 1, kinds, context.Controller, allSemantics: false);
                return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = candidates,
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = context.Controller,
                    Title = title,
                    // lbl-toast 定案（2026-10-16）：宿主+效果 Id 供 UI 渲染完整效果文说明「选目标来干嘛」
                    PromptSource = (Entity)context.CastCard ?? context.Source,
                    PromptEffectId = def.Id,
                    AllowCancel = allowCancelDecl,
                });
            }

            // 选多个（Multiple/MultipleUnion）
            if (effCount == -1) // 任意：玩家自选数量
            {
                return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = candidates,
                    MinCount = 0,
                    MaxCount = candidates.Count,
                    Chooser = context.Controller,
                    Title = title + "（任意数量）",
                    PromptSource = (Entity)context.CastCard ?? context.Source,
                    PromptEffectId = def.Id,
                    AllowCancel = allowCancelDecl,
                });
            }
            int need = effCount > 0 ? effCount : candidates.Count;
            // 数量档>0=固定 N：候选 ≤ 需求自动取（2026-10-16 晚定案——反转当日「一律弹选」，
            // 落点可见性走历史记录）；effCount≤0=全取维持自动
            if (ShouldAutoTake(kinds, candidates.Count, need, allSemantics: effCount <= 0))
                return AutoTake(candidates, need, kinds, context.Controller, allSemantics: effCount <= 0);
            return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = candidates,
                MinCount = Math.Min(need, candidates.Count), // 候选不足需求数按实有收口（否则确认门永不满）
                MaxCount = need,
                Chooser = context.Controller,
                Title = title,
                PromptSource = (Entity)context.CastCard ?? context.Source,
                PromptEffectId = def.Id,
                AllowCancel = allowCancelDecl,
            });
        }

        /// <summary>
        /// 声明期目标收集器（2026-10-20 法术发动流程改案）：法术发动（上栈）前逐原子预选目标——
        /// 与 ResolveAtomTargetsAsync 结算路径同口径（候选/帷幕收窄/显示域过滤/自动取谓词单源），
        /// 差异两处：①AllowCancel=true（预选可取消=撤销发动——与结算期必须选相对）；
        /// ②只收集「结算期会弹选」的交互原子——defer（效果处理时选）/无域/引擎主干/全隐藏域/
        /// 全取/强制/随机/自动取档一律跳过（结算期照常自解析，互不干扰）。
        /// 返回序=原子执行序：结算期经预选队列有序消费对齐归属（多原子不互抢）。
        /// 返回：null=玩家取消（调用方终止发动）；空表=无需问询（直通声明）；非空=预选目标。
        /// </summary>
        public static async UniTask<List<Entity>> CollectDeclarationTargetsAsync(
            EffectDefinition def, EffectExecutionContext context, string title = "选择目标")
        {
            var none = new List<Entity>();
            if (def == null || context == null) return none;

            // header 域（PerAtomTargets=false）：组合目标共享一次问选
            if (!def.PerAtomTargets)
                return await AskCompositionDeclarationAsync(def, context, title);

            if (def.Effects == null || def.Effects.Count == 0) return none;
            var collected = new List<Entity>();
            foreach (var atom in def.Effects)
            {
                if (atom == null) continue;
                // 引擎主干=条件载体不执行（与执行器 IsEngineTrunk 同判）
                if (atom.Branch?.Settle == BranchSettleKind.Engine
                    || ComposerCatalog.EngineKindOf(atom.Type) != BranchEngineKind.None) continue;

                var kinds = atom.TargetKinds ?? new List<int>();
                if (kinds.Count == 0) continue; // 无域原子：handler 自结算
                if (atom.DeferSelect == 1) continue; // 效果处理时选——结算期问（必须选）
                if (TargetKindRules.AllHiddenZone(kinds)) continue; // 隐藏区不进选择

                bool noWindow = def.ActivationType == EffectActivationType.Mandatory;
                bool randomPick = atom.EffectiveRandomTarget(def);
                int effCount = atom.EffectiveTargetCount(def);
                if (noWindow || randomPick || effCount == 0
                    || SelectionModeRules.IsTakeAll(def.SelectionMode))
                    continue; // 强制/随机/全取：结算期自动，无问选

                var candidates = ResolveCandidates(kinds, atom.Filter ?? "", context);
                if (candidates.Count == 0) continue;
                bool edictExempt = TargetResolver.IsEdict(atom.Type);
                candidates = TargetResolver.ExcludeUnselectable(
                    TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt), context.Controller);
                if (candidates.Count == 0) continue;

                bool noneAsSingle = def.SelectionMode == SelectionMode.None;
                TargetSelectionRequest req;
                if (noneAsSingle || SelectionModeRules.IsPickOne(def.SelectionMode))
                {
                    if (ShouldAutoTake(kinds, candidates.Count, need: 1, allSemantics: false)) continue;
                    req = new TargetSelectionRequest
                    {
                        Candidates = candidates, MinCount = 1, MaxCount = 1, Title = title,
                    };
                }
                else if (effCount == -1) // 任意：玩家自选数量
                {
                    req = new TargetSelectionRequest
                    {
                        Candidates = candidates, MinCount = 0, MaxCount = candidates.Count,
                        Title = title + "（任意数量）",
                    };
                }
                else
                {
                    int need = effCount > 0 ? effCount : candidates.Count;
                    if (ShouldAutoTake(kinds, candidates.Count, need, allSemantics: effCount <= 0)) continue;
                    req = new TargetSelectionRequest
                    {
                        Candidates = candidates,
                        MinCount = Math.Min(need, candidates.Count), // 候选不足按实有收口（同结算口径）
                        MaxCount = need, Title = title,
                    };
                }
                req.Chooser = context.Controller;
                req.PromptSource = (Entity)context.CastCard ?? context.Source;
                req.PromptEffectId = def.Id;
                req.AllowCancel = true; // 预选可取消=撤销发动
                var picked = await TargetSelectionService.RequestAsync(req);
                if (picked == null || picked.Count == 0) return null; // 取消（任意档确认 0 个视同取消）
                collected.AddRange(picked);
            }
            return collected;
        }

        /// <summary>声明期组合目标问选（header 域效果，收集器子路径）：镜像
        /// ResolveCompositionTargetsAsync 的交互分支（takeall/random/自动取/隐藏区/无域不问——
        /// 结算期照常自动或早退），差异=AllowCancel=true 可取消撤销发动。
        /// null=取消；空表=无需问询；非空=预选组合目标。</summary>
        private static async UniTask<List<Entity>> AskCompositionDeclarationAsync(
            EffectDefinition def, EffectExecutionContext context, string title)
        {
            var domain = def.TargetDomain;
            if (def.ChoiceDomains != null && context.ModeIndex >= 0
                && context.ModeIndex < def.ChoiceDomains.Length
                && def.ChoiceDomains[context.ModeIndex] != null
                && def.ChoiceDomains[context.ModeIndex].Count > 0)
                domain = def.ChoiceDomains[context.ModeIndex];
            if (domain == null || domain.Count == 0
                || (def.SelectionMode == SelectionMode.None && TargetKindRules.AllHiddenZone(domain))
                || SelectionModeRules.IsTakeAll(def.SelectionMode)
                || def.RandomTarget)
                return new List<Entity>();

            var candidates = ResolveCandidates(domain, def.TargetFilter, context);
            if (candidates.Count == 0) return new List<Entity>();

            bool edictExempt = def.Effects != null
                && def.Effects.Any(a => a != null && TargetResolver.IsEdict(a.Type));
            candidates = TargetResolver.ExcludeUnselectable(
                TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt), context.Controller);
            if (candidates.Count == 0) return new List<Entity>();

            bool noneAsSingle = def.SelectionMode == SelectionMode.None; // None+可见域：按选一共享
            TargetSelectionRequest Build(int min, int max, string t)
                => new TargetSelectionRequest
                {
                    Candidates = candidates, MinCount = min, MaxCount = max,
                    Chooser = context.Controller, Title = t,
                    PromptSource = (Entity)context.CastCard ?? context.Source,
                    PromptEffectId = def.Id,
                    AllowCancel = true, // 预选可取消=撤销发动
                };

            if (noneAsSingle || SelectionModeRules.IsPickOne(def.SelectionMode))
            {
                if (ShouldAutoTake(domain, candidates.Count, need: 1, allSemantics: false))
                    return new List<Entity>();
                var picked = await TargetSelectionService.RequestAsync(Build(1, 1, title));
                return picked != null && picked.Count > 0 ? picked : null;
            }
            if (def.TargetCount == -1) // 任意：玩家自选数量
            {
                var picked = await TargetSelectionService.RequestAsync(Build(0, candidates.Count, title + "（任意数量）"));
                return picked != null && picked.Count > 0 ? picked : null;
            }
            int need = noneAsSingle ? 1 : (def.TargetCount > 0 ? def.TargetCount : candidates.Count);
            if (ShouldAutoTake(domain, candidates.Count, need,
                    allSemantics: def.TargetCount <= 0 && !noneAsSingle))
                return new List<Entity>();
            var pickedN = await TargetSelectionService.RequestAsync(
                Build(Math.Min(need, candidates.Count), need, title));
            return pickedN != null && pickedN.Count > 0 ? pickedN : null;
        }

        /// <summary>按域+filter 解析候选（域模型统一口径；TargetDomainService 预检复用）。</summary>
        public static List<Entity> ResolveCandidates(List<int> kinds, string filter, EffectExecutionContext context)
        {
            var resolver = new TargetResolver(context.ZoneManager);
            var candidates = resolver.GetCandidates(kinds, filter, context);
            if (!string.IsNullOrEmpty(filter))
            {
                var filters = resolver.ParseFilters(filter);
                candidates = resolver.ApplyFilters(candidates, filters, context);
            }
            return candidates;
        }


        /// <summary>
        /// 获取所有已注册的效果类型
        /// </summary>
        public static IEnumerable<AtomicEffectType> GetRegisteredTypes()
        {
            return _handlers.Keys;
        }

        /// <summary>
        /// 获取已注册处理器数量
        /// </summary>
        public static int HandlerCount => _handlers.Count;

        /// <summary>
        /// 检查效果类型是否已注册
        /// </summary>
        public static bool IsRegistered(AtomicEffectType type)
        {
            return _handlers.ContainsKey(type);
        }
    }
}
