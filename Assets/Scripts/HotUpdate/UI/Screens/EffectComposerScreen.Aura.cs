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
        // ======================================== 光环模式（箭头+条目随效果合成，挂卡并集） ========================================

        // 箭头计数标签（就地刷新防输入丢焦；光环条目费用由顶栏 AuraTopCostText 同源独占——面板行已退役）
        private TMP_Text _arrowCountLabel;

        /// <summary>光环编辑面板（2026-10-23 五分类双面；2026-10-30 改版）：
        /// 连接光环=光环槽（增益条目卡——aura-card 克隆、编辑走卡内 editor，恒箭头档无作用面下拉）
        /// + 槽底连接箭头盒（arrow-picker 预制体实例，必选≥1 支），条目库点击整组替换；
        /// 场地光环=场地光环槽（仪典卡+属性降低条目卡——作用面随极性钉死/属性降低恒对方）——无箭头。
        /// 两面共用 tpl-aura-slot 模板；单槽单选——库点击即替换上一个选中。</summary>
        private void MakeAuraPanel(RectTransform parent)
        {
            var h = _graph.header;
            var slot = UiKit.CloneTemplate("tpl-aura-slot", _slotArea.Content);
            if (slot == null)
            {
                Debug.LogError("[EffectComposer] 缺 tpl-aura-slot 光环槽模板——光环槽区跳过（校验区随缺）；检查 EffectUI.prefab");
                return;
            }
            foreach (var ol in slot.GetComponents<Outline>()) UnityEngine.Object.Destroy(ol);
            var slotImg = slot.GetComponent<Image>();
            if (slotImg != null) slotImg.raycastTarget = true; // 整槽可点（选中）
            var auraTitle = UiKit.FindDeep(slot, "slot-title")?.GetComponent<TMP_Text>();
            if (auraTitle == null) Debug.LogError("[EffectComposer] tpl-aura-slot 缺 slot-title——检查 EffectUI.prefab");
            else { auraTitle.text = _mode == ComposeMode.RangeAura ? "场地光环（仪典/属性降低）" : "光环槽（增益条目）"; UiKit.Size(auraTitle, fw: 1f); }
            MarkSelected(slot, SelKind.Parallel, 0);
            SelectOnClick(slot, SelKind.Parallel, 0, false);

            // ---- 场地光环面（2026-10-30 属性降低入场地光环）：仪典步 + 负属性条目（无箭头） ----
            if (_mode == ComposeMode.RangeAura)
            {
                h.ArrowDirections = 0; // 场地光环恒无箭头（仪典按作用面、属性降低按对方侧——写盘一致）
                h.LinkAuras ??= new List<LinkAuraData>();
                // 条目归属归一：本面只留负属性条目（增益/关键词条目属连接光环——防御旧档/旁路），
                // scope 恒对方（3）——极性 −1 单侧口径
                if (h.LinkAuras.Count > 0)
                {
                    h.LinkAuras = h.LinkAuras.Where(a => a != null
                        && !string.IsNullOrEmpty(a.stat) && a.value < 0).ToList();
                    foreach (var a in h.LinkAuras) a.scope = 3;
                }
                MakeHint(parent, "场地光环＝仪典/属性降低：载体入场即挂，离场/被无效即失效——不占连接位；"
                    + "仪典同名全局唯一（作用面随极性钉死：增益仅己方、减益仅对方，单侧档计价 ×1.5）；"
                    + "属性降低仅对方生物生效，双方各挂一份可叠加");
                int ritual = 0, foreign = 0;
                foreach (var s in _graph.steps ?? new List<EffectStepData>())
                {
                    if (s?.kind != 0 || s.atomic == null) { if (s != null) foreign++; continue; }
                    if (!IsRitualStepRow(AtomicEffectTable.GetByHashId(s.atomic.refId))) { foreign++; continue; }
                    if (ritual == 0) MakeAuraRuleCard(slot, s.atomic); // 仪典卡=摘要+作用面（光环专用卡）
                    ritual++;
                }
                for (int i = 0; i < h.LinkAuras.Count; i++)
                    MakeAuraEntryCard(slot, i); // 属性降低条目卡（数值可调——恒负、恒对方）
                if (ritual == 0 && h.LinkAuras.Count == 0)
                    MakeHint(slot, "尚无内容——右侧仪典库/属性降低条目点击选择（新选即替换）");
                else if (ritual > 1)
                    MakeHint(slot, $"已装 {ritual} 条仪典——仪典同名全局唯一、通常一条就够（多的入场时会被送墓）");
                if (foreign > 0)
                    MakeHint(slot, $"另有 {foreign} 步非仪典内容（历史数据）——本面不展示，保存时原样保留");
                _slotZones.Add(MakeZone(slot, () => RuleAtomCount(_graph) == 0 && ValidAuraCount(h.LinkAuras) == 0
                    ? "场地光环至少配一条仪典或属性降低条目——右侧点击选择" : null));
                return;
            }

            // ---- 连接光环面（2026-10-30 只留增益）：条目+箭头（仪典/属性降低不入——切「场地光环」配） ----
            MakeHint(parent, "光环＝持续增益：作用对象=箭头指向格的当前占据者（断链/离场即失效）——"
                + "无作用面档/全体档；关键词条目恒含角色（指向角色格=投递角色），属性条目只作用于生物；"
                + "效果条目=箭头指向的单位视为持有该触发效果（仅生物可挂的一回合一次/稀有档·无代价效果可授予，2026-11-05；"
                + "一回合多次=结界专属，2026-11-14）。");
            h.LinkAuras ??= new List<LinkAuraData>();
            // 条目归属归一：本面只留增益/关键词条目（负属性条目属场地光环——防御旧档/旁路），
            // 恒箭头档（scope=0，作用范围下拉已随 2026-10-30 删退役）；空条目一并剔除
            if (h.LinkAuras.Count > 0)
            {
                h.LinkAuras = h.LinkAuras.Where(a => a != null
                    && (string.IsNullOrEmpty(a.stat) || a.value > 0)).ToList();
                foreach (var a in h.LinkAuras) a.scope = 0;
            }
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
                if (s?.kind == 0 && s.atomic != null)
                    MakeReadonlyStepBadge(slot, s); // 历史仪典/原子步只读（连接面不编辑——切场地光环处理）
            for (int i = 0; i < h.LinkAuras.Count; i++)
                MakeAuraEntryCard(slot, i);
            if (h.LinkAuras.Count == 0)
                MakeHint(slot, "尚无光环——右侧增益库点击选择（属性/关键词/效果；新选即替换旧选）");
            MakeHint(slot, "条目费=行锚×量级×箭头数（×1.2 按箭头数累乘；关键词条目恒含角色=箭头×5 档；"
                + "效果条目=效果锚价×箭头数，触发不另收元素费）；光环默认持续——不收持续费");

            // ---- 连接箭头（2026-10-09 迁入光环槽底部——跨级克隆 tpl-arrows 进 slot；
            //      存在连接方向档条目才显示；作用面档条目不依赖箭头） ----
            _arrowCountLabel = null; // 随面板重建作废（防悬挂引用写已销毁对象）
            if (AnyArrowModeEntry(h))
            {
                var arrowsBox = UiKit.CloneTemplateFrom(_slotArea.Content, "tpl-arrows", slot);
                if (arrowsBox == null)
                    Debug.LogError("[EffectComposer] 槽区缺 arrows 烘焙节点（2026-10-06 箭头预制体化）——检查 EffectUI.prefab");
                else
                {
                    UiKit.Size(arrowsBox, fw: 1f); // 槽内铺满宽（高度仍由模板接管）
                    var title = UiKit.FindDeep(arrowsBox, "title")?.GetComponent<TMP_Text>();
                    if (title != null) title.text = "连接箭头";
                    var head = UiKit.FindDeep(arrowsBox, "head");
                    if (head == null)
                        Debug.LogError("[EffectComposer] arrows 盒缺 head 行——箭头计数标签不显示；检查 EffectUI.prefab");
                    _arrowCountLabel = head != null
                        ? UiKit.Label("count", head, ArrowCountText(), UiStyle.MiniSize, UiStyle.TextHint)
                        : null;
                    var picker = BindArrowPicker(arrowsBox);
                    UiKit.Described(picker.GetComponent<Button>() ?? picker.gameObject.AddComponent<Button>(),
                        "连接箭头：光环的作用方向。点击某个方向点亮或取消（白=已选），"
                        + "光环持续作用于箭头所指格子上的单位。方向以这张卡持有者的视角为准，"
                        + "至少要选一个方向；箭头越多费用涨得越快。");
                    // 校验：连接光环必须搭配至少一支箭头（无箭头=永无受益者；2026-10-30 条目恒箭头档——必选）
                    _slotZones.Add(MakeZone(arrowsBox, () => (HexDirection)h.ArrowDirections == HexDirection.None
                        ? "未选箭头——连接光环必须搭配至少一支箭头（无箭头=永无受益者）；场地光环（仪典/属性降低）则不需要箭头" : null));
                }
            }
            // 校验：至少一条有效光环内容 + 关键词条目须位 5 可挂 + 效果引用须可授予（仪典唯一性属规则光环面）
            _slotZones.Add(MakeZone(slot, () =>
            {
                if (ValidAuraCount(h.LinkAuras) == 0)
                    return "无有效光环内容——至少配一条条目（空条目保存时剔除）";
                foreach (var a in h.LinkAuras)
                {
                    if (a == null) continue;
                    if (!string.IsNullOrEmpty(a.keyword)
                        && !ComposerCatalog.IsAuraMountableKeyword(a.keyword))
                        return $"关键词「{a.keyword}」不可作光环——表行标「不可作为连接光环」"
                             + "（真消耗型生效后移除，与光环 live-query 持续语义冲突）；请改选下拉中的可挂关键词";
                    // 效果引用条目（2026-11-05）：悬空/资格不符拦截（AuraGrantRejectReason 单源；
                    // Get 不告警防校验循环刷屏，装载闸有 Warn 兜底）
                    if (!string.IsNullOrEmpty(a.effectRef))
                    {
                        var dto = EffectsLibrary.Get(a.effectRef);
                        var reject = dto == null ? "引用不在效果库（悬空——效果可能已被删除）"
                            : EffectEligibility.AuraGrantRejectReason(EffectSlim.ToCardEffect(dto));
                        if (reject != null)
                            return $"效果「{a.effectRef}」{reject}——请改选效果库中的可授予效果";
                    }
                }
                return null;
            }));
        }

        /// <summary>steps 中的规则光环原子数（光环槽内仪典计数——全局唯一提醒用）。</summary>
        private static int RuleAtomCount(EffectGraphData g)
        {
            int n = 0;
            foreach (var s in g?.steps ?? new List<EffectStepData>())
                if (s?.kind == 0 && s.atomic != null && IsRuleAuraRow(AtomicEffectTable.GetByHashId(s.atomic.refId))) n++;
            return n;
        }

        /// <summary>是否存在连接方向档条目（2026-10-08 条目级作用面）：箭头盒显隐与箭头校验的适用条件
        ///——全部条目走作用面档时无箭头语义（空条目表=默认箭头档，保旧默认视图）。</summary>
        private static bool AnyArrowModeEntry(CardEffectData h)
            => h.LinkAuras == null || h.LinkAuras.All(a => a == null || a.scope == 0);

        /// <summary>六向箭头绑定（2026-10-06 arrow-picker 预制体化）：按钮名 T/D/L/R=上/下/左/右
        ///（L-T=左上…R-D=右下——见 ArrowButtons 表）；三角朝向/间距/底格由预制体烘焙，代码只挂
        /// 选中态变色——未选 #A1A1A1、选中纯白。</summary>
        private RectTransform BindArrowPicker(RectTransform arrowsBox)
        {
            var h = _graph.header;
            var picker = UiKit.FindDeep(arrowsBox, "arrow-picker");
            if (picker == null)
            {
                Debug.LogError("[EffectComposer] arrows 盒缺 arrow-picker 预制体实例——检查 EffectUI.prefab");
                return arrowsBox;
            }
            foreach (var (dir, nodeName) in ArrowButtons)
            {
                var node = UiKit.FindDeep(picker, nodeName);
                var btn = node?.GetComponent<Button>();
                // 变色目标=按钮自身的底格 Image（方向标注层）；tri 子节点（DropdownArrow 刻痕）保持预制体
                // 烘焙色只作方向标记——2026-10-06 实测定案：刻痕太小，选中态亮底格才可见
                var padImg = node?.GetComponent<Image>();
                if (btn == null || padImg == null)
                {
                    Debug.LogError($"[EffectComposer] arrow-picker 缺按钮「{nodeName}」或其底格 Image——检查 arrow-picker.prefab");
                    continue;
                }
                void Sync()
                {
                    bool on = ((HexDirection)h.ArrowDirections).HasFlag(dir);
                    padImg.color = on ? Color.white
                        : new Color(161f / 255f, 161f / 255f, 161f / 255f); // 未选 #A1A1A1
                    // [ArrowDebug] 变色排查临时日志：面板构建×6 + 每次点击各打一条（按钮名+写入色）
                    Debug.Log($"[ArrowDebug] {nodeName} 选中={on} 底格写入=#{ColorUtility.ToHtmlStringRGB(padImg.color)}"
                        + $" Arrows={(HexDirection)h.ArrowDirections}");
                }
                Sync();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    var cur = (HexDirection)h.ArrowDirections;
                    h.ArrowDirections = (int)(cur.HasFlag(dir) ? cur & ~dir : cur | dir);
                    Sync();
                    if (_arrowCountLabel != null) _arrowCountLabel.text = ArrowCountText();
                    RefreshName();
                    ValidateZones();
                });
            }
            return picker;
        }

        private string ArrowCountText()
        {
            int n = CountArrowBits((HexDirection)_graph.header.ArrowDirections);
            return n == 0 ? "（未选）" : $"（已选 {n} 支）";
        }

        /// <summary>有效光环条目数（stat/keyword/effectRef 至少一项非空——2026-11-05 效果光环扩三空）。</summary>
        private static int ValidAuraCount(List<LinkAuraData> auras)
            => auras?.Count(a => a != null && (!string.IsNullOrEmpty(a.stat) || !string.IsNullOrEmpty(a.keyword)
                                              || !string.IsNullOrEmpty(a.effectRef))) ?? 0;

        /// <summary>光环卡克隆源（2026-10-07 光环专用卡）：tpl-aura-slot 内 pristine aura-card——
        /// head 摘要(text+del) + field-value 数值 + aura-duration 行(placeholder 标签 + aura-kinds 下拉)。
        /// 缺卡 LogError 返回 null——不代码补建。</summary>
        private RectTransform CloneAuraCard(RectTransform slot)
        {
            var tplCard = _slotArea.Content.Find("tpl-aura-slot")?.Find("aura-card") as RectTransform;
            var card = tplCard != null
                ? UnityEngine.Object.Instantiate(tplCard, slot, false) as RectTransform
                : null;
            if (card == null)
            {
                Debug.LogError("[EffectComposer] tpl-aura-slot 缺 aura-card——本卡跳过；检查 EffectUI.prefab");
                return null;
            }
            card.gameObject.SetActive(true);
            return card;
        }

        /// <summary>仪典卡（光环专用）：aura-card 摘要+删除——仪典无参数（field-value/种类行隐藏）；
        /// 普通原子卡（atom-card）不再进光环槽（2026-10-07 不混用定案）。</summary>
        private void MakeAuraRuleCard(RectTransform slot, AtomicEffectEntry atom)
        {
            var card = CloneAuraCard(slot);
            if (card == null) return;
            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>();
            if (text == null) Debug.LogError("[EffectComposer] aura-card 缺 text 摘要文本——检查 EffectUI.prefab");
            else
            {
                text.text = "◈ " + AtomText.RenderAtomEntry(atom, _graph.header);
                UiKit.Size(text, fw: 1f);
            }
            var head = UiKit.FindDeep(card, "head");
            var delNode = head != null ? UiKit.FindDeep(head, "del") : null;
            if (delNode == null) Debug.LogError("[EffectComposer] aura-card 缺 del——仪典不可删；检查 EffectUI.prefab");
            else BindCardButton(delNode, "删除", true, () => RemoveAtom(SelKind.Parallel, 0));
            UiKit.FindDeep(card, "field-value")?.gameObject.SetActive(false); // 仪典无数值

            // 仪典作用范围（2026-10-09 设置盒拆除迁入 actuating-range 行）：绑 step.atomic.value
            //（RuleAuraScope 序=方向码−1；选项极性门控——2026-11-06 定案翻转：极性 0 行只能双方、
            // ±1 行=极性单侧+双方两档，缺省双方=表价档）——原 BindAuraScopeSettings/dd-kinds 复用退役；
            // 仪典无「含角色」概念——player Toggle 恒隐（prefab 未烘焙亦不报缺）
            var range = UiKit.FindDeep(card, "actuating-range") as RectTransform;
            var rangeDd = range != null ? UiKit.FindDeep(range, "aura-kinds")?.GetComponent<TMP_Dropdown>() : null;
            var rcfg = AtomicEffectTable.GetByHashId(atom?.refId);
            float ritualPol = Mathf.Clamp(rcfg?.Polarity ?? 0f, -1f, 1f);
            if (rcfg != null && Mathf.Abs(ritualPol) < 0.5f)
            {
                // 极性 0 行（2026-11-06 定案）：只能作用于双方——实例 value 恒写 Both、
                // 隐藏作用范围下拉行（表价即实价，无单侧档）
                atom.value = (int)RuleAuraScope.Both;
                if (range != null) range.gameObject.SetActive(false);
            }
            else if (range == null || rangeDd == null || rcfg == null)
            {
                Debug.LogError("[EffectComposer] 仪典卡缺 actuating-range/aura-kinds 或表行——作用范围不可编；检查 EffectUI.prefab");
            }
            else
            {
                UiKit.FindDeep(range, "player")?.gameObject.SetActive(false);
                var opts = AllowedScopesOfPolarity(rcfg.Polarity); // ±1 行：{极性单侧, 双方}
                int cur = atom.value + 1; // RuleAuraScope 序 → 方向码
                if (!opts.Contains(cur)) cur = opts.Contains(2) ? 2 : opts[opts.Count - 1]; // 存量旧档值回落双方=表价档
                atom.value = cur - 1;

                rangeDd.ClearOptions();
                rangeDd.AddOptions(opts.Select(LinkScopeZh).ToList());
                rangeDd.SetValueWithoutNotify(Mathf.Max(0, opts.IndexOf(cur)));
                rangeDd.RefreshShownValue();
                rangeDd.onValueChanged.RemoveAllListeners();
                rangeDd.onValueChanged.AddListener(i =>
                {
                    int v = opts[Mathf.Clamp(i, 0, opts.Count - 1)];
                    if (v > 0) atom.value = v - 1;
                    BindAtomDes(card, rcfg, atom); // des 的作用范围随选择刷新
                    RefreshName(); // 范围影响计价——费用预览随改随刷
                });
                UiKit.DescribedOptions(rangeDd,
                    "作用范围：仪典对哪一侧生效——表价按对称双方校准；极性 0 行恒双方，±1 行可收窄单侧。",
                    i =>
                    {
                        if (i < 0 || i >= opts.Count) return null;
                        int v = opts[i];
                        return v == 1 ? "己方：只为你（光环控制者）一侧生效（单侧档计价 ×1.5）。"
                            : v == 3 ? "对方：只为对手一侧生效（单侧档计价 ×1.5）。"
                            : "双方：两侧都生效（对称面，表价即实价——缺省档）。";
                    });
            }

            // des 放最后：作用范围已归一（RuleAuraScope）——读 atom.value，不读 Domain
            BindAtomDes(card, AtomicEffectTable.GetByHashId(atom?.refId), atom);
        }

        /// <summary>光环条目卡（2026-10-07 光环专用卡；2026-10-30 作用范围退役）：aura-card 直编辑——
        /// head 摘要+删除；field-value=属性数值（关键词条目隐藏）。连接光环面：条目恒箭头档
        ///（scope=0，作用对象=箭头指向格占据者）、数值恒正（只留增益）；场地光环面（2026-10-30
        /// 属性降低挪入）：条目恒仅对方（scope=3）、数值恒负可调。作用范围下拉行两面皆隐藏；
        /// 条目种类（攻/生/属性、关键词选择）随 2026-10-08 定案在右栏库行细分。</summary>
        private void MakeAuraEntryCard(RectTransform slot, int index)
        {
            var aura = _graph.header.LinkAuras[index];
            var card = CloneAuraCard(slot);
            if (card == null) return;
            bool isStat = !string.IsNullOrEmpty(aura.stat);
            bool field = _mode == ComposeMode.RangeAura; // 场地光环面：属性降低条目（恒负·恒对方）

            // 光环条目不读 Domain（2026-11-07 定案）：连接光环=箭头指向单位（不指对手战场）、
            // 场地属性降低=恒对方——des 统一隐藏，避免把源行的战场域显示成作用范围。
            UiKit.FindDeep(card, "des")?.gameObject.SetActive(false);

            var head = UiKit.FindDeep(card, "head");
            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>();
            if (text == null) Debug.LogError("[EffectComposer] aura-card 缺 text 摘要文本——检查 EffectUI.prefab");
            if (text != null) UiKit.Size(text, fw: 1f);
            void RefreshText()
            {
                if (text != null) text.text = AuraEntryText(aura);
                RefreshName(); // 顶栏费用（AuraTopCostText 同源）随条目改动刷新
                ValidateZones();
            }
            RefreshText();

            var delNode = head != null ? UiKit.FindDeep(head, "del") : null;
            if (delNode == null) Debug.LogError("[EffectComposer] aura-card 缺 del——条目不可删；检查 EffectUI.prefab");
            else BindCardButton(delNode, "删除", true, () =>
            {
                _graph.header.LinkAuras.RemoveAt(index);
                RefreshSlots();
            });

            // ---- 数值（属性条目）：连接面恒正（只留增益，≤0 收 1）；场地面恒负（≤−1 收 −1） ----
            var valNode = UiKit.FindDeep(card, "field-value");
            if (valNode == null) Debug.LogError("[EffectComposer] aura-card 缺 field-value——检查 EffectUI.prefab");
            else
            {
                valNode.gameObject.SetActive(isStat);
                if (isStat)
                {
                    var input = valNode.GetComponentInChildren<TMP_InputField>(true);
                    if (input != null)
                    {
                        input.contentType = TMP_InputField.ContentType.IntegerNumber;
                        input.text = aura.value.ToString();
                        input.onEndEdit.RemoveAllListeners();
                        input.onEndEdit.AddListener(_ =>
                        {
                            if (int.TryParse(input.text, out var v))
                            {
                                aura.value = field ? Math.Min(v, -1) : Math.Max(v, 1);
                                RefreshSlots();
                            }
                            else input.text = aura.value.ToString();
                        });
                        UiKit.Described(input, field
                            ? "数值：受光环影响的对方生物持续降低的量——恒为负数（至少 −1），数值越大降得越多；"
                              + "双方各挂一份时叠加生效。"
                            : "数值：光环给箭头指向单位的属性加成——恒为正数（只留增益），数值越大费用越高。"
                              + "多支箭头指向同一个单位时，每一支都各自生效一次。");
                    }
                }
            }

            // ---- 作用面（2026-10-30 下拉退役）：连接面恒箭头档（scope=0）、场地面恒仅对方（scope=3）——
            //      行隐藏；数值钳制已保证极性不翻转（无门控收敛需求） ----
            aura.scope = field ? 3 : 0;
            UiKit.FindDeep(card, "actuating-range")?.gameObject.SetActive(false);
        }

        /// <summary>条目摘要（2026-10-08 作用面版）：作用面档=【范围】前缀；
        /// 属性=「属性：攻击 +1」（Both=属性——攻生同值 ±1/±1）；关键词=「关键词：帷幕」（库外脏值直显 id）；
        /// 效果（2026-11-05 效果光环）=「效果：名」（库外悬空直显 id）。
        /// 含角色尾注已随 2026-10-09 裁定退役（关键词条目恒含角色，不再逐条目标注）。</summary>
        private static string AuraEntryText(LinkAuraData aura)
        {
            if (aura == null) return "（空条目）";
            string scopeZh = aura.scope > 0 ? $"【{LinkScopeZh(aura.scope)}】" : "";
            if (!string.IsNullOrEmpty(aura.stat))
                return $"{scopeZh}属性：{StatZhOf(aura.stat)} {(aura.value >= 0 ? "+" : "")}{aura.value}";
            if (!string.IsNullOrEmpty(aura.effectRef))
                return $"{scopeZh}效果：{EffectsLibrary.Get(aura.effectRef)?.name ?? aura.effectRef}";
            var c = ComposerCatalog.AuraKeywordChoices().FirstOrDefault(k => k.id == aura.keyword);
            return $"{scopeZh}关键词：{c.label ?? aura.keyword ?? "？"}";
        }

        /// <summary>光环计价预览卡：效果级光环无宿主卡——临时 CardData 承条目＋预选箭头
        ///（2026-10-12 裁定翻转旧「预览不计箭头」口径：箭头档条目在预览同样按箭头位数计费）。
        /// 顶栏 AuraTopCostText 同源只读（2026-10-07 面板 aura-cost 行退役）。</summary>
        private CardData AuraPreviewCard()
        {
            var h = _graph.header;
            return new CardData
            {
                CardName = "AURA_PREVIEW",
                Supertype = Cardtype.Enchantment,
                ArrowDirections = (HexDirection)h.ArrowDirections,
                LinkAuras = h.LinkAuras != null && h.LinkAuras.Count > 0
                    ? new List<LinkAuraData>(h.LinkAuras) : new List<LinkAuraData>(),
            };
        }

        private static int CountArrowBits(HexDirection d)
        {
            int n = 0, v = (int)d;
            while (v != 0) { n += v & 1; v >>= 1; }
            return n;
        }

    }
}
