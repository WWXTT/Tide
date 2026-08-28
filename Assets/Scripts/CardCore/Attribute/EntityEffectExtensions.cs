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

        /// <summary>受到伤害</summary>
        public static void TakeDamage(this Entity entity, int amount)
        {
            if (entity is Player player)
            {
                player.Life -= amount;
            }
            else if (entity is Card card)
            {
                card._life -= amount;
                if (card._life <= 0)
                {
                    entity.IsAlive = false;
                }
            }
        }

        /// <summary>治疗</summary>
        public static void Heal(this Entity entity, int amount)
        {
            if (entity is Player player)
            {
                player.Life = Math.Min(player.Life + amount, player.MaxHealth);
            }
            else if (entity is Card card)
            {
                card._life = Math.Min(card._life + amount, card._maxLife > 0 ? card._maxLife : card._life + amount);
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

        /// <summary>添加关键词</summary>
        public static void AddKeyword(this Entity entity, string keyword, DurationType duration = DurationType.Permanent)
        {
            if (entity is Card card)
            {
                if (!card._keywords.Contains(keyword))
                {
                    card._keywords.Add(keyword);
                }
            }
        }

        /// <summary>移除关键词</summary>
        public static void RemoveKeyword(this Entity entity, string keyword)
        {
            if (entity is Card card)
            {
                card._keywords.Remove(keyword);
            }
        }

        /// <summary>检查是否有关键词</summary>
        public static bool HasKeyword(this Entity entity, string keyword)
        {
            if (entity is Card card) return card._keywords.Contains(keyword);
            return false;
        }

        #endregion

        #region 指示物

        /// <summary>添加指示物</summary>
        public static void AddCounters(this Entity entity, string counterType, int amount)
        {
            if (entity is Card card)
            {
                if (!card._counters.ContainsKey(counterType))
                    card._counters[counterType] = 0;
                card._counters[counterType] += amount;
            }
        }

        /// <summary>获取指示物数量</summary>
        public static int GetCounterCount(this Entity entity, string counterType)
        {
            if (entity is Card card && card._counters.TryGetValue(counterType, out var count))
                return count;
            return 0;
        }

        /// <summary>移除指示物</summary>
        public static void RemoveCounters(this Entity entity, string counterType, int amount)
        {
            if (entity is Card card && card._counters.TryGetValue(counterType, out var count))
            {
                card._counters[counterType] = Math.Max(0, count - amount);
            }
        }

        #endregion

        #region 特殊状态

        /// <summary>冻结</summary>
        public static void Freeze(this Entity entity, DurationType duration)
        {
            if (entity is Card card) card._isFrozen = true;
        }

        /// <summary>是否冻结</summary>
        public static bool IsFrozen(this Entity entity)
        {
            if (entity is Card card) return card._isFrozen;
            return false;
        }

        /// <summary>添加护甲</summary>
        public static void AddArmor(this Entity entity, int amount)
        {
            if (entity is Card card) card._armor += amount;
        }

        /// <summary>添加伤害防止</summary>
        public static void AddDamagePrevention(this Entity entity, int amount, DurationType duration)
        {
            if (entity is Card card) card._damagePrevention += amount;
        }

        /// <summary>移除所有减益</summary>
        public static void RemoveAllDebuffs(this Entity entity)
        {
            if (entity is Card card)
            {
                card._isFrozen = false;
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
        internal int _armor = 0;
        internal int _damagePrevention = 0;

        // 状态
        internal bool _isTapped = false;
        internal bool _isFrozen = false;
        internal bool _isNegated = false;
        internal bool _isNullified = false;
        internal Zone _zone = Zone.None;
        internal Player _controller;
        internal Player _owner;

        // 效果标记
        internal EffectTargetFlags _targetFlags = EffectTargetFlags.CanBeTargetedByAll;

        // 关键词和指示物
        internal HashSet<string> _keywords = new HashSet<string>();
        internal Dictionary<string, int> _counters = new Dictionary<string, int>();

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
