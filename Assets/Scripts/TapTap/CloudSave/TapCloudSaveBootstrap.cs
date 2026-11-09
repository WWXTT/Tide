using System;
using TapSDK.Core;
using UnityEngine;

namespace CloudSave
{
    /// <summary>
    /// TapTap SDK 初始化引导（2026-10-23 云存档接入）：HotUpdateEntry.Start 装配宿主后调用。
    /// 链路=Init(异步)→OnInitSuccess→静默登录(basic_info)→启动同步；任何一步失败只落日志，
    /// 绝不影响本地玩法（本地优先，云存档永远只是旁路）。
    /// 回调由静态单例终身持有（App 生命周期=回调生命周期，无需 RemoveInitCallback 防泄漏）。
    /// </summary>
    public static class TapCloudSaveBootstrap
    {
        private static readonly BootInitCallback Callback = new BootInitCallback();
        private static bool _initCalled;

        public static void Init()
        {
            if (!TapSdkConfig.CloudSaveSupported)
            {
                Debug.Log("[TapSdkInit] 当前平台不启用云存档（编辑器/PC），跳过初始化");
                return;
            }
            if (_initCalled) return; // HotUpdateEntry 重复装配的幂等闸
            if (!TapSdkConfig.IsCredentialsReady)
            {
                Debug.LogWarning("[TapSdkInit] TapSdkConfig.ClientId/ClientToken 未配置，云存档停用（填入凭证后生效）");
                return;
            }

            _initCalled = true;
            TapTapSDK.AddInitCallback(Callback);
            // 主线程调用（文档要求）；Init 幂等语义由 SDK 状态机承担，失败码走 OnInitFail。
            TapTapSDK.Init(new TapTapSdkOptions
            {
                clientId = TapSdkConfig.ClientId,
                clientToken = TapSdkConfig.ClientToken,
                enableLog = false, // 上线口径：关 SDK 日志
            });
            Debug.Log("[TapSdkInit] TapTapSDK.Init 已发起（TapSDK 4.11.0 core/login/cloudsave）");
        }

        private sealed class BootInitCallback : ITapInitCallback
        {
            public void OnInitSuccess()
            {
                Debug.Log("[TapSdkInit] OnInitSuccess——进入静默登录");
                RunAsync().Forget();
            }

            public void OnInitFail(int errorCode, string errorMsg)
            {
                // 1000=参数错 1001=配置错 1003=内部错（多为后台包名/签名不匹配或网络不可达）
                Debug.LogError($"[TapSdkInit] 初始化失败 code={errorCode} msg={errorMsg}——云存档停用，玩法不受影响");
            }

            private static async Cysharp.Threading.Tasks.UniTaskVoid RunAsync()
            {
                try
                {
                    bool loggedIn = await TapAccountService.EnsureLoginAsync();
                    if (!loggedIn)
                    {
                        Debug.Log("[CloudSync] 未登录（用户取消/网络失败）——云存档待主菜单手动登录");
                        return;
                    }
                    CloudSyncService.EnsureHost(); // 常驻轮询宿主（自动上传/暂停退出冲刷）
                    await CloudSyncService.StartupSyncAsync();
                }
                catch (Exception ex)
                {
                    // 兜底：链路任何未捕获异常只落日志，不向玩法抛
                    Debug.LogError($"[CloudSync] 启动链路异常：{ex}");
                }
            }
        }
    }
}
