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
    /// <summary>
    /// 卡牌合成界面（2026-10-01 预制体化：静态层级来自 Assets/Art/UI/CardUI.prefab，
    /// Build 深度按名绑定+闭包接线；滚动列表行/动态表单/代价栏运行时重建）——三栏：
    /// 左=卡池预览（生物/法术分组，颜色过滤+搜索+全局排序），中=卡牌表单（类型/攻血/耐久/代价栏/算费），
    /// 右=挂载效果+关键词。
    ///
    /// 数据模型（2026-09-24 关键词引用化定案）：卡只做卡层增量（名称/类型/身材/耐久/声明费/代价栏），
    /// 效果与关键词全部走 effectIds 引用——**直接引用原子 refId=本体关键词**（本界面关键词区），
    /// 引用 Effects.json 组合效果=效果挂载（其中的赋予族=赋予关键词，赋予亦可作用于自己）。
    /// 编辑旧卡保存=覆盖替换（删旧存新；旧卡被卡组引用则拦截——内容哈希 ID 机制下无法原地覆盖）。
    /// 超模校验已按 2026-09-24 定案移除——卡层只算减费与建议档位，不做 D≤C 合规判断。
    /// </summary>
    public sealed class CardComposerScreen : UIScreen
    {
        protected override string PrefabName => "CardUI";
        protected override string RootName => "card-composer";
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

        private UiKit.Scroll _poolList;
        private UiKit.Scroll _attachedList;
        private UiKit.Scroll _breakdownList;
        private RectTransform _dynamicForm;
        private RectTransform _payloadZone;
        private RectTransform _keywordZone;
        private RectTransform _effectLibraryZone;
        private RectTransform _poolFilter;
        private TMP_Text _toast;
        private TMP_Text _suggested;
        private TMP_Text _costLabel;
        private UInputField _nameField;
        private UInputField _tagsField;
        private UInputField _searchField;
        private UiKit.Dropdown _cardtypeDropdown;
        private RectTransform _overlay;

        // 卡池颜色过滤 chips（预制体烘焙，Build 一次绑定；激活态=Outline 开关）。
        private readonly Dictionary<UIColor, UnityEngine.UI.Button> _filterChips = new Dictionary<UIColor, UnityEngine.UI.Button>();

        private ElementCost _lastSuggestedCost;

        protected override void Build()
        {
            _overlay = Find("overlay"); // 2026-10-07 兜底退役：缺节点 LogError——下拉弹层挂载点不可用

            // ---- 工具栏 ----
            BindButton("btn-back", () => Manager.Back());
            _nameField = FindInput("field-card-name");
            _tagsField = FindInput("field-tags");
            BindButton("btn-new", OnNewCard);
            BindButton("btn-save", OnSave);
            _toast = FindText("lbl-toast");

            // ---- 三栏容器/滚动区 ----
            _poolFilter = Find("pool-filter");
            _searchField = FindInput("field-search");
            if (_searchField != null)
                _searchField.onValueChanged.AddListener(_ => RefreshPool());
            _poolList = FindScroll("list-pool");

            // 类型下拉（绑定烘焙头部按钮）
            var typeHead = Find("dropdown-cardtype");
            var typeHeadBtn = typeHead != null ? typeHead.GetComponentInChildren<UnityEngine.UI.Button>(true) : null;
            if (typeHeadBtn != null)
            {
                _cardtypeDropdown = new UiKit.Dropdown(typeHeadBtn, _overlay,
                    KindNames.Select(k => k.name).ToList(), 0,
                    onChanged: (idx, _) =>
                    {
                        if (idx >= 0 && idx < KindNames.Length)
                        {
                            _kind = KindNames[idx].kind;
                            ApplyKindToCard();
                            BuildDynamicForm();
                            Recalculate();
                        }
                    });
            }

            _dynamicForm = Find("dynamic-form");
            _payloadZone = Find("payload-zone");
            _suggested = FindText("lbl-suggested");
            _costLabel = FindText("lbl-cost");
            BindButton("btn-adopt", OnAdoptCost);
            _breakdownList = FindScroll("list-breakdown");
            BindButton("btn-new-effect", () =>
            {
                ComposerSession.BeginCardEdit(_card, -1); // -1 = 新建（保存时追加）
                Manager.Show<EffectComposerScreen>();
            });

            _effectLibraryZone = Find("effect-library-zone");
            _attachedList = FindScroll("list-attached");
            _keywordZone = Find("keyword-zone");

            BindPoolFilter();
        }

        public override void OnEnter()
        {
            EnsureCardLists();
            _kind = MapKind(_card.Supertype);
            _cardtypeDropdown?.SetIndex(KindIndex(_kind));
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

        /// <summary>过滤 chips 绑定（预制体烘焙 chip-{Color}，一次接线；颜色/文案由预制体烘焙）。</summary>
        private void BindPoolFilter()
        {
            if (_poolFilter == null) return;
            foreach (UIColor color in Enum.GetValues(typeof(UIColor)))
            {
                var captured = color;
                var chipRt = UiKit.FindDeep(_poolFilter, $"chip-{captured}");
                var chip = chipRt != null ? chipRt.GetComponentInChildren<UnityEngine.UI.Button>(true) : null;
                if (chip == null) continue; // 缺 chip 容错跳过
                chip.onClick.AddListener(() =>
                {
                    _colorFilter = captured;
                    UpdatePoolFilter();
                    RefreshPool();
                });
                _filterChips[captured] = chip;
            }
            UpdatePoolFilter();
        }

        /// <summary>激活态切换：当前过滤色的 chip 加描边（预制体化后不重建行，只开关 Outline）。</summary>
        private void UpdatePoolFilter()
        {
            foreach (var kv in _filterChips)
            {
                bool active = kv.Key == _colorFilter;
                var ol = kv.Value.GetComponent<Outline>();
                if (active && ol == null)
                {
                    ol = kv.Value.gameObject.AddComponent<Outline>();
                    ol.effectColor = UiStyle.TextPrimary;
                    ol.effectDistance = new Vector2(1.5f, -1.5f);
                }
                if (ol != null) ol.enabled = active;
            }
        }

        private static (Color bg, Color fg) ChipColors(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return (Rgb(150, 56, 56), Rgb(255, 235, 235));
                case UIColor.Blue: return (Rgb(52, 84, 150), Rgb(232, 240, 255));
                case UIColor.Green: return (Rgb(52, 120, 72), Rgb(232, 255, 240));
                case UIColor.Gray: return (Rgb(80, 86, 98), Rgb(232, 235, 240));
                default: return (UiStyle.BtnBg, UiStyle.TextBody); // All
            }
        }

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        private void RefreshPool()
        {
            ClearContent(_poolList.Content);
            IEnumerable<CardData> cards = CardCatalog.LoadPlayPool(); // 教学专用卡不进编辑器卡池预览（2026-10-06 隔离）
            if (_colorFilter != UIColor.All)
                cards = cards.Where(c => CardSorter.HasCostColor(c, _colorFilter));
            string query = _searchField != null ? _searchField.text : null;
            if (!string.IsNullOrWhiteSpace(query))
                cards = cards.Where(c => (c.CardName ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);

            var sorted = CardSorter.Sort(cards).ToList();
            if (sorted.Count == 0)
            {
                var empty = UiKit.Label("empty", _poolList.Content, "（无匹配卡牌）",
                    UiStyle.SmallSize, UiStyle.TextHint);
                UiKit.Size(empty, fw: 1f);
                return;
            }

            // 分组（2026-09-24 定案）：生物 / 法术（瞬间+结界）
            AddPoolGroup("生物", sorted.Where(c => c.Supertype == Cardtype.Creature).ToList());
            AddPoolGroup("法术（瞬间/结界）", sorted.Where(c => c.Supertype != Cardtype.Creature).ToList());
        }

        private void AddPoolGroup(string title, List<CardData> cards)
        {
            if (cards.Count == 0) return;
            var header = UiKit.Label("group", _poolList.Content, $"{title}（{cards.Count}）",
                UiStyle.HeaderSize, UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(header, fw: 1f);
            foreach (var card in cards)
                MakePoolRow(card);
        }

        /// <summary>卡池预览行：费用徽标+名称+类型/身材，次行=关键词+效果摘要（AtomText 单一来源）。</summary>
        private void MakePoolRow(CardData card)
        {
            var row = UiKit.Column("row", _poolList.Content, spacing: 2f, pad: 6f);
            var bg = UiKit.BgRow(row);
            bool selected = !string.IsNullOrEmpty(_editingOriginalId) && card.ID == _editingOriginalId;
            if (selected) bg.color = UiStyle.SelectedRowBg;
            bg.raycastTarget = true;
            UiKit.Size(row, fw: 1f);

            var top = UiKit.Row("top", row, spacing: 8f);
            CostBadge("cost", top, ((int)card.TotalCost).ToString());
            var name = UiKit.Label("name", top,
                string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName,
                UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            UiKit.Label("meta", top, TypeStatLine(card), UiStyle.SmallSize, UiStyle.TextFaint);

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
                var detail = UiKit.Label("detail", row, string.Join("｜", detailParts),
                    UiStyle.SmallSize, UiStyle.TextFaint, wrap: true);
                UiKit.Size(detail, fw: 1f);
            }

            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => LoadCardForEdit(card));
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
        /// 深拷贝容器、共享效果实例——效果合成器写回=整实例替换（EffectComposerScreen）、
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
            _card.Cost = src.Cost != null ? src.Cost.Clone() : null;
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

            _nameField.SetTextWithoutNotify(_card.CardName ?? "");
            _tagsField.SetTextWithoutNotify(string.Join(",", _card.Tags ?? new List<string>()));
            _cardtypeDropdown?.SetIndex(KindIndex(_kind));
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

            _nameField.SetTextWithoutNotify("新卡牌");
            _tagsField.SetTextWithoutNotify("");
            _cardtypeDropdown?.SetIndex(KindIndex(_kind));
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

        private static int KindIndex(CardKind kind)
        {
            for (int i = 0; i < KindNames.Length; i++)
                if (KindNames[i].kind == kind) return i;
            return 0;
        }

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
            ClearChildren(_dynamicForm);

            // 攻 / 血
            if (HasStats)
            {
                var statRow = UiKit.Row("stats", _dynamicForm, spacing: 8f);
                MakeLabeledInt(statRow, "攻击", _card.Power ?? 0, v => { _card.Power = v; Recalculate(); });
                MakeLabeledInt(statRow, "生命", _card.Life ?? 0, v => { _card.Life = v; Recalculate(); });
            }

            // 等级
            if (HasLevel)
                MakeLabeledInt(_dynamicForm, "等级", _card.Level ?? 1, v => _card.Level = v);

            // 耐久（结界专属，2026-09-24 定案：类似生物生命、被攻击每次仅损失 1 点）
            if (HasDurability)
                MakeLabeledInt(_dynamicForm, "耐久", _card.Durability, v => { _card.Durability = v; Recalculate(); });
        }

        // 带标签的整数输入（横排）。
        private static void MakeLabeledInt(RectTransform parent, string label, int value, Action<int> onChanged)
        {
            var row = UiKit.Row("field", parent, spacing: 6f);
            UiKit.Label("label", row, label, UiStyle.SmallSize, UiStyle.TextDim);
            UiKit.IntField("field-" + label, row, label, value, onChanged, width: 70f);
        }

        // ---------- 代价栏（2026-09-23 定案：上移卡组合层；2026-10-04 规则改造 + 本轮全套口径）----------
        // 单卡单条 Payload：任意单向效果不限价——填装时逆转选择范围到错误一侧
        //（表域双侧收窄 / 正确侧单向镜像 / 恰为错误侧免写，统一口 CostDerivationService.PayloadCostDomain）；
        // 代价也是效果栏：占 1 效果槽（底盘 3 灰同口径）；全价不并入卡费——使用时单独过地牌门槛
        //（全价>地牌槽上限 → 整卡不可用）；打出时先扣卡费再强制执行，生效后按执行前快照全价补偿黑/白。

        private void BuildPayloadZone()
        {
            ClearChildren(_payloadZone);
            var header = UiKit.Label("header", _payloadZone, "代价（逆转错侧·单卡单条·占1效果槽）",
                UiStyle.HeaderSize, UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(header, fw: 1f);

            var pe = _card.PayloadCost?.payload;
            if (pe != null && !string.IsNullOrEmpty(pe.refId))
            {
                var row = UiKit.Row("payload-row", _payloadZone, spacing: 8f);
                var name = UiKit.Label("name", row, AtomText.RenderAtomEntry(pe),
                    UiStyle.BodySize, UiStyle.TextBody, wrap: true);
                UiKit.Size(name, fw: 1f);
                var price = UiKit.Label("price", row, PayloadPriceText(pe),
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.MiniButton("del", row, "移除", () =>
                {
                    _card.PayloadCost = null;
                    _card.ResetCache();
                    BuildPayloadZone();
                    Recalculate();
                }, UiStyle.BtnDanger);
                return;
            }

            // 未填装：候选下拉（与 FillPayloadCost 同口径的表级过滤——PayloadCostDomain 可入，不限价）
            var rows = PayloadCandidates().ToList();
            var choices = new List<string> { "（选择代价原子）" };
            choices.AddRange(rows.Select(r => r.DisplayName));
            new UiKit.Dropdown("dd-payload", _payloadZone, _overlay, choices, 0, (idx, _) =>
            {
                int i = idx - 1;
                if (i >= 0 && i < rows.Count)
                    FillPayloadCost(new AtomicEffectEntry { refId = rows[i].HashId, value = 1 });
            }, width: 220f);
        }

        /// <summary>填装代价栏（2026-10-04 定案：任意单向效果·逆转选择范围·不限价）。
        /// 有效域统一走 CostDerivationService.PayloadCostDomain：p≠0 强制错误侧——表域双侧收窄、
        /// 正确侧单向**镜像逆转**（MirrorDomain）、恰为错误侧免写；p=0 须表域单侧锁定。
        /// 不再限价（09-21「等价1」退役）——全价不并入卡费：占 1 效果槽（底盘同口径），
        /// 使用时单独过地牌门槛（全价>上限整卡不可用），生效后按快照全价补偿黑/白（产出不封）。</summary>
        private void FillPayloadCost(AtomicEffectEntry entry)
        {
            var cfg = AtomicEffectTable.GetByHashId(entry?.refId);
            if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type))
            {
                ShowToast("原子表引用缺失——无法作代价");
                return;
            }
            if (ComposerCatalog.HasMountBit(cfg, MountKind.SystemInternal))
            { ShowToast("系统内部原子（数值修改原语）不可作代价"); return; }
            if (ComposerCatalog.HasMountBit(cfg, MountKind.Keyword))
            { ShowToast("关键词类原子不可作代价（代价位不出现关键词）"); return; }

            var (kinds, eligible) = CostDerivationService.PayloadCostDomain(cfg);
            if (!eligible)
            {
                ShowToast("该原子无法逆转到错误一侧（纯自身域有益 / 中性双侧域）——不可作代价");
                return;
            }
            entry.kinds = kinds; // null=免写（表默认同效）；镜像域可超出表行域——逆转语义

            _card.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = entry };
            _card.ResetCache();
            BuildPayloadZone();
            Recalculate();
            var inst = CardEffectConverter.ConvertPayloadForDisplay(entry);
            int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            ShowToast($"已填装代价（全价 {price}·逆转错侧·占1效果槽）——打出时先扣卡费再强制执行，生效后按全价得黑/白；全价超过地牌槽上限时整卡不可用");
        }

        /// <summary>代价候选行（表级，2026-10-04 新准入）：非关键词/非系统内部 + PayloadCostDomain 可入
        ///（p≠0 双侧收窄或正确侧镜像逆转 / p=0 单侧锁）——不限价，下拉只列真正可装的原子。</summary>
        private static IEnumerable<AtomicEffectConfig> PayloadCandidates()
        {
            foreach (var row in AtomicEffectTable.GetAll())
            {
                if (row == null || string.IsNullOrEmpty(row.EnumName)) continue;
                if (!Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) continue;
                if (ComposerCatalog.HasMountBit(row, MountKind.Keyword)) continue;
                if (ComposerCatalog.HasMountBit(row, MountKind.SystemInternal)) continue;

                if (!CostDerivationService.PayloadCostDomain(row).eligible) continue;
                yield return row;
            }
        }

        /// <summary>代价全价展示行（口径同装载期 PayloadUnitGrant；全价作地牌门槛与补偿基准，不并入卡费）。</summary>
        private static string PayloadPriceText(AtomicEffectEntry entry)
        {
            var inst = CardEffectConverter.ConvertPayloadForDisplay(entry);
            int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            return $"全价：{price}（过地牌门槛·生效后补偿黑/白·占1效果槽）";
        }

        // ======================================== 右栏：效果挂载 ========================================

        /// <summary>效果库挂载下拉（2026-09-24 布局重做：大列表收成下拉，能力不减）。</summary>
        private void BuildEffectLibraryZone()
        {
            ClearChildren(_effectLibraryZone);
            var graphs = EffectLibrarySerializer.LoadAll();
            var choices = new List<string> { "（从效果库挂载）" };
            choices.AddRange(graphs.Select(g => string.IsNullOrEmpty(g.name) ? "(未命名)" : g.name));
            new UiKit.Dropdown("dd-library", _effectLibraryZone, _overlay, choices, 0, (idx, _) =>
            {
                int i = idx - 1;
                if (i >= 0 && i < graphs.Count)
                    AttachEffect(graphs[i]);
            }, width: 240f);
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

        // 效果图 → 卡内嵌效果（2026-09-14 修复：编排字段全量拷贝；2026-10-05 两槽定案：引擎通道载荷化——
        // branch 载荷随步骤原子走，快照不再分路）。
        private static CardEffectData SnapshotEffect(EffectGraphData graph)
        {
            var src = graph.header ?? new CardEffectData();
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
                ArrowDirections = src.ArrowDirections, // 光环上移效果层（2026-09-23）——箭头随效果快照
                LinkAuras = src.LinkAuras != null && src.LinkAuras.Count > 0
                    ? new List<LinkAuraData>(src.LinkAuras) : null,
                ActivationConditions = src.ActivationConditions,
                TriggerConditions = src.TriggerConditions,
                Costs = src.Costs,
                Tags = src.Tags,
                Steps = graph.steps != null ? new List<EffectStepData>(graph.steps) : null,
                AtomicEffects = ProjectLinear(graph.steps),
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
            ClearContent(_attachedList.Content);
            var effects = _card.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                var index = i;
                var label = string.IsNullOrEmpty(effects[i].DisplayName) ? $"效果 #{i + 1}" : effects[i].DisplayName;
                MakeAttachedRow(label, effects[i],
                    () => RemoveAttachedAt(index),
                    () =>
                    {
                        // 跳转效果合成器编辑该效果（2026-09-14 合成器重做：静态会话传上下文）
                        ComposerSession.BeginCardEdit(_card, index);
                        Manager.Show<EffectComposerScreen>();
                    });
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
        private void MakeAttachedRow(string name, CardEffectData effect, Action onRemove, Action onEdit)
        {
            var row = UiKit.Column("row", _attachedList.Content, spacing: 2f, pad: 6f);
            UiKit.BgRow(row);
            UiKit.Size(row, fw: 1f);

            var top = UiKit.Row("top", row, spacing: 6f);
            var label = UiKit.Label("name", top, name, UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(label, fw: 1f);
            UiKit.MiniButton("edit", top, "编辑", onEdit);
            UiKit.MiniButton("remove", top, "移除", onRemove, UiStyle.BtnDanger);

            var graph = new EffectGraphData(name) { header = effect, steps = effect?.Steps };
            var summary = UiKit.Label("summary", row, AtomText.RenderEffectSummary(graph),
                UiStyle.SmallSize, UiStyle.TextFaint, wrap: true);
            UiKit.Size(summary, fw: 1f);
        }

        // ======================================== 右栏：关键词（本体=直接引用原子） ========================================

        private void BuildKeywordZone()
        {
            ClearChildren(_keywordZone);
            EnsureCardLists();

            var header = UiKit.Label("header", _keywordZone, "本体关键词", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(header, fw: 1f);

            // 现有关键词 chips（颜色沿用效果组合界面的 chip 体系）
            var chips = UiKit.Row("chips", _keywordZone, spacing: 4f);
            if (_card.Keywords.Count == 0)
            {
                var none = UiKit.Label("none", chips, "（无本体关键词——直接引用原子效果即关键词）",
                    UiStyle.SmallSize, UiStyle.TextHint);
                UiKit.Size(none, fw: 1f);
            }
            else
            {
                foreach (var kw in _card.Keywords.ToList())
                {
                    var captured = kw;
                    var (bg, fg) = ChipColors(ColorFilter.OfKeyword(captured));
                    var chip = UiKit.Row("chip", chips, spacing: 2f, pad: 4f);
                    var chipBg = chip.gameObject.AddComponent<Image>();
                    chipBg.sprite = UiKit.RoundedSprite;
                    chipBg.type = Image.Type.Sliced;
                    chipBg.color = bg;
                    var lbl = UiKit.Label("label", chip, KeywordZh(captured),
                        UiStyle.SmallSize, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
                    UiKit.Size(chip, h: 26f);
                    UiKit.MiniButton("del", chip, "✕", () =>
                    {
                        _card.Keywords.Remove(captured);
                        _card.ResetCache();
                        BuildKeywordZone();
                        Recalculate();
                    }, fg: fg);
                }
            }

            // 添加下拉（目录=Grant 族原子，中文名；赋予他人形态走效果合成器）
            var catalog = KeywordCatalog.LoadAll();
            var choices = new List<string> { "（添加本体关键词）" };
            choices.AddRange(catalog.Select(k => k.DisplayName));
            new UiKit.Dropdown("dd-keyword", _keywordZone, _overlay, choices, 0, (idx, _) =>
            {
                int i = idx - 1;
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
            }, width: 240f);

            var hint = UiKit.Label("hint", _keywordZone,
                "关键词=卡直接引用原子（本体）；「赋予」类关键词经效果合成器组合后按效果挂载",
                UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(hint, fw: 1f);
        }

        // ======================================== 自动算费 ========================================

        private void Recalculate()
        {
            var result = CardCostCalculator.Calculate(_card);
            _lastSuggestedCost = result.CostDict;

            ClearContent(_breakdownList.Content);
            foreach (var line in result.Breakdown)
            {
                var row = UiKit.Row("row", _breakdownList.Content, spacing: 8f, pad: 4f);
                UiKit.BgRow(row);
                var label = UiKit.Label("label", row, line.Label, UiStyle.SmallSize, UiStyle.TextBody);
                UiKit.Size(label, fw: 1f);
                UiKit.Label("value", row, line.Value.ToString("0.0"), UiStyle.SmallSize, UiStyle.TextFaint);
            }

            // 超模校验已移除（2026-09-24 定案）：卡层只算减费与建议档位——展示 D 与黑白获得，不做合规判断
            var grantStr = result.Grants.Count > 0
                ? "｜获得 " + string.Join(" ", result.Grants.Select(kv => $"{(ManaType)kv.Key} {(int)kv.Value}"))
                : "";
            _suggested.text = $"建议档位 {result.ManaCost}（D={result.Total:0}）{grantStr}";
            RefreshCostLabel();
        }

        // 采纳：建议费用分布整组写入（位置数组）。
        private void OnAdoptCost()
        {
            if (_lastSuggestedCost == null || _lastSuggestedCost.IsZero)
            {
                ShowToast("无建议费用可采纳（D=0 保持空，打出按默认灰 1 计）");
                return;
            }
            _card.Cost = _lastSuggestedCost.Clone();
            _card.ResetCache();
            RefreshCostLabel();
            ShowToast("已采纳建议分布：" + _lastSuggestedCost.ToString());
        }

        private void RefreshCostLabel()
        {
            if (_card.Cost == null || _card.Cost.IsZero)
            {
                _costLabel.text = "当前费用 (无)";
                return;
            }
            _costLabel.text = "当前费用 " + _card.Cost.ToString();
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
            TutorialCreationFlow.NotifyCardsChanged(); // 第三课走查步检测（未开课零行为）

            ShowToast(replacing
                ? $"已覆盖替换：{_card.CardName}（{_editingOriginalId} → {newId}）"
                : $"已保存到卡表：{System.IO.Path.GetFileName(path)}");

            OnNewCard();
        }

        private void ApplyTextLists()
        {
            _card.CardName = string.IsNullOrWhiteSpace(_nameField.text) ? "新卡牌" : _nameField.text.Trim();
            _card.Tags = SplitCsv(_tagsField.text);
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

        // ======================================== 通用 ========================================

        /// <summary>费用徽标（蓝色小圆片+数字）。</summary>
        private static void CostBadge(string name, RectTransform parent, string value)
        {
            var rt = UiKit.Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiKit.CircleSprite;
            img.color = UiStyle.BtnPrimary;
            var lbl = UiKit.Label("label", rt, value, UiStyle.SmallSize, UiStyle.White,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.StretchInset(lbl.rectTransform, 2f, 0f);
            UiKit.Size(rt, w: 26f, h: 24f);
        }

        /// <summary>清空容器（先摘父再 Destroy，防 Destroy 延迟导致的同帧占位）。</summary>
        private static void ClearChildren(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.SetParent(null);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static void ClearContent(RectTransform content) => ClearChildren(content);
    }
}
