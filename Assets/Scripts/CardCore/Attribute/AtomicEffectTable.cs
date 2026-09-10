using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CardCore.Attribute
{
    /// <summary>
    /// 原子效果属性表
    /// 运行时唯一真相源：从 Assets/Configs/AttributeValueConfig.json（由 Attribute.xlsm 导出）加载完整配置，
    /// 含展示/费用与 targeting/持续/发动，全部由配置驱动。
    /// </summary>
    public static class AtomicEffectTable
    {
        // 相对 Application.dataPath 的配置路径（该目录不是 Resources，必须用 System.IO 读取）
        private const string ConfigRelativePath = "Configs/AttributeValueConfig.json";

        private static Dictionary<int, AtomicEffectConfig> _idMap;
        private static Dictionary<string, AtomicEffectConfig> _enumNameMap;
        private static Dictionary<AtomicEffectType, AtomicEffectConfig> _typeMap;

        static AtomicEffectTable()
        {
            Initialize();
        }

        private static void Initialize()
        {
            _idMap = new Dictionary<int, AtomicEffectConfig>();
            _enumNameMap = new Dictionary<string, AtomicEffectConfig>();
            _typeMap = new Dictionary<AtomicEffectType, AtomicEffectConfig>();

            int loaded = 0;
            try
            {
                loaded = LoadFromJson();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AtomicEffectTable] 加载 {ConfigRelativePath} 失败: {e.Message}");
            }

            if (loaded == 0)
                Debug.LogWarning($"[AtomicEffectTable] 未从 JSON 加载到任何条目（配置缺失或解析失败）");
        }

        /// <summary>从 JSON 薄配置加载并与引擎默认值合并，返回成功合入的条目数</summary>
        private static int LoadFromJson()
        {
            string path = Path.Combine(Application.dataPath, ConfigRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[AtomicEffectTable] 配置文件不存在: {path}");
                return 0;
            }

            string raw = File.ReadAllText(path);
            var entries = ParseEntries(raw);
            if (entries == null) return 0;

            int count = 0;
            int nextId = 1;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.EffectType)) continue;
                if (!Enum.TryParse<AtomicEffectType>(entry.EffectType, out var type))
                {
                    Debug.LogWarning($"[AtomicEffectTable] 无法解析 EffectType='{entry.EffectType}'（EnumName={entry.EnumName}），已跳过");
                    continue;
                }

                AddConfig(BuildConfig(nextId++, type, entry));
                count++;
            }
            return count;
        }

        /// <summary>由 JSON 薄配置（含 targeting）构建完整 AtomicEffectConfig；字段解析失败用安全兜底</summary>
        private static AtomicEffectConfig BuildConfig(int id, AtomicEffectType type, AttributeValueConfigEntry entry)
        {
            var config = new AtomicEffectConfig
            {
                Id = id,
                EnumName = type.ToString(),                 // 英文枚举名 → AddConfig 据此填 _typeMap
                CostMultiplier = 1.0f,
                Stackable = true,
                Priority = 50,
                // ---- 安全兜底（仅当 entry 缺失或字段解析失败时生效）----
                TargetKinds = "0,1",                        // 双方有生命单位（最宽域）
                TargetFilter = null,                        // 无过滤（旧常量 "Creature" 已废：token 改名 NoRole，
                                                              // 死 token 顶替 null 会让「review 定案 filter=(空)」失真）
            };

            if (entry != null)
            {
                // EnumName(中文短名) → DisplayName；DisplayName(模板) → Description
                config.DisplayName = entry.EnumName;
                config.Description = entry.DisplayName;
                config.BaseCost = entry.BaseCost;
                config.Tags = string.IsNullOrEmpty(entry.EffectFunction)
                    ? entry.EffectColor
                    : (string.IsNullOrEmpty(entry.EffectColor) ? entry.EffectFunction : entry.EffectFunction + "," + entry.EffectColor);

                // targeting / 发动 / 三分类：配置驱动，解析失败保留上面的兜底。
                // TargetKinds 列即真相：行内显式空（null/""）= 真无域（守卫/跳回合类被动，
                // 不落 "0,1" 宽域兜底——兜底仅在整行缺失 entry==null 时生效）
                config.TargetKinds = entry.TargetKinds;
                if (!string.IsNullOrEmpty(entry.TargetFilter)) config.TargetFilter = entry.TargetFilter;
                config.Polarity = UnityEngine.Mathf.Clamp(entry.Polarity, -1f, 1f);
            }
            else
            {
                config.DisplayName = type.ToString();
                config.Description = "";
                config.BaseCost = 1.0f;
                config.Tags = "";
            }

            return config;
        }

        /// <summary>解析 JSON（顶层为裸数组，JsonUtility 需包一层）</summary>
        private static List<AttributeValueConfigEntry> ParseEntries(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string trimmed = raw.TrimStart();
            string wrapped = trimmed.StartsWith("[")
                ? "{\"items\":" + raw + "}"
                : raw; // 已是对象（含 items）则直接用
            var wrapper = JsonUtility.FromJson<AttributeValueConfigWrapper>(wrapped);
            return wrapper?.items;
        }

        private static void AddConfig(AtomicEffectConfig config)
        {
            _idMap[config.Id] = config;
            _enumNameMap[config.EnumName] = config;
            if (Enum.TryParse<AtomicEffectType>(config.EnumName, out var type))
                _typeMap[type] = config;
        }

        /// <summary>通过 AtomicEffectType 获取配置</summary>
        public static AtomicEffectConfig GetByType(AtomicEffectType type)
        {
            return _typeMap.TryGetValue(type, out var config) ? config : null;
        }

        /// <summary>通过英文枚举名获取配置</summary>
        public static AtomicEffectConfig GetByEnumName(string enumName)
        {
            return _enumNameMap.TryGetValue(enumName, out var config) ? config : null;
        }

        /// <summary>遍历所有已加载配置（EnumName 为英文枚举名；供关键词目录等派生用）</summary>
        public static IEnumerable<AtomicEffectConfig> GetAll()
        {
            return _typeMap.Values;
        }

        /// <summary>已加载的配置总数（供诊断/验证用）</summary>
        public static int Count => _typeMap?.Count ?? 0;

        // ======================================== JSON DTO（薄 5+1 列）========================================

        [Serializable]
        private class AttributeValueConfigEntry
        {
            public string EnumName;       // 中文短名（造成伤害）
            public string DisplayName;    // 展示模板（对{target}造成{value}点伤害）
            public string EffectFunction; // Damage / Movement / Status / Protection / Counter
            public string EffectColor;    // Red / Blue / Green ...
            public float BaseCost;
            public string EffectType;     // 英文枚举名（DealDamage）→ AtomicEffectType
            public string EffectTier;     // Atom / Keyword / Counter（三分类，缺省 Atom）

            // ---- targeting / 发动（2026-09-10 目标域模型：TargetKinds+SelectionMode 取代 TargetType/Scope；持续已上移组合层）----
            public string TargetKinds;    // 逗号分隔 TargetKind 序号（空 = 无目标原子）
            public string TargetFilter;   // 逗号分隔属性 token（Creature/Player/Untapped/...）
            public float Polarity;        // 极性 [-1,1]：-1=对对手释放有益 / +1=对己方释放有益 / 0=中性
        }

        [Serializable]
        private class AttributeValueConfigWrapper
        {
            public List<AttributeValueConfigEntry> items;
        }
    }
}
