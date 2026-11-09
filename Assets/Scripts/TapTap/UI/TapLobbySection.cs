using System;
using System.Collections.Generic;
using CardCore.Network;
using Cysharp.Threading.Tasks;
using TapSDK.OnlineBattle;
using TMPro;
using Tide.HotUpdate;
using UnityEngine;

namespace SynergyUI.Online
{
    /// <summary>
    /// 联机屏的 TapTap 段（2026-10-23 接入，2026-10-24 预编译隔离自 LobbyScreen 迁入本程序集）：
    /// 入口钮 + TapTap 面板（登录/建房/房间列表浏览加入）。面板 RectTransform 经
    /// LobbyScreen.RegisterTapPanel 交回，换页仍走 LobbyScreen.ShowPanel 统一口；
    /// 建房/加入经跨程序集助手（BeginHostSession/BeginGuestSession/ConnectLoopback/AdoptClientAndJoin）
    /// 驱动屏本体，与局域网两路径完全同构。本地单机风味本类不编译——联机屏只剩局域网两面板。
    /// </summary>
    public sealed class TapLobbySection : ITapLobbySection
    {
        private readonly LobbyScreen _screen;
        private TMP_Text _tapStatusLabel;
        private RectTransform _tapRoomListNode;
        private List<RoomBasicInfo> _tapRooms;
        private bool _tapBusy;

        public TapLobbySection(LobbyScreen screen) => _screen = screen;

        public void Build(RectTransform root, RectTransform entryButtonsRow)
        {
            // ---- 入口钮（插在「加入房间」与「返回主菜单」之间——按钮序与原版一致）----
            UiKit.Button("btn-taptap", entryButtonsRow, "TapTap 联机（房间列表）",
                onClick: () => { _screen.ShowTapPanel(); RefreshTapRooms().Forget(); });

            // ---- TapTap 联机面板（进面板自动登录；建房=房主，房间列表=客人加入）----
            var panelTap = UiKit.Column("panel-taptap", root);
            _tapStatusLabel = UiKit.Label("lbl-tap-status", panelTap, "进入即自动登录 TapTap…", 13, wrap: true);
            var tapBtns = UiKit.Row("row-tap-btns", panelTap);
            UiKit.Button("btn-tap-create", tapBtns, "创建房间（房主）",
                onClick: () => StartTapHost().Forget(), bg: UiStyle.BtnPrimary);
            UiKit.Button("btn-tap-refresh", tapBtns, "刷新房间列表",
                onClick: () => RefreshTapRooms().Forget());
            UiKit.Button("btn-tap-back", tapBtns, "返回",
                onClick: () => _screen.ShowEntryPanel());
            UiKit.Label("lbl-tap-rooms", panelTap, "公开房间（点条目加入；空=暂无，可自己创建）：", 13);
            _tapRoomListNode = UiKit.ScrollColumn("list-tap-rooms", panelTap).Content;

            _screen.RegisterTapPanel(panelTap); // 纳入 LobbyScreen.ShowPanel 换页集
        }

        /// <summary>TapTap 建房（房主）：登录 → CreateRoom → 组合传输（TCP 回环 + TapTap 客人腿）
        /// → 本进程 NetSessionServer（权威）→ 回环入座椅位 0（与局域网开房同一路径）。</summary>
        private async UniTaskVoid StartTapHost()
        {
            if (_tapBusy) { SetTapStatus("处理中，请稍候…"); return; }
            _tapBusy = true;
            try
            {
                SetTapStatus("TapTap 登录中…");
                var err = await TapLobbyController.EnsureLoginAndConnectAsync();
                if (err != null) { SetTapStatus(err); return; }

                var nickname = _screen.ResolveNickname();
                var room = await TapLobbyController.CreateRoomAsync(
                    string.IsNullOrEmpty(nickname) ? "潮汐对战房" : $"{nickname}的房间");
                if (room == null) { SetTapStatus("创建 TapTap 房间失败（详见 Console）"); return; }

                // 组合传输：TCP 腿=127.0.0.1 回环（房主自己 + AI 填位，现有路径零改动）；
                // TapTap 腿=远端客人（定向消息，快照按座位过滤后送达）
                var server = new NetSessionServer("taptap", msg => Debug.Log(msg),
                    host: new CompositeTransportHost(new NetTcpHost(), new TapBattleTransportHost()));
                try { server.Start(0); }
                catch (Exception ex)
                {
                    SetTapStatus($"本地会话服务器启动失败：{ex.Message}");
                    await TapLobbyController.LeaveAndDisconnectAsync();
                    return;
                }

                _screen.BeginHostSession(server,
                    $"TapTap 房间「{room.name}」已创建——对手在「TapTap 联机」的房间列表点选加入");
                _screen.ConnectLoopback(server.Port); // 房主回环入座（后台线程连接，避免 UI 卡帧）
            }
            finally { _tapBusy = false; }
        }

        /// <summary>刷新 TapTap 公开房间列表（RoomType 过滤本游戏房间）。</summary>
        private async UniTaskVoid RefreshTapRooms()
        {
            if (_tapBusy) { SetTapStatus("处理中，请稍候…"); return; }
            _tapBusy = true;
            try
            {
                SetTapStatus("登录并拉取房间列表…");
                var err = await TapLobbyController.EnsureLoginAndConnectAsync();
                if (err != null) { SetTapStatus(err); return; }
                var rooms = await TapLobbyController.GetRoomListAsync();
                if (rooms == null) { SetTapStatus("拉取房间列表失败（详见 Console）"); return; }
                _tapRooms = rooms;
                RebuildTapRoomList();
                SetTapStatus(rooms.Count == 0
                    ? "暂无公开房间——可点「创建房间」当房主"
                    : $"共 {rooms.Count} 个房间（点条目加入；满员房不显示）");
            }
            finally { _tapBusy = false; }
        }

        /// <summary>加入指定 TapTap 房间（客人）：JoinRoom 成功 → TapClientTransport（对端=房主）
        /// → 协议 JoinRoom 进房分座。</summary>
        private async UniTaskVoid JoinTapRoom(RoomBasicInfo room)
        {
            if (_tapBusy || room == null || string.IsNullOrEmpty(room.id)) return;
            _tapBusy = true;
            try
            {
                SetTapStatus($"加入「{room.name}」…");
                var err = await TapLobbyController.EnsureLoginAndConnectAsync();
                if (err != null) { SetTapStatus(err); return; }
                var joined = await TapLobbyController.JoinRoomAsync(room.id);
                if (joined == null) { SetTapStatus("加入失败：房间可能已满或已开局"); return; }
                if (string.IsNullOrEmpty(joined.ownerId)) { SetTapStatus("房间数据异常（无房主）"); return; }

                var transport = new TapClientTransport(joined.ownerId, TapLobbyController.SelfPlayerId);
                var client = new NetGameClient();
                client.Connect(transport); // 传输就绪（JoinRoom 已成功）——后续统一走协议 JoinRoom
                _screen.BeginGuestSession($"TapTap 房间「{joined.name}」");
                _screen.AdoptClientAndJoin(client, wantSeat: -1, _screen.ResolveNickname());
            }
            finally { _tapBusy = false; }
        }

        /// <summary>重建房间列表行（刷新时整组重排——条目≤20 无虚拟化必要；满员房跳过）。</summary>
        private void RebuildTapRoomList()
        {
            if (_tapRoomListNode == null) return;
            for (int i = _tapRoomListNode.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_tapRoomListNode.GetChild(i).gameObject);
            var rooms = _tapRooms ?? new List<RoomBasicInfo>();
            int row = 0;
            foreach (var r in rooms)
            {
                if (r == null || string.IsNullOrEmpty(r.id)) continue;
                if (r.maxPlayerCount > 0 && r.playerCount >= r.maxPlayerCount) continue; // 满员
                var captured = r;
                UiKit.Button($"btn-tap-room-{row}", _tapRoomListNode,
                    $"{captured.name}（{captured.playerCount}/{captured.maxPlayerCount}）",
                    onClick: () => JoinTapRoom(captured).Forget());
                row++;
            }
        }

        private void SetTapStatus(string text)
        {
            if (_tapStatusLabel != null) _tapStatusLabel.text = text;
            Debug.Log($"[Lobby·TapTap] {text}");
        }

        /// <summary>拆会话收口（LobbyScreen.TeardownSession 调用）：退房+断服务
        ///（对局移交后不走这里——战斗屏接管期间保持连接）。</summary>
        public void Teardown()
        {
            if (TapLobbyController.IsServiceConnected || _tapBusy)
                TapLobbyController.LeaveAndDisconnectAsync().Forget();
            _tapRooms = null;
            _tapBusy = false;
        }

        /// <summary>昵称兜底：TapTap 登录名（未登录为空——上层继续随机兜底）。</summary>
        public string FallbackNickname() => TapLobbyController.LoginName;
    }
}
