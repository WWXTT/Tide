using System;
using System.Collections.Generic;
using UnityEngine;
using CardCore.Attribute; // For EffectTargetType

namespace CardCore
{
    /// <summary>
    /// 价值系统运行时配置 - 临时实现
    /// 用于卡牌和效果价值计算
    /// </summary>
    [Serializable]
    public class ValueSystemRuntimeConfig
    {
        public TargetModifierConfig TargetModifierConfig = new TargetModifierConfig();
        public TimingModifierConfig TimingModifierConfig = new TimingModifierConfig();
        public CostValueConfig CostValueConfig = new CostValueConfig();
        public EffectValueConfig EffectValueConfig = new EffectValueConfig();
        public TriggerValueConfig TriggerValueConfig = new TriggerValueConfig();
        public SynergyConfig SynergyConfig = new SynergyConfig();
        public AttributeValueConfig AttributeValueConfig = new AttributeValueConfig();
        public CardTypeValueConfig CardTypeValueConfig = new CardTypeValueConfig();
    }

    /// <summary>
    /// 目标修正配置
    /// </summary>
    [Serializable]
    public class TargetModifierConfig
    {
        public float SelfModifier = 0.8f;
        public float SingleTargetModifier = 1.0f;
        public float AllEnemiesModifier = 1.5f;
        public float AllAlliesModifier = 1.3f;
        public float AllModifier = 1.8f;
        public float RandomModifier = 0.9f;

        public float GetTargetModifier(EffectTargetType targetType)
        {
            return targetType switch
            {
                EffectTargetType.Self => SelfModifier,
                EffectTargetType.Target => SingleTargetModifier,
                EffectTargetType.AllEnemies => AllEnemiesModifier,
                EffectTargetType.AllAllies => AllAlliesModifier,
                EffectTargetType.All => AllModifier,
                EffectTargetType.Random => RandomModifier,
                _ => 1.0f
            };
        }

        public float GetMultiTargetModifier(int targetCount)
        {
            if (targetCount <= 1) return 1.0f;
            // 多目标线性递增，但有上限
            return Mathf.Min(1.0f + (targetCount - 1) * 0.1f, 2.0f);
        }
    }

    /// <summary>
    /// 时机修正配置
    /// </summary>
    [Serializable]
    public class TimingModifierConfig
    {
        public float InstantModifier = 1.0f;
        public float SorcerySpeedModifier = 0.9f;
        public float TriggeredModifier = 0.8f;
        public float PassiveModifier = 0.7f;

        public float GetTimingModifier(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.Activate_Instant => InstantModifier,
                TriggerTiming.Activate_Active => SorcerySpeedModifier,
                _ => TriggeredModifier
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
    /// 代价价值配置
    /// </summary>
    [Serializable]
    public class CostValueConfig
    {
        public float ManaValueCoefficient = 1.0f;
        public float LifeValueCoefficient = 2.0f;
        public float TapValueCoefficient = 0.3f;
        public float SacrificeValueCoefficient = 1.5f;
        public float DiscardValueCoefficient = 1.2f;

    }

    /// <summary>
    /// 效果价值配置
    /// </summary>
    [Serializable]
    public class EffectValueConfig
    {
        public float BaseDamageValue = 1.0f;
        public float BaseHealValue = 0.8f;
        public float BaseDrawValue = 1.5f;
        public float BaseDestroyValue = 2.0f;

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
    /// 触发价值配置
    /// </summary>
    [Serializable]
    public class TriggerValueConfig
    {
        public float UnlimitedFrequency = 1.0f;
        public float OncePerTurnFrequency = 0.8f;
        public float OncePerGameFrequency = 0.6f;

        public float GetTriggerValue(TriggerTiming timing, TriggerFrequency frequency)
        {
            float timingValue = timing switch
            {
                TriggerTiming.On_AttackDeclare => 0.9f,
                TriggerTiming.On_Death => 0.7f,
                TriggerTiming.On_TurnStart => 0.8f,
                TriggerTiming.On_TurnEnd => 0.7f,
                _ => 0.8f
            };

            float frequencyValue = frequency switch
            {
                TriggerFrequency.Unlimited => UnlimitedFrequency,
                TriggerFrequency.OncePerTurn => OncePerTurnFrequency,
                TriggerFrequency.OncePerGame => OncePerGameFrequency,
                _ => UnlimitedFrequency
            };

            return timingValue * frequencyValue;
        }
    }

    /// <summary>
    /// 协同效应配置
    /// </summary>
    [Serializable]
    public class SynergyConfig
    {
        public int DiscountStartsAt = 3;
        public float SameTypeDiscountRate = 0.1f;
        public float MaxDiscount = 0.5f;

        public float CalculateSameTypeDiscount(int count, float totalValue)
        {
            if (count < DiscountStartsAt) return totalValue;

            int discountCount = count - DiscountStartsAt + 1;
            float discount = Mathf.Min(discountCount * SameTypeDiscountRate, MaxDiscount);
            return totalValue * (1 - discount);
        }

        public float CalculateSynergyBonus(HashSet<AtomicEffectType> effectTypes, float currentValue)
        {
            // 不同类型效果的协同加成
            int typeCount = effectTypes.Count;
            if (typeCount < 2) return currentValue;

            // 每多一种类型，加成5%
            float bonus = 1.0f + (typeCount - 1) * 0.05f;
            return currentValue * bonus;
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

        public float CalculateStatValue(int power, int life, bool isPermanent = false)
        {
            float value = power * PowerValue + life * LifeValue;
            if (isPermanent)
            {
                value *= PermanentBonus;
            }
            return value;
        }

        public float GetDurationDiscount(DurationType duration)
        {
            return duration switch
            {
                DurationType.Once => 0.5f,
                DurationType.Permanent => 1.0f,
                DurationType.UntilEndOfTurn => 0.6f,
                DurationType.UntilLeaveBattlefield => 0.8f,
                DurationType.WhileCondition => 0.75f,
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
    /// 价值系统配置管理器 - 单例
    /// </summary>
    public class ValueSystemConfigManager
    {
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
                _config = new ValueSystemRuntimeConfig();
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
    }
}
