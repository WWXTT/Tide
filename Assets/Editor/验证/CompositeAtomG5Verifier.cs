// SPDX-FileCopyrightText: 2026 WWXTT
// SPDX-License-Identifier: BUSL-1.1

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 合成器投影验证器（G5 2026-11-08 · 地基计划 §8 验收——数据层证据，不走截图）：
    /// ① 投影派生——价=Σ展开行单价（逐色）、占槽数=展开原子数、描述=词条名+展开体行文案、
    ///    域=展开行域并集（**全部从展开体派生，不从自述读**）；
    /// ② 转换器展开计价——合成器编辑图（@引用步）ConvertAll 后主序列=展开原子，且推导价与
    ///    「直摆同原子」完全相等（派生价可见=同一计价管道）；
    /// ③ 保存/重载不变形——dto 往返后 @ 引用步原样（refId+绑定值保持），重载展开 2 步；
    /// ④ AtomText 投影——【词条·名】参数=N：展开体文案；
    /// ⑤ 库分区数据源可枚举；⑥ 卸包清场。
    /// 菜单：Tools/验证/合成器投影（G5）。
    /// </summary>
    internal static class CompositeAtomG5Verifier
    {
        private const string PackId = "g5probe";
        private const string EmberId = "@g5probe/ember";

        private sealed class Check
        {
            public bool Ok; public string Text;
            public Check(bool ok, string text) { Ok = ok; Text = text; }
        }

        [MenuItem("Tools/验证/合成器投影（G5）")]
        public static void Run()
        {
            var log = new StringBuilder();
            var checks = new List<Check>();
            string tempRoot = null;
            try
            {
                tempRoot = Path.Combine(Path.GetTempPath(), "TideG5Probe_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                RunChecks(tempRoot, checks);
            }
            catch (Exception e)
            {
                checks.Add(new Check(false, "验证器异常中断：" + e.Message + "\n" + e.StackTrace));
            }
            finally
            {
                try
                {
                    ContentPackRegistry.Uninstall(PackId, out _);
                    if (tempRoot != null && Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                    CardCatalog.Invalidate();
                }
                catch { /* 由清场检查兜底 */ }
                checks.Add(new Check(CompositeAtomTable.Get(EmberId) == null
                    && !Directory.Exists(Path.Combine(CardDataPaths.PackDir, PackId)),
                    "清场：词条表空·目录无残留"));
            }

            int fail = checks.Count(c => !c.Ok);
            foreach (var c in checks)
                log.AppendLine(c.Ok ? "[PASS] " + c.Text : "[FAIL] " + c.Text);
            Debug.Log($"==== CompositeAtomG5Verifier {(fail == 0 ? "全过" : $"失败 {fail}/{checks.Count} 项")} ====\n" + log);
        }

        private static string DealRow() => Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId;
        private static string DealDomain() => Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.Domain;
        private static ElementCost DealMana() => Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.ManaList;

        private static void RunChecks(string tempRoot, List<Check> checks)
        {
            // —— 装包（词条 ember：body 两原子·n∈[1,5] 缺省 3）——
            var def = new CompositeAtomDef
            {
                id = EmberId, name = "余烬",
                slot = new CompositeAtomSlot { trunk = 1, domain = DealDomain() },
                @params = new List<CompositeAtomParam>
                {
                    new CompositeAtomParam { key = "n", type = "Value", min = 1, max = 5, defaultValue = 3 },
                },
                body = new List<CompositeAtomBodyAtom>
                {
                    new CompositeAtomBodyAtom { refId = DealRow(), value = "{n}" },
                    new CompositeAtomBodyAtom { refId = DealRow(), value = "2" },
                },
            };
            def.seal = CompositeAtomSeal.Compute(def);
            string dir = Path.Combine(tempRoot, "pack");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "pack.json"),
                JsonUtility.ToJson(new ContentPackManifest { id = PackId, name = "G5", author = "qa", version = "1" }));
            File.WriteAllText(Path.Combine(dir, "atoms.json"),
                JsonUtility.ToJson(new ContentPackAtomsWrapper { items = new List<CompositeAtomDef> { def } }));
            var install = ContentPackRegistry.InstallFrom(dir);
            checks.Add(new Check(install.Ok, install.Ok ? "装包成功" : "装包被拒：" + string.Join("；", install.Errors)));
            if (!install.Ok) return;

            // ① 投影派生（展开体，非自述）
            var row = CompositeAtomProjection.Project(CompositeAtomTable.Get(EmberId));
            var dealMana = DealMana();
            bool costOk = row.Cost != null && dealMana != null;
            if (costOk)
                for (int i = 0; i < ElementCost.Length; i++)
                    if (Math.Abs(row.Cost[(ManaType)i] - dealMana[(ManaType)i] * 2f) > 0.0001f) costOk = false;
            checks.Add(new Check(costOk && row.SlotCount == 2,
                $"派生价=Σ展开行单价×2（逐色）·占槽数={row.SlotCount}（期望 2）——总 {row.Cost?.Total:0.#}（行单价 {dealMana?.Total:0.#}×2）"));
            var dealDesc = Attribute.AtomicEffectTable.GetByHashId(DealRow())?.Description ?? "";
            checks.Add(new Check(row.Description != null && row.Description.Contains("余烬")
                && row.Description.Contains("占2槽") && row.Description.Contains(dealDesc),
                $"描述=词条名+占槽+展开体行文案（自述 slot 不进显示）：{row.Description?.Substring(0, Math.Min(40, row.Description?.Length ?? 0))}…"));
            checks.Add(new Check(row.DomainUnion == DealDomain(),
                $"域=展开行域并集（{row.DomainUnion}==行域）"));

            // ② 转换器展开计价：@步编辑图 ConvertAll → 主序列 2 原子；价 == 直摆同原子
            var viaComposite = CostDerivationService.DeriveElementCosts(CardEffectConverter.ConvertAll(
                new List<CardEffectData> { CompositeFx() }, "PROBE").First(), 0);
            var direct = CostDerivationService.DeriveElementCosts(CardEffectConverter.ConvertAll(
                new List<CardEffectData> { DirectFx() }, "PROBE").First(), 0);
            var defConverted = CardEffectConverter.ConvertAll(new List<CardEffectData> { CompositeFx() }, "PROBE").First();
            bool priceEqual = Math.Abs(viaComposite.Total - direct.Total) < 0.0001f;
            checks.Add(new Check(defConverted.Effects.Count == 2 && priceEqual,
                $"转换器展开：主序列 {defConverted.Effects.Count} 原子（期望 2）·价 词条摆 {viaComposite.Total:0.##} vs 直摆 {direct.Total:0.##}（相等={priceEqual}）"));

            // ③ 保存/重载不变形：dto 往返 → @ 引用步原样；重载（ToCardEffect）展开 2 步
            var dto = CompositeDto();
            var round = JsonUtility.FromJson<EffectSlimDto>(JsonUtility.ToJson(dto));
            var reloaded = EffectSlim.ToCardEffect(round);
            checks.Add(new Check(round.steps != null && round.steps.Count == 1
                && round.steps[0].atom.refId == EmberId && round.steps[0].atom.value == 4
                && reloaded?.Steps != null && reloaded.Steps.Count == 2,
                $"保存/重载不变形：往返后 @引用+绑定值保持（{round.steps?[0]?.atom?.refId}={round.steps?[0]?.atom?.value}）·重载展开 {reloaded?.Steps?.Count} 步（期望 2）"));

            // ④ AtomText 投影（展开体经既有渲染链——{target}/{value} 已替换为实际文案，非原始模板）
            string text = AtomText.RenderAtomEntry(new AtomicEffectEntry { refId = EmberId, value = 4 });
            checks.Add(new Check(text != null && text.StartsWith("【词条·余烬】") && text.Contains("造成4点伤害"),
                $"AtomText 投影：{text?.Substring(0, Math.Min(44, text?.Length ?? 0))}…"));

            // ⑤ 库分区数据源
            checks.Add(new Check(CompositeAtomTable.GetAll().Any(d => d.id == EmberId),
                "库分区数据源可枚举（GetAll 含 ember——RefreshLibrary 词条分区的取数口）"));
        }

        /// <summary>合成器编辑图形态：@ 引用步（value=4 绑定 n）。</summary>
        private static CardEffectData CompositeFx() => new CardEffectData
        {
            Id = "@g5/EFF_VIA", DisplayName = "词条版", TriggerTiming = 19, ActivationType = 2,
            Steps = new List<EffectStepData>
            {
                new EffectStepData { kind = 0, atomic = new AtomicEffectEntry { refId = EmberId, value = 4 } },
            },
        };

        private static CardEffectData DirectFx() => new CardEffectData
        {
            Id = "@g5/EFF_DIRECT", DisplayName = "直摆版", TriggerTiming = 19, ActivationType = 2,
            Steps = new List<EffectStepData>
            {
                new EffectStepData { kind = 0, atomic = new AtomicEffectEntry { refId = DealRow(), value = 4 } },
                new EffectStepData { kind = 0, atomic = new AtomicEffectEntry { refId = DealRow(), value = 2 } },
            },
        };

        private static EffectSlimDto CompositeDto() => new EffectSlimDto
        {
            id = "@g5/EFF_VIA", name = "词条版", timing = 19, activation = 2,
            steps = new List<StepRef>
            {
                new StepRef { kind = 0, atom = new AtomicEffectEntry { refId = EmberId, value = 4 } },
            },
        };
    }
}
#endif
