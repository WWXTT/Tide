using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardCore;
using Cysharp.Threading.Tasks;
using Tide.HotUpdate;

namespace SynergyUI
{
    /// <summary>
    /// 主菜单 —— 子界面入口（2026-10-01 预制体化：层级来自 Assets/Art/Prefab/MainUI.prefab，
    /// Build 只做按名绑定+闭包接线；title/subtitle 等静态文本由预制体烘焙）。
    /// 教学接线（2026-11-11 起四课制）：btn-teaching=纯进度路由（basics→basics_plus→advanced→lunzhuan 逐课推进）。
    /// 2026-10-25 本地对战入口退役（打 AI 归木桩测试难度档承接）——IsTutorialDone 后教学按钮
    /// 隐藏，战斗入口=btn-match 联机 + btn-teststart 木桩测试。
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        protected override string PrefabName => "MainUI";
        protected override string RootName => "main-menu";

        private RectTransform _teachingNode;
        private Button _teachingButton;
        private TMP_Text _teachingLabel;
        private RectTransform _matchNode;
        private readonly RectTransform[] _comboNodes = new RectTransform[3]; // 卡组/卡面/效果三入口（对局记录 2026-11-10 移联机屏）

        // ---- 设置面板（2026-11-22 音效系统 B4·计划 §4.5）：节点全部烘焙在 MainUI.prefab，代码只按名绑定 ----
        private RectTransform _settingsPanel;             // panel-settings（默认 inactive）
        private Slider _musicSlider;                      // slider-music（0..1）
        private Slider _sfxSlider;                        // slider-sfx（0..1）
        private TMP_Text _musicValueLabel;                // lbl-music-value（「60%」）
        private TMP_Text _sfxValueLabel;                  // lbl-sfx-value（「80%」）
        private TMP_Text _muteLabel;                      // btn-mute 的 label（静音开关文案）
        private Toggle _muteToggle;                       // btn-mute（2026-11-23 重排改版：纯 Toggle，isOn↔MuteOn 同步）

        // 账号区（2026-11-23 烘焙改版）：登录/退出登录/存档上传/云存档下载四钮（slot-login/
        // slot-logout/up-cloud/down-cloud）烘焙在 MainUI.prefab panel-settings，接线与登录态
        // 显隐在 Tide.TapTap 侧 CloudSaveMenuController——旧跨程序集槽位（SettingsLoginSlot/
        // SettingsCloudSlot）与 TapLoginBadge/CloudSaveEntry 注入链退役。

        protected override void Build()
        {
            _teachingButton = BindButton("btn-teaching", EnterTutorialFlow);

            // 联机入口（2026-10-21 P2P 复活）：btn-match 复活为联机房间屏入口
            //（房主=进程内服务器/客人直连 IP——RefreshTutorialGates 随教学完成换装显隐）。
            // 旧联机大厅 MatchScreen 已随 2026-10-18 网络簇整删，现行= LobbyScreen。
            BindButton("btn-match", () => Manager.Show<LobbyScreen>());
            BindButton("btn-deck", () => Manager.Show<DeckBuilderScreen>());
            BindButton("btn-card", () => Manager.Show<CardComposerScreen>());
            BindButton("btn-effect", () => Manager.Show<EffectComposerScreen>());

            // 木桩测试（2026-10-22，接管原 2026-10-16 测试开局入口；2026-11-03 对手统一预组策略 AI）：
            // 进入战场先弹选项面板（先后手/洗牌/元素池），确认后走五段式开局变体（固定先后手=跳过
            // 掷币段；掷硬币照常），先选你的卡组、再从三套主题卡组里给木桩选一套；
            // 对手=预组主题策略 AI（AutoMatch：红快攻/绿慢速/蓝控制，三档难度同源——旧「全让过
            // 木桩」2026-11-03 退役）。
            // 旧一键「地牌充满」直入链保留在 TestStartFlow.EnterFullLandsTest（代码入口，无 UI 挂接）。
            BindButton("btn-teststart", () =>
            {
                BattleEntry.Mode = BattleMode.LocalAI;
                BattleEntry.StagedOpening = true; // 木桩走五段式变体（选项面板先于段一）
                BattleEntry.DummyTest = true;
                SwallowToBattle();
            });

            _teachingNode = Find("btn-teaching");
            _matchNode = Find("btn-match");
            _comboNodes[0] = Find("btn-deck");
            _comboNodes[1] = Find("btn-card");
            _comboNodes[2] = Find("btn-effect");

            // 教学按钮文案节点（label 在 btn-teaching 子级；预制体不烘焙文字，运行时按进度写）
            var teachingLabel = _teachingNode != null ? FindUnder(_teachingNode, "label") : null;
            _teachingLabel = teachingLabel != null ? teachingLabel.GetComponentInChildren<TMP_Text>(true) : null;

            // 设置面板（2026-11-22 音效系统 B4；2026-11-23 账号区烘焙改版）：音乐/音效两条滑条+
            // 静音+关闭，主音量不单列（§4.5 定案）；账号区（登录/退出登录/存档上传/云存档下载）
            // 四钮烘焙在预制体，接线在 Tide.TapTap 侧——必须先于下方 TapBridge 挂件。
            //（TapBridge.Module 由 HotUpdateEntry.Start 扫域安装；本地单机风味无 Tide.TapTap
            // 程序集=Module null，账号区在 BindSettingsPanel 整体隐藏，主菜单只剩本地方案）。
            BindSettingsPanel();

            TapBridge.Module?.OnMainMenuBuilt(this);
        }

        public override void OnEnter()
        {
            TutorialProgressManager.Instance.OnProgressChanged += RefreshTutorialGates;
            RefreshTutorialGates();
            TapBridge.Module?.OnMainMenuEnter(this); // 静默续登 + 云存档事件订阅（单机风味 no-op）
        }

        public override void OnExit()
        {
            TutorialProgressManager.Instance.OnProgressChanged -= RefreshTutorialGates;
            TapBridge.Module?.OnMainMenuExit(this); // 云存档退订 + 关面板（单机风味 no-op）
        }

        // ======================================== 设置面板（2026-11-22 音效系统 B4·计划 §4.5） ========================================

        /// <summary>按名绑定设置面板（节点烘焙在 MainUI.prefab：btn-settings/panel-settings/
        /// slider-music/lbl-music-value/slider-sfx/lbl-sfx-value/btn-mute/btn-settings-close）。
        /// 滑条 onValueChanged → AudioService.SetBusVolume（实时生效+防抖持久化）；静音=btn-mute
        /// Toggle（2026-11-23 重排改版：无 Button——onValueChanged → SetMute，isOn 回填同步勾选态）。
        /// 账号区四钮（slot-login/slot-logout/up-cloud/down-cloud）由 Tide.TapTap 侧
        /// CloudSaveMenuController 按名接线（TapModule.OnMainMenuBuilt）；单机风味 Module=null
        /// 时此处整体隐藏账号区（缺席即正确态——节点常驻烘焙后必须主动藏；2026-11-24 定案：
        /// 有 Tide.TapTap 时账号区默认显示（登录钮未登录态常驻），TapTap 侧按平台隐藏链已退役）。</summary>
        private void BindSettingsPanel()
        {
            BindButton("btn-settings", () => ShowSettingsPanel(true));

            _settingsPanel = Find("panel-settings");
            if (_settingsPanel != null) _settingsPanel.gameObject.SetActive(false); // 默认收起（§4.5）

            BindButton("btn-settings-close", () => ShowSettingsPanel(false));

            var muteNode = Find("btn-mute");
            _muteLabel = muteNode != null ? muteNode.GetComponentInChildren<TMP_Text>(true) : null;
            _muteToggle = muteNode != null ? muteNode.GetComponentInChildren<Toggle>(true) : null;
            if (_muteToggle != null)
            {
                _muteToggle.SetIsOnWithoutNotify(AudioService.MuteOn); // 初值不带通知（防绑定期误触发写盘）
                _muteToggle.onValueChanged.AddListener(on =>
                {
                    AudioService.SetMute(on); // Master 闸（§4.2 SetMute；防抖持久化）
                    RefreshMuteLabel();
                });
            }

            _musicSlider = BindVolumeSlider("slider-music", "lbl-music-value", AudioBus.Music);
            _sfxSlider = BindVolumeSlider("slider-sfx", "lbl-sfx-value", AudioBus.Sfx);

            // 账号区（2026-11-23 烘焙改版）：接线在 Tide.TapTap 侧（CloudSaveMenuController——
            // 旧 SettingsLoginSlot/SettingsCloudSlot 槽位注入链退役）；单机风味整体隐藏。
            bool hasTap = TapBridge.Module != null;
            foreach (var nodeName in new[] { "lbl-account", "slot-login", "slot-logout", "up-cloud", "down-cloud" })
            {
                var node = FindOptional(nodeName);
                if (node != null) node.gameObject.SetActive(hasTap);
            }

            RefreshMuteLabel();
        }

        private void ShowSettingsPanel(bool open)
        {
            if (_settingsPanel != null) _settingsPanel.gameObject.SetActive(open);
        }

        private void RefreshMuteLabel()
        {
            if (_muteLabel != null) _muteLabel.text = AudioService.MuteOn ? "静音：开" : "静音：关";
        }

        /// <summary>单条音量滑条绑定：初值不带通知回填（防 Awake 期误触发写盘），拖动实时生效。</summary>
        private Slider BindVolumeSlider(string sliderName, string valueLabelName, AudioBus bus)
        {
            var sliderRt = Find(sliderName);
            var slider = sliderRt != null ? sliderRt.GetComponentInChildren<Slider>(true) : null;
            if (slider == null) return null;

            var labelRt = Find(valueLabelName);
            var label = labelRt != null ? labelRt.GetComponentInChildren<TMP_Text>(true) : null;
            if (bus == AudioBus.Music) _musicValueLabel = label; else _sfxValueLabel = label;

            float v = AudioService.GetBusVolume(bus);
            slider.SetValueWithoutNotify(v);
            if (label != null) label.text = Mathf.RoundToInt(v * 100f) + "%";

            slider.onValueChanged.AddListener(nv =>
            {
                AudioService.SetBusVolume(bus, nv);
                if (label != null) label.text = Mathf.RoundToInt(nv * 100f) + "%";
            });
            return slider;
        }

        /// <summary>教学门禁（2026-10-19 三课制·2026-10-25 本地对战退役改版）：教学未整体完成=
        /// btn-teaching 在、btn-match 藏；IsTutorialDone（四课全胜）反转——教学按钮隐藏、联机入口
        /// 外露（考验三未胜不换装=依旧不给用；本地对战按钮已退役，打 AI 走木桩测试难度档）。
        /// 三个组合界面按钮（2026-10-21 提前）：教学三（advanced）完成即显示——供自组卡组备战
        /// （教学四毕业连战的卡组来源），此前隐藏。btn-teaching 文案按进度显示：基础教学一/二/三/四
        ///（2026-11-02 教学三连战改版上线——原「内容尚未完成」置灰闸退役；2026-11-11 课程重编号：
        /// basics_plus 关键词课升格教学二，原教学二/三顺延为教学三/四）。
        /// public=云存档控制器跨程序集（Tide.TapTap）重订调用。</summary>
        public void RefreshTutorialGates()
        {
            var p = TutorialProgressManager.Instance;
            bool done = p.IsTutorialDone;
            if (_teachingNode != null) _teachingNode.gameObject.SetActive(!done);
            if (_matchNode != null) _matchNode.gameObject.SetActive(done); // 联机入口（2026-10-21 P2P 复活；2026-10-25 起 done 态唯一对战主入口）
            bool comboUnlocked = p.CompletedTutorials.Contains(TutorialProgressManager.AdvancedId); // 教学三（advanced）完成（四课全胜时必含 advanced，无需再兜底）
            foreach (var node in _comboNodes)
                if (node != null) node.gameObject.SetActive(comboUnlocked);

            // 文案跟随下一课进度（教学四=毕业连战：三套预组三场全胜过课）
            var next = p.NextTutorialId;
            if (_teachingLabel != null)
                _teachingLabel.text = next == TutorialProgressManager.BasicsPlusId ? "基础教学二"
                    : next == TutorialProgressManager.AdvancedId ? "基础教学三"
                    : next == TutorialProgressManager.LunzhuanId ? "基础教学四"
                    : "基础教学一";
        }

        /// <summary>「基础教学」入口（纯进度路由，无任何询问/跳过路径）：
        /// 按 CourseOrder 推进——basics 攻防 → basics_plus 关键词（2026-11-11 升格教学二）→
        /// advanced 主动效果 → lunzhuan 毕业连战（预组三连战，2026-11-02 改版）；
        /// 四课全胜=教学整体完成（教学按钮隐藏，本口不可达）。</summary>
        private void EnterTutorialFlow()
        {
            if (TutorialProgressManager.Instance.IsTutorialDone) return; // 换装漏网兜底
            StartTutorialBattleWithSplit();
        }

        /// <summary>开教学局（序列=basics→basics_plus→advanced→lunzhuan 纯对战局）。
        /// 第四课 lunzhuan 毕业连战（2026-11-02 改版）：LunzhuanGauntlet.Begin() 开新一轮（每次
        /// 入口从头计胜场），走战场五段式开局（段四首场单轮选我方卡组、后续场沿用；对手=预组序列），
        /// 三场全胜过课。</summary>
        private void StartTutorialBattleWithSplit()
        {
            var id = TutorialProgressManager.Instance.NextTutorialId;
            if (id == TutorialProgressManager.LunzhuanId)
            {
                LunzhuanGauntlet.Begin(); // 每次入口=全新一轮连战（中途退屏的残留状态就此作废）
                BattleEntry.Mode = BattleMode.Tutorial;
                BattleEntry.TutorialId = TutorialProgressManager.LunzhuanId;
                BattleEntry.StagedOpening = true; // 连战开场变体（场间 Replace 重入沿用现值——胜场/卡组在 Gauntlet 态）
                BattleEntry.DummyTest = false;    // 同 StagedOpening 清残留口径
                SwallowToBattle();
                return;
            }
            BattleEntry.StagedOpening = false; // 教学一二=立即启动（防上次普通局残留串模式）
            BattleEntry.DummyTest = false;     // 同上清残留
            TutorialFlow.StartTutorialBattle(id);
            SwallowToBattle();
        }

        // ======================================== 云存档还原回调（UI 实现住 Tide.TapTap 的 CloudSaveMenuController） ========================================

        /// <summary>云存档还原后重订教学事件（还原会重建 TutorialProgressManager 单例——旧实例
        /// 订阅随实例消亡，须向新实例补订并刷新门槛；云存档控制器在 OnRestored 时跨程序集调用）。</summary>
        public void RebindTutorialProgressEvents()
        {
            TutorialProgressManager.Instance.OnProgressChanged -= RefreshTutorialGates; // 新实例上 no-op 兜底
            TutorialProgressManager.Instance.OnProgressChanged += RefreshTutorialGates;
            RefreshTutorialGates();
        }

        private void SwallowToBattle()
            => BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
    }
}
