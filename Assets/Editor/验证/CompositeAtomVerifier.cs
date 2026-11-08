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
    /// 词条（虚拟原子）展开验证器（G3 2026-11-08 · docs/地基计划_虚拟原子与数值A.md §6 验收）：
    /// ① 词条 seal 语义——改 name 不变 / 改 body 变（CompositeAtomSeal 纯哈希层）；
    /// ② 装包（atoms.json 词条 + 引用词条的效果 + 卡）→ 装入成功，词条表可查；
    /// ③ 展开先于校验/计价——装载后的卡效果 Steps 已是展开形态（2 步·绑定 value/str·字面量）；
    /// ④ **价格守恒（G3 主验收）**：同一效果「用词条摆」与「直摆同原子」DeriveElementCosts 完全相等；
    /// ⑤ 握手闭包含词条封印行（PK:包id|词条id|seal）；
    /// ⑥⑦ 拒装——body 引用词条（深度恒 1）/ 词条封印不符，Errors 点名且不落目录；
    /// ⑧ 卸包清场（表/目录/卡组面复原）。
    /// 菜单：Tools/验证/词条展开（G3）。
    /// </summary>
    internal static class CompositeAtomVerifier
    {
        private const string PackId = "probepack";
        private const string AtomId = "@probepack/ember";
        private const string OtherAtomId = "@probepack/other";
        private const string EffectId = "@probepack/EFF_ENTRY";
        private const string CardId = "@probepack/C_ENTRY";

        private sealed class Check
        {
            public bool Ok; public string Text;
            public Check(bool ok, string text) { Ok = ok; Text = text; }
        }

        [MenuItem("Tools/验证/词条展开（G3）")]
        public static void Run()
        {
            var log = new StringBuilder();
            var checks = new List<Check>();
            string tempRoot = null;
            try
            {
                tempRoot = Path.Combine(Path.GetTempPath(), "TideAtomProbe_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                RunSealSemantics(checks);
                RunExpandAndPrice(tempRoot, checks);
                RunRejections(tempRoot, checks);
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
                catch { /* 清场失败由下方检查兜底 */ }
                checks.Add(new Check(CompositeAtomTable.Get(AtomId) == null
                    && CardCatalog.GetById(CardId) == null
                    && !Directory.Exists(Path.Combine(CardDataPaths.PackDir, PackId)),
                    "清场：词条表空·包卡 miss·目录无残留"));
            }

            int fail = checks.Count(c => !c.Ok);
            foreach (var c in checks)
                log.AppendLine(c.Ok ? "[PASS] " + c.Text : "[FAIL] " + c.Text);
            Debug.Log($"==== CompositeAtomVerifier {(fail == 0 ? "全过" : $"失败 {fail}/{checks.Count} 项")} ====\n" + log);
        }

        private static string DealRow() => Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId;
        /// <summary>声明域照抄 body 行域（G4 域一致：子原子行域 ⊆ 词条声明域——夹具须合规）。</summary>
        private static string DealDomain() => Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.Domain;

        private static CompositeAtomDef EmberDef(string name, string bodyValue0)
        {
            return new CompositeAtomDef
            {
                id = AtomId,
                name = name,
                slot = new CompositeAtomSlot { trunk = 1, domain = DealDomain() },
                @params = new List<CompositeAtomParam>
                {
                    new CompositeAtomParam { key = "n", type = "Value", min = 1, max = 5, defaultValue = 2 },
                    new CompositeAtomParam { key = "s", type = "Str", defaultStr = "X" },
                },
                body = new List<CompositeAtomBodyAtom>
                {
                    new CompositeAtomBodyAtom { refId = DealRow(), value = bodyValue0, str = "{s}" },
                    new CompositeAtomBodyAtom { refId = DealRow(), value = "2" },
                },
            };
        }

        private static EffectSlimDto EntryDto() => new EffectSlimDto
        {
            id = EffectId, name = "探针·词条入口", timing = 19, activation = 2,
            steps = new List<StepRef>
            {
                new StepRef { kind = 0, atom = new AtomicEffectEntry { refId = AtomId, value = 3, str = "Foo" } },
            },
        };

        /// <summary>期望封印=候选上下文下按展开后行为计算（与装包校验 ⑥ 同口径）。</summary>
        private static string ExpectedEffectSeal(EffectSlimDto dto, List<CompositeAtomDef> defs)
        {
            CompositeAtomTable.PushContext(defs);
            try { return ContentPackRegistry.ComputeEffectSeal(dto); }
            finally { CompositeAtomTable.PopContext(); }
        }

        private static string BuildPack(string dir, List<CompositeAtomDef> defs, bool withEntry)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "pack.json"),
                JsonUtility.ToJson(new ContentPackManifest { id = PackId, name = "词条探针包", author = "qa", version = "1" }));
            foreach (var def in defs)
                if (string.IsNullOrEmpty(def.seal)) def.seal = CompositeAtomSeal.Compute(def);
            File.WriteAllText(Path.Combine(dir, "atoms.json"),
                JsonUtility.ToJson(new ContentPackAtomsWrapper { items = defs }));
            if (withEntry)
            {
                var dto = EntryDto();
                File.WriteAllText(Path.Combine(dir, "effects.json"),
                    JsonUtility.ToJson(new ContentPackEffectsWrapper { items = new List<EffectSlimDto> { dto } }));
                File.WriteAllText(Path.Combine(dir, "seal.json"), JsonUtility.ToJson(new ContentPackSealFile
                {
                    items = new List<ContentPackSealEntry>
                    {
                        new ContentPackSealEntry { id = EffectId, seals = new List<string> { ExpectedEffectSeal(dto, defs) } },
                    },
                }));
                File.WriteAllText(Path.Combine(dir, "cards.json"), JsonUtility.ToJson(new TestCardsConfigWrapper
                {
                    cards = new List<CardConfigEntry>
                    {
                        new CardConfigEntry
                        {
                            id = CardId, cardName = "探针·词条卡", supertype = "Spell",
                            costList = new List<float> { 0, 2, 0, 0, 0 },
                            effectIds = new List<string> { EffectId },
                        },
                    },
                }));
            }
            return dir;
        }

        // ======================================== ① seal 语义 ========================================

        private static void RunSealSemantics(List<Check> checks)
        {
            string row = DealRow();
            if (string.IsNullOrEmpty(row)) { checks.Add(new Check(false, "原子表无 DealDamage 行（夹具前提缺失）")); return; }
            string sA = CompositeAtomSeal.Compute(EmberDef("余烬", "{n}"));
            string sB = CompositeAtomSeal.Compute(EmberDef("改名不改行为", "{n}"));
            string sBody = CompositeAtomSeal.Compute(EmberDef("余烬", "2"));
            checks.Add(new Check(sA == sB && sA != sBody,
                $"词条 seal：改名不变（{sA}=={sB}）·改 body 变（{sA}!={sBody}）——seal 盖 body/params/slot 不盖 name"));
        }

        // ======================================== ②③④⑤ 装包 → 展开 → 价格守恒 → 闭包 ========================================

        private static void RunExpandAndPrice(string tempRoot, List<Check> checks)
        {
            var defs = new List<CompositeAtomDef> { EmberDef("余烬", "{n}") };
            var good = BuildPack(Path.Combine(tempRoot, "good"), defs, true);
            var report = ContentPackRegistry.InstallFrom(good);
            checks.Add(new Check(report.Ok,
                report.Ok ? $"装包成功（词条 {defs.Count}·效果 {report.EffectCount}·卡 {report.CardCount}）"
                          : "装包被拒：" + string.Join("；", report.Errors)));
            if (!report.Ok) return;

            checks.Add(new Check(CompositeAtomTable.Get(AtomId) != null, "词条表可查（CompositeAtomTable.Get 命中）"));

            // 展开先于校验/计价：装载后的卡效果 Steps 已是展开形态（2 步·绑定/字面量/str 绑定）
            var card = CardCatalog.GetById(CardId);
            var fx = card?.Effects?.FirstOrDefault();
            var steps = fx?.Steps;
            bool expanded = steps != null && steps.Count == 2
                && steps[0]?.atomic?.refId == DealRow() && steps[0].atomic.value == 3 && steps[0].atomic.str == "Foo"
                && steps[1]?.atomic?.refId == DealRow() && steps[1].atomic.value == 2;
            checks.Add(new Check(expanded,
                $"装载即展开：Steps={steps?.Count ?? -1}（期望 2）·步1 value={steps?[0]?.atomic?.value}/str='{steps?[0]?.atomic?.str}'（绑定 n←3·s←Foo）·步2 value={steps?[1]?.atomic?.value}（字面量 2）"));

            // 价格守恒（G3 主验收）：词条摆 == 直摆同原子 → DeriveElementCosts 完全相等
            var viaComposite = CostDerivationService.DeriveElementCosts(
                CardEffectConverter.ConvertAll(new List<CardEffectData> { EffectSlim.ToCardEffect(EntryDto()) }, "PROBE").First(), 0);
            var direct = CostDerivationService.DeriveElementCosts(
                CardEffectConverter.ConvertAll(new List<CardEffectData> { EffectSlim.ToCardEffect(DirectDto()) }, "PROBE").First(), 0);
            bool priceEqual = Math.Abs(viaComposite.Total - direct.Total) < 0.0001f;
            foreach (ManaType c in Enum.GetValues(typeof(ManaType)))
                if (Math.Abs(viaComposite[c] - direct[c]) > 0.0001f) priceEqual = false;
            checks.Add(new Check(priceEqual,
                $"价格守恒：词条摆 总{viaComposite.Total:0.##} vs 直摆 总{direct.Total:0.##}（逐色相等={priceEqual}·红 {viaComposite[ManaType.Red]:0.#}/{direct[ManaType.Red]:0.#}）"));

            // 握手闭包含词条封印行
            var lines = ContentPackRegistry.ReferencedSealLines(new[] { EffectId });
            bool hasAtomLine = lines.Any(l => l.Contains("|" + AtomId + "|"));
            checks.Add(new Check(lines.Count == 2 && hasAtomLine,
                $"闭包含词条封印：{lines.Count} 行（效果 PK + 词条 PK）——{string.Join(" · ", lines)}"));

            // 卸包 → 词条表/卡组面复原（整卡断链通道复用 G2 验证，此处只证词条面复原）
            ContentPackRegistry.Uninstall(PackId, out _);
            checks.Add(new Check(CompositeAtomTable.Get(AtomId) == null && CardCatalog.GetById(CardId) == null,
                "卸包：词条表 miss·包卡 miss（词条随包消失——引用它的效果在重装前不再可展开）"));
        }

        /// <summary>直摆版效果（与词条展开体逐原子相同）——价格守恒对照。</summary>
        private static EffectSlimDto DirectDto() => new EffectSlimDto
        {
            id = "@probe/DIRECT", name = "直摆对照", timing = 19, activation = 2,
            steps = new List<StepRef>
            {
                new StepRef { kind = 0, atom = new AtomicEffectEntry { refId = DealRow(), value = 3, str = "Foo" } },
                new StepRef { kind = 0, atom = new AtomicEffectEntry { refId = DealRow(), value = 2 } },
            },
        };

        // ======================================== ⑥⑦ 拒装 ========================================

        private static void RunRejections(string tempRoot, List<Check> checks)
        {
            // ㊀ 深度恒 1：body 引用同包另一词条
            var nested = EmberDef("嵌套", "{n}");
            nested.body[0].refId = OtherAtomId;
            var other = new CompositeAtomDef
            {
                id = OtherAtomId, name = "被嵌套",
                slot = new CompositeAtomSlot { trunk = 1, domain = "对方战场:生物" },
                body = new List<CompositeAtomBodyAtom> { new CompositeAtomBodyAtom { refId = DealRow(), value = "1" } },
            };
            var defs = new List<CompositeAtomDef> { nested, other };
            foreach (var d in defs) d.seal = CompositeAtomSeal.Compute(d);
            var r0 = ContentPackRegistry.InstallFrom(BuildPack(Path.Combine(tempRoot, "nested"), defs, false));
            string m0 = r0.Errors.FirstOrDefault(e => e.Contains("深度恒 1")) ?? (r0.Errors.Count > 0 ? r0.Errors[0] : "（无错误信息）");
            checks.Add(new Check(!r0.Ok && r0.Errors.Any(e => e.Contains("深度恒 1"))
                && CompositeAtomTable.Get(AtomId) == null,
                "拒装·深度恒1：" + m0));

            // ㊁ 词条封印不符（seal 声明错值）
            var tampered = EmberDef("余烬", "{n}");
            tampered.seal = "00000000";
            var r1 = ContentPackRegistry.InstallFrom(
                BuildPack(Path.Combine(tempRoot, "tampered"), new List<CompositeAtomDef> { tampered }, false));
            string m1 = r1.Errors.Count > 0 ? r1.Errors[0] : "（无错误信息）";
            checks.Add(new Check(!r1.Ok && r1.Errors.Any(e => e.Contains("词条") && e.Contains("封印不符"))
                && CompositeAtomTable.Get(AtomId) == null,
                "拒装·词条封印不符：" + m1));
        }
    }
}
#endif
