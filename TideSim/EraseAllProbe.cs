using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 枯竭「全取」探针（2026-11-15 临时，验完删）：P1 18 块地（刚好付得起 18 灰）+ 手牌枯竭，
    /// P2 元素池 3 块地 → 打出枯竭，断言对手池被**一次抹光**（不是只抹一张）。
    /// 用法：dotnet run --project TideSim -- --erase-all-probe
    /// </summary>
    internal static class EraseAllProbe
    {
        private const string LandId = "C_3D10E1A5";   // 教学·1（地牌）
        private const string EraseId = "C_91CE77A8";  // 枯竭
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

            Console.WriteLine("【枯竭·全取探针】P1 18 地 + 手牌枯竭（18 灰）｜P2 元素池 3 地");
            TargetSelectionService.AiOrder = null;
            var core = Setup();
            try
            {
                var p1 = core.Player1;
                var p2 = core.Player2;

                int myLands = core.ElementPool.GetPooledCards(p1).Count();
                int oppLands = core.ElementPool.GetPooledCards(p2).Count();
                Assert(myLands == 18, $"P1 元素池 18 地（实际 {myLands}）");
                Assert(oppLands == 3, $"P2 元素池 3 地（实际 {oppLands}）");

                var spell = (core.ZoneManager.GetCards(p1, Zone.Hand) ?? new List<Card>())
                    .FirstOrDefault(c => c.ID == EraseId);
                Assert(spell != null, "枯竭在手牌");

                var cost = GameActions.GetCardCost(spell, 0);
                Console.WriteLine($"    枯竭卡费=[{string.Join("+", cost.NonzeroColors().Select(k => k + "×" + cost[k]))}]");

                bool played = GameActions.PlayCard(core, p1, spell, null, Zone.Hand, 0, out var reject);
                Assert(played, $"打出枯竭（拒绝原因：{reject ?? "无"}）");
                GameActions.DrainStack(core);

                int after = core.ElementPool.GetPooledCards(p2).Count();
                Assert(after == 0, $"P2 元素池被全抹（3 → {after}）");
                Assert((core.ZoneManager.GetCards(p1, Zone.Graveyard) ?? new List<Card>()).Any(c => c.ID == EraseId),
                    "枯竭进墓地（已结算）");
            }
            finally { Teardown(core); }

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "枯竭全取探针：通过" : $"枯竭全取探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        private static GameCore Setup()
        {
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 18; i++) deck1.Add(CardCatalog.GetById(LandId));
            deck1.Add(CardCatalog.GetById(EraseId));
            var deck2 = new List<CardData>();
            for (int i = 0; i < 3; i++) deck2.Add(CardCatalog.GetById(LandId));

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
                startTurn = 20,   // 地牌槽上限随回合增长——18 灰需要 18 槽
                player = new TutorialSideState
                {
                    landCapBonus = 20,
                    lands = Enumerable.Range(0, 18)
                        .Select(_ => new TutorialLandSpec { card = LandId, tapped = false }).ToList(),
                    hand = new List<string> { EraseId },
                },
                ai = new TutorialSideState
                {
                    landCapBonus = 20,
                    lands = Enumerable.Range(0, 3)
                        .Select(_ => new TutorialLandSpec { card = LandId, tapped = true }).ToList(),
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
