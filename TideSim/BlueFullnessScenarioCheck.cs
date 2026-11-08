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
    /// 教学式固定盘面场景回归（2026-11-04；2026-11-06 判据七改版）——蓝方碑族**元素充盈**引擎的行为级验证：
    /// · 判据七（已定稿口径，本文件只验证不改）：任一方使用卡牌、**整条连锁结算完成后**（连锁终点补发
    ///   CardCastSettledEvent）双方各自评估己方场上引擎——我方剩余元素（未横置地+钱包）−对方剩余元素，
    ///   封顶 6，**恰好等于** x（Then 锚价推导，两碑 x=2）才触发；每卡每回合只触发 1 次。
    ///   判据六 ≥x、单边「仅对手回合」、横置触发口（OnOpponentTappedLands）均已退役。
    /// · 盘面来自 Assets/TutorialData/蓝充盈场景.json（scenario 块=教学 TutorialScenario 同构，
    ///   TutorialScenarioSeeder 静默注入）。场景①-⑤受测回合=红（P2，SimpleAI(General) 走完整回合、
    ///   蓝挂机）；场景⑥受测回合=蓝自己（firstPlayer=P1）——双方触发面的直接证据。
    /// · 断言只认外部证据：KeywordAppliedEvent(元素充盈) 播报明细（含触发口/判据归属）、以碑为源的
    ///   AtomicDamageEvent、TempCounter 挂层/手牌读数、区域终态；不读引擎内部状态下结论。
    /// 用法：dotnet run --project TideSim -- --tutorial-scenario（红快攻 9 场景 + 蓝充盈 6 场景一起跑）
    /// </summary>
    internal static class BlueFullnessScenarioCheck
    {
        private const string ScenarioFileName = "蓝充盈场景";
        private const int RngSeed = 20261104;

        private static int _fail;
        private static GameBoard.BoardState _board;

        // —— 事件序外部证据（每场景重置）——
        private static readonly List<string> _fullness = new List<string>();          // 元素充盈播报明细（触发口归属看前缀）
        private static readonly List<(string Target, int Dmg)> _monumentDamage =      // 以受测碑为源的原子伤害
            new List<(string, int)>();
        private static readonly List<string> _tempCopies = new List<string>();        // 临时指示物(遗忘)挂层=回响复制
        private static int _redPlays;                                                  // 红（P2）本回合出牌数
        private static int _bluePlays;                                                 // 蓝（P1）本回合出牌数（场景⑥己方自触发）
        private static int _redAttacks;                                                // 红（P2）本回合攻击宣言数
        private static Card _monument;                                                 // 本场景受测碑（伤害事件 Source 比对）

        // —— 诊断通道（定位奖励目标解析行为；结论落定后保留无害）——
        private static readonly List<string> _allDamage = new List<string>();          // 全部原子伤害（任意源）
        private static readonly List<string> _handEnters = new List<string>();         // 入手事件（回响复制/抽牌）

        /// <summary>探针策略（=General 行为，仅把每次无头目标选择的候选池打到控制台——
        /// 断言不读它，只作外部证据留档）。</summary>
        private sealed class ProbeStrategy : AiStrategy
        {
            public override string Key => "probe";
            public override string DisplayName => "探针(General同款)";

            public override List<Entity> OrderTargetCandidates(GameCore core, Player me, TargetSelectionRequest req)
            {
                var list = base.OrderTargetCandidates(core, me, req);
                Console.WriteLine($"    [选择] chooser={req?.Chooser?.Name} atom={req?.PromptAtom} " +
                                  $"min={req?.MinCount}/{req?.MaxCount} 候选=[{string.Join(",", (req?.Candidates ?? new List<Entity>()).Select(NameOf))}]");
                return list;
            }
        }

        // ===== JSON 装载 DTO：envelope 本文件私有，scenario 块 = 教学 TutorialScenario 同一数据类 =====

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

            Console.WriteLine("【教学场景回归】蓝·元素充盈：潮痕碑/深潭碑 触发·不触发·每回合封顶（固定盘面，注入走 TutorialScenarioSeeder）");
            Console.WriteLine("红池推导费探针（GetRuntimeCost——出牌是否横置地牌的依据）：");
            foreach (var id in new[] { "C_138CE290", "C_E105FABA", "C_03E4B181", "C_57BA44EC", "C_7118DF4D" })
            {
                var d = CardCatalog.GetById(id);
                Console.WriteLine($"  {d?.CardName}（{id}）← {CardCostService.GetRuntimeCost(d)}");
            }
            foreach (var entry in file.scenarios)
                RunScenario(entry);

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "蓝充盈场景回归：通过" : $"蓝充盈场景回归：失败 {_fail} 项");
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
            AiStrategyRegistry.Bind(core.Player1, AiStrategy.General); // 蓝：场景①-⑤挂机（红回合），场景⑥自己走回合（双方触发面）
            AiStrategyRegistry.Bind(core.Player2, AiStrategy.General); // 红=①-⑤受测回合的行动方（出牌/横置/攻击）
            ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;

            var p1 = core.Player1;
            var p2 = core.Player2;

            _fullness.Clear();
            _monumentDamage.Clear();
            _tempCopies.Clear();
            _redPlays = 0;
            _bluePlays = 0;
            _redAttacks = 0;
            _monument = null;

            Action<CardCore.KeywordAppliedEvent> onKeyword = e =>
            {
                if (e?.Keyword == "元素充盈") _fullness.Add(e.Detail ?? "");
            };
            Action<AtomicDamageEvent> onDamage = e =>
            {
                if (_monument != null && e?.Source is Card src && ReferenceEquals(src, _monument))
                    _monumentDamage.Add((NameOf(e.Target), e.Damage));
            };
            Action<CardPlayEvent> onPlay = e =>
            {
                if (e?.Player == p2 && !e.IsResponseCast) _redPlays++;
                if (e?.Player == p1 && !e.IsResponseCast) _bluePlays++;
            };
            Action<AttackDeclarationEvent> onAttack = e =>
            {
                if (e?.AttackingPlayer == p2) _redAttacks++;
            };
            Action<CounterChangedEvent> onCounter = e =>
            {
                if (e?.CounterType == CounterRules.TempCounter && e.Amount > 0)
                    _tempCopies.Add(NameOf(e.Target));
            };
            Action<AtomicDamageEvent> onAnyDamage = e =>
            {
                if (e != null) _allDamage.Add($"{NameOf(e.Source)}→{NameOf(e.Target)} −{e.Damage}");
            };
            Action<CardEnterHandEvent> onHandEnter = e =>
            {
                if (e != null) _handEnters.Add($"{e.Player?.Name}:{NameOf(e.Card)}{(e.IsDraw ? "(抽)" : "")}");
            };
            EventManager.Instance.Subscribe(onKeyword);
            EventManager.Instance.Subscribe(onDamage);
            EventManager.Instance.Subscribe(onPlay);
            EventManager.Instance.Subscribe(onAttack);
            EventManager.Instance.Subscribe(onCounter);
            EventManager.Instance.Subscribe(onAnyDamage);
            EventManager.Instance.Subscribe(onHandEnter);
            try
            {
                // 教学直入口径（与红快攻场景回归同序）：Seeder.Apply → StartGame → SimpleAI 走完整回合。
                // 先手按 scenario.firstPlayer 注入（StartGame 无参恒 P1——P2 起跳回合必须显式传 P2，
                // 否则 SimpleAI.TakeTurn 的回合归属守卫会静默空转）
                TutorialScenarioSeeder.Apply(core, entry.scenario, _board);
                core.StartGame(entry.scenario.FirstIsPlayer1 ? null : core.Player2);

                // 盘面读数（外部证据：门槛推导值 + 双方剩余元素差——与引擎判定同式）
                _monument = In(core, p1, Zone.Battlefield, MonumentNameOf(entry.id));
                int x = BranchEngines.MaxManaSurplusThreshold(p1);
                int blueRem = core.ElementPool.GetRemainingElementTotal(p1);
                int redRem = core.ElementPool.GetRemainingElementTotal(p2);
                Console.WriteLine($"    读数：受测碑={NameOf(_monument)}，门槛x={x}（Then锚价推导），"
                                  + $"蓝剩余={blueRem} 红剩余={redRem}，差={Math.Min(blueRem - redRem, 6)}（封顶6）");
                Assert(x == 2, $"门槛x=2（实际{x}）——判据七板面按x=2设计（两碑Then锚价：2伤=2红／回响1=2蓝），锚价漂移须重调板面");
                foreach (var c in core.ZoneManager.GetCards(p2, Zone.Hand) ?? new List<Card>())
                {
                    var d = (c as CardWrapper)?.GetData();
                    Console.WriteLine($"    红手牌：{NameOf(c)} 推导费={CardCostService.GetRuntimeCost(d)?.Total.ToString() ?? "?"}");
                }
                if (_monument != null)
                    foreach (var def in GameActions.GetCardEffectDefinitions(_monument) ?? new List<EffectDefinition>())
                        foreach (var a in def?.Effects ?? new List<AtomicEffectInstance>())
                            if (a?.Branch != null)
                                Console.WriteLine($"    [碑载荷] def={def.Id} 主干={a.Type} engine={a.Branch.EngineKind} " +
                                                  $"then原子数={a.Branch.Then?.Count ?? 0}（门槛x随then合计推导——载荷重复计数会直接抬高x并双发奖励）");

                _allDamage.Clear();
                _handEnters.Clear();
                new SimpleAI(new ProbeStrategy()).TakeTurn(new BattleController()); // P2（红）走一个完整回合

                switch (entry.id)
                {
                    case "tide-trigger-on-play":
                        Assert(_redPlays >= 1, "① 红确实出牌（火苗 1 灰走钱包，全程不横置地）");
                        Assert(_fullness.Count == 1, $"① 充盈触发恰 1 次（实际 {_fullness.Count}）");
                        Assert(_fullness.Count == 1 && _fullness[0].Contains("效果结算后") && _fullness[0].Contains("== 2"),
                            $"① 触发口=效果结算后·判据恰等（明细：{_fullness.FirstOrDefault() ?? "无"}）");
                        Assert(_monumentDamage.Count == 1 && _monumentDamage[0].Dmg == 2
                            && _monumentDamage[0].Target == "火苗",
                            $"① 潮痕碑奖励=2 伤落在火苗（实际 {DescribeDamage()}）");
                        Assert(In(core, p2, Zone.Graveyard, "火苗") != null
                              || !(Unit(core, p2, "火苗")?.IsAlive ?? false),
                            "① 火苗 2/2 吃 2 伤阵亡（进墓/不再存活）");
                        break;

                    case "deep-echo-on-play":
                        Assert(_redPlays >= 1, "② 红确实出牌（火苗）");
                        Assert(_fullness.Count == 1, $"② 充盈触发恰 1 次（实际 {_fullness.Count}）");
                        var hand = core.ZoneManager.GetCards(p1, Zone.Hand) ?? new List<Card>();
                        var temps = hand.Where(c => c.GetCounterCount(CounterRules.TempCounter) > 0).ToList();
                        Assert(temps.Count == 1, $"② 蓝手牌出现 1 张带临时指示物(遗忘)的复制（实际 {temps.Count}）");
                        string copiedName = temps.FirstOrDefault() is IHasName cn ? cn.CardName : "?";
                        Assert(copiedName == "深潭碑" || copiedName == "火苗",
                            $"② 复制源=双方战场一张卡（实际复制了【{copiedName}】）");
                        Assert(_tempCopies.Count == 1, $"② 回响挂层事件恰 1 次（实际 {_tempCopies.Count}）");
                        break;

                    case "no-trigger-high-diff":
                        Assert(_redPlays >= 1, "③ 红确实出牌了（差越过≠没出牌）");
                        Assert(_fullness.Count == 0, $"③ 差 6 > 门槛 2：恰等不中零播报（==x——判据六 ≥x 已退役；实际 {_fullness.Count}）");
                        Assert(_monumentDamage.Count == 0, $"③ 零碑伤（实际 {DescribeDamage()}）");
                        Assert(Unit(core, p2, "火苗") != null && Life(Unit(core, p2, "火苗")) == 2,
                            "③ 火苗满血存活（差越过门槛不触发）");
                        Assert(_tempCopies.Count == 0, "③ 无临时复制入手");
                        break;

                    case "no-trigger-no-play":
                        Assert(_redPlays == 0, $"④ 红本回合未出牌（空手；实际出牌 {_redPlays}）");
                        Assert(_redAttacks >= 1, $"④ 红只做了场上动作（攻击宣言 {_redAttacks} 次）");
                        Assert(p1.Life < 30, $"④ 攻击真实结算（蓝脸被打开：{p1.Life} < 30）");
                        Assert(_fullness.Count == 0, $"④ 零充盈播报——差恰=2 也不评估：非施放动作无评估口（实际 {_fullness.Count}）");
                        Assert(_monumentDamage.Count == 0, $"④ 零碑伤（实际 {DescribeDamage()}）");
                        Assert(Life(Unit(core, p2, "教学·8")) == 7, "④ 教学·8 无伤存活（蓝无单位无反伤）");
                        break;

                    case "once-per-turn":
                        Assert(_redPlays == 2, $"⑤ 红同回合连出两张牌（实际 {_redPlays}）");
                        Assert(_fullness.Count == 1,
                            $"⑤ 恰等只中一次——首张差2==x触发、次张差3≠2不触发（实际 {_fullness.Count}）");
                        Assert(_monumentDamage.Count == 1 && _monumentDamage[0].Dmg == 2,
                            $"⑤ 碑 2 伤只发生一次（实际 {DescribeDamage()}）");
                        int grave = CountIn(core, p2, Zone.Graveyard, "火苗");
                        int field = CountAlive(core, p2, "火苗");
                        Assert(grave == 1, $"⑤ 第一只火苗被充盈 2 伤打死进墓（实际墓 {grave}）");
                        Assert(field == 1, $"⑤ 差滑过恰等档：第二只火苗存活（实际场 {field}）");
                        break;

                    case "own-turn-self-trigger":
                        Assert(_bluePlays >= 1, $"⑥ 蓝确实自己出牌（实际 {_bluePlays}）——己方回合施放是本场景受测动作");
                        Assert(_redPlays == 0, $"⑥ 红全程未施放（实际 {_redPlays}）");
                        Assert(_fullness.Count == 1, $"⑥ 充盈触发恰 1 次——己方回合自己施放也评估（双方触发面；实际 {_fullness.Count}）");
                        Assert(_fullness.Count == 1 && _fullness[0].Contains("== 2"),
                            $"⑥ 判据=恰等于（明细：{_fullness.FirstOrDefault() ?? "无"}）");
                        Assert(_monumentDamage.Count == 1 && _monumentDamage[0].Dmg == 2
                            && _monumentDamage[0].Target == p2.Name,
                            $"⑥ 潮痕碑 2 伤落红角色（对方无单位→角色兜底；实际 {DescribeDamage()}）");
                        Assert(p2.Life <= 28 && p2.Life >= 26,
                            $"⑥ 红终态=28（碑2伤）或 26（再吃火苗攻脸2）——实际 {p2.Life}");
                        break;

                    default:
                        Assert(false, $"未知场景 id：{entry.id}（C# 断言表没有对应分支）");
                        break;
                }

                Console.WriteLine($"    轨迹：红出牌 {_redPlays} 次 / 攻击宣言 {_redAttacks} 次 / "
                                  + $"充盈播报 {_fullness.Count} 条 / 碑伤 {_monumentDamage.Count} 次 / 临时复制 {_tempCopies.Count} 张 / "
                                  + $"终态生命 蓝={p1.Life} 红={p2.Life}");
                foreach (var d in _fullness) Console.WriteLine($"    [充盈] {d}");
                foreach (var d in _allDamage) Console.WriteLine($"    [伤害] {d}");
                foreach (var h in _handEnters) Console.WriteLine($"    [入手] {h}");
            }
            finally
            {
                EventManager.Instance.Unsubscribe(onKeyword);
                EventManager.Instance.Unsubscribe(onDamage);
                EventManager.Instance.Unsubscribe(onPlay);
                EventManager.Instance.Unsubscribe(onAttack);
                EventManager.Instance.Unsubscribe(onCounter);
                EventManager.Instance.Unsubscribe(onAnyDamage);
                EventManager.Instance.Unsubscribe(onHandEnter);
                AiStrategyRegistry.Clear();
                ResponseWindowService.AiResponder = null;
                TargetSelectionService.AiOrder = null;
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
                DetachBoard();
            }
        }

        // ======================================== 断言辅助 ========================================

        /// <summary>场景受测碑名（①③④⑤⑥=潮痕碑；②=深潭碑）。</summary>
        private static string MonumentNameOf(string id)
            => id == "deep-echo-on-play" ? "深潭碑" : "潮痕碑";

        private static string DescribeDamage()
            => _monumentDamage.Count == 0 ? "无"
             : string.Join("；", _monumentDamage.Select(d => $"{d.Target} −{d.Dmg}"));

        private static Card In(GameCore core, Player p, Zone zone, string name)
            => (core.ZoneManager.GetCards(p, zone) ?? new List<Card>())
                .FirstOrDefault(c => c is IHasName n && n.CardName == name);

        private static Card Unit(GameCore core, Player p, string name)
            => In(core, p, Zone.Battlefield, name);

        private static int Life(Card c) => c?.GetLife() ?? -1;

        private static int CountIn(GameCore core, Player p, Zone zone, string name)
            => (core.ZoneManager.GetCards(p, zone) ?? new List<Card>())
                .Count(c => c is IHasName n && n.CardName == name);

        private static int CountAlive(GameCore core, Player p, string name)
            => (core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                .Count(c => c.IsAlive && c is IHasName n && n.CardName == name);

        private static string NameOf(Entity e)
        {
            if (e is CardWrapper w) return w.GetData()?.CardName ?? "?";
            if (e is Player p) return p.Name;
            return e?.GetType().Name ?? "null";
        }

        // ======================================== 装载/棋盘（与红快攻场景回归同口径） ========================================

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
