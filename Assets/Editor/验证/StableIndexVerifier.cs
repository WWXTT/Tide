using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CardCore;
using CardCore.AI.NeuralEnv;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// P-1 稳定下标验证器（2026-11-19，docs/AI动作空间补全_修改计划.md §0.4.6）：
    /// ① 同 ID → 同 index（纯函数确定性 + IndexOf 与 StableIndex 单源一致）；
    /// ② 范围：0 = hash 0/无；全部 1..pool-1（hash 0 → 0，未登记不再=0）；
    /// ③ C#/Python 算法对拍（内嵌 Python tide_features.stable_index 预计算样本——跨运行时同值
    ///    即「跨进程同号」证据；样本由 Python 侧算出后固化为常量，改算法须双侧同批改）；
    /// ④ C#/Python 常量对账（N_CARD_POOL/N_EFFECT_TYPES/LAYOUT_VERSION 读 tide_features.py）；
    /// ⑤ 碰撞报告（CollisionReport）：全卡池身份/EffectType/效果 Id 三族落桶统计——
    ///    碰撞=共享 embedding 行可接受，阈值内 PASS（§0.4.5：239 身份期望 ~1.7 碰撞）。
    /// 入口：菜单 Tools/验证/稳定下标（P-1）；SpecMasterVerifier F 段聚合（计入总校验汇总）。
    /// 旧 ManifestVerifier/manifest sync 已随 P-1 退役（manifest 仅存档）。
    /// </summary>
    public static class StableIndexVerifier
    {
        // ③ Python 对拍样本（python -c "from tide_features import *; ..." 于 2026-11-19 固化；
        // 改 SplitMix64/FNV/公式须双侧同批重算这组常量）
        private static readonly (string name, int want)[] PyTypeSamples =
        {
            ("DealDamage", 1690), ("Heal", 1933), ("Sacrifice", 565), ("Dormancy", 3326), ("DrawCard", 1715),
        };
        private static readonly (ulong hash, int want)[] PyCardSamples =
        {
            (1UL, 8880), (42UL, 1841), (0x123456789ABCDEFUL, 7162), (0xDEADBEEFUL, 6131), (0xC0FFEEUL, 9047),
        };

        [MenuItem("Tools/验证/稳定下标（P-1）")]
        public static void RunFromMenu() => Debug.Log(Run(out _, out _));

        /// <summary>跑全部断言，返回报告文本（末行汇总）。fails/total 供 SpecMaster F 段聚合。</summary>
        public static string Run(out int fails, out int total)
        {
            fails = 0; total = 0;
            var log = new StringBuilder();
            var checks = new List<(bool pass, string text)>();
            try
            {
                CheckDeterminismAndRange(checks);
                CheckPythonParity(checks);
                CheckPythonConstants(checks);
                CheckCollisions(log, checks);
            }
            catch (Exception ex)
            {
                checks.Add((false, $"异常中断：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"));
            }
            total = checks.Count;
            fails = checks.Count(c => !c.pass);
            foreach (var c in checks)
                log.AppendLine(c.pass ? "[PASS] " + c.text : "[FAIL] " + c.text);
            log.AppendLine(fails == 0
                ? $"==== StableIndexVerifier 全过（{total} 项）===="
                : $"==== StableIndexVerifier 失败 {fails}/{total} 项 ====");
            return log.ToString();
        }

        /// <summary>① 同 ID 同 index + ② 范围/零语义。</summary>
        private static void CheckDeterminismAndRange(List<(bool, string)> checks)
        {
            bool same = true;
            for (int round = 0; round < 3 && same; round++)
                foreach (var (hash, want) in PyCardSamples)
                    same &= TideCardIndex.IndexOf(hash) == want;
            checks.Add((same, "同 ID 三轮取值一致（纯函数确定性——跨进程同号的机制保证）"));

            bool zero = TideCardIndex.IndexOf(0UL) == 0 && CardIdentityService.StableIndex(0, TideCardIndex.Capacity) == 0;
            checks.Add((zero, "hash 0 → 0（无；「未登记=0」旧语义已废弃）"));

            bool src = PyCardSamples.All(s => TideCardIndex.IndexOf(s.hash) == CardIdentityService.StableIndex(s.hash, TideCardIndex.Capacity))
                       && PyTypeSamples.All(s => CardIdentityService.AtomTypeIndex(s.name) == CardIdentityService.StableIndex(CardIdentityService.Fnv1a64(s.name), CardIdentityService.MaxTypeIndex + 1));
            checks.Add((src, "IndexOf/AtomTypeIndex 与 StableIndex 单源一致"));

            var rng = new System.Random(20261119);
            bool range = true;
            for (int i = 0; i < 1000 && range; i++)
            {
                ulong h = ((ulong)rng.Next() << 32) | (uint)rng.Next();
                if (h == 0) continue;
                int idx = TideCardIndex.IndexOf(h);
                range &= idx >= 1 && idx < TideCardIndex.Capacity;
                int t = CardIdentityService.AtomTypeIndex("探针#" + i);
                range &= t >= 1 && t <= CardIdentityService.MaxTypeIndex;
            }
            checks.Add((range, $"随机 1000 hash 范围恒 [1,{TideCardIndex.Capacity - 1}] / type 恒 [1,{CardIdentityService.MaxTypeIndex}]"));
        }

        /// <summary>③ C#/Python 算法对拍（内嵌 Python 预计算样本）。</summary>
        private static void CheckPythonParity(List<(bool, string)> checks)
        {
            bool typeOk = PyTypeSamples.All(s => CardIdentityService.AtomTypeIndex(s.name) == s.want);
            checks.Add((typeOk, "EffectType 派生 = Python stable_index(fnv1a64(name)) 逐值一致（"
                + string.Join(", ", PyTypeSamples.Select(s => $"{s.name}={s.want}").ToArray()) + "）"));
            bool cardOk = PyCardSamples.All(s => TideCardIndex.IndexOf(s.hash) == s.want);
            checks.Add((cardOk, "卡身份派生 = Python stable_index(hash) 逐值一致（跨运行时同号=跨进程同号证据）"));
        }

        /// <summary>④ C#/Python 常量对账（读 tide_features.py 文本）。</summary>
        private static void CheckPythonConstants(List<(bool, string)> checks)
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", "tide_rl", "tide_features.py");
            if (!System.IO.File.Exists(path))
            {
                checks.Add((false, "tide_features.py 不存在（训练副本未同步？）"));
                return;
            }
            var text = System.IO.File.ReadAllText(path);
            int pyCard = IntOf(text, "N_CARD_POOL"), pyType = IntOf(text, "N_EFFECT_TYPES"), pyLayout = IntOf(text, "LAYOUT_VERSION");
            bool ok = pyCard == TideCardIndex.Capacity
                      && pyType == CardIdentityService.MaxTypeIndex + 1
                      && pyLayout == TideObservation.LayoutVersion;
            checks.Add((ok, $"常量对账 C# cardPool {TideCardIndex.Capacity}/typePool {CardIdentityService.MaxTypeIndex + 1}"
                + $"/layout v{TideObservation.LayoutVersion} vs Python {pyCard}/{pyType}/v{pyLayout}"));
        }

        /// <summary>⑤ 碰撞报告：全池身份（原子/结构/组合哈希）、EffectType 名、效果 Id 三族落桶。
        /// 预算（§0.4.5「总数在阈值内」的统计化口径）：碰撞总数 ≤ 生日碰撞期望 n²/2N + 3σ + 2
        ///（Poisson 容差）；最大组：大池（≥4096）≤2、效果 Id 1024 池 ≤3（λ≈0.17/桶下三撞
        /// 概率不可忽略）；超出=高频冲突报 FAIL。碰撞=两 ID 共享行/特征值（可接受，非缺陷）。</summary>
        private static void CheckCollisions(StringBuilder log, List<(bool, string)> checks)
        {
            var cards = CardCatalog.LoadAll().Where(c => c != null).ToList();
            foreach (var c in cards) CardIdentityService.EnsureIdentity(c);

            var cardHashes = new HashSet<ulong>();
            foreach (var c in cards)
            {
                if (c.AtomContentHashes != null)
                    foreach (var h in c.AtomContentHashes)
                        if (h != 0UL) cardHashes.Add(h);
                if (c.StructureContentHash != 0UL) cardHashes.Add(c.StructureContentHash);
                if (c.CompositionContentHash != 0UL) cardHashes.Add(c.CompositionContentHash);
            }
            var typeNames = new HashSet<string>(Attribute.AtomicEffectTable.GetAll()
                .Select(cfg => cfg.EnumName).Where(n => !string.IsNullOrEmpty(n)));
            var effectIds = new HashSet<string>();
            foreach (var c in cards)
            {
                if (c.Effects == null) continue;
                foreach (var def in CardEffectConverter.ConvertAll(c.Effects, c.ID))
                    if (def != null && !string.IsNullOrEmpty(def.Id)) effectIds.Add(def.Id);
            }

            ReportBucket(log, checks, "卡身份(16384)", TideCardIndex.Capacity,
                cardHashes.Select(h => (key: h.ToString(), idx: TideCardIndex.IndexOf(h))), cardHashes.Count);
            ReportBucket(log, checks, "EffectType(4096)", CardIdentityService.MaxTypeIndex + 1,
                typeNames.Select(n => (key: n, idx: CardIdentityService.AtomTypeIndex(n))), typeNames.Count);
            ReportBucket(log, checks, "效果Id(1024)", TideCardIndex.EffectIdentityPool,
                effectIds.Select(id => (key: id, idx: TideCardIndex.IndexOfEffectDefinition(new EffectDefinition { Id = id }))), effectIds.Count);
        }

        private static void ReportBucket(StringBuilder log, List<(bool, string)> checks, string tag, int pool,
            IEnumerable<(string key, int idx)> entries, int count)
        {
            double expected = (double)count * count / (2.0 * pool); // 生日碰撞期望 n²/2N
            int collisionBudget = (int)Math.Ceiling(expected + 3.0 * Math.Sqrt(expected) + 2.0);
            int maxGroupBudget = pool >= 4096 ? 2 : 3;
            var groups = entries.GroupBy(e => e.idx).ToList();
            int collisions = count - groups.Count;
            int maxGroup = groups.Max(g => g.Count());
            var hot = groups.Where(g => g.Count() > 1)
                .Select(g => g.Key + " ← " + string.Join("|", g.Select(e => e.key).Take(6)))
                .ToList();
            log.AppendLine($"[碰撞] {tag}: 身份 {count} / 桶 {groups.Count} / 碰撞 {collisions}（期望 {expected:F1}，预算 ≤{collisionBudget}）/ 最大组 {maxGroup}");
            foreach (var h in hot.Take(8)) log.AppendLine("  " + h);
            bool ok = collisions <= collisionBudget && maxGroup <= maxGroupBudget;
            checks.Add((ok, $"{tag} 碰撞报告：{count} 身份碰撞 {collisions}（期望 {expected:F1}、预算 ≤{collisionBudget}）、最大组 {maxGroup}（≤{maxGroupBudget}）"
                + (hot.Count > 0 ? "；热桶见验证器日志" : "；无碰撞")));
        }

        private static int IntOf(string text, string constName)
        {
            var m = Regex.Match(text, @"(?m)^\s*" + constName + @"\s*=\s*(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }
    }
}
