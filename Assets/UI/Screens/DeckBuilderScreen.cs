using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 卡组构建界面（2026-09-24 重做）——三栏：左=效果预览（点击行显示），中=当前卡组+统计
    /// （费用曲线/颜色分布/类型比例/英雄技能预览），右=卡牌列表（颜色过滤+搜索+全局排序，
    /// 已添加卡的添加按钮禁用）。
    ///
    /// 合法性规则（2026-09-24 定案）：**只查重复**（每卡 1 张）——不设张数硬规则；
    /// 重复由 UI 禁用按钮天然防住，保存口兜底拦截。超模检查已随 2026-09-24 定案移除。
    /// </summary>
    public sealed class DeckBuilderScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/DeckBuilder";

        // 当前正在构筑的卡组（卡牌 ID 列表）。
        private readonly List<string> _deckCardIds = new List<string>();

        private UIColor _colorFilter = UIColor.All;
        private CardData _previewCard;

        private ScrollView _catalogList;
        private ScrollView _deckList;
        private ScrollView _previewZone;
        private VisualElement _statsZone;
        private VisualElement _catalogFilter;
        private Label _summary;
        private Label _toast;
        private TextField _nameField;
        private TextField _searchField;
        private DropdownField _decksDropdown;

        public override void OnEnter()
        {
            _catalogList = Q<ScrollView>("list-catalog");
            _deckList = Q<ScrollView>("list-deck");
            _previewZone = Q<ScrollView>("preview-zone");
            _statsZone = Q<VisualElement>("stats-zone");
            _catalogFilter = Q<VisualElement>("catalog-filter");
            _summary = Q<Label>("lbl-summary");
            _toast = Q<Label>("lbl-toast");
            _nameField = Q<TextField>("field-deck-name");
            _searchField = Q<TextField>("field-search");
            _decksDropdown = Q<DropdownField>("dropdown-decks");

            UIBinder.BindButton(Root, "btn-back", () => Manager.Back());
            UIBinder.BindButton(Root, "btn-save", OnSave);
            UIBinder.BindButton(Root, "btn-load", OnLoad);
            UIBinder.BindButton(Root, "btn-delete", OnDelete);
            _searchField.RegisterValueChangedCallback(_ => RefreshCatalog());

            BuildFilterRow();
            RefreshDecksDropdown();
            RefreshAll();
        }

        // ======================================== 右栏：卡牌列表 ========================================

        private void BuildFilterRow()
        {
            _catalogFilter.Clear();
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
                    BuildFilterRow(); // 重建以刷新激活态
                    RefreshCatalog();
                };
                _catalogFilter.Add(chip);
            }
        }

        private void RefreshCatalog()
        {
            _catalogList.Clear();
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
                _catalogList.Add(empty);
                return;
            }
            foreach (var card in sorted)
            {
                var captured = card;
                _catalogList.Add(MakeListRow(captured, "添加",
                    onAdd: () => AddCard(captured.ID),
                    added: _deckCardIds.Contains(captured.ID)));
            }
        }

        /// <summary>名称行（2026-09-24 定案）：费用徽标+名称+类型/身材，行点击=预览，行尾按钮=添加/移除。</summary>
        private VisualElement MakeListRow(CardData card, string action, Action onAdd, bool added)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            var cost = new Label(((int)card.TotalCost).ToString());
            cost.AddToClassList("cost-badge");
            row.Add(cost);

            var name = new Label(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName);
            name.AddToClassList("list-row__name");
            name.style.flexGrow = 1;
            row.Add(name);

            var meta = new Label(TypeStatLine(card));
            meta.AddToClassList("list-row__meta");
            row.Add(meta);

            var btn = new Button(onAdd) { text = action };
            btn.AddToClassList("btn");
            btn.AddToClassList("btn--mini");
            if (added) btn.SetEnabled(false); // 已在卡组——添加按钮禁用（重复由 UI 天然防住）
            row.Add(btn);

            row.RegisterCallback<ClickEvent>(_ => ShowPreview(card));
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
            _deckList.Clear();
            for (int i = 0; i < _deckCardIds.Count; i++)
            {
                var index = i;
                var card = CardCatalog.GetById(_deckCardIds[i]);
                if (card == null)
                {
                    var missing = new Label($"[缺失] {_deckCardIds[i]}——卡表中无此 ID");
                    missing.AddToClassList("list-row__meta");
                    _deckList.Add(missing);
                    continue;
                }
                var captured = card;
                var row = MakeDeckRow(captured, () => RemoveCardAt(index));
                _deckList.Add(row);
            }
        }

        private VisualElement MakeDeckRow(CardData card, Action onRemove)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            var cost = new Label(((int)card.TotalCost).ToString());
            cost.AddToClassList("cost-badge");
            row.Add(cost);

            var name = new Label(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName);
            name.AddToClassList("list-row__name");
            name.style.flexGrow = 1;
            row.Add(name);

            var meta = new Label(TypeStatLine(card));
            meta.AddToClassList("list-row__meta");
            row.Add(meta);

            var btn = new Button(onRemove) { text = "移除" };
            btn.AddToClassList("btn");
            btn.AddToClassList("btn--mini");
            btn.AddToClassList("btn--danger");
            row.Add(btn);

            row.RegisterCallback<ClickEvent>(_ => ShowPreview(card));
            return row;
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
            _statsZone.Clear();
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

            var header = new Label("费用曲线");
            header.AddToClassList("panel__header");
            _statsZone.Add(header);

            var chart = new VisualElement();
            chart.style.flexDirection = FlexDirection.Row;
            chart.style.alignItems = Align.FlexEnd;
            chart.style.height = 64;
            chart.style.marginBottom = 4;
            int max = tiers.Skip(1).Max();
            for (int tier = 1; tier <= 9; tier++)
            {
                int count = tiers[tier];
                var cell = new VisualElement();
                cell.style.width = 26;
                cell.style.alignItems = Align.Center;
                cell.style.justifyContent = Justify.FlexEnd;
                cell.style.marginRight = 2;

                var bar = new VisualElement();
                bar.style.width = 14;
                bar.style.height = count > 0 ? Mathf.Max(6, (int)(52f * count / Mathf.Max(1, max))) : 2;
                bar.style.backgroundColor = new Color(0.27f, 0.43f, 0.71f, 0.9f);
                bar.style.borderTopLeftRadius = 2;
                bar.style.borderTopRightRadius = 2;
                cell.Add(bar);

                var countLabel = new Label(count > 0 ? count.ToString() : "");
                countLabel.AddToClassList("list-row__meta");
                cell.Add(countLabel);

                var tierLabel = new Label(tier == 9 ? "9+" : tier.ToString());
                tierLabel.AddToClassList("hint");
                cell.Add(tierLabel);
                chart.Add(cell);
            }
            _statsZone.Add(chart);
        }

        /// <summary>颜色分布：六色计数（色点+数字），与英雄指派的主色统计同源视角。</summary>
        private void BuildColorDistribution(List<CardData> cards)
        {
            var row = new VisualElement();
            row.AddToClassList("toolbar");
            row.style.flexWrap = Wrap.Wrap;

            foreach (UIColor color in new[] { UIColor.Red, UIColor.Blue, UIColor.Green, UIColor.Gray, UIColor.Black, UIColor.White })
            {
                int count = cards.Count(c => CardSorter.HasCostColor(c, color));
                var dot = new Label { name = $"dot-{color}" };
                dot.AddToClassList("color-dot");
                dot.AddToClassList(ChipClass(color));
                row.Add(dot);

                var num = new Label($"{count}");
                num.AddToClassList("list-row__meta");
                num.style.marginRight = 8;
                row.Add(num);
            }
            _statsZone.Add(row);
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

        /// <summary>类型比例：生物/瞬间/结界计数（生物数=可作地牌的置入能力参考）。</summary>
        private void BuildTypeRatio(List<CardData> cards)
        {
            int creatures = cards.Count(c => c.Supertype == Cardtype.Creature);
            int spells = cards.Count(c => c.Supertype == Cardtype.Spell);
            int enchantments = cards.Count(c => c.Supertype == Cardtype.Enchantment);
            int other = cards.Count - creatures - spells - enchantments;

            var line = new Label($"类型：生物 {creatures} · 瞬间 {spells} · 结界 {enchantments}"
                + (other > 0 ? $" · 其他 {other}" : ""));
            line.AddToClassList("list-row__meta");
            _statsZone.Add(line);
        }

        /// <summary>英雄技能预览：按当前卡组自动指派（Theme 标签优先、再费用主色，灰不参选——同开局口径）。</summary>
        private void BuildHeroSkillPreview(List<CardData> cards)
        {
            var header = new Label("英雄技能（开局自动指派）");
            header.AddToClassList("panel__header");
            _statsZone.Add(header);

            var skill = HeroSkillSystem.AutoSkillForDeck(
                cards.Select(c => (Card)new CardWrapper(c)).ToList());
            if (skill == HeroSkillId.None)
            {
                var none = new Label("无主色（灰不参选）——开局不指派英雄技能");
                none.AddToClassList("hint");
                _statsZone.Add(none);
                return;
            }

            var title = new Label(HeroSkillSystem.SkillName(skill));
            title.AddToClassList("list-row__name");
            _statsZone.Add(title);

            var baseLine = new Label("基础：" + HeroSkillSystem.Describe(skill, false));
            baseLine.AddToClassList("list-row__meta");
            baseLine.style.whiteSpace = WhiteSpace.Normal;
            _statsZone.Add(baseLine);

            var upLine = new Label("升级（第 8 次发动起）：" + HeroSkillSystem.Describe(skill, true));
            upLine.AddToClassList("list-row__meta");
            upLine.style.whiteSpace = WhiteSpace.Normal;
            _statsZone.Add(upLine);
        }

        // ======================================== 左栏：效果预览 ========================================

        private void ShowPreview(CardData card)
        {
            _previewCard = card;
            BuildPreview();
        }

        private void BuildPreview()
        {
            _previewZone.Clear();
            var card = _previewCard;
            if (card == null)
            {
                var hint = new Label("点击卡牌列表或卡组中的行，在此显示卡牌效果预览");
                hint.AddToClassList("hint");
                _previewZone.Add(hint);
                return;
            }

            // 头部：费用徽标 + 名称 + 类型/身材（或耐久）
            var head = new VisualElement();
            head.AddToClassList("toolbar");
            var cost = new Label(((int)card.TotalCost).ToString());
            cost.AddToClassList("cost-badge");
            head.Add(cost);
            var name = new Label(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName);
            name.AddToClassList("list-row__name");
            head.Add(name);
            var meta = new Label(TypeStatLine(card));
            meta.AddToClassList("list-row__meta");
            head.Add(meta);
            _previewZone.Add(head);

            // 费用构成
            if (card.Cost != null && card.Cost.Count > 0)
            {
                var costLine = new Label("费用：" + string.Join(" ",
                    card.Cost.Select(kv => $"{(ManaType)kv.Key}×{kv.Value:0.#}")));
                costLine.AddToClassList("list-row__meta");
                _previewZone.Add(costLine);
            }

            // 关键词（本体=直接引用原子，2026-09-24 引用化定案）
            if (card.Keywords != null && card.Keywords.Count > 0)
            {
                var kwLine = new Label("关键词：" + string.Join("·", card.Keywords.Select(KeywordZh)));
                kwLine.AddToClassList("list-row__meta");
                _previewZone.Add(kwLine);
            }

            // 代价栏（Payload）
            var payload = card.PayloadCost?.payload;
            if (payload != null && !string.IsNullOrEmpty(payload.refId))
            {
                var payLine = new Label("代价：" + AtomText.RenderAtomEntry(payload));
                payLine.AddToClassList("list-row__meta");
                _previewZone.Add(payLine);
            }

            // 效果全文（AtomText 单一来源）
            if (card.Effects != null && card.Effects.Count > 0)
            {
                var fxHeader = new Label("效果");
                fxHeader.AddToClassList("panel__header");
                _previewZone.Add(fxHeader);
                foreach (var fx in card.Effects)
                {
                    if (fx == null) continue;
                    var title = new Label(string.IsNullOrEmpty(fx.DisplayName) ? "效果" : fx.DisplayName);
                    title.AddToClassList("list-row__name");
                    _previewZone.Add(title);

                    var graph = new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps };
                    var body = new Label(AtomText.RenderEffectSummary(graph));
                    body.AddToClassList("list-row__meta");
                    body.style.whiteSpace = WhiteSpace.Normal;
                    _previewZone.Add(body);
                }
            }

            // 标签
            if (card.Tags != null && card.Tags.Count > 0)
            {
                var tagLine = new Label("标签：" + string.Join("·", card.Tags));
                tagLine.AddToClassList("hint");
                _previewZone.Add(tagLine);
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
        }

        // ======================================== 存读删 ========================================

        private void OnSave()
        {
            var name = string.IsNullOrWhiteSpace(_nameField.value) ? "新卡组" : _nameField.value.Trim();

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
            if (_decksDropdown.choices.Contains(name))
            {
                _decksDropdown.SetValueWithoutNotify(name);
            }
        }

        private void OnLoad()
        {
            var name = _decksDropdown.value;
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
            _nameField.SetValueWithoutNotify(deck.name);
            RefreshAll();
            ShowToast($"已载入：{deck.name}");
        }

        private void OnDelete()
        {
            var name = _decksDropdown.value;
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

        // 刷新读取下拉的可选卡组列表。
        private void RefreshDecksDropdown()
        {
            var names = DeckSerializer.LoadAll().Select(d => d.name).ToList();
            _decksDropdown.choices = names;
            if (names.Count > 0 && string.IsNullOrEmpty(_decksDropdown.value))
            {
                _decksDropdown.SetValueWithoutNotify(names[0]);
            }
        }

        // 临时提示文本（无需事件，直接写 Label）。
        private void ShowToast(string message)
        {
            _toast.text = message;
        }
    }
}
