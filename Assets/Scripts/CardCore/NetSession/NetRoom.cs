using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using GameBoard;
using SynergyUI;

namespace CardCore.Network
{
    /// <summary>
    /// 单房间状态机（M2 会话层，2026-09-23；设计见 网络协议.md §12）：
    /// Waiting → DeckSubmit → Playing → Finished，全员断开后回 Waiting（单房间单进程定案）。
    ///
    /// - 分座：JoinRoom 占椅位（0/1/观战）；双玩家位满 → DeckSubmit。
    /// - 握手驱动（§11 消息流接线）：DeckSubmit → NetMatchHandshake.ValidateDeckSubmit →
    ///   双座位通过 → BuildMatchManifest 各发各座 → 开局。
    /// - 换先手（训练桥先例）：随机选本局先手椅位，其卡组进 Player1（引擎座位恒 0 先手）——
    ///   零引擎改动；椅位↔引擎座位映射 = MatchManifest.OwnSeat 权威 + RoomState.FirstSeatThisMatch。
    /// - 观战：全信息（2026-09-23 定案）——事件流不滤、快照走 fullInfo 视角；随时可进。
    /// - 断线：Waiting/DeckSubmit 退位回 Waiting；Playing 玩家断线 → 对局作废（重连留 M4）。
    /// - 线程：全部方法只允许逻辑线程（NetSessionServer.Pump）调用；IO 线程只入队。
    /// </summary>
    public sealed class NetRoom
    {
        private readonly string _roomId;
        private readonly Random _rng;
        private readonly int? _seed;

        private NetRoomPhase _phase = NetRoomPhase.Waiting;
        private readonly NetClientConnection[] _chairs = new NetClientConnection[2];
        private readonly List<NetClientConnection> _spectators = new List<NetClientConnection>();
        private readonly string[][] _deckIds = new string[2][];

        private int _firstSeat = -1;          // 本局先手椅位（未开局 -1）
        private bool _roomStateDirty = true;  // RoomState 待广播
        private bool _gameOver;

        // 对局接线（搬 TideHeadlessDriver.ResetCore 骨架）
        private BoardState _board;
        private NetworkTargetSelector _selector;

        /// <summary>seed=null 用时间随机（生产）；钉种子（M3 对拍）时先手椅位与引擎随机流均确定。</summary>
        public NetRoom(string roomId, int? seed = null)
        {
            _roomId = roomId ?? "";
            _seed = seed;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public NetRoomPhase Phase => _phase;
        public int FirstSeat => _firstSeat;
        /// <summary>房间是否已无成员（宿主判断可否重置）。</summary>
        public bool IsEmpty =>
            _chairs.All(c => c == null) && _spectators.Count == 0;

        // ============================================================ 上行分派 ============================================================

        /// <summary>处理一条来自成员连接的上行消息（逻辑线程）。</summary>
        public void OnMessage(GameCore core, NetClientConnection conn, NetworkMessage msg)
        {
            if (conn == null || msg == null) return;

            try
            {
                switch (msg.Type)
                {
                    case NetworkMessageType.JoinRoom:
                        HandleJoin(conn, msg);
                        break;

                    case NetworkMessageType.DeckSubmit:
                        HandleDeckSubmit(conn, msg);
                        break;

                    case NetworkMessageType.SelectResponse:
                        HandleSelectResponse(conn, msg);
                        break;

                    case NetworkMessageType.Ping:
                        conn.SendPayload<object>(NetworkMessageType.Pong, null);
                        break;

                    default:
                        if (msg.Type >= NetworkMessageType.IntentPlayCard
                            && msg.Type <= NetworkMessageType.IntentConcede)
                            HandleIntent(core, conn, msg);
                        else
                            SendError(conn, $"消息类型 {msg.Type} 不是会话层可处理的上行", msg.Type.ToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                // 单条消息异常不倒服务器：拒绝该消息并继续泵（验证器断言零到达）
                SendError(conn, $"消息处理异常：{ex.GetType().Name}: {ex.Message}", msg.Type.ToString());
            }
        }

        private void HandleJoin(NetClientConnection conn, NetworkMessage msg)
        {
            var join = NetworkSerializer.DeserializePayload<MsgJoinRoom>(msg);
            if (join == null) { SendError(conn, "JoinRoom 载荷损坏", "JoinRoom"); return; }
            if (conn.ChairSeat != -2) { SendError(conn, "本连接已进房（M2 一连接一身份）", "JoinRoom"); return; }
            if (_phase == NetRoomPhase.Finished)
            { SendError(conn, "对局已结束（单局串行——全员断开后房间回 Waiting 再进）", "JoinRoom"); return; }

            if (join.AsSpectator)
            {
                conn.ChairSeat = -1;
                conn.MatchSeat = -1;
                conn.Nickname = string.IsNullOrEmpty(join.Nickname) ? $"观战者{_spectators.Count + 1}" : join.Nickname;
                // 中途观战从当前时点看起（历史事件/快照不回放——M2 简化，回放属 M4 重连范畴）
                conn.EventCursor = NetEventProjector.Events.Count;
                conn.SnapshotDirty = _phase == NetRoomPhase.Playing;
                _spectators.Add(conn);
                _roomStateDirty = true;
                return;
            }

            if (_phase != NetRoomPhase.Waiting)
            { SendError(conn, "对局已开局/等卡组中，玩家位锁定（M2 不支持中途补位）", "JoinRoom"); return; }

            int seat = join.WantSeat;
            if (seat != 0 && seat != 1) seat = -1; // -1/非法值 = 任意空位
            if (seat >= 0 && _chairs[seat] != null)
            { SendError(conn, $"座位 {seat} 已被占用", "JoinRoom"); return; }
            if (seat < 0)
            {
                if (_chairs[0] == null) seat = 0;
                else if (_chairs[1] == null) seat = 1;
                else { SendError(conn, "房间已满（双玩家位占用）", "JoinRoom"); return; }
            }

            conn.ChairSeat = seat;
            conn.MatchSeat = -1;
            conn.Nickname = string.IsNullOrEmpty(join.Nickname) ? $"玩家{seat + 1}" : join.Nickname;
            _chairs[seat] = conn;
            _roomStateDirty = true;

            if (_chairs[0] != null && _chairs[1] != null)
            {
                _phase = NetRoomPhase.DeckSubmit;
                _roomStateDirty = true;
            }
        }

        private void HandleDeckSubmit(NetClientConnection conn, NetworkMessage msg)
        {
            if (conn.ChairSeat != 0 && conn.ChairSeat != 1)
            { SendError(conn, "非玩家座位不能提交卡组", "DeckSubmit"); return; }
            if (_phase == NetRoomPhase.Waiting)
            { SendError(conn, "等双方就座后再提交卡组", "DeckSubmit"); return; }
            if (_phase != NetRoomPhase.DeckSubmit)
            { SendError(conn, "当前阶段不接受卡组提交", "DeckSubmit"); return; }
            if (_deckIds[conn.ChairSeat] != null)
            { SendError(conn, "本座位已提交过卡组", "DeckSubmit"); return; }

            var submit = NetworkSerializer.DeserializePayload<MsgDeckSubmit>(msg);
            if (submit == null) { SendError(conn, "DeckSubmit 载荷损坏", "DeckSubmit"); return; }

            if (!NetMatchHandshake.ValidateDeckSubmit(submit, out var reason))
            {
                SendError(conn, reason, "DeckSubmit"); // 拒绝后保持等该座位重提
                return;
            }

            _deckIds[conn.ChairSeat] = submit.CardIds;
            if (_deckIds[0] != null && _deckIds[1] != null)
                StartMatch();
        }

        private void HandleSelectResponse(NetClientConnection conn, NetworkMessage msg)
        {
            if (_phase != NetRoomPhase.Playing || _selector == null) return;
            var resp = NetworkSerializer.DeserializePayload<MsgSelectResponse>(msg);
            if (resp == null) return;
            if (_selector.TryComplete(resp)) return;

            // 迟到/未知 RequestId：静默忽略之外再给一次可诊断回执（引擎侧已按空应答/超时兜底）
            SendError(conn, $"反问应答未知 RequestId={resp.RequestId}（迟到或已作废）", "SelectResponse");
        }

        private void HandleIntent(GameCore core, NetClientConnection conn, NetworkMessage msg)
        {
            if (_phase != NetRoomPhase.Playing)
            { SendError(conn, "对局未在进行中", msg.Type.ToString()); return; }
            if (conn.MatchSeat < 0)
            { SendError(conn, "观战连接不能发 intent", msg.Type.ToString()); return; }

            if (!NetworkIntentApplier.Apply(core, conn.MatchSeat, msg, out var error))
            {
                SendError(conn, error, msg.Type.ToString()); // §5 拒绝语义：Error 帧回发（M2 落地）
                return;
            }
            MarkSnapshotDirty();
        }

        // ============================================================ 开局 / 终局 ============================================================

        /// <summary>双座位卡组就绪 → 开局。换先手 = 谁的卡组进 Player1（引擎座位 0 恒先手）。</summary>
        private void StartMatch()
        {
            var core = GameCore.Instance;
            _firstSeat = _rng.Next(2);

            // 组合根注入 + 引擎初始化（镜像 TideHeadlessDriver.ResetCore；InitGame 内含 Reset→
            // NetEventProjector.ClearAll（GameCore.Reset 接线）→ 洗牌 → 起手 → StartGame）。
            // 钉种子（M3 对拍）：_seed 同时进 InitGame——GameRng 与洗牌流（ZoneContainer）双流重播。
            MorphSystem.ResolveMorphTarget = CardCatalog.GetById;
            var deckFirst = BuildDeck(_deckIds[_firstSeat]);
            var deckSecond = BuildDeck(_deckIds[1 - _firstSeat]);
            core.InitGame(CardLoader.BuildDeck(deckFirst, 1), CardLoader.BuildDeck(deckSecond, 1), _seed);
            core.Player1.IsAI = false; // 人类在环：反问走 NetworkTargetSelector
            core.Player2.IsAI = false;

            // 棋盘占用层接线（碾压邻接 + 连接光环；核心不绑棋盘，宿主接线——静态扩展点惯例）
            _board?.Dispose();
            GameBoard.LinkAuraSystem.Detach();
            _board = new BoardState(core, core.Player1, core.Player2,
                HalfFieldData.Flat(), HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.Neighbors;
            GameBoard.LinkAuraSystem.Attach(_board);

            // 反问桥：SendAsync = 按 ChooserSeat 定向出队（返回已完成任务——实际字节由泵的出队段写）
            _selector?.Detach();
            _selector = new NetworkTargetSelector
            {
                SendAsync = request =>
                {
                    var target = SeatConnection(request.ChooserSeat);
                    target?.SendPayload(NetworkMessageType.SelectRequest, request);
                    return Cysharp.Threading.Tasks.UniTask.CompletedTask;
                },
            };
            _selector.Attach();

            // 座位映射（椅位 → 引擎座位）+ 事件游标对齐（InitGame 已 ClearAll）
            foreach (var chair in _chairs.Where(c => c != null))
            {
                chair.MatchSeat = chair.ChairSeat == _firstSeat ? 0 : 1;
                chair.EventCursor = 0;
                chair.SnapshotDirty = true;
            }
            foreach (var spec in _spectators)
            {
                spec.MatchSeat = -1;
                spec.EventCursor = 0; // 先于开局的观战者从头看整局（InitGame 的 ClearAll 已清缓冲）
                spec.SnapshotDirty = true;
            }

            // 对局清单：各座位视角各发一份（OwnSeat=引擎座位权威；摘要按该座位卡组计算）
            for (int chair = 0; chair < 2; chair++)
            {
                var conn = _chairs[chair];
                if (conn == null) continue;
                conn.SendPayload(NetworkMessageType.MatchManifest, NetMatchHandshake.BuildMatchManifest(
                    conn.MatchSeat, _deckIds[chair], _deckIds[1 - chair].Length));
            }

            _gameOver = false;
            _phase = NetRoomPhase.Playing;
            _roomStateDirty = true;
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnGameOver(GameOverEvent e)
        {
            if (_phase != NetRoomPhase.Playing) return;
            _gameOver = true;
            _phase = NetRoomPhase.Finished;
            _roomStateDirty = true;
            MarkSnapshotDirty(); // 终局快照：静默判定对 Finished 恒真
        }

        /// <summary>终局/重置收口：卸反问桥、退订终局、拆棋盘接线。</summary>
        private void TeardownMatch()
        {
            _selector?.AbortPending();
            _selector?.Detach();
            _selector = null;
            EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            CombatSystem.AdjacentResolver = null;
            GameBoard.LinkAuraSystem.Detach();
            _board?.Dispose();
            _board = null;
        }

        private static List<CardData> BuildDeck(string[] cardIds)
        {
            var deck = new List<CardData>(cardIds.Length);
            foreach (var id in cardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card != null) deck.Add(card);
            }
            return deck;
        }

        // ============================================================ 断线 / 重置 ============================================================

        /// <summary>成员断开（逻辑线程；由宿主泵发现）。</summary>
        public void OnDisconnect(NetClientConnection conn)
        {
            if (conn.ChairSeat == -2) return; // 未进房的连接：无房间侧状态

            if (conn.ChairSeat == -1)
            {
                _spectators.Remove(conn); // 观战离开不影响对局
                _roomStateDirty = true;
                return;
            }

            _chairs[conn.ChairSeat] = null;
            switch (_phase)
            {
                case NetRoomPhase.Waiting:
                    break; // 未满员：退位即可

                case NetRoomPhase.DeckSubmit:
                    // 缺员退回等进房：另一座位的提交作废（卡组对局必须双座位同批提交）
                    _deckIds[0] = null;
                    _deckIds[1] = null;
                    _phase = NetRoomPhase.Waiting;
                    break;

                case NetRoomPhase.Playing:
                    // 对局作废（M2 最小策略；重连留 M4）：作废全部未决反问防泵悬死，
                    // 广播 Error + RoomState(Finished)——不判胜负。
                    _selector?.AbortPending();
                    _phase = NetRoomPhase.Finished;
                    BroadcastError($"对局作废：座位 {conn.ChairSeat}（{conn.Nickname}）断线");
                    break;

                case NetRoomPhase.Finished:
                    break;
            }
            _roomStateDirty = true;
        }

        /// <summary>全员离场 → 房间回 Waiting（重开 = 重新进房+重新提交）。</summary>
        public void ResetIfEmpty()
        {
            if (!IsEmpty || _phase != NetRoomPhase.Finished) return;
            TeardownMatch();
            _phase = NetRoomPhase.Waiting;
            _deckIds[0] = null;
            _deckIds[1] = null;
            _firstSeat = -1;
            _gameOver = false;
            _roomStateDirty = false; // 无成员：无需广播
        }

        // ============================================================ 泵（逻辑线程每 tick） ============================================================

        /// <summary>
        /// 引擎推进（逻辑线程每 tick）。**不调 GameCore.Update**——其空栈分支会连发
        /// CheckPhaseTransition 自由滑阶段（真实对局的相位推进本就靠玩家 intent 驱动，
        /// 与全部无头驱动器同口径）：出牌/攻击/过优先权全部来自 intent；本方法只做两件事——
        /// ① 栈上响应窗口：优先权持有者有真实响应候选 → 不排干，等客户端
        ///   （IntentPlayCard/ActivateEffect 响应 或 IntentPassPriority 让行）；
        /// ② End→Standby 折返（镜像 RunIntentSection/AiBattleDriver 驱动侧职责）。
        /// </summary>
        public void PumpEngine(GameCore core)
        {
            if (_phase != NetRoomPhase.Playing || _gameOver || core == null) return;
            var engine = core.StackEngine;
            if (engine == null || engine.IsResolving) return;

            bool stackSettled = engine.IsEmpty && !engine.HasPendingEffects;
            if (!stackSettled)
            {
                var holder = engine.CurrentPriorityHolder;
                if (holder != null && GameActions.CollectAvailableResponses(core, holder).Count > 0)
                    return; // 网络响应窗口：超时无响应的兜底属 M4（服务器主动代打）

                GameActions.DrainStack(core, 32);
                return;
            }

            // End→Standby 折返门：栈排干 + 无未决反问（手牌上限弃牌等回合末异步链收口后才折返）
            if (core.TurnEngine?.CurrentPhase?.Phase == PhaseType.End
                && (_selector == null || !_selector.HasPending))
            {
                core.TurnEngine.CheckPhaseTransition();
            }
        }

        /// <summary>下行出队：RoomState 广播 → 事件流（按座位过滤，观战全信息）→ 静默点快照。</summary>
        public void FlushDownlink(GameCore core)
        {
            if (_roomStateDirty)
            {
                _roomStateDirty = false;
                var state = BuildRoomState();
                foreach (var member in AllMembers())
                    member.SendPayload(NetworkMessageType.RoomState, state);
            }

            if (_phase != NetRoomPhase.Playing && _phase != NetRoomPhase.Finished) return;

            // ---- 事件流：全局投影缓冲按游标切片，逐成员按视角过滤（观战直通）----
            var events = NetEventProjector.Events;
            int total = events.Count;
            var hiddenBySeat = new Dictionary<int, HashSet<uint>>();
            bool sentAny = false;
            foreach (var member in AllMembers())
            {
                if (member.EventCursor >= total) continue;
                HashSet<uint> hidden = null;
                if (member.MatchSeat >= 0)
                {
                    if (!hiddenBySeat.TryGetValue(member.MatchSeat, out hidden))
                    {
                        hidden = NetEventSeatFilter.HiddenCardIds(core, member.MatchSeat);
                        hiddenBySeat[member.MatchSeat] = hidden;
                    }
                }

                for (int offset = member.EventCursor; offset < total; offset += 64)
                {
                    var chunk = new NetEvent[Math.Min(64, total - offset)];
                    for (int i = 0; i < chunk.Length; i++)
                        chunk[i] = NetEventSeatFilter.Filter(events[offset + i], hidden);
                    member.SendPayload(NetworkMessageType.NetEventBatch, new MsgNetEventBatch { Events = chunk });
                }
                member.EventCursor = total;
                sentAny = true;
            }
            if (sentAny) MarkSnapshotDirty();

            // ---- 快照：稳定决策点取样（§7 时点约束收口：不在结算中 + 无未决反问——
            // 栈非空但优先权等待是稳定决策点，快照含 StackV2/PrioritySeat 正是客户端的让行依据）----
            bool stable = _phase == NetRoomPhase.Finished
                || (core != null && core.StackEngine != null
                    && !core.StackEngine.IsResolving
                    && (_selector == null || !_selector.HasPending));
            if (!stable) return;

            var snapshotCache = new Dictionary<(int seat, bool full), MsgGameStateSync>();
            foreach (var member in AllMembers())
            {
                if (!member.SnapshotDirty) continue;
                bool full = member.MatchSeat < 0;
                var key = (member.MatchSeat, full);
                if (!snapshotCache.TryGetValue(key, out var snapshot))
                {
                    snapshot = NetSnapshotBuilder.Build(core, member.MatchSeat, full);
                    snapshotCache[key] = snapshot;
                }
                member.SendPayload(NetworkMessageType.GameStateSyncV2, snapshot);
                member.SnapshotDirty = false;
            }
        }

        // ============================================================ 杂项 ============================================================

        private IEnumerable<NetClientConnection> AllMembers()
        {
            foreach (var chair in _chairs)
                if (chair != null) yield return chair;
            foreach (var spec in _spectators)
                yield return spec;
        }

        private NetClientConnection SeatConnection(int matchSeat)
            => _chairs.FirstOrDefault(c => c != null && c.MatchSeat == matchSeat);

        private void MarkSnapshotDirty()
        {
            foreach (var member in AllMembers())
                member.SnapshotDirty = true;
        }

        private MsgRoomState BuildRoomState()
        {
            var players = new MsgRoomSeatInfo[2];
            for (int i = 0; i < 2; i++)
            {
                var conn = _chairs[i];
                players[i] = new MsgRoomSeatInfo
                {
                    Seat = i,
                    Nickname = conn?.Nickname ?? "",
                    Connected = conn != null,
                };
            }
            return new MsgRoomState
            {
                RoomId = _roomId,
                Phase = (int)_phase,
                Players = players,
                SpectatorCount = _spectators.Count,
                FirstSeatThisMatch = _firstSeat,
            };
        }

        private void SendError(NetClientConnection conn, string reason, string context)
            => conn.SendPayload(NetworkMessageType.Error, new MsgError { Reason = reason, Context = context });

        private void BroadcastError(string reason)
        {
            var err = new MsgError { Reason = reason, Context = "Room" };
            foreach (var member in AllMembers())
                member.SendPayload(NetworkMessageType.Error, err);
        }
    }
}
