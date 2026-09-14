using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 动态分支引擎运行时（2026-09-13 分支体系正规化；2026-09-15 灰费全废）：主效果=条件引擎，奖励原子不占卡费。
    /// - **倒计时**：入场挂 Countdown 计数（层数=def.CountdownTurns=奖励推导费换算回合，1费=1回合；
    ///   UntilLeaveBattlefield 换区清）；控制者回合开始 -1，归零→执行奖励原子→重置回初值。
    /// - **运势**：控制者回合开始掷 2d6（GameRng），双 > x → 执行奖励（无状态，每回合独立判定；x=纯概率门槛）。
    /// - **拼点**（2026-09-15 门槛制定案）：控制者回合开始双方牌库顶各展示一张（放回原位不改序，空库按费用 0）——
    ///   比的是**费用总额**（数量，不计算颜色）；**差额 ≥ 奖励锚价合计**（大于等于）才触发，奖励按声明值结算。
    /// 组合根 EnsureRegistered（GameCore.Reset，幂等）；奖励原子各自解析目标（ExecuteEffectAsync）。
    /// </summary>
    public static class BranchEngines
    {
        private static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnterBattlefield);
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStarted);
        }

        private static void OnEnterBattlefield(CardPutToBattlefieldEvent e)
        {
            var card = e?.Card;
            if (card == null || !card.IsAlive) return;
            foreach (var def in DefsOf(card))
            {
                if (def?.EngineKind == BranchEngineKind.Countdown && def.CountdownTurns > 0
                    && card.GetCounterCount(Attribute.CounterRules.CountdownCounter) <= 0)
                {
                    card.AddCounters(Attribute.CounterRules.CountdownCounter, def.CountdownTurns);
                    break;
                }
            }
        }

        private static void OnTurnStarted(TurnStartEvent e)
        {
            var player = e?.TurnPlayer;
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (player == null || zm == null) return;

            foreach (var card in zm.GetCards(player, Zone.Battlefield).ToList())
            {
                if (card == null || !card.IsAlive) continue;
                foreach (var def in DefsOf(card))
                {
                    if (def == null) continue;
                    switch (def.EngineKind)
                    {
                        case BranchEngineKind.Countdown:
                            if (card.GetCounterCount(Attribute.CounterRules.CountdownCounter) <= 0)
                                card.AddCounters(Attribute.CounterRules.CountdownCounter, Math.Max(1, def.CountdownTurns)); // 兜底重挂
                            card.AddCounters(Attribute.CounterRules.CountdownCounter, -1);
                            if (card.GetCounterCount(Attribute.CounterRules.CountdownCounter) <= 0)
                            {
                                FireRewards(def, card, player, core);
                                card.AddCounters(Attribute.CounterRules.CountdownCounter, Math.Max(1, def.CountdownTurns)); // 归零发奖并重置
                                EventManager.Instance.Publish(new KeywordAppliedEvent
                                {
                                    Target = card, Keyword = "倒计时",
                                    Detail = $"倒计时归零：执行奖励并重置（{Math.Max(1, def.CountdownTurns)} 回合）",
                                });
                            }
                            break;

                        case BranchEngineKind.LuckRoll:
                            int x = Math.Max(1, Math.Min(5, def.EngineParam));
                            int d1 = GameRng.Next(1, 7), d2 = GameRng.Next(1, 7);
                            if (d1 > x && d2 > x)
                            {
                                EventManager.Instance.Publish(new KeywordAppliedEvent
                                {
                                    Target = card, Keyword = "运势",
                                    Detail = $"运势 {d1}+{d2} > {x}×2：执行奖励",
                                });
                                FireRewards(def, card, player, core);
                            }
                            break;

                        case BranchEngineKind.Clash:
                            // 2026-09-15 用户定案（门槛制）：比双方牌库顶**费用总额**（数量，不计算颜色）；
                            // 门槛 = 奖励锚价合计（推导），**差额 ≥ 门槛**（大于等于）才触发，奖励按声明值结算。
                            // 灰机制费已废除——锚价既是门槛也是奖励的价，由差额支付。
                            int mine = DeckTopCost(player, zm);
                            int theirs = DeckTopCost(player.Opponent, zm);
                            int threshold = Math.Max(1, (int)Math.Round(
                                CostDerivationService.RewardDerivedCost(def.RewardAtoms), MidpointRounding.AwayFromZero));
                            if (mine - theirs >= threshold)
                            {
                                EventManager.Instance.Publish(new KeywordAppliedEvent
                                {
                                    Target = card, Keyword = "拼点",
                                    Detail = $"拼点 {mine} vs {theirs}（差额 {mine - theirs} ≥ 门槛 {threshold}）：执行奖励（展示牌已放回原位）",
                                });
                                FireRewards(def, card, player, core);
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>牌库顶卡的总费用（展示后放回原位不改序）；空库=0。</summary>
        private static int DeckTopCost(Player player, ZoneManager zm)
        {
            if (player == null || zm == null) return 0;
            var deck = zm.GetCards(player, Zone.Deck);
            if (deck == null || deck.Count == 0) return 0;
            var top = deck[0]; // 牌库顶（index 0 = 顶，ZoneContainer 容器约定；只读展示，不移不动）
            float total = top is CardWrapper w ? (w.GetData()?.Cost?.Values.Sum() ?? 0f) : 0f;
            return (int)Math.Round(total, MidpointRounding.AwayFromZero);
        }

        /// <summary>执行奖励原子（各自解析目标——引擎无当前目标，per 原子独立上下文）。</summary>
        private static void FireRewards(EffectDefinition def, Card source, Player controller, GameCore core)
        {
            if (def.RewardAtoms == null || def.RewardAtoms.Count == 0) return;
            foreach (var atom in def.RewardAtoms)
            {
                if (atom == null) continue;
                var ctx = new EffectExecutionContext
                {
                    Source = source,
                    Controller = controller,
                    ZoneManager = core.ZoneManager,
                    ElementPool = core.ElementPool,
                    ModeIndex = -1,
                };
                EffectHandlerRegistry.ExecuteEffectAsync(atom, ctx).Forget();
            }
        }

        private static IEnumerable<EffectDefinition> DefsOf(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null) return Enumerable.Empty<EffectDefinition>();
            return CardEffectConverter.ConvertAll(data.Effects, data.ID).Where(d => d != null);
        }
    }
}
