using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Attribute
{
    /// <summary>
    /// 关键词战斗/伤害行为的唯一实现点。
    ///
    /// 【设计原则（定案）】表内关键词零参数、效果固定（坚韧恒 −1、再生恒 +2）——
    /// 强化只走融合叠加（重复坚韧 = −N，仅融合怪兽；非融合不可重复添加）。
    /// 参数化效果走原子（AddArmor 护甲 N 点指示物）。
    ///
    /// 伤害管线（效果 TakeDamage 与战斗 CombatSystem 两条路径都经 ApplyDamage）：
    /// 圣盾（挡一次任意伤害，消耗）→ 护甲指示物（逐点吸收）→ 坚韧（−持有次数）→ 落血；
    /// 剧毒（任意来源伤害即致死，仅随从）；吸血（恢复随从自身）/系命（回复角色）。
    /// 生命流失（LifeLoss 类）不经此管线——圣盾/护甲不挡流失。
    /// </summary>
    public static class KeywordRules
    {
        /// <summary>护甲指示物名（AddArmor 原子写入，伤害先扣）</summary>
        public const string ArmorCounter = "Armor";

        // ---- 关键词 id（与 GrantKeywordHandlerFactory.Specs 的运行时字符串同源） ----
        public const string DivineShield = "DivineShield";
        public const string Armor = "Armor";
        public const string Poisonous = "Poisonous";
        public const string Lifesteal = "Lifesteal";
        public const string Lifelink = "Lifelink";
        public const string Vigilance = "Vigilance";
        public const string Guard = "Guard";
        public const string Stealth = "Stealth";
        public const string Taunt = "Taunt";
        public const string Charge = "Charge";
        public const string Rush = "Rush";
        public const string Windfury = "Windfury";
        public const string FirstStrike = "FirstStrike";
        public const string DoubleStrike = "DoubleStrike";
        public const string Trample = "Trample";
        public const string Overwhelm = "Overwhelm";
        public const string Reborn = "Reborn";
        public const string Indestructible = "Indestructible";
        public const string Regeneration = "Regeneration";
        public const string Growth = "Growth";
        public const string SpellShield = "SpellShield";
        public const string Untargetable = "Untargetable";

        /// <summary>持有某关键词的次数（融合叠加：重复坚韧计 2）</summary>
        public static int KeywordCount(Entity entity, string keyword)
        {
            return entity is Card card ? card.GetKeywordCount(keyword) : (entity.HasKeyword(keyword) ? 1 : 0);
        }

        /// <summary>
        /// 统一伤害结算：修正并施加伤害，返回实际造成的伤害量。
        /// 只改状态不发事件——DamageEvent/CombatDamageEvent 等由调用方按路径发布。
        /// </summary>
        public static int ApplyDamage(Entity source, Entity target, int amount, bool isCombat)
        {
            if (amount <= 0 || target == null || !target.IsAlive) return 0;

            // 规则修改类效果（OCP）：伤害实例先经替代引擎取最终值（如伤害封顶），再走关键词管线。
            // 替代效果经 GameCore.ReplacementEngine 注册（状态无关、实时查询光环），本管线不点名任何具体系统。
            var routedEvent = new DamageEvent { Source = source, Target = target, Amount = amount };
            var engine = CardCore.GameCore.Instance?.ReplacementEngine;
            if (engine != null)
            {
                if (engine.CheckReplacements(routedEvent).GetFinalEvent() is DamageEvent final)
                    amount = final.Amount;
                if (amount <= 0) return 0;
            }

            // 1. 圣盾：挡下一次任意伤害（战斗+效果），消耗
            if (target.HasKeyword(DivineShield))
            {
                target.RemoveKeyword(DivineShield);
                return 0;
            }

            // 2. 护甲指示物：逐点吸收（AddArmor 原子写入的 counter 池）
            if (target is Card armored)
            {
                int armor = armored.GetCounterCount(ArmorCounter);
                if (armor > 0)
                {
                    int absorbed = Math.Min(armor, amount);
                    armored.AddCounters(ArmorCounter, -absorbed);
                    amount -= absorbed;
                    if (amount <= 0) return 0;
                }
            }

            // 3. 坚韧：每次受到的最终伤害 −1 × 持有次数（叠加 = 融合专属强化）
            int toughness = KeywordCount(target, Armor);
            if (toughness > 0)
                amount = Math.Max(0, amount - toughness);
            if (amount <= 0) return 0;

            // 4. 落血（Card 到 0 标记死亡；Player 直接扣）
            if (target is Card card)
            {
                card._life -= amount;
                if (card._life <= 0)
                {
                    card._life = 0;
                    card.IsAlive = false;
                }
            }
            else if (target is Player player)
            {
                player.Life -= amount;
            }

            // 5. 剧毒：任意来源伤害即致死（仅随从目标）
            if (source != null && source.HasKeyword(Poisonous) && target is Card poisoned && poisoned.IsAlive)
            {
                poisoned._life = 0;
                poisoned.IsAlive = false;
            }

            // 6. 吸血（恢复随从自身）/ 系命（回复角色）——按实际造成的伤害结算
            if (source != null && source.IsAlive)
            {
                if (source.HasKeyword(Lifesteal) && source is Card stealer)
                    stealer.Heal(amount);
                if (source.HasKeyword(Lifelink) && source.GetController() is Player owner)
                    owner.Life += amount;
            }

            // 结算后发布最终伤害事件（替代已在管线前消费，此处发实际值供触发器观察）。
            // 统一由本方法发布，调用方不再事后补发（历史上仅 CombatSystem 补发，已删）。
            EventManager.Instance.Publish(new DamageEvent { Source = source, Target = target, Amount = amount });

            return amount;
        }

        /// <summary>
        /// 是否应横置该实体：警戒可取消一次横置（攻击横置/发动代价横置），每回合一次。
        /// 返回 true = 应横置；false = 警戒抵消（消耗本回合额度）。
        /// </summary>
        public static bool ShouldTap(Entity entity)
        {
            if (entity == null) return false;
            if (entity is Card card && card.HasKeyword(Vigilance) && !card._vigilanceUsedThisTurn)
            {
                card._vigilanceUsedThisTurn = true; // 一回合只生效一次
                return false;
            }
            return true;
        }

        /// <summary>
        /// 复生结算：死亡时以 1 血回场——横置 + 带召唤失调（视为重新入场），消耗关键词。
        /// 由死亡路径（SBA 零防御 / 摧毁效果）在移墓前调用；true = 已复生（留在战场）。
        /// </summary>
        public static bool TryReborn(Card card)
        {
            if (card == null || !card.HasKeyword(Reborn)) return false;

            card.RemoveKeyword(Reborn);
            card._life = 1;
            if (card._maxLife < 1) card._maxLife = 1;
            card.IsAlive = true;
            card._isTapped = true;          // 横置
            card.SummonedThisTurn = true;  // 带召唤失调（视为重新入场）
            return true;
        }

        /// <summary>
        /// 法术护盾结算：目标列表中带护盾的对手随从——该效果对其无效（移出目标）并消耗护盾。
        /// 在效果执行前置过滤（候选/查询阶段不消耗——只在真正执行时挡）。
        /// </summary>
        public static void ConsumeSpellShields(List<Entity> targets, Entity source)
        {
            if (targets == null || targets.Count == 0) return;
            var sourceController = source?.GetController();

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (!(targets[i] is Card shielded)) continue;
                var targetController = shielded.GetController();
                if (sourceController == null || targetController == null || sourceController == targetController)
                    continue;
                if (!shielded.HasKeyword(SpellShield)) continue;

                shielded.RemoveKeyword(SpellShield);
                targets.RemoveAt(i);
            }
        }
    }
}
