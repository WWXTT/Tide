#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using CardCore.Serialization;

namespace CardCore.Network
{
    /// <summary>
    /// 运行时 AI 填位客户端（2026-10-21 P2P）：独立线程向房主回环（127.0.0.1:port）发起
    /// NetClientBrain 客户端——JoinRoom 任意空位 → 提交卡组（+自动挑技能卡）→ 锁步打到终局。
    /// 对房间而言就是一个普通 TCP 客户端，走完整握手与校验，零特殊逻辑。
    /// （NetLobbyServer.AiLoopbackClient 的运行时公开版——大厅层不进 P2P 拓扑，逻辑平移。）
    /// </summary>
    public sealed class NetAiFiller : IBrainChannel
    {
        private readonly TcpClient _tcp = new TcpClient();
        private readonly FrameCodec.FrameDecoder _decoder = new FrameCodec.FrameDecoder();
        private readonly byte[] _buf = new byte[16 * 1024];
        private readonly NetClientBrain _brain = new NetClientBrain();
        private readonly string _nickname;
        private readonly string[] _deckIds;
        private readonly string _skillCardId;
        private readonly int _turnCap;
        private readonly NetDeckDigest _digest; // 构造期预计算（调用方线程——生产=主线程；摘要计算的非线程安全共享态见下注）

        private NetworkStream _stream;
        private MsgRoomState _roomState;
        private MsgMatchManifest _manifest;
        private readonly List<NetEvent> _events = new List<NetEvent>();
        private volatile bool _stop;
        private bool _disconnected;

        /// <summary>回环线程句柄（Stop 后由 GC 回收；IsAlive 可观测）。</summary>
        public Thread Thread { get; private set; }

        /// <summary>卡组与技能显式版（测试夹具用）。</summary>
        public NetAiFiller(string nickname, string[] deckIds, string skillCardId = null, int turnCap = 60)
        {
            _nickname = nickname;
            _deckIds = deckIds;
            _skillCardId = skillCardId;
            _turnCap = turnCap;
            // 摘要在构造期（调用方线程）算好，不在回环线程现算：ComputeDeckDigest 走共享
            // 转换静态量非线程安全——生产路径摘要=主线程算、服务器泵=主线程验天然串行；
            // 测试里多填位线程并发现算会互相打穿（p2pcheck 实测：后算方 0 行 vs 服务器 2 行）。
            _digest = NetMatchHandshake.ComputeDeckDigest(deckIds);
        }

        /// <summary>便捷工厂：卡池前 30 张非重复 + 自动挑技能卡（验证器 DeckIds 同口径——
        /// LoadAll 前 30 过 DeckSubmit 闭包校验；LoadPlayPool 滤池含效果断链卡会被拒）。</summary>
        public static NetAiFiller FromPlayPool(string nickname)
        {
            var pool = SynergyUI.CardCatalog.LoadAll();
            var deckIds = pool.Select(c => c.ID).Distinct().Take(30).ToArray();
            if (deckIds.Length == 0) return null;
            return new NetAiFiller(nickname, deckIds, HeroSkillSystem.AutoPickSkillCard(pool));
        }

        public void Start(int port)
        {
            Thread = new Thread(() => Run(port)) { IsBackground = true, Name = $"NetAiFiller-{_nickname}" };
            Thread.Start();
        }

        /// <summary>停机（关 socket 让阻塞读退出；线程自然收尾）。</summary>
        public void Stop()
        {
            _stop = true;
            try { _tcp.Close(); } catch (Exception) { }
        }

        private bool Done => _events.Any(e => e.EventType == nameof(GameOverEvent))
            && _roomState?.Phase == (int)NetRoomPhase.Finished;

        private void Run(int port)
        {
            try
            {
                _tcp.Connect("127.0.0.1", port);
                _stream = _tcp.GetStream();

                Send(NetworkMessageType.JoinRoom, new MsgJoinRoom
                {
                    RoomId = "",
                    WantSeat = -1,
                    Nickname = _nickname,
                });
                var submit = new MsgDeckSubmit
                {
                    DeckName = $"ai-{_nickname}",
                    CardIds = _deckIds,
                    Digest = _digest,
                    SkillCardId = _skillCardId,
                };

                while (!_stop && !_disconnected && !Done)
                {
                    Receive(20);
                    if (_manifest == null && _roomState?.Phase == (int)NetRoomPhase.DeckSubmit)
                        Send(NetworkMessageType.DeckSubmit, submit); // 重复提交被拒无妨（一次性即成功）
                    _brain.Think(this, _turnCap);
                }
            }
            catch (Exception)
            {
                // 房主停机/断开：随线程退出
            }
            finally
            {
                try { _stream?.Close(); } catch (Exception) { }
                try { _tcp.Close(); } catch (Exception) { }
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
            while (DateTime.UtcNow < deadline && !_stop)
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
                    var errMsg = NetworkSerializer.DeserializePayload<MsgError>(msg);
                    TideLog.Info($"[NetAiFiller-{_nickname}] 被拒（{errMsg?.Context}）：{errMsg?.Reason}");
                    _brain.ErrorsSeen++;
                    break;
            }
        }
    }
}
#endif
