using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace CardCore.Attribute
{
    /// <summary>
    /// 原子效果属性表
    /// 运行时唯一真相源：从 Assets/Configs/AttributeValueConfig.json（由 Attribute.xlsm 导出）加载完整配置，
    /// 含展示/费用与 targeting/持续/发动，全部由配置驱动。
    /// </summary>
    public static class AtomicEffectTable
    {
        // 配置寻址（2026-09-30 合并定案）：共享源统一走 TidePaths.ReadConfigText（相对 dataPath 路径）。
        // 服务器/编辑器=文件直读（直改 JSON 后 Reload 免重启）；真机由宿主装配 YooAsset 热更包
        // （HotUpdateEntry 装钩子，地址=文件名 AddressByFileName）。
        private const string ConfigRelativePath = "Configs/AttributeValueConfig.json";

        private static Dictionary<int, AtomicEffectConfig> _idMap;
        private static Dictionary<string, AtomicEffectConfig> _hashIdMap;
        private static Dictionary<string, AtomicEffectConfig> _enumNameMap;
        private static Dictionary<AtomicEffectType, AtomicEffectConfig> _typeMap;

        // 保序全行（2026-10-06 原子表工坊）：_idMap.Values 是字典序（加行不删行时恰为插入序，
        // 但不保证）；工坊 UI 逐行展示需要确定顺序，装载序即表书写序。
        private static List<AtomicEffectConfig> _orderedRows;

        /// <summary>表内容代际（2026-10-06 原子表工坊）：Reload 重建或 TrySetRowTotal 改行成功各 +1。
        /// 消费方（overlay 重放、缓存失效判断）凭代际判断「我上次看到的表现在还作不作数」。</summary>
        public static int Version { get; private set; }

        static AtomicEffectTable()
        {
            Initialize();
        }

        private static void Initialize()
        {
            _idMap = new Dictionary<int, AtomicEffectConfig>();
            _hashIdMap = new Dictionary<string, AtomicEffectConfig>();
            _enumNameMap = new Dictionary<string, AtomicEffectConfig>();
            _typeMap = new Dictionary<AtomicEffectType, AtomicEffectConfig>();
            _orderedRows = new List<AtomicEffectConfig>();
            Version++;

            int loaded = 0;
            try
            {
                loaded = LoadFromJson();
            }
            catch (Exception e)
            {
                TideLog.Warn($"[AtomicEffectTable] 加载 {ConfigRelativePath} 失败: {e.Message}");
            }

            if (loaded == 0)
                TideLog.Warn($"[AtomicEffectTable] 未从 JSON 加载到任何条目（配置缺失或解析失败）");
        }

        /// <summary>强制重读表（2026-09-14 合成器「读取原子表」按钮——外部直改 JSON 后免重启刷新；
        /// 重读重建三张映射，运行中已取出的 config 引用不回填）。</summary>
        public static void Reload() => Initialize();

        /// <summary>从 JSON 薄配置加载并与引擎默认值合并，返回成功合入的条目数</summary>
        private static int LoadFromJson()
        {
            string raw = TidePaths.ReadConfigText(ConfigRelativePath);
            if (string.IsNullOrEmpty(raw))
            {
                TideLog.Warn($"[AtomicEffectTable] 配置不可读：{ConfigRelativePath}");
                return 0;
            }
            var entries = ParseEntries(raw);
            if (entries == null) return 0;

            int count = 0;
            int nextId = 1;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.EffectType)) continue;
                if (!Enum.TryParse<AtomicEffectType>(entry.EffectType, out var type))
                {
                    TideLog.Warn($"[AtomicEffectTable] 无法解析 EffectType='{entry.EffectType}'（EnumName={entry.EnumName}），已跳过");
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
                TargetKinds = "己方单位,对方单位",           // 双方有生命单位（最宽域）
                TargetFilter = null,                        // 无过滤（旧常量 "Creature" 已废：token 改名 NoRole，
                                                              // 死 token 顶替 null 会让「review 定案 filter=(空)」失真）
            };

            if (entry != null)
            {
                // 表首列 ID（8-hex）：原子引用键（EffectSlim.AtomRef.refId）——空列不可被引用
                if (!string.IsNullOrEmpty(entry.ID))
                {
                    config.HashId = entry.ID;
                }
                // EnumName(中文短名) → DisplayName；DisplayName(模板) → Description
                config.DisplayName = entry.EnumName;
                config.Description = entry.DisplayName;
                // 费用构成（2026-09-14 EffectColor+BaseCost 两列合并定案；2026-10-04 位置数组迁移完成）：
                // 下标 = ManaType 枚举序号 [灰,红,蓝,绿,白,黑]；旧两列与兜底合成（2f0fcf4 解析器先迁、
                // 数据未迁的过渡态）已随全表数据迁移删除。null/空 = 不计价行。
                config.ManaList = ParsePositionalMana(entry.ManaList);

                // 表 Tags 列（2026-10-06 产出族迁表 + 中文化）先入，表色随后追加（逗号连接）——
                // GetTagList() 两侧消费：ComposerCatalog.OutcomeConditionsFor 族匹配（伤害产出族 等）、
                // ElementAffinities.GetAffinityForEffect 色名匹配（中文色名，族标签与色名不冲突）。
                config.Tags = entry.Tags;

                // 表色回填：ElementAffinities.GetAffinityForEffect 读 Tags 取色——
                // 费用构成首色（首个非零下标）即表色（混合色原子取序数序首个非零色）。
                if (config.ManaList != null && !config.ManaList.IsZero)
                {
                    var color = ManaTypeNames.ZhNameOf(config.ManaList.PrimaryColor);
                    config.Tags = string.IsNullOrEmpty(config.Tags) ? color : config.Tags + "," + color;
                }

                // targeting / 发动 / 三分类：配置驱动，解析失败保留上面的兜底。
                // TargetKinds 列即真相：行内显式空（null/""）= 真无域（守卫/跳回合类被动，
                // 不落 "0,1" 宽域兜底——兜底仅在整行缺失 entry==null 时生效）
                config.TargetKinds = entry.TargetKinds;
                if (!string.IsNullOrEmpty(entry.TargetFilter)) config.TargetFilter = entry.TargetFilter;
                config.Polarity = Math.Clamp(entry.Polarity, -1f, 1f);

                // 可装载范围（2026-09-11；2026-10-06 中文化）：空 = 未声明 → 兜底 "不限"（向后兼容存量行，逐步收紧）
                config.MountKinds = string.IsNullOrEmpty(entry.MountKinds) ? "主动,关键词,指示物,分支奖励,随机幅度,上限锁定,连接光环" : entry.MountKinds;
            }
            else
            {
                config.DisplayName = type.ToString();
                config.Description = "";
                config.ManaList = ElementCost.FromValue(ManaType.Gray, 1f);
            }

            return config;
        }

        /// <summary>位置数组解析（2026-10-04）：null/空 → null（不计价行）；短数组补零、
        /// 长数组截断并告警（长度 = ManaType 枚举成员数，扩色自动跟随）。</summary>
        private static ElementCost ParsePositionalMana(List<float> list)
        {
            if (list == null || list.Count == 0) return null;
            if (list.Count > ElementCost.Length)
                TideLog.Warn($"[AtomicEffectTable] ManaList 长度 {list.Count} 超过元素色数 {ElementCost.Length}，超出部分截断");
            var arr = new float[Math.Min(list.Count, ElementCost.Length)];
            for (int i = 0; i < arr.Length; i++) arr[i] = list[i];
            return new ElementCost(arr);
        }

        /// <summary>解析 JSON（顶层为裸数组，JsonUtility 需包一层）</summary>
        private static List<AttributeValueConfigEntry> ParseEntries(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string trimmed = raw.TrimStart();
            string wrapped = trimmed.StartsWith("[")
                ? "{\"items\":" + raw + "}"
                : raw; // 已是对象（含 items）则直接用
            var wrapper = TideJson.FromJson<AttributeValueConfigWrapper>(wrapped);
            return wrapper?.items;
        }

        private static void AddConfig(AtomicEffectConfig config)
        {
            _idMap[config.Id] = config;
            if (!string.IsNullOrEmpty(config.HashId))
                _hashIdMap[config.HashId] = config;
            _enumNameMap[config.EnumName] = config;
            if (Enum.TryParse<AtomicEffectType>(config.EnumName, out var type))
                _typeMap[type] = config;
            _orderedRows.Add(config);
        }

        /// <summary>通过表 ID 列（8-hex）获取配置（原子引用键——无则 null）。</summary>
        public static AtomicEffectConfig GetByHashId(string hashId)
        {
            if (string.IsNullOrEmpty(hashId)) return null;
            return _hashIdMap.TryGetValue(hashId, out var cfg) ? cfg : null;
        }

        /// <summary>
        /// 反查行 ID（EffectSlim.ToRef 用）：EffectType + 实例域 → 表 ID 列 8-hex。
        /// 规则：实例域非空且与某行表级域**集合相等**且唯一 → 该行；否则取该类型**末次注册行**
        /// （与 GetByType 覆写语义一致——同枚举多行时运行时读的就是末行）。无行返回 null。
        /// </summary>
        public static string ResolveRowId(string effectTypeName, List<int> instanceKinds)
        {
            AtomicEffectConfig last = null;
            AtomicEffectConfig unique = null;
            int uniqueCount = 0;
            foreach (var cfg in _idMap.Values)
            {
                if (cfg == null || cfg.EnumName != effectTypeName || string.IsNullOrEmpty(cfg.HashId)) continue;
                last = cfg;
                if (instanceKinds != null && instanceKinds.Count > 0)
                {
                    var tableKinds = cfg.GetTargetKindList();
                    if (tableKinds.Count == instanceKinds.Count && !tableKinds.Except(instanceKinds).Any())
                    {
                        unique = cfg;
                        uniqueCount++;
                    }
                }
            }
            if (uniqueCount == 1) return unique.HashId;
            return last?.HashId;
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

        /// <summary>遍历所有已加载配置行（EnumName 为英文枚举名；供关键词目录等派生用）。
        /// 2026-09-13 修正：改返回 _idMap.Values（全行）——此前返回 _typeMap.Values，
        /// 同 EffectType 多行（效果/指示物分离，如 Sleep 沉睡/苏醒）只暴露覆盖后的末行，
        /// 消费方（关键词目录反查、验证器表行锚）拿不到首行。</summary>
        public static IEnumerable<AtomicEffectConfig> GetAll()
        {
            return _idMap.Values;
        }

        /// <summary>已加载的配置总数（供诊断/验证用）</summary>
        public static int Count => _typeMap?.Count ?? 0;

        // ======================================== 原子表工坊（2026-10-06 玩家改价） ========================================

        /// <summary>玩家可编辑的总价下界（0.5 步进网格起点）。0 = 白嫖，禁设。</summary>
        public const float MinEditableTotal = 0.5f;

        /// <summary>玩家可编辑的总价上界（0.5 步进网格终点）。9 = 顶格档，禁设。</summary>
        public const float MaxEditableTotal = 8.5f;

        /// <summary>全行保序枚举（表书写序；工坊 UI 逐行展示用）。</summary>
        public static IReadOnlyList<AtomicEffectConfig> OrderedRows => _orderedRows;

        /// <summary>
        /// 工坊编辑合法性（UI 预检与写入共用同一口径，禁双轨）：
        /// 总价落在 [0.5, 8.5] 且为 0.5 整倍数——0（白嫖）与 9（顶格档）永远设不进。
        /// </summary>
        public static bool IsEditableTotal(float total)
        {
            if (total < MinEditableTotal || total > MaxEditableTotal) return false;
            return Math.Abs(total * 2f - Math.Round(total * 2f)) < 1e-3f;
        }

        /// <summary>
        /// 行级总价改写（原子表工坊）：按 hashId 定位行，把 ManaList 整只替换为
        /// 「原六色占比 × 新总价」的等比分摊——占比不动 ⇒ 表色/Tags/Polarity/HashId 全不变，
        /// 转换器实例快照（Polarity/RowHashId）与 ResolveRowId 反查全部不过期；
        /// 四张字典持同一对象引用，改字段即全映射生效。
        /// 不计价行（ManaList=null）与非法值拒绝并返回 false。成功后 Version+1。
        /// </summary>
        public static bool TrySetRowTotal(string hashId, float total)
        {
            if (string.IsNullOrEmpty(hashId) || !IsEditableTotal(total)) return false;
            if (!_hashIdMap.TryGetValue(hashId, out var row) || row == null) return false;
            var old = row.ManaList;
            if (old == null || old.IsZero || old.Total <= 0f) return false; // 不计价行不可编辑

            var scale = total / old.Total;
            var arr = new float[ElementCost.Length];
            for (int i = 0; i < ElementCost.Length && i < old.v.Length; i++)
                arr[i] = old.v[i] * scale;
            row.ManaList = new ElementCost(arr);
            Version++;
            return true;
        }

        // ======================================== JSON DTO（薄 5+1 列）========================================

        [Serializable]
        private class AttributeValueConfigEntry
        {
            public string EnumName;       // 中文短名（造成伤害）
            public string DisplayName;    // 展示模板（对{target}造成{value}点伤害）

            // 费用构成（2026-09-14 两列合并定案；2026-10-04 位置数组迁移完成）：
            // 下标 = ManaType 枚举序号 [灰,红,蓝,绿,白,黑]，长度 = 枚举成员数（扩色自动加长）；
            // null/空 = 不计价行。旧 EffectColor/BaseCost 两列与死列 EffectTier 已随数据迁移删除。
            public List<float> ManaList;
            public string EffectType;     // 英文枚举名（DealDamage）→ AtomicEffectType

            // ---- targeting / 发动（2026-09-10 目标域模型：TargetKinds+SelectionMode 取代 TargetType/Scope；持续已上移组合层）----
            public string TargetKinds;    // 逗号分隔 TargetKind 中文名（2026-10-06 中文化，解析双轨兼容序号；空 = 无目标原子）
            public string TargetFilter;   // 逗号分隔属性 token（Creature/Player/Untapped/...）
            public float Polarity;        // 极性 [-1,1]：-1=对对手释放有益 / +1=对己方释放有益 / 0=中性

            // ---- 可装载范围（2026-09-11 定案；2026-10-06 中文化）：主动效果/关键词/指示物/分支位置/赋予目标，显性化 ----
            public string MountKinds;     // 逗号分隔 MountKind 中文名（解析双轨兼容序号；空 = 未声明，消费方兜底=不限）
            public string Tags;           // 逗号分隔中文标签（2026-10-06 产出族迁表中文化：伤害产出族/诅咒产出族 等；
                                          // 表色由 BuildConfig 在标签后追加中文色名，ElementAffinity 扫标签取色）
        public string ID;             // 表首列：8 位 hex 描述哈希（原子引用键——EffectSlim.AtomRef.refId）
        }

        [Serializable]
        private class AttributeValueConfigWrapper
        {
            public List<AttributeValueConfigEntry> items;
        }
    }
}
