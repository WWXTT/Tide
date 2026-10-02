using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// Card 类的扩展方法
    /// 提供通过接口类型检查和获取卡牌属性的能力
    /// </summary>
    public static class CardExtensions
    {
        /// <summary>
        /// 尝试获取指定类型的属性接口
        /// </summary>
        public static bool TryGetProperty<T>(this Card card, out T property) where T : class
        {
            property = null;

            // 如果 Card 本身就是目标接口类型
            if (card is T cardAsT)
            {
                property = cardAsT;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查卡牌是否具有指定的属性接口
        /// </summary>
        public static bool HasProperty<T>(this Card card) where T : class
        {
            return card is T;
        }

        /// <summary>
        /// 获取指定类型的属性接口，如果不存在则返回 null
        /// </summary>
        public static T GetProperty<T>(this Card card) where T : class
        {
            return card as T;
        }

        /// <summary>
        /// 尝试获取立绘
        /// </summary>
        public static bool TryGetIllustration(this Card card, out string illustration)
        {
            illustration = null;
            if (card is IHasIllustration hasIllustration)
            {
                illustration = hasIllustration.Illustration;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取卡牌名称
        /// </summary>
        public static bool TryGetName(this Card card, out string name)
        {
            name = null;
            if (card is IHasName hasName)
            {
                name = hasName.CardName;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取生命值
        /// </summary>
        public static bool TryGetLife(this Card card, out int life)
        {
            life = 0;
            if (card is IHasLife hasLife)
            {
                life = hasLife.Life;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取攻击力
        /// </summary>
        public static bool TryGetPower(this Card card, out int power)
        {
            power = 0;
            if (card is IHasPower hasPower)
            {
                power = hasPower.Power;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取费用
        /// </summary>
        public static bool TryGetCost(this Card card, out Dictionary<int, float> cost)
        {
            cost = null;
            if (card is IHasCost hasCost)
            {
                cost = hasCost.Cost;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取效果列表
        /// </summary>
        public static bool TryGetEffects(this Card card, out List<Effect_table> effects)
        {
            effects = null;
            if (card is IHasEffects hasEffects)
            {
                effects = hasEffects.Effects;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 是否魔法卡（瞬间/巫术/法术/通常魔法）。
        /// 来源归因定案（2026-09-09）：所有魔法卡的效果来源=角色（Player）——
        /// 出牌分流（ResolveCardCastAsync）、三轨判轨（StatGrantRouter）、SimpleAI 模拟共用此口径。
        /// </summary>
        public static bool IsSpellCard(this Card card)
            => card is IHasSupertype t && t.Supertype == Cardtype.Spell;

        /// <summary>
        /// 活体单位（2026-10-02 三类卡型定案）：生物（Supertype==Creature）。
        /// 生命/死亡管线（落血、SBA ZeroToughness、DeathRules）只对活体单位生效；
        /// 角色 Player 不经此判（玩家生命管线独立）。
        /// </summary>
        public static bool IsLivingUnit(this Card card)
            => card is IHasSupertype t && t.Supertype == Cardtype.Creature;

        /// <summary>
        /// 无生命单位（耐久体，2026-10-02 结界实装）：战场上的非生物卡（结界为主，装备同语义）——
        /// 受击恒 -1 耐久不落血，耐久归零直送墓（Smashed 直毁，不经死亡决策表）。
        /// 与目标域 3/4（NonLivingOf = 战场非生物卡）同口径；FieldZone 英雄技能卡、
        /// 元素池地牌等非战场卡不算。
        /// </summary>
        public static bool IsNonLivingUnit(this Card card)
            => card is IHasSupertype t && t.Supertype != Cardtype.Creature
               && card.GetZone() == Zone.Battlefield;
    }
}
