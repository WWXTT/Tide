using System;
using CardCore;
using GameBoard;

namespace SynergyUI
{
    /// <summary>
    /// 教学流程编排（2026-10-06 教学逻辑层，纯逻辑不建 UI）：主菜单教学按钮的进度路由 +
    /// 教学局的开局参数与结果上报。UI 层接线只调本类与
    /// TutorialProgressManager/TutorialCreationFlow，不自持流程状态。
    /// 流程（2026-10-19 三课制定案）：教学按钮纯进度路由，无任何询问/跳过——按 CourseOrder
    /// 逐课推进（basics 攻防 → advanced 进阶机制 → lunzhuan 末日降临 Boss）；三课全胜=教学整体完成
    /// （主菜单教学按钮换装本地对战按钮→对局准备双选卡组）。
    /// （旧「最后一课镜像局」环节 2026-10-19 随三课制整删——StartFinalChallenge/
    ///   FinalChallengeActive/裁决枚举全链退役；测试一律走 btn-teststart/TestStartFlow。）
    /// </summary>
    public static class TutorialFlow
    {
        /// <summary>
        /// 开教学局：tutorialId 缺省取进行中关卡（否则序列首门未完成课）。scenario 条目由
        /// BattleScreen.StartTutorialGame 按 TutorialId 自动走预设场面直入。
        /// 创作课 chuangzuo 已取消（2026-10-17 定案）——序列=basics→advanced→lunzhuan 纯对战局，走查分流删除。</summary>
        public static void StartTutorialBattle(string tutorialId = null)
        {
            var id = string.IsNullOrEmpty(tutorialId)
                ? TutorialProgressManager.Instance.NextTutorialId
                : tutorialId;
            if (string.IsNullOrEmpty(id))
            {
                // 三课已全完成（IsTutorialDone，按钮已换装本地对战）——兜底回落 basics
                TideLog.Warn("[TutorialFlow] 新手课程已全部完成，无课可开——回落 basics（教学按钮应已换装）");
                id = TutorialProgressManager.BasicsId;
            }
            BattleEntry.Mode = BattleMode.Tutorial;
            BattleEntry.TutorialId = id;
            BattleEntry.StagedOpening = false; // 教学一二=立即启动（2026-10-19 防普通局五段开场旗标残留串模式）
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
        /// 其余对局（普通本地）忽略。
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
            }
        }
    }
}
