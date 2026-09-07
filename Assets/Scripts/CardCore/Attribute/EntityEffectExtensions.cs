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

        /// <summary>获取攻击力</summary>
        public static int GetPower(this Entity entity)
        {
            if (entity is Unit unit) return unit.BaseAttack;
            if (entity is Card card) return card._power;
            return 0;
        }

        /// <summary>获取生命值</summary>
        public static int GetLife(this Entity entity)
        {
            if (entity is Player player) return player.Life;
            if (entity is Card card) return card._life;
            return 0;
        }

        /// <summary>获取最大生命值</summary>
        public static int GetMaxLife(this Entity entity)
        {
            if (entity is Player player) return player.MaxHealth;
            if (entity is Card card) return card._maxLife > 0 ? card._maxLife : card._life;
            return 0;
        }

        /// <summary>获取当前生命值</summary>
        public static int GetCurrentLife(this Entity entity) => entity.GetLife();

        #endregion

        #region 战斗操作

        /// <summary>
        /// 受到伤害（关键词管线）：经 KeywordRules 结算——圣盾挡一次、护甲指示物逐点吸收、
        /// 坚韧 −持有次数、剧毒致死、吸血（恢复自身）/系命（回复角色）。
        /// 事件链（DamageEvent/CombatDamageEvent/LifeChangeEvent/吸血系命）由管线统一按时序发布。
        /// </summary>
        public static void TakeDamage(this Entity entity, int amount, Entity source, bool isCombat = false)
        {
            Attribute.KeywordRules.ApplyDamage(source, entity, amount, isCombat);
        }

        /// <summary>关键词持有次数（List 计数——融合叠加：重复坚韧计 2）</summary>
        public static int GetKeywordCount(this Entity entity, string keyword)
        {
            return entity is Card card ? card._keywords.Count(k => k == keyword) : 0;
        }

        /// <summary>
        /// 治疗（2026-09-07 定案：溢出转生命上限，走 LifeUp 指示物）：
        /// 溢出 X → 加 ceil(X/2) 层「生命值增加」指示物（上限与当前同加——临时上限：
        /// 生物换区清除、角色不换区按默认持续时间 max=整局），当前实得 +floor(X/2)。
        /// 例：满血 30 回复 7 → LifeUp×4 → 上限 34、当前 33（奇数溢出偏向上限）。
        /// 角色与生物同口径；指示物持续/清除规则见 CounterRules（默认 max，特殊标记才有具体时长）。
        /// </summary>
        public static void Heal(this Entity entity, int amount)
        {
            if (entity is Player player)
            {
                int cap = player.MaxHealth;
                int raw = player.Life + amount;
                int over = raw - cap;
                if (over > 0)
                {
                    int layers = (over + 1) / 2;
                    player.AddCounters(Attribute.CounterRules.LifeUpCounter, layers); // 层记录（默认持续时间 max）
                    player.IncreaseMaxHealth(layers);                                  // 层效果：上限+当前同加
                    player.Life = raw - layers;                                        // 实得 = 溢出后原始值 − 层数
                }
                else
                {
                    player.Life = raw;
                }
            }
            else if (entity is Card card)
            {
                if (card._maxLife < card._life) card._maxLife = card._life; // 未初始化兜底：先对齐再判溢出
                int cap = card._maxLife;
                int raw = card._life + amount;
                int over = raw - cap;
                if (over > 0)
                {
                    int layers = (over + 1) / 2;
                    Attribute.CounterRules.AddStatCounter(card, Attribute.CounterRules.LifeUpCounter, layers);
                    card._life = raw - layers; // AddStatCounter 已把当前抬到上限，回写实得（floor 半入当前）
                }
                else
                {
                    card._life = raw;
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

        /// <summary>添加关键词（Entity 级：角色/卡牌同构；duration 当前未实现过期）</summary>
        public static void AddKeyword(this Entity entity, string keyword, DurationType duration = DurationType.Permanent)
        {
            if (entity != null && !entity._keywords.Contains(keyword))
            {
                entity._keywords.Add(keyword);
            }
        }

        /// <summary>移除关键词</summary>
        public static void RemoveKeyword(this Entity entity, string keyword)
        {
            entity?._keywords.Remove(keyword);
        }

        /// <summary>检查是否有关键词（角色默认带神佑 DivineProtection）</summary>
        public static bool HasKeyword(this Entity entity, string keyword)
        {
            return entity != null && entity._keywords.Contains(keyword);
        }

        #endregion

        #region 指示物

        /// <summary>
        /// 添加指示物（Entity 级：角色/卡牌同构）。amount 可为负（攻/血/费指示物带符号）。
        /// source = 施加方（指示物来源定案：正量登记，净量归零丢弃；GetCounterSource 查询）。
        /// </summary>
        public static void AddCounters(this Entity entity, string counterType, int amount, Entity source = null)
            => AddCounters(entity, counterType, amount, -1, source);

        /// <summary>
        /// 添加指示物并附带回合时钟（turns &gt; 0 时每层进 _counterClocks，
        /// 由 CounterRules.OnTurnEnd 逐回合末递减，到期层回收并从计数扣除——毒素层=3）。
        /// </summary>
        public static void AddCounters(this Entity entity, string counterType, int amount, int turns, Entity source = null)
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

            if (turns > 0 && amount > 0)
                entity._counterClocks.Add(new CounterInstance { Id = counterType, Amount = amount, RemainingTurns = turns });

            // 净量归零时丢弃该类指示物的全部时钟（已无对应层）
            if (entity._counters[counterType] <= 0 && entity._counterClocks.Count > 0)
                entity._counterClocks.RemoveAll(c => c.Id == counterType);
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
                if (entity._counters[counterType] == 0 && entity._counterClocks.Count > 0)
                    entity._counterClocks.RemoveAll(c => c.Id == counterType);
            }
        }

        #endregion

        #region 特殊状态

        /// <summary>
        /// 冻结（定案）：强制横置（一次性动作）+ 放置一个负面冻结指示物（持续到回合结束，
        /// 经 CounterRules 统一消退）。不修改回合规则——回合开始横置重置照常。
        /// </summary>
        public static void Freeze(this Entity entity, DurationType duration)
        {
            if (entity is Card card)
            {
                card._isTapped = true;
                card.AddCounters(Attribute.KeywordRules.FreezeCounter, 1);
            }
        }

        /// <summary>是否冻结（冻结指示物 &gt; 0）</summary>
        public static bool IsFrozen(this Entity entity)
        {
            return entity is Card card && card.GetCounterCount(Attribute.KeywordRules.FreezeCounter) > 0;
        }

        /// <summary>移除所有减益：清空全部负面指示物（CounterRules 极性口径；横置不在此恢复）+ 减益关键词。</summary>
        public static void RemoveAllDebuffs(this Entity entity)
        {
            if (entity is Card card)
            {
                Attribute.CounterRules.ClearNegative(card);
                // 移除其他减益关键词
                card._keywords.Remove("Silenced");
                card._keywords.Remove("Weakened");
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
        // _keywords 与 _counters/_counterClocks 均已上移至 Entity 基类（角色=普通生物单位的
        // 世界观定案：Player 同构持有，角色默认带神佑；剧毒/毒素指示物可指向玩家）。
        // List 而非 HashSet：融合继承允许重复叠加（双坚韧 = −2），
        // 普通授予路径的「唯一性」由 AddKeyword 的 Contains 检查保证（非融合不可重复添加）。

        // ===== 战斗状态（关键词行为；核心规则字段，非棋盘坐标） =====

        // 【定案】随从可用性唯一指标 = 横置（_isTapped）：默认横置入场（冲锋/突袭豁免），
        // 攻击与发动效果各消耗一次横置，己方回合开始（地牌重置后）重置——不另设召唤失调标记。

        /// <summary>本回合已攻击次数（上限 1，风怒 = 2；己方回合开始清零）</summary>
        public int AttacksThisTurn { get; set; } = 0;

        /// <summary>警戒：本回合横置抵消额度已消耗（一回合只生效一次）</summary>
        internal bool _vigilanceUsedThisTurn = false;

        /// <summary>
        /// 召唤来源标记：是否经「正式召唤」入场（普通召唤 / 特殊召唤 / 从额外组召唤）。
        /// 仅正式召唤过的随从死亡后才可被 ReturnFromGraveyard 复活；
        /// 被弃牌 / 送墓（本组）/ 送额外组等「非正式入墓」的随从该标记为 false，不可复活。
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
