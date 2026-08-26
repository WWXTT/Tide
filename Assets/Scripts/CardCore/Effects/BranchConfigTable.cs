using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// 分支配置注册表。运行时唯一真相源：从 Assets/Configs/BranchConfig.json 加载三族条件目录
    /// （OutcomeGate / Drawback / FilterPrecision）+ 减费缺陷 + 检索维度系数。增删条件不改代码。
    /// </summary>
    public static class BranchConfigTable
    {
        private const string ConfigRelativePath = "Configs/BranchConfig.json";

        private static Dictionary<string, BranchConfig> _idMap;
        private static Dictionary<string, BranchConfig> _effectTypeMap;
        private static Dictionary<string, BranchCondition> _drawbackMap;
        private static List<BranchFilterTier> _filterTiers;

        static BranchConfigTable()
        {
            Initialize();
        }

        private static void Initialize()
        {
            _idMap = new Dictionary<string, BranchConfig>();
            _effectTypeMap = new Dictionary<string, BranchConfig>();
            _drawbackMap = new Dictionary<string, BranchCondition>();
            _filterTiers = new List<BranchFilterTier>();

            int loaded = 0;
            try
            {
                loaded = LoadFromJson();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BranchConfigTable] 加载 {ConfigRelativePath} 失败: {e.Message}");
            }

            if (loaded == 0)
                Debug.LogWarning($"[BranchConfigTable] 未从 JSON 加载到任何条目（配置缺失或解析失败）");
        }

        private static int LoadFromJson()
        {
            string path = Path.Combine(Application.dataPath, ConfigRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[BranchConfigTable] 配置文件不存在: {path}");
                return 0;
            }

            string raw = File.ReadAllText(path);
            var root = JsonUtility.FromJson<BranchConfigRoot>(raw);
            if (root == null) return 0;

            int count = 0;
            if (root.effects != null)
            {
                foreach (var effect in root.effects)
                {
                    if (effect == null || string.IsNullOrEmpty(effect.effectType)) continue;
                    AddConfig(BuildConfig(effect));
                    count++;
                }
            }

            if (root.drawbacks != null)
            {
                foreach (var d in root.drawbacks)
                {
                    if (d == null || string.IsNullOrEmpty(d.id)) continue;
                    _drawbackMap[d.id] = new BranchCondition
                    {
                        Id = d.id,
                        Kind = BranchConditionKind.Drawback,
                        DisplayName = d.displayName,
                        Description = d.description,
                        CostReduction = d.costReduction,
                    };
                }
            }

            if (root.filterPrecisionTiers != null)
            {
                foreach (var t in root.filterPrecisionTiers)
                {
                    if (t == null || string.IsNullOrEmpty(t.id)) continue;
                    _filterTiers.Add(new BranchFilterTier
                    {
                        Id = t.id,
                        DisplayName = t.displayName,
                        Cost = t.cost,
                    });
                }
            }

            return count;
        }

        private static BranchConfig BuildConfig(BranchEffectEntry entry)
        {
            var config = new BranchConfig
            {
                Id = entry.effectType + "Branch",
                EffectTypeName = entry.effectType,
                DisplayName = entry.displayName,
                MaxChainedEffects = entry.maxChainedEffects > 0 ? entry.maxChainedEffects : 2,
                AllowNesting = entry.allowNesting,
                Conditions = new List<BranchCondition>(),
            };

            if (entry.conditions != null)
            {
                foreach (var c in entry.conditions)
                {
                    if (c == null || string.IsNullOrEmpty(c.id)) continue;
                    var kind = BranchConditionKind.OutcomeGate;
                    if (!string.IsNullOrEmpty(c.kind))
                        Enum.TryParse<BranchConditionKind>(c.kind, out kind);

                    config.Conditions.Add(new BranchCondition
                    {
                        Id = c.id,
                        Kind = kind,
                        DisplayName = c.displayName,
                        Description = c.description,
                        Param = c.param,
                        StringParam = c.stringParam,
                        CostReduction = c.costReduction,
                    });
                }
            }

            return config;
        }

        private static void AddConfig(BranchConfig config)
        {
            _idMap[config.Id] = config;
            _effectTypeMap[config.EffectTypeName] = config;
        }

        /// <summary>通过ID获取分支配置</summary>
        public static BranchConfig GetById(string id)
        {
            return _idMap.TryGetValue(id, out var config) ? config : null;
        }

        /// <summary>通过原子效果枚举名获取分支配置（无可串联条件的效果返回 null）</summary>
        public static BranchConfig GetByEffectType(string effectTypeName)
        {
            return _effectTypeMap.TryGetValue(effectTypeName, out var config) ? config : null;
        }

        /// <summary>获取所有分支配置</summary>
        public static IEnumerable<BranchConfig> GetAll()
        {
            return _idMap.Values;
        }

        /// <summary>获取抽牌减费缺陷定义</summary>
        public static BranchCondition GetDrawback(string id)
        {
            return _drawbackMap.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>获取全部抽牌减费缺陷</summary>
        public static IEnumerable<BranchCondition> GetAllDrawbacks()
        {
            return _drawbackMap.Values;
        }

        /// <summary>获取检索筛选维度档</summary>
        public static BranchFilterTier GetFilterTier(string id)
        {
            foreach (var t in _filterTiers)
                if (t.Id == id) return t;
            return null;
        }

        /// <summary>获取全部检索筛选维度档</summary>
        public static IReadOnlyList<BranchFilterTier> GetFilterTiers()
        {
            return _filterTiers;
        }

        // ======================================== JSON DTO ========================================

        [Serializable]
        private class BranchConfigRoot
        {
            public List<BranchEffectEntry> effects;
            public List<DrawbackEntry> drawbacks;
            public List<FilterTierEntry> filterPrecisionTiers;
        }

        [Serializable]
        private class BranchEffectEntry
        {
            public string effectType;
            public string displayName;
            public int maxChainedEffects;
            public bool allowNesting;
            public List<ConditionEntry> conditions;
        }

        [Serializable]
        private class ConditionEntry
        {
            public string id;
            public string kind;
            public string displayName;
            public string description;
            public int param;
            public string stringParam;
            public int costReduction;
        }

        [Serializable]
        private class DrawbackEntry
        {
            public string id;
            public string displayName;
            public string description;
            public int costReduction;
        }

        [Serializable]
        private class FilterTierEntry
        {
            public string id;
            public string displayName;
            public int cost;
        }
    }
}
