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
    /// <summary>
    /// 端到端验证：读配置 → 组卡 → 真实对局中打出并结算。
    /// 覆盖法术（火球术 DealDamage+DrawCard）与生物（古树守卫，配置驱动血量、可被伤害效果击杀）。
    /// 棋盘层（TestBoard）：布局常量 → 六边形数学 → 确定性重建（重连语义）→
    /// 发动区流转 → 容量闸门 → _zone 维护回归。
    /// </summary>
    public static class CardPipelineVerifier
    {
        private static int _pass;
        private static int _fail;

        [MenuItem("Tools/端到端验证")]
        public static void RunVerification()
        {
            _pass = 0;
            _fail = 0;

            // 测试卡配置缺失时不再整体早退：目录/预言等不依赖卡表的段落照常验证
            //（卡表四段——地牌经济/法术/生物/棋盘——需要真实卡数据，缺失时跳过）
            var cardsData = new List<CardData>();
            string jsonPath = Path.Combine(Application.dataPath, "Configs/TestDecks/TestCreatureCards.json");
            if (File.Exists(jsonPath))
            {
                cardsData = CardLoader.LoadCardsFromText(File.ReadAllText(jsonPath));
                Debug.Log($"[Verify] 加载卡牌数据 {cardsData.Count} 张");
            }
            else
            {
                Debug.LogWarning($"[Verify] 找不到测试卡配置（跳过依赖卡表的段落）: {jsonPath}");
            }

            var core = GameCore.Instance;
            // 构筑规则（定案）：卡组不重复（×1）；测试主卡组只带 1 张仪式（三相），
            // 其余 6 张仪式卡仅供 TestRituals / TestNewRituals 注入驱动
            var deckData = new List<CardData>();
            if (cardsData.Count > 0)
            {
                deckData.AddRange(cardsData.Where(c => !RitualSystem.IsRitual(new CardWrapper(c))));
                var trinity = cardsData.FirstOrDefault(c => c.ID == "RITUAL_TRINITY_001");
                if (trinity != null) deckData.Add(trinity);
            }
            var deck1 = deckData.Count > 0 ? CardLoader.BuildDeck(deckData, 1) : new List<Card>();
            var deck2 = deckData.Count > 0 ? CardLoader.BuildDeck(deckData, 1) : new List<Card>();
            core.InitGame(deck1, deck2);

            var p1 = core.Player1;
            var p2 = core.Player2;

            if (cardsData.Count > 0)
            {
                // 仪式开局入手断言（须在 TestLandEconomy 消耗手牌之前）
                TestRitualOpeningHand(core, cardsData);

                // 统一计价：全表规则一巡检（声明档位 + 代价抵扣足够）。
                // 仪式豁免：0费说明书卡（保持空 Cost 打出免费），效果费不参抵扣校验。
                var offenders = cardsData
                    .Where(c => !RitualSystem.IsRitual(new CardWrapper(c)) && !CardCostService.Derive(c).Conformant)
                    .ToList();
                Assert(offenders.Count == 0,
                       $"规则一巡检：全表 {cardsData.Count} 张（仪式除外）代价抵扣足够（不符 {offenders.Count}：{string.Join(",", offenders.Select(o => o.ID))}）");

                // 准备阶段 → 主阶段
                GameActions.SkipElementPool(core, p1);

                // 回合开始接线（2.1）：InitGame→StartGame→StartNewTurn 发布 TurnStartEvent，
                // GameCore 订阅后应已重置栈优先权持有者
                Assert(core.StackEngine.CurrentPriorityHolder != null,
                       $"回合开始接线生效（优先权持有者非空：{core.StackEngine.CurrentPriorityHolder?.Name}）");

                // 地牌经济（新资源模型）：上限曲线 / 补地牌 / 手动产出 / 费用门槛 / 结束产灰 / 台账
                TestLandEconomy(core, p1, p2, cardsData);

                TestSpell(core, p1, p2, cardsData);
                TestCreature(core, p1, cardsData);
                TestSpellBranch(core, p1, p2, cardsData);
                TestSpellModal(core, p1, p2, cardsData);
                TestBoard(core, p1, p2, cardsData);
                TestLinkAura(core, p1, p2, cardsData);
            }

            // 分支条件目录 + 信息族（宣言/预言）引擎流：不依赖卡表（合成卡驱动）
            TestCostAnchors();
            TestBranchCatalog();
            TestInformationFlow(core, p1, p2);
            TestKeywords(core, p1, p2);

            // 使用时点/响应窗口 + 死亡原子（合成卡驱动，不依赖卡表）
            TestCounterWindow(core, p1, p2);
            TestDeathAtoms(core, p1, p2);

            // 三轨制（2026-09-09）：来源归因（法术=角色）
            TestAttribution(core, p1, p2);

            // 时点接线（P0）+ 衍生物（P1）+ 计数/日志（P2）（合成卡驱动，不依赖卡表）
            TestEntrySources(core, p1, p2);
            TestTriggerPayloadFilters(core, p1, p2);
            TestDeadTimings(core, p1, p2);
            TestAtomicPhaseRouting(core, p1, p2);
            TestSummonToken(core, p1, p2);
            TestMatchStats(core, p1, p2);

            if (cardsData.Count > 0)
            {
                TestRituals(core, cardsData);
                TestNewRituals(core, cardsData);
            }

            // P2b：对局日志按需导出（内存缓冲 → markdown 战报落盘）
            var verifyLog = MatchLogService.ExportMarkdown($"Logs/VerifyLog_{System.DateTime.Now:yyyyMMdd_HHmmss}.md");
            Debug.Log($"[Verify] 对局日志导出：{verifyLog ?? "无条目未导出"}");

            Debug.Log($"[Verify] 完成 — PASS={_pass} FAIL={_fail}");
            if (_fail == 0)
                Debug.Log("[Verify] ✅ 整条链路贯通：读配置→组卡→对局结算");
            else
                Debug.LogError($"[Verify] ❌ 存在 {_fail} 项失败");
        }

        /// <summary>
        /// 地牌经济（新资源模型）验证：
        /// - 地牌槽上限 = min(全局回合数, 9)，双方同步推进（先手首回合 1，对手首回合即 2）
        /// - 主阶段补地牌：可用（未横置）状态入场，张数受上限约束
        /// - 手动横置自选色：颜色限于地牌自身构成；每地牌每回合周期一次
        /// - 费用门槛：bank 灌满也打不出总费用 > 当前上限的卡
        /// - 结束阶段：未横置地牌自动产 1 灰；耗尽进墓后槽位空出可补充
        /// - 己方回合开始地牌恢复；台账逐行 LandCap 与产出计数
        /// 结束时游戏停在 p1 的第 3 回合主阶段。
        /// </summary>
        private static void TestLandEconomy(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            var pool1 = core.ElementPool.GetPool(p1);

            // ---- T1（p1 主阶段）：上限 1 ----
            Assert(core.ElementPool.GetLandCap(p1) == 1, "T1 地牌槽上限 = 1");

            var hand1 = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Hand));
            Assert(hand1.Count > 0, "p1 有手牌可放地牌");

            // 地牌资格（定案）：只有卡组正式生物可作地牌——魔法/仪式等非生物、衍生物/副本临时卡拒绝
            var creatures = hand1.Where(CardCore.ElementPoolSystem.CanServeAsLand).ToList();
            Assert(creatures.Count > 0, "p1 手牌有生物可放地牌");
            var nonCreature = hand1.FirstOrDefault(c => !CardCore.ElementPoolSystem.CanServeAsLand(c));
            var token = new Card { ID = "VERIFY_TOKEN" }; // 裸 Card = 效果生成的临时卡
            Assert(!core.ElementPool.AddCardToPool(token, p1), "地牌资格：衍生物/副本临时卡（裸 Card）拒绝");
            if (nonCreature != null)
                Assert(!GameActions.AddToElementPool(core, p1, nonCreature), "地牌资格：魔法/仪式等非生物超类拒绝");

            Assert(GameActions.AddToElementPool(core, p1, creatures[0]), "主阶段放地牌成功");

            var pooled1 = core.ElementPool.GetPooledCards(p1);
            Assert(pooled1.Count == 1 && !pooled1[0].IsTapped, "地牌以可用（未横置）状态入场");

            if (creatures.Count > 1)
                Assert(!GameActions.AddToElementPool(core, p1, creatures[1]), "超过地牌槽上限拒绝（T1 上限 1）");

            // 手动横置：颜色限于地牌自身构成
            var colors = pooled1[0].GetAvailableColors();
            Assert(colors.Count > 0, "地牌有可用颜色指示物");
            var pick = colors[0];
            var bad = PickAbsentColor(pooled1[0]);
            Assert(!GameActions.GainElementFromToken(core, p1, pooled1[0], bad),
                   "自选颜色限于地牌自身构成（非法色拒绝）");

            int bankBefore = pool1.AvailableMana[pick];
            Assert(GameActions.GainElementFromToken(core, p1, pooled1[0], pick), "手动横置产出成功");
            Assert(pool1.AvailableMana[pick] == bankBefore + 1, "自选颜色正确入 bank");
            Assert(pooled1[0].IsTapped, "产出后地牌横置");
            Assert(!GameActions.GainElementFromToken(core, p1, pooled1[0], pick), "同一地牌本回合周期不可重复产出");

            // 费用门槛：bank 灌满也打不出总费用 > 当前上限（1）的卡
            var candidate = hand1.Skip(1).FirstOrDefault(c => TotalCost(c) > 1);
            if (candidate != null)
            {
                foreach (var t in AllManaTypes())
                    pool1.AvailableMana[t] = 99;
                Assert(!PlayCardSync(core, p1, candidate, null),
                       $"费用门槛：总费用 {TotalCost(candidate)} > 上限 1，bank 充足也拒绝");
            }
            else
            {
                Debug.LogWarning("[Verify] 跳过费用门槛用例：手牌无总费用 > 1 的卡");
            }

            // ---- 结束 T1 → T2：上限双方同步推进 ----
            int gray1 = pool1.AvailableMana[ManaType.Gray];
            Assert(EndTurnPumped(core, p1), "结束 T1 回合");
            Assert(pool1.AvailableMana[ManaType.Gray] == gray1, "已横置地牌结束阶段不产出");
            Assert(core.ElementPool.GetLandCap(p1) == 2 && core.ElementPool.GetLandCap(p2) == 2,
                   "T2 双方地牌槽上限同步 = 2（对手首回合即 2）");

            // ---- T2（p2）：耗尽→墓地→补充 循环 + 结束阶段自动产灰 ----
            GameActions.SkipElementPool(core, p2);
            var hand2 = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Hand));

            var oneCost = hand2.FirstOrDefault(c => TotalCost(c) == 1);
            if (oneCost == null)
            {
                // 卡表 3 份白板未必抽进手牌：注入 1 费白板保证用例确定性
                var grayData = cardsData.FirstOrDefault(c => c.ID == "TEST_GRAY_001");
                if (grayData != null)
                {
                    oneCost = new CardWrapper(grayData);
                    oneCost.SetController(p2);
                    core.ZoneManager.GetZoneContainer(p2).Add(oneCost, Zone.Hand);
                }
            }
            if (oneCost != null)
            {
                Assert(GameActions.AddToElementPool(core, p2, oneCost), "放入 1 指示物地牌");
                var pc2 = core.ElementPool.GetPooledCards(p2)[0];
                var color2 = pc2.GetAvailableColors()[0];
                int before2 = core.ElementPool.GetPool(p2).AvailableMana[color2];

                Assert(GameActions.GainElementFromToken(core, p2, pc2, color2), "产出最后 1 指示物");
                Assert(core.ElementPool.GetPooledCards(p2).Count == 0, "耗尽地牌移出元素池");
                Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(oneCost), "耗尽地牌进墓地");
                Assert(core.ElementPool.GetPool(p2).AvailableMana[color2] == before2 + 1, "末次产出仍入 bank");

                var hand2b = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Hand));
                if (hand2b.Count > 0)
                {
                    // 合格地牌=卡组正式生物且非零费（CanServeAsLand + 指示物来源非空）——
                    // 夹具卡组成变动后手牌首位可能是法术/零费卡，取首张**合格**卡（对齐段 21 的检索惯例）
                    bool refilled = false;
                    foreach (var cand in hand2b)
                        if (GameActions.AddToElementPool(core, p2, cand)) { refilled = true; break; }
                    Assert(refilled, "耗尽后槽位空出，可补充地牌（手牌存在合格地牌卡）");
                }
            }
            else
            {
                if (hand2.Count > 0)
                    GameActions.AddToElementPool(core, p2, hand2[0]);
            }

            var pooled2 = core.ElementPool.GetPooledCards(p2).LastOrDefault();
            var pool2 = core.ElementPool.GetPool(p2);
            // 结束阶段产出定案（改版）：未横置地牌自动产「剩余最多色」（并列取枚举序靠前者，
            // 与 ElementPool.PickProduceColor 同法），不再恒产灰
            ManaType? expectColor = null;
            int expectCount = 0;
            if (pooled2 != null)
                foreach (ManaType t in AllManaTypes())
                    if (pooled2.Tokens.TryGetValue(t, out int n) && n > expectCount)
                    {
                        expectCount = n;
                        expectColor = t;
                    }
            int mostBefore = expectColor.HasValue ? pool2.AvailableMana[expectColor.Value] : 0;
            EndTurnPumped(core, p2);
            if (pooled2 != null && expectColor.HasValue)
            {
                Assert(pool2.AvailableMana[expectColor.Value] == mostBefore + 1,
                       $"结束阶段未横置地牌自动产「剩余最多色」{expectColor.Value}（+1 入 bank）");
                Assert(pooled2.IsTapped, "自动产出后地牌横置");
            }

            // ---- T3（p1）：地牌恢复 + 上限 3 + 台账 ----
            GameActions.SkipElementPool(core, p1);
            Assert(core.ElementPool.GetLandCap(p1) == 3, "T3 地牌槽上限 = 3");
            var landAfter = core.ElementPool.GetPooledCards(p1).FirstOrDefault();
            Assert(landAfter != null && !landAfter.IsTapped,
                   $"己方回合开始地牌恢复（解除横置）（p1 池 {core.ElementPool.GetPooledCards(p1).Count} 张：{string.Join(",", core.ElementPool.GetPooledCards(p1).Select(l => l.SourceCard.ID + (l.IsTapped ? "·横置" : "·直立")))}）");

            var records = core.ResourceLedger.GetRecords(p1);
            Assert(records.Count >= 2, "台账已有 p1 两行记录");
            Assert(records[0].LandCap == 1 && records[1].LandCap == 3, "台账 LandCap = min(全局回合数, 9)");
            Assert(records[0].TapsTaken == 1, "台账记录 T1 手动产出 1 次");
        }

        /// <summary>卡总费用（费用字典求和；无费用卡按默认灰 1 计）</summary>
        private static int TotalCost(Card card)
        {
            if (card is IHasCost hasCost && hasCost.Cost != null)
                return (int)hasCost.Cost.Values.Sum();
            return 1;
        }

        /// <summary>选一个地牌没有的颜色（用于验证非法色拒绝）</summary>
        private static ManaType PickAbsentColor(PooledCard land)
        {
            foreach (var t in AllManaTypes())
                if (!land.HasToken(t))
                    return t;
            return ManaType.Gray; // 不可达：地牌不可能持有全部颜色
        }

        private static List<ManaType> AllManaTypes()
        {
            return System.Enum.GetValues(typeof(ManaType)).Cast<ManaType>().ToList();
        }

        private static void TestSpell(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.ID == "TEST_SPELL_RED_001");
            Assert(data != null, "火球术配置存在");
            if (data == null) return;
            Assert(data.Effects != null && data.Effects.Count > 0
                   && data.Effects[0].AtomicEffects != null
                   && data.Effects[0].AtomicEffects.Count == 2,
                   "火球术效果反序列化（DealDamage+DrawCard）");

            var fireball = new CardWrapper(data);
            fireball.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(fireball, Zone.Hand);
            // 测试脚手架：模拟后期回合，费用门槛（= 地牌槽上限 9）不挡火球术费用
            // 新费用（法术不折，锚价全额）：红 4 + 蓝 1 = 档位 5
            core.ElementPool.GetPool(p1).GlobalTurnIndex = 9;
            core.ElementPool.GetPool(p1).AvailableMana[ManaType.Red] = 4;
            core.ElementPool.GetPool(p1).AvailableMana[ManaType.Blue] = 1;

            int lifeBefore = p2.Life;
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;

            bool played = PlayCardSync(core, p1, fireball,
                new List<Entity> { p2 });
            Assert(played, "火球术成功打出");

            Assert(p2.Life == lifeBefore - 4, $"对手 Life −4（{lifeBefore}→{p2.Life}）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "抽牌生效（牌库 −1）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball),
                   "法术结算后入墓地");
        }

        /// <summary>
        /// 分支卡经 PlayCard 的端到端（法术执行链修复的回归锚）：
        /// Steps 法术打出 → 发动区 → 步骤遍历 + OutcomeGate 即时分支 → 命中奖励抽牌 → 入墓；
        /// 元素费只扣一次（执行器 skipElementCost 防双计）。
        /// </summary>
        private static void TestSpellBranch(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.ID == "TEST_SPELL_DECLARE_001");
            Assert(data != null, "宣言分支卡配置存在");
            if (data == null) return;

            // 确定性：向 p2 手牌注入一张已知生物（宣言 类型:Creature 必命中）
            var markerData = new CardData { ID = "VERIFY_BRANCH_MARKER", CardName = "分支标记生物" };
            markerData.Supertype = Cardtype.Creature;
            var marker = new CardWrapper(markerData);
            marker.SetController(p2);
            core.ZoneManager.GetZoneContainer(p2).Add(marker, Zone.Hand);
            // 单张验证确定性：选位器指定注入的标记卡（缺省取手牌第一张，会受真实手牌干扰）
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker =
                (_, cands) => cands.Contains(marker) ? marker : cands[0];

            var declare = new CardWrapper(data);
            declare.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(declare, Zone.Hand);

            var pool1 = core.ElementPool.GetPool(p1);
            pool1.AvailableMana[ManaType.Blue] += 4;
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            int blueBefore = pool1.AvailableMana[ManaType.Blue];
            int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;

            Assert(PlayCardSync(core, p1, declare), "宣言分支卡成功打出");

            // 无 UI 的结算链同步完成：打出 −1 + 分支命中抽 1 = 手牌数不变
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore,
                   "打出 −1 且宣言命中抽 +1（分支经 PlayCard 真实触发）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1, "奖励抽牌来自牌库顶");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(declare), "分支法术结算后入墓地");
            Assert(core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "结算完成后发动区清空");
            // 期望扣费从声明费用动态取值（卡表调价不再牵动断言）；skipElementCost=true 保证执行器
            // 不再对派生元素费二次扣款——实际扣费恰为声明值即证明"只扣一次"
            var declaredBlue = GameActions.GetCardCost(declare, 0).TryGetValue((int)ManaType.Blue, out var blueCost)
                ? (int)blueCost : 0;
            Assert(pool1.AvailableMana[ManaType.Blue] == blueBefore - declaredBlue,
                   $"元素费只扣一次（skipElementCost 防执行器双计费；声明费蓝 {declaredBlue}，实际扣 {blueBefore - pool1.AvailableMana[ManaType.Blue]}）");

            core.ZoneManager.GetZoneContainer(p2).Remove(marker, Zone.Hand);
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker = null; // 还原缺省选位器
        }

        // ======================================== 抉择（Modal/选发，2026-09-07 定案） ========================================

        /// <summary>
        /// 抉择卡端到端：per-mode 独立计价（构筑期推导存储，发动时只读）+ 声明期先选模式再定费用 +
        /// 只执行所选模式。同时锁死组合计价三规则：①并发取总（4伤并抽1=两笔之和）；
        /// ②条件奖励免费（门后 then/else 不计价）；③抉择各计所需（只付所选模式）。
        /// </summary>
        private static void TestSpellModal(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            EnsureMainPhase(core, p1);

            // ---- 0. 规则①并发取总 / ②条件奖励免费（既有卡锁死断言）----
            var fireballData = cardsData.FirstOrDefault(c => c.ID == "TEST_SPELL_RED_001");
            if (fireballData != null)
            {
                var fireDef = CardEffectConverter.ConvertAll(fireballData.Effects, fireballData.ID)[0];
                var fireCosts = CostDerivationService.DeriveElementCosts(fireDef);
                int drawPrice = (int)System.Math.Round(CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DrawCard).BaseCost,
                    System.MidpointRounding.AwayFromZero);
                var drawColor = CardCore.ElementAffinities.GetAffinityForEffect(AtomicEffectType.DrawCard).PrimaryColor;
                Assert(fireCosts.Where(c => c.ManaType == ManaType.Red).Sum(c => c.Value) == 4,
                       "并发组合：4伤=红4（取总的一半）");
                Assert(fireCosts.Where(c => c.ManaType == drawColor).Sum(c => c.Value) == drawPrice,
                       "并发组合：抽1=抽牌表价并入总费（费用取总）");
            }

            var declareData = cardsData.FirstOrDefault(c => c.ID == "TEST_SPELL_DECLARE_001");
            if (declareData != null)
            {
                var def = CardEffectConverter.ConvertAll(declareData.Effects, declareData.ID)[0];
                var stripped = new EffectDefinition { Steps = def.Steps.Where(s => s.Kind != RuntimeStepKind.Branch).ToList() };
                int totalAll = CostDerivationService.DeriveElementCosts(def).Sum(c => c.Value);
                int totalNoBranch = CostDerivationService.DeriveElementCosts(stripped).Sum(c => c.Value);
                Assert(totalAll == totalNoBranch,
                       "条件奖励不计费：门后 then/else 步骤免计价（击杀→抽牌只付伤害费）");
            }

            // ---- 1. 抉择卡数据/转换/判据 ----
            var data = cardsData.FirstOrDefault(c => c.ID == "TEST_SPELL_MODAL_001");
            Assert(data != null, "抉择卡配置存在（TEST_SPELL_MODAL_001）");
            if (data == null) return;

            Assert(CostDerivationService.HasChoiceEffect(data) && CostDerivationService.GetModeCount(data) == 2,
                   "抉择判据：HasChoiceEffect 且模式数=2");
            var defs = CardEffectConverter.ConvertAll(data.Effects, data.ID);
            Assert(defs.Count == 1 && defs[0].Steps.Count == 1 && defs[0].Steps[0].Kind == RuntimeStepKind.Choice
                   && defs[0].Steps[0].Choices.Count == 2,
                   "转换：Steps[0] 为 Choice 且双模式（choices≥2 有效）");

            // ---- 2. per-mode 计价（构筑期推导存储）----
            var mode0 = CardCostService.GetModeCost(data, 0);
            var mode1 = CardCostService.GetModeCost(data, 1);
            Assert(mode0.TryGetValue((int)ManaType.Red, out var r0) && System.Math.Abs(r0 - 4f) < 0.01f,
                   "模式0计价：4伤=红4（1伤=1元素锚）");
            var modalDrawColor = CardCore.ElementAffinities.GetAffinityForEffect(AtomicEffectType.DrawCard).PrimaryColor;
            var modalDrawPrice = (int)System.Math.Round(CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DrawCard).BaseCost,
                System.MidpointRounding.AwayFromZero);
            Assert(mode1.TryGetValue((int)modalDrawColor, out var d1) && System.Math.Abs(d1 - modalDrawPrice) < 0.01f,
                   "模式1计价：抽1=抽牌表价（per-mode 独立，不求和）");
            Assert(mode1.Values.Sum() < mode0.Values.Sum(),
                   "两模式费用独立（红4 ≠ 蓝" + modalDrawPrice + "，未取总）");
            float maxTotal = System.Math.Max(mode0.Values.Sum(), mode1.Values.Sum());
            Assert(data.Cost != null && data.Cost.Count > 0 && System.Math.Abs(data.Cost.Values.Sum() - maxTotal) < 0.01f,
                   "声明 costList=最大模式费（EnsureCost 抉择分支——地牌产元素/素材口径）");

            // ---- 状态快照（本段灌 bank/上限，结束恢复）----
            var pool1 = core.ElementPool.GetPool(p1);
            var snap = new Dictionary<ManaType, int>(pool1.AvailableMana);
            int idxSnap = pool1.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9; // 费用门槛不挡
            pool1.AvailableMana[ManaType.Red] += 6;
            pool1.AvailableMana[modalDrawColor] += 5;

            try
            {
                // ---- 3. 模式0端到端（缺省参数=0）：只伤不抽 ----
                int p2Life = p2.Life;
                int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                int redBefore = pool1.AvailableMana[ManaType.Red];
                var modal0 = InjectCard(core, p1, data);

                // 伤害事件捕获（诊断）：观察模式0结算期间实际发生的每笔伤害（目标/来源/量）
                var dmgSeen = new List<string>();
                void OnModalDamage(DamageEvent e) =>
                    dmgSeen.Add($"{(e.Target is Player pl ? pl.Name : (e.Target as Card)?.ID)}<-{e.Amount}(src={(e.Source is Card sc ? sc.ID : "Player")})");
                EventManager.Instance.Subscribe<DamageEvent>(OnModalDamage);
                try
                {
                    Assert(PlayCardSync(core, p1, modal0, new List<Entity> { p2 }),
                           "抉择卡模式0打出（缺省 mode=0）");
                }
                finally
                {
                    EventManager.Instance.Unsubscribe<DamageEvent>(OnModalDamage);
                }
                Assert(p2.Life == p2Life - 4,
                       $"模式0：对目标造成4伤（实际 {p2Life}→{p2.Life}；期间伤害事件 [{string.Join("; ", dmgSeen)}]；p2 护甲={p2.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter)} 易损={p2.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter)}）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore, "模式0：不抽牌（只执行所选）");
                Assert(pool1.AvailableMana[ManaType.Red] == redBefore - 4, "模式0实付红4（先选择再定费用）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(modal0)
                       && core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "抉择法术结算后入墓、发动区清空");

                // ---- 4. 模式1端到端：只抽不伤 ----
                p2Life = p2.Life;
                deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                int drawBefore = pool1.AvailableMana[modalDrawColor];
                var modal1 = InjectCard(core, p1, data);

                Assert(PlayCardSync(core, p1, modal1, null, modeIndex: 1), "抉择卡模式1打出（先选模式再定费用）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1, "模式1：抽一张牌");
                Assert(p2.Life == p2Life, "模式1：不造伤害（执行隔离）");
                Assert(pool1.AvailableMana[modalDrawColor] == drawBefore - modalDrawPrice, "模式1实付抽牌表价（非两模式之和）");

                // ---- 5. 防超发：pending 按声明模式计 ----
                pool1.AvailableMana[ManaType.Red] = 4; // bank 恰好一张模式0
                var overA = InjectCard(core, p1, data);
                var overB = InjectCard(core, p1, data);
                Assert(GameActions.PlayCard(core, p1, overA, new List<Entity> { p2 }, Zone.Hand, 0),
                       "防超发：第一张模式0声明上栈（声明期不付费）");
                Assert(!GameActions.PlayCard(core, p1, overB, new List<Entity> { p2 }, Zone.Hand, 0),
                       "防超发：同笔 bank 第二张模式0被拒（GetPendingCastCosts 按模式计）");
                GameActions.DrainStack(core);
                Assert(pool1.AvailableMana[ManaType.Red] == 0, "防超发：结算后实付红4");
            }
            finally
            {
                foreach (var kv in snap) pool1.AvailableMana[kv.Key] = kv.Value;
                pool1.GlobalTurnIndex = idxSnap;
            }

            // ---- 6. 无效数据容错：choices<2 整步跳过 ----
            var bad = new CardData { ID = "VERIFY_MODAL_BAD", CardName = "无效抉择" };
            bad.Supertype = Cardtype.Spell;
            bad.Effects.Add(new CardEffectData
            {
                Id = "BAD_MAIN",
                Steps = new List<EffectStepData>
                {
                    new EffectStepData
                    {
                        kind = 2,
                        choices = new List<EffectChoiceData> { new EffectChoiceData() }, // 仅 1 个模式 → 无效
                    }
                }
            });
            var badDefs = CardEffectConverter.ConvertAll(bad.Effects, bad.ID);
            Assert(badDefs.Count == 1 && (badDefs[0].Steps == null || badDefs[0].Steps.Count == 0),
                   "choices<2：整步跳过不抛异常（等价无该步骤）");
            Assert(!CostDerivationService.HasChoiceEffect(bad) && CostDerivationService.GetModeCount(bad) == 1,
                   "无效抉择不计模式数");

            // ---- 7. 功能哈希：抉择顺序参与（不碰撞）----
            var swap = new CardData { ID = data.ID, CardName = data.CardName };
            swap.Supertype = data.Supertype;
            var srcStep = data.Effects[0].Steps[0];
            swap.Effects.Add(new CardEffectData
            {
                Id = data.Effects[0].Id,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 2, choices = new List<EffectChoiceData> { srcStep.choices[1], srcStep.choices[0] } }
                }
            });
            Assert(SynergyUI.ContentHasher.HashCard(data) != SynergyUI.ContentHasher.HashCard(swap),
                   "抉择模式顺序参与功能哈希（ContentHasher kind==2 递归）");

            // ---- 8. MemoryPack DTO 往返 ----
            var ser = CardCore.Serialization.SerializableEffectStepData.FromData(data.Effects[0].Steps[0]);
            var back = ser.ToData();
            Assert(back.choices != null && back.choices.Count == 2
                   && back.choices[0].steps[0].atomic.EffectType == "DealDamage"
                   && back.choices[1].steps[0].atomic.EffectType == "DrawCard",
                   "序列化往返保抉择结构（模式数与原子类型）");
        }

        // ======================================== 使用时点 / 响应窗口（Phase 2） ========================================

        /// <summary>
        /// 使用时点 + 响应窗口端到端（Option Y 定案；合成卡驱动，不依赖卡表）：
        /// - 打出即声明上栈（声明期不付费），对手获得优先权；
        /// - 打落（软打断）：发动区卡送墓 → cast 中止：不付费、无效果；
        /// - 发动无效（硬反制）：cast 照常付费但跳效果入墓，费用不退；
        /// - 声明超发保护：同笔 bank 已被在栈 cast 占用时拒绝新声明（防超发，不回卷）。
        /// </summary>
        private static void TestCounterWindow(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // 合成法术：显式灰 1 费（绕开推导），原子由参数给定
            CardData SpellData(string id, string name, params AtomicEffectEntry[] atoms)
            {
                var data = new CardData { ID = id, CardName = name };
                data.Supertype = Cardtype.Spell;
                data.Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 1 } };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_MAIN",
                    DisplayName = name,
                    AtomicEffects = new List<AtomicEffectEntry>(atoms),
                });
                return data;
            }
            AtomicEffectEntry Atom(string type, int value = 0)
                => new AtomicEffectEntry { EffectType = type, Value = value };

            // 状态快照（本段灌满双方 bank/上限，结束恢复——不污染后续段落）
            var pool1 = core.ElementPool.GetPool(p1);
            var pool2 = core.ElementPool.GetPool(p2);
            var snap1 = new Dictionary<ManaType, int>(pool1.AvailableMana);
            var snap2 = new Dictionary<ManaType, int>(pool2.AvailableMana);
            int idx1 = pool1.GlobalTurnIndex, idx2 = pool2.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;   // 费用门槛（=地牌槽上限）不挡
            pool2.GlobalTurnIndex = 9;
            foreach (var t in AllManaTypes()) { pool1.AvailableMana[t] = 99; pool2.AvailableMana[t] = 99; }

            try
            {
                // ---- 1. 打落（软打断）：不付费、无效果 ----
                var fireball = InjectCard(core, p1, SpellData("VERIFY_CAST_FB", "验证火球", Atom("DealDamage", 4)));
                var knock = InjectCard(core, p2, SpellData("VERIFY_CAST_KD", "验证打落", Atom("KnockDown")));

                int p2Life = p2.Life;
                int gray1 = pool1.AvailableMana[ManaType.Gray];

                Assert(GameActions.PlayCard(core, p1, fireball, new List<Entity> { p2 }),
                       "打出火球（使用时点声明上栈）");
                Assert(core.StackEngine.StackSize == 1 && core.StackEngine.CurrentPriorityHolder == p2,
                       "cast 上栈且对手持有优先权（响应窗口开启）");
                Assert(pool1.AvailableMana[ManaType.Gray] == gray1, "声明期不付费（Option Y）");

                Assert(GameActions.PlayCardInResponse(core, p2, knock, new List<Entity> { fireball }),
                       "响应窗口内打出打落（指向发动区的火球）");
                Assert(core.ZoneManager.IsCardInZone(fireball, p1, Zone.Activation),
                       "打落仅入栈未结算：火球仍留发动区");

                GameActions.DrainStack(core);   // 双 Pass → LIFO：打落先、火球 cast 后

                Assert(core.StackEngine.IsEmpty, "栈已排干");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball), "火球被送墓（软打断达成）");
                Assert(p2.Life == p2Life, "被中止的 cast 不结算效果（伤害 0）");
                Assert(pool1.AvailableMana[ManaType.Gray] == gray1, "被中止的 cast 不付费");
                Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(knock), "打落牌结算后入墓");

                // ---- 2. 发动无效（硬反制）：付费但跳效果，费用不退 ----
                var fireball2 = InjectCard(core, p1, SpellData("VERIFY_CAST_FB2", "验证火球二", Atom("DealDamage", 4)));
                var negate = InjectCard(core, p2, SpellData("VERIFY_CAST_NA", "验证无效", Atom("NegateActivation")));

                int gray1b = pool1.AvailableMana[ManaType.Gray];

                Assert(GameActions.PlayCard(core, p1, fireball2, new List<Entity> { p2 }), "打出第二张火球");
                Assert(GameActions.PlayCardInResponse(core, p2, negate, new List<Entity> { fireball2 }),
                       "响应窗口内打出发动无效");
                GameActions.DrainStack(core);

                Assert(p2.Life == p2Life, "被无效的 cast 跳过效果（伤害 0）");
                Assert(pool1.AvailableMana[ManaType.Gray] == gray1b - 1,
                       "无效路径照常付费（窗口后扣费，费用不退）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball2), "被无效的卡入墓");

                // ---- 3. 声明超发保护：同笔 bank 不重复承诺 ----
                var a = InjectCard(core, p1, SpellData("VERIFY_CAST_A", "验证超发A", Atom("DealDamage", 1)));
                var b = InjectCard(core, p1, SpellData("VERIFY_CAST_B", "验证超发B", Atom("DealDamage", 1)));
                pool1.AvailableMana[ManaType.Gray] = 1;   // 只够一张
                Assert(GameActions.PlayCard(core, p1, a, new List<Entity> { p2 }), "第一张灰1可声明（占用承诺）");
                Assert(!GameActions.PlayCard(core, p1, b, new List<Entity> { p2 }),
                       "同一笔 bank 已被在栈 cast 占用：第二张拒绝（防超发）");
                GameActions.DrainStack(core);
                Assert(pool1.AvailableMana[ManaType.Gray] == 0, "第一张结算后照常扣费");
                Assert(p2.Life == p2Life - 1, "第一张效果照常结算（伤害 1）");

                // ---- 4. 新代价执行（2026-09-07 补）：对手抽牌 / 对手回复生命 ----
                int p2HandBefore = core.ZoneManager.GetCards(p2, Zone.Hand).Count;
                int p2DeckBefore = core.ZoneManager.GetCards(p2, Zone.Deck).Count;
                var oppCostCtx = new CostContext { Payer = p1, ZoneManager = core.ZoneManager, Source = null };
                Assert(CostHandlerRegistry.Pay(new CostInstance { Type = CostType.OpponentDraw, Value = 1 }, oppCostCtx),
                       "代价执行：对手抽1（p1 支付，无需资源恒可付）");
                Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandBefore + 1
                       && core.ZoneManager.GetCards(p2, Zone.Deck).Count == p2DeckBefore - 1,
                       "代价执行：对手手牌 +1、牌库 −1");

                p2.Life = 10;
                Assert(CostHandlerRegistry.Pay(new CostInstance { Type = CostType.OpponentHeal, Value = 2 }, oppCostCtx),
                       "代价执行：对手回复2（p1 支付）");
                Assert(p2.Life == 12, "代价执行：对手生命 10→12（未溢出常规回复）");
            }
            finally
            {
                pool1.GlobalTurnIndex = idx1;
                pool2.GlobalTurnIndex = idx2;
                pool1.AvailableMana.Clear();
                foreach (var kv in snap1) pool1.AvailableMana[kv.Key] = kv.Value;
                pool2.AvailableMana.Clear();
                foreach (var kv in snap2) pool2.AvailableMana[kv.Key] = kv.Value;
            }
        }

        // ======================================== 死亡原子（牺牲/吞噬/湮灭） ========================================

        /// <summary>
        /// 死亡原子端到端（DeathRules 死因的原子层落地；合成随从驱动，不依赖卡表）：
        /// - 牺牲：己方生物经 Sacrifice 死因入墓；
        /// - 吞噬：消灭裁决成功才吸收——复制目标全部关键词 + 回复目标当前生命；
        ///   不灭拦 Devour（消灭类），拦下即无吸收；
        /// - 湮灭：直送除外区、复生不可救；不灭不拦（非消灭类，照常湮灭）。
        /// </summary>
        private static void TestDeathAtoms(GameCore core, Player p1, Player p2)
        {
            var used = new List<Card>();

            Card Make(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData { ID = "VERIFY_DEATH_" + used.Count, CardName = "死" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = power;
                data.Life = life;
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                core.ZoneManager.TryAddToBattlefield(card, owner);
                card.Untap();
                used.Add(card);
                return card;
            }
            void Clean()
            {
                foreach (var c in used)
                {
                    var owner = c.GetController() ?? p1;
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Battlefield);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Graveyard);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Exile);
                }
            }

            // 原子直发（直连执行器，跳过元素费；与 TestCreature 击杀路径同法）
            void Atom(AtomicEffectType type, Player controller, Entity source, Card target, int value = 0)
            {
                var def = new EffectDefinition
                {
                    Id = "VERIFY_DEATH_" + type,
                    TriggerTiming = TriggerTiming.Activate_Active,
                    Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = type, Value = value } },
                };
                core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                {
                    Definition = def,
                    Source = source,
                    Controller = controller,
                    Targets = new List<Entity> { target },
                }, skipElementCost: true).Forget();
            }

            try
            {
                // ---- 牺牲：己方生物入墓 ----
                var victim = Make(p1, 2, 3);
                Atom(AtomicEffectType.Sacrifice, p1, p1, victim);
                Assert(!victim.IsAlive && core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(victim),
                       "牺牲：生物经 Sacrifice 死因入墓");

                // ---- 吞噬：消灭裁决成功才吸收 ----
                var devourer = Make(p1, 3, 6);
                Atom(AtomicEffectType.DealDamage, p1, p1, devourer, 4); // 先受伤留回复缺口
                Assert(devourer.GetLife() == 2, "吞噬者先受伤至 2（留回复缺口）");

                var prey = Make(p2, 1, 3, CardCore.Attribute.KeywordRules.Taunt);
                Atom(AtomicEffectType.Devour, p1, devourer, prey);
                Assert(!prey.IsAlive && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(prey),
                       "吞噬：猎物经 Devour 死因入墓");
                Assert(devourer.HasKeyword(CardCore.Attribute.KeywordRules.Taunt), "吞噬：复制目标全部关键词（嘲讽）");
                Assert(devourer.GetLife() == 5, "吞噬：回复目标当前生命（2+3=5）");

                // 不灭拦 Devour（消灭类）：拦下即无吸收
                var rock = Make(p2, 0, 5, CardCore.Attribute.KeywordRules.Indestructible, CardCore.Attribute.KeywordRules.Taunt);
                int lifeBefore = devourer.GetLife();
                Atom(AtomicEffectType.Devour, p1, devourer, rock);
                Assert(rock.IsAlive && core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(rock),
                       "不灭拦下吞噬（消灭类死因）");
                Assert(!devourer.HasKeyword(CardCore.Attribute.KeywordRules.Taunt) && devourer.GetLife() == lifeBefore,
                       "拦下即无吸收（关键词未复制、生命未回复）");

                // ---- 湮灭：直送除外 + 复生不可救 + 不灭不拦 ----
                var phoenix = Make(p2, 2, 2, CardCore.Attribute.KeywordRules.Reborn);
                Atom(AtomicEffectType.Annihilate, p1, p1, phoenix);
                Assert(!phoenix.IsAlive && core.ZoneManager.GetCards(p2, Zone.Exile).Contains(phoenix),
                       "湮灭：直送除外区（不入墓）");
                Assert(!core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(phoenix),
                       "湮灭：复生不可救（未回场）");

                var adamant = Make(p2, 1, 4, CardCore.Attribute.KeywordRules.Indestructible);
                Atom(AtomicEffectType.Annihilate, p1, p1, adamant);
                Assert(!adamant.IsAlive && core.ZoneManager.GetCards(p2, Zone.Exile).Contains(adamant),
                       "湮灭：不灭不拦（非消灭类，照常湮灭）");
            }
            finally
            {
                Clean();
            }
        }

        private static void TestCreature(GameCore core, Player p1, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.ID == "TEST_GREEN_003");
            Assert(data != null, "古树守卫配置存在");
            if (data == null) return;

            var creature = new CardWrapper(data);
            creature.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(creature, Zone.Hand);
            var pool = core.ElementPool.GetPool(p1);
            // 新费用（身材灰 3+5=8 点 = 灰 4）：灰 4 = 档位 4
            pool.AvailableMana[ManaType.Gray] = 4;

            bool played = PlayCardSync(core, p1, creature);
            Assert(played, "古树守卫成功打出");
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(creature),
                   "生物进入战场");
            // 配置驱动血量：CardWrapper 同步 _life 前此处恒为 1
            Assert(creature.GetLife() == 5, $"血量来自配置（应为5，实际{creature.GetLife()}）");

            // 伤害效果击杀
            var killDef = new EffectDefinition
            {
                Id = "VERIFY_KILL",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 5 }
                }
            };
            var kill = new EffectInstance
            {
                Definition = killDef,
                Source = creature,
                Controller = p1,
                Targets = new List<Entity> { creature },
            };
            // 直连执行器（跳过元素费：合成效果的计价与击杀语义无关）
            core.StackEngine.GetExecutor().ExecuteAsync(kill, skipElementCost: true).Forget();

            Assert(creature.GetLife() <= 0, $"受伤后血量≤0（实际{creature.GetLife()}）");
            Assert(!creature.IsAlive, "生物被伤害效果击杀");
        }

        // ======================================== 棋盘层（Phase A） ========================================

        /// <summary>
        /// 棋盘选择/表现层验证。架构铁律：棋盘 = 核心状态的确定性纯函数
        /// （核心不与棋盘绑定，重连/传输只同步 CardCore，Resync 即重建占用）。
        /// 全程手动 Resync，不启用事件自动刷新。
        /// </summary>
        /// <summary>
        /// 来源归因（三轨制定案 2026-09-09 规则①②）：所有魔法卡的效果来源=角色（Player）——
        /// 法术伤害致死归因到施法玩家（不再是法术卡）；法术伤害不触发吸血（来源非生物）。
        /// 规则①（生物效果来源=该生物）由 TestKeywords 段 18f 指示物轨锚锁定。
        /// </summary>
        private static void TestAttribution(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // 归因靶：p2 场上 1/2 生物（受 3 伤必死）
            var victimData = new CardData { ID = "VERIFY_ATTRIB_VICTIM", CardName = "归因靶" };
            victimData.Supertype = Cardtype.Creature;
            victimData.Power = 1;
            victimData.Life = 2;
            var victim = new CardWrapper(victimData);
            victim.SetController(p2);
            core.ZoneManager.TryAddToBattlefield(victim, p2);

            // 法术：灰 0 费 DealDamage 3
            var boltData = new CardData { ID = "VERIFY_ATTRIB_SPELL", CardName = "归因法术" };
            boltData.Supertype = Cardtype.Spell;
            boltData.Cost[(int)ManaType.Gray] = 0;
            var boltEffect = new CardEffectData { Id = "VERIFY_ATTRIB_EFF", TriggerTiming = (int)TriggerTiming.OnPlay };
            boltEffect.AtomicEffects = new List<AtomicEffectEntry> { new AtomicEffectEntry { EffectType = "DealDamage", Value = 3 } };
            boltData.Effects.Add(boltEffect);
            var bolt = InjectCard(core, p1, boltData);

            CardDestroyEvent killEvt = null;
            void OnAttribKill(CardDestroyEvent e) { if (e.DestroyedCard == victim) killEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnAttribKill);
            int p1LifeBefore = p1.Life;
            try
            {
                Assert(PlayCardSync(core, p1, bolt, new List<Entity> { victim }), "归因法术：成功施放并结算");
                core.SBAEngine.CheckAndExecute();
                Assert(killEvt != null && ReferenceEquals(killEvt.Source, p1) && !(killEvt.Source is Card),
                       "来源归因②：法术伤害致死归因到施法玩家（Source=角色，非法术卡）");
                Assert(!victim.IsAlive && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(victim),
                       "来源归因②：目标死亡入墓（伤害链完整）");
                Assert(p1.Life == p1LifeBefore, "法术伤害不触发吸血/系命（来源非生物无从回复）");
            }
            finally
            {
                EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnAttribKill);
            }
        }

        /// <summary>
        /// 连接光环运行时（三轨制·光环轨 2026-09-09）：方向映射双射 + 单向指向 + 受益者豁免干扰 +
        /// 无效压制来源（唯一能压光环的口）+ 断链失效（live-query 无物化）+ 对手视角镜像 +
        /// 生命光环（伤害先吃光环/断链回落 SBA 收尸）。自建 BoardState（合成卡）；结束归零。
        /// </summary>
        private static void TestLinkAura(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            // ---- 1. 方向映射定死：六对六双射（互逆）+ Opposite 180° 对向 ----
            Assert(GameBoard.BoardMath.MapArrow(CardCore.HexDirection.Up) == GameBoard.BoardDirection.NE
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.UpperRight) == GameBoard.BoardDirection.E
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.LowerRight) == GameBoard.BoardDirection.SE
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.Down) == GameBoard.BoardDirection.SW
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.LowerLeft) == GameBoard.BoardDirection.W
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.UpperLeft) == GameBoard.BoardDirection.NW
                   && GameBoard.BoardMath.ArrowOf(GameBoard.BoardDirection.NE) == CardCore.HexDirection.Up,
                   "链接箭头映射：六对六双射（MapArrow/ArrowOf 互逆）");
            Assert(GameBoard.BoardMath.Opposite(GameBoard.BoardDirection.NE) == GameBoard.BoardDirection.SW
                   && GameBoard.BoardMath.Opposite(GameBoard.BoardDirection.E) == GameBoard.BoardDirection.W
                   && GameBoard.BoardMath.Opposite(GameBoard.BoardDirection.SE) == GameBoard.BoardDirection.NW,
                   "Opposite：180° 对向（对手视角镜像基础）");

            GameBoard.LinkAuraSystem.Detach(); // 隔离上局残留（静态扩展点惯例）
            var board = new GameBoard.BoardState(core, p1, p2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            board.EnableAutoResync();
            GameBoard.LinkAuraSystem.Attach(board);

            // 清空双方战场残留（前面各段的测试卡已断言完毕；直删不发事件，随后手动 Resync）
            foreach (var pl in new[] { p1, p2 })
            {
                var leftover = core.ZoneManager.GetCards(pl, Zone.Battlefield).ToList();
                foreach (var c in leftover)
                    core.ZoneManager.GetZoneContainer(pl).Remove(c, Zone.Battlefield);
            }
            board.Resync();

            var used = new List<Card>();
            Card MakePlain(Player owner, int power, int life)
            {
                var data = new CardData { ID = "VERIFY_LA_" + used.Count, CardName = "链" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = power;
                data.Life = life;
                var card = new CardWrapper(data);
                card.SetController(owner);
                core.ZoneManager.TryAddToBattlefield(card, owner);
                used.Add(card);
                return card;
            }
            CardData DataOf(Card c) => (c as CardWrapper).GetData();
            BoardDirection DirBetween(Card from, Card to, out bool adjacent)
            {
                adjacent = false;
                board.TryGetCell(from, out int fx, out int fz);
                board.TryGetCell(to, out int tx, out int tz);
                foreach (GameBoard.BoardDirection d in System.Enum.GetValues(typeof(GameBoard.BoardDirection)))
                {
                    var (nx, nz) = GameBoard.BoardMath.Neighbor(fx, fz, d);
                    if (nx == tx && nz == tz) { adjacent = true; return d; }
                }
                return default;
            }

            try
            {
                // ---- 2. 单向指向：箭头指向格的占据者享受（攻/关键词）；来源自身不吃 ----
                var ben = MakePlain(p1, 3, 5);
                var src = MakePlain(p1, 2, 5);
                var dirTo = DirBetween(src, ben, out bool adjacent);
                Assert(adjacent, "光环：落位相邻前提（first-free 顺序相邻）");
                DataOf(src).LinkAuras.Add(new LinkAuraData { stat = "Power", value = 2 });
                DataOf(src).LinkAuras.Add(new LinkAuraData { keyword = "Taunt" });
                DataOf(src).ArrowDirections = GameBoard.BoardMath.ArrowOf(dirTo);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 5 && ben.HasKeyword("Taunt"),
                       "连接光环：箭头指向格的占据者攻+2 且视为有嘲讽（单向指向）");
                Assert(ben.GetKeywordCount("Taunt") == 0,
                       "光环关键词不物化：GetKeywordCount 仍 0（融合叠加计数只归真授予）");
                Assert(src.GetPower() == 2 && !src.HasKeyword("Taunt"),
                       "箭头不指向自己：来源自身无加成");
                Assert(core.LayerEngine.CalculatePower(ben) == 5,
                       "战斗读数可见：LayerEngine.CalculatePower 基值含光环（战斗/SBA 同源）");

                // ---- 3. 受益者豁免：净化/沉默/无效打在受益者身上，光环不变 ----
                CardCore.Attribute.KeywordRules.PurifyKeywords(ben);
                ben.AddCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
                ben.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 5 && ben.HasKeyword("Taunt"),
                       "受益者不可被干扰：净化/沉默/无效都不压光环（只有作用于来源才有效）");
                ben.RemoveCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
                ben.RemoveCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);

                // ---- 4. 无效压制来源（唯一能压光环的口）；净化不压箭头（箭头=卡面数据）----
                src.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1, p1);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 3 && !ben.HasKeyword("Taunt"),
                       "无效压制来源：光环熄灭（无效=唯一能压光环的指示物）");
                CardCore.Attribute.CounterRules.PurgeAll(src); // 净化口径清掉无效（含全部指示物）
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 5 && ben.HasKeyword("Taunt"),
                       "净化来源不压箭头：无效被清后光环恢复（箭头是卡面数据，净化只清状态）");

                // ---- 5. 断链失效：来源离场光环消失（live-query 无物化） ----
                core.ZoneManager.MoveCard(src, p1, Zone.Battlefield, Zone.Graveyard);
                Assert(ben.GetPower() == 3 && !ben.HasKeyword("Taunt"),
                       "断链失效：来源离场光环消失");

                // ---- 6. 对手视角镜像：p2（归属1）的箭头绝对方向取 Opposite ----
                var ben2 = MakePlain(p2, 2, 4);
                var src2 = MakePlain(p2, 2, 4);
                var dir2 = DirBetween(src2, ben2, out bool adjacent2);
                Assert(adjacent2, "光环镜像：落位相邻前提");
                DataOf(src2).LinkAuras.Add(new LinkAuraData { stat = "Life", value = 2 });
                DataOf(src2).ArrowDirections = GameBoard.BoardMath.ArrowOf(dir2); // 反例：按绝对方向写
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben2.GetLife() == 4 && ben2.GetMaxLife() == 4,
                       "镜像负例：对手箭头未按其视角写（直接绝对方向）不生效");
                DataOf(src2).ArrowDirections = GameBoard.BoardMath.ArrowOf(GameBoard.BoardMath.Opposite(dir2)); // 正例
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben2.GetLife() == 6 && ben2.GetMaxLife() == 6,
                       "镜像正例：对手箭头按其视角写（绝对方向取 Opposite）生效——生命光环上限与当前同加");

                // ---- 7. 生命光环：伤害先吃光环；断链回落经 SBA 收尸 ----
                CardCore.Attribute.KeywordRules.ApplyDamage(p1, ben2, 5, false); // 有效 6 受 5 伤剩 1
                Assert(ben2.IsAlive && ben2.GetLife() == 1,
                       "生命光环：伤害先吃穿光环加成（有效生命 6 受 5 伤剩 1，不死）");
                core.ZoneManager.MoveCard(src2, p2, Zone.Battlefield, Zone.Graveyard); // 断链
                core.SBAEngine.CheckAndExecute();
                Assert(!ben2.IsAlive,
                       "断链回落：光环垫的生命随链消失，有效归零经 SBA 收尸");

                // ---- 8. JSON 链路：夹具卡 linkAuras/arrows 装载 + 内容哈希分叉（LA: 条件段） ----
                var fixture = cardsData.FirstOrDefault(c => c.ID == "TEST_LINK_AURA_001");
                Assert(fixture != null
                       && fixture.ArrowDirections == (CardCore.HexDirection.Up | CardCore.HexDirection.LowerRight)
                       && fixture.LinkAuras.Count == 2
                       && fixture.LinkAuras[0].stat == "Power" && fixture.LinkAuras[0].value == 2
                       && fixture.LinkAuras[1].keyword == "Taunt",
                       "光环 JSON 链路：arrows/linkAuras 装载还原（三轨测试夹具卡）");
                var noAuraClone = MakePlain(p1, 2, 4);
                var noAuraData = (noAuraClone as CardWrapper).GetData();
                string hashNoAura = SynergyUI.ContentHasher.HashCard(noAuraData);
                noAuraData.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 2 });
                string hashWithAura = SynergyUI.ContentHasher.HashCard(noAuraData);
                Assert(hashNoAura != hashWithAura,
                       "光环内容哈希：linkAuras 影响 ID（LA: 段）");
                noAuraData.LinkAuras.Clear();
                Assert(SynergyUI.ContentHasher.HashCard(noAuraData) == hashNoAura,
                       "光环内容哈希：空表不追加 LA: 段（存量卡 ID 不漂移）");
                used.Add(noAuraClone); // 交由 finally 统一清理
            }
            finally
            {
                foreach (var c in used)
                {
                    var owner = c.GetController() ?? p1;
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Battlefield);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Graveyard);
                }
                GameBoard.LinkAuraSystem.Detach();
                board.Dispose();
            }
        }

        private static void TestBoard(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            // ---- 1. 布局静态：计数 / 语义格坐标 / 180° 对称 ----
            int edge = 0, unit = 0, land = 0, special = 0;
            for (int z = 0; z < BoardMath.Height; z++)
                for (int x = 0; x < BoardMath.Width; x++)
                {
                    switch (BoardLayout.RoleOf(x, z))
                    {
                        case CellRole.Edge: edge++; break;
                        case CellRole.Unit: unit++; break;
                        case CellRole.Land: land++; break;
                        default: special++; break;
                    }
                }
            Assert(edge == 38 && unit == 36 && land == 18 && special == 12,
                   $"布局计数 104 = 边缘{edge} + 单位{unit} + 地牌{land} + 特殊{special}");

            Assert(BoardLayout.RoleOf(11, 6) == CellRole.Character && BoardLayout.OwnerOf(11, 6) == 0, "P1 角色格 = (11,6)");
            Assert(BoardLayout.RoleOf(1, 6) == CellRole.Activation && BoardLayout.OwnerOf(1, 6) == 0, "P1 发动格 = (1,6)");
            Assert(BoardLayout.RoleOf(11, 5) == CellRole.Deck && BoardLayout.OwnerOf(11, 5) == 0, "P1 卡组格 = (11,5)");
            Assert(BoardLayout.RoleOf(11, 2) == CellRole.Graveyard && BoardLayout.OwnerOf(11, 2) == 1, "P2 墓地格 = (11,2)");
            Assert(BoardLayout.RoleOf(1, 1) == CellRole.Character && BoardLayout.OwnerOf(1, 1) == 1, "P2 角色格 = (1,1)");
            Assert(BoardLayout.RoleOf(11, 1) == CellRole.Activation && BoardLayout.OwnerOf(11, 1) == 1, "P2 发动格 = (11,1)");

            bool symmetric = true;
            for (int z = 0; z < BoardMath.Height && symmetric; z++)
                for (int x = 0; x < BoardMath.Width && symmetric; x++)
                {
                    var (rx, rz) = BoardLayout.Rotate180(x, z);
                    if (BoardLayout.RoleOf(rx, rz) != BoardLayout.RoleOf(x, z)) symmetric = false;
                    int o = BoardLayout.OwnerOf(x, z);
                    if (o != -1 && BoardLayout.OwnerOf(rx, rz) != 1 - o) symmetric = false;
                }
            Assert(symmetric, "全盘 180° 旋转对称（角色一致、归属互补）");
            Assert(BoardLayout.UnitCellsPerPlayer == ZoneManager.BattlefieldCapacityPerPlayer,
                   "单位格数 18 = 核心战场容量 18（计数与格子天然一致）");

            // ---- 2. 六边形数学（手算向量表，odd-r 奇偶约定） ----
            Assert(BoardMath.Neighbor(2, 4, BoardDirection.NE) == (2, 5), "偶行 NE 邻居 = (x, z+1)");
            Assert(BoardMath.Neighbor(2, 5, BoardDirection.NE) == (3, 6), "奇行 NE 邻居 = (x+1, z+1)");
            Assert(BoardMath.Neighbor(2, 5, BoardDirection.SW) == (2, 4), "SW 与 NE 互逆（odd-r 邻接回路）");
            Assert(BoardMath.HexDistance(2, 4, 3, 4) == 1, "同行相邻格距离 1");
            Assert(BoardMath.HexDistance(2, 4, 2, 5) == 1, "跨行邻接距离 1");
            Assert(BoardMath.HexDistance(2, 4, 4, 4) == 2, "同行隔一格距离 2（默认射程边界）");
            Assert(BoardMath.HexDistance(2, 4, 5, 4) == 3, "同行隔两格距离 3（默认射程外）");

            // ---- 3. 距离档位（战棋预留公式）与半场拼合 ----
            var half1 = new HalfFieldData();
            half1.Set(5, 0, 2, 0); // 本地(5,0) → 整场 (6,4)，落差 2
            var board = new BoardState(core, p1, p2, half1, HalfFieldData.Flat());
            Assert(board.Field.ElevationAt(6, 4) == 2 && board.Field.ElevationAt(6, 5) == 0,
                   "半场地形拼合（落差写入正确格，边缘恒 0）");
            board.Mode = BoardRulesMode.Flat;
            Assert(board.AttackDistance(6, 4, 6, 5) == 1, "Flat 档忽略落差（P2P 完全平面）");
            board.Mode = BoardRulesMode.Tactical;
            Assert(board.AttackDistance(6, 4, 6, 5) == 3, "Tactical 档落差参与距离（六距1 + |2−0| = 3）");
            board.Mode = BoardRulesMode.Flat;

            // ---- 4. 确定性重建（重连语义）+ 占用与区域一致 ----
            board.Resync();
            Assert(board.IsConsistent(), "占用双向索引成对一致");
            string snap1 = BoardSnapshot(board);
            board.Resync();
            Assert(snap1 == BoardSnapshot(board), "Resync 幂等（同状态两次重建逐格一致 = 重连零成本）");

            foreach (var c in core.ZoneManager.GetCards(p1, Zone.Battlefield))
            {
                bool onCell = board.TryGetCell(c, out int bx, out int bz)
                              && BoardLayout.RoleOf(bx, bz) == CellRole.Unit && BoardLayout.OwnerOf(bx, bz) == 0;
                Assert(onCell, $"战场卡落在 P1 单位区格（{c.ID}）");
            }
            foreach (var landCard in core.ElementPool.GetPooledCards(p1))
            {
                bool onCell = board.TryGetCell(landCard.SourceCard, out int lx, out int lz)
                              && BoardLayout.RoleOf(lx, lz) == CellRole.Land && BoardLayout.OwnerOf(lx, lz) == 0;
                Assert(onCell, $"地牌落在 P1 地牌行格（{landCard.SourceCard.ID}）");
            }

            // ---- 5. 发动区流转：事件序 / 暂态不跨调用 / 终态 ----
            var pool1 = core.ElementPool.GetPool(p1);
            foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99;
            var creatureData = cardsData.FirstOrDefault(c => c.ID == "TEST_GREEN_003");
            if (creatureData != null)
            {
                var creature = new CardWrapper(creatureData);
                creature.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(creature, Zone.Hand);

                var order = new List<string>();
                System.Action<CardEnterActivationEvent> onEnter = _ => order.Add("Enter");
                System.Action<CardPlayEvent> onPlay = _ => order.Add("Play");
                System.Action<CardLeaveActivationEvent> onLeave = _ => order.Add("Leave");
                EventManager.Instance.Subscribe(onEnter);
                EventManager.Instance.Subscribe(onPlay);
                EventManager.Instance.Subscribe(onLeave);
                try
                {
                    Assert(PlayCardSync(core, p1, creature), "棋盘段生物成功打出");
                }
                finally
                {
                    EventManager.Instance.Unsubscribe(onEnter);
                    EventManager.Instance.Unsubscribe(onPlay);
                    EventManager.Instance.Unsubscribe(onLeave);
                }

                Assert(order.SequenceEqual(new[] { "Enter", "Play", "Leave" }),
                       $"发动区流转事件序 Enter→Play→Leave（实际 {string.Join("→", order)}）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "调用结束后发动区为空（暂态不跨调用）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(creature), "永久物终态在战场");
                Assert(creature.GetZone() == Zone.Battlefield, "_zone 随容器维护（GetZone 正确）");

                board.Resync();
                Assert(board.TryGetCell(creature, out int cx, out int cz)
                       && BoardLayout.RoleOf(cx, cz) == CellRole.Unit,
                       "Resync 后新入场卡获得单位格");
            }

            var spellData = cardsData.FirstOrDefault(c => c.ID == "TEST_SPELL_RED_001");
            if (spellData != null)
            {
                var spell = new CardWrapper(spellData);
                spell.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(spell, Zone.Hand);
                Assert(PlayCardSync(core, p1, spell), "棋盘段法术成功打出");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(spell), "法术终态入墓地");
                Assert(spell.GetZone() == Zone.Graveyard, "法术 GetZone == Graveyard");
                Assert(core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "法术结算后发动区为空");
            }

            // ---- 6. _zone 维护回归（不再双挂） ----
            var handNow = core.ZoneManager.GetCards(p1, Zone.Hand);
            Assert(handNow.All(c => c.GetZone() == Zone.Hand), "_zone 修复：手牌卡 GetZone==Hand");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).All(c => c.GetZone() == Zone.Graveyard),
                   "_zone 修复：墓地卡 GetZone==Graveyard");
            if (handNow.Count > 0)
            {
                var mover = handNow[0];
                core.ZoneManager.MoveCard(mover, p1, Zone.Hand, Zone.Exile);
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handNow.Count - 1,
                       "_zone 修复：移出后源区计数 −1（不再双挂旧列表）");
                Assert(mover.GetZone() == Zone.Exile, "_zone 修复：除外后 GetZone==Exile");
            }

            // ---- 7. 容量闸门：手动预检拒绝 / 结算时入场失败入墓 ----
            var container1 = core.ZoneManager.GetZoneContainer(p1);
            var fillers = new List<Card>();
            int fill = ZoneManager.BattlefieldCapacityPerPlayer - container1.GetCount(Zone.Battlefield);
            for (int i = 0; i < fill; i++)
            {
                var filler = new Card { ID = "VERIFY_FILLER" };
                filler.SetController(p1);
                container1.Add(filler, Zone.Battlefield);
                fillers.Add(filler);
            }
            Assert(!core.ZoneManager.HasBattlefieldSpace(p1), "战场填满 18 后无空位");

            if (creatureData != null)
            {
                var blocked = new CardWrapper(creatureData);
                blocked.SetController(p1);
                container1.Add(blocked, Zone.Hand);
                int bankBefore = pool1.AvailableMana.Values.Sum();
                Assert(!PlayCardSync(core, p1, blocked), "满场手动出牌被拒（MD 式预检）");
                Assert(pool1.AvailableMana.Values.Sum() == bankBefore, "被拒时费用未扣");
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Contains(blocked), "被拒时卡留在手牌");
            }

            bool failedFired = false;
            System.Action<CardActivationFailedEvent> onFail = _ => failedFired = true;
            EventManager.Instance.Subscribe(onFail);
            var tokenCard = new Card { ID = "VERIFY_TOKEN" };
            try
            {
                Assert(!core.ZoneManager.TryAddToBattlefield(tokenCard, p1), "满场时结算入场（衍生物）失败");
            }
            finally
            {
                EventManager.Instance.Unsubscribe(onFail);
            }
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(tokenCard), "入场失败 → 进墓地");
            Assert(failedFired, "入场失败事件已发布");

            // ---- 8. 离场清理（派生层自动释放） ----
            var victim = core.ZoneManager.GetCards(p1, Zone.Battlefield).FirstOrDefault();
            if (victim != null)
            {
                core.ZoneManager.MoveCard(victim, p1, Zone.Battlefield, Zone.Graveyard);
                board.Resync();
                Assert(!board.TryGetCell(victim, out _, out _), "离场卡不再占用格子");
            }

            // 清理填充物（保证重复运行稳定）
            foreach (var f in fillers) container1.Remove(f, Zone.Battlefield);
            container1.Remove(tokenCard, Zone.Graveyard);
        }

        /// <summary>棋盘占用快照（排序串，用于确定性对比）</summary>
        private static string BoardSnapshot(BoardState board)
        {
            var entries = new List<string>();
            for (int z = 0; z < BoardMath.Height; z++)
                for (int x = 0; x < BoardMath.Width; x++)
                {
                    var role = BoardLayout.RoleOf(x, z);
                    if (role != CellRole.Unit && role != CellRole.Land) continue;
                    var card = board.CardAt(x, z);
                    if (card != null) entries.Add($"{card.ID}@{x},{z}");
                }
            entries.Sort();
            return string.Join("|", entries);
        }

        // ======================================== 分支条件目录 + 信息族（Phase A） ========================================

        /// <summary>
        /// 分支目录契约验证：三族覆盖、与设计文稿数值一致、目录↔代码同步契约、维度有限域。
        /// </summary>
        private static void TestBranchCatalog()
        {
            var all = BranchConfigTable.GetAll().ToList();
            Assert(all.Count >= 8, $"BranchConfig 目录加载（{all.Count} 个效果条目 ≥ 8）");
            if (all.Count == 0) return;

            Assert(BranchConfigTable.GetByEffectType("DealDamage")?.Conditions.Count(c => c.Kind == BranchConditionKind.OutcomeGate) == 3,
                   "伤害族 3 条件（DmgKillsTarget/TargetSurvived/Overkill）");
            Assert(BranchConfigTable.GetByEffectType("PierceDamage")?.Conditions.Count >= 2, "穿透伤害仍可挂伤害分支");
            Assert(BranchConfigTable.GetByEffectType("Heal")?.Conditions.Count >= 2, "治疗族 2 条件（Overheal/TargetStillWounded）");
            Assert(BranchConfigTable.GetByEffectType("DeclareHand")?.Conditions.Count >= 2, "宣言族 2 条件（DeclareHit/DeclareMiss）");
            Assert(BranchConfigTable.GetByEffectType("ProphecyNextCard")?.Conditions.Count >= 2, "预言族 2 条件（ProphecyHit/ProphecyMiss）");

            Assert(BranchConfigTable.GetDrawback("UnusableThisTurn")?.CostReduction == 1
                   && BranchConfigTable.GetDrawback("DiscardAtEndOfTurnIfInHand")?.CostReduction == 1,
                   "抽牌减费缺陷 ×2 各 −1（设计文稿值）");
            // 检索改宣言卡名（ExactCard 单档；TypePlusRace/SingleDimension 不再可表达，已删）
            Assert(BranchConfigTable.GetFilterTier("ExactCard")?.Cost == 3
                   && BranchConfigTable.GetFilterTier("TypePlusRace") == null
                   && BranchConfigTable.GetFilterTier("SingleDimension") == null,
                   "检索维度收敛 ExactCard 单档=3（宣言卡名）");

            // 同步契约：目录项全在原子表 ∩ 产出族；条件 id 全被评估器识别（与 BranchConfigTable 自检同源）
            bool contract = true;
            foreach (var cfg in all)
            {
                if (CardCore.Attribute.AtomicEffectTable.GetByEnumName(cfg.EffectTypeName) == null) contract = false;
                else if (!BranchConfigTable.OutcomeProducerTypes.Contains(cfg.EffectTypeName)) contract = false;
                else
                    foreach (var c in cfg.Conditions)
                        if (c.Kind == BranchConditionKind.OutcomeGate && !BranchConditionEvaluator.IsKnownCondition(c.Id))
                            contract = false;
            }
            Assert(contract, "同步契约：目录项全在原子表 ∩ 产出族，条件 id 全被评估器识别");

            // 不可挂族不在目录（改值/授予/移动系——设计文稿：无可串联条件）
            Assert(BranchConfigTable.GetByEffectType("ModifyPower") == null
                   && BranchConfigTable.GetByEffectType("DrawCard") == null
                   && BranchConfigTable.GetByEffectType("SearchDeck") == null,
                   "改值/移动/检索系不在分支目录（Drawback 与 FilterPrecision 另有挂载机制）");

            // 信息族原子已入原子表（JSON → 运行时表全链路；宣言手牌族只保留一档，探查手牌已删）
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareHand") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareDeckTop") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareArrow") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("ProphecyNextCard") != null,
                   "信息族 4 原子已入原子表（ID 由描述哈希生成）");
            // 剧毒指示物化（Poison 原子）与毒素/沉默/净化/穿透同链入表
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("Poison") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddToxin") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("Silence") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("Purify") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("PierceDamage") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("SetCost") != null,
                   "新语义六原子已入表（剧毒指示物/毒素/沉默/净化/穿透/设置费用）");
            // 三轨制同期（2026-09-09）：无效指示物（蓝3）入表 + 设置系三原子 handler 已注册复活
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddNullify") != null,
                   "无效指示物原子已入表（蓝3，非启动式能力无法发动）");
            Assert(CardCore.Attribute.EffectHandlerRegistry.TryGetHandler(CardCore.AtomicEffectType.SetPower, out _)
                   && CardCore.Attribute.EffectHandlerRegistry.TryGetHandler(CardCore.AtomicEffectType.SetLife, out _)
                   && CardCore.Attribute.EffectHandlerRegistry.TryGetHandler(CardCore.AtomicEffectType.SetCost, out _)
                   && CardCore.Attribute.EffectHandlerRegistry.TryGetHandler(CardCore.AtomicEffectType.AddNullify, out _),
                   "设置系三原子复活注册 + 无效原子注册（三轨制重建）");

            // ---- 维度有限域（宣言对象必须是有限明确范围；卡名等开放集被排除） ----
            Assert(ProphecyDimension.IsValidValue("Type", "Creature") && !ProphecyDimension.IsValidValue("Type", "Bogus"), "维度 Type 域校验");
            Assert(ProphecyDimension.IsValidValue("Color", "Red") && !ProphecyDimension.IsValidValue("Color", "Pink"), "维度 Color 域校验");
            Assert(ProphecyDimension.IsValidValue("CostParity", "Odd") && !ProphecyDimension.IsValidValue("CostParity", "Maybe"), "维度 CostParity 域校验");
            Assert(ProphecyDimension.IsValidValue("CostExact", "9") && !ProphecyDimension.IsValidValue("CostExact", "10"), "维度 CostExact 域 0..9");
            Assert(ProphecyDimension.IsValidValue("LinkArrow", "Up") && !ProphecyDimension.IsValidValue("LinkArrow", "All"), "维度 LinkArrow 限单方向（CardCore.HexDirection 成员名）");
            Assert(ProphecyDimension.TryParse("Type:Creature", out var dim, out var val) && dim == "Type" && val == "Creature", "编码解析 维度:值");
        }

        /// <summary>
        /// 信息族引擎流验证（合成卡，不依赖卡表）：
        /// - 宣言：DeclareHand + DeclareHit 门 → 即时结算 then 奖励；
        /// - 预言：ProphecyNextCard + ProphecyHit 门 → 延迟注册（押注隐藏），
        ///   对手下回合首张出牌验证命中/未命中；整回合未出牌作未命中走 else。
        /// </summary>
        private static void TestInformationFlow(GameCore core, Player p1, Player p2)
        {
            var container1 = core.ZoneManager.GetZoneContainer(p1);
            var container2 = core.ZoneManager.GetZoneContainer(p2);
            var pool1 = core.ElementPool.GetPool(p1);
            foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 9;

            // 合成对手手牌：一张生物（红 2 费）+ 一张法术（蓝 1 费）
            var creatureData = new CardData { ID = "VERIFY_INFO_CREATURE", CardName = "验证生物" };
            creatureData.Supertype = Cardtype.Creature;
            creatureData.Cost[(int)ManaType.Red] = 2;
            var spellData = new CardData { ID = "VERIFY_INFO_SPELL", CardName = "验证法术" };
            spellData.Supertype = Cardtype.Spell;
            spellData.Cost[(int)ManaType.Blue] = 1;
            var foeCreature = new CardWrapper(creatureData);
            var foeSpell = new CardWrapper(spellData);
            foeCreature.SetController(p2);
            foeSpell.SetController(p2);
            container2.Add(foeCreature, Zone.Hand);
            container2.Add(foeSpell, Zone.Hand);

            // p1 牌库放 3 张白卡（DrawCard 奖励的可观测载体）
            for (int i = 0; i < 3; i++) container1.Add(new Card { ID = "VERIFY_INFO_DECK" }, Zone.Deck);

            var executor = core.StackEngine.GetExecutor();

            // 单张验证确定性：A 段选位器固定指定注入的生物卡；A2 递进到法术卡
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker =
                (_, cands) => cands.Contains(foeCreature) ? foeCreature : cands[0];

            // ---- A. 宣言（即时验证）：宣言 类型:Creature → 命中 → then 抽 1 ----
            var declareDef = new EffectDefinition
            {
                Id = "VERIFY_DECLARE",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.DeclareHand, StringValue = "Type:Creature" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "DeclareHit",
                        Then = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = declareDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore + 1,
                   "宣言命中（对手手牌含生物）→ then 抽 1 即时结算");

            // ---- A2. 已展示递进（单张验证，不展示全部）：已确认的卡不再可指定 → 下一宣言取下一张 ----
            Assert(foeCreature.IsRevealed && !foeSpell.IsRevealed,
                   "宣言确认：翻开的卡永久已展示，其余保持未展示（不展示全部）");
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker =
                (_, cands) => cands.Contains(foeSpell) ? foeSpell : cands[0];
            handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = declareDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore && foeSpell.IsRevealed,
                   "宣言递进：已展示卡跳过 → 指定法术卡 → Type:Creature 未命中 → 无奖励");

            // ---- B. 预言（延迟验证）：押注 颜色:红，命中走 then 抽 1 ----
            var prophecyDef = new EffectDefinition
            {
                Id = "VERIFY_PROPHECY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.ProphecyNextCard, StringValue = "Color:Red" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "ProphecyHit",
                        Then = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            ProphecySystem.Reset();
            handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = prophecyDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();

            Assert(ProphecySystem.Pending.Count == 1, "预言已注册为待验证押注");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore,
                   "预言押注时刻不结算奖励（延迟验证）");

            // 模拟对手回合开始 → 首张打出红费生物 → 验证命中
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p2, TurnNumber = 99 });
            EventManager.Instance.Publish(new CardPlayEvent { Player = p2, PlayedCard = foeCreature });
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore + 1,
                   "对手首张出红卡 → 预言命中 → then 抽 1（验证时刻结算）");
            Assert(ProphecySystem.Pending.Count == 0, "验证后预言出列");

            // ---- C. 预言到期：整回合未出牌 → 未命中走 else ----
            var expiryDef = new EffectDefinition
            {
                Id = "VERIFY_PROPHECY_EXPIRY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.ProphecyNextCard, StringValue = "Color:Green" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "ProphecyHit",
                        Else = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = expiryDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p2, TurnNumber = 100 });
            EventManager.Instance.Publish(new TurnEndEvent { TurnPlayer = p2, TurnNumber = 100 });
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore + 1,
                   "对手整回合未出牌 → 预言作未命中 → else 抽 1（用户定案）");

            // 清理：合成卡不残留
            container2.Remove(foeCreature, Zone.Hand);
            container2.Remove(foeSpell, Zone.Hand);
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker = null; // 还原缺省选位器
            ProphecySystem.Reset();
        }

        // ======================================== 关键词行为（合成随从） ========================================

        /// <summary>
        /// 关键词行为验证（合成随从，不依赖卡表）：横置可用性/冲锋/突袭/嘲讽/潜行/警戒/
        /// 先攻/连击/碾压/毒刺/吸血/系命/圣盾/坚韧/护甲/不灭/复生/再生/成长/辟邪/法术护盾。
        /// 守卫/风怒已删除（2026-09-03 原子表整体修正）；剧毒改指示物（毒素/剧毒回合结束结算）；
        /// 穿透伤害/沉默/净化/换区清除（属性指示物反向回写）同段验证。
        /// 死亡交互（剧毒×不灭×复生）另见 DeathRules 决策表。
        /// </summary>
        private static void TestKeywords(GameCore core, Player p1, Player p2)
        {
            var combat = core.CombatSystem;
            var used = new List<Card>();

            // 合成随从：进场（带失调）+ 登记（测试后统一清理）
            Card Make(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData { ID = "VERIFY_KW_" + used.Count, CardName = "KW" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = power;
                data.Life = life;
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                core.ZoneManager.TryAddToBattlefield(card, owner);
                card.Untap(); // 新规则横置入场；本段测试前提 = 已过回合重置的竖直随从（横置行为单独断言）
                used.Add(card);
                return card;
            }

            void CleanKeywords()
            {
                foreach (var c in used)
                    core.ZoneManager.GetZoneContainer(c.GetController() ?? p1).Remove(c, Zone.Battlefield);
            }

            // ---- 1. 表与工厂 ----
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantReborn") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantIndestructible") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantLifelink") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddArmor") != null,
                   "新关键词（复生/不灭/系命）与护甲原子已入表");
            Assert(CardCore.Attribute.Handlers.GrantKeywordHandlerFactory.TryGetKeywordId(AtomicEffectType.GrantReborn, out var rebornId) && rebornId == "Reborn",
                   "工厂登记复生关键词 id");

            // ---- 2. 横置可用性 / 冲锋 / 突袭（2026-09-08 定案：一律横置入场；冲锋/突袭=登场效果，无入场豁免） ----
            combat.StartCombat(p1, p2);
            var tappedUnit = Make(p1, 3, 3);
            tappedUnit.Tap(); // 模拟横置在场（未过回合重置）
            Assert(!combat.CanDeclareAttack(tappedUnit, p1), "横置随从不可攻击（可用性统一走横置）");
            var readyUnit = Make(p1, 3, 3);
            Assert(combat.CanDeclareAttack(readyUnit, p1), "未横置随从可攻击");

            // 入场（直接观察 TryAddToBattlefield 行为，不经 Make 的竖置前提）
            CardWrapper MakeEntry(params CardEffectData[] effects)
            {
                var data = new CardData { ID = "VERIFY_KW_ENTRY_" + used.Count, CardName = "入场" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = 3; data.Life = 3;
                if (effects != null && effects.Length > 0)
                {
                    data.Effects ??= new List<CardEffectData>();
                    data.Effects.AddRange(effects);
                }
                var card = new CardWrapper(data);
                card.SetController(p1);
                // 经发动区真入场（CastPlayed 派生）：OnPlay 载荷过滤只认 CastPlayed——
                // 直接 TryAddToBattlefield 是 TokenSpawned，冲锋/突袭的登场效果（OnPlay）不会触发
                // （锚在 f1eb82c 写下时即用错路径，存量修正）
                core.ZoneManager.GetZoneContainer(p1).Add(card, Zone.Activation);
                core.ZoneManager.TryMoveToBattlefield(card, p1, Zone.Activation);
                used.Add(card);
                return card;
            }

            // 冲锋/突袭的登场效果（2026-09-08）：OnPlay + 激励自己（Untap→Self）；
            // 突袭另自上紊乱指示物作代价减费（费用经 CostDerivationService 的 Self 紊乱对冲）
            CardEffectData EntryReadyEffect(bool withSickness)
            {
                var eff = new CardEffectData
                {
                    Id = "VERIFY_ENTRY_READY",
                    DisplayName = withSickness ? "突袭" : "冲锋",
                    Description = "登场：激励自身——解除横置",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                };
                eff.AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry
                    {
                        EffectType = AtomicEffectType.Untap.ToString(),
                        Value = 1,
                        TargetTypeOverride = (int)CardCore.Attribute.EffectTargetType.Self,
                    },
                };
                if (withSickness)
                    eff.AtomicEffects.Add(new AtomicEffectEntry
                    {
                        EffectType = AtomicEffectType.RushSickness.ToString(),
                        Value = 1,
                        TargetTypeOverride = (int)CardCore.Attribute.EffectTargetType.Self,
                    });
                return eff;
            }

            var plain = MakeEntry();
            Assert(plain.IsTapped(), "普通随从：一律横置入场");
            var charger = MakeEntry(EntryReadyEffect(false));
            Assert(charger.IsTapped(), "冲锋（登场效果）：入场时仍横置（结算前无豁免）");
            GameActions.DrainStack(core); // 排干栈：登场效果结算
            Assert(!charger.IsTapped(), "冲锋（登场效果）：结算后解除横置（激励自己）");
            Assert(combat.CanDeclareAttack(charger, p1) && combat.CanAttackTarget(charger, p2),
                   "冲锋：无目标限制，可攻击玩家");
            var rusher = MakeEntry(EntryReadyEffect(true));
            var enemy = Make(p2, 1, 9);
            GameActions.DrainStack(core);
            Assert(!rusher.IsTapped()
                   && rusher.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 1,
                   "突袭（登场效果）：结算后解除横置、自上紊乱指示物");
            Assert(combat.CanAttackTarget(rusher, enemy) && !combat.CanAttackTarget(rusher, p2),
                   "突袭：紊乱期间只能攻随从，不准攻击玩家（效果发动同口径）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager); // 回合结束持续指示物清理
            Assert(rusher.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 0
                   && combat.CanAttackTarget(rusher, p2),
                   "突袭：紊乱消退（持续到回合结束）后目标限制解除");
            combat.EndCombat();

            // ---- 3. 嘲讽 / 碾压无视嘲讽 ----
            combat.StartCombat(p1, p2);
            var attacker = Make(p1, 3, 3);
            var taunter = Make(p2, 0, 9, "Taunt");
            Assert(!combat.CanAttackTarget(attacker, p2), "嘲讽：防守方有嘲讽随从时不能指定玩家");
            var overwhelmer = Make(p1, 3, 3, "Overwhelm");
            Assert(combat.CanAttackTarget(overwhelmer, p2), "碾压：无视嘲讽");
            taunter.IsAlive = false; // 移除嘲讽者
            Assert(combat.CanAttackTarget(attacker, p2), "嘲讽随从清除后可指定玩家");
            combat.EndCombat();

            // ---- 4. 守卫关键词已删除（2026-09-03 原子表整体修正）：无守卫转移，攻击目标保持宣言 ----
            combat.StartCombat(p1, p2);
            var striker = Make(p1, 3, 3);
            var victim = Make(p2, 2, 5);
            var guardDoppel = Make(p2, 1, 8); // 同位置单位（无 Guard 关键词——已删除）
            combat.DeclareAttack(striker, victim);
            var participant = combat.Attackers.FirstOrDefault(a => a.Entity == striker);
            Assert(participant != null && participant.DeclaredTarget == victim && !guardDoppel.IsTapped(),
                   "守卫删除：攻击目标保持宣言（无转移、无横置旁观者）");
            combat.ExecuteDamage();
            combat.EndCombat();

            // ---- 5. 潜行：不可被指定 + 攻击后移除 ----
            combat.StartCombat(p1, p2);
            var lurker = Make(p2, 2, 2, "Stealth");
            var hunter = Make(p1, 3, 3);
            Assert(!combat.CanAttackTarget(hunter, lurker), "潜行：不可被指定为攻击目标");
            var spy = Make(p1, 2, 2, "Stealth");
            combat.DeclareAttack(spy, p2);
            Assert(!spy.HasKeyword("Stealth"), "潜行：攻击后移除");
            combat.ExecuteDamage();
            combat.EndCombat();

            // ---- 6. 警戒：攻击不横置（一回合一次） ----
            combat.StartCombat(p1, p2);
            var vigilant = Make(p1, 3, 3, "Vigilance");
            combat.DeclareAttack(vigilant, p2);
            Assert(!vigilant.IsTapped(), "警戒：攻击不横置");
            Assert(CardCore.Attribute.KeywordRules.ShouldTap(vigilant), "警戒：一回合只生效一次（额度已耗）");
            combat.ExecuteDamage();
            combat.EndCombat();

            // ---- 7. 风怒关键词已删除：每回合攻击上限恒 1，攻击后不重置 ----
            combat.StartCombat(p1, p2);
            var loneWolf = Make(p1, 1, 9);
            combat.DeclareAttack(loneWolf, p2);
            combat.ExecuteDamage();
            Assert(loneWolf.IsTapped() && loneWolf.AttacksThisTurn == 1
                   && CardCore.CombatSystem.MaxAttacksPerTurn(loneWolf) == 1,
                   "风怒删除：攻击后保持横置、每回合上限 1");

            // ---- 7b. 穿透伤害（原"不可防止伤害"改名）：越过关键词与指示物，替代层照走 ----
            var wardedTank = Make(p2, 1, 20, "DivineShield");
            wardedTank.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
            wardedTank.AddKeyword("Armor"); // 坚韧 −1
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, wardedTank, 5, false, pierce: true);
            Assert(wardedTank.IsAlive && wardedTank.GetLife() == 15 && wardedTank.HasKeyword("DivineShield")
                   && wardedTank.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 3,
                   "穿透：圣盾/护甲/坚韧全被越过（20−5=15，防护层不消耗）");
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, wardedTank, 5, false); // 普通路径对照
            Assert(wardedTank.GetLife() == 15 && !wardedTank.HasKeyword("DivineShield"),
                   "普通伤害对照：圣盾挡下一次并消耗");
            combat.EndCombat();

            // ---- 8. 先攻：目标死亡不反击 ----
            combat.StartCombat(p1, p2);
            var first = Make(p1, 5, 3, "FirstStrike");
            var bulky = Make(p2, 4, 3);
            bool declared8 = combat.DeclareAttack(first, bulky);
            UnityEngine.Debug.Log($"[CMBTDBG] declared={declared8} attackers={combat.Attackers.Count} first._power={first.GetPower()} layerPower={core.LayerEngine.CalculatePower(first)} firstTapped={first.IsTapped()} bulkyAlive={bulky.IsAlive} bulkyLife={bulky.GetLife()}");
            combat.ExecuteDamage();
            UnityEngine.Debug.Log($"[CMBTDBG] after ExecuteDamage: bulkyAlive={bulky.IsAlive} bulkyLife={bulky.GetLife()} firstLife={first.GetLife()}");
            Assert(!bulky.IsAlive && first.GetLife() == 3, "先攻：目标死于先攻步，不反击");

            // ---- 8b. 缴械：被攻击的目标无法反击 ----
            combat.StartCombat(p1, p2);
            var disarmer = Make(p1, 3, 5, "Disarm");
            var bigGuard = Make(p2, 4, 9);
            combat.DeclareAttack(disarmer, bigGuard);
            combat.ExecuteDamage();
            Assert(disarmer.GetLife() == 5 && bigGuard.GetLife() == 6,
                   "缴械：目标（4 攻）无法反击，攻击者无伤（单向伤害）");
            combat.EndCombat();

            // ---- 8c. 反击资格：已横置的随从只能挨打（不反击、无消耗） ----
            combat.StartCombat(p1, p2);
            var aggressor = Make(p1, 3, 5);
            var tired = Make(p2, 4, 9);
            tired.Tap(); // 模拟已横置（刚攻击过/被冻结）
            combat.DeclareAttack(aggressor, tired);
            combat.ExecuteDamage();
            Assert(aggressor.GetLife() == 5 && tired.GetLife() == 6,
                   "反击资格：已横置目标（4 攻）不反击，攻击者无伤");
            combat.EndCombat();
            combat.EndCombat();

            // ---- 9. 连击：两步各结算一次 ----
            combat.StartCombat(p1, p2);
            var doubleS = Make(p1, 2, 9, "DoubleStrike");
            var tank = Make(p2, 1, 5);
            combat.DeclareAttack(doubleS, tank);
            combat.ExecuteDamage();
            Assert(tank.GetLife() == 1 && doubleS.IsAlive, "连击：伤害结算两次（5命 −2×2 = 1）");
            combat.EndCombat();

            // ---- 11. 碾压：邻接受击（注入邻接扩展点） ----
            combat.StartCombat(p1, p2);
            var hammer = Make(p1, 4, 9, "Overwhelm");
            var pivot = Make(p2, 1, 9);
            var neighbor = Make(p2, 1, 9);
            CardCore.CombatSystem.AdjacentResolver = c => c == pivot ? new[] { neighbor } : System.Array.Empty<Card>();
            combat.DeclareAttack(hammer, pivot);
            combat.ExecuteDamage();
            Assert(pivot.GetLife() == 5 && neighbor.GetLife() == 5, "碾压：目标与相邻随从各受 4 点（无反击）");
            CardCore.CombatSystem.AdjacentResolver = null;
            combat.EndCombat();

            // ---- 12. 毒刺（原剧毒关键词替换）：战斗伤害后附加毒素指示物（回合结束每层 1 伤） ----
            combat.StartCombat(p1, p2);
            var viper = Make(p1, 1, 9, "PoisonSting");
            var giant = Make(p2, 3, 10);
            combat.DeclareAttack(viper, giant);
            combat.ExecuteDamage();
            Assert(giant.IsAlive && giant.GetLife() == 9 && viper.GetLife() == 6
                   && giant.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 1,
                   "毒刺：战斗伤害后目标附 1 层毒素（伤害本身照常，反击正常）");
            // 毒素回合结束结算：每个回合末每层 1 伤，ForTurns=3 计时
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(giant.GetLife() == 8, "毒素：回合结束每层受 1 点伤害");
            combat.EndCombat();

            // ---- 12b. 毒素 3 层时钟：第 3 个回合末到期消失 ----
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(giant.GetLife() == 7 && giant.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 1,
                   "毒素：第 2 个回合末仍跳伤（层未到期）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(giant.IsAlive && giant.GetLife() == 6 && giant.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 0,
                   "毒素：第 3 个回合末跳伤后到期消失");

            // ---- 12c. 剧毒指示物（新语义）：回合结束时持有者死亡（效果死亡、无伤害来源） ----
            var plagued = Make(p2, 3, 10);
            plagued.AddCounters(CardCore.Attribute.CounterRules.PoisonCounter, 1);
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(!plagued.IsAlive, "剧毒指示物：回合结束时死亡（不再造成即时伤害）");

            // ---- 13. 吸血（恢复自身）/ 系命（回复角色） ----
            combat.StartCombat(p1, p2);
            var bat = Make(p1, 2, 3, "Lifesteal");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, bat, 2, false); // 受伤状态
            var prey = Make(p2, 0, 9);
            combat.DeclareAttack(bat, prey);
            combat.ExecuteDamage();
            Assert(bat.GetLife() == 3, "吸血：造成 2 伤害恢复随从自身");
            combat.EndCombat();

            combat.StartCombat(p1, p2);
            p1.Life = 20;
            var monk = Make(p1, 3, 3, "Lifelink");
            combat.DeclareAttack(monk, p2);
            combat.ExecuteDamage();
            Assert(p1.Life == 23, "系命：造成 3 伤害回复角色");
            combat.EndCombat();

            // ---- 14. 圣盾 / 坚韧 / 护甲指示物 ----
            combat.StartCombat(p1, p2);
            var shielded = Make(p2, 1, 5, "DivineShield");
            var breaker = Make(p1, 4, 9);
            combat.DeclareAttack(breaker, shielded);
            combat.ExecuteDamage();
            Assert(shielded.IsAlive && shielded.GetLife() == 5 && !shielded.HasKeyword("DivineShield"),
                   "圣盾：挡下一次伤害并消耗");
            breaker.Untap();
            combat.StartCombat(p1, p2);
            combat.DeclareAttack(breaker, shielded);
            combat.ExecuteDamage();
            Assert(shielded.GetLife() == 1, "圣盾消耗后正常受伤");
            combat.EndCombat();

            combat.StartCombat(p1, p2);
            var tough = Make(p2, 1, 9, "Armor"); // 坚韧 −1
            var hitter = Make(p1, 4, 9);
            combat.DeclareAttack(hitter, tough);
            combat.ExecuteDamage();
            Assert(tough.GetLife() == 6, "坚韧：最终伤害 −1（9 −3 = 6）");
            combat.EndCombat();

            // 融合叠加：直接向列表补第二个坚韧（融合继承路径）
            ((IHasKeywords)tough).Keywords.Add("Armor");
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, tough, 4, false);
            Assert(tough.GetLife() == 4, "双坚韧（融合叠加）：伤害 −2");

            var plated = Make(p2, 1, 5);
            plated.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, plated, 2, false);
            Assert(plated.GetLife() == 5 && plated.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 1,
                   "护甲指示物：吸收 2 点后剩余 1（生命未动）");

            // ---- 15. 不灭 / 复生（毁灭原子已删除——用吞噬路径验证不灭拦截） ----
            var eternal = Make(p1, 2, 5, "Indestructible");
            var devourDef = new EffectDefinition
            {
                Id = "VERIFY_DEVOUR",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Devour } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = devourDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { eternal },
            }, skipElementCost: true).Forget();
            Assert(eternal.IsAlive, "不灭：吞噬类消灭无效（毁灭原子已删除，不灭拦消灭族）");

            var phoenix = Make(p1, 2, 5, "Reborn");
            phoenix.IsAlive = false;
            Assert(CardCore.Attribute.KeywordRules.TryReborn(phoenix)
                   && phoenix.IsAlive && phoenix.GetLife() == 1 && phoenix.IsTapped()
                   && !phoenix.HasKeyword("Reborn"),
                   "复生：1 血回场、横置（本回合不可用）、关键词消耗");

            // ---- 15b. 死亡决策表（DeathRules）：死因×护盾 定案断言 ----
            var venomLord = Make(p2, 2, 9, "Indestructible");
            Assert(CardCore.Attribute.DeathRules.TryKill(venomLord, CardCore.Attribute.DeathCause.Poison, p1, core.ZoneManager)
                   && !venomLord.IsAlive,
                   "决策表：剧毒死因不被不灭拦截（伤害族照死——关键词与原子同裁决）");
            var stoneGiant = Make(p2, 2, 9, "Indestructible");
            Assert(!CardCore.Attribute.DeathRules.TryKill(stoneGiant, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager)
                   && stoneGiant.IsAlive,
                   "决策表：不灭拦截摧毁效果死因");
            var ironReborn = Make(p2, 2, 9, "Reborn");
            Assert(!CardCore.Attribute.DeathRules.TryKill(ironReborn, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager)
                   && ironReborn.IsAlive && ironReborn.GetLife() == 1,
                   "决策表：复生对摧毁效果死因生效（消耗回场，TryKill 返回未死）");

            // ---- 15c. 神佑（世界观定案：角色=普通生物单位，免疫来自状态而非硬编码） ----
            // 剧毒已改指示物：神佑拦截"回合结束剧毒死亡"；神佑对净化有抗性（2026-09-09 定案：
            // 净化剥神佑+剧毒组合无法计价平衡——剥除通路关闭，移除留给未来专用效果；RemoveKeyword 手工剥仍可）
            Assert(p1.HasKeyword(CardCore.Attribute.DeathRules.DivineProtection)
                   && p2.HasKeyword(CardCore.Attribute.DeathRules.DivineProtection),
                   "神佑：角色默认持有神佑状态");
            p2.Life = 30;
            p2.AddCounters(CardCore.Attribute.CounterRules.PoisonCounter, 1);
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(p2.Life == 30 && p2.IsAlive
                   && p2.GetCounterCount(CardCore.Attribute.CounterRules.PoisonCounter) == 0,
                   "神佑：回合结束剧毒死亡被拦截（指示物照常到期消失）");
            p2.RemoveKeyword(CardCore.Attribute.DeathRules.DivineProtection);
            p2.AddCounters(CardCore.Attribute.CounterRules.PoisonCounter, 1);
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(p2.Life == 0, "神佑移除后：剧毒指示物对角色致死（免疫只来自状态）");
            p2.Life = 30;
            p2.AddKeyword(CardCore.Attribute.DeathRules.DivineProtection, CardCore.KeywordLane.Status);
            // 神佑抗净化锚：净化打在角色上清指示物/临时状态，但神佑保留
            new CardCore.Attribute.Handlers.PurifyHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.Purify },
                new CardCore.EffectExecutionContext
                {
                    Source = p1, Controller = p1,
                    Targets = new List<Entity> { p2 },
                    ZoneManager = core.ZoneManager
                });
            Assert(p2.HasKeyword(CardCore.Attribute.DeathRules.DivineProtection),
                   "神佑抗净化：净化后神佑仍在（剥神佑+剧毒组合已堵死，移除留给未来专用效果）");

            // ---- 15d. 死亡归因全链路（2026-09-07 定案：死因/死亡来源/伤害来源/效果来源） ----
            // ① 生命流失：即时决策表死亡（非伤害、不可防止），Cause=LifeLoss、死亡来源=效果来源
            var drainSource = Make(p1, 4, 4);
            var drainVictim = Make(p2, 2, 3);
            CardDestroyEvent drainEvt = null;
            void OnDrainKill(CardDestroyEvent e) { if (e.DestroyedCard == drainVictim) drainEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnDrainKill);
            new CardCore.Attribute.Handlers.LifeLossHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.LifeLoss, Value = 3 },
                new CardCore.EffectExecutionContext
                {
                    Source = drainSource, Controller = p1,
                    Targets = new List<Entity> { drainVictim },
                    ZoneManager = core.ZoneManager
                });
            Assert(drainEvt != null && drainEvt.Cause == CardCore.Attribute.DeathCause.LifeLoss
                   && drainEvt.Source == drainSource && !drainVictim.IsAlive
                   && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(drainVictim),
                   "生命流失：即时决策表死亡（Cause=LifeLoss，死亡来源=效果来源，不走伤害管线）");

            // ② 伤害致死：SBA 收尸归因不丢（DamageLethal + 伤害来源）
            var killer = Make(p1, 5, 5);
            var slain = Make(p2, 2, 3);
            CardDestroyEvent combatEvt = null;
            void OnCombatKill(CardDestroyEvent e) { if (e.DestroyedCard == slain) combatEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnCombatKill);
            CardCore.Attribute.KeywordRules.ApplyDamage(killer, slain, 3, false);
            core.SBAEngine.CheckAndExecute();
            Assert(combatEvt != null && combatEvt.Cause == CardCore.Attribute.DeathCause.DamageLethal
                   && combatEvt.Source == killer && !slain.IsAlive
                   && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(slain),
                   "伤害致死：归因随尸体留档，SBA 送墓上事件（Cause=DamageLethal，死亡来源=伤害来源）");

            // ③ 剧毒死亡：死亡来源=施加方（指示物来源，消计数前捕获）
            var poisoner = Make(p1, 3, 3);
            var poisonVictim = Make(p2, 2, 5);
            poisonVictim.AddCounters(CardCore.Attribute.CounterRules.PoisonCounter, 1, poisoner);
            CardDestroyEvent poisonEvt = null;
            void OnPoisonKill(CardDestroyEvent e) { if (e.DestroyedCard == poisonVictim) poisonEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnPoisonKill);
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(poisonEvt != null && poisonEvt.Cause == CardCore.Attribute.DeathCause.Poison
                   && poisonEvt.Source == poisoner,
                   "剧毒死亡：死亡来源=施加方（指示物来源登记）");

            // ④ 减益致死：削减归零标死，指示物来源已登记（GetCounterSource 公共查询）
            var debuffer = Make(p1, 4, 4);
            var withered = Make(p2, 2, 3);
            CardCore.Attribute.CounterRules.AddStatCounter(withered, CardCore.Attribute.CounterRules.LifeDownCounter, 3, debuffer);
            Assert(!withered.IsAlive
                   && withered.GetCounterSource(CardCore.Attribute.CounterRules.LifeDownCounter) == debuffer,
                   "减益致死：削减归零标死，来源登记（SBA 收尸时归因到施加方）");

            // ⑤ 生命抵扣边界：恰好归零可付（归零=正常死亡），超出当前生命不可付
            var costCtx15 = new CostContext { Payer = p2, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool };
            Assert(CostHandlerRegistry.CanPay(new CostInstance { Type = CostType.LifePayment, Value = p2.Life }, costCtx15)
                   && !CostHandlerRegistry.CanPay(new CostInstance { Type = CostType.LifePayment, Value = p2.Life + 1 }, costCtx15),
                   "生命抵扣：可付到恰好归零（归零=正常死亡），超出不可付");

            // ---- 16. 再生 / 成长（回合开始维护） ----
            for (int i = 0; i < 3; i++) core.ZoneManager.GetZoneContainer(p1).Add(new Card { ID = "VERIFY_KW_DECK" }, Zone.Deck);
            var regrow = Make(p1, 2, 4, "Regeneration", "Growth");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, regrow, 2, false);
            int lifeExpected = System.Math.Min(4, 2 + 2); // 再生 +2（不超上限）
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 300 });
            Assert(regrow.GetLife() == lifeExpected + 1 && regrow.GetPower() == 3,
                   "再生 +2 与成长 +1/+1（回合开始维护）");

            // ---- 17. 辟邪 / 法术护盾（效果指定） ----
            var hermit = Make(p2, 2, 5, "Untargetable");
            var foe1 = Make(p1, 2, 2);
            Assert(!CardCore.EffectTargetValidator.CanTarget(hermit, foe1, AtomicEffectType.DealDamage)
                   && CardCore.EffectTargetValidator.CanTarget(hermit, hermit, AtomicEffectType.AddArmor),
                   "辟邪：对手效果不可指定，友方可以");

            var warded = Make(p2, 2, 5, "SpellShield");
            var targets = new List<Entity> { warded, p2 };
            CardCore.Attribute.KeywordRules.ConsumeSpellShields(targets, foe1);
            Assert(targets.Count == 1 && targets[0] == p2 && !warded.HasKeyword("SpellShield"),
                   "法术护盾：首次成为对手效果目标时移出目标并消耗");

            // ---- 18. 单向属性指示物：加时回写、换区清除时反向回写 ----
            var buffed = Make(p2, 2, 5);
            CardCore.Attribute.CounterRules.AddStatCounter(buffed, CardCore.Attribute.CounterRules.PowerUpCounter, 3);
            CardCore.Attribute.CounterRules.AddStatCounter(buffed, CardCore.Attribute.CounterRules.LifeUpCounter, 2);
            Assert(buffed.GetPower() == 5 && buffed.GetMaxLife() == 7 && buffed.GetLife() == 7,
                   "单向属性指示物：攻击力+3层、生命值+2层（上限与当前同加）");
            core.ZoneManager.MoveCard(buffed, p2, Zone.Battlefield, Zone.Hand); // 弹回手牌 → 指示物清除
            Assert(buffed.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && buffed.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 0
                   && buffed.GetPower() == 2 && buffed.GetMaxLife() == 5,
                   "换区清除：攻击力/生命值指示物离场消失（反向回写）");

            // ---- 18b. 削减类归零：生命值减少层到 0 标死（送墓交 SBA） ----
            var frail = Make(p2, 2, 3);
            CardCore.Attribute.CounterRules.AddStatCounter(frail, CardCore.Attribute.CounterRules.LifeDownCounter, 3);
            Assert(!frail.IsAlive, "生命值减少层：上限归零标死（SBA 收尸）");

            // ---- 18c. ±1/+1 与虚弱/鼓舞（同一路径：施加 ±1 层） ----
            var twin = Make(p2, 2, 4);
            CardCore.Attribute.CounterRules.AddStatCounter(twin, CardCore.Attribute.CounterRules.MinusOneCounter, 2);
            CardCore.Attribute.CounterRules.AddStatCounter(twin, CardCore.Attribute.CounterRules.PlusOneCounter, 1);
            Assert(twin.GetPower() == 1 && twin.GetMaxLife() == 3,
                   "±1 层对消共存：-1/-1×2 与 +1/+1×1（净 -1/-1）");

            // ---- 18d. 易损：受到伤害每层 +1（防护层吸收放大后的量）；回合末到期 ----
            var brittle = Make(p2, 2, 9);
            brittle.AddCounters(CardCore.Attribute.CounterRules.VulnerableCounter, 2);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, brittle, 3, false);
            Assert(brittle.GetLife() == 4, "易损：3 伤 + 2 层 = 5 伤（防护层前放大）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(brittle.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 0,
                   "易损：持续 1 回合（每个回合末到期）");

            // ---- 18e. 紊乱原子：附加后不能以玩家为目标（攻击与效果同口径） ----
            var dizzy = Make(p2, 2, 5);
            dizzy.AddCounters(CardCore.Attribute.KeywordRules.RushSicknessCounter, 1);
            combat.StartCombat(p1, p2);
            Assert(!combat.CanAttackTarget(dizzy, p1), "紊乱：持有者不能以玩家为目标（攻击侧）");
            combat.EndCombat();
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(dizzy.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 0,
                   "紊乱：持续到回合结束消退");

            // ---- 18f. 三轨路由·生物轨（非永久档）：Source=生物卡 → 换区清层（来源归因规则①锁定） ----
            var trackA = Make(p2, 2, 5);
            var trackDefF = new EffectDefinition
            {
                Id = "VERIFY_TRACK_F",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyPower, Value = 2 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefF,
                Source = trackA, // 生物来源（规则①：生物发动的效果来源=该生物）
                Controller = p2,
                Targets = new List<Entity> { trackA },
            }, skipElementCost: true).Forget();
            Assert(trackA.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 2 && trackA.GetPower() == 4,
                   "三轨·生物轨：生物来源攻+2 走换区清层（PowerUp×2 加时回写，非永久默认档）");

            // ---- 18g. 三轨路由·法术轨（设置类）：Source=角色 → 永久直改、跨区保留、净化不清 ----
            var trackB = Make(p2, 2, 5);
            var trackDefG = new EffectDefinition
            {
                Id = "VERIFY_TRACK_G",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyPower, Value = 3 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefG,
                Source = p1, // 魔法卡来源（规则②：所有魔法卡的效果来源=角色）
                Controller = p1,
                Targets = new List<Entity> { trackB },
            }, skipElementCost: true).Forget();
            Assert(trackB.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && trackB.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 0
                   && trackB.GetPower() == 5,
                   "三轨·法术轨：角色来源攻+3 永久直改（无任何指示物层）");
            core.ZoneManager.MoveCard(trackB, p2, Zone.Battlefield, Zone.Hand);
            Assert(trackB.GetPower() == 5,
                   "三轨·法术轨：设置类跨区保留（弹回手不清）");
            CardCore.Attribute.CounterRules.PurgeAll(trackB); // 净化口径全清指示物
            Assert(trackB.GetPower() == 5,
                   "三轨·法术轨：净化清不掉设置类属性（直改无层可回滚，视同本体）");
            var trackB2 = Make(p2, 2, 5);
            var trackDefG2 = new EffectDefinition
            {
                Id = "VERIFY_TRACK_G2",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyLife, Value = -5 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefG2,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { trackB2 },
            }, skipElementCost: true).Forget();
            Assert(!trackB2.IsAlive && trackB2.GetCounterCount(CardCore.Attribute.CounterRules.LifeDownCounter) == 0,
                   "三轨·法术轨：设置类生命直减归零标死（无层，交 SBA）");

            // ---- 18h. 三轨路由·生物轨永久档：Duration=Permanent → Permanent 层（换区不清、净化清） ----
            var trackC = Make(p2, 2, 5);
            var trackDefH = new EffectDefinition
            {
                Id = "VERIFY_TRACK_H",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyPower, Value = 2, Duration = DurationType.Permanent } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefH,
                Source = trackC, // 生物来源 + 永久赋予
                Controller = p2,
                Targets = new List<Entity> { trackC },
            }, skipElementCost: true).Forget();
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 2
                   && trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && trackC.GetPower() == 4,
                   "三轨·生物轨永久档：Duration=Permanent 走 PowerUpPermanent 层（单属性，不带动生命）");
            core.ZoneManager.MoveCard(trackC, p2, Zone.Battlefield, Zone.Hand);
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 2
                   && trackC.GetPower() == 4 && trackC.GetMaxLife() == 5,
                   "三轨·生物轨永久档：换区不清（±1/±1 双属性层不同——单属性永久层随卡走）");
            CardCore.Attribute.CounterRules.PurgeAll(trackC);
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 0
                   && trackC.GetPower() == 2,
                   "三轨·生物轨永久档：净化清（反向回写）——区别于设置类");

            // ---- 19. 净化（2026-09-09 语义重定义：变回生物原有状态）----
            // 关键词四轨矩阵：Printed（卡面本体）保留 / Setting（魔法卡赋予视同本体）保留 /
            // Temp（临时）清 / GrantedPermanent（生物赋的永久）清；指示物全清（含永久层）。
            var cursed = Make(p2, 3, 6, "Taunt"); // Taunt=Printed（CardWrapper 构造注入）
            cursed.AddKeyword("Stealth", CardCore.KeywordLane.Temp);                    // 临时轨
            cursed.AddKeyword("Indestructible", CardCore.KeywordLane.GrantedPermanent); // 生物赋的永久
            cursed.AddKeyword("Vigilance", CardCore.KeywordLane.Setting);               // 设置类（视同本体）
            cursed.AddCounters(CardCore.Attribute.CounterRules.ToxinCounter, 2,
                turns: CardCore.Attribute.CounterRules.Find(CardCore.Attribute.CounterRules.ToxinCounter).Turns);
            cursed.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
            var purifyDef = new EffectDefinition
            {
                Id = "VERIFY_PURIFY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Purify } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = purifyDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { cursed },
            }, skipElementCost: true).Forget();
            Assert(cursed.HasKeyword("Taunt") && cursed.HasKeyword("Vigilance")
                   && !cursed.HasKeyword("Stealth") && !cursed.HasKeyword("Indestructible"),
                   "净化四轨矩阵：本体与设置类保留，临时/生物永久赋予清除");
            Assert(cursed.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 0
                   && cursed.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   "净化：全部指示物清空（含永久类，属性层反向回写）");

            // ---- 19b. 关键词换区清除（三轨制定案）：Temp 换区清、Printed 换区留 ----
            var zoneKw = Make(p2, 2, 4, "Taunt");
            zoneKw.AddKeyword("Stealth", CardCore.KeywordLane.Temp);
            zoneKw.AddKeyword("FirstStrike", CardCore.KeywordLane.GrantedPermanent);
            core.ZoneManager.MoveCard(zoneKw, p2, Zone.Battlefield, Zone.Hand);
            Assert(!zoneKw.HasKeyword("Stealth")
                   && zoneKw.HasKeyword("Taunt") && zoneKw.HasKeyword("FirstStrike"),
                   "换区清关键词：Temp 轨清除；Printed 与 GrantedPermanent（换区不清档）保留");

            // ---- 19c. 生物轨永久档属性层：Permanent 层换区不清、净化清 ----
            var permBuff = Make(p2, 2, 4);
            CardCore.Attribute.CounterRules.AddStatCounter(permBuff,
                CardCore.Attribute.CounterRules.PowerUpPermanentCounter, 2, p1);
            CardCore.Attribute.CounterRules.AddStatCounter(permBuff,
                CardCore.Attribute.CounterRules.LifeUpPermanentCounter, 1, p1);
            Assert(permBuff.GetPower() == 4 && permBuff.GetMaxLife() == 5,
                   "永久档属性层：加时回写（与换区清层同粒度）");
            core.ZoneManager.MoveCard(permBuff, p2, Zone.Battlefield, Zone.Hand);
            Assert(permBuff.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 2
                   && permBuff.GetPower() == 4,
                   "永久档属性层：换区不清（生物赋的永久属性随卡走）");
            CardCore.Attribute.KeywordRules.PurifyKeywords(permBuff);
            CardCore.Attribute.CounterRules.PurgeAll(permBuff);
            Assert(permBuff.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 0
                   && permBuff.GetPower() == 2,
                   "永久档属性层：净化清（反向回写）");

            // ---- 19d. 入场刷新（2026-09-09 定案）：真实入场补回被消耗的卡面关键词（Printed 差集）；
            //      复生（原地留场）与控制权变更（容器直移）不经统一出口，天然不触发 ----
            var refreshShield = Make(p2, 2, 5, "DivineShield");
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, refreshShield, 1, false);
            Assert(!refreshShield.HasKeyword("DivineShield") && refreshShield.GetLife() == 5,
                   "入场刷新前置：圣盾挡一次伤害后消耗（关键词移除、伤害被完全挡下）");
            core.ZoneManager.MoveCard(refreshShield, p2, Zone.Battlefield, Zone.Hand); // 弹回手
            core.ZoneManager.TryAddToBattlefield(refreshShield, p2);                   // 真实入场（统一出口）
            Assert(refreshShield.HasKeyword("DivineShield"),
                   "入场刷新：弹回重打补回被消耗的卡面圣盾（Printed 轨差集补齐）");

            var refreshReborn = Make(p2, 2, 5, "Reborn");
            CardCore.Attribute.DeathRules.TryKill(refreshReborn, CardCore.Attribute.DeathCause.DamageLethal, p1, core.ZoneManager);
            Assert(refreshReborn.IsAlive && refreshReborn.GetLife() == 1 && !refreshReborn.HasKeyword("Reborn"),
                   "复生不触发刷新：死亡替代原地留场（不经入场口），消耗后不自我补回（无无限复生）");

            var refreshStolen = Make(p1, 2, 5, "DivineShield");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, refreshStolen, 1, false); // 消耗圣盾
            new CardCore.Attribute.Handlers.GainControlHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.GainControl },
                new CardCore.EffectExecutionContext
                {
                    Source = p2, Controller = p2,
                    Targets = new List<Entity> { refreshStolen },
                    ZoneManager = core.ZoneManager
                });
            Assert(!refreshStolen.HasKeyword("DivineShield"),
                   "控制权变更不触发刷新：场内迁移（容器直移+补发事件）不补回消耗项（偷取不白得圣盾）");

            // ---- 20. 沉默指示物：持有者不可发动主动效果（激活式能力路径） ----
            var silenced = Make(p1, 2, 5);
            var activatable = new EffectDefinition
            {
                Id = "VERIFY_SILENCE_ABILITY",
                TriggerTiming = TriggerTiming.Activate_Active,
                ActivationType = EffectActivationType.Voluntary,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Tap } },
            };
            var executor = core.StackEngine.GetExecutor();
            bool beforeSilence = executor.CanActivate(activatable, silenced, p1, p1, PhaseType.Main, 1);
            silenced.AddCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
            bool afterSilence = executor.CanActivate(activatable, silenced, p1, p1, PhaseType.Main, 1);
            Assert(beforeSilence && !afterSilence,
                   "沉默：持有者激活式能力被封锁（出牌不受限——PlayCard 不经此门）");

            // ---- 20b. 无效指示物：拦全部触发式（事件匹配后、上栈前——含 OnTakeDamage 族） ----
            var nullified = Make(p2, 2, 9);
            var nullTrigDef = new EffectDefinition
            {
                Id = "VERIFY_NULLIFY_TRIG",
                TriggerTiming = TriggerTiming.OnTakeDamage,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
            };
            core.TriggerEngine.RegisterEffect(nullTrigDef, nullified, p2);
            nullified.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1, p1);
            int p2HandAtNullify = core.ZoneManager.GetCards(p2, Zone.Hand).Count;
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullified, 1, false);
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandAtNullify,
                   "无效：非启动式（触发式）被拦——受击触发不上栈");
            // 对照：无效消退后同一触发照常上栈结算
            nullified.RemoveCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullified, 1, false);
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandAtNullify + 1,
                   "无效消退后：同一触发照常上栈（对照锚，证明 20b 拦截来自无效指示物本身）");

            // ---- 20c. 无效不拦启动式（与沉默对照：沉默拦启动式、无效拦非启动式，互不重叠） ----
            var nullActivated = Make(p1, 2, 5);
            var nullActDef = new EffectDefinition
            {
                Id = "VERIFY_NULLIFY_ACT",
                TriggerTiming = TriggerTiming.Activate_Active,
                ActivationType = EffectActivationType.Voluntary,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Tap } },
            };
            nullActivated.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            bool canActUnderNullify = executor.CanActivate(nullActDef, nullActivated, p1, p1, PhaseType.Main, 1);
            Assert(canActUnderNullify,
                   "无效：启动式照常可发动（拦截面与沉默互补不重叠）");

            // ---- 20d. 无效不拦伤害管线被动（坚韧/圣盾等非「能力发动」；回合维护再生/成长同口径） ----
            var nullTough = Make(p2, 3, 8, "Armor"); // 坚韧：每次受伤 −1
            nullTough.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullTough, 3, false);
            Assert(nullTough.GetLife() == 6,
                   "无效：伤害管线被动照常（坚韧减伤不受无效影响）");

            // ---- 21. 摧毁：无生命值单位（地牌）出池直送墓地，不走死亡决策表 ----
            Card landTarget = null;
            var p2Pooled = core.ElementPool.GetPooledCards(p2);
            if (p2Pooled.Count > 0)
            {
                landTarget = p2Pooled[0].SourceCard;
            }
            else
            {
                var p2Creature = core.ZoneManager.GetCards(p2, Zone.Hand)
                    .FirstOrDefault(c => CardCore.ElementPoolSystem.CanServeAsLand(c));
                if (p2Creature != null && GameActions.AddToElementPool(core, p2, p2Creature))
                    landTarget = p2Creature;
            }
            if (landTarget != null)
            {
                int graveBefore = core.ZoneManager.GetCards(p2, Zone.Graveyard).Count;
                var smashDef = new EffectDefinition
                {
                    Id = "VERIFY_SMASH",
                    TriggerTiming = TriggerTiming.Activate_Active,
                    Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Smash } },
                };
                executor.ExecuteAsync(new EffectInstance
                {
                    Definition = smashDef,
                    Source = p1,
                    Controller = p1,
                    Targets = new List<Entity> { landTarget },
                }, skipElementCost: true).Forget();
                Assert(core.ElementPool.GetPooledCards(p2).All(pc => pc.SourceCard != landTarget)
                       && core.ZoneManager.GetCards(p2, Zone.Graveyard).Count == graveBefore + 1
                       && !landTarget.WasDepletedAsLand,
                       "摧毁：地牌出池直送墓地（非耗尽——回收后可再作地牌）");
            }
            else
            {
                Debug.LogWarning("[Verify] 跳过摧毁段：p2 无可用地牌（池空且手牌无可入池生物）");
            }

            // ---- 22. 费用指示物端到端：减费作用于预检与付费全程；层在发动区存活；结算后清除 ----
            EnsureMainPhase(core, p1);
            CardData GraySpellData(int gray, string id, string name)
            {
                var d = new CardData { ID = id, CardName = name };
                d.Supertype = Cardtype.Spell;
                d.Cost[(int)ManaType.Gray] = gray;
                return d;
            }
            var cheap = InjectCard(core, p1, GraySpellData(3, "VERIFY_COST_GRAY3", "灰费验证"));
            var pool1 = core.ElementPool.GetPool(p1);

            // 基线：无层原价 灰3，bank 灰1 → 声明预检拦截
            pool1.AvailableMana[ManaType.Gray] = 1;
            Assert(!GameActions.PlayCard(core, p1, cheap), "费用基线：灰3 卡在 bank 灰1 下声明被拒（原价）");

            // 减 2 层 → 有效费 灰1：声明过（预检按层后费）+ cast 付费按层后费扣（层在发动区存活）
            CardCore.Attribute.CounterRules.AddStatCounter(cheap, CardCore.Attribute.CounterRules.CostDownCounter, 2);
            Assert(PlayCardSync(core, p1, cheap), "减费：灰3 − 2层 = 灰1，声明通过");
            Assert(pool1.AvailableMana[ManaType.Gray] == 0,
                   "减费：cast 付费按层后费扣 1（进发动区不清层——发动区豁免）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(cheap), "减费卡（空效果法术）结算后入墓");
            Assert(cheap.GetCounterCount(CardCore.Attribute.CounterRules.CostDownCounter) == 0,
                   "费用层：入墓（真实去向）清除");

            // 增 2 层：灰1 + 2层 = 灰3，bank 灰2 拒、灰3 过
            var pricey = InjectCard(core, p1, GraySpellData(1, "VERIFY_COST_GRAY1", "增费验证"));
            CardCore.Attribute.CounterRules.AddStatCounter(pricey, CardCore.Attribute.CounterRules.CostUpCounter, 2);
            pool1.AvailableMana[ManaType.Gray] = 2;
            Assert(!GameActions.PlayCard(core, p1, pricey), "增费：灰1 + 2层 = 灰3，bank 灰2 拒");
            pool1.AvailableMana[ManaType.Gray] = 3;
            Assert(PlayCardSync(core, p1, pricey), "增费：bank 灰3 过（层后费全额支付）");

            CleanKeywords();
        }

        // ======================================== 仪式系统（竞速任务卡） ========================================

        /// <summary>开局入手断言：占卡组位（牌库留 2 副本）、不占起手数（额外入手）、双方各 1 张。</summary>
        private static void TestRitualOpeningHand(GameCore core, List<CardData> cardsData)
        {
            // 测试主卡组：非仪式卡 + 单张三相仪典（构筑规则：卡组不重复、测试只带 1 张仪式）
            if (!cardsData.Any(c => c.ID == "RITUAL_TRINITY_001")) return;

            var p1 = core.Player1;
            var p2 = core.Player2;

            foreach (var p in new[] { p1, p2 })
            {
                var hand = core.ZoneManager.GetCards(p, Zone.Hand);
                var ritualsInHand = hand.Count(c => RitualSystem.IsRitual(c)).ToString();
                Assert(hand.Count(c => RitualSystem.IsRitual(c)) == 1,
                       $"仪式占初始手牌位：{p.Name} 手牌恰含 1 张仪式（实际 {ritualsInHand}）");
                Assert(core.ZoneManager.GetCards(p, Zone.Deck).Count(c => RitualSystem.IsRitual(c)) == 0,
                       $"{p.Name} 牌库无仪式残留（单张已占位入手）");
                // 总量断言按玩家区分：p1 已含首回合抽牌（6+1=7），p2 尚未开始回合（6）
            }

            // InitGame 已开 P1 回合（含首回合抽 1）：p1 = 6 + 1 = 7；p2 尚未开始回合 = 6
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == 7, "p1 含首回合抽牌共 7");
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == 6, "p2 初始 6");
        }

        /// <summary>
        /// 仪式全流程验证（末段执行，注入卡驱动，不重开对局）：
        /// 0费打出/占格 → 全局唯一任务（同玩家第二张顶掉第一张）→ 竞速颜色断言（失败清零、3回合达标）
        /// → 完成态（不灭+辟邪、光环独享）→ 血偿流（生命累计/支付转嫁/完成态不可破坏）
        /// → 进行中破坏回手（手牌满则入墓）→ 完成态光环与新任务并存。
        /// </summary>
        private static void TestRituals(GameCore core, List<CardData> cardsData)
        {
            var trinityData = cardsData.FirstOrDefault(c => c.ID == "RITUAL_TRINITY_001");
            var bloodData = cardsData.FirstOrDefault(c => c.ID == "RITUAL_BLOOD_002");
            if (trinityData == null || bloodData == null)
            {
                Debug.LogWarning("[Verify] 跳过仪式段：卡表缺仪式卡");
                return;
            }

            var p1 = core.Player1;
            var p2 = core.Player2;
            var pool1 = core.ElementPool.GetPool(p1);
            var pool2 = core.ElementPool.GetPool(p2);

            // 脚手架：起手改 6（含仪式）后手牌常满 7，回手类断言需要余位——双方裁到 5
            foreach (var p in new[] { p1, p2 })
            {
                var handSnapshot = core.ZoneManager.GetCards(p, Zone.Hand).ToList();
                foreach (var extra in handSnapshot.Skip(5))
                    core.ZoneManager.MoveCard(extra, p, Zone.Hand, Zone.Graveyard);
            }

            // ---- 1. 0 费打出 + 占格 ----
            var trinityA = InjectCard(core, p1, trinityData);
            Assert(PlayCardSync(core, p1, trinityA), "仪式 0 费打出成功（无需任何元素）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(trinityA), "仪式占战场格");
            Assert(RitualSystem.Active != null && RitualSystem.Active.Card == trinityA
                   && RitualSystem.Active.Definition.id == "RITUAL_TRINITY_001",
                   "打出即激活为全局唯一任务");

            // ---- 2. 全局唯一：同玩家第二张顶掉第一张 ----
            var trinityB = InjectCard(core, p1, trinityData);
            Assert(PlayCardSync(core, p1, trinityB), "第二张仪式打出成功");
            Assert(RitualSystem.Active.Card == trinityB, "后发仪式成为唯一任务");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Contains(trinityA)
                   && !core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(trinityA),
                   "先发仪式被顶掉：进度作废并回手牌");

            // ---- 3. 竞速颜色断言：红(过)→红(败清零)→蓝→绿→红 = 连续3回合达标 ----
            foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99;

            bool firstRedChecked = false;
            foreach (var color in new[] { ManaType.Red, ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Red })
            {
                var colored = InjectColoredCreature(core, p1, color);
                Assert(PlayCardSync(core, p1, colored), $"竞速回合用 {color} 卡");
                EndTurnPumped(core, p1);    // TurnEnd 断言结算
                GameActions.SkipElementPool(core, p2);
                EndTurnPumped(core, p2);    // p2 空过（p2 自己断言失败，不完成）
                GameActions.SkipElementPool(core, p1);

                // 仅首个红回合检查 streak==1（第二个红回合按规则清零，不在本断言范围）
                if (color == ManaType.Red && !firstRedChecked && RitualSystem.Active != null)
                {
                    firstRedChecked = true;
                    int s1 = RitualComponents.ColorStreak.GetStreak(p1);
                    Assert(s1 == 1, $"首个红回合断言通过 streak=1（实际 {s1}）");
                }
            }

            Assert(RitualSystem.Active == null && RitualSystem.CompletedAuras.Count == 1
                   && RitualSystem.CompletedAuras[0].Completer == p1
                   && RitualSystem.CompletedAuras[0].Card == trinityB,
                   "连续 3 回合颜色断言达标 → p1 完成仪式，任务槽清空（对手进度作废）");
            Assert(trinityB.HasKeyword(CardCore.Attribute.KeywordRules.Indestructible)
                   && trinityB.HasKeyword(CardCore.Attribute.KeywordRules.Untargetable),
                   "完成态：不可摧毁 + 不受其他卡效果影响");

            // ---- 4. 三相光环：完成者付 3 纯色 → RGB 各 +1（守恒兑换，独享） ----
            foreach (var t in AllManaTypes()) pool2.AvailableMana[t] = 99;
            int r1 = pool1.AvailableMana[ManaType.Red], b1 = pool1.AvailableMana[ManaType.Blue], g1 = pool1.AvailableMana[ManaType.Green];
            core.ElementPool.PayCost(new Dictionary<int, float> { [(int)ManaType.Red] = 3f }, p1);
            Assert(pool1.AvailableMana[ManaType.Red] == r1 - 3 + 1
                   && pool1.AvailableMana[ManaType.Blue] == b1 + 1
                   && pool1.AvailableMana[ManaType.Green] == g1 + 1,
                   "三相光环：完成者付 3 红 → 红/蓝/绿各 +1");

            int r2b = pool2.AvailableMana[ManaType.Red], b2b = pool2.AvailableMana[ManaType.Blue];
            core.ElementPool.PayCost(new Dictionary<int, float> { [(int)ManaType.Red] = 3f }, p2);
            Assert(pool2.AvailableMana[ManaType.Red] == r2b - 3 && pool2.AvailableMana[ManaType.Blue] == b2b,
                   "三相光环：对手消耗不转化（完成者独享）");

            // 余数保留：再付 1 红（累计 4 → 第二次转化不触发，计数 1）
            int r1c = pool1.AvailableMana[ManaType.Red];
            core.ElementPool.PayCost(new Dictionary<int, float> { [(int)ManaType.Red] = 1f }, p1);
            Assert(pool1.AvailableMana[ManaType.Red] == r1c - 1, "三相光环：余数保留（累计 4 只转化一次，余 1 不触发）");

            // ---- 5. 血偿流：p2 打出（与完成态光环并存）→ 累计 30 命 → 完成 → 支付转嫁 ----
            EndTurnPumped(core, p1);          // 颜色竞速后轮到 p2
            GameActions.SkipElementPool(core, p2);
            var blood = InjectCard(core, p2, bloodData);
            Assert(PlayCardSync(core, p2, blood), "血偿仪典打出（0 费）");
            Assert(RitualSystem.Active != null && RitualSystem.Active.Card == blood
                   && RitualSystem.CompletedAuras.Count == 1,
                   "完成态光环与进行中任务并存（光环占格存续，新仪式开新任务）");

            p2.Life = 100; // 测试脚手架：保证 3×10 可付且不触发抵扣归零终局
            var costCtx = new CostContext { Payer = p2, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool };
            for (int i = 0; i < 3; i++)
            {
                Assert(CostHandlerRegistry.Pay(new CostInstance { Type = CostType.LifePayment, Value = 10 }, costCtx),
                       $"血偿任务：第 {i + 1} 次支付 10 生命（累计 {(i + 1) * 10}）");
            }
            Assert(RitualSystem.Active == null && RitualSystem.CompletedAuras.Count == 2
                   && RitualSystem.CompletedAuras[1].Completer == p2,
                   "累计支付 30 生命 → p2 完成血偿仪典");
            Assert(blood.HasKeyword(CardCore.Attribute.KeywordRules.Indestructible), "血偿完成态：不可摧毁");

            // ---- 6. 血偿光环：p2 支付生命 → p1 扣血、p2 不动 ----
            p1.Life = 50;
            int p2LifeBefore = p2.Life;
            Assert(CostHandlerRegistry.Pay(new CostInstance { Type = CostType.LifePayment, Value = 5 }, costCtx),
                   "血偿光环下支付生命代价成功");
            Assert(p1.Life == 45 && p2.Life == p2LifeBefore,
                   "血偿光环：改用对手生命支付（p1 −5，p2 不动）");

            // ---- 7. 完成态不可破坏 ----
            var onField = core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(blood);
            DestroyViaEffect(core, p2, blood);
            Assert(onField && core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(blood) && blood.IsAlive,
                   "完成态仪式：Destroy 无效（不灭）");

            // ---- 8. 进行中破坏 → 回手牌；手牌满 → 入墓 ----
            // 脚手架：竞速 5 轮抽牌后 p2 手牌常已满 7，回手断言需要余位——裁到 5
            var p2HandBeforeDestroy = core.ZoneManager.GetCards(p2, Zone.Hand).ToList();
            foreach (var extra in p2HandBeforeDestroy.Skip(5))
                core.ZoneManager.MoveCard(extra, p2, Zone.Hand, Zone.Graveyard);
            var blood2 = InjectCard(core, p2, bloodData);
            Assert(PlayCardSync(core, p2, blood2), "第二张血偿打出（新任务）");
            DestroyViaEffect(core, p2, blood2);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Contains(blood2)
                   && !core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(blood2)
                   && RitualSystem.Active == null,
                   "进行中仪式被破坏 → 回手牌（非墓地），任务进度清空");

            Assert(PlayCardSync(core, p2, blood2), "回手的仪式可再打出（进度重开）");
            var container2 = core.ZoneManager.GetZoneContainer(p2);
            // 脚手架：先裁再补到恰好 7（异步手牌上限弃牌可能已把手牌压到任意值）
            var p2HandSnapshot = core.ZoneManager.GetCards(p2, Zone.Hand).ToList();
            foreach (var extra in p2HandSnapshot.Skip(7))
                container2.Move(extra, Zone.Hand, Zone.Graveyard);
            while (core.ZoneManager.GetCards(p2, Zone.Hand).Count < 7)
                container2.Add(new Card { ID = "VERIFY_RITUAL_FILLER" }, Zone.Hand);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == 7, "测试脚手架：p2 手牌灌满 7");
            DestroyViaEffect(core, p2, blood2);
            Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(blood2)
                   && !core.ZoneManager.GetCards(p2, Zone.Hand).Contains(blood2),
                   "手牌已满时仪式被破坏 → 直接入墓地");
        }

        /// <summary>
        /// 六轴扩展仪式验证（注入驱动，TestRituals 之后执行，不重开对局）：
        /// 丰盈（治疗溢出 20 → 角色伤害封顶 5）/ 窥渊（展示对手手牌 10 → 回合开始锁定）/
        /// 归土（自己送墓 30 → 墓地视手牌使用，每回合一次）/ 疾风（跳过准备阶段 ×2 → 额外回合 → 自毁）/
        /// 纳川（非抽牌入手 15 → 手牌上限 15 + 免疲劳）。
        /// </summary>
        private static void TestNewRituals(GameCore core, List<CardData> cardsData)
        {
            CardData RitualData(string id) => cardsData.FirstOrDefault(c => c.ID == id);
            var survival = RitualData("RITUAL_SURVIVAL_003");
            var info = RitualData("RITUAL_INFO_004");
            var resource = RitualData("RITUAL_RESOURCE_005");
            var tempo = RitualData("RITUAL_TEMPO_006");
            var handRitual = RitualData("RITUAL_HAND_007");
            var creature = cardsData.FirstOrDefault(c => c.ID == "TEST_GREEN_003");
            if (survival == null || info == null || resource == null || tempo == null || handRitual == null || creature == null)
            {
                Debug.LogWarning("[Verify] 跳过六轴仪式段：卡表缺新仪式卡或生物载体");
                return;
            }

            var p1 = core.Player1;
            var p2 = core.Player2;
            var pool1 = core.ElementPool.GetPool(p1);
            foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99; // 脚手架：支付不设限

            // ---- 1. 丰盈仪典：治疗溢出 20 → 完成 → 角色单次伤害封顶 5 ----
            EnsureMainPhase(core, p1);
            var survivalCard = InjectCard(core, p1, survival);
            Assert(PlayCardSync(core, p1, survivalCard), "丰盈仪典 0 费打出（顶掉进行中任务）");

            p1.Life = 29;
            EventManager.Instance.Publish(new CardCore.Attribute.HealEvent { Target = p1, Amount = 12, Overfill = 11, Source = null });
            EventManager.Instance.Publish(new CardCore.Attribute.HealEvent { Target = p1, Amount = 12, Overfill = 9, Source = null });
            Assert(RitualSystem.Active == null && RitualSystem.CompletedAuras.Any(a => a.Card == survivalCard),
                   "治疗溢出累计 20 → 丰盈完成");

            p1.Life = 30;
            CardCore.Attribute.KeywordRules.ApplyDamage(null, p1, 12, false);
            Assert(p1.Life == 25, "丰盈光环：完成者单次伤害封顶 5（12 截断为 5）");
            p2.Life = 30;
            CardCore.Attribute.KeywordRules.ApplyDamage(null, p2, 12, false);
            Assert(p2.Life == 18, "无光环对手照常全额受伤（12）");

            // ---- 2. 窥渊仪典：展示对手手牌累计 10 → 每回合开始锁定 1 张 ----
            var infoCard = InjectCard(core, p1, info);
            Assert(PlayCardSync(core, p1, infoCard), "窥渊仪典打出");

            while (core.ZoneManager.GetCards(p2, Zone.Hand).Count < 5)
                InjectCard(core, p2, creature); // 脚手架：保证两次全场展示 ≥ 10 张
            var p2Hand = core.ZoneManager.GetCards(p2, Zone.Hand).ToList();
            var revealSource = InjectCard(core, p1, creature); // 展示方（Source 控制者 = p1）
            EventManager.Instance.Publish(new CardCore.Attribute.RevealHandEvent { Player = p2, Cards = p2Hand, Source = revealSource });
            EventManager.Instance.Publish(new CardCore.Attribute.RevealHandEvent { Player = p2, Cards = p2Hand, Source = revealSource });
            Assert(RitualSystem.CompletedAuras.Any(a => a.Card == infoCard),
                   $"展示对手手牌累计 {p2Hand.Count * 2} ≥ 10 → 窥渊完成");

            var lockedCandidate = p2Hand[0];
            EndTurnPumped(core, p1);                    // → p2 回合开始：无 UI 自动锁定首张已展示卡
            Assert(LockRevealedAura.IsLockedThisTurn(lockedCandidate), "窥渊光环：回合开始锁定对手已展示卡");
            GameActions.SkipElementPool(core, p2);
            Assert(!PlayCardSync(core, p2, lockedCandidate), "被锁定的卡本回合不可使用");
            EndTurnPumped(core, p2);                    // 回合结束 → 锁定清空
            Assert(!LockRevealedAura.IsLockedThisTurn(lockedCandidate), "回合结束：锁定解除");

            // ---- 3. 归土仪典：自己送墓 30 → 墓地视手牌使用（每回合一次）----
            EnsureMainPhase(core, p1);
            var resourceCard = InjectCard(core, p1, resource);
            Assert(PlayCardSync(core, p1, resourceCard), "归土仪典打出");

            var costCtx = new CostContext { Payer = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool };
            Assert(CostHandlerRegistry.Pay(new CostInstance { Type = CostType.MillDeck, Value = 5 }, costCtx),
                   "真实代价送墓 5 张（MillDeckCostEvent 计数）");
            for (int i = 0; i < 25; i++)
                EventManager.Instance.Publish(new CardCore.Attribute.CardMillEvent { Player = p1, Source = null });
            Assert(RitualSystem.CompletedAuras.Any(a => a.Card == resourceCard), "送墓累计 30 → 归土完成");

            var graveCreature = InjectCard(core, p1, creature);
            core.ZoneManager.MoveCard(graveCreature, p1, Zone.Hand, Zone.Graveyard);
            var graveCreature2 = InjectCard(core, p1, creature);
            core.ZoneManager.MoveCard(graveCreature2, p1, Zone.Hand, Zone.Graveyard);
            Assert(PlayCardSync(core, p1, graveCreature, null, Zone.Graveyard), "墓地视手牌使用：第一张成功");
            Assert(!PlayCardSync(core, p1, graveCreature2, null, Zone.Graveyard), "每回合限一次：第二张被拒");

            // ---- 4. 疾风仪典：跳过准备阶段 ×2 → 额外回合 → 自毁 ----
            EnsureMainPhase(core, p1);
            var tempoCard = InjectCard(core, p1, tempo);
            Assert(PlayCardSync(core, p1, tempoCard), "疾风仪典打出");

            int handBeforeSkip = core.ZoneManager.GetCards(p1, Zone.Hand).Count;

            RitualComponents.SkipStandby.Commit(p1);
            EndTurnPumped(core, p1);          // → p2 回合（p1 池随全局推进 +1）
            int idxAfterOpponentTurn = pool1.GlobalTurnIndex;
            GameActions.SkipElementPool(core, p2);
            EndTurnPumped(core, p2);          // → p1 回合开始：宣告消费，准备阶段整体跳过
            // 断言意图而非精确快照：回合结束的异步手牌弃牌可能减手牌，故只断「不增」（未抽牌）
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count <= handBeforeSkip, "跳过准备阶段：本回合未抽牌（手牌不增）");
            Assert(pool1.GlobalTurnIndex == idxAfterOpponentTurn, "跳过准备阶段：自己的回合不再推进地牌槽曲线");
            int skip1 = RitualComponents.SkipStandby.GetSkipCount(p1);
            Assert(skip1 == 1, "跳过计数 1");

            GameActions.SkipElementPool(core, p1);  // 跳过后的准备阶段照常推进入主阶段
            RitualComponents.SkipStandby.Commit(p1);
            EndTurnPumped(core, p1);
            GameActions.SkipElementPool(core, p2);
            EndTurnPumped(core, p2);          // 第二次跳过 → 达标完成
            Assert(RitualSystem.Active == null && RitualSystem.CompletedAuras.Any(a => a.Card == tempoCard),
                   "跳过 ×2 → 疾风完成（一次性奖励，不形成常驻光环语义）");

            GameActions.SkipElementPool(core, p1);  // 第二次跳过后的准备阶段 → 主阶段
            EndTurnPumped(core, p1);          // 完成回合结束 → 授予额外回合
            Assert(core.TurnEngine.TurnPlayer == p1, "完成的回合结束 → p1 获得额外回合（连续行动）");
            GameActions.SkipElementPool(core, p1);
            EndTurnPumped(core, p1);          // 额外回合结束 → 仪式自毁
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(tempoCard)
                   && !RitualSystem.CompletedAuras.Any(a => a.Card == tempoCard),
                   "额外回合结束 → 疾风自毁入墓（光环移除）");

            // ---- 5. 纳川仪典：非抽牌入手 15 → 手牌上限 15 + 免疲劳 ----
            EnsureMainPhase(core, p1);
            var handCard = InjectCard(core, p1, handRitual);
            Assert(PlayCardSync(core, p1, handCard), "纳川仪典打出");

            var shuttle = InjectCard(core, p1, creature);
            core.ZoneManager.MoveCard(shuttle, p1, Zone.Hand, Zone.Graveyard);
            for (int i = 0; i < 15; i++)
            {
                core.ZoneManager.MoveCard(shuttle, p1, Zone.Graveyard, Zone.Hand);   // 非抽牌入手 +1
                if (i < 14) core.ZoneManager.MoveCard(shuttle, p1, Zone.Hand, Zone.Graveyard);
            }
            Assert(RitualSystem.CompletedAuras.Any(a => a.Card == handCard), "非抽牌入手累计 15 → 纳川完成");
            Assert(RuleHooks.GetHandLimit(p1) == 15, "手牌上限 7 → 15");
            Assert(HandLimitAura.HasFatigueImmunity(p1), "空库抽牌免疲劳");

            foreach (var c in core.ZoneManager.GetCards(p1, Zone.Deck).ToList())
                core.ZoneManager.MoveCard(c, p1, Zone.Deck, Zone.Graveyard);         // 清空牌库
            int lifeBeforeFatigue = p1.Life;
            ZoneManagerExtensions.DrawCard(core.ZoneManager, p1);
            Assert(p1.Life == lifeBeforeFatigue && p1.FatigueCount == 0,
                   "空库抽牌：免疲劳（无伤害、不计数）");
        }

        /// <summary>推进回合直到 p1 处于主阶段（验证器节奏：EndTurn 折返后 SkipElementPool 入主阶段）。</summary>
        private static void EnsureMainPhase(GameCore core, Player p1)
        {
            for (int i = 0; i < 6; i++)
            {
                if (core.TurnEngine.TurnPlayer == p1 && core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main)
                    return;
                var tp = core.TurnEngine.TurnPlayer;
                if (core.TurnEngine.CurrentPhase?.Phase == PhaseType.Standby)
                    GameActions.SkipElementPool(core, tp);
                else
                    EndTurnPumped(core, tp);
            }
        }

        /// <summary>注入一张合成单色 1 费生物（竞速颜色断言的用卡载体）。</summary>
        private static Card InjectColoredCreature(GameCore core, Player owner, ManaType color)
        {
            var data = new CardData { ID = "VERIFY_RITUAL_" + color, CardName = "竞速色卡" + color };
            data.Supertype = Cardtype.Creature;
            data.Power = 1;
            data.Life = 1;
            data.Cost[(int)color] = 1;
            return InjectCard(core, owner, data);
        }

        /// <summary>
        /// 结束回合并驱动折返：编辑器验证上下文没有帧泵（GameLoopController.Update 无人调用），
        /// EndTurn 只推进到结束阶段；手动补一次 CheckPhaseTransition 完成 End→Standby 折返回合切换。
        /// </summary>
        private static bool EndTurnPumped(GameCore core, Player player)
        {
            if (!GameActions.EndTurn(core, player)) return false;
            core.TurnEngine.CheckPhaseTransition();   // End(Active) → 折返 StartNewTurn(下一位)
            return true;
        }

        /// <summary>
        /// PlayCard + 排干栈（验证器同步快进）：出牌即上栈（使用时点声明，不付费），
        /// 双 Pass 让 cast 立即结算——付费、效果、离区在断言前全部完成。
        /// 响应窗口本身的交互（打落/发动无效）见 TestCounterWindow。
        /// </summary>
        private static bool PlayCardSync(GameCore core, Player player, Card card,
            List<Entity> targets = null, Zone fromZone = Zone.Hand, int modeIndex = 0)
        {
            if (!GameActions.PlayCard(core, player, card, targets, fromZone, modeIndex))
                return false;
            GameActions.DrainStack(core);
            return true;
        }

        /// <summary>把 CardData 包成实例注入指定玩家手牌。</summary>
        private static Card InjectCard(GameCore core, Player owner, CardData data)
        {
            var card = new CardWrapper(data);
            card.SetController(owner);
            core.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
            return card;
        }

        /// <summary>
        /// 对目标执行一次"消灭"裁决（直连死亡决策表，DestroyEffect 死因）。
        /// 毁灭原子已删除（2026-09-03）——仪式摧毁回手特例仍挂在 DestroyEffect 死因上，直连同路径。
        /// </summary>
        private static void DestroyViaEffect(GameCore core, Player actor, Card target)
        {
            CardCore.Attribute.DeathRules.TryKill(target, CardCore.Attribute.DeathCause.DestroyEffect, actor, core.ZoneManager);
        }

        // ======================================== 统一计价锚点（规则一·平衡） ========================================

        /// <summary>
        /// 统一计价锚点验证（纯函数，不依赖卡表/对局）：
        /// 表值（Heal 0.5 / White→Gray / d(C)）、身材 1费=2属性、法术不折、回2命=1费、
        /// 9费挂3费全免（d(9)=0）、卡层组合费用（挂载口两向 + 抉择价差——2026-09-07 新层，
        /// 原「选发额外折」已删）、构筑代价抵消（无"超模"态）、关键词同享 d(C)、缺省档位 Ĉ。
        /// </summary>
        private static void TestCostAnchors()
        {
            // ---- 表值 ----
            Assert(CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Heal)?.BaseCost == 0.5f,
                   "计价锚：Heal BaseCost=0.5（回2命=1费）");
            Assert(ElementAffinities.GetAffinityForEffect(AtomicEffectType.GrantTaunt).PrimaryColor == ManaType.Green,
                   "计价锚：GrantTaunt 表 Green（2026-09-02 关键词纯三色迁移后；锚原按 White→灰 已过期）");
            var dd = ValueSystemConfigManager.Instance.GetOrCreateConfig().DelayDiscountConfig;
            Assert(System.Math.Abs(dd.At(1) - 1f) < 1e-4 && System.Math.Abs(dd.At(5) - 0.875f) < 1e-4
                   && System.Math.Abs(dd.At(9) - 0.75f) < 1e-4 && System.Math.Abs(dd.At(12) - 0.75f) < 1e-4,
                   "计价锚：d(C) 整卡最后折 d(1)=1 / d(5)=0.875 / d(9)=0.75 / 9费及以上钳0.75");

            // ---- 身材：1费 = 2点属性（灰）；卡层挂载口：默认2口，空置每口退1（用户定案例：2/2 白板 0费）----
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 1, 1)) == 0, "计价锚：1/1 白板 D=0（S1 − 空两口退2，灰下限0）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 0, 2)) == 0, "计价锚：0/2 白板 D=0（同上）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 2, 2)) == 0, "计价锚：2/2 白板 D=0（挂载口退费——定案例）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 3, 2)) == 1, "计价锚：3/2 白板 D=1（S3 − 退2）");

            // ---- 卡层挂载口纯函数（基准2口：空置退 ExtraRate、超出加 UnusedRate）----
            Assert(CardCompositionCost.MountSlotAdjust(MakeCostCard(Cardtype.Creature, 1, 1)) == -2,
                   "计价锚：挂载口 0 效果 → −2");
            Assert(CardCompositionCost.MountSlotAdjust(MakeCostCard(Cardtype.Creature, 1, 1, MakeEffect("Heal", 1))) == -1,
                   "计价锚：挂载口 1 效果 → −1");
            Assert(CardCompositionCost.MountSlotAdjust(MakeCostCard(Cardtype.Creature, 1, 1, MakeEffect("Heal", 1), MakeEffect("Heal", 1))) == 0,
                   "计价锚：挂载口 2 效果 → ±0（基准口）");
            Assert(CardCompositionCost.MountSlotAdjust(MakeCostCard(Cardtype.Creature, 1, 1,
                       MakeEffect("Heal", 1), MakeEffect("Heal", 1), MakeEffect("Heal", 1))) == 1,
                   "计价锚：挂载口 3 效果 → +1（用户例：1-1 挂三效果 原基础+1）");

            // ---- 卡层抉择价差纯函数（相等+0 / 差3→+1 / 差6以上→+2 封顶）----
            Assert(CardCompositionCost.ChoiceSpreadPremium(new List<Dictionary<int, float>>
                       { new Dictionary<int, float> { { (int)ManaType.Red, 4f } }, new Dictionary<int, float> { { (int)ManaType.Blue, 4f } } }) == 0,
                   "计价锚：抉择价差 相等 → +0");
            Assert(CardCompositionCost.ChoiceSpreadPremium(new List<Dictionary<int, float>>
                       { new Dictionary<int, float> { { (int)ManaType.Red, 4f } }, new Dictionary<int, float> { { (int)ManaType.Blue, 1f } } }) == 1,
                   "计价锚：抉择价差 3 → +1");
            Assert(CardCompositionCost.ChoiceSpreadPremium(new List<Dictionary<int, float>>
                       { new Dictionary<int, float> { { (int)ManaType.Red, 9f } }, new Dictionary<int, float> { { (int)ManaType.Blue, 1f } } }) == 2,
                   "计价锚：抉择价差 8 → +2（≥Step×Cap 封顶）");

            // ---- 法术不折：锚价全额（挂载退费只吃已有灰，无灰不变）----
            var fbEffect = MakeEffect("DealDamage", 4);
            fbEffect.AtomicEffects.Add(new AtomicEffectEntry { EffectType = "DrawCard", Value = 1 });
            var fb = CardCostService.Derive(MakeCostCard(Cardtype.Spell, null, null, fbEffect));
            Assert(fb.DerivedCost.GetValueOrDefault(ManaType.Red) == 4 && fb.DerivedCost.GetValueOrDefault(ManaType.Blue) == 1,
                   "计价锚：法术 4伤+1抽 = 红4+蓝1（锚价全额）");
            Assert(fb.Factor == 1f, "计价锚：法术 f=1（打出即生效，不折）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Spell, null, null, MakeEffect("Heal", 2))) == 1,
                   "计价锚：回2命 = 1费");

            // ---- 9费档：整卡最后折 f=d(9)=0.75（原 d(9)=0 全免已废）----
            var bigBody = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            bigBody.Cost[(int)ManaType.Gray] = 9;
            var big = CardCostService.Derive(bigBody);
            Assert(System.Math.Abs(big.Factor - 0.75f) < 1e-4 && big.DerivedTotal == 8 && big.OffsetRequirement == 0,
                   "计价锚：9费 9/9 挂3伤 → 整卡(9−退1+3)×0.75=8.25→D=8，Req=0");
            var midTier = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            midTier.Cost[(int)ManaType.Gray] = 5;
            var mid = CardCostService.Derive(midTier);
            Assert(mid.DerivedTotal == 10 && mid.OffsetRequirement == 5 && !mid.Conformant,
                   "计价锚：同卡声明 5 费档 → f=d(5)=0.875，整卡(8+3)×0.875=9.625→D=10，Req=5 无代价不符规则一");

            // ---- 两效果=基准口不加不减；f=d(C)（原选发额外折已删，与挂载口重复）----
            var chooser = MakeCostCard(Cardtype.Creature, 2, 2, MakeEffect("DealDamage", 3), MakeEffect("DrawCard", 1));
            chooser.Cost[(int)ManaType.Gray] = 4;
            var ch = CardCostService.Derive(chooser);
            Assert(System.Math.Abs(ch.Factor - 0.90625f) < 1e-4 && ch.DerivedTotal == 5 && ch.OffsetRequirement == 1,
                   "计价锚：两效果 f=d(4)=0.90625 整卡(2+4)×f=5.4→D=5");

            // ---- 超口加价随整卡同折（1-1 挂三效果 9费档：(1+3效果+超口1)×0.75=3.75→D=4）----
            var tripleMount = MakeCostCard(Cardtype.Creature, 1, 1,
                MakeEffect("DrawCard", 1), MakeEffect("DrawCard", 1), MakeEffect("DrawCard", 1));
            tripleMount.Cost[(int)ManaType.Gray] = 9;
            var tm = CardCostService.Derive(tripleMount);
            Assert(tm.DerivedTotal == 4,
                   "计价锚：1/1 挂三效果 9费档 → 整卡(S1+E3+超口1)×0.75=3.75→D=4（口费随整卡折）");

            // ---- 构筑代价抵消（无"超模"态：抵消完即符合规则一；白板退费后 D≤C 恒真，用带效果卡锚定）----
            var overBaseline = MakeCostCard(Cardtype.Creature, 2, 2, MakeEffect("DealDamage", 3), MakeEffect("DrawCard", 1));
            overBaseline.Cost[(int)ManaType.Gray] = 3; // 3费声明：f=d(3)=0.9375 → 整卡(2+4)×f=5.6→D=6
            var ov = CardCostService.Derive(overBaseline);
            Assert(ov.OffsetRequirement == 3 && !ov.Conformant,
                   "计价锚：3费声明 整卡折 D=6 → 抵扣需求 3，无代价 → 不符规则一");
            overBaseline.Effects[0].Costs = new List<CostEntry> { new CostEntry { CostType = (int)CostType.DiscardCard, Value = 3 } };
            var ov2 = CardCostService.Derive(overBaseline);
            Assert(ov2.OffsetProvided >= 3f && ov2.Conformant,
                   "计价锚：同卡挂弃3张代价（当量3）→ O≥Req → 符合规则一");

            // ---- 关键词计价：Grant 固定费 + 随整卡同折（挂载口退费并存）----
            var kwCard = MakeCostCard(Cardtype.Creature, 1, 1);
            kwCard.Keywords.Add("Taunt");
            kwCard.Cost[(int)ManaType.Green] = 1; // 嘲讽表色已迁 Green（09-02）——K 落绿桶
            var kw = CardCostService.Derive(kwCard);
            Assert(kw.DerivedTotal == 1 && kw.OffsetRequirement == 0,
                   "计价锚：嘲讽 K=1（表 Green→绿）d(1)=1，两口空退2（灰下限0）→ D=绿1=声明费");
            var kwBig = MakeCostCard(Cardtype.Creature, 9, 9);
            kwBig.Keywords.Add("Taunt");
            kwBig.Cost[(int)ManaType.Gray] = 9;
            var kwb = CardCostService.Derive(kwBig);
            Assert(kwb.DerivedTotal == 6 && kwb.OffsetRequirement == 0,
                   "计价锚：9费档 → (S9+K1−退2)×0.75=6 → D=6");

            // ---- 缺省档位 Ĉ：5/5 挂 3伤 → C=6（挂载退费 −1 与取整相抵后不变）----
            var noCost = MakeCostCard(Cardtype.Creature, 5, 5, MakeEffect("DealDamage", 3));
            var nc = CardCostService.Derive(noCost);
            Assert(nc.SuggestedTier == 6, $"计价锚：缺省档位 Ĉ=6（实际 {nc.SuggestedTier}）");

            // ---- 新代价当量（2026-09-07 补）：对手抽1=1费、对手回2点=1费 ----
            var opponentCostCard = MakeCostCard(Cardtype.Spell, null, null, MakeEffect("Heal", 2));
            opponentCostCard.Effects[0].Costs = new List<CostEntry>
            {
                new CostEntry { CostType = (int)CostType.OpponentDraw, Value = 1 },
                new CostEntry { CostType = (int)CostType.OpponentHeal, Value = 2 },
            };
            var occ = CardCostService.Derive(opponentCostCard);
            Assert(System.Math.Abs(occ.OffsetProvided - 2f) < 1e-3,
                   "计价锚：对手抽1（当量1）+ 对手回2点（当量0.5×2=1）→ O=2");

            // ---- 溢出治疗转临时上限（2026-09-07 定案：走 LifeUp 指示物，ceil半入上限/floor半入当前）----
            var overflowPlayer = new Player("VERIFY_OVERFLOW", 30);
            overflowPlayer.Heal(7);
            Assert(overflowPlayer.MaxHealth == 34 && overflowPlayer.Life == 33
                   && overflowPlayer.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 4,
                   "溢出治疗：满血30回复7 → LifeUp×4 → 34/33（用户定案例）");
            var evenOverflow = new Player("VERIFY_OVERFLOW2", 30);
            evenOverflow.Heal(4);
            Assert(evenOverflow.MaxHealth == 32 && evenOverflow.Life == 32
                   && evenOverflow.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 2,
                   "溢出治疗：满血30回复4 → LifeUp×2 → 32/32（偶溢出均分仍满）");
            var partialHeal = new Player("VERIFY_OVERFLOW3", 30);
            partialHeal.Life = 28;
            partialHeal.Heal(2); // 恰好补满（28+2=30，无溢出）——原用 Heal(3) 实溢出1会按定案转层
            Assert(partialHeal.MaxHealth == 30 && partialHeal.Life == 30
                   && partialHeal.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 0,
                   "常规治疗：未溢出照旧封顶（不触发层）");
            var overflowCreature = new CardWrapper(MakeCostCard(Cardtype.Creature, 2, 5));
            overflowCreature.Heal(7);
            Assert(overflowCreature.GetMaxLife() == 9 && overflowCreature.GetLife() == 8
                   && overflowCreature.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 4,
                   "溢出治疗（生物）：满血5回7 → LifeUp×4 → 上限9当前8（层换区清除）");

            // ---- 永久类指示物（2026-09-07：max 再分一类，换区不删、净化可清——框架锚）----
            var permCard = new CardWrapper(MakeCostCard(Cardtype.Creature, 2, 5));
            permCard.AddCounters("Awakening", 2); // 已登记 Duration=Permanent（苏醒倒计时）
            CardCore.Attribute.CounterRules.AddStatCounter(permCard, CardCore.Attribute.CounterRules.PowerUpCounter, 3);
            CardCore.Attribute.CounterRules.ClearAll(permCard); // 换区口径
            Assert(permCard.GetCounterCount("Awakening") == 2 && permCard.GetPower() == 2
                   && permCard.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0,
                   "永久类：换区清除跳过 Permanent 层（临时层照常清+反写）");
            CardCore.Attribute.CounterRules.PurgeAll(permCard); // 净化口径
            Assert(permCard.GetCounterCount("Awakening") == 0,
                   "永久类：净化全清（效果级移除是永久层唯一清除口）");

            // ---- 三轨计价（2026-09-09 定案）：法术宿主按永久档 / 生物按表默认档 / 光环按单回合档 ----
            // ① 同文本「攻+2」：生物宿主 E=round(1×2×1)=2（表默认 UntilEndOfTurn）；
            //    法术宿主 E=round(1×2×1.0/0.6)=3（设置类=永久，按永久档估算——×1.67 加价）
            var pricingSpell = MakeCostCard(Cardtype.Spell, null, null, MakeEffect("ModifyPower", 2));
            var pricingCreature = MakeCostCard(Cardtype.Creature, null, null, MakeEffect("ModifyPower", 2));
            var psR = CardCostService.Derive(pricingSpell);
            var pcR = CardCostService.Derive(pricingCreature);
            Assert(pcR.EAnchor == 2 && psR.EAnchor == 3,
                   $"三轨计价·轨别档位：同文本攻+2 生物宿主 E=2 / 法术宿主（永久档）E=3（实际 {pcR.EAnchor}/{psR.EAnchor}）");

            // ② 连接光环费 A：linkAuras 按**单回合指示物档**计价（来源须持续在场的折价）——
            //    stat 行=ModifyPower 代表原子 ×(0.6/0.6)=1；keyword 行=Grant 固定费 ×(0.6/1.0)=0.6
            var auraCard = MakeCostCard(Cardtype.Creature, 2, 2);
            var auraBase = CardCostService.Derive(auraCard);
            auraCard.ArrowDirections = HexDirection.Up;
            auraCard.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 1 });
            auraCard.LinkAuras.Add(new LinkAuraData { keyword = "Taunt" });
            var auraR = CardCostService.Derive(auraCard);
            var aLines = auraR.Breakdown.Where(l => l.Stage == "A").ToList();
            Assert(aLines.Count == 2
                   && System.Math.Abs(aLines[0].Value - 1f) < 1e-3       // Power1：1×(0.6/0.6)
                   && System.Math.Abs(aLines[1].Value - 0.6f) < 1e-3,    // Taunt：1×(0.6/1.0)
                   "三轨计价·光环档：linkAuras 单回合档计价（stat=代表原子相对系数 / keyword=Grant 固定费×0.6）");
            Assert(auraR.DerivedTotal >= auraBase.DerivedTotal,
                   "三轨计价·光环档：光环费并入推导费（不白送）");
        }

        // ======================================== 时点接线（P0）/ 衍生物（P1）/ 计数与日志（P2） ========================================

        /// <summary>合成触发卡：生物 1/1 灰 1 费，指定时点的 DrawCard 触发式（触发次数=手牌增量，可观察）。</summary>
        private static CardData TrigData(string id, params TriggerTiming[] timings)
        {
            var data = new CardData { ID = id, CardName = id, Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            data.Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 1 } };
            foreach (var t in timings)
            {
                data.Effects.Add(new CardEffectData
                {
                    Id = $"{id}_T{(int)t}",
                    DisplayName = id,
                    TriggerTiming = (int)t,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        new AtomicEffectEntry { EffectType = "DrawCard", Value = 1 }
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
            var snap1 = new Dictionary<ManaType, int>(pool1.AvailableMana);
            var snap2 = new Dictionary<ManaType, int>(pool2.AvailableMana);
            int i1 = pool1.GlobalTurnIndex, i2 = pool2.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;
            pool2.GlobalTurnIndex = 9;
            foreach (var t in AllManaTypes()) { pool1.AvailableMana[t] = 99; pool2.AvailableMana[t] = 99; }
            return (snap1, snap2, i1, i2);
        }

        private static void RestoreBanks(GameCore core, Player p1, Player p2,
            (Dictionary<ManaType, int> snap1, Dictionary<ManaType, int> snap2, int i1, int i2) s)
        {
            var pool1 = core.ElementPool.GetPool(p1);
            var pool2 = core.ElementPool.GetPool(p2);
            pool1.GlobalTurnIndex = s.i1;
            pool2.GlobalTurnIndex = s.i2;
            pool1.AvailableMana.Clear();
            foreach (var kv in s.snap1) pool1.AvailableMana[kv.Key] = kv.Value;
            pool2.AvailableMana.Clear();
            foreach (var kv in s.snap2) pool2.AvailableMana[kv.Key] = kv.Value;
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
        /// （墓地经 IPlaySource 打出的 FromZone=Graveyard 路径由仪式段（归土仪典）回归覆盖。）
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
                // ① 手牌打出：宣言 FromZone=Hand；入场 Source=CastPlayed、FromZone=Activation
                var a = InjectCard(core, p1, TrigData("VERIFY_ES_PLAYED"));
                enterEvents.Clear();
                Assert(GameActions.PlayCard(core, p1, a), "来源①：手牌打出声明成功");
                GameActions.DrainStack(core);
                Assert(playEvt != null && playEvt.FromZone == Zone.Hand, "使用宣言 FromZone=Hand");
                Assert(enterEvents.Count == 1 && enterEvents[0].Source == EnterSource.CastPlayed
                       && enterEvents[0].FromZone == Zone.Activation,
                       "打出入场 Source=CastPlayed、FromZone=Activation");
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
                int h1 = Hand();
                Assert(GameActions.PlayCard(core, p1, d), "双时点卡打出声明");
                GameActions.DrainStack(core);
                Assert(Hand() == h1 - 1 + 2 + 1,
                       "打出：登场+1、进场+1（超集语义）、观察者 +1（净 +2）");

                // 复活 D：OnPlay 不触发（Source=Revived）、OnSummon 触发、观察者 +1
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
                int h3 = Hand(); // 稳态后再测法术宣言
                var spell = InjectCard(core, p1, new CardData
                {
                    ID = "VERIFY_TPF_SPELL", CardName = "宣言测法术", Supertype = Cardtype.Spell,
                    Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 1 } },
                });
                Assert(GameActions.PlayCard(core, p1, spell), "打出无效果法术");
                GameActions.DrainStack(core);
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
                var atom = new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 0 };
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, new EffectExecutionContext
                {
                    Source = p2, Controller = p2,
                    Targets = new List<Entity> { z },
                    ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                });
                GameActions.DrainStack(core);
                Assert(Hand() == h2 + 1, "OnTargeted 经原子 StartApplying 触发（统一路由修复后可见）");

                RetireCards(core, p1, x, y, z);
            }
            finally
            {
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>原子三阶段统一路由（P0.2）：每阶段恰发布一次 + OnAtomicEffectResolution 触发式可达。</summary>
        private static void TestAtomicPhaseRouting(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);

            var phaseCounts = new Dictionary<CardCore.AtomicEffectPhase, int>();
            void OnPhase(AtomicEffectPhaseEvent e)
            {
                phaseCounts.TryGetValue(e.Phase, out var c);
                phaseCounts[e.Phase] = c + 1;
            }
            EventManager.Instance.Subscribe<AtomicEffectPhaseEvent>(OnPhase);
            try
            {
                var w = InjectCard(core, p1, TrigData("VERIFY_AR_RES", TriggerTiming.OnAtomicEffectResolution));
                Assert(GameActions.PlayCard(core, p1, w), "原子结算观察者打出");
                GameActions.DrainStack(core);
                // w 入场本身不发原子三阶段；清零后测一次原子执行
                phaseCounts.Clear();
                int Hand() => core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                int h0 = Hand();

                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                    new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 0 },
                    new EffectExecutionContext
                    {
                        Source = p1, Controller = p1,
                        Targets = new List<Entity> { p2 },
                        ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                    });
                GameActions.DrainStack(core);

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

                // ② 落手牌 ×2：非抽牌入手事件（喂 NonDrawDrawAccum 语义）
                int h0 = Hand();
                hands.Clear();
                SummonTokenHandlerUtil.Run(core, p1, Zone.Hand, 2);
                GameActions.DrainStack(core);
                Assert(Hand() == h0 + 2, "落手牌：+2 张");
                Assert(hands.Count == 2 && hands.All(e => !e.IsDraw), "CardEnterHandEvent ×2 且 IsDraw=false");

                // ③ 落牌组 ×1（洗入）
                int d0 = Deck();
                SummonTokenHandlerUtil.Run(core, p1, Zone.Deck, 1);
                Assert(Deck() == d0 + 1, "落牌组：+1 张");

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

                // ⑤ 落区费用三档（Value=10：战场 10 / 手牌 round(10×1.2)=12 / 牌组 round(10×1.1)=11）
                int CostOf(Zone zone)
                {
                    var def = new EffectDefinition
                    {
                        Id = "VERIFY_TOKEN_COST",
                        Effects = new List<AtomicEffectInstance>
                        {
                            new AtomicEffectInstance { Type = AtomicEffectType.SummonToken, Value = 10, ZoneParam = zone }
                        },
                    };
                    return CostDerivationService.DeriveElementCosts(def).Sum(c => c.Value);
                }
                int cBf = CostOf(Zone.Battlefield), cHand = CostOf(Zone.Hand), cDeck = CostOf(Zone.Deck);
                Assert(cBf == 10 && cHand == 12 && cDeck == 11,
                       $"落区费用三档：战场 {cBf} / 手牌 {cHand} / 牌组 {cDeck}（SummonDrop 系数生效）");
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
                        StringValue = "VERIFY_TOKEN_TPL", ZoneParam = dropZone,
                    },
                    new EffectExecutionContext
                    {
                        Source = caster, Controller = caster,
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
                    Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 1 } },
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
            var d = new CardData { ID = "VERIFY_COST", CardName = "计价锚点", Supertype = type, Power = power, Life = life };
            d.Effects.AddRange(effects);
            return d;
        }

        /// <summary>单原子效果（站场类时点——挂载判定只看卡超类，时点不影响计价）。</summary>
        private static CardEffectData MakeEffect(string atomType, int value)
        {
            var e = new CardEffectData { Id = "VERIFY_COST_EFF", TriggerTiming = (int)TriggerTiming.OnDeath };
            e.AtomicEffects = new List<AtomicEffectEntry> { new AtomicEffectEntry { EffectType = atomType, Value = value } };
            return e;
        }

        private static int DeriveTotal(CardData card) => CardCostService.Derive(card).DerivedTotal;

        // ======================================== 测试卡表费用重生成 ========================================

        /// <summary>
        /// 重推导测试卡组费用：逐卡按统一计价换建议档位分布（声明价作废），
        /// 逐卡输出 S/K/E/f/D/Ĉ 明细供人工过目，回写 TestDecks 下每一套卡组 JSON（形状不变）。
        /// </summary>
        [MenuItem("Tools/重推导测试卡表费用")]
        public static void RegenerateTestTableCosts()
        {
            string dir = Path.Combine(Application.dataPath, "Configs", "TestDecks");
            if (!Directory.Exists(dir))
            {
                Debug.LogError($"[Regen] 找不到目录 {dir}");
                return;
            }

            var files = Directory.GetFiles(dir, "*.json")
                .Where(f => !f.EndsWith(".meta"))
                .ToArray();
            if (files.Length == 0)
            {
                Debug.LogError($"[Regen] {dir} 下没有卡组 JSON");
                return;
            }

            foreach (var path in files)
                RegenOneDeck(path);
        }

        /// <summary>对单套卡组重推导费用并回写。</summary>
        private static void RegenOneDeck(string path)
        {
            string raw = File.ReadAllText(path);
            var cards = CardLoader.LoadCardsFromText(raw);
            foreach (var card in cards)
            {
                var r = CardCostService.Derive(card);
                card.Cost = r.SuggestedCost.ToDictionary(kv => (int)kv.Key, kv => (float)kv.Value);
                card.ResetCache();
                Debug.Log($"[Regen] {card.ID}({card.CardName}) S={r.S} K={r.K} E={r.EAnchor} f={r.Factor:0.###} D={r.DerivedTotal} → 档位{r.SuggestedTier}："
                          + string.Join(" ", r.SuggestedCost.Select(kv => $"{kv.Key}:{kv.Value}")));
            }

            // 回写：解析原 JSON（保留未参与计价的字段原样），仅替换 costList
            var wrapper = JsonUtility.FromJson<TestCardsConfigWrapper>(raw);
            var byId = cards.ToDictionary(c => c.ID);
            foreach (var entry in wrapper.cards)
            {
                if (entry == null || !byId.TryGetValue(entry.id, out var card)) continue;
                entry.costList = card.Cost
                    .Select(kv => new CostJsonEntry { manaType = kv.Key, amount = kv.Value })
                    .ToList();
            }
            File.WriteAllText(path, JsonUtility.ToJson(wrapper, true));
            Debug.Log($"[Regen] 已重写 {cards.Count} 张卡的费用 → {path}");
        }

        private static void Assert(bool condition, string label)
        {
            if (condition)
            {
                _pass++;
                Debug.Log($"[Verify] PASS — {label}");
            }
            else
            {
                _fail++;
                Debug.LogError($"[Verify] FAIL — {label}");
            }
        }
    }
}
