using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CardCore;
using CardCore.Attribute;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 2026-11-08 规则地基·阶段 1 验证（引擎奖励/发动档单一事实源）。
    /// 本文件只写测试：不改数据、不改核心实现；失败只报告。
    ///
    /// A 三类引擎奖励类型映射：主动奖励引擎（倒计时/死亡计数/元素充盈）、自动奖励引擎
    ///   （运势/拼枪/团结）、载荷引擎（附加诅咒/祝福，两旗皆假）互斥分区；
    ///   行级主动行（附加诅咒/附加祝福——模拟/变形 2026-11-08 退役出组合）与自动引擎行（其余六行）互斥分区，
    ///   原子表 EngineTrunk 位全行有钉（新行未登记会被抓）。
    /// B 奖励资格 R4 单谓词（2026-11-10 修订：引擎奖励与一般分支奖励合并走
    ///   BranchRewardRejectReason，不再按发动档分档——任意发动档的并列效果均放行；
    ///   EngineRewardRejectReason 保留为转发口）。
    ///   正例=并列形状（1~2 主干原子·任意发动档）；反例=引擎主干/光环形态/空效果/
    ///   带代价行（代价=费用抵扣不是奖励）/无 Domain 原子（检索等无域行·混合同拒）/
    ///   嵌套分支（直挂载荷与遗留 kind=1 门步折算同拒）/超并列（>2 主干原子）。
    /// C 发动方式钉：EngineActivationPinOf 期望值（主动行→Voluntary、自动引擎行→Automatic、
    ///   非引擎→null）；EngineActivationFree 已随双选面整删（反射证不存在）；
    ///   EngineHeaderPresetOf——固定三引擎保持原时机、运势/拼枪/团结转自然缺省（FreeTiming，
    ///   占位缺省值=原固定时机）、诅咒/祝福保持主动。
    ///
    /// 夹具全部内存合成（不落盘）；入口：菜单 Tools/验证/引擎奖励规则（E2E）；
    /// Run() 返回报告文本（[Verify] 风格日志）。
    /// </summary>
    public static class EngineRewardRulesVerifier
    {
        private static readonly List<(bool Pass, string Text)> Checks = new List<(bool, string)>();
        private static readonly StringBuilder Log = new StringBuilder();

        // 三档枚举面（期望值钉死——目录谓词与钉值表改动的看门断言）
        private static readonly BranchEngineKind[] ActiveRewardEngines =
            { BranchEngineKind.Countdown, BranchEngineKind.DeathToll, BranchEngineKind.ManaSurplus };
        private static readonly BranchEngineKind[] AutomaticRewardEngines =
            { BranchEngineKind.LuckRoll, BranchEngineKind.Clash, BranchEngineKind.Unity };
        private static readonly BranchEngineKind[] PayloadEngines =
            { BranchEngineKind.CurseOnDraw, BranchEngineKind.BlessingOnDraw };

        private static readonly AtomicEffectType[] ActiveRowTypes =
        {
            AtomicEffectType.EngineCurseOnDraw, AtomicEffectType.EngineBlessingOnDraw,
        };
        private static readonly AtomicEffectType[] AutomaticRowTypes =
        {
            AtomicEffectType.EngineCountdown, AtomicEffectType.EngineLuckRoll, AtomicEffectType.EngineClash,
            AtomicEffectType.EngineDeathToll, AtomicEffectType.EngineManaSurplus, AtomicEffectType.EngineUnity,
        };

        [MenuItem("Tools/验证/引擎奖励规则（E2E）")]
        public static void RunFromMenu()
        {
            var report = Run();
            Debug.Log(report);
            if (Checks.Count(c => !c.Pass) > 0)
                Debug.LogError("[Verify·引擎奖励规则] 存在失败断言，详见上方报告。");
        }

        public static string Run()
        {
            Checks.Clear();
            Log.Clear();
            Log.AppendLine("[Verify·引擎奖励规则] ==== 开始（2026-11-08 规则地基·阶段 1）====");
            try
            {
                RunMappingChecks();      // A
                RunRejectReasonChecks(); // B
                RunPinChecks();          // C
            }
            catch (Exception ex)
            {
                Check(false, $"异常中断：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            int fails = Checks.Count(c => !c.Pass);
            Log.AppendLine(fails == 0
                ? $"[Verify·引擎奖励规则] ==== 全过（{Checks.Count} 项断言）===="
                : $"[Verify·引擎奖励规则] ==== 失败 {fails}/{Checks.Count} 项 ====");
            return Log.ToString();
        }

        private static void Check(bool ok, string text)
        {
            Checks.Add((ok, text));
            if (!ok) Log.AppendLine("[Verify·FAIL] " + text);
        }

        // ======================================== 夹具 ========================================

        private static Attribute.AtomicEffectConfig Row(AtomicEffectType type)
            => Attribute.AtomicEffectTable.GetByType(type);

        private static AtomicEffectEntry Entry(AtomicEffectType type, int value = 1, int count = 1)
            => new AtomicEffectEntry { refId = Row(type)?.HashId, value = value, count = count };

        /// <summary>内存合成完整卡内效果（EngineRewardRejectReason 输入契约：完整效果非裸 header）。</summary>
        private static CardEffectData Fx(int timing, int activation, params AtomicEffectEntry[] atoms)
            => new CardEffectData
            {
                Id = "EFF_ENGINE_RULE_PROBE",
                DisplayName = "引擎奖励规则探针",
                TriggerTiming = timing,
                ActivationType = activation,
                SelectionMode = -1,
                TargetCount = -2,
                AtomicEffects = atoms.ToList(),
            };

        // ======================================== A 三类引擎奖励类型映射 ========================================

        private static void RunMappingChecks()
        {
            Log.AppendLine("[Verify·A] ---- 奖励类型映射（主动奖励 / 自动奖励 / 载荷三分）----");

            var activeHit = new List<BranchEngineKind>();
            var autoHit = new List<BranchEngineKind>();
            var both = new List<BranchEngineKind>();
            foreach (BranchEngineKind k in Enum.GetValues(typeof(BranchEngineKind)))
            {
                bool a = ComposerCatalog.IsActiveRewardEngine(k);
                bool b = ComposerCatalog.IsAutomaticRewardEngine(k);
                if (a) activeHit.Add(k);
                if (b) autoHit.Add(k);
                if (a && b) both.Add(k);
            }
            Check(activeHit.Count == ActiveRewardEngines.Length && !activeHit.Except(ActiveRewardEngines).Any(),
                $"A1 主动奖励引擎={string.Join("/", activeHit)}（期望 倒计时/死亡计数/元素充盈）");
            Check(autoHit.Count == AutomaticRewardEngines.Length && !autoHit.Except(AutomaticRewardEngines).Any(),
                $"A1 自动奖励引擎={string.Join("/", autoHit)}（期望 运势/拼枪/团结）");
            Check(both.Count == 0, $"A1 三类互斥：无引擎同时命中两档（违规：{string.Join("/", both)}）");
            Check(PayloadEngines.All(k => !ComposerCatalog.IsActiveRewardEngine(k)
                                       && !ComposerCatalog.IsAutomaticRewardEngine(k)),
                "A1 载荷引擎（附加诅咒/祝福）两旗皆假（奖励资格另走 CursePayloadRejectReason）");

            // 行级分区：主动行 / 自动引擎行 互斥且并集=引擎主干行全集
            //（模拟/变形 2026-11-08 退役出组合——挂「系统」位整库隐藏，不再占主动行）
            var rowActive = new List<AtomicEffectType>();
            var rowAuto = new List<AtomicEffectType>();
            foreach (AtomicEffectType t in Enum.GetValues(typeof(AtomicEffectType)))
            {
                if (ComposerCatalog.IsActiveFreeBranchRow(t)) rowActive.Add(t);
                if (ComposerCatalog.IsAutomaticFreeBranchRow(t)) rowAuto.Add(t);
            }
            Check(rowActive.Count == ActiveRowTypes.Length && !rowActive.Except(ActiveRowTypes).Any(),
                $"A2 主动行={string.Join("/", rowActive)}（期望 附加诅咒/附加祝福）");
            Check(rowAuto.Count == AutomaticRowTypes.Length && !rowAuto.Except(AutomaticRowTypes).Any(),
                $"A2 自动引擎行={string.Join("/", rowAuto)}（期望 倒计时/运势/拼点/死亡计数/元素充盈/团结六行）");
            Check(!rowActive.Intersect(rowAuto).Any(), "A2 行级两集互斥");
            var nonEngine = new[] { AtomicEffectType.DealDamage, AtomicEffectType.Heal, AtomicEffectType.GateLifeBelow };
            Check(nonEngine.All(t => !ComposerCatalog.IsActiveFreeBranchRow(t)
                                  && !ComposerCatalog.IsAutomaticFreeBranchRow(t)),
                "A2 非引擎行（伤害/回复/局面门抽检）两集皆不入");

            // 一致性：自动引擎行 ⟺ 引擎种类 ∈ 主动奖励 ∪ 自动奖励；主动行全部为载荷引擎行
            bool consistent = AutomaticRowTypes.All(t => ActiveRewardEngines.Concat(AutomaticRewardEngines)
                             .Contains(ComposerCatalog.EngineKindOf(t)));
            Check(consistent, "A3 自动引擎行与奖励引擎并集一致（行级⟺引擎级同口径）");
            Check(ActiveRowTypes.All(t => PayloadEngines.Contains(ComposerCatalog.EngineKindOf(t))),
                "A3 主动行全部为载荷引擎行（附加诅咒/附加祝福）");

            // 表级：原子表 EngineTrunk 位全行有钉且各归一集（新行未登记会被抓）
            var unpinned = new List<string>();
            int trunkRows = 0;
            foreach (var cfg in Attribute.AtomicEffectTable.GetAll())
            {
                if (cfg == null || !ComposerCatalog.HasMountBit(cfg, MountKind.EngineTrunk)) continue;
                trunkRows++;
                if (!Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)
                    || ComposerCatalog.EngineActivationPinOf(t) == null)
                    unpinned.Add(cfg.EnumName ?? cfg.HashId);
            }
            Check(trunkRows >= 8, $"A4 原子表引擎主干行 {trunkRows} 行（期望 ≥8——模拟/变形已退役挂「系统」位）");
            Check(unpinned.Count == 0,
                $"A4 全部引擎主干行均有发动方式钉（无钉：{string.Join("、", unpinned)}；期望无）");
        }

        // ======================================== B 奖励资格 R4 单谓词（正反例） ========================================

        private static void RunRejectReasonChecks()
        {
            Log.AppendLine("[Verify·B] ---- 奖励资格 R4 单谓词（2026-11-10：引擎奖励与分支奖励同口径，不限发动档）----");

            // 夹具：主动效果（Activate_*）/ 自动效果（OnDraw=真实触发事件锚）/ 声明档与时机错配
            var activeFx = Fx((int)TriggerTiming.Activate_Active, 2, Entry(AtomicEffectType.Heal, value: 2));
            var autoFx = Fx((int)TriggerTiming.OnDraw, 1, Entry(AtomicEffectType.Heal, value: 2));

            // B1 任意发动档都可通过新谓词（R4：分档闸整删——主动奖励引擎不再只收主动、自动奖励引擎
            // 不再只收自动；发射/评估时机由引擎自理）。全部六奖励引擎 + 载荷/None 同一放行面。
            foreach (var engine in ActiveRewardEngines.Concat(AutomaticRewardEngines)
                         .Concat(PayloadEngines).Concat(new[] { BranchEngineKind.None }))
            {
                var a = EffectEligibility.BranchRewardRejectReason(activeFx);
                var b = EffectEligibility.BranchRewardRejectReason(autoFx);
                Check(a == null && b == null,
                    $"B1 {engine} 主动形态：{a ?? "过"} / 自动形态：{b ?? "过"}（期望任意发动档并列效果均放行）");
                // 转发口等价（旧签名保留兼容——消费点不换即同语义）
                var fwd = EffectEligibility.EngineRewardRejectReason(activeFx, engine);
                Check(fwd == a, $"B1 {engine} EngineRewardRejectReason 转发与 BranchRewardRejectReason 等价（{fwd ?? "过"} vs {a ?? "过"}）");
            }

            // B2 并列形状正例：单原子/双原子主干均通过（1~2 主干原子=并列形状）
            var dualFx = Fx((int)TriggerTiming.Activate_Active, 2,
                Entry(AtomicEffectType.Heal, value: 2), Entry(AtomicEffectType.DrawCard));
            var dualOk = EffectEligibility.BranchRewardRejectReason(dualFx);
            Check(dualOk == null, $"B2 双原子并列效果通过（实际：{dualOk ?? "无"}）");

            // B3 通用闸（引擎主干/光环形态/空效果——R4 保留）
            var trunkFx = Fx((int)TriggerTiming.Activate_Active, 2,
                new AtomicEffectEntry
                {
                    refId = Row(AtomicEffectType.EngineCountdown)?.HashId,
                    branch = new BranchEntryData { settle = (int)BranchSettleKind.Engine, engine = (int)BranchEngineKind.Countdown },
                });
            var trunkReason = EffectEligibility.BranchRewardRejectReason(trunkFx);
            Check(trunkReason != null && trunkReason.Contains("引擎主干"),
                $"B3 引擎主干作奖励被拒（实际：{trunkReason ?? "null"}，期望点名引擎主干）");

            var auraFx = Fx((int)TriggerTiming.Activate_Active, 2, Entry(AtomicEffectType.Heal, value: 2));
            auraFx.ArrowDirections = 1;
            var auraReason = EffectEligibility.BranchRewardRejectReason(auraFx);
            Check(auraReason != null && auraReason.Contains("光环形态"),
                $"B3 光环形态效果作奖励被拒（实际：{auraReason ?? "null"}）");

            var emptyFx = Fx((int)TriggerTiming.Activate_Active, 2);
            var emptyReason = EffectEligibility.BranchRewardRejectReason(emptyFx);
            Check(emptyReason != null && emptyReason.Contains("空效果"),
                $"B3 空效果（无原子）被拒（实际：{emptyReason ?? "null"}）");

            // B4 带代价行拒（R4 新增：代价=费用抵扣不是奖励——全价≤3、同色逐行抵扣口径）
            var costFx = Fx((int)TriggerTiming.Activate_Active, 2, Entry(AtomicEffectType.Heal, value: 2));
            costFx.Costs = new List<CostEntry>
                { new CostEntry { CostType = (int)CostType.Payload, Value = 1 } };
            var costReason = EffectEligibility.BranchRewardRejectReason(costFx);
            Check(costReason != null && costReason.Contains("代价"),
                $"B4 带代价行的效果作奖励被拒（实际：{costReason ?? "null"}，期望点名代价非奖励）");

            // B5 无 Domain 原子（2026-11-08 阶段4 入闸·R4 保留）：含无域行（检索等——与
            // 「无Domain不参与效果组合」同口径）的效果整体不可作奖励——奖励发射/目标解析走域管线。
            var domainlessActiveFx = Fx((int)TriggerTiming.Activate_Active, 2, Entry(AtomicEffectType.SearchDeck));
            var domainlessAutoFx = Fx((int)TriggerTiming.OnDraw, 1, Entry(AtomicEffectType.SearchDeck));
            var dl1 = EffectEligibility.BranchRewardRejectReason(domainlessActiveFx);
            Check(dl1 != null && dl1.Contains("无目标域"),
                $"B5 主动档奖励含无Domain原子（检索）被拒（实际：{dl1 ?? "null"}）");
            var dl2 = EffectEligibility.BranchRewardRejectReason(domainlessAutoFx);
            Check(dl2 != null && dl2.Contains("无目标域"),
                $"B5 自动档奖励含无Domain原子（检索）被拒（实际：{dl2 ?? "null"}）");
            // 混合（带域+无域并存）同拒——条款按「任一原子无域」整体拒绝
            var mixedFx = Fx((int)TriggerTiming.Activate_Active, 2,
                Entry(AtomicEffectType.Heal, value: 2), Entry(AtomicEffectType.SearchDeck));
            var dl3 = EffectEligibility.BranchRewardRejectReason(mixedFx);
            Check(dl3 != null && dl3.Contains("无目标域"),
                $"B5 无Domain原子与带域原子混合仍被拒（实际：{dl3 ?? "null"}）");

            // B6 嵌套分支拒（R4 新增）：奖励=并列整效果单次结算——直挂分支载荷/遗留 kind=1 门步
            // 折算后的分支均拒（深扫含 Then 递归）。
            var nestedFx = Fx((int)TriggerTiming.Activate_Active, 2,
                new AtomicEffectEntry
                {
                    refId = Row(AtomicEffectType.Heal)?.HashId, value = 2,
                    branch = new BranchEntryData
                    {
                        settle = (int)BranchSettleKind.Gate,
                        gateId = "LifeBelowOpp",
                        then = new List<AtomicEffectEntry> { Entry(AtomicEffectType.DrawCard) },
                    },
                });
            var nestedReason = EffectEligibility.BranchRewardRejectReason(nestedFx);
            Check(nestedReason != null && nestedReason.Contains("分支嵌套"),
                $"B6 直挂分支载荷的效果作奖励被拒（实际：{nestedReason ?? "null"}）");

            // kind=1 门步折算形态：Steps=[原子步, 门步(产出条件+then)] → 折算为主干原子 Branch——同拒
            var gateFoldFx = new CardEffectData
            {
                Id = "EFF_ENGINE_RULE_PROBE",
                DisplayName = "引擎奖励规则探针",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                ActivationType = 2,
                SelectionMode = -1,
                TargetCount = -2,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 0, atomic = Entry(AtomicEffectType.Heal, value: 2) },
                    new EffectStepData
                    {
                        kind = 1,
                        conditionId = "DmgKillsTarget",
                        thenSteps = new List<AtomicEffectEntry> { Entry(AtomicEffectType.DrawCard) },
                    },
                },
            };
            var foldReason = EffectEligibility.BranchRewardRejectReason(gateFoldFx);
            Check(foldReason != null && foldReason.Contains("分支嵌套"),
                $"B6 遗留 kind=1 门步折算后的分支同拒（实际：{foldReason ?? "null"}）");

            // B7 超并列拒（R4 新增）：主干原子 >2（两槽口径）不可作奖励
            var tripleFx = Fx((int)TriggerTiming.Activate_Active, 2,
                Entry(AtomicEffectType.Heal, value: 2), Entry(AtomicEffectType.DrawCard), Entry(AtomicEffectType.DealDamage));
            var tripleReason = EffectEligibility.BranchRewardRejectReason(tripleFx);
            Check(tripleReason != null && tripleReason.Contains("并列"),
                $"B7 三原子主干超并列形状被拒（实际：{tripleReason ?? "null"}，期望点名非并列形状）");
        }

        // ======================================== C 发动方式钉 / 预设档 ========================================

        private static void RunPinChecks()
        {
            Log.AppendLine("[Verify·C] ---- 发动方式钉（EngineActivationPinOf / 预设档期望值）----");

            // C1 钉值表：主动行→主动、自动引擎行→自动、非引擎→null
            foreach (var t in ActiveRowTypes)
                Check(ComposerCatalog.EngineActivationPinOf(t) == EffectActivationType.Voluntary,
                    $"C1 {t} → 主动（实际 {ComposerCatalog.EngineActivationPinOf(t)}）");
            foreach (var t in AutomaticRowTypes)
                Check(ComposerCatalog.EngineActivationPinOf(t) == EffectActivationType.Automatic,
                    $"C1 {t} → 自动（实际 {ComposerCatalog.EngineActivationPinOf(t)}）");
            foreach (var t in new[] { AtomicEffectType.DealDamage, AtomicEffectType.Heal, AtomicEffectType.GrantTaunt })
                Check(ComposerCatalog.EngineActivationPinOf(t) == null,
                    $"C1 {t}（非引擎）→ null（实际 {ComposerCatalog.EngineActivationPinOf(t)}）");

            // C2 双选口 EngineActivationFree 已整删（反射证不存在——防刷新工具复活）
            var resurrected = typeof(ComposerCatalog).GetMethod("EngineActivationFree",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Check(resurrected == null, "C2 EngineActivationFree 已随双选面整删（反射无此方法）");

            // C3 预设档：固定三引擎保持原固定时机；运势/拼枪/团结转自然缺省（占位缺省=原时机）；诅咒/祝福保持主动
            var fixedExpect = new[]
            {
                (BranchEngineKind.Countdown, TriggerTiming.OnStandbyPhase),
                (BranchEngineKind.DeathToll, TriggerTiming.OnOtherCreatureDeath),
                (BranchEngineKind.ManaSurplus, TriggerTiming.OnCardCastSettled),
            };
            foreach (var (kind, timing) in fixedExpect)
            {
                var p = ComposerCatalog.EngineHeaderPresetOf(kind);
                Check(p != null && !p.Value.FreeTiming && p.Value.Timing == timing
                          && p.Value.Activation == EffectActivationType.Automatic,
                    $"C3 {kind} 固定档=自动·{timing}（实际 {(p == null ? "null" : $"{p.Value.Activation}·{p.Value.Timing}·FreeTiming={p.Value.FreeTiming}")}）");
            }
            var freeExpect = new[]
            {
                (BranchEngineKind.LuckRoll, TriggerTiming.OnStandbyPhase),
                (BranchEngineKind.Clash, TriggerTiming.OnAttack),
                (BranchEngineKind.Unity, TriggerTiming.OnCardPlayed),
            };
            foreach (var (kind, timing) in freeExpect)
            {
                var p = ComposerCatalog.EngineHeaderPresetOf(kind);
                Check(p != null && p.Value.FreeTiming && p.Value.Timing == timing
                          && p.Value.Activation == EffectActivationType.Automatic,
                    $"C3 {kind} 自然缺省=自动·占位{timing}·FreeTiming（实际 {(p == null ? "null" : $"{p.Value.Activation}·{p.Value.Timing}·FreeTiming={p.Value.FreeTiming}")}）");
            }
            foreach (var kind in PayloadEngines)
            {
                var p = ComposerCatalog.EngineHeaderPresetOf(kind);
                Check(p != null && p.Value.Activation == EffectActivationType.Voluntary
                          && p.Value.Timing == TriggerTiming.Activate_Active && !p.Value.FreeTiming,
                    $"C3 {kind} 保持主动·主动钉档（实际 {(p == null ? "null" : $"{p.Value.Activation}·{p.Value.Timing}")}）");
            }
        }
    }
}
