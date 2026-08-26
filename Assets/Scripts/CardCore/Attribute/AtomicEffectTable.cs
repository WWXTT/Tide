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
                TargetType = EffectTargetType.Target,
                TargetFilter = "Creature",
                TargetCount = 1,
                TargetScope = EffectTargetScope.Single,
                DurationType = EffectDurationType.Instant,
                ActivationType = EffectActivationType.Voluntary,
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

                // targeting / 持续 / 发动：配置驱动，解析失败保留上面的兜底
                if (Enum.TryParse<EffectTargetType>(entry.TargetType, out var tt)) config.TargetType = tt;
                if (!string.IsNullOrEmpty(entry.TargetFilter)) config.TargetFilter = entry.TargetFilter;
                else if (entry.TargetType == "None") config.TargetFilter = "";
                config.TargetCount = entry.TargetCount;
                if (Enum.TryParse<EffectTargetScope>(entry.TargetScope, out var ts)) config.TargetScope = ts;
                if (Enum.TryParse<EffectDurationType>(entry.DurationType, out var dt)) config.DurationType = dt;
                if (Enum.TryParse<EffectActivationType>(entry.ActivationType, out var at)) config.ActivationType = at;
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

        /// <summary>已加载的配置总数（供诊断/验证用）</summary>
        public static int Count => _typeMap?.Count ?? 0;

        // ======================================== JSON DTO（薄 5+1 列）========================================

        [Serializable]
        private class AttributeValueConfigEntry
        {
            public string EnumName;       // 中文短名（造成伤害）
            public string DisplayName;    // 展示模板（对{target}造成{value}点伤害）
            public string EffectFunction; // Damage / Movement / Status / Protection
            public string EffectColor;    // Red / Blue / Green ...
            public float BaseCost;
            public string EffectType;     // 英文枚举名（DealDamage）→ AtomicEffectType

            // ---- targeting / 持续 / 发动（全进 xlsm 后由配置驱动）----
            public string TargetType;     // EffectTargetType 枚举名
            public string TargetFilter;   // 逗号分隔筛选条件
            public int TargetCount;       // 0=全部, -1=任意, >0=指定
            public string TargetScope;    // EffectTargetScope 枚举名
            public string DurationType;   // EffectDurationType 枚举名
            public string ActivationType; // EffectActivationType 枚举名
        }

        [Serializable]
        private class AttributeValueConfigWrapper
        {
            public List<AttributeValueConfigEntry> items;
        }
    }
}
