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
    /// 设置面板账号区入口（CloudSaveEntry.prefab，2026-11-22 登录迁移迁入 slot-cloud）+ 弹开式面板
    ///（CloudSavePanel.prefab，打开时建、关闭即毁）。入口文案=「昵称 · 同步态」；未登录点入口=提示
    /// 先在上方登录（2026-11-22 用户定案：登录就在同面板账号区，不再清栈回启动页）；面板账号操作
    /// 只剩「退出登录」；**云恢复完成=原地刷新**（数据已由 GameDataReload 全量失效，重订教学事件+
    /// 刷门槛即读新数据——2026-11-22 用户定案「不走重进」，旧清栈回启动页链退役）。
    /// 经 TapModule.OnMainMenuBuilt/Enter/Exit 驱动；本地单机风味本类不编译——入口整体缺席。
    /// 控制器随 TapModule 进程级存续、每任主菜单 Build 重绑（事件订阅在控制器方法上，跨屏重订对称）。
    /// </summary>
    public sealed class CloudSaveMenuController
    {
        private MainMenuScreen _menu;
        private RectTransform _root; // 当前主菜单根（面板挂点——Build 时刷新）

        private Button _cloudButton;
        private TMP_Text _cloudStatusLabel;
        private GameObject _cloudPanel; // 面板实例（打开时建、关闭即毁——TipPanel 同款）
        private TMP_Text _cloudStatusLine;
        private TMP_Text _cloudAccountLine;
        private Button _cloudLoginButton; // 文案随登录态换「登录/退出登录」
        private TipPromptHost _tips;      // 游客闸提示（2026-11-22：替代旧清栈回启动页）

        private static GameObject _cloudEntryPrefab; // 进程级预制体句柄保活（同 TipPromptHost 口径）
        private static GameObject _cloudPanelPrefab;

        /// <summary>主菜单 Build 期：云存档入口实例化进设置面板账号区槽位+绑定（视觉烘焙在预制体，
        /// 落位=槽位居中）。未启用平台/未配置凭证时置灰——状态文本仍可见（编辑器冒烟=「编辑器/PC 未启用」态）。</summary>
        public void Build(MainMenuScreen menu)
        {
            _menu = menu;
            _root = menu.Root;
            _tips = new TipPromptHost(() => _root);
            _cloudPanel = null; // 上一任主菜单的面板已随屏销毁——引用归零防悬挂
            _cloudStatusLine = null;
            _cloudAccountLine = null;
            _cloudLoginButton = null;

            var slot = menu.SettingsCloudSlot; // 设置面板账号区槽位（2026-11-22 登录迁移）
            if (slot == null)
            {
                Debug.LogError("[MainMenu] 设置面板缺 slot-cloud 节点——云存档入口不显示"
                               + "（检查 MainUI.prefab panel-settings）");
                return;
            }
            if (_cloudEntryPrefab == null)
                _cloudEntryPrefab = HotUpdateAssets.Load<GameObject>("CloudSaveEntry", UiKit.PrefabPath("CloudSaveEntry"));
            if (_cloudEntryPrefab == null)
            {
                Debug.LogError("[MainMenu] CloudSaveEntry.prefab 加载失败——云存档入口不显示"
                               + "（检查 Assets/Art/Prefab/CloudSaveEntry.prefab 与 Yoo UI 组收集）");
                return;
            }
            var entry = UnityEngine.Object.Instantiate(_cloudEntryPrefab, slot, false);
            entry.name = "cloud-entry";
            var rt = (RectTransform)entry.transform; // 预制体根原锚点=主菜单右下——落槽改居中（尺寸保持烘焙值）
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
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
        /// 状态/账号行 + 上传/恢复/退出登录/关闭（登录入口在设置面板账号区——2026-11-22 登录迁移）。</summary>
        private void OpenCloudPanel()
        {
            // 游客闸（2026-11-22 用户定案：登录就在设置面板账号区，不再清栈回启动页）：
            // 未登录点云存档=提示先在上方登录（登录成功云同步自动启动，原地再点即见面板）。
            if (!TapAccountService.LoggedIn && !SynergyUI.Online.TapAccountService.IsLoggedIn)
            {
                _tips?.ShowAsync("登录 TapTap 后可使用云存档（在设置面板上方登录）", "知道了").Forget();
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

        /// <summary>退出登录（2026-11-09 用户定案：面板内唯一账号操作；2026-11-22 起原地停留——
        /// 登录就在设置面板账号区，无需回启动页）：双侧登出——先联机侧（清 PUBLIC_PROFILE 态+SDK
        /// 账号缓存；云侧 OnAccountChanged 桥只清内存不清 SDK 缓存，顺序反了会残留缓存导致下次
        /// 「静默续登」复活），后云侧（清云端档案引用）→ 关面板原地刷新（账号区事件自动回落登录按钮态）。</summary>
        private void LogoutFromPanel()
        {
            CloseCloudPanel();
            SynergyUI.Online.TapAccountService.Logout();
            TapAccountService.Logout();
            RefreshCloudEntry(); // 入口状态行回「未登录」（账号区徽章经 OnAccountChanged 自刷）
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

        /// <summary>云存档已还原（2026-11-22 用户定案：**原地刷新、不走重进**）：数据已由
        /// GameDataReload 全量失效——关面板 + 向新教学单例重订事件并刷门槛（RebindTutorialProgressEvents
        /// 自带补订+RefreshTutorialGates）+ 入口状态行回「已恢复」即读新数据；其余屏（卡组/构筑等）
        /// 层级每进屏重建、重建即读新数据。旧「清栈回启动页+PendingNotice 停留提示」链退役。</summary>
        private void OnCloudRestored()
        {
            CloseCloudPanel();
            _menu?.RebindTutorialProgressEvents(); // 新 TutorialProgressManager 补订 + 门槛刷新
            RefreshCloudEntry();
        }

        private static string FormatCloudTime(long unixSeconds)
            => unixSeconds <= 0 ? "时间未知"
                : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MM-dd HH:mm");
    }
}
