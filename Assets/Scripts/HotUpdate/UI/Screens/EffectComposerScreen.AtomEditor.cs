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
        // ======================================== 门槛卡与分支主干（2026-10-23 五分类） ========================================

        /// <summary>分支形态主干落槽（自由=引擎行自动挂 Engine 载荷；有限=普通原子、保留既有门槛/奖励载荷
        /// ——门槛与奖励属效果不属原子，换主干原子不丢）。落位后选中停在主干槽、展开新卡。</summary>
        private void ReplaceBranchTrunk(AtomicEffectEntry entry, bool keepBranch)
        {
            _graph.steps ??= new List<EffectStepData>();
            var old = _graph.steps.Count > 0 && _graph.steps[0].kind == 0 ? _graph.steps[0].atomic : null;
            if (keepBranch && old?.branch != null) entry.branch = old.branch;
            _graph.steps.Clear();
            _graph.steps.Add(new EffectStepData { kind = 0, atomic = entry });
            _expandedAtoms.Add(entry);
            _selSlot = SelKind.Parallel;
            _selSlotIndex = 0;
            RefreshSlots();
        }

        /// <summary>有限分支主干原子列表（2026-11-10：第一槽 1~2 个内联原子；超过 2 截断）。</summary>
        private List<AtomicEffectEntry> LimitedTrunkAtoms()
            => (_graph?.steps ?? new List<EffectStepData>())
                .Where(s => s != null && s.kind == 0 && s.atomic != null)
                .Select(s => s.atomic).Take(2).ToList();

        /// <summary>有限分支门/奖励的挂载原子：优先带真实分支的原子（新数据=最后一个原子），
        /// 否则回落最后一个主干原子（空组合=null）。</summary>
        private AtomicEffectEntry LimitedBranchAtom()
        {
            var atoms = LimitedTrunkAtoms();
            for (int i = atoms.Count - 1; i >= 0; i--)
                if (atoms[i].branch != null && !BranchEntryRules.IsPhantom(atoms[i].branch))
                    return atoms[i];
            return atoms.Count > 0 ? atoms[atoms.Count - 1] : null;
        }

        /// <summary>有限分支第一槽落子（2026-11-10：索引 0/1，最多 2 原子）。落第二原子时把门/奖励
        /// 分支从旧最后原子迁到新原子；换第一原子时分支留在最后原子。</summary>
        private void ReplaceLimitedTrunk(AtomicEffectEntry entry, int slotIndex, bool keepBranch)
        {
            _graph.steps ??= new List<EffectStepData>();
            var trunks = _graph.steps.Where(s => s != null && s.kind == 0 && s.atomic != null).ToList();
            int idx = Mathf.Clamp(slotIndex, 0, 1);
            var oldLast = trunks.Count > 0 ? trunks[trunks.Count - 1].atomic : null;
            var branch = keepBranch ? oldLast?.branch : null;
            if (branch != null && BranchEntryRules.IsPhantom(branch)) branch = null;

            if (idx < trunks.Count)
                trunks[idx].atomic = entry;
            else
                _graph.steps.Add(new EffectStepData { kind = 0, atomic = entry });

            // 分支恒挂"最后一个主干原子"：先把所有真实分支摘掉，再挂到新的最后原子
            foreach (var s in _graph.steps)
                if (s?.kind == 0 && s.atomic != null && s.atomic != entry) s.atomic.branch = null;
            var atoms = LimitedTrunkAtoms();
            if (atoms.Count > 0 && branch != null) atoms[atoms.Count - 1].branch = branch;
            _expandedAtoms.Add(entry);
            _selSlot = SelKind.Parallel;
            _selSlotIndex = atoms.Count - 1;
            RefreshSlots();
        }

        /// <summary>右栏门槛行点击 → 写入 branch{settle=Gate, gateId, then=原奖励保留}——
        /// 历史 Outcome 载荷被整体换成 Gate（产出条件不再新建，2026-10-23 裁定）；
        /// 写入后选中自动切奖励块（下一步必是填奖励），右栏随 RefreshLibrary 切奖励候选。</summary>
        private void ApplySituationGate(ComposerCatalog.GateSpec gate)
        {
            var atom = LimitedBranchAtom() ?? FirstKind0Atom();
            if (atom == null) { ShowToast("先到右侧选一颗主干原子——门槛挂在主干上"); return; }
            var b = atom.branch ?? new BranchEntryData();
            b.then ??= new List<AtomicEffectEntry>();
            b.settle = (int)BranchSettleKind.Gate;
            b.gateId = gate.Id;
            b.outcomeId = null;
            b.engine = 0; b.engineParam = 0; b.condParam = 0; b.condStr = null;
            atom.branch = b;
            _selSlot = SelKind.Reward; // 门槛已选——下一步必是填奖励
            _selRewardSlot = 0;
            RefreshSlots();
            RefreshLibrary();
            ShowToast(gate.Id == ComposerCatalog.CostGateId
                ? "代价门已选——到右侧挑一个代价原子（一般效果行·双侧域·与效果同色，减免 ≤3）"
                : "门槛已选——到右侧挑一个奖励原子");
        }

        /// <summary>门槛卡（2026-10-23 五分类·有限分支；2026-10-30 预制体化）：tpl-slot 内烘焙
        /// gate-card（head/text + des），克隆 aura-card 退役——代码只填字接线，不建结构。
        /// 空态=引导点选；已选=◈ 名【奖励B】；历史 Outcome 载荷只读展示「产出条件（旧版）」。
        /// 点卡=选中门槛（右栏切 11 条门槛列表）；换选门槛即整条覆盖（prefab 未烘 del——清空口不提供）。
        /// 选中态=焦点底色（与原子槽/奖励块同款）。</summary>
        private void MakeGateCard(RectTransform slot, AtomicEffectEntry atom)
        {
            var card = slot.Find("gate-card") as RectTransform;
            if (card == null)
            {
                Debug.LogError("[EffectComposer] tpl-slot 缺 gate-card 门槛卡——检查 EffectUI.prefab");
                return;
            }
            card.gameObject.SetActive(true);

            var b = atom?.branch;
            bool phantom = b == null || BranchEntryRules.IsPhantom(b);
            bool legacyOutcome = !phantom && (BranchSettleKind)b.settle == BranchSettleKind.Outcome;
            var gate = !phantom && (BranchSettleKind)b.settle == BranchSettleKind.Gate ? GateSpecOf(b.gateId) : null;
            bool costGate = !phantom && IsCostGate(b); // 代价门（2026-11-06）：第二槽=代价原子

            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>();
            if (text == null) Debug.LogError("[EffectComposer] gate-card 缺 text 摘要文本——门槛卡不可显；检查 EffectUI.prefab");
            else
            {
                text.text = gate != null
                    ? costGate
                        ? $"◈ {gate.DisplayName}【减免≤{CostDiscountCap}】"
                        : $"◈ {gate.DisplayName}【奖励{ComposerCatalog.GateBudget(gate)}】"
                    : legacyOutcome
                        ? $"产出条件（旧版）：{ComposerCatalog.OutcomeConditions.FirstOrDefault(g => g.Id == b.outcomeId)?.DisplayName ?? b.outcomeId}"
                        : "门槛：未选——点击本卡后在右侧选择有限分支";
                UiKit.Size(text, fw: 1f);
            }
            var des = UiKit.FindDeep(card, "des")?.GetComponent<TMP_Text>();
            if (des == null) Debug.LogError("[EffectComposer] gate-card 缺 des 参数文本——门槛卡不可显；检查 EffectUI.prefab");
            else
            {
                des.text = costGate
                    ? "支付代价：使用时强制发动下方代价原子——减免等值费用（全价≤3，抵扣只作用该原子可产出色的卡费；保存回写卡级代价行）"
                    : "有限分支：达标发放下方奖励，不达标不奖励也不惩罚（奖励免费）";
                UiKit.Size(des, fw: 1f);
            }

            // 点卡=选中门槛（右栏切门槛列表）——整个卡区都可点：卡根+标题行双路接线
            //（prefab head 烘焙了自带 Button 会吃掉点击不冒泡到卡根——2026-10-23 用户反馈修正）
            void SelectGate()
            {
                if (LimitedBranchAtom() == null) { ShowToast("先选主干原子——门槛挂在主干上"); return; }
                SelectSlot(SelKind.Gate, 0);
            }
            var img = card.GetComponent<Image>();
            var btn = card.gameObject.GetComponent<Button>();
            if (btn == null)
            {
                btn = card.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(SelectGate);
            var head = UiKit.FindDeep(card, "head");
            if (head != null)
            {
                foreach (var headBtn in head.GetComponents<Button>())
                {
                    headBtn.transition = Selectable.Transition.None;
                    headBtn.onClick.RemoveAllListeners();
                    headBtn.onClick.AddListener(SelectGate);
                }
            }
            UiKit.Described(btn,
                "门槛：当前局面达标时发放下方 Then 奖励——点击本卡任意位置后到右侧点选一条局面门槛（11 条，表驱动）");
            if (_selSlot == SelKind.Gate && img != null)
            {
                img.enabled = true;
                img.color = new Color(70f / 255f, 110f / 255f, 170f / 255f, 0.32f); // 焦点态底（与槽/奖励块同款）
            }

            // 校验：门槛未选（主干空由主干区报——此处只管有主干没门槛）
            _slotZones.Add(MakeZone(card, () =>
            {
                var a = LimitedBranchAtom();
                if (a == null) return null;
                return a.branch == null || BranchEntryRules.IsPhantom(a.branch)
                    ? "门槛未选——点击门槛卡后到右侧选择有限分支" : null;
            }));
        }

        /// <summary>gateId → 目录项（局面门）。</summary>
        private static ComposerCatalog.GateSpec GateSpecOf(string gateId)
            => ComposerCatalog.SituationGates.FirstOrDefault(g => g.Id == gateId);

        /// <summary>槽内 Then 奖励槽区（2026-10-06 预制体化；2026-10-23 五分类：分支两形态共用——
        /// 自由分支=引擎载荷（free），有限分支=门槛/产出载荷）：content 烘焙 reward-slot → tpl-reward-slot
        /// 跨级克隆进槽，视觉不再代码构建——slot-title 标题行 + head 摘要行 + editor 参数区。
        /// 非自由主干（局面门/产出条件——预算=门槛 Budget/条件 2）奖励参数按预算自动生成不可控
        /// ——editor 恒隐藏、head 摘要无展开箭头（点击=选中奖励块）；
        /// 自由主干（引擎）奖励无上限引擎封顶 6——维持手控：head 点击展开 editor 编辑数值。
        /// 奖励=双通道（2026-11-07 分支奖励效果化；**2026-11-10 R4 修订**推翻 2026-11-08 阶段4
        /// 单通道——引擎分支恢复 then 原子+effectId 组合效果两通道并存，附加诅咒/祝福不经本槽）。</summary>
        private void MakeRewardSlot(RectTransform slot, BranchEntryData b)
        {
            bool free = (BranchSettleKind)b.settle == BranchSettleKind.Engine;
            bool costGate = IsCostGate(b);         // 代价门（2026-11-06）：第二槽=代价原子（减免等值费用）
            bool curse = free && IsCurseEngine((BranchEngineKind)b.engine);
            bool atomOnly = free && ComposerCatalog.IsAtomOnlyEngine((BranchEngineKind)b.engine);
            bool manualParams = free || costGate;  // 手控参数=自由主干（引擎）/代价门
            var reward = b.then?.FirstOrDefault();
            // 非自由主干：参数随预算自动生成（落槽/换门/载入统一归一）——代价门例外（减免量=代价
            // 全价口径 PayloadUnitGrant，不做预算上探；超限由 editor 回退闸与槽区校验拦）
            if (reward != null && !costGate) AutoFitRewardParams(reward, b);

            var area = UiKit.CloneTemplateFrom(_slotArea.Content, "tpl-reward-slot", slot);
            var img = area != null ? area.GetComponent<Image>() : null;
            if (area == null || img == null)
            {
                if (area != null) UnityEngine.Object.Destroy(area.gameObject); // 残缺克隆体先弃——防空行残留
                Debug.LogError("[EffectComposer] 缺 tpl-reward-slot 模板或根缺背景 Image——本槽奖励区跳过；检查 EffectUI.prefab");
                return; // 兜底构建已退役（2026-10-07）
            }
            img.raycastTarget = true; // 整区可点（选中奖励槽）
            // 防御（2026-10-06）：克隆体结构子节点强制激活——不依赖 prefab 烘焙的 active 状态
            UiKit.FindDeep(area, "slot-title")?.gameObject.SetActive(true);
            UiKit.FindDeep(area, "head")?.gameObject.SetActive(true);

            var title = UiKit.FindDeep(area, "slot-title")?.GetComponent<TMP_Text>();
            if (title == null) Debug.LogError("[EffectComposer] tpl-reward-slot 缺 slot-title——检查 EffectUI.prefab");
            else
            {
                title.text = costGate
                    ? $"代价（减免 ≤{CostDiscountCap}）"
                    : curse
                        ? "载荷（单个原子 / 主动·并列效果引用）"
                        : atomOnly
                            ? "Then 奖励（单个原子，原子费×时机系数 ≤6）"
                            : $"Then 奖励（预算 {RewardCapOf(b)} 以内）";
                UiKit.Size(title, fw: 1f);
            }

            // ---- head：奖励摘要行（空槽=填入提示）----
            bool expanded = manualParams && _expandedRewards.Contains(b);
            var head = UiKit.FindDeep(area, "head") as RectTransform;
            var text = head != null ? UiKit.FindDeep(head, "text")?.GetComponent<TMP_Text>() : null;
            if (head == null || text == null)
                Debug.LogError("[EffectComposer] tpl-reward-slot 缺 head/text 摘要行——检查 EffectUI.prefab");
            else UiKit.Size(text, fw: 1f);
            void RefreshText()
            {
                if (text != null)
                {
                    // 奖励效果引用前缀（2026-11-07 分支奖励效果化）：双通道=先效果后原子
                    //（2026-11-10 R4：引擎分支同口径——2026-11-08 阶段4 单通道摘要退役）
                    string fxPrefix = "";
                    if (curse && !string.IsNullOrEmpty(b.effectId))
                    {
                        var fxg = EffectsLibrary.Get(b.effectId);
                        fxPrefix = $"【效果】{(fxg != null ? fxg.name : b.effectId + "(悬空)")}｜";
                    }
                    if (reward != null)
                    {
                        // 代价门合成视图可含多原子（存量代价行多 payload）——首原子成句+尾注计数，
                        // 未展示原子保存时原样回写不动；代价原子走代价渲染口（资敌主语「对手…」）
                        string more = b.then != null && b.then.Count > 1 ? $" 等{b.then.Count}个原子" : "";
                        string body = costGate
                            ? AtomText.RenderCostAtomEntry(reward)
                            : AtomText.RenderRewardAtomEntry(reward);
                        text.text = (manualParams ? (expanded ? "▼ " : "▶ ") : "") + fxPrefix + body + more;
                    }
                    else
                    {
                        text.text = costGate
                            ? "选中后点击库中的代价原子填入（一般效果行·双侧域·与效果同色，全价 ≤3）"
                            : curse
                                ? fxPrefix.Length > 0
                                    ? fxPrefix + "（可再点库追加单个载荷原子——双通道并存）"
                                    : "点本槽在右栏选单个载荷原子，或点「载荷效果」行选主动·并列组合效果（两者可并存）"
                                : atomOnly
                                    ? "点本槽在右栏选单个奖励原子（原子费×宿主时机系数 ≤6）"
                                    : "选中后点击库中的奖励原子填入（单个原子，与第一槽同色且 ≤ 门预算）";
                    }
                }
                RefreshName();
                ValidateZones(); // 参数/预算变化原地复验
            }
            var delNode = head != null ? UiKit.FindDeep(head, "del") : null;
            if (delNode == null)
                Debug.LogError("[EffectComposer] tpl-reward-slot 缺 del 删除按钮——检查 EffectUI.prefab（兜底创建已退役）");
            else
            {
                BindCardButton(delNode, "删除", true, () =>
                {
                    b.then?.Clear();
                    _expandedRewards.Remove(b);
                    RefreshSlots();
                    RefreshLibrary(); // 全局唯一（隐藏口径）：奖励删除后该原子回归库
                });
                delNode.gameObject.SetActive(reward != null);
            }
            if (head != null)
            {
                var headBtn = head.gameObject.GetComponent<Button>() ?? head.gameObject.AddComponent<Button>();
                headBtn.transition = Selectable.Transition.None;
                headBtn.targetGraphic = head.GetComponent<Image>();
                headBtn.onClick.RemoveAllListeners();
                headBtn.onClick.AddListener(() =>
                {
                    if (free) ToggleRewardExpand(b);               // 自由主干=展开手控（多卡独立展开）
                    else SelectSlot(SelKind.Reward, 0);            // 非自由主干=仅选中（无原子参数可编）
                });
            }

            // ——「载荷效果」选择行（2026-11-10）：只有附加诅咒/祝福有主动·并列效果引用通道；
            // 自由分支前 6 引擎=单原子、有限分支第二槽=单原子，均不显示本行。——
            if (curse) MakeRewardFxRow(area, b, head, RefreshText);

            // 原子表行四参数 des + 每原子目标行/衍生物行（2026-10-09 下沉定案；持续行随层化退役）：
            // 仅自由主干（引擎）奖励手控参数——非自由主干参数随预算自动生成，行隐藏
            var rewardCfg = AtomicEffectTable.GetByHashId(reward?.refId);
            // 传实例（2026-11-06 代价门第二槽修复）：代价原子的锁侧镜像 kinds（如对方牌库）
            // 必须进作用范围行——此前只按表行极性收窄，代价实例锁侧被吞。
            // 双通道（2026-11-10 R4）：效果引用单通道时无原子可显示——des 行隐藏，避免残留「—」占位。
            if (reward != null) BindAtomDes(area, rewardCfg, reward);
            else UiKit.FindDeep(area, "des")?.gameObject.SetActive(false);
            if (manualParams && reward != null)
            {
                // 费用指示物（四色增减 8 行）奖励数量钉 1（2026-10-09 定案，见 BindAtomTargetRow/IsAddCostDownRow）；
                // costGate=代价门数量档剔 0（禁整体——A2 定案创作期补闸，2026-11-06）
                BindAtomTargetRow(area, reward, RefreshText, pinCountOne: IsAddCostDownRow(rewardCfg),
                    costGate: costGate);
                BindAtomDerivativeRow(area, reward);

                // 奖励发动方式（2026-10-20 定案：奖励=一般效果按自身发动方式发射，主干只管触发判定）：
                // 仅自由主干（引擎）——代价门不经此档（代价=使用时强制发动）。
                // 克隆区内现有下拉代码建（prefab 不动，同「目标选择」做法）。
                // 倒计时不建（2026-10-20 固定触发定案：主干/奖励不拆开——归零恒自动直发，tactivate 不适用）。
                var srcDd = free && (BranchEngineKind)b.engine == BranchEngineKind.Countdown
                    ? null
                    : area.GetComponentInChildren<TMP_Dropdown>(true);
                if (srcDd != null)
                {
                    var tgo = UnityEngine.Object.Instantiate(srcDd.gameObject, area, false);
                    tgo.name = "dd-tactivate";
                    var trt = (RectTransform)tgo.transform;
                    trt.SetAsLastSibling();
                    trt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, trt.rect.width + 50f);
                    BindTierDropdown(trt, new[] { -1, 2, 1 }, b.tactivate,
                        v =>
                        {
                            b.tactivate = v;
                            RefreshText();
                        },
                        "奖励发动：条件达成后奖励按此方式发射——主动=待发由你在响应窗发动（0 费上栈可连锁）；自动=达成即直发；未声明=随主干（旧口径）。",
                        labelOf: v => v == -1 ? "随主干" : v == 2 ? "主动" : "自动",
                        optionDesc: v => v == -1 ? "未声明（默认）：奖励随主干的发动方式（向后兼容旧数据）。"
                            : v == 2 ? "主动：条件达成后奖励待发，由你在响应窗中发动（0 费上栈，可被连锁）。"
                            : "自动：条件达成后奖励立即结算（不经响应窗）。");
                }
            }
            else
            {
                UiKit.FindDeep(area, "placeholder")?.gameObject.SetActive(false);
                UiKit.FindDeep(area, "derivative")?.gameObject.SetActive(false);
            }

            // ---- editor：参数区（非自由主干恒隐藏——2026-10-06 参数不可控定案）----
            var editor = UiKit.FindDeep(area, "editor") as RectTransform;
            if (editor == null) Debug.LogError("[EffectComposer] tpl-reward-slot 缺 editor 参数区——检查 EffectUI.prefab");
            else editor.gameObject.SetActive(expanded);
            if (editor != null && expanded && reward != null)
            {
                var cfg = AtomicEffectTable.GetByHashId(reward.refId);
                bool hasValue = (cfg?.Description ?? "").Contains("{value}");
                var valueNode = UiKit.FindDeep(editor, "field-value");
                if (valueNode != null)
                {
                    valueNode.gameObject.SetActive(hasValue);
                    if (hasValue)
                    {
                        var input = valueNode.GetComponent<TMP_InputField>()
                            ?? valueNode.GetComponentInChildren<TMP_InputField>(true);
                        if (input != null)
                        {
                            input.contentType = TMP_InputField.ContentType.IntegerNumber;
                            input.text = reward.value.ToString();
                            input.onEndEdit.RemoveAllListeners();
                            input.onEndEdit.AddListener(_ =>
                            {
                                if (!int.TryParse(input.text, out var v))
                                {
                                    input.text = reward.value.ToString();
                                    return;
                                }
                                int prev = reward.value;
                                reward.value = v;
                                if (costGate)
                                {
                                    // 代价门全价上限（≤CostDiscountCap）——超限回退上一合法值
                                    //（同卡组合 BindCostGenerate 口径；全价=逐原子 PayloadUnitGrant）
                                    float pts = 0f;
                                    foreach (var a in b.then ?? new List<AtomicEffectEntry>()) pts += CostAtomPrice(a);
                                    if (pts > CostDiscountCap)
                                    {
                                        reward.value = prev;
                                        input.text = prev.ToString();
                                        ShowToast($"代价减免 {pts:0.#} 点超上限（≤{CostDiscountCap}）——参数已回退");
                                        return;
                                    }
                                }
                                RefreshText();
                            });
                            UiKit.Described(input, ValueFieldDesc);
                        }
                    }
                }
                if (!hasValue) MakeHint(editor, "该原子无可调参数（无数值模板）——行为由描述与目标域决定");
            }

            // 选中态高亮（2026-10-09：选中色改焦点蓝与原子槽一致（MarkSelected 同色）——只跟奖励槽选中走，选别处即褪色；编辑器展开不携带色）
            if (_selSlot == SelKind.Reward && _selRewardSlot == 0)
            {
                img.enabled = true; // 防御：组件禁用态强制开启——选中色必可见
                img.color = new Color(70f / 255f, 110f / 255f, 170f / 255f, 0.32f);
            }

            // 整区点击=选中该奖励块（head 有自己的按钮——其余区域冒泡到本区）
            var btn = area.gameObject.GetComponent<Button>();
            if (btn == null)
            {
                btn = area.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
            }
            btn.onClick.AddListener(() => SelectSlot(SelKind.Reward, 0));

            // 校验：分支未设奖励（converter 折叠为无分支——条件空转）+ 可实现费用内。
            // 2026-10-09 定案：上限统一走 RewardFilterCap——自由分支（引擎）恒 6（2026-11-02 减费三选一：
            // 1-5 放宽一档；此前 -1 直通=「校验恒满足」根因）；有限分支=门槛 Budget（全局上界 1~3）。
            // 2026-11-07 分支奖励效果化：奖励效果引用费并入合计（双通道至少其一非空即不空转）。
            // **2026-11-10 R4**：引擎分支恢复双通道——then/effectId 至少一个（2026-11-08 阶段4 的
            // 「引擎分支不再支持 Then 原子」红框退役）；奖励资格走 R4 单谓词 BranchRewardRejectReason
            //（与装载期 ValidateAtomParams 同口径）；引用费超上限照旧拦。超限=GuideFrame 红框+禁存。
            _slotZones.Add(MakeZone(area, () =>
            {
                if (costGate)
                {
                    if (b.then == null || b.then.Count == 0)
                        return "代价未填——点本区选中后到右栏挑一个代价原子（一般效果行·双侧域·与效果同色，减免 ≤3）";
                    // 代价门全价=逐原子 PayloadUnitGrant（与卡级代价行 CostRowPrice 同锚）；
                    // 存量代价行未改动（与合成快照一致）豁免——绕过UI数据可超上限，零漂移往返
                    if (CostGateUnchanged(b.then)) return null;
                    float pts = 0f;
                    foreach (var a in b.then) pts += CostAtomPrice(a);
                    return pts > CostDiscountCap
                        ? $"代价减免 {pts:0.#} 点超上限（≤{CostDiscountCap}）——调低数值或换原子"
                        : null;
                }

                if (curse)
                {
                    // 双通道：原子 Then（≤1）+ 主动·并列效果引用（branch.effectId / legacy atom.str）
                    var err = CursePayloadError(FirstKind0Atom(), (BranchEngineKind)b.engine);
                    if (err != null) return err;
                    if (b.then != null && b.then.Count > 1)
                        return "载荷原子超过 1 个——附加诅咒/祝福只收单个原子（并列组合请用效果引用）";
                    return null;
                }

                if (!string.IsNullOrEmpty(b.effectId))
                    return atomOnly
                        ? "前 6 引擎奖励只收单个原子——删除 effectId（组合效果请用并列/有限分支/光环）"
                        : "有限分支第二槽只收原子——组合效果请放进第一槽或改用自由分支";

                if (b.then == null || b.then.Count == 0)
                    return atomOnly
                        ? "奖励未填——点本区选中后到右栏挑一个奖励原子（原子费×宿主时机系数 ≤6）"
                        : "奖励未填——点本区选中后到右栏挑一个奖励原子（与第一槽同色，≤门槛预算）";
                if (b.then.Count > 1)
                    return "奖励原子超过 1 个——本槽只收单个原子";

                if (atomOnly)
                {
                    var inst = CardEffectConverter.ConvertAtomForUI(b.then[0]);
                    var insts = inst == null ? new List<AtomicEffectInstance>() : new List<AtomicEffectInstance> { inst };
                    var timing = (TriggerTiming)_graph.header.TriggerTiming;
                    float cost = CostDerivationService.EngineRewardCost(insts, timing);
                    if (cost > FreeBranchFeeCap)
                        return $"奖励超出 6 费上限（{cost:0.#}/6，基础原子费）——换更便宜的原子";
                    int engineCap = ComposerCatalog.EngineThresholdCap((BranchEngineKind)b.engine);
                    if (engineCap > 0)
                    {
                        int x = CostDerivationService.EngineRewardThreshold(insts, timing);
                        if (x > engineCap)
                            return $"{ComposerCatalog.EngineZhOf((BranchEngineKind)b.engine)}门槛 x={x} 超过引擎自身上限 {engineCap}"
                                   + "——条件永远不可达（死档）；请降奖励数值或换更便宜的原子";
                    }
                    return null;
                }

                if (_mode == ComposeMode.LimitedBranch)
                {
                    var cfg = AtomicEffectTable.GetByHashId(b.then[0].refId);
                    if (!RewardColorAllowed(new LibPayload(default, cfg), CostGateColors()))
                    {
                        var colors = CostGateColors();
                        return "奖励原子与第一槽不同色——只能加第一槽包含的颜色（当前："
                               + (colors.Count == 0 ? "无彩" : string.Join("/", colors.Select(ManaTypeNames.ZhNameOf))) + "）";
                    }
                }
                int cap = RewardCapOf(b);
                float rewardCost = RewardCost(b.then[0]);
                return rewardCost > cap
                    ? $"奖励超出门槛预算（{rewardCost:0.#}/{cap}）——换更便宜的原子"
                    : null;
            }));
            RefreshText();
        }

        /// <summary>非自由主干（局面门/产出条件，2026-10-06 定案）奖励参数自动生成——玩家不可控：
        /// 该类奖励预算恒 1/2/3，数值=推导费不超预算的最大整数（锚价随值线性单调，自 1 上探；零锚价行回落 1）、
        /// 随机幅度清零。渲染期统一归一（落槽/换门缩预算/载入旧数据三路同口）。
        /// 自由主干（引擎）预算可达 9/无上限——维持手控，不经此函数。</summary>
        private static void AutoFitRewardParams(AtomicEffectEntry reward, BranchEntryData b)
        {
            if (reward == null || b == null) return;
            if ((BranchSettleKind)b.settle == BranchSettleKind.Engine) return;
            int budget = BranchBudget(b);
            if (budget < 0) return;
            reward.amp = 0f; // 随机幅度=参数之一——自动生成恒 0
            var cfg = AtomicEffectTable.GetByHashId(reward.refId);
            if (cfg == null || !(cfg.Description ?? "").Contains("{value}")) return;
            reward.value = 1;
            if (RewardCost(reward) <= 0f) return; // 零锚价行——数值不参与预算，维持默认 1
            int best = 1;
            for (int v = 2; v <= 99; v++)
            {
                reward.value = v;
                if (RewardCost(reward) > budget) break;
                best = v;
            }
            reward.value = best;
        }

        private static void MakeReadonlyStepBadge(RectTransform slot, EffectStepData step)
        {
            var badge = UiKit.Column("readonly", slot, spacing: 2f, pad: 6f);
            var bg = badge.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.RoundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = UiStyle.PanelBg;
            UiKit.Size(badge, fw: 1f);
            var text = UiKit.Label("text", badge, step.kind == 2
                ? $"抉择步骤（kind=2）——2026-10-31 P12 已退役：法术多主动效果在施放时择一，此遗留数据不生效"
                : $"步骤 kind={step.kind}（只读）",
                RowSmallSize, UiStyle.TextBody, wrap: true);
            UiKit.Size(text, fw: 1f);
        }

        // ---- 分支载荷工具（2026-10-05 两槽定案；2026-10-23 五分类：分支恒挂首主干） ----

        private BranchEntryData BranchOfSlot(int slotIndex)
        {
            // 2026-11-10：有限分支的门/奖励恒挂"最后一个主干原子"（第一槽可 1~2 原子）；
            // 其余形态仍按槽位索引取。
            if (_mode == ComposeMode.LimitedBranch) return LimitedBranchAtom()?.branch;
            var steps = _graph.steps ?? new List<EffectStepData>();
            if (slotIndex < 0 || slotIndex >= steps.Count) return null;
            var s = steps[slotIndex];
            return s?.kind == 0 ? s.atomic?.branch : null;
        }

        /// <summary>分支奖励预算（两槽定案）：Gate=门预算（表 Budget 列——SituationGates/BuildGatePremium 同源）；
        /// Outcome=产出条件预算；Engine=引擎预算（2026-10-09 自平衡统一：全引擎 -1 无上限——门槛与预算同源于 Then 锚价推导）。</summary>
        private static int BranchBudget(BranchEntryData b)
        {
            if (b == null) return -1;
            switch ((BranchSettleKind)b.settle)
            {
                case BranchSettleKind.Gate:
                    var gate = GateSpecOf(b.gateId);
                    return gate != null ? ComposerCatalog.GateBudget(gate) : -1;
                case BranchSettleKind.Outcome:
                    var oc = ComposerCatalog.OutcomeConditions.FirstOrDefault(g => g.Id == b.outcomeId);
                    return oc != null ? ComposerCatalog.GateBudget(oc) : -1;
                case BranchSettleKind.Engine:
                    return ComposerCatalog.EngineRewardBudget((BranchEngineKind)b.engine, b.engineParam);
                default:
                    return -1;
            }
        }

        /// <summary>奖励可实现费用上限（2026-10-09 定案·四口共用；2026-11-02 减费三选一改版）：
        /// 有限分支=门预算（表 Budget 列 1/2；DrawnInStandbyThisTurn=3）钳全局上界 1~3；
        /// 产出条件=条件预算（恒 2）同钳；自由分支=引擎分支恒 6（原 5 同批放宽一档——引擎预算全 -1，
        /// 玩家在 1-6 费区间自由调奖励、门槛随计费推导）
        /// ——库展示、落槽校验、槽区保存校验（超限禁存）与费用预览分支段共用一口。</summary>
        private static int RewardFilterCap(BranchEntryData b)
        {
            int budget = BranchBudget(b);
            if (budget >= 0) return Mathf.Min(budget, LimitedBranchFeeCap);
            return FreeBranchFeeCap;
        }

        /// <summary>预言族奖励预算覆盖（2026-11-02 押注行收紧定案）：预言分支（ProphecyHit/Miss·Outcome）
        /// 的 Then 锚价上限=预言押注行（ProphecyNextCard）的表行 Budget（现值 3——类别三选一押注；
        /// 2026-10-29 旧口径「类别 ≤1、卡名 ≤3」随卡名档退役作废）；无押注行的手写数据按 1
        /// 收口。其余分支照旧 RewardFilterCap。</summary>
        private int RewardCapOf(BranchEntryData b)
        {
            int cap = RewardFilterCap(b);
            if (b == null || (BranchSettleKind)b.settle != BranchSettleKind.Outcome
                || (b.outcomeId != "ProphecyHit" && b.outcomeId != "ProphecyMiss"))
                return cap;
            if (_graph?.steps != null)
                foreach (var s in _graph.steps)
                {
                    if (s?.kind != 0 || s.atomic == null) continue;
                    var cfg = AtomicEffectTable.GetByHashId(s.atomic.refId);
                    if (cfg?.EnumName != nameof(AtomicEffectType.ProphecyNextCard)) continue;
                    return cfg.Budget > 0 ? cfg.Budget : 3;
                }
            return 1;
        }

        /// <summary>奖励原子是否过筛选上限（value=1 锚价口径=库行费用方格同源；转换失败=不过）。
        /// 2026-10-06 修正：推导价与表价（ManaList=行方格展示价）取大比对——检索行宣言名空时推导价
        /// 落单维度档 1 而表价 3（ExactCard 档），旧口径曾致 3 费行漏进 ≤2 的门预算筛选
        /// （库展示与落槽校验共用本口，一并修复）。</summary>
        private static bool RewardWithinFilterCap(AtomicEffectEntry entry, int cap)
        {
            var inst = CardEffectConverter.ConvertAtomForUI(entry);
            if (inst == null) return false;
            float derived = CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { inst });
            float declared = AtomicEffectTable.GetByHashId(entry?.refId)?.TotalUnitCost ?? 0f;
            return Mathf.Max(derived, declared) <= cap;
        }

        /// <summary>有限分支第二槽同色过滤（2026-11-10）：无色/纯灰原子放行；有彩原子须落在
        /// 第一槽颜色并集内（与代价同色口径同源）。</summary>
        private static bool RewardColorAllowed(LibPayload lp, HashSet<ManaType> colors)
        {
            var mana = lp?.Cfg?.ManaList;
            if (mana == null) return true; // 零费/无表价行=无色
            bool hasColor = false;
            foreach (var c in mana.NonzeroColors())
                if (c != ManaType.Gray && c != ManaType.Any && mana[c] > 0f) { hasColor = true; break; }
            if (!hasColor) return true;
            return CardCostService.PayloadColorAllowed(mana, colors);
        }

        /// <summary>自由分支前 6 引擎奖励过滤（2026-11-10）：原子费 × 宿主效果头时机系数 ≤6。
        /// 表价也按同一系数放大后取大值，防「推导价≤6 但表价过 6」漏进。</summary>
        private bool EngineRewardWithinCap(AtomicEffectEntry entry, BranchEntryData b)
        {
            if (b == null || !ComposerCatalog.IsAtomOnlyEngine((BranchEngineKind)b.engine)) return false;
            var inst = CardEffectConverter.ConvertAtomForUI(entry);
            if (inst == null) return false;
            var timing = (TriggerTiming)(_graph?.header?.TriggerTiming ?? (int)TriggerTiming.Activate_Active);
            var insts = new List<AtomicEffectInstance> { inst };
            float derived = CostDerivationService.RewardDerivedCost(insts); // 引擎奖励不乘时机系数
            float declared = AtomicEffectTable.GetByHashId(entry?.refId)?.TotalUnitCost ?? 0f;
            if (Mathf.Max(derived, declared) > FreeBranchFeeCap) return false;
            int engineCap = ComposerCatalog.EngineThresholdCap((BranchEngineKind)b.engine);
            if (engineCap > 0 && CostDerivationService.EngineRewardThreshold(insts, timing) > engineCap)
                return false;
            return true;
        }

        /// <summary>原子引用 → 枚举（行缺失回退 DealDamage——门行兜底口径同旧）。</summary>
        private static AtomicEffectType ResolveType(AtomicEffectEntry a)
        {
            var row = AtomicEffectTable.GetByHashId(a?.refId);
            return row != null && Enum.TryParse<AtomicEffectType>(row.EnumName, out var t)
                ? t : AtomicEffectType.DealDamage;
        }

        /// <summary>槽内原子卡展开切换（2026-10-06 定案：多卡独立展开——点击其他卡不再收起已展开卡；
        /// 键=原子对象身份，换序/删除免下标维护，悬挂条目由 ClampSelection 渲染前剪除）。</summary>
        private void ToggleExpand(AtomicEffectEntry atom)
        {
            if (atom == null) return;
            if (!_expandedAtoms.Remove(atom)) _expandedAtoms.Add(atom);
            RefreshSlots();
        }

        /// <summary>槽内 Then 奖励 editor 展开切换（仅自由主干可展开；键=槽分支载荷）。</summary>
        private void ToggleRewardExpand(BranchEntryData b)
        {
            if (b == null) return;
            if (!_expandedRewards.Remove(b)) _expandedRewards.Add(b);
            RefreshSlots();
        }

        // ---- 并列槽操作 ----
        private void MoveParallel(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= _graph.steps.Count || target < 0 || target >= _graph.steps.Count) return;
            (_graph.steps[index], _graph.steps[target]) = (_graph.steps[target], _graph.steps[index]);
            // 展开键=对象身份——换序不动展开态（2026-10-06 多卡独立展开）
            if (_selSlot == SelKind.Parallel && _selSlotIndex == index) _selSlotIndex = target;
            else if (_selSlot == SelKind.Parallel && _selSlotIndex == target) _selSlotIndex = index;
            RefreshSlots();
        }

        private void RemoveAtom(SelKind kind, int index)
        {
            switch (kind)
            {
                case SelKind.Parallel:
                {
                    var removed = _graph.steps[index]?.atomic;
                    var branch = removed?.branch;
                    _expandedAtoms.Remove(removed); // 悬挂条目 ClampSelection 兜底剪除
                    _graph.steps.RemoveAt(index);
                    // 有限分支：删掉带分支的原子时，把门/奖励迁到新的最后原子（保持"挂最后原子"）
                    if (_mode == ComposeMode.LimitedBranch && branch != null)
                    {
                        var atoms = LimitedTrunkAtoms();
                        if (atoms.Count > 0) atoms[atoms.Count - 1].branch = branch;
                    }
                    break;
                }
                case SelKind.Reward:
                    var br = BranchOfSlot(index);
                    br?.then?.Clear();
                    if (br != null) _expandedRewards.Remove(br);
                    break;
            }
            RefreshSlots();
            RefreshLibrary(); // 全局唯一（隐藏口径）：删除后原子回归库
        }

        // ---- 落槽资格与写入（点击替换选中槽） ----

        // 并列主干槽可落：主动位原子∪引擎主干行，且非错边（内容契约：效果区禁错边——错边只能进代价槽）
        private bool ParallelCanDrop(object payload)
            => payload is LibPayload lp && lp.CanBeTrunk && !lp.IsWrongSideOnly;

        // 槽内 Then 奖励可落：开放奖励挂载位+非错边+可实现费用上限内（两槽定案；2026-10-09 上限口径
        // ——产出条件=条件预算，自由分支=引擎预算·无上限封顶 5）；
        // 代价门（2026-11-06）：候选口径=CostGateCandidate（一般效果行·双侧域）。
        private bool RewardCanDrop(object payload, BranchEntryData b)
        {
            if (!(payload is LibPayload lp)) return false;
            if (IsCostGate(b)) return CostGateCandidate(lp, CostGateColors());
            if (!lp.CanBeBranchReward || lp.IsWrongSideOnly) return false;
            if (b != null && IsCurseEngine((BranchEngineKind)b.engine)) return true; // 原子载荷无 6 费帽
            if (b != null && ComposerCatalog.IsAtomOnlyEngine((BranchEngineKind)b.engine))
                return EngineRewardWithinCap(lp.Entry(), b);
            if (_mode == ComposeMode.LimitedBranch)
                return RewardColorAllowed(lp, CostGateColors())
                       && RewardWithinFilterCap(lp.Entry(), RewardCapOf(b));
            return RewardWithinFilterCap(lp.Entry(), RewardCapOf(b));
        }

        /// <summary>槽内 Then 奖励落槽（两槽定案）：写入该槽 branch.then（单奖励槽）。
        /// 2026-10-06 定案：非自由主干奖励参数按预算自动生成不可控——落槽不展开（所属原子展开关闭）；
        /// 自由主干（引擎）维持展开手控。参数归一在 MakeRewardSlot 渲染期 AutoFitRewardParams 统一执行。
        /// 代价门（2026-11-06）：条目按代价域收窄锁侧（PayloadCostDomain——同 FillPayloadCost 口径），
        /// 落槽即展开手控参数（value 可编·超3回退），不经 AutoFit。</summary>
        private void DropBranchReward(LibPayload lp, int slotIndex)
        {
            var b = BranchOfSlot(slotIndex);
            if (b == null) return;
            var entry = EntryForEffectSlot(lp);
            bool free = (BranchSettleKind)b.settle == BranchSettleKind.Engine;
            bool costGate = IsCostGate(b);
            if (costGate)
            {
                // 2026-11-06 同色定案（同日基准收口=效果主干色）：落槽防御（候选已过滤，旁路/旧数据兜底）——异色代价拒收
                if (!CostGateCandidate(lp, CostGateColors()))
                {
                    var colors = CostGateColors();
                    ShowToast("代价只能添加与效果同色的原子（当前效果色："
                              + (colors.Count == 0 ? "无彩" : string.Join("/", colors.Select(ManaTypeNames.ZhNameOf))) + "）");
                    return;
                }
                var (kinds, _) = CostDerivationService.PayloadCostDomain(lp.Cfg);
                if (kinds != null) entry.kinds = kinds; // p<0 锁己方·p>0 锁对方——代价域收窄/镜像
            }
            b.then = new List<AtomicEffectEntry> { entry };
            // 前 6 引擎=原子单通道：清掉任何残留效果引用（旧数据/切引擎）
            if (free && ComposerCatalog.IsAtomOnlyEngine((BranchEngineKind)b.engine)) b.effectId = null;
            if (free || costGate) _expandedRewards.Add(b); // 自由主干/代价门=落槽展开手控；其余不展开（参数自动生成）
            _selSlot = SelKind.Reward;
            _selRewardSlot = slotIndex;
            RefreshSlots();
        }

        // ---- 全局唯一规则（2026-10-09 定案·隐藏口径）+ 衍生物行 ----

        /// <summary>组合内限重复行已用 refId 集（主干槽+Then 奖励——与 DuplicateAtomError 同口径）。
        /// 2026-10-19 放开定案后仅引擎主干/仪典行（ComposerCatalog.DuplicateRestricted）用过即隐
        /// （不拒绝），换掉/删除后随 RefreshLibrary 回归；其余行可重复不隐藏。</summary>
        private HashSet<string> RestrictedUsedRefIds()
        {
            var set = new HashSet<string>();
            void Add(string refId)
            {
                if (string.IsNullOrEmpty(refId)) return;
                if (ComposerCatalog.DuplicateRestricted(AtomicEffectTable.GetByHashId(refId))) set.Add(refId);
            }
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                Add(s.atomic.refId);
                foreach (var r in s.atomic.branch?.then ?? new List<AtomicEffectEntry>())
                    if (r != null) Add(r.refId);
            }
            return set;
        }

        /// <summary>校验区口径的限重复检查（null=合规）：库隐藏口径下正常组合不会产生重复
        /// ——本区仅拦载入的旧数据/旁路落位（报错时保存禁用，删其一即解）。
        /// 2026-10-19 放开定案：仅引擎主干/仪典行限重复，其余行可重复。</summary>
        private string DuplicateAtomError()
        {
            var seen = new HashSet<string>();
            string dupName = null;
            void Check(AtomicEffectEntry a)
            {
                if (a == null || !string.IsNullOrEmpty(dupName) || string.IsNullOrEmpty(a.refId)) return;
                if (!ComposerCatalog.DuplicateRestricted(AtomicEffectTable.GetByHashId(a.refId))) return;
                if (!seen.Add(a.refId))
                    dupName = AtomicEffectTable.GetByHashId(a.refId)?.DisplayName ?? a.refId;
            }
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                Check(s.atomic);
                foreach (var r in s.atomic.branch?.then ?? new List<AtomicEffectEntry>()) Check(r);
            }
            return dupName == null ? null
                : $"重复效果「{dupName}」——引擎主干/仪典在一个组合中只能出现一次（并列槽与奖励均计入）";
        }

        private static bool IsSummonTokenEntry(AtomicEffectEntry a)
            => AtomicEffectTable.GetByHashId(a?.refId)?.EnumName == nameof(AtomicEffectType.SummonToken);

        /// <summary>变形原子（原变形赋予，2026-10-30 用户改名）：与召唤衍生物共用「引用卡ID」输入行
        ///（2026-10-30 起效果组合期不再填——卡组构筑期绑定，行隐藏）。</summary>
        private static bool IsGrantMorphEntry(AtomicEffectEntry a)
            => AtomicEffectTable.GetByHashId(a?.refId)?.EnumName == nameof(AtomicEffectType.GrantMorph);

        /// <summary>模拟原子（原临摹，2026-10-30 用户改名·挪入自由分支）：目标数量固定 1——数量档禁改。</summary>
        private static bool IsImitateEntry(AtomicEffectEntry a)
            => AtomicEffectTable.GetByHashId(a?.refId)?.EnumName == nameof(AtomicEffectType.Morph);

        /// <summary>宣言编码行（2026-10-29 阶段3c 信息轴；2026-10-30 族改版后四行共用——
        /// 箭头宣言退役；2026-10-31 窃取入伙=五行）：derivative 输入行写 str（"维度:值"，
        /// ProphecyDimension 编码；输入域按行分化，见 DeclarationStrDomainHint）。</summary>
        private static bool IsDeclarationStrEntry(AtomicEffectEntry a)
        {
            var name = AtomicEffectTable.GetByHashId(a?.refId)?.EnumName;
            return name == nameof(AtomicEffectType.DeclareHand)
                   || name == nameof(AtomicEffectType.DeclareDeck)
                   || name == nameof(AtomicEffectType.DeclareDeckTop)
                   || name == nameof(AtomicEffectType.ProphecyNextCard)
                   || name == nameof(AtomicEffectType.RevealByDimension);
        }

        /// <summary>宣言编码输入域提示（2026-10-30 族改版：按行分化——强夺/窃取只收卡名、
        /// 预言两行只收类别（2026-11-02 押注行同收紧）、筛选展示维度全量）。</summary>
        private static string DeclarationStrDomainHint(AtomicEffectEntry a)
        {
            var name = AtomicEffectTable.GetByHashId(a?.refId)?.EnumName;
            if (name == nameof(AtomicEffectType.DeclareHand))
                return "宣言编码：Name:卡名（如 Name:青眼白龙——强夺宣言卡名，从对手手牌获得该卡；"
                       + "运行时只收卡名档，其他编码作未命中）";
            if (name == nameof(AtomicEffectType.DeclareDeck))
                return "宣言编码：Name:卡名（如 Name:青眼白龙——窃取宣言卡名，从对手牌库获得该卡；"
                       + "运行时只收卡名档，其他编码作未命中）";
            if (name == nameof(AtomicEffectType.DeclareDeckTop))
                return "宣言编码：Type:Creature / Type:Spell / Type:Enchantment（3 选 1——隐秘宣言类别，"
                       + "对手下回合首张同类卡发动无效；运行时只收类别档）";
            if (name == nameof(AtomicEffectType.ProphecyNextCard))
                return "宣言编码：Type:Creature / Type:Spell / Type:Enchantment（3 选 1——押注对手下回合"
                       + "首张出牌类别，命中触发奖励·预算 3；运行时只收类别档，战斗期弹类别三选一）";
            return "宣言编码（维度:值）：Type:Creature|Spell|Enchantment / Name:卡名 / Color:Red|Blue|Green|Gray / "
                   + "CostParity:Odd|Even / CostExact:0-7（筛选展示维度——运行时按档两段反问）";
        }

    }
}
