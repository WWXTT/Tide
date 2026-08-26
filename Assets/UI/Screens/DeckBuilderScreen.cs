using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 卡组构筑界面（Phase 1 竖切片）—— 演示完整的数据↔UI 双向绑定与 JSON 存档：
    ///   数据→UI：CardCatalog 读全部卡，渲染到左侧总表。
    ///   UI→数据：点卡加入/移除右侧卡组，顶部实时汇总卡数与费用。
    ///   存档：DeckSerializer 存/读 Assets/Configs/Decks/*.json。
    /// </summary>
    public sealed class DeckBuilderScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/DeckBuilder";

        // 当前正在构筑的卡组（卡牌 ID 列表）。
        private readonly List<string> _deckCardIds = new List<string>();

        private ScrollView _catalogList;
        private ScrollView _deckList;
        private Label _summary;
        private Label _toast;
        private TextField _nameField;
        private DropdownField _decksDropdown;

        public override void OnEnter()
        {
            _catalogList = Q<ScrollView>("list-catalog");
            _deckList = Q<ScrollView>("list-deck");
            _summary = Q<Label>("lbl-summary");
            _toast = Q<Label>("lbl-toast");
            _nameField = Q<TextField>("field-deck-name");
            _decksDropdown = Q<DropdownField>("dropdown-decks");

            UIBinder.BindButton(Root, "btn-back", () => Manager.Back());
            UIBinder.BindButton(Root, "btn-save", OnSave);
            UIBinder.BindButton(Root, "btn-load", OnLoad);

            BuildCatalog();
            RefreshDeckList();
            RefreshDecksDropdown();
        }

        // ---------- 数据→UI：渲染卡牌总表 ----------
        private void BuildCatalog()
        {
            _catalogList.Clear();
            foreach (var card in CardCatalog.LoadAll())
            {
                var captured = card;
                _catalogList.Add(MakeCardRow(captured, onClick: () => AddCard(captured.ID)));
            }
        }

        // 构造一行卡牌视图：费用徽标 + 名称 + 类型。
        private VisualElement MakeCardRow(CardData card, System.Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            var cost = new Label(((int)card.TotalCost).ToString());
            cost.AddToClassList("cost-badge");
            row.Add(cost);

            var name = new Label(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName);
            name.AddToClassList("list-row__name");
            row.Add(name);

            var meta = new Label(card.Supertype.ToString());
            meta.AddToClassList("list-row__meta");
            row.Add(meta);

            row.RegisterCallback<ClickEvent>(_ => onClick());
            return row;
        }

        // ---------- UI→数据：卡组增删 ----------
        private void AddCard(string id)
        {
            _deckCardIds.Add(id);
            RefreshDeckList();
        }

        private void RemoveCardAt(int index)
        {
            if (index >= 0 && index < _deckCardIds.Count)
            {
                _deckCardIds.RemoveAt(index);
                RefreshDeckList();
            }
        }

        // ---------- 数据→UI：渲染当前卡组 ----------
        private void RefreshDeckList()
        {
            _deckList.Clear();
            for (int i = 0; i < _deckCardIds.Count; i++)
            {
                var index = i;
                var card = CardCatalog.GetById(_deckCardIds[i]);
                if (card == null)
                {
                    continue;
                }
                _deckList.Add(MakeCardRow(card, onClick: () => RemoveCardAt(index)));
            }
            UpdateSummary();
        }

        // 顶部实时汇总：卡数 + 合计费用。
        private void UpdateSummary()
        {
            float totalCost = 0f;
            foreach (var id in _deckCardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card != null)
                {
                    totalCost += card.TotalCost;
                }
            }
            _summary.text = $"卡数 {_deckCardIds.Count} · 合计费用 {(int)totalCost}";
        }

        // ---------- 存档：保存当前卡组 ----------
        private void OnSave()
        {
            var name = string.IsNullOrWhiteSpace(_nameField.value) ? "新卡组" : _nameField.value.Trim();
            var deck = new DeckData(name) { cardIds = new List<string>(_deckCardIds) };
            var path = DeckSerializer.Save(deck);
            ShowToast(path == null ? "保存失败" : $"已保存：{name}");
            RefreshDecksDropdown();
            if (_decksDropdown.choices.Contains(name))
            {
                _decksDropdown.SetValueWithoutNotify(name);
            }
        }

        // ---------- 读档：载入下拉选中的卡组 ----------
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
            RefreshDeckList();
            ShowToast($"已载入：{deck.name}");
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
