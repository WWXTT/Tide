using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 装备系统运行时（2026-09-13 第二十一批定案：万智神器 × 炉石武器——箭头佩带 + 驱动）。
    ///
    /// - **佩带 = 箭头指向格的占据者**（认格不认主：对手走进指向格效果照发）——
    ///   普通装备的佩带者效果经现有 LinkAuraSystem（LinkAuraData 声明 stat/keyword）live-query 生效，
    ///   悬空（空格）= 无受益者 = 效果暂停，天然成立。EquipRules 不重复实现此路径。
    /// - **耐久归零 = 销毁入墓**（Smash 同款直毁路径）。
    ///
    /// 2026-10-07 武器面退役：武器（佩带者=控制者角色、主动攻击、反伤、驱动）整体删除——
    /// 角色参战改走 HeroAttackCounter 弹药原子（CounterRules/CombatSystem，弹药即闸门），
    /// CombatSystem 反伤扩展点（PlayerCounterattackPower/OnPlayerCounterattackResolved）一并退役。
    /// 保留：IsEquipment 判定、入场耐久初始化（结界/装备一套语义）、LoseDurability 转发。
    /// </summary>
    public static class EquipRules
    {
        // ======================================== 耐久 ========================================

        /// <summary>装备是否为装备（无生命持久物 + Artifact 超类或 Equip 标签）。</summary>
        public static bool IsEquipment(Card card)
            => card is IHasSupertype st && st.Supertype == Cardtype.Artifact;

        /// <summary>消耗 N 点耐久；归零 → 销毁入墓（Smash 同款直毁路径 + 播报）。
        /// 实现已迁 CounterRules.LoseDurability（2026-10-02 结界实装：装备/结界一套耐久语义），此处转发保调用点稳定。</summary>
        public static void LoseDurability(GameCore core, Card equipment, int amount, string reason)
            => CounterRules.LoseDurability(core, equipment, amount, reason);

        /// <summary>入场初始化耐久（CardData.Durability > 0 时挂 N 层；由 CardPutToBattlefieldEvent 驱动）。
        /// 2026-10-02 结界实装：一切无生命单位（战场非生物卡——装备与结界）同走此初始化，一套耐久语义。</summary>
        public static void OnEnterBattlefield(Card card)
        {
            if (!(card is CardWrapper w)) return;
            var data = w.GetData();
            if (data == null || !card.IsNonLivingUnit()) return;
            if (data.Durability > 0 && card.GetCounterCount(CounterRules.DurabilityCounter) <= 0)
                card.AddCounters(CounterRules.DurabilityCounter, data.Durability);
        }
    }
}
