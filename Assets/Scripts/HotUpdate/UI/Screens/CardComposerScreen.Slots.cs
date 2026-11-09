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
        // ======================================== 载入编辑 / 新建 / 删除 ========================================

        /// <summary>载入已有卡编辑（覆盖替换语义；深拷贝容器、共享效果实例——效果合成器写回=整实例替换）。
        /// presetSource=true＝预设段预组卡（2026-10-31 参考效果库预设段）：另存通道——保存不触发
        /// 覆盖替换闸（源档=教学数据，改后内容哈希必换新 ID=另存新卡；被预组卡组引用不拦截）。</summary>
        private void LoadCardForEdit(CardData src, bool presetSource = false)
        {
            // 2026-11-05 ID 冻结：编辑既有卡带上原 id——保存走原地覆盖，卡组/存档引用不断。
            // 预设段=另存语义，故意清空 id 让其按内容重新分配。
            _card.ID = presetSource ? null : src.ID;
            _card.CardName = src.CardName;
            _card.Supertype = src.Supertype;
            _card.Illustration = src.Illustration;
            _card.Power = src.Power;
            _card.Life = src.Life;
            _card.Durability = src.Durability;
            _card.IsField = src.IsField && src.Supertype == Cardtype.Enchantment; // 场地标记（P8）：非结界脏数据不带入
            _card.Keywords = src.Keywords != null ? new List<string>(src.Keywords) : new List<string>();
            _card.Tags = src.Tags != null ? new List<string>(src.Tags) : new List<string>();
            _card.Effects = src.Effects != null ? new List<CardEffectData>(src.Effects) : new List<CardEffectData>();
            _card.Cost = src.Cost != null ? src.Cost.Clone() : null;
            //（卡级 PayloadCost 已随 2026-11-08 代价效果级化整删——代价随效果 Costs 走效果数据）
            _card.ArrowDirections = src.ArrowDirections;
            _card.LinkAuras = src.LinkAuras != null ? new List<LinkAuraData>(src.LinkAuras) : null;
            // 系统动作资格位（2026-11-06 opt-in）：攻/守仅生物带入（脏数据不带入——装载端同降级口径）
            _card.AttackAbility = src.AttackAbility && src.Supertype == Cardtype.Creature;
            _card.GuardAbility = src.GuardAbility && src.Supertype == Cardtype.Creature;
            _card.ChargeAbility = src.ChargeAbility;
            _card.SpeedBonus = src.SpeedBonus;
            _card.ResetCache();
            _card.AggregateEffectAuras(true);
            _editingOriginalId = src.ID;
            _editingPresetSource = presetSource;
            _kind = MapKind(src.Supertype);
            ResetSlotState(); // 槽位选中/空槽随载入复位（追加态）

            // 载入归一（2026-10-16 槽位化）：生物属性 null 视作 0（必填口径只看输入框留空）；
            // 非生物关键词违规旧卡直接清空（关键词=生物专属），防隐藏区无法移除的死锁
            bool purgedKeywords = _kind != CardKind.Creature && _card.Keywords.Count > 0;
            if (purgedKeywords) _card.Keywords = new List<string>();
            if (_kind == CardKind.Creature)
            {
                _card.Power = _card.Power ?? 0;
                _card.Life = _card.Life ?? 0;
            }

            SyncNameInput();
            SyncKindButtons();
            RebuildCardPanels();
            ShowToast((purgedKeywords ? "该卡带本体关键词——已按「关键词为生物专属」清空；" : "")
                + $"编辑：{_card.CardName ?? src.ID}"
                + (presetSource ? "（预设卡——保存＝另存新卡，源档不动）" : "（保存将覆盖替换旧卡）"));
        }

        private void OnNewCard()
        {
            _card.ID = null; // 2026-11-05 ID 冻结：新建卡无 id，保存时按内容指纹分配
            _card.CardName = $"新建·{++_newCardSeq}"; // 默认名（2026-10-23 定案：不补零、无上限）；玩家可改
            _card.Supertype = Cardtype.Creature;
            _card.Illustration = null; // 立绘复位（画廊高亮随 RefreshCardFace 对账）
            _card.Power = 0;
            _card.Life = 0;
            _card.Durability = 0;
            _card.IsField = false; // 场地标记复位（P8）
            _card.Keywords = new List<string>();
            _card.Tags = new List<string>();
            _card.Effects = new List<CardEffectData>();
            _card.Cost = null;
            //（卡级 PayloadCost 已随 2026-11-08 代价效果级化整删）
            _card.ArrowDirections = default;
            _card.LinkAuras = null;
            _card.AttackAbility = _card.GuardAbility = _card.ChargeAbility = false; // 资格位复位（2026-11-06 opt-in：新建默认不带）
            _card.SpeedBonus = 0;
            _card.ResetCache();
            _editingOriginalId = null;
            _editingPresetSource = false;
            _kind = CardKind.Creature;
            ResetSlotState(); // 槽位选中/空槽复位（追加态）

            SyncNameInput();
            SyncKindButtons();
            RebuildCardPanels();
        }

        /// <summary>槽位选中态与空槽全清（载入/新建复位；不触右栏刷新——调用方随全量刷新）。</summary>
        private void ResetSlotState()
        {
            _selType = null;
            _selIndex = -1;
            _emptySlots.Clear();
        }

        private void RebuildCardPanels()
        {
            SyncZoneVisibility();
            SyncBasicAbility();
            BuildArtGallery(); // 画廊高亮随载入/新建对账（类型换源后的统一收口）
            RefreshSlots();
            SetAttrTexts(); // 攻/血留空口径：新建空、载入回显
            _card.AggregateEffectAuras(true);
            Recalculate();
            RefreshPool();
        }

        /// <summary>删除卡池中的当前卡（2026-10-16 强制删除机制定案）：无引用直接删；
        /// 被卡组引用→TipPanel 提示+允许强制（2026-10-18 弹窗统一）——强删后卡组断链，卡组界面按
        /// DeckCompositionValidator.MissingIds 标记「不可用」。</summary>
        private void OnDeleteCard()
        {
            if (_editingPresetSource)
            {
                ShowToast("预设卡为教学数据源档，不可删——载入改动保存即另存新卡");
                return;
            }
            if (string.IsNullOrEmpty(_editingOriginalId))
            {
                ShowToast("未在编辑已有卡——先在右栏「卡池」点击要删的卡载入，再点删除");
                return;
            }
            var refs = ReferenceAudit.DecksReferencingCard(_editingOriginalId);
            if (refs.Count == 0)
            {
                DeleteCurrentCard(0);
                return;
            }
            ConfirmForceDeleteCardAsync(refs).Forget();
        }

        /// <summary>强删确认（TipPanel 双键）：取消/拆屏=null/否=不动。</summary>
        private async UniTaskVoid ConfirmForceDeleteCardAsync(List<string> refs)
        {
            if (await Tips.ShowAsync(
                    $"「{_card.CardName}」被 {refs.Count} 个卡组引用：\n{string.Join("、", refs)}\n\n"
                    + "强制删除后这些卡组将断链（卡组界面会标记「断链·不可用」）。",
                    "强制删除", "取消") != true)
                return;
            DeleteCurrentCard(refs.Count);
        }

        private void DeleteCurrentCard(int brokenDecks)
        {
            if (!CardConfigSerializer.Remove(_editingOriginalId))
            {
                ShowToast("删除失败：卡表中未找到该 ID（可能已被外部修改——重进界面刷新卡池）");
                return;
            }
            CardCatalog.Invalidate();
            CorpusCache.Clear();
            TutorialCreationFlow.NotifyCardsChanged(); // 第三课走查步检测（未开课零行为）
            var deleted = _card.CardName;
            OnNewCard();
            RefreshLibrary();
            ShowToast(brokenDecks > 0
                ? $"已删除「{deleted}」——{brokenDecks} 个卡组引用断链，卡组界面已标记不可用"
                : $"已从卡表删除「{deleted}」");
        }

        // ======================================== 中栏表单逻辑 ========================================

        private static CardKind MapKind(Cardtype type)
        {
            switch (type)
            {
                case Cardtype.Spell: return CardKind.Spell;
                case Cardtype.Enchantment: return CardKind.Enchantment;
                default: return CardKind.Creature;
            }
        }

        // ---------- 业务类型 → 后端字段 ----------
        // 设置 Supertype；清掉与新类型无关的额外字段（保持模型干净）。
        private void ApplyKindToCard()
        {
            switch (_kind)
            {
                case CardKind.Creature:
                    _card.Supertype = Cardtype.Creature;
                    _card.Durability = 0; // 耐久是结界专属
                    _card.IsField = false; // 场地标记是结界专属（P8）
                    _card.SpeedBonus = 0; // 字段已退役（2026-10-15 改版：速度动态算不落字段）
                    if (_card.Illustration == "Title") _card.Illustration = null; // Title=法术/结界默认图，生物清回占位
                    // 生物禁挂连接光环（2026-10-15 改版）：切类型即清光环与箭头（防旧数据残留）
                    _card.LinkAuras = new List<LinkAuraData>();
                    _card.ArrowDirections = HexDirection.None;
                    break;
                case CardKind.Spell:
                    _card.Supertype = Cardtype.Spell;
                    _card.Power = null;
                    _card.Life = null;
                    _card.Durability = 0;
                    _card.IsField = false; // 场地标记是结界专属（P8）
                    _card.AttackAbility = _card.GuardAbility = false; // 攻/守=生物专属（2026-11-06 opt-in，切型即清）
                    _card.SpeedBonus = 0;
                    _card.Illustration = "Title"; // 2026-11-06 默认图定案：法术自动选 Title（占位项可清空）
                    _card.LinkAuras = new List<LinkAuraData>(); // 法术只挂主动效果
                    _card.ArrowDirections = HexDirection.None;
                    _card.Keywords = new List<string>(); // 关键词=生物专属（2026-10-16 槽位化）
                    break;
                case CardKind.Enchantment:
                    _card.Supertype = Cardtype.Enchantment;
                    _card.Power = null;
                    _card.Life = null; // 结界=耐久体（2026-09-24 定案），不用攻/血
                    _card.AttackAbility = _card.GuardAbility = false; // 攻/守=生物专属（2026-11-06 opt-in，切型即清）
                    _card.SpeedBonus = 0; // 速度通道退役（2026-10-15 改版：结界禁主动，免费点全投耐久）
                    _card.Illustration = "Title"; // 2026-11-06 默认图定案：结界自动选 Title（占位项可清空）
                    _card.Keywords = new List<string>(); // 关键词=生物专属（2026-10-16 槽位化）
                    break;
            }
        }

        private bool HasStats => _kind == CardKind.Creature;
        private bool HasDurability => _kind == CardKind.Enchantment;

        // ======================================== 效果槽（effect 区=主动 / passives 区=其余） ========================================

        /// <summary>主动效果判据（CardOverlayItem.FromCardDataFull 同源）：首个主动上 effect 区，其余归被动。</summary>
        private static bool IsActiveEffect(CardEffectData fx)
        {
            if (fx == null) return false;
            var t = (TriggerTiming)fx.TriggerTiming;
            return t == TriggerTiming.Activate_Active || t == TriggerTiming.Activate_Instant;
        }

        /// <summary>效果级净费（2026-11-06 方格改版数据源）：带效果级代价的主动效果=发动费−抵扣
        ///（EffectActivationGate.NetBill 单源：先灰后最高费用色、逐色 ≥0）；无代价行 null
        ///（调用方回落锚价/模式费账单）。原「发动费→抵扣→净费」公式文本随本改版退役——
        /// 信息由 CostSquares 双组承载（费用组=净费、代价组=EffectPayloadCostOf）。</summary>
        internal static ElementCost ActiveEffectNetCost(CardEffectData fx)
        {
            var (fee, pts, net, atom) = ActiveEffectCostParts(fx);
            return fee == null ? null : net;
        }

        /// <summary>效果级代价分色面 → ElementCost（Cost2 代价组数据源，2026-11-06）：经 ConvertOne
        /// 契约转换（违约行剔除同运行时）→ PayloadGrantByColor 按原子可产出色记账。
        /// null=无效果级代价（调用方回落卡级施放代价 PayloadCost）。</summary>
        internal static ElementCost EffectPayloadCostOf(CardEffectData fx)
        {
            if (fx?.Costs == null) return null;
            var def = CardEffectConverter.ConvertOne(fx, "COMPOSER_COST_PREVIEW");
            var special = def?.Costs != null
                ? def.Costs.Where(c => c != null && c.Type != CostType.ElementConsume).ToList()
                : null;
            if (special == null || special.Count == 0) return null;
            var grant = CardCostService.PayloadGrantByColor(special);
            if (grant == null || grant.Count == 0) return null;
            var cost = new ElementCost();
            foreach (var kv in grant) cost[kv.Key] = kv.Value;
            return cost.IsZero ? null : cost;
        }

        /// <summary>P5 净费四段（发动费/抵扣点/净费/首个代价原子）：数据口=fx.Costs 首个 payload 条目，经
        /// CardEffectConverter 装载（契约校验同运行时）→ EffectActivationGate 抵扣；无代价行 fee=null。
        /// internal（P4 2026-10-29）：ACard 卡面内联同源复用。</summary>
        internal static (ElementCost fee, int pts, ElementCost net, CardCore.AtomicEffectInstance atom) ActiveEffectCostParts(CardEffectData fx)
        {
            if (!IsActiveEffect(fx) || fx?.Costs == null) return (null, 0, null, null);
            // payload/payloads 两形态同收（2026-11-06 修复：此前只认 legacy ce.payload——
            // 新形态 payloads 的代价行文本有、净费公式/抵扣读数恒缺）
            bool hasPayload = false;
            foreach (var ce in fx.Costs)
            {
                var ps = ce?.AllPayloads();
                if (ps == null) continue;
                foreach (var p in ps)
                    if (p != null && !string.IsNullOrEmpty(p.refId)) { hasPayload = true; break; }
                if (hasPayload) break;
            }
            if (!hasPayload) return (null, 0, null, null);
            var def = CardEffectConverter.ConvertOne(fx, "COMPOSER_NET_PREVIEW");
            var special = def?.Costs != null
                ? def.Costs.Where(c => c != null && c.Type != CostType.ElementConsume).ToList()
                : null;
            if (special == null || special.Count == 0) return (null, 0, null, null); // 契约违约被剔除——不显示
            int pts = EffectActivationGate.DeductionPoints(special);
            if (pts <= 0) return (null, 0, null, null);
            var fee = CostDerivationService.DeriveElementCosts(def, 0);
            var atom = special.SelectMany(c => c.Payloads ?? new List<CardCore.AtomicEffectInstance>())
                .FirstOrDefault(a => a != null);
            return (fee, pts, EffectActivationGate.NetBill(fee, special), atom);
        }

        /// <summary>槽行重建（2026-10-16 二次改版）：四类槽行=克隆对应单槽模板（text 标题/des 内容/delete 删除钮）；
        /// 已填槽在前、空槽在后；选中槽=白描边+行底色。效果全文写进 des。</summary>
        private void RefreshSlots()
        {
            EnsureCardLists();
            if (_editorRoot == null) return;
            // 删槽后选中下标越界回收（空槽 -1 不动）
            if (_selIndex >= 0)
            {
                if (_selType == ZoneId.Effect || _selType == ZoneId.Passives)
                {
                    if (_selIndex >= _card.Effects.Count) _selIndex = -1;
                }
                else if (_selType == ZoneId.Keys && _selIndex >= _card.Keywords.Count)
                {
                    _selIndex = -1;
                }
            }
            ClearRuntimeChildren(FormContent());
            BuildSlots(ZoneId.Effect); // cost 区已随 2026-11-06 代价挪构筑期退役（模板删除）
            BuildSlots(ZoneId.Passives);
            BuildSlots(ZoneId.Keys);
            SyncAddSlotButtons();
            RefreshPreview(); // 卡面效果行随槽内容同拍（多数路径随后还有 Recalculate 兜底）
        }

        /// <summary>逐槽行渲染：已填（数据下标）在前、空槽（_emptySlots）在后；
        /// 与卡型不匹配的已填槽仍渲染（可删——保存闸兜底拦）。</summary>
        private void BuildSlots(ZoneId id)
        {
            if (!_slotTemplates.TryGetValue(id, out var tpl) || tpl == null) return;
            int filled = FilledSlotCount(id);
            int empties = EmptySlotCount(id);
            if (filled == 0 && empties == 0) return;
            int ordinal = 0;
            for (int i = 0; i < filled; i++) MakeSlotRow(id, tpl, i, ordinal++);
            for (int e = 0; e < empties; e++) MakeSlotRow(id, tpl, -1, ordinal++);
        }

        /// <summary>中栏表单滚动内容（2026-11-06 布局改版：CardPreView 内联 form-scroll/viewport/content）——
        /// 槽模板与克隆行的共同父（与 ACard 卡面实例的内部 form-scroll 是两棵树）。</summary>
        private RectTransform FormContent()
        {
            if (_formContent == null && _editorRoot != null)
                _formContent = _editorRoot.Find("form-scroll/viewport/content") as RectTransform;
            return _formContent;
        }

        /// <summary>单槽行（克隆模板）：text=序号标题、des=效果全文/关键词/代价文案（空槽=操作提示）、
        /// delete=删除（已填删数据/空槽删占位）；行点击=选中该槽（右栏按槽类型过滤，点选候选填入）。</summary>
        private void MakeSlotRow(ZoneId id, RectTransform tpl, int dataIndex, int ordinal)
        {
            var formCt = FormContent();
            var row = formCt != null ? UiKit.CloneTemplateFrom(formCt, tpl.name, formCt) : null;
            if (row == null) return;
            row.name = "rt-slot";
            // 紧跟同类型模板之后排布（四类分组：cost→effect→passives→keys，add-slot 恒末位）
            row.SetSiblingIndex(tpl.GetSiblingIndex() + 1 + ordinal);

            bool selected = _selType == id && _selIndex == dataIndex;
            var img = row.GetComponent<Image>();
            if (img != null && selected) img.color = UiStyle.SelectedRowBg;
            var ol = row.GetComponent<Outline>();
            if (ol != null)
            {
                if (selected)
                {
                    ol.effectColor = UiStyle.TextPrimary; // 选中=白描边
                    ol.enabled = true;
                }
                else if (_slotOutlineBase.TryGetValue(id, out var b))
                {
                    ol.effectColor = b.color;
                    ol.enabled = b.on;
                }
            }

            var title = UiKit.FindDeep(row, "text")?.GetComponent<TMP_Text>();
            if (title != null) title.text = SlotTitle(id, ordinal);
            var des = UiKit.FindDeep(row, "des")?.GetComponent<TMP_Text>();
            if (des != null)
            {
                des.text = SlotDesText(id, dataIndex);
                des.color = dataIndex < 0 ? UiStyle.TextHint
                    : (!TypeEnabledForKind(id) ? UiStyle.ErrorRed : UiStyle.TextBody);
            }

            var del = UiKit.FindDeep(row, "delete")?.GetComponent<UnityEngine.UI.Button>();
            if (del != null)
            {
                var capturedIndex = dataIndex;
                del.onClick.RemoveAllListeners();
                del.onClick.AddListener(() =>
                {
                    Debug.Log("[UI点击] 槽delete " + id + " #" + (capturedIndex + 1));
                    DeleteSlot(id, capturedIndex);
                });
                Describe(del, capturedIndex < 0
                    ? $"删除这个空{TypeZh(id)}槽（未填内容，不影响卡数据）"
                    : $"删除该{TypeZh(id)}槽内容（卡面光环随剩余内容重算；要改内容=删后重挂）");
            }

            var btn = row.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                var capturedIndex = dataIndex;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    Debug.Log("[UI点击] 槽行选中 " + id + " #" + (capturedIndex + 1));
                    SelectSlot(id, capturedIndex);
                });
            }
            Describe((Component)btn ?? row, SlotRowDesc(id, dataIndex));
            BindSetDerivant(row, dataIndex); // 显隐=该效果含衍生物/变形原子（无钮模板空转）
        }

        private static string SlotTitle(ZoneId id, int ordinal) =>
            (id == ZoneId.Effect ? "主动槽" : id == ZoneId.Passives ? "自动槽" : "关键词")
            + (char)('①' + ordinal) + "：";

        /// <summary>槽行 des 内容：已填=渲染摘要单遍（效果名即全文定案下不再名+摘要并列）/关键词；
        /// 空槽=操作提示。与卡型不匹配的已填槽前缀⚠（保存闸兜底）。
        ///（代价槽已随 2026-11-08 代价效果级化整退役——代价=效果 Costs，编辑在效果合成器。）</summary>
        private string SlotDesText(ZoneId id, int dataIndex)
        {
            if (dataIndex < 0)
                return $"（空槽）点击行选中 → 右栏按「{TypeZh(id)}」过滤 → 点候选项填入此槽";
            string body;
            if (id == ZoneId.Keys)
            {
                body = KeywordZh(_card.Keywords[dataIndex]);
            }
            else
            {
                var fx = _card.Effects[dataIndex];
                var name = string.IsNullOrEmpty(fx?.DisplayName) ? $"效果 #{dataIndex + 1}" : fx.DisplayName;
                // des 只显渲染摘要单遍（2026-11-06 修复双写）：效果名=保存时渲染全文（效果合成器
                // 2026-10-03 定案 CurrentDisplayName=RenderEffectSummary）——名+摘要并列把同一效果
                // 写两遍，渲染文案改版后两遍句式还会分叉（「；若」旧名 vs「，如果」新析）。
                // 与卡面 FromCardDataFull/卡池行同源；空摘要（无步骤）回落名字。
                string prefix = id == ZoneId.Passives && IsAuraEffect(fx) ? "[光环] " : "";
                var summary = fx == null ? "" : AtomText.RenderEffectSummary(
                    new EffectGraphData(name) { header = fx, steps = fx.Steps }, _card);
                body = string.IsNullOrEmpty(summary) ? prefix + name : prefix + summary;
                // 卡级代价显示口已随 2026-11-06「代价全面并入效果级」定案退役——效果级 Costs 的
                // 「，【代价】…」由 RenderEffectSummary 拼进 summary；净费/代价读数在中栏 ACard 行级方格
            }
            return TypeEnabledForKind(id) ? body : "⚠与本卡类型不匹配（保存会被拦）——删除或换型\n" + body;
        }

        private string SlotRowDesc(ZoneId id, int dataIndex) => dataIndex < 0
            ? $"选中这个空{TypeZh(id)}槽：右栏自动按「{TypeZh(id)}」过滤候选，点选填入此槽"
            : id switch
            {
                ZoneId.Keys => "关键词槽：选中后右栏列出关键词目录（点选=替换）",
                _ => "效果槽：选中后右栏列出对应形态的效果（点选=整实例替换此槽）",
            };

        /// <summary>槽删除：已填=删数据（效果/关键词），空槽=回收占位。
        ///（代价槽已随 2026-11-08 代价效果级化整退役——代价随效果删除。）</summary>
        private void DeleteSlot(ZoneId id, int dataIndex)
        {
            if (dataIndex < 0)
            {
                _emptySlots[id] = Mathf.Max(0, EmptySlotCount(id) - 1);
                if (_selType == id && _selIndex < 0) DeselectSlot();
                else RefreshSlots();
                return;
            }
            switch (id)
            {
                case ZoneId.Effect:
                case ZoneId.Passives:
                    RemoveAttachedAt(dataIndex); // 内部已刷新+重算
                    return;
                default:
                    _card.Keywords.RemoveAt(dataIndex);
                    _card.ResetCache();
                    break;
            }
            if (_selType == id && _selIndex == dataIndex) DeselectSlot();
            else RefreshSlots();
            Recalculate();
        }

        /// <summary>填入成功后回收一个该类型空槽（选中空槽填入=显式消费；全量视图追加=兜底回收防空槽累计超容）。</summary>
        private void ConsumeEmptySlot(ZoneId id)
        {
            _emptySlots[id] = Mathf.Max(0, EmptySlotCount(id) - 1);
        }

        private void RemoveAttachedAt(int index)
        {
            if (index < 0 || index >= _card.Effects.Count) return;
            _card.Effects.RemoveAt(index);
            // 光环随效果回收：移除效果后按剩余效果重算卡面箭头+光环
            _card.AggregateEffectAuras(true);
            _card.ResetCache();
            if ((_selType == ZoneId.Effect || _selType == ZoneId.Passives) && _selIndex == index) DeselectSlot();
            else RefreshSlots();
            Recalculate();
        }

        // ======================================== 代价栏（已整退役 2026-11-08 代价效果级化） ========================================
        //（FillPayloadCost/CostEligibleRow/CostRowPreview/BuildLibraryPayloadView/_costParamValue
        //  随卡级代价整删——代价唯一编辑口=效果合成器有限分支「支付代价」门，数据=CardEffectData.Costs；
        //  代价行文案/抵扣读数随效果行渲染（EffectPayloadCostOf 行级代价组）。）

        /// <summary>代价全价合计（逐原子 PayloadUnitGrant 求和——门槛/补偿同锚）。</summary>
        private static int PayloadTotalGrant(List<CardCore.AtomicEffectEntry> atoms)
        {
            int total = 0;
            foreach (var e in atoms ?? new List<CardCore.AtomicEffectEntry>())
            {
                var inst = CardEffectConverter.ConvertPayloadForDisplay(e);
                if (inst != null) total += CostDerivationService.PayloadUnitGrant(inst);
            }
            return total;
        }

        // ======================================== 关键词（keys 槽；生物专属） ========================================

        // 关键词区随 2026-10-16 二次改版并入统一槽行渲染（tpl-kwchip chip 模板已随 prefab 消失）：
        // 关键词=keys 类型槽行（克隆 keys 模板），删除走槽行 delete，添加走 addkeys 新建空槽+右栏目录点选。

        /// <summary>右栏关键词候选行点击 → 填入选中关键词槽（选中已填槽=替换；空槽/未选=追加；
        /// 去重+容量闸；保存时以 effectIds 直接引用原子 refId）。</summary>
        private void FillKeyword(KeywordCatalogEntry kw)
        {
            if (kw == null || string.IsNullOrEmpty(kw.Id)) return;
            if (_kind != CardKind.Creature)
            {
                ShowToast("关键词为生物专属——法术/结界不可装关键词");
                return;
            }
            bool replacing = _selType == ZoneId.Keys && _selIndex >= 0 && _selIndex < _card.Keywords.Count;
            if (_card.Keywords.Where((k, i) => i != (_selType == ZoneId.Keys ? _selIndex : -1)).Contains(kw.Id))
            {
                ShowToast($"已有关键词：{kw.DisplayName}");
                return;
            }
            if (!replacing && _card.Keywords.Count + 1 > KeywordCapacity)
            {
                ShowToast($"关键词槽已满（{_card.Keywords.Count}/{KeywordCapacity}）——可关闭攻击/守卫资格腾出槽位");
                return;
            }
            if (replacing) _card.Keywords[_selIndex] = kw.Id;
            else _card.Keywords.Add(kw.Id);
            _card.ResetCache();
            ConsumeEmptySlot(ZoneId.Keys);
            RefreshSlots();
            Recalculate();
            ShowToast(replacing ? $"已替换关键词槽 {_selIndex + 1}：{kw.DisplayName}" : $"已添加：{kw.DisplayName}");
        }

        // ========================================（系统效果直加段已删 2026-11-05） ========================================

        // 充能直加行（AddChargeDirectRow/FillChargeDirect）已随 2026-11-05 充能系统动作化改版删除：
        // 充能不再是卡上效果——改为任意手牌卡可宣的速度0系统动作（引擎 GameActions.DeclareCharge，
        // 结算期反问元素类别），卡组合界面不再提供充能挂载。

    }
}
