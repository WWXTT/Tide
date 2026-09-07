using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    #region 栈对象接口

    /// <summary>
    /// 栈对象接口
    /// </summary>
    public interface IStackObject
    {
        StackObjectType Type { get; }
        int BaseSpeed { get; }
        bool IsCounter { get; }
        Effect SourceEffect { get; }
    }

    public enum StackObjectType
    {
        ActivatedAbility,
        TriggeredAbility,
        Spell,
        Effect,
        AttackDeclaration
    }

    #endregion

    #region 效果定义

    /// <summary>
    /// 效果定义
    /// 完整的效果配置，可在卡牌构建界面配置
    /// </summary>
    [Serializable]
    public partial class EffectDefinition
    {
        #region 基本信息
        public string Id;
        public string DisplayName;
        public string Description;
        #endregion

        #region 速度系统
        /// <summary>
        /// 卡牌速度加成（编辑阶段设置，默认0）
        /// 最终速度 = 默认(阶段+回合归属) + BaseSpeed + 动态支付
        /// </summary>
        public int BaseSpeed = 0;

        /// <summary>发动类型</summary>
        public EffectActivationType ActivationType = EffectActivationType.Voluntary;
        #endregion

        #region 触发相关
        public TriggerTiming TriggerTiming;
        public List<ActivationCondition> TriggerConditions = new List<ActivationCondition>();
        #endregion

        #region 发动条件
        public List<ActivationCondition> ActivationConditions = new List<ActivationCondition>();
        #endregion

        #region 元数据
        public bool IsOptional = false;
        public DurationType Duration = DurationType.Permanent;
        public List<AtomicEffectInstance> Effects = new List<AtomicEffectInstance>();
        public List<CostInstance> Costs = new List<CostInstance>();
        public List<string> Tags = new List<string>();
        public string SourceCardId;

        /// <summary>
        /// 卡内效果标记：元素费已随卡牌费用收讫（打出按档位+构筑期代价抵扣，见 CardCostService），
        /// 执行器跳过元素费支付以防双计。由 CardEffectConverter / KeywordEffectMapper 在卡效果转换时置位；
        /// 游戏中途授予的动态效果不置位，照常支付。（衍生物 CreateToken 直接入场的卡内效果同样免付——设计内，强度由生成器定价。）
        /// </summary>
        public bool ElementCostPrepaid = false;
        public EffectTargetType TargetType;

        /// <summary>
        /// 节点化效果步骤（原子 + 单层 per-target 条件分支）。
        /// 非空时执行引擎走步骤遍历；为空时退化为扁平 Effects 线性结算（向后兼容）。
        /// </summary>
        public List<RuntimeEffectStep> Steps = new List<RuntimeEffectStep>();
        #endregion

        #region 方法

        /// <summary>
        /// 计算实际发动速度
        /// 三层叠加: 默认(阶段+回合归属) + BaseSpeed + 动态支付
        /// </summary>
        public int CalculateActivationSpeed(Player activator, Player activePlayer, PhaseType phase, int paidBoost = 0)
        {
            int defaultSpeed = SpeedCalculator.GetDefaultSpeed(activator, activePlayer, phase);
            return SpeedCalculator.CalculateSpeed(defaultSpeed, BaseSpeed, paidBoost);
        }

        public string GetFullDescription()
        {
            var parts = new List<string>();
            if (IsTriggeredEffect)
                parts.Add($"[{GetTriggerDescription()}]");
            if (ActivationType == EffectActivationType.Mandatory)
                parts.Add("[强制]");
            return string.Join("：", parts);
        }

        private string GetTriggerDescription()
        {
            return TriggerTiming switch
            {
                TriggerTiming.Activate_Active => "主要阶段发动",
                TriggerTiming.Activate_Instant => "瞬间发动",
                TriggerTiming.Activate_Response => "响应发动",
                TriggerTiming.OnPlay => "登场",
                TriggerTiming.OnSummon => "进场时",
                TriggerTiming.OnOtherCreatureEnter => "其他生物进场时",
                TriggerTiming.OnDeath => "死亡时",
                TriggerTiming.OnDestroy => "破坏时",
                TriggerTiming.OnExile => "除外时",
                TriggerTiming.OnReturnFromGraveyard => "从墓地回到战场时",
                TriggerTiming.OnLeaveBattlefield => "离场时",
                TriggerTiming.OnDraw => "抽牌时",
                TriggerTiming.OnDealDamage => "造成伤害时",
                TriggerTiming.OnTakeDamage => "受到伤害时",
                TriggerTiming.OnTurnStart => "回合开始时",
                TriggerTiming.OnTurnEnd => "回合结束时",
                TriggerTiming.OnPhaseStart => "阶段开始时",
                TriggerTiming.OnPhaseEnd => "阶段结束时",
                TriggerTiming.OnAttack => "攻击宣言时",
                TriggerTiming.OnAttacked => "被攻击时",
                TriggerTiming.OnBlockDeclare => "阻拦宣言时",
                TriggerTiming.OnCardPlayed => "使用卡牌时",
                TriggerTiming.OnSpellCast => "施放法术时",
                TriggerTiming.OnTap => "横置时",
                TriggerTiming.OnUntap => "重置时",
                TriggerTiming.OnTargeted => "被指定为目标时",
                TriggerTiming.OnGameStart => "游戏开始时",
                TriggerTiming.OnMaterialDetach => "超量素材取除时",
                TriggerTiming.OnAtomicEffectActivation => "原子效果发动时",
                TriggerTiming.OnAtomicEffectStartApplying => "原子效果开始作用时",
                TriggerTiming.OnAtomicEffectResolution => "原子效果结算完成时",
                _ => TriggerTiming.ToString()
            };
        }

        public bool IsTriggeredEffect =>
            TriggerTiming != TriggerTiming.Activate_Active &&
            TriggerTiming != TriggerTiming.Activate_Instant &&
            TriggerTiming != TriggerTiming.Activate_Response;

        public bool IsActivatedEffect =>
            TriggerTiming == TriggerTiming.Activate_Active ||
            TriggerTiming == TriggerTiming.Activate_Instant ||
            TriggerTiming == TriggerTiming.Activate_Response;

        #endregion
    }

    #endregion

    #region 待发效果

    /// <summary>
    /// 待发效果
    /// </summary>
    public class PendingEffect : ITimestamped
    {
        public EffectDefinition Effect { get; set; }
        public Entity Source { get; set; }
        public Player Controller { get; set; }
        public int ActivationSpeed { get; set; }
        public EffectActivationType ActivationType { get; set; }
        public TimestampInfo TimestampInfo { get; set; }
        public DateTime CreationTime => TimestampInfo.DateTime;
        public uint SequenceNumber => TimestampInfo.Sequence;
        public IGameEvent TriggeringEvent { get; set; }
        public List<Entity> SelectedTargets { get; set; } = new List<Entity>();
        public int PaidSpeedBoost { get; set; }
        public bool IsOnStack { get; set; }

        /// <summary>
        /// 创建待发效果
        /// 条件发动 speed=Max 绕过速度检查
        /// 速度发动 三层叠加: 默认 + BaseSpeed + paidBoost
        /// </summary>
        public static PendingEffect Create(
            EffectDefinition effect,
            Entity source,
            Player controller,
            Player activePlayer,
            PhaseType currentPhase,
            int paidBoost = 0,
            IGameEvent triggeringEvent = null)
        {
            int speed = (effect.ActivationType != EffectActivationType.Voluntary)
                ? int.MaxValue
                : SpeedCalculator.CalculateSpeed(
                    SpeedCalculator.GetDefaultSpeed(controller, activePlayer, currentPhase),
                    effect.BaseSpeed,
                    paidBoost);

            return new PendingEffect
            {
                Effect = effect,
                Source = source,
                Controller = controller,
                ActivationSpeed = speed,
                ActivationType = effect.ActivationType,
                TimestampInfo = TimestampSystem.CreateTimestamp(),
                TriggeringEvent = triggeringEvent,
                PaidSpeedBoost = paidBoost,
                IsOnStack = false
            };
        }
    }

    #endregion

    #region 待发效果队列

    /// <summary>
    /// 待发效果队列
    /// 管理等待入栈的效果
    /// </summary>
    public class PendingEffectQueue
    {
        private List<PendingEffect> _mandatoryEffects = new List<PendingEffect>();
        private List<PendingEffect> _automaticEffects = new List<PendingEffect>();
        private List<PendingEffect> _voluntaryEffects = new List<PendingEffect>();
        private SpeedCounter _speedCounter;

        public PendingEffectQueue(SpeedCounter speedCounter)
        {
            _speedCounter = speedCounter;
        }

        /// <summary>
        /// 添加待发效果
        /// 条件发动不检查速度直接入队
        /// </summary>
        public void AddPendingEffect(PendingEffect effect)
        {
            switch (effect.ActivationType)
            {
                case EffectActivationType.Mandatory:
                    _mandatoryEffects.Add(effect);
                    break;
                case EffectActivationType.Automatic:
                    _automaticEffects.Add(effect);
                    break;
                case EffectActivationType.Voluntary:
                    _voluntaryEffects.Add(effect);
                    break;
            }
        }

        /// <summary>
        /// 获取下一个要入栈的条件发动效果（仅强制/自动）
        /// </summary>
        public PendingEffect GetNextEffect()
        {
            if (_mandatoryEffects.Count > 0)
                return PopHighestSpeed(_mandatoryEffects);
            if (_automaticEffects.Count > 0)
                return PopHighestSpeed(_automaticEffects);
            return null;
        }

        /// <summary>
        /// 获取可发动的速度效果列表（speed > counter）
        /// </summary>
        public List<PendingEffect> GetVoluntaryEffects()
        {
            return _voluntaryEffects
                .Where(e => e.ActivationSpeed > _speedCounter.CurrentSpeed)
                .OrderByDescending(e => e.ActivationSpeed)
                .ThenBy(e => e.SequenceNumber)
                .ToList();
        }

        /// <summary>
        /// 玩家选择发动效果
        /// </summary>
        public PendingEffect PlayerChooseEffect(PendingEffect effect)
        {
            if (_voluntaryEffects.Remove(effect))
                return effect;
            return null;
        }

        public void RemoveEffect(PendingEffect effect)
        {
            _mandatoryEffects.Remove(effect);
            _automaticEffects.Remove(effect);
            _voluntaryEffects.Remove(effect);
        }

        public void Clear()
        {
            _mandatoryEffects.Clear();
            _automaticEffects.Clear();
            _voluntaryEffects.Clear();
        }

        public bool HasPendingEffects =>
            _mandatoryEffects.Count > 0 ||
            _automaticEffects.Count > 0 ||
            _voluntaryEffects.Count > 0;

        public bool HasAutoEffects =>
            _mandatoryEffects.Count > 0 ||
            _automaticEffects.Count > 0;

        private PendingEffect PopHighestSpeed(List<PendingEffect> list)
        {
            if (list.Count == 0) return null;

            var sorted = list
                .OrderByDescending(e => e.ActivationSpeed)
                .ThenBy(e => e.TimestampInfo.Sequence)
                .ToList();

            var result = sorted.First();
            list.Remove(result);
            return result;
        }
    }

    #endregion

    #region 触发时点映射

    /// <summary>
    /// 触发时点默认配置
    /// </summary>
    public static class TriggerTimingDefaults
    {
        public static EffectActivationType GetDefaultActivationType(TriggerTiming timing)
        {
            return timing switch
            {
                TriggerTiming.Activate_Active => EffectActivationType.Voluntary,
                TriggerTiming.Activate_Instant => EffectActivationType.Voluntary,
                TriggerTiming.Activate_Response => EffectActivationType.Voluntary,
                _ => EffectActivationType.Automatic
            };
        }

        /// <summary>
        /// 时点 → 锚定事件唯一映射表（时点接线定案）。
        ///
        /// 观察者语义对照（与 TriggerTiming 双泳道注释配套）：
        /// - CardEnterActivationEvent = 发动开始（进发动区，反制窗口锚点，含 FromZone）
        /// - CardPlayEvent = 使用宣言（付费前）：OnCardPlayed / OnSpellCast 锚点；FromZone 区分手牌/墓地（IPlaySource）打出
        /// - CardPutToBattlefieldEvent = 入场（已在战场容器）：
        ///     OnSummon 任意来源（超集：打出也触发）/ OnPlay=登场 仅 CastPlayed /
        ///     OnOtherCreatureEnter 进场的不是自己 / OnReturnFromGraveyard 仅 Revived——EnterSource 区分
        /// 三来源对号：①手牌=EnterActivation(Hand)→CardPlay(Hand)→PutToBattlefield(CastPlayed)；
        /// ②效果召唤/复活/token=直接 PutToBattlefield(SummonedByEffect/Revived/TokenSpawned)，token 另发 TokenCreatedEvent；
        /// ③墓地经 IPlaySource=EnterActivation(Graveyard)→CardPlay(Graveyard)→PutToBattlefield(CastPlayed)。
        /// 类型匹配后的 self/来源/施受区分见 TriggerPayloadFilter。
        /// </summary>
        public static Type GetEventType(TriggerTiming timing)
        {
            return timing switch
            {
                // 登场/进场族：锚 CardPutToBattlefieldEvent，payload 过滤区分 self/来源
                TriggerTiming.OnPlay => typeof(CardPutToBattlefieldEvent),
                TriggerTiming.OnSummon => typeof(CardPutToBattlefieldEvent),
                TriggerTiming.OnOtherCreatureEnter => typeof(CardPutToBattlefieldEvent),
                TriggerTiming.OnReturnFromGraveyard => typeof(CardPutToBattlefieldEvent),

                // 死亡/离场/除外族
                TriggerTiming.OnDeath => typeof(CardDestroyEvent),
                TriggerTiming.OnDestroy => typeof(CardDestroyEvent),
                TriggerTiming.OnExile => typeof(CardExileEvent),
                TriggerTiming.OnLeaveBattlefield => typeof(CardLeaveBattlefieldEvent),

                // 资源族
                TriggerTiming.OnDraw => typeof(CardDrawEvent),
                TriggerTiming.OnDealDamage => typeof(DamageEvent),
                TriggerTiming.OnTakeDamage => typeof(DamageEvent),

                // 回合/阶段族
                TriggerTiming.OnTurnStart => typeof(TurnStartEvent),
                TriggerTiming.OnTurnEnd => typeof(TurnEndEvent),
                TriggerTiming.OnPhaseStart => typeof(PhaseStartEvent),
                TriggerTiming.OnPhaseEnd => typeof(PhaseEndEvent),
                TriggerTiming.OnGameStart => typeof(GameStartEvent),

                // 战斗族
                TriggerTiming.OnAttack => typeof(AttackDeclarationEvent),
                TriggerTiming.OnAttacked => typeof(AttackDeclarationEvent),
                TriggerTiming.OnBlockDeclare => typeof(BlockDeclarationEvent),

                // 使用宣言观察族（付费前）
                TriggerTiming.OnCardPlayed => typeof(CardPlayEvent),
                TriggerTiming.OnSpellCast => typeof(CardPlayEvent),

                TriggerTiming.OnTap => typeof(TapEvent),
                TriggerTiming.OnUntap => typeof(UntapEvent),

                // 原子三阶段 + 被指定为目标（场上效果发动的可观察时点）
                TriggerTiming.OnAtomicEffectActivation => typeof(AtomicEffectPhaseEvent),
                TriggerTiming.OnAtomicEffectStartApplying => typeof(AtomicEffectPhaseEvent),
                TriggerTiming.OnAtomicEffectResolution => typeof(AtomicEffectPhaseEvent),
                TriggerTiming.OnTargeted => typeof(AtomicEffectPhaseEvent),

                // OnMaterialDetach：待超量素材取除事件补齐，暂无映射（返回 null 静默跳过）
                _ => null
            };
        }
    }

    #endregion

    #region 节点化效果步骤（运行时）

    /// <summary>步骤种类：原子效果 / 条件分支 / 抉择。</summary>
    public enum RuntimeStepKind
    {
        Atomic = 0,
        Branch = 1,
        Choice = 2,
    }

    /// <summary>
    /// 条件族判别：
    /// OutcomeGate（伤害/治疗/信息→读产出，达成走 then 免费奖励），
    /// Drawback（抽牌→可叠加减费缺陷，挂在原子上，不走 then/else），
    /// FilterPrecision（检索→筛选即条件，按维度计费）。
    /// 分支步骤（Kind==Branch）当前仅承载 OutcomeGate。
    /// </summary>
    public enum BranchConditionKind
    {
        OutcomeGate = 0,
        Drawback = 1,
        FilterPrecision = 2,
    }

    /// <summary>
    /// 运行时效果步骤。Kind==Atomic 时执行 Atomic；
    /// Kind==Branch 时为 OutcomeGate 分支：评估 ConditionId 对当前目标的产出，
    /// 真走 Then、假走 Else（单层、扁平原子列表，奖励免费）；
    /// Kind==Choice 时为抉择：按 EffectInstance.ModeIndex 只执行 Choices 中所选模式的
    /// 子步骤序列（单层不可嵌套，converter 已拒；模式费用 per-mode 独立推导）。
    /// </summary>
    [Serializable]
    public class RuntimeEffectStep
    {
        public RuntimeStepKind Kind;

        /// <summary>Kind==Atomic 时的原子效果。</summary>
        public AtomicEffectInstance Atomic;

        /// <summary>Kind==Branch 时的条件 id（取自 BranchConfig 目录）。</summary>
        public string ConditionId;
        public int ConditionParam;
        public string ConditionStringParam;
        public bool Negate;

        public List<AtomicEffectInstance> Then = new List<AtomicEffectInstance>();
        public List<AtomicEffectInstance> Else = new List<AtomicEffectInstance>();

        /// <summary>Kind==Choice 时的选发模式列表（每模式=原子+紧邻分支的步骤序列）。</summary>
        public List<List<RuntimeEffectStep>> Choices;
    }

    #endregion

    #region 原子效果实例

    [Serializable]
    public class AtomicEffectInstance
    {
        public AtomicEffectType Type;
        public int Value;
        public int Value2;
        public string StringValue;
        public ManaType ManaTypeParam;
        public Zone ZoneParam;
        public DurationType Duration;
        public int DurationValue;      // Duration==ForTurns 时的回合数 N（0 视为 1）

        // 每实例目标覆盖 —— 哨兵值表示沿用 AtomicEffectTable 的配置级目标。
        public int TargetTypeOverride = -1;        // EffectTargetType 枚举值，-1 = 用配置
        public string TargetFilterOverride = "";   // 逗号分隔 filter token，"" = 用配置
        public int TargetCountOverride = -2;        // -2 = 用配置（-1=任意、0=全部 为合法语义）
        public bool DynamicTargetCount;             // true = 运行时玩家自选个数（0..候选数），费用计 0 且不可作地牌
        public List<string> Drawbacks = new List<string>(); // 抽牌减费缺陷 id（UnusableThisTurn 等）

        // メタ効果（RepeatEffect/RandomEffect/ChooseOneEffect/DelayedEffect）の子効果。
        // TODO(network): MemoryPack DTO 往復は範囲外。ネットワーク同期する場合は専用 DTO へ写像が必要。
        public List<AtomicEffectInstance> SubEffects = new List<AtomicEffectInstance>();

        public string GetDescription()
        {
            return AtomicEffectTypeExtensions.GetEffectDescription(Type, Value);
        }
    }

    #endregion
}
