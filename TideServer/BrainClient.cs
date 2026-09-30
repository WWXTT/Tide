using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using CardCore;
using CardCore.Network;

namespace TideServer
{
    /// <summary>
    /// 回环 AI 客户端（2026-09-27 自 NetLobbyServer.AiLoopbackClient 提炼为公共组件）：
    /// 快照驱动的 NetClientBrain（修订锁步决策层）+ 帧收发循环，跑完整局到终局。
    /// 网关 AI 填位（直连 match 子进程）与 selftest（走大厅全链路）共用。
    ///
    /// 两种入口模式：
    /// - Direct：连上即 JoinRoom 任意空位（等价旧 AiLoopbackClient，match 子进程直连调试用）。
    /// - LobbyCreate / LobbyAutoMatch：先 LobbyHello 再建房/排队（走网关完整链路）。
    /// </summary>
    public sealed class BrainClient : IBrainChannel
    {
        private readonly FrameCodec.FrameDecoder _decoder = new FrameCodec.FrameDecoder();
        private readonly byte[] _buf = new byte[16 * 1024];
        private readonly NetClientBrain _brain = new NetClientBrain();

        private readonly TcpClient _tcp = new TcpClient();
        private NetworkStream _stream;
        private MsgRoomState _roomState;
        private MsgMatchManifest _manifest;
        private readonly List<NetEvent> _events = new List<NetEvent>();
        private bool _disconnected;

        public string LastError;
        public int ErrorsSeen => _brain.ErrorsSeen;
        public int SnapshotsSeen => _brain.SnapshotsSeen;
        public IReadOnlyList<NetEvent> Events => _events;
        public MsgRoomState RoomState => _roomState;

        /// <summary>终局判定：见过 GameOverEvent 且房间 Finished。</summary>
        public bool Done =>
            _events.Exists(e => e.EventType == nameof(GameOverEvent))
            && _roomState != null && _roomState.Phase == (int)NetRoomPhase.Finished;

        public bool Disconnected => _disconnected;

        public void RunDirect(string host, int port, string nickname, string[] deckIds, int turnCap)
            => Run(host, port, nickname, deckIds, turnCap, mode: "direct", roomName: null, stop: null);

        public void RunLobbyCreate(string host, int port, string nickname, string[] deckIds, int turnCap, string roomName)
            => Run(host, port, nickname, deckIds, turnCap, mode: "create", roomName: roomName, stop: null);

        public void RunLobbyAutoMatch(string host, int port, string nickname, string[] deckIds, int turnCap)
            => Run(host, port, nickname, deckIds, turnCap, mode: "automatch", roomName: null, stop: null);

        private void Run(string host, int port, string nickname, string[] deckIds, int turnCap,
            string mode, string roomName, VolatileBool stop)
        {
            try
            {
                _tcp.Connect(host, port);
                _stream = _tcp.GetStream();

                if (mode == "direct")
                {
                    Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "", WantSeat = -1, Nickname = nickname });
                }
                else
                {
                    Send(NetworkMessageType.LobbyHello, new MsgLobbyHello { Nickname = nickname });
                }

                var submit = new MsgDeckSubmit
                {
                    DeckName = $"ai-{nickname}",
                    CardIds = deckIds,
                    Digest = NetMatchHandshake.ComputeDeckDigest(deckIds),
                };
                bool lobbyEntrySent = false;

                while (!StopNow(stop) && !_disconnected && !Done)
                {
                    Receive(20);
                    if (mode == "direct")
                    {
                        if (_manifest == null && _roomState?.Phase == (int)NetRoomPhase.DeckSubmit)
                            Send(NetworkMessageType.DeckSubmit, submit); // 重复提交被拒无妨（一次性即成功）
                    }
                    else if (!lobbyEntrySent && _roomState == null)
                    {
                        // 收到过 LobbyState（大厅就绪）后发起建房/排队
                        if (mode == "create")
                            Send(NetworkMessageType.LobbyCreateRoom, new MsgLobbyCreateRoom { RoomName = roomName ?? $"{nickname}的房间" });
                        else
                            Send<object>(NetworkMessageType.LobbyAutoMatch, null);
                        lobbyEntrySent = true;
                    }
                    else if (_manifest == null && _roomState?.Phase == (int)NetRoomPhase.DeckSubmit)
                    {
                        Send(NetworkMessageType.DeckSubmit, submit);
                    }
                    _brain.Think(this, turnCap);
                }
            }
            catch (Exception ex)
            {
                LastError = $"{ex.GetType().Name}: {ex.Message}";
                Console.Error.WriteLine($"[brain:{nickname}] 异常退出：{ex}");
            }
            finally
            {
                try { _stream?.Close(); } catch { }
                try { _tcp.Close(); } catch { }
                _disconnected = true;
            }
        }

        private static bool StopNow(VolatileBool stop) => stop != null && stop.Value;

        /// <summary>可停止标记（网关 AI 填位线程退出用）。</summary>
        public sealed class VolatileBool { public volatile bool Value; }

        public void Send<T>(NetworkMessageType type, T payload) where T : class
        {
            if (_disconnected) return;
            try
            {
                var frame = FrameCodec.Frame(NetworkSerializer.BuildEnvelope(type, payload));
                _stream.Write(frame, 0, frame.Length);
                _stream.Flush();
            }
            catch (Exception ex)
            {
                _disconnected = true;
                Console.Error.WriteLine($"[brain] 发送 {type} 失败：{ex.GetType().Name}: {ex.Message}");
            }
        }

        private void Receive(int timeoutMs)
        {
            if (_disconnected || _stream == null) return;
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (!_stream.DataAvailable) { Thread.Sleep(5); continue; }
                int n;
                try { n = _stream.Read(_buf, 0, _buf.Length); }
                catch (Exception ex)
                {
                    _disconnected = true;
                    Console.Error.WriteLine($"[brain] 读失败：{ex.GetType().Name}: {ex.Message}");
                    return;
                }
                if (n <= 0) { _disconnected = true; return; }
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
                    _roomState = NetworkSerializer.DeserializePayload<MsgRoomState>(msg);
                    _brain.RoomState = _roomState;
                    break;
                case NetworkMessageType.LobbyState:
                    // 大厅就绪信号（触发建房/排队）
                    _brain.RoomState ??= new MsgRoomState { Phase = (int)NetRoomPhase.Waiting };
                    break;
                case NetworkMessageType.MatchManifest:
                    _manifest = NetworkSerializer.DeserializePayload<MsgMatchManifest>(msg);
                    _brain.MyEngineSeat = _manifest.OwnSeat;
                    break;
                case NetworkMessageType.GameStateSyncV2:
                    _brain.OnSnapshot(NetworkSerializer.DeserializePayload<MsgGameStateSync>(msg));
                    break;
                case NetworkMessageType.NetEventBatch:
                    var batch = NetworkSerializer.DeserializePayload<MsgNetEventBatch>(msg);
                    if (batch?.Events != null) _events.AddRange(batch.Events);
                    break;
                case NetworkMessageType.SelectRequest:
                    _brain.PendingSelects.Add(NetworkSerializer.DeserializePayload<MsgSelectRequest>(msg));
                    break;
                case NetworkMessageType.Error:
                    _brain.ErrorsSeen++;
                    break;
            }
        }
    }
}
