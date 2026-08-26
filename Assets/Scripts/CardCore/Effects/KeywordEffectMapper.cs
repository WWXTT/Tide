using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 关键词效果映射器
    /// 将关键词 ID 转换为可执行的运行时效果。
    /// 数据唯一真相源：Assets/Configs/KeywordsConfig.json（经 CardLoader.LoadKeywords 加载）。
    ///
    /// 关键词分两类：
    /// 1. 被动标记型（Passive）- 写入 IHasKeywords，由游戏系统查询
    /// 2. 触发效果型（Triggered）- 生成 EffectDefinition 注册到 TriggerEngine
    /// 触发式由配置的 triggerTiming（非空）判定。
    /// </summary>
    public static class KeywordEffectMapper
    {
        private static Dictionary<string, KeywordDefinition> Defs => CardLoader.LoadKeywords();

        /// <summary>
        /// 获取关键词分类（triggerTiming 非空即为触发式）
        /// </summary>
        public static KeywordCategory GetCategory(string keywordId)
        {
            return Defs.TryGetValue(keywordId, out var d) && !string.IsNullOrEmpty(d.triggerTiming)
                ? KeywordCategory.Triggered
                : KeywordCategory.Passive;
        }

        /// <summary>
        /// 获取关键词对应的 Grant 原子效果类型
        /// </summary>
        public static AtomicEffectType? GetGrantEffectType(string keywordId)
        {
            if (Defs.TryGetValue(keywordId, out var d)
                && !string.IsNullOrEmpty(d.atomicEffect)
                && Enum.TryParse<AtomicEffectType>(d.atomicEffect, out var type))
                return type;
            return null;
        }

        /// <summary>
        /// 为触发式关键词生成 EffectDefinition
        /// 返回 null 表示被动标记型，不需要 EffectDefinition
        /// </summary>
        public static EffectDefinition CreateTriggeredEffect(string keywordId, string sourceCardId)
        {
            if (!Defs.TryGetValue(keywordId, out var d))
                return null;
            if (string.IsNullOrEmpty(d.triggerTiming) || string.IsNullOrEmpty(d.triggeredEffect))
                return null;
            if (!Enum.TryParse<TriggerTiming>(d.triggerTiming, out var timing))
                return null;
            if (!Enum.TryParse<AtomicEffectType>(d.triggeredEffect, out var atomicType))
                return null;

            Enum.TryParse<DurationType>(d.duration, out var duration);

            return new EffectDefinition
            {
                Id = $"KW_{keywordId}_{sourceCardId}",
                DisplayName = d.nameZh,
                Description = d.description,
                TriggerTiming = timing,
                ActivationType = EffectActivationType.Automatic,
                IsOptional = false,
                Duration = DurationType.Permanent,
                SourceCardId = sourceCardId,
                Effects = new List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance
                    {
                        Type = atomicType,
                        Value = d.value,
                        Value2 = d.value2,
                        Duration = duration
                    }
                }
            };
        }

        /// <summary>
        /// 批量为卡牌生成所有触发式关键词的 EffectDefinition
        /// </summary>
        public static List<EffectDefinition> CreateAllTriggeredEffects(Card card)
        {
            var results = new List<EffectDefinition>();

            if (!(card is IHasKeywords hasKw))
                return results;

            var cardId = card.ID;
            foreach (var keywordId in hasKw.Keywords)
            {
                var effectDef = CreateTriggeredEffect(keywordId, cardId);
                if (effectDef != null)
                    results.Add(effectDef);
            }

            return results;
        }

        /// <summary>
        /// 获取关键词中文名
        /// </summary>
        public static string GetNameZh(string keywordId)
        {
            return Defs.TryGetValue(keywordId, out var d) ? d.nameZh : keywordId;
        }

        /// <summary>
        /// 获取关键词描述
        /// </summary>
        public static string GetDescription(string keywordId)
        {
            return Defs.TryGetValue(keywordId, out var d) ? d.description : "";
        }

        /// <summary>
        /// 是否为已知关键词
        /// </summary>
        public static bool IsKnownKeyword(string keywordId)
        {
            return Defs.ContainsKey(keywordId);
        }

        /// <summary>
        /// 获取所有已定义的关键词ID
        /// </summary>
        public static IEnumerable<string> GetAllKeywordIds()
        {
            return Defs.Keys;
        }
    }

    /// <summary>
    /// 关键词分类
    /// </summary>
    public enum KeywordCategory
    {
        /// <summary>被动标记型 - 由游戏系统查询</summary>
        Passive,
        /// <summary>触发效果型 - 需注册到 TriggerEngine</summary>
        Triggered
    }
}
