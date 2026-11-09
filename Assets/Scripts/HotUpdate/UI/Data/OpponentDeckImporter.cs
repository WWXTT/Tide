using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using CardCore.Network;

namespace SynergyUI
{
    /// <summary>
    /// 导入对手卡组（2026-11-09 协议 v11 对局记录定案）：把一条 EncounterRecord 的对手内容拷入
    /// 本地（对局期默认不显示——导入是玩家在对局记录屏的显式动作）。三段落地：
    /// · 内容包来源（卡 ID 带 "@包id/" 前缀）→ **按原包 id 合成内容包**装包（ContentPackRegistry.
    ///   InstallFrom）——必须保原包 id：效果封印（seal）行=「包id|效果id|seal」计入握手摘要，
    ///   改包 id 会令同卡组两端摘要分叉、日后联机互拒；词条（@前缀 refId）也只能以包形态持久化。
    /// · 玩家自建来源（C_ 前缀卡/内容哈希效果）→ upsert 进本地 Cards.json/Effects.json——
    ///   **本地已有同 ID 跳过保留本地**（ID 冻结后同 ID 可载不同内容；效果 ID=内容哈希，同 ID
    ///   即同内容，跳过无损）。
    /// · 卡组本体 → DeckSerializer.Save（重名追加「（对手）」后缀直到唯一）。
    /// 完成后 GameDataReload 全量失效——卡池/卡组列表立即可见。返回人话结果（错误时含原因）。
    /// </summary>
    public static class OpponentDeckImporter
    {
        /// <summary>导入一条对局记录的对手卡组。成功返回 null；失败返回人话错误。</summary>
        public static string Import(EncounterRecord record, out string summary)
        {
            summary = null;
            if (record == null) return "记录为空";
            if (record.opponentCardIds == null || record.opponentCardIds.Length == 0) return "该记录无对手卡组数据";

            var cards = (record.cards ?? Array.Empty<MsgCardPayload>()).Where(p => p != null && !string.IsNullOrEmpty(p.CardJson)).ToList();
            var effects = (record.effects ?? Array.Empty<MsgEffectPayload>()).Where(p => p != null && !string.IsNullOrEmpty(p.EffectJson)).ToList();
            var atomRows = (record.atomRows ?? Array.Empty<MsgAtomRowPayload>()).Where(p => p != null && !string.IsNullOrEmpty(p.RowJson)).ToList();

            // ---- 分流：包命名空间卡（@包id/…）按原包分组；玩家自建卡（其余）直进本地表 ----
            var packCards = new Dictionary<string, List<MsgCardPayload>>(StringComparer.Ordinal); // packId → 卡载荷
            var ownCards = new List<MsgCardPayload>();
            foreach (var p in cards)
            {
                if (CardCatalog.GetById(p.CardId) != null) continue; // 本地已有同 ID——保留本地（跳过）
                if (p.CardId.StartsWith("@", StringComparison.Ordinal))
                {
                    string packId = OriginalPackIdOf(p.CardId);
                    if (packId == null) { ownCards.Add(p); continue; }
                    if (!packCards.TryGetValue(packId, out var list)) packCards[packId] = list = new List<MsgCardPayload>();
                    list.Add(p);
                }
                else ownCards.Add(p);
            }

            // 效果归属：包卡引用的效果随包走（封印一致性）；玩家卡引用的效果进本地效果表。
            // 逐载荷去重 + 本地已有跳过。
            var effectsByRef = new Dictionary<string, MsgEffectPayload>(StringComparer.Ordinal);
            foreach (var e in effects)
            {
                var dto = ParseEffect(e);
                if (dto == null) continue;
                effectsByRef[dto.id] = e;
            }
            var packEffectIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal); // packId → 效果 id 集
            foreach (var group in packCards.Values)
                foreach (var p in group)
                    foreach (var eid in EffectIdsOfEntry(p))
                        if (effectsByRef.ContainsKey(eid))
                        {
                            string pk = OriginalPackIdOf(p.CardId);
                            if (!packEffectIds.TryGetValue(pk, out var set)) packEffectIds[pk] = set = new HashSet<string>(StringComparer.Ordinal);
                            set.Add(eid);
                        }
            var ownEffectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in ownCards)
                foreach (var eid in EffectIdsOfEntry(p))
                    ownEffectIds.Add(eid);
            // 词条（@前缀 def id）按其原包分组（包可能只有词条无卡——对手自建卡引用了对方包词条）
            var packAtoms = new Dictionary<string, List<CompositeAtomDef>>(StringComparer.Ordinal);
            foreach (var a in atomRows)
            {
                CompositeAtomDef def;
                try { def = TideJson.FromJson<CompositeAtomDef>(a.RowJson); } catch { continue; }
                if (def == null || string.IsNullOrEmpty(def.id) || def.body == null) continue;
                if (CompositeAtomTable.Get(def.id) != null) continue; // 本地已装
                string pk = OriginalPackIdOf(def.id);
                if (pk == null) continue;
                if (!packAtoms.TryGetValue(pk, out var list)) packAtoms[pk] = list = new List<CompositeAtomDef>();
                list.Add(def);
            }

            var errors = new List<string>();
            int cardsSaved = 0, effectsSaved = 0, packsSaved = 0;

            // ---- ① 合成内容包装包（原包 id；卡+该包效果+词条同包；包 id 并集=有卡或有词条的包） ----
            var packIds = new List<string>(packCards.Keys);
            foreach (string pk in packAtoms.Keys)
                if (!packIds.Contains(pk)) packIds.Add(pk);
            foreach (string packId in packIds)
            {
                var groupCards = packCards.TryGetValue(packId, out var gc) ? gc : new List<MsgCardPayload>();
                var groupAtoms = packAtoms.TryGetValue(packId, out var ga) ? ga : new List<CompositeAtomDef>();
                var groupEffects = packEffectIds.TryGetValue(packId, out var ge)
                    ? ge.Select(id => effectsByRef[id]).ToList() : new List<MsgEffectPayload>();
                string err = InstallSyntheticPack(record, packId, groupCards, groupEffects, groupAtoms);
                if (err != null) errors.Add(err);
                else
                {
                    packsSaved++;
                    cardsSaved += groupCards.Count;
                }
            }

            // ---- ② 玩家自建效果 upsert（本地缺行才写；同 ID 同内容跳过无损） ----
            foreach (var id in ownEffectIds)
            {
                if (EffectsLibrary.Get(id) != null) continue;
                var dto = ParseEffect(effectsByRef[id]);
                if (dto == null) continue;
                EffectLibrarySerializer.SaveDto(dto);
                effectsSaved++;
            }

            // ---- ③ 玩家自建卡 upsert 进 Cards.json（经 CardLoader 物化→Save；效果已在②/包内，幂等） ----
            foreach (var p in ownCards)
            {
                var card = Materialize(p);
                if (card == null) { errors.Add($"卡 {p.CardId} 载荷无法物化（跳过）"); continue; }
                CardConfigSerializer.Save(card);
                cardsSaved++;
            }

            if (errors.Count > 0)
                return "导入失败：" + string.Join("；", errors.Take(3));

            // ---- ④ 卡组本体（重名追加后缀直到唯一；预组名只读——也走后缀绕开） ----
            string deckName = UniqueDeckName(string.IsNullOrEmpty(record.opponentDeckName) ? $"{record.opponentName}的卡组" : record.opponentDeckName);
            var deck = new DeckData(deckName)
            {
                cardIds = record.opponentCardIds.ToList(),
                skillCardId = string.IsNullOrEmpty(record.opponentSkillCardId) ? null : record.opponentSkillCardId,
                tokenCardIds = record.opponentTokenCardIds != null && record.opponentTokenCardIds.Length > 0
                    ? record.opponentTokenCardIds.ToList() : null,
                tokenBindings = NetMatchHandshake.FromWireBindings(record.opponentTokenBindings),
            };
            DeckSerializer.Save(deck);

            GameDataReload.ReloadAllCaches(); // 卡池/效果库/卡组列表立即可见

            summary = $"已导入「{deckName}」：新卡 {cardsSaved} 张、效果 {effectsSaved} 条、内容包 {packsSaved} 个"
                      + (packsSaved > 0 ? "（原包 id 保持——联机摘要两端一致）" : "");
            TideLog.Info($"[OpponentImport] {summary}");
            return null;
        }

        /// <summary>原包 id（"@packId/名" → packId；非该形态返回 null）。</summary>
        private static string OriginalPackIdOf(string namespacedId)
        {
            if (string.IsNullOrEmpty(namespacedId) || namespacedId[0] != '@') return null;
            int slash = namespacedId.IndexOf('/');
            if (slash <= 1) return null;
            return namespacedId.Substring(1, slash - 1);
        }

        /// <summary>合成内容包：临时目录写 pack.json/cards.json/effects.json/atoms.json（缺省块不写）
        /// → InstallFrom（含装包校验六查）→ 删临时目录。效果带 seal.json（按现装内容计算——
        /// 同 dto 自算自查，恒过且与对端真实包的 seal 行一致）。</summary>
        private static string InstallSyntheticPack(EncounterRecord record, string packId,
            List<MsgCardPayload> cards, List<MsgEffectPayload> effects, List<CompositeAtomDef> atoms)
        {
            if (cards.Count == 0 && effects.Count == 0 && atoms.Count == 0) return null;
            if (packId.Length > 32 || packId.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                return $"对手内容包 id 非法（{packId}）——跳过装包";

            string tmpRoot = Path.Combine(TidePaths.PersistentDataPath, "tmp-import");
            string tmpDir = Path.Combine(tmpRoot, packId);
            try
            {
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                Directory.CreateDirectory(tmpDir);
                File.WriteAllText(Path.Combine(tmpDir, "pack.json"), TideJson.ToJson(new ContentPackManifest
                {
                    id = packId,
                    name = $"对手导入·{record.opponentName}",
                    author = record.opponentName,
                    version = "imported",
                    notes = "对局记录「导入对手卡组」自动合成（原包 id 保持——seal 摘要两端一致）",
                }));
                if (cards.Count > 0)
                    File.WriteAllText(Path.Combine(tmpDir, "cards.json"),
                        "{\"cards\":[" + string.Join(",", cards.Select(p => p.CardJson)) + "]}");
                if (effects.Count > 0)
                {
                    var dtos = effects.Select(ParseEffect).Where(d => d != null).ToList();
                    File.WriteAllText(Path.Combine(tmpDir, "effects.json"), TideJson.ToJson(
                        new CardCore.ContentPackEffectsWrapper { items = dtos }));
                    var seal = new ContentPackSealFile
                    {
                        items = dtos.Select(d => new ContentPackSealEntry
                        {
                            id = d.id,
                            seals = new List<string> { ContentPackRegistry.ComputeEffectSeal(d) },
                        }).ToList(),
                    };
                    File.WriteAllText(Path.Combine(tmpDir, "seal.json"), TideJson.ToJson(seal));
                }
                if (atoms.Count > 0)
                    File.WriteAllText(Path.Combine(tmpDir, "atoms.json"), TideJson.ToJson(
                        new CardCore.ContentPackAtomsWrapper { items = atoms }));

                var report = ContentPackRegistry.InstallFrom(tmpDir);
                return report.Ok ? null : $"内容包 {packId} 装包被拒（{string.Join("；", report.Errors.Take(2))}）";
            }
            catch (Exception ex)
            {
                return $"内容包 {packId} 合成失败：{ex.Message}";
            }
            finally
            {
                try { if (Directory.Exists(tmpRoot)) Directory.Delete(tmpRoot, true); } catch { }
            }
        }

        private static EffectSlimDto ParseEffect(MsgEffectPayload payload)
        {
            try { return TideJson.FromJson<EffectSlimDto>(payload.EffectJson); }
            catch { return null; }
        }

        private static IEnumerable<string> EffectIdsOfEntry(MsgCardPayload card)
        {
            // 行 JSON 的 effectIds 列（效果引用化——与发送端 EntryOf 同构）
            try
            {
                var entry = TideJson.FromJson<CardConfigEntry>(card.CardJson);
                return entry?.effectIds ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        private static CardData Materialize(MsgCardPayload payload)
        {
            try
            {
                var cards = CardLoader.LoadCardsFromText("{\"cards\":[" + payload.CardJson + "]}");
                return cards != null && cards.Count > 0 ? cards[0] : null;
            }
            catch { return null; }
        }

        private static string UniqueDeckName(string baseName)
        {
            string name = baseName;
            int suffix = 1;
            var existing = new HashSet<string>(DeckSerializer.LoadAll().Select(d => d.name), StringComparer.Ordinal);
            while (existing.Contains(name) || CardDataPaths.IsPresetDeckName(name))
                name = $"{baseName}（对手{++suffix}）";
            return name;
        }
    }
}
