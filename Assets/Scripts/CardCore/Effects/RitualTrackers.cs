using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 竞速任务跟踪器（OCP 扩展点）：一个任务类型（task.kind）一个类——
    /// 自订阅事件源、自持进度状态、达标时调 RitualSystem.Complete。
    /// 新任务类型 = 新类 + 在 RitualComponents 登记，RitualSystem 与引擎热点不改。
    /// 订阅一次性挂载（RitualComponents.EnsureRegistered），事件回调内按 Kind 守卫。
    /// </summary>
    public interface IRitualTaskTracker
    {
        string Kind { get; }

        /// <summary>任务开始（新 ActiveRitual 挂载）：进度清零。</summary>
        void OnStarted(ActiveRitual ritual);

        /// <summary>任务结束（完成/被顶掉/破坏/重开局）：进度清空。</summary>
        void OnEnded(ActiveRitual ritual);
    }

    /// <summary>累计型任务基类：每玩家计数 + 达标完成。</summary>
    public abstract class AccumRitualTrackerBase : IRitualTaskTracker
    {
        protected readonly Dictionary<Player, int> Count = new Dictionary<Player, int>();

        public abstract string Kind { get; }

        public virtual void OnStarted(ActiveRitual ritual) => Count.Clear();
        public virtual void OnEnded(ActiveRitual ritual) => Count.Clear();

        /// <summary>累计 n 并在达标时完成（当前任务非本 kind 或无效入参时忽略）。</summary>
        protected void Accumulate(Player player, int n)
        {
            var active = RitualSystem.Active;
            if (active?.Definition?.task?.kind != Kind || player == null || n <= 0) return;

            Count.TryGetValue(player, out var total);
            total += n;
            Count[player] = total;

            if (total >= active.Definition.task.target)
                RitualSystem.Complete(active, player);
        }

        /// <summary>玩家当前累计进度（验证/UI 用）。</summary>
        public int GetCount(Player player) => player != null && Count.TryGetValue(player, out var v) ? v : 0;
    }

    /// <summary>
    /// 差值化累计基类（P2a 定案）：任务进度 = MatchStatsService 全局计数 − 任务开始基线。
    /// 事件订阅保留（仅用于即时判达标），计数本身读服务——单一计数源，仪式/卡条件/AI 三方读同一份。
    /// 依赖订阅顺序：服务先订阅（组合根注册先于 RitualComponents.EnsureRegistered），
    /// 事件回调到本类时服务值已含本事件，达标判断不差一笔。
    /// </summary>
    public abstract class StatsRitualTrackerBase : IRitualTaskTracker
    {
        private readonly Dictionary<Player, int> _baseline = new Dictionary<Player, int>();

        public abstract string Kind { get; }

        /// <summary>对应的 MatchStatsService statId（词汇表同源）</summary>
        protected abstract string StatId { get; }

        public virtual void OnStarted(ActiveRitual ritual)
        {
            _baseline.Clear();
            var svc = MatchStatsService.Instance;
            if (svc == null || ritual?.Owner == null) return;
            _baseline[ritual.Owner] = svc.GetStat(ritual.Owner, StatId);
            if (ritual.Owner.Opponent != null)
                _baseline[ritual.Owner.Opponent] = svc.GetStat(ritual.Owner.Opponent, StatId);
        }

        public virtual void OnEnded(ActiveRitual ritual) => _baseline.Clear();

        /// <summary>事件回调：判达标（当前任务非本 kind 或无效入参时忽略）。</summary>
        protected void TryComplete(Player player)
        {
            var active = RitualSystem.Active;
            if (active?.Definition?.task?.kind != Kind || player == null) return;
            if (GetCount(player) >= active.Definition.task.target)
                RitualSystem.Complete(active, player);
        }

        /// <summary>玩家当前任务进度（全局计数 − 基线）。验证/UI 用。</summary>
        public int GetCount(Player player)
        {
            var svc = MatchStatsService.Instance;
            if (svc == null || player == null) return 0;
            _baseline.TryGetValue(player, out var b);
            return svc.GetStat(player, StatId) - b;
        }
    }

    /// <summary>LifePaidAccum：累计通过代价支付生命值（血偿仪典）。</summary>
    public sealed class LifePaidAccumTracker : StatsRitualTrackerBase
    {
        public override string Kind => "LifePaidAccum";
        protected override string StatId => MatchStatsService.LifePaid;

        public LifePaidAccumTracker()
        {
            EventManager.Instance.Subscribe<LifePaymentCostEvent>(e => TryComplete(e?.Player));
        }
    }

    /// <summary>HealOverflowAccum：角色治疗溢出累计（丰盈仪典；溢出=治疗超上限截断部分）。</summary>
    public sealed class HealOverflowAccumTracker : StatsRitualTrackerBase
    {
        public override string Kind => "HealOverflowAccum";
        protected override string StatId => MatchStatsService.HealOverflow;

        public HealOverflowAccumTracker()
        {
            EventManager.Instance.Subscribe<HealEvent>(e =>
            {
                if (e?.Target is Player player && e.Overfill > 0)
                    TryComplete(player);
            });
        }
    }

    /// <summary>RevealAccum：展示对手手牌张数累计（窥渊仪典；展示者视角，离开手牌/被使用不影响）。</summary>
    public sealed class RevealAccumTracker : StatsRitualTrackerBase
    {
        public override string Kind => "RevealAccum";
        protected override string StatId => MatchStatsService.CardsRevealed;

        public RevealAccumTracker()
        {
            EventManager.Instance.Subscribe<RevealCardsEvent>(e => CountReveal(e?.Player, e?.Source));
            EventManager.Instance.Subscribe<RevealHandEvent>(e => CountReveal(e?.Player, e?.Source));
        }

        private void CountReveal(Player revealedOwner, Entity source)
        {
            var revealer = source?.GetController();
            if (revealer == null || revealedOwner == null || revealedOwner != revealer.Opponent) return;
            TryComplete(revealer);
        }
    }

    /// <summary>MillSelfAccum：自己卡组送墓张数累计（归土仪典；效果送墓每张一发 + 代价送墓一次 N 张）。</summary>
    public sealed class MillSelfAccumTracker : StatsRitualTrackerBase
    {
        public override string Kind => "MillSelfAccum";
        protected override string StatId => MatchStatsService.MilledSelf;

        public MillSelfAccumTracker()
        {
            EventManager.Instance.Subscribe<CardMillEvent>(e => TryComplete(e?.Player));
            EventManager.Instance.Subscribe<MillDeckCostEvent>(e => TryComplete(e?.Player));
        }
    }

    /// <summary>NonDrawDrawAccum：非抽牌形式加入手牌张数累计（纳川仪典；抽牌路径 IsDraw=true 不计）。</summary>
    public sealed class NonDrawDrawAccumTracker : StatsRitualTrackerBase
    {
        public override string Kind => "NonDrawDrawAccum";
        protected override string StatId => MatchStatsService.NonDrawCardsGained;

        public NonDrawDrawAccumTracker()
        {
            EventManager.Instance.Subscribe<CardEnterHandEvent>(e =>
            {
                if (e?.Player != null && e.Card != null && !e.IsDraw)
                    TryComplete(e.Player);
            });
        }
    }

    /// <summary>
    /// SkipStandbyCount：跳过自己准备阶段次数（疾风仪典）。
    /// 同时持有宣告机制（回合结束阶段预先宣告 → 下次己方回合开始消费）与
    /// 「跳过准备阶段」规则拦截器（RuleHooks 扩展点）。
    /// </summary>
    public sealed class SkipStandbyCountTracker : AccumRitualTrackerBase
    {
        public override string Kind => "SkipStandbyCount";

        /// <summary>已宣告「跳过自己下个回合准备阶段」的玩家。</summary>
        private static readonly HashSet<Player> _pending = new HashSet<Player>();

        public SkipStandbyCountTracker()
        {
            EventManager.Instance.Subscribe<StandbySkippedEvent>(e => Accumulate(e?.Player, 1));
            EventManager.Instance.Subscribe<TurnEndEvent>(e => PromptSkipStandbyAsync(e?.TurnPlayer).Forget());

            // 规则扩展点（OCP）：经 RuleHooks 接入 GameCore 回合开始热点
            RuleHooks.RegisterTurnStartInterceptor(new Interceptor());
        }

        public override void OnEnded(ActiveRitual ritual)
        {
            base.OnEnded(ritual);
            _pending.Clear(); // 宣告随任务消失作废
        }

        /// <summary>玩家已执行的跳过次数（验证用）。</summary>
        public int GetSkipCount(Player player) => GetCount(player);

        /// <summary>宣告跳过自己下个回合的准备阶段（回合结束阶段调用）。</summary>
        public void Commit(Player player)
        {
            if (player != null && CanOffer())
                _pending.Add(player);
        }

        /// <summary>
        /// 消费宣告：该玩家本回合准备阶段自动化整体跳过。
        /// 任务已不在（被顶掉/破坏）时宣告作废——跳过失去意义，不执行。
        /// </summary>
        public bool Consume(Player player)
        {
            if (player == null || !_pending.Remove(player)) return false;
            return CanOffer();
        }

        private static bool CanOffer()
            => RitualSystem.Active?.Definition?.task?.kind == "SkipStandbyCount";

        /// <summary>
        /// 回合结束阶段的宣告提示：「跳过自己下个回合的准备阶段」。
        /// AI 直接宣告；人类经选择面板；无 UI 自动放弃。
        /// 注：准备阶段自动化发生在 TurnStartEvent 同步链上、其间无法弹选择——宣告时机前置到回合结束。
        /// </summary>
        private static async UniTask PromptSkipStandbyAsync(Player player)
        {
            if (player == null || !CanOffer() || _pending.Contains(player)) return;

            if (player.IsAI)
            {
                _pending.Add(player); // SimpleAI：有竞速就推进
                return;
            }

            var labels = new List<string>
            {
                "不跳过（保留准备阶段）",
                "跳过：推进竞速，放弃下回合的抽牌/地牌上限/重置"
            };
            int idx = await TargetSelectionService.RequestOneIndexAsync(player, labels, "跳过准备阶段（宣告）");
            if (idx == 1)
                _pending.Add(player);
        }

        /// <summary>「跳过准备阶段」拦截器（规则扩展点实现）。</summary>
        private class Interceptor : ITurnStartInterceptor
        {
            public bool ShouldSkipTurnStartAutomation(Player player)
                => RitualComponents.SkipStandby.Consume(player);
        }
    }

    /// <summary>
    /// ColorStreak：颜色连击断言（三相仪典）——连续 N 个回合，本回合用卡纯色与前 window 回合不重复。
    /// 口径：「使用的卡」只认 PlayCard 打出的卡；颜色只看纯色（红/蓝/绿），灰不计；
    /// 多色卡按其全部纯色计；只记回合玩家自己的出牌；空回合视为断言失败。
    /// </summary>
    public sealed class ColorStreakTracker : IRitualTaskTracker
    {
        public string Kind => "ColorStreak";

        private readonly Dictionary<Player, int> _streak = new Dictionary<Player, int>();
        private readonly Dictionary<Player, List<HashSet<ManaType>>> _recentColors = new Dictionary<Player, List<HashSet<ManaType>>>();
        private HashSet<ManaType> _turnColors = new HashSet<ManaType>();
        private Player _turnPlayer;

        public ColorStreakTracker()
        {
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStart);
            EventManager.Instance.Subscribe<CardPlayEvent>(OnCardPlayed);
            EventManager.Instance.Subscribe<TurnEndEvent>(OnTurnEnd);
        }

        public void OnStarted(ActiveRitual ritual)
        {
            _streak.Clear();
            _recentColors.Clear();
            // 中途打出的仪式立即接入当前回合：TurnStartEvent 已过，不等下回合才初始化用卡口径
            //（打出前同回合的用卡不计）
            _turnPlayer = GameCore.Instance?.TurnEngine?.TurnPlayer;
            _turnColors = new HashSet<ManaType>();
        }

        public void OnEnded(ActiveRitual ritual)
        {
            _streak.Clear();
            _recentColors.Clear();
            _turnColors = new HashSet<ManaType>();
            _turnPlayer = null;
        }

        /// <summary>玩家当前连续达标回合数（验证用）。</summary>
        public int GetStreak(Player player) => player != null && _streak.TryGetValue(player, out var v) ? v : 0;

        private void OnTurnStart(TurnStartEvent e)
        {
            if (RitualSystem.Active?.Definition?.task?.kind != Kind) return;
            _turnPlayer = e.TurnPlayer;
            _turnColors = new HashSet<ManaType>();
        }

        private void OnCardPlayed(CardPlayEvent e)
        {
            if (RitualSystem.Active?.Definition?.task?.kind != Kind) return;
            if (e?.Player == null || e.Player != _turnPlayer || !(e.PlayedCard is Card card)) return;

            foreach (var color in RitualSystem.GetPureCostColors(card))
                _turnColors.Add(color);
        }

        private void OnTurnEnd(TurnEndEvent e)
        {
            var active = RitualSystem.Active;
            if (active?.Definition?.task?.kind != Kind) return;
            if (e?.TurnPlayer == null || e.TurnPlayer != _turnPlayer) return;

            var task = active.Definition.task;
            int window = System.Math.Max(1, task.window);

            if (!_recentColors.TryGetValue(e.TurnPlayer, out var history))
            {
                history = new List<HashSet<ManaType>>();
                _recentColors[e.TurnPlayer] = history;
            }

            // 断言：本回合用卡纯色非空，且与前 window 回合的用卡纯色不重复。
            bool pass = _turnColors.Count > 0 && history.Take(window).All(s => !s.Overlaps(_turnColors));

            _streak.TryGetValue(e.TurnPlayer, out var streak);
            streak = pass ? streak + 1 : 0;
            _streak[e.TurnPlayer] = streak;

            // 无论断言成败，本回合实际用过的颜色入历史（事实记录）
            if (_turnColors.Count > 0)
            {
                history.Insert(0, new HashSet<ManaType>(_turnColors));
                if (history.Count > window) history.RemoveAt(window);
            }

            if (streak >= task.target)
                RitualSystem.Complete(active, e.TurnPlayer);
        }
    }
}
