using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Network;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 匹配界面（阶段三，2026-09-24）——连接栏 + 左房右态：
    /// 连接（IP:端口+昵称）→ LobbyHello 进大厅 → 房间列表（创建/加入/自动匹配/AI 填位）
    /// → 双方就座后自动提交所选卡组 → MatchManifest 下发即接续对战界面
    /// （连接经 BattleEntry.Client 移交——BattleScreen 消费后接管关闭，本屏不再碰）。
    ///
    /// 服务器宿主：编辑器菜单 Tools/大厅服务器（本进程，单房串行——GameCore 进程单例约束）
    /// 或另一台机器 batchmode NetLobbyHost.Main；单人测试流程=本机开服 → 创建房间 → AI 填位 → 开局。
    /// </summary>
    public sealed class MatchScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/Match";

        private NetGameClient _net;
        private bool _handedOff;   // 连接已移交对战界面（OnExit 不关闭）
        private bool _deckSent;    // 本房间已提交过卡组（防重发）
        private string _myNick = "";

        private MsgLobbyState _lobby;
        private MsgRoomState _room;
        private MsgMatchManifest _manifest;

        private TextField _hostField;
        private TextField _nickField;
        private TextField _roomNameField;
        private IntegerField _portField;
        private ScrollView _roomsList;
        private ScrollView _statusZone;
        private DropdownField _decksDropdown;
        private Label _toast;
        private Button _btnConnect;
        private Button _btnAutoMatch;

        private bool Connected => _net != null && !_net.Disconnected;
        private bool InRoom => Connected && _room != null
            && _room.Players != null && _room.Players.Any(p => p.Connected && p.Nickname == _myNick);

        // 自动匹配按钮是开关（重复发送=取消排队）——本地记录期望态供按钮文案
        private bool _lastAutoMatch;

        public override void OnEnter()
        {
            _hostField = Q<TextField>("field-host");
            _nickField = Q<TextField>("field-nickname");
            _roomNameField = Q<TextField>("field-roomname");
            _portField = Q<IntegerField>("field-port");
            _roomsList = Q<ScrollView>("list-rooms");
            _statusZone = Q<ScrollView>("status-zone");
            _decksDropdown = Q<DropdownField>("dropdown-decks");
            _toast = Q<Label>("lbl-toast");
            _btnConnect = Q<Button>("btn-connect");
            _btnAutoMatch = Q<Button>("btn-automatch");

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

            UIBinder.BindButton(Root, "btn-back", () => Manager.Back());
            UIBinder.BindButton(Root, "btn-connect", OnConnectToggle);
            UIBinder.BindButton(Root, "btn-create", OnCreateRoom);
            UIBinder.BindButton(Root, "btn-automatch", OnAutoMatch);
            UIBinder.BindButton(Root, "btn-ai", OnAddAi);

            RefreshDecksDropdown();
            RefreshRooms();
            RefreshStatus();

            // 客户端泵：UITK 调度驱动（主线程回调——下行事件直接刷 UI）
            Root.schedule.Execute(NetPump).Every(30);
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

            var host = string.IsNullOrWhiteSpace(_hostField.value) ? "127.0.0.1" : _hostField.value.Trim();
            int port = _portField.value > 0 ? _portField.value : 7777;
            _myNick = string.IsNullOrWhiteSpace(_nickField.value) ? "player" + UnityEngine.Random.Range(100, 999) : _nickField.value.Trim();

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
            var name = string.IsNullOrWhiteSpace(_roomNameField.value) ? $"{_myNick}的房间" : _roomNameField.value.Trim();
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
                var deckName = _decksDropdown?.value;
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
            BattleEntry.Host = string.IsNullOrWhiteSpace(_hostField.value) ? "127.0.0.1" : _hostField.value.Trim();
            BattleEntry.Port = _portField.value > 0 ? _portField.value : 7777;
            BattleEntry.Nickname = _myNick;
            BattleEntry.Client = _net;
            ShowToast(manifest.OwnSeat == 0 ? "对局开始：我方先手" : "对局开始：我方后手");
            Manager.Show<BattleScreen>();
        }

        // ======================================== 刷新 ========================================

        private void NetPump()
        {
            _net?.Pump();
        }

        private void RefreshAll()
        {
            RefreshRooms();
            RefreshStatus();
        }

        private void RefreshRooms()
        {
            _roomsList.Clear();
            if (_lobby == null || _lobby.Rooms == null || _lobby.Rooms.Length == 0)
            {
                var empty = new Label(Connected ? "（暂无房间——创建一个或排队自动匹配）" : "（未连接）");
                empty.AddToClassList("hint");
                _roomsList.Add(empty);
                return;
            }
            foreach (var room in _lobby.Rooms)
            {
                var captured = room;
                var row = new VisualElement();
                row.AddToClassList("list-row");

                var name = new Label(string.IsNullOrEmpty(captured.Name) ? captured.RoomId : captured.Name);
                name.AddToClassList("list-row__name");
                name.style.flexGrow = 1;
                row.Add(name);

                var players = captured.Nicknames != null && captured.Nicknames.Length > 0
                    ? string.Join(" vs ", captured.Nicknames)
                    : "—";
                var meta = new Label($"{PhaseZh(captured.Phase)} · {captured.PlayerCount}/2{(captured.SpectatorCount > 0 ? $" · 观战{captured.SpectatorCount}" : "")} · {players}");
                meta.AddToClassList("list-row__meta");
                row.Add(meta);

                var join = new Button(() =>
                {
                    if (!EnsureConnected()) return;
                    _net.Send(NetworkMessageType.LobbyJoinRoom, new MsgLobbyJoinRoom { RoomId = captured.RoomId });
                }) { text = "加入" };
                join.AddToClassList("btn");
                join.AddToClassList("btn--mini");
                join.SetEnabled(Connected && captured.Phase == (int)NetRoomPhase.Waiting && captured.PlayerCount < 2 && !InRoom);
                row.Add(join);
                _roomsList.Add(row);
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
            _statusZone.Clear();

            AddStatusLine($"连接：{(Connected ? "已连接" : "未连接")}");
            if (!Connected)
            {
                var hint = new Label("单人测试：先在编辑器菜单 Tools/大厅服务器 启动本机服务器 → 连接 → 创建房间 → +AI 对手填位 → 自动开局");
                hint.AddToClassList("hint");
                hint.style.whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal;
                _statusZone.Add(hint);
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
                var hint = new Label("等待对手——真人加入、自动匹配配对，或点「+ AI 对手填位」单人开局");
                hint.AddToClassList("hint");
                hint.style.whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal;
                _statusZone.Add(hint);
            }
            if (_deckSent && _manifest == null)
                AddStatusLine("卡组已提交——等待双方就绪开局");
            if (_manifest != null)
                AddStatusLine($"对局开始（引擎座位 {_manifest.OwnSeat}）——已移交对战界面");
        }

        private void AddStatusLine(string text)
        {
            var line = new Label(text);
            line.AddToClassList("list-row__meta");
            line.style.whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal;
            _statusZone.Add(line);
        }

        private void RefreshDecksDropdown()
        {
            if (_decksDropdown == null) _decksDropdown = Q<DropdownField>("dropdown-decks");
            var names = DeckSerializer.LoadAll().Select(d => d.name).ToList();
            _decksDropdown.choices = names;
            if (names.Count > 0 && string.IsNullOrEmpty(_decksDropdown.value))
                _decksDropdown.SetValueWithoutNotify(names[0]);
        }

        private void ShowToast(string message)
        {
            _toast.text = message;
        }
    }
}
