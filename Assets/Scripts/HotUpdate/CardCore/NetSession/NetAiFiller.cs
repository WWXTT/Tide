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
        private bool _readySent;   // 就绪层（2026-10-28）：入座即准备（一次性）
        private bool _startSent;   // 就绪层：落在椅位0 时替房主开局（一次性；p2pcheck 双 AI 拓扑）
        private bool _submitSent;  // 卡组提交一次性（2026-10-28 刷屏修复）：提交→manifest 到达有空窗，
                                   // 旧逻辑空窗内每 20ms 重发→服务器回「本座位已提交过卡组」Error 刷屏

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

        /// <summary>便捷工厂：随机预组一套（NetPresetDecks 三选一，恒过校验；2026-10-28「不选=随机」同源）。
        /// 预组全缺回落全池前 30 + 自动挑技能卡（LoadAll 教学卡已并入全池，闭包校验可过）。</summary>
        public static NetAiFiller FromPlayPool(string nickname)
        {
            var deckIds = NetPresetDecks.RandomPreset(out var skill);
            if (deckIds != null)
                return new NetAiFiller(nickname, deckIds, skill);
            // 2026-10-22 三套主题卡组迁教学：本类在 TideServer 共享编译（CardCore 目录），
            // 不能引用 UI 层 DeckCatalog——统一用 CardCatalog.LoadAll（教学卡已并入全池）。
            var pool = SynergyUI.CardCatalog.LoadAll();
            var fallback = pool.Select(c => c.ID).Distinct().Take(30).ToArray();
            if (fallback.Length == 0) return null;
            return new NetAiFiller(nickname, fallback, HeroSkillSystem.AutoPickSkillCard(pool));
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
                    PumpRoomReadiness();
                    if (!_submitSent && _manifest == null && _roomState?.Phase == (int)NetRoomPhase.DeckSubmit)
                    {
                        _submitSent = true; // 一次性：随机预组恒过校验，被拒也不重发（防空窗重发刷 Error）
                        Send(NetworkMessageType.DeckSubmit, submit);
                    }
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

        /// <summary>就绪层自动机（2026-10-28 开局流程改版）：入座即准备；
        /// 落在椅位0（房主位）且双方就座+客人已备时替房主开局——p2pcheck 双 AI 拓扑由先入座者扮房主；
        /// 真人房主房中 AI 恒在椅位1，只准备。</summary>
        private void PumpRoomReadiness()
        {
            if (_roomState == null || _roomState.Phase != (int)NetRoomPhase.Waiting || _roomState.Players == null)
                return;
            int mySeat = -1;
            for (int i = 0; i < _roomState.Players.Length; i++)
                if (_roomState.Players[i] != null && _roomState.Players[i].Connected
                    && _roomState.Players[i].Nickname == _nickname) { mySeat = i; break; }
            if (mySeat < 0) return;

            if (!_readySent)
            {
                _readySent = true;
                Send(NetworkMessageType.RoomReady, new MsgRoomReady { Ready = true });
            }

            bool bothSeated = _roomState.Players.Length == 2
                && _roomState.Players[0] != null && _roomState.Players[0].Connected
                && _roomState.Players[1] != null && _roomState.Players[1].Connected;
            if (mySeat == 0 && !_startSent && bothSeated && _roomState.GuestReady)
            {
                _startSent = true;
                Send(NetworkMessageType.RoomStart, (object)null);
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
                case NetworkMessageType.TextRequest: // 文本反问（2026-10-31 阶段C）：bot 回默认值
                    _brain.PendingTexts.Add(NetworkSerializer.DeserializePayload<MsgTextRequest>(msg));
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
