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
    /// 展开后校验验证器（G4 2026-11-08 · docs/地基计划_虚拟原子与数值A.md §7 验收——六条逐条可触发、
    /// 错误点名到卡/效果/步/原子/约束）：
    /// ① 位预算（直摆1+词条body2=3&gt;2 拒）② 域一致（行域⊄声明域 拒）③ 深度（body 引词条——
    /// 装包层拒·本验证器同触发）④ 参数（value 超声明范围 拒）⑤ 断链（词条定义缺失→整卡不可用）
    /// ⑥ 反模式（分支奖励位引词条 拒）。全部=装载期整卡剔除（不静默降级），卡组面 MissingIds 可见。
    /// 菜单：Tools/验证/展开后校验（G4）。
    /// </summary>
    internal static class CompositeAtomG4Verifier
    {
        private const string PackId = "g4probe";
        private const string BadPackId = "g4bad";
        private const string EmberId = "@g4probe/ember";
        private const string NarrowId = "@g4probe/narrow";
        private const string OkCard = "@g4probe/C_OK";
        private const string BudgetCard = "@g4probe/C_BUDGET";
        private const string DomainCard = "@g4probe/C_DOMAIN";
        private const string ParamCard = "@g4probe/C_PARAM";
        private const string AntipCard = "@g4probe/C_ANTIP";
        private const string GhostCard = "@g4bad/C_GHOST";

        private sealed class Check
        {
            public bool Ok; public string Text;
            public Check(bool ok, string text) { Ok = ok; Text = text; }
        }

        [MenuItem("Tools/验证/展开后校验（G4）")]
        public static void Run()
        {
            var log = new StringBuilder();
            var checks = new List<Check>();
            string tempRoot = null;
            try
            {
                tempRoot = Path.Combine(Path.GetTempPath(), "TideG4Probe_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                RunSixChecks(tempRoot, checks);
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
                    string badDir = Path.Combine(CardDataPaths.PackDir, BadPackId);
                    if (Directory.Exists(badDir)) Directory.Delete(badDir, true);
                    if (tempRoot != null && Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                    InvalidateAll();
                }
                catch { /* 由清场检查兜底 */ }
                checks.Add(new Check(CardCatalog.GetById(OkCard) == null && CardCatalog.GetById(GhostCard) == null
                    && !Directory.Exists(Path.Combine(CardDataPaths.PackDir, PackId)),
                    "清场：两包已卸·目录无残留"));
            }

            int fail = checks.Count(c => !c.Ok);
            foreach (var c in checks)
                log.AppendLine(c.Ok ? "[PASS] " + c.Text : "[FAIL] " + c.Text);
            Debug.Log($"==== CompositeAtomG4Verifier {(fail == 0 ? "全过" : $"失败 {fail}/{checks.Count} 项")} ====\n" + log);
        }

        private static string DealRow() => Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId;
        private static string DealDomain() => Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.Domain;

        private static void InvalidateAll()
        {
            ContentPackRegistry.Invalidate();
            CompositeAtomTable.Invalidate();
            CardCatalog.Invalidate();
            EffectsLibrary.Reload();
        }

        private static EffectSlimDto Fx(string id, params StepRef[] steps) => new EffectSlimDto
        {
            id = id, name = id, timing = 19, activation = 2, steps = steps.ToList(),
        };

        private static StepRef Direct(int v) => new StepRef { kind = 0, atom = new AtomicEffectEntry { refId = DealRow(), value = v } };
        private static StepRef Use(string atomId, int v, string s = null)
            => new StepRef { kind = 0, atom = new AtomicEffectEntry { refId = atomId, value = v, str = s } };

        private static void RunSixChecks(string tempRoot, List<Check> checks)
        {
            string dealDomain = DealDomain();
            var ember = new CompositeAtomDef
            {
                id = EmberId, name = "余烬",
                slot = new CompositeAtomSlot { trunk = 1, domain = dealDomain },
                @params = new List<CompositeAtomParam>
                {
                    new CompositeAtomParam { key = "n", type = "Value", min = 1, max = 5, defaultValue = 2 },
                },
                body = new List<CompositeAtomBodyAtom>
                {
                    new CompositeAtomBodyAtom { refId = DealRow(), value = "{n}" },
                    new CompositeAtomBodyAtom { refId = DealRow(), value = "2" },
                },
            };
            var narrow = new CompositeAtomDef
            {
                id = NarrowId, name = "窄域",
                slot = new CompositeAtomSlot { trunk = 1, domain = "己方战场:生物" }, // 故意不含 DealDamage 行域
                body = new List<CompositeAtomBodyAtom> { new CompositeAtomBodyAtom { refId = DealRow(), value = "{n}" } },
                @params = new List<CompositeAtomParam> { new CompositeAtomParam { key = "n", type = "Value", min = 1, max = 3 } },
            };

            var fxOk = Fx("@g4probe/EFF_OK", Use(EmberId, 3, "Foo"));
            var fxBudget = Fx("@g4probe/EFF_BUDGET", Direct(1), Use(EmberId, 2));
            var fxDomain = Fx("@g4probe/EFF_DOMAIN", Use(NarrowId, 1));
            var fxParam = Fx("@g4probe/EFF_PARAM", Use(EmberId, 7)); // n∈[1,5] 超 5
            var fxAntip = Fx("@g4probe/EFF_ANTIP",
                new StepRef
                {
                    kind = 0,
                    atom = new AtomicEffectEntry
                    {
                        refId = DealRow(), value = 1,
                        branch = new BranchEntryData
                        {
                            settle = 1, gateId = "FirstCardThisTurn",
                            then = new List<AtomicEffectEntry> { new AtomicEffectEntry { refId = EmberId, value = 1 } },
                        },
                    },
                });

            var dtos = new[] { fxOk, fxBudget, fxDomain, fxParam, fxAntip };
            var defs = new List<CompositeAtomDef> { ember, narrow };
            foreach (var d in defs) d.seal = CompositeAtomSeal.Compute(d);

            // —— 装包（装包层只查 def 级——用量级违规照过，G4 装载层收口）——
            string dir = Path.Combine(tempRoot, "good");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "pack.json"),
                JsonUtility.ToJson(new ContentPackManifest { id = PackId, name = "G4 探针", author = "qa", version = "1" }));
            File.WriteAllText(Path.Combine(dir, "atoms.json"), JsonUtility.ToJson(new ContentPackAtomsWrapper { items = defs }));
            File.WriteAllText(Path.Combine(dir, "effects.json"), JsonUtility.ToJson(new ContentPackEffectsWrapper { items = dtos.ToList() }));
            CompositeAtomTable.PushContext(defs);
            List<ContentPackSealEntry> sealItems;
            try
            {
                sealItems = dtos.Select(d => new ContentPackSealEntry
                { id = d.id, seals = new List<string> { ContentPackRegistry.ComputeEffectSeal(d) } }).ToList();
            }
            finally { CompositeAtomTable.PopContext(); }
            File.WriteAllText(Path.Combine(dir, "seal.json"), JsonUtility.ToJson(new ContentPackSealFile { items = sealItems }));
            File.WriteAllText(Path.Combine(dir, "cards.json"), JsonUtility.ToJson(new TestCardsConfigWrapper
            {
                cards = new List<CardConfigEntry>
                {
                    Card(OkCard, "@g4probe/EFF_OK"),
                    Card(BudgetCard, "@g4probe/EFF_BUDGET"),
                    Card(DomainCard, "@g4probe/EFF_DOMAIN"),
                    Card(ParamCard, "@g4probe/EFF_PARAM"),
                    Card(AntipCard, "@g4probe/EFF_ANTIP"),
                },
            }));
            var install = ContentPackRegistry.InstallFrom(dir);
            checks.Add(new Check(install.Ok,
                install.Ok ? "装包通过（用量级违规留给 G4 装载层——分层正确）"
                          : "装包被拒（不应——G4 属装载层）：" + string.Join("；", install.Errors)));
            if (!install.Ok) return;

            // —— 装载触发 + G4 错误捕获 ——
            var g4Errors = new List<string>();
            Application.LogCallback hook = (condition, stack, type) =>
            {
                if (type == LogType.Error && condition != null && condition.Contains("[G4-")) g4Errors.Add(condition);
            };
            Application.logMessageReceived += hook;
            CardData okCardData;
            try
            {
                CardCatalog.Invalidate();
                okCardData = CardCatalog.GetById(OkCard); // 触发全量装载（G4 在此跑）
            }
            finally { Application.logMessageReceived -= hook; }

            string[] tags = { "[G4-位预算]", "[G4-域一致]", "[G4-参数]", "[G4-反模式]" };
            foreach (var tag in tags)
            {
                string hit = g4Errors.FirstOrDefault(e => e.Contains(tag));
                checks.Add(new Check(hit != null, $"{tag} 触发且点名：" + (hit ?? "（未触发）")));
            }

            checks.Add(new Check(okCardData != null
                && CardCatalog.GetById(BudgetCard) == null && CardCatalog.GetById(DomainCard) == null
                && CardCatalog.GetById(ParamCard) == null && CardCatalog.GetById(AntipCard) == null,
                $"违规整卡剔除：OK 卡在={okCardData != null}·位预算/域一致/参数/反模式卡全 miss="
                + (CardCatalog.GetById(BudgetCard) == null && CardCatalog.GetById(DomainCard) == null
                   && CardCatalog.GetById(ParamCard) == null && CardCatalog.GetById(AntipCard) == null)));

            var deck = DeckCompositionValidator.Check("g4", new[] { OkCard, BudgetCard });
            checks.Add(new Check(deck.MissingIds.Contains(BudgetCard) && !deck.MissingIds.Contains(OkCard),
                $"卡组面可见：MissingIds=[{string.Join(",", deck.MissingIds)}]（含违规卡不含好卡）"));

            // —— ⑤ 断链（手摆目录绕过装包校验：引用不存在的词条定义）——
            string badDir = Path.Combine(CardDataPaths.PackDir, BadPackId);
            Directory.CreateDirectory(badDir);
            File.WriteAllText(Path.Combine(badDir, "pack.json"),
                JsonUtility.ToJson(new ContentPackManifest { id = BadPackId, name = "坏包", author = "qa", version = "1" }));
            var ghostFx = Fx("@g4bad/EFF_GHOST", Use("@g4bad/ghost", 1));
            File.WriteAllText(Path.Combine(badDir, "effects.json"),
                JsonUtility.ToJson(new ContentPackEffectsWrapper { items = new List<EffectSlimDto> { ghostFx } }));
            File.WriteAllText(Path.Combine(badDir, "cards.json"), JsonUtility.ToJson(new TestCardsConfigWrapper
            { cards = new List<CardConfigEntry> { Card(GhostCard, "@g4bad/EFF_GHOST") } }));

            var broken5 = new List<string>();
            Application.LogCallback hook5 = (condition, stack, type) =>
            {
                if (type == LogType.Error && condition != null && condition.Contains("[G4-断链]")) broken5.Add(condition);
            };
            Application.logMessageReceived += hook5;
            CardData ghostData;
            try
            {
                InvalidateAll();
                ghostData = CardCatalog.GetById(GhostCard);
            }
            finally { Application.logMessageReceived -= hook5; }
            checks.Add(new Check(broken5.Count > 0 && ghostData == null,
                $"[G4-断链] 触发且整卡不可用：" + (broken5.Count > 0 ? broken5[0] : "（未触发）") + $"·卡 miss={ghostData == null}"));

            // —— ③ 深度（装包层同约束触发：body 引词条拒装）——
            string nestDir = Path.Combine(tempRoot, "nested");
            Directory.CreateDirectory(nestDir);
            var nested = new CompositeAtomDef
            {
                id = "@nest/outer", name = "外层",
                slot = new CompositeAtomSlot { trunk = 1, domain = dealDomain },
                body = new List<CompositeAtomBodyAtom> { new CompositeAtomBodyAtom { refId = "@nest/inner", value = "1" } },
            };
            nested.seal = CompositeAtomSeal.Compute(nested);
            var inner = new CompositeAtomDef
            {
                id = "@nest/inner", name = "内层", // 深度夹具：被嵌套目标必须真实存在——才落「深度恒 1」而非「缺表」
                slot = new CompositeAtomSlot { trunk = 1, domain = dealDomain },
                body = new List<CompositeAtomBodyAtom> { new CompositeAtomBodyAtom { refId = DealRow(), value = "1" } },
            };
            inner.seal = CompositeAtomSeal.Compute(inner);
            File.WriteAllText(Path.Combine(nestDir, "pack.json"),
                JsonUtility.ToJson(new ContentPackManifest { id = "nest", name = "嵌套", author = "qa", version = "1" }));
            File.WriteAllText(Path.Combine(nestDir, "atoms.json"),
                JsonUtility.ToJson(new ContentPackAtomsWrapper { items = new List<CompositeAtomDef> { nested, inner } }));
            var nestInstall = ContentPackRegistry.InstallFrom(nestDir);
            string depthMsg = nestInstall.Errors.FirstOrDefault(e => e.Contains("深度恒 1"));
            string depthText = depthMsg ?? "（未触发：" + string.Join("；", nestInstall.Errors) + "）";
            checks.Add(new Check(!nestInstall.Ok && depthMsg != null,
                $"[G4-深度] 装包层触发：" + depthText));
        }

        private static CardConfigEntry Card(string id, string fxId) => new CardConfigEntry
        {
            id = id, cardName = id.Substring(id.LastIndexOf('/') + 1), supertype = "Spell",
            costList = new List<float> { 0, 1, 0, 0, 0 }, effectIds = new List<string> { fxId },
        };
    }
}
#endif
