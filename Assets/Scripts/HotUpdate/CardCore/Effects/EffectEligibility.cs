using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 效果适用卡型单源（2026-10-29 P9 定案；**2026-11-14 档位制**）：输入效果定义
    /// （激活方式 + 触发时机 + 原子构成），输出可挂载的卡型集合与逐卡型拒绝原因。
    /// 时机档位单源=<see cref="TimingWeights.TierOf"/>（表 Bucket），判定表与已定案承载规则同源：
    /// · 关键词（表行 Keyword 挂位 ∪ Grant* 域恰 {Self}）：生物✓ 结界✓ 法术✗；
    /// · **一回合一次（PerTurn）**——登场/进场/攻击/守卫/回合开始/准备阶段/回合结束/被攻击/横置/
    ///   重置/游戏开始：默认每回合至多触发一次（系数 1）；生物✓ 结界✓；
    ///   法术仅「登场」通过（法术=施放即结算的一次性形态）。
    /// · **稀有（Rare）**——被破坏/被除外/死亡/离场/墓地回场/角色死亡/占卜：比一回合一次更难触发
    ///   （系数=触发概率）；生物✓ 结界✓；法术✗（不驻场）。
    /// · **一回合多次（Multi）**——抽牌/伤害/使用卡牌/结算观察/原子三阶段/阶段起止/其他生物进出场/
    ///   施放法术：**结界专属**（耐久即次数池，每次触发 −1）；生物✗ 法术✗（不驻场）。
    /// · 攻/守/横置/重置四个生物动作族（IsTapAnchoredTiming）：**只有生物可挂**（结界不横置、
    ///   法术不驻场）——它们属一回合一次档（横置限频），系数 1。
    /// · 被攻击（OnAttacked）：一回合一次 + 双宿主（生物/结界都可挂，受击有真实事件锚）。
    /// · 引擎载荷（2026-11-13 收口）：**只有结界可挂**——引擎=无上限触发，生物挂载没有耐久对价；
    ///   装载期生物带引擎直接拒（Creator/Eligibility 同口），效果头时机=奖励发射窗仅作展示归一。
    /// · 主动效果（Activate_*）：生物✓（横置发动）结界仅充能✓（P12）法术✓（抉择槽 1~3）。
    /// · 连接光环：生物✓（属性±类）结界✓ 法术✗；规则光环：生物✗ 结界✓ 法术✗；
    /// · 光环效果引用（effectRef）：授予资格单源=非主动∧生物臂可挂∧非可选∧无代价∧非引擎主干
    ///   （AuraGrantRejectReason——组合器筛选/装载闸/授予缓存共用）。
    /// · 附加诅咒/祝福载荷：载荷资格单源=主动∧极性匹配引擎行∧非引擎主干。
    /// 消费方：效果合成器顶栏「适用」实时推导、卡组合候选过滤（不静默隐藏——计数行+原因+跳转）、
    /// 装载期 ValidateCreatureShape / ValidateEnchantShape（CardLoader 兜底，防直改 JSON/旧档）。
    /// 注意：本类只判「能不能挂」，不管槽位容量与保存闸——既有形状闸（FillSelectedSlot）与
    /// 宿主钉值（SyncActivationVisibility）仍在调用侧前置。
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
        /// 非主动 ∧ 生物臂可挂（受益者=生物；2026-11-14 档位制下＝一回合一次/稀有两档——
        /// 一回合多次=结界专属，故不可授予）∧ 非可选触发（Voluntary 需玩家选择窗口）∧
        /// 无代价行（授予=免费自动效果，2026-11-05 拍板）∧ 非引擎主干（引擎效果不入触发管线，
        /// 合成条目会成孤儿栈项）。返回 null=可引用，否则拒绝原因（UI 筛选提示/装载告警共用文案）。
        /// </summary>
        public static string AuraGrantRejectReason(CardEffectData data)
        {
            if (data == null) return "空效果";
            var timing = (TriggerTiming)data.TriggerTiming;
            if (timing < 0 || timing > TriggerTiming.OnCardCastSettled) return "时机值非法";
            var def = CardEffectConverter.ConvertOne(data, data.Id ?? "");
            if (def.IsActivatedEffect) return "主动效果不可被光环授予（只授予自动触发式）";
            // 2026-11-10 用户定案：连接光环可赋予**所有自动触发效果**（含 OnPlay 登场档）；
            // 旧「只授予多次触发时机」硬闸删除；2026-11-14 档位制下「一回合多次」仍被生物臂拒
            //（结界专属，授予到生物身上没有耐久对价）。
            if (TriggerTimingDefaults.GetEventType(timing) == null)
                return "该时机没有真实事件锚，不可被光环授予（只授予自动触发效果）";
            var creatureArm = RejectReason(Cardtype.Creature, def);
            if (creatureArm != null) return creatureArm; // 一回合多次=结界专属、场地光环原子等
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
        /// ——合成器奖励效果选择器 / 槽区保存闸 / 装载期 ValidateAtomParams 共用；**2026-11-10 R4
        /// 修订**：引擎奖励与一般分支奖励合并为同一谓词，不再按发动档分档）：
        /// 非引擎主干（引擎主干自持事件经济，作奖励=奖励永不发射）∧ 非光环形态（箭头/连接光环条目=
        /// 驻场持续语义，奖励是单次结算）∧ 无场地光环步（RuleAura 驻场全局唯一）∧ 非空原子
        /// ∧ **无代价行**（代价=费用抵扣不是奖励——全价≤3、同色逐行抵扣口径）∧ **无任何嵌套分支**
        ///（含遗留 kind=1 门步折算后的 Gate/Outcome 载荷——奖励=并列整效果单次结算，条件/引擎
        /// 嵌套请拆独立效果；深扫递归 Then）∧ **无 Domain 原子**（奖励发射/目标解析走域管线——
        /// 与「无Domain不参与效果组合」同口径，深扫含嵌套 Then）∧ **并列形状（1~2 主干原子）**
        ///（与合成器并列槽同口径——奖励是扁平并列效果，不是长链编排）。
        /// 不限发动方式与极性——奖励发射口 LaunchReward 自理（固定三直发/自动三按外层效果头时机
        /// 评估），奖励目标由控制者结算期弹选（极性=目标侧语义，与诅咒/祝福载荷的极性闸不同源）。
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
            if (data.Costs != null && data.Costs.Count > 0)
                return "带代价行的效果不可作分支奖励（代价=费用抵扣不是奖励——全价≤3、同色逐行抵扣）";
            var def = CardEffectConverter.ConvertOne(data, data.Id ?? "");
            if (def == null) return "空效果";
            if (CardEffectConverter.EngineKindOfDefinition(def) != BranchEngineKind.None)
                return "引擎主干不作分支奖励（引擎不经奖励管线，条件永不评估）";
            if (HasRuleAuraAtom(def)) return "场地光环步不可作分支奖励（驻场全局唯一语义）";
            int trunkCount = 0;
            foreach (var a in AtomsDeepOf(def))
            {
                if (a == null) continue;
                if (a.Branch != null)
                    return "分支嵌套效果不可作奖励（奖励=并列整效果单次结算——条件/引擎嵌套请拆独立效果）";
                var row = CardCore.Attribute.AtomicEffectTable.GetByType(a.Type);
                if (row != null && row.GetTargetKindList().Count == 0)
                    return "效果含无目标域原子（检索/强夺等无域行）——不可作奖励";
                trunkCount++;
            }
            if (trunkCount == 0) return "空效果（无原子）";
            if (trunkCount > 2)
                return $"非并列形状不可作奖励（主干原子 {trunkCount} 个超两槽——奖励=1~2 原子的并列效果）";
            return null;
        }

        /// <summary>引擎奖励效果引用资格（2026-11-08 阶段4 曾按发动档分档——主动奖励引擎只收主动/
        /// 自动奖励引擎只收自动且带真实时机；**2026-11-10 R4 修订**：分档闸整删，引擎奖励与一般
        /// 分支奖励合并走 <see cref="BranchRewardRejectReason"/> 同一谓词——任意发动档的并列效果
        /// 均可作奖励，发射/评估时机由引擎自理（固定三直发；自动三按外层效果头时机评估——
        /// 否则主动效果作运势奖励时事件类型永不匹配）。engine 参数保留仅为调用方签名兼容。
        /// 载荷引擎（附加诅咒/祝福）载荷资格另走 CursePayloadRejectReason，不经本口。</summary>
        public static string EngineRewardRejectReason(CardEffectData data, BranchEngineKind engine)
            => BranchRewardRejectReason(data);

        /// <summary>逐卡型拒绝原因（null=可挂载）。多特征叠加时任一命中即拒。
        /// 2026-11-14 档位制：生物=一回合一次+稀有两档（CreatureCanTakeTiming 单源）；
        /// 一回合多次=结界专属；攻/守/横置/重置四个生物动作=只有生物可挂；引擎载荷=只有结界可挂。
        /// OnTurnStart 已随档位制开放（一回合一次，与 OnStandbyPhase 并列，不再系统专用）。</summary>
        public static string RejectReason(Cardtype host, EffectDefinition def, bool auraShape = false)
        {
            if (def == null) return null;
            if (host == Cardtype.Spell)
            {
                if (HasKeywordAtom(def)) return "法术无关键词载体（关键词=生物/结界印刷位）";
                if (auraShape) return "光环（连接/场地）专属结界载体（法术只挂主动效果）";
                if (HasRuleAuraAtom(def)) return "场地光环专属结界载体（全局唯一、驻场生效）";
                if (def.IsTriggeredEffect)
                    return def.TriggerTiming == TriggerTiming.OnPlay
                        ? null // 登场=施放即结算的一次性——法术的合法形态
                        : "法术一次性施放，只能挂「登场」时机（死亡/回合/反复触发在法术上无从发生）";
                return null; // 主动=抉择槽形态
            }
            if (host == Cardtype.Enchantment)
            {
                //（P12 充能豁免已随 2026-11-05 充能系统动作化改版收回：充能不再是卡上效果——
                //  结界照旧禁一切主动，只挂自动/光环。）
                if (def.IsActivatedEffect)
                    return "结界禁主动效果（2026-10-15 改版——只挂自动/光环）";
                if (def.IsTriggeredEffect && IsTapAnchoredTiming(def.TriggerTiming))
                    return "攻/守/横置/重置族（4 值）为生物动作时机——结界不横置、不宣言攻击，无从触发"
                           + "（2026-11-14 档位制；被攻击 OnAttacked 仍为双宿主）";
                return null; // 一回合一次/稀有/一回合多次=都放行（多次=耐久即次数池）
            }
            // 生物（及其他无耐久宿主）：2026-11-14 档位制——一回合一次（PerTurn）与稀有（Rare）可挂；
            // 一回合多次（Multi）=结界专属（耐久即次数池，生物挂载=无对价白嫖）。
            // **2026-11-13 收口：引擎载荷不再豁免**——引擎=无上限触发，生物挂载=无耐久对价，
            // 引擎只能挂结界（耐久买次数）。
            if (HasRuleAuraAtom(def)) return "场地光环专属结界（全局唯一、驻场生效）";
            if (def.IsTriggeredEffect && HasEnginePayload(def))
                return "引擎载荷只能挂结界（引擎无触发上限，结界用耐久买次数）——生物没有耐久对价";
            if (def.IsTriggeredEffect && !CreatureCanTakeTiming(def.TriggerTiming))
                return "一回合内会多次触发的时机为结界专属（耐久即次数池）"
                       + "——生物只能挂一回合一次时机（攻击/守卫/回合开始/回合结束/登场等）"
                       + "或稀有（被破坏/被除外/死亡等，按概率计价）";
            return null;
        }

        /// <summary>生物可挂时机判定（2026-11-14 档位制——RejectReason 生物臂 / HostCanTakeTiming /
        /// 装载 ValidateCreatureShape / 验证器宿主矩阵共用单源）：
        /// <see cref="TimingTier.PerTurn"/>（一回合一次：登场/进场/攻击/守卫/回合开始/准备/回合结束/
        /// 被攻击/横置/重置/游戏开始）与 <see cref="TimingTier.Rare"/>（被破坏/被除外/死亡/离场/
        /// 墓地回场/角色死亡/占卜）✓；<see cref="TimingTier.Multi"/>（一回合多次）✗——结界专属。
        /// 主动档（Activate_*）不经本口。表不可读时按内置档位镜像判（<see cref="TimingWeights.TierOf"/>）。</summary>
        public static bool CreatureCanTakeTiming(TriggerTiming t)
            => TimingWeights.TierOf(t) != TimingTier.Multi;

        /// <summary>生物专属自动效果族（攻/守/横置——攻击宣言/阻拦宣言/横置/重置 4 值；2026-11-14
        /// 档位制下归「一回合一次」档，系数 1，OnAttacked 为双宿主不计入本族）：
        /// 横置状态即天然限频锚，只有生物可挂（结界不横置、法术不驻场，无从触发）。</summary>
        public static bool IsTapAnchoredTiming(TriggerTiming t)
            => t == TriggerTiming.OnAttack
               || t == TriggerTiming.OnBlockDeclare || t == TriggerTiming.OnTap || t == TriggerTiming.OnUntap;

        /// <summary>双宿主时机（生物与结界都可挂，一回合一次档）：现仅 OnAttacked（被攻击：
        /// 结界受击有真实事件锚）。档位由 TimingWeights.TierOf=PerTurn 决定，系数 1。</summary>
        public static bool IsDualHostTiming(TriggerTiming t)
            => t == TriggerTiming.OnAttacked;

        /// <summary>宿主×时机可见性粗筛（合成器时机下拉；与 RejectReason 同口径——2026-11-14 档位制）：
        /// 生物=CreatureCanTakeTiming 单源（一回合一次+稀有）；结界=非生物动作族（一回合一次/稀有/
        /// 一回合多次全开）；法术=仅登场（施放即结算的一次性形态）。效果库模式（无宿主）不调用本口、全开。</summary>
        public static bool HostCanTakeTiming(Cardtype host, TriggerTiming t)
        {
            if (host == Cardtype.Spell) return t == TriggerTiming.OnPlay;
            if (host == Cardtype.Creature) return CreatureCanTakeTiming(t);
            if (host == Cardtype.Enchantment) return !IsTapAnchoredTiming(t);
            return true;
        }

        /// <summary>一回合多次档中**结界专属**的部分（耐久即次数池——每次触发 −1 耐久已支付次数，
        /// 生物挂载=无对价白嫖）：2026-11-14 档位制= <see cref="TimingWeights.TierOf"/>==Multi。
        /// 消费方：RejectReason 生物臂 / 合成器时机下拉宿主过滤 / 装载校验 / 验证器审计。</summary>
        public static bool IsEnchantExclusiveTiming(TriggerTiming t)
            => TimingWeights.TierOf(t) == TimingTier.Multi;

        /// <summary>是否携带引擎载荷（settle=Engine——扁平 Effects 与节点化 Steps 双扫）：
        /// 效果头时机=奖励发射窗，不代表主干发射次数（与计价 TimingCostFactor 豁免同口径）——
        /// 宿主闸 2026-11-13 收口后**只有结界可挂**（引擎=无上限触发，生物没有耐久对价）。
        /// Editor 可见的公开转发口。</summary>
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
