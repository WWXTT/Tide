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
    /// <summary>
    /// 联机房间屏（2026-10-21 P2P 复活；纯代码构建——prefab 化留待美术换皮，
    /// 节点名按预制体口径预留）：房主=本进程内 NetSessionServer（UIBootstrap 泵驱动），
    /// 客人 NetGameClient 直连 IP:端口——不经大厅层（直连 JoinRoom 是 M2 兼容路径）。
    /// 房主自身也走回环客户端入座（127.0.0.1——与 AI 填位同款先例，天然享受座位过滤，
    /// 与客人同一客户端代码路径）。
    ///
    /// 流程：入口（昵称）→ [创建房间：起服务器+回环入座+显示本机地址] / [加入：输 IP:端口]
    /// → 房间面板（RoomState 实时刷新；房主可 +AI 填位测试）→ 双方就座后各循环选一套
    /// 已保存卡组提交（DeckSubmit 闭包校验）→ 服务器开局下发 MatchManifest → 同连接经
    /// BattleEntry.Client 移交战斗屏（网络模式）。终局/断线回本屏或主菜单
    ///（OnExit 未移交时统一拆连接）。
    /// </summary>
    public sealed class LobbyScreen : UIScreen
    {
        protected override string RootName => "lobby-screen";

        private const int DefaultPort = 28000;

        private static string _lastNickname = "";

        private RectTransform _panelEntry, _panelHost, _panelJoin, _panelRoom, _panelTap;
        private TMP_InputField _nickInput, _portInput, _ipInput, _port2Input;
        private TMP_Text _statusLabel, _addressLabel, _modesLabel, _modeFirstLabel, _modeShuffleLabel;
        private UnityEngine.UI.Button _startBtn, _readyBtn, _modeFirstBtn, _modeShuffleBtn;
        private RectTransform _addAiNode;

        // TapTap 联机段（2026-10-24 预编译隔离迁 Tide.TapTap 程序集）：段对象与面板 RectTransform
        // 经桥装缝接回（RegisterTapPanel/换页助手）；本地单机风味 Module=null——无 TapTap 入口钮，
        // 本屏只剩局域网建房/加入两路径。
        private ITapLobbySection _tapSection;

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

        // 卡组选择（循环制）：0=随机卡组，1..N=已保存卡组
        private List<DeckData> _savedDecks;
        private int _deckIndex;

        // 局域网自动发现（2026-10-28 加入流改造）：房主信标 + 客人扫描，唯一房间自动直入
        private LanRoomBeacon _beacon;
        private volatile string _beaconPayload = "";
        private LanRoomScanner _scanner;
        private bool _scanLoopRunning;
        private bool _autoJoinArmed;
        private string _scanSignature;
        private TMP_Text _scanLabel;
        private RectTransform _roomList;

        protected override void Build()
        {
            Root = UiKit.Screen(RootName, Parent);

            UiKit.Label("lbl-title", Root, "联机对战（房主即服务器）", 20,
                align: TextAnchor.MiddleCenter, style: FontStyle.Bold);

            // ---- 入口面板 ----
            _panelEntry = UiKit.Column("panel-entry", Root);
            var nickRow = UiKit.Row("row-nick", _panelEntry);
            UiKit.Label("lbl-nick", nickRow, "昵称：");
            _nickInput = UiKit.InputField("input-nick", nickRow, "输入昵称", _lastNickname, width: 240f);
            var entryBtns = UiKit.Row("row-entry-btns", _panelEntry);
            UiKit.Button("btn-host", entryBtns, "创建房间（房主）", null, UiStyle.BtnPrimary);
            UiKit.Button("btn-join", entryBtns, "加入房间（客人）");
            // TapTap 联机入口钮（插在加入与返回之间，按钮序与原版一致）；本地单机风味桥装段缺席——
            // 无此钮，面板集里也无 panel-taptap。
            _tapSection = TapBridge.Module?.CreateLobbySection(this);
            _tapSection?.Build(Root, entryBtns);
            UiKit.Button("btn-lobby-back", entryBtns, "返回主菜单");

            // ---- 房主面板 ----
            _panelHost = UiKit.Column("panel-host", Root);
            var portRow = UiKit.Row("row-port", _panelHost);
            UiKit.Label("lbl-port", portRow, "监听端口：");
            _portInput = UiKit.InputField("input-port", portRow, "端口", DefaultPort.ToString(), width: 120f);
            UiKit.Button("btn-host-start", _panelHost, "开房", StartHostRoom, UiStyle.BtnPrimary);
            UiKit.Button("btn-host-back", _panelHost, "返回");

            // ---- 加入面板（2026-10-28 局域网自动发现：进面板即扫描，唯一房间自动直入；手输 IP 降为兜底） ----
            _panelJoin = UiKit.Column("panel-join", Root);
            _scanLabel = UiKit.Label("lbl-scan", _panelJoin, "正在搜索局域网房间…", 14, wrap: true);
            _roomList = UiKit.Column("room-list", _panelJoin);
            UiKit.Button("btn-rescan", _panelJoin, "重新扫描", RestartScan);
            var ipRow = UiKit.Row("row-ip", _panelJoin);
            UiKit.Label("lbl-ip", ipRow, "房主 IP（手动）：");
            _ipInput = UiKit.InputField("input-ip", ipRow, "例如 192.168.1.100", width: 240f);
            var port2Row = UiKit.Row("row-port2", _panelJoin);
            UiKit.Label("lbl-port2", port2Row, "端口：");
            _port2Input = UiKit.InputField("input-port2", port2Row, "端口", DefaultPort.ToString(), width: 120f);
            UiKit.Button("btn-join-connect", _panelJoin, "连接", ConnectGuest, UiStyle.BtnPrimary);
            UiKit.Button("btn-join-back", _panelJoin, "返回");

            // ---- 房间面板（2026-10-28 就绪层改版：模式配置/准备/开始取代房内选卡组——选卡组挪战场五段式）----
            _panelRoom = UiKit.Column("panel-room", Root);
            _addressLabel = UiKit.Label("lbl-address", _panelRoom, "", 13, wrap: true);
            _statusLabel = UiKit.Label("lbl-status", _panelRoom, "", 14, UiStyle.ToastGreen, wrap: true);
            _modesLabel = UiKit.Label("lbl-modes", _panelRoom, "模式：先后手：掷硬币 · 洗牌：随机", 14, wrap: true);
            var modeRow = UiKit.Row("row-mode", _panelRoom);
            _modeFirstBtn = UiKit.Button("btn-mode-first", modeRow, "先后手：掷硬币", CycleFirstMoveMode);
            _modeFirstLabel = _modeFirstBtn != null ? _modeFirstBtn.GetComponentInChildren<TMP_Text>(true) : null;
            _modeShuffleBtn = UiKit.Button("btn-mode-shuffle", modeRow, "洗牌：随机", CycleShuffleMode);
            _modeShuffleLabel = _modeShuffleBtn != null ? _modeShuffleBtn.GetComponentInChildren<TMP_Text>(true) : null;
            _startBtn = UiKit.Button("btn-room-start", _panelRoom, "开始对战", HostStartMatch, UiStyle.BtnPrimary);
            _readyBtn = UiKit.Button("btn-ready", _panelRoom, "准备", ToggleReady);
            var roomBtns = UiKit.Row("row-room-btns", _panelRoom);
            var addAi = UiKit.Button("btn-add-ai", roomBtns, "加AI对手", AddAiOpponent);
            _addAiNode = addAi != null ? (RectTransform)addAi.transform : null;
            UiKit.Button("btn-room-quit", roomBtns, "离开房间", QuitRoom);

            BindButton("btn-lobby-back", () => Manager.Back());
            BindButton("btn-host", () => ShowPanel(_panelHost));
            BindButton("btn-host-back", () => ShowPanel(_panelEntry));
            BindButton("btn-join", () => ShowPanel(_panelJoin));
            BindButton("btn-join-back", () => ShowPanel(_panelEntry));
        }

        public override void OnEnter()
        {
            _handedOver = false;
            _modeFirst = 0;
            _modeShuffle = 0;
            ShowPanel(_panelEntry);
            SetStatus("输入昵称后创建或加入房间");
            if (_nickInput != null && !string.IsNullOrEmpty(_lastNickname)) _nickInput.text = _lastNickname;
        }

        public override void OnExit()
        {
            if (_handedOver) return; // 连接已移交战斗屏（BattleEntry.Client）——不拆
            TeardownSession();
        }

        // ============================================================ 连接建立 ============================================================

        /// <summary>房主开房：起会话服务器 → 回环客户端入座（椅位 0）。</summary>
        private void StartHostRoom()
        {
            if (_server != null) return;
            int port = ParsePort(_portInput, DefaultPort);
            var server = new NetSessionServer("p2p", msg => Debug.Log(msg));
            try
            {
                server.Start(port);
            }
            catch (Exception ex) // 端口被占（SocketException）等
            {
                SetStatus($"开房失败：{ex.Message}（端口被占？换一个再试）");
                return;
            }

            BeginHostSession(server,
                $"本机地址（告诉对手）：{string.Join(" / ", LocalIpv4Addresses().Select(ip => $"{ip}:{server.Port}"))}");
            ConnectLoopback(server.Port); // 房主回环入座（后台线程连接，避免 UI 卡帧）
            StartBeacon(); // 局域网信标：开房即可被发现；绑定失败不阻塞建房（手输 IP 兜底）
        }

        /// <summary>客人加入：直连房主 IP:端口（任意空位）。</summary>
        private void ConnectGuest()
        {
            StopScan(); // 手动路径出加入面板前收掉扫描线程
            string ip = _ipInput != null ? _ipInput.text.Trim() : "";
            if (string.IsNullOrEmpty(ip))
            {
                SetStatus("请输入房主 IP 地址");
                return;
            }
            int port = ParsePort(_port2Input, DefaultPort);
            BeginGuestSession($"目标：{ip}:{port}");
            ConnectAsync(ip, port, wantSeat: -1).Forget();
        }

        private async UniTaskVoid ConnectAsync(string host, int port, int wantSeat)
        {
            var nickname = ResolveNickname();
            SetStatus($"连接 {host}:{port} …");
            var client = new NetGameClient();
            bool ok;
            try
            {
                ok = await UniTask.RunOnThreadPool(() =>
                {
                    try { client.Connect(host, port); return true; }
                    catch (Exception) { return false; }
                });
            }
            catch (Exception)
            {
                ok = false;
            }
            if (!ok)
            {
                SetStatus($"连接失败：{host}:{port}（不可达/房未开）");
                client.Close();
                if (_isHost) StopHostServer();
                return;
            }

            AdoptClientAndJoin(client, wantSeat, nickname);
        }

        /// <summary>客户端就位：接管连接 → 发协议 JoinRoom（首条消息——分座由 NetRoom 定）。
        /// TCP 直连与 TapTap 传输（已就绪再构造）两路共用。public=Tide.TapTap 联机段跨程序集同路调用。</summary>
        public void AdoptClientAndJoin(NetGameClient client, int wantSeat, string nickname)
        {
            _client = client;
            WireClientEvents(client);
            NetClientRuntime.Bind(client);
            client.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom
            {
                RoomId = "",
                WantSeat = wantSeat,
                Nickname = nickname,
            });
            SetStatus("已连接，等待进房…");
        }

        /// <summary>昵称解析：输入框 → TapTap 登录名（桥装段兜底） → 随机（局域网/TapTap 两路共用）。
        /// public=Tide.TapTap 联机段跨程序集同路调用。</summary>
        public string ResolveNickname()
        {
            var nickname = _nickInput != null ? _nickInput.text.Trim() : "";
            var fallback = _tapSection?.FallbackNickname();
            if (string.IsNullOrEmpty(nickname) && !string.IsNullOrEmpty(fallback)) nickname = fallback;
            if (string.IsNullOrEmpty(nickname)) nickname = $"玩家{UnityEngine.Random.Range(1000, 9999)}";
            _lastNickname = nickname;
            return nickname;
        }

        // ============================================================ 局域网自动发现（2026-10-28：房主 LanRoomBeacon 应答 + 客人 LanRoomScanner 探测） ============================================================

        /// <summary>房主侧：开局域网信标（payload 快照由主线程在开房/房间态变化时刷新，信标线程只读）。</summary>
        private void StartBeacon()
        {
            ResolveNickname(); // 确保昵称快照在案（空则随机）
            UpdateBeaconPayload();
            _beacon = new LanRoomBeacon(() => _beaconPayload);
            _beacon.Start(); // UDP/TCP 全绑定失败也继续建房——手输 IP 路径不受影响
        }

        /// <summary>信标 payload 快照刷新（主线程调用；格式 ver|roomId|hostNick|tcpPort|seats|phase）。</summary>
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
            _beaconPayload = $"1|p2p|{_lastNickname}|{_server.Port}|{seated}/2|{phase}";
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

        private void RestartScan()
        {
            if (_scanner == null) return;
            _scanner.StartScan();
            _autoJoinArmed = true;
            _scanSignature = null;
            if (_scanLabel != null) _scanLabel.text = "正在搜索局域网房间…";
        }

        private void StopScan()
        {
            _scanLoopRunning = false;
            _scanner?.StopScan();
        }

        /// <summary>扫描刷新循环（主线程 UniTask 轮询；面板离开/StopScan 即退，重入由 _scanLoopRunning 挡）。</summary>
        private async UniTaskVoid RunScanLoopAsync()
        {
            if (_scanLoopRunning) return;
            _scanLoopRunning = true;
            while (_scanLoopRunning && _panelJoin != null && _panelJoin.gameObject.activeSelf)
            {
                var rooms = _scanner != null ? _scanner.Snapshot() : new List<LanRoomEntry>();
                UpdateScanUi(rooms);
                await UniTask.Delay(400);
            }
            _scanLoopRunning = false;
        }

        private void UpdateScanUi(List<LanRoomEntry> rooms)
        {
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
                label = "未发现房间——确认房主已开房后「重新扫描」，或在下方手动输入 IP";
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
            for (int i = _roomList.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_roomList.GetChild(i).gameObject);
            for (int i = 0; i < rooms.Count; i++)
            {
                var entry = rooms[i];
                UiKit.Button("room-" + i, _roomList, $"加入 {entry.HostNick} 的房间（{entry.Seats}）",
                    () => ConnectToRoom(entry.Host, entry.TcpPort, entry.HostNick), UiStyle.BtnPrimary);
            }
        }

        /// <summary>按发现结果直连（与手动 ConnectGuest 同尾段，免去手输 IP）。</summary>
        private void ConnectToRoom(string host, int port, string hostNick)
        {
            StopScan();
            SetStatus($"发现房间（{(string.IsNullOrEmpty(hostNick) ? host : hostNick)}），正在加入 {host}:{port} …");
            BeginGuestSession($"目标（自动发现）：{host}:{port}");
            ConnectAsync(host, port, wantSeat: -1).Forget();
        }

        // ============================================================ TapTap 桥装段与跨程序集会话助手（Tide.TapTap 的 TapLobbySection 经此驱动本屏；跨程序集须 public） ============================================================

        /// <summary>TapTap 面板 RectTransform 登记（桥装段 Build 时交回——纳入 ShowPanel 换页集）。</summary>
        public void RegisterTapPanel(RectTransform panel) => _panelTap = panel;

        /// <summary>房主会话就位（LAN/TapTap 建房共用尾段）：登记服务器→房间面板→加 AI 可用→地址展示。</summary>
        public void BeginHostSession(NetSessionServer server, string addressText)
        {
            _isHost = true;
            _server = server;
            ShowPanel(_panelRoom);
            if (_addAiNode != null) _addAiNode.gameObject.SetActive(true);
            if (_addressLabel != null) _addressLabel.text = addressText;
            SetStatus("房间已创建，等待对手加入…");
            RefreshRoomUi(); // 就绪层初刷（模式钮=房主可见、开始钮未就绪置灰）
        }

        /// <summary>客人会话就位（LAN 直连/TapTap 加入共用尾段）：房间面板→地址展示→加 AI 收起。</summary>
        public void BeginGuestSession(string addressText)
        {
            _isHost = false;
            ShowPanel(_panelRoom);
            if (_addressLabel != null) _addressLabel.text = addressText;
            if (_addAiNode != null) _addAiNode.gameObject.SetActive(false);
            RefreshRoomUi(); // 就绪层初刷（准备钮=客人可见）
        }

        /// <summary>房主回环入座（127.0.0.1，后台线程连接避免 UI 卡帧；TapTap 建房同路）。</summary>
        public void ConnectLoopback(int port) => ConnectAsync("127.0.0.1", port, wantSeat: 0).Forget();

        public void ShowEntryPanel() => ShowPanel(_panelEntry);
        public void ShowRoomPanel() => ShowPanel(_panelRoom);
        public void ShowTapPanel() => ShowPanel(_panelTap);

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

            // 房主开局 → 选卡组相位（先手已定+30s 倒计时）：双方移交战场走五段式（掷币+选卡组+握手）
            if (room.Phase == (int)NetRoomPhase.DeckSubmit && !_handedOver)
            {
                HandOverToBattle(room);
                return;
            }

            var phaseText = room.Phase == (int)NetRoomPhase.Waiting ? "等待就座"
                : room.Phase == (int)NetRoomPhase.Playing ? "对战中" : "已结束";
            int mySeat = MySeatOf(room);
            var opp = room.Players != null ? room.Players.FirstOrDefault(p => p != null && p.Seat != mySeat) : null;
            string oppText = "";
            if (opp != null && opp.Seat >= 0)
                oppText = opp.Connected ? $"对手：{opp.Nickname}" + (room.GuestReady ? "（已准备）" : "（未准备）") : "对手未就座";
            SetStatus($"[{phaseText}] {oppText}");
            RefreshRoomUi();
        }

        /// <summary>房间面板就绪层刷新：模式展示（服务器回显权威）/房主开始钮门槛/客人准备钮。</summary>
        private void RefreshRoomUi()
        {
            if (_roomState == null) return;
            int first = ((_roomState.FirstMoveMode % 3) + 3) % 3;
            int shuffle = ((_roomState.ShuffleMode % 2) + 2) % 2;
            if (_modesLabel != null)
                _modesLabel.text = $"模式：{FirstMoveLabels[first].Replace("先后手：", "")} · {ShuffleLabels[shuffle].Replace("洗牌：", "")}";
            if (_modeFirstLabel != null) _modeFirstLabel.text = FirstMoveLabels[first];
            if (_modeShuffleLabel != null) _modeShuffleLabel.text = ShuffleLabels[shuffle];

            if (_modeFirstBtn != null) _modeFirstBtn.gameObject.SetActive(_isHost);
            if (_modeShuffleBtn != null) _modeShuffleBtn.gameObject.SetActive(_isHost);
            if (_startBtn != null)
            {
                _startBtn.gameObject.SetActive(_isHost);
                int mySeat = MySeatOf(_roomState);
                var opp = _roomState.Players != null
                    ? _roomState.Players.FirstOrDefault(p => p != null && p.Seat != mySeat && p.Seat >= 0) : null;
                _startBtn.interactable = opp != null && opp.Connected && _roomState.GuestReady;
            }
            if (_readyBtn != null)
            {
                _readyBtn.gameObject.SetActive(!_isHost);
                var lbl = _readyBtn.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = _roomState.GuestReady ? "取消准备" : "准备";
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
                SetStatus("AI 填位失败：本地卡表为空");
                return;
            }
            _aiFiller.Start(_server.Port);
            SetStatus("AI 对手正在入座…");
        }

        /// <summary>本端椅位（昵称对账；-1=未命中——回环客户端未就座完成等瞬态）。</summary>
        private static int MySeatOf(MsgRoomState room)
        {
            if (room?.Players == null) return -1;
            foreach (var p in room.Players)
                if (p != null && p.Connected && p.Nickname == _lastNickname) return p.Seat;
            return -1;
        }

        // ============================================================ 开局移交 / 收尾 ============================================================

        /// <summary>选卡组相位到达（房主已开局）：双方移交战场走五段式（掷币演出+战场选卡组+握手开战）。
        /// 先手表现参数在此解析：掷币模式=照常 3D 演出（结果=服务器权威注入）；固定先后手=快进直出浮字。</summary>
        private void HandOverToBattle(MsgRoomState room)
        {
            if (_client == null) return;
            _handedOver = true;
            UnwireClientEvents(_client); // 战斗屏接手下行事件（含 manifest 校验——选卡组确认后到）
            int mySeat = MySeatOf(room);
            bool coinMode = room.FirstMoveMode == 0;
            bool playerFirst = coinMode
                ? room.FirstSeatThisMatch == mySeat
                : room.FirstMoveMode == 1 ? mySeat == 0 : mySeat == 1;
            BattleEntry.Mode = BattleMode.Network;
            BattleEntry.Client = _client;
            BattleEntry.Nickname = _lastNickname;
            BattleEntry.StagedOpening = true; // 网络五段式（2026-10-28：掷币+战场选卡组）
            BattleEntry.NetCoinToss = coinMode;
            BattleEntry.NetPlayerFirst = playerFirst;
            SetStatus("房主已开局——进入战场（掷币+选卡组）");
            TideLog.Info($"[Lobby] 移交战场：mySeat={mySeat} 先手椅位={room.FirstSeatThisMatch} 掷币演出={coinMode} playerFirst={playerFirst}");
            BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
        }

        private void OnServerError(MsgError err)
            => SetStatus($"被拒（{err.Context}）：{err.Reason}" + (err.Context == "DeckSubmit" ? "——请重选卡组提交" : ""));

        private void OnDisconnected()
        {
            if (_handedOver) return; // 战斗屏接管后断线由战斗屏收口
            SetStatus("连接已断开");
            ResetToEntry();
        }

        private void QuitRoom()
        {
            _handedOver = false;
            TeardownSession();
            ResetToEntry();
        }

        private void ResetToEntry()
        {
            _roomState = null;
            ShowPanel(_panelEntry);
        }

        /// <summary>拆本屏持有的会话（连接+房主服务器+AI 填位+TapTap 房间态）。</summary>
        private void TeardownSession()
        {
            if (_client != null)
            {
                UnwireClientEvents(_client);
                NetClientRuntime.Unbind(_client);
                _client.Close();
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

        private void ShowPanel(RectTransform panel)
        {
            if (_panelEntry != null) _panelEntry.gameObject.SetActive(ReferenceEquals(panel, _panelEntry));
            if (_panelHost != null) _panelHost.gameObject.SetActive(ReferenceEquals(panel, _panelHost));
            if (_panelJoin != null) _panelJoin.gameObject.SetActive(ReferenceEquals(panel, _panelJoin));
            if (_panelTap != null) _panelTap.gameObject.SetActive(ReferenceEquals(panel, _panelTap));
            if (_panelRoom != null) _panelRoom.gameObject.SetActive(ReferenceEquals(panel, _panelRoom));

            // 加入面板进出即扫描起停（2026-10-28 局域网自动发现）
            if (ReferenceEquals(panel, _panelJoin)) StartScan();
            else StopScan();
        }

        /// <summary>状态行刷新（Tide.TapTap 联机段共用）。public=跨程序集同路调用。</summary>
        public void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text;
            Debug.Log($"[Lobby] {text}");
        }

        private static int ParsePort(TMP_InputField input, int fallback)
        {
            if (input == null) return fallback;
            return int.TryParse(input.text.Trim(), out var port) && port > 0 && port < 65536 ? port : fallback;
        }

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
