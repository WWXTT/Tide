using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using CardCore.Serialization;

namespace CardCore.Network
{
    /// <summary>
    /// 大厅服务器组合根（L1，2026-09-24：房间列表 + 自动匹配 + AI 填位，见 网络协议.md §13）：
    /// NetTcpHost（传输）+ 大厅相位（未入房连接/匹配队列）+ 单个 NetRoom（复用 M2/M3 全部房间逻辑）。
    ///
    /// 宿主形态（单进程单局是引擎硬约束——GameCore.Instance/EventManager.Instance 进程单例）：
    /// 大厅 + 游戏房同进程**串行**——房间占用期间创建/自动匹配排队等待，房间回收
    /// （Finished + 全员离开 → ResetIfEmpty 回 Waiting）后队列自动补位。真并发多局需专用服务器
    /// 构建（每局一进程，后置里程碑）。
    ///
    /// 消息路由：大厅消息（130-135）在本层处理；JoinRoom/DeckSubmit/intent/反问等原样转房间——
    /// 直连 JoinRoom(112) 的 M2 路径完全兼容。
    ///
    /// AI 填位：服务器向自身回环（127.0.0.1:Port）发起 NetClientBrain 客户端连接（独立线程），
    /// 对房间而言就是一个普通 TCP 客户端——走完整 JoinRoom/DeckSubmit/锁步链路，零特殊逻辑。
    ///
    /// 线程模型与 NetSessionServer 同：IO 线程只入队，一切大厅/房间状态只在 Pump（逻辑线程）碰；
    /// AI 回环客户端是独立 TCP 对端，不触碰服务器状态。
    /// </summary>
    public sealed class NetLobbyServer
    {
        private readonly NetTcpHost _host = new NetTcpHost();
        private readonly NetRoom _room;
        private readonly Action<string> _log;

        private string _roomName = "";                       // 展示名（创建者命名；房间回收时清空）
        private readonly List<NetClientConnection> _lobby = new List<NetClientConnection>(); // 大厅相位连接
        private readonly List<NetClientConnection> _queue = new List<NetClientConnection>();  // 自动匹配队列
        private readonly List<AiFiller> _ais = new List<AiFiller>();
        private bool _lobbyDirty = true;
        private int _aiCounter;

        public NetLobbyServer(string roomId, Action<string> log = null, int? seed = null)
        {
            _room = new NetRoom(roomId, seed);
            _log = log;
        }

        public NetRoom Room => _room;
        public int Port => _host.Port;

        /// <summary>是否有大厅实例在运行（2026-09-24）：UIBootstrap.Update 的引擎驱动闸——
        /// 同进程宿主对局期间，GameCore 由本服务器泵独占驱动，UI 层每帧 Update 会
        /// 双驱动引擎（旧 GameLoopController 自由滑相位口径下即整局相位空转）。</summary>
        public static bool IsRunning { get; private set; }

        /// <summary>开始监听（port=0 由 OS 分配空闲口）。</summary>
        public void Start(int port)
        {
            _host.Start(port);
            IsRunning = true;
            _log?.Invoke($"[NetLobby] 大厅服务器监听 0.0.0.0:{_host.Port}（单房串行）");
        }

        /// <summary>停机：关监听、断全部连接、停 AI 回环线程。</summary>
        public void Stop()
        {
            IsRunning = false;
            foreach (var ai in _ais)
            {
                ai.Stop = true;
                try { ai.Tcp?.Close(); } catch { }
            }
            _ais.Clear();
            _host.Stop();
            _log?.Invoke("[NetLobby] 已停止");
        }

        /// <summary>
        /// 逻辑线程单步（宿主每 tick 调用）：连接收发 → 大厅/房间分派 → 匹配补位 →
        /// 引擎推进 → 下行出队 → 大厅状态广播。
        /// </summary>
        public void Pump()
        {
            // ① 连接生命周期
            foreach (var dropped in _host.Poll())
                OnDisconnect(dropped);
            _room.ResetIfEmpty();
            if (_room.IsEmpty && _room.Phase == NetRoomPhase.Waiting && !string.IsNullOrEmpty(_roomName))
            {
                _roomName = ""; // 房间回收：展示名清空、队列可补位
                _lobbyDirty = true;
            }
            _ais.RemoveAll(a => a.Thread == null || !a.Thread.IsAlive);

            // ② 上行分派：大厅消息本层处理，其余转房间（M2 兼容）
            foreach (var conn in _host.Connections)
            {
                while (conn.TryReceive(out var msg))
                {
                    if (IsLobbyMessage(msg.Type))
                        HandleLobbyMessage(conn, msg);
                    else
                        _room.OnMessage(GameCore.Instance, conn, msg);
                }
            }

            // ③ 自动匹配补位（房间回收/半满均可吸收队列）
            TryPairQueue();

            // ④ 房间引擎推进 + 下行
            _room.PumpEngine(GameCore.Instance);
            _room.FlushDownlink(GameCore.Instance);

            // ⑤ 房间指纹变化 → 大厅脏（直连 JoinRoom/M2 路径不经过大厅上行，靠对比兜底刷新列表）
            var state = _room.State;
            var fingerprint = ((int)_room.Phase, state.Players.Count(p => p.Connected), state.SpectatorCount);
            if (fingerprint != _roomFingerprint)
            {
                _roomFingerprint = fingerprint;
                _lobbyDirty = true;
            }

            // ⑥ 大厅状态广播
            if (_lobbyDirty)
            {
                _lobbyDirty = false;
                BroadcastLobbyState();
            }
        }

        private (int phase, int players, int spectators) _roomFingerprint = (-1, -1, -1);

        private static bool IsLobbyMessage(NetworkMessageType type)
        {
            return type >= NetworkMessageType.LobbyHello && type <= NetworkMessageType.LobbyAddAi;
        }

        // ============================================================ 大厅上行 ============================================================

        private void HandleLobbyMessage(NetClientConnection conn, NetworkMessage msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case NetworkMessageType.LobbyHello:
                    {
                        var hello = NetworkSerializer.DeserializePayload<MsgLobbyHello>(msg);
                        var nickname = string.IsNullOrEmpty(hello?.Nickname) ? $"玩家{Environment.TickCount % 10000}" : hello.Nickname;
                        conn.Nickname = nickname;
                        if (!_lobby.Contains(conn)) _lobby.Add(conn);
                        conn.SendPayload(NetworkMessageType.LobbyState, BuildLobbyState());
                        _lobbyDirty = true;
                        break;
                    }

                    case NetworkMessageType.LobbyCreateRoom:
                    {
                        var create = NetworkSerializer.DeserializePayload<MsgLobbyCreateRoom>(msg);
                        if (_room.Phase != NetRoomPhase.Waiting || _room.FreeChairCount < 2)
                        { SendLobbyError(conn, "房间被占用（单房串行——对局进行中/等待回收），请排队或稍后", "LobbyCreateRoom"); return; }
                        _roomName = string.IsNullOrEmpty(create?.RoomName) ? $"房间{Environment.TickCount % 10000}" : create.RoomName;
                        _room.HandleJoin(conn, new MsgJoinRoom { WantSeat = -1, Nickname = conn.Nickname });
                        _lobby.Remove(conn);
                        _lobbyDirty = true;
                        _log?.Invoke($"[NetLobby] 创建房间「{_roomName}」：{conn.Nickname} 就座");
                        break;
                    }

                    case NetworkMessageType.LobbyJoinRoom:
                    {
                        var joinLobby = NetworkSerializer.DeserializePayload<MsgLobbyJoinRoom>(msg);
                        if (joinLobby?.RoomId != _room.RoomId || _room.Phase != NetRoomPhase.Waiting || _room.FreeChairCount <= 0)
                        { SendLobbyError(conn, "房间不存在/已满/已开局（单房串行）", "LobbyJoinRoom"); return; }
                        _room.HandleJoin(conn, new MsgJoinRoom { WantSeat = -1, Nickname = conn.Nickname });
                        _lobby.Remove(conn);
                        _queue.Remove(conn); // 从列表加入=顺带退出匹配队列
                        _lobbyDirty = true;
                        break;
                    }

                    case NetworkMessageType.LobbyAutoMatch:
                    {
                        if (_queue.Contains(conn))
                        {
                            _queue.Remove(conn); // 重复发送 = 取消排队
                            _log?.Invoke($"[NetLobby] {conn.Nickname} 取消匹配");
                        }
                        else
                        {
                            _queue.Add(conn);
                            _log?.Invoke($"[NetLobby] {conn.Nickname} 加入匹配队列");
                        }
                        _lobbyDirty = true;
                        break;
                    }

                    case NetworkMessageType.LobbyAddAi:
                    {
                        var addAi = NetworkSerializer.DeserializePayload<MsgLobbyAddAi>(msg);
                        if (addAi?.RoomId != _room.RoomId || _room.Phase != NetRoomPhase.Waiting || _room.FreeChairCount <= 0)
                        { SendLobbyError(conn, "房间里没有空位（或未开局等待中），无法 AI 填位", "LobbyAddAi"); return; }
                        SpawnAiFiller(addAi.Nickname);
                        _lobbyDirty = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLobbyError(conn, $"大厅消息处理异常：{ex.GetType().Name}: {ex.Message}", msg.Type.ToString());
            }
        }

        /// <summary>自动匹配补位：房间有空位时从队列头取连接入座（半满吸收 1 人、空房吸收 2 人）。</summary>
        private void TryPairQueue()
        {
            if (_queue.Count == 0) return;
            if (_room.Phase != NetRoomPhase.Waiting) return;

            while (_queue.Count > 0 && _room.FreeChairCount > 0)
            {
                var conn = _queue[0];
                _queue.RemoveAt(0);
                _lobby.Remove(conn);
                _room.HandleJoin(conn, new MsgJoinRoom { WantSeat = -1, Nickname = conn.Nickname });
                _lobbyDirty = true;
                _log?.Invoke($"[NetLobby] 自动匹配：{conn.Nickname} 入座");
            }
        }

        private void OnDisconnect(NetClientConnection conn)
        {
            if (_lobby.Remove(conn)) _lobbyDirty = true;
            if (_queue.Remove(conn)) _lobbyDirty = true;
        }

        // ============================================================ 大厅下行 ============================================================

        private MsgLobbyState BuildLobbyState()
        {
            var state = _room.State; // 与成员广播同构
            var occupied = state.Players.Any(p => p.Connected) || state.SpectatorCount > 0
                || _room.Phase != NetRoomPhase.Waiting;
            var rooms = occupied || !string.IsNullOrEmpty(_roomName)
                ? new[] { new MsgLobbyRoomInfo
                {
                    RoomId = state.RoomId,
                    Name = string.IsNullOrEmpty(_roomName) ? state.RoomId : _roomName,
                    Phase = (int)_room.Phase,
                    PlayerCount = state.Players.Count(p => p.Connected),
                    Nicknames = state.Players.Where(p => p.Connected).Select(p => p.Nickname).ToArray(),
                    SpectatorCount = state.SpectatorCount,
                } }
                : new MsgLobbyRoomInfo[0];
            return new MsgLobbyState { Rooms = rooms, QueuedCount = _queue.Count };
        }

        private void BroadcastLobbyState()
        {
            var state = BuildLobbyState();
            foreach (var conn in _lobby.ToList())
                conn.SendPayload(NetworkMessageType.LobbyState, state);
        }

        private void SendLobbyError(NetClientConnection conn, string reason, string context)
            => conn.SendPayload(NetworkMessageType.Error, new MsgError { Reason = reason, Context = context });

        // ============================================================ AI 填位（回环客户端） ============================================================

        /// <summary>AI 回环填位宿主记录（Stop 时关 socket 让线程退出）。</summary>
        private sealed class AiFiller
        {
            public Thread Thread;
            public TcpClient Tcp;
            public volatile bool Stop;
        }

        /// <summary>
        /// 向自身回环发起一个 NetClientBrain 客户端（独立线程）：JoinRoom 任意空位 →
        /// 卡组=本地卡表前 30 张非重复（与 NetClientHost.Main 同口径）→ 锁步打到终局。
        /// 对房间而言它就是一个普通 TCP 客户端，走完整握手与校验。
        /// </summary>
        private void SpawnAiFiller(string nickname)
        {
            var deckIds = SynergyUI.CardCatalog.LoadAll().Select(c => c.ID).Distinct().Take(30).ToArray();
            if (deckIds.Length == 0)
            {
                _log?.Invoke("[NetLobby] AI 填位失败：本地卡表为空");
                return;
            }
            var name = string.IsNullOrEmpty(nickname) ? $"AI-{++_aiCounter}" : nickname;
            var filler = new AiFiller();
            filler.Thread = new Thread(() => RunAiLoopback(filler, name, deckIds)) { IsBackground = true };
            filler.Thread.Start();
            _ais.Add(filler);
            _log?.Invoke($"[NetLobby] AI 填位：{name} 正在回环入座（卡组 {deckIds.Length} 张）");
        }

        private void RunAiLoopback(AiFiller filler, string nickname, string[] deckIds)
        {
            try
            {
                var client = new AiLoopbackClient(filler);
                filler.Tcp = client.Tcp; // 先登记：Stop() 关 socket 才能在运行中生效
                client.Run("127.0.0.1", _host.Port, nickname, deckIds, turnCap: 60);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[NetLobby] AI 回环客户端退出（{nickname}）：{ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>回环 AI 客户端（NetClientHost.Client 的运行时精简版：去命令行/日志依赖，加 Stop 位）。</summary>
        private sealed class AiLoopbackClient : IBrainChannel
        {
            private readonly AiFiller _owner;
            private readonly FrameCodec.FrameDecoder _decoder = new FrameCodec.FrameDecoder();
            private readonly byte[] _buf = new byte[16 * 1024];
            private readonly NetClientBrain _brain = new NetClientBrain();

            public readonly TcpClient Tcp = new TcpClient();
            private NetworkStream _stream;
            private MsgRoomState _roomState;
            private MsgMatchManifest _manifest;
            private readonly List<NetEvent> _events = new List<NetEvent>();
            private bool _disconnected;

            private bool Done => _events.Any(e => e.EventType == nameof(GameOverEvent))
                && _roomState?.Phase == (int)NetRoomPhase.Finished;

            public AiLoopbackClient(AiFiller owner) { _owner = owner; }

            public void Run(string host, int port, string nickname, string[] deckIds, int turnCap)
            {
                Tcp.Connect(host, port);
                _stream = Tcp.GetStream();

                Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "", WantSeat = -1, Nickname = nickname });
                var submit = new MsgDeckSubmit
                {
                    DeckName = $"ai-{nickname}",
                    CardIds = deckIds,
                    Digest = NetMatchHandshake.ComputeDeckDigest(deckIds),
                };

                while (!_owner.Stop && !_disconnected && !Done)
                {
                    Receive(20);
                    if (_manifest == null && _roomState?.Phase == (int)NetRoomPhase.DeckSubmit)
                        Send(NetworkMessageType.DeckSubmit, submit); // 重复提交被拒无妨（一次性即成功）
                    _brain.Think(this, turnCap);
                }
            }

            public void Send<T>(NetworkMessageType type, T payload) where T : class
            {
                if (_disconnected) return;
                try
                {
                    var frame = FrameCodec.Frame(NetworkSerializer.BuildEnvelope(type, payload));
                    _stream.Write(frame, 0, frame.Length);
                    _stream.Flush();
                }
                catch (Exception) { _disconnected = true; }
            }

            private void Receive(int timeoutMs)
            {
                if (_disconnected || _stream == null) return;
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline && !_owner.Stop)
                {
                    if (!_stream.DataAvailable) { Thread.Sleep(5); continue; }
                    int n;
                    try { n = _stream.Read(_buf, 0, _buf.Length); }
                    catch (Exception) { _disconnected = true; return; }
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
}
