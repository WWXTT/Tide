using System;
using System.Collections.Generic;


namespace CardCore
{
    /// <summary>
    /// 效果基类
    /// 卡牌能力效果的抽象基类
    /// </summary>
    public abstract class Effect
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
        /// 主动效果（玩家主动发动）
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

        /// <summary>参照单位列表（步5 2026-11-08 目标选择重构 §3.3）：声明期 Reference spec 选位
        /// 显式随实例入栈——网络意图/重放/栈快照按角色携带（不混进 Targets 平铺数组）；执行器把它
        /// 种进 context.Refs（v1 单参照=首元素）。null/空=无参照（普通效果）。</summary>
        public List<Entity> Refs { get; set; }

        /// <summary>动态数量（2026-11-14）：声明期收集的每原子目标数（下标=效果 def.Effects，
        /// 0=非动态）。随 cast 入栈——结算期扣费（ResolveCardCastAsync→GetCardCost）与执行
        /// （EffectExecutor 应用后按 N 消费预选队列）同表，防「选 N 个却按 base 收」。
        /// null/空=无动态数量（行为与旧实例一致）。</summary>
        public List<int> DynamicCounts { get; set; }

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
        /// 非施放路径（场上触发/主动）不填——宿主=Source。</summary>
        public Card CastCard { get; set; }

        /// <summary>
        /// 效果是否已结算
        /// </summary>
        public bool IsResolved { get; set; }

        /// <summary>发动层无效（2026-10-04 两层无效定案，NegateActivation 原子标记）：结算时净零成本跳过——
        /// 主动不扣费（横置为声明期代价不重置）、触发式不计发动次数（入栈记账退还）。
        /// 强制桶（Mandatory）不可被标记（不受发动无效管制）。</summary>
        public bool IsActivationNegated { get; set; }

        /// <summary>效果层无效（NegateEffect 原子标记）：发动照常（计费/计次/潜行移除照走），效果不结算。
        /// 强制桶同样可被标记（受效果无效管制）。</summary>
        public bool IsEffectNegated { get; set; }

        /// <summary>发动方式（FromPendingEffect 透传）：两层无效的管制面判定——
        /// 强制桶不受发动无效管制、受效果无效管制。</summary>
        public EffectActivationType ActivationType { get; set; }

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
        /// 响应绑定（E1-1 2026-11-04 定案·游戏王口径）：入栈瞬间捕获的**当时栈顶**（空栈=null）。
        /// 反制语义的唯一锚——「要反制谁就必须跟在谁后面发动」：反制原子执行时不做目标选择，
        /// 直接对本条目打 IsActivationNegated/IsEffectNegated。随谁入栈就绑谁——普通施放/效果
        /// 入栈同样捕获（链条 A←B←C 的 RespondedEntry 链即连锁链）。**不参与序列化/观测**（栈内瞬态引用）。
        /// </summary>
        public EffectInstance RespondedEntry { get; set; }

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
        /// 阶段窗伪对象标记（2026-11-01 定案：0 速伪栈对象开阶段优先权窗；同日修订双窗时点）：
        /// 本实例不代表任何效果/宣言，仅为「结束阶段开始」（GameActions.EndTurn）与「主阶段开始」
        /// （GameCore.OnMainPhaseStartedWindow 订阅 PhaseStartEvent(Main)——原「回合结束前」废弃）
        /// 两个时点开响应窗（双方 Pass=让路，阶段推进在窗口外由调用链/帧泵承担）。
        /// 由 StackEngine.PushPhaseWindow 创建、ResolveStack 的 IsPhaseWindow 分支消费（到点自动
        /// 消失——不结算、不重查、不发事件）。0 速不抬记速器：窗口内响应门槛=速≥1 且 速≥0
        /// （1 速手牌/主动效果可入，同空栈基线）。Source/Definition 恒 null（Controller=优先权
        /// 起点，仅供描述层）；Type 报告 StackObjectType.Effect；网络快照按 null 安全回落（同 SBA）。
        /// </summary>
        public bool IsPhaseWindow { get; set; }

        /// <summary>阶段窗时点名（IsPhaseWindow 时由 PushPhaseWindow 写入，可空）：「结束阶段开始」/
        /// 「主阶段开始」——描述层（响应弹窗「在响应什么」/回放标签）区分两个窗口用。</summary>
        public string PhaseWindowLabel { get; set; }

        /// <summary>
        /// 攻击宣言标记（2026-09-16 战斗接入栈机器定案）：本实例代表一次攻击宣言——
        /// 速度0栈对象（逐攻击开窗），Source=攻击者、Targets=[宣言目标]、Controller=攻击方；
        /// 由 StackEngine.PushAttackDeclaration 创建、ResolveStack 的 IsAttackDeclaration 分支
        /// 交 CombatSystem.ResolveAttackDeclaration 消费（重检→战斗结算→战斗后横置支付；死亡
        /// 处理由栈机器 SBA 轮统一接管）。横置/扣费在**结算时**支付（到点重查，宣言期零支付）——
        /// 2026-10-15 起攻击者以可用状态完成战斗，横置移到战斗结算完成后支付。
        /// </summary>
        public bool IsAttackDeclaration { get; set; }

        /// <summary>
        /// 守卫拦截宣言标记（2026-09-16 定案）：守卫=速度1响应（原2速阻挡阶段退役）——
        /// Source=守卫者、Targets=[被拦截的攻击宣言对象]、Controller=防守方；
        /// ResolveStack 交 CombatSystem.ResolveGuardDeclaration 消费：重检→攻击目标变更
        ///（改写攻击宣言对象的 Targets[0]=守卫者+登记 GuardBlocker）。守卫横置不再在此支付——
        /// 2026-10-15 定案：战斗期间守卫可用（照常反击），横置随攻击结算在战斗完成后统一支付。
        /// </summary>
        public bool IsGuardDeclaration { get; set; }

        /// <summary>守卫拦截载荷（IsGuardDeclaration 时非空）：被拦截的攻击宣言栈对象——
        /// 守卫结算时改写其 Targets[0]=守卫者（攻击目标变更）。Targets 装不下栈对象（List&lt;Entity&gt;），专用字段承载。</summary>
        public EffectInstance InterceptedAttack { get; set; }

        /// <summary>拦截本攻击宣言的守卫者（ResolveGuardDeclaration 登记；多次拦截只记
        /// 最后一位=最终目标，前序拦截者未参战不支付）：攻击结算在战斗完成后据此给守卫补横置。
        /// 攻击落空（重检失败）时守卫未参战——不支付。</summary>
        public Entity GuardBlocker { get; set; }

        /// <summary>攻击宣言的原目标（PushAttackDeclaration 写入；守卫改写 Targets[0] 后保留）：
        /// 2026-10-16 碾压改版——溅射枢轴恒取原目标（战斗发生在原被攻击者那一格，守卫赴挡只拦
        /// 主战斗伤害、不改溅射域）；攻角色时原目标=角色（Player），溅射域=角色格左右邻格生物。</summary>
        public Entity OriginalAttackTarget { get; set; }

        /// <summary>
        /// 抉择模式索引（2026-09-07 定案）：整卡施放声明期选定（先选择再定费用），
        /// 由 PushCardCast 写入、ResolveCardCastAsync 按此付费、执行引擎按此分派 Choices。
        /// 普通效果实例恒 0；越界由消费方 Clamp（多 Choice 步骤共享卡级索引）。
        /// </summary>
        public int ModeIndex { get; set; }

        /// <summary>
        /// 充能宣言标记（2026-11-05 充能系统动作化改版）：充能=速度0规则动作（仿攻击宣言）——
        /// 任意手牌卡一回合一次可宣，Source=手牌卡、Controller=充能方；宣言期零支付零选色，
        /// ResolveStack 交 GameActions.ResolveChargeDeclarationAsync 消费（结算期反问元素类别
        /// ——下拉只列该卡具有且未充满的颜色，确定后 +1 层同色费用降低指示物并展示）。
        /// 旧 P12「充能=卡上 Charge 原子主动效果」整链已退役（ChargeHandler/PendingEffect.ChargeColor）。
        /// </summary>
        public bool IsChargeDeclaration { get; set; }

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
                ActivationType = pending.ActivationType, // 两层无效管制面判定（2026-10-04）
                Targets = new List<Entity>(pending.SelectedTargets),
                TriggeringEvent = pending.TriggeringEvent,
                TimestampInfo = pending.TimestampInfo,
                CreatedAt = pending.TimestampInfo.DateTime,
                IsResolved = false
            };
        }
    }

    /// <summary>
    /// 主动效果
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
