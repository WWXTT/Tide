using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 原子效果类型（英文枚举作为 key）
    /// 实际顺序：代码中先实现一个原子效果，然后在表中记录该效果，填写基准费用和颜色
    /// </summary>
    public enum AtomicEffectType
    {
        // ============ 伤害与治疗 ============
        DealDamage,
        DealCombatDamage,
        LifeLoss,
        Heal,
        TrampleDamage,
        DamageCannotBePrevented,
        RestoreToFullLife,

        // ============ 卡牌移动 ============
        DrawCard,
        DiscardCard,
        MillCard,
        ReturnToHand,
        PutToBattlefield,
        Destroy,
        Exile,
        ShuffleIntoDeck,
        SearchDeck,
        BounceToTop,
        BounceToBottom,
        MoveToAnyZone,
        ExchangePosition,
        SearchAndReveal,
        SearchAndPlay,
        MoveCard,

        // ============ 状态变更 ============
        Tap,
        Untap,
        ModifyPower,
        ModifyLife,
        SetPower,
        SetLife,
        SwapStats,
        AddCounters,
        DoubleCounters,
        AddKeyword,
        RemoveKeyword,
        FreezePermanent,

        // ============ 资源相关 ============
        AddMana,
        ConsumeMana,
        UntapAll,

        // ============ 控制相关 ============
        GainControl,
        StealControl,
        SwapController,
        PreventDamage,
        CounterSpell,
        CounterTargetSpell,
        NegateActivation,
        NegateEffect,
        RedirectTarget,
        Nullify,
        DrawThenDiscard,
        ScryCards,

        // ============ 保护相关 ============
        GrantHaste,
        GrantRush,
        GrantDoubleStrike,
        GrantMultiAttack,
        GrantTrample,
        GrantReach,
        GrantCannotBeTargeted,
        GrantSpellShield,
        GrantImmunity,
        GrantUnaffected,
        RemoveDebuffs,

        // ============ 破坏效果 ============
        DestroyRandom,

        // ============ 特殊效果 ============
        CreateToken,
        CopyCard,
        CopyExact,
        FightTarget,

        // ============ 反规则效果 ============
        ModifyGameRule,
        OverrideRestriction,

        // ============ 伤害补充 ============
        DrainLife,
        DamageBasedOnStat,
        PoisonousDamage,

        // ============ 移动补充 ============
        ReturnFromGraveyard,
        RecoverToHand,
        LookAtTopCards,
        PutOnBottomOfDeck,
        RevealHand,

        // ============ 状态补充 ============
        ModifyAllStats,
        SetController,
        ChangeOwner,
        ModifyCost,

        // ============ 保护补充 ============
        GrantPoisonous,
        GrantLifesteal,
        GrantStealth,
        GrantWindfury,
        GrantTaunt,
        GrantDivineShield,
        GrantOverwhelm,
        GrantArmor,
        GrantWard,
        GrantFirstStrike,
        GrantFlying,
        GrantVigilance,
        GrantGuard,
        GrantRegeneration,
        GrantGrowth,

        // ============ 控制补充 ============
        CopyTargetAbility,
        TakeExtraTurn,
        SkipTurn,
        ChooseOneEffect,
        RevealCards,
        RandomEffect,

        // ============ 特殊补充 ============
        RepeatEffect,
        DelayedEffect,
    }

    #region 效果分类扩展方法

    /// <summary>
    /// 效果类型扩展方法
    /// </summary>
    public static class AtomicEffectTypeExtensions
    {
       

        /// <summary>
        /// 获取效果描述模板（使用英文枚举作为 key）
        /// </summary>
        public static string GetEffectDescription(AtomicEffectType effectType, int value = 0)
        {
            
            return "";
        }
    }

    #endregion

    #region 原子效果基类

    /// <summary>
    /// 原子效果基类
    /// </summary>
    [Serializable]
    public abstract class AtomicEffectBase : IAtomicEffect
    {
        /// <summary>效果类型</summary>
        public abstract AtomicEffectType EffectType { get; }

        /// <summary>效果数值</summary>
        public int Value;

        /// <summary>效果修正器</summary>
        public List<EffectModifier> Modifiers { get; set; } = new List<EffectModifier>();

        /// <summary>
        /// 执行效果
        /// </summary>
        public abstract void Execute(EffectExecutionContext context);

        /// <summary>
        /// 获取效果描述
        /// </summary>
        public virtual string GetDescription()
        {
            return AtomicEffectTypeExtensions.GetEffectDescription(EffectType, Value);
        }
    }

    /// <summary>
    /// 效果修正器
    /// </summary>
    [Serializable]
    public class EffectModifier
    {
        public int Apply(int baseValue) => baseValue;
    }

    /// <summary>
    /// 单步原子效果的产出结果，供 per-target 条件分支（OutcomeGate）评估。
    /// 以「单个目标」为粒度：每对一个目标执行原子前调用 Reset()，结算时由 handler 写入。
    /// </summary>
    public class EffectOutcome
    {
        /// <summary>受伤/治疗前的生命快照（供 Overkill / Overheal 计算）</summary>
        public int TargetLifeBefore;

        /// <summary>本步实际造成的总伤害</summary>
        public int DamageDealt;

        /// <summary>本步受影响的目标</summary>
        public List<Entity> AffectedTargets = new List<Entity>();

        /// <summary>本步因伤害/归零而死的目标</summary>
        public List<Entity> KilledTargets = new List<Entity>();

        /// <summary>本步实际回复量</summary>
        public int HealApplied;

        /// <summary>治疗溢出量 = 申请治疗 − 实际回复，下限 0</summary>
        public int OverhealAmount;

        /// <summary>受影响目标中是否存在存活者</summary>
        public bool AnySurvived;

        /// <summary>本步是否有目标死亡</summary>
        public bool AnyKilled => KilledTargets.Count > 0;

        public void Reset()
        {
            TargetLifeBefore = 0;
            DamageDealt = 0;
            HealApplied = 0;
            OverhealAmount = 0;
            AnySurvived = false;
            AffectedTargets.Clear();
            KilledTargets.Clear();
        }

        /// <summary>记录一次伤害结算（调用前由 handler 快照 lifeBefore，造成伤害后再调用）。</summary>
        public void RecordDamage(Entity target, int lifeBefore, int dmg)
        {
            if (target == null) return;
            TargetLifeBefore = lifeBefore;
            DamageDealt += dmg;
            AffectedTargets.Add(target);
            if (!target.IsAlive) KilledTargets.Add(target);
            else AnySurvived = true;
        }

        /// <summary>记录一次治疗结算（调用前由 handler 快照 lifeBefore，传入申请治疗量）。</summary>
        public void RecordHeal(Entity target, int lifeBefore, int requested)
        {
            if (target == null) return;
            TargetLifeBefore = lifeBefore;
            int applied = target.GetLife() - lifeBefore;
            if (applied < 0) applied = 0;
            HealApplied += applied;
            int overheal = requested - applied;
            if (overheal > 0) OverhealAmount += overheal;
            AffectedTargets.Add(target);
        }
    }

    /// <summary>
    /// 效果执行上下文
    /// </summary>
    public class EffectExecutionContext
    {
        /// <summary>效果来源实体</summary>
        public Entity Source { get; set; }

        /// <summary>效果控制者</summary>
        public Player Controller { get; set; }

        /// <summary>目标列表</summary>
        public List<Entity> Targets { get; set; } = new List<Entity>();

        /// <summary>主要目标</summary>
        public Entity PrimaryTarget => Targets.Count > 0 ? Targets[0] : null;

        /// <summary>触发事件</summary>
        public IGameEvent TriggeringEvent { get; set; }

        /// <summary>区域管理器</summary>
        public ZoneManager ZoneManager { get; set; }

        /// <summary>元素池系统</summary>
        public ElementPoolSystem ElementPool { get; set; }

        /// <summary>上一步原子效果的产出结果（per-target，由分支步骤读取）</summary>
        public EffectOutcome LastOutcome { get; set; } = new EffectOutcome();

        /// <summary>
        /// 获取效果数值修正后的值
        /// </summary>
        public int GetValueAfterModifiers(int baseValue)
        {
            return baseValue;
        }
    }

    #endregion
}
