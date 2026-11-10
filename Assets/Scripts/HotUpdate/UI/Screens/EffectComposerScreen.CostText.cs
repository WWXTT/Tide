using Cysharp.Threading.Tasks;
using CardCore;
using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UInputField = TMPro.TMP_InputField;

namespace SynergyUI
{
    public sealed partial class EffectComposerScreen : UIScreen
    {
        // ======================================== 主动效果代价行（2026-10-29 P5 建 · 2026-10-30 用户定案整删） ========================================
        // cost-row 代码建组（标签+下拉+参数输入）与候选/回写/写入链全部退役——效果组合界面不再编辑
        // 效果级代价；header.Costs 存量数据保留（运行时净费闸照常消费）。「代价→净费」③段公式文本
        // 已随 2026-11-06 方格改版退役——净费/代价进费用徽标双组（CostBadgeCosts）。

        // ======================================== 效果名自动构成 ========================================

        /// <summary>自动名（2026-10-05 两槽定案）：并列=逐槽 AtomText.RenderAtomEntry+BranchSuffix 拼接
        ///（空槽不占位）；光环=条目列表×箭头数；规则级光环=仪典名（双方生效·全局唯一，无箭头）。
        /// FreeBranch/OutcomeGate 专属文案分支已随形态删除。</summary>
        private string AutoName()
        {
            var h = _graph.header;
            if (IsAuraMode(_mode))
            {
                // 投放步（仪典/改写，2026-10-07 定案句式）：仪典=「规则光环：【范围】描述」
                //（表 Description 已带「规则光环：」前缀——剥离后插范围）；改写（降级连接光环）
                //=「【范围】受光环影响的…」（表文案原样）。
                var ruleParts = new List<string>();
                foreach (var s in _graph.steps ?? new List<EffectStepData>())
                {
                    if (s?.kind != 0 || s.atomic == null) continue;
                    var rc = AtomicEffectTable.GetByHashId(s.atomic.refId);
                    if (!IsRitualStepRow(rc)) continue;
                    string scopeZh = RuleAuraSystem.RuleAuraScopeZh(s.atomic.value);
                    if (IsRuleAuraRow(rc))
                    {
                        var desc = rc.Description ?? "";
                        const string Prefix = "场地光环：";
                        if (desc.StartsWith(Prefix)) desc = desc.Substring(Prefix.Length);
                        ruleParts.Add($"场地光环：【{scopeZh}】{desc}");
                    }
                    else ruleParts.Add($"【{scopeZh}】{rc.Description}");
                }
                if (ruleParts.Count > 0) return string.Join("；", ruleParts);

                // 连接条目（2026-10-07 句式；2026-10-12 改稿）：作用面条目=【范围】前缀+「受光环影响的…」
                //（关键词条目含角色尾注）；连接方向档=「×N箭头，箭头指向的…」——箭头数前置，主语换成指向者
                var scopedParts = new List<string>();
                var arrowParts = new List<string>();
                foreach (var a in h.LinkAuras ?? new List<LinkAuraData>())
                {
                    if (a == null) continue;
                    string subject = a.scope > 0 ? "受光环影响的" : "箭头指向的";
                    string body;
                    if (!string.IsNullOrEmpty(a.stat))
                        body = $"{subject}生物{StatZhOf(a.stat)}{a.value:+0;-0}";
                    else if (!string.IsNullOrEmpty(a.effectRef))
                        body = $"{subject}单位获得效果「{EffectsLibrary.Get(a.effectRef)?.name ?? a.effectRef}」";
                    else if (!string.IsNullOrEmpty(a.keyword))
                        body = $"{subject}单位获得{KeywordZh(a.keyword)}"; // 关键词条目恒含角色（2026-10-09 裁定）
                    else continue;
                    if (a.scope > 0)
                        scopedParts.Add($"【{LinkScopeZh(a.scope)}】{body}");
                    else arrowParts.Add(body);
                }
                if (scopedParts.Count > 0 || arrowParts.Count > 0)
                {
                    var all = new List<string>(scopedParts);
                    if (arrowParts.Count > 0)
                    {
                        int n = CountArrowBits((HexDirection)h.ArrowDirections);
                        all.Add(n > 0 ? $"×{n}箭头，{string.Join("，", arrowParts)}" : string.Join("，", arrowParts));
                    }
                    return string.Join("；", all);
                }
            }
            // 并列：逐槽 主干描述+分支后缀（BranchSuffix 无载荷返回空串）
            var parts = new List<string>();
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                string body = AtomText.RenderAtomEntry(s.atomic, h);
                string suffix = AtomText.BranchSuffix(s.atomic);
                parts.Add(suffix.Length > 0 ? $"{body}{suffix}" : body);
            }
            return parts.Count == 0 ? "（空）" : string.Join("＋", parts);
        }

        /// <summary>光环关键词中文名（2026-10-09 改口径：与卡组合/构筑/悬浮卡三处 KeywordZh 同源
        /// ——关键词定义 nameZh=原子表中文短名，关键词不单独开表；未登记回落旧脏值映射，
        /// "Armor"→坚韧为 2026-10-08 指示物化前的旧数据兼容显示，再回落原样 id）。</summary>
        private static string KeywordZh(string keywordId)
        {
            var def = CardLoader.GetKeywordDefinition(keywordId);
            if (!string.IsNullOrEmpty(def?.nameZh)) return def.nameZh;
            return keywordId switch
            {
                "Armor" => "坚韧",
                "Guardian" => "守护",
                null or "" => "？",
                _ => keywordId,
            };
        }

        /// <summary>条目 stat 短名（Both=属性——攻生同值 ±1/±1，2026-10-07 深夜三档）。</summary>
        private static string StatZhOf(string stat)
            => stat.Equals("Life", StringComparison.OrdinalIgnoreCase) ? "生命"
             : stat.Equals("Both", StringComparison.OrdinalIgnoreCase) ? "属性" : "攻击";

        /// <summary>条目级作用范围短名（2026-10-08 actuating-range：0=连接方向/1己/2双/3对）。</summary>
        private static string LinkScopeZh(int scope)
            => scope == 1 ? "己方" : scope == 2 ? "双方" : scope == 3 ? "对方" : "连接方向";

        /// <summary>顶栏效果名=完整效果描述（2026-10-03 定案：不再简写组合名——空效果回落自动名占位）；
        /// 光环形态（2026-10-07）改走 AutoName 三段句式（仪典/改写/条目——范围前置、描述取表行）。
        /// 2026-10-09 单源化：顶栏预览与保存（graph.name/header.DisplayName）同取此串——
        /// 名字同时是效果 id 的哈希基底（HashEffect NM 段），两处不同源会出现「预览对、存后名字不一致」。</summary>
        private string CurrentDisplayName()
        {
            var full = AtomText.RenderEffectSummary(_graph);
            return IsAuraMode(_mode) ? AutoName()
                : (string.IsNullOrEmpty(full) ? AutoName() : full);
        }

        private void RefreshName()
        {
            _nameLabel.text = CurrentDisplayName();
            // 2026-10-30 区域扩容改版：顶栏=徽标＋多行明细（构成/合计+实付/代价→净费）；悬浮描述条留完整构成
            if (_costLabel != null)
            {
                _costLabel.text = CostSummaryText();
                var costTag = _costLabel.GetComponent<UiKit.DescTag>();
                if (costTag != null) costTag.Text = EffectCostText();
            }
            if (_costBadgeView != null)
            {
                var (badgeCost, badgePay) = CostBadgeCosts();
                // 0费=单块灰「0」占位（2026-11-03 手牌定案·2026-11-06 全局统一）——净费全零也 SetCosts
                if (badgeCost != null) _costBadgeView.SetCosts(badgeCost);
                else _costBadgeView.Clear();
                _costBadgeView.SetPayloadCosts(badgePay); // 效果级代价组（null=藏 Cost2）
                if (_costBadgeNode != null)
                    _costBadgeNode.gameObject.SetActive(badgeCost != null || badgePay != null);
            }
        }

        /// <summary>费用完整构成（2026-10-23 改版：不再直显顶栏——悬浮费用区经描述条单行滚动展开）：
        /// 并列=主干段（参数合规才显示：效果锚价完整构成——逐原子贡献＋合计＋黑白获得）
        /// ＋分支段（2026-10-09 定案：只声明「分支（奖励免费）」——奖励锚价/上限不随段列出，
        /// 超上限提示由槽区校验红框承载、保存已禁；分支段独立于主干校验恒显——超限时主干段隐藏，
        /// 分支段在案提示）；光环=条目平价直列（2026-10-12：箭头费入条目价、预览卡携带预选箭头，
        /// 无「费用（光环条目）：」冠语）；主干校验违规/转换失败/推导异常→对应段空串。</summary>
        private string EffectCostText()
        {
            try
            {
                if (IsAuraMode(_mode)) return AuraTopCostText();
                string trunk = ZonesAllValid() ? ParallelCostText() : "";
                string branch = BranchCostText();
                if (trunk.Length == 0) return branch;
                return branch.Length == 0 ? trunk : trunk + "｜" + branch;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>顶栏费用多行明细（2026-10-30 区域扩容改版：一行摘要→分行直显，2026-10-23 缩进
        /// 悬浮的逐原子公式链随区域扩容回迁顶栏）：
        /// ① 构成行=「费用：逐原子 基准×系数链＝贡献」（ParallelCostBreakdown 单源）；
        /// ② 合计行=合计真值（真实小数）＋对战实付（逐色进1——整数费两值同则省略）＋分支段；
        /// ③ 代价行=「代价（臂）：名 值（价P）→ 净费 X（实付 Y·进1）」（带代价行才显）。
        /// 光环=「合计 X（N 条目）」（光环价入卡价非对战现付——不附实付段）；转换失败只留分支段。
        /// 完整构成仍留悬浮描述条（EffectCostText——与顶栏同源不冲突）。</summary>
        private string CostSummaryText()
        {
            try
            {
                if (IsAuraMode(_mode))
                {
                    var (auraTotal, n) = AuraCostTotal();
                    return n > 0 ? $"合计 {CostZh(auraTotal)}（{n} 条目）" : "";
                }
                string branch = BranchCostText();
                if (!ZonesAllValid()) return branch;
                var (body, total, def) = ParallelCostBreakdown();
                if (def == null) return branch; // 转换失败=旧口径：只留分支段
                var lines = new List<string>();
                if (body.Length > 0) lines.Add(body); // ① 构成行（逐原子公式链）
                var sumLine = $"合计 {CostZh(total)}"; // ② 合计行：真值＋代价抵扣＋净费＋对战实付＋分支
                // 代价抵扣→净费（2026-11-06）：带效果级代价的主动效果——抵扣=合计−净费的逐色实抵
                //（NetBill 色锁扣桶后的差额，比 DeductionPoints 潜在上限更准）——费用预览三段式
                //「合计 X｜代价抵扣 Y｜净费 Z」，与引擎发动闸/徽标费用组同源
                var special = def.Costs != null
                    ? def.Costs.Where(c => c != null && c.Type != CostType.ElementConsume).ToList() : null;
                //（2026-11-10 R4 阶段3：去 IsActivatedEffect 前置——非主动效果带代价门同样显示
                //  「全费 − 代价 = 净费」；def 经 PreviewEffectWithCosts 合成临时 Costs）
                if (special != null && special.Count > 0
                    && EffectActivationGate.DeductionPoints(special) > 0)
                {
                    var net = EffectActivationGate.NetBill(total, special);
                    var deduction = new ElementCost();
                    for (int ci = 0; ci < ElementCost.Length; ci++)
                        deduction.v[ci] = Math.Max(0f, total.v[ci] - net.v[ci]);
                    if (!deduction.IsZero)
                        sumLine += $"｜代价抵扣 {CostZh(deduction)}｜净费 {CostZh(net)}";
                }
                var pay = PayZh(total);
                if (pay != CostZh(total)) sumLine += $"｜对战实付 {pay}（小数逐色进1）";
                if (branch.Length > 0) sumLine += "｜" + branch;
                lines.Add(sumLine);
                // ③「代价→净费」段已随 2026-11-06 方格改版退役——信息进费用徽标双组
                //（费用组=净费、代价组=效果级代价，CostBadgeCosts）
                return string.Join("\n", lines);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>费用徽标双组（2026-11-06 方格改版：原「代价→净费」③段公式文本退役）：
        /// 费用组=净费（光环=条目合计；并列=推导合计；带效果级代价=全费−抵扣=NetBill——
        /// 2026-11-10 R4 阶段3 起不限主动档：非主动效果带代价门同样显示净费）；
        /// 代价组=效果级代价分色面（PayloadGrantByColor——Cost2，有代价才显组）。
        /// def 经 PreviewEffectWithCosts（代价门哨兵→临时 Costs 只读合成）。无效组合=null。</summary>
        private (ElementCost fee, ElementCost pay) CostBadgeCosts()
        {
            try
            {
                if (IsAuraMode(_mode))
                {
                    var (auraTotal, n) = AuraCostTotal();
                    return (n > 0 ? auraTotal : null, null);
                }
                if (!ZonesAllValid()) return (null, null);
                var def = PreviewEffectWithCosts();
                if (def == null) return (null, null);
                var badge = CostDerivationService.DeriveElementCosts(def, 0);
                var special = def.Costs != null
                    ? def.Costs.Where(c => c != null && c.Type != CostType.ElementConsume).ToList()
                    : null;
                //（2026-11-10 R4 阶段3：去 IsActivatedEffect 前置——非主动效果带代价门同样
                //  费用组=净费；代价组仍显示代价分色面 PayloadGrantByColor）
                if (special != null && special.Count > 0
                    && EffectActivationGate.DeductionPoints(special) > 0)
                    badge = EffectActivationGate.NetBill(badge, special); // 带代价行——费用组=净费
                var grant = special != null && special.Count > 0
                    ? CardCostService.PayloadGrantByColor(special) : null;
                var pay = new ElementCost();
                if (grant != null)
                    foreach (var kv in grant) pay[kv.Key] = kv.Value;
                return (badge, pay.IsZero ? null : pay);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>光环条目费用合计（Stage A 明细按色累加；n=条目行数——AuraTopCostText 同源）。</summary>
        private (ElementCost total, int n) AuraCostTotal()
        {
            var lines = CardCore.CardCostService.Derive(AuraPreviewCard()).Breakdown
                .Where(l => l != null && l.Stage == "A").ToList();
            var total = new ElementCost();
            foreach (var l in lines)
            {
                if (l.Color.HasValue) total[l.Color.Value] += l.Value;
                else total[(int)ManaType.Gray] += l.Value;
            }
            return (total, lines.Count);
        }

        /// <summary>分支段文本（2026-10-09 定案）：引擎/其余分支只声明「分支（奖励免费）」；
        /// **代价门**（2026-11-10 R4 阶段3）显示「代价（减免 N）」——代价=费用抵扣不是奖励
        ///（N=逐原子全价 PayloadUnitGrant 合计，≤3 上限由槽区校验拦）；
        /// **附加诅咒/祝福例外**（同日附加指示物定案）——不再奖励免费：按分支实际载荷费用减半向上取整入价
        ///（2026-11-06 载荷效果化：载荷=效果库组合效果引用——按引用效果锚价取费；
        /// EngineBranchSurchargeHalfOf 与卡价 DeriveElementCosts.EngineBranchSurcharge 同源）；
        /// 超上限提示由槽区校验红框承载（保存已禁），未设载荷的分支不列（槽区校验拦）。</summary>
        private string BranchCostText()
        {
            var steps = _graph.steps ?? new List<EffectStepData>();
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var b = step?.kind == 0 ? step.atomic?.branch : null;
                if (b == null) continue;
                var ek = Enum.IsDefined(typeof(BranchEngineKind), b.engine) ? (BranchEngineKind)b.engine : BranchEngineKind.None;
                if (ek == BranchEngineKind.CurseOnDraw || ek == BranchEngineKind.BlessingOnDraw)
                {
                    float half = CurseSurchargeHalfOf(step.atomic, b);
                    if (half > 0f) return $"分支（{ComposerCatalog.EngineZhOf(ek)}费减半：{half:0.#}）";
                    continue; // 未设载荷——槽区校验已拦（CursePayloadError），费用段不列
                }
                if (IsCostGate(b))
                {
                    // 代价门（2026-11-10 R4 阶段3）：代价=费用抵扣不是奖励——减免量=逐原子全价合计
                    //（与槽区校验/保存回写同锚；未填由槽区红框拦，费用段不列）
                    if (b.then == null || b.then.Count == 0) continue;
                    float pts = 0f;
                    foreach (var a in b.then) pts += CostAtomPrice(a);
                    return $"代价（减免 {pts:0.#}）";
                }
                if (b.then?.FirstOrDefault() == null) continue;
                return "分支（奖励免费）";
            }
            return "";
        }

        /// <summary>附加诅咒/祝福附加费标量（2026-11-10 双通道）：效果引用（branch.effectId 优先，
        /// legacy atom.str 兼容）+ Then 原子 合计后 ceil 减半——与 CostDerivation.CursePayloadFullCost 同口径。</summary>
        private static float CurseSurchargeHalfOf(AtomicEffectEntry atom, BranchEntryData b)
        {
            var fxId = !string.IsNullOrEmpty(b?.effectId) ? b.effectId : atom?.str;
            var insts = new List<AtomicEffectInstance>();
            if (b?.then != null)
                foreach (var t in b.then)
                {
                    var inst = CardEffectConverter.ConvertAtomForUI(t);
                    if (inst != null) insts.Add(inst);
                }
            float full = CostDerivationService.BranchRewardDerivedCost(insts, fxId);
            return full > 0f ? (float)Math.Ceiling(full / 2f) : 0f;
        }

        /// <summary>全部校验区合规（无副作用查询：只跑 Check 判空，不动红框/错误标签——与 ValidateZones 互不干扰）。</summary>
        private bool ZonesAllValid()
        {
            foreach (var z in _slotZones)
            {
                if (z?.Zone == null || z.Check == null) continue;
                string err = null;
                try { err = z.Check(); } catch { /* 校验异常按合规处理——同 ValidateZones 口径 */ }
                if (!string.IsNullOrEmpty(err)) return false;
            }
            return true;
        }

        /// <summary>并列效果费用构成（效果锚价口径：ConvertOne+DeriveElementCosts 纯表累加、
        /// 无减免抵消；代价不参与——代价栏已上移卡组合层）。逐原子贡献=同头单原子 shim 单独推导
        /// （与 RewardDerivedCost 同手法）。逐原子 shim 各自为真值（2026-10-30 真实小数定案：
        /// 合成期不再逐色取整），分账加总与合计允许浮点尾差——合计行才是真值。
        /// 2026-10-10 参与变量写全：逐原子不再只报数——展开「基准×系数链＝结果」
        ///（AtomCostFormula 与 ComputeAtomCost 同批判定原语，如 造成伤害5：基准1红×5×速度档2＝10红）。</summary>
        private string ParallelCostText()
        {
            var (body, total, _) = ParallelCostBreakdown();
            return body.Length == 0 ? "" : body + $"｜合计 {CostZh(total)}";
        }

        /// <summary>并列费用分账单源（ParallelCostText 悬浮全文与顶栏明细 CostSummaryText 共用）：
        /// body=「费用：逐原子公式链」（空组合=""）；total=合计真值（真实小数）；
        /// def=装载后效果定义（费用徽标双组 CostBadgeCosts 消费——省一次重复 ConvertOne）。
        /// 2026-11-10 R4 阶段3：def 经 PreviewEffectWithCosts 构造——代价门哨兵剥除+临时 Costs
        /// 合成（净费/代价组预览=保存后真实账单；不写回 _graph）。</summary>
        private (string body, ElementCost total, EffectDefinition def) ParallelCostBreakdown()
        {
            var def = PreviewEffectWithCosts();
            if (def == null) return ("", new ElementCost(), null);
            var total = CostDerivationService.DeriveElementCosts(def, 0);

            var parts = new List<string>();
            foreach (var atom in BillableAtomsOf(def))
            {
                var shim = PricingShim(def, atom);
                var atomCost = CostZh(CostDerivationService.DeriveElementCosts(shim, 0));
                var (baseExpr, factors) = CostDerivationService.AtomCostFormula(atom, shim,
                    CostDerivationService.PricingVisitDomain(shim, atom));
                var chain = factors.Count == 0 ? baseExpr : baseExpr + "×" + string.Join("×", factors);
                // 引擎附加费（附加诅咒/祝福分支减半入价）非乘法链——尾注展开，防「公式≠结果」
                //（2026-11-06 载荷效果化：载荷=效果引用时按引用效果锚价取费——HalfOf 双通道同源）
                var b = atom.Branch;
                if (b != null && b.Settle == BranchSettleKind.Engine
                    && (b.EngineKind == BranchEngineKind.CurseOnDraw || b.EngineKind == BranchEngineKind.BlessingOnDraw))
                {
                    float half = CostDerivationService.EngineBranchSurchargeHalfOf(b);
                    if (half > 0f) chain += $"＋附加减半{half:0.#}";
                }
                parts.Add($"{AtomPartLabel(atom)}：{chain}＝{atomCost}");
            }
            // 黑白获得尾注已删（2026-10-28 P1：错边/代价不再产出元素——DeriveElementGrants 退役）
            return (parts.Count == 0 ? "" : "费用：" + string.Join("＋", parts), total, def);
        }

        /// <summary>计费原子遍历（镜像 CostDerivationService.VisitBillableAtoms 主序列口径——
        /// 主干原子；分支 Then 奖励免费不列——分支段另见 BranchCostText。抉择首模式扫描已随
        /// 2026-10-31 P12 抉择退役删除——converter 不再生成 Choice 步）。</summary>
        private static IEnumerable<AtomicEffectInstance> BillableAtomsOf(EffectDefinition def)
        {
            if (def.Steps != null && def.Steps.Count > 0)
            {
                foreach (var step in def.Steps)
                {
                    if (step == null) continue;
                    if (step.Kind == RuntimeStepKind.Atomic && step.Atomic != null)
                        yield return step.Atomic;
                }
            }
            else if (def.Effects != null)
            {
                foreach (var atom in def.Effects)
                    if (atom != null) yield return atom;
            }
        }

        /// <summary>单原子计价 shim：克隆影响计价的全部效果头字段（持续/目标数量/选择模式/
        /// 触发时机/发动速度/逐原子目标制/组合域），Effects 只装该原子——推导值＝该原子在当前参数下的真实贡献。</summary>
        private static EffectDefinition PricingShim(EffectDefinition def, AtomicEffectInstance atom) =>
            new EffectDefinition
            {
                Id = "COST_PART_PREVIEW",
                DisplayName = def.DisplayName,
                ActivationType = def.ActivationType,
                TriggerTiming = def.TriggerTiming,
                BaseSpeed = def.BaseSpeed,             // 速度入价（2026-10-09）：分账与合计同乘
                Duration = def.Duration,
                DurationValue = def.DurationValue,
                SelectionMode = def.SelectionMode,
                TargetCount = def.TargetCount,
                RandomTarget = def.RandomTarget,
                TargetDomain = def.TargetDomain,
                PerAtomTargets = def.PerAtomTargets,   // 对称减半逐原子口径（2026-10-09）：分账不回落共享臂
                Effects = new List<AtomicEffectInstance> { atom },
            };

        /// <summary>原子行短标签：中文短名＋数值（有 {value} 模板才带值——同槽卡摘要口径）。</summary>
        private static string AtomPartLabel(AtomicEffectInstance atom)
        {
            var cfg = AtomicEffectTable.GetByType(atom.Type);
            if (cfg == null) return atom.Type.ToString();
            bool hasValue = (cfg.Description ?? "").Contains("{value}");
            return hasValue ? $"{cfg.DisplayName}{atom.Value}" : cfg.DisplayName;
        }

        /// <summary>费用文本化：非零色按序拼「N色」（如 2红1蓝；全零=0）。
        /// 2026-10-12 小数直通：效果级预览按真值显示小数（0.6绿/3.6红——整数不带小数位）；
        /// 卡层费用由 CardCostService 在代价抵扣前逐色向上取整（2026-11-09 定案），不经此口。</summary>
        private static string CostZh(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            var parts = new List<string>();
            foreach (var color in cost.NonzeroColors())
                parts.Add($"{cost[color]:0.##}{ColorZh(color)}");
            return string.Join("", parts);
        }

        /// <summary>对战实付文本（2026-10-30 支付进1定案）：费用经支付终点 NormalizeBill
        /// 逐色向上取整后的实际扣费账单（大于 0 也消耗 1）——与引擎实付同单源。</summary>
        private static string PayZh(ElementCost cost)
        {
            var bill = ElementPaymentValidator.NormalizeBill(cost);
            var c = new ElementCost();
            foreach (var kv in bill) c[kv.Key] = kv.Value;
            return CostZh(c);
        }

        /// <summary>光环模式顶栏费用：条目平价（Stage A 明细，AuraPreviewCard 同源——
        /// 2026-10-07 面板 aura-cost 行退役后顶栏独占；2026-10-12 删「费用（光环条目）：」
        /// 冠语——行首直入条目公式；箭头费已入条目价，预览卡携带预选箭头同计）。</summary>
        private string AuraTopCostText()
        {
            var lines = CardCore.CardCostService.Derive(AuraPreviewCard()).Breakdown
                .Where(l => l != null && l.Stage == "A").ToList();
            if (lines.Count == 0) return "";
            var parts = lines.Select(l =>
                $"{l.Label}＝{l.Value:0.#}{(l.Color.HasValue ? ColorZh(l.Color.Value) : "")}");
            return string.Join("；", parts);
        }

        private static string ColorZh(ManaType m) => m switch
        {
            ManaType.Red => "红",
            ManaType.Blue => "蓝",
            ManaType.Green => "绿",
            ManaType.Gray => "灰",
            _ => m.ToString(),
        };

        // ======================================== 整体刷新 ========================================

        private void RefreshAll()
        {
            RefreshModeBar();

            SyncActivationVisibility(); // 显隐随形态（分支/光环锁定——2026-10-03 定案）
            UpdateDeleteState();
            RefreshSlots();
            RefreshLibrary();
            RefreshName();
        }

        /// <summary>删除按钮显隐（2026-10-24 改版）：仅"从效果表载入的库效果"显示——
        /// 按钮已挪到右栏「切换效果表」旁，卡编辑/新建态直接隐藏。</summary>
        private void UpdateDeleteState()
        {
            if (_deleteBtn != null)
            {
                bool loaded = _editingCard == null && !string.IsNullOrEmpty(_loadedFromLibraryId);
                _deleteBtn.gameObject.SetActive(loaded);
                _deleteBtn.interactable = loaded;
            }
        }

        /// <summary>删除当前载入的库效果（btn-delete）：按源 id 清档后重置为新建空效果。
        /// 2026-10-16 强制删除机制：被卡引用时 TipPanel 提示+允许强制（ReferenceAudit 反查，
        /// 2026-10-18 弹窗统一）——强删后引用卡装载时该效果被跳过（卡组合界面卡池行标记⚠断链）。
        /// 不重建两下拉——防 Changed 重复订阅（Build*Dropdown 只在进屏时挂一次）。</summary>
        private void OnDeleteEffect()
        {
            if (_editingCard != null || string.IsNullOrEmpty(_loadedFromLibraryId)) return;
            var removed = _loadedFromLibraryId;
            var refs = ReferenceAudit.CardsReferencingEffect(removed);
            if (refs.Count > 0)
            {
                ConfirmForceDeleteEffectAsync(removed, refs).Forget();
                return;
            }
            DeleteEffectFromLibrary(removed, 0);
        }

        /// <summary>强删确认（TipPanel 双键）：取消/拆屏=null/否=不动。</summary>
        private async UniTaskVoid ConfirmForceDeleteEffectAsync(string removed, List<string> refs)
        {
            if (await Tips.ShowAsync(
                    $"效果 {removed}（{_graph?.name}）被 {refs.Count} 张卡引用：\n{string.Join("、", refs)}\n\n"
                    + "强制删除后这些卡的效果引用断链（卡组合界面卡池行会标记⚠，卡装载时该效果被跳过）。",
                    "强制删除", "取消") != true)
                return;
            DeleteEffectFromLibrary(removed, refs.Count);
        }

        /// <summary>保存替换旧版效果的去留询问（TipPanel 二选一，fire-and-forget）：删除旧版=引用卡
        /// 断链（同删效果口径）；保留旧版=新旧共存。null（拆屏/顶替）=按保留处理不提示——数据口径
        /// 与保留一致（旧版留存），只是没有确认 toast。</summary>
        private async UniTaskVoid HandleReplacedLegacyAsync(string oldId, List<string> refs)
        {
            var picked = await Tips.ShowAsync(
                $"旧版 {oldId} 被 {refs.Count} 张卡引用：{string.Join("、", refs)}。\n\n"
                + "「保留旧版」=新旧共存，引用卡继续用旧版；「删除旧版」=引用卡断链（装载时该效果被跳过）。",
                "删除旧版", "保留旧版");
            if (picked == true)
            {
                EffectLibrarySerializer.DeleteById(oldId);
                ShowToast($"旧版 {oldId} 已删——{refs.Count} 张卡引用断链（卡组合界面已标记⚠）");
                if (_rightMode == RightMode.Effects) RefreshEffectsList();
            }
            else if (picked == false)
                ShowToast($"已保留旧版 {oldId}（{refs.Count} 张卡仍引用旧版）");
        }

        private void DeleteEffectFromLibrary(string removed, int brokenCards)
        {
            EffectLibrarySerializer.DeleteById(removed);
            _loadedFromLibraryId = null;
            _presetSourceId = null;
                _presetSnapshotHash = null;
            _graph = CardEffectToGraph(null, out _);
            _mode = InferMode(_graph);
            _expandedAtoms.Clear();
            _expandedRewards.Clear();
            EnsureDefaultSelection();
            SyncTimingDropdown();
            SyncActivationVisibility();
            RefreshAll();
            SetRightMode(RightMode.Atoms);
            ShowToast(brokenCards > 0
                ? $"效果 {removed} 已删——{brokenCards} 张卡引用断链（卡组合界面已标记⚠）"
                : $"效果 {removed} 已从效果库删除");
        }

        private void RefreshModeBar()
        {
            if (_modeBar == null) return;
            // 不清重建：模式片按名绑定烘焙节点（mode-Parallel/FreeBranch/LimitedBranch/LinkAura/RangeAura）；
            // 芯片文案以预制体为准（2026-10-09 定案：代码不覆写 UI 设置好的文本——BindableButton 传 null
            // 跳过回写），刷新只更新态色/接线；缺节点 BindableButton 已 LogError 跳过（兜底构建全退）
            MakeModeChip(ComposeMode.Parallel,
                "并列形态：最多两个槽，各放一颗原子组成效果，两原子并列生效——要给原子挂条件或引擎请切分支形态");
            MakeModeChip(ComposeMode.FreeBranch,
                "自由分支：主干是一颗事件引擎（倒计时/拼点/运势等），事件达成时发放下方 Then 奖励——奖励在 1-6 费区间自由调整，门槛随计费推导");
            MakeModeChip(ComposeMode.LimitedBranch,
                "有限分支：一颗主干原子加一个局面门槛，当前局面达标时发放 Then 奖励——不达标不奖励也不惩罚（奖励免费）");
            MakeModeChip(ComposeMode.LinkAura,
                "连接光环：只放增益条目（属性/关键词），效果挂在连接箭头指向的单位上——箭头必选（至少一支），在光环槽底部选");
            MakeModeChip(ComposeMode.RangeAura,
                "场地光环：挂一条仪典或属性降低条目持续生效——仪典同名全局唯一；作用面随极性钉死（增益仅己方、减益仅对方），属性降低双方可叠加");

            // 卡编辑模式提示（常驻节点按名绑定——非编辑时隐藏；新建才定宽，烘焙宽以节点为准）
            bool tagExisted = _modeBar.Find("editing-tag") != null;
            var tag = UiKit.BindableLabel("editing-tag", _modeBar,
                _editingCard != null ? $"正在编辑：{_editingCard.CardName} #{_editingIndex + 1}" : "",
                UiStyle.MiniSize, UiStyle.TextHint);
            var tagNode = _modeBar.Find("editing-tag");
            if (tagNode != null) tagNode.gameObject.SetActive(_editingCard != null);
            if (!tagExisted && tag != null) UiKit.Size(tag, w: 200f);
        }

        private void MakeModeChip(ComposeMode mode, string desc)
        {
            bool active = mode == _mode;
            // label=null：不覆写预制体文本（UiKit.BindableButton text==null 跳过回写）——只换态色/挂接线
            // 形态恒可点（2026-11-06 修订：代价迁入效果级后带代价卡普增，chip 硬禁让三形态在编辑
            // 带代价卡时全锁死不可重塑——互斥改由保存闸/槽区校验拦，形态自由切换）
            var btn = UiKit.BindableButton($"mode-{mode}", _modeBar, null, () => SwitchMode(mode),
                active ? UiStyle.ChipActiveBg : UiStyle.BtnBg,
                active ? UiStyle.ChipActiveText : UiStyle.TextBody);
            UiKit.Described(btn, desc);
        }

        /// <summary>宿主（卡编辑会话）是否挂代价：效果自身 Costs 行（legacy 通道）或宿主卡卡级/效果级行。</summary>
        private bool HostHasCost()
        {
            if (_graph?.header?.Costs != null)
                foreach (var ce in _graph.header.Costs)
                    if (ce != null && ce.AllPayloads().Count > 0) return true;
            return _editingCard != null && CardHasCost(_editingCard);
        }

        /// <summary>形态切换（2026-10-23 五分类，best-effort 数据搬运）：
        /// →并列：保留前 2 原子，拆分支载荷（奖励随之丢弃）；
        /// →自由分支：保留首个引擎行（无则清空提示先选引擎主干），奖励随载荷保留；
        /// →有限分支：保留首个非引擎原子，门槛/奖励保留（引擎载荷不跟普通原子）；
        /// →连接光环：清 steps（仪典步不入）、箭头重选（光环字段只在光环形态存续）；
        /// →规则光环：清 LinkAuras/箭头，保留仪典步。切换后收起展开卡。</summary>
        private void SwitchMode(ComposeMode newMode)
        {
            if (newMode == _mode) return;
            // 形态恒可切（2026-11-06 修订：chip 硬禁与切换闸随代价迁入效果级一并退役——
            // 代价×分支/光环互斥由保存闸/槽区校验拦）
            var h = _graph.header;
            _graph.steps ??= new List<EffectStepData>();

            switch (newMode)
            {
                case ComposeMode.Parallel:
                {
                    // 保留前 2 原子，拆分支载荷（奖励随之丢弃——toast 明示）
                    var kept = new List<EffectStepData>();
                    int seen = 0;
                    foreach (var s in _graph.steps)
                    {
                        if (s?.kind == 0)
                        {
                            if (seen >= ParallelSlotCount) continue;
                            seen++;
                            if (s.atomic != null) s.atomic.branch = null;
                        }
                        kept.Add(s);
                    }
                    _graph.steps.Clear();
                    _graph.steps.AddRange(kept);
                    h.AtomicEffects = null; // 扁平/奖励通道退役——防夹带
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
                    ShowToast("切换为并列——已保留前两颗原子，分支载荷与 Then 奖励已丢弃");
                    break;
                }
                case ComposeMode.FreeBranch:
                {
                    h.AtomicEffects = null;
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
                    var engine = _graph.steps.FirstOrDefault(s => s?.kind == 0 && s.atomic != null
                        && ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(s.atomic.refId)));
                    _graph.steps.Clear();
                    if (engine != null)
                    {
                        _graph.steps.Add(new EffectStepData { kind = 0, atomic = engine.atomic });
                        ShowToast("切换为自由分支——已保留引擎主干与 Then 奖励");
                    }
                    else ShowToast("自由分支主干=引擎行——无可保留主干已清空，先到右侧选引擎主干");
                    break;
                }
                case ComposeMode.LimitedBranch:
                {
                    h.AtomicEffects = null;
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
                    int dropped = 0;
                    EffectStepData trunk = null;
                    foreach (var s in _graph.steps)
                    {
                        if (s?.kind != 0 || s.atomic == null
                            || ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(s.atomic.refId)))
                        {
                            if (s != null) dropped++;
                            continue;
                        }
                        trunk = s;
                        break;
                    }
                    _graph.steps.Clear();
                    if (trunk != null)
                    {
                        if (trunk.atomic.branch != null
                            && (BranchSettleKind)trunk.atomic.branch.settle == BranchSettleKind.Engine)
                            trunk.atomic.branch = null; // 引擎载荷不跟普通原子
                        _graph.steps.Add(trunk);
                        ShowToast(trunk.atomic.branch != null
                            ? $"切换为有限分支——主干/门槛/奖励已保留" + (dropped > 0 ? $"（丢弃 {dropped} 步）" : "")
                            : $"切换为有限分支——主干已保留，门槛未选（点门槛卡到右栏选择）" + (dropped > 0 ? $"（丢弃 {dropped} 步）" : ""));
                    }
                    else ShowToast("有限分支主干=普通原子——无可保留主干，先到右侧选一颗原子");
                    break;
                }
                case ComposeMode.LinkAura:
                {
                    // 连接光环（效果层定案）＝箭头+增益条目随效果合成；仪典步不入（切场地光环再配）；
                    // 属性降低条目属场地光环（2026-10-30 挪入）——切入即清，只留增益/关键词条目
                    h.AtomicEffects = null;
                    h.ArrowDirections = 0; // 切入箭头恒 0——连接方向的光环在光环槽底部选箭头
                    _graph.steps.Clear();
                    if (h.LinkAuras != null)
                        h.LinkAuras = h.LinkAuras.Where(a => a != null
                            && (string.IsNullOrEmpty(a.stat) || a.value > 0)).ToList();
                    ShowToast("连接光环：只放增益条目（属性/关键词）——右侧光环库点击换选（新选即替换上一个）；箭头在光环槽底部必选");
                    break;
                }
                case ComposeMode.RangeAura:
                {
                    // 场地光环＝仪典步/属性降低条目专用面：箭头清空、增益·关键词条目清空（属连接光环），
                    // 仪典步与负属性条目保留（2026-10-30 属性降低入场地光环）
                    h.AtomicEffects = null;
                    h.ArrowDirections = 0;
                    if (h.LinkAuras != null)
                        h.LinkAuras = h.LinkAuras.Where(a => a != null
                            && !string.IsNullOrEmpty(a.stat) && a.value < 0).ToList();
                    _graph.steps.RemoveAll(s => s == null || s.kind != 0 || s.atomic == null
                        || !IsRuleAuraRow(AtomicEffectTable.GetByHashId(s.atomic.refId)));
                    ShowToast("场地光环：右侧仪典库/属性降低条目点击选择（新选即替换；属性降低双方可叠加）");
                    break;
                }
            }

            // 逐原子目标制（2026-10-09）：header.TargetKinds 恒空（作用范围不再手选）——切形态清残留防夹带
            h.TargetKinds = null;

            _mode = newMode;
            _expandedAtoms.Clear();
            _expandedRewards.Clear();
            EnsureDefaultSelection();
            RefreshAll();
        }

        private AtomicEffectEntry FirstKind0Atom()
        {
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
                if (s?.kind == 0 && s.atomic != null) return s.atomic;
            return null;
        }

    }
}
