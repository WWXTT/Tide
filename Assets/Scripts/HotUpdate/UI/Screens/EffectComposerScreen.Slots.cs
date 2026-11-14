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
        // ======================================== 左：槽位区（卡片=摘要+内联编辑） ========================================

        private void RefreshSlots()
        {
            ResetGuideFrame(); // 高亮框可能挂在即将销毁的区下——先回家（EffectUI 根）防连带销毁
            ClearContent(_slotArea.Content);
            _slotZones.Clear(); // 槽区校验区随重建
            ClampSelection();
            _graph.steps ??= new List<EffectStepData>();

            switch (_mode)
            {
                case ComposeMode.Parallel:
                {
                    // 并列（2026-10-23 五分类回归纯并列）：两槽原子、无分支——分支两形态独立成 chip
                    // 减费三选一（2026-11-02）：宿主已挂代价 → 并列封锁单槽（并列添加两个效果与代价互斥；
                    // 第二槽不可选中/不可再填——已填原子卡保留删除通道可清）
                    bool costLock = HostHasCost();
                    RectTransform firstEmptySlot = null;
                    for (int i = 0; i < ParallelSlotCount; i++)
                    {
                        int idx = i;
                        bool slotBlocked = costLock && i >= 1;
                        var slot = MakeSlot($"原子槽 {i + 1}");
                        if (slot == null) continue; // 模板缺失——该槽跳过（MakeSlot 已报）
                        MarkSelected(slot, SelKind.Parallel, i);
                        // 越过空档的槽不可选（保持无空档填充）；挂代价封锁槽恒不可选
                        SelectOnClick(slot, SelKind.Parallel, i, slotBlocked || i > _graph.steps.Count);
                        if (i < _graph.steps.Count)
                        {
                            var step = _graph.steps[i];
                            if (step.kind == 0 && step.atomic != null)
                                MakeAtomCard(slot, step.atomic, idx, SelKind.Parallel);
                            else MakeReadonlyStepBadge(slot, step); // 抉择等只读
                        }
                        else
                        {
                            // 空槽占位引导（2026-10-23 改版）：空槽卡区内常驻一行「从右侧选原子填入」
                            if (i == _graph.steps.Count && !slotBlocked) firstEmptySlot ??= slot;
                            MakeHint(slot, slotBlocked ? "（已封锁——本卡挂代价，并列限单原子）" : "从右侧选原子填入");
                        }
                    }
                    if (costLock && _graph.steps.Count(s => s != null && s.kind == 0 && s.atomic != null) >= 2)
                        _slotZones.Add(MakeZone(_slotArea.Content, () =>
                            "本卡已挂代价——并列添加了两个效果时不允许再挂代价：删除第二颗原子或移除代价"));
                    if (_graph.steps.Count >= ParallelSlotCount)
                        MakeHint(_slotArea.Content, "已满 2 原子——点击槽选中后，右侧点击即替换");
                    // 校验：并列模式至少一个原子（只读抉择步计入内容——卡组成阶段产物）
                    if (firstEmptySlot != null)
                        _slotZones.Add(MakeZone(firstEmptySlot, () =>
                            !_graph.steps.Any(s => s != null && ((s.kind == 0 && s.atomic != null) || s.kind == 2))
                                ? "效果为空——并列模式至少需要一个原子（选中槽后点击右侧库行）" : null));
                    // 限重复（2026-10-09 全局唯一 → 2026-10-19 放开修订：仅引擎主干/仪典）：
                    // 库已隐不可再添，本区仅拦载入旧数据/旁路（隐藏交互、不拒绝）
                    _slotZones.Add(MakeZone(_slotArea.Content, DuplicateAtomError));
                    break;
                }
                case ComposeMode.FreeBranch:
                {
                    // 自由分支：单主干槽（引擎行）+ 槽内 Then 奖励块（引擎预设锁效果头）
                    //（模拟/变形 2026-11-08 退役出组合——挂「系统」位整库隐藏，卡直接引用+卡组合界面绑定）
                    var slot = MakeSlot("主干（引擎行）");
                    if (slot == null) break; // 模板缺失——MakeSlot 已报
                    MarkSelected(slot, SelKind.Parallel, 0);
                    SelectOnClick(slot, SelKind.Parallel, 0, false);
                    var atom = FirstKind0Atom();
                    if (atom != null)
                    {
                        MakeAtomCard(slot, atom, 0, SelKind.Parallel);
                        if (ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(atom.refId)))
                        {
                            var engineKind = ComposerCatalog.EngineKindOf(ResolveType(atom));
                            var b = atom.branch ?? new BranchEntryData { settle = (int)BranchSettleKind.Engine };
                            b.settle = (int)BranchSettleKind.Engine; // 引擎载荷落槽即挂（载入/换行双保险）
                            if (engineKind != BranchEngineKind.None) b.engine = (int)engineKind;
                            atom.branch = b;
                            if (IsCurseEngine(engineKind))
                            {
                                // 附加诅咒/祝福（2026-11-10 用户定案）：载荷=单个原子 + 主动·并列组合效果
                                // 引用双通道，可并存；张数=附加张数（每张各自完整载荷）；附加费=载荷费减半入卡价。
                                MakeHint(slot, $"{ComposerCatalog.EngineZhOf(engineKind)}：载荷=单个原子 和/或 主动·并列效果"
                                    + (engineKind == BranchEngineKind.BlessingOnDraw
                                        ? "（主动·极性+1——附加祝福给自己牌库）"
                                        : "（主动·极性−1——附加诅咒给对手牌库）")
                                    + "——原子点本槽落库，组合效果点「载荷效果」行；两者可并存；"
                                    + "附加费=载荷费减半入卡价；抽到该卡时自动结算（一次性）");
                                MakeRewardSlot(slot, b);
                                _slotZones.Add(MakeZone(slot, () => CursePayloadError(atom, engineKind)));
                            }
                            else
                            {
                                // 前 6 引擎（2026-11-10 用户定案）：奖励=单个原子，原子费×宿主时机系数 ≤6；
                                // 不允许组合效果引用。门槛=原子费×系数实时推导。
                                MakeHint(slot, $"{ComposerCatalog.EngineZhOf(engineKind)}门槛=奖励原子费×时机系数"
                                    + "（奖励=单个原子——点本槽到右栏选；原子费×宿主效果头时机系数 ≤6；"
                                    + "门槛随原子费变；同一张卡每回合只触发一次）"
                                    + $"——见顶栏预览「自由分支·{ComposerCatalog.EngineZhOf(engineKind)}x」");
                                MakeRewardSlot(slot, b);
                            }
                        }
                        else
                        {
                            atom.branch = null; // 引擎载荷只配引擎主干行（旧形状静默归位）
                            MakeHint(slot, "该行不是引擎主干——到右侧重新选择");
                        }
                    }
                    else MakeHint(slot, "主干为空——到右侧选择引擎行（倒计时/拼点/运势/死亡计数/元素充盈…）");
                    _slotZones.Add(MakeZone(slot, () =>
                    {
                        var a = FirstKind0Atom();
                        if (a == null) return "主干为空——到右侧选择引擎主干行";
                        return ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(a.refId))
                            ? null : "自由分支主干必须是引擎行——到右侧重新选择";
                    }));
                    break;
                }
                case ComposeMode.LimitedBranch:
                {
                    // 有限分支（2026-11-10 用户定案）：第一槽=1~2 个内联原子（不引用效果 ID）；
                    // 门/奖励/代价挂在**最后一个主干原子**上；第一个槽可留空第二原子=单原子形态。
                    var trunkAtoms = LimitedTrunkAtoms();
                    var branchAtom = LimitedBranchAtom();
                    for (int i = 0; i < ParallelSlotCount; i++)
                    {
                        int idx = i;
                        var slot = MakeSlot(i == 0 ? "主干（原子槽 1）" : "主干（原子槽 2，可选）");
                        if (slot == null) continue; // 模板缺失——MakeSlot 已报
                        MarkSelected(slot, SelKind.Parallel, i);
                        SelectOnClick(slot, SelKind.Parallel, i, false);
                        if (i < trunkAtoms.Count)
                        {
                            var atom = trunkAtoms[i];
                            MakeAtomCard(slot, atom, i, SelKind.Parallel);
                            if (atom.branch != null
                                && (BranchSettleKind)atom.branch.settle == BranchSettleKind.Engine
                                && (BranchEngineKind)atom.branch.engine != BranchEngineKind.DeathToll)
                                atom.branch = null; // 引擎载荷不配普通原子（DeathToll 增幅标记例外）
                            if (ReferenceEquals(atom, branchAtom))
                            {
                                MakeGateCard(slot, atom);
                                var ab = atom.branch;
                                if (ab != null && !BranchEntryRules.IsPhantom(ab)) MakeRewardSlot(slot, ab);
                            }
                        }
                        else
                        {
                            MakeHint(slot, i == 0
                                ? "主干为空——到右侧选 1~2 个原子（引擎行请切「自由分支」）"
                                : "可选第二原子——与第一原子组成并列效果；留空=单原子");
                        }
                    }
                    _slotZones.Add(MakeZone(_slotArea.Content, () =>
                    {
                        var atoms = LimitedTrunkAtoms();
                        if (atoms.Count == 0) return "主干为空——到右侧选择 1~2 个原子";
                        foreach (var a in atoms)
                            if (ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(a.refId)))
                                return "有限分支主干须为普通原子——引擎行请切「自由分支」形态";
                        if (atoms.Count > 2) return "第一槽最多 2 个原子（并列形状）";
                        return null;
                    }));
                    break;
                }
                case ComposeMode.LinkAura:
                case ComposeMode.RangeAura:
                {
                    MakeAuraPanel(_slotArea.Content);
                    break;
                }
            }

            // 设置盒已随 2026-10-09 prefab 重排拆除：发动四参数=activate-bar 静态组（Build 一次绑定），
            // 数量/随机/衍生物=每原子卡面行（MakeAtomCard/MakeRewardSlot 逐卡绑定）
            //（充能效果头钉档已随 2026-11-05 充能系统动作化改版删除——充能不再是卡上效果。）
            ApplyEngineHeaderLock(); // 引擎在案→效果头四值钉预设+四控件禁用（2026-10-09 九项定案，含载入/换引擎路径）
            RefreshName();
            ValidateZones(); // 槽区重建后立即校验
        }

        // ========================================（充能形态契约已删 2026-11-05） ========================================
        // GraphHasChargeAtom/ApplyChargeHeaderLock/ChargeShapeError 三口随「充能系统动作化」改版删除：
        // 充能不再是卡上原子效果（原子表行已删，原子库本就不列系统位行），效果合成器无充能形态可言。

        /// <summary>图内是否含引擎载荷（settle=Engine——步骤/扁平双扫）：引擎=无上限触发，
        /// 2026-11-13 收口后只有结界可挂（生物没有耐久对价）——宿主闸据此拒生物，不再豁免。</summary>
        private static bool GraphHasEnginePayload(EffectGraphData g)
        {
            if (g?.steps != null)
                foreach (var s in g.steps)
                    if (s?.atomic?.branch != null && s.atomic.branch.settle == (int)BranchSettleKind.Engine)
                        return true;
            if (g?.header?.AtomicEffects != null)
                foreach (var a in g.header.AtomicEffects)
                    if (a?.branch != null && a.branch.settle == (int)BranchSettleKind.Engine)
                        return true;
            return false;
        }

        /// <summary>宿主时机档位闸（2026-11-14 档位制）：档位单源=TimingWeights.TierOf（表 Bucket）——
        /// 生物=一回合一次+稀有两档（CreatureCanTakeTiming 单源）；结界=非生物动作族（一回合多次全开）；
        /// 法术=仅登场（一次性形态）。违规=保存禁用。效果库模式无宿主不查。
        /// 引擎载荷只有结界可挂（引擎无上限触发，生物没有耐久对价——2026-11-13 收口）。</summary>
        private string HostTimingTierError()
        {
            if (_editingCard == null) return null;
            var h = _graph?.header;
            if (h == null) return null;
            var t = (TriggerTiming)h.TriggerTiming;
            if (t == TriggerTiming.Activate_Active || t == TriggerTiming.Activate_Instant
                || t == TriggerTiming.Activate_Response)
                return null; // 主动档无时机（现付）
            string zh = TimingZh.TryGetValue(t, out var name) ? name : t.ToString();
            if (_editingCard.Supertype == Cardtype.Enchantment && EffectEligibility.IsTapAnchoredTiming(t))
                return $"本卡宿主（{_editingCard.CardName}）挂不住生物动作时机「{zh}」——攻/守/横置/重置只有生物会做"
                       + "（结界不横置、不宣言攻击）；请改一回合一次/稀有/一回合多次时机，或把卡改为生物";
            if (_editingCard.Supertype == Cardtype.Spell && t != TriggerTiming.OnPlay)
                return $"本卡宿主（{_editingCard.CardName}）是一次性法术，只能挂「登场」时机；"
                       + "请改登场，或改为生物/结界宿主";
            if (_editingCard.Supertype == Cardtype.Creature && GraphHasEnginePayload(_graph))
                return "引擎载荷只能挂结界（引擎无触发上限，结界用耐久买次数）——生物没有耐久对价；"
                       + "请把引擎效果移到结界卡，或改为普通自动效果";
            if (_editingCard.Supertype == Cardtype.Creature && TimingWeights.TierOf(t) == TimingTier.Multi)
                return $"本卡宿主（{_editingCard.CardName}）挂不住一回合多次时机「{zh}」"
                       + $"（每回合期望 {CardCore.TimingWeights.ExpectedOf(t):0.##} 次）——结界专属（耐久即次数池，每次触发 −1）；"
                       + "请改一回合一次时机（攻击/守卫/回合开始/回合结束/登场等）或稀有（被破坏/被除外/死亡等），"
                       + "或把卡改为结界配耐久";
            return null;
        }


        // ======================================== 槽位选中（右侧点击=替换选中槽） ========================================

        /// <summary>槽高亮（2026-10-05 实测修正：与展开卡一致）——展开卡所在槽即高亮；
        /// 无展开时回落选中槽（库点击落点提示）。选中态=焦点底色（Outline 描边已随闪光体系退役）。</summary>
        /// <summary>槽高亮（2026-10-06 二次修正）：颜色只跟选中槽走（单选——选别处即褪色）；
        /// 展开态独立多卡并存（不收缩），不再携带高亮。选中态=焦点底色（Outline 描边已退役）。</summary>
        private void MarkSelected(RectTransform slot, SelKind kind, int index)
        {
            bool selected = _selSlot == kind && _selSlotIndex == index;
            if (selected)
            {
                var img = slot.GetComponent<Image>();
                if (img != null) img.color = new Color(70f / 255f, 110f / 255f, 170f / 255f, 0.32f); // 焦点态底（USS .drop-slot--selected）
            }
        }

        /// <summary>槽点击=选中（整槽可点；卡片摘要点击会冒泡到槽——选中和展开并存）。</summary>
        private void SelectOnClick(RectTransform slot, SelKind kind, int index, bool disabled)
        {
            var btn = slot.gameObject.GetComponent<Button>();
            if (btn == null)
            {
                btn = slot.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = slot.GetComponent<Image>();
            }
            btn.onClick.AddListener(() =>
            {
                if (disabled) { ShowToast("请按顺序填入——先选前面的槽"); return; }
                bool already = kind == SelKind.Reward
                    ? _selSlot == SelKind.Reward && _selRewardSlot == index
                    : _selSlot == kind && _selSlotIndex == index;
                if (already) return;
                SelectSlot(kind, index);
            });
        }

        private void SelectSlot(SelKind kind, int index)
        {
            if (kind == SelKind.Reward) _selRewardSlot = index;
            else _selSlotIndex = index;
            // 2026-10-06 定案：选中槽不再收起任何展开卡（多卡独立展开）
            _selSlot = kind;
            RefreshSlots();
            RefreshRightPanels(); // 库按选中槽重过滤
        }

        /// <summary>右栏列表刷新：选中槽/门变化后按可用性重过滤。</summary>
        private void RefreshRightPanels()
        {
            RefreshLibrary();
        }

        /// <summary>进入/切模式/载入时的默认选中（2026-10-23 五分类）：并列=原子 1；分支=主干；
        /// 光环双形态=唯一光环槽（仪典入槽）。门槛选中态（Gate）只由点门槛卡进入。</summary>
        private void EnsureDefaultSelection()
        {
            _selSlot = SelKind.Parallel;
            _selSlotIndex = 0;
        }

        /// <summary>每次刷新前校正当选中槽（模式变化/删除原子/清门槛后防悬挂）；
        /// 并列槽允许 0..min(已填数,槽位数-1)（首空槽可追加）；分支形态恒主干槽，
        /// 奖励选中态须仍有分支载荷，门槛选中态（Gate）只在有限分支。</summary>
        private void ClampSelection()
        {
            // 展开态防悬挂（2026-10-06 多卡独立展开）：键=对象身份——只保留仍挂在当前 steps 上的原子/分支
            var stepsNow = _graph.steps ?? new List<EffectStepData>();
            _expandedAtoms.RemoveWhere(a => !stepsNow.Any(s => s?.kind == 0 && ReferenceEquals(s.atomic, a)));
            _expandedRewards.RemoveWhere(br => !stepsNow.Any(s => s?.kind == 0 && ReferenceEquals(s.atomic?.branch, br)));

            switch (_mode)
            {
                case ComposeMode.Parallel:
                    if (_selSlot != SelKind.Parallel) { _selSlot = SelKind.Parallel; _selSlotIndex = 0; }
                    int max = Mathf.Min(stepsNow.Count, ParallelSlotCount - 1);
                    if (_selSlotIndex > max) _selSlotIndex = max;
                    break;
                case ComposeMode.FreeBranch:
                case ComposeMode.LimitedBranch:
                    // 2026-11-10：自由分支单主干；有限分支第一槽可 1~2 原子——选中允许 0/1。
                    _selSlotIndex = _mode == ComposeMode.LimitedBranch
                        ? Mathf.Clamp(_selSlotIndex, 0, 1)
                        : 0;
                    if (_selSlot == SelKind.Reward && BranchOfSlot(0) == null) _selSlot = SelKind.Parallel;
                    if (_selSlot == SelKind.Gate && _mode != ComposeMode.LimitedBranch) _selSlot = SelKind.Parallel;
                    if (_selSlot != SelKind.Parallel && _selSlot != SelKind.Reward && _selSlot != SelKind.Gate)
                        _selSlot = SelKind.Parallel;
                    break;
                case ComposeMode.LinkAura:
                case ComposeMode.RangeAura:
                    _selSlot = SelKind.Parallel; // 光环单槽：选中恒槽 0（奖励/门槛选中态不适用）
                    _selSlotIndex = 0;
                    break;
            }
        }

        /// <summary>槽位/设置盒模板接管（2026-10-05 prefab 静态化）：prefab 在 slot-area/content 烘焙了
        /// slot 样板（slot-title + atom-card[head/editor]）与 settings 样板（row0 目标行+row1 发动行）
        /// ——Build 时分别转隐藏模板 tpl-slot 等（ClearChildren 放行 tpl-* 不销毁）；
        /// MakeSlot 每槽克隆；设置盒模板已随 2026-10-09 prefab 重排拆除（发动四参数=activate-bar 静态组）。
        /// 2026-10-06 箭头盒同批接管：烘焙 arrows 节点（head + arrow-picker 预制体实例）→ tpl-arrows，
        /// 光环形态克隆、并列形态不克隆（隐藏）。</summary>
        private void PrepareSlotTemplates()
        {
            var content = _slotArea?.Content;
            if (content == null) return;
            var tpl = content.Find("slot") as RectTransform;
            if (tpl != null)
            {
                tpl.name = "tpl-slot";
                tpl.gameObject.SetActive(false);
                // 模板内 atom-card 预先收起（克隆体由 MakeAtomCard 按需激活；editor 展开态才激活）
                var card = tpl.Find("atom-card");
                if (card != null) card.gameObject.SetActive(false);
                // 模板内 gate-card 预先收起（2026-10-30 预制体化：有限分支克隆体由 MakeGateCard 按需激活）
                var gateCard = tpl.Find("gate-card");
                if (gateCard != null) gateCard.gameObject.SetActive(false);
            }
            var rewardSlot = content.Find("reward-slot");
            if (rewardSlot != null)
            {
                // Then 奖励槽（2026-10-06 预制体化：slot-title 标题 + head 摘要 + editor 参数区）——
                // MakeRewardSlot 每槽跨级克隆；无分支不克隆（整区隐藏）
                rewardSlot.name = "tpl-reward-slot";
                rewardSlot.gameObject.SetActive(false);
            }
            var arrows = content.Find("arrows");
            if (arrows != null)
            {
                arrows.name = "tpl-arrows"; // 光环连接箭头盒（head + arrow-picker 预制体实例）
                arrows.gameObject.SetActive(false);
            }
            var auraSlot = content.Find("aura-slot") as RectTransform;
            if (auraSlot != null)
            {
                // 光环专用槽（2026-10-07 定案：光环不再与普通原子槽混用——tpl-slot 只归并列）
                auraSlot.name = "tpl-aura-slot";
                auraSlot.gameObject.SetActive(false);
                var acard = auraSlot.Find("aura-card");
                if (acard != null) acard.gameObject.SetActive(false); // 卡片按需激活（克隆体各自实例化）
            }
        }

        /// <summary>槽位克隆（2026-10-07 兜底退役）：tpl-slot 模板克隆+按名绑定；缺模板返回 null
        ///（调用方跳过该槽）、缺件 LogError——不再代码构建槽体。</summary>
        private RectTransform MakeSlot(string title)
        {
            var slot = UiKit.CloneTemplate("tpl-slot", _slotArea.Content);
            if (slot == null)
            {
                Debug.LogError("[EffectComposer] 缺 tpl-slot 槽模板（克隆失败）——检查 EffectUI.prefab");
                return null;
            }
            // 闪光提示的 Outline 已退役（违规提示=GuideFrame 遮罩框）——烘焙模板自带的 Outline 克隆时剥除
            foreach (var ol in slot.GetComponents<Outline>()) UnityEngine.Object.Destroy(ol);
            var img = slot.GetComponent<Image>();
            if (img == null) Debug.LogError("[EffectComposer] tpl-slot 根缺背景 Image——检查 EffectUI.prefab");
            else img.raycastTarget = true; // 槽整体可点（选中）
            UiKit.Size(slot, fw: 1f);
            var lbl = UiKit.FindDeep(slot, "slot-title")?.GetComponent<TMP_Text>();
            if (lbl == null) Debug.LogError("[EffectComposer] tpl-slot 缺 slot-title 文本——检查 EffectUI.prefab");
            else
            {
                lbl.text = title;
                UiKit.Size(lbl, fw: 1f);
            }
            return slot;
        }

        private static void MakeHint(RectTransform parent, string text)
        {
            var hint = UiKit.Label("hint", parent, text, RowSmallSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(hint, fw: 1f);
        }

        // 槽内原子卡：摘要行（描述+↑↓删除，点击展开）+ 展开态=内联编辑（prefab 静态模板：head/settings/editor）。
        private void MakeAtomCard(RectTransform slot, AtomicEffectEntry atom, int index, SelKind selKind)
        {
            // 展开态（2026-10-06 多卡独立展开）：键=原子对象身份——各卡互不收起
            bool expanded = _expandedAtoms.Contains(atom);

            var card = slot.Find("atom-card") as RectTransform;
            if (card == null)
            {
                Debug.LogError("[EffectComposer] 槽内缺 atom-card 模板节点（2026-10-05 prefab 静态化）——检查 EffectUI.prefab");
                return; // 兜底构建已退役（2026-10-07）——缺失即缺卡
            }
            card.gameObject.SetActive(true);

            // 展开区（editor）默认收起——展开卡在下方按需激活（设置盒=content 级单例，不随卡）
            UiKit.FindDeep(card, "editor")?.gameObject.SetActive(false);

            var head = UiKit.FindDeep(card, "head");
            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>();
            if (text == null) Debug.LogError("[EffectComposer] atom-card 缺 text 摘要文本——检查 EffectUI.prefab");
            if (text != null)
            {
                // 2026-10-24 定案：换行/溢出表现归预制体（Text Wrapping Mode 在 prefab 上改）——代码只填字
                text.text = (expanded ? "▼ " : "▶ ") + AtomText.RenderAtomEntry(atom, _graph.header)
                    + (atom?.branch != null
                       && (BranchSettleKind)atom.branch.settle == BranchSettleKind.Engine
                       && (BranchEngineKind)atom.branch.engine == BranchEngineKind.DeathToll
                        ? "［死亡计数增幅］" : "");
                UiKit.Size(text, fw: 1f);
            }

            if (head != null)
            {
                bool par = selKind == SelKind.Parallel && _mode == ComposeMode.Parallel; // 换序仅并列双槽有意义
                BindCardButton(UiKit.FindDeep(card, "up"), "↑", par, () => MoveParallel(index, -1));
                BindCardButton(UiKit.FindDeep(card, "down"), "↓", par, () => MoveParallel(index, 1));
                BindCardButton(UiKit.FindDeep(card, "del"), "删除", true, () => RemoveAtom(selKind, index));
                var headBtn = head.gameObject.GetComponent<Button>() ?? head.gameObject.AddComponent<Button>();
                headBtn.transition = Selectable.Transition.None;
                headBtn.targetGraphic = head.GetComponent<Image>();
                headBtn.onClick.RemoveAllListeners();
                headBtn.onClick.AddListener(() => ToggleExpand(atom));
            }

            // 原子表行四参数 des（2026-10-09 定案：作用范围/筛选标签/极性/费用基准——卡面常显）；
            // 传实例（2026-11-06）：组合器收窄/代价锁侧的作用范围按实例 kinds 收窄显示
            BindAtomDes(card, AtomicEffectTable.GetByHashId(atom?.refId), atom);

            // 每原子目标行/衍生物行（2026-10-09 下沉定案：placeholder=数量+随机、derivative=召唤配置）
            void RefreshCard()
            {
                if (text != null)
                    text.text = (_expandedAtoms.Contains(atom) ? "▼ " : "▶ ")
                        + AtomText.RenderAtomEntry(atom, _graph.header);
                RefreshName();
                ValidateZones();
            }
            // 模拟/变形目标数量钉 1（2026-10-30 定案：目标恒单个——数量下拉禁改；2026-11-08 变形
            // 对齐同口径——「变成{target}」恒单目标）；其余行走默认
            BindAtomTargetRow(card, atom, RefreshCard,
                pinCountOne: IsImitateEntry(atom) || IsGrantMorphEntry(atom));
            BindAtomDerivativeRow(card, atom);

            if (expanded)
                MakeAtomEditorInline(card, atom, selKind, text);
        }

        /// <summary>prefab 的 amplification Toggle（2026-11-10 用户定案）：只对带 {value} 的非引擎原子、
        /// 且无其他分支时显示。打开=DeathToll 增幅（engineParam=9=计数上限 9）；关闭=拆标记。
        /// 切换后 RefreshName 同步 lbl-effect-name / lbl-effect-cost。</summary>
        private void BindAmplificationToggle(RectTransform editor, AtomicEffectEntry atom, SelKind selKind)
        {
            var toggle = UiKit.FindDeep(editor, "amplification")?.GetComponent<Toggle>();
            if (toggle == null || atom == null)
            {
                if (toggle != null) toggle.gameObject.SetActive(false);
                return;
            }
            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            bool eligible = selKind == SelKind.Parallel && cfg != null
                            && !ComposerCatalog.IsEngineTrunkRow(cfg)
                            && !string.IsNullOrEmpty(cfg.Description) && cfg.Description.Contains("{value}");
            var branch = atom.branch;
            bool isDt = branch != null
                        && (BranchSettleKind)branch.settle == BranchSettleKind.Engine
                        && (BranchEngineKind)branch.engine == BranchEngineKind.DeathToll;
            bool hasOther = branch != null && !isDt;
            toggle.gameObject.SetActive(eligible && !hasOther);
            if (!eligible || hasOther) return;

            toggle.SetIsOnWithoutNotify(isDt && branch.engineParam == 9);
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(on =>
            {
                if (on)
                    atom.branch = new BranchEntryData
                    {
                        settle = (int)BranchSettleKind.Engine,
                        engine = (int)BranchEngineKind.DeathToll,
                        engineParam = 9, // 增幅：积累上限 9
                    };
                else
                    atom.branch = null;
                RefreshName(); // lbl-effect-name + lbl-effect-cost
                RefreshAll();
            });
        }
        /// <summary>prefab 烘焙的卡片头部小按钮绑定（组件缺失补建；visible=false 直接隐藏）。</summary>
        private static void BindCardButton(RectTransform node, string label, bool visible, Action onClick)
        {
            if (node == null) return;
            node.gameObject.SetActive(visible);
            if (!visible) return;
            var lbl = node.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = label;
            var btn = node.gameObject.GetComponent<Button>() ?? node.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = node.GetComponent<Image>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick());
        }

        // 赋予类持续行已随 2026-10-09 指示物层化定案退役（原 BindGrantDurationRow/aura-duration 行删除）：
        // 关键词/指示物持续由各自机制承载，效果级 Duration 不再编辑——保存路径哨兵清空回落 Once 锚

        /// <summary>原子内联编辑器（prefab 静态 editor 节点：field-value 输入行[内含 slider-amp]）。
        /// headLabel=卡片摘要行（值/随机改动原地重渲，不重建槽区——修复滑条拖动中失焦）。
        /// 2026-10-09 持续行/每原子目标行迁出编辑器：持续档随指示物层化退役；数量/随机/衍生物=卡面常显行。</summary>
        private void MakeAtomEditorInline(RectTransform card, AtomicEffectEntry atom, SelKind selKind, TMP_Text headLabel)
        {
            var editor = UiKit.FindDeep(card, "editor");
            if (editor == null)
            {
                Debug.LogError("[EffectComposer] atom-card 缺 editor 节点——数值/随机不可编辑");
                return;
            }
            editor.gameObject.SetActive(true);
            BindAmplificationToggle(editor, atom, selKind); // 2026-11-10：prefab 的 amplification Toggle
            // 2026-10-09 prefab 重组：editor 只剩 field-value（内含数值输入+slider-amp）——
            // 动态提示直接挂 editor 根（原 dd-trunk* 主干下拉遗留已随设置盒重排清场）
            var inject = editor;

            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type))
            {
                MakeHint(inject, $"（原子表引用缺失：{atom.refId ?? "空"}）");
                return;
            }
            var mounts = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
            bool hasValue = (cfg?.Description ?? "").Contains("{value}");

            // 原地刷新：值/随机改动只更新摘要/顶栏——不再 RefreshSlots 整区重建
            // （描述行已去重——head 摘要即 RenderAtomEntry 全量渲染）
            void RefreshTexts()
            {
                if (headLabel != null)
                    headLabel.text = "▼ " + AtomText.RenderAtomEntry(atom, _graph.header);
                RefreshName();
                ValidateZones(); // 值/随机改动可能触发预算超限——原地复验
            }

            var valueNode = UiKit.FindDeep(editor, "field-value");
            var sliderNode = UiKit.FindDeep(editor, "slider-amp");

            // ---- 关键词类原子（MountKinds 含 Keyword 位）：卡面行已承载参数——编辑器内无参数可编 ----
            if (mounts.Contains(MountKind.Keyword))
            {
                valueNode?.gameObject.SetActive(false);
                sliderNode?.gameObject.SetActive(false);
                return;
            }

            // ---- 场地光环原子（位 6 RuleAura）：Value=作用范围（RuleAuraScope）非数值——数值/随机不显示；
            // 作用面随极性钉死（2026-10-30：±1 行单侧唯一——+1 仅己方/−1 仅对方，单侧计价 ×1.5）----
            if (mounts.Contains(MountKind.RuleAura))
            {
                valueNode?.gameObject.SetActive(false);
                sliderNode?.gameObject.SetActive(false);
                MakeHint(inject, "仪典无数值参数：作用面随极性钉死——增益仪典仅己方、减益仪典仅对方（单侧档计价 ×1.5）；中性改写行三档在卡面「作用范围」行选");
                return;
            }

            // ---- Value（TMP 输入框静态绑定；无 {value} 模板则隐藏+只读说明）----
            bool showValue = hasValue;
            if (valueNode != null)
            {
                valueNode.gameObject.SetActive(showValue);
                if (showValue)
                {
                    var input = valueNode.GetComponent<TMP_InputField>()
                        ?? valueNode.GetComponentInChildren<TMP_InputField>(true);
                    if (input != null)
                    {
                        input.contentType = TMP_InputField.ContentType.IntegerNumber;
                        input.text = atom.value.ToString();
                        input.onEndEdit.RemoveAllListeners();
                        input.onEndEdit.AddListener(_ =>
                        {
                            if (int.TryParse(input.text, out var v))
                            {
                                atom.value = v;
                                RefreshTexts();
                            }
                            else input.text = atom.value.ToString();
                        });
                        UiKit.Described(input, ValueFieldDesc);
                    }
                }
            }
            if (!showValue) MakeHint(inject, "无数值参数（该原子 Value 不参与语义）");

            // ---- 数值随机滑条（uGUI Slider 静态绑定；0-100% → RandomAmplitude；
            // 2026-10-07 黑名单翻转：含 {value} 默认可随机，标「不可随机」位的行禁随机）----
            bool showAmp = hasValue && !mounts.Contains(MountKind.NoRandom);
            if (sliderNode != null)
            {
                sliderNode.gameObject.SetActive(showAmp);
                if (showAmp)
                {
                    var sl = sliderNode.GetComponent<Slider>() ?? sliderNode.GetComponentInChildren<Slider>(true);
                    if (sl != null)
                    {
                        sl.minValue = 0;
                        sl.maxValue = 100;
                        sl.wholeNumbers = true;
                        sl.SetValueWithoutNotify(Mathf.RoundToInt(atom.amp * 100f));
                        sl.onValueChanged.RemoveAllListeners();
                        sl.onValueChanged.AddListener(v =>
                        {
                            atom.amp = v / 100f;
                            RefreshTexts();
                        });
                        var slLbl = UiKit.FindDeep(sliderNode, "label")?.GetComponent<TMP_Text>();
                        if (slLbl != null) slLbl.text = "数值随机 %";
                        UiKit.Described(sl, AmpSliderDesc);
                    }
                }
            }

            // 原子级目标域下拉/目标随机已上移设置盒（2026-10-05 并列相同目标定案——作用范围升级效果级 TargetKinds）

            // 位 2：指示物持续提示（2026-10-08 层即持续定案：持续与计价均由指示物规格承载——
            //     计价=表行锚×max(1,层数)，效果级持续档不生效）
            if (mounts.Contains(MountKind.Counter))
                MakeHint(inject, "指示物原子：持续与计价均由指示物规格承载（表行锚×max(1,层数)，四分类见 Tags）——效果级持续档不生效");

            // 检索按维度档计费提示（2026-10-09 定案：宣言在对局中以文本输入——组合期不设输入口）
            if (type == AtomicEffectType.SearchDeck)
                MakeHint(inject, "检索按维度档计费：宣言在对局中以文本输入（组合期不填——空=单维度 1 档）");

            // 模拟/变形动态主干提示（2026-10-30 挪入自由分支·均仅主动）：引用卡在卡牌合成界面选
            //（选中后显示卡牌列表，不带衍生物的卡均可选）、模拟目标数量恒 1
            if (type == AtomicEffectType.Morph)
                MakeHint(inject, "模拟（仅主动）：变成场上目标单位（跳过其场地光环）——目标数量恒 1；"
                    + "发动主动效果按变形前后费差以彩元素补差");
            else if (type == AtomicEffectType.GrantMorph)
                MakeHint(inject, "变形（仅主动）：引用卡不在此填——在卡牌合成界面选择（选中后显示卡牌列表，"
                    + "不带衍生物的卡均可选）；目标数量恒 1，发动主动效果按变形前后费差以彩元素补差");
        }

    }
}
