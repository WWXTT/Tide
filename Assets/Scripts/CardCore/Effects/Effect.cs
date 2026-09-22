using System;
using System.Collections.Generic;
using UnityEngine;


namespace CardCore
{
    /// <summary>
    /// 效果基类
    /// 卡牌能力效果的抽象基类
    /// </summary>
    public abstract class Effect : ScriptableObject
    {
        /// <summary>
        /// 效果ID
        /// </summary>
        public int EffectId { get; set; }

        /// <summary>
        /// 效果名称
        /// </summary>
        public string EffectName { get; set; }

        /// <summary>
        /// 效果描述
        /// </summary>
        [TextArea]
        public string Description { get; set; }

        /// <summary>
        /// 效果速度
        /// </summary>
        public EffectSpeed Speed { get; set; }

        /// <summary>
        /// 效果类型（持续/诱发/触发等）
        /// </summary>
        public abstract EffectType EffectKind { get; }

        /// <summary>
        /// 效果逻辑
        /// </summary>
        public EffectLogic Logic { get; set; }

        /// <summary>
        /// 效果来源卡牌
        /// </summary>
        public Card SourceCard { get; set; }

        /// <summary>
        /// 效果来源实体
        /// </summary>
        public Entity SourceEntity { get; set; }

        /// <summary>
        /// 来源效果（用于 IStackObject 接口）
        /// </summary>
        public virtual Effect SourceEffect => this;

        /// <summary>
        /// 是否已无效
        /// </summary>
        public bool IsNegated { get; set; }

        /// <summary>
        /// 创建效果实例
        /// </summary>
        public virtual EffectInstance CreateInstance()
        {
            return new EffectInstance
            {
                SourceEffect = this,
                SourceEntity = SourceEntity,
                Targets = new List<Entity>()
            };
        }
    }

    /// <summary>
    /// 效果类型
    /// </summary>
    public enum EffectType
    {
        /// <summary>
        /// 激活式效果（玩家主动发动）
        /// </summary>
        Activated,

        /// <summary>
        /// 触发式效果（满足条件自动触发）
        /// </summary>
        Triggered,

        /// <summary>
        /// 静态效果（持续在场）
        /// </summary>
        Static,

        /// <summary>
        /// 启动时效果
        /// </summary>
        Instant,

        /// <summary>
        /// 持续效果
        /// </summary>
        Continuous
    }

    /// <summary>
    /// 效果实例
    /// 表示一个具体的效果结算实例
    /// 整合了原有的 EffectInstance 和 StackEffectInstance 功能
    /// </summary>
    public class EffectInstance : IStackObject
    {
        /// <summary>
        /// 效果定义（用于基于 EffectDefinition 的效果）
        /// </summary>
        public EffectDefinition Definition { get; set; }

        /// <summary>
        /// 效果定义（用于基于 Effect 的效果）
        /// </summary>
        public Effect SourceEffect { get; set; }

        /// <summary>
        /// 效果来源实体
        /// </summary>
        public Entity Source { get; set; }

        /// <summary>
        /// 来源实体（别名，与 Source 相同）
        /// </summary>
        public Entity SourceEntity { get => Source; set => Source = value; }

        /// <summary>
        /// 目标实体列表
        /// </summary>
        public List<Entity> Targets { get; set; }

        /// <summary>
        /// 效果解析上下文
        /// </summary>
        public EffectResolutionContext EContext { get; set; }

        /// <summary>
        /// 效果控制者
        /// </summary>
        public Player Controller { get; set; }

        /// <summary>施放中的宿主卡（2026-09-22）：卡牌 cast 结算链路上效果所属的卡实例——
        /// 魔法卡效果 Source=角色（来源归因定案），状态门需要卡身份时经此透传（context.CastCard）。
        /// 非施放路径（场上触发/启动式）不填——宿主=Source。</summary>
        public Card CastCard { get; set; }

        /// <summary>
        /// 效果是否已结算
        /// </summary>
        public bool IsResolved { get; set; }

        /// <summary>
        /// 执行完整文本（2026-09-16 描述接口化定案，效果级聚合）：栈结算完成时由执行器写入——
        /// 原子片段经 handler.GetDescription(atom, context) 逐个生成（含目标名与 LastOutcome 真实产出）
        /// 后聚合为一条。UI 栈显示/战报/回放共用。
        /// </summary>
        public string ExecutionSummary { get; set; }

        /// <summary>
        /// 整卡施放标记：本实例代表「一张牌的使用」（打出的卡 = Source），
        /// 由 StackEngine.PushCardCast 创建、GameActions.ResolveCardCastAsync 结算
        /// （付费 → 无效裁决 → 效果 → 离区），不走普通 EffectDefinition 执行路径。
        /// Type 报告为保留值 StackObjectType.Spell。
        /// </summary>
        public bool IsCardCast { get; set; }

        /// <summary>
        /// SBA 伪对象标记（2026-09-15 定案：SBA=速度1栈对象）：本实例代表一批待执行的状态动作
        /// （尸体送墓/判负），由 StackEngine.PushStateAction 创建、ResolveStack 的 IsSBA 分支消费
        /// （SBAEngine.ExecuteAll 到点重查——窗口内被救回的不再执行），
        /// 不走 EffectExecutor（Definition=null 会 throw EffectResolutionException）。
        /// Source/Controller/Definition 恒 null；Type 报告 StackObjectType.Effect；
        /// GetPendingCastCosts 按 IsCardCast=false 跳过、网络快照按 null 安全回落。
        /// </summary>
        public bool IsSBA { get; set; }

        /// <summary>
        /// 攻击宣言标记（2026-09-16 战斗接入栈机器定案）：本实例代表一次攻击宣言——
        /// 速度0栈对象（逐攻击开窗），Source=攻击者、Targets=[宣言目标]、Controller=攻击方；
        /// 由 StackEngine.PushAttackDeclaration 创建、ResolveStack 的 IsAttackDeclaration 分支
        /// 交 CombatSystem.ResolveAttackDeclaration 消费（重检+横置支付→战斗结算；死亡处理由
        /// 栈机器 SBA 轮统一接管）。横置/扣费在**结算时**支付（到点重查，宣言期零支付）。
        /// </summary>
        public bool IsAttackDeclaration { get; set; }

        /// <summary>
        /// 守卫拦截宣言标记（2026-09-16 定案）：守卫=速度1响应（原2速阻挡阶段退役）——
        /// Source=守卫者、Targets=[被拦截的攻击宣言对象]、Controller=防守方；
        /// ResolveStack 交 CombatSystem.ResolveGuardDeclaration 消费：重检+横置支付→
        /// 攻击目标变更（改写攻击宣言对象的 Targets[0]=守卫者）。横置结算时支付（同攻击口径）。
        /// </summary>
        public bool IsGuardDeclaration { get; set; }

        /// <summary>守卫拦截载荷（IsGuardDeclaration 时非空）：被拦截的攻击宣言栈对象——
        /// 守卫结算时改写其 Targets[0]=守卫者（攻击目标变更）。Targets 装不下栈对象（List&lt;Entity&gt;），专用字段承载。</summary>
        public EffectInstance InterceptedAttack { get; set; }

        /// <summary>
        /// 抉择模式索引（2026-09-07 定案）：整卡施放声明期选定（先选择再定费用），
        /// 由 PushCardCast 写入、ResolveCardCastAsync 按此付费、执行引擎按此分派 Choices。
        /// 普通效果实例恒 0；越界由消费方 Clamp（多 Choice 步骤共享卡级索引）。
        /// </summary>
        public int ModeIndex { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 发动速度
        /// </summary>
        public int ActivationSpeed { get; set; }

        /// <summary>
        /// 触发事件
        /// </summary>
        public IGameEvent TriggeringEvent { get; set; }

        /// <summary>
        /// 时间戳信息
        /// </summary>
        public TimestampInfo TimestampInfo { get; set; }

        /// <summary>
        /// 序列号
        /// </summary>
        public uint SequenceNumber => TimestampInfo.Sequence;

        #region IStackObject 实现

        /// <summary>
        /// 栈对象类型
        /// </summary>
        public StackObjectType Type
        {
            get
            {
                if (IsCardCast) return StackObjectType.Spell;
                if (Definition != null)
                    return Definition.IsTriggeredEffect
                        ? StackObjectType.TriggeredAbility
                        : StackObjectType.ActivatedAbility;
                if (SourceEffect != null)
                    return SourceEffect.EffectKind == EffectType.Triggered
                        ? StackObjectType.TriggeredAbility
                        : StackObjectType.ActivatedAbility;
                return StackObjectType.Effect;
            }
        }

        int IStackObject.BaseSpeed => ActivationSpeed;

        bool IStackObject.IsCounter => false;

        #endregion

        public EffectInstance()
        {
            Targets = new List<Entity>();
        }

        /// <summary>
        /// 从待发效果创建实例
        /// </summary>
        public static EffectInstance FromPendingEffect(PendingEffect pending)
        {
            return new EffectInstance
            {
                Definition = pending.Effect,
                Source = pending.Source,
                SourceEffect = null, // PendingEffect 基于 EffectDefinition，不是 Effect
                Controller = pending.Controller,
                ActivationSpeed = pending.ActivationSpeed,
                Targets = new List<Entity>(pending.SelectedTargets),
                TriggeringEvent = pending.TriggeringEvent,
                TimestampInfo = pending.TimestampInfo,
                CreatedAt = pending.TimestampInfo.DateTime,
                IsResolved = false
            };
        }
    }

    /// <summary>
    /// 激活式效果
    /// 玩家可以选择发动的效果
    /// </summary>
    public class ActivatedAbility : Effect, IStackObject
    {
        public override EffectType EffectKind => EffectType.Activated;

        /// <summary>
        /// 栈对象类型
        /// </summary>
        public StackObjectType Type => StackObjectType.ActivatedAbility;

        /// <summary>
        /// 控制者
        /// </summary>
        public Player Controller { get; set; }

        /// <summary>
        /// 基础速度
        /// </summary>
        public int BaseSpeed { get; set; }

        /// <summary>
        /// 是否为响应效果
        /// </summary>
        public bool IsCounter { get; set; }

        /// <summary>
        /// 时间戳信息
        /// </summary>
        public TimestampInfo TimestampInfo { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreationTime => TimestampInfo.DateTime;

        /// <summary>
        /// 序列号
        /// </summary>
        public uint SequenceNumber => TimestampInfo.Sequence;

        /// <summary>
        /// 来源效果（显式实现 IStackObject）
        /// </summary>
        Effect IStackObject.SourceEffect => this;

        /// <summary>
        /// 是否可以在当前阶段发动
        /// </summary>
        public virtual bool canActivateInPhase(PhaseType phase) => true;

        /// <summary>
        /// 检查发动条件
        /// </summary>
        public virtual bool CanActivate()
        {
            // TODO: 检查费用是否足够
            // TODO: 检查目标是否有效
            return !IsNegated;
        }
    }

    /// <summary>
    /// 静态效果
    /// 不进入栈，持续在场的效果
    /// </summary>
    public class StaticAbility : Effect
    {
        public override EffectType EffectKind => EffectType.Static;

        /// <summary>
        /// 层级（用于特征定义能力）
        /// </summary>
        public int Layer { get; set; }
    }

    /// <summary>
    /// 触发式能力
    /// 满足条件自动触发
    /// </summary>
    public class TriggeredAbility : Effect, IStackObject
    {
        public override EffectType EffectKind => EffectType.Triggered;

        /// <summary>控制者</summary>
        public Player Controller { get; set; }

        /// <summary>基础速度</summary>
        public int BaseSpeed { get; set; } = 1;

        /// <summary>触发条件</summary>
        public TriggerTiming TriggerTiming { get; set; }

        #region IStackObject 实现

        public StackObjectType Type => StackObjectType.TriggeredAbility;

        int IStackObject.BaseSpeed => BaseSpeed;

        bool IStackObject.IsCounter => false;

        Effect IStackObject.SourceEffect => this;

        #endregion
    }

    /// <summary>
    /// 持续效果
    /// </summary>
    public class ContinuousEffect : Effect
    {
        public override EffectType EffectKind => EffectType.Continuous;

        /// <summary>
        /// 效果开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 效果结束条件
        /// </summary>
        public DurationCondition DurationCondition { get; set; }

        /// <summary>
        /// 持续时长（帧数）
        /// </summary>
        public int DurationFrames { get; set; }

        /// <summary>
        /// 检查效果是否仍然有效
        /// </summary>
        public bool IsValid()
        {
            // TODO: 检查来源是否在场
            // TODO: 检查持续时间是否到期
            return !IsNegated;
        }
    }

    /// <summary>
    /// 持续条件
    /// </summary>
    public enum DurationCondition
    {
        /// <summary>
        /// 永久持续（直到条件改变）
        /// </summary>
        Permanent,

        /// <summary>
        /// 指定时长
        /// </summary>
        UntilEndOfTurn,

        /// <summary>
        /// 指定帧数
        /// </summary>
        ForFrames,

        /// <summary>
        /// 指定实体在场
        /// </summary>
        WhileEntityInPlay,

        /// <summary>
        /// 指定阶段结束
        /// </summary>
        UntilEndOfPhase
    }
}
