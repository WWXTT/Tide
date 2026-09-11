using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// 卡牌数据类 - 可序列化的卡牌实体
    /// 通过实现多个接口来组合不同的卡牌属性
    /// </summary>
    [Serializable]
    public class CardData
    {
        /// <summary>
        /// 卡牌唯一标识ID（基于内容Hash生成）
        /// </summary>
        [SerializeField]
        private string _id;
        public string ID
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>
        /// 卡牌类型
        /// </summary>
        [SerializeField]
        private Cardtype _supertype;
        public Cardtype Supertype
        {
            get => _supertype;
            set => _supertype = value;
        }

        /// <summary>
        /// 卡牌名称
        /// </summary>
        [SerializeField]
        private string _cardName;
        public string CardName
        {
            get => _cardName;
            set => _cardName = value;
        }

        /// <summary>
        /// 立绘路径
        /// </summary>
        [SerializeField]
        private string _illustration;
        public string Illustration
        {
            get => _illustration;
            set => _illustration = value;
        }

        /// <summary>
        /// 生命值（可选）
        /// </summary>
        [SerializeField]
        private int? _life;
        public int? Life
        {
            get => _life;
            set => _life = value;
        }

        /// <summary>
        /// 攻击力（可选）
        /// </summary>
        [SerializeField]
        private int? _power;
        public int? Power
        {
            get => _power;
            set => _power = value;
        }

        /// <summary>
        /// 法力消耗
        /// </summary>
        [SerializeField]
        private string _costJson;
        public Dictionary<int, float> Cost { get; set; } = new Dictionary<int, float>();

        // 抉择（Choice）per-mode 费用缓存（2026-09-07 定案）：构筑/装载期由 CardCostService.DeriveModeCosts
        // 推导写入（发动时只读取不重推导）；声明 Cost 为最大模式费（地牌/素材/UI 消费面口径）。
        [NonSerialized]
        internal List<Dictionary<int, float>> ModeCostCache;

        // 自我沉睡判定缓存（2026-09-11 灰费豁免定案）：任一非启动式效果含 Sleep 原子且 SelectionMode=Self。
        // GetCardCost 高频调用，转换结果缓存（ResetCache 失效，随 ModeCostCache 口径）。
        [NonSerialized]
        internal bool? SelfSleepEffectCache;

        // 内容身份（2026-09-09 定案，CardIdentityService 与 EnsureCost 同管线推导）：拆散到原子级——
        // 原子哈希数组（跨效果按执行序展平）+ 组合结构哈希 + 关键词/tag/光环组合哈希，全部混入
        // 原子表指纹。观测侧（TideObservation [15..22]）据此查 embedding，同原子跨卡共享行。
        // 参数数值通路（同日）：EffectType 下标数组（跨参数共享嵌入行）+ 参数浮点块
        // （每原子 CardIdentityService.AtomParamDim 维，与哈希同序），见观测 [23..]。
        // 属性不进身份（obs 实时特征已有，不重复）。
        // 不随 ResetCache 失效：身份只依赖效果数据与原子表，与费用缓存失效口径无关。
        [NonSerialized]
        internal ulong[] _atomContentHashes;
        [NonSerialized]
        internal ulong _structureContentHash;
        [NonSerialized]
        internal ulong _compositionContentHash;
        [NonSerialized]
        internal int[] _atomTypeIndexes;   // 与 _atomContentHashes 同序（执行序展平）；0 = 无/未入表
        [NonSerialized]
        internal float[] _atomParams;      // 同序参数块（每原子 AtomParamDim 维，全部原子不截断）

        public ulong[] AtomContentHashes => _atomContentHashes;
        public ulong StructureContentHash => _structureContentHash;
        public ulong CompositionContentHash => _compositionContentHash;
        public int[] AtomTypeIndexes => _atomTypeIndexes;
        public float[] AtomParams => _atomParams;

        internal void SetIdentity(ulong[] atomHashes, ulong structureHash, ulong compositionHash,
            int[] atomTypeIndexes, float[] atomParams)
        {
            _atomContentHashes = atomHashes;
            _structureContentHash = structureHash;
            _compositionContentHash = compositionHash;
            _atomTypeIndexes = atomTypeIndexes;
            _atomParams = atomParams;
        }

        /// <summary>
        /// 效果列表
        /// </summary>
        [SerializeField]
        private List<CardEffectData> _effects;
        public List<CardEffectData> Effects
        {
            get => _effects ??= new List<CardEffectData>();
            set => _effects = value;
        }
        

        /// <summary>
        /// 创建时间（用于时间戳）
        /// </summary>
        [SerializeField]
        private long _creationTicks;
        public DateTime CreationTime
        {
            get => new DateTime(_creationTicks);
            set => _creationTicks = value.Ticks;
        }

        /// <summary>
        /// 卡牌标签（用于存储额外属性，如Durability等）
        /// </summary>
        [SerializeField]
        private List<string> _tags;
        public List<string> Tags
        {
            get => _tags ??= new List<string>();
            set => _tags = value;
        }

        /// <summary>
        /// 关键词列表（如冲锋、飞行、圣盾等自身被动效果）
        /// </summary>
        [SerializeField]
        private List<string> _keywords;
        public List<string> Keywords
        {
            get => _keywords ??= new List<string>();
            set => _keywords = value;
        }

        /// <summary>
        /// 卡牌子类型（种族 / 怪兽种类 / 融合-同步-超量-链接等额外卡组标记），Flags 组合。
        /// </summary>
        [SerializeField]
        private CardSubtype _subtype = CardSubtype.None;
        public CardSubtype Subtype
        {
            get => _subtype;
            set => _subtype = value;
        }

        /// <summary>等级（生物 / 融合 / 同步），可空。</summary>
        [SerializeField]
        private int _level = -1;
        public int? Level
        {
            get => _level < 0 ? (int?)null : _level;
            set => _level = value ?? -1;
        }

        /// <summary>阶级（超量），可空。</summary>
        [SerializeField]
        private int _rank = -1;
        public int? Rank
        {
            get => _rank < 0 ? (int?)null : _rank;
            set => _rank = value ?? -1;
        }

        /// <summary>链接值（链接），可空。</summary>
        [SerializeField]
        private int _linkRating = -1;
        public int? LinkRating
        {
            get => _linkRating < 0 ? (int?)null : _linkRating;
            set => _linkRating = value ?? -1;
        }

        /// <summary>链接箭头方向（链接），Flags 组合。</summary>
        [SerializeField]
        private HexDirection _arrowDirections = HexDirection.None;
        public HexDirection ArrowDirections
        {
            get => _arrowDirections;
            set => _arrowDirections = value;
        }

        /// <summary>
        /// 连接光环声明（三轨制定案 2026-09-09）：箭头指向格的当前占据者享受的持续效果——
        /// 属性修正（stat="Power"|"Life" + value）或关键词（keyword=id），与 stat 二选一。
        /// 光环=live-query（LinkAuraSystem 实时计算）：断链/来源离场/来源被无效即失效；
        /// 受益者身上的净化/沉默/无效不能干扰光环（只有「无效」作用于来源能压制）。
        /// 计价按单回合指示物档（来源须持续在场的折价）。
        /// </summary>
        [SerializeField]
        private List<LinkAuraData> _linkAuras;
        public List<LinkAuraData> LinkAuras
        {
            get => _linkAuras ?? (_linkAuras = new List<LinkAuraData>());
            set => _linkAuras = value;
        }

        // ---- 战斗底盘（2026-09-10 攻击/守卫效果化）----
        // 攻/守 = 1速/2速主动效果（各 1 灰，不占槽位），生物默认自带；opt-out 退底盘额度。
        // 计价见 CardCompositionCost.ChassisAdjust；资格见 CombatSystem（NoAttack 卡不能攻击）。
        [SerializeField]
        private bool _noAttack;
        public bool NoAttack { get => _noAttack; set => _noAttack = value; }

        [SerializeField]
        private bool _noGuard;
        public bool NoGuard { get => _noGuard; set => _noGuard = value; }

        /// <summary>瞬间法术的底盘盈余（无攻无守省下的 2 灰）构筑时自由分配：
        /// true = 转 BaseSpeed+1（不退费）；false（缺省）= 退费（灰不足落最高费用色）。</summary>
        [SerializeField]
        private bool _surplusToSpeed;
        public bool SurplusToSpeed { get => _surplusToSpeed; set => _surplusToSpeed = value; }

        /// <summary>
        /// 总费用计算
        /// </summary>
        [NonSerialized]
        private float _totalCost = -1;
        public float TotalCost
        {
            get
            {
                if (_totalCost < 0)
                {
                    float total = 0;
                    foreach (var cost in Cost.Values)
                    {
                        total += cost;
                    }
                    _totalCost = total;
                }
                return _totalCost;
            }
        }

        /// <summary>
        /// 是否有主动效果
        /// </summary>
        [NonSerialized]
        private bool? _hasActiveEffect;
        public bool HasActiveEffect
        {
            get
            {
                if (!_hasActiveEffect.HasValue)
                {
                    foreach (var effect in Effects)
                    {
                        if (effect.TriggerTiming == (int)TriggerTiming.Activate_Active ||
                            effect.TriggerTiming == (int)TriggerTiming.Activate_Instant)
                        {
                            _hasActiveEffect = true;
                            return true;
                        }
                    }
                    _hasActiveEffect = false;
                }
                return _hasActiveEffect.Value;
            }
        }

        /// <summary>
        /// 是否有战斗属性
        /// </summary>
        public bool HasCombatStats => Life.HasValue || Power.HasValue;

        /// <summary>
        /// 序列化前处理
        /// </summary>
        public void OnBeforeSerialize()
        {
            // 将 Cost 序列化为 JSON
            _costJson = CostToJson();
        }

        /// <summary>
        /// 反序列化后处理
        /// </summary>
        public void OnAfterDeserialize()
        {
            // 从 JSON 反序列化 Cost
            Cost = JsonFromCost(_costJson);
        }

        /// <summary>
        /// 根据卡牌类型计算Hash作为ID
        /// </summary>
        public string CalculateID()
        {
            string content = JsonUtility.ToJson(this);
            uint hash = MurmurHash3.Hash32(content);
            return hash.ToString("X8");
        }

        /// <summary>
        /// 将 Cost 字典序列化为 JSON
        /// </summary>
        private string CostToJson()
        {
            var costList = new List<CostEntry>();
            foreach (var kvp in Cost)
            {
                costList.Add(new CostEntry { ManaType = kvp.Key, Value = kvp.Value });
            }
            return JsonUtility.ToJson(costList, true);
        }

        /// <summary>
        /// 从 JSON 反序列化 Cost 字典
        /// </summary>
        private Dictionary<int, float> JsonFromCost(string json)
        {
            var result = new Dictionary<int, float>();
            if (string.IsNullOrEmpty(json)) return result;

            try
            {
                var costList = JsonUtility.FromJson<CostEntryList>(json);
                foreach (var entry in costList.entries)
                {
                    result[entry.ManaType] = entry.Value;
                }
            }
            catch
            {
                // 解析失败返回空字典
            }
            return result;
        }

        /// <summary>
        /// 重置缓存
        /// </summary>
        public void ResetCache()
        {
            _totalCost = -1;
            _hasActiveEffect = null;
            ModeCostCache = null; // 抉择 per-mode 费用随配置失效（下次构筑期重推导）
            SelfSleepEffectCache = null; // 自我沉睡判定随效果数据失效（2026-09-11）
        }

        /// <summary>
        /// 成本条目
        /// </summary>
        [Serializable]
        private class CostEntry
        {
            public int ManaType;
            public float Value;
        }

        /// <summary>
        /// 成本条目列表包装
        /// </summary>
        [Serializable]
        private class CostEntryList
        {
            public List<CostEntry> entries;
        }
    }

    /// <summary>
    /// 原子效果条目 —— 描述单个原子效果的所有参数
    /// 直接映射为 AtomicEffectInstance
    /// </summary>
    [Serializable]
    public class AtomicEffectEntry
    {
        public string ID;     // 字符串参数（token ID、关键词 ID、宣言编码）
        public string EffectType;      // AtomicEffectType 枚举名，如 "DealDamage"
        public int Value;              // 唯一数值参数（伤害量、抽卡数、修改量——2026-09-10 定案：原子只有 Value）

        // Mana 字典：与卡计费方式一致（costList 同款 {manaType, amount}；JsonUtility 不支持字典故用列表）。
        public List<ManaAmountEntry> ManaList;
        // 目标种类集合（TargetKind 序号，见 Effects/TargetKind.cs）。null = 用表级默认；
        // 空集 = 无目标原子（DrawCard 等），不参与组合交集约束。
        public List<int> TargetKinds;
    }

    /// <summary>Mana 字典条目（与卡 costList 的 {manaType, amount} 完全同款——键名一致）。</summary>
    [Serializable]
    public class ManaAmountEntry
    {
        public int manaType;
        public float amount;
    }

    /// <summary>
    /// 代价条目 —— 卡牌效果的费用配置。
    /// 定案（2026-09-11）：一张卡只有一个代价栏、只能填一个代价（整卡 Costs 条目合计 ≤1，无法像效果那样组合）。
    /// </summary>
    [Serializable]
    public class CostEntry
    {
        public int CostType;           // CostType 枚举值
        public int Value;              // 代价数值
        public int ManaType;           // 元素消耗的 ManaType
        public int TurnDuration;       // 沉睡回合数

        /// <summary>效果型代价（CostType=Payload，2026-09-11）：付费步强制执行的原子效果，
        /// 按全价补偿黑/白元素（如「给对手召唤 30/30 衍生物 → 获得白16」）。</summary>
        public AtomicEffectEntry payload;
    }

    /// <summary>
    /// 发动条件条目
    /// </summary>
    [Serializable]
    public class ActivationConditionData
    {
        public int Type;               // ConditionType 枚举值
        public int Value;
        public int Value2;
        public string StringValue;
        public bool Negate;
    }

    /// <summary>
    /// 抉择模式条目 —— EffectStepData.kind==2（Choice）的一个选发分支。
    /// JsonUtility 不支持嵌套泛型集合（List&lt;List&lt;T&gt;&gt; 会静默丢字段），
    /// 故用包装类承载每个模式的子步骤序列（原子+紧邻分支；不可再嵌 Choice）。
    /// 费用定案（2026-09-07）：各模式构筑期独立推导存储；发动时先选模式再定费用。
    /// </summary>
    [Serializable]
    public class EffectChoiceData
    {
        public string label;                      // 模式显示名（仅 UI 展示，不参与哈希）
        public List<EffectStepData> steps;        // 该模式的子步骤序列
    }

    /// <summary>
    /// 效果步骤条目 —— 把效果序列扩展为「原子效果」「条件分支」「抉择」三种步骤。
    /// 由效果合成界面（UI）编排，JsonUtility 可序列化（判别字段 + 有界一层 then/else）。
    ///
    /// 执行：EffectExecutor.ExecuteAsync 读 Steps 做 per-target 遍历（EffectsExecutionEngine
    /// 的 ExecuteStepsAsync）；分支必须紧邻其原子之后，评估读 LastOutcome。
    /// 预言族条件（ProphecyHit/Miss）被引擎拦截为延迟验证，由 ProphecySystem 在
    /// 对手下回合首张出牌时结算 then/else。Steps 为空时退化为扁平 AtomicEffects。
    ///
    /// 抉择（kind==2）：≥2 个选发模式，发动声明期选定 ModeIndex（随 cast 上栈），
    /// 结算只执行所选模式；各模式费用独立推导（CostDerivation per-mode）。
    /// </summary>
    [Serializable]
    public class EffectStepData
    {
        public int kind;                              // 0=原子效果, 1=条件分支, 2=抉择
        public AtomicEffectEntry atomic;              // kind==0 时有效
        public ActivationConditionData condition;     // kind==1 时有效（保留：发动前条件，旧字段）
        public List<AtomicEffectEntry> thenSteps;     // kind==1：条件成立时执行
        public List<AtomicEffectEntry> elseSteps;     // kind==1：否则执行

        // kind==1 的产出条件（OutcomeGate），取自 BranchConfig 目录。
        // 替代借用 condition/ConditionType（后者保留给发动前条件）。
        public string conditionId;                    // 条件 id，如 "DmgKillsTarget"
        public int conditionParam;                    // 数值参数（如门槛）
        public string conditionStringParam;           // 字符串参数（如预言类型）

        // kind==2：选发模式列表（≥2）。包装类见 EffectChoiceData 注释。
        public List<EffectChoiceData> choices;
    }

    /// <summary>
    /// 连接光环声明条目（三轨制定案 2026-09-09）：箭头指向格的占据者享受的持续效果。
    /// stat（"Power"|"Life"）+value 与 keyword 二选一；空 stat 且空 keyword = 无效条目（加载时忽略）。
    /// 光环轨 live-query（LinkAuraSystem），断链/离场/来源被无效即失效。
    /// </summary>
    [Serializable]
    public class LinkAuraData
    {
        /// <summary>属性名："Power" 或 "Life"（空 = 关键词条目）</summary>
        public string stat;
        /// <summary>数值幅度（stat 条目的 ±修正量）</summary>
        public int value;
        /// <summary>关键词 id（与 stat 二选一；光环期间 HasKeyword=true，不进 _keywords）</summary>
        public string keyword;
    }

    /// <summary>
    /// 卡牌效果数据 —— 描述卡牌的一个完整效果
    /// 包含触发时点、条件、代价、以及有序的原子效果列表
    /// 转换后成为一个 EffectDefinition
    /// </summary>
    [Serializable]
    public class CardEffectData
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int TriggerTiming;      // TriggerTiming 枚举值
        public int ActivationType;     // 0=强制, 1=自动, 2=主动
        public int BaseSpeed;
        public bool IsOptional;
        public int Duration;           // 整体效果的 DurationType（2026-09-10 重构激活：持续唯一真相在效果级；-1=迁移哨兵=未声明）
        // ---- 组合层编排属性（2026-09-10 重构 P1：自原子层上移）----
        public int DurationValue;      // Duration==ForTurns 时的回合数 N（0 视为 1）
        public int SummonDropZone;    // SummonToken 落区（Zone 枚举：战场/手牌/牌库三档）
        public int SelectionMode = -1; // SelectionMode 枚举值（-1=None 无目标哨兵）
        public int TargetCount = -2;   // >0=N，0=全部，-1=任意；-2=未声明回落表级
        public bool DynamicTargetCount;// 动态数量：运行时玩家自选个数
        public List<string> Drawbacks = new List<string>(); // 抽牌减费缺陷（上移；执行暂缓）

        public List<ActivationConditionData> ActivationConditions;
        public List<ActivationConditionData> TriggerConditions;
        public List<AtomicEffectEntry> AtomicEffects;
        public List<CostEntry> Costs;
        public List<string> Tags;

        // 可选：分支化的效果步骤（含 then/else）。为空时退化为扁平 AtomicEffects（向后兼容）。
        // 执行引擎按 Steps 遍历（见 EffectStepData 注释）。
        public List<EffectStepData> Steps;
    }

    /// <summary>
    /// 卡牌数据包装器 - 用于将CardData包装为可运行时使用的卡牌
    /// </summary>
    public class CardWrapper : Card,
        IHasSupertype,
        IHasName,
        IHasIllustration,
        IHasLife,
        IHasPower,
        IHasCost,
        IHasEffects,
        IHasRuntimeEffects,
        IHasKeywords,
        CardDataWrapper
    {
        private readonly CardData _data;
        private List<IEffect> _runtimeEffects;

        // IHasSupertype
        Cardtype IHasSupertype.Supertype { get; set ; }

        // IHasName
        string IHasName.CardName { get; set; }

        // IHasIllustration
        string IHasIllustration.Illustration { get; set; }

        // IHasLife
        int IHasLife.Life { get; set; }

        // IHasPower
        int IHasPower.Power { get; set; }

        // IHasCost
        Dictionary<int, float> IHasCost.Cost { get; set; }

        // IHasEffects
        List<Effect_table> IHasEffects.Effects { get; set; }

        // IHasRuntimeEffects
        List<IEffect> IHasRuntimeEffects.RuntimeEffects
        {
            get => _runtimeEffects;
            set => _runtimeEffects = value;
        }

        // IHasKeywords - 直接使用 Card._keywords，与 EntityEffectExtensions 统一
        List<string> IHasKeywords.Keywords
        {
            get => _keywords;
            set
            {
                _keywords.Clear();
                if (value != null)
                    _keywords.AddRange(value);
            }
        }

        void IHasKeywords.AddKeyword(string keywordId)
        {
            // 经扩展咽喉入账（轨别=Temp 兜底；台账同步——原直加绕过台账会被清除口径漏计）
            EntityEffectExtensions.AddKeyword(this, keywordId, KeywordLane.Temp);
        }

        void IHasKeywords.RemoveKeyword(string keywordId)
        {
            EntityEffectExtensions.RemoveKeyword(this, keywordId);
        }

        bool IHasKeywords.HasKeyword(string keywordId)
        {
            return _keywords.Contains(keywordId);
        }

        /// <summary>
        /// 从 CardData 创建 CardWrapper
        /// </summary>
        public CardWrapper(CardData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));

            ID = data.ID;

            // 设置接口属性
            (this as IHasSupertype).Supertype = data.Supertype;
            (this as IHasName).CardName = data.CardName;
            (this as IHasIllustration).Illustration = data.Illustration;

            // 设置可选属性
            if (data.Life.HasValue)
                (this as IHasLife).Life = data.Life.Value;
            else
                (this as IHasLife).Life = 0;

            if (data.Power.HasValue)
                (this as IHasPower).Power = data.Power.Value;
            else
                (this as IHasPower).Power = 0;

            // 设置费用（前先做统一计价兜底：直构 CardData（验证器/合成卡）无 costList 时补建议档位）
            CardCostService.EnsureCost(data);
            (this as IHasCost).Cost = data.Cost;

            // 同步内部战斗字段 — EntityEffectExtensions/handler 全部读 _power/_life/_maxLife/_baseCost，
            // 仅设接口属性会导致加载的生物血量恒为默认 1
            _power = data.Power ?? 0;
            _life = data.Life ?? 1;
            _maxLife = _life;
            _baseCost = ComputeBaseCost(data.Cost);

            // 设置效果列表（Editor路径使用 Effect_table，运行时由 CardEffectConverter 转换）
            (this as IHasEffects).Effects = new List<Effect_table>();

            // 初始化运行时效果列表（由 CardEffectConverter 在 PlayCard 时填充）
            _runtimeEffects = new List<IEffect>();

            // 注入关键词到 Card._keywords（与 EntityEffectExtensions 统一）——
            // 轨别=Printed（卡面本体：净化/换区都不清，三轨制定案）
            foreach (var kw in data.Keywords)
            {
                _keywords.Add(kw);
                _keywordGrants.Add(new KeywordGrant { Keyword = kw, Lane = KeywordLane.Printed });
            }
        }

        /// <summary>
        /// 获取原始数据
        /// </summary>
        public CardData GetData() => _data;

        /// <summary>费用字典求和作为内部 _baseCost（用于 GetCost/ModifyCost 原语）</summary>
        private static int ComputeBaseCost(Dictionary<int, float> cost)
        {
            if (cost == null) return 0;
            float total = 0f;
            foreach (var kvp in cost) total += kvp.Value;
            return (int)total;
        }
    }

    /// <summary>
    /// 预定义的卡牌模板
    /// </summary>
    [Serializable]
    public class CardTemplate
    {
        [SerializeField]
        private Cardtype _supertype;
        public Cardtype Supertype
        {
            get => _supertype;
            set => _supertype = value;
        }

        [SerializeField]
        private string _cardName;
        public string CardName
        {
            get => _cardName;
            set => _cardName = value;
        }

        [SerializeField]
        private string _illustration;
        public string Illustration
        {
            get => _illustration;
            set => _illustration = value;
        }

        [SerializeField]
        private int? _defaultLife;
        public int? DefaultLife
        {
            get => _defaultLife;
            set => _defaultLife = value;
        }

        [SerializeField]
        private int? _defaultPower;
        public int? DefaultPower
        {
            get => _defaultPower;
            set => _defaultPower = value;
        }

        [SerializeField]
        private List<CardEffectData> _defaultEffects;
        public List<CardEffectData> DefaultEffects
        {
            get => _defaultEffects ??= new List<CardEffectData>();
            set => _defaultEffects = value;
        }

        /// <summary>
        /// 创建基础卡牌数据
        /// </summary>
        public CardData CreateCardData()
        {
            return new CardData
            {
                Supertype = Supertype,
                CardName = CardName,
                Illustration = Illustration,
                Life = DefaultLife,
                Power = DefaultPower,
                Effects = new List<CardEffectData>(DefaultEffects),
                Cost = new Dictionary<int, float>()
            };
        }
    }
}
