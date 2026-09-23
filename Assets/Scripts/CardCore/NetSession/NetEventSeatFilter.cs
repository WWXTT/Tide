using System;
using System.Collections.Generic;

namespace CardCore.Network
{
    /// <summary>
    /// 事件流按座位过滤（M2，网络协议.md §6 定案的"服务器出队时过滤"落地）：
    /// NetEvent 本身是全信息内部流——投影器不裁剪；本过滤器在**服务器出队**时
    /// 按 viewer 座位剔除隐藏实体引用（2026-09-23 定案：观战=全信息，不滤）。
    ///
    /// 规则：
    /// - Kind=Entity 的字段引用隐藏卡 → 整字段丢弃（事件保留，例：对手抽牌仍可见"抽了牌"，
    ///   但 DrawnCard 引用不下发——客户端以快照手牌数量对账）。
    /// - Kind=EntityList 的字段 → 只把隐藏引用替换为 None（保留列表长度=数量语义，公开成员照发）。
    /// - 其余 Kind 不涉及实体引用，原样通过。
    /// - 零拷贝：无命中时返回原实例；有命中才克隆（共享投影缓冲不可原地改）。
    ///
    /// 隐藏集口径（HiddenCardIds）：viewer 的**对方手牌** + **双方牌库**（对局中未公开区）。
    /// 按**出队时点**的当前区域判定——同 tick 内事件即发即滤，时点漂移窗口为一个泵周期。
    /// </summary>
    public static class NetEventSeatFilter
    {
        /// <summary>计算 viewer 的隐藏卡 RuntimeId 集（观战 viewerSeat=-1 返回空——全信息）。</summary>
        public static HashSet<uint> HiddenCardIds(GameCore core, int viewerSeat)
        {
            var hidden = new HashSet<uint>();
            if (core == null || viewerSeat < 0) return hidden;

            var opponent = NetEntityDirectory.SeatToPlayer(core, 1 - viewerSeat);
            AddZone(hidden, core, opponent, Zone.Hand);
            AddZone(hidden, core, core.Player1, Zone.Deck);
            AddZone(hidden, core, core.Player2, Zone.Deck);
            return hidden;
        }

        private static void AddZone(HashSet<uint> hidden, GameCore core, Player player, Zone zone)
        {
            if (player == null) return;
            var cards = core.ZoneManager.GetCards(player, zone);
            if (cards == null) return;
            foreach (var card in cards)
                if (card != null)
                    hidden.Add(card.RuntimeId);
        }

        /// <summary>按隐藏集过滤一条事件（无命中返回原实例）。hidden 为空/null 直通。</summary>
        public static NetEvent Filter(NetEvent evt, HashSet<uint> hidden)
        {
            if (evt?.Params == null || evt.Params.Length == 0 || hidden == null || hidden.Count == 0)
                return evt;

            NetParam[] copy = null;
            for (int i = 0; i < evt.Params.Length; i++)
            {
                var p = evt.Params[i];
                if (p?.EntityRefs == null || p.EntityRefs.Length == 0) continue;

                if (p.Kind == NetParamKind.Entity && p.EntityRefs[0] != null && hidden.Contains(p.EntityRefs[0].RuntimeId))
                {
                    copy = copy ?? (NetParam[])evt.Params.Clone();
                    copy[i] = new NetParam { FieldName = p.FieldName, Kind = NetParamKind.Null }; // 字段在、值隐
                    continue;
                }

                if (p.Kind == NetParamKind.EntityList)
                {
                    bool touched = false;
                    var refs = (NetEntityRef[])p.EntityRefs.Clone();
                    for (int r = 0; r < refs.Length; r++)
                    {
                        if (refs[r] != null && !refs[r].IsPlayer && hidden.Contains(refs[r].RuntimeId))
                        {
                            refs[r] = NetEntityMapper.None;
                            touched = true;
                        }
                    }
                    if (touched)
                    {
                        copy = copy ?? (NetParam[])evt.Params.Clone();
                        copy[i] = new NetParam
                        {
                            FieldName = p.FieldName,
                            Kind = p.Kind,
                            IntValue = p.IntValue,
                            FloatValue = p.FloatValue,
                            StringValue = p.StringValue,
                            EntityRefs = refs,
                        };
                    }
                }
            }

            if (copy == null) return evt;
            return new NetEvent
            {
                EventType = evt.EventType,
                EventId = evt.EventId,
                TurnNumber = evt.TurnNumber,
                Params = copy,
            };
        }
    }
}
