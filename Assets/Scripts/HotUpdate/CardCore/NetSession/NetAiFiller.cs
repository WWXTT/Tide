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
        // 2026-11-11「AI 默认用玩家同一套卡组」：衍生物区/槽绑定随卡组一起提交（否则 TokenSig
        // 与玩家侧卡组摘要口径分叉）；null/空=无衍生物（既有调用零改动）。
        private readonly string[] _tokenCardIds;
        private readonly List<SynergyUI.DeckTokenBinding> _tokenBindings;
        private readonly string _deckName;
        private readonly NetDeckDigest _digest; // 构造期预计算（调用方线程——生产=主线程；摘要计算的非线程安全共享态见下注）

        private NetworkStream _stream;
        private MsgRoomState _roomState;
        private MsgMatchManifest _manifest;
        private readonly List<NetEvent> _events = new List<NetEvent>();
        private volatile bool _stop;
        private bool _disconnected;
        private bool _readySent;   // 就绪层重发闸（v17：逐座 Ready 回显；1s 内至多一次）
        private DateTime _readySentAt;
        // 2026-11-11 v17 定案：AI **只自动准备、永不开局**（原先落椅 0 会替发 RoomStart=AI 自动开局，
        // 已删）——开局权恒在房主连接（真人），双 AI+房主观战也须房主点「开始」。
        private bool _submitSent;  // 卡组提交一次性（2026-10-28 刷屏修复）：提交→manifest 到达有空窗，
                                   // 旧逻辑空窗内每 20ms 重发→服务器回「本座位已提交过卡组」Error 刷屏

        /// <summary>回环线程句柄（Stop 后由 GC 回收；IsAlive 可观测）。</summary>
        public Thread Thread { get; private set; }

        /// <summary>卡组与技能显式版（测试夹具用；2026-11-11 追加衍生物区/绑定/卡组名——
        /// 房主「加 AI」默认直接用玩家选中的那套卡组，按同口径提交）。</summary>
        public NetAiFiller(string nickname, string[] deckIds, string skillCardId = null, int turnCap = 60,
            string[] tokenCardIds = null, List<SynergyUI.DeckTokenBinding> tokenBindings = null,
            string deckName = null)
        {
            _nickname = nickname;
            _deckIds = deckIds;
            _skillCardId = skillCardId;
            _turnCap = turnCap;
            _tokenCardIds = tokenCardIds;
            _tokenBindings = tokenBindings;
            _deckName = deckName;
            // 摘要在构造期（调用方线程）算好，不在回环线程现算：ComputeDeckDigest 走共享
            // 转换静态量非线程安全——生产路径摘要=主线程算、服务器泵=主线程验天然串行；
            // 测试里多填位线程并发现算会互相打穿（p2pcheck 实测：后算方 0 行 vs 服务器 2 行）。
            _digest = NetMatchHandshake.ComputeDeckDigest(deckIds, tokenCardIds, tokenBindings);
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
                    IsAi = true, // v11 对局记录：AI 填位——终局摘要不下发 AI 局
                });
                var submit = new MsgDeckSubmit
                {
                    DeckName = string.IsNullOrEmpty(_deckName) ? $"ai-{_nickname}" : _deckName,
                    CardIds = _deckIds,
                    Digest = _digest,
                    SkillCardId = _skillCardId,
                    TokenCardIds = _tokenCardIds ?? Array.Empty<string>(),
                    TokenBindings = NetMatchHandshake.ToWireBindings(_tokenBindings),
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

        /// <summary>就绪层自动机（v17 2026-11-11）：入座即**自动准备**（逐座 Ready 回显为准——
        /// 房间复位后再次准备），**永不发 RoomStart**（开局权恒在房主，双 AI 局也等房主点开始）。
        /// 重发闸：同一座位 1s 内最多一次（正常一次就落定，防被拒时刷屏）。</summary>
        private void PumpRoomReadiness()
        {
            if (_roomState == null || _roomState.Phase != (int)NetRoomPhase.Waiting || _roomState.Players == null)
                return;
            int mySeat = -1;
            bool ready = false;
            for (int i = 0; i < _roomState.Players.Length; i++)
            {
                var p = _roomState.Players[i];
                if (p != null && p.Connected && p.Nickname == _nickname)
                {
                    mySeat = i;
                    ready = p.Ready;
                    break;
                }
            }
            if (mySeat < 0 || ready) { _readySent = ready; return; }
            if (_readySent && (DateTime.UtcNow - _readySentAt).TotalSeconds < 1.0) return;
            _readySent = true;
            _readySentAt = DateTime.UtcNow;
            Send(NetworkMessageType.RoomReady, new MsgRoomReady { Ready = true });
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
            // 协议版本闸（v8 起，2026-11-05）：失配=版本错乱（同进程回环理论不触发），断连自护
            if (!NetworkProtocol.IsCompatible(msg.ProtocolVersion))
            {
                TideLog.Error($"[NetAiFiller] {NetworkProtocol.MismatchText(msg.ProtocolVersion)}——断开");
                _disconnected = true;
                return;
            }

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
