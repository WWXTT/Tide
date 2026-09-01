using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// 分支配置注册表。运行时唯一真相源：从 Assets/Configs/BranchConfig.json 加载三族条件目录
    /// （OutcomeGate / Drawback / FilterPrecision）+ 减费缺陷 + 检索维度系数。增删条件不改代码。
    ///
    /// 与代码的同步契约（加载自检强制）：
    /// - effects 里每个 effectType 必须存在于 AtomicEffectTable 且属于 OutcomeProducerTypes（产出族）；
    /// - 每个 OutcomeGate 条件 id 必须被 BranchConditionEvaluator 识别（目录与评估器 switch 是唯一同步点）。
    /// </summary>
    public static class BranchConfigTable
    {
        private const string ConfigRelativePath = "Configs/BranchConfig.json";

        /// <summary>
        /// 产出族清单（谁能写 LastOutcome / 延迟产出验证结果）——「可挂分支」的唯一判定。
        /// 规则：结算产出可观察、可失败、可量化的原子才可挂 OutcomeGate 分支
        /// （伤害族/治疗族/信息族）；改值/改费/授予系/移动系无可串联条件（设计文稿明文）。
        /// handler 新增产出记录时须同步此表（自检会拦目录侧的越界）。
        /// </summary>
        public static readonly HashSet<string> OutcomeProducerTypes = new HashSet<string>
        {
            // 伤害族
            "DealDamage", "DamageCannotBePrevented", "DrainLife", "PoisonousDamage",
            // 治疗族
            "Heal", "RestoreToFullLife",
            // 信息族：宣言（即时验证写 DeclareHit）
            "DeclareHand", "DeclareHandSampled", "DeclareDeckTop", "DeclareArrow",
            // 信息族：预言（延迟验证——产出在验证时刻由 ProphecySystem 结算）
            "ProphecyNextCard",
        };

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
            else
                ValidateAgainstCode();
        }

        /// <summary>
        /// 加载自检：目录 ↔ 代码同步契约。不一致 LogError（可见但不炸——目录错项仍可用，
        /// 只是组装 UI 会看不到/评估器判 false），验证器 TestBranchCatalog 断言同一契约。
        /// </summary>
        private static void ValidateAgainstCode()
        {
            foreach (var config in _idMap.Values)
            {
                if (CardCore.Attribute.AtomicEffectTable.GetByEnumName(config.EffectTypeName) == null)
                {
                    Debug.LogError($"[BranchConfig] 目录项 '{config.EffectTypeName}' 不在 AtomicEffectTable 中（EffectType 拼写错误或表缺行）");
                    continue;
                }

                if (!OutcomeProducerTypes.Contains(config.EffectTypeName))
                {
                    Debug.LogError($"[BranchConfig] 目录项 '{config.EffectTypeName}' 不属于产出族（OutcomeProducerTypes）——" +
                                   "改值/改费/授予系/移动系无可串联条件，不应出现在分支目录中");
                }

                if (config.Conditions == null || config.Conditions.Count == 0)
                {
                    Debug.LogError($"[BranchConfig] 目录项 '{config.EffectTypeName}' 无任何条件（应从产出族目录移除或补条件）");
                    continue;
                }

                foreach (var cond in config.Conditions)
                {
                    if (cond.Kind == BranchConditionKind.OutcomeGate
                        && !BranchConditionEvaluator.IsKnownCondition(cond.Id))
                    {
                        Debug.LogError($"[BranchConfig] 条件 '{cond.Id}'（挂在 {config.EffectTypeName}）不被 BranchConditionEvaluator 识别——" +
                                       "目录与评估器 switch 是唯一同步点，两侧须一起改");
                    }
                }
            }
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
