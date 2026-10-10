using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute
{
    /// <summary>
    /// 临时复制卡规则（微缩/放大 2026-09-11 引入，2026-10-10 照抄炉石机制退役删除；
    /// 2026-10-07 回响改普通效果 EchoCopy 退出事件订阅）：
    ///
    /// - 回响（现役唯一用户）：复制走 EchoCopyHandler 结算期调用 CreateTemporaryCopy
    ///  （完全复制、连锁保留）；被反制不再入手（旧宣言时点分支已删）。
    /// - 临时性指示物化（2026-10-10）：IsTemporary 布尔退役——临时卡=持 1 层临时指示物
    ///  （CounterRules.TempCounter，Exception 换区不清+回合末例程：持有者回合末不在战场
    ///  →从游戏中移除、在战场→衰退一层转正）。回合末清理由 CounterRules.OnTurnEnd ②块承接
    ///  （先于手牌上限弃牌的时序不变——调用点在 GameCore.OnTurnEnded）。
    /// - 本类不再订阅任何事件，仅余 CreateTemporaryCopy 静态工具。
    /// </summary>
    public static class TempCopyRules
    {
        /// <summary>
        /// 生成临时复制卡入手（临时性=1 层临时指示物，2026-10-10 指示物化定案）。
        /// power/life/cost 传 null = 完全复制（回响；微缩/放大覆盖档已随其退役闲置）。
        /// </summary>
        internal static void CreateTemporaryCopy(
            CardData source, int? power, int? life, float? grayCost, Player controller, ZoneManager zm)
        {
            if (source == null || controller == null || zm == null) return;

            // 深克隆：TideJson 往返（CardData 序列化字段全量，Cost 经 ITideSerializationCallback 同步）
            var data = TideJson.FromJson<CardData>(TideJson.ToJson(source));
            if (data == null) return;

            if (power.HasValue && life.HasValue && source.HasCombatStats)
            {
                data.Power = power.Value;
                data.Life = life.Value;
            }
            if (grayCost.HasValue)
            {
                data.Cost = ElementCost.FromValue(ManaType.Gray, grayCost.Value);
            }

            var card = new CardWrapper(data)
            {
                ID = $"{source.ID}#t{CardCore.TimestampSystem.NextSequence}", // 对局临时实例 ID（token 同惯例）
            };
            card.SetController(controller);

            // 临时标记=1 层临时指示物（回合末口径=CounterRules.OnTurnEnd ②块：
            // 不在战场从游戏中移除、在战场衰退一层转正；不可作地牌=CanServeAsLand 读同一指示物）
            card.AddCounters(CounterRules.TempCounter, 1);
            Publish(new CounterChangedEvent
            {
                Target = card,
                CounterType = CounterRules.TempCounter,
                Amount = 1,
                Source = null,
            });

            // 入手：容器 Add 不经 OnCardMoved——补发入手事件（非抽牌，token 先例）
            zm.GetZoneContainer(controller)?.Add(card, Zone.Hand);
            Publish(new CardEnterHandEvent
            {
                Player = controller,
                Card = card,
                FromZone = Zone.None,
                IsDraw = false,
            });
        }

        // PurgeTemporaryHandCards 已删（2026-10-10 临时卡指示物化）：回合末口统一为
        // CounterRules.OnTurnEnd ②临时例程（全区域扫描——手牌/墓地/牌库的挂标卡一律出局，
        // 不再只清手牌）；先于手牌上限弃牌的时序不变（同在 GameCore.OnTurnEnded 内）。

        private static void Publish<T>(T e) where T : IGameEvent
        {
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
            else EventManager.Instance.Publish(e);
        }
    }
}
