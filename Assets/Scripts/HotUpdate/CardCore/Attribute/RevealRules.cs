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
    }
}
