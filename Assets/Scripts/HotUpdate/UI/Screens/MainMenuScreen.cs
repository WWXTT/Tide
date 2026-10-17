using System.Linq;
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

            // 匹配入口（2026-10-17 对局准备改版）：硬币定先后+选卡组（本地 AI）。旧联机大厅
            // MatchScreen 休眠保留（NetGate 关闭本就不可达——联机回归时再换回）
            BindButton("btn-match", () => Manager.Show<PreMatchScreen>());
            BindButton("btn-deck", () => Manager.Show<DeckBuilderScreen>());
            BindButton("btn-card", () => Manager.Show<CardComposerScreen>());
            BindButton("btn-effect", () => Manager.Show<EffectComposerScreen>());

            // 测试开局（2026-10-16）：教学预设场面管线解耦的通用测试入口（当前预设=地牌充满，
            // 见 TestStartFlow）；入口只写 BattleEntry，跳转同 EnterTutorialFlow 口径
            BindButton("btn-teststart", () => { TestStartFlow.EnterFullLandsTest(); SwallowToBattle(); });

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

        /// <summary>「基础教学」入口（2026-10-17 恢复建档跳过链后三态）：最后一课镜像局 / 正常开课 /
        /// 考核路径。首点教学（建档 skipAskPending）弹「是否跳过基础教学，进入考核？」；选过跳过且
        /// 考核未胜=直接重开考核局（不提供回课程——用户定案：负=原地重试）。</summary>
        private void EnterTutorialFlow()
        {
            if (TutorialProgressManager.Instance.IsTutorialDone) return; // 换装漏网兜底
            switch (TutorialFlow.ResolveTutorialButtonClick())
            {
                case TutorialButtonClickAction.StartFinalChallenge:
                    if (TutorialCreationFlow.StartFinalChallenge())
                        SwallowToBattle();
                    else
                        TideLog.Warn("[MainMenu] 最后一课无可用卡组（记名卡组丢失）——停留菜单");
                    return;
                case TutorialButtonClickAction.StartTutorial:
                    AskSkipOrStart();
                    return;
            }
        }

        /// <summary>教学按钮三态分流（2026-10-17 定案，替代旧「跳过基础介绍？」重进弹窗——已删）：
        /// ① 建档首点=弹「是否跳过基础教学，进入考核？」（不跳过=从教学一完整链开始）；
        /// ② 考核路径（选过跳过、未胜）=直接重开考核局；
        /// ③ 其余（课程进行中/续点）=正常开课——教学一每次点开都从头完整重走，不再询问。</summary>
        private void AskSkipOrStart()
        {
            var p = TutorialProgressManager.Instance;
            if (p.SkipAskPending)
            {
                UiKit.ConfirmBox(Root, "是否跳过基础教学，进入考核？",
                    "跳过将直接进行镜像考核对局：获胜即完成全部新手教学；失败可随时重试。",
                    "跳过，进入考核", () => { p.ChooseSkip(true); StartAssessment(); },
                    "不跳过，从教学一开始", () => { p.ChooseSkip(false); StartTutorialBattleWithSplit(); },
                    danger: true);
                return;
            }
            if (p.SkipChosen && !p.Skipped)
            {
                StartAssessment(); // 考核路径重试（2026-10-17 用户定案：负=原地重试，不回课程）
                return;
            }
            StartTutorialBattleWithSplit();
        }

        /// <summary>开考核局（建档跳过路径）：默认卡组镜像对局（主体卡组→红·快攻→随机镜像回落）；
        /// 无可用卡组=记日志停留菜单。</summary>
        private void StartAssessment()
        {
            if (TutorialCreationFlow.StartAssessment())
                SwallowToBattle();
            else
                TideLog.Warn("[MainMenu] 考核局无可用镜像卡组——停留菜单");
        }

        /// <summary>开教学局（创作课取消后无分流，2026-10-17：序列=basics→lunzhuan 纯对战局）。</summary>
        private void StartTutorialBattleWithSplit()
        {
            TutorialFlow.StartTutorialBattle();
            SwallowToBattle();
        }

        private void SwallowToBattle()
            => BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
    }
}
