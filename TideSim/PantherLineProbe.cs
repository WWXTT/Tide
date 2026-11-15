using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 暗影豹收尾探针（2026-11-15 第二版口径：**不再强求 T7 出豹**）：
    ///   A) T7 12 地（豹这一拍真付得出）→ 进豹脚本，小精灵 → 归根 → 豹直接落地；
    ///   B) T7 10 地（付不出）→ 不进豹脚本、不硬祭 4 张，走减半循环（小精灵→牺牲→归根→落地）长地，
    ///      豹留手/等下一拍——这就是「缓一个回合出、少烧手牌」。
    /// 断言只认外部证据：战场/元素池/墓地/手牌，不读策略内部状态。
    /// 用法：dotnet run --project TideSim -- --panther-probe
    /// </summary>
    internal static class PantherLineProbe
    {
        private const int RngSeed = 20261115;

        // 绿预组卡 id（TutorialCards.json）
        private const string IdSpirit = "C_F245F2F9";   // 小精灵 1/1，首卡给一手牌 −2 绿
        private const string IdReturn = "C_60D27A90";   // 归根 4 绿，回收 3
        private const string IdBloom = "C_A92F5E99";    // 沃土 6 绿，成长 3
        private const string IdHalve = "C_33367D9D";    // 减半
        private const string IdPanther = "C_B0EF3C08";  // 暗影豹 12 费（灰9+绿3）
        private const string IdSkill = "C_AC7FC01F";    // 英雄技能=牺牲
        private const string IdRedFiller = "C_57BA44EC";// 火苗（P2 垫牌，不行动）

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
            Console.WriteLine("【暗影豹收尾探针】不再强求 T7：付得出就打，付不出就循环长地缓一回合");
            ScenarioA();
            ScenarioB();
            ScenarioC();
            ScenarioD();
            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "暗影豹线探针：通过" : $"暗影豹线探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        // ---------- A：T7 12 地（付得出）→ 豹直接落地 ----------

        private static void ScenarioA()
        {
            Console.WriteLine("  —— A T7 12 地（付得出）：小精灵 → 归根(2) → 暗影豹落地");
            var core = Setup(12);
            try
            {
                var p1 = core.Player1;
                Assert(core.TurnEngine.TurnNumber == 7 && ReferenceEquals(core.TurnEngine.TurnPlayer, p1),
                    "到达 T7（全局回合 7·P1 回合）");
                Assert(core.ElementPool.GetPooledCards(p1).Count == 12, "盘面：地牌池 12 张（豹付得出）");

                int gyBefore = GraveCount(core, p1);
                RunGreenTurn(core);

                var panther = OnField(core, p1, IdPanther);
                Assert(panther != null && panther.IsAlive, "暗影豹已落地且存活");
                Assert(HandCount(core, p1, IdPanther) == 0, "手牌里的暗影豹已打出（不在手）");
                Assert(GraveCount(core, p1) >= 1, $"有牌进墓（墓地 {gyBefore} → {GraveCount(core, p1)}）");
                Console.WriteLine($"      [盘面] 地={core.ElementPool.GetPooledCards(p1).Count} 手={HandNames(core, p1).Count} 张");
            }
            finally { Teardown(core); }
        }

        // ---------- B：T7 10 地（付不出）→ 不硬出，循环长地 ----------

        private static void ScenarioB()
        {
            Console.WriteLine("  —— B T7 10 地（付不出）：不硬祭 4 张，走减半循环，豹留手");
            var core = Setup(10);
            try
            {
                var p1 = core.Player1;
                Assert(core.TurnEngine.TurnNumber == 7 && ReferenceEquals(core.TurnEngine.TurnPlayer, p1),
                    "到达 T7（全局回合 7·P1 回合）");
                Assert(core.ElementPool.GetPooledCards(p1).Count == 10, "盘面：地牌池 10 张（豹差 2 费）");

                int gyBefore = GraveCount(core, p1);
                RunGreenTurn(core);

                var panther = OnField(core, p1, IdPanther);
                Assert(panther == null, "这一拍没有硬出暗影豹（不再强求 T7）");
                Assert(HandCount(core, p1, IdPanther) == 1, "暗影豹留在手里等下一拍");
                int landAfter = core.ElementPool.GetPooledCards(p1).Count;
                Assert(landAfter >= 10, $"减半循环继续长地（地 10 → {landAfter}）");
                Assert(GraveCount(core, p1) >= gyBefore, $"循环有送墓（墓地 {gyBefore} → {GraveCount(core, p1)}）");
                bool looped = InGrave(core, p1, IdSpirit) != null || OnField(core, p1, IdSpirit) != null;
                Assert(looped, "小精灵已打出（减半循环的燃料）");
            }
            finally { Teardown(core); }
        }

        private static void RunGreenTurn(GameCore core)
        {
            var p1 = core.Player1;
            var strategy = new GreenRampStrategy();
            AiStrategyRegistry.Bind(p1, strategy);
            ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;
            var ctrl = new BattleController();
            BindBoard(core, ctrl);
            new SimpleAI(strategy).TakeTurn(ctrl);
        }

        // ======================================== 夹具 ========================================

        // ---------- C/D：回收三格的换位（直调策略排序：12 地换豹，10 地仍是减半） ----------

        private static void ScenarioC() => AssertRecoverThird("C", 12, expectPanther: true);
        private static void ScenarioD() => AssertRecoverThird("D", 10, expectPanther: false);

        private static void AssertRecoverThird(string tag, int lands, bool expectPanther)
        {
            Console.WriteLine($"  —— {tag} {lands} 地：回收三格第三格 = {(expectPanther ? "暗影豹" : "减半")}");
            var core = Setup(lands);
            try
            {
                var p1 = core.Player1;
                var cards = new List<Card>();
                foreach (var id in new[] { IdPanther, IdHalve, IdSpirit, IdReturn, IdBloom })
                {
                    var c = TakeFromDeck(core, p1, 1, id).FirstOrDefault();
                    if (c != null) cards.Add(c);
                }
                var req = new TargetSelectionRequest
                {
                    Candidates = cards.Cast<Entity>().ToList(),
                    Chooser = p1,
                    PromptAtom = AtomicEffectType.RecoverToHand,
                };
                req.ZoneGroups = TargetZoneGrouping.Build(req);
                var ordered = new GreenRampStrategy().OrderTargetCandidates(core, p1, req);
                var top = ordered.Take(3).Select(e => (e as CardWrapper)?.GetData()?.CardName ?? "?").ToList();
                Console.WriteLine($"      [排序] {string.Join(" > ", top)}");
                bool has = top.Contains("暗影豹"), hasHalve = top.Contains("减半");
                if (expectPanther)
                {
                    Assert(has, "回收三格含「暗影豹」");
                    Assert(!hasHalve, "「减半」让位（不在前三）");
                }
                else
                {
                    Assert(hasHalve, "回收三格含「减半」");
                    Assert(!has, "「暗影豹」不占格（付不出）");
                }
            }
            finally { Teardown(core); }
        }
        private static GameCore Setup(int lands, string[] gyIds = null)
        {
            var core = GameCore.Instance;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;

            var deck1 = new List<CardData>();
            for (int i = 0; i < 24; i++) deck1.Add(CardCatalog.GetById(IdReturn));
            for (int i = 0; i < 24; i++) deck1.Add(CardCatalog.GetById(IdSpirit));
            for (int i = 0; i < 6; i++) deck1.Add(CardCatalog.GetById(IdBloom));
            for (int i = 0; i < 3; i++) deck1.Add(CardCatalog.GetById(IdHalve));
            for (int i = 0; i < 3; i++) deck1.Add(CardCatalog.GetById(IdPanther));
            var deck2 = new List<CardData>();
            for (int i = 0; i < 20; i++) deck2.Add(CardCatalog.GetById(IdRedFiller));

            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true,
                skillCardId1: IdSkill);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;
            _board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.FlankNeighbors;
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole;
            core.StartGame();

            var p1 = core.Player1;
            // 推进到全局回合 7（P1 回合）：双方各空过 3 个回合
            for (int i = 0; i < 6 && !core.IsGameOver; i++)
            {
                GameActions.EndTurn(core, core.TurnEngine.TurnPlayer);
                core.TurnEngine.CheckPhaseTransition();
            }

            // 模拟此前的成长（沃土）：地牌槽上限补到 lands（T7 基础上限 7）
            for (int i = 0; i < Math.Max(0, lands - 7); i++) core.ElementPool.AddLandCapBonus(p1);

            // 手牌清空回牌库（起手/抽牌会带进无关牌）
            foreach (var c in (core.ZoneManager.GetCards(p1, Zone.Hand) ?? new List<Card>()).ToList())
                core.ZoneManager.MoveCard(c, p1, Zone.Hand, Zone.Deck);

            // 元素池：抽 lands 张牌铺地（未横置——本回合由小精灵/归根把它们横置出来）
            foreach (var card in TakeFromDeck(core, p1, lands, null))
                GameActions.AddToElementPool(core, p1, card);

            // 手牌：小精灵 / 归根 / 暗影豹 / 沃土 / 减半
            foreach (var id in new[] { IdSpirit, IdReturn, IdPanther, IdBloom, IdHalve })
            {
                var c = TakeFromDeck(core, p1, 1, id).FirstOrDefault();
                if (c != null && c.GetZone() != Zone.Hand)
                    core.ZoneManager.MoveCard(c, p1, c.GetZone(), Zone.Hand);
            }
            // 墓地（默认 3 张；C/D 场景按名单摆 5 张，用来验证回收三格的取舍）
            string[] gy = gyIds ?? new string[] { null, null, null };
            foreach (var id in gy)
            {
                var c = TakeFromDeck(core, p1, 1, id).FirstOrDefault();
                if (c != null) core.ZoneManager.MoveCard(c, p1, c.GetZone(), Zone.Graveyard);
            }
            return core;
        }

        /// <summary>从牌库按 id（null=任意）抽 <=count 张并移到「手牌」，返回实例。</summary>
        private static List<Card> TakeFromDeck(GameCore core, Player p, int count, string id)
        {
            var deck = (core.ZoneManager.GetCards(p, Zone.Deck) ?? new List<Card>()).ToList();
            var picks = new List<Card>();
            foreach (var c in deck)
            {
                if (picks.Count >= count) break;
                if (id != null && c.ID != id) continue;
                core.ZoneManager.MoveCard(c, p, Zone.Deck, Zone.Hand);
                picks.Add(c);
            }
            return picks;
        }

        private static void BindBoard(GameCore core, BattleController ctrl)
        {
            CombatSystem.AdjacentResolver = _board.FlankNeighbors;
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole;
            BranchEngines.UnityAdjacentCountOf = _board.AdjacentOccupantCount;
            GameBoard.LinkAuraSystem.Attach(_board);
            ctrl?.BindBoard(_board);
        }

        private static void Teardown(GameCore core)
        {
            AiStrategyRegistry.Clear();
            ResponseWindowService.AiResponder = null;
            TargetSelectionService.AiOrder = null;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
            BranchEngines.UnityAdjacentCountOf = null;
            GameBoard.LinkAuraSystem.Detach();
            _board?.Dispose();
            _board = null;
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
        }

        // ======================================== 读数 ========================================

        private static Card OnField(GameCore core, Player p, string id)
            => (core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>()).FirstOrDefault(c => c.ID == id);

        private static Card InGrave(GameCore core, Player p, string id)
            => (core.ZoneManager.GetCards(p, Zone.Graveyard) ?? new List<Card>()).FirstOrDefault(c => c.ID == id);

        private static int HandCount(GameCore core, Player p, string id)
            => (core.ZoneManager.GetCards(p, Zone.Hand) ?? new List<Card>()).Count(c => c.ID == id);

        private static List<string> HandNames(GameCore core, Player p)
            => (core.ZoneManager.GetCards(p, Zone.Hand) ?? new List<Card>()).Select(c => c.ID).ToList();

        private static List<string> GraveNames(GameCore core, Player p)
            => (core.ZoneManager.GetCards(p, Zone.Graveyard) ?? new List<Card>()).Select(c => c.ID).ToList();

        private static int GraveCount(GameCore core, Player p)
            => (core.ZoneManager.GetCards(p, Zone.Graveyard) ?? new List<Card>()).Count;
    }
}
