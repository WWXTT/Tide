using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Network;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UInputField = TMPro.TMP_InputField;

namespace SynergyUI
{
    /// <summary>
    /// 匹配界面（2026-10-01 UGUI 移植，语义与 UITK 版一一对应）——连接栏 + 左房右态：
    /// 连接（IP:端口+昵称）→ LobbyHello 进大厅 → 房间列表（创建/加入/自动匹配/AI 填位）
    /// → 双方就座后自动提交所选卡组 → MatchManifest 下发即接续对战界面
    /// （连接经 BattleEntry.Client 移交——BattleScreen 消费后接管关闭，本屏不再碰）。
    ///
    /// 服务器宿主：Play 模式自动启动的本机大厅（NetLobbyHost，单房串行——GameCore 进程单例约束）
    /// 或另一台机器 batchmode NetLobbyHost.Main；单人测试流程=直接连接 → 创建房间 → AI 填位 → 开局。
    /// </summary>
    public sealed class MatchScreen : UIScreen
    {
        private NetGameClient _net;
        private bool _handedOff;   // 连接已移交对战界面（OnExit 不关闭）
        private bool _deckSent;    // 本房间已提交过卡组（防重发）
        private string _myNick = "";

        private MsgLobbyState _lobby;
        private MsgRoomState _room;
        private MsgMatchManifest _manifest;

        private UInputField _hostField;
        private UInputField _nickField;
        private UInputField _roomNameField;
        private UInputField _portField;
        private UiKit.Scroll _roomsList;
        private UiKit.Scroll _statusZone;
        private UiKit.Dropdown _decksDropdown;
        private TMP_Text _toast;
        private Button _btnConnect;
        private Button _btnAutoMatch;
        private RectTransform _overlay;

        // 自动匹配按钮是开关（重复发送=取消排队）——本地记录期望态供按钮文案
        private bool _lastAutoMatch;

        // 客户端泵节流（旧版 root.schedule.Execute(NetPump).Every(30) → Updater+累积器）
        private float _pumpAccumMs;

        private bool Connected => _net != null && !_net.Disconnected;
        private bool InRoom => Connected && _room != null
            && _room.Players != null && _room.Players.Any(p => p.Connected && p.Nickname == _myNick);

        protected override void Build()
        {
            Root = UiKit.Screen("match", Parent);
            _overlay = UiKit.Overlay("overlay", Root);

            // ---- 顶栏：返回 + 标题 + toast ----
            var top = UiKit.Row("topbar", Root, spacing: 8f);
            UiKit.Button("btn-back", top, "← 返回", () => Manager.Back());
            UiKit.Label("title", top, "联机大厅", UiStyle.HeaderSize, UiStyle.TextPrimary,
                TextAnchor.LowerLeft, FontStyle.Bold);
            _toast = UiKit.Toast("lbl-toast", top);
            UiKit.Size(_toast, fw: 1f);

            // ---- 连接栏 ----
            var connect = UiKit.Panel("connect-panel", Root, pad: 10f, spacing: 8f);
            var connectRow = UiKit.Row("row", connect, spacing: 8f);
            _hostField = UiKit.InputField("field-host", connectRow, "IP（默认 127.0.0.1）", "127.0.0.1", width: 150f);
            _portField = UiKit.IntField("field-port", connectRow, "端口", 8090, _ => { }, width: 80f);
            _nickField = UiKit.InputField("field-nickname", connectRow, "昵称（默认随机）", "", width: 130f);
            _btnConnect = UiKit.Button("btn-connect", connectRow, "连接", OnConnectToggle, UiStyle.BtnPrimary);

            // ---- 房间操作栏 ----
            var actions = UiKit.Row("actions", Root, spacing: 8f);
            _roomNameField = UiKit.InputField("field-roomname", actions, "房间名（默认 我的房间）", "", width: 170f);
            UiKit.Button("btn-create", actions, "创建房间", OnCreateRoom);
            _btnAutoMatch = UiKit.Button("btn-automatch", actions, "自动匹配", OnAutoMatch);
            UiKit.Button("btn-ai", actions, "+ AI 对手填位", OnAddAi);
            _decksDropdown = new UiKit.Dropdown("dropdown-decks", actions, _overlay,
                new List<string>(), 0, width: 200f);

            // ---- 左房右态 ----
            var main = UiKit.Row("main", Root, spacing: 12f);
            var roomsPanel = UiKit.Panel("rooms", main, pad: 10f, spacing: 8f);
            UiKit.Size(roomsPanel, fw: 6f);
            UiKit.Label("rooms-header", roomsPanel, "房间列表", UiStyle.HeaderSize, UiStyle.TextSecondary,
                TextAnchor.LowerLeft, FontStyle.Bold);
            _roomsList = UiKit.ScrollColumn("list-rooms", roomsPanel, bg: UiStyle.ListBg);
            UiKit.Size(_roomsList.Rect.transform, fw: 1f, fh: 1f);

            var statusPanel = UiKit.Panel("status", main, pad: 10f, spacing: 8f);
            UiKit.Size(statusPanel, fw: 4f);
            UiKit.Label("status-header", statusPanel, "状态", UiStyle.HeaderSize, UiStyle.TextSecondary,
                TextAnchor.LowerLeft, FontStyle.Bold);
            _statusZone = UiKit.ScrollColumn("status-zone", statusPanel, bg: UiStyle.ListBg);
            UiKit.Size(_statusZone.Rect.transform, fw: 1f, fh: 1f);
        }

        public override void OnEnter()
        {
            // 对战结束返回本界面：上一条连接已被对战界面关闭，整体归零
            if (_net != null && _net.Disconnected)
            {
                _net = null;
                _deckSent = false;
                _room = null;
                _manifest = null;
                _lobby = null;
                _lastAutoMatch = false;
            }
            _handedOff = false;

            RefreshDecksDropdown();
            RefreshRooms();
            RefreshStatus();

            // 客户端泵（主线程回调——下行事件直接刷 UI）
            UiKit.Updater.Attach(Root, PumpFrame);
        }

        public override void OnExit()
        {
            // 未移交（用户主动离开）→ 关连接释放座位；已移交 → 对战界面接管生命周期
            if (!_handedOff)
            {
                _net?.Close();
                _net = null;
            }
        }

        private void PumpFrame()
        {
            _pumpAccumMs += Time.unscaledDeltaTime * 1000f;
            if (_pumpAccumMs < 30f) return;
            _pumpAccumMs = 0f;
            _net?.Pump();
        }

        // ======================================== 连接 ========================================

        private void OnConnectToggle()
        {
            if (Connected)
            {
                _net.Close(); // 座位/排队随断开释放（服务器 OnDisconnect 收口）
                _net = null;
                _room = null;
                _lobby = null;
                _deckSent = false;
                _manifest = null;
                _lastAutoMatch = false;
                ShowToast("已断开");
                RefreshAll();
                return;
            }

            var host = string.IsNullOrWhiteSpace(_hostField.text) ? "127.0.0.1" : _hostField.text.Trim();
            int port = int.TryParse(_portField.text, out var portValue) && portValue > 0 ? portValue : 8090;
            _myNick = string.IsNullOrWhiteSpace(_nickField.text) ? "player" + UnityEngine.Random.Range(100, 999) : _nickField.text.Trim();

            _net = new NetGameClient();
            _net.OnLobbyState += NetOnLobbyState;
            _net.OnRoomState += NetOnRoomState;
            _net.OnManifest += NetOnManifest;
            _net.OnError += NetOnError;
            _net.OnDisconnected += NetOnDisconnected;

            try
            {
                _net.Connect(host, port);
            }
            catch (Exception ex)
            {
                ShowToast($"连接失败：{ex.Message}");
                _net = null;
                return;
            }

            _net.Send(NetworkMessageType.LobbyHello, new MsgLobbyHello { Nickname = _myNick });
            ShowToast($"已连接 {host}:{port}——进入大厅");
            RefreshAll();
        }

        // ---- 命名处理器（移交连接给对战界面前可退订——lambda 无法退订）----
        private void NetOnLobbyState(MsgLobbyState state) { _lobby = state; RefreshAll(); }
        private void NetOnRoomState(MsgRoomState room) { _room = room; OnRoomChanged(); }
        private void NetOnManifest(MsgMatchManifest manifest) => OnManifest(manifest);
        private void NetOnError(MsgError err) => ShowToast($"拒绝（{err?.Context}）：{err?.Reason}");
        private void NetOnDisconnected() => ShowToast("与服务器的连接已断开");

        private void DetachNetHandlers()
        {
            if (_net == null) return;
            _net.OnLobbyState -= NetOnLobbyState;
            _net.OnRoomState -= NetOnRoomState;
            _net.OnManifest -= NetOnManifest;
            _net.OnError -= NetOnError;
            _net.OnDisconnected -= NetOnDisconnected;
        }

        // ======================================== 大厅操作 ========================================

        private void OnCreateRoom()
        {
            if (!EnsureConnected()) return;
            if (InRoom) { ShowToast("已在房间中"); return; }
            var name = string.IsNullOrWhiteSpace(_roomNameField.text) ? $"{_myNick}的房间" : _roomNameField.text.Trim();
            _net.Send(NetworkMessageType.LobbyCreateRoom, new MsgLobbyCreateRoom { RoomName = name });
        }

        private void OnAutoMatch()
        {
            if (!EnsureConnected()) return;
            _lastAutoMatch = !_lastAutoMatch;
            _net.Send<object>(NetworkMessageType.LobbyAutoMatch, null); // 重复发送=取消排队（空载荷）
            ShowToast(_lastAutoMatch ? "已加入匹配队列" : "已取消匹配");
            RefreshStatus();
        }

        private void OnAddAi()
        {
            if (!EnsureConnected()) return;
            if (!InRoom) { ShowToast("先进房（创建/加入）才能 AI 填位"); return; }
            _net.Send(NetworkMessageType.LobbyAddAi, new MsgLobbyAddAi { RoomId = _room.RoomId, Nickname = "" });
            ShowToast("AI 对手正在入座…");
        }

        private bool EnsureConnected()
        {
            if (Connected) return true;
            ShowToast("未连接服务器");
            return false;
        }

        // ======================================== 房间状态机 ========================================

        private void OnRoomChanged()
        {
            // 双方就座 → 自动提交所选卡组（一次性；被拒由 Error 回显）
            if (_room != null && _room.Phase == (int)NetRoomPhase.DeckSubmit && !_deckSent)
            {
                var deckName = _decksDropdown?.Value;
                var deck = string.IsNullOrEmpty(deckName) ? null : DeckSerializer.Load(deckName);
                if (deck == null || deck.cardIds == null || deck.cardIds.Count == 0)
                {
                    ShowToast("所选卡组无效——请先在卡组构建中保存一套卡组");
                    return;
                }
                var ids = deck.cardIds.ToArray();
                _net.Send(NetworkMessageType.DeckSubmit, new MsgDeckSubmit
                {
                    DeckName = deck.name,
                    CardIds = ids,
                    Digest = NetMatchHandshake.ComputeDeckDigest(ids),
                });
                _deckSent = true;
                ShowToast($"已提交卡组「{deck.name}」（{ids.Length} 张）——等待对手/开局");
            }
            RefreshAll();
        }

        private void OnManifest(MsgMatchManifest manifest)
        {
            if (manifest == null) return;
            _manifest = manifest;

            // 接续对战界面：先退订本屏处理器（防对战期间刷新已脱离 UI / 二次 Manifest 重复入对战屏），
            // 再移交连接（BattleEntry.Client 一次性消费），本屏不再管理其生命周期
            DetachNetHandlers();
            _handedOff = true;
            BattleEntry.Mode = BattleMode.Network;
            BattleEntry.Host = string.IsNullOrWhiteSpace(_hostField.text) ? "127.0.0.1" : _hostField.text.Trim();
            BattleEntry.Port = int.TryParse(_portField.text, out var portValue) && portValue > 0 ? portValue : 8090;
            BattleEntry.Nickname = _myNick;
            BattleEntry.Client = _net;
            ShowToast(manifest.OwnSeat == 0 ? "对局开始：我方先手" : "对局开始：我方后手");
            Manager.Show<BattleScreen>();
        }

        // ======================================== 刷新 ========================================

        private void RefreshAll()
        {
            RefreshRooms();
            RefreshStatus();
        }

        private void RefreshRooms()
        {
            ClearContent(_roomsList.Content);
            if (_lobby == null || _lobby.Rooms == null || _lobby.Rooms.Length == 0)
            {
                var empty = UiKit.Label("empty", _roomsList.Content,
                    Connected ? "（暂无房间——创建一个或排队自动匹配）" : "（未连接）",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(empty, fw: 1f);
                return;
            }
            foreach (var room in _lobby.Rooms)
            {
                var captured = room;
                var row = UiKit.Row("room-row", _roomsList.Content, spacing: 8f, pad: 6f);
                UiKit.BgRow(row);

                var name = UiKit.Label("name", row,
                    string.IsNullOrEmpty(captured.Name) ? captured.RoomId : captured.Name,
                    UiStyle.BodySize, UiStyle.TextBody);
                UiKit.Size(name, fw: 1f);

                var players = captured.Nicknames != null && captured.Nicknames.Length > 0
                    ? string.Join(" vs ", captured.Nicknames)
                    : "—";
                var meta = UiKit.Label("meta", row,
                    $"{PhaseZh(captured.Phase)} · {captured.PlayerCount}/2{(captured.SpectatorCount > 0 ? $" · 观战{captured.SpectatorCount}" : "")} · {players}",
                    UiStyle.SmallSize, UiStyle.TextFaint);
                UiKit.Size(meta, w: Mathf.Max(meta.preferredWidth, 220f));

                var join = UiKit.MiniButton("join", row, "加入", () =>
                {
                    if (!EnsureConnected()) return;
                    _net.Send(NetworkMessageType.LobbyJoinRoom, new MsgLobbyJoinRoom { RoomId = captured.RoomId });
                });
                join.interactable = Connected && captured.Phase == (int)NetRoomPhase.Waiting
                    && captured.PlayerCount < 2 && !InRoom;
            }
        }

        private static string PhaseZh(int phase)
        {
            switch ((NetRoomPhase)phase)
            {
                case NetRoomPhase.Waiting: return "等待中";
                case NetRoomPhase.DeckSubmit: return "提交卡组";
                case NetRoomPhase.Playing: return "对战中";
                case NetRoomPhase.Finished: return "已结束";
                default: return phase.ToString();
            }
        }

        private void RefreshStatus()
        {
            ClearContent(_statusZone.Content);

            AddStatusLine($"连接：{(Connected ? "已连接" : "未连接")}");
            if (!Connected)
            {
                var hint = UiKit.Label("hint", _statusZone.Content,
                    "单人测试：本机大厅服务器随 Play 模式自动启动 → 连接 → 创建房间/自动匹配 → +AI 对手填位 → 自动开局",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(hint, fw: 1f);
                return;
            }

            AddStatusLine($"昵称：{_myNick}");
            if (_lobby != null)
                AddStatusLine($"匹配队列：{_lobby.QueuedCount} 人{(_lastAutoMatch && !InRoom ? "（排队中）" : "")}");
            if (_room != null && _room.Players != null)
            {
                var seats = string.Join(" / ", _room.Players.Select(p =>
                    p.Connected ? (string.IsNullOrEmpty(p.Nickname) ? "?" : p.Nickname) : "空位"));
                AddStatusLine($"房间：{seats} · {PhaseZh(_room.Phase)}{(_room.SpectatorCount > 0 ? $" · 观战{_room.SpectatorCount}" : "")}");
            }
            if (InRoom && _room.Phase == (int)NetRoomPhase.Waiting && _room.Players.Count(p => p.Connected) < 2)
            {
                var hint = UiKit.Label("hint", _statusZone.Content,
                    "等待对手——真人加入、自动匹配配对，或点「+ AI 对手填位」单人开局",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(hint, fw: 1f);
            }
            if (_deckSent && _manifest == null)
                AddStatusLine("卡组已提交——等待双方就绪开局");
            if (_manifest != null)
                AddStatusLine($"对局开始（引擎座位 {_manifest.OwnSeat}）——已移交对战界面");
        }

        private void AddStatusLine(string text)
        {
            var line = UiKit.Label("line", _statusZone.Content, text,
                UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            UiKit.Size(line, fw: 1f);
        }

        private void RefreshDecksDropdown()
        {
            var names = DeckSerializer.LoadAll().Select(d => d.name).ToList();
            // 保持现选（仍在列表内时）；无选或失效则取第一套
            int index = names.IndexOf(_decksDropdown.Value);
            if (index < 0) index = names.Count > 0 ? 0 : -1;
            _decksDropdown.SetOptions(names, index);
        }

        private void ShowToast(string message)
        {
            _toast.text = message;
        }

        /// <summary>清空滚动列内容（RefreshAll 全量重建语义，同旧版 ScrollView.Clear）。
        /// 先摘父再 Destroy——Destroy 延迟帧末，不摘父则同帧重建时旧行仍占布局位。</summary>
        private static void ClearContent(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                child.SetParent(null);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}
