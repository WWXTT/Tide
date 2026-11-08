using System;
using System.Collections.Generic;

namespace CardCore.Attribute
{
    /// <summary>
    /// 原子效果属性配置
    /// 对应 #Attribute.xlsm 数据表的一行
    /// Luban生成后会替换为cfg命名空间下的类
    /// </summary>
    [Serializable]
    public class AtomicEffectConfig
    {
        /// <summary>效果行序号（装载序）</summary>
        public int Id;

        /// <summary>来源表类别（2026-10-30 五分表；装载期由来源文件写入——见 AtomicEffectTable.Category* 常量）。
        /// 与文件一一对应、非 MountKinds 派生：2026-10-31 代价槽改版起，合成器「一般效果」筛选
        ///（代价原子只收一般效果行）以本字段为判定源——改行归属=挪文件，装载自动跟随。</summary>
        public int TableCategory;

        /// <summary>表首列 ID（8-hex 描述哈希——2026-09-14 原子引用化的引用键；
        /// 空=行缺失该列，不可被 AtomRef 引用）。</summary>
        public string HashId;

        /// <summary>枚举名称（对应 AtomicEffectType）</summary>
        public string EnumName;

        /// <summary>中文显示名</summary>
        public string DisplayName;

        /// <summary>效果描述模板</summary>
        public string Description;

        /// <summary>费用构成（2026-10-04 位置数组定案：下标=ManaType 枚举序号 [灰,红,蓝,绿,白,黑]，
        /// 长度=枚举成员数，扩色自动加长；计价 = 各色份额 × Value（随值缩放）；null = 不计价行）。</summary>
        public ElementCost ManaList;

        /// <summary>主色（首个非零下标色；null/全零=Gray）——UI 圆点等单色消费口。</summary>
        public ManaType PrimaryColor => ManaList?.PrimaryColor ?? ManaType.Gray;

        /// <summary>费用合计基数（各色份额之和）——原 BaseCost 的标量消费者过渡用。</summary>
        public float TotalUnitCost => ManaList?.Total ?? 0f;

        /// <summary>费用乘数（不同目标范围对费用的影响）</summary>
        public float CostMultiplier;

        /// <summary>目标种类集（**B2 2026-11-05 硬切：TargetKinds 列已删**——本字段退役删除；
        /// kinds 视图经 <see cref="GetTargetKindList"/> 自 Domain 派生；候选走 TargetAxis）。
        /// 卡效果 entry.kinds（实例收窄）保留在 AtomicEffectEntry——不经本字段。</summary>

        /// <summary>域内属性过滤（逗号分隔属性 token：Damaged/Tapped/Untapped/Stealth/Power&gt;N 等）。
        /// 目标域重做 P0 契约：**只留属性 token**——声明了新语法 Domain 的行禁用类别性 token
        ///（NoRole/NotRole/Player/Living/Field——TargetAxisRules.FilterConflicts 拒）。</summary>
        public string TargetFilter;

        /// <summary>目标域声明（目标域重做 P0·docs/目标域契约.md）：**新选择器语法优先**
        ///（domain := selector (";" selector)*；selector := region (":" category)*）——合法即产轴
        ///（converter→TargetAxis→TargetAxisResolver）。旧 EffectDomain 词表 token 识别为 legacy：
        /// 不产轴，回落 TargetKinds 旧口径（试点期兼容，P2 删）。空=未声明（旧 kinds 口径）。</summary>
        public string Domain;

        // ---- 参照域三列（2026-11-08 目标选择重构 §2.1）：仅分段原子（活体炸弹/护甲）声明，其余行留空 ----
        /// <summary>参照域声明：复用 Domain 同一套选择器文法；与 Domain 各自独立产轴
        ///（converter→ReferenceAxis），不再用 entry.kinds 顶替参照域。</summary>
        public string RefDomain;

        /// <summary>参照侧属性过滤（2026-11-08 护盾猛击参照化）：逗号分隔属性 token
        ///（Armored/Damaged/…，同 TargetFilter 词表）——挂在参照轴上随候选解析生效。
        /// RefDomain 表结构、RefFilter 表状态：护盾猛击「具有护甲的己方单位」=两者合取。</summary>
        public string RefFilter;

        /// <summary>参照收集时机：Cast=声明期选（取消参照=撤销发动）；Resolve=结算期问；
        /// Auto=不弹窗自动取。string 存枚举名（表列直通；装载校验口=AtomicEffectTable.BuildConfig）。</summary>
        public string RefPick;

        /// <summary>参照候选为空是否空转（当前行为=true）。bool 列——xlsm 回导可能变 0/1 或
        /// "true"/"false"，Newtonsoft 绑定 bool 天然兼容三态。缺省 true。</summary>
        public bool RefRequired = true;

        /// <summary>极性（2026-09-10 定案）：-1=对对手释放有益（伤害/削弱类）；+1=对己方释放有益（治疗/增益类）；
        /// 0=中性。表级列（EffectType 固有语义，实例不可覆盖）。配合 TargetKind 域侧别做错边折价（CostDerivation）。</summary>
        public float Polarity;

        /// <summary>可装载范围（2026-09-11 定案；2026-10-06 中文化）：CSV MountKind **中文名**
        ///（入效果栏/关键词/指示物/分支奖励/随机幅度/连接光环/系统/引擎主干/规则光环，
        /// 解析双轨兼容序号）。空 = 未声明兜底不限。显性化装载位，
        /// 取代用 TargetKinds 反推主动/关键词/指示物的隐式口径。</summary>
        public string MountKinds;

        /// <summary>默认触发时机（仅对触发式效果有效）</summary>
        public string DefaultTriggerTiming;

        /// <summary>默认发动条件（逗号分隔）</summary>
        public string DefaultConditions;

        // Stackable（是否可叠加）字段已删（2026-10-31 指示物定案）：指示物默认恒可叠加，
        // 无非叠加档、零读者——叠加不再作为效果文本/配置面描述。

        /// <summary>结算优先级（0-100，值越高越先结算）</summary>
        public int Priority;

        /// <summary>效果标签（逗号分隔；2026-10-06 中文化——产出族词表：伤害产出族/宣言产出族/
        /// 预言产出族/诅咒产出族，表色由加载器追加中文色名）</summary>
        public string Tags;

        /// <summary>门槛语义 id（2026-10-23 有限分支入表）：仅 MountKinds 含「有限分支」位的门槛行有意义
        ///（如 LifeBelowOpp）——目录（ComposerCatalog.SituationGates）与预算（BuildGatePremium）的派生源；
        /// 判定谓词仍由 BranchConditionEvaluator 代码注册。其余行 null。</summary>
        public string GateId;

        /// <summary>门槛奖励锚价上限（2026-10-23 有限分支入表）：门槛行 Then 奖励的放置上限（灰）；
        /// 其余行 0（无门槛语义）。</summary>
        public int Budget;

        /// <summary>可选触发时机（逗号分隔的 TriggerTiming 枚举名，空表示无触发配置）</summary>
        public string AvailableTriggerTimings;

        /// <summary>关联效果枚举名（逗号分隔，常搭配出现的效果）</summary>
        public string RelatedEffects;

        /// <summary>设计备注</summary>
        public string Notes;

        /// <summary>特殊声明（2026-10-31 阶段B 表「特殊」列）：文案没说、但引擎实际这么做的规则
        ///（中文短句）——纯展示层追加（AtomText.RenderCore 单点，合成器效果描述/卡面 tooltip 共用），
        /// 不改 DisplayName 模板、不改行 ID。**2026-11-05 起 ID 冻结**，可随意改模板：ID 不再随文本漂移，
        /// 文本哈希退为 TextHash 列只做查重/漂移检测（见 Config/atom_id_ledger.py）。</summary>
        public string Special;

        /// <summary>实例级字符串参数声明（2026-10-31 阶段B 表「参数」列）：本行实例需要什么 str。
        /// None=不需要；CardRef=引用卡 ID（装载期须在卡库解析到该卡）；EffectRef=引用效果库条目 id
        ///（2026-11-06 附加诅咒/祝福载荷效果化：附加诅咒→极性 −1 效果、附加祝福→极性 +1 效果，
        /// 须为主动效果——资格单源 EffectEligibility.CursePayloadRejectReason）；CardName=宣言卡名；
        /// Dimension=「维度:值」编码。装载校验口=CardLoader.ValidateAtomParams（空 str 报错点名行名，
        /// 不再静默空转/退回默认档）。</summary>
        public AtomicParamKind Param;

        /// <summary>
        /// 获取关联效果类型列表
        /// </summary>
        public List<string> GetRelatedEffectList()
        {
            if (string.IsNullOrEmpty(RelatedEffects)) return new List<string>();
            var list = new List<string>();
            foreach (var s in RelatedEffects.Split(','))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        /// <summary>
        /// 获取标签列表
        /// </summary>
        public List<string> GetTagList()
        {
            if (string.IsNullOrEmpty(Tags)) return new List<string>();
            var list = new List<string>();
            foreach (var s in Tags.Split(','))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        /// <summary>
        /// 获取可选触发时机列表
        /// </summary>
        public List<string> GetAvailableTriggerTimingList()
        {
            if (string.IsNullOrEmpty(AvailableTriggerTimings)) return new List<string>();
            var list = new List<string>();
            foreach (var s in AvailableTriggerTimings.Split(','))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        /// <summary>
        /// 目标种类集（**B2 2026-11-05 硬切：TargetKinds 列已删——kinds 视图自 Domain 新语法派生**）。
        /// 派生口径（区级语义，供 SideLock/隐藏区/装载校验等 17 个消费口）：自己→{0}；
        /// 战场 selector→对应侧单位 kind（Own→1/Enemy→2/Both→1,2）；区域 selector→Zone+Side 匹配的
        /// 区域 kind（5..16）；角色 selector→对应侧单位 kind（角色经单位 kind 承载侧别——Player 滤器语义）。
        /// 类别细分（生物/结界/法术/卡）不进 kinds——候选走 TargetAxis（TargetAxisResolver）。
        /// 空 Domain（无目标行）= 空集。
        /// </summary>
        public List<int> GetTargetKindList() => KindListOf(Domain);

        /// <summary>战场 selector 是否含结界类别（派生无生命位 3/4 的条件）。</summary>
        private bool wantNonLivingFor(CardCore.TargetSelector s)
            => (s.Categories & CardCore.TargetCategory.Enchantment) != 0;

        /// <summary>参照种类集（2026-11-08 目标选择重构 §2.1）：与目标域同一派生口径，源=RefDomain。
        /// 空/不可解析 RefDomain = 空集（非分段原子）。</summary>
        public List<int> GetReferenceKindList() => KindListOf(RefDomain);

        /// <summary>域声明 → 区级 kinds 视图（GetTargetKindList 的参数化本体）。</summary>
        private List<int> KindListOf(string domain)
        {
            if (!CardCore.TargetAxisRules.TryParseDomain(domain, out var axis))
                return new List<int>();
            var kinds = new List<int>();
            foreach (var s in axis.Selectors)
            {
                if (s == null) continue;
                if (s.Region == CardCore.TargetRegion.Self)
                {
                    if (!kinds.Contains(0)) kinds.Add(0);
                    continue;
                }
                var zone = CardCore.TargetRegionRules.ZoneOf(s.Region);
                var side = CardCore.TargetRegionRules.SideOf(s.Region);
                // 类别细分（2026-11-05 B2 修正）：战场 selector 的类别含结界才派生无生命位 3/4，
                // 否则只派生有生命位 1/2——沉睡/牺牲等生物域行派生保持旧口径 {1,2}。
                // 注：单位 kind 1-4 是跨区实体类（TargetKindRules.ZoneOf 返回 None）——战场位在此手写。
                if (zone == Zone.Battlefield)
                {
                    if (side == CardCore.RegionSide.Own || side == CardCore.RegionSide.Both)
                    {
                        if (!kinds.Contains(1)) kinds.Add(1);
                        if (wantNonLivingFor(s) && !kinds.Contains(3)) kinds.Add(3);
                    }
                    if (side == CardCore.RegionSide.Opposing || side == CardCore.RegionSide.Both)
                    {
                        if (!kinds.Contains(2)) kinds.Add(2);
                        if (wantNonLivingFor(s) && !kinds.Contains(4)) kinds.Add(4);
                    }
                    continue;
                }
                for (int k = 5; k <= 16; k++)
                {
                    var (kZone, kOwn) = CardCore.TargetKindRules.ZoneOf(k);
                    if (kZone != zone) continue;
                    bool sideMatch = side == CardCore.RegionSide.Both
                        || (side == CardCore.RegionSide.Own && kOwn)
                        || (side == CardCore.RegionSide.Opposing && !kOwn);
                    if (sideMatch && !kinds.Contains(k)) kinds.Add(k);
                }
            }
            kinds.Sort();
            return kinds;
        }
    }

    /// <summary>原子行实例级字符串参数声明（2026-10-31 阶段B 表「参数」列）：
    /// None=不需要 str；CardRef=引用卡 ID；EffectRef=引用效果库条目 id（2026-11-06 附加诅咒/祝福
    /// 载荷效果化——装载期按 EffectsLibrary 解析+资格过滤）；CardName=宣言卡名；Dimension=维度:值 编码。
    /// 缺省（表内不写键）= None。</summary>
    public enum AtomicParamKind
    {
        None,
        CardRef,
        EffectRef,
        CardName,
        Dimension,
    }

    #region 属性枚举定义

    // EffectTargetType / EffectTargetScope 已删除（2026-09-10 目标域模型）：
    // 取代者 = TargetKind 序号集合（Effects/TargetKind.cs）+ 组合层 SelectionMode。
    // 旧→新迁移映射见 Config/m1_schema_migration.py 与 Config/target_kinds_review.csv。

    // 效果持续时间类型 EffectDurationType 已删除：
    // 与运行时 DurationType（Enums/Zones.cs）合并为单一真相源，
    // 消除 CardEffectConverter 曾以 int 强转跨枚举导致的错位映射
    // （旧表侧 Permanent=4 会被误转为运行时 WhileCondition=4）。
    // 旧 JSON 里的表侧枚举名经 AtomicEffectTable.ParseDurationType 别名归一。

    // EffectTier 三分类枚举已删除（2026-09-10：零消费——关键词认 Grant 前缀、指示物认 CounterRules spec、合成编辑性不看档）。

    #endregion
}
