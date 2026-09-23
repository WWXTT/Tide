#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using CardCore.Network;
using CardCore.Serialization;
using MemoryPack;
using SynergyUI;
using UnityEngine;

namespace CardCore.Editor.NetSession
{
    /// <summary>
    /// M3 独立 headless 客户端（batchmode 入口）：连接会话服务器 → 进房 → 提交卡组 →
    /// NetClientBrain 锁步打到终局。跨机/跨进程手工验证用（自动门禁=回环验证器第七段，
    /// 那里同进程双客户端已覆盖真实 TCP 链路）。
    ///
    /// 调用约定：
    ///   Unity.exe -batchmode -nographics -projectPath &lt;proj&gt;
    ///     -executeMethod CardCore.Editor.NetSession.NetClientHost.Main
    ///     -netHost &lt;ip&gt; -netPort &lt;n&gt; -nickname A -seat 0 [-turnCap 60]
    /// （收发结构与验证器 SocketTestClient 同款——编辑器工具各自持一份轻量副本，不进运行时程序集）
    /// </summary>
    public static class NetClientHost
    {
        private sealed class Client : IBrainChannel
        {
            private readonly TcpClient _tcp = new TcpClient();
            private NetworkStream _stream;
            private readonly FrameCodec.FrameDecoder _decoder = new FrameCodec.FrameDecoder();
            private readonly byte[] _buf = new byte[16 * 1024];
            private readonly MsgDeckSubmit _submit;
            private readonly int _wantSeat;
            private readonly string _nickname;
            private readonly int _turnCap;
            private readonly NetClientBrain _brain = new NetClientBrain();

            public bool Disconnected;
            public MsgRoomState RoomState;
            public MsgMatchManifest Manifest;
            public readonly List<NetEvent> Events = new List<NetEvent>();
            public bool Done => Events.Any(e => e.EventType == nameof(GameOverEvent))
                && RoomState?.Phase == (int)NetRoomPhase.Finished;

            public Client(MsgDeckSubmit submit, int wantSeat, string nickname, int turnCap)
            {
                _submit = submit;
                _wantSeat = wantSeat;
                _nickname = nickname;
                _turnCap = turnCap;
            }

            public void Connect(string host, int port)
            {
                _tcp.Connect(host, port);
                _stream = _tcp.GetStream();
            }

            public void Send<T>(NetworkMessageType type, T payload) where T : class
            {
                if (Disconnected) return;
                try
                {
                    var frame = FrameCodec.Frame(NetworkSerializer.BuildEnvelope(type, payload));
                    _stream.Write(frame, 0, frame.Length);
                    _stream.Flush();
                }
                catch (Exception) { Disconnected = true; }
            }

            /// <summary>收一段（有数据续期）+ 分派下行 + 喂大脑。</summary>
            public void Receive(int timeoutMs)
            {
                if (Disconnected || _stream == null) return;
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    if (!_stream.DataAvailable) { Thread.Sleep(5); continue; }
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
                        RoomState = NetworkSerializer.DeserializePayload<MsgRoomState>(msg);
                        _brain.RoomState = RoomState;
                        break;
                    case NetworkMessageType.MatchManifest:
                        Manifest = NetworkSerializer.DeserializePayload<MsgMatchManifest>(msg);
                        _brain.MyEngineSeat = Manifest.OwnSeat;
                        break;
                    case NetworkMessageType.GameStateSyncV2:
                        _brain.OnSnapshot(NetworkSerializer.DeserializePayload<MsgGameStateSync>(msg));
                        break;
                    case NetworkMessageType.NetEventBatch:
                        var batch = NetworkSerializer.DeserializePayload<MsgNetEventBatch>(msg);
                        if (batch?.Events != null) Events.AddRange(batch.Events);
                        break;
                    case NetworkMessageType.SelectRequest:
                        _brain.PendingSelects.Add(NetworkSerializer.DeserializePayload<MsgSelectRequest>(msg));
                        break;
                    case NetworkMessageType.Error:
                        var err = NetworkSerializer.DeserializePayload<MsgError>(msg);
                        _brain.ErrorsSeen++;
                        Debug.Log($"[NetClient] 拒绝（{err.Context}）：{err.Reason}");
                        break;
                }
            }

            /// <summary>主循环：进房 → 提交 → 锁步打到终局（或断线/时长上限退出）。</summary>
            public void Run(int totalSeconds)
            {
                Send(NetworkMessageType.JoinRoom, new MsgJoinRoom
                {
                    RoomId = "",
                    WantSeat = _wantSeat,
                    Nickname = _nickname,
                });

                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < totalSeconds * 1000L && !Disconnected && !Done)
                {
                    Receive(20);

                    // 双座位就座后提交卡组（一次性；重复提交被拒无妨）
                    if (Manifest == null && RoomState?.Phase == (int)NetRoomPhase.DeckSubmit)
                        Send(NetworkMessageType.DeckSubmit, _submit);

                    _brain.Think(this, _turnCap);
                }

                for (int i = 0; i < 50 && !Disconnected && !Done; i++) // 终局冲刷
                {
                    Receive(20);
                    if (Done) break;
                    _brain.Think(this, _turnCap);
                }

                Debug.Log($"[NetClient] {_nickname} 退出：{(Done ? "终局" : Disconnected ? "断线" : "超时")}，" +
                    $"事件 {Events.Count} 条，快照 {_brain.SnapshotsSeen} 帧，终局回合 {_brain.Snapshot?.CurrentTurn ?? -1}");
            }
        }

        public static void Main()
        {
            string host = "127.0.0.1";
            int port = 7777, seat = -1, turnCap = 60;
            var nickname = $"client{Environment.TickCount % 10000}";
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
            {
                if (argv[i] == "-netHost") host = argv[i + 1];
                else if (argv[i] == "-netPort" && int.TryParse(argv[i + 1], out var p)) port = p;
                else if (argv[i] == "-seat" && int.TryParse(argv[i + 1], out var s)) seat = s;
                else if (argv[i] == "-nickname") nickname = argv[i + 1];
                else if (argv[i] == "-turnCap" && int.TryParse(argv[i + 1], out var tc)) turnCap = tc;
            }

            // 卡组：本地卡池前 30 张非重复（手工联调用；正式客户端卡组选择属 UI 范畴）
            var deckIds = CardCatalog.LoadAll().Select(c => c.ID).Distinct().Take(30).ToArray();
            var submit = new MsgDeckSubmit
            {
                DeckName = $"headless-{nickname}",
                CardIds = deckIds,
                Digest = NetMatchHandshake.ComputeDeckDigest(deckIds),
            };

            var client = new Client(submit, seat, nickname, turnCap);
            client.Connect(host, port);
            Debug.unityLogger.logEnabled = true;
            Debug.Log($"[NetClient] 连接 {host}:{port}（seat={seat}，卡组 {deckIds.Length} 张）");
            client.Run(totalSeconds: 600);
        }
    }
}
#endif
