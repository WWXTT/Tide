using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using CardCore.Network;

namespace TideServer.Verify
{
    /// <summary>
    /// 验证测试客户端（平移 NetProtocolLoopbackVerifier.SocketTestClient）：真实 TCP + FrameCodec，
    /// 下行按类型分派到公开字段；可选挂 NetClientBrain（对拍局下行自动喂大脑）。
    /// 服务器版扩充：LobbyState 下行记录 + 大厅上行便捷口（V8 网关段用）。
    /// </summary>
    public sealed class VerifyClient : IBrainChannel
    {
        private readonly TcpClient _tcp = new TcpClient();
        private NetworkStream _stream;
        private readonly FrameCodec.FrameDecoder _decoder = new FrameCodec.FrameDecoder();
        private readonly byte[] _buf = new byte[16 * 1024];

        public NetClientBrain Brain;

        public int MyEngineSeat = -1;
        public int LastActedTurn;
        public bool Disconnected;
        public MsgRoomState LastRoomState;
        public MsgLobbyState LastLobbyState;
        public MsgMatchManifest Manifest;
        public MsgGameStateSync LastSnapshot;
        public MsgGameStateSync FirstSnapshot;
        public int SnapshotsReceived;
        public readonly List<NetEvent> Events = new List<NetEvent>();
        public readonly List<MsgSelectRequest> PendingSelects = new List<MsgSelectRequest>();
        public readonly List<MsgError> Errors = new List<MsgError>();

        public bool SawGameOver => Events.Any(e => e.EventType == nameof(CardCore.GameOverEvent));
        public bool RoomFinished => LastRoomState?.Phase == (int)NetRoomPhase.Finished;

        public void Connect(string host, int port)
        {
            _tcp.Connect(host, port);
            _stream = _tcp.GetStream();
        }

        public void Send<T>(NetworkMessageType type, T payload) where T : class
        {
            if (Disconnected || _stream == null) return;
            try
            {
                var frame = FrameCodec.Frame(NetworkSerializer.BuildEnvelope(type, payload));
                _stream.Write(frame, 0, frame.Length);
                _stream.Flush();
            }
            catch (Exception) { Disconnected = true; }
        }

        /// <summary>收一段时间（有数据续期），解码分派全部下行。</summary>
        public void Receive(int timeoutMs)
        {
            if (Disconnected || _stream == null) return;
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (!_stream.DataAvailable) { Thread.Sleep(2); continue; }
                int n;
                try { n = _stream.Read(_buf, 0, _buf.Length); }
                catch (Exception) { Disconnected = true; return; }
                if (n <= 0) { Disconnected = true; return; }
                _decoder.Append(_buf, 0, n);
                while (_decoder.TryDecode(out var msg))
                    Dispatch(msg);
                deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            }
        }

        private void Dispatch(NetworkMessage msg)
        {
            switch (msg.Type)
            {
                case NetworkMessageType.RoomState:
                    LastRoomState = NetworkSerializer.DeserializePayload<MsgRoomState>(msg);
                    if (Brain != null) Brain.RoomState = LastRoomState;
                    break;
                case NetworkMessageType.LobbyState:
                    LastLobbyState = NetworkSerializer.DeserializePayload<MsgLobbyState>(msg);
                    break;
                case NetworkMessageType.MatchManifest:
                    Manifest = NetworkSerializer.DeserializePayload<MsgMatchManifest>(msg);
                    if (Brain != null) Brain.MyEngineSeat = Manifest.OwnSeat;
                    break;
                case NetworkMessageType.GameStateSyncV2:
                    LastSnapshot = NetworkSerializer.DeserializePayload<MsgGameStateSync>(msg);
                    if (FirstSnapshot == null) FirstSnapshot = LastSnapshot;
                    SnapshotsReceived++;
                    Brain?.OnSnapshot(LastSnapshot);
                    break;
                case NetworkMessageType.NetEventBatch:
                    var batch = NetworkSerializer.DeserializePayload<MsgNetEventBatch>(msg);
                    if (batch?.Events != null) Events.AddRange(batch.Events);
                    break;
                case NetworkMessageType.SelectRequest:
                    var request = NetworkSerializer.DeserializePayload<MsgSelectRequest>(msg);
                    PendingSelects.Add(request);
                    Brain?.PendingSelects.Add(request);
                    break;
                case NetworkMessageType.Error:
                    var error = NetworkSerializer.DeserializePayload<MsgError>(msg);
                    Errors.Add(error);
                    if (Brain != null) Brain.ErrorsSeen++;
                    break;
            }
        }

        // ---- 大厅便捷口（V8 网关段）----

        public void Hello(string nickname)
            => Send(NetworkMessageType.LobbyHello, new MsgLobbyHello { Nickname = nickname });

        public void CreateRoom(string roomName)
            => Send(NetworkMessageType.LobbyCreateRoom, new MsgLobbyCreateRoom { RoomName = roomName });

        public void JoinRoomByLobby(string roomId)
            => Send(NetworkMessageType.LobbyJoinRoom, new MsgLobbyJoinRoom { RoomId = roomId });

        public void AutoMatch()
            => Send<object>(NetworkMessageType.LobbyAutoMatch, null);

        public void AddAi(string roomId, string nickname)
            => Send(NetworkMessageType.LobbyAddAi, new MsgLobbyAddAi { RoomId = roomId, Nickname = nickname });

        public void Close()
        {
            Disconnected = true;
            try { _tcp.Close(); } catch (Exception) { }
        }

        /// <summary>硬断（模拟拔线——对局作废用例）：不告而别。</summary>
        public void Abort()
        {
            Disconnected = true;
            try { _tcp.Client?.Close(); } catch (Exception) { }
            try { _tcp.Close(); } catch (Exception) { }
        }
    }

    /// <summary>窗口内循环泵 + 客户端收下行（覆盖"字节还在路上"的时序窗口——单拍泵会赶在读线程
    /// 入队前空转，断言随机失败；NetProtocolLoopbackVerifier.PumpAndReceive 同款）。</summary>
    internal static class VerifyPump
    {
        public static void PumpAndReceive(CardCore.Network.NetSessionServer server, int totalMs, params VerifyClient[] clients)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(totalMs);
            while (DateTime.UtcNow < deadline)
            {
                server.Pump();
                foreach (var c in clients)
                    c.Receive(10);
            }
        }
    }

    /// <summary>
    /// 脚本客户端决策（平移 NetProtocolLoopbackVerifier.ScriptClient，黑盒：只依据自己收到的
    /// 快照/反问）：反问优先应答 → 栈上让行 → Standby 跳过 → Main 尝试出牌/放地后 EndTurn。
    /// 非法尝试被服务器拒绝即收 Error 帧——拒绝面本身是验证点，脚本只保证回合必然流动。
    /// </summary>
    internal static class ScriptBrain
    {
        public static void Act(VerifyClient c)
        {
            if (c.PendingSelects.Count > 0)
            {
                var req = c.PendingSelects[0];
                c.PendingSelects.RemoveAt(0);
                int poolCount = req.Candidates != null && req.Candidates.Length > 0
                    ? req.Candidates.Length
                    : (req.Labels?.Length ?? 0);
                int count = Math.Max(1, Math.Min(req.Min, poolCount));
                c.Send(NetworkMessageType.SelectResponse, new MsgSelectResponse
                {
                    RequestId = req.RequestId,
                    Indices = Enumerable.Range(0, count).ToArray(),
                });
                return;
            }

            var snap = c.LastSnapshot;
            if (snap == null || snap.ActiveSeat != c.MyEngineSeat) return;

            // 栈上有对象且优先权在我 → 让行（响应窗口放行；要响应的客户端走 PlayCardInResponse 同一消息位）
            if (snap.PrioritySeat == c.MyEngineSeat && snap.StackV2 != null && snap.StackV2.Length > 0)
            {
                c.Send<object>(NetworkMessageType.IntentPassPriority, null);
                return;
            }

            var phase = (CardCore.PhaseType)snap.CurrentPhase;
            if (phase == CardCore.PhaseType.Standby)
            {
                c.Send<object>(NetworkMessageType.IntentSkipStandby, null);
                return;
            }
            if (phase == CardCore.PhaseType.Main)
            {
                if (c.LastActedTurn < snap.CurrentTurn)
                {
                    c.LastActedTurn = snap.CurrentTurn;
                    var handIds = snap.Hands.FirstOrDefault(h => h.Seat == c.MyEngineSeat)?.OwnRuntimeIds;
                    if (handIds != null && handIds.Length > 0)
                    {
                        c.Send(NetworkMessageType.IntentPlayCard, new MsgIntentPlayCard
                        {
                            CardRuntimeId = handIds[0],
                            FromZone = (int)CardCore.Zone.Hand,
                            ModeIndex = 0,
                        });
                        c.Send(NetworkMessageType.IntentAddToElementPool,
                            new MsgIntentAddToElementPool { CardRuntimeId = handIds[handIds.Length - 1] });
                    }
                    return; // 本帧动作上栈，等快照同步后再 EndTurn
                }
                c.Send<object>(NetworkMessageType.IntentEndTurn, null);
            }
        }

        /// <summary>取事件实体字段的座位（仅玩家引用有座位；其余 null）。</summary>
        public static int? SeatOfParam(NetEvent e, string fieldName)
        {
            var p = e.Params?.FirstOrDefault(x => x.FieldName == fieldName);
            if (p == null || p.Kind != NetParamKind.Entity || p.EntityRefs == null || p.EntityRefs.Length == 0)
                return null;
            var r = p.EntityRefs[0];
            return r != null && r.IsPlayer ? r.Seat : (int?)null;
        }

        /// <summary>字段被隐藏过滤（整体丢弃）或被置 Null（字段在、值隐）。</summary>
        public static bool HiddenOrAbsent(NetEvent e, string fieldName)
        {
            var p = e.Params?.FirstOrDefault(x => x.FieldName == fieldName);
            if (p == null) return true;
            return p.Kind == NetParamKind.Null;
        }
    }
}
