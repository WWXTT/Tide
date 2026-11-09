using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCore;
using Cysharp.Threading.Tasks;
using TapSDK.Core;
using TapSDK.Login;
using TapSDK.OnlineBattle;
using UnityEngine;

namespace SynergyUI.Online
{
    /// <summary>
    /// TapTap 联机 SDK 胶水（2026-10-23）：登录（静默优先，未登录拉起）→ Connect 对战服务 →
    /// 建房/房间列表/加入/退出。UI 层（LobbyScreen 的 TapTap 面板）只碰本口，不直接摸 SDK。
    /// 错误统一以文案返回（null=成功），TapException 换算可读信息。
    /// </summary>
    public static class TapLobbyController
    {
        private static bool _sdkInited;
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
                if (!_sdkInited)
                {
                    TapTapSDK.Init(new TapTapSdkOptions
                    {
                        clientId = TapOnlineConfig.ClientId,
                        clientToken = TapOnlineConfig.ClientToken,
                        region = TapTapRegionType.CN,
                        enableLog = Application.isEditor,
                    });
                    _sdkInited = true;
                }

                // 静默续登优先（SDK 有账号缓存），无账号才拉起 TapTap 登录
                var account = await TapTapLogin.Instance.GetCurrentTapAccount();
                if (account == null || string.IsNullOrEmpty(account.openId))
                    account = await TapTapLogin.Instance.LoginWithScopes(
                        new[] { TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE });
                if (account == null || string.IsNullOrEmpty(account.openId))
                    return "TapTap 登录未完成（取消或失败）";
                LoginName = string.IsNullOrEmpty(account.name) ? "TapTap玩家" : account.name;

                TapBattleRouter.EnsureRegistered();
                SelfPlayerId = await TapTapOnlineBattle.Connect();
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

        /// <summary>创建房间（房主）。失败返回 null 并打日志。</summary>
        public static async UniTask<RoomInfo> CreateRoomAsync(string roomName)
        {
            try
            {
                return await TapTapOnlineBattle.CreateRoom(new CreateRoomConfig
                {
                    roomConfig = new RoomConfig
                    {
                        maxPlayerCount = 2,
                        type = TapOnlineConfig.RoomType,
                        name = roomName,
                    },
                    playerConfig = new PlayerConfig(),
                });
            }
            catch (Exception ex)
            {
                TideLog.Error($"[TapLobby] 创建房间失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>拉房间列表（RoomType 过滤本游戏公开房间）。</summary>
        public static async UniTask<List<RoomBasicInfo>> GetRoomListAsync()
        {
            try
            {
                var data = await TapTapOnlineBattle.GetRoomList(TapOnlineConfig.RoomType, 0, 20);
                return data?.rooms ?? new List<RoomBasicInfo>();
            }
            catch (Exception ex)
            {
                TideLog.Error($"[TapLobby] 拉取房间列表失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>按房间 ID 加入（客人）。失败返回 null。</summary>
        public static async UniTask<RoomInfo> JoinRoomAsync(string roomId)
        {
            try
            {
                return await TapTapOnlineBattle.JoinRoom(roomId, new PlayerConfig());
            }
            catch (Exception ex)
            {
                TideLog.Error($"[TapLobby] 加入房间失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>退房 + 断开对战服务（离开联机入口/终局收口；吞错——退不干净交给 SDK 超时）。</summary>
        public static async UniTask LeaveAndDisconnectAsync()
        {
            SelfPlayerId = null;
            _roomStale = false;
            try { await TapTapOnlineBattle.LeaveRoom(); } catch (Exception) { }
            try { await TapTapOnlineBattle.Disconnect(); } catch (Exception) { }
        }
    }
}
