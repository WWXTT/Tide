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
    /// 教学式固定盘面场景回归（2026-11-04 建，2026-11-13 削弱版改版）——红快攻策略的**行为级**验证：
    /// · 血偿：只打生命 &gt;4 的敌方单位；没有就留手（手里没别的牌可出才考虑打脸）。
    /// · 活体炸弹：参照必须是「活得过自己代价」的那只——只剩 1 只己方生物时留手；
    ///   2 只时参照取「不会被对手代价挑走的那只」，主伤 + 首卡奖励落脸。
    /// · 斩杀线：活体炸弹先（首卡 +2）→ 血偿收尾，9 伤正好杀；顺序反了参照会被血偿代价吃掉。
    /// · 拆波：对手有未横置守卫或留费 ≥4，且我方 ≥2 只同攻（≥2）可攻击单位 →
    ///   出牌循环前先打一轮（先攻后铺）；混编（4攻+2攻）不拆。
    ///
    /// 盘面来自 Assets/TutorialData/红快攻策略场景.json——scenario 块与教学配置
    /// （Assets/Configs/TutorialConfig2.json）同构，直接反序列化成教学侧同一 TutorialScenario
    /// 数据类，经 TutorialScenarioSeeder 静默注入（教学直入口径：InitGame(deferStart) →
    /// 棋盘 → Seeder.Apply → StartGame），随后 SimpleAI(RedAggro) 以 TakeTurn 走完整回合。
    /// 断言只认外部证据：牌去了哪个区 / 生命读数 / 事件先后（CardPlayEvent vs AttackDeclarationEvent），
    /// 不读策略内部状态下结论。
    /// 两张法术「只有先结算的那张付代价」的引擎级口径见 TideSim/ChainCastCheck.cs（--chain-cast）。
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

            Console.WriteLine("【教学场景回归】红快攻（2026-11-13 削弱版）：血偿/活体炸弹准入 + 斩杀线排序 + 拆波"
                              + "（固定盘面，注入走 TutorialScenarioSeeder）");
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
                        // 2026-11-04 修订：手里还有别的牌时留着；别的牌打完后就是「无牌可出」→ 打脸，不打 1/1。
                        // 小怪可能被火苗的攻击换掉（正常解场），所以只认「血偿的 5 伤落在脸上」这一条硬证据
                        // 2026-11-13 代价改版：血偿代价=消灭一个己方生物——先下场的火苗是唯一候选，
                        // 被代价消灭（原「火苗已下场」断言随之改为打出轨迹+墓地证据）。
                        Assert(p2.Life == 25, "① 血偿 5 伤落在脸（30→25），没有浪费在 1/1 上");
                        Assert(In(core, p1, Zone.Graveyard, "血偿") != null, "① 血偿没浪费在小怪上（进了墓地=打脸）");
                        Assert(p2.Life == 25, $"① 5 伤打脸（30→25，实际 {p2.Life}）");
                        Assert(_trace.Any(t => t.Kind == "play" && t.Detail == "火苗"), "① 出牌循环照常运转（火苗已打出）");
                        Assert(In(core, p1, Zone.Graveyard, "火苗") != null, "① 火苗作为血偿代价被消灭（唯一候选，进墓地）");
                        break;

                    case "blood-hit-7hp":
                        Assert(In(core, p1, Zone.Graveyard, "血偿") != null, "② 血偿对 7 血目标：出手（已进墓地）");
                        Assert(Life(Unit(core, p2, "教学·8")) == 2, "② 5 伤砸在 7 血单位身上（7→2）");
                        break;

                    case "burst-hold-one-creature":
                        // 2026-11-13：炸弹=参照攻（门奖励已删）。只有 1 只生物时它等于「拿它换 2 点」，
                        // 不能终结 → ShouldHold 扣住（只下火苗，炸弹留手）。
                        Assert(In(core, p1, Zone.Hand, "活体炸弹") != null, "③ 只有 1 只生物：活体炸弹留手（还在手）");
                        Assert(Unit(core, p1, "火苗") != null, "③ 正常出牌循环（火苗已下场）");
                        Assert(p2.Life == 28, $"③ 炸弹没打出去（只掉了火苗的 2 点攻击，30→28，实际 {p2.Life}）");
                        break;

                    case "burst-hit-two-creatures":
                        // 2 只生物但炸弹不能终结（2 < 30）、也没有血偿凑斩杀 → ShouldHold 扣住：
                        // 不为 2 点伤害白送一只生物。本回合只走攻击（火苗 2 + 预组·28 4 = 6）。
                        Assert(In(core, p1, Zone.Hand, "活体炸弹") != null, "④ 2 只生物但炸不终结：留手（还在手）");
                        Assert(Unit(core, p1, "预组·28") != null, "④ 两只生物都没被牺牲");
                        Assert(p2.Life == 24, $"④ 只走攻击（火苗 2 + 预组·28 4 = 6，30→24，实际 {p2.Life}）");
                        break;

                    case "burst-combo-lethal":
                        // 场景：P2 7 命。排序=活体炸弹先（参照攻 2）→ 血偿收尾（5）= 7 斩杀。
                        // 两张的代价各吃一只己方生物（炸弹挑预组·28、血偿吃火苗）。
                        Assert(p2.Life <= 0, $"⑨ 本回合斩杀（P2 生命 {p2.Life}）");
                        Assert(In(core, p1, Zone.Graveyard, "活体炸弹") != null, "⑨ 活体炸弹已结算（进墓地）");
                        Assert(In(core, p1, Zone.Graveyard, "血偿") != null, "⑨ 血偿已结算（进墓地）");
                        {
                            int seqBomb = _trace.Where(t => t.Kind == "play" && t.Detail == "活体炸弹").Select(t => t.Seq).FirstOrDefault();
                            int seqBlood = _trace.Where(t => t.Kind == "play" && t.Detail == "血偿").Select(t => t.Seq).FirstOrDefault();
                            Assert(seqBomb > 0 && seqBlood > 0 && seqBomb < seqBlood, "⑨ 排序：活体炸弹先打、血偿收尾");
                        }
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

                    case "blood-face-no-cards":
                        Assert(In(core, p1, Zone.Hand, "血偿") == null, "⑧ 血偿：无牌可出 → 主阶段打出（不在手）");
                        Assert(p2.Life == 25, $"⑧ 5 伤打脸（30→25，实际 {p2.Life}）");
                        Assert(Life(Unit(core, p2, "教学·3")) == 1, "⑧ 小怪没吃伤害（仍 1 血）");
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



