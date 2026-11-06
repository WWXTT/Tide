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
    /// 身份稳定验证器（2026-11-05 ID 冻结收口配套）——证「修一下不再动一次」落在代码层：
    ///
    /// ① 盐已拆：内存中改原子表行内容（Polarity/TargetFilter）→ 同一张卡重跑 EnsureIdentity，
    ///    原子哈希数组 / 结构哈希 / 组合哈希 全等（旧口径混 TableFingerprint 全局盐，任何表编辑
    ///    都会全体换血——已拆除）。
    /// ② EffectType 下标注册表：Adopt 自身快照幂等（不重排）、当前表全部枚举名有号且 ∈[1,255]
    ///    互异（追加式铁律的装载面）。
    /// ③ manifest v2 往返：legacy「table 指纹」头 = -1 作废；v2（scheme 2 + type 行 + 哈希行）
    ///    载入条数正确、IndexOf 命中、Register 追加后落盘含 scheme 2 头与 type 行。
    ///    探针经反射复位 TideCardIndex 进程态（_map/_manifestPath/_maxIndex），跑完还原——
    ///    不污染同进程后续真实绑定。
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
                RunTypeRegistryProbe(checks);
                RunManifestV2Probe(checks);
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

        // ============================================================ ② 类型注册表 ============================================================

        private static void RunTypeRegistryProbe(List<Check> checks)
        {
            var svc = typeof(CardIdentityService);
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var snapshot = svc.GetMethod("TypeIndexSnapshot", flags)?.Invoke(null, null) as IReadOnlyDictionary<string, int>;
            var adopt = svc.GetMethod("AdoptTypeIndexes", flags);
            var atomTypeIndex = svc.GetMethod("AtomTypeIndex", flags);
            if (snapshot == null || adopt == null || atomTypeIndex == null)
            {
                checks.Add(new Check(false, "CardIdentityService 反射口缺失（TypeIndexSnapshot/AdoptTypeIndexes/AtomTypeIndex）"));
                return;
            }

            int DistinctCount(string name) => (int)atomTypeIndex.Invoke(null, new object[] { name });
            var before = snapshot.ToDictionary(kv => kv.Key, kv => kv.Value);
            adopt.Invoke(null, new object[] { before }); // Adopt 自身快照 → 幂等
            bool idempotent = before.All(kv => DistinctCount(kv.Key) == kv.Value);
            checks.Add(new Check(idempotent, $"Adopt 自身快照幂等（{before.Count} 名零重排）"));

            var allNames = AtomicEffectTable.GetAll()
                .Select(r => r?.EnumName).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            bool allIndexed = allNames.All(n => DistinctCount(n) >= 1);
            var values = allNames.Select(DistinctCount).ToList();
            bool distinct = values.Distinct().Count() == values.Count;
            bool inRange = values.All(v => v >= 1 && v <= 255);
            checks.Add(new Check(allIndexed && distinct && inRange,
                $"当前表 {allNames.Count} 个枚举名全有号、互异且 ∈[1,255]（追加式装载面）"));
            checks.Add(new Check(DistinctCount("") == 0 && DistinctCount(null) == 0,
                "空名记 0（未入表哨兵）"));
        }

        // ============================================================ ③ manifest v2 往返 ============================================================

        private static void RunManifestV2Probe(List<Check> checks)
        {
            var idx = typeof(TideCardIndex);
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var mapField = idx.GetField("_map", flags);
            var pathField = idx.GetField("_manifestPath", flags);
            var maxField = idx.GetField("_maxIndex", flags);
            if (mapField == null || pathField == null || maxField == null)
            {
                checks.Add(new Check(false, "TideCardIndex 反射口缺失（_map/_manifestPath/_maxIndex）"));
                return;
            }

            // _map 是 readonly 字典——不替换实例（readonly 反射赋值不可靠），Clear + 回填还原
            var map = mapField.GetValue(null) as System.Collections.IDictionary;
            var savedEntries = new Dictionary<object, object>();
            if (map != null)
                foreach (System.Collections.DictionaryEntry e in map)
                    savedEntries[e.Key] = e.Value;
            object savedPath = pathField.GetValue(null);
            object savedMax = maxField.GetValue(null);
            try
            {
                ResetIndexState(map, pathField, maxField);

                // legacy「table 指纹」头 = 冻结前混盐清单 → -1 作废
                string legacyPath = Path.Combine(Path.GetTempPath(), "tide_identity_probe_legacy.txt");
                File.WriteAllText(legacyPath, "# probe\ntable 1234567890123456789\n999 1\n");
                ResetIndexState(map, pathField, maxField);
                int legacy = TideCardIndex.ConfigureManifest(legacyPath);
                checks.Add(new Check(legacy == -1, $"legacy table 头清单作废（返回 {legacy} == -1）"));

                // v2：scheme 2 + type 行 + 哈希行 → 载入计数与 IndexOf 命中。
                // type 行用真实枚举名 + 排序种子序号（与装载期播种口径一致——零污染：
                // 即使后续真实清单落盘，也不会带进假名/错号）
                ResetIndexState(map, pathField, maxField);
                var seedNames = AtomicEffectTable.GetAll()
                    .Select(r => r?.EnumName).Where(n => !string.IsNullOrEmpty(n))
                    .Distinct().OrderBy(n => n, StringComparer.Ordinal).Take(2).ToList();
                if (seedNames.Count < 2)
                {
                    checks.Add(new Check(false, "原子表枚举名不足 2 个（探针需要真实表）"));
                    return;
                }
                string v2Path = Path.Combine(Path.GetTempPath(), "tide_identity_probe_v2.txt");
                File.WriteAllText(v2Path,
                    "# probe\nscheme 2\n" +
                    $"type {seedNames[0]} 1\ntype {seedNames[1]} 2\n" +
                    "777 3\n888 4\n");
                int loaded = TideCardIndex.ConfigureManifest(v2Path);
                checks.Add(new Check(loaded == 2, $"v2 清单载入 2 条哈希行（实际 {loaded}）"));
                checks.Add(new Check(TideCardIndex.IndexOf(777UL) == 3 && TideCardIndex.IndexOf(888UL) == 4,
                    "v2 清单哈希→下标命中"));

                // Register 追加 → 落盘为 v2 格式（scheme 2 头 + type 行）
                int added = TideCardIndex.Register(CardCatalog.LoadAll());
                string saved = File.Exists(v2Path) ? File.ReadAllText(v2Path) : "";
                bool schemeWritten = saved.Contains("scheme 2");
                bool typeWritten = saved.Contains("type ");
                checks.Add(new Check(added > 0 && schemeWritten && typeWritten,
                    $"Register 追加 {added} 条并按 v2 落盘（scheme 2 头 + type 行）"));

                // 重绑幂等（进程内一次绑定）
                checks.Add(new Check(TideCardIndex.ConfigureManifest(v2Path) == 0,
                    "重复绑定幂等返回 0"));
            }
            finally
            {
                // 还原进程态（清探针临时文件 + TideCardIndex 回到探针前分配）
                ResetIndexState(map, pathField, maxField);
                if (map != null)
                    foreach (var kv in savedEntries)
                        map[kv.Key] = kv.Value;
                pathField.SetValue(null, savedPath);
                maxField.SetValue(null, savedMax);
                try { File.Delete(Path.Combine(Path.GetTempPath(), "tide_identity_probe_legacy.txt")); } catch { }
                try { File.Delete(Path.Combine(Path.GetTempPath(), "tide_identity_probe_v2.txt")); } catch { }
            }
        }

        private static void ResetIndexState(System.Collections.IDictionary map, FieldInfo pathField, FieldInfo maxField)
        {
            map?.Clear();
            pathField.SetValue(null, null);
            maxField.SetValue(null, 0);
        }

        private static void Section(StringBuilder log, string title)
        {
            log.AppendLine($"---- {title} ----");
            Debug.Log($"[IdentityStability] {title}");
        }
    }
}
#endif
