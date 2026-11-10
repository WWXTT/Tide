using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using CardCore;
using CardCore.Network;
using Cysharp.Threading.Tasks;
using TMPro;
using Tide.HotUpdate;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>进房参数聚合（S8）：S6b 的 password/deckName + 排位三件——
    /// AdoptClientAndJoin/ConnectAsync/ConnectLoopback 一路透传（AOT 安全普通类，替代参数续膨胀）。</summary>
    public sealed class JoinOptions
    {
        public string Password;
        public string DeckName;
        public bool Ranked;
        public string DeckFingerprint;
        /// <summary>胜率千分比（-1=无战绩）。</summary>
        public int WinRate = -1;
    }

    /// <summary>
    /// 联机对战屏（2026-10-21 P2P 复活；S6a 起绑定三栏 v2 预制体——视觉真源=Assets/Art/Prefab/
    /// LobbyUI.prefab，DSH 2026-11-10 搭建；Build 只做深度按名绑定+接线）：
    /// 左栏=自身信息+可用卡组池（预组不显示、无卡组禁建房/加入）；中栏=页签（普通=建房+房间列表
    /// /排位）+房间页（建房/进房驱动）；右栏=对战历史（S6c 接入）。
    /// 房主=本进程内 NetSessionServer（UIBootstrap 泵驱动），客人 NetGameClient 直连——
    /// 房主自身走回环客户端入座（127.0.0.1——与 AI 填位同款先例，与客人同一客户端代码路径）。
    /// S6a 范围：绑定 v2 节点+页签+自身信息+卡组池；网络/超时/座位/解散逻辑不动
    ///（房间列表双源/房名密码/历史= S6b/S6c）。
    /// </summary>
    public sealed class LobbyScreen : UIScreen
    {
        protected override string RootName => "lobby-screen";
        protected override string PrefabName => "LobbyUI"; // S4b：预制体真源（框架实例化为 Root）

        private static string _lastNickname = "";

        // ---- v2 三栏面板（S6a）：中栏三页=panel-normal/panel-ranked/panel-room（页签+会话驱动）----
        private RectTransform _panelNormal, _panelRanked, _panelRoom, _panelTap;

        private TMP_Text _statusLabel, _modesLabel, _modeFirstLabel, _modeShuffleLabel;
        private TMP_Text _roomHintLabel; // S1：房间面板常驻操作引导（RefreshRoomUi 按角色/准备态填）
        private TMP_Text _roomTitleLabel; // S6b：房间页标题（「局域网房间」/「TapTap 房间」）
        private TMP_Text _addressLabel;   // S6b：房间页地址/来源行
        private UnityEngine.UI.Button _startBtn, _readyBtn, _modeFirstBtn, _modeShuffleBtn, _quitBtn;
        private RectTransform _addAiNode;

        // ---- 左栏（S6a）：自身信息 + 卡组池 ----
        private TMP_Text _selfNickLabel, _selfStateLabel, _selfHintLabel;
        private RectTransform _deckList; // list-decks content（动态行挂它）
        private TMP_Text _deckEmptyLabel;
        private List<DeckData> _decks = new List<DeckData>();
        private readonly List<UnityEngine.UI.Image> _deckRowBgs = new List<UnityEngine.UI.Image>();
        private static string _lastSelectedDeckName = ""; // 重进屏保持选择（S6a 定案）

        /// <summary>卡组池当前选中（S6a：无卡组禁建房/加入的判定源；S7 将随 JoinRoom 上报）。
        /// 静态记住名字，重进屏恢复。</summary>
        public DeckData SelectedDeck { get; private set; }

        /// <summary>选中卡组的内容指纹（S8：排位匹配/记账键——随选中刷新；无选中为空）。</summary>
        private string _selectedFingerprint = "";

        // ---- 中栏（S6a）：页签 + 普通（建房+房间列表）/排位 ----
        private UnityEngine.UI.Button _tabNormalBtn, _tabRankedBtn, _createRoomBtn, _refreshBtn, _rankedStartBtn;
        private TMP_InputField _roomNameInput, _roomPwdInput;
        private TMP_Text _rankedDeckLabel, _rankedRateLabel, _rankedStateLabel;

        // ---- 右栏（S6c 对战历史）----
        private UnityEngine.UI.Button _copyDeckBtn, _replayBtn;
        private RectTransform _historyList; // list-history content（历史动态行挂它）
        private EncounterRecord _selectedRecord; // 选中行（btn-copy-deck 的判定源）
        private readonly List<EncounterRecord> _historyRecords = new List<EncounterRecord>(); // 行序对齐（选中底色按引用对账）
        private readonly List<UnityEngine.UI.Image> _historyRowBgs = new List<UnityEngine.UI.Image>();

        // S2 双座位卡（seat-0/seat-1）：昵称/身份 tag/在线点/准备态四件按椅位索引存取
        private readonly TMP_Text[] _seatNickLabels = new TMP_Text[2];
        private readonly TMP_Text[] _seatTagLabels = new TMP_Text[2];
        private readonly UnityEngine.UI.Image[] _seatDots = new UnityEngine.UI.Image[2];
        private readonly TMP_Text[] _seatReadyLabels = new TMP_Text[2];

        // TapTap 联机段（2026-10-24 预编译隔离迁 Tide.TapTap 程序集）：v2 三栏无独立 TapTap 面板
        //（S6b 房间列表双源统一）——段对象仍要（登录态/OpenId 经它取），不再调段 Build。
        // 本地单机风味 Module=null。
        private ITapLobbySection _tapSection;

        /// <summary>TapTap 段（S8：RankedMatchmaking 驱动登录态/候选拉取/建房；可能为 null=单机风味）。</summary>
        internal ITapLobbySection TapSection => _tapSection;

        private NetGameClient _client;
        private NetSessionServer _server; // 房主侧（自建自管，OnExit/离开时停机）
        private NetAiFiller _aiFiller;
        private bool _isHost;
        private bool _handedOver; // 已移交战斗屏：OnExit 不拆连接
        private MsgRoomState _roomState;

        // 就绪层模式镜像（2026-10-28：0先后手=掷币/1房主先/2客先；0洗牌=随机/1固定）——
        // 房主循环钮本地换档即发 RoomSettings，服务端 RoomState 回显为权威展示源
        private int _modeFirst;
        private int _modeShuffle;

        // 局域网自动发现（2026-10-28 加入流改造）：房主信标 + 客人扫描，唯一房间自动直入
        private LanRoomBeacon _beacon;
        private volatile string _beaconPayload = "";
        private string _lanRoomName = "";  // S6b：局域网房名快照（信标 payload ver2 带出；建房时记录）
        private bool _lanRoomHasPassword;  // S6b：密码标志（信标第 9 段；密码本体绝不上广播）
        private LanRoomScanner _scanner;
        private bool _scanLoopRunning;
        private bool _autoJoinArmed;
        private string _scanSignature;
        private TMP_Text _scanLabel; // v2：扫描/房间计数状态行=lbl-rooms-count
        private RectTransform _roomList; // v2：list-rooms content（双源动态行挂它：LAN room-{i} / TapTap tap-room-{i}）
        private bool _tapListBusy;    // S6b：TapTap 列表拉取闸（防重入）
        private bool _roomsFromTap;   // S6b：当前列表源（true=TapTap / false=局域网）——切源丢弃迟到结果

        // S6a 预制体绑定 v2：视觉真源=Assets/Art/Prefab/LobbyUI.prefab 三栏版（DSH 2026-11-10）。
        // Build 只做深度按名绑定+闭包接线；缺节点 Find/BindButton LogError 暴露断线。
        // 动态行（房间 room-{i}/卡组 deck-{i}）仍运行期创建挂各自 content。
        protected override void Build()
        {
            // ---- 中栏三页（普通/排位由页签切；房间页由建房/进房驱动）----
            _panelNormal = Find("panel-normal");
            _panelRanked = Find("panel-ranked");
            _panelRoom = Find("panel-room");

            // ---- 常驻状态行（Root 下——任何换页可见，SetStatus/SetError 切色）----
            _statusLabel = FindText("lbl-status");

            // ---- 左栏：自身信息 + 卡组池（panel-left/panel-self/panel-decks 恒显示，无换页逻辑）----
            Find("panel-left"); // 存在性诊断（漏搭即 LogError）
            Find("panel-self");
            _selfNickLabel = FindText("lbl-self-nick");
            _selfStateLabel = FindText("lbl-self-state");
            _selfHintLabel = FindText("lbl-self-hint");
            var deckScroll = FindScroll("list-decks");
            _deckList = deckScroll != null ? deckScroll.Content : null;
            _deckEmptyLabel = FindText("lbl-deck-empty");

            // ---- 中栏：页签 + 普通页（建房+房间列表）----
            _tabNormalBtn = BindButton("btn-tab-normal", () => ShowCenterTab(ranked: false));
            _tabRankedBtn = BindButton("btn-tab-ranked", () => ShowCenterTab(ranked: true));
            _roomNameInput = FindInput("input-room-name"); // S6b 接建房（房名+密码）；本轮先绑
            _roomPwdInput = FindInput("input-room-pwd");
            _createRoomBtn = BindButton("btn-create-room", StartHostRoom); // 空态置灰由 RefreshDeckPool 管
            _refreshBtn = BindButton("btn-refresh", RefreshRooms); // S6b：双源刷新（登录=TapTap / 未登录=局域网重扫）
            _scanLabel = FindText("lbl-rooms-count"); // 扫描/计数状态行（S6a=局域网扫描；S6b 双源）
            var roomsScroll = FindScroll("list-rooms");
            _roomList = roomsScroll != null ? roomsScroll.Content : null; // 扫描动态行挂它

            // ---- 中栏：排位页（S6a 占位——排位未开放）----
            _rankedDeckLabel = FindText("lbl-ranked-deck");
            _rankedRateLabel = FindText("lbl-ranked-rate");
            _rankedStateLabel = FindText("lbl-ranked-state");
            _rankedStartBtn = BindButton("btn-ranked-start", () => RankedMatchmaking.Start(this)); // S8：排位匹配（须登录+选中卡组——RefreshRankedGate 管）

            // ---- 中栏房间页（S2/S3 结构沿用：座位/模式/操作/底部）----
            for (int seat = 0; seat < 2; seat++)
            {
                var card = Find("seat-" + seat);
                var head = card != null ? FindUnder(card, "row-seat-" + seat) : null;
                var dot = head != null ? FindUnder(head, "dot-seat") : null;
                _seatDots[seat] = dot != null ? dot.GetComponent<UnityEngine.UI.Image>() : null;
                var nick = head != null ? FindUnder(head, "lbl-seat-nick") : null;
                _seatNickLabels[seat] = nick != null ? nick.GetComponentInChildren<TMP_Text>(true) : null;
                var tag = card != null ? FindUnder(card, "lbl-seat-tag") : null;
                _seatTagLabels[seat] = tag != null ? tag.GetComponentInChildren<TMP_Text>(true) : null;
                var ready = card != null ? FindUnder(card, "lbl-seat-ready") : null;
                _seatReadyLabels[seat] = ready != null ? ready.GetComponentInChildren<TMP_Text>(true) : null;
            }
            _modesLabel = FindText("lbl-modes");
            _roomTitleLabel = FindText("lbl-room-title"); // S6b：来源标题（建房/进房时填）
            _addressLabel = FindText("lbl-address");      // S6b：地址/来源行
            _modeFirstBtn = BindButton("btn-mode-first", CycleFirstMoveMode);
            _modeFirstLabel = _modeFirstBtn != null ? _modeFirstBtn.GetComponentInChildren<TMP_Text>(true) : null;
            _modeShuffleBtn = BindButton("btn-mode-shuffle", CycleShuffleMode);
            _modeShuffleLabel = _modeShuffleBtn != null ? _modeShuffleBtn.GetComponentInChildren<TMP_Text>(true) : null;
            _startBtn = BindButton("btn-room-start", HostStartMatch);
            _readyBtn = BindButton("btn-ready", ToggleReady);
            _roomHintLabel = FindText("lbl-room-hint");
            BindButton("btn-add-ai", AddAiOpponent);
            _addAiNode = Find("btn-add-ai");
            _quitBtn = BindButton("btn-room-quit", QuitRoom);

            // ---- 右栏：对战历史（S6c：EncounterStore 列表+选中+复制对手卡组；重播恒灰）----
            Find("panel-history"); // 存在性诊断
            var historyScroll = FindScroll("list-history");
            _historyList = historyScroll != null ? historyScroll.Content : null;
            _copyDeckBtn = BindButton("btn-copy-deck", ImportSelectedRecord); // 选中+有卡组数据才可点（UpdateCopyDeckButton 管）
            _replayBtn = BindButton("btn-replay", null); // 重播不做（SPEC 定案）——恒灰
            if (_replayBtn != null) _replayBtn.interactable = false;

            // ---- TapTap 桥装段：v2 无独立面板（S6b 房间列表双源统一）——段对象仍创建
            //      （登录名/OpenId 经它取），不再调段 Build（旧节点已不存在）----
            _tapSection = TapBridge.Module?.CreateLobbySection(this);
        }

        public override void OnEnter()
        {
            // S1-B2/B3/B4：上一局移交战斗屏后 OnExit 跳过了拆会话（_handedOver）——战斗屏 NetShutdown
            // 只停服务器/连接，本屏的 _server/_client/_beacon 引用与 TapTap 房间态都还挂着
            //（幽灵信标继续广播、再开房被死引用吞掉），重进先统一收口
            if (_server != null || _client != null || _beacon != null)
            {
                TideLog.Info("[Lobby] 检测到上一局残留会话——先收口");
                TeardownSession();
            }
            _handedOver = false;
            _modeFirst = 0;
            _modeShuffle = 0;
            ShowPanel(_panelNormal);
            SetStatus("选择左侧卡组后创建或加入房间");
            RankedMatchmaking.StateChanged -= OnRankedStateChanged; // 防重（屏实例缓存复用）
            RankedMatchmaking.StateChanged += OnRankedStateChanged;
            RefreshSelfInfo();  // S6a ③：登录态/游客（含排位门禁/状态行）
            RefreshDeckPool();  // S6a ④：卡组池（重进恢复选中）
            RefreshHistory();   // S6c：对战历史（战斗返回重新 OnEnter 天然刷新）
        }

        public override void OnExit()
        {
            RankedMatchmaking.StateChanged -= OnRankedStateChanged; // 浮窗独立存活——本屏只退订刷新
            if (_handedOver) return; // 连接已移交战斗屏（BattleEntry.Client）——不拆
            TeardownSession();
        }

        // ============================================================ 连接建立 ============================================================

        /// <summary>创建房间（S6b：btn-create-room）：须先选卡组；房名/密码取输入框
        ///（空房名={昵称}的房间，空密码=无密码）。登录=TapTap 建房；未登录=局域网
        ///（OS 随机端口——信标广播实际端口，手动 IP 已退役）。</summary>
        private void StartHostRoom()
        {
            if (RankedMatchmaking.IsBusy) { SetError("排位匹配进行中——请先在浮窗取消匹配"); return; } // S9
            if (SelectedDeck == null) { SetError("请先在左侧选择一套卡组"); return; } // S6b 定案：无卡组不能建房
            if (_server != null)
            {
                // S1-B2：局后战斗屏 NetShutdown 停过服务器/连接但本屏引用没清——原先静默 return 界面毫无反应
                SetError("检测到未收口的上一局会话——已清理，请再点一次「创建房间」");
                TeardownSession();
                return;
            }
            string roomName = _roomNameInput != null ? _roomNameInput.text.Trim() : "";
            string pwd = RoomPasswordInput;
            if (UseTapRooms) CreateTapRoomAsync(roomName, pwd).Forget();
            else CreateLanRoom(roomName, pwd);
        }

        /// <summary>双源判定（S6b）：已登录 TapTap=联机列表/建房走 TapTap；否则局域网。</summary>
        private bool UseTapRooms => _tapSection != null && _tapSection.IsLoggedIn;

        /// <summary>房间密码输入（S6b：input-room-pwd；空=无密码）。建房与加入共用。</summary>
        private string RoomPasswordInput => _roomPwdInput != null ? _roomPwdInput.text.Trim() : "";

        /// <summary>局域网建房（S6b）：NetSessionServer 带密码 + Start(0)（信标广播实际端口）→
        /// 回环入座（密码/卡组名随协议上报）→ 信标。</summary>
        private void CreateLanRoom(string roomName, string pwd)
        {
            string nick = ResolveLanNickname();
            if (string.IsNullOrEmpty(roomName)) roomName = $"{nick}的房间";
            var server = new NetSessionServer("p2p", msg => Debug.Log(msg),
                password: string.IsNullOrEmpty(pwd) ? null : pwd);
            try
            {
                server.Start(0); // OS 分配空闲口——信标带实际端口（S6b：固定端口/手动 IP 退役）
            }
            catch (Exception ex) // 端口被占（SocketException）等
            {
                SetError($"创建房间失败：{ex.Message}");
                return;
            }

            _lanRoomName = roomName;                              // S6b：房名/密码标志进信标（ver2）
            _lanRoomHasPassword = !string.IsNullOrEmpty(pwd);     // 密码本体不上广播——只带"有无"标志
            BeginHostSession(server,
                $"本机地址（告诉对手）：{string.Join(" / ", LocalIpv4Addresses().Select(ip => $"{ip}:{server.Port}"))}",
                "局域网房间");
            ConnectLoopback(server.Port, opts: BuildJoinOptions(pwd)); // 回环入座（密码/卡组名随协议上报）
            StartBeacon(); // 局域网信标：开房即可被发现；绑定失败不阻塞建房
        }

        /// <summary>TapTap 建房（S6b）：SDK 建房（密码入 customProperties）+ 本地组合传输服务器
        ///（段侧完成）→ BeginHostSession + 回环入座（在线昵称+密码+卡组名）。</summary>
        private async UniTaskVoid CreateTapRoomAsync(string roomName, string pwd)
        {
            string nick = ResolveOnlineNickname();
            if (string.IsNullOrEmpty(roomName)) roomName = $"{nick}的房间";
            SetStatus("正在创建 TapTap 房间…");
            var server = await _tapSection.CreateRoomAsync(roomName, pwd);
            if (server == null)
            {
                SetError(string.IsNullOrEmpty(_tapSection.LastError) ? "创建 TapTap 房间失败" : _tapSection.LastError);
                return; // 留在列表页
            }
            BeginHostSession(server, $"TapTap 房间「{roomName}」——对手在房间列表点选加入", "TapTap 房间");
            ConnectLoopback(server.Port, nick, BuildJoinOptions(pwd)); // 回环入座（密码/卡组名随协议上报）
        }

        // 手动 IP 加入路径已随 v2 面板退役（S5 定案：加入入口只留房间列表）——
        // 加入=列表行（LAN room-{i} / TapTap tap-room-{i}）/ 唯一房间自动直入（ConnectToRoom）。

        private async UniTaskVoid ConnectAsync(string host, int port, int wantSeat, string nickname = null,
            JoinOptions opts = null)
        {
            // S1 昵称定案：默认=局域网解析；TapTap 建房的回环入座经 ConnectLoopback 传入在线昵称
            if (string.IsNullOrEmpty(nickname)) nickname = ResolveLanNickname();
            SetStatus($"连接 {host}:{port} …");
            var client = new NetGameClient();
            bool ok;
            try
            {
                ok = await UniTask.RunOnThreadPool(() =>
                {
                    try { client.Connect(host, port); return true; }
                    catch (Exception ex)
                    {
                        // 连接层异常（拒绝/不可达）——类型必须落日志，不能只吞成 false
                        TideLog.Warn($"[Lobby] 连接异常（{host}:{port}）{ex.GetType().Name}: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                // 异步路径异常（含 AOT 泛型缺口的 MissingMethodException）原先被静默吞成
                // 「连接失败」文案（2026-11-09 真机实录），logcat 零痕迹——类型+栈必须可见
                TideLog.Error($"[Lobby] 连接异常（{host}:{port}）{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                ok = false;
            }
            if (!ok)
            {
                SetError($"连接失败：{host}:{port}（不可达/房未开）");
                client.Close();
                if (_isHost) StopHostServer();
                return;
            }

            // S6b/S8：密码/卡组名/排位三件随进房协议上报（LAN 直连/房主回环两用）
            AdoptClientAndJoin(client, wantSeat, nickname, opts);
        }

        /// <summary>客户端就位：接管连接 → 发协议 JoinRoom（首条消息——分座由 NetRoom 定）。
        /// TCP 直连与 TapTap 传输（已就绪再构造）两路共用。public=Tide.TapTap 联机段跨程序集同路调用。
        /// OpenId（2026-11-09 协议 v11）：TapTap 已登录时随进房自报（对局记录对手身份；局域网空）。
        /// opts（S6b/S8 JoinOptions）：密码（NetRoom 校验）/卡组名/排位标记+指纹+胜率。</summary>
        public void AdoptClientAndJoin(NetGameClient client, int wantSeat, string nickname,
            JoinOptions opts = null)
        {
            _client = client;
            _lastNickname = nickname; // S1：进房昵称即本端昵称快照（MySeatOf 对账口径），两路统一在此落定
            WireClientEvents(client);
            NetClientRuntime.Bind(client);
            client.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom
            {
                RoomId = "",
                WantSeat = wantSeat,
                Nickname = nickname,
                OpenId = _tapSection?.OpenIdOf() ?? "",
                Password = opts?.Password ?? "",              // S6b：房间密码（NetRoom 校验）
                DeckName = opts?.DeckName ?? "",              // S6b：选中卡组名（座位快照带出）
                Ranked = opts?.Ranked ?? false,               // S8：排位房进房标记
                DeckFingerprint = opts?.DeckFingerprint ?? "",// S8：卡组内容指纹
                WinRate = opts?.WinRate ?? -1,                // S8：胜率千分比
            });
            SetStatus("已连接，等待进房…");
        }

        /// <summary>进房参数组装（S8）：密码取输入框；卡组名/指纹/胜率取当前选中——ranked=true 时
        /// 附带排位三件（RankedMatchmaking 与常规两路共用）。</summary>
        internal JoinOptions BuildJoinOptions(string password = null, bool ranked = false)
        {
            var deck = SelectedDeck;
            return new JoinOptions
            {
                Password = password ?? RoomPasswordInput,
                DeckName = deck != null ? deck.name : "",
                Ranked = ranked,
                DeckFingerprint = ranked ? _selectedFingerprint : "",
                WinRate = ranked ? DeckRatingStore.PermilleOf(_selectedFingerprint) : -1,
            };
        }

        /// <summary>发排位接受应答（S8：RankedMatchmaking 浮窗「接受」/取消时的 false）。</summary>
        internal void SendRankedAck(bool accepted)
            => _client?.Send(NetworkMessageType.RankedAck, new MsgRankedAck { Accepted = accepted });

        /// <summary>局域网昵称（2026-11-10 定案）：v2 无昵称输入框——首用「玩家{随机}」并在
        /// _lastNickname 稳定保持（开房/信标/回环入座多次解析须同名；重进屏沿用上次）。
        /// 只服务局域网路径——不读 TapTap 登录名。</summary>
        public string ResolveLanNickname()
        {
            if (string.IsNullOrEmpty(_lastNickname))
                _lastNickname = $"玩家{UnityEngine.Random.Range(1000, 9999)}";
            return _lastNickname;
        }

        /// <summary>联机昵称（2026-11-10 定案）：只取 TapTap 登录名（桥装段；空则 "TapTap玩家"），写 _lastNickname。
        /// **绝不读输入框**——TapTap 段两路径（建房/加入）专用。</summary>
        public string ResolveOnlineNickname()
        {
            var nickname = _tapSection?.FallbackNickname();
            if (string.IsNullOrEmpty(nickname)) nickname = "TapTap玩家";
            _lastNickname = nickname;
            return nickname;
        }

        /// <summary>兼容旧名（原「输入框→TapTap→随机」混合解析已按来源拆分）：转调局域网解析。
        /// TapTap 段不得再调它——用 ResolveOnlineNickname。</summary>
        public string ResolveNickname() => ResolveLanNickname();

        // ============================================================ 左栏：自身信息 + 卡组池（S6a ③④） ============================================================

        /// <summary>自身信息（S6a ③）：已登录 TapTap=登录名+「TapTap 已登录」；
        /// 未登录=「游客」+「局域网模式（未登录 TapTap）」，hint 提示登录可排位。</summary>
        private void RefreshSelfInfo()
        {
            var loginName = _tapSection?.FallbackNickname(); // =TapLobbyController.LoginName（未登录为空）
            bool loggedIn = !string.IsNullOrEmpty(loginName);
            if (_selfNickLabel != null) _selfNickLabel.text = loggedIn ? loginName : "游客";
            if (_selfStateLabel != null)
                _selfStateLabel.text = loggedIn ? "TapTap 已登录" : "局域网模式（未登录 TapTap）";
            if (_selfHintLabel != null)
                _selfHintLabel.text = loggedIn
                    ? "登录账号可跨网络联机与排位"
                    : "登录 TapTap 后可跨网络联机与排位";
            RefreshRankedStateLabel();
            RefreshNormalGate(); // 排位须登录——登录态变化联动按钮门禁
        }

        /// <summary>卡组池刷新（S6a ④；OnEnter 调）：DeckCatalog 全量过滤预组、按名排序、
        /// 重建行（名字+9 柱费用图）；空态=提示行+三按钮置灰（无可用卡组不能建房/加入/排位）；
        /// 选中按静态记住的名字恢复。</summary>
        private void RefreshDeckPool()
        {
            _decks = DeckCatalog.LoadAll()
                .Where(d => d != null && !string.IsNullOrEmpty(d.name) && !DeckCatalog.IsPreset(d.name))
                .OrderBy(d => d.name, StringComparer.CurrentCulture)
                .ToList();
            SelectedDeck = _decks.FirstOrDefault(d => d.name == _lastSelectedDeckName);
            RefreshSelectedFingerprint(); // S8：指纹随选中刷新（排位页胜率/JoinOptions/信标共用）
            RebuildDeckRows();
            RefreshNormalGate();
            UpdateRankedDeckLabel();
        }

        /// <summary>选中卡组指纹刷新（S8：DeckRatingStore 三段口径；无选中=空串）。</summary>
        private void RefreshSelectedFingerprint()
        {
            var deck = SelectedDeck;
            _selectedFingerprint = deck != null
                ? DeckRatingStore.FingerprintOf(deck.cardIds, deck.tokenCardIds, deck.tokenBindings)
                : "";
        }

        /// <summary>常规页门禁（S6a 空态 + S9 匹配中置灰：创建/刷新不可点——匹配期间不能进常规房间）。</summary>
        private void RefreshNormalGate()
        {
            bool empty = _decks == null || _decks.Count == 0;
            bool blocked = empty || RankedMatchmaking.IsBusy;
            if (_createRoomBtn != null) _createRoomBtn.interactable = !blocked;
            if (_refreshBtn != null) _refreshBtn.interactable = !blocked;
            if (_rankedStartBtn != null)
                _rankedStartBtn.interactable = !empty && !RankedMatchmaking.IsBusy
                    && (_tapSection != null && _tapSection.IsLoggedIn); // S8 定案：排位必须登录
        }

        /// <summary>排位页状态行（S8：由 RankedMatchmaking.StateChanged/进场刷新）。</summary>
        private void RefreshRankedStateLabel()
        {
            if (_rankedStateLabel == null) return;
            _rankedStateLabel.text = RankedMatchmaking.State switch
            {
                RankedMatchState.Searching => "匹配中…（同率优先，10s 后放宽）",
                RankedMatchState.Matched => "已匹配对手——5 秒内点浮窗「接受」",
                RankedMatchState.Accepted => "已接受——等待对手…",
                _ => _tapSection != null && _tapSection.IsLoggedIn ? "未在匹配" : "排位需要 TapTap 登录",
            };
        }

        /// <summary>匹配状态迁移回调（S8：门禁+排位页刷新）。</summary>
        private void OnRankedStateChanged()
        {
            RefreshNormalGate();
            RefreshRankedStateLabel();
        }

        /// <summary>重建卡组行（动态行挂 list-decks 的 content；行=圆角底+名字+费用分布 9 柱，
        /// 整行可点选中——底色 RowBg / 选中 SelectedRowBg）。</summary>
        private void RebuildDeckRows()
        {
            _deckRowBgs.Clear();
            if (_deckList == null) return;
            for (int i = _deckList.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_deckList.GetChild(i).gameObject);
            for (int i = 0; i < _decks.Count; i++)
            {
                var deck = _decks[i];
                var row = UiKit.Row("deck-" + i, _deckList, spacing: 10f);
                UiKit.Size(row, fw: 1f, h: 72f);
                var bg = UiKit.BgRow(row, deck == SelectedDeck ? UiStyle.SelectedRowBg : UiStyle.RowBg);
                bg.raycastTarget = true; // BgRow 默认不挡点击——整行点击须开 raycast
                _deckRowBgs.Add(bg);
                var nameLbl = UiKit.Label("lbl-deck-name", row, deck.name, 16, UiStyle.TextPrimary);
                UiKit.Size(nameLbl.rectTransform, fw: 1f); // 名字弹性占宽，费用图靠右
                BuildCostChart(row, deck);
                var captured = deck;
                var click = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                click.targetGraphic = bg;
                click.onClick.AddListener(() => SelectDeck(captured));
            }
        }

        /// <summary>9 柱费用分布图（S6a ④）：TotalCost 取整入桶 1..9（>9 归 9、<1 归 1）；
        /// 柱高=张数/最大值×40，柱下标 1..8、9+。</summary>
        private static void BuildCostChart(RectTransform row, DeckData deck)
        {
            var chart = UiKit.Row("row-cost", row, spacing: 2f);
            var buckets = CostBucketsOf(deck);
            int max = Math.Max(1, buckets.Max());
            for (int c = 1; c <= 9; c++)
            {
                var col = UiKit.Column("col-" + c, chart, spacing: 1f, align: TextAnchor.UpperCenter);
                var bar = UiKit.Row("bar-" + c, col, spacing: 0f);
                UiKit.BgRow(bar, UiStyle.BarFill); // 统计柱定案色（deck-ui-plan §2）
                UiKit.Size(bar, w: 8f, h: buckets[c - 1] * 40f / max);
                UiKit.Label("lbl-col-" + c, col, c == 9 ? "9+" : c.ToString(), 10,
                    UiStyle.TextFaint, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>费用桶（S6a ④）：deck.cardIds 逐张 GetById 取 TotalCost 取整——桶 1..9。</summary>
        private static int[] CostBucketsOf(DeckData deck)
        {
            var buckets = new int[9];
            if (deck?.cardIds == null) return buckets;
            foreach (var id in deck.cardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue;
                int c = Math.Max(1, Math.Min(9, (int)Math.Round(card.TotalCost, MidpointRounding.AwayFromZero)));
                buckets[c - 1]++;
            }
            return buckets;
        }

        /// <summary>选中卡组（S6a ④）：行底色即时刷新+排位页出战卡组联动+静态记住（重进屏保持）。</summary>
        private void SelectDeck(DeckData deck)
        {
            SelectedDeck = deck;
            _lastSelectedDeckName = deck != null ? deck.name : "";
            RefreshSelectedFingerprint(); // S8：指纹/胜率随选中联动
            for (int i = 0; i < _deckRowBgs.Count && i < _decks.Count; i++)
                if (_deckRowBgs[i] != null)
                    _deckRowBgs[i].color = _decks[i] == deck ? UiStyle.SelectedRowBg : UiStyle.RowBg;
            UpdateRankedDeckLabel();
            if (deck != null) SetStatus($"已选择卡组「{deck.name}」");
        }

        /// <summary>排位页出战卡组+胜率（S8 真数据：指纹键取 DeckRatingStore——改卡=指纹变=胜率归零）。</summary>
        private void UpdateRankedDeckLabel()
        {
            if (_rankedDeckLabel != null)
                _rankedDeckLabel.text = SelectedDeck != null ? $"出战卡组：{SelectedDeck.name}" : "未选择卡组";
            if (_rankedRateLabel != null)
                _rankedRateLabel.text = SelectedDeck != null ? DeckRatingStore.Describe(_selectedFingerprint) : "";
        }

        // ============================================================ 右栏：对战历史（S6c——EncounterStore 列表/选中/复制对手卡组） ============================================================

        /// <summary>历史列表刷新（S6c；OnEnter 调——战斗返回重新 OnEnter 天然刷新）：
        /// finishedAtUnix 倒序（最新在前）；空态=提示行。</summary>
        private void RefreshHistory()
        {
            var records = EncounterStore.LoadAll()
                .Where(r => r != null)
                .OrderByDescending(r => r.finishedAtUnix)
                .ToList();
            _selectedRecord = null; // 列表重建——选中态不跨刷新保留
            _historyRecords.Clear();
            _historyRowBgs.Clear();
            UpdateCopyDeckButton();
            if (_historyList == null) return;
            for (int i = _historyList.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_historyList.GetChild(i).gameObject);
            if (records.Count == 0)
            {
                UiKit.Label("lbl-history-empty", _historyList, "暂无对战记录——联机对局结束后自动入账",
                    14, UiStyle.TextFaint, wrap: true);
                return;
            }
            for (int i = 0; i < records.Count; i++)
                BuildHistoryRow(records[i], i);
        }

        /// <summary>历史行（S6c）：圆角底 64 高，主列=胜负·对手名（胜 ToastGreen/负 ErrorRed）+
        /// 双方卡组「A」 vs 「B」，右侧时间 MM-dd HH:mm；整行可点选中。</summary>
        private void BuildHistoryRow(EncounterRecord record, int index)
        {
            var row = UiKit.Row("history-" + index, _historyList, spacing: 8f);
            UiKit.Size(row, fw: 1f, h: 64f);
            var bg = UiKit.BgRow(row, UiStyle.RowBg);
            bg.raycastTarget = true; // 整行点击
            _historyRecords.Add(record);
            _historyRowBgs.Add(bg);
            var main = UiKit.Column("col-main", row, spacing: 2f);
            UiKit.Size(main, fw: 1f); // 主列弹性占宽，时间靠右
            UiKit.Label("lbl-result", main,
                $"{(record.win ? "胜" : "负")} · {record.opponentName}", 16,
                record.win ? UiStyle.ToastGreen : UiStyle.ErrorRed, style: FontStyle.Bold);
            UiKit.Label("lbl-decks", main,
                $"「{record.ownDeckName}」 vs 「{record.opponentDeckName}」", 16, UiStyle.TextDim, wrap: true);
            var time = DateTimeOffset.FromUnixTimeSeconds(record.finishedAtUnix).LocalDateTime;
            UiKit.Label("lbl-time", row, time.ToString("MM-dd HH:mm"), 16, UiStyle.TextFaint);
            var captured = record;
            var click = row.gameObject.AddComponent<UnityEngine.UI.Button>();
            click.targetGraphic = bg;
            click.onClick.AddListener(() => SelectRecord(captured));
        }

        /// <summary>选中历史行（S6c）：底色 SelectedRowBg + 记 _selectedRecord + 复制按钮解禁。</summary>
        private void SelectRecord(EncounterRecord record)
        {
            _selectedRecord = record;
            for (int i = 0; i < _historyRowBgs.Count && i < _historyRecords.Count; i++)
                if (_historyRowBgs[i] != null)
                    _historyRowBgs[i].color = _historyRecords[i] == record
                        ? UiStyle.SelectedRowBg
                        : UiStyle.RowBg;
            UpdateCopyDeckButton();
        }

        /// <summary>复制对手卡组按钮态（S6c）：选中且 opponentCardIds 非空才可点。</summary>
        private void UpdateCopyDeckButton()
        {
            if (_copyDeckBtn == null) return;
            _copyDeckBtn.interactable = _selectedRecord != null
                && _selectedRecord.opponentCardIds != null
                && _selectedRecord.opponentCardIds.Length > 0;
        }

        /// <summary>复制对手卡组（S6c：btn-copy-deck）：确认框 → 复用 OpponentDeckImporter.Import
        ///（包合成/upsert/卡组落库一条不改）→ 结果框（同 MatchHistoryScreen 口径）。</summary>
        private void ImportSelectedRecord()
        {
            var r = _selectedRecord;
            if (r == null) return; // 按钮已置灰——防御
            UiKit.ConfirmBox(Root, "复制对手卡组",
                $"将把对手 {r.opponentName} 的卡组「{r.opponentDeckName}」及其自建卡/效果导入本地"
                + "（本地已有同 ID 内容保留本地版本）。确认导入？",
                "导入", ImportSelectedRecordCore,
                "取消", null, danger: false);
        }

        private void ImportSelectedRecordCore()
        {
            string error = OpponentDeckImporter.Import(_selectedRecord, out var summary);
            if (error != null)
            {
                UiKit.ConfirmBox(Root, "导入失败", error, "知道了", null, "关闭", null, danger: true);
                return;
            }
            UiKit.ConfirmBox(Root, "复制对手卡组", summary ?? "导入完成。", "好的", null, "关闭", null, danger: false);
        }

        // ============================================================ 局域网自动发现（2026-10-28：房主 LanRoomBeacon 应答 + 客人 LanRoomScanner 探测） ============================================================

        /// <summary>房主侧：开局域网信标（payload 快照由主线程在开房/房间态变化时刷新，信标线程只读）。</summary>
        private void StartBeacon()
        {
            ResolveLanNickname(); // 确保昵称快照在案（空则随机）——局域网路径专用解析（S1 定案）
            UpdateBeaconPayload();
            _beacon = new LanRoomBeacon(() => _beaconPayload);
            _beacon.Start(); // UDP/TCP 全绑定失败也继续建房——手输 IP 路径不受影响
        }

        /// <summary>信标 payload 快照刷新（主线程调用；格式
        /// ver|roomId|hostNick|tcpPort|seats|phase|roomName|hasPwd|ranked|fingerprint|rate——
        /// S6b ver2 房名+密码标志；S8 ver3 排位三段。房名内段分隔符替换全角；指纹段用「:」内联无冲突；
        /// 常规 LAN 房 ranked 恒 0（排位必须 TapTap 登录——S8 定案，信标位保留为协议完整）。</summary>
        private void UpdateBeaconPayload()
        {
            if (_server == null) return;
            var phase = _roomState == null
                ? nameof(NetRoomPhase.Waiting)
                : ((NetRoomPhase)_roomState.Phase).ToString();
            int seated = 0;
            if (_roomState?.Players != null)
                foreach (var pl in _roomState.Players)
                    if (pl != null && pl.Connected) seated++;
            var roomName = (_lanRoomName ?? "").Replace("|", "｜"); // 段分隔守卫——房名自由文本
            _beaconPayload = $"3|p2p|{_lastNickname}|{_server.Port}|{seated}/2|{phase}|{roomName}|{(_lanRoomHasPassword ? 1 : 0)}"
                + "|0||-1"; // S8 ver3：ranked=0/fp 空/rate=-1（LAN 排位不可达——定案 2）
        }

        /// <summary>客人侧：进加入面板即扫描；唯一房间自动直入（加入不应自己找房主——2026-10-28 定案）。</summary>
        private void StartScan()
        {
            if (_scanner == null) _scanner = new LanRoomScanner();
            _scanner.StartScan();
            _autoJoinArmed = true;
            _scanSignature = null;
            if (_scanLabel != null) _scanLabel.text = "正在搜索局域网房间…";
            RefreshRoomRows(new List<LanRoomEntry>());
            RunScanLoopAsync().Forget();
        }

        private void StopScan()
        {
            _scanLoopRunning = false;
            _scanner?.StopScan();
        }

        /// <summary>扫描刷新循环（主线程 UniTask 轮询；面板离开/StopScan 即退，重入由 _scanLoopRunning 挡）。
        /// v2：普通页（panel-normal）显示期间扫描（房间列表双源=S6b）。</summary>
        private async UniTaskVoid RunScanLoopAsync()
        {
            if (_scanLoopRunning) return;
            _scanLoopRunning = true;
            while (_scanLoopRunning && _panelNormal != null && _panelNormal.gameObject.activeSelf)
            {
                var rooms = _scanner != null ? _scanner.Snapshot() : new List<LanRoomEntry>();
                UpdateScanUi(rooms);
                await UniTask.Delay(400);
            }
            _scanLoopRunning = false;
        }

        private void UpdateScanUi(List<LanRoomEntry> rooms)
        {
            if (_roomsFromTap) return; // S6b：当前源=TapTap——扫描迟到的快照不写 UI
            if (_scanLabel == null || _roomList == null) return;
            var sig = string.Join(";", rooms.Select(r => $"{r.Key}|{r.Seats}|{r.Phase}"));
            if (sig != _scanSignature)
            {
                _scanSignature = sig;
                RefreshRoomRows(rooms);
            }

            string label;
            if (rooms.Count == 1) label = $"发现房间（{rooms[0].HostNick}，{rooms[0].Seats}）——正在加入…";
            else if (rooms.Count > 1) label = $"发现 {rooms.Count} 个房间——点选加入";
            else if (_scanner != null && !_scanner.Scanning && Environment.TickCount - _scanner.StartedAtMs > 5000)
                label = "未发现房间——确认房主已创建房间后点「刷新」";
            else label = "正在搜索局域网房间…";
            if (_scanLabel.text != label) _scanLabel.text = label;

            if (rooms.Count == 1 && _autoJoinArmed)
            {
                _autoJoinArmed = false; // 唯一房间直接进入
                var only = rooms[0];
                ConnectToRoom(only.Host, only.TcpPort, only.HostNick);
            }
        }

        private void RefreshRoomRows(List<LanRoomEntry> rooms)
        {
            ClearRoomRows();
            for (int i = 0; i < rooms.Count; i++)
            {
                var entry = rooms[i];
                // S6b：ver2 信标带房名/密码标志——行文案与 TapTap 列表同构；旧信标（无房名）回退昵称
                var display = !string.IsNullOrEmpty(entry.RoomName)
                    ? entry.RoomName
                    : $"{entry.HostNick} 的房间";
                UiKit.Button("room-" + i, _roomList, $"{display}（{entry.Seats}）" + (entry.HasPassword ? "·有密码" : ""),
                    () => ConnectToRoom(entry.Host, entry.TcpPort, entry.HostNick), UiStyle.BtnPrimary,
                    height: 44f, fontSize: 16); // S3：基准按钮规格
            }
        }

        /// <summary>清空房间列表动态行（双源共用同一 content）。</summary>
        private void ClearRoomRows()
        {
            if (_roomList == null) return;
            for (int i = _roomList.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_roomList.GetChild(i).gameObject);
        }

        // ============================================================ 房间列表双源（S6b：登录=TapTap 公开房间 / 未登录=局域网扫描） ============================================================

        /// <summary>双源列表入口（普通页进入与 btn-refresh 都走这里）：登录→拉 TapTap 列表
        ///（顺带停扫描线程）；未登录→局域网扫描（TapTap 拉取为一次性 await，无循环可停）。</summary>
        private void RefreshRooms()
        {
            if (UseTapRooms) RefreshTapRoomsAsync().Forget();
            else
            {
                _roomsFromTap = false;
                StartScan();
            }
        }

        /// <summary>拉 TapTap 公开房间列表（S6b/S8）：段侧只滤满员（ranked 房带元数据返回——
        /// 常规行构建时 UI 侧滤 Ranked，排位匹配器专用候选）；状态写 lbl-rooms-count。</summary>
        private async UniTaskVoid RefreshTapRoomsAsync()
        {
            if (_tapSection == null || _tapListBusy) return;
            _tapListBusy = true;
            _roomsFromTap = true;
            StopScan(); // 切源停另一方（局域网扫描线程）
            try
            {
                if (_scanLabel != null) _scanLabel.text = "正在拉取 TapTap 房间列表…";
                var rooms = await _tapSection.ListRoomsAsync();
                if (!_roomsFromTap) return; // 拉取期间已切回局域网——丢弃迟到结果
                if (rooms == null)
                {
                    SetError(string.IsNullOrEmpty(_tapSection.LastError) ? "拉取房间列表失败" : _tapSection.LastError);
                    if (_scanLabel != null) _scanLabel.text = "拉取失败——点「刷新」重试";
                    return;
                }
                RefreshTapRoomRows(rooms);
                if (_scanLabel != null)
                    _scanLabel.text = rooms.Count == 0
                        ? "暂无公开房间——可点「创建房间」当房主"
                        : $"共 {rooms.Count} 个房间（点条目加入）";
            }
            finally { _tapListBusy = false; }
        }

        /// <summary>TapTap 房间行动态构建（挂 list-rooms content；有密码房行尾标注；
        /// ranked 房不进常规列表——S8 排位走匹配器双接受流）。</summary>
        private void RefreshTapRoomRows(List<TapRoomEntry> rooms)
        {
            ClearRoomRows();
            int row = 0;
            foreach (var entry in rooms)
            {
                if (entry.Ranked) continue; // S8：排位房只做匹配候选，不列常规列表
                UiKit.Button("tap-room-" + row, _roomList,
                    $"{entry.Name}（{entry.PlayerCount}/{entry.MaxPlayerCount}）" + (entry.HasPassword ? "·有密码" : ""),
                    () => JoinTapEntryAsync(entry).Forget(),
                    height: 44f, fontSize: 16);
                row++;
            }
        }

        /// <summary>加入 TapTap 公开房间（S6b）：须先选卡组；SDK 加入+传输就绪由段侧完成，
        /// 进房协议（Password/DeckName）走 AdoptClientAndJoin 统一口；失败 SetError 留在列表。</summary>
        private async UniTaskVoid JoinTapEntryAsync(TapRoomEntry entry)
        {
            if (RankedMatchmaking.IsBusy) { SetError("排位匹配进行中——请先在浮窗取消匹配"); return; } // S9
            if (entry.Ranked) return; // 排位房不进常规列表流（S8：排位走匹配器双接受）
            if (SelectedDeck == null) { SetError("请先在左侧选择一套卡组"); return; } // S6b：无卡组不能加入
            if (_tapSection == null || string.IsNullOrEmpty(entry.RoomId)) return;
            string pwd = RoomPasswordInput;
            SetStatus($"加入「{entry.Name}」…");
            var client = await _tapSection.JoinRoomAsync(entry.RoomId, pwd);
            if (client == null)
            {
                SetError(string.IsNullOrEmpty(_tapSection.LastError) ? "加入失败：房间可能已满或已开局" : _tapSection.LastError);
                return; // 留在列表
            }
            BeginGuestSession($"TapTap 房间「{entry.Name}」", "TapTap 房间");
            AdoptClientAndJoin(client, wantSeat: -1, ResolveOnlineNickname(), BuildJoinOptions(pwd));
        }

        /// <summary>按发现结果直连（房间列表/唯一房间自动直入共用；手动 IP 路径已退役）。
        /// S6b：须先选卡组；密码取 input-room-pwd、卡组名=SelectedDeck 随协议上报。</summary>
        private void ConnectToRoom(string host, int port, string hostNick)
        {
            StopScan();
            if (RankedMatchmaking.IsBusy) { SetError("排位匹配进行中——请先在浮窗取消匹配"); return; } // S9
            if (SelectedDeck == null) { SetError("请先在左侧选择一套卡组"); return; } // S6b：无卡组不能加入
            SetStatus($"发现房间（{(string.IsNullOrEmpty(hostNick) ? host : hostNick)}），正在加入 {host}:{port} …");
            BeginGuestSession($"目标（自动发现）：{host}:{port}", "局域网房间");
            ConnectAsync(host, port, wantSeat: -1, opts: BuildJoinOptions()).Forget();
        }

        // ============================================================ TapTap 桥装段与跨程序集会话助手（Tide.TapTap 的 TapLobbySection 经此驱动本屏；跨程序集须 public） ============================================================

        /// <summary>TapTap 面板 RectTransform 登记（桥装段 Build 时交回——纳入 ShowPanel 换页集）。</summary>
        public void RegisterTapPanel(RectTransform panel) => _panelTap = panel;

        /// <summary>房主会话就位（LAN/TapTap 建房共用尾段）：登记服务器→房间页→标题/地址展示→加 AI 可用。
        /// roomTitle（S6b 来源标题）：「局域网房间」/「TapTap 房间」，缺省「对战房间」。</summary>
        public void BeginHostSession(NetSessionServer server, string addressText, string roomTitle = null)
        {
            _isHost = true;
            _server = server;
            ShowPanel(_panelRoom);
            if (_addAiNode != null) _addAiNode.gameObject.SetActive(true);
            if (_roomTitleLabel != null)
                _roomTitleLabel.text = string.IsNullOrEmpty(roomTitle) ? "对战房间" : roomTitle;
            if (_addressLabel != null) _addressLabel.text = addressText;
            SetStatus("房间已创建，等待对手加入…");
            RefreshRoomUi(); // 就绪层初刷（模式钮=房主可见、开始钮未就绪置灰）
        }

        /// <summary>客人会话就位（LAN 直连/TapTap 加入共用尾段）：房间页→标题/地址展示→加 AI 收起。</summary>
        public void BeginGuestSession(string addressText, string roomTitle = null)
        {
            _isHost = false;
            ShowPanel(_panelRoom);
            if (_roomTitleLabel != null)
                _roomTitleLabel.text = string.IsNullOrEmpty(roomTitle) ? "对战房间" : roomTitle;
            if (_addressLabel != null) _addressLabel.text = addressText;
            if (_addAiNode != null) _addAiNode.gameObject.SetActive(false);
            RefreshRoomUi(); // 就绪层初刷（准备钮=客人可见）
        }

        /// <summary>房主回环入座（127.0.0.1，后台线程连接避免 UI 卡帧；LAN/TapTap 建房同路）。
        /// nickname 缺省=局域网解析；TapTap 建房传入登录昵称（S1 昵称来源定案）。
        /// opts（S6b/S8）：自家密码（回环入座同受门禁）/排位三件。</summary>
        public void ConnectLoopback(int port, string nickname = null, JoinOptions opts = null)
            => ConnectAsync("127.0.0.1", port, wantSeat: 0, nickname, opts).Forget();

        public void ShowEntryPanel() => ShowPanel(_panelNormal); // v2：入口页=普通页（跨程序集兼容名保留）
        public void ShowRoomPanel() => ShowPanel(_panelRoom);
        public void ShowTapPanel() => ShowPanel(_panelTap); // v2 无 TapTap 面板（_panelTap=null=no-op；S6b 双源统一）

        /// <summary>下行事件接线（Pump 在主线程分派——回调里直接碰 UI 安全）。</summary>
        private void WireClientEvents(NetGameClient client)
        {
            client.OnRoomState += OnRoomState;
            client.OnError += OnServerError;
            client.OnDisconnected += OnDisconnected;
            // OnManifest 归战斗屏（2026-10-28：选卡组相位即移交，manifest 在战场内校验收口）
        }

        private void UnwireClientEvents(NetGameClient client)
        {
            client.OnRoomState -= OnRoomState;
            client.OnError -= OnServerError;
            client.OnDisconnected -= OnDisconnected;
        }

        // ============================================================ 房间态（2026-10-28 就绪层改版） ============================================================

        private static readonly string[] FirstMoveLabels = { "先后手：掷硬币", "先后手：房主先手", "先后手：客人先手" };
        private static readonly string[] ShuffleLabels = { "洗牌：随机", "洗牌：固定牌序" };

        private void OnRoomState(MsgRoomState room)
        {
            _roomState = room;
            UpdateBeaconPayload(); // 座位/阶段变化刷新信标快照（主线程）
            RankedMatchmaking.OnRoomState(room); // S8：排位双座满 → 5s 接受窗（非匹配态内部自滤）

            // 房主开局 → 选卡组相位（先手已定+30s 倒计时）。S7a（2026-11-10 跳过战斗选卡组）：
            // 先用大厅选中卡组**提前提交** DeckSubmit（公共提交器——payload/digest 单源），
            // 战斗屏五段式跳过段四不再弹选卡面板；提交失败（理论不可达——进房门禁已拦无卡组）
            // 走服务器 30s 代提兜底，移交照常。
            if (room.Phase == (int)NetRoomPhase.DeckSubmit && !_handedOver)
            {
                if (SelectedDeck != null && NetDeckSubmit.Send(_client, SelectedDeck))
                    BattleEntry.NetDeckSubmitted = true;
                else
                    TideLog.Warn("[Lobby] S7a 提前提交失败（无选中卡组？）——依赖服务器 30s 代提兜底");
                HandOverToBattle(room);
                return;
            }

            var phaseText = room.Phase == (int)NetRoomPhase.Waiting ? "等待就座"
                : room.Phase == (int)NetRoomPhase.Playing ? "对战中" : "已结束";
            int mySeat = MySeatOf(room);
            var opp = room.Players != null ? room.Players.FirstOrDefault(p => p != null && p.Seat != mySeat) : null;
            // S1-B7：GuestReady 恒为客人（椅位 1）自己的准备态——房主读它=对手状态，客人读它=自己状态。
            // 原先不分角色统一写成「对手：X（未准备）」，客人误以为在等对手点准备，实际该点的是自己
            string oppText;
            if (_isHost)
            {
                if (opp == null || opp.Seat < 0 || !opp.Connected)
                    oppText = "等待对手加入…";
                else if (room.GuestReady)
                    oppText = "对手已准备，可点「开始对战」";
                else
                    oppText = $"对手：{opp.Nickname}（未准备）——等对手点「准备」";
            }
            else
            {
                oppText = room.GuestReady
                    ? "你已准备，等待房主开始…"
                    : "你：未准备——点下方「准备」，房主才能开局";
            }
            SetStatus($"[{phaseText}] {oppText}");
            RefreshRoomUi();
        }

        /// <summary>房间面板就绪层刷新：双座位卡（S2）/模式展示（服务器回显权威）/房主开始钮门槛/
        /// 客人准备钮（S2 主色/中性底切换）/常驻引导行（S1）/退出钮角色文案（S1：房主=解散房间）。</summary>
        private void RefreshRoomUi()
        {
            if (_roomState == null) return;
            RefreshSeatCards();
            int first = ((_roomState.FirstMoveMode % 3) + 3) % 3;
            int shuffle = ((_roomState.ShuffleMode % 2) + 2) % 2;
            if (_modesLabel != null)
                _modesLabel.text = $"模式：{FirstMoveLabels[first].Replace("先后手：", "")} · {ShuffleLabels[shuffle].Replace("洗牌：", "")}";
            if (_modeFirstLabel != null) _modeFirstLabel.text = FirstMoveLabels[first];
            if (_modeShuffleLabel != null) _modeShuffleLabel.text = ShuffleLabels[shuffle];

            int mySeat = MySeatOf(_roomState);
            var opp = _roomState.Players != null
                ? _roomState.Players.FirstOrDefault(p => p != null && p.Seat != mySeat && p.Seat >= 0) : null;
            bool oppReady = opp != null && opp.Connected && _roomState.GuestReady;

            if (_modeFirstBtn != null) _modeFirstBtn.gameObject.SetActive(_isHost);
            if (_modeShuffleBtn != null) _modeShuffleBtn.gameObject.SetActive(_isHost);
            if (_startBtn != null)
            {
                _startBtn.gameObject.SetActive(_isHost);
                _startBtn.interactable = oppReady;
            }
            if (_readyBtn != null)
            {
                _readyBtn.gameObject.SetActive(!_isHost);
                var lbl = _readyBtn.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = _roomState.GuestReady ? "取消准备" : "准备（让房主开局）"; // S1：点明这是客人自己的动作
                // S2：未准备=主色底白字引导点击；已准备=中性底（文案不变）
                var readyImg = _readyBtn.GetComponent<UnityEngine.UI.Image>();
                if (readyImg != null) readyImg.color = _roomState.GuestReady ? UiStyle.BtnBg : UiStyle.BtnPrimary;
                if (lbl != null) lbl.color = _roomState.GuestReady ? UiStyle.TextBody : UiStyle.White;
            }
            if (_roomHintLabel != null)
            {
                // S1：常驻引导——真机实录里客人不知道要点「准备」、房主不知道在等什么
                if (_isHost)
                    _roomHintLabel.text = opp == null || !opp.Connected ? "等待对手加入…"
                        : _roomState.GuestReady ? "对手已准备，点「开始对战」"
                        : "对手已入座，等待对手点「准备」";
                else
                    _roomHintLabel.text = _roomState.GuestReady ? "已准备，等待房主开始…" : "点「准备」，让房主开始对战";
            }
            if (_quitBtn != null)
            {
                var quitLbl = _quitBtn.GetComponentInChildren<TMP_Text>(true);
                if (quitLbl != null) quitLbl.text = _isHost ? "解散房间" : "离开房间"; // S1：房主离开=整房解散（语义如实）
            }
        }

        /// <summary>双座位卡填座（S2；S3 座位判定改角色口径）：Players 恒 2 元素（按 Seat 落卡防乱序）——
        /// 「你」标记按角色座位号（_isHost?0:1，不比昵称——同名双默认名会双标）；AI- 前缀=AI；
        /// 在线点绿/灰；准备态只有椅位 1（GuestReady 恒指客人）有意义。</summary>
        private void RefreshSeatCards()
        {
            int mySeat = _isHost ? 0 : 1; // S3：角色定座——isSelf 用座位号比较
            var players = _roomState?.Players;
            for (int i = 0; i < 2; i++)
            {
                var p = players != null && i < players.Length ? players[i] : null;
                int seat = p != null && p.Seat >= 0 && p.Seat < 2 ? p.Seat : i; // 防 Players 乱序
                bool connected = p != null && p.Connected;
                string nick = !string.IsNullOrEmpty(p?.Nickname) ? p.Nickname : "等待加入…";
                bool isAi = connected && nick.StartsWith("AI-", StringComparison.Ordinal);
                bool isSelf = seat == mySeat;
                string tag = isAi ? "AI"
                    : (seat == 0 ? "房主" : "客人") + (isSelf ? "·你" : "");
                if (_seatNickLabels[seat] != null) _seatNickLabels[seat].text = connected ? nick : "等待加入…";
                if (_seatTagLabels[seat] != null) _seatTagLabels[seat].text = connected ? tag : (seat == 0 ? "房主" : "客人");
                if (_seatDots[seat] != null) _seatDots[seat].color = connected ? UiStyle.DotGreen : UiStyle.DotGray;
                if (_seatReadyLabels[seat] != null)
                    _seatReadyLabels[seat].text = seat == 1 && connected
                        ? (_roomState.GuestReady ? "已准备" : "未准备") : "";
            }
        }

        private void CycleFirstMoveMode()
        {
            if (!_isHost) return;
            _modeFirst = (_modeFirst + 1) % 3;
            SendRoomSettings();
        }

        private void CycleShuffleMode()
        {
            if (!_isHost) return;
            _modeShuffle = (_modeShuffle + 1) % 2;
            SendRoomSettings();
        }

        private void SendRoomSettings()
        {
            if (_client == null) return;
            _client.Send(NetworkMessageType.RoomSettings, new MsgRoomSettings
            {
                FirstMoveMode = _modeFirst,
                ShuffleMode = _modeShuffle,
            });
            RefreshRoomUi(); // 乐观刷新（服务器回显随后校正）
        }

        private void ToggleReady()
        {
            if (_client == null || _roomState == null || _roomState.Phase != (int)NetRoomPhase.Waiting) return;
            _client.Send(NetworkMessageType.RoomReady, new MsgRoomReady { Ready = !_roomState.GuestReady });
        }

        private void HostStartMatch()
        {
            if (_client == null || _roomState == null || _roomState.Phase != (int)NetRoomPhase.Waiting) return;
            _client.Send(NetworkMessageType.RoomStart, (object)null);
        }

        /// <summary>房主加 AI 对手（回环填位——对房间就是普通客户端；就绪层下 AI 自动准备，房主即可开局）。</summary>
        private void AddAiOpponent()
        {
            if (!_isHost || _server == null) return;
            if (_roomState != null && _roomState.Phase != (int)NetRoomPhase.Waiting)
            {
                SetStatus("只在等待阶段可以加 AI");
                return;
            }
            if (_aiFiller != null) return;
            _aiFiller = NetAiFiller.FromPlayPool($"AI-{UnityEngine.Random.Range(100, 999)}");
            if (_aiFiller == null)
            {
                SetError("AI 填位失败：本地卡表为空");
                return;
            }
            _aiFiller.Start(_server.Port);
            SetStatus("AI 对手正在入座…");
        }

        /// <summary>本端椅位（S3 定案：按角色定座——本房间恒双人位，房主=椅 0、客人=椅 1，AI 只是房主的客位。
        /// 昵称匹配仅留日志对账，不再是判定依据：两人同名（如同为 TapTap 默认名）时会双方同标
        /// 「你」、先手判定（HandOverToBattle 的 mySeat）取错座）。</summary>
        private int MySeatOf(MsgRoomState room)
        {
            int seat = _isHost ? 0 : 1;
            if (room?.Players != null)
                foreach (var p in room.Players)
                    if (p != null && p.Seat == seat && p.Connected && p.Nickname != _lastNickname)
                        TideLog.Warn($"[Lobby] 椅位昵称对账不一致（椅{seat} 实际={p.Nickname} 本端={_lastNickname}）——以角色定座为准");
            return seat;
        }

        // ============================================================ 开局移交 / 收尾 ============================================================

        /// <summary>选卡组相位到达（房主已开局）：双方移交战场走五段式（掷币演出+战场选卡组+握手开战）。
        /// 先手表现参数在此解析：掷币模式=照常 3D 演出（结果=服务器权威注入）；固定先后手=快进直出浮字。</summary>
        private void HandOverToBattle(MsgRoomState room)
        {
            if (_client == null) return;
            _handedOver = true;
            RankedMatchmaking.OnBattleHandedOver(); // S8：排位匹配完成——关浮窗回 Idle（指纹留作终局记账）
            UnwireClientEvents(_client); // 战斗屏接手下行事件（含 manifest 校验——选卡组确认后到）
            int mySeat = MySeatOf(room);
            bool coinMode = room.FirstMoveMode == 0;
            bool playerFirst = coinMode
                ? room.FirstSeatThisMatch == mySeat
                : room.FirstMoveMode == 1 ? mySeat == 0 : mySeat == 1;
            BattleEntry.Mode = BattleMode.Network;
            BattleEntry.Client = _client;
            BattleEntry.Nickname = _lastNickname;
            BattleEntry.StagedOpening = true; // 网络五段式（2026-10-28：掷币+选卡组；S7a 已提前提交时段四跳过）
            BattleEntry.NetCoinToss = coinMode;
            BattleEntry.NetPlayerFirst = playerFirst;
            SetStatus("房主已开局——进入战场（掷币+选卡组）");
            TideLog.Info($"[Lobby] 移交战场：mySeat={mySeat} 先手椅位={room.FirstSeatThisMatch} 掷币演出={coinMode} playerFirst={playerFirst}");
            BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
        }

        private void OnServerError(MsgError err)
        {
            SetError($"被拒（{err.Context}）：{err.Reason}" + (err.Context == "DeckSubmit" ? "——请重选卡组提交" : ""));
            // S6b：进房被拒（密码错/满员/已开局）——收口连接与 SDK 房态，回列表页可重试
            if (err.Context == "JoinRoom")
            {
                TeardownSession();
                ResetToEntry();
            }
            // S8：排位未接受/超时（服务器已断开）——匹配状态收敛回 Idle（不自动重排）
            if (err.Context == "Ranked")
                RankedMatchmaking.NotifyError();
        }

        private void OnDisconnected()
        {
            if (_handedOver) return; // 战斗屏接管后断线由战斗屏收口
            SetStatus("连接已断开");
            ResetToEntry();
        }

        /// <summary>离开房间（S1 语义分离）：房主离开=解散整房（停服务器/断信标/退 TapTap 房间，
        /// 客人全部掉线）——须确认；客人离开只断自己。</summary>
        private void QuitRoom()
        {
            if (_isHost)
            {
                UiKit.ConfirmBox(Root, "解散房间", "解散后所有客人将被断开连接，确定解散？",
                    "解散", () => { QuitRoomNow(); SetStatus("房间已解散"); }, danger: true);
                return;
            }
            QuitRoomNow();
        }

        private void QuitRoomNow()
        {
            _handedOver = false;
            TeardownSession();
            ResetToEntry();
        }

        private void ResetToEntry()
        {
            _roomState = null;
            ShowPanel(_panelNormal); // v2：回中栏普通页
        }

        /// <summary>拆本屏持有的会话（连接+房主服务器+AI 填位+TapTap 房间态）。
        /// internal=S8：RankedMatchmaking 拆自开等待房/断候选连接同用本口。</summary>
        internal void TeardownSession()
        {
            if (_client != null)
            {
                UnwireClientEvents(_client);
                NetClientRuntime.Unbind(_client);
                if (!_client.Disconnected) _client.Close(); // S1：战斗屏已 Close 过的连接不再重复收口
                _client = null;
            }
            _aiFiller?.Stop();
            _aiFiller = null;
            _scanLoopRunning = false;
            try { _scanner?.StopScan(); } catch (Exception) { }
            StopHostServer();
            _tapSection?.Teardown(); // TapTap 段收口：退房+断对战服务（对局移交后不走这里；单机风味无段=no-op）
            _roomState = null;
        }

        private void StopHostServer()
        {
            if (_server == null) return;
            _beacon?.Dispose(); // 先停信标再停服务器（扫描端即刻探测不到）
            _beacon = null;
            _server.Stop();
            _server = null;
            _isHost = false;
        }

        // ============================================================ 杂项 ============================================================

        /// <summary>中栏换页（v2 三页：普通/排位/房间）。普通页进出即扫描起停（2026-10-28 口径沿用）。</summary>
        private void ShowPanel(RectTransform panel)
        {
            if (_panelNormal != null) _panelNormal.gameObject.SetActive(ReferenceEquals(panel, _panelNormal));
            if (_panelRanked != null) _panelRanked.gameObject.SetActive(ReferenceEquals(panel, _panelRanked));
            if (_panelRoom != null) _panelRoom.gameObject.SetActive(ReferenceEquals(panel, _panelRoom));
            RefreshTabVisuals(); // 房间页=两页签都不选中（中性底）

            if (ReferenceEquals(panel, _panelNormal)) RefreshRooms(); // S6b：双源（登录=TapTap / 未登录=扫描）
            else StopScan();
        }

        /// <summary>页签切换（S6a ②）：切中栏普通/排位页。</summary>
        private void ShowCenterTab(bool ranked) => ShowPanel(ranked ? _panelRanked : _panelNormal);

        /// <summary>页签选中态（S6a ②）：选中=BtnPrimary 主色底白字；未选=BtnBg 中性底。</summary>
        private void RefreshTabVisuals()
        {
            bool rankedActive = _panelRanked != null && _panelRanked.gameObject.activeSelf;
            bool normalActive = _panelNormal != null && _panelNormal.gameObject.activeSelf;
            ApplyTabVisual(_tabNormalBtn, normalActive);
            ApplyTabVisual(_tabRankedBtn, rankedActive);
        }

        private static void ApplyTabVisual(UnityEngine.UI.Button btn, bool selected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = selected ? UiStyle.BtnPrimary : UiStyle.BtnBg;
            var lbl = btn.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.color = selected ? UiStyle.White : UiStyle.TextBody;
        }

        /// <summary>状态行刷新（Tide.TapTap 联机段共用）。public=跨程序集同路调用。
        /// 常驻状态行（S1-B1）单节点两色：普通提示=ToastGreen（覆盖上次的红）。</summary>
        public void SetStatus(string text)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = text;
                _statusLabel.color = UiStyle.ToastGreen;
            }
            Debug.Log($"[Lobby] {text}");
        }

        /// <summary>失败提示（S1-B1）：常驻状态行红字——任何面板下都可见，与 SetStatus 同一节点。</summary>
        public void SetError(string text)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = text;
                _statusLabel.color = UiStyle.ErrorRed;
            }
            Debug.Log($"[Lobby] [失败] {text}");
        }

        // ParsePort 已随 v2 输入框退役（端口固定 DefaultPort=28000）。

        /// <summary>本机 IPv4 清单（房主展示供对手输入；过滤回环/IPv6）。</summary>
        private static List<string> LocalIpv4Addresses()
        {
            var result = new List<string>();
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        result.Add(ip.ToString());
                }
            }
            catch (Exception) { /* 主机名解析失败（离线等）——列表留空，回环地址兜底提示 */ }
            if (result.Count == 0) result.Add("127.0.0.1");
            return result;
        }
    }
}
