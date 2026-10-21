using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using CardCore;
using CardCore.Network;
using Cysharp.Threading.Tasks;
using TMPro;
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

        private RectTransform _panelEntry, _panelHost, _panelJoin, _panelRoom;
        private TMP_InputField _nickInput, _portInput, _ipInput, _port2Input;
        private TMP_Text _statusLabel, _addressLabel, _deckPickLabel;
        private RectTransform _addAiNode;

        private NetGameClient _client;
        private NetSessionServer _server; // 房主侧（自建自管，OnExit/离开时停机）
        private NetAiFiller _aiFiller;
        private bool _isHost;
        private bool _deckSubmitted;
        private bool _handedOver; // 已移交战斗屏：OnExit 不拆连接
        private MsgRoomState _roomState;

        // 卡组选择（循环制）：0=随机卡组，1..N=已保存卡组
        private List<DeckData> _savedDecks;
        private int _deckIndex;

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
            UiKit.Button("btn-lobby-back", entryBtns, "返回主菜单");

            // ---- 房主面板 ----
            _panelHost = UiKit.Column("panel-host", Root);
            var portRow = UiKit.Row("row-port", _panelHost);
            UiKit.Label("lbl-port", portRow, "监听端口：");
            _portInput = UiKit.InputField("input-port", portRow, "端口", DefaultPort.ToString(), width: 120f);
            UiKit.Button("btn-host-start", _panelHost, "开房", StartHostRoom, UiStyle.BtnPrimary);
            UiKit.Button("btn-host-back", _panelHost, "返回");

            // ---- 加入面板 ----
            _panelJoin = UiKit.Column("panel-join", Root);
            var ipRow = UiKit.Row("row-ip", _panelJoin);
            UiKit.Label("lbl-ip", ipRow, "房主 IP：");
            _ipInput = UiKit.InputField("input-ip", ipRow, "例如 192.168.1.100", width: 240f);
            var port2Row = UiKit.Row("row-port2", _panelJoin);
            UiKit.Label("lbl-port2", port2Row, "端口：");
            _port2Input = UiKit.InputField("input-port2", port2Row, "端口", DefaultPort.ToString(), width: 120f);
            UiKit.Button("btn-join-connect", _panelJoin, "连接", ConnectGuest, UiStyle.BtnPrimary);
            UiKit.Button("btn-join-back", _panelJoin, "返回");

            // ---- 房间面板 ----
            _panelRoom = UiKit.Column("panel-room", Root);
            _addressLabel = UiKit.Label("lbl-address", _panelRoom, "", 13, wrap: true);
            _statusLabel = UiKit.Label("lbl-status", _panelRoom, "", 14, UiStyle.ToastGreen, wrap: true);
            var deckRow = UiKit.Row("row-deck", _panelRoom);
            var deckPick = UiKit.Button("btn-deck-pick", deckRow, "卡组：随机卡组", CycleDeckPick);
            _deckPickLabel = deckPick != null ? deckPick.GetComponentInChildren<TMP_Text>(true) : null;
            UiKit.Button("btn-deck-submit", deckRow, "提交卡组", SubmitDeck, UiStyle.BtnPrimary);
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
            _deckSubmitted = false;
            _deckIndex = 0;
            _savedDecks = null;
            ShowPanel(_panelEntry);
            SetStatus("输入昵称后创建或加入房间");
            if (_nickInput != null && !string.IsNullOrEmpty(_lastNickname)) _nickInput.text = _lastNickname;
            RefreshDeckPickLabel();
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

            _isHost = true;
            _server = server;
            ShowPanel(_panelRoom);
            if (_addAiNode != null) _addAiNode.gameObject.SetActive(true);
            if (_addressLabel != null)
                _addressLabel.text =
                    $"本机地址（告诉对手）：{string.Join(" / ", LocalIpv4Addresses().Select(ip => $"{ip}:{server.Port}"))}";
            SetStatus("房间已创建，等待对手加入…");

            // 房主回环入座（Connect 同步口在后台线程跑，避免 UI 卡帧）
            ConnectAsync("127.0.0.1", server.Port, wantSeat: 0).Forget();
        }

        /// <summary>客人加入：直连房主 IP:端口（任意空位）。</summary>
        private void ConnectGuest()
        {
            string ip = _ipInput != null ? _ipInput.text.Trim() : "";
            if (string.IsNullOrEmpty(ip))
            {
                SetStatus("请输入房主 IP 地址");
                return;
            }
            int port = ParsePort(_port2Input, DefaultPort);
            _isHost = false;
            ShowPanel(_panelRoom);
            if (_addressLabel != null) _addressLabel.text = $"目标：{ip}:{port}";
            if (_addAiNode != null) _addAiNode.gameObject.SetActive(false);
            ConnectAsync(ip, port, wantSeat: -1).Forget();
        }

        private async UniTaskVoid ConnectAsync(string host, int port, int wantSeat)
        {
            var nickname = _nickInput != null ? _nickInput.text.Trim() : "";
            if (string.IsNullOrEmpty(nickname)) nickname = $"玩家{UnityEngine.Random.Range(1000, 9999)}";
            _lastNickname = nickname;

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

        /// <summary>下行事件接线（Pump 在主线程分派——回调里直接碰 UI 安全）。</summary>
        private void WireClientEvents(NetGameClient client)
        {
            client.OnRoomState += OnRoomState;
            client.OnManifest += OnManifest;
            client.OnError += OnServerError;
            client.OnDisconnected += OnDisconnected;
        }

        private void UnwireClientEvents(NetGameClient client)
        {
            client.OnRoomState -= OnRoomState;
            client.OnManifest -= OnManifest;
            client.OnError -= OnServerError;
            client.OnDisconnected -= OnDisconnected;
        }

        // ============================================================ 房间态 ============================================================

        private void OnRoomState(MsgRoomState room)
        {
            _roomState = room;
            var phaseText = room.Phase == (int)NetRoomPhase.Waiting ? "等待就座"
                : room.Phase == (int)NetRoomPhase.DeckSubmit ? "选卡组"
                : room.Phase == (int)NetRoomPhase.Playing ? "对战中"
                : "已结束";
            var opp = room.Players != null ? room.Players.FirstOrDefault(p => p != null && p.Seat == 1) : null;
            string oppText = opp == null ? "" : opp.Connected ? $"对手：{opp.Nickname}" : "对手未就座";
            SetStatus($"[{phaseText}] {oppText}");

            if (room.Phase == (int)NetRoomPhase.DeckSubmit && !_deckSubmitted)
                EnsureSavedDecks();
        }

        /// <summary>已保存卡组懒加载（进选卡阶段时刷新一次；空列表也有"随机卡组"兜底）。</summary>
        private void EnsureSavedDecks()
        {
            if (_savedDecks != null) return;
            _savedDecks = DeckSerializer.LoadAll();
            if (_deckIndex > _savedDecks.Count) _deckIndex = 0;
            RefreshDeckPickLabel();
        }

        /// <summary>循环选卡组：随机 → 存档1 → 存档2 → … → 随机。</summary>
        private void CycleDeckPick()
        {
            EnsureSavedDecks();
            _deckIndex = (_deckIndex + 1) % (_savedDecks.Count + 1);
            RefreshDeckPickLabel();
        }

        private void RefreshDeckPickLabel()
        {
            if (_deckPickLabel == null) return;
            string name = _deckIndex == 0 || _savedDecks == null || _savedDecks.Count == 0
                ? "随机卡组"
                : _savedDecks[_deckIndex - 1].name;
            _deckPickLabel.text = $"卡组：{name}";
        }

        private void SubmitDeck()
        {
            if (_client == null || _client.Disconnected) return;
            if (_roomState != null && _roomState.Phase != (int)NetRoomPhase.DeckSubmit)
            {
                SetStatus("等双方就座后才能提交卡组");
                return;
            }
            EnsureSavedDecks();

            string[] cardIds;
            string skillCardId;
            string deckName;
            if (_deckIndex == 0 || _savedDecks.Count == 0)
            {
                // LoadAll 前 30（验证器 DeckIds 同口径——LoadPlayPool 滤池含效果断链卡，DeckSubmit 闭包校验会拒）
                var pool = CardCatalog.LoadAll();
                cardIds = pool.Select(c => c.ID).Distinct().Take(30).ToArray();
                skillCardId = HeroSkillSystem.AutoPickSkillCard(pool);
                deckName = "随机卡组";
            }
            else
            {
                var deck = _savedDecks[_deckIndex - 1];
                if (deck == null || deck.cardIds == null || deck.cardIds.Count == 0)
                {
                    SetStatus("所选卡组为空——换一套或先在卡组界面保存");
                    return;
                }
                cardIds = deck.cardIds.ToArray();
                skillCardId = string.IsNullOrEmpty(deck.skillCardId) ? null : deck.skillCardId;
                deckName = deck.name;
            }

            _deckSubmitted = true;
            _deckSubmittedDeckIds = cardIds;
            _client.Send(NetworkMessageType.DeckSubmit, new MsgDeckSubmit
            {
                DeckName = deckName,
                CardIds = cardIds,
                Digest = NetMatchHandshake.ComputeDeckDigest(cardIds),
                SkillCardId = skillCardId,
            });
            SetStatus($"已提交卡组「{deckName}」，等待对手与开局…");
        }

        /// <summary>房主加 AI 对手（回环填位，测试用——对房间就是普通客户端）。</summary>
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

        // ============================================================ 开局移交 / 收尾 ============================================================

        private string[] _deckSubmittedDeckIds; // 提交时的卡组（清单回显对账用）

        private void OnManifest(MsgMatchManifest manifest)
        {
            if (_client == null) return;
            if (!NetMatchHandshake.VerifyMatchManifest(manifest, _deckSubmittedDeckIds, out var reason))
            {
                // 己方回显/摘要对不上：本地中止（服务器不可信——防御分支，正常链路不达）
                SetStatus($"开局清单校验失败：{reason}");
                TideLog.Error($"[Lobby] MatchManifest 校验失败：{reason}");
                QuitRoom();
                return;
            }

            _handedOver = true;
            UnwireClientEvents(_client); // 战斗屏接手下行事件
            BattleEntry.Mode = BattleMode.Network;
            BattleEntry.Client = _client;
            BattleEntry.Nickname = _lastNickname;
            BattleEntry.StagedOpening = false; // 网络局走战斗屏网络分支（非本地五段式）
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
            _deckSubmitted = false;
            _roomState = null;
            ShowPanel(_panelEntry);
        }

        /// <summary>拆本屏持有的会话（连接+房主服务器+AI 填位）。</summary>
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
            StopHostServer();
            _deckSubmitted = false;
            _roomState = null;
        }

        private void StopHostServer()
        {
            if (_server == null) return;
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
            if (_panelRoom != null) _panelRoom.gameObject.SetActive(ReferenceEquals(panel, _panelRoom));
        }

        private void SetStatus(string text)
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
