using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore
{
    // =====================================================================================
    // 每卡 30 次 → 过载指示物（2026-11-09 裁定，草案 §11；2026-11-10 实现）。
    //
    // 口径（§11.1-11.5，决议 §11.9 全部确认）：
    //   · 计数单位=卡实例：同一张卡一回合累计**发动/触发 30 次**（组合主动/自动、攻击/守卫宣言、
    //     高危 11 关键词）后挂系统指示物「过载」——主动/自动/自动触发型关键词失效、攻守拒绝宣言。
    //   · 指示物**完全移出**计数与压制：圣盾/护甲/坚韧/易损/毒素/荆棘等照自己的层数规则工作；
    //     非高危关键词（先攻/连击/警戒/守护等静态规则）不受压。
    //   · 高危关键词**结算前 CanCommit、结算后 Commit**——第 30 次本身照常结算；
    //     主动/触发在入栈/入队口计数，本次结算不受自己刚挂的过载影响。
    //   · 过载不可净化、不可换区清除（ClearCounters 例外），只在下回合开始移除。
    //   · 30 闸是系统断言不是收敛策略——循环收敛仍由 Chain 的 LoopCost/StateRepeat 承担。
    //   · 单例静态（EffectExecutor.OnNewTurn/Reset 驱动回合与对局重置——§11.7 回合重置行）。
    // =====================================================================================

    /// <summary>计入种类（事件遥测用）。</summary>
    public enum CardEffectCommitKind
    {
        Active,    // 组合主动效果
        Triggered, // 触发式（含光环授予——计入受益者，§11.2.2）
        Attack,    // 攻击宣言
        Guard,     // 守卫宣言
        Keyword,   // 高危关键词触发（结算后提交）
    }

    /// <summary>过载挂载（第 30 次 Commit 时发布）。</summary>
    public class CardOverloadedEvent : GameEventBase
    {
        public Card Card;
        public int Count;
        public CardEffectCommitKind Kind;
    }

    /// <summary>预算读数变化（UI x/y 读数与过载态同步）。</summary>
    public class CardEffectBudgetChangedEvent : GameEventBase
    {
        public Card Card;
        public int Count;
        public bool Overloaded;
        public CardEffectCommitKind Kind;
    }

    /// <summary>每卡效果预算（§11.6 接口照录 + 关键词口助手）。计数字典是本回合台账；
    /// 过载态以卡上的 Overload 指示物为权威（可见/可播报/可被快照读取）。</summary>
    public sealed class CardEffectBudget
    {
        public const int Limit = 30;

        public static readonly CardEffectBudget Instance = new CardEffectBudget();

        private readonly Dictionary<Card, int> _count = new Dictionary<Card, int>();
        private readonly HashSet<Card> _overloaded = new HashSet<Card>(); // 仅用于回合末清指示物

        private CardEffectBudget() { }

        public int Count(Card card)
            => card != null && _count.TryGetValue(card, out var n) ? n : 0;

        /// <summary>过载读数：只读指示物层（System 指示物不受绝缘体豁免——§11.4）。</summary>
        public bool IsOverloaded(Card card)
            => card != null && card.GetCounterCount(CounterRules.OverloadCounter) > 0;

        public bool CanCommit(Card card, out string reason)
        {
            reason = null;
            if (card == null) return true;
            if (IsOverloaded(card))
            {
                reason = "过载：本回合主动/自动效果已失效";
                return false;
            }
            return true;
        }

        public bool Commit(Card card, CardEffectCommitKind kind, out string reason)
        {
            if (!CanCommit(card, out reason)) return false;

            int n = Count(card) + 1;
            _count[card] = n;

            if (n >= Limit && !IsOverloaded(card))
            {
                card.AddCounters(CounterRules.OverloadCounter, 1, card);
                _overloaded.Add(card);
                EventManager.Instance.Publish(new CardOverloadedEvent
                {
                    Card = card,
                    Count = n,
                    Kind = kind,
                });
            }

            EventManager.Instance.Publish(new CardEffectBudgetChangedEvent
            {
                Card = card,
                Count = n,
                Overloaded = IsOverloaded(card),
                Kind = kind,
            });
            return true;
        }

        /// <summary>退款（发动层无效）：计数回落，低于上限时移除过载（边界情况，记日志）。</summary>
        public void Refund(Card card)
        {
            if (card == null) return;
            int n = Count(card);
            if (n <= 0) return;

            n--;
            if (n <= 0) _count.Remove(card); else _count[card] = n;

            if (n < Limit && IsOverloaded(card))
            {
                int layers = card.GetCounterCount(CounterRules.OverloadCounter);
                if (layers > 0) card.AddCounters(CounterRules.OverloadCounter, -layers, card);
                _overloaded.Remove(card);
                TideLog.Warn($"[Overload] 退款移除过载：{card.ID} count={n}（边界情况）");
            }

            EventManager.Instance.Publish(new CardEffectBudgetChangedEvent
            {
                Card = card,
                Count = n,
                Overloaded = IsOverloaded(card),
                Kind = CardEffectCommitKind.Active,
            });
        }

        /// <summary>回合开始：移除已挂卡的过载指示物、清空本回合台账（§11.4）。</summary>
        public void OnNewTurn(int turnNumber)
        {
            foreach (var card in _overloaded)
            {
                int layers = card?.GetCounterCount(CounterRules.OverloadCounter) ?? 0;
                if (layers > 0) card.AddCounters(CounterRules.OverloadCounter, -layers, null);
            }
            _overloaded.Clear();
            _count.Clear();
        }

        /// <summary>对局重置（EffectExecutor.Reset 驱动）：清台账（新局新卡实例，旧指示物随旧卡消亡）。</summary>
        public void Reset()
        {
            _overloaded.Clear();
            _count.Clear();
        }

        // ============================== 高危关键词口（§11.3：结算前闸、结算后计） ==============================

        /// <summary>关键词触发闸：非卡（玩家/系统/空）不计不压（§11.2.2 授予玩家默认跳过）；
        /// 过载期间自动触发型关键词不触发（§11.5）。返回 false = 跳过该关键词块。</summary>
        public static bool KeywordAllowed(Entity owner)
        {
            if (!(owner is Card card)) return true;
            return Instance.CanCommit(card, out _);
        }

        /// <summary>关键词结算后提交计数（每次触发=1；第 30 次本身照常结算）。</summary>
        public static void CommitKeyword(Entity owner)
        {
            if (owner is Card card)
                Instance.Commit(card, CardEffectCommitKind.Keyword, out _);
        }
    }
}
