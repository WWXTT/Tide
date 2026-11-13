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
    /// 红套改版（2026-11-13 追击除组）后「两张直伤法术的连锁」引擎级取证：
    ///
    /// 血偿与活体炸弹的并列代价都改成**消灭一个己方生物**（TutorialEffects.json：cost.type=1
    /// Payload，payload refId=a50b8c19 kinds=[1]）。按 2026-11-13 四条口径断言：
    /// ① 发动前闸：每张卡声明期各自看得到代价候选 → 只有 1 只生物时两张也能一起上栈连锁；
    /// ② 结算期先付代价、**代价失败=该张效果无效**（PayPayloadsAsync 返回 false → 效果空转）；
    /// ③ **参照在发动时取**（代价执行前快照攻/命）——参照被自己的代价杀掉/移走也不影响本次伤害；
    /// ④ 代价与门奖励不能并存（活体炸弹的「首卡 +2」门奖励已删，只按参照攻打）。
    /// 只认外部事实：生命/区域/生物存活/代价行/伤害事件。
    /// 用法：dotnet run --project TideSim -- --chain-cast
    /// </summary>
    internal static class ChainCastCheck
    {
        private const int RngSeed = 20261113;

        // 卡 id（红·快攻预组 / 教学卡表）
        private const string BloodId = "C_E105FABA";   // 血偿：红3 净费（锚红6−代价红3），对敌方单位 5 伤
        private const string BombId = "C_5705F9FE";    // 活体炸弹：锚红3−代价红3 = 净 0；参照己方生物当前攻
        private const string KindlingId = "C_138CE290";// 火苗 2/2 0 费（参照/牺牲位）
        private const string BigId = "C_FFF2FD83";     // 预组·28 4/5 0 费（更高价值 → 对手代价优先杀它）
        private const string LandId = "C_11B82155";    // 教学·35 地牌载体（tokenType=Red）
        private const string FillerId = "C_DA499FAE";  // 教学·3 1/1（牌库填充 / 对手场面）

        private static int _fail;
        private static GameBoard.BoardState _board;

        /// <summary>一步声明：卡 id + 是否走响应窗（true=PlayCardInResponse）＋声明后是否立即排干栈。</summary>
        private sealed class Step
        {
            public string Card;
            public bool Response;
            public bool DrainAfter;
            public static Step Main(string id, bool drainAfter = true) => new Step { Card = id, DrainAfter = drainAfter };
            public static Step Resp(string id, bool drainAfter = false) => new Step { Card = id, Response = true, DrainAfter = drainAfter };
        }

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            CardCatalog.LoadAll();
            Console.WriteLine("【连锁直伤探针】血偿 + 活体炸弹：代价（消灭一个己方生物）在结算期执行"
                              + "；参照在发动时取（快照）、代价失败=效果无效（2026-11-13 定案）");

            // ---- 单生物：连锁两序（两张法术各需一只牺牲位；只有 1 只 = 第二张代价失败→效果无效）----
            RunBoard("对照·只打活体炸弹（单发）", 30, new[] { KindlingId }, new List<Step> { Step.Main(BombId) },
                expDealt: 2, expCostLines: 1,
                note: "唯一生物=参照；发动时快照攻 2 → 代价杀它 → 主伤 2 照落（参照丢失不影响结算）");
            RunBoard("A 先血偿 → 再活体炸弹（炸弹后声明=先结算）", 30, new[] { KindlingId },
                new List<Step> { Step.Main(BloodId, drainAfter: false), Step.Resp(BombId) },
                expDealt: 2, expCostLines: 1,
                note: "炸弹先结算：代价杀掉唯一生物 → 炸弹按快照打 2；血偿代价候选=0 → 代价失败、效果无效（0 伤）");
            RunBoard("B 先活体炸弹 → 再血偿（血偿后声明=先结算）", 30, new[] { KindlingId },
                new List<Step> { Step.Main(BombId, drainAfter: false), Step.Resp(BloodId) },
                expDealt: 5, expCostLines: 1,
                note: "血偿先结算：代价杀掉唯一生物 → 5 伤；炸弹代价失败 → 效果无效（0 伤）");

            // ---- 双生物：参照不再是唯一牺牲位 ----
            RunBoard("C 双生物·只打活体炸弹", 30, new[] { KindlingId, BigId },
                new List<Step> { Step.Main(BombId) },
                expDealt: 2, expCostLines: 1,
                note: "对手代价杀最高价值（预组·28 4/5）→ 参照火苗活到结算：主伤 2（门奖励已删，无 +2）");
            RunBoard("D 双生物·先活体炸弹 → 再血偿（顺序照常结算）", 30, new[] { KindlingId, BigId },
                new List<Step> { Step.Main(BombId), Step.Main(BloodId) },
                expDealt: 7, expCostLines: 2,
                note: "炸弹 2（杀预组·28）→ 血偿 5（杀火苗）；两张代价各付一次");
            RunBoard("E 双生物·连锁（血偿先声明、炸弹响应）", 30, new[] { KindlingId, BigId },
                new List<Step> { Step.Main(BloodId, drainAfter: false), Step.Resp(BombId) },
                expDealt: 7, expCostLines: 2,
                note: "炸弹先结算：代价杀预组·28、参照火苗活 → 2；血偿代价杀火苗 → 5");
            RunBoard("F 双生物·先血偿 → 再活体炸弹（顺序照常结算）", 30, new[] { KindlingId, BigId },
                new List<Step> { Step.Main(BloodId), Step.Main(BombId) },
                expDealt: 7, expCostLines: 2,
                note: "血偿先杀预组·28 → 5；炸弹代价杀火苗，参照丢失但按发动快照打 2（规则③）");
            RunBoard("G 单生物·AI 口径（不传 References，只给声明期参照目标）", 30, new[] { KindlingId },
                new List<Step> { Step.Main(BombId) },
                expDealt: 2, expCostLines: 1,
                note: "AI 路径没有显式 References：参照仍要在发动时取到并快照，代价杀它后照打 2",
                aiStyleRefs: true);

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "连锁直伤探针：通过" : $"连锁直伤探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>固定盘面：P1 地 3 红 + units + hand；P2 oppLife 命、空场。
        /// aiStyleRefs=true 时按 SimpleAI 的真实口径声明炸弹：只给声明期目标（参照）、不传 References，
        /// 用来验证「AI 路径没有显式 References 时，参照仍在发动时被取到并快照」。</summary>
        private static void RunBoard(string title, int oppLife, string[] units, List<Step> steps,
            int expDealt, int expCostLines, string note, bool aiStyleRefs = false)
        {
            Console.WriteLine();
            Console.WriteLine($"—— {title}");
            Console.WriteLine($"    {note}");
            var core = GameCore.Instance;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;

            var p1Ids = new List<string>(steps.Select(s => s.Card));
            p1Ids.AddRange(units);
            p1Ids.AddRange(new[] { LandId, LandId, LandId, FillerId, FillerId });
            var p2Ids = new List<string> { FillerId, FillerId };
            var deck1 = p1Ids.Select(CardCatalog.GetById).Where(d => d != null).ToList();
            var deck2 = p2Ids.Select(CardCatalog.GetById).Where(d => d != null).ToList();

            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;
            AttachBoard(core);
            // 受测方=红快攻（炸弹受害者/血偿目标排序都走策略口径），对手=通用策略
            //（代价选位走「对手视角最大化损失」——真实对局口径）
            AiStrategyRegistry.Bind(core.Player1, AiStrategy.RedAggro);
            AiStrategyRegistry.Bind(core.Player2, AiStrategy.General);
            TargetSelectionService.AiOrder = req => AiStrategy.DispatchOrder(
                ReferenceEquals(req?.Chooser, core.Player1) ? AiStrategy.RedAggro : AiStrategy.General,
                core, req?.Chooser, req);
            ResponseWindowService.AiResponder = null; // AI 回合驱动不经本探针

            var costLines = new List<string>();
            Action<CostPaidEvent> onCost = e => { if (e != null) costLines.Add(e.Description); };
            EventManager.Instance.Subscribe(onCost);
            var dmgLines = new List<string>();
            Action<AtomicDamageEvent> onDmg = e =>
            {
                if (e != null) dmgLines.Add($"{NameOfCard(e.Source as Card)}({e.Damage})→{NameOfCard(e.Target as Card) ?? (e.Target as Player)?.Name}");
            };
            EventManager.Instance.Subscribe(onDmg);

            var scenario = new TutorialScenario
            {
                startTurn = 10,
                firstPlayer = "P1",
                player = new TutorialSideState
                {
                    life = 30,
                    bank = new[] { 0, 3, 0, 0, 0, 0 }, // 3 红：血偿 3 + 活体炸弹净 0 够付
                    lands = new List<TutorialLandSpec>
                    {
                        new TutorialLandSpec { card = LandId, tokenType = "Red" },
                        new TutorialLandSpec { card = LandId, tokenType = "Red" },
                        new TutorialLandSpec { card = LandId, tokenType = "Red" },
                    },
                    units = units.Select(id => new TutorialUnitSpec { card = id }).ToList(),
                    hand = steps.Select(s => s.Card).ToList(),
                    deck = new List<string> { FillerId, FillerId },
                },
                ai = new TutorialSideState
                {
                    life = oppLife,
                    deck = new List<string> { FillerId }, // 对手空场：炸弹/血偿的伤害只能落脸，读数干净
                },
            };

            try
            {
                TutorialScenarioSeeder.Apply(core, scenario, _board);
                core.StartGame();

                var p1 = core.Player1;
                var p2 = core.Player2;
                int lifeBefore = p2.Life;

                Console.WriteLine("    出手前：P1 场[" + string.Join("、", FieldNames(core, p1)) + "]／P2 "
                    + $"{lifeBefore} 命");

                foreach (var step in steps)
                {
                    var card = HandCard(core, p1, step.Card);
                    var refs = step.Card == BombId && !aiStyleRefs ? BombReference(core, p1) : null;
                    var targets = new List<Entity> { p2 }; // 两张牌的主目标都取脸（让伤害读数干净）
                    if (step.Card == BombId && aiStyleRefs)
                    {
                        // SimpleAI 真实口径：参照走声明期目标（不进 References），由引擎在发动时取
                        var r = BombReference(core, p1);
                        if (r != null) targets = r;
                    }
                    bool ok;
                    string reason = "";
                    if (step.Response)
                    {
                        core.StackEngine.PassPriority(p2).GetAwaiter().GetResult(); // 对手让过 → 优先权回到自己
                        ok = GameActions.PlayCardInResponse(core, p1, card, targets, 0, refs, out reason);
                    }
                    else
                    {
                        ok = GameActions.PlayCard(core, p1, card, targets, Zone.Hand, 0, refs, out reason);
                    }
                    Assert(ok, $"声明 {NameOf(step.Card)}（{(step.Response ? "响应窗" : "主阶段")}）成功"
                                + (ok ? "" : $"——被拒：{reason}"));
                    if (!ok) return;
                    if (step.DrainAfter) GameActions.DrainStack(core);
                }
                GameActions.DrainStack(core);

                int dealt = lifeBefore - p2.Life;
                Console.WriteLine($"    结果：P2 {lifeBefore} → {p2.Life}（共 {dealt} 伤）；P1 剩余场["
                    + string.Join("、", FieldNames(core, p1)) + $"]；代价行 {costLines.Count} 条〔"
                    + string.Join("｜", costLines) + "〕");
                if (dmgLines.Count > 0)
                    Console.WriteLine($"    伤害事件：{string.Join("｜", dmgLines)}");

                Assert(dealt == expDealt, $"伤害={expDealt}（实际 {dealt}）");
                Assert(costLines.Count == expCostLines, $"代价行={expCostLines} 条（实际 {costLines.Count} 条）");
            }
            finally
            {
                EventManager.Instance.Unsubscribe(onCost);
                EventManager.Instance.Unsubscribe(onDmg);
                AiStrategyRegistry.Clear();
                TargetSelectionService.AiOrder = null;
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
                DetachBoard();
            }
        }

        // ======================================== 辅助 ========================================

        /// <summary>炸弹参照：按策略口径取「攻击力最高、且不是对手最想杀的那只」（≥2 生物时才有可活参照）——
        /// 对手代价选位=最高单位价值 (攻+血)/2（AiStrategy.OrderOpponentChosenCostTargets），
        /// 参照若正好是那只，结算期已被代价杀死 → 炸弹恒空转。</summary>
        private static List<Entity> BombReference(GameCore core, Player p1)
        {
            var units = (core.ZoneManager.GetCards(p1, Zone.Battlefield) ?? new List<Card>())
                .Where(c => c != null && c.IsAlive && c.IsLivingUnit()).ToList();
            if (units.Count < 2) return null; // 唯一生物=自己的牺牲位，炸弹无活参照
            var doomed = units.OrderByDescending(c => core.LayerEngine.CalculatePower(c) + c.GetLife())
                .First(); // 对手最想杀的（单位价值最高）
            var refCard = units.Where(c => !ReferenceEquals(c, doomed))
                .OrderByDescending(c => core.LayerEngine.CalculatePower(c))
                .FirstOrDefault();
            return refCard != null ? new List<Entity> { refCard } : null;
        }

        private static IEnumerable<string> FieldNames(GameCore core, Player p)
            => (core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                .Where(c => c != null && c.IsAlive)
                .Select(c => $"{NameOfCard(c)} {core.LayerEngine.CalculatePower(c)}/"
                             + $"{c.GetLife()}");

        private static string NameOfCard(Card c) => c is IHasName n ? n.CardName : c?.ID ?? "?";

        private static string NameOf(string id) => id == BloodId ? "血偿" : id == BombId ? "活体炸弹" : id;

        private static Card HandCard(GameCore core, Player p, string id)
            => (core.ZoneManager.GetCards(p, Zone.Hand) ?? new List<Card>()).FirstOrDefault(c => c.ID == id
                || (c.ID != null && c.ID.StartsWith(id + "#", StringComparison.Ordinal)));

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
