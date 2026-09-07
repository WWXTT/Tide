using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 原子效果类型（英文枚举作为 key）
    /// 实际顺序：代码中先实现一个原子效果，然后在表中记录该效果，填写基准费用和颜色
    /// 表行三分类（EffectTier）：Atom=可编辑参数的原子效果 / Keyword=授关键词 / Counter=附指示物
    /// </summary>
    public enum AtomicEffectType
    {
        // ============ 伤害与治疗 ============
        DealDamage,
        DealCombatDamage,
        LifeLoss,
        Heal,
        PierceDamage,
        DrainLife,

        // ============ 卡牌移动 ============
        DrawCard,
        DiscardCard,
        MillCard,
        ReturnToHand,
        Exile,
        ShuffleIntoDeck,
        SearchDeck,
        BounceToTop,
        BounceToBottom,

        // ============ 状态变更 ============
        Tap,
        Untap,
        ModifyPower,
        ModifyLife,
        SetPower,
        SetLife,
        SetCost,
        FreezePermanent,
        Purify,
        Weaken,
        Inspire,
        Smash,

        // ============ 控制相关 ============
        GainControl,
        NegateActivation,
        Silence,
        RedirectTarget,
        ScryCards,

        // ============ 保护相关（关键词） ============
        GrantHaste,
        GrantRush,
        GrantDoubleStrike,
        GrantCannotBeTargeted,
        GrantSpellShield,

        // ============ 特殊效果 ============
        Morph,

        // ============ 反规则效果（暂不实现白名单，无表行） ============
        ModifyGameRule,
        OverrideRestriction,

        // ============ 移动补充 ============
        ReturnFromGraveyard,
        RecoverToHand,
        LookAtTopCards,

        // ============ 状态补充 ============
        ChangeOwner,
        ModifyCost,

        // ============ 保护补充（关键词） ============
        GrantPoisonSting,
        GrantLifesteal,
        GrantStealth,
        GrantTaunt,
        GrantDivineShield,
        GrantOverwhelm,
        GrantArmor,
        GrantFirstStrike,
        GrantDisarm,
        Recharging,
        GrantVigilance,
        GrantRegeneration,
        GrantGrowth,

        // ============ 控制补充 ============
        TakeExtraTurn,
        SkipTurn,

        // ============ 信息族（宣言/预言——有限域押注，产出命中与否） ============
        DeclareHand,
        DeclareDeckTop,
        DeclareArrow,
        ProphecyNextCard,

        // ============ 关键词补充 + 指示物原子 ============
        GrantReborn,
        GrantIndestructible,
        GrantLifelink,
        AddArmor,
        AddToxin,
        Poison,
        RushSickness,
        AddVulnerable,
        AddPowerUp,
        AddPowerDown,
        AddLifeUp,
        AddLifeDown,
        AddPlusOne,
        AddMinusOne,
        AddCostUp,
        AddCostDown,

        // ============ 死亡原子（DeathRules 死因已有，补原子层） ============
        Sacrifice,
        Devour,
        Annihilate,

        // ============ 反制原子（使用时点/响应窗口——指向发动区） ============
        KnockDown,

        // ============ 衍生物生成（原 PutToBattlefield 枚举位已收编至此） ============
        SummonToken,
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

        /// <summary>信息族：宣言/预言是否命中（宣言族即时写入；预言族由 ProphecySystem 在验证时刻另行结算）</summary>
        public bool DeclareHit;

        /// <summary>信息族：本步的宣言原文（"维度:值"编码，见 ProphecyDimension）——延迟验证的押注凭据</summary>
        public string Declaration;

        /// <summary>本步是否有目标死亡</summary>
        public bool AnyKilled => KilledTargets.Count > 0;

        public void Reset()
        {
            TargetLifeBefore = 0;
            DamageDealt = 0;
            HealApplied = 0;
            OverhealAmount = 0;
            AnySurvived = false;
            DeclareHit = false;
            Declaration = null;
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

        /// <summary>抉择模式索引（来自 EffectInstance.ModeIndex；执行引擎遇 Choice 步骤按此分派）</summary>
        public int ModeIndex;

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
