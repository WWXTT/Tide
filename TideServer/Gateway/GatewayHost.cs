using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using CardCore.Network;

namespace TideServer.Gateway
{
    /// <summary>
    /// 网关宿主（2026-09-27 独立权威服务器，网络协议.md §14）：公网口受理大厅消息
    /// （130-135），匹配/建房后 spawn match 子进程并为每个客户端建立字节级中继隧道——
    /// 线格式零改动、Unity 客户端零改动（同一条连接从大厅用到终局，连接移交语义与
    /// NetLobbyServer §13.3 一致）。
    ///
    /// 线程模型（沿用单逻辑线程纪律）：
    /// - IO 线程只入队：客户端读线程（大厅期解帧入队 / 入座后帧转发）、隧道下行线程（原始字节）、
    ///   子进程 stdout 读线程（行协议事件入队）、accept 由泵轮询 Pending 非阻塞完成。
    /// - 一切大厅/房间列表/子进程生命周期状态只在 Pump 里碰。
    /// </summary>
    internal sealed class GatewayHost
    {
        private readonly Dictionary<string, string> _options;
        private readonly int _tickMs;
        private readonly int? _seed;                     // 钉种子（selftest 对拍）；null=每局随机

        private TcpListener _listener;
        private readonly List<GatewayClient> _clients = new List<GatewayClient>();      // 全部在线连接
        private readonly List<GatewayClient> _lobby = new List<GatewayClient>();        // 大厅相位连接
        private readonly List<GatewayClient> _queue = new List<GatewayClient>();        // 自动匹配队列
        private readonly List<GatewayRoom> _rooms = new List<GatewayRoom>();
        private readonly List<BrainClient.VolatileBool> _aiStops = new List<BrainClient.VolatileBool>();
        private bool _lobbyDirty = true;
        private int _roomCounter;
        private volatile bool _stopping;

        private readonly ConcurrentQueue<(GatewayClient client, NetworkMessage msg)> _uplink =
            new ConcurrentQueue<(GatewayClient, NetworkMessage)>();
        private readonly ConcurrentQueue<GatewayClient> _disconnects = new ConcurrentQueue<GatewayClient>();
        private readonly ConcurrentQueue<(GatewayRoom room, string line)> _childLines =
            new ConcurrentQueue<(GatewayRoom, string)>();

        public GatewayHost(Dictionary<string, string> options)
        {
            _options = options;
            _tickMs = Program.GetInt(options, "tick-ms", 15);
            _seed = Program.GetNullableInt(options, "seed");
        }

        public int Port { get; private set; }

        // ============================================================ 静态入队口（IO 线程侧） ============================================================

        // 实例侧入队（静态口转投当前实例——单网关进程单实例假设）
        private static GatewayHost _current;

        public static void EnqueueUplink(GatewayClient client, NetworkMessage msg)
            => _current?._uplink.Enqueue((client, msg));

        public static void EnqueueDisconnect(GatewayClient client)
            => _current?._disconnects.Enqueue(client);

        /// <summary>大厅层上行（130-135，值域同 NetLobbyServer.IsLobbyMessage）——
        /// 入座后也归网关处理（见 GatewayClient.ReadLoop 注释：房内 LobbyAddAi 死路修复）。</summary>
        public static bool IsLobbyUplink(NetworkMessageType type)
            => type >= NetworkMessageType.LobbyHello && type <= NetworkMessageType.LobbyAddAi;

        // ============================================================ 主循环 ============================================================

        public int Run()
        {
            int port = Program.GetInt(_options, "port", 7777);
            _current = this;

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _stopping = true;
            };

            // Job Object：网关进程死亡（含被强杀/崩溃）时连带杀掉全部 match 子进程——无孤儿对局进程
            if (OperatingSystem.IsWindows()) JobObject.KillChildrenOnClose();

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Console.Error.WriteLine($"[gateway] 监听 0.0.0.0:{Port}（大厅 + 每局一进程监督者）");
            Console.WriteLine($"READY port={Port}");
            Console.Out.Flush();

            try
            {
                while (!_stopping)
                {
                    Pump();
                    Thread.Sleep(_tickMs);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[gateway] 泵异常: {ex}");
                return 1;
            }
            finally
            {
                Shutdown();
            }
            return 0;
        }

        private void Pump()
        {
            // ① 新连接（非阻塞 accept）
            while (_listener.Pending())
            {
                var tcp = _listener.AcceptTcpClient();
                var client = new GatewayClient(tcp) { Stream = tcp.GetStream() };
                _clients.Add(client);
                client.ReaderThread.Start();
                Console.Error.WriteLine($"[gateway] 新连接 {((IPEndPoint)tcp.Client.RemoteEndPoint)?.Address}");
            }

            // ② 断线回收
            while (_disconnects.TryDequeue(out var dropped))
            {
                _clients.Remove(dropped);
                _lobby.Remove(dropped);
                _queue.Remove(dropped);
                OnClientLeftRoom(dropped);
                _lobbyDirty = true;
            }

            // ③ 子进程行协议事件
            while (_childLines.TryDequeue(out var item))
                OnChildLine(item.room, item.line);

            // ④ 大厅上行
            while (_uplink.TryDequeue(out var item))
                HandleUplink(item.client, item.msg);

            // ⑤ 自动匹配补位
            TryPairQueue();

            // ⑥ 房间生命周期（崩溃清理/空房回收/超时杀）
            ManageRooms();

            // ⑦ 大厅状态广播
            if (_lobbyDirty)
            {
                _lobbyDirty = false;
                BroadcastLobbyState();
            }
        }

        // ============================================================ 大厅上行处理 ============================================================

        private void HandleUplink(GatewayClient client, NetworkMessage msg)
        {
            if (client.Closed) return;
            Console.Error.WriteLine($"[gateway] 上行 {msg.Type}（{client.Nickname}）");
            try
            {
                switch (msg.Type)
                {
                    case NetworkMessageType.LobbyHello:
                    {
                        var hello = NetworkSerializer.DeserializePayload<MsgLobbyHello>(msg);
                        client.Nickname = string.IsNullOrEmpty(hello?.Nickname)
                            ? $"玩家{Environment.TickCount % 10000}" : hello.Nickname;
                        if (!_lobby.Contains(client)) _lobby.Add(client);
                        Send(client, NetworkMessageType.LobbyState, BuildLobbyState());
                        _lobbyDirty = true;
                        break;
                    }

                    case NetworkMessageType.LobbyCreateRoom:
                    {
                        var create = NetworkSerializer.DeserializePayload<MsgLobbyCreateRoom>(msg);
                        if (client.Room != null)
                        { SendError(client, "本连接已在房间里", "LobbyCreateRoom"); break; }
                        var room = CreateRoom(string.IsNullOrEmpty(create?.RoomName)
                            ? $"房间{Environment.TickCount % 10000}" : create.RoomName);
                        SeatClient(room, client);
                        Console.Error.WriteLine($"[gateway] 创建房间「{room.Name}」：{client.Nickname} 就座（子进程 :{room.Port}）");
                        break;
                    }

                    case NetworkMessageType.LobbyJoinRoom:
                    {
                        if (client.Room != null)
                        { SendError(client, "本连接已在房间里", "LobbyJoinRoom"); break; }
                        var join = NetworkSerializer.DeserializePayload<MsgLobbyJoinRoom>(msg);
                        var room = join == null ? null : _rooms.FirstOrDefault(r => !r.Dead && r.RoomId == join.RoomId);
                        if (room == null || !IsJoinable(room))
                        { SendError(client, "房间不存在/已满/已开局", "LobbyJoinRoom"); break; }
                        SeatClient(room, client);
                        break;
                    }

                    case NetworkMessageType.LobbyAutoMatch:
                    {
                        if (client.Room != null)
                        { SendError(client, "本连接已在房间里（先离开再排队）", "LobbyAutoMatch"); break; }
                        if (_queue.Contains(client))
                        {
                            _queue.Remove(client);
                            Console.Error.WriteLine($"[gateway] {client.Nickname} 取消匹配");
                        }
                        else _queue.Add(client);
                        _lobbyDirty = true;
                        break;
                    }

                    case NetworkMessageType.LobbyAddAi:
                    {
                        var addAi = NetworkSerializer.DeserializePayload<MsgLobbyAddAi>(msg);
                        var room = addAi == null ? null : _rooms.FirstOrDefault(r => !r.Dead && r.RoomId == addAi.RoomId);
                        if (room == null || !IsJoinable(room))
                        { SendError(client, "房间里没有空位（或已开局），无法 AI 填位", "LobbyAddAi"); break; }
                        SpawnAiFiller(room, addAi.Nickname);
                        _lobbyDirty = true;
                        break;
                    }

                    case NetworkMessageType.Ping:
                        Send<object>(client, NetworkMessageType.Pong, null);
                        break;

                    default:
                        // 大厅相位只受理大厅消息；直连 JoinRoom(112) 调试路径请直连 match 子进程端口
                        SendError(client, $"网关大厅相位不受理 {msg.Type}（直连调试请连 match 子进程端口）", msg.Type.ToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                SendError(client, $"大厅消息处理异常：{ex.GetType().Name}: {ex.Message}", msg.Type.ToString());
            }
        }

        /// <summary>可入座判定：Waiting 或 Starting（子进程就绪中——READY 已解析即可入座，
        /// PHASE 行被泵消费前存在竞态窗口，真实客户端快速匹配会踩）且有空椅。</summary>
        private static bool IsJoinable(GatewayRoom r)
            => !r.Dead && (r.Phase == "Waiting" || r.Phase == "Starting") && r.FreeChairCount > 0;

        /// <summary>自动匹配：优先补进现有 Waiting 空位房；无房可补且有排队者 → 开新房入座。</summary>
        private void TryPairQueue()
        {
            while (_queue.Count > 0)
            {
                var room = _rooms.FirstOrDefault(IsJoinable);
                if (room == null)
                {
                    if (_queue.Count < 1) break;
                    room = CreateRoom($"自动匹配{++_roomCounter}");
                }
                var head = _queue[0];
                _queue.RemoveAt(0);
                _lobby.Remove(head);
                SeatClient(room, head);
                Console.Error.WriteLine($"[gateway] 自动匹配：{head.Nickname} 入座 {room.RoomId}");
            }
        }

        // ============================================================ 入座与隧道 ============================================================

        private void SeatClient(GatewayRoom room, GatewayClient client)
        {
            int seat = room.Chairs[0] == null ? 0 : 1;
            if (room.Chairs[seat] != null)
            {
                SendError(client, "房间已满", "Seat");
                return;
            }

            room.Chairs[seat] = client;
            room.ChairNicknames[seat] = client.Nickname;
            client.Room = room;
            client.ChairSeat = seat;
            _lobby.Remove(client);
            _queue.Remove(client);
            _lobbyDirty = true;

            // 隧道：连子进程 → 合成 JoinRoom（网关代发，昵称=大厅昵称）→ 交下行中继线程
            var tunnel = new TcpClient();
            tunnel.Connect(IPAddress.Loopback, room.Port);
            var tunnelStream = tunnel.GetStream();
            var join = NetworkSerializer.BuildEnvelope(NetworkMessageType.JoinRoom,
                new MsgJoinRoom { RoomId = room.RoomId, WantSeat = seat, Nickname = client.Nickname });
            var frame = FrameCodec.Frame(join);
            tunnelStream.Write(frame, 0, frame.Length);
            tunnelStream.Flush();

            client.Tunnel = tunnelStream;
            var thread = new Thread(() => GatewayClient.TunnelDownLoop(client, tunnelStream)) { IsBackground = true };
            thread.Start();
        }

        private void OnClientLeftRoom(GatewayClient client)
        {
            var room = client.Room;
            if (room == null) return;
            client.Room = null;
            for (int i = 0; i < 2; i++)
            {
                if (ReferenceEquals(room.Chairs[i], client))
                {
                    room.Chairs[i] = null;
                    room.ChairNicknames[i] = "";
                }
            }
            _lobbyDirty = true;
        }

        // ============================================================ 房间/子进程生命周期 ============================================================

        private GatewayRoom CreateRoom(string name)
        {
            var room = new GatewayRoom
            {
                RoomId = $"room-{++_roomCounter}",
                Name = name,
            };
            SpawnChild(room);
            _rooms.Add(room);
            _lobbyDirty = true;
            return room;
        }

        private void SpawnChild(GatewayRoom room)
        {
            var exe = Environment.ProcessPath;
            var seed = _seed ?? new Random().Next();
            // --room-id 透传：子进程 RoomState 广播的 RoomId 与网关一致（客户端回传 RoomId 命中网关房间）
            var psi = new ProcessStartInfo(exe,
                $"match --port 0 --room-id {room.RoomId} --seed {seed} --tick-ms {_tickMs}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var child = Process.Start(psi);
            room.Child = child;

            // stdout 行协议读线程（READY/PHASE/BEAT/EXIT 入队泵处理；日志行忽略——诊断日志在 stderr）
            var reader = new Thread(() =>
            {
                try
                {
                    string line;
                    while (!room.Dead && (line = child.StandardOutput.ReadLine()) != null)
                        _childLines.Enqueue((room, line));
                }
                catch { }
            }) { IsBackground = true };
            reader.Start();

            // stderr 诊断日志透传（带房间前缀）
            var errReader = new Thread(() =>
            {
                try
                {
                    string line;
                    while (!room.Dead && (line = child.StandardError.ReadLine()) != null)
                        Console.Error.WriteLine($"[{room.RoomId}] {line}");
                }
                catch { }
            }) { IsBackground = true };
            errReader.Start();

            // READY 握手（阻塞等子进程上报端口——本机子进程启动毫秒级；超时杀掉重来）。
            // 他房行先收进本地列表、排完本轮再统一回插——直接"出队又回插队尾"会在队列里
            // 只剩他房行时活锁（同一行被无限取出放回，内层 while 永不退出，泵线程死转）。
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (room.Port == 0 && DateTime.UtcNow < deadline)
            {
                var deferred = new List<(GatewayRoom room, string line)>();
                while (_childLines.TryDequeue(out var item))
                {
                    if (ReferenceEquals(item.room, room)) OnChildLine(room, item.line);
                    else deferred.Add(item);
                }
                foreach (var again in deferred) _childLines.Enqueue(again);
                if (room.Port != 0) break;
                Thread.Sleep(10);
            }
            if (room.Port == 0)
            {
                Console.Error.WriteLine($"[gateway] 子进程 {room.RoomId} READY 超时——标记死亡");
                room.Dead = true;
                try { child.Kill(); } catch { }
            }
        }

        private void OnChildLine(GatewayRoom room, string line)
        {
            room.LastStdoutAt = DateTime.UtcNow;
            if (line.StartsWith("READY port=", StringComparison.Ordinal))
            {
                if (int.TryParse(line.Substring("READY port=".Length).Trim(), out var port))
                    room.Port = port;
            }
            else if (line.StartsWith("PHASE ", StringComparison.Ordinal))
            {
                var phase = line.Substring("PHASE ".Length).Trim();
                if (phase != room.Phase)
                {
                    room.Phase = phase;
                    if (phase == "Playing") room.HasHostedMatch = true;
                    if (phase == "Waiting") room.AiCount = 0;
                    _lobbyDirty = true;
                    Console.Error.WriteLine($"[gateway] {room.RoomId} → {phase}");
                }
            }
            else if (line.StartsWith("EXIT ", StringComparison.Ordinal))
            {
                room.Dead = true;
                Console.Error.WriteLine($"[gateway] {room.RoomId} 子进程退出（{line.Substring(5)}）");
                CloseRoomClients(room);
                _lobbyDirty = true;
            }
            // BEAT / 其他行：仅保活记账
        }

        private void ManageRooms()
        {
            for (int i = _rooms.Count - 1; i >= 0; i--)
            {
                var room = _rooms[i];
                var now = DateTime.UtcNow;

                if (room.Dead || room.Child == null || room.Child.HasExited)
                {
                    if (!room.Dead)
                    {
                        room.Dead = true;
                        Console.Error.WriteLine($"[gateway] {room.RoomId} 子进程异常退出（exit={room.Child?.ExitCode}）");
                    }
                    CloseRoomClients(room);
                    CleanupRoom(room, i);
                    continue;
                }

                // 僵死探测：60s 无任何 stdout 行（含 BEAT）→ 强杀
                if ((now - room.LastStdoutAt).TotalSeconds > 60)
                {
                    Console.Error.WriteLine($"[gateway] {room.RoomId} 子进程僵死（60s 无心跳）——强杀");
                    try { room.Child.Kill(); } catch { }
                    room.Dead = true;
                    CloseRoomClients(room);
                    CleanupRoom(room, i);
                    continue;
                }

                // 空房回收（宽限统一口径，EmptySince 泵侧维护）：
                // - 开过局的房间：终局后全员离开（子进程 PHASE 回 Waiting 佐证）→ 5s 宽限杀——
                //   对局产物不留温房，重开 = 新房间新子进程（selftest 第二局验证的正是这条重建链）
                // - Finished 挂着（客户端停留在终局界面）：真人椅位已空 10s → 杀
                // - 从未开局：闲置 180s → 杀
                if (room.IsEmpty) room.EmptySince ??= now;
                else room.EmptySince = null;

                bool humansEmpty = room.IsEmpty && room.EmptySince.HasValue;
                bool recycle =
                    (room.HasHostedMatch && room.Phase == "Waiting" && humansEmpty
                        && (now - room.EmptySince.Value).TotalSeconds > 5)
                    || (room.HasHostedMatch && room.Phase == "Finished" && humansEmpty
                        && (now - room.EmptySince.Value).TotalSeconds > 10)
                    // 从未开局且全员离开（创建者弃房/反例测试残留）：5s 回收——
                    // 僵尸房会吞掉后续自动匹配的玩家（2026-09-27 V8 并发段实锤）
                    || (!room.HasHostedMatch && room.IsEmpty
                        && (now - (room.EmptySince ?? room.CreatedAt)).TotalSeconds > 5);
                if (recycle)
                {
                    Console.Error.WriteLine($"[gateway] {room.RoomId} {(room.HasHostedMatch ? "终局回收" : "空房闲置回收")}——杀子进程");
                    try { room.Child.Kill(); } catch { }
                    room.Dead = true;
                    CloseRoomClients(room);
                    CleanupRoom(room, i);
                }
            }

            _aiStops.RemoveAll(s => !s.Value);
        }

        private void CloseRoomClients(GatewayRoom room)
        {
            foreach (var chair in room.Chairs)
            {
                if (chair == null) continue;
                chair.Room = null;
                chair.CloseTunnel();
                chair.Close();
            }
            room.Chairs[0] = null;
            room.Chairs[1] = null;
        }

        private void CleanupRoom(GatewayRoom room, int index)
        {
            try { if (room.Child != null && !room.Child.HasExited) room.Child.Kill(); } catch { }
            room.Child?.Dispose();
            _rooms.RemoveAt(index);
            _lobbyDirty = true;
        }

        // ============================================================ AI 填位（直连子进程的回环客户端） ============================================================

        private void SpawnAiFiller(GatewayRoom room, string nickname)
        {
            var deckIds = SynergyUI.CardCatalog.LoadAll().Select(c => c.ID).Distinct().Take(30).ToArray();
            if (deckIds.Length == 0)
            {
                Console.Error.WriteLine("[gateway] AI 填位失败：本地卡表为空");
                return;
            }
            var name = string.IsNullOrEmpty(nickname) ? $"AI-{++room.AiCount}" : nickname;
            room.AiCount++;
            var stop = new BrainClient.VolatileBool();
            _aiStops.Add(stop);
            var thread = new Thread(() =>
            {
                try
                {
                    var ai = new BrainClient();
                    ai.RunDirect("127.0.0.1", room.Port, name, deckIds, turnCap: 60);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[gateway] AI 回环客户端退出（{name}）：{ex.GetType().Name}: {ex.Message}");
                }
            }) { IsBackground = true };
            thread.Start();
            Console.Error.WriteLine($"[gateway] AI 填位：{name} 直连子进程 :{room.Port}（卡组 {deckIds.Length} 张）");
        }

        // ============================================================ 下行（大厅相位） ============================================================

        private MsgLobbyState BuildLobbyState()
        {
            var rooms = _rooms.Where(r => !r.Dead).Select(r => new MsgLobbyRoomInfo
            {
                RoomId = r.RoomId,
                Name = r.Name,
                Phase = PhaseValue(r.Phase),
                PlayerCount = 2 - r.FreeChairCount,
                Nicknames = r.ChairNicknames.Where(n => !string.IsNullOrEmpty(n)).ToArray(),
                SpectatorCount = 0,
            }).ToArray();
            return new MsgLobbyState { Rooms = rooms, QueuedCount = _queue.Count };
        }

        private static int PhaseValue(string phase)
        {
            return phase switch
            {
                "DeckSubmit" => (int)NetRoomPhase.DeckSubmit,
                "Playing" => (int)NetRoomPhase.Playing,
                "Finished" => (int)NetRoomPhase.Finished,
                _ => (int)NetRoomPhase.Waiting,
            };
        }

        private void BroadcastLobbyState()
        {
            var state = BuildLobbyState();
            foreach (var conn in _lobby.ToList())
                Send(conn, NetworkMessageType.LobbyState, state);
        }

        private void Send<T>(GatewayClient client, NetworkMessageType type, T payload) where T : class
        {
            if (client.Closed) return;
            try
            {
                var frame = FrameCodec.Frame(NetworkSerializer.BuildEnvelope(type, payload));
                lock (client.WriteLock)
                {
                    if (!client.Closed) client.Stream.Write(frame, 0, frame.Length);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[gateway] 下发 {type} 失败：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                client.Close();
            }
        }

        private void SendError(GatewayClient client, string reason, string context)
            => Send(client, NetworkMessageType.Error, new MsgError { Reason = reason, Context = context });

        // ============================================================ 停机 ============================================================

        private void Shutdown()
        {
            _stopping = true;
            foreach (var ai in _aiStops) ai.Value = true;
            foreach (var room in _rooms.ToList())
            {
                try { if (room.Child != null && !room.Child.HasExited) room.Child.Kill(); } catch { }
                CloseRoomClients(room);
            }
            foreach (var client in _clients.ToList())
                client.Close();
            try { _listener.Stop(); } catch { }
            Console.Error.WriteLine("[gateway] 已停止（全部子进程与连接已回收）");
        }
    }
}
