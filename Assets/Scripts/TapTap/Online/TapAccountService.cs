using System;
using System.Threading.Tasks;
using CardCore;
using Cysharp.Threading.Tasks;
using TapSDK.Core;
using TapSDK.Login;
using UnityEngine;

namespace SynergyUI.Online
{
    /// <summary>登录态快照（防腐层——不外泄 TapSDK 类型，联机/存档侧只碰本类型）。</summary>
    public sealed class TapProfile
    {
        public string OpenId;
        public string UnionId;
        public string Name;
        public string AvatarUrl;
    }

    /// <summary>
    /// TapTap 登录门面（2026-10-23 登录接入）：主菜单登录入口 + 启动静默续登的单一登录态源。
    /// 与联机侧 TapLobbyController.EnsureLoginAndConnectAsync 互不依赖——那边自带
    /// Init+静默登录（TapTapSDK 重复 Init 属设计内场景：状态机代际号处理二次会话，串行安全），
    /// 这边先登好的账号缓存会被那边的静默路径直接复用。
    /// 线程纪律：Init 终态回调由 SDK 保证主线程；Task await 自主线程发起、经 Unity
    /// SynchronizationContext 回主线程；超时分支（WhenAny 败给 Delay 的计时线程）显式
    /// SwitchToMainThread 收口——OnChanged 恒主线程派发，订阅方可直接碰 UI。
    /// </summary>
    public static class TapAccountService
    {
        /// <summary>初始化终态等待上限（gatekeeper 网络请求）——超时按失败处理，登录入口可重试。</summary>
        private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(10);

        public static TapProfile Current { get; private set; }
        public static bool IsLoggedIn => Current != null && !string.IsNullOrEmpty(Current.OpenId);

        /// <summary>登录入口可用性：配置缺失 / WebGL / 编辑器（授权需真机 TapTap 客户端或 WebView）=false。</summary>
        public static bool IsSupported
        {
            get
            {
                if (!TapOnlineConfig.IsConfigured) return false;
                var p = Application.platform;
                return p != RuntimePlatform.WebGLPlayer
                    && p != RuntimePlatform.WindowsEditor
                    && p != RuntimePlatform.OSXEditor
                    && p != RuntimePlatform.LinuxEditor;
            }
        }

        /// <summary>登录态变化（登录成功/登出）。主线程派发。</summary>
        public static event Action OnChanged;

        static TapAccountService()
        {
            // 对账桥（2026-10-23 双服务并存期）：云存档侧 CloudSave.TapAccountService（静默
            // basic_info，无 UI）登出/换号时同步清本侧——防主菜单徽章残留已登录假态。
            // 反向（本侧登录→那边）不必桥：同一 SDK 账号缓存，那边 EnsureLoginAsync 每次
            // 重取自愈。两服务合并为单源后本桥退役（见当日会话汇报协调项）。
            CloudSave.TapAccountService.OnAccountChanged += () =>
            {
                if (!CloudSave.TapAccountService.LoggedIn && IsLoggedIn)
                {
                    Current = null;
                    OnChanged?.Invoke();
                }
            };
        }

        private static readonly TaskCompletionSource<int> InitTcs = new TaskCompletionSource<int>();
        private static bool _initCalled;
        private static bool _busy;

        /// <summary>首次进主菜单预热：Init + 静默续登（有缓存账号则无感登录；无缓存不弹任何 UI）。</summary>
        public static void Warmup() => WarmupInternal().Forget();

        /// <summary>登录（主菜单按钮）：静默优先，无缓存拉起 TapTap 授权。
        /// 返回 null=成功（或已登录），否则=用户可读错误文案。</summary>
        public static async UniTask<string> LoginAsync()
        {
            if (!IsSupported) return "TapTap 登录未配置或当前平台不支持";
            if (IsLoggedIn) return null;
            if (_busy) return "正在登录 TapTap，请稍候…";
            _busy = true;
            try
            {
                var fail = await InitAndWaitAsync();
                if (fail != null) return fail;
                var account = await TapTapLogin.Instance.GetCurrentTapAccount();
                if (account == null || string.IsNullOrEmpty(account.openId))
                    account = await TapTapLogin.Instance.LoginWithScopes(
                        new[] { TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE });
                if (account == null || string.IsNullOrEmpty(account.openId))
                    return "TapTap 登录未完成（已取消）";
                ApplyAccount(account);
                return null;
            }
            catch (TaskCanceledException)
            {
                return "已取消 TapTap 登录";
            }
            catch (TapException ex)
            {
                return $"TapTap 登录失败（{ex.Code}）：{ex.Message}";
            }
            catch (Exception ex)
            {
                return $"TapTap 登录异常：{ex.Message}";
            }
            finally { _busy = false; }
        }

        /// <summary>登出（只清本地缓存与登录态，不退出 TapTap 客户端）。</summary>
        public static void Logout()
        {
            if (!IsLoggedIn) return;
            Current = null;
            try { TapTapLogin.Instance.Logout(); } catch (Exception) { }
            TideLog.Info("[TapAccount] 已登出 TapTap");
            OnChanged?.Invoke();
        }

        private static async UniTaskVoid WarmupInternal()
        {
            if (!IsSupported || IsLoggedIn) return;
            try
            {
                var fail = await InitAndWaitAsync();
                if (fail != null)
                {
                    TideLog.Warn($"[TapAccount] 预热止于初始化（点登录时重试）：{fail}");
                    return;
                }
                var account = await TapTapLogin.Instance.GetCurrentTapAccount();
                if (account != null && !string.IsNullOrEmpty(account.openId))
                    ApplyAccount(account); // 静默续登成功——OnChanged 已带日志
            }
            catch (Exception ex)
            {
                TideLog.Warn($"[TapAccount] 静默预热失败（不影响游戏）：{ex.Message}");
            }
        }

        /// <summary>Init（幂等——进程内只发起一次，TrySetResult 吸收重复终态）+ 等终态。
        /// 返回 null=就绪，否则=失败文案。</summary>
        private static async UniTask<string> InitAndWaitAsync()
        {
            if (!_initCalled)
            {
                _initCalled = true;
                TapTapSDK.Init(new TapTapSdkOptions
                {
                    clientId = TapOnlineConfig.ClientId,
                    clientToken = TapOnlineConfig.ClientToken,
                    region = TapTapRegionType.CN,
                    enableLog = Application.isEditor,
                });
                // 静态类进程级存续——注册表强引用无泄漏面（SDK 注释的 Remove 义务针对短命对象）
                TapTapSDK.AddInitCallback(new InitCallback(code => InitTcs.TrySetResult(code)));
            }
            var done = await Task.WhenAny(InitTcs.Task, Task.Delay(InitTimeout));
            await UniTask.SwitchToMainThread(); // 超时分支在计时线程续跑——收口回主线程
            if (done != InitTcs.Task) return "TapTap 服务初始化超时（网络不通？稍后再试）";
            var result = InitTcs.Task.Result;
            if (result != 0) return $"TapTap 服务初始化失败（错误码 {result}）";
            return null;
        }

        private static void ApplyAccount(TapTapAccount account)
        {
            Current = new TapProfile
            {
                OpenId = account.openId,
                UnionId = account.unionId,
                Name = string.IsNullOrEmpty(account.name) ? "TapTap玩家" : account.name,
                AvatarUrl = account.avatar ?? "",
            };
            TideLog.Info($"[TapAccount] 已登录 TapTap（openId={Current.OpenId}，玩家={Current.Name}）");
            OnChanged?.Invoke();
        }

        /// <summary>Init 终态桥（成功=0/失败=错误码）。SDK 保证回调主线程；注册晚于终态会立即补发。</summary>
        private sealed class InitCallback : ITapInitCallback
        {
            private readonly Action<int> _report;
            public InitCallback(Action<int> report) => _report = report;
            public void OnInitSuccess() => _report(0);
            public void OnInitFail(int errorCode, string errorMsg) => _report(errorCode);
        }
    }
}
