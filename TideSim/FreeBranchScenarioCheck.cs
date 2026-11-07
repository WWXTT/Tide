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
    /// 教学式固定盘面场景回归（2026-11-06）——自由分支·引擎主干 7 种（元素充盈已由蓝充盈场景覆盖）的
    /// 行为级验证：倒计时（准备阶段 tick·归零直发·重挂）/ 运势（主动发动掷 2d6 双≥x）/ 拼枪（攻击宣言·
    /// 牌库随机生物比攻·差≥门槛）/ 死亡计数（双方生物死亡累计·满值6）/ 团结（施放结算后邻接占用≥x）/
    /// 附加诅咒·附加祝福（施放部署→抽到发作→消层）。
    /// · 盘面来自 Assets/TutorialData/自由分支场景.json（scenario 块=教学 TutorialScenario 同构，
    ///   TutorialScenarioSeeder 静默注入）。受测行动方恒红（P2，SimpleAI(General) 走完整回合）；蓝（P1）
    ///   惰性侧——奖励伤害按「对方无单位→角色兜底」落蓝角色（蓝充盈场景⑥同款已验口径）。
    /// · 多回合场景（倒计时等待/诅咒/祝福）连续 TakeTurn 驱动（P2→P1→P2 轮替——BattleController.
    ///   TurnPlayer 动态读当前回合方）。
    /// · 断言只认外部证据：KeywordAppliedEvent（引擎中文名播报，含全局回合号）、LuckRolledEvent/
    ///   ClashResolvedEvent（成败两态必发）、以引擎卡为源的非战斗 AtomicDamageEvent、HealEvent、
    ///   Curse/Blessing/Countdown/DeathToll 指示物事件与终态读数；不读引擎内部状态下结论。
    /// 用法：dotnet run --project TideSim -- --tutorial-scenario（红快攻 + 蓝充盈 + 自由分支三套一起跑）
    /// </summary>
    internal static class FreeBranchScenarioCheck
    {
        private const string ScenarioFileName = "自由分支场景";
        private const int RngSeed = 20261104;

        private static int _fail;
        private static GameBoard.BoardState _board;

        // —— 事件序外部证据（每场景重置）——
        private static readonly List<(int turn, string keyword, string detail)> _keywords = new();
        private static readonly List<(string target, int dmg)> _engineDamage =          // 以受测引擎卡为源的非战斗伤害
            new();
        private static readonly List<(int d1, int d2, int x, bool ok)> _luckRolls = new();
        private static readonly List<(int my, int their, int x, bool ok)> _clashes = new();
        private static readonly List<(string target, string type, int amount)> _counters = new();
        private static readonly List<(string target, int amount)> _heals = new();
        private static int _redPlays;                                                    // 红（P2）主阶段出牌数
        private static int _redResponseCasts;                                            // 红（P2）响应窗出牌数（⑱响应链=第二栈对象）
        private static int _redAttacks;                                                  // 红（P2）攻击宣言数
        private static int _blueAttacks;                                                 // 蓝（P1）攻击宣言数（响应链场景=宣攻锚点）
        private static readonly List<string> _blueDraws = new();                         // 蓝（P1）抽牌（诅咒发作时点证据）
        private static readonly List<string> _redDraws = new();                          // 红（P2）抽牌（死亡计数抽牌奖励逐次读数）
        private static Card _engine;                                                     // 本场景主受测引擎卡（计数/邻接读数用）
        private static readonly List<Card> _engines = new();                             // 全部受测引擎卡（伤害事件 Source 比对；响应链场景双引擎）
        private static int _seq;                                                         // 全局事件序号（时点断言用）
        private static readonly List<(int seq, string tag)> _order = new();              // 事件序日志：播报/伤害/宣攻的相对次序

        // —— 诊断通道（定位行为；结论落定后保留无害）——
        private static readonly List<string> _allDamage = new();

        /// <summary>探针策略（=General 行为，仅打印无头目标选择候选池——断言不读，只作外部证据留档）。</summary>
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

            Console.WriteLine("【教学场景回归】自由分支·引擎主干 7 种：倒计时/运势/拼枪/死亡计数/团结/附加诅咒/附加祝福（固定盘面，注入走 TutorialScenarioSeeder）");
            Console.WriteLine("受测引擎卡池推导费（GetRuntimeCost——施放/发动的支付依据）：");
            foreach (var id in new[] { "C_FFDF953C", "C_2E00FB6A", "C_CD77F233", "C_E84B9F8A", "C_BD0822E9", "C_CC0DE001", "C_CC0DE002", "C_CC0DE003", "C_E2122348", "C_A9ED8C5A" })
            {
                var d = CardCatalog.GetById(id);
                Console.WriteLine($"  {d?.CardName}（{id}）← {CardCostService.GetRuntimeCost(d)}");
            }
            foreach (var entry in file.scenarios)
                RunScenario(entry);

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "自由分支场景回归：通过" : $"自由分支场景回归：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        // ======================================== 单场景 ========================================

        /// <summary>场景 id → 受测回合数（多回合场景=红→蓝→红连续 TakeTurn；起跳回合 PrepareScenarioStart
        /// 直落 Main 跳过准备阶段自动化——倒计时 tick/抽牌都从**第二个**同侧回合开始生效）。</summary>
        private static int TurnCountOf(string id)
            => id == "countdown-wait" ? 5
             : id == "countdown-fire" || id == "blessing-fire" || id == "countdown-sleep" ? (id == "countdown-sleep" ? 5 : 3)
             : id == "curse-fire" ? 2
             : id == "deathtoll-repeat" ? 11
             : 1;

        /// <summary>场景 id → 受测引擎卡名列表（诅咒/祝福宿主是法术：开局在手，施放后入墓；
        /// ⑨⑩⑰=2026-11-06 载荷效果化新形态卡 教学·50/51——原子 str=组合效果引用主通道；
        /// ⑱=响应链双引擎：潮痕碑(充盈·连锁终点拍) + 教学·37(团结·施放直调拍)）。</summary>
        private static string[] EngineNamesOf(string id)
        {
            switch (id)
            {
                case "countdown-fire":
                case "countdown-wait":
                case "countdown-sleep": return new[] { "教学·20" };
                case "luck-fire": return new[] { "教学·21" };
                case "luck-miss": return new[] { "教学·26" };
                case "clash-fire":
                case "clash-miss":
                case "clash-twice": return new[] { "教学·10" };
                case "deathtoll-fire": return new[] { "教学·27" };
                case "deathtoll-repeat": return new[] { "教学·52" };
                case "unity-fire":
                case "unity-miss":
                case "unity-mixed-cells": return new[] { "教学·37" };
                case "fullness-cap6": return new[] { "教学·33" };
                case "curse-fire":
                case "curse-empty-deck": return new[] { "教学·50" };
                case "blessing-fire": return new[] { "教学·51" };
                case "fullness-response-chain": return new[] { "潮痕碑", "教学·37" };
                default: return new string[0];
            }
        }

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
            AiStrategyRegistry.Bind(core.Player1, AiStrategy.General); // 蓝：惰性侧（空场空手零银行）
            AiStrategyRegistry.Bind(core.Player2, AiStrategy.General); // 红=受测行动方
            ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;

            var p1 = core.Player1;
            var p2 = core.Player2;

            _keywords.Clear();
            _engineDamage.Clear();
            _luckRolls.Clear();
            _clashes.Clear();
            _counters.Clear();
            _heals.Clear();
            _redPlays = 0;
            _redResponseCasts = 0;
            _redAttacks = 0;
            _blueAttacks = 0;
            _blueDraws.Clear();
            _redDraws.Clear();
            _engine = null;
            _engines.Clear();
            _seq = 0;
            _order.Clear();

            Action<CardCore.KeywordAppliedEvent> onKeyword = e =>
            {
                if (e == null) return;
                _keywords.Add((GameCore.Instance?.TurnEngine?.TurnNumber ?? -1, e.Keyword ?? "", e.Detail ?? ""));
                _order.Add((++_seq, $"[{e.Keyword}]"));
            };
            Action<AtomicDamageEvent> onDamage = e =>
            {
                if (e?.Source is Card src && _engines.Contains(src) && !e.IsCombatDamage)
                    _engineDamage.Add((NameOf(e.Target), e.Damage));
            };
            Action<LuckRolledEvent> onLuck = e =>
            {
                if (e != null) _luckRolls.Add((e.D1, e.D2, e.Threshold, e.Success));
            };
            Action<ClashResolvedEvent> onClash = e =>
            {
                if (e != null) _clashes.Add((e.MyPower, e.TheirPower, e.Threshold, e.Success));
            };
            Action<CounterChangedEvent> onCounter = e =>
            {
                if (e != null && (e.CounterType == CounterRules.CountdownCounter || e.CounterType == CounterRules.DeathTollCounter
                                  || e.CounterType == CounterRules.CurseCounter || e.CounterType == CounterRules.BlessingCounter))
                    _counters.Add((NameOf(e.Target), e.CounterType, e.Amount));
            };
            Action<HealEvent> onHeal = e =>
            {
                if (e != null) _heals.Add((NameOf(e.Target), e.Amount));
            };
            Action<CardPlayEvent> onPlay = e =>
            {
                if (e?.Player != p2) return;
                if (e.IsResponseCast) _redResponseCasts++;
                else _redPlays++;
            };
            Action<AttackDeclarationEvent> onAttack = e =>
            {
                if (e == null) return;
                if (e.AttackingPlayer == p2) _redAttacks++;
                if (e.AttackingPlayer == p1) _blueAttacks++;
                _order.Add((++_seq, $"宣攻:{(e.AttackingPlayer == p1 ? "蓝" : "红")}"));
            };
            Action<AttackResolvedEvent> onAttackResolved = e =>
            {
                if (e == null) return;
                _order.Add((++_seq, $"攻结:{(e.AttackingPlayer == p1 ? "蓝" : "红")}"));
            };
            Action<CardEnterHandEvent> onHandEnter = e =>
            {
                if (e == null || !e.IsDraw) return;
                if (e.Player == p1) _blueDraws.Add(NameOf(e.Card));
                if (e.Player == p2) _redDraws.Add(NameOf(e.Card));
            };
            Action<AtomicDamageEvent> onAnyDamage = e =>
            {
                if (e == null) return;
                _allDamage.Add($"{NameOf(e.Source)}→{NameOf(e.Target)} −{e.Damage}{(e.IsCombatDamage ? "(战)" : "")}");
                _order.Add((++_seq, $"伤:{NameOf(e.Source)}→{NameOf(e.Target)}−{e.Damage}{(e.IsCombatDamage ? "(战)" : "")}"));
            };
            EventManager.Instance.Subscribe(onKeyword);
            EventManager.Instance.Subscribe(onDamage);
            EventManager.Instance.Subscribe(onLuck);
            EventManager.Instance.Subscribe(onClash);
            EventManager.Instance.Subscribe(onCounter);
            EventManager.Instance.Subscribe(onHeal);
            EventManager.Instance.Subscribe(onPlay);
            EventManager.Instance.Subscribe(onAttack);
            EventManager.Instance.Subscribe(onAttackResolved);
            EventManager.Instance.Subscribe(onHandEnter);
            EventManager.Instance.Subscribe(onAnyDamage);
            try
            {
                // 教学直入口径（与蓝充盈场景回归同序）：Seeder.Apply → StartGame(P2 起跳须显式传) → SimpleAI 连续走回合
                TutorialScenarioSeeder.Apply(core, entry.scenario, _board);
                core.StartGame(entry.scenario.FirstIsPlayer1 ? null : core.Player2);

                // 受测引擎卡定位 + 盘面读数（外部证据：门槛推导值/预置计数/邻接数——与引擎判定同式）
                foreach (var engineName in EngineNamesOf(entry.id))
                {
                    var card = Unit(core, p2, engineName) ?? In(core, p2, Zone.Hand, engineName) ?? In(core, p2, Zone.Graveyard, engineName);
                    Assert(card != null, $"受测引擎卡在场/在手：{engineName}");
                    if (card != null) _engines.Add(card);
                }
                _engine = _engines.Count > 0 ? _engines[0] : null;
                if (_engine != null)
                {
                    foreach (var def in GameActions.GetCardEffectDefinitions(_engine) ?? new List<EffectDefinition>())
                        foreach (var a in def?.Effects ?? new List<AtomicEffectInstance>())
                            if (a?.Branch != null)
                                Console.WriteLine($"    [引擎载荷] def={def.Id} engine={a.Branch.EngineKind} param={a.Branch.EngineParam} " +
                                                  $"载荷通道={(string.IsNullOrEmpty(a.Branch.EffectId) ? "Then原子列" : $"效果引用 {a.Branch.EffectId}")}" +
                                                  $"（Then原子 {a.Branch.Then?.Count ?? 0} 个）" +
                                                  $" 门槛x（拼枪/死亡计数=Then锚价推导）={CostDerivationService.RewardThreshold(a.Branch.Then)}");
                    if (entry.id.StartsWith("countdown"))
                        Console.WriteLine($"    读数：教学·20 预置 Countdown 层数={_engine.GetCounterCount(CounterRules.CountdownCounter)}（seeder counters 注入）");
                    if (entry.id.StartsWith("unity"))
                        Console.WriteLine($"    读数：教学·37 邻接占用={BranchEngines.UnityAdjacentCountOf?.Invoke(_engine) ?? -1}（UnityAdjacentCountOf 注入口，x=4）");
                    if (entry.id == "deathtoll-fire")
                        Console.WriteLine($"    读数：教学·27 预置 DeathToll 层数={_engine.GetCounterCount(CounterRules.DeathTollCounter)}");
                }

                _allDamage.Clear();
                _order.Clear();
                // 响应链场景（⑱）：钉选红（P2）在蓝宣攻响应窗打出手中法术——窗口机制/速度门/可付性
                // 全走引擎真实裁决，只钉「打不打」这一决策（General 默认不会在对手攻击窗主动甩诅咒）。
                if (entry.id == "fullness-response-chain")
                    ResponseWindowService.AiResponder = (c, responder, opts) =>
                        responder == p2
                            ? (opts ?? new List<ResponseOption>()).FirstOrDefault(o => o.Kind == ResponseOption.ResponseKind.HandCard)
                            : null;
                RunTurns(TurnCountOf(entry.id));

                int Kw(string kw) => _keywords.Count(k => k.keyword == kw);
                string KwDetail(string kw) => _keywords.FirstOrDefault(k => k.keyword == kw).detail ?? "无";
                string DescribeDamage()
                    => _engineDamage.Count == 0 ? "无" : string.Join("；", _engineDamage.Select(d => $"{d.target} −{d.dmg}"));

                switch (entry.id)
                {
                    case "countdown-fire":
                        Assert(Kw("倒计时") == 1 && KwDetail("倒计时").Contains("归零"),
                            $"① 准备阶段归零直发恰 1 次播报（实际 {Kw("倒计时")}，明细：{KwDetail("倒计时")}）");
                        Assert(_keywords.FirstOrDefault(k => k.keyword == "倒计时").turn == 12,
                            $"① tick 时点=红第 2 个回合准备阶段（起跳回合跳准备阶段；实际回合 " +
                            $"{_keywords.FirstOrDefault(k => k.keyword == "倒计时").turn}）");
                        Assert(_engineDamage.Count == 1 && _engineDamage[0].dmg == 2 && _engineDamage[0].target == p1.Name,
                            $"① 奖励=2 伤落蓝角色（实际 {DescribeDamage()}）");
                        Assert(p1.Life == 26, $"① 蓝终态 26（30−引擎2伤−教学·20攻击2：t12 回合开始解横置后引擎参战）——实际 {p1.Life}");
                        Assert(_engine != null && _engine.GetCounterCount(CounterRules.CountdownCounter) == 2,
                            "① 归零后重挂=2 回合（param=2）");
                        break;

                    case "countdown-wait":
                        Assert(Kw("倒计时") == 1, $"② 全程恰 1 次触发（实际 {Kw("倒计时")}）——首红回合 tick 2→1 不出奖");
                        var fireTurn = _keywords.FirstOrDefault(k => k.keyword == "倒计时").turn;
                        Assert(fireTurn == 14, $"② 触发时点=第 3 个红回合（全局回合14=起跳10+4：t12 tick 2→1、t14 tick 1→0；实际 {fireTurn}）——蓝回合 tick 不动红方引擎");
                        Assert(_engineDamage.Count == 1 && _engineDamage[0].dmg == 2, $"② 奖励 2 伤恰一次（实际 {DescribeDamage()}）");
                        Assert(p1.Life == 24, $"② 蓝终态 24（30−引擎2伤−教学·20攻击2×2：t12/t14 解横置各攻一次）——实际 {p1.Life}");
                        Assert(_engine != null && _engine.GetCounterCount(CounterRules.CountdownCounter) == 2, "② 重挂=2");
                        break;

                    case "luck-fire":
                        Assert(_luckRolls.Count >= 1 && _luckRolls.All(r => r.ok && r.x == 1),
                            $"③ 掷骰必发且 x=1 双骰恒中（实际 {string.Join("/", _luckRolls.Select(r => $"{r.d1}+{r.d2}≥{r.x}:{r.ok}"))}）");
                        Assert(Kw("运势") == 1, $"③ 运势播报恰 1 次（每卡每回合封顶；实际 {Kw("运势")}）");
                        Assert(_engineDamage.Count == 1 && _engineDamage[0].dmg == 1 && _engineDamage[0].target == p1.Name,
                            $"③ 奖励=1 伤落蓝角色（实际 {DescribeDamage()}）");
                        Assert(p1.Life == 29, $"③ 蓝终态 29（30−引擎1伤；主动发动横置宿主=教学·21 本回合不再攻击）——实际 {p1.Life}");
                        break;

                    case "clash-fire":
                        Assert(_redAttacks >= 1, "④ 红确实宣言攻击（攻击宣言=拼枪触发口）");
                        Assert(_clashes.Count >= 1 && _clashes[0].ok && _clashes[0].my == 7 && _clashes[0].their == 0 && _clashes[0].x == 6,
                            $"④ 拼枪评估：红库7/7 vs 蓝库无生物(0)，差7 ≥ 门槛6（实际 {string.Join("/", _clashes.Select(c => $"{c.my}vs{c.their}≥{c.x}:{c.ok}"))}）");
                        Assert(Kw("拼枪") == 1, $"④ 拼枪播报恰 1 次（实际 {Kw("拼枪")}）");
                        Assert(_engineDamage.Count == 1 && _engineDamage[0].dmg == 6 && _engineDamage[0].target == p1.Name,
                            $"④ 奖励=6 伤落蓝角色（实际 {DescribeDamage()}）");
                        Assert(p1.Life == 17, $"④ 蓝终态 17（30−引擎6伤−攻击本体7）——实际 {p1.Life}");
                        break;

                    case "clash-miss":
                        Assert(_redAttacks >= 1, "⑤ 红确实宣言攻击（评估必发）");
                        Assert(_clashes.Count >= 1 && !_clashes[0].ok,
                            $"⑤ 差额 −7 < 6 → Success=false（实际 {string.Join("/", _clashes.Select(c => $"{c.my}vs{c.their}≥{c.x}:{c.ok}"))}）");
                        Assert(Kw("拼枪") == 0 && _engineDamage.Count == 0,
                            $"⑤ 不达标零播报零奖励（播报 {Kw("拼枪")} 伤 {DescribeDamage()}）");
                        Assert(p1.Life == 23, $"⑤ 蓝终态 23（只吃攻击本体7）——实际 {p1.Life}");
                        break;

                    case "deathtoll-fire":
                        Assert(Kw("死亡计数") == 1, $"⑥ 计数满 6=门槛→恰 1 次触发（实际 {Kw("死亡计数")}，明细：{KwDetail("死亡计数")}）");
                        Assert(_engineDamage.Count == 1 && _engineDamage[0].dmg == 6 && _engineDamage[0].target == "教学·3",
                            $"⑥ 奖励=6 伤恰一次（实际 {DescribeDamage()}）");
                        Assert(_engine != null && _engine.GetCounterCount(CounterRules.DeathTollCounter) == 6,
                            "⑥ 终态计数=6（满值封顶不再计）");
                        Assert(CountIn(core, p1, Zone.Graveyard, "教学·3") == 6 && CountAlive(core, p1, "教学·3") == 0,
                            $"⑥ 蓝 6 只 1/1 全灭（墓 {CountIn(core, p1, Zone.Graveyard, "教学·3")} / 场存活 {CountAlive(core, p1, "教学·3")}）");
                        Assert(p1.Life == 30, $"⑥ 蓝零战损生命（无头自动选首=第 6 杀的尸体仍在候选首位，6 伤落尸体不落角色——角色兜底只发生在完全无单位时，充盈⑥同源口径）——实际 {p1.Life}");
                        break;

                    case "unity-fire":
                        Assert(BranchEngines.UnityAdjacentCountOf != null && BranchEngines.UnityAdjacentCountOf(_engine) == 4,
                            $"⑦ 施放前邻接占用=4（实际 {BranchEngines.UnityAdjacentCountOf?.Invoke(_engine) ?? -1}）——盘面钉位成立");
                        Assert(_redPlays == 2, $"⑦ 红同回合连出两张（实际 {_redPlays}）");
                        Assert(Kw("团结") == 1, $"⑦ 首张结算触发、次张每卡每回合封顶（实际 {Kw("团结")}，明细：{KwDetail("团结")}）");
                        Assert(_engineDamage.Count == 1 && _engineDamage[0].dmg == 4 && _engineDamage[0].target == p1.Name,
                            $"⑦ 奖励=4 伤恰一次落蓝角色（实际 {DescribeDamage()}）");
                        Assert(p1.Life == 22, $"⑦ 蓝终态 22（30−引擎4伤−两张新火苗攻击各2；盘面四火苗已横置不参与）——实际 {p1.Life}");
                        break;

                    case "unity-miss":
                        Assert(BranchEngines.UnityAdjacentCountOf != null && BranchEngines.UnityAdjacentCountOf(_engine) == 3,
                            $"⑧ 施放前邻接占用=3（实际 {BranchEngines.UnityAdjacentCountOf?.Invoke(_engine) ?? -1}）——盘面钉位成立");
                        Assert(_redPlays == 1, $"⑧ 红出一张火苗（落位(5,4)不邻引擎、计数稳定3；实际 {_redPlays}）");
                        Assert(Kw("团结") == 0 && _engineDamage.Count == 0,
                            $"⑧ 3 < 4 恒不触发（播报 {Kw("团结")} 伤 {DescribeDamage()}）");
                        Assert(p1.Life == 28, $"⑧ 蓝终态 28（只吃新火苗攻击2；零引擎伤害）——实际 {p1.Life}");
                        break;

                    case "curse-fire":
                        Assert(_redPlays >= 1, "⑨ 红确实施放教学·50（施放=部署口）");
                        Assert(EngineEffectIdOf(_engine, BranchEngineKind.CurseOnDraw) == "CC0DE001",
                            $"⑨ 载荷通道=组合效果引用 CC0DE001（converter 原子str→EffectId 主通道；Then 兼容通道未用）——实际 " +
                            $"{EngineEffectIdOf(_engine, BranchEngineKind.CurseOnDraw) ?? "null"}");
                        Assert(Kw("诅咒") >= 1 && _keywords.Any(k => k.keyword == "诅咒" && k.detail.Contains("附加")),
                            $"⑨ 部署段播报（附加；明细：{KwDetail("诅咒")}）");
                        Assert(_keywords.Any(k => k.keyword == "诅咒" && k.detail.Contains("发作")),
                            $"⑨ 蓝抽到→发作段播报（明细：{KwDetail("诅咒")}）");
                        Assert(_counters.Any(c => c.type == CounterRules.CurseCounter && c.amount >= 1),
                            "⑨ 诅咒指示物挂载事件（+1 层）");
                        var cursed = (core.ZoneManager.GetCards(p1, Zone.Hand) ?? new List<Card>())
                            .Concat(core.ZoneManager.GetCards(p1, Zone.ElementPool) ?? new List<Card>())
                            .FirstOrDefault(c => c is IHasName n && n.CardName == "教学·3");
                        Assert(cursed != null && cursed.GetCounterCount(CounterRules.CurseCounter) == 0,
                            "⑨ 被诅咒卡已抽到入手（蓝付不起 1 灰随后铺为地）且指示物消层（先消层再结算·一次性）");
                        Assert(_blueDraws.Count == 1 && _blueDraws[0] == "教学·3",
                            $"⑨ 蓝本回合恰抽 1 张=被诅咒卡（实际 [{string.Join(",", _blueDraws)}]）");
                        Assert(_engineDamage.Count == 2 && _engineDamage.All(d => d.dmg == 3 && d.target == p1.Name),
                            $"⑨ 组合载荷两段各 3 伤落蓝角色（按效果定义 Steps 遍历·免编排·原子各自解析目标；实际 {DescribeDamage()}）");
                        Assert(p1.Life == 24, $"⑨ 蓝终态 24（30−两段3伤）——实际 {p1.Life}");
                        break;

                    case "blessing-fire":
                        Assert(_redPlays >= 1, "⑩ 红确实施放教学·51（施放=部署口）");
                        Assert(EngineEffectIdOf(_engine, BranchEngineKind.BlessingOnDraw) == "CC0DE002",
                            $"⑩ 载荷通道=组合效果引用 CC0DE002（converter 原子str→EffectId 主通道；Then 兼容通道未用）——实际 " +
                            $"{EngineEffectIdOf(_engine, BranchEngineKind.BlessingOnDraw) ?? "null"}");
                        Assert(_keywords.Any(k => k.keyword == "祝福" && k.detail.Contains("附加")),
                            $"⑩ 部署段播报（附加；明细：{KwDetail("祝福")}）");
                        Assert(_keywords.Any(k => k.keyword == "祝福" && k.detail.Contains("发作")),
                            $"⑩ 红第 2 回合抽到→发作段播报（明细：{KwDetail("祝福")}）");
                        Assert(_counters.Any(c => c.type == CounterRules.BlessingCounter && c.amount >= 1),
                            "⑩ 祝福指示物挂载事件（+1 层）");
                        Assert(_heals.Count == 1 && _heals[0].amount == 12 && _heals[0].target == p2.Name,
                            $"⑩ 发作=组合载荷回 12 落红角色（实际 {string.Join("/", _heals.Select(h => $"{h.target}+{h.amount}"))}）");
                        Assert(p2.Life == 24, $"⑩ 红终态 24（12+回12）——实际 {p2.Life}");
                        var blessedInHand = (core.ZoneManager.GetCards(p2, Zone.Hand) ?? new List<Card>())
                            .FirstOrDefault(c => c is IHasName bn && bn.CardName == "教学·3");
                        Assert(blessedInHand == null || blessedInHand.GetCounterCount(CounterRules.BlessingCounter) == 0,
                            "⑩ 被祝福卡抽到后指示物消层");
                        break;

                    case "countdown-sleep":
                        Assert(Kw("倒计时") == 1, $"⑪ 沉睡期间归零不发、睡醒补发恰 1 次（实际 {Kw("倒计时")}，明细：{KwDetail("倒计时")}）");
                        var wakeTurn = _keywords.FirstOrDefault(k => k.keyword == "倒计时").turn;
                        Assert(wakeTurn >= 12, $"⑪ 补发时点=睡醒后首个己方准备阶段（≥t12；实际 {wakeTurn}）");
                        Assert(_engineDamage.Count == 1 && _engineDamage[0].dmg == 2, $"⑪ 奖励 2 伤恰一次（实际 {DescribeDamage()}）");
                        Assert(_engine != null && _engine.GetCounterCount(CounterRules.CountdownCounter) == 2, "⑪ 补发后重挂=2");
                        Assert(_engine != null && _engine.GetCounterCount(KeywordRules.SleepCounter) == 0, "⑪ 沉睡层耗尽（睡醒口径）");
                        break;

                    case "luck-miss":
                        Assert(_luckRolls.Count >= 1, $"⑫ 掷骰必发=评估发生证据（实际 {_luckRolls.Count} 次）");
                        Assert(_luckRolls.All(r => !r.ok && r.x == 6),
                            $"⑫ x=6 非双6 不中（实际 {string.Join("/", _luckRolls.Select(r => $"{r.d1}+{r.d2}≥{r.x}:{r.ok}"))}）");
                        Assert(Kw("运势") == 1 && KwDetail("运势").Contains("未达标"),
                            $"⑫ 不中也播报（成败两态口径——负例带评估证据；明细：{KwDetail("运势")}）");
                        Assert(_engineDamage.Count == 0, $"⑫ 零奖励伤害（实际 {DescribeDamage()}）");
                        Assert(p1.Life == 30, $"⑫ 蓝满血（主动发动横置宿主=不攻击）——实际 {p1.Life}");
                        break;

                    case "clash-twice":
                        Assert(_redAttacks == 2, $"⑬ 两次攻击宣言（实际 {_redAttacks}）");
                        Assert(_clashes.Count == 2 && _clashes.All(c => c.ok && c.my == 7 && c.their == 0),
                            $"⑬ 每宣必评估且均命中——评估无每回合上限（实际 {string.Join("/", _clashes.Select(c => $"{c.my}vs{c.their}:{c.ok}"))}）");
                        Assert(Kw("拼枪") == 1 && _engineDamage.Count == 1 && _engineDamage[0].dmg == 6,
                            $"⑬ 奖励发射每卡每回合封顶 1 发（播报 {Kw("拼枪")} 伤 {DescribeDamage()}）");
                        Assert(p1.Life == 10, $"⑬ 蓝终态 10（30−引擎6−攻击7×2）——实际 {p1.Life}");
                        break;

                    case "deathtoll-repeat":
                        Assert(EngineThresholdOf(_engine, BranchEngineKind.DeathToll) == 2,
                            $"⑭ 门槛x=2（then=抽1牌锚价推导；实际 {EngineThresholdOf(_engine, BranchEngineKind.DeathToll)}）");
                        var fireTurns = _keywords.Where(k => k.keyword == "死亡计数").Select(k => k.turn).ToList();
                        Assert(fireTurns.Count == 5 && fireTurns.SequenceEqual(new[] { 12, 14, 16, 18, 20 }),
                            $"⑭ [x,6] 每档一发：t12/14/16/18/20 共5发（实际 [{string.Join("/", fireTurns)}]；首杀计数1<2=未达门槛负例）");
                        Assert(_redDraws.Count == 10,
                            $"⑭ 红总抽牌=5准备阶段(t12起)+5奖励=10（实际 {_redDraws.Count}）——抽牌奖励逐次触发的直接读数");
                        Assert(_engine != null && _engine.GetCounterCount(CounterRules.DeathTollCounter) == 6,
                            "⑭ 终态计数=6（触发不清零·满值封顶不再计）");
                        Assert(CountIn(core, p1, Zone.Graveyard, "教学·3") == 6 && CountAlive(core, p1, "教学·3") == 0,
                            $"⑭ 蓝 6 只 1/1 全灭（墓 {CountIn(core, p1, Zone.Graveyard, "教学·3")} 场 {CountAlive(core, p1, "教学·3")}）");
                        Assert(Unit(core, p2, "教学·36")?.IsAlive ?? false, "⑭ 逐杀器教学·36 存活（1/2 满血——蓝 1/1 打不动它）");
                        break;

                    case "unity-mixed-cells":
                        Assert(BranchEngines.UnityAdjacentCountOf != null && BranchEngines.UnityAdjacentCountOf(_engine) == 4,
                            $"⑮ 施放前邻接占用=4（角色格1+地牌格1+单位格2；实际 {BranchEngines.UnityAdjacentCountOf?.Invoke(_engine) ?? -1}）");
                        Assert(_redPlays == 1, $"⑮ 红出一张火苗（实际 {_redPlays}）");
                        Assert(Kw("团结") == 1 && KwDetail("团结").Contains("各算一"),
                            $"⑮ 触发且播报含「各算一」口径（明细：{KwDetail("团结")}）——若角色/地牌不计则仅2<4不触发");
                        Assert(_engineDamage.Count == 1 && _engineDamage[0].dmg == 4 && _engineDamage[0].target == p1.Name,
                            $"⑮ 奖励=4 伤落蓝角色（实际 {DescribeDamage()}）");
                        Assert(p1.Life == 24, $"⑮ 蓝终态 24（30−4−火苗攻击2）——实际 {p1.Life}");
                        break;

                    case "fullness-cap6":
                        Assert(EngineThresholdOf(_engine, BranchEngineKind.ManaSurplus) == 6,
                            $"⑯ 门槛x=6（then 6伤锚价推导；实际 {EngineThresholdOf(_engine, BranchEngineKind.ManaSurplus)}）");
                        Assert(Kw("元素充盈") == 1 && KwDetail("元素充盈").Contains("封顶 6）== 6") && KwDetail("元素充盈").Contains("己 8"),
                            $"⑯ 差8读作封顶6仍恰等触发（明细：{KwDetail("元素充盈")}）");
                        Assert(_engineDamage.Count == 1 && _engineDamage[0].dmg == 6 && _engineDamage[0].target == p1.Name,
                            $"⑯ 奖励=6 伤落蓝角色（实际 {DescribeDamage()}）");
                        Assert(p1.Life == 22, $"⑯ 蓝终态 22（30−6−火苗攻击2）——实际 {p1.Life}");
                        break;

                    case "curse-empty-deck":
                        Assert(_redPlays == 1, $"⑰ 红确实施放教学·50（实际 {_redPlays}）——部署口确被进入，负例不是引擎没跑");
                        Assert(Kw("诅咒") == 0 && !_counters.Any(c => c.type == CounterRules.CurseCounter),
                            $"⑰ 空库空转：零附加播报零挂载事件（播报 {Kw("诅咒")}）");
                        Assert(p1.Life == 30 && p2.Life == 30, $"⑰ 双方满血零副作用（蓝 {p1.Life} 红 {p2.Life}）");
                        break;

                    case "fullness-response-chain":
                        Assert(_blueAttacks >= 1, $"⑱ 蓝确实宣言攻击（实际 {_blueAttacks}）——响应窗的锚点");
                        Assert(_redResponseCasts == 1, $"⑱ 红在响应窗打出教学·50（实际 {_redResponseCasts}——响应出牌=第二栈对象）");
                        Console.WriteLine($"    [读数] 连锁后剩余元素：红={core.ElementPool.GetRemainingElementTotal(p2)} 蓝={core.ElementPool.GetRemainingElementTotal(p1)}"
                                          + $"（充盈门槛x=2，差须恰==2）");
                        int SeqOf(string tag) => _order.Where(o => o.tag.Contains(tag)).Select(o => o.seq).FirstOrDefault();
                        var seqCurse = SeqOf("[诅咒]");
                        var seqUnity = SeqOf("[团结]");
                        var seqAtkRes = _order.Where(o => o.tag == "攻结:蓝").Select(o => o.seq).FirstOrDefault();
                        var seqFull = SeqOf("[元素充盈]");
                        Assert(seqCurse > 0 && seqUnity > 0 && seqAtkRes > 0 && seqFull > 0,
                            $"⑱ 四时点事件齐全（诅咒{seqCurse}/团结{seqUnity}/攻结{seqAtkRes}/充盈{seqFull}）——序号日志：{string.Join(" < ", _order.Select(o => o.tag))}");
                        Assert(seqCurse < seqUnity && seqUnity < seqAtkRes && seqAtkRes < seqFull,
                            $"⑱ 时点序：诅咒附加({seqCurse}) < 团结随施放即评估({seqUnity}) < 攻击结算({seqAtkRes}) < 充盈整链落定后({seqFull})——团结=施放拍/充盈=连锁终点拍（判据七拆拍直接证据；攻击打脸伤害不走 AtomicDamageEvent，时点证据=AttackResolvedEvent）");
                        Assert(Kw("元素充盈") == 1 && KwDetail("元素充盈").Contains("== 2"),
                            $"⑱ 充盈恰等一次（红付费后剩余2；明细：{KwDetail("元素充盈")}）");
                        Assert(_keywords.Any(k => k.keyword == "诅咒" && k.detail.Contains("附加")), "⑱ 响应出牌的诅咒部署照常（部署口与施放路径无关）");
                        Assert(Life(Unit(core, p1, "教学·8")) == 1,
                            $"⑱ 双引擎奖励均落蓝7/7（团结4伤+充盈2伤：7→1；实际生命 {Life(Unit(core, p1, "教学·8"))}）——蓝宣攻单位存活=攻击结算真实发生");
                        Assert(p1.Life == 30, $"⑱ 蓝满血（奖励落单位不落脸）——实际 {p1.Life}");
                        Assert(p2.Life == 29, $"⑱ 红终态 29（吃蓝1/1攻击1）——实际 {p2.Life}");
                        break;

                    default:
                        Assert(false, $"未知场景 id：{entry.id}（C# 断言表没有对应分支）");
                        break;
                }

                Console.WriteLine($"    轨迹：红出牌 {_redPlays} 次 / 攻击宣言 {_redAttacks} 次 / "
                                  + $"引擎播报 {string.Join(",", _keywords.Select(k => $"{k.keyword}@{k.turn}"))} / "
                                  + $"引擎伤 {_engineDamage.Count} 次 / 蓝抽牌 {_blueDraws.Count} 张 / "
                                  + $"终态生命 蓝={p1.Life} 红={p2.Life}");
                foreach (var k in _keywords) Console.WriteLine($"    [播报] {k.keyword}@回合{k.turn}：{k.detail}");
                foreach (var c in _counters) Console.WriteLine($"    [指示物] {c.target} {c.type} {c.amount:+#;-#;0}");
                foreach (var r in _luckRolls) Console.WriteLine($"    [掷骰] {r.d1}+{r.d2} ≥ {r.x}×2 → {r.ok}");
                foreach (var c in _clashes) Console.WriteLine($"    [拼枪] 我{c.my} vs 对{c.their}，门槛{c.x} → {c.ok}");
                foreach (var d in _allDamage) Console.WriteLine($"    [伤害] {d}");
                foreach (var h in _heals) Console.WriteLine($"    [治疗] {h.target} +{h.amount}");
            }
            finally
            {
                EventManager.Instance.Unsubscribe(onKeyword);
                EventManager.Instance.Unsubscribe(onDamage);
                EventManager.Instance.Unsubscribe(onLuck);
                EventManager.Instance.Unsubscribe(onClash);
                EventManager.Instance.Unsubscribe(onCounter);
                EventManager.Instance.Unsubscribe(onHeal);
                EventManager.Instance.Unsubscribe(onPlay);
                EventManager.Instance.Unsubscribe(onAttack);
                EventManager.Instance.Unsubscribe(onAttackResolved);
                EventManager.Instance.Unsubscribe(onHandEnter);
                EventManager.Instance.Unsubscribe(onAnyDamage);
                AiStrategyRegistry.Clear();
                ResponseWindowService.AiResponder = null;
                TargetSelectionService.AiOrder = null;
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
                DetachBoard();
            }
        }

        /// <summary>连续走 N 个回合（P2→P1→P2 轮替——BattleController.TurnPlayer 动态读当前回合方）。
        /// 回合**之间**补 <see cref="TurnEngine.CheckPhaseTransition"/> 泵（无帧泵环境的唯一推进口，
        /// AiDuelRunner 同款）——不泵则相位卡 End、后续 TakeTurn 空转且准备阶段自动化（抽牌/倒计时 tick）
        /// 永不发生；末回合后不泵（多泵会推进下一回合准备阶段：空库抽牌转疲劳等脏读数）。</summary>
        private static void RunTurns(int turns)
        {
            var ai = new SimpleAI(new ProbeStrategy());
            var core = GameCore.Instance;
            var ctrl = new BattleController();
            for (int i = 0; i < turns && !core.IsGameOver; i++)
            {
                ai.TakeTurn(ctrl);
                if (!core.IsGameOver && i < turns - 1)
                    core.TurnEngine.CheckPhaseTransition(); // 无帧泵环境的唯一推进口
            }
        }

        // ======================================== 断言辅助 ========================================

        /// <summary>卡上指定引擎的载荷效果引用（2026-11-06 附加诅咒/祝福载荷效果化：converter 原子
        /// str→EffectId 主通道；null=旧 Then 兼容通道/无载荷）。</summary>
        private static string EngineEffectIdOf(Card card, BranchEngineKind kind)
        {
            foreach (var def in GameActions.GetCardEffectDefinitions(card) ?? new List<EffectDefinition>())
                foreach (var a in def?.Effects ?? new List<AtomicEffectInstance>())
                    if (a?.Branch != null && a.Branch.Settle == BranchSettleKind.Engine && a.Branch.EngineKind == kind)
                        return a.Branch.EffectId;
            return null;
        }

        /// <summary>卡上指定引擎的自平衡门槛（Then 锚价推导——与运行时判定同式；无载荷=-1）。</summary>
        private static int EngineThresholdOf(Card card, BranchEngineKind kind)
        {
            foreach (var def in GameActions.GetCardEffectDefinitions(card) ?? new List<EffectDefinition>())
                foreach (var a in def?.Effects ?? new List<AtomicEffectInstance>())
                    if (a?.Branch != null && a.Branch.Settle == BranchSettleKind.Engine && a.Branch.EngineKind == kind)
                        return CostDerivationService.RewardThreshold(a.Branch.Then);
            return -1;
        }

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

        // ======================================== 装载/棋盘（与蓝充盈场景回归同口径） ========================================

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
