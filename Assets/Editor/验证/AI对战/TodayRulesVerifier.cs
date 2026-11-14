using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CardCore;
using CardCore.Attribute;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
    {
    /// <summary>
    /// 2026-11-07 规则大调整·阶段 2/2 端到端验证（规范=.ztmp/2026-11-07_规则变更.md）。
    /// 本文件只写测试：不改数据、不改核心实现、不动既有验证器断言；失败只报告。
    ///
    /// A 关键词：GrantTaunt 表行域=己方战场:生物/kinds=[1]；count=1/0 经 converter 后
    ///   TargetCount 与轴正确；真实 PlayCard 一次让我方战场生物获得 Temp 帷幕；
    ///   PurifyKeywords 后消失；CardData.Keywords 印刷轨不被驱散。
    /// B 费用增减：AddCostUpGray（p=−1）converter 后轴=对方手牌:卡、AddCostDownGray（p=+1）
    ///   轴=己方手牌:卡；count=0 与 2 均不被收 1；运行时候选只取对应侧手牌
    ///   （TargetAxisResolver 真实取件，非手牌侧零混入）。
    /// C 数量档（converter 装载守卫）：count=0 战场/手牌合法，牌库/墓地/除外区/元素池/角色/Self 收 1；
    ///   count=2/3 角色收 1，元素池/牌库/墓地/除外区保留 2/3；Self 收 1。
    /// D 光环：DomainOptionsOf 对全部 IsAuraLike 行返回空（dd-domain 隐藏，反射 internal）；
    ///   AllowedScopesOfPolarity 极性门控（p=0 只双方 / +1 {己方,双方} / −1 {对方,双方}，反射）；
    ///   AuraGrantRejectReason 全 +1 效果可授予、含 0/−1 原子拒绝；
    ///   连接条目 scope=0（无箭头不生效/箭头指向生效）、场地属性降低条目 scope=3（无需箭头打对方战场）。
    /// E 文案：AtomText.Render 关键词「赋予己方战场的1个生物缴械…」（count 0→全部）、
    ///   费用「为1张/2张/全部 己方(对方)手牌中的卡…」。
    ///
    /// 夹具全部内存合成（不落盘、不读 CardData/TutorialData/TrainingData 内容）；
    /// 入口：菜单 Tools/验证/今天规则（E2E）；Run() 返回报告文本（[Verify] 风格日志）。
    /// </summary>
    public static class TodayRulesVerifier
    {
        private const string GrantSpellName = "探针·赋予帷幕";
        private const string PlainCreatureName = "探针·白板生物";
        private const string PrintedCreatureName = "探针·印刷帷幕生物";
        private const string LinkerName = "探针·连接光环源";
        private const string LinkTargetName = "探针·受光环生物";
        private const string FieldCarrierName = "探针·场地减攻载体";

        private static readonly List<(bool Pass, string Text)> Checks = new List<(bool, string)>();
        private static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Tools/验证/今天规则（E2E）")]
        public static void RunFromMenu()
        {
            var report = Run();
            Debug.Log(report);
            if (Checks.Count(c => !c.Pass) > 0)
                Debug.LogError("[Verify·今天规则] 存在失败断言，详见上方报告。");
        }

        public static string Run()
        {
            Checks.Clear();
            Log.Clear();
            Log.AppendLine("[Verify·今天规则] ==== 开始（2026-11-07 规则基准）====");
            try
            {
                RunKeywordChecks();       // A
                RunCostIndicatorChecks(); // B
                RunCountGuardChecks();    // C
                RunAuraChecks();          // D
                RunTextChecks();          // E
            }
            catch (Exception ex)
            {
                Check(false, $"异常中断：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            int fails = Checks.Count(c => !c.Pass);
            Log.AppendLine(fails == 0
                ? $"[Verify·今天规则] ==== 全过（{Checks.Count} 项断言）===="
                : $"[Verify·今天规则] ==== 失败 {fails}/{Checks.Count} 项 ====");
            return Log.ToString();
        }

        private static void Check(bool ok, string text)
        {
            Checks.Add((ok, text));
            if (!ok) Log.AppendLine("[Verify·FAIL] " + text);
        }

        // ======================================== 表行短口 ========================================

        private static Attribute.AtomicEffectConfig Row(AtomicEffectType type)
            => Attribute.AtomicEffectTable.GetByType(type);

        private static AtomicEffectEntry Entry(AtomicEffectType type, int value = 1, int count = 1,
            string domain = null, List<int> kinds = null)
            => new AtomicEffectEntry
            {
                refId = Row(type)?.HashId,
                value = value,
                count = count,
                domain = domain,
                kinds = kinds,
            };

        // ======================================== A 关键词 ========================================

        private static void RunKeywordChecks()
        {
            Log.AppendLine("[Verify·A] ---- 关键词（效果层赋予 vs 印刷）----");
            var taunt = Row(AtomicEffectType.GrantTaunt);
            Check(taunt != null, "A0 前置：原子表能取到 GrantTaunt 行");
            if (taunt == null) return;

            // A1 表行：域=己方战场:生物，派生 kinds=[1]，关键词挂位
            Check(taunt.Domain == "己方战场:生物",
                $"A1 GrantTaunt 行 Domain='{taunt.Domain}'（期望 己方战场:生物）");
            var tauntKinds = taunt.GetTargetKindList();
            Check(tauntKinds.Count == 1 && tauntKinds[0] == (int)TargetKind.OwnLivingUnit,
                $"A1 GrantTaunt 派生 kinds=[{string.Join(",", tauntKinds)}]（期望 [1]=己方生物）");

            // A2 converter：count=1 / count=0（战场可全域）与轴
            var one = CardEffectConverter.ConvertAtomForUI(Entry(AtomicEffectType.GrantTaunt, count: 1));
            Check(one != null && one.TargetCount == 1 && one.Axis != null && one.Axis.Zh == "己方战场:生物",
                $"A2 count=1 → TargetCount={one?.TargetCount} 轴={one?.Axis?.Zh}（期望 1 / 己方战场:生物）");
            var all = CardEffectConverter.ConvertAtomForUI(Entry(AtomicEffectType.GrantTaunt, count: 0));
            Check(all != null && all.TargetCount == 0 && all.Axis != null && all.Axis.Zh == "己方战场:生物",
                $"A2 count=0 → TargetCount={all?.TargetCount} 轴={all?.Axis?.Zh}（期望 0 不被收 / 己方战场:生物）");
            // 旧自指存储（kinds:[0]）不再压回自己——NarrowByKinds 对纯 Self kinds 不收窄，按行域走
            var legacy = CardEffectConverter.ConvertAtomForUI(Entry(AtomicEffectType.GrantTaunt, count: 1, kinds: new List<int> { 0 }));
            Check(legacy != null && legacy.Axis != null && legacy.Axis.Zh == "己方战场:生物",
                $"A2 旧档 kinds=[0] → 轴={legacy?.Axis?.Zh}（期望仍按行域 己方战场:生物——旧自指存储不夺域）");

            // A3 真实执行：最小 GameCore 夹具（参考 AuraCounterStackVerifier / LaeMultiTargetVerifier）
            var core = GameCore.Instance;
            if (core == null) { Check(false, "A3 前置：GameCore.Instance 为空"); return; }

            var grantEff = new CardEffectData
            {
                Id = "EFF_TODAY_GRANT_TAUNT",
                DisplayName = "赋予帷幕（探针）",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                ActivationType = 2, // Voluntary（法术施放档）
                SelectionMode = -1, // None：逐原子目标制
                TargetCount = -2,
                AtomicEffects = new List<AtomicEffectEntry> { Entry(AtomicEffectType.GrantTaunt) },
            };
            var grantCard = new CardData
            {
                ID = "C_TODAY_GRANT_TAUNT", CardName = GrantSpellName, Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { grantEff },
            };
            var plain = new CardData
            {
                ID = "C_TODAY_PLAIN", CardName = PlainCreatureName, Supertype = Cardtype.Creature, Power = 2, Life = 10,
            };
            var printed = new CardData
            {
                ID = "C_TODAY_PRINTED_TAUNT", CardName = PrintedCreatureName, Supertype = Cardtype.Creature,
                Power = 2, Life = 10, Keywords = new List<string> { KeywordRules.Taunt },
            };

            var deck1 = new List<CardData>();
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) { deck1.Add(grantCard); deck1.Add(plain); deck1.Add(printed); }
            for (int i = 0; i < 6; i++) { deck2.Add(plain); deck2.Add(printed); }
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = false;
            p2.IsAI = false;

            using (var board = new GameBoard.BoardState(core, p1, p2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.FlankNeighbors;
                GameBoard.LinkAuraSystem.Attach(board);
                try
                {
                    AdvanceToMainPhase(core, p1);
                    var zm = core.ZoneManager;
                    var target = FindAndMoveByName(zm, p1, PlainCreatureName, Zone.Battlefield);
                    var printedUnit = FindAndMoveByName(zm, p1, PrintedCreatureName, Zone.Battlefield);
                    var spell = FindAndMoveByName(zm, p1, GrantSpellName, Zone.Hand);
                    Check(target != null && printedUnit != null && spell != null,
                        $"A3 夹具就位：目标生物/印刷生物/赋帷幕法术（{target != null}/{printedUnit != null}/{spell != null}）");
                    if (target == null || printedUnit == null || spell == null) return;

                    foreach (var color in new[] { ManaType.Gray, ManaType.Red, ManaType.Green, ManaType.Blue })
                        core.ElementPool.AddMana(p1, color, null, 10);

                    // 印刷轨：进场统一出口补 Printed 份（CardData.Keywords → 不经 Grant handler）
                    Check(printedUnit.HasKeyword(KeywordRules.Taunt),
                        "A3 印刷关键词：CardData.Keywords=[Taunt] 入场后 HasKeyword=true（Printed 轨）");
                    Check(!target.HasKeyword(KeywordRules.Taunt),
                        "A3 基线：白板生物无帷幕");

                    // 真实执行一次：PlayCard → GrantKeywordHandler → KeywordLane.Temp
                    var ok = GameActions.PlayCard(core, p1, spell, new List<Entity> { target }, Zone.Hand, 0);
                    int drain = 0;
                    while (ok && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
                    Check(ok, "A3 PlayCard（赋帷幕法术 count=1 指己方生物）被引擎接受");
                    Check(target.HasKeyword(KeywordRules.Taunt),
                        $"A3 效果层授予：结算后目标生物 HasKeyword(Taunt)={target.HasKeyword(KeywordRules.Taunt)}（期望 true=Temp 轨）");

                    // 驱散：Temp 清、Printed 留
                    KeywordRules.PurifyKeywords(target);
                    Check(!target.HasKeyword(KeywordRules.Taunt),
                        "A3 PurifyKeywords 后效果层授予消失（Temp 轨被清）");
                    KeywordRules.PurifyKeywords(printedUnit);
                    Check(printedUnit.HasKeyword(KeywordRules.Taunt),
                        "A3 PurifyKeywords 后印刷关键词保留（CardData.Keywords 免疫驱散）");
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        // ======================================== B 费用增减指示物 ========================================

        private static void RunCostIndicatorChecks()
        {
            Log.AppendLine("[Verify·B] ---- 费用增减指示物（极性定侧 × 手牌全域）----");
            var up = Row(AtomicEffectType.AddCostUpGray);
            var down = Row(AtomicEffectType.AddCostDownGray);
            Check(up != null && down != null, "B0 前置：原子表能取到 AddCostUpGray/AddCostDownGray 行");
            if (up == null || down == null) return;

            // B1 表行：全部候选区域=双方手牌:卡；极性 增费=−1 / 减费=+1
            Check(up.Domain == "双方手牌:卡" && down.Domain == "双方手牌:卡",
                $"B1 表行 Domain：增费='{up.Domain}' 减费='{down.Domain}'（期望均 双方手牌:卡）");
            Check(up.Polarity < -0.5f && down.Polarity > 0.5f,
                $"B1 极性：增费={up.Polarity} 减费={down.Polarity}（期望 −1 / +1）");

            // B2 converter：极性解析出单侧手牌轴；count=0/2 均不被收 1（手牌区域允许全域/多选）
            var up0 = CardEffectConverter.ConvertAtomForUI(Entry(AtomicEffectType.AddCostUpGray, count: 0));
            var up2 = CardEffectConverter.ConvertAtomForUI(Entry(AtomicEffectType.AddCostUpGray, count: 2));
            Check(up0 != null && up0.TargetCount == 0 && up0.Axis != null && up0.Axis.Zh == "对方手牌:卡",
                $"B2 增费(p=−1) count=0 → TargetCount={up0?.TargetCount} 轴={up0?.Axis?.Zh}（期望 0 / 对方手牌:卡）");
            Check(up2 != null && up2.TargetCount == 2 && up2.Axis != null && up2.Axis.Zh == "对方手牌:卡",
                $"B2 增费 count=2 → TargetCount={up2?.TargetCount} 轴={up2?.Axis?.Zh}（期望 2 / 对方手牌:卡）");
            var down0 = CardEffectConverter.ConvertAtomForUI(Entry(AtomicEffectType.AddCostDownGray, count: 0));
            var down2 = CardEffectConverter.ConvertAtomForUI(Entry(AtomicEffectType.AddCostDownGray, count: 2));
            Check(down0 != null && down0.TargetCount == 0 && down0.Axis != null && down0.Axis.Zh == "己方手牌:卡",
                $"B2 减费(p=+1) count=0 → TargetCount={down0?.TargetCount} 轴={down0?.Axis?.Zh}（期望 0 / 己方手牌:卡）");
            Check(down2 != null && down2.TargetCount == 2 && down2.Axis != null && down2.Axis.Zh == "己方手牌:卡",
                $"B2 减费 count=2 → TargetCount={down2?.TargetCount} 轴={down2?.Axis?.Zh}（期望 2 / 己方手牌:卡）");

            // B3 运行时：真实取件（TargetAxisResolver）只取对应侧手牌
            var core = GameCore.Instance;
            if (core == null) { Check(false, "B3 前置：GameCore.Instance 为空"); return; }
            var filler = new CardData
            {
                ID = "C_TODAY_COST_FILLER", CardName = "探针·费用夹材", Supertype = Cardtype.Spell,
            };
            var deck1 = new List<CardData>();
            var deck2 = new List<CardData>();
            for (int i = 0; i < 9; i++) { deck1.Add(filler); deck2.Add(filler); }
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            AdvanceToMainPhase(core, p1);
            var zm = core.ZoneManager;
            EnsureHand(zm, p1, 3);
            EnsureHand(zm, p2, 2);
            var hand1 = new HashSet<Card>(zm.GetCards(p1, Zone.Hand));
            var hand2 = new HashSet<Card>(zm.GetCards(p2, Zone.Hand));
            Check(hand1.Count >= 3 && hand2.Count >= 2,
                $"B3 夹具：p1 手牌 {hand1.Count} 张 / p2 手牌 {hand2.Count} 张（期望 ≥3 / ≥2）");

            var ctx = new EffectExecutionContext
            {
                Source = p1, Controller = p1, ZoneManager = zm, ElementPool = core.ElementPool, ModeIndex = 0,
            };
            var enemyCands = new HashSet<Card>(TargetAxisResolver.GetCandidates(up0.Axis, ctx).OfType<Card>());
            var ownCands = new HashSet<Card>(TargetAxisResolver.GetCandidates(down0.Axis, ctx).OfType<Card>());
            bool upOk = enemyCands.SetEquals(hand2) && !enemyCands.Overlaps(hand1);
            bool downOk = ownCands.SetEquals(hand1) && !ownCands.Overlaps(hand2);
            Check(upOk,
                $"B3 增费轴运行时候选={enemyCands.Count} 个，恰为 p2 全部手牌 {hand2.Count} 张且零混入 p1（{Describe(upOk, enemyCands.Count, hand2.Count)}）");
            Check(downOk,
                $"B3 减费轴运行时候选={ownCands.Count} 个，恰为 p1 全部手牌 {hand1.Count} 张且零混入 p2（{Describe(downOk, ownCands.Count, hand1.Count)}）");
        }

        private static string Describe(bool ok, int got, int want) => ok ? "全取单侧" : $"got={got} want={want}";

        private static void EnsureHand(ZoneManager zm, Player owner, int min)
        {
            int guard = 0;
            while (guard++ < 40 && zm.GetCards(owner, Zone.Hand).Count < min)
            {
                var c = zm.GetCards(owner, Zone.Deck).FirstOrDefault();
                if (c == null) break;
                zm.MoveCard(c, owner, Zone.Deck, Zone.Hand);
            }
        }

        // ======================================== C 数量档（装载守卫） ========================================

        private static void RunCountGuardChecks()
        {
            Log.AppendLine("[Verify·C] ---- 数量档守卫（按作用域，不按原子族）----");
            // 基原子=Purify（p=0：极性收窄零影响，entry.domain 覆盖即所测轴——纯守卫逻辑探针）
            var baseRow = Row(AtomicEffectType.Purify);
            Check(baseRow != null, "C0 前置：原子表能取到 Purify 行（守卫探针基原子）");
            if (baseRow == null) return;

            int ConvCount(string domain, int count)
                => CardEffectConverter.ConvertAtomForUI(Entry(AtomicEffectType.Purify, count: count, domain: domain))
                   ?.TargetCount ?? -99;
            string ConvAxis(string domain)
                => CardEffectConverter.ConvertAtomForUI(Entry(AtomicEffectType.Purify, count: 1, domain: domain))
                   ?.Axis?.Zh ?? "（null）";

            // C1 count=0（全域）只有战场/手牌合法，其余收 1
            Check(ConvCount("双方战场:生物", 0) == 0, "C1 战场 count=0 → 保留 0（全域合法）");
            Check(ConvCount("双方手牌:卡", 0) == 0, "C1 手牌 count=0 → 保留 0（全域合法）");
            Check(ConvCount("己方牌库:卡", 0) == 1, "C1 牌库 count=0 → 收 1");
            Check(ConvCount("己方墓地:卡", 0) == 1, "C1 墓地 count=0 → 收 1");
            Check(ConvCount("己方除外区:卡", 0) == 1, "C1 除外区 count=0 → 收 1");
            Check(ConvCount("己方元素池:卡", 0) == 1, "C1 元素池 count=0 → 收 1");
            Check(ConvCount("己方角色", 0) == 1, "C1 角色 count=0 → 收 1");
            Check(ConvCount("自己", 0) == 1, "C1 Self count=0 → 收 1");

            // C2 count=2/3：角色收 1；元素池/牌库/墓地/除外区保留
            Check(ConvCount("己方角色", 2) == 1 && ConvCount("己方角色", 3) == 1, "C2 角色 count=2/3 → 均 收 1");
            Check(ConvCount("自己", 2) == 1, "C2 Self count=2 → 收 1");
            Check(ConvCount("己方元素池:卡", 2) == 2 && ConvCount("己方元素池:卡", 3) == 3, "C2 元素池 count=2/3 → 保留 2/3");
            Check(ConvCount("己方牌库:卡", 2) == 2 && ConvCount("己方牌库:卡", 3) == 3, "C2 牌库 count=2/3 → 保留 2/3");
            Check(ConvCount("己方墓地:卡", 2) == 2 && ConvCount("己方墓地:卡", 3) == 3, "C2 墓地 count=2/3 → 保留 2/3");
            Check(ConvCount("己方除外区:卡", 2) == 2 && ConvCount("己方除外区:卡", 3) == 3, "C2 除外区 count=2/3 → 保留 2/3");

            // C3 轴未被守卫改写（收 1 只动数量，不动域）
            Check(ConvAxis("己方牌库:卡") == "己方牌库:卡" && ConvAxis("己方角色") == "己方角色"
                && ConvAxis("己方元素池:卡") == "己方元素池:卡",
                "C3 收窄只动 TargetCount 不动轴（三域抽检原样）");
        }

        // ======================================== D 光环 ========================================

        private static void RunAuraChecks()
        {
            Log.AppendLine("[Verify·D] ---- 光环（Domain 退役 / 极性门控 / 授予资格 / scope 约定）----");

            // D1 DomainOptionsOf 对全部光环行返回空（反射 internal static；dd-domain 隐藏）
            var composerType = typeof(SynergyUI.EffectComposerScreen);
            var miOptions = composerType.GetMethod("DomainOptionsOf",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Check(miOptions != null, "D1 反射取到 EffectComposerScreen.DomainOptionsOf（internal static）");
            if (miOptions != null)
            {
                var offenders = new List<string>();
                int auraRows = 0;
                foreach (var cfg in Attribute.AtomicEffectTable.GetAll())
                {
                    if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)) continue;
                    if (!RuleAuraComponents.IsAuraLike(t)) continue;
                    auraRows++;
                    var opts = miOptions.Invoke(null, new object[] { cfg, false }) as List<string>;
                    if (opts != null && opts.Count > 0)
                        offenders.Add($"{cfg.EnumName}({cfg.HashId})→{opts.Count} 项");
                }
                Log.AppendLine($"[Verify·D] 光环行 {auraRows} 行扫描 DomainOptionsOf");
                Check(auraRows >= 10, $"D1 光环行数={auraRows}（期望 ≥10——枚举面未被误收缩）");
                Check(offenders.Count == 0,
                    $"D1 全部 IsAuraLike 行 DomainOptionsOf 为空（违规：{string.Join("；", offenders)}；期望无）");
            }

            // D2 极性门控（反射 private static AllowedScopesOfPolarity；方向码 1=己方 2=双方 3=对方）
            var miScopes = composerType.GetMethod("AllowedScopesOfPolarity",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Check(miScopes != null, "D2 反射取到 EffectComposerScreen.AllowedScopesOfPolarity（private static）");
            if (miScopes != null)
            {
                List<int> Of(float p)
                    => (miScopes.Invoke(null, new object[] { p }) as IEnumerable<int>)?.OrderBy(x => x).ToList();
                bool Same(List<int> got, params int[] want)
                    => got != null && got.SequenceEqual(want.OrderBy(x => x));
                Check(Same(Of(0f), 2), $"D2 p=0 → [{string.Join(",", Of(0f))}]（期望 [2]=只双方）");
                Check(Same(Of(1f), 1, 2), $"D2 p=+1 → [{string.Join(",", Of(1f))}]（期望 [1,2]=己方/双方）");
                Check(Same(Of(-1f), 2, 3), $"D2 p=−1 → [{string.Join(",", Of(-1f))}]（期望 [2,3]=双方/对方）");
            }

            // D3 连接光环 effectRef 资格：全 +1 可授予；含 0/−1 原子拒绝
            CardEffectData Fx(params AtomicEffectEntry[] atoms) => new CardEffectData
            {
                Id = "EFF_TODAY_AURA_PROBE",
                DisplayName = "光环资格探针",
                // OnAttack=生物动作族（2026-11-14 档位制归「一回合一次」档）：生物臂放行，
                // 光环可授予；一回合多次（结界专属）不命中
                TriggerTiming = (int)TriggerTiming.OnAttack,
                ActivationType = 1, // Automatic（非主动、非可选触发）
                SelectionMode = -1,
                TargetCount = -2,
                AtomicEffects = atoms.ToList(),
            };
            var heal = Entry(AtomicEffectType.Heal, value: 2);
            var regen = Entry(AtomicEffectType.GrantRegeneration);
            var damage = Entry(AtomicEffectType.DealDamage, value: 2);
            var purify = Entry(AtomicEffectType.Purify);

            var okReason = EffectEligibility.AuraGrantRejectReason(Fx(heal, regen));
            Check(okReason == null, $"D3 全 +1（回复+赋予再生）可授予（实际拒绝原因：{okReason ?? "无"}）");

            var negReason = EffectEligibility.AuraGrantRejectReason(Fx(heal, damage));
            Check(negReason != null && negReason.Contains("极性"),
                $"D3 含 −1 原子（回复+造成伤害）被拒（实际：{negReason ?? "null"}，期望点名极性）");

            var zeroReason = EffectEligibility.AuraGrantRejectReason(Fx(heal, purify));
            Check(zeroReason != null && zeroReason.Contains("极性"),
                $"D3 含 0 原子（回复+净化）被拒（实际：{zeroReason ?? "null"}，期望点名极性）");

            var allNeg = EffectEligibility.AuraGrantRejectReason(Fx(damage));
            Check(allNeg != null && allNeg.Contains("极性"),
                $"D3 纯 −1 效果被拒（实际：{allNeg ?? "null"}）");

            // D4 scope 约定：连接条目=0（箭头几何）、场地属性降低条目=3（无需箭头打对方战场）
            Check(new LinkAuraData { stat = "Power", value = 2 }.scope == 0,
                "D4 数据形态：连接属性条目默认 scope=0（箭头连接方向）");
            RunAuraScopeBoardChecks();
        }

        private static void RunAuraScopeBoardChecks()
        {
            var core = GameCore.Instance;
            if (core == null) { Check(false, "D4 前置：GameCore.Instance 为空"); return; }

            var creature = new CardData
            {
                ID = "C_TODAY_LINK_TARGET", CardName = LinkTargetName, Supertype = Cardtype.Creature, Power = 1, Life = 10,
            };
            var linker = new CardData
            {
                ID = "C_TODAY_LINKER", CardName = LinkerName, Supertype = Cardtype.Creature, Power = 1, Life = 10,
            };
            var carrier = new CardData
            {
                ID = "C_TODAY_FIELD_CARRIER", CardName = FieldCarrierName, Supertype = Cardtype.Enchantment, Life = 10,
            };
            var deck1 = new List<CardData>();
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) { deck1.Add(creature); deck1.Add(linker); deck2.Add(carrier); deck2.Add(creature); }
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = false;
            p2.IsAI = false;

            using (var board = new GameBoard.BoardState(core, p1, p2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                GameBoard.LinkAuraSystem.Attach(board);
                board.Resync();
                try
                {
                    var zm = core.ZoneManager;
                    var target = PlaceFromDeck(zm, p1, LinkTargetName);
                    var linkerCard = PlaceFromDeck(zm, p1, LinkerName);
                    var carrierCard = PlaceFromDeck(zm, p2, FieldCarrierName);
                    board.Resync();
                    Check(target != null && linkerCard != null && carrierCard != null,
                        "D4 夹具：受光环生物/连接光环源（己方）+ 场地载体（对方）三卡就位");
                    if (target == null || linkerCard == null || carrierCard == null) return;

                    int basePower = target.GetPower();
                    Check(basePower == 1, $"D4 基线：生物攻击力 {basePower}（期望 1）");

                    var arrow = ArrowTo(board, linkerCard, target);
                    Check(arrow != HexDirection.None, "D4 几何：光环源与受益者相邻（可建箭头）");

                    // scope=0：无箭头 → 不生效
                    var linkerData = DataOf(linkerCard);
                    linkerData.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 2 }); // scope 默认 0
                    GameBoard.LinkAuraSystem.InvalidateCache();
                    int noArrow = target.GetPower();
                    Check(noArrow == 1,
                        $"D4 连接条目 scope=0 无箭头 → 攻击力 {noArrow}（期望 1=不生效）");

                    // scope=0：箭头指向 → 生效
                    linkerData.ArrowDirections = arrow;
                    GameBoard.LinkAuraSystem.InvalidateCache();
                    int withArrow = target.GetPower();
                    Check(withArrow == 3,
                        $"D4 连接条目 scope=0 箭头指向 → 攻击力 {withArrow}（期望 3=1+2）");

                    // scope=3：场地属性降低条目（负值·对方作用面·无需箭头）
                    var carrierData = DataOf(carrierCard);
                    carrierData.LinkAuras.Add(new LinkAuraData { stat = "Power", value = -1, scope = 3 });
                    GameBoard.LinkAuraSystem.InvalidateCache();
                    int fieldDebuff = target.GetPower();
                    Check(fieldDebuff == 2,
                        $"D4 场地属性降低条目 scope=3 → 攻击力 {fieldDebuff}（期望 2=3−1，无需箭头打对方战场）");
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        // ======================================== E 文案 ========================================

        private static void RunTextChecks()
        {
            Log.AppendLine("[Verify·E] ---- 卡面文案（量词只标类别前 / 全部档）----");

            // E1 关键词：「赋予己方战场的1个生物缴械…」（count 0 → 全部）
            var disarm = Row(AtomicEffectType.GrantDisarm);
            Check(disarm != null, "E1 前置：原子表能取到 GrantDisarm 行（文案探针）");
            if (disarm != null)
            {
                var text1 = AtomText.Render(disarm, Entry(AtomicEffectType.GrantDisarm, count: 1), null);
                Check(text1.Contains("赋予己方战场的1个生物缴械"),
                    $"E1 关键词 count=1 文案（实际：「{text1}」）");
                var textAll = AtomText.Render(disarm, Entry(AtomicEffectType.GrantDisarm, count: 0), null);
                Check(textAll.Contains("赋予己方战场的全部生物缴械") && !textAll.Contains("1个"),
                    $"E1 关键词 count=0 文案（实际：「{textAll}」）");
            }

            // E2 费用增减：「为1张/2张/全部 单侧手牌中的卡…」
            var up = Row(AtomicEffectType.AddCostUpGray);
            var down = Row(AtomicEffectType.AddCostDownGray);
            Check(up != null && down != null, "E2 前置：原子表能取到费用增减两灰行");
            if (up != null && down != null)
            {
                var down1 = AtomText.Render(down, Entry(AtomicEffectType.AddCostDownGray, count: 1), null);
                Check(down1.Contains("为1张己方手牌中的卡"),
                    $"E2 减费 count=1 文案（实际：「{down1}」）");
                var downAll = AtomText.Render(down, Entry(AtomicEffectType.AddCostDownGray, count: 0), null);
                Check(downAll.Contains("为全部己方手牌中的卡") && !downAll.Contains("1张"),
                    $"E2 减费 count=0 文案（实际：「{downAll}」）");
                var up2 = AtomText.Render(up, Entry(AtomicEffectType.AddCostUpGray, count: 2), null);
                Check(up2.Contains("为2张对方手牌中的卡"),
                    $"E2 增费 count=2 文案（实际：「{up2}」）");
            }
        }

        // ======================================== 夹具辅助（同 AuraCounterStackVerifier / LaeMultiTargetVerifier） ========================================

        private static CardData DataOf(Card c) => (c as CardWrapper)?.GetData();

        /// <summary>推进到 p1 主阶段（跳过准备阶段的元素池询问——同 LaeMultiTargetVerifier）。</summary>
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

        private static bool IsName(CardData c, string name) => c != null && c.CardName == name;

        private static bool IsName(Card c, string name) => c is IHasName n && n.CardName == name;

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

        /// <summary>从牌库/手牌找首张同名卡，移出原区后走进场统一出口（同 AuraCounterStackVerifier）。</summary>
        private static Card PlaceFromDeck(ZoneManager zm, Player owner, string name)
        {
            var card = zm.GetCards(owner, Zone.Hand).FirstOrDefault(c => IsName(c, name))
                       ?? zm.GetCards(owner, Zone.Deck).FirstOrDefault(c => IsName(c, name));
            if (card == null) return null;
            var from = zm.IsCardInZone(card, owner, Zone.Hand) ? Zone.Hand : Zone.Deck;
            zm.GetZoneContainer(owner).Remove(card, from);
            return zm.TryAddToBattlefield(card, owner) ? card : null;
        }

        /// <summary>光环源 → 受益者的箭头方向（BoardMath 映射同连接光环几何）。</summary>
        private static HexDirection ArrowTo(GameBoard.BoardState board, Card from, Card to)
        {
            if (!board.TryGetCell(from, out int fx, out int fz)) return HexDirection.None;
            if (!board.TryGetCell(to, out int tx, out int tz)) return HexDirection.None;
            foreach (GameBoard.BoardDirection d in Enum.GetValues(typeof(GameBoard.BoardDirection)))
            {
                var (nx, nz) = GameBoard.BoardMath.Neighbor(fx, fz, d);
                if (nx == tx && nz == tz) return GameBoard.BoardMath.ArrowOf(d);
            }
            return HexDirection.None;
        }
    }
}
