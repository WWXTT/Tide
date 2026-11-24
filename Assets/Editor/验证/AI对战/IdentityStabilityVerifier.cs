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
    /// ② 底盘原子锚定冻结行（v21 2026-11-24 新增）：生物攻/守资格的合成原子只锚冻结表行 ID——攻击行
    ///    内容内存变更后重推身份全等、攻槽 [10] 非零。① 的样本卡选取同步收紧为「须有可解析的效果行
    ///    引用」：v21 起白板生物也带原子身份，不再能当「有行可改」的样本。
    /// ③④ EffectType 注册表 / manifest v2 往返——**已随 P-1 稳定下标退役（2026-11-19）**：
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
                RunChassisAnchorProbe(log, checks);
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
            // 样本卡须有**效果表行**的原子引用（本探针改的是那一行的表内容）。v21（2026-11-24）起生物的
            // 攻/守资格也占原子身份槽（CardIdentityService 战斗底盘合成效果）——「有原子身份」不再蕴含
            // 「有效果行引用」：白板生物（如 C_895AC28D）会是卡池里第一张带原子身份的卡，选它无行可改。
            CardData victim = null;
            List<string> refIds = null;
            foreach (var c in pool)
            {
                if (!HasAtomIdentity(c)) continue;
                var ids = EffectRefIdsOf(c);
                if (!ids.Any(id => AtomicEffectTable.GetByHashId(id) != null)) continue;
                victim = c;
                refIds = ids;
                break;
            }
            if (victim == null)
            {
                checks.Add(new Check(false, "卡池无带效果行原子引用的卡（Cards.json 空？——探针需要真实卡）"));
                return;
            }

            var row = refIds.Select(AtomicEffectTable.GetByHashId).First(r => r != null);

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

        /// <summary>卡的效果表行原子引用（Steps/扁平两形态全扫；不含 v21 战斗底盘合成原子——那是
        /// 卡面资格、不是表行引用，① 探针改行内容时与它无关）。</summary>
        private static List<string> EffectRefIdsOf(CardData c)
        {
            var sink = new List<string>();
            foreach (var fx in c.Effects ?? new List<CardEffectData>())
                CollectRefIds(fx, sink);
            return sink;
        }

        // ============================================================ ② 底盘原子锚定冻结行（v21 2026-11-24 新增） ============================================================

        /// <summary>① 的同款证明扩到战斗底盘：生物攻/守资格的合成原子只锚冻结表行 ID——攻击行内容
        ///（Polarity/TargetFilter/Domain）内存变更后重推身份，三族哈希全等、攻槽非零、生物原子数组
        /// 恒 12 长。防的是「底盘原子哈希掺进行内容」这种回退（那会让改一条系统行就换血全体生物）。</summary>
        private static void RunChassisAnchorProbe(StringBuilder log, List<Check> checks)
        {
            Section(log, "② 底盘原子锚定冻结行（v21）");

            var pool = CardCatalog.LoadAll();
            var victim = pool.FirstOrDefault(c => c != null && c.Supertype == Cardtype.Creature && c.AttackAbility);
            if (victim == null)
            {
                checks.Add(new Check(false, "卡池无带攻击资格的生物（探针需要真实卡）"));
                return;
            }
            var row = AtomicEffectTable.GetByType(AtomicEffectType.Attack);
            if (row == null)
            {
                checks.Add(new Check(false, "原子表缺 Attack 行——攻/守资格无法入身份"));
                return;
            }

            var before = SnapshotIdentity(victim);
            float oldPolarity = row.Polarity;
            string oldFilter = row.TargetFilter;
            string oldDomain = row.Domain;
            try
            {
                row.Polarity = oldPolarity <= 0f ? 1f : -1f; // 翻转（必变）
                row.TargetFilter = (oldFilter ?? "") + ",Damaged";
                row.Domain = (oldDomain ?? "") + ";己方角色";
                CardIdentityService.EnsureIdentity(victim);
                var after = SnapshotIdentity(victim);
                bool allEqual = before.Atoms.SequenceEqual(after.Atoms)
                                && before.Structure == after.Structure
                                && before.Composition == after.Composition
                                && before.Atoms.Length == CardIdentityService.ChassisGuardSlot + 1
                                && before.Atoms[CardIdentityService.ChassisAttackSlot] != 0UL;
                checks.Add(new Check(allEqual,
                    $"行 {row.HashId}（{row.EnumName}）内容内存变更后生物 {victim.ID} 身份全等、攻槽 [{CardIdentityService.ChassisAttackSlot}] 非零（底盘原子只锚冻结行 ID）"));
            }
            finally
            {
                row.Polarity = oldPolarity; // 还原（表缓存是共享态）
                row.TargetFilter = oldFilter;
                row.Domain = oldDomain;
                CardIdentityService.EnsureIdentity(victim);
            }
        }

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

        // ============================================================ 原②③ 已退役（P-1 2026-11-19；② 号自 v21 起由底盘原子探针占用） ============================================================

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
