using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 合成器共享目录（2026-09-14 合成器重做；2026-10-05 两槽定案重构）：
    /// 槽级分支条件目录（代码侧）——UI（EffectComposerScreen）与验证器（TestComposerModel）同源消费，防两套口径。
    ///
    /// 三条件族（BranchSettleKind）：
    /// - 有限分支（Gate）＝局面状态门：与原子产出无关、任意主干原子可挂，效果结算时评估一次；
    ///   达标→Then 奖励，不达标→不奖励也不惩罚（2026-10-09 定案——原对赌惩罚面不还原）；
    /// - 自由分支·产出条件（Outcome）＝读主干 per-target 产出（"伤害击杀 Then xx"），producer 匹配挂条件上；
    /// - 自由分支·事件引擎（Engine）＝倒计时/拼点等，条件时机内生于引擎事件（参数/预算见 EngineParamRange）。
    /// 条件评估真源仍是 BranchConditionEvaluator（运行时）；此处只暴露「用户可拼」目录与定价（GatePremium）。
    /// 预言族（延迟验证）不进目录（无 premium 定价口径——目录恢复时再议）。
    /// </summary>
    public static class ComposerCatalog
    {
        // ======================================== 自由分支·事件引擎 ========================================

        /// <summary>引擎主干行 → BranchEngineKind 映射（2026-10-05 回表定案；2026-10-08 附加诅咒/附加祝福入列）：
        /// 表行 EffectType 名（EngineCountdown 等）→ 引擎枚举；非引擎行返回 None。</summary>
        public static BranchEngineKind EngineKindOf(AtomicEffectType type)
        {
            switch (type)
            {
                case AtomicEffectType.EngineCountdown: return BranchEngineKind.Countdown;
                case AtomicEffectType.EngineLuckRoll: return BranchEngineKind.LuckRoll;
                case AtomicEffectType.EngineClash: return BranchEngineKind.Clash;
                case AtomicEffectType.EngineDeathToll: return BranchEngineKind.DeathToll;
                case AtomicEffectType.EngineManaSurplus: return BranchEngineKind.ManaSurplus;
                case AtomicEffectType.EngineUnity: return BranchEngineKind.Unity;
                case AtomicEffectType.EngineCurseOnDraw: return BranchEngineKind.CurseOnDraw;
                case AtomicEffectType.EngineBlessingOnDraw: return BranchEngineKind.BlessingOnDraw;
                default: return BranchEngineKind.None;
            }
        }

        /// <summary>表行是否引擎主干行（MountKinds 位 7）。</summary>
        public static bool IsEngineTrunkRow(Attribute.AtomicEffectConfig row)
            => HasMountBit(row, MountKind.EngineTrunk);

        /// <summary>组合内限重复行（2026-10-09 全局唯一定案 → 2026-10-19 放开修订）：仅引擎主干
        /// （位 7，倒计时等引擎状态按卡单台账共槽）与规则光环仪典（位 6，运行时同名一槽）维持
        /// 「同一效果一个组合中只出现一次（并列与奖励均计入）」；其余行放开可重复——逐原子目标制下
        /// 按实例独立解析/独立计价，重复=双倍价双倍效。合成期隐藏口径与构筑期兜底共用此单源。
        /// null 行（断链）不拦——断链另有断链告警。</summary>
        public static bool DuplicateRestricted(Attribute.AtomicEffectConfig row)
            => HasMountBit(row, MountKind.EngineTrunk) || HasMountBit(row, MountKind.RuleAura);

        /// <summary>引擎中文名（2026-10-09 代码侧单一来源——AtomText 短文案「自由分支·{名}{x}」消费；
        /// 原界面侧 EngineZh 已随引擎标识行退役删除）。</summary>
        public static string EngineZhOf(BranchEngineKind engine) => engine switch
        {
            BranchEngineKind.Countdown => "倒计时",
            BranchEngineKind.LuckRoll => "运势",
            BranchEngineKind.Clash => "拼点",
            BranchEngineKind.DeathToll => "死亡计数",
            BranchEngineKind.ManaSurplus => "元素充盈",
            BranchEngineKind.Unity => "团结",
            BranchEngineKind.CurseOnDraw => "附加诅咒",
            BranchEngineKind.BlessingOnDraw => "附加祝福",
            _ => engine.ToString(),
        };

        /// <summary>引擎参数 x 的钳制范围（UI 输入框与运行时判定共用）。
        /// 2026-10-09 自平衡统一：死亡计数/元素充盈并入拼点式派生门槛（IsDerivedThresholdEngine）
        /// ——参数区间随「预算=x」旧制一并退役（落入 default；合成器不再出参数行，运行时读推导门槛）。
        /// 团结（2026-10-20 效果序位换轴）为手填参数：x∈[1,6]=所需相邻单位数（六邻封顶 6）。</summary>
        public static void EngineParamRange(BranchEngineKind kind, out int min, out int max)
        {
            switch (kind)
            {
                case BranchEngineKind.LuckRoll: min = 1; max = 6; break;      // 概率门槛：双骰≥x（x=6 双 6 才中=1/36）
                case BranchEngineKind.Unity: min = 1; max = 6; break;         // 团结：所需相邻单位数 x（六邻天然封顶 6）
                case BranchEngineKind.CurseOnDraw: min = 1; max = 3; break;   // 附加诅咒：附加张数（每张各自完整载荷）
                case BranchEngineKind.BlessingOnDraw: min = 1; max = 3; break; // 附加祝福：附加张数（同上）
                default: min = 0; max = 99; break;                            // Countdown：0=按奖励推导费自动换算
            }
        }

        /// <summary>引擎奖励预算（Then 原子锚价合计上限）。2026-10-09 自平衡统一：全引擎 -1 无上限——
        /// 拼点/倒计时/运势本就无上限；死亡计数/元素充盈/手牌序位原「预算=x（EngineParam）」旧制退役
        /// （门槛与预算同源于 Then 锚价推导，见 CostDerivationService.RewardThreshold）；
        /// 可实现费用上限由 UI 侧 RewardFilterCap 统一封顶 5。</summary>
        public static int EngineRewardBudget(BranchEngineKind kind, int engineParam)
            => -1;

        /// <summary>派生门槛引擎（2026-10-09 自平衡统一单源）：拼点/死亡计数/元素充盈——
        /// 门槛 x=Then 奖励锚价实时推导（RewardThreshold），无手填参数、无独立预算。
        /// 消费：合成器参数行门控（EffectComposerScreen）、装载诊断跳参（CardLoader）、
        /// 短文案 x 口径（AtomText）与运行时判定（BranchEngines）。
        /// 团结（2026-10-20 效果序位换轴）不入此族：x∈[1,6] 手填（EngineParamRange），门槛=x。</summary>
        public static bool IsDerivedThresholdEngine(BranchEngineKind kind) => kind switch
        {
            BranchEngineKind.Clash => true,
            BranchEngineKind.DeathToll => true,
            BranchEngineKind.ManaSurplus => true,
            _ => false,
        };

        /// <summary>引擎效果头固定档（2026-10-09 自由分支九项定案）：引擎=单原子形态效果，发动方式/时机/
        /// 速度/生效次数由引擎内生触发事件决定——效果头四值按引擎钉死：合成器锁定不可改（ApplyEngineHeaderLock），
        /// 转换期（ConvertOne）对旧数据/手写数据兜底归一。引擎运行时本就无视效果头（BranchEngines 自订阅
        /// 事件）——此表保证显示/计价/数据三面与引擎真实语义一致。
        /// 2026-10-16 解绑定案：运势 FreeTiming=true——时机不再钉死（掷骰随声明时机判定），表内
        /// Timing/Activation 退化为**缺省值**，合成器解锁时机可改，转换期声明值优先、未声明回落此缺省。
        /// 2026-10-20 倒计时固定触发定案：倒计时退回钉死组（自动·回合开始——归零直接触发奖励，
        /// 主干/奖励不拆开，tactivate 不适用）。</summary>
        public readonly struct EngineHeaderPreset
        {
            public readonly EffectActivationType Activation;
            public readonly TriggerTiming Timing;
            public readonly int Speed;
            public readonly int Limit; // 生效次数：-1=无限
            /// <summary>自由时机引擎（2026-10-16 解绑）：true=时机可自由声明，
            /// 表内 Activation/Timing 为缺省而非钉死值；速度/生效次数仍钉。</summary>
            public readonly bool FreeTiming;

            public EngineHeaderPreset(EffectActivationType activation, TriggerTiming timing, int speed, int limit,
                bool freeTiming = false)
            { Activation = activation; Timing = timing; Speed = speed; Limit = limit; FreeTiming = freeTiming; }
        }

        /// <summary>各引擎固定档（null=非引擎）——FreeTiming 组（2026-10-16 解绑）仅剩**运势**=缺省自动·
        /// 准备阶段（掷骰随声明时机判定，骰子本就随时可扔；缺省时机 2026-10-20 随「回合开始收归系统」
        /// 由回合开始迁至准备阶段）。
        /// 钉死组照旧：倒计时=自动·**准备阶段**·无限（2026-10-20 固定触发定案：回合开始系统递减、
        /// 归零**直接触发奖励**——主干/奖励不拆开，tactivate 不适用；效果头时点=准备阶段，
        /// 递减本身是回合开始的系统行为）；
        /// 死亡计数=自动·其他生物死亡·无限（2026-10-19 手牌/场上改案：手牌或场上累计双方
        /// 生物死亡，满 6 不再计，达标触发不清零，使用后清零——时机/次数随口径更新）；拼点=自动·攻击宣言·无限（仅攻击方结算）；
        /// 元素充盈=自动·使用卡牌时·无限（2026-10-20 判据五：真实评估口=OnCardCastSettled 且**仅对手回合**
        /// 对手出牌后（剩余元素差恰==x），时机列仅作显示缺省；奖励恒直发）；团结=自动·使用卡牌时·无限
        /// （2026-10-20 效果序位换轴：团结评估口=OnCardCastSettled 出牌施放结算完成，与充盈同拍——
        /// 时机列仅作显示缺省）；
        /// 附加诅咒/祝福=主动·主动钉档·速度 0（部署点=施放结算，主动打出即生效）。</summary>
        public static EngineHeaderPreset? EngineHeaderPresetOf(BranchEngineKind kind) => kind switch
        {
            BranchEngineKind.Countdown => new EngineHeaderPreset(EffectActivationType.Automatic, TriggerTiming.OnStandbyPhase, 0, -1),
            BranchEngineKind.LuckRoll => new EngineHeaderPreset(EffectActivationType.Automatic, TriggerTiming.OnStandbyPhase, 0, -1, freeTiming: true),
            BranchEngineKind.DeathToll => new EngineHeaderPreset(EffectActivationType.Automatic, TriggerTiming.OnOtherCreatureDeath, 0, -1),
            BranchEngineKind.Clash => new EngineHeaderPreset(EffectActivationType.Automatic, TriggerTiming.OnAttack, 0, -1),
            BranchEngineKind.ManaSurplus => new EngineHeaderPreset(EffectActivationType.Automatic, TriggerTiming.OnCardPlayed, 0, -1),
            BranchEngineKind.Unity => new EngineHeaderPreset(EffectActivationType.Automatic, TriggerTiming.OnCardPlayed, 0, -1),
            BranchEngineKind.CurseOnDraw => new EngineHeaderPreset(EffectActivationType.Voluntary, TriggerTiming.Activate_Active, 0, 1),
            BranchEngineKind.BlessingOnDraw => new EngineHeaderPreset(EffectActivationType.Voluntary, TriggerTiming.Activate_Active, 0, 1),
            _ => (EngineHeaderPreset?)null,
        };

        // ======================================== 自由分支·产出条件目录 ========================================

        /// <summary>产出条件目录项。Budget（门附加费=Then 预算）：产出条件族=代码常量（各 2）；
        /// 局面门族从表派生（SituationGates 直通）——勿双写。</summary>
        public sealed class GateSpec
        {
            /// <summary>条件 id（评估走 BranchConditionEvaluator，如 "DmgKillsTarget"）。</summary>
            public string Id;
            /// <summary>中文名（UI 显示）。</summary>
            public string DisplayName;
            /// <summary>适用产出族标签（2026-10-06 迁表+中文化定案）：原子表行 Tags 列携带的中文族标签
            /// （如 "伤害产出族"）；null = 非产出条件。</summary>
            public string ProducerTag;
            /// <summary>「如果」句式从句（2026-10-05 文本表述定案）：产出条件摘要走自然句
            /// 「{主干}，如果{IfClause}，{奖励}」（如"消灭了目标"）——与 DisplayName（"消灭目标时"）
            /// 并存：DisplayName 用于 gate-row 下拉名，IfClause 用于摘要句子。null 时回退去尾「时」。</summary>
            public string IfClause;
            /// <summary>门预算（Then 原子锚价上限）：产出条件族=代码常量 2；局面门族=表 Budget 列直通。
            /// 2026-10-23 入表定案：目录项自带预算（与 BuildGatePremium 同源），GateLabel/GateBudget 读此。</summary>
            public int Budget;
        }

        /// <summary>产出族标签词表（2026-10-06 迁表+中文化：族成员名单由原子表 Tags 列维护，代码不再持有数组；
        /// 词表中文、可多标签逗号并列）：伤害族=造成/穿透/吸取；诅咒族=附加诅咒；
        /// 宣言族/预言族原子尚未入表（枚举先行）——行入表时带标签即生效。</summary>
        public const string DamageProducerTag = "伤害产出族";
        public const string DeclareProducerTag = "宣言产出族";
        public const string ProphecyProducerTag = "预言产出族";

        /// <summary>自由分支·产出条件全集（原 OutcomeGate 产出族——2026-10-05 两槽定案改归自由分支；
        /// 同日删除：TargetSurvived/Overkill/TargetStillWounded/Overheal）。
        /// 2026-10-23 入表定案的边界：产出条件族保持代码常量（预算 2 内联）——不迁表。</summary>
        public static readonly GateSpec[] OutcomeConditions =
        {
            new GateSpec { Id = "DmgKillsTarget", DisplayName = "消灭目标时", IfClause = "消灭了目标", ProducerTag = DamageProducerTag, Budget = 2 },
            new GateSpec { Id = "DeclareHit",     DisplayName = "宣言命中时", IfClause = "宣言命中",   ProducerTag = DeclareProducerTag, Budget = 2 },
            new GateSpec { Id = "DeclareMiss",    DisplayName = "宣言落空时", IfClause = "宣言落空",   ProducerTag = DeclareProducerTag, Budget = 2 },
            new GateSpec { Id = "ProphecyHit",    DisplayName = "预言命中时", IfClause = "预言命中",   ProducerTag = ProphecyProducerTag, Budget = 2 },
            new GateSpec { Id = "ProphecyMiss",   DisplayName = "预言落空时", IfClause = "预言落空",   ProducerTag = ProphecyProducerTag, Budget = 2 },
        };

        /// <summary>是否产出条件族 id（converter 折叠遗留门步骤时按此归类 Outcome）。</summary>
        public static bool IsOutcomeCondition(string conditionId)
            => !string.IsNullOrEmpty(conditionId)
               && OutcomeConditions.Any(g => g.Id == conditionId);

        /// <summary>某主干原子可挂的产出条件：原子表行 Tags 含条件的族标签才命中（迁表定案）；
        /// 无表行/无族标签 → 空=该原子不可挂产出条件。</summary>
        public static IEnumerable<GateSpec> OutcomeConditionsFor(AtomicEffectType trunk)
        {
            var tags = AtomicEffectTable.GetByType(trunk)?.GetTagList();
            if (tags == null || tags.Count == 0) return Enumerable.Empty<GateSpec>();
            return OutcomeConditions.Where(g => g.ProducerTag != null && tags.Contains(g.ProducerTag));
        }

        // ======================================== 有限分支（局面状态门）目录 ========================================

        /// <summary>有限分支条件全集（局面状态族）：不读主干产出、任意原子可挂，效果结算时评估一次——
        /// 达标→Then 奖励，不达标→不奖励也不惩罚（2026-10-09 定案，纯条件奖励）。
        /// 2026-10-23 入表定案：目录**每次从原子表派生**（表重读即生效，不做永久缓存——11 行开销可忽略）：
        /// 取 MountKinds 含「有限分支」位（MountKind.SituationGate）的行，顺序=表序（装载序号）；
        /// 行承载身份（GateId）/挂载（位 10）/预算（Budget 列），判定谓词仍由 BranchConditionEvaluator 代码注册。
        /// 诅咒门（CurseOnDraw）不在列——诅咒通道=引擎主干行 EngineCurseOnDraw（2026-10-08 接棒）。</summary>
        public static GateSpec[] SituationGates => Attribute.AtomicEffectTable.GetAll()
            .Where(r => r != null && HasMountBit(r, MountKind.SituationGate) && !string.IsNullOrEmpty(r.GateId))
            .OrderBy(r => r.Id)
            .Select(r => new GateSpec { Id = r.GateId, DisplayName = r.DisplayName, Budget = r.Budget, ProducerTag = null })
            .ToArray();

        /// <summary>是否局面状态门 id（converter 折叠遗留门步骤时按此归类 Gate）。</summary>
        public static bool IsSituationCondition(string conditionId)
            => SituationGates.Any(g => g.Id == conditionId);

        // ======================================== 构筑唯一键（每卡组限 1 张） ========================================

        /// <summary>唯一门（2026-10-11 命运一抽起）：显示名带「【唯一】」后缀的局面门——使用该门的卡
        /// 每个卡组限 1 张（不同卡 id 也互斥）。单一事实源=SituationGates 显示名后缀，
        /// 后续门加后缀即生效（构筑 UI 拦截/随机组卡过滤自动跟进）。</summary>
        public static bool IsUniqueGate(string gateId)
            => !string.IsNullOrEmpty(gateId)
               && SituationGates.Any(g => g.Id == gateId
                   && g.DisplayName != null && g.DisplayName.EndsWith("【唯一】", StringComparison.Ordinal));

        /// <summary>唯一门显示名（未知名回退 id 本身——提示文案用）。</summary>
        public static string UniqueGateDisplayName(string gateId)
            => SituationGates.FirstOrDefault(g => g.Id == gateId)?.DisplayName ?? gateId;

        /// <summary>构筑唯一槽（2026-10-11 构筑规则②）：卡的「每卡组限 1 张」身份。</summary>
        public sealed class DeckUniqueSlot
        {
            /// <summary>身份键（族前缀+id：「gate:{局面门 id}」/「aura:{仪典短名}」）——同键不同卡互斥。</summary>
            public string Key;
            /// <summary>提示用显示名（如 命运一抽【唯一】/ 苦痛仪典）。</summary>
            public string Display;
        }

        /// <summary>卡携带的构筑唯一槽全集（消费方：DeckBuilderScreen 添加/保存拦截、
        /// BattleController.RandomDeckFrom 随机组卡过滤）。两族：
        /// ① 唯一门——槽级 branch 载荷（settle=Gate）gateId 命中 IsUniqueGate；
        /// ② 规则光环仪典——ModifyGameRule 原子步（表行 MountKinds 含 RuleAura 位）str=规则短名，
        /// 同名仪典每卡组限 1 张（镜像运行时「异名共存、同名禁止」，不同仪典可共存）。
        /// 扫描全部效果步骤（含抉择嵌套与旧版 then/else 扁平列）与扁平 AtomicEffects。</summary>
        public static List<DeckUniqueSlot> DeckUniqueSlots(CardData card)
        {
            var slots = new List<DeckUniqueSlot>();
            if (card?.Effects == null) return slots;
            foreach (var fx in card.Effects)
            {
                if (fx == null) continue;
                CollectDeckUniqueSlots(fx.Steps, slots);
                CollectDeckUniqueSlots(fx.AtomicEffects, slots);
            }
            return slots;
        }

        private static void CollectDeckUniqueSlots(List<EffectStepData> steps, List<DeckUniqueSlot> slots)
        {
            if (steps == null) return;
            foreach (var step in steps)
            {
                if (step == null) continue;
                CollectDeckUniqueSlot(step.atomic, slots);
                CollectDeckUniqueSlots(step.thenSteps, slots);
                CollectDeckUniqueSlots(step.elseSteps, slots);
                if (step.choices == null) continue;
                foreach (var choice in step.choices)
                    CollectDeckUniqueSlots(choice?.steps, slots);
            }
        }

        private static void CollectDeckUniqueSlots(List<AtomicEffectEntry> atoms, List<DeckUniqueSlot> slots)
        {
            if (atoms == null) return;
            foreach (var atom in atoms)
                CollectDeckUniqueSlot(atom, slots);
        }

        private static void CollectDeckUniqueSlot(AtomicEffectEntry atom, List<DeckUniqueSlot> slots)
        {
            if (atom == null) return;
            var b = atom.branch;
            if (b != null && (BranchSettleKind)b.settle == BranchSettleKind.Gate && IsUniqueGate(b.gateId))
                AddDeckUniqueSlot(slots, "gate:" + b.gateId, UniqueGateDisplayName(b.gateId));
            var row = AtomicEffectTable.GetByHashId(atom.refId);
            if (!string.IsNullOrEmpty(atom.str) && HasMountBit(row, MountKind.RuleAura))
                AddDeckUniqueSlot(slots, "aura:" + atom.str, row?.DisplayName ?? atom.str);
        }

        private static void AddDeckUniqueSlot(List<DeckUniqueSlot> slots, string key, string display)
        {
            foreach (var s in slots)
                if (s.Key == key) return; // 同卡同键去重
            slots.Add(new DeckUniqueSlot { Key = key, Display = display });
        }

        // ======================================== 可挂范围判定（MountKinds=唯一权威，合成器/装载共用） ========================================

        /// <summary>表行挂载位判定小工具（全判定统一走 ParseCsv——空串=空集=未声明）。</summary>
        public static bool HasMountBit(Attribute.AtomicEffectConfig row, MountKind bit)
            => MountKindExtensions.ParseCsv(row?.MountKinds ?? "").Contains(bit);

        /// <summary>表行 TargetFilter 是否含指定 token。</summary>
        public static bool HasFilterToken(Attribute.AtomicEffectConfig row, string token)
        {
            var filter = row?.TargetFilter ?? "";
            if (string.IsNullOrEmpty(filter)) return false;
            foreach (var t in filter.Split(','))
                if (t.Trim() == token) return true;
            return false;
        }

        /// <summary>生物侧可赋予行：表行 TargetFilter 含 "NoRole"（仅生物）——
        /// 消费方：关键词面板资格（KeywordCatalog，另叠加关键词位门槛）。
        /// （2026-10-05 自 MountKinds 位5 迁移；2026-10-07 回响改普通效果后 Spell token 退役，
        /// 法术侧可赋予行判定 IsSpellGrantRow 随之删除。）</summary>
        public static bool IsCreatureGrantRow(Attribute.AtomicEffectConfig row)
            => HasFilterToken(row, "NoRole");

        /// <summary>行级「可否作连接光环条目」统一判定（2026-10-07 深夜终版：光环库/装载拦截/保存校验共用）：
        /// 位 8 不可作为连接光环（消耗型黑名单）一票否决 → 位 1 指示物硬拒（永不可无例外——防误标；
        /// 坚韧 2026-10-08 指示物化即经此位退出光环族）→ 位 0 关键词默认可 → 其余看位 3 可以作为连接光环
        ///（一般效果行光环化源——属性增加/减少）。位 7 仅可已删（IsAuraOnlyRow 随之退役）。</summary>
        public static bool CanMountAsAura(Attribute.AtomicEffectConfig row)
        {
            if (row == null) return false;
            if (HasMountBit(row, MountKind.NoLinkAura)) return false;
            if (HasMountBit(row, MountKind.Counter)) return false;
            if (HasMountBit(row, MountKind.Keyword)) return true;
            return HasMountBit(row, MountKind.LinkAura);
        }

        /// <summary>主干槽可落资格（2026-10-07 位 0「入效果栏」删除后派生，合成器 LibPayload 与装载校验共用）：
        /// 非规则光环即可（位 7 仅可已删）。引擎行经 EngineTrunk
        /// 位进主干槽（填槽即自由分支）；系统/攻守行有资格但被合成器库过滤隐藏；关键词/指示物行以授予形态落槽。
        /// 2026-10-23 有限分支入表：门槛行（位 10）不得作主干——表行只承载门槛身份/挂载/预算，
        /// 防门槛行漏进原子库与卡组合。</summary>
        public static bool CanBeTrunkRow(Attribute.AtomicEffectConfig row)
            => row != null && !HasMountBit(row, MountKind.RuleAura) && !HasMountBit(row, MountKind.SituationGate);

        /// <summary>槽内 Then 奖励资格（2026-10-07 位 3「分支奖励」删除后派生）：
        /// 派生主干资格 ∩ 非引擎行（引擎行=条件载体零域，不作奖励）。
        /// 预算/可逆转/非错边过滤在调用点照旧。
        /// 2026-10-23 有限分支入表：门槛行随主干排除一并出局（位 10）。
        /// （2026-10-13 晚「主动发动」位已删：动态计价原子（召唤衍生物/临摹/变形赋予）触发式预付
        /// 与主动现付同额同源（ResolveTemplate→ContentPriceOf），奖励入价在构筑期——主动/自动自由选择。）</summary>
        public static bool CanBeRewardRow(Attribute.AtomicEffectConfig row)
            => CanBeTrunkRow(row) && !HasMountBit(row, MountKind.EngineTrunk);

        /// <summary>关键词型 Grant 判定（2026-10-02 定案）：Grant 行且表行目标域恰为 {Self}（纯自指）。
        /// 关键词型原子 = 关键词的挂载形态（卡面印刷/登场/触发式如"自我沉睡"），**不得作为主动
        /// 效果的原子**——横置代价换静态身份无意义；域含其他目标 = 赋予型 Grant（"给目标加词"），
        /// 可作主动。行级判定即完备：实例 kinds 是收窄（交集），自指域收不出他人域、赋予域收不出自指域。
        /// 消费方：装载校验（CardEffectConverter）与效果合成器 UI——同源防两套口径。</summary>
        public static bool IsKeywordStyleGrant(AtomicEffectType t)
        {
            var name = t.ToString();
            if (!name.StartsWith("Grant")) return false;
            var row = Attribute.AtomicEffectTable.GetByEnumName(name);
            if (row == null) return false;
            var kinds = row.GetTargetKindList();
            return kinds != null && kinds.Count == 1 && kinds[0] == (int)TargetKind.Self;
        }

        /// <summary>关键词 id 是否可作连接光环条目（2026-09-23 定案·数据驱动；2026-10-07 晚默认翻转）：
        /// 经关键词定义 → Grant 原子表行 → CanMountAsAura——关键词**默认可**，
        /// 黑名单表标位 8（现值：守护/再生/禁魔石——与光环 live-query 持续语义冲突）——名单在表不在代码。
        /// 指示物化关键词（圣盾/潜行/复生 2026-10-08、法术护盾 10-09）已退出关键词目录，恒 false。</summary>
        public static bool IsAuraMountableKeyword(string keywordId)
        {
            if (string.IsNullOrEmpty(keywordId)) return false;
            var def = CardLoader.LoadKeywords().TryGetValue(keywordId, out var d) ? d : null;
            if (def == null || string.IsNullOrEmpty(def.atomicEffect)) return false;
            return CanMountAsAura(Attribute.AtomicEffectTable.GetByEnumName(def.atomicEffect));
        }

        /// <summary>光环关键词下拉数据源（UI 用）：全部可作光环条目的 Grant 行（CanMountAsAura——
        /// 关键词默认可−消耗型拉黑）。desc=条目效果说明（2026-10-05：光环库行不再用
        /// 统一"live-query"术语文案——逐条给真实效果，{target} 模板代词按光环语义换写为「连接的单位」）。</summary>
        public static List<(string id, string label, string desc)> AuraKeywordChoices()
        {
            var list = new List<(string, string, string)>();
            foreach (var kv in CardLoader.LoadKeywords())
            {
                var def = kv.Value;
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                var row = Attribute.AtomicEffectTable.GetByEnumName(def.atomicEffect);
                if (!CanMountAsAura(row)) continue;
                string desc = (def.description ?? "")
                    .Replace("{target}", "连接的单位").Replace("{value}", "×N");
                list.Add((def.id, string.IsNullOrEmpty(def.nameZh) ? def.id : def.nameZh, desc));
            }
            return list;
        }

        /// <summary>条件的显示文本（含【奖励x】——x=目录项 Budget：产出条件族=代码常量，
        /// 局面门族=表 Budget 列；2026-10-23 入表起与 BuildGatePremium 同源（都从表派生））。</summary>
        public static string GateLabel(GateSpec gate)
            => $"{gate.DisplayName}【奖励{gate.Budget}】";

        /// <summary>门预算（Then 原子推导费上限）：目录项 Budget 直通（灰）——
        /// 局面门族=表 Budget 列（SituationGates 同源），产出条件族=代码常量 2。</summary>
        public static int GateBudget(GateSpec gate)
            => gate.Budget;
    }
}
