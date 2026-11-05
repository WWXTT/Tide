using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 效果适用卡型单源（2026-10-29 P9 定案；2026-10-30 无次数上限定案修订；2026-11-05 时机两档
    /// 收敛再修订）：输入效果定义（激活方式 + 触发时机 + 原子构成），输出可挂载的卡型集合与逐卡型
    /// 拒绝原因。判定表与已定案承载规则同源：
    /// · 关键词（表行 Keyword 挂位 ∪ Grant* 域恰 {Self}）：生物✓ 结界✓ 法术✗；
    /// · 一次性触发（FiresOncePerPaidLifetime 单源——不另造表）：生物✓ 结界✓；
    ///   法术仅「登场」通过（法术=施放即结算的一次性形态），死亡/离场等驻场时点无从触发；
    /// · 周期/观察族多次触发（回合/阶段/抽牌/伤害/使用/事件观察——IsEnchantExclusiveTiming）：
    ///   生物✗（**2026-11-05 收回 2026-10-30 的放开**——多次档系数已收敛 ×1，周期价值须由
    ///   耐久支付，生物挂载=无对价白嫖）结界✓（耐久即次数池）法术✗（法术不驻场）；
    ///   引擎载荷豁免（效果头时机=奖励发射窗，非重复语义——与计价 TimingCostFactor 同口径）；
    /// · 绑横置触发（攻击/守卫/横置族 IsTapAnchoredTiming）：虽归多次档**生物仍可挂**
    ///   （2026-11-05 定案：宿主自身行动承担次数，横置锚天然限频）结界✗（不横置）法术✗；
    /// · 主动效果（Activate_*）：生物✓（横置发动）结界仅充能✓（P12：充能=手牌主动——其余禁，
    ///   2026-10-15 改版）法术✓（抉择槽 1~3 即主动形态——与宿主钉值/主动槽同口径）；
    /// · 连接光环：生物✓（属性±类）结界✓ 法术✗；规则光环：生物✗ 结界✓ 法术✗。
    /// 消费方：效果合成器顶栏「适用」实时推导、卡组合候选过滤（不静默隐藏——计数行+原因+跳转）、
    /// 装载期 ValidateCreatureShape（CardLoader 兜底，防直改 JSON/旧档）。
    /// 注意：本类只判「能不能挂」，不管槽位容量与保存闸——既有形状闸（FillSelectedSlot）与
    /// 宿主钉值（SyncActivationVisibility）仍在调用侧前置。
    ///
    /// 不做但记档：
    /// · 「登场」不是严格一次（弹回再入场会再触发），时机系数 1 是近似计价——不加机制，只在注释写清；
    /// · 耐久档重排：反复触发的结界一回合可能烧多点耐久，现有默认耐久偏厚——本次不改数值，
    ///   内容侧建议高频时机（抽牌/伤害）耐久配 2～3。
    /// </summary>
    public static class EffectEligibility
    {
        /// <summary>三卡型全表（显示序=合成器顶栏）。</summary>
        public static readonly IReadOnlyList<Cardtype> AllHosts =
            new[] { Cardtype.Creature, Cardtype.Enchantment, Cardtype.Spell };

        /// <summary>可挂载卡型集合（P9 契约口；auraShape=连接光环形态标记——EffectDefinition
        /// 不携带箭头/条目，由调用侧按图形态传入）。</summary>
        public static IReadOnlyList<Cardtype> AllowedHosts(EffectDefinition def, bool auraShape = false)
            => AllHosts.Where(h => RejectReason(h, def, auraShape) == null).ToList();

        /// <summary>逐卡型拒绝原因（null=可挂载）。多特征叠加时任一命中即拒。</summary>
        public static string RejectReason(Cardtype host, EffectDefinition def, bool auraShape = false)
        {
            if (def == null) return null;
            if (host == Cardtype.Spell)
            {
                if (HasKeywordAtom(def)) return "法术无关键词载体（关键词=生物/结界印刷位）";
                if (auraShape) return "光环（连接/场地）专属结界载体（法术只挂主动效果）";
                if (HasRuleAuraAtom(def)) return "场地光环专属结界载体（全局唯一、驻场生效）";
                if (def.IsTriggeredEffect)
                {
                    if (CostDerivationService.FiresOncePerPaidLifetime(def.TriggerTiming))
                        return def.TriggerTiming == TriggerTiming.OnPlay
                            ? null // 登场=施放即结算的一次性——法术的合法形态
                            : "该时点在法术上无从触发（法术不驻场，死亡/离场等永不发生）";
                    return "法术一次性施放，挂不住反复触发";
                }
                return null; // 主动=抉择槽形态
            }
            if (host == Cardtype.Enchantment)
            {
                //（P12 充能豁免已随 2026-11-05 充能系统动作化改版收回：充能不再是卡上效果——
                //  结界照旧禁一切主动，只挂自动/光环。）
                if (def.IsActivatedEffect)
                    return "结界禁主动效果（2026-10-15 改版——只挂自动/光环）";
                if (def.IsTriggeredEffect && !CostDerivationService.FiresOncePerPaidLifetime(def.TriggerTiming)
                    && IsTapAnchoredTiming(def.TriggerTiming))
                    return "结界不横置——绑横置触发（攻击/守卫）无从锚定";
                return null; // 一次性/周期反复触发=耐久即次数池（2026-10-10 发数经济；2026-11-05 多次档×1）
            }
            // 生物（及其他无耐久宿主）：2026-10-30 曾放开反复触发（平衡由期望次数系数承担）——
            // 2026-11-05 收回：两档系数收敛 ×1 后周期价值必须由耐久支付，仅结界可挂；
            // 攻/守/横置族豁免（多次档但宿主自身行动承担次数）；引擎载荷豁免（发射窗非重复语义）。
            if (HasRuleAuraAtom(def)) return "场地光环专属结界（全局唯一、驻场生效）";
            if (def.IsTriggeredEffect && IsEnchantExclusiveTiming(def.TriggerTiming) && !HasEnginePayload(def))
                return "周期性多次时机（回合/阶段/抽牌/伤害/事件观察族）为结界专属（耐久即次数池）"
                       + "——生物只能挂单次时机或攻/守/横置族";
            return null;
        }

        /// <summary>绑横置触发族（判定表「攻击/守卫」行 + 横置/重置——横置状态即天然限频锚）：
        /// 归多次档但生物可挂（2026-11-05 定案），结界不横置拒挂。</summary>
        public static bool IsTapAnchoredTiming(TriggerTiming t)
            => t == TriggerTiming.OnAttack || t == TriggerTiming.OnAttacked
               || t == TriggerTiming.OnBlockDeclare || t == TriggerTiming.OnTap || t == TriggerTiming.OnUntap;

        /// <summary>周期/观察族多次时机（多次档减攻/守/横置族——回合/阶段/抽牌/伤害/被指向/
        /// 使用/施放/占卜/事件观察/OnSummon 等）：**结界专属**（耐久即次数池——额外触发次数已由
        /// 耐久支付，生物挂载=无对价白嫖）。2026-11-05 收回 2026-10-30 的放开。
        /// 消费方：RejectReason 生物臂 / 合成器时机下拉宿主过滤 / 装载 ValidateCreatureShape / 验证器审计。</summary>
        public static bool IsEnchantExclusiveTiming(TriggerTiming t)
            => CostDerivationService.IsRepeatTiming(t) && !IsTapAnchoredTiming(t);

        /// <summary>是否携带引擎载荷（settle=Engine——扁平 Effects 与节点化 Steps 双扫）：
        /// 效果头时机=奖励发射窗，不代表主干发射次数（与计价 TimingCostFactor 豁免同口径）——
        /// 宿主时机闸对引擎豁免（倒计时/运势/元素充盈等引擎自持重复经济）。Editor 可见的公开转发口。</summary>
        public static bool HasEnginePayload(EffectDefinition def)
            => CostDerivationService.HasEnginePayload(def);

        /// <summary>是否携带关键词型原子（表行 Keyword 挂位 ∪ Grant* 域恰 {Self}——与
        /// KeywordCatalog/装载校验同口径；Effects 主序列与 Steps 节点双扫）。</summary>
        public static bool HasKeywordAtom(EffectDefinition def)
        {
            foreach (var a in AtomsOf(def))
            {
                var row = CardCore.Attribute.AtomicEffectTable.GetByType(a.Type);
                if (ComposerCatalog.HasMountBit(row, MountKind.Keyword)) return true;
                if (ComposerCatalog.IsKeywordStyleGrant(a.Type)) return true;
            }
            return false;
        }

        /// <summary>是否携带规则光环投放原子（表行 RuleAura 挂位）。</summary>
        public static bool HasRuleAuraAtom(EffectDefinition def)
        {
            foreach (var a in AtomsOf(def))
                if (ComposerCatalog.HasMountBit(
                        CardCore.Attribute.AtomicEffectTable.GetByType(a.Type), MountKind.RuleAura))
                    return true;
            return false;
        }

        private static IEnumerable<AtomicEffectInstance> AtomsOf(EffectDefinition def)
        {
            if (def == null) yield break;
            if (def.Steps != null)
                foreach (var s in def.Steps)
                    if (s?.Atomic != null) yield return s.Atomic;
            if (def.Effects != null)
                foreach (var a in def.Effects)
                    if (a != null) yield return a;
        }
    }
}
