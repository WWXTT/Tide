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
    /// B EngineRewardRejectReason 正反例：先吃 BranchRewardRejectReason 通用闸
    ///   （引擎主干/光环形态/空效果），再按奖励档分档——主动奖励引擎只收主动效果（Activate_*），
    ///   自动奖励引擎只收自动效果（真实触发时机 ∧ Automatic）；载荷引擎/None 不经发动档闸。
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

        // ======================================== B EngineRewardRejectReason 正反例 ========================================

        private static void RunRejectReasonChecks()
        {
            Log.AppendLine("[Verify·B] ---- 引擎奖励发动档资格（正反例）----");

            // 夹具：主动效果（Activate_*）/ 自动效果（OnDraw=真实触发事件锚）/ 声明档与时机错配
            var activeFx = Fx((int)TriggerTiming.Activate_Active, 2, Entry(AtomicEffectType.Heal, value: 2));
            var autoFx = Fx((int)TriggerTiming.OnDraw, 1, Entry(AtomicEffectType.Heal, value: 2));
            var declaredAutoButActivateTiming = Fx((int)TriggerTiming.Activate_Active, 1, Entry(AtomicEffectType.Heal, value: 2));
            var declaredVoluntaryButTriggered = Fx((int)TriggerTiming.OnDraw, 2, Entry(AtomicEffectType.Heal, value: 2));

            // B1 主动奖励引擎（倒计时/死亡计数/元素充盈）：只收主动效果
            foreach (var engine in ActiveRewardEngines)
            {
                var ok = EffectEligibility.EngineRewardRejectReason(activeFx, engine);
                Check(ok == null, $"B1 {engine} 挂主动效果（Activate_*）通过（实际：{ok ?? "无"}）");
                var bad = EffectEligibility.EngineRewardRejectReason(autoFx, engine);
                Check(bad != null && bad.Contains("主动"),
                    $"B1 {engine} 挂自动效果被拒（实际：{bad ?? "null"}，期望点名只能挂主动）");
            }

            // B2 自动奖励引擎（运势/拼枪/团结）：只收自动效果且须真实触发时机
            foreach (var engine in AutomaticRewardEngines)
            {
                var ok = EffectEligibility.EngineRewardRejectReason(autoFx, engine);
                Check(ok == null, $"B2 {engine} 挂自动效果（OnDraw 真实事件锚）通过（实际：{ok ?? "无"}）");
                var bad = EffectEligibility.EngineRewardRejectReason(activeFx, engine);
                Check(bad != null && bad.Contains("自动"),
                    $"B2 {engine} 挂主动效果被拒（实际：{bad ?? "null"}，期望点名只能挂自动）");
                // 声明自动但时机=主动三档：非触发式 → 无真实触发时机
                var noTiming = EffectEligibility.EngineRewardRejectReason(declaredAutoButActivateTiming, engine);
                Check(noTiming != null && noTiming.Contains("自动"),
                    $"B2 {engine} 声明自动但时机=主动档被拒（实际：{noTiming ?? "null"}）");
                // 声明主动但时机=触发式：ActivationType != Automatic
                var wrongDecl = EffectEligibility.EngineRewardRejectReason(declaredVoluntaryButTriggered, engine);
                Check(wrongDecl != null && wrongDecl.Contains("自动"),
                    $"B2 {engine} 时机真实但声明主动被拒（实际：{wrongDecl ?? "null"}）");
            }

            // B3 通用闸透传（先复用 BranchRewardRejectReason）：引擎主干/光环形态/空效果
            var trunkFx = Fx((int)TriggerTiming.Activate_Active, 2,
                new AtomicEffectEntry
                {
                    refId = Row(AtomicEffectType.EngineCountdown)?.HashId,
                    branch = new BranchEntryData { settle = (int)BranchSettleKind.Engine, engine = (int)BranchEngineKind.Countdown },
                });
            var trunkReason = EffectEligibility.EngineRewardRejectReason(trunkFx, BranchEngineKind.DeathToll);
            Check(trunkReason != null && trunkReason.Contains("引擎主干"),
                $"B3 引擎主干作奖励被拒（实际：{trunkReason ?? "null"}，期望点名引擎主干）");

            var auraFx = Fx((int)TriggerTiming.Activate_Active, 2, Entry(AtomicEffectType.Heal, value: 2));
            auraFx.ArrowDirections = 1;
            var auraReason = EffectEligibility.EngineRewardRejectReason(auraFx, BranchEngineKind.Countdown);
            Check(auraReason != null && auraReason.Contains("光环形态"),
                $"B3 光环形态效果作奖励被拒（实际：{auraReason ?? "null"}）");

            var emptyFx = Fx((int)TriggerTiming.Activate_Active, 2);
            var emptyReason = EffectEligibility.EngineRewardRejectReason(emptyFx, BranchEngineKind.LuckRoll);
            Check(emptyReason != null && emptyReason.Contains("空效果"),
                $"B3 空效果（无原子）被拒（实际：{emptyReason ?? "null"}）");

            // B4 载荷引擎与 None：不经发动档闸——主动/自动两形态都过通用闸即放行
            foreach (var engine in PayloadEngines.Concat(new[] { BranchEngineKind.None }))
            {
                var a = EffectEligibility.EngineRewardRejectReason(activeFx, engine);
                var b = EffectEligibility.EngineRewardRejectReason(autoFx, engine);
                Check(a == null && b == null,
                    $"B4 {engine} 不经发动档闸（主动形态：{a ?? "过"} / 自动形态：{b ?? "过"}，期望双双放行）");
            }
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
