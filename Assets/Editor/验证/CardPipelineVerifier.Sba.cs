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
        // ======================================== 时点接线（P0）/ 衍生物（P1）/ 计数与日志（P2） ========================================

        /// <summary>合成触发卡：生物 1/1 灰 1 费，指定时点的 DrawCard 触发式（触发次数=手牌增量，可观察）。
        /// 显式 TriggerLimitPerTurn=-1 为**数据兼容遗留**（2026-10-30 无次数上限定案：该列零消费——
        /// 本段夹具均为同回合单次发射形状，不受影响）。</summary>
        private static CardData TrigData(string id, params TriggerTiming[] timings)
        {
            var data = new CardData { ID = id, CardName = id, Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            data.Cost = CostOf((ManaType.Gray, 1));
            foreach (var t in timings)
            {
                data.Effects.Add(new CardEffectData
                {
                    Id = $"{id}_T{(int)t}",
                    DisplayName = id,
                    TriggerTiming = (int)t,
                    TriggerLimitPerTurn = -1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)
                    },
                });
            }
            return data;
        }

        /// <summary>灌满 bank/上限（快照返回，结束时恢复）——出牌段落的费用前置。</summary>
        private static (Dictionary<ManaType, int> snap1, Dictionary<ManaType, int> snap2, int i1, int i2)
            FillBanks(GameCore core, Player p1, Player p2)
        {
            var pool1 = core.ElementPool.GetPool(p1);
            var pool2 = core.ElementPool.GetPool(p2);
            var snap1 = new Dictionary<ManaType, int>(pool1.TempElements);
            var snap2 = new Dictionary<ManaType, int>(pool2.TempElements);
            int i1 = pool1.GlobalTurnIndex, i2 = pool2.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;
            pool2.GlobalTurnIndex = 9;
            foreach (var t in AllManaTypes()) { pool1.TempElements[t] = 99; pool2.TempElements[t] = 99; }
            return (snap1, snap2, i1, i2);
        }

        private static void RestoreBanks(GameCore core, Player p1, Player p2,
            (Dictionary<ManaType, int> snap1, Dictionary<ManaType, int> snap2, int i1, int i2) s)
        {
            var pool1 = core.ElementPool.GetPool(p1);
            // 段尾收口（2026-09-21）：清场残留观察者（否则下段 EnsureMainPhase 的阶段事件会让
            // 其触发式入栈、记速器抬 1 拦住 0 速宣言——12 条连锁的根因）+ 排干清栈。
            foreach (var pl in new[] { p1, p2 })
            {
                var leftovers = core.ZoneManager.GetCards(pl, Zone.Battlefield).ToList();
                foreach (var c in leftovers)
                    core.ZoneManager.GetZoneContainer(pl).Remove(c, Zone.Battlefield);
            }
            GameActions.DrainStack(core);
            if (!core.StackEngine.IsEmpty) core.StackEngine.Clear();

            var pool2 = core.ElementPool.GetPool(p2);
            pool1.GlobalTurnIndex = s.i1;
            pool2.GlobalTurnIndex = s.i2;
            pool1.TempElements.Clear();
            foreach (var kv in s.snap1) pool1.TempElements[kv.Key] = kv.Value;
            pool2.TempElements.Clear();
            foreach (var kv in s.snap2) pool2.TempElements[kv.Key] = kv.Value;
        }

        /// <summary>
        /// S3b 删发动区验收段（2026-11-05 收口）：卡施放中**留来源区**（手牌/牌库顶），
        /// 「施放中」只由栈条目表示（StackEngine.IsPendingCast 单口）。四场景：
        /// ①手牌施放——卡留手牌+手牌口径六读数一致（快照 HandCount/CardHandInfo.Count/
        ///   OwnRuntimeIds/ZoneCards(Hand)/BuildLocal.SelfHand/观测 g[5]）+重复施放拒+施放卡不投影战场格；
        /// ②牌库顶施放+响应窗抽/磨——牌库顶锁（IsDeckLocked：跳过 pending 取下一张）；
        /// ③永久物入场——Source=CastPlayed、FromZone=Hand、Resync 落格；
        /// ④法术 Hand→Graveyard 直达。附 Zone=8 存档迁移断言。
        /// Enter/Leave Activation 事件类型已随区退役**删除**（订阅口无法写出——编译即证明无生产点）。
        /// </summary>
        private static void TestS3bActivationRemoval(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);
            var board = new BoardState(core, p1, p2);
            try
            {
                // ---- ① 手牌施放：卡留手牌 / 口径一致 / 重复施放拒 / 不投影战场格 ----
                var s3bSpellData = new CardData { ID = "VERIFY_S3B_SPELL", CardName = "S3b法术" };
                s3bSpellData.Supertype = Cardtype.Spell;
                var s3bSpell = InjectCard(core, p1, s3bSpellData);
                int handBase = core.ZoneManager.GetCards(p1, Zone.Hand).Count;

                Assert(GameActions.PlayCard(core, p1, s3bSpell), "S3b①：手牌施放声明成功—" + PlayGateProbe(core, p1, s3bSpell));
                Assert(core.ZoneManager.IsCardInZone(s3bSpell, p1, Zone.Hand),
                       "S3b①：施放中卡仍在手牌区（物理不动——「施放中」由栈条目表示）");
                Assert(core.StackEngine.IsPendingCast(s3bSpell), "S3b①：IsPendingCast 单口=true（cast 条目在栈）");
                Assert(!GameActions.PlayCard(core, p1, s3bSpell),
                       "S3b①：重复施放拒（卡留手牌后 hand.Contains 恒真——栈上重复闸是新防线）");

                // 手牌口径七读数一致（S3b 定案 2026-11-05：全部消费口同源剔除施放中）
                int expected = handBase - 1;
                var snap = CardCore.Network.NetSnapshotBuilder.Build(core, 0);
                var psState = snap.Players.First(pl => pl.Seat == 0);
                var handInfo = snap.Hands.First(h => h.Seat == 0);
                var handZone = snap.ZoneCards.First(z => z.Seat == 0 && z.Zone == (int)Zone.Hand);
                var localView = SynergyUI.BattleView.BuildLocal(core, board, p1, p2);
                var obsG5 = (int)AI.NeuralEnv.TideObservation.Build(core).Globals[5];
                Assert(psState.HandCount == expected && handInfo.Count == expected
                       && handInfo.OwnRuntimeIds.Length == expected && handZone.Cards.Length == expected
                       && localView.SelfHand.Count == expected && localView.Self.HandCount == expected
                       && obsG5 == expected,
                       $"S3b①：手牌口径七读数一致=剔除施放中 {expected}（快照Count={psState.HandCount} 手牌信息={handInfo.Count} "
                       + $"OwnIds={handInfo.OwnRuntimeIds.Length} 区快照={handZone.Cards.Length} 扇区={localView.SelfHand.Count} "
                       + $"HUD={localView.Self.HandCount} 观测g5={obsG5}）");

                // 施放卡不投影战场格（占位退役）：Resync 后无落格、视图施放列表恒悬浮
                board.Resync();
                Assert(!board.TryGetCell(s3bSpell, out _, out _), "S3b①：施放卡不落单位区格（TryGetCell=false——占位投影退役）");
                var castingViews = localView.CastingCards;
                Assert(castingViews.Any(v => v != null && v.RuntimeId == s3bSpell.RuntimeId && v.X < 0 && v.Z < 0),
                       "S3b①：视图施放列表从栈来且恒悬浮（X/Z=-1，不占格呈现）");

                // ---- ④（并①）法术 Hand→Graveyard 直达 ----
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear();
                Assert(core.ZoneManager.IsCardInZone(s3bSpell, p1, Zone.Graveyard),
                       "S3b④：法术结算 Hand→Graveyard 直达（不经发动区）");
                Assert(!core.StackEngine.IsPendingCast(s3bSpell), "S3b④：结算后 IsPendingCast=false（cast 条目已消费）");

                // ---- ② 牌库顶施放 + 响应窗抽/磨：牌库顶锁 ----
                // 授权：牌库顶当手牌光环直投（教学 Seeder 同款——Activate 直调）
                var auraCarrier = new CardWrapper(new CardData
                {
                    ID = "VERIFY_S3B_AURA", CardName = "S3b光环载体", Supertype = Cardtype.Enchantment,
                });
                auraCarrier.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(auraCarrier, Zone.Battlefield);
                RuleAuraSystem.Activate(RuleAuraComponents.TopDeckAsHandAura, auraCarrier, p1);

                var deckSpellData = new CardData { ID = "VERIFY_S3B_DECK", CardName = "S3b牌库顶法术" };
                deckSpellData.Supertype = Cardtype.Spell;
                var deckSpell = new CardWrapper(deckSpellData);
                deckSpell.SetController(p1);
                var second = new Card { ID = "VERIFY_S3B_DECK2" };
                core.ZoneManager.GetZoneContainer(p1).Add(second, Zone.Deck, DeckPosition.Top); // 第二张先入顶
                core.ZoneManager.GetZoneContainer(p1).Add(deckSpell, Zone.Deck, DeckPosition.Top); // deckSpell 压顶（Add 含 Contains 防重——一次到位）

                Assert(GameActions.PlayCardFromDeckTop(core, p1, deckSpell),
                       "S3b②：牌库顶施放声明成功（TopDeckAsHand 授权）—" + PlayGateProbe(core, p1, deckSpell));
                Assert(core.ZoneManager.IsCardInZone(deckSpell, p1, Zone.Deck)
                       && core.ZoneManager.GetCards(p1, Zone.Deck)[0] == deckSpell,
                       "S3b②：施放中卡留牌库顶原位（IsPendingCast=true=" + core.StackEngine.IsPendingCast(deckSpell) + "）");

                // 响应窗内抽/磨：牌库顶锁——拿不到正在施放的牌（跳过取下一张）
                var drawn = ZoneManagerExtensions.DrawCard(core.ZoneManager, p1);
                Assert(drawn == second && drawn != deckSpell,
                       "S3b②：响应窗内抽牌拿不到施放中卡（抽到第二张——牌库顶锁）");
                var milled = ZoneManagerExtensions.MillCard(core.ZoneManager, p1);
                Assert(milled != deckSpell && milled != null,
                       "S3b②：响应窗内磨牌磨不到施放中卡（磨下一张）");

                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear();
                Assert(core.ZoneManager.IsCardInZone(deckSpell, p1, Zone.Graveyard),
                       "S3b②：牌库顶法术结算 Deck→Graveyard（cast 消费点从真实所在区入墓）");
                // 光环回收：载体撤场（活性=载体在场，撤场即失效——不污染后续段落）
                core.ZoneManager.GetZoneContainer(p1)?.Remove(auraCarrier, Zone.Battlefield);

                // ---- ③ 永久物入场：Source=CastPlayed / FromZone=Hand / Resync 落格 ----
                CardPutToBattlefieldEvent enterEvt = null;
                void OnS3bEnter(CardPutToBattlefieldEvent e) { if (e.Card != null && e.Card.ID == "VERIFY_S3B_CREATURE") enterEvt = e; }
                EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnS3bEnter);
                try
                {
                    var creatureData = new CardData
                    {
                        ID = "VERIFY_S3B_CREATURE", CardName = "S3b生物", Supertype = Cardtype.Creature,
                        Power = 1, Life = 1,
                    };
                    var s3bCreature = InjectCard(core, p1, creatureData);
                    Assert(GameActions.PlayCard(core, p1, s3bCreature), "S3b③：永久物施放声明成功—" + PlayGateProbe(core, p1, s3bCreature));
                    Assert(core.ZoneManager.IsCardInZone(s3bCreature, p1, Zone.Hand) && core.StackEngine.IsPendingCast(s3bCreature),
                           "S3b③：施放中卡先留手牌（入场发生在 cast 结算）");
                    GameActions.DrainStack(core);
                    if (!core.StackEngine.IsEmpty) core.StackEngine.Clear();
                    Assert(core.ZoneManager.IsCardInZone(s3bCreature, p1, Zone.Battlefield), "S3b③：cast 结算后入场在战场");
                    Assert(enterEvt != null && enterEvt.Source == EnterSource.CastPlayed && enterEvt.FromZone == Zone.Hand,
                           "S3b③：入场 Source=CastPlayed、FromZone=Hand（S3b：不经发动区）");
                    board.Resync();
                    Assert(board.TryGetCell(s3bCreature, out int ex, out int ez)
                           && BoardLayout.RoleOf(ex, ez) == CellRole.Unit,
                           "S3b③：入场后 Resync 落单位区格（施放期不占格→入场真占格）");
                    RetireCards(core, p1, s3bCreature);
                }
                finally
                {
                    EventManager.Instance.Unsubscribe<CardPutToBattlefieldEvent>(OnS3bEnter);
                }

                // ---- ⑤ Zone=8 存档迁移（旧档 Activation 读取侧迁 Hand） ----
                var legacyDto = new CardCore.Serialization.SerializableRuntimeCardState { Zone = 8 };
                var migrant = new Card { ID = "VERIFY_S3B_MIGRANT" };
                legacyDto.ApplyToCard(migrant);
                Assert(migrant.GetZone() == Zone.Hand, "S3b⑤：旧档 Zone=8（Activation）读取侧迁 Hand（ApplyToCard 单点）");
            }
            finally
            {
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>段落收尾：本段合成卡撤场 + 注销触发式（防遗留观察者干扰后续段落）。</summary>
        private static void RetireCards(GameCore core, Player owner, params Card[] cards)
        {
            foreach (var c in cards)
            {
                if (c == null) continue;
                core.ZoneManager.GetZoneContainer(owner)?.Remove(c, c.GetZone());
                core.TriggerEngine.UnregisterEntityEffects(c);
            }
        }

        /// <summary>
        /// 进场来源三通道（P0）：手牌打出 CastPlayed / 墓地复活 Revived / token 生成 TokenSpawned。
        /// </summary>
        private static void TestEntrySources(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);

            CardPlayEvent playEvt = null;
            var enterEvents = new List<CardPutToBattlefieldEvent>();
            void OnPlay(CardPlayEvent e) => playEvt = e;
            void OnEnter(CardPutToBattlefieldEvent e) => enterEvents.Add(e);
            EventManager.Instance.Subscribe<CardPlayEvent>(OnPlay);
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnter);
            try
            {
                // ① 手牌打出：宣言 FromZone=Hand；入场 Source=CastPlayed、FromZone=Hand
                //（S3b 2026-11-04 删发动区：卡施放中留手牌——入场不再经 Activation，FromZone=真实来源）
                var a = InjectCard(core, p1, TrigData("VERIFY_ES_PLAYED"));
                enterEvents.Clear();
                // 收口（2026-09-21）：EnsureMainPhase 的阶段事件会让残留触发式入栈（计=1 拦 0 速宣言）——打牌前排干
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear();
                Assert(GameActions.PlayCard(core, p1, a), "来源①：手牌打出声明成功—" + PlayGateProbe(core, p1, a));
                GameActions.DrainStack(core);
                Assert(playEvt != null && playEvt.FromZone == Zone.Hand, "使用宣言 FromZone=Hand");
                Assert(enterEvents.Count == 1 && enterEvents[0].Source == EnterSource.CastPlayed
                       && enterEvents[0].FromZone == Zone.Hand,
                       "打出入场 Source=CastPlayed、FromZone=Hand（S3b：不经发动区）");
                Assert(core.ZoneManager.IsCardInZone(a, p1, Zone.Battlefield), "打出后已入场");

                // ② 墓地复活：Source=Revived
                var b = InjectCard(core, p1, TrigData("VERIFY_ES_REVIVED"));
                core.ZoneManager.GetZoneContainer(p1).Add(b, Zone.Graveyard);
                enterEvents.Clear();
                Assert(core.ZoneManager.TryMoveToBattlefield(b, p1, Zone.Graveyard), "来源②：复活入场成功");
                Assert(enterEvents.Count == 1 && enterEvents[0].Source == EnterSource.Revived,
                       "复活入场 Source=Revived");

                // ③ token/新生：Source=TokenSpawned、FromZone=None
                var c = new CardWrapper(new CardData { ID = "VERIFY_ES_TOKEN", CardName = "来源token", Supertype = Cardtype.Creature, Power = 1, Life = 1 });
                enterEvents.Clear();
                Assert(core.ZoneManager.TryAddToBattlefield(c, p1), "来源③：token 直接入场成功");
                Assert(enterEvents.Count == 1 && enterEvents[0].Source == EnterSource.TokenSpawned
                       && enterEvents[0].FromZone == Zone.None,
                       "token 入场 Source=TokenSpawned、FromZone=None（TryAddToBattlefield 补发已接通）");

                RetireCards(core, p1, a, b, c);
            }
            finally
            {
                EventManager.Instance.Unsubscribe<CardPlayEvent>(OnPlay);
                EventManager.Instance.Unsubscribe<CardPutToBattlefieldEvent>(OnEnter);
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>
        /// payload 过滤（P0.3）：同一入场事件下 OnPlay（登场）/ OnSummon（超集）/ OnOtherCreatureEnter（观察者）互不误触。
        /// 手牌净变量 = 触发次数的计数器（打出 -1 / 每次触发 +1）。
        /// </summary>
        private static void TestTriggerPayloadFilters(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);
            int Hand() => core.ZoneManager.GetCards(p1, Zone.Hand).Count;

            try
            {
                // 观察者 F：他人进场时抽 1（自己入场不触发）
                var f = InjectCard(core, p1, TrigData("VERIFY_TPF_OBS", TriggerTiming.OnOtherCreatureEnter));
                int h0 = Hand();
                Assert(GameActions.PlayCard(core, p1, f), "观察者打出声明");
                GameActions.DrainStack(core);
                Assert(Hand() == h0 - 1, "OnOtherCreatureEnter 自己入场不触发（净 -1=打出无抽）");

                // D：同卡挂 OnPlay + OnSummon 双效果——打出两触发（+2），观察者 +1
                var d = InjectCard(core, p1, TrigData("VERIFY_TPF_BOTH", TriggerTiming.OnPlay, TriggerTiming.OnSummon));
                // 牌库保底：净抽 3（双时点+观察者），抽干则断言测的是牌库见底而非触发（根因 D 修复）
                PadDeck(core, p1, 3, "VERIFY_TPF_BOTHPAD");
                int h1 = Hand();
                Assert(GameActions.PlayCard(core, p1, d), "双时点卡打出声明");
                GameActions.DrainStack(core);
                Assert(Hand() == h1 - 1 + 2 + 1,
                       "打出：登场+1、进场+1（超集语义）、观察者 +1（净 +2）");

                // 复活 D：OnPlay 不触发（Source=Revived）、OnSummon 触发、观察者 +1
                // 2026-09-13 修复：前面累计抽牌可能已抽干牌库——补保底（下方 G 段同款），
                // 否则抽不到=净 0，断言测的是牌库见底而非触发
                while (core.ZoneManager.GetCards(p1, Zone.Deck).Count < 2)
                {
                    var deckFiller = new CardWrapper(new CardData
                    {
                        ID = "VERIFY_TPF_FILLR" + core.ZoneManager.GetCards(p1, Zone.Deck).Count,
                        CardName = "复活前填充", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                    });
                    deckFiller.SetController(p1);
                    core.ZoneManager.GetZoneContainer(p1).Add(deckFiller, Zone.Deck);
                }
                core.ZoneManager.MoveCard(d, p1, Zone.Battlefield, Zone.Graveyard);
                int h2 = Hand();
                Assert(core.ZoneManager.TryMoveToBattlefield(d, p1, Zone.Graveyard), "复活双时点卡");
                GameActions.DrainStack(core);
                Assert(Hand() == h2 + 1 + 1,
                       "复活：登场不触发、进场 +1、观察者 +1（净 +2）");

                // OnCardPlayed 观察者 G：使用宣言时点（含法术）——与 OnPlay 分工
                var g = InjectCard(core, p1, TrigData("VERIFY_TPF_DECL", TriggerTiming.OnCardPlayed));
                Assert(GameActions.PlayCard(core, p1, g), "宣言观察者打出声明");
                GameActions.DrainStack(core);
                // G 入场：G 自身无 OnPlay/OnSummon、观察者 F 对 G 入场 +1；打出的宣言触发 G 自己的 OnCardPlayed +1
                // 牌库保底：套件运行至此 p1 牌库可能已抽干（前面的抽牌观察者累计消耗），
                // 抽不到 = 触发结算了但净 0 —— 补注入填充卡保证断言测的是触发而非牌库见底
                while (core.ZoneManager.GetCards(p1, Zone.Deck).Count < 4)
                {
                    Crumb("deck-filler iter");
                    var filler = new CardWrapper(new CardData
                    {
                        ID = "VERIFY_TPF_FILL" + core.ZoneManager.GetCards(p1, Zone.Deck).Count,
                        CardName = "填充", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                    });
                    filler.SetController(p1);
                    core.ZoneManager.GetZoneContainer(p1).Add(filler, Zone.Deck);
                }
                var spell = InjectCard(core, p1, new CardData
                {
                    ID = "VERIFY_TPF_SPELL", CardName = "宣言测法术", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Gray, 1)),
                });
                int h3 = Hand(); // 注入后稳态基线（原版在注入前测，漏算注入+1——账错）
                Assert(GameActions.PlayCard(core, p1, spell), "打出无效果法术");
                GameActions.DrainStack(core);
                UnityEngine.Debug.Log($"[TPFDBG] OnCardPlayed 法术宣言：净 {Hand() - h3}（期望 0）"
                    + $" 牌库余 {core.ZoneManager.GetCards(p1, Zone.Deck).Count}"
                    + $" 注册数 {core.TriggerEngine.RegisteredEffects.Count(r => r.Effect?.Id != null && r.Effect.Id.StartsWith("VERIFY_TPF_DECL"))}");
                Assert(Hand() == h3 - 1 + 1, "OnCardPlayed 在法术使用宣言触发（净 0，与登场分工）");

                RetireCards(core, p1, f, d, g);
            }
            finally
            {
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>死时点激活（P0.3）：OnAttacked/OnExile/OnTargeted 映射补全后经真实事件触发。</summary>
        private static void TestDeadTimings(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);

            // 映射存在性（原为 null 静默跳过）
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnAttacked) == typeof(AttackDeclarationEvent),
                   "映射：OnAttacked → AttackDeclarationEvent");
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnExile) == typeof(CardCore.Attribute.CardExileEvent),
                   "映射：OnExile → CardExileEvent");
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnReturnFromGraveyard) == typeof(CardPutToBattlefieldEvent),
                   "映射：OnReturnFromGraveyard → CardPutToBattlefieldEvent");
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnTargeted) == typeof(AtomicEffectPhaseEvent),
                   "映射：OnTargeted → AtomicEffectPhaseEvent");

            int Hand() => core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            try
            {
                // OnExile：经 Exile 原子真实管线（handler 基类 PublishEvent → 统一路由）
                var x = InjectCard(core, p1, TrigData("VERIFY_DT_EXILE", TriggerTiming.OnExile));
                Assert(GameActions.PlayCard(core, p1, x), "除外观察者打出");
                GameActions.DrainStack(core);
                int h0 = Hand();
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                    new AtomicEffectInstance { Type = AtomicEffectType.Exile },
                    new EffectExecutionContext
                    {
                        Source = p1, Controller = p1,
                        Targets = new List<Entity> { x },
                        ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                    });
                GameActions.DrainStack(core);
                Assert(Hand() == h0 + 1, "OnExile 经除外事件触发（净 +1）");
                Assert(core.ZoneManager.IsCardInZone(x, p1, Zone.Exile) || x.GetZone() == Zone.Exile, "除外原子已把目标送入除外区");

                // OnAttacked：payload 过滤单测（映射已断言；管线可达性由 OnExile 腿证明）
                var y = InjectCard(core, p1, TrigData("VERIFY_DT_ATKED", TriggerTiming.OnAttacked));
                Assert(GameActions.PlayCard(core, p1, y), "被攻击观察者打出");
                GameActions.DrainStack(core);
                var regY = new RegisteredEffect
                {
                    Effect = new EffectDefinition { Id = "VERIFY_DT_ATKED_F", TriggerTiming = TriggerTiming.OnAttacked },
                    Source = y, Controller = p1,
                };
                Assert(TriggerPayloadFilter.Matches(TriggerTiming.OnAttacked,
                           new AttackDeclarationEvent { Attacker = p2, Target = y, AttackingPlayer = p2 }, regY),
                       "OnAttacked 过滤：被指方=自己 → 通过");
                Assert(!TriggerPayloadFilter.Matches(TriggerTiming.OnAttacked,
                           new AttackDeclarationEvent { Attacker = p2, Target = p1, AttackingPlayer = p2 }, regY),
                       "OnAttacked 过滤：被指方=他人 → 拒绝");

                // OnTargeted：原子 StartApplying 且目标含自己
                var z = InjectCard(core, p1, TrigData("VERIFY_DT_TARGET", TriggerTiming.OnTargeted));
                Assert(GameActions.PlayCard(core, p1, z), "被指向观察者打出");
                GameActions.DrainStack(core);
                int h2 = Hand();
                // 经 executor 真路径：OnTargeted 挂在原子三阶段 StartApplying（PublishPhase 在执行器，
                // 注册表直连不发相位事件——与 TestAtomicPhaseRouting 同根因）
                core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                {
                    Definition = new EffectDefinition
                    {
                        Id = "VERIFY_DT_TARGET_ATOM",
                        ElementCostPrepaid = true,
                        Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 0 } },
                    },
                    Source = p2, Controller = p2,
                    Targets = new List<Entity> { z },
                }, skipElementCost: true).Forget();
                GameActions.DrainStack(core);
                Assert(Hand() == h2 + 1, "OnTargeted 经原子 StartApplying 触发（统一路由修复后可见）");

                RetireCards(core, p1, x, y, z);
            }
            finally
            {
                RestoreBanks(core, p1, p2, banks);
            }
        }

        // ======================================== SBA 窗口（2026-09-15 SBA=速度1栈对象定案） ========================================

        /// <summary>
        /// SBA=速度1栈对象（2026-09-15 定案）：死亡/判负类 SBA 在连锁排干后作为速度1伪对象入栈——
        /// 记速器抬到1、开响应窗口（回合方≥1 / 非回合方严格&gt;1 可连锁）、LIFO 救场效果先结算、
        /// SBA 到点重查（被救回则空转、亡语不触发——与预言延迟验证同哲学）。
        /// 覆盖：T1 时点过滤单测（OnDeath/OnOtherCreatureDeath payload）；
        /// T2 亡语真实管线（门禁自身死亡豁免后 OnDeath 经 SBA 送墓真实触发）；
        /// T4a 速度2治疗在 SBA 窗口救回标死宿主（亡语「对手获得胜利」不触发）；
        /// T4c 速度1治疗非回合方连锁被拒（1 不&gt;1）；
        /// T3 无人救场 → SBA 结算 → 亡语 DeclareVictory 宣告对手胜利（EffectVictory 终局）。
        /// </summary>
        /// <summary>SBA 取证（2026-11-05）：栈顶实况一行——断言失败时直接看出是「空栈 / 卡施放 / 阶段窗 / 其他」，
        /// 免得只看到 "不是 SBA" 却不知道当时栈上是什么。</summary>
        private static string TopState(GameCore core)
        {
            if (core?.StackEngine == null) return "无栈";
            var t = core.StackEngine.Peek();
            if (t == null) return "空栈";
            if (t.IsSBA) return "SBA";
            if (t.IsCardCast) return "CAST:" + ((t.Source as Card)?.ID ?? "?");
            if (t.IsPhaseWindow) return "PHASE";
            if (t.IsAttackDeclaration) return "ATK";
            if (t.IsGuardDeclaration) return "GRD";
            return "OTHER:" + (t.Definition?.Id ?? t.Source?.ToString() ?? "?");
        }
        private static void TestSbaWindow(GameCore core, Player p1, Player p2)
        {
            // ---- 段内夹具 ----
            CardData Vanilla(string id) => new CardData
            {
                ID = id, CardName = id, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            };

            // 亡语宿主（"角色"替身）：1/1 生物，OnDeath → 指定原子
            CardData DeathrattleData(string id, AtomicEffectType atom)
            {
                var data = new CardData
                {
                    ID = id, CardName = id, Supertype = Cardtype.Creature, Power = 1, Life = 1,
                };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_ONDEATH",
                    DisplayName = id,
                    TriggerTiming = (int)TriggerTiming.OnDeath,
                    TriggerLimitPerTurn = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(atom, value: 1) },
                });
                return data;
            }

            // 定速法术（BaseSpeed 声明卡面速度）：kinds 实例域收窄 + 选一/全取选择
            CardData SpeedSpellData(string id, int baseSpeed, AtomicEffectType atom, int value,
                List<int> kinds, CardCore.SelectionMode mode = CardCore.SelectionMode.Single)
            {
                var data = new CardData { ID = id, CardName = id, Supertype = Cardtype.Spell };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_ONPLAY",
                    DisplayName = id,
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    BaseSpeed = baseSpeed,
                    SelectionMode = (int)mode,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(atom, value: value, kinds: kinds) },
                });
                return data;
            }

            // AoE 法术：对全体敌人（kinds{2}=EnemyLivingUnit 对方生物+角色）value 伤
            CardData AoEData(string id, int value = 1)
            {
                var data = new CardData { ID = id, CardName = id, Supertype = Cardtype.Spell };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_ONPLAY",
                    DisplayName = "对全体敌人" + value + "伤",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    BaseSpeed = 1,
                    SelectionMode = (int)CardCore.SelectionMode.Whole,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.DealDamage, value: value, kinds: new List<int> { 2 }),
                    },
                });
                return data;
            }

            Card Spawn(Player owner, CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"SBA段合成入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            int Hand(Player p) => core.ZoneManager.GetCards(p, Zone.Hand).Count;

            // ---- T1 时点过滤单测（不上桌）----
            var stubSelf = new CardWrapper(Vanilla("VERIFY_SBA_T1_SELF"));
            var stubOther = new CardWrapper(Vanilla("VERIFY_SBA_T1_OTHER"));
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnOtherCreatureDeath) == typeof(CardDestroyEvent),
                   "SBA·T1 映射：OnOtherCreatureDeath → CardDestroyEvent");
            var regOnDeath = new RegisteredEffect
            {
                Effect = new EffectDefinition { Id = "VERIFY_SBA_T1_F", TriggerTiming = TriggerTiming.OnDeath },
                Source = stubSelf, Controller = p1,
            };
            Assert(TriggerPayloadFilter.Matches(TriggerTiming.OnDeath,
                       new CardDestroyEvent { DestroyedCard = stubSelf }, regOnDeath),
                   "SBA·T1 OnDeath 过滤：自己死 → 通过");
            Assert(!TriggerPayloadFilter.Matches(TriggerTiming.OnDeath,
                        new CardDestroyEvent { DestroyedCard = stubOther }, regOnDeath),
                   "SBA·T1 OnDeath 过滤：他人死 → 拒绝（default 跨火已修）");
            var regOtherDeath = new RegisteredEffect
            {
                Effect = new EffectDefinition { Id = "VERIFY_SBA_T1_G", TriggerTiming = TriggerTiming.OnOtherCreatureDeath },
                Source = stubSelf, Controller = p1,
            };
            Assert(TriggerPayloadFilter.Matches(TriggerTiming.OnOtherCreatureDeath,
                       new CardDestroyEvent { DestroyedCard = stubOther }, regOtherDeath),
                   "SBA·T1 OnOtherCreatureDeath 过滤：他人死 → 通过");
            Assert(!TriggerPayloadFilter.Matches(TriggerTiming.OnOtherCreatureDeath,
                        new CardDestroyEvent { DestroyedCard = stubSelf }, regOtherDeath),
                   "SBA·T1 OnOtherCreatureDeath 过滤：自己死 → 拒绝");

            // ---- T2 亡语真实管线（门禁豁免后：OnDeath 经 SBA 送墓真实触发）----
            // 2026-09-21 契约对齐：内容契约（2026-09-13）效果栏不可挂「有害原子+己方域」——
            // 原版 kinds={1} 打己方宿主被 converter 剔除成白板（宿主不死）。改为宿主在 p2 侧、
            // p1 以 kinds={2} 直伤对方宿主；仍测「1速直伤致死→SBA 真实送墓→亡语真实触发」。
            // 账目修正：原 h0 在注入前取，「打出-1+亡语+1=持平」在亡语未触发时才成立（反账）；
            // 现亡语抽牌归宿主持有者 p2，断言 p2 净 +1。
            EnsureMainPhase(core, p1);
            var banks2 = FillBanks(core, p1, p2);
            try
            {
                var host2 = Spawn(p2, DeathrattleData("VERIFY_SBA_T2HOST", AtomicEffectType.DrawCard));
                var bolt = InjectCard(core, p1,
                    SpeedSpellData("VERIFY_SBA_T2BOLT", 1, AtomicEffectType.DealDamage, 1, new List<int> { 2 }));
                int h2 = Hand(p2);
                Assert(PlayCardSync(core, p1, bolt, new List<Entity> { host2 }), "SBA·T2 直伤打出（打对方宿主）");
                Assert(!host2.IsAlive && core.ZoneManager.IsCardInZone(host2, p2, Zone.Graveyard),
                       "SBA·T2 门禁豁免后亡语宿主真实送墓（SBA 轮对 DrainStack 透明）");
                Assert(Hand(p2) == h2 + 1, "SBA·T2 OnDeath 亡语经真实管线结算（宿主方抽 +1）");
                RetireCards(core, p2, host2);
                RetireCards(core, p1, bolt);
            }
            finally { RestoreBanks(core, p1, p2, banks2); }

            // ---- T4a 速度2治疗在 SBA 窗口救回（核心新语义）----
            // P2 回合：P1 是非回合方（响应门槛=速度严格 >1）；P1 场上 3×1/1 + 亡语宿主，
            // P1 手持速度2治疗。AoE 结算后全场归 0 → SBA 入栈开窗 → 2 速治疗 LIFO 先结（解标死）
            // → SBA 到点重查：宿主已活（空转不送墓、亡语不触发）、三个无辜生物照旧送墓。
            EnsureMainPhase(core, p2);
            var banks4a = FillBanks(core, p1, p2);
            try
            {
                var m1 = Spawn(p1, Vanilla("VERIFY_SBA_M1"));
                var m2 = Spawn(p1, Vanilla("VERIFY_SBA_M2"));
                var m3 = Spawn(p1, Vanilla("VERIFY_SBA_M3"));
                var hero = Spawn(p1, DeathrattleData("VERIFY_SBA_HERO", AtomicEffectType.DeclareVictory));
                var heal2 = InjectCard(core, p1,
                    SpeedSpellData("VERIFY_SBA_HEAL2", 2, AtomicEffectType.Heal, 2, new List<int> { 1 }));
                var aoe = InjectCard(core, p2, AoEData("VERIFY_SBA_AOE"));

                Assert(GameActions.PlayCard(core, p2, aoe), "SBA·T4a P2 主阶段打出 AoE（对全体敌人1伤）");
                GameActions.PassPriority(core, p1);
                GameActions.PassPriority(core, p2); // 双 Pass → AoE 同步结算 → FinishResolution → SBA 入栈
                var top4a = core.StackEngine.Peek();
                Assert(top4a != null && top4a.IsSBA, "SBA·T4a AoE 结算后：SBA 伪对象在栈顶（速度1栈对象）");
                Assert(core.StackEngine.CurrentPriorityHolder == p2,
                       "SBA·T4a SBA 轮：回合方先持优先权");
                Assert(core.StackEngine.SpeedCounter.CurrentSpeed == 1,
                       "SBA·T4a 记速器被 SBA 抬到 1");
                Assert(!hero.IsAlive, "SBA·T4a 宿主伤害落血即标死（未送墓——等 SBA 到点）");

                GameActions.PassPriority(core, p2); // 回合方让权 → holder=p1
                bool healRespOk = GameActions.PlayCardInResponse(core, p1, heal2,
                    new List<Entity> { hero }, 0, out var healWhy);
                Assert(healRespOk, $"SBA·T4a 非回合方速度2治疗响应入栈（2 > 1 达标）——拒因:{healWhy} top={TopState(core)}");
                GameActions.DrainStack(core);       // LIFO：治疗先结（解标死）→ SBA 到点重查

                Assert(hero.IsAlive && hero.GetLife() > 0
                           && core.ZoneManager.IsCardInZone(hero, p1, Zone.Battlefield),
                       $"SBA·T4a 宿主被救回（治疗解标死，SBA 重查空转）——实际 alive={hero.IsAlive} " +
                       $"life={hero.GetLife()} zone={hero.GetZone()} top={TopState(core)}");
                Assert(!core.IsGameOver, $"SBA·T4a 亡语「对手获得胜利」未触发（无终局）——实际 gameOver={core.IsGameOver}");
                Assert(core.ZoneManager.IsCardInZone(m1, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(m2, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(m3, p1, Zone.Graveyard),
                       "SBA·T4a 三个无辜生物照旧送墓（SBA 批内各自结算）");
                RetireCards(core, p1, m1, m2, m3, hero, heal2, aoe);
            }
            finally { RestoreBanks(core, p1, p2, banks4a); }

            // ---- T4c 速度1拒连（非回合方 1 不 > 1）----
            EnsureMainPhase(core, p2);
            var banks4c = FillBanks(core, p1, p2);
            try
            {
                var m4c = Spawn(p1, Vanilla("VERIFY_SBA_T4C_M"));
                var hero4c = Spawn(p1, DeathrattleData("VERIFY_SBA_T4C_HERO", AtomicEffectType.DeclareVictory));
                // 速度1探针（2026-10-15 改版：法术施放速度=效果 BaseSpeed+底盘剩余提速=3−槽数）——
                // 双效果（2 槽）+ BaseSpeed 0 → 速度恰 1（旧「BaseSpeed 1+提速2=3」恒过门槛，测不出拒绝）
                var heal1Data = new CardData { ID = "VERIFY_SBA_HEAL1", CardName = "速1治疗", Supertype = Cardtype.Spell };
                for (int h = 0; h < 2; h++)
                    heal1Data.Effects.Add(new CardEffectData
                    {
                        Id = "VERIFY_SBA_HEAL1_" + h,
                        DisplayName = "速1治疗" + h,
                        TriggerTiming = (int)TriggerTiming.OnPlay,
                        BaseSpeed = 0,
                        SelectionMode = (int)CardCore.SelectionMode.Single,
                        AtomicEffects = new List<AtomicEffectEntry>
                            { AtomRefs.New(AtomicEffectType.Heal, value: 2, kinds: new List<int> { 1 }) },
                    });
                var heal1 = InjectCard(core, p1, heal1Data);
                var aoe4c = InjectCard(core, p2, AoEData("VERIFY_SBA_T4C_AOE"));

                Assert(GameActions.PlayCard(core, p2, aoe4c), "SBA·T4c P2 打出 AoE");
                GameActions.PassPriority(core, p1);
                GameActions.PassPriority(core, p2);
                                // 2026-11-05 口径：无人可响应时引擎自动双 Pass，SBA 窗口的「栈顶中间态」不再可观测——
                // 这里降级为诊断日志；本段真正的验收在下方最终态（拒绝/救回/送墓/终局）。
                if (core.StackEngine.Peek()?.IsSBA != true)
                    Debug.LogWarning($"[SBA] 双 Pass 后栈顶非 SBA（{TopState(core)}）——按自动 Pass 语义属正常");

                GameActions.PassPriority(core, p2); // holder → p1
                Assert(!GameActions.PlayCardInResponse(core, p1, heal1, new List<Entity> { hero4c }),
                       "SBA·T4c 非回合方速度1响应被拒（法术速度=3−槽数=1，1 不> 1——记速器已被 SBA 抬到 1）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Contains(heal1),
                       "SBA·T4c 治疗卡留在手中（声明失败退回手）");
                
                // 不排干收尾（避免第二场终局撞 PublishGameOverOnce 幂等闸门）：直接清场
                core.StackEngine.Clear();
                core.SBAEngine.ClearHistory();
                RetireCards(core, p1, m4c, hero4c, heal1, aoe4c);
            }
            finally { RestoreBanks(core, p1, p2, banks4c); }

            // ---- T4d 角色判负同样可救：AoE 打角色至 0 → SBA 窗口速度2治疗救回 → 不宣判不终局 ----
            EnsureMainPhase(core, p2);
            var banks4d = FillBanks(core, p1, p2);
            try
            {
                var m4d = Spawn(p1, Vanilla("VERIFY_SBA_T4D_M"));
                var heal2d = InjectCard(core, p1,
                    SpeedSpellData("VERIFY_SBA_T4D_HEAL", 2, AtomicEffectType.Heal, 2, new List<int> { 1, 2 }));
                // 显式费绕开推导：DealDamage 30 全取推导=红120，超地牌帽(9)在 CanAfford 第一道门被拒
                var aoe4dData = AoEData("VERIFY_SBA_T4D_AOE", 30);
                aoe4dData.Cost = CostOf((ManaType.Red, 1));
                // 2026-11-06 A1 定案：全取域=side×战场×生物——角色不进全取（不再命中角色）。
                // 「角色判负可救」语义改由显式「对方角色」指名伤害原子承载（30 伤归零 → SBA 窗口救回）。
                // 形态=单效果双原子（perAtom：一次施放两段都结算；拆两效果=两施放模式只能选一）；
                // 锚价钉死红1：双行 30 伤推导≈红210 超混合支付上限（红99+灰99=198），CanAfford 拒。
                aoe4dData.Effects[0].AnchorCostPinned = 1;
                aoe4dData.Effects[0].AnchorCost = new ElementCost();
                aoe4dData.Effects[0].AnchorCost[ManaType.Red] = 1;
                aoe4dData.Effects[0].AtomicEffects.Add(new AtomicEffectEntry
                {
                    refId = CardCore.Attribute.AtomicEffectTable.GetByEnumName(nameof(AtomicEffectType.DealDamage))?.HashId,
                    value = 30,
                    domain = "对方角色",
                    count = 1,
                });
                var aoe4d = InjectCard(core, p2, aoe4dData);
                // 前置修正：T4a/T4c 的 AoE(1) 已累计 chip 2 点（Life=28）——玩家落血不截断（引擎定案
                // 「Player 直接扣」，可负），AoE(30) 会打成 -2，治疗 +2 只回到 0 仍 ≤0 → 宣判。
                // 回满 30 保证 30−30=0、治疗→2 的「角色判负可救」语义被真正测到
                p1.Life = 30;

                Assert(GameActions.PlayCard(core, p2, aoe4d),
                       "SBA·T4d P2 打出 AoE(30)+角色指名（角色一并归零——全取已剔角色，指名行补位）—门禁:" + PlayGateProbe(core, p2, aoe4d));
                GameActions.PassPriority(core, p1);
                GameActions.PassPriority(core, p2);
                                // 2026-11-05 口径：无人可响应时引擎自动双 Pass，SBA 窗口的「栈顶中间态」不再可观测——
                // 这里降级为诊断日志；本段真正的验收在下方最终态（拒绝/救回/送墓/终局）。
                if (core.StackEngine.Peek()?.IsSBA != true)
                    Debug.LogWarning($"[SBA] 双 Pass 后栈顶非 SBA（{TopState(core)}）——按自动 Pass 语义属正常");

                GameActions.PassPriority(core, p2); // holder → p1
                bool resp4dOk = GameActions.PlayCardInResponse(core, p1, heal2d, new List<Entity> { p1 }, 0, out var resp4dWhy);
                Assert(resp4dOk,
                       $"SBA·T4d 速度2治疗响应入栈（目标=自己的角色）——拒因:{resp4dWhy} top={TopState(core)} " +
                       $"gameOver={core.IsGameOver} life={p1.Life}");
                GameActions.DrainStack(core); // 治疗先结（角色 Life 0→2）→ SBA 到点重查：ZeroLife 空转、生物照送

                Assert(p1.Life == 2, $"SBA·T4d 角色被救回（Life=2，宣判未发生）——实际 life={p1.Life} gameOver={core.IsGameOver}");
                Assert(!core.IsGameOver, "SBA·T4d 无终局（角色亡语未宣告）");
                Assert(core.ZoneManager.IsCardInZone(m4d, p1, Zone.Graveyard),
                       $"SBA·T4d 无辜生物照旧送墓——实际 zone={m4d.GetZone()} alive={m4d.IsAlive}");
                RetireCards(core, p1, heal2d, aoe4d);
            }
            finally { RestoreBanks(core, p1, p2, banks4d); }

            // ---- T5 角色亡语宣告终局（判负效果化，本段最后）----
            // AoE(30) 连角色一并归零 → SBA 批 [ZeroLife, ZeroToughness×4] 到点：
            // 先宣判（RoleDeathEvent → 内置角色亡语入队）→ 四尸送墓（宿主卡亡语入队）→
            // 亡语轮：角色亡语 DeclareVictory 宣告 P2 胜（宣判即终局，宿主卡亡语同轮幂等空转）。
            EnsureMainPhase(core, p2);
            var banks3 = FillBanks(core, p1, p2);
            GameOverEvent goEvt = null;
            void OnGameOver(GameOverEvent e) => goEvt = e;
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
            try
            {
                var n1 = Spawn(p1, Vanilla("VERIFY_SBA_T5_M1"));
                var n2 = Spawn(p1, Vanilla("VERIFY_SBA_T5_M2"));
                var n3 = Spawn(p1, Vanilla("VERIFY_SBA_T5_M3"));
                var hero3 = Spawn(p1, DeathrattleData("VERIFY_SBA_T5_HERO", AtomicEffectType.DeclareVictory));
                var aoe3Data = AoEData("VERIFY_SBA_T5_AOE", 30);
                aoe3Data.Cost = CostOf((ManaType.Red, 1)); // 同 T4d：显式费绕开推导（红90 超帽）
                var aoe3 = InjectCard(core, p2, aoe3Data);

                Assert(GameActions.PlayCard(core, p2, aoe3),
                       "SBA·T5 P2 打出 AoE(30)（角色+三生物+宿主全灭）—门禁:" + PlayGateProbe(core, p2, aoe3));
                GameActions.PassPriority(core, p1);
                GameActions.PassPriority(core, p2);
                                // 2026-11-05 口径：无人可响应时引擎自动双 Pass，SBA 窗口的「栈顶中间态」不再可观测——
                // 这里降级为诊断日志；本段真正的验收在下方最终态（拒绝/救回/送墓/终局）。
                if (core.StackEngine.Peek()?.IsSBA != true)
                    Debug.LogWarning($"[SBA] 双 Pass 后栈顶非 SBA（{TopState(core)}）——按自动 Pass 语义属正常");

                GameActions.DrainStack(core); // SBA 轮 → 宣判+送墓 → 亡语轮（角色亡语先宣）→ 终局

                Assert(core.ZoneManager.IsCardInZone(n1, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(n2, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(n3, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(hero3, p1, Zone.Graveyard),
                       $"SBA·T5 四具尸体全部送墓（三生物 + 亡语宿主）——实际 n1={n1.GetZone()} n2={n2.GetZone()} " +
                       $"n3={n3.GetZone()} hero={hero3.GetZone()} gameOver={core.IsGameOver}");
                Assert(core.IsGameOver, "SBA·T5 游戏终局");
                Assert(goEvt != null && goEvt.Winner == p2 && goEvt.Reason == GameOverReason.EffectVictory,
                       $"SBA·T5 角色亡语「对手获得胜利」宣告 P2 胜（EffectVictory，判负效果化）——实际 " +
                       $"winner={goEvt?.Winner?.Name} reason={goEvt?.Reason}");
            }
            finally
            {
                EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
                RestoreBanks(core, p1, p2, banks3);
            }
        }

        /// <summary>
        /// 三类卡型定案回归（2026-10-02，与 TideServer verify V9 段同口径）：
        /// ①法术全额计价——延迟折 f=d(Ĉ) 已随 2026-10-31 阶梯减价删除整链退役（旧「法术同折/法术豁免」
        ///   两代口径均废；S+E+K 全额，三类卡型同价式）；
        /// ②域数据——伤害族开放无生命域 3,4（法术伤害可消耐久）；冻结域 2026-11-02 改版收紧对方 2；
        /// ③结界耐久战斗侧——入场初始化耐久、受击（战斗/法术同管线）恒 -1 不落血、
        ///   归零直送墓（Smashed 直毁不经死亡决策表）、SBA 生命管线不扫无生命单位；
        /// ④攻击目标显式化——结界=合法攻击目标；非战场卡（FieldZone 英雄技能卡）不可被攻击。
        /// 自开新局（合成卡组），不依赖卡表数据。
        /// </summary>
        private static void TestThreeCardTypes(GameCore core, Player p1, Player p2)
        {
            // ---- ① 法术/生物全额计价（纯函数；2026-10-22 恒推导定案：档位=推导总额所在档 Ĉ，不经存档 costList）----
            CardCore.CardCostResult Probe(Cardtype type, int dmg)
            {
                var d = new CardData { ID = $"TT_probe_{type}_{dmg}", CardName = "三类定案计价探针", Supertype = type };
                // 2026-11-06 opt-in：生物探针补攻守资格=恢复改版前计价口径（无资格免费属性底抬升、
                // 2/2 身材零费 → D 漂移）；法术探针不带（本断言锚生物全额档）
                if (type == Cardtype.Creature) { d.AttackAbility = true; d.GuardAbility = true; d.Power = 2; d.Life = 2; }
                d.Effects.Add(new CardEffectData
                {
                    Id = "TT_probe_onplay",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    // 档位由内容自决：E=红 dmg×1（登场=单次档 ×1）→ Ĉ=round(E[+S]) 所在档
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: dmg) },
                });
                return CardCore.CardCostService.Derive(d);
            }
            var spellCheap = Probe(Cardtype.Spell, 1);   // Ĉ=E1→档1
            var spell9 = Probe(Cardtype.Spell, 9);       // Ĉ=E9→档9
            var creature9 = Probe(Cardtype.Creature, 9); // Ĉ=S2+E9=11→档帽后 10
            Assert(spellCheap.DerivedTotal == 1, $"三类定案：法术低档全额 D(1伤)={spellCheap.DerivedTotal}=1");
            Assert(spell9.DerivedTotal == 9,
                   $"三类定案：法术高档全额 D(9伤)={spell9.DerivedTotal}=9（2026-10-31 阶梯减价删除——不再 ×d(Ĉ) 打折）");
            Assert(creature9.DerivedTotal == 11 && creature9.DerivedTier == 10,
                   $"三类定案：生物全额 D(2/2+9伤)={creature9.DerivedTotal}=11（S2 计入 Ĉ；档帽=LandCurveCap 地牌封顶 2026-11-03 起单源；实际 D={creature9.DerivedTotal} Ĉ={creature9.DerivedTier}）");
            Assert(spell9.DerivedTotal > spellCheap.DerivedTotal,
                   $"三类定案：内容决定价 D(9伤)={spell9.DerivedTotal} > D(1伤)={spellCheap.DerivedTotal}");

            // ---- ② 域数据（表级） ----
            void AssertKinds(AtomicEffectType type, int[] expect, string what)
            {
                var row = CardCore.Attribute.AtomicEffectTable.GetByType(type);
                Assert(row != null, $"{what}：表行存在");
                if (row == null) return;
                var kinds = row.GetTargetKindList().OrderBy(x => x).ToArray();
                Assert(kinds.SequenceEqual(expect.OrderBy(x => x)),
                       $"{what}：TargetKinds=[{string.Join(",", kinds)}]");
            }
            AssertKinds(AtomicEffectType.DealDamage, new[] { 1, 2, 3, 4 }, "伤害族开放无生命域·造成伤害");
            AssertKinds(AtomicEffectType.Freeze, new[] { 1, 2 },
                "冻结域 kinds={1,2}（对方元素池位已删——B1 定案：冻结无元素池）；域=Domain 列 双方战场:生物");
            var freezeRowB1 = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Freeze);
            Assert(CardCore.TargetAxisRules.TryParseDomain(freezeRowB1?.Domain, out var freezeAxis)
                   && freezeAxis.Zh == "双方战场:生物",
                   "冻结 Domain=双方战场:生物（契约 §6：无元素池——历史易错点写死）");

            // ---- ③④ 结界耐久全流程（自开新局，合成卡组） ----
            var filler = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"TT_fill_{i}", CardName = "三类填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            core.InitGame(CardLoader.BuildDeck(filler, 2), CardLoader.BuildDeck(filler, 2));
            p1 = core.Player1;
            p2 = core.Player2;
            GameActions.SkipElementPool(core, p1);

            var ench = InjectCard(core, p1, new CardData
            {
                ID = "TT_ench", CardName = "耐久结界", Supertype = Cardtype.Enchantment, Durability = 3,
            });
            // 碑族0费改版（2026-11-04）：0 槽 → 免费点 3 → 免费耐久底 6 ≥ 印刷 3 → 0 费打出（免备元素）
            core.ElementPool.GetPool(p1).GlobalTurnIndex = 9;
            Assert(GameActions.PlayCard(core, p1, ench), "结界0费打出（免费耐久底6≥印刷3——2026-11-04 碑族0费改版）");
            GameActions.DrainStack(core);
            Assert((int)core.ElementPool.GetPool(p1).TempElements[ManaType.Gray] == 0,
                   "0 费结界无扣费（旧耐久账单随免费耐久底改版退役）");
            Assert(ench.GetZone() == Zone.Battlefield, "结界两步式：发动区→进入战场");
            // 2026-11-04 碑族0费改版：入场耐久=印刷值（免费点折底抵卡价，旧 3+3=6 堆叠退役）
            Assert(ench.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 3,
                   "结界入场初始化耐久=3（印刷值——CardPutToBattlefieldEvent 驱动）");
            core.SBAEngine.ExecuteAll();
            Assert(ench.IsAlive && ench.GetZone() == Zone.Battlefield,
                   "SBA 不扫无生命单位（旧「结界被当 0 防御送墓」bug 回归锚）");

            int lifeBefore = core.LayerEngine.CalculateToughness(ench); // 公开口径读有效生命（internal _life 编辑器程序集不可见）
            int dealt = CardCore.Attribute.KeywordRules.ApplyDamage(p2, ench, 2, isCombat: true);
            Assert(dealt == 2, $"战斗伤害对结界全额返回（实际 {dealt}——2026-10-16 撤钳 1）");
            Assert(ench.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 1,
                   "2 点战斗伤害全额扣 2 耐久（3→1）");
            Assert(core.LayerEngine.CalculateToughness(ench) == lifeBefore, "结界受击不落血（绕过生命管线）");

            CardCore.DestroyReason? reason = null;
            System.Action<CardCore.CardDestroyEvent> onDest = e => { if (e.DestroyedCard == ench) reason = e.Reason; };
            EventManager.Instance.Subscribe(onDest);
            try
            {
                // 剩 1 耐久吃 3 伤：法术伤害同管线全额扣（超扣按剩余钳 0）→ 直毁
                CardCore.Attribute.KeywordRules.ApplyDamage(p1, ench, 3, isCombat: false);
                Assert(ench.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 0,
                       "法术伤害同耐久管线全额扣（1−3 超扣钳 0）");
                Assert(ench.GetZone() == Zone.Graveyard, "结界耐久归零直送墓");
                Assert(reason == CardCore.DestroyReason.Smashed,
                       $"死因=Smashed 无生命直毁（实际 {reason}），不经死亡决策表");
            }
            finally { EventManager.Instance.Unsubscribe(onDest); }

            var attacker = new CardWrapper(new CardData
            {
                ID = "TT_atk", CardName = "三类攻击者", Supertype = Cardtype.Creature, Power = 2, Life = 2,
            });
            attacker.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(attacker, p2), "攻击者入场");
            attacker.Untap();
            var ench2 = new CardWrapper(new CardData
            {
                ID = "TT_ench2", CardName = "耐久结界2", Supertype = Cardtype.Enchantment, Durability = 2,
            });
            ench2.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(ench2, p1), "第二张结界入场");
            Assert(core.CombatSystem.CanAttackTarget(attacker, ench2, p2), "结界=合法攻击目标");
            foreach (var fc in core.ZoneManager.GetCards(p1, Zone.FieldZone))
                Assert(!core.CombatSystem.CanAttackTarget(attacker, fc, p2), "非战场卡（技能卡）不可被攻击");

            var victim = new CardWrapper(new CardData
            {
                ID = "TT_victim", CardName = "三类受害者", Supertype = Cardtype.Creature, Power = 1, Life = 1,
            });
            victim.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(victim, p1), "受害者入场");
            CardCore.Attribute.KeywordRules.ApplyDamage(attacker, victim, 1, isCombat: true);
            Assert(!victim.IsAlive, "生物 1 伤致死：标死路径不变（回归锚）");
            core.SBAEngine.ExecuteAll();
            Assert(victim.GetZone() == Zone.Graveyard, "生物死亡经 SBA 送墓（回归锚）");

            // ---- ⑤ 主动效果计价口径（2026-10-02 定案，与 TideServer V9.d 同口径） ----
            var actCard = new CardData { ID = "TT_act", CardName = "主动探针", Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            actCard.Effects.Add(new CardEffectData
            {
                Id = "TT_act_ab",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 2) },
            });
            Assert(CardCore.CardCostService.Derive(actCard).EAnchor == 0,
                   "主动效果：主动不进 E 桶（锚价运行时现付，2026-09-08 定案回归锚）");
            var mixCard = new CardData { ID = "TT_mix", CardName = "混合探针", Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            mixCard.Effects.Add(new CardEffectData
            {
                Id = "TT_mix_act",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 2) },
            });
            mixCard.Effects.Add(new CardEffectData
            {
                Id = "TT_mix_onplay",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(CardCore.CostDerivationService.CountEffectSlots(mixCard) == 2,
                   "主动效果：主动照常计效果槽（占用维持定案——AI 每卡原子数上限的可见性锚）");

            Assert(!ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantLifesteal)
                   && !ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantDepravity),
                   "主动效果：关键词行已改赋予己方战场生物——不再判「自指关键词型」（2026-11-07 特判）");
            var kwAct = new CardEffectData
            {
                Id = "TT_kw_act",
                TriggerTiming = (int)TriggerTiming.Activate_Instant,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.GrantLifesteal),
                    AtomRefs.New(AtomicEffectType.DealDamage, value: 1),
                },
            };
            var kwDef = CardEffectConverter.ConvertOne(kwAct, "TT_src");
            Assert(kwDef.Effects.Any(a => a.Type == AtomicEffectType.GrantLifesteal)
                   && kwDef.Effects.Any(a => a.Type == AtomicEffectType.DealDamage),
                   "主动效果：关键词赋予原子保留（赋予通道可作主动；旧「自指关键词型剔除」随特判退役）");
            var grantDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "TT_grant_act",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.GrantDepravity, value: 1) },
            }, "TT_src");
            Assert(grantDef.Effects.Any(a => a.Type == AtomicEffectType.GrantDepravity),
                   "主动效果：赋予型 Grant 做主动放行（赋予指示物可以；正例=反疗，微缩/放大已删 2026-10-10）");

            // ---- ⑥ 发动方式钉死（2026-10-02 定案，与 TideServer V9.e 同口径） ----
            var volBad = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "TT_vol_bad",
                ActivationType = (int)EffectActivationType.Voluntary,
                TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "TT_src");
            Assert(volBad.TriggerTiming == TriggerTiming.Activate_Active && !volBad.ElementCostPrepaid,
                   "发动方式钉死：主动+触发时机 → Activate_Active 且按主动计费（锚价现付）");
            var volInstant = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "TT_vol_inst",
                ActivationType = (int)EffectActivationType.Voluntary,
                TriggerTiming = (int)TriggerTiming.Activate_Instant,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "TT_src");
            Assert(volInstant.TriggerTiming == TriggerTiming.Activate_Instant,
                   "发动方式钉死：主动+Activate_Instant 合法组合原样保留");
            var actMand = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "TT_act_mand",
                ActivationType = (int)EffectActivationType.Mandatory,
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "TT_src");
            Assert(actMand.ActivationType == EffectActivationType.Voluntary,
                   "发动方式钉死：主动+强制 → 覆写回主动（对称校验回归锚）");
        }

        /// <summary>原子三阶段统一路由（P0.2）：每阶段恰发布一次 + OnAtomicEffectResolution 触发式可达。</summary>
        private static void TestAtomicPhaseRouting(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);

            var phaseCounts = new Dictionary<CardCore.AtomicEffectPhase, int>();
            // 只数被测 DealDamage 原子的相位：观察者 w 的触发效果（DrawCard）结算时
            // 同样合法发布三相位，不能进「恰一次」的账（按原子类型过滤）
            void OnPhase(AtomicEffectPhaseEvent e)
            {
                if (e.EffectInstance == null || e.EffectInstance.Type != AtomicEffectType.DealDamage) return;
                phaseCounts.TryGetValue(e.Phase, out var c);
                phaseCounts[e.Phase] = c + 1;
            }
            EventManager.Instance.Subscribe<AtomicEffectPhaseEvent>(OnPhase);
            try
            {
                var w = InjectCard(core, p1, TrigData("VERIFY_AR_RES", TriggerTiming.OnAtomicEffectResolution));
                Crumb("AR: w injected");
                Assert(GameActions.PlayCard(core, p1, w), "原子结算观察者打出");
                Crumb("AR: w played");
                GameActions.DrainStack(core);
                Crumb("AR: drain#1 done");
                // w 入场本身不发原子三阶段；清零后测一次原子执行
                phaseCounts.Clear();
                int Hand() => core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                int h0 = Hand();

                // 经 executor 真路径（直调 EffectHandlerRegistry.ExecuteEffect 不发原子三阶段——
                // PublishPhase 在执行器的扁平/步骤循环里，注册表直连只是 handler 分发）
                core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                {
                    Definition = new EffectDefinition
                    {
                        Id = "VERIFY_AR_ATOM",
                        ElementCostPrepaid = true, // 只测相位路由，不测付费
                        Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 0 } },
                    },
                    Source = p1, Controller = p1,
                    Targets = new List<Entity> { p2 },
                }, skipElementCost: true).Forget();
                Crumb("AR: executor fired");
                GameActions.DrainStack(core);
                Crumb("AR: drain#2 done");

                Assert(phaseCounts.TryGetValue(CardCore.AtomicEffectPhase.Activation, out var a) && a == 1,
                       "原子三阶段 Activation 恰一次（无路由双发）");
                Assert(phaseCounts.TryGetValue(CardCore.AtomicEffectPhase.StartApplying, out var s) && s == 1,
                       "原子三阶段 StartApplying 恰一次");
                Assert(phaseCounts.TryGetValue(CardCore.AtomicEffectPhase.ResolutionComplete, out var r) && r == 1,
                       "原子三阶段 ResolutionComplete 恰一次");
                Assert(Hand() == h0 + 1, "OnAtomicEffectResolution 触发式经统一路由可达（净 +1）");

                RetireCards(core, p1, w);
            }
            finally
            {
                EventManager.Instance.Unsubscribe<AtomicEffectPhaseEvent>(OnPhase);
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>SummonToken 原子（P1）：三落区 / 实例 ID / 事件 / 满场 / 落区费用三档。</summary>
        private static void TestSummonToken(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            var template = new CardData
            {
                ID = "VERIFY_TOKEN_TPL", CardName = "验证衍生物", Supertype = Cardtype.Creature, Power = 2, Life = 2,
            };
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = id => id == template.ID ? template : null;

            var tokens = new List<TokenCreatedEvent>();
            var enters = new List<CardPutToBattlefieldEvent>();
            var hands = new List<CardEnterHandEvent>();
            void OnToken(TokenCreatedEvent e) => tokens.Add(e);
            void OnEnter(CardPutToBattlefieldEvent e) => enters.Add(e);
            void OnHand(CardEnterHandEvent e) => hands.Add(e);
            EventManager.Instance.Subscribe<TokenCreatedEvent>(OnToken);
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnter);
            EventManager.Instance.Subscribe<CardEnterHandEvent>(OnHand);
            try
            {
                int Bf() => core.ZoneManager.GetCards(p1, Zone.Battlefield).Count;
                int Hand() => core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                int Deck() => core.ZoneManager.GetCards(p1, Zone.Deck).Count;

                // ① 落战场 ×2：进场事件 Source=TokenSpawned、实例 ID 唯一
                int bf0 = Bf();
                SummonTokenHandlerUtil.Run(core, p1, Zone.Battlefield, 2);
                GameActions.DrainStack(core);
                Assert(Bf() == bf0 + 2, "落战场：+2 个 token 占格");
                Assert(tokens.Count == 2 && tokens.All(t => t.Card != null && t.DropZone == Zone.Battlefield),
                       "TokenCreatedEvent ×2（带实例与落区）");
                Assert(enters.Count >= 2 && enters.TakeLast(2).All(e => e.Source == EnterSource.TokenSpawned),
                       "token 进场事件 Source=TokenSpawned");
                var ids = tokens.Select(t => t.Card.ID).ToList();
                Assert(ids.All(id => id.StartsWith("VERIFY_TOKEN_TPL#")) && ids.Distinct().Count() == 2,
                       $"实例 ID = 模板#序号 且唯一（{string.Join(",", ids)}）");

                // 2026-10-09 清理：落手牌/落牌组两段删除——衍生物落区已写死=战场
                //（2026-10-05 效果通用属性定案），手牌/牌库落区路径退役

                // ④ 满场：入墓 + 失败事件
                var filler = new List<Card>();
                while (core.ZoneManager.HasBattlefieldSpace(p1))
                {
                    var c = new CardWrapper(new CardData { ID = "VERIFY_TOKEN_FILL", CardName = "占位", Supertype = Cardtype.Creature, Power = 1, Life = 1 });
                    core.ZoneManager.TryAddToBattlefield(c, p1, EnterSource.SummonedByEffect);
                    filler.Add(c);
                }
                var failed = new List<CardActivationFailedEvent>();
                void OnFail(CardActivationFailedEvent e) => failed.Add(e);
                EventManager.Instance.Subscribe<CardActivationFailedEvent>(OnFail);
                int gy0 = core.ZoneManager.GetCards(p1, Zone.Graveyard).Count;
                SummonTokenHandlerUtil.Run(core, p1, Zone.Battlefield, 1);
                EventManager.Instance.Unsubscribe<CardActivationFailedEvent>(OnFail);
                Assert(failed.Count == 1 && failed[0].Reason == "BattlefieldFull", "满场：token 入墓 + 失败事件");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Count == gy0 + 1, "满场 token 落墓");
                RetireCards(core, p1, filler.ToArray());

                // ⑤ 计价随模板（2026-10-31 动态费用改版：总价=本体锚价（表行·恒收）＋模板单价×数量，
                //    单价=模板内容全价（S+K+E，不乘 d(C)）−遗忘补偿按色落位；模板不可解析=只剩本体锚价）。
                //    锚价/单价随表漂移不锚——只钉结构（锚价+线性）与可解析性）
                template.Cost = CardCore.ElementCost.FromValue(ManaType.Red, 3);
                CardCore.ElementCost CostOf(int count, string tplId)
                {
                    var def = new EffectDefinition
                    {
                        Id = "VERIFY_TOKEN_COST",
                        // 手构 def 显式单次（第十四批定案）——触发档不误乘
                        TriggerLimitPerTurn = 1,
                        Effects = new List<AtomicEffectInstance>
                        {
                            new AtomicEffectInstance { Type = AtomicEffectType.SummonToken, Value = count, StringValue = tplId }
                        },
                    };
                    return CostDerivationService.DeriveElementCosts(def);
                }
                var c1 = CostOf(1, template.ID);
                var c2 = CostOf(2, template.ID);
                var c10 = CostOf(10, template.ID);
                var cMiss = CostOf(2, "VERIFY_TOKEN_MISSING");
                float summonAnchor = CardCore.Attribute.AtomicEffectTable.GetByEnumName("SummonToken").TotalUnitCost;
                // 2026-10-31 动态费用改版：总价=本体锚价（表行·恒收）＋单价×数量——模板段仍线性
                //（c(n)=A+U·n → c10−c2=8U、c2−c1=U），锚价/单价随表漂移不锚，只钉结构与线性
                Assert(c1.Total > summonAnchor && c2.Total > c1.Total
                       && System.Math.Abs((c10.Total - c2.Total) - (c2.Total - c1.Total) * 8f) < 0.05f,
                       $"计价=本体锚价＋模板单价×数量：A={summonAnchor:0.##}、×1={c1.Total:0.##}、×2={c2.Total:0.##}、×10={c10.Total:0.##}（模板段线性·随表漂移不锚）");
                Assert(System.Math.Abs(cMiss.Total - summonAnchor) < 0.001f,
                       $"模板不可解析=只剩本体锚价（{summonAnchor:0.##}·构筑期拦未绑/运行时兜底同口径）");
            }
            finally
            {
                EventManager.Instance.Unsubscribe<TokenCreatedEvent>(OnToken);
                EventManager.Instance.Unsubscribe<CardPutToBattlefieldEvent>(OnEnter);
                EventManager.Instance.Unsubscribe<CardEnterHandEvent>(OnHand);
            }
        }

        /// <summary>SummonToken 直连执行辅助（验证段内使用）。</summary>
        private static class SummonTokenHandlerUtil
        {
            public static void Run(GameCore core, Player caster, Zone dropZone, int count)
            {
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                    new AtomicEffectInstance
                    {
                        Type = AtomicEffectType.SummonToken, Value = count,
                        StringValue = "VERIFY_TOKEN_TPL",
                    },
                    new EffectExecutionContext
                    {
                        Source = caster, Controller = caster,
                        SummonDropZone = dropZone, // 2026-09-10：落区上移组合层（经 context 下发）
                        Targets = new List<Entity>(),
                        ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                    });
            }
        }

        /// <summary>对局史计数服务（P2a）：CardsPlayed/Damage 双向统计、Custom 条件、回合/本局双 scope。</summary>
        private static void TestMatchStats(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var stats = core.MatchStats;
            Assert(stats != null, "MatchStatsService 已注册（组合根）");

            var banks = FillBanks(core, p1, p2);
            try
            {
                int played0 = stats.GetStat(p1, MatchStatsService.CardsPlayed);
                var spell = InjectCard(core, p1, new CardData
                {
                    ID = "VERIFY_MS_SPELL", CardName = "计数法术", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Gray, 1)),
                });
                Assert(GameActions.PlayCard(core, p1, spell), "打出计数法术");
                GameActions.DrainStack(core);
                Assert(stats.GetStat(p1, MatchStatsService.CardsPlayed) == played0 + 1,
                       "CardsPlayed 使用宣言计数 +1");
                Assert(stats.GetStat(p1, MatchStatsService.CardsPlayed, StatScope.ThisTurn) >= 1,
                       "ThisTurn scope 可查");

                // DamageDealt / DamageTaken 双向
                int dd0 = stats.GetStat(p1, MatchStatsService.DamageDealt);
                int dt0 = stats.GetStat(p2, MatchStatsService.DamageTaken);
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                    new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 3 },
                    new EffectExecutionContext
                    {
                        Source = p1, Controller = p1, Targets = new List<Entity> { p2 },
                        ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                    });
                Assert(stats.GetStat(p1, MatchStatsService.DamageDealt) == dd0 + 3, "DamageDealt +3");
                Assert(stats.GetStat(p2, MatchStatsService.DamageTaken) == dt0 + 3, "DamageTaken +3");

                // Custom 条件：StringValue=statId、Value=阈值
                var checker = new ConditionChecker(core.ZoneManager);
                var ctx = new ConditionCheckContext { Activator = p1, ZoneManager = core.ZoneManager };
                int playedNow = stats.GetStat(p1, MatchStatsService.CardsPlayed);
                Assert(checker.Check(new ActivationCondition
                {
                    Type = ConditionType.Custom,
                    StringValue = MatchStatsService.CardsPlayed,
                    Value = playedNow,
                }, ctx), "Custom 条件：CardsPlayed>=当前值 → 满足");
                Assert(!checker.Check(new ActivationCondition
                {
                    Type = ConditionType.Custom,
                    StringValue = MatchStatsService.CardsPlayed,
                    Value = playedNow + 99,
                }, ctx), "Custom 条件：阈值不可达 → 不满足");

                // 回合 scope：跨回合 ThisTurn 清零、ThisGame 保留
                int gamePlayed = stats.GetStat(p1, MatchStatsService.CardsPlayed, StatScope.ThisGame);
                EndTurnPumped(core, p1);
                EnsureMainPhase(core, core.TurnEngine.TurnPlayer);
                Assert(stats.GetStat(p1, MatchStatsService.CardsPlayed, StatScope.ThisTurn) == 0,
                       "跨回合 ThisTurn 清零");
                Assert(stats.GetStat(p1, MatchStatsService.CardsPlayed, StatScope.ThisGame) == gamePlayed,
                       "ThisGame 跨回合保留");
            }
            finally
            {
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>计价锚点合成卡（不入对局，只喂 Derive）。</summary>
        private static CardData MakeCostCard(Cardtype type, int? power, int? life, params CardEffectData[] effects)
        {
            // 2026-11-06 opt-in 改版：计价锚按「攻/守资格齐备」旧默认口径造卡（底=攻1+守1+首槽）——
            // 无攻/无守特例在用例处显式改字段（wall/ng2/noAtk 三处）。
            var d = new CardData
            {
                ID = "VERIFY_COST", CardName = "计价锚点", Supertype = type, Power = power, Life = life,
                AttackAbility = type == Cardtype.Creature,
                GuardAbility = type == Cardtype.Creature,
            };
            d.Effects.AddRange(effects);
            return d;
        }

        /// <summary>单原子效果（站场类时点——挂载判定只看卡超类，时点不影响计价）。
        /// duration：DurationType 值，默认 0=Once（2026-09-10 持续上移——档位由数据声明决定）。</summary>
        private static CardEffectData MakeEffect(string atomType, int value, int duration = 0)
        {
            var e = new CardEffectData { Id = "VERIFY_COST_EFF", TriggerTiming = (int)TriggerTiming.OnDeath, Duration = duration };
            e.AtomicEffects = new List<AtomicEffectEntry> { Atom(atomType, value) };
            return e;
        }

        private static int DeriveTotal(CardData card) => CardCostService.Derive(card).DerivedTotal;

    }
}
