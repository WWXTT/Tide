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
        // ======================================== 逐原子目标制（2026-10-09 定案） ========================================

        /// <summary>
        /// 逐原子目标制端到端（2026-10-09 定案：并列组合无效果级作用范围声明 → 各原子按自身
        /// 极性过滤域独立解析，弹窗全部落结算期连续等待，数量档/随机档各原子各自生效；
        /// 单条效果作用域并集上限 4）：
        /// ①判别与域：无 header=PerAtomTargets+并集域；header 声明=共享口径不回归；
        /// ②极性过滤助手锚（TargetKindRules.PolarityFilter）；
        /// ③行为端到端：域不交的 DealDamage（对方侧）+永久属性增加（己方侧）同卡并列——
        ///   旧交集口径下不可构筑的组合，现各自独立解析结算（无头连弹自动代选不挂起）；
        /// ④无域原子（DrawCard）与有域原子并列自结算共存；
        /// ⑤强制类无窗口=逐原子自动全取；
        /// ⑥并集 >4 装载兜底拦截（合成器加入门的 backstop）。
        /// </summary>
        private static void TestPerAtomTargeting(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 判别与域 ----
            var dmgEntry = AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 2);          // p=-1 域{1,2}→{2}
            var plusEntry = AtomRefs.New(CardCore.AtomicEffectType.AddPermanentPowerUp, value: 1); // p=+1 域{1,2}→{1}
            var perAtomDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_PA",
                AtomicEffects = new List<AtomicEffectEntry> { dmgEntry, plusEntry },
            }, "VERIFY_PA");
            Assert(perAtomDef.PerAtomTargets
                   && TargetKindRules.Format(perAtomDef.TargetDomain) == "1,2,3,4",
                $"无 header=逐原子模式+并集域（B2 口径：并集=行 Domain 派生视图——伤害行 2026-11-05 扩含结界→位1..4、属性行生物位 1；"
                + $"实际 flag={perAtomDef.PerAtomTargets} 域=[{TargetKindRules.Format(perAtomDef.TargetDomain)}]）");
            var sharedDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_PA_SHARED",
                TargetKinds = new List<int> { 2 }, // 旧数据：效果级作用范围声明（共享单选）
                AtomicEffects = new List<AtomicEffectEntry> { dmgEntry },
            }, "VERIFY_PA_SHARED");
            Assert(!sharedDef.PerAtomTargets && TargetKindRules.Format(sharedDef.TargetDomain) == "2",
                "header 声明=共享口径不回归（PerAtomTargets=false、域=声明值）");

            // ---- 2. Union 助手（逐原子组合域=并集来源；运行时不做极性过滤——双域原子可选两侧、
            //     跨侧命中走错边黑白发放经济，与旧共享口径一致；极性收窄是合成器 UI 的选择集口径） ----
            Assert(TargetKindRules.Format(TargetKindRules.Union(new List<int> { 2, 1 }, new List<int> { 3, 2 })) == "1,2,3"
                   && TargetKindRules.Format(TargetKindRules.Union(null, new List<int> { 5 })) == "5",
                "Union：并集升序去重、null 成员不参与");

            // ---- 3/4. 行为端到端：域不交双原子并列 + 无域原子共存（无头连弹自动代选） ----
            int seq = 0;
            Card Spawn(Player owner, int power, int life)
            {
                var data = new CardData
                {
                    ID = "VERIFY_PA_" + (++seq), CardName = "PA" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"逐原子段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            var saveP1Field = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Battlefield));
            var saveP2Field = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Battlefield));
            foreach (var c in saveP1Field) core.ZoneManager.MoveCard(c, p1, Zone.Battlefield, Zone.Graveyard);
            foreach (var c in saveP2Field) core.ZoneManager.MoveCard(c, p2, Zone.Battlefield, Zone.Graveyard);
            var pool1 = core.ElementPool.GetPool(p1);
            var snap = new Dictionary<ManaType, int>(pool1.TempElements);
            try
            {
                FillSixColors(pool1, 99);
                var a = Spawn(p1, 2, 5);
                var b = Spawn(p2, 2, 5);
                int p1Deck = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                Assert(p1Deck > 0, "逐原子段前提：牌库非空（DrawCard 自结算观察点）");

                var paCardData = new CardData
                {
                    ID = "VERIFY_PA_CR", CardName = "逐原子验证", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Gray, 1f)),
                };
                paCardData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_PA_ONPLAY",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 显式域收窄（逐原子卡的标准形态：每原子声明自己的作用范围）——对方单位 {2}/己方单位 {1}
                        AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 2, kinds: new List<int> { 2 }),
                        AtomRefs.New(CardCore.AtomicEffectType.AddPermanentPowerUp, value: 1, kinds: new List<int> { 1 }),
                    },
                });
                var paCard = InjectCard(core, p1, paCardData);
                Assert(GameActions.PlayCard(core, p1, paCard), "打出域不交逐原子组合（旧交集口径不可构筑）");
                GameActions.DrainStack(core);
                Assert(b.GetLife() == 3 && a.GetPower() == 3,
                    $"逐原子独立解析：DealDamage 落对方生物（b 生 {b.GetLife()}=3）、"
                    + $"永久+1 落己方生物（a 攻 {a.GetPower()}=3）——两原子域不交各自结算");

                var paDrawData = new CardData
                {
                    ID = "VERIFY_PA_DRAW", CardName = "逐原子抽卡", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Gray, 1f)),
                };
                paDrawData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_PA_DRAW_ONPLAY",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1),             // 隐藏区（牌库）自结算
                        AtomRefs.New(CardCore.AtomicEffectType.AddPermanentPowerUp, value: 1, kinds: new List<int> { 1 }),
                    },
                });
                var paDrawCard = InjectCard(core, p1, paDrawData);
                Assert(GameActions.PlayCard(core, p1, paDrawCard), "打出无域+有域并列组合");
                GameActions.DrainStack(core);
                Assert(a.GetPower() == 4
                       && core.ZoneManager.GetCards(p1, Zone.Deck).Count == p1Deck - 1,
                    "无域原子自结算共存：DrawCard 抽 1（牌库 -1）、永久+1 照常落己方生物（a 攻 4）");

                // ---- 5. 强制类无窗口：逐原子自动全取（不弹选，全域命中）——
                //      直构 def（CardData.ActivationType=0 会被转换器当未声明回落默认桶，
                //      V9.o ④ 强制桶同款先例）；PerAtomTargets 手动置位（绕过 converter） ----
                var mandDef = new CardCore.EffectDefinition
                {
                    Id = "VERIFY_PA_MAND",
                    DisplayName = "强制逐原子",
                    ActivationType = CardCore.EffectActivationType.Mandatory,
                    TriggerTiming = TriggerTiming.OnPlay,
                    ElementCostPrepaid = true,
                    PerAtomTargets = true,
                    Effects = new List<CardCore.AtomicEffectInstance>
                    {
                        new CardCore.AtomicEffectInstance
                        {
                            Type = CardCore.AtomicEffectType.DealDamage, Value = 1,
                            TargetKinds = new List<int> { 2 }, Polarity = -1f,
                            RowHashId = CardCore.Attribute.AtomicEffectTable.GetByType(CardCore.AtomicEffectType.DealDamage)?.HashId,
                        },
                    },
                };
                int ubLife0 = b.GetLife(), p2Life0 = p2.Life;
                core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                {
                    Definition = mandDef,
                    Source = p1,
                    Controller = p1,
                    Targets = new List<Entity>(),
                }).GetAwaiter().GetResult();
                Assert(b.GetLife() == ubLife0 - 1 && p2.Life == p2Life0,
                    $"强制类自动全取：对方生物受 1（{ubLife0}→{b.GetLife()}）、角色不动（{p2Life0}→{p2.Life}）"
                    + "——不弹选全域命中；角色不参与全取（2026-11-06 定案）");
            }
            finally
            {
                foreach (var kv in snap) pool1.TempElements[kv.Key] = kv.Value;
                var kill = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Battlefield).Concat(
                    core.ZoneManager.GetCards(p2, Zone.Battlefield)));
                foreach (var c in kill) core.ZoneManager.MoveCard(c, c.GetController(), Zone.Battlefield, Zone.Graveyard);
                foreach (var c in saveP1Field) core.ZoneManager.MoveCard(c, p1, Zone.Graveyard, Zone.Battlefield);
                foreach (var c in saveP2Field) core.ZoneManager.MoveCard(c, p2, Zone.Graveyard, Zone.Battlefield);
            }
            // ---- 6. 并集 >4 装载兜底（合成器加入门的 backstop） ----
            var ddHash = CardCore.Attribute.AtomicEffectTable.GetByType(CardCore.AtomicEffectType.DealDamage)?.HashId;
            var prevSink = TideLog.Sink;
            var errs = new List<string>();
            TideLog.Sink = (level, msg) =>
            {
                // 负向探针的装载拦截 Error 只捕获不上控制台（断言读 errs；转发 prevSink 会染红 Console）
                if (level == TideLogLevel.Error) errs.Add(msg);
            };
            try
            {
                var overJson = "{\"cards\":[{\"id\":\"VERIFY_PA_OVER\",\"cardName\":\"并集超限\","
                             + "\"supertype\":\"Spell\",\"effects\":[{\"id\":\"E1\",\"triggerTiming\":19,\"atomicEffects\":[{\"refId\":\""
                             + ddHash + "\",\"value\":1,\"kinds\":[2,4,6,8,10]"
                             + "}]}]}]}";
                CardLoader.LoadCardsFromText(overJson); // p=-1 全对方侧 5 域 → 并集 5 > 4
                Assert(errs.Any(e => e.Contains("并集") && e.Contains("超上限")),
                    "并集 >4 装载兜底：TideLog Error 点名超上限（逐原子单条效果限 4 个作用范围）");

                errs.Clear();
                var okJson = "{\"cards\":[{\"id\":\"VERIFY_PA_OK\",\"cardName\":\"并集合规\","
                           + "\"supertype\":\"Spell\",\"effects\":[{\"id\":\"E1\",\"triggerTiming\":19,\"atomicEffects\":[{\"refId\":\""
                           + ddHash + "\",\"value\":1,\"kinds\":[2,4]"
                           + "}]}]}]}";
                CardLoader.LoadCardsFromText(okJson);
                Assert(!errs.Any(),
                    "并集 ≤4 合规装载：零 Error（主动时点 19 + 并集恰 4——无任何连带拒绝）");
            }
            finally
            {
                TideLog.Sink = prevSink;
            }
        }

        // ======================================== 沉睡（2026-09-11 改造） ========================================

        /// <summary>
        /// 沉睡端到端：①表行锚（2026-09-13 用户改表：绿2 Pol=-1 域={1,2} NoRole——通用赋予域）
        /// ②普通施放全价入场，手动横置+挂沉睡层建立「沉睡中」状态
        /// ③沉睡期间无法重置（回合开始扣层代替重置、扣完即醒）+ 效果无效（OnTurnEnd 触发被拦）
        /// ④通用授予（Value 层数直接给）。效果/指示物分离：持续规则全在指示物上（本原子只赋予）。
        /// </summary>
        private static void TestSleep(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 0. 表行锚（表为单一来源：颜色/价格钉断言已删——沉睡表价绿→黑随表改面漂移，不钉） ----
            var sleepCfg = CardCore.Attribute.AtomicEffectTable.GetAll()
                .FirstOrDefault(c => c.EnumName == "Sleep" && c.DisplayName == "沉睡");
            var sleepKinds = sleepCfg?.GetTargetKindList();
            Assert(sleepKinds != null && sleepKinds.Count == 2
                   && sleepKinds.Contains(0) == false && sleepKinds.Contains(1) && sleepKinds.Contains(2),
                   "沉睡域 = {1,2}（表授予域=双方单位——「存储态=自己」口径已随表改面退役，自我沉睡经实例收窄 kinds={0} 表达）");
            Assert((sleepCfg?.TargetFilter ?? "") == ""
                   && CardCore.TargetAxisRules.TryParseDomain(sleepCfg?.Domain, out var sleepAxis)
                   && sleepAxis.Zh == "双方战场:生物",
                   "沉睡域=双方战场:生物（B1 硬切·Domain 迁移；类别滤角色——NoRole 已随类别语义退役）");

            // ---- 0b. 域兜底断言（2026-10-31 沉睡只能以生物为目标定案）：结界=无生命建筑，词义上不可沉睡。
            // 目标域由表保证（沉睡行只列双方有生命单位 {1,2}+NoRole，无「无生命单位」{3,4}）——
            // 此处只做现场解析断言，不加代码特判：用沉睡表行 kinds+filter 解析候选，
            // 结界与角色不得在列、生物在列（正向对照防候选空转伪绿）。
            {
                var probeCreature = InjectCard(core, p1, new CardData
                {
                    ID = "VERIFY_SLEEP_PROBE_CR", CardName = "验证沉睡域生物", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                });
                var probeEnchant = InjectCard(core, p1, new CardData
                {
                    ID = "VERIFY_SLEEP_PROBE_ENCH", CardName = "验证沉睡域结界", Supertype = Cardtype.Enchantment,
                });
                core.ZoneManager.TryMoveToBattlefield(probeCreature, p1, Zone.Hand);
                core.ZoneManager.TryMoveToBattlefield(probeEnchant, p1, Zone.Hand);
                Assert(core.ZoneManager.IsCardInZone(probeCreature, p1, Zone.Battlefield)
                       && core.ZoneManager.IsCardInZone(probeEnchant, p1, Zone.Battlefield),
                       "沉睡域前置：对照生物与结界均已入场");
                var domainCtx = new EffectExecutionContext
                {
                    Controller = p1, Source = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                };
                var domain = Attribute.EffectHandlerRegistry.ResolveCandidates(sleepKinds, sleepCfg.TargetFilter, domainCtx,
                    CardCore.TargetAxisRules.TryParseDomain(sleepCfg?.Domain, out var sleepAxis2) ? sleepAxis2 : null);
                Assert(domain != null && domain.Contains(probeCreature), "沉睡域正向对照：生物在候选内");
                Assert(domain.All(e => !(e is Card c && c.IsNonLivingUnit())),
                       "沉睡域不含无生命单位（结界不可沉睡——2026-10-31 定案）");
                Assert(domain.All(e => !(e is Player)), "沉睡域不含角色（NoRole 现场解析）");
                core.ZoneManager.MoveCard(probeCreature, p1, Zone.Battlefield, Zone.Graveyard);
                core.ZoneManager.MoveCard(probeEnchant, p1, Zone.Battlefield, Zone.Graveyard);
            }

            var pool1 = core.ElementPool.GetPool(p1);
            var snapBank = new Dictionary<ManaType, int>(pool1.TempElements);

            try
            {
                FillSixColors(pool1, 99);

                // ---- 1. 沉睡夹具：普通施放全价入场，手动横置+挂沉睡层建立「沉睡中」状态 ----
                var sleepData = new CardData
                {
                    ID = "VERIFY_SLEEP_CR", CardName = "验证沉睡生物", Supertype = Cardtype.Creature, Power = 4, Life = 4,
                    Cost = CostOf((ManaType.Red, 2), (ManaType.Gray, 3)),
                };
                sleepData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SLEEP_TURNEND",
                    TriggerTiming = (int)TriggerTiming.OnTurnEnd,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 直伤=红色锚（推导色随内容走，Declared Cost 仅显示缓存——P2a 改表后不钉色形）
                        Atom(AtomicEffectType.DealDamage, value: 1, kinds: new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit }),
                        // 观测探针（2026-11-07 关键词特判后）：Grant 走 Temp，OnTurnEnd 发的关键词会在
                        // 同一回合末被清，不能作观测——改用攻击力增加指示物（Resident 持久），
                        // 锁实例域=自己；仅确认 OnTurnEnd 效果确实发动，不测关键词域/轨。
                        new AtomicEffectEntry { refId = AtomRefs.New(AtomicEffectType.AddPowerUp).refId, domain = "自己" }
                    },
                });
                var sleepCard = InjectCard(core, p1, sleepData);

                // p2 侧靶子单位：直伤锚原子（敌方单位域）的声明期目标域预检候选——
                // HasPlayableTargets 不过滤触发时点（OnTurnEnd 效果同样当场验域）
                var dummyCard = InjectCard(core, p2, new CardData
                {
                    ID = "VERIFY_SLEEP_DUMMY", CardName = "验证沉睡靶子", Supertype = Cardtype.Creature, Power = 2, Life = 4,
                });
                core.ZoneManager.TryMoveToBattlefield(dummyCard, p2, Zone.Hand);

                // 全价口径（恒推导不钉数值——断言对表价漂移免疫）
                var sleepFull = GameActions.GetCardCost(sleepCard);
                int redFull = (int)sleepFull[ManaType.Red];
                int grayFull = (int)sleepFull[ManaType.Gray], greenFull = (int)sleepFull[ManaType.Green];
                Assert(redFull > 0 && greenFull > 0,
                       $"前置：本卡推导含红绿彩色（红{redFull} 绿{greenFull} 灰{grayFull}）");

                // 普通施放：全价照扣（无免灰变体）
                int redBefore = pool1.TempElements[ManaType.Red];
                int grayBefore = pool1.TempElements[ManaType.Gray];
                int greenBefore = pool1.TempElements[ManaType.Green];
                Assert(GameActions.PlayCard(core, p1, sleepCard), "普通施放沉睡生物（全价）");
                GameActions.DrainStack(core);

                Assert(pool1.TempElements[ManaType.Red] == redBefore - redFull
                       && pool1.TempElements[ManaType.Gray] == grayBefore - grayFull
                       && pool1.TempElements[ManaType.Green] == greenBefore - greenFull,
                       $"全价照扣：红{redBefore - pool1.TempElements[ManaType.Red]}/{redFull}，"
                       + $"灰{grayBefore - pool1.TempElements[ManaType.Gray]}/{grayFull}，"
                       + $"绿{greenBefore - pool1.TempElements[ManaType.Green]}/{greenFull}");
                Assert(core.ZoneManager.IsCardInZone(sleepCard, p1, Zone.Battlefield), "已入场");

                // 手动建立沉睡态：横置+2 层（供②③段周期断言消费）
                const int totalLayers = 2;
                sleepCard.Tap();
                sleepCard.AddCounters(Attribute.KeywordRules.SleepCounter, totalLayers, p1);
                Assert(sleepCard.IsTapped() && sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == totalLayers,
                       $"沉睡态建立：横置+沉睡指示物{totalLayers}层");

                // ---- 2. 周期推进：沉睡期间无法重置 + OnTurnEnd 被拦；totalLayers 次己方回合结束后倒数归零 ----
                // 周期 = p1回合结束(③块倒数+触发口被拦) → p2空过 → p1回合开始(BlocksUntap 判定) → 回主阶段
                // （2026-10-08 层即持续定案：倒数从回合开始挪回合末——归零后下回合开始自然重置）
                for (int cycle = 1; cycle <= totalLayers; cycle++)
                {
                    int countersBefore = sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter);
                    EndTurnPumped(core, p1);                 // p1 回合结束：③块倒数 −1 层；沉睡中 → OnTurnEnd 被拦
                    int tickedTo = sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter);
                    Assert(tickedTo == countersBefore - 1,
                           $"周期{cycle}：回合末倒数一层（{countersBefore}→{tickedTo}）");
                    GameActions.SkipElementPool(core, p2);
                    EndTurnPumped(core, p2);                 // p2 空过 → p1 回合开始
                    GameActions.SkipElementPool(core, p1);

                    if (tickedTo == 0)
                        Assert(!sleepCard.IsTapped(), "沉睡解除：层归零后下回合开始自然重置");
                    else
                        Assert(sleepCard.IsTapped(), $"周期{cycle}：沉睡期间无法重置（仍横置）");
                    Assert(sleepCard.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0,
                           $"周期{cycle}：沉睡期间 OnTurnEnd 效果无效（未获攻击力+1指示物）");
                }
                Assert(!sleepCard.IsTapped() && sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == 0, "倒数归零后沉睡完全解除");

            // ---- 3. 沉睡解除后 OnTurnEnd 恢复 ----
            Crumb($"awake pre: sleep={sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)} tapped={sleepCard.IsTapped()}");
            EndTurnPumped(core, p1);
            FillSixColors(pool1, 99); // 回合结束清空钱包后再灌——
            // 触发式效果执行走执行器计费口，欠费会静默跳过（排干前灌足保证金可付）
            GameActions.DrainStack(core); // OnTurnEnd 触发式入栈后显式排干（同打出路径 PlayCardSync 先例）
            GameActions.SkipElementPool(core, p2);
            EndTurnPumped(core, p2);
            GameActions.SkipElementPool(core, p1);
            Assert(sleepCard.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 1, "沉睡解除后 OnTurnEnd 效果恢复生效（获攻击力+1指示物——AddPowerUp 实例域=自己探针）");

                // ---- 4. 显式 Value 沉睡：灰费**不**豁免；层数恒 1（2026-10-31 指示物定案：数量参数退役） ----
                var fixedData = new CardData
                {
                    ID = "VERIFY_SLEEP_FIXED", CardName = "验证定长沉睡", Supertype = Cardtype.Creature, Power = 3, Life = 3,
                    Cost = CostOf((ManaType.Gray, 2), (ManaType.Green, 1)),
                };
                fixedData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SLEEP_FIXED_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.Sleep, value: 2, kinds: new List<int> { 0 }), // 收窄域={Self} 定长 2
                    },
                });
                var fixedCard = InjectCard(core, p1, fixedData);
                FillSixColors(pool1, 99); // 上一周期的回合结束已清空钱包——重灌
                // 全价动态读数（P2a 改表后推导色形漂移，不钉「2 灰」数值——只钉「灰不豁免」语义）
                int grayExpect4 = (int)GameActions.GetCardCost(fixedCard)[ManaType.Gray];
                // 2026-11-05 删除「定长卡推导含灰费」断言：卡费恒推导，端到端不替代码验算推导公式
                int grayBefore4 = pool1.TempElements[ManaType.Gray];
                // B2 后行 Domain（双方战场:生物）接管目标域，生物 OnPlay 不吃 cast 预选（永久物
                // 入场走触发路径）——醒转的沉睡生物先行送墓，场上唯一候选=自身，自动代选不漂移
                core.ZoneManager.MoveCard(sleepCard, p1, Zone.Battlefield, Zone.Graveyard);
                Assert(GameActions.PlayCard(core, p1, fixedCard), "打出定长沉睡生物");
                GameActions.DrainStack(core);
                Assert(pool1.TempElements[ManaType.Gray] == grayBefore4 - grayExpect4,
                       $"定长沉睡不走灰豁免：照扣推导灰{grayExpect4}（实际余 {pool1.TempElements[ManaType.Gray]}）");
                Assert(fixedCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == 2,
                       $"沉睡行数量语义：显式 Value 2 给 2 层（表行「{{value}}层」+ 2026-10-31 晚定案按 value 给层；实际 {fixedCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)}）");
            }
            finally
            {
                foreach (var kv in snapBank) pool1.TempElements[kv.Key] = kv.Value;
                // 战场残留（沉睡生物/同伴/靶子）送墓清理，不污染后续段落
                foreach (var pl in new[] { p1, p2 })
                    foreach (var c in core.ZoneManager.GetCards(pl, Zone.Battlefield)
                                 .Where(c => c.ID.StartsWith("VERIFY_SLEEP")).ToList())
                        core.ZoneManager.MoveCard(c, pl, Zone.Battlefield, Zone.Graveyard);
            }
        }

        // ======================================== 充能（2026-10-31 P12：建造→充能；四色费用降低指示物） ========================================

        /// <summary>
        /// 充能回归（2026-11-05 系统动作化改版重写——旧 P12「充能=卡上 Charge 原子主动效果」链整体退役）：
        /// 充能=速度0系统动作（仿攻击宣言：空栈首动·宣言期零支付零选色·对手响应窗口），任意手牌卡
        /// **一回合一次**、结算期反问元素类别（无头/AI 自动取最大余量色）→ **免费落层**（不支付元素）。
        /// ① 普通卡可宣（无充能效果门槛）：入栈不换区·栈上他卡再宣被拒（速度0须空栈）；结算=最大余量色
        ///    +1 层（2红3灰夹具 → 灰3&gt;红2 → CostDownGray=1·红层0）·Exposed=1·钱包不变·净费灰−1；
        /// ② 一张卡一回合一次：同回合第二宣拒（reason 含「一回合」）·跨回合恢复；
        /// ③ 颜色域=「卡具有且未充满」：单色卡（法术锚价红2）逐回合充 2/2 后 CanCharge 拒（含「没有可充颜色」）；
        /// ④ 法术补差：层并入模式 0 锚价套算（cast 账单红 2→1）；
        /// ⑤ 对手回合拒（reason 含「自己的回合」）；战场源拒（reason 含「手牌」）；
        /// ⑥ 换区清除：层与 Exposed 全清；
        /// ⑦ 副本/跨玩家隔离（指示物实例级）。
        /// </summary>
        private static void TestCharge(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var pool1 = core.ElementPool.GetPool(p1);
            var snapWallet = new Dictionary<ManaType, int>(pool1.TempElements);
            int snapIndex = pool1.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9; // 防上限钳干扰（TestActiveEffectCost 同口径）
            foreach (var t in AllManaTypes()) pool1.TempElements[t] = 20;

            // 夹具：钉死锚价 {红2,灰3} 的非主动效果（E 桶直出）——**不带任何充能效果**
            //（2026-11-05 系统动作化：充能不再要求卡面载体，任意手牌卡可宣）
            CardEffectData PricedOnPlay(string id, float red, float gray)
            {
                var eff = new CardEffectData
                {
                    Id = id + "_E1",
                    TriggerTiming = (int)TriggerTiming.OnPlay, // 非主动：锚价入卡费 E 桶（主动=运行时现付不占卡费）
                    AnchorCostPinned = 1,                       // 锚价钉死：直出不推导（P5/P7b 同机制）
                    AnchorCost = new ElementCost(),
                };
                eff.AnchorCost[ManaType.Red] = red;
                eff.AnchorCost[ManaType.Gray] = gray;
                return eff;
            }
            CardData MultiColorData(string id) => new CardData
            {
                ID = id, CardName = "P12·充" + id, Supertype = Cardtype.Enchantment,
                // 2026-11-06 opt-in：充能资格=卡面声明——+1 灰入推导费（含 PrePayload 留影，
                // 充能上限基数含本灰——灰 1 层可充回属设计涌现）：抵扣前 2红4灰·充 1 灰层后净 2红3灰
                ChargeAbility = true,
                Effects = new List<CardEffectData> { PricedOnPlay(id, 2, 3) },
            };

            try
            {
                // ---- ① 普通卡宣言：入栈不换区·空栈门·结算免费落层（最大余量色） ----
                var dataA = MultiColorData("VERIFY_P12_A");
                CardCostService.GetPrePayloadCost(dataA, 0, out var preA, out _);
                Assert((int)preA[ManaType.Red] == 2 && (int)preA[ManaType.Gray] == 4,
                       $"P12① 前置：钉死锚价夹具抵扣前卡费 = 2红4灰（含充能费 1 灰；实际 红{preA[ManaType.Red]:0.#}灰{preA[ManaType.Gray]:0.#}）");
                var f1 = InjectCard(core, p1, dataA);
                var f1b = InjectCard(core, p1, MultiColorData("VERIFY_P12_B"));
                int redBefore = (int)pool1.TempElements[ManaType.Red];
                int grayBefore = (int)pool1.TempElements[ManaType.Gray];
                bool ok1 = GameActions.DeclareCharge(core, p1, f1, out var r1);
                bool stackLive = ok1 && core.StackEngine.StackSize > 0;
                bool stackTopCharge = ok1 && core.StackEngine.PeekStack() != null
                                       && core.StackEngine.PeekStack().IsChargeDeclaration;
                bool stackGate = !GameActions.DeclareCharge(core, p1, f1b, out var rStack)
                                 && rStack != null && rStack.Contains("空栈");
                Assert(ok1 && stackLive && stackTopCharge && stackGate && f1.GetZone() == Zone.Hand,
                       $"P12① 充能宣言入栈成功（CHG 栈对象·卡仍在手牌·栈上他卡再宣被拒「{rStack ?? "?"}」·拒因 {r1 ?? "无"}）");
                GameActions.DrainStack(core);
                var n1 = GameActions.GetCardCost(f1);
                Assert(f1.GetCounterCount(CardCore.Attribute.CounterRules.CostDownGrayCounter) == 1
                       && f1.GetCounterCount(CardCore.Attribute.CounterRules.CostDownRedCounter) == 0
                       && f1.GetCounterCount(CardCore.Attribute.CounterRules.ExposedCounter) == 1
                       && (int)pool1.TempElements[ManaType.Red] == redBefore
                       && (int)pool1.TempElements[ManaType.Gray] == grayBefore
                       && (int)n1[ManaType.Red] == 2 && (int)n1[ManaType.Gray] == 3,
                       $"P12① 结算：免费落最大余量色（灰4>红2→灰） CostDownGray=1·红层0·Exposed=1·钱包不动·净费 2红3灰（4灰−1层）"
                       + $"（实际 红{n1[ManaType.Red]:0.#}灰{n1[ManaType.Gray]:0.#}·灰层 {f1.GetCounterCount(CardCore.Attribute.CounterRules.CostDownGrayCounter)}"
                       + $"·钱包 红{redBefore}→{(int)pool1.TempElements[ManaType.Red]}灰{grayBefore}→{(int)pool1.TempElements[ManaType.Gray]}）");

                // ---- ② 一张卡一回合一次：同回合第二宣拒 ----
                bool sameTurn = GameActions.DeclareCharge(core, p1, f1, out var rOnce);
                bool onceGate = !sameTurn && rOnce != null && rOnce.Contains("一回合");
                Assert(onceGate,
                       $"P12② 一回合一次：同回合再宣被拒（{rOnce ?? "未拒"}）");

                // ---- ⑤a 对手回合拒 + 战场源拒 ----
                EndTurnPumped(core, p1); // 推进到 p2 回合
                bool oppTurnOk = !GameActions.CanCharge(core, p1, f1, out var rOpp)
                                 && rOpp != null && rOpp.Contains("自己的回合");
                EndTurnPumped(core, p2); // 回 p1
                EnsureMainPhase(core, p1);
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 20; // 回合末钱包清空→回填
                var fBf = InjectCard(core, p1, MultiColorData("VERIFY_P12_BF"));
                Assert(core.ZoneManager.TryMoveToBattlefield(fBf, p1, Zone.Hand),
                       "P12⑤ 前置：夹具卡入战场（占格直入）");
                bool fieldOk = !GameActions.CanCharge(core, p1, fBf, out var rField)
                               && rField != null && rField.Contains("手牌");
                Assert(oppTurnOk && fieldOk,
                       $"P12⑤ 对手回合拒「{rOpp}」·战场源拒「{rField}」");

                // ---- ③ 锚价卡逐回合充到满（2026-11-06 opt-in 后夹具费=锚红2+充能费灰1 双色）：
                //      余量序自动落色 红2 →（平局1/1·色序灰先）灰1 → 红1 →「没有可充颜色」 ----
                var spellData = new CardData
                {
                    ID = "VERIFY_P12_S", CardName = "P12·充能法术", Supertype = Cardtype.Spell,
                    ChargeAbility = true, // 充能资格：+1 灰并入施放账单（纯法术基价 0→1 灰）
                    Effects = new List<CardEffectData>
                    {
                        new CardEffectData
                        {
                            Id = "VERIFY_P12_S_E", TriggerTiming = (int)TriggerTiming.Activate_Active,
                            AnchorCostPinned = 1, AnchorCost = new ElementCost(), // 主动锚价：施放时现付（不进卡费）
                            AtomicEffects = new List<AtomicEffectEntry> { Atom(AtomicEffectType.DealDamage) },
                        },
                    },
                };
                spellData.Effects[0].AnchorCost[ManaType.Red] = 2;
                var fs = InjectCard(core, p1, spellData);
                var sBill0 = GameActions.GetCardCost(fs, 0);
                Assert((int)sBill0[ManaType.Red] == 2 && (int)sBill0[ManaType.Gray] == 1,
                       $"P12③/④ 前置：法术 cast 账单红 2 灰 1（锚价现付+充能费并入；实际 {sBill0}）");
                bool c1 = GameActions.DeclareCharge(core, p1, fs, out _); // 余量红2>灰1 → 红
                GameActions.DrainStack(core);
                var sBill1 = GameActions.GetCardCost(fs, 0);
                bool step1 = c1 && (int)sBill1[ManaType.Red] == 1
                             && fs.GetCounterCount(CardCore.Attribute.CounterRules.CostDownRedCounter) == 1;
                EndTurnPumped(core, p1);
                EnsureMainPhase(core, p1);
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 20;
                bool c2 = GameActions.DeclareCharge(core, p1, fs, out _); // 红1=灰1 平局→色序灰先
                GameActions.DrainStack(core);
                bool step2 = c2 && fs.GetCounterCount(CardCore.Attribute.CounterRules.CostDownGrayCounter) == 1;
                EndTurnPumped(core, p1);
                EnsureMainPhase(core, p1);
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 20;
                bool c3 = GameActions.DeclareCharge(core, p1, fs, out _); // 仅剩红 1 → 红
                GameActions.DrainStack(core);
                bool fullGate = !GameActions.CanCharge(core, p1, fs, out var rFull)
                                && rFull != null && rFull.Contains("没有可充颜色");
                Assert(step1 && step2 && c3 && fullGate
                       && fs.GetCounterCount(CardCore.Attribute.CounterRules.CostDownRedCounter) == 2,
                       $"P12③/④ 逐层充到满：红2(锚)·灰1(充能费)→红·灰·红→充满拒「{rFull ?? "可充(未拒)"}」"
                       + $"（step红={step1} step灰={step2}）");

                // ---- ⑦ 副本/跨玩家隔离（指示物实例级） ----
                var ca = InjectCard(core, p1, dataA);
                var cb = InjectCard(core, p1, dataA);
                bool a1 = GameActions.DeclareCharge(core, p1, ca, out _);
                GameActions.DrainStack(core);
                var nb = GameActions.GetCardCost(cb);
                Assert(a1 && (int)nb[ManaType.Red] == 2 && (int)nb[ManaType.Gray] == 4
                       && cb.GetCounterCount(CardCore.Attribute.CounterRules.CostDownGrayCounter) == 0,
                       $"P12⑦ 副本隔离：A 充 1 灰后 B 净费仍 2红4灰·B 灰层 0（实际 {nb}）");
                var theirs = InjectCard(core, p2, dataA);
                var nTheirs = GameActions.GetCardCost(theirs);
                Assert((int)nTheirs[ManaType.Red] == 2
                       && theirs.GetCounterCount(CardCore.Attribute.CounterRules.CostDownGrayCounter) == 0,
                       $"P12⑦ 跨玩家隔离：p2 同名实例净费 2红4灰·灰层 0（实际 {nTheirs}）");

                // ---- ⑥ 换区清除：层与 Exposed 全清（免费落层无可退元素） ----
                var f6 = InjectCard(core, p1, dataA);
                int walletRed6 = (int)pool1.TempElements[ManaType.Red];
                bool f6ok = GameActions.DeclareCharge(core, p1, f6, out _);
                GameActions.DrainStack(core);
                core.ZoneManager.MoveCard(f6, p1, Zone.Hand, Zone.Graveyard);
                var n6 = GameActions.GetCardCost(f6);
                Assert(f6ok && (int)pool1.TempElements[ManaType.Red] == walletRed6
                       && f6.GetCounterCount(CardCore.Attribute.CounterRules.CostDownGrayCounter) == 0
                       && f6.GetCounterCount(CardCore.Attribute.CounterRules.ExposedCounter) == 0
                       && (int)n6[ManaType.Red] == 2 && (int)n6[ManaType.Gray] == 4,
                       $"P12⑥ 换区清除：充 1 灰后移出手牌——灰层/Exposed 归零·净费回 2红4灰·钱包不动"
                       + $"（红 {walletRed6}→{(int)pool1.TempElements[ManaType.Red]}）");
            }
            finally
            {
                pool1.GlobalTurnIndex = snapIndex;
                pool1.TempElements.Clear();
                foreach (var kv in snapWallet) pool1.TempElements[kv.Key] = kv.Value;
                // 本段注入物清理：手牌/墓/战场/元素池残留直移除——不污染后续段落
                bool IsP12(Card c)
                {
                    var d = (c as CardWrapper)?.GetData();
                    return d != null && d.ID != null && d.ID.StartsWith("VERIFY_P12");
                }
                foreach (var pl in new[] { p1, p2 })
                {
                    foreach (var c in core.ZoneManager.GetCards(pl, Zone.Hand).Where(IsP12).ToList())
                        core.ZoneManager.MoveCard(c, pl, Zone.Hand, Zone.Graveyard);
                    foreach (var c in core.ZoneManager.GetCards(pl, Zone.Graveyard).Where(IsP12).ToList())
                        core.ZoneManager.GetZoneContainer(pl).Remove(c, Zone.Graveyard);
                    foreach (var c in core.ZoneManager.GetCards(pl, Zone.Battlefield).Where(IsP12).ToList())
                        core.ZoneManager.GetZoneContainer(pl).Remove(c, Zone.Battlefield);
                }
            }
        }

        // ======================================== 场地格（2026-10-29 P8 定案） ========================================

        /// <summary>
        /// P8 场地卡回归（场地=结界+场地标记·每方一格·只能自顶替·普通耐久·可建造）：
        /// ① 数据层：IsField 进内容哈希（改标记=换卡 ID）+ isField 紧凑序列化往返保真 + 非结界脏数据装载降级；
        /// ② 打出落位：落己方场地格 (0,2)；再打=顶替（旧卡入墓不回手、新卡就位）；
        /// ③ 对手无法顶替：对手场地卡落自己的格 (6,4)，己方场地不受影响；
        /// ④ 双向拒格：普通卡不可落场地格（UnitCells 不含）+ 场地卡钉单位格被强制回场地格 + 普通卡钉场地格无效；
        /// ⑤ 容量口径：场地卡不计单位容量 9——满场普通卡被拒、场地卡仍可打出；
        /// ⑥ 耐久全额：受伤按全额扣（无「每次最多-1」钳），归零按结界规则销毁入墓；
        /// ⑦ 建造：场地卡手上可逐色预建；打出（顶替）入场后已建额随实例作废清零（P7b 离手口径）。
        /// </summary>
        private static void TestP8FieldCards(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var board = new BoardState(core, p1, p2);
            board.Resync();
            var pool1 = core.ElementPool.GetPool(p1);
            var snapWallet = new Dictionary<ManaType, int>(pool1.TempElements);
            int snapIndex = pool1.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9; // 防上限钳干扰（TestBuildPrepaid 同口径）
            foreach (var t in AllManaTypes()) pool1.TempElements[t] = 20;

            // 夹具：钉死锚价 3 灰的（场地/普通）结界——免费出牌口径可复算；Durability=4 印刷耐久
            // charge=true 仅 P8⑦ 充能夹具用（2026-11-06 opt-in：资格=卡面声明·+1 灰入推导费）
            CardData FieldData(string id, bool isField = true, bool charge = false)
            {
                var eff = new CardEffectData
                {
                    Id = id + "_E1",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AnchorCostPinned = 1, // 锚价钉死：直出不推导（P5/P7b 同机制）
                    AnchorCost = new ElementCost(),
                };
                eff.AnchorCost[ManaType.Gray] = 3;
                return new CardData
                {
                    ID = id, CardName = "P8·" + id, Supertype = Cardtype.Enchantment,
                    IsField = isField, Durability = 4, ChargeAbility = charge,
                    Effects = new List<CardEffectData> { eff },
                };
            }
            bool IsP8(Card c)
            {
                var d = (c as CardWrapper)?.GetData();
                return d != null && d.ID != null && d.ID.StartsWith("VERIFY_P8");
            }

            try
            {
                // ---- ① 数据层：哈希 + 序列化往返 + 脏数据降级 ----
                var hashOn = SynergyUI.ContentHasher.HashCard(FieldData("VERIFY_P8_H", true));
                var hashOff = SynergyUI.ContentHasher.HashCard(FieldData("VERIFY_P8_H", false));
                Assert(hashOn != hashOff,
                       $"P8① 场地标记进内容哈希：同卡 IsField 开/关 ID 不同（{hashOn} vs {hashOff}——改标记=换卡 ID）");

                var jsonCards = new List<CardConfigEntry>
                {
                    new CardConfigEntry { id = "VERIFY_P8_JSON_FIELD", cardName = "P8·序列化场地", supertype = "Enchantment", isField = true },
                    new CardConfigEntry { id = "VERIFY_P8_JSON_PLAIN", cardName = "P8·序列化普通", supertype = "Enchantment" },
                    new CardConfigEntry { id = "VERIFY_P8_JSON_DIRTY", cardName = "P8·脏数据", supertype = "Creature", isField = true },
                };
                // 脏数据卡的降级 Error 只捕获不上控制台（负向探针——断言读捕获文本反向锚定）
                var dirtyErrs = new List<string>();
                var prevSinkP8 = CardCore.TideLog.Sink;
                CardCore.TideLog.Sink = (level, msg) =>
                {
                    if (level == CardCore.TideLogLevel.Error) dirtyErrs.Add(msg ?? "");
                };
                List<CardData> loaded;
                try { loaded = CardLoader.LoadCardsFromText(CardConfigCompact.EmitCompact(jsonCards, new DeckConfig())); }
                finally { CardCore.TideLog.Sink = prevSinkP8; }
                var lField = loaded.FirstOrDefault(c => c.ID == "VERIFY_P8_JSON_FIELD");
                var lPlain = loaded.FirstOrDefault(c => c.ID == "VERIFY_P8_JSON_PLAIN");
                var lDirty = loaded.FirstOrDefault(c => c.ID == "VERIFY_P8_JSON_DIRTY");
                Assert(lField != null && lField.IsField && lPlain != null && !lPlain.IsField
                       && lDirty != null && !lDirty.IsField
                       && dirtyErrs.Any(e => e.Contains("降级")),
                       "P8① 序列化链：isField 紧凑发射→装载往返保真；缺省列=false；"
                       + "非结界脏数据装载降级为普通卡（Error 已捕获——不炸装载、控制台零红）");

                // ---- ② 打出落位 + 自顶替 ----
                var f1 = InjectCard(core, p1, FieldData("VERIFY_P8_A"));
                Assert(GameActions.PlayCard(core, p1, f1), "P8② 场地卡打出（PlayCard 声明成功）");
                GameActions.DrainStack(core);
                Assert(f1.GetZone() == Zone.Battlefield, "P8② cast 结算入场（进战场区）");
                board.Resync();
                Assert(board.TryGetCell(f1, out int ax, out int az) && ax == 1 && az == 2,
                       $"P8② 落位己方场地格 (1,2)（实际 ({ax},{az})）");
                Assert(board.FieldCardOf(p1) == f1 && board.FieldOccupied(p1),
                       "P8② FieldCardOf/FieldOccupied 查询口与落位一致");
                Assert(f1.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) >= 1,
                       "P8② 入场耐久层初始化（EquipRules.OnEnterBattlefield 结界同链）");

                var f2 = InjectCard(core, p1, FieldData("VERIFY_P8_B"));
                Assert(GameActions.PlayCard(core, p1, f2), "P8② 场地格已有卡时第二张场地可打出（=顶替，不拒）");
                GameActions.DrainStack(core);
                Assert(f2.GetZone() == Zone.Battlefield && f1.GetZone() == Zone.Graveyard
                       && core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(f1),
                       "P8② 顶替：旧场地入墓（不是回手）、新卡就位战场");
                board.Resync();
                Assert(board.FieldCardOf(p1) == f2 && board.TryGetCell(f2, out int bx, out int bz) && bx == 1 && bz == 2,
                       "P8② 顶替后新卡落 (1,2)，每方仍只一张场地");

                // ---- ③ 对手无法顶替（只能落自己的格）----
                var fOpp = InjectCard(core, p2, FieldData("VERIFY_P8_C"));
                Assert(core.ZoneManager.TryMoveToBattlefield(fOpp, p2, Zone.Hand) && fOpp.GetZone() == Zone.Battlefield,
                       "P8③ 对手场地卡入场（入场口直入——与 cast 结算同出口）");
                Assert(f2.GetZone() == Zone.Battlefield,
                       "P8③ 对手场地入场不影响己方场地（f2 仍在场——顶替只查己方战场区）");
                board.Resync();
                int ox = -1, oz = -1;
                Assert(board.FieldCardOf(p2) == fOpp && board.TryGetCell(fOpp, out ox, out oz) && ox == 6 && oz == 4
                       && board.FieldCardOf(p1) == f2,
                       $"P8③ 对手场地落自己的格 (6,4)（实际 ({ox},{oz})）；己方场地仍是 f2");

                // ---- ④ 双向拒格 ----
                bool unitCellsContainField = false;
                foreach (var c in BoardLayout.UnitCells(0))
                    if (c.x == 1 && c.z == 2) unitCellsContainField = true;
                Assert(!unitCellsContainField && BoardLayout.RoleOf(1, 2) == CellRole.Field,
                       "P8④ 普通卡不可落场地格：UnitCells 不含 (1,2)（选格面天然只有单位格）");

                board.PinCard(f2, 2, 2); // 恶意钉到单位格——Resync 强制回场地格
                Assert(board.TryGetCell(f2, out int px, out int pz) && px == 1 && pz == 2,
                       $"P8④ 场地卡不可落单位格：钉 (2,2) 被强制回场地格（实际 ({px},{pz})）");

                var normalEnchant = InjectCard(core, p1, FieldData("VERIFY_P8_D", isField: false));
                core.ZoneManager.TryMoveToBattlefield(normalEnchant, p1, Zone.Hand);
                board.PinCard(normalEnchant, 0, 2); // 普通结界钉场地格——钉无效回落单位格
                int nx = -1, nz = -1;
                Assert(normalEnchant.GetZone() == Zone.Battlefield
                       && board.TryGetCell(normalEnchant, out nx, out nz)
                       && BoardLayout.RoleOf(nx, nz) == CellRole.Unit,
                       $"P8④ 普通结界钉场地格无效：仍落单位格（实际 ({nx},{nz})）");

                // ---- ⑤ 容量口径：场地卡不计单位容量 9 ----
                var fillers = new List<Card>();
                while (core.ZoneManager.GetZoneContainer(p1).NonFieldBattlefieldCount()
                       < ZoneManager.BattlefieldCapacityPerPlayer)
                {
                    var fc = InjectCard(core, p1, FieldData("VERIFY_P8_FILL" + fillers.Count, isField: false));
                    if (!core.ZoneManager.TryMoveToBattlefield(fc, p1, Zone.Hand)) break;
                    fillers.Add(fc);
                }
                Assert(!core.ZoneManager.HasBattlefieldSpace(p1),
                       "P8⑤ 前置：非场地卡填满后 HasBattlefieldSpace=false（场地 f2 在场不计入）");

                var normFull = InjectCard(core, p1, FieldData("VERIFY_P8_E", isField: false));
                Assert(!GameActions.PlayCard(core, p1, normFull, null, Zone.Hand, 0, out var capReject)
                       && capReject != null && capReject.Contains("战场已满"),
                       $"P8⑤ 满场普通结界被拒（reason={capReject ?? "可打(未拒)"}）");
                var f3 = InjectCard(core, p1, FieldData("VERIFY_P8_F"));
                Assert(GameActions.PlayCard(core, p1, f3), "P8⑤ 满场场地卡仍可打出（不占单位容量）");
                GameActions.DrainStack(core);
                Assert(f3.GetZone() == Zone.Battlefield && f2.GetZone() == Zone.Graveyard,
                       "P8⑤ 场地卡满场入场成功且顶替旧场地（容量口径=非场地数）");

                // ---- ⑥ 耐久全额 + 归零销毁 ----
                int dur = f3.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter);
                Assert(dur >= 4, $"P8⑥ 前置：场地卡耐久层 {dur} ≥ 4（印刷 4·免费点同结界派生）");
                CardCore.Attribute.CounterRules.LoseDurability(core, f3, 2, "P8验证");
                Assert(f3.GetZone() == Zone.Battlefield
                       && f3.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == dur - 2,
                       $"P8⑥ 受伤全额扣耐久：-2 后 {dur - 2}/{dur}（无「每次最多-1」钳）；未归零不销毁");
                CardCore.Attribute.CounterRules.LoseDurability(core, f3, dur - 2, "P8验证");
                Assert(f3.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) <= 0
                       && f3.GetZone() == Zone.Graveyard,
                       "P8⑥ 耐久归零 → 按结界规则销毁入墓（Smashed 直毁）");

                // ---- ⑦ 充能可用（2026-11-05 系统动作化 + 2026-11-06 opt-in 资格）+ 入场后进度作废 ----
                // 夹具=普通场地卡（钉死 3 灰 + 充能费 1 灰）；进度挂 CostDownGray 层
                var fBuild = InjectCard(core, p1, FieldData("VERIFY_P8_G", charge: true));
                bool bOk = GameActions.DeclareCharge(core, p1, fBuild, out var bReject);
                GameActions.DrainStack(core);
                Assert(bOk && fBuild.GetCounterCount(CardCore.Attribute.CounterRules.CostDownGrayCounter) == 1,
                       $"P8⑦ 充能在场地卡可用：手牌充 1 灰成功（拒因 {bReject ?? "无"}）");
                Assert(GameActions.PlayCard(core, p1, fBuild), "P8⑦ 已充能场地卡可打出（净费=4−1=3 灰，含充能费）");
                GameActions.DrainStack(core);
                Assert(fBuild.GetZone() == Zone.Battlefield
                       && fBuild.GetCounterCount(CardCore.Attribute.CounterRules.CostDownGrayCounter) == 0,
                       "P8⑦ 入场后充能进度随换区清零（指示物 Resident 口径——预付不退、不转移）");

                // ---- ⑧ 盘面视图（表现层数据道，验收7）：BuildLocal 场地卡落 (0,2) + 耐久显示基准 ----
                // 身份键=RuntimeId（BattleScreen 对账同键）；诊断附带 SelfUnits 计数。
                // 显式 Resync（⑤-⑦ 直入战场未落格对账——真实对局由换区事件驱动，本测试手动同拍）
                board.Resync();
                uint fRid = fBuild.RuntimeId;
                var dView = SynergyUI.BattleView.BuildLocal(core, board, p1, p2);
                var fieldView = dView.SelfUnits.FirstOrDefault(u => u != null && u.RuntimeId == fRid);
                Assert(fieldView != null && fieldView.X == 1 && fieldView.Z == 2,
                       $"P8⑧ 盘面视图：BuildLocal 场地卡落己方场地格 (1,2)（实际 ({(fieldView == null ? -1 : fieldView.X)},{(fieldView == null ? -1 : fieldView.Z)})）"
                       + "——BattleFieldView 按 X/Z 直渲（本地=0 号座位原生半场·无观察者归一）+ 结界耐久中槽（DurabilityOnly）同链"
                       + "（2026-10-29 P8 布局定案：P1Field=(1,2)；旧 (0,2) 为收边缺口格——随布局改版作废）");
                // 耐久显示口=Runtime.Counters 的 Durability 层（BattleFieldView 中槽直读同源；
                // BaseLife 配色基准走 CardCatalog 查表——夹具卡不入表，断言取真数据源）
                int viewDur = fieldView != null && fieldView.Runtime != null && fieldView.Runtime.Counters != null
                    ? (fieldView.Runtime.Counters.FirstOrDefault(c => c.Key == CardCore.Attribute.CounterRules.DurabilityCounter)?.Value ?? 0)
                    : 0;
                Assert(viewDur >= 1,
                       $"P8⑧ 耐久显示：视图 Runtime.Counters 带耐久层 {viewDur}（≥1——DurabilityOnly 中槽直读同源，2026-10-17 恒耐久定案同链）");
            }
            finally
            {
                pool1.GlobalTurnIndex = snapIndex;
                pool1.TempElements.Clear();
                foreach (var kv in snapWallet) pool1.TempElements[kv.Key] = kv.Value;
                // 本段注入物清理（P7b 同款）：三个区全扫——战场/墓地残留直移除，手牌送墓后再移除
                foreach (var pl in new[] { p1, p2 })
                {
                    foreach (var c in core.ZoneManager.GetCards(pl, Zone.Hand).Where(IsP8).ToList())
                        core.ZoneManager.GetZoneContainer(pl).Remove(c, Zone.Hand);
                    foreach (var c in core.ZoneManager.GetCards(pl, Zone.Battlefield).Where(IsP8).ToList())
                        core.ZoneManager.GetZoneContainer(pl).Remove(c, Zone.Battlefield);
                    foreach (var c in core.ZoneManager.GetCards(pl, Zone.Graveyard).Where(IsP8).ToList())
                        core.ZoneManager.GetZoneContainer(pl).Remove(c, Zone.Graveyard);
                }
                board.Resync();
            }
        }

    }
}
