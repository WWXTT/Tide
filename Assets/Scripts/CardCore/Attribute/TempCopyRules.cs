using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CardCore.Attribute;

namespace CardCore.Attribute
{
    /// <summary>
    /// 微缩/放大/回响——临时复制卡规则（2026-09-11 定案）：
    ///
    /// - 微缩/放大（登场效果授予单位，MountKinds=0,5）：控制者使用任意卡时（CardPlayEvent 宣言时点），
    ///   将一张「同效果」临时卡加入手牌——微缩 = 1/1 费 1 灰、放大 = 10/10 费 10 灰；
    ///   法术原卡无属性则复制也不设属性（费用仍覆盖）。回合结束时从手牌移除。
    ///   临时卡自身不再触发微缩/放大（防自复制链）；不可作地牌（CanServeAsLand 守卫）。
    /// - 回响（2026-09-13 用户定案：**生物和法术通用**，MountKinds=1,5,6）：使用时获得本体完全复制
    ///   （属性/费用/效果不变）的临时卡，复制也带回响（可连锁，回合结束移除兜底）。
    ///   宣言时点定案：被反制时复制已入手。生物宿主照常（打出→复制入手→再打出=再进一个）。
    /// - 时序定案：回合结束「先移除临时卡、再看手牌上限 7 弃牌」（GameCore.OnTurnEnded 顺序）。
    ///
    /// 订阅由 GameCore.Initialize 挂载（与 TurnStart/PhaseEnd 同一接线块）；静态无状态，跨局无残留。
    /// </summary>
    public static class TempCopyRules
    {
        /// <summary>CardPlayEvent 回调（GameCore.Initialize 订阅）</summary>
        public static void OnCardPlayed(CardPlayEvent e)
        {
            if (e?.Player == null || e.PlayedCard == null) return;

            var core = GameCore.Instance;
            var zm = core != null ? core.ZoneManager : null;

            // ---- 回响：本体自带（临时复制本体也带回响 → 允许连锁，靠回合结束移除兜底）----
            var playedData = (e.PlayedCard as CardWrapper)?.GetData();
            if (playedData != null && e.PlayedCard.HasKeyword(KeywordRules.Echo))
            {
                // 完全复制：不覆盖属性/费用，克隆自带 Keywords（含 Echo）随构造回灌 Printed 轨
                CreateTemporaryCopy(playedData, null, null, null, e.Player, zm);
            }

            // ---- 微缩/放大：控制者场上单位持有；临时卡不触发（防自复制链）----
            if (e.PlayedCard.IsTemporary || zm == null) return;
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

            // 深克隆：JsonUtility 往返（CardData 全 [SerializeField]，Cost 经 _costJson 同步回调往返）
            var data = JsonUtility.FromJson<CardData>(JsonUtility.ToJson(source));
            if (data == null) return;

            if (power.HasValue && life.HasValue && source.HasCombatStats)
            {
                data.Power = power.Value;
                data.Life = life.Value;
            }
            if (grayCost.HasValue)
            {
                data.Cost = new Dictionary<int, float> { { (int)ManaType.Gray, grayCost.Value } };
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
