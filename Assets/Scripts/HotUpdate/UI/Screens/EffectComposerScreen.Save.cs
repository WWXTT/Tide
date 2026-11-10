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
        // ======================================== 校验区（不合规=区域闪烁红框+提示，保存禁用） ========================================

        /// <summary>注册一个校验区（Zone=区域元素；Check 返回 null=合规，否则=错误提示文本）。</summary>
        private ZoneEntry MakeZone(RectTransform zone, Func<string> check)
            => new ZoneEntry { Zone = zone, Check = check };

        /// <summary>引导高亮框外扩边距（px）——框比区域大一圈，视觉上"框住"目标
        ///（描边只有内半段可渲染——外半段无片元，pad 一并补偿）。</summary>
        private const float ZoneFramePad = 6f;

        /// <summary>取引导高亮框（prefab 烘焙 guide-frame——EffectUI 根下带 Image；2026-10-07 兜底退役：
        /// 缺节点 LogError 一次并返回 null——校验红框不可用。材质共享，shader v2 自推导几何不接收 C# 尺寸）。</summary>
        private Image GuideFrame()
        {
            if (_guideFrame != null) return _guideFrame;
            if (_guideFrameMissing) return null;
            var node = UiKit.FindDeep(Root, "guide-frame");
            var img = node != null ? node.GetComponent<Image>() : null;
            if (img == null)
            {
                _guideFrameMissing = true; // 只报一次——防校验高频刷新刷屏
                Debug.LogError("[EffectComposer] 缺 guide-frame 高亮框（根下带 Image）——校验红框不可用；检查 EffectUI.prefab");
                return null;
            }
            if (img.material == null) img.material = UiKit.GuideFrameMaterial();
            img.raycastTarget = false;
            img.enabled = true; // 防御：prefab 烘焙/历史状态可能禁用组件——每次取用强制可见
            img.color = UiStyle.ErrorRed; // shader 顶点色相乘（shader 缺失回退材质时=静态红块）
            _guideFrame = img;
            return img;
        }

        private bool _guideFrameMissing;

        /// <summary>高亮框聚焦某区：临时挂到该区下、锚点拉伸外扩 pad（矩形由变换系统实时跟随父级）；
        /// ignoreLayout 防布局流排、SetAsLastSibling 顶层。</summary>
        private void FocusGuideFrame(RectTransform zone)
        {
            var img = GuideFrame();
            if (img == null) return;
            var rt = (RectTransform)img.transform;
            if (rt.parent != zone) rt.SetParent(zone, false);
            (rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-ZoneFramePad, -ZoneFramePad);
            rt.offsetMax = new Vector2(ZoneFramePad, ZoneFramePad);
            rt.SetAsLastSibling();
            rt.gameObject.SetActive(true);
        }

        /// <summary>高亮框回家（EffectUI 根下末位）并隐藏——无违规/槽区重建前调用，
        /// 防止挂在即将销毁的区下随之消失。</summary>
        private void ResetGuideFrame()
        {
            if (_guideFrame == null) return;
            var rt = (RectTransform)_guideFrame.transform;
            rt.SetParent(Root, false);
            rt.SetAsLastSibling();
            rt.gameObject.SetActive(false);
        }

        /// <summary>奖励原子推导费（校验/预算行共用口径）。</summary>
        private static float RewardCost(AtomicEffectEntry entry)
        {
            var inst = CardEffectConverter.ConvertAtomForUI(entry);
            return inst != null
                ? CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { inst }) : 0f;
        }

        /// <summary>统一校验收口（2026-10-05 改版：区内红字 zone-error 删除）：违规区域 GuideFrame 遮罩高亮
        /// + 错误文本上描述条 lbl-prop-desc（多区违规以「；」相连；转合规只清一次——不误清控件功能描述）；
        /// 存在任何违规 → 保存按钮禁用。返回 true=全部合规。</summary>
        private bool ValidateZones()
        {
            // 第一遍：跑 Check 判定（数据驱动——与矩形无关）
            RectTransform focus = null; // 单框焦点：首个违规区（全部错误文本见描述条）
            var errs = new List<string>();
            foreach (var z in _slotZones)
            {
                if (z?.Zone == null) continue;
                string err = null;
                try { err = z.Check?.Invoke(); } catch { /* 校验异常按合规处理——不阻塞编辑 */ }
                z.Invalid = !string.IsNullOrEmpty(err);
                if (z.Invalid) { errs.Add(err); focus ??= z.Zone; }
            }

            //（充能形态校验已随 2026-11-05 充能系统动作化改版删除——充能不再是卡上效果。）

            // 宿主时机档位闸（2026-11-05 两档定案；2026-11-09 生物专属档拆分）：
            // 生物=单次+攻/守/横置（生物专属 ×1.5）、结界=单次+周期/观察族、法术=仅单次——违规=保存禁用
            //（卡编辑模式兜载入旧数据/直改 JSON；效果库模式无宿主不查）
            string timingTierErr = HostTimingTierError();
            if (timingTierErr != null) errs.Add(timingTierErr);

            // 高亮框：违规→临时挂到首个违规区下拉伸填充；全合规→回 EffectUI 根下隐藏
            if (focus != null) FocusGuideFrame(focus);
            else ResetGuideFrame();

            bool any = errs.Count > 0;
            if (any)
            {
                _zoneErrShown = true;
                SetDescStrip(string.Join("；", errs));
            }
            else if (_zoneErrShown)
            {
                _zoneErrShown = false;
                SetDescStrip("");
            }
            // 保存显隐（2026-10-24 改版）：按钮在左栏组合侧——校验全合规才出现（出现即可点；
            // 空组合本就被「效果为空/主干为空」红框拦下，天然涵盖"必要填充完成"口径）
            if (_saveBtn != null) _saveBtn.gameObject.SetActive(!any);
            return !any;
        }

        // ======================================== 保存 ========================================

        private void OnSave()
        {
            // 校验兜底：违规时保存按钮已禁用——此处防御性复查
            if (!ValidateZones()) { ShowToast("存在不合规设置——红框区域按提示调整后再保存"); return; }

            // 效果名=顶栏预览同源串（CurrentDisplayName 单源化）；只读展示，保存时落账
            _graph.name = CurrentDisplayName();
            _graph.header.DisplayName = _graph.name;
            if (IsAuraMode(_mode))
            {
                // 空条目剔除（stat/keyword 双空=无效——与装载期过滤同口径）
                _graph.header.LinkAuras?.RemoveAll(a =>
                    a == null || (string.IsNullOrEmpty(a.stat) && string.IsNullOrEmpty(a.keyword)));
            }

            // 代价门反向回写（2026-11-08 代价效果级化）：哨兵分支不落 Steps——then 原样回写本效果
            // header.Costs（克隆原行保留 CostType/ManaType 等字段只换 payloads；删光代价原子=清空）。
            // 卡编辑/库模式同链：卡路径经 BuildCardEffect header 往返挂回效果；库路径经 EffectSlim
            // costs 落库（payloads 多原子承载）。数据层闸=EffectCostConflictOf 逐效果口径，
            // 存量未改动（与合成快照一致）豁免、新改动强校验，违规回滚禁存。
            List<AtomicEffectEntry> costPayloads = null;
            EffectStepData costStep = null;
            BranchEntryData costBranch = null;
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic?.branch == null || !IsCostGate(s.atomic.branch)) continue;
                costStep = s;
                costBranch = s.atomic.branch;
                costPayloads = costBranch.then;
                s.atomic.branch = null; // 剥除——代价门永不进 Steps
                break;
            }
            if (costPayloads != null)
            {
                var originalCosts = _graph.header.Costs;
                if (costPayloads.Count > 0)
                {
                    var originalRow = originalCosts != null && originalCosts.Count > 0 ? originalCosts[0] : null;
                    var row = originalRow != null // 克隆回滚源——不动原行对象
                        ? JsonUtility.FromJson<CostEntry>(JsonUtility.ToJson(originalRow))
                        : new CostEntry { CostType = (int)CostType.Payload, Value = 1 };
                    row.payloads = costPayloads;
                    row.payload = null; // 新写恒 payloads（legacy 单原子口退役）
                    _graph.header.Costs = new List<CostEntry> { row };
                }
                else _graph.header.Costs = null;
                if (!CostGateUnchanged(costPayloads))
                {
                    var costConflict = EffectCostConflictOf(_graph.header);
                    if (costConflict != null)
                    {
                        _graph.header.Costs = originalCosts; // 回滚——保持进入本屏时的效果状态
                        costStep.atomic.branch = costBranch; // 代价门视图恢复
                        ShowToast(costConflict);
                        return;
                    }
                }
            }

            // 卡编辑模式：写回卡牌并返回（不落效果库文件）
            if (_editingCard != null)
            {
                _editingCard.Effects ??= new List<CardEffectData>();
                var effect = BuildCardEffect();
                if (_editingIndex >= 0 && _editingIndex < _editingCard.Effects.Count)
                    _editingCard.Effects[_editingIndex] = effect;
                else
                    _editingCard.Effects.Add(effect);
                // 光环上移效果层：效果携带的箭头/条目并集入卡面（重算式聚合），计价缓存随之失效
                _editingCard.AggregateEffectAuras(true);
                _editingCard.ResetCache();
                _editingCard = null;
                _editingIndex = -1;
                Manager.Back();
                return;
            }

            // 效果库模式：清旧档只认「从效果表载入」的源 id（编辑换内容=换 id，旧档被新版替代）。
            // 2026-10-09 修复：此前保存后把 _graph.id 回填为源 id——下一次原位改组别的效果再保存
            // 会把上一条独立库效果误删（1点伤害↔1点穿透互相覆盖根因）。删毕即闭链，不再重新武装。
            // 2026-10-16 强制删除机制：旧版仍被卡引用时弹窗二选一——「保留旧版」新旧共存（引用卡
            // 不受影响）/「删除旧版」引用卡断链（同删效果口径）。
            // 预设效果只读载入（2026-10-28 定案）：与载入快照同内容（无文名指纹）→拦存，
            // 防把预设原样拷进主表造语义重复条目；内容有变=新 id 自然落主表为独立新效果
            bool fromPreset = !string.IsNullOrEmpty(_presetSourceId);
            if (fromPreset && PresetContentHash(_graph) == _presetSnapshotHash)
            {
                ShowToast("内容未修改——预设效果只读；调整内容后再保存会另存为新效果");
                return;
            }

            var path = EffectLibrarySerializer.Save(_graph); // Save 内计算/回填 graph.id
            string replacedId = null;
            if (!string.IsNullOrEmpty(_loadedFromLibraryId) && _loadedFromLibraryId != _graph.id)
            {
                replacedId = _loadedFromLibraryId;
                var refs = ReferenceAudit.CardsReferencingEffect(replacedId);
                if (refs.Count > 0)
                {
                    // 2026-10-18 弹窗统一：TipPanel 二选一，fire-and-forget——保存流程不被弹窗阻塞
                    //（同旧 ConfirmBox 回调时序：此处立即闭链继续保存，旧版去留弹窗后置处理）。
                    HandleReplacedLegacyAsync(replacedId, refs).Forget();
                    _loadedFromLibraryId = null; // 闭链；旧版去留交给弹窗（本保存提示走共存口径）
                    replacedId = null;
                }
                else
                {
                    EffectLibrarySerializer.DeleteById(replacedId);
                    _loadedFromLibraryId = null;
                }
            }
            if (path != null) TutorialCreationFlow.NotifyEffectsChanged(); // 第三课走查步检测（未开课零行为）
            if (fromPreset && path != null)
            {
                // 首次另存成功：预设只读态闭链——已落玩家库的新条目转为常规库条目（后续可改可删）
                _loadedFromLibraryId = _graph.id;
                _presetSourceId = null;
                _presetSnapshotHash = null;
                ShowToast($"已另存为新效果：{_graph.name}（{_graph.id}）——原预设不变");
                if (_rightMode == RightMode.Effects) RefreshEffectsList();
                UpdateDeleteState();
                if (_returnToCardComposer) // 卡组合「编辑」跳入：保存成功即回跳（新条目已在玩家库可引用）
                {
                    _returnToCardComposer = false;
                    Manager.Back();
                }
                return;
            }
            ShowToast(path == null ? "保存失败"
                : replacedId != null ? $"已保存：{_graph.name}（{_graph.id}）——旧版 {replacedId} 已被替代"
                : $"已保存：{_graph.name}（{_graph.id}）");
            if (_rightMode == RightMode.Effects) RefreshEffectsList();
            UpdateDeleteState(); // 清旧档闭链后 _loadedFromLibraryId 可能为 null——删除按钮同步回落禁用
            if (path != null && _returnToCardComposer)
            {
                _returnToCardComposer = false;
                Manager.Back(); // 卡组合「编辑」跳入：保存成功即回跳
            }
        }

        // ======================================== 卡编辑往返（深拷贝） ========================================

        /// <summary>卡内效果 → 编辑图（JsonUtility 往返深拷贝 + 载入归一化——两槽定案：
        /// 遗留 kind=1 门步骤折入槽级 branch 载荷，UI 恒载荷形态）。note=归一化提示（null=无变动）。</summary>
        private static EffectGraphData CardEffectToGraph(CardEffectData effect, out string note)
        {
            note = null;
            if (effect == null) return new EffectGraphData("新效果");
            var header = JsonUtility.FromJson<CardEffectData>(JsonUtility.ToJson(effect));
            // AnchorCost 回拷（2026-11-06 合计-0 修复·上游）：ElementCost.v 是 public readonly float[]——
            // JsonUtility 不序列化 readonly 字段，往返即丢（BuildCardEffect 同修）；钉死/非钉死都回拷
            if (effect.AnchorCost != null) header.AnchorCost = effect.AnchorCost.Clone();
            var steps = effect.Steps != null
                ? JsonUtility.FromJson<ListStepWrap>(JsonUtility.ToJson(new ListStepWrap { items = effect.Steps })).items
                : new List<EffectStepData>();
            var graph = new EffectGraphData(string.IsNullOrEmpty(effect.DisplayName) ? "新效果" : effect.DisplayName)
            {
                header = header,
                steps = steps ?? new List<EffectStepData>(),
            };
            // 2026-11-10：诅咒/祝福 legacy atom.str（效果 id）归一进 branch.effectId——新「载荷效果」行
            // 读 branch.effectId；保留 atom.str 供 converter 旧通道兜底。
            foreach (var s in graph.steps)
            {
                var a = s?.kind == 0 ? s.atomic : null;
                var b = a?.branch;
                if (b == null || b.settle != (int)BranchSettleKind.Engine) continue;
                var ek = (BranchEngineKind)b.engine;
                if (!ComposerCatalog.IsCursePayloadEngine(ek)) continue;
                if (string.IsNullOrEmpty(b.effectId) && !string.IsNullOrEmpty(a.str)) b.effectId = a.str;
            }
            note = NormalizeLoadedGraph(graph);
            return graph;
        }

        /// <summary>代价门·载入合成快照（JsonUtility 序列化的 then 列表）：null=本次未合成（手选
        /// 代价门/非卡编辑态）。内容与快照一致=存量代价行未改动——保存与槽区校验跳过 ≤3 上限闸
        ///（存量绕过UI代价可超上限——"以现有数据为准"，零漂移往返；新改动才强校验）。</summary>
        private string _costGateOriginalJson;

        /// <summary>代价门内容是否与载入合成快照一致（未改动的存量代价行）。</summary>
        private bool CostGateUnchanged(List<AtomicEffectEntry> then)
            => _costGateOriginalJson != null && then != null
               && JsonUtility.ToJson(new ListAtomWrap { items = then }) == _costGateOriginalJson;

        /// <summary>代价门·展示期合成（2026-11-08 代价效果级化修订）：载入后把本效果代价行
        /// （header.Costs 首个有原子行）的 payloads 深拷贝合成为单主干原子的「支付代价」门分支
        ///（settle=Gate + CostGateId 哨兵）——InferMode 落有限分支按代价门渲染；保存时 OnSave
        /// 反向回写 header.Costs 并剥除哨兵（永不落盘，运行时零改）。卡编辑/库载入（含预设只读）
        /// 同链——每个效果各自合成自己的代价行；保留限制：单主干·无已有分支·非光环形态
        ///（其余形态代价不进本编辑器）。</summary>
        private void SynthesizeCostGateView()
        {
            var payloads = _graph?.header?.Costs?.FirstOrDefault(
                r => r != null && r.AllPayloads().Count > 0)?.AllPayloads();
            if (payloads == null || payloads.Count == 0) return;
            if (IsAuraMode(InferMode(_graph))) return;
            var steps = _graph.steps ?? new List<EffectStepData>();
            var trunks = steps.Where(s => s?.kind == 0 && s.atomic != null).ToList();
            // 2026-11-10：有限分支第一槽可 1~2 原子；代价门挂最后一个原子。
            if (trunks.Count < 1 || trunks.Count > 2) return;
            if (trunks.Any(t => t.atomic.branch != null)) return; // 已有分支（普通门）不承载代价
            var last = trunks[trunks.Count - 1].atomic;
            var copy = JsonUtility.FromJson<ListAtomWrap>(
                JsonUtility.ToJson(new ListAtomWrap { items = payloads })).items;
            last.branch = new BranchEntryData
            {
                settle = (int)BranchSettleKind.Gate,
                gateId = ComposerCatalog.CostGateId,
                then = copy ?? new List<AtomicEffectEntry>(),
            };
            // 未改动快照（保存/槽区校验的零漂移豁免依据）
            _costGateOriginalJson = JsonUtility.ToJson(
                new ListAtomWrap { items = last.branch.then });
        }

        [Serializable]
        private sealed class ListAtomWrap { public List<AtomicEffectEntry> items; }

        /// <summary>幽灵分支剥离（递归进 branch.then 奖励原子）——JsonUtility 深拷贝/落盘物化的
        /// settle=0 空对象清 null；真分支（settle∈{Gate,Outcome,Engine}）不动。</summary>
        private static void StripPhantomBranch(AtomicEffectEntry a)
        {
            if (a == null || a.branch == null) return;
            if (BranchEntryRules.IsPhantom(a.branch)) { a.branch = null; return; }
            if (a.branch.then != null)
                foreach (var r in a.branch.then) StripPhantomBranch(r);
        }

        /// <summary>载入归一化（2026-10-05 两槽定案）：①扁平 AtomicEffects（Steps 空）展开为槽位；
        /// ②遗留 kind=1 门步骤折入前一个 kind=0 原子的 branch 载荷——产出条件族（IsOutcomeCondition）
        /// → settle=2/outcomeId；非产出条件剔除；thenSteps→then；
        /// elseSteps 丢弃（条件不达成不发奖励）；落单门步剔除。③主干>2 截断保留前 2。
        /// ④幽灵分支剥离（2026-10-09）——JsonUtility 深拷贝/落盘物化的 settle=0 空分支对象统一清 null
        /// ⑤并列/分支互斥归一（2026-10-09）——并列图两槽齐时槽级 branch 载荷拆除。
        /// header.AtomicEffects 恒清（扁平/奖励通道退役——防夹带）。返回提示文本（null=无变动）。</summary>
        private static string NormalizeLoadedGraph(EffectGraphData g)
        {
            var notes = new List<string>();
            var steps = g.steps ??= new List<EffectStepData>();

            if (steps.Count == 0 && g.header.AtomicEffects != null && g.header.AtomicEffects.Count > 0)
            {
                foreach (var a in g.header.AtomicEffects)
                    if (a != null) steps.Add(new EffectStepData { kind = 0, atomic = a });
                notes.Add("扁平原子已展开为槽位");
            }
            g.header.AtomicEffects = null;

            // 幽灵分支剥离（2026-10-09）：与效果库读档 EffectSlim.ToEntry 同口径——此处收卡编辑深拷贝
            // 与夹带数据；步骤原子/遗留门奖励原子递归清（branch.then 奖励原子顺带剥）
            foreach (var s in steps)
            {
                if (s == null) continue;
                if (s.atomic != null) StripPhantomBranch(s.atomic);
                if (s.thenSteps != null) foreach (var r in s.thenSteps) if (r != null) StripPhantomBranch(r);
                if (s.elseSteps != null) foreach (var r in s.elseSteps) if (r != null) StripPhantomBranch(r);
            }

            // 旧→新迁移（2026-10-08 条目级作用面）：效果级 AuraScope 盖戳到 scope==0 的条目，
            // header 恒清零（作用面编辑在条目卡 actuating-range——保存侧不再写非零）
            if (g.header.AuraScope != 0)
            {
                int stamped = 0;
                foreach (var a in g.header.LinkAuras ?? new List<LinkAuraData>())
                    if (a != null && a.scope == 0) { a.scope = g.header.AuraScope; stamped++; }
                g.header.AuraScope = 0;
                if (stamped > 0) notes.Add($"光环作用面已迁至条目卡（{stamped} 条盖戳）");
            }

            // 遗留 kind=1 门步骤折叠（同 converter FoldBranchSteps 口径）：产出条件归 Outcome；
            // 非产出条件——剔除并记数
            var folded = new List<EffectStepData>();
            EffectStepData prev = null;
            int orphanGates = 0, droppedElse = 0, droppedForeign = 0;
            foreach (var s in steps)
            {
                if (s == null) continue;
                if (s.kind == 1)
                {
                    if (prev == null) { orphanGates++; continue; }
                    if (s.elseSteps != null && s.elseSteps.Count > 0) droppedElse++;
                    if (!ComposerCatalog.IsOutcomeCondition(s.conditionId)) { droppedForeign++; continue; }
                    var b = prev.atomic.branch ?? new BranchEntryData();
                    b.settle = (int)BranchSettleKind.Outcome;
                    b.outcomeId = s.conditionId;
                    b.condParam = s.conditionParam;
                    b.condStr = s.conditionStringParam;
                    b.then = s.thenSteps != null
                        ? new List<AtomicEffectEntry>(s.thenSteps) : new List<AtomicEffectEntry>();
                    prev.atomic.branch = b;
                    continue;
                }
                folded.Add(s);
                prev = s.kind == 0 && s.atomic != null ? s : null;
            }
            steps.Clear();
            steps.AddRange(folded);
            if (orphanGates > 0) notes.Add($"{orphanGates} 个无前置主干的门步骤已剔除");
            if (droppedElse > 0) notes.Add($"{droppedElse} 个分支的 else 奖励已丢弃（条件不达成不发奖励）");
            if (droppedForeign > 0) notes.Add($"{droppedForeign} 个非产出条件分支已剔除");

            // 主干>2 截断（两槽定案：截断显示前 2 + 告警）——仅并列形态；
            // 光环图（箭头/条目/规则光环步）不占两槽，规则仪典原子可多行，不截断
            bool auraGraph = g.header.ArrowDirections != 0
                || (g.header.LinkAuras != null && g.header.LinkAuras.Count > 0)
                || HasRuleAuraStep(g);
            int trunks = steps.Count(s => s?.kind == 0);
            if (!auraGraph && trunks > ParallelSlotCount)
            {
                var kept = new List<EffectStepData>();
                int seen = 0;
                foreach (var s in steps)
                {
                    if (s?.kind == 0)
                    {
                        if (seen >= ParallelSlotCount) continue;
                        seen++;
                    }
                    kept.Add(s);
                }
                steps.Clear();
                steps.AddRange(kept);
                notes.Add($"主干超两槽——已截断保留前 {ParallelSlotCount}（丢弃 {trunks - ParallelSlotCount} 个）");
            }

            // 2026-11-10 用户定案：有限分支第一槽可 1~2 原子，门/奖励挂最后一个原子——
            // 两原子 + 分支是合法形态，不再执行"并列两槽拆分支"归一（旧口径已废）。
            return notes.Count > 0 ? string.Join("；", notes) : null;
        }

        /// <summary>编辑图 → 卡内效果（编排字段全量拷贝）。两槽定案：保存只写 kind=0（含 branch 载荷）
        /// /kind=2 步骤；AtomicEffects=ProjectLinear 扁平投影（converter Steps 非空走 Steps——投影为兜底）。
        /// 光环字段只在光环形态存续——其余形态防夹带清空。</summary>
        private CardEffectData BuildCardEffect()
        {
            var effect = JsonUtility.FromJson<CardEffectData>(JsonUtility.ToJson(_graph.header));
            // AnchorCost 回拷（2026-11-06 合计-0 修复）：ElementCost.v 是 public readonly float[]——
            // JsonUtility 不序列化 readonly 字段，往返即丢（钉死标记 AnchorCostPinned 是 int 存活）→
            // 转换器按「钉死+空值=0费」直出 0（顶栏合计 0/徽标费用组空的根因）。钉死/非钉死两态都回拷。
            if (_graph.header != null && _graph.header.AnchorCost != null)
                effect.AnchorCost = _graph.header.AnchorCost.Clone();
            effect.DisplayName = _graph.name;
            // 防御：UI 恒载荷形态（载入已归一化）——此处仍滤掉任何残留 kind=1 门步骤与空步
            var saved = (_graph.steps ?? new List<EffectStepData>())
                .Where(s => s != null && s.kind != 1).ToList();
            // 形态防御（2026-10-23 五分类）：并列=纯两原子（分支载荷一律拆）；连接光环=steps 恒空
            //（条目在 LinkAuras）；规则光环=只留仪典步（条目/杂步属其他形态）
            if (_mode == ComposeMode.Parallel)
                foreach (var s in saved)
                    if (s?.kind == 0 && s.atomic != null) s.atomic.branch = null;
            else if (_mode == ComposeMode.LinkAura)
                saved.RemoveAll(s => s.kind == 0);
            else if (_mode == ComposeMode.RangeAura)
                saved.RemoveAll(s => s.kind == 0
                    && !IsRitualStepRow(AtomicEffectTable.GetByHashId(s.atomic?.refId)));
            effect.Steps = saved.Count > 0
                ? JsonUtility.FromJson<ListStepWrap>(JsonUtility.ToJson(new ListStepWrap { items = saved })).items
                : null;
            // 扁平投影（converter 双通道兼容——Steps 非空走 Steps）
            effect.AtomicEffects = ProjectLinear(saved);
            // 逐原子目标制（2026-10-09）：header.TargetKinds 恒空防夹带（作用范围不再手选——各原子自身域独立解析）
            effect.TargetKinds = null;
            // 目标数量档 1/2/3（全部档 2026-10-09 退役，AOE 另出专用原子行）：存量 0/-1 归一未声明
            //（converter 回落 1）——效果级值此后仅作未声明原子的回落，编辑口在每原子卡面行
            if (effect.TargetCount == 0 || effect.TargetCount == -1) effect.TargetCount = -2;
            // 持续行退役（2026-10-09 指示物层化定案）：UI 不再编辑持续档——哨兵清空回落 Once 锚（1.0×）
            effect.Duration = -1;
            effect.DurationValue = 0;
            if (!IsAuraMode(_mode))
            {
                effect.ArrowDirections = 0;
                effect.LinkAuras = null;
            }
            else if (_mode == ComposeMode.RangeAura || HasRuleAuraStep(_graph))
            {
                effect.ArrowDirections = 0; // 规则光环/规则级光环不占连接位（UI 已关箭头——写盘一致）
            }
            return effect;
        }

        [Serializable]
        private sealed class ListStepWrap { public List<EffectStepData> items; }

        /// <summary>只读预览 def（2026-11-10 R4 阶段3）：BuildCardEffect 克隆 → 剥代价门哨兵并把
        /// payloads 合成**临时** Costs（镜像 OnSave 回写——净费/代价组预览=保存落库后的真实账单），
        /// → ConvertOne。全程只动克隆对象，不写回 _graph.header.Costs / steps（落盘是 OnSave 的职责）。
        /// 代价门空（未填）时 Costs 同步清空——预览与保存形态一致。</summary>
        private EffectDefinition PreviewEffectWithCosts()
        {
            var effect = BuildCardEffect();
            if (effect == null) return null;
            foreach (var s in effect.Steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic?.branch == null || !IsCostGate(s.atomic.branch)) continue;
                if (s.atomic.branch.then != null && s.atomic.branch.then.Count > 0)
                {
                    var originalRow = effect.Costs != null && effect.Costs.Count > 0 ? effect.Costs[0] : null;
                    var row = originalRow != null // 克隆保留 CostType/ManaType 等字段只换 payloads（同 OnSave）
                        ? JsonUtility.FromJson<CostEntry>(JsonUtility.ToJson(originalRow))
                        : new CostEntry { CostType = (int)CostType.Payload, Value = 1 };
                    row.payloads = s.atomic.branch.then;
                    row.payload = null;
                    effect.Costs = new List<CostEntry> { row };
                }
                else effect.Costs = null;
                s.atomic.branch = null; // 哨兵剥除——预览不携带视图合成态
                break;
            }
            return CardEffectConverter.ConvertOne(effect, "COMPOSER_COST_PREVIEW");
        }

        /// <summary>扁平投影：kind=0 原子 + 其槽级 branch.then 奖励 + 遗留 kind=1 门奖励。</summary>
        private static List<AtomicEffectEntry> ProjectLinear(List<EffectStepData> steps)
        {
            var flat = new List<AtomicEffectEntry>();
            if (steps == null) return flat;
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.kind == 0 && step.atomic != null)
                {
                    flat.Add(step.atomic);
                    if (step.atomic.branch?.then != null) flat.AddRange(step.atomic.branch.then);
                }
                else if (step.kind == 1 && step.thenSteps != null) flat.AddRange(step.thenSteps);
            }
            return flat;
        }

        // ======================================== 小工具 ========================================

        private static Color DotColor(UIColor color)
        {
            return color switch
            {
                UIColor.Red => UiStyle.DotRed,
                UIColor.Blue => UiStyle.DotBlue,
                UIColor.Green => UiStyle.DotGreen,
                _ => UiStyle.DotGray, // Gray/All（白/黑已随 2026-10-29 P4 色序清尾删除）
            };
        }

        private void ShowToast(string message)
        {
            _descStripNode.GetComponentInChildren<TMP_Text>().text = message;
        }

        /// <summary>清空容器（先摘父再 Destroy，防 Destroy 延迟导致的同帧占位）；
        /// 不激活的隐藏模板保留（tpl-* 前缀，或名为 row 的不激活模板——动态行的克隆源，
        /// 2026-10-03 模板约定；代码建的行恒为激活态，不误伤）。</summary>
        private static void ClearChildren(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (!child.gameObject.activeSelf
                    && (child.name.StartsWith("tpl-") || child.name == "row")) continue;
                child.SetParent(null);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static void ClearContent(RectTransform content) => ClearChildren(content);

        // ======================================== 库行负载（表行 ↔ 落槽资格的唯一桥） ========================================

        /// <summary>
        /// 原子库行负载：表行 + 枚举。落槽资格全部经此判（MountKinds 位）——
        /// 点击（QuickAdd）消费，保证口径唯一。
        /// </summary>
        public sealed class LibPayload
        {
            public readonly AtomicEffectType Type;
            public readonly AtomicEffectConfig Cfg;
            public LibPayload(AtomicEffectType type, AtomicEffectConfig cfg) { Type = type; Cfg = cfg; }

            /// <summary>预览默认值（新原子 Value=1）。</summary>
            public int PreviewValue => 1;

            // 2026-10-05 两槽定案：IsEngineTrunk/CanBeGateTrunk/CanBeGrantReward 已随
            // ComposerCatalog 旧 API（IsEngineTrunk/CanBeGateTrunk）与 Grant 引擎删除——
            // 引擎主干不再是原子（表内引擎行已删），主干/奖励资格只看挂载位。
            // 2026-10-05 晚间回表定案：引擎主干行回表（位 5）——填槽即自由分支。

            private HashSet<MountKind> Mounts => MountKindExtensions.ParseCsv(Cfg?.MountKinds ?? "");

            /// <summary>关键词类原子（位 0 Keyword）——库行点击同关键词面板：恒为他人赋予形态
            ///（kinds/domain 不写=行默认域 + str=关键词 id）。</summary>
            public bool IsKeywordAtom => Cfg != null && Mounts.Contains(MountKind.Keyword);

            /// <summary>指示物类原子（位 1 Counter，2026-10-07 与关键词同型）：落槽=赋予形态
            ///（kinds/domain 不写=行默认域按极性定侧；持续由 CounterSpec 承载，无关键词 id/头部惯例）。</summary>
            public bool IsCounterAtom => Cfg != null && Mounts.Contains(MountKind.Counter);

            /// <summary>引擎主干行（位 5）：填入槽即自由分支（引擎参数+奖励槽直出）；
            /// 仅进第一槽（2026-10-09 互斥——分支与第二槽不可兼得）；零锚价零域，不参与主序列执行。</summary>
            public bool IsEngineTrunk => Mounts.Contains(MountKind.EngineTrunk);

            /// <summary>主干槽可落（2026-10-07 位 0 删除后派生，单一来源=ComposerCatalog.CanBeTrunkRow）：
            /// 非规则光环即可（守护/坚韧已先后退出可挂面——配对制/指示物化）；引擎行经位 5 走主干槽，
            /// 系统/攻守行有资格但被库过滤（IsLockedKeywordRow）隐藏。</summary>
            public bool CanBeTrunk => ComposerCatalog.CanBeTrunkRow(Cfg);

            /// <summary>槽内 Then 奖励资格（2026-10-07 位 3 删除后派生=CanBeRewardRow）：
            /// 派生主干资格 ∩ 非引擎行（预算/非错边过滤在调用点）。</summary>
            public bool CanBeBranchReward => ComposerCatalog.CanBeRewardRow(Cfg);

            /// <summary>错边原子（内容契约：只能进代价栏）——p≠0 且表级域锁错侧，或 p=0 单侧域锁。
            /// 与 CardEffectConverter 代价校验同口径（2026-10-28 P1：WrongSide 删除——侧别判定内联，
            /// 行为等价；代价栏合法载体=p<0 锁己方，其余形态 UI 处置归 P4）。</summary>
            public bool IsWrongSideOnly
            {
                get
                {
                    if (Cfg == null) return false;
                    float p = UnityEngine.Mathf.Clamp(Cfg.Polarity, -1f, 1f);
                    var kinds = Cfg.GetTargetKindList();
                    int side = CostDerivationService.SideLock(kinds);
                    if (p > 0f) return side == 1;   // 有益锁对方（对对手有益）
                    if (p < 0f) return side == -1;  // 有害锁己方（自伤——代价合法载体）
                    return side != 0;               // 零极性单侧锁
                }
            }

            /// <summary>落槽生成的新原子条目（引用型：refId=表行ID，value=1）。</summary>
            public AtomicEffectEntry Entry()
                => new AtomicEffectEntry { refId = Cfg?.HashId, value = 1 };
        }
    }
}
