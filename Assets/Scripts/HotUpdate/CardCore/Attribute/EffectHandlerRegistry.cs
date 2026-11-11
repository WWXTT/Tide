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

            int originalValue = effect.Value;
            int bonus = DeathTollBonusOf(effect, context);
            if (bonus > 0) effect.Value += bonus;
            try
            {
                handler.Execute(effect, context);
                return true;
            }
            finally
            {
                effect.Value = originalValue;
            }
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
            // TargetsResolvedBySpec（步3 换轨）=执行器已按 spec 计划解析封口——空 Targets 也不再回落
            //（分段原子受体域空=空转，防 kinds 旧口径兜底误解析出参照候选）。
            bool hasPreselectedTargets = context.Targets != null && context.Targets.Count > 0;
            if (!hasPreselectedTargets && !context.TargetsResolvedBySpec)
                context.Targets = await ResolveTargetsInteractiveAsync(effect, context);

            if (!PrepareForExecution(effect, context, out var handler, skipTargetResolution: true))
                return false;

            // 铁律守卫作用域（2026-11-08 §1.1）：handler 执行期发起的选择请求须带已批准 SpecKey，
            // 否则 TargetSelectionService 内 GuardHandlerScope 报 Error（先叫后清零）。
            TargetSelectionService.BeginHandlerScope(context);
            int originalValue = effect.Value;
            int bonus = DeathTollBonusOf(effect, context);
            if (bonus > 0) effect.Value += bonus;
            try
            {
                await handler.ExecuteAsync(effect, context);
            }
            finally
            {
                effect.Value = originalValue;
                TargetSelectionService.EndHandlerScope();
            }
            return true;
        }

        /// <summary>死亡计数增幅（2026-11-10 用户定案）：宿主卡手牌累计 DeathTollCounter；
        /// 该卡之后所有 {value} 原子都吃同一增幅，直到离场（计数器由 CounterRules 换区口径保证）。
        /// 增幅 = min(9, floor(deaths / 该原子每 +1 value 的费))；1 死亡=1 费，不同原子单价不同；普通计数上限6、增幅9。
        /// 只对描述含 {value} 的行生效；动态 valueExpr 不在此口（保持原值）。</summary>
        private static int DeathTollBonusOf(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context == null || context.IsCostPayload) return 0;
            var host = context.CastCard ?? context.Source as Card;
            if (host == null) return 0;
            int deaths = host.GetCounterCount(Attribute.CounterRules.DeathTollCounter);
            if (deaths <= 0) return 0;
            var cfg = AtomicEffectTable.GetByHashId(effect.RowHashId);
            if (cfg == null || string.IsNullOrEmpty(cfg.Description) || !cfg.Description.Contains("{value}"))
                return 0;
            if (!string.IsNullOrEmpty(effect.ValueExpr)) return 0;
            float step = CostDerivationService.ValueStepFee(effect, context.ResolvingEffect);
            if (step <= 0.0001f) return 0;
            return Math.Min(9, Math.Max(0, (int)Math.Floor(deaths / step)));
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
                // 2026-11-05 诊断补全：旧行只有效果类型，无法定位是哪张卡的哪一原子
                //（曾出现单次回归 379 条同名告警却查不到来源）。带源卡名 + 目标数。
                var srcName = (context.Source as IHasName)?.CardName
                              ?? (context.CastCard as IHasName)?.CardName
                              ?? context.Source?.ToString() ?? "?";
                TideLog.Warn($"效果无法执行: {effect.Type}（源={srcName}，目标={context.Targets?.Count ?? 0}）");
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

        /// <summary>效果首个作用原子类型（AI 候选排序提示，2026-11-01；无原子=null）。
        /// 组合层共用域多原子时取首原子——策略只按"这类目标选谁"排序，取首足够。</summary>
        private static AtomicEffectType? PromptAtomOf(EffectDefinition def)
        {
            if (def?.Effects == null) return null;
            foreach (var a in def.Effects)
                if (a != null) return a.Type;
            return null;
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

            // per-mode 域（ChoiceDomains）已随 2026-10-31 P12 抉择退役恒 null——统一 def.TargetDomain
            var domain = def.TargetDomain;
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
                    PromptAtom = PromptAtomOf(def), // AI 候选排序（2026-11-01）
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
                    PromptAtom = PromptAtomOf(def), // AI 候选排序（2026-11-01）
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
                PromptAtom = PromptAtomOf(def), // AI 候选排序（2026-11-01）
            });
        }

        /// <summary>固定数量选目标是否免弹自动取（2026-10-16 晚定案·反转当日早间口径）：
        /// **操作越省越好**——目标集被收窄到代码可自动执行（候选 ≤ 需求数，无任何选择余地）
        /// 即自动取，不弹确认窗；「效果作用到哪儿」的可见性交给历史记录，不通过复杂化操作。
        /// 纯 Self 域（唯一候选=源卡自身——守护类关键词效果）与全取档（数量声明 ≤0=全部）
        /// 维持自动；AI/无头 Service 选首同结果。
        /// 2026-11-02 提 public：LAE/SimpleAI 镜像「候选≤需求→引擎自动取不占选择权」口径（单源防漂移）。</summary>
        public static bool ShouldAutoTake(List<int> kinds, int candidateCount, int need, bool allSemantics)
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

            var candidates = ResolveCandidates(kinds, filter, context, effect.Axis);
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
                ResolveCandidates(kinds, reward.Filter ?? "", context, reward.Axis), context);
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
                PromptAtom = reward.Type, // AI 候选排序（2026-11-01）
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
                var hidden = ResolveCandidates(kinds, filter, context, effect.Axis);
                return hidden.Count > 0 ? new List<Entity> { hidden[0] } : new List<Entity>();
            }

            var candidates = ResolveCandidates(kinds, filter, context, effect.Axis);
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
                PromptAtom = effect.Type, // AI 候选排序（2026-11-01）
            });
        }

        /// <summary>
        /// 逐原子目标解析（2026-10-09 逐原子目标制定案，PerAtomTargets 主序列专用）：
        /// 域=该原子实例域**原样**（表默认或显式收窄——运行时不做极性侧别过滤：与旧共享口径一致，
        /// 双域原子可选两侧、跨侧命中走错边黑白发放经济；极性收窄是合成器 UI 的选择集口径，
        /// 「自缚式」（负面效果锁己域）数据照常生效）；数量档/随机档为**每原子声明**
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

            // 分段原子（步4）：域源切 RefDomain——kinds=ReferenceKinds、axis=ReferenceAxis、
            // 候选过滤只做可见性（ExcludeUnselectable，无帷幕/守护，§2.3）；受体侧由执行器
            // ResolveSegmentedTargetsAsync 从行 Domain 解析（不经本口）。
            bool refAtom = atom.ReferenceAxis != null;
            var kinds = (refAtom ? atom.ReferenceKinds : atom.TargetKinds) ?? new List<int>();
            if (kinds.Count == 0) return new List<Entity>(); // 无域原子：handler 自结算
            var resolveAxis = refAtom ? atom.ReferenceAxis : atom.Axis;

            bool noWindow = def.ActivationType == EffectActivationType.Mandatory; // 强制类无目标选择窗口
            bool randomPick = atom.EffectiveRandomTarget(def);
            int effCount = atom.EffectiveTargetCount(def); // 每原子有效数量（未声明=效果级）

            // 预选归属（有序消费）：仅交互原子吃队列（豁免口径见头注）
            if (atom.DeferSelect != 1 && !randomPick && effCount != 0 && !noWindow
                && !SelectionModeRules.IsTakeAll(def.SelectionMode)
                && preselectQueue != null && preselectQueue.Count > 0)
            {
                var pool = ResolveCandidates(kinds, atom.Filter ?? "", context, resolveAxis);
                int quota = effCount == -1 ? int.MaxValue : Math.Max(1, effCount);
                var kept = new List<Entity>();
                for (int i = 0; i < preselectQueue.Count && kept.Count < quota; i++)
                {
                    if (!pool.Contains(preselectQueue[i])) continue;
                    kept.Add(preselectQueue[i]);
                    preselectQueue.RemoveAt(i); // 已消费——后续原子不再可见（有序归属）
                    i--;
                }
                if (kept.Count > 0)
                {
                    // 预选欠额补足（2026-11-02 洗回改版）：AI/无头按**效果级**数量预选
                    //（SimpleAI 读 header count），而每原子数量档 effCount 压过缺省——
                    // 人类路径声明期收集器本就按 effCount 问满，此口把未填满的 quota 按
                    // 选择层同口径过滤后的候选序补足（跨双方墓地选 N 等逐原子多目标卡）。
                    if (quota != int.MaxValue && kept.Count < quota)
                    {
                        var fillPool = refAtom ? pool
                            : TargetResolver.ApplyTauntRestriction(pool, context, TargetResolver.IsEdict(atom.Type));
                        var fill = TargetResolver.ExcludeUnselectable(fillPool, context.Controller);
                        foreach (var c in fill)
                        {
                            if (kept.Count >= quota) break;
                            if (!kept.Contains(c)) kept.Add(c);
                        }
                    }
                    return kept;
                }
            }

            // 全隐藏区（对方手牌/双方牌库）：隐藏信息不进选择——自结算（与组合层早退口径一致）
            if (TargetKindRules.AllHiddenZone(kinds)) return new List<Entity>();

            var candidates = ResolveCandidates(kinds, atom.Filter ?? "", context, resolveAxis);
            GameActions.Crumb($"atom-targets {def.Id}/{atom.Type} kinds={TargetKindRules.Format(kinds)} "
                + $"pre={preselectQueue?.Count ?? 0} cands={candidates.Count} mode={def.SelectionMode} cnt={atom.EffectiveTargetCount(def)}");
            if (candidates.Count == 0) return candidates;

            bool edictExempt = TargetResolver.IsEdict(atom.Type);
            bool noneAsSingle = def.SelectionMode == SelectionMode.None; // 未声明模式=按选一（与组合层同口径）

            // 全取：候选全取——潜行照常命中，不受帷幕收窄；强制类无窗口同走全取（构筑期目标明确）
            // 角色默认不参与全取（2026-11-06 用户定案：角色只有一个——全部档全取只吃面积域目标
            //（战场生物/结界等）；候选恰全为角色时保留，防角色专属域原子全取空转）。
            if (SelectionModeRules.IsTakeAll(def.SelectionMode) || effCount == 0 || noWindow)
            {
                var nonRole = new List<Entity>();
                foreach (var c in candidates) if (!(c is Player)) nonRole.Add(c);
                return nonRole.Count > 0 || candidates.Count == 0 ? nonRole : candidates;
            }

            // 目标随机：不弹窗，帷幕收窄后的完整候选域按种子抽取
            //（2026-11-02 多目标定案：每原子数量档 effCount>0 优先——带 value 的原子设 N 个随机目标即抽 N）
            if (atom.EffectiveRandomTarget(def))
            {
                var pool = refAtom ? candidates
                    : TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt);
                int take = effCount > 0 ? effCount
                    : (noneAsSingle || SelectionModeRules.IsPickOne(def.SelectionMode) ? 1 : pool.Count);
                if (take >= pool.Count) return pool;
                return GameRng.PickN(pool, take);
            }

            // 选一/选多：交互选取——两道过滤（帷幕收窄 + 弹窗显示域；AI/无头自动取前 N 同口径）
            //（分段原子只做可见性过滤——参照无帷幕/守护语义，§2.3）
            candidates = refAtom
                ? TargetResolver.ExcludeUnselectable(candidates, context.Controller)
                : TargetResolver.ExcludeUnselectable(
                    TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt), context.Controller);
            if (candidates.Count == 0) return candidates;

            // 结算期问选取消闸（2026-10-20 定案）：法术 cast 上下文（CastCard 非空）=类自动效果
            // 必须选目标不可取消——费已付卡已承诺无落空逃跑（可取消撤销只在声明期收集器）；
            // 生物/技能/触发效果照旧 Voluntary 口径。
            bool allowCancelDecl = context.CastCard == null
                && def.ActivationType == EffectActivationType.Voluntary;

            // 选一个（2026-10-16 晚定案，同组合层口径）：候选 ≤ 需求/纯 Self 域自动取
            //（落点可见性走历史记录）；AI/无头 Service 选首不变。
            // 2026-11-02 多目标定案：**每原子数量档 effCount>1 压过选一模式提示**（None/Single 档
            // 只代表「未声明数量的缺省=1」）——数量声明 2/3 的原子走下方选多，每目标独立结算一份；
            // 候选不足按实有收口（MinCount=候选数）。
            if ((noneAsSingle || SelectionModeRules.IsPickOne(def.SelectionMode)) && effCount <= 1)
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
                    PromptAtom = atom.Type, // AI 候选排序（2026-11-01）
                    SpecRole = refAtom ? TargetSpecRole.Reference : TargetSpecRole.Target, // 步5 AI 分派
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
                    PromptAtom = atom.Type, // AI 候选排序（2026-11-01）
                    SpecRole = refAtom ? TargetSpecRole.Reference : TargetSpecRole.Target, // 步5 AI 分派
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
                PromptAtom = atom.Type, // AI 候选排序（2026-11-01）
                SpecRole = refAtom ? TargetSpecRole.Reference : TargetSpecRole.Target, // 步5 AI 分派
                AllowCancel = allowCancelDecl,
            });
        }

        /// <summary>逐原子交互选择规格（2026-11-02 LAE 动作空间 v8 单源）：收集"结算期会弹选"的
        /// 原子清单——声明期收集器（人类弹选）、LAE（神经动作行展开）、SimpleAI（预选排序）三方共用，
        /// 跳过条件与结算路径 ResolveAtomTargetsAsync 的自动档逐条同判：引擎主干/无域/defer/全隐藏区/
        /// 强制类/随机/effCount=0（全取）/SelectionMode 全取档/候选空（这些原子结算期自动收口，不占选择权）。
        /// 候选已完成两道过滤（帷幕收窄+显示域，同结算路径交互分支口径）；返回序=原子执行序
        /// （预选队列有序消费对齐归属）。</summary>
        public static List<AtomTargetSpec> CollectAtomTargetSpecs(EffectDefinition def, EffectExecutionContext context)
        {
            var none = new List<AtomTargetSpec>();
            if (def == null || context == null || !def.PerAtomTargets) return none;
            if (def.Effects == null || def.Effects.Count == 0) return none;
            bool noWindow = def.ActivationType == EffectActivationType.Mandatory;
            if (noWindow) return none; // 强制类无选择窗口——整效果自动全取

            var specs = new List<AtomTargetSpec>();
            for (int atomIndex = 0; atomIndex < def.Effects.Count; atomIndex++)
            {
                var atom = def.Effects[atomIndex];
                if (atom == null) continue;
                // 引擎主干=条件载体不执行（与执行器 IsEngineTrunk 同判）
                if (atom.Branch?.Settle == BranchSettleKind.Engine
                    || ComposerCatalog.EngineKindOf(atom.Type) != BranchEngineKind.None) continue;

                // 分段原子（步4）：域源切 RefDomain——kinds=ReferenceKinds、axis=ReferenceAxis、
                // 候选只做可见性过滤（无帷幕/守护——参照不是被施加效果的对象，§2.3）。
                bool refAtom = atom.ReferenceAxis != null;
                var kinds = (refAtom ? atom.ReferenceKinds : atom.TargetKinds) ?? new List<int>();
                if (kinds.Count == 0) continue; // 无域原子：handler 自结算
                if (atom.DeferSelect == 1) continue; // 效果处理时选——结算期问（必须选）
                if (TargetKindRules.AllHiddenZone(kinds)) continue; // 隐藏区不进选择

                if (atom.EffectiveRandomTarget(def)) continue; // 随机：结算期自动抽取
                int effCount = atom.EffectiveTargetCount(def);
                if (effCount == 0 || SelectionModeRules.IsTakeAll(def.SelectionMode))
                    continue; // 全取：结算期自动全取

                var candidates = ResolveCandidates(kinds, atom.Filter ?? "", context,
                    refAtom ? atom.ReferenceAxis : atom.Axis);
                if (candidates.Count == 0) continue;
                if (refAtom)
                    candidates = TargetResolver.ExcludeUnselectable(candidates, context.Controller);
                else
                {
                    bool edictExempt = TargetResolver.IsEdict(atom.Type);
                    candidates = TargetResolver.ExcludeUnselectable(
                        TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt), context.Controller);
                }
                if (candidates.Count == 0) continue;

                specs.Add(new AtomTargetSpec
                {
                    Atom = atom,
                    Kinds = kinds,
                    EffCount = effCount,
                    Candidates = candidates,
                    // 任意档（-1）必弹选；固定 N 档 allSemantics=false（effCount>0，同声明期旧判）
                    AutoTake = effCount > 0 && ShouldAutoTake(kinds, candidates.Count, effCount, false),
                    // spec 计划元数据（步4 起随域切换填齐；CollectSpecPlan 同口径）
                    SpecKey = $"atom:{atomIndex}/spec:0/role:{(refAtom ? "reference" : "target")}",
                    Role = refAtom ? TargetSpecRole.Reference : TargetSpecRole.Target,
                    AtomIndex = atomIndex,
                    Axis = refAtom ? atom.ReferenceAxis : atom.Axis,
                    RefPick = atom.RefPick,
                    RefRequired = atom.RefRequired,
                    AiMode = AiPickMode.Enumerate,
                });
            }
            return specs;
        }

        /// <summary>参照候选解析（步4 §2.3 单源）：RefDomain 轴取候选 + **仅可见性过滤**
        ///（ExcludeUnselectable 潜行/隐藏/对方手牌隐藏语义）——不做可指性过滤（帷幕/守护/edict
        /// 豁免），参照不进 context.Targets、不触发 OnTargeted 等被指定事件。</summary>
        public static List<Entity> ResolveReferenceCandidates(AtomicEffectInstance atom, EffectExecutionContext context)
        {
            if (atom == null || atom.ReferenceAxis == null || context == null) return new List<Entity>();
            return TargetResolver.ExcludeUnselectable(
                ResolveCandidates(atom.ReferenceKinds ?? new List<int>(), "", context, atom.ReferenceAxis),
                context.Controller);
        }

        /// <summary>spec 计划收集（2026-11-08 目标选择重构 §3.1/§3.5，步5 起唯一入口）：
        /// ① Reference spec 前置（RefDomain 分段原子，候选=ResolveReferenceCandidates 可见性口径，
        ///    AiMode=Enumerate——参照是唯一有意义的决策维度）；
        /// ② Target spec：非分段交互原子按**同受体域分组共享**（沉默+消灭同域→单 spec 单窗同目标；
        ///    弹回+回收跨域→各自 spec——跨域并列卡的分段选目标语义保持）。计划原文的「全体原子域
        ///    交集」按此分组口径落地（全体交集会把跨域并列卡判空，与既有断言冲突）；
        /// ③ 分段受体 spec 尾置（AiMode=Auto——LAE 不展开，结算期引擎/AiOrder 代选：打脸/取首）。
        /// 顺序固定 Reference→Target（人类窗口序）；SpecKey 全局唯一（§3.3）。</summary>
        public static List<AtomTargetSpec> CollectSpecPlan(EffectDefinition def, EffectExecutionContext context)
        {
            var specs = new List<AtomTargetSpec>();
            if (def == null || def.Effects == null || context == null) return specs;
            if (def.ActivationType == EffectActivationType.Mandatory) return specs; // 强制类无选择窗口

            var perAtom = CollectAtomTargetSpecs(def, context);

            // ① Reference specs（RefDomain 分段原子——CollectAtomTargetSpecs 已按 RefDomain 域源产出）
            specs.AddRange(perAtom.Where(s => s.Role == TargetSpecRole.Reference));

            // ② Target specs：同域分组共享（分组键=kinds 序列+filter+effCount+轴短语——受体域等价）
            var targets = perAtom.Where(s => s.Role != TargetSpecRole.Reference).ToList();
            var groups = new List<List<AtomTargetSpec>>();
            foreach (var s in targets)
            {
                var g = groups.FirstOrDefault(x => SameTargetGroup(x[0], s));
                if (g != null) g.Add(s);
                else groups.Add(new List<AtomTargetSpec> { s });
            }
            foreach (var g in groups)
            {
                var first = g[0];
                var shared = g.Count > 1;
                specs.Add(new AtomTargetSpec
                {
                    SpecKey = shared
                        ? $"effect/spec:{specs.Count(s => s.Role == TargetSpecRole.Target)}/role:target"
                        : first.SpecKey,
                    Role = TargetSpecRole.Target,
                    AtomIndex = shared ? -1 : first.AtomIndex,
                    AtomIndices = shared ? g.Select(x => x.AtomIndex).ToList() : null,
                    // 组内首原子=偏好锚（SimpleAI 有害/有益排序按原子 Type 判定——共享组不丢偏好）
                    Atom = first.Atom,
                    Axis = first.Axis,
                    Kinds = first.Kinds,
                    EffCount = first.EffCount,
                    Candidates = first.Candidates,
                    AutoTake = first.AutoTake,
                    Random = first.Random,
                    Defer = first.Defer,
                    AiMode = AiPickMode.Enumerate,
                });
            }

            // ③ 分段受体 spec（AiMode=Auto——候选结算期解析，参照就位后 PromptValue=参照攻/命）
            for (int i = 0; i < def.Effects.Count; i++)
            {
                var atom = def.Effects[i];
                if (atom?.ReferenceAxis == null) continue;
                var row = AtomicEffectTable.GetByHashId(atom.RowHashId);
                TargetAxis victimAxis = null;
                List<int> victimKinds = null;
                if (row != null && TargetAxisRules.TryParseDomain(row.Domain, out var va))
                {
                    victimAxis = va;
                    victimKinds = row.GetTargetKindList();
                }
                specs.Add(new AtomTargetSpec
                {
                    SpecKey = $"atom:{i}/spec:1/role:target",
                    Role = TargetSpecRole.Target,
                    AtomIndex = i,
                    Atom = atom,
                    Axis = victimAxis,
                    Kinds = victimKinds,
                    EffCount = 1,
                    Candidates = null, // 结算期解析（依赖参照就位）
                    AutoTake = false,
                    RefRequired = true,
                    AiMode = AiPickMode.Auto,
                });
            }
            return specs;
        }

        /// <summary>同域分组判定（② 的分组键）：kinds 序列 + filter + effCount + 轴短语等价
        ///（随机/defer 档不并组——结算期自解析语义不同）。</summary>
        private static bool SameTargetGroup(AtomTargetSpec a, AtomTargetSpec b)
        {
            if (a.Random || b.Random || a.Defer || b.Defer) return false;
            if (a.EffCount != b.EffCount || a.AutoTake != b.AutoTake) return false;
            if ((a.Kinds ?? new List<int>()).SequenceEqual(b.Kinds ?? new List<int>()) == false) return false;
            if ((a.Atom?.Filter ?? "") != (b.Atom?.Filter ?? "")) return false;
            return (a.Axis?.Zh ?? "") == (b.Axis?.Zh ?? "");
        }

        /// <summary>原子下标 → 所属 spec（步5 执行器/LAE 消费口）：同域共享组按 AtomIndices 命中；
        /// 分段原子命中其 reference spec（受体 spec 由执行器在参照就位后单独解析）。</summary>
        public static AtomTargetSpec SpecOfAtom(List<AtomTargetSpec> plan, int atomIndex)
        {
            if (plan == null) return null;
            foreach (var s in plan)
            {
                if (s.Role == TargetSpecRole.Reference && s.AtomIndex == atomIndex) return s;
                if (s.Role == TargetSpecRole.Target)
                {
                    if (s.AtomIndices != null && s.AtomIndices.Contains(atomIndex)) return s;
                    if (s.AtomIndices == null && s.AtomIndex == atomIndex) return s;
                }
            }
            return null;
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

            // 逐原子规格 → spec 计划单源（步5 §3.2）：Reference（RefPick=Cast）先问、Target spec 后问；
            // 同域共享组只问**一次**（沉默+消灭同域并列→单窗同目标）；分段受体 Auto spec 不问
            //（结算期引擎/策略代选）。窗口序=计划序（参照先/目标后）；跳过条件/候选口径与
            // CollectAtomTargetSpecs 同源（此处只剩弹选问询）。
            var plan = CollectSpecPlan(def, context);
            var askable = plan
                .Where(s => s.Role == TargetSpecRole.Reference ? s.RefPick == RefPick.Cast : !s.AutoTake)
                .Where(s => s.Candidates != null && s.Candidates.Count > 0)
                .ToList();
            if (askable.Count == 0) return none;
            var collected = new List<Entity>();
            foreach (var spec in askable)
            {
                TargetSelectionRequest req;
                // 2026-11-02 多目标定案：每原子数量档 effCount>1 压过选一模式提示——声明期问选 N 个
                //（与结算路径 ResolveAtomTargetsAsync 同口径，预选队列 quota 本就按 effCount）；
                // 候选不足按实有收口（MinCount=候选数）。
                if (spec.EffCount <= 1)
                {
                    req = new TargetSelectionRequest
                    {
                        Candidates = spec.Candidates, MinCount = 1, MaxCount = 1, Title = title,
                    };
                }
                else if (spec.EffCount == -1) // 任意：玩家自选数量
                {
                    req = new TargetSelectionRequest
                    {
                        Candidates = spec.Candidates, MinCount = 0, MaxCount = spec.Candidates.Count,
                        Title = title + "（任意数量）",
                    };
                }
                else
                {
                    req = new TargetSelectionRequest
                    {
                        Candidates = spec.Candidates,
                        MinCount = Math.Min(spec.EffCount, spec.Candidates.Count), // 候选不足按实有收口（同结算口径）
                        MaxCount = spec.EffCount, Title = title,
                    };
                }
                req.Chooser = context.Controller;
                req.PromptSource = (Entity)context.CastCard ?? context.Source;
                req.PromptEffectId = def.Id;
                req.PromptAtom = spec.Atom?.Type; // AI 候选排序（2026-11-01）
                req.SpecKey = spec.SpecKey;       // §3.3 显式关联键（网络/重放对齐）
                req.SpecRole = spec.Role;         // AI 按角色分派（步5）
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
            // ChoiceDomains 已随 2026-10-31 P12 退役恒 null——统一 def.TargetDomain
            var domain = def.TargetDomain;
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
                    PromptAtom = PromptAtomOf(def), // AI 候选排序（2026-11-01）
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

        /// <summary>按域+filter 解析候选（域模型统一口径；TargetDomainService 预检复用）。
        /// P1-b 两轴优先（2026-11-05 运行时切轴）：axis 非空（converter 从行 Region/Category 构建）
        /// → TargetAxisResolver；null=回落旧 kinds/Domain 口径（kinds 恒为 converter 解析缓存，本签名可用）。</summary>
        public static List<Entity> ResolveCandidates(List<int> kinds, string filter, EffectExecutionContext context,
            TargetAxis axis = null)
        {
            if (axis != null) return TargetAxisResolver.GetCandidates(axis, context);
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

    /// <summary>spec 角色（2026-11-08 目标选择重构 §1.2）：Target=受体（进 context.Targets，
    /// 走可指性过滤+被指定事件）；Reference=参照（进 context.Refs，只做可见性过滤）。</summary>
    public enum TargetSpecRole
    {
        Target,
        Reference,
    }

    /// <summary>参照收集时机（§2.2）：Cast=声明期选（取消参照=撤销发动）；Resolve=结算期问；
    /// Auto=不弹窗、引擎按确定性规则取（无头模拟/回放稳定）。</summary>
    public enum RefPick
    {
        Cast,
        Resolve,
        Auto,
    }

    /// <summary>AI 动作空间模式（§3.5，不影响人类弹窗次数）：Enumerate=LAE 为该 spec 逐候选
    ///（或组合）生成动作行；Auto=LAE 只生成一行，由引擎/AiOrder 代选（Auto≠取首）。</summary>
    public enum AiPickMode
    {
        Enumerate,
        Auto,
    }

    /// <summary>逐原子交互选择规格（CollectAtomTargetSpecs 产出，2026-11-02 LAE 动作空间 v8）：
    /// 一个"结算期会弹选"原子的问选参数+已过滤候选。声明期收集器（人类弹选）、LAE（动作行展开）、
    /// SimpleAI（预选排序）三方单源消费。</summary>
    public sealed class AtomTargetSpec
    {
        /// <summary>来源原子实例（SimpleAI 有害/有益偏好按原子 Type 判定用；LAE 不消费）。</summary>
        public AtomicEffectInstance Atom;
        /// <summary>原子有效域（atom.TargetKinds）。</summary>
        public List<int> Kinds;
        /// <summary>每原子有效数量（EffectiveTargetCount）：>0=选 N（1=选一）、-1=任意数量。</summary>
        public int EffCount;
        /// <summary>两道过滤（帷幕收窄+显示域）后的交互候选（引擎 ResolveCandidates 返回序）。</summary>
        public List<Entity> Candidates;
        /// <summary>ShouldAutoTake 命中（候选≤需求/纯 Self 域）：结算期引擎自动取前 N 个——
        /// 不占选择权，AI/玩家均无需预选。</summary>
        public bool AutoTake;

        // ---- 2026-11-08 目标选择重构 §1.2 新增（spec 计划元数据；步1 无消费方，行为零变化）----
        /// <summary>显式关联键（§3.3）：atom:{atomIndex}/spec:{specIndex}/role:{role}；
        /// effect-level 目标 spec 用 effect/spec:{specIndex}/role:target。网络/重放/触发重入/
        /// AI 代选按此键对齐——禁止用「本效果第几次弹窗」隐含序号。</summary>
        public string SpecKey;
        /// <summary>spec 角色：Target=受体；Reference=参照。</summary>
        public TargetSpecRole Role;
        /// <summary>主序列原子下标；effect-level target spec 为 -1。</summary>
        public int AtomIndex = -1;
        /// <summary>受体/参照轴（RefDomain/Domain 产轴后由 spec 计划填充；null=旧 kinds 路径）。</summary>
        public TargetAxis Axis;
        /// <summary>Reference 专用：参照收集时机（Cast/Resolve/Auto）。</summary>
        public RefPick RefPick;
        /// <summary>Reference 专用：参照候选为空是否空转（v1 恒 true；false 留扩展位）。</summary>
        public bool RefRequired = true;
        /// <summary>AI 动作空间模式（Enumerate/Auto；默认 Enumerate）。</summary>
        public AiPickMode AiMode;
        /// <summary>随机抽取档（spec 级，沿用原子 rand 口径）。</summary>
        public bool Random;
        /// <summary>延迟选择档（spec 级，沿用原子 defer 口径）。</summary>
        public bool Defer;
        /// <summary>共享组成员（步5 同域分组）：同受体域并列原子共享本 spec 一次解析/一次窗口；
        /// null/单元素=单原子 spec。执行器按原子下标 → spec 查找用。</summary>
        public List<int> AtomIndices;
    }
}
