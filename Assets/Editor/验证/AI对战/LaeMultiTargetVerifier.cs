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
    /// ⑤ P0 无域原子 prefix-k（2026-11-19，AI动作空间补全 §1.4 B）——牺牲/蛰伏（原子无 Domain、
    ///    作用域写死在 handler）：T5 纯池 11 地=11 行 k=1..11、T7 场上阿格洛斯被排除、蛰伏 k=2 恰
    ///    2 张横置计层；应用走 Apply→ActivateHeroSkill 预选透传，handler 消费。
    /// ⑥ P1 移动/宣言交互行（2026-11-19 v0.3 §6.2/6.3/6.4）——训练·疾行者（MoveUnit）行=单位×
    ///    合法空格、[14]/[15]=格坐标、满盘 0 行；训练·强夺/窃取/检索行=候选名、[16] 序号互异、
    ///    命中迁移、无展示 0 行、手牌/牌库候选不混区；SimpleAI 端 PickMoveCell/PickDeclaration/空集不发动。
    /// ⑦ P2 落格专项（2026-11-19 反馈）——空场生物行=9（[17]=0..8 互异）、应用 cell8 实际落格、
    ///    占格递减、满区 0 行、法术行 -1。⑧ P3.1 弃牌专项——EnumerateDiscardChoices over=2/3 组合行、
    ///    特征 [10]、停靠门 Park/Snapshot/Complete/非模型座回落。⑨ P3.2 预言专项——合成预言卡 3 行
    ///    （Type:*）、[16]=0/1/2、Signature 互异（反馈回归）、应用直通。
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
        // P0 无域原子 prefix-k 场景（2026-11-19）：牺牲/蛰伏夹具素材（卡名定位——ID 随内容重推漂移）
        private const string LandName = "教学·1";             // 地（池候选素材）
        private const string AgrosName = "系命者 阿格洛斯";   // 场上排除项（HeroSkillTargetPolicy.OrderSacrifice 白名单排除）

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
                RunTargetZoneFeatureScenario(log, checks);
                RunNoDomainPrefixScenarios(log, checks);
                RunMoveDeclareScenarios(log, checks);
                RunPlacementScenario(log, checks);      // ⑦ P2 落格专项
                RunDiscardChoiceScenario(log, checks); // ⑧ P3.1 弃牌专项
                RunProphecyScenario(log, checks);      // ⑨ P3.2 预言三选一专项
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
                    CheckZoneFeatures(lae, core, p1, checks, "诱敌场景");
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
                    CheckZoneFeatures(lae, core, p1, checks, "逐原子场景");
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
                    CheckZoneFeatures(lae, core, p1, checks, "并列弹回回收场景");
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

        // ======================================== ④ 目标区域特征（v14 [12]/[13]，2026-11-15 规格 v0.2 §9.5） ========================================

        /// <summary>盘面：己方元素池 2 张地 + 战场 1 只己方生物（教学·2=牺牲原子宿主）→
        /// LAE 的 Activate 行按首目标区域携带 [12]targetZoneRank / [13]targetSideRank：
        /// 池内地行 (2,0)、战场生物行 (1,0)、无目标行（EndTurn）(-1,-1)；全行恒等式=与
        /// TargetZoneGrouping 单源判定逐行一致。</summary>
        private static void RunTargetZoneFeatureScenario(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            var unitData = ByName(catalog, "教学·2");
            var landData = ByName(catalog, "教学·1");
            checks.Add(new Check(unitData != null && landData != null, "教学·2/教学·1 在卡池（区域特征场景前提）"));
            if (unitData == null || landData == null) return;

            var core = GameCore.Instance;
            var deck1 = new List<CardData> { unitData, landData, landData, landData };
            var deck2 = new List<CardData> { unitData, unitData };
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            p1.IsAI = false; core.Player2.IsAI = false;
            using (var board = new GameBoard.BoardState(core, p1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    var unit = TakeByName(zm, p1, "教学·2", Zone.Hand) ?? TakeByName(zm, p1, "教学·2", Zone.Deck);
                    checks.Add(new Check(unit != null, "教学·2 可寻"));
                    if (unit == null) return;
                    zm.MoveCard(unit, p1, unit.GetZone(), Zone.Battlefield);
                    var lands = new List<Card>();
                    foreach (var c in zm.GetCards(p1, Zone.Hand).Concat(zm.GetCards(p1, Zone.Deck))
                                 .Where(c => IsName(c, "教学·1")).ToList())
                    {
                        if (lands.Count >= 2) break;
                        lands.Add(c);
                    }
                    // 直接入池（绕地牌槽上限——夹具播种；区域真源经 MoveCard 同步，镜像
                    // GameActions.AddToElementPool 的落地两步：PooledCards.Add + Hand→ElementPool）
                    var pool = core.ElementPool.GetPool(p1);
                    foreach (var c in lands)
                    {
                        pool.PooledCards.Add(new PooledCard(c, ManaType.Green));
                        zm.MoveCard(c, p1, c.GetZone(), Zone.ElementPool);
                    }
                    checks.Add(new Check(lands.Count == 2, $"池内 2 张教学·1（实际 {lands.Count}）"));

                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    CheckZoneFeatures(lae, core, p1, checks, "区域特征场景");

                    var actRows = lae.Actions
                        .Where(a => a.Type == TideActionType.Activate && ReferenceEquals(a.Card, unit)).ToList();
                    var poolRows = actRows.Where(r => r.Targets != null && r.Targets.Count > 0
                        && r.Targets[0] is Card tc && tc.GetZone() == Zone.ElementPool).ToList();
                    var fieldRows = actRows.Where(r => r.Targets != null && r.Targets.Count > 0
                        && r.Targets[0] is Card tc2 && tc2.GetZone() == Zone.Battlefield).ToList();
                    log.AppendLine($"[区域特征] Activate 行={actRows.Count}（池首目标 {poolRows.Count} / 场首目标 {fieldRows.Count}）");
                    checks.Add(new Check(poolRows.Count > 0 && poolRows.All(r =>
                            FeatureOf(lae, r, 12) == 2f && FeatureOf(lae, r, 13) == 0f),
                        $"首目标=元素池地行 [12]=2/[13]=0（池首目标行 {poolRows.Count}）"));
                    checks.Add(new Check(fieldRows.Count > 0 && fieldRows.All(r =>
                            FeatureOf(lae, r, 12) == 1f && FeatureOf(lae, r, 13) == 0f),
                        $"首目标=战场生物行 [12]=1（场首目标行 {fieldRows.Count}）"));
                    var endRow = lae.Actions.FirstOrDefault(a => a.Type == TideActionType.EndTurn);
                    // 2026-11-15 DSH 修：TideAction 已是 struct，FirstOrDefault 返回 default，
                    // 不能再用 `endRow != null`（CS0019）。EndTurn=4 ≠ default(0=PlayCard)，
                    // 故以 Type 判"是否枚举到该行"，语义与原 `!= null` 等价。
                    checks.Add(new Check(endRow.Type == TideActionType.EndTurn
                            && FeatureOf(lae, endRow, 12) == -1f && FeatureOf(lae, endRow, 13) == -1f,
                        "无目标行（EndTurn）[12]=[13]=-1"));
                }
                finally { }
            }
        }

        // ======================================== ⑤ P0 无域原子 prefix-k（牺牲/蛰伏，2026-11-19） ========================================

        /// <summary>P0 无域原子目标（docs/AI动作空间补全_修改计划.md §1.4 B）：牺牲（C_AC7FC01F/FXGREEN08）
        /// 与蛰伏（C_23EE2486/E31E9EB0）的原子行无 Domain、作用域写死在 handler——改造前
        /// CollectAtomTargetSpecs 跳过无域原子，HeroSkill 只剩一条 Targets=null 行（点牺牲=空放）。
        /// 改造后白名单（NoDomainTargetScopes）按域串产候选、HeroSkillTargetPolicy 共享序排序，
        /// LAE 产 prefix-k 前缀行（k=1..N——模型可选「祭几张」，k=Targets 长度）。
        /// 三盘面：T5=纯池 11 地（11 行，应用 k=11 全献）；T7=场上阿格洛斯+池 11 地（行不含场上
        /// 阿格洛斯，应用 k=6 恰献 6 张池地）；蓝蛰伏=池 3 未横置地（3 行，应用 k=2 恰 2 张横置
        /// 计层、第 3 张不动）。另附无目标行回归（EndTurn [10]=0、[12]/[13]=-1）。</summary>
        private static void RunNoDomainPrefixScenarios(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            var greenSkill = catalog.FirstOrDefault(c => c.ID == "C_AC7FC01F"); // 牺牲
            var blueSkill = catalog.FirstOrDefault(c => c.ID == "C_23EE2486");  // 蛰伏
            checks.Add(new Check(greenSkill != null && blueSkill != null,
                "牺牲/蛰伏技能卡在卡池（P0 场景前提）"));
            if (greenSkill == null || blueSkill == null) return;

            RunSacrificeBoard(catalog, log, checks, fieldAgros: false); // T5
            RunSacrificeBoard(catalog, log, checks, fieldAgros: true);  // T7
            RunDormancyBoard(catalog, log, checks);                    // 蓝蛰伏
        }

        /// <summary>牺牲盘面（T5/T7）：绿技能=牺牲；元素池直入 11 张地（夹具播种绕槽上限，
        /// 镜像 ④ 的池两步落地）；fieldAgros=true 时场上加 1 张阿格洛斯（OrderSacrifice 排除项）。</summary>
        private static void RunSacrificeBoard(List<CardData> catalog, StringBuilder log, List<Check> checks, bool fieldAgros)
        {
            string tag = fieldAgros ? "P0牺牲T7" : "P0牺牲T5";
            var landData = ByName(catalog, LandName);
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 12; i++) deck1.Add(landData); // 11 张入池 + 1 张余量
            if (fieldAgros) deck1.Add(ByName(catalog, AgrosName));
            var deck2 = new List<CardData>();
            for (int i = 0; i < 4; i++) deck2.Add(ByName(catalog, OwnBigName)); // 对面填充（防开抽抽空）
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1),
                skillCardId1: "C_AC7FC01F");
            var p1 = core.Player1;
            p1.IsAI = false; core.Player2.IsAI = false;
            using (var board = new GameBoard.BoardState(core, p1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10); // 技能锚价可付（夹具脱敏）

                    // 池直入 11 张地（MoveCard 同步区域真源——镜像 GameActions.AddToElementPool 两步）
                    var lands = new List<Card>();
                    foreach (var c in zm.GetCards(p1, Zone.Deck).Concat(zm.GetCards(p1, Zone.Hand))
                                 .Where(c => IsName(c, LandName)))
                    {
                        if (lands.Count >= 11) break;
                        lands.Add(c);
                    }
                    var pool = core.ElementPool.GetPool(p1);
                    foreach (var c in lands)
                    {
                        pool.PooledCards.Add(new PooledCard(c, ManaType.Green));
                        zm.MoveCard(c, p1, c.GetZone(), Zone.ElementPool);
                    }
                    Card agros = null;
                    if (fieldAgros) agros = FindAndMoveByName(zm, p1, AgrosName, Zone.Battlefield);
                    checks.Add(new Check(lands.Count == 11 && (!fieldAgros || agros != null),
                        $"{tag} 夹具：池 11 地（实际 {lands.Count}）{(fieldAgros ? "+ 场上阿格洛斯" : "")}"));
                    if (lands.Count < 11) return;

                    var skillCard = HeroSkillSystem.ResolveSkillCard(core, p1);
                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    CheckZoneFeatures(lae, core, p1, checks, tag);
                    var rows = lae.Actions
                        .Where(a => a.Type == TideActionType.HeroSkill && ReferenceEquals(a.Card, skillCard)).ToList();
                    string Describe(TideAction a) => "n=" + (a.Targets?.Count ?? -1);
                    log.AppendLine($"[{tag}] HeroSkill 行数={rows.Count} :: {string.Join(" ; ", rows.Select(Describe).ToArray())}");

                    checks.Add(new Check(rows.Count == 11 && rows.All(r => r.Targets != null),
                        $"{tag} 期望 11 行 prefix-k（k=1..11）且无 Targets=null 行，实际 {rows.Count} 行"));
                    checks.Add(new Check(rows.Count == 11
                        && rows.Select(r => r.Targets.Count).SequenceEqual(Enumerable.Range(1, 11)),
                        $"{tag} 第 k 行 Targets.Count==k（前缀行序）"));
                    checks.Add(new Check(rows.Count == 11 && rows.All(r => FeatureOf(lae, r, 10) == r.Targets.Count
                            && FeatureOf(lae, r, 12) == 2f && FeatureOf(lae, r, 13) == 0f),
                        $"{tag} 全行 [10]==k（0..16 钳后 k=9..11 仍可分）、首目标 [12]=2/[13]=0（元素池/己方）"));
                    if (fieldAgros)
                        checks.Add(new Check(rows.All(r => r.Targets.All(t => !ReferenceEquals(t, agros))),
                            $"{tag} 行内目标不含场上的阿格洛斯（OrderSacrifice 排除）"));

                    // 无目标行回归：EndTurn [10]=0、[12]=[13]=-1
                    var endRow = lae.Actions.FirstOrDefault(a => a.Type == TideActionType.EndTurn);
                    checks.Add(new Check(endRow.Type == TideActionType.EndTurn
                            && FeatureOf(lae, endRow, 10) == 0f
                            && FeatureOf(lae, endRow, 12) == -1f && FeatureOf(lae, endRow, 13) == -1f,
                        $"{tag} 无目标行回归：EndTurn [10]=0、[12]=[13]=-1"));

                    // 应用：T5=全取 k=11；T7=k=6（恰献 6 张池地，阿格洛斯留场）
                    int applyK = fieldAgros ? 6 : 11;
                    var row = rows.FirstOrDefault(r => r.Targets.Count == applyK);
                    var ok = LegalActionEnumerator.Apply(core, p1, row);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    int graveLands = zm.GetCards(p1, Zone.Graveyard).Count(c => IsName(c, LandName));
                    int poolLeft = core.ElementPool.GetPooledCards(p1).Count;
                    int tempAny = core.ElementPool.GetPool(p1).TempElements.TryGetValue(ManaType.Any, out var ta) ? ta : 0;
                    bool agrosSafe = !fieldAgros || (zm.IsCardInZone(agros, p1, Zone.Battlefield) && agros.IsAlive);
                    log.AppendLine($"[{tag}] 应用 k={applyK}：ok={ok} 池余={poolLeft} 墓地地={graveLands} "
                        + $"临时彩={tempAny} 阿格洛斯留场={agrosSafe} 技能横置={skillCard.IsTapped()}");
                    checks.Add(new Check(ok && poolLeft == 11 - applyK && graveLands == applyK
                            && tempAny == applyK && agrosSafe && skillCard.IsTapped(),
                        $"{tag} 应用 k={applyK}：{applyK} 张池地入墓、临时彩=={applyK}、池余 {11 - applyK}、技能横置"
                        + (fieldAgros ? "、阿格洛斯仍在场" : "")));
                }
                finally { }
            }
        }

        /// <summary>蓝蛰伏盘面：蓝技能=蛰伏；池 3 张未横置地 → 3 行 prefix-k；应用 k=2 恰 2 张
        /// 横置+各 1 层 DormancyLayers，第 3 张不动。</summary>
        private static void RunDormancyBoard(List<CardData> catalog, StringBuilder log, List<Check> checks)
        {
            var landData = ByName(catalog, LandName);
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 5; i++) deck1.Add(landData); // 3 张入池 + 余量
            var deck2 = new List<CardData>();
            for (int i = 0; i < 4; i++) deck2.Add(ByName(catalog, OwnBigName));
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1),
                skillCardId1: "C_23EE2486");
            var p1 = core.Player1;
            p1.IsAI = false; core.Player2.IsAI = false;
            using (var board = new GameBoard.BoardState(core, p1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10);

                    var lands = new List<Card>();
                    foreach (var c in zm.GetCards(p1, Zone.Deck).Concat(zm.GetCards(p1, Zone.Hand))
                                 .Where(c => IsName(c, LandName)))
                    {
                        if (lands.Count >= 3) break;
                        lands.Add(c);
                    }
                    var pool = core.ElementPool.GetPool(p1);
                    foreach (var c in lands)
                    {
                        pool.PooledCards.Add(new PooledCard(c, ManaType.Blue));
                        zm.MoveCard(c, p1, c.GetZone(), Zone.ElementPool);
                    }
                    checks.Add(new Check(lands.Count == 3, $"P0蛰伏 夹具：池 3 未横置地（实际 {lands.Count}）"));
                    if (lands.Count < 3) return;

                    var skillCard = HeroSkillSystem.ResolveSkillCard(core, p1);
                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    CheckZoneFeatures(lae, core, p1, checks, "P0蛰伏");
                    var rows = lae.Actions
                        .Where(a => a.Type == TideActionType.HeroSkill && ReferenceEquals(a.Card, skillCard)).ToList();
                    log.AppendLine($"[P0蛰伏] HeroSkill 行数={rows.Count} :: "
                        + string.Join(" ; ", rows.Select(r => "n=" + (r.Targets?.Count ?? -1)).ToArray()));
                    checks.Add(new Check(rows.Count == 3
                            && rows.Select(r => r.Targets?.Count ?? -1).SequenceEqual(new[] { 1, 2, 3 })
                            && rows.All(r => FeatureOf(lae, r, 12) == 2f && FeatureOf(lae, r, 13) == 0f),
                        "P0蛰伏 期望 3 行 prefix-k（k=1..3）首目标=元素池"));

                    var row = rows.FirstOrDefault(r => r.Targets.Count == 2);
                    var ok = LegalActionEnumerator.Apply(core, p1, row);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    var pooled = core.ElementPool.GetPooledCards(p1);
                    int tappedLayers = pooled.Count(pc => pc != null && pc.IsTapped && pc.DormancyLayers == 1);
                    int untouched = pooled.Count(pc => pc != null && !pc.IsTapped && pc.DormancyLayers == 0);
                    log.AppendLine($"[P0蛰伏] 应用 k=2：ok={ok} 横置+1层={tappedLayers} 未动={untouched} 技能横置={skillCard.IsTapped()}");
                    checks.Add(new Check(ok && tappedLayers == 2 && untouched == 1 && skillCard.IsTapped(),
                        "P0蛰伏 应用 k=2：恰 2 张横置+各 1 层、第 3 张不动、技能横置"));
                }
                finally { }
            }
        }

        // ======================================== ⑥ P1 移动/宣言交互行（2026-11-19 v0.3） ========================================

        /// <summary>P1 v0.3（docs/AI动作空间_移动与宣言_规格_v0.3.md §6.2/6.3/6.4）：移动选格 +
        /// 宣言卡名进 LAE 动作空间（InteractionParams 透传）。移动盘面=训练·疾行者（MoveUnit 主动）：
        /// 行=单位×合法空格、特征 [14]/[15]=格坐标、应用后单位实际落格、满盘 0 行、SimpleAI 端
        /// PickMoveCell 默认首格；宣言盘面=训练·强夺/窃取（对手手牌/牌库已展示卡名）与训练·检索
        ///（己方牌库去重名）：行=候选名、[16] 候选序号互异、应用后命中迁移、无展示 0 行、
        /// 手牌/牌库候选互不混区、SimpleAI 端候选空=不发动。</summary>
        private static void RunMoveDeclareScenarios(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            var runner = catalog.FirstOrDefault(c => IsName(c, "训练·疾行者"));   // MoveUnit 主动（BA66529D）
            var seize = catalog.FirstOrDefault(c => IsName(c, "训练·强夺"));      // DeclareHand（对手手牌已展示）
            var steal = catalog.FirstOrDefault(c => IsName(c, "训练·窃取"));      // DeclareDeck（对手牌库已展示）
            var search = catalog.FirstOrDefault(c => IsName(c, "训练·检索"));     // SearchDeck（己方牌库去重名）
            checks.Add(new Check(runner != null && seize != null && steal != null && search != null,
                "训练·疾行者/强夺/窃取/检索 在卡池（P1 场景前提）"));
            if (runner == null || seize == null || steal == null || search == null) return;

            RunMoveScenario(catalog, log, checks, runner);
            RunDeclareScenario(catalog, log, checks, seize, steal);
            RunSearchScenario(catalog, log, checks, search);
        }

        /// <summary>移动盘面：疾行者（己方战场，钉格）+若干填充单位；LAE Activate 行=单位×合法空格。</summary>
        private static void RunMoveScenario(List<CardData> catalog, StringBuilder log, List<Check> checks, CardData runner)
        {
            var unitData = ByName(catalog, "教学·2"); // 填充单位（兼移动目标候选）
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 2; i++) deck1.Add(runner);
            for (int i = 0; i < 10; i++) deck1.Add(unitData); // 满盘场景的填充材
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) deck2.Add(ByName(catalog, EnemyAName));
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            p1.IsAI = false; core.Player2.IsAI = false;
            using (var board = new GameBoard.BoardState(core, p1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                GameBoard.MoveUnitRules.Attach(board); // P1：移动原子棋盘装配（BattleController/AiDuelRunner 同款）
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10);

                    // 场上：疾行者钉在首格（含 CanActivate 主动口）
                    var r0 = FindAndMoveByName(zm, p1, "训练·疾行者", Zone.Battlefield);
                    var first = GameBoard.BoardLayout.UnitCells(0)[0];
                    board.PinCard(r0, first.x, first.z);
                    checks.Add(new Check(r0 != null, "P1移动 夹具：疾行者上field钉格"));

                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    CheckZoneFeatures(lae, core, p1, checks, "P1移动");
                    var rows = lae.Actions
                        .Where(a => a.Type == TideActionType.Activate && ReferenceEquals(a.Card, r0)).ToList();
                    var legal = GameBoard.MoveUnitRules.LegalDestinations(r0);
                    string D(TideAction a) => $"({a.Interaction?.MoveX},{a.Interaction?.MoveZ})";
                    log.AppendLine($"[P1移动] Activate 行数={rows.Count}（合法空格 {legal.Count}）:: "
                        + string.Join(" ; ", rows.Select(D).ToArray()));

                    checks.Add(new Check(rows.Count == legal.Count,
                        $"P1移动 行数=合法空格数（{rows.Count} 行 vs {legal.Count} 格）"));
                    if (rows.Count == 0) return; // 行缺失已计 FAIL——后续应用断言无对象，防 default(struct) NRE
                    checks.Add(new Check(rows.All(r => r.Interaction != null && r.Interaction.HasMoveCell
                            && legal.Any(c => c.x == r.Interaction.MoveX && c.z == r.Interaction.MoveZ)
                            && FeatureOf(lae, r, 14) == r.Interaction.MoveX
                            && FeatureOf(lae, r, 15) == r.Interaction.MoveZ
                            && FeatureOf(lae, r, 16) == -1f),
                        "P1移动 全行 [14]/[15]=格坐标、[16]=-1（非宣言行）"));

                    // 应用：选末行（非首格）→ 单位实际落格
                    var pick = rows[rows.Count - 1];
                    var ok = LegalActionEnumerator.Apply(core, p1, pick);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    bool moved = board.TryGetCell(r0, out int nx, out int nz)
                                 && nx == pick.Interaction.MoveX && nz == pick.Interaction.MoveZ;
                    log.AppendLine($"[P1移动] 应用格=({pick.Interaction.MoveX},{pick.Interaction.MoveZ}) → 实际=({nx},{nz}) ok={ok}");
                    checks.Add(new Check(ok && moved, "P1移动 应用行后单位实际落格（Interaction 透传 → MoveUnitHandler）"));
                    r0.Untap(); // 应用横置了源卡——满盘/反射段用第二张前先复原首张可用性

                    // SimpleAI 端：TryBuildInteraction（反射）默认首格——须在满盘 setup 前有空格
                    var ai = new SimpleAI();
                    var mi = typeof(SimpleAI).GetMethod("TryBuildInteraction",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var r0cells = GameBoard.MoveUnitRules.LegalDestinations(r0);
                    object[] args = { core, p1, r0, CardEffectConverter.ConvertAll(runner.Effects, runner.ID).First(),
                        new List<Entity> { r0 }, null };
                    bool aiMove = (bool)mi.Invoke(ai, args);
                    var aiInter = args[5] as InteractionParams; // out 参在末位（第 6 参）
                    checks.Add(new Check(r0cells.Count > 0 && aiMove && aiInter != null
                            && aiInter.MoveX == r0cells[0].x && aiInter.MoveZ == r0cells[0].z,
                        $"SimpleAI PickMoveCell 默认首格（{aiInter?.MoveX},{aiInter?.MoveZ}）"));

                    // 满盘：9 单位区全占 → 0 行
                    var fillers = new List<Card>();
                    foreach (var c in zm.GetCards(p1, Zone.Deck).Concat(zm.GetCards(p1, Zone.Hand))
                                 .Where(c => IsName(c, "教学·2")))
                    {
                        if (fillers.Count >= 8) break;
                        fillers.Add(c);
                    }
                    foreach (var c in fillers)
                    {
                        zm.MoveCard(c, p1, c.GetZone(), Zone.Battlefield);
                        foreach (var cell in GameBoard.BoardLayout.UnitCells(0))
                            if (board.CardAt(cell.x, cell.z) == null) { board.PinCard(c, cell.x, cell.z); break; }
                    }
                    // 疾行者已在声明后横置——重开一局满盘太重，直接对第二张疾行者验
                    var r1 = FindAndMoveByName(zm, p1, "训练·疾行者", Zone.Battlefield);
                    foreach (var cell in GameBoard.BoardLayout.UnitCells(0))
                        if (board.CardAt(cell.x, cell.z) == null) { board.PinCard(r1, cell.x, cell.z); break; }
                    var lae2 = new LegalActionEnumerator();
                    lae2.Enumerate(core, p1);
                    var rowsFull = lae2.Actions
                        .Where(a => a.Type == TideActionType.Activate && ReferenceEquals(a.Card, r1)).ToList();
                    log.AppendLine($"[P1移动] 满盘（单位区 {GameBoard.BoardLayout.UnitCells(0).Count} 格全占）行数={rowsFull.Count}");
                    checks.Add(new Check(rowsFull.Count == 0, "P1移动 无空格 → 0 行（不发动，不白付）"));

                }
                finally
                {
                    GameBoard.MoveUnitRules.Detach();
                }
            }
        }

        /// <summary>宣言盘面：强夺（对手手牌已展示）/窃取（对手牌库已展示）——候选不混区、命中迁移、空集 0 行。</summary>
        private static void RunDeclareScenario(List<CardData> catalog, StringBuilder log, List<Check> checks,
            CardData seize, CardData steal)
        {
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 3; i++) deck1.Add(seize);
            for (int i = 0; i < 3; i++) deck1.Add(steal);
            for (int i = 0; i < 4; i++) deck1.Add(ByName(catalog, EnemyCName)); // 填充
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) deck2.Add(ByName(catalog, EnemyBName)); // 对面手牌/牌库素材（教学·3）
            for (int i = 0; i < 6; i++) deck2.Add(ByName(catalog, EnemyAName)); // 火灵·攻（牌库展示样本）
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true; p2.IsAI = true; // 镜像 ②：cast 声明后 DrainStack 自动收敛（非 AI 等人响应停摆）
            using (var board = new GameBoard.BoardState(core, p1, p2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10);

                    var handSeize = FindAndMoveByName(zm, p1, seize.CardName, Zone.Hand);
                    var handSteal = FindAndMoveByName(zm, p1, steal.CardName, Zone.Hand); // 窃取留手（PlayCard 只扫手牌）
                    // 盘面 A：对手手牌样本——取**已在手**的卡（p2 手牌满 7 时 MoveCard 入手会触发
                    // 手牌上限把新卡直接除外——「对手回合超 7 入除外」规则，夹具须避开）
                    var p2Hand = zm.GetCards(p2, Zone.Hand);
                    var handCard = p2Hand.FirstOrDefault(c => IsName(c, EnemyBName)); // 教学·3——与牌库样本异名
                    var handName = handCard != null ? NameOf(handCard) : "?";
                    var deckCard = TakeByName(zm, p2, EnemyAName, Zone.Deck);                  // 盘面 C：对手牌库
                    checks.Add(new Check(handCard != null && deckCard != null,
                        $"P1宣言 夹具：对手手牌/牌库样本就位（{handCard != null}{deckCard != null}）"));
                    if (handCard == null || deckCard == null) return;
                    CardCore.Attribute.RevealRules.Reveal(handCard, null); // 手牌展示 → 强夺候选
                    CardCore.Attribute.RevealRules.Reveal(deckCard, null); // 牌库展示 → 窃取候选

                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    CheckZoneFeatures(lae, core, p1, checks, "P1宣言");
                    // 按卡名过滤：枚举器对同 ID 卡按签名去重只留首实例行，ReferenceEquals(移库实例) 会假阴性
                    var seizeRows = lae.Actions
                        .Where(a => a.Type == TideActionType.PlayCard && a.Card != null && IsName(a.Card, seize.CardName)).ToList();
                    var stealRows = lae.Actions
                        .Where(a => a.Type == TideActionType.PlayCard && a.Card != null && IsName(a.Card, steal.CardName)).ToList();
                    log.AppendLine($"[P1宣言] 强夺行={seizeRows.Count}（名={string.Join("/", seizeRows.Select(r => r.Interaction?.DeclaredName).ToArray())}）"
                        + $" 窃取行={stealRows.Count}（名={string.Join("/", stealRows.Select(r => r.Interaction?.DeclaredName).ToArray())}）");

                    // 盘面 A + 不混区：强夺候选=手牌展示名（不含牌库展示名）；行 [16]=候选序号
                    checks.Add(new Check(seizeRows.Count == 1
                            && seizeRows[0].Interaction?.DeclaredName == handName
                            && FeatureOf(lae, seizeRows[0], 16) == 0f
                            && FeatureOf(lae, seizeRows[0], 14) == -1f && FeatureOf(lae, seizeRows[0], 15) == -1f,
                        $"盘面A 强夺行=1、DeclaredName={handName}、[16]=0、[14]/[15]=-1"));
                    checks.Add(new Check(stealRows.Count == 1 && stealRows[0].Interaction?.DeclaredName == EnemyAName,
                        $"盘面C 窃取行=1、DeclaredName={EnemyAName}（手牌展示名 {handName} 不进窃取候选——按区间隔）"));
                    if (seizeRows.Count == 0 || stealRows.Count == 0) return; // 行缺失已 FAIL——应用断言防越界

                    // 应用强夺行 → 命中迁移：p2 手牌 → p1 手牌
                    var ok = LegalActionEnumerator.Apply(core, p1, seizeRows[0]);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    bool stolen = zm.IsCardInZone(handCard, p1, Zone.Hand) && !zm.IsCardInZone(handCard, p2, Zone.Hand);
                    log.AppendLine($"[P1宣言] 强夺应用：命中迁移={stolen} ok={ok}");
                    checks.Add(new Check(ok && stolen, "盘面A 应用强夺行 → 对手手牌该卡入己方手牌（宣言必命中）"));

                    // 盘面 B：手牌展示清空（卡已离开）→ 强夺 0 行
                    var lae2 = new LegalActionEnumerator();
                    lae2.Enumerate(core, p1);
                    var seizeRows2 = lae2.Actions
                        .Where(a => a.Type == TideActionType.PlayCard && a.Card != null && IsName(a.Card, seize.CardName)).ToList();
                    log.AppendLine($"[P1宣言] 无手牌展示 → 强夺行={seizeRows2.Count}");
                    checks.Add(new Check(seizeRows2.Count == 0, "盘面B 无已展示 → 强夺 0 行（不发动）"));

                    // 应用窃取行 → 对手牌库该卡入己方手牌
                    var ok2 = LegalActionEnumerator.Apply(core, p1, stealRows[0]);
                    drain = 0;
                    while (ok2 && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    bool stolenDeck = zm.IsCardInZone(deckCard, p1, Zone.Hand);
                    log.AppendLine($"[P1宣言] 窃取应用：牌库卡入手={stolenDeck} ok={ok2}");
                    checks.Add(new Check(ok2 && stolenDeck, "盘面C 应用窃取行 → 对手牌库该卡入己方手牌"));

                    // SimpleAI 端：候选空=不发动（反射 TryBuildInteraction）
                    var ai = new SimpleAI();
                    var mi = typeof(SimpleAI).GetMethod("TryBuildInteraction",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    object[] args = { core, p1, handSeize, CardEffectConverter.ConvertAll(seize.Effects, seize.ID).First(),
                        (List<Entity>)null, null };
                    bool aiDecl = (bool)mi.Invoke(ai, args); // 此刻无已展示 → false
                    log.AppendLine($"[P1宣言] SimpleAI 候选空 → 不发动={aiDecl == false}");
                    checks.Add(new Check(!aiDecl, "SimpleAI 无已读卡名 → 不发动（TryBuildInteraction false）"));
                }
                finally { }
            }
        }

        /// <summary>检索盘面：候选=己方牌库去重名；行数=候选数；应用后该名卡从牌库入手。</summary>
        private static void RunSearchScenario(List<CardData> catalog, StringBuilder log, List<Check> checks, CardData search)
        {
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 3; i++) deck1.Add(search);
            for (int i = 0; i < 5; i++) deck1.Add(ByName(catalog, LandName));
            for (int i = 0; i < 3; i++) deck1.Add(ByName(catalog, OwnBigName));
            var deck2 = new List<CardData>();
            for (int i = 0; i < 8; i++) deck2.Add(ByName(catalog, EnemyAName));
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            p1.IsAI = true; core.Player2.IsAI = true; // 镜像 ②：cast 声明后 DrainStack 自动收敛
            using (var board = new GameBoard.BoardState(core, p1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10);
                    var handCard = FindAndMoveByName(zm, p1, search.CardName, Zone.Hand);

                    // 期望候选=己方牌库去重名（DeclarationCandidates 单源镜像）
                    var def = CardEffectConverter.ConvertAll(search.Effects, search.ID).First();
                    var atom = def.Effects.FirstOrDefault(a => a != null && a.Type == AtomicEffectType.SearchDeck);
                    var want = DeclarationCandidates.For(core, p1, atom);

                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    var rows = lae.Actions
                        .Where(a => a.Type == TideActionType.PlayCard && a.Card != null && IsName(a.Card, search.CardName)).ToList(); // 按名过滤（同 ID 去重只留首实例）
                    var gotNames = rows.Select(r => r.Interaction?.DeclaredName).ToList();
                    log.AppendLine($"[P1检索] 行数={rows.Count}（期望候选 {want.Count}）:: {string.Join("/", gotNames.ToArray())}");
                    checks.Add(new Check(rows.Count == Math.Min(want.Count, 16),
                        $"盘面D 检索行=候选名数（{rows.Count} vs {want.Count}）"));
                    var idx16 = rows.Select(r => FeatureOf(lae, r, 16)).ToList();
                    checks.Add(new Check(gotNames.All(n => want.Contains(n)) && idx16.Distinct().Count() == rows.Count
                            && idx16.All(v => v >= 0 && v <= 15),
                        "盘面D 全行 DeclaredName∈己方牌库名、[16] 序号互异 ∈[0,15]"));

                    // 应用：任选首行 → 该行宣言名的卡从己方牌库入手（动态断言，不锚定具体卡名）
                    var row = rows.FirstOrDefault(r => r.Interaction != null);
                    if (row.Interaction == null) // TideAction 是 struct：以 Interaction 判行是否存在
                    {
                        checks.Add(new Check(false, "盘面D 检索 0 行（候选夹具）"));
                        return;
                    }
                    string wantName = row.Interaction.DeclaredName;
                    int before = zm.GetCards(p1, Zone.Hand).Count(c => IsName(c, wantName));
                    var ok = LegalActionEnumerator.Apply(core, p1, row);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    int after = zm.GetCards(p1, Zone.Hand).Count(c => IsName(c, wantName));
                    log.AppendLine($"[P1检索] 应用「{wantName}」行：手牌 {before}→{after} ok={ok}");
                    checks.Add(new Check(ok && after == before + 1, "盘面D 应用检索行 → 该名卡从己方牌库入手"));
                }
                finally { }
            }
        }


        // ======================================== ⑦⑧⑨ P2/P3 专项（2026-11-19 反馈追补） ========================================

        /// <summary>⑦ P2 落格专项：空场生物 PlayCard 行=空格数（9）、特征 [17]=UnitCells 下标互异、
        /// 应用指定格→单位实际落该格；占格递减；满区 0 行；法术行恒 -1。</summary>
        private static void RunPlacementScenario(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            var unitData = ByName(catalog, "教学·2");
            var spellData = ByName(catalog, "训练·检索");
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 6; i++) deck1.Add(unitData);
            for (int i = 0; i < 4; i++) deck1.Add(spellData); // 10 张：抽 8 后 deck 恒余 2（检索候选保底）
            var deck2 = new List<CardData>();
            for (int i = 0; i < 8; i++) deck2.Add(ByName(catalog, EnemyAName));
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            p1.IsAI = true; core.Player2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                GameBoard.MoveUnitRules.Attach(board);
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10);
                    // 先取检索（拿走 deck 至多 1 张→候选仍非空）；unit **只从手牌取**（开抽 8 张必有，
                    // 不再动 deck）——否则两次 Find 会把 deck（10-8=2 张）抽干→检索候选空集 0 行（夹具坑×2）
                    var spell = FindAndMoveByName(zm, p1, spellData.CardName, Zone.Hand);
                    var unit = TakeByName(zm, p1, "教学·2", Zone.Hand);

                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    CheckZoneFeatures(lae, core, p1, checks, "P2落格");
                    var unitRows = lae.Actions
                        .Where(a => a.Type == TideActionType.PlayCard && a.Card != null && IsName(a.Card, "教学·2")).ToList();
                    var spellRows = lae.Actions
                        .Where(a => a.Type == TideActionType.PlayCard && a.Card != null && IsName(a.Card, spellData.CardName)).ToList();
                    var unitCells = GameBoard.BoardLayout.UnitCells(0);
                    log.AppendLine("[P2落格] 生物行=" + unitRows.Count + " cells="
                        + string.Join(",", unitRows.Select(r => r.PlacementCell).OrderBy(x => x).ToArray())
                        + " 法术行=" + spellRows.Count + "（[17]=" + string.Join(",", spellRows.Select(r => r.PlacementCell).ToArray()) + "）");
                    checks.Add(new Check(unitRows.Count == unitCells.Count,
                        $"⑦空场生物行=空格数（{unitRows.Count} vs {unitCells.Count}）"));
                    checks.Add(new Check(unitRows.Select(r => r.PlacementCell).OrderBy(x => x)
                            .SequenceEqual(Enumerable.Range(0, unitCells.Count))
                            && unitRows.All(r => FeatureOf(lae, r, 17) == r.PlacementCell),
                        "⑦全行 [17]=0..8 互异且特征一致"));
                    bool coordMatch = unitRows.All(r =>
                    {
                        var cell = unitCells[r.PlacementCell];
                        return board.CardAt(cell.x, cell.z) == null; // 行格=当前空格
                    });
                    checks.Add(new Check(coordMatch, "⑦行内格下标与 UnitCells 坐标一致（均空格）"));
                    checks.Add(new Check(spellRows.Count > 0 && spellRows.All(r => r.PlacementCell == -1
                            && FeatureOf(lae, r, 17) == -1f),
                        $"⑦法术行 [17] 恒 -1（{spellRows.Count} 行=宣言候选数，deck 保底非空）"));

                    var pick = unitRows.FirstOrDefault(r => r.PlacementCell == 8);
                    var want = unitCells[8];
                    var ok = LegalActionEnumerator.Apply(core, p1, pick);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    // 落格断言用 pick.Card（上场的是去重保留行实例；unit 变量可能是另一同名实例→假阴性）
                    bool landed = board.TryGetCell(pick.Card, out int ux, out int uz) && ux == want.x && uz == want.z;
                    log.AppendLine($"[P2落格] 应用 cell8({want.x},{want.z}) → 实际=({ux},{uz}) ok={ok}");
                    checks.Add(new Check(ok && landed, "⑦应用 [17]=8 行 → 单位落 UnitCells[8]（声明后 Pin 生效）"));

                    // 占三格（已落 1 + 上两单位）→ 行=6
                    var fill1 = FindAndMoveByName(zm, p1, "教学·2", Zone.Battlefield);
                    var fill2 = FindAndMoveByName(zm, p1, "教学·2", Zone.Battlefield);
                    foreach (var c2 in new[] { fill1, fill2 })
                        foreach (var cell in unitCells)
                            if (board.CardAt(cell.x, cell.z) == null) { board.PinCard(c2, cell.x, cell.z); break; }
                    var lae2 = new LegalActionEnumerator();
                    lae2.Enumerate(core, p1);
                    var rows2 = lae2.Actions
                        .Where(a => a.Type == TideActionType.PlayCard && a.Card != null && IsName(a.Card, "教学·2")).ToList();
                    log.AppendLine($"[P2落格] 占三格后行={rows2.Count}");
                    checks.Add(new Check(rows2.Count == unitCells.Count - 3,
                        $"⑦占三格后行={rows2.Count}（期望 {unitCells.Count - 3}）"));

                    // 满区：填满 9 格 → 0 行
                    var fillers = new List<Card>();
                    foreach (var c2 in zm.GetCards(p1, Zone.Deck).Concat(zm.GetCards(p1, Zone.Hand))
                                 .Where(c => IsName(c, "教学·2")))
                    {
                        if (fillers.Count >= 6) break;
                        fillers.Add(c2);
                    }
                    foreach (var c2 in fillers)
                    {
                        zm.MoveCard(c2, p1, c2.GetZone(), Zone.Battlefield);
                        foreach (var cell in unitCells)
                            if (board.CardAt(cell.x, cell.z) == null) { board.PinCard(c2, cell.x, cell.z); break; }
                    }
                    var lae3 = new LegalActionEnumerator();
                    lae3.Enumerate(core, p1);
                    var rows3 = lae3.Actions
                        .Where(a => a.Type == TideActionType.PlayCard && a.Card != null && IsName(a.Card, "教学·2")).ToList();
                    log.AppendLine($"[P2落格] 满区行={rows3.Count}");
                    checks.Add(new Check(rows3.Count == 0, "⑦满区（9 格全占）→ 0 行（不白付）"));
                }
                finally { GameBoard.MoveUnitRules.Detach(); }
            }
        }

        /// <summary>⑧ P3.1 弃牌专项：EnumerateDiscardChoices 组合行（over=2 → C(5,2)=10；
        /// over=3 → C(5,3)=10 含默认组；特征 [10]=over）；停靠门 Park→Snapshot→Complete 全链
        /// + 非模型座回落；Complete 后镜像余段入墓。</summary>
        private static void RunDiscardChoiceScenario(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            var unitData = ByName(catalog, "教学·2");
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 12; i++) deck1.Add(unitData);
            var deck2 = new List<CardData>();
            for (int i = 0; i < 8; i++) deck2.Add(ByName(catalog, EnemyAName));
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            p1.IsAI = true; core.Player2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    var hand5 = new List<Card>();
                    while (hand5.Count < 5)
                    {
                        var c = TakeByName(zm, p1, "教学·2", Zone.Deck) ?? TakeByName(zm, p1, "教学·2", Zone.Hand);
                        if (c == null) break;
                        if (zm.IsCardInZone(c, p1, Zone.Deck)) zm.MoveCard(c, p1, Zone.Deck, Zone.Hand);
                        hand5.Add(c);
                    }
                    foreach (var extra in zm.GetCards(p1, Zone.Hand).Where(c => !hand5.Contains(c)).ToList())
                        zm.MoveCard(extra, p1, Zone.Hand, Zone.Deck);
                    checks.Add(new Check(hand5.Count == 5, $"⑧夹具：恰 5 张手牌（{hand5.Count}）"));
                    if (hand5.Count < 5) return;

                    var lae2 = LegalActionEnumerator.EnumerateDiscardChoices(core, p1, hand5, 2);
                    log.AppendLine($"[⑧弃牌] over=2 行={lae2.Actions.Count}（C(5,2)=10）");
                    checks.Add(new Check(lae2.Actions.Count == 10
                            && lae2.Actions.All(r => r.Type == TideActionType.DiscardChoice && r.Targets.Count == 2)
                            && lae2.Actions.Select(r => string.Join("|", r.Targets.Select(x => x.GetHashCode().ToString()).OrderBy(x => x)))
                                .Distinct().Count() == 10,
                        "⑧over=2：10 行组合两两不同、行 Targets=2"));
                    checks.Add(new Check(lae2.Actions.All(r => FeatureOf(lae2, r, 10) == 2f),
                        "⑧over=2 特征 [10]=2"));

                    var lae3 = LegalActionEnumerator.EnumerateDiscardChoices(core, p1, hand5, 3);
                    log.AppendLine($"[⑧弃牌] over=3 行={lae3.Actions.Count}");
                    checks.Add(new Check(lae3.Actions.Count == 10
                            && lae3.Actions.All(r => r.Targets.Count == 3)
                            && lae3.Actions.Count(r => r.Targets.Contains(hand5[0]) && r.Targets.Contains(hand5[1]) && r.Targets.Contains(hand5[2])) >= 1,
                        "⑧over=3：C(5,3)=10 行、含默认组（前 3 张）"));

                    // 停靠门全链
                    HandLimitDiscardGate.Reset();
                    HandLimitDiscardGate.Bind(p2 => ReferenceEquals(p2, p1));
                    var hand4 = hand5.Take(4).ToList();
                    foreach (var extra in hand5.Skip(4).ToList()) zm.MoveCard(extra, p1, Zone.Hand, Zone.Deck);
                    checks.Add(new Check(HandLimitDiscardGate.TryPark(p1, hand4, 2) && HandLimitDiscardGate.HasPending,
                        "⑧停靠门 TryPark（模型座）→ HasPending"));
                    var snap = HandLimitDiscardGate.Snapshot();
                    var laeD = LegalActionEnumerator.EnumerateDiscardChoices(core, p1, snap.cards, snap.over);
                    var pickRow = laeD.Actions.FirstOrDefault(r => r.Targets.Contains(hand4[3]) && r.Targets.Contains(hand4[2]));
                    checks.Add(new Check(snap.player == p1 && snap.over == 2 && laeD.Actions.Count == 6,
                        $"⑧Snapshot（player/over=2）→ 驱动侧枚举 C(4,2)={laeD.Actions.Count} 行"));
                    var picked = pickRow.Targets.Cast<Card>().ToList();
                    HandLimitDiscardGate.Complete(picked);
                    foreach (var c2 in picked)
                        if (zm.IsCardInZone(c2, p1, Zone.Hand)) zm.MoveCard(c2, p1, Zone.Hand, Zone.Graveyard);
                    log.AppendLine($"[⑧弃牌] Complete→释放={!HandLimitDiscardGate.HasPending} 入墓={picked.Count}");
                    checks.Add(new Check(!HandLimitDiscardGate.HasPending
                            && picked.All(c2 => zm.IsCardInZone(c2, p1, Zone.Graveyard)),
                        "⑧Complete 释放门 + 选中 2 张入墓（镜像余段）"));
                    HandLimitDiscardGate.Bind(null);
                    checks.Add(new Check(!HandLimitDiscardGate.TryPark(p1, hand4, 2),
                        "⑧非模型座（未绑定）TryPark=false → 回落 RequestAsync 旧行为"));
                }
                finally { HandLimitDiscardGate.Reset(); HandLimitDiscardGate.Bind(null); }
            }
        }

        /// <summary>⑨ P3.2 预言三选一专项：合成预言卡（内存构造）——LAE PlayCard 行=3
        ///（Type:Creature/Spell/Enchantment）、[16]=0/1/2 互异、其余交互维 -1、Signature 互异
        ///（P1 反馈回归）；应用一行结算成功（AskDimensionAsync 读 DeclaredName 直通）。</summary>
        private static void RunProphecyScenario(StringBuilder log, List<Check> checks)
        {
            var catalog = CardCatalog.LoadAll().ToList();
            var atom = Attribute.AtomicEffectTable.GetByType(AtomicEffectType.ProphecyNextCard);
            checks.Add(new Check(atom != null, "⑨原子表有 ProphecyNextCard 行"));
            if (atom == null) return;
            var fx = new CardEffectData
            {
                Id = "EFF_TMP_PROPHECY",
                DisplayName = "预言·探针",
                TriggerTiming = 19,
                ActivationType = 2,
                SelectionMode = 0,
                TargetCount = 1,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 0, atomic = new AtomicEffectEntry { refId = atom.HashId, count = 1 } },
                },
            };
            var tmp = new CardData
            {
                ID = "C_TMP_PROPHECY_PROBE", CardName = "探针·预言", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { fx },
            };

            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 4; i++) deck1.Add(tmp);
            for (int i = 0; i < 6; i++) deck1.Add(ByName(catalog, EnemyCName));
            var deck2 = new List<CardData>();
            for (int i = 0; i < 8; i++) deck2.Add(ByName(catalog, EnemyAName));
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            p1.IsAI = true; core.Player2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                        core.ElementPool.AddMana(p1, color, null, 10);
                    var handCard = FindAndMoveByName(zm, p1, tmp.CardName, Zone.Hand);

                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    CheckZoneFeatures(lae, core, p1, checks, "⑨预言");
                    var rows = lae.Actions
                        .Where(a => a.Type == TideActionType.PlayCard && a.Card != null && IsName(a.Card, tmp.CardName)).ToList(); // 按名过滤（同 ID 去重只留首实例）
                    var names = rows.Select(r => r.Interaction?.DeclaredName).ToList();
                    log.AppendLine($"[⑨预言] 行={rows.Count} :: {string.Join("/", names.ToArray())}");
                    var want = new[] { "Type:Creature", "Type:Spell", "Type:Enchantment" };
                    checks.Add(new Check(rows.Count == 3 && names.OrderBy(x => x).SequenceEqual(want.OrderBy(x => x)),
                        "⑨预言行=3（Type:Creature/Spell/Enchantment）"));
                    var idx16 = rows.Select(r => FeatureOf(lae, r, 16)).OrderBy(x => x).ToList();
                    checks.Add(new Check(idx16.SequenceEqual(new[] { 0f, 1f, 2f })
                            && rows.All(r => FeatureOf(lae, r, 14) == -1f && FeatureOf(lae, r, 15) == -1f
                                && FeatureOf(lae, r, 17) == -1f),
                        "⑨特征 [16]=0/1/2 互异、[14]/[15]/[17]=-1"));
                    var sigs = rows.Select(r => r.Signature).ToList();
                    checks.Add(new Check(sigs.Distinct().Count() == 3, "⑨三行 Signature 互异（Interaction 入键回归）"));

                    var pick = rows.FirstOrDefault(r => r.Interaction?.DeclaredName == "Type:Enchantment");
                    var ok = LegalActionEnumerator.Apply(core, p1, pick);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    log.AppendLine($"[⑨预言] 应用 Type:Enchantment：ok={ok} drains={drain}");
                    checks.Add(new Check(ok, "⑨应用预言行（AskDimensionAsync 读 DeclaredName 直通，无反问窗）"));
                }
                finally { }
            }
        }

        /// <summary>全行恒等式（v14 规格验收）：每行动作特征 [12]/[13] 与 TargetZoneGrouping
        /// 单源判定（首目标）逐行一致——三条既有场景各自枚举后调用。</summary>
        private static void CheckZoneFeatures(LegalActionEnumerator lae, GameCore core, Player me,
            List<Check> checks, string tag)
        {
            int nAction = TideObservation.NAction;
            int bad = 0; int total = 0;
            for (int k = 0; k < lae.Actions.Count; k++)
            {
                var a = lae.Actions[k];
                var first = a.Targets != null && a.Targets.Count > 0 ? a.Targets[0] : a.Target;
                float wantZone = first is Card tc ? TargetZoneGrouping.ZoneRankOf(tc.GetZone())
                    : first != null ? 0f : -1f;
                float wantSide = first != null ? TargetZoneGrouping.SideRankOf(first, me) : -1f;
                float gotZone = lae.Features[k * nAction + 12];
                float gotSide = lae.Features[k * nAction + 13];
                total++;
                if (gotZone != wantZone || gotSide != wantSide) bad++;
            }
            checks.Add(new Check(bad == 0,
                $"{tag}：全 {total} 行 [12]/[13] 与单源判定一致（坏 {bad} 行）"));
        }

        private static float FeatureOf(LegalActionEnumerator lae, TideAction a, int slot)
            => lae.Features[lae.Actions.IndexOf(a) * TideObservation.NAction + slot];

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
