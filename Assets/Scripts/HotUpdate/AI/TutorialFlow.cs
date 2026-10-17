using System;
using CardCore;
using GameBoard;

namespace SynergyUI
{
    /// <summary>教学按钮点击后该做什么（TutorialFlow.ResolveTutorialButtonClick 的裁决结果）。
    /// 2026-10-17 清理：AskSkip 询问/StartAssessment 考核局（旧教学时代跳过路径）整体退役——
    /// 按钮只走课程（StartTutorial）与最后一课（StartFinalChallenge）；测试一律走 btn-teststart。</summary>
    public enum TutorialButtonClickAction
    {
        /// <summary>走正常教学流程——进入进行中（或下一门未完成）教学关卡。</summary>
        StartTutorial,
        /// <summary>最后一课镜像局（三课走完后的等效入口；主入口=主菜单临时按钮「最后一课」）。</summary>
        StartFinalChallenge,
    }

    /// <summary>
    /// 教学流程编排（2026-10-06 教学逻辑层，纯逻辑不建 UI）：主菜单教学按钮的两态裁决 +
    /// 教学局/最后一课镜像局的开局参数与结果上报。UI 层接线只调本类与
    /// TutorialProgressManager/TutorialCreationFlow，不自持流程状态。
    /// 流程（2026-10-17 清理后）：按 CourseOrder 逐课推进（basics 两段式 → lunzhuan → chuangzuo）；
    /// 三课全完成 → 主菜单「最后一课」镜像局（玩家自己的卡组）→ 胜=教学整体完成。
    /// （旧「询问跳过 → 考核局免教学」路径已删——AskSkip 从未实装弹窗、考核分支不可达，
    ///   测试期直达镜像对局同样退役，测试统一走 btn-teststart/TestStartFlow。）
    /// </summary>
    public static class TutorialFlow
    {
        /// <summary>
        /// 教学按钮点击裁决（progress 缺省真实单例；验证器可注入临时实例）：
        /// 三课走完等最后一课→StartFinalChallenge；其余（课程进行中/课程间隙续点）→StartTutorial。
        /// </summary>
        public static TutorialButtonClickAction ResolveTutorialButtonClick(TutorialProgressManager progress = null)
        {
            var p = progress ?? TutorialProgressManager.Instance;
            if (p.ShouldShowFinalChallenge)
                return TutorialButtonClickAction.StartFinalChallenge;
            return TutorialButtonClickAction.StartTutorial;
        }

        /// <summary>开教学局：tutorialId 缺省取进行中关卡（否则序列首门未完成课）。scenario 条目由
        /// BattleScreen.StartTutorialGame 按 TutorialId 自动走预设场面直入。
        /// 创作课 chuangzuo 已取消（2026-10-17 定案）——序列=basics→lunzhuan，走查分流删除。</summary>
        public static void StartTutorialBattle(string tutorialId = null)
        {
            var id = string.IsNullOrEmpty(tutorialId)
                ? TutorialProgressManager.Instance.NextTutorialId
                : tutorialId;
            if (string.IsNullOrEmpty(id))
            {
                // 新手课程已全完成（「最后一课」入口属 ShouldShowFinalChallenge）——兜底回落 basics
                TideLog.Warn("[TutorialFlow] 新手课程已全部完成，无课可开——回落 basics（应走最后一课镜像局入口）");
                id = TutorialProgressManager.BasicsId;
            }
            BattleEntry.Mode = BattleMode.Tutorial;
            BattleEntry.TutorialId = id;
        }

        /// <summary>第二段注入（midTransition，2026-10-16 教学一两段式·逻辑层单源）：ApplyRuntime
        /// 无视规则归一 skipScenario（田忌场面）。BattleScreen 的 reveal 消费共用本口
        /// （UI 侧随后自行对账刷新）。</summary>
        public static void ApplyMidTransitionScenario(GameCore core, TutorialConfig tutorial,
            GameBoard.BoardState board = null)
        {
            if (core == null || tutorial?.skipScenario == null) return;
            TutorialScenarioSeeder.ApplyRuntime(core, tutorial.skipScenario, board);
        }

        /// <summary>
        /// 对局终局上报（BattleScreen 胜负弹窗时调用）：教学局胜=关卡完成落档（负=进行中保留，
        /// 回菜单再点教学按钮重开）；挑战模式=按难度落战绩档（2026-10-06）。
        /// 其余对局（普通本地/网络）忽略。
        /// </summary>
        public static void ReportBattleResult(BattleMode mode, bool playerWon)
        {
            if (mode == BattleMode.Tutorial)
            {
                if (playerWon)
                    TutorialProgressManager.Instance.MarkTutorialCompleted(BattleEntry.TutorialId);
                return;
            }
            if (mode == BattleMode.Challenge)
            {
                ChallengeProgressManager.Instance.RecordResult(BattleEntry.ChallengeDifficulty, playerWon);
                return;
            }
            if (mode == BattleMode.LocalAI && TutorialCreationFlow.AssessmentActive)
                TutorialCreationFlow.ReportAssessmentResult(playerWon); // 考核局（建档跳过路径，2026-10-17 恢复）
            else if (mode == BattleMode.LocalAI && TutorialCreationFlow.FinalChallengeActive)
                TutorialCreationFlow.ReportFinalChallengeResult(playerWon);
        }

        /// <summary>会话标记清理（对局中途退出等未终局路径；BattleScreen.OnExit 调用防跨局残留）。
        /// 2026-10-17：考核局标记随建档跳过路径恢复（ClearSession 同清 FinalChallenge/Assessment）。</summary>
        public static void ClearAssessment()
        {
            TutorialCreationFlow.ClearSession();
        }
    }
}
