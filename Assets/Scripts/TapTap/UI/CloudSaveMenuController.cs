using System;
using CloudSyncServiceRef = CloudSave.CloudSyncService;
using Cysharp.Threading.Tasks;
using SynergyUI;
using Tide.HotUpdate;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloudSave
{
    /// <summary>
    /// 主菜单云存档 UI 控制器（2026-10-23 接入，2026-10-24 预编译隔离自 MainMenuScreen 迁入本程序集）：
    /// 右下角常驻入口（CloudSaveEntry.prefab）+ 弹开式面板（CloudSavePanel.prefab，打开时建、关闭即毁）。
    /// 2026-11-09 登录迁移改版：入口文案=「昵称 · 同步态」；未登录点入口=清栈回启动页（登录界面，
    /// 不开面板）；面板账号操作只剩「退出登录」（登录入口在启动页）；云恢复完成=全量重载+回启动页。
    /// 经 TapModule.OnMainMenuBuilt/Enter/Exit 驱动；本地单机风味本类不编译——入口整体缺席。
    /// 控制器随 TapModule 进程级存续、每任主菜单 Build 重绑（事件订阅在控制器方法上，跨屏重订对称）。
    /// </summary>
    public sealed class CloudSaveMenuController
    {
        private MainMenuScreen _menu;
        private RectTransform _root; // 当前主菜单根（入口/面板挂点——Build 时刷新）

        private Button _cloudButton;
        private TMP_Text _cloudStatusLabel;
        private GameObject _cloudPanel; // 面板实例（打开时建、关闭即毁——TipPanel 同款）
        private TMP_Text _cloudStatusLine;
        private TMP_Text _cloudAccountLine;
        private Button _cloudLoginButton; // 文案随登录态换「登录/退出登录」

        private static GameObject _cloudEntryPrefab; // 进程级预制体句柄保活（同 TipPromptHost 口径）
        private static GameObject _cloudPanelPrefab;

        /// <summary>主菜单 Build 期：右下角云存档入口实例化+绑定（视觉/锚点烘焙在预制体）。
        /// 未启用平台/未配置凭证时置灰——状态文本仍可见（编辑器冒烟=「编辑器/PC 未启用」态）。</summary>
        public void Build(MainMenuScreen menu)
        {
            _menu = menu;
            _root = menu.Root;
            _cloudPanel = null; // 上一任主菜单的面板已随屏销毁——引用归零防悬挂
            _cloudStatusLine = null;
            _cloudAccountLine = null;
            _cloudLoginButton = null;

            if (_cloudEntryPrefab == null)
                _cloudEntryPrefab = HotUpdateAssets.Load<GameObject>("CloudSaveEntry", UiKit.PrefabPath("CloudSaveEntry"));
            if (_cloudEntryPrefab == null)
            {
                Debug.LogError("[MainMenu] CloudSaveEntry.prefab 加载失败——云存档入口不显示"
                               + "（检查 Assets/Art/Prefab/CloudSaveEntry.prefab 与 Yoo UI 组收集）");
                return;
            }
            var entry = UnityEngine.Object.Instantiate(_cloudEntryPrefab, _root, false);
            entry.name = "cloud-entry"; // 锚点/尺寸烘焙在预制体根（右下 -16,12）
            var rt = (RectTransform)entry.transform;
            var btnNode = UiKit.FindDeep(rt, "btn-cloud");
            _cloudButton = btnNode != null ? btnNode.GetComponentInChildren<Button>(true) : null;
            if (_cloudButton != null) _cloudButton.onClick.AddListener(OpenCloudPanel);
            var statusNode = UiKit.FindDeep(rt, "lbl-status");
            _cloudStatusLabel = statusNode != null ? statusNode.GetComponentInChildren<TMP_Text>(true) : null;
        }

        /// <summary>主菜单进场：云侧事件订阅 + 入口状态刷新 + 启动链挂起询问补问。</summary>
        public void OnEnter(MainMenuScreen menu)
        {
            if (_menu == null) Build(menu); // Build 未走过的兜底（正常链路 Show 必先 Build）
            CloudSyncServiceRef.OnStateChanged += RefreshCloudEntry;
            CloudSyncServiceRef.OnRestoreSuggested += OnCloudRestoreSuggested;
            CloudSyncServiceRef.OnRestored += OnCloudRestored;
            TapAccountService.OnAccountChanged += RefreshCloudEntry;
            SynergyUI.Online.TapAccountService.OnChanged += RefreshCloudEntry; // 昵称源（PUBLIC_PROFILE 侧）
            RefreshCloudEntry();
            if (CloudSyncServiceRef.PendingRestorePrompt) OnCloudRestoreSuggested(); // 启动链询问时主菜单未就绪——进场补问
        }

        /// <summary>主菜单离场：退订 + 关面板。</summary>
        public void OnExit(MainMenuScreen menu)
        {
            CloudSyncServiceRef.OnStateChanged -= RefreshCloudEntry;
            CloudSyncServiceRef.OnRestoreSuggested -= OnCloudRestoreSuggested;
            CloudSyncServiceRef.OnRestored -= OnCloudRestored;
            TapAccountService.OnAccountChanged -= RefreshCloudEntry;
            SynergyUI.Online.TapAccountService.OnChanged -= RefreshCloudEntry;
            CloseCloudPanel();
        }

        /// <summary>入口状态行+可点性刷新（OnStateChanged/账号变化/进场触发；面板开着同步刷）。
        /// 文案（2026-11-09 登录迁移）=「昵称 · 同步态」；未登录只显示同步态（含「未登录」）。</summary>
        private void RefreshCloudEntry()
        {
            if (_cloudStatusLabel != null) _cloudStatusLabel.text = EntryStatusText();
            if (_cloudButton != null)
                _cloudButton.interactable = TapSdkConfig.CloudSaveSupported && TapSdkConfig.IsCredentialsReady;
            if (_cloudPanel != null) RefreshCloudPanel();
        }

        /// <summary>入口一栏文案：已登录带昵称（联机侧 PUBLIC_PROFILE 昵称；云侧 basic_info 无昵称
        /// 时回落「TapTap玩家」），未登录=纯状态（StatusText 已含「未登录」）。</summary>
        private static string EntryStatusText()
        {
            var profile = SynergyUI.Online.TapAccountService.Current;
            if (profile == null && !TapAccountService.LoggedIn) return CloudSyncServiceRef.StatusText;
            string name = profile != null ? profile.Name : "TapTap玩家";
            return $"{name} · {CloudSyncServiceRef.StatusText}";
        }

        /// <summary>云存档面板：CloudSavePanel.prefab 实例化+接线（打开时建、关闭即毁）。
        /// 状态/账号行 + 上传/恢复/退出登录/关闭（登录入口 2026-11-09 迁启动页——面板不再提供登录）。</summary>
        private void OpenCloudPanel()
        {
            // 游客闸（2026-11-09 用户定案）：未登录点云存档=弹登录界面（清栈回启动页）而非存档面板；
            // 登录成功自动回主菜单，云同步随之启动，玩家再点入口即见面板。
            if (!TapAccountService.LoggedIn && !SynergyUI.Online.TapAccountService.IsLoggedIn)
            {
                UIBootstrap.ReturnToLaunch();
                return;
            }
            CloseCloudPanel();
            CloudSyncServiceRef.EnsureHost(); // 首次交互保底宿主（自动上传轮询常驻）
            if (_cloudPanelPrefab == null)
                _cloudPanelPrefab = HotUpdateAssets.Load<GameObject>("CloudSavePanel", UiKit.PrefabPath("CloudSavePanel"));
            if (_cloudPanelPrefab == null)
            {
                Debug.LogError("[MainMenu] CloudSavePanel.prefab 加载失败——云存档面板打不开"
                               + "（检查 Assets/Art/Prefab/CloudSavePanel.prefab 与 Yoo UI 组收集）");
                return;
            }
            var panel = UnityEngine.Object.Instantiate(_cloudPanelPrefab, _root, false);
            panel.name = "cloud-panel";
            _cloudPanel = panel;
            var rt = (RectTransform)panel.transform;
            var statusNode = UiKit.FindDeep(rt, "lbl-status");
            _cloudStatusLine = statusNode != null ? statusNode.GetComponentInChildren<TMP_Text>(true) : null;
            var accountNode = UiKit.FindDeep(rt, "lbl-account");
            _cloudAccountLine = accountNode != null ? accountNode.GetComponentInChildren<TMP_Text>(true) : null;
            BindPanelButton(rt, "btn-upload", () => CloudSyncServiceRef.UploadAsync("manual").Forget());
            BindPanelButton(rt, "btn-restore", ConfirmCloudRestore);
            _cloudLoginButton = BindPanelButton(rt, "btn-login", LogoutFromPanel); // 2026-11-09 登录迁移：改固定「退出登录」
            BindPanelButton(rt, "btn-close", CloseCloudPanel);
            RefreshCloudPanel();
        }

        /// <summary>面板按钮绑定：找节点→清监听→挂回调；缺节点 LogError（不静默）。</summary>
        private Button BindPanelButton(RectTransform panel, string name, Action onClick)
        {
            var node = UiKit.FindDeep(panel, name);
            var btn = node != null ? node.GetComponentInChildren<Button>(true) : null;
            if (btn == null)
            {
                Debug.LogError($"[MainMenu] 云存档面板缺按钮：{name}（检查 CloudSavePanel.prefab）");
                return null;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        private void RefreshCloudPanel()
        {
            if (_cloudPanel == null) return;
            if (_cloudStatusLine != null) _cloudStatusLine.text = CloudSyncServiceRef.StatusText;
            if (_cloudAccountLine != null) _cloudAccountLine.text = CloudAccountText();
            if (_cloudLoginButton != null) // 面板只在已登录态可开（游客闸拦在 OpenCloudPanel）
            {
                var lbl = _cloudLoginButton.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = "退出登录";
            }
        }

        private void CloseCloudPanel()
        {
            if (_cloudPanel != null)
            {
                UnityEngine.Object.Destroy(_cloudPanel);
                _cloudPanel = null;
            }
            _cloudStatusLine = null;
            _cloudAccountLine = null;
            _cloudLoginButton = null;
        }

        private static string CloudAccountText()
        {
            var online = SynergyUI.Online.TapAccountService.Current; // 昵称源（PUBLIC_PROFILE）
            var account = TapAccountService.Current;                  // 云侧 openId（basic_info 静默）
            if (online == null && account == null) return "未登录";
            string openId = account != null ? (account.openId ?? "")
                : (online != null ? (online.OpenId ?? "") : "");
            string name = online != null ? online.Name : "TapTap玩家";
            return $"{name}（openId {(openId.Length > 8 ? openId.Substring(0, 8) + "…" : openId)}）";
        }

        /// <summary>退出登录（2026-11-09 用户定案：面板内唯一账号操作）：双侧登出——先联机侧
        ///（清 PUBLIC_PROFILE 态+SDK 账号缓存；云侧 OnAccountChanged 桥只清内存不清 SDK 缓存，
        /// 顺序反了会残留缓存导致下次「静默续登」复活），后云侧（清云端档案引用）→ 清栈回启动页。</summary>
        private void LogoutFromPanel()
        {
            CloseCloudPanel();
            SynergyUI.Online.TapAccountService.Logout();
            TapAccountService.Logout();
            UIBootstrap.ReturnToLaunch();
        }

        /// <summary>手动「从云端恢复」：二次确认后执行（覆盖本地内容属破坏性操作）。</summary>
        private void ConfirmCloudRestore()
        {
            if (!CloudSyncServiceRef.HasCloudArchive)
            {
                if (_cloudStatusLine != null) _cloudStatusLine.text = "云端暂无存档——先「上传到云端」";
                return;
            }
            UiKit.ConfirmBox(_root, "从云端恢复",
                "将用云端存档覆盖本机内容（教学/挑战进度、卡组与自建卡效果、对局记录），"
                + "恢复后回到启动页重新进入。确认恢复？",
                "覆盖恢复", () => CloudSyncServiceRef.RestoreFromCloudAsync().Forget(),
                "取消", null, danger: true);
        }

        /// <summary>启动同步发现云端较新（启动时主菜单未就绪则进场补问）——询问是否恢复。</summary>
        private void OnCloudRestoreSuggested(long cloudSavedAt = 0)
        {
            UiKit.ConfirmBox(_root, "云端有更新的存档",
                $"云端存档（{FormatCloudTime(CloudSyncServiceRef.SuggestedCloudSavedAt)}）比本机新。"
                + "是否恢复到本机？本机当前进度将被覆盖。",
                "恢复云端", () => CloudSyncServiceRef.AnswerRestorePrompt(true),
                "暂不恢复", () => CloudSyncServiceRef.AnswerRestorePrompt(false), danger: true);
        }

        /// <summary>云存档已还原（2026-11-09 重载定案）：数据已由 GameDataReload 全量失效——
        /// 清栈回启动页（带停留提示）后自动续进主菜单，整树重建后必读新数据；
        /// 主菜单 OnEnter 自会向新教学单例重订事件（原 RebindTutorialProgressEvents 调用随之退役）。</summary>
        private void OnCloudRestored()
        {
            CloseCloudPanel();
            LaunchScreen.PendingNotice = "云端存档已恢复，数据已刷新";
            UIBootstrap.ReturnToLaunch();
        }

        private static string FormatCloudTime(long unixSeconds)
            => unixSeconds <= 0 ? "时间未知"
                : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MM-dd HH:mm");
    }
}
