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

        // ---- 一次性关键词概念已彻底删除（2026-09-08 定案：错误设计）----
        // 关键词都是持续性特征，无「一次性生效后消失/重新入场刷新」的说法：
        // · 冲锋/突袭：改由卡的登场效果表达（OnPlay+激励自己解除横置，突袭另自上紊乱指示物
        //   作代价减费——见 CostDerivationService 的 Self 紊乱对冲）；
        // · 复生：死亡替代结算时移除关键词（TryReborn 内的效果性移除），无自动刷新。

        /// <summary>
        /// 突袭紊乱指示物名（负面，持续到回合结束）：持有期间不能以玩家为目标
        /// （攻击与效果发动同口径）；消退走 CounterRules 统一清理。
        /// 来源：突袭的登场效果自上（代价减费），或紊乱原子直接施加给敌方。
        /// </summary>
        public const string RushSicknessCounter = "RushSickness";

        /// <summary>是否处于突袭紊乱（负面指示物存在期间不能以玩家为目标；消退走 CounterRules）。</summary>
        public static bool HasRushSickness(Entity entity)
            => entity is Card c && c.GetCounterCount(RushSicknessCounter) > 0;

        // ==================== 关键词轨别台账（三轨制定案 2026-09-09） ====================

        /// <summary>
        /// 净化豁免名单（2026-09-09 定案：神佑对净化有抗性）。净化剥神佑+剧毒杀角色的组合
        /// 价值过高、无法与作用在生物上的效果计价平衡——净化剥不掉神佑，移除留给未来专用效果。
        /// </summary>
        public static readonly HashSet<string> PurgeProtectedKeywords =
            new HashSet<string> { DeathRules.DivineProtection };

        /// <summary>
        /// 换区清关键词（与 CounterRules.ClearAll 同口）：移除 Temp 轨授予。
        /// 由 ZoneContainer.OnCardMoved 调用（同受发动区豁免约束），且须在 TryEndMorph 之后执行
        /// （否则变形快照恢复会把已清的临时层复活）。
        /// </summary>
        public static void ClearZoneKeywords(Entity entity)
            => RemoveGrants(entity, g => g.Lane == KeywordLane.Temp);

        /// <summary>
        /// 净化清关键词（净化语义重定义 2026-09-09：净化=变回生物原有状态）：
        /// 保留 Printed（卡面本体）与 Setting（设置类视同本体——设置后即「原本属性效果」）；
        /// 清除 Temp / GrantedPermanent / Status 轨（PurgeProtectedKeywords 豁免——神佑等抗净化状态保留）。
        /// 与 CounterRules.PurgeAll（指示物全清）配套，由 PurifyHandler 调用。
        /// </summary>
        public static void PurifyKeywords(Entity entity)
            => RemoveGrants(entity, g => g.Lane != KeywordLane.Printed
                                      && g.Lane != KeywordLane.Setting
                                      && !PurgeProtectedKeywords.Contains(g.Keyword));

        /// <summary>
        /// 形态复制（定案⑨：临时不随形态）——只复制 from 的 Printed+Setting 轨关键词，
        /// 落到 to 按 toLane 记账（吞噬/融合继承=Setting：吸收后视同本体）。
        /// 台账缺失（重连/旧档）时保守按 from._keywords 全量去重复制。
        /// </summary>
        public static void CopyFormKeywords(Card from, Card to, KeywordLane toLane = KeywordLane.Setting)
        {
            if (from == null || to == null) return;
            var formGrants = from._keywordGrants
                .Where(g => g.Lane == KeywordLane.Printed || g.Lane == KeywordLane.Setting)
                .Select(g => g.Keyword)
                .Distinct()
                .ToList();
            if (formGrants.Count == 0 && from._keywords.Count > 0)
                formGrants = from._keywords.Distinct().ToList();
            foreach (var kw in formGrants)
                to.AddKeyword(kw, toLane);
        }

        /// <summary>
        /// 入场刷新（2026-09-09 定案）：**真实入场**时对 Printed 轨做卡面差集补齐——
        /// 卡面（CardData.Keywords）有而该实例 Printed 轨没有的关键词补回，恢复到卡面份数。
        ///
        /// 挂载红线：只挂在 TryMoveToBattlefield / TryAddToBattlefield 统一出口（真换区才算入场）——
        /// · 复生（TryReborn）是死亡替代原地留场，不经出口 → 消耗掉的复生不会自我补回（无无限复生）；
        /// · 控制权变更（ChangeControl）走容器直移 + 补发 CardPutToBattlefieldEvent——
        ///   ⚠ 因此**绝不能**把刷新挂到 CardPutToBattlefieldEvent 事件上（偷取不刷新消耗项）。
        ///
        /// 差集安全性：Printed 轨目前只能被**消耗型移除**（圣盾/复生/潜行/法术护盾——移除即用掉）；
        /// 净化保留本体、无任何效果可剥 Printed——差集补回的必然只是被消耗项。
        /// </summary>
        public static void RefreshPrintedKeywordsOnEntry(Card card)
        {
            if (card == null) return;
            var face = (card as CardWrapper)?.GetData()?.Keywords;
            if (face == null || face.Count == 0) return;

            foreach (var kw in face.Distinct())
            {
                if (string.IsNullOrEmpty(kw)) continue;

                int faceCount = 0;
                foreach (var k in face)
                    if (k == kw) faceCount++;

                int printedCount = 0;
                foreach (var g in card._keywordGrants)
                    if (g.Keyword == kw && g.Lane == KeywordLane.Printed)
                        printedCount++;

                for (int i = printedCount; i < faceCount; i++)
                {
                    card.AddKeywordStack(kw, KeywordLane.Printed); // 叠加补齐（_keywords 与台账同补一份）
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card,
                        Keyword = kw,
                        Detail = "入场刷新：补回卡面本体关键词（消耗项随真实入场恢复）"
                    });
                }
            }
        }

        /// <summary>
        /// 台账移除核心：按谓词撤掉授予条目，随后同步 _keywords——
        /// 某关键词的授予条目被清空后（无任何轨再持有）才移除其本体占用。
        /// 注意：无台账条目的关键词（重连还原/融合直加）不在清除面内——保守视同本体。
        /// </summary>
        private static void RemoveGrants(Entity entity, Func<KeywordGrant, bool> predicate)
        {
            if (entity == null || entity._keywordGrants.Count == 0) return;
            var affected = entity._keywordGrants.Where(predicate).Select(g => g.Keyword).Distinct().ToList();
            if (affected.Count == 0) return;
            entity._keywordGrants.RemoveAll(g => predicate(g));
            foreach (var kw in affected)
            {
                // 叠加语义：本体占用份数归一到台账剩余份数（无台账条目的关键词不在 affected，不受波及）
                int remaining = entity._keywordGrants.Count(g => g.Keyword == kw);
                int have = entity._keywords.RemoveAll(k => k == kw);
                for (int i = 0; i < Math.Min(have, remaining); i++)
                    entity._keywords.Add(kw);
            }
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
            //    三轨制（2026-09-09）：归零判定按**有效生命**（_life + 连接光环加成）——
            //    生命光环垫着的单位要先吃穿光环加成才死；断链回落由 SBA 收尾。
            int oldLife = (target as Player)?.Life ?? 0; // 生命变化播报用
            if (target is Card card)
            {
                card._life -= amount;
                int auraLife = GameBoard.LinkAuraSystem.GetLifeBonus(card);
                if (card._life + auraLife <= 0)
                {
                    card._life = 0;
                    if (!DeathRules.IsShielded(card, DeathCause.DamageLethal))
                    {
                        card._pendingDeathCause = DeathCause.DamageLethal;
                        card._pendingDeathSource = source;
                        card.IsAlive = false;
                    }
                    else if (auraLife > 0)
                    {
                        card._life = 1 - auraLife; // 护盾拦下：以有效生命 1 存活（无光环保持原样=0 存活）
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
        /// 是否应横置该实体：攻击/发动代价恒横置。
        /// 【2026-09-10 警戒重定义】警戒不再是「代替横置扣除」（攻击照常横置）——
        /// 新语义为「横置也能造成战斗伤害」（反击资格判定见 CombatSystem.ResolvePair：
        /// 横置目标持警戒仍可反击）。被动无额度、无消耗。
        /// </summary>
        public static bool ShouldTap(Entity entity)
        {
            if (entity == null) return false;
            return true; // 横置是固定代价（旧警戒抵扣已随重定义删除）
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
