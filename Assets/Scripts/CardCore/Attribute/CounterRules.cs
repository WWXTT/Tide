using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Attribute
{
    /// <summary>
    /// 指示物极性（定案）：正面=增益/资源，负面=减益/妨害。
    /// </summary>
    public enum CounterPolarity
    {
        /// <summary>正面（增益/资源类：护甲、+1/+1 等）</summary>
        Positive,
        /// <summary>负面（减益/妨害类：剧毒、毒素、冻结、沉默、突袭紊乱、-1/-1 等）</summary>
        Negative,
    }

    /// <summary>
    /// 回合结束时指示物触发的效果类别（指示物=不可编辑、效果固定）：
    /// 剧毒=持有者死亡；毒素=每层 1 伤。
    /// </summary>
    public enum CounterTurnEndEffect
    {
        /// <summary>无回合结束效果</summary>
        None,
        /// <summary>剧毒：回合结束时持有者死亡（效果死亡、无伤害来源；神佑经 DeathRules 裁决拦截）</summary>
        PoisonDeath,
        /// <summary>毒素：回合结束时持有者每层受到 1 点伤害（可叠加）</summary>
        DamagePerStack,
    }

    /// <summary>属性指示物对应的字段类别（附加时即时回写、清除时反向回写）。
    /// 定案：攻/血指示物为**单向粒度**（增加/减少各一，单独成行方便后续取用，如分类净化/对消）；
    /// 每层效果固定（±1），计数恒正，方向由类别承载。</summary>
    public enum StatCounterKind
    {
        None,
        /// <summary>攻击力增加（每层 +1 攻，仅场上）</summary>
        PowerUp,
        /// <summary>攻击力减少（每层 −1 攻，仅场上）</summary>
        PowerDown,
        /// <summary>生命值增加（每层 +1 上限与当前，仅场上）</summary>
        LifeUp,
        /// <summary>生命值减少（每层 −1 上限，归零交 SBA，仅场上）</summary>
        LifeDown,
        /// <summary>费用增加（每层 +1 费，仅手牌生效，离手消失）</summary>
        CostUp,
        /// <summary>费用减少（每层 −1 费，仅手牌生效，离手消失）</summary>
        CostDown,
        /// <summary>+1/+1 属性增加（成长产物，历史 id）</summary>
        PlusOnePlusOne,
        /// <summary>-1/-1 属性减少（历史 id，SBA 与 +1/+1 对消）</summary>
        MinusOneMinusOne,
    }

    /// <summary>指示物规格：id → 极性 + 默认持续时间 + 回合结束效果 + 属性回写类别。</summary>
    public class CounterSpec
    {
        public string Id;
        public CounterPolarity Polarity;
        /// <summary>
        /// 默认持续：Permanent = 常驻（消耗型/叠加型）；
        /// UntilEndOfTurn = 回合结束消退；UntilLeaveBattlefield = 换区清除（未写持续时间的默认）；
        /// ForTurns = 按回合末计数 N 回合（Turns 字段）。
        /// </summary>
        public DurationType Duration;
        /// <summary>播报显示名（消退播报用）</summary>
        public string DisplayName;
        /// <summary>回合结束效果（默认无）</summary>
        public CounterTurnEndEffect TurnEndEffect = CounterTurnEndEffect.None;
        /// <summary>ForTurns 的回合数（毒素=3；-1=不适用）</summary>
        public int Turns = -1;
        /// <summary>属性回写类别（None=非属性指示物）</summary>
        public StatCounterKind StatKind = StatCounterKind.None;
    }

    /// <summary>
    /// 指示物统一管理（定案）：按正面/负面登记规格，持续到期走回合结束统一清理。
    ///
    /// 【设计原则】
    /// - 指示物不可编辑、效果固定、有持续时间、自动移除；**未写持续时间 = 持续到移动所属区域才消除**
    ///   （ZoneContainer.Move 统一调 ClearAll——属性指示物先反向回写再清空）。
    /// - 回合结束口径（定案）：**每个回合结束都结算**（双方回合末各一次；剧毒在最近的回合末死亡，
    ///   毒素 3 层时钟=3 个回合末）——效果型指示物与 ForTurns 时钟遍历全部实体，非仅回合玩家。
    /// - UntilEndOfTurn 整类清零保持"回合玩家战场"范围（冻结/突袭紊乱既有语义不变）。
    /// - 持续指示物不改回合规则（回合开始横置重置照常）。
    /// - 新指示物 = 此处登记一条（OCP：不改引擎热点）；未登记 id 保守视为 正面/Permanent（不参与清理）。
    /// </summary>
    public static class CounterRules
    {
        // ---- 指示物 id 常量（新指示物 = 一条常量 + 一条 spec）----
        /// <summary>剧毒：持续 1 回合，回合结束时持有者死亡（效果死亡，无伤害来源）</summary>
        public const string PoisonCounter = "Poison";
        /// <summary>毒素：持续 3 回合，回合结束时持有者每层受 1 点伤害，可叠加</summary>
        public const string ToxinCounter = "Toxin";
        /// <summary>沉默：持有者不可发动主动效果（未写持续时间=换区清除）</summary>
        public const string SilenceCounter = "Silence";
        /// <summary>易损：持续1回合，受到伤害时每层使受到的伤害+1（每个回合末到期）</summary>
        public const string VulnerableCounter = "Vulnerable";
        /// <summary>攻击力增加指示物（每层 +1 攻，仅场上）</summary>
        public const string PowerUpCounter = "PowerUp";
        /// <summary>攻击力减少指示物（每层 −1 攻，仅场上）</summary>
        public const string PowerDownCounter = "PowerDown";
        /// <summary>生命值增加指示物（每层 +1 上限与当前，仅场上）</summary>
        public const string LifeUpCounter = "LifeUp";
        /// <summary>生命值减少指示物（每层 −1 上限，仅场上）</summary>
        public const string LifeDownCounter = "LifeDown";
        /// <summary>费用增加指示物（每层 +1 费，仅手牌，离手消失）</summary>
        public const string CostUpCounter = "CostUp";
        /// <summary>费用减少指示物（每层 −1 费，仅手牌，离手消失）</summary>
        public const string CostDownCounter = "CostDown";
        /// <summary>+1/+1（成长产物，历史 id 保留）</summary>
        public const string PlusOneCounter = "+1/+1";
        /// <summary>-1/-1（历史 id，SBA 对消）</summary>
        public const string MinusOneCounter = "-1/-1";

        private static readonly Dictionary<string, CounterSpec> _registry =
            new Dictionary<string, CounterSpec>();

        static CounterRules()
        {
            // ---- 正面（常驻）----
            Register(new CounterSpec { Id = KeywordRules.ArmorCounter, Polarity = CounterPolarity.Positive, Duration = DurationType.Permanent, DisplayName = "护甲" });
            Register(new CounterSpec { Id = PlusOneCounter, Polarity = CounterPolarity.Positive, Duration = DurationType.Permanent, DisplayName = "+1/+1", StatKind = StatCounterKind.PlusOnePlusOne });
            Register(new CounterSpec { Id = "Awakening", Polarity = CounterPolarity.Positive, Duration = DurationType.Permanent, DisplayName = "觉醒" });

            // ---- 负面 ----
            // 常驻（SBA 与 +1/+1 对消）
            Register(new CounterSpec { Id = MinusOneCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.Permanent, DisplayName = "-1/-1", StatKind = StatCounterKind.MinusOneMinusOne });
            // 持续到回合结束（施加时横置为一次性动作；回合开始重置照常，不改回合规则）
            Register(new CounterSpec { Id = KeywordRules.FreezeCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilEndOfTurn, DisplayName = "冻结" });
            Register(new CounterSpec { Id = KeywordRules.RushSicknessCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilEndOfTurn, DisplayName = "突袭紊乱" });

            // ---- 原子表整体修正新增（2026-09-03 定案）----
            Register(new CounterSpec { Id = PoisonCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilEndOfTurn, DisplayName = "剧毒", TurnEndEffect = CounterTurnEndEffect.PoisonDeath });
            Register(new CounterSpec { Id = ToxinCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.ForTurns, DisplayName = "毒素", TurnEndEffect = CounterTurnEndEffect.DamagePerStack, Turns = 3 });
            Register(new CounterSpec { Id = SilenceCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilLeaveBattlefield, DisplayName = "沉默" });
            Register(new CounterSpec { Id = VulnerableCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilEndOfTurn, DisplayName = "易损" });
            Register(new CounterSpec { Id = PowerUpCounter, Polarity = CounterPolarity.Positive, Duration = DurationType.UntilLeaveBattlefield, DisplayName = "攻击力增加", StatKind = StatCounterKind.PowerUp });
            Register(new CounterSpec { Id = PowerDownCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilLeaveBattlefield, DisplayName = "攻击力减少", StatKind = StatCounterKind.PowerDown });
            Register(new CounterSpec { Id = LifeUpCounter, Polarity = CounterPolarity.Positive, Duration = DurationType.UntilLeaveBattlefield, DisplayName = "生命值增加", StatKind = StatCounterKind.LifeUp });
            Register(new CounterSpec { Id = LifeDownCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilLeaveBattlefield, DisplayName = "生命值减少", StatKind = StatCounterKind.LifeDown });
            // 费用层定案：层带颜色（将来按色增减各色分量），P1 恒作用于灰色分量——
            // 出牌链唯一口径 GameActions.GetCardCost 在此套层（预检/付费/pending 三处同源）；
            // 进发动区不清（发动区豁免），结算离开发动区与离手时清除。
            Register(new CounterSpec { Id = CostUpCounter, Polarity = CounterPolarity.Negative, Duration = DurationType.UntilLeaveBattlefield, DisplayName = "费用增加", StatKind = StatCounterKind.CostUp });
            Register(new CounterSpec { Id = CostDownCounter, Polarity = CounterPolarity.Positive, Duration = DurationType.UntilLeaveBattlefield, DisplayName = "费用减少", StatKind = StatCounterKind.CostDown });
        }

        /// <summary>登记指示物规格（新指示物=新登记，OCP）。</summary>
        public static void Register(CounterSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id)) return;
            _registry[spec.Id] = spec;
        }

        /// <summary>查规格；未登记保守视为 正面/Permanent（不参与持续清理，不改现有行为）。</summary>
        public static CounterSpec Find(string id)
            => _registry.TryGetValue(id, out var spec) ? spec
               : new CounterSpec { Id = id, Polarity = CounterPolarity.Positive, Duration = DurationType.Permanent, DisplayName = id };

        public static bool IsNegative(string id) => Find(id).Polarity == CounterPolarity.Negative;
        public static bool IsUntilEndOfTurn(string id) => Find(id).Duration == DurationType.UntilEndOfTurn;

        // ==================== 属性指示物（加时回写 / 清时反写） ====================

        /// <summary>
        /// 附加属性指示物并即时回写字段。攻/血为单向粒度（计数恒正、方向由类别承载——
        /// 正值走增加类、负值走减少类由调用方路由）；费用保持带符号净量。
        /// 加时即写、清除（换区/净化）时反向回写——数值=字段+指示物，战斗直读字段的路径零改动。
        /// </summary>
        public static void AddStatCounter(Card card, string id, int amount)
        {
            if (card == null || amount == 0) return;
            var spec = Find(id);
            if (spec.StatKind == StatCounterKind.None) return;
            card.AddCounters(id, amount);
            ApplyStatDelta(card, spec.StatKind, amount);
            EventManager.Instance.Publish(new CounterChangedEvent
            {
                Target = card,
                CounterType = id,
                Amount = amount,
                Source = null,
            });
        }

        /// <summary>属性增量回写（每层效果固定 ±1；n=层数恒正。削减类归零标死，送墓交 SBA）。</summary>
        private static void ApplyStatDelta(Card card, StatCounterKind kind, int n)
        {
            switch (kind)
            {
                case StatCounterKind.PowerUp:
                    card._power += n;
                    break;
                case StatCounterKind.PowerDown:
                    card._power -= n;
                    break;
                case StatCounterKind.LifeUp:
                    card._maxLife += n;
                    card._life += n;
                    break;
                case StatCounterKind.LifeDown:
                    card._maxLife -= n;
                    if (card._maxLife < 0) card._maxLife = 0;
                    if (card._life > card._maxLife) card._life = card._maxLife;
                    if (card._life <= 0) card.IsAlive = false; // 削减归零：标死，送墓交 SBA
                    break;
                case StatCounterKind.CostUp:
                    card._costModifier += n;
                    break;
                case StatCounterKind.CostDown:
                    card._costModifier -= n;
                    break;
                case StatCounterKind.PlusOnePlusOne:
                    card._power += n;
                    card._maxLife += n;
                    card._life += n;
                    break;
                case StatCounterKind.MinusOneMinusOne:
                    card._power -= n;
                    card._maxLife -= n;
                    if (card._maxLife < 0) card._maxLife = 0;
                    if (card._life > card._maxLife) card._life = card._maxLife;
                    if (card._life <= 0) card.IsAlive = false;
                    break;
            }
        }

        /// <summary>按当前计数反向回写全部属性指示物（不清计数——供 ClearAll 与净化复用前的还原）。</summary>
        private static void RevertStatCounters(Card card)
        {
            RevertStat(card, PowerUpCounter, StatCounterKind.PowerUp);
            RevertStat(card, PowerDownCounter, StatCounterKind.PowerDown);
            RevertStat(card, LifeUpCounter, StatCounterKind.LifeUp);
            RevertStat(card, LifeDownCounter, StatCounterKind.LifeDown);
            RevertStat(card, CostUpCounter, StatCounterKind.CostUp);
            RevertStat(card, CostDownCounter, StatCounterKind.CostDown);
            RevertStat(card, PlusOneCounter, StatCounterKind.PlusOnePlusOne);
            RevertStat(card, MinusOneCounter, StatCounterKind.MinusOneMinusOne);
        }

        private static void RevertStat(Card card, string id, StatCounterKind kind)
        {
            int n = card.GetCounterCount(id);
            if (n == 0) return;
            switch (kind)
            {
                case StatCounterKind.PowerUp:
                    card._power -= n;
                    break;
                case StatCounterKind.PowerDown:
                    card._power += n;
                    break;
                case StatCounterKind.LifeUp:
                    card._maxLife -= n;
                    if (card._life > card._maxLife) card._life = card._maxLife;
                    break;
                case StatCounterKind.LifeDown:
                    card._maxLife += n;
                    break;
                case StatCounterKind.CostUp:
                    card._costModifier -= n;
                    break;
                case StatCounterKind.CostDown:
                    card._costModifier += n;
                    break;
                case StatCounterKind.PlusOnePlusOne:
                    card._power -= n;
                    card._maxLife -= n;
                    if (card._life > card._maxLife) card._life = card._maxLife;
                    break;
                case StatCounterKind.MinusOneMinusOne:
                    card._power += n;
                    card._maxLife += n;
                    break;
            }
        }

        // ==================== 换区清除（定案默认持续） ====================

        /// <summary>
        /// 换区清除：未写持续时间的指示物一律持续到移动所属区域——移动时属性指示物先反向回写，
        /// 再清空全部计数与时钟（攻/血指示物离场消失、费用指示物离手消失皆走此处）。
        /// 由 ZoneContainer.Move 调用。
        /// </summary>
        public static void ClearAll(Card card)
        {
            if (card == null || (card._counters.Count == 0 && card._counterClocks.Count == 0)) return;
            RevertStatCounters(card);
            card._counters.Clear();
            card._counterClocks.Clear();
            EventManager.Instance.Publish(new CounterChangedEvent
            {
                Target = card,
                CounterType = "*",
                Amount = 0,
                Source = null,
            });
        }

        /// <summary>
        /// 净化口径：清除目标全部指示物（属性先反向回写）。与清空关键词（含神佑）配套，由 PurifyHandler 调用。
        /// </summary>
        public static void PurgeAll(Entity entity)
        {
            if (entity is Card card) ClearAll(card);
            else if (entity != null)
            {
                // Player：无属性回写，直接清计数与时钟（剧毒/毒素/护甲可指向玩家）
                entity._counters.Clear();
                entity._counterClocks.Clear();
                EventManager.Instance.Publish(new CounterChangedEvent
                {
                    Target = entity,
                    CounterType = "*",
                    Amount = 0,
                    Source = null,
                });
            }
        }

        // ==================== 回合结束处理 ====================

        /// <summary>
        /// 回合结束处理（由 GameCore.OnTurnEnd 调用，与 DurationTracker / TextChangeLayer 同链）：
        /// ① 效果型指示物与 ForTurns 时钟——**定案口径：每个回合结束都结算**（双方回合末各一次），
        ///    遍历全部实体（双方玩家 + 双方战场卡）；
        /// ② UntilEndOfTurn 整类清零——保持回合玩家战场范围（冻结/突袭紊乱既有语义）。
        /// </summary>
        public static void OnTurnEnd(Player turnPlayer, ZoneManager zoneManager)
        {
            if (turnPlayer == null) return;

            // ── ① 效果型指示物 + 回合时钟（全部实体） ──
            foreach (var entity in AllEntities(turnPlayer, zoneManager))
            {
                ProcessTurnEndEffects(entity, zoneManager);
                TickClocks(entity);
            }

            // ── ② UntilEndOfTurn 整类清零（回合玩家战场；既有语义） ──
            if (zoneManager == null) return;
            foreach (var card in zoneManager.GetCards(turnPlayer, Zone.Battlefield))
            {
                var expiring = card._counters
                    .Where(kv => kv.Value > 0 && IsUntilEndOfTurn(kv.Key))
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var id in expiring)
                {
                    card.AddCounters(id, -card.GetCounterCount(id));
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card,
                        Keyword = id,
                        Detail = $"{Find(id).DisplayName}消退（持续到回合结束）"
                    });
                }
            }
        }

        /// <summary>双方玩家 + 双方战场卡（效果型指示物的结算域）。
        /// ToList 物化快照：剧毒死亡会在结算中把卡移出战场（容器列表变更），惰性遍历会炸。</summary>
        private static IEnumerable<Entity> AllEntities(Player turnPlayer, ZoneManager zoneManager)
        {
            var snapshot = new List<Entity> { turnPlayer };
            if (turnPlayer.Opponent != null) snapshot.Add(turnPlayer.Opponent);
            if (zoneManager != null)
            {
                var seen = new HashSet<Entity>();
                foreach (var p in new[] { turnPlayer, turnPlayer.Opponent })
                {
                    if (p == null) continue;
                    foreach (var card in zoneManager.GetCards(p, Zone.Battlefield).ToList())
                        if (card != null && seen.Add(card))
                            snapshot.Add(card);
                }
            }
            return snapshot;
        }

        /// <summary>效果型指示物：剧毒=回合结束死亡（无伤害来源）；毒素=每层 1 伤（null 来源）。</summary>
        private static void ProcessTurnEndEffects(Entity entity, ZoneManager zoneManager)
        {
            // 剧毒：持续 1 回合——先消计数（无论是否被拦下，本回合末即到期），再裁决死亡
            int poison = entity.GetCounterCount(PoisonCounter);
            if (poison > 0)
            {
                entity.AddCounters(PoisonCounter, -poison);
                if (!DeathRules.IsShielded(entity, DeathCause.Poison))
                {
                    if (entity is Card card)
                    {
                        DeathRules.TryKill(card, DeathCause.Poison, null, zoneManager);
                    }
                    else if (entity is Player player)
                    {
                        // 角色：无神佑即被剧毒杀死——生命归零，交由生命判定收尾
                        player.Life = 0;
                    }
                }
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = entity,
                    Keyword = PoisonCounter,
                    Detail = "剧毒发作（回合结束）"
                });
            }

            // 毒素：每层 1 点伤害（null 来源——毒素自身不是伤害来源实体，吸血/系命无从触发）
            int toxin = entity.GetCounterCount(ToxinCounter);
            if (toxin > 0 && entity.IsAlive)
            {
                KeywordRules.ApplyDamage(null, entity, toxin, false);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = entity,
                    Keyword = ToxinCounter,
                    Detail = $"毒素发作（{toxin} 层）"
                });
            }

            // 易损：持续 1 回合（定案口径=每个回合末到期，无论归属）——毒素结算后清层
            int vulnerable = entity.GetCounterCount(VulnerableCounter);
            if (vulnerable > 0)
            {
                entity.AddCounters(VulnerableCounter, -vulnerable);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = entity,
                    Keyword = VulnerableCounter,
                    Detail = "易损消退（持续1回合）"
                });
            }
        }

        /// <summary>ForTurns 时钟递减：每个回合结束减一，到期层回收并从计数扣除（发消退播报）。</summary>
        private static void TickClocks(Entity entity)
        {
            if (entity._counterClocks.Count == 0) return;
            var expired = new List<CounterInstance>();
            foreach (var clock in entity._counterClocks)
            {
                clock.RemainingTurns--;
                if (clock.RemainingTurns <= 0) expired.Add(clock);
            }
            foreach (var clock in expired)
            {
                entity._counters.TryGetValue(clock.Id, out var cur);
                entity._counters[clock.Id] = Math.Max(0, cur - clock.Amount);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = entity,
                    Keyword = clock.Id,
                    Detail = $"{Find(clock.Id).DisplayName}消退（持续{Find(clock.Id).Turns}回合）"
                });
            }
            entity._counterClocks.RemoveAll(c => c.RemainingTurns <= 0);
        }

        /// <summary>清空目标全部负面指示物（解减益类效果的统一口径；横置状态不在此恢复）。</summary>
        public static void ClearNegative(Card card)
        {
            if (card == null) return;
            var negatives = card._counters
                .Where(kv => kv.Value > 0 && IsNegative(kv.Key))
                .Select(kv => kv.Key)
                .ToList();
            foreach (var id in negatives)
                card.AddCounters(id, -card.GetCounterCount(id));
        }
    }
}
