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
    /// 卡组构建界面（2026-10-01 预制体化：静态层级来自 Assets/Art/UI/DeckUI.prefab，
    /// Build 深度按名绑定+闭包接线；滚动列表行/统计区/预览内容运行时重建）——三栏：
    /// 左=效果预览（点击行显示），中=当前卡组+统计（费用曲线/颜色分布/类型比例/英雄技能预览），
    /// 右=卡牌列表（颜色过滤+搜索+全局排序，已添加卡的添加按钮禁用）。
    ///
    /// 合法性规则（2026-09-24 定案）：**只查重复**（每卡 1 张）——不设张数硬规则；
    /// 重复由 UI 禁用按钮天然防住，保存口兜底拦截。超模检查已随 2026-09-24 定案移除。
    /// 卡组区=UGUI 卡网格（ScrollGrid+卡实例挂槽位）。
    /// </summary>
    public sealed class DeckBuilderScreen : UIScreen
    {
        protected override string PrefabAddress => "DeckUI";
        protected override string PrefabAssetPath => "Assets/Art/UI/DeckUI.prefab";
        protected override string RootName => "deck-builder";

        // 当前正在构筑的卡组（卡牌 ID 列表）。
        private readonly List<string> _deckCardIds = new List<string>();

        // UGUI 卡牌层绑定（卡组区=卡槽网格 + UGUI 卡面；RefreshAll 全量重建）。
        private readonly List<CardOverlayBinding> _deckBindings = new List<CardOverlayBinding>();

        // 颜色过滤 chips（预制体烘焙，Build 一次绑定；激活态=Outline 开关）。
        private readonly Dictionary<UIColor, UButton> _filterChips = new Dictionary<UIColor, UButton>();

        private UIColor _colorFilter = UIColor.All;
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
            _overlay = FindOptional("overlay") ?? UiKit.Overlay("overlay", Root);

            // ---- 工具栏 ----
            BindButton("btn-back", () => Manager.Back());
            _nameField = FindInput("field-deck-name");
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
            ClearContent(_catalogList.Content);
            IEnumerable<CardData> cards = CardCatalog.LoadAll();
            if (_colorFilter != UIColor.All)
                cards = cards.Where(c => CardSorter.HasCostColor(c, _colorFilter));
            string query = _searchField != null ? _searchField.text : null;
            if (!string.IsNullOrWhiteSpace(query))
                cards = cards.Where(c => (c.CardName ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);

            var sorted = CardSorter.Sort(cards).ToList();
            if (sorted.Count == 0)
            {
                var empty = UiKit.Label("empty", _catalogList.Content, "（无匹配卡牌）",
                    UiStyle.SmallSize, UiStyle.TextHint);
                UiKit.Size(empty, fw: 1f);
                return;
            }
            foreach (var card in sorted)
            {
                var captured = card;
                MakeListRow(captured, "添加",
                    onAdd: () => AddCard(captured.ID),
                    added: _deckCardIds.Contains(captured.ID));
            }
        }

        /// <summary>名称行（2026-09-24 定案）：费用徽标+名称+类型/身材，行点击=预览，行尾按钮=添加/移除。</summary>
        private void MakeListRow(CardData card, string action, Action onAdd, bool added)
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
            btn.interactable = !added; // 已在卡组——添加按钮禁用（重复由 UI 天然防住）

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
                    return "瞬间";
                default:
                    return CardSorter.TypeName(card);
            }
        }

        // ======================================== 中栏：当前卡组 ========================================

        private void AddCard(string id)
        {
            if (_deckCardIds.Contains(id))
            {
                ShowToast("该卡已在卡组（每卡 1 张）");
                return;
            }
            _deckCardIds.Add(id);
            RefreshAll();
        }

        private void RemoveCardAt(int index)
        {
            if (index >= 0 && index < _deckCardIds.Count)
            {
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
            _summary.text = $"卡数 {_deckCardIds.Count} · 合计费用 {(int)totalCost}";
        }

        // ======================================== 统计区 ========================================

        private void RefreshStats()
        {
            ClearChildren(_statsZone);
            var cards = _deckCardIds
                .Select(id => CardCatalog.GetById(id))
                .Where(c => c != null)
                .ToList();

            BuildCostCurve(cards);
            BuildColorDistribution(cards);
            BuildTypeRatio(cards);
            BuildHeroSkillPreview(cards);
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

        /// <summary>类型比例：生物/瞬间/结界计数（生物数=可作地牌的置入能力参考）。</summary>
        private void BuildTypeRatio(List<CardData> cards)
        {
            int creatures = cards.Count(c => c.Supertype == Cardtype.Creature);
            int spells = cards.Count(c => c.Supertype == Cardtype.Spell);
            int enchantments = cards.Count(c => c.Supertype == Cardtype.Enchantment);
            int other = cards.Count - creatures - spells - enchantments;

            var line = UiKit.Label("type-ratio", _statsZone,
                $"类型：生物 {creatures} · 瞬间 {spells} · 结界 {enchantments}"
                + (other > 0 ? $" · 其他 {other}" : ""),
                UiStyle.SmallSize, UiStyle.TextDim);
            UiKit.Size(line, fw: 1f);
        }

        /// <summary>英雄技能预览：按当前卡组自动指派（Theme 标签优先、再费用主色，灰不参选——同开局口径）。</summary>
        private void BuildHeroSkillPreview(List<CardData> cards)
        {
            UiKit.Label("skill-header", _statsZone, "英雄技能（开局自动指派）", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);

            var skill = HeroSkillSystem.AutoSkillForDeck(
                cards.Select(c => (Card)new CardWrapper(c)).ToList());
            if (skill == HeroSkillId.None)
            {
                var none = UiKit.Label("none", _statsZone, "无主色（灰不参选）——开局不指派英雄技能",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(none, fw: 1f);
                return;
            }

            var title = UiKit.Label("skill-name", _statsZone, HeroSkillSystem.SkillName(skill),
                UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(title, fw: 1f);

            var baseLine = UiKit.Label("skill-base", _statsZone, "基础：" + HeroSkillSystem.Describe(skill, false),
                UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            UiKit.Size(baseLine, fw: 1f);

            var upLine = UiKit.Label("skill-up", _statsZone, "升级（第 8 次发动起）：" + HeroSkillSystem.Describe(skill, true),
                UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            UiKit.Size(upLine, fw: 1f);
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

            // 费用构成
            if (card.Cost != null && card.Cost.Count > 0)
            {
                var costLine = UiKit.Label("cost-line", _previewZone.Content, "费用：" + string.Join(" ",
                    card.Cost.Select(kv => $"{(ManaType)kv.Key}×{kv.Value:0.#}")),
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
                    var body = UiKit.Label("fx-body", _previewZone.Content, AtomText.RenderEffectSummary(graph),
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

            // 合法性（2026-09-24 定案：只查重复，每卡 1 张）
            var dupes = _deckCardIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0)
            {
                ShowToast($"存在重复卡（{dupes.Count} 种）——卡组不重复（每卡 1 张），请先移除重复项");
                return;
            }

            var deck = new DeckData(name) { cardIds = new List<string>(_deckCardIds) };
            var path = DeckSerializer.Save(deck);
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
