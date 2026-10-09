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
        /// 选一（Single/SingleUnion）：need=1（域={Self} 时=源卡自身，候选≤1 自动取不弹选）；
        /// 选多（Multiple/MultipleUnion）：TargetCount/DynamicTargetCount 管数量（候选>需求时弹选）；
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
            // 按选一共享解析一次：并列全体原子共享同一份选中目标（候选≤1 自动取；RandomTarget 随机 1），
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

            // 全取（Whole/WholeUnion）：候选全取——扰魔/潜行照常命中（范围波及），不受帷幕收窄。
            if (SelectionModeRules.IsTakeAll(def.SelectionMode))
                return candidates;

            // 目标随机（RandomTarget 正交标志，2026-09-16 自 SelectionMode 移出）：不弹窗，随机种子自动抽取；
            // 从完整候选域抽（对方侧扰魔/潜行可被随机命中——绕过选择），
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
            // ②弹窗显示域（对方侧扰魔/潜行隐藏；AI/无头自动取前 N 同口径）。
            candidates = TargetResolver.ExcludeUnselectable(
                TargetResolver.ApplyTauntRestriction(candidates, context, edictExempt), context.Controller);
            if (candidates.Count == 0) return candidates;

            if (SelectionModeRules.IsPickOne(def.SelectionMode))
            {
                // 选一个：候选≤1 自动取（域={Self} 的关键词效果即此路——唯一候选=源卡自身）
                if (candidates.Count <= 1) return candidates.Take(1).ToList();
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
            if (candidates.Count <= need)
                return candidates.Take(need).ToList();
            return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = candidates,
                MinCount = need,
                MaxCount = need,
                Chooser = context.Controller,
                Title = "选择目标",
            });
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
        /// 「扰魔/潜行可命中」；未声明（-2/-1）维持弹选 1）。
        /// 候选按手动选择口径两道过滤（帷幕收窄 + 弹窗显示域滤对方侧扰魔/潜行/隐密）后
        /// 由 Chooser=效果控制者（自己）弹选；候选≤需求自动取不弹（AI/无头由 Service 自动选首）；
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
            if (pool.Count <= need) return pool.Take(need).ToList();
            return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = pool,
                MinCount = need,
                MaxCount = need,
                Chooser = context.Controller,
                Title = title,
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

            // 选择层两道过滤（2026-09-13）：帷幕收窄 + 弹窗显示域（扰魔/潜行隐藏）——
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
            });
        }

        /// <summary>
        /// 逐原子目标解析（2026-10-09 逐原子目标制定案，PerAtomTargets 主序列专用）：
        /// 域=该原子实例域**原样**（表默认或显式收窄——运行时不做极性侧别过滤：与旧共享口径一致，
        /// 双域原子可选两侧、跨侧命中走错边黑白发放经济；极性收窄是合成器 UI 的选择集口径，
        /// 「苏醒式自缚」类显式锁己域数据照常生效）；数量档/随机档为**每原子声明**
        ///（2026-10-09 下沉：未声明 -2/-1 回落效果级 def，效果级对未声明原子统一生效）。
        /// 声明期预选目标归属：preselected 落在该原子合法候选内 → 直接沿用（响应窗口指向性卡
        ///（发动区反制）/教学与 AI 预选保活）；预选全落域外 → 该原子回落自解析。
        /// 行为口径与组合层解析（ResolveCompositionTargetsAsync）对齐：
        /// 全隐藏区=自结算空列表（隐藏信息不进选择）；全取档/数量"全部"=候选全取不弹窗；
        /// 强制类（Mandatory）无选择窗口=自动全取；随机=帷幕收窄池内种子抽取（扰魔/潜行可命中）；
        /// 选一/选多=两道过滤（帷幕收窄+显示域）后候选≤需求自动取、否则弹窗（AI/无头 Service 代选）。
        /// 无域原子返回空列表（调用方按单次 null 目标自结算执行）。
        /// </summary>
        public static async UniTask<List<Entity>> ResolveAtomTargetsAsync(
            EffectDefinition def, AtomicEffectInstance atom, EffectExecutionContext context,
            List<Entity> preselected = null, string title = "选择目标")
        {
            if (def == null || atom == null || context == null) return new List<Entity>();

            var kinds = atom.TargetKinds ?? new List<int>();
            if (kinds.Count == 0) return new List<Entity>(); // 无域原子：handler 自结算

            // 预选归属：预选目标 ∈ 该原子合法候选 → 沿用（域外原子不消费预选，回落自解析）。
            // 先于隐藏区早退——「点名已展示的对方手牌卡」等信息轴联动依赖预选透传。
            if (preselected != null && preselected.Count > 0)
            {
                var pool = ResolveCandidates(kinds, atom.Filter ?? "", context);
                var kept = preselected.Where(pool.Contains).ToList();
                if (kept.Count > 0) return kept;
            }

            // 全隐藏区（对方手牌/双方牌库）：隐藏信息不进选择——自结算（与组合层早退口径一致）
            if (TargetKindRules.AllHiddenZone(kinds)) return new List<Entity>();

            var candidates = ResolveCandidates(kinds, atom.Filter ?? "", context);
            GameActions.Crumb($"atom-targets {def.Id}/{atom.Type} kinds={TargetKindRules.Format(kinds)} "
                + $"pre={preselected?.Count ?? 0} cands={candidates.Count} mode={def.SelectionMode} cnt={atom.EffectiveTargetCount(def)}");
            if (candidates.Count == 0) return candidates;

            bool edictExempt = TargetResolver.IsEdict(atom.Type);
            bool noWindow = def.ActivationType == EffectActivationType.Mandatory; // 强制类无目标选择窗口
            bool noneAsSingle = def.SelectionMode == SelectionMode.None; // 未声明模式=按选一（与组合层同口径）
            int effCount = atom.EffectiveTargetCount(def); // 每原子有效数量（未声明=效果级）

            // 全取：候选全取——扰魔/潜行照常命中，不受帷幕收窄；强制类无窗口同走全取（构筑期目标明确）
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

            if (noneAsSingle || SelectionModeRules.IsPickOne(def.SelectionMode))
            {
                // 选一个：候选≤1 自动取（域={Self} 的关键词效果即此路——唯一候选=源卡自身）
                if (candidates.Count <= 1) return candidates.Take(1).ToList();
                return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = candidates,
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = context.Controller,
                    Title = title,
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
                });
            }
            int need = effCount > 0 ? effCount : candidates.Count;
            if (candidates.Count <= need)
                return candidates.Take(need).ToList();
            return await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = candidates,
                MinCount = need,
                MaxCount = need,
                Chooser = context.Controller,
                Title = title,
            });
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
