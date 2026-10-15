using UnityEngine;
using CardCore;
using Cysharp.Threading.Tasks;

namespace SynergyUI
{
    /// <summary>
    /// 主菜单 —— 子界面入口（2026-10-01 预制体化：层级来自 Assets/Art/Prefab/MainUI.prefab，
    /// Build 只做按名绑定+闭包接线；title/subtitle 等静态文本由预制体烘焙）。
    /// 教学接线（2026-10-13）：btn-teaching=教学四态裁决入口（§6 #1/#4/#5/#6/#8）；
    /// btn-battle 已由用户隐藏（后续自接新手引导逻辑）；教学↔联机匹配按钮按 IsTutorialDone 换装（§6 #10）。
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        protected override string PrefabName => "MainUI";
        protected override string RootName => "main-menu";

        private RectTransform _teachingNode;
        private RectTransform _matchNode;

        protected override void Build()
        {
            BindButton("btn-battle", () =>
            {
                BattleEntry.Mode = BattleMode.LocalAI; // 本地 vs AI（随机卡组开局）
                // 进战场过渡（2026-10-03 定案）：黑洞吞屏（光压 -20°→视界膨胀 0.5s）完成后再切屏，
                // 屏内 OnEnter 继续装棋盘+日出（BattleStageDirector 四步链）
                BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
            }); // 按钮已隐藏（用户 2026-10-13）：绑定保留供后续新手引导逻辑替换

            BindButton("btn-teaching", EnterTutorialFlow);

            // 大厅入口（未完成教学期间隐藏，RefreshTutorialGates 换装）
            BindButton("btn-match", () => Manager.Show<MatchScreen>());
            BindButton("btn-deck", () => Manager.Show<DeckBuilderScreen>());
            BindButton("btn-card", () => Manager.Show<CardComposerScreen>());
            BindButton("btn-effect", () => Manager.Show<EffectComposerScreen>());

            _teachingNode = Find("btn-teaching");
            _matchNode = Find("btn-match");
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

        /// <summary>教学↔匹配换装（§6 #10）：未完成教学=教学入口在/联机藏；IsTutorialDone 反转。
        /// btn-deck/btn-card/btn-effect 不藏（第三课创作走查需要）。</summary>
        private void RefreshTutorialGates()
        {
            bool done = TutorialProgressManager.Instance.IsTutorialDone;
            if (_teachingNode != null) _teachingNode.gameObject.SetActive(!done);
            if (_matchNode != null) _matchNode.gameObject.SetActive(done);
        }

        /// <summary>「基础教学」入口（2026-10-14 测试期直达）：直接进镜像对决——默认卡组镜像 +
        /// AI=tide_policy（不可用回落 SimpleAI）。不落考核档（AssessmentActive=false，进度不被
        /// 测试胜负污染）。四态裁决/两个教学回头恢复（原实现见 git：AskSkip/FinalChallenge/
        /// Assessment/Tutorial 四分支）。</summary>
        private void EnterTutorialFlow()
        {
            BattleEntry.Mode = BattleMode.LocalAI;
            BattleEntry.MirrorDeck = TutorialCreationFlow.ResolveDefaultMirrorDeck();
            BattleEntry.MirrorSkillCardId = null;
            SwallowToBattle();
        }

        /// <summary>开教学局；下一课=chuangzuo 时分流到效果合成器（StartTutorialBattle 对
        /// chuangzuo 只 BeginLesson 不设 BattleEntry——分支判定必须用调用前的 NextTutorialId，
        /// 事后查 BattleEntry.Mode 会被上一局残留值骗到）。</summary>
        private void StartTutorialBattleWithSplit()
        {
            var next = TutorialProgressManager.Instance.NextTutorialId;
            TutorialFlow.StartTutorialBattle();
            if (next == TutorialProgressManager.CreationLessonId)
            {
                Manager.Show<EffectComposerScreen>();
                return;
            }
            SwallowToBattle();
        }

        private void SwallowToBattle()
            => BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
    }
}
