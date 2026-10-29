#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CardCore;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;
using CardCore.Network;
using SynergyUI;
using EffectDefinitionNS = CardCore.EffectDefinition;

namespace CardCore.Editor
{
    /// <summary>
    /// P9b 衍生物槽绑定（2026-10-29）数据层验收：.ztmp/refactor-20261028/P9b.md §7 验收 2~7。
    /// 纯数据层断言（不进 Play、不碰场景）：槽枚举/实例解析回落链/卡组校验四拒/联机摘要分叉/
    /// 旧数据固定绑定存活。验收 1（两工程 0 error）与 8（汇报）不在此跑。
    /// 运行口：execute_code 反射调用 Run（P5 先例）——返回逐条 PASS/FAIL 报告串。
    /// </summary>
    public static class P9bTokenBindingVerifier
    {
        public static string Run()
        {
            var lines = new List<string>();
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = "")
            {
                lines.Add((ok ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? "" : "——" + detail));
                if (ok) pass++; else fail++;
            }

            // ---- 夹具：卡表装载 + 合成带空槽宿主卡 + 真实生物模板 ----
            SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            var summonRow = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.SummonToken);
            Check("夹具·原子表 SummonToken 行可查", summonRow != null, summonRow?.HashId ?? "null");
            if (summonRow == null)
                return string.Join("\n", lines) + $"\n== P9b 验收 {pass}/{pass + fail}（夹具失败，中止）==";

            var host = MakeHostCard(summonRow.HashId); // 合成：法术·1 效果·SummonToken 原子 str 空 = 未绑定槽
            // 模板取自预组卡组的真实生物（主 Cards.json 按设计为空——生物全在合并视图/教学表；
            // GetById 走合并视图，与引擎解析同口）
            var presetDeck = DeckCatalog.LoadPresets().FirstOrDefault(p => p?.cardIds != null && p.cardIds.Count > 0);
            var templates = (presetDeck?.cardIds ?? new List<string>())
                .Select(id => CardCatalog.GetById(id))
                .Where(c => c != null && c.Supertype == Cardtype.Creature)
                .Distinct()
                .Take(8)
                .ToList();
            Check("夹具·真实生物模板 ≥8 张", templates.Count >= 8,
                $"预组 {presetDeck?.name ?? "?"} 实得 {templates.Count}");
            if (templates.Count < 8)
                return string.Join("\n", lines) + $"\n== P9b 验收 {pass}/{pass + fail}（夹具失败，中止）==";

            // 宿主卡注入目录缓存（编辑器夹具专用反射——校验器按 id 解析卡，生产无此路径）
            var byId = typeof(CardCatalog).GetField("_byId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.GetValue(null) as Dictionary<string, CardData>;
            if (byId != null) byId[host.ID] = host;
            Check("夹具·宿主卡注入目录（GetById 可解析）", CardCatalog.GetById(host.ID) == host);

            // ---- 验收2：同一张临摹卡在两个卡组绑不同衍生物——卡 ID 相同、解析模板不同 ----
            var slots = DeckTokenSupport.EnumerateSlots(host);
            Check("验收2·槽枚举命中 1 空槽（效果0）",
                slots.Count == 1 && slots[0].EffectIndex == 0 && slots[0].NeedsBinding,
                $"slots={slots.Count} idx={slots.FirstOrDefault().EffectIndex}");
            var bindingsA = new List<DeckTokenBinding> { new DeckTokenBinding(host.ID, 0, templates[0].ID) };
            var bindingsB = new List<DeckTokenBinding> { new DeckTokenBinding(host.ID, 0, templates[1].ID) };
            var deckA = CardLoader.BuildDeck(new List<CardData> { host }, 1, bindingsA);
            var deckB = CardLoader.BuildDeck(new List<CardData> { host }, 1, bindingsB);
            var atom = new AtomicEffectInstance { Type = AtomicEffectType.SummonToken, RowHashId = summonRow.HashId };
            var def = CardEffectConverter.ConvertOne(host.Effects[0], host.ID);
            string idA = ResolveWith(deckA[0], atom, def);
            string idB = ResolveWith(deckB[0], atom, def);
            Check("验收2·两卡组同卡同 ID",
                deckA[0].ID == deckB[0].ID && deckA[0].ID == host.ID, deckA[0].ID);
            Check("验收2·实例绑定分叉（解析模板不同且各归其主）",
                idA == templates[0].ID && idB == templates[1].ID && idA != idB,
                $"A→{idA} B→{idB}（期望 {templates[0].ID} / {templates[1].ID}）");

            // ---- 验收3：未绑定保存被拒（reason 指向具体卡与槽） ----
            var unbound = DeckTokenSupport.ValidateDeck(new List<string> { host.ID }, new List<string>(), new List<DeckTokenBinding>());
            Check("验收3·未绑定被拒且指向卡与效果槽",
                unbound.Count > 0 && unbound[0].Contains(host.CardName) && unbound[0].Contains("效果1") && unbound[0].Contains("未绑定"),
                unbound.FirstOrDefault() ?? "(无错误=漏拦)");

            // ---- 验收4：绑定不在衍生物区 → 拒 ----
            var notInZone = DeckTokenSupport.ValidateDeck(new List<string> { host.ID }, new List<string>(), bindingsA);
            Check("验收4·绑定值不在衍生物区被拒",
                notInZone.Any(e => e.Contains("不在衍生物区")), notInZone.FirstOrDefault() ?? "(无错误=漏拦)");

            // ---- 验收5：衍生物区超 6 → 拒 ----
            var overZone = new List<string>(templates.GetRange(0, 7).Select(t => t.ID));
            var overBind = new List<DeckTokenBinding> { new DeckTokenBinding(host.ID, 0, templates[0].ID) };
            var capErrors = DeckTokenSupport.ValidateDeck(new List<string> { host.ID }, overZone, overBind);
            Check("验收5·区超 6 被拒", capErrors.Any(e => e.Contains("超容量")),
                capErrors.FirstOrDefault(e => e.Contains("超容量")) ?? "(无错误=漏拦)");

            // ---- 验收6：绑定不同、卡表相同 → 摘要不同（互不同步） ----
            string[] sameCards = { templates[2].ID, templates[3].ID };
            var zone = new List<string> { templates[0].ID, templates[1].ID };
            var digA = NetMatchHandshake.ComputeDeckDigest(sameCards, zone, bindingsA);
            var digB = NetMatchHandshake.ComputeDeckDigest(sameCards, zone, bindingsB);
            Check("验收6·同卡表异绑定 → TokenSig 不同（AtomicRowsHash 相同）",
                digA.AtomicRowsHash == digB.AtomicRowsHash && digA.TokenSig != digB.TokenSig,
                $"rows {digA.AtomicRowsHash}=={digB.AtomicRowsHash} sig {digA.TokenSig}!={digB.TokenSig}");
            var manifest = NetMatchHandshake.BuildMatchManifest(0, sameCards, 30, zone, bindingsA);
            bool verifyMismatch = NetMatchHandshake.VerifyMatchManifest(manifest, sameCards, zone, bindingsB, out var reason6);
            bool verifyMatch = NetMatchHandshake.VerifyMatchManifest(manifest, sameCards, zone, bindingsA, out _);
            Check("验收6·清单校验：绑定漂移即拒、一致即过",
                !verifyMismatch && verifyMatch && (reason6 ?? "").Contains("衍生物绑定摘要不一致"),
                $"mismatch={verifyMismatch} match={verifyMatch} reason={reason6}");

            // ---- 验收7：旧数据（StringValue 直书 ID）回落路径生效 + 预组仍可跑 ----
            var fixedAtom = new AtomicEffectInstance
            { Type = AtomicEffectType.SummonToken, RowHashId = summonRow.HashId, StringValue = templates[4].ID };
            var noBindDeck = CardLoader.BuildDeck(new List<CardData> { host }, 1, null); // 无绑定实例
            string viaStr = ResolveWith(noBindDeck[0], fixedAtom, def);
            Check("验收7·空绑定实例回落原子 str 固定绑定", viaStr == templates[4].ID, $"→{viaStr}");
            var presets = DeckCatalog.LoadPresets();
            Check("验收7·三套预组可读且构成校验通过（固定绑定不拦）",
                presets.Count > 0 && presets.All(p => DeckCompositionValidator.Usable(p)),
                string.Join("/", presets.Select(p => $"{p.name}:{(DeckCompositionValidator.Usable(p) ? "OK" : "FAIL")}")));
            var tokenPreset = presets.FirstOrDefault(p => p.tokenCardIds != null && p.tokenCardIds.Count > 0);
            Check("验收7·含模板预组规范化（tokenCardIds 非空且 cardIds 无残留）",
                tokenPreset == null
                || (tokenPreset.cardIds.All(id => CardCatalog.GetById(id)?.IsTokenTemplate != true)),
                tokenPreset == null ? "(预组本无模板卡)" : $"{tokenPreset.name} tokens={tokenPreset.tokenCardIds.Count}");
            var builtPreset = tokenPreset != null ? DeckTokenSupport.BuildCardList(tokenPreset) : new List<CardData>();
            int extraCount = builtPreset.Count(c => c.IsTokenTemplate);
            Check("验收7·预组组卡：模板随组（IsTokenTemplate 卡在列——落额外区前提）",
                tokenPreset == null || extraCount == tokenPreset.tokenCardIds.Count,
                $"{tokenPreset?.name} tokens in built={extraCount}/{tokenPreset?.tokenCardIds.Count}");

            // 夹具还原：宿主卡移出目录缓存（生产无此路径——见上方注入注释）
            if (byId != null) byId.Remove(host.ID);

            lines.Add($"== P9b 验收（数据层 2~7）pass {pass} / fail {fail} ==");
            return string.Join("\n", lines);
        }

        /// <summary>合成宿主卡：法术·单效果·主干 SummonToken 原子 str 空（= 卡组期绑定槽）。</summary>
        private static CardData MakeHostCard(string summonRefId)
        {
            var fx = new CardEffectData
            {
                Id = "EFF_P9B_LINMO_TEST",
                DisplayName = "召唤测试槽",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                ActivationType = 2,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry { refId = summonRefId, value = 1, str = null },
                },
            };
            var card = new CardData
            {
                ID = "C_P9B_LINMO_HOST",
                CardName = "P9b临摹测试",
                Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { fx },
                Keywords = new List<string>(),
                Tags = new List<string>(),
            };
            CardCostService.EnsureCost(card);
            return card;
        }

        /// <summary>经执行上下文解析生效模板 ID（与引擎结算同参：Source=卡实例 + ResolvingEffect 对位）。</summary>
        private static string ResolveWith(CardCore.Card wrapper, AtomicEffectInstance atom, EffectDefinitionNS def)
        {
            var ctx = new EffectExecutionContext
            {
                Source = wrapper,
                ResolvingEffect = def,
            };
            return SummonTokenHandler.ResolveBoundTemplateId(atom, ctx);
        }
    }
}
#endif
