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
    ///   换算回合，1费=1回合；UntilLeaveBattlefield 换区清）；控制者准备阶段 -1；归零→**直接触发奖励**
    ///   （FireRewards 直发——主干与奖励不拆开：不按奖励发动方式 tactivate 分流、不入待发队列、不走响应窗）
    ///   →重挂计数（多轮继续触发）。2026-10-16 解绑（归零→待发标记→按声明时机发射）与 2026-10-20
    ///   分支奖励发动方式拆分，均对倒计时收回。
    /// - **运势**（2026-10-16 解绑改版；2026-10-19 双≥定案）：掷 2d6（GameRng）随 def 声明时机判定
    ///   （骰子本就随时可扔），双骰都 ≥ x（=分支费用）→ 执行奖励（无状态，每次时机事件独立判定；
    ///   x∈[1,6]，x=6 双 6 才中=1/36）。
        /// - **拼抢**（2026-10-04 改版；2026-10-09 触发改版；2026-11-05 牌库随机取样；**2026-11-18 改版·手牌暗舍弃比费用**）：
        ///   **攻击宣言时**（仅攻击方——攻击宣言者战场上的引擎卡，OnRoutedEvent·AttackDeclarationEvent）
        ///   双方各从**手牌暗选舍弃一张卡**（可拒绝，拒绝=费用 0；AI/无头确定性代选：发起方最高费≥门槛才弃、
        ///   应对方恒弃最高费拦截），两选收齐后**同时公示**落墓——按**己方舍弃费用−对手舍弃费用**
        ///   （封顶 6；抉择卡费用取最高分支）**≥ 门槛**（Then 锚价推导）执行奖励；
        ///   **无限次**（每次攻击宣言独立判定；交互异步经串行队列，无头同步完成）。
        /// - **死亡计数**（2026-09-22 定案；2026-10-09 自平衡统一；2026-11-08 阶段3 收窄）：**此卡在场
        ///   上时**计数双方生物死亡（手牌不计数）——每次生物死亡 +1 死亡计数指示物（系统层，换区清=
        ///   使用后清零）；**满值 6**，达满不再计数；计数 ≥ 门槛（Then 锚价推导）即触发奖励——[x,6] 区间
        ///   **每次递增都触发**（满值 6 天然封顶总次数；触发**不清零**，仅使用/换区清零重计）。
        ///   旧「场上·本回合合计·每回合一发」口径与 2026-10-19 手牌/场上双区口径均退役。
    /// - **元素充盈**（2026-09-22 定案；2026-11-06 判据七·充盈改造：**恰等于**——推翻 2026-10-20
    ///     判据六「大于等于」，双方触发面让恰等重新可玩；**2026-11-13 再开放为 ≥ 门槛即触发**）：
    ///     **只能是自动效果**——任一方施放的卡
    ///     成功结算、且**整条连锁结算完成后**（StackEngine.FinishResolution 稳定点统一 flush，
    ///     不再在本卡结算中段直调；横置触发口 2026-11-04 引入、本日随判据七退役）双方各自评估
    ///     己方场上引擎：比较双方**剩余元素总量**（GetRemainingElementTotal=未横置地牌+钱包，
    ///     与支付同资源口径）——**己方剩余与对手的差**（封顶 6）**达到或超过**门槛（Then 锚价推导）
    ///     即执行奖励（≥x：每次施放每卡至多结算一次）；**每次达标都触发（2026-11-13 起无每回合上限，频率由耐久/挂载价付费）**
    ///     （2026-10-22 全局规则，引擎侧不看效果行 limit）；奖励恒直发（LaunchReward 直发例外），
    ///     目标由控制者结算期弹选。判据四「地牌上限差/≥x」、判据五/六均退役。
        /// - **团结**（2026-10-20 效果序位换轴·第6种改空间轴；2026-11-16 用户口径）：
        ///   与此卡相邻的**己方生物**数（只算相邻己方生物、不含自身；敌方生物/结界/角色/地/牌格不计）≥ x 时执行奖励——评估口与元素充盈同拍
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

        // —— （2026-11-13 定案）引擎奖励**不再有单回合触发上限**：旧「每卡每回合 1 次」
        //    （2026-10-22 全局规则）已删——自动效果的频率只由**结界耐久**（每次触发 −1）
        //    与宿主挂载价（生物=原价×f；f 只留在生物计价侧）承担，和卡面自动效果同口径。
        //    TriggerLimitPerTurn 对引擎分支本就只是数据兼容列，不再有任何运行时闸。

        /// <summary>引擎奖励发动的耐久成本（2026-11-13 定案）：只有**结界宿主在战场**时付——
        /// **每次触发扣 1 点耐久**（固定值）；生物宿主已在挂载价里按 ×f 付过、法术宿主无耐久。</summary>
        private static bool PaysTriggerDurability(Card source)
            => source != null
               && source is IHasSupertype st && st.Supertype == Cardtype.Enchantment
               && source.GetZone() == Zone.Battlefield;

        /// <summary>测试专用：清空未补发的使用结算残账（运势概率等需要独立同分布重掷的夹具用）。</summary>
        public static void ResetTurnCapsForTest()
        {
            _pendingCastSettles.Clear();
        }

        // —— 效果结算后待补发清单（2026-11-06 判据七·充盈改造）：ResolveCardCastAsync 成功结算的
        //    施放先记账（NoteCastSettled），整条连锁结算完成的稳定点（StackEngine.FinishResolution
        //    稳定 break 处调 FlushCardCastSettled）统一补发 CardCastSettledEvent + 双方充盈评估。
        //    **只存载荷数据、发布期才构造事件对象**——EventId 在 GameEventBase 构造期分配
        //    （++_nextEventId），记账期预构造会让补发事件带旧 Id 插进投影流=EventId 乱序
        //    （NetProtocolLoopbackVerifier 严格递增断言实证）。
        //    连锁不跨回合：RunCountdownTick 翻章清空兜底（终局搁浅未 flush 的残账不带入下一局——
        //    新局任何施放前必先过首个回合开始）。
        private static readonly List<(Player caster, Card card, int modeIndex)> _pendingCastSettles =
            new List<(Player, Card, int)>();

        /// <summary>是否有待补发的使用结算记录（StackEngine.FinishResolution 稳定点查询）。</summary>
        public static bool HasPendingCastSettles => _pendingCastSettles.Count > 0;

        /// <summary>使用结算记账（GameActions.ResolveCardCastAsync 末步·成功路径调用）：只记账不评估——
        /// 评估统一推迟到连锁终点（FlushCardCastSettled）。发动无效/效果无效/付费失败早退不调用。</summary>
        public static void NoteCastSettled(Player caster, Card playedCard, int modeIndex)
        {
            if (caster == null || playedCard == null) return;
            _pendingCastSettles.Add((caster, playedCard, modeIndex));
        }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnterBattlefield);
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnCreatureDestroyed);
            // 拼抢/团结已随 2026-11-08 阶段3 迁 OnRoutedEvent（时机=奖励效果时机）——
            // AttackDeclarationEvent 订阅与 OnCardCastSettled 直调均退役。
            // 倒计时 tick 已随 2026-10-26 精准切分迁 GameCore.RunStandbyAutomation 显式调用（原 TurnStartEvent 订阅退役）
        }

        /// <summary>卡上全部指定引擎的槽级载荷（两槽定案：def.Effects 主干原子的 Branch 载荷）。
        /// def 仅为求值/日志上下文——单回合上限已统一按卡（_engineRewardFires），不读效果行次数。</summary>
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
            ArmCountdownOnEnter(card);
        }

        /// <summary>入场挂倒计时计数（2026-11-08 抽公共口）：首个倒计时载荷挂 CountdownTurns 层
        ///（已挂/无载荷直返）。真实入场（CardPutToBattlefieldEvent）与教学静默播种
        ///（TutorialScenarioSeeder——不发入场事件，须显式补调）共用——不补则计数 0 起步，
        /// 首个准备阶段 tick 误走「沉睡解除补发」兜底分支（重挂满值+立即发奖，教学二段三a实证）。</summary>
        public static void ArmCountdownOnEnter(Card card)
        {
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

        /// <summary>
        /// 倒计时引擎回合 tick（2026-10-26 回合精准切分随迁准备阶段——推翻 2026-10-20「钉回合开始」定案：
        /// 归零发奖是可触发效果时点的游戏内容，不属系统区；由 GameCore.RunStandbyAutomation 在回合抽牌后
        /// 调用——相对序与旧 TurnStartEvent 订阅期一致（自动化+抽牌之后）。发射账翻章随本口（每回合一次）。
        /// </summary>
        public static void RunCountdownTick(Player player)
        {
            _pendingCastSettles.Clear(); // 使用结算残账翻章（2026-11-06 判据七）：连锁不跨回合，届时未 flush 的均为中断残账
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
                                    // 效果口径（2026-11-09）：绝缘体单位的沉睡层是死层，倒计时照常即走即发
                                    bool asleep = card.GetEffectiveCounterCount(Attribute.KeywordRules.SleepCounter) > 0;
                                    // 声明时机门（2026-11-05 时机两档定案：倒计时按**声明时机**发射）：
                                    // 主动档宿主（TriggerTiming∈{19,20,21}——判主动权威口径）归零=待发，
                                    // 不随宿主回合开始自动发奖（发射窗=玩家主动发动）；自动档照旧即发即重挂。
                                    bool activeHost = def.IsActivatedEffect;
                                    if (asleep)
                                    {
                                        // 2026-10-22 沉睡口径：沉睡期间倒计时照走，但归零不出奖（停在0），
                                        // 等沉睡解除后第一个回合开始才触发——「一边沉睡，一边倒计时，睡醒才成功」
                                        if (cd > 0) card.AddCounters(Attribute.CounterRules.CountdownCounter, -1);
                                        break;
                                    }
                                    if (cd <= 0)
                                    {
                                        // 沉睡期间已归零：沉睡解除后补发并重挂（2026-11-08 起静默播种由
                                        // ArmCountdownOnEnter 显式挂层，新卡不再以 0 计数进本分支）
                                        card.AddCounters(Attribute.CounterRules.CountdownCounter, turns);
                                        if (activeHost) break; // 主动档：重挂不补发
                                        EventManager.Instance.Publish(new KeywordAppliedEvent
                                        {
                                            Target = card, Keyword = "倒计时",
                                            Detail = $"倒计时归零：自动触发奖励，重挂（{turns} 回合）",
                                        });
                                        FireRewards(payload, card, player, core, PaysTriggerDurability(card));
                                        break;
                                    }
                                    card.AddCounters(Attribute.CounterRules.CountdownCounter, -1);
                                    if (card.GetCounterCount(Attribute.CounterRules.CountdownCounter) <= 0)
                                    {
                                        // 主动档：归零重挂不发奖（发射窗=玩家主动发动；计数照常回初值）
                                        card.AddCounters(Attribute.CounterRules.CountdownCounter, turns);
                                        if (activeHost) break;
                                        EventManager.Instance.Publish(new KeywordAppliedEvent
                                        {
                                            Target = card, Keyword = "倒计时",
                                            Detail = $"倒计时归零：自动触发奖励，重挂（{turns} 回合）",
                                        });
                                        FireRewards(payload, card, player, core, PaysTriggerDurability(card));
                                    }
                                    break;
                                }

                            // 运势已迁 OnRoutedEvent（2026-10-16 解绑）：掷骰随 def 声明时机判定，不再钉回合开始。
                        }
                    }
                }
            }
        }

        /// <summary>自动三引擎（运势/拼抢/团结）的统一事件评估口（2026-11-08 阶段3·奖励效果时机判定；
        /// 2026-11-10 R4：时机=外层效果头声明值，不再读引用效果时机）：
        /// GameCore.PublishEventRouted 在总线分发之后逐事件调用。时机=<see cref="RewardTimingOf"/>
        ///（def.TriggerTiming=转换器归一值——玩家在合成器选的引擎评估窗口）
        /// 映射的事件类型 == 当前事件类型才评估。归属过滤（<see cref="ExcludedByOwnership"/>）：
        /// 拼抢仅攻击方、团结仅施放者；运势沿用双方各评估；事件无法归属时双方各评估一次。
        /// 不走 TriggerPayloadFilter——载荷过滤是「来源卡自身参战」语义（伤害源==自己等），
        /// 引擎卡非战斗参与方会被误杀。</summary>
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
                            if (payload.EngineKind != BranchEngineKind.LuckRoll
                                && payload.EngineKind != BranchEngineKind.Clash
                                && payload.EngineKind != BranchEngineKind.Unity) continue;
                            if (ExcludedByOwnership(payload.EngineKind, e, player)) continue;
                            if (TriggerTimingDefaults.GetEventType(RewardTimingOf(def)) != eventType) continue;

                            switch (payload.EngineKind)
                            {
                                case BranchEngineKind.LuckRoll:
                                {
                                    int x = Math.Max(1, Math.Min(6, payload.EngineParam));
                                    int d1 = GameRng.Next(1, 7), d2 = GameRng.Next(1, 7);
                                    bool luckHit = d1 >= x && d2 >= x;
                                    // 表现层结构化观察口（成败都发·与主动路径同源）：头顶浮字
                                    EventManager.Instance.Publish(new LuckRolledEvent
                                    {
                                        EngineCard = card, Controller = player,
                                        D1 = d1, D2 = d2, Threshold = x, Success = luckHit,
                                    });
                                    if (luckHit) // 2026-11-13：无单回合上限（耐久/挂载价付费）
                                    {
                                        EventManager.Instance.Publish(new KeywordAppliedEvent
                                        {
                                            Target = card, Keyword = "运势",
                                            Detail = $"运势 {d1}+{d2} ≥ {x}×2：执行奖励",
                                        });
                                        LaunchReward(payload, def, card, player, core, e);
                                    }
                                    else if (!luckHit)
                                    {
                                        // 未达标播报（2026-11-08 运势奖励效果化迁移配套——主干钉自动后
                                        // 掷骰走本口，成败两态与主动路径 EffectExecutionEngine 同口径：
                                        // 负例带评估证据，教学 luck-miss 断言立足点）
                                        EventManager.Instance.Publish(new KeywordAppliedEvent
                                        {
                                            Target = card, Keyword = "运势",
                                            Detail = $"运势 {d1}+{d2} < {x}×2：未达标",
                                        });
                                    }
                                    break;
                                }

                                case BranchEngineKind.Clash:
                                {
                                    // 拼抢评估（2026-11-18 改版·手牌暗舍弃比费用；触发口不变=攻击宣言·仅攻击方）：
                                    // 双方各从手牌**暗选**舍弃一张卡（可拒绝，拒绝=费用0），两选收齐后**同时公示**
                                    // 落墓——己方舍弃费用−对手舍弃费用（封顶 ClashDiffCap）≥ 门槛（Then 锚价推导）
                                    // 执行奖励。交互走异步链（详见 ClashJob 区）：人类经目标选择弹窗；AI/无头
                                    // 同步代选零挂起（RequestAsync 自动路径）——无头时序与旧抽样版逐字节一致
                                    //（训练确定性）；串行队列防同事件多载荷并发弹窗。
                                    EnqueueClash(new ClashJob
                                    {
                                        Payload = payload, Def = def, Card = card,
                                        Player = player, Core = core, Trigger = e,
                                    });
                                    break;
                                }

                                case BranchEngineKind.Unity:
                                {
                                    // 团结评估（2026-10-20 效果序位换轴·空间轴；2026-11-16 用户口径）：
                                    // 与此卡相邻的**己方生物**数（只算相邻己方生物、不含自身；敌方生物/结界/角色/地/牌格不计）≥ x 即执行奖励
                                    //——2026-11-08 阶段3 自 OnCardCastSettled 直调迁本口（时机=奖励效果）。
                                    ComposerCatalog.EngineParamRange(BranchEngineKind.Unity, out int uMin, out int uMax);
                                    int x = Math.Max(uMin, Math.Min(uMax, payload.EngineParam));
                                    int n = UnityAdjacentCountOf != null ? UnityAdjacentCountOf(card) : 0;
                                    if (n < x) break;

                                    EventManager.Instance.Publish(new KeywordAppliedEvent
                                    {
                                        Target = card, Keyword = "团结",
                                        Detail = $"评估时点相邻己方生物数 {n} ≥ {x}：执行奖励（只算相邻己方生物、不含自身）",
                                    });
                                    LaunchReward(payload, def, card, player, core, e);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>引擎奖励效果时机（2026-11-08 阶段3 曾解析 payload.EffectId → 引用效果
        /// TriggerTiming；**2026-11-10 R4 修订**：以效果头时机为准——def.TriggerTiming=转换器
        /// 声明值（2026-11-14 起 OnTurnStart 不再归一，按声明值当事件窗）。运势/拼抢/团结的评估窗口由玩家
        /// 在合成器外层时机选（converter/装载闸同口径），不再读引用效果时机——R4 允许主动效果
        /// 作奖励后，引用时机反钉会令事件类型永不匹配（引擎永不评估）。</summary>
        private static TriggerTiming RewardTimingOf(EffectDefinition def)
            => def != null ? def.TriggerTiming : TriggerTiming.OnStandbyPhase;

        /// <summary>归属过滤（2026-11-08 阶段3）：拼抢仅攻击方（AttackDeclarationEvent）、
        /// 团结仅施放者（CardPlayEvent/CardCastSettledEvent）——事件可归属且归属方≠player 时跳过；
        /// 运势（及一切无法归属的事件）双方各评估一次。</summary>
        private static bool ExcludedByOwnership(BranchEngineKind kind, IGameEvent e, Player player)
        {
            switch (kind)
            {
                case BranchEngineKind.Clash:
                    return e is AttackDeclarationEvent a && a.AttackingPlayer != player;
                case BranchEngineKind.Unity:
                    // 2026-11-16：团结支持自由时机（用户定案：运势/团结都可自选发动窗口）。
                    // OnTurnEnd 窗口按**回合玩家的团结卡**评估——否则双方团结在任意回合末都会触发。
                    if (e is TurnEndEvent te) return te.TurnPlayer != player;
                    return e is CardCastSettledEvent cs && cs.Caster != player
                        || e is CardPlayEvent cp && cp.Player != player;
                default:
                    return false;
            }
        }

        // ======================================== 拼抢（2026-11-18 改版：手牌暗舍弃比费用） ========================================

        /// <summary>拼抢差额封顶（沿用旧抽样版口径：差额读数封顶 6——门槛 x&gt;6 为装载闸拦的死档）。</summary>
        private const int ClashDiffCap = 6;

        /// <summary>拼抢结算作业（在途串行队列载体——一次事件可能命中多张拼抢卡/多载荷，
        /// 人类局弹窗必须一次一张，并发 RequestAsync 会叠窗）。</summary>
        private class ClashJob
        {
            public BranchPayload Payload;
            public EffectDefinition Def;
            public Card Card;
            public Player Player;
            public GameCore Core;
            public IGameEvent Trigger;
            public int Generation; // 对局代次（ResetClashChain 自增——旧局在途作业不得污染新局）
        }

        private static readonly Queue<ClashJob> _pendingClashes = new Queue<ClashJob>();
        private static bool _clashChainRunning;
        private static int _clashGeneration;

        /// <summary>对局初始化复位（2026-11-18）：静态串行链/待发施放账不随对局销毁——人类局中途退出、
        /// 断线，或上局最后一次施放未 flush（红套→蓝套串跑实测：上局血偿残账在新局首次 flush 被消费，
        /// 蓝充盈凭空触发一次），新局拼抢/充盈会凭空评估。GameCore.InitGame 单点调用：清队列 + 代次
        /// 自增（旧作业见代次不符直接丢弃）+ 复位链标志 + 清施放残账。</summary>
        public static void ResetClashChain()
        {
            _clashGeneration++;
            _pendingClashes.Clear();
            _clashChainRunning = false;
            _pendingCastSettles.Clear();
        }

        /// <summary>拼抢入队并驱动串行链：链空闲则当场驱动（无头/AI 链路同步排干——时序与旧版
        /// 内联评估一致）；链在跑只入队，由链尾自取。finally 复位保证异常不卡死后续拼抢。</summary>
        private static void EnqueueClash(ClashJob job)
        {
            job.Generation = _clashGeneration;
            _pendingClashes.Enqueue(job);
            if (_clashChainRunning) return;
            _clashChainRunning = true;
            DrainClashChainAsync().Forget();
        }

        private static async UniTask DrainClashChainAsync()
        {
            try
            {
                while (_pendingClashes.Count > 0)
                    await ResolveClashAsync(_pendingClashes.Dequeue());
            }
            finally { _clashChainRunning = false; }
        }

        /// <summary>拼抢结算（自动路径·攻击宣言）：双暗选（收集期零状态变更零公示=同时亮）→
        /// 提交比费 → 成功播报+LaunchReward。详见 <see cref="CommitClashDuel"/>。</summary>
        private static async UniTask ResolveClashAsync(ClashJob job)
        {
            if (job.Generation != _clashGeneration) return; // 旧局在途作业（ResetClashChain 已换代）
            var zm = job.Core?.ZoneManager;
            if (job.Player == null || zm == null) return;
            int threshold = CostDerivationService.EngineRewardThreshold(job.Payload.Then, job.Def.TriggerTiming);
            var (myPick, theirPick) = await CollectDuelPicksAsync(job.Player, threshold, job.Card, zm);
            var (success, detail) = CommitClashDuel(job.Player, job.Card, threshold, myPick, theirPick, job.Core,
                shortCircuit: myPick == null);
            if (!success) return; // 2026-11-13：无单回合上限（耐久/挂载价付费）
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = job.Card, Keyword = "拼抢",
                Detail = $"{detail}：执行奖励（手牌暗舍弃比费用，抉择卡按最高分支费用）",
            });
            LaunchReward(job.Payload, job.Def, job.Card, job.Player, job.Core, job.Trigger);
        }

        /// <summary>主动档拼抢（ApplyActiveEngineTrunkAsync 特判口·2026-11-18 改版）：与自动路径
        /// 同一双暗选+提交单源；不在此播报/发奖——调用方按主动口径播报并 ApplyThen。</summary>
        public static async UniTask<(bool success, string detail)> ResolveActiveClashAsync(
            BranchPayload payload, EffectDefinition def, Card card, Player player, GameCore core)
        {
            var zm = core?.ZoneManager;
            if (player == null || zm == null) return (false, "拼抢：环境缺失（无玩家/无区域）");
            int threshold = CostDerivationService.EngineRewardThreshold(payload.Then,
                def?.TriggerTiming ?? TriggerTiming.OnStandbyPhase);
            var (myPick, theirPick) = await CollectDuelPicksAsync(player, threshold, card, zm);
            return CommitClashDuel(player, card, threshold, myPick, theirPick, core, shortCircuit: myPick == null);
        }

        /// <summary>双方暗选（先发起方后应对方——顺序仅为弹窗次序，**选择间零状态变更零公示**，
        /// 先手信息不泄漏=同时亮语义）。发起方拒绝/空手→应对方免问（0 费侧差额恒 &lt; 门槛 x≥1）。</summary>
        private static async UniTask<(Card myPick, Card theirPick)> CollectDuelPicksAsync(
            Player initiator, int threshold, Card engineCard, ZoneManager zm)
        {
            var myPick = await CollectClashPickAsync(initiator, threshold, engineCard, zm, initiator: true);
            var theirPick = myPick == null ? null
                : await CollectClashPickAsync(initiator.Opponent, threshold, engineCard, zm, initiator: false);
            return (myPick, theirPick);
        }

        /// <summary>单侧暗选：空手=拒绝（0费）不弹窗；AI/无头→确定性代选（<see cref="AutoClashPick"/>）；
        /// 人类→弹窗（AllowCancel=true：取消=拒绝=费用0）。候选按费用降序预排——超时安全网
        /// AutoSelect 取首张≈代选策略，超时行为与 AI 口径一致。</summary>
        private static async UniTask<Card> CollectClashPickAsync(Player chooser, int threshold,
            Card engineCard, ZoneManager zm, bool initiator)
        {
            var hand = zm.GetCards(chooser, Zone.Hand)
                .Where(c => c != null && c.IsAlive).ToList();
            if (hand.Count == 0) return null;
            if (chooser.IsAI || TargetSelectionService.Current == null)
                return AutoClashPick(hand, threshold, initiator);

            var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = hand.OrderByDescending(ClashCostOf).Cast<Entity>().ToList(),
                MinCount = 1,
                MaxCount = 1,
                Chooser = chooser,
                Title = initiator
                    ? "拼抢：舍弃一张手牌参与比费（取消=未弃牌，视为费用0）"
                    : "拼抢：对手的攻击引擎触发比费——舍弃一张手牌拦截（取消=未弃牌，视为费用0）",
                // 视角分写（2026-11-18 修正）：发起方看「己方−对手」，应对方必须看「对手−你」——
                // 同一句「己方−对手≥门槛触发奖励」会让防守方误以为弃高费是自己拿奖励。
                Hint = initiator
                    ? $"双方暗选、同时公示：己方舍弃费用−对手舍弃费用 ≥ {threshold} 触发奖励"
                      + $"（未弃牌/抉择法术视为费用0；差额封顶{ClashDiffCap}）"
                    : $"对手引擎：对手舍弃费用−你舍弃费用 ≥ {threshold} 时对手拿奖励——你弃得越高越能拦截"
                      + $"（未弃牌/抉择法术视为费用0；差额封顶{ClashDiffCap}）",
                AllowCancel = true,
                PromptSource = engineCard,
            });
            return picked.OfType<Card>().FirstOrDefault(); // 空=拒绝（取消；超时已被安全网代选）
        }

        /// <summary>AI/无头代选（盲选·确定性）：候选按费用降序（并列取手牌原序）。
        /// 发起方=最高费 ≥ 门槛才弃（对「拒绝=0费」侧也能独立成差），否则拒绝保牌；
        /// 应对方=恒弃最高费（盲选不见对方选择，弃最高=最大化拦截覆盖），空手已在上游拦截。</summary>
        private static Card AutoClashPick(List<Card> hand, int threshold, bool initiator)
        {
            var best = hand.OrderByDescending(ClashCostOf).First();
            if (initiator && ClashCostOf(best) < threshold) return null;
            return best;
        }

        /// <summary>拼抢比费口径（2026-11-18 用户定案）：卡面推导费（逐色进位求和——支付口径同源，
        /// 单效果法术已是代价净费）；**抉择/多主动法术视为费用 0**（无法确定实际分支，按零费参与比费，
        /// 2026-11-18 用户定案，写进原子行/效果文案）；生物/结界=单模式推导价（GetRuntimeCost）。</summary>
        private static int ClashCostOf(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return 0;
            if (data.Supertype == Cardtype.Spell && CostDerivationService.HasChoiceEffect(data))
                return 0; // 抉择法术视为费用 0（2026-11-18 用户定案）
            return ElementPaymentValidator.NormalizeBill(CardCostService.GetRuntimeCost(data)).Values.Sum();
        }

        /// <summary>拼抢提交+比费（同步单源·自动/主动两路共用）：两张一起落墓（仍在手牌才移——
        /// 人类局选择与提交的间隙可能被其它效果移走，已离手的不再二次移动、费用照计=选择已作出）
        /// → 差额=min(己费−对费, <see cref="ClashDiffCap"/>) ≥ 门槛 → <see cref="ClashResolvedEvent"/>
        /// 成败两态必发（字段零改动：My/TheirReveal=舍弃卡、My/TheirPower=费用——协议/回放兼容）。
        /// shortCircuit=发起方拒绝/空手：应对方免问，按双方 0 费收口（成败两态口径保留）。</summary>
        private static (bool success, string detail) CommitClashDuel(Player initiator, Card engineCard,
            int threshold, Card myPick, Card theirPick, GameCore core, bool shortCircuit)
        {
            int myCost = ClashCostOf(myPick);
            int theirCost = ClashCostOf(theirPick);

            CommitClashDiscard(core, initiator, myPick, engineCard);
            CommitClashDiscard(core, initiator.Opponent, theirPick, engineCard);

            if (shortCircuit)
            {
                EventManager.Instance.Publish(new ClashResolvedEvent
                {
                    EngineCard = engineCard, AttackingPlayer = initiator,
                    MyReveal = null, TheirReveal = null,
                    MyPower = 0, TheirPower = 0,
                    Threshold = threshold, Success = false,
                });
                return (false, "拼抢：发起方未舍弃（费用0）——应对方免问，未达门槛");
            }

            int diff = Math.Min(myCost - theirCost, ClashDiffCap);
            bool success = diff >= threshold;
            EventManager.Instance.Publish(new ClashResolvedEvent
            {
                EngineCard = engineCard, AttackingPlayer = initiator,
                MyReveal = myPick, TheirReveal = theirPick,
                MyPower = myCost, TheirPower = theirCost,
                Threshold = threshold, Success = success,
            });
            string myText = myPick != null ? $"我方舍弃{NameOf(myPick)}（费{myCost}）" : "我方拒绝（费0）";
            string theirText = theirPick != null ? $"对手舍弃{NameOf(theirPick)}（费{theirCost}）" : "对手拒绝（费0）";
            return (success, $"拼抢 {myCost} vs {theirCost}（{myText}／{theirText}，差额 {diff}{(success ? " ≥ " : " < ")}门槛 {threshold}）");
        }

        /// <summary>单张落墓（弃牌事件与 DiscardCardHandler 同构；Source=引擎卡）。</summary>
        private static void CommitClashDiscard(GameCore core, Player owner, Card pick, Card source)
        {
            if (pick == null || pick.GetZone() != Zone.Hand) return;
            core.ZoneManager.GetZoneContainer(owner).Move(pick, Zone.Hand, Zone.Graveyard);
            EventManager.Instance.Publish(new CardDiscardEvent { Player = owner, Card = pick, Source = source });
        }

        private static string NameOf(Card card)
            => card != null ? $"【{(card as CardWrapper)?.GetData()?.CardName}】" : "（未舍弃）";

        // ======================================== 死亡计数 / 元素充盈（2026-09-22 定案） ========================================

        /// <summary>死亡事件计数（2026-11-08 阶段3：只扫战场——手牌卡不计数）：场上此卡计数双方生物死亡——
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
            // 2026-11-10 用户定案：死亡计数只在**手牌中**累计；进战场后冻结、不再加；
            // 不再有门槛奖励——计数留作该卡 {value} 的增幅（离场才清，见 CounterRules.ClearAll 重载）。
            foreach (var card in zm.GetCards(player, Zone.Hand).ToList())
            {
                if (card == null || !card.IsAlive) continue;
                foreach (var (def, payload) in EnginePayloadsOf(card, BranchEngineKind.DeathToll))
                {
                    int count = card.GetCounterCount(Attribute.CounterRules.DeathTollCounter);
                    int cap = payload.EngineParam > 0 ? payload.EngineParam : DeathTollCap; // 增幅9/普通6
                    if (count >= cap) break; // 满值不再计数
                    card.AddCounters(Attribute.CounterRules.DeathTollCounter, 1);
                    count++;
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card, Keyword = "死亡计数",
                        Detail = $"手牌计数 +1 → {count}（满值 {cap}；进战场冻结，离场清零，增幅 value）",
                    });
                    _ = def;
                    break;
                }
            }
        }

        /// <summary>主动发动条件评估（2026-10-30 双选组主动路径——EffectExecutionEngine.ApplyActiveEngineTrunkAsync
        /// 消费；**拼抢 2026-11-18 改版起主动档已特判走异步口 <see cref="ResolveActiveClashAsync"/>**，
        /// 本同步口的拼抢分支仅作防御位=确定性代选不弹窗）：主动发动时**立即**评估引擎条件，返回达标与否；
        /// 副作用与自动路径完全同源——拼抢=双方代选手牌暗舍弃比费用（CommitClashDuel 单源）；死亡计数=
        /// 读计数 ≥ 门槛（**不清零**——换区才清，同自动路径）；团结=注入邻接计数（未接线=0 恒不达标）。
        /// detail=成败播报文案（调用方负责 KeywordAppliedEvent 发布）。
        /// 运势（掷骰）/倒计时/元素充盈（仅自动）/附加诅咒·祝福（施放时部署）不经本口。</summary>
        public static bool EvaluateActiveCondition(BranchEngineKind kind, BranchPayload payload, Card card,
            Player player, EffectDefinition def, out string detail)
        {
            detail = null;
            var core = GameCore.Instance;
            switch (kind)
            {
                case BranchEngineKind.Clash:
                {
                    // 2026-11-18 改版：拼抢=手牌暗舍弃比费用。同步口只跑确定性代选策略（不弹窗）——
                    // 人类交互版由 ApplyActiveEngineTrunkAsync 特判 ResolveActiveClashAsync 承接。
                    if (player == null || core?.ZoneManager == null) return false;
                    var zm = core.ZoneManager;
                    int threshold = CostDerivationService.EngineRewardThreshold(payload.Then,
                        def?.TriggerTiming ?? TriggerTiming.OnStandbyPhase); // 单原子费×宿主时机系数
                    var myHand = zm.GetCards(player, Zone.Hand).Where(c => c != null && c.IsAlive).ToList();
                    var myPick = myHand.Count > 0 ? AutoClashPick(myHand, threshold, initiator: true) : null;
                    if (myPick == null)
                    {
                        var (r0, d0) = CommitClashDuel(player, card, threshold, null, null, core, shortCircuit: true);
                        detail = d0;
                        return r0;
                    }
                    var oppHand = zm.GetCards(player.Opponent, Zone.Hand).Where(c => c != null && c.IsAlive).ToList();
                    var theirPick = oppHand.Count > 0 ? AutoClashPick(oppHand, threshold, initiator: false) : null;
                    var (result, resultDetail) = CommitClashDuel(player, card, threshold, myPick, theirPick, core,
                        shortCircuit: false);
                    detail = resultDetail;
                    return result;
                }
                case BranchEngineKind.DeathToll:
                {
                    // 2026-11-10：死亡计数改为被动 value 增幅（手牌累计、进战场冻结、离场清零），
                    // 不再有“计数≥x 触发奖励”的主动发动口。
                    detail = "死亡计数为被动增幅：手牌累计，进战场冻结，离场清零";
                    return false;
                }
                case BranchEngineKind.Unity:
                {
                    ComposerCatalog.EngineParamRange(BranchEngineKind.Unity, out int uMin, out int uMax);
                    int x = Math.Max(uMin, Math.Min(uMax, payload.EngineParam));
                    int n = card != null && UnityAdjacentCountOf != null ? UnityAdjacentCountOf(card) : 0;
                    bool success = n >= x;
                    detail = $"相邻己方生物 {n}{(success ? " ≥ " : " < ")}{x}（只算相邻己方生物、不含自身）";
                    return success;
                }
                default:
                    return false;
            }
        }

        //（团结引擎评估口 OnCardCastSettled 已随 2026-11-08 阶段3 整删：时机=奖励效果时机、
        //  归属=施放者——统一迁 OnRoutedEvent（CardPlayEvent/CardCastSettledEvent 等按 RewardTimingOf
        //  匹配）；元素充盈此前已迁 FlushCardCastSettled 连锁终点。GameActions 调用点同批删除。）

        /// <summary>效果结算后连锁终点 flush（StackEngine.FinishResolution 稳定 break 处调用——
        /// 2026-11-06 判据七·充盈改造）：逐条补发 CardCastSettledEvent（OnCardCastSettled 观察族——
        /// 普通触发式经 TriggerEngine 入待发队列，由 FinishResolution 的 continue 再走一轮处理），
        /// 并对每条记录做**双方**充盈评估（2026-11-18 第二版：**仅对手回合**——持有者自己的回合不评估）。充盈奖励恒直发，
        /// 其产生的新触发/SBA 同样由调用方下一轮收敛。先取走清单再处理——flush 内奖励若引发新施放
        /// （新账），由下一轮 flush 消化，无重入风险。2026-11-04 横置触发口（OnOpponentTappedLands）
        /// 随判据七退役：单口=使用卡牌结算后。</summary>
        public static void FlushCardCastSettled()
        {
            var core = GameCore.Instance;
            if (core == null || _pendingCastSettles.Count == 0) return;
            var notes = _pendingCastSettles.ToArray();
            _pendingCastSettles.Clear();
            foreach (var note in notes)
            {
                // 发布期构造（EventId 构造期分配——迟构造迟发布，Id 与发布序一致；统一路由口：
                // 替代检查→总线→引擎→Trigger/Layer 全观察）
                core.PublishEvent(new CardCastSettledEvent
                {
                    Caster = note.caster, PlayedCard = note.card, ModeIndex = note.modeIndex,
                });
                // 2026-11-18 用户定案（第三版·**己方释放法术后**触发）：只评估**施放者本人**，
                // 且仅当施放物是**法术**——结界/生物（深潭碑等）施放不触发（回响自繁殖断链）；
                // 蓝套自己的法术（潮汐/回潮）能触发自己的碑。
                if (note.caster != null && note.card is IHasSupertype hs && hs.Supertype == Cardtype.Spell)
                    EvaluateManaSurplusEngines(note.caster, core);
            }
        }

        /// <summary>充盈评估（2026-11-18 用户定案·第三版：**己方释放法术后**触发，不限回合）：
        /// 由 <see cref="FlushCardCastSettled"/> 在连锁终点对「施放者本人 + 施放物是法术」调用一次——
        /// 引擎主=owner，差=双方剩余元素总量（GetRemainingElementTotal=未横置地牌+钱包，与支付同资源口径）
        /// 之差（己−对，封顶 <see cref="SurplusCap"/>）**≥ x**（Then 锚价推导）即执行奖励；
        /// 无每回合次数上限（2026-11-14：结界宿主每次触发扣 1 点耐久、生物宿主在挂载价 ×f 付费）；
        /// 同卡单载荷单次。历史：2026-11-06 起「双方施放·双方评估」→ 2026-11-18 定稿「己方施放法术」，
        /// 同时解决深潭碑回响自繁殖与蓝套自触发节奏。</summary>
        private static void EvaluateManaSurplusEngines(Player owner, GameCore core)
        {
            var zm = core?.ZoneManager;
            if (owner == null || zm == null) return;
            // 2026-11-18 第三版：不再按回合归属设闸（由调用点限定「施放者本人 + 法术」）。
            // 2026-11-04：headless 下 owner.Opponent 可能未接线 → 按座位取（沿旧口径）
            var opponent = ReferenceEquals(owner, core.Player1) ? core.Player2
                         : ReferenceEquals(owner, core.Player2) ? core.Player1 : owner.Opponent;
            if (opponent == null) return;

            foreach (var card in zm.GetCards(owner, Zone.Battlefield).ToList())
            {
                if (card == null || !card.IsAlive) continue;
                foreach (var (def, payload) in EnginePayloadsOf(card, BranchEngineKind.ManaSurplus))
                {
                    // 2026-10-09 自平衡统一：门槛=Then 奖励锚价推导（与拼抢同式）——奖励越贵需领先越多
                    int x = CostDerivationService.EngineRewardThreshold(payload.Then, def.TriggerTiming);
                    // 2026-11-10：先算引擎自身机制上限（充盈差值封顶 6）——x 超上限=死档，不参与判定
                    if (x > SurplusCap)
                    {
                        TideLog.Warn($"[BranchEngines] 元素充盈门槛 x={x} 超过差值封顶 {SurplusCap}（死档）"
                                     + $"——卡 {NameOf(card)} 奖励费×时机系数过高，请降奖励或换时机");
                        continue;
                    }
                    int mine = core.ElementPool?.GetRemainingElementTotal(owner) ?? 0;
                    int theirs = core.ElementPool?.GetRemainingElementTotal(opponent) ?? 0;
                    int diff = Math.Min(mine - theirs, SurplusCap);
                    if (diff < x) continue; // 2026-11-13 开放：差 **≥** 门槛即触发（旧「恰等于 ==x」判据七退役）

                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card, Keyword = "元素充盈",
                        Detail = $"效果结算后剩余元素差 {diff}（己 {mine}−对 {theirs}，封顶 {SurplusCap}）≥ {x}：执行奖励（双方施放·连锁终点·自动）",
                    });
                    LaunchReward(payload, def, card, owner, core);
                    //（2026-11-13：无每回合上限——单次施放每卡只结算首个达标载荷，耐久/挂载价即刹车）
                    break;
                }
            }
        }

        /// <summary>场上充盈引擎门槛最大值（2026-11-02 蓝套留费动态化·AI 只读口）：owner 战场上
        /// ManaSurplus 引擎的最高门槛 x（Then 锚价推导——与 EvaluateManaSurplusEngines 判定同式）；
        /// 无引擎=0（兼作「己方场上是否存在充盈引擎」判定）。只读，不影响引擎结算。</summary>
        public static int MaxManaSurplusThreshold(Player owner)
        {
            int best = 0;
            var zm = GameCore.Instance?.ZoneManager;
            if (owner == null || zm == null) return best;
            foreach (var card in zm.GetCards(owner, Zone.Battlefield) ?? new List<Card>())
            {
                if (card == null || !card.IsAlive) continue;
                foreach (var (def, payload) in EnginePayloadsOf(card, BranchEngineKind.ManaSurplus))
                    best = Math.Max(best, CostDerivationService.EngineRewardThreshold(payload.Then, def.TriggerTiming));
            }
            return best;
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

        /// <summary>附加诅咒/附加祝福部署（2026-10-08 引擎主干化；2026-11-06 载荷效果化）：施放结算时
        /// 一次性投放——随机 x 张（GameRng，不给施放者看牌库——炉石式随机即结果）各挂 1 层 +
        /// CurseSystem.AttachEngine 登记载荷（payload.EffectId 效果引用主通道 / Then 原子列兼容通道）；
        /// 空库=本次施放空转（不部署不登记）。同卡多载荷取首个部署（倒计时入场同款口径）；
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
                    int actualDelta = target.AddCounters(counterId, 1, player);
                    // 2026-11-06 载荷效果化：载荷=效果库组合效果引用（payload.EffectId，主通道——
                    // 祝福=极性+1/诅咒=极性−1 主动效果）；Then 原子列=旧手写数据兼容通道
                    CurseSystem.AttachEngine(target, counterId, payload.EffectId, payload.Then, player, card);
                    EventManager.Instance.Publish(new Attribute.CounterChangedEvent
                    {
                        Target = target,
                        CounterType = counterId,
                        Amount = actualDelta,
                        Source = player,
                    });
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = target, Keyword = keyword,
                        Detail = $"{keyword}附加：抽到该卡时自动执行{keyword}分支效果（一次性）",
                    });
                    // 2026-11-03 展示定案：隐藏区的卡成为效果作用目标即自动展示（被附加诅咒/祝福的
                    // 牌库卡对双方可查——RevealRules.Reveal 单源，二值不叠层；抽到换区自清）。
                    Attribute.RevealRules.Reveal(target, player);
                }
                break;
            }
        }

        /// <summary>执行 Then 奖励原子（依次弹选目标——两槽定案；无头/AI 自动选首保 verify 确定性）。
        /// per 原子独立上下文（引擎无当前目标）。</summary>

        /// <summary>奖励发射统一口（2026-10-20 用户定案；**2026-11-08 阶段3 定案：固定三一律直发**）：
        /// 固定三引擎（倒计时/死亡计数/元素充盈，ComposerCatalog.IsActiveRewardEngine）主干触发立即生效——
        /// **忽略 tactivate**（payload.ThenActivation 与 def 发动方式均不看），奖励虽为主动效果
        ///（资格闸 EngineRewardRejectReason 钉主动档）也**不入待发队列**——直发 FireRewards，
        /// 目标仍走结算期弹选。自动三（运势/拼抢/团结）奖励=自动效果：launch 分流恒落直发分支
        ///（入队段仅剩对手写 tactivate=2 数据的防御位）；附加诅咒/祝福不经此口（CurseSystem）。
        /// 2026-11-07 分支奖励效果化：双通道（EffectId 效果引用+Then 原子）——入队段引用效果
        /// 单独入队（Steps 保真· prepaid），直发段两通道一并结算。</summary>
        private static void LaunchReward(BranchPayload payload, EffectDefinition def, Card source,
            Player controller, GameCore core, IGameEvent triggeringEvent = null)
        {
            if (BranchPayloadEmpty(payload)) return;
            // 结界宿主的奖励发动成本（2026-11-14 档位制）：**每次触发扣 1 点耐久**（固定值）——
            // 生物宿主在挂载价 ×f 里已付、法术宿主无耐久（PaysTriggerDurability 内判）。
            bool payDurability = PaysTriggerDurability(source);
            // 固定三（2026-11-08 阶段3）：主干与奖励不拆开——无论 tactivate/效果头声明恒直发
            //（倒计时 2026-10-20 固定触发、充盈 2026-10-20 判据五「只能是自动效果」定案的延伸，
            //  死亡计数 2026-11-08 入列——主动奖励入待发队列的旧注释口径随之作废）。
            // 直发≠免弹选：目标仍走 ExecuteBranchRewardsAsync 结算期弹选（控制者择目标）。
            if (ComposerCatalog.IsActiveRewardEngine(payload.EngineKind))
            {
                FireRewards(payload, source, controller, core, payDurability);
                return;
            }
            var launch = payload.ThenActivation >= 0
                ? (EffectActivationType)payload.ThenActivation
                : (def?.ActivationType ?? EffectActivationType.Automatic);
            if (launch == EffectActivationType.Voluntary && core?.StackEngine != null)
            {
                // 奖励效果引用（2026-11-07）：引用效果单独入队（定义保真——Steps/时机/编排原样，
                // prepaid 落实费用 0）；Then 原子照旧 BRANCH_REWARD 合成 def 入队。先效果后原子。
                if (!string.IsNullOrEmpty(payload.EffectId))
                {
                    var refDef = ResolveRewardFx(payload.EffectId);
                    if (refDef != null)
                    {
                        var queued = new EffectDefinition
                        {
                            Id = refDef.Id,
                            DisplayName = refDef.DisplayName,
                            TriggerTiming = refDef.TriggerTiming,
                            ActivationType = EffectActivationType.Voluntary,
                            ElementCostPrepaid = true,
                            TargetCount = refDef.TargetCount,
                            SelectionMode = refDef.SelectionMode,
                            RandomTarget = refDef.RandomTarget,
                            Effects = refDef.Effects,
                            Steps = refDef.Steps,
                        };
                        core.StackEngine.AddPendingEffect(PendingEffect.Create(
                            queued, source, controller,
                            core.TurnEngine?.TurnPlayer ?? controller,
                            core.TurnEngine?.CurrentPhase?.Phase ?? PhaseType.Main,
                            triggeringEvent: triggeringEvent));
                    }
                }
                if (payload.Then != null && payload.Then.Count > 0)
                {
                    var rewardDef = new EffectDefinition
                    {
                        Id = "BRANCH_REWARD",
                        DisplayName = $"{ComposerCatalog.EngineZhOf(payload.EngineKind)}奖励",
                        TriggerTiming = def?.TriggerTiming ?? TriggerTiming.OnStandbyPhase,
                        ActivationType = EffectActivationType.Voluntary,
                        ElementCostPrepaid = true,
                        Effects = payload.Then,
                    };
                    core.StackEngine.AddPendingEffect(PendingEffect.Create(
                        rewardDef, source, controller,
                        core.TurnEngine?.TurnPlayer ?? controller,
                        core.TurnEngine?.CurrentPhase?.Phase ?? PhaseType.Main,
                        triggeringEvent: triggeringEvent));
                }
                return;
            }
            FireRewards(payload, source, controller, core, payDurability);
        }

        /// <summary>分支载荷双通道皆空（Then 原子列空 ∧ 效果引用空）=无奖励可发
        ///（2026-11-07 分支奖励效果化——发射口统一闸；EffectExecutor.BranchPayloadEmpty 同口径）。</summary>
        private static bool BranchPayloadEmpty(BranchPayload payload)
            => (payload?.Then == null || payload.Then.Count == 0)
               && string.IsNullOrEmpty(payload?.EffectId);

        /// <summary>奖励效果库条目 → 定义（缓存键=库 DTO 实例——Reload 换实例即失效重解析，
        /// 与 CurseSystem.Reset 语义对齐：编辑器改表重载后不喂旧定义；负缓存同存）。</summary>
        private static readonly Dictionary<string, EffectSlimDto> _rewardFxSrc =
            new Dictionary<string, EffectSlimDto>();
        private static readonly Dictionary<string, EffectDefinition> _rewardFxCache =
            new Dictionary<string, EffectDefinition>();

        private static EffectDefinition ResolveRewardFx(string effectId)
        {
            var src = EffectsLibrary.Get(effectId);
            if (_rewardFxCache.TryGetValue(effectId, out var cached)
                && ReferenceEquals(_rewardFxSrc[effectId], src))
                return cached;
            var def = src != null ? CardEffectConverter.ConvertOne(EffectsLibrary.Resolve(effectId), effectId) : null;
            _rewardFxSrc[effectId] = src;
            _rewardFxCache[effectId] = def;
            return def;
        }

        private static void FireRewards(BranchPayload payload, Card source, Player controller, GameCore core,
            bool payDurability = false)
        {
            if (BranchPayloadEmpty(payload)) return;
            // 结界宿主：每次触发扣 1 点耐久（2026-11-14 档位制）。
            // 生物宿主在挂载价 ×f 已付、法术无耐久（payDurability=false）。
            if (payDurability && source != null)
                Attribute.CounterRules.ConsumeTriggerDurability(core, source, "引擎奖励发动（耐久−1/次）");
            var ctx = new EffectExecutionContext
            {
                Source = source,
                Controller = controller,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = -1,
            };
            EffectExecutor.ExecuteBranchRewardsAsync(payload, ctx, null).Forget(); // 双通道：先效果引用后原子（2026-11-07）
        }

        private static IEnumerable<EffectDefinition> DefsOf(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null) return Enumerable.Empty<EffectDefinition>();
            return CardEffectConverter.ConvertAll(data.Effects, data.ID).Where(d => d != null);
        }
    }
}






