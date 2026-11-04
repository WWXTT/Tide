using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using Newtonsoft.Json;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 教学式固定盘面场景回归（2026-11-04）——红快攻三条新策略的**行为级**验证：
    /// · 血偿：只打生命 &gt;4 的敌方单位；没有就留手（手里没别的牌可出才考虑打脸）。
    /// · 连击：只贴「能攻击且攻击力 ≥4」的己方单位；1/1、火灵·守（不可攻击）一律不贴。
    /// · 拆波：对手有未横置守卫或留费 ≥4，且我方 ≥2 只同攻（≥2）可攻击单位 →
    ///   出牌循环前先打一轮（先攻后铺）；混编（4攻+2攻）不拆。
    ///
    /// 盘面来自 Assets/TutorialData/红快攻策略场景.json——scenario 块与教学配置
    /// （Assets/Configs/TutorialConfig2.json）同构，直接反序列化成教学侧同一 TutorialScenario
    /// 数据类，经 TutorialScenarioSeeder 静默注入（教学直入口径：InitGame(deferStart) →
    /// 棋盘 → Seeder.Apply → StartGame），随后 SimpleAI(RedAggro) 以 TakeTurn 走完整回合。
    /// 断言只认外部证据：牌去了哪个区 / 事件先后（CardPlayEvent vs AttackDeclarationEvent），
    /// 不读策略内部状态下结论。
    /// 用法：dotnet run --project TideSim -- --tutorial-scenario
    /// </summary>
    internal static class TutorialScenarioCheck
    {
        private const string ScenarioFileName = "红快攻策略场景";
        private const int RngSeed = 20261104;

        private static int _fail;
        private static bool _earlyCombatProbe; // 回合开始时的 WantEarlyCombat 读数（TakeTurn 步骤 1.5 同款盘面）
        private static readonly List<TraceLine> _trace = new List<TraceLine>();
        private static int _seq;
        private static GameBoard.BoardState _board;

        private sealed class TraceLine
        {
            public string Kind;   // play / attack
            public int Seq;       // 全局先后序号
            public string Detail; // 卡名
        }

        // ===== JSON 装载 DTO：envelope 是本文件私有的，scenario 块 = 教学 TutorialScenario 同一数据类 =====

        private sealed class ScenarioEntry
        {
            public string id;
            public string name;
            public string note;
            public TutorialScenario scenario;
        }

        private sealed class ScenarioFile
        {
            public List<ScenarioEntry> scenarios;
        }

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            CardCatalog.LoadAll();
            string path = CardDataPaths.TutorialFileIn(ScenarioFileName + ".json");
            if (!File.Exists(path))
            {
                Console.WriteLine($"✗ 场景文件不存在：{path}");
                return 1;
            }
            ScenarioFile file;
            try
            {
                file = JsonConvert.DeserializeObject<ScenarioFile>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Console.WriteLine($"✗ 场景文件解析失败：{e.Message}");
                return 1;
            }
            if (file?.scenarios == null || file.scenarios.Count == 0)
            {
                Console.WriteLine("✗ 场景文件没有可用条目（scenarios 为空）");
                return 1;
            }

            Console.WriteLine("【教学场景回归】红快攻：血偿/连击准入 + 拆波（固定盘面，注入走 TutorialScenarioSeeder）");
            foreach (var entry in file.scenarios)
                RunScenario(entry);

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "教学场景回归：通过" : $"教学场景回归：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        // ======================================== 单场景 ========================================

        private static void RunScenario(ScenarioEntry entry)
        {
            Console.WriteLine();
            Console.WriteLine($"—— {entry.id}：{entry.name}");
            if (!string.IsNullOrEmpty(entry.note)) Console.WriteLine($"    盘面：{entry.note}");

            var deck1 = BuildDeck(TutorialScenario.DeckIdsOf(entry.scenario?.player), $"{entry.id}.player");
            var deck2 = BuildDeck(TutorialScenario.DeckIdsOf(entry.scenario?.ai), $"{entry.id}.ai");
            if (deck1 == null || deck2 == null || entry.scenario == null) return;

            var core = GameCore.Instance;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;
            AttachBoard(core);
            AiStrategyRegistry.Bind(core.Player1, AiStrategy.RedAggro); // 受测方：红快攻
            AiStrategyRegistry.Bind(core.Player2, AiStrategy.General);  // 对手：通用策略
            ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;

            _trace.Clear();
            _seq = 0;
            Action<CardPlayEvent> onPlay = e =>
            {
                if (e?.Player == core.Player1 && !e.IsResponseCast)
                    _trace.Add(new TraceLine { Kind = "play", Seq = ++_seq, Detail = NameOf(e.PlayedCard) });
            };
            Action<AttackDeclarationEvent> onAttack = e =>
            {
                if (e?.AttackingPlayer == core.Player1)
                    _trace.Add(new TraceLine { Kind = "attack", Seq = ++_seq, Detail = NameOf(e.Attacker) });
            };
            EventManager.Instance.Subscribe(onPlay);
            EventManager.Instance.Subscribe(onAttack);
            try
            {
                // 教学直入口径（BattleController.StartNewGame(scenario≠null) 同序）：
                // 静默注入 → StartGame（TurnEngine 已预置起跳回合 → 落到 startTurn/Main，跳过准备阶段自动化）
                TutorialScenarioSeeder.Apply(core, entry.scenario, _board);
                core.StartGame();

                var p1 = core.Player1;
                var p2 = core.Player2;
                _earlyCombatProbe = AiStrategy.RedAggro.WantEarlyCombat(core, p1);
                new SimpleAI(AiStrategy.RedAggro).TakeTurn(new BattleController()); // P1 走一个完整回合

                switch (entry.id)
                {
                    case "blood-hold-1hp":
                        Assert(In(core, p1, Zone.Hand, "血偿") != null, "① 血偿对 1/1：留手（回合结束仍在手牌）");
                        Assert(In(core, p1, Zone.Graveyard, "血偿") == null, "① 血偿没有被当作打脸/烧小怪的出口（不在墓地）");
                        Assert(Unit(core, p1, "火苗") != null, "① 出牌循环照常运转（手里别的牌——火苗——已下场）");
                        break;

                    case "blood-hit-7hp":
                        Assert(In(core, p1, Zone.Graveyard, "血偿") != null, "② 血偿对 7 血目标：出手（已进墓地）");
                        Assert(Life(Unit(core, p2, "教学·8")) == 2, "② 5 伤砸在 7 血单位身上（7→2）");
                        break;

                    case "combo-hold-no-carrier":
                        Assert(In(core, p1, Zone.Hand, "追击") != null, "③ 连击没有 ≥4 攻载体：留手");
                        var atk = Unit(core, p1, "火灵·攻");
                        var def = Unit(core, p1, "火灵·守");
                        Assert(atk != null && !atk.HasKeyword(KeywordRules.DoubleStrike)
                            && (def == null || !def.HasKeyword(KeywordRules.DoubleStrike)),
                            "③ 1/1 火灵·攻与不可攻击的火灵·守都没吃到连击");
                        Assert(Unit(core, p1, "火苗") != null, "③ 出牌循环照常运转（火苗已下场）");
                        break;

                    case "combo-hit-4atk":
                        Assert(In(core, p1, Zone.Graveyard, "追击") != null, "④ 连击对 4 攻载体：出手（已进墓地）");
                        var big = Unit(core, p1, "预组·28");
                        Assert(big != null && big.HasKeyword(KeywordRules.DoubleStrike), "④ 预组·28（4/5）获得连击");
                        break;

                    case "wave-split-same-atk":
                        Assert(_earlyCombatProbe, "⑤ 策略闸：同攻 2/2×2 + 对手留 4 费 → WantEarlyCombat=true");
                        Assert(FirstSeq("attack") > 0 && FirstSeq("play") > 0, "⑤ 本回合攻击与出牌都发生过");
                        Assert(FirstSeq("attack") < FirstSeq("play"), "⑤ 拆波序：首个攻击宣言早于首次出牌（先攻后铺）");
                        Assert(CountBefore("attack", FirstSeq("play")) >= 2, "⑤ 第一波两只 2/2 都在铺场前完成攻击宣言");
                        break;

                    case "wave-mixed-no-split":
                        Assert(!_earlyCombatProbe, "⑥ 策略闸：混编 4攻+2攻 → WantEarlyCombat=false（不拆）");
                        Assert(FirstSeq("attack") > 0 && FirstSeq("play") > 0, "⑥ 本回合攻击与出牌都发生过");
                        Assert(FirstSeq("play") < FirstSeq("attack"), "⑥ 常规序：首次出牌早于首个攻击宣言（先铺后攻）");
                        break;

                    case "wave-split-guard":
                        Assert(_earlyCombatProbe, "⑦ 策略闸（守卫臂）：对手未横置守卫 → WantEarlyCombat=true");
                        Assert(FirstSeq("attack") > 0 && FirstSeq("play") > 0, "⑦ 本回合攻击与出牌都发生过");
                        Assert(FirstSeq("attack") < FirstSeq("play"), "⑦ 拆波序：首个攻击宣言早于首次出牌（先攻后铺）");
                        break;

                    default:
                        Assert(false, $"未知场景 id：{entry.id}（C# 断言表没有对应分支）");
                        break;
                }

                Console.WriteLine("    轨迹：" + string.Join(" → ", _trace.Select(t => $"{t.Kind}:{t.Detail}")));
            }
            finally
            {
                EventManager.Instance.Unsubscribe(onPlay);
                EventManager.Instance.Unsubscribe(onAttack);
                AiStrategyRegistry.Clear();
                ResponseWindowService.AiResponder = null;
                TargetSelectionService.AiOrder = null;
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
                DetachBoard();
            }
        }

        // ======================================== 断言辅助 ========================================

        private static int FirstSeq(string kind)
        {
            var first = _trace.FirstOrDefault(t => t.Kind == kind);
            return first?.Seq ?? 0;
        }

        private static int CountBefore(string kind, int seq)
            => _trace.Count(t => t.Kind == kind && t.Seq < seq);

        private static Card In(GameCore core, Player p, Zone zone, string name)
            => (core.ZoneManager.GetCards(p, zone) ?? new List<Card>())
                .FirstOrDefault(c => c is IHasName n && n.CardName == name);

        private static Card Unit(GameCore core, Player p, string name)
            => In(core, p, Zone.Battlefield, name);

        private static int Life(Card c) => c?.GetLife() ?? -1;

        private static string NameOf(Entity e)
        {
            if (e is CardWrapper w) return w.GetData()?.CardName ?? "?";
            if (e is Player p) return p.Name;
            return e?.GetType().Name ?? "null";
        }

        // ======================================== 装载/棋盘（与 AiDuelRunner 同口径） ========================================

        /// <summary>scenario 一侧的全卡并集 → CardData 卡组（缺 id 即失败——固定盘面必须完整）。</summary>
        private static List<CardData> BuildDeck(List<string> ids, string label)
        {
            var deck = new List<CardData>();
            foreach (var id in ids ?? new List<string>())
            {
                var d = CardCatalog.GetById(id);
                if (d == null)
                {
                    Console.WriteLine($"    ✗ 夹具：卡 id 不在卡池：{id}（{label}）——场景作废");
                    _fail++;
                    return null;
                }
                deck.Add(d);
            }
            if (deck.Count == 0)
            {
                Console.WriteLine($"    ✗ 夹具：{label} 卡组为空——场景作废");
                _fail++;
                return null;
            }
            return deck;
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
