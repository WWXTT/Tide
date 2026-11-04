using System.Collections.Generic;
using System.Linq;

namespace CardCore.Attribute
{
    /// <summary>
    /// 展示状态查询口（信息轴，2026-10-02 定案）：展示 = Exposed 指示物（换区清除——离开被展示时
    /// 所在区域即失效）。UI「点击对应区域查看被展示的卡」、网络快照（RevealedZoneCards）与
    /// 事件座位过滤（隐藏集豁免）共用此口径；亦为后续「被展示的卡本回合不可使用/送入墓地/
    /// 以被展示换费用减免」类设计的目标判定挂点（TargetFilter "Exposed" token 同源）。
    /// </summary>
    public static class RevealRules
    {
        /// <summary>卡当前是否处于展示状态（对双方公开）。</summary>
        public static bool IsExposed(Card card)
            => card != null && card.GetCounterCount(CounterRules.ExposedCounter) > 0;

        /// <summary>某玩家某区域中所有被展示的卡（隐藏区 Hand/Deck 的可查看子集；公开区全量本就可见）。</summary>
        public static List<Card> GetExposedCards(ZoneManager zoneManager, Player player, Zone zone)
            => (zoneManager != null ? zoneManager.GetCards(player, zone) : new List<Card>())
                .Where(IsExposed)
                .ToList();

        /// <summary>展示投放统一口（2026-11-03 定案：隐藏区域的卡**成为效果作用目标即自动展示**——
        /// 占卜/发现/附加诅咒·祝福/筛选展示/展示原子/拼点引擎取样/首充，全走此口）：
        /// 挂 1 层 Exposed（二值不叠层——已展示不重复挂/不重复发事件，换区自清），并发布
        /// CounterChangedEvent + RevealCardsEvent（净计数/动画/统计三消费口同口径）。
        /// 返回是否本次新投放。</summary>
        public static bool Reveal(Card card, Entity source)
        {
            if (card == null || !card.IsAlive) return false;
            if (IsExposed(card)) return false;

            int actualDelta = card.AddCounters(CounterRules.ExposedCounter, 1, source);
            EventManager.Instance.Publish(new CounterChangedEvent
            {
                Target = card,
                CounterType = CounterRules.ExposedCounter,
                Amount = actualDelta,
                Source = source,
            });
            EventManager.Instance.Publish(new RevealCardsEvent
            {
                Player = card.GetController(),
                Cards = new List<Card> { card },
                Source = source,
            });
            return true;
        }
    }
}
