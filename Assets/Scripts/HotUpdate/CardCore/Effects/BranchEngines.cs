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
    /// - **倒计时**（2026-10-16 解绑改版）：入场挂 Countdown 计数（层数=payload.CountdownTurns=Then 推导费
    ///   换算回合，1费=1回合；UntilLeaveBattlefield 换区清）；控制者回合开始 -1（**计数**仍钉回合开始）；
    ///   归零→奖励转**待发标记**（布尔，多轮归零不积累）→重挂计数；奖励按 def 声明的发动时机发射
    ///   （OnRoutedEvent）：主动=0费 def 上栈走响应窗、自动=直接结算。声明时机缺省=回合开始时
    ///   同一事件内随即发射，可观察行为与旧「归零发奖并重置」一致。
    /// - **运势**（2026-10-16 解绑改版；2026-10-19 双≥定案）：掷 2d6（GameRng）随 def 声明时机判定
    ///   （骰子本就随时可扔），双骰都 ≥ x（=分支费用）→ 执行奖励（无状态，每次时机事件独立判定；
    ///   x∈[1,6]，x=6 双 6 才中=1/36）。
        /// - **拼点**（2026-10-04 改版；2026-10-09 触发改版；2026-10-19 取样改版）：**攻击宣言时**（仅攻击方——
        ///   攻击宣言者战场上的引擎卡，OnAttackDeclared）双方各从**手牌**展示一张**生物**（自动取攻击力最高——
        ///   双方理性策略同为最高：引擎方拉大差额、对手压缩差额，无需选择窗口；选中卡挂「展示」指示物对双方
        ///   公开=RevealCard 原子同款口径，已展示不重复挂；手牌无生物按攻击力 0）——比**攻击力**；
        ///   **差额 ≥ 奖励锚价合计**（大于等于）才触发，奖励按声明值结算；**无限次**（每次攻击宣言独立判定，无每回合 guard）。
    /// - **死亡计数**（2026-09-22 定案；2026-10-09 自平衡统一；2026-10-19 手牌/场上改案）：**此卡在手牌中
    ///   或场上时**计数双方生物死亡——每次生物死亡 +1 死亡计数指示物（系统层，换区清=使用后清零）；
    ///   **满值 6**，达满不再计数；计数 ≥ 门槛（Then 锚价推导）即触发奖励——[x,6] 区间**每次递增都触发**
    ///   （元素充盈「每次达标都触发」同款，满值 6 天然封顶总次数；触发**不清零**，仅使用/换区清零重计）。
    ///   旧「场上·本回合合计·每回合一发」口径随本改案退役。
    /// - **元素充盈**（2026-09-22 定案；同上自平衡统一；2026-10-19 判据三改）：自己出牌付费完成后判定
    ///   （GameActions.PayCost 成功后回调 OnCardCostPaid——**先扣除消耗后才读余量**）——元素池剩余总量
    ///   （未横置地牌张数+钱包余额，全六色合计，读数封顶 6）≥ 门槛（Then 锚价推导）即执行奖励；
    ///   **每次达标都触发**（用户定案，无每回合上限，多张引擎卡各自触发）；效果费支付不触发。
        /// - **效果序位**（2026-10-19 手牌序位改案·定案）：本回合**双方**效果发动数达到 x−1 时执行奖励
        ///   ——奖励自身即「第 x 位」不占发动位（不用计数自身，FireRewards 直执行不入栈）。发动口径=
        ///   主动/自动效果入栈声明+法术/技能整卡施放各 1（攻击/守卫宣言、SBA、引擎合成奖励不算；
        ///   观察口=StackAddEvent 六发布点全集）；x∈[1,6] 手填，门槛=x−1；观察面=手牌∪场上双方引擎卡
        ///   （死亡计数 2026-10-19 改案同口径）；TurnStart 清零后即评估（x=1 门槛 0=回合开始即触发）。
        /// - **附加诅咒**（2026-10-08 自有限分支 CurseOnDraw 门接棒）：施放结算时给对手牌库
        ///   随机 x 张卡挂「诅咒」+登记 Then 载荷（CurseSystem）——对手抽到该卡时执行奖励并消层（一次性；
        ///   x∈[1,3]=附加张数，延迟与不确定性即代价，预算不设上限）。
        /// - **附加祝福**（2026-10-08）：同附加诅咒，作用面=自己牌库、指示物=「祝福」。两引擎用到的
        ///   指示物归系统（无玩家表行），投放/触发/消耗全在本类与 CurseSystem 内部。
    /// 组合根 EnsureRegistered（GameCore.Reset，幂等）；Then 奖励目标=合法范围内弹选（EffectExecutor.ExecuteThenRewardsAsync，2026-10-07 晚定案：AI/无头自动选首）。
    /// </summary>
    public static class BranchEngines
    {
        private static bool _registered;
        /// <summary>倒计时待发奖励集（2026-10-16 解绑：归零置位、按声明时机发射时移除；同集在册=多轮归零不积累；
        /// 引擎卡离场即清——奖励随卡而去）。</summary>
        private static readonly HashSet<Card> _countdownRewardPending = new HashSet<Card>();
        /// <summary>本回合**双方**效果发动计数（效果序位引擎用）：主动/自动效果入栈声明+法术/技能整卡
        /// 施放各 +1（攻击/守卫宣言、SBA、引擎合成奖励不算）。TurnStart 清零——清零后随即评估
        /// （x=1 门槛 0 = 回合开始即占第 1 位）。</summary>
        private static int _effectActivationCountThisTurn;

        /// <summary>验证器探针（同程序集可见——TideServer 源码子集编译）：计数读数/复位。
        /// 复位仅供夹具锚定基线，真实清零口=TurnStart（合成回合开始事件有级联副作用，不作夹具复位用）。</summary>
        internal static int EffectOrdinalCountForVerify => _effectActivationCountThisTurn;
        internal static void ResetEffectOrdinalForVerify() => _effectActivationCountThisTurn = 0;

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnterBattlefield);
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStarted);
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnCreatureDestroyed);
            EventManager.Instance.Subscribe<StackAddEvent>(OnEffectDeclared); // 效果序位（2026-10-19：发动声明观察口）
            EventManager.Instance.Subscribe<AttackDeclarationEvent>(OnAttackDeclared); // 拼点（2026-10-09 触发改版）
            EventManager.Instance.Subscribe<CardLeaveBattlefieldEvent>(OnLeaveBattlefield); // 倒计时待发标记卫生（2026-10-16）
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
            _effectActivationCountThisTurn = 0;

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
                                    // 2026-10-16 解绑：归零不再立即发奖——置待发标记（同集在册=多轮归零不积累），
                                    // 奖励按 def 声明时机发射（OnRoutedEvent）。声明时机=回合开始（存量缺省）时
                                    // PublishEventRouted 在本事件尾随即发射，可观察行为与旧「归零发奖并重置」一致。
                                    bool alreadyPending = !_countdownRewardPending.Add(card);
                                    card.AddCounters(Attribute.CounterRules.CountdownCounter, Math.Max(1, payload.CountdownTurns)); // 归零重挂
                                    EventManager.Instance.Publish(new KeywordAppliedEvent
                                    {
                                        Target = card, Keyword = "倒计时",
                                        Detail = alreadyPending
                                            ? $"倒计时归零：奖励待发未消耗不积累，重挂（{Math.Max(1, payload.CountdownTurns)} 回合）"
                                            : $"倒计时归零：奖励就绪，等待发动时机，重挂（{Math.Max(1, payload.CountdownTurns)} 回合）",
                                    });
                                }
                                break;

                            // 运势已迁 OnRoutedEvent（2026-10-16 解绑）：掷骰随 def 声明时机判定，不再钉回合开始。
                        }
                    }
                }
            }

            // 效果序位（2026-10-19）：计数清零后即评估——x=1（门槛 0）回合开始即触发（奖励占本回合第 1 发动位）
            EvaluateEffectOrdinalEngines();
        }

        /// <summary>引擎时机评估钩子（2026-10-16 解绑「回合开始」硬绑定）：GameCore.PublishEventRouted 在
        /// 总线分发（PublishDynamic——倒计时递减/置待发先行完成）之后逐事件调用。仅倒计时/运势消费
        /// （其余六引擎仍按各自内生事件驱动，不走此口）：def 声明时机（TriggerTimingDefaults.GetEventType）
        /// 映射的事件类型 == 当前事件类型才评估。仅做类型匹配、不走 TriggerPayloadFilter——载荷过滤是
        /// 「来源卡自身参战」语义（伤害源==自己等），引擎卡非战斗参与方会被误杀；同一事件双方均评估为已知取舍。</summary>
        public static void OnRoutedEvent(IGameEvent e)
        {
            if (e == null) return;
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (zm == null || core?.StackEngine == null) return;
            var eventType = e.GetType();
            var turnPlayer = core.TurnEngine?.TurnPlayer ?? core.Player1;
            var phase = core.TurnEngine?.CurrentPhase?.Phase ?? PhaseType.Standby;

            foreach (var player in new[] { core.Player1, core.Player2 })
            {
                if (player == null) continue;
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
                            if (payload.EngineKind != BranchEngineKind.Countdown
                                && payload.EngineKind != BranchEngineKind.LuckRoll) continue;
                            if (TriggerTimingDefaults.GetEventType(def.TriggerTiming) != eventType) continue;

                            switch (payload.EngineKind)
                            {
                                case BranchEngineKind.LuckRoll:
                                    int x = Math.Max(1, Math.Min(6, payload.EngineParam));
                                    int d1 = GameRng.Next(1, 7), d2 = GameRng.Next(1, 7);
                                    if (d1 >= x && d2 >= x)
                                    {
                                        EventManager.Instance.Publish(new KeywordAppliedEvent
                                        {
                                            Target = card, Keyword = "运势",
                                            Detail = $"运势 {d1}+{d2} ≥ {x}×2：执行奖励",
                                        });
                                        FireRewards(payload, card, player, core);
                                    }
                                    break;

                                case BranchEngineKind.Countdown:
                                    if (!_countdownRewardPending.Contains(card)) break;
                                    _countdownRewardPending.Remove(card); // 发射即消耗（多轮归零不积累的单发保障）
                                    EventManager.Instance.Publish(new KeywordAppliedEvent
                                    {
                                        Target = card, Keyword = "倒计时",
                                        Detail = "倒计时奖励：按发动时机执行",
                                    });
                                    if (def.ActivationType == EffectActivationType.Voluntary)
                                    {
                                        // 主动（2026-10-16）：0 费 def 上栈走既有响应窗（AI/无头自动代选）；
                                        // ElementCostPrepaid 落实费用 0——卡价收讫，防执行器再扣锚价。
                                        var rewardDef = new EffectDefinition
                                        {
                                            Id = "COUNTDOWN_REWARD",
                                            DisplayName = "倒计时奖励",
                                            TriggerTiming = def.TriggerTiming,
                                            ActivationType = def.ActivationType,
                                            ElementCostPrepaid = true,
                                            Effects = payload.Then,
                                        };
                                        core.StackEngine.AddPendingEffect(PendingEffect.Create(
                                            rewardDef, card, player, turnPlayer, phase, triggeringEvent: e));
                                    }
                                    else
                                    {
                                        FireRewards(payload, card, player, core);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>倒计时待发标记卫生（2026-10-16）：引擎卡离场即清——奖励随卡而去，与计数「换区清」口径一致。</summary>
        private static void OnLeaveBattlefield(CardLeaveBattlefieldEvent e)
        {
            var card = e?.Card;
            if (card != null) _countdownRewardPending.Remove(card);
        }

        /// <summary>拼点引擎评估（2026-10-09 触发改版：回合开始→攻击宣言，仅攻击方·无限次；
        /// 2026-10-19 取样改版：牌库随机取样→手牌展示）：攻击宣言事件结算**攻击方**战场引擎卡
        /// （与回合开始只遍历当前回合玩家同口径）；双方各从手牌展示攻击力最高的一张生物
        /// （挂「展示」指示物对双方公开，手牌无生物按 0），差额 ≥ 门槛（Then 锚价推导）执行奖励。</summary>
        private static void OnAttackDeclared(AttackDeclarationEvent e)
        {
            var player = e?.AttackingPlayer;
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (player == null || zm == null) return;

            foreach (var card in zm.GetCards(player, Zone.Battlefield).ToList())
            {
                if (card == null || !card.IsAlive) continue;
                foreach (var payload in EnginePayloadsOf(card, BranchEngineKind.Clash))
                {
                    var minePick = RevealBestCreatureInHand(player, zm, card);
                    var theirsPick = RevealBestCreatureInHand(player.Opponent, zm, card);
                    int mine = PowerOf(minePick);
                    int theirs = PowerOf(theirsPick);
                    int threshold = CostDerivationService.RewardThreshold(payload.Then); // 公式与 UI 短文案同源
                    if (mine - theirs >= threshold)
                    {
                        EventManager.Instance.Publish(new KeywordAppliedEvent
                        {
                            Target = card, Keyword = "拼点",
                            Detail = $"拼点 {mine} vs {theirs}（我方展示{NameOf(minePick)}／对方展示{NameOf(theirsPick)}，差额 {mine - theirs} ≥ 门槛 {threshold}）：执行奖励（手牌展示比攻击力，手牌未动）",
                        });
                        FireRewards(payload, card, player, core);
                    }
                }
            }
        }

        /// <summary>手牌中攻击力最高的生物（拼点展示取样，2026-10-19）：选中卡挂「展示」指示物对双方公开
        /// （RevealCard 原子同款口径——已展示不重复挂/不发事件，换区自清）；手牌无生物=null（按攻击力 0）。
        /// 取最高=双方理性策略同构（引擎方拉大差额、对手压缩差额），引擎自动判定无需选择窗口。</summary>
        private static Card RevealBestCreatureInHand(Player player, ZoneManager zm, Card engineCard)
        {
            if (player == null || zm == null) return null;
            Card best = null;
            int bestPower = int.MinValue;
            foreach (var c in zm.GetCards(player, Zone.Hand))
            {
                var data = (c as CardWrapper)?.GetData();
                if (data?.Supertype != Cardtype.Creature) continue;
                if (best == null || (data.Power ?? 0) > bestPower)
                {
                    best = c;
                    bestPower = data.Power ?? 0;
                }
            }
            if (best == null) return null;

            if (best.GetCounterCount(Attribute.CounterRules.ExposedCounter) == 0)
            {
                best.AddCounters(Attribute.CounterRules.ExposedCounter, 1, engineCard);
                EventManager.Instance.Publish(new CounterChangedEvent
                {
                    Target = best,
                    CounterType = Attribute.CounterRules.ExposedCounter,
                    Amount = 1,
                    Source = engineCard,
                });
                EventManager.Instance.Publish(new RevealCardsEvent
                {
                    Player = player,
                    Cards = new List<Card> { best },
                    Source = engineCard,
                });
            }
            return best;
        }

        private static int PowerOf(Card card) => (card as CardWrapper)?.GetData()?.Power ?? 0;

        private static string NameOf(Card card)
            => card != null ? $"【{(card as CardWrapper)?.GetData()?.CardName}】" : "无生物(0)";

        // ======================================== 死亡计数 / 元素充盈（2026-09-22 定案） ========================================

        /// <summary>死亡事件计数（2026-10-19 手牌/场上改案）：此卡在手牌中或场上时计数双方生物死亡——
        /// 每次生物死亡 +1 死亡计数指示物（系统层，换区清=使用后清零）；满值 <see cref="DeathTollCap"/>
        /// 达满不再计数；计数 ≥ 门槛（Then 锚价推导）即触发奖励（每次递增都触发，触发不清零）。
        /// MatchStats 订阅序依赖已全解除（手牌序位引擎 2026-10-19 退役后无读序消费方）。</summary>
        private static void OnCreatureDestroyed(CardDestroyEvent e)
        {
            if (!(e?.DestroyedCard is IHasSupertype st) || st.Supertype != Cardtype.Creature) return;

            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (zm == null) return;

            // 事件归属锚点（任一在场玩家）展开双方
            var anchor = e?.DestroyedCard?.GetOwner() ?? e?.DestroyedCard?.GetController();
            if (anchor == null) return;

            EvaluateDeathTollEngines(anchor, zm, core);
            EvaluateDeathTollEngines(anchor.Opponent, zm, core);
        }

        /// <summary>死亡计数满值（2026-10-19 定案）：计数上限 6，达满不再计数——[x,6] 区间每次递增都触发，
        /// 满值即总触发次数天然封顶（6−x+1 次/充电周期）。</summary>
        private const int DeathTollCap = 6;

        /// <summary>元素充盈读数满值（2026-10-19 判据三改）：剩余总量封顶 6——门槛 x&gt;6 的奖励天然不可达
        /// （与死亡计数满值 6 / 运势 x∈[1,6] 同族）。</summary>
        private const int SurplusCap = 6;

        private static void EvaluateDeathTollEngines(Player player, ZoneManager zm, GameCore core)
        {
            foreach (var card in zm.GetCards(player, Zone.Hand)
                .Concat(zm.GetCards(player, Zone.Battlefield)).ToList())
            {
                if (card == null) continue; // 手牌卡无战场生死态——实体在即计（法术/结界宿主同权）
                foreach (var payload in EnginePayloadsOf(card, BranchEngineKind.DeathToll))
                {
                    int count = card.GetCounterCount(Attribute.CounterRules.DeathTollCounter);
                    if (count >= DeathTollCap) break; // 满值不再计数（同卡单载荷一份——旧制口径）

                    card.AddCounters(Attribute.CounterRules.DeathTollCounter, 1);
                    count++;
                    // 2026-10-09 自平衡统一（沿旧制）：门槛=Then 奖励锚价推导——奖励越贵需死亡越多
                    int x = CostDerivationService.RewardThreshold(payload.Then);
                    if (count < x) break;

                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card, Keyword = "死亡计数",
                        Detail = $"此卡计数 {count} ≥ {x}：执行奖励（满值 {DeathTollCap}，触发不清零，使用后清零）",
                    });
                    FireRewards(payload, card, player, core);
                    break;
                }
            }
        }

        /// <summary>出牌付费完成回调（GameActions.ResolveCardCastAsync 在 PayCost 成功后调用）：
        /// 付费后余量判定元素充盈引擎——元素池剩余总量（未横置地牌+钱包余额合计，2026-10-19 判据三改；
        /// 先扣除本次消耗后读数，封顶 <see cref="SurplusCap"/>）≥ x 即执行奖励，**每次达标都触发**
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
                    // 2026-10-09 自平衡统一：门槛=Then 奖励锚价推导（与拼点同式）——奖励越贵需盈余越多
                    int x = CostDerivationService.RewardThreshold(payload.Then);
                    int total = Math.Min(core.ElementPool?.GetRemainingElementTotal(player) ?? 0, SurplusCap);
                    if (total < x) continue;

                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card, Keyword = "元素充盈",
                        Detail = $"出牌付费后元素池剩余总量 {total} ≥ {x}（封顶 {SurplusCap}）：执行奖励",
                    });
                    FireRewards(payload, card, player, core);
                    break; // 同卡单载荷单次付费只触发一次
                }
            }
        }

        // ======================================== 效果序位（2026-10-19 手牌序位改案） ========================================

        /// <summary>发动计数口（声明观察者）：StackAddEvent 六发布点全覆盖——主动发动/自动触发入栈
        /// （Definition≠null）各计 1 + 法术/技能整卡施放（IsCardCast 且 Spell 型）计 1；生物/地牌施放、
        /// 攻击/守卫宣言、SBA（无 Definition 的规则栈对象）及引擎合成奖励（COUNTDOWN_REWARD）不算。
        /// 生物登场效果走自动触发入栈另行计数（不与施放重复）。计数后评估引擎。</summary>
        private static void OnEffectDeclared(StackAddEvent e)
        {
            var inst = e?.AddedObject as EffectInstance;
            if (inst == null) return;
            if (inst.Definition == null)
            {
                // 整卡施放=「此卡被使用」的声明——只有携带效果发动的法术/技能算一次发动
                if (!inst.IsCardCast || !(inst.Source is IHasSupertype st) || st.Supertype != Cardtype.Spell) return;
            }
            else if (inst.Definition.Id == "COUNTDOWN_REWARD") return; // 引擎合成奖励不占发动位
            _effectActivationCountThisTurn++;
            EvaluateEffectOrdinalEngines();
        }

        /// <summary>门槛评估：本回合双方发动数 == x−1 时执行奖励（奖励自身即「第 x 位」，不计数——
        /// FireRewards 直执行不入栈，天然无递归）。计数单调、每值只经过一次→各门槛至多一次/回合；
        /// 观察面=手牌∪场上**双方**引擎卡（死亡计数 2026-10-19 改案同口径——手牌卡无战场生死态，
        /// 实体在即计）。TurnStart 清零后亦经 OnTurnStarted 调用（x=1 门槛 0）。</summary>
        private static void EvaluateEffectOrdinalEngines()
        {
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (zm == null) return;
            ComposerCatalog.EngineParamRange(BranchEngineKind.EffectOrdinal, out int min, out int max);

            foreach (var player in new[] { core.Player1, core.Player2 })
            {
                if (player == null) continue;
                foreach (var card in zm.GetCards(player, Zone.Hand)
                    .Concat(zm.GetCards(player, Zone.Battlefield)).ToList())
                {
                    if (card == null) continue;
                    foreach (var payload in EnginePayloadsOf(card, BranchEngineKind.EffectOrdinal))
                    {
                        int x = Math.Max(min, Math.Min(max, payload.EngineParam));
                        if (_effectActivationCountThisTurn != x - 1) continue;

                        EventManager.Instance.Publish(new KeywordAppliedEvent
                        {
                            Target = card, Keyword = "效果序位",
                            Detail = $"本回合双方效果发动数达到 {x - 1}（x={x}）：执行奖励（奖励自身不占发动位）",
                        });
                        FireRewards(payload, card, player, core);
                        break; // 同卡单载荷一份（死亡计数/倒计时同款口径）
                    }
                }
            }
        }

        /// <summary>施放结算回调（GameActions.ResolveCardCastAsync 在发动无效裁决**之后**调用）：
        /// 附加诅咒/附加祝福（2026-10-08 引擎主干化）——此卡施放结算时部署：诅咒=对手牌库随机 x 张卡、
        /// 祝福=自己牌库随机 x 张卡，各挂 1 层系统指示物+登记 Then 载荷（CurseSystem）；触发时点=该卡
        /// 被抽到（CurseSystem.OnCardDrawn），与引擎卡此后去向无关（法术入墓/永久物离场载荷均存续）。
        /// 发动无效已在上游跳过（不结算效果）。手牌序位引擎已随 2026-10-19 效果序位改案退役（不走此口）。</summary>
        public static void OnCardCastResolved(Card card, Player player)
        {
            var core = GameCore.Instance;
            if (card == null || player == null || core == null) return;

            DeployOnDrawEngines(card, player, BranchEngineKind.CurseOnDraw,
                Attribute.CounterRules.CurseCounter, "诅咒", enemyDeck: true, core);
            DeployOnDrawEngines(card, player, BranchEngineKind.BlessingOnDraw,
                Attribute.CounterRules.BlessingCounter, "祝福", enemyDeck: false, core);
        }

        /// <summary>附加诅咒/附加祝福部署（2026-10-08 引擎主干化）：施放结算时一次性投放——
        /// 随机 x 张（GameRng，不给施放者看牌库——炉石式随机即结果）各挂 1 层 + CurseSystem.AttachEngine
        /// 登记 Then 载荷；空库=本次施放空转（不部署不登记）。同卡多载荷取首个部署（倒计时入场同款口径）；
        /// 同一张目标卡被重复命中=多层多载荷，一次抽牌一并消耗（CurseSystem 一次性定案）。</summary>
        private static void DeployOnDrawEngines(Card card, Player player, BranchEngineKind kind,
            string counterId, string keyword, bool enemyDeck, GameCore core)
        {
            foreach (var payload in EnginePayloadsOf(card, kind))
            {
                ComposerCatalog.EngineParamRange(kind, out int min, out int max);
                int n = Math.Max(min, Math.Min(max, payload.EngineParam));
                var deckOwner = enemyDeck ? player.Opponent : player;
                var deck = core.ZoneManager.GetCards(deckOwner, Zone.Deck);
                if (deck == null || deck.Count == 0) break;

                for (int i = 0; i < n; i++)
                {
                    var target = deck[GameRng.Next(0, deck.Count)];
                    target.AddCounters(counterId, 1, player);
                    CurseSystem.AttachEngine(target, counterId, payload.Then, player, card);
                    EventManager.Instance.Publish(new Attribute.CounterChangedEvent
                    {
                        Target = target,
                        CounterType = counterId,
                        Amount = 1,
                        Source = player,
                    });
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = target, Keyword = keyword,
                        Detail = $"{keyword}附加：抽到该卡时自动执行{keyword}分支效果（一次性）",
                    });
                }
                break;
            }
        }

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
