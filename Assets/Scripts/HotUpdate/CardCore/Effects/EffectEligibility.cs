using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 效果适用卡型单源（2026-10-29 P9 定案；2026-10-30 无次数上限定案修订；2026-11-05 时机两档
    /// 收敛、2026-11-09 再拆生物专属档修订；**2026-11-10 资格层对齐时机系数新口径**）：输入效果定义
    /// （激活方式 + 触发时机 + 原子构成），输出可挂载的卡型集合与逐卡型拒绝原因。判定表与已定案承载规则同源：
    /// · 关键词（表行 Keyword 挂位 ∪ Grant* 域恰 {Self}）：生物✓ 结界✓ 法术✗；
    /// · 一次性触发（FiresOncePerPaidLifetime 单源——不另造表）：生物✓ 结界✓；
    ///   法术仅「登场」通过（法术=施放即结算的一次性形态），死亡/离场等驻场时点无从触发；
    /// · 多次档按**期望兑现次数**判宿主（2026-11-10 新口径，CreatureCanTakeTiming 单源）：
    ///   E[N]≤1 的低频时机（TimingWeights.ExpectedOf，如 OnScry=0.6）生物✓结界✓——接受方差，
    ///   不加每回合闸，价格只按 E[N] 收；E[N]&gt;1 生物✗（期望触发 &gt;1 次的周期时机为结界专属
    ///   ——耐久即次数池，生物挂载=无对价白嫖）结界✓ 法术✗（不驻场）；
    ///   引擎载荷豁免（效果头时机=奖励发射窗，非重复语义——与计价 TimingCostFactor 同口径）；
    /// · 攻/守/横置族（IsTapAnchoredTiming=4 值，生物专属状态）：**只有生物可挂**（2026-11-09 定案）——
    ///   计价影子期走 TimingFactor.CreatureOnly ×1.5、新表期读行 Factor（宿主自身行动承担次数）；
    ///   结界✗（不横置、无从触发）法术✗（不驻场）；结界即便携带引擎载荷也不放行（该时机对结界无语义）；
    /// · 双宿主多次时机（IsUniversalRepeatTiming，2026-11-10：OnAttacked 解绑生物专属）：生物✓
    ///   结界✓（被攻击对结界有真实事件锚——受击）——计价按 Repeat 桶/行 E[N]，无生物专属溢价；
    /// · OnTurnStart：**任何宿主✗**（回合开始已收归系统专用——不能靠 Factor=0 静默变免费，
    ///   装载期点名 Error；玩家侧由 OnStandbyPhase 承接；引擎载荷豁免——raw 值经
    ///   NormalizeEngineTiming 归一为 OnStandbyPhase，运行时不可见）；
    /// · 主动效果（Activate_*）：生物✓（横置发动）结界仅充能✓（P12：充能=手牌主动——其余禁，
    ///   2026-10-15 改版）法术✓（抉择槽 1~3 即主动形态——与宿主钉值/主动槽同口径）；
    /// · 连接光环：生物✓（属性±类）结界✓ 法术✗；规则光环：生物✗ 结界✓ 法术✗；
    /// · 光环效果引用（effectRef，2026-11-05 效果光环）：授予资格单源=非主动∧多次档∧生物臂
    ///   ∧非可选∧无代价∧非引擎主干（AuraGrantRejectReason——组合器筛选/装载闸/授予缓存共用）。
    /// · 附加诅咒/祝福载荷（2026-11-06 载荷效果化）：载荷资格单源=主动∧极性匹配引擎行
    ///   （祝福=+1/诅咒=−1，全原子同极性）∧非引擎主干（CursePayloadRejectReason——
    ///   组合器载荷效果选择器/装载闸/装载期 ValidateAtomParams 共用）。
    /// 消费方：效果合成器顶栏「适用」实时推导、卡组合候选过滤（不静默隐藏——计数行+原因+跳转）、
    /// 装载期 ValidateCreatureShape / ValidateEnchantShape（CardLoader 兜底，防直改 JSON/旧档）。
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

        /// <summary>
        /// 可否作自由分支/有限分支的 Then 奖励效果引用（2026-11-07 分支奖励效果化定案·资格谓词单源
        /// ——合成器奖励效果选择器 / 槽区保存闸 / 装载期 ValidateAtomParams 共用）：
        /// 非引擎主干（引擎主干自持事件经济，作奖励=奖励永不发射）∧ 非光环形态（箭头/连接光环条目=
        /// 驻场持续语义，奖励是单次结算）∧ 无场地光环步（RuleAura 驻场全局唯一）∧ 非空原子。
        /// 不限发动方式与极性——奖励发射口 LaunchReward 自理（主动档入待发队列/自动档直发），
        /// 奖励目标由控制者结算期弹选（极性=目标侧语义，与诅咒/祝福载荷的极性闸不同源）。
        /// 输入契约同 <see cref="CursePayloadRejectReason"/>：完整卡内效果（EffectsLibrary.Resolve 还原）。
        /// 自引用（分支奖励引用宿主效果自身）由选择器排除+运行时深度闸兜底，不在本谓词判。
        /// </summary>
        public static string BranchRewardRejectReason(CardEffectData data)
        {
            if (data == null) return "空效果";
            var timing = (TriggerTiming)data.TriggerTiming;
            if (timing < 0 || timing > TriggerTiming.OnCardCastSettled) return "时机值非法";
            if (data.ArrowDirections != 0 || (data.LinkAuras != null && data.LinkAuras.Count > 0))
                return "光环形态效果不可作分支奖励（箭头/光环条目=驻场持续语义，奖励是单次结算）";
            var def = CardEffectConverter.ConvertOne(data, data.Id ?? "");
            if (def == null) return "空效果";
            if (CardEffectConverter.EngineKindOfDefinition(def) != BranchEngineKind.None)
                return "引擎主干不作分支奖励（引擎不经奖励管线，条件永不评估）";
            if (HasRuleAuraAtom(def)) return "场地光环步不可作分支奖励（驻场全局唯一语义）";
            bool sawAtom = false;
            foreach (var a in AtomsDeepOf(def))
            {
                if (a == null) continue;
                sawAtom = true;
                break;
            }
            if (!sawAtom) return "空效果（无原子）";
            return null;
        }

        /// <summary>
        /// 引擎奖励效果引用的发动档资格（2026-11-08 规则地基定案·资格谓词单源——合成器奖励效果
        /// 选择器 / 槽区保存闸 / 验证器共用）：先复用 <see cref="BranchRewardRejectReason"/>
        ///（非引擎主干/非光环形态/非空原子），再按引擎奖励类型分档——
        /// 主动奖励引擎（倒计时/死亡计数/元素充盈，ComposerCatalog.IsActiveRewardEngine）：
        /// 奖励必须是主动效果（def.IsActivatedEffect——Activate_*；发射口 LaunchReward **直发**，
        /// 2026-11-08 阶段3 定案：固定三主干触发立即生效，主动奖励不入待发队列、忽略 tactivate）；
        /// 自动奖励引擎（运势/拼枪/团结，IsAutomaticRewardEngine）：奖励必须是自动效果
        ///（IsTriggeredEffect ∧ ActivationType==Automatic）且时机有真实事件锚
        ///（TriggerTimingDefaults.GetEventType 非空——达标直发）。
        /// 无 Domain 原子不入引擎奖励（2026-11-08 阶段4 入闸）：奖励发射/目标解析走域管线——
        /// 含无域行（检索/强夺/跳过阶段等，与「无Domain不参与效果组合」同口径）的效果整体拒绝，
        /// 深扫含嵌套 Then（奖励内嵌奖励同为发射内容）。
        /// 载荷引擎（附加诅咒/祝福）不经发动档闸——载荷资格另走 CursePayloadRejectReason，
        /// Then 原子旧兼容通道照 BranchRewardRejectReason 通用口径。
        /// 输入契约同 <see cref="CursePayloadRejectReason"/>：完整卡内效果（EffectsLibrary.Resolve 还原）。
        /// </summary>
        public static string EngineRewardRejectReason(CardEffectData data, BranchEngineKind engine)
        {
            var common = BranchRewardRejectReason(data);
            if (common != null) return common;
            if (!ComposerCatalog.IsActiveRewardEngine(engine) && !ComposerCatalog.IsAutomaticRewardEngine(engine))
                return null; // 非奖励引擎（载荷/None）：只吃通用闸
            var def = CardEffectConverter.ConvertOne(data, data.Id ?? "");
            if (def == null) return "空效果";
            if (ComposerCatalog.IsActiveRewardEngine(engine) && !def.IsActivatedEffect)
                return "该引擎奖励只能挂主动效果";
            if (ComposerCatalog.IsAutomaticRewardEngine(engine)
                && !(def.IsTriggeredEffect
                     && def.ActivationType == EffectActivationType.Automatic
                     && TriggerTimingDefaults.GetEventType(def.TriggerTiming) != null))
                return "该引擎奖励只能挂自动效果，且必须有真实触发时机";
            foreach (var a in AtomsDeepOf(def))
            {
                var row = CardCore.Attribute.AtomicEffectTable.GetByType(a.Type);
                if (row != null && row.GetTargetKindList().Count == 0)
                    return "效果含无目标域原子（检索/强夺等无域行）——不可作引擎奖励";
            }
            return null;
        }

        /// <summary>逐卡型拒绝原因（null=可挂载）。多特征叠加时任一命中即拒。
        /// 2026-11-10 资格层新口径：OnTurnStart 任何宿主拒（引擎载荷豁免——raw 值经归一化）；
        /// 生物臂多次档按 E[N] 判（CreatureCanTakeTiming 单源），不再用 IsEnchantExclusiveTiming 全量判。</summary>
        public static string RejectReason(Cardtype host, EffectDefinition def, bool auraShape = false)
        {
            if (def == null) return null;
            if (def.TriggerTiming == TriggerTiming.OnTurnStart && !HasEnginePayload(def))
                return "回合开始已收归系统专用（2026-10-20）——效果时点不准挂，玩家侧由 OnStandbyPhase 承接";
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
                if (def.IsTriggeredEffect && IsTapAnchoredTiming(def.TriggerTiming))
                    return "攻/守/横置族（4 值）为生物专属自动效果——结界不横置，无从触发"
                           + "（2026-11-09 定案；2026-11-10 OnAttacked 解绑为双宿主）";
                return null; // 单次/周期反复/双宿主=耐久即次数池（2026-10-10 发数经济）；OnTurnStart 已在上闸
            }
            // 生物（及其他无耐久宿主）：2026-11-10 资格层对齐时机系数新口径——多次档按期望兑现次数判
            //（CreatureCanTakeTiming 单源：E[N]≤1 低频可挂，>1 结界专属）；攻/守/横置族（4 值）与
            // 双宿主时机（OnAttacked）照旧生物✓；引擎载荷豁免（发射窗非重复语义）。
            if (HasRuleAuraAtom(def)) return "场地光环专属结界（全局唯一、驻场生效）";
            if (def.IsTriggeredEffect && !CreatureCanTakeTiming(def.TriggerTiming) && !HasEnginePayload(def))
                return "期望触发 >1 次的周期时机为结界专属（耐久即次数池）"
                       + "——生物只能挂单次时机、攻/守/横置族、双宿主时机（OnAttacked）或低频（E[N]≤1）时机";
            return null;
        }

        /// <summary>生物可挂时机判定（2026-11-10 资格层对齐时机系数新口径——RejectReason 生物臂 /
        /// HostCanTakeTiming / 装载 ValidateCreatureShape / 验证器宿主矩阵共用单源）：
        /// OnTurnStart 恒拒（系统专用）；单次档（FiresOncePerPaidLifetime）/ 攻守横置族
        ///（IsTapAnchoredTiming 4 值）/ 双宿主时机（IsUniversalRepeatTiming——OnAttacked）✓；
        /// 其余多次档按 E[N]≤1（TimingWeights.ExpectedOf——低频时机按期望收价，接受方差不加每回合闸；
        /// 表不可读/缺行回落 1.0=恰在可挂边界，不误杀存量）。主动档（Activate_*）不经本口。</summary>
        public static bool CreatureCanTakeTiming(TriggerTiming t)
        {
            if (t == TriggerTiming.OnTurnStart) return false;
            return CostDerivationService.FiresOncePerPaidLifetime(t)
                   || IsTapAnchoredTiming(t)
                   || IsUniversalRepeatTiming(t)
                   || TimingWeights.ExpectedOf(t) <= 1.0f;
        }

        /// <summary>生物专属自动效果族（攻/守/横置——攻击宣言/阻拦宣言/横置/重置 4 值；2026-11-10 口径：
        /// OnAttacked 解绑为双宿主时机 IsUniversalRepeatTiming）：横置状态即天然限频锚，
        /// 只有生物可挂（结界不横置、法术不驻场，无从触发）；计价影子期走 TimingFactor.CreatureOnly
        /// ×1.5（2026-11-09 用户定案）、新表期读行 Factor。</summary>
        public static bool IsTapAnchoredTiming(TriggerTiming t)
            => t == TriggerTiming.OnAttack
               || t == TriggerTiming.OnBlockDeclare || t == TriggerTiming.OnTap || t == TriggerTiming.OnUntap;

        /// <summary>双宿主多次时机（2026-11-10 口径）：生物与结界都可挂，计价按 Repeat 桶/行 E[N]，
        /// 无生物专属溢价——现仅 OnAttacked（被攻击：结界受击有真实事件锚，不再视为攻/守/横置族；
        /// Bucket=Universal，从 IsEnchantExclusiveTiming 与 IsTapAnchoredTiming 双侧排除）。</summary>
        public static bool IsUniversalRepeatTiming(TriggerTiming t)
            => t == TriggerTiming.OnAttacked;

        /// <summary>宿主×时机可见性粗筛（合成器时机下拉；与 RejectReason 同口径——2026-11-10 资格层
        /// 新口径）：OnTurnStart 任何宿主✗；生物=CreatureCanTakeTiming 单源（单次+攻/守/横置族
        ///+双宿主+E[N]≤1 低频）；结界=非攻/守/横置族（双宿主/周期/观察族全开）；法术=仅登场
        ///（施放即结算的一次性形态）。效果库模式（无宿主）不调用本口、全开。</summary>
        public static bool HostCanTakeTiming(Cardtype host, TriggerTiming t)
        {
            if (t == TriggerTiming.OnTurnStart) return false;
            if (host == Cardtype.Spell) return t == TriggerTiming.OnPlay;
            if (host == Cardtype.Creature) return CreatureCanTakeTiming(t);
            if (host == Cardtype.Enchantment) return !IsTapAnchoredTiming(t);
            return true;
        }

        /// <summary>周期/观察族多次时机中**结界专属**的部分（耐久即次数池——额外触发次数已由耐久支付，
        /// 生物挂载=无对价白嫖）：多次档 − 攻/守/横置族（4 值）− 双宿主时机（OnAttacked——2026-11-10
        /// 解绑，生物可挂）。2026-11-05 收回 2026-10-30 的放开；2026-11-09 攻/守/横置族拆出。
        /// 注意：本口不再是生物臂资格闸的全量判据——2026-11-10 起生物臂按 E[N] 判
        ///（CreatureCanTakeTiming 单源，E[N]≤1 的本集合成员生物也可挂）。
        /// 消费方：RejectReason 生物臂（历史留痕）/ 合成器时机下拉宿主过滤 / 装载校验 / 验证器审计。</summary>
        public static bool IsEnchantExclusiveTiming(TriggerTiming t)
            => CostDerivationService.IsRepeatTiming(t) && !IsTapAnchoredTiming(t) && !IsUniversalRepeatTiming(t);

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
