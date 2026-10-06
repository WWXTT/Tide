using System;
using System.Collections.Generic;

namespace CardCore
{
    // ================================================================
    // 规则扩展点（OCP 定案）
    // 规则修改类效果经这些窄接口接入引擎热点，热点只认接口、不点名具体系统；
    // 新规则 = 新类 + 注册（+ 配置条目）。
    //
    // 选用指引：
    // - 事件内容改写（伤害/疲劳数值、事件替换为另一事件）→ ReplacementEngine（替代引擎）
    // - 行为许可（能否打出/能否用某来源区/是否跳过回合开始自动化）→ 本文件注册表
    // - 数值查询链（手牌上限）→ 本文件注册表
    // - 回合开始重置例外（冻结/沉睡无法重置）→ 本文件注册表（IUntapBlockRule）
    // ================================================================

    /// <summary>出牌限制：CanPlay 返回 false 拒绝打出（出牌管线的通用拦截扩展点）。</summary>
    public interface IPlayRestriction
    {
        bool CanPlay(GameCore core, Player player, Card card, Zone fromZone);
    }

    /// <summary>非手牌出牌来源（如墓地视手牌使用）：声明来源区与使用配额。</summary>
    public interface IPlaySource
    {
        Zone SourceZone { get; }
        bool CanUse(Player player);
        bool TryBeginUse(Player player);
    }

    /// <summary>回合开始自动化拦截：返回 true 则该玩家本回合的准备阶段自动化整体跳过（发 StandbySkippedEvent）。</summary>
    public interface ITurnStartInterceptor
    {
        bool ShouldSkipTurnStartAutomation(Player player);
    }

    /// <summary>手牌上限修改链：逐个套用，基值 RuleHooks.DefaultHandLimit（7）。</summary>
    public interface IHandLimitModifier
    {
        int Modify(Player player, int currentLimit);
    }

    /// <summary>回合开始重置拦截（2026-09-13 定案：冻结/沉睡期间无法重置）：
    /// 注册方声明实体本回合开始是否禁止重置，并自理被禁期间的状态推进（沉睡扣层/苏醒）。
    /// 重置循环（GameCore 回合开始）只认此扩展点，不点名具体指示物（OCP）。</summary>
    public interface IUntapBlockRule
    {
        /// <summary>该实体本回合开始是否禁止重置（无论当前是否横置——沉睡扣层对未横置者照常推进）。</summary>
        bool BlocksUntap(Card card);

        /// <summary>禁止重置时的状态推进（如沉睡扣 1 层；末层耗尽由注册方自行苏醒重置）。</summary>
        void OnUntapBlocked(GameCore core, Card card);
    }

    /// <summary>
    /// 规则扩展注册表（仿 CostHandlerRegistry 惯例）。
    /// 实现约定为状态无关：实时查询光环/宣告等活状态（同 ReplacementEngine 替代件与血偿装饰器的惯例），
    /// 因此一次性注册即可跨对局复用，无需逐局注销。
    /// </summary>
    public static class RuleHooks
    {
        private static readonly List<IPlayRestriction> _playRestrictions = new List<IPlayRestriction>();
        private static readonly Dictionary<Zone, IPlaySource> _playSources = new Dictionary<Zone, IPlaySource>();
        private static readonly List<ITurnStartInterceptor> _turnStartInterceptors = new List<ITurnStartInterceptor>();
        private static readonly List<IHandLimitModifier> _handLimitModifiers = new List<IHandLimitModifier>();

        /// <summary>手牌上限基值（修改链在此之上叠加）。</summary>
        public const int DefaultHandLimit = 7;

        // ---- 出牌限制 ----

        public static void RegisterPlayRestriction(IPlayRestriction restriction)
        {
            if (restriction != null && !_playRestrictions.Contains(restriction))
                _playRestrictions.Add(restriction);
        }

        public static void UnregisterPlayRestriction(IPlayRestriction restriction)
            => _playRestrictions.Remove(restriction);

        /// <summary>全部出牌限制都放行才可打出。</summary>
        public static bool CanPlay(GameCore core, Player player, Card card, Zone fromZone)
        {
            foreach (var restriction in _playRestrictions)
                if (!restriction.CanPlay(core, player, card, fromZone))
                    return false;
            return true;
        }

        // ---- 非手牌出牌来源 ----

        public static void RegisterPlaySource(IPlaySource source)
        {
            if (source != null)
                _playSources[source.SourceZone] = source;
        }

        public static void UnregisterPlaySource(IPlaySource source)
        {
            if (source != null && _playSources.TryGetValue(source.SourceZone, out var registered) && registered == source)
                _playSources.Remove(source.SourceZone);
        }

        /// <summary>查询某来源区的出牌来源（无注册则该区不可作为出牌来源）。</summary>
        public static IPlaySource GetPlaySource(Zone zone)
            => _playSources.TryGetValue(zone, out var source) ? source : null;

        // ---- 回合开始自动化拦截 ----

        public static void RegisterTurnStartInterceptor(ITurnStartInterceptor interceptor)
        {
            if (interceptor != null && !_turnStartInterceptors.Contains(interceptor))
                _turnStartInterceptors.Add(interceptor);
        }

        public static void UnregisterTurnStartInterceptor(ITurnStartInterceptor interceptor)
            => _turnStartInterceptors.Remove(interceptor);

        /// <summary>任一拦截器命中即跳过该玩家本回合的准备阶段自动化。</summary>
        public static bool ShouldSkipTurnStartAutomation(Player player)
        {
            foreach (var interceptor in _turnStartInterceptors)
                if (interceptor.ShouldSkipTurnStartAutomation(player))
                    return true;
            return false;
        }

        // ---- 手牌上限链 ----

        public static void RegisterHandLimitModifier(IHandLimitModifier modifier)
        {
            if (modifier != null && !_handLimitModifiers.Contains(modifier))
                _handLimitModifiers.Add(modifier);
        }

        public static void UnregisterHandLimitModifier(IHandLimitModifier modifier)
            => _handLimitModifiers.Remove(modifier);

        /// <summary>玩家当前手牌上限（基值 7，逐个套用修改链）。</summary>
        public static int GetHandLimit(Player player)
        {
            var limit = DefaultHandLimit;
            foreach (var modifier in _handLimitModifiers)
                limit = modifier.Modify(player, limit);
            return limit;
        }

        // ---- 回合开始重置拦截 ----

        private static readonly List<IUntapBlockRule> _untapBlockRules = new List<IUntapBlockRule>();

        public static void RegisterUntapBlockRule(IUntapBlockRule rule)
        {
            if (rule != null && !_untapBlockRules.Contains(rule))
                _untapBlockRules.Add(rule);
        }

        public static void UnregisterUntapBlockRule(IUntapBlockRule rule)
            => _untapBlockRules.Remove(rule);

        /// <summary>任一规则命中即禁止该实体本回合开始重置。</summary>
        public static bool BlocksUntap(Card card)
        {
            foreach (var rule in _untapBlockRules)
                if (rule.BlocksUntap(card))
                    return true;
            return false;
        }

        /// <summary>禁止重置时的状态推进——派发给第一个命中的规则。</summary>
        public static void OnUntapBlocked(GameCore core, Card card)
        {
            foreach (var rule in _untapBlockRules)
                if (rule.BlocksUntap(card))
                {
                    rule.OnUntapBlocked(core, card);
                    return;
                }
        }

        // ======================================== 教学引导闸（2026-10-06 第一课改版） ========================================

        // 瞬态扩展点（单局 Begin/End 成对装卸），区别于上方常驻注册式规则——教学局由 SynergyUI.TutorialGuide
        // 注入：暂停引导的语义 = 引导步骤未完成时把对局钉住（攻击只放行引导动作、结束回合禁走、
        // 守卫候选只留引导守卫者）。全部 null=无教学局，引擎行为与原先逐位一致。

        /// <summary>攻击宣言闸：(player, attacker, target) → 拒绝原因（非 null 即拒绝）；null=放行。
        /// 在常规资格检查之前（教学语义优先——错误目标/锁定生物在此拦下）。</summary>
        public static Func<Player, Entity, Entity, string> TutorialAttackGate;

        /// <summary>结束回合闸：player → 拒绝原因；null=放行（引导步骤未完成时钉住回合）。</summary>
        public static Func<Player, string> TutorialEndTurnGate;

        /// <summary>守卫候选过滤：(holder, 当批守卫候选) → 过滤后的候选（null=原样）。
        /// 调用时候选列表只含守卫项（效果项在其后追加）。</summary>
        public static Func<Player, List<ResponseOption>, List<ResponseOption>> TutorialGuardFilter;

        /// <summary>攻击宣言成功探针（引导步骤完成的信号；非教学局攻击也会经过——订阅方自行过滤）。</summary>
        public static Action<Player, Entity, Entity> OnTutorialAttackDeclared;

        /// <summary>攻击结算/取消探针：(attacker, 最终目标——被守卫改写后)。守卫步骤的漏守兜底信号。</summary>
        public static Action<Entity, Entity> OnTutorialAttackResolved;

        /// <summary>守卫拦截结算探针：(guarder, 被拦截的攻击宣言)。</summary>
        public static Action<Card, EffectInstance> OnTutorialGuardResolved;

        /// <summary>结束回合成功探针（end 引导步骤的完成信号；非教学局结束回合也会经过——订阅方自行过滤）。</summary>
        public static Action<Player> OnTutorialTurnEnded;

        // ---- 资源流转闸（2026-10-06 第二课）：放地/横置产地/横置生物产色/出牌四动作的教学闸与探针 ——
        // 语义同上组：null=无教学局行为不变；闸先于常规资格（教学语义优先）；探针在成功路径发射。

        /// <summary>放地闸（手牌卡入元素池）：(player, card) → 拒绝原因；null=放行。</summary>
        public static Func<Player, Card, string> TutorialElementGate;

        /// <summary>地牌横置产色闸：(player, land, type) → 拒绝原因；null=放行。</summary>
        public static Func<Player, PooledCard, ManaType, string> TutorialTapGate;

        /// <summary>生物横置产色闸（LandTrait 特性产元素）：(player, creature) → 拒绝原因；null=放行。</summary>
        public static Func<Player, Card, string> TutorialTapCreatureGate;

        /// <summary>出牌闸（手牌打出）：(player, card) → 拒绝原因；null=放行。</summary>
        public static Func<Player, Card, string> TutorialPlayGate;

        /// <summary>放地成功探针（element 引导步骤完成信号）。</summary>
        public static Action<Player, Card> OnTutorialElementPlaced;

        /// <summary>地牌产色成功探针：(player, 地牌源卡, 产出的颜色)——tap 步骤完成信号。</summary>
        public static Action<Player, Card, ManaType> OnTutorialManaGained;

        /// <summary>生物横置产色成功探针（tapcreature 步骤完成信号）。</summary>
        public static Action<Player, Card> OnTutorialCreatureTapped;

        /// <summary>出牌宣言成功探针（play 步骤完成信号）。</summary>
        public static Action<Player, Card> OnTutorialCardPlayed;
    }
}
