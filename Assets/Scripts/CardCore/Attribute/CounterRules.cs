using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Attribute
{
    /// <summary>
    /// 指示物极性（定案）：正面=增益/资源，负面=减益/妨害。
    /// </summary>
    public enum CounterPolarity
    {
        /// <summary>正面（增益/资源类：护甲、+1/+1 等）</summary>
        Positive,
        /// <summary>负面（减益/妨害类：冻结、突袭紊乱、-1/-1 等）</summary>
        Negative,
    }

    /// <summary>指示物规格：id → 极性 + 默认持续时间 + 显示名。</summary>
    public class CounterSpec
    {
        public string Id;
        public CounterPolarity Polarity;
        /// <summary>
        /// 默认持续：Permanent = 常驻（消耗型/叠加型）；
        /// UntilEndOfTurn = 归属卡控制者的回合结束消退（走本类统一清理，不修改回合规则）。
        /// </summary>
        public DurationType Duration;
        /// <summary>播报显示名（消退播报用）</summary>
        public string DisplayName;
    }

    /// <summary>
    /// 指示物统一管理（定案）：按正面/负面登记规格，持续到期走回合结束统一清理。
    ///
    /// 【设计原则】
    /// - 持续指示物不改回合规则（"回合开始不重置横置"之类的妨碍属于规则修改类能力，已废除）——
    ///   冻结 = 施加时横置（一次性动作）+ 负面指示物（持续到回合结束）；回合开始重置照常走。
    /// - UntilEndOfTurn 口径与 ContinuousEffectDurationTracker 一致：归属卡控制者的回合结束消退。
    /// - 新指示物 = 此处登记一条（OCP：不改引擎热点）；未登记 id 保守视为 正面/Permanent（不参与清理）。
    /// </summary>
    public static class CounterRules
    {
        private static readonly Dictionary<string, CounterSpec> _registry =
            new Dictionary<string, CounterSpec>();

        static CounterRules()
        {
            // ---- 正面（常驻）----
            Register(new CounterSpec { Id = KeywordRules.ArmorCounter, Polarity = CounterPolarity.Positive, Duration = DurationType.Permanent, DisplayName = "护甲" });
            Register(new CounterSpec { Id = "+1/+1", Polarity = CounterPolarity.Positive, Duration = DurationType.Permanent, DisplayName = "+1/+1" });
            Register(new CounterSpec { Id = "Awakening", Polarity = CounterPolarity.Positive, Duration = DurationType.Permanent, DisplayName = "觉醒" });

            // ---- 负面 ----
            // 常驻（SBA 与 +1/+1 对消）
            Register(new CounterSpec { Id = "-1/-1", Polarity = CounterPolarity.Negative, Duration = DurationType.Permanent, DisplayName = "-1/-1" });
            // 持续到回合结束（施加时横置为一次性动作；回合开始重置照常，不改回合规则）
            Register(new CounterSpec { Id = KeywordRules.FreezeCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilEndOfTurn, DisplayName = "冻结" });
            Register(new CounterSpec { Id = KeywordRules.RushSicknessCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilEndOfTurn, DisplayName = "突袭紊乱" });
        }

        /// <summary>登记指示物规格（新指示物=新登记，OCP）。</summary>
        public static void Register(CounterSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id)) return;
            _registry[spec.Id] = spec;
        }

        /// <summary>查规格；未登记保守视为 正面/Permanent（不参与持续清理，不改现有行为）。</summary>
        public static CounterSpec Find(string id)
            => _registry.TryGetValue(id, out var spec) ? spec
               : new CounterSpec { Id = id, Polarity = CounterPolarity.Positive, Duration = DurationType.Permanent, DisplayName = id };

        public static bool IsNegative(string id) => Find(id).Polarity == CounterPolarity.Negative;
        public static bool IsUntilEndOfTurn(string id) => Find(id).Duration == DurationType.UntilEndOfTurn;

        /// <summary>
        /// 回合结束清理：消退归属回合玩家场上随从的全部 UntilEndOfTurn 指示物（整类清零，发消退播报）。
        /// 由 GameCore.OnTurnEnd 调用（与 DurationTracker / TextChangeLayer 同链）。
        /// </summary>
        public static void OnTurnEnd(Player turnPlayer, ZoneManager zoneManager)
        {
            if (turnPlayer == null || zoneManager == null) return;
            foreach (var card in zoneManager.GetCards(turnPlayer, Zone.Battlefield))
            {
                var expiring = card._counters
                    .Where(kv => kv.Value > 0 && IsUntilEndOfTurn(kv.Key))
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var id in expiring)
                {
                    card.AddCounters(id, -card.GetCounterCount(id));
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card,
                        Keyword = id,
                        Detail = $"{Find(id).DisplayName}消退（持续到回合结束）"
                    });
                }
            }
        }

        /// <summary>清空目标全部负面指示物（解减益类效果的统一口径；横置状态不在此恢复）。</summary>
        public static void ClearNegative(Card card)
        {
            if (card == null) return;
            var negatives = card._counters
                .Where(kv => kv.Value > 0 && IsNegative(kv.Key))
                .Select(kv => kv.Key)
                .ToList();
            foreach (var id in negatives)
                card.AddCounters(id, -card.GetCounterCount(id));
        }
    }
}
