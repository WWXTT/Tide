using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using GameBoard;
using CardCore.Attribute.Handlers;
using Cysharp.Threading.Tasks;

namespace CardCore.Editor
{
    public static partial class CardPipelineVerifier
    {
        // ======================================== 分支体系正规化（2026-09-13 定案） ========================================

        /// <summary>
        /// 固定分支门附加费（伤害命中1/击杀2/宣言命中2，灰；奖励原子 0 费）+ 动态分支引擎端到端：
        /// 倒计时（奖励推导费换算回合，1费=1回合；归零发奖并重置）/ 运势（2d6 双&gt;x，x=1 钉种子 20 回合内必中）/
        /// 拼点（双方牌库各随机取样一张生物比攻击力，差额≥门槛；只读取样；无生物=0）。
        /// </summary>
        private static void TestBranchEngines(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 固定分支条件附加费（2026-10-05 载荷口径：槽级 Branch 载荷承载条件+Then）----
            CardCore.EffectDefinition GateDef(string gateId)
            {
                var data = new CardEffectData
                {
                    Id = "VERIFY_GATE_" + gateId,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 产出条件族（DmgKillsTarget/DeclareHit）走 Outcome 载荷——与 converter 折叠归类同源；
                        // 局面门走 Gate 载荷（2026-10-09 还原：达标奖励/不达标无事）
                        CardCore.ComposerCatalog.IsOutcomeCondition(gateId)
                            ? OutcomeAtom(CardCore.AtomicEffectType.DealDamage, 3, gateId,
                                AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1))
                            : GateAtom(CardCore.AtomicEffectType.DealDamage, 3, gateId,
                                AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                    },
                };
                return CardEffectConverter.ConvertOne(data, "VERIFY_GATE");
            }
            int GrayOf(CardCore.EffectDefinition d) => (int)CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Gray];
            int BlueOf(CardCore.EffectDefinition d) => (int)CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Blue];
            // 2026-09-14 用户口径定案（废除 09-13 灰费）：分支=纯校验上限，零计价——奖励免费，条件不产生任何费用
            Assert(GrayOf(GateDef("DmgKillsTarget")) == 0, "产出条件零计价：击杀门不产生灰费（奖励免费）");
            Assert(GrayOf(GateDef("DeclareHit")) == 0, "产出条件零计价：宣言门不产生灰费");
            Assert(GrayOf(GateDef("LifeBelowOpp")) == 0, "局面门零计价：有限分支还原后同口径（奖励免费）");
            Assert(BlueOf(GateDef("DmgKillsTarget")) == 0, "条件奖励免费：then 抽1（锚蓝2）不计入费用");
            CardCore.EffectDefinition SubGateDef()
            {
                var data = new CardEffectData
                {
                    Id = "VERIFY_GATE_SUB",
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        OutcomeAtom(CardCore.AtomicEffectType.DealDamage, 3, "DmgKillsTarget",
                            AtomRefs.New(CardCore.AtomicEffectType.Heal, value: 2)), // 锚1（回2命=1费）< 门预算2，合法
                    },
                };
                return CardEffectConverter.ConvertOne(data, "VERIFY_GATE_SUB");
            }
            Assert(GrayOf(SubGateDef()) == 0 && CardCore.CostDerivationService.DeriveElementCosts(SubGateDef())
                   [CardCore.ManaType.Green] == 0,
                   "奖励低于预算同样零计价（预算只是放置校验上限，不是收费）");

            // ---- 1b. 护甲部分吸收（毒刺/冰晶改写族 2026-10-15 整体退役——「剩余改写」断言随退；
            //      本段改钉现行护甲语义：逐层吸收、剩余照常落血）----
            var stingSrc = SpawnTier(core, p1, 3, 3);
            var stingTgt = SpawnTier(core, p2, 3, 5);
            // 防护层优先：圣盾完全挡住（全额吸收、盾层消耗）
            var shielded = SpawnTier(core, p2, 3, 5);
            shielded.AddCounters(CardCore.Attribute.CounterRules.DivineShieldCounter, 1, shielded); // 圣盾已指示物化
            int shieldLife = shielded.GetLife();
            CardCore.Attribute.KeywordRules.ApplyDamage(stingSrc, shielded, 3, true);
            Assert(shielded.GetLife() == shieldLife
                   && shielded.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "防护优先：圣盾完全挡住（盾层消耗、无落血）");
            RetireCards(core, p2, shielded);

            // 部分吸收：护甲 2 挡 3 伤中的 2 → 剩余 1 照常落血（护甲耗尽）
            var armoredTgt = SpawnTier(core, p2, 3, 5);
            armoredTgt.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 2);
            int armoredLife = armoredTgt.GetLife();
            CardCore.Attribute.KeywordRules.ApplyDamage(stingSrc, armoredTgt, 3, true);
            Assert(armoredTgt.GetLife() == armoredLife - 1
                   && armoredTgt.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   $"部分吸收：护甲逐层吃掉 2、剩余 1 照常落血（护甲耗尽）——实际 life {armoredLife}→{armoredTgt.GetLife()} 护甲×{armoredTgt.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter)}");
            RetireCards(core, p2, armoredTgt);
            RetireCards(core, p1, stingSrc);

            // 无实体（2026-11-02 指示物化，表行 6c38edaa 挪指示物表）：持有期间每次受到的伤害固定
            // 变为 1——战斗+非战斗同口；衰退型不按次消耗（层=持续回合）
            var wardUnit = SpawnTier(core, p1, 2, 5);
            wardUnit.AddCounters(CardCore.Attribute.CounterRules.SpellbanCounter, 1, wardUnit);
            int wardLife = wardUnit.GetLife();
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, wardUnit, 3, false);
            Assert(wardUnit.GetLife() == wardLife - 1, "无实体：非战斗伤害变为 1");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, wardUnit, 3, true);
            Assert(wardUnit.GetLife() == wardLife - 2, "无实体：战斗伤害同为 1（累计 -2）");
            RetireCards(core, p1, wardUnit);

            // 绝缘体（2026-11-09 用户口径：**全部指示物不拦添加**——层照常落账，效果走效果口径
            // 读数压制；失效/沉睡两个压制源=死层：层照常落账并正常倒数，但不横置、不拦重置、
            // 不压自动/主动，正反序一致）。
            var insUnit = SpawnTier(core, p1, 2, 5);
            insUnit.AddKeyword(CardCore.Attribute.KeywordRules.Insulator, CardCore.KeywordLane.Temp, insUnit);
            var insCtl = SpawnTier(core, p1, 2, 5);
            insUnit.AddCounters(CardCore.Attribute.KeywordRules.FreezeCounter, 2, p2);
            Assert(insUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 2
                   && insUnit.GetEffectiveCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 0,
                   "绝缘体：敌方冻结层照常落账（不拦添加），效果读数压制为 0");
            insUnit.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 1, p1);
            Assert(insUnit.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 1
                   && insUnit.GetEffectiveCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   "绝缘体：己方护甲同样入账无己/敌豁免，效果读 0");
            insCtl.AddCounters(CardCore.Attribute.KeywordRules.FreezeCounter, 2, p2);
            Assert(insCtl.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 2
                   && insCtl.GetEffectiveCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 2,
                   "绝缘体对照：无关键词单位冻结正常上 2 层且生效");

            // 失效层：照常落账 + 零效果（正序）
            var insNull = SpawnTier(core, p1, 2, 5);
            insNull.AddKeyword(CardCore.Attribute.KeywordRules.Insulator, CardCore.KeywordLane.Temp, insNull);
            int nullDelta = insNull.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1, p2);
            Assert(nullDelta == 1
                   && insNull.GetCounterCount(CardCore.Attribute.CounterRules.NullifyCounter) == 1
                   && insNull.HasKeyword(CardCore.Attribute.KeywordRules.Insulator)
                   && !CardCore.Attribute.SuppressRules.IsAutoSuppressed(insNull),
                   $"绝缘体：失效层照常落账、零效果（+{nullDelta} 层；绝缘体仍真、自动效果不被压）");

            // 沉睡层（真实原子口）：照常落账、不横置、不拦重置、不压效果
            var insSleep = SpawnTier(core, p1, 2, 5);
            insSleep.AddKeyword(CardCore.Attribute.KeywordRules.Insulator, CardCore.KeywordLane.Temp, insSleep);
            new SleepHandler().Execute(
                new AtomicEffectInstance { Type = AtomicEffectType.Sleep, Value = 1 },
                new EffectExecutionContext
                {
                    Source = p2, Controller = p2,
                    Targets = new List<Entity> { insSleep },
                    ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                });
            Assert(insSleep.GetCounterCount(CardCore.Attribute.KeywordRules.SleepCounter) == 1
                   && !insSleep.IsTapped()
                   && insSleep.GetEffectiveCounterCount(CardCore.Attribute.KeywordRules.SleepCounter) == 0
                   && !CardCore.Attribute.SuppressRules.IsAutoSuppressed(insSleep),
                   "绝缘体：沉睡层照常落账、不横置、零效果");
            Assert(!CardCore.Attribute.SleepFreezeUntapBlockRule.Instance.BlocksUntap(insSleep),
                   "绝缘体：沉睡层不拦重置（效果口径）");

            // 反序：先失效后获绝缘——失效层转死层，绝缘体不被压
            var lateIns = SpawnTier(core, p1, 2, 5);
            lateIns.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1, p2);
            lateIns.AddKeyword(CardCore.Attribute.KeywordRules.Insulator, CardCore.KeywordLane.Temp, lateIns);
            Assert(lateIns.GetCounterCount(CardCore.Attribute.CounterRules.NullifyCounter) == 1
                   && lateIns.HasKeyword(CardCore.Attribute.KeywordRules.Insulator)
                   && !CardCore.Attribute.SuppressRules.IsAutoSuppressed(lateIns),
                   "绝缘体反序：先失效后获绝缘——失效层被无效化（正反序一致）");

            // 时序：先沉睡后获绝缘——沉睡转死层，绝缘体立刻生效，其后新加冻结照常落账但效果读 0
            var insOrder = SpawnTier(core, p1, 2, 5);
            insOrder.AddCounters(CardCore.Attribute.KeywordRules.SleepCounter, 1, p2);
            insOrder.AddKeyword(CardCore.Attribute.KeywordRules.Insulator, CardCore.KeywordLane.Temp, insOrder);
            int orderFreeze = insOrder.AddCounters(CardCore.Attribute.KeywordRules.FreezeCounter, 1, p2);
            Assert(orderFreeze == 1
                   && insOrder.HasKeyword(CardCore.Attribute.KeywordRules.Insulator)
                   && insOrder.GetEffectiveCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 0,
                   "绝缘体反序：先沉睡后获绝缘——沉睡转死层，其后冻结照常落账但效果读 0");
            RetireCards(core, p1, insUnit);
            RetireCards(core, p1, insCtl);
            RetireCards(core, p1, insNull);
            RetireCards(core, p1, insSleep);
            RetireCards(core, p1, lateIns);
            RetireCards(core, p1, insOrder);

            // ---- 2. 倒计时（槽级载荷）：奖励推导费换算回合；归零发奖并重置 ----
            // 引擎宿主用真实生物（0/0 结界会在 DrainStack 的 SBA 泵里被"防御归零"送墓——死亡计数段同款教训）
            var cdData = new CardData
            {
                ID = "VERIFY_ENGINE_CD", CardName = "验证倒计时", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            cdData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_CD_MAIN",
                // 主动时机=主干不随施放/入场自动结算（主动轮询制）——引擎宿主惰性化：
                // BranchEngines 只扫 def.Effects 的 Branch 载荷，主干 DrawCard 不在场外偷跑
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    // 主干=抽1（宿主原子，不入场不结算）；倒计时载荷 Then=抽1（锚价 2 → 2 回合）
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.Countdown, 0,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var cdCard = new CardWrapper(cdData);
            cdCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(cdCard, p1), "倒计时：引擎宿主入场");

            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            // 入场挂 2 层（奖励锚价 2 → 2 回合）→ 每回合开始 -1 → 归零发奖（抽1）→ 重置回 2。
            // 拦截器跳过回合自动化（防回合抽牌污染牌库读数——拼点段同款口径）
            {
                var cdDef = CardEffectConverter.ConvertAll(cdData.Effects, cdData.ID)
                    .FirstOrDefault(d => d?.Effects != null && d.Effects.Any(a =>
                        a?.Branch?.EngineKind == CardCore.BranchEngineKind.Countdown));
                Crumb($"cd debug: CountdownTurns={cdDef?.Effects.FirstOrDefault(a => a?.Branch != null)?.Branch.CountdownTurns} counterAtStart={cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter)} deck={deckBefore}");
            }
            var cdShield = new TurnStartAutomationShield();
            RuleHooks.RegisterTurnStartInterceptor(cdShield);
            try
            {
                // 2026-10-26 精准切分：回合自动化（含回合抽牌）已迁 RunStandbyAutomation——合成
                // TurnStartEvent 不再触发抽牌，倒计时 tick 显式调用（隔离盾随之只挡显式自动化路径；
                // 新语义=跳过闸连倒计时一并跳，本段隔离口径已由显式 tick 取代）
                CardCore.BranchEngines.RunCountdownTick(p1);
                GameActions.DrainStack(core);
                Crumb($"cd after t1: counter={cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter)} deckNow={core.ZoneManager.GetCards(p1, Zone.Deck).Count}");
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore
                       && cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter) == 1,
                       "倒计时：首回合 2→1 未归零不发奖（tick 显式调用，牌库只受引擎影响）");
                CardCore.BranchEngines.RunCountdownTick(p1);
                GameActions.DrainStack(core);
            }
            finally { RuleHooks.UnregisterTurnStartInterceptor(cdShield); }
            // 倒计时发射窗=持有者准备阶段（引擎头预设默认 OnStandbyPhase——2026-11-14 档位制起
            // OnTurnStart 不再归一，只有显式声明才走回合开始）：归零自动发奖（抽1）并重挂回初值。
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   $"倒计时：归零自动发奖抽1（引擎发射窗=准备相位）——诊断 deck {deckBefore}→{core.ZoneManager.GetCards(p1, Zone.Deck).Count}");
            Assert(cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter) == 2,
                   "倒计时：归零后重置回初值（2 层——奖励锚价 2 换算）");
            core.ZoneManager.MoveCard(cdCard, p1, Zone.Battlefield, Zone.Graveyard); // 清场（换区清计数）

            // ---- 3. 运势（槽级载荷）：2d6 双 > x；x 费灰；x=1 钉种子 20 回合内必中 ----
            var luckData = new CardData
            {
                ID = "VERIFY_ENGINE_LUCK", CardName = "验证运势", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            luckData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_LUCK_MAIN",
                // 2026-10-16 解绑：掷骰随**声明时机**的事件驱动（OnRoutedEvent）——须 OnTurnStart
                // 才会在回合事件上评估（Activate 无对应事件永不会被掷）；主干改 ModifyPower（抽牌
                // 主干会每回合自动抽 1 污染牌库读数——OnTurnStart 宿主无法再用主动惰性化）
                TriggerTiming = (int)CardCore.TriggerTiming.OnTurnStart,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    // 干干须为**表行原子**（ModifyPower 系运行时原子无表行——refId 解析失败会被
                    // 转换器整颗剔除、载荷陪葬）；GrantTaunt 无牌库/生命可观察副作用
                    EngineAtom(CardCore.AtomicEffectType.GrantTaunt, 1, CardCore.BranchEngineKind.LuckRoll, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var luckDef = CardEffectConverter.ConvertOne(luckData.Effects[0], luckData.ID);
            int luckGray = (int)CardCore.CostDerivationService.DeriveElementCosts(luckDef)[CardCore.ManaType.Gray];
            Assert(luckGray == 0, "运势计价（2026-09-15 废灰费）：x=纯概率门槛，零计价（奖励原子 0 费）");

            var luckCard = new CardWrapper(luckData);
            luckCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(luckCard, p1), "运势：引擎宿主入场");
            deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            GameRng.Reseed(20260913);
            bool luckHit = false;
            var luckEvents = new List<string>();
            void OnLuckApplied(CardCore.KeywordAppliedEvent e) { if (e?.Keyword == "运势") luckEvents.Add(e.Detail); }
            int luckTurnStarts = 0; int luckAdvanceFails = 0;
            void OnLuckTurnStart(TurnStartEvent e) => luckTurnStarts++;
            EventManager.Instance.Subscribe<CardCore.KeywordAppliedEvent>(OnLuckApplied);
            EventManager.Instance.Subscribe<TurnStartEvent>(OnLuckTurnStart);
            var luckShield = new TurnStartAutomationShield();
            RuleHooks.RegisterTurnStartInterceptor(luckShield); // 隔离回合抽牌——只测引擎自身发奖
            try
            {
                for (int i = 0; i < 20 && !luckHit; i++)
                {
                    // 裸 EventManager.Publish 不走路由（GameCore.PublishEventRouted）——掷骰评估
                    // 须真实回合流转，TurnStartEvent 才会经 GameCore 路由到 BranchEngines
                    var luckTurnPlayer = core.TurnEngine.TurnPlayer;
                    GameActions.SkipElementPool(core, luckTurnPlayer);
                    if (!EndTurnPumped(core, luckTurnPlayer)) luckAdvanceFails++;
                    GameActions.DrainStack(core);
                    if (core.ZoneManager.GetCards(p1, Zone.Deck).Count < deckBefore) luckHit = true;
                }
            }
            finally
            {
                RuleHooks.UnregisterTurnStartInterceptor(luckShield);
                EventManager.Instance.Unsubscribe<CardCore.KeywordAppliedEvent>(OnLuckApplied);
                EventManager.Instance.Unsubscribe<TurnStartEvent>(OnLuckTurnStart);
            }
            Crumb($"luck diag: turnStarts={luckTurnStarts} advanceFails={luckAdvanceFails} hits={luckEvents.Count}");
            Assert(luckHit, $"运势运行时：x=1（25/36 命中）真实回合流转 20 回合内至少中一次并执行奖励（掷骰随声明时机事件——2026-10-16 解绑；诊断：回合事件×{luckTurnStarts} 推进失败×{luckAdvanceFails} 命中×{luckEvents.Count}）");
            core.ZoneManager.MoveCard(luckCard, p1, Zone.Battlefield, Zone.Graveyard);

            // ---- 4. 拼点（槽级载荷）：双方牌库各随机取样一张生物，攻击力差 ≥ 门槛；只读取样；无生物=0 ----
            var clashData = new CardData
            {
                ID = "VERIFY_ENGINE_CLASH", CardName = "验证拼点", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            clashData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_CLASH_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 引擎宿主惰性化（同倒计时段注）
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.Clash, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var clashDef = CardEffectConverter.ConvertOne(clashData.Effects[0], clashData.ID);
            int clashGray = (int)CardCore.CostDerivationService.DeriveElementCosts(clashDef)[CardCore.ManaType.Gray];
            Assert(clashGray == 0, "拼点计价（2026-09-15 门槛制）：门槛=奖励锚价（运行时差额≥门槛判），零计价");

            // 2026-10-04 改版（随机生物攻击力）：随机取样需确定性夹具——清双方牌库（移入墓地；
            // 后续 2026-09-22 段前有 PadDeck 15 张兜底），各置唯一生物（p1 攻5 / p2 攻2）→
            // 库内唯一，随机必中 → 差额 5−2=3 ≥ 门槛（抽1 锚价 2）→ 必中
            foreach (var c in core.ZoneManager.GetCards(p1, Zone.Deck).ToList())
                core.ZoneManager.MoveCard(c, p1, Zone.Deck, Zone.Graveyard);
            foreach (var c in core.ZoneManager.GetCards(p2, Zone.Deck).ToList())
                core.ZoneManager.MoveCard(c, p2, Zone.Deck, Zone.Graveyard);
            var topMine = new CardData { ID = "VERIFY_CLASH_TOP_M", CardName = "拼点生物M",
                Supertype = Cardtype.Creature, Power = 5, Life = 5 };
            var topFoe = new CardData { ID = "VERIFY_CLASH_TOP_F", CardName = "拼点生物F",
                Supertype = Cardtype.Creature, Power = 2, Life = 5 };
            core.ZoneManager.GetZoneContainer(p1).Add(new CardWrapper(topMine), Zone.Deck, DeckPosition.Top);
            core.ZoneManager.GetZoneContainer(p2).Add(new CardWrapper(topFoe), Zone.Deck, DeckPosition.Top);

            var clashCard = new CardWrapper(clashData);
            clashCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(clashCard, p1), "拼点：引擎宿主入场");
            deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            System.Func<Card, int> powerOf = c => (c as CardWrapper)?.GetData()?.Power ?? 0;
            Crumb($"clash pre: p1deck={deckBefore} myCreature={powerOf(core.ZoneManager.GetCards(p1, Zone.Deck).First())} "
                + $"foeCreature={powerOf(core.ZoneManager.GetCards(p2, Zone.Deck).First())} "
                + $"p2deck={core.ZoneManager.GetCards(p2, Zone.Deck).Count}");
            // 2026-10-09 触发改版：拼点由攻击宣言触发（仅攻击方战场引擎卡·无限次，无回合自动化干扰）——
            // 攻击宣言无回合抽牌副作用，无需拦截器；库内唯一生物（己方 5 / 对方 2）随机必中。
            // 2026-11-08 阶段3：拼枪评估已自 AttackDeclarationEvent 总线订阅迁 GameCore.PublishEventRouted
            // 钩子（OnRoutedEvent·归属=攻击方）——裸 EventManager.Publish 只进总线不再触发引擎，
            // 须反射 internal 路由单口发布（同效果光环段 FireTap 先例）。
            var clashPublish = typeof(GameCore).GetMethod("PublishEventRouted",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert(clashPublish != null, "拼点段前提：GameCore.PublishEventRouted 可反射（internal 事件路由单口）");
            clashPublish.Invoke(core, new object[] { new AttackDeclarationEvent { AttackingPlayer = p1 }, false });
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "拼点运行时（2026-10-09 触发改版：攻击宣言·仅攻击方）：己方随机生物 5 − 对方 2 = 3 ≥ 门槛 2 → 执行奖励（抽 1）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Any(c =>
                       (c as CardWrapper)?.GetData()?.ID == "VERIFY_CLASH_TOP_M"),
                   "拼点：随机取样只读不移牌——奖励抽 1 抽到的正是牌库中那张生物（取样未预耗）");
            core.ZoneManager.MoveCard(clashCard, p1, Zone.Battlefield, Zone.Graveyard);

            // ======================================== 2026-09-22 定案：死亡计数 / 元素充盈 / 状态门 / 改写门 =======

            // 本段抽牌断言密集——牌库保底（见 PadDeck 注释：抽干=净 0 / 疲劳判负两类失效模式）
            PadDeck(core, p1, 15, "VERIFY_0922_P1_");
            PadDeck(core, p2, 15, "VERIFY_0922_P2_");

            // ---- 5. 死亡计数增幅（2026-11-10 改版定案）：**只在手牌中累计**——双方生物每死 1 只 +1
            //      （增幅档 engineParam=9 → 满值 9；普通档 0 → 满值 6）；**奖励引擎退役**——不再有
            //      门槛→Then 奖励（死亡零抽牌）；进战场**冻结**（手牌→战场换区保留计数，场上死亡
            //      不再累计）；离场**清零**（战场→任何区清，含回手）；计数留作该卡 {value} 原子的增幅：
            //      执行期 bonus = min(9, floor(计数 / 每 +1 value 的费))（1死亡=1费，单价随原子行），
            //      经 EffectHandlerRegistry 临时加到 value、结算后还原；计价侧增幅标记（engineParam≥9）
            //      原子费 ×4（DeathTollAmplificationMultiplier）。----
            var tollData = new CardData
            {
                ID = "VERIFY_ENGINE_TOLL", CardName = "验证死亡计数增幅", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            tollData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_TOLL_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 宿主惰性化（同倒计时段注）——增幅标记不随施放/入场自动结算
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AmpAtom(CardCore.AtomicEffectType.DealDamage, 2, 9), // 增幅档：2 伤 + engineParam=9
                },
            });
            var tollDef = CardEffectConverter.ConvertOne(tollData.Effects[0], tollData.ID);
            Assert(tollDef.Effects.Count == 1 && tollDef.Effects[0].Branch != null
                   && tollDef.Effects[0].Branch.Settle == CardCore.BranchSettleKind.Engine
                   && tollDef.Effects[0].Branch.EngineKind == CardCore.BranchEngineKind.DeathToll
                   && tollDef.Effects[0].Branch.EngineParam == 9
                   && tollDef.Effects[0].Branch.Then.Count == 0,
                   "死亡计数增幅：效果原子 branch 载荷（Engine/DeathToll/engineParam=9）——Then 空（奖励引擎退役）");
            var tollAtom = tollDef.Effects[0];
            // 计价（2026-11-10 定案）：增幅标记原子费 = 基准 ×4；增幅费口 AmplificationFeeOf = +3×正常总费
            var plainTollDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_TOLL_PLAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DealDamage, 2) },
            }, tollData.ID);
            float tollAmpFee = CardCore.CostDerivationService.ComputeAtomCostByColor(
                tollAtom, tollDef, null,
                CardCore.Attribute.AtomicEffectTable.GetByHashId(tollAtom.RowHashId)).Values.Sum();
            float tollPlainFee = CardCore.CostDerivationService.ComputeAtomCostByColor(
                plainTollDef.Effects[0], plainTollDef, null,
                CardCore.Attribute.AtomicEffectTable.GetByHashId(plainTollDef.Effects[0].RowHashId)).Values.Sum();
            Assert(tollPlainFee > 0f
                   && System.Math.Abs(tollAmpFee - tollPlainFee * CardCore.CostDerivationService.DeathTollAmplificationMultiplier) < 0.01f,
                   $"死亡计数计价：增幅标记原子费 = 基准 ×4（实际 amp={tollAmpFee:0.##} plain={tollPlainFee:0.##}）——旧「引擎零计价」随奖励引擎退役");
            Assert(System.Math.Abs(CardCore.CostDerivationService.AmplificationFeeOf(tollAtom, tollDef, null)
                       - (tollAmpFee - tollPlainFee)) < 0.01f,
                   "死亡计数计价：增幅费 AmplificationFeeOf = +3×正常总费（与 ×4 口径同源）");

            // 运行时：A=增幅档宿主（满 9）；B=普通档宿主（满 6）——双双入手（改版后计数载体=手牌）
            var tollCard = new CardWrapper(tollData);
            tollCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(tollCard, p1), "死亡计数：增幅宿主占位入场（转手前置）");
            core.ZoneManager.MoveCard(tollCard, p1, Zone.Battlefield, Zone.Hand); // 载体进手牌
            var toll6Data = new CardData
            {
                ID = "VERIFY_ENGINE_TOLL6", CardName = "验证死亡计数普通档", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            toll6Data.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_TOLL6_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AmpAtom(CardCore.AtomicEffectType.Heal, 1, 0), // 普通档：engineParam=0 → 满值 6、无 ×4 计价标记
                },
            });
            var toll6Card = new CardWrapper(toll6Data);
            toll6Card.SetController(p1);
            Assert(CardEffectConverter.ConvertOne(toll6Data.Effects[0], toll6Data.ID).Effects[0].Branch.EngineParam == 0,
                   "死亡计数：普通档载荷 engineParam=0（<9 不吃 ×4 计价、满值 6）");
            core.ZoneManager.TryAddToBattlefield(toll6Card, p1);
            core.ZoneManager.MoveCard(toll6Card, p1, Zone.Battlefield, Zone.Hand);

            int deckAtToll = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            System.Func<int> tollCount = () =>
                tollCard.GetCounterCount(CardCore.Attribute.CounterRules.DeathTollCounter);
            System.Func<int> toll6Count = () =>
                toll6Card.GetCounterCount(CardCore.Attribute.CounterRules.DeathTollCounter);
            System.Func<string> tollState = () =>
                $"A={tollCount()} B={toll6Count()} deck={core.ZoneManager.GetCards(p1, Zone.Deck).Count} tollZone={tollCard.GetZone()}";
            int tollKillSeq = 0;
            void TollKill()
            {
                var victim = tollKillSeq % 2 == 0 ? SpawnTier(core, p2, 1, 1) : SpawnTier(core, p1, 1, 1); // 双方交替死
                tollKillSeq++;
                CardCore.Attribute.KeywordRules.ApplyDamage(tollKillSeq % 2 == 1 ? p2 : p1, victim, 99, false);
                core.SBAEngine.CheckAndExecute(); // 直接 ApplyDamage 不经栈——手动泵 SBA 送墓（标死→死透→CardDestroyEvent）
                Crumb($"toll kill{tollKillSeq}: " + tollState());
            }
            TollKill(); TollKill(); // 对手侧 + 己方各 1 死
            Assert(tollCount() == 2 && toll6Count() == 2
                   && core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckAtToll,
                   "死亡计数：手牌累计双方生物死亡（对手/己方各 +1 → A=2 B=2）——死亡零抽牌（奖励引擎退役）");
            for (int i = 0; i < 8; i++) TollKill(); // 累计 10 死：A 封顶 9、B 封顶 6
            Assert(tollCount() == 9 && toll6Count() == 6
                   && core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckAtToll,
                   $"死亡计数：满值封顶不再计数（增幅档 engineParam=9 → 9；普通档 0 → 6）——实际 {tollState()}");
            core.ZoneManager.MoveCard(tollCard, p1, Zone.Hand, Zone.Battlefield); // 进战场
            Assert(tollCount() == 9, "死亡计数：手牌→战场换区**保留**计数（冻结载体——ClearAll(Hand,Battlefield) 豁免口）");
            TollKill(); // 场上死亡：A 已冻结不加、B 满值不加
            Assert(tollCount() == 9 && toll6Count() == 6,
                   "死亡计数：进战场后冻结——场上死亡不再累计（EvaluateDeathTollEngines 只扫手牌）");

            // 增幅消费：执行 A 的 {value} 原子（DealDamage 2）——bonus=min(9,floor(计数/每+1费)) 临时加值
            var bigVictim = SpawnTier(core, p2, 0, 50); // 大血靶（不会死于本段伤害）
            float tollStep = CardCore.CostDerivationService.ValueStepFee(tollAtom, tollDef);
            int tollBonus9 = tollStep > 0.0001f ? System.Math.Min(9, (int)System.Math.Floor(9f / tollStep)) : 0;
            Crumb($"toll amplification: step={tollStep:0.###} deaths=9 → bonus={tollBonus9}");
            System.Func<int> AmpDeal = () =>
            {
                int before = bigVictim.GetLife();
                Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(tollAtom, new EffectExecutionContext
                {
                    Source = p1, Controller = p1, CastCard = tollCard, ResolvingEffect = tollDef, // 增幅单价 ValueStepFee 读 def——缺此位则 step=0 零增幅
                    Targets = new List<Entity> { bigVictim },
                    ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                }), "死亡计数增幅：DealDamage 经执行器结算（handler 就绪）");
                return before - bigVictim.GetLife();
            };
            int ampDamage = AmpDeal();
            Assert(ampDamage == 2 + tollBonus9 && tollBonus9 > 0
                   && tollAtom.Value == 2,
                   $"死亡计数增幅：执行期 value 2+{tollBonus9}（bonus=min(9,floor(9/step={tollStep:0.##})) 实际伤害 {ampDamage}）——结算后原子 value 还原 2");
            core.ZoneManager.MoveCard(tollCard, p1, Zone.Battlefield, Zone.Hand); // 离场 → 清零
            Assert(tollCount() == 0, "死亡计数：离场清零（战场→手：ClearAll 无豁免——增幅随计数消失）");
            Assert(AmpDeal() == 2,
                   "死亡计数增幅：离场清零后执行同原子——零增幅回落基础 2（计数=增幅的唯一来源）");
            core.ZoneManager.MoveCard(tollCard, p1, Zone.Hand, Zone.Graveyard);
            core.ZoneManager.MoveCard(toll6Card, p1, Zone.Hand, Zone.Graveyard);

            // ---- 6. 元素充盈（2026-11-06 判据七 ==x·双方）：仅自动——任一方施放的卡成功结算、且整条
            //      连锁结算完成后（NoteCastSettled 记账 → StackEngine.FinishResolution 稳定点
            //      FlushCardCastSettled）双方各自评估己方场上引擎：双方剩余元素总量
            //      （GetRemainingElementTotal=未横置地牌+钱包，支付先扣后算）之差（封顶 6）**恰好等于**
            //      门槛（Then 锚价推导）才触发——每卡每回合 1 次（2026-10-22 全局规则，引擎侧不看
            //      效果行 limit）；己方回合出牌同样评估；非施放支付不触发。判据五/六（==x→≥x 轮替）、
            //      「仅对手回合」单边、横置触发口（2026-11-04）均随判据七退役----
            // 引擎宿主用真实生物：0/0 结界会在出牌结算后的 SBA 泵里被"防御归零"送墓（首跑实证），引擎只活半次
            var surgeData = new CardData
            {
                ID = "VERIFY_ENGINE_SURGE", CardName = "验证元素充盈", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            surgeData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_SURGE_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 引擎宿主惰性化（同倒计时段注）
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.ManaSurplus, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var surgeCard = new CardWrapper(surgeData);
            surgeCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(surgeCard, p1), "元素充盈：引擎宿主入场");
            PadDeck(core, p1, 12, "VERIFY_SURGE_DECK_");  // 引擎侧累计抽 6（门槛2×2 发 + 门槛6×3 发）+ 回合翻转抽牌
            PadDeck(core, p2, 20, "VERIFY_SURGE_DECK2_"); // 对手载体自抽 10 发 + 回合翻转抽牌——垫牌防疲劳干扰

            // 判据供能：无地牌纯钱包口径（剩余=钱包余额；钱包任一回合结束清空——EnsureMainPhase 后重灌）
            var surgePool = core.ElementPool.GetPool(p1);
            var surgePool2 = core.ElementPool.GetPool(p2);
            surgePool.PooledCards.Clear();
            surgePool2.PooledCards.Clear();
            void SurgeWallets(int p1Red, int p2Red)
            {
                foreach (ManaType t in System.Enum.GetValues(typeof(ManaType)))
                { surgePool.TempElements[t] = 0; surgePool2.TempElements[t] = 0; }
                surgePool.TempElements[ManaType.Red] = p1Red;
                surgePool2.TempElements[ManaType.Red] = p2Red;
            }
            // 对手（p2）出牌载体：Spell+AddPowerUp1（灰0.5/点→支付进 1 灰1/张——2026-11-14 基础属性归灰；
            // 判据供能的阶梯口径）。
            // 2026-11-05 改版注：原 DrawCard1 载体锚=蓝2（11-02 颜色规范重分配），色锁后红钱包付不了
            // 蓝账单；恒推导定案下声明费仅显示缓存（旧 Cost 钉红已失效）。载体效果=给 p1 生物+1攻
            // （指示物·无副作用），主动档（法术只挂主动效果——OnPlay 档会进 Derive E 桶与施放锚双扣）。
            CardData SurgeSpell()
            {
                var d = new CardData
                {
                    ID = "VERIFY_SURGE_SPELL", CardName = "充盈载体", Supertype = Cardtype.Spell,
                };
                d.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SURGE_SPELL_FX",
                    TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active,
                    // value 2 = 锚恰 1.0/张（0.5/点，2026-11-14 归灰）→ 载体恒 1 费（判据阶梯按施放后剩余=钱包−1 精确步进）；
                    // 不带 kinds——正极性原子锁对方域（kinds{2}）违反内容契约会被整颗剔除（空效果零费）。
                    // 2026-11-06 注：旧签名 SurgeSpell(float red) 的 red 是死参数——所有载体本就 1 费，
                    // 旧「0费载体」注释为幻觉（旧断言不卡剩余精度故未暴露，判据七 ==x 夹具修正时实证）。
                    AtomicEffects = new List<AtomicEffectEntry>
                        { AtomRefs.New(CardCore.AtomicEffectType.AddPowerUp, value: 2) },
                });
                return d;
            }
            // 拒因直读（出牌失败时面包屑留痕——上游段落状态级联排查用）
            bool SurgeCast(CardData data, string tag)
            {
                var c = InjectCard(core, p2, data);
                if (!GameActions.PlayCard(core, p2, c, null, Zone.Hand, 0, out var why))
                { Crumb($"surge {tag} reject: {why}"); return false; }
                GameActions.DrainStack(core);
                return true;
            }
            int surgeDeck = -1; // 每次 EnsureMainPhase 后重钉基数（回合翻转抽牌会漂移）
            System.Func<string> surgeState = () =>
                $"remain1={core.ElementPool.GetRemainingElementTotal(p1)} remain2={core.ElementPool.GetRemainingElementTotal(p2)} "
                + $"deck1={core.ZoneManager.GetCards(p1, Zone.Deck).Count} surgeZone={surgeCard.GetZone()} "
                + $"turn={core.TurnEngine.TurnPlayer?.Name}";

            // 对手回合阶梯（2026-11-14：差 ≥ 门槛即触发，无每回合上限；旧「==x 恰等」口径退役）：
            // p1 剩 6 恒定，p2 从 6 起每打一张 1 费载体剩余 6→0 → 差 1..6 逐次递进；
            // 门槛 2 宿主在差 ≥2 的每一档抽 1。
            EnsureMainPhase(core, p2); // 对手（p2）回合主阶段——充盈评估窗
            SurgeWallets(p1Red: 6, p2Red: 6);
            surgeDeck = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            Assert(SurgeCast(SurgeSpell(), "ladder1"), "元素充盈：对手载体①（差1）打出");
            Crumb("surge ladder1: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck,
                   "元素充盈：差 1 < 门槛 2 不触发");
            Assert(SurgeCast(SurgeSpell(), "ladder2"), "元素充盈：对手载体②（差2）打出");
            Crumb("surge ladder2: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 1,
                   $"元素充盈：差 2 ≥ 门槛 2 → 门槛2宿主抽1（实际抽 {surgeDeck - core.ZoneManager.GetCards(p1, Zone.Deck).Count}）");
            Assert(SurgeCast(SurgeSpell(), "ladder3"), "元素充盈：对手载体③（差3）打出");
            Crumb("surge ladder3: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 2,
                   $"元素充盈：差 3 ≥ 门槛 2 仍触发（≥x 越线即发；实际抽 {surgeDeck - core.ZoneManager.GetCards(p1, Zone.Deck).Count}）");

            // 2026-11-13 起无每回合上限：同卡每回合可重复达标（TriggerLimitPerTurn 仅数据留档、不再有运行时闸）；
            // 旧宿主与「限一」卡同场时，各宿主每次达标各发一次。
            var cappedData = new CardData
            {
                ID = "VERIFY_ENGINE_SURGE_CAP", CardName = "验证充盈·限一", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            cappedData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_SURGE_CAP_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 引擎宿主惰性化（同上注）
                TriggerLimitPerTurn = 1, // 历史列留档（2026-11-13 起无运行时消费）
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.ManaSurplus, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var cappedCard = new CardWrapper(cappedData);
            cappedCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(cappedCard, p1), "元素充盈：限一宿主入场");
            int capBase = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            // ≥x 判别证据（门槛2宿主×2）：付后差 3 ≥2 → 两宿主各抽1（旧 ==x 裁量会零发）
            SurgeWallets(p1Red: 6, p2Red: 4); // 施放付 1 → remain2=3 → 差 3（≥ 门槛）
            Assert(SurgeCast(SurgeSpell(), "geq-proof"), "元素充盈：1费载体（付后差3）打出");
            Crumb("surge geq-proof: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == capBase - 2,
                   $"元素充盈：差 3 ≥ 门槛 2 → 两门槛2宿主各抽1（旧 ==x 恰等口径作废；实际抽 {capBase - core.ZoneManager.GetCards(p1, Zone.Deck).Count}）");
            // 差恰 2：两门槛2宿主每次达标都触发
            SurgeWallets(p1Red: 5, p2Red: 4); // 施放付 1 → remain2=3 → 差 = 5−3 = 2
            Assert(SurgeCast(SurgeSpell(), "eq-proof"), "元素充盈：1费载体（付后差恰2）打出");
            Crumb("surge eq-proof: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == capBase - 4,
                   $"元素充盈：差恰 2 两门槛2宿主各发 1（累计 −4；实际 −{capBase - core.ZoneManager.GetCards(p1, Zone.Deck).Count}）");
            SurgeWallets(p1Red: 5, p2Red: 4); // 复位（否则上一载体的付款把差滑到 3——同差证据须钉回恰 2）
            Assert(SurgeCast(SurgeSpell(), "cap2"), "元素充盈：限一段载体③（付后差仍2）打出");
            Crumb("surge cap2: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == capBase - 6,
                   $"元素充盈：同差重复评估——两宿主再次各发 1（无每回合上限，同差可重复触发；实际 −{capBase - core.ZoneManager.GetCards(p1, Zone.Deck).Count}）");

            // 双方触发面（2026-11-14 ≥x）：非出牌支付不触发 + 己方回合自触发——评估不再分施放者侧
            EnsureMainPhase(core, p1);
            SurgeWallets(p1Red: 6, p2Red: 6);
            surgeDeck = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            // 非出牌支付（直接 PayCost）不触发：直付 p2 两红 → 差恰 2、宿主预算全新——
            // 零抽=直付无评估的强证据（评估只在施放结算口）
            core.ElementPool.TryPayCostWithAutoTap(CostOf((ManaType.Red, 2f)), p2);
            Crumb("surge manualpay: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck,
                   "元素充盈：非出牌支付（直接扣款→差恰2）不触发——评估只在施放结算口");
            // 己方回合自触发（双方评估）：1 费载体把付后差钉在 2，p1 自己施放结算后 p1 引擎开火
            SurgeWallets(p1Red: 7, p2Red: 4); // 施放付 1 → remain1=6 → 差 = 6−4 = 2（清直付残留）
            var ownCard = InjectCard(core, p1, SurgeSpell());
            Assert(GameActions.PlayCard(core, p1, ownCard, null, Zone.Hand, 0, out var ownWhy),
                   $"元素充盈：己方1费载体打出（拒因={ownWhy}）");
            GameActions.DrainStack(core);
            Crumb("surge own-turn: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 2,
                   $"元素充盈：己方回合出牌（差2）也触发——双方评估，两门槛2宿主各抽1（实际抽 {surgeDeck - core.ZoneManager.GetCards(p1, Zone.Deck).Count}）");

            // 封顶 6 上界 + 差 1..6 阶梯（2026-11-14 ≥x）：门槛 6 宿主（Then=3×DrawCard1 锚价 6），
            // p2 剩余 6→0、差 1..6：门槛2双宿主在差 ≥2 的每一档各抽 1；门槛6宿主在差 6 再抽 3；
            // 合计 = 差2..6 五档 ×2 + 差6 的 3 = 13。
            var surgeData2 = new CardData
            {
                ID = "VERIFY_ENGINE_SURGE2", CardName = "验证充盈封顶", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            surgeData2.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_SURGE2_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 引擎宿主惰性化（同上）
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.ManaSurplus, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1),
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1),
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var surgeCard2 = new CardWrapper(surgeData2);
            surgeCard2.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(surgeCard2, p1), "元素充盈：封顶宿主入场");
            EnsureMainPhase(core, p2); // 回到对手回合（回合结束清钱包——重灌）
            SurgeWallets(p1Red: 6, p2Red: 6);
            // 前面段把牌库抽得很薄：本段要一次兑现 −13 张，先补 20 张探针卡，避免「牌库抽空」截断读数
            for (int i = 0; i < 20; i++)
            {
                var fillData = new CardData
                {
                    ID = "VERIFY_SURGE_FILL" + i, CardName = "充盈填充" + i,
                    Supertype = Cardtype.Creature, Power = 1, Life = 1,
                };
                var fill = new CardWrapper(fillData);
                fill.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(fill, Zone.Deck, DeckPosition.Top);
            }
            surgeDeck = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            for (int k = 1; k <= 6; k++) Assert(SurgeCast(SurgeSpell(), $"cap{k}"), $"元素充盈：对手六连载体{k}（差{k}）打出");
            Crumb("surge ladder-done: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 13,
                   $"元素充盈：差 1..6 六档阶梯——差≥2 各档两门槛2宿主各抽1（5档×2）＋差6 门槛6宿主抽3＝−13（实际 −{surgeDeck - core.ZoneManager.GetCards(p1, Zone.Deck).Count}）");

            core.ZoneManager.MoveCard(surgeCard, p1, Zone.Battlefield, Zone.Graveyard); // 门槛2宿主退场防连带
            core.ZoneManager.MoveCard(surgeCard2, p1, Zone.Battlefield, Zone.Graveyard);
            SurgeWallets(0, 0); // 段界卫生：钱包/地牌不留段
            surgePool.PooledCards.Clear();
            surgePool2.PooledCards.Clear();

            // ---- 7. 局面状态门（评估器直测 + 口径断言；有限分支 2026-10-09 还原）----
            // 回合层清零（shield 跳过自动化防抽牌副作用；事件订阅照常分发清 _turn）
            var stateShield = new TurnStartAutomationShield();
            RuleHooks.RegisterTurnStartInterceptor(stateShield);
            try
            {
                EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 700 });
                GameActions.DrainStack(core);
            }
            finally { RuleHooks.UnregisterTurnStartInterceptor(stateShield); }

            var sctx = new EffectExecutionContext
            { Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool };
            bool EvalGate(string id) => CardCore.BranchConditionEvaluator.Evaluate(id, new CardCore.EffectOutcome(), 0, null, sctx);

            int lifeKeep1 = p1.Life, lifeKeep2 = p2.Life;
            p1.Life = 10; p2.Life = 20;
            Assert(EvalGate("LifeBelowOpp") && !EvalGate("LifeAboveOpp"), "状态门：生命 10<20 → 低于真/高于假");
            p1.Life = 25;
            Assert(EvalGate("LifeAboveOpp") && !EvalGate("LifeBelowOpp"), "状态门：生命 25>20 → 高于真");
            p1.Life = 20;
            Assert(!EvalGate("LifeAboveOpp") && !EvalGate("LifeBelowOpp"), "状态门：生命相等 → 双假（严格比较）");
            p1.Life = lifeKeep1; p2.Life = lifeKeep2;

            int deckP1 = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            int deckP2 = core.ZoneManager.GetCards(p2, Zone.Deck).Count;
            Assert(EvalGate("DeckBelowOpp") == (deckP1 < deckP2) && EvalGate("DeckAboveOpp") == (deckP1 > deckP2),
                   $"状态门：卡组剩余对比与实际一致（{deckP1} vs {deckP2}）");

            int crP1 = core.ZoneManager.GetCards(p1, Zone.Battlefield)
                .Count(c => c is CardCore.IHasSupertype st && st.Supertype == Cardtype.Creature);
            int crP2 = core.ZoneManager.GetCards(p2, Zone.Battlefield)
                .Count(c => c is CardCore.IHasSupertype st && st.Supertype == Cardtype.Creature);
            Assert(EvalGate("CreaturesBelowOpp") == (crP1 < crP2) && EvalGate("CreaturesAboveOpp") == (crP1 > crP2),
                   $"状态门：场上生物对比与实际一致（{crP1} vs {crP2}）");

            Assert(!EvalGate("FirstCardThisTurn"), "状态门：回合开始未出牌 → 非首张");
            EventManager.Instance.Publish(new CardPlayEvent { Player = p1 });
            Assert(EvalGate("FirstCardThisTurn"), "状态门：本回合首张出牌宣言 → 真（CardsPlayed ThisTurn==1）");
            EventManager.Instance.Publish(new CardPlayEvent { Player = p1 });
            Assert(!EvalGate("FirstCardThisTurn"), "状态门：第二张出牌后 → 假");

            // 前面段可能已把牌库抽空：补两张探针卡（本测试只验抽牌事件登记；禁抽令按回合计，若命中则下方前置断言报出）
            for (int i = 0; i < 2; i++)
            {
                var probeData = new CardData
                {
                    ID = "VERIFY_DRAW_PROBE" + i, CardName = "抽牌探针" + i,
                    Supertype = Cardtype.Creature, Power = 1, Life = 1,
                };
                var probeCard = new CardWrapper(probeData);
                probeCard.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(probeCard, Zone.Deck, DeckPosition.Top);
            }
            var standbyDrawn = ZoneManagerExtensions.DrawCard(core.ZoneManager, p1, firstDrawOfTurn: true);
            var effectDrawn = ZoneManagerExtensions.DrawCard(core.ZoneManager, p1, firstDrawOfTurn: false);
            Assert(standbyDrawn != null && effectDrawn != null,
                   "状态门前置：牌库可抽（补探针卡后自然抽/效果抽各 1 张）");
            var dctxStandby = new EffectExecutionContext
            { Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, CastCard = standbyDrawn };
            var dctxEffect = new EffectExecutionContext
            { Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, CastCard = effectDrawn };
            Assert(CardCore.BranchConditionEvaluator.Evaluate("DrawnInStandbyThisTurn", new CardCore.EffectOutcome(), 0, null, dctxStandby),
                   "状态门：本回合第一张抽到的卡（FirstDrawOfTurn 自然抽）→ 真");
            Assert(!CardCore.BranchConditionEvaluator.Evaluate("DrawnInStandbyThisTurn", new CardCore.EffectOutcome(), 0, null, dctxEffect),
                   "状态门：效果抽牌（非准备阶段）→ 假");

            // ---- 7b. 局面状态门·二批（2026-09-22 追加，预算 2：操控地≥7 / 手牌=0 / 生命≤7）----
            // 生命≤7（7 真 / 8 假，恢复原值）
            int lifeKeepB = p1.Life;
            p1.Life = 7;
            Assert(EvalGate("LifeLe7"), "状态门二批：生命 7 ≤ 7 → 真");
            p1.Life = 8;
            Assert(!EvalGate("LifeLe7"), "状态门二批：生命 8 > 7 → 假");
            p1.Life = lifeKeepB;

            // 手牌=0：临时清手（手→牌库）→ 真 → 还原
            var handSnapshot = core.ZoneManager.GetCards(p1, Zone.Hand).ToList();
            Assert(EvalGate("HandEmpty") == (handSnapshot.Count == 0), "状态门二批：手牌判与实际张数一致（清空前）");
            foreach (var hc in handSnapshot)
                core.ZoneManager.MoveCard(hc, p1, Zone.Hand, Zone.Deck);
            Assert(EvalGate("HandEmpty"), "状态门二批：手牌清空 → 真");
            foreach (var hc in handSnapshot)
                core.ZoneManager.MoveCard(hc, p1, Zone.Deck, Zone.Hand);

            // 操控地≥7：红1 费生物卡垫地到 7 张（surge 段已置 GlobalTurnIndex=9 → 上限 9），结束回收
            var addedLands = new List<Card>();
            Assert(EvalGate("LandsGe7") == (core.ElementPool.GetPooledCards(p1).Count >= 7),
                   "状态门二批：地数判与实际张数一致（垫前）");
            while (core.ElementPool.GetPooledCards(p1).Count < 7)
            {
                var landData = new CardData
                {
                    ID = "VERIFY_GATE_LAND_" + addedLands.Count, CardName = "状态门垫地",
                    Supertype = Cardtype.Creature, Power = 0, Life = 1,
                };
                // 声明费退役（2026-11-05）：锁色读内容推导——挂红锚原子（DealDamage=红1）保入池锁色
                landData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_GATE_LAND_FX",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
                });
                var land = CardLoader.BuildDeck(new List<CardData> { landData }, 1)[0];
                land.SetController(p1);
                if (!core.ElementPool.AddCardToPool(land, p1)) break; // 上限兜底（不应发生：cap=9）
                addedLands.Add(land);
            }
            Assert(EvalGate("LandsGe7"), $"状态门二批：操控地 {core.ElementPool.GetPooledCards(p1).Count} ≥ 7 → 真");
            foreach (var land in addedLands)
            {
                core.ElementPool.RemoveCardFromPool(land, p1);
                // 裸 AddCardToPool 的容器账（zone 字段未写——MoveCard 不可用，容器直删；
                // 残留条目会毒化后续牺牲段的池候选序）
                core.ZoneManager.GetZoneContainer(p1).Remove(land, Zone.ElementPool);
            }

            // ---- 8. 拦截式改写门断言已删（2026-10-05：DmgRewrite* 四门随四条改写迁唯一光环退役——
            //      差价计价 / converter 配对守卫 / 效果伤害拦截三路同批废除，无头侧锚见 V9.p；
            //      战斗伤害改写族=关键词侧（毒刺/冰晶等），另测未动）----

            // ---- 9. 团结（2026-10-20 效果序位换轴·空间轴）：与此卡相邻的单位数（生物/结界占格、角色格、
            //      非空地牌格各算一；施放占位卡不计）≥ x 时执行奖励——评估口=OnCardCastSettled（己方出牌
            //      结算后，充盈同拍），每次达标都触发；只观察己方场上引擎卡（手牌无格）；x∈[1,6] 手填；
            //      邻接计数经 UnityAdjacentCountOf 注入（编辑器无棋盘——注假计数） ----
            EnsureMainPhase(core, p1);
            PadDeck(core, p1, 6, "VERIFY_UNI_FILL_"); // 奖励观测口径=抽牌数（引擎奖励抽 2，垫足 6）
            CardData UnityCard(string id, int x)
            {
                var d = new CardData
                {
                    ID = id, CardName = "验证团结x" + x,
                    Supertype = Cardtype.Creature, Power = 2, Life = 2,
                };
                // 主干=引擎行（EngineUnity，条件载体非效果——执行按 Settle==Engine 跳过）
                // + x 手填（=所需相邻单位数）+ Then=抽 1
                d.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)CardCore.TriggerTiming.OnCardPlayed,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        EngineAtom(CardCore.AtomicEffectType.EngineUnity, 1, CardCore.BranchEngineKind.Unity, x,
                            AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                    },
                });
                return d;
            }
            var uniDef = CardEffectConverter.ConvertOne(UnityCard("VERIFY_UNI_A", 2).Effects[0], "VERIFY_UNI_A");
            Assert(uniDef != null && uniDef.Effects.Count == 1 && uniDef.Effects[0].Branch != null
                   && uniDef.Effects[0].Branch.Settle == CardCore.BranchSettleKind.Engine
                   && uniDef.Effects[0].Branch.EngineKind == CardCore.BranchEngineKind.Unity
                   && uniDef.Effects[0].Branch.Then.Count == 1
                   && uniDef.TriggerTiming == CardCore.TriggerTiming.OnCardPlayed
                   && uniDef.ActivationType == CardCore.EffectActivationType.Automatic
                   && uniDef.TriggerLimitPerTurn == -1,
                   "团结：槽级载荷转存 Then + 引擎头钉死 使用卡牌时+自动+无限（2026-10-20 效果序位换轴口径）");
            Assert((int)CardCore.CostDerivationService.DeriveElementCosts(uniDef)[CardCore.ManaType.Gray] == 0,
                   "团结计价：引擎零计价（x=所需相邻单位数判定条件非收费；Then 奖励不并入卡费）");

            Card UnityToZone(CardData data, Zone zone)
            {
                var card = new CardWrapper(data);
                card.SetController(p1);
                card.IsAlive = true;
                core.ZoneManager.GetZoneContainer(p1).Add(card, zone);
                return card;
            }
            // 隔离：清 p1 战场残留（前段夹具可能留有充盈等引擎卡——同口评估会串扰抽牌计数）
            foreach (var uniStale in core.ZoneManager.GetCards(p1, Zone.Battlefield).ToList())
                if (uniStale != null) core.ZoneManager.GetZoneContainer(p1).Remove(uniStale, Zone.Battlefield);
            var uniA = UnityToZone(UnityCard("VERIFY_UNI_A", 2), Zone.Battlefield); // x=2
            var uniB = UnityToZone(UnityCard("VERIFY_UNI_B", 3), Zone.Battlefield); // x=3
            var uniC = UnityToZone(UnityCard("VERIFY_UNI_C", 1), Zone.Hand);        // 手牌载体：无格不观察
            int uniDeck0 = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            try
            {
                // 2026-11-08 阶段3：团结评估已自 OnCardCastSettled 直调迁 OnRoutedEvent（时机=奖励效果
                // 时机——本夹具声明 OnCardPlayed → 驱动口=发布 CardPlayEvent·归属=施放者）。
                CardCore.CardPlayEvent UnityPump() => new CardCore.CardPlayEvent { Player = p1 };

                // 未接线（编辑器默认无棋盘）=0 恒不触发
                CardCore.BranchEngines.UnityAdjacentCountOf = null;
                CardCore.BranchEngines.OnRoutedEvent(UnityPump());
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == uniDeck0,
                       "团结：邻接计数未接线 = 0 恒不触发（无头/编辑器默认）");

                // 邻接不足（1 < 2/3）不触发；手牌载体(x=1)不受 1 达标——观察面=仅场上
                CardCore.BranchEngines.UnityAdjacentCountOf = c => 1;
                CardCore.BranchEngines.OnRoutedEvent(UnityPump());
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == uniDeck0,
                       "团结：相邻单位数 1 < x=2/3 不触发（手牌载体无格不观察）");

                // 达标 2：A(x=2) 触发抽 1（B=3 未达）
                CardCore.BranchEngines.UnityAdjacentCountOf = c => 2;
                CardCore.BranchEngines.OnRoutedEvent(UnityPump());
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == uniDeck0 - 1,
                       "团结：相邻单位数 2 ≥ x=2 → A 触发抽 1（B(x=3) 未达不触发）");
            }
            finally { CardCore.BranchEngines.UnityAdjacentCountOf = null; } // 静态委托防跨段泄漏
            core.ZoneManager.MoveCard(uniA, p1, Zone.Battlefield, Zone.Graveyard); // 清场防跨段连带
            core.ZoneManager.MoveCard(uniB, p1, Zone.Battlefield, Zone.Graveyard);
            core.ZoneManager.MoveCard(uniC, p1, Zone.Hand, Zone.Graveyard);
        }

        /// <summary>
        /// 测试拦截器：合成 TurnStartEvent 期间跳过 GameCore 回合自动化（回合抽牌/地牌曲线/重置）——
        /// 分支引擎段断言只测量引擎奖励自身的抽牌，隔离环境副作用（2026-09-13 拼点口径修复配套）。
        /// </summary>
        private sealed class TurnStartAutomationShield : ITurnStartInterceptor
        {
            public bool ShouldSkipTurnStartAutomation(Player player) => true;
        }

        // ======================================== 档位收缩定案（2026-09-13；2026-09-16 指示物统一档追改） ========================================

        /// <summary>
        /// ①冻结（2026-11-02 回退指示物版·表行 973884b1，费用 1→2 蓝）：横置+挂冻结指示物——
        /// 层=持续回合，持有期间无法重置，持有者回合末 −1 归零解除；
        /// ②效果层赋予关键词=Temp 轨换区清除（2026-11-09 定案，取代 2026-09-13「固定 Temp 1 回合
        /// 回合末清」——授予跨回合保留，离场换区才清；魔法 Setting/光环照旧）；
        /// ③计价梯三族（2026-10-09 清理：Duration 档计价随三轨统一退役，断言已删——价梯以 CounterSpec 为准）。
        /// </summary>
        private static void TestTierConsolidation(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 冻结（2026-10-08 层即持续定案：层=持续回合，持有者回合末 −1、叠加=延长） ----
            var frozenUnit = SpawnTier(core, p1, 3, 3);
            var freezeAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Freeze, Value = 0 };
            var fctx = new EffectExecutionContext
            {
                Source = p2, Controller = p2, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { frozenUnit },
            };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(freezeAtom, fctx); // 默认 1 层
            Assert(frozenUnit.IsTapped() && frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 1,
                   "冻结：默认 1 层");
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(freezeAtom, fctx); // 再施加 = 层数累计（延长）
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 2,
                   "冻结叠加：层数累计（层即持续——2 层=2 回合）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager); // 施加方回合末：不碰持有者侧
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 2 && frozenUnit.IsTapped(),
                   "冻结：施加方回合末不倒数（持有者侧结算域，仍冻结仍横置）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager); // 持有者回合末①：−1
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 1 && frozenUnit.IsTapped(),
                   "冻结倒数：持有者回合末 −1（2→1，期间无法重置）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager); // 持有者回合末②：归零解除
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 0,
                   "冻结解除：层归零（下回合可重置）");
            RetireCards(core, p1, frozenUnit);

            // ---- 2. 关键词赋予通道（2026-11-09 换区清除定案，取代 2026-09-13「固定 Temp 1 回合」）：
            //      他人/自己=Temp 轨——跨回合保留，换区时经 KeywordRules.ClearZoneKeywords 清除；
            //      Printed（卡牌组合界面直加的本体关键词）才是永久/免疫，Setting 文本轨不再由效果层写入 ----
            var granter = SpawnTier(core, p1, 2, 2);
            var receiver = SpawnTier(core, p1, 2, 2);
            var grantCtx = new EffectExecutionContext
            {
                Source = granter, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { receiver },
                Duration = DurationType.Permanent, // 故意声明永久——效果层赋予应被覆写为 Temp
            };
            var grantAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.GrantTaunt, Value = 1 };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(grantAtom, grantCtx);
            // 同一来源自赋予（目标=自己）——同样走赋予通道，换区清
            var selfGrantCtx = new EffectExecutionContext
            {
                Source = granter, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { granter },
                Duration = DurationType.Permanent,
            };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                new CardCore.AtomicEffectInstance { Type = AtomicEffectType.GrantVigilance, Value = 1 }, selfGrantCtx);
            Assert(receiver.HasKeyword(CardCore.Attribute.KeywordRules.Taunt), "生物赋予：关键词生效");
            Assert(granter.HasKeyword(CardCore.Attribute.KeywordRules.Vigilance), "自赋予：关键词生效");
            EventManager.Instance.Publish(new TurnEndEvent { TurnPlayer = p1, TurnNumber = 700 });
            Assert(receiver.HasKeyword(CardCore.Attribute.KeywordRules.Taunt)
                   && granter.HasKeyword(CardCore.Attribute.KeywordRules.Vigilance),
                   "赋予跨回合保留：回合末不清（2026-11-09 换区清除定案——声明永久被覆写为 Temp）");
            core.ZoneManager.MoveCard(receiver, p1, Zone.Battlefield, Zone.Graveyard);
            Assert(!receiver.HasKeyword(CardCore.Attribute.KeywordRules.Taunt),
                   "赋予他人（Temp 轨）：换区清除——离场即清，非回合末到期");
            core.ZoneManager.MoveCard(granter, p1, Zone.Battlefield, Zone.Graveyard);
            Assert(!granter.HasKeyword(CardCore.Attribute.KeywordRules.Vigilance),
                   "自赋予也走赋予通道（Temp 轨）：同样换区清除（印刷关键词才永久）");
            RetireCards(core, p1, granter, receiver);

            // 2026-10-09 清理：计价梯三族（控制权 1.2/1.6/3.0、Grant 四档、费用修改两档）断言删除——
            // Duration 档计价已随 2026-10-05 三轨统一 + 2026-10-08 指示物化退役（价梯以 CounterSpec 为准）

            // ---- 改写持有者归属路由端到端：偷取→死亡/弹回均归新主 ----
            var stealTgt = SpawnTier(core, p2, 2, 3);
            var stealAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.ChangeOwner, Value = 1 };
            var stealCtx = new EffectExecutionContext
            {
                Source = p1, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { stealTgt },
            };
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(stealAtom, stealCtx), "改写持有者原子执行");
            Assert(stealTgt.GetController() == p1 && stealTgt.GetOwner() == p1, "改写持有者：控制权与 owner 均换到新主");
            CardCore.Attribute.DeathRules.TryKill(stealTgt, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager);
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(stealTgt)
                   && !core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(stealTgt),
                   "归属路由：改写后死亡进**新主**墓地");

            var stealTgt2 = SpawnTier(core, p2, 2, 3);
            stealCtx.Targets = new List<Entity> { stealTgt2 };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(stealAtom, stealCtx);
            var bounceAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.ReturnToHand, Value = 1 };
            var bounceCtx = new EffectExecutionContext
            {
                Source = p1, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { stealTgt2 },
            };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(bounceAtom, bounceCtx);
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Contains(stealTgt2),
                   "归属路由：改写后弹回进**新主**手牌");

            // 对照：临时控制（非改写）死亡回原主墓地
            var tempTgt = SpawnTier(core, p2, 2, 3);
            var tempAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.GainControl, Value = 1 };
            var tempCtx = new EffectExecutionContext
            {
                Source = p1, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { tempTgt },
                Duration = DurationType.UntilEndOfTurn,
            };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(tempAtom, tempCtx);
            CardCore.Attribute.DeathRules.TryKill(tempTgt, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager);
            Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(tempTgt),
                   "归属对照：临时控制（未改写 owner）死亡回**原主**墓地");

            // 2026-10-09 清理：Grant 四档/费用修改两档（Duration 档计价）随上段一并删除——
            // 关键词统一计价=行锚×max(1,层数)（2026-10-08 关键词不叠加定案），不再按持续档分梯
        }

        private static Card SpawnTier(GameCore core, Player owner, int power, int life)
        {
            var data = new CardData
            {
                ID = "VERIFY_TIER_" + System.Guid.NewGuid().ToString("N").Substring(0, 6),
                CardName = "档位段", Supertype = Cardtype.Creature, Power = power, Life = life,
            };
            var card = new CardWrapper(data);
            card.SetController(owner);
            card.SetOwner(owner); // 2026-09-13 修复：合成卡未设 owner → GetOwner() 为 null，
                                  // 临时控制后死亡按 controller 落墓（归属对照断言失真）；补 owner=归属者
            Assert(core.ZoneManager.TryAddToBattlefield(card, owner), "档位段合成随从入场");
            card.Untap();
            return card;
        }

    }
}
