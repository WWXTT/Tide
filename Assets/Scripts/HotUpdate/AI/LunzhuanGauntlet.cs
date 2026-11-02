using System;
using System.Collections.Generic;

namespace SynergyUI
{
    /// <summary>
    /// 教学三·毕业连战状态机（2026-11-02 教学三改版：末日降临 Boss 战整链退役，第三课=
    /// 预组三连战——玩家用自组卡组（或直接用预组）依次击败三套预组）。
    /// 口径（2026-11-02 用户定案）：对手=AI 正式对手（MakePacedAi+AutoMatch 主题策略，
    /// 与木桩难度 2 同款）；连战顺序=CardDataPaths.PresetDeckNames 原序（红·快攻→蓝·控制→
    /// 绿·慢速）；三场全胜=MarkTutorialCompleted 过课；**败局不清胜场**——保留胜场+沿用
    /// 当前卡组重掷币再战刚输的对手，或返回主菜单（进度作废，下次入口从头开始）；
    /// 中途退屏（认输键退出）同样不保进度——每次主菜单入口 Begin() 即全新一轮。
    /// 纯静态会话态（不落盘）：跨场存续靠 Manager.Replace 重入战屏（不回主菜单），
    /// 终局弹窗链（BattleTutorialDirector.PromptGauntletOverAsync）为唯一胜场记账口。
    ///
    /// 隐藏镜像局（2026-11-02 用户需求·三战全胜后的加时赛）：我方=玩家自组卡组（非随机/
    /// 非预组）且卡组引用的预组卡**种类**&lt;3（去重卡 ID 计，张数不计）→ 通关弹窗加开
    /// 「镜像对决」选项——AI 用刚才通关的同款卡组、双方锁牌库序（相同牌序）再战一场；
    /// 镜像局胜负不记账（Active 保持 true，ReportBattleResult 拦截口径照旧）。
    /// </summary>
    public static class LunzhuanGauntlet
    {
        /// <summary>连战通关所需胜场数（=预组套数）。</summary>
        public const int RequiredWins = 3;

        /// <summary>隐藏镜像局门槛：自组卡组引用的预组卡种类数须小于此值（种类而非数量）。</summary>
        public const int HiddenMirrorPresetTypeLimit = 3;

        private static bool _active;
        private static int _wins;          // 已获胜场（0..RequiredWins）
        private static string _myDeckName; // 我方卡组名（DeckCatalog 原名；null=随机）
        private static bool _picked;       // 本轮已选过卡组（第 2/3 场与败局再战跳过段四选卡组）
        private static bool _mirror;       // 隐藏镜像局进行中（三战全胜后开启——见 BeginMirror）

        /// <summary>连战进行中（主菜单入口 Begin；主动放弃/整体完成 End）。</summary>
        public static bool Active => _active;

        /// <summary>已获胜场数。</summary>
        public static int Wins => _wins;

        /// <summary>我方卡组名（null=随机兜底）。</summary>
        public static string MyDeckName => _myDeckName;

        /// <summary>本轮已选过卡组（BattleOpeningDirector 段四据此跳过重选，沿用卡组）。</summary>
        public static bool Picked => _picked;

        /// <summary>隐藏镜像局进行中（段四沿用卡组/段五对手=我方卡组镜像/锁牌库序三消费口）。</summary>
        public static bool Mirror => _mirror;

        /// <summary>当前场对手预组名（PresetDeckNames 原序按胜场索引；越界钳尾防完成态越界）。</summary>
        public static string OpponentName
        {
            get
            {
                var names = CardDataPaths.PresetDeckNames;
                return names[Math.Min(_wins, names.Length - 1)];
            }
        }

        /// <summary>段五对手卡组名（BattleOpeningDirector 消费）：镜像局=我方卡组名（镜像对决），
        /// 常规局=预组序列 OpponentName。镜像局资格已排除随机堆（MyDeckName 恒非 null）。</summary>
        public static string OpponentDeckName => _mirror ? _myDeckName : OpponentName;

        /// <summary>开新一轮连战（主菜单教学按钮入口：每次进入都从头开始——上轮中途退屏的
        /// 残留状态就此作废）。</summary>
        public static void Begin()
        {
            _active = true;
            _wins = 0;
            _myDeckName = null;
            _picked = false;
            _mirror = false;
        }

        /// <summary>连战收口（场间弹窗「返回主菜单」/三场全胜完成/镜像局结算时调用）。</summary>
        public static void End()
        {
            _active = false;
            _wins = 0;
            _myDeckName = null;
            _picked = false;
            _mirror = false;
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

        /// <summary>开启隐藏镜像局（PromptGauntletOverAsync 隐藏关卡分支专用，三战全胜后调用）：
        /// 沿用本轮我方卡组与已选标记（段四跳过重选），对手卡组=我方卡组（段五经
        /// OpponentDeckName 镜像取用），EngageLocalBattle 按 Mirror 锁牌库序=双方相同牌序。
        /// Active 保持 true——镜像局胜负经 ReportBattleResult 拦截不落档（连战已另行收口过课）。</summary>
        public static void BeginMirror()
        {
            _active = true;
            _mirror = true;
            _picked = true;
        }

        /// <summary>隐藏镜像局资格：我方=玩家自组卡组（随机堆/预组本体均不算自组）且引用的
        /// 预组卡种类 &lt; HiddenMirrorPresetTypeLimit。预组副本（同 id 直引整组预组）天然约满编
        /// 30 种超限，无须特判。</summary>
        public static bool HiddenMirrorEligible()
        {
            var name = _myDeckName;
            if (string.IsNullOrEmpty(name) || DeckCatalog.IsPreset(name)) return false;
            var deck = DeckCatalog.Load(name);
            return deck != null && PresetCardTypesReferenced(deck) < HiddenMirrorPresetTypeLimit;
        }

        /// <summary>卡组引用的预组卡种类数：卡组去重卡 ID 与三套预组（PresetDeckNames）卡 ID
        /// 并集的交集大小——种类而非数量（同卡多张计 1）。技能卡按保存闸口径须在 cardIds 内
        /// 天然计入；衍生物模板（tokenCardIds）不算引用。</summary>
        public static int PresetCardTypesReferenced(DeckData deck)
        {
            if (deck?.cardIds == null || deck.cardIds.Count == 0) return 0;
            var presetIds = new HashSet<string>();
            foreach (var preset in DeckCatalog.LoadPresets())
            {
                if (preset?.cardIds == null) continue;
                foreach (var id in preset.cardIds) presetIds.Add(id);
            }
            int types = 0;
            var seen = new HashSet<string>();
            foreach (var id in deck.cardIds)
            {
                if (id == null || !seen.Add(id) || !presetIds.Contains(id)) continue;
                types++;
            }
            return types;
        }
    }
}
