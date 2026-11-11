using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Network
{
    /// <summary>
    /// 房间通道（v17 2026-11-11 房间/对战通道拆分，设计见 网络协议.md §16；原 NetRoom 的房间半边）：
    /// 房间成员/座位/就绪/房主/房态广播 + 与对局无关的一切房间操作。单房间状态机
    /// Waiting → DeckSubmit → Playing → Finished，全员断开后回 Waiting（单房间单进程定案）。
    ///
    /// - 分座：JoinRoom 占椅位（0/1/观战）；双椅满时真人自动转观战（v16），AI 满即拒。
    /// - 就绪层（2026-10-28 改版 / v17 逐座化）：房主配模式 + 非房主逐座准备 + 房主开局。
    ///   **房主=本进程宿主**（JoinRoom.IsHost 自报，P2P 不可转移）：可落任意椅或观战，
    ///   开局权/模式配置权恒在房主连接；非房主（真人/AI）一律「准备然后等房主点开始」，
    ///   AI 只自动准备、**不再替发 RoomStart**（2026-11-11 用户定案）。
    /// - 房主退出=解散整房（无服务器可转移）：OnDisconnect 走 DissolveRoom，断开全部成员。
    ///   只剩 AI 的对局随宿主停机结束（DissolveRoom 顺带拆对局）。
    /// - 座位↔观战（SeatSwap）：仅 Waiting、排位房拒；释放椅位/占空位，昵称保留。
    /// - 观战：全信息（2026-09-23 定案）——事件流不滤、快照走 fullInfo 视角；随时可进。
    /// - 阶段迁移只由本类与对战通道（<see cref="NetBattleChannel"/>）经 EnterPhase 驱动；
    ///   对战通道不持有成员表，房间与对局生命周期解耦。
    /// - 线程：全部方法只允许逻辑线程（NetSessionServer.Pump）调用；IO 线程只入队。
    /// </summary>
    public sealed class NetRoom
    {
        /// <summary>排位接受窗时长（秒；S8 2026-11-10 定案 5s——双座位满起算，双方都须点「接受」，
        /// 任一拒绝/超时=广播 Error("排位未接受","Ranked") 并解散，双方各自回 Idle 不自动重排）。</summary>
        public const int RankedAckSeconds = 5;

        private readonly string _roomId;
        private readonly bool _autoStartWhenAllReady; // 无真人房主（无头房主）拓扑：双座满即自动开局（默认模式掷币+随机）
        private readonly string _password; // S5（2026-11-10）房间密码：空/null=无密码房（JoinRoom 不校验）
        private readonly bool _ranked;     // S8（2026-11-10）排位房：双座满后走 RankedAck 双接受窗（替代准备/房主开局）

        // S5：随 JoinRoom 上报的卡组名（房间侧按椅位存——INetChannel 接口在 NetTransports.cs，
        // 不为本轮扩口；RoomState Players 快照带出供座位卡展示。退位即清）
        private readonly string[] _joinDeckNames = new string[2];

        private NetRoomPhase _phase = NetRoomPhase.Waiting;
        private readonly INetChannel[] _chairs = new INetChannel[2];
        private readonly List<INetChannel> _spectators = new List<INetChannel>();

        // 房主（v17）：随 JoinRoom 自报，只认首个声明者；未声明时回落椅 0（无头/验证器拓扑）。
        // 房主可落任意椅或观战——身份不跟座位走；退出（连接断开）=解散整房。
        private INetChannel _host;
        private bool _dissolved;

        private bool _roomStateDirty = true;

        // S8 排位接受窗：双座满起算 5s；_rankedAck 按椅位三态（null=未报）
        private readonly bool?[] _rankedAck = new bool?[2];
        private DateTime? _rankedAckDeadlineUtc;

        // 就绪层设置（2026-10-28）：先后手方式 + 洗牌模式 + v13 房主钉种子。设置本身是房间内容
        //（RoomSettings 消息归房间通道），开局时一次性交给对战通道的 BeginDeckPickPhase。
        private int _settingsFirstMove;       // 0=掷币 1=椅0先 2=椅1先
        private int _settingsShuffle;         // 0=随机洗牌 1=固定牌序
        private int _settingsSeed;            // v13 房主钉种子（0=开局随机定死）

        // 逐座准备态（v17：房主可落任意椅/观战，单 GuestReady 不足以表达）
        private readonly bool[] _ready = new bool[2];

        private readonly NetBattleChannel _battle;

        /// <summary>seed=null 用时间随机（生产）；钉种子（M3 对拍）时先手椅位与引擎随机流均确定。
        /// autoStartWhenAllReady=无头房主（MatchHost 等）：双座满即开局（跳过准备/房主确认）。
        /// password（S5 2026-11-10）：房间密码——非空时 JoinRoom 须携带同值；默认 null=无密码（既有调用零改动）。
        /// ranked（S8 2026-11-10）：排位房——双座满进 5s 双接受窗（RankedAck），收齐双 true 自动开局。</summary>
        public NetRoom(string roomId, int? seed = null, bool autoStartWhenAllReady = false,
            string password = null, bool ranked = false)
        {
            _roomId = roomId ?? "";
            _autoStartWhenAllReady = autoStartWhenAllReady;
            _password = password;
            _ranked = ranked;
            _battle = new NetBattleChannel(this, seed);
        }

        public NetRoomPhase Phase => _phase;
        public int FirstSeat => _battle.FirstSeat;

        /// <summary>房间是否已无成员（宿主判断可否重置）。</summary>
        public bool IsEmpty =>
            _chairs.All(c => c == null) && _spectators.Count == 0;

        /// <summary>房间 ID（大厅层列表/加入寻址用，2026-09-24）。</summary>
        public string RoomId => _roomId;

        /// <summary>玩家椅子位空位数（大厅层判断可否入座/配对，2026-09-24）。</summary>
        public int FreeChairCount => _chairs.Count(c => c == null);

        /// <summary>房间当前状态快照（大厅列表/外部展示用——与成员广播同构，2026-09-24）。</summary>
        public MsgRoomState State => BuildRoomState();

        // ============================================================ 上行分派（按通道） ============================================================

        /// <summary>处理一条来自成员连接的上行消息（逻辑线程）：版本闸 → NetworkChannels 归类 →
        /// 房间通道本类 / 对战通道 NetBattleChannel / 系统通道（Ping）。</summary>
        public void OnMessage(GameCore core, INetChannel conn, NetworkMessage msg)
        {
            if (conn == null || msg == null) return;

            // 协议版本闸（v8 起，2026-11-05）：此前信封版本只写不校验，新旧端只能靠
            // MemoryPack 错位自然崩——现显式拒绝并回 Error 帧（对局/大厅共用本口）。
            if (!NetworkProtocol.IsCompatible(msg.ProtocolVersion))
            {
                SendError(conn, NetworkProtocol.MismatchText(msg.ProtocolVersion), "ProtocolVersion");
                return;
            }

            try
            {
                switch (NetworkChannels.Of(msg.Type))
                {
                    case NetworkChannel.Room:
                        HandleRoomMessage(conn, msg);
                        break;

                    case NetworkChannel.Battle:
                        _battle.HandleMessage(core, conn, msg);
                        break;

                    case NetworkChannel.System:
                        if (msg.Type == NetworkMessageType.Ping)
                            conn.SendPayload<object>(NetworkMessageType.Pong, null);
                        else
                            SendError(conn, $"系统通道不接受上行 {msg.Type}", msg.Type.ToString());
                        break;

                    default:
                        // 大厅消息在 NetLobbyServer 先拦；直连会话服务器收到=调用方用错了通道
                        SendError(conn, $"消息类型 {msg.Type} 不属于房间/对战通道", msg.Type.ToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                // 单条消息异常不倒服务器：拒绝该消息并继续泵（验证器断言零到达）
                SendError(conn, $"消息处理异常：{ex.GetType().Name}: {ex.Message}", msg.Type.ToString());
            }
        }

        /// <summary>房间通道上行分派（成员/座位/就绪/房主/排位接受）。</summary>
        private void HandleRoomMessage(INetChannel conn, NetworkMessage msg)
        {
            switch (msg.Type)
            {
                case NetworkMessageType.JoinRoom:
                    var join = NetworkSerializer.DeserializePayload<MsgJoinRoom>(msg);
                    if (join == null) { SendError(conn, "JoinRoom 载荷损坏", "JoinRoom"); break; }
                    HandleJoin(conn, join);
                    break;

                case NetworkMessageType.RoomSettings:
                    HandleRoomSettings(conn, msg);
                    break;

                case NetworkMessageType.RoomReady:
                    HandleRoomReady(conn, msg);
                    break;

                case NetworkMessageType.RoomStart:
                    HandleRoomStart(conn);
                    break;

                case NetworkMessageType.RankedAck: // S8 排位：椅子位接受应答
                    HandleRankedAck(conn, msg);
                    break;

                case NetworkMessageType.SeatSwap: // v16 多成员房间：座位↔观战切换
                    HandleSeatSwap(conn, msg);
                    break;

                default:
                    SendError(conn, $"消息类型 {msg.Type} 不是房间通道可处理的上行", msg.Type.ToString());
                    break;
            }
        }

        /// <summary>进房分座（M2；2026-09-24 大厅层复用：LobbyCreateRoom/LobbyJoinRoom/自动配对
        /// 全部经此口入座，与直连 JoinRoom 同一校验路径）。</summary>
        public void HandleJoin(INetChannel conn, MsgJoinRoom join)
        {
            if (conn.ChairSeat != -2) { SendError(conn, "本连接已进房（M2 一连接一身份）", "JoinRoom"); return; }
            if (_phase == NetRoomPhase.Finished)
            { SendError(conn, "对局已结束（单局串行——全员断开后房间回 Waiting 再进）", "JoinRoom"); return; }

            // S5 房间密码闸：无密码房（_password 空）零影响；密码房校验观战与玩家位一视同仁
            if (!string.IsNullOrEmpty(_password) && (join.Password ?? "") != _password)
            { SendError(conn, "密码错误", "JoinRoom"); return; }

            // v17 房主自报：只认首个声明者（房主=P2P 宿主，不可转移；AI 不可能是房主）
            bool claimsHost = join.IsHost && !join.IsAi;
            if (claimsHost && _host != null && !ReferenceEquals(_host, conn))
            {
                TideLog.Warn($"[NetRoom] {conn.Nickname} 自报房主但已有房主 {_host.Nickname}——按普通成员处理");
                claimsHost = false;
            }
            conn.IsHost = claimsHost; // 落座/入观战成功后登记 _host（失败分支不留残值）

            if (join.AsSpectator)
            {
                conn.ChairSeat = -1;
                conn.MatchSeat = -1;
                conn.Nickname = string.IsNullOrEmpty(join.Nickname) ? $"观战者{_spectators.Count + 1}" : join.Nickname;
                conn.OpenId = join.OpenId ?? "";
                // 中途观战从当前时点看起（历史事件/快照不回放——M2 简化，回放属 M4 重连范畴）
                conn.EventCursor = NetEventProjector.Events.Count;
                conn.SnapshotDirty = _phase == NetRoomPhase.Playing;
                _spectators.Add(conn);
                if (conn.IsHost) _host = conn;
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
                else
                {
                    // v16 多成员房间（2026-11-11）：任意位进房且双椅满——真人自动转观战（保留原昵称，
                    // RoomState.SpectatorCount 反映；客户端由「自己不在 Players」推导观战态）。
                    // AI 填位客户端不得被转观战（它只玩不观——满即拒，防 NetAiFiller 误挂观战席）。
                    if (join.IsAi) { SendError(conn, "房间已满（双玩家位占用）", "JoinRoom"); return; }
                    conn.ChairSeat = -1;
                    conn.MatchSeat = -1;
                    conn.Nickname = string.IsNullOrEmpty(join.Nickname) ? $"观战者{_spectators.Count + 1}" : join.Nickname;
                    conn.OpenId = join.OpenId ?? "";
                    conn.EventCursor = NetEventProjector.Events.Count; // 与 AsSpectator 同口径（从当前时点看）
                    conn.SnapshotDirty = _phase == NetRoomPhase.Playing;
                    _spectators.Add(conn);
                    if (conn.IsHost) _host = conn;
                    _roomStateDirty = true;
                    return;
                }
            }

            conn.ChairSeat = seat;
            conn.MatchSeat = -1;
            conn.Nickname = string.IsNullOrEmpty(join.Nickname) ? $"玩家{seat + 1}" : join.Nickname;
            conn.OpenId = join.OpenId ?? ""; // v11 对局记录：进房方自报（观战/局域网/AI 为空）
            conn.IsAi = join.IsAi;           // v11：AI 填位标记——AI 局不下发终局摘要
            _joinDeckNames[seat] = join.DeckName ?? ""; // S5：大厅选中卡组随进房上报（RoomState 快照带出）
            _chairs[seat] = conn;
            if (conn.IsHost) _host = conn;
            _ready[seat] = false; // 新入座者未准备（重进房/换人：准备态不继承）
            _rankedAck[seat] = null; // S8：接受态不继承（换人重报）
            _roomStateDirty = true;

            // 2026-10-28 就绪层：真人房主房等待 RoomStart；无头房主房（autoStart）双座满即开局
            if (_autoStartWhenAllReady && _chairs[0] != null && _chairs[1] != null)
                _battle.BeginDeckPickPhase(_settingsFirstMove, _settingsShuffle, _settingsSeed);

            // S8 排位：双座满进 5s 双接受窗（替代准备/房主开局——收齐双 true 服务器自动开局）
            if (_ranked && _chairs[0] != null && _chairs[1] != null)
                _rankedAckDeadlineUtc = DateTime.UtcNow.AddSeconds(RankedAckSeconds);
        }

        /// <summary>排位接受应答（S8；仅排位房+Waiting+玩家椅）。双方都 true → 开局；
        /// 任一 false → 广播 Error 并解散（双方回 Idle，不自动重排——2026-11-10 定案）。</summary>
        private void HandleRankedAck(INetChannel conn, NetworkMessage msg)
        {
            if (!_ranked) { SendError(conn, "本房间不是排位房", "RankedAck"); return; }
            if (conn.ChairSeat != 0 && conn.ChairSeat != 1) { SendError(conn, "非玩家座位不能应答排位", "RankedAck"); return; }
            if (_phase != NetRoomPhase.Waiting) { SendError(conn, "当前阶段不能应答排位", "RankedAck"); return; }
            var ack = NetworkSerializer.DeserializePayload<MsgRankedAck>(msg);
            if (ack == null) { SendError(conn, "RankedAck 载荷损坏", "RankedAck"); return; }

            _rankedAck[conn.ChairSeat] = ack.Accepted;
            _roomStateDirty = true;
            if (_rankedAck[0] == true && _rankedAck[1] == true)
            {
                _rankedAckDeadlineUtc = null; // 收齐双 true——开局（房主收齐 ack 才开局的权威侧）
                _battle.BeginDeckPickPhase(_settingsFirstMove, _settingsShuffle, _settingsSeed);
                return;
            }
            if (_rankedAck[0] == false || _rankedAck[1] == false)
                RejectRanked("排位未接受");
        }

        /// <summary>排位匹配失败收口（S8）：广播 Error(Ranked) + 双方退房断开——两端各自回 Idle。
        /// 注意：不走 OnDisconnect（房主断开=解散的通用语义会把「排位未接受」二次广播成房主退出）。</summary>
        private void RejectRanked(string reason)
        {
            _rankedAckDeadlineUtc = null;
            TideLog.Info($"[NetRoom] 排位收口：{reason}——广播并解散");
            BroadcastError(reason, "Ranked");
            _host = null;
            for (int seat = 0; seat < 2; seat++)
            {
                var c = _chairs[seat];
                if (c == null) continue;
                _chairs[seat] = null;
                _ready[seat] = false;
                _rankedAck[seat] = null;
                c.Close();       // 客户端 OnDisconnected → 回列表/Idle
            }
            EnterPhase(NetRoomPhase.Finished);
        }

        // ============================================================ 就绪层（2026-10-28 开局流程改版 / v17 逐座化） ============================================================

        /// <summary>房主配置模式（仅房主连接、仅 Waiting 相位）。</summary>
        private void HandleRoomSettings(INetChannel conn, NetworkMessage msg)
        {
            if (!IsHostConnection(conn)) { SendError(conn, "只有房主能配置模式", "RoomSettings"); return; }
            if (_phase != NetRoomPhase.Waiting) { SendError(conn, "开局后不能再改模式", "RoomSettings"); return; }
            var s = NetworkSerializer.DeserializePayload<MsgRoomSettings>(msg);
            if (s == null) { SendError(conn, "RoomSettings 载荷损坏", "RoomSettings"); return; }
            if (s.FirstMoveMode < 0 || s.FirstMoveMode > 2 || s.ShuffleMode < 0 || s.ShuffleMode > 1)
            { SendError(conn, "模式取值非法", "RoomSettings"); return; }
            if (s.Seed < 0)
            { SendError(conn, "随机种子须为非负整数", "RoomSettings"); return; }
            _settingsFirstMove = s.FirstMoveMode;
            _settingsShuffle = s.ShuffleMode;
            _settingsSeed = s.Seed; // v13：0=开局随机定死；钉值=复现局
            _roomStateDirty = true;
        }

        /// <summary>逐座准备 toggle（Waiting；非房主座位。房主不准备——开局权在房主，落椅也不准备）。</summary>
        private void HandleRoomReady(INetChannel conn, NetworkMessage msg)
        {
            if (conn.ChairSeat != 0 && conn.ChairSeat != 1) { SendError(conn, "非玩家座位不能准备", "RoomReady"); return; }
            if (IsHostConnection(conn)) { SendError(conn, "房主无需准备——直接点「开始」", "RoomReady"); return; }
            if (_phase != NetRoomPhase.Waiting) { SendError(conn, "当前阶段不能改准备状态", "RoomReady"); return; }
            var r = NetworkSerializer.DeserializePayload<MsgRoomReady>(msg);
            if (r == null) { SendError(conn, "RoomReady 载荷损坏", "RoomReady"); return; }
            _ready[conn.ChairSeat] = r.Ready;
            _roomStateDirty = true;
        }

        /// <summary>房主开始（门槛：双座满 + 全体非房主座位已准备）→ 进入选卡组相位。
        /// **不要求房主在座上**：房主可自身观战 + 双 AI 对打（2026-11-11 用户定案）。</summary>
        private void HandleRoomStart(INetChannel conn)
        {
            if (!IsHostConnection(conn)) { SendError(conn, "只有房主能开始", "RoomStart"); return; }
            if (_phase != NetRoomPhase.Waiting) { SendError(conn, "当前阶段不能开始", "RoomStart"); return; }
            if (_chairs[0] == null || _chairs[1] == null) { SendError(conn, "双方就座后才能开始", "RoomStart"); return; }
            if (!AllNonHostSeatsReady()) { SendError(conn, "还有座位未准备", "RoomStart"); return; }
            _battle.BeginDeckPickPhase(_settingsFirstMove, _settingsShuffle, _settingsSeed);
        }

        /// <summary>座位↔观战切换（v16 2026-11-11 多成员房间 / v17 房主可切换）：座上转观战=释放椅位
        /// （昵称保留、清准备/接受态/上报卡组名）；观战者入座=任意空位（满拒）。仅 Waiting（对局中
        /// 玩家位锁定语义不变）；排位房拒（双接受窗与换座互斥）。方向由载荷显式声明（防连点竞态），
        /// 与现态不符按 Error 拒。房主转观战不丢房主身份（开局权随连接，不随座位）。</summary>
        private void HandleSeatSwap(INetChannel conn, NetworkMessage msg)
        {
            if (conn.ChairSeat == -2) { SendError(conn, "未进房不能换座", "SeatSwap"); return; }
            if (_ranked) { SendError(conn, "排位局不支持换座", "SeatSwap"); return; }
            if (_phase != NetRoomPhase.Waiting)
            { SendError(conn, "对局相关阶段不能换座（玩家位锁定）", "SeatSwap"); return; }
            var swap = NetworkSerializer.DeserializePayload<MsgSeatSwap>(msg);
            if (swap == null) { SendError(conn, "SeatSwap 载荷损坏", "SeatSwap"); return; }

            if (swap.ToSpectator)
            {
                if (conn.ChairSeat < 0) { SendError(conn, "已在观战位", "SeatSwap"); return; }
                int seat = conn.ChairSeat;
                _chairs[seat] = null;
                _ready[seat] = false;
                _joinDeckNames[seat] = "";
                _rankedAck[seat] = null;
                conn.ChairSeat = -1;
                conn.MatchSeat = -1;
                conn.EventCursor = NetEventProjector.Events.Count; // Waiting 期恒 0，与观战同口径
                conn.SnapshotDirty = false;
                _spectators.Add(conn);
                _roomStateDirty = true;
                TideLog.Info($"[NetRoom] 座位切换：{conn.Nickname} 释放椅 {seat} 转观战（观战 {_spectators.Count}）");
                return;
            }

            // 观战 → 入座（任意空位，满拒）
            if (conn.ChairSeat >= 0) { SendError(conn, "已在座位上", "SeatSwap"); return; }
            int free = _chairs[0] == null ? 0 : (_chairs[1] == null ? 1 : -1);
            if (free < 0) { SendError(conn, "座位已满（双玩家位占用）", "SeatSwap"); return; }
            _spectators.Remove(conn);
            _chairs[free] = conn;
            conn.ChairSeat = free;
            _ready[free] = false;
            _joinDeckNames[free] = "";
            _rankedAck[free] = null;
            _roomStateDirty = true;
            TideLog.Info($"[NetRoom] 座位切换：{conn.Nickname} 观战转椅 {free}");
        }

        /// <summary>全体非房主座位是否都已准备（开局门槛；空椅=未满足）。</summary>
        private bool AllNonHostSeatsReady()
        {
            for (int seat = 0; seat < 2; seat++)
            {
                var conn = _chairs[seat];
                if (conn == null) return false;
                if (IsHostConnection(conn)) continue;
                if (!_ready[seat]) return false;
            }
            return true;
        }

        // ============================================================ 断线 / 重置 ============================================================

        /// <summary>成员断开（逻辑线程；由宿主泵发现）。房主断开=解散整房（P2P 无服务器可转移，
        /// 2026-11-11 用户定案）；客人断开按阶段收口（Waiting/DeckSubmit 退位，Playing 作废对局）。</summary>
        public void OnDisconnect(INetChannel conn)
        {
            if (conn.ChairSeat == -2) return; // 未进房的连接：无房间侧状态
            if (_dissolved) return;

            // v17：房主退出=解散（只剩 AI 时同样成立——宿主停机即对局结束）
            if (IsHostConnection(conn))
            {
                DissolveRoom($"房主 {conn.Nickname} 已退出——房间解散");
                return;
            }

            if (conn.ChairSeat == -1)
            {
                _spectators.Remove(conn); // 观战离开不影响对局
                _roomStateDirty = true;
                return;
            }

            int seat = conn.ChairSeat;
            _chairs[seat] = null;
            _ready[seat] = false;
            _joinDeckNames[seat] = null; // S5：退位即清卡组名（快照空座位不带残值）
            _rankedAck[seat] = null;     // S8：接受态随退位清（残座未决窗=对方超时收口）
            if (_rankedAckDeadlineUtc != null && (_chairs[0] == null || _chairs[1] == null))
                _rankedAckDeadlineUtc = null; // S8：缺员即撤接受窗（RejectRanked/正常开局外的第三路）

            switch (_phase)
            {
                case NetRoomPhase.Waiting:
                    break; // 未满员：退位即可（准备态已在下座时清）

                case NetRoomPhase.DeckSubmit:
                    // 缺员退回等进房：另一座位的提交作废（卡组对局必须双座位同批提交）；
                    // 就绪层状态复位（先手/倒计时作废、准备清空——重新走准备→开局）
                    _battle.ResetDecks();
                    _ready[0] = false;
                    _ready[1] = false;
                    EnterPhase(NetRoomPhase.Waiting);
                    break;

                case NetRoomPhase.Playing:
                    // 对局作废（M2 最小策略；重连留 M4）：作废全部未决反问防泵悬死，
                    // 广播 Error + RoomState(Finished)——不判胜负。
                    _battle.VoidMatch($"对局作废：座位 {seat}（{conn.Nickname}）断线");
                    break;

                case NetRoomPhase.Finished:
                    break;
            }
            _roomStateDirty = true;
        }

        /// <summary>房主退出/房间解散（v17）：广播 Error(房间通道) → 拆对局 → 断开全部成员 → 清空房间。
        /// 只发生在没有服务器可转移的 P2P 拓扑（房主进程退出即整房结束）。幂等。</summary>
        private void DissolveRoom(string reason)
        {
            if (_dissolved) return;
            _dissolved = true;
            TideLog.Info($"[NetRoom] 解散：{reason}");
            BroadcastError(reason, "Room", NetworkChannel.Room);
            _battle.TeardownIfActive();
            EnterPhase(NetRoomPhase.Finished);

            var members = AllMembers().ToList();
            for (int seat = 0; seat < 2; seat++) _chairs[seat] = null;
            _spectators.Clear();
            _host = null;
            _ready[0] = _ready[1] = false;
            _joinDeckNames[0] = _joinDeckNames[1] = "";
            _rankedAckDeadlineUtc = null;
            _roomStateDirty = false; // 无成员：无需广播
            _battle.ResetDecks();
            foreach (var member in members) member.Close(); // 客户端 OnDisconnected → 回列表
        }

        /// <summary>全员离场 → 房间回 Waiting（重开 = 重新进房+重新提交）。模式设置保留（房主重进可续用）。</summary>
        public void ResetIfEmpty()
        {
            if (!IsEmpty || _phase != NetRoomPhase.Finished) return;
            _battle.TeardownIfActive();
            _battle.ResetDecks();
            EnterPhase(NetRoomPhase.Waiting);
            _ready[0] = false;
            _ready[1] = false;
            _dissolved = false;
            _host = null;
            _roomStateDirty = false; // 无成员：无需广播
        }

        // ============================================================ 泵（逻辑线程每 tick） ============================================================

        /// <summary>引擎推进（对战通道）：阶段由房间通道把关，本口只转发。</summary>
        public void PumpEngine(GameCore core) => _battle.PumpEngine(core);

        /// <summary>下行出队（通道分道）：排位接受窗 → 选卡组倒计时（房态相关计时，先泵再广播）→
        /// 房间通道房态广播 → 对战通道事件/行流/快照。</summary>
        public void FlushDownlink(GameCore core)
        {
            PumpRankedAckWindow();
            _battle.PumpDeckPickCountdown();

            if (_roomStateDirty)
            {
                _roomStateDirty = false;
                var state = BuildRoomState();
                foreach (var member in AllMembers())
                    member.SendPayload(NetworkMessageType.RoomState, state);
            }

            _battle.FlushDownlink(core);
        }

        /// <summary>排位接受窗泵（S8；每 tick，房间通道出队头部）：双座满 5s 内未收齐双 true →
        /// 广播 Error("排位未接受","Ranked") 并解散——双方各自回 Idle（不自动重排）。</summary>
        private void PumpRankedAckWindow()
        {
            if (_rankedAckDeadlineUtc == null) return;
            if (DateTime.UtcNow < _rankedAckDeadlineUtc.Value) return;
            RejectRanked($"排位未接受（{RankedAckSeconds} 秒内未确认）");
        }

        /// <summary>停机强制收口（NetSessionServer.Stop 调用）：拆对局接线（对战通道幂等）。</summary>
        public void TeardownIfActive() => _battle.TeardownIfActive();

        // ============================================================ 房间通道内部口（对战通道/验证器共用） ============================================================

        /// <summary>椅位连接表（对战通道只读：座位映射/定向发帧/终局摘要）。</summary>
        internal INetChannel[] Chairs => _chairs;

        /// <summary>观战连接表（对战通道只读：开局游标对齐）。</summary>
        internal List<INetChannel> Spectators => _spectators;

        /// <summary>排位房标记（终局摘要 Ranked 字段）。</summary>
        internal bool Ranked => _ranked;

        /// <summary>洗牌模式镜像（对战通道开局锁牌序用；0随机/1固定）。</summary>
        internal bool ShuffleModeLocked => _settingsShuffle == 1;

        /// <summary>房主连接（未自报时回落椅 0——无头/验证器拓扑）。
        /// 房主可落任意椅或观战，身份不跟座位走。</summary>
        internal INetChannel HostConnection => _host ?? _chairs[0];

        internal bool IsHostConnection(INetChannel conn)
            => conn != null && ReferenceEquals(conn, HostConnection);

        /// <summary>阶段迁移（房间/对战通道共用唯一写入口：置阶段 + 房态置脏）。</summary>
        internal void EnterPhase(NetRoomPhase phase)
        {
            _phase = phase;
            _roomStateDirty = true;
        }

        internal void MarkRoomStateDirty() => _roomStateDirty = true;

        internal IEnumerable<INetChannel> AllMembers()
        {
            foreach (var chair in _chairs)
                if (chair != null) yield return chair;
            foreach (var spec in _spectators)
                yield return spec;
        }

        internal void MarkSnapshotDirty()
        {
            foreach (var member in AllMembers())
                member.SnapshotDirty = true;
        }

        internal INetChannel SeatConnection(int matchSeat)
            => _chairs.FirstOrDefault(c => c != null && c.MatchSeat == matchSeat);

        /// <summary>验证口（V11 对拍）：读当前行流历史快照（seat=0/1/-1；未安装返回 null）。</summary>
        internal List<MatchLogLine> LogHistorySnapshot(int seat) => _battle.LogHistorySnapshot(seat);

        private MsgRoomState BuildRoomState()
        {
            var players = new MsgRoomSeatInfo[2];
            for (int i = 0; i < 2; i++)
            {
                var conn = _chairs[i];
                bool isHost = IsHostConnection(conn);
                players[i] = new MsgRoomSeatInfo
                {
                    Seat = i,
                    Nickname = conn?.Nickname ?? "",
                    Connected = conn != null,
                    DeckName = conn != null ? (_joinDeckNames[i] ?? "") : "", // S5：进房上报的卡组名（座位卡展示）
                    Ready = conn != null && !isHost && _ready[i],              // v17：逐座准备（房主座位恒 false）
                    IsAi = conn?.IsAi ?? false,                                // v17：AI 填位
                    IsHost = isHost,                                           // v17：座位是否房主
                };
            }
            return new MsgRoomState
            {
                RoomId = _roomId,
                Phase = (int)_phase,
                Players = players,
                SpectatorCount = _spectators.Count,
                FirstSeatThisMatch = _battle.FirstSeat,
                FirstMoveMode = _settingsFirstMove,
                ShuffleMode = _settingsShuffle,
                GuestReady = AllNonHostSeatsReady(), // v17：兼容字段=全体非房主座位就绪
                HostName = HostConnection?.Nickname ?? "", // v17：房主昵称（空=无房主）
                DeckPickSecondsLeft = _battle.DeckPickSecondsLeft,
                Seed = _battle.MatchSeed, // v13：开局定死回显（0=未开局）——客户端日志展示/复制复现
            };
        }

        internal void SendError(INetChannel conn, string reason, string context,
            NetworkChannel channel = NetworkChannel.Room)
            => conn.SendPayload(NetworkMessageType.Error, new MsgError
            {
                Reason = reason,
                Context = context,
                Channel = (int)channel,
            });

        internal void BroadcastError(string reason, string context = "Room",
            NetworkChannel channel = NetworkChannel.Room)
        {
            var err = new MsgError { Reason = reason, Context = context, Channel = (int)channel };
            foreach (var member in AllMembers())
                member.SendPayload(NetworkMessageType.Error, err);
        }
    }
}
