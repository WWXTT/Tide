using System;
using System.Collections.Generic;
using System.IO;
using CardCore.Attribute; // AtomicEffectTable

namespace CardCore
{
    /// <summary>
    /// 价值系统运行时配置
    /// 用于卡牌和效果价值计算。
    /// 各子配置的字段名 = ValueSystemConfig.json 的 Key（Category+\"Config\" 对应同类字段），
    /// 表值由 ValueSystemConfigManager 按名反射灌入；字段初始化器仅为表缺失时的代码兜底。
    /// </summary>
    [Serializable]
    public class ValueSystemRuntimeConfig
    {
        public AttributeValueConfig AttributeValueConfig = new AttributeValueConfig();
        public CardCostConfig CardCostConfig = new CardCostConfig();
        public DelayDiscountConfig DelayDiscountConfig = new DelayDiscountConfig();
        public SummonDropConfig SummonDropConfig = new SummonDropConfig();
        public CardCompositionConfig CardCompositionConfig = new CardCompositionConfig();
        public PricingTierConfig PricingTierConfig = new PricingTierConfig();
    }

    /// <summary>
    /// 衍生物落区系数（表 Category=SummonDrop）——SummonToken 原子按落区分档计价：
    /// 落手牌=即时弹性资源（溢价）；落牌组=检索稀释后延迟可得（微溢价）；落战场=基准。
    /// 取值 = 实例级 atom.ZoneParam（注意 Zone.Hand==0：合成界面必须显式填落区）。
    /// </summary>
    [Serializable]
    public class SummonDropConfig
    {
        public float BattlefieldFactor = 1.0f;
        public float HandFactor = 1.2f;
        public float DeckFactor = 1.1f;

        public float GetFactor(Zone zone)
        {
            switch (zone)
            {
                case Zone.Hand: return HandFactor;
                case Zone.Deck: return DeckFactor;
                default: return BattlefieldFactor;
            }
        }
    }

    // 注：TargetModifier（按目标范围/AOE 的计价乘数）已删除——计价只看目标数量（固定 N ×N，见
    // CostDerivationService.EffectiveTargetCountForCost）；范围/Scope 仅是目标选取元数据，不参与计价。
    // 注：Synergy（同类递减罚重复/多样协同奖多样）已删除——单卡计价由规则一（CardCostService）完整承担。

    /// <summary>
    /// 卡层组合费用配置（表 Category=CardComposition）——「效果→卡」组合层的额外费用参数。
    /// 与原子层（CostDerivation：原子→效果锚价）、规则一（CardCostService.Derive）正交：
    /// 本层只对「卡的结构性弹性」收税，独立演进。
    /// </summary>
    [Serializable]
    public class CardCompositionConfig
    {
        /// <summary>底盘预算（2026-09-10 攻/守效果化）：免费额度覆盖 攻+守+1 效果槽（默认 3）。</summary>
        public float ChassisBudget = 3f;
        /// <summary>底盘单项费率：攻/守/每个效果槽各占 1。</summary>
        public float ChassisItemRate = 1f;
    }

    /// <summary>
    /// 计价档位系数（表 Category=PricingTier，2026-10-05 用户定案）——目标数量/作用次数的增量系数统一：
    /// 数量 1:1 / 2:1.5 / 3:2 / 全部:3；次数 1:1 / 2:1.5 / 3:2 / 无上限:4。
    /// 消费方 CostDerivation（QuantityFactor / TriggerCostFactor）；原 整数×N/期望4/1.2 连乘 口径退役。
    /// </summary>
    [Serializable]
    public class PricingTierConfig
    {
        public float TargetCount1 = 1.0f;        // 目标数量 1：基准
        public float TargetCount2 = 1.5f;        // 目标数量 2
        public float TargetCount3 = 2.0f;        // 目标数量 3
        public float TargetCountAll = 3.0f;      // 目标数量 全部（0=域内全取；-1 任意同此档）
        public float TriggerLimit1 = 1.0f;       // 作用次数 1：基准（未声明 0 亦按此）
        public float TriggerLimit2 = 1.5f;       // 作用次数 2
        public float TriggerLimit3 = 2.0f;       // 作用次数 3
        public float TriggerLimitInfinite = 4.0f; // 作用次数 无上限（-1；N>3 视同此档）

        /// <summary>目标数量→计价系数。越界口径：N&gt;3 视同全部；-1（任意·运行时自选）同全部；-2/其他未声明=单目标基准。</summary>
        public float TargetCountFactor(int count)
        {
            switch (count)
            {
                case 1: return TargetCount1;
                case 2: return TargetCount2;
                case 3: return TargetCount3;
                case 0: return TargetCountAll;
                case -1: return TargetCountAll;
                default: return count > 3 ? TargetCountAll : TargetCount1;
            }
        }

        /// <summary>作用次数→计价系数。N&gt;3 视同无上限；0（未声明=一回合一次）=基准。</summary>
        public float TriggerLimitFactor(int limit)
        {
            switch (limit)
            {
                case 1: return TriggerLimit1;
                case 2: return TriggerLimit2;
                case 3: return TriggerLimit3;
                case -1: return TriggerLimitInfinite;
                default: return limit > 3 ? TriggerLimitInfinite : TriggerLimit1;
            }
        }
    }

    // 注：TimingModifier / CostValue / EffectValue / TriggerValue / CardTypeValue 五类配置
    // 已于 2026-10-05 删除——全仓无消费方（时机轴未接入计价、EffectValue 委托原子表后无人调用、
    // CardTypeValue 对应的卡型价值未使用）；ValueSystemConfig.json 同批清理 33 行死行。

    /// <summary>
    /// 属性价值配置
    /// </summary>
    [Serializable]
    public class AttributeValueConfig
    {
        // ==== 持续时间折扣（时间换费用：持续越短越便宜，曲线为凹函数——每多 1 回合的增量递减）====
        // 用法为「相对折扣」：CostDerivation 按 D(实际持续)/D(表内默认) 计价，
        // 保证既有锚点（1 伤害 = 1 元素，伤害原子表默认 Once）不因接入而漂移。
        public float OnceDiscount = 0.5f;                  // 瞬发（本回合内即时结算）
        public float UntilEndOfTurnDiscount = 0.6f;        // 到持有者回合结束（限时指示物统一档，2026-09-16 两档合一）
        public float UntilNextTurnDiscount = 0.6f;         // ≡UntilEndOfTurn（2026-09-16 统一档：运行时语义与费用均按持有者回合结束=1回合档）
        public float WhileConditionDiscount = 0.75f;       // 条件满足期间
        public float UntilLeaveBattlefieldDiscount = 0.8f; // 到离场（挂在实体上，实体亡即失效）
        public float PermanentDiscount = 1.0f;             // 本局有效（永续档，复利载体）
        // ForTurns(N)：N≤1 对齐 UntilEndOfTurn；N=2 起按 Base + Step×(N-2)，Cap 封顶（< Permanent）
        public float ForTurnsBase = 0.7f;                  // N=2 基准（与 UntilNextTurn 对齐）
        public float ForTurnsStep = 0.04f;                 // 每多 1 回合的增量（< 相邻锚点差 → 凹）
        public float ForTurnsCap = 0.9f;                   // 渐近上限

        /// <summary>
        /// 持续时间折扣（绝对系数）。2026-09-14 收缩：效果级 DurationValue 删除（ForTurns 档
        /// 收缩后 ≡ 专档同义词），turns 参数退役——ForTurns 仅存于指示物时钟（CounterSpec.Turns）。
        /// 费用侧请用相对折扣：D(实际)/D(表内默认)，见 CostDerivationService.ComputeAtomCost。
        /// </summary>
        public float GetDurationDiscount(DurationType duration)
        {
            return duration switch
            {
                DurationType.Once => OnceDiscount,
                DurationType.Permanent => PermanentDiscount,
                DurationType.UntilEndOfTurn => UntilEndOfTurnDiscount,
                DurationType.UntilNextTurn => UntilNextTurnDiscount,
                DurationType.UntilLeaveBattlefield => UntilLeaveBattlefieldDiscount,
                DurationType.WhileCondition => WhileConditionDiscount,
                DurationType.ForTurns => UntilNextTurnDiscount, // 效果级已不可声明；兜底按 2 回合档
                _ => 0.8f
            };
        }
    }

    /// <summary>
    /// 卡牌计价配置（表 Category=CardCost，ValueSystemConfig.json）——规则一·平衡的统一推导参数。
    /// 代价当量表已删（2026-09-14 代价原子化）：弃牌/送墓/流失等资源支付的补偿一律按
    /// Payload 原子全价（原子表唯一锚），不再有第二套当量。
    /// </summary>
    [Serializable]
    public class CardCostConfig
    {
        public float StatUnit = 2f;                     // 1费=StatUnit点属性（攻血各 1/StatUnit 元素，灰）
        public bool KeywordsShareDelayDiscount = true;  // 关键词是否同享挂载折扣（关键词=Grant原子=挂载效果）
        public int MaxTier = 9;                         // 档位上限（=地牌槽曲线上限）
        /// <summary>属性锚：+1 攻/+1 生命 = 0.5（攻血同锚；2026-10-05 自 CostDerivation 常量迁表）。</summary>
        public float StatAnchor = 0.5f;
        /// <summary>修改族持续档乘数：换区移除/条件持续 = 锚×3（=1.5/+1）；连接光环档同此乘数。</summary>
        public float StatSustainMultiplier = 3f;
        /// <summary>修改族永久档乘数：换区不移除 = 锚×4（=2.0/+1）。</summary>
        public float StatPermanentMultiplier = 4f;
        /// <summary>改写族恒价：Set*/费用永久直改 = 3.0/+1。</summary>
        public float StatRewriteFlatCost = 3f;
        /// <summary>双方同时作用减半系数（双侧全取/规则光环 ×0.5；2026-10-05 迁表）。</summary>
        public float SymmetricDiscountFactor = 0.5f;
        /// <summary>耐久单价：0.5 灰/点（2026-10-05 定案——耐久体随身材费计价）。</summary>
        public float DurabilityUnitCost = 0.5f;
    }

    /// <summary>
    /// 挂载延迟折扣配置（表 Category=DelayDiscount；2026-09-07 第三层重定案）。
    /// d(C)：费用 C=最早第 C 回合落地（地牌曲线 [1..9] 锁定）→ 按落地延迟对**整卡**
    /// （S+E+K+卡层调整）在组合完成后**最后一步**打折。d(1)=全价，线性降到 d(9)=0.75，
    /// **9 费及以上钳在 0.75**（最大折 25%——原 d(9)=0「9费挂载全免」已废）。
    /// 三类卡型统一适用（2026-10-02 定案：法术不再豁免，旧"法术不折 f≡1"口径已废）。
    /// （原 ExtraActivationSlope「选发每多1回合发动的额外折」已删——与卡层挂载口计价重复。）
    /// </summary>
    [Serializable]
    public class DelayDiscountConfig
    {
        public float C1 = 1.00000f;   // d(1)=全价（几乎即时）
        public float C2 = 0.96875f;
        public float C3 = 0.93750f;
        public float C4 = 0.90625f;
        public float C5 = 0.87500f;   // d(5)=八七五折
        public float C6 = 0.84375f;
        public float C7 = 0.81250f;
        public float C8 = 0.78125f;
        public float C9 = 0.75000f;   // d(9)=0.75（最大折 25%；9费及以上钳此值）

        public float At(int tier)
        {
            return tier switch
            {
                1 => C1, 2 => C2, 3 => C3, 4 => C4, 5 => C5,
                6 => C6, 7 => C7, 8 => C8, _ => C9 // 9 及以上钳 C9
            };
        }
    }

    /// <summary>
    /// 价值系统配置管理器 - 单例。
    /// 首次取用时从 Assets/Configs/ValueSystemConfig.json（ValueSystem.xlsm 导出）灌入配置，
    /// 文件缺失/解析失败则退回 ValueSystemRuntimeConfig 字段初始化器的代码默认值。
    /// </summary>
    public class ValueSystemConfigManager
    {
        // 相对 Application.dataPath 的配置路径（该目录不是 Resources，必须用 System.IO 读取）
        private const string ConfigRelativePath = "Configs/ValueSystemConfig.json";

        private static ValueSystemConfigManager _instance;
        public static ValueSystemConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ValueSystemConfigManager();
                }
                return _instance;
            }
        }

        private ValueSystemRuntimeConfig _config;
        private int _configVersion = 1;

        public int ConfigVersion => _configVersion;

        public ValueSystemRuntimeConfig GetOrCreateConfig()
        {
            if (_config == null)
            {
                _config = LoadFromJson() ?? new ValueSystemRuntimeConfig();
            }
            return _config;
        }

        public void SetConfig(ValueSystemRuntimeConfig config)
        {
            _config = config;
            _configVersion++;
        }

        public void InvalidateConfig()
        {
            _configVersion++;
        }

        // ======================================== 表装载 ========================================

        /// <summary>
        /// 读表并灌入配置，返回 null 表示放弃（调用方退回代码默认值）。
        /// 映射纯按名：Category+"Config" → ValueSystemRuntimeConfig 同名字段，Key → 子配置同名字段——
        /// 表里加行、代码加同名字段即自动接通，不维护第二份映射。未知 Category/Key 告警跳过（暴露表结构漂移）。
        /// </summary>
        private static ValueSystemRuntimeConfig LoadFromJson()
        {
            string path = Path.Combine(TidePaths.DataPath, ConfigRelativePath);
            if (!File.Exists(path))
            {
                TideLog.Warn($"[ValueSystemConfigManager] 配置文件不存在: {path}，使用代码默认值");
                return null;
            }

            var config = new ValueSystemRuntimeConfig(); // 字段初始化器 = 兜底默认值，表值逐条覆盖
            int applied = ApplyEntries(path, config);

            // CostOffsetConfig.json 已删（2026-09-14 代价原子化）：CardCost 三行（StatUnit/
            // KeywordsShareDelayDiscount/MaxTier）已回迁本表；机制行与当量行随抵消系统退役。
            if (applied == 0)
                TideLog.Warn($"[ValueSystemConfigManager] 未从 {ConfigRelativePath} 灌入任何条目");
            return config;
        }

        /// <summary>读单文件 KV 条目并反射灌入（Category+"Config" → 同名字段；表加行+代码加同名字段即接通）。</summary>
        private static int ApplyEntries(string path, ValueSystemRuntimeConfig config)
        {
            List<ValueSystemConfigEntry> entries;
            try
            {
                entries = ParseEntries(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                TideLog.Warn($"[ValueSystemConfigManager] 加载 {path} 失败: {e.Message}，跳过该文件");
                return 0;
            }
            if (entries == null) return 0;

            int applied = 0;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Category) || string.IsNullOrEmpty(entry.Key))
                    continue;

                var section = typeof(ValueSystemRuntimeConfig).GetField(entry.Category + "Config");
                if (section == null)
                {
                    TideLog.Warn($"[ValueSystemConfigManager] 未知 Category='{entry.Category}'（Key={entry.Key}），已跳过");
                    continue;
                }

                var field = section.FieldType.GetField(entry.Key);
                if (field == null)
                {
                    TideLog.Warn($"[ValueSystemConfigManager] {entry.Category} 无同名字段 '{entry.Key}'，已跳过");
                    continue;
                }

                field.SetValue(section.GetValue(config), Convert.ChangeType(entry.Value, field.FieldType));
                applied++;
            }
            return applied;
        }

        /// <summary>解析 JSON（导出器每个 sheet 产出顶层裸数组，JsonUtility 需包一层；同 AtomicEffectTable.ParseEntries 惯例）</summary>
        private static List<ValueSystemConfigEntry> ParseEntries(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string trimmed = raw.TrimStart();
            string wrapped = trimmed.StartsWith("[")
                ? "{\"items\":" + raw + "}"
                : raw; // 已是对象（含 items）则直接用
                    var wrapper = TideJson.FromJson<ValueSystemConfigWrapper>(wrapped);
            return wrapper?.items;
        }

        // ======================================== JSON DTO ========================================

        [Serializable]
        private class ValueSystemConfigEntry
        {
            public string Category; // TargetModifier / TimingModifier / CostValue / ...（+Config = 同名字段）
            public string Key;      // 子配置同名字段名
            public float Value;     // 灌入值（int 字段自动转换）
        }

        [Serializable]
        private class ValueSystemConfigWrapper
        {
            public List<ValueSystemConfigEntry> items;
        }
    }
}
