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

        [MenuItem("Tools/卡牌核心/端到端验证")]
        public static void RunVerification()
        {
            _pass = 0;
            _fail = 0;

            // 测试卡配置缺失时不再整体早退：目录/预言等不依赖卡表的段落照常验证
            //（卡表四段——地牌经济/法术/生物/棋盘——需要真实卡数据，缺失时跳过）
            var cardsData = new List<CardData>();
            string jsonPath = Path.Combine(Application.dataPath, "Configs/TestCreatureCards.json");
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

                // 统一计价：全表规则一巡检（声明档位 + 代价抵扣足够）
                var offenders = cardsData.Where(c => !CardCostService.Derive(c).Conformant).ToList();
                Assert(offenders.Count == 0,
                       $"规则一巡检：全表 {cardsData.Count} 张代价抵扣足够（不符 {offenders.Count}：{string.Join(",", offenders.Select(o => o.ID))}）");

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
                TestBoard(core, p1, p2, cardsData);
            }

            // 分支条件目录 + 信息族（宣言/预言）引擎流：不依赖卡表（合成卡驱动）
            TestCostAnchors();
            TestBranchCatalog();
            TestInformationFlow(core, p1, p2);
            TestKeywords(core, p1, p2);
            if (cardsData.Count > 0)
            {
                TestRituals(core, cardsData);
                TestNewRituals(core, cardsData);
            }

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
            Assert(GameActions.AddToElementPool(core, p1, hand1[0]), "主阶段放地牌成功");

            var pooled1 = core.ElementPool.GetPooledCards(p1);
            Assert(pooled1.Count == 1 && !pooled1[0].IsTapped, "地牌以可用（未横置）状态入场");

            if (hand1.Count > 1)
                Assert(!GameActions.AddToElementPool(core, p1, hand1[1]), "超过地牌槽上限拒绝（T1 上限 1）");

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
                Assert(!GameActions.PlayCard(core, p1, candidate, null),
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
                    Assert(GameActions.AddToElementPool(core, p2, hand2b[0]), "耗尽后槽位空出，可补充地牌");
            }
            else
            {
                if (hand2.Count > 0)
                    GameActions.AddToElementPool(core, p2, hand2[0]);
            }

            var pooled2 = core.ElementPool.GetPooledCards(p2).LastOrDefault();
            var pool2 = core.ElementPool.GetPool(p2);
            int gray2Before = pool2.AvailableMana[ManaType.Gray];
            EndTurnPumped(core, p2);
            if (pooled2 != null)
            {
                Assert(pool2.AvailableMana[ManaType.Gray] == gray2Before + 1, "结束阶段未横置地牌自动产 1 灰");
                Assert(pooled2.IsTapped, "自动产灰后地牌横置");
            }

            // ---- T3（p1）：地牌恢复 + 上限 3 + 台账 ----
            GameActions.SkipElementPool(core, p1);
            Assert(core.ElementPool.GetLandCap(p1) == 3, "T3 地牌槽上限 = 3");
            var landAfter = core.ElementPool.GetPooledCards(p1).FirstOrDefault();
            Assert(landAfter != null && !landAfter.IsTapped, "己方回合开始地牌恢复（解除横置）");

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

            bool played = GameActions.PlayCard(core, p1, fireball,
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

            var declare = new CardWrapper(data);
            declare.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(declare, Zone.Hand);

            var pool1 = core.ElementPool.GetPool(p1);
            pool1.AvailableMana[ManaType.Blue] += 4;
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            int blueBefore = pool1.AvailableMana[ManaType.Blue];
            int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;

            Assert(GameActions.PlayCard(core, p1, declare), "宣言分支卡成功打出");

            // 无 UI 的结算链同步完成：打出 −1 + 分支命中抽 1 = 手牌数不变
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore,
                   "打出 −1 且宣言命中抽 +1（分支经 PlayCard 真实触发）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1, "奖励抽牌来自牌库顶");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(declare), "分支法术结算后入墓地");
            Assert(core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "结算完成后发动区清空");
            Assert(pool1.AvailableMana[ManaType.Blue] == blueBefore - 2,
                   "元素费只扣一次（skipElementCost 防执行器双计费）");

            core.ZoneManager.GetZoneContainer(p2).Remove(marker, Zone.Hand);
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

            bool played = GameActions.PlayCard(core, p1, creature);
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
                    Assert(GameActions.PlayCard(core, p1, creature), "棋盘段生物成功打出");
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
                Assert(GameActions.PlayCard(core, p1, spell), "棋盘段法术成功打出");
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
                Assert(!GameActions.PlayCard(core, p1, blocked), "满场手动出牌被拒（MD 式预检）");
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
            Assert(all.Count >= 11, $"BranchConfig 目录加载（{all.Count} 个效果条目 ≥ 11）");
            if (all.Count == 0) return;

            Assert(BranchConfigTable.GetByEffectType("DealDamage")?.Conditions.Count(c => c.Kind == BranchConditionKind.OutcomeGate) == 3,
                   "伤害族 3 条件（DmgKillsTarget/TargetSurvived/Overkill）");
            Assert(BranchConfigTable.GetByEffectType("Heal")?.Conditions.Count >= 2, "治疗族 2 条件（Overheal/TargetStillWounded）");
            Assert(BranchConfigTable.GetByEffectType("DeclareHand")?.Conditions.Count >= 2, "宣言族 2 条件（DeclareHit/DeclareMiss）");
            Assert(BranchConfigTable.GetByEffectType("ProphecyNextCard")?.Conditions.Count >= 2, "预言族 2 条件（ProphecyHit/ProphecyMiss）");

            Assert(BranchConfigTable.GetDrawback("UnusableThisTurn")?.CostReduction == 1
                   && BranchConfigTable.GetDrawback("DiscardAtEndOfTurnIfInHand")?.CostReduction == 1,
                   "抽牌减费缺陷 ×2 各 −1（设计文稿值）");
            Assert(BranchConfigTable.GetFilterTier("ExactCard")?.Cost == 3
                   && BranchConfigTable.GetFilterTier("TypePlusRace")?.Cost == 2
                   && BranchConfigTable.GetFilterTier("SingleDimension")?.Cost == 1,
                   "检索维度档 3/2/1（设计文稿值）");

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

            // 信息族原子已入原子表（xlsm → JSON → 运行时表全链路）
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareHand") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareHandSampled") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareDeckTop") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareArrow") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("ProphecyNextCard") != null,
                   "信息族 5 原子已入原子表（ID 由描述哈希生成）");

            // ---- 维度有限域（宣言对象必须是有限明确范围；卡名等开放集被排除） ----
            Assert(ProphecyDimension.IsValidValue("Type", "Creature") && !ProphecyDimension.IsValidValue("Type", "Bogus"), "维度 Type 域校验");
            Assert(ProphecyDimension.IsValidValue("Color", "Red") && !ProphecyDimension.IsValidValue("Color", "Pink"), "维度 Color 域校验");
            Assert(ProphecyDimension.IsValidValue("CostParity", "Odd") && !ProphecyDimension.IsValidValue("CostParity", "Maybe"), "维度 CostParity 域校验");
            Assert(ProphecyDimension.IsValidValue("CostExact", "9") && !ProphecyDimension.IsValidValue("CostExact", "10"), "维度 CostExact 域 0..9");
            Assert(ProphecyDimension.IsValidValue("LinkArrow", "NE") && !ProphecyDimension.IsValidValue("LinkArrow", "All"), "维度 LinkArrow 限单方向");
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
            ProphecySystem.Reset();
        }

        // ======================================== 关键词行为（合成随从） ========================================

        /// <summary>
        /// 关键词行为验证（合成随从，不依赖卡表）：失调/冲锋/突袭/嘲讽/守卫/潜行/警戒/风怒/
        /// 先攻/连击/穿透/碾压/剧毒/吸血/系命/圣盾/坚韧/护甲/不灭/复生/再生/成长/辟邪/法术护盾。
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

            // ---- 2. 召唤失调 / 冲锋 / 突袭 ----
            combat.StartCombat(p1, p2);
            var sick = Make(p1, 3, 3);
            Assert(!combat.CanDeclareAttack(sick, p1), "召唤失调：入场当回合不可攻击");
            var charger = Make(p1, 3, 3, "Charge");
            Assert(combat.CanDeclareAttack(charger, p1) && combat.CanAttackTarget(charger, p2),
                   "冲锋：失调豁免，可立即攻击玩家");
            var rusher = Make(p1, 3, 3, "Rush");
            var enemy = Make(p2, 1, 9);
            Assert(combat.CanAttackTarget(rusher, enemy) && !combat.CanAttackTarget(rusher, p2),
                   "突袭：失调回合只能攻随从不能攻玩家");
            combat.EndCombat();

            // ---- 3. 嘲讽 / 碾压无视嘲讽 ----
            combat.StartCombat(p1, p2);
            var attacker = Make(p1, 3, 3);
            attacker.SummonedThisTurn = false;
            var taunter = Make(p2, 0, 9, "Taunt");
            Assert(!combat.CanAttackTarget(attacker, p2), "嘲讽：防守方有嘲讽随从时不能指定玩家");
            var overwhelmer = Make(p1, 3, 3, "Overwhelm");
            overwhelmer.SummonedThisTurn = false;
            Assert(combat.CanAttackTarget(overwhelmer, p2), "碾压：无视嘲讽");
            taunter.IsAlive = false; // 移除嘲讽者
            Assert(combat.CanAttackTarget(attacker, p2), "嘲讽随从清除后可指定玩家");
            combat.EndCombat();

            // ---- 4. 守卫：强制转移攻击目标 ----
            combat.StartCombat(p1, p2);
            var striker = Make(p1, 3, 3);
            striker.SummonedThisTurn = false;
            var victim = Make(p2, 2, 5);
            var guard = Make(p2, 1, 8, "Guard");
            combat.DeclareAttack(striker, victim);
            var participant = combat.Attackers.FirstOrDefault(a => a.Entity == striker);
            Assert(participant != null && participant.DeclaredTarget == guard && guard.IsTapped(),
                   "守卫：友方被选为目标时横置并强制转移攻击目标");
            combat.ExecuteDamage();
            Assert(victim.GetLife() == 5, "守卫转移：原目标未受伤");
            combat.EndCombat();

            // ---- 5. 潜行：不可被指定 + 攻击后移除 ----
            combat.StartCombat(p1, p2);
            var lurker = Make(p2, 2, 2, "Stealth");
            var hunter = Make(p1, 3, 3);
            hunter.SummonedThisTurn = false;
            Assert(!combat.CanAttackTarget(hunter, lurker), "潜行：不可被指定为攻击目标");
            var spy = Make(p1, 2, 2, "Stealth");
            spy.SummonedThisTurn = false;
            combat.DeclareAttack(spy, p2);
            Assert(!spy.HasKeyword("Stealth"), "潜行：攻击后移除");
            combat.ExecuteDamage();
            combat.EndCombat();

            // ---- 6. 警戒：攻击不横置（一回合一次） ----
            combat.StartCombat(p1, p2);
            var vigilant = Make(p1, 3, 3, "Vigilance");
            vigilant.SummonedThisTurn = false;
            combat.DeclareAttack(vigilant, p2);
            Assert(!vigilant.IsTapped(), "警戒：攻击不横置");
            Assert(CardCore.Attribute.KeywordRules.ShouldTap(vigilant), "警戒：一回合只生效一次（额度已耗）");
            combat.ExecuteDamage();
            combat.EndCombat();

            // ---- 7. 风怒：每回合两次 ----
            combat.StartCombat(p1, p2);
            var windfury = Make(p1, 1, 9, "Windfury");
            windfury.SummonedThisTurn = false;
            combat.DeclareAttack(windfury, p2);
            combat.ExecuteDamage();
            windfury.Untap();
            combat.StartCombat(p1, p2);
            Assert(combat.CanDeclareAttack(windfury, p1), "风怒：第二次攻击可用");
            combat.DeclareAttack(windfury, p2);
            combat.ExecuteDamage();
            Assert(windfury.AttacksThisTurn == 2 && !combat.CanDeclareAttack(windfury, p1), "风怒：两次后不可再攻");
            combat.EndCombat();

            // ---- 8. 先攻：目标死亡不反击 ----
            combat.StartCombat(p1, p2);
            var first = Make(p1, 5, 3, "FirstStrike");
            first.SummonedThisTurn = false;
            var bulky = Make(p2, 4, 3);
            combat.DeclareAttack(first, bulky);
            combat.ExecuteDamage();
            Assert(!bulky.IsAlive && first.GetLife() == 3, "先攻：目标死于先攻步，不反击");
            combat.EndCombat();

            // ---- 9. 连击：两步各结算一次 ----
            combat.StartCombat(p1, p2);
            var doubleS = Make(p1, 2, 9, "DoubleStrike");
            doubleS.SummonedThisTurn = false;
            var tank = Make(p2, 1, 5);
            combat.DeclareAttack(doubleS, tank);
            combat.ExecuteDamage();
            Assert(tank.GetLife() == 1 && doubleS.IsAlive, "连击：伤害结算两次（5命 −2×2 = 1）");
            combat.EndCombat();

            // ---- 10. 穿透：溢出给控制者 ----
            combat.StartCombat(p1, p2);
            int p2LifeBefore = p2.Life;
            var trampler = Make(p1, 5, 9, "Trample");
            trampler.SummonedThisTurn = false;
            var small = Make(p2, 1, 3);
            combat.DeclareAttack(trampler, small);
            combat.ExecuteDamage();
            Assert(!small.IsAlive && p2.Life == p2LifeBefore - 2, "穿透：溢出 2 点伤害给防守玩家");
            combat.EndCombat();

            // ---- 11. 碾压：邻接受击（注入邻接扩展点） ----
            combat.StartCombat(p1, p2);
            var hammer = Make(p1, 4, 9, "Overwhelm");
            hammer.SummonedThisTurn = false;
            var pivot = Make(p2, 1, 9);
            var neighbor = Make(p2, 1, 9);
            CardCore.CombatSystem.AdjacentResolver = c => c == pivot ? new[] { neighbor } : System.Array.Empty<Card>();
            combat.DeclareAttack(hammer, pivot);
            combat.ExecuteDamage();
            Assert(pivot.GetLife() == 5 && neighbor.GetLife() == 5, "碾压：目标与相邻随从各受 4 点（无反击）");
            CardCore.CombatSystem.AdjacentResolver = null;
            combat.EndCombat();

            // ---- 12. 剧毒：任意伤害致死 ----
            combat.StartCombat(p1, p2);
            var viper = Make(p1, 1, 9, "Poisonous");
            viper.SummonedThisTurn = false;
            var giant = Make(p2, 3, 10);
            combat.DeclareAttack(viper, giant);
            combat.ExecuteDamage();
            Assert(!giant.IsAlive && viper.IsAlive && viper.GetLife() == 6, "剧毒：1 点伤害致死 10 命随从（反击正常）");
            combat.EndCombat();

            // ---- 13. 吸血（恢复自身）/ 系命（回复角色） ----
            combat.StartCombat(p1, p2);
            var bat = Make(p1, 2, 3, "Lifesteal");
            bat.SummonedThisTurn = false;
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, bat, 2, false); // 受伤状态
            var prey = Make(p2, 0, 9);
            combat.DeclareAttack(bat, prey);
            combat.ExecuteDamage();
            Assert(bat.GetLife() == 3, "吸血：造成 2 伤害恢复随从自身");
            combat.EndCombat();

            combat.StartCombat(p1, p2);
            p1.Life = 20;
            var monk = Make(p1, 3, 3, "Lifelink");
            monk.SummonedThisTurn = false;
            combat.DeclareAttack(monk, p2);
            combat.ExecuteDamage();
            Assert(p1.Life == 23, "系命：造成 3 伤害回复角色");
            combat.EndCombat();

            // ---- 14. 圣盾 / 坚韧 / 护甲指示物 ----
            combat.StartCombat(p1, p2);
            var shielded = Make(p2, 1, 5, "DivineShield");
            var breaker = Make(p1, 4, 9);
            breaker.SummonedThisTurn = false;
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
            hitter.SummonedThisTurn = false;
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

            // ---- 15. 不灭 / 复生 ----
            var eternal = Make(p1, 2, 5, "Indestructible");
            var destroyDef = new EffectDefinition
            {
                Id = "VERIFY_DESTROY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Destroy } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = destroyDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { eternal },
            }, skipElementCost: true).Forget();
            Assert(eternal.IsAlive, "不灭：摧毁效果无效");

            var phoenix = Make(p1, 2, 5, "Reborn");
            phoenix.IsAlive = false;
            Assert(CardCore.Attribute.KeywordRules.TryReborn(phoenix)
                   && phoenix.IsAlive && phoenix.GetLife() == 1 && phoenix.IsTapped() && phoenix.SummonedThisTurn
                   && !phoenix.HasKeyword("Reborn"),
                   "复生：1 血回场、横置带失调、关键词消耗");

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
                   && CardCore.EffectTargetValidator.CanTarget(hermit, hermit, AtomicEffectType.AddCounters),
                   "辟邪：对手效果不可指定，友方可以");

            var warded = Make(p2, 2, 5, "SpellShield");
            var targets = new List<Entity> { warded, p2 };
            CardCore.Attribute.KeywordRules.ConsumeSpellShields(targets, foe1);
            Assert(targets.Count == 1 && targets[0] == p2 && !warded.HasKeyword("SpellShield"),
                   "法术护盾：首次成为对手效果目标时移出目标并消耗");

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
            Assert(GameActions.PlayCard(core, p1, trinityA), "仪式 0 费打出成功（无需任何元素）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(trinityA), "仪式占战场格");
            Assert(RitualSystem.Active != null && RitualSystem.Active.Card == trinityA
                   && RitualSystem.Active.Definition.id == "RITUAL_TRINITY_001",
                   "打出即激活为全局唯一任务");

            // ---- 2. 全局唯一：同玩家第二张顶掉第一张 ----
            var trinityB = InjectCard(core, p1, trinityData);
            Assert(GameActions.PlayCard(core, p1, trinityB), "第二张仪式打出成功");
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
                Assert(GameActions.PlayCard(core, p1, colored), $"竞速回合用 {color} 卡");
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
            Assert(GameActions.PlayCard(core, p2, blood), "血偿仪典打出（0 费）");
            Assert(RitualSystem.Active != null && RitualSystem.Active.Card == blood
                   && RitualSystem.CompletedAuras.Count == 1,
                   "完成态光环与进行中任务并存（光环占格存续，新仪式开新任务）");

            p2.Life = 100; // 测试脚手架：保证 3×10 可付（CanPay 严格大于）
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
            Assert(GameActions.PlayCard(core, p2, blood2), "第二张血偿打出（新任务）");
            DestroyViaEffect(core, p2, blood2);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Contains(blood2)
                   && !core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(blood2)
                   && RitualSystem.Active == null,
                   "进行中仪式被破坏 → 回手牌（非墓地），任务进度清空");

            Assert(GameActions.PlayCard(core, p2, blood2), "回手的仪式可再打出（进度重开）");
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
            Assert(GameActions.PlayCard(core, p1, survivalCard), "丰盈仪典 0 费打出（顶掉进行中任务）");

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
            Assert(GameActions.PlayCard(core, p1, infoCard), "窥渊仪典打出");

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
            Assert(!GameActions.PlayCard(core, p2, lockedCandidate), "被锁定的卡本回合不可使用");
            EndTurnPumped(core, p2);                    // 回合结束 → 锁定清空
            Assert(!LockRevealedAura.IsLockedThisTurn(lockedCandidate), "回合结束：锁定解除");

            // ---- 3. 归土仪典：自己送墓 30 → 墓地视手牌使用（每回合一次）----
            EnsureMainPhase(core, p1);
            var resourceCard = InjectCard(core, p1, resource);
            Assert(GameActions.PlayCard(core, p1, resourceCard), "归土仪典打出");

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
            Assert(GameActions.PlayCardFromGraveyard(core, p1, graveCreature), "墓地视手牌使用：第一张成功");
            Assert(!GameActions.PlayCardFromGraveyard(core, p1, graveCreature2), "每回合限一次：第二张被拒");

            // ---- 4. 疾风仪典：跳过准备阶段 ×2 → 额外回合 → 自毁 ----
            EnsureMainPhase(core, p1);
            var tempoCard = InjectCard(core, p1, tempo);
            Assert(GameActions.PlayCard(core, p1, tempoCard), "疾风仪典打出");

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
            Assert(GameActions.PlayCard(core, p1, handCard), "纳川仪典打出");

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

        /// <summary>把 CardData 包成实例注入指定玩家手牌。</summary>
        private static Card InjectCard(GameCore core, Player owner, CardData data)
        {
            var card = new CardWrapper(data);
            card.SetController(owner);
            core.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
            return card;
        }

        /// <summary>经效果执行器对目标执行一次 Destroy（直连，跳过元素费）。</summary>
        private static void DestroyViaEffect(GameCore core, Player actor, Card target)
        {
            var def = new EffectDefinition
            {
                Id = "VERIFY_RITUAL_DESTROY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Destroy } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = def,
                Source = actor,
                Controller = actor,
                Targets = new List<Entity> { target },
            }, skipElementCost: true).Forget();
        }

        // ======================================== 统一计价锚点（规则一·平衡） ========================================

        /// <summary>
        /// 统一计价锚点验证（纯函数，不依赖卡表/对局）：
        /// 表值（Heal 0.5 / White→Gray / d(C)）、身材 1费=2属性、法术不折、回2命=1费、
        /// 9费挂3费全免（d(9)=0）、选发额外折、构筑代价抵消（无"超模"态）、关键词同享 d(C)、缺省档位 Ĉ。
        /// </summary>
        private static void TestCostAnchors()
        {
            // ---- 表值 ----
            Assert(CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Heal)?.BaseCost == 0.5f,
                   "计价锚：Heal BaseCost=0.5（回2命=1费）");
            Assert(ElementAffinities.GetAffinityForEffect(AtomicEffectType.GrantTaunt).PrimaryColor == ManaType.Gray,
                   "计价锚：GrantTaunt(表 White) 归一为灰");
            var dd = ValueSystemConfigManager.Instance.GetOrCreateConfig().DelayDiscountConfig;
            Assert(System.Math.Abs(dd.At(1) - 1f) < 1e-4 && System.Math.Abs(dd.At(5) - 0.5f) < 1e-4 && System.Math.Abs(dd.At(9)) < 1e-4,
                   "计价锚：d(C) 表 d(1)=1 / d(5)=0.5 / d(9)=0");

            // ---- 身材：1费 = 2点属性（灰）----
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 1, 1)) == 1, "计价锚：1/1 白板 D=1");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 0, 2)) == 1, "计价锚：0/2 白板 D=1");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 2, 2)) == 2, "计价锚：2/2 白板 D=2");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 3, 2)) == 3, "计价锚：3/2 白板 D=3（2.5 远离零取整）");

            // ---- 法术不折：锚价全额 ----
            var fbEffect = MakeEffect("DealDamage", 4);
            fbEffect.AtomicEffects.Add(new AtomicEffectEntry { EffectType = "DrawCard", Value = 1 });
            var fb = CardCostService.Derive(MakeCostCard(Cardtype.Spell, null, null, fbEffect));
            Assert(fb.DerivedCost.GetValueOrDefault(ManaType.Red) == 4 && fb.DerivedCost.GetValueOrDefault(ManaType.Blue) == 1,
                   "计价锚：法术 4伤+1抽 = 红4+蓝1（锚价全额）");
            Assert(fb.Factor == 1f, "计价锚：法术 f=1（打出即生效，不折）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Spell, null, null, MakeEffect("Heal", 2))) == 1,
                   "计价锚：回2命 = 1费");

            // ---- 9费挂3费全免（落地延迟 d(9)=0）----
            var bigBody = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            bigBody.Cost[(int)ManaType.Gray] = 9;
            var big = CardCostService.Derive(bigBody);
            Assert(System.Math.Abs(big.Factor) < 1e-4 && big.DerivedTotal == 9 && big.OffsetRequirement == 0,
                   "计价锚：9费 9/9 挂3伤效果全免（f=d(9)=0），D=9，Req=0");
            var midTier = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            midTier.Cost[(int)ManaType.Gray] = 5;
            var mid = CardCostService.Derive(midTier);
            Assert(mid.DerivedTotal == 11 && mid.OffsetRequirement == 6 && !mid.Conformant,
                   "计价锚：同卡声明 5 费档 → f=d(5)=0.5，效果实付 2，D=11，Req=6 无代价不符规则一");

            // ---- 选发额外折（用户例：4费身材挂 3伤/抽1 两个站场效果，f=0.625−0.125=0.5）----
            var chooser = MakeCostCard(Cardtype.Creature, 2, 2, MakeEffect("DealDamage", 3), MakeEffect("DrawCard", 1));
            chooser.Cost[(int)ManaType.Gray] = 4;
            var ch = CardCostService.Derive(chooser);
            Assert(System.Math.Abs(ch.Factor - 0.5f) < 1e-4 && ch.DerivedTotal == 4 && ch.OffsetRequirement == 0,
                   "计价锚：选发两效果 f=0.5 → 挂载实付 2，D=4=档位");

            // ---- 构筑代价抵消（无"超模"态：抵消完即符合规则一）----
            var overBaseline = MakeCostCard(Cardtype.Creature, 2, 2);
            overBaseline.Cost[(int)ManaType.Gray] = 1; // 1费声明 2/2（D=2）
            var ov = CardCostService.Derive(overBaseline);
            Assert(ov.OffsetRequirement == 1 && !ov.Conformant,
                   "计价锚：1费声明 2/2 → 抵扣需求 1，无代价 → 不符规则一");
            overBaseline.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_COST_OFFSET",
                Costs = new List<CostEntry> { new CostEntry { CostType = (int)CostType.DiscardCard, Value = 1 } },
            });
            var ov2 = CardCostService.Derive(overBaseline);
            Assert(ov2.OffsetProvided >= 1f && ov2.Conformant,
                   "计价锚：同卡挂弃1张代价（当量1）→ O≥Req → 符合规则一");

            // ---- 关键词计价：Grant 固定费 + 同享 d(C) ----
            var kwCard = MakeCostCard(Cardtype.Creature, 1, 1);
            kwCard.Keywords.Add("Taunt");
            kwCard.Cost[(int)ManaType.Gray] = 1;
            var kw = CardCostService.Derive(kwCard);
            Assert(kw.DerivedTotal == 2 && kw.OffsetRequirement == 1,
                   "计价锚：嘲讽 K=1（White→灰）同享 d(1)=1 → D=2");
            var kwBig = MakeCostCard(Cardtype.Creature, 9, 9);
            kwBig.Keywords.Add("Taunt");
            kwBig.Cost[(int)ManaType.Gray] = 9;
            var kwb = CardCostService.Derive(kwBig);
            Assert(kwb.DerivedTotal == 9 && kwb.OffsetRequirement == 0,
                   "计价锚：9费档 d(9)=0 → 嘲讽随挂载折扣免费");

            // ---- 缺省档位 Ĉ：5/5 挂 3伤 → C=6（D=5+round(3×0.375)=6）----
            var noCost = MakeCostCard(Cardtype.Creature, 5, 5, MakeEffect("DealDamage", 3));
            var nc = CardCostService.Derive(noCost);
            Assert(nc.SuggestedTier == 6, $"计价锚：缺省档位 Ĉ=6（实际 {nc.SuggestedTier}）");
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
        /// 重推导测试卡表费用：逐卡按统一计价换建议档位分布（声明价作废），
        /// 逐卡输出 S/K/E/f/D/Ĉ 明细供人工过目，回写 TestCreatureCards.json（形状不变）。
        /// </summary>
        [MenuItem("Tools/卡牌核心/重推导测试卡表费用")]
        public static void RegenerateTestTableCosts()
        {
            string path = Path.Combine(Application.dataPath, "Configs/TestCreatureCards.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Regen] 找不到 {path}");
                return;
            }

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
