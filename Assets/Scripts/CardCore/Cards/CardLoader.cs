using System;
using System.Collections.Generic;
using UnityEngine;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;
using System.Linq;

namespace CardCore
{
    // ======================================== JSON 数据结构 ========================================

    /// <summary>
    /// 关键词定义（运行时由原子效果表的 Grant* 条目合成，见 CardLoader.LoadKeywords）
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

        // 连接光环声明（三轨制 2026-09-09）：箭头指向格占据者享受的持续效果（stat/keyword 二选一）
        public List<LinkAuraData> linkAuras;
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

        /// <summary>
        /// 加载关键词定义 —— 关键词本身即原子效果（GrantXxx，作用默认指向自身），不单独开表：
        /// 直接从原子效果表（AtomicEffectTable ← AttributeValueConfig.json）的 Grant* 条目合成。
        /// 中文名/描述/颜色取自表内字段；id 取 GrantKeywordHandlerFactory 登记的运行时关键词 id
        /// （与写入 IHasKeywords 的字符串同源，如 GrantReborn → Reborn），未登记退化为去 Grant 前缀。
        /// 触发式关键词（triggerTiming）表内暂无来源，统一按被动处理。
        /// （2026-09-08 冲锋/突袭已去关键词化：GrantHaste/GrantRush 删除，改由登场效果表达。）
        /// </summary>
        public static Dictionary<string, KeywordDefinition> LoadKeywords()
        {
            if (_keywordCache != null) return _keywordCache;

            _keywordCache = new Dictionary<string, KeywordDefinition>();
            foreach (var atom in AtomicEffectTable.GetAll())
            {
                // EnumName = 英文枚举名（GrantXxx）；Grant 前缀 = 可作为关键词授予的原子
                if (atom == null || string.IsNullOrEmpty(atom.EnumName) || !atom.EnumName.StartsWith("Grant"))
                    continue;
                if (!Enum.TryParse<AtomicEffectType>(atom.EnumName, out var type))
                    continue;

                if (!GrantKeywordHandlerFactory.TryGetKeywordId(type, out var keywordId))
                    keywordId = atom.EnumName.Substring("Grant".Length);

                _keywordCache[keywordId] = new KeywordDefinition
                {
                    id = keywordId,
                    nameZh = atom.DisplayName,      // 中文短名（冲锋/突袭…）
                    nameEn = atom.EnumName,
                    color = ElementAffinities.GetAffinityForEffect(type).PrimaryColor.ToString(), // 源自表 EffectColor
                    description = atom.Description, // 展示模板（{target}获得冲锋）
                    isPassive = true,
                    atomicEffect = atom.EnumName,   // 被动授予的原子效果（GrantXxx）
                };
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

            WarnCostNonConformance(result);

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

            WarnCostNonConformance(result);

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
        /// 构建测试卡组（每张卡 copiesPerCard 份）。
        /// 构筑规则（定案）：玩家卡组内卡牌不重复——每张卡是效果 ID 的唯一索引，
        /// 仅效果产生的衍生物可重复。copiesPerCard 默认 1；>1 仅测试脚手架用。
        /// </summary>
        public static List<Card> BuildTestDeck(string jsonPath, int copiesPerCard = 1)
        {
            var cardsData = LoadCards(jsonPath);
            return BuildDeck(cardsData, copiesPerCard);
        }

        /// <summary>
        /// 从文本构建测试卡组（用于测试；copiesPerCard 默认 1，见构筑规则定案）
        /// </summary>
        public static List<Card> BuildTestDeckFromText(string jsonText, int copiesPerCard = 1)
        {
            var cardsData = LoadCardsFromText(jsonText);
            return BuildDeck(cardsData, copiesPerCard);
        }

        /// <summary>
        /// 从 CardData 列表构建卡组（卡组不重复：copiesPerCard=1 为规则默认）
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

            // 连接光环声明（三轨制）：无效条目（stat/keyword 双空）装载期即丢弃
            if (entry.linkAuras != null)
                cardData.LinkAuras = entry.linkAuras
                    .Where(a => a != null && (!string.IsNullOrEmpty(a.stat) || !string.IsNullOrEmpty(a.keyword)))
                    .ToList();

            // 统一计价兜底：costList 缺省 → 写入建议档位分布（幂等，非空不动）
            CardCostService.EnsureCost(cardData);

            return cardData;
        }

        /// <summary>
        /// 装载期构筑校验（提示级）：声明档位的卡若代价抵扣不足（O &lt; Req）打警告，不阻止加载。
        /// </summary>
        private static void WarnCostNonConformance(List<CardData> cards)
        {
            foreach (var card in cards)
            {
                if (card?.Cost == null || card.Cost.Count == 0) continue;
                var r = CardCostService.Derive(card);
                if (!r.Conformant)
                    Debug.LogWarning($"[CardCost] {card.ID}({card.CardName}) 代价抵扣不足：需求 Req={r.OffsetRequirement}，已提供 O={r.OffsetProvided}，不符规则一");
            }
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
