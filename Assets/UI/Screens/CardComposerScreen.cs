using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌合成界面（Phase 2-3 精修）—— 业务 3 类型（生物/法术/结界）+ 按类型表单，
    /// 从效果库挂载效果、实时算费（可手改，非效果杂费记灰色），存为 CardLoader 能读回的卡牌 JSON。
    ///
    /// 业务类型 → 后端：生物/法术/结界=纯 Supertype。子类型/等级做到存盘 + 读回 + UI 还原。
    /// </summary>
    public sealed class CardComposerScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/CardComposer";

        // 业务类型（UI 维度，非裸 Cardtype）。
        private enum CardKind { Creature, Spell, Enchantment }

        private static readonly (CardKind kind, string name)[] KindNames =
        {
            (CardKind.Creature, "生物"), (CardKind.Spell, "法术"), (CardKind.Enchantment, "结界"),
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
        private Dictionary<int, float> _lastSuggestedCost;

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
            UIBinder.BindButton(Root, "btn-new-effect", () =>
            {
                ComposerSession.BeginCardEdit(_card, -1); // -1 = 新建（保存时追加）
                Manager.Show<EffectComposerScreen>();
            });

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
            _card.Subtype = CardSubtype.None;
            _card.Level = null;

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
            }
        }

        private bool HasStats => _kind == CardKind.Creature;
        private bool HasLevel => _kind == CardKind.Creature;

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
                DurationValue = src.DurationValue,
                SummonDropZone = src.SummonDropZone,
                SelectionMode = src.SelectionMode,
                TargetCount = src.TargetCount,
                TriggerLimitPerTurn = src.TriggerLimitPerTurn,
                DynamicTargetCount = src.DynamicTargetCount,
                Drawbacks = src.Drawbacks,
                EngineKind = src.EngineKind,
                EngineParam = src.EngineParam,
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

        // ---------- 自动算费 ----------
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

            // 规则一（2026-09-11 简化）：D≤C 直判；错边原子出计价转黑白获得（结算时发放）
            var balance = result.OffsetRequirement == 0
                ? "符合规则一"
                : $"超模（D 超 C {result.OffsetRequirement}）";
            var grantStr = result.Grants.Count > 0
                ? "｜获得 " + string.Join(" ", result.Grants.Select(kv => $"{(ManaType)kv.Key} {(int)kv.Value}"))
                : "";
            _suggested.text = $"建议档位 {result.ManaCost}（D={result.Total:0}）｜{balance}{grantStr}";
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

        // ---------- 保存 ----------
        private void OnSave()
        {
            ApplyTextLists();

            // 构筑期规则一校验（提示级，不阻止保存）：D > C → 警告（2026-09-11 简化口径）
            string warn = null;
            var check = CardCostService.Derive(_card);
            if (check.DeclaredTier > 0 && !check.Conformant)
            {
                warn = $"超模：D={check.DerivedTotal} > C={check.DeclaredTier}";
                UnityEngine.Debug.LogWarning($"[CardCost] {_card.CardName} {warn}，不符规则一");
            }

            var path = CardConfigSerializer.Save(_card);
            if (path == null)
                ShowToast("保存失败");
            else
                ShowToast(warn != null
                    ? $"已保存（警告：{warn}）：{System.IO.Path.GetFileName(path)}"
                    : $"已保存到卡表：{System.IO.Path.GetFileName(path)}");
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
