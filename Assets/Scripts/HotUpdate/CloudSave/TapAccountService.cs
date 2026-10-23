using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TapSDK.Login;
using UnityEngine;

namespace CloudSave
{
    /// <summary>
    /// TapTap 登录服务（2026-10-23 云存档接入·静默 basic_info 定案）：无感授权只拿
    /// openId/unionId 供云存档识别账号，不取昵称头像（不弹授权界面）。未登录时游戏完全可玩。
    /// </summary>
    public static class TapAccountService
    {
        public static TapTapAccount Current { get; private set; }
        public static bool LoggedIn => Current != null;

        /// <summary>登录态变化（主菜单云存档面板/同步服务的刷新接线点）。</summary>
        public static event Action OnAccountChanged;

        /// <summary>
        /// 确保已登录：先取缓存账号，无缓存走静默 basic_info 授权。主线程回调（轮询式等待，
        /// 见 <see cref="CloudTasks"/>）。用户取消不视为错误。
        /// </summary>
        public static async UniTask<bool> EnsureLoginAsync()
        {
            if (!TapSdkConfig.CloudSaveSupported || !TapSdkConfig.IsCredentialsReady) return false;
            try
            {
                Current = await CloudTasks.AwaitOnMain(TapTapLogin.Instance.GetCurrentTapAccount(), 30f);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TapLogin] GetCurrentTapAccount 失败（按未登录继续）：{ex.Message}");
                Current = null;
            }

            if (Current == null)
            {
                try
                {
                    Current = await CloudTasks.AwaitOnMain(
                        TapTapLogin.Instance.LoginWithScopes(new[] { TapTapLogin.TAP_LOGIN_SCOPE_BASIC_INFO }), 60f);
                    Debug.Log($"[TapLogin] 静默登录成功 openId={Current?.openId}");
                }
                catch (TaskCanceledException)
                {
                    Debug.Log("[TapLogin] 用户取消静默授权——云存档待手动登录");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TapLogin] 登录失败（云存档待手动登录）：{ex.Message}");
                }
            }

            OnAccountChanged?.Invoke();
            return LoggedIn;
        }

        /// <summary>手动登录（云存档面板按钮）——与启动链同一条静默路径。</summary>
        public static async UniTaskVoid LoginFromUiAsync()
        {
            if (LoggedIn) return;
            bool ok = await EnsureLoginAsync();
            if (ok) await CloudSyncService.StartupSyncAsync();
        }

        /// <summary>退出登录（只清本地缓存，不影响 TapTap 客户端登录态）。</summary>
        public static void Logout()
        {
            if (!LoggedIn) return;
            TapTapLogin.Instance.Logout();
            Current = null;
            CloudSyncService.OnLoggedOut();
            OnAccountChanged?.Invoke();
        }
    }

    /// <summary>
    /// SDK Task→主线程桥（HybridCLR+UniTask 环境的关键约束）：UniTask async 方法内直接
    /// await Task 会把续体落在 Task 完成线程（线程池），而 SDK 回调后的写盘/UI/管理器重置
    /// 必须在主线程。这里用主线程 PlayerLoop 轮询等待 Task 完成后再取结果——登录/云存档
    /// 均为秒级网络操作，逐帧轮询成本可忽略；超时兜底防 SDK 内部挂死占死同步位。
    /// </summary>
    internal static class CloudTasks
    {
        public static async UniTask<T> AwaitOnMain<T>(Task<T> task, float timeoutSeconds)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            float waited = 0f;
            while (!task.IsCompleted)
            {
                await UniTask.Yield();
                waited += Time.unscaledDeltaTime;
                if (waited > timeoutSeconds)
                    throw new TimeoutException($"TapSDK 调用超时（{timeoutSeconds:F0}s）");
            }
            if (task.IsFaulted) throw task.Exception.GetBaseException(); // 主线程重抛原始异常（TapException 保留 Code）
            if (task.IsCanceled) throw new TaskCanceledException(task);
            return task.Result;
        }

        public static async UniTask AwaitOnMain(Task task, float timeoutSeconds)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            float waited = 0f;
            while (!task.IsCompleted)
            {
                await UniTask.Yield();
                waited += Time.unscaledDeltaTime;
                if (waited > timeoutSeconds)
                    throw new TimeoutException($"TapSDK 调用超时（{timeoutSeconds:F0}s）");
            }
            if (task.IsFaulted) throw task.Exception.GetBaseException();
            if (task.IsCanceled) throw new TaskCanceledException(task);
        }
    }
}
