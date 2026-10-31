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

        private readonly string _roomId;
        private readonly Random _rng;
        private readonly int? _seed;
        private readonly bool _autoStartWhenAllReady; // 无真人房主（无头房主）拓扑：双座满即自动开局（默认模式掷币+随机）

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

        private int _firstSeat = -1;          // 本局先手椅位（未开局 -1）
        private bool _roomStateDirty = true;  // RoomState 待广播
        private bool _gameOver;

        // 就绪层状态（2026-10-28）：模式（0先后手/1洗牌）+ 按椅位准备态 + 选卡组倒计时
        private int _settingsFirstMove;       // 0=掷币 1=房主(椅0)先 2=客人(椅1)先
        private int _settingsShuffle;         // 0=随机洗牌 1=固定牌序
        private readonly bool[] _ready = new bool[2];
        private DateTime? _pickDeadlineUtc;   // 选卡组倒计时截止（DeckSubmit 相位；null=未在倒计时）
        private int _lastPickSecond = -1;     // 上次广播的剩秒（整秒去重）

        // 对局接线（搬 TideHeadlessDriver.ResetCore 骨架）
        private BoardState _board;
        private NetworkTargetSelector _selector;

        /// <summary>seed=null 用时间随机（生产）；钉种子（M3 对拍）时先手椅位与引擎随机流均确定。
        /// autoStartWhenAllReady=无头房主（MatchHost 等）：双座满即开局（跳过准备/房主确认）。</summary>
        public NetRoom(string roomId, int? seed = null, bool autoStartWhenAllReady = false)
        {
            _roomId = roomId ?? "";
            _seed = seed;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _autoStartWhenAllReady = autoStartWhenAllReady;
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

                    case NetworkMessageType.Ping:
                        conn.SendPayload<object>(NetworkMessageType.Pong, null);
                        break;

                    default:
                        // intent 段=100..109 连续段 + 离散位（113 IntentActivateHeroSkill 在会话层消息之后）
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
            _ready[seat] = false; // 新入座者未准备（重进房/换人：准备态不继承）
            _roomStateDirty = true;

            // 2026-10-28 就绪层：真人房主房等待 RoomStart；无头房主房（autoStart）双座满即开局
            if (_autoStartWhenAllReady && _chairs[0] != null && _chairs[1] != null)
                BeginDeckPickPhase();
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
            _settingsFirstMove = s.FirstMoveMode;
            _settingsShuffle = s.ShuffleMode;
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

        /// <summary>进入选卡组相位（房主 RoomStart / autoStart 双座满共用）：先手确定 + 倒计时起算 + 广播。</summary>
        private void BeginDeckPickPhase()
        {
            _firstSeat = _settingsFirstMove == 1 ? 0 : _settingsFirstMove == 2 ? 1 : _rng.Next(2);
            _pickDeadlineUtc = DateTime.UtcNow.AddSeconds(DeckPickSeconds);
            _lastPickSecond = -1;
            _phase = NetRoomPhase.DeckSubmit;
            _roomStateDirty = true;
            TideLog.Info($"[NetRoom] 开局：先手椅位 {_firstSeat}（模式 {_settingsFirstMove}/{_settingsShuffle}），选卡组倒计时 {DeckPickSeconds}s");
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

            if (!NetMatchHandshake.ValidateDeckSubmit(submit, out var reason))
            {
                SendError(conn, reason, "DeckSubmit"); // 拒绝后保持等该座位重提
                return;
            }

            _deckIds[conn.ChairSeat] = submit.CardIds;
            _skillIds[conn.ChairSeat] = SanitizeSkillCardId(submit.SkillCardId, submit.CardIds, conn.ChairSeat);
            _deckTokens[conn.ChairSeat] = submit.TokenCardIds ?? Array.Empty<string>();
            _deckTokenBindings[conn.ChairSeat] = NetMatchHandshake.FromWireBindings(submit.TokenBindings);
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
            var deckFirst = BuildDeck(_deckIds[_firstSeat], _deckTokens[_firstSeat]);
            var deckSecond = BuildDeck(_deckIds[1 - _firstSeat], _deckTokens[1 - _firstSeat]);
            core.InitGame(CardLoader.BuildDeck(deckFirst, 1, _deckTokenBindings[_firstSeat]),
                CardLoader.BuildDeck(deckSecond, 1, _deckTokenBindings[1 - _firstSeat]), _seed,
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
                conn.SendPayload(NetworkMessageType.MatchManifest, NetMatchHandshake.BuildMatchManifest(
                    conn.MatchSeat, _deckIds[chair], _deckIds[1 - chair].Length,
                    _deckTokens[chair], _deckTokenBindings[chair]));
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

        private static List<CardData> BuildDeck(string[] cardIds, string[] tokenCardIds)
        {
            var deck = new List<CardData>(cardIds.Length);
            foreach (var id in cardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card != null) deck.Add(card);
            }
            // P9b 衍生物区模板随组入局（落 Zone.Extra）；缺失 id 记警告跳过（同上口径）
            foreach (var id in tokenCardIds ?? Array.Empty<string>())
            {
                var card = CardCatalog.GetById(id);
                if (card != null) deck.Add(card);
                else TideLog.Warn($"[NetRoom] 衍生物区模板 {id} 不在卡表——已跳过");
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
            PumpDeckPickCountdown();

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
            };
        }

        private void SendError(INetChannel conn, string reason, string context)
            => conn.SendPayload(NetworkMessageType.Error, new MsgError { Reason = reason, Context = context });

        private void BroadcastError(string reason)
        {
            var err = new MsgError { Reason = reason, Context = "Room" };
            foreach (var member in AllMembers())
                member.SendPayload(NetworkMessageType.Error, err);
        }
    }
}
