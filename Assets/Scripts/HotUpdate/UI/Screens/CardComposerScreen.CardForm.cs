using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UInputField = TMPro.TMP_InputField;

namespace SynergyUI
{
    public sealed partial class CardComposerScreen : UIScreen
    {
        // ======================================== 中栏：卡面即编辑器 ========================================

        private void BindMiddleCard()
        {
            // 卡面=只读实时预览（2026-11-06 布局改版）：ACardView 按卡型点亮类型块/立绘/徽章/效果行，
            // 与手牌/战场渲染同源（FromCardDataFull 静态快照，无运行态订阅）
            _cardFace = Find("ACard");
            if (_cardFace != null)
            {
                _previewView = _cardFace.GetComponent<ACardView>() ?? _cardFace.gameObject.AddComponent<ACardView>();
                _previewView.Bind();
                // 单击卡面=放大展示（2026-11-06：DeckBuilder 预览同款——运行时补 Button，
                // 编辑走下方表单/槽行，卡面本体只读+可放大）
                var faceBtn = _cardFace.GetComponent<UnityEngine.UI.Button>();
                if (faceBtn == null)
                {
                    faceBtn = _cardFace.gameObject.AddComponent<UnityEngine.UI.Button>();
                    faceBtn.transition = UnityEngine.UI.Button.Transition.None;
                }
                faceBtn.onClick.RemoveAllListeners();
                faceBtn.onClick.AddListener(() => OpenZoom(_card));
                Describe(faceBtn, "单击放大查看全卡（点空白遮罩还原）——卡面为实时预览，编辑请用右侧表单与效果槽行");
            }

            // 编辑区锚点：CardPreView 直属子树（fields/add-slot/form-scroll 与 ACard 实例平级）——
            // 直查路径绑定，防深扫与卡面内部同名节点（name/effect/passives/keys）串线
            _editorRoot = Find("CardPreView");
            _fields = _editorRoot != null ? _editorRoot.Find("fields") as RectTransform : null;
            if (_fields == null)
                Debug.LogError("[CardComposer] 屏内缺 CardPreView/fields（CardUI.prefab）——表单输入区不可用");

            // 费用公式文本节点（fields/cost-row 下；节点名带尾空格 "CostSquares "）——
            // 按名前缀深扫防尾空格丢失；开启自动换行（公式长于行宽）
            _typeCostText = FindCostFormulaText(_fields);
            if (_typeCostText != null) _typeCostText.enableWordWrapping = true;

            _nameInput = FindInputUnder(_fields, "name");
            if (_nameInput != null)
                _nameInput.onValueChanged.AddListener(v => { _card.CardName = v; RefreshPreview(); });

            _attributeZone = UiKit.FindDeep(_fields, "attribute");
            _attackInput = BindIntInput(_attributeZone, "attack",
                v => { _card.Power = ClampStat(v, _attackInput); Recalculate(); },
                $"生物攻击力——计入自动算费（区间 [0, {MaxCreatureStat}]；底盘剩余点×2 免费、攻/血自由分配，动态）", MaxCreatureStat);
            _lifeInput = BindIntInput(_attributeZone, "life",
                v => { _card.Life = ClampStat(v, _lifeInput); Recalculate(); },
                $"生物生命值——计入自动算费（区间 [0, {MaxCreatureStat}]；底盘剩余点×2 免费、攻/血自由分配，动态）", MaxCreatureStat);
            _durabilityInput = BindIntInput(_attributeZone, "durability",
                v => { _card.Durability = v; Recalculate(); }, "结界耐久——免费点全投耐久，装/卸效果时即时重算");

            // 场地开关（2026-10-29 P8 定案）：结界专属 isField 标记——钮建在耐久行壳内
            //（行壳显隐=开关显隐，SetAttrActive 同拍）；运行时建件（UiKit 工厂），prefab 零改。
            var durRow = _durabilityInput != null ? _durabilityInput.transform.parent as RectTransform : null;
            if (durRow != null)
            {
                _fieldToggle = UiKit.MiniButton("field-toggle", durRow, "场地:关", OnFieldToggleClicked, null, null, 88f);
                _fieldToggleLabel = _fieldToggle != null ? _fieldToggle.GetComponentInChildren<TMP_Text>() : null;
                Describe(_fieldToggle, "场地卡（P8）：结界专属标记——打出后固定落在己方场地格（每方一张），"
                    + "再打场地=顶替（旧场地入墓）。改标记=换卡 ID（内容哈希）");
            }

            // v2 只读预览层（name-view/kind-view/face-note）已随 ACardView 实时渲染退役（prefab 节点同批删除）

            var formCt = FormContent();
            _effectZone = formCt != null ? formCt.Find("effect") as RectTransform : null;
            _passivesZone = formCt != null ? formCt.Find("passives") as RectTransform : null;
            _keysZone = formCt != null ? formCt.Find("keys") as RectTransform : null;

            // 三区节点=单槽模板（cost 模板已随 2026-11-06 代价挪构筑期退役）：prefab 恒 inactive，
            // 槽行=运行时克隆；捕获主 Outline 原色（选中行变白/取消还原），登记模板供克隆
            RegisterSlotTemplate(ZoneId.Effect, _effectZone);
            RegisterSlotTemplate(ZoneId.Passives, _passivesZone);
            RegisterSlotTemplate(ZoneId.Keys, _keysZone);
            BindBasicAbility();
            BindAddSlotButtons();
        }

        /// <summary>登记槽模板并捕获主 Outline 原色（缺节点 LogError 空转不炸）。
        /// 模板登记即压回 inactive（2026-11-06 prefab 改版后模板可能烘成激活态——CloneTemplateFrom
        /// 只收 inactive 模板：激活态会静默不克隆，且模板自身常驻显示成三条空行）。</summary>
        private void RegisterSlotTemplate(ZoneId id, RectTransform zone)
        {
            if (zone == null) return;
            zone.gameObject.SetActive(false);
            _slotTemplates[id] = zone;
            var ol = zone.GetComponent<Outline>();
            if (ol != null) _slotOutlineBase[id] = (ol.effectColor, ol.enabled);
        }

        // ---- setderivant（2026-11-06 改版）：主动/自动槽行内「设置衍生物」钮——效果含召唤衍生物/
        // 变形原子才显形（用户定案）；点击开选卡器（候选过滤单源 TokenTemplateCandidateOk），选中写
        // 原子 str=定义期直绑（保存闸对未绑 CardRef 原子照拦）。旧 derive 行模板/addderive 钮已随
        // prefab 布局改版退役 ----

        /// <summary>槽行 setderivant 钮接线（MakeSlotRow 逐行调用）：显隐=该效果是否含
        /// SummonToken/GrantMorph 槽（空槽/关键词行/无槽效果恒隐）；目标=该效果第一个未绑槽
        /// （全绑=首个槽，重选）；标签随绑定对账（已绑显卡名）。</summary>
        private void BindSetDerivant(RectTransform row, int dataIndex)
        {
            var btnRt = UiKit.FindDeep(row, "setderivant");
            if (btnRt == null) return; // keys 模板/旧 prefab 兼容——无钮空转
            var mine = DeckTokenSupport.EnumerateSlots(_card)
                .Where(s => s.EffectIndex == dataIndex).ToList();
            btnRt.gameObject.SetActive(mine.Count > 0);
            if (mine.Count == 0) return;
            var btn = btnRt.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) return;
            var target = mine.FirstOrDefault(s => s.NeedsBinding) ?? mine[0];
            var label = UiKit.FindDeep(btnRt, "label")?.GetComponent<TMP_Text>();
            var tpl = string.IsNullOrEmpty(target.FixedTemplateId)
                ? null : CardCatalog.GetById(target.FixedTemplateId);
            if (label != null)
                label.text = tpl != null
                    ? (target.IsMorph ? "变形:" : "衍生物:") + (string.IsNullOrEmpty(tpl.CardName) ? tpl.ID : tpl.CardName)
                    : "设置衍生物";
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Debug.Log("[UI点击] setderivant 效果#" + (dataIndex + 1));
                OpenDerivePicker(target);
            });
            Describe(btn, (target.IsMorph ? "变形" : "召唤衍生物") + "槽：点按从卡表选模板卡——候选须"
                + (target.IsMorph ? "生物/结界" : "生物") + "·费≥1·不带代价、自身不含衍生物/变形（不可嵌套）；选中即写进效果引用");
        }

        /// <summary>衍生物模板选卡器（卡表全量+三条过滤+搜索）：选中写原子 str=定义期直绑
        ///（运行时解析=实例绑定→原子 str，同 SummonTokenHandler.ResolveBoundTemplateId）。</summary>
        private void OpenDerivePicker(DeckTokenSupport.TokenSlot slot)
        {
            if (_deriveModal != null) { _deriveModal.Close(); _deriveModal = null; } // 重复点开=换卡窗
            bool creatureOnly = !slot.IsMorph; // 召唤=仅生物；变形=生物/结界
            var kindZh = slot.IsMorph ? "变形" : "召唤衍生物";
            var modal = UiKit.ModalBox(Root,
                $"选择模板卡——{kindZh}（{(creatureOnly ? "仅生物" : "生物/结界")}·费≥1·无代价·不可嵌套）",
                width: 660f, height: 600f);
            _deriveModal = modal;

            var searchRow = UiKit.Row("search", modal.Panel, spacing: 8f);
            UiKit.Size(UiKit.Label("lbl", searchRow, "搜索：", 13, UiStyle.TextSecondary), w: 40f);
            var searchField = UiKit.InputField("input", searchRow, "卡名 / ID 关键词", "", width: 400f);
            UiKit.Button("btn-close", searchRow, "关闭", () =>
            {
                _deriveModal?.Close();
                _deriveModal = null;
            }, height: 30f, fontSize: 13);

            var scroll = UiKit.ScrollColumn("list", modal.Panel);
            UiKit.Size(scroll.Rect, fw: 1f, fh: 1f);

            void Rebuild()
            {
                for (int i = scroll.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(scroll.Content.GetChild(i).gameObject);
                string key = (searchField.text ?? "").Trim();
                var candidates = CardSorter.Sort(CardCatalog.LoadAll()
                        .Where(c => DeckTokenSupport.TokenTemplateCandidateOk(c, creatureOnly))
                        .Where(c => string.IsNullOrEmpty(key)
                                    || (c.CardName ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)
                                    || (c.ID ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                const int cap = 150;
                int shown = 0;
                foreach (var card in candidates)
                {
                    if (shown++ >= cap) break;
                    var pick = card;
                    var line = UiKit.Row("card-" + pick.ID, scroll.Content, spacing: 8f);
                    UiKit.Size(UiKit.Label("cost", line, ((int)pick.TotalCost).ToString(), 13, UiStyle.TextFaint), w: 30f);
                    UiKit.Size(UiKit.Label("name", line,
                        string.IsNullOrEmpty(pick.CardName) ? pick.ID : pick.CardName, 13), w: 200f);
                    UiKit.Size(UiKit.Label("meta", line, DeriveMetaOf(pick), 12, UiStyle.TextFaint), fw: 1f);
                    bool current = slot.FixedTemplateId == pick.ID;
                    UiKit.Button("btn-pick", line, current ? "已选 ✓" : "选择", () =>
                    {
                        slot.Atom.str = pick.ID; // 定义期直绑
                        Recalculate();           // 卡面效果文案 {引用卡}→卡名 同拍
                        RefreshSlots();          // setderivant 标签随绑定对账
                        _deriveModal?.Close();
                        _deriveModal = null;
                    }, bg: current ? UiStyle.SelectedRowBg : null, height: 30f, fontSize: 13);
                }
                UiKit.Label("count", scroll.Content,
                    candidates.Count == 0
                        ? "（无匹配卡——候选须为费≥1、无代价、不含衍生物/变形的真实卡）"
                        : shown < candidates.Count ? $"共 {candidates.Count} 张，仅列前 {cap} 张——用搜索缩小范围"
                        : $"共 {candidates.Count} 张", 12, UiStyle.TextHint);
            }

            searchField.onValueChanged.RemoveAllListeners();
            searchField.onValueChanged.AddListener(_ => Rebuild());
            Rebuild();
        }

        /// <summary>候选行 meta 文本：类型 + 攻/血（生物，缺项显 ?）或类型标注（同 CardRef 选卡口径）。</summary>
        private static string DeriveMetaOf(CardData card)
            => card.Supertype == Cardtype.Creature
                ? $"生物 {(card.Power.HasValue ? card.Power.Value.ToString() : "?")}/{(card.Life.HasValue ? card.Life.Value.ToString() : "?")}"
                : card.Supertype == Cardtype.Enchantment ? "结界" : card.Supertype.ToString();

        // ---- add-slot 三钮：新建空槽（容量按型闸；新建即选中→右栏按槽类型过滤） ----

        private void BindAddSlotButtons()
        {
            // addcost 已随 2026-11-06 代价挪构筑期退役（prefab 同批删除；既有代价行编辑口=效果合成器）
            BindAddSlot(ZoneId.Effect, "addeffect", "添加主动效果",
                "新建空主动槽（生物×1 / 法术×1~3·施放时择一发动；结界禁主动）");
            BindAddSlot(ZoneId.Passives, "addpassives", "添加自动效果",
                "新建空自动槽（生物=自动×1；结界=光环恰好 1〔必填〕+自动×2；法术无）");
            BindAddSlot(ZoneId.Keys, "addkeys", "添加关键词",
                "新建空关键词槽（生物专属·至多 3，每关掉一个攻击/守卫能力 +1）");
        }

        private void BindAddSlot(ZoneId id, string node, string label, string desc)
        {
            var btn = BindButton(node, () => AddEmptySlot(id));
            if (btn == null) return;
            _addButtons[id] = btn;
            Describe(btn, desc);
        }

        /// <summary>新建空槽：容量闸（已用=已填+空槽）；新建即选中（右栏自动过滤到该类型）。</summary>
        private void AddEmptySlot(ZoneId id)
        {
            int cap = SlotCapacity(id);
            if (cap <= 0)
            {
                ShowToast(TypeZh(id) + "槽与本卡类型不匹配——不可添加");
                return;
            }
            if (FilledSlotCount(id) + EmptySlotCount(id) >= cap)
            {
                ShowToast($"{TypeZh(id)}槽已满（{FilledSlotCount(id)}/{cap}）——先删除槽内条目再添加");
                return;
            }
            _emptySlots[id] = EmptySlotCount(id) + 1;
            Debug.Log("[UI点击] add-slot " + id);
            SelectSlot(id, -1);
        }

        /// <summary>add 三钮显隐/可用随卡型与容量（容量 0 整钮隐藏；满槽禁用）。</summary>
        private void SyncAddSlotButtons()
        {
            foreach (var kv in _addButtons)
            {
                int cap = SlotCapacity(kv.Key);
                kv.Value.gameObject.SetActive(cap > 0);
                kv.Value.interactable = FilledSlotCount(kv.Key) + EmptySlotCount(kv.Key) < cap;
            }
        }

        //（EffectShapeBlocksCost 已随 2026-11-08 代价效果级化整删——无消费方：形态互斥在
        //  EffectComposerScreen.EffectCostConflictOf 效果级单源判定。）

        private static UInputField FindInputUnder(RectTransform scope, string name) =>
            scope != null ? UiKit.FindDeep(scope, name)?.GetComponentInChildren<UInputField>(true) : null;

        /// <summary>费用公式节点深扫（v2：节点在表单网格 cost-row 下；名带尾空格按前缀匹配）。</summary>
        private static TMP_Text FindCostFormulaText(RectTransform scope)
        {
            if (scope == null) return null;
            for (int i = 0; i < scope.childCount; i++)
            {
                var c = scope.GetChild(i);
                if (c.name.StartsWith("CostSquares", StringComparison.Ordinal))
                {
                    var t = c.GetComponent<TMP_Text>();
                    if (t != null) return t;
                }
                var deep = FindCostFormulaText(c as RectTransform);
                if (deep != null) return deep;
            }
            return null;
        }

        private UInputField BindIntInput(RectTransform scope, string node, Action<int> onChanged, string desc,
            int max = int.MaxValue)
        {
            var field = FindInputUnder(scope, node);
            if (field == null) return null;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.onValueChanged.AddListener(s =>
            {
                if (!int.TryParse(s, out var v)) return;
                if (v > max)
                {
                    v = max;
                    field.SetTextWithoutNotify(v.ToString()); // 回写钳后值——输入框显示与卡数据不脱钩
                }
                onChanged(v);
            });
            Describe(field, desc);
            return field;
        }

        private void SyncNameInput()
        {
            if (_nameInput != null) _nameInput.SetTextWithoutNotify(_card.CardName ?? "");
        }

        /// <summary>生物属性输入钳制（2026-11-14 免费属性自由分配定案）：区间=[0, MaxCreatureStat]。
        /// 免费属性=底盘剩余点数×2，可在攻/血间任意分配（不再强制 n/n 底、也不设单边下限）；
        /// 未用完的免费点在计价侧作废。越界输入即时钳制回写。非生物不钳。</summary>
        private int ClampStat(int v, UInputField field)
        {
            if (_kind != CardKind.Creature) return v;
            int clamped = Math.Max(0, Math.Min(MaxCreatureStat, v));
            if (clamped != v && field != null)
                field.SetTextWithoutNotify(clamped.ToString()); // 回写钳后值——输入框显示与卡数据不脱钩
            return clamped;
        }

        /// <summary>属性输入框显隐+回写（生物=攻/血、结界=耐久、法术=attribute 整区隐藏）。
        /// 攻/血文本回显收窄到 SetAttrTexts（留空=未填，保存闸口径）；耐久恒回显数字。
        /// v2：输入在表单网格行壳内（icon+label+input）——行壳随输入一起显隐。</summary>
        private void SyncAttributeInputs()
        {
            if (_attributeZone != null)
                _attributeZone.gameObject.SetActive(HasStats || HasDurability);
            SetAttrActive(_attackInput, HasStats);
            SetAttrActive(_lifeInput, HasStats);
            SetAttrActive(_durabilityInput, HasDurability);
            if (_durabilityInput != null)
                _durabilityInput.SetTextWithoutNotify(_card.Durability.ToString());
        }

        /// <summary>输入框连同其行壳显隐（行壳=输入的直接父级，若非 attribute 区本身）。</summary>
        private void SetAttrActive(UInputField field, bool on)
        {
            if (field == null) return;
            var go = field.gameObject;
            var row = field.transform.parent as RectTransform;
            if (row != null && row != _attributeZone && row.name.EndsWith("-row", StringComparison.Ordinal))
                go = row.gameObject;
            go.SetActive(on);
        }

        /// <summary>攻/血输入框回显（2026-10-16 属性必填定案）：留空=未填（新建/清空后保存拦截）；
        /// 载入旧卡 null 视作 0 已在 LoadCardForEdit 归一——此处只回显非空值。</summary>
        private void SetAttrTexts()
        {
            if (_attackInput != null)
                _attackInput.SetTextWithoutNotify(
                    _kind == CardKind.Creature && _card.Power.HasValue ? _card.Power.Value.ToString() : "");
            if (_lifeInput != null)
                _lifeInput.SetTextWithoutNotify(
                    _kind == CardKind.Creature && _card.Life.HasValue ? _card.Life.Value.ToString() : "");
            SyncFieldToggle(); // 场地开关文案回显（P8）：切型/载入/新建同拍
        }

        // ======================================== 槽位选中制（2026-10-16 二次改版） ========================================

        /// <summary>类型中文名（提示语/标题用）。</summary>
        private static string TypeZh(ZoneId id) => id switch
        {
            ZoneId.Cost => "代价",
            ZoneId.Effect => "主动",
            ZoneId.Passives => "自动",
            _ => "关键词",
        };

        /// <summary>槽容量按型（单源：add 四钮/填入闸/保存闸共用同一口径）。
        /// 结界 passives=光环1+自动2 合一（光环恰好 1 由保存闸拦截——填入时不强分）。
        /// 代价槽已随 2026-11-08 代价效果级化整退役（容量 0=全区隐藏）——代价编辑口=
        /// 效果合成器有限分支「支付代价」门（代价行挂 CardEffectData.Costs）。</summary>
        private int SlotCapacity(ZoneId id) => id switch
        {
            ZoneId.Cost => 0, // 代价槽退役（2026-11-08 效果级化）——编辑走效果合成器
            ZoneId.Effect => ActiveSlotCapacity,
            ZoneId.Passives => _kind == CardKind.Enchantment ? AuraSlotCapacity + AutoSlotCapacity : AutoSlotCapacity,
            _ => KeywordCapacity,
        };

        /// <summary>该类型已填槽数（数据侧计数；空槽另计 EmptySlotCount）。</summary>
        private int FilledSlotCount(ZoneId id) => id switch
        {
            ZoneId.Cost => 0, // 代价槽退役（2026-11-08）——卡级代价行不存在
            ZoneId.Effect => ActiveEffectCount,
            ZoneId.Passives => AuraEffectCount + AutoEffectCount,
            _ => _card.Keywords?.Count ?? 0,
        };

        private int EmptySlotCount(ZoneId id) =>
            _emptySlots.TryGetValue(id, out var n) ? n : 0;

        /// <summary>该槽类型是否与当前卡型匹配（不匹配的已填槽仍渲染、可删——保存闸兜底拦）。</summary>
        private bool TypeEnabledForKind(ZoneId id) => id switch
        {
            ZoneId.Cost => true,
            //（P12「结界主动槽只收充能/法术充能位」已随 2026-11-05 充能系统动作化改版收回——
            //  充能不再是卡上效果：结界禁主动（容量 0），法术主动槽=1~3。）
            ZoneId.Effect => true,
            ZoneId.Passives => _kind != CardKind.Spell,
            _ => _kind == CardKind.Creature,
        };

        /// <summary>选中槽（再点同槽=取消）：行内主 Outline 变白；右栏自动切效果库并按槽类型过滤。</summary>
        private void SelectSlot(ZoneId type, int index)
        {
            if (_selType == type && _selIndex == index)
            {
                DeselectSlot();
                return;
            }
            _selType = type;
            _selIndex = index;
            ShowRightView(false);
            RefreshSlots();
            RefreshLibrary();
        }

        /// <summary>取消选中（右栏回全量效果图；槽行重建还原描边）。</summary>
        private void DeselectSlot()
        {
            _selType = null;
            _selIndex = -1;
            RefreshSlots();
            RefreshLibrary();
        }

        /// <summary>卡型可见性（2026-10-16 二次改版；2026-11-06 opt-in 修订）：四区模板恒 inactive（槽行运行时克隆）；
        /// 此处只管 add 四钮（容量 0 隐藏）——basicability 行可见性归 SyncBasicAbility（充能暂不
        /// 开放后非生物整区收起，见其 2026-11-08 注）。</summary>
        private void SyncZoneVisibility()
        {
            SyncBasicAbility();
            SyncAddSlotButtons();
            if (_selType != null && SlotCapacity(_selType.Value) == 0) DeselectSlot();
        }

        // ---- basicability（2026-11-06 opt-in 改版：ac/dc/rc 三开关正向绑 AttackAbility/GuardAbility/
        //      ChargeAbility，默认关=不带——「添加了才有」；ac/dc 生物专属、rc 全卡型。
        //      2026-11-08 充能暂不开放：rc 编辑口收起——绑定/数据保留，重开=去 SyncBasicAbility 恒藏行） ----

        private void BindBasicAbility()
        {
            _basicAbilityZone = UiKit.FindDeep(_fields, "basicability");
            _attackToggle = BindAbilityToggle("ac", "具备攻击",
                on => _card.AttackAbility = on,
                "具备攻击能力（basicability，2026-11-06 opt-in）：默认不带；开启占 1 点底盘预算，关闭释放（关键词槽 +1、免费属性点 +2）",
                affectsKeywordSlots: true);
            _guardToggle = BindAbilityToggle("dc", "具备守卫",
                on => _card.GuardAbility = on,
                "具备守卫能力（basicability，2026-11-06 opt-in）：默认不带；开启占 1 点底盘预算，关闭释放（关键词槽 +1、免费属性点 +2）",
                affectsKeywordSlots: true);
            _chargeToggle = BindAbilityToggle("rc", "具备充能",
                on => _card.ChargeAbility = on,
                "具备充能能力（basicability，2026-11-06 opt-in）：默认不带；开启=该卡可宣充能（己方主阶段空栈、一回合一次、结算期选色免费落层），卡费固定 +1 灰",
                affectsKeywordSlots: false);
            SyncBasicAbility();
        }

        private Toggle BindAbilityToggle(string node, string label, Action<bool> apply, string desc, bool affectsKeywordSlots)
        {
            var rt = _basicAbilityZone != null ? UiKit.FindDeep(_basicAbilityZone, node) : null;
            var toggle = rt != null ? rt.GetComponent<Toggle>() : null;
            if (toggle == null) return null;
            ConvertLegacyLabelToTmp(rt, label); // prefab 手加的 legacy Text → TMP（全量 TMP 约定）
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(on =>
            {
                apply(on);
                _card.ResetCache();
                if (affectsKeywordSlots) RefreshSlots(); // 关键词槽容量随攻/守开关变化（add 按钮态同步；充能不影响）
                Recalculate();
                ShowToast(on ? $"已具备：{label}" : affectsKeywordSlots
                    ? $"已关闭：{label}——关键词槽 +1（现上限 {KeywordCapacity}）"
                    : $"已关闭：{label}——该卡不再可宣充能");
            });
            Describe(toggle, desc);
            return toggle;
        }

        /// <summary>legacy Text 标签运行时换装 TMP（cardtype 换装先例）：沿用原 rect，只换文本组件。</summary>
        private static void ConvertLegacyLabelToTmp(RectTransform toggleRt, string text)
        {
            var legacy = toggleRt.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (legacy == null) return;
            var src = legacy.rectTransform;
            var anchors = (src.anchorMin, src.anchorMax, src.pivot, src.sizeDelta, src.anchoredPosition);
            UnityEngine.Object.Destroy(legacy);
            var label = UiKit.Label("label", toggleRt, text, UiStyle.SmallSize, UiStyle.TextBody);
            var rt = label.rectTransform;
            rt.anchorMin = anchors.Item1;
            rt.anchorMax = anchors.Item2;
            rt.pivot = anchors.Item3;
            rt.sizeDelta = anchors.Item4;
            rt.anchoredPosition = anchors.Item5;
        }

        /// <summary>三开关回显与行可见性（2026-11-06 opt-in）：攻/守行仅生物可见（资格清空归
        /// ApplyKindToCard 切型口，与关键词「切型即清」同口径）。2026-11-08 充能暂不开放：
        /// rc 行恒藏（回显保留防池化残亮）——区只剩攻/守行，随其生物专属走：非生物整区收起。</summary>
        private void SyncBasicAbility()
        {
            if (_basicAbilityZone != null)
                _basicAbilityZone.gameObject.SetActive(_kind == CardKind.Creature);
            if (_attackToggle != null)
            {
                _attackToggle.SetIsOnWithoutNotify(_card.AttackAbility);
                _attackToggle.gameObject.SetActive(_kind == CardKind.Creature);
            }
            if (_guardToggle != null)
            {
                _guardToggle.SetIsOnWithoutNotify(_card.GuardAbility);
                _guardToggle.gameObject.SetActive(_kind == CardKind.Creature);
            }
            if (_chargeToggle != null)
            {
                _chargeToggle.SetIsOnWithoutNotify(_card.ChargeAbility);
                _chargeToggle.gameObject.SetActive(false); // 充能暂不开放（2026-11-08）：编辑口收起
            }
        }

        // ---- 场地开关（2026-10-29 P8）：结界专属 isField 标记 ----

        private void OnFieldToggleClicked()
        {
            _card.IsField = !_card.IsField;
            SyncFieldToggle();
            ShowToast(_card.IsField
                ? "已标记场地卡——场地卡只能放在场地格，每方一张"
                : "已取消场地标记（回到普通结界）");
        }

        /// <summary>钮文案回显（场地:开/关）。空转安全——钮只在结界面板的耐久行壳内存在。</summary>
        private void SyncFieldToggle()
        {
            if (_fieldToggleLabel != null)
                _fieldToggleLabel.text = _card.IsField ? "场地:开" : "场地:关";
        }

        // ---- 容量与效果分型（单源：保存闸/槽行/右栏过滤共用） ----

        //（P12「结界主动槽 1（只收充能）/法术主动槽 4（含充能位）」已随 2026-11-05 充能系统动作化
        //  改版收回——充能不再是卡上效果：法术主动槽=1~3（与 CardLoader「总效果数 ≤4」含自动位
        //  同口径），结界禁主动（容量 0），生物照旧 ×1。）
        private int ActiveSlotCapacity => _kind == CardKind.Spell ? 3 : (_kind == CardKind.Creature ? 1 : 0);
        private int AutoSlotCapacity => _kind == CardKind.Enchantment ? 2 : (_kind == CardKind.Creature ? 1 : 0);
        private int AuraSlotCapacity => _kind == CardKind.Enchantment ? 1 : 0;

        /// <summary>关键词上限（生物专属，2026-11-06 opt-in 口径）：3 + 攻/守资格缺位各 +1（最高 5；
        /// 充能资格不占底盘——不影响关键词槽）。</summary>
        private int KeywordCapacity => _kind == CardKind.Creature
            ? 3 + (_card.AttackAbility ? 0 : 1) + (_card.GuardAbility ? 0 : 1)
            : 0;

        /// <summary>光环形态判据（与效果合成器 InferMode 同源）：连接/规则光环都走光环形态效果图（IsAuraMode）。</summary>
        private static bool IsAuraEffect(CardEffectData fx)
        {
            if (fx == null) return false;
            var graph = new EffectGraphData(fx.DisplayName ?? "") { header = fx, steps = fx.Steps };
            return EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(graph));
        }

        private static bool IsActiveGraph(EffectGraphData g)
        {
            var fx = g?.header;
            if (fx == null) return false;
            var t = (TriggerTiming)fx.TriggerTiming;
            return t == TriggerTiming.Activate_Active || t == TriggerTiming.Activate_Instant;
        }

        // ---- P9（2026-10-29）宿主适用分区：EffectEligibility 单源——候选不静默隐藏 ----

        private static Cardtype HostOf(CardKind kind) => kind switch
        {
            CardKind.Enchantment => Cardtype.Enchantment,
            CardKind.Spell => Cardtype.Spell,
            _ => Cardtype.Creature,
        };

        /// <summary>宿主×图 适用判定的**唯一分区谓词**（效果库槽视图过滤与「隐藏计数行」共用；
        /// 验证器按此断言「反复触发图不在生物候选里出现」）。null=可挂；非空=拒绝原因。</summary>
        public static string HostRejectReason(Cardtype host, EffectGraphData g)
        {
            bool auraShape = EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(g));
            return EffectEligibility.RejectReason(host, EligibilityProbeDef(g), auraShape);
        }

        /// <summary>图 → 适用判定探针 def（只装激活方式/时机/原子类型——不跑完整装载转换，
        /// 不触发费用/校验副作用；原子来自 steps 优先、扁平 AtomicEffects 兜底）。</summary>
        private static CardCore.EffectDefinition EligibilityProbeDef(EffectGraphData g)
        {
            var def = new CardCore.EffectDefinition
            {
                Id = "ELIG_PROBE",
                TriggerTiming = (TriggerTiming)(g?.header?.TriggerTiming ?? 0),
                ActivationType = (EffectActivationType)(g?.header?.ActivationType ?? 0),
                Effects = new List<CardCore.AtomicEffectInstance>(),
            };
            void Add(AtomicEffectEntry e)
            {
                var inst = e == null ? null : CardEffectConverter.ConvertAtomForUI(e);
                if (inst != null) def.Effects.Add(inst);
            }
            bool fromSteps = false;
            foreach (var s in g?.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                fromSteps = true;
                Add(s.atomic);
            }
            if (!fromSteps && g?.header?.AtomicEffects != null)
                foreach (var e in g.header.AtomicEffects) Add(e);
            return def;
        }

        private int ActiveEffectCount
        {
            get
            {
                int n = 0;
                foreach (var fx in _card.Effects ?? new List<CardEffectData>()) if (IsActiveEffect(fx)) n++;
                return n;
            }
        }

        private int AuraEffectCount
        {
            get
            {
                int n = 0;
                foreach (var fx in _card.Effects ?? new List<CardEffectData>()) if (IsAuraEffect(fx)) n++;
                return n;
            }
        }

        private int AutoEffectCount
        {
            get
            {
                int n = 0;
                foreach (var fx in _card.Effects ?? new List<CardEffectData>())
                    if (fx != null && !IsAuraEffect(fx) && !IsActiveEffect(fx)) n++;
                return n;
            }
        }

        // ---- 卡面刷新（Recalculate 统一收口） ----

        /// <summary>卡面实时预览（2026-11-06 布局改版）：ACardView 静态快照——类型块/立绘/徽章/
        /// 效果行全量重绘（与手牌/战场渲染同源；FromCardDataFull 无运行态订阅，幂等）。</summary>
        private void RefreshPreview()
        {
            if (_previewView != null)
                _previewView.Apply(CardOverlayItem.FromCardDataFull(_card, "ComposerPreview"), CardOverlayLayout.Full);
        }

        private void RefreshCardFace(CardCostCalculator.Result result)
        {
            RefreshPreview();

            // fields/cost-row "CostSquares " 文本节点=卡费文字公式（2026-10-16 二次改版：方格退役，
            // 口式参考效果组合界面费用公式——逐项「标签＝值」＋折扣＋合计/分布）
            if (_typeCostText != null)
                _typeCostText.text = CardCostFormulaText(result);

            // 悬浮费用构成（完整逐行明细；文本公式是它的紧凑投影）——挂费用公式节点
            if (_typeCostText != null && result != null)
            {
                var lines = new List<string>();
                foreach (var l in result.Breakdown) lines.Add($"{l.Label} {l.Value:0.0}");
                if (result.Grants.Count > 0)
                    lines.Add("获得 " + string.Join(" ", result.Grants.Select(kv => $"{(ManaType)kv.Key} {(int)kv.Value}")));
                lines.Add($"总档位 D={result.Total:0}");
                Describe(_typeCostText, "卡牌费用（自动算费，手动声明已退役）——构成：\n" + string.Join("\n", lines));
            }
        }

        // ---- 卡面放大（2026-11-06）：全屏遮罩 + 放大卡居中（DeckBuilder OpenZoom 同款） ----

        /// <summary>放大当前组合卡：遮罩点亮、卡面层池化挂入放大槽（点卡面不关闭——控制器给卡实例
        /// 挂的空 OnClick 吞点击；点遮罩/空白还原）。</summary>
        private void OpenZoom(CardData card)
        {
            if (card == null) return;
            EnsureZoomOverlay();
            if (_zoomOverlay == null || _zoomSlot == null) return;
            _zoomBindings.Clear();
            _zoomBindings.Add(new CardOverlayBinding
            {
                Slot = _zoomSlot,
                Item = CardOverlayItem.FromCardDataFull(card, "ComposerZoom"),
                Layout = CardOverlayLayout.Full,
            });
            CardOverlayController.Instance.Bind(_zoomBindings);
            if (_zoomSlot.childCount > 0)
                ((RectTransform)_zoomSlot.GetChild(0)).localScale = Vector2.one * ZoomCardScale;
            _zoomOverlay.SetAsLastSibling(); // 置顶（表单/弹层之上）
            _zoomOverlay.gameObject.SetActive(true);
            Debug.Log("[UI点击] 卡面放大（组合预览）");
        }

        /// <summary>放大遮罩懒建：半透明全屏底（Button=点按还原）+ 居中 360×648 放大槽。</summary>
        private void EnsureZoomOverlay()
        {
            if (_zoomOverlay != null) return;
            var go = new GameObject("zoom-overlay", typeof(RectTransform));
            _zoomOverlay = (RectTransform)go.transform;
            _zoomOverlay.SetParent(Root, false);
            UiKit.Stretch(_zoomOverlay);
            var mask = go.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.6f);
            mask.raycastTarget = true;
            var maskBtn = go.AddComponent<UnityEngine.UI.Button>();
            maskBtn.transition = UnityEngine.UI.Button.Transition.None;
            maskBtn.onClick.AddListener(CloseZoom);
            var slotGo = new GameObject("zoom-card", typeof(RectTransform));
            _zoomSlot = (RectTransform)slotGo.transform;
            _zoomSlot.SetParent(_zoomOverlay, false);
            _zoomSlot.anchorMin = _zoomSlot.anchorMax = _zoomSlot.pivot = new Vector2(0.5f, 0.5f);
            _zoomSlot.anchoredPosition = Vector2.zero;
            _zoomSlot.sizeDelta = new Vector2(360f, 648f);
            _zoomOverlay.gameObject.SetActive(false);
        }

        /// <summary>还原放大态：关遮罩+清绑定（UGUI 卡面层回收放大卡实例；未开过=空转）。</summary>
        private void CloseZoom()
        {
            if (_zoomOverlay == null || !_zoomOverlay.gameObject.activeSelf) return;
            _zoomBindings.Clear();
            CardOverlayController.ClearActive();
            _zoomOverlay.gameObject.SetActive(false);
        }

    }
}
