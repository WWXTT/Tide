using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CardCore;
using CardCore.AI.NeuralEnv;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 规范总校验（2026-11-02 定案）：一次性只读体检，Console 分区打印 A~E 报告 + 末尾 PASS/FAIL 汇总。
    /// 任何一项 FAIL 给出「项名 + 具体卡/原子 + 原因」。
    /// ■ A 对账三件套：A1 卡表合并 SHA256（Cards.json + Effects.json + Configs/原子表_*.json，
    ///   按相对路径 Ordinal 排序后拼接原文，输出前 8 位）；A2 布局版本 C#(TideObservation) vs
    ///   Python(tide_rl/tide_features.py)。A3（ONNX stamp）2026-11-02 已删——与卡表 hash 不同域，恒假警报。
    ///   域不同（stamp=导出时 card_identity_manifest.json 整文件 sha256），按定案只 WARN 不 FAIL，
    ///   附链路上下文（fixture↔磁盘 manifest、manifest table 指纹↔当前原子表指纹、fixture 布局字段）。
    /// ■ B 颜色规范：反射复用 CardPipelineVerifier.RunColorAudit（单源不改），捕获其 Console 输出汇总。
    /// ■ C 减费三选一：复用 EffectComposerScreen 单源判定（DiscountConflictOf / BranchRewardCapViolationOf）。
    /// ■ D 目标口径回归：自动构造对局——D1 诱敌 3 敌 LAE 组合枚举（夹具同 LaeMultiTargetVerifier①）；
    ///   D2 预组·22（冻结两个对方单位）3 敌施放后恰 2 横置。
    /// ■ E 系统规则抽检：E1 全体消灭 handler 含结界分支（源码文本）；E2 代价栏两条拒绝文案存在；
    ///   E3 SkillValueViolations 存在；E4 HasPlayableTargets 三态（0 敌 false / 1 敌 true，挂在 D1 对局内）。
    /// 硬约束：全程只读——不写表、不改数据、不触发重推（AtomicEffectTable/EffectsLibrary.Reload 与
    /// RunColorAudit 同款「重读现值」；对局为内存态）。
    /// </summary>
    public static class SpecMasterVerifier
    {
        // ---- 夹具卡名（与 LaeMultiTargetVerifier 同源；教学池 ID 随内容重推漂移，按卡名解析。
        //      2026-11-03 教学池红卡改名对齐：火灵→火灵·攻/守、火种→火种·锐/厚、火炬退役）----
        private const string LureName = "诱敌";
        private const string FreezeTwoName = "预组·22"; // C_B19375C1 冻结两个对方单位（蓝3·selection=Multiple·count=2）
        private const string EnemyAName = "火灵·攻";   // 1 血
        private const string EnemyBName = "教学·3";   // 1/1 白板（原火种·锐已改 2/2，移出 1 血夹具）
        private const string EnemyCName = "火灵·守";   // 1 血

        private static readonly List<Item> Items = new List<Item>();

        private sealed class Item
        {
            public readonly string Id;
            public bool Fail;
            public bool Warn;
            public readonly List<string> Lines = new List<string>();
            public Item(string id) { Id = id; }
            public void Line(string s) => Lines.Add(s);
            public void FailWith(string s) { Fail = true; Lines.Add(s); }
            public void WarnWith(string s) { Warn = true; Lines.Add(s); }
        }

        [MenuItem("Tools/规范总校验（2026-11-02 定案）")]
        public static void Run()
        {
            Items.Clear();
            string a1Hash = null;
            try { a1Hash = SectionA(); }
            catch (Exception ex) { Of("A").FailWith("A 段异常中断：" + ex.Message); }
            try { SectionB(); }
            catch (Exception ex) { Of("B").FailWith("B 段异常中断：" + ex.Message); }
            try { SectionC(); }
            catch (Exception ex) { Of("C").FailWith("C 段异常中断：" + ex.Message); }
            try { SectionD(); }
            catch (Exception ex) { Of("D").FailWith("D 段异常中断：" + ex.Message + "\n" + ex.StackTrace); }
            try { SectionE(); }
            catch (Exception ex) { Of("E").FailWith("E 段异常中断：" + ex.Message); }

            // ---- 分区打印 + 汇总 ----
            Debug.Log("[规范总校验] ===== A. 对账三件套（并行开发防串味）=====");
            Print("A1"); Print("A2");
            Debug.Log("[规范总校验] ===== B. 颜色规范（复用 RunColorAudit，只报不改）=====");
            Print("B");
            Debug.Log("[规范总校验] ===== C. 减费三选一（2026-11-02 新规）=====");
            Print("C1"); Print("C2"); Print("C3"); Print("C4"); Print("C5");
            Debug.Log("[规范总校验] ===== D. 目标口径（自动构造对局回归）=====");
            Print("D1"); Print("D2");
            Debug.Log("[规范总校验] ===== E. 系统规则抽检 =====");
            Print("E1"); Print("E2"); Print("E3"); Print("E4");

            int pass = Items.Count(i => !i.Fail), fail = Items.Count(i => i.Fail), warn = Items.Count(i => i.Warn);
            var sb = new StringBuilder();
            sb.Append("[规范总校验] ===== 汇总：PASS ").Append(pass).Append(" / FAIL ").Append(fail)
              .Append("（WARN ").Append(warn).Append("）=====");
            if (fail > 0)
            {
                sb.Append("\nFAIL 明细（项名 + 卡/原子 + 原因）：");
                foreach (var i in Items.Where(i => i.Fail))
                    foreach (var l in i.Lines.Where(l => l.StartsWith("[FAIL]", StringComparison.Ordinal)))
                        sb.Append("\n  ").Append(i.Id).Append(' ').Append(l);
            }
            Debug.Log(sb.ToString());
        }

        private static Item Of(string id)
        {
            var it = Items.FirstOrDefault(i => i.Id == id);
            if (it == null) { it = new Item(id); Items.Add(it); }
            return it;
        }

        private static void Print(string id)
        {
            var it = Items.FirstOrDefault(i => i.Id == id);
            if (it == null) { Debug.Log($"[规范总校验][{id}] ?（未执行）"); return; }
            var head = it.Fail ? "FAIL" : it.Warn ? "WARN" : "PASS";
            Debug.Log($"[规范总校验][{id}] {head} {string.Join("\n  ", it.Lines)}");
        }

        // ======================================== A. 对账三件套 ========================================

        private static string SectionA()
        {
            string root = Path.Combine(Application.dataPath, "..");
            // A1 卡表合并哈希
            var a1 = Of("A1");
            var files = new List<string> { "Assets/Data/CardData/Cards.json", "Assets/Data/CardData/Effects.json" };
            // 2026-11-03 训练池分家：训练表（TrainingData）纳入指纹——训练内容不再住主表，
            // 不并入则训练表内容漂移（换号/改行）失去对账；文件缺失（旧副本未同步）跳过不计。
            foreach (var tf in new[] { "Assets/Data/TrainingData/TrainingCards.json", "Assets/Data/TrainingData/TrainingEffects.json" })
                if (File.Exists(Path.Combine(root, tf))) files.Add(tf);
            var cfgDir = Path.Combine(root, "Assets", "Configs");
            files.AddRange(Directory.GetFiles(cfgDir, "原子表_*.json").Select(p => "Assets/Configs/" + Path.GetFileName(p)));
            files.Sort(StringComparer.Ordinal);
            var merged = new StringBuilder();
            var parts = new List<string>();
            foreach (var f in files)
            {
                var text = File.ReadAllText(Path.Combine(root, f));
                merged.Append(text);
                parts.Add($"{Path.GetFileName(f)}({text.Length})");
            }
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(merged.ToString()))).Replace("-", "").ToLowerInvariant();
            string a1Hash = hash.Substring(0, 8);
            a1.Line($"卡表合并 SHA256={a1Hash}（{files.Count} 文件按相对路径排序拼接原文：{string.Join(" | ", parts)}）");

            // A2 布局版本两侧对账
            var a2 = Of("A2");
            int csLayout = TideObservation.LayoutVersion, csNAction = TideObservation.NAction;
            var py = File.ReadAllText(Path.Combine(root, "tide_rl", "tide_features.py"));
            int pyLayout = IntOf(py, @"LAYOUT_VERSION"), pyNAction = IntOf(py, @"N_ACTION_FEATURES");
            a2.Line($"C# TideObservation LayoutVersion={csLayout} NAction={csNAction} vs Python tide_features LAYOUT_VERSION={pyLayout} N_ACTION_FEATURES={pyNAction}");
            if (pyLayout < 0 || pyNAction < 0)
                a2.FailWith("[FAIL] tide_features.py 解析不到 LAYOUT_VERSION/N_ACTION_FEATURES 常量");
            else if (csLayout != pyLayout || csNAction != pyNAction)
                a2.FailWith($"[FAIL] 布局版本两侧不一致（训练副本口径漂移——模型/观测/动作空间不同代）");

            // A3（ONNX stamp vs 卡表 hash）已于 2026-11-02 删除：两者不同域，恒为假警报。
            return a1Hash;
        }

        private static int IntOf(string text, string constName)
        {
            var m = Regex.Match(text, @"(?m)^\s*" + constName + @"\s*=\s*(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }

        // ======================================== B. 颜色规范（复用 RunColorAudit） ========================================

        private static void SectionB()
        {
            var b = Of("B");
            // 表重读现值（与 RunColorAudit 自身行为同款，只读）——B/C 共享一次
            CardCore.Attribute.AtomicEffectTable.Reload();
            EffectsLibrary.Reload();
            CardIdentityService.InvalidateTableCache();

            var captured = new List<string>();
            void OnLog(string condition, string stack, LogType type) => captured.Add(condition);
            Application.logMessageReceived += OnLog;
            try
            {
                var mi = typeof(CardPipelineVerifier).GetMethod("RunColorAudit", BindingFlags.NonPublic | BindingFlags.Static);
                if (mi == null) { b.FailWith("[FAIL] 找不到 CardPipelineVerifier.RunColorAudit（被并行改动？）"); return; }
                mi.Invoke(null, null);
            }
            finally { Application.logMessageReceived -= OnLog; }

            var atomBad = captured.Where(s => s.StartsWith("[颜色][原子]", StringComparison.Ordinal)).ToList();
            // 卡级分色校验已删除（2026-11-02 定案）：没有「本家色」这回事——卡可以带任意颜色效果，
            // 能不能用由卡组可用性校验负责，不在卡面层面对这一点提要求。
            var summary = captured.FirstOrDefault(s => s.StartsWith("[颜色校验]", StringComparison.Ordinal)) ?? "";
            var m = Regex.Match(summary, @"原子符合\s*(\d+)\s*/\s*不符合\s*(\d+)");
            int atomOk = m.Success ? int.Parse(m.Groups[1].Value) : -1;
            b.Line($"原子符合 {atomOk} / 不符合 {atomBad.Count}");
            if (atomBad.Count > 0)
            {
                b.WarnWith($"[WARN] 原子不符合 {atomBad.Count} 条（前 10 条）：");
                foreach (var s in atomBad.Take(10)) b.Line("  " + s);
            }
        }

        // ======================================== C. 减费三选一 ========================================

        private static void SectionC()
        {
            var c1 = Of("C1"); var c2 = Of("C2"); var c3 = Of("C3"); var c4 = Of("C4"); var c5 = Of("C5");
            var cards = LoadCardUniverse();
            int scanned = 0, withCost = 0, branchFx = 0;

            foreach (var card in cards)
            {
                if (card == null) continue;
                scanned++;
                // C1/C4/C5：DiscountConflictOf 单源（返回首个违规；2026-11-08 效果级化——代价行都在效果 Costs 口径内）
                if (EffectComposerScreen.CardHasCost(card))
                {
                    withCost++;
                    var v = EffectComposerScreen.DiscountConflictOf(card);
                    if (v != null)
                    {
                        if (v.Contains("超上限")) c1.FailWith("[FAIL] " + v);
                        else if (v.Contains("并列添加了两个效果")) c4.FailWith("[FAIL] " + v);
                        else if (v.Contains("光环类")) c5.FailWith("[FAIL] " + v);
                        else if (v.Contains("不能再选分支")) c4.FailWith("[FAIL] " + v); // 分支+代价=同一效果只能三选一（C4 同族）
                        else c4.FailWith("[FAIL] " + v);
                    }
                }
                // C2/C3：BranchRewardCapViolationOf 逐效果单源
                foreach (var fx in card.Effects ?? new List<CardEffectData>())
                {
                    if (fx == null) continue;
                    var v = EffectComposerScreen.BranchRewardCapViolationOf(fx);
                    if (v == null) continue;
                    branchFx++;
                    string where = $"卡「{(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName)}」";
                    if (v.Contains("有限分支")) c2.FailWith("[FAIL] " + where + v);
                    else if (v.Contains("自由分支")) c3.FailWith("[FAIL] " + where + v);
                    else c3.FailWith("[FAIL] " + where + v);
                }
            }
            c1.Line($"代价减费上限 ≤{EffectComposerScreen.CostDiscountCap}：扫卡 {scanned} 张（挂代价 {withCost} 张）"
                     + (c1.Fail ? "" : " 无超限"));
            c2.Line($"有限分支减费区间 1~{EffectComposerScreen.LimitedBranchFeeCap}（Then 奖励费值口径，逐效果单源 BranchRewardCapViolationOf）"
                     + (c2.Fail ? "" : " 无越界"));
            c3.Line($"自由分支减费区间 1~{EffectComposerScreen.FreeBranchFeeCap}（同上单源）" + (c3.Fail ? "" : " 无越界"));
            c4.Line($"「并列两个效果/分支 + 代价」互斥（同一效果只能三选一）" + (c4.Fail ? "" : " 无违规"));
            c5.Line($"光环类（结界门槛/仪典行）+ 代价互斥" + (c5.Fail ? "" : " 无违规"));
        }

        // ======================================== D. 目标口径（自动构造对局） ========================================

        private static void SectionD()
        {
            // ---------- D1（兼 E4 三态探针）：诱敌 3 敌 LAE 组合枚举 ----------
            var d1 = Of("D1");
            var catalog = CardCatalog.LoadAll().ToList();
            // 2026-11-06 诱敌卡已删出教学池（④同色违规清除）——卡池取不到时从教学效果库合成夹具卡
            var lure = catalog.FirstOrDefault(c => IsName(c, LureName)) ?? LureFixture();

            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 6; i++) deck1.Add(lure);
            for (int i = 0; i < 3; i++) deck1.Add(ByName(catalog, EnemyCName)); // 填充兼蓝地垫材
            var deck2 = new List<CardData>();
            // 12 张：开抽最多 7 + 三敌上场，对方牌库恒余 ≥2（诱敌资敌代价须对方牌库非空）
            for (int i = 0; i < 4; i++) { deck2.Add(ByName(catalog, EnemyAName)); deck2.Add(ByName(catalog, EnemyBName)); deck2.Add(ByName(catalog, EnemyCName)); }
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = false; p2.IsAI = false;
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.FlankNeighbors;
                GameBoard.LinkAuraSystem.Attach(board);
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;

                    foreach (var n in new[] { EnemyAName, EnemyBName, EnemyCName })
                        FindAndMoveByName(zm, p2, n, Zone.Battlefield);

                    var handLure = FindAndMoveByName(zm, p1, LureName, Zone.Hand);
                    var pool = core.ElementPool.GetPool(p1);
                    var landFodder = new List<Card>();
                    landFodder.AddRange(zm.GetCards(p1, Zone.Deck).Where(c => IsName(c, EnemyCName)));
                    landFodder.AddRange(zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, EnemyCName)));
                    landFodder.AddRange(zm.GetCards(p1, Zone.Deck).Where(c => IsName(c, LureName)));
                    landFodder.AddRange(zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, LureName) && c != handLure));
                    int lands = 0;
                    foreach (var c in landFodder) { if (lands >= 2) break; pool.PooledCards.Add(new PooledCard(c, ManaType.Blue)); lands++; }
                    foreach (var c in zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, LureName) && c != handLure).ToList())
                        zm.MoveCard(c, p1, Zone.Hand, Zone.Graveyard);
                    core.ElementPool.AddMana(p1, ManaType.Red, null, 10); // 诱敌 4.5 红 → 实付 5

                    var lae = new LegalActionEnumerator();
                    // 敌 3 → 2 → 1 → 0 分档读数（D1 只判 3 敌档；1/0 档供 E4 探针）
                    for (int scen = 3; scen >= 0; scen--)
                    {
                        while (zm.GetCards(p2, Zone.Battlefield).Count > scen)
                        {
                            var bf = zm.GetCards(p2, Zone.Battlefield);
                            zm.MoveCard(bf[0], p2, Zone.Battlefield, Zone.Graveyard);
                        }
                        lae.Enumerate(core, p1);
                        var rows = lae.Actions.Where(a => a.Type == TideActionType.PlayCard && ReferenceEquals(a.Card, handLure)).ToList();
                        string Describe(TideAction a) => a.Targets == null ? "null"
                            : "n=" + a.Targets.Count + "[" + string.Join("+", a.Targets.Select(NameOf).ToArray()) + "]";
                        d1.Line($"敌={scen} → LAE 行数={rows.Count} :: {string.Join(" ; ", rows.Select(Describe).ToArray())}");

                        if (scen == 3)
                        {
                            if (rows.Count == 3 && rows.All(r => r.Targets != null && r.Targets.Count == 2))
                                d1.Line("3 敌 = 3 行 nTargets=2 ✓（C(3,2) 组合枚举）");
                            else
                                d1.FailWith($"[FAIL] 诱敌+3 敌期望 3 行 nTargets=2，实际 {rows.Count} 行（卡：{LureName}；原因：组合枚举/多目标档回归）");
                        }
                        if (scen == 1)
                        {
                            var e4 = Of("E4");
                            bool probe = TargetDomainService.HasPlayableTargets(core, p1, handLure, 0);
                            if (probe) e4.Line("1 敌 → HasPlayableTargets=true ✓");
                            else e4.FailWith("[FAIL] E4：1 个敌人时 HasPlayableTargets 应为 true，实际 false（卡：诱敌）");
                        }
                        if (scen == 0)
                        {
                            var e4 = Of("E4");
                            bool probe = TargetDomainService.HasPlayableTargets(core, p1, handLure, 0);
                            if (!probe) e4.Line("0 敌 → HasPlayableTargets=false ✓");
                            else e4.FailWith("[FAIL] E4：0 个敌人时 HasPlayableTargets 应为 false，实际 true（卡：诱敌；原因：目标不足可放回归）");
                        }
                    }
                    var e4head = Of("E4");
                    if (!e4head.Lines.Any(l => l.StartsWith("0 敌", StringComparison.Ordinal)))
                        e4head.Line("（探针未覆盖——见 D1 输出）");
                }
                finally { GameBoard.LinkAuraSystem.Detach(); }
            }

            // ---------- D2：预组·22 冻结两个对方单位 → 施放后恰 2 横置 ----------
            var d2 = Of("D2");
            var fz = ByName(catalog, FreezeTwoName);
            var deckA = new List<CardData>();
            for (int i = 0; i < 4; i++) deckA.Add(fz);
            for (int i = 0; i < 3; i++) deckA.Add(ByName(catalog, EnemyCName)); // 填充
            var deckB = new List<CardData>();
            for (int i = 0; i < 4; i++) { deckB.Add(ByName(catalog, EnemyAName)); deckB.Add(ByName(catalog, EnemyBName)); deckB.Add(ByName(catalog, EnemyCName)); }
            core.InitGame(CardLoader.BuildDeck(deckA, 1), CardLoader.BuildDeck(deckB, 1));
            p1 = core.Player1; p2 = core.Player2;
            p1.IsAI = false; p2.IsAI = false;
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.FlankNeighbors;
                GameBoard.LinkAuraSystem.Attach(board);
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;

                    var enemies = new List<Card>();
                    foreach (var n in new[] { EnemyAName, EnemyBName, EnemyCName })
                        enemies.Add(FindAndMoveByName(zm, p2, n, Zone.Battlefield));

                    var hand = FindAndMoveByName(zm, p1, FreezeTwoName, Zone.Hand);
                    foreach (var c in zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, FreezeTwoName) && c != hand).ToList())
                        zm.MoveCard(c, p1, Zone.Hand, Zone.Deck); // 多余副本沉回牌库
                    core.ElementPool.AddMana(p1, ManaType.Blue, null, 10); // 足够蓝（冻结两个=蓝3）

                    if (enemies.Any(e => e == null) || hand == null)
                    {
                        d2.FailWith($"[FAIL] D2 夹具不齐：敌三 {enemies.All(e => e != null)} 手卡 {hand != null}（卡名漂移？）");
                        return;
                    }

                    var ok = GameActions.PlayCard(core, p1, hand, new List<Entity> { enemies[0], enemies[1] }, Zone.Hand, 0);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);

                    int tapped = enemies.Count(e => ((Entity)e).IsTapped());
                    var tapState = string.Join(",", enemies.Select(e => NameOf(e) + "=" + (((Entity)e).IsTapped() ? "横" : "竖")).ToArray());
                    d2.Line($"施放返回={ok}（目标={NameOf(enemies[0])}+{NameOf(enemies[1])}）→ 三敌状态 [{tapState}]，横置数={tapped}");
                    if (ok && tapped == 2)
                        d2.Line("3 敌施放后恰 2 个横置 ✓（Multiple count=2 逐目标结算）");
                    else
                        d2.FailWith($"[FAIL] {FreezeTwoName}（冻结两个对方单位）+3 敌+足够蓝 → 期望横置 2，实际 {tapped}（施放 ok={ok}；原因：多目标结算/冻结横置回归）");
                }
                finally { GameBoard.LinkAuraSystem.Detach(); }
            }
        }

        // ======================================== E. 系统规则抽检（静态） ========================================

        private static void SectionE()
        {
            string root = Path.Combine(Application.dataPath, "..");
            // E1 全体消灭（2026-11-05 B2 硬切删除；2026-11-06 墓碑清理收口）：handler 与枚举成员均已移除
            var e1 = Of("E1");
            try
            {
                string handlers = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "HotUpdate", "CardCore", "Attribute", "Handlers", "ThirdBatchEffectHandlers.cs"));
                string atoms = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "HotUpdate", "CardCore", "Effects", "AtomicEffects.cs"));
                if (handlers.Contains("class DevourAllEnemiesHandler"))
                    e1.FailWith("[FAIL] DevourAllEnemiesHandler 残留（B2 已删除全体原子）");
                else if (atoms.Contains("DevourAllEnemies"))
                    e1.FailWith("[FAIL] AtomicEffectType.DevourAllEnemies 枚举成员残留");
                else
                    e1.Line("全体消灭 handler 与枚举成员均已移除 ✓");
            }
            catch (Exception ex) { e1.FailWith("[FAIL] E1 读取异常：" + ex.Message); }

            // E2 代价栏三条拒绝文案（2026-11-03 补③域单侧）
            var e2 = Of("E2");
            try
            {
                bool hasMethod = typeof(CostDerivationService).GetMethod("PayloadCostViolations", BindingFlags.Public | BindingFlags.Static) != null;
                string src = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "HotUpdate", "CardCore", "Effects", "CostDerivation.cs"));
                bool hasP0 = src.Contains("极性=0");
                bool hasNoTarget = src.Contains("无 {{target}} 参数");
                bool hasSingleSide = src.Contains("域单侧");
                e2.Line($"PayloadCostViolations 存在={hasMethod}；「极性=0」文案={hasP0}；「无 {{target}} 参数」文案={hasNoTarget}；「域单侧」文案={hasSingleSide}");
                if (!hasMethod || !hasP0 || !hasNoTarget || !hasSingleSide)
                    e2.FailWith("[FAIL] 代价栏拒绝路径残缺（极性=0 / 无 {target} / 域单侧 三条违规文案或方法本体缺失）");
            }
            catch (Exception ex) { e2.FailWith("[FAIL] E2 读取异常：" + ex.Message); }

            // E3 SkillValueViolations 存在
            var e3 = Of("E3");
            try
            {
                var mi = typeof(HeroSkillSystem).GetMethod("SkillValueViolations", BindingFlags.Public | BindingFlags.Static);
                string src = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "HotUpdate", "CardCore", "BaseSystems", "HeroSkillSystem.cs"));
                bool rejects = src.Contains("e.value >= 2");
                e3.Line($"HeroSkillSystem.SkillValueViolations 存在={mi != null}；value≥2 拒绝判定={rejects}");
                if (mi == null || !rejects)
                    e3.FailWith("[FAIL] 技能 value=1 校验缺失（SkillValueViolations 不存在或无 value≥2 拒绝）");
            }
            catch (Exception ex) { e3.FailWith("[FAIL] E3 读取异常：" + ex.Message); }
        }

        // ======================================== 辅助 ========================================

        /// <summary>卡宇宙=主表+教学副表（与 RunColorAudit 卡级扫描同口径，只读装载）。</summary>
        private static List<CardData> LoadCardUniverse()
        {
            var cards = new List<CardData>();
            string mainPath = SynergyUI.CardDataPaths.FileIn("Cards.json");
            if (File.Exists(mainPath))
                cards.AddRange(CardLoader.LoadCardsFromText(File.ReadAllText(mainPath)));
            string tutPath = SynergyUI.CardDataPaths.TutorialFileIn("TutorialCards.json");
            if (File.Exists(tutPath))
                cards.AddRange(CardLoader.LoadCardsFromText(File.ReadAllText(tutPath)));
            return cards;
        }

        private static void AdvanceToMainPhase(GameCore core, Player p1)
        {
            int guard = 0;
            while (guard++ < 20 && (core.TurnEngine.CurrentPhase == null || core.TurnEngine.CurrentPhase.Phase != PhaseType.Main))
            {
                var ph = core.TurnEngine.CurrentPhase == null ? PhaseType.Standby : core.TurnEngine.CurrentPhase.Phase;
                if (ph == PhaseType.Standby) GameActions.SkipElementPool(core, core.TurnEngine.TurnPlayer);
                else core.TurnEngine.CheckPhaseTransition();
            }
        }

        private static CardData ByName(List<CardData> catalog, string name)
            => catalog.FirstOrDefault(c => IsName(c, name))
               ?? throw new InvalidOperationException($"卡池找不到「{name}」（卡名变更？验证器夹具须跟进）");

        private static bool IsName(CardData c, string name) => c != null && c.CardName == name;

        private static bool IsName(Card c, string name) => c is IHasName n && n.CardName == name;

        /// <summary>诱敌夹具卡合成（2026-11-06·同 LaeMultiTargetVerifier①）：诱敌卡（C_AC7E3467，
        /// 卡级蓝抽卡代价×红效果=④同色违规）已删出教学池——D1 改从教学效果库取条目合成：效果 88C651B8
        ///（Multiple·count=2·两敌弹回）保留在库专供夹具，断言口径不变；「对方抽1」代价行
        /// 2026-11-08 效果级化改挂该效果 Costs（主动行=法术施放净费账单——对局行为前提等价）。
        /// ToGraph 不回填 header.Steps，手动接 graph.steps。
        /// 效果内容若改动换号，按显示名兜底。</summary>
        private static CardData LureFixture()
        {
            var graphs = EffectLibrarySerializer.LoadAll(tutorial: true);
            var g = graphs.FirstOrDefault(x => x != null && x.id == "88C651B8")
                 ?? graphs.FirstOrDefault(x => x?.header != null
                      && x.header.DisplayName == "将两个对方单位弹回手牌");
            if (g?.header == null)
                throw new InvalidOperationException(
                    "教学效果库缺诱敌夹具条目（id=88C651B8 / 将两个对方单位弹回手牌）——卡已删，效果条目须保留");
            g.header.Steps = g.steps != null && g.steps.Count > 0 ? g.steps : null;
            g.header.Costs = new List<CostEntry>
            {
                new CostEntry
                {
                    CostType = (int)CostType.Payload,
                    payloads = new List<AtomicEffectEntry>
                    {
                        new AtomicEffectEntry { refId = "03c85bc4", kinds = new List<int> { 8 }, value = 1 },
                    },
                },
            };
            return new CardData
            {
                ID = "C_LURE_FIXTURE",
                CardName = LureName,
                Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { g.header },
            };
        }

        private static Card FindAndMoveByName(ZoneManager zm, Player owner, string name, Zone to)
        {
            Card found = zm.GetCards(owner, Zone.Deck).FirstOrDefault(c => IsName(c, name))
                         ?? zm.GetCards(owner, Zone.Hand).FirstOrDefault(c => IsName(c, name));
            if (found == null) return null;
            var from = zm.IsCardInZone(found, owner, Zone.Hand) ? Zone.Hand : Zone.Deck;
            if (from != to) zm.MoveCard(found, owner, from, to);
            return found;
        }

        private static string NameOf(Entity e)
            => e is Player p ? (p.Name ?? (ReferenceEquals(p, GameCore.Instance.Player1) ? "己方角色" : "对方角色"))
             : (e as IHasName)?.CardName ?? "?";
    }
}
