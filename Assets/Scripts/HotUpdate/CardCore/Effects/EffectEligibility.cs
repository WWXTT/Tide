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
    /// · 连接光环：生物✓（属性±类）结界✓ 法术✗；规则光环：生物✗ 结界✓ 法术✗；
    /// · 光环效果引用（effectRef，2026-11-05 效果光环）：授予资格单源=非主动∧多次档∧生物臂
    ///   ∧非可选∧无代价∧非引擎主干（AuraGrantRejectReason——组合器筛选/装载闸/授予缓存共用）。
    /// · 附加诅咒/祝福载荷（2026-11-06 载荷效果化）：载荷资格单源=主动∧极性匹配引擎行
    ///   （祝福=+1/诅咒=−1，全原子同极性）∧非引擎主干（CursePayloadRejectReason——
    ///   组合器载荷效果选择器/装载闸/装载期 ValidateAtomParams 共用）。
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

        /// <summary>
        /// 可否被连接光环 effectRef 条目引用（2026-11-05 效果光环定案·资格谓词单源——
        /// 组合器光环效果库筛选 / 装载校验闸 / 触发引擎授予缓存三处共用）：
        /// 非主动 ∧ 多次档时机 ∧ 生物宿主臂放行（受益者=生物——攻/守/横置族豁免口径同源）∧
        /// 非可选触发（Voluntary 需玩家选择窗口；关键词类比=授予即自动）∧ 无代价行（授予=免费
        /// 自动效果，2026-11-05 拍板）∧ 非引擎主干（引擎效果不入触发管线，合成条目会成孤儿栈项）。
        /// 返回 null=可引用，否则拒绝原因（UI 筛选提示/装载告警共用文案）。
        /// </summary>
        public static string AuraGrantRejectReason(CardEffectData data)
        {
            if (data == null) return "空效果";
            var timing = (TriggerTiming)data.TriggerTiming;
            if (timing < 0 || timing > TriggerTiming.OnCardCastSettled) return "时机值非法";
            var def = CardEffectConverter.ConvertOne(data, data.Id ?? "");
            if (def.IsActivatedEffect) return "主动效果不可被光环授予（只授予触发式）";
            if (!CostDerivationService.IsRepeatTiming(timing)) return "单次时机不可被光环授予（只授予多次触发时机）";
            var creatureArm = RejectReason(Cardtype.Creature, def);
            if (creatureArm != null) return creatureArm; // 周期/观察族=结界专属、场地光环原子等
            if (def.ActivationType == EffectActivationType.Voluntary) return "可选触发不可被光环授予（授予=自动效果）";
            if (data.Costs != null && data.Costs.Count > 0) return "带代价行的效果不可被光环授予（免费自动效果）";
            if (CardEffectConverter.EngineKindOfDefinition(def) != BranchEngineKind.None)
                return "引擎主干不入触发管线";
            // 连接光环只授予极性 +1 效果（2026-11-07 定案）：箭头只能指向己方战场（中间信息条隔断，
            // 指不到对手战场）——减益/中性效果挂上去只会打自己人，直接拒绝。
            bool sawAtom = false;
            foreach (var a in AtomsDeepOf(def))
            {
                if (a == null) continue;
                sawAtom = true;
                if (a.Polarity < 0.5f)
                    return "连接光环只授予极性 +1 效果（箭头只能指向己方战场，减益/中性不可）";
            }
            if (!sawAtom) return "空效果（无原子）";
            return null;
        }

        /// <summary>
        /// 可否作附加诅咒/附加祝福的载荷效果（2026-11-06 载荷效果化定案·资格谓词单源——
        /// 组合器载荷效果选择器 / 装载校验闸 / 装载期 ValidateAtomParams 三处共用）：
        /// **主动效果**（Activate_*——抽到时按声明结算，不依赖场上触发语境）∧ **极性匹配引擎行**
        ///（附加祝福=给自己 → 极性 +1 效果；附加诅咒=给对手 → 极性 −1 效果——全部原子须同极性，
        /// 中性/混合极性效果不可作载荷）∧ 非引擎主干（引擎主干不经载荷管线，挂上=静默空转）。
        /// 返回 null=可作载荷，否则拒绝原因（UI 筛选提示/装载告警共用文案）。
        /// 输入契约：**完整卡内效果**（Steps/AtomicEffects 任一形态携带原子）——效果库条目须先经
        /// `EffectsLibrary.Resolve`（还原 Steps）再入；`EffectGraphData.header` 只带头部字段不含原子，
        /// 直接喂入会极性判定恒空（合成器选择器曾踩）。
        /// </summary>
        public static string CursePayloadRejectReason(CardEffectData data, float enginePolarity)
        {
            if (data == null) return "空效果";
            var timing = (TriggerTiming)data.TriggerTiming;
            if (timing < 0 || timing > TriggerTiming.OnCardCastSettled) return "时机值非法";
            var def = CardEffectConverter.ConvertOne(data, data.Id ?? "");
            if (def == null) return "空效果";
            if (!def.IsActivatedEffect) return "非主动效果不可作附加诅咒/祝福载荷（只挂主动效果）";
            if (CardEffectConverter.EngineKindOfDefinition(def) != BranchEngineKind.None)
                return "引擎主干不作载荷（引擎不经载荷管线，抽到时静默空转）";
            bool wantPositive = enginePolarity >= 0.5f;
            bool sawAtom = false;
            foreach (var a in AtomsDeepOf(def))
            {
                if (a == null) continue;
                sawAtom = true;
                bool positive = a.Polarity >= 0.5f, negative = a.Polarity <= -0.5f;
                if ((wantPositive && !positive) || (!wantPositive && !negative))
                    return wantPositive
                        ? "极性不匹配——附加祝福只挂极性 +1 的效果（全部原子须增益极性；中性/混合不可）"
                        : "极性不匹配——附加诅咒只挂极性 −1 的效果（全部原子须减益极性；中性/混合不可）";
            }
            if (!sawAtom) return "空效果（无原子）";
            return null;
        }

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

        /// <summary>深枚举原子（主序列 ∪ 槽级 Branch.Then 奖励递归）——载荷极性判定用：
        /// 主动效果的 Then 奖励（如引擎/门分支）也是效果内容的一部分，极性口径须整体一致。
        /// AtomsOf 维持浅扫（关键词/规则光环挂位判定不进奖励——既有口径不动）。</summary>
        private static IEnumerable<AtomicEffectInstance> AtomsDeepOf(EffectDefinition def)
        {
            if (def == null) yield break;
            foreach (var a in AtomsOf(def))
            {
                if (a == null) continue;
                yield return a;
                if (a.Branch?.Then == null) continue;
                var stack = new Stack<AtomicEffectInstance>(a.Branch.Then);
                while (stack.Count > 0)
                {
                    var r = stack.Pop();
                    if (r == null) continue;
                    yield return r;
                    if (r.Branch?.Then != null)
                        foreach (var rr in r.Branch.Then) stack.Push(rr);
                }
            }
        }
    }
}
