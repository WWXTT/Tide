#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CardCore;
using CardCore.AI.NeuralEnv;
using CardCore.Attribute;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 身份稳定验证器（2026-11-05 ID 冻结收口配套；P-1 2026-11-19 口径更新）——证「修一下不再动一次」落在代码层：
    ///
    /// ① 盐已拆（保留）：内存中改原子表行内容（Polarity/TargetFilter）→ 同一张卡重跑 EnsureIdentity，
    ///    原子哈希数组 / 结构哈希 / 组合哈希 全等（旧口径混 TableFingerprint 全局盐，任何表编辑
    ///    都会全体换血——已拆除）。身份哈希口径与 P-1 无关（P-1 只换哈希→下标的派生方式），继续有效。
    /// ②③ EffectType 注册表 / manifest v2 往返——**已随 P-1 稳定下标退役（2026-11-19）**：
    ///    追加式注册表（AdoptTypeIndexes/TypeIndexSnapshot）与 TideCardIndex 登记簿
    ///    （_map/_manifestPath/_maxIndex）已拆除，manifest 仅存档；对应断言移交给
    ///    StableIndexVerifier（同值/范围/Python 对拍/碰撞报告，菜单 Tools/验证/稳定下标（P-1），
    ///    并入规范总校验 F 段）。
    ///
    /// 入口：菜单 验证/身份稳定（ID冻结收口）；Run() 返回报告文本（全过=PASS 结尾）。
    /// </summary>
    public static class IdentityStabilityVerifier
    {
        private readonly struct Check
        {
            public readonly bool Pass;
            public readonly string Text;
            public Check(bool pass, string text) { Pass = pass; Text = text; }
        }

        [MenuItem("Tools/验证/身份稳定（ID冻结收口）")]
        public static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var log = new StringBuilder();
            var checks = new List<Check>();
            try
            {
                RunSaltRemovedProbe(log, checks);
            }
            catch (Exception ex)
            {
                checks.Add(new Check(false, $"异常中断：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"));
            }
            int fails = checks.Count(c => !c.Pass);
            log.AppendLine(fails == 0
                ? $"==== IdentityStabilityVerifier 全过（{checks.Count} 项断言）===="
                : $"==== IdentityStabilityVerifier 失败 {fails}/{checks.Count} 项 ====");
            foreach (var c in checks.Where(c => !c.Pass))
                log.AppendLine("[FAIL] " + c.Text);
            return log.ToString();
        }

        // ============================================================ ① 盐已拆 ============================================================

        private static void RunSaltRemovedProbe(StringBuilder log, List<Check> checks)
        {
            Section(log, "① 表内容编辑不再换血（盐已拆）");

            var pool = CardCatalog.LoadAll();
            var victim = pool.FirstOrDefault(HasAtomIdentity);
            if (victim == null)
            {
                checks.Add(new Check(false, "卡池无带原子身份的卡（Cards.json 空？——探针需要真实卡）"));
                return;
            }

            var refIds = new List<string>();
            foreach (var fx in victim.Effects ?? new List<CardEffectData>())
                CollectRefIds(fx, refIds);
            var row = refIds.Select(AtomicEffectTable.GetByHashId).FirstOrDefault(r => r != null);
            if (row == null)
            {
                checks.Add(new Check(false, $"样本卡 {victim.ID} 无可变更的原子表行引用"));
                return;
            }

            var before = SnapshotIdentity(victim);
            float oldPolarity = row.Polarity;
            string oldFilter = row.TargetFilter;
            try
            {
                row.Polarity = oldPolarity <= 0f ? 1f : -1f; // 翻转（必变）
                row.TargetFilter = (oldFilter ?? "") + ",Damaged";
                CardIdentityService.EnsureIdentity(victim);
                var after = SnapshotIdentity(victim);
                bool allEqual = before.Atoms.SequenceEqual(after.Atoms)
                                && before.Structure == after.Structure
                                && before.Composition == after.Composition;
                checks.Add(new Check(allEqual,
                    $"行 {row.HashId}（{row.EnumName}）内容内存变更后三族哈希全等（盐已拆——旧口径此步全体换血）"));
            }
            finally
            {
                row.Polarity = oldPolarity; // 还原（表缓存是共享态）
                row.TargetFilter = oldFilter;
                CardIdentityService.EnsureIdentity(victim);
            }
        }

        private static bool HasAtomIdentity(CardData c)
            => c != null && c.AtomContentHashes != null && c.AtomContentHashes.Length > 0;

        private static (ulong[] Atoms, ulong Structure, ulong Composition) SnapshotIdentity(CardData card)
        {
            CardIdentityService.EnsureIdentity(card);
            return ((ulong[])card.AtomContentHashes.Clone(), card.StructureContentHash, card.CompositionContentHash);
        }

        private static void CollectRefIds(CardEffectData fx, List<string> sink)
        {
            if (fx == null) return;
            if (fx.Steps != null && fx.Steps.Count > 0)
                foreach (var s in fx.Steps) CollectStepRefIds(s, sink);
            else if (fx.AtomicEffects != null)
                foreach (var a in fx.AtomicEffects)
                    if (a != null && !string.IsNullOrEmpty(a.refId)) sink.Add(a.refId);
        }

        private static void CollectStepRefIds(EffectStepData s, List<string> sink)
        {
            if (s == null) return;
            switch (s.kind)
            {
                case 0:
                    if (s.atomic != null && !string.IsNullOrEmpty(s.atomic.refId)) sink.Add(s.atomic.refId);
                    break;
                case 1:
                    foreach (var a in s.thenSteps ?? new List<AtomicEffectEntry>())
                        if (a != null && !string.IsNullOrEmpty(a.refId)) sink.Add(a.refId);
                    foreach (var a in s.elseSteps ?? new List<AtomicEffectEntry>())
                        if (a != null && !string.IsNullOrEmpty(a.refId)) sink.Add(a.refId);
                    break;
                case 2:
                    foreach (var mode in s.choices ?? new List<EffectChoiceData>())
                        if (mode?.steps != null)
                            foreach (var ms in mode.steps) CollectStepRefIds(ms, sink);
                    break;
            }
        }

        // ============================================================ ②③ 已退役（P-1 2026-11-19） ============================================================

        // ② EffectType 注册表探针（Adopt 幂等/全名有号 ∈[1,255] 互异）与 ③ manifest v2 往返探针
        // 已随 P-1 稳定下标退役：注册表/登记簿/manifest 读写全部拆除（card_identity_manifest.json
        // 仅存档）。替代验收 = StableIndexVerifier（Tools/验证/稳定下标（P-1），并入规范总校验 F 段：
        // 同 ID 同值 / 范围 / C#-Python 算法对拍与常量对账 / 碰撞报告）。

        private static void Section(StringBuilder log, string title)
        {
            log.AppendLine($"---- {title} ----");
            Debug.Log($"[IdentityStability] {title}");
        }
    }
}
#endif
