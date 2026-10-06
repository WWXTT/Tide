using System;
using CardCore;

namespace SynergyUI
{
    /// <summary>教学按钮点击后该做什么（TutorialFlow.ResolveTutorialButtonClick 的裁决结果）。</summary>
    public enum TutorialButtonClickAction
    {
        /// <summary>首次未考核——UI 弹「是否跳过基础教学」询问框（今日逻辑层只出裁决，弹窗后续接）。</summary>
        AskSkip,
        /// <summary>开镜像考核局（首次 或 考核输了重试——输了不转课程，留在挑战路径）。</summary>
        StartAssessment,
        /// <summary>走正常教学流程——进入进行中（或下一门未完成）教学关卡。</summary>
        StartTutorial,
        /// <summary>最后一课镜像局（三课走完后的等效入口；主入口=主菜单临时按钮「最后一课」）。</summary>
        StartFinalChallenge,
    }

    /// <summary>
    /// 教学流程编排（2026-10-06 教学逻辑层，纯逻辑不建 UI）：主菜单教学按钮的三态裁决 +
    /// 考核局/教学局/最后一课镜像局的开局参数与结果上报。UI 层后续接线只调本类与
    /// TutorialProgressManager/TutorialCreationFlow，不自持流程状态。
    /// 流程定案（2026-10-06 第三课+多课序列）：首次点击→询问跳过→
    /// 跳过=镜像考核局（默认卡组双方同用；胜=免教学，负=留挑战路径——调整默认卡组后可重试）；
    /// 不跳过=按 CourseOrder 逐课推进（tianji→lunzhuan→chuangzuo 创作走查）；
    /// 三课全完成→主菜单「最后一课」镜像局（玩家自己的卡组）→胜=教学整体完成。
    /// </summary>
    public static class TutorialFlow
    {
        /// <summary>当前 LocalAI 局是否为教学考核局（会话级标记；ReportBattleResult/ClearAssessment 消费）。</summary>
        public static bool AssessmentActive { get; private set; }

        /// <summary>
        /// 教学按钮点击裁决（progress 缺省真实单例；验证器可注入临时实例）：
        /// 待询问→AskSkip；三课走完等最后一课→StartFinalChallenge；未完成且不在课程进行中→
        /// StartAssessment（首次考核 或 输了重试——2026-10-06 定案：输了不转课程，留在挑战路径）；
        /// 其余（课程进行中/续点）→StartTutorial。
        /// </summary>
        public static TutorialButtonClickAction ResolveTutorialButtonClick(TutorialProgressManager progress = null)
        {
            var p = progress ?? TutorialProgressManager.Instance;
            if (p.SkipAskPending)
                return TutorialButtonClickAction.AskSkip;
            if (p.ShouldShowFinalChallenge)
                return TutorialButtonClickAction.StartFinalChallenge;
            if (!p.IsTutorialDone && string.IsNullOrEmpty(p.CurrentTutorial))
                return TutorialButtonClickAction.StartAssessment;
            return TutorialButtonClickAction.StartTutorial;
        }

        /// <summary>「是否跳过」弹窗的选择落档（true=进考核局路径；false=直接进正常教学流程）。</summary>
        public static void ChooseSkip(bool skip)
            => TutorialProgressManager.Instance.ChooseSkip(skip);

        /// <summary>开考核局（跳过教学路径，2026-10-06 改版为镜像局）：默认卡组（主体卡组）
        /// 双方同用；胜=免教学，负=留挑战路径（可先调整默认卡组再重试——镜像局每次开局重读
        /// 该卡组文件，改动生效）。切屏/过渡由 UI 层随后自理（同 btn-battle 的黑洞吞屏口径）。</summary>
        public static void StartAssessmentBattle()
        {
            BattleEntry.Mode = BattleMode.LocalAI;
            BattleEntry.MirrorDeck = TutorialCreationFlow.ResolveDefaultMirrorDeck();
            AssessmentActive = true;
        }

        /// <summary>开教学局：tutorialId 缺省取进行中关卡（否则序列首门未完成课）。scenario 条目由
        /// BattleScreen.StartTutorialGame 按 TutorialId 自动走预设场面直入。
        /// 第三课 chuangzuo 分流到创作管线走查（无战斗剧本，UI 后续自行导航合成器界面）。</summary>
        public static void StartTutorialBattle(string tutorialId = null)
        {
            var id = string.IsNullOrEmpty(tutorialId)
                ? TutorialProgressManager.Instance.NextTutorialId
                : tutorialId;
            AssessmentActive = false;
            if (id == TutorialProgressManager.CreationLessonId)
            {
                TutorialCreationFlow.BeginLesson();
                return;
            }
            if (string.IsNullOrEmpty(id))
            {
                // 新手课程已全完成（「最后一课」入口属 ShouldShowFinalChallenge）——兜底回落 basics
                TideLog.Warn("[TutorialFlow] 新手课程已全部完成，无课可开——回落 basics（应走最后一课镜像局入口）");
                id = TutorialProgressManager.BasicsId;
            }
            BattleEntry.Mode = BattleMode.Tutorial;
            BattleEntry.TutorialId = id;
        }

        /// <summary>
        /// 对局终局上报（BattleScreen 胜负弹窗时调用）：教学局胜=关卡完成落档（负=进行中保留，
        /// 回菜单再点教学按钮重开）；考核局/最后一课镜像局=结果落档（考核胜=免教学，负=留挑战路径
        /// 可调卡组重试；最后一课胜=教学整体完成，负=可重开）。挑战模式=按难度落战绩档（2026-10-06）。
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
            if (mode == BattleMode.LocalAI && AssessmentActive)
            {
                AssessmentActive = false;
                TutorialProgressManager.Instance.RecordAssessmentResult(playerWon);
                return;
            }
            if (mode == BattleMode.LocalAI && TutorialCreationFlow.FinalChallengeActive)
                TutorialCreationFlow.ReportFinalChallengeResult(playerWon);
        }

        /// <summary>会话标记清理（对局中途退出等未终局路径；BattleScreen.OnExit 调用防跨局残留）。</summary>
        public static void ClearAssessment()
        {
            AssessmentActive = false;
            TutorialCreationFlow.ClearSession();
        }
    }
}
