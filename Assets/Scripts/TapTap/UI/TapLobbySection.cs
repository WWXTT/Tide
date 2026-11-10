using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Network;
using Cysharp.Threading.Tasks;
using Tide.HotUpdate;
using UnityEngine;

namespace SynergyUI.Online
{
    /// <summary>
    /// 联机屏的 TapTap 段（S6b 双源改版）：不再持有 UI——以 ITapLobbySection 身份供
    /// LobbyScreen 驱动（登录态/房间列表/建房/加入/退房），SDK 细节封装在 TapLobbyController。
    /// 失败通道=返回 null + TapLobbyController.LastError 可读文案（不用元组——AOT 纪律）。
    /// 本地单机风味本类不编译（联机屏只剩局域网路径）。
    /// </summary>
    public sealed class TapLobbySection : ITapLobbySection
    {
        private readonly LobbyScreen _screen;

        public TapLobbySection(LobbyScreen screen) => _screen = screen;

        public bool IsLoggedIn => !string.IsNullOrEmpty(TapLobbyController.LoginName);
        public string LoginName => TapLobbyController.LoginName;
        public string LastError => TapLobbyController.LastError;
        public string LastCreatedRoomId => TapLobbyController.LastCreatedRoomId;

        public async UniTask<string> EnsureLoginAsync()
            => await TapLobbyController.EnsureLoginAndConnectAsync();

        /// <summary>公开房间列表（S6b/S8）：滤满员；**含 ranked 房及其元数据**（customProperties
        /// 同义 JSON：ranked/fp/rate）——普通列表 UI 侧滤 Ranked，排位匹配器专用候选。
        /// 手写轻提取（我们自己写的 JSON 格式，无嵌套无转义歧义）。</summary>
        public async UniTask<List<TapRoomEntry>> ListRoomsAsync()
        {
            var rooms = await TapLobbyController.GetRoomListAsync();
            if (rooms == null) return null; // LastError 已在控制器侧
            return rooms
                .Where(r => r != null && !string.IsNullOrEmpty(r.id))
                .Where(r => !(r.maxPlayerCount > 0 && r.playerCount >= r.maxPlayerCount)) // 满员不列
                .Select(r =>
                {
                    var props = r.customProperties ?? "";
                    return new TapRoomEntry
                    {
                        RoomId = r.id,
                        Name = r.name,
                        PlayerCount = r.playerCount,
                        MaxPlayerCount = r.maxPlayerCount,
                        HasPassword = props.Contains("pwd"),
                        Ranked = props.Contains("\"ranked\":true"),
                        DeckFingerprint = PropValue(props, "fp"),
                        WinRate = int.TryParse(PropValue(props, "rate"), out var rate) ? rate : -1,
                    };
                })
                .ToList();
        }

        /// <summary>从自写 props JSON 提取字符串字段值（"key":"value"——无嵌套；找不到返回 null）。</summary>
        private static string PropValue(string json, string key)
        {
            var marker = $"\"{key}\":\"";
            int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;
            int end = json.IndexOf('"', start);
            return end > start ? json.Substring(start, end - start) : null;
        }

        /// <summary>TapTap 建房（S6b）：SDK 建房（密码写 customProperties——真门禁=NetRoom）+
        /// 本地组合传输会话服务器（TCP 回环腿=房主自己/AI 填位，TapTap 腿=远端客人；同 password）
        /// 已 Start(0)。失败返回 null（LastError 文案含本地服务器启动失败路径）。</summary>
        public async UniTask<NetSessionServer> CreateRoomAsync(string roomName, string password)
            => await CreateRoomCore(roomName, password, ranked: false, null, -1);

        /// <summary>排位建房（S8）：ranked/fp/rate 入 customProperties；本地服务器 ranked=true
        ///（双接受窗）——回环入座时 JoinRoom 也带 Ranked=true（LobbyScreen 侧 JoinOptions）。</summary>
        public async UniTask<NetSessionServer> CreateRankedRoomAsync(string roomName,
            string deckFingerprint, int winRatePermille)
            => await CreateRoomCore(roomName, null, ranked: true, deckFingerprint, winRatePermille);

        private static async UniTask<NetSessionServer> CreateRoomCore(string roomName, string password,
            bool ranked, string deckFingerprint, int winRatePermille)
        {
            var room = await TapLobbyController.CreateRoomAsync(roomName, password, ranked,
                deckFingerprint, winRatePermille);
            if (room == null) return null;
            var server = new NetSessionServer("taptap", msg => Debug.Log(msg),
                host: new CompositeTransportHost(new NetTcpHost(), new TapBattleTransportHost()),
                password: string.IsNullOrEmpty(password) ? null : password,
                ranked: ranked);
            try
            {
                server.Start(0);
            }
            catch (Exception ex)
            {
                TapLobbyController.SetLastError($"本地会话服务器启动失败：{ex.Message}");
                await TapLobbyController.LeaveAndDisconnectAsync(); // SDK 侧房间不残留
                return null;
            }
            return server; // BeginHostSession/回环入座由 LobbyScreen 收尾（两路同构）
        }

        /// <summary>加入指定房间（S6b）：SDK JoinRoom（无密码参——密码经协议 JoinRoom 由 NetRoom
        /// 校验，由 LobbyScreen 统一发送）+ TapClientTransport 就绪的客户端。失败返回 null。</summary>
        public async UniTask<NetGameClient> JoinRoomAsync(string roomId, string password)
        {
            var joined = await TapLobbyController.JoinRoomAsync(roomId);
            if (joined == null) return null;
            var transport = new TapClientTransport(joined.ownerId, TapLobbyController.SelfPlayerId);
            var client = new NetGameClient();
            client.Connect(transport); // 传输就绪（SDK JoinRoom 已成功）
            return client;
        }

        public async UniTask<string> LeaveAsync()
        {
            await TapLobbyController.LeaveAndDisconnectAsync();
            return null; // 吞错口径沿用（两腿 15s 超时兜底在控制器）
        }

        /// <summary>S6a 起 v2 三栏无独立 TapTap 面板——留空（接口兼容保留）。</summary>
        public void Build(RectTransform root, RectTransform entryButtonsRow) { }

        /// <summary>拆会话收口（LobbyScreen.TeardownSession 调）：退房+断对战服务。</summary>
        public void Teardown()
        {
            if (TapLobbyController.IsServiceConnected)
                TapLobbyController.LeaveAndDisconnectAsync().Forget();
        }

        /// <summary>昵称兜底（TapTap 登录名；未登录为空——上层继续兜底）。</summary>
        public string FallbackNickname() => TapLobbyController.LoginName;

        /// <summary>本机 TapTap openId（协议 v11 对局记录：进房自报）；未登录返回空。</summary>
        public string OpenIdOf() => TapAccountService.Current?.OpenId ?? "";
    }
}
