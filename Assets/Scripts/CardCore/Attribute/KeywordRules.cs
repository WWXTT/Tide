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
    /// 替代引擎（光环层——穿透伤害也受其限制）→ 圣盾（挡一次任意伤害，消耗）→
    /// 护甲指示物（逐点吸收）→ 坚韧（−持有次数）→ 落血；
    /// 吸血（恢复随从自身）/系命（回复角色）。
    /// 【穿透伤害（pierce=true）】越过关键词和指示物计算伤害——跳过圣盾/护甲/坚韧三层，
    /// 替代引擎与落血/事件链/吸血照走。
    /// 【剧毒】已从关键词伤害改为指示物（CounterRules.PoisonCounter，回合结束时持有者死亡，
    /// 无伤害来源）；毒刺关键词改为战斗伤害后附加毒素指示物（挂 CombatSystem）。
    /// 事件链统一在管线尾部按时序发布：DamageEvent（触发器）→ CombatDamageEvent（战斗）→
    /// LifeChangeEvent（角色）→ 吸血/系命（伤害的后果）。
    /// 生命流失（LifeLoss 类）不经此管线——圣盾/护甲不挡流失。
    /// </summary>
    public static class KeywordRules
    {
        /// <summary>护甲指示物名（AddArmor 原子写入，伤害先扣）</summary>
        public const string ArmorCounter = "Armor";

        /// <summary>
        /// 冻结指示物名（定案）：冻结 = 强制横置（一次性动作）+ 负面指示物（持续到回合结束）。
        /// 不修改回合规则（回合开始横置重置照常）；消退走 CounterRules 统一清理。
        /// </summary>
        public const string FreezeCounter = "Freeze";

        // ---- 关键词 id（与 GrantKeywordHandlerFactory.Specs 的运行时字符串同源） ----
        public const string DivineShield = "DivineShield";
        public const string Armor = "Armor";
        public const string PoisonSting = "PoisonSting";
        public const string Lifesteal = "Lifesteal";
        public const string Lifelink = "Lifelink";
        public const string Vigilance = "Vigilance";
        public const string Stealth = "Stealth";
        public const string Taunt = "Taunt";
        public const string Charge = "Charge";
        public const string Rush = "Rush";
        public const string FirstStrike = "FirstStrike";
        public const string DoubleStrike = "DoubleStrike";
        public const string Disarm = "Disarm";
        public const string Overwhelm = "Overwhelm";
        public const string Reborn = "Reborn";
        public const string Indestructible = "Indestructible";
        public const string Regeneration = "Regeneration";
        public const string Growth = "Growth";
        public const string SpellShield = "SpellShield";
        public const string Untargetable = "Untargetable";

        // ---- 一次性关键词（定案）：生效即消耗，重新进入战场时从卡牌固有定义刷新 ----

        /// <summary>入场型一次性关键词（冲锋/突袭）：入场生效时消耗。</summary>
        public static readonly string[] OneShotEntryKeywords = { Charge, Rush };

        /// <summary>死亡触发型一次性关键词（复生）：死亡送墓被替代时消耗。</summary>
        public static readonly string[] OneShotDeathKeywords = { Reborn };

        /// <summary>
        /// 突袭紊乱指示物名（负面，持续到回合结束）：突袭生效消耗后残留在随从身上——
        /// 期间不能以玩家为目标（攻击与效果发动同口径）；消退走 CounterRules 统一清理。
        /// </summary>
        public const string RushSicknessCounter = "RushSickness";

        /// <summary>
        /// 入场型一次性关键词生效（定案）：随从一律横置入场；冲锋/突袭生效 = 解除横置 + 消耗关键词，
        /// 突袭另放一个紊乱指示物（负面，持续一回合）。
        /// 由入场路径（TryMoveToBattlefield / TryAddToBattlefield）调用；返回 true = 已解除横置。
        /// </summary>
        public static bool ApplyEntryKeywords(Card card)
        {
            if (card == null) return false;
            if (card.HasKeyword(Charge))
            {
                card.RemoveKeyword(Charge);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = card,
                    Keyword = Charge,
                    Detail = "冲锋生效：解除横置（一次性，已消耗）"
                });
                return true;
            }
            if (card.HasKeyword(Rush))
            {
                card.RemoveKeyword(Rush);
                card.AddCounters(RushSicknessCounter, 1);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = card,
                    Keyword = Rush,
                    Detail = "突袭生效：解除横置（一次性，已消耗；紊乱一回合——期间不能以玩家为目标）"
                });
                return true;
            }
            return false;
        }

        /// <summary>是否处于突袭紊乱（负面指示物存在期间不能以玩家为目标；消退走 CounterRules）。</summary>
        public static bool HasRushSickness(Entity entity)
            => entity is Card c && c.GetCounterCount(RushSicknessCounter) > 0;

        /// <summary>
        /// 一次性关键词刷新（重新进入战场时）：从卡牌固有定义补回已消耗的冲锋/突袭/复生。
        /// 仅刷一次性类——持续型关键词的效果移除不被入场重置。
        /// </summary>
        public static void RefreshOneShotKeywords(Card card)
        {
            if (!(card is CardWrapper wrapper)) return;
            var data = wrapper.GetData();
            if (data?.Keywords == null) return;
            var oneShot = new List<string>(OneShotEntryKeywords);
            oneShot.AddRange(OneShotDeathKeywords);
            foreach (var kw in oneShot)
                if (data.Keywords.Contains(kw) && !card.HasKeyword(kw))
                    card.AddKeyword(kw);
        }

        /// <summary>持有某关键词的次数（融合叠加：重复坚韧计 2）</summary>
        public static int KeywordCount(Entity entity, string keyword)
        {
            return entity is Card card ? card.GetKeywordCount(keyword) : (entity.HasKeyword(keyword) ? 1 : 0);
        }

        /// <summary>
        /// 统一伤害结算：修正并施加伤害，返回实际造成的伤害量。
        /// 只改状态不发事件——DamageEvent/CombatDamageEvent 等由调用方按路径发布。
        /// pierce=true 为穿透伤害：越过关键词和指示物（跳过圣盾/护甲/坚韧），
        /// 但受光环限制——替代引擎/层效果照走，事件链与吸血照常。
        /// </summary>
        public static int ApplyDamage(Entity source, Entity target, int amount, bool isCombat, bool pierce = false)
        {
            if (amount <= 0 || target == null || !target.IsAlive) return 0;

            // 规则修改类效果（OCP）：伤害实例先经替代引擎取最终值（如伤害封顶），再走关键词管线。
            // 替代效果经 GameCore.ReplacementEngine 注册（状态无关、实时查询光环），本管线不点名任何具体系统。
            // 穿透伤害同样经此层（"受光环限制"）。
            var routedEvent = new DamageEvent { Source = source, Target = target, Amount = amount };
            var engine = CardCore.GameCore.Instance?.ReplacementEngine;
            if (engine != null)
            {
                if (engine.CheckReplacements(routedEvent).GetFinalEvent() is DamageEvent final)
                    amount = final.Amount;
                if (amount <= 0) return 0;
            }

            // 0. 易损指示物（定案）：受到伤害时每层使受到的伤害 +1——
            //    替代结算后、防护层前生效（圣盾/护甲吸收的是放大后的量；穿透伤害同样被放大）
            int vulnerable = target.GetCounterCount(CounterRules.VulnerableCounter);
            if (vulnerable > 0)
                amount += vulnerable;

            if (!pierce)
            {
                ApplyPreventionLayers(source, target, ref amount);
                if (amount <= 0) return 0;
            }

            // 4. 落血（Card 到 0 标记死亡；Player 直接扣）。
            //    死亡决策走决策表（当前无护盾拦 DamageLethal；复生/落墓由 SBA 泵自发连锁处理）。
            //    死亡归因（2026-09-07）：伤害来源随尸体留档，SBA 送墓时经 TryKill 上 CardDestroyEvent。
            int oldLife = (target as Player)?.Life ?? 0; // 生命变化播报用
            if (target is Card card)
            {
                card._life -= amount;
                if (card._life <= 0)
                {
                    card._life = 0;
                    if (!DeathRules.IsShielded(card, DeathCause.DamageLethal))
                    {
                        card._pendingDeathCause = DeathCause.DamageLethal;
                        card._pendingDeathSource = source;
                        card.IsAlive = false;
                    }
                }
            }
            else if (target is Player player)
            {
                player.Life -= amount;
            }

            // 结算后发布伤害事件链（实际值；替代已在管线前消费）。
            // 时序定案：DamageEvent（触发器时点，OnDealDamage/OnTakeDamage 此刻可见）
            // → CombatDamageEvent（战斗路径表现）→ LifeChangeEvent（角色生命变化）
            // → 吸血/系命（伤害的后果最后结算——观察伤害事件的触发器不应看到回复已发生）。
            // 统一由本方法发布，调用方不再事后补发（战斗/效果两条路径同口径）。
            var damageEvent = new DamageEvent { Source = source, Target = target, Amount = amount };
            if (GameCore.Instance != null)
                GameCore.Instance.PublishEventRouted(damageEvent, replacementsApplied: true);
            else
                EventManager.Instance.Publish(damageEvent);

            if (isCombat)
            {
                EventManager.Instance.Publish(new CombatDamageEvent
                {
                    Attacker = source,
                    Defender = target,
                    Damage = amount
                });
            }
            if (target is Player hurt)
            {
                EventManager.Instance.Publish(new LifeChangeEvent
                {
                    Player = hurt,
                    OldLife = oldLife,
                    NewLife = hurt.Life,
                    Source = source
                });
            }

            // 6. 吸血（恢复随从自身）/ 系命（回复角色）——伤害事件的后果，按实际造成的伤害结算
            if (source != null && source.IsAlive)
            {
                if (source.HasKeyword(Lifesteal) && source is Card stealer)
                {
                    stealer.Heal(amount);
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = stealer,
                        Keyword = Lifesteal,
                        Detail = $"吸血回复 {amount} 生命",
                        Source = target
                    });
                }
                if (source.HasKeyword(Lifelink) && source.GetController() is Player owner)
                {
                    owner.Life += amount;
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = owner,
                        Keyword = Lifelink,
                        Detail = $"系命回复 {amount} 生命",
                        Source = source
                    });
                }
            }

            return amount;
        }

        /// <summary>
        /// 防护层（非穿透伤害）：圣盾（挡一次任意伤害，消耗）→ 护甲指示物（逐点吸收）→ 坚韧（−持有次数）。
        /// amount 按 ref 递减；归零即全部挡下。穿透伤害跳过本方法全部三层。
        /// </summary>
        private static void ApplyPreventionLayers(Entity source, Entity target, ref int amount)
        {
            // 1. 圣盾：挡下一次任意伤害（战斗+效果），消耗
            if (target.HasKeyword(DivineShield))
            {
                target.RemoveKeyword(DivineShield);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = DivineShield,
                    Detail = $"圣盾抵挡 {amount} 点伤害",
                    Source = source
                });
                amount = 0;
                return;
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
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = target,
                        Keyword = ArmorCounter,
                        Detail = $"护甲指示物吸收 {absorbed} 点",
                        Source = source
                    });
                    if (amount <= 0)
                    {
                        amount = 0;
                        return;
                    }
                }
            }

            // 3. 坚韧：每次受到的最终伤害 −1 × 持有次数（叠加 = 融合专属强化）
            int toughness = KeywordCount(target, Armor);
            if (toughness > 0)
            {
                amount = Math.Max(0, amount - toughness);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = Armor,
                    Detail = $"坚韧减免 {toughness} 点",
                    Source = source
                });
            }
            if (amount <= 0) amount = 0;
        }

        /// <summary>
        /// 是否应横置该实体：警戒可取消一次横置（攻击横置/发动代价横置），每回合一次。
        /// 返回 true = 应横置；false = 警戒抵消（消耗本回合额度）。
        /// 【定案】警戒抵扣仅在未横置时有效：已横置 = 代价不可支付（不可被警戒抵消），
        /// 不消耗警戒额度——返回 true（调用方的前置校验应拦截横置中实体，此为兜底）。
        /// </summary>
        public static bool ShouldTap(Entity entity)
        {
            if (entity == null) return false;
            if (entity.IsTapped()) return true; // 已横置：支付不出，不消耗警戒额度
            if (entity is Card card && card.HasKeyword(Vigilance) && !card._vigilanceUsedThisTurn)
            {
                card._vigilanceUsedThisTurn = true; // 一回合只生效一次
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = card,
                    Keyword = Vigilance,
                    Detail = "警戒抵消横置"
                });
                return false;
            }
            return true;
        }

        /// <summary>
        /// 复生结算（死亡替代定案）：死亡时的送墓效果被替代——不进墓地、不离场，
        /// 生命值变成 1、横置（本回合不可用），消耗一次性关键词。
        /// 由死亡路径（SBA 零防御 / 摧毁效果）在送墓前调用；true = 已复生（留在战场）。
        /// </summary>
        public static bool TryReborn(Card card)
        {
            if (card == null || !card.HasKeyword(Reborn)) return false;

            card.RemoveKeyword(Reborn);
            card._life = 1;
            if (card._maxLife < 1) card._maxLife = 1;
            card.IsAlive = true;
            card._isTapped = true;          // 横置（可用性统一指标：视为重新入场，本回合不可用）
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = card,
                Keyword = Reborn,
                Detail = "复生：死亡替代——生命变 1 留场（横置，不进墓）"
            });
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
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = shielded,
                    Keyword = SpellShield,
                    Detail = "法术护盾使效果对其无效",
                    Source = source
                });
            }
        }
    }
}
