using System;
using CloudSyncServiceRef = CloudSave.CloudSyncService;
using Cysharp.Threading.Tasks;
using SynergyUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloudSave
{
    /// <summary>
    /// 设置面板账号区控制器（2026-11-23 烘焙改版）：登录/退出登录/存档上传/云存档下载四钮
    ///（slot-login/slot-logout/up-cloud/down-cloud）烘焙在 MainUI.prefab panel-settings，
    /// 本类只按名绑定与填数（UI 只绑定不生成）；TapLoginBadge/CloudSaveEntry/CloudSavePanel
    /// 三件注入预制体退役。显隐门禁（2026-11-23 用户定案）=**登录后才显示退出登录与两个存档钮**，
    /// 登录钮默认显示（未登录=「登录」可点，登录成功原地换用户名；2026-11-24 用户定案：编辑器/
    /// 不支持平台不再整体隐藏——登录钮照常显示，点击经 toast 提示「未配置或平台不支持」）。
    /// 唯一整体缺席=本地单机风味（Tide.TapTap 程序集不编译、TapBridge.Module=null，由
    /// HotUpdate 侧 MainMenuScreen.BindSettingsPanel 隐藏账号区）。
    /// 云恢复完成=**原地刷新**（数据已由 GameDataReload 全量失效，重订教学事件+刷门槛即读新
    /// 数据——2026-11-22 用户定案，旧清栈回启动页链退役）。上传/下载/登录失败结果经
    /// TipPromptHost toast 反馈（旧 CloudSavePanel 面板与状态行一并退役）。
    /// 经 TapModule.OnMainMenuBuilt/Enter/Exit 驱动；本地单机风味本类不编译——入口整体缺席。
    /// 控制器随 TapModule 进程级存续、每任主菜单 Build 重绑（事件订阅在控制器方法上，跨屏重订对称）。
    /// </summary>
    public sealed class CloudSaveMenuController
    {
        private MainMenuScreen _menu;
        private RectTransform _root; // 当前主菜单根（toast/确认框挂点——Build 时刷新）

        private Button _loginButton;
        private TMP_Text _loginLabel;    // slot-login 的 label：未登录=「登录/登录中…」，已登录=用户名
        private Button _logoutButton;
        private Button _uploadButton;
        private Button _downloadButton;
        private bool _loggingIn;
        private TipPromptHost _tips;     // 上传/下载结果与游客闸提示

        /// <summary>主菜单 Build 期：账号区四钮按名绑定+登录态刷新。登录钮默认显示（2026-11-24
        /// 用户定案：不按 IsSupported 隐藏——未登录态只显示登录钮，编辑器/不支持平台点击经
        /// toast 提示不可用）；缺节点 LogError 不静默。</summary>
        public void Build(MainMenuScreen menu)
        {
            _menu = menu;
            _root = menu.Root;
            _tips = new TipPromptHost(() => _root);
            _loggingIn = false; // 上一任主菜单登录中途退屏的残留——防新屏登录钮永久禁用

            _loginButton = BindAccountButton("slot-login", OnLoginClicked);
            _loginLabel = _loginButton != null ? _loginButton.GetComponentInChildren<TMP_Text>(true) : null;
            _logoutButton = BindAccountButton("slot-logout", Logout);
            _uploadButton = BindAccountButton("up-cloud", () => UploadFromUi()); // UniTaskVoid 不直转 Action——lambda 舍返回值
            _downloadButton = BindAccountButton("down-cloud", ConfirmCloudRestore);
            RefreshAccountArea();
        }

        /// <summary>主菜单进场：云侧事件订阅 + 账号区刷新 + 启动链挂起询问补问。</summary>
        public void OnEnter(MainMenuScreen menu)
        {
            if (_menu == null) Build(menu); // Build 未走过的兜底（正常链路 Show 必先 Build）
            CloudSyncServiceRef.OnStateChanged += RefreshAccountArea;
            CloudSyncServiceRef.OnRestoreSuggested += OnCloudRestoreSuggested;
            CloudSyncServiceRef.OnRestored += OnCloudRestored;
            TapAccountService.OnAccountChanged += RefreshAccountArea;
            SynergyUI.Online.TapAccountService.OnChanged += RefreshAccountArea; // 用户名源（PUBLIC_PROFILE 侧）
            RefreshAccountArea();
            if (CloudSyncServiceRef.PendingRestorePrompt) OnCloudRestoreSuggested(); // 启动链询问时主菜单未就绪——进场补问
        }

        /// <summary>主菜单离场：退订（无面板可关——旧 CloudSavePanel 弹窗链已退役）。</summary>
        public void OnExit(MainMenuScreen menu)
        {
            CloudSyncServiceRef.OnStateChanged -= RefreshAccountArea;
            CloudSyncServiceRef.OnRestoreSuggested -= OnCloudRestoreSuggested;
            CloudSyncServiceRef.OnRestored -= OnCloudRestored;
            TapAccountService.OnAccountChanged -= RefreshAccountArea;
            SynergyUI.Online.TapAccountService.OnChanged -= RefreshAccountArea;
        }

        /// <summary>账号区显隐/文案刷新（两侧账号服务任一登录即算已登录；Build/登录态事件/
        /// 同步态事件触发）。门禁（2026-11-23 用户定案）：退出登录与两个存档钮登录后才显示，
        /// 未登录只显示登录钮；登录后 slot-login 原地换用户名。云钮可点性=凭证就绪且非同步中。</summary>
        private void RefreshAccountArea()
        {
            bool logged = TapAccountService.LoggedIn || SynergyUI.Online.TapAccountService.IsLoggedIn;
            if (_loginLabel != null)
            {
                _loginLabel.text = _loggingIn ? "登录中…"
                    : logged ? UsernameOrFallback() : "登录";
            }
            if (_logoutButton != null) _logoutButton.gameObject.SetActive(logged);
            bool cloudReady = TapSdkConfig.CloudSaveSupported && TapSdkConfig.IsCredentialsReady
                              && !CloudSyncServiceRef.Busy;
            if (_uploadButton != null)
            {
                _uploadButton.gameObject.SetActive(logged);
                _uploadButton.interactable = cloudReady;
            }
            if (_downloadButton != null)
            {
                _downloadButton.gameObject.SetActive(logged);
                _downloadButton.interactable = cloudReady;
            }
        }

        /// <summary>用户名源（联机侧 PUBLIC_PROFILE 昵称；云侧 basic_info 无昵称回落「TapTap玩家」）。</summary>
        private static string UsernameOrFallback()
        {
            var profile = SynergyUI.Online.TapAccountService.Current;
            return profile != null && !string.IsNullOrEmpty(profile.Name) ? profile.Name : "TapTap玩家";
        }

        /// <summary>账号区按钮绑定：panel-settings 烘焙节点按名查找→挂回调；缺节点 LogError（不静默）。</summary>
        private Button BindAccountButton(string name, Action onClick)
        {
            var node = UiKit.FindDeep(_root, name);
            var btn = node != null ? node.GetComponentInChildren<Button>(true) : null;
            if (btn == null)
            {
                Debug.LogError($"[MainMenu] 设置面板账号区缺按钮：{name}（检查 MainUI.prefab panel-settings）");
                return null;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        // ======================================== 登录/退出登录 ========================================

        /// <summary>登录钮：未登录才拉起授权（已登录时本钮只是用户名行，点击无操作）。</summary>
        private void OnLoginClicked()
        {
            if (_loggingIn) return;
            if (TapAccountService.LoggedIn || SynergyUI.Online.TapAccountService.IsLoggedIn) return;
            LoginFlow().Forget();
        }

        /// <summary>登录流程（原 TapLoginBadge.LoginFlow 迁入，2026-11-23 注入链退役）：
        /// 联机侧拉起授权（PUBLIC_PROFILE——拿昵称头像）；云侧（basic_info 静默）随后补登录
        /// 并启动云同步（LoginFromUiAsync=UniTaskVoid 火 Forget——登录态经事件回报，
        /// OnAccountChanged→原地刷新账号区，玩家就在账号区无需跳页）。失败经 toast 提示并回落
        /// 登录钮态（**原地停留**——云存档入口状态随事件自刷）。</summary>
        private async UniTaskVoid LoginFlow()
        {
            _loggingIn = true;
            if (_loginButton != null) _loginButton.interactable = false;
            if (_loginLabel != null) _loginLabel.text = "登录中…";
            try
            {
                if (SynergyUI.Online.TapAccountService.IsLoggedIn)
                {
                    TapAccountService.LoginFromUiAsync(); // 罕见态兜底：联机侧已登录、云侧掉队
                }
                else
                {
                    var error = await SynergyUI.Online.TapAccountService.LoginAsync();
                    if (error != null)
                    {
                        if (_tips != null) await _tips.ShowAsync(error, "知道了"); // 成功态经 OnChanged→原地刷新
                        return;
                    }
                    TapAccountService.LoginFromUiAsync();
                }
            }
            finally
            {
                _loggingIn = false;
                if (_loginButton != null) _loginButton.interactable = true;
                RefreshAccountArea(); // 授权未完成/失败时回落登录钮态（成功则事件已刷过，幂等）
            }
        }

        /// <summary>退出登录：双侧登出——先联机侧（清 PUBLIC_PROFILE 态+SDK 账号缓存；云侧
        /// OnAccountChanged 桥只清内存不清 SDK 缓存，顺序反了会残留缓存导致下次「静默续登」复活），
        /// 后云侧（清云端档案引用）→ 原地刷新（事件自动回落登录钮态）。</summary>
        private void Logout()
        {
            SynergyUI.Online.TapAccountService.Logout();
            TapAccountService.Logout();
            RefreshAccountArea();
        }

        // ======================================== 存档上传/云存档下载 ========================================

        /// <summary>「本地存档上传」：手动上传（自动轮询外的兜底口）。未登录闸兜底（按钮登录后才
        /// 显示，正常不可达）；完成经 toast 反馈（旧面板状态行退役）。</summary>
        private async UniTaskVoid UploadFromUi()
        {
            if (!TapAccountService.LoggedIn && !SynergyUI.Online.TapAccountService.IsLoggedIn)
            {
                if (_tips != null) await _tips.ShowAsync("登录 TapTap 后可使用云存档（在上方登录）", "知道了");
                return;
            }
            CloudSyncServiceRef.EnsureHost(); // 首次交互保底宿主（自动上传轮询常驻）
            bool ok = await CloudSyncServiceRef.UploadAsync("manual");
            if (_tips != null)
                await _tips.ShowAsync(ok ? "已上传到云端" : $"上传失败（{CloudSyncServiceRef.StatusText}）", "知道了");
            RefreshAccountArea();
        }

        /// <summary>「云存档下载」：二次确认后用云端覆盖本地（破坏性操作）。2026-11-22 定案恢复=
        /// 原地刷新——文案不再提「回启动页重进」（旧口径已废）。</summary>
        private void ConfirmCloudRestore()
        {
            if (!TapAccountService.LoggedIn && !SynergyUI.Online.TapAccountService.IsLoggedIn)
            {
                if (_tips != null) _tips.ShowAsync("登录 TapTap 后可使用云存档（在上方登录）", "知道了").Forget();
                return;
            }
            if (!CloudSyncServiceRef.HasCloudArchive)
            {
                if (_tips != null) _tips.ShowAsync("云端暂无存档——先「本地存档上传」", "知道了").Forget();
                return;
            }
            UiKit.ConfirmBox(_root, "云存档下载",
                "将用云端存档覆盖本机内容（教学/挑战进度、卡组与自建卡效果、对局记录），"
                + "恢复完成后原地刷新生效（无需重进）。确认下载覆盖？",
                "覆盖恢复", () => CloudSyncServiceRef.RestoreFromCloudAsync().Forget(),
                "取消", null, danger: true);
        }

        /// <summary>云存档已还原（2026-11-22 用户定案：**原地刷新、不走重进**）：数据已由
        /// GameDataReload 全量失效——向新教学单例重订事件并刷门槛（RebindTutorialProgressEvents
        /// 自带补订+RefreshTutorialGates）+ toast 反馈；其余屏（卡组/构筑等）层级每进屏重建、
        /// 重建即读新数据。</summary>
        private void OnCloudRestored()
        {
            _menu?.RebindTutorialProgressEvents(); // 新 TutorialProgressManager 补订 + 门槛刷新
            RefreshAccountArea();
            if (_tips != null) _tips.ShowAsync("云存档已恢复到本机", "知道了").Forget();
        }

        /// <summary>启动同步发现「云端较新」或「设备变更」（启动时主菜单未就绪则进场补问）——
        /// 设备变更时让玩家自选：使用云端（覆盖本机）还是使用当前设备（本机为准，有进度则上传覆盖云端）。</summary>
        private void OnCloudRestoreSuggested(long cloudSavedAt = 0)
        {
            if (CloudSyncServiceRef.PendingReason == CloudSyncServiceRef.RestorePromptReason.DeviceChanged)
            {
                UiKit.ConfirmBox(_root, "云端存档来自其他设备",
                    $"云端存档（{FormatCloudTime(CloudSyncServiceRef.SuggestedCloudSavedAt)}）来自另一台设备，"
                    + "与本机数据不是同一份。要使用哪一份？\n\n"
                    + "· 使用云端：下载云端存档覆盖本机\n"
                    + "· 使用当前设备：以本机为准（本机有进度时会上传覆盖云端）",
                    "使用云端", () => CloudSyncServiceRef.AnswerRestorePrompt(true),
                    "使用当前设备", () => CloudSyncServiceRef.AnswerRestorePrompt(false), danger: true);
                return;
            }
            UiKit.ConfirmBox(_root, "云端有更新的存档",
                $"云端存档（{FormatCloudTime(CloudSyncServiceRef.SuggestedCloudSavedAt)}）比本机新。"
                + "是否恢复到本机？本机当前进度将被覆盖。",
                "恢复云端", () => CloudSyncServiceRef.AnswerRestorePrompt(true),
                "暂不恢复", () => CloudSyncServiceRef.AnswerRestorePrompt(false), danger: true);
        }

        private static string FormatCloudTime(long unixSeconds)
            => unixSeconds <= 0 ? "时间未知"
                : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MM-dd HH:mm");
    }
}
