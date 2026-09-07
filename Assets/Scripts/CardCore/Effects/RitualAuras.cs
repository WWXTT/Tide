using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// 仪式光环组件（OCP 扩展点）：一个奖励（aura.effectId）一个类。
    /// 状态无关件（替代效果/规则钩子）在构造时统一注册、实时查询 CompletedAuras；
    /// 有状态件经 OnCompleted/OnRemoved 管理生命周期。
    /// 新奖励 = 新类 + 在 RitualComponents 登记，引擎热点与 RitualSystem 不改。
    /// </summary>
    public interface IRitualAura
    {
        string EffectId { get; }

        /// <summary>光环完成（常驻开始）。</summary>
        void OnCompleted(CompletedRitualAura aura);

        /// <summary>光环移除（自毁/重开局）。</summary>
        void OnRemoved(CompletedRitualAura aura);
    }

    /// <summary>光环组件公共查询。</summary>
    internal static class RitualAuraQueries
    {
        /// <summary>player 是否为指定 effectId 仪式的完成者。</summary>
        public static bool HasCompletedAura(Player player, string effectId)
        {
            if (player == null) return false;
            foreach (var aura in RitualSystem.CompletedAuras)
            {
                if (aura?.Definition?.aura?.effectId == effectId && aura.Completer == player)
                    return true;
            }
            return false;
        }
    }

    /// <summary>三相仪典：完成者每消耗 3 点相同纯色元素 → 获得红/蓝/绿各 1（守恒兑换，余数保留）。</summary>
    public sealed class ElementConversionAura : IRitualAura
    {
        public string EffectId => "ElementConversion";

        public ElementConversionAura()
        {
            EventManager.Instance.Subscribe<ElementPoolPayEvent>(OnElementPaid);
        }

        public void OnCompleted(CompletedRitualAura aura) { }
        public void OnRemoved(CompletedRitualAura aura) => aura?.SpendCounters.Clear();

        /// <summary>只计完成者自己的消耗（出牌支付与效果元素费两处发布都算）。</summary>
        private void OnElementPaid(ElementPoolPayEvent e)
        {
            var core = GameCore.Instance;
            if (core == null || e?.Player == null || e.PaidCost == null) return;

            foreach (var aura in RitualSystem.CompletedAuras)
            {
                if (aura?.Definition?.aura?.effectId != EffectId) continue;
                if (e.Player != aura.Completer) continue; // 完成者独享

                foreach (var kv in e.PaidCost)
                {
                    var color = (ManaType)kv.Key;
                    if (color != ManaType.Red && color != ManaType.Blue && color != ManaType.Green) continue;

                    aura.SpendCounters.TryGetValue(color, out var count);
                    count += (int)kv.Value;
                    while (count >= 3)
                    {
                        count -= 3;
                        GrantTrinity(core, aura.Completer, aura.Card);
                    }
                    aura.SpendCounters[color] = count;
                }
            }
        }

        /// <summary>红/蓝/绿各 +1 入 bank（照 AddManaHandler 先例：直写 AvailableMana + 发 AddManaEvent）。</summary>
        private static void GrantTrinity(GameCore core, Player completer, Entity source)
        {
            var bank = core.ElementPool.GetPool(completer).AvailableMana;
            foreach (var color in new[] { ManaType.Red, ManaType.Blue, ManaType.Green })
            {
                bank.TryGetValue(color, out var cur);
                bank[color] = cur + 1;
                EventManager.Instance.Publish(new AddManaEvent
                {
                    Player = completer,
                    ManaType = color,
                    Amount = 1,
                    Source = source
                });
            }
        }
    }

    /// <summary>血偿仪典：完成者支付生命代价时，改用对手的生命值支付（装饰器包装代价处理器）。</summary>
    public sealed class BloodPactAura : IRitualAura
    {
        public string EffectId => "BloodPact";

        private static bool _decoratorRegistered;

        public BloodPactAura()
        {
            // 覆盖注册生命代价装饰器（在 BuiltinCostHandlers.RegisterAll 之后挂载即可——
            // EnsureRegistered 由 GameCore.Reset 调用，晚于 GameCore 构造期的内置注册）。
            // 装饰器内部实时查询完成者状态：无血偿完成者时行为与原处理器完全一致。
            if (_decoratorRegistered) return;
            _decoratorRegistered = true;
            var original = CostHandlerRegistry.GetHandler(CostType.LifePayment);
            CostHandlerRegistry.Register(new Decorator(original));
        }

        public void OnCompleted(CompletedRitualAura aura) { }
        public void OnRemoved(CompletedRitualAura aura) { }

        /// <summary>payer 是否为某已完成血偿仪典的完成者（是 → 其生命代价转由对手承担）。</summary>
        public static bool IsCompleter(Player payer)
            => RitualAuraQueries.HasCompletedAura(payer, "BloodPact");

        /// <summary>装饰器：完成者支付生命代价时 Payer 换为对手（CanPay 同样看对手——可付到恰好归零，
        /// 归零=正常死亡，与基础处理器同口径）。LifePaymentCostEvent.Player = 实际失去生命的一方（对手）。</summary>
        private class Decorator : ICostHandler
        {
            private readonly ICostHandler _fallback;

            public Decorator(ICostHandler fallback) => _fallback = fallback;

            public CostType CostType => CostType.LifePayment;

            public bool CanPay(CostInstance cost, CostContext context)
            {
                if (IsCompleter(context?.Payer))
                {
                    var payer = context.Payer.Opponent;
                    return payer != null && payer.Life >= cost.Value;
                }
                return _fallback != null && _fallback.CanPay(cost, context);
            }

            public void Pay(CostInstance cost, CostContext context)
            {
                if (IsCompleter(context?.Payer))
                {
                    var payer = context.Payer.Opponent; // 实际支付者
                    payer.Life -= cost.Value;
                    EventManager.Instance.Publish(new LifePaymentCostEvent
                    {
                        Player = payer,
                        Amount = cost.Value,
                        Source = context.Source
                    });
                    // 转嫁同口径：可付到归零（=正常死亡），终局交连锁结束后统一检查（同基础处理器）
                    return;
                }
                _fallback?.Pay(cost, context);
            }

            public string GetDescription(CostInstance cost) => $"支付 {cost.Value} 点生命";
        }
    }

    /// <summary>丰盈仪典：完成者（角色）单次受到的伤害 ≤ 5（经替代引擎，见 OnGameReset 注册）。</summary>
    public sealed class DamageCapAura : IRitualAura
    {
        public string EffectId => "DamageCap";

        /// <summary>单次伤害封顶值（角色）。</summary>
        public const int CapValue = 5;

        public void OnCompleted(CompletedRitualAura aura) { }
        public void OnRemoved(CompletedRitualAura aura) { }

        /// <summary>伤害封顶替代：状态无关（实时查询完成者），随对局重挂（ReplacementEngine.ClearAll 之后）。</summary>
        internal static void RegisterReplacement(ReplacementEngine engine)
            => engine.RegisterReplacementEffect(new Replacement(), typeof(DamageEvent));

        private class Replacement : ReplacementEffectBase
        {
            public Replacement() : base(typeof(DamageEvent), "RitualDamageCap") { }

            public override bool CanReplace(IGameEvent e)
                => e is DamageEvent d
                   && d.Amount > CapValue
                   && d.Target is Player p
                   && RitualAuraQueries.HasCompletedAura(p, "DamageCap");

            public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
                => originalEvent is DamageEvent d
                    ? new DamageEvent { Source = d.Source, Target = d.Target, Amount = CapValue }
                    : null;
        }
    }

    /// <summary>窥渊仪典：每个回合开始时，完成者指定对手一张已展示的卡，直到回合结束不可使用。</summary>
    public sealed class LockRevealedAura : IRitualAura
    {
        public string EffectId => "LockRevealed";

        /// <summary>各玩家的「已展示」手牌集合（展示即记录；查询时惰性修剪离手卡）。</summary>
        private static readonly Dictionary<Player, HashSet<Card>> _revealedInHand = new Dictionary<Player, HashSet<Card>>();

        /// <summary>本回合被锁定不可使用的卡（回合结束清空）。</summary>
        private static readonly HashSet<Card> _lockedThisTurn = new HashSet<Card>();

        /// <summary>锁定的合法回合计数（提示跨回合竞态时弃权用）。</summary>
        private static int _currentTurnNumber = -1;

        public LockRevealedAura()
        {
            EventManager.Instance.Subscribe<RevealCardsEvent>(e => RecordRevealed(e?.Player, e?.Cards));
            EventManager.Instance.Subscribe<RevealHandEvent>(e => RecordRevealed(e?.Player, e?.Cards));
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStart);
            EventManager.Instance.Subscribe<TurnEndEvent>(e => _lockedThisTurn.Clear());

            // 规则扩展点（OCP）：锁定经出牌限制钩子接入 PlayCard
            RuleHooks.RegisterPlayRestriction(new LockRestriction());
        }

        public void OnCompleted(CompletedRitualAura aura) { }
        public void OnRemoved(CompletedRitualAura aura) { }

        /// <summary>卡是否本回合被锁定不可使用。</summary>
        public static bool IsLockedThisTurn(Card card) => card != null && _lockedThisTurn.Contains(card);

        /// <summary>展示记录（开局起采集，先于仪式完成也有效）。</summary>
        private static void RecordRevealed(Player revealedOwner, List<Card> cards)
        {
            if (revealedOwner == null || cards == null || cards.Count == 0) return;
            if (!_revealedInHand.TryGetValue(revealedOwner, out var set))
            {
                set = new HashSet<Card>();
                _revealedInHand[revealedOwner] = set;
            }
            foreach (var card in cards)
                set.Add(card);
        }

        /// <summary>某玩家当前仍在手且已被展示的卡（惰性修剪）。</summary>
        public static List<Card> GetRevealedInHand(Player revealedOwner)
        {
            var result = new List<Card>();
            if (revealedOwner == null || !_revealedInHand.TryGetValue(revealedOwner, out var set))
                return result;

            var hand = GameCore.Instance?.ZoneManager?.GetCards(revealedOwner, Zone.Hand);
            if (hand == null) return result;

            foreach (var card in set)
                if (hand.Contains(card))
                    result.Add(card);
            return result;
        }

        private static void OnTurnStart(TurnStartEvent e)
        {
            _currentTurnNumber = e.TurnNumber;

            foreach (var aura in RitualSystem.CompletedAuras)
            {
                if (aura?.Definition?.aura?.effectId != "LockRevealed") continue;
                PromptLockAsync(aura, e.TurnNumber).Forget();
            }
        }

        private static async UniTask PromptLockAsync(CompletedRitualAura aura, int turnNumber)
        {
            var completer = aura.Completer;
            var opponent = completer?.Opponent;
            if (opponent == null) return;

            var candidates = GetRevealedInHand(opponent);
            if (candidates.Count == 0) return;

            if (completer.IsAI || TargetSelectionService.Current == null)
            {
                _lockedThisTurn.Add(candidates[0]); // AI/无头：锁第一张
                return;
            }

            var labels = new List<string> { "不指定" };
            labels.AddRange(candidates.Select(c => c is IHasName named ? named.CardName : c.ToString()));
            int idx = await TargetSelectionService.RequestOneIndexAsync(
                completer, labels, "指定对手一张已展示的卡（本回合不可使用）");

            if (_currentTurnNumber != turnNumber) return; // 提示跨回合：弃权
            if (idx >= 1 && idx <= candidates.Count)
                _lockedThisTurn.Add(candidates[idx - 1]);
        }

        /// <summary>信息轴锁定：本回合被锁定的卡不可打出。</summary>
        private class LockRestriction : IPlayRestriction
        {
            public bool CanPlay(GameCore core, Player player, Card card, Zone fromZone)
                => !IsLockedThisTurn(card);
        }
    }

    /// <summary>归土仪典：每回合主要阶段一次，从自己的墓地使用一张牌，视为手牌中使用。</summary>
    public sealed class GraveyardPlayAura : IRitualAura
    {
        public string EffectId => "GraveyardPlay";

        /// <summary>本回合已用掉墓地出牌配额的玩家（回合结束清空）。</summary>
        private static readonly HashSet<Player> _usedThisTurn = new HashSet<Player>();

        public GraveyardPlayAura()
        {
            EventManager.Instance.Subscribe<TurnEndEvent>(e => _usedThisTurn.Clear());

            // 规则扩展点（OCP）：墓地作为出牌来源接入 PlayCard
            RuleHooks.RegisterPlaySource(new Source());
        }

        public void OnCompleted(CompletedRitualAura aura) { }
        public void OnRemoved(CompletedRitualAura aura) { }

        /// <summary>player 是否拥有墓地出牌光环。</summary>
        public static bool CanUse(Player player) => RitualAuraQueries.HasCompletedAura(player, "GraveyardPlay");

        /// <summary>尝试占用本回合的墓地出牌配额。</summary>
        public static bool TryBeginUse(Player player)
            => CanUse(player) && !_usedThisTurn.Contains(player) && _usedThisTurn.Add(player);

        /// <summary>墓地出牌来源（IPlaySource 实现）。</summary>
        private class Source : IPlaySource
        {
            public Zone SourceZone => Zone.Graveyard;
            public bool CanUse(Player player) => GraveyardPlayAura.CanUse(player);
            public bool TryBeginUse(Player player) => GraveyardPlayAura.TryBeginUse(player);
        }
    }

    /// <summary>疾风仪典：完成的回合结束时获得一次额外回合；额外回合结束时本仪式自毁（一次性奖励）。</summary>
    public sealed class TempoRitualAura : IRitualAura
    {
        public string EffectId => "ExtraTurnThenSelfDestruct";

        /// <summary>额外回合是否已授予（按光环卡记录；授予后的下一个完成者回合末自毁）。</summary>
        private static readonly Dictionary<Card, bool> _extraTurnGranted = new Dictionary<Card, bool>();

        public TempoRitualAura()
        {
            EventManager.Instance.Subscribe<TurnEndEvent>(OnTurnEnd);
        }

        public void OnCompleted(CompletedRitualAura aura) { }

        public void OnRemoved(CompletedRitualAura aura)
        {
            if (aura?.Card != null) _extraTurnGranted.Remove(aura.Card);
        }

        private void OnTurnEnd(TurnEndEvent e)
        {
            var core = GameCore.Instance;
            foreach (var aura in RitualSystem.CompletedAuras.ToList())
            {
                if (aura?.Definition?.aura?.effectId != EffectId) continue;
                if (e.TurnPlayer != aura.Completer) continue;

                _extraTurnGranted.TryGetValue(aura.Card, out var granted);
                if (!granted)
                {
                    // 完成回合（或之后首个完成者回合）结束：获得一次额外回合
                    core?.TurnEngine.GrantExtraTurn(aura.Completer);
                    _extraTurnGranted[aura.Card] = true;
                }
                else
                {
                    // 额外回合结束：仪式自毁（一次性奖励，不形成常驻光环）
                    SelfDestruct(aura);
                }
            }
        }

        /// <summary>仪式自毁：摘除完成态关键词 → 入墓 → 移除光环（触发 OnRemoved）→ 发销毁事件。</summary>
        private static void SelfDestruct(CompletedRitualAura aura)
        {
            var card = aura.Card;
            var owner = card?.GetController();
            var core = GameCore.Instance;

            card.RemoveKeyword(KeywordRules.Indestructible);
            card.RemoveKeyword(KeywordRules.Untargetable);
            if (core?.ZoneManager != null && owner != null)
                core.ZoneManager.MoveCard(card, owner, Zone.Battlefield, Zone.Graveyard);

            RitualSystem.RemoveCompleted(aura);
            EventManager.Instance.Publish(new CardDestroyEvent
            {
                DestroyedCard = card,
                Reason = DestroyReason.Destroyed,
                Source = card
            });
        }
    }

    /// <summary>纳川仪典：手牌上限提升到 15 + 免疫空库抽牌疲劳（均经规则扩展点接入）。</summary>
    public sealed class HandLimitAura : IRitualAura
    {
        public string EffectId => "HandLimitAndNoFatigue";

        /// <summary>提升后的手牌上限。</summary>
        public const int BoostedHandLimit = 15;

        public HandLimitAura()
        {
            // 规则扩展点（OCP）：手牌上限修改链接入
            RuleHooks.RegisterHandLimitModifier(new Modifier());
        }

        public void OnCompleted(CompletedRitualAura aura) { }
        public void OnRemoved(CompletedRitualAura aura) { }

        /// <summary>完成者免疫空库抽牌的疲劳伤害。</summary>
        public static bool HasFatigueImmunity(Player player)
            => RitualAuraQueries.HasCompletedAura(player, "HandLimitAndNoFatigue");

        /// <summary>疲劳免疫替代：状态无关，随对局重挂（ReplacementEngine.ClearAll 之后）。</summary>
        internal static void RegisterReplacement(ReplacementEngine engine)
            => engine.RegisterReplacementEffect(new Replacement(), typeof(CardCore.Attribute.FatigueEvent));

        private class Modifier : IHandLimitModifier
        {
            public int Modify(Player player, int currentLimit)
                => RitualAuraQueries.HasCompletedAura(player, "HandLimitAndNoFatigue") ? BoostedHandLimit : currentLimit;
        }

        private class Replacement : ReplacementEffectBase
        {
            public Replacement() : base(typeof(CardCore.Attribute.FatigueEvent), "RitualFatigueImmunity") { }

            public override bool CanReplace(IGameEvent e)
                => e is CardCore.Attribute.FatigueEvent f
                   && f.Damage > 0
                   && f.Player != null
                   && HasFatigueImmunity(f.Player);

            public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
                => originalEvent is CardCore.Attribute.FatigueEvent f
                    ? new CardCore.Attribute.FatigueEvent { Player = f.Player, Damage = 0 }
                    : null;
        }
    }

    /// <summary>
    /// 仪式组件启动注册中心（仿 BuiltinCostHandlers.RegisterAll 惯例）：
    /// 登记任务跟踪器与光环组件；由 RitualSystem.EnsureRuntime（GameCore.Reset）调用，幂等。
    /// </summary>
    public static class RitualComponents
    {
        private static bool _registered;

        // ---- 任务跟踪器（task.kind → 组件） ----
        public static ColorStreakTracker ColorStreak { get; private set; }
        public static LifePaidAccumTracker LifePaid { get; private set; }
        public static HealOverflowAccumTracker HealOverflow { get; private set; }
        public static RevealAccumTracker RevealAccum { get; private set; }
        public static MillSelfAccumTracker MillSelf { get; private set; }
        public static SkipStandbyCountTracker SkipStandby { get; private set; }
        public static NonDrawDrawAccumTracker NonDrawDraw { get; private set; }

        // ---- 光环组件（aura.effectId → 组件） ----
        public static ElementConversionAura ElementConversion { get; private set; }
        public static BloodPactAura BloodPact { get; private set; }
        public static DamageCapAura DamageCap { get; private set; }
        public static LockRevealedAura LockRevealed { get; private set; }
        public static GraveyardPlayAura GraveyardPlay { get; private set; }
        public static TempoRitualAura Tempo { get; private set; }
        public static HandLimitAura HandLimit { get; private set; }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            // 任务跟踪器
            ColorStreak = new ColorStreakTracker();
            LifePaid = new LifePaidAccumTracker();
            HealOverflow = new HealOverflowAccumTracker();
            RevealAccum = new RevealAccumTracker();
            MillSelf = new MillSelfAccumTracker();
            SkipStandby = new SkipStandbyCountTracker();
            NonDrawDraw = new NonDrawDrawAccumTracker();

            // 光环组件（构造即挂各自的订阅与规则钩子）
            ElementConversion = new ElementConversionAura();
            BloodPact = new BloodPactAura();
            DamageCap = new DamageCapAura();
            LockRevealed = new LockRevealedAura();
            GraveyardPlay = new GraveyardPlayAura();
            Tempo = new TempoRitualAura();
            HandLimit = new HandLimitAura();

            RitualSystem.RegisterTracker(ColorStreak);
            RitualSystem.RegisterTracker(LifePaid);
            RitualSystem.RegisterTracker(HealOverflow);
            RitualSystem.RegisterTracker(RevealAccum);
            RitualSystem.RegisterTracker(MillSelf);
            RitualSystem.RegisterTracker(SkipStandby);
            RitualSystem.RegisterTracker(NonDrawDraw);

            RitualSystem.RegisterAura(ElementConversion);
            RitualSystem.RegisterAura(BloodPact);
            RitualSystem.RegisterAura(DamageCap);
            RitualSystem.RegisterAura(LockRevealed);
            RitualSystem.RegisterAura(GraveyardPlay);
            RitualSystem.RegisterAura(Tempo);
            RitualSystem.RegisterAura(HandLimit);
        }

        /// <summary>
        /// 对局重置钩子（RitualSystem.Reset 转发，时序在 GameCore.Reset 的 ReplacementEngine.ClearAll 之后）：
        /// 重挂状态无关替代件。
        /// </summary>
        public static void OnGameReset()
        {
            var engine = GameCore.Instance?.ReplacementEngine;
            if (engine != null)
            {
                DamageCapAura.RegisterReplacement(engine);
                HandLimitAura.RegisterReplacement(engine);
            }
        }
    }
}
