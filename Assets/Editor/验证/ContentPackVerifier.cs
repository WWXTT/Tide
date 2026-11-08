// SPDX-FileCopyrightText: 2026 WWXTT
// SPDX-License-Identifier: BUSL-1.1

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using CardCore.Network;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 内容包容器验证器（G2 2026-11-08 · docs/地基计划_虚拟原子与数值A.md §5 验收）：
    /// ① seal 语义——改文案 seal 不变（HashEffect 却变）、改 body seal 变；
    /// ② 装包 → 卡可用（目录可查 + 卡组校验过）；
    /// ③ 摘要闭包——被引用包 seal 计入 AtomicRowsHash（PK 行）；
    /// ④ 卸包 → **整卡断链**（GetById miss + DeckCompositionValidator.MissingIds 点名——
    ///    不静默剔除单原子装成弱卡）；摘要随包缺席分叉；
    /// ⑤ 三类拒装——封印不符 / 原子行缺表 / 卡 id 不在包命名空间，Errors 逐一可触发且不落目录。
    /// 菜单：Tools/验证/内容包容器（G2）。
    /// </summary>
    internal static class ContentPackVerifier
    {
        private const string PackId = "probepack";
        private const string CardId = "@probepack/C_PROBE1";
        private const string EffectId = "@probepack/EFF_PROBE";

        private sealed class Check
        {
            public bool Ok; public string Text;
            public Check(bool ok, string text) { Ok = ok; Text = text; }
        }

        [MenuItem("Tools/验证/内容包容器（G2）")]
        public static void Run()
        {
            var log = new StringBuilder();
            var checks = new List<Check>();
            string tempRoot = null;
            try
            {
                tempRoot = Path.Combine(Path.GetTempPath(), "TidePackProbe_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                RunSealSemantics(checks);
                RunInstallUseUninstall(tempRoot, checks);
                RunRejections(tempRoot, checks);
            }
            catch (Exception e)
            {
                checks.Add(new Check(false, "验证器异常中断：" + e.Message + "\n" + e.StackTrace));
            }
            finally
            {
                // 清场：卸残留包 + 删临时目录——Assets/Packs 不留探针残骸
                try
                {
                    ContentPackRegistry.Uninstall(PackId, out _);
                    if (tempRoot != null && Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                    CardCatalog.Invalidate();
                }
                catch { /* 清场失败不判红（下一条清场检查兜底） */ }
                checks.Add(new Check(CardCatalog.GetById(CardId) == null
                    && !Directory.Exists(Path.Combine(CardDataPaths.PackDir, PackId)),
                    "清场：探针包已卸、临时目录已删、目录无残留"));
            }

            int fail = checks.Count(c => !c.Ok);
            foreach (var c in checks)
                log.AppendLine(c.Ok ? "[PASS] " + c.Text : "[FAIL] " + c.Text);
            Debug.Log($"==== ContentPackVerifier {(fail == 0 ? "全过" : $"失败 {fail}/{checks.Count} 项")} ====\n" + log);
        }

        // ======================================== ① seal 语义（纯哈希层） ========================================

        private static void RunSealSemantics(List<Check> checks)
        {
            string dealRow = Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId;
            if (string.IsNullOrEmpty(dealRow)) { checks.Add(new Check(false, "原子表无 DealDamage 行（夹具前提缺失）")); return; }

            CardEffectData Fx(string name, int value) => new CardEffectData
            {
                Id = EffectId, DisplayName = name, TriggerTiming = 19, ActivationType = 2,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 0, atomic = new AtomicEffectEntry { refId = dealRow, value = value } },
                },
            };
            string hA = ContentHasher.HashEffectOf(Fx("探针·直伤甲", 2));
            string hB = ContentHasher.HashEffectOf(Fx("探针·直伤乙", 2));
            string sA = ContentHasher.SealEffectOf(Fx("探针·直伤甲", 2));
            string sB = ContentHasher.SealEffectOf(Fx("探针·直伤乙", 2));
            string sBody = ContentHasher.SealEffectOf(Fx("探针·直伤甲", 3));
            checks.Add(new Check(hA != hB && sA == sB,
                $"改文案：HashEffect 变（{hA}→{hB}）而 seal 不变（{sA}=={sB}）——seal 只盖行为不盖名字"));
            checks.Add(new Check(sA != sBody,
                $"改 body（value 2→3）：seal 变（{sA}→{sBody}）——封印对行为漂移敏感"));
        }

        // ======================================== ②③④ 装 → 用 → 卸 → 断链 ========================================

        private static EffectSlimDto ProbeDto(int value) => new EffectSlimDto
        {
            id = EffectId, name = "探针·包效果", timing = 19, activation = 2,
            steps = new List<StepRef>
            {
                new StepRef { kind = 0, atom = new AtomicEffectEntry { refId = DealRow(), value = value } },
            },
        };

        private static string DealRow() => Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId;

        private static string BuildPack(string dir, EffectSlimDto dto, string sealOverride = null)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "pack.json"),
                JsonUtility.ToJson(new ContentPackManifest { id = PackId, name = "探针包", author = "qa", version = "1" }));
            File.WriteAllText(Path.Combine(dir, "effects.json"),
                JsonUtility.ToJson(new ContentPackEffectsWrapper { items = new List<EffectSlimDto> { dto } }));
            string seal = sealOverride ?? ContentPackRegistry.ComputeEffectSeal(dto) ?? "∅";
            File.WriteAllText(Path.Combine(dir, "seal.json"), JsonUtility.ToJson(new ContentPackSealFile
            {
                items = new List<ContentPackSealEntry> { new ContentPackSealEntry { id = EffectId, seals = new List<string> { seal } } },
            }));
            File.WriteAllText(Path.Combine(dir, "cards.json"), JsonUtility.ToJson(new TestCardsConfigWrapper
            {
                cards = new List<CardConfigEntry>
                {
                    new CardConfigEntry
                    {
                        id = CardId, cardName = "探针·包卡", supertype = "Spell",
                        costList = new List<float> { 0, 1, 0, 0, 0 },
                        effectIds = new List<string> { EffectId },
                    },
                },
            }));
            return dir;
        }

        private static void RunInstallUseUninstall(string tempRoot, List<Check> checks)
        {
            var good = BuildPack(Path.Combine(tempRoot, "good"), ProbeDto(2));

            // 装包
            var report = ContentPackRegistry.InstallFrom(good);
            checks.Add(new Check(report.Ok,
                report.Ok ? $"装包成功（卡 {report.CardCount} · 效果 {report.EffectCount}）"
                          : "装包被拒：" + string.Join("；", report.Errors)));

            // 卡可用：目录可查 + 效果可解析 + 卡组校验过
            var card = CardCatalog.GetById(CardId);
            var fx = EffectsLibrary.Get(EffectId);
            var deckReport = DeckCompositionValidator.Check("probe", new[] { CardId });
            checks.Add(new Check(card != null && card.Supertype == Cardtype.Spell && fx != null,
                $"装包后可见：GetById={card != null}（{card?.Supertype}）· EffectsLibrary.Get={fx != null}"));
            checks.Add(new Check(deckReport.Ok && deckReport.MissingIds.Count == 0,
                $"卡组校验过：Ok={deckReport.Ok}（Missing={deckReport.MissingIds.Count}·ShapeErr={deckReport.ShapeErrors.Count}）"
                + (deckReport.ShapeErrors.Count > 0 ? "——" + deckReport.ShapeErrors[0] : "")));

            // 摘要闭包：被引用包 seal 计入 AtomicRowsHash
            var sealLines = ContentPackRegistry.ReferencedSealLines(new[] { EffectId });
            var d1 = NetMatchHandshake.ComputeDeckDigest(new[] { CardId });
            var d2 = NetMatchHandshake.ComputeDeckDigest(new[] { CardId });
            checks.Add(new Check(sealLines.Count == 1 && sealLines[0].StartsWith("PK:" + PackId + "|", StringComparison.Ordinal)
                && d1.AtomicRowsHash == d2.AtomicRowsHash && d1.AtomicRowCount >= 2,
                $"封印入闭包：PK 行={sealLines.Count}（{string.Join(",", sealLines)}）· 摘要重算稳定={d1.AtomicRowsHash == d2.AtomicRowsHash}·行数={d1.AtomicRowCount}（原子行+封印行）"));

            // 卸包 → 整卡断链（点名 MissingIds，非静默弱卡）+ 摘要分叉
            string digestWithPack = d1.AtomicRowsHash;
            bool uninstalled = ContentPackRegistry.Uninstall(PackId, out var uninstallErr);
            var gone = CardCatalog.GetById(CardId);
            var brokenDeck = DeckCompositionValidator.Check("probe", new[] { CardId });
            var d3 = NetMatchHandshake.ComputeDeckDigest(new[] { CardId });
            checks.Add(new Check(uninstalled && gone == null && !brokenDeck.Ok
                && brokenDeck.MissingIds.Contains(CardId),
                $"卸包=整卡断链：Uninstall={uninstalled}（{uninstallErr ?? "ok"}）· GetById miss={gone == null}·"
                + $"卡组 MissingIds 点名={brokenDeck.MissingIds.Contains(CardId)}（不静默装弱卡）"));
            checks.Add(new Check(d3.AtomicRowsHash != digestWithPack && d3.AtomicRowCount == 0,
                $"摘要随包分叉：装包 {digestWithPack}（{d1.AtomicRowCount} 行）→ 卸包 {d3.AtomicRowsHash}（{d3.AtomicRowCount} 行）"));
        }

        // ======================================== ⑤ 三类拒装 ========================================

        private static void RunRejections(string tempRoot, List<Check> checks)
        {
            // ㊀ 封印不符（body 与封印不一致——seal.json 故意声明错值）
            var tampered = BuildPack(Path.Combine(tempRoot, "tampered"), ProbeDto(2), sealOverride: "00000000");
            var r0 = ContentPackRegistry.InstallFrom(tampered);
            string m0 = r0.Errors.Count > 0 ? r0.Errors[0] : "（无错误信息）";
            checks.Add(new Check(!r0.Ok && r0.Errors.Any(e => e.Contains("封印不符"))
                && CardCatalog.GetById(CardId) == null,
                "拒装·封印不符：" + m0));

            // ㊁ 原子行缺表（效果引用不存在的原子行——整卡断链口径，不得静默剔除单原子）
            var badDto = ProbeDto(2);
            badDto.steps[0].atom.refId = "ffffffff";
            var badAtom = BuildPack(Path.Combine(tempRoot, "badatom"), badDto, sealOverride: "00000000");
            var r1 = ContentPackRegistry.InstallFrom(badAtom);
            string m1 = r1.Errors.Count > 0 ? r1.Errors[0] : "（无错误信息）";
            checks.Add(new Check(!r1.Ok && r1.Errors.Any(e => e.Contains("原子行缺表") && e.Contains("整卡断链"))
                && CardCatalog.GetById(CardId) == null,
                "拒装·原子行缺表：" + m1));

            // ㊂ 卡 id 不在包命名空间
            var nsPack = BuildPack(Path.Combine(tempRoot, "ns"), ProbeDto(2));
            var wrapper = JsonUtility.FromJson<TestCardsConfigWrapper>(File.ReadAllText(Path.Combine(nsPack, "cards.json")));
            wrapper.cards[0].id = "C_OUTSIDE_NS";
            File.WriteAllText(Path.Combine(nsPack, "cards.json"), JsonUtility.ToJson(wrapper));
            var r2 = ContentPackRegistry.InstallFrom(nsPack);
            string m2 = r2.Errors.Count > 0 ? r2.Errors[0] : "（无错误信息）";
            checks.Add(new Check(!r2.Ok && r2.Errors.Any(e => e.Contains("不在包命名空间"))
                && CardCatalog.GetById(CardId) == null,
                "拒装·命名空间：" + m2));
        }
    }
}
#endif
