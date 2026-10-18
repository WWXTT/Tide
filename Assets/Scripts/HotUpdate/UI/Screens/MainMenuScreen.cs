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
    /// 教学接线（2026-10-19 三课制）：btn-teaching=纯进度路由（basics→jinjie→lunzhuan 逐课推进）；
    /// 教学↔本地对战按钮按 IsTutorialDone 换装（btn-battle=对局准备双选卡组入口）。
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        protected override string PrefabName => "MainUI";
        protected override string RootName => "main-menu";

        private RectTransform _teachingNode;
        private RectTransform _battleNode;
        private RectTransform _matchNode;
        private RectTransform[] _comboNodes = new RectTransform[3]; // 卡组/卡面/效果三组合界面

        protected override void Build()
        {
            BindButton("btn-battle", () =>
            {
                // 本地对战（2026-10-19 三课制定案：教学全完成后的主入口）——对局准备界面
                // 普通模式双选卡组（掷硬币+我方/对方 AI 卡组各选一套，见 PreMatchScreen）
                PreMatchScreen.Teaching3 = false; // 教学三会话标记随普通入口显式复位（防上次残留串模式）
                Manager.Show<PreMatchScreen>();
            });

            BindButton("btn-teaching", EnterTutorialFlow);

            // 匹配入口（2026-10-17 对局准备改版）：硬币定先后+双选卡组（本地 AI）。旧联机大厅
            // MatchScreen 已随 2026-10-18 网络簇整删（NetGate 关闭期本就不可达；git 历史可回取）。
            // 2026-10-19 三课制：教学三会话标记由本地对战入口（btn-battle）复位，本按钮不外露
            BindButton("btn-match", () => { PreMatchScreen.Teaching3 = false; Manager.Show<PreMatchScreen>(); });
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
        /// 卡组）。btn-match 不外露（与 btn-battle 同目的地）。三个组合界面按钮：第三课打过一局
        /// （无论胜负=LunzhuanAttempted）或教学整体完成前隐藏——之后常显供自组卡组重挑末日降临。</summary>
        private void RefreshTutorialGates()
        {
            var p = TutorialProgressManager.Instance;
            bool done = p.IsTutorialDone;
            if (_teachingNode != null) _teachingNode.gameObject.SetActive(!done);
            if (_battleNode != null) _battleNode.gameObject.SetActive(done);
            if (_matchNode != null) _matchNode.gameObject.SetActive(false); // 预留：对局准备入口（btn-battle 同目的地，不外露）
            bool comboUnlocked = p.LunzhuanAttempted || done;
            foreach (var node in _comboNodes)
                if (node != null) node.gameObject.SetActive(comboUnlocked);
        }

        /// <summary>「基础教学」入口（2026-10-19 三课制：纯进度路由，无任何询问/跳过路径）：
        /// 按 CourseOrder 推进——basics 攻防 → jinjie 进阶机制 → lunzhuan 末日降临 Boss（走
        /// 对局准备教学三变体选卡组）；三课全胜=教学整体完成（按钮换装 btn-battle，本口不可达）。</summary>
        private void EnterTutorialFlow()
        {
            if (TutorialProgressManager.Instance.IsTutorialDone) return; // 换装漏网兜底
            StartTutorialBattleWithSplit();
        }

        /// <summary>开教学局（2026-10-19 三课制：序列=basics→jinjie→lunzhuan 纯对战局）。
        /// 第三课 lunzhuan 走对局准备界面（教学三变体：硬币+10 秒选卡组+AI 镜像卡组），
        /// 其余直进战场。</summary>
        private void StartTutorialBattleWithSplit()
        {
            var id = TutorialProgressManager.Instance.NextTutorialId;
            if (id == TutorialProgressManager.LunzhuanId)
            {
                PreMatchScreen.Teaching3 = true; // 教学三会话（静态标记：胜负面重挑 Back 回本屏仍保持）
                Manager.Show<PreMatchScreen>();
                return;
            }
            TutorialFlow.StartTutorialBattle(id);
            SwallowToBattle();
        }

        private void SwallowToBattle()
            => BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
    }
}
