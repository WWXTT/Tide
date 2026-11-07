using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CardCore;
using CardCore.AI.NeuralEnv;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 参照族双段目标验证器（2026-11-04 双段改版：第一段弹选{target}=**己方生物**参照
    ///（表域=己方单位+NoRole 滤角色——角色不作参照，修复教学活体护甲以角色生命 30 为基准
    /// 刷甲无敌）；第二段=结算期反问落点——活体炸弹问受害者（对方单位含角色）、活体护甲问
    /// 护甲落点（己方单位含角色——角色可受甲）。2026-11-03「单目标+硬编码最高参照」与
    /// str="SelfUnit" 档作废）：
    /// ① 活体炸弹（BurstDamage 表行 80daf3b6，红3）：参照=弹选己方生物、伤害=参照攻。
    ///    断言：kinds=[1]+NoRole、规格 2 候选（巨像+火灵·攻，角色被滤）、LAE 每候选一行、
    ///    AI 预选=己方生物、引擎①参照巨像→9 伤打死敌方火灵（角色不动）、引擎②参照火灵·攻
    ///    →等量伤落对方角色（受害者候选=1 自动取）——两引擎合证伤害=**所选参照**攻非最高硬编码。
    /// ② 活体护甲缺省档（LivingArmor 表行 10c6345b，绿3）：参照=弹选己方生物、落点=反问己方单位。
    ///    断言：规格 2 候选（守卫/巨像，角色被滤）、LAE 2 行、引擎①参照巨像→落点自动取首=
    ///    守卫得甲=巨像命 9（角色甲恒 0）、引擎②**预选角色=被滤**（回归锚：修复前角色可作参照
    ///    →30 甲无敌；修复后角色甲恒 0、甲按候选序落生物）。
    /// ③ 活体护甲教学真卡（6C24867E，str=SelfUnit 已退役）：整卡 PlayCard 全链（0 费+代价行
    ///    「敌方单位获得 6 层护甲」）——参照=守卫→守卫甲=守卫自身命 6（≠巨像 9=若仍硬编码
    ///    最高则贴 9），代价敌火灵同步 +6 甲。
    /// ④ 护盾猛击（ShieldSlam 表行 247c2ae1，红3·无参照段照旧）：拆**己方角色**全部护甲（硬编码）
    ///    → 等量伤落弹选{target}。断言：角色 5 甲清零+火灵(1 命)被 5 伤打死；二次施放角色无甲=
    ///    空转（对方角色不掉血、无异常）。
    /// 夹具口径：候选序=战场序生物在前角色在后（GetCandidates 定序）、AI/无头 Service 自动取首、
    /// 预选目标结算期经 pool.Contains 校验（被滤目标丢弃回落自动取）。卡名解析（ID 随内容重推
    /// 漂移）、内存合成临时卡不落盘、p1/p2 双 AI 自动收敛、InitGame 后 BoardState+LinkAura 挂载。
    /// 入口：菜单 验证/参照族双段目标三原子；Run() 返回报告文本（全过=PASS 结尾）。
    /// </summary>
    public static class RefAtomSingleTargetVerifier
    {
        private const string EnemyAName = "火灵·攻";   // 1 血（受害者位/次攻对照位）
        private const string OwnBigName = "虚空巨像"; // 9/9（攻/血最高对照位）
        private const string OwnGuardName = "林地守卫"; // 0/7 守卫（活体护甲落点）
        private const string RealArmorName = "活体护甲"; // 教学真卡（双段·SelfUnit 已退役）

        private readonly struct Check
        {
            public readonly bool Pass;
            public readonly string Text;
            public Check(bool pass, string text) { Pass = pass; Text = text; }
        }

        [MenuItem("Tools/验证/参照族双段目标三原子")]
        public static void RunFromMenu()
        {
            var report = Run();
            Debug.Log(report);
        }

        public static string Run()
        {
            var log = new StringBuilder();
            var checks = new List<Check>();
            try
            {
                RunBurstDamageScenario(log, checks);
                RunLivingArmorDefaultScenario(log, checks);
                RunRealTutorialArmorScenario(log, checks);
                RunShieldSlamScenario(log, checks);
            }
            catch (Exception ex)
            {
                checks.Add(new Check(false, $"异常中断：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"));
            }
            int fails = checks.Count(c => !c.Pass);
            log.AppendLine(fails == 0
                ? $"==== RefAtomSingleTargetVerifier 全过（{checks.Count} 项断言）===="
                : $"==== RefAtomSingleTargetVerifier 失败 {fails}/{checks.Count} 项 ====");
            foreach (var c in checks.Where(c => !c.Pass))
                log.AppendLine("[FAIL] " + c.Text);
            return log.ToString();
        }

        // ======================================== ① 活体炸弹（BurstDamage·双段） ========================================

        private static void RunBurstDamageScenario(StringBuilder log, List<Check> checks)
        {
            var tmp = BuildTempCard("探针·活体炸弹", "C_TMP_BURST_PROBE", "EFF_TMP_BURST", AtomicEffectType.BurstDamage, 1);
            CheckTableDomain(checks, AtomicEffectType.BurstDamage, "双方战场:结界;己方战场:生物", null, "活体炸弹"); // B2：Domain 新语法；伤害行已扩含结界（2026-11-05 遗留期望同步）

            var core = GameCore.Instance;
            var catalog = CardCatalog.LoadAll().ToList();
            var deck1 = new List<CardData>();
            for (int i = 0; i < 4; i++) deck1.Add(tmp);
            for (int i = 0; i < 3; i++) deck1.Add(ByName(catalog, OwnBigName));
            for (int i = 0; i < 2; i++) deck1.Add(ByName(catalog, EnemyAName)); // 己方次攻对照位
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) { deck2.Add(ByName(catalog, EnemyAName)); deck2.Add(ByName(catalog, OwnBigName)); }
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true; p2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.FlankNeighbors;
                GameBoard.LinkAuraSystem.Attach(board);
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;

                    var enemy = FindAndMoveByName(zm, p2, EnemyAName, Zone.Battlefield);   // 1 血受害者位
                    var big = FindAndMoveByName(zm, p1, OwnBigName, Zone.Battlefield);     // 9/9 参照位（先入战场）
                    var small = FindAndMoveByName(zm, p1, EnemyAName, Zone.Battlefield);   // 次攻对照参照位
                    var tCard = FindAndMoveByName(zm, p1, tmp.CardName, Zone.Hand);
                    foreach (var c in zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, tmp.CardName) && c != tCard).ToList())
                        zm.MoveCard(c, p1, Zone.Hand, Zone.Deck);
                    core.ElementPool.AddMana(p1, ManaType.Red, null, 10);

                    int bigAtk = EntityEffectExtensions.GetPower(big), smallAtk = EntityEffectExtensions.GetPower(small);
                    checks.Add(new Check(enemy != null && big != null && small != null && tCard != null
                        && bigAtk > smallAtk && smallAtk > 0,
                        $"夹具前提：火灵/巨像/对照/手卡齐 且 巨像攻({bigAtk})>对照攻({smallAtk})>0——否则「参照=弹选」断言无区分度"));

                    // 数据链：单原子双段第一段 kinds=[1]+NoRole（参照=己方生物），候选=巨像+火灵·攻 共 2、角色被滤
                    var def = CardEffectConverter.ConvertAll(tmp.Effects, tmp.ID).First();
                    var ctx = new EffectExecutionContext { Source = p1, Controller = p1, ZoneManager = zm, ElementPool = core.ElementPool, ModeIndex = 0 };
                    var specs = Attribute.EffectHandlerRegistry.CollectAtomTargetSpecs(def, ctx);
                    checks.Add(new Check(def.PerAtomTargets && def.Effects.Count == 1
                        && def.Effects[0].TargetKinds.SequenceEqual(new[] { 1 }),
                        "活体炸弹应为逐原子单原子 kinds=[1]（双段改版：第一段=己方生物参照）"));
                    checks.Add(new Check(specs.Count == 1 && specs[0].Candidates.Count == 2 && !specs[0].AutoTake
                        && !specs[0].Candidates.Contains(p1),
                        $"活体炸弹规格期望 1 spec / 2 候选（巨像+火灵·攻·角色被 NoRole 滤）占选择权，实际 "
                        + string.Join(" ; ", specs.Select(s => $"{s.Atom.Type}:{s.Candidates.Count}c/auto={s.AutoTake}").ToArray())));

                    // LAE：每参照候选一行 nTargets=1
                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    var rows = lae.Actions.Where(a => a.Type == TideActionType.PlayCard && ReferenceEquals(a.Card, tCard)).ToList();
                    checks.Add(new Check(rows.Count == 2 && rows.All(r => r.Targets != null && r.Targets.Count == 1),
                        $"活体炸弹 LAE 期望 2 行 nTargets=1（每己方生物参照一行），实际 {rows.Count} 行"));

                    // AI 预选：己方生物参照（非角色非对方）
                    var ai = new SimpleAI();
                    var mi = typeof(SimpleAI).GetMethod("ChooseTargets", BindingFlags.NonPublic | BindingFlags.Instance);
                    var picked = mi.Invoke(ai, new object[] { core, p1, tCard, 0 }) as List<Entity>;
                    log.AppendLine("[活体炸弹] AI picked=" + (picked == null ? "null" : string.Join("+", picked.Select(NameOf).ToArray())));
                    checks.Add(new Check(picked != null && picked.Count == 1
                        && (ReferenceEquals(picked[0], big) || ReferenceEquals(picked[0], small)),
                        $"AI 预选期望 1 目标=己方生物参照，实际 " + (picked == null ? "null" : string.Join("+", picked.Select(NameOf).ToArray()))));

                    // 引擎①：参照=巨像 → 受害者自动取首=敌方火灵（候选序生物在前）→ 9 伤打死（1 命），对方角色不动
                    int heroBefore = p2.Life, enemyBefore = EntityEffectExtensions.GetLife(enemy);
                    var ok = GameActions.PlayCard(core, p1, tCard, new List<Entity> { big }, Zone.Hand, 0);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    int dHero = p2.Life - heroBefore;
                    bool dead = enemyBefore > 0 && (!enemy.IsAlive || EntityEffectExtensions.GetLife(enemy) <= 0);
                    log.AppendLine($"[活体炸弹] 引擎① 参照巨像 火灵存活={enemy.IsAlive} Δ对方角色={dHero}（参照攻={bigAtk}）");
                    checks.Add(new Check(ok && dead && dHero == 0,
                        $"引擎① 期望 火灵被参照攻({bigAtk})伤打死 且 对方角色 Δ=0，实际 火灵存活={enemy.IsAlive} Δ角色={dHero}"));

                    // 引擎②：参照=火灵·攻（次攻对照）→ 受害者候选仅剩对方角色（火灵已亡）自动取 → Δ=-对照攻
                    //（与引擎①合证：伤害=**所选参照**攻——若仍是 11-03 硬编码最高则两引擎都应打 {bigAtk}）
                    var t2 = FindAndMoveByName(zm, p1, tmp.CardName, Zone.Hand);
                    var ok2 = t2 != null && GameActions.PlayCard(core, p1, t2, new List<Entity> { small }, Zone.Hand, 0);
                    drain = 0;
                    while (ok2 && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    int dHero2 = p2.Life - heroBefore - dHero;
                    log.AppendLine($"[活体炸弹] 引擎② 参照火灵·攻 Δ对方角色={dHero2}（对照攻={smallAtk}）");
                    checks.Add(new Check(ok2 && dHero2 == -smallAtk,
                        $"引擎② 期望 对方角色 Δ=-{smallAtk}（对照攻·参照=弹选非最高），实际 Δ={dHero2}"));
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        // ======================================== ② 活体护甲缺省档（双段：弹选参照+反问落点） ========================================

        private static void RunLivingArmorDefaultScenario(StringBuilder log, List<Check> checks)
        {
            var tmp = BuildTempCard("探针·活体护甲缺省", "C_TMP_ARMOR_PROBE", "EFF_TMP_ARMOR", AtomicEffectType.LivingArmor, 1);
            CheckTableDomain(checks, AtomicEffectType.LivingArmor, "己方战场:生物", null, "活体护甲"); // B2

            var core = GameCore.Instance;
            var catalog = CardCatalog.LoadAll().ToList();
            var deck1 = new List<CardData>();
            for (int i = 0; i < 4; i++) deck1.Add(tmp);
            for (int i = 0; i < 3; i++) { deck1.Add(ByName(catalog, OwnBigName)); deck1.Add(ByName(catalog, OwnGuardName)); }
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) { deck2.Add(ByName(catalog, EnemyAName)); deck2.Add(ByName(catalog, "教学·3")); }
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true; p2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.FlankNeighbors;
                GameBoard.LinkAuraSystem.Attach(board);
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;

                    FindAndMoveByName(zm, p2, EnemyAName, Zone.Battlefield);
                    var guard = FindAndMoveByName(zm, p1, OwnGuardName, Zone.Battlefield); // 0/6（先入战场=落点自动取首位）
                    var big = FindAndMoveByName(zm, p1, OwnBigName, Zone.Battlefield);     // 9/9 参照位
                    var tCard = FindAndMoveByName(zm, p1, tmp.CardName, Zone.Hand);
                    foreach (var c in zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, tmp.CardName) && c != tCard).ToList())
                        zm.MoveCard(c, p1, Zone.Hand, Zone.Deck);
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10);

                    int bigLife = EntityEffectExtensions.GetLife(big), guardLife = EntityEffectExtensions.GetLife(guard);
                    checks.Add(new Check(guard != null && big != null && tCard != null && bigLife > guardLife,
                        $"夹具前提：守卫/巨像/手卡齐 且 巨像命({bigLife})>守卫命({guardLife})——否则「参照=弹选」断言无区分度"));

                    // 规格：kinds=[1]+NoRole 候选=守卫+巨像 共 2（角色被滤——参照不可为角色）
                    var def = CardEffectConverter.ConvertAll(tmp.Effects, tmp.ID).First();
                    var ctx = new EffectExecutionContext { Source = p1, Controller = p1, ZoneManager = zm, ElementPool = core.ElementPool, ModeIndex = 0 };
                    var specs = Attribute.EffectHandlerRegistry.CollectAtomTargetSpecs(def, ctx);
                    checks.Add(new Check(def.Effects.Count == 1 && def.Effects[0].TargetKinds.SequenceEqual(new[] { 1 }),
                        "活体护甲缺省档应为单原子 kinds=[1]（双段改版：第一段=己方生物参照）"));
                    checks.Add(new Check(specs.Count == 1 && specs[0].Candidates.Count == 2 && !specs[0].AutoTake
                        && !specs[0].Candidates.Contains(p1),
                        $"活体护甲规格期望 1 spec / 2 候选（守卫+巨像·角色被 NoRole 滤），实际 "
                        + string.Join(" ; ", specs.Select(s => $"{s.Atom.Type}:{s.Candidates.Count}c/auto={s.AutoTake}").ToArray())));

                    // LAE：2 行 nTargets=1
                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    var rows = lae.Actions.Where(a => a.Type == TideActionType.PlayCard && ReferenceEquals(a.Card, tCard)).ToList();
                    checks.Add(new Check(rows.Count == 2 && rows.All(r => r.Targets != null && r.Targets.Count == 1),
                        $"活体护甲 LAE 期望 2 行 nTargets=1（每己方生物参照一行），实际 {rows.Count} 行"));

                    // AI 预选：己方生物参照（非角色）
                    var ai = new SimpleAI();
                    var mi = typeof(SimpleAI).GetMethod("ChooseTargets", BindingFlags.NonPublic | BindingFlags.Instance);
                    var picked = mi.Invoke(ai, new object[] { core, p1, tCard, 0 }) as List<Entity>;
                    checks.Add(new Check(picked != null && picked.Count == 1
                        && (ReferenceEquals(picked[0], guard) || ReferenceEquals(picked[0], big)),
                        $"AI 预选期望 1 目标=己方生物参照，实际 " + (picked == null ? "null" : string.Join("+", picked.Select(NameOf).ToArray()))));

                    // 引擎①：参照=巨像 → 落点反问自动取首=守卫（候选序生物在前）→ 守卫甲=巨像命（≠守卫命=区分参照与落点）
                    var ok = GameActions.PlayCard(core, p1, tCard, new List<Entity> { big }, Zone.Hand, 0);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    int heroArmor = EntityEffectExtensions.GetCounterCount(p1, Attribute.KeywordRules.ArmorCounter);
                    int guardArmor = EntityEffectExtensions.GetCounterCount(guard, Attribute.KeywordRules.ArmorCounter);
                    log.AppendLine($"[活体护甲缺省] 引擎① 参照巨像 守卫甲={guardArmor} 角色甲={heroArmor}（参照命={bigLife}）");
                    checks.Add(new Check(ok && guardArmor == bigLife && heroArmor == 0,
                        $"引擎① 期望 守卫甲={bigLife}（巨像命·参照=弹选）角色甲=0，实际 守卫甲={guardArmor} 角色甲={heroArmor}"));

                    // 引擎②（回归锚）：预选**角色**作参照=被滤（修复前角色可入参照→以角色命 30 刷甲无敌）
                    // → 结算回落候选序自动取生物参照、落点自动取首——角色甲恒 0
                    int guardArmorBefore = EntityEffectExtensions.GetCounterCount(guard, Attribute.KeywordRules.ArmorCounter);
                    var t2 = FindAndMoveByName(zm, p1, tmp.CardName, Zone.Hand);
                    var ok2 = t2 != null && GameActions.PlayCard(core, p1, t2, new List<Entity> { p1 }, Zone.Hand, 0);
                    drain = 0;
                    while (ok2 && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    heroArmor = EntityEffectExtensions.GetCounterCount(p1, Attribute.KeywordRules.ArmorCounter);
                    int guardArmor2 = EntityEffectExtensions.GetCounterCount(guard, Attribute.KeywordRules.ArmorCounter);
                    log.AppendLine($"[活体护甲缺省] 引擎② 预选角色 角色甲={heroArmor} 守卫甲Δ={guardArmor2 - guardArmorBefore}");
                    checks.Add(new Check(ok2 && heroArmor == 0 && guardArmor2 - guardArmorBefore > 0
                        && guardArmor2 - guardArmorBefore <= bigLife,
                        $"引擎② 期望 预选角色被滤：角色甲=0（修复前=角色命 30 无敌）且甲按候选序落生物，"
                        + $"实际 角色甲={heroArmor} 守卫甲Δ={guardArmor2 - guardArmorBefore}"));
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        // ======================================== ③ 活体护甲教学真卡（双段·SelfUnit 退役 + 代价行） ========================================

        private static void RunRealTutorialArmorScenario(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            var real = ByName(catalog, RealArmorName);

            // 数据链：SelfUnit 已退役（str 空）+ kinds=[1]（双段第一段=己方生物参照）
            var def = CardEffectConverter.ConvertAll(real.Effects, real.ID).First();
            var atom = def.Effects.FirstOrDefault(a => a != null && a.Type == AtomicEffectType.LivingArmor);
            checks.Add(new Check(atom != null && string.IsNullOrEmpty(atom.StringValue)
                && atom.TargetKinds != null && atom.TargetKinds.SequenceEqual(new[] { 1 }),
                "教学真卡活体护甲应为 LivingArmor 原子 str 空（SelfUnit 已随双段改版退役）kinds=[1]（载荷链——表/教学 JSON 同步前提）"));

            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 3; i++) deck1.Add(real);
            for (int i = 0; i < 4; i++) { deck1.Add(ByName(catalog, OwnBigName)); deck1.Add(ByName(catalog, OwnGuardName)); }
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) { deck2.Add(ByName(catalog, EnemyAName)); deck2.Add(ByName(catalog, "教学·3")); }
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true; p2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.FlankNeighbors;
                GameBoard.LinkAuraSystem.Attach(board);
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;

                    // 敌方恰 1 单位（代价行「敌方单位获得6层护甲」候选=1 全自动，不走对手问选）
                    var enemy = FindAndMoveByName(zm, p2, EnemyAName, Zone.Battlefield);
                    var guard = FindAndMoveByName(zm, p1, OwnGuardName, Zone.Battlefield); // 0/6（先入=落点自动取首位）
                    FindAndMoveByName(zm, p1, OwnBigName, Zone.Battlefield);               // 9/9（若仍硬编码最高会贴 9）
                    var tCard = FindAndMoveByName(zm, p1, RealArmorName, Zone.Hand);
                    foreach (var c in zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, RealArmorName) && c != tCard).ToList())
                        zm.MoveCard(c, p1, Zone.Hand, Zone.Deck);
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10);

                    // 引擎：整卡施放（0 费 + 代价行自动执行）参照=守卫 → 落点自动取首=守卫 → 守卫甲=守卫自身命
                    int guardLife = EntityEffectExtensions.GetLife(guard), bigLife = EntityEffectExtensions.GetLife(
                        zm.GetCards(p1, Zone.Battlefield).FirstOrDefault(c => IsName(c, OwnBigName)));
                    checks.Add(new Check(guardLife != bigLife,
                        $"夹具前提：守卫命({guardLife})≠巨像命({bigLife})——否则 弹选参照/硬编码最高 无区分度"));
                    var ok = GameActions.PlayCard(core, p1, tCard, new List<Entity> { guard }, Zone.Hand, 0);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    int guardArmor = EntityEffectExtensions.GetCounterCount(guard, Attribute.KeywordRules.ArmorCounter);
                    int enemyArmor = EntityEffectExtensions.GetCounterCount(enemy, Attribute.KeywordRules.ArmorCounter);
                    int heroArmor = EntityEffectExtensions.GetCounterCount(p1, Attribute.KeywordRules.ArmorCounter);
                    log.AppendLine($"[真卡活体护甲] 守卫甲={guardArmor}（守卫命={guardLife} 巨像命={bigLife}）敌火灵甲={enemyArmor} 角色甲={heroArmor}");
                    checks.Add(new Check(ok && guardArmor == guardLife,
                        $"引擎期望 守卫甲={guardLife}（参照=弹选守卫自身命；≠巨像{bigLife}=若仍硬编码最高），实际 {guardArmor}"));
                    checks.Add(new Check(enemyArmor == 6 && heroArmor == 0,
                        $"代价行期望 敌火灵甲=6（敌方单位获得6层护甲·候选1自动）角色甲=0，实际 火灵甲={enemyArmor} 角色甲={heroArmor}"));
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        // ======================================== ④ 护盾猛击（ShieldSlam·无参照段照旧） ========================================

        private static void RunShieldSlamScenario(StringBuilder log, List<Check> checks)
        {
            var tmp = BuildTempCard("探针·护盾猛击", "C_TMP_SLAM_PROBE", "EFF_TMP_SLAM", AtomicEffectType.ShieldSlam, 2);
            CheckTableDomain(checks, AtomicEffectType.ShieldSlam, "双方战场:结界;对方战场:生物; 对方角色", null, "护盾猛击"); // B2；伤害行已扩含结界（2026-11-05 遗留期望同步）

            var core = GameCore.Instance;
            var catalog = CardCatalog.LoadAll().ToList();
            var deck1 = new List<CardData>();
            for (int i = 0; i < 4; i++) deck1.Add(tmp);
            for (int i = 0; i < 6; i++) deck1.Add(ByName(catalog, OwnBigName));
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) { deck2.Add(ByName(catalog, EnemyAName)); deck2.Add(ByName(catalog, "教学·3")); }
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true; p2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.FlankNeighbors;
                GameBoard.LinkAuraSystem.Attach(board);
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;

                    var enemy = FindAndMoveByName(zm, p2, EnemyAName, Zone.Battlefield); // 1/1
                    var tCard = FindAndMoveByName(zm, p1, tmp.CardName, Zone.Hand);
                    foreach (var c in zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, tmp.CardName) && c != tCard).ToList())
                        zm.MoveCard(c, p1, Zone.Hand, Zone.Deck);
                    core.ElementPool.AddMana(p1, ManaType.Red, null, 10);

                    // 己方角色预挂 5 层护甲（拆甲来源=硬编码角色）
                    EntityEffectExtensions.AddCounters(p1, Attribute.KeywordRules.ArmorCounter, 5, null);

                    // 规格：kinds=[2] 候选=火灵+对方角色 共 2
                    var def = CardEffectConverter.ConvertAll(tmp.Effects, tmp.ID).First();
                    var ctx = new EffectExecutionContext { Source = p1, Controller = p1, ZoneManager = zm, ElementPool = core.ElementPool, ModeIndex = 0 };
                    var specs = Attribute.EffectHandlerRegistry.CollectAtomTargetSpecs(def, ctx);
                    checks.Add(new Check(def.Effects.Count == 1 && def.Effects[0].TargetKinds.SequenceEqual(new[] { 2 })
                        && specs.Count == 1 && specs[0].Candidates.Count == 2 && !specs[0].AutoTake,
                        $"护盾猛击规格期望 单原子 kinds=[2] / 1 spec / 2 候选，实际 "
                        + string.Join(" ; ", specs.Select(s => $"{s.Atom.Type}:{s.Candidates.Count}c/auto={s.AutoTake}").ToArray())));

                    // LAE：2 行 nTargets=1
                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    var rows = lae.Actions.Where(a => a.Type == TideActionType.PlayCard && ReferenceEquals(a.Card, tCard)).ToList();
                    checks.Add(new Check(rows.Count == 2 && rows.All(r => r.Targets != null && r.Targets.Count == 1),
                        $"护盾猛击 LAE 期望 2 行 nTargets=1（单目标），实际 {rows.Count} 行"));

                    // 引擎①：打火灵 → 角色甲 5 清零、火灵(1 命)被 5 伤打死
                    var ok = GameActions.PlayCard(core, p1, tCard, new List<Entity> { enemy }, Zone.Hand, 0);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    int heroArmor = EntityEffectExtensions.GetCounterCount(p1, Attribute.KeywordRules.ArmorCounter);
                    bool dead = !enemy.IsAlive || EntityEffectExtensions.GetLife(enemy) <= 0;
                    log.AppendLine($"[护盾猛击] 引擎① 角色甲={heroArmor} 火灵存活={enemy.IsAlive}");
                    checks.Add(new Check(ok && heroArmor == 0 && dead,
                        $"引擎① 期望 角色甲清零 且 火灵被等量 5 伤打死，实际 角色甲={heroArmor} 火灵存活={enemy.IsAlive}"));

                    // 引擎②：角色无甲再施放 → 空转（对方角色不掉血、不异常）
                    int foeLife = p2.Life;
                    var t2 = FindAndMoveByName(zm, p1, tmp.CardName, Zone.Hand);
                    var ok2 = t2 != null && GameActions.PlayCard(core, p1, t2, new List<Entity> { p2 }, Zone.Hand, 0);
                    drain = 0;
                    while (ok2 && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    log.AppendLine($"[护盾猛击] 引擎② 空转 对方角色Δ={p2.Life - foeLife}");
                    checks.Add(new Check(ok2 && p2.Life == foeLife,
                        $"引擎② 期望 无甲空转（对方角色 Δ=0），实际 Δ={p2.Life - foeLife}"));
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        // ======================================== 夹具辅助 ========================================

        /// <summary>表行域断言：运行时装载的原子表行域/滤口须与双段改版描述一致
        ///（参照段=己方单位+NoRole；护盾猛击无参照段=对方单位无滤）。</summary>
        private static void CheckTableDomain(List<Check> checks, AtomicEffectType type, string expectDomain, string expectFilter, string label)
        {
            var cfg = Attribute.AtomicEffectTable.GetByType(type);
            string filterNow = cfg == null ? "行缺失" : $"域={cfg.Domain}/滤={cfg.TargetFilter ?? "无"}";
            checks.Add(new Check(cfg != null && !string.IsNullOrEmpty(cfg.HashId) && cfg.Domain == expectDomain
                && (cfg.TargetFilter ?? "") == (expectFilter ?? ""),
                $"{label}表行期望 域={expectDomain}/滤={expectFilter ?? "无"}（B2：TargetKinds 列删，域=Domain），实际 {filterNow}"));
        }

        /// <summary>内存合成临时卡（不落盘）：单原子 refId=表行 HashId·kinds=count=1，header 无 kinds。</summary>
        private static CardData BuildTempCard(string cardName, string cardId, string effId, AtomicEffectType type, int kind)
        {
            var row = Attribute.AtomicEffectTable.GetByType(type);
            var eff = new CardEffectData
            {
                Id = effId,
                DisplayName = cardName,
                TriggerTiming = 19, // Activate_Active（法术施放档）
                ActivationType = 2, // Voluntary
                SelectionMode = -1, // None：选择参数在原子上（PerAtomTargets）
                TargetCount = -2,   // 未声明——原子 count=1 显式声明
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry { refId = row.HashId, kinds = new List<int> { kind }, count = 1 },
                },
            };
            return new CardData { ID = cardId, CardName = cardName, Supertype = Cardtype.Spell, Effects = new List<CardEffectData> { eff } };
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
               ?? throw new InvalidOperationException($"卡池找不到「{name}」（教学池卡名变更？验证器夹具须跟进）");

        private static bool IsName(CardData c, string name)
            => c != null && c.CardName == name;

        private static bool IsName(Card c, string name)
            => c is IHasName n && n.CardName == name;

        /// <summary>按卡名在牌库/手牌找到首张并移到目标区（已在目标区=幂等直返；找不到返回 null）。</summary>
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
