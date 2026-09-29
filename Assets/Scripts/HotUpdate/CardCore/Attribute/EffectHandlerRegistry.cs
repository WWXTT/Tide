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
                UnityEngine.Debug.LogWarning($"未注册的效果处理器: {effect.Type}");
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

            // 法术护盾：首次成为对手效果目标时该效果对其无效（移出目标 + 消耗护盾）
            KeywordRules.ConsumeSpellShields(context.Targets, context.Source);

            if (!handler.CanExecute(effect, context))
            {
                UnityEngine.Debug.LogWarning($"效果无法执行: {effect.Type}");
                return false;
            }

            return true;
        }

        /// <summary>每实例有效目标参数（域模型）：实例解析域 + 实例 filter + 计数恒 1
        /// （2026-09-10 表级 TargetCount 列删除——分支奖励等免编排路径的单原子按 1 个目标结算；
        /// 多目标需求由组合层 TargetCount 声明）。</summary>
        private static (List<int> kinds, string filter, int count) GetEffectiveDomain(AtomicEffectInstance effect)
        {
            return (effect.TargetKinds ?? new List<int>(),
                    effect.Filter ?? "",
                    1);
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
            if (def.SelectionMode == SelectionMode.None) return new List<Entity>();
            if (domain == null || domain.Count == 0) return new List<Entity>(); // 无目标效果

            var candidates = ResolveCandidates(domain, def.TargetFilter, context);
            GameActions.Crumb($"comp-targets def={def.Id} mode={def.SelectionMode} dom={TargetKindRules.Format(domain)} cands={candidates.Count} sel={context.Controller?.Name}");
            if (candidates.Count == 0)
            {
                // Self 域空转诊断（原 Self 模式口径）：源不在候选（瞬间在发动区不在单位域）
                if (domain.Count == 1 && domain[0] == (int)TargetKind.Self)
                    UnityEngine.Debug.LogWarning(
                        $"[TargetDomain] 域={{Self}} 但源不在候选内（瞬间在发动区？）——效果空转: {def.Id}");
                return candidates;
            }

            // edict 豁免（2026-09-13）：牺牲/摒弃类选择权在目标方——帷幕只约束对手的选择，不管持有者自选
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
                int take = SelectionModeRules.IsPickOne(def.SelectionMode) ? 1
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
            int need = def.TargetCount > 0 ? def.TargetCount : candidates.Count;
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
        /// 异步目标解析（分支奖励等免编排路径）：候选 > 需求数时弹选择。
        /// 动态数量已上移组合层——原子级路径恒为固定数量（表级 TargetCount）。
        /// AI / 无头 / 超时由 Service 自动取前 N。
        /// </summary>
        public static async UniTask<List<Entity>> ResolveTargetsInteractiveAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (effect == null || context == null) return new List<Entity>();

            var (kinds, filter, count) = GetEffectiveDomain(effect);
            if (kinds.Count == 0) return new List<Entity>();

            var candidates = ResolveCandidates(kinds, filter, context);
            if (candidates.Count == 0) return candidates;

            // 固定数量：count<=0 视为全部
            int need = count > 0 ? count : candidates.Count;
            if (candidates.Count <= need)
                return candidates.Take(need).ToList();

            // 选择层两道过滤（2026-09-13）：帷幕收窄 + 弹窗显示域（扰魔/潜行隐藏）——
            // 全域/随机不经此处（随机池在组合层收窄）；edict 原子（牺牲/摒弃）豁免帷幕
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
