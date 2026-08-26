using System;
using System.Collections.Generic;
using System.Linq;


namespace CardCore
{
    /// <summary>
    /// 协同效应加成定义
    /// 当卡牌同时拥有特定组合的效果时获得的额外价值
    /// </summary>
    [Serializable]
    public class SynergyBonus
    {
        public string Name;
        public string Description;
        public List<AtomicEffectType> RequiredEffects = new List<AtomicEffectType>();
        public float BonusMultiplier = 1.15f;

        public bool IsSatisfied(HashSet<AtomicEffectType> effectTypes)
        {
            return RequiredEffects.All(e => effectTypes.Contains(e));
        }
    }

    /// <summary>
    /// 协同效应价值配置（运行时）
    /// 管理同类效果递减和组合加成
    /// </summary>
    public class SynergyRuntimeConfig
    {
        // 同类效果递减
        public float SameTypeDiscount { get; set; } = 0.85f;
        public int DiscountStartsAt { get; set; } = 2;

        // 组合加成
        public List<SynergyBonus> SynergyBonuses { get; set; } = new List<SynergyBonus>();

        public SynergyRuntimeConfig()
        {
            InitializeDefaultSynergies();
        }

        /// <summary>
        /// 初始化默认协同效应组合
        /// </summary>
        public void InitializeDefaultSynergies()
        {
            if (SynergyBonuses.Count > 0) return;

            SynergyBonuses.Add(new SynergyBonus
            {
                Name = "爆发组合",
                Description = "同时造成伤害和抽牌",
                RequiredEffects = new List<AtomicEffectType> { AtomicEffectType.DealDamage, AtomicEffectType.DrawCard },
                BonusMultiplier = 1.12f
            });

            SynergyBonuses.Add(new SynergyBonus
            {
                Name = "生存组合",
                Description = "同时具有治疗和防御能力",
                RequiredEffects = new List<AtomicEffectType> { AtomicEffectType.Heal, AtomicEffectType.PreventDamage },
                BonusMultiplier = 1.10f
            });

            SynergyBonuses.Add(new SynergyBonus
            {
                Name = "终结组合",
                Description = "可以摧毁并除外目标",
                RequiredEffects = new List<AtomicEffectType> { AtomicEffectType.Destroy, AtomicEffectType.Exile },
                BonusMultiplier = 1.15f
            });

            SynergyBonuses.Add(new SynergyBonus
            {
                Name = "全能增益",
                Description = "同时增加攻击力和生命值",
                RequiredEffects = new List<AtomicEffectType> { AtomicEffectType.ModifyPower, AtomicEffectType.ModifyLife },
                BonusMultiplier = 1.08f
            });
        }

        /// <summary>
        /// 计算同类效果递减
        /// </summary>
        public float CalculateSameTypeDiscount(int count, float baseValue)
        {
            if (count < DiscountStartsAt) return baseValue;

            int discountedCount = count - DiscountStartsAt + 1;
            float discountFactor = 1.0f;
            for (int i = 0; i < discountedCount; i++)
            {
                discountFactor *= SameTypeDiscount;
            }

            float avgValue = baseValue / count;
            float normalCount = DiscountStartsAt - 1;
            return avgValue * normalCount + avgValue * (count - normalCount) * discountFactor;
        }

        /// <summary>
        /// 计算协同效应加成
        /// </summary>
        public float CalculateSynergyBonus(HashSet<AtomicEffectType> effectTypes, float baseTotal)
        {
            float result = baseTotal;
            foreach (var synergy in SynergyBonuses)
            {
                if (synergy.IsSatisfied(effectTypes))
                {
                    result *= synergy.BonusMultiplier;
                }
            }
            return result;
        }

        /// <summary>
        /// 获取满足的协同效应列表
        /// </summary>
        public List<SynergyBonus> GetSatisfiedSynergies(HashSet<AtomicEffectType> effectTypes)
        {
            return SynergyBonuses.Where(s => s.IsSatisfied(effectTypes)).ToList();
        }

        public static SynergyRuntimeConfig CreateDefault()
        {
            return new SynergyRuntimeConfig();
        }
    }
}
