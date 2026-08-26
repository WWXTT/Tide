using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CardCore
{
    // ======================================== JSON 数据结构 ========================================

    /// <summary>
    /// 关键词定义
    /// </summary>
    [Serializable]
    public class KeywordDefinition
    {
        public string id;
        public string nameZh;
        public string nameEn;
        public string color;
        public string description;
        public bool isPassive;
        public string atomicEffect;     // 被动授予的关键词原子效果（GrantXxx）
        public string triggerTiming;    // 触发式：TriggerTiming 枚举名，空=被动
        public string triggeredEffect;  // 触发时执行的原子效果（区别于 atomicEffect 的授予）
        public int value;               // 触发效果数值
        public int value2;              // 触发效果第二数值
        public string duration;         // DurationType 枚举名
    }

    /// <summary>
    /// 关键词配置包装（JsonUtility 需要顶层对象）
    /// </summary>
    [Serializable]
    public class KeywordsConfigWrapper
    {
        public List<KeywordDefinition> keywords;
    }

    /// <summary>
    /// 卡牌配置条目（JSON 中单张卡的数据）
    /// </summary>
    [Serializable]
    public class CardConfigEntry
    {
        public string id;
        public string cardName;
        public string supertype;
        public int power;
        public int life;
        public List<CostJsonEntry> costList;
        public List<string> keywords;
        public List<string> tags;
        public List<CardEffectData> effects;

        // 额外卡组 / 子类型字段（缺省 → 旧行为，向后兼容）
        public string subtype;     // 逗号分隔的 CardSubtype Flags 名，如 "Synchro,Tuner"
        public int level = -1;     // 等级，-1 = 无
        public int rank = -1;      // 阶级，-1 = 无
        public int linkRating = -1;// 链接值，-1 = 无
        public string arrows;      // 逗号分隔的 HexDirection Flags 名，如 "Up,LowerRight"
    }

    /// <summary>
    /// 费用 JSON 条目
    /// </summary>
    [Serializable]
    public class CostJsonEntry
    {
        public int manaType;
        public float amount;
    }

    /// <summary>
    /// 卡组配置
    /// </summary>
    [Serializable]
    public class DeckConfig
    {
        public int copiesPerCard = 3;
    }

    /// <summary>
    /// 测试卡牌配置包装
    /// </summary>
    [Serializable]
    public class TestCardsConfigWrapper
    {
        public List<CardConfigEntry> cards;
        public DeckConfig deckConfig;
    }

    // ======================================== 卡牌加载器 ========================================

    /// <summary>
    /// 卡牌加载器 - 从 JSON 配置构建 CardData 和测试卡组
    /// </summary>
    public static class CardLoader
    {
        private static Dictionary<string, KeywordDefinition> _keywordCache;

        // 相对 Application.dataPath 的关键词配置路径（非 Resources 目录，需用 System.IO 读取）
        private const string KeywordConfigRelativePath = "Configs/KeywordsConfig.json";

        /// <summary>
        /// 加载关键词配置（从 Assets/Configs/KeywordsConfig.json）
        /// </summary>
        public static Dictionary<string, KeywordDefinition> LoadKeywords()
        {
            if (_keywordCache != null) return _keywordCache;

            _keywordCache = new Dictionary<string, KeywordDefinition>();
            string path = Path.Combine(Application.dataPath, KeywordConfigRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CardLoader] 关键词配置未找到: {path}");
                return _keywordCache;
            }

            var wrapper = JsonUtility.FromJson<KeywordsConfigWrapper>(File.ReadAllText(path));
            if (wrapper?.keywords == null) return _keywordCache;

            foreach (var kw in wrapper.keywords)
            {
                if (kw != null && !string.IsNullOrEmpty(kw.id))
                    _keywordCache[kw.id] = kw;
            }
            return _keywordCache;
        }

        /// <summary>
        /// 获取已加载的关键词定义
        /// </summary>
        public static KeywordDefinition GetKeywordDefinition(string keywordId)
        {
            if (_keywordCache != null && _keywordCache.TryGetValue(keywordId, out var def))
                return def;
            return null;
        }

        /// <summary>
        /// 从 JSON 加载测试卡牌列表
        /// </summary>
        public static List<CardData> LoadCards(string jsonPath)
        {
            var json = Resources.Load<TextAsset>(jsonPath);
            if (json == null)
            {
                Debug.LogWarning($"[CardLoader] 卡牌配置未找到: {jsonPath}");
                return new List<CardData>();
            }

            var wrapper = JsonUtility.FromJson<TestCardsConfigWrapper>(json.text);
            var result = new List<CardData>();

            foreach (var entry in wrapper.cards)
            {
                var cardData = CreateCardData(entry);
                result.Add(cardData);
            }

            return result;
        }

        /// <summary>
        /// 直接从 JSON 文本加载卡牌（用于测试，不依赖 Resources）
        /// </summary>
        public static List<CardData> LoadCardsFromText(string jsonText)
        {
            var wrapper = JsonUtility.FromJson<TestCardsConfigWrapper>(jsonText);
            var result = new List<CardData>();

            foreach (var entry in wrapper.cards)
            {
                var cardData = CreateCardData(entry);
                result.Add(cardData);
            }

            return result;
        }

        /// <summary>
        /// 将 CardData 列表转换为 CardWrapper 列表
        /// </summary>
        public static List<Card> ToCardInstances(List<CardData> cardsData)
        {
            var result = new List<Card>();
            foreach (var data in cardsData)
            {
                result.Add(new CardWrapper(data));
            }
            return result;
        }

        /// <summary>
        /// 构建测试卡组（每张卡 copiesPerCard 份）
        /// </summary>
        public static List<Card> BuildTestDeck(string jsonPath, int copiesPerCard = 3)
        {
            var cardsData = LoadCards(jsonPath);
            return BuildDeck(cardsData, copiesPerCard);
        }

        /// <summary>
        /// 从文本构建测试卡组（用于测试）
        /// </summary>
        public static List<Card> BuildTestDeckFromText(string jsonText, int copiesPerCard = 3)
        {
            var cardsData = LoadCardsFromText(jsonText);
            return BuildDeck(cardsData, copiesPerCard);
        }

        /// <summary>
        /// 从 CardData 列表构建卡组
        /// </summary>
        public static List<Card> BuildDeck(List<CardData> cardsData, int copiesPerCard)
        {
            var deck = new List<Card>();
            foreach (var data in cardsData)
            {
                for (int i = 0; i < copiesPerCard; i++)
                {
                    deck.Add(new CardWrapper(data));
                }
            }
            return deck;
        }

        // ======================================== 内部方法 ========================================

        /// <summary>
        /// 从配置条目创建 CardData
        /// </summary>
        private static CardData CreateCardData(CardConfigEntry entry)
        {
            var cardData = new CardData
            {
                ID = entry.id,
                CardName = entry.cardName,
                Supertype = ParseCardtype(entry.supertype),
                Power = entry.power,
                Life = entry.life,
                Cost = ParseCost(entry.costList),
                Keywords = entry.keywords ?? new List<string>(),
                Tags = entry.tags ?? new List<string>(),
                Effects = entry.effects ?? new List<CardEffectData>(),
                Subtype = ParseFlags<CardSubtype>(entry.subtype),
                ArrowDirections = ParseFlags<HexDirection>(entry.arrows),
            };

            if (entry.level >= 0) cardData.Level = entry.level;
            if (entry.rank >= 0) cardData.Rank = entry.rank;
            if (entry.linkRating >= 0) cardData.LinkRating = entry.linkRating;

            return cardData;
        }

        /// <summary>
        /// 解析逗号分隔的 Flags 枚举串（缺省 → 默认值 0）
        /// </summary>
        private static T ParseFlags<T>(string raw) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(raw))
                return default;
            if (Enum.TryParse<T>(raw, out var result))
                return result;
            return default;
        }

        /// <summary>
        /// 解析卡牌类型
        /// </summary>
        private static Cardtype ParseCardtype(string typeStr)
        {
            if (Enum.TryParse<Cardtype>(typeStr, out var result))
                return result;
            return Cardtype.Creature;
        }

        /// <summary>
        /// 解析费用列表
        /// </summary>
        private static Dictionary<int, float> ParseCost(List<CostJsonEntry> costList)
        {
            var cost = new Dictionary<int, float>();
            if (costList == null) return cost;
            foreach (var entry in costList)
            {
                cost[entry.manaType] = entry.amount;
            }
            return cost;
        }
    }
}
