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

        protected override void Build()
        {
            BindButton("btn-battle", () =>
            {
                // 本地对战（2026-10-19 三课制定案：教学全完成后的主入口；同日掷币演出挪战场）——
                // 直吞战场走五段式开局：地块就位→角色就位→掷硬币定先后→战场内双选卡组→回合一
                //（硬币 50/50、后手角色 +5 护甲——语义与旧对局准备流程一致，演出挪进战场 3D）
                BattleEntry.Mode = BattleMode.LocalAI;
                BattleEntry.StagedOpening = true;
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

            // 测试开局（2026-10-16）：教学预设场面管线解耦的通用测试入口（当前预设=地牌充满，
            // 见 TestStartFlow）；入口只写 BattleEntry，跳转同 EnterTutorialFlow 口径
            BindButton("btn-teststart", () => { TestStartFlow.EnterFullLandsTest(); SwallowToBattle(); });

            _teachingNode = Find("btn-teaching");
            _battleNode = Find("btn-battle");
            _matchNode = Find("btn-match");
            _comboNodes[0] = Find("btn-deck");
            _comboNodes[1] = Find("btn-card");
            _comboNodes[2] = Find("btn-effect");

            // 教学按钮文案节点（label 在 btn-teaching 子级；预制体不烘焙文字，运行时按进度写）
            var teachingLabel = _teachingNode != null ? FindUnder(_teachingNode, "label") : null;
            _teachingLabel = teachingLabel != null ? teachingLabel.GetComponentInChildren<TMP_Text>(true) : null;
        }

        public override void OnEnter()
        {
            TutorialProgressManager.Instance.OnProgressChanged += RefreshTutorialGates;
            RefreshTutorialGates();
        }

        public override void OnExit()
        {
            TutorialProgressManager.Instance.OnProgressChanged -= RefreshTutorialGates;
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
                SwallowToBattle();
                return;
            }
            BattleEntry.StagedOpening = false; // 教学一二=立即启动（防上次普通局残留串模式）
            TutorialFlow.StartTutorialBattle(id);
            SwallowToBattle();
        }

        private void SwallowToBattle()
            => BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
    }
}
