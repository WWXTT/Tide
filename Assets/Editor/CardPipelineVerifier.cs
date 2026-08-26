using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace CardCore.Editor
{
    /// <summary>
    /// 端到端验证：读配置 → 组卡 → 真实对局中打出并结算。
    /// 覆盖法术（火球术 DealDamage+DrawCard）与生物（古树守卫，配置驱动血量、可被伤害效果击杀）。
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

            string jsonPath = Path.Combine(Application.dataPath, "Configs/TestCreatureCards.json");
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[Verify] 找不到测试卡配置: {jsonPath}");
                return;
            }

            var cardsData = CardLoader.LoadCardsFromText(File.ReadAllText(jsonPath));
            Debug.Log($"[Verify] 加载卡牌数据 {cardsData.Count} 张");

            var core = GameCore.Instance;
            var deck1 = CardLoader.BuildDeck(cardsData, 3);
            var deck2 = CardLoader.BuildDeck(cardsData, 3);
            core.InitGame(deck1, deck2);

            var p1 = core.Player1;
            var p2 = core.Player2;

            // 准备阶段 → 主阶段
            GameActions.SkipElementPool(core, p1);

            // 回合开始接线（2.1）：InitGame→StartGame→StartNewTurn 发布 TurnStartEvent，
            // GameCore 订阅后应已重置栈优先权持有者
            Assert(core.StackEngine.CurrentPriorityHolder != null,
                   $"回合开始接线生效（优先权持有者非空：{core.StackEngine.CurrentPriorityHolder?.Name}）");

            // 地牌经济（新资源模型）：上限曲线 / 补地牌 / 手动产出 / 费用门槛 / 结束产灰 / 台账
            TestLandEconomy(core, p1, p2);

            TestSpell(core, p1, p2, cardsData);
            TestCreature(core, p1, cardsData);

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
        private static void TestLandEconomy(GameCore core, Player p1, Player p2)
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
            Assert(GameActions.EndTurn(core, p1), "结束 T1 回合");
            Assert(pool1.AvailableMana[ManaType.Gray] == gray1, "已横置地牌结束阶段不产出");
            Assert(core.ElementPool.GetLandCap(p1) == 2 && core.ElementPool.GetLandCap(p2) == 2,
                   "T2 双方地牌槽上限同步 = 2（对手首回合即 2）");

            // ---- T2（p2）：耗尽→墓地→补充 循环 + 结束阶段自动产灰 ----
            GameActions.SkipElementPool(core, p2);
            var hand2 = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Hand));

            var oneCost = hand2.FirstOrDefault(c => TotalCost(c) == 1);
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
            GameActions.EndTurn(core, p2);
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
            core.ElementPool.GetPool(p1).GlobalTurnIndex = 9;
            core.ElementPool.GetPool(p1).AvailableMana[ManaType.Red] = 3;

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

        private static void TestCreature(GameCore core, Player p1, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.ID == "TEST_GREEN_003");
            Assert(data != null, "古树守卫配置存在");
            if (data == null) return;

            var creature = new CardWrapper(data);
            creature.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(creature, Zone.Hand);
            var pool = core.ElementPool.GetPool(p1);
            pool.AvailableMana[ManaType.Green] = 3;
            pool.AvailableMana[ManaType.Gray] = 1;

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
            //core.StackEngine.GetExecutor().Execute(kill);

            Assert(creature.GetLife() <= 0, $"受伤后血量≤0（实际{creature.GetLife()}）");
            Assert(!creature.IsAlive, "生物被伤害效果击杀");
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
