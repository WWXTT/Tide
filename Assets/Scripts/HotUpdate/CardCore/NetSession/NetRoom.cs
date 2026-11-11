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
    /// 2026-10-28 开局流程改版：Waiting 不再「就座即迁 DeckSubmit」，改为就绪层——
    /// 房主（椅位0）配模式（先后手方式+洗牌）/客人（椅位1）准备 toggle/房主 RoomStart 开局；
    /// 开局时先手按模式确定（掷币/房主先/客先）+ 起 30s 选卡组倒计时（超时服务器替落随机预组）。
    ///
    /// - 分座：JoinRoom 占椅位（0/1/观战）；房主开局（或 autoStart 房双座满）→ DeckSubmit。
    /// - 握手驱动（§11 消息流接线）：DeckSubmit → NetMatchHandshake.ValidateDeckSubmit →
    ///   双座位通过 → BuildMatchManifest 各发各座 → 开局。
    /// - 换先手（训练桥先例）：先手椅位其卡组进 Player1（引擎座位恒 0 先手）——
    ///   零引擎改动；椅位↔引擎座位映射 = MatchManifest.OwnSeat 权威 + RoomState.FirstSeatThisMatch。
    /// - 观战：全信息（2026-09-23 定案）——事件流不滤、快照走 fullInfo 视角；随时可进。
    /// - 断线：Waiting/DeckSubmit 退位回 Waiting；Playing 玩家断线 → 对局作废（重连留 M4）。
    /// - 线程：全部方法只允许逻辑线程（NetSessionServer.Pump）调用；IO 线程只入队。
    /// </summary>
    public sealed class NetRoom
    {
        /// <summary>选卡组倒计时时长（秒；2026-10-28 用户定案 30s，服务器权威）。</summary>
        public const int DeckPickSeconds = 30;

        /// <summary>每回合可用时间（秒；2026-11-11 用户定案「联机也按真人方式跑」——服务器权威计时，
        /// 到点代打：栈上有待响应者=替其让行，否则替回合方结束回合）。调档只改这一个常量。</summary>
        public const int TurnSeconds = 90;

        /// <summary>超时代打后的宽限（秒）：让行/结束回合后若栈上仍有待响应，下一拍不会立刻再罚。</summary>
        public const int TurnTimeoutGraceSeconds = 10;

        /// <summary>排位接受窗时长（秒；S8 2026-11-10 定案 5s——双座位满起算，双方都须点「接受」，
        /// 任一拒绝/超时=广播 Error("排位未接受","Ranked") 并解散，双方各自回 Idle 不自动重排）。</summary>
        public const int RankedAckSeconds = 5;

        private readonly string _roomId;
        private readonly Random _rng;
        private readonly int? _seed;
        private readonly bool _autoStartWhenAllReady; // 无真人房主（无头房主）拓扑：双座满即自动开局（默认模式掷币+随机）
        private readonly string _password; // S5（2026-11-10）房间密码：空/null=无密码房（JoinRoom 不校验）
        private readonly bool _ranked;     // S8（2026-11-10）排位房：双座满后走 RankedAck 双接受窗（替代准备/房主开局）

        // S5：随 JoinRoom 上报的卡组名（房间侧按椅位存——INetChannel 接口在 NetTransports.cs，
        // 不为本轮扩口；RoomState Players 快照带出供座位卡展示。退位即清）
        private readonly string[] _joinDeckNames = new string[2];

        private NetRoomPhase _phase = NetRoomPhase.Waiting;
        private readonly INetChannel[] _chairs = new INetChannel[2];
        private readonly List<INetChannel> _spectators = new List<INetChannel>();
        private readonly string[][] _deckIds = new string[2][];
        // 英雄技能标记卡（2026-10-07 卡牌化）：按椅位随卡组提交存；开局前宽松校验
        //（须 ∈ CardIds 且 HeroSkillSystem.CanBeSkillCard——否则置 null 按无技能开局，不拒握手）
        private readonly string[] _skillIds = new string[2];

        // 衍生物区/槽绑定（P9b 2026-10-29 协议 v5）：按椅位随卡组提交存；开局组卡时模板随组入局
        //（落额外区）+ 绑定盖到卡实例（DeckTokenSupport.Apply）。随机预组代提=两块空（模板在
        // CardIds 内、固定绑定走原子 str）。
        private readonly string[][] _deckTokens = new string[2][];
        private readonly List<SynergyUI.DeckTokenBinding>[] _deckTokenBindings =
            new List<SynergyUI.DeckTokenBinding>[2];

        // 自建内容跨端 + 对局记录（2026-11-09 协议 v11）：按椅位存提交原件（内容载荷/卡组名）——
        // Manifest 期把对手内容下发对方客户端渲染、终局期组装 MsgMatchSummary 双端落对局记录。
        // 服务器替落预组的座位 _submits 为 null（预组内容双方本地恒有，无需载荷）。
        private readonly MsgDeckSubmit[] _submits = new MsgDeckSubmit[2];
        private readonly string[] _deckNames = new string[2];

        private int _firstSeat = -1;          // 本局先手椅位（未开局 -1）
        private bool _roomStateDirty = true;  // RoomState 待广播
        private bool _gameOver;

        // S8 排位接受窗：双座满起算 5s；_rankedAck 按椅位三态（null=未报）
        private readonly bool?[] _rankedAck = new bool?[2];
        private DateTime? _rankedAckDeadlineUtc;

        // 就绪层状态（2026-10-28）：模式（0先后手/1洗牌）+ 按椅位准备态 + 选卡组倒计时
        private int _settingsFirstMove;       // 0=掷币 1=房主(椅0)先 2=客人(椅1)先
        private int _settingsShuffle;         // 0=随机洗牌 1=固定牌序
        private int _settingsSeed;            // v13 房主钉种子（0=开局随机定死）
        private int _matchSeed;               // 本局实际种子（BeginDeckPickPhase 定死；0=未开局——RoomState.Seed 回显）
        private readonly bool[] _ready = new bool[2];
        private DateTime? _pickDeadlineUtc;   // 选卡组倒计时截止（DeckSubmit 相位；null=未在倒计时）
        private int _lastPickSecond = -1;     // 上次广播的剩秒（整秒去重）

        // 回合计时（v14 2026-11-11 服务器权威）：按「回合号+回合方」重置；快照携带剩余秒数供 HUD 倒计时，
        // 到点由 PumpTurnClock 代打（真人/AI 一视同仁——AI 由 NetClientBrain 驱动，同样受此钟约束）。
        private DateTime? _turnDeadlineUtc;
        private int _clockTurn = -1;
        private int _clockActiveSeat = -1;

        // 对局接线（搬 TideHeadlessDriver.ResetCore 骨架）
        private BoardState _board;
        private NetworkTargetSelector _selector;

        /// <summary>seed=null 用时间随机（生产）；钉种子（M3 对拍）时先手椅位与引擎随机流均确定。
        /// autoStartWhenAllReady=无头房主（MatchHost 等）：双座满即开局（跳过准备/房主确认）。
        /// password（S5 2026-11-10）：房间密码——非空时 JoinRoom 须携带同值；默认 null=无密码（既有调用零改动）。
        /// ranked（S8 2026-11-10）：排位房——双座满进 5s 双接受窗（RankedAck），收齐双 true 自动开局。</summary>
        public NetRoom(string roomId, int? seed = null, bool autoStartWhenAllReady = false,
            string password = null, bool ranked = false)
        {
            _roomId = roomId ?? "";
            _seed = seed;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _autoStartWhenAllReady = autoStartWhenAllReady;
            _password = password;
            _ranked = ranked;
        }

        public NetRoomPhase Phase => _phase;
        public int FirstSeat => _firstSeat;
        /// <summary>房间是否已无成员（宿主判断可否重置）。</summary>
        public bool IsEmpty =>
            _chairs.All(c => c == null) && _spectators.Count == 0;

        /// <summary>房间 ID（大厅层列表/加入寻址用，2026-09-24）。</summary>
        public string RoomId => _roomId;

        /// <summary>玩家椅子位空位数（大厅层判断可否入座/配对，2026-09-24）。</summary>
        public int FreeChairCount => _chairs.Count(c => c == null);

        /// <summary>房间当前状态快照（大厅列表/外部展示用——与成员广播同构，2026-09-24）。</summary>
        public MsgRoomState State => BuildRoomState();

        // ============================================================ 上行分派 ============================================================

        /// <summary>处理一条来自成员连接的上行消息（逻辑线程）。</summary>
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
                switch (msg.Type)
                {
                    case NetworkMessageType.JoinRoom:
                        var join = NetworkSerializer.DeserializePayload<MsgJoinRoom>(msg);
                        if (join == null) { SendError(conn, "JoinRoom 载荷损坏", "JoinRoom"); break; }
                        HandleJoin(conn, join);
                        break;

                    case NetworkMessageType.DeckSubmit:
                        HandleDeckSubmit(conn, msg);
                        break;

                    case NetworkMessageType.SelectResponse:
                        HandleSelectResponse(conn, msg);
                        break;

                    case NetworkMessageType.TextResponse:
                        HandleTextResponse(conn, msg);
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

                    case NetworkMessageType.Ping:
                        conn.SendPayload<object>(NetworkMessageType.Pong, null);
                        break;

                    default:
                        // intent 段=IntentPlayCard..IntentConcede 连续段（2026-10-31 阶段D 重排后 100..108 九连无洞）
                        // + 离散位（IntentActivateHeroSkill=112 在会话层消息 SelectResponse/DeckSubmit/JoinRoom 之后）
                        bool isIntent = (msg.Type >= NetworkMessageType.IntentPlayCard
                                            && msg.Type <= NetworkMessageType.IntentConcede)
                                        || msg.Type == NetworkMessageType.IntentActivateHeroSkill;
                        if (isIntent)
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
            conn.OpenId = join.OpenId ?? ""; // v11 对局记录：进房方自报（观战/局域网/AI 为空）
            conn.IsAi = join.IsAi;           // v11：AI 填位标记——AI 局不下发终局摘要
            _joinDeckNames[seat] = join.DeckName ?? ""; // S5：大厅选中卡组随进房上报（RoomState 快照带出）
            _chairs[seat] = conn;
            _ready[seat] = false; // 新入座者未准备（重进房/换人：准备态不继承）
            _rankedAck[seat] = null; // S8：接受态不继承（换人重报）
            _roomStateDirty = true;

            // 2026-10-28 就绪层：真人房主房等待 RoomStart；无头房主房（autoStart）双座满即开局
            if (_autoStartWhenAllReady && _chairs[0] != null && _chairs[1] != null)
                BeginDeckPickPhase();

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
                BeginDeckPickPhase();
                return;
            }
            if (_rankedAck[0] == false || _rankedAck[1] == false)
                RejectRanked("排位未接受");
        }

        /// <summary>排位匹配失败收口（S8）：广播 Error(Ranked) + 双方退房断开——两端各自回 Idle。</summary>
        private void RejectRanked(string reason)
        {
            _rankedAckDeadlineUtc = null;
            TideLog.Info($"[NetRoom] 排位收口：{reason}——广播并解散");
            BroadcastError(reason, "Ranked");
            for (int seat = 0; seat < 2; seat++)
            {
                var c = _chairs[seat];
                if (c == null) continue;
                OnDisconnect(c); // 清椅/准备态/接受态（Waiting 相位收口路径）
                c.Close();       // 客户端 OnDisconnected → 回列表/Idle
            }
            _roomStateDirty = true;
        }

        // ============================================================ 就绪层（2026-10-28 开局流程改版） ============================================================

        /// <summary>房主配置模式（仅椅位0=房主、仅 Waiting 相位）。</summary>
        private void HandleRoomSettings(INetChannel conn, NetworkMessage msg)
        {
            if (conn.ChairSeat != 0) { SendError(conn, "只有房主能配置模式", "RoomSettings"); return; }
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

        /// <summary>准备 toggle（Waiting 相位；椅位1=客人的准备是房主开局门槛；椅位0 房主自身无意义但不禁）。</summary>
        private void HandleRoomReady(INetChannel conn, NetworkMessage msg)
        {
            if (conn.ChairSeat != 0 && conn.ChairSeat != 1) { SendError(conn, "非玩家座位不能准备", "RoomReady"); return; }
            if (_phase != NetRoomPhase.Waiting) { SendError(conn, "当前阶段不能改准备状态", "RoomReady"); return; }
            var r = NetworkSerializer.DeserializePayload<MsgRoomReady>(msg);
            if (r == null) { SendError(conn, "RoomReady 载荷损坏", "RoomReady"); return; }
            _ready[conn.ChairSeat] = r.Ready;
            _roomStateDirty = true;
        }

        /// <summary>房主开始（门槛：双方就座+客人已准备）→ 进入选卡组相位：先手按模式确定 + 30s 倒计时起算。</summary>
        private void HandleRoomStart(INetChannel conn)
        {
            if (conn.ChairSeat != 0) { SendError(conn, "只有房主能开始", "RoomStart"); return; }
            if (_phase != NetRoomPhase.Waiting) { SendError(conn, "当前阶段不能开始", "RoomStart"); return; }
            if (_chairs[0] == null || _chairs[1] == null) { SendError(conn, "双方就座后才能开始", "RoomStart"); return; }
            if (!_ready[1]) { SendError(conn, "对手还未准备", "RoomStart"); return; }
            BeginDeckPickPhase();
        }

        /// <summary>进入选卡组相位（房主 RoomStart / autoStart 双座满共用）：先手确定 + 倒计时起算 + 广播。
        /// v13：本局种子在此定死（房主钉值 ?? ctor 种子 ?? 随机）——此后设置冻结，RoomState.Seed 回显。</summary>
        private void BeginDeckPickPhase()
        {
            _firstSeat = _settingsFirstMove == 1 ? 0 : _settingsFirstMove == 2 ? 1 : _rng.Next(2);
            _matchSeed = _settingsSeed != 0 ? _settingsSeed : (_seed ?? _rng.Next());
            _pickDeadlineUtc = DateTime.UtcNow.AddSeconds(DeckPickSeconds);
            _lastPickSecond = -1;
            _phase = NetRoomPhase.DeckSubmit;
            _roomStateDirty = true;
            TideLog.Info($"[NetRoom] 开局：先手椅位 {_firstSeat}（模式 {_settingsFirstMove}/{_settingsShuffle}），"
                + $"种子 {_matchSeed}{(_settingsSeed != 0 ? "（房主钉）" : "")}，选卡组倒计时 {DeckPickSeconds}s");
        }

        private void HandleDeckSubmit(INetChannel conn, NetworkMessage msg)
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

            // v11 自建内容跨端：先装瞬态覆盖层再校验——提交方随卡组携带非基础池卡/效果/词条的
            // 完整内容，校验/摘要按「本地表+覆盖层」计算（此前对端缺卡恒拒局，自建卡无法跨玩家
            // 对战）。被拒提交的残留载荷无害（add-if-missing 不覆盖本地、不落盘，Teardown 统一清）。
            TransientNetContent.Install(submit.AddedCards, submit.AddedEffects, submit.AddedRows);

            if (!NetMatchHandshake.ValidateDeckSubmit(submit, out var reason))
            {
                SendError(conn, reason, "DeckSubmit"); // 拒绝后保持等该座位重提
                return;
            }

            _deckIds[conn.ChairSeat] = submit.CardIds;
            _skillIds[conn.ChairSeat] = SanitizeSkillCardId(submit.SkillCardId, submit.CardIds, conn.ChairSeat);
            _deckTokens[conn.ChairSeat] = submit.TokenCardIds ?? Array.Empty<string>();
            _deckTokenBindings[conn.ChairSeat] = NetMatchHandshake.FromWireBindings(submit.TokenBindings);
            _submits[conn.ChairSeat] = submit;
            _deckNames[conn.ChairSeat] = submit.DeckName;
            if (_deckIds[0] != null && _deckIds[1] != null)
            {
                _pickDeadlineUtc = null; // 双方确认完毕：倒计时退役
                StartMatch();
            }
        }

        /// <summary>技能标记宽松校验（2026-10-15 改版：技能=任选一张法术，**不要求在卡组内**）：
        /// 非空须为卡池内合格技能法术（1~3 主动效果）；不合格置 null 按无技能开局
        ///（摘要口径不含技能标记，不构成拒握手理由）。</summary>
        private string SanitizeSkillCardId(string skillCardId, string[] cardIds, int seat)
        {
            if (string.IsNullOrEmpty(skillCardId)) return null;
            var data = CardCatalog.GetById(skillCardId);
            if (data == null || !HeroSkillSystem.CanBeSkillCard(data))
            {
                TideLog.Warn($"[NetRoom] 座位 {seat} 技能标记 {skillCardId} 资格不符（非法术或效果非 1~3 主动）——按无技能开局");
                return null;
            }
            return skillCardId;
        }

        private void HandleSelectResponse(INetChannel conn, NetworkMessage msg)
        {
            if (_phase != NetRoomPhase.Playing || _selector == null) return;
            var resp = NetworkSerializer.DeserializePayload<MsgSelectResponse>(msg);
            if (resp == null) return;
            if (_selector.TryComplete(resp)) return;

            // 迟到/未知 RequestId：静默忽略之外再给一次可诊断回执（引擎侧已按空应答/超时兜底）
            SendError(conn, $"反问应答未知 RequestId={resp.RequestId}（迟到或已作废）", "SelectResponse");
        }

        /// <summary>文本反问应答（2026-10-31 阶段C）：座位+长度校验在 TryCompleteText 内
        ///（不合法按取消 null 收口——效果不结算、费用不退）；迟到/未知 RequestId 给可诊断回执。</summary>
        private void HandleTextResponse(INetChannel conn, NetworkMessage msg)
        {
            if (_phase != NetRoomPhase.Playing || _selector == null) return;
            var resp = NetworkSerializer.DeserializePayload<MsgTextResponse>(msg);
            if (resp == null) return;
            if (_selector.TryCompleteText(resp, conn.MatchSeat)) return;

            SendError(conn, $"文本反问应答未知 RequestId={resp.RequestId}（迟到或已作废）", "TextResponse");
        }

        private void HandleIntent(GameCore core, INetChannel conn, NetworkMessage msg)
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

        /// <summary>双座位卡组就绪 → 开局。换先手 = 谁的卡组进 Player1（引擎座位 0 恒先手）。
        /// 先手椅位已在 BeginDeckPickPhase 按模式确定（2026-10-28 前移）；洗牌模式=固定牌序时锁卡组序。</summary>
        private void StartMatch()
        {
            var core = GameCore.Instance;

            // 组合根注入 + 引擎初始化（镜像 TideHeadlessDriver.ResetCore；InitGame 内含 Reset→
            // NetEventProjector.ClearAll（GameCore.Reset 接线）→ 洗牌 → 起手 → StartGame）。
            // 钉种子（M3 对拍）：_seed 同时进 InitGame——GameRng 与洗牌流（ZoneContainer）双流重播。
            // 引用卡ID 解析器（2026-10-13 临摹退役：原 MorphSystem.ResolveMorphTarget 迁 SummonToken）
            Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            // P9b：卡组期绑定盖到卡实例（效果序号→模板 ID；空表=固定绑定走原子 str）；
            // 衍生物区模板随组入局（IsTokenTemplate 分流落 Zone.Extra）
            // 缺卡响亮拒绝（2026-11-05 ID 冻结收口）：提交链有 ValidateDeckSubmit 全命中校验，
            // 此处 miss = 预组超时替落断链 / 提交后卡表漂移（mtime 重载）——不再静默跳卡变薄，
            // 广播错误并退回提交位（宁可不开局，与「宁可拒不可静默」同口径）。
            var missingFirst = new List<string>();
            var missingSecond = new List<string>();
            var deckFirst = BuildDeck(_deckIds[_firstSeat], _deckTokens[_firstSeat], missingFirst);
            var deckSecond = BuildDeck(_deckIds[1 - _firstSeat], _deckTokens[1 - _firstSeat], missingSecond);
            if (missingFirst.Count > 0 || missingSecond.Count > 0)
            {
                int total = missingFirst.Count + missingSecond.Count;
                var sample = string.Join(", ", missingFirst.Concat(missingSecond).Distinct().Take(3));
                TideLog.Error($"[NetRoom] 开局中止：卡表缺失 {total} 张（如 {sample}）——预组断链或表内容漂移");
                BroadcastError($"开局中止：卡组引用的 {total} 张卡不在本端卡表（预组断链或内容漂移）——请重新提交卡组");
                if (missingFirst.Count > 0) ResetSeatDeck(_firstSeat);
                if (missingSecond.Count > 0) ResetSeatDeck(1 - _firstSeat);
                _roomStateDirty = true;
                return;
            }
            core.InitGame(CardLoader.BuildDeck(deckFirst, 1, _deckTokenBindings[_firstSeat]),
                CardLoader.BuildDeck(deckSecond, 1, _deckTokenBindings[1 - _firstSeat]), _matchSeed,
                lockDeckOrder: _settingsShuffle == 1,
                skillCardId1: _skillIds[_firstSeat], skillCardId2: _skillIds[1 - _firstSeat]);
            core.Player1.IsAI = false; // 人类在环：反问走 NetworkTargetSelector
            core.Player2.IsAI = false;

            // 后手开局护甲补偿（2026-10-30 补齐：本地局同规则——引擎座位 1 恒后手〔换先手=换卡组
            // 进 Player1〕；InitGame 内含 Reset→StartGame，注入时点在 Reset 清角色指示物之后，
            // 时序镜像 BattleController.StartNewGame。注入先于 EventCursor=0 对齐——首帧事件/
            // 快照都带护甲，客户端 PlayerState.Counters 直接可见）。
            core.Player2.AddCounters(KeywordRules.ArmorCounter, GameCore.SecondPlayerArmorBonus, core.Player2);
            TideLog.Info($"[NetRoom] 后手 {core.Player2.Name} 开局护甲 +{GameCore.SecondPlayerArmorBonus}（先后手补偿）");

            // 棋盘占用层接线（碾压邻接 + 连接光环；核心不绑棋盘，宿主接线——静态扩展点惯例）
            _board?.Dispose();
            GameBoard.LinkAuraSystem.Detach();
            _board = new BoardState(core, core.Player1, core.Player2,
                HalfFieldData.Flat(), HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.FlankNeighbors; // 碾压=左右同排生物（2026-10-04 语义修订）
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole; // 碾压攻角色=角色格左右邻格（2026-10-16 改版）
            GameBoard.LinkAuraSystem.Attach(_board);

            // 反问桥：SendAsync = 按 ChooserSeat 定向出队（返回已完成任务——实际字节由泵的出队段写）；
            // 文本反问同构第二条通道（2026-10-31 阶段C：预言类隐秘宣言只在 Chooser 座位连接下发）
            _selector?.Detach();
            _selector = new NetworkTargetSelector
            {
                SendAsync = request =>
                {
                    var target = SeatConnection(request.ChooserSeat);
                    target?.SendPayload(NetworkMessageType.SelectRequest, request);
                    return Cysharp.Threading.Tasks.UniTask.CompletedTask;
                },
                SendTextAsync = request =>
                {
                    var target = SeatConnection(request.ChooserSeat);
                    target?.SendPayload(NetworkMessageType.TextRequest, request);
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

            // 对局清单：各座位视角各发一份（OwnSeat=引擎座位权威；摘要按该座位卡组计算——含 P9b TokenSig）
            for (int chair = 0; chair < 2; chair++)
            {
                var conn = _chairs[chair];
                if (conn == null) continue;
                var manifest = NetMatchHandshake.BuildMatchManifest(
                    conn.MatchSeat, _deckIds[chair], _deckIds[1 - chair].Length,
                    _deckTokens[chair], _deckTokenBindings[chair]);
                // v11：对手自建内容随清单下发（卡/效果/词条定义——对方客户端渲染对手卡用；
                // 只传内容定义不传卡组构成，局内隐藏口径不变；终局完整卡组走 MatchSummary）
                var opponent = _submits[1 - chair];
                manifest.OpponentCards = opponent?.AddedCards ?? Array.Empty<MsgCardPayload>();
                manifest.OpponentEffects = opponent?.AddedEffects ?? Array.Empty<MsgEffectPayload>();
                manifest.OpponentRows = opponent?.AddedRows ?? Array.Empty<MsgAtomRowPayload>();
                // S7b 掷币段展示：对手费用直方图按对手 DeckSubmit 主列表统计（替落预组=空数组）
                manifest.OpponentCostHistogram = opponent != null
                    ? NetMatchHandshake.CostHistogramOf(opponent.CardIds)
                    : Array.Empty<int>();
                conn.SendPayload(NetworkMessageType.MatchManifest, manifest);
            }

            _gameOver = false;
            _phase = NetRoomPhase.Playing;
            // v14：回合计时起钟（本局第一拍由 PumpTurnClock 按回合号/回合方重置；此处先给满时长，
            // 保证 StartMatch 当拍下发的首快照就带 TurnSecondsLeft）
            _turnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds);
            _clockTurn = -1;
            _clockActiveSeat = -1;
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
            SendMatchSummaries(e); // v11 对局记录：终局摘要双座位各发一份
        }

        /// <summary>终局摘要（2026-11-09 协议 v11 对局记录）：双座位各收一份，视角=收件人的对手——
        /// 对手完整卡组（对局已结束，局内「卡组构成隐藏」不再适用）+ 自建内容载荷 + 收件人胜负 +
        /// 战报全文（房主侧引擎事件流渲染——客人端无本地引擎，战报只能随摘要携带）。AI 填位局
        /// 不下发（对手内容=本机预组，无记录价值）；替落预组座位（_submits 空）照发——卡组数据
        /// 在 _deckIds 系列恒有。客户端收到后落 EncounterRecord 随云存档同步。</summary>
        private void SendMatchSummaries(GameOverEvent e)
        {
            if (_chairs.Any(c => c == null || c.IsAi)) return;
            // 胜者座位（Player 无座位字段——工程口径=ReferenceEquals 对 Player1/Player2，TurnEngine 同款）
            var core = GameCore.Instance;
            int winnerSeat = -1;
            if (e.Winner != null && core != null)
            {
                if (ReferenceEquals(e.Winner, core.Player1)) winnerSeat = 0;
                else if (ReferenceEquals(e.Winner, core.Player2)) winnerSeat = 1;
            }
            string log = MatchLogService.ExportMarkdownText();
            long finishedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (int chair = 0; chair < 2; chair++)
            {
                int opp = 1 - chair;
                var oppSubmit = _submits[opp];
                bool win = winnerSeat >= 0 && winnerSeat == _chairs[chair].MatchSeat;
                _chairs[chair].SendPayload(NetworkMessageType.MatchSummary, new MsgMatchSummary
                {
                    FinishedAtUnix = finishedAt,
                    OpponentName = _chairs[opp].Nickname,
                    OpponentOpenId = _chairs[opp].OpenId ?? "",
                    Win = win,
                    OwnDeckName = _deckNames[chair] ?? "",
                    OpponentDeckName = _deckNames[opp] ?? "",
                    OpponentCardIds = _deckIds[opp] ?? Array.Empty<string>(),
                    OpponentSkillCardId = _skillIds[opp],
                    OpponentTokenCardIds = _deckTokens[opp] ?? Array.Empty<string>(),
                    OpponentTokenBindings = oppSubmit?.TokenBindings,
                    Cards = oppSubmit?.AddedCards,
                    Effects = oppSubmit?.AddedEffects,
                    AtomRows = oppSubmit?.AddedRows,
                    Log = log,
                    Ranked = _ranked, // S8：客户端只对排位局按卡组内容指纹记账（DeckRatingStore）
                });
                TideLog.Info($"[NetRoom] 终局摘要已下发 → 座位 {chair}（对手 {_chairs[opp].Nickname}，{(win ? "胜" : "负")}）");
            }
        }

        /// <summary>终局/重置收口：卸反问桥、退订终局、拆棋盘接线、清瞬态内容覆盖层（v11）。</summary>
        private void TeardownMatch()
        {
            _turnDeadlineUtc = null; // v14 回合计时停钟
            _clockTurn = -1;
            _clockActiveSeat = -1;
            _selector?.AbortPending();
            _selector?.Detach();
            _selector = null;
            EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            TransientNetContent.Clear(); // v11：对局期装载的对端自建内容出索引（默认不显示铁律）
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
            GameBoard.LinkAuraSystem.Detach();
            _board?.Dispose();
            _board = null;
        }

        /// <summary>停机强制收口（NetSessionServer.Stop 调用，2026-10-03）：兑现「拆对局接线」——
        /// 正常链路靠 ResetIfEmpty（全员离场才 Teardown），但宿主直接 Stop 时不再 Pump、
        /// 断线不被观察，反问桥（TargetSelectionService.Current）会残留全局静态——
        /// 后续任何本地目标反问都挂死在死选择器上（网络人类 120s 超时后才自动代选）。
        /// TeardownMatch 幂等，与 ResetIfEmpty 重复调用无副作用。</summary>
        public void TeardownIfActive() => TeardownMatch();

        /// <summary>清空某座位的卡组提交态（缺卡中止回退用——镜像 DeckSubmit 相位断线重置块的字段集）。</summary>
        private void ResetSeatDeck(int seat)
        {
            _deckIds[seat] = null;
            _skillIds[seat] = null;
            _deckTokens[seat] = null;
            _deckTokenBindings[seat] = null;
            _submits[seat] = null;
            _deckNames[seat] = null;
        }

        private static List<CardData> BuildDeck(string[] cardIds, string[] tokenCardIds, List<string> missing)
        {
            var deck = new List<CardData>(cardIds.Length);
            foreach (var id in cardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card != null) deck.Add(card);
                else missing.Add(id); // 2026-11-05 响亮拒绝：不再静默跳卡（StartMatch 聚合中止）
            }
            // P9b 衍生物区模板随组入局（落 Zone.Extra）；缺失同入 missing（同口径）
            foreach (var id in tokenCardIds ?? Array.Empty<string>())
            {
                var card = CardCatalog.GetById(id);
                if (card != null) deck.Add(card);
                else missing.Add(id);
            }
            return deck;
        }

        // ============================================================ 断线 / 重置 ============================================================

        /// <summary>成员断开（逻辑线程；由宿主泵发现）。</summary>
        public void OnDisconnect(INetChannel conn)
        {
            if (conn.ChairSeat == -2) return; // 未进房的连接：无房间侧状态

            if (conn.ChairSeat == -1)
            {
                _spectators.Remove(conn); // 观战离开不影响对局
                _roomStateDirty = true;
                return;
            }

            _chairs[conn.ChairSeat] = null;
            _joinDeckNames[conn.ChairSeat] = null; // S5：退位即清卡组名（快照空座位不带残值）
            _rankedAck[conn.ChairSeat] = null;     // S8：接受态随退位清（残座未决窗=对方超时收口）
            if (_rankedAckDeadlineUtc != null && (_chairs[0] == null || _chairs[1] == null))
                _rankedAckDeadlineUtc = null; // S8：缺员即撤接受窗（RejectRanked/正常开局外的第三路）
            switch (_phase)
            {
                case NetRoomPhase.Waiting:
                    _ready[conn.ChairSeat] = false; // 退位即取消准备
                    break; // 未满员：退位即可

                case NetRoomPhase.DeckSubmit:
                    // 缺员退回等进房：另一座位的提交作废（卡组对局必须双座位同批提交）；
                    // 就绪层状态复位（先手/倒计时作废、准备清空——重新走准备→开局）
                    _deckIds[0] = null;
                    _deckIds[1] = null;
                    _skillIds[0] = null;
                    _skillIds[1] = null;
                    _deckTokens[0] = null;
                    _deckTokens[1] = null;
                    _deckTokenBindings[0] = null;
                    _deckTokenBindings[1] = null;
                    _submits[0] = null;
                    _submits[1] = null;
                    _deckNames[0] = null;
                    _deckNames[1] = null;
                    _pickDeadlineUtc = null;
                    _firstSeat = -1;
                    _ready[0] = false;
                    _ready[1] = false;
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

        /// <summary>全员离场 → 房间回 Waiting（重开 = 重新进房+重新提交）。模式设置保留（房主重进可续用）。</summary>
        public void ResetIfEmpty()
        {
            if (!IsEmpty || _phase != NetRoomPhase.Finished) return;
            TeardownMatch();
            _phase = NetRoomPhase.Waiting;
            _deckIds[0] = null;
            _deckIds[1] = null;
            _skillIds[0] = null;
            _skillIds[1] = null;
            _deckTokens[0] = null;
            _deckTokens[1] = null;
            _deckTokenBindings[0] = null;
            _deckTokenBindings[1] = null;
            _submits[0] = null;
            _submits[1] = null;
            _deckNames[0] = null;
            _deckNames[1] = null;
            _firstSeat = -1;
            _pickDeadlineUtc = null;
            _ready[0] = false;
            _ready[1] = false;
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

            PumpTurnClock(core); // 回合计时（v14）：到点代打——真人/AI 同口径

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

        // ============================================================ 回合计时（v14 2026-11-11） ============================================================

        /// <summary>本回合剩余秒数（快照 TurnSecondsLeft 取值；负数=无计时）。</summary>
        public int TurnSecondsLeft
        {
            get
            {
                if (_phase != NetRoomPhase.Playing || _gameOver || _turnDeadlineUtc == null) return -1;
                double left = (_turnDeadlineUtc.Value - DateTime.UtcNow).TotalSeconds;
                return left <= 0 ? 0 : (int)Math.Ceiling(left);
            }
        }

        /// <summary>回合计时泵（每 tick，PumpEngine 头部）：回合号/回合方变化 → 重置满时长；
        /// 到点 → 代打（栈上有待响应者=替其让行；否则替回合方结束回合），随后给宽限秒数防连罚。
        /// 真人/AI 一视同仁：AI 客人同样受此钟约束（brain 在 20ms 内响应，正常局不会触发）。
        /// 代打走 NetworkIntentApplier（与客户端 intent 同一校验路径，拒绝只记日志不抛）。</summary>
        private void PumpTurnClock(GameCore core)
        {
            var te = core.TurnEngine;
            if (te == null) return;

            int turn = te.TurnNumber;
            int active = NetEntityMapper.SeatOf(te.TurnPlayer);
            if (turn != _clockTurn || active != _clockActiveSeat)
            {
                _clockTurn = turn;
                _clockActiveSeat = active;
                _turnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds);
                return;
            }
            if (_turnDeadlineUtc == null)
            {
                _turnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds);
                return;
            }
            if (DateTime.UtcNow < _turnDeadlineUtc.Value) return;

            // 到点：优先权在别处（响应窗口）= 替响应方让行；否则替回合方结束回合
            var engine = core.StackEngine;
            int holder = engine != null ? NetEntityMapper.SeatOf(engine.CurrentPriorityHolder) : -1;
            bool stackPending = engine != null && !engine.IsEmpty;
            if (stackPending && holder >= 0 && holder != active)
                AutoPlayTimeout(core, holder, NetworkMessageType.IntentPassPriority,
                    $"{TurnSeconds}s 响应超时——已自动让行");
            else
                AutoPlayTimeout(core, active, NetworkMessageType.IntentEndTurn,
                    $"{TurnSeconds}s 回合超时——已自动结束回合");

            _turnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnTimeoutGraceSeconds); // 宽限：防同一拍连罚
            MarkSnapshotDirty(); // 剩余秒数随快照回零
        }

        /// <summary>超时代打（无 payload 的 intent——EndTurn/PassPriority 均不需要载荷）。
        /// 拒绝只记日志：宁可本回合拖延，绝不因代打异常拆局。</summary>
        private void AutoPlayTimeout(GameCore core, int seat, NetworkMessageType type, string reason)
        {
            string error;
            bool ok = NetworkIntentApplier.Apply(core, seat,
                NetworkSerializer.BuildEnvelope<object>(type, null), out error);
            if (ok)
            {
                TideLog.Info($"[NetRoom] 超时代打：座位 {seat} → {type}（{reason}）");
                BroadcastError(reason, "TurnTimeout"); // 双端可见（客户端 NetOnError 以提示口径显示）
            }
            else
            {
                TideLog.Warn($"[NetRoom] 超时代打被拒（座位 {seat} / {type}）：{error}——{reason}");
            }
        }

        /// <summary>下行出队：RoomState 广播 → 事件流（按座位过滤，观战全信息）→ 静默点快照。</summary>
        public void FlushDownlink(GameCore core)
        {            PumpDeckPickCountdown();
            PumpRankedAckWindow();

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
            int turnLeft = TurnSecondsLeft; // v14：同一拍所有座位共用同一剩余秒数
            foreach (var member in AllMembers())
            {
                if (!member.SnapshotDirty) continue;
                bool full = member.MatchSeat < 0;
                var key = (member.MatchSeat, full);
                if (!snapshotCache.TryGetValue(key, out var snapshot))
                {
                    snapshot = NetSnapshotBuilder.Build(core, member.MatchSeat, full);
                    if (snapshot != null) snapshot.TurnSecondsLeft = turnLeft;
                    snapshotCache[key] = snapshot;
                }
                member.SendPayload(NetworkMessageType.GameStateSyncV2, snapshot);
                member.SnapshotDirty = false;
            }
        }

        /// <summary>选卡组倒计时泵（每 tick，FlushDownlink 头部）：整秒变化→广播 RoomState；
        /// 归零→未提交座位替落随机预组（用户定案 2026-10-28：不选=随机卡组）。</summary>
        private void PumpDeckPickCountdown()
        {
            if (_phase != NetRoomPhase.DeckSubmit || !_pickDeadlineUtc.HasValue) return;
            int left = (int)Math.Ceiling((_pickDeadlineUtc.Value - DateTime.UtcNow).TotalSeconds);
            if (left > 0)
            {
                if (left != _lastPickSecond)
                {
                    _lastPickSecond = left;
                    _roomStateDirty = true; // 整秒推送（客户端倒计时显示校准）
                }
                return;
            }

            _pickDeadlineUtc = null;
            for (int seat = 0; seat < 2; seat++)
            {
                if (_chairs[seat] == null || _deckIds[seat] != null) continue;
                var ids = NetPresetDecks.RandomPreset(out var skill);
                if (ids == null)
                {
                    // 预组全缺（数据异常）：不开局，广播错误等手动提交兜底
                    BroadcastError("选卡组超时且随机预组不可用（预组文件缺失）——请手动提交卡组");
                    _roomStateDirty = true;
                    return;
                }
                _deckIds[seat] = ids;
                _skillIds[seat] = SanitizeSkillCardId(skill, ids, seat);
                TideLog.Info($"[NetRoom] 座位 {seat}（{_chairs[seat].Nickname}）选卡组超时——已替落随机预组");
            }
            _roomStateDirty = true;
            if (_deckIds[0] != null && _deckIds[1] != null)
                StartMatch();
        }

        /// <summary>排位接受窗泵（S8；每 tick，FlushDownlink 头部）：双座满 5s 内未收齐双 true →
        /// 广播 Error("排位未接受","Ranked") 并解散——双方各自回 Idle（不自动重排）。</summary>
        private void PumpRankedAckWindow()
        {
            if (_rankedAckDeadlineUtc == null) return;
            if (DateTime.UtcNow < _rankedAckDeadlineUtc.Value) return;
            RejectRanked($"排位未接受（{RankedAckSeconds} 秒内未确认）");
        }

        // ============================================================ 杂项 ============================================================

        private IEnumerable<INetChannel> AllMembers()
        {
            foreach (var chair in _chairs)
                if (chair != null) yield return chair;
            foreach (var spec in _spectators)
                yield return spec;
        }

        private INetChannel SeatConnection(int matchSeat)
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
                    DeckName = conn != null ? (_joinDeckNames[i] ?? "") : "", // S5：进房上报的卡组名（座位卡展示）
                };
            }
            return new MsgRoomState
            {
                RoomId = _roomId,
                Phase = (int)_phase,
                Players = players,
                SpectatorCount = _spectators.Count,
                FirstSeatThisMatch = _firstSeat,
                FirstMoveMode = _settingsFirstMove,
                ShuffleMode = _settingsShuffle,
                GuestReady = _ready[1],
                DeckPickSecondsLeft = _phase == NetRoomPhase.DeckSubmit && _pickDeadlineUtc.HasValue
                    ? Math.Max(0, (int)Math.Ceiling((_pickDeadlineUtc.Value - DateTime.UtcNow).TotalSeconds))
                    : -1,
                Seed = _matchSeed, // v13：开局定死回显（0=未开局）——客户端日志展示/复制复现
            };
        }

        private void SendError(INetChannel conn, string reason, string context)
            => conn.SendPayload(NetworkMessageType.Error, new MsgError { Reason = reason, Context = context });

        private void BroadcastError(string reason, string context = "Room")
        {
            var err = new MsgError { Reason = reason, Context = context };
            foreach (var member in AllMembers())
                member.SendPayload(NetworkMessageType.Error, err);
        }
    }
}
