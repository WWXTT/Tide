using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;
using CloudSave;
using Cysharp.Threading.Tasks;
using Tide.HotUpdate;

namespace SynergyUI
{
    /// <summary>
    /// 主菜单 —— 子界面入口（2026-10-01 预制体化：层级来自 Assets/Art/Prefab/MainUI.prefab，
    /// Build 只做按名绑定+闭包接线；title/subtitle 等静态文本由预制体烘焙）。
    /// 教学接线（2026-10-19 三课制）：btn-teaching=纯进度路由（basics→advanced→lunzhuan 逐课推进）；
    /// 教学↔本地对战按钮按 IsTutorialDone 换装（btn-battle=对局准备双选卡组入口）。
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        protected override string PrefabName => "MainUI";
        protected override string RootName => "main-menu";

        private RectTransform _teachingNode;
        private Button _teachingButton;
        private TMP_Text _teachingLabel;
        private RectTransform _battleNode;
        private RectTransform _matchNode;
        private RectTransform[] _comboNodes = new RectTransform[3]; // 卡组/卡面/效果三组合界面

        // 云存档入口/面板（2026-10-23 预制体化：视觉烘焙在 CloudSaveEntry/CloudSavePanel.prefab，
        // 本屏只加载实例化+绑定；状态刷新只写文本/可点性）
        private Button _cloudButton;
        private TMP_Text _cloudStatusLabel;
        private GameObject _cloudPanel; // 面板实例（打开时建、关闭即毁——TipPanel 同款）
        private TMP_Text _cloudStatusLine;
        private TMP_Text _cloudAccountLine;
        private Button _cloudLoginButton; // 文案随登录态换「登录/退出登录」

        private static GameObject _cloudEntryPrefab; // 进程级预制体句柄保活（同 TipPromptHost 口径）
        private static GameObject _cloudPanelPrefab;

        protected override void Build()
        {
            BindButton("btn-battle", () =>
            {
                // 本地对战（2026-10-19 三课制定案：教学全完成后的主入口；同日掷币演出挪战场）——
                // 直吞战场走五段式开局：地块就位→角色就位→掷硬币定先后→战场内双选卡组→回合一
                //（硬币 50/50、后手角色 +5 护甲——语义与旧对局准备流程一致，演出挪进战场 3D）
                BattleEntry.Mode = BattleMode.LocalAI;
                BattleEntry.StagedOpening = true;
                BattleEntry.DummyTest = false; // 显式清（持久旗标防上次木桩局残留串模式）
                SwallowToBattle();
            });

            _teachingButton = BindButton("btn-teaching", EnterTutorialFlow);

            // 联机入口（2026-10-21 P2P 复活）：btn-match 复活为联机房间屏入口
            //（房主=进程内服务器/客人直连 IP——RefreshTutorialGates 随教学完成换装显隐）。
            // 旧联机大厅 MatchScreen 已随 2026-10-18 网络簇整删，现行= LobbyScreen。
            BindButton("btn-match", () => Manager.Show<LobbyScreen>());
            BindButton("btn-deck", () => Manager.Show<DeckBuilderScreen>());
            BindButton("btn-card", () => Manager.Show<CardComposerScreen>());
            BindButton("btn-effect", () => Manager.Show<EffectComposerScreen>());

            // 木桩测试（2026-10-22，接管原 2026-10-16 测试开局入口；2026-10-23 木桩改脚本驱动）：
            // 进入战场先弹选项面板（先后手/洗牌/元素池），确认后走五段式开局变体（固定先后手=跳过
            // 掷币段；掷硬币照常），先选你的卡组、再从三套主题卡组里给木桩选一套；
            // 对手=脚本木桩（ScriptedAi 无剧本全让过，可喂剧本）+响应窗全让过。
            // 旧一键「地牌充满」直入链保留在 TestStartFlow.EnterFullLandsTest（代码入口，无 UI 挂接）。
            BindButton("btn-teststart", () =>
            {
                BattleEntry.Mode = BattleMode.LocalAI;
                BattleEntry.StagedOpening = true; // 木桩走五段式变体（选项面板先于段一）
                BattleEntry.DummyTest = true;
                SwallowToBattle();
            });

            _teachingNode = Find("btn-teaching");
            _battleNode = Find("btn-battle");
            _matchNode = Find("btn-match");
            _comboNodes[0] = Find("btn-deck");
            _comboNodes[1] = Find("btn-card");
            _comboNodes[2] = Find("btn-effect");

            // 教学按钮文案节点（label 在 btn-teaching 子级；预制体不烘焙文字，运行时按进度写）
            var teachingLabel = _teachingNode != null ? FindUnder(_teachingNode, "label") : null;
            _teachingLabel = teachingLabel != null ? teachingLabel.GetComponentInChildren<TMP_Text>(true) : null;

            // TapTap 登录徽章（2026-10-23 登录接入·预制体化）：右上角 TapLoginBadge.prefab 实例化；
            // 编辑器/未配置时 Attach 内 IsSupported 闸直接不实例化（徽章缺席=正确态）。
            TapLoginBadge.Attach(Root);

            // 云存档入口（2026-10-23 接入）：右下角常驻（不受教学门禁——进度同步对新手同样有价值；
            // 未启用平台/未配置凭证时置灰只读展示状态）。
            BuildCloudEntry();
        }

        public override void OnEnter()
        {
            TutorialProgressManager.Instance.OnProgressChanged += RefreshTutorialGates;
            RefreshTutorialGates();
            SynergyUI.Online.TapAccountService.Warmup(); // 静默续登（有缓存账号无感登录；编辑器/未配置=no-op）

            CloudSyncService.OnStateChanged += RefreshCloudEntry;
            CloudSyncService.OnRestoreSuggested += OnCloudRestoreSuggested;
            CloudSyncService.OnRestored += OnCloudRestored;
            CloudSave.TapAccountService.OnAccountChanged += RefreshCloudEntry;
            RefreshCloudEntry();
            if (CloudSyncService.PendingRestorePrompt) OnCloudRestoreSuggested(); // 启动链询问时主菜单未就绪——进场补问
        }

        public override void OnExit()
        {
            TutorialProgressManager.Instance.OnProgressChanged -= RefreshTutorialGates;

            CloudSyncService.OnStateChanged -= RefreshCloudEntry;
            CloudSyncService.OnRestoreSuggested -= OnCloudRestoreSuggested;
            CloudSyncService.OnRestored -= OnCloudRestored;
            CloudSave.TapAccountService.OnAccountChanged -= RefreshCloudEntry;
            CloseCloudPanel();
        }

        /// <summary>教学↔本地对战换装（2026-10-19 三课制定案）：教学未整体完成=btn-teaching 在、
        /// btn-battle 藏；IsTutorialDone（三课全胜）反转——btn-battle=本地对战入口（对局准备双选
        /// 卡组；考验三未胜不换装=依旧不给用）。btn-match 不外露（与 btn-battle 同目的地）。
        /// 三个组合界面按钮（2026-10-21 提前）：教学二（advanced）完成即显示——供自组卡组备战，
        /// 此前隐藏。btn-teaching 文案按进度显示：基础教学一/二/三；教学三内容尚未完成——按钮
        /// 置灰不可进。</summary>
        private void RefreshTutorialGates()
        {
            var p = TutorialProgressManager.Instance;
            bool done = p.IsTutorialDone;
            if (_teachingNode != null) _teachingNode.gameObject.SetActive(!done);
            if (_battleNode != null) _battleNode.gameObject.SetActive(done);
            if (_matchNode != null) _matchNode.gameObject.SetActive(done); // 联机入口（2026-10-21 P2P 复活：与 btn-battle 同门槛外露）
            bool comboUnlocked = p.CompletedTutorials.Contains(TutorialProgressManager.AdvancedId); // 教学二完成（三课全胜时必含 advanced，无需再兜底）
            foreach (var node in _comboNodes)
                if (node != null) node.gameObject.SetActive(comboUnlocked);

            // 文案与可点性跟随下一课进度（教学三内容尚未完成：置灰+拦截，完成后去掉本闸即可）
            var next = p.NextTutorialId;
            bool thirdLocked = next == TutorialProgressManager.LunzhuanId;
            if (_teachingLabel != null)
            {
                _teachingLabel.text = next == TutorialProgressManager.AdvancedId ? "基础教学二"
                    : thirdLocked ? "基础教学三"
                    : "基础教学一";
                _teachingLabel.alpha = thirdLocked ? 0.5f : 1f;
            }
            if (_teachingButton != null)
                _teachingButton.interactable = !thirdLocked;
        }

        /// <summary>「基础教学」入口（2026-10-19 三课制：纯进度路由，无任何询问/跳过路径）：
        /// 按 CourseOrder 推进——basics 攻防 → advanced 进阶机制 → lunzhuan 末日降临 Boss（走
        /// 对局准备教学三变体选卡组）；三课全胜=教学整体完成（按钮换装 btn-battle，本口不可达）。
        /// 教学三内容尚未完成：进度到达时按钮置灰，本口直接返回。</summary>
        private void EnterTutorialFlow()
        {
            if (TutorialProgressManager.Instance.IsTutorialDone) return; // 换装漏网兜底
            if (TutorialProgressManager.Instance.NextTutorialId == TutorialProgressManager.LunzhuanId) return; // 教学三内容尚未完成（按钮已置灰，此处兜底）
            StartTutorialBattleWithSplit();
        }

        /// <summary>开教学局（2026-10-19 三课制：序列=basics→advanced→lunzhuan 纯对战局）。
        /// 第三课 lunzhuan 同走战场五段式开局变体（段四只选我方+10 秒倒计时+AI 镜像卡组——
        /// 角色就位让位 boss intro 揭示段），其余课直进战场立即开打。</summary>
        private void StartTutorialBattleWithSplit()
        {
            var id = TutorialProgressManager.Instance.NextTutorialId;
            if (id == TutorialProgressManager.LunzhuanId)
            {
                BattleEntry.Mode = BattleMode.Tutorial;
                BattleEntry.TutorialId = TutorialProgressManager.LunzhuanId;
                BattleEntry.StagedOpening = true; // 教学三开场变体（败局重挑 Replace 重入沿用现值）
                BattleEntry.DummyTest = false;    // 同 StagedOpening 清残留口径
                SwallowToBattle();
                return;
            }
            BattleEntry.StagedOpening = false; // 教学一二=立即启动（防上次普通局残留串模式）
            BattleEntry.DummyTest = false;     // 同上清残留
            TutorialFlow.StartTutorialBattle(id);
            SwallowToBattle();
        }

        // ======================================== 云存档（2026-10-23 接入） ========================================

        /// <summary>右下角云存档入口：CloudSaveEntry.prefab 实例化+绑定（视觉/锚点烘焙在预制体）。
        /// 未启用平台/未配置凭证时置灰——状态文本仍可见（编辑器冒烟=「编辑器/PC 未启用」态）。</summary>
        private void BuildCloudEntry()
        {
            if (_cloudEntryPrefab == null)
                _cloudEntryPrefab = HotUpdateAssets.Load<GameObject>("CloudSaveEntry", UiKit.PrefabPath("CloudSaveEntry"));
            if (_cloudEntryPrefab == null)
            {
                Debug.LogError("[MainMenu] CloudSaveEntry.prefab 加载失败——云存档入口不显示"
                               + "（检查 Assets/Art/Prefab/CloudSaveEntry.prefab 与 Yoo UI 组收集）");
                return;
            }
            var entry = UnityEngine.Object.Instantiate(_cloudEntryPrefab, Root, false);
            entry.name = "cloud-entry"; // 锚点/尺寸烘焙在预制体根（右下 -16,12）
            var rt = (RectTransform)entry.transform;
            var btnNode = UiKit.FindDeep(rt, "btn-cloud");
            _cloudButton = btnNode != null ? btnNode.GetComponentInChildren<Button>(true) : null;
            if (_cloudButton != null) _cloudButton.onClick.AddListener(OpenCloudPanel);
            var statusNode = UiKit.FindDeep(rt, "lbl-status");
            _cloudStatusLabel = statusNode != null ? statusNode.GetComponentInChildren<TMP_Text>(true) : null;
        }

        /// <summary>入口状态行+可点性刷新（OnStateChanged/OnAccountChanged/进场触发；面板开着同步刷）。</summary>
        private void RefreshCloudEntry()
        {
            if (_cloudStatusLabel != null) _cloudStatusLabel.text = CloudSyncService.StatusText;
            if (_cloudButton != null)
                _cloudButton.interactable = TapSdkConfig.CloudSaveSupported && TapSdkConfig.IsCredentialsReady;
            if (_cloudPanel != null) RefreshCloudPanel();
        }

        /// <summary>云存档面板：CloudSavePanel.prefab 实例化+接线（打开时建、关闭即毁）。
        /// 状态/账号行 + 上传/恢复/登录/关闭。</summary>
        private void OpenCloudPanel()
        {
            CloseCloudPanel();
            CloudSyncService.EnsureHost(); // 首次交互保底宿主（自动上传轮询常驻）
            if (_cloudPanelPrefab == null)
                _cloudPanelPrefab = HotUpdateAssets.Load<GameObject>("CloudSavePanel", UiKit.PrefabPath("CloudSavePanel"));
            if (_cloudPanelPrefab == null)
            {
                Debug.LogError("[MainMenu] CloudSavePanel.prefab 加载失败——云存档面板打不开"
                               + "（检查 Assets/Art/Prefab/CloudSavePanel.prefab 与 Yoo UI 组收集）");
                return;
            }
            var panel = UnityEngine.Object.Instantiate(_cloudPanelPrefab, Root, false);
            panel.name = "cloud-panel";
            _cloudPanel = panel;
            var rt = (RectTransform)panel.transform;
            var statusNode = UiKit.FindDeep(rt, "lbl-status");
            _cloudStatusLine = statusNode != null ? statusNode.GetComponentInChildren<TMP_Text>(true) : null;
            var accountNode = UiKit.FindDeep(rt, "lbl-account");
            _cloudAccountLine = accountNode != null ? accountNode.GetComponentInChildren<TMP_Text>(true) : null;
            BindPanelButton(rt, "btn-upload", () => CloudSyncService.UploadAsync("manual").Forget());
            BindPanelButton(rt, "btn-restore", ConfirmCloudRestore);
            _cloudLoginButton = BindPanelButton(rt, "btn-login", ToggleCloudLogin);
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
            if (_cloudStatusLine != null) _cloudStatusLine.text = CloudSyncService.StatusText;
            if (_cloudAccountLine != null) _cloudAccountLine.text = CloudAccountText();
            if (_cloudLoginButton != null)
            {
                var lbl = _cloudLoginButton.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = CloudSave.TapAccountService.LoggedIn ? "退出登录" : "登录";
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
            var account = CloudSave.TapAccountService.Current;
            if (account == null) return "未登录——云存档需要 TapTap 账号（登录不影响本地游玩）";
            string openId = account.openId ?? "";
            return $"已登录（openId {(openId.Length > 8 ? openId.Substring(0, 8) + "…" : openId)}）";
        }

        private void ToggleCloudLogin()
        {
            if (CloudSave.TapAccountService.LoggedIn)
            {
                CloudSave.TapAccountService.Logout();
            }
            else
            {
                CloudSave.TapAccountService.LoginFromUiAsync().Forget();
            }
            RefreshCloudEntry();
        }

        /// <summary>手动「从云端恢复」：二次确认后执行（覆盖本地进度属破坏性操作）。</summary>
        private void ConfirmCloudRestore()
        {
            if (!CloudSyncService.HasCloudArchive)
            {
                if (_cloudStatusLine != null) _cloudStatusLine.text = "云端暂无存档——先「上传到云端」";
                return;
            }
            UiKit.ConfirmBox(Root, "从云端恢复",
                "将用云端存档覆盖本机进度（教程/挑战/元素解锁/卡组与自建卡）。确认恢复？",
                "覆盖恢复", () => CloudSyncService.RestoreFromCloudAsync().Forget(),
                "取消", null, danger: true);
        }

        /// <summary>启动同步发现云端较新（启动时主菜单未就绪则进场补问）——询问是否恢复。</summary>
        private void OnCloudRestoreSuggested(long cloudSavedAt = 0)
        {
            UiKit.ConfirmBox(Root, "云端有更新的存档",
                $"云端存档（{FormatCloudTime(CloudSyncService.SuggestedCloudSavedAt)}）比本机新。"
                + "是否恢复到本机？本机当前进度将被覆盖。",
                "恢复云端", () => CloudSyncService.AnswerRestorePrompt(true),
                "暂不恢复", () => CloudSyncService.AnswerRestorePrompt(false), danger: true);
        }

        /// <summary>云存档已还原：教学管理器单例已重建——重订事件+刷新门槛，并轻提示。</summary>
        private void OnCloudRestored()
        {
            TutorialProgressManager.Instance.OnProgressChanged -= RefreshTutorialGates; // 新实例上 no-op 兜底
            TutorialProgressManager.Instance.OnProgressChanged += RefreshTutorialGates;
            RefreshTutorialGates();
            CloseCloudPanel();
            UiKit.ConfirmBox(Root, "云存档", "云端存档已恢复，进度已刷新。", "好的", null, "关闭", null, danger: false);
        }

        private static string FormatCloudTime(long unixSeconds)
            => unixSeconds <= 0 ? "时间未知"
                : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MM-dd HH:mm");

        private void SwallowToBattle()
            => BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
    }
}
