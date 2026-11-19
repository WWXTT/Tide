using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.AI.NeuralEnv;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 残局求解器·阶段1（2026-11-19）：**本回合能否斩杀**。
    ///
    /// 口径（与「训练一个模型」互补，不替代）：
    /// - 胜负 = 对手生命 ≤ 0（单回合内）；不考虑对手响应（对手不响应=我方上界，见 TerribleTruth）。
    /// - 合法动作 = 训练侧同一份 <see cref="LegalActionEnumerator"/>（不复制「什么算合法」，避免两套规则漂移）。
    /// - 试打 = <see cref="LegalActionEnumerator.Apply"/> + <see cref="GameActions.DrainStack"/>。
    /// - 回溯 = **重建局面 + 重放动作下标**（尚无状态快照/回滚，见 TurnStartSnapshot 的「重开本回合未做」）。
    ///   每个搜索边 = 一次 InitGame + Seeder + StartGame + len(path) 次 Apply —— 这是本阶段要量的成本。
    ///
    /// 用法：dotnet run --project TideSim -- --lethal-probe
    /// 验收：求解器报「能斩杀」的线路，执行后对手生命必须 ≤ 0（同进程回放自证）；
    ///       报「不能」的线路，必须在节点预算内穷尽且每个终局生命 > 0。
    /// </summary>
    internal static class EndgameSolver
    {
        private const int RngSeed = 20261119;

        // 卡 id（红·快攻 / 教学卡表）
        private const string BloodId = "C_E105FABA";   // 血偿：红 3 净费 + 代价（消灭一个己方生物），对目标 5 伤
        private const string KindlingId = "C_138CE290";// 火苗 2/2 0 费（代价牺牲位）
        private const string LandId = "C_11B82155";    // 教学·35 地牌载体（tokenType=Red）
        private const string FillerId = "C_DA499FAE";  // 教学·3 1/1（牌库填充）

        private static int _fail;
        private static GameBoard.BoardState _board;

        /// <summary>一个待求解局面（P1=我方，P2=对手）。</summary>
        private sealed class Case
        {
            public string Title;
            public string Note;
            public int OppLife;
            public List<string> Hand = new List<string>();
            public List<string> Units = new List<string>();
            public int[] Bank = { 0, 3, 0, 0, 0, 0 };   // [Red]=3
            public bool ExpectLethal;
        }

        private sealed class SearchCtx
        {
            public int Nodes;
            public int MaxNodes = 6000;
            public int MaxMs = 30000;
            public Stopwatch Watch = Stopwatch.StartNew();
            public List<int> BestPath;
            public bool Expired => Nodes >= MaxNodes || Watch.ElapsedMilliseconds >= MaxMs;
        }

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            CardCatalog.LoadAll();
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            Console.WriteLine("【残局求解器 阶段1】本回合斩杀判定：枚举=训练侧 LegalActionEnumerator，回溯=重建+重放");

            var cases = new List<Case>
            {
                new Case
                {
                    Title = "A 一击斩杀",
                    Note = "血偿 5 伤 ≥ 对手 5 命：搜索应在本回合找到一条出牌线",
                    OppLife = 5,
                    Hand = new List<string> { BloodId },
                    Units = new List<string> { KindlingId },
                    ExpectLethal = true,
                },
                new Case
                {
                    Title = "B 够不着（生命 30）",
                    Note = "同一手牌，5 伤打不出 30：预算内必须穷尽且找不到",
                    OppLife = 30,
                    Hand = new List<string> { BloodId },
                    Units = new List<string> { KindlingId },
                    ExpectLethal = false,
                },
                new Case
                {
                    Title = "C 无生物=代价失败",
                    Note = "血偿的代价（消灭己方生物）付不出 → 效果空转、0 伤",
                    OppLife = 5,
                    Hand = new List<string> { BloodId },
                    Units = new List<string>(),
                    ExpectLethal = false,
                },
            };

            foreach (var c in cases) RunCase(c);

            MeasureRebuild(cases[0]);

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "残局求解器阶段1：通过" : $"残局求解器阶段1：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        private static void RunCase(Case c)
        {
            Console.WriteLine();
            Console.WriteLine($"—— {c.Title}");
            Console.WriteLine($"    {c.Note}");

            var ctx = new SearchCtx();
            List<int> path = new List<int>();
            bool found;
            var ctx2 = new SearchCtx();
            bool found2;
            try
            {
                found = Search(c, path, depth: 3, ctx: ctx);
                found2 = Search(c, path, depth: 3, ctx: ctx2); // 确定性自检：同局面搜两次
            }
            finally
            {
                DetachBoard();
                AiStrategyRegistry.Clear();
                TargetSelectionService.AiOrder = null;
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
            }

            string line = ctx.BestPath != null ? DescribeLine(c, ctx.BestPath) : "（无）";
            Console.WriteLine($"    搜索：节点 {ctx.Nodes}／{ctx.MaxNodes}，耗时 {ctx.Watch.ElapsedMilliseconds} ms"
                              + $"（{(ctx.Expired ? "预算耗尽" : "穷尽")}）");
            Console.WriteLine($"    结论：{(found ? "本回合可斩杀" : "本回合不可斩杀")}；线路 {line}");

            Assert(found == c.ExpectLethal, $"结论符合预期（期望 {(c.ExpectLethal ? "可斩杀" : "不可斩杀")}）");
            Assert(found == found2 && SamePath(ctx.BestPath, ctx2.BestPath),
                $"同局面两次搜索结论与路线一致（确定性；节点 {ctx.Nodes} vs {ctx2.Nodes}）");

            // 自证：把找到的线路原样执行一遍，读数必须与「斩杀」一致（同进程、同局面、零假设）
            if (found)
            {
                var verify = BuildAndReplay(c, ctx.BestPath, out bool ok);
                int life = verify.Player2.Life;
                Console.WriteLine($"    回放自证：沿该线路重放 → 对手生命 {c.OppLife} → {life}");
                Assert(ok && life <= 0, "回放后对手生命 ≤ 0（线路可执行且真斩杀）");
            }
            else
            {
                Console.WriteLine("    回放自证：无线路可回放（未找到斩杀）");
            }
        }

        // ======================================== 搜索 ========================================

        /// <summary>DFS：返回是否找到「对手生命 ≤ 0」的动作序列。每次递归=重建局面+重放 path。</summary>
        private static bool Search(Case c, List<int> path, int depth, SearchCtx ctx)
        {
            if (ctx.Expired) return false;

            var core = BuildAndReplay(c, path, out bool ok);
            ctx.Nodes++;
            if (!ok) return false;
            if (core.Player2.Life <= 0)
            {
                ctx.BestPath = new List<int>(path);
                return true;
            }
            if (depth == 0) return false;

            var me = core.Player1;
            var en = new LegalActionEnumerator();
            en.Enumerate(core, me);

            for (int i = 0; i < en.Count; i++)
            {
                var a = en.Actions[i];
                // 结束回合/让过不是「本回合内」的进展：不进入搜索（本阶段只解「打不打得死」，不解「要不要收手」）
                if (a.Type == TideActionType.EndTurn || a.Type == TideActionType.PassPriority) continue;
                if (ctx.Expired) break;

                path.Add(i);
                if (Search(c, path, depth - 1, ctx))
                {
                    path.RemoveAt(path.Count - 1);
                    return true;
                }
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        // ======================================== 局面构造 / 重放 ========================================

        /// <summary>重建同一局面并重放动作下标。动作下标按「同局面同枚举序」稳定（确定性=本方案的前提）。</summary>
        private static GameCore BuildAndReplay(Case c, List<int> path, out bool ok)
        {
            ok = true;
            var core = Build(c);
            var me = core.Player1;
            foreach (int idx in path)
            {
                var en = new LegalActionEnumerator();
                en.Enumerate(core, me);
                if (idx < 0 || idx >= en.Count) { ok = false; return core; }
                if (!LegalActionEnumerator.Apply(core, me, en.Actions[idx])) { ok = false; return core; }
                GameActions.DrainStack(core);
            }
            return core;
        }

        private static GameCore Build(Case c)
        {
            var core = GameCore.Instance;

            var p1Ids = new List<string>(c.Hand);
            p1Ids.AddRange(c.Units);
            p1Ids.AddRange(new[] { LandId, LandId, LandId, FillerId, FillerId });
            var deck1 = p1Ids.Select(CardCatalog.GetById).Where(d => d != null).ToList();
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
                    bank = c.Bank,
                    lands = new List<TutorialLandSpec>
                    {
                        new TutorialLandSpec { card = LandId, tokenType = "Red" },
                        new TutorialLandSpec { card = LandId, tokenType = "Red" },
                        new TutorialLandSpec { card = LandId, tokenType = "Red" },
                    },
                    units = c.Units.Select(id => new TutorialUnitSpec { card = id }).ToList(),
                    hand = new List<string>(c.Hand),
                    deck = new List<string> { FillerId, FillerId },
                },
                ai = new TutorialSideState
                {
                    life = c.OppLife,
                    deck = new List<string> { FillerId },
                },
            };

            TutorialScenarioSeeder.Apply(core, scenario, _board);
            core.StartGame();
            return core;
        }

        // ======================================== 辅助 ========================================

        private static bool SamePath(List<int> a, List<int> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>重建成本实测：这是「没有状态回滚」时的单节点固定开销，决定搜索能开多深。</summary>
        private static void MeasureRebuild(Case c)
        {
            Console.WriteLine();
            Console.WriteLine("—— D 重建成本（决定要不要做状态快照/回滚）");
            const int n = 30;
            Build(c); DetachBoard(); // 预热
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++)
            {
                Build(c);
                DetachBoard();
            }
            sw.Stop();
            double per = sw.Elapsed.TotalMilliseconds / n;
            Console.WriteLine($"    一次「InitGame + 场景注入 + StartGame」：{per:0.00} ms（{n} 次平均）");
            Console.WriteLine($"    → 深度 d 的搜索边数按 b^d 计，单边 ≈ {per:0.0} ms + k×动作耗时；"
                              + "这是阶段1压在预算内的主要瓶颈，也是阶段2要拿掉的成本");
        }


        /// <summary>把动作下标序列渲染成可读文本（重放一遍、逐动作按当时的状态取描述）。</summary>
        private static string DescribeLine(Case c, List<int> path)
        {
            if (path == null || path.Count == 0) return "（空）";
            var core = Build(c);
            var me = core.Player1;
            var parts = new List<string>();
            foreach (int idx in path)
            {
                var en = new LegalActionEnumerator();
                en.Enumerate(core, me);
                if (idx < 0 || idx >= en.Count) { parts.Add($"[{idx}:越界]"); break; }
                var a = en.Actions[idx];
                parts.Add(a.ToString());
                LegalActionEnumerator.Apply(core, me, a);
                GameActions.DrainStack(core);
            }
            return string.Join(" → ", parts) + $"（终局 P2 {core.Player2.Life} 命）";
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
        }

        private static void DetachBoard()
        {
            _board?.Dispose();
            _board = null;
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
            BranchEngines.UnityAdjacentCountOf = null;
            GameBoard.LinkAuraSystem.Detach();
        }
    }
}
