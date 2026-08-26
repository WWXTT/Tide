using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌合成界面（Phase 2-3 精修）—— 业务 7 类型（生物/法术/结界/融合/同步/超量/链接）+ 按类型表单，
    /// 从效果库挂载效果、实时算费（可手改，非效果杂费记灰色），存为 CardLoader 能读回的卡牌 JSON。
    ///
    /// 业务类型 → 后端：生物/法术/结界=纯 Supertype；融合/同步/超量/链接=Creature + CardSubtype Flags。
    ///   超量无固定攻血/费用（由素材推导，UI 标注）；链接显示箭头网格；同步可勾 Tuner。
    /// 子类型/等级/阶级/链接值/箭头本阶段做到存盘 + 读回 + UI 还原；特殊召唤执行留 Phase 4。
    /// </summary>
    public sealed class CardComposerScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/CardComposer";

        // 业务类型（UI 维度，非裸 Cardtype）。
        private enum CardKind { Creature, Spell, Enchantment, Fusion, Synchro, Xyz, Link }

        private static readonly (CardKind kind, string name)[] KindNames =
        {
            (CardKind.Creature, "生物"), (CardKind.Spell, "法术"), (CardKind.Enchantment, "结界"),
            (CardKind.Fusion, "融合"), (CardKind.Synchro, "同步"), (CardKind.Xyz, "超量"),
            (CardKind.Link, "链接"),
        };

        private readonly CardData _card = new CardData
        {
            CardName = "新卡牌",
            Supertype = Cardtype.Creature,
            Power = 0,
            Life = 0,
        };

        private CardKind _kind = CardKind.Creature;

        private ScrollView _libraryList;
        private ScrollView _attachedList;
        private ScrollView _breakdownList;
        private VisualElement _dynamicForm;
        private Label _toast;
        private Label _suggested;
        private Label _costLabel;
        private TextField _nameField;
        private TextField _tagsField;
        private TextField _keywordsField;
        private DropdownField _cardtypeDropdown;
        private DropdownField _manaDropdown;

        private List<ManaType> _manaTypes;
        private int _lastSuggestedMana;

        public override void OnEnter()
        {
            _libraryList = Q<ScrollView>("list-library");
            _attachedList = Q<ScrollView>("list-attached");
            _breakdownList = Q<ScrollView>("list-breakdown");
            _dynamicForm = Q<VisualElement>("dynamic-form");
            _toast = Q<Label>("lbl-toast");
            _suggested = Q<Label>("lbl-suggested");
            _costLabel = Q<Label>("lbl-cost");
            _nameField = Q<TextField>("field-card-name");
            _tagsField = Q<TextField>("field-tags");
            _keywordsField = Q<TextField>("field-keywords");
            _cardtypeDropdown = Q<DropdownField>("dropdown-cardtype");
            _manaDropdown = Q<DropdownField>("dropdown-mana");

            UIBinder.BindButton(Root, "btn-back", () => Manager.Back());
            UIBinder.BindButton(Root, "btn-save", OnSave);
            UIBinder.BindButton(Root, "btn-adopt", OnAdoptCost);

            BindForm();
            BuildCardtypeDropdown();
            BuildManaDropdown();
            BuildDynamicForm();
            BuildLibrary();
            RefreshAttached();
            Recalculate();
        }

        private void BindForm()
        {
            UIBinder.BindField(_nameField, _card.CardName ?? "", v => { _card.CardName = v; });
            UIBinder.BindField(_tagsField, "", _ => { });
            UIBinder.BindField(_keywordsField, "", _ => { });
        }

        private void BuildCardtypeDropdown()
        {
            _cardtypeDropdown.choices = KindNames.Select(k => k.name).ToList();
            _cardtypeDropdown.index = 0;
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

        private void BuildManaDropdown()
        {
            _manaTypes = Enum.GetValues(typeof(ManaType)).Cast<ManaType>().ToList();
            _manaDropdown.choices = _manaTypes.Select(t => t.ToString()).ToList();
            _manaDropdown.index = 0;
        }

        // ---------- 业务类型 → 后端字段 ----------
        // 设置 Supertype + Subtype；清掉与新类型无关的额外字段（保持模型干净）。
        private void ApplyKindToCard()
        {
            // 先清额外卡组字段，再按类型补。
            _card.Subtype = CardSubtype.None;
            _card.Level = null;
            _card.Rank = null;
            _card.LinkRating = null;
            _card.ArrowDirections = CardCore.HexDirection.None;

            switch (_kind)
            {
                case CardKind.Creature:
                    _card.Supertype = Cardtype.Creature;
                    break;
                case CardKind.Spell:
                    _card.Supertype = Cardtype.Spell;
                    _card.Power = null;
                    _card.Life = null;
                    break;
                case CardKind.Enchantment:
                    _card.Supertype = Cardtype.Enchantment;
                    _card.Power = null;
                    _card.Life = null;
                    break;
                case CardKind.Fusion:
                    _card.Supertype = Cardtype.Creature;
                    _card.Subtype = CardSubtype.Fusion;
                    break;
                case CardKind.Synchro:
                    _card.Supertype = Cardtype.Creature;
                    _card.Subtype = CardSubtype.Synchro;
                    break;
                case CardKind.Xyz:
                    _card.Supertype = Cardtype.Creature;
                    _card.Subtype = CardSubtype.Xyz;
                    // 超量无固定攻血/费用：清空，标注由素材推导。
                    _card.Power = null;
                    _card.Life = null;
                    _card.Cost?.Clear();
                    break;
                case CardKind.Link:
                    _card.Supertype = Cardtype.Creature;
                    _card.Subtype = CardSubtype.Link;
                    _card.Life = null; // 链接怪无防御，只有攻击力。
                    break;
            }
        }

        private bool HasStats => _kind == CardKind.Creature || _kind == CardKind.Fusion
            || _kind == CardKind.Synchro;
        private bool HasLevel => _kind == CardKind.Creature || _kind == CardKind.Fusion
            || _kind == CardKind.Synchro;

        // ---------- 动态表单（按类型切换字段） ----------
        private void BuildDynamicForm()
        {
            _dynamicForm.Clear();

            if (_kind == CardKind.Xyz)
            {
                var note = new Label("超量：攻血/费用由素材推导（本阶段不输入）");
                note.AddToClassList("hint");
                _dynamicForm.Add(note);
            }

            // 攻 / 血
            if (HasStats || _kind == CardKind.Link)
            {
                var statRow = new VisualElement();
                statRow.AddToClassList("toolbar");
                statRow.Add(MakeLabeledInt("攻击", _card.Power ?? 0, v => { _card.Power = v; Recalculate(); }));
                if (_kind != CardKind.Link) // 链接怪无防御。
                {
                    statRow.Add(MakeLabeledInt("生命", _card.Life ?? 0, v => { _card.Life = v; Recalculate(); }));
                }
                _dynamicForm.Add(statRow);
            }

            // 等级 / 阶级 / 链接值
            var lvlRow = new VisualElement();
            lvlRow.AddToClassList("toolbar");
            if (HasLevel)
            {
                lvlRow.Add(MakeLabeledInt("等级", _card.Level ?? 1, v => _card.Level = v));
            }
            if (_kind == CardKind.Xyz)
            {
                lvlRow.Add(MakeLabeledInt("阶级", _card.Rank ?? 1, v => _card.Rank = v));
            }
            if (_kind == CardKind.Link)
            {
                lvlRow.Add(MakeLabeledInt("链接值", _card.LinkRating ?? 1, v => _card.LinkRating = v));
            }
            if (lvlRow.childCount > 0)
            {
                _dynamicForm.Add(lvlRow);
            }

            // Tuner（仅同步）
            if (_kind == CardKind.Synchro)
            {
                var tuner = new Toggle("调谐怪（Tuner）");
                tuner.SetValueWithoutNotify((_card.Subtype & CardSubtype.Tuner) != 0);
                tuner.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        _card.Subtype |= CardSubtype.Tuner;
                    }
                    else
                    {
                        _card.Subtype &= ~CardSubtype.Tuner;
                    }
                });
                _dynamicForm.Add(tuner);
            }

            // 箭头网格（仅链接）
            if (_kind == CardKind.Link)
            {
                var arrowHeader = new Label("链接箭头");
                arrowHeader.AddToClassList("panel__header");
                _dynamicForm.Add(arrowHeader);
                _dynamicForm.Add(MakeArrowGrid());
            }
        }

        // 链接箭头 6 向网格（对应 CardCore.HexDirection 的 6 个方向，多选合成 Flags）。
        private static readonly (CardCore.HexDirection dir, string label)[] ArrowLayout =
        {
            (CardCore.HexDirection.UpperLeft, "↖"), (CardCore.HexDirection.Up, "↑"), (CardCore.HexDirection.UpperRight, "↗"),
            (CardCore.HexDirection.LowerLeft, "↙"), (CardCore.HexDirection.Down, "↓"), (CardCore.HexDirection.LowerRight, "↘"),
        };

        private VisualElement MakeArrowGrid()
        {
            var grid = new VisualElement();
            grid.AddToClassList("arrow-grid");

            foreach (var (dir, label) in ArrowLayout)
            {
                var captured = dir;
                var cell = new Button { text = label };
                cell.AddToClassList("arrow-cell");
                if ((_card.ArrowDirections & captured) != 0)
                {
                    cell.AddToClassList("arrow-cell--on");
                }
                cell.clicked += () =>
                {
                    if ((_card.ArrowDirections & captured) != 0)
                    {
                        _card.ArrowDirections &= ~captured;
                        cell.RemoveFromClassList("arrow-cell--on");
                    }
                    else
                    {
                        _card.ArrowDirections |= captured;
                        cell.AddToClassList("arrow-cell--on");
                    }
                };
                grid.Add(cell);
            }
            return grid;
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

        // ---------- 右：效果库 ----------
        private void BuildLibrary()
        {
            _libraryList.Clear();
            foreach (var graph in EffectLibrarySerializer.LoadAll())
            {
                var captured = graph;
                _libraryList.Add(MakeNameRow(
                    string.IsNullOrEmpty(captured.name) ? "(未命名)" : captured.name,
                    "挂载",
                    () => AttachEffect(captured)));
            }
        }

        private void AttachEffect(EffectGraphData graph)
        {
            _card.Effects.Add(SnapshotEffect(graph));
            RefreshAttached();
            Recalculate();
        }

        // 效果图 → 卡内嵌效果：header 复制 + Steps 原样保留 + then 分支线性投影到 AtomicEffects。
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
                ActivationConditions = src.ActivationConditions,
                TriggerConditions = src.TriggerConditions,
                Costs = src.Costs,
                Tags = src.Tags,
                Steps = graph.steps != null ? new List<EffectStepData>(graph.steps) : new List<EffectStepData>(),
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
            _attachedList.Clear();
            var effects = _card.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                var index = i;
                var label = string.IsNullOrEmpty(effects[i].DisplayName) ? $"效果 #{i + 1}" : effects[i].DisplayName;
                _attachedList.Add(MakeNameRow(label, "移除", () => RemoveAttachedAt(index)));
            }
        }

        private void RemoveAttachedAt(int index)
        {
            if (index >= 0 && index < _card.Effects.Count)
            {
                _card.Effects.RemoveAt(index);
                RefreshAttached();
                Recalculate();
            }
        }

        private VisualElement MakeNameRow(string name, string action, Action onAction)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            var label = new Label(name);
            label.AddToClassList("list-row__name");
            row.Add(label);

            var btn = new Button(onAction) { text = action };
            btn.AddToClassList("btn");
            btn.AddToClassList("btn--mini");
            row.Add(btn);
            return row;
        }

        // ---------- 自动算费 ----------
        private void Recalculate()
        {
            var result = CardCostCalculator.Calculate(_card);
            _lastSuggestedMana = result.ManaCost;

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

            if (_kind == CardKind.Xyz)
            {
                _suggested.text = "超量：费用由素材决定";
            }
            else
            {
                _suggested.text = $"建议费用 {result.ManaCost}（合计 {result.Total:0.0}）";
            }
            RefreshCostLabel();
        }

        // 采纳：把建议费用落到所选主色（超量不输入费用）。
        private void OnAdoptCost()
        {
            if (_kind == CardKind.Xyz)
            {
                ShowToast("超量卡费用由素材决定，无需采纳");
                return;
            }
            int manaIdx = _manaDropdown.index;
            if (manaIdx < 0 || manaIdx >= _manaTypes.Count)
            {
                manaIdx = 0;
            }
            int manaKey = (int)_manaTypes[manaIdx];
            _card.Cost[manaKey] = _lastSuggestedMana;
            RefreshCostLabel();
            ShowToast($"已采纳：{_manaTypes[manaIdx]} {_lastSuggestedMana}");
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

        // ---------- 保存 ----------
        private void OnSave()
        {
            ApplyTextLists();
            var path = CardConfigSerializer.Save(_card);
            ShowToast(path == null ? "保存失败" : $"已保存到卡表：{System.IO.Path.GetFileName(path)}");
        }

        private void ApplyTextLists()
        {
            _card.CardName = string.IsNullOrWhiteSpace(_nameField.value) ? "新卡牌" : _nameField.value.Trim();
            _card.Tags = SplitCsv(_tagsField.value);
            _card.Keywords = SplitCsv(_keywordsField.value);
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
