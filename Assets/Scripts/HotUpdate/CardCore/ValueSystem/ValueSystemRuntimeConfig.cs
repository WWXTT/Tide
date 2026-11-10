using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore.Attribute; // AtomicEffectTable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CardCore
{
    /// <summary>
    /// 价值系统运行时配置
    /// 用于卡牌和效果价值计算。
    /// 各子配置的字段名 = ValueSystemConfig.json 的 section 字段名（section 名+\"Config\" 对应同类字段，
    /// 2026-11-10 由 Category/Key/Value/Des KV 数组结构化为 section 对象），表值由
    /// ValueSystemConfigManager 按 section PopulateObject 灌入；字段初始化器仅为表缺失时的代码兜底。
    /// </summary>
    [Serializable]
    public class ValueSystemRuntimeConfig
    {
        public AttributeValueConfig AttributeValueConfig = new AttributeValueConfig();
        public CardCostConfig CardCostConfig = new CardCostConfig();
        public CardCompositionConfig CardCompositionConfig = new CardCompositionConfig();
        public PricingTierConfig PricingTierConfig = new PricingTierConfig();
        public TimingFactorConfig TimingFactorConfig = new TimingFactorConfig();
        public AtomTierConfig AtomTierConfig = new AtomTierConfig();
        public GameRulesConfig GameRulesConfig = new GameRulesConfig();
    }

    // 注：SummonDropConfig（衍生物落区系数，表 Category=SummonDrop）已删除（2026-10-09）——
    // 原子落区内生：衍生物恒落战场、临时卡恒入手牌，落区价值已含在模板卡/原子自身费用里，
    // 不再按落区外乘分档系数。

    // 注：TargetModifier（按目标范围/AOE 的计价乘数）已删除——计价只看目标数量（固定 N ×N，见
    // CostDerivationService.EffectiveTargetCountForCost）；范围/Scope 仅是目标选取元数据，不参与计价。
    // 注：Synergy（同类递减罚重复/多样协同奖多样）已删除——单卡计价由规则一（CardCostService）完整承担。

    /// <summary>
    /// 卡层组合费用配置（表 Category=CardComposition）——「效果→卡」组合层的额外费用参数。
    /// 与原子层（CostDerivation：原子→效果锚价）、规则一（CardCostService.Derive）正交：
    /// 本层只对「卡的结构性弹性」收税，独立演进。
    /// </summary>
    [Serializable]
    public class CardCompositionConfig
    {
        /// <summary>底盘预算（2026-09-10 攻/守效果化）：免费额度覆盖 攻+守+1 效果槽（默认 3）。</summary>
        public float ChassisBudget = 3f;
        /// <summary>底盘单项费率：攻/守/每个效果槽各占 1。</summary>
        public float ChassisItemRate = 1f;
    }

    /// <summary>
    /// 计价档位系数（表 Category=PricingTier，2026-10-05 用户定案）——只保留目标数量的增量系数：
    /// 数量 1:1 / 2:1.5 / 3:2 / 全部:3。消费方 CostDerivation.QuantityFactor。
    /// **作用次数档已退役**（2026-10-30 定案）：效果不再有每回合生效次数上限，
    /// TriggerLimit1/2/3/Infinite 与 TriggerLimitFactor 整删（此前已零消费，TriggerCostFactor 恒 1）。
    /// **发动时机系数已迁出本段**：2026-10-30 起配在 Category=TimingFactor——2026-11-05 收敛为
    /// 单次/多次两档，2026-11-09 再拆出生物专属档（攻/守/横置 ×1.5，仅生物可挂，
    /// 见 TimingFactorConfig），旧 TimingOnceFactor×0.8 / TimingRepeatFactor×1 两档与 31 行期望次数表先后删除。
    /// 速度档已随 2026-10-19 速度计价退役整删——三类卡分类把发动速度特化为法术专属类特性
    ///（速度=3−槽数、其余主动默认 0），不再是可购溢价轴。
    /// </summary>
    [Serializable]
    public class PricingTierConfig
    {
        public float TargetCount1 = 1.0f;        // 目标数量 1：基准
        public float TargetCount2 = 1.5f;        // 目标数量 2
        public float TargetCount3 = 2.0f;        // 目标数量 3
        public float TargetCountAll = 3.0f;      // 目标数量 全部（0=域内全取；-1 任意同此档）

        /// <summary>目标数量→计价系数。越界口径：N&gt;3 视同全部；-1（任意·运行时自选）同全部；-2/其他未声明=单目标基准。</summary>
        public float TargetCountFactor(int count)
        {
            switch (count)
            {
                case 1: return TargetCount1;
                case 2: return TargetCount2;
                case 3: return TargetCount3;
                case 0: return TargetCountAll;
                case -1: return TargetCountAll;
                default: return count > 3 ? TargetCountAll : TargetCount1;
            }
        }
    }

    /// <summary>
    /// 发动时机系数（表 Category=TimingFactor）——2026-11-10 时机系数规格 v0.1 起**退化为桶默认**：
    /// TimingWeights.json（35 行逐时机 f）Enabled=true 且行命中时计价读行 Factor，本段只在
    /// 影子模式（Enabled=false，默认——恒走本段，不动任何卡价）与新时机缺权重行时兜底：
    /// · 单次 Once=1：付清后只发生一次（登场/死亡/破坏/除外/离场/墓地回场/游戏开始/角色死亡）；
    /// · 多次 Repeat=1：回合/阶段/抽牌/伤害/事件观察族——仅结界可挂；
    /// · 生物专属 CreatureOnly=1.5：攻/守/横置族——只有生物能挂（结界不横置、法术不驻场）。
    /// **段 2b 切换单**（Enabled:false→true 同批）：本值 1.5 → 1.0——新口径下攻/守/横置族次数由
    /// 横置承担、已由 f 计价，不再叠加 1.5（缺行兜底也不该多收）；影子期间保持 1.5 = 旧价不变。
    /// 单据 FiresOncePerPaidLifetime 判单次，EffectEligibility.IsTapAnchoredTiming 判生物专属
    /// （CostDerivation 单源）；主动发动（Activate_*）与引擎载荷恒 1（TimingCostFactor 内判，不乘表）。
    /// 消费方 CostDerivation.TimingCostFactor[Of]/IsRepeatTiming；合成器时机下拉档位标签与本段同源。
    /// 旧 31 行逐时机期望次数表（2026-10-30 上线）已整删；2026-11-05 收敛两档 ×1；
    /// 2026-11-09 拆出生物专属档 ×1.5；2026-11-10 起由 TimingWeights.json 逐时机表接管（影子开关）。
    /// </summary>
    [Serializable]
    public class TimingFactorConfig
    {
        public float Once = 1f;           // 桶默认·单次档（FiresOncePerPaidLifetime=true 的时机）：基准 ×1
        public float Repeat = 1f;         // 桶默认·多次档（周期/观察族，结界专属）：×1（影子模式=现行价；新口径下次数由 f 计价）
        public float CreatureOnly = 1.5f; // 桶默认·生物专属档（攻/守/横置族）：影子期间 ×1.5（旧价不变）；段 2b 切换时改 1.0（次数已由 f 计价）
    }

    /// <summary>
    /// 原子档位计价（表 Category=AtomTier，2026-11-10 双轨定案；同日改版）——原子表 Tier 写法的
    /// 价格公式：价格 = Base + Slope × d（d=档位位移）。**档位名直接用 d 值**（Dm1/D0/Dp1…），
    /// 不用语义名——同一档位会被不同语义共用（伤害 1 与洗回 1 都是 P=1，一个是「数值」一个是
    /// 「循环」），语义名会误导；同档异语义不是巧合：1 伤害 = 1 费 = 洗回 1 张 都在「比基准低
    /// 1 档」的位置上。一档一价全行共享：调一个档位 = 改 ValueSystemConfig 一行，全表跟随；
    /// 原子表「能推的不写 ManaList」，只写 Tier（颜色走 AtomColorRules.For 单源生成）。
    /// 关键词/光环/压制阶梯指示物等不在 d 轴的行直写 ManaList 不受影响；Tier 与 ManaList 同行 =
    /// 双源冲突装载期 Error（AtomicEffectTable.BuildConfig 单口展开）。
    /// 八档代入（现表值 Base=3/Slope=2，覆盖表内现有全部价格档）：
    /// Dm1_5 −1.5→0 / Dm1_25 −1.25→0.5 / Dm1 −1→1 / Dm0_5 −0.5→2 / D0 0→3 / Dp1 1→5 /
    /// Dp1_5 1.5→6 / Dp3 3→9。
    /// 每档的同类语义清单（仅供理解，不入价格）见 ValueSystemConfig.json 的 Des 列；
    /// 对账口=验证器 P9-8 双轨段（价差/色差零容忍）。
    /// </summary>
    [Serializable]
    public class AtomTierConfig
    {
        public float Base = 3f;      // 档位推导截距＝卡底座（一张牌的价值）
        public float Slope = 2f;     // 每档位移价（1 档 d = 2 费）
        public float Dm1_5 = -1.5f;  // P=0：载体（模拟/变形/牺牲/预言）
        public float Dm1_25 = -1.25f; // P=0.5：数值增量（回血/护甲/攻/血/耐久/毒素/易损/展示；永久属性基础单价×2后=1）
        public float Dm1 = -1f;      // P=1：数值 1 点、回到循环起点（伤害/洗回/激励/占卜/锁定/静默/坚韧=2×0.5）
        public float Dm0_5 = -0.5f;  // P=2：恢复可用性（抽/回收/磨/窃取/发动无效/移动/筛选展示/回响/流失/攻守互换）
        public float D0 = 0f;        // P=3：等价交换（消灭/摧毁/弹回/弃牌/封印/强夺/驱散/净化/效果无效/跳过结束阶段）
        public float Dp1 = 1f;       // P=5：不可逆/获得资产（除外/控制/苏生/检索/跳过准备/主要阶段）
        public float Dp1_5 = 1.5f;   // P=6：永久移除/归属改写（抹除/操控）
        public float Dp3 = 3f;       // P=9：整回合（跳过回合/额外回合）

        /// <summary>档位名 → d。按名反射本类 float 字段、跳过 Base/Slope——加档位 = 加字段即接通
        ///（与装载器 section 按名映射同精神，不维护第二份映射）。未知档位返回 false：
        /// 装载期 Error 点名行，不静默回落。</summary>
        public bool TryGetD(string tier, out float d)
        {
            d = 0f;
            if (string.IsNullOrEmpty(tier)) return false;
            if (tier == nameof(Base) || tier == nameof(Slope)) return false;
            var field = typeof(AtomTierConfig).GetField(tier);
            if (field == null || field.FieldType != typeof(float)) return false;
            d = (float)field.GetValue(this);
            return true;
        }

        /// <summary>档位名 → 价格 = Base + Slope×d。未知档位返回 NaN（毒值显性传播）——
        /// 调用方须先 TryGetD 把关，勿裸调。</summary>
        public float PriceOf(string tier)
            => TryGetD(tier, out var d) ? Base + Slope * d : float.NaN;
    }

    // 注：TimingModifier / CostValue / EffectValue / TriggerValue / CardTypeValue 五类配置
    // 已于 2026-10-05 删除——全仓无消费方（时机轴未接入计价、EffectValue 委托原子表后无人调用、
    // CardTypeValue 对应的卡型价值未使用）；ValueSystemConfig.json 同批清理 33 行死行。
    // 2026-10-16 修订：时机轴已接入计价——走 PricingTierConfig.TimingOnceFactor 单值（一发时机 ×0.8）。
    // 2026-10-30 再修订：两档合表改为逐时机期望触发次数（TimingFactorConfig，Category=TimingFactor）。
    // 2026-11-05 定案：期望次数口径退役——收敛为单次/多次两档且均 ×1（多次成本由结界耐久承担），
    // TimingFactorConfig 缩为 Once/Repeat 两字段，31 行逐时机表整删。
    // 2026-11-09 修订：攻/守/横置族为生物专属状态（结界不横置、法术不驻场），从多次档拆出
    // CreatureOnly=1.5 单列计价，只有生物可挂；Repeat 回归周期/观察族结界专属口径。

    /// <summary>
    /// 开局/全局规则数值（表 Category=GameRules，2026-10-13 迁表定案：逻辑不动、数值出代码——
    /// 审计裁定硬编码数值不留在引擎）。消费方：GameCore（InitialLife/OpeningHandSize）、
    /// RuleHooks（DefaultHandLimit）、ZoneManager（BattlefieldCapacityPerPlayer）、
    /// ElementPool/ResourceCurveModel（LandCurveCap=自然上限）、EffectExecutor（MaxTriggersPerTurnPerEffect=
    /// 自馈闸门）。MaxStackedLandCap 已删（2026-10-15：成长叠加无封顶）。
    /// </summary>
    [Serializable]
    public class GameRulesConfig
    {
        public float InitialLife = 30f;                  // 初始生命（Reset 跨局复位同用）
        public float OpeningHandSize = 6f;               // 起手手牌数（双方对称抽满；引擎不补抽）
        public float DefaultHandLimit = 7f;              // 手牌上限基值（回合末选弃；IHandLimitModifier 修改链之上叠加）
        public float BattlefieldCapacityPerPlayer = 9f;  // 每玩家战场单位容量（=单位区 4+5；P11 盘面重塑；BoardLayout.UnitCellsPerPlayer 单源引用）
        public float LandCurveCap = 10f;                 // 标准地牌槽曲线自然封顶 [1..cap]（2026-10-15：7→10，每次回合开始双方同步 +1；成长等效果增加在此之外无限制）
        public float SecondPlayerArmorBonus = 5f;        // 后手开局护甲补偿（2026-10-30 迁表：后手角色 +N 层护甲指示物；本地木桩/联机/教学开局注入共用 GameCore.SecondPlayerArmorBonus 单口）
        /// <summary>自馈闸门（2026-10-30 用户定案）：同一来源实例+效果 Id 每回合最多触发次数——
        /// 效果已无「一回合一次」限制，本值只用于截断自循环（抽牌时抽牌/受伤时造成伤害等）。
        /// 常规触发远低于 30；引擎载荷与结界耐久经济不叠闸（各自天然上限）。</summary>
        public float MaxTriggersPerTurnPerEffect = 30f;
    }

    /// <summary>
    /// 属性价值配置
    /// </summary>
    [Serializable]
    public class AttributeValueConfig
    {
        // ==== 持续时间折扣（时间换费用：持续越短越便宜，曲线为凹函数——每多 1 回合的增量递减）====
        // 用法为「相对折扣」：CostDerivation 按 D(实际持续)/D(表内默认) 计价，
        // 保证既有锚点（1 伤害 = 1 元素，伤害原子表默认 Once）不因接入而漂移。
        public float OnceDiscount = 0.5f;                  // 瞬发（本回合内即时结算）
        public float UntilEndOfTurnDiscount = 0.6f;        // 到持有者回合结束（限时指示物统一档，2026-09-16 两档合一）
        public float UntilNextTurnDiscount = 0.6f;         // ≡UntilEndOfTurn（2026-09-16 统一档：运行时语义与费用均按持有者回合结束=1回合档）
        public float WhileConditionDiscount = 0.75f;       // 条件满足期间
        public float UntilLeaveBattlefieldDiscount = 0.8f; // 到离场（挂在实体上，实体亡即失效）
        public float PermanentDiscount = 1.0f;             // 本局有效（永续档，复利载体）
        // ForTurnsBase/ForTurnsStep/ForTurnsCap 三字段已删（2026-10-13 死配置清理）：
        // 2026-09-14 收缩后零消费——GetDurationDiscount 对 ForTurns 恒回落 UntilNextTurnDiscount。

        /// <summary>
        /// 持续时间折扣（绝对系数）。2026-09-14 收缩：效果级 DurationValue 删除（ForTurns 档
        /// 收缩后 ≡ 专档同义词），turns 参数退役——ForTurns 仅存于指示物时钟（CounterSpec.Turns）。
        /// 费用侧请用相对折扣：D(实际)/D(表内默认)，见 CostDerivationService.ComputeAtomCost。
        /// </summary>
        public float GetDurationDiscount(DurationType duration)
        {
            return duration switch
            {
                DurationType.Once => OnceDiscount,
                DurationType.Permanent => PermanentDiscount,
                DurationType.UntilEndOfTurn => UntilEndOfTurnDiscount,
                DurationType.UntilNextTurn => UntilNextTurnDiscount,
                DurationType.UntilLeaveBattlefield => UntilLeaveBattlefieldDiscount,
                DurationType.WhileCondition => WhileConditionDiscount,
                DurationType.ForTurns => UntilNextTurnDiscount, // 效果级已不可声明；兜底按 2 回合档
                _ => 0.8f
            };
        }
    }

    /// <summary>
    /// 卡牌计价配置（表 Category=CardCost，ValueSystemConfig.json）——规则一·平衡的统一推导参数。
    /// 代价当量表已删（2026-09-14 代价原子化）：弃牌/送墓/流失等资源支付的补偿一律按
    /// Payload 原子全价（原子表唯一锚），不再有第二套当量。
    /// </summary>
    [Serializable]
    public class CardCostConfig
    {
        public float StatUnit = 2f;                     // 1费=StatUnit点属性（攻血各 1/StatUnit 元素，灰）
        // MaxTier 已删（2026-11-03）：档帽=GameRules.LandCurveCap 单源（CardCostService.Derive 直读）——
        // 旧独立值 14 是地牌封顶 14 时代的残留（池侧 2026-10-15 已退役，本字段是最后一处）。
        /// <summary>属性锚：+1 攻/+1 生命 = 0.5（攻血同锚；2026-10-05 自 CostDerivation 常量迁表）。</summary>
        public float StatAnchor = 0.5f;
        /// <summary>修改族持续档乘数：换区移除/条件持续 = 锚×3（=1.5/+1）；连接光环档同此乘数。</summary>
        public float StatSustainMultiplier = 3f;
        /// <summary>修改族永久档乘数：换区不移除 = 锚×4（=2.0/+1）。</summary>
        public float StatPermanentMultiplier = 4f;
        /// <summary>改写族恒价：Set*/费用永久直改 = 3.0/+1。</summary>
        public float StatRewriteFlatCost = 3f;
        /// <summary>双方同时作用减半系数（双侧全取/规则光环 ×0.5；2026-10-05 迁表）。</summary>
        public float SymmetricDiscountFactor = 0.5f;
        /// <summary>耐久单价：0.5/点（色=蓝——2026-11-03 耐久改蓝定案，落桶见 CardCostService.StatBucketsOf；
        /// 2026-10-16 回调 0.5 灰/点口径退役）。</summary>
        public float DurabilityUnitCost = 0.5f;
        /// <summary>场地光环（原规则光环）单侧溢价系数（2026-10-30 定案）：极性 ±1 仪典作用面收窄单侧
        ///（+1 仅己方 / −1 仅对方）后，表价（按对称双方校准）× 本系数；双方档仍 ×1。具体值用户可调。</summary>
        public float RuleAuraSingleSideFactor = 1.5f;
    }

    /// <summary>
    /// 挂载延迟折扣 d(C) 已整删（2026-10-31 用户定案：阶梯性减价退役——无前中后期卡组概念，
    /// 所有卡组开局即榨取整体能力上限，S+E+K 全额计价不再按额度打折；DelayDiscountConfig 类、
    /// ValueSystemConfig.json DelayDiscount 行、CardCost.KeywordsShareDelayDiscount 行同步移除）。
    /// </summary>

    /// <summary>
    /// 价值系统配置管理器 - 单例。
    /// 首次取用时从 Assets/Configs/ValueSystemConfig.json 灌入配置（2026-11-10 结构化迁移：
    /// section 对象，section 名=Category、字段名=Key、Des 已转 // 行注释；顶层裸数组旧格式仅回滚兼容），
    /// 文件缺失/解析失败则退回 ValueSystemRuntimeConfig 字段初始化器的代码默认值。
    /// </summary>
    public class ValueSystemConfigManager
    {
        // 相对 DataPath 的配置路径（2026-10-18 寻址收口走 TidePaths.ReadConfigText：宿主 → 直读 → StreamingAssets 随包档）
        private const string ConfigRelativePath = "Configs/ValueSystemConfig.json";

        private static ValueSystemConfigManager _instance;
        public static ValueSystemConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ValueSystemConfigManager();
                }
                return _instance;
            }
        }

        private ValueSystemRuntimeConfig _config;
        private int _configVersion = 1;

        public int ConfigVersion => _configVersion;

        public ValueSystemRuntimeConfig GetOrCreateConfig()
        {
            if (_config == null)
            {
                _config = LoadFromJson() ?? new ValueSystemRuntimeConfig();
            }
            return _config;
        }

        public void SetConfig(ValueSystemRuntimeConfig config)
        {
            _config = config;
            _configVersion++;
        }

        public void InvalidateConfig()
        {
            _configVersion++;
        }

        // ======================================== 表装载 ========================================

        /// <summary>
        /// 读表并灌入配置，返回 null 表示放弃（调用方退回代码默认值）。
        /// 2026-11-10 结构化迁移：顶层 { = section 对象（section 名=Category、字段名=Key，Des 转行注释）——
        /// section → ValueSystemRuntimeConfig 同名字段（+"Config"）定位后 JsonConvert.PopulateObject 整段灌入
        /// （MissingMemberHandling.Error：未知 Key 硬错，不再 Warn 跳过）。表里加字段、代码加同名字段即接通。
        /// 顶层 [ = 旧 KV 数组（xlsm 导出器口径）——回滚兼容分支保留：按 Category 分组转 section 后走同一
        /// PopulateObject 管线，不再有第二套逐条反射灌入。
        /// </summary>
        private static ValueSystemRuntimeConfig LoadFromJson()
        {
            // 2026-10-18 寻址收口：统一 TidePaths.ReadConfigText（玩家构建命中 StreamingAssets 随包档）
            string raw = TidePaths.ReadConfigText(ConfigRelativePath);
            if (string.IsNullOrEmpty(raw))
            {
                TideLog.Warn($"[ValueSystemConfigManager] 配置不可读：{ConfigRelativePath}，使用代码默认值");
                return null;
            }

            var config = new ValueSystemRuntimeConfig(); // 字段初始化器 = 兜底默认值，表值逐段覆盖
            string trimmed = raw.TrimStart();
            string sectionsJson;
            if (trimmed.StartsWith("{"))
            {
                sectionsJson = raw; // 新格式：section 对象（// 行注释 JObject.Parse 直接容忍）
            }
            else if (trimmed.StartsWith("["))
            {
                // 旧格式 KV 数组（迁移前导出器口径）：转 section 对象复用同一灌入管线（回滚零行为差）
                try
                {
                    sectionsJson = LegacyKvToSections(raw);
                }
                catch (Exception e)
                {
                    TideLog.Warn($"[ValueSystemConfigManager] 解析 {ConfigRelativePath}（旧 KV 数组）失败: {e.Message}，使用代码默认值");
                    return config;
                }
            }
            else
            {
                TideLog.Warn($"[ValueSystemConfigManager] 配置顶层既非对象也非数组：{ConfigRelativePath}，使用代码默认值");
                return null;
            }

            // CostOffsetConfig.json 已删（2026-09-14 代价原子化）：CardCost 三行（StatUnit/
            // KeywordsShareDelayDiscount/MaxTier）已回迁本表；DelayDiscount 行与 KeywordsShareDelayDiscount
            // 已随 2026-10-31 阶梯减价删除整链退役；机制行与当量行随抵消系统退役。
            int applied = ApplySections(sectionsJson, config);
            if (applied == 0)
                TideLog.Warn($"[ValueSystemConfigManager] 未从 {ConfigRelativePath} 灌入任何条目");
            return config;
        }

        /// <summary>section 对象装载（新格式；旧 KV 数组经 LegacyKvToSections 转换后同走此口）。
        /// 未知 section / 无同名字段 = Error 点名；section 内未知 Key 由 PopulateObject
        /// （MissingMemberHandling.Error）抛错转 Error 点名——表结构漂移装载期显性暴露，不静默跳过。</summary>
        private static int ApplySections(string raw, ValueSystemRuntimeConfig config)
        {
            JObject root;
            try
            {
                root = JObject.Parse(raw);
            }
            catch (Exception e)
            {
                TideLog.Warn($"[ValueSystemConfigManager] 解析 {ConfigRelativePath} 失败: {e.Message}，使用代码默认值");
                return 0;
            }

            int applied = 0;
            var hits = new List<string>();
            foreach (var prop in root.Properties())
            {
                var section = typeof(ValueSystemRuntimeConfig).GetField(prop.Name + "Config");
                if (section == null)
                {
                    TideLog.Error($"[ValueSystemConfigManager] 未知 section '{prop.Name}'（ValueSystemRuntimeConfig 无 {prop.Name}Config 字段），未灌入");
                    continue;
                }
                if (!(prop.Value is JObject sec))
                {
                    TideLog.Error($"[ValueSystemConfigManager] section '{prop.Name}' 不是对象，未灌入");
                    continue;
                }
                try
                {
                    JsonConvert.PopulateObject(sec.ToString(), section.GetValue(config),
                        new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
                }
                catch (Exception e)
                {
                    TideLog.Error($"[ValueSystemConfigManager] section '{prop.Name}' 灌入失败（未知 Key 或类型不符）: {e.Message}");
                    continue;
                }
                applied += sec.Count;
                hits.Add($"{prop.Name}({sec.Count})");
            }
            if (hits.Count > 0)
                TideLog.Info($"[ValueSystemConfigManager] 结构化装载 {hits.Count} section 命中：{string.Join("/", hits)}——共 {applied} 字段");
            return applied;
        }

        /// <summary>旧格式 KV 数组（Category/Key/Value/Des 裸数组）→ section 对象：按 Category 分组，
        /// Key→字段名、Value→字段值（Des 丢弃）。回滚兼容分支，结构与语义不再演进。</summary>
        private static string LegacyKvToSections(string raw)
        {
            var root = new JObject();
            foreach (var item in JArray.Parse(raw).OfType<JObject>())
            {
                string category = (string)item["Category"];
                string key = (string)item["Key"];
                if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key)) continue;
                var sec = root[category] as JObject;
                if (sec == null)
                {
                    sec = new JObject();
                    root[category] = sec;
                }
                sec[key] = item["Value"];
            }
            return root.ToString();
        }
    }
}
