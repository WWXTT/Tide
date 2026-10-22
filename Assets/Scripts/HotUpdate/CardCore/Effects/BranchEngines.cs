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
    /// - **倒计时**（2026-10-20 固定触发定案）：入场挂 Countdown 计数（层数=payload.CountdownTurns=Then 推导费
    ///   换算回合，1费=1回合；UntilLeaveBattlefield 换区清）；控制者回合开始 -1；归零→**直接触发奖励**
    ///   （FireRewards 直发——主干与奖励不拆开：不按奖励发动方式 tactivate 分流、不入待发队列、不走响应窗）
    ///   →重挂计数（多轮继续触发）。2026-10-16 解绑（归零→待发标记→按声明时机发射）与 2026-10-20
    ///   分支奖励发动方式拆分，均对倒计时收回。
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
    ///   （满值 6 天然封顶总次数；触发**不清零**，仅使用/换区清零重计）。
    ///   旧「场上·本回合合计·每回合一发」口径随本改案退役。
    /// - **元素充盈**（2026-09-22 定案；同上自平衡统一；2026-10-20 判据六改：**大于等于**——判据五
    ///     「恰好等于」恰等太难（自回合囤元素代价大、对手回合精准踩值不现实）同日退役）：**只能是自动
    ///     效果、只能在对手回合中发动**——对手（=当前回合玩家）出牌施放结算完成后（ResolveCardCastAsync
    ///     末步回调 OnCardCastSettled；响应出牌/己方回合出牌不评估）比较双方**剩余元素总量**
    ///     （GetRemainingElementTotal=未横置地牌+钱包，与支付同资源口径）——**己方剩余与对手的差**
    ///     （封顶 6）**大于等于**门槛（Then 锚价推导）即执行奖励；**2026-10-22 全局规则：同一张卡
    ///     每回合只触发一次自由分支**（与教学二充盈卡 limit=1 同口径，引擎侧不再看效果行 limit）；
    ///     奖励恒直发（LaunchReward 直发例外），目标由控制者结算期弹选。判据四「地牌上限差/≥x」此前已退役。
        /// - **团结**（2026-10-20 效果序位换轴·第6种改空间轴）：与此卡相邻的单位数（生物/结界占格、
        ///   角色格、非空地牌格各算一；施放占位卡不计）≥ x 时执行奖励——评估口与元素充盈同拍
        ///   （OnCardCastSettled；2026-10-22 起同卡每回合仅 1 次）；只观察己方场上引擎卡（手牌无格）；x∈[1,6] 手填；
        ///   邻接计数经 UnityAdjacentCountOf 注入（核心不绑棋盘，CombatSystem.AdjacentResolver 同惯例）。
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

        /// <summary>团结邻接计数注入（宿主接线 BoardState.AdjacentOccupantCount——核心不绑棋盘，
        /// CombatSystem.AdjacentResolver 同惯例；BattleController/TideHeadlessDriver 成对设置/清空）。
        /// null（无头/未接线）=0 恒不触发；验证器（Editor 程序集）可注假计数——故 public。</summary>
        public static Func<Card, int> UnityAdjacentCountOf;

        // —— 引擎奖励单回合触发上限（2026-10-22 全局规则定案）：**所有自由分支（Engine settle）**
        //    在同一张卡上**每回合只能触发一次**——不再看效果行的 TriggerLimitPerTurn（该列对引擎分支
        //    只保留数据兼容，运行时以本全局规则为准）。按卡计发，任一回合开始清零（TurnStartEvent 翻章——
        //    双方回合都算新回合，跨局残留同清）。教学二充盈卡的 limit=1 由此升格为全引擎默认。
        private static readonly Dictionary<Card, int> _engineRewardFires = new Dictionary<Card, int>();

        /// <summary>本回合该卡是否已发过引擎奖励（全局每卡每回合 1 次）。</summary>
        private static bool EngineCapReached(Card card, EffectDefinition def)
            => _engineRewardFires.TryGetValue(card, out int fires) && fires >= 1;

        /// <summary>记账一次引擎奖励发射（每卡每回合 1 次；def 参数保留供日志/调用面兼容）。</summary>
        private static void RecordEngineFire(Card card, EffectDefinition def)
        {
            if (card == null) return;
            _engineRewardFires.TryGetValue(card, out int fires);
            _engineRewardFires[card] = fires + 1;
        }

        /// <summary>测试专用：清空本回合引擎发射记账（运势概率等需要独立同分布重掷的夹具用；
        /// 生产路径只由 TurnStartEvent 翻章清空，勿在业务代码调用）。</summary>
        public static void ResetTurnCapsForTest() => _engineRewardFires.Clear();

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnterBattlefield);
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStarted);
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnCreatureDestroyed);
            EventManager.Instance.Subscribe<AttackDeclarationEvent>(OnAttackDeclared); // 拼点（2026-10-09 触发改版）
        }

        /// <summary>卡上全部指定引擎的槽级载荷（两槽定案：def.Effects 主干原子的 Branch 载荷）。
        /// 2026-10-20 起连带 def（单回合上限读 def.TriggerLimitPerTurn——显式声明&gt;0 才收口）。</summary>
        private static IEnumerable<(EffectDefinition def, BranchPayload payload)> EnginePayloadsOf(Card card, BranchEngineKind kind)
        {
            foreach (var def in DefsOf(card))
            {
                if (def?.Effects == null) continue;
                foreach (var atom in def.Effects)
                {
                    var payload = atom?.Branch;
                    if (payload != null && payload.Settle == BranchSettleKind.Engine && payload.EngineKind == kind)
                        yield return (def, payload);
                }
            }
        }

        private static void OnEnterBattlefield(CardPutToBattlefieldEvent e)
        {
            var card = e?.Card;
            if (card == null || !card.IsAlive) return;
            foreach (var payload in EnginePayloadsOf(card, BranchEngineKind.Countdown).Select(tp => tp.payload))
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
            _engineRewardFires.Clear(); // 单回合上限翻章（2026-10-20）：任一回合开始都算新周期
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
                                {
                                    int turns = Math.Max(1, payload.CountdownTurns);
                                    int cd = card.GetCounterCount(Attribute.CounterRules.CountdownCounter);
                                    bool asleep = card.GetCounterCount(Attribute.KeywordRules.SleepCounter) > 0;
                                    if (asleep)
                                    {
                                        // 2026-10-22 苏醒口径：沉睡期间倒计时照走，但归零不出奖（停在0），
                                        // 等苏醒后第一个回合开始才触发——「一边苏醒，一边倒计时，苏醒走完才成功」
                                        if (cd > 0) card.AddCounters(Attribute.CounterRules.CountdownCounter, -1);
                                        break;
                                    }
                                    if (cd <= 0)
                                    {
                                        // 沉睡期间已归零（或刚入场即0）：苏醒后补发并重挂
                                        card.AddCounters(Attribute.CounterRules.CountdownCounter, turns);
                                        if (EngineCapReached(card, def)) break;
                                        EventManager.Instance.Publish(new KeywordAppliedEvent
                                        {
                                            Target = card, Keyword = "倒计时",
                                            Detail = $"倒计时归零：自动触发奖励，重挂（{turns} 回合）",
                                        });
                                        FireRewards(payload, card, player, core);
                                        RecordEngineFire(card, def);
                                        break;
                                    }
                                    card.AddCounters(Attribute.CounterRules.CountdownCounter, -1);
                                    if (card.GetCounterCount(Attribute.CounterRules.CountdownCounter) <= 0)
                                    {
                                        card.AddCounters(Attribute.CounterRules.CountdownCounter, turns); // 归零重挂
                                        if (EngineCapReached(card, def)) break;
                                        EventManager.Instance.Publish(new KeywordAppliedEvent
                                        {
                                            Target = card, Keyword = "倒计时",
                                            Detail = $"倒计时归零：自动触发奖励，重挂（{turns} 回合）",
                                        });
                                        FireRewards(payload, card, player, core);
                                        RecordEngineFire(card, def);
                                    }
                                    break;
                                }

                            // 运势已迁 OnRoutedEvent（2026-10-16 解绑）：掷骰随 def 声明时机判定，不再钉回合开始。
                        }
                    }
                }
            }
        }

        /// <summary>引擎时机评估钩子（2026-10-16 解绑「回合开始」硬绑定；2026-10-20 倒计时改固定触发后**仅运势消费**）：
        /// GameCore.PublishEventRouted 在总线分发之后逐事件调用。def 声明时机（TriggerTimingDefaults.GetEventType）
        /// 映射的事件类型 == 当前事件类型才评估。仅做类型匹配、不走 TriggerPayloadFilter——载荷过滤是
        /// 「来源卡自身参战」语义（伤害源==自己等），引擎卡非战斗参与方会被误杀；同一事件双方均评估为已知取舍。</summary>
        public static void OnRoutedEvent(IGameEvent e)
        {
            if (e == null) return;
            // 准备阶段时点（2026-10-20 定案）：GetEventType 只有类型级映射（PhaseStartEvent）——
            // Main/End 相位开始不评估（否则每回合掷三次骰）。
            if (e is PhaseStartEvent phaseEvt && phaseEvt.Phase != PhaseType.Standby) return;
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
                            if (payload.EngineKind != BranchEngineKind.LuckRoll) continue;
                            if (TriggerTimingDefaults.GetEventType(def.TriggerTiming) != eventType) continue;

                            switch (payload.EngineKind)
                            {
                                case BranchEngineKind.LuckRoll:
                                    int x = Math.Max(1, Math.Min(6, payload.EngineParam));
                                    int d1 = GameRng.Next(1, 7), d2 = GameRng.Next(1, 7);
                                    bool luckHit = d1 >= x && d2 >= x;
                                    // 表现层结构化观察口（成败都发·与主动路径同源）：头顶浮字
                                    EventManager.Instance.Publish(new LuckRolledEvent
                                    {
                                        EngineCard = card, Controller = player,
                                        D1 = d1, D2 = d2, Threshold = x, Success = luckHit,
                                    });
                                    if (luckHit && !EngineCapReached(card, def)) // 每卡每回合 1 次（2026-10-22 全局规则）
                                    {
                                        EventManager.Instance.Publish(new KeywordAppliedEvent
                                        {
                                            Target = card, Keyword = "运势",
                                            Detail = $"运势 {d1}+{d2} ≥ {x}×2：执行奖励",
                                        });
                                        LaunchReward(payload, def, card, player, core, e);
                                        RecordEngineFire(card, def);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
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
                foreach (var (def, payload) in EnginePayloadsOf(card, BranchEngineKind.Clash))
                {
                    var minePick = RevealBestCreatureInHand(player, zm, card);
                    var theirsPick = RevealBestCreatureInHand(player.Opponent, zm, card);
                    int mine = PowerOf(minePick);
                    int theirs = PowerOf(theirsPick);
                    int threshold = CostDerivationService.RewardThreshold(payload.Then); // 公式与 UI 短文案同源
                    bool success = mine - theirs >= threshold;
                    EventManager.Instance.Publish(new ClashResolvedEvent // 成败两态必发（2026-10-20 表现观察口）
                    {
                        EngineCard = card,
                        AttackingPlayer = player,
                        MyReveal = minePick,
                        TheirReveal = theirsPick,
                        MyPower = mine,
                        TheirPower = theirs,
                        Threshold = threshold,
                        Success = success,
                    });
                    if (success && !EngineCapReached(card, def)) // 每卡每回合 1 次（2026-10-22 全局规则）
                    {
                        EventManager.Instance.Publish(new KeywordAppliedEvent
                        {
                            Target = card, Keyword = "拼点",
                            Detail = $"拼点 {mine} vs {theirs}（我方展示{NameOf(minePick)}／对方展示{NameOf(theirsPick)}，差额 {mine - theirs} ≥ 门槛 {threshold}）：执行奖励（手牌展示比攻击力，手牌未动）",
                        });
                        LaunchReward(payload, null, card, player, core, e);
                        RecordEngineFire(card, def);
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

        /// <summary>元素充盈差额满值（2026-10-20 判据六）：剩余元素差读数封顶 6——门槛 x&gt;6 的奖励
        /// 天然不可达（与死亡计数满值 6 / 运势 x∈[1,6] 同族）。</summary>
        private const int SurplusCap = 6;

        private static void EvaluateDeathTollEngines(Player player, ZoneManager zm, GameCore core)
        {
            foreach (var card in zm.GetCards(player, Zone.Hand)
                .Concat(zm.GetCards(player, Zone.Battlefield)).ToList())
            {
                if (card == null) continue; // 手牌卡无战场生死态——实体在即计（法术/结界宿主同权）
                foreach (var (def, payload) in EnginePayloadsOf(card, BranchEngineKind.DeathToll))
                {
                    int count = card.GetCounterCount(Attribute.CounterRules.DeathTollCounter);
                    if (count >= DeathTollCap) break; // 满值不再计数（同卡单载荷一份——旧制口径）

                    card.AddCounters(Attribute.CounterRules.DeathTollCounter, 1);
                    count++;
                    // 2026-10-09 自平衡统一（沿旧制）：门槛=Then 奖励锚价推导——奖励越贵需死亡越多
                    int x = CostDerivationService.RewardThreshold(payload.Then);
                    if (count < x) break;
                    if (EngineCapReached(card, def)) break; // 每卡每回合 1 次（2026-10-22 全局规则）

                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card, Keyword = "死亡计数",
                        Detail = $"此卡计数 {count} ≥ {x}：执行奖励（满值 {DeathTollCap}，触发不清零，使用后清零）",
                    });
                    LaunchReward(payload, null, card, player, core);
                    RecordEngineFire(card, def);
                    break;
                }
            }
        }

        /// <summary>施放结算完成回调（GameActions.ResolveCardCastAsync 末步——法术效果结算/永久物入场之后）：
        /// 元素充盈引擎判定（2026-10-20 判据六改：**大于等于**）——**只能在对手回合中发动**：施放者==当前
        /// 回合玩家（对手于自己回合的出牌；响应出牌/己方回合出牌不评估）时，评估**施放者对手**（引擎主）
        /// 战场上的充盈引擎——双方剩余元素总量（GetRemainingElementTotal=未横置地牌+钱包）之差（己−对，
        /// 封顶 <see cref="SurplusCap"/>）**大于等于** x（Then 锚价推导）即执行奖励：首次达标即触发、
        /// 2026-10-22 全局规则：同一张卡每回合只触发一次自由分支（引擎侧不再看效果行 limit）。
        /// 判据五「恰好等于 ==x」同日退役（用户定案：恰等太难——自回合囤元素代价大，对手回合精准踩值不现实）。
        /// 发动无效/效果无效早退不评估；效果费支付不走此口。判据四「地牌上限差/≥x」此前已退役。</summary>
        public static void OnCardCastSettled(Player player)
        {
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (player == null || zm == null) return;

            // 元素充盈（判据六 ≥x）：引擎主=施放者的对手——施放者须为当前回合玩家（对手回合内对手自己的出牌）
            if (core.TurnEngine?.TurnPlayer == player && player.Opponent != null)
            {
                var owner = player.Opponent;
                foreach (var card in zm.GetCards(owner, Zone.Battlefield).ToList())
                {
                    if (card == null || !card.IsAlive) continue;
                    foreach (var (def, payload) in EnginePayloadsOf(card, BranchEngineKind.ManaSurplus))
                    {
                        // 2026-10-09 自平衡统一：门槛=Then 奖励锚价推导（与拼点同式）——奖励越贵需领先越多
                        int x = CostDerivationService.RewardThreshold(payload.Then);
                        int mine = core.ElementPool?.GetRemainingElementTotal(owner) ?? 0;
                        int theirs = core.ElementPool?.GetRemainingElementTotal(player) ?? 0;
                        int diff = Math.Min(mine - theirs, SurplusCap);
                        if (diff < x) continue; // 判据六（≥x）：差达到门槛即触发（==x 恰等语义随判据五退役）

                        if (EngineCapReached(card, def)) break; // 每卡每回合 1 次（2026-10-22 全局规则）

                        EventManager.Instance.Publish(new KeywordAppliedEvent
                        {
                            Target = card, Keyword = "元素充盈",
                            Detail = $"对手出牌结算后剩余元素差 {diff}（己 {mine}−对 {theirs}，封顶 {SurplusCap}）≥ {x}：执行奖励（仅对手回合·自动）",
                        });
                        LaunchReward(payload, null, card, owner, core);
                        RecordEngineFire(card, def);
                        break; // 同卡单载荷单次施放只触发一次
                    }
                }
            }

            // 团结（2026-10-20 效果序位换轴·第6种改空间轴）：与此卡相邻的单位数（生物/结界占格、
            // 角色格、非空地牌格各算一；施放占位卡不计）≥ x 即执行奖励——与充盈同拍同纪律
            //（≥ 达标即触发、只观察己方场上引擎卡——手牌无格；2026-10-22 起同卡每回合仅 1 次）。
            // 邻接计数经注入委托（核心不绑棋盘），未接线=0 恒不触发（无头/验证夹具可注假计数）。
            foreach (var card in zm.GetCards(player, Zone.Battlefield).ToList())
            {
                if (card == null || !card.IsAlive) continue;
                foreach (var (def, payload) in EnginePayloadsOf(card, BranchEngineKind.Unity))
                {
                    ComposerCatalog.EngineParamRange(BranchEngineKind.Unity, out int uMin, out int uMax);
                    int x = Math.Max(uMin, Math.Min(uMax, payload.EngineParam));
                    int n = UnityAdjacentCountOf != null ? UnityAdjacentCountOf(card) : 0;
                    if (n < x) continue;

                    if (EngineCapReached(card, def)) break; // 每卡每回合 1 次（2026-10-22 全局规则）

                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card, Keyword = "团结",
                        Detail = $"出牌结算后相邻单位数 {n} ≥ {x}：执行奖励（生物/结界/角色/非空地牌格各算一）",
                    });
                    LaunchReward(payload, null, card, player, core);
                    RecordEngineFire(card, def);
                    break; // 同卡单载荷单次施放只触发一次（充盈同口径）
                }
            }
        }

        /// <summary>施放结算回调（GameActions.ResolveCardCastAsync 在发动无效裁决**之后**调用）：
        /// 附加诅咒/附加祝福（2026-10-08 引擎主干化）——此卡施放结算时部署：诅咒=对手牌库随机 x 张卡、
        /// 祝福=自己牌库随机 x 张卡，各挂 1 层系统指示物+登记 Then 载荷（CurseSystem）；触发时点=该卡
        /// 被抽到（CurseSystem.OnCardDrawn），与引擎卡此后去向无关（法术入墓/永久物离场载荷均存续）。
        /// 发动无效已在上游跳过（不结算效果）。手牌序位/效果序位引擎均已退役（后者 2026-10-20 换轴为团结——评估走 OnCardCastSettled，不走此口）。</summary>
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
            foreach (var (_, payload) in EnginePayloadsOf(card, kind))
            {
                ComposerCatalog.EngineParamRange(kind, out int min, out int max);
                int n = Math.Max(min, Math.Min(max, payload.EngineParam));
                var deckOwner = enemyDeck ? player.Opponent : player;
                var deck = core.ZoneManager.GetCards(deckOwner, Zone.Deck);
                if (deck == null || deck.Count == 0)
                {
                    TideLog.Warn($"[OnDrawDeploy] {keyword}空转：{deckOwner?.Name} 牌库为空（无挂载目标）");
                    break;
                }

                for (int i = 0; i < n; i++)
                {
                    var target = deck[GameRng.Next(0, deck.Count)];
                    TideLog.Info($"[OnDrawDeploy] {keyword}附加 → {target.ID}（{deckOwner?.Name} 牌库 {deck.Count} 张随机取）");
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

        /// <summary>奖励发射统一口（2026-10-20 用户定案：**奖励=一般效果按自身发动方式发射，
        /// 主干只管触发判定**；同日倒计时例外——固定触发定案：归零直接触发奖励，不走此口分流）：
        /// 发动口径=payload.ThenActivation（-1=未声明随主干 def·向后兼容；1=自动；2=主动）。
        /// 主动→0 费奖励 def 入待发队列走既有响应窗路径（玩家发动/AI·无头自动
        /// 代选；ElementCostPrepaid 落实费用 0——卡价收讫防执行器再扣锚价；BRANCH_REWARD 不占
        /// 发动位）。自动/强制/未声明随主干非主动 → FireRewards 直发。</summary>
        private static void LaunchReward(BranchPayload payload, EffectDefinition def, Card source,
            Player controller, GameCore core, IGameEvent triggeringEvent = null)
        {
            if (payload?.Then == null || payload.Then.Count == 0) return;
            // 倒计时固定触发（2026-10-20 定案）：主干与奖励不拆开——无论 tactivate 声明恒直发
            //（防御兜底；正路发射口=OnTurnStarted 归零处直调 FireRewards，不经此方法）。
            // 元素充盈同款例外（2026-10-20 判据五定案「只能是自动效果」）：奖励恒直发——
            // ThenActivation 不适用（主动队列会把触发挪进响应窗，违背仅对手回合自动发动）。
            // 直发≠免弹选：目标仍走 ExecuteThenRewardsAsync 结算期弹选（控制者择目标）。
            if (payload.EngineKind == BranchEngineKind.Countdown
                || payload.EngineKind == BranchEngineKind.ManaSurplus)
            {
                FireRewards(payload, source, controller, core);
                return;
            }
            var launch = payload.ThenActivation >= 0
                ? (EffectActivationType)payload.ThenActivation
                : (def?.ActivationType ?? EffectActivationType.Automatic);
            if (launch == EffectActivationType.Voluntary && core?.StackEngine != null)
            {
                var rewardDef = new EffectDefinition
                {
                    Id = "BRANCH_REWARD",
                    DisplayName = $"{ComposerCatalog.EngineZhOf(payload.EngineKind)}奖励",
                    TriggerTiming = def?.TriggerTiming ?? TriggerTiming.OnTurnStart,
                    ActivationType = EffectActivationType.Voluntary,
                    ElementCostPrepaid = true,
                    Effects = payload.Then,
                };
                core.StackEngine.AddPendingEffect(PendingEffect.Create(
                    rewardDef, source, controller,
                    core.TurnEngine?.TurnPlayer ?? controller,
                    core.TurnEngine?.CurrentPhase?.Phase ?? PhaseType.Main,
                    triggeringEvent: triggeringEvent));
                return;
            }
            FireRewards(payload, source, controller, core);
        }

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
