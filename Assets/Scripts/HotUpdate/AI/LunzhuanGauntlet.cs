using System;

namespace SynergyUI
{
    /// <summary>
    /// 教学四·毕业连战状态机（2026-11-02 改版：末日降临 Boss 战整链退役，本课=预组三连战——
    /// 玩家用自组卡组（或直接用预组）依次击败三套预组；2026-11-11 课程重编号：原教学三→教学四）。
    /// 口径（2026-11-02 用户定案）：对手=AI 正式对手（MakePacedAi+AutoMatch 主题策略，
    /// 与木桩难度 2 同款）；连战顺序=CardDataPaths.PresetDeckNames 原序（红·快攻→蓝·控制→
    /// 绿·慢速）；三场全胜=MarkTutorialCompleted 过课；**败局不清胜场**——保留胜场+沿用
    /// 当前卡组重掷币再战刚输的对手，或返回主菜单（进度作废，下次入口从头开始）；
    /// 中途退屏（认输键退出）同样不保进度——每次主菜单入口 Begin() 即全新一轮。
    /// 纯静态会话态（不落盘）：跨场存续靠 Manager.Replace 重入战屏（不回主菜单），
    /// 终局弹窗链（BattleTutorialDirector.PromptGauntletOverAsync）为唯一胜场记账口。
    /// 隐藏镜像局（2026-11-02 三战全胜后的加时赛）2026-11-11 按用户要求整链删除——
    /// BeginMirror/HiddenMirrorEligible/PresetCardTypesReferenced/Mirror/OpponentDeckName 退役。
    /// </summary>
    public static class LunzhuanGauntlet
    {
        /// <summary>连战通关所需胜场数（=预组套数）。</summary>
        public const int RequiredWins = 3;

        private static bool _active;
        private static int _wins;          // 已获胜场（0..RequiredWins）
        private static string _myDeckName; // 我方卡组名（DeckCatalog 原名；null=随机）
        private static bool _picked;       // 本轮已选过卡组（第 2/3 场与败局再战跳过段四选卡组）

        /// <summary>连战进行中（主菜单入口 Begin；主动放弃/整体完成 End）。</summary>
        public static bool Active => _active;

        /// <summary>已获胜场数。</summary>
        public static int Wins => _wins;

        /// <summary>我方卡组名（null=随机兜底）。</summary>
        public static string MyDeckName => _myDeckName;

        /// <summary>本轮已选过卡组（BattleOpeningDirector 段四据此跳过重选，沿用卡组）。</summary>
        public static bool Picked => _picked;

        /// <summary>当前场对手预组名（PresetDeckNames 原序按胜场索引；越界钳尾防完成态越界）。</summary>
        public static string OpponentName
        {
            get
            {
                var names = CardDataPaths.PresetDeckNames;
                return names[Math.Min(_wins, names.Length - 1)];
            }
        }

        /// <summary>开新一轮连战（主菜单教学按钮入口：每次进入都从头开始——上轮中途退屏的
        /// 残留状态就此作废）。</summary>
        public static void Begin()
        {
            _active = true;
            _wins = 0;
            _myDeckName = null;
            _picked = false;
        }

        /// <summary>连战收口（场间弹窗「返回主菜单」/三场全胜完成时调用）。</summary>
        public static void End()
        {
            _active = false;
            _wins = 0;
            _myDeckName = null;
            _picked = false;
        }

        /// <summary>记一胜（终局弹窗链专用——每场玩家胜利时调用一次）。
        /// 返回 true=胜场数已满 RequiredWins（连战完成，调用方落档过课）。</summary>
        public static bool ReportWin()
        {
            if (!_active) return false;
            _wins++;
            return _wins >= RequiredWins;
        }

        /// <summary>段四选卡组提交（第一场选完记档；随机堆=null 同样记档——后续场沿用随机）。</summary>
        public static void OnDeckPicked(string deckName)
        {
            _myDeckName = deckName;
            _picked = true;
        }
    }
}
