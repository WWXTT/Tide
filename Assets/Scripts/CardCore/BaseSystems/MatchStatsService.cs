using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>计数作用域：本回合（每个 TurnStart 清零）/ 本局（对局期间累计）</summary>
    public enum StatScope
    {
        ThisTurn,
        ThisGame
    }

    /// <summary>
    /// 对局史计数服务（P2a 定案）——对局行为的全局累计，供三方共用查询：
    /// ① 仪式任务进度（RitualTrackers 差值化消费）② 卡牌效果条件（ConditionType.Custom，
    /// StringValue=statId）③ AI 决策。
    ///
    /// 形态仿 ResourceLedger：实例子系统（组合根注册 + Reset 清数据 + 幂等重挂订阅），
    /// 构造即订阅（一次性挂载）；stat 词汇表与 RitualTrackers 的 7 个 kind 一一同源
    /// （事件→计数归属口径逐条对齐，tracker 差值化后二者读同一份计数，无双份漂移）。
    /// 「任务期间」语义不进本服务——归 tracker 的 baseline 窗口。
    /// </summary>
    public class MatchStatsService
    {
        /// <summary>查询便捷口（条件求值/AI 用；组合根注册后可用）</summary>
        public static MatchStatsService Instance => GameCore.Instance?.MatchStats;

        private readonly Dictionary<Player, Dictionary<string, int>> _game =
            new Dictionary<Player, Dictionary<string, int>>();
        private readonly Dictionary<Player, Dictionary<string, int>> _turn =
            new Dictionary<Player, Dictionary<string, int>>();
        private bool _subscribed;

        public MatchStatsService()
        {
            EnsureSubscribed();
        }

        // ======================================== stat 词汇表（与 RitualTrackers kind 同源） ========================================

        public const string LifePaid = "LifePaid";                       // 通过代价支付生命值（血偿）
        public const string HealOverflow = "HealOverflow";               // 角色治疗溢出（丰盈）
        public const string CardsRevealed = "CardsRevealed";             // 展示对手手牌张数（窥渊，展示者视角）
        public const string MilledSelf = "MilledSelf";                   // 自己卡组送墓张数（归土）
        public const string NonDrawCardsGained = "NonDrawCardsGained";   // 非抽牌形式加入手牌张数（纳川）
        public const string StandbySkipped = "StandbySkipped";           // 跳过自己准备阶段次数（疾风）
        public const string CreaturesDied = "CreaturesDied";             // 己方随从死亡次数
        public const string CardsPlayed = "CardsPlayed";                 // 使用卡牌次数（使用宣言）
        public const string SpellsCast = "SpellsCast";                   // 施放法术次数
        public const string DamageDealt = "DamageDealt";                 // 造成伤害量（点）
        public const string DamageTaken = "DamageTaken";                 // 受到伤害量（点）
        public const string ElementsSpent = "ElementsSpent";             // 支付元素总量

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;

            var em = EventManager.Instance;
            em.Subscribe<TurnStartEvent>(OnTurnStart);

            // —— 与 RitualTrackers 同源的 5 个累计口径 ——
            em.Subscribe<LifePaymentCostEvent>(e => Add(e?.Player, LifePaid, e?.Amount ?? 0));
            em.Subscribe<HealEvent>(e =>
            {
                if (e?.Target is Player player && e.Overfill > 0)
                    Add(player, HealOverflow, e.Overfill);
            });
            em.Subscribe<RevealCardsEvent>(e => CountReveal(e?.Player, e?.Cards, e?.Source));
            em.Subscribe<RevealHandEvent>(e => CountReveal(e?.Player, e?.Cards, e?.Source));
            em.Subscribe<CardMillEvent>(e => Add(e?.Player, MilledSelf, 1));
            em.Subscribe<MillDeckCostEvent>(e => Add(e?.Player, MilledSelf, e?.Cards?.Count ?? 0));
            em.Subscribe<CardEnterHandEvent>(e =>
            {
                if (e?.Player != null && e.Card != null && !e.IsDraw)
                    Add(e.Player, NonDrawCardsGained, 1);
            });
            em.Subscribe<StandbySkippedEvent>(e => Add(e?.Player, StandbySkipped, 1));

            // —— 扩展词汇（无仪式对应，供卡条件/AI） ——
            em.Subscribe<CardDestroyEvent>(e =>
            {
                if (e?.DestroyedCard is IHasSupertype st && st.Supertype == Cardtype.Creature)
                    Add(OwnerOf(e.DestroyedCard), CreaturesDied, 1);
            });
            em.Subscribe<CardPlayEvent>(e =>
            {
                if (e?.Player == null) return;
                Add(e.Player, CardsPlayed, 1);
                if (e.PlayedCard is IHasSupertype st && st.Supertype == Cardtype.Spell)
                    Add(e.Player, SpellsCast, 1);
            });
            em.Subscribe<DamageEvent>(e =>
            {
                if (e == null) return;
                Add(ControllerOf(e.Source), DamageDealt, Math.Max(0, e.Amount));
                Add(ControllerOf(e.Target), DamageTaken, Math.Max(0, e.Amount));
            });
            em.Subscribe<ElementPoolPayEvent>(e =>
            {
                if (e?.Player == null || e.PaidCost == null) return;
                Add(e.Player, ElementsSpent, (int)e.PaidCost.Values.Sum(v => Math.Max(0f, v)));
            });
        }

        /// <summary>展示对手手牌（展示者视角，与 RevealAccumTracker 口径一致）</summary>
        private void CountReveal(Player revealedOwner, List<Card> cards, Entity source)
        {
            var revealer = source?.GetController();
            if (revealer == null || revealedOwner == null || revealedOwner != revealer.Opponent) return;
            Add(revealer, CardsRevealed, cards?.Count ?? 0);
        }

        private static Player OwnerOf(Card card) => card?.GetOwner() ?? card?.GetController();

        private static Player ControllerOf(Entity entity)
        {
            switch (entity)
            {
                case null: return null;
                case Card card: return card.GetController();
                case Player player: return player;
                default: return entity.GetController();
            }
        }

        // ======================================== 查询与维护 ========================================

        /// <summary>查询某玩家某 stat（scope 默认本局；未知 statId 恒 0）</summary>
        public int GetStat(Player player, string statId, StatScope scope = StatScope.ThisGame)
        {
            if (player == null || string.IsNullOrEmpty(statId)) return 0;
            var dict = scope == StatScope.ThisTurn ? _turn : _game;
            return dict.TryGetValue(player, out var stats) && stats.TryGetValue(statId, out var v) ? v : 0;
        }

        /// <summary>全部 stat 快照（调试/验证/日志用）</summary>
        public Dictionary<string, int> GetSnapshot(Player player, StatScope scope = StatScope.ThisGame)
        {
            var dict = scope == StatScope.ThisTurn ? _turn : _game;
            return dict.TryGetValue(player, out var stats)
                ? new Dictionary<string, int>(stats)
                : new Dictionary<string, int>();
        }

        /// <summary>清空（GameCore.Reset 调用；幂等重挂订阅防 ClearAll 后静默漏挂）</summary>
        public void ClearAll()
        {
            _game.Clear();
            _turn.Clear();
            // EventManager.ClearAll 会清光订阅且当前无人调用；若未来接入，幂等旗标在此复位保证重挂
            //（订阅本身一次性挂载，与 RitualComponents 同模式）
        }

        // ======================================== 内部 ========================================

        private void Add(Player player, string statId, int amount)
        {
            if (player == null || amount <= 0) return;
            Bump(_game, player, statId, amount);
            Bump(_turn, player, statId, amount);
        }

        private static void Bump(Dictionary<Player, Dictionary<string, int>> dict, Player player, string statId, int amount)
        {
            if (!dict.TryGetValue(player, out var stats))
            {
                stats = new Dictionary<string, int>();
                dict[player] = stats;
            }
            stats.TryGetValue(statId, out var v);
            stats[statId] = v + amount;
        }

        private void OnTurnStart(TurnStartEvent e)
        {
            // 本回合层清零（全局回合口径：双方共享同一个"当前回合"）
            _turn.Clear();
        }
    }
}
