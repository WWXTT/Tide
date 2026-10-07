using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute
{
    /// <summary>
    /// 微缩/放大——临时复制卡规则（2026-09-11 定案；2026-10-07 回响改普通效果 EchoCopy 退出事件订阅）：
    ///
    /// - 微缩/放大（登场效果授予单位，关键词）：控制者使用任意卡时（CardPlayEvent 宣言时点），
    ///   将一张「同效果」临时卡加入手牌——微缩 = 1/1 费 1 灰、放大 = 10/10 费 10 灰；
    ///   法术原卡无属性则复制也不设属性（费用仍覆盖）。回合结束时从手牌移除。
    ///   临时卡自身不再触发微缩/放大（防自复制链）；不可作地牌（CanServeAsLand 守卫）。
    /// - 回响（2026-10-07 关键词→普通效果改版）：复制走 EchoCopyHandler 结算期调用
    ///   CreateTemporaryCopy（完全复制、连锁保留）；宣言时点分支已删，被反制不再入手。
    /// - 时序定案：回合结束「先移除临时卡、再看手牌上限 7 弃牌」（GameCore.OnTurnEnded 顺序）。
    ///
    /// 订阅由 GameCore.Initialize 挂载（与 TurnStart/PhaseEnd 同一接线块）；静态无状态，跨局无残留。
    /// </summary>
    public static class TempCopyRules
    {
        /// <summary>CardPlayEvent 回调（GameCore.Initialize 订阅）——仅微缩/放大（回响分支已删）</summary>
        public static void OnCardPlayed(CardPlayEvent e)
        {
            if (e?.Player == null || e.PlayedCard == null) return;

            var core = GameCore.Instance;
            var zm = core != null ? core.ZoneManager : null;

            // ---- 微缩/放大：控制者场上单位持有；临时卡不触发（防自复制链）----
            if (e.PlayedCard.IsTemporary || zm == null) return;
            var playedData = (e.PlayedCard as CardWrapper)?.GetData();
            foreach (var unit in zm.GetCards(e.Player, Zone.Battlefield))
            {
                if (unit == null || !unit.IsAlive) continue;
                if (unit.HasKeyword(KeywordRules.Miniature))
                    CreateTemporaryCopy(playedData, 1, 1, 1f, e.Player, zm);
                if (unit.HasKeyword(KeywordRules.Magnify))
                    CreateTemporaryCopy(playedData, 10, 10, 10f, e.Player, zm);
            }
        }

        /// <summary>
        /// 生成临时复制卡入手。power/life/cost 传 null = 完全复制（回响）；否则覆盖（微缩/放大——
        /// 原卡无战斗属性时跳过属性覆盖，只覆盖费用）。
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
                IsTemporary = true,
            };
            card.SetController(controller);

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

        /// <summary>
        /// 回合结束移除手牌临时卡（GameCore.OnTurnEnded 调用，先于手牌上限 7 弃牌——时序定案）。
        /// 移除 = 消失（不进墓地，不触发死亡/苏生污染）。
        /// </summary>
        public static void PurgeTemporaryHandCards(Player player, ZoneManager zm)
        {
            if (player == null || zm == null) return;
            var hand = zm.GetCards(player, Zone.Hand);
            foreach (var card in hand.Where(c => c != null && c.IsTemporary).ToList())
            {
                zm.GetZoneContainer(player)?.Remove(card, Zone.Hand);
                Publish(new CardMoveEvent
                {
                    MovedCard = card,
                    From = Zone.Hand,
                    To = Zone.None,
                    Controller = player,
                });
            }
        }

        private static void Publish<T>(T e) where T : IGameEvent
        {
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
            else EventManager.Instance.Publish(e);
        }
    }
}
