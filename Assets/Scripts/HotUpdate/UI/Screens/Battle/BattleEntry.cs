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
        /// <summary>网络对局（P2P，2026-10-21 复活）：房主=进程内 NetSessionServer 权威服务器，
        /// 本端（房主/客人同一路径）以 NetGameClient 客户端运行——快照/事件流驱动渲染、intent 上行。
        /// 入口方接线：LobbyScreen 收到 MatchManifest 后把同一条连接经 Client 字段移交战斗屏。</summary>
        Network,
    }

    /// <summary>
    /// 对战入口参数（UIManager.Show 无参构造——静态传参）。
    /// 2026-10-18 功能拆分随 BattleScreen 出文件；同日网络簇整删（Network 模式值与连接字段退役）；
    /// 2026-10-21 P2P 复活：Network 模式值与 Client/Nickname 连接字段回归（Host/Port 不再需要——
    /// 连接对象自持远端地址，房间建立由 LobbyScreen 负责）。
    /// </summary>
    public static class BattleEntry
    {
        public static BattleMode Mode = BattleMode.LocalAI;

        /// <summary>网络局客户端连接（一次性移交）：LobbyScreen 收到 MatchManifest 时写入，
        /// BattleScreen OnEnter 消费后接管生命周期（NetClientRuntime 同步改绑）——
        /// 同连接防「重连+重新进房」撞座位锁（DeckSubmit 起玩家位锁定）。null=非网络局。</summary>
        public static CardCore.Network.NetGameClient Client;

        /// <summary>本端昵称（网络局房间展示/战报口径；空=战斗屏按座位兜底）。</summary>
        public static string Nickname = "";

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

        // 对局准备流程五字段（MatchDeck/MatchSkillCardId/AiDeck/AiSkillCardId/FirstPlayerIsPlayer）
        // 已随 2026-10-19 掷币演出挪战场整删：掷币+选卡组改在战场五段式开局内发生（BattleOpeningDirector），
        // 结果经 EngageStagedBattle 直喂 EngageLocalBattle 参数——不再经静态入口传递。

        /// <summary>战场五段式开局（2026-10-19 掷币演出挪战场定案）：true=掷硬币+选卡组在战场 3D 内
        /// 发生（1 地块就位→2 角色就位→3 硬币先后手→4 战场选卡组→5 开始回合一），卡组/硬币结果由
        /// BattleOpeningDirector 段五喂给 EngageLocalBattle；false=立即启动（教学一二/挑战/测试镜像路径）。
        /// 持久旗标（非一次性·ChallengeDifficulty 同口径）：每个对局入口显式置位——普通对战与教学三
        /// （lunzhuan）=true，教学一二/挑战/测试入口=false，防跨局残留串模式；教学三败局重挑 Replace
        /// 重入沿用现值（重掷硬币+重选卡组=换卡组新局的教学语义保持）。</summary>
        public static bool StagedOpening;
    }
}
