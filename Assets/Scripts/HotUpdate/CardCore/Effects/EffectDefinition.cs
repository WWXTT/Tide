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
        // 未声明持续回退 Once（2026-09-10 上移定案：与 CardEffectData 哨兵口径一致——
        // 旧默认 Permanent 是「法术宿主永久档」旧口径残留，会让裸构造 def 意外走永久层/×2 计价）
        public DurationType Duration = DurationType.Once;
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

        /// <summary>锚价钉死（2026-10-20 教学段七配套）：手作效果行声明 cost 列为**权威锚价**——
        /// DeriveElementCosts 头部直出 AnchorCostPin（null/空=0 费钉死），显示/预检/扣款全单源；
        /// 缺省 false=实时推导（历史唯一口径，正式表零影响）。</summary>
        public bool AnchorCostPinned;

        /// <summary>钉死锚价值（AnchorCostPinned=true 时消费；null=0 费）。</summary>
        public ElementCost AnchorCostPin;

        // ---- 组合层编排属性（2026-09-10 重构：自原子层上移；旧死字段 TargetType 已删）----
        /// <summary>ForTurns 持续的回合数 N（0 视为 1）。持续唯一真相在效果级（Duration）。</summary>
        public int DurationValue;
        /// <summary>SummonToken 落区（战场/手牌/牌库三档，计价按落区系数）。</summary>
        public Zone SummonDropZone = Zone.Battlefield;
        /// <summary>目标选择模式（六值定案 2026-09-16：数量 1/N/全 × 范围宽 单/多；None=无目标/区域自结算）。
        /// 一个 {target} 只能从一个范围选择——0/1/2 要求组合域单一 TargetKind（构筑期校验）。</summary>
        public SelectionMode SelectionMode = SelectionMode.None;
        /// <summary>目标数量：&gt;0=N，0=全部，-1=任意（旧语义保留）；-2=未声明回落表级 TargetCount。</summary>
        public int TargetCount = -2;
        /// <summary>目标随机（2026-09-16 自 SelectionMode 移出为正交标志）：不弹选择，从完整候选域按种子
        /// 随机抽取（TargetCount 管抽取个数；帷幕收窄生效、潜行可被随机命中——不可被"选"≠不可被随机/范围波及）。
        /// 仅对选一/选多档有意义（全取随机≡全取）；与动态数量（-1）互斥（构筑校验）。</summary>
        public bool RandomTarget;
        /// <summary>动态数量：运行时玩家自选个数（0..候选数）；费用计 0（2026-09-21 定案：不关联地牌资格）。</summary>
        public bool DynamicTargetCount;
        /// <summary>**历史字段·无运行时/计价消费方**（2026-10-30 定案：效果不再有每回合生效次数上限）：
        /// 旧语义 &gt;0=每回合 N 次、-1=无限、未声明=1。字段保留只为存档/内容哈希稳定（EffectSlim
        /// 往返与效果 ID 都含本列）；唯一保留的「每回合 1 次」是自由分支引擎奖励（BranchEngines 按卡自限，
        /// 不读本字段）。新效果写成 0 也不影响行为。</summary>
        public int TriggerLimitPerTurn = -1;
        // 动态分支引擎 def 级字段（EngineKind/EngineParam/RewardAtoms/CountdownTurns）已随 2026-10-05
        // 两槽定案载荷化退役——引擎条件与 Then 奖励挂槽级原子 Branch 载荷（见 AtomicEffectInstance.Branch）。
        /// <summary>预计算组合目标域（2026-10-09 逐原子目标制）：共享口径（header 声明）=主序列原子域交集；
        /// 逐原子口径（PerAtomTargets）=并集——仅供展示/预检/AI，结算期各原子按自身极性过滤域独立解析。</summary>
        public List<int> TargetDomain;
        /// <summary>逐原子目标制（2026-10-09 定案）：无效果级作用范围声明（header.TargetKinds 空）时置位——
        /// 结算期主序列每个原子从自身有效域（极性过滤后）独立解析目标，数量档/随机档对各原子各自生效，
        /// 弹窗全部落结算期连续等待（AI/无头由 TargetSelectionService 自动代选）；强制类无窗口自动全取。
        /// header 声明的旧数据保持共享单选语义（两代同链共存）。</summary>
        public bool PerAtomTargets;
        /// <summary>组合域内属性过滤（成员带域原子的 Filter token 之 AND；converter 预计算）。</summary>
        public string TargetFilter;
        /// <summary>per-mode 组合域（与 Choices 平行；无抉择为 null——用 TargetDomain）。</summary>
        public List<int>[] ChoiceDomains;

        /// <summary>
        /// 节点化效果步骤（遗留+光环专用，2026-10-05 两槽定案后）：
        /// 分支/引擎不再写入 Steps（槽级 Branch 载荷承载）；光环形态的规则光环原子步与
        /// 存量抉择（Choice）步骤仍走此路径。非空且无光环/抉择时按原子步平铺执行（向后兼容）。
        /// </summary>
        public List<RuntimeEffectStep> Steps = new List<RuntimeEffectStep>();
        #endregion

        #region 方法

        /// <summary>
        /// 计算实际发动速度
        /// 2026-09-13 定案：无"基础速度"——速度 = BaseSpeed（组合期声明，原子默认 0）+ 动态支付；
        /// 回合归属不进速度（2026-10-18 起也不进记速器门槛——全严格大于，回合/非回合同一裁决）
        /// </summary>
        public int CalculateActivationSpeed(Player activator, Player activePlayer, PhaseType phase, int paidBoost = 0)
        {
            return SpeedCalculator.CalculateSpeed(BaseSpeed, paidBoost);
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

        /// <summary>充能色（2026-10-31 P12）：充能发动时玩家选定的颜色（灰/红/蓝/绿）——
        /// 沿出队→入栈→执行链透传给 ChargeHandler（扣 1 点该色 + 放同色费用降低层）；
        /// 非充能效果恒 null。</summary>
        public ManaType? ChargeColor { get; set; }

        /// <summary>
        /// 创建待发效果
        /// 条件发动 speed=Max 绕过速度检查
        /// 速度发动 = BaseSpeed + 疾风场上加成（2026-10-16：场上源卡主动效果+1）+ paidBoost（2026-09-13：无基础速度）。
        /// 技能栏法术源（英雄技能）基速恒 0（2026-10-18 深夜修订：技能可反复发动，不吃法术槽位
        /// 盈余——转换器烘焙进 BaseSpeed 的加成整体不取；场上增益/付费提速照常叠加）。
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
                    (HeroSkillSystem.IsSkillSource(source) ? 0 : effect.BaseSpeed)
                    + RuleAuraComponents.OnBoardActiveSpeedBonus(source), paidBoost);

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
        /// 获取下一个要入栈的条件发动效果（仅强制/自动）。
        /// 桶序（2026-09-16 对调定案）：自动桶先清——自动池最高优先（先入栈居链底）；
        /// 强制桶后清——居栈顶，混合批中先结算（宣判优先）。旧序（强制先清）与描述口径相反。
        /// </summary>
        public PendingEffect GetNextEffect()
        {
            if (_automaticEffects.Count > 0)
                return PopHighestSpeed(_automaticEffects);
            if (_mandatoryEffects.Count > 0)
                return PopHighestSpeed(_mandatoryEffects);
            return null;
        }

        /// <summary>
        /// 获取可发动的速度效果列表（2026-10-18 定案：门槛=全严格大于；stackEmpty=空栈首动免门槛，
        /// 由 StackEngine 以栈真值传入——主阶段空栈发动 0 速可选触发靠此豁免）
        /// </summary>
        public List<PendingEffect> GetVoluntaryEffects(bool stackEmpty)
        {
            return _voluntaryEffects
                .Where(e => _speedCounter.CanActivate(e.ActivationSpeed, EffectActivationType.Voluntary, stackEmpty))
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

        /// <summary>自动桶非空——批分类探针（2026-09-16 拆轮定案）：批内含自动效果 → 开窗批（等双 Pass）；
        /// 纯强制批 → 合成双 Pass 直接结算（无响应窗口，见 StackEngine.FinishResolution）。</summary>
        public bool HasAutomaticEffects => _automaticEffects.Count > 0;

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
                TriggerTiming.OnOtherCreatureDeath => typeof(CardDestroyEvent),
                TriggerTiming.OnRoleDeath => typeof(RoleDeathEvent),
                TriggerTiming.OnExile => typeof(CardExileEvent),
                TriggerTiming.OnLeaveBattlefield => typeof(CardLeaveBattlefieldEvent),

                // 资源族
                TriggerTiming.OnDraw => typeof(CardDrawEvent),
                TriggerTiming.OnDealDamage => typeof(DamageEvent),
                TriggerTiming.OnTakeDamage => typeof(DamageEvent),

                // 回合/阶段族
                TriggerTiming.OnTurnStart => typeof(TurnStartEvent), // 系统专用（2026-10-20 定案）——效果时点归一 OnStandbyPhase
                TriggerTiming.OnStandbyPhase => typeof(PhaseStartEvent), // 准备阶段（载荷过滤限定 Phase==Standby）
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

                // 信息轴（2026-10-29 阶段3c）：占卜结算观察——LookAtTopCards 同步/异步两路发布
                TriggerTiming.OnScry => typeof(ScryEvent),

                // 原子三阶段 + 被指定为目标（场上效果发动的可观察时点）
                TriggerTiming.OnAtomicEffectActivation => typeof(AtomicEffectPhaseEvent),
                TriggerTiming.OnAtomicEffectStartApplying => typeof(AtomicEffectPhaseEvent),
                TriggerTiming.OnAtomicEffectResolution => typeof(AtomicEffectPhaseEvent),
                TriggerTiming.OnTargeted => typeof(AtomicEffectPhaseEvent),

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
    /// OutcomeGate（产出条件→读主干 per-target 产出），
    /// FilterPrecision（检索→筛选即条件，按维度计费）。
    /// Drawback 空洞已随 2026-10-05 墓碑清理实删重排（效果库空置期，无存量 int 依赖）。
    /// </summary>
    public enum BranchConditionKind
    {
        OutcomeGate = 0,
        FilterPrecision = 1,
    }

    /// <summary>
    /// 运行时效果步骤（遗留结构，2026-10-05 两槽定案后仅承载：光环形态的规则光环原子步
    /// 与存量抉择步骤）。Kind==Branch 的产出门步骤已由槽级 BranchPayload（Outcome）取代，
    /// converter 对存量 Branch 步骤做载荷折叠兼容。抉择（Choice）：按 EffectInstance.ModeIndex
    /// 只执行 Choices 中所选模式的子步骤序列（单层不可嵌套，converter 已拒；模式费用 per-mode 独立推导）。
    /// </summary>
    [Serializable]
    public class RuntimeEffectStep
    {
        public RuntimeStepKind Kind;

        /// <summary>Kind==Atomic 时的原子效果。</summary>
        public AtomicEffectInstance Atomic;

        /// <summary>Kind==Branch 时的条件 id（由代码侧 BranchConditionEvaluator 识别；原 BranchConfig 目录已删）。</summary>
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

    #region 槽级分支载荷（两槽定案 2026-10-05）

    /// <summary>分支结算方式（两槽定案）：条件族三分——
    /// Gate=有限分支条件（局面状态门，与原子产出无关，效果结算时评估一次；达标→Then 奖励，
    /// 不达标→不奖励也不惩罚——2026-10-09 定案，原对赌惩罚面不还原）；
    /// Outcome=自由分支·产出条件（读主干 per-target 产出，如"伤害击杀 Then xx"——producer 匹配挂条件上）；
    /// Engine=自由分支·事件引擎（倒计时/拼点等，条件时机内生于引擎事件）。
    /// 枚举值即落盘 settle 序列化值；未定义值一律按幽灵分支剥离（BranchEntryRules.IsPhantom）。</summary>
    public enum BranchSettleKind
    {
        Gate = 1,
        Outcome = 2,
        Engine = 3,
    }

    /// <summary>槽级分支载荷判定（UI/存储侧共用）。</summary>
    public static class BranchEntryRules
    {
        /// <summary>幽灵分支（2026-10-09）：JsonUtility 落盘会把 null 的 BranchEntryData 物化成
        /// 默认对象（settle=0——非枚举定义值），读档/深拷贝后以此剥离——渲染、奖励槽还原、内容哈希
        /// 都不得把幽灵分支当真分支（运行时执行侧 CardEffectConverter 同口径丢弃越界 settle）。</summary>
        public static bool IsPhantom(BranchEntryData b)
            => b == null || !Enum.IsDefined(typeof(BranchSettleKind), b.settle);
    }

    /// <summary>
    /// 槽级分支载荷：条件在主干中 + Then 奖励。Then=条件达成后**强制结算**的奖励原子
    /// （无发动时机；有目标域的奖励在结算前依次弹目标选择）；计价 0——预算制
    ///（Gate/Outcome=条件 premium；Engine=机制费/引擎预算口径）。奖励目标各自解析，不与主序列共享。
    /// </summary>
    [Serializable]
    public class BranchPayload
    {
        public BranchSettleKind Settle;

        /// <summary>Settle==Gate：局面状态条件 id（BranchConditionEvaluator 局面族目录，如 "LifeBelowOpp"）。
        /// 局面条件均无参数（ConditionParam/ConditionStringParam 不消费）。</summary>
        public string GateId;

        /// <summary>Settle==Outcome：产出条件 id（如 "DmgKillsTarget"/"DeclareHit"）及其参数。</summary>
        public string OutcomeId;
        public int ConditionParam;
        public string ConditionStringParam;

        /// <summary>Settle==Engine：事件引擎。</summary>
        public BranchEngineKind EngineKind;
        /// <summary>引擎参数 x（运势阈值/死亡计数阈值等；倒计时缺省 0=按 Then 推导费自动换算）。</summary>
        public int EngineParam;
        /// <summary>倒计时初值回合数（converter 换算：Then 推导费 1费=1回合，向上取整下限 1；
        /// 引擎运行时归零重置回此值）。仅 EngineKind==Countdown 有语义。</summary>
        public int CountdownTurns;

        /// <summary>奖励发动方式（2026-10-20 用户定案：奖励=一般效果按自身发动方式发射，主干只管
        /// 触发判定）：-1=未声明随主干 def.ActivationType·向后兼容；1=自动直发；2=主动（条件达成
        /// 入待发队列走既有响应窗路径——LaunchReward 统一口）。</summary>
        public int ThenActivation = -1;

        /// <summary>条件达成的奖励原子（≤预算；结算期依次弹选/解析目标）。</summary>
        public List<AtomicEffectInstance> Then = new List<AtomicEffectInstance>();
    }

    #endregion

    #region 原子效果实例

    [Serializable]
    /// <summary>事件引擎（原"动态分支引擎"，2026-10-05 两槽定案载荷化——自由分支条件族的事件子族）。
    /// 条件时机内生于引擎事件（回合开始/死亡事件/付费后/施放序位），不强制固定发动时点。</summary>
    public enum BranchEngineKind : int
    {
        /// <summary>无引擎</summary>
        None = 0,
        /// <summary>倒计时：回合开始计数-1，归零→执行奖励并重置；初值=奖励推导费换算回合（1费=1回合）——延迟即付费</summary>
        Countdown = 1,
        /// <summary>运势：掷 2d6（随声明时机，缺省回合开始），双骰都≥x → 执行奖励；机制费=x 灰（x∈[1,6]，双 6 才中=1/36）</summary>
        LuckRoll = 2,
        /// <summary>拼点：攻击宣言时双方各从手牌展示一张生物（自动取攻击力最高并挂「展示」公开；
        /// 无生物按攻击力 0）——自己生物攻击力 − 对手 ≥ 门槛（=奖励锚价合计）→ 执行奖励</summary>
        Clash = 3,
        /// <summary>死亡计数（2026-09-22 定案）：本回合**双方合计**生物死亡数 ≥ x 时执行奖励（计数单调，
        /// 每回合达标时刻唯一→天然一次/回合）；奖励预算=x（奖励原子锚价合计 ≤ x，引擎零计价）。x∈[1,9]</summary>
        DeathToll = 4,
        /// <summary>元素充盈（2026-09-22 定案；2026-10-20 判据五改）：**只能是自动效果、只能在对手回合中
        /// 发动**——对手（=当前回合玩家）出牌施放结算完成后，比较双方剩余元素总量（未横置地牌+钱包）
        /// ——己方与对手的差（封顶 6）**恰好等于** x 即执行奖励（==x：差每+1踩一档各一次）；奖励恒直发，
        /// 目标由控制者结算期弹选；门槛=Then 锚价推导。</summary>
        ManaSurplus = 5,
        // 2026-10-31 墓碑清理：原位 6（手牌序位·2026-10-19 退役）与位 7（Grant 赋予引擎·2026-10-05 解体）
        // 两处空洞已删，余位紧凑重排；`branch.engine` int 载荷已同步迁移（TutorialEffects.json 7 处：
        // 8/9/10 → 6/7/8），全仓无旧值引用。
        /// <summary>附加诅咒（2026-10-08 自由分支化，自有限分支 CurseOnDraw 门接棒）：此卡施放结算时
        /// 给对手牌库随机 x 张卡挂「诅咒」指示物+登记 Then 载荷（CurseSystem）——对手抽到该卡时执行
        /// 奖励并消层（一次性；触发与引擎卡去向无关）。x∈[1,3]=附加张数；延迟与不确定性即代价（预算不设上限）。</summary>
        CurseOnDraw = 6,
        /// <summary>附加祝福（2026-10-08）：同 CurseOnDraw，作用面=自己牌库、指示物=「祝福」——
        /// 抽到该卡时执行祝福分支效果并消层（一次性）。x∈[1,3]。</summary>
        BlessingOnDraw = 7,
        /// <summary>团结（2026-10-20 效果序位换轴定案·第6种改空间轴）：与此卡**相邻的单位数** ≥ x 时执行
        /// 奖励——计数口径：生物/结界占格各 1、角色格恒 1（角色无卡实体按格计）、非空地牌格 1
        ///（同色组首张占格=非空；空地牌格不计）；施放占位卡非在场单位不计（同战斗邻接口径）；六邻天然
        /// 封顶 6。评估口=己方每次出牌施放结算完成后（OnCardCastSettled，与元素充盈同拍）——只观察己方
        /// 场上引擎卡（手牌无格），每次达标都触发；x∈[1,6] 手填。核心不绑棋盘（架构铁律）——邻接计数经
        /// UnityAdjacentCountOf 注入（宿主接线 BoardState.AdjacentOccupantCount）。</summary>
        Unity = 8,
    }

    public class AtomicEffectInstance
    {
        public AtomicEffectType Type;
        public int Value;              // 唯一数值参数（2026-09-10 定案：原子只有 Value）——名义值：计价/描述/UI 读它
        /// <summary>数值随机幅度 0..1（2026-09-13 定案）：&gt;0 时结算读 GetRolledValue()——
        /// 每目标独立掷 [Value−span, Value+span]（span=round(|Value|×幅度)）；计价按名义 Value（锚点不漂移）。</summary>
        public float RandomAmplitude;
        public string StringValue;
        /// <summary>来源表行 ID（2026-10-03 计价行身份修复）：同枚举多行（变体行各自锚价，如规则光环 7 行）
        /// 时计价按此行取锚——null/缺行回落 GetByType（末行，旧口径）。运行时字段，不入网络 DTO。</summary>
        public string RowHashId;
        /// <summary>槽级分支载荷（2026-10-05 两槽定案）：条件在主干中 + Then 奖励——
        /// Gate=有限分支（局面状态门，效果结算时评估一次，任意原子可挂）；
        /// Outcome/Engine=自由分支（Outcome 读主干 per-target 产出；Engine 事件驱动，时机内生于引擎）。
        /// null=无分支。诅咒=Gate 特例（GateId="CurseOnDraw"，时机固定"抽到该卡时"，CurseSystem 驱动）。
        /// 运行时字段，不入网络 DTO。</summary>
        public BranchPayload Branch;

        /// <summary>Mana 字典（与卡计费同款表达；无 Mana 参数的原子为 null）。</summary>
        public Dictionary<ManaType, float> Mana;
        /// <summary>解析后有效目标域（entry 显式收窄 ?? 表级默认；converter 填）。</summary>
        public List<int> TargetKinds;
        /// <summary>每原子目标数量覆盖（2026-10-09：entry.count；-2=未声明回落效果级 def.TargetCount）。
        /// 解析/定价读 EffectiveTargetCount(def) 取有效值。</summary>
        public int TargetCount = -2;
        /// <summary>每原子目标随机覆盖（2026-10-09：entry.rand；-1=未声明回落效果级 def.RandomTarget）。
        /// 解析读 EffectiveRandomTarget(def) 取有效值。</summary>
        public int RandomTarget = -1;
        /// <summary>每原子目标选择方式（2026-10-20：entry.defer；0=发动前预选=默认/1=效果处理时选——
        /// 结算期必须选不可取消（类自动效果）。声明期收集器与预选归属段双消费。</summary>
        public int DeferSelect;

        /// <summary>每原子有效目标数量（2026-10-09）：实例声明（1/2/3）优先；-2 未声明回落效果级 def.TargetCount。</summary>
        public int EffectiveTargetCount(EffectDefinition def)
            => TargetCount != -2 ? TargetCount : (def != null ? def.TargetCount : 1);

        /// <summary>每原子有效目标随机（2026-10-09）：实例声明优先（1=随机抽不弹窗/0=弹窗）；-1 未声明回落效果级 def.RandomTarget。</summary>
        public bool EffectiveRandomTarget(EffectDefinition def)
            => RandomTarget != -1 ? RandomTarget == 1 : def != null && def.RandomTarget;

        /// <summary>效果基础色（2026-10-20 用户定案：弹体/爆点按**所用效果**的基础色）：
        /// 原子表行费用基准（ManaList）主色；无行/零费 → null（表现层回落来源卡费用主色口径）。</summary>
        public ManaType? AnchorTint()
        {
            var ml = Attribute.AtomicEffectTable.GetByHashId(RowHashId)?.ManaList;
            return ml != null && !ml.IsZero ? ml.PrimaryColor : (ManaType?)null;
        }
        /// <summary>域内属性过滤 token（表级 TargetFilter；converter 解析存实例）。</summary>
        public string Filter;
        /// <summary>极性（表级解析：-1=对对手释放有益 / +1=对己方释放有益 / 0=中性；错边折价输入）。</summary>
        public float Polarity;

        // 元效果（RepeatEffect/RandomEffect/ChooseOneEffect/DelayedEffect）的子效果。
        // TODO(network)：MemoryPack DTO 不往返这一层；将来要进网络同步需映射到专用 DTO。
        public List<AtomicEffectInstance> SubEffects = new List<AtomicEffectInstance>();

        /// <summary>无执行场景的模板描述（代价文本等）：走 handler 注册表（context=null → 纯模板）。
        /// 执行期完整描述由执行器在结算时经 handler.GetDescription(effect, context) 生成（效果级聚合）。</summary>
        public string GetDescription()
        {
            var handler = Attribute.EffectHandlerRegistry.GetHandler(Type);
            return handler != null ? handler.GetDescription(this, null) : Type.ToString();
        }

        /// <summary>结算期掷值（2026-09-13 数值随机定案）：幅度&gt;0 时名义值 ±span 均匀随机
        /// （每调用一次掷一次——handler 在 per-target 循环里读即每目标独立掷）；幅度 0 恒名义值。
        /// 只有效果结算读它；计价/构筑/描述读 Value 名义值。</summary>
        public int GetRolledValue() => RandomAmplitude > 0f ? GameRng.RollValue(Value, RandomAmplitude) : Value;
    }

    #endregion
}
