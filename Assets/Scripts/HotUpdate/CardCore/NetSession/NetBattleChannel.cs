using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using GameBoard;
using SynergyUI;

namespace CardCore.Network
{
    /// <summary>
    /// 对战通道（v17 2026-11-11 房间/对战通道拆分，设计见 网络协议.md §16；原 NetRoom 的对战半边）：
    /// 承载**随对局起停**的一切——卡组提交/开局握手/引擎推进/intent/反问/事件流/快照/战报行流/
    /// 终局摘要/回合计时。房间侧（成员/座位/就绪/房主/房态广播）在 <see cref="NetRoom"/>。
    ///
    /// 边界纪律：
    /// - 本类**不持有成员表**：椅位/观战/房主/阶段全读 <see cref="NetRoom"/>（单向依赖 room→battle 调用，
    ///   battle→room 只读状态与发帧）；对局与房间的生命周期因此解耦——对局结束/作废只迁阶段，
    ///   房间成员不随之解散（房主退出才解散，见 NetRoom.DissolveRoom）。
    /// - 与对局无关的新内容（房聊/房主变化等）**不得**加进本类，走房间通道。
    ///
    /// 线程：全部方法只允许逻辑线程（NetSessionServer.Pump）调用。
    /// </summary>
    public sealed class NetBattleChannel
    {
        /// <summary>选卡组倒计时时长（秒；2026-10-28 用户定案 30s，服务器权威）。</summary>
        public const int DeckPickSeconds = 30;

        /// <summary>每回合可用时间（秒；2026-11-11 用户定案「联机也按真人方式跑」——服务器权威计时，
        /// 到点代打：栈上有待响应者=替其让行，否则替回合方结束回合）。调档只改这一个常量。</summary>
        public const int TurnSeconds = 90;

        /// <summary>超时代打后的宽限（秒）：让行/结束回合后若栈上仍有待响应，下一拍不会立刻再罚。</summary>
        public const int TurnTimeoutGraceSeconds = 10;

        private readonly NetRoom _room;
        private readonly Random _rng;
        private readonly int? _seed;

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
        private int _matchSeed;               // 本局实际种子（BeginDeckPickPhase 定死；0=未开局——RoomState.Seed 回显）
        private bool _gameOver;

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

        // 战报行流（2026-11-11 C 案）：服务端逐视角渲染（MatchLogLineStream 三条：seat0/seat1 观侧
        // 遮蔽 + 观战 -1 中立实名全信息），行历史按座位积累、FlushDownlink 按成员游标切片下发。
        // 订阅必须先于 core.InitGame（InstallLogStreams 注释——晚订漏首回合头行=整局零行）。
        private readonly Dictionary<int, MatchLogLineStream> _logStreams = new Dictionary<int, MatchLogLineStream>();
        private readonly Dictionary<int, List<MatchLogLine>> _logHistory = new Dictionary<int, List<MatchLogLine>>();
        private readonly Dictionary<int, int> _logBase = new Dictionary<int, int>(); // 截头偏移（游标换算：绝对行号-偏移=窗口下标）
        // 终局全量行（二期 B·v18 MatchLogFull）：与遮蔽行同点渲染的双 redact=false 视角流
        //（文案仍按座位观侧——「我方/对方」措辞；遮蔽只差 R1/R2），终局最后一拍按成员一次性下发。
        private readonly Dictionary<int, MatchLogLineStream> _logFullStreams = new Dictionary<int, MatchLogLineStream>();
        private readonly Dictionary<int, List<MatchLogLine>> _logFullHistory = new Dictionary<int, List<MatchLogLine>>();
        private bool _logFullPending;               // 终局已至——下一次 FlushDownlink 末尾发全量行
        private bool _logFullSent;                  // 本局全量行已发（一次性；重开局 StartMatch 复位）
        private Action<MatchLogEntry> _onLogAppended;
        private const int MaxLogLines = 4096;   // 单视角行历史上限（实测长局 ~800 行；截头防御镜像 NetEventProjector.MaxEvents）
        private const int TrimLogBatch = 256;   // 分批截头（均摊 O(1)——逐条 RemoveAt(0) 是 O(n)/行）

        /// <summary>seed=null 用时间随机（生产）；钉种子（M3 对拍）时先手椅位与引擎随机流均确定。</summary>
        public NetBattleChannel(NetRoom room, int? seed = null)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _seed = seed;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>本局先手椅位（未开局 -1；房间通道 RoomState.FirstSeatThisMatch 回显）。</summary>
        public int FirstSeat => _firstSeat;

        /// <summary>本局实际种子（0=未开局；房间通道 RoomState.Seed 回显）。</summary>
        public int MatchSeed => _matchSeed;

        /// <summary>选卡组倒计时剩秒（-1=非倒计时；房间通道 BuildRoomState 取用）。</summary>
        public int DeckPickSecondsLeft
        {
            get
            {
                if (_room.Phase != NetRoomPhase.DeckSubmit || !_pickDeadlineUtc.HasValue) return -1;
                return Math.Max(0, (int)Math.Ceiling((_pickDeadlineUtc.Value - DateTime.UtcNow).TotalSeconds));
            }
        }

        /// <summary>本回合剩余秒数（快照 TurnSecondsLeft 取值；负数=无计时）。</summary>
        public int TurnSecondsLeft
        {
            get
            {
                if (_room.Phase != NetRoomPhase.Playing || _gameOver || _turnDeadlineUtc == null) return -1;
                double left = (_turnDeadlineUtc.Value - DateTime.UtcNow).TotalSeconds;
                return left <= 0 ? 0 : (int)Math.Ceiling(left);
            }
        }

        // ============================================================ 上行分派（对战通道） ============================================================

        /// <summary>处理一条对战通道上行（房间通道已按 NetworkChannels 归类后转来；逻辑线程）。</summary>
        public void HandleMessage(GameCore core, INetChannel conn, NetworkMessage msg)
        {
            switch (msg.Type)
            {
                case NetworkMessageType.DeckSubmit:
                    HandleDeckSubmit(conn, msg);
                    break;

                case NetworkMessageType.SelectResponse:
                    HandleSelectResponse(conn, msg);
                    break;

                case NetworkMessageType.TextResponse:
                    HandleTextResponse(conn, msg);
                    break;

                default:
                    if (NetworkChannels.IsIntent(msg.Type))
                        HandleIntent(core, conn, msg);
                    else
                        _room.SendError(conn, $"消息类型 {msg.Type} 不是对战通道可处理的上行", msg.Type.ToString(),
                            NetworkChannel.Battle);
                    break;
            }
        }

        private void HandleDeckSubmit(INetChannel conn, NetworkMessage msg)
        {
            if (conn.ChairSeat != 0 && conn.ChairSeat != 1)
            { _room.SendError(conn, "非玩家座位不能提交卡组", "DeckSubmit", NetworkChannel.Battle); return; }
            if (_room.Phase == NetRoomPhase.Waiting)
            { _room.SendError(conn, "等双方就座后再提交卡组", "DeckSubmit", NetworkChannel.Battle); return; }
            if (_room.Phase != NetRoomPhase.DeckSubmit)
            { _room.SendError(conn, "当前阶段不接受卡组提交", "DeckSubmit", NetworkChannel.Battle); return; }
            if (_deckIds[conn.ChairSeat] != null)
            { _room.SendError(conn, "本座位已提交过卡组", "DeckSubmit", NetworkChannel.Battle); return; }

            var submit = NetworkSerializer.DeserializePayload<MsgDeckSubmit>(msg);
            if (submit == null) { _room.SendError(conn, "DeckSubmit 载荷损坏", "DeckSubmit", NetworkChannel.Battle); return; }

            // v11 自建内容跨端：先装瞬态覆盖层再校验——提交方随卡组携带非基础池卡/效果/词条的
            // 完整内容，校验/摘要按「本地表+覆盖层」计算（此前对端缺卡恒拒局，自建卡无法跨玩家
            // 对战）。被拒提交的残留载荷无害（add-if-missing 不覆盖本地、不落盘，Teardown 统一清）。
            TransientNetContent.Install(submit.AddedCards, submit.AddedEffects, submit.AddedRows);

            if (!NetMatchHandshake.ValidateDeckSubmit(submit, out var reason))
            {
                _room.SendError(conn, reason, "DeckSubmit", NetworkChannel.Battle); // 拒绝后保持等该座位重提
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
            if (_room.Phase != NetRoomPhase.Playing || _selector == null) return;
            var resp = NetworkSerializer.DeserializePayload<MsgSelectResponse>(msg);
            if (resp == null) return;
            if (_selector.TryComplete(resp)) return;

            // 迟到/未知 RequestId：静默忽略之外再给一次可诊断回执（引擎侧已按空应答/超时兜底）
            _room.SendError(conn, $"反问应答未知 RequestId={resp.RequestId}（迟到或已作废）", "SelectResponse",
                NetworkChannel.Battle);
        }

        /// <summary>文本反问应答（2026-10-31 阶段C）：座位+长度校验在 TryCompleteText 内
        ///（不合法按取消 null 收口——效果不结算、费用不退）；迟到/未知 RequestId 给可诊断回执。</summary>
        private void HandleTextResponse(INetChannel conn, NetworkMessage msg)
        {
            if (_room.Phase != NetRoomPhase.Playing || _selector == null) return;
            var resp = NetworkSerializer.DeserializePayload<MsgTextResponse>(msg);
            if (resp == null) return;
            if (_selector.TryCompleteText(resp, conn.MatchSeat)) return;

            _room.SendError(conn, $"文本反问应答未知 RequestId={resp.RequestId}（迟到或已作废）", "TextResponse",
                NetworkChannel.Battle);
        }

        private void HandleIntent(GameCore core, INetChannel conn, NetworkMessage msg)
        {
            if (_room.Phase != NetRoomPhase.Playing)
            { _room.SendError(conn, "对局未在进行中", msg.Type.ToString(), NetworkChannel.Battle); return; }
            if (conn.MatchSeat < 0)
            { _room.SendError(conn, "观战连接不能发 intent", msg.Type.ToString(), NetworkChannel.Battle); return; }

            if (!NetworkIntentApplier.Apply(core, conn.MatchSeat, msg, out var error, _board)) // v19：传宿主棋盘（落格校验+钉格）
            {
                _room.SendError(conn, error, msg.Type.ToString(), NetworkChannel.Battle); // §5 拒绝语义：Error 帧回发（M2 落地）
                return;
            }
            _room.MarkSnapshotDirty();
        }

        // ============================================================ 开局 / 终局 ============================================================

        /// <summary>进入选卡组相位（房间通道 RoomStart/autoStart/排位双接受共用）：先手确定 + 倒计时起算 +
        /// 阶段迁移 + 广播。v13：本局种子在此定死（房主钉值 ?? ctor 种子 ?? 随机）——此后设置冻结，
        /// RoomState.Seed 回显。</summary>
        internal void BeginDeckPickPhase(int firstMoveMode, int shuffleMode, int pinnedSeed)
        {
            if (_room.Phase != NetRoomPhase.Waiting) return;
            _firstSeat = firstMoveMode == 1 ? 0 : firstMoveMode == 2 ? 1 : _rng.Next(2);
            _matchSeed = pinnedSeed != 0 ? pinnedSeed : (_seed ?? _rng.Next());
            _pickDeadlineUtc = DateTime.UtcNow.AddSeconds(DeckPickSeconds);
            _lastPickSecond = -1;
            _room.EnterPhase(NetRoomPhase.DeckSubmit);
            TideLog.Info($"[NetRoom] 开局：先手椅位 {_firstSeat}（模式 {firstMoveMode}/{shuffleMode}），"
                + $"种子 {_matchSeed}{(pinnedSeed != 0 ? "（房主钉）" : "")}，选卡组倒计时 {DeckPickSeconds}s");
        }

        /// <summary>双座位卡组就绪 → 开局。换先手 = 谁的卡组进 Player1（引擎座位 0 恒先手）。
        /// 先手椅位已在 BeginDeckPickPhase 按模式确定（2026-10-28 前移）；洗牌模式=固定牌序时锁卡组序
        /// （房间通道的 RoomSettings 付在本局开局参数里，本类不存设置）。</summary>
        private void StartMatch()
        {
            var core = GameCore.Instance;
            bool lockDeckOrder = _room.ShuffleModeLocked; // 房间通道设置镜像（RoomSettings：0随机/1固定）

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
                _room.BroadcastError($"开局中止：卡组引用的 {total} 张卡不在本端卡表（预组断链或内容漂移）——请重新提交卡组",
                    "Room", NetworkChannel.Battle);
                if (missingFirst.Count > 0) ResetSeatDeck(_firstSeat);
                if (missingSecond.Count > 0) ResetSeatDeck(1 - _firstSeat);
                _room.MarkRoomStateDirty();
                return;
            }
            // 战报行流安装（2026-11-11 C 案）：必须先于 InitGame——订阅赶在 Reset→StartGame 的
            // 首发TurnStartEvent 之前，否则行流 armed 永假=整局零行（见 InstallLogStreams 注释）。
            InstallLogStreams();
            core.InitGame(CardLoader.BuildDeck(deckFirst, 1, _deckTokenBindings[_firstSeat]),
                CardLoader.BuildDeck(deckSecond, 1, _deckTokenBindings[1 - _firstSeat]), _matchSeed,
                lockDeckOrder: lockDeckOrder,
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
                    var target = _room.SeatConnection(request.ChooserSeat);
                    target?.SendPayload(NetworkMessageType.SelectRequest, request);
                    return Cysharp.Threading.Tasks.UniTask.CompletedTask;
                },
                SendTextAsync = request =>
                {
                    var target = _room.SeatConnection(request.ChooserSeat);
                    target?.SendPayload(NetworkMessageType.TextRequest, request);
                    return Cysharp.Threading.Tasks.UniTask.CompletedTask;
                },
            };
            _selector.Attach();

            // 座位映射（椅位 → 引擎座位）+ 事件/行流游标对齐（InitGame 已 ClearAll；行流历史由
            // InstallLogStreams 清）。EventFeed（二期 B·v18）：AI 填位=Full（NetClientBrain 观察）；
            // 人类座位/观战=FxOnly（演出白名单事件子集——日志由行流承担）。
            foreach (var chair in _room.Chairs.Where(c => c != null))
            {
                chair.MatchSeat = chair.ChairSeat == _firstSeat ? 0 : 1;
                chair.EventCursor = 0;
                chair.LogCursor = 0;
                chair.EventFeed = chair.IsAi ? EventFeed.Full : EventFeed.FxOnly;
                chair.SnapshotDirty = true;
            }
            foreach (var spec in _room.Spectators)
            {
                spec.MatchSeat = -1;
                spec.EventCursor = 0; // 先于开局的观战者从头看整局（InitGame 的 ClearAll 已清缓冲）
                spec.LogCursor = 0;
                spec.EventFeed = EventFeed.FxOnly; // 观战演出数据前提（二期 C NetFilmSource 消费）
                spec.SnapshotDirty = true;
            }

            // 对局清单：各座位视角各发一份（OwnSeat=引擎座位权威；摘要按该座位卡组计算——含 P9b TokenSig）
            for (int chair = 0; chair < 2; chair++)
            {
                var conn = _room.Chairs[chair];
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
            _logFullPending = false; // 二期 B：全量行终局一次性下发标记复位
            _logFullSent = false;
            _room.EnterPhase(NetRoomPhase.Playing);
            // v14：回合计时起钟（本局第一拍由 PumpTurnClock 按回合号/回合方重置；此处先给满时长，
            // 保证 StartMatch 当拍下发的首快照就带 TurnSecondsLeft）
            _turnDeadlineUtc = DateTime.UtcNow.AddSeconds(TurnSeconds);
            _clockTurn = -1;
            _clockActiveSeat = -1;
            _room.MarkRoomStateDirty();
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnGameOver(GameOverEvent e)
        {
            if (_room.Phase != NetRoomPhase.Playing) return;
            _gameOver = true;
            _logFullPending = true; // 二期 B·v18：终局最后一拍发全量无遮蔽行（MatchLogFull）
            _room.EnterPhase(NetRoomPhase.Finished);
            _room.MarkSnapshotDirty(); // 终局快照：静默判定对 Finished 恒真
            SendMatchSummaries(e); // v11 对局记录：终局摘要双座位各发一份
        }

        /// <summary>终局摘要（2026-11-09 协议 v11 对局记录）：双座位各收一份，视角=收件人的对手——
        /// 对手完整卡组（对局已结束，局内「卡组构成隐藏」不再适用）+ 自建内容载荷 + 收件人胜负 +
        /// 战报全文（房主侧引擎事件流渲染——客人端无本地引擎，战报只能随摘要携带）。AI 填位局
        /// 不下发（对手内容=本机预组，无记录价值）；替落预组座位（_submits 空）照发——卡组数据
        /// 在 _deckIds 系列恒有。客户端收到后落 EncounterRecord 随云存档同步。</summary>
        private void SendMatchSummaries(GameOverEvent e)
        {
            if (_room.Chairs.Any(c => c == null || c.IsAi)) return;
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
                bool win = winnerSeat >= 0 && winnerSeat == _room.Chairs[chair].MatchSeat;
                _room.Chairs[chair].SendPayload(NetworkMessageType.MatchSummary, new MsgMatchSummary
                {
                    FinishedAtUnix = finishedAt,
                    OpponentName = _room.Chairs[opp].Nickname,
                    OpponentOpenId = _room.Chairs[opp].OpenId ?? "",
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
                    Ranked = _room.Ranked, // S8：客户端只对排位局按卡组内容指纹记账（DeckRatingStore）
                });
                TideLog.Info($"[NetRoom] 终局摘要已下发 → 座位 {chair}（对手 {_room.Chairs[opp].Nickname}，{(win ? "胜" : "负")}）");
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
            TeardownLogStreams(); // 战报行流退订+清史（2026-11-11 C 案：漏退订=跨局串行）
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

        /// <summary>战报行流安装（2026-11-11 C 案）：**必须在 core.InitGame 之前调用**——InitGame 内含
        /// Reset→MatchLogService.ClearAll→洗牌→起手→StartGame（GameCore.Reset 接线 EnsureStarted+ClearAll，
        /// StartGame 紧接着发第一发 TurnStartEvent）。订阅晚于它 ⇒ 行流 armed 永 false ⇒ 整局零行。
        /// 三视角：seat0/seat1 观侧（isSelf=引擎座位归属，R1/R2 遮蔽生效）、观战 -1 中立实名全信息
        ///（isSelf=null，与观战快照口径一致）。线程：Appended 在引擎发布线程同步触发，与 Pump/FlushDownlink
        /// 同逻辑线程，无锁（沿用 NetEventProjector 纪律）。</summary>
        private void InstallLogStreams()
        {
            TeardownLogStreams(); // 幂等重装（StartMatch 可重入）
            foreach (int seat in new[] { 0, 1, -1 })
            {
                _logStreams[seat] = new MatchLogLineStream(
                    seat < 0 ? null : p => NetEntityMapper.SeatOf(p) == seat, // 引擎座位（Player1=0），非椅位
                    redactEffectSummary: seat >= 0,                           // R2：座位流只出效果短名（观战全信息出 Description）
                    redactHidden: seat >= 0,                                  // R1（E3）：座位流遮对手隐藏区入手；观战 isSelf=null 恒不遮
                    richValues: true);                                        // D7（2026-11-13 P4）：座位/观战行流上面板——数值染绿/红
                _logHistory[seat] = new List<MatchLogLine>(1024);
                _logBase[seat] = 0;
                // 二期 B·v18 全量行：同视角观侧文案、双 redact=false（终局 MatchLogFull 下发用）
                // richValues=true（2026-11-13 P4·D7）：FullText 回填复盘剧本——复盘行同染（与座位/观战流一致；
                // 长期存档走 MsgMatchSummary.Log=纯文本通道，不经此流）
                _logFullStreams[seat] = new MatchLogLineStream(
                    seat < 0 ? null : p => NetEntityMapper.SeatOf(p) == seat,
                    redactEffectSummary: false, redactHidden: false,
                    richValues: true);
                _logFullHistory[seat] = new List<MatchLogLine>(1024);
            }
            _onLogAppended = entry =>
            {
                foreach (var kv in _logStreams)
                {
                    var history = _logHistory[kv.Key];
                    var fullHistory = _logFullHistory[kv.Key];
                    try
                    {
                        kv.Value.Append(entry, history);
                        _logFullStreams[kv.Key].Append(entry, fullHistory);
                    }
                    catch (Exception ex) // 渲染异常不中断对局（与 NetEventProjector.ErrorCount 同风格）
                    {
                        TideLog.Error($"[NetRoom] 战报行流渲染异常（seat={kv.Key} {entry.EventType}）：{ex.Message}");
                        continue;
                    }
                    if (history.Count > MaxLogLines)
                    {
                        int drop = Math.Min(history.Count - MaxLogLines + TrimLogBatch, history.Count);
                        history.RemoveRange(0, drop);
                        _logBase[kv.Key] += drop;
                    }
                    if (fullHistory.Count > MaxLogLines) // 全量史同步截头（终局一次性下发——超长防御）
                        fullHistory.RemoveRange(0, fullHistory.Count - MaxLogLines);
                }
            };
            MatchLogService.Appended += _onLogAppended;
        }

        /// <summary>行流退订（TeardownMatch 调；漏掉会跨局串行——下一局开局前事件被旧流吃掉）。
        /// 只停积累（退订+清流）：行历史留档供终局后读取（V11 对拍/排查），下次 InstallLogStreams 清。</summary>
        private void TeardownLogStreams()
        {
            if (_onLogAppended != null)
            {
                MatchLogService.Appended -= _onLogAppended;
                _onLogAppended = null;
            }
            _logStreams.Clear();
            _logFullStreams.Clear();
        }

        /// <summary>验证口（V11 对拍）：读当前行流历史快照（seat=0/1/-1；未安装返回 null）。</summary>
        internal List<MatchLogLine> LogHistorySnapshot(int seat)
            => _logHistory.TryGetValue(seat, out var h) ? new List<MatchLogLine>(h) : null;

        /// <summary>验证口（V11.f）：读当前全量行历史快照（v18 MatchLogFull 下发源；未安装返回 null）。</summary>
        internal List<MatchLogLine> LogFullHistorySnapshot(int seat)
            => _logFullHistory.TryGetValue(seat, out var h) ? new List<MatchLogLine>(h) : null;

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

        /// <summary>选卡组相位缺员退回：两座提交作废 + 先手/倒计时作废（阶段由房间通道回 Waiting）。</summary>
        internal void ResetDecks()
        {
            ResetSeatDeck(0);
            ResetSeatDeck(1);
            _pickDeadlineUtc = null;
            _firstSeat = -1;
        }

        /// <summary>对局作废（对局中断线）：作废全部未决反问防泵悬死，广播 Error + 阶段 Finished——
        /// 不判胜负。房间成员不随之解散（房主退出才解散，见 NetRoom.DissolveRoom）。</summary>
        internal void VoidMatch(string reason)
        {
            _selector?.AbortPending();
            _room.EnterPhase(NetRoomPhase.Finished);
            _room.BroadcastError(reason, "Room", NetworkChannel.Battle);
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
            if (_room.Phase != NetRoomPhase.Playing || _gameOver || core == null) return;
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
            _room.MarkSnapshotDirty(); // 剩余秒数随快照回零
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
                _room.BroadcastError(reason, "TurnTimeout", NetworkChannel.Battle); // 双端可见（客户端以提示口径显示）
            }
            else
            {
                TideLog.Warn($"[NetRoom] 超时代打被拒（座位 {seat} / {type}）：{error}——{reason}");
            }
        }

        /// <summary>对战通道下行出队（房间态广播由房间通道先发）：事件流（AI 填位）→ 战报行流 →
        /// 静默点快照。选卡组倒计时由房间通道在房态广播前泵（<see cref="PumpDeckPickCountdown"/>）。
        /// 房间/对战分道后，本方法只管随对局起停的帧。</summary>
        public void FlushDownlink(GameCore core)
        {
            if (_room.Phase != NetRoomPhase.Playing && _room.Phase != NetRoomPhase.Finished) return;

            // ---- 事件流：全局投影缓冲按游标切片，逐成员按视角过滤（观战直通）。
            // 二期 B（v18 EventFeed）：AI 填位=Full（NetClientBrain 观察，隐藏过滤照旧）；
            // 人类座位/观战=FxOnly（演出白名单子集——NetFxEventWhitelist 单源，逐条过滤）；
            // None 不发。日志与终局文本由行流/MatchLogFull 承担。----
            var events = NetEventProjector.Events;
            int total = events.Count;
            var hiddenBySeat = new Dictionary<int, HashSet<uint>>();
            bool sentAny = false;
            foreach (var member in _room.AllMembers())
            {
                if (member.EventFeed == EventFeed.None) continue;
                if (member.EventCursor >= total) continue;
                var fxOnly = member.EventFeed == EventFeed.FxOnly;
                HashSet<uint> hidden = null;
                if (!fxOnly && member.MatchSeat >= 0)
                {
                    if (!hiddenBySeat.TryGetValue(member.MatchSeat, out hidden))
                    {
                        hidden = NetEventSeatFilter.HiddenCardIds(core, member.MatchSeat);
                        hiddenBySeat[member.MatchSeat] = hidden;
                    }
                }

                var chunk = new List<NetEvent>(64);
                for (int offset = member.EventCursor; offset < total; offset += 64)
                {
                    chunk.Clear();
                    for (int i = 0; i < 64 && offset + i < total; i++)
                    {
                        var e = events[offset + i];
                        if (fxOnly && !NetFxEventWhitelist.Allows(e.EventType)) continue; // 演出白名单
                        chunk.Add(!fxOnly ? NetEventSeatFilter.Filter(e, hidden) : e);
                    }
                    if (chunk.Count > 0)
                        member.SendPayload(NetworkMessageType.NetEventBatch, new MsgNetEventBatch { Events = chunk.ToArray() });
                }
                member.EventCursor = total;
                sentAny = true;
            }
            if (sentAny) _room.MarkSnapshotDirty();

            // ---- 战报行流（2026-11-11 C 案 v15）：每座位一条渲染历史，成员按 LogCursor 切片 64 行/批。
            // 服务端渲染好的最终行（观侧遮蔽/时序旗标已定），客户端纯 append；TCP 有序 ⇒ 无需序号。
            // 观战取 -1 流（中立实名全信息）；截头时游标按绝对行号换算（- base 偏移=窗口下标）。
            // 发过行 ⇒ MarkSnapshotDirty（接棒事件流段的置脏语义——人类停发事件后，对手行动引发的
            // 快照节奏由行流段驱动，否则纯真人房快照停摆）。----
            bool sentAnyLines = false;
            foreach (var member in _room.AllMembers())
            {
                int seat = member.MatchSeat < 0 ? -1 : member.MatchSeat;
                if (!_logHistory.TryGetValue(seat, out var history) || history.Count == 0) continue;
                int base0 = _logBase.TryGetValue(seat, out var b) ? b : 0;
                int total0 = base0 + history.Count; // 绝对行数
                if (member.LogCursor >= total0) continue;
                int start = Math.Max(member.LogCursor - base0, 0); // 游标→窗口下标（截头只影响已读过段）
                for (int offset = start; offset < history.Count; offset += 64)
                {
                    int take = Math.Min(64, history.Count - offset);
                    var chunk = new NetLogLine[take];
                    for (int i = 0; i < take; i++)
                    {
                        var line = history[offset + i];
                        chunk[i] = new NetLogLine
                        {
                            Kind = line.Kind,
                            Flags = line.Flags,
                            EventId = line.EventId,
                            Text = line.Text,
                        };
                    }
                    member.SendPayload(NetworkMessageType.NetLogBatch, new MsgNetLogBatch { Lines = chunk });
                }
                member.LogCursor = total0;
                sentAnyLines = true;
            }
            if (sentAnyLines) _room.MarkSnapshotDirty();

            // ---- 快照：稳定决策点取样（§7 时点约束收口：不在结算中 + 无未决反问——
            // 栈非空但优先权等待是稳定决策点，快照含 StackV2/PrioritySeat 正是客户端的让行依据）----
            bool stable = _room.Phase == NetRoomPhase.Finished
                || (core != null && core.StackEngine != null
                    && !core.StackEngine.IsResolving
                    && (_selector == null || !_selector.HasPending));
            if (!stable) return;

            var snapshotCache = new Dictionary<(int seat, bool full), MsgGameStateSync>();
            int turnLeft = TurnSecondsLeft; // v14：同一拍所有座位共用同一剩余秒数
            foreach (var member in _room.AllMembers())
            {
                if (!member.SnapshotDirty) continue;
                bool full = member.MatchSeat < 0;
                var key = (member.MatchSeat, full);
                if (!snapshotCache.TryGetValue(key, out var snapshot))
                {
                    snapshot = NetSnapshotBuilder.Build(core, member.MatchSeat, full, _board); // v19：快照携带盘面绝对坐标
                    if (snapshot != null) snapshot.TurnSecondsLeft = turnLeft;
                    snapshotCache[key] = snapshot;
                }
                member.SendPayload(NetworkMessageType.GameStateSyncV2, snapshot);
                member.SnapshotDirty = false;
            }

            // ---- 终局全量行（二期 B·v18 MatchLogFull）：GameOver 置 pending，最后一拍事件/行/快照
            // 之后按成员视角一次性下发（64 行/批分帧）。AI 填位局照发（本地回环客户端按需忽略）；
            // 观战发 -1 全量。行文本=双 redact=false 的同座位观侧全量版（客户端按 EventId 回填）。----
            if (_logFullPending && !_logFullSent && _room.Phase == NetRoomPhase.Finished)
            {
                _logFullSent = true;
                foreach (var member in _room.AllMembers())
                {
                    int seat = member.MatchSeat < 0 ? -1 : member.MatchSeat;
                    if (!_logFullHistory.TryGetValue(seat, out var full) || full.Count == 0) continue;
                    for (int offset = 0; offset < full.Count; offset += 64)
                    {
                        int take = Math.Min(64, full.Count - offset);
                        var chunk = new NetLogLine[take];
                        for (int i = 0; i < take; i++)
                        {
                            var line = full[offset + i];
                            chunk[i] = new NetLogLine
                            {
                                Kind = line.Kind,
                                Flags = line.Flags,
                                EventId = line.EventId,
                                Text = line.Text,
                            };
                        }
                        member.SendPayload(NetworkMessageType.MatchLogFull, new MsgNetLogBatch { Lines = chunk });
                    }
                    TideLog.Info($"[NetRoom] 终局全量行已下发 → {(seat < 0 ? "观战" : "座位 " + seat)}（{full.Count} 行）");
                }
            }
        }

        /// <summary>选卡组倒计时泵（房间通道每 tick 在房态广播前调）：整秒变化→置房态脏（房间通道广播）；
        /// 归零→未提交座位替落随机预组（用户定案 2026-10-28：不选=随机卡组）。</summary>
        internal void PumpDeckPickCountdown()
        {
            if (_room.Phase != NetRoomPhase.DeckSubmit || !_pickDeadlineUtc.HasValue) return;
            int left = (int)Math.Ceiling((_pickDeadlineUtc.Value - DateTime.UtcNow).TotalSeconds);
            if (left > 0)
            {
                if (left != _lastPickSecond)
                {
                    _lastPickSecond = left;
                    _room.MarkRoomStateDirty(); // 整秒推送（客户端倒计时显示校准）
                }
                return;
            }

            _pickDeadlineUtc = null;
            for (int seat = 0; seat < 2; seat++)
            {
                var conn = _room.Chairs[seat];
                if (conn == null || _deckIds[seat] != null) continue;
                var ids = NetPresetDecks.RandomPreset(out var skill);
                if (ids == null)
                {
                    // 预组全缺（数据异常）：不开局，广播错误等手动提交兜底
                    _room.BroadcastError("选卡组超时且随机预组不可用（预组文件缺失）——请手动提交卡组",
                        "Room", NetworkChannel.Battle);
                    _room.MarkRoomStateDirty();
                    return;
                }
                _deckIds[seat] = ids;
                _skillIds[seat] = SanitizeSkillCardId(skill, ids, seat);
                TideLog.Info($"[NetRoom] 座位 {seat}（{conn.Nickname}）选卡组超时——已替落随机预组");
            }
            _room.MarkRoomStateDirty();
            if (_deckIds[0] != null && _deckIds[1] != null)
                StartMatch();
        }
    }
}
