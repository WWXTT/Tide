using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CardCore.Attribute; // For EffectTargetType

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
        public TimingModifierConfig TimingModifierConfig = new TimingModifierConfig();
        public CostValueConfig CostValueConfig = new CostValueConfig();
        public EffectValueConfig EffectValueConfig = new EffectValueConfig();
        public TriggerValueConfig TriggerValueConfig = new TriggerValueConfig();
        public AttributeValueConfig AttributeValueConfig = new AttributeValueConfig();
        public CardTypeValueConfig CardTypeValueConfig = new CardTypeValueConfig();
        public CardCostConfig CardCostConfig = new CardCostConfig();
        public DelayDiscountConfig DelayDiscountConfig = new DelayDiscountConfig();
        public SummonDropConfig SummonDropConfig = new SummonDropConfig();
        public CardCompositionConfig CardCompositionConfig = new CardCompositionConfig();
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
        /// <summary>抉择价差溢价步长：可选模式最高/最低费用每差此值 → 整体 +1（相等 +0）。</summary>
        public float ChoiceSpreadStep = 3f;
        /// <summary>抉择价差溢价封顶（相差 Step×Cap 及以上 → 整体 +Cap 封顶）。</summary>
        public float ChoiceSpreadCap = 2f;
        /// <summary>效果挂载口基线：默认每卡含此数量的挂载口（不满退费、超出加价）。</summary>
        public float MountBaseline = 2f;
        /// <summary>每超出 1 口挂载的加价（例：1-1 挂三效果 → 原有基础 +1）。</summary>
        public float MountExtraRate = 1f;
        /// <summary>每空置 1 口挂载的退费（例：2-2 白板两口全空 → −2，恰为 0 费）。</summary>
        public float MountUnusedRate = 1f;
    }

    /// <summary>
    /// 时机修正配置（表 Category=TimingModifier）
    /// </summary>
    [Serializable]
    public class TimingModifierConfig
    {
        public float Instant = 1.0f;
        public float SorcerySpeed = 0.9f;
        public float Triggered = 0.8f;
        public float Passive = 0.7f;

        public float GetTimingModifier(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.Activate_Instant => Instant,
                TriggerTiming.Activate_Active => SorcerySpeed,
                _ => Triggered
            };
        }

        public float GetConditionModifier(List<string> conditions)
        {
            if (conditions == null || conditions.Count == 0) return 1.0f;
            // 每个条件减少5%价值（条件越严格，价值越低）
            return Mathf.Max(0.5f, 1.0f - conditions.Count * 0.05f);
        }

        public float GetCombinedModifier(float timing, float condition, float trigger)
        {
            return timing * condition * trigger;
        }
    }

    /// <summary>
    /// 代价价值配置（表 Category=CostValue）
    /// </summary>
    [Serializable]
    public class CostValueConfig
    {
        public float Mana = 1.0f;
        public float Life = 2.0f;
        public float Tap = 0.3f;
        public float Sacrifice = 1.5f;
        public float Discard = 1.2f;

    }

    /// <summary>
    /// 效果价值配置（表 Category=EffectValue）。
    /// 注意：这几项是占位——原子基础价值实际委托原子表 BaseCost（见 GetAtomicEffectBaseValue），
    /// 保留字段仅为对齐表结构与后续非委托场景。
    /// </summary>
    [Serializable]
    public class EffectValueConfig
    {
        public float BaseDamage = 1.0f;
        public float BaseHeal = 0.8f;
        public float BaseDraw = 1.5f;
        public float BaseDestroy = 2.0f;

        public float GetAtomicEffectBaseValue(AtomicEffectType type, int value = 1, bool applyValue = true)
        {
            // 基础价值来自配置表（AttributeValueConfig.json 的 BaseCost），不再维护第二份硬编码
            float baseValue = AtomicEffectTable.GetByType(type)?.BaseCost ?? 1.0f;

            if (applyValue && value > 0)
            {
                // 数值型效果：基础价值 * 数值，但有边际递减
                return baseValue * Mathf.Log(1 + value, 2);
            }

            return baseValue;
        }
    }

    /// <summary>
    /// 触发价值配置（表 Category=TriggerValue）
    /// </summary>
    [Serializable]
    public class TriggerValueConfig
    {
        public float FrequencyUnlimited = 1.0f;
        public float FrequencyOncePerTurn = 0.8f;
        public float FrequencyOncePerGame = 0.6f;

        // 时机系数（原为 switch 内硬编码，现已接表，代码值兜底）
        public float TimingOnAttackDeclare = 0.9f;
        public float TimingOnDeath = 0.7f;
        public float TimingOnTurnStart = 0.8f;
        public float TimingOnTurnEnd = 0.7f;

        public float GetTriggerValue(TriggerTiming timing, TriggerFrequency frequency)
        {
            float timingValue = timing switch
            {
                TriggerTiming.OnAttack => TimingOnAttackDeclare,
                TriggerTiming.OnDeath => TimingOnDeath,
                TriggerTiming.OnTurnStart => TimingOnTurnStart,
                TriggerTiming.OnTurnEnd => TimingOnTurnEnd,
                _ => 0.8f
            };

            float frequencyValue = frequency switch
            {
                TriggerFrequency.Unlimited => FrequencyUnlimited,
                TriggerFrequency.OncePerTurn => FrequencyOncePerTurn,
                TriggerFrequency.OncePerGame => FrequencyOncePerGame,
                _ => FrequencyUnlimited
            };

            return timingValue * frequencyValue;
        }
    }

    /// <summary>
    /// 属性价值配置
    /// </summary>
    [Serializable]
    public class AttributeValueConfig
    {
        public float PowerValue = 0.5f;
        public float LifeValue = 0.4f;
        public float PermanentBonus = 1.2f;

        // ==== 持续时间折扣（时间换费用：持续越短越便宜，曲线为凹函数——每多 1 回合的增量递减）====
        // 用法为「相对折扣」：CostDerivation 按 D(实际持续)/D(表内默认) 计价，
        // 保证既有锚点（1 伤害 = 1 元素，伤害原子表默认 Once）不因接入而漂移。
        public float OnceDiscount = 0.5f;                  // 瞬发（本回合内即时结算）
        public float UntilEndOfTurnDiscount = 0.6f;        // 到自己回合结束（快攻攻击 buff 标准档）
        public float UntilNextTurnDiscount = 0.7f;         // 到对手回合结束（防御档：活过对手回合）
        public float WhileConditionDiscount = 0.75f;       // 条件满足期间
        public float UntilLeaveBattlefieldDiscount = 0.8f; // 到离场（挂在实体上，实体亡即失效）
        public float PermanentDiscount = 1.0f;             // 本局有效（永续档，复利载体）
        // ForTurns(N)：N≤1 对齐 UntilEndOfTurn；N=2 起按 Base + Step×(N-2)，Cap 封顶（< Permanent）
        public float ForTurnsBase = 0.7f;                  // N=2 基准（与 UntilNextTurn 对齐）
        public float ForTurnsStep = 0.04f;                 // 每多 1 回合的增量（< 相邻锚点差 → 凹）
        public float ForTurnsCap = 0.9f;                   // 渐近上限

        public float CalculateStatValue(int power, int life, bool isPermanent = false)
        {
            float value = power * PowerValue + life * LifeValue;
            if (isPermanent)
            {
                value *= PermanentBonus;
            }
            return value;
        }

        /// <summary>
        /// 持续时间折扣（绝对系数）。
        /// turns 仅在 duration==ForTurns 时有意义（回合数 N，≤0 按 1 计）。
        /// 费用侧请用相对折扣：D(实际)/D(表内默认)，见 CostDerivationService.ComputeAtomCost。
        /// </summary>
        public float GetDurationDiscount(DurationType duration, int turns = 1)
        {
            return duration switch
            {
                DurationType.Once => OnceDiscount,
                DurationType.Permanent => PermanentDiscount,
                DurationType.UntilEndOfTurn => UntilEndOfTurnDiscount,
                DurationType.UntilNextTurn => UntilNextTurnDiscount,
                DurationType.UntilLeaveBattlefield => UntilLeaveBattlefieldDiscount,
                DurationType.WhileCondition => WhileConditionDiscount,
                DurationType.ForTurns => turns <= 1
                    ? UntilEndOfTurnDiscount
                    : Mathf.Min(ForTurnsBase + ForTurnsStep * (turns - 2), ForTurnsCap),
                _ => 0.8f
            };
        }
    }

    /// <summary>
    /// 卡牌类型价值配置
    /// </summary>
    [Serializable]
    public class CardTypeValueConfig
    {
        public float CreatureBaseValue = 1.0f;
        public float SpellBaseValue = 0.5f;
        public float ArtifactBaseValue = 0.8f;
        public float EnchantmentBaseValue = 0.7f;
        public float LandBaseValue = 0.0f;

        public float ExtraDeckSummonDiscount = 0.7f;
        public float RitualSummonDiscount = 0.8f;
        public float FusionSummonDiscount = 0.75f;

        public float GetSupertypeBaseValue(Cardtype supertype)
        {
            return supertype switch
            {
                Cardtype.Creature => CreatureBaseValue,
                Cardtype.Spell => SpellBaseValue,
                Cardtype.Artifact => ArtifactBaseValue,
                Cardtype.Enchantment => EnchantmentBaseValue,
                Cardtype.Land => LandBaseValue,
                _ => 0.5f
            };
        }

        public float GetSummonMethodDiscount(SummonMethod method)
        {
            return method switch
            {
                SummonMethod.Normal => 1.0f,
                SummonMethod.Ritual => RitualSummonDiscount,
                SummonMethod.Fusion => FusionSummonDiscount,
                SummonMethod.Synchro => ExtraDeckSummonDiscount,
                SummonMethod.Xyz => ExtraDeckSummonDiscount,
                SummonMethod.Link => ExtraDeckSummonDiscount,
                _ => 1.0f
            };
        }
    }

    /// <summary>
    /// 卡牌计价配置（表 Category=CardCost）——规则一·平衡的统一推导参数。
    /// </summary>
    [Serializable]
    public class CardCostConfig
    {
        public float StatUnit = 2f;                     // 1费=StatUnit点属性（攻血各 1/StatUnit 元素，灰）
        public bool KeywordsShareDelayDiscount = true;  // 关键词是否同享挂载折扣（关键词=Grant原子=挂载效果）
        public int MaxTier = 9;                         // 档位上限（=地牌槽曲线上限）

        // ==== 卡上代价条目 → 元素当量（构筑期抵扣换算；默认与 CostOffsetConfig 锚定同源：弃1张/费、2命/费）====
        public float DiscardCardValue = 1.0f;           // 弃 1 张 = 1 元素
        public float LifeValuePerPoint = 0.5f;          // 1 点生命 = 0.5 元素（2命/费）
        public float SleepValuePerTurn = 1.0f;          // 沉睡 1 回合 = 1 元素
        public float SummonMaterialValue = 1.0f;        // 1 个召唤素材 = 1 元素
        public float OpponentDrawValue = 1.0f;          // 对手抽 1 张 = 1 元素（减益型代价，2026-09-07 补）
        public float OpponentHealValuePerPoint = 0.5f;  // 对手回 1 点 = 0.5 元素（2点/费，2026-09-07 补）
        public float SelfSicknessValue = 1.0f;          // 自身紊乱 1 条 = 1 元素（自身减益作代价，2026-09-08 拓展）
        public float OpponentBuffValue = 1.0f;          // 对手 +1/+1 一层 = 1 元素（对手增益作代价，2026-09-08 拓展）
    }

    /// <summary>
    /// 挂载延迟折扣配置（表 Category=DelayDiscount；2026-09-07 第三层重定案）。
    /// d(C)：费用 C=最早第 C 回合落地（地牌曲线 [1..9] 锁定）→ 按落地延迟对**整卡**
    /// （S+E+K+卡层调整）在组合完成后**最后一步**打折。d(1)=全价，线性降到 d(9)=0.75，
    /// **9 费及以上钳在 0.75**（最大折 25%——原 d(9)=0「9费挂载全免」已废）。
    /// 法术不折（打出即生效，f 恒 1）。
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
            string path = Path.Combine(Application.dataPath, ConfigRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ValueSystemConfigManager] 配置文件不存在: {path}，使用代码默认值");
                return null;
            }

            List<ValueSystemConfigEntry> entries;
            try
            {
                entries = ParseEntries(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ValueSystemConfigManager] 加载 {ConfigRelativePath} 失败: {e.Message}，使用代码默认值");
                return null;
            }
            if (entries == null) return null;

            var config = new ValueSystemRuntimeConfig(); // 字段初始化器 = 兜底默认值，表值逐条覆盖
            int applied = 0;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Category) || string.IsNullOrEmpty(entry.Key)) continue;

                var section = typeof(ValueSystemRuntimeConfig).GetField(entry.Category + "Config");
                if (section == null)
                {
                    Debug.LogWarning($"[ValueSystemConfigManager] 未知 Category='{entry.Category}'（Key={entry.Key}），已跳过");
                    continue;
                }

                var field = section.FieldType.GetField(entry.Key);
                if (field == null)
                {
                    Debug.LogWarning($"[ValueSystemConfigManager] {entry.Category} 无同名字段 '{entry.Key}'，已跳过");
                    continue;
                }

                field.SetValue(section.GetValue(config), Convert.ChangeType(entry.Value, field.FieldType));
                applied++;
            }

            if (applied == 0)
                Debug.LogWarning($"[ValueSystemConfigManager] 未从 {ConfigRelativePath} 灌入任何条目");
            return config;
        }

        /// <summary>解析 JSON（导出器每个 sheet 产出顶层裸数组，JsonUtility 需包一层；同 AtomicEffectTable.ParseEntries 惯例）</summary>
        private static List<ValueSystemConfigEntry> ParseEntries(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string trimmed = raw.TrimStart();
            string wrapped = trimmed.StartsWith("[")
                ? "{\"items\":" + raw + "}"
                : raw; // 已是对象（含 items）则直接用
            var wrapper = JsonUtility.FromJson<ValueSystemConfigWrapper>(wrapped);
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
