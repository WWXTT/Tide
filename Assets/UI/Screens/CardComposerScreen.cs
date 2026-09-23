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
        private VisualElement _payloadZone;
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
            _payloadZone = Q<VisualElement>("payload-zone");
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
            BuildPayloadZone(); // 代价栏（2026-09-23 上移卡组合层）
            BuildLibrary();
            // 光环上移效果层（2026-09-23）：从效果合成器返回/重进时按挂载效果重算卡面箭头+光环
            _card.AggregateEffectAuras(true);
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

            // 未填装：候选下拉（与 FillPayloadCost 同口径的表级过滤——错侧可达·默认 1 费）
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
