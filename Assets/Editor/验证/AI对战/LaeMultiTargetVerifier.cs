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
    /// LAE 多目标动作空间回归验证器（2026-11-02 多目标验收）：
    /// ① 组合枚举——诱敌（selection=Multiple，count=2，单原子 kinds=[2]）按敌人数分档：
    ///    3 敌=3 行 nTargets=2（C(3,2) 两两不同）；2 敌=1 行 nTargets=2（AutoTake 档显式展开唯一组合）；
    ///    1 敌=1 行 nTargets=1；0 敌=不可施放（无 PlayCard 行）。
    ///    数据链断言：effect.count=2 经 EffectSlim→CardEffectData.TargetCount→def.TargetCount=2
    ///    （DynamicTargetCount 恒 false——全仓无赋值点，随机/任意档才走引擎代选）。
    /// ② PerAtomTargets 逐原子——内存合成临时卡「对一敌方单位伤 2 + 对一己方单位疗 2」
    ///    （header 不声明 kinds）：LAE 出逐原子笛卡尔积行（3 敌 × 2 己 = 6 行 n=2）；
    ///    SimpleAI.ChooseTargets 逐原子预选（两个不同域目标各一）；引擎结算按原子序消费预选
    ///    （伤在指定敌、疗在指定己，其余候选零变化）。临时卡纯内存构造不落盘，跑完即弃。
    /// ③ 并列效果分段选目标——「弹回×2（对方单位）+ 回收×2（己方墓地）」并列卡（合成器形状）：
    ///    LAE 分段组合展开 C(3,2)×C(4,2)=18 行（行内前 2 敌方战场/后 2 己方墓地）；AI 预选 4 目标分段；
    ///    引擎交叉结算弹回入对方手牌/回收入己方手牌各只落指定目标（2026-11-02「取消并列收缩作用域」配套）。
    /// 入口：菜单 验证/LAE多目标组合枚举；Run() 返回报告文本（全过=PASS 结尾）。
    /// 夹具定位按**卡名**解析+动作行按**卡实例**过滤（教学池 ID 随内容重推漂移——2026-11-02
    /// 火炬即被并行改版换号，硬编码 ID 会假阴）。
    /// </summary>
    public static class LaeMultiTargetVerifier
    {
        private const string LureName = "诱敌";
        // 2026-11-03 教学池红卡改名对齐：火灵→火灵·攻/守、火种→火种·锐/厚、火炬退役——夹具随之更名
        private const string EnemyAName = "火灵·攻";   // 1 血
        private const string EnemyBName = "教学·3";   // 1/1 白板（原火种·锐已改 2/2，移出 1 血夹具）
        private const string EnemyCName = "火灵·守";   // 1 血
        private const string OwnBigName = "虚空巨像"; // 9/9

        private readonly struct Check
        {
            public readonly bool Pass;
            public readonly string Text;
            public Check(bool pass, string text) { Pass = pass; Text = text; }
        }

        [MenuItem("Tools/验证/LAE多目标组合枚举")]
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
                RunLureScenarios(log, checks);
                RunPerAtomScenario(log, checks);
                RunParallelBounceRecoverScenario(log, checks);
            }
            catch (Exception ex)
            {
                checks.Add(new Check(false, $"异常中断：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"));
            }
            int fails = checks.Count(c => !c.Pass);
            log.AppendLine(fails == 0
                ? $"==== LaeMultiTargetVerifier 全过（{checks.Count} 项断言）===="
                : $"==== LaeMultiTargetVerifier 失败 {fails}/{checks.Count} 项 ====");
            foreach (var c in checks.Where(c => !c.Pass))
                log.AppendLine("[FAIL] " + c.Text);
            return log.ToString();
        }

        // ======================================== ① 诱敌组合枚举分档 ========================================

        private static void RunLureScenarios(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            // 2026-11-06 诱敌卡已删出教学池（④同色违规清除）——卡池取不到时从教学效果库合成夹具卡
            var lure = catalog.FirstOrDefault(c => IsName(c, LureName)) ?? LureFixture();

            // 数据链：count=2 → def.TargetCount=2（Multiple 走组合枚举的前提）。
            // 诱敌单效果——效果 ID 随 DisplayName 哈希漂移，不硬编码。
            var lureDefs = CardEffectConverter.ConvertAll(lure.Effects, lure.ID);
            checks.Add(new Check(lureDefs.Count == 1, $"诱敌效果数={lureDefs.Count}（期望 1——断言口径前提）"));
            var def = lureDefs[0];
            checks.Add(new Check(def.SelectionMode == SelectionMode.Multiple, $"诱敌 SelectionMode={def.SelectionMode}（期望 Multiple）"));
            checks.Add(new Check(def.TargetCount == 2 && !def.DynamicTargetCount && !def.RandomTarget,
                $"诱敌 TargetCount={def.TargetCount} dyn={def.DynamicTargetCount} rand={def.RandomTarget}（期望 2/false/false）"));

            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 6; i++) deck1.Add(lure);
            for (int i = 0; i < 3; i++) deck1.Add(ByName(catalog, EnemyCName)); // 填充：兼作蓝地垫材（开抽随机性脱敏）
            var deck2 = new List<CardData>();
            // 12 张：开抽最多 7 + 三敌上场，牌库恒余 ≥2——诱敌资敌代价（对方抽 1）须对方牌库非空，
            // 库存抽空会把整卡拦成「不可施放」（夹具假阴）
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

                    // 三敌上场
                    var enemies = new List<Card>();
                    foreach (var n in new[] { EnemyAName, EnemyBName, EnemyCName })
                        enemies.Add(FindAndMoveByName(zm, p2, n, Zone.Battlefield));

                    // 手里恰好一张诱敌；蓝地垫材从 牌库∪手牌 取（开抽随机性脱敏——诱敌 payload 代价「蓝2」须蓝地门槛）
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
                    for (int scen = 3; scen >= 0; scen--)
                    {
                        while (zm.GetCards(p2, Zone.Battlefield).Count > scen)
                        {
                            var bf = zm.GetCards(p2, Zone.Battlefield);
                            zm.MoveCard(bf[0], p2, Zone.Battlefield, Zone.Graveyard);
                        }
                        lae.Enumerate(core, p1);
                        var rows = lae.Actions.Where(a => a.Type == TideActionType.PlayCard && ReferenceEquals(a.Card, handLure)).ToList();
                        string describe(TideAction a) => a.Targets == null ? "null"
                            : "n=" + a.Targets.Count + "[" + string.Join("+", a.Targets.Select(NameOf).ToArray()) + "]";
                        log.AppendLine($"[诱敌] 敌人={scen} → 行数={rows.Count} :: {string.Join(" ; ", rows.Select(describe).ToArray())}");

                        switch (scen)
                        {
                            case 3:
                                checks.Add(new Check(rows.Count == 3 && rows.All(r => r.Targets != null && r.Targets.Count == 2),
                                    $"3 敌期望 3 行 nTargets=2，实际 {rows.Count} 行（{string.Join(" ; ", rows.Select(describe).ToArray())}）"));
                                var pairs = rows.Select(r => string.Join("|", r.Targets.Select(t => t.GetHashCode().ToString()).OrderBy(x => x))).ToList();
                                checks.Add(new Check(pairs.Distinct().Count() == 3, "3 敌各行 t1/t2 组合须两两不同（C(3,2)）"));
                                break;
                            case 2:
                                checks.Add(new Check(rows.Count == 1 && rows[0].Targets != null && rows[0].Targets.Count == 2,
                                    $"2 敌期望 1 行 nTargets=2，实际 {rows.Count} 行（{string.Join(" ; ", rows.Select(describe).ToArray())}）"));
                                break;
                            case 1:
                                checks.Add(new Check(rows.Count == 1 && rows[0].Targets != null && rows[0].Targets.Count == 1,
                                    $"1 敌期望 1 行 nTargets=1（候选≤需求 AutoTake 显式收口），实际 {rows.Count} 行"));
                                break;
                            case 0:
                                checks.Add(new Check(rows.Count == 0, $"0 敌期望不可施放（0 行），实际 {rows.Count} 行"));
                                break;
                        }
                    }
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        // ======================================== ② PerAtomTargets 逐原子 ========================================

        private static void RunPerAtomScenario(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            var tmp = BuildTempPerAtomCard();

            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 4; i++) deck1.Add(tmp);
            for (int i = 0; i < 3; i++) deck1.Add(ByName(catalog, OwnBigName));
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) { deck2.Add(ByName(catalog, EnemyAName)); deck2.Add(ByName(catalog, EnemyBName)); } // 12 张防开抽抽空
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true; p2.IsAI = true; // 出牌结算自动收敛
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.FlankNeighbors;
                GameBoard.LinkAuraSystem.Attach(board);
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;

                    var e1 = FindAndMoveByName(zm, p2, EnemyAName, Zone.Battlefield);
                    var e2 = FindAndMoveByName(zm, p2, EnemyBName, Zone.Battlefield);
                    var ow = FindAndMoveByName(zm, p1, OwnBigName, Zone.Battlefield);
                    var tCard = FindAndMoveByName(zm, p1, tmp.CardName, Zone.Hand);
                    foreach (var c in zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, tmp.CardName) && c != tCard).ToList())
                        zm.MoveCard(c, p1, Zone.Hand, Zone.Graveyard);
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10);

                    // 巨像 9→6、角色 -5：Heal 行 Damaged 滤下己方候选=2（巨像+角色）
                    typeof(Card).GetField("_life", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ow, 6);
                    p1.Life -= 5;
                    int heroBefore = p1.Life;

                    // 数据链：PerAtomTargets=true，两原子各自域
                    var def = CardEffectConverter.ConvertAll(tmp.Effects, tmp.ID).First();
                    checks.Add(new Check(def.PerAtomTargets, "临时卡 PerAtomTargets 应为 true（header 未声明 kinds）"));
                    var ctx = new EffectExecutionContext { Source = p1, Controller = p1, ZoneManager = zm, ElementPool = core.ElementPool, ModeIndex = 0 };
                    var specs = Attribute.EffectHandlerRegistry.CollectAtomTargetSpecs(def, ctx);
                    checks.Add(new Check(specs.Count == 2 && specs[0].Candidates.Count == 3 && specs[1].Candidates.Count == 2
                        && !specs[0].AutoTake && !specs[1].AutoTake,
                        $"原子规格期望 伤(3 候选)+疗(2 候选) 且均占选择权，实际 " +
                        string.Join(" ; ", specs.Select(s => $"{s.Atom.Type}:{s.Candidates.Count}c/auto={s.AutoTake}").ToArray())));

                    // LAE：逐原子笛卡尔积 3×2=6 行 n=2
                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    var rows = lae.Actions.Where(a => a.Type == TideActionType.PlayCard && ReferenceEquals(a.Card, tCard)).ToList();
                    log.AppendLine("[逐原子] LAE 行数=" + rows.Count + " :: " + string.Join(" ; ",
                        rows.Select(r => r.Targets == null ? "null" : "n=" + r.Targets.Count + "[" + string.Join("+", r.Targets.Select(NameOf).ToArray()) + "]").ToArray()));
                    checks.Add(new Check(rows.Count == 6 && rows.All(r => r.Targets != null && r.Targets.Count == 2),
                        $"LAE 期望 6 行 nTargets=2（3 敌 × 2 己笛卡尔），实际 {rows.Count} 行"));
                    // 行序=原子执行序：目标0 须敌方域（对方生物或对方角色）、目标1 须己方域（己方生物或己方角色）
                    bool IsEnemySide(Entity t) => ReferenceEquals(t, p2) || (t is Card tc && tc.GetController() == p2);
                    bool IsOwnSide(Entity t) => ReferenceEquals(t, p1) || (t is Card oc && oc.GetController() == p1);
                    checks.Add(new Check(rows.All(r => IsEnemySide(r.Targets[0]) && IsOwnSide(r.Targets[1])),
                        "笛卡尔行序须为 [敌方目标, 己方目标]（返回序=原子执行序）"));

                    // AI：SimpleAI.ChooseTargets 逐原子预选（两域各一）
                    var ai = new SimpleAI();
                    var mi = typeof(SimpleAI).GetMethod("ChooseTargets", BindingFlags.NonPublic | BindingFlags.Instance);
                    var picked = mi.Invoke(ai, new object[] { core, p1, tCard, 0 }) as List<Entity>;
                    bool aiOk = picked != null && picked.Count == 2
                        && picked[0] is Card pc0 && pc0.GetController() == p2
                        && (ReferenceEquals(picked[1], p1) || (picked[1] is Card pc1 && pc1.GetController() == p1));
                    log.AppendLine("[逐原子] AI picked=" + (picked == null ? "null" : string.Join("+", picked.Select(NameOf).ToArray())));
                    checks.Add(new Check(aiOk, $"AI 期望 2 目标（敌一 + 己一·不同域），实际 " + (picked == null ? "null" : picked.Count + " 个")));

                    // 引擎：交叉预选 [伤=火种, 疗=巨像] —— 按原子序消费
                    int e1L = EntityEffectExtensions.GetLife(e1), e2L = EntityEffectExtensions.GetLife(e2), owL = EntityEffectExtensions.GetLife(ow);
                    var ok = GameActions.PlayCard(core, p1, tCard, new List<Entity> { e2, ow }, Zone.Hand, 0);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    int dE1 = EntityEffectExtensions.GetLife(e1) - e1L, dE2 = EntityEffectExtensions.GetLife(e2) - e2L;
                    int dOw = EntityEffectExtensions.GetLife(ow) - owL, dHero = p1.Life - heroBefore;
                    int wantDE2 = -Math.Min(2, e2L); // 1 血单位受 2 伤=死亡钳 0（Δ=-1），高血单位 Δ=-2
                    log.AppendLine($"[逐原子] 引擎交叉结算 Δ：火灵={dE1} 火种={dE2} 巨像={dOw} 角色={dHero}");
                    checks.Add(new Check(ok && dE2 == wantDE2 && dOw == +2 && dE1 == 0 && dHero == 0,
                        $"引擎期望 伤只落在火种（Δ={wantDE2}·血量钳底）、疗+2 只落在巨像（其余 0），实际 Δ火灵={dE1} Δ火种={dE2} Δ巨像={dOw} Δ角色={dHero}"));
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        /// <summary>内存合成临时卡（不落盘）：DealDamage2(kinds=[2]) + Heal2(kinds=[1])，header 无 kinds。</summary>
        private static CardData BuildTempPerAtomCard()
        {
            var dmg = Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage);
            var heal = Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Heal);
            var eff = new CardEffectData
            {
                Id = "EFF_TMP_PROBE",
                DisplayName = "伤2+疗2",
                TriggerTiming = 19, // Activate_Active（法术施放档，与诱敌同）
                ActivationType = 2, // Voluntary
                SelectionMode = -1, // None：选择参数在原子上（PerAtomTargets）
                TargetCount = -2,   // 未声明——每原子 count=1 显式声明
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry { refId = dmg.HashId, value = 2, kinds = new List<int> { 2 }, count = 1 },
                    new AtomicEffectEntry { refId = heal.HashId, value = 2, kinds = new List<int> { 1 }, count = 1 },
                },
            };
            return new CardData { ID = "C_TMP_PERATOM_PROBE", CardName = "探针·逐原子", Supertype = Cardtype.Spell, Effects = new List<CardEffectData> { eff } };
        }

        // ======================================== ③ 并列效果分段选目标（弹回2 + 回收2） ========================================

        /// <summary>并列效果分段选取端到端（2026-11-02「取消并列收缩作用域」配套验收）：
        /// 内存合成临时卡 = 并列两原子〔弹回×2（对方单位·NoRole 滤）+ 回收×2（己方墓地）〕，
        /// 合成器数据形状（header 无 kinds → PerAtomTargets；每原子 value+count 各自声明）。
        /// 三面断言：LAE 逐原子组合展开 = C(3,2)×C(4,2)=18 行、行内 4 目标分两段（前 2 敌方战场/
        /// 后 2 己方墓地）；SimpleAI 预选 4 目标（2 敌 + 2 墓地卡）；引擎交叉预选结算——弹回只落
        /// 指定两敌（入对方手牌·持有者路由）、回收只落指定两卡（入己方手牌），其余候选零变化。</summary>
        private static void RunParallelBounceRecoverScenario(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            var tmp = BuildParallelBounceRecoverCard();

            // 数据链：header 无 kinds → PerAtomTargets=true，两原子各自域/数量
            var def = CardEffectConverter.ConvertAll(tmp.Effects, tmp.ID).First();
            checks.Add(new Check(def.PerAtomTargets, "并列临时卡 PerAtomTargets 应为 true（header 未声明 kinds）"));
            checks.Add(new Check(def.Effects.Count == 2
                && def.Effects[0].EffectiveTargetCount(def) == 2 && def.Effects[1].EffectiveTargetCount(def) == 2,
                $"并列两原子 effCount 均应为 2，实际 {def.Effects.Count} 原子："
                + string.Join(" ; ", def.Effects.Select(a => $"{a.Type}#{a.EffectiveTargetCount(def)}").ToArray())));

            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 4; i++) deck1.Add(tmp);
            for (int i = 0; i < 6; i++) deck1.Add(ByName(catalog, EnemyCName)); // 填充兼墓地素材（火炬 1/1）
            var deck2 = new List<CardData>();
            for (int i = 0; i < 4; i++) { deck2.Add(ByName(catalog, EnemyAName)); deck2.Add(ByName(catalog, EnemyBName)); deck2.Add(ByName(catalog, EnemyCName)); }
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

                    // 敌方战场：火灵/火种/火炬各一（弹回候选=3——NoRole 滤掉双方角色）
                    var e1 = FindAndMoveByName(zm, p2, EnemyAName, Zone.Battlefield);
                    var e2 = FindAndMoveByName(zm, p2, EnemyBName, Zone.Battlefield);
                    var e3 = FindAndMoveByName(zm, p2, EnemyCName, Zone.Battlefield);
                    // 己方墓地：火炬×4（回收候选）
                    var grave = new List<Card>();
                    while (grave.Count < 4)
                    {
                        var c = TakeByName(zm, p1, EnemyCName, Zone.Deck) ?? TakeByName(zm, p1, EnemyCName, Zone.Hand);
                        if (c == null) break;
                        zm.MoveCard(c, p1, zm.IsCardInZone(c, p1, Zone.Hand) ? Zone.Hand : Zone.Deck, Zone.Graveyard);
                        grave.Add(c);
                    }
                    // 手里恰好一张临时卡；多余副本沉回牌库（防污染墓地候选）
                    var tCard = FindAndMoveByName(zm, p1, tmp.CardName, Zone.Hand);
                    foreach (var c in zm.GetCards(p1, Zone.Hand).Where(c => IsName(c, tmp.CardName) && c != tCard).ToList())
                        zm.MoveCard(c, p1, Zone.Hand, Zone.Deck);
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 12);

                    var setupOk = e1 != null && e2 != null && e3 != null && grave.Count == 4 && tCard != null;
                    checks.Add(new Check(setupOk,
                        $"夹具：敌三 {e1 != null}{e2 != null}{e3 != null} 墓地四 {grave.Count} 手卡 {tCard != null}"));
                    if (!setupOk) return;

                    var ctx = new EffectExecutionContext { Source = p1, Controller = p1, ZoneManager = zm, ElementPool = core.ElementPool, ModeIndex = 0 };
                    var specs = Attribute.EffectHandlerRegistry.CollectAtomTargetSpecs(def, ctx);
                    checks.Add(new Check(specs.Count == 2 && specs[0].Candidates.Count == 3 && specs[1].Candidates.Count == 4
                        && !specs[0].AutoTake && !specs[1].AutoTake,
                        "原子规格期望 弹回(3 候选)+回收(4 候选) 且均占选择权，实际 "
                        + string.Join(" ; ", specs.Select(s => $"{s.Atom.Type}:{s.Candidates.Count}c/auto={s.AutoTake}").ToArray())));

                    // LAE：分段组合展开 C(3,2)×C(4,2)=18 行，行内 4 目标（前 2 敌方战场 / 后 2 己方墓地）
                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    var rows = lae.Actions.Where(a => a.Type == TideActionType.PlayCard && ReferenceEquals(a.Card, tCard)).ToList();
                    var enemySet = new HashSet<Card> { e1, e2, e3 };
                    var graveSet = new HashSet<Card>(grave);
                    bool rowsOk = rows.Count == 18 && rows.All(r => r.Targets != null && r.Targets.Count == 4
                        && enemySet.Contains(r.Targets[0] as Card) && enemySet.Contains(r.Targets[1] as Card)
                        && graveSet.Contains(r.Targets[2] as Card) && graveSet.Contains(r.Targets[3] as Card)
                        && !ReferenceEquals(r.Targets[0], r.Targets[1]) && !ReferenceEquals(r.Targets[2], r.Targets[3]));
                    log.AppendLine("[并列弹回回收] LAE 行数=" + rows.Count + "（前 2 行示例："
                        + string.Join(" ; ", rows.Take(2).Select(r => string.Join("+", r.Targets.Select(NameOf).ToArray())).ToArray()) + "）");
                    checks.Add(new Check(rowsOk,
                        $"LAE 期望 18 行（C(3,2)×C(4,2)）各 4 目标分段〔敌×2+墓地×2〕，实际 {rows.Count} 行"));

                    // AI：SimpleAI 逐原子预选 4 目标（2 敌 + 2 墓地卡）
                    var ai = new SimpleAI();
                    var mi = typeof(SimpleAI).GetMethod("ChooseTargets", BindingFlags.NonPublic | BindingFlags.Instance);
                    var picked = mi.Invoke(ai, new object[] { core, p1, tCard, 0 }) as List<Entity>;
                    bool aiOk = picked != null && picked.Count == 4
                        && picked.Take(2).All(t => enemySet.Contains(t as Card))
                        && picked.Skip(2).All(t => graveSet.Contains(t as Card));
                    log.AppendLine("[并列弹回回收] AI picked=" + (picked == null ? "null" : string.Join("+", picked.Select(NameOf).ToArray())));
                    checks.Add(new Check(aiOk, $"AI 期望 4 目标分段（敌 2 + 墓地 2），实际 " + (picked == null ? "null" : picked.Count + " 个")));

                    // 引擎：交叉预选 [弹回=火种+火炬敌, 回收=墓地3+墓地4] —— 按原子序分段消费
                    var chosen = new List<Entity> { e2, e3, grave[2], grave[3] };
                    var ok = GameActions.PlayCard(core, p1, tCard, chosen, Zone.Hand, 0);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    bool engineOk = ok
                        && zm.IsCardInZone(e2, p2, Zone.Hand) && zm.IsCardInZone(e3, p2, Zone.Hand) // 弹回=回持有者(P2)手牌
                        && zm.IsCardInZone(e1, p2, Zone.Battlefield)                                 // 未选敌不动
                        && zm.IsCardInZone(grave[2], p1, Zone.Hand) && zm.IsCardInZone(grave[3], p1, Zone.Hand) // 回收入己方手牌
                        && zm.IsCardInZone(grave[0], p1, Zone.Graveyard) && zm.IsCardInZone(grave[1], p1, Zone.Graveyard); // 未选卡留墓地
                    log.AppendLine($"[并列弹回回收] 引擎交叉结算：火种入手2={zm.IsCardInZone(e2, p2, Zone.Hand)} 火炬敌入手2={zm.IsCardInZone(e3, p2, Zone.Hand)}"
                        + $" 火灵留场={zm.IsCardInZone(e1, p2, Zone.Battlefield)} 墓3入手1={zm.IsCardInZone(grave[2], p1, Zone.Hand)} 墓4入手1={zm.IsCardInZone(grave[3], p1, Zone.Hand)}"
                        + $" 墓1留={zm.IsCardInZone(grave[0], p1, Zone.Graveyard)} 墓2留={zm.IsCardInZone(grave[1], p1, Zone.Graveyard)}");
                    checks.Add(new Check(engineOk,
                        "引擎分段结算：弹回两指定敌应入 P2 手牌、回收两指定卡应入 P1 手牌、其余候选不动（见上行读数）"));
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        /// <summary>内存合成并列卡（不落盘）：弹回 value=2·kinds=[2]·count=2 ＋ 回收 value=2·kinds=[9]·count=2，
        /// header 无 kinds/selection=None（合成器保存形状——逐原子目标制）。</summary>
        private static CardData BuildParallelBounceRecoverCard()
        {
            var bounce = Attribute.AtomicEffectTable.GetByType(AtomicEffectType.ReturnToHand);
            var recover = Attribute.AtomicEffectTable.GetByType(AtomicEffectType.RecoverToHand);
            var eff = new CardEffectData
            {
                Id = "EFF_TMP_PARALLEL",
                DisplayName = "弹二收二",
                TriggerTiming = 19,
                ActivationType = 2,
                SelectionMode = -1,
                TargetCount = -2,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry { refId = bounce.HashId, value = 2, kinds = new List<int> { 2 }, count = 2 },
                    new AtomicEffectEntry { refId = recover.HashId, value = 2, kinds = new List<int> { 9 }, count = 2 },
                },
            };
            return new CardData { ID = "C_TMP_PARALLEL_PROBE", CardName = "探针·并列弹回回收", Supertype = Cardtype.Spell, Effects = new List<CardEffectData> { eff } };
        }

        /// <summary>按卡名取出一首张（不移区——调用方自行 MoveCard）。</summary>
        private static Card TakeByName(ZoneManager zm, Player owner, string name, Zone zone)
            => zm.GetCards(owner, zone).FirstOrDefault(c => IsName(c, name));

        // ======================================== 夹具辅助 ========================================

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

        /// <summary>诱敌夹具卡合成（2026-11-06）：诱敌卡（C_AC7E3467，卡级蓝抽卡代价×红效果=④同色
        /// 违规）已删出教学池——本验证改从教学效果库取条目合成：效果 88C651B8（Multiple·count=2·
        /// 两敌弹回）保留在库专供本夹具，形态断言口径不变；「对方抽1」代价行 2026-11-08 效果级化
        /// 改挂该效果 Costs（主动行=法术施放净费账单——对局行为前提等价）。ToGraph 不回填
        /// header.Steps，手动接 graph.steps（同 EffectLibrarySerializer.DeriveAnchorCost 口径）。
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
