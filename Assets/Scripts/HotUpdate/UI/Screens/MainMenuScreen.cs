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
    /// 教学接线（2026-10-19 三课制）：btn-teaching=纯进度路由（basics→advanced→lunzhuan 逐课推进）。
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

            // TapTap 挂件（2026-10-24 预编译隔离；2026-11-09 登录迁移改版）：右下云存档入口经桥装段
            // 挂接（入口文案含昵称/退出登录在面板）——登录入口已迁启动页 LaunchScreen。
            //（TapBridge.Module 由 HotUpdateEntry.Start 扫域安装；本地单机风味无 Tide.TapTap
            // 程序集=Module null，云存档入口整体缺席，主菜单只剩本地方案）。
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
