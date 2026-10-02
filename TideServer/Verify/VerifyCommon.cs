using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Network;
using CardCore.Serialization;
using Cysharp.Threading.Tasks;
using MemoryPack;

namespace TideServer.Verify
{
    /// <summary>
    /// 验证公共件（平移 NetProtocolLoopbackVerifier 的比较/往返/对账辅助 + 回环选择器 + 对拍记录）。
    /// </summary>
    internal static class VerifyCommon
    {
        // ============================================================ 往返（信封+帧） ============================================================

        /// <summary>MsgNetEventBatch 全链路往返：DTO → 信封 → 帧（三段切分喂解码器）→ 解码 → 反序列化。</summary>
        public static MsgNetEventBatch RoundTripBatch(MsgNetEventBatch batch)
        {
            var wire = NetworkSerializer.SerializeMessage(NetworkMessageType.NetEventBatch, batch);
            var envelope = NetworkSerializer.DeserializeEnvelope(wire);
            var frame = FrameCodec.Frame(envelope);

            var decoder = new FrameCodec.FrameDecoder();
            decoder.Append(frame.Take(2).ToArray());          // 前缀内切一刀
            decoder.Append(frame.Skip(2).Take(frame.Length / 3).ToArray());
            decoder.Append(frame.Skip(2 + frame.Length / 3).ToArray());

            if (!decoder.TryDecode(out var decoded) || decoder.TryDecode(out _))
                throw new InvalidOperationException("帧解码失败或多余帧");
            return NetworkSerializer.DeserializePayload<MsgNetEventBatch>(decoded);
        }

        public static MsgGameStateSync RoundTripSnapshot(MsgGameStateSync snapshot)
        {
            var wire = NetworkSerializer.SerializeMessage(NetworkMessageType.GameStateSyncV2, snapshot);
            var envelope = NetworkSerializer.DeserializeEnvelope(wire);
            var frame = FrameCodec.Frame(envelope);
            var decoder = new FrameCodec.FrameDecoder();
            decoder.Append(frame);
            if (!decoder.TryDecode(out var decoded))
                throw new InvalidOperationException("快照帧解码失败");
            return NetworkSerializer.DeserializePayload<MsgGameStateSync>(decoded);
        }

        /// <summary>单消息全链路往返：DTO → 信封 → 帧 → 解码 → 反序列化（握手段专用）。</summary>
        public static T RoundTripMessage<T>(NetworkMessageType type, T msg) where T : class
        {
            var wire = NetworkSerializer.SerializeMessage(type, msg);
            var envelope = NetworkSerializer.DeserializeEnvelope(wire);
            var frame = FrameCodec.Frame(envelope);
            var decoder = new FrameCodec.FrameDecoder();
            decoder.Append(frame);
            if (!decoder.TryDecode(out var decoded))
                throw new InvalidOperationException("握手帧解码失败");
            return NetworkSerializer.DeserializePayload<T>(decoded);
        }

        public static void AssertDeepEqual(NetEvent[] expected, NetEvent[] actual, string label)
        {
            if (expected.Length != actual.Length)
            {
                VerifySuite.Assert(false, $"{label} 事件数不符：{expected.Length} vs {actual.Length}");
                return;
            }
            for (int i = 0; i < expected.Length; i++)
            {
                if (!EqualEvent(expected[i], actual[i]))
                {
                    VerifySuite.Assert(false, $"{label}[{i}] 事件 {expected[i].EventType} 深度比较失败");
                    return;
                }
            }
            VerifySuite.Assert(true, $"{label} 信封+帧往返无损（{expected.Length} 条）");
        }

        // ============================================================ 深比较 ============================================================

        public static bool EqualEvent(NetEvent a, NetEvent b)
        {
            if (a.EventType != b.EventType || a.EventId != b.EventId || a.TurnNumber != b.TurnNumber
                || a.Params?.Length != b.Params?.Length) return false;
            if (a.Params == null) return true;
            for (int p = 0; p < a.Params.Length; p++)
            {
                var pa = a.Params[p];
                var pb = b.Params[p];
                if (pa.FieldName != pb.FieldName || pa.Kind != pb.Kind
                    || pa.IntValue != pb.IntValue || Math.Abs(pa.FloatValue - pb.FloatValue) > 1e-9
                    || pa.StringValue != pb.StringValue
                    || pa.EntityRefs?.Length != pb.EntityRefs?.Length) return false;
                if (pa.EntityRefs == null) continue;
                for (int r = 0; r < pa.EntityRefs.Length; r++)
                {
                    var ra = pa.EntityRefs[r];
                    var rb = pb.EntityRefs[r];
                    if (ra.RuntimeId != rb.RuntimeId || ra.Seat != rb.Seat
                        || ra.IsPlayer != rb.IsPlayer || ra.CardId != rb.CardId) return false;
                }
            }
            return true;
        }

        public static bool EqualCardState(SerializableRuntimeCardState a, SerializableRuntimeCardState b)
        {
            if (a.ID != b.ID || a.RuntimeId != b.RuntimeId || a.Power != b.Power || a.Life != b.Life
                || a.MaxLife != b.MaxLife || a.IsTapped != b.IsTapped || a.IsFrozen != b.IsFrozen
                || a.ControllerSeat != b.ControllerSeat || a.Zone != b.Zone
                || !(a.Keywords ?? Array.Empty<string>()).SequenceEqual(b.Keywords ?? Array.Empty<string>()))
                return false;

            // CounterEntryDTO 是 class——SequenceEqual 比引用，必须逐值比较
            var ca = (a.Counters ?? Array.Empty<CounterEntryDTO>()).OrderBy(c => c.Key).ToArray();
            var cb = (b.Counters ?? Array.Empty<CounterEntryDTO>()).OrderBy(c => c.Key).ToArray();
            if (ca.Length != cb.Length) return false;
            for (int i = 0; i < ca.Length; i++)
                if (ca[i].Key != cb[i].Key || ca[i].Value != cb[i].Value) return false;
            return true;
        }

        // ============================================================ 快照对账 ============================================================

        /// <summary>快照对账：PlayerState 与活体、区域卡与 FromCard 双向、隐藏信息口径、字节确定性、往返无损。</summary>
        public static void VerifySnapshot(GameCore core, int viewerSeat)
        {
            var snapshot = NetSnapshotBuilder.Build(core, viewerSeat);

            // 对账一：PlayerState 各字段 == 活体
            foreach (var ps in snapshot.Players)
            {
                var player = ps.Seat == 0 ? core.Player1 : core.Player2;
                VerifySuite.Assert(ps.Life == player.Life && ps.MaxHealth == player.MaxHealth
                    && ps.DeckCount == core.ZoneManager.GetCards(player, Zone.Deck).Count
                    && ps.HandCount == core.ZoneManager.GetCards(player, Zone.Hand).Count
                    && ps.FatigueCount == player.FatigueCount
                    && ps.GraveyardCount == core.ZoneManager.GetCards(player, Zone.Graveyard).Count
                    && ps.ExileCount == core.ZoneManager.GetCards(player, Zone.Exile).Count
                    && ps.LandCap == core.ElementPool.GetLandCap(player)
                    && ps.IsAI == player.IsAI,
                    $"P{ps.Seat} PlayerState 与活体一致（viewer={viewerSeat}）");

                // bank 逐色比对（快照只列 >0 条目）
                foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
                {
                    int live = core.ElementPool.GetAvailableManaCount(type, player);
                    int sent = ps.ElementBank?.Where(e => e.ManaType == (int)type).Sum(e => e.Value) ?? 0;
                    if (live != sent) { VerifySuite.Assert(false, $"P{ps.Seat} bank {type}：live={live} sent={sent}"); break; }
                }
            }

            // 对账二：区域卡与 FromCard 现算 DTO 全等（活体真值）
            foreach (var zoneCards in snapshot.ZoneCards)
            {
                var player = zoneCards.Seat == 0 ? core.Player1 : core.Player2;
                var live = core.ZoneManager.GetCards(player, (Zone)zoneCards.Zone);
                VerifySuite.Assert(zoneCards.Cards.Length == live.Count,
                    $"区域 {zoneCards.Zone}(seat{zoneCards.Seat}) 卡数 {zoneCards.Cards.Length} == 活体 {live.Count}");
                for (int i = 0; i < live.Count; i++)
                {
                    var truth = SerializableRuntimeCardState.FromCard(live[i]);
                    // 元素池地牌的权威态在 PooledCard 包装（入池即清卡内字段——快照侧 BuildZone
                    // 已按包装覆盖 IsTapped/余量，对账真值同口径，否则横置地牌恒误报）
                    if ((Zone)zoneCards.Zone == Zone.ElementPool && core.ElementPool != null)
                    {
                        var wrap = core.ElementPool.GetPooledCards(player)?
                            .FirstOrDefault(pc => pc?.SourceCard != null
                                                  && pc.SourceCard.RuntimeId == live[i].RuntimeId);
                        if (wrap != null) truth.IsTapped = wrap.IsTapped;
                    }
                    VerifySuite.Assert(EqualCardState(zoneCards.Cards[i], truth),
                        $"区域 {zoneCards.Zone}[{i}] RuntimeId={live[i].RuntimeId} 卡状态与活体全等");
                }
            }

            // 对账三：隐藏信息口径（己方手牌可见、对方只见数量；牌库只数量）
            var myHand = snapshot.Hands.First(h => h.Seat == viewerSeat);
            var oppHand = snapshot.Hands.First(h => h.Seat != viewerSeat);
            var liveMine = core.ZoneManager.GetCards(viewerSeat == 0 ? core.Player1 : core.Player2, Zone.Hand);
            var liveOpp = core.ZoneManager.GetCards(viewerSeat == 0 ? core.Player2 : core.Player1, Zone.Hand);
            VerifySuite.Assert(myHand.Count == liveMine.Count
                && myHand.OwnRuntimeIds != null
                && myHand.OwnRuntimeIds.OrderBy(x => x).SequenceEqual(liveMine.Select(c => c.RuntimeId).OrderBy(x => x)),
                $"viewer{viewerSeat} 见己方手牌 RuntimeId 全集");
            VerifySuite.Assert(oppHand.Count == liveOpp.Count
                && (oppHand.OwnRuntimeIds == null || oppHand.OwnRuntimeIds.Length == 0),
                $"viewer{viewerSeat} 对对方手牌只见数量（{oppHand.Count}）");
            VerifySuite.Assert(snapshot.ZoneCards.All(z => (Zone)z.Zone != Zone.Deck), "牌库不进 ZoneCards（只数量）");

            // 对账四：字节确定性（同状态连拍两次，MemoryPack 字节全等——Counters 排序回归）
            var again = NetSnapshotBuilder.Build(core, viewerSeat);
            var b1 = MemoryPackSerializer.Serialize(snapshot);
            var b2 = MemoryPackSerializer.Serialize(again);
            VerifySuite.Assert(b1.SequenceEqual(b2), $"viewer{viewerSeat} 快照字节确定（{b1.Length}B）");

            // 对账五：往返无损
            var rt = RoundTripSnapshot(snapshot);
            VerifySuite.Assert(rt.ViewerSeat == snapshot.ViewerSeat
                && rt.Players.Length == snapshot.Players.Length
                && rt.ZoneCards.Length == snapshot.ZoneCards.Length
                && rt.Hands.Length == snapshot.Hands.Length,
                $"viewer{viewerSeat} 快照信封往返结构无损");
        }
    }

    /// <summary>
    /// 回环选择器（平移）：SelectAsync 内 MsgSelectRequest → 信封 → 解码（去程），
    /// 模拟客户端选索引，MsgSelectResponse → 信封 → 解码（回程）——全同步。
    /// </summary>
    internal sealed class LoopbackTargetSelector : ITargetSelectorEx
    {
        public readonly List<MsgSelectRequest> Seen = new List<MsgSelectRequest>();

        public void Attach() => TargetSelectionService.Current = this;

        public void Detach()
        {
            if (ReferenceEquals(TargetSelectionService.Current, this))
                TargetSelectionService.Current = null;
        }

        public UniTask<List<int>> SelectAsync(TargetSelectionRequest request, IReadOnlyList<string> labels,
            float timeoutSeconds)
        {
            var msg = new MsgSelectRequest
            {
                RequestId = Seen.Count + 1,
                ChooserSeat = NetEntityMapper.SeatOf(request.Chooser),
                Title = request.Title,
                Hint = request.Hint,
                AllowCancel = request.AllowCancel,
                TimeoutSeconds = request.TimeoutSeconds,
                Min = request.MinCount,
                Max = request.MaxCount,
                // 与 NetworkTargetSelector 同口径（2026-09-22 线上去文本）：实体反问不传 Labels
                Labels = request.Candidates.Count > 0 ? Array.Empty<string>() : labels.ToArray(),
                Candidates = request.Candidates.Select(NetEntityMapper.FromEntity).ToArray(),
            };

            // 去程：请求经完整协议栈到达"客户端"
            var wire = NetworkSerializer.SerializeMessage(NetworkMessageType.SelectRequest, msg);
            var decodedRequest = NetworkSerializer.DeserializePayload<MsgSelectRequest>(
                NetworkSerializer.DeserializeEnvelope(wire));
            Seen.Add(decodedRequest);

            // 模拟客户端：选前 Min 个索引（AutoSelect 同款策略）
            int pool = decodedRequest.Candidates.Length > 0 ? decodedRequest.Candidates.Length : decodedRequest.Labels.Length;
            int count = Math.Max(1, Math.Min(decodedRequest.Min, pool));
            var indices = Enumerable.Range(0, count).ToArray();

            // 回程：应答经完整协议栈返回"服务器"
            var resp = new MsgSelectResponse { RequestId = decodedRequest.RequestId, Indices = indices };
            var respWire = NetworkSerializer.SerializeMessage(NetworkMessageType.SelectResponse, resp);
            var decodedResponse = NetworkSerializer.DeserializePayload<MsgSelectResponse>(
                NetworkSerializer.DeserializeEnvelope(respWire));

            return UniTask.FromResult(decodedResponse.Indices.ToList());
        }

        public UniTask<int> SelectOneAsync(Player chooser, IReadOnlyList<string> options, string title,
            float timeoutSeconds)
            => UniTask.FromResult(0);

        public UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
            int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
            => throw new NotSupportedException("回环选择器只经 Ex 路径消费");
    }

    /// <summary>对拍记录（平移 M3RunRecord）。</summary>
    internal sealed class GameRunRecord
    {
        public NetEvent[] Events;
        public byte[] SnapshotSeat0;
        public byte[] SnapshotSeat1;
        public int FinalTurn;
        public int FirstSeat;
        public bool ReachedGameOver;
        public bool ClientDisconnected;
        public int SnapshotCount;
    }
}
