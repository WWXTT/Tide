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
    // ================================================================

    /// <summary>出牌限制：CanPlay 返回 false 拒绝打出（如信息轴仪式的回合锁定）。</summary>
    public interface IPlayRestriction
    {
        bool CanPlay(GameCore core, Player player, Card card, Zone fromZone);
    }

    /// <summary>非手牌出牌来源（如归土仪典的墓地视手牌使用）：声明来源区与使用配额。</summary>
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
    }
}
