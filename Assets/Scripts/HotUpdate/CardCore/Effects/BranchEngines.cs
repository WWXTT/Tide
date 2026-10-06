using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 自由分支·事件引擎运行时（2026-09-13 分支体系正规化；2026-10-05 两槽定案载荷化）：
    /// 引擎条件挂槽级原子 Branch 载荷（Settle==Engine），Then 奖励不占卡费（机制费/预算口径）。
    /// - **倒计时**：入场挂 Countdown 计数（层数=payload.CountdownTurns=Then 推导费换算回合，1费=1回合；
    ///   UntilLeaveBattlefield 换区清）；控制者回合开始 -1，归零→执行奖励原子→重置回初值。
    /// - **运势**：控制者回合开始掷 2d6（GameRng），双 > x → 执行奖励（无状态，每回合独立判定；x=纯概率门槛）。
    /// - **拼点**（2026-10-04 改版）：控制者回合开始双方牌库各**随机**取样一张**生物**（只读展示，不移牌不改序，
    ///   牌库无生物按攻击力 0）——比**攻击力**；**差额 ≥ 奖励锚价合计**（大于等于）才触发，奖励按声明值结算。
    /// - **死亡计数**（2026-09-22 定案）：本回合**双方合计**生物死亡数 ≥ x 时执行奖励（事件驱动——每次死亡事件后
    ///   复查；计数单调→每回合达标时刻唯一，天然一次/回合，回合作用域守卫集兜底）；奖励预算=x。
    /// - **元素充盈**（2026-09-22 定案）：自己出牌付费完成后判定（GameActions.PayCost 成功后回调 OnCardCostPaid）——
    ///   bank 数量最多的颜色（全六色，并列取枚举序首个）> x 即执行奖励；**每次达标都触发**（用户定案，无每回合
    ///   上限，多张引擎卡各自触发）；判定读付费后余量。效果费支付不触发。
    /// - **手牌序位**（2026-09-22 定案）：**此卡**为本回合从手牌使用的第 x 张卡（含自身，宣言序抓拍；响应出牌同计、
    ///   墓地视手牌等他源不算）→ 施放结算中执行奖励（发动无效跳过；回调 OnCardCastResolved）。
    /// 组合根 EnsureRegistered（GameCore.Reset，幂等）；Then 奖励目标=合法范围内随机（EffectExecutor.ExecuteThenRewardsAsync）。
    /// </summary>
    public static class BranchEngines
    {
        private static bool _registered;
        /// <summary>死亡计数引擎本回合已触发集（计数单调达标时刻唯一；防奖励连锁死亡的重复触发。TurnStart 清零）。</summary>
        private static readonly HashSet<Card> _deathTollFiredThisTurn = new HashSet<Card>();
        /// <summary>本回合各卡的**手牌使用序位**（手牌序位引擎用；宣言时抓拍——解析期他人再宣言不改本人序位。
        /// TurnStart 清零；只收 FromZone==Hand 的宣言）。</summary>
        private static readonly Dictionary<Card, int> _handCardOrdinalThisTurn = new Dictionary<Card, int>();

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnterBattlefield);
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStarted);
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnCreatureDestroyed);
            EventManager.Instance.Subscribe<CardPlayEvent>(OnCardPlayed);
        }

        /// <summary>卡上全部指定引擎的槽级载荷（两槽定案：def.Effects 主干原子的 Branch 载荷）。</summary>
        private static IEnumerable<BranchPayload> EnginePayloadsOf(Card card, BranchEngineKind kind)
        {
            foreach (var def in DefsOf(card))
            {
                if (def?.Effects == null) continue;
                foreach (var atom in def.Effects)
                {
                    var payload = atom?.Branch;
                    if (payload != null && payload.Settle == BranchSettleKind.Engine && payload.EngineKind == kind)
                        yield return payload;
                }
            }
        }

        private static void OnEnterBattlefield(CardPutToBattlefieldEvent e)
        {
            var card = e?.Card;
            if (card == null || !card.IsAlive) return;
            foreach (var payload in EnginePayloadsOf(card, BranchEngineKind.Countdown))
            {
                if (payload.CountdownTurns > 0
                    && card.GetCounterCount(Attribute.CounterRules.CountdownCounter) <= 0)
                {
                    card.AddCounters(Attribute.CounterRules.CountdownCounter, payload.CountdownTurns);
                    break;
                }
            }
        }

        private static void OnTurnStarted(TurnStartEvent e)
        {
            _deathTollFiredThisTurn.Clear();
            _handCardOrdinalThisTurn.Clear();

            var player = e?.TurnPlayer;
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (player == null || zm == null) return;

            foreach (var card in zm.GetCards(player, Zone.Battlefield).ToList())
            {
                if (card == null || !card.IsAlive) continue;
                foreach (var def in DefsOf(card))
                {
                    if (def?.Effects == null) continue;
                    foreach (var atom in def.Effects)
                    {
                        var payload = atom?.Branch;
                        if (payload == null || payload.Settle != BranchSettleKind.Engine) continue;
                        switch (payload.EngineKind)
                        {
                            case BranchEngineKind.Countdown:
                                if (card.GetCounterCount(Attribute.CounterRules.CountdownCounter) <= 0)
                                    card.AddCounters(Attribute.CounterRules.CountdownCounter, Math.Max(1, payload.CountdownTurns)); // 兜底重挂
                                card.AddCounters(Attribute.CounterRules.CountdownCounter, -1);
                                if (card.GetCounterCount(Attribute.CounterRules.CountdownCounter) <= 0)
                                {
                                    FireRewards(payload, card, player, core);
                                    card.AddCounters(Attribute.CounterRules.CountdownCounter, Math.Max(1, payload.CountdownTurns)); // 归零发奖并重置
                                    EventManager.Instance.Publish(new KeywordAppliedEvent
                                    {
                                        Target = card, Keyword = "倒计时",
                                        Detail = $"倒计时归零：执行奖励并重置（{Math.Max(1, payload.CountdownTurns)} 回合）",
                                    });
                                }
                                break;

                            case BranchEngineKind.LuckRoll:
                                int x = Math.Max(1, Math.Min(5, payload.EngineParam));
                                int d1 = GameRng.Next(1, 7), d2 = GameRng.Next(1, 7);
                                if (d1 > x && d2 > x)
                                {
                                    EventManager.Instance.Publish(new KeywordAppliedEvent
                                    {
                                        Target = card, Keyword = "运势",
                                        Detail = $"运势 {d1}+{d2} > {x}×2：执行奖励",
                                    });
                                    FireRewards(payload, card, player, core);
                                }
                                break;

                            case BranchEngineKind.Clash:
                                // 2026-10-04 用户定案（改版）：双方牌库各随机取样一张**生物**，比**攻击力**；
                                // 门槛 = 奖励锚价合计（推导），**差额 ≥ 门槛**（大于等于）才触发，奖励按声明值结算。
                                // 灰机制费已废除——锚价既是门槛也是奖励的价，由差额支付。
                                int mine = RandomCreaturePower(player, zm);
                                int theirs = RandomCreaturePower(player.Opponent, zm);
                                int threshold = Math.Max(1, (int)Math.Round(
                                    CostDerivationService.RewardDerivedCost(payload.Then), MidpointRounding.AwayFromZero));
                                if (mine - theirs >= threshold)
                                {
                                    EventManager.Instance.Publish(new KeywordAppliedEvent
                                    {
                                        Target = card, Keyword = "拼点",
                                        Detail = $"拼点 {mine} vs {theirs}（差额 {mine - theirs} ≥ 门槛 {threshold}）：执行奖励（随机生物取样，牌库未动）",
                                    });
                                    FireRewards(payload, card, player, core);
                                }
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>牌库中随机一张生物的攻击力（只读取样，不移牌不改序）；牌库无生物=0。</summary>
        private static int RandomCreaturePower(Player player, ZoneManager zm)
        {
            if (player == null || zm == null) return 0;
            var creatures = zm.GetCards(player, Zone.Deck)
                .Where(c => (c as CardWrapper)?.GetData()?.Supertype == Cardtype.Creature).ToList();
            if (creatures.Count == 0) return 0;
            var pick = creatures[GameRng.Next(0, creatures.Count)]; // GameRng 与运势同源，钉种子可复现
            return (pick as CardWrapper)?.GetData()?.Power ?? 0;
        }

        // ======================================== 死亡计数 / 元素充盈（2026-09-22 定案） ========================================

        /// <summary>死亡事件复查：双方合计死亡数（MatchStatsService.CreaturesDied ThisTurn 之和）≥ x 的引擎卡
        /// 逐张触发（守卫集防同回合重复/奖励连锁死亡重入；双方战场上的引擎卡都检查）。
        /// 计数依赖订阅序：MatchStatsService 于 GameCore 组合根先注册（L190）先收到事件——本处理器读到的
        /// 已是含本次死亡的值（顺序若被打破，验证器 TestDeathToll 阈值断言会先红）。</summary>
        private static void OnCreatureDestroyed(CardDestroyEvent e)
        {
            if (!(e?.DestroyedCard is IHasSupertype st) || st.Supertype != Cardtype.Creature) return;

            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (zm == null) return;

            // 事件归属锚点（任一在场玩家）展开双方
            var anchor = e?.DestroyedCard?.GetOwner() ?? e?.DestroyedCard?.GetController();
            if (anchor == null) return;

            int totalDeaths =
                StatOf(core, anchor, MatchStatsService.CreaturesDied)
                + StatOf(core, anchor.Opponent, MatchStatsService.CreaturesDied);

            EvaluateDeathTollEngines(anchor, totalDeaths, zm, core);
            EvaluateDeathTollEngines(anchor.Opponent, totalDeaths, zm, core);
        }

        private static void EvaluateDeathTollEngines(Player player, int totalDeaths, ZoneManager zm, GameCore core)
        {
            foreach (var card in zm.GetCards(player, Zone.Battlefield).ToList())
            {
                if (card == null || !card.IsAlive || _deathTollFiredThisTurn.Contains(card)) continue;
                foreach (var payload in EnginePayloadsOf(card, BranchEngineKind.DeathToll))
                {
                    ComposerCatalog.EngineParamRange(BranchEngineKind.DeathToll, out int min, out int max);
                    int x = Math.Max(min, Math.Min(max, payload.EngineParam));
                    if (totalDeaths < x) continue;

                    _deathTollFiredThisTurn.Add(card);
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card, Keyword = "死亡计数",
                        Detail = $"本回合双方合计 {totalDeaths} 个生物死亡 ≥ {x}：执行奖励",
                    });
                    FireRewards(payload, card, player, core);
                    break; // 同卡单载荷触发一次
                }
            }
        }

        /// <summary>出牌付费完成回调（GameActions.ResolveCardCastAsync 在 PayCost 成功后调用）：
        /// 付费后余量判定元素充盈引擎——bank 最多色（全六色）> x 即执行奖励，**每次达标都触发**
        /// （2026-09-22 用户定案：无每回合上限，多张引擎卡各自触发）。效果费支付不走此口。</summary>
        public static void OnCardCostPaid(Player player)
        {
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (player == null || zm == null) return;

            foreach (var card in zm.GetCards(player, Zone.Battlefield).ToList())
            {
                if (card == null || !card.IsAlive) continue;
                foreach (var payload in EnginePayloadsOf(card, BranchEngineKind.ManaSurplus))
                {
                    ComposerCatalog.EngineParamRange(BranchEngineKind.ManaSurplus, out int min, out int max);
                    int x = Math.Max(min, Math.Min(max, payload.EngineParam));
                    int maxColor = core.ElementPool?.GetMaxManaCount(player) ?? 0;
                    if (maxColor <= x) continue;

                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card, Keyword = "元素充盈",
                        Detail = $"出牌付费后 bank 最多色 {maxColor} > {x}：执行奖励",
                    });
                    FireRewards(payload, card, player, core);
                    break; // 同卡单载荷单次付费只触发一次
                }
            }
        }

        // ======================================== 手牌序位（2026-09-22 定案） ========================================

        /// <summary>使用宣言抓拍：从手牌宣言的卡记录其序位（= CardsPlayedFromHand ThisTurn 含本次——
        /// 依赖订阅序：MatchStatsService 先于本类注册，同死亡计数口径）。他源（墓地视手牌等）不记。</summary>
        private static void OnCardPlayed(CardPlayEvent e)
        {
            if (e?.Player == null || e.PlayedCard == null || e.FromZone != Zone.Hand) return;
            int ordinal = GameCore.Instance?.MatchStats?.GetStat(
                e.Player, MatchStatsService.CardsPlayedFromHand, StatScope.ThisTurn) ?? 0;
            _handCardOrdinalThisTurn[e.PlayedCard] = ordinal;
        }

        /// <summary>施放结算回调（GameActions.ResolveCardCastAsync 在发动无效裁决**之后**调用）：
        /// 手牌序位引擎——**此卡**为本回合从手牌使用的第 x 张卡（含自身）时执行奖励。
        /// 评估对象=正在施放的这张卡本身（自指条件），非场上其他引擎卡；发动无效已在上游跳过（不结算效果）。</summary>
        public static void OnCardCastResolved(Card card, Player player)
        {
            var core = GameCore.Instance;
            if (card == null || player == null || core == null) return;
            if (!_handCardOrdinalThisTurn.TryGetValue(card, out int ordinal)) return; // 非手牌来源使用 → 恒不触发

            foreach (var payload in EnginePayloadsOf(card, BranchEngineKind.NthHandCard))
            {
                ComposerCatalog.EngineParamRange(BranchEngineKind.NthHandCard, out int min, out int max);
                int x = Math.Max(min, Math.Min(max, payload.EngineParam));
                if (ordinal != x) continue;

                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = card, Keyword = "手牌序位",
                    Detail = $"此卡为本回合从手牌使用的第 {ordinal} 张卡 = x：执行奖励",
                });
                FireRewards(payload, card, player, core);
                break;
            }
        }

        /// <summary>本回合 stat 查询便捷口（死亡计数双方合计用）。</summary>
        private static int StatOf(GameCore core, Player player, string statId)
            => core?.MatchStats?.GetStat(player, statId, StatScope.ThisTurn) ?? 0;

        /// <summary>执行 Then 奖励原子（依次弹选目标——两槽定案；无头/AI 自动选首保 verify 确定性）。
        /// per 原子独立上下文（引擎无当前目标）。</summary>
        private static void FireRewards(BranchPayload payload, Card source, Player controller, GameCore core)
        {
            if (payload?.Then == null || payload.Then.Count == 0) return;
            var ctx = new EffectExecutionContext
            {
                Source = source,
                Controller = controller,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = -1,
            };
            EffectExecutor.ExecuteThenRewardsAsync(payload.Then, ctx, null).Forget();
        }

        private static IEnumerable<EffectDefinition> DefsOf(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null) return Enumerable.Empty<EffectDefinition>();
            return CardEffectConverter.ConvertAll(data.Effects, data.ID).Where(d => d != null);
        }
    }
}
