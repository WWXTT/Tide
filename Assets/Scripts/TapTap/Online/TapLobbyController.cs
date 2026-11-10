using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCore;
using Cysharp.Threading.Tasks;
using TapSDK.Core;
using TapSDK.Login;
using TapSDK.OnlineBattle;

namespace SynergyUI.Online
{
    /// <summary>
    /// TapTap 联机 SDK 胶水（2026-10-23）：登录（静默优先，未登录拉起）→ Connect 对战服务 →
    /// 建房/房间列表/加入/退出。UI 层（LobbyScreen 的 TapTap 面板）只碰本口，不直接摸 SDK。
    /// 错误统一以文案返回（登录口=返回值 null 表成功；房间三口=返回 null 后读 LastError），
    /// TapException 换算可读信息。
    /// </summary>
    public static class TapLobbyController
    {
        // S1-B5：SDK 调用（Task）无超时——挂起一次，建房/刷新/加入/退房全被 _tapBusy 锁死。
        // 统一 15s 超时（Task.AsUniTask() 转换后挂 UniTask.Timeout，超时抛 TimeoutException）
        private static readonly TimeSpan SdkTimeout = TimeSpan.FromSeconds(15);

        private static bool _busy;
        private static bool _roomStale; // 传输关闭时未显式退房——下次连接前先退净，防残留房间态

        public static string SelfPlayerId { get; private set; }
        public static string LoginName { get; private set; } = "";
        public static bool IsServiceConnected => !string.IsNullOrEmpty(SelfPlayerId);

        /// <summary>传输侧断开时标记房间态可能残留（终局/异常退出路径——Close 无异步口）。</summary>
        public static void MarkRoomStale() => _roomStale = true;

        /// <summary>
        /// 登录 + 连接对战服务（幂等——已连接直接过）。返回 null=成功，否则=用户可读错误文案。
        /// </summary>
        public static async UniTask<string> EnsureLoginAndConnectAsync()
        {
            if (!TapOnlineConfig.IsConfigured)
                return "TapTap 联机未配置：请在 Scripts/TapTap/Online/TapOnlineConfig.cs 填入开发者后台 ClientId/ClientToken";
            if (IsServiceConnected) return null;
            if (_busy) return "正在登录 TapTap，请稍候…";
            _busy = true;
            try
            {
                // 初始化竞态修复（2026-11-09 A3）：进程内只允许一处 TapTapSDK.Init——原先这里
                // 自行 Init 后立刻取账号，与启动链（TapCloudSaveBootstrap/账号服务预热）的 Init
                // 相撞，重复 Init 重置 SDK 状态导致 GetCurrentTapAccount 报「SDK not initialized」。
                // 改走账号服务的幂等 Init+终态等待（EnsureReadyAsync），就绪后再登录。
                var initFail = await TapAccountService.EnsureReadyAsync();
                if (initFail != null) return initFail;

                // 静默续登优先（SDK 有账号缓存），无账号才拉起 TapTap 登录
                var account = await TapTapLogin.Instance.GetCurrentTapAccount();
                if (account == null || string.IsNullOrEmpty(account.openId))
                    account = await TapTapLogin.Instance.LoginWithScopes(
                        new[] { TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE });
                if (account == null || string.IsNullOrEmpty(account.openId))
                    return "TapTap 登录未完成（取消或失败）";
                LoginName = string.IsNullOrEmpty(account.name) ? "TapTap玩家" : account.name;

                TapBattleRouter.EnsureRegistered();
                try
                {
                    SelfPlayerId = await TapTapOnlineBattle.Connect().AsUniTask().Timeout(SdkTimeout);
                }
                catch (TimeoutException)
                {
                    SelfPlayerId = null;
                    return "TapTap 连接超时（15s）——请检查网络后重试";
                }
                if (string.IsNullOrEmpty(SelfPlayerId))
                    return "连接 TapTap 对战服务失败";
                if (_roomStale)
                {
                    // 上一次联机未经 LeaveRoom 收口（终局直退等）：退净再进，防 SDK 残留房间态
                    _roomStale = false;
                    try { await TapTapOnlineBattle.LeaveRoom(); } catch (Exception) { }
                }
                TideLog.Info($"[TapLobby] 已连接对战服务（playerId={SelfPlayerId}，玩家={LoginName}）");
                return null;
            }
            catch (TapException ex)
            {
                SelfPlayerId = null;
                return $"TapTap 联机失败（{ex.Code}）：{ex.Message}";
            }
            catch (Exception ex)
            {
                SelfPlayerId = null;
                return $"TapTap 联机异常：{ex.Message}";
            }
            finally { _busy = false; }
        }

        /// <summary>最近一次 Create/GetList/Join 失败/超时的用户可读文案（对应 *Async 返回 null 时读；
        /// 成功时清空）。S2-AOT 回退：S1 的 (T,string) 双载荷返回是值类型泛型实例化——
        /// HybridCLR 真机可能 MissingMethodException（同 HotUpdateLoader.cs AOT 锚实录：
        /// 元组是值类型不共享），故回退单载荷 + 本侧通道传文案。</summary>
        public static string LastError { get; private set; } = "";

        /// <summary>段侧（TapLobbySection）补写失败文案——控制器之外的失败
        ///（如本地组合服务器启动异常）也走同一 LastError 通道。</summary>
        public static void SetLastError(string text) => LastError = text;

        /// <summary>密码 → customProperties JSON（S6b：SDK 不校验密码——随房间元数据广播，
        /// 真门禁=本地 NetRoom 的 JoinRoom.Password 校验；空密码=空串不发）。</summary>
        private static string PasswordPropsJson(string password) =>
            string.IsNullOrEmpty(password)
                ? ""
                : $"{{\"pwd\":\"{password.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"}}";

        /// <summary>创建房间（房主；登录内置——未登录自动走登录链）。失败/超时返回 null（读 LastError）；
        /// 15s 超时（S1-B5）；密码写 roomConfig.customProperties（S6b）。</summary>
        public static async UniTask<RoomInfo> CreateRoomAsync(string roomName, string password)
        {
            var loginErr = await EnsureLoginAndConnectAsync();
            if (loginErr != null) { LastError = loginErr; return null; }
            try
            {
                var room = await TapTapOnlineBattle.CreateRoom(new CreateRoomConfig
                {
                    roomConfig = new RoomConfig
                    {
                        maxPlayerCount = 2,
                        type = TapOnlineConfig.RoomType,
                        name = roomName,
                        customProperties = PasswordPropsJson(password), // S6b：密码随房间元数据
                    },
                    playerConfig = new PlayerConfig(),
                }).AsUniTask().Timeout(SdkTimeout);
                if (room == null) { LastError = "创建 TapTap 房间失败（SDK 空返回）"; return null; }
                LastError = null;
                return room;
            }
            catch (TimeoutException)
            {
                LastError = "创建 TapTap 房间超时（15s）——请检查网络后重试";
                return null;
            }
            catch (Exception ex)
            {
                TideLog.Error($"[TapLobby] 创建房间失败：{ex.Message}");
                LastError = $"创建 TapTap 房间失败：{ex.Message}";
                return null;
            }
        }

        /// <summary>拉房间列表（RoomType 过滤本游戏公开房间；登录内置）。失败/超时返回 null（读 LastError）；
        /// 成功恒非空（空表=正常）；15s 超时（S1-B5）。</summary>
        public static async UniTask<List<RoomBasicInfo>> GetRoomListAsync()
        {
            var loginErr = await EnsureLoginAndConnectAsync();
            if (loginErr != null) { LastError = loginErr; return null; }
            try
            {
                var data = await TapTapOnlineBattle.GetRoomList(TapOnlineConfig.RoomType, 0, 20)
                    .AsUniTask().Timeout(SdkTimeout);
                LastError = null;
                return data?.rooms ?? new List<RoomBasicInfo>();
            }
            catch (TimeoutException)
            {
                LastError = "拉取房间列表超时（15s）——请检查网络后重试";
                return null;
            }
            catch (Exception ex)
            {
                TideLog.Error($"[TapLobby] 拉取房间列表失败：{ex.Message}");
                LastError = $"拉取房间列表失败：{ex.Message}";
                return null;
            }
        }

        /// <summary>按房间 ID 加入（客人；登录内置；SDK 加入无密码参——密码经协议 JoinRoom 由
        /// 本地 NetRoom 校验）。失败/超时返回 null（读 LastError）；15s 超时（S1-B5）。</summary>
        public static async UniTask<RoomInfo> JoinRoomAsync(string roomId)
        {
            var loginErr = await EnsureLoginAndConnectAsync();
            if (loginErr != null) { LastError = loginErr; return null; }
            try
            {
                var room = await TapTapOnlineBattle.JoinRoom(roomId, new PlayerConfig())
                    .AsUniTask().Timeout(SdkTimeout);
                if (room == null) { LastError = "加入失败：房间可能已满或已开局"; return null; }
                if (string.IsNullOrEmpty(room.ownerId)) { LastError = "房间数据异常（无房主）"; return null; }
                LastError = null;
                return room;
            }
            catch (TimeoutException)
            {
                LastError = "加入房间超时（15s）——请检查网络后重试";
                return null;
            }
            catch (Exception ex)
            {
                TideLog.Error($"[TapLobby] 加入房间失败：{ex.Message}");
                LastError = $"加入失败：{ex.Message}";
                return null;
            }
        }

        /// <summary>退房 + 断开对战服务（离开联机入口/终局收口；吞错——含超时）。
        /// 两腿各挂 15s 超时（S1-B5）：SDK 挂起会把退房按钮的 _tapBusy 永久锁死。</summary>
        public static async UniTask LeaveAndDisconnectAsync()
        {
            SelfPlayerId = null;
            _roomStale = false;
            try { await TapTapOnlineBattle.LeaveRoom().AsUniTask().Timeout(SdkTimeout); } catch (Exception) { }
            try { await TapTapOnlineBattle.Disconnect().AsUniTask().Timeout(SdkTimeout); } catch (Exception) { }
        }
    }
}
