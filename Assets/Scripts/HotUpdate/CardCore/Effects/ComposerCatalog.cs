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
                case AtomicEffectType.EngineNthHandCard: return BranchEngineKind.NthHandCard;
                case AtomicEffectType.EngineCurseOnDraw: return BranchEngineKind.CurseOnDraw;
                case AtomicEffectType.EngineBlessingOnDraw: return BranchEngineKind.BlessingOnDraw;
                default: return BranchEngineKind.None;
            }
        }

        /// <summary>表行是否引擎主干行（MountKinds 位 7）。</summary>
        public static bool IsEngineTrunkRow(Attribute.AtomicEffectConfig row)
            => HasMountBit(row, MountKind.EngineTrunk);

        /// <summary>引擎中文名（2026-10-09 代码侧单一来源——AtomText 短文案「自由分支·{名}{x}」消费；
        /// 原界面侧 EngineZh 已随引擎标识行退役删除）。</summary>
        public static string EngineZhOf(BranchEngineKind engine) => engine switch
        {
            BranchEngineKind.Countdown => "倒计时",
            BranchEngineKind.LuckRoll => "运势",
            BranchEngineKind.Clash => "拼点",
            BranchEngineKind.DeathToll => "死亡计数",
            BranchEngineKind.ManaSurplus => "元素充盈",
            BranchEngineKind.NthHandCard => "手牌序位",
            BranchEngineKind.CurseOnDraw => "附加诅咒",
            BranchEngineKind.BlessingOnDraw => "附加祝福",
            _ => engine.ToString(),
        };

        /// <summary>引擎参数 x 的钳制范围（UI 输入框与运行时判定共用）。</summary>
        public static void EngineParamRange(BranchEngineKind kind, out int min, out int max)
        {
            switch (kind)
            {
                case BranchEngineKind.LuckRoll: min = 1; max = 5; break;      // 概率门槛（双 6 才中=1/36）
                case BranchEngineKind.DeathToll: min = 1; max = 9; break;     // 双方合计死亡阈值
                case BranchEngineKind.ManaSurplus: min = 1; max = 9; break;   // bank 最多色阈值
                case BranchEngineKind.NthHandCard: min = 1; max = 9; break;   // 本回合手牌使用序位
                case BranchEngineKind.CurseOnDraw: min = 1; max = 3; break;   // 附加诅咒：附加张数（每张各自完整载荷）
                case BranchEngineKind.BlessingOnDraw: min = 1; max = 3; break; // 附加祝福：附加张数（同上）
                default: min = 0; max = 99; break;                            // Countdown：0=按奖励推导费自动换算
            }
        }

        /// <summary>引擎奖励预算（Then 原子锚价合计上限；2026-09-22 定案=奖励预算制）：死亡计数/元素充盈/手牌序位
        /// 预算=x（EngineParam）；既有三引擎维持无上限自平衡（拼点门槛=锚价、倒计时回合=锚价、运势概率制）；
        /// 附加诅咒/附加祝福（2026-10-08）同无上限——延迟与抽到的不确定性即代价 → -1。</summary>
        public static int EngineRewardBudget(BranchEngineKind kind, int engineParam)
        {
            switch (kind)
            {
                case BranchEngineKind.DeathToll:
                case BranchEngineKind.ManaSurplus:
                case BranchEngineKind.NthHandCard:
                    return Math.Max(1, engineParam);
                default:
                    return -1;
            }
        }

        // ======================================== 自由分支·产出条件目录 ========================================

        /// <summary>产出条件目录项。Premium（门附加费=Then 预算）从 GatePremium 读——勿双写。</summary>
        public sealed class GateSpec
        {
            /// <summary>条件 id（评估走 BranchConditionEvaluator，如 "DmgKillsTarget"）。</summary>
            public string Id;
            /// <summary>中文名（UI 显示）。</summary>
            public string DisplayName;
            /// <summary>适用产出族标签（2026-10-06 迁表+中文化定案）：原子表行 Tags 列携带的中文族标签
            /// （如 "伤害产出族"）；null = 局面门（与产出无关，任意主干可挂）。</summary>
            public string ProducerTag;
            /// <summary>「如果」句式从句（2026-10-05 文本表述定案）：产出条件摘要走自然句
            /// 「{主干}，如果{IfClause}，{奖励}」（如"消灭了目标"）——与 DisplayName（"消灭目标时"）
            /// 并存：DisplayName 用于 gate-row 下拉名，IfClause 用于摘要句子。null 时回退去尾「时」。</summary>
            public string IfClause;
        }

        /// <summary>产出族标签词表（2026-10-06 迁表+中文化：族成员名单由原子表 Tags 列维护，代码不再持有数组；
        /// 词表中文、可多标签逗号并列）：伤害族=造成/穿透/吸取；诅咒族=附加诅咒；
        /// 宣言族/预言族原子尚未入表（枚举先行）——行入表时带标签即生效。</summary>
        public const string DamageProducerTag = "伤害产出族";
        public const string DeclareProducerTag = "宣言产出族";
        public const string ProphecyProducerTag = "预言产出族";

        /// <summary>自由分支·产出条件全集（原 OutcomeGate 产出族——2026-10-05 两槽定案改归自由分支；
        /// 同日删除：TargetSurvived/Overkill/TargetStillWounded/Overheal）。</summary>
        public static readonly GateSpec[] OutcomeConditions =
        {
            new GateSpec { Id = "DmgKillsTarget", DisplayName = "消灭目标时", IfClause = "消灭了目标", ProducerTag = DamageProducerTag },
            new GateSpec { Id = "DeclareHit",     DisplayName = "宣言命中时", IfClause = "宣言命中",   ProducerTag = DeclareProducerTag },
            new GateSpec { Id = "DeclareMiss",    DisplayName = "宣言落空时", IfClause = "宣言落空",   ProducerTag = DeclareProducerTag },
            new GateSpec { Id = "ProphecyHit",    DisplayName = "预言命中时", IfClause = "预言命中",   ProducerTag = ProphecyProducerTag },
            new GateSpec { Id = "ProphecyMiss",   DisplayName = "预言落空时", IfClause = "预言落空",   ProducerTag = ProphecyProducerTag },
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

        /// <summary>有限分支条件全集（局面状态族）：不读主干产出、任意原子可挂，效果结算时评估一次。</summary>
        public static readonly GateSpec[] SituationGates =
        {
            // ---- 局面状态族·一批（通用门，预算 1；DrawnInStandbyThisTurn 2026-10-09 调 3）----
            new GateSpec { Id = "DrawnInStandbyThisTurn", DisplayName = "本回合第一张抽到的卡" },
            new GateSpec { Id = "LifeBelowOpp",    DisplayName = "生命值低于对手" },
            new GateSpec { Id = "LifeAboveOpp",    DisplayName = "生命值高于对手" },
            new GateSpec { Id = "DeckBelowOpp",    DisplayName = "卡组剩余低于对手" },
            new GateSpec { Id = "DeckAboveOpp",    DisplayName = "卡组剩余高于对手" },
            new GateSpec { Id = "CreaturesBelowOpp", DisplayName = "场上生物低于对手" },
            new GateSpec { Id = "CreaturesAboveOpp", DisplayName = "场上生物高于对手" },
            new GateSpec { Id = "FirstCardThisTurn", DisplayName = "本回合使用的第一张卡" },

            // ---- 局面状态族·二批（通用门，预算 2）----
            new GateSpec { Id = "LandsGe7",   DisplayName = "操控地数量≥7" },
            new GateSpec { Id = "HandEmpty",  DisplayName = "手牌数量为0" },
            new GateSpec { Id = "LifeLe7",    DisplayName = "生命值≤7" },
        };

        /// <summary>是否局面状态门 id。</summary>
        public static bool IsSituationCondition(string conditionId)
            => SituationGates.Any(g => g.Id == conditionId);

        // ======================================== 诅咒门（Gate 特例） ========================================

        /// <summary>诅咒门（legacy，2026-10-05 有限分支定案；2026-10-08 表行退役）：主干=附加诅咒（AddCurse，
        /// 表行已删——自由分支·引擎主干行 EngineCurseOnDraw 接棒）唯一门——时机固定"抽到该卡时"
        /// （CurseSystem 驱动，施放时恒假不结算），Then 原子=该诅咒专属载荷（预算 2，converter 折入主干原子
        /// Branch 载荷）。保留供手写数据兼容，合成器已不可达（AddCurse 行不在原子库）。</summary>
        public const string CurseGateId = "CurseOnDraw";

        /// <summary>诅咒门族标签（legacy——AddCurse 行已随 2026-10-08 引擎主干化退役，无携带行）。</summary>
        public const string CurseProducerTag = "诅咒产出族";

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
        /// 位进主干槽（填槽即自由分支）；系统/攻守行有资格但被合成器库过滤隐藏；关键词/指示物行以授予形态落槽。</summary>
        public static bool CanBeTrunkRow(Attribute.AtomicEffectConfig row)
            => row != null && !HasMountBit(row, MountKind.RuleAura);

        /// <summary>槽内 Then 奖励资格（2026-10-07 位 3「分支奖励」删除后派生）：
        /// 派生主干资格 ∩ 非引擎行（引擎行=条件载体零域，不作奖励）。
        /// 预算/可逆转/非错边过滤在调用点照旧。</summary>
        public static bool CanBeRewardRow(Attribute.AtomicEffectConfig row)
            => CanBeTrunkRow(row) && !HasMountBit(row, MountKind.EngineTrunk);

        /// <summary>关键词型 Grant 判定（2026-10-02 定案）：Grant 行且表行目标域恰为 {Self}（纯自指）。
        /// 关键词型原子 = 关键词的挂载形态（卡面印刷/登场/触发式如"自我沉睡"），**不得作为启动式
        /// 效果的原子**——横置代价换静态身份无意义；域含其他目标 = 赋予型 Grant（"给目标加词"），
        /// 可作启动式。行级判定即完备：实例 kinds 是收窄（交集），自指域收不出他人域、赋予域收不出自指域。
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

        /// <summary>条件的显示文本（含【奖励x】——premium 取 GatePremium，与计价同源）。</summary>
        public static string GateLabel(GateSpec gate)
        {
            int premium = CostDerivationService.GatePremium.TryGetValue(gate.Id, out var p) ? p : 0;
            return $"{gate.DisplayName}【奖励{premium}】";
        }

        /// <summary>门预算（Then 原子推导费上限）：GatePremium 值（灰）。</summary>
        public static int GateBudget(GateSpec gate)
            => CostDerivationService.GatePremium.TryGetValue(gate.Id, out var p) ? p : 0;
    }
}
