using System;
using System.Collections.Generic;
using CardCore;

namespace SynergyUI
{
    /// <summary>对战模式。</summary>
    public enum BattleMode
    {
        /// <summary>本地 vs 极简 AI（GameCore 直连）。</summary>
        LocalAI,
        /// <summary>教学固定局（2026-10-02）：双方卡组顺序=摸牌序列（锁牌库序）、机器人逐回合剧本演出
        /// （ScriptedAi）、响应窗口全让过——渲染/交互与本地 AI 局完全同轨。</summary>
        Tutorial,
        /// <summary>挑战模式（2026-10-06）：玩家自选卡组，电脑按难度 N 获得非对称优势——
        /// 地牌槽上限 7+N（N≥7 触顶 14）、起手 6+N（InitGame 加成参数）。渲染/交互与本地 AI 局同轨。</summary>
        Challenge,
    }

    /// <summary>
    /// 对战入口参数（UIManager.Show 无参构造——静态传参）。
    /// 2026-10-18 功能拆分随 BattleScreen 出文件；同日网络簇整删：Network 模式值与
    /// Host/Port/Nickname/Client 四个连接字段一并退役（NetGate 禁联机期 BattleScreen 网络链
    /// 为休眠死代码、零调用；引擎侧协议在 CardCore.Network 原样保留，复活走第二轮决策管线重写，
    /// git 历史可回取）。
    /// </summary>
    public static class BattleEntry
    {
        public static BattleMode Mode = BattleMode.LocalAI;

        /// <summary>教学局关卡 id（TutorialLibrary 条目；缺省 "basics" 基础教学）。
        /// 入口方接线：BattleEntry.Mode = BattleMode.Tutorial 后 Show&lt;BattleScreen&gt;。</summary>
        public static string TutorialId = "basics";

        /// <summary>跳过教学第一大段（2026-10-16 教学一两段式）：true=basics 等支持跳过的条目
        /// 改走 skipScenario 直入+guideSkip 引导链（第一大段介绍不播）。一次性消费防跨局残留。
        /// 注入方=MainMenuScreen（重进未完课且 introDone 时的选择弹窗）。</summary>
        public static bool SkipIntro;

        /// <summary>测试镜像局卡组（TestStartFlow 专用；2026-10-19 结业镜像局退役）：非空=双方
        /// 同用这份卡组开局（AI 操纵其镜像）。一次性消费防跨局残留（StartLocalFan 开局即置空）。</summary>
        public static List<CardData> MirrorDeck;

        /// <summary>测试镜像局技能标记卡 ID：与 MirrorDeck 成对一次性消费——双方同用（镜像口径）；
        /// null=镜像局无技能。</summary>
        public static string MirrorSkillCardId;

        /// <summary>测试开局预设（2026-10-16）：非空=镜像局改走教学注入管线（InitGame 跳起手 +
        /// TutorialScenarioSeeder 静默铺场 + StartGame 起跳回合，StartNewGame scenario 分支）。
        /// 一次性消费防跨局残留（StartLocalFan 开局即置空）。注入方=TestStartFlow。</summary>
        public static TutorialScenario Scenario;

        /// <summary>挑战模式难度（2026-10-06，1..9 单值联动）：电脑地牌槽上限 7+N（N≥7 触顶 14）、起手 6+N。
        /// 与 TutorialId 同口径：注入方设值后保持到终局（ReportBattleResult 读取落档），
        /// 每次进挑战模式都会重设，无跨局残留问题。</summary>
        public static int ChallengeDifficulty = 1;

        /// <summary>挑战模式玩家卡组（2026-10-06）：null=随机卡组（RandomDeckFrom 兜底）。
        /// 一次性消费防跨局残留（StartLocalFan 开局即置空）。</summary>
        public static List<CardData> ChallengeDeck;

        /// <summary>挑战模式玩家技能标记卡 ID（2026-10-07 卡牌化）：与 ChallengeDeck 成对
        /// 一次性消费；null=无技能。AI 侧随机卡组在 StartLocalFan 自动挑合格结界。</summary>
        public static string ChallengeSkillCardId;

        /// <summary>对局准备流程玩家卡组（2026-10-17 PreMatchScreen）：非空=按此开局（null=随机兜底）。
        /// 一次性消费防跨局残留（StartLocalFan 开局即置空）。注入方=PreMatchScreen。</summary>
        public static List<CardData> MatchDeck;

        /// <summary>对局准备流程玩家技能标记卡 ID：与 MatchDeck 成对一次性消费；null=按卡组随机挑。</summary>
        public static string MatchSkillCardId;

        /// <summary>对局准备流程 AI 卡组（2026-10-17）：非空=AI 按此开局（null=随机兜底）。</summary>
        public static List<CardData> AiDeck;

        /// <summary>对局准备流程 AI 技能标记卡 ID：与 AiDeck 成对一次性消费。</summary>
        public static string AiSkillCardId;

        /// <summary>硬币先手（2026-10-17 对局准备流程）：true=我方先手 / false=对方先手 / null=现状
        /// （Player1 恒先）。后手角色开局 +5 护甲指示物（StartNewGame secondArmorBonus）。</summary>
        public static bool? FirstPlayerIsPlayer;
    }
}
