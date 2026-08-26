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
    /// 落盘到 Assets/Configs/<file>.json（默认 ComposedCards.json）。仅编辑器内有效。
    /// </summary>
    public static class CardConfigSerializer
    {
        public const string DefaultFileRelative = "Configs/ComposedCards.json";

        private static string PathFor(string relative)
        {
            return Path.Combine(Application.dataPath, string.IsNullOrEmpty(relative) ? DefaultFileRelative : relative);
        }

        /// <summary>
        /// 把一张卡写入目标卡表（按 id 去重更新，不存在则追加）。返回写入的完整路径。
        /// </summary>
        public static string Save(CardData card, string fileRelative = null)
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
            var entry = new CardConfigEntry
            {
                id = "C_" + ContentHasher.HashCard(card),
                cardName = card.CardName ?? "",
                supertype = card.Supertype.ToString(),
                power = card.Power ?? 0,
                life = card.Life ?? 0,
                costList = new List<CostJsonEntry>(),
                keywords = card.Keywords != null ? new List<string>(card.Keywords) : new List<string>(),
                tags = card.Tags != null ? new List<string>(card.Tags) : new List<string>(),
                effects = card.Effects != null ? new List<CardEffectData>(card.Effects) : new List<CardEffectData>(),
                subtype = card.Subtype == CardSubtype.None ? "" : card.Subtype.ToString(),
                level = card.Level ?? -1,
                rank = card.Rank ?? -1,
                linkRating = card.LinkRating ?? -1,
                arrows = card.ArrowDirections.ToString(),
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
