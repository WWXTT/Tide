using System;
using System.Collections.Generic;
using System.IO;
using CardCore.Attribute; // AtomicEffectTable

namespace CardCore
{
    /// <summary>
    /// 价值系统运行时配置
    /// 用于卡牌和效果价值计算。
    /// 各子配置的字段名 = ValueSystemConfig.json 的 Key（Category+\"Config\" 对应同类字段），
    /// 表值由 ValueSystemConfigManager 按名反射灌入；字段初始化器仅为表缺失时的代码兜底。
    /// </summary>
    [Serializable]
    public class ValueSystemRuntimeConfig
    {
        public AttributeValueConfig AttributeValueConfig = new AttributeValueConfig();
        public CardCostConfig CardCostConfig = new CardCostConfig();
        public CardCompositionConfig CardCompositionConfig = new CardCompositionConfig();
        public PricingTierConfig PricingTierConfig = new PricingTierConfig();
        public TimingFactorConfig TimingFactorConfig = new TimingFactorConfig();
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
    /// 发动时机系数（表 Category=TimingFactor；2026-11-05 两档收敛，2026-11-09 拆出生物专属档）——
    /// 三档：单次 / 多次（周期·观察族，结界专属）/ 生物专属（攻/守/横置族）。
    /// · 单次 Once=1：付清后只发生一次（登场/死亡/破坏/除外/离场/墓地回场/游戏开始/角色死亡）；
    /// · 多次 Repeat=1：回合/阶段/抽牌/伤害/事件观察族——仅结界可挂，额外次数已由耐久支付
    ///   （每次触发 −1 耐久、0.5 蓝/点——耐久即次数池），不按次数加价；
    /// · 生物专属 CreatureOnly=1.5：攻/守/横置族——只有生物能挂（结界不横置、法术不驻场，
    ///   无从触发），宿主自身行动承担次数，按 ×1.5 计费。
    /// 单据 FiresOncePerPaidLifetime 判单次，EffectEligibility.IsTapAnchoredTiming 判生物专属
    /// （CostDerivation 单源）；主动发动（Activate_*）与引擎载荷恒 1（TimingCostFactor 内判，不乘表）。
    /// 消费方 CostDerivation.TimingCostFactor[Of]/IsRepeatTiming；合成器时机下拉档位标签与本段同源。
    /// 旧 31 行逐时机期望次数表（2026-10-30 上线）已整删——「存活期望 1.5 回合/暂定 2」口径退役。
    /// </summary>
    [Serializable]
    public class TimingFactorConfig
    {
        public float Once = 1f;           // 单次档（FiresOncePerPaidLifetime=true 的时机）：基准 ×1
        public float Repeat = 1f;         // 多次档（周期/观察族，结界专属）：×1——额外次数已由耐久支付，不按次数加价
        public float CreatureOnly = 1.5f; // 生物专属档（攻/守/横置族）：×1.5——只有生物可挂（结界不横置、法术不驻场）
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
    /// 首次取用时从 Assets/Configs/ValueSystemConfig.json（ValueSystem.xlsm 导出）灌入配置，
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
        /// 映射纯按名：Category+"Config" → ValueSystemRuntimeConfig 同名字段，Key → 子配置同名字段——
        /// 表里加行、代码加同名字段即自动接通，不维护第二份映射。未知 Category/Key 告警跳过（暴露表结构漂移）。
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

            var config = new ValueSystemRuntimeConfig(); // 字段初始化器 = 兜底默认值，表值逐条覆盖
            int applied = ApplyEntries(raw, config);

            // CostOffsetConfig.json 已删（2026-09-14 代价原子化）：CardCost 三行（StatUnit/
            // KeywordsShareDelayDiscount/MaxTier）已回迁本表；DelayDiscount 行与 KeywordsShareDelayDiscount
            // 已随 2026-10-31 阶梯减价删除整链退役；机制行与当量行随抵消系统退役。
            if (applied == 0)
                TideLog.Warn($"[ValueSystemConfigManager] 未从 {ConfigRelativePath} 灌入任何条目");
            return config;
        }

        /// <summary>解析 KV 条目并反射灌入（Category+"Config" → 同名字段；表加行+代码加同名字段即接通）。</summary>
        private static int ApplyEntries(string raw, ValueSystemRuntimeConfig config)
        {
            List<ValueSystemConfigEntry> entries;
            try
            {
                entries = ParseEntries(raw);
            }
            catch (Exception e)
            {
                TideLog.Warn($"[ValueSystemConfigManager] 解析 {ConfigRelativePath} 失败: {e.Message}，跳过该文件");
                return 0;
            }
            if (entries == null) return 0;

            int applied = 0;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Category) || string.IsNullOrEmpty(entry.Key))
                    continue;

                var section = typeof(ValueSystemRuntimeConfig).GetField(entry.Category + "Config");
                if (section == null)
                {
                    TideLog.Warn($"[ValueSystemConfigManager] 未知 Category='{entry.Category}'（Key={entry.Key}），已跳过");
                    continue;
                }

                var field = section.FieldType.GetField(entry.Key);
                if (field == null)
                {
                    TideLog.Warn($"[ValueSystemConfigManager] {entry.Category} 无同名字段 '{entry.Key}'，已跳过");
                    continue;
                }

                field.SetValue(section.GetValue(config), Convert.ChangeType(entry.Value, field.FieldType));
                applied++;
            }
            return applied;
        }

        /// <summary>解析 JSON（导出器每个 sheet 产出顶层裸数组，JsonUtility 需包一层；同 AtomicEffectTable.ParseEntries 惯例）</summary>
        private static List<ValueSystemConfigEntry> ParseEntries(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string trimmed = raw.TrimStart();
            string wrapped = trimmed.StartsWith("[")
                ? "{\"items\":" + raw + "}"
                : raw; // 已是对象（含 items）则直接用
                    var wrapper = TideJson.FromJson<ValueSystemConfigWrapper>(wrapped);
            return wrapper?.items;
        }

        // ======================================== JSON DTO ========================================

        [Serializable]
        private class ValueSystemConfigEntry
        {
            public string Category; // TargetModifier / TimingModifier / CostValue / ...（+Config = 同名字段）
            public string Key;      // 子配置同名字段名
            public float Value;     // 灌入值（int 字段自动转换）
        }

        [Serializable]
        private class ValueSystemConfigWrapper
        {
            public List<ValueSystemConfigEntry> items;
        }
    }
}
