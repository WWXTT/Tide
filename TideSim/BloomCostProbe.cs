using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 沃土「代价：一个敌方单位获得 6 层护甲」探针（2026-11-15 临时，验完删）：
    /// P1 6 块绿地 + 手牌沃土（锚 9 绿 − 代价 3 = 净 6 绿）｜P2 场上 1 只小精灵。
    /// 打出后应当：① 扣 6 绿；② 对方小精灵 +6 护甲；③ 成长 +3 块地；④ 沃土进墓地。
    /// 反向：对方场上没有生物时，代价无目标 → 沃土不可施放。
    /// 用法：dotnet run --project TideSim -- --bloom-cost-probe
    /// </summary>
    internal static class BloomCostProbe
    {
        private const string LandId = "C_C3B25F32";     // 萌芽（绿3，当绿地牌用：入池锁绿）
        private const string UnitId = "C_F245F2F9";     // 小精灵（对方场上的代价目标）
        private const string BloomId = "C_A92F5E99";    // 沃土
        private const int RngSeed = 20261115;

        private static int _fail;
        private static GameBoard.BoardState _board;

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            CardCatalog.LoadAll();

            Console.WriteLine("【沃土·代价探针】6 绿地 + 手牌沃土（净 6 绿）｜对方 1 生物");
            TargetSelectionService.AiOrder = null;
            CastWithTarget();
            CastWithoutTarget();

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "沃土代价探针：通过" : $"沃土代价探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        private static void CastWithTarget()
        {
            Console.WriteLine("  —— A 对方有生物：代价 = 它获得 6 层护甲");
            var core = Setup(withEnemyUnit: true);
            try
            {
                var p1 = core.Player1;
                var p2 = core.Player2;
                Assert(core.ElementPool.GetPooledCards(p1).Count() == 6, "A 起始 6 地");
                var spell = (core.ZoneManager.GetCards(p1, Zone.Hand) ?? new List<Card>())
                    .FirstOrDefault(c => c.ID == BloomId);
                Assert(spell != null, "A 沃土在手牌");
                var enemy = (core.ZoneManager.GetCards(p2, Zone.Battlefield) ?? new List<Card>())
                    .FirstOrDefault(c => c.ID == UnitId);
                Assert(enemy != null, "A 对方小精灵在场");

                var cost = GameActions.GetCardCost(spell, 0);
                Assert(Math.Abs(cost.Total - 6f) < 0.01f, $"A 沃土净费 6（实际 {cost.Total}）");

                int armor0 = enemy.GetEffectiveCounterCount(KeywordRules.ArmorCounter);
                bool played = GameActions.PlayCard(core, p1, spell, null, Zone.Hand, 0, out var reject);
                Assert(played, $"A 打出沃土（拒绝原因：{reject ?? "无"}）");
                GameActions.DrainStack(core);

                int armor1 = enemy.GetEffectiveCounterCount(KeywordRules.ArmorCounter);
                Assert(armor1 - armor0 == 6, $"A 对方小精灵 +6 护甲（{armor0} → {armor1}）");
                int lands1 = core.ElementPool.GetPooledCards(p1).Count();
                Assert(lands1 == 9, $"A 成长 +3 地（6 → {lands1}）");
                Assert((core.ZoneManager.GetCards(p1, Zone.Graveyard) ?? new List<Card>()).Any(c => c.ID == BloomId),
                    "A 沃土进墓地");
            }
            finally { Teardown(core); }
        }

        private static void CastWithoutTarget()
        {
            Console.WriteLine("  —— B 对方没有生物：代价落到对方**角色**（角色也算对方有生命单位）");
            var core = Setup(withEnemyUnit: false);
            try
            {
                var p1 = core.Player1;
                var p2 = core.Player2;
                var spell = (core.ZoneManager.GetCards(p1, Zone.Hand) ?? new List<Card>())
                    .FirstOrDefault(c => c.ID == BloomId);
                Assert(spell != null, "B 沃土在手牌");
                int armor0 = p2.GetEffectiveCounterCount(KeywordRules.ArmorCounter);
                bool played = GameActions.PlayCard(core, p1, spell, null, Zone.Hand, 0, out var reject);
                Assert(played, $"B 仍可打出（拒绝原因：{reject ?? "无"}）");
                GameActions.DrainStack(core);
                int armor1 = p2.GetEffectiveCounterCount(KeywordRules.ArmorCounter);
                Assert(armor1 - armor0 == 6, $"B 对方角色 +6 护甲（{armor0} → {armor1}）");
            }
            finally { Teardown(core); }
        }

        private static GameCore Setup(bool withEnemyUnit)
        {
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 6 + 3; i++) deck1.Add(CardCatalog.GetById(LandId)); // 6 块在手 + 3 块给成长
            deck1.Add(CardCatalog.GetById(BloomId));
            var deck2 = new List<CardData>();
            deck2.Add(CardCatalog.GetById(UnitId));
            if (withEnemyUnit) deck2.Add(CardCatalog.GetById(UnitId));

            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;
            _board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.FlankNeighbors;
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole;

            var sc = new TutorialScenario
            {
                startTurn = 6,
                player = new TutorialSideState
                {
                    landCapBonus = 12,
                    lands = Enumerable.Range(0, 6)
                        .Select(_ => new TutorialLandSpec { card = LandId, tapped = false }).ToList(),
                    hand = new List<string> { BloomId },
                },
                ai = new TutorialSideState
                {
                    landCapBonus = 6,
                    units = withEnemyUnit
                        ? new List<TutorialUnitSpec> { new TutorialUnitSpec { card = UnitId, x = -1, z = -1 } }
                        : new List<TutorialUnitSpec>(),
                },
            };
            TutorialScenarioSeeder.Apply(core, sc, _board);
            core.StartGame();
            return core;
        }

        private static void Teardown(GameCore core)
        {
            _board?.Dispose();
            _board = null;
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
        }
    }
}
