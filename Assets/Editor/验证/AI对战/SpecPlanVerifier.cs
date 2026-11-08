// SPDX-FileCopyrightText: 2026 WWXTT
// SPDX-License-Identifier: BUSL-1.1

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using CardCore;
using CardCore.AI.NeuralEnv;
using CardCore.Network;
using SynergyUI;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// spec 计划验证器（2026-11-08 目标选择重构步7·docs/目标选择重构_修改计划_修订版.md §8.2）：
    /// ① 铁律——handler 内发起未批准选择须被守卫点名（参照族执行全程 SpecGuard=0）；
    /// ② spec 计划——活体炸弹 2 spec（Reference 前/Target 后、SpecKey 唯一、Enumerate/Auto 各就位）；
    /// ③ 人类窗口——参照窗→受害者窗两窗、取消参照=撤销发动、无第三窗（计数桩选择器）；
    /// ④ 并列共享——同域并列两原子单 Target spec（沉默+消灭形态：一次解析两原子同目标）；
    /// ⑤ LAE——参照 Enumerate N 行、受体 Auto 不展开（不乘 M）；
    /// ⑥ 网络——MsgIntentPlayCard.References / MsgSelectRequest.SpecKey MemoryPack 往返；
    /// ⑦ 计价——ValueExpr/ValueMax 不影响定价（恒按 Value 名义锚）。
    /// </summary>
    internal static class SpecPlanVerifier
    {
        private const string OwnBigName = "虚空巨像";   // 9/9 参照位
        private const string OwnGuardName = "林地守卫"; // 0/7 次参照位
        private const string EnemyAName = "火灵·攻";    // 1 血敌方受害者位

        private readonly struct Check
        {
            public readonly bool Ok;
            public readonly string Text;
            public Check(bool ok, string text) { Ok = ok; Text = text; }
        }

        [MenuItem("Tools/验证/spec计划（目标选择重构）")]
        public static void Run()
        {
            var log = new StringBuilder();
            var checks = new List<Check>();
            int guardErrors = 0;
            Application.LogCallback hook = (condition, stackTrace, type) =>
            {
                if (type == LogType.Error && condition != null && condition.Contains("[SpecGuard]")) guardErrors++;
            };
            Application.logMessageReceived += hook;
            try
            {
                RunSpecPlanScenario(log, checks);
                RunSharedGroupScenario(log, checks);
                RunLaeScenario(log, checks);
                RunNetworkScenario(log, checks);
                RunCostScenario(log, checks);
                RunRenderScenario(log, checks);
            }
            finally
            {
                Application.logMessageReceived -= hook;
            }

            // ① 铁律：全程（含引擎施放结算）零 SpecGuard 告警——参照族 handler 内选择=0
            checks.Add(new Check(guardErrors == 0,
                $"铁律：全程 SpecGuard 点名={guardErrors}（期望 0——参照族选择全部在引擎 spec 计划）"));

            int fail = checks.Count(c => !c.Ok);
            foreach (var c in checks)
                log.AppendLine(c.Ok ? "[PASS] " + c.Text : "[FAIL] " + c.Text);
            string head = $"==== SpecPlanVerifier {(fail == 0 ? "全过" : $"失败 {fail}/{checks.Count} 项")} ====";
            Debug.Log(head + "\n" + log);
        }

        // ======================================== 夹具 ========================================

        /// <summary>活体炸弹探针卡（RefAtom 同口径 + valueExpr=ref.power）。</summary>
        private static CardData BombProbeCard(string cardId, string effId)
        {
            var row = Attribute.AtomicEffectTable.GetByType(AtomicEffectType.BurstDamage);
            return new CardData
            {
                ID = cardId, CardName = "探针·spec炸弹", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Id = effId, DisplayName = "探针·spec炸弹", TriggerTiming = 19, ActivationType = 2,
                        SelectionMode = -1, TargetCount = -2,
                        AtomicEffects = new List<AtomicEffectEntry>
                        {
                            new AtomicEffectEntry
                            {
                                refId = row.HashId, kinds = new List<int> { 1 }, count = 1,
                                valueExpr = "ref.power", valueMax = 12,
                            },
                        },
                    },
                },
            };
        }

        /// <summary>战场夹具：p1 场上 巨像+守卫（参照候选 2），p2 场上 火灵（受害者候选 2 含角色）。</summary>
        private static GameCore SetupBoard(CardData probe)
        {
            var core = GameCore.Instance;
            var catalog = CardCatalog.LoadAll().ToList();
            var deck1 = new List<CardData>();
            for (int i = 0; i < 3; i++) deck1.Add(ByName(catalog, OwnBigName));
            for (int i = 0; i < 2; i++) deck1.Add(ByName(catalog, OwnGuardName));
            deck1.Add(probe);
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) { deck2.Add(ByName(catalog, EnemyAName)); deck2.Add(ByName(catalog, OwnBigName)); }
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            return core;
        }

        private static CardData ByName(List<CardData> catalog, string name)
            => catalog.FirstOrDefault(c => c != null && c.CardName == name)
               ?? throw new InvalidOperationException($"卡池找不到「{name}」");

        private static Card FindAndMove(ZoneManager zm, Player owner, string name, Zone to)
        {
            var found = zm.GetCards(owner, Zone.Deck).FirstOrDefault(c => c is IHasName n && n.CardName == name)
                        ?? zm.GetCards(owner, Zone.Hand).FirstOrDefault(c => c is IHasName n && n.CardName == name);
            if (found == null) return null;
            var from = zm.IsCardInZone(found, owner, Zone.Hand) ? Zone.Hand : Zone.Deck;
            if (from != to) zm.MoveCard(found, owner, from, to);
            return found;
        }

        // ======================================== ②③ spec 计划与人类窗口 ========================================

        /// <summary>计数桩选择器（③ 人类窗口序验证）：记录每次问选的候选构成，恒选首项/可取消。</summary>
        private sealed class CountingSelector : ITargetSelector
        {
            public List<List<Entity>> Asks = new List<List<Entity>>();
            public bool CancelNext;
            public UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
                int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
            {
                Asks.Add(labels == null ? new List<Entity>() : new List<Entity>()); // 候选经 Ex 通道不可见——数量以调用侧断言
                var picked = CancelNext ? new List<int>() : new List<int> { 0 };
                return UniTask.FromResult(picked);
            }
        }

        private static void RunSpecPlanScenario(StringBuilder log, List<Check> checks)
        {
            var tmp = BombProbeCard("C_TMP_SPECP_PLAN", "EFF_TMP_SPECP");
            var core = SetupBoard(tmp);
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true; p2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                GameBoard.LinkAuraSystem.Attach(board);
                try
                {
                    // 推进主阶段 + 布场
                    while (core.TurnEngine.CurrentPhase == null || core.TurnEngine.CurrentPhase.Phase != PhaseType.Main)
                    {
                        var ph = core.TurnEngine.CurrentPhase == null ? PhaseType.Standby : core.TurnEngine.CurrentPhase.Phase;
                        if (ph == PhaseType.Standby) GameActions.SkipElementPool(core, core.TurnEngine.TurnPlayer);
                        else core.TurnEngine.CheckPhaseTransition();
                    }
                    var zm = core.ZoneManager;
                    var enemy = FindAndMove(zm, p2, EnemyAName, Zone.Battlefield);
                    var big = FindAndMove(zm, p1, OwnBigName, Zone.Battlefield);
                    var guard = FindAndMove(zm, p1, OwnGuardName, Zone.Battlefield);
                    var tCard = FindAndMove(zm, p1, tmp.CardName, Zone.Hand);
                    core.ElementPool.AddMana(p1, ManaType.Red, null, 10);

                    var def = CardEffectConverter.ConvertAll(tmp.Effects, tmp.ID).First();
                    var ctx = new EffectExecutionContext
                    {
                        Source = p1, Controller = p1, ZoneManager = zm, ElementPool = core.ElementPool, ModeIndex = 0,
                    };

                    // ② spec 计划：2 spec（Reference 前/Target 后）、键唯一、AiMode 各就位
                    var plan = Attribute.EffectHandlerRegistry.CollectSpecPlan(def, ctx);
                    var refSpec = plan.FirstOrDefault(s => s.Role == Attribute.TargetSpecRole.Reference);
                    var tgtSpec = plan.FirstOrDefault(s => s.Role == Attribute.TargetSpecRole.Target);
                    checks.Add(new Check(plan.Count == 2
                        && refSpec != null && tgtSpec != null
                        && plan[0].Role == Attribute.TargetSpecRole.Reference
                        && plan[1].Role == Attribute.TargetSpecRole.Target,
                        $"spec 计划：期望 2 spec（Reference 前/Target 后），实际 {plan.Count} spec 序=[{string.Join(",", plan.Select(s => s.Role.ToString()))}]"));
                    checks.Add(new Check(plan.Select(s => s.SpecKey).Distinct().Count() == plan.Count
                        && !plan.Any(s => string.IsNullOrEmpty(s.SpecKey)),
                        "SpecKey 唯一且非空：" + string.Join(" | ", plan.Select(s => s.SpecKey).ToArray())));
                    string refInfo = refSpec == null || refSpec.Candidates == null
                        ? "null" : refSpec.Candidates.Count + "c AiMode=" + refSpec.AiMode;
                    checks.Add(new Check(refSpec != null && refSpec.AiMode == Attribute.AiPickMode.Enumerate
                        && refSpec.Candidates != null && refSpec.Candidates.Count == 2
                        && refSpec.Candidates.Contains(big) && refSpec.Candidates.Contains(guard)
                        && !refSpec.Candidates.Contains(p1),
                        $"参照 spec=Enumerate 候选 2（巨像+守卫·角色被 RefDomain 排除），实际 {refInfo}"));
                    checks.Add(new Check(tgtSpec != null && tgtSpec.AiMode == Attribute.AiPickMode.Auto,
                        $"受体 spec=AiMode.Auto（LAE 不展开·引擎/AiOrder 代选），实际 AiMode={tgtSpec?.AiMode}"));

                    // ③ 人类窗口：声明期=1 窗（参照·Cast 档）——桩选择器（p1 非 AI 才可达人类路径）
                    var prevSelector = TargetSelectionService.Current;
                    var counter = new CountingSelector();
                    int askCount = 0;
                    List<Entity> declaration = null;
                    TargetSelectionService.Current = counter;
                    p1.IsAI = false;
                    try
                    {
                        declaration = GameActions.CollectSpellDeclarationTargetsAsync(tCard, p1, 0).GetAwaiter().GetResult();
                        askCount = counter.Asks.Count;
                    }
                    finally { TargetSelectionService.Current = prevSelector; p1.IsAI = true; }
                    string declInfo = declaration == null ? "null(取消)"
                        : string.Join("+", declaration.Select(e => e is IHasName n ? n.CardName : "?").ToArray());
                    checks.Add(new Check(askCount == 1 && declaration != null && declaration.Count == 1 && ReferenceEquals(declaration[0], big),
                        $"声明期=1 窗（参照·Cast 档·首候选=巨像），实际 窗 {askCount} 个 预选={declInfo}"));

                    // 结算期=1 窗（受害者·引擎 spec 发起）：直接驱动执行器（不经 PlayCard 优先权机制）
                    int settleAsks = 0;
                    TargetSelectionService.Current = counter;
                    p1.IsAI = false;
                    try
                    {
                        var cast = new EffectInstance
                        {
                            Definition = def, Source = p1, Controller = p1,
                            Targets = new List<Entity>(declaration), CastCard = tCard, ModeIndex = 0,
                        };
                        core.StackEngine.GetExecutor().ExecuteAsync(cast, skipElementCost: true).GetAwaiter().GetResult();
                        settleAsks = counter.Asks.Count - askCount;
                    }
                    finally { TargetSelectionService.Current = prevSelector; p1.IsAI = true; }
                    checks.Add(new Check(settleAsks == 1,
                        $"结算期=1 窗（受害者·受体 spec 引擎发起），实际 {settleAsks} 窗（合计 {counter.Asks.Count} 窗·无第三窗）"));

                    // 取消参照=撤销发动：桩取消 → 声明期收集返回 null 且不再追问
                    var counter2 = new CountingSelector { CancelNext = true };
                    List<Entity> cancelled = new List<Entity>();
                    TargetSelectionService.Current = counter2;
                    p1.IsAI = false;
                    try { cancelled = GameActions.CollectSpellDeclarationTargetsAsync(tCard, p1, 0).GetAwaiter().GetResult(); }
                    finally { TargetSelectionService.Current = prevSelector; p1.IsAI = true; }
                    checks.Add(new Check(cancelled == null && counter2.Asks.Count == 1,
                        "取消参照=撤销发动（声明期收集返回 null·不再追问受害者）"));
                }
                finally
                {
                    GameBoard.LinkAuraSystem.Detach();
                }
            }
        }

        // ======================================== ④ 并列共享 ========================================

        private static void RunSharedGroupScenario(StringBuilder log, List<Check> checks)
        {
            // 同域并列两原子（沉默+消灭形态）：DealDamage×2 同 kinds=[2]——单 Target spec 共享
            var row = Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage);
            var card = new CardData
            {
                ID = "C_TMP_SHARED_PROBE", CardName = "探针·同域并列", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Id = "EFF_TMP_SHARED", DisplayName = "探针·同域并列", TriggerTiming = 19, ActivationType = 2,
                        SelectionMode = -1, TargetCount = -2,
                        AtomicEffects = new List<AtomicEffectEntry>
                        {
                            new AtomicEffectEntry { refId = row.HashId, value = 1, kinds = new List<int> { 2 }, count = 1 },
                            new AtomicEffectEntry { refId = row.HashId, value = 2, kinds = new List<int> { 2 }, count = 1 },
                        },
                    },
                },
            };
            var tmpHolder = card;
            var core = SetupBoard(tmpHolder);
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true; p2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                try
                {
                    while (core.TurnEngine.CurrentPhase == null || core.TurnEngine.CurrentPhase.Phase != PhaseType.Main)
                    {
                        var ph = core.TurnEngine.CurrentPhase == null ? PhaseType.Standby : core.TurnEngine.CurrentPhase.Phase;
                        if (ph == PhaseType.Standby) GameActions.SkipElementPool(core, core.TurnEngine.TurnPlayer);
                        else core.TurnEngine.CheckPhaseTransition();
                    }
                    var zm = core.ZoneManager;
                    var enemy = FindAndMove(zm, p2, EnemyAName, Zone.Battlefield);
                    var def = CardEffectConverter.ConvertAll(card.Effects, card.ID).First();
                    var ctx = new EffectExecutionContext
                    {
                        Source = p1, Controller = p1, ZoneManager = zm, ElementPool = core.ElementPool, ModeIndex = 0,
                    };
                    var plan = Attribute.EffectHandlerRegistry.CollectSpecPlan(def, ctx);
                    var targetSpecs = plan.Where(s => s.Role == Attribute.TargetSpecRole.Target).ToList();
                    checks.Add(new Check(plan.Count == 1 && targetSpecs.Count == 1
                        && targetSpecs[0].AtomIndices != null && targetSpecs[0].AtomIndices.Count == 2
                        && targetSpecs[0].AtomIndices.Contains(0) && targetSpecs[0].AtomIndices.Contains(1),
                        $"并列共享：同域两原子=1 个共享 Target spec（AtomIndices=[{string.Join(",", targetSpecs[0]?.AtomIndices ?? new List<int>())}]·单窗同目标）"));
                }
                finally { }
            }
        }

        // ======================================== ⑤ LAE：参照 Enumerate / 受体 Auto 不展开 ========================================

        private static void RunLaeScenario(StringBuilder log, List<Check> checks)
        {
            var tmp = BombProbeCard("C_TMP_SPECP_LAE", "EFF_TMP_LAE");
            var core = SetupBoard(tmp);
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true; p2.IsAI = true;
            using (var board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat()))
            {
                board.EnableAutoResync();
                try
                {
                    while (core.TurnEngine.CurrentPhase == null || core.TurnEngine.CurrentPhase.Phase != PhaseType.Main)
                    {
                        var ph = core.TurnEngine.CurrentPhase == null ? PhaseType.Standby : core.TurnEngine.CurrentPhase.Phase;
                        if (ph == PhaseType.Standby) GameActions.SkipElementPool(core, core.TurnEngine.TurnPlayer);
                        else core.TurnEngine.CheckPhaseTransition();
                    }
                    var zm = core.ZoneManager;
                    FindAndMove(zm, p2, EnemyAName, Zone.Battlefield);
                    FindAndMove(zm, p1, OwnBigName, Zone.Battlefield);
                    FindAndMove(zm, p1, OwnGuardName, Zone.Battlefield);
                    var tCard = FindAndMove(zm, p1, tmp.CardName, Zone.Hand);
                    core.ElementPool.AddMana(p1, ManaType.Red, null, 10);

                    var lae = new LegalActionEnumerator();
                    lae.Enumerate(core, p1);
                    var rows = lae.Actions.Where(a => a.Type == TideActionType.PlayCard && ReferenceEquals(a.Card, tCard)).ToList();
                    var refRows = rows.Where(r => r.Reference != null).ToList();
                    checks.Add(new Check(rows.Count == 2 && rows.All(r => r.Targets != null && r.Targets.Count == 1)
                        && refRows.Count == 2,
                        $"LAE：参照 Enumerate=2 行（每己方生物一行·受体 Auto 不展开非 2×M），实际 {rows.Count} 行 参照镜像 {refRows.Count} 行"));
                    var refSlot = lae.Actions.Where(a => ReferenceEquals(a.Card, tCard) && a.Type == TideActionType.PlayCard)
                        .Select(a => a.ReferenceIndex).Distinct().ToList();
                    checks.Add(new Check(refSlot.All(i => i >= 0) && refSlot.Count == 2,
                        $"特征 [11] referenceIndex 槽位非 -1 且逐行不同（模型可见参照选位）：[{string.Join(",", refSlot)}]"));
                }
                finally { }
            }
        }

        // ======================================== ⑥ 网络：Refs/SpecKey 往返 ========================================

        private static void RunNetworkScenario(StringBuilder log, List<Check> checks)
        {
            var intent = new MsgIntentPlayCard
            {
                CardRuntimeId = 777u,
                Targets = new[] { new NetEntityRef { RuntimeId = 1u, Seat = 0, IsPlayer = false, CardId = "A" } },
                FromZone = (int)Zone.Hand,
                ModeIndex = 2,
                References = new[] { new NetEntityRef { RuntimeId = 9u, Seat = 1, IsPlayer = false, CardId = "REF" } },
            };
            var wire = MemoryPack.MemoryPackSerializer.Serialize(intent);
            var back = MemoryPack.MemoryPackSerializer.Deserialize<MsgIntentPlayCard>(wire);
            checks.Add(new Check(back != null && back.CardRuntimeId == 777u && back.ModeIndex == 2
                && back.References != null && back.References.Length == 1 && back.References[0].RuntimeId == 9u
                && back.References[0].Seat == 1 && back.References[0].CardId == "REF",
                "MsgIntentPlayCard.References MemoryPack 往返（参照分角色上送）"));

            var sel = new MsgSelectRequest
            {
                RequestId = 5, ChooserSeat = 1, Title = "选择活体炸弹的受害者", AllowCancel = false,
                TimeoutSeconds = 30f, Min = 1, Max = 1,
                Candidates = new[] { new NetEntityRef { RuntimeId = 3u, Seat = 1, IsPlayer = true, CardId = null } },
                SpecKey = "atom:0/spec:1/role:target",
            };
            var wire2 = MemoryPack.MemoryPackSerializer.Serialize(sel);
            var back2 = MemoryPack.MemoryPackSerializer.Deserialize<MsgSelectRequest>(wire2);
            checks.Add(new Check(back2 != null && back2.SpecKey == "atom:0/spec:1/role:target" && back2.RequestId == 5,
                "MsgSelectRequest.SpecKey MemoryPack 往返（显式关联键·不靠弹窗顺序）"));

            var stackDto = new StackItemDTO
            {
                Source = new NetEntityRef { RuntimeId = 1u, Seat = 0, IsPlayer = true, CardId = null },
                IsCardCast = true, EffectId = "0909E61B", ModeIndex = 0,
                Targets = new NetEntityRef[0],
                References = new[] { new NetEntityRef { RuntimeId = 42u, Seat = 0, IsPlayer = false, CardId = "BIG" } },
            };
            var wire3 = MemoryPack.MemoryPackSerializer.Serialize(stackDto);
            var back3 = MemoryPack.MemoryPackSerializer.Deserialize<StackItemDTO>(wire3);
            checks.Add(new Check(back3 != null && back3.References != null && back3.References.Length == 1
                && back3.References[0].RuntimeId == 42u,
                "StackItemDTO.References MemoryPack 往返（栈条目携带参照）"));
        }

        // ======================================== ⑦ 计价：ValueExpr/ValueMax 不影响定价 ========================================

        private static void RunCostScenario(StringBuilder log, List<Check> checks)
        {
            var tmp = BombProbeCard("C_TMP_SPECP_COST", "EFF_TMP_COST");
            var def = CardEffectConverter.ConvertAll(tmp.Effects, tmp.ID).First();
            var atom = def.Effects[0];
            var cost = CostDerivationService.DeriveElementCosts(def, 0);
            // 炸弹行 ManaList=红3 × Value 1 = 3 红；valueExpr/valueMax 在场不改变锚价
            checks.Add(new Check(atom.ValueExpr == "ref.power" && atom.ValueMax == 12
                && Math.Abs(cost.Total - 3f) < 0.01f && Math.Abs(cost[ManaType.Red] - 3f) < 0.01f,
                $"定价恒按 Value 名义锚：ValueExpr='{atom.ValueExpr}' ValueMax={atom.ValueMax} 在场 → 费用=红{cost[ManaType.Red]:0.#}（总 {cost.Total:0.#}·期望红3）"));

            // 哈希区分（步6 §4.1 验收）：同 refId 有/无 valueExpr → 内容哈希不同；同输入重算稳定
            var fxExpr = BombProbeCard("C_TMP_SPECP_HA", "EFF_TMP_HA").Effects[0];
            var fxNoExpr = BombProbeCard("C_TMP_SPESP_HB", "EFF_TMP_HB").Effects[0];
            fxNoExpr.AtomicEffects[0].valueExpr = null;
            string hExpr = ContentHasher.HashEffectOf(fxExpr);
            string hNoExpr = ContentHasher.HashEffectOf(fxNoExpr);
            checks.Add(new Check(!string.IsNullOrEmpty(hExpr) && hExpr != hNoExpr
                && hExpr == ContentHasher.HashEffectOf(fxExpr),
                $"内容哈希区分：同 refId 有/无 valueExpr → {(hExpr != null && hExpr.Length >= 8 ? hExpr.Substring(0, 8) : hExpr)}… vs {(hNoExpr != null && hNoExpr.Length >= 8 ? hNoExpr.Substring(0, 8) : hNoExpr)}…（不同且重算稳定）"));

            // 上界两查（步6 §4.3）：valueMax<value → Error 拒绝；valueMax 超同类分布 → Warn 告警
            int boundErrors = 0, boundWarns = 0;
            Application.LogCallback boundHook = (condition, stackTrace, type) =>
            {
                if (condition == null || (!condition.Contains("ValueMax") && !condition.Contains("valueMax"))) return;
                if (type == LogType.Error) boundErrors++;
                else if (type == LogType.Warning) boundWarns++;
            };
            Application.logMessageReceived += boundHook;
            try
            {
                var badCard = BombProbeCard("C_TMP_SPESP_B1", "EFF_TMP_B1");
                badCard.Effects[0].AtomicEffects[0].value = 3;
                badCard.Effects[0].AtomicEffects[0].valueMax = 2; // < 名义值 → 拒绝
                var warnCard = BombProbeCard("C_TMP_SPESP_B2", "EFF_TMP_B2"); // valueMax=12 > 参照上限 9 → 告警
                var validate = typeof(CardLoader).GetMethod("ValidateValueBounds",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                validate.Invoke(null, new object[] { new List<CardData> { badCard, warnCard } });
            }
            finally
            {
                Application.logMessageReceived -= boundHook;
            }
            checks.Add(new Check(boundErrors == 1 && boundWarns == 1,
                $"上界两查触发：valueMax<value 拒绝={boundErrors}（期望1）、超分布告警={boundWarns}（期望1·valueMax12 vs 参照上限9）"));
        }
        // ======================================== ⑧ {reference} 落表渲染 ========================================

        /// <summary>{reference} 正式落表（2026-11-08 用户改三条 DisplayName）：占位符渲染走 RefDomain
        /// 短语（AtomText {reference}→AxisPhraseOf(RefDomain)），无残留字面量；{target} 走 Domain。</summary>
        private static void RunRenderScenario(StringBuilder log, List<Check> checks)
        {
            var cases = new[]
            {
                new { type = AtomicEffectType.BurstDamage, kind = 1, expect = "以己方战场的生物为参照" },
                new { type = AtomicEffectType.LivingArmor, kind = 1, expect = "以己方战场的生物为参照" },
                new { type = AtomicEffectType.ShieldSlam, kind = 2, expect = "移除" },
            };
            foreach (var c in cases)
            {
                var cfg = Attribute.AtomicEffectTable.GetByType(c.type);
                var entry = new AtomicEffectEntry { refId = cfg.HashId, kinds = new List<int> { c.kind }, count = 1 };
                string rendered = AtomText.Render(cfg, entry, null);
                checks.Add(new Check(!string.IsNullOrEmpty(rendered)
                    && !rendered.Contains("{reference}") && !rendered.Contains("{target}")
                    && rendered.Contains(c.expect),
                    $"{cfg.DisplayName}（{c.type}）渲染：期望含「{c.expect}」且占位符零残留，实际 {rendered}"));
            }
        }
    }
}
#endif
