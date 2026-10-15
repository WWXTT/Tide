using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UInputField = TMPro.TMP_InputField;
using UButton = UnityEngine.UI.Button;

namespace SynergyUI
{
    /// <summary>
    /// 卡组构建界面（2026-10-01 预制体化：静态层级来自 Assets/Art/Prefab/DeckUI.prefab，
    /// Build 深度按名绑定+闭包接线；滚动列表行/统计区/预览内容运行时重建）——三栏：
    /// 左=效果预览（点击行显示），中=当前卡组+统计（费用曲线/颜色分布/类型比例/英雄技能预览），
    /// 右=卡牌列表（颜色过滤+搜索+全局排序，可重复添加）。
    ///
    /// 构筑规则（2026-10-11 定案，取代 2026-09-24「每卡 1 张」限制）：
    /// ① 必须标记英雄技能（资格=恰好一个主动效果的结界）——未标记不可保存；
    /// ② 唯一效果（唯一门命运一抽【唯一】/同名仪典规则光环）每卡组限 1 张——添加口+保存口
    /// 双拦截，同卡重复与异卡同名都算冲突（不同名仪典可共存，镜像运行时异名共存）；
    /// ③ 卡组 30-60 张、卡可重复任意张（唯一门槛落地后放开——硬凑 30 张不同卡是玩家负担）。
    /// 超模检查仍移除（2026-09-24 定案）。卡组区=UGUI 卡网格（ScrollGrid+卡实例挂槽位）。
    /// </summary>
    public sealed class DeckBuilderScreen : UIScreen
    {
        protected override string PrefabName => "DeckUI";
        protected override string RootName => "deck-builder";

        // 构筑张数上下限（2026-10-11 定案规则③）：30-60 张、卡可重复任意张——
        // 旧「每卡 1 张」限制随唯一效果门槛落地而废止。
        private const int MinDeckCards = 30;
        private const int MaxDeckCards = 60;

        // 当前正在构筑的卡组（卡牌 ID 列表）。
        private readonly List<string> _deckCardIds = new List<string>();

        // 英雄技能标记卡 ID（2026-10-07 卡牌化：标记一张「恰好一个主动效果的结界」为技能，
        // 开局从牌库抽出落技能栏；null = 未标记——2026-10-11 构筑规则①：未标记不可保存）。
        // 资格校验=HeroSkillSystem.CanBeSkillCard。
        private string _skillCardId;

        // UGUI 卡牌层绑定（卡组区=卡槽网格 + UGUI 卡面；RefreshAll 全量重建）。
        private readonly List<CardOverlayBinding> _deckBindings = new List<CardOverlayBinding>();

        // 颜色过滤 chips（预制体烘焙，Build 一次绑定；激活态=Outline 开关）。
        private readonly Dictionary<UIColor, UButton> _filterChips = new Dictionary<UIColor, UButton>();

        private UIColor _colorFilter = UIColor.All;

        // 2026-10-13 搜索改版分面：卡型/费用档/关键词（与色 chips 同为谓词交集）。
        // chips+下拉=列表内容首两行，随 RefreshCatalog 重建（列表本就全量重建，不另设容器/不烘焙 prefab）。
        private string _kindFilterZh;   // null = 全部（技能=可标记英雄技能的结界，独立于结界档）
        private int _costBand = -1;     // -1 = 全部；档界=CostBandLo/Hi（左闭右开）
        private string _keywordFilter;  // null/空 = 全部（关键词中文名）
        private UiKit.Dropdown _keywordDropdown;
        private static readonly string[] KindFilterChoices = { "生物", "法术", "结界", "技能" };
        private static readonly string[] CostBandLabels = { "0-1费", "2-3费", "4-5费", "6+费" };
        private static readonly float[] CostBandLo = { 0f, 2f, 4f, 6f };
        private static readonly float[] CostBandHi = { 2f, 4f, 6f, float.MaxValue };

        private CardData _previewCard;

        private UiKit.Scroll _catalogList;
        private UiKit.Scroll _deckList;
        private UiKit.Scroll _previewZone;
        private RectTransform _statsZone;
        private RectTransform _catalogFilter;
        private TMP_Text _summary;
        private TMP_Text _toast;
        private UInputField _nameField;
        private UInputField _searchField;
        private UiKit.Dropdown _decksDropdown;
        private RectTransform _overlay;

        protected override void Build()
        {
            _overlay = Find("overlay"); // 2026-10-07 兜底退役：缺节点 LogError——下拉弹层挂载点不可用

            // ---- 工具栏 ----
            BindButton("btn-back", () => Manager.Back());
            _nameField = FindInput("field-deck-name");
            if (_nameField != null)
                // 主题约束随卡组名实时换算（2026-10-15 构成检测：红/绿/蓝首字触发双色检测块）
                _nameField.onValueChanged.AddListener(_ => RefreshStats());
            BindButton("btn-save", OnSave);
            BindButton("btn-load", OnLoad);
            BindButton("btn-delete", OnDelete);
            var decksHeadRt = Find("dropdown-decks");
            if (decksHeadRt != null)
            {
                var head = decksHeadRt.GetComponentInChildren<UnityEngine.UI.Button>(true);
                if (head != null)
                    _decksDropdown = new UiKit.Dropdown(head, _overlay, new List<string>(), 0, width: 160f);
            }
            _searchField = FindInput("field-search");
            if (_searchField != null)
                _searchField.onValueChanged.AddListener(_ => RefreshCatalog());
            _toast = FindText("lbl-toast");

            // ---- 三栏容器/滚动区 ----
            _catalogFilter = Find("catalog-filter");
            _catalogList = FindScroll("list-catalog");
            _deckList = FindScroll("list-deck");
            _previewZone = FindScroll("preview-zone");
            _statsZone = Find("stats-zone");
            _summary = FindText("lbl-summary");

            BindFilterRow();
        }

        public override void OnEnter()
        {
            RefreshDecksDropdown();
            RefreshAll();
        }

        public override void OnExit()
        {
            // UGUI 卡牌层回收（卡面独立池化，切屏必须显式清）
            CardOverlayController.ClearActive();
        }

        // ======================================== 右栏：卡牌列表 ========================================

        /// <summary>过滤 chips 绑定（预制体烘焙 chip-{Color}，一次接线；颜色/文案由预制体烘焙）。</summary>
        private void BindFilterRow()
        {
            if (_catalogFilter == null) return;
            foreach (UIColor color in Enum.GetValues(typeof(UIColor)))
            {
                var captured = color;
                var chipRt = UiKit.FindDeep(_catalogFilter, $"chip-{captured}");
                var chip = chipRt != null ? chipRt.GetComponentInChildren<UButton>(true) : null;
                if (chip == null) continue; // 缺 chip 由 FindDeep 静默——此处仅容错跳过
                chip.onClick.AddListener(() =>
                {
                    _colorFilter = captured;
                    UpdateFilterChips();
                    RefreshCatalog();
                });
                _filterChips[captured] = chip;
            }
            UpdateFilterChips();
        }

        /// <summary>激活态切换：当前过滤色的 chip 加描边（预制体化后不重建行，只开关 Outline）。</summary>
        private void UpdateFilterChips()
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

        private void RefreshCatalog()
        {
            _keywordDropdown?.Close(); // 弹层挂 overlay 不随头按钮销毁——重刷前先收，防孤儿弹层
            ClearContent(_catalogList.Content);
            BuildFacetRows(); // 分面行=内容首两行（卡型/费用档 chips + 关键词下拉），随刷新重建

            IEnumerable<CardData> cards = CardCatalog.LoadPlayPool(); // 教学专用卡不进构筑卡池（2026-10-06 隔离）
            if (_colorFilter != UIColor.All)
                cards = cards.Where(c => CardSorter.HasCostColor(c, _colorFilter));
            if (_kindFilterZh != null)
                cards = cards.Where(c => KindMatch(c, _kindFilterZh));
            if (_costBand >= 0)
                cards = cards.Where(c => c.TotalCost >= CostBandLo[_costBand] && c.TotalCost < CostBandHi[_costBand]);
            if (!string.IsNullOrEmpty(_keywordFilter))
                cards = cards.Where(c => c.Keywords != null
                    && c.Keywords.Any(k => k == _keywordFilter || KeywordZh(k) == _keywordFilter));
            string query = _searchField != null ? _searchField.text : null;
            // 2026-10-13 搜索改版：语料并集（名∪id∪卡型∪关键词∪Tags∪技能∪组合后效果全文）多词 AND
            // ——搜「守护」命中带守护关键词的卡、搜效果措辞命中组合卡，不再只匹配卡名
            if (SearchQuery.IsActive(query))
                cards = cards.Where(c => SearchQuery.Match(SearchQuery.BuildCorpus(c), query));

            var sorted = CardSorter.Sort(cards).ToList();
            if (SearchQuery.IsActive(query))
                // 搜索态排序加权：名命中前置（OrderByDescending 稳定——命中组内保持全局 色→费→名 序）；
                // 无搜索词时排序口径不变（2026-09-24 全局定案不受影响）
                sorted = sorted.OrderByDescending(c => SearchQuery.NameHit(c, query)).ToList();

            if (sorted.Count == 0)
            {
                MakeEmptyState(query);
                return;
            }
            foreach (var card in sorted)
            {
                var captured = card;
                MakeListRow(captured, "添加", onAdd: () => AddCard(captured.ID));
            }
        }

        /// <summary>分面行（2026-10-13 搜索改版）：卡型+费用档 chips、关键词下拉。
        /// 词表派生零新增：关键词=CardLoader.LoadKeywords 合成表中文名；与搜索词同语义
        ///（点「守护」=搜「守护」——新手看几次即学会搜索框用法）。</summary>
        private void BuildFacetRows()
        {
            var kindRow = UiKit.Row("facet-kind", _catalogList.Content, spacing: 4f, pad: 4f);
            UiKit.Size(kindRow, fw: 1f);
            MakeChip("全部", kindRow, _kindFilterZh == null, () => { _kindFilterZh = null; RefreshCatalog(); });
            foreach (var kind in KindFilterChoices)
            {
                var captured = kind;
                MakeChip(captured, kindRow, _kindFilterZh == captured,
                    () => { _kindFilterZh = captured; RefreshCatalog(); });
            }

            var costRow = UiKit.Row("facet-cost", _catalogList.Content, spacing: 4f, pad: 4f);
            UiKit.Size(costRow, fw: 1f);
            MakeChip("全部", costRow, _costBand < 0, () => { _costBand = -1; RefreshCatalog(); });
            for (int i = 0; i < CostBandLabels.Length; i++)
            {
                var captured = i;
                MakeChip(CostBandLabels[i], costRow, _costBand == captured,
                    () => { _costBand = captured; RefreshCatalog(); });
            }

            // 关键词下拉（词表≈20，chips 行放不下）；头=MiniButton，弹层挂 _overlay（Dropdown 自理）
            var kwOptions = new List<string> { "关键词:全部" };
            kwOptions.AddRange(CardLoader.LoadKeywords().Values
                .Select(d => d.nameZh).Where(n => !string.IsNullOrEmpty(n)).Distinct()
                .OrderBy(n => n, StringComparer.CurrentCulture));
            int kwIdx = 0;
            if (!string.IsNullOrEmpty(_keywordFilter)) kwIdx = Mathf.Max(0, kwOptions.IndexOf(_keywordFilter));
            var kwHead = UiKit.MiniButton("dropdown-kw", costRow, "关键词", () => { });
            _keywordDropdown = new UiKit.Dropdown(kwHead, _overlay, kwOptions, kwIdx,
                onChanged: (idx, val) =>
                {
                    _keywordFilter = idx <= 0 ? null : val;
                    RefreshCatalog();
                }, width: 130f);
        }

        /// <summary>分面 chip：MiniButton+激活描边（与色 chips 的 Outline 口径一致；
        /// 随内容全量重建，激活态建行时烧入，不需字典态）。</summary>
        private static void MakeChip(string label, RectTransform parent, bool active, Action onClick)
        {
            var chip = UiKit.MiniButton("chip-" + label, parent, label, onClick);
            if (!active) return;
            var ol = chip.gameObject.AddComponent<Outline>();
            ol.effectColor = UiStyle.TextPrimary;
            ol.effectDistance = new Vector2(1.5f, -1.5f);
        }

        /// <summary>卡型谓词：技能=HeroSkillSystem.CanBeSkillCard（2026-10-15 改版：法术且 1~3 个
        /// 主动效果）——独立第四档；「法术」档含技能资格卡（技能卡本就是法术）。</summary>
        private static bool KindMatch(CardData c, string kindZh)
        {
            switch (kindZh)
            {
                case "生物": return c.Supertype == Cardtype.Creature;
                case "法术": return c.Supertype == Cardtype.Spell;
                case "结界": return c.Supertype == Cardtype.Enchantment;
                case "技能": return HeroSkillSystem.CanBeSkillCard(c);
                default: return true;
            }
        }

        /// <summary>空结果可解释（2026-10-13）：列出当前生效的全部筛选 +「清除全部」——
        /// 新手卡死的最大原因是不知道什么在约束列表。</summary>
        private void MakeEmptyState(string query)
        {
            var parts = new List<string>();
            if (_colorFilter != UIColor.All) parts.Add($"色：{ColorZh(_colorFilter)}");
            if (_kindFilterZh != null) parts.Add($"型：{_kindFilterZh}");
            if (_costBand >= 0) parts.Add($"费：{CostBandLabels[_costBand]}");
            if (!string.IsNullOrEmpty(_keywordFilter)) parts.Add($"关键词：{_keywordFilter}");
            if (SearchQuery.IsActive(query)) parts.Add($"词：{query.Trim()}");
            var hint = UiKit.Label("empty", _catalogList.Content,
                parts.Count > 0 ? $"（无匹配卡牌）当前筛选：{string.Join(" + ", parts)}" : "（无匹配卡牌）",
                UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(hint, fw: 1f);
            if (parts.Count > 0)
                UiKit.MiniButton("btn-clear-filters", _catalogList.Content, "清除全部筛选", ClearAllFilters);
        }

        private void ClearAllFilters()
        {
            _colorFilter = UIColor.All;
            _kindFilterZh = null;
            _costBand = -1;
            _keywordFilter = null;
            if (_searchField != null) _searchField.SetTextWithoutNotify("");
            UpdateFilterChips(); // 色 chips 为 prefab 常驻，单独恢复
            RefreshCatalog();
        }

        /// <summary>色名中文（空结果解释行用——chips 文案烘焙在预制体，这里补中文映射）。</summary>
        private static string ColorZh(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return "红";
                case UIColor.Blue: return "蓝";
                case UIColor.Green: return "绿";
                case UIColor.Black: return "黑";
                case UIColor.White: return "白";
                default: return "灰";
            }
        }

        /// <summary>名称行：费用徽标+名称+类型/身材，行点击=预览，行尾按钮=添加——
        /// 可重复添加（2026-10-11 放开「每卡 1 张」限制；唯一效果冲突由 AddCard toast 拦截）。</summary>
        private void MakeListRow(CardData card, string action, Action onAdd)
        {
            var row = UiKit.Row("row", _catalogList.Content, spacing: 8f, pad: 6f);
            var rowBg = UiKit.BgRow(row);
            rowBg.raycastTarget = true; // 行背景可点（行点击=预览；行内按钮在上层，互不串扰）
            UiKit.Size(row, h: 40f);

            var cost = CostBadge("cost", row, ((int)card.TotalCost).ToString());
            var name = UiKit.Label("name", row,
                string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName,
                UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            var meta = UiKit.Label("meta", row, TypeStatLine(card), UiStyle.SmallSize, UiStyle.TextFaint);
            var btn = UiKit.MiniButton("action", row, action, onAdd);

            // 行点击=预览（透明覆盖按钮防误触行内按钮：只挂在行背景上会与行内按钮争抢——
            // UGUI Button 事件由最上层 Graphic 接收，行内按钮在上层，点击它们不会触发行）
            var rowBtn = row.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            rowBtn.targetGraphic = row.GetComponent<Image>();
            rowBtn.onClick.AddListener(() => ShowPreview(card));
        }

        /// <summary>费用徽标（蓝色小圆片+数字，对标 .cost-badge）。</summary>
        private static TMP_Text CostBadge(string name, RectTransform parent, string value)
        {
            var rt = UiKit.Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiKit.CircleSprite;
            img.color = UiStyle.BtnPrimary;
            var lbl = UiKit.Label("label", rt, value, UiStyle.SmallSize, UiStyle.White,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.StretchInset(lbl.rectTransform, 2f, 0f);
            UiKit.Size(rt, w: 26f, h: 24f);
            return lbl;
        }

        private static string TypeStatLine(CardData card)
        {
            switch (card.Supertype)
            {
                case Cardtype.Creature:
                    return $"生物 {(card.Power ?? 0)}/{(card.Life ?? 0)}";
                case Cardtype.Enchantment:
                    return card.Durability > 0 ? $"结界 耐久{card.Durability}" : "结界";
                case Cardtype.Spell:
                    return "法术";
                default:
                    return CardSorter.TypeName(card);
            }
        }

        // ======================================== 中栏：当前卡组 ========================================

        private void AddCard(string id)
        {
            // 构筑规则③（2026-10-11）：30-60 张内重复任意张——上限在添加口即时反馈
            if (_deckCardIds.Count >= MaxDeckCards)
            {
                ShowToast($"卡组已达上限 {MaxDeckCards} 张");
                return;
            }
            // 构筑规则②（2026-10-11）：唯一门/同名仪典每卡组限 1 张——同卡第二张与异卡同名
            // 都算冲突（不排除自身 id），冲突走 toast 反馈
            var card = CardCatalog.GetById(id);
            if (card != null)
            {
                foreach (var slot in ComposerCatalog.DeckUniqueSlots(card))
                {
                    var holder = DeckCardHoldingKey(slot.Key);
                    if (holder != null)
                    {
                        ShowToast($"「{slot.Display}」每卡组限 1 张——已有 {NameOf(holder)}");
                        return;
                    }
                }
                // 主题双色约束（2026-10-15 构成检测）：红/绿/蓝开头的卡组拒加主题外颜色卡
                // （出牌费+启动式锚价两面，DeckCompositionValidator 与编辑器检测同口径）
                var theme = CurrentTheme();
                if (theme != null)
                {
                    var foreign = DeckCompositionValidator.CheckCard(card, theme.Value);
                    if (foreign.Count > 0)
                    {
                        var v = foreign[0];
                        ShowToast($"「{NameOf(card)}」{v.Surface} 含" +
                                  $"{DeckCompositionValidator.ColorZh(v.Color)}{v.Amount:0.#}——" +
                                  $"{DeckCompositionValidator.ColorZh(theme.Value)}+灰卡组不可添加");
                        return;
                    }
                }
            }
            _deckCardIds.Add(id);
            RefreshAll();
        }

        /// <summary>卡组内已持有指定构筑唯一键的卡（无=null）——添加拦截用。
        /// 键源=ComposerCatalog.DeckUniqueSlots（唯一门+同名仪典）；含同 id 已入组册数
        ///（2026-10-11 可重复：同卡第二张同样互斥）。</summary>
        private CardData DeckCardHoldingKey(string key)
        {
            foreach (var id in _deckCardIds)
            {
                var c = CardCatalog.GetById(id);
                if (c != null && ComposerCatalog.DeckUniqueSlots(c).Any(u => u.Key == key))
                    return c;
            }
            return null;
        }

        private static string NameOf(CardData card)
            => card == null ? "?" : string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName;

        private void RemoveCardAt(int index)
        {
            if (index >= 0 && index < _deckCardIds.Count)
            {
                // 技能标记不随卡组移除清除（2026-10-15 改版：技能=全卡池任选法术，不占卡组张数）
                _deckCardIds.RemoveAt(index);
                RefreshAll();
            }
        }

        private void RefreshDeckList()
        {
            _deckBindings.Clear();
            ClearContent(_deckList.Content);

            // 卡组区=UGUI 卡网格：点卡面=预览、右上角×=移除（同旧行语义）
            for (int i = 0; i < _deckCardIds.Count; i++)
            {
                var index = i;
                var card = CardCatalog.GetById(_deckCardIds[i]);
                if (card == null)
                {
                    var missing = UiKit.Label("missing", _deckList.Content,
                        $"[缺失] {_deckCardIds[i]}——卡表中无此 ID", UiStyle.SmallSize, UiStyle.TextFaint);
                    UiKit.Size(missing, fw: 1f);
                    continue;
                }
                var captured = card;
                var slot = UiKit.Node("deck-card-slot", _deckList.Content);
                _deckBindings.Add(new CardOverlayBinding
                {
                    Slot = slot,
                    Item = CardOverlayItem.FromCardData(captured, "D" + captured.ID),
                    Layout = CardOverlayLayout.Full,
                    OnClick = () => ShowPreview(captured),
                    OnRemoveClick = () => RemoveCardAt(index),
                });
            }
        }

        private void RefreshSummary()
        {
            float totalCost = 0f;
            foreach (var id in _deckCardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card != null) totalCost += card.TotalCost;
            }
            _summary.text = $"卡数 {_deckCardIds.Count}（需 {MinDeckCards}-{MaxDeckCards}） · 合计费用 {(int)totalCost}";
        }

        // ======================================== 统计区 ========================================

        private void RefreshStats()
        {
            ClearChildren(_statsZone);
            var cards = _deckCardIds
                .Select(id => CardCatalog.GetById(id))
                .Where(c => c != null)
                .ToList();

            BuildCompositionCheck();
            BuildCostCurve(cards);
            BuildColorDistribution(cards);
            BuildTypeRatio(cards);
            BuildHeroSkillPreview();
        }

        /// <summary>当前卡组名主题色（红/绿/蓝首字；其余 null=无约束）——
        /// 添加口/保存口/统计区检测块三处共用。</summary>
        private ManaType? CurrentTheme()
            => DeckCompositionValidator.ThemeColorOf(_nameField != null ? _nameField.text : null);

        /// <summary>构成检测块（2026-10-15，统计区首块常显）：主题双色约束结果+断链——
        /// 加卡/删卡/改名实时刷新。违色在添加口已拦，此处兜底旧档载入与卡数据热改；
        /// 口径=DeckCompositionValidator（出牌费+启动式锚价两面），与编辑器检测同源。</summary>
        private void BuildCompositionCheck()
        {
            UiKit.Label("check-header", _statsZone, "构成检测", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);

            var report = DeckCompositionValidator.Check(
                _nameField != null ? _nameField.text : null, _deckCardIds);

            if (report.ThemeColor == null)
            {
                var hint = UiKit.Label("check-theme", _statsZone,
                    "无主题约束（卡组名以 红/绿/蓝 开头才启用「主题色+灰」双色检测）",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(hint, fw: 1f);
                if (report.MissingIds.Count == 0) return;
            }
            else
            {
                var line = UiKit.Label("check-theme", _statsZone,
                    $"主题约束：{DeckCompositionValidator.ColorZh(report.ThemeColor.Value)}+灰",
                    UiStyle.SmallSize, UiStyle.TextSecondary);
                UiKit.Size(line, fw: 1f);
            }

            if (report.Ok)
            {
                var ok = UiKit.Label("check-ok", _statsZone,
                    report.CardCount == 0 ? "（空卡组）" : "✔ 通过——无主题外颜色、无断链",
                    UiStyle.SmallSize, UiStyle.DotGreen);
                UiKit.Size(ok, fw: 1f);
                return;
            }

            const int maxRows = 8; // 超出折叠计数——违例长列表滚动区放不下
            int shown = 0;
            foreach (var v in report.Violations)
            {
                if (shown++ >= maxRows) break;
                var row = UiKit.Label("check-bad", _statsZone,
                    $"✘ {v.CardName} ×{v.Copies}｜{v.Surface}｜" +
                    $"{DeckCompositionValidator.ColorZh(v.Color)}{v.Amount:0.#}",
                    UiStyle.SmallSize, UiStyle.DotRed, wrap: true);
                UiKit.Size(row, fw: 1f);
            }
            if (report.Violations.Count > maxRows)
            {
                var more = UiKit.Label("check-more", _statsZone,
                    $"……另有 {report.Violations.Count - maxRows} 项违色",
                    UiStyle.SmallSize, UiStyle.DotRed);
                UiKit.Size(more, fw: 1f);
            }
            foreach (var id in report.MissingIds)
            {
                var miss = UiKit.Label("check-missing", _statsZone,
                    $"✘ 断链卡 {id}——卡表中无此 ID", UiStyle.SmallSize, UiStyle.DotRed, wrap: true);
                UiKit.Size(miss, fw: 1f);
            }
        }

        /// <summary>费用曲线：1-9 档（>9 并入 9+）条形分布。</summary>
        private void BuildCostCurve(List<CardData> cards)
        {
            var tiers = new int[10]; // 0 位弃用；1..9；>9 并入 9（标签显示 9+）
            foreach (var card in cards)
            {
                int tier = Mathf.Clamp(Mathf.RoundToInt(card.TotalCost), 1, 9);
                tiers[tier]++;
            }

            UiKit.Label("curve-header", _statsZone, "费用曲线", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);

            var chart = UiKit.Row("curve", _statsZone, spacing: 2f);
            chart.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft; // 条形自底生长
            UiKit.Size(chart, fw: 1f, h: 64f);
            int max = tiers.Skip(1).Max();
            for (int tier = 1; tier <= 9; tier++)
            {
                int count = tiers[tier];
                var cell = UiKit.Column("tier-" + tier, chart, spacing: 0f);
                cell.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerCenter;
                UiKit.Size(cell, w: 26f, fh: 1f);

                var bar = UiKit.Node("bar", cell);
                var barImg = bar.gameObject.AddComponent<Image>();
                barImg.color = new Color(0.27f, 0.43f, 0.71f, 0.9f);
                UiKit.Size(bar, w: 14f, h: count > 0 ? Mathf.Max(6f, (int)(52f * count / Mathf.Max(1, max))) : 2f);

                var countLabel = UiKit.Label("count", cell, count > 0 ? count.ToString() : "",
                    UiStyle.SmallSize, UiStyle.TextFaint, TextAnchor.MiddleCenter);
                var tierLabel = UiKit.Label("tier", cell, tier == 9 ? "9+" : tier.ToString(),
                    UiStyle.SmallSize, UiStyle.TextHint, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>颜色分布：六色计数（色点+数字），与英雄指派的主色统计同源视角。</summary>
        private void BuildColorDistribution(List<CardData> cards)
        {
            var row = UiKit.Row("colors", _statsZone, spacing: 4f);

            foreach (UIColor color in new[] { UIColor.Red, UIColor.Blue, UIColor.Green, UIColor.Gray, UIColor.Black, UIColor.White })
            {
                int count = cards.Count(c => CardSorter.HasCostColor(c, color));
                UiKit.Dot($"dot-{color}", row, DotColor(color));
                var num = UiKit.Label("num", row, $"{count}", UiStyle.SmallSize, UiStyle.TextFaint);
                UiKit.Size(num, w: 26f);
            }
        }

        private static Color DotColor(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return UiStyle.DotRed;
                case UIColor.Blue: return UiStyle.DotBlue;
                case UIColor.Green: return UiStyle.DotGreen;
                case UIColor.Black: return UiStyle.DotBlack;
                case UIColor.White: return UiStyle.DotWhite;
                default: return UiStyle.DotGray;
            }
        }

        /// <summary>类型比例：生物/法术/结界计数（生物数=可作地牌的置入能力参考）。</summary>
        private void BuildTypeRatio(List<CardData> cards)
        {
            int creatures = cards.Count(c => c.Supertype == Cardtype.Creature);
            int spells = cards.Count(c => c.Supertype == Cardtype.Spell);
            int enchantments = cards.Count(c => c.Supertype == Cardtype.Enchantment);
            int other = cards.Count - creatures - spells - enchantments;

            var line = UiKit.Label("type-ratio", _statsZone,
                $"类型：生物 {creatures} · 法术 {spells} · 结界 {enchantments}"
                + (other > 0 ? $" · 其他 {other}" : ""),
                UiStyle.SmallSize, UiStyle.TextDim);
            UiKit.Size(line, fw: 1f);
        }

        /// <summary>英雄技能预览（2026-10-07 卡牌化）：显示构筑标记的技能结界（发动费用=唯一
        /// 主动效果的派生元素价，非卡面费用）；未标记=对局无英雄技能。</summary>
        private void BuildHeroSkillPreview()
        {
            UiKit.Label("skill-header", _statsZone, "英雄技能（构筑标记）", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);

            var marked = string.IsNullOrEmpty(_skillCardId) ? null : CardCatalog.GetById(_skillCardId);
            if (marked == null)
            {
                var none = UiKit.Label("none", _statsZone,
                    "未标记——保存前必须标记英雄技能（一张法术；卡池浏览点卡，在预览里标记——不占卡组张数）",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(none, fw: 1f);
                return;
            }

            var title = UiKit.Label("skill-name", _statsZone,
                string.IsNullOrEmpty(marked.CardName) ? marked.ID : marked.CardName,
                UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(title, fw: 1f);

            // 费用行（2026-10-15 改版）：各效果现付锚价（多效果显示区间）；发动=每回合一次
            var anchors = HeroSkillSystem.SkillEffectsOf(new CardWrapper(marked))
                .Select(f => CostDerivationService.DeriveElementCosts(f))
                .Where(c => c != null).ToList();
            string costText = anchors.Count == 0 ? "无"
                : string.Join(" ", anchors[0].NonzeroColors().Select(c => $"{c}×{anchors[0][c]:0.#}"));
            var costLine = UiKit.Label("skill-cost", _statsZone,
                anchors.Count <= 1
                    ? $"发动费用：{costText}（每回合一次，现付所选效果费用）"
                    : $"发动费用：{anchors.Min(a => a.Total):0.#}~{anchors.Max(a => a.Total):0.#} 区间" +
                      $"（{marked.Effects.Count} 个作用择一发动；每回合一次）",
                UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            UiKit.Size(costLine, fw: 1f);

            if (marked.Effects != null)
                foreach (var eff in marked.Effects)
                {
                    if (eff == null) continue;
                    var graph = new EffectGraphData(eff.DisplayName) { header = eff, steps = eff.Steps };
                    var body = UiKit.Label("skill-body", _statsZone, AtomText.RenderEffectSummary(graph, marked),
                        UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
                    UiKit.Size(body, fw: 1f);
                }
        }

        // ======================================== 左栏：效果预览 ========================================

        private void ShowPreview(CardData card)
        {
            _previewCard = card;
            BuildPreview();
        }

        private void BuildPreview()
        {
            ClearContent(_previewZone.Content);
            var card = _previewCard;
            if (card == null)
            {
                var hint = UiKit.Label("hint", _previewZone.Content,
                    "点击卡牌列表或卡组中的行，在此显示卡牌效果预览",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(hint, fw: 1f);
                return;
            }

            // 头部：费用徽标 + 名称 + 类型/身材（或耐久）
            var head = UiKit.Row("head", _previewZone.Content, spacing: 8f);
            CostBadge("cost", head, ((int)card.TotalCost).ToString());
            var name = UiKit.Label("name", head,
                string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName,
                UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            UiKit.Label("meta", head, TypeStatLine(card), UiStyle.SmallSize, UiStyle.TextFaint);

            // 费用构成（2026-10-04 位置数组口径，序数序=枚举序）
            if (card.Cost != null && !card.Cost.IsZero)
            {
                var costLine = UiKit.Label("cost-line", _previewZone.Content, "费用：" + string.Join(" ",
                    card.Cost.NonzeroColors().Select(c => $"{c}×{card.Cost[c]:0.#}")),
                    UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
                UiKit.Size(costLine, fw: 1f);
            }

            // 关键词（本体=直接引用原子，2026-09-24 引用化定案）
            if (card.Keywords != null && card.Keywords.Count > 0)
            {
                var kwLine = UiKit.Label("kw-line", _previewZone.Content,
                    "关键词：" + string.Join("·", card.Keywords.Select(KeywordZh)),
                    UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
                UiKit.Size(kwLine, fw: 1f);
            }

            // 代价栏（Payload）
            var payload = card.PayloadCost?.payload;
            if (payload != null && !string.IsNullOrEmpty(payload.refId))
            {
                var payLine = UiKit.Label("pay-line", _previewZone.Content,
                    "代价：" + AtomText.RenderAtomEntry(payload),
                    UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
                UiKit.Size(payLine, fw: 1f);
            }

            // 效果全文（AtomText 单一来源）
            if (card.Effects != null && card.Effects.Count > 0)
            {
                var fxHeader = UiKit.Label("fx-header", _previewZone.Content, "效果", UiStyle.HeaderSize,
                    UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
                UiKit.Size(fxHeader, fw: 1f);
                foreach (var fx in card.Effects)
                {
                    if (fx == null) continue;
                    var title = UiKit.Label("fx-title", _previewZone.Content,
                        string.IsNullOrEmpty(fx.DisplayName) ? "效果" : fx.DisplayName,
                        UiStyle.BodySize, UiStyle.TextBody);
                    UiKit.Size(title, fw: 1f);

                    var graph = new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps };
                    var body = UiKit.Label("fx-body", _previewZone.Content, AtomText.RenderEffectSummary(graph, card),
                        UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
                    UiKit.Size(body, fw: 1f);
                }
            }

            // 标签
            if (card.Tags != null && card.Tags.Count > 0)
            {
                var tagLine = UiKit.Label("tag-line", _previewZone.Content,
                    "标签：" + string.Join("·", card.Tags), UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(tagLine, fw: 1f);
            }

            // 英雄技能标记（2026-10-15 改版：技能=全卡池任选一张法术，不占卡组张数——
            // 卡池浏览侧即可标记；资格=法术且 1~3 个主动效果）
            {
                bool marked = _skillCardId == card.ID;
                bool eligible = HeroSkillSystem.CanBeSkillCard(card);
                var markRow = UiKit.Row("skill-mark", _previewZone.Content, spacing: 8f);
                var markBtn = UiKit.MiniButton("btn-skill-mark", markRow,
                    marked ? "取消英雄技能标记" : "标记为英雄技能", () => ToggleSkillMark(card));
                markBtn.interactable = eligible || marked; // 已标记的总是可取消
                if (!eligible)
                {
                    var why = UiKit.Label("skill-why", markRow,
                        "不可标记：" + HeroSkillSystem.SkillIneligibleReason(card),
                        UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                    UiKit.Size(why, fw: 1f);
                }
            }
        }

        /// <summary>切换英雄技能标记（2026-10-15 改版）：技能=任选一张法术——开局从卡池生成落技能栏，
        /// 不占卡组张数、不进牌库；发动=每回合一次+现付所选效果锚价。不合格（非法术/效果非 1~3 主动）
        /// 拦截并 toast 原因。</summary>
        private void ToggleSkillMark(CardData card)
        {
            if (_skillCardId == card.ID)
            {
                _skillCardId = null;
                ShowToast("已取消英雄技能标记");
            }
            else
            {
                var reason = HeroSkillSystem.SkillIneligibleReason(card);
                if (reason != null)
                {
                    ShowToast("不可标记：" + reason);
                    return;
                }
                _skillCardId = card.ID;
                ShowToast($"已标记「{card.CardName}」为英雄技能——开局落技能栏（不占卡组张数），每回合可发动一次");
            }
            RefreshAll();
        }

        private static string KeywordZh(string keywordId)
        {
            var def = CardLoader.GetKeywordDefinition(keywordId);
            return string.IsNullOrEmpty(def?.nameZh) ? keywordId : def.nameZh;
        }

        // ======================================== 刷新总口 ========================================

        private void RefreshAll()
        {
            RefreshCatalog();
            RefreshDeckList();
            RefreshSummary();
            RefreshStats();
            BuildPreview();

            // UGUI 卡牌层重绑（卡实例挂卡组网格槽位；滚动裁剪由 RectMask2D 自动处理）
            CardOverlayController.Instance.Bind(_deckBindings);
        }

        // ======================================== 存读删 ========================================

        private void OnSave()
        {
            var name = string.IsNullOrWhiteSpace(_nameField.text) ? "新卡组" : _nameField.text.Trim();

            // 构筑规则③（2026-10-11）：卡组 30-60 张（卡可重复任意张——旧「每卡 1 张」限制废止）
            if (_deckCardIds.Count < MinDeckCards || _deckCardIds.Count > MaxDeckCards)
            {
                ShowToast($"卡组须 {MinDeckCards}-{MaxDeckCards} 张（当前 {_deckCardIds.Count} 张）");
                return;
            }

            // 构筑唯一键校验（2026-10-11 构筑规则②）：唯一门/同名仪典每卡组限 1 张——
            // 同卡重复与异卡同名都算冲突（添加口已拦，此处兜底旧档载入/卡数据热改）
            var seenKeys = new HashSet<string>();
            foreach (var id in _deckCardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue;
                foreach (var slot in ComposerCatalog.DeckUniqueSlots(card))
                {
                    if (!seenKeys.Add(slot.Key))
                    {
                        ShowToast($"「{slot.Display}」每卡组限 1 张（{NameOf(card)} 冲突）——请先移除多余的一张");
                        return;
                    }
                }
            }

            // 构成检测硬门槛（2026-10-15）：断链卡组（Cards.json 无此 ID）与主题违色
            // （红/绿/蓝开头卡组含主题色+灰以外的出牌费/启动式锚价）不可保存——违色在
            // 添加口已拦，此处兜底旧档载入/卡数据热改；口径=DeckCompositionValidator
            var composition = DeckCompositionValidator.Check(name, _deckCardIds);
            if (composition.MissingIds.Count > 0)
            {
                ShowToast($"卡组含 {composition.MissingIds.Count} 张断链卡（卡表无此 ID）——不可保存");
                return;
            }
            // 类型×形状硬门槛（2026-10-15 深夜改版）：生物禁光环/结界禁主动/法术形状——兜底旧档载入/卡数据热改
            if (composition.ShapeErrors.Count > 0)
            {
                ShowToast($"卡组含 {composition.ShapeErrors.Count} 项类型形状违规（首项：{composition.ShapeErrors[0]}）——不可保存");
                return;
            }
            if (composition.Violations.Count > 0)
            {
                var v0 = composition.Violations[0];
                ShowToast($"主题约束 {DeckCompositionValidator.ColorZh(composition.ThemeColor.Value)}+灰：" +
                          $"{composition.Violations.Count} 项违色（首项 {v0.CardName}｜{v0.Surface}｜" +
                          $"{DeckCompositionValidator.ColorZh(v0.Color)}{v0.Amount:0.#}）——不可保存");
                return;
            }

            // 技能标记自愈（2026-10-15 改版：技能=卡池任选法术，不要求在卡组内）：资格失效 → 清标记；
            // 构筑规则①：未标记不可保存——自愈后落下方必选拦截
            if (!string.IsNullOrEmpty(_skillCardId))
            {
                var markedCard = CardCatalog.GetById(_skillCardId);
                if (markedCard == null || !HeroSkillSystem.CanBeSkillCard(markedCard))
                {
                    _skillCardId = null;
                    ShowToast("技能标记已清除（卡池无此卡或资格失效）——请重新标记英雄技能");
                }
            }
            if (string.IsNullOrEmpty(_skillCardId))
            {
                ShowToast("必须选英雄技能（一张法术——卡池浏览点卡，在预览里标记；不占卡组张数）");
                return;
            }

            var deck = new DeckData(name)
            {
                cardIds = new List<string>(_deckCardIds),
                skillCardId = _skillCardId,
            };
            var path = DeckSerializer.Save(deck);
            if (path != null) TutorialCreationFlow.NotifyDecksChanged(); // 第三课走查步检测（未开课零行为）
            ShowToast(path == null ? "保存失败" : $"已保存：{name}");
            RefreshDecksDropdown();
            if (_decksDropdown != null)
            {
                int idx = DeckSerializer.LoadAll().Select(d => d.name).ToList().IndexOf(name);
                if (idx >= 0) _decksDropdown.SetIndex(idx);
            }
        }

        private void OnLoad()
        {
            var name = _decksDropdown?.Value;
            if (string.IsNullOrEmpty(name))
            {
                ShowToast("请先选择卡组");
                return;
            }
            var deck = DeckSerializer.Load(name);
            if (deck == null)
            {
                ShowToast("读取失败");
                return;
            }
            _deckCardIds.Clear();
            if (deck.cardIds != null)
            {
                _deckCardIds.AddRange(deck.cardIds);
            }
            // 技能标记读取 + 自愈（2026-10-15 改版：技能=卡池任选法术，不要求在卡组内；
            // 旧档 null / 资格失效 → 清）
            _skillCardId = null;
            if (!string.IsNullOrEmpty(deck.skillCardId)
                && CardCatalog.GetById(deck.skillCardId) is { } skill
                && HeroSkillSystem.CanBeSkillCard(skill))
            {
                _skillCardId = deck.skillCardId;
            }
            _nameField.SetTextWithoutNotify(deck.name);
            RefreshAll();
            ShowToast($"已载入：{deck.name}");
        }

        private void OnDelete()
        {
            var name = _decksDropdown?.Value;
            if (string.IsNullOrEmpty(name))
            {
                ShowToast("请先选择要删除的卡组");
                return;
            }
            if (DeckSerializer.Delete(name))
            {
                ShowToast($"已删除卡组：{name}");
                RefreshDecksDropdown();
            }
            else
            {
                ShowToast("删除失败（卡组不存在）");
            }
        }

        // 刷新读取下拉的可选卡组列表（保持现选；无选/失效取第一套）。
        private void RefreshDecksDropdown()
        {
            if (_decksDropdown == null) return;
            var names = DeckSerializer.LoadAll().Select(d => d.name).ToList();
            int index = names.IndexOf(_decksDropdown.Value);
            if (index < 0) index = names.Count > 0 ? 0 : -1;
            _decksDropdown.SetOptions(names, index);
        }

        // 临时提示文本（无需事件，直接写 Label）。
        private void ShowToast(string message)
        {
            _toast.text = message;
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
