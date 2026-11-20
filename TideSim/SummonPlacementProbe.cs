using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI.NeuralEnv;
using CardCore.Attribute;
using CardCore.AI;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 召唤落位专项探针（2026-11-20 计划《AI召唤落位与团结观测》§7 验收项 5/6/8 的可命令行部分）：
    /// A LAE 行：召唤主动效果展开=默认 1 行 + 每空格锚点 1 行（≤MaxSummonRows=12）、
    ///   [17]=计划首格互异、[18]=计划 id 互异、[19] 默认行 -1/锚点行 ≥0、
    ///   非召唤行 [18]/[19] 恒 -1（特征污染回归——Attack/EndTurn 等构造点 struct 默认 0 的坑）、
    ///   折叠后签名互异（FoldEquivalentRows 在 Enumerate 内已跑）。
    /// B Apply 锚点计划：锚点 8 计划 Apply → 逐只按序落位（TokenCreated 序=计划序）。
    /// C 默认计划（无 SummonCells）：与 SummonPlacementRules.DefaultCell 逐格一致（旧行为 A/B）。
    /// D 观测 v20：NCard=157/NGlobal=88/NAction=20/LayoutVersion=20；g[70..78] 占用码、
    ///   g[79..87] 生物口径邻接、[153] 邻接维、EngineRewardOf 摘要（深潭碑 then 召唤）。
    /// 用法：dotnet run --project TideSim -- --summon-placement-probe
    /// 未覆盖（需编辑器/Play）：§7 第 3/4 项人类取消 0 事件、事件先于 TokenCreatedEvent。
    /// </summary>
    internal static class SummonPlacementProbe
    {
        private const int RngSeed = 20261120;

        // 卡 id（教学卡表）
        private const string HiveId = "C_D1A00001";     // 战斗母巢：主动「召唤5个战斗衍生物」（D1A00013）
        private const string TokenName = "战斗衍生物";   // 衍生物模板（C_D1A00003）
        private const string FillerId = "C_DA499FAE";   // 教学·3 1/1（占格/填充）
        private const string LandId = "C_11B82155";     // 教学·35 地牌载体
        private const string SteleId = "C_2BD6EDE2";    // 深潭碑（元素充盈→then 召唤潮痕碑：奖励摘要用例）

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
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            Console.WriteLine("【召唤落位专项探针】LAE 计划行 / Apply 逐只落位 / 默认计划 A/B / 观测 v20（计划 §7-5/6/8）");

            Console.WriteLine("—— A1 LAE 召唤计划行（空盘 9 格：默认 1 + 锚点 9 = 10 行）");
            var core = Build(fillerUnits: 0);
            var me = core.Player1;
            var en = new LegalActionEnumerator();
            en.Enumerate(core, me);
            var summonRows = en.Actions.Where(a => a.Effect != null && GameActions.MainSummonCount(a.Effect) > 0).ToList();
            var free = GameBoard.SummonPlacementRules.FreeCells(_board, me);
            Assert(summonRows.Count == 1 + free.Count,
                $"行数 {summonRows.Count} = 默认 1 + 空格 {free.Count}（≤ MaxSummonRows 12）");
            Assert(summonRows.Count <= 12, "行数在 MaxSummonRows=12 内");
            Assert(summonRows.Select(r => r.Signature).Distinct().Count() == summonRows.Count,
                "折叠后签名互异（FoldEquivalentRows 不误并不同计划）");
            var defaults = summonRows.Where(r => r.SummonPlan == 0).ToList();
            var anchors = summonRows.Where(r => r.SummonPlan >= 1).ToList();
            Assert(defaults.Count == 1 && defaults[0].Interaction == null,
                "默认计划恰 1 行且不带 SummonCells（结算期 DefaultCell）");
            Assert(anchors.Select(r => r.SummonPlan).Distinct().Count() == anchors.Count
                   && anchors.All(r => r.PlacementCell == r.Interaction.SummonCells[0]),
                "锚点行 [17]=计划首格 且 计划 id 互异");
            // 特征断言（折叠后按签名找回行——Features 与 Actions 同序重排，改按「重枚举后特征扫描」）
            Assert(FeatureCheck(en, summonRows), "特征 [17]/[18]/[19] 语义（召唤行互异；默认行 [19]=-1；非召唤行恒 -1）");

            Console.WriteLine("—— A2 拥挤盘面（7 只占位 → 2 空格 = 3 行）");
            core = Build(fillerUnits: 7);
            en = new LegalActionEnumerator();
            en.Enumerate(core, core.Player1);
            summonRows = en.Actions.Where(a => a.Effect != null && GameActions.MainSummonCount(a.Effect) > 0).ToList();
            var free2 = GameBoard.SummonPlacementRules.FreeCells(_board, core.Player1);
            Assert(summonRows.Count == 1 + free2.Count,
                $"行数 {summonRows.Count} = 默认 1 + 空格 {free2.Count}（空格不足 N 不产生超额行；盘面=母巢+7 教学·3）");

            Console.WriteLine("—— B Apply 锚点计划：首格=最后空格的计划 → 逐只按序落位");
            core = Build(fillerUnits: 0);
            me = core.Player1;
            en = new LegalActionEnumerator();
            en.Enumerate(core, me);
            int anchorIdx = -1;
            for (int i = 0; i < en.Actions.Count; i++)
            {
                var a = en.Actions[i];
                if (a.Effect != null && GameActions.MainSummonCount(a.Effect) > 0
                    && a.SummonPlan >= 1 && a.PlacementCell == 8) { anchorIdx = i; break; } // 末格锚点
            }
            Assert(anchorIdx >= 0, "找到末格锚点计划行");
            if (anchorIdx >= 0)
            {
                var anchorRow = en.Actions[anchorIdx];
                var plan = anchorRow.Interaction.SummonCells;
                Assert(LegalActionEnumerator.Apply(core, me, anchorRow), "Apply 声明成功");
                GameActions.DrainStack(core);
                var tokens = TokensOf(core, me);
                Assert(tokens.Count == plan.Count, $"衍生物 {tokens.Count} 只 = 计划 {plan.Count} 只");
                var layout = GameBoard.BoardLayout.UnitCells(_board.PlayerIndex(me));
                bool allMatch = tokens.Count == plan.Count;
                var mismatch = new List<string>();
                for (int i = 0; i < tokens.Count && i < plan.Count; i++)
                {
                    if (!_board.TryGetCell(tokens[i], out int x, out int z)
                        || x != layout[plan[i]].x || z != layout[plan[i]].z)
                    {
                        allMatch = false;
                        var holder = _board.CardAt(layout[plan[i]].x, layout[plan[i]].z);
                        mismatch.Add($"#{i}: 期望({layout[plan[i]].x},{layout[plan[i]].z}) 实际({x},{z})"
                                     + $" 格主={holder?.ID ?? "空"}");
                    }
                }
                Assert(allMatch, "逐只落位=计划序（第 i 只钉 SummonCells[i]）"
                       + (allMatch ? "" : "；mismatch=[" + string.Join("; ", mismatch) + "]"
                                   + $" plan=[{string.Join(",", plan)}]"));
            }

            Console.WriteLine("—— C 默认计划（无 SummonCells）：与 SummonPlacementRules.DefaultCell 逐格一致");
            core = Build(fillerUnits: 0);
            me = core.Player1;
            en = new LegalActionEnumerator();
            en.Enumerate(core, me);
            int defaultIdx = -1;
            for (int i = 0; i < en.Actions.Count; i++)
            {
                var a = en.Actions[i];
                if (a.Effect != null && GameActions.MainSummonCount(a.Effect) > 0 && a.SummonPlan == 0) { defaultIdx = i; break; }
            }
            Assert(defaultIdx >= 0, "找到默认计划行");
            if (defaultIdx >= 0)
            {
                var defaultRow = en.Actions[defaultIdx];
                // 期望复算必须在 Apply **前**（结算后盘面已含新 tokens——母巢 2×3 块首空格漂移）
                var hive0 = core.ZoneManager.GetCards(me, Zone.Battlefield)
                    .FirstOrDefault(c => (c as CardWrapper)?.GetData()?.ID == HiveId);
                var expected = GameBoard.SummonPlacementRules.DefaultCell(_board, me, hive0,
                    core.ZoneManager.GetCards(me, Zone.Battlefield), 5);
                LegalActionEnumerator.Apply(core, me, defaultRow);
                GameActions.DrainStack(core);
                var tokens = TokensOf(core, me);
                // 现状口径（Resync 钉格每格一卡）：「同格钉」实际效果=首只在 DefaultCell、
                // 其余按入场序粘滞/first-free 落格（历史行为 2026-11-16 起如此——同格钉仅首只生效）
                var tokenCells = tokens.Select(t => _board.TryGetCell(t, out int x, out int z) ? (x, z) : (-1, -1)).ToList();
                Assert(tokens.Count > 0 && tokenCells.All(c => c.Item1 >= 0),
                    $"默认计划 {tokens.Count} 只全部落格（容量外不落=失败）");
                Assert(tokenCells.Contains((expected.x, expected.z)),
                    $"首只钉 DefaultCell({expected.x},{expected.z})——实际集 [{string.Join(",", tokenCells)}]");
                Assert(tokenCells.Distinct().Count() == tokenCells.Count, "每格一卡（Resync 钉格唯一性）");
            }

            Console.WriteLine("—— D 观测 v20（157/88/20 + 占用码/邻接/奖励摘要）");
            Assert(TideObservation.NCard == 157 && TideObservation.NGlobal == 88
                   && TideObservation.NAction == 20 && TideObservation.LayoutVersion == 20,
                $"常量 NCard={TideObservation.NCard} NGlobal={TideObservation.NGlobal} "
                + $"NAction={TideObservation.NAction} Layout={TideObservation.LayoutVersion}");
            var obs = TideObservation.Build(core, core.Player1, false, _board);
            var g = obs.Globals;
            var cells = GameBoard.BoardLayout.UnitCells(_board.PlayerIndex(core.Player1));
            int idx8 = 8;
            bool occOk = true;
            for (int i = 0; i < cells.Count; i++)
            {
                var occ = _board.CardAt(cells[i].x, cells[i].z);
                float expect = (occ == null ? 0f : 1f) / 2f; // 己方视角占用码
                if (Math.Abs(g[70 + i] - expect) > 1e-4f) { occOk = false; break; }
            }
            Assert(occOk, "g[70..78] 占用码与盘面一致（空=0 / 己方=0.5）");
            int idx0Adj = GameBoard.SummonPlacementRules.AdjacentOwnCreatures(_board, cells[idx8].x, cells[idx8].z, core.Player1);
            Assert(Math.Abs(g[79 + idx8] - idx0Adj / 6f) < 1e-4f,
                $"g[79+8] 邻接=单源复算 {idx0Adj}/6（生物口径）");
            CardIdentityService.EngineRewardOf(CardCatalog.GetById(SteleId),
                out int rwCount, out int rwType0, out float rwValue0);
            Assert(rwCount == 1 && rwType0 == CardIdentityService.AtomTypeIndex(nameof(AtomicEffectType.SummonToken))
                   && Math.Abs(rwValue0 - 1f) < 1e-4f,
                $"EngineRewardOf(深潭碑)=载荷{rwCount}/类型{rwType0}/值{rwValue0}（then 召唤 1 只）");

            Console.WriteLine(_fail == 0 ? "\n召唤落位专项探针全部通过 ✔" : $"\n失败 {_fail} 项 ✗");
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>特征语义断言：召唤行 [17] 互异、[18] 互异（默认 0 + 锚点 1..9）、[19] 默认 -1/锚点 ≥0；
        /// 非召唤行 [18]/[19] 恒 -1（污染回归）。Features 与 Actions 同序（BuildFeatures 逐行写）。</summary>
        private static bool FeatureCheck(LegalActionEnumerator en, List<TideAction> summonRows)
        {
            var bySig = new Dictionary<string, int>();
            for (int i = 0; i < en.Actions.Count; i++) bySig[en.Actions[i].Signature] = i;
            var f17 = new HashSet<float>();
            var f18 = new HashSet<float>();
            bool ok = true;
            foreach (var r in summonRows)
            {
                if (!bySig.TryGetValue(r.Signature, out int b)) { ok = false; break; }
                float v17 = en.Features[b * TideObservation.NAction + 17];
                float v18 = en.Features[b * TideObservation.NAction + 18];
                float v19 = en.Features[b * TideObservation.NAction + 19];
                f17.Add(v17);
                f18.Add(v18);
                if (r.SummonPlan == 0 && v19 != -1f) ok = false;   // 默认行 [19]=未估
                if (r.SummonPlan >= 1 && v19 < 0f) ok = false;     // 锚点行有预测
                if (v18 != r.SummonPlan) ok = false;               // [18]=计划 id
            }
            if (!ok || f18.Count != summonRows.Count) return false; // 计划 id 互异（默认 0 也唯一）
            // 非召唤行（EndTurn/攻击/出地等构造点）不得带 0 假信号
            for (int i = 0; i < en.Actions.Count; i++)
            {
                var a = en.Actions[i];
                if (a.Effect != null && GameActions.MainSummonCount(a.Effect) > 0) continue;
                if (en.Features[i * TideObservation.NAction + 18] != -1f
                    || en.Features[i * TideObservation.NAction + 19] != -1f) return false;
            }
            return true;
        }

        private static List<Card> TokensOf(GameCore core, Player me)
            => core.ZoneManager.GetCards(me, Zone.Battlefield)
                .Where(c => (c as CardWrapper)?.GetData()?.CardName == TokenName)
                .OrderBy(c =>
                {
                    var hash = c.ID.IndexOf('#');
                    return hash >= 0 && int.TryParse(c.ID.Substring(hash + 1), out var n) ? n : 0;
                })
                .ToList();

        // —— 装配（镜像 EndgameSolver.Build：InitGame + 场景注入 + StartGame + 棋盘接线） ——

        private static GameCore Build(int fillerUnits)
        {
            var core = GameCore.Instance;
            var units = new List<string> { HiveId };
            for (int i = 0; i < fillerUnits; i++) units.Add(FillerId);
            var deck1 = new List<string> { HiveId, FillerId, FillerId }
                .Concat(units).Append(LandId).Select(CardCatalog.GetById).Where(d => d != null).ToList();
            var deck2 = new[] { FillerId, FillerId }.Select(CardCatalog.GetById).Where(d => d != null).ToList();

            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;

            AttachBoard(core);
            AiStrategyRegistry.Bind(core.Player1, AiStrategy.General);
            AiStrategyRegistry.Bind(core.Player2, AiStrategy.General);
            TargetSelectionService.AiOrder = req => AiStrategy.DispatchOrder(AiStrategy.General, core, req?.Chooser, req);
            ResponseWindowService.AiResponder = null;

            var scenario = new TutorialScenario
            {
                startTurn = 10,
                firstPlayer = "P1",
                player = new TutorialSideState
                {
                    life = 30,
                    bank = new[] { 20, 20, 20, 20, 0, 0 }, // 主动召唤锚价现付：钱包给足
                    lands = new List<TutorialLandSpec>(),
                    units = units.Select(id => new TutorialUnitSpec { card = id }).ToList(),
                    hand = new List<string>(),
                    deck = new List<string> { FillerId, FillerId },
                },
                ai = new TutorialSideState
                {
                    life = 30,
                    deck = new List<string> { FillerId },
                },
            };
            TutorialScenarioSeeder.Apply(core, scenario, _board);
            core.StartGame();
            return core;
        }

        private static void AttachBoard(GameCore core)
        {
            DetachBoard();
            _board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.FlankNeighbors;
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole;
            BranchEngines.UnityAdjacentCountOf = _board.AdjacentOccupantCount;
            GameBoard.LinkAuraSystem.Attach(_board);
            GameBoard.MoveUnitRules.Attach(_board); // v20：无头装配同 TideHeadlessDriver 修复
        }

        private static void DetachBoard()
        {
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
            BranchEngines.UnityAdjacentCountOf = null;
            GameBoard.LinkAuraSystem.Detach();
            GameBoard.MoveUnitRules.Detach();
            _board?.Dispose();
            _board = null;
        }
    }
}
