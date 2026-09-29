using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using UnityEngine;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌合成界面（2026-09-24 重做）——三栏：左=卡池预览（生物/法术分组，颜色过滤+搜索+
    /// 全局排序），中=卡牌表单（类型/攻血/耐久/代价栏/算费），右=挂载效果+关键词。
    ///
    /// 数据模型（2026-09-24 关键词引用化定案）：卡只做卡层增量（名称/类型/身材/耐久/声明费/代价栏），
    /// 效果与关键词全部走 effectIds 引用——**直接引用原子 refId=本体关键词**（本界面关键词区），
    /// 引用 Effects.json 组合效果=效果挂载（其中的赋予族=赋予关键词，赋予亦可作用于自己）。
    /// 编辑旧卡保存=覆盖替换（删旧存新；旧卡被卡组引用则拦截——内容哈希 ID 机制下无法原地覆盖）。
    /// 超模校验已按 2026-09-24 定案移除——卡层只算减费与建议档位，不做 D≤C 合规判断。
    /// </summary>
    public sealed class CardComposerScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/CardComposer";

        // 业务类型（UI 维度，非裸 Cardtype）：法术分为瞬间（常规魔法）与结界（耐久体）。
        private enum CardKind { Creature, Spell, Enchantment }

        private static readonly (CardKind kind, string name)[] KindNames =
        {
            (CardKind.Creature, "生物"), (CardKind.Spell, "瞬间"), (CardKind.Enchantment, "结界"),
        };

        private readonly CardData _card = new CardData
        {
            CardName = "新卡牌",
            Supertype = Cardtype.Creature,
            Power = 0,
            Life = 0,
        };

        /// <summary>覆盖替换（2026-09-24 定案）：正在编辑的旧卡 ID；null=新建。效果合成器往返期间保留。</summary>
        private string _editingOriginalId;

        private CardKind _kind = CardKind.Creature;
        private UIColor _colorFilter = UIColor.All;

        private ScrollView _poolList;
        private ScrollView _attachedList;
        private ScrollView _breakdownList;
        private VisualElement _dynamicForm;
        private VisualElement _payloadZone;
        private VisualElement _keywordZone;
        private VisualElement _poolFilter;
        private Label _toast;
        private Label _suggested;
        private Label _costLabel;
        private TextField _nameField;
        private TextField _tagsField;
        private TextField _searchField;
        private DropdownField _cardtypeDropdown;

        private Dictionary<int, float> _lastSuggestedCost;

        public override void OnEnter()
        {
            _poolList = Q<ScrollView>("list-pool");
            _attachedList = Q<ScrollView>("list-attached");
            _breakdownList = Q<ScrollView>("list-breakdown");
            _dynamicForm = Q<VisualElement>("dynamic-form");
            _payloadZone = Q<VisualElement>("payload-zone");
            _keywordZone = Q<VisualElement>("keyword-zone");
            _poolFilter = Q<VisualElement>("pool-filter");
            _toast = Q<Label>("lbl-toast");
            _suggested = Q<Label>("lbl-suggested");
            _costLabel = Q<Label>("lbl-cost");
            _nameField = Q<TextField>("field-card-name");
            _tagsField = Q<TextField>("field-tags");
            _searchField = Q<TextField>("field-search");
            _cardtypeDropdown = Q<DropdownField>("dropdown-cardtype");

            UIBinder.BindButton(Root, "btn-back", () => Manager.Back());
            UIBinder.BindButton(Root, "btn-save", OnSave);
            UIBinder.BindButton(Root, "btn-adopt", OnAdoptCost);
            UIBinder.BindButton(Root, "btn-new", OnNewCard);
            UIBinder.BindButton(Root, "btn-new-effect", () =>
            {
                ComposerSession.BeginCardEdit(_card, -1); // -1 = 新建（保存时追加）
                Manager.Show<EffectComposerScreen>();
            });
            _searchField.RegisterValueChangedCallback(_ => RefreshPool());

            EnsureCardLists();
            _kind = MapKind(_card.Supertype);
            BuildCardtypeDropdown();
            BuildPoolFilter();
            BuildDynamicForm();
            BuildPayloadZone(); // 代价栏（2026-09-23 上移卡组合层）
            BuildEffectLibraryZone();
            BuildKeywordZone();
            // 光环上移效果层（2026-09-23）：从效果合成器返回/重进时按挂载效果重算卡面箭头+光环
            _card.AggregateEffectAuras(true);
            RefreshAttached();
            Recalculate();
            RefreshPool();
        }

        // 容器空值防护（CardData 字段缺省可能为 null——CSV/加载路径都各自赋过，这里统一兜底）
        private void EnsureCardLists()
        {
            _card.Keywords ??= new List<string>();
            _card.Tags ??= new List<string>();
            _card.Effects ??= new List<CardEffectData>();
        }

        // ======================================== 左栏：卡池 ========================================

        private void BuildPoolFilter()
        {
            _poolFilter.Clear();
            foreach (UIColor color in Enum.GetValues(typeof(UIColor)))
            {
                var captured = color;
                var chip = new Button { text = ColorFilter.DisplayName(color), name = $"chip-{captured}" };
                chip.AddToClassList("chip");
                if (captured == UIColor.All) chip.AddToClassList("chip--all");
                else if (captured == UIColor.Red) chip.AddToClassList("chip--red");
                else if (captured == UIColor.Blue) chip.AddToClassList("chip--blue");
                else if (captured == UIColor.Green) chip.AddToClassList("chip--green");
                else if (captured == UIColor.Gray) chip.AddToClassList("chip--gray");
                if (captured == _colorFilter) chip.AddToClassList("chip--active");
                chip.clicked += () =>
                {
                    _colorFilter = captured;
                    BuildPoolFilter(); // 重建以刷新激活态
                    RefreshPool();
                };
                _poolFilter.Add(chip);
            }
        }

        private void RefreshPool()
        {
            _poolList.Clear();
            IEnumerable<CardData> cards = CardCatalog.LoadAll();
            if (_colorFilter != UIColor.All)
                cards = cards.Where(c => CardSorter.HasCostColor(c, _colorFilter));
            string query = _searchField != null ? _searchField.value : null;
            if (!string.IsNullOrWhiteSpace(query))
                cards = cards.Where(c => (c.CardName ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);

            var sorted = CardSorter.Sort(cards).ToList();
            if (sorted.Count == 0)
            {
                var empty = new Label("（无匹配卡牌）");
                empty.AddToClassList("hint");
                _poolList.Add(empty);
                return;
            }

            // 分组（2026-09-24 定案）：生物 / 法术（瞬间+结界）
            AddPoolGroup("生物", sorted.Where(c => c.Supertype == Cardtype.Creature).ToList());
            AddPoolGroup("法术（瞬间/结界）", sorted.Where(c => c.Supertype != Cardtype.Creature).ToList());
        }

        private void AddPoolGroup(string title, List<CardData> cards)
        {
            if (cards.Count == 0) return;
            var header = new Label($"{title}（{cards.Count}）");
            header.AddToClassList("panel__header");
            _poolList.Add(header);
            foreach (var card in cards)
            {
                var captured = card;
                _poolList.Add(MakePoolRow(captured));
            }
        }

        /// <summary>卡池预览行：费用徽标+名称+类型/身材，次行=关键词+效果摘要（AtomText 单一来源）。</summary>
        private VisualElement MakePoolRow(CardData card)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");
            row.style.flexDirection = FlexDirection.Column;
            if (!string.IsNullOrEmpty(_editingOriginalId) && card.ID == _editingOriginalId)
                row.AddToClassList("list-row--selected");

            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;

            var cost = new Label(((int)card.TotalCost).ToString());
            cost.AddToClassList("cost-badge");
            top.Add(cost);

            var name = new Label(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName);
            name.AddToClassList("list-row__name");
            name.style.flexGrow = 1;
            top.Add(name);

            var meta = new Label(TypeStatLine(card));
            meta.AddToClassList("list-row__meta");
            top.Add(meta);
            row.Add(top);

            var keywords = (card.Keywords ?? new List<string>())
                .Select(k => KeywordZh(k)).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var summaries = (card.Effects ?? new List<CardEffectData>())
                .Select(fx => AtomText.RenderEffectSummary(
                    new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }))
                .Where(s => !string.IsNullOrEmpty(s)).ToList();
            var detailParts = new List<string>();
            if (keywords.Count > 0) detailParts.Add(string.Join("·", keywords));
            if (summaries.Count > 0) detailParts.Add(string.Join("｜", summaries));
            if (detailParts.Count > 0)
            {
                var detail = new Label(string.Join("｜", detailParts));
                detail.AddToClassList("list-row__meta");
                detail.AddToClassList("single-line");
                row.Add(detail);
            }

            row.RegisterCallback<ClickEvent>(_ => LoadCardForEdit(card));
            return row;
        }

        private static string TypeStatLine(CardData card)
        {
            switch (card.Supertype)
            {
                case Cardtype.Creature:
                    return $"生物 {(card.Power ?? 0)}/{(card.Life ?? 0)}";
                case Cardtype.Enchantment:
                    return card.Durability > 0 ? $"结界 耐久{card.Durability}" : "结界";
                default:
                    return CardSorter.TypeName(card);
            }
        }

        private static string KeywordZh(string keywordId)
        {
            var def = CardLoader.GetKeywordDefinition(keywordId);
            return string.IsNullOrEmpty(def?.nameZh) ? keywordId : def.nameZh;
        }

        // ======================================== 载入编辑 / 新建 ========================================

        /// <summary>载入已有卡编辑（2026-09-24 定案：覆盖替换语义）。
        /// 深拷贝容器、共享效果实例——效果合成器写回=整实例替换（EffectComposerScreen L2326）、
        /// 入口即深拷贝，不污染卡池缓存实例；保存前池中原件不动。</summary>
        private void LoadCardForEdit(CardData src)
        {
            _card.CardName = src.CardName;
            _card.Supertype = src.Supertype;
            _card.Power = src.Power;
            _card.Life = src.Life;
            _card.Level = src.Level;
            _card.Subtype = src.Subtype;
            _card.Durability = src.Durability;
            _card.Keywords = src.Keywords != null ? new List<string>(src.Keywords) : new List<string>();
            _card.Tags = src.Tags != null ? new List<string>(src.Tags) : new List<string>();
            _card.Effects = src.Effects != null ? new List<CardEffectData>(src.Effects) : new List<CardEffectData>();
            _card.Cost = src.Cost != null ? new Dictionary<int, float>(src.Cost) : null;
            _card.PayloadCost = src.PayloadCost; // 单条 Payload：填装走整体替换
            _card.ArrowDirections = src.ArrowDirections;
            _card.LinkAuras = src.LinkAuras != null ? new List<LinkAuraData>(src.LinkAuras) : null;
            _card.NoAttack = src.NoAttack;
            _card.NoGuard = src.NoGuard;
            _card.SurplusToSpeed = src.SurplusToSpeed;
            _card.ResetCache();
            _card.AggregateEffectAuras(true);
            _editingOriginalId = src.ID;
            _kind = MapKind(src.Supertype);

            _nameField.SetValueWithoutNotify(_card.CardName ?? "");
            _tagsField.SetValueWithoutNotify(string.Join(",", _card.Tags ?? new List<string>()));
            _cardtypeDropdown.SetValueWithoutNotify(KindName(_kind));
            RebuildCardPanels();
            ShowToast($"编辑：{_card.CardName}（保存将覆盖替换旧卡）");
        }

        private void OnNewCard()
        {
            _card.CardName = "新卡牌";
            _card.Supertype = Cardtype.Creature;
            _card.Power = 0;
            _card.Life = 0;
            _card.Level = null;
            _card.Subtype = CardSubtype.None;
            _card.Durability = 0;
            _card.Keywords = new List<string>();
            _card.Tags = new List<string>();
            _card.Effects = new List<CardEffectData>();
            _card.Cost = null;
            _card.PayloadCost = null;
            _card.ArrowDirections = default;
            _card.LinkAuras = null;
            _card.NoAttack = _card.NoGuard = _card.SurplusToSpeed = false;
            _card.ResetCache();
            _editingOriginalId = null;
            _kind = CardKind.Creature;

            _nameField.SetValueWithoutNotify("新卡牌");
            _tagsField.SetValueWithoutNotify("");
            _cardtypeDropdown.SetValueWithoutNotify(KindName(_kind));
            RebuildCardPanels();
            ShowToast("已新建空白卡牌");
        }

        private void RebuildCardPanels()
        {
            BuildDynamicForm();
            BuildPayloadZone();
            BuildKeywordZone();
            _card.AggregateEffectAuras(true);
            RefreshAttached();
            Recalculate();
            RefreshPool();
        }

        // ======================================== 中栏：表单 ========================================

        private void BuildCardtypeDropdown()
        {
            _cardtypeDropdown.choices = KindNames.Select(k => k.name).ToList();
            _cardtypeDropdown.index = KindIndex(_kind);
            _cardtypeDropdown.RegisterValueChangedCallback(_ =>
            {
                int idx = _cardtypeDropdown.index;
                if (idx >= 0 && idx < KindNames.Length)
                {
                    _kind = KindNames[idx].kind;
                    ApplyKindToCard();
                    BuildDynamicForm();
                    Recalculate();
                }
            });
        }

        private static int KindIndex(CardKind kind)
        {
            for (int i = 0; i < KindNames.Length; i++)
                if (KindNames[i].kind == kind) return i;
            return 0;
        }

        private static string KindName(CardKind kind) => KindNames[KindIndex(kind)].name;

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
        // 设置 Supertype + Subtype；清掉与新类型无关的额外字段（保持模型干净）。
        private void ApplyKindToCard()
        {
            _card.Subtype = CardSubtype.None;
            _card.Level = null;

            switch (_kind)
            {
                case CardKind.Creature:
                    _card.Supertype = Cardtype.Creature;
                    _card.Durability = 0; // 耐久是结界专属
                    break;
                case CardKind.Spell:
                    _card.Supertype = Cardtype.Spell;
                    _card.Power = null;
                    _card.Life = null;
                    _card.Durability = 0;
                    break;
                case CardKind.Enchantment:
                    _card.Supertype = Cardtype.Enchantment;
                    _card.Power = null;
                    _card.Life = null; // 结界=耐久体（2026-09-24 定案），不用攻/血
                    break;
            }
        }

        private bool HasStats => _kind == CardKind.Creature;
        private bool HasLevel => _kind == CardKind.Creature;
        private bool HasDurability => _kind == CardKind.Enchantment;

        // ---------- 动态表单（按类型切换字段） ----------
        private void BuildDynamicForm()
        {
            _dynamicForm.Clear();

            // 攻 / 血
            if (HasStats)
            {
                var statRow = new VisualElement();
                statRow.AddToClassList("toolbar");
                statRow.Add(MakeLabeledInt("攻击", _card.Power ?? 0, v => { _card.Power = v; Recalculate(); }));
                statRow.Add(MakeLabeledInt("生命", _card.Life ?? 0, v => { _card.Life = v; Recalculate(); }));
                _dynamicForm.Add(statRow);
            }

            // 等级
            if (HasLevel)
            {
                var lvlRow = new VisualElement();
                lvlRow.AddToClassList("toolbar");
                lvlRow.Add(MakeLabeledInt("等级", _card.Level ?? 1, v => _card.Level = v));
                _dynamicForm.Add(lvlRow);
            }

            // 耐久（结界专属，2026-09-24 定案：类似生物生命、被攻击每次仅损失 1 点）
            if (HasDurability)
            {
                var durRow = new VisualElement();
                durRow.AddToClassList("toolbar");
                durRow.Add(MakeLabeledInt("耐久", _card.Durability, v => { _card.Durability = v; Recalculate(); }));
                _dynamicForm.Add(durRow);
            }
        }

        // 带标签的整数输入（横排）。
        private VisualElement MakeLabeledInt(string label, int value, Action<int> onChanged)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("toolbar");
            wrap.style.marginBottom = 0;
            var lbl = new Label(label);
            lbl.AddToClassList("field-label");
            wrap.Add(lbl);
            var field = new IntegerField();
            field.AddToClassList("int-input");
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            wrap.Add(field);
            return wrap;
        }

        // ---------- 代价栏（2026-09-23 定案：上移卡组合层）----------
        // 单卡单条 Payload：填装时作用域改写错侧（有益→对手 / 有害→己方），限 1 费；
        // cast 付费步强制执行并按全价补偿黑/白（CollectCardSpecialCosts 读 CardData.PayloadCost）。

        private void BuildPayloadZone()
        {
            _payloadZone.Clear();
            var header = new Label("代价（错侧作用·限 1 费·单卡单条）");
            header.AddToClassList("panel__header");
            _payloadZone.Add(header);

            var pe = _card.PayloadCost?.payload;
            if (pe != null && !string.IsNullOrEmpty(pe.refId))
            {
                var row = new VisualElement();
                row.AddToClassList("toolbar");
                row.style.flexWrap = Wrap.Wrap;
                var name = new Label(AtomText.RenderAtomEntry(pe));
                name.AddToClassList("list-row__name");
                row.Add(name);
                var price = new Label(PayloadPriceText(pe));
                price.AddToClassList("hint");
                row.Add(price);
                var del = new Button(() =>
                {
                    _card.PayloadCost = null;
                    _card.ResetCache();
                    BuildPayloadZone();
                    Recalculate();
                }) { text = "移除" };
                del.AddToClassList("btn");
                del.AddToClassList("btn--mini");
                del.AddToClassList("btn--danger");
                row.Add(del);
                _payloadZone.Add(row);
                return;
            }

            // 未填装：候选下拉（与 FillPayloadCost 同口径的表级过滤——错边可达·默认 1 费）
            var rows = PayloadCandidates().ToList();
            var choices = new List<string> { "（选择代价原子）" };
            choices.AddRange(rows.Select(r => r.DisplayName));
            var dd = new DropdownField("填装") { choices = choices };
            dd.AddToClassList("text-input");
            dd.index = 0;
            dd.RegisterValueChangedCallback(_ =>
            {
                int i = dd.index - 1;
                if (i >= 0 && i < rows.Count)
                    FillPayloadCost(new AtomicEffectEntry { refId = rows[i].HashId, value = 1 });
            });
            _payloadZone.Add(dd);
        }

        /// <summary>填装代价栏（自效果合成器 FillCostSlot 移植，2026-09-23）：**作用单位改写错侧 + 限 1 费**。
        /// 改写规则（与装载期 WrongSide/SideLock 同口径）：有益(p&gt;0)→实例域取表域的对手侧成员、
        /// 有害(p&lt;0)→取己方侧成员（表域无该侧成员=该原子无法作用于错误对象，拒）；
        /// 中性(p=0)须表域单侧锁定（双侧/无目标=既非代价也非收益，拒）。
        /// 限价：PayloadUnitGrant &gt;1 放置口拦截（2026-09-21 定案）。</summary>
        private void FillPayloadCost(AtomicEffectEntry entry)
        {
            var cfg = AtomicEffectTable.GetByHashId(entry?.refId);
            if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type))
            {
                ShowToast("原子表引用缺失——无法作代价");
                return;
            }
            if (ComposerCatalog.IsEngineTrunk(type)) { ShowToast("引擎主干（拼点/运势/倒计时）不可作代价"); return; }
            if (ComposerCatalog.HasMountBit(cfg, MountKind.Keyword))
            { ShowToast("关键词类原子不可作代价（代价位不出现关键词）"); return; }

            var kinds = cfg.GetTargetKindList();
            float p = Mathf.Clamp(cfg.Polarity, -1f, 1f);
            if (p != 0f)
            {
                bool wantEnemy = p > 0f; // 有益→对手侧 / 有害→己方侧
                var side = kinds.Where(k => TargetKindRules.IsEnemySide(k) == wantEnemy).ToList();
                if (side.Count == 0)
                {
                    ShowToast($"该原子的作用域没有{(wantEnemy ? "对手" : "己方")}侧单位——无法作用于错误对象，不可作代价");
                    return;
                }
                entry.kinds = side.Count == kinds.Count ? null : side; // 表域恰为整侧=免写（表默认同效）
            }
            else
            {
                if (CostDerivationService.SideLock(kinds) == 0)
                {
                    ShowToast("中性原子须单侧域锁定才可作代价（双侧域/无目标=既非代价也非收益）");
                    return;
                }
                entry.kinds = null; // 表默认即单侧锁
            }

            // 构筑期限价（2026-09-21 定案：只允许装形成 1 费的代价）——放置口拦截
            var inst = CardEffectConverter.ConvertPayloadForDisplay(entry);
            int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            if (price > 1)
            {
                ShowToast($"该原子作代价将形成 {price} 费——构筑期只允许 1 费（调低数值或换原子）");
                return;
            }

            _card.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = entry };
            _card.ResetCache();
            BuildPayloadZone();
            Recalculate();
            ShowToast($"已填装代价（{price} 费·错侧作用）——打出时付费步强制执行并按全价补偿黑/白");
        }

        /// <summary>代价候选行（表级）：非引擎/非关键词 + 错侧可达（p≠0 有错侧成员 / p=0 单侧锁）
        /// + 默认值(value=1)全价 ≤1——下拉只列真正可装的原子。</summary>
        private static IEnumerable<AtomicEffectConfig> PayloadCandidates()
        {
            foreach (var row in AtomicEffectTable.GetAll())
            {
                if (row == null || string.IsNullOrEmpty(row.EnumName)) continue;
                if (!Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) continue;
                if (ComposerCatalog.IsEngineTrunk(type)) continue;
                if (ComposerCatalog.HasMountBit(row, MountKind.Keyword)) continue;

                var kinds = row.GetTargetKindList();
                float p = Mathf.Clamp(row.Polarity, -1f, 1f);
                bool sideOk = p != 0f
                    ? kinds.Any(k => TargetKindRules.IsEnemySide(k) == (p > 0f))
                    : CostDerivationService.SideLock(kinds) != 0;
                if (!sideOk) continue;

                var inst = CardEffectConverter.ConvertPayloadForDisplay(
                    new AtomicEffectEntry { refId = row.HashId, value = 1 });
                int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
                if (price > 1) continue;
                yield return row;
            }
        }

        /// <summary>代价全价展示行（限 1 费——口径同装载期 PayloadUnitGrant）。</summary>
        private static string PayloadPriceText(AtomicEffectEntry entry)
        {
            var inst = CardEffectConverter.ConvertPayloadForDisplay(entry);
            int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            return price <= 1
                ? $"全价：{price} / 限 1 费"
                : $"全价：{price}——超限（构筑期只允许 1 费代价，保存前请调低数值或换原子）";
        }

        // ======================================== 右栏：效果挂载 ========================================

        /// <summary>效果库挂载下拉（2026-09-24 布局重做：大列表收成下拉，能力不减）。</summary>
        private void BuildEffectLibraryZone()
        {
            var zone = Q<VisualElement>("effect-library-zone");
            zone.Clear();
            var graphs = EffectLibrarySerializer.LoadAll();
            var choices = new List<string> { "（从效果库挂载）" };
            choices.AddRange(graphs.Select(g => string.IsNullOrEmpty(g.name) ? "(未命名)" : g.name));
            var dd = new DropdownField("效果库") { choices = choices };
            dd.AddToClassList("text-input");
            dd.index = 0;
            dd.RegisterValueChangedCallback(_ =>
            {
                int i = dd.index - 1;
                if (i >= 0 && i < graphs.Count)
                {
                    AttachEffect(graphs[i]);
                    dd.SetValueWithoutNotify(choices[0]);
                }
            });
            zone.Add(dd);
        }

        private void AttachEffect(EffectGraphData graph)
        {
            _card.Effects.Add(SnapshotEffect(graph));
            // 光环上移效果层（2026-09-23）：挂载即并集入卡面（多光环取并集）——重算式
            _card.AggregateEffectAuras(true);
            _card.ResetCache();
            RefreshAttached();
            Recalculate();
        }

        // 效果图 → 卡内嵌效果（2026-09-14 修复：编排字段全量拷贝——此前漏 SelectionMode/TargetCount/
        // Duration/DurationValue/TriggerLimitPerTurn/EngineKind 等上移字段；引擎通道 AtomicEffects
        // 原被 ProjectLinear(steps) 清空（自由分支 steps 为空）——现按形态分路）。
        private static CardEffectData SnapshotEffect(EffectGraphData graph)
        {
            var src = graph.header ?? new CardEffectData();
            var isEngine = src.EngineKind != (int)BranchEngineKind.None;
            return new CardEffectData
            {
                Id = src.Id,
                DisplayName = string.IsNullOrEmpty(src.DisplayName) ? graph.name : src.DisplayName,
                Description = src.Description,
                TriggerTiming = src.TriggerTiming,
                ActivationType = src.ActivationType,
                BaseSpeed = src.BaseSpeed,
                IsOptional = src.IsOptional,
                Duration = src.Duration,
                SummonDropZone = src.SummonDropZone,
                SelectionMode = src.SelectionMode,
                TargetCount = src.TargetCount,
                RandomTarget = src.RandomTarget,
                TriggerLimitPerTurn = src.TriggerLimitPerTurn,
                EngineKind = src.EngineKind,
                EngineParam = src.EngineParam,
                ArrowDirections = src.ArrowDirections, // 光环上移效果层（2026-09-23）——箭头随效果快照
                LinkAuras = src.LinkAuras != null && src.LinkAuras.Count > 0
                    ? new List<LinkAuraData>(src.LinkAuras) : null,
                ActivationConditions = src.ActivationConditions,
                TriggerConditions = src.TriggerConditions,
                Costs = src.Costs,
                Tags = src.Tags,
                Steps = isEngine ? null : (graph.steps != null ? new List<EffectStepData>(graph.steps) : null),
                // 引擎通道：奖励原子来自 header.AtomicEffects；其余形态线性投影（converter 双通道兼容）
                AtomicEffects = isEngine
                    ? (src.AtomicEffects != null ? new List<AtomicEffectEntry>(src.AtomicEffects) : null)
                    : ProjectLinear(graph.steps),
            };
        }

        private static List<AtomicEffectEntry> ProjectLinear(List<EffectStepData> steps)
        {
            var flat = new List<AtomicEffectEntry>();
            if (steps == null)
            {
                return flat;
            }
            foreach (var step in steps)
            {
                if (step == null)
                {
                    continue;
                }
                if (step.kind == 0 && step.atomic != null)
                {
                    flat.Add(step.atomic);
                }
                else if (step.kind == 1 && step.thenSteps != null)
                {
                    flat.AddRange(step.thenSteps);
                }
            }
            return flat;
        }

        private void RefreshAttached()
        {
            _attachedList.Clear();
            var effects = _card.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                var index = i;
                var label = string.IsNullOrEmpty(effects[i].DisplayName) ? $"效果 #{i + 1}" : effects[i].DisplayName;
                var row = MakeAttachedRow(label, effects[i],
                    () => RemoveAttachedAt(index),
                    () =>
                    {
                        // 跳转效果合成器编辑该效果（2026-09-14 合成器重做：静态会话传上下文）
                        ComposerSession.BeginCardEdit(_card, index);
                        Manager.Show<EffectComposerScreen>();
                    });
                _attachedList.Add(row);
            }
        }

        private void RemoveAttachedAt(int index)
        {
            if (index >= 0 && index < _card.Effects.Count)
            {
                _card.Effects.RemoveAt(index);
                // 光环随效果回收（2026-09-23）：移除效果后按剩余效果重算卡面箭头+光环
                _card.AggregateEffectAuras(true);
                _card.ResetCache();
                RefreshAttached();
                Recalculate();
            }
        }

        /// <summary>挂载效果行：名称 + 组合摘要（AtomText）+ 编辑/移除。</summary>
        private VisualElement MakeAttachedRow(string name, CardEffectData effect, Action onRemove, Action onEdit)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");
            row.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap;

            var label = new Label(name);
            label.AddToClassList("list-row__name");
            row.Add(label);

            var graph = new EffectGraphData(name) { header = effect, steps = effect?.Steps };
            var summary = new Label(AtomText.RenderEffectSummary(graph));
            summary.AddToClassList("list-row__meta");
            row.Add(summary);

            var edit = new Button(onEdit) { text = "编辑" };
            edit.AddToClassList("btn");
            edit.AddToClassList("btn--mini");
            row.Add(edit);

            var btn = new Button(onRemove) { text = "移除" };
            btn.AddToClassList("btn");
            btn.AddToClassList("btn--mini");
            btn.AddToClassList("btn--danger");
            row.Add(btn);
            return row;
        }

        // ======================================== 右栏：关键词（本体=直接引用原子） ========================================

        private void BuildKeywordZone()
        {
            _keywordZone.Clear();
            EnsureCardLists();

            // 现有关键词 chips（颜色沿用效果组合界面的 chip 体系）
            var chips = new VisualElement();
            chips.AddToClassList("toolbar");
            chips.style.flexWrap = Wrap.Wrap;
            if (_card.Keywords.Count == 0)
            {
                var none = new Label("（无本体关键词——直接引用原子效果即关键词）");
                none.AddToClassList("hint");
                chips.Add(none);
            }
            else
            {
                foreach (var kw in _card.Keywords.ToList())
                {
                    var captured = kw;
                    var chip = new VisualElement();
                    chip.style.flexDirection = FlexDirection.Row;
                    chip.style.alignItems = Align.Center;

                    var label = new Label(KeywordZh(captured));
                    label.AddToClassList("chip");
                    label.AddToClassList(ChipClass(ColorFilter.OfKeyword(captured)));
                    chip.Add(label);

                    var del = new Button(() =>
                    {
                        _card.Keywords.Remove(captured);
                        _card.ResetCache();
                        BuildKeywordZone();
                        Recalculate();
                    }) { text = "✕" };
                    del.AddToClassList("btn");
                    del.AddToClassList("btn--mini");
                    chip.Add(del);
                    chips.Add(chip);
                }
            }
            _keywordZone.Add(chips);

            // 添加下拉（目录=Grant 族原子，中文名；赋予他人形态走效果合成器）
            var catalog = KeywordCatalog.LoadAll();
            var choices = new List<string> { "（添加本体关键词）" };
            choices.AddRange(catalog.Select(k => k.DisplayName));
            var dd = new DropdownField("添加") { choices = choices };
            dd.AddToClassList("text-input");
            dd.index = 0;
            dd.RegisterValueChangedCallback(_ =>
            {
                int i = dd.index - 1;
                if (i < 0 || i >= catalog.Count) return;
                var kw = catalog[i];
                if (_card.Keywords.Contains(kw.Id))
                {
                    ShowToast($"已有关键词：{kw.DisplayName}");
                }
                else
                {
                    _card.Keywords.Add(kw.Id); // 保存时经 TryGetAtomRefId 反查 refId 写入 effectIds
                    _card.ResetCache();
                    ShowToast($"已添加：{kw.DisplayName}（保存=effectIds 直接引用原子 refId）");
                }
                BuildKeywordZone();
                Recalculate();
            });
            _keywordZone.Add(dd);

            var hint = new Label("关键词=卡直接引用原子（本体）；「赋予」类关键词经效果合成器组合后按效果挂载");
            hint.AddToClassList("hint");
            _keywordZone.Add(hint);
        }

        private static string ChipClass(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return "chip--red";
                case UIColor.Blue: return "chip--blue";
                case UIColor.Green: return "chip--green";
                case UIColor.Black: return "chip--black";
                case UIColor.White: return "chip--white";
                default: return "chip--gray";
            }
        }

        // ======================================== 自动算费 ========================================

        private void Recalculate()
        {
            var result = CardCostCalculator.Calculate(_card);
            _lastSuggestedCost = result.CostDict;

            _breakdownList.Clear();
            foreach (var line in result.Breakdown)
            {
                var row = new VisualElement();
                row.AddToClassList("list-row");

                var label = new Label(line.Label);
                label.AddToClassList("list-row__name");
                row.Add(label);

                var value = new Label(line.Value.ToString("0.0"));
                value.AddToClassList("list-row__meta");
                row.Add(value);
                _breakdownList.Add(row);
            }

            // 超模校验已移除（2026-09-24 定案）：卡层只算减费与建议档位——展示 D 与黑白获得，不做合规判断
            var grantStr = result.Grants.Count > 0
                ? "｜获得 " + string.Join(" ", result.Grants.Select(kv => $"{(ManaType)kv.Key} {(int)kv.Value}"))
                : "";
            _suggested.text = $"建议档位 {result.ManaCost}（D={result.Total:0}）{grantStr}";
            RefreshCostLabel();
        }

        // 采纳：整字典写入建议费用分布（多色）。
        private void OnAdoptCost()
        {
            if (_lastSuggestedCost == null || _lastSuggestedCost.Count == 0)
            {
                ShowToast("无建议费用可采纳（D=0 保持空，打出按默认灰 1 计）");
                return;
            }
            _card.Cost = new Dictionary<int, float>(_lastSuggestedCost);
            _card.ResetCache();
            RefreshCostLabel();
            ShowToast("已采纳建议分布：" + string.Join(" ", _lastSuggestedCost.Select(kv => $"{(ManaType)kv.Key}:{(int)kv.Value}")));
        }

        private void RefreshCostLabel()
        {
            if (_card.Cost == null || _card.Cost.Count == 0)
            {
                _costLabel.text = "当前费用 (无)";
                return;
            }
            var parts = _card.Cost.Select(kv => $"{(ManaType)kv.Key}:{(int)kv.Value}");
            _costLabel.text = "当前费用 " + string.Join(" ", parts);
        }

        // ======================================== 保存（覆盖替换） ========================================

        private void OnSave()
        {
            ApplyTextLists();
            EnsureCardLists();

            var newId = "C_" + ContentHasher.HashCard(_card);
            bool replacing = !string.IsNullOrEmpty(_editingOriginalId) && _editingOriginalId != newId;

            // 覆盖替换前置检查（2026-09-24 定案）：旧卡被卡组引用则拦截（删除会使卡组引用悬空）
            if (replacing)
            {
                var refs = DeckSerializer.LoadAll()
                    .Where(d => d.cardIds != null && d.cardIds.Contains(_editingOriginalId))
                    .Select(d => d.name)
                    .ToList();
                if (refs.Count > 0)
                {
                    ShowToast($"旧卡仍被卡组引用（{string.Join("、", refs)}）——先到卡组构建中替换该卡再保存");
                    return;
                }
            }

            var path = CardConfigSerializer.Save(_card, null, replacing ? _editingOriginalId : null);
            if (path == null)
            {
                ShowToast("保存失败");
                return;
            }
            CardCatalog.Invalidate();

            ShowToast(replacing
                ? $"已覆盖替换：{_card.CardName}（{_editingOriginalId} → {newId}）"
                : $"已保存到卡表：{System.IO.Path.GetFileName(path)}");

            OnNewCard();
        }

        private void ApplyTextLists()
        {
            _card.CardName = string.IsNullOrWhiteSpace(_nameField.value) ? "新卡牌" : _nameField.value.Trim();
            _card.Tags = SplitCsv(_tagsField.value);
        }

        private static List<string> SplitCsv(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }
            return raw.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private void ShowToast(string message)
        {
            _toast.text = message;
        }
    }
}
