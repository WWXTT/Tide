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
    /// 教学二·关键词课（basics_plus，2026-11-09 建·2026-11-11 升格教学二）配置与引擎回归：
    /// · 结构校验：TutorialConfig4.json 装载（本地 DTO 镜像——不引 UI 侧 TutorialScript/TutorialGuide）、
    ///   38 步 / 10 换场 / 11 段盘面 / completeOnConfirm 收尾、全部引用卡 id 在教学卡表、
    ///   guide 攻击步 attacker/target 落在所属段盘面、aiScript 5 回合的 attacker/target 可解析。
    /// · 引擎断言：11 段逐段重开（TutorialScenarioSeeder 静默注入同口径），按引导步/aiScript 直驱
    ///   GameActions.DeclareAttack/PlayCard/EndTurn（回合间补 TurnEngine.CheckPhaseTransition 泵）。
    ///   断言按 2026-11-09 修订后的设计文本写：段1 连击与 2/8 同归、段2 溢出连锁三杀、
    ///   段5 吸血活/死双演示+系命反击回血、段8 守护吃 8 点溢出、段9 狩猎+吸血组合、
    ///   段10 三来源染毒（连击 2 层/普攻 1 层/法术 1 层）。
    ///   其中「段10 法术伤害来源染毒且回合末发作」按文本断言 AI 角色 29——引擎对效果伤害 source
    ///   的归属若与文本不符会在此显红（不符清单条目，补齐后应转绿）。
    /// 用法：dotnet run --project TideSim -- --keyword-scenario
    /// </summary>
    internal static class KeywordTutorialCheck
    {
        private const int RngSeed = 20261109;
        private const string CourseId = "basics_plus";

        private static int _fail;
        private static GameBoard.BoardState _board;

        // ===== 本地 DTO 镜像（TutorialConfig4 私有 envelope；scenario 块 = 教学 TutorialScenario 同一数据类） =====

        private sealed class ConfigFile { public List<CourseDto> tutorials; }

        private sealed class CourseDto
        {
            public string id;
            public string name;
            public int rngSeed;
            public TutorialScenario scenario;
            public TutorialScenario skipScenario;
            public List<TutorialScenario> phaseScenarios;
            public List<StepDto> guide;
            public List<TurnDto> aiScript;
        }

        private sealed class StepDto
        {
            public string type;
            public string attacker;
            public string target;
            public string card;
            public string reveal;
            public bool completeOnConfirm;
            public bool waitTurnStart;
        }

        private sealed class TurnDto { public int turn; public List<ActionDto> actions; }

        private sealed class ActionDto { public string type; public string card; public string attacker; public string target; public string targets; }

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            CardCatalog.LoadAll();
            string path = FindRepoFile(Path.Combine("Assets", "Configs", "TutorialConfig4.json"));
            if (path == null)
            {
                Console.WriteLine("✗ 找不到 Assets/Configs/TutorialConfig4.json（从运行目录向上回溯无命中）");
                return 1;
            }
            CourseDto course;
            try
            {
                var file = JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(path));
                course = file?.tutorials?.FirstOrDefault(t => t.id == CourseId);
            }
            catch (Exception e)
            {
                Console.WriteLine($"✗ TutorialConfig4.json 解析失败：{e.Message}");
                return 1;
            }
            if (course == null)
            {
                Console.WriteLine("✗ TutorialConfig4.json 没有 basics_plus 条目");
                return 1;
            }

            Console.WriteLine("【关键词课回归】教学二：结构校验 + 11 段引擎断言（basics_plus）");
            ValidateStructure(course);

            var segments = BuildSegments(course);
            // 段序：0 先攻/连击 1 碾压/溅射 2 缴械/警戒 3 隐密/帷幕 4 吸血/系命 5 剧毒/不灭
            //       6 再生 7 守护 8 狩猎 9 病原体 10 绝缘体
            RunSeg0(segments[0]);
            RunSeg1(segments[1]);
            RunSeg2(segments[2]);
            RunSeg3(segments[3]);
            RunSeg4(segments[4]);
            RunSeg5(segments[5]);
            RunSeg6(segments[6]);
            RunSeg7(segments[7]);
            RunSeg8(segments[8]);
            RunSeg9(segments[9]);
            RunSeg10(segments[10]);

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "关键词课回归：通过" : $"关键词课回归：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        // ======================================== 结构校验 ========================================

        private static void ValidateStructure(CourseDto course)
        {
            Console.WriteLine("  —— 结构校验");
            Assert(course.scenario != null && course.skipScenario != null, "scenario/skipScenario 在位");
            Assert(course.phaseScenarios != null && course.phaseScenarios.Count == 9, $"phaseScenarios=9（实际 {course.phaseScenarios?.Count ?? -1}）");
            Assert(course.guide != null && course.guide.Count == 44, $"guide 44 步（实际 {course.guide?.Count ?? -1}）");
            int midCount = course.guide.Count(s => s?.reveal == "midTransition");
            Assert(midCount == 10, $"midTransition 换场步 10 个（实际 {midCount}）");
            var last = course.guide.LastOrDefault();
            Assert(last != null && last.type == "intro" && last.completeOnConfirm, "收尾步=intro+completeOnConfirm");
            Assert(course.guide.Count(s => s?.type == "end" && s.waitTurnStart) == 8, "end+waitTurnStart 8 步（段5/7/8/9/10×3）");
            Assert(course.aiScript != null && course.aiScript.Select(t => t.turn).OrderBy(v => v).SequenceEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }), "aiScript 回合 1..8");

            // 引用卡 id 全在教学卡表
            var segments = BuildSegments(course);
            var referenced = new HashSet<string>();
            foreach (var sc in segments)
            {
                CollectIds(sc.player, referenced);
                CollectIds(sc.ai, referenced);
            }
            foreach (var s in course.guide)
            {
                if (!string.IsNullOrEmpty(s?.attacker)) referenced.Add(s.attacker);
                if (!string.IsNullOrEmpty(s?.target) && s.target != "hero") referenced.Add(s.target);
                if (!string.IsNullOrEmpty(s?.card)) referenced.Add(s.card);
            }
            foreach (var t in course.aiScript ?? new List<TurnDto>())
                foreach (var a in t.actions ?? new List<ActionDto>())
                {
                    if (!string.IsNullOrEmpty(a?.card)) referenced.Add(a.card);
                    if (!string.IsNullOrEmpty(a?.attacker)) referenced.Add(a.attacker);
                    // wallet 的 target=颜色名（Red/Green/...），非卡 id——跳过
                    if (!string.IsNullOrEmpty(a?.target) && a.target != "hero" && a.type != "wallet") referenced.Add(a.target);
                    if (!string.IsNullOrEmpty(a?.targets)) referenced.Add(a.targets);
                }
            var missing = referenced.Where(id => CardCatalog.GetById(id) == null).ToList();
            Assert(missing.Count == 0, $"全部 {referenced.Count} 个引用卡 id 在教学卡表（缺失：{string.Join(",", missing)}）");

            // guide 攻击步：attacker∈所属段 P1 units，target∈所属段 P2 units 或 hero
            int bad = 0;
            for (int i = 0; i < course.guide.Count; i++)
            {
                var s = course.guide[i];
                if (s?.type != "attack") continue;
                int seg = SegmentOfStep(course, i);
                if (seg < 0 || seg >= segments.Count) { bad++; continue; }
                var p1Ids = segments[seg].player.units.Select(u => u.card).ToHashSet();
                var p2Ids = segments[seg].ai.units.Select(u => u.card).ToHashSet();
                if (!p1Ids.Contains(s.attacker)) bad++;
                if (!(s.target == "hero" || p2Ids.Contains(s.target))) bad++;
            }
            Assert(bad == 0, $"guide 攻击步 attacker/target 全部落段（异常 {bad} 处）");

            // aiScript：回合→段映射（turn1→段4 吸血/系命、turn2→段6 再生、turn3→段7 守护、turn4→段8 狩猎、turn5→段9 病原体、turn6/7/8→段10 绝缘体）
            var turnSeg = new Dictionary<int, int> { { 1, 4 }, { 2, 6 }, { 3, 7 }, { 4, 8 }, { 5, 9 }, { 6, 10 }, { 7, 10 }, { 8, 10 } };
            int badAi = 0;
            foreach (var t in course.aiScript)
            {
                if (!turnSeg.TryGetValue(t.turn, out var seg)) { badAi++; continue; }
                var aiIds = segments[seg].ai.units.Select(u => u.card).ToHashSet();
                var p1Ids = segments[seg].player.units.Select(u => u.card).ToHashSet();
                var p1Hand = new HashSet<string>(segments[seg].player.hand ?? new List<string>());
                foreach (var a in t.actions ?? new List<ActionDto>())
                {
                    if (a?.type == "attack")
                    {
                        if (!aiIds.Contains(a.attacker)) badAi++;
                        if (!(a.target == "hero" || p1Ids.Contains(a.target))) badAi++;
                    }
                    else if (a?.type == "play")
                    {
                        if (!segments[seg].ai.hand.Contains(a.card)) badAi++;
                        // play 目标可为对方场上单位（含玩家先打出的结界=原手牌 id）
                        if (!string.IsNullOrEmpty(a.targets) && !(p1Ids.Contains(a.targets) || p1Hand.Contains(a.targets))) badAi++;
                    }
                    else if (a?.type == "wallet" || a?.type == "end") { /* target=颜色名/mode=数量，不校验卡 id */ }
                }
            }
            Assert(badAi == 0, $"aiScript attacker/target/play 全部可解析（异常 {badAi} 处）");
        }

        private static void CollectIds(TutorialSideState side, HashSet<string> into)
        {
            foreach (var u in side?.units ?? new List<TutorialUnitSpec>()) into.Add(u.card);
            foreach (var l in side?.lands ?? new List<TutorialLandSpec>()) into.Add(l.card);
            foreach (var id in side?.hand ?? new List<string>()) into.Add(id);
            foreach (var id in side?.deck ?? new List<string>()) into.Add(id);
            foreach (var id in side?.grave ?? new List<string>()) into.Add(id);
        }

        private static List<TutorialScenario> BuildSegments(CourseDto course)
        {
            var list = new List<TutorialScenario> { course.scenario, course.skipScenario };
            list.AddRange(course.phaseScenarios ?? new List<TutorialScenario>());
            return list;
        }

        private static int SegmentOfStep(CourseDto course, int stepIndex)
        {
            int seg = 0;
            for (int i = 0; i <= stepIndex && i < course.guide.Count; i++)
                if (course.guide[i]?.reveal == "midTransition") seg++;
            return seg;
        }

        // ======================================== 段引擎驱动 ========================================

        private static GameCore StartSegment(TutorialScenario sc)
        {
            var core = GameCore.Instance;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            var deck1 = TutorialScenario.DeckIdsOf(sc.player).Select(CardCatalog.GetById).ToList();
            var deck2 = TutorialScenario.DeckIdsOf(sc.ai).Select(CardCatalog.GetById).ToList();
            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;
            AttachBoard(core);
            ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;
            TutorialScenarioSeeder.Apply(core, sc, _board);
            core.StartGame();
            return core;
        }

        private static void EndSegment(GameCore core)
        {
            AiStrategyRegistry.Clear();
            ResponseWindowService.AiResponder = null;
            TargetSelectionService.AiOrder = null;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
            DetachBoard();
        }

        private static Card OnField(GameCore core, Player p, string id)
            => (core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                .FirstOrDefault(c => c.ID == id);

        /// <summary>死亡判据：离场或死亡标志（回合末毒素死亡走 SBA 收尸，区域搬移可能迟一拍）。</summary>
        private static bool IsDead(GameCore core, Player p, string id)
        {
            var c = OnField(core, p, id);
            return c == null || !c.IsAlive;
        }

        private static void DumpField(GameCore core, Player p, string label)
        {
            var cards = core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>();
            Console.WriteLine($"    [{label}] " + string.Join(" | ", cards.Select(c =>
                $"{c.ID} P{c.GetPower()} L{c.GetLife()} tap={c.IsTapped()} alive={c.IsAlive} kw=[{string.Join(",", c._keywords)}] counters=[{string.Join(",", c._counters.Select(kv => kv.Key + ":" + kv.Value))}]")));
        }

        private static bool Attack(GameCore core, Player p, string attackerId, Entity target)
        {
            var attacker = OnField(core, p, attackerId);
            if (attacker == null || target == null) { Console.WriteLine($"    [attack] 攻击者/目标缺失：{attackerId}"); return false; }
            if (!GameActions.DeclareAttack(core, p, attacker, target, out var reason))
            {
                Console.WriteLine($"    [attack] {attackerId} 被拒：{reason}");
                return false;
            }
            GameActions.DrainStack(core);
            return true;
        }

        private static bool Cast(GameCore core, Player p, string cardId, List<Entity> targets)
        {
            var card = (core.ZoneManager.GetCards(p, Zone.Hand) ?? new List<Card>())
                .FirstOrDefault(c => c.ID == cardId);
            if (card == null) { Console.WriteLine($"    [cast] 手牌无 {cardId}"); return false; }
            GameActions.DrainStack(core);
            if (!GameActions.PlayCard(core, p, card, targets, Zone.Hand, 0, out var reason))
            {
                Console.WriteLine($"    [cast] {cardId} 被拒：{reason}");
                return false;
            }
            GameActions.DrainStack(core);
            return true;
        }

        private static void Pump(GameCore core) => core.TurnEngine.CheckPhaseTransition();

        // ---------- 段0：先攻/连击 ----------

        private static void RunSeg0(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段1 先攻/连击（4 只 4/2 对 2/4×2 + 2/8×2）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                Assert(Attack(core, p1, "C_8E45017F", OnField(core, p2, "C_FC5BC585")), "先攻甲→2/4甲 宣言成功");
                Assert(Attack(core, p1, "C_732BCF71", OnField(core, p2, "C_6DBC9CDC")), "连击甲→2/4乙 宣言成功");
                Assert(Attack(core, p1, "C_271AA1F8", OnField(core, p2, "C_036D0A6A")), "先攻乙→2/8甲 宣言成功");
                Assert(Attack(core, p1, "C_082B62DB", OnField(core, p2, "C_2C6CAC92")), "连击乙→2/8乙 宣言成功");

                Assert(OnField(core, p1, "C_8E45017F") != null, "先攻甲一步打死 2/4：无伤存活");
                Assert(OnField(core, p2, "C_FC5BC585") == null, "2/4甲 已死");
                Assert(OnField(core, p1, "C_732BCF71") != null, "连击甲首段打死 2/4：无伤存活");
                Assert(OnField(core, p2, "C_6DBC9CDC") == null, "2/4乙 已死");
                Assert(OnField(core, p1, "C_271AA1F8") == null, "先攻乙打 2/8：被 2 点反击打死");
                var t28a = OnField(core, p2, "C_036D0A6A");
                Assert(t28a != null && t28a.GetLife() == 4, $"2/8甲 只挨 4 点（剩 {t28a?.GetLife() ?? -1}）");
                Assert(OnField(core, p1, "C_082B62DB") == null, "连击乙两段打死 2/8：死前反击锁定，同归于尽");
                Assert(OnField(core, p2, "C_2C6CAC92") == null, "2/8乙 已死（连击 8 点全额）");
            }
            finally { EndSegment(core); }
        }

        // ---------- 段1：碾压/溅射 ----------

        private static void RunSeg1(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段2 碾压/溅射（结界夹排 + 三只分散 2/2）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                Action<CardCore.DamageEvent> onDmg = e =>
                    Console.WriteLine($"    [伤害] {(e.Source as Card)?.ID ?? (e.Source as Player)?.Name ?? "null"} → {(e.Target as Card)?.ID ?? (e.Target as Player)?.Name} {e.Amount}");
                EventManager.Instance.Subscribe(onDmg);
                Assert(Attack(core, p1, "C_3AFBFB85", OnField(core, p2, "C_9914CF8A")), "碾压→结界 宣言成功");
                Assert(OnField(core, p2, "C_9914CF8A") == null, "6 耐结界被 6 点打碎");
                Assert(OnField(core, p2, "C_0FDF4A1D") == null && OnField(core, p2, "C_FF042960") == null, "碾压邻格全额：左右两只 2/6 一起死");
                Assert(OnField(core, p1, "C_3AFBFB85") != null, "结界反击力 0：碾压方无伤存活");

                Assert(Attack(core, p1, "C_B9F338E2", OnField(core, p2, "C_AC584F8A")), "溅射→2/2甲 宣言成功");
                Assert(OnField(core, p2, "C_AC584F8A") == null, "溅射主目标死");
                // 注：本断言与上一条碾压断言耦合——主目标结界被直毁即时移墓致邻格溅射落空（不符清单①），
                // 残存的 2/6 混入溅射随机池吃掉余量；碾压侧修复后本池只剩两只 2/2、连锁全灭应转绿。
                Assert(OnField(core, p2, "C_7E1B7AA1") == null && OnField(core, p2, "C_17BD0D3E") == null,
                    "溢出 4 连锁：另两只 2/2 全灭（6-2=4 溅死一只、余 2 再溅死一只）");
                Assert(OnField(core, p1, "C_B9F338E2") != null, "溅射方存活");
                EventManager.Instance.Unsubscribe(onDmg);
            }
            finally { EndSegment(core); }
        }

        // ---------- 段2：缴械/警戒 ----------

        private static void RunSeg2(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段3 缴械/警戒（三普通送死 + 三缴械压制）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                Assert(Attack(core, p1, "C_BA6BE8EC", OnField(core, p2, "C_7348D471")), "普通甲→横置警戒 宣言成功");
                Assert(Attack(core, p1, "C_5DBE70BB", OnField(core, p2, "C_64AA066B")), "普通乙→先攻巨兽 宣言成功");
                Assert(Attack(core, p1, "C_0EDE312A", OnField(core, p2, "C_AC582815")), "普通丙→连击巨兽 宣言成功");
                Assert(OnField(core, p1, "C_BA6BE8EC") == null, "普通甲被横置警戒反击打死（警戒横置也能反）");
                var vig = OnField(core, p2, "C_7348D471");
                Assert(vig != null && vig.GetLife() == 1, $"警戒挨 3（4→1，实际 {vig?.GetLife() ?? -1}）");
                Assert(OnField(core, p1, "C_5DBE70BB") == null, "普通乙被先攻反击先落地打死（0 输出）");
                var fsBig = OnField(core, p2, "C_64AA066B");
                Assert(fsBig != null && fsBig.GetLife() == 8, $"先攻巨兽零损（8，实际 {fsBig?.GetLife() ?? -1}）");
                Assert(OnField(core, p1, "C_0EDE312A") == null, "普通丙被连击两段反击打死");
                var dsBig = OnField(core, p2, "C_AC582815");
                Assert(dsBig != null && dsBig.GetLife() == 5, $"连击巨兽挨 3（8→5，实际 {dsBig?.GetLife() ?? -1}）");

                Assert(Attack(core, p1, "C_202A366E", OnField(core, p2, "C_7348D471")), "缴械甲→警戒 宣言成功");
                Assert(Attack(core, p1, "C_5654ABA8", OnField(core, p2, "C_64AA066B")), "缴械乙→先攻巨兽 宣言成功");
                Assert(Attack(core, p1, "C_AB547E9C", OnField(core, p2, "C_AC582815")), "缴械丙→连击巨兽 宣言成功");
                Assert(OnField(core, p2, "C_7348D471") == null, "缴械甲补刀残血警戒");
                Assert(OnField(core, p1, "C_202A366E") != null && OnField(core, p1, "C_202A366E").GetLife() == 3, "缴械甲无伤（反击被压制）");
                Assert(OnField(core, p1, "C_5654ABA8") != null && OnField(core, p1, "C_5654ABA8").GetLife() == 3, "缴械乙无伤（先攻反击被压制）");
                Assert(OnField(core, p1, "C_AB547E9C") != null && OnField(core, p1, "C_AB547E9C").GetLife() == 3, "缴械丙无伤（连击反击被压制）");
                var fs2 = OnField(core, p2, "C_64AA066B");
                var ds2 = OnField(core, p2, "C_AC582815");
                Assert(fs2 != null && fs2.GetLife() == 5 && ds2 != null && ds2.GetLife() == 2, "两只巨兽各再挨 3（8→5 / 5→2）");
            }
            finally { EndSegment(core); }
        }

        // ---------- 段3：隐密/帷幕 ----------

        private static void RunSeg3(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段4 隐密/帷幕（两发消灭的候选过滤）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                var hidden = OnField(core, p2, "C_3D2CBFE6");
                var taunt = OnField(core, p2, "C_98135A73");
                var big = OnField(core, p2, "C_8C47525E");
                Assert(hidden != null && taunt != null && big != null, "三只靶子在位");
                if (hidden == null || taunt == null || big == null) return;

                var ctx = new EffectExecutionContext { Source = p1, Controller = p1, ZoneManager = core.ZoneManager };
                var pool = new List<Entity> { hidden, taunt, big };
                var filtered = TargetResolver.ApplyTauntRestriction(TargetResolver.ExcludeUnselectable(pool, p1), ctx);
                Assert(filtered.Count == 1 && ReferenceEquals(filtered[0], taunt), "第一发候选=仅帷幕（隐密不可选+帷幕收窄全场）");

                Assert(Cast(core, p1, "C_C635DB5D", new List<Entity> { taunt }), "教学·14 消灭帷幕 成功");
                Assert(OnField(core, p2, "C_98135A73") == null, "帷幕替 8/8 挡刀死");

                var pool2 = new List<Entity> { hidden, big };
                var filtered2 = TargetResolver.ApplyTauntRestriction(TargetResolver.ExcludeUnselectable(pool2, p1), ctx);
                Assert(filtered2.Count == 1 && ReferenceEquals(filtered2[0], big), "第二发候选=仅 8/8（帷幕死后暴露，隐密仍不可选）");

                Assert(Cast(core, p1, "C_DE95C8CC", new List<Entity> { big }), "消灭乙点 8/8 成功");
                Assert(OnField(core, p2, "C_8C47525E") == null, "8/8 被消灭");
                Assert(OnField(core, p2, "C_3D2CBFE6") != null, "隐密全程不可点、存活到最后");
            }
            finally { EndSegment(core); }
        }

        // ---------- 段4：吸血/系命 ----------

        private static void RunSeg4(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段5 吸血/系命（活/死双演示 + 对手回合反击回血）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                var lsA = OnField(core, p1, "C_E8B8EE22");
                var llA = OnField(core, p1, "C_E5D9D557");
                Assert(lsA != null && lsA.GetLife() == 1 && llA != null && llA.GetLife() == 3,
                    $"currentLife 播种生效（吸血甲 1 血，实际 {lsA?.GetLife() ?? -1}；系命甲满 3）");

                Assert(Attack(core, p1, "C_E8B8EE22", OnField(core, p2, "C_AC584F8A")), "吸血甲→2/2甲 宣言成功");
                var lsA2 = OnField(core, p1, "C_E8B8EE22");
                Assert(lsA2 != null && lsA2.GetLife() == 1, $"吸血甲先回 3 再挨 2 反击、1 血存活（实际 {lsA2?.GetLife() ?? -1}）");
                Assert(OnField(core, p2, "C_AC584F8A") == null, "2/2甲 被打死");

                Assert(Attack(core, p1, "C_9460C612", OnField(core, p2, "C_BA6BE8EC")), "吸血乙→3/3甲 宣言成功");
                Assert(OnField(core, p1, "C_9460C612") == null, "吸血乙回满 3 仍被 3 点反击打死（等量互换救不回）");

                Assert(Attack(core, p1, "C_E5D9D557", OnField(core, p2, "C_5DBE70BB")), "系命甲→3/3乙 宣言成功");
                Assert(p1.Life == 23, $"系命甲主动攻击：角色 20→23（实际 {p1.Life}）");
                Assert(OnField(core, p1, "C_E5D9D557") == null, "系命甲正常被反击打死（回血照算）");

                GameActions.EndTurn(core, p1);
                Pump(core);
                Assert(Attack(core, p2, "C_7E1B7AA1", OnField(core, p1, "C_6CE1BAAF")), "AI 2/2乙→系命乙 宣言成功");
                GameActions.EndTurn(core, p2);
                var llB = OnField(core, p1, "C_6CE1BAAF");
                Assert(llB != null && llB.GetLife() == 1, $"系命乙挨 2 反击 3 杀掉 2/2、自身 1 血存活（实际 {llB?.GetLife() ?? -1}）");
                Assert(p1.Life == 26, $"系命反击回血：角色 23→26（实际 {p1.Life}）");
                Assert(OnField(core, p2, "C_7E1B7AA1") == null, "2/2乙 被反击打死");
            }
            finally { EndSegment(core); }
        }

        // ---------- 段5：剧毒/不灭 ----------

        private static void RunSeg5(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段6 剧毒/不灭（无视生命的消灭 vs 拦消灭）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                Assert(Attack(core, p1, "C_DCDC05A7", OnField(core, p2, "C_8C47525E")), "剧毒甲→8/8白板 宣言成功");
                Assert(OnField(core, p2, "C_8C47525E") == null, "剧毒伤害结算后直接消灭 8/8（不看生命）");
                Assert(OnField(core, p1, "C_DCDC05A7") == null, "剧毒甲被 8 点反击打死（以小换大）");

                Assert(Attack(core, p1, "C_52A944EE", OnField(core, p2, "C_750FFED3")), "剧毒乙→8/8不灭 宣言成功");
                var indes = OnField(core, p2, "C_750FFED3");
                Assert(indes != null && indes.GetLife() == 6, $"不灭拦住消灭：只掉 2（8→6，实际 {indes?.GetLife() ?? -1}）");
                Assert(OnField(core, p1, "C_52A944EE") == null, "剧毒乙被反击打死");
            }
            finally { EndSegment(core); }
        }

        // ---------- 段6：再生 ----------

        private static void RunSeg6(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段7 再生（准备阶段回满）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                var regen = OnField(core, p1, "C_629A3AF5");
                Assert(regen != null && regen.GetLife() == 1, $"再生带伤播种 1 血（实际 {regen?.GetLife() ?? -1}）");
                GameActions.EndTurn(core, p1);
                Pump(core); // AI 回合（剧本=空过）
                GameActions.EndTurn(core, p2);
                Pump(core); // 己方准备阶段：回满
                var regen2 = OnField(core, p1, "C_629A3AF5");
                Assert(regen2 != null && regen2.GetLife() == 5, $"下个己方准备阶段回满（1→5，实际 {regen2?.GetLife() ?? -1}）");
            }
            finally { EndSegment(core); }
        }

        // ---------- 段7：守护 ----------

        private static void RunSeg7(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段8 守护（溢出伤害转移）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                GameActions.EndTurn(core, p1);
                Pump(core); // AI 回合主阶段
                Assert(Attack(core, p2, "C_8C47525E", p1), "AI 8/8→角色 宣言成功");
                GameActions.EndTurn(core, p2);
                Assert(OnField(core, p1, "C_E73AEE6F") == null, "守护 2/7 吃下全额 8 点（含超出生命的溢出）倒下");
                Assert(p1.Life == 30, $"角色分毫无伤（30，实际 {p1.Life}）——伤害连溢出部分都改由守护承受");
            }
            finally { EndSegment(core); }
        }

        // ---------- 段8：狩猎 ----------

        private static void RunSeg8(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段9 狩猎 + 关键词组合（回合末自动出击）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                GameActions.EndTurn(core, p1); // 回合末：两只狩猎自动出击（AI 空场=只打角色）
                Assert(p2.Life == 22, $"回合末狩猎双出击：AI 30→22（4+4，实际 {p2.Life}）");
                var huntA = OnField(core, p1, "C_030E8991");
                var huntB = OnField(core, p1, "C_59A3BB05");
                Assert(huntA != null && !huntA.IsTapped() && huntB != null && !huntB.IsTapped(), "狩猎攻击不吃横置代价（两只仍未横置）");
                Assert(huntB != null && huntB.GetLife() == 2, $"狩猎+吸血组合：1 血出击咬 4 回满 2（实际 {huntB?.GetLife() ?? -1}）");
            }
            finally { EndSegment(core); }
        }

        // ---------- 段9：病原体 ----------

        private static void RunSeg9(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段10 病原体（普攻 1 层/连击 2 层/主动效果 1 层）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                GameActions.EndTurn(core, p1);
                Pump(core); // AI 回合主阶段

                Assert(Attack(core, p2, "C_DD519F03", OnField(core, p1, "C_E3CADDFD")), "AI 连击小兵→病原体乙 宣言成功");
                var rat = OnField(core, p1, "C_E3CADDFD");
                Assert(rat != null && rat.GetLife() == 2, $"病原体乙挨两段各 1（4→2，实际 {rat?.GetLife() ?? -1}）");
                var soldier = OnField(core, p2, "C_DD519F03");
                Assert(soldier != null && soldier.GetLife() == 1 && soldier.GetCounterCount(CounterRules.ToxinCounter) == 2,
                    $"连击两下=2 层毒素（小兵 1 血剩 1、层 {soldier?.GetCounterCount(CounterRules.ToxinCounter) ?? -1}）");

                Assert(Attack(core, p2, "C_BA6BE8EC", OnField(core, p1, "C_6454E497")), "AI 白板3/3→病原体甲 宣言成功");
                var plain = OnField(core, p2, "C_BA6BE8EC");
                Assert(plain != null && plain.GetLife() == 1 && plain.GetCounterCount(CounterRules.ToxinCounter) == 1,
                    $"普通攻击=1 层毒素（3/3 挨反击 2 剩 1、层 {plain?.GetCounterCount(CounterRules.ToxinCounter) ?? -1}）");

                core.ElementPool.AddMana(p2, ManaType.Red, null, 3); // aiScript turn5 wallet 重灌（temp 钱包回合末清空，施放当回合须重灌）
                Assert(Cast(core, p2, "C_2730BF8F", new List<Entity> { OnField(core, p1, "C_6454E497") }), "AI 一点法术→病原体甲 施放成功");
                Assert(OnField(core, p1, "C_6454E497") == null, "病原体甲被法术点死");

                GameActions.EndTurn(core, p2); // AI 回合末：毒素发作
                GameActions.DrainStack(core);  // 回合末死亡走 SBA——排干收尸再断言
                Assert(IsDead(core, p2, "C_DD519F03"), "小兵 2 层毒发作掉 2：死");
                Assert(IsDead(core, p2, "C_BA6BE8EC"), "3/3 1 层毒发作掉 1：死");;
                // 文本预期：主动效果的伤害来源也染 1 层、回合末发作——若引擎把 source 归给施放者（AI 角色）
                // 则此处 29；若归给法术卡（已进墓地、不在毒素扫描域）则 30——显红即为不符清单条目。
                Assert(p2.Life == 29, $"法术伤害来源染毒发作：AI 角色 30→29（实际 {p2.Life}；不符则 source 归属与文本不一致）");
            }
            finally { EndSegment(core); }
        }

        // ---------- 段10：绝缘体（三改：法术赋予+光环授予+驱散引出赋予/印刷之别） ----------

        private static void RunSeg10(TutorialScenario sc)
        {
            Console.WriteLine("  —— 段11 绝缘体（印刷死层 + 法术/光环双通道授予 + 驱散拆通道）");
            var core = StartSegment(sc);
            try
            {
                var p1 = core.Player1; var p2 = core.Player2;
                var hunter = OnField(core, p1, "C_030E8991");   // 狩猎（法术赋予通道）
                var vamp = OnField(core, p1, "C_59A3BB05");     // 狩猎吸血（光环授予通道）
                var innate = OnField(core, p1, "C_7FD01D48");   // 印刷绝缘体本体
                Assert(hunter != null && vamp != null && innate != null, "三只己方生物在位（狩猎/狩猎吸血/印刷绝缘体）");

                // —— AI turn6：沉睡/失效四连（本体=印刷死层） ——
                GameActions.EndTurn(core, p1); // 段首回合末：两只狩猎播种横置→不狩猎（AI 保持 30）
                Console.WriteLine($"    [段首回合末AI生命] {p2.Life}");
                Assert(p2.Life == 30, "段首回合末无狩猎开火（两只播种横置）");
                Pump(core);
                core.ElementPool.AddMana(p2, ManaType.Blue, null, 15);
                Assert(Cast(core, p2, "C_BA2BFFAD", new List<Entity> { OnField(core, p1, "C_030E8991") }), "AI 沉睡→狩猎 成功");
                Assert(Cast(core, p2, "C_3AC147C9", new List<Entity> { OnField(core, p1, "C_59A3BB05") }), "AI 失效→狩猎吸血 成功");
                Assert(Cast(core, p2, "C_63FAF37B", new List<Entity> { OnField(core, p1, "C_7FD01D48") }), "AI 沉睡→绝缘体本体 成功");
                Assert(Cast(core, p2, "C_C55C98E2", new List<Entity> { OnField(core, p1, "C_7FD01D48") }), "AI 失效→绝缘体本体 成功");
                Assert(hunter.GetCounterCount(KeywordRules.SleepCounter) == 1, "狩猎挂 1 层沉睡（层照落）");
                Assert(vamp.GetCounterCount(CounterRules.NullifyCounter) == 1, "狩猎吸血挂 1 层失效");
                Assert(SuppressRules.IsAutoSuppressed(hunter), "未绝缘：沉睡压制生效（狩猎关键词被压）");
                Assert(innate.GetCounterCount(KeywordRules.SleepCounter) == 1 && innate.GetCounterCount(CounterRules.NullifyCounter) == 1,
                    "本体印刷绝缘：沉睡/失效层照落账（1+1）");
                Assert(!SuppressRules.IsAutoSuppressed(innate), "本体印刷绝缘：层是死层——压制无效");
                Assert(innate.GetEffectiveCounterCount(KeywordRules.SleepCounter) == 0, "本体效果口径读数=0");

                // —— 玩家回合 N+1：法术赋予（狩猎）+ 光环授予（狩猎吸血，地牌自动付费） ——
                GameActions.EndTurn(core, p2);
                Pump(core); // N+1 准备阶段：狩猎被沉睡拦住重置（未绝缘）；狩猎吸血正常站起
                Assert(OnField(core, p1, "C_030E8991").IsTapped(), "未绝缘：沉睡的横置生效（拦住重置）");
                Assert(!OnField(core, p1, "C_59A3BB05").IsTapped(), "失效不拦重置：狩猎吸血照常站起");
                Assert(Cast(core, p1, "C_01835F2B", new List<Entity> { OnField(core, p1, "C_030E8991") }), "法术【绝缘赋予】→狩猎 施放成功（costPin 0费）");
                Assert(hunter.HasKeyword(KeywordRules.Insulator), "法术赋予：狩猎获得绝缘体（Temp 轨）");
                Assert(!SuppressRules.IsAutoSuppressed(hunter), "法术通道：沉睡层立即变死层（压制豁免）");
                Assert(Cast(core, p1, "C_12705A5A", null), "结界【绝缘光环】打出成功（灰6蓝2 由地牌自动横置支付；自动落位首空格=仅邻狩猎吸血）");
                DumpField(core, p1, "光环后P1");
                Assert(GameBoard.LinkAuraSystem.HasAuraKeyword(vamp, KeywordRules.Insulator),
                    "光环授予：狩猎吸血获得绝缘体【表位7拉黑未解除则红——装载期光环条目被剥】");
                GameActions.EndTurn(core, p1); // 回合末：光环授予的绝缘体不压沉睡——狩猎吸血仍被压，不开火
                Console.WriteLine($"    [AI生命] {p2.Life}");
                Assert(p2.Life == 30, $"光环授予的绝缘体压不住沉睡：AI 留 30（实际 {p2.Life}）【2026-11-09 定案：IsActive 只认本体轨】");
                Assert(hunter.GetCounterCount(KeywordRules.SleepCounter) == 0, $"层的倒数照常结算：狩猎沉睡 1→0（实际 {hunter.GetCounterCount(KeywordRules.SleepCounter)}）");
                Assert(vamp.GetCounterCount(CounterRules.NullifyCounter) == 1,
                    $"失效不自动倒数（非衰退档，账面保持——驱散才清；实际 {vamp.GetCounterCount(CounterRules.NullifyCounter)}）");

                // —— AI turn7：再沉睡×2（死层不横置不压制） ——
                Pump(core);
                core.ElementPool.AddMana(p2, ManaType.Blue, null, 8);
                Assert(Cast(core, p2, "C_0BFA4FD0", new List<Entity> { OnField(core, p1, "C_030E8991") }), "AI 再沉睡→狩猎 成功");
                Assert(Cast(core, p2, "C_04425248", new List<Entity> { OnField(core, p1, "C_59A3BB05") }), "AI 再沉睡→狩猎吸血 成功");
                DumpField(core, p1, "回合7后P1");
                Assert(hunter.GetCounterCount(KeywordRules.SleepCounter) == 1 && vamp.GetCounterCount(KeywordRules.SleepCounter) == 1,
                    "绝缘下沉睡层照挂（账面 1+1）");
                GameActions.EndTurn(core, p2);
                Pump(core); // 玩家回合 N+2 准备阶段：沉睡的横置不生效——照常重置
                DumpField(core, p1, "N+2准备后P1");
                Assert(!OnField(core, p1, "C_030E8991").IsTapped(), "沉睡的横置不生效——狩猎照常重置（法术通道绝缘=死层）");
                Assert(!OnField(core, p1, "C_59A3BB05").IsTapped(), "狩猎吸血同样照常重置【光环通道缺口则被沉睡压住保持横置】");
                Assert(OnField(core, p1, "C_030E8991").HasKeyword(KeywordRules.Hunt), "死层不压关键词——狩猎照常可读");

                // —— AI turn8：驱散/无效拆通道（赋予 vs 印刷） ——
                GameActions.EndTurn(core, p1);
                Pump(core);
                core.ElementPool.AddMana(p2, ManaType.Blue, null, 9);
                core.ElementPool.AddMana(p2, ManaType.Red, null, 9);
                Assert(Cast(core, p2, "C_483E94BC", new List<Entity> { OnField(core, p1, "C_030E8991") }), "AI 驱散→狩猎（拆法术赋予）");
                Assert(!hunter.HasKeyword(KeywordRules.Insulator), "法术赋予的绝缘体被驱散整个抹掉（Temp 轨）");
                Assert(hunter.GetCounterCount(KeywordRules.SleepCounter) == 0, "驱散顺带清光狩猎身上的指示物");
                var auraSrc = OnField(core, p1, "C_12705A5A");
                Assert(Cast(core, p2, "C_2D514294", auraSrc != null ? new List<Entity> { auraSrc } : null),
                    "AI 无效→绝缘光环来源（光环结界）【失效原子域=生物，指结界被拒则红】");
                Assert(!GameBoard.LinkAuraSystem.HasAuraKeyword(vamp, KeywordRules.Insulator),
                    "光环来源被无效→光环赋予的绝缘体失效【需光环受压制新口径】");
                Assert(Cast(core, p2, "C_4082D33D", new List<Entity> { OnField(core, p1, "C_7FD01D48") }), "AI 驱散→绝缘体本体（拆印刷）");
                var innate2 = OnField(core, p1, "C_7FD01D48");
                Assert(innate2 != null && innate2.HasKeyword(KeywordRules.Insulator), "印刷的绝缘体驱不散（Printed 轨保留）");
                Assert(innate2.GetCounterCount(KeywordRules.SleepCounter) == 0 && innate2.GetCounterCount(CounterRules.NullifyCounter) == 0,
                    "驱散只清了本体身上的其他指示物（沉睡/失效全清）");
            }
            finally { EndSegment(core); }
        }

        // ======================================== 装载/棋盘（与 TutorialScenarioCheck 同口径） ========================================

        private static string FindRepoFile(string relative)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var p = Path.Combine(dir.FullName, relative);
                if (File.Exists(p)) return p;
                dir = dir.Parent;
            }
            return null;
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
