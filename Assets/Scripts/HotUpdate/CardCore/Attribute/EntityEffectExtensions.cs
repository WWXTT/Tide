using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// Entity扩展方法 - 为效果处理器提供必要的API
    /// </summary>
    public static class EntityEffectExtensions
    {
        #region 战斗属性

        // 【三轨制·光环读数咽喉（2026-09-09）】连接光环（LinkAuraSystem live-query）的属性/关键词
        // 贡献并入以下读数——战斗（LayerEngine 基值也读此处）、伤害管线、SBA、AI 全部经此同源。
        // ⚠ 绕过本组方法直读 _power/_life/_maxLife 的新代码会让光环静默不可见。

        /// <summary>获取攻击力（含连接光环加成）</summary>
        public static int GetPower(this Entity entity)
        {
            if (entity is Unit unit) return unit.BaseAttack;
            if (entity is Card card) return card._power + GameBoard.LinkAuraSystem.GetPowerBonus(card);
            // 角色（2026-10-07 弹药原子定案）：攻击力=角色攻击指示物层数（无光环成分、无减益参与）——
            // 攻击/反击结算后烧除（CounterRules.RemoveHeroAttackAmmo），读数 0 即无攻击资格。
            if (entity is Player player) return Math.Max(0, player.GetCounterCount(Attribute.CounterRules.HeroAttackCounter));
            return 0;
        }

        // ---- 战斗底盘能力（2026-09-10 攻/守效果化）：opt-out 经卡数据声明，缺省自带 ----
        // 扩展方法口径（与 IsTapped 同款）：CombatSystem/AI 拿到的是 Card 基类引用。
        // 裸 Card（衍生物/临时卡，无卡数据）从严 = 无攻无守。

        /// <summary>攻击能力（速度0主动效果，2026-09-16 战斗接入栈机器）：生物默认自带；NoAttack 卡不能宣言攻击。</summary>
        public static bool HasAttackAbility(this Card card)
            => card is CardWrapper w && w.GetData() is CardData d
               && d.Supertype == Cardtype.Creature && !d.NoAttack;

        /// <summary>守卫能力（速度1响应拦截，2026-09-16）：生物默认自带；守卫拦截入口按此过滤。</summary>
        public static bool HasGuardAbility(this Card card)
            => card is CardWrapper w && w.GetData() is CardData d
               && d.Supertype == Cardtype.Creature && !d.NoGuard;

        /// <summary>获取生命值（含连接光环加成；伤害扣 _life 原值，断链自动回落）</summary>
        public static int GetLife(this Entity entity)
        {
            if (entity is Player player) return player.Life;
            if (entity is Card card) return card._life + GameBoard.LinkAuraSystem.GetLifeBonus(card);
            return 0;
        }

        /// <summary>获取最大生命值（含连接光环加成——生命光环上限与当前同加）</summary>
        public static int GetMaxLife(this Entity entity)
        {
            if (entity is Player player) return player.MaxHealth;
            if (entity is Card card)
                return (card._maxLife > 0 ? card._maxLife : card._life) + GameBoard.LinkAuraSystem.GetMaxLifeBonus(card);
            return 0;
        }

        /// <summary>获取当前生命值</summary>
        public static int GetCurrentLife(this Entity entity) => entity.GetLife();

        #endregion

        #region 战斗操作

        /// <summary>
        /// 受到伤害（关键词管线）：经 KeywordRules 结算——圣盾挡一次、护甲指示物逐点吸收、
        /// 坚韧指示物每层 −1（拦到即生效——层数减半）、剧毒致死、吸血（恢复自身）/系命（回复角色）。
        /// 事件链（DamageEvent/CombatDamageEvent/LifeChangeEvent/吸血系命）由管线统一按时序发布。
        /// </summary>
        public static void TakeDamage(this Entity entity, int amount, Entity source, bool isCombat = false)
        {
            Attribute.KeywordRules.ApplyDamage(source, entity, amount, isCombat);
        }

        /// <summary>关键词持有份数（不叠加定案后正常路径恒 0/1；List 保留兼容直加旧档）</summary>
        public static int GetKeywordCount(this Entity entity, string keyword)
        {
            return entity is Card card ? card._keywords.Count(k => k == keyword) : 0;
        }

        /// <summary>
        /// 治疗（2026-10-04 丰盈仪典改造定案：溢出转化**收编为光环规则**——
        /// RuleAuraSystem.IsActive(HealOverflow) 生效时保持旧基线：每次溢出固定加 1 层
        /// 「生命值增加」指示物（上限与当前同加——生物换区清除、角色无换区概念即常驻），
        /// 当前实得=新上限，剩余溢出直接截断（例：满血 30 回复 7 → 上限 31、当前 31）。
        /// **无光环时溢出纯浪费**（生命钳在上限，不加上限——生命恢复计价因此享 0.8 系优惠，
        /// Heal 原子行 绿0.5→0.4）。角色与生物同口径。
        /// </summary>
        public static void Heal(this Entity entity, int amount)
        {
            // 反疗改写（2026-10-09 暗牧仪典+反疗指示物，先于丰盈溢出判定）：治疗改写为等量伤害
            //——改写口/重入闸/光源归因见 RuleAuraComponents.TryRewriteHealAsDamage；
            // 治疗五入口（Heal 原子/吸血/吸取/再生双路）全经此咽喉，一处收口全覆盖。
            if (amount > 0 && RuleAuraComponents.TryRewriteHealAsDamage(entity, amount)) return;

            if (entity is Player player)
            {
                // 丰盈范围化（2026-10-07）：按受疗方一侧判命中（缺省双方=旧行为）
                bool overflowToMax = RuleAuraSystem.ScopeHits(RuleAuraComponents.HealOverflow, player);
                int cap = player.MaxHealth;
                int raw = player.Life + amount;
                int over = raw - cap;
                if (over > 0 && overflowToMax)
                {
                    player.AddCounters(Attribute.CounterRules.LifeUpCounter, 1); // 层记录（换区清除型；角色无换区即常驻）
                    player.IncreaseMaxHealth(1);                                  // 层效果：上限+当前同加
                    player.Life = player.MaxHealth;                                // 实得=新上限，剩余溢出截断
                }
                else
                {
                    player.Life = System.Math.Min(raw, cap); // 无光环：溢出纯浪费（钳上限）
                }
            }
            else if (entity is Card card)
            {
                bool overflowToMax = RuleAuraSystem.ScopeHits(RuleAuraComponents.HealOverflow, card.GetController());
                if (card._maxLife < card._life) card._maxLife = card._life; // 未初始化兜底：先对齐再判溢出
                int cap = card._maxLife;
                int raw = card._life + amount;
                int over = raw - cap;
                if (over > 0 && overflowToMax)
                {
                    Attribute.CounterRules.AddStatCounter(card, Attribute.CounterRules.LifeUpCounter, 1);
                    card._life = card._maxLife; // AddStatCounter 已 +1/+1，实得=新上限，剩余溢出截断
                }
                else
                {
                    card._life = System.Math.Min(raw, cap); // 无光环：溢出纯浪费（钳上限）
                }

                // SBA 窗口救回（2026-09-15）：战场标死卡（伤害致死未送墓）治疗回到有效生命 >0 → 撤销标死，
                // SBA 到点重查两类捕获（!IsAlive / toughness≤0）均不命中 → 不送墓、亡语不触发。
                // 口径对齐 KeywordRules.TryReborn（不横置——未离场过）；清归因档防陈旧死因残留。
                // 连锁内 LIFO 的后置治疗救回先置伤害的标死单位，正是「到点重查」的规则语义。
                if (!card.IsAlive
                    && card._life + GameBoard.LinkAuraSystem.GetLifeBonus(card) > 0
                    && card.GetZone() == Zone.Battlefield)
                {
                    card.IsAlive = true;
                    card._pendingDeathCause = null;
                    card._pendingDeathSource = null;
                }
            }
        }

        #endregion

        #region 横置状态

        /// <summary>是否已横置</summary>
        public static bool IsTapped(this Entity entity)
        {
            if (entity is Card card) return card._isTapped;
            return false;
        }

        /// <summary>是否可以横置</summary>
        public static bool CanTap(this Entity entity)
        {
            if (entity is Card card) return !card._isTapped && entity.IsAlive;
            return false;
        }

        /// <summary>横置</summary>
        public static void Tap(this Entity entity)
        {
            if (entity is Card card) card._isTapped = true;
        }

        /// <summary>重置</summary>
        public static void Untap(this Entity entity)
        {
            if (entity is Card card) card._isTapped = false;
        }

        #endregion

        #region 属性修改

        /// <summary>修改攻击力</summary>
        public static void ModifyPower(this Entity entity, int amount)
        {
            if (entity is Unit unit) unit.BaseAttack += amount;
            else if (entity is Card card) card._power += amount;
        }

        /// <summary>修改生命值</summary>
        public static void ModifyLife(this Entity entity, int amount)
        {
            if (entity is Player player) player.Life += amount;
            else if (entity is Card card) card._life += amount;
        }

        /// <summary>设置攻击力</summary>
        public static void SetPower(this Entity entity, int value)
        {
            if (entity is Unit unit) unit.BaseAttack = value;
            else if (entity is Card card) card._power = value;
        }

        /// <summary>设置生命值</summary>
        public static void SetLife(this Entity entity, int value)
        {
            if (entity is Player player) player.Life = value;
            else if (entity is Card card) card._life = value;
        }

        #endregion

        #region 区域与控制权

        /// <summary>获取当前区域</summary>
        public static Zone GetZone(this Entity entity)
        {
            if (entity is Card card) return card._zone;
            return Zone.None;
        }

        /// <summary>设置当前区域</summary>
        public static void SetZone(this Entity entity, Zone zone)
        {
            if (entity is Card card) card._zone = zone;
        }

        /// <summary>获取控制者</summary>
        public static Player GetController(this Entity entity)
        {
            if (entity is Card card) return card._controller;
            return null;
        }

        /// <summary>设置控制者</summary>
        public static void SetController(this Entity entity, Player controller)
        {
            if (entity is Card card) card._controller = controller;
        }

        /// <summary>获取拥有者</summary>
        public static Player GetOwner(this Entity entity)
        {
            if (entity is Card card) return card._owner ?? card._controller;
            return null;
        }

        /// <summary>设置拥有者</summary>
        public static void SetOwner(this Entity entity, Player owner)
        {
            if (entity is Card card) card._owner = owner;
        }

        #endregion

        #region 效果标记

        /// <summary>检查效果标记</summary>
        public static bool HasFlag(this Entity entity, EffectTargetFlags flag)
        {
            if (entity is Card card) return (card._targetFlags & flag) == flag;
            return false;
        }

        /// <summary>添加效果标记</summary>
        public static void AddFlag(this Entity entity, EffectTargetFlags flag)
        {
            if (entity is Card card) card._targetFlags |= flag;
        }

        /// <summary>移除效果标记</summary>
        public static void RemoveFlag(this Entity entity, EffectTargetFlags flag)
        {
            if (entity is Card card) card._targetFlags &= ~flag;
        }

        #endregion

        #region 关键词

        /// <summary>
        /// 添加关键词（兼容垫片：不带轨别=Temp——最接近现状的清除语义，换区/净化皆可清）。
        /// duration 形参保留签名兼容；三轨制定案后持续时间由台账轨别承载，不再走 DurationType
        /// （修复旧账：duration 形参自始被忽略，现由 KeywordLane 表达同类语义）。
        /// </summary>
        public static void AddKeyword(this Entity entity, string keyword, DurationType duration = DurationType.Permanent)
            => AddKeyword(entity, keyword, KeywordLane.Temp);

        /// <summary>
        /// 添加关键词（带轨别咽喉）：_keywords 唯一真身（Contains 去重、幂等）；
        /// 台账按关键词全轨唯一——不叠加定案（2026-10-08；2026-10-09 修订彻底不叠加）：
        /// 新实例取代旧实例（值/次数/来源/轨别刷新），**跨轨不并存、互不补充**
        /// （附加状态不补文本份、文本份不补附加份——取代制下双轨只承载存储/清除语义，
        /// 不是两份可各自消耗的存量）。
        /// </summary>
        public static void AddKeyword(this Entity entity, string keyword, KeywordLane lane, Entity source = null)
            => AddKeyword(entity, keyword, lane, source, 1, 1);

        /// <summary>参数化添加关键词（2026-10-07 值化 + 2026-10-08 不叠加 + 2026-10-09 全轨取代）：
        /// 实例值与生效次数入台账（Value≤0 兜底 1；Limit 1/2/3，-1=无限）；
        /// 同关键词**无论轨别**新实例取代旧实例（旧条目移除、新条目入账，不叠加）。</summary>
        public static void AddKeyword(this Entity entity, string keyword, KeywordLane lane, Entity source, int value, int limit)
        {
            if (entity == null) return;
            if (!entity._keywords.Contains(keyword))
                entity._keywords.Add(keyword);
            for (int i = entity._keywordGrants.Count - 1; i >= 0; i--)
            {
                var g = entity._keywordGrants[i];
                if (g.Keyword == keyword)
                    entity._keywordGrants.RemoveAt(i); // 取代制（2026-10-09 全轨）：同关键词旧实例全移除，不限轨别
            }
            entity._keywordGrants.Add(new KeywordGrant
            {
                Keyword = keyword, Lane = lane, Source = source,
                Value = value <= 0 ? 1 : value, Limit = limit,
            });
        }

        /// <summary>关键词实例值（不叠加定案 2026-10-08；2026-10-09 全轨取代）：同关键词全轨仅一实例，
        /// 读当前实例 Value（多实例循环仅兜底旧档，非并存取 Max）；台账缺失（旧档/直改 _keywords）持有即 1。
        /// 运行时消费者已清零（坚韧 2026-10-08 指示物化、守护配对制无限次）——留作不叠加台账读数。</summary>
        public static int GetKeywordValue(this Entity entity, string keyword)
        {
            if (entity == null) return 0;
            if (!entity._keywords.Contains(keyword)) return 0;
            int best = 0;
            foreach (var g in entity._keywordGrants)
                if (g != null && g.Keyword == keyword && g.Value > best)
                    best = g.Value;
            return best > 0 ? best : 1;
        }

        /// <summary>关键词实例生效次数上限/回合（不叠加定案 2026-10-08；2026-10-09 全轨取代）：
        /// 读当前实例 Limit（-1=无限；多实例循环仅兜底旧档，非并存取 Max）；台账缺失兜底=持有即 1。
        /// 光环形态不设闸（恒无限）。</summary>
        public static int GetKeywordLimit(this Entity entity, string keyword)
        {
            if (entity == null) return 0;
            if (!entity._keywords.Contains(keyword)) return 0;
            int best = 0;
            foreach (var g in entity._keywordGrants)
            {
                if (g == null || g.Keyword != keyword) continue;
                if (g.Limit < 0) return -1;
                if (g.Limit > best) best = g.Limit;
            }
            return best > 0 ? best : 1;
        }

        /// <summary>
        /// 移除关键词（消耗型）：撤掉台账实例（2026-10-09 全轨取代制下同关键词仅剩单份，
        /// 倒序取最近授予兼容旧档多份）；消耗即整词撤除，**无跨轨回补**
        ///（文本份不补附加份、附加份不补文本份——再次生效须有新授予=全新实例，非存量接续）。
        /// </summary>
        public static void RemoveKeyword(this Entity entity, string keyword)
        {
            if (entity == null) return;
            for (int i = entity._keywordGrants.Count - 1; i >= 0; i--)
            {
                if (entity._keywordGrants[i].Keyword == keyword)
                {
                    entity._keywordGrants.RemoveAt(i);
                    break;
                }
            }
            bool stillHeld = false;
            foreach (var g in entity._keywordGrants)
                if (g.Keyword == keyword) { stillHeld = true; break; }
            if (!stillHeld)
                entity._keywords.RemoveAll(k => k == keyword);
        }

        /// <summary>检查是否有关键词（角色默认带神佑 DivineProtection；连接光环关键词=光环期间视为持有，
        /// 不进 _keywords、不参与 GetKeywordCount 重复叠加计数）。角色同走连接光环读数
        /// （2026-10-07 定案：关键词光环可经箭头指向角色格投递；属性光环钉死生物专用）。</summary>
        public static bool HasKeyword(this Entity entity, string keyword)
        {
            if (entity == null) return false;
            if (entity._keywords.Contains(keyword)) return true;
            if (entity is Card card) return GameBoard.LinkAuraSystem.HasAuraKeyword(card, keyword);
            if (entity is Player player) return GameBoard.LinkAuraSystem.HasAuraKeyword(player, keyword);
            return false;
        }

        #endregion

        #region 指示物

        /// <summary>
        /// 添加指示物（Entity 级：角色/卡牌同构）。amount 可为负（攻/血/费指示物带符号）。
        /// source = 施加方（指示物来源定案：正量登记，净量归零丢弃；GetCounterSource 查询）。
        /// 回合时钟重载已删（2026-10-08 死路径清理）：限时持续由 CounterSpec 的
        /// Duration/TickPolicy 声明驱动，不再随施加传 turns。
        /// </summary>
        public static void AddCounters(this Entity entity, string counterType, int amount, Entity source = null)
        {
            if (entity == null) return;
            if (!entity._counters.ContainsKey(counterType))
                entity._counters[counterType] = 0;
            entity._counters[counterType] += amount;

            // 来源登记（与计数同生命周期：正量记施加方，净量归零丢弃）
            if (amount > 0 && source != null)
                entity._counterSources[counterType] = source;
            else if (entity._counters[counterType] <= 0)
                entity._counterSources.Remove(counterType);
        }

        /// <summary>查询指示物施加方（最后施加的来源；无来源/未登记返回 null）</summary>
        public static Entity GetCounterSource(this Entity entity, string counterType)
            => entity != null && entity._counterSources.TryGetValue(counterType, out var src) ? src : null;

        /// <summary>获取指示物数量（净量；带符号指示物可为负）</summary>
        public static int GetCounterCount(this Entity entity, string counterType)
        {
            if (entity != null && entity._counters.TryGetValue(counterType, out var count))
                return count;
            return 0;
        }

        /// <summary>移除指示物（计数下限 0）</summary>
        public static void RemoveCounters(this Entity entity, string counterType, int amount)
        {
            if (entity != null && entity._counters.TryGetValue(counterType, out var count))
            {
                entity._counters[counterType] = Math.Max(0, count - amount);
            }
        }

        #endregion

        #region 特殊状态

        /// <summary>
        /// 冻结（2026-09-13 定案；2026-10-08 层即持续定案）：强制横置（一次性动作）+ 放置冻结指示物——
        /// 层=持续回合数，持有者回合末 −1、归零解除（叠加=延长）；持有期间无法重置（BlocksUntap）。
        /// layers：指示物层数=回合数（Value 掷值，缺省 1）。
        /// source = 施加方（2026-10-08 source 补齐：霜蚀光环经此口传 carrier）。
        /// </summary>
        public static void Freeze(this Entity entity, int layers = 1, Entity source = null)
        {
            if (entity is Card card)
            {
                card._isTapped = true;
                card.AddCounters(Attribute.KeywordRules.FreezeCounter, System.Math.Max(1, layers), source);
            }
        }

        /// <summary>是否冻结（冻结指示物 &gt; 0）</summary>
        public static bool IsFrozen(this Entity entity)
        {
            return entity is Card card && card.GetCounterCount(Attribute.KeywordRules.FreezeCounter) > 0;
        }

        /// <summary>移除所有减益：清空全部负面指示物（CounterRules 极性口径；横置不在此恢复）。
        /// 旧 Silenced/Weakened 减益关键词死行已删（2026-10-08）——负面状态统一走指示物（沉默=SilenceCounter）。</summary>
        public static void RemoveAllDebuffs(this Entity entity)
        {
            if (entity is Card card)
            {
                Attribute.CounterRules.ClearNegative(card);
            }
        }

        #endregion

        #region 复制与转化

        /// <summary>创建副本</summary>
        public static Entity CreateCopy(this Entity entity)
        {
            if (entity is Card card)
            {
                // TODO: 实现卡牌复制逻辑
                return null;
            }
            return null;
        }

        /// <summary>无效化</summary>
        public static void Negate(this Entity entity)
        {
            if (entity is Card card) card._isNegated = true;
        }

        /// <summary>无效化卡牌</summary>
        public static void Nullify(this Entity entity)
        {
            if (entity is Card card) card._isNullified = true;
        }

        /// <summary>修改费用</summary>
        public static void ModifyCost(this Entity entity, int amount)
        {
            if (entity is Card card) card._costModifier += amount;
        }

        /// <summary>获取费用</summary>
        public static int GetCost(this Entity entity)
        {
            if (entity is Card card) return card._baseCost + card._costModifier;
            return 0;
        }

        /// <summary>设置基础费用</summary>
        public static void SetCost(this Entity entity, int value)
        {
            if (entity is Card card) card._baseCost = value;
        }

        #endregion

        #region 玩家扩展

        /// <summary>增加额外回合</summary>
        public static void AddExtraTurn(this Player player)
        {
            GameCore.Instance?.TurnEngine?.GrantExtraTurn(player);
        }

        /// <summary>跳过下一回合</summary>
        public static void SkipNextTurn(this Player player)
        {
            GameCore.Instance?.TurnEngine?.SkipNextTurnFor(player);
        }

        #endregion
    }

    /// <summary>
    /// Card扩展字段 - 为Card类添加效果处理所需的私有字段
    /// </summary>
    public partial class Card
    {
        // 战斗属性
        internal int _power = 0;
        internal int _life = 1;
        internal int _maxLife = 1;
        internal int _baseCost = 0;
        internal int _costModifier = 0;

        // 状态
        internal bool _isTapped = false;
        internal bool _isNegated = false;
        internal bool _isNullified = false;
        internal Zone _zone = Zone.None;
        internal Player _controller;
        internal Player _owner;

        // 宣言确认手牌的"已展示"标记：翻开过即公开，未展示卡不可被宣言确认重复指定；
        // 回到手牌（任何来源）即重置为未展示。
        internal bool _isRevealed = false;

        // 灰费豁免转沉睡时长（2026-09-11 定案）：自我沉睡卡打出时灰份额不扣（GetCardCost 剥离），
        // 剥离量暂存于此，登场 Sleep 原子据此赋沉睡指示物（消费后清零）。运行时瞬态，不序列化。
        internal int _pendingSleepGray = 0;
        /// <summary>自我沉睡的灰费豁免量（支付时剥离→入场转沉睡指示物数）。</summary>
        public int PendingSleepGray => _pendingSleepGray;

        // 出牌两阶段（2026-10-04 定案）：带代价的卡声明期先算代价——预选代价目标暂存于此，
        // cast 结算付费步经 CostContext.PreselectedCostTargets 消费（响应窗口内不重选；消费后清空）。
        // 代价无目标时 PlayCard 直接拒发（卡的发动无效）。运行时瞬态，不序列化。
        internal List<Entity> _pendingCostTargets = null;

        // 声明来源区（2026-10-04 本轮定案）：cast 结算期「代价无有效目标→发动失败回退」需要原路退回
        //（手牌路径回手牌；墓地视手牌路径回墓地，不白赚挪区）。声明时写入，回退消费后清空。运行时瞬态，不序列化。
        internal Zone _pendingCastFromZone = Zone.None;

        // 死亡归因留档（2026-09-07 定案）：伤害/流失路径只标死不送墓（落墓由 SBA 泵处理），
        // 死因与死亡来源随尸体留存，SBA/即时路径送墓时经 TryKill 消费并清档。
        // DamageLethal→伤害来源；LifeLoss→效果来源；null=减益/状态动作（无来源）。
        internal Attribute.DeathCause? _pendingDeathCause = null;
        internal Entity _pendingDeathSource = null;

        /// <summary>是否已被宣言确认翻开（已展示；入手时重置）</summary>
        public bool IsRevealed => _isRevealed;

        // 效果标记
        internal EffectTargetFlags _targetFlags = EffectTargetFlags.CanBeTargetedByAll;

        // 关键词和指示物。
        // _keywords 与 _counters 均已上移至 Entity 基类（角色=普通生物单位的
        // 世界观定案：Player 同构持有，角色默认带神佑；剧毒/毒素指示物可指向玩家）。
        // List 而非 HashSet：重复叠加允许（历史例：双坚韧——2026-10-08 指示物化后坚韧走指示物叠加，
        // 关键词份数叠加留给消耗型计数），
        // 普通授予路径的「唯一性」由 AddKeyword 的 Contains 检查保证。

        // ===== 战斗状态（关键词行为；核心规则字段，非棋盘坐标） =====

        // 【定案】随从可用性唯一指标 = 横置（_isTapped）：默认横置入场（冲锋/突袭豁免），
        // 攻击与发动效果各消耗一次横置，己方回合开始（地牌重置后）重置——不另设召唤失调标记。

        /// <summary>本回合已攻击次数台账（纯统计：2026-09-10 横置即上限定案后非发动门槛，
        /// CardPipelineVerifier 激励再攻锚消费此计数；己方回合开始清零）</summary>
        public int AttacksThisTurn { get; set; } = 0;

        /// <summary>临时卡标记（2026-09-11 微缩/放大/回响定案）：复制生成的临时卡回合结束时从手牌移除、
        /// 不可作地牌（CanServeAsLand 守卫）、自身不再触发微缩/放大（防自复制链；回响连锁除外——复制自带回响是设计）。</summary>
        public bool IsTemporary { get; set; } = false;

        /// <summary>衍生物标记（2026-10-03 地牌资格定案）：SummonToken 生成的实例不作地牌
        ///（CanServeAsLand 拒绝——衍生物=真实生物卡实例，CardWrapper 判定拦不住，需显式标记）。
        /// 运行时标记不入序列化（身份/观测/计价全走真实卡数据——快照无需区分）。</summary>
        public bool IsToken { get; set; } = false;

        /// <summary>苏醒已发动（2026-10-03 苏醒改造）：在手牌中预立约后为 true——
        /// 施放费用的灰色份额全免（转入场沉睡层数，GameActions.GetCardCost 闸门）；
        /// 未发动=照常全价。修复旧「使用时生效」死循环（付不起→用不出→无减免）。</summary>
        public bool AwakenCommitted { get; set; } = false;

        /// <summary>
        /// 召唤来源标记：是否经「正式召唤」入场（打出 / 效果召唤 / 复活）。
        /// 仅正式召唤过的随从死亡后才可被 ReturnFromGraveyard 复活；
        /// 被弃牌 / 送墓（本组）等「非正式入墓」的随从该标记为 false，不可复活。
        /// </summary>
        public bool WasFormallySummoned { get; set; } = false;

        // ===== 地牌状态（状态跟随卡：指示物余量记在卡上，回手再入池保留剩余）=====

        /// <summary>作为地牌离池（未耗尽）时的剩余指示物余量；null = 从未离池过</summary>
        internal Dictionary<ManaType, int> _remainingLandTokens = null;

        /// <summary>
        /// 耗尽永久标记：作为地牌产完指示物进墓地后置位。
        /// 置位后任何途径（含墓地回收回手）都不可再次作为地牌产元素 —— 资源枯竭不可逆。
        /// </summary>
        public bool WasDepletedAsLand { get; set; } = false;

        /// <summary>是否有地牌余量状态（离池未耗尽时为 true）</summary>
        public bool HasLandTokenState =>
            _remainingLandTokens != null && _remainingLandTokens.Values.Sum() > 0;

        /// <summary>读取地牌余量副本（无状态时返回 null）</summary>
        public Dictionary<ManaType, int> GetRemainingLandTokens() =>
            _remainingLandTokens == null ? null : new Dictionary<ManaType, int>(_remainingLandTokens);

        /// <summary>写入地牌余量状态（离池时由 ElementPoolSystem 调用）</summary>
        internal void SetRemainingLandTokens(Dictionary<ManaType, int> tokens)
        {
            _remainingLandTokens = tokens == null ? null : new Dictionary<ManaType, int>(tokens);
        }
    }
}
