using System;
using System.Collections.Generic;


namespace CardCore
{
    /// <summary>
    /// 效果目标状态标记
    /// </summary>
    [Flags]
    public enum EffectTargetFlags
    {
        None = 0,

        #region 基础可目标性

        /// <summary>可被法术指定</summary>
        CanBeTargetedBySpells = 1 << 0,

        /// <summary>可被异能指定</summary>
        CanBeTargetedByAbilities = 1 << 1,

        /// <summary>可被效果指定</summary>
        CanBeTargetedByEffects = 1 << 2,

        #endregion

        #region 效果免疫类型

        /// <summary>免疫伤害</summary>
        ImmuneToDamage = 1 << 3,

        /// <summary>免疫消灭</summary>
        ImmuneToDestruction = 1 << 4,

        /// <summary>免疫减益</summary>
        ImmuneToDebuff = 1 << 5,

        /// <summary>免疫控制权变更</summary>
        ImmuneToControl = 1 << 6,

        /// <summary>免疫横置</summary>
        ImmuneToTap = 1 << 7,

        /// <summary>免疫冻结</summary>
        ImmuneToFreeze = 1 << 8,

        /// <summary>免疫返回手牌</summary>
        ImmuneToBounce = 1 << 9,

        /// <summary>免疫弃牌</summary>
        ImmuneToDiscard = 1 << 10,

        /// <summary>免疫流放</summary>
        ImmuneToExile = 1 << 11,

        #endregion

        #region 预定义组合

        /// <summary>不可被指定（魔免）- 不可被任何效果指定</summary>
        CannotBeTargeted = None,

        /// <summary>可被所有效果指定（默认）</summary>
        CanBeTargetedByAll = CanBeTargetedBySpells | CanBeTargetedByAbilities | CanBeTargetedByEffects,

        /// <summary>法术免疫</summary>
        SpellImmune = CanBeTargetedByAbilities | CanBeTargetedByEffects,

        /// <summary>异能免疫</summary>
        AbilityImmune = CanBeTargetedBySpells | CanBeTargetedByEffects,

        /// <summary>全抗（不受效果影响）</summary>
        UnaffectedByEffects = ~0,

        /// <summary>伤害免疫</summary>
        DamageImmune = CanBeTargetedByAll | ImmuneToDamage,

        /// <summary>毁灭免疫</summary>
        DestructionImmune = CanBeTargetedByAll | ImmuneToDestruction,

        /// <summary>控制免疫</summary>
        ControlImmune = CanBeTargetedByAll | ImmuneToControl,

        #endregion
    }

    /// <summary>
    /// 效果免疫接口
    /// 实体实现此接口以支持效果免疫
    /// </summary>
    public interface IHasEffectImmunity
    {
        /// <summary>
        /// 目标标记
        /// </summary>
        EffectTargetFlags TargetFlags { get; }

        /// <summary>
        /// 检查是否对指定效果类型免疫
        /// </summary>
        /// <param name="effectType">效果类型</param>
        /// <returns>是否免疫</returns>
        bool IsImmuneTo(AtomicEffectType effectType);

        /// <summary>
        /// 检查是否不受指定效果影响
        /// </summary>
        /// <param name="effectType">效果类型</param>
        /// <returns>是否不受影响</returns>
        bool IsUnaffectedBy(AtomicEffectType effectType);

        /// <summary>
        /// 检查是否可被指定
        /// </summary>
        /// <param name="source">效果来源</param>
        /// <param name="effectType">效果类型</param>
        /// <returns>是否可被指定</returns>
        bool CanBeTargetedBy(Entity source, AtomicEffectType effectType);
    }

    /// <summary>
    /// 效果目标验证器
    /// </summary>
    public static class EffectTargetValidator
    {
        /// <summary>
        /// 检查实体是否可以作为效果目标
        /// </summary>
        /// <param name="target">目标实体</param>
        /// <param name="source">效果来源</param>
        /// <param name="effectType">效果类型</param>
        /// <returns>是否可以作为目标</returns>
        public static bool CanTarget(Entity target, Entity source, AtomicEffectType effectType)
        {
            if (target == null) return false;
            if (!target.IsAlive) return false;

            if (target is IHasEffectImmunity immunity)
            {
                return immunity.CanBeTargetedBy(source, effectType);
            }
            return true;
        }

        /// <summary>
        /// 验证目标实体是否可以作为效果目标，失败时抛出异常
        /// </summary>
        /// <param name="target">目标实体</param>
        /// <param name="source">效果来源</param>
        /// <param name="effectType">效果类型</param>
        /// <exception cref="InvalidTargetException">目标为null、已死亡或不可被指定时抛出</exception>
        public static void ValidateTarget(Entity target, Entity source, AtomicEffectType effectType)
        {
            if (target == null)
                throw new InvalidTargetException(null, $"Target is null for effect type {effectType}.");
            if (!target.IsAlive)
                throw new InvalidTargetException(target, $"Target {target} is not alive and cannot be targeted by {effectType}.");

            if (target is IHasEffectImmunity immunity)
            {
                if (!immunity.CanBeTargetedBy(source, effectType))
                    throw new InvalidTargetException(target, $"Target {target} cannot be targeted by {effectType} from {source} due to immunity.");
            }
        }

        /// <summary>
        /// 检查实体是否受效果影响
        /// </summary>
        /// <param name="target">目标实体</param>
        /// <param name="effectType">效果类型</param>
        /// <returns>是否受效果影响</returns>
        public static bool IsAffectedBy(Entity target, AtomicEffectType effectType)
        {
            if (target == null) return false;

            if (target is IHasEffectImmunity immunity)
            {
                return !immunity.IsUnaffectedBy(effectType);
            }
            return true;
        }

        /// <summary>
        /// 验证实体是否受效果影响，失败时抛出异常
        /// </summary>
        /// <param name="target">目标实体</param>
        /// <param name="effectType">效果类型</param>
        /// <exception cref="InvalidTargetException">目标不受效果影响时抛出</exception>
        public static void ValidateAffectedBy(Entity target, AtomicEffectType effectType)
        {
            if (target == null)
                throw new InvalidTargetException(null, $"Cannot check effect affinity: target is null for effect type {effectType}.");

            if (target is IHasEffectImmunity immunity)
            {
                if (immunity.IsUnaffectedBy(effectType))
                    throw new InvalidTargetException(target, $"Target {target} is unaffected by {effectType}.");
            }
        }

        /// <summary>
        /// 检查实体是否免疫指定效果
        /// </summary>
        /// <param name="target">目标实体</param>
        /// <param name="effectType">效果类型</param>
        /// <returns>是否免疫</returns>
        public static bool IsImmuneTo(Entity target, AtomicEffectType effectType)
        {
            if (target is IHasEffectImmunity immunity)
            {
                return immunity.IsImmuneTo(effectType);
            }
            return false;
        }

        /// <summary>
        /// 获取效果类型的免疫标记
        /// </summary>
        /// <param name="effectType">效果类型</param>
        /// <returns>对应的免疫标记</returns>
        public static EffectTargetFlags GetImmunityFlagForEffect(AtomicEffectType effectType)
        {
            return effectType switch
            {
                // 伤害相关
                AtomicEffectType.DealDamage => EffectTargetFlags.ImmuneToDamage,
                AtomicEffectType.DealCombatDamage => EffectTargetFlags.ImmuneToDamage,
                AtomicEffectType.TrampleDamage => EffectTargetFlags.ImmuneToDamage,
                AtomicEffectType.DamageCannotBePrevented => EffectTargetFlags.ImmuneToDamage,

                // 破坏相关
                AtomicEffectType.Destroy => EffectTargetFlags.ImmuneToDestruction,
                AtomicEffectType.DestroyRandom => EffectTargetFlags.ImmuneToDestruction,

                // 横置相关
                AtomicEffectType.Tap => EffectTargetFlags.ImmuneToTap,

                // 冻结相关
                AtomicEffectType.FreezePermanent => EffectTargetFlags.ImmuneToFreeze,

                // 弹回相关
                AtomicEffectType.ReturnToHand => EffectTargetFlags.ImmuneToBounce,
                AtomicEffectType.BounceToTop => EffectTargetFlags.ImmuneToBounce,
                AtomicEffectType.BounceToBottom => EffectTargetFlags.ImmuneToBounce,

                // 弃牌相关
                AtomicEffectType.DiscardCard => EffectTargetFlags.ImmuneToDiscard,

                // 流放相关
                AtomicEffectType.Exile => EffectTargetFlags.ImmuneToExile,

                // 控制权相关
                AtomicEffectType.GainControl => EffectTargetFlags.ImmuneToControl,
                AtomicEffectType.StealControl => EffectTargetFlags.ImmuneToControl,
                AtomicEffectType.SwapController => EffectTargetFlags.ImmuneToControl,

                // 减益相关
                AtomicEffectType.ModifyPower => EffectTargetFlags.ImmuneToDebuff,
                AtomicEffectType.ModifyLife => EffectTargetFlags.ImmuneToDebuff,
                AtomicEffectType.SetPower => EffectTargetFlags.ImmuneToDebuff,
                AtomicEffectType.SetLife => EffectTargetFlags.ImmuneToDebuff,
                AtomicEffectType.RemoveKeyword => EffectTargetFlags.ImmuneToDebuff,

                // 默认无特定免疫
                _ => EffectTargetFlags.None
            };
        }

        /// <summary>
        /// 检查目标标记是否包含指定免疫
        /// </summary>
        /// <param name="flags">目标标记</param>
        /// <param name="effectType">效果类型</param>
        /// <returns>是否免疫</returns>
        public static bool HasImmunityFlag(EffectTargetFlags flags, AtomicEffectType effectType)
        {
            var immunityFlag = GetImmunityFlagForEffect(effectType);
            if (immunityFlag == EffectTargetFlags.None) return false;
            return (flags & immunityFlag) == immunityFlag;
        }
    }

    /// <summary>
    /// 效果免疫实现基类
    /// 可继承或组合使用
    /// </summary>
    [Serializable]
    public class EffectImmunityHandler : IHasEffectImmunity
    {
        private EffectTargetFlags _targetFlags;
        private HashSet<AtomicEffectType> _additionalImmunities = new HashSet<AtomicEffectType>();
        private HashSet<AtomicEffectType> _additionalUnaffected = new HashSet<AtomicEffectType>();

        /// <summary>
        /// 目标标记
        /// </summary>
        public EffectTargetFlags TargetFlags => _targetFlags;

        /// <summary>
        /// 创建免疫处理器
        /// </summary>
        public EffectImmunityHandler(EffectTargetFlags initialFlags = EffectTargetFlags.CanBeTargetedByAll)
        {
            _targetFlags = initialFlags;
        }

        /// <summary>
        /// 检查是否对指定效果类型免疫
        /// </summary>
        public bool IsImmuneTo(AtomicEffectType effectType)
        {
            // 检查标记免疫
            if (EffectTargetValidator.HasImmunityFlag(_targetFlags, effectType))
                return true;

            // 检查额外免疫列表
            if (_additionalImmunities.Contains(effectType))
                return true;

            return false;
        }

        /// <summary>
        /// 检查是否不受指定效果影响
        /// </summary>
        public bool IsUnaffectedBy(AtomicEffectType effectType)
        {
            // 全抗
            if (_targetFlags == EffectTargetFlags.UnaffectedByEffects)
                return true;

            // 检查额外不受影响列表
            if (_additionalUnaffected.Contains(effectType))
                return true;

            // 如果免疫该效果，也算不受影响
            return IsImmuneTo(effectType);
        }

        /// <summary>
        /// 检查是否可被指定
        /// </summary>
        public bool CanBeTargetedBy(Entity source, AtomicEffectType effectType)
        {
            // 检查基础可目标性
            // 如果没有任何可目标标记，则不可被指定
            if ((_targetFlags & EffectTargetFlags.CanBeTargetedByAll) == EffectTargetFlags.None)
                return false;

            // TODO: 可以根据source判断是否可以指定（例如"不能被对手指定"等）

            return true;
        }

        #region 修改方法

        /// <summary>
        /// 添加免疫标记
        /// </summary>
        public void AddImmunityFlag(EffectTargetFlags flag)
        {
            _targetFlags |= flag;
        }

        /// <summary>
        /// 移除免疫标记
        /// </summary>
        public void RemoveImmunityFlag(EffectTargetFlags flag)
        {
            _targetFlags &= ~flag;
        }

        /// <summary>
        /// 添加对特定效果类型的免疫
        /// </summary>
        public void AddImmunity(AtomicEffectType effectType)
        {
            _additionalImmunities.Add(effectType);
        }

        /// <summary>
        /// 移除对特定效果类型的免疫
        /// </summary>
        public void RemoveImmunity(AtomicEffectType effectType)
        {
            _additionalImmunities.Remove(effectType);
        }

        /// <summary>
        /// 添加不受影响
        /// </summary>
        public void AddUnaffected(AtomicEffectType effectType)
        {
            _additionalUnaffected.Add(effectType);
        }

        /// <summary>
        /// 移除不受影响
        /// </summary>
        public void RemoveUnaffected(AtomicEffectType effectType)
        {
            _additionalUnaffected.Remove(effectType);
        }

        /// <summary>
        /// 设置为魔免（不可被指定）
        /// </summary>
        public void SetUntargetable()
        {
            _targetFlags &= ~EffectTargetFlags.CanBeTargetedByAll;
        }

        /// <summary>
        /// 设置为可被指定
        /// </summary>
        public void SetTargetable()
        {
            _targetFlags |= EffectTargetFlags.CanBeTargetedByAll;
        }

        /// <summary>
        /// 设置为全抗
        /// </summary>
        public void SetUnaffected()
        {
            _targetFlags = EffectTargetFlags.UnaffectedByEffects;
        }

        #endregion
    }

    #region 反规则效果支持

    /// <summary>
    /// 效果免疫授予器
    /// 用于实现 GrantImmunity、GrantUnaffected 等效果
    /// </summary>
    public static class EffectImmunityGranter
    {
        /// <summary>
        /// 授予免疫
        /// </summary>
        /// <param name="target">目标实体</param>
        /// <param name="immunityType">免疫类型</param>
        /// <param name="duration">持续时间（回合数，-1表示永久）</param>
        public static void GrantImmunity(Entity target, EffectTargetFlags immunityType, int duration = -1)
        {
            if (target is Card card && card is IHasEffectImmunityEx immunityEx)
            {
                immunityEx.AddTemporaryImmunity(immunityType, duration);
            }
        }

        /// <summary>
        /// 授予法术护盾（一次性免疫）
        /// </summary>
        public static void GrantSpellShield(Entity target)
        {
            if (target is Card card && card is IHasEffectImmunityEx immunityEx)
            {
                immunityEx.AddSpellShield();
            }
        }

        /// <summary>
        /// 授予不可被指定（魔免）
        /// </summary>
        public static void GrantCannotBeTargeted(Entity target, int duration = -1)
        {
            if (target is Card card && card is IHasEffectImmunityEx immunityEx)
            {
                immunityEx.SetTemporaryUntargetable(duration);
            }
        }
    }

    /// <summary>
    /// 扩展效果免疫接口（支持临时免疫）
    /// </summary>
    public interface IHasEffectImmunityEx : IHasEffectImmunity
    {
        /// <summary>
        /// 添加临时免疫
        /// </summary>
        void AddTemporaryImmunity(EffectTargetFlags immunityType, int duration);

        /// <summary>
        /// 添加法术护盾
        /// </summary>
        void AddSpellShield();

        /// <summary>
        /// 设置临时不可被指定
        /// </summary>
        void SetTemporaryUntargetable(int duration);

        /// <summary>
        /// 回合开始时更新免疫状态
        /// </summary>
        void OnTurnStart();

        /// <summary>
        /// 消耗法术护盾
        /// </summary>
        /// <returns>是否有护盾被消耗</returns>
        bool ConsumeSpellShield();
    }

    #endregion
}
