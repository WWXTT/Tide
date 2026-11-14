using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 牺牲原子改版探针（2026-11-14 basics_10 修复）：行域改为候选声明
    /// 「己方战场:生物; 己方战场:结界;己方元素池:卡」——落点由解析层选定（结算期弹窗 /
    /// AI 与无头代选 / 超时兜底），handler 只做规则送墓 + 逐卡 +1 临时彩元素。
    ///
    /// 盘面 = 教学一 basics_10 时点：教学·2（C_95DC98E5）在场 + 教学·1（C_3D10E1A5）地牌在池，
    /// 注入走 TutorialScenarioSeeder（教学同口径）。断言只认外部证据：区移动 / 临时钱包
    /// TempElements[Any] / 元素池 PooledCards，不读 handler 内部状态。
    ///
    /// 场景：
    /// · A 双候选（生物+地牌）：无头代选取首=战场生物 → 教学·2 入墓、+1 彩、地牌留池；
    /// · B 同盘面 AiOrder 地牌排首：代选取地牌 → 地牌出池入墓、+1 彩、教学·2 留场存活；
    ///   （B 即人类弹窗里选地牌的结算路径——域含「己方元素池:卡」段的直接证据）
    /// · 附带：发动后教学·2 横置（生物主动效果横置代价）。
    /// 用法：dotnet run --project TideSim -- --sacrifice-probe
    /// </summary>
    internal static class SacrificeProbe
    {
        private const int RngSeed = 20261114;
        private const string UnitId = "C_95DC98E5";  // 教学·2（效果宿主=牺牲原子唯一引用者）
        private const string LandId = "C_3D10E1A5";  // 教学·1（basics_09 放置的地牌）

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

            Console.WriteLine("【牺牲探针】basics_10 盘面：教学·2 在场 + 教学·1 地牌在池 → 发动『元素合成』");
            ScenarioA();
            ScenarioB();

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "牺牲探针：通过" : $"牺牲探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        // ---------- 场景 A：双候选，无头代选取首（战场生物序在域首） ----------

        private static void ScenarioA()
        {
            Console.WriteLine("  —— A 双候选：代选取首=战场生物（教学·2 自身）");
            TargetSelectionService.AiOrder = null;
            var core = Setup();
            try
            {
                var p1 = core.Player1;
                var unit = OnField(core, p1, UnitId);
                Assert(unit != null, "A 教学·2 在场");
                Assert(PooledOf(core, p1, LandId) != null, "A 教学·1 地牌在池");

                var ok = Activate(core, p1, out var reason, out var def);
                Assert(ok, $"A 发动成功（被拒：{reason ?? "无"}）");
                Assert(def != null && def.Effects != null && def.Effects.Any(a => a.Type == AtomicEffectType.Sacrifice),
                    "A 效果定义含牺牲原子（装载零回落）");

                Assert(OnField(core, p1, UnitId) == null, "A 教学·2 送墓（代选取首=战场生物）");
                Assert(InGrave(core, p1, UnitId) != null, "A 教学·2 在墓地（非效果死亡，规则送墓）");
                Assert(TempAny(core, p1) == 1, $"A +1 临时彩元素（实际 {TempAny(core, p1)}）");
                Assert(PooledOf(core, p1, LandId) != null, "A 地牌留池（未被殃及）");
            }
            finally { Teardown(core); }
        }

        // ---------- 场景 B：同盘面，AiOrder 地牌排首 = 人类弹窗选地牌的结算路径 ----------

        private static void ScenarioB()
        {
            Console.WriteLine("  —— B 双候选：代选取地牌（=弹窗选地牌路径，验证域含元素池段）");
            TargetSelectionService.AiOrder = req => req.Candidates
                .OrderByDescending(e => (e as Card)?.GetZone() == Zone.ElementPool)
                .ThenBy(e => (e as Card)?.ID)
                .ToList();
            var core = Setup();
            try
            {
                var p1 = core.Player1;

                var ok = Activate(core, p1, out var reason, out _);
                Assert(ok, $"B 发动成功（被拒：{reason ?? "无"}）");

                Assert(PooledOf(core, p1, LandId) == null, "B 地牌出池（元素池:卡 候选段生效）");
                Assert(InGrave(core, p1, LandId) != null, "B 地牌入墓（余量写回后规则送墓）");
                Assert(TempAny(core, p1) == 1, $"B +1 临时彩元素（实际 {TempAny(core, p1)}）");
                var unit = OnField(core, p1, UnitId);
                Assert(unit != null && unit.IsAlive, "B 教学·2 留场存活");
                Assert(unit != null && unit.IsTapped(), "B 教学·2 发动后横置（主动效果代价）");
            }
            finally
            {
                TargetSelectionService.AiOrder = null;
                Teardown(core);
            }
        }

        // ======================================== 夹具 ========================================

        /// <summary>basics_10 时点盘面：教学二段式口径 InitGame(deferStart) → Seeder → StartGame。</summary>
        private static GameCore Setup()
        {
            var core = GameCore.Instance;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            var ids = new List<string> { UnitId, LandId };
            var deck1 = ids.Select(CardCatalog.GetById).ToList();
            var deck2 = new List<CardData> { CardCatalog.GetById(UnitId) };
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
                startTurn = 1,
                player = new TutorialSideState
                {
                    units = new List<TutorialUnitSpec> { new TutorialUnitSpec { card = UnitId, x = -1, z = -1 } },
                    lands = new List<TutorialLandSpec> { new TutorialLandSpec { card = LandId } },
                },
                ai = new TutorialSideState(),
            };
            TutorialScenarioSeeder.Apply(core, sc, _board);
            core.StartGame();
            return core;
        }

        private static void Teardown(GameCore core)
        {
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
            _board?.Dispose();
            _board = null;
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
        }

        private static bool Activate(GameCore core, Player p, out string reason, out EffectDefinition def)
        {
            reason = null;
            def = null;
            var unit = OnField(core, p, UnitId);
            if (unit == null) { reason = "教学·2 不在场"; return false; }
            def = GameActions.GetCastableEffectDefinitions(unit)
                .FirstOrDefault(d => d.Effects != null && d.Effects.Any(a => a.Type == AtomicEffectType.Sacrifice));
            if (def == null) { reason = "效果定义缺失（装载回落？）"; return false; }
            if (!GameActions.ActivateEffect(core, p, def, unit, 0, out reason))
                return false;
            GameActions.DrainStack(core);
            return true;
        }

        // ======================================== 读数 ========================================

        private static Card OnField(GameCore core, Player p, string id)
            => (core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                .FirstOrDefault(c => c.ID == id);

        private static Card InGrave(GameCore core, Player p, string id)
            => (core.ZoneManager.GetCards(p, Zone.Graveyard) ?? new List<Card>())
                .FirstOrDefault(c => c.ID == id);

        private static PooledCard PooledOf(GameCore core, Player p, string id)
            => core.ElementPool.GetPool(p).PooledCards.FirstOrDefault(pc => pc.SourceCard?.ID == id);

        private static int TempAny(GameCore core, Player p)
            => core.ElementPool.GetPool(p).TempElements[ManaType.Any];
    }
}
