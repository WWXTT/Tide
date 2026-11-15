using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using CardCore.Network;
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
            ScenarioC();
            ScenarioD();
            ScenarioE();

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "牺牲探针：通过" : $"牺牲探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        // ---------- 场景 C（2026-11-15 共享规格 v0.2 §9.3）：绿套牺牲按「己方元素池」组取地 ----------
        // 盘面：己方元素池 2 张已横置地 + 战场 1 只已横置生物；T3 脚本回合驱动绿策略牺牲排序
        // → 只取池内 2 张地（战场生物绝不当地祭）；组标签含「己方元素池」（TargetZoneGrouping 单源）。

        private static void ScenarioC()
        {
            Console.WriteLine("  —— C §9.3 绿套牺牲：池 2 横置地 + 场 1 横置生物 → 只祭池内地（区域组单源）");
            TargetSelectionService.AiOrder = null;
            var core = SetupC();
            try
            {
                var p1 = core.Player1;
                // 推进到全局回合 3（P1 回合——绿脚本 T3；StartGame=回合1）
                GameActions.EndTurn(core, p1);
                core.TurnEngine.CheckPhaseTransition();
                GameActions.EndTurn(core, core.Player2);
                core.TurnEngine.CheckPhaseTransition();
                Assert(core.TurnEngine.TurnNumber == 3
                       && ReferenceEquals(core.TurnEngine.TurnPlayer, p1),
                    "C 到达 T3（全局回合 3·P1 回合）");

                // 回合 1 播种时地牌槽上限只容 1 块（个人曲线）——第 2 块地被 Seeder 退回牌库底；
                // 到 T3（上限已 ≥2）再正式入池，凑成 §9.3 的「池 2 地」盘面。
                var deckLand = (core.ZoneManager.GetCards(p1, Zone.Deck) ?? new List<Card>())
                    .FirstOrDefault(c => (c as CardWrapper)?.GetData()?.ID == LandId)
                    ?? (core.ZoneManager.GetCards(p1, Zone.Hand) ?? new List<Card>())
                        .FirstOrDefault(c => (c as CardWrapper)?.GetData()?.ID == LandId);
                Assert(deckLand != null, "C 第 2 块地在牌库/手牌可寻");
                if (deckLand != null)
                    Assert(GameActions.AddToElementPool(core, p1, deckLand), "C 第 2 块地入池（T3 上限放行）");

                var unit = OnField(core, p1, UnitId);
                Assert(unit != null, "C 教学·2 在场");
                var pool = core.ElementPool.GetPooledCards(p1).Where(pc => pc?.SourceCard != null).ToList();
                Assert(pool.Count == 2, $"C 池内 2 张地（实际 {pool.Count}）");

                // 回合开始自动重置已过：此处显式横置（§9.3 盘面=已横置地+已横置生物）
                foreach (var pc in pool) pc.IsTapped = true;
                unit.Tap();

                var cands = new List<Entity> { unit };
                cands.AddRange(pool.Select(pc => (Entity)pc.SourceCard));
                var req = new TargetSelectionRequest
                {
                    Candidates = cands,
                    Chooser = p1,
                    PromptAtom = AtomicEffectType.Sacrifice,
                };
                req.ZoneGroups = TargetZoneGrouping.Build(req);

                var poolGroup = req.ZoneGroups.FirstOrDefault(g => g.Label == "己方元素池");
                Assert(poolGroup != null && poolGroup.Indices.Count == 2,
                    $"C 分组含「己方元素池」组×2（实际 {(poolGroup == null ? "无组" : poolGroup.Indices.Count.ToString())}）");
                var fieldGroup = req.ZoneGroups.FirstOrDefault(g => g.Label == "己方战场");
                Assert(fieldGroup != null && fieldGroup.Indices.Count == 1
                       && ReferenceEquals(req.Candidates[fieldGroup.Indices[0]], unit),
                    "C 生物落「己方战场」组");
                Assert(req.ZoneGroups[0].SideRank == 0, "C 组序己方在前");

                var picked = new CardCore.AI.GreenRampStrategy().OrderTargetCandidates(core, p1, req);
                Assert(picked != null && picked.Count == 2,
                    $"C 绿套 T3 只祭 2 张（实际 {picked?.Count ?? 0}）");
                Assert(picked != null && picked.All(e => e is Card c && c.GetZone() == Zone.ElementPool),
                    "C 祭的全是元素池地（组单源收窄）");
                Assert(picked != null && !picked.Contains(unit),
                    "C 已横置战场生物不被当成地祭");
            }
            finally { Teardown(core); }
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

        // ---------- 场景 D（2026-11-15 共享规格 v0.2 §9.4）：脚本区域令牌（TargetZoneTokens 单源） ----------
        // 同 C 盘面（池 2 地 + 场 1 生物），直调引擎单源 TargetZoneTokens.Resolve
        //（ScriptedAi.ResolveTargets 即此口的薄委托）：
        // 「己方元素池×N」按组取前 N；组不存在=令牌失败；混用令牌去重保序；旧令牌不受影响。

        private static void ScenarioD()
        {
            Console.WriteLine("  —— D §9.4 脚本区域令牌：己方元素池×N / 组不存在失败 / 混用去重 / 旧令牌不变");
            TargetSelectionService.AiOrder = null;
            var core = SetupC();
            try
            {
                var p1 = core.Player1;
                GameActions.EndTurn(core, p1);
                core.TurnEngine.CheckPhaseTransition();
                GameActions.EndTurn(core, core.Player2);
                core.TurnEngine.CheckPhaseTransition();
                var deckLand = (core.ZoneManager.GetCards(p1, Zone.Deck) ?? new List<Card>())
                    .FirstOrDefault(c => (c as CardWrapper)?.GetData()?.ID == LandId)
                    ?? (core.ZoneManager.GetCards(p1, Zone.Hand) ?? new List<Card>())
                        .FirstOrDefault(c => (c as CardWrapper)?.GetData()?.ID == LandId);
                Assert(deckLand != null, "D 第 2 块地可寻");
                if (deckLand != null)
                    Assert(GameActions.AddToElementPool(core, p1, deckLand), "D 第 2 块地入池");
                var poolD = core.ElementPool.GetPooledCards(p1).Where(pc => pc?.SourceCard != null).ToList();
                Assert(poolD.Count == 2, $"D 池内 2 张地（实际 {poolD.Count}）");
                var unit = OnField(core, p1, UnitId);
                Assert(unit != null, "D 教学·2 在场");
                var def = GameActions.GetCardEffectDefinitions(unit).FirstOrDefault(
                    d => d?.Effects != null && d.Effects.Any(a => a != null && a.Type == AtomicEffectType.Sacrifice));
                Assert(def != null, "D 教学·2 效果含牺牲原子（候选含池+场两段）");

                // legacy 口径镜像 ScriptedAi.ResolveEntity（hero=对方角色；其余=两侧场上找卡）
                Entity Legacy(string t)
                    => string.IsNullOrEmpty(t) || t == "hero"
                        ? core.Player2
                        : (Entity)OnField(core, p1, t) ?? OnField(core, core.Player2, t);

                var t2 = TargetZoneTokens.Resolve(core, p1, "己方元素池×2", unit, def, Legacy, out var zf2);
                Assert(!zf2 && t2 != null && t2.Count == 2
                       && t2.All(e => e is Card c && c.GetZone() == Zone.ElementPool),
                    "D 己方元素池×2 → 恰取 2 张池内地（组内引擎序）");
                var t1 = TargetZoneTokens.Resolve(core, p1, "己方元素池×1", unit, def, Legacy, out var zf1);
                Assert(!zf1 && t1 != null && t1.Count == 1
                       && t1[0] is Card c1 && c1.GetZone() == Zone.ElementPool,
                    "D 己方元素池×1 → 只取 1 张");
                var tAll = TargetZoneTokens.Resolve(core, p1, "己方元素池", unit, def, Legacy, out var zfAll);
                Assert(!zfAll && tAll != null && tAll.Count == 2, "D 无 ×N → 取全组");

                var tMiss = TargetZoneTokens.Resolve(core, p1, "对方墓地×1", unit, def, Legacy, out var zfMiss);
                Assert(zfMiss && (tMiss == null || tMiss.Count == 0),
                    "D 组不存在 → 令牌失败（不静默换区）");

                var tMix = TargetZoneTokens.Resolve(core, p1, "己方元素池×2,hero", unit, def, Legacy, out var zfMix);
                Assert(!zfMix && tMix != null && tMix.Count == 3
                       && tMix.Count(e => e is Card c2 && c2.GetZone() == Zone.ElementPool) == 2
                       && tMix[^1] is Player pl && ReferenceEquals(pl, core.Player2),
                    "D 混用「己方元素池×2,hero」→ 2 地+对方角色，按令牌序去重保序");

                var tHero = TargetZoneTokens.Resolve(core, p1, "hero", unit, def, Legacy, out var zfHero);
                Assert(!zfHero && tHero != null && tHero.Count == 1 && ReferenceEquals(tHero[0], core.Player2),
                    "D 旧令牌 hero 不变（=对方角色）");
                var tAuto = TargetZoneTokens.Resolve(core, p1, "auto", unit, def, Legacy, out var zfAuto);
                Assert(!zfAuto && tAuto == null, "D auto/null=引擎自动（返回 null 不变）");
            }
            finally { Teardown(core); }
        }

        // ---------- 场景 E（2026-11-15 共享规格 v0.2 §9.6 无头替代）：v20 ZoneGroups 线格式往返 ----------
        // 纯序列化往返（MemoryPack 信封→帧→解码）：Label/Start/Count 三字段无损；
        // 完整「引擎反问→客户端→回传翻译」链路（3a-2）在 Unity 回环验证器（编辑器空闲时跑）。

        private static void ScenarioE()
        {
            Console.WriteLine("  —— E §9.6(无头替代) v20 ZoneGroups 线格式往返：Label/Start/Count 无损");
            var msg = new MsgSelectRequest
            {
                RequestId = 7,
                ChooserSeat = 1,
                Title = "分组回环",
                Min = 1,
                Max = 2,
                Candidates = Array.Empty<NetEntityRef>(),
                Labels = Array.Empty<string>(),
                ZoneGroups = new[]
                {
                    new MsgSelectZoneGroup { Label = "己方战场", Start = 0, Count = 1 },
                    new MsgSelectZoneGroup { Label = "己方手牌", Start = 1, Count = 3 },
                },
            };
            var wire = NetworkSerializer.SerializeMessage(NetworkMessageType.SelectRequest, msg);
            var back = NetworkSerializer.DeserializePayload<MsgSelectRequest>(
                NetworkSerializer.DeserializeEnvelope(wire));
            Assert(back != null && back.ZoneGroups != null && back.ZoneGroups.Length == 2
                   && back.ZoneGroups[0].Label == "己方战场" && back.ZoneGroups[0].Start == 0 && back.ZoneGroups[0].Count == 1
                   && back.ZoneGroups[1].Label == "己方手牌" && back.ZoneGroups[1].Start == 1 && back.ZoneGroups[1].Count == 3,
                "E v20：ZoneGroups 三字段经完整协议栈往返无损");
            Assert(back != null && back.RequestId == 7 && back.Title == "分组回环",
                "E v20：既有字段不受新字段影响（RequestId/Title 同帧往返）");
        }

        private static void Teardown(GameCore core)
        {
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
            _board?.Dispose();
            _board = null;
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
        }

        /// <summary>场景 C 夹具（§9.3）：教学·2 在场 + 教学·1 地牌 ×2 在池（同 Setup 骨架、双地）。 </summary>
        private static GameCore SetupC()
        {
            var core = GameCore.Instance;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            var ids = new List<string> { UnitId, LandId, LandId };
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
                    lands = new List<TutorialLandSpec>
                    {
                        new TutorialLandSpec { card = LandId },
                        new TutorialLandSpec { card = LandId },
                    },
                },
                ai = new TutorialSideState(),
            };
            TutorialScenarioSeeder.Apply(core, sc, _board);
            core.StartGame();
            return core;
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
