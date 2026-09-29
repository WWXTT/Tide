using System.Collections.Generic;
using System.IO;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌 JSON 写出/读回 —— 把合成界面产出的 CardData 写成 CardLoader 能解析的格式。
    /// 项目原本只有 CardLoader 读、无任何写出路径，本类补齐写出端。
    ///
    /// 输出严格匹配 CardLoader 的 schema：
    ///   外层 TestCardsConfigWrapper { cards:[CardConfigEntry], deckConfig:{copiesPerCard} }
    ///   每卡 CardConfigEntry（camelCase: id/cardName/supertype/power/life/costList/keywords/tags/effects）
    ///   costList 项 { manaType:int, amount:float }；effects 项为 CardEffectData（PascalCase）。
    ///
    /// 落盘到 StreamingAssets/Card/Cards.json（2026-09-14 统一定案+效果引用化；2026-09-21 定案：
    /// 卡属用户数据，与效果/卡组同住 Card/ 目录。编辑器读取（CardCatalog 卡池）与 UI 保存同一文件——
    /// 合成卡即入池；效果不内嵌——写 effectIds 引用，定义经 EffectLibrarySerializer upsert 进
    /// Card/Effects.json；TestDecks 原件留作训练桥/验证夹具）。
    /// </summary>
    public static class CardConfigSerializer
    {
        public const string DefaultFileRelative = "Card/Cards.json";

        private static string PathFor(string relative)
        {
            // 路径收口（2026-09-24）：经 CardDataPaths——编辑器 StreamingAssets / 玩家 persistentData
            return CardDataPaths.FileIn("Cards.json");
        }

        /// <summary>
        /// 把一张卡写入目标卡表（按 id 去重更新，不存在则追加）。返回写入的完整路径。
        /// replaceId（2026-09-24 覆盖替换定案）：编辑旧卡保存时传旧 ID——内容哈希机制下内容变则
        /// ID 必变、无法原地覆盖，落盘新卡后删除旧条目（调用方负责先检查卡组引用）。
        /// </summary>
        public static string Save(CardData card, string fileRelative = null, string replaceId = null)
        {
            if (card == null)
            {
                Debug.LogWarning("[CardConfigSerializer] 卡牌为空，已跳过保存。");
                return null;
            }

            string path = PathFor(fileRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var wrapper = ReadWrapper(path) ?? new TestCardsConfigWrapper();
            if (wrapper.cards == null)
            {
                wrapper.cards = new List<CardConfigEntry>();
            }
            if (wrapper.deckConfig == null)
            {
                wrapper.deckConfig = new DeckConfig();
            }

            // 效果引用化：ToEntry 的 EffectIdsOf 逐效果 upsert（HashEffect 为 id）并取引用——
            // 效果 id → 卡 id 依赖链在 HashCard 内部同构推导（ContentHasher.HashEffectOf 单源投影）

            var entry = ToEntry(card);

            // 按功能内容哈希去重：相同功能内容（id 相同）替换，不重复追加。
            int existing = wrapper.cards.FindIndex(c => c != null && c.id == entry.id);
            if (existing >= 0)
            {
                wrapper.cards[existing] = entry;
            }
            else
            {
                wrapper.cards.Add(entry);
            }

            // 覆盖替换（2026-09-24 定案）：删旧存新——replaceId 与新 id 相同（内容未变）时为无操作
            if (!string.IsNullOrEmpty(replaceId) && replaceId != entry.id)
            {
                wrapper.cards.RemoveAll(c => c != null && c.id == replaceId);
            }

            File.WriteAllText(path, JsonUtility.ToJson(wrapper, true));
            return path;
        }

        /// <summary>读回目标卡表为 CardData 列表（复用 CardLoader 解析器验证 round-trip）。</summary>
        public static List<CardData> LoadAll(string fileRelative = null)
        {
            string path = PathFor(fileRelative);
            if (!File.Exists(path))
            {
                return new List<CardData>();
            }
            return CardLoader.LoadCardsFromText(File.ReadAllText(path));
        }

        // 把 CardData 映射回 JSON 条目。
        private static CardConfigEntry ToEntry(CardData card)
        {
            // 关键词引用化（2026-09-24 定案）：本体关键词=effectIds 直接引用原子 refId（Grant 族反查），
            // keywords 列退役——仅承接反查失败的遗留 id（引擎专用词），装载端合并去重。
            // HashCard 读运行时 Keywords 列表（物化结果）——引用化前后卡 ID 稳定，卡组引用不断链。
            var effectIds = EffectLibrarySerializer.EffectIdsOf(card.Effects);
            var unmappedKeywords = new List<string>();
            foreach (var kw in card.Keywords ?? new List<string>())
            {
                if (CardCore.Attribute.Handlers.GrantKeywordHandlerFactory.TryGetAtomRefId(kw, out var refId))
                {
                    if (!effectIds.Contains(refId)) effectIds.Add(refId);
                }
                else
                {
                    unmappedKeywords.Add(kw);
                    Debug.LogWarning($"[CardConfigSerializer] 关键词 {kw} 反查不到原子表行——留 legacy keywords 列");
                }
            }

            var entry = new CardConfigEntry
            {
                id = "C_" + ContentHasher.HashCard(card),
                cardName = card.CardName ?? "",
                supertype = card.Supertype.ToString(),
                power = card.Power ?? 0,
                life = card.Life ?? 0,
                costList = new List<CostJsonEntry>(),
                keywords = unmappedKeywords,
                tags = card.Tags != null ? new List<string>(card.Tags) : new List<string>(),
                effects = null, // 效果引用化（2026-09-14）：不内嵌——经 effectIds 引用 Effects.json
                effectIds = effectIds,
                subtype = card.Subtype == CardSubtype.None ? "" : card.Subtype.ToString(),
                level = card.Level ?? -1,
                arrows = card.ArrowDirections.ToString(),
                linkAuras = card.LinkAuras != null && card.LinkAuras.Count > 0
                    ? new List<LinkAuraData>(card.LinkAuras)
                    : null, // 空表不写列（向后兼容旧 JSON）
                durability = card.Durability,
            };

            if (card.Cost != null)
            {
                foreach (var kv in card.Cost)
                {
                    entry.costList.Add(new CostJsonEntry { manaType = kv.Key, amount = kv.Value });
                }
            }
            return entry;
        }

        private static TestCardsConfigWrapper ReadWrapper(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonUtility.FromJson<TestCardsConfigWrapper>(File.ReadAllText(path));
        }
    }
}
