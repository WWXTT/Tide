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
    /// 效果合成界面（2026-09-14 合成器重做；当日二轮：顶栏重构 + 右栏双模式 + 内联编辑）——
    /// 左=编辑栏 / 右=展示区（原子库 / 效果表双模式），拖拽组装。
    ///
    /// 组合三态（用户定案，InferMode 推断防互串）：
    ///   并列     = steps 里 1-3 个 kind=0 原子（无时序选项——全部按现行逐一结算，引擎零改动）；
    ///   自由分支 = steps 空 + header.EngineKind/EngineParam（主干=拼点/运势/倒计时表行）+ AtomicEffects[0]=奖励；
    ///   有限分支 = steps=[kind=0 主干, kind=1 分支（conditionId ∈ OutcomeGates，thenSteps=奖励单原子）]，
    ///              门文本带【奖励x】，奖励推导费 ≤ x（drop 时校验）。
    ///
    /// 顶栏（二轮定案）：
    ///   - 效果名自动构成：中文名＋组合方式＋中文名（并列"＋"/分支"→"），只读展示；
    ///   - 触发方式三态：主动（仅自己主阶段，隐藏触发时机）/ 自动（条件触发·弹窗询问）/ 强制（条件触发·不询问）；
    ///     触发时机下拉全中文；
    ///   - 读取/载入下拉删除；右上两按钮：读取原子表（AtomicEffectTable.Reload+回原子库）/ 读取效果表
    ///     （右栏切效果表模式——点击效果载入左侧编辑，保存覆盖原名，自动改名清旧档）；
    ///   - toast 移至左下角。
    ///
    /// 检查器已删（用户裁定）：原子参数编辑改为**槽内卡片点击展开**（Value/随机滑条/目标域/目标随机/
    /// MountKinds 提示全部内联）；效果级编排字段在"效果设置"区（每个选项带说明）。
    ///
    /// 三轮追加（同日）：代价=**错边原子槽**（效果区禁错边/代价区只能错边，位于效果编辑上方）；
    /// 自动名旁显示自动费用（ConvertOne+DeriveElementCosts 实时推导，含引擎机制费）；
    /// 存储统一 StreamingAssets/Tide（CreatureCards=卡池+合成保存同文件；效果库 Effects/；卡组只存 ID 引用）。
    /// 旧版分支/缺陷/维度档走 BranchConfigTable 的死路径（BranchConfig.json 已删）全部移除——
    /// 有限分支条件目录用代码侧 ComposerCatalog；Drawbacks 降级为效果级 CSV 文本行。
    /// </summary>
    public sealed class EffectComposerScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/EffectComposer";

        // ======================================== 组合形态 ========================================

        /// <summary>组合三态（UI/存读共用推断，防三种形态互串）。</summary>
        public enum ComposeMode { Parallel, FreeBranch, OutcomeGate }

        /// <summary>右栏双模式：原子库（拖拽源）/ 效果表（点击载入编辑）。</summary>
        private enum RightMode { Atoms, Effects }

        /// <summary>由数据推断组合形态：引擎通道优先 → 含 kind=1 步骤=有限分支 → 其余并列。</summary>
        public static ComposeMode InferMode(EffectGraphData g)
        {
            if (g?.header != null && g.header.EngineKind != (int)BranchEngineKind.None) return ComposeMode.FreeBranch;
            if (g?.steps != null && g.steps.Any(s => s?.kind == 1)) return ComposeMode.OutcomeGate;
            return ComposeMode.Parallel;
        }

        // ---- 状态 ----
        private EffectGraphData _graph = new EffectGraphData("新效果");
        private ComposeMode _mode = ComposeMode.Parallel;
        private RightMode _rightMode = RightMode.Atoms;

        // 槽内卡片展开状态（并列下标存 _selIndex；FreeTrunk/Cost 无下标）
        private enum SelKind { None, Parallel, GateTrunk, GateReward, FreeTrunk, FreeReward, Cost }
        private SelKind _sel = SelKind.None;
        private int _selIndex = -1;

        private readonly DragController _drag = new DragController();

        // 卡编辑会话（null=效果库模式——保存到效果库文件）
        private CardData _editingCard;
        private int _editingIndex = -1;

        // 从效果表载入的源效果 id（编辑换内容=换 id——保存后清旧档防重复；null=非效果表载入）
        private string _loadedFromLibraryId;

        // ---- UI 引用 ----
        private ScrollView _slotArea, _effectSettings, _libraryList, _keywordList, _effectsList;
        private VisualElement _modeBar, _atomPanel, _costZone;
        private Label _toast, _nameLabel, _costLabel, _activationHint, _timingLabel;
        // RefreshSlots 重建的槽元素登记（重建时逐个反注册落区——防 ClearZones 误清代价区固定落区）
        private readonly List<VisualElement> _slotEls = new List<VisualElement>();
        private TextField _filterName;
        private DropdownField _timingDropdown, _activationDropdown, _filterTypeDropdown;
        private Button _reloadAtomsBtn, _loadEffectsBtn;
        private List<TriggerTiming> _timings;

        // 筛选状态（效果分类=中文名下拉；近似搜索=中文名∪模板两列并集。颜色筛选已删——圆点直读表色）
        private string _filterTypeZh; // null = 全部

        // 发动方式：0=强制 1=自动 2=主动（EffectActivationType 同序）。
        private static readonly string[] ActivationNames = { "强制", "自动", "主动" };
        private static readonly string[] ActivationHints =
        {
            "强制：条件达成时不询问，直接发动",
            "自动：条件达成时自行触发（弹窗询问是否发动）",
            "主动：只能在自己回合的主要阶段主动发动——不设触发时机",
        };

        // 触发时机中文（多数取自 EffectDefinition 描述映射，登/召唤/他生物进场为补全；主动三档不入下拉）
        private static readonly Dictionary<TriggerTiming, string> TimingZh = new Dictionary<TriggerTiming, string>
        {
            { TriggerTiming.OnPlay, "登场时" },
            { TriggerTiming.OnDeath, "死亡时" },
            { TriggerTiming.OnDestroy, "破坏时" },
            { TriggerTiming.OnExile, "除外时" },
            { TriggerTiming.OnReturnFromGraveyard, "从墓地回到战场时" },
            { TriggerTiming.OnLeaveBattlefield, "离场时" },
            { TriggerTiming.OnDraw, "抽牌时" },
            { TriggerTiming.OnDealDamage, "造成伤害时" },
            { TriggerTiming.OnTakeDamage, "受到伤害时" },
            { TriggerTiming.OnTurnStart, "回合开始时" },
            { TriggerTiming.OnTurnEnd, "回合结束时" },
            { TriggerTiming.OnPhaseStart, "阶段开始时" },
            { TriggerTiming.OnPhaseEnd, "阶段结束时" },
            { TriggerTiming.OnAttack, "攻击宣言时" },
            { TriggerTiming.OnAttacked, "被攻击时" },
            { TriggerTiming.OnBlockDeclare, "阻拦宣言时" },
            { TriggerTiming.OnCardPlayed, "使用卡牌时" },
            { TriggerTiming.OnSpellCast, "施放法术时" },
            { TriggerTiming.OnTap, "横置时" },
            { TriggerTiming.OnUntap, "重置时" },
            { TriggerTiming.OnTargeted, "被指定为目标时" },
            { TriggerTiming.OnSummon, "召唤进场时" },
            { TriggerTiming.OnOtherCreatureEnter, "其他生物进场时" },
            { TriggerTiming.OnGameStart, "游戏开始时" },
            { TriggerTiming.OnAtomicEffectActivation, "原子效果发动时" },
            { TriggerTiming.OnAtomicEffectStartApplying, "原子效果开始作用时" },
            { TriggerTiming.OnAtomicEffectResolution, "原子效果结算完成时" },
        };

        private static readonly (string label, int value)[] SelectionModes =
        {
            ("无目标", (int)CardCore.SelectionMode.None),
            ("自身", (int)CardCore.SelectionMode.Self),
            ("手动", (int)CardCore.SelectionMode.Manual),
            ("全域", (int)CardCore.SelectionMode.Full),
            ("随机", (int)CardCore.SelectionMode.Random),
        };

        // ======================================== 生命周期 ========================================

        public override void OnEnter()
        {
            try
            {
                OnEnterInternal();
            }
            catch (Exception e)
            {
                // OnEnter 中途异常会让槽位/原子库静默空白——打日志+toast 可见化
                UnityEngine.Debug.LogException(e);
                _toast ??= Q<Label>("lbl-toast");
                if (_toast != null) _toast.text = "界面初始化异常：" + e.Message;
            }
        }

        private void OnEnterInternal()
        {
            _slotArea = Q<ScrollView>("slot-area");
            _effectSettings = Q<ScrollView>("effect-settings");
            _libraryList = Q<ScrollView>("library-list");
            _keywordList = Q<ScrollView>("keyword-list");
            _effectsList = Q<ScrollView>("list-effects");
            _modeBar = Q<VisualElement>("mode-bar");
            _atomPanel = Q<VisualElement>("atom-panel");
            _costZone = Q<VisualElement>("cost-zone");
            _toast = Q<Label>("lbl-toast");
            _nameLabel = Q<Label>("lbl-effect-name");
            _costLabel = Q<Label>("lbl-effect-cost");
            _activationHint = Q<Label>("lbl-activation-hint");
            _timingLabel = Q<Label>("lbl-timing");
            _filterName = Q<TextField>("field-filter-name");
            _timingDropdown = Q<DropdownField>("dropdown-timing");
            _activationDropdown = Q<DropdownField>("dropdown-activation");
            _filterTypeDropdown = Q<DropdownField>("dropdown-filter-type");
            _reloadAtomsBtn = Q<Button>("btn-reload-atoms");
            _loadEffectsBtn = Q<Button>("btn-load-effects");

            UIBinder.BindButton(Root, "btn-back", () => Manager.Back());
            UIBinder.BindButton(Root, "btn-save", OnSave);
            UIBinder.BindButton(Root, "btn-reload-atoms", OnReloadAtoms);
            UIBinder.BindButton(Root, "btn-load-effects", OnLoadEffectsTable);

            // 卡编辑会话（一次性消费）：进入即改写 _graph 为该效果的深拷贝
            var (card, index) = ComposerSession.Take();
            if (card != null)
            {
                _editingCard = card;
                _editingIndex = index;
                _graph = CardEffectToGraph(index >= 0 && index < card.Effects?.Count ? card.Effects[index] : null);
                _loadedFromLibraryId = null;
                ShowToast(index >= 0 ? $"编辑效果 #{index + 1}（保存写回卡牌）" : "新建效果（保存追加到卡牌）");
            }

            BuildTimingDropdown();
            BuildActivationDropdown();
            BuildFilterBar();
            RegisterCostZone();
            _mode = InferMode(_graph);
            RefreshAll();
            SetRightMode(RightMode.Atoms);

            // 空库自诊断：正常应 ≥87 行——为空说明表未装载或初始化半途出错（查 Console）
            int libCount = _libraryList?.contentContainer?.childCount ?? 0;
            if (libCount == 0)
                ShowToast($"原子库为空（表加载失败或初始化异常——查 Console；分类项 {_filterTypeDropdown.choices?.Count ?? 0}）");
        }

        // ======================================== 元信息（顶栏） ========================================

        private void BuildTimingDropdown()
        {
            // 全中文时机下拉（主动三档 Activate_* 不入——主动不设时机）
            _timings = TimingZh.Keys.ToList();
            var labels = _timings.Select(t => TimingZh[t]).ToList();
            _timingDropdown.choices = labels;
            SyncTimingDropdown();
            _timingDropdown.RegisterValueChangedCallback(_ =>
            {
                int idx = _timingDropdown.index;
                if (idx >= 0 && idx < _timings.Count) _graph.header.TriggerTiming = (int)_timings[idx];
            });
        }

        private void SyncTimingDropdown()
        {
            int current = _timings.IndexOf((TriggerTiming)_graph.header.TriggerTiming);
            _timingDropdown.index = current < 0 ? 0 : current;
        }

        private void BuildActivationDropdown()
        {
            _activationDropdown.choices = ActivationNames.ToList();
            int act = _graph.header.ActivationType;
            _activationDropdown.index = act >= 0 && act < ActivationNames.Length ? act : 0;
            SyncActivationVisibility();
            _activationDropdown.RegisterValueChangedCallback(_ =>
            {
                _graph.header.ActivationType = _activationDropdown.index;
                SyncActivationVisibility();
            });
        }

        /// <summary>主动（=2）只在自己主阶段发动——隐藏触发时机；其余显示时机 + 语义提示。</summary>
        private void SyncActivationVisibility()
        {
            bool voluntary = _graph.header.ActivationType == 2;
            var display = voluntary ? DisplayStyle.None : DisplayStyle.Flex;
            _timingDropdown.style.display = display;
            if (_timingLabel != null) _timingLabel.style.display = display;
            int idx = Mathf.Clamp(_graph.header.ActivationType, 0, ActivationHints.Length - 1);
            _activationHint.text = ActivationHints[idx];
        }

        // ======================================== 效果名自动构成 ========================================

        /// <summary>自动名 = 中文名＋组合方式＋中文名（并列"＋"、两分支"→"；空槽以"…"占位）。</summary>
        private string AutoName()
        {
            var h = _graph.header;
            if (_mode == ComposeMode.FreeBranch)
            {
                string trunk = h.EngineKind != (int)BranchEngineKind.None
                    ? TrunkZh((BranchEngineKind)h.EngineKind) : "…";
                return $"{trunk}→{RewardZh(h.AtomicEffects?.FirstOrDefault())}";
            }
            if (_mode == ComposeMode.OutcomeGate && _graph.steps.Count >= 2)
            {
                return $"{AtomZh(_graph.steps[0].atomic)}→{RewardZh(_graph.steps[1].thenSteps?.FirstOrDefault())}";
            }
            var parts = new List<string>();
            foreach (var s in _graph.steps)
                if (s?.kind == 0 && s.atomic != null) parts.Add(AtomZh(s.atomic));
            return parts.Count == 0 ? "（空）" : string.Join("＋", parts);
        }

        private static string AtomZh(AtomicEffectEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.refId)) return "…";
            return AtomicEffectTable.GetByHashId(entry.refId)?.DisplayName ?? entry.refId;
        }

        private static string RewardZh(AtomicEffectEntry entry) => entry == null ? "…" : AtomZh(entry);

        private static string TrunkZh(BranchEngineKind engine)
            => AtomicEffectTable.GetByEnumName("BranchEngine" + engine)?.DisplayName ?? engine.ToString();

        private void RefreshName()
        {
            _nameLabel.text = AutoName();
            _costLabel.text = "费用：" + AutoCostText();
        }

        /// <summary>自动费用预览（ConvertOne + DeriveElementCosts 实时推导——含引擎机制费与门附加费；
        /// 编辑中间态转换失败显示 —）。</summary>
        private string AutoCostText()
        {
            try
            {
                var def = CardEffectConverter.ConvertOne(BuildCardEffect(), "COMPOSER_COST_PREVIEW");
                if (def == null) return "—";
                var costs = CostDerivationService.DeriveElementCosts(def, 0);
                if (costs == null || costs.Count == 0) return "—";
                var parts = new List<string>();
                foreach (var c in costs)
                {
                    if (c.Value <= 0) continue;
                    parts.Add($"{ColorZh(c.ManaType)}{(int)c.Value}");
                }
                return parts.Count == 0 ? "—" : string.Join(" ", parts);
            }
            catch
            {
                return "—";
            }
        }

        private static string ColorZh(ManaType m) => m switch
        {
            ManaType.Red => "红",
            ManaType.Blue => "蓝",
            ManaType.Green => "绿",
            ManaType.Gray => "灰",
            ManaType.Black => "黑",
            ManaType.White => "白",
            _ => m.ToString(),
        };

        // ======================================== 整体刷新 ========================================

        private void RefreshAll()
        {
            RefreshModeBar();
            RefreshSlots();
            RefreshCostZone();
            RefreshEffectSettings();
            RefreshLibrary();
            RefreshKeywords();
            RefreshName();
        }

        private void RefreshModeBar()
        {
            _modeBar.Clear();
            _modeBar.Add(MakeModeChip("并列（1-3 原子）", ComposeMode.Parallel));
            _modeBar.Add(MakeModeChip("自由分支（引擎条件）", ComposeMode.FreeBranch));
            _modeBar.Add(MakeModeChip("有限分支（产出条件）", ComposeMode.OutcomeGate));

            // 卡编辑模式提示
            if (_editingCard != null)
            {
                var tag = new Label($"正在编辑：{_editingCard.CardName} #{_editingIndex + 1}");
                tag.AddToClassList("hint");
                _modeBar.Add(tag);
            }
        }

        private VisualElement MakeModeChip(string label, ComposeMode mode)
        {
            var chip = new Button(() => SwitchMode(mode)) { text = label };
            chip.AddToClassList("mode-chip");
            if (mode == _mode) chip.AddToClassList("mode-chip--active");
            return chip;
        }

        /// <summary>形态切换：best-effort 数据搬运（原子尽量保留），切换后收起展开卡。</summary>
        private void SwitchMode(ComposeMode newMode)
        {
            if (newMode == _mode) return;
            var h = _graph.header;
            _graph.steps ??= new List<EffectStepData>();

            switch (newMode)
            {
                case ComposeMode.Parallel:
                {
                    AtomicEffectEntry seed = h.AtomicEffects?.FirstOrDefault();
                    if (seed == null) seed = FirstKind0Atom();
                    h.EngineKind = (int)BranchEngineKind.None;
                    h.EngineParam = 0;
                    h.AtomicEffects = null;
                    _graph.steps.Clear();
                    if (seed != null) _graph.steps.Add(new EffectStepData { kind = 0, atomic = seed });
                    ShowToast("切换为并列（原子已尽量保留）");
                    break;
                }
                case ComposeMode.FreeBranch:
                {
                    AtomicEffectEntry seed = FirstKind0Atom();
                    _graph.steps.Clear();
                    h.EngineKind = (int)BranchEngineKind.None; // 待拖主干
                    h.EngineParam = 0;
                    h.AtomicEffects = seed != null ? new List<AtomicEffectEntry> { seed } : new List<AtomicEffectEntry>();
                    ShowToast("切换为自由分支——请拖入主干（拼点/运势/倒计时）与奖励");
                    break;
                }
                case ComposeMode.OutcomeGate:
                {
                    var atoms = new List<AtomicEffectEntry>();
                    if (h.AtomicEffects != null) atoms.AddRange(h.AtomicEffects);
                    foreach (var s in _graph.steps)
                    {
                        if (s?.kind == 0 && s.atomic != null) atoms.Add(s.atomic);
                        else if (s?.kind == 1 && s.thenSteps != null) atoms.AddRange(s.thenSteps);
                    }
                    h.EngineKind = (int)BranchEngineKind.None;
                    h.EngineParam = 0;
                    h.AtomicEffects = null;
                    _graph.steps.Clear();
                    if (atoms.Count > 0)
                    {
                        _graph.steps.Add(new EffectStepData { kind = 0, atomic = atoms[0] });
                        _graph.steps.Add(new EffectStepData
                        {
                            kind = 1,
                            conditionId = null,
                            thenSteps = atoms.Count > 1 ? new List<AtomicEffectEntry> { atoms[1] } : new List<AtomicEffectEntry>(),
                            elseSteps = new List<AtomicEffectEntry>(),
                        });
                    }
                    ShowToast("切换为有限分支——主干须为产出族原子");
                    break;
                }
            }

            _mode = newMode;
            _sel = SelKind.None;
            _selIndex = -1;
            RefreshAll();
        }

        private AtomicEffectEntry FirstKind0Atom()
        {
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
                if (s?.kind == 0 && s.atomic != null) return s.atomic;
            return null;
        }

        // ======================================== 左：槽位区（卡片=摘要+内联编辑） ========================================

        private void RefreshSlots()
        {
            _slotArea.Clear();
            // 只反注册上轮槽位（代价区是固定落区，OnEnter 注册一次——不能被这里清掉）
            foreach (var el in _slotEls) _drag.UnregisterZone(el);
            _slotEls.Clear();
            var h = _graph.header;
            _graph.steps ??= new List<EffectStepData>();

            switch (_mode)
            {
                case ComposeMode.Parallel:
                {
                    for (int i = 0; i < 3; i++)
                    {
                        int idx = i;
                        var slot = MakeSlot($"原子槽 {i + 1}", "drop-slot");
                        if (i < _graph.steps.Count)
                        {
                            var step = _graph.steps[i];
                            if (step.kind == 0) slot.Add(MakeAtomCard(step.atomic, idx, SelKind.Parallel));
                            else slot.Add(MakeReadonlyStepBadge(step)); // 抉择等只读
                        }
                        else if (i == _graph.steps.Count)
                        {
                            slot.Add(MakeHint("拖入原子 / 点击库追加"));
                        }
                        _drag.RegisterZone(slot,
                            payload => ParallelCanDrop(payload) && _graph.steps.Count < 3 && idx == _graph.steps.Count,
                            (payload, el) => DropParallel(payload));
                        _slotEls.Add(slot);
                        _slotArea.Add(slot);
                    }
                    if (_graph.steps.Count >= 3)
                        _slotArea.Add(MakeHint("并列上限 3 个原子（删除后再拖入）"));
                    break;
                }
                case ComposeMode.FreeBranch:
                {
                    // 主干槽（引擎条件）
                    var trunkSlot = MakeSlot("主干（条件引擎）", "drop-slot", "drop-slot--trunk");
                    if (h.EngineKind != (int)BranchEngineKind.None)
                    {
                        trunkSlot.Add(MakeTrunkCard((BranchEngineKind)h.EngineKind, h.EngineParam));
                    }
                    else trunkSlot.Add(MakeHint("拖入 拼点/运势/倒计时"));
                    _drag.RegisterZone(trunkSlot,
                        payload => payload is LibPayload lp && lp.IsEngineTrunk,
                        (payload, el) => DropFreeTrunk((LibPayload)payload));
                    _slotEls.Add(trunkSlot);
                    _slotArea.Add(trunkSlot);

                    var arrow = new Label("条件达成 →");
                    arrow.AddToClassList("hint");
                    arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
                    _slotArea.Add(arrow);

                    // 奖励槽（单原子）
                    var rewardSlot = MakeSlot("奖励（单原子）", "drop-slot", "drop-slot--reward");
                    var reward = h.AtomicEffects?.FirstOrDefault();
                    if (reward != null) rewardSlot.Add(MakeAtomCard(reward, 0, SelKind.FreeReward));
                    else rewardSlot.Add(MakeHint("拖入奖励原子（须开放分支奖励挂载）"));
                    _drag.RegisterZone(rewardSlot,
                        payload => RewardCanDrop(payload),
                        (payload, el) => DropFreeReward((LibPayload)payload));
                    _slotEls.Add(rewardSlot);
                    _slotArea.Add(rewardSlot);
                    break;
                }
                case ComposeMode.OutcomeGate:
                {
                    EnsureGateShape();
                    var trunk = _graph.steps[0];
                    var branch = _graph.steps[1];

                    // 主干槽（产出族原子）
                    var trunkSlot = MakeSlot("主干（产出原子）", "drop-slot", "drop-slot--trunk");
                    trunkSlot.Add(MakeAtomCard(trunk.atomic, 0, SelKind.GateTrunk));
                    _drag.RegisterZone(trunkSlot,
                        payload => payload is LibPayload lp && lp.CanBeGateTrunk && !lp.IsWrongSideOnly,
                        (payload, el) => DropGateTrunk((LibPayload)payload));
                    _slotEls.Add(trunkSlot);
                    _slotArea.Add(trunkSlot);

                    // 门行：条件下拉（中文+【奖励x】）内联——不再走检查器
                    _slotArea.Add(MakeGateRow(branch));

                    // 奖励槽（单原子 + 预算）
                    var rewardSlot = MakeSlot("奖励（单原子·预算内）", "drop-slot", "drop-slot--reward");
                    var reward = branch.thenSteps?.FirstOrDefault();
                    if (reward != null) rewardSlot.Add(MakeAtomCard(reward, 0, SelKind.GateReward));
                    else rewardSlot.Add(MakeHint("拖入奖励原子（推导费 ≤ 门预算）"));
                    _drag.RegisterZone(rewardSlot,
                        payload => GateRewardCanDrop(payload, branch),
                        (payload, el) => DropGateReward((LibPayload)payload));
                    _slotEls.Add(rewardSlot);
                    _slotArea.Add(rewardSlot);

                    _slotArea.Add(MakeGateBudgetLine(branch));
                    break;
                }
            }

            RefreshName();
        }

        // ======================================== 代价区（错边原子槽，位于效果编辑上方） ========================================

        /// <summary>代价落区注册（OnEnter 一次——容器固定，内容重建不影响注册）。
        /// 代价=错边效果（内容契约）：效果区禁错边、代价区只能错边；付费步执行+全价补偿黑/白。</summary>
        private void RegisterCostZone()
        {
            _drag.RegisterZone(_costZone,
                payload => payload is LibPayload lp && lp.IsWrongSideOnly,
                (payload, el) =>
                {
                    var lp = (LibPayload)payload;
                    _graph.header.Costs = new List<CostEntry>
                    {
                        new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = lp.NewEntry() },
                    };
                    _sel = SelKind.Cost;
                    _selIndex = 0;
                    RefreshCostZone();
                    RefreshName();
                });
        }

        /// <summary>当前代价栏的 Payload 原子（无则 null）。</summary>
        private AtomicEffectEntry PayloadCostEntry()
            => _graph.header.Costs?.FirstOrDefault(c => c != null
               && (CostType)c.CostType == CostType.Payload && c.payload != null)?.payload;

        private void RefreshCostZone()
        {
            _costZone.Clear();
            var header = new Label("代价（错边原子）——效果区禁错边；代价只能错边（付费步执行·全价补偿黑/白）");
            header.AddToClassList("panel__header");
            _costZone.Add(header);

            var slot = MakeSlot("代价槽", "drop-slot", "drop-slot--reward");
            var payload = PayloadCostEntry();
            if (payload != null) slot.Add(MakeAtomCard(payload, 0, SelKind.Cost));
            else slot.Add(MakeHint("拖入错边原子（对自己有害/对对手有益——如 弃牌/送墓/流失 锁己方域，或增益锁对方域）"));
            _costZone.Add(slot);
        }

        /// <summary>有限分支数据形：恒 [kind=0 主干, kind=1 分支]。</summary>
        private void EnsureGateShape()
        {
            while (_graph.steps.Count < 2)
                _graph.steps.Add(_graph.steps.Count == 0
                    ? new EffectStepData { kind = 0, atomic = AtomRefs.New(AtomicEffectType.DealDamage) }
                    : new EffectStepData { kind = 1, thenSteps = new List<AtomicEffectEntry>(), elseSteps = new List<AtomicEffectEntry>() });
            _graph.steps[1].thenSteps ??= new List<AtomicEffectEntry>();
            _graph.steps[1].elseSteps ??= new List<AtomicEffectEntry>();
        }

        private VisualElement MakeSlot(string title, params string[] classes)
        {
            var slot = new VisualElement();
            slot.AddToClassList("drop-slot");
            foreach (var c in classes) slot.AddToClassList(c);
            var lbl = new Label(title);
            lbl.AddToClassList("field-label");
            slot.Add(lbl);
            return slot;
        }

        private VisualElement MakeHint(string text)
        {
            var hint = new Label(text);
            hint.AddToClassList("hint");
            return hint;
        }

        // 槽内原子卡：摘要行（描述+↑↓删除，点击展开）+ 展开态=内联参数编辑（原检查器内容）。
        private VisualElement MakeAtomCard(AtomicEffectEntry atom, int index, SelKind selKind)
        {
            var expanded = _sel == selKind
                && (selKind != SelKind.Parallel || _selIndex == index)
                && selKind != SelKind.FreeTrunk;

            var card = new VisualElement();
            card.AddToClassList("step-card");
            card.AddToClassList("atom-card");

            // ---- 摘要行 ----
            var head = new VisualElement();
            head.AddToClassList("list-row");

            var text = new Label((expanded ? "▼ " : "▶ ") + AtomText.RenderAtomEntry(atom));
            text.AddToClassList("list-row__name");
            head.Add(text);

            if (selKind == SelKind.Parallel)
            {
                var up = new Button(() => MoveParallel(index, -1)) { text = "↑" };
                var down = new Button(() => MoveParallel(index, 1)) { text = "↓" };
                foreach (var b in new[] { up, down })
                {
                    b.AddToClassList("btn"); b.AddToClassList("btn--mini");
                    head.Add(b);
                }
            }

            var del = new Button(() => RemoveAtom(selKind, index)) { text = "删除" };
            del.AddToClassList("btn"); del.AddToClassList("btn--mini"); del.AddToClassList("btn--danger");
            head.Add(del);

            text.RegisterCallback<ClickEvent>(_ => ToggleExpand(selKind, index));
            card.Add(head);

            // ---- 展开态：内联编辑（原检查器的 MountKinds 驱动面） ----
            if (expanded)
                card.Add(MakeAtomEditorInline(atom, selKind));

            return card;
        }

        /// <summary>原子内联编辑器（检查器移除后的替代——挂在卡片展开区）。</summary>
        private VisualElement MakeAtomEditorInline(AtomicEffectEntry atom, SelKind selKind)
        {
            var box = new VisualElement();
            box.AddToClassList("sub-item");

            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type))
            {
                box.Add(MakeHint($"（原子表引用缺失：{atom.refId ?? "空"}）"));
                return box;
            }
            var mounts = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
            bool hasValue = (cfg?.Description ?? "").Contains("{value}");

            // 实时描述行（值随机/目标随机动态渲染）
            var desc = new Label(AtomText.Render(cfg, atom, _graph.header));
            desc.AddToClassList("hint");
            box.Add(desc);

            // Value（原子唯一可编辑数值；无 {value} 模板则只读说明）
            if (hasValue)
            {
                var v = new IntegerField("数值") { value = atom.value };
                v.AddToClassList("int-input");
                v.RegisterValueChangedCallback(e =>
                {
                    atom.value = e.newValue;
                    RefreshSlots();
                });
                box.Add(v);
            }
            else
            {
                box.Add(MakeHint("无数值参数（该原子 Value 不参与语义）"));
            }

            // MountKind 7：数值随机滑条（0-100% → RandomAmplitude；描述即时重渲）
            if (mounts.Contains(MountKind.RandomMount) && hasValue)
            {
                var slider = new SliderInt("数值随机 %", 0, 100) { value = Mathf.RoundToInt(atom.amp * 100f) };
                slider.AddToClassList("int-input");
                slider.RegisterValueChangedCallback(e =>
                {
                    atom.amp = e.newValue / 100f;
                    RefreshSlots();
                });
                box.Add(slider);
                box.Add(MakeHint("计价按名义数值——随机只影响结算分布（锚点不漂移）"));
            }

            // TargetKinds 实例收窄（单选下拉：表级全集；"表默认"=null）+ 目标随机开关
            var tableKinds = cfg?.GetTargetKindList() ?? new List<int>();
            if (tableKinds.Count > 0)
            {
                var row = new VisualElement(); row.AddToClassList("toolbar");
                var labels = new List<string> { "表默认" };
                labels.AddRange(tableKinds.Select(k => $"{k} {((TargetKind)k)}"));
                var dd = new DropdownField("目标域") { choices = labels };
                dd.AddToClassList("text-input");
                int cur = atom.kinds != null && atom.kinds.Count == 1
                    ? 1 + tableKinds.IndexOf(atom.kinds[0]) : 0;
                dd.index = cur >= 0 ? cur : 0;
                dd.RegisterValueChangedCallback(_ =>
                {
                    int i = dd.index;
                    atom.kinds = i <= 0 ? null : new List<int> { tableKinds[i - 1] };
                });
                row.Add(dd);
                box.Add(MakeHint("目标域：收窄本原子的可选范围（表默认=表行声明域；目标随机=开启后不弹选择，按种子随机选）"));
                box.Add(row);

                var rand = new Toggle("目标随机（不弹选择·按种子随机）")
                {
                    value = _graph.header.SelectionMode == (int)CardCore.SelectionMode.Random,
                };
                rand.RegisterValueChangedCallback(e =>
                {
                    _graph.header.SelectionMode = e.newValue ? (int)CardCore.SelectionMode.Random : (int)CardCore.SelectionMode.None;
                    RefreshEffectSettings();
                    RefreshSlots();
                });
                box.Add(rand);
            }

            // 位 2：指示物持续提示
            if (mounts.Contains(MountKind.Counter))
                box.Add(MakeHint("指示物原子：持续规则由指示物本身承载——效果级持续档仅供参考"));

            // 位 8：触发上限锁定
            if (mounts.Contains(MountKind.TriggerCapImmutable))
                box.Add(MakeHint("触发上限锁定：该原子恒无限（TriggerLimitPerTurn 被覆写，不可限）"));

            // DrawCard：Drawbacks 降级 CSV（数据源 BranchConfig 已删）
            if (type == AtomicEffectType.DrawCard)
            {
                _graph.header.Drawbacks ??= new List<string>();
                var f = new TextField("抽牌缺陷 CSV") { value = string.Join(",", _graph.header.Drawbacks) };
                f.AddToClassList("text-input");
                f.RegisterValueChangedCallback(e =>
                    _graph.header.Drawbacks = e.newValue.Split(',')
                        .Select(s2 => s2.Trim()).Where(s2 => s2.Length > 0).ToList());
                box.Add(f);
                box.Add(MakeHint("缺陷目录（BranchConfig.json）已删——按 id 手填，每个 −1 费"));
            }
            else if (type == AtomicEffectType.SearchDeck)
            {
                box.Add(MakeHint("检索按维度档计费：字符串字段填宣言卡名（ExactCard=3），空=单维度 1"));
            }

            // 有限分支奖励：预算行
            if (selKind == SelKind.GateReward && _graph.steps.Count > 1)
                box.Add(MakeHint(GateBudgetText(_graph.steps[1])));

            return box;
        }

        // 自由分支主干卡：摘要行（引擎描述+移除）+ 展开态=参数编辑（header 通道无原子条目）。
        private VisualElement MakeTrunkCard(BranchEngineKind engine, int param)
        {
            var expanded = _sel == SelKind.FreeTrunk;
            var card = new VisualElement();
            card.AddToClassList("step-card");
            card.AddToClassList("atom-card");

            var head = new VisualElement();
            head.AddToClassList("list-row");
            var text = new Label((expanded ? "▼ " : "▶ ") + AtomText.TrunkText(engine, param));
            text.AddToClassList("list-row__name");
            head.Add(text);

            var del = new Button(() =>
            {
                _graph.header.EngineKind = (int)BranchEngineKind.None;
                _graph.header.EngineParam = 0;
                _sel = SelKind.None;
                RefreshSlots();
            }) { text = "移除" };
            del.AddToClassList("btn"); del.AddToClassList("btn--mini"); del.AddToClassList("btn--danger");
            head.Add(del);

            text.RegisterCallback<ClickEvent>(_ => ToggleExpand(SelKind.FreeTrunk, 0));
            card.Add(head);

            if (expanded)
            {
                var box = new VisualElement();
                box.AddToClassList("sub-item");
                string label = engine == BranchEngineKind.Countdown ? "回合数（0=自动换算）" : "参数 x（1-5）";
                var field = new IntegerField(label) { value = _graph.header.EngineParam };
                field.AddToClassList("int-input");
                field.RegisterValueChangedCallback(e =>
                {
                    _graph.header.EngineParam = engine == BranchEngineKind.Countdown
                        ? Mathf.Max(0, e.newValue)
                        : Mathf.Clamp(e.newValue, 1, 5);
                    field.SetValueWithoutNotify(_graph.header.EngineParam);
                    RefreshSlots();
                });
                box.Add(field);
                box.Add(MakeHint("主干只是条件——奖励在下方奖励槽（单原子，不占卡费）。"));
                card.Add(box);
            }
            return card;
        }

        private VisualElement MakeReadonlyStepBadge(EffectStepData step)
        {
            var badge = new VisualElement();
            badge.AddToClassList("step-card");
            var text = new Label(step.kind == 2
                ? $"抉择（{step.choices?.Count ?? 0} 模式）——卡组成阶段编辑，此处只读"
                : $"步骤 kind={step.kind}（只读）");
            text.AddToClassList("list-row__name");
            badge.Add(text);
            return badge;
        }

        // 门行：条件下拉内联（中文标签带【奖励x】）。
        private VisualElement MakeGateRow(EffectStepData branch)
        {
            var row = new VisualElement();
            row.AddToClassList("step-card");
            var wrap = new VisualElement();
            wrap.AddToClassList("toolbar");
            var lbl = new Label("条件：");
            lbl.AddToClassList("field-label");
            wrap.Add(lbl);

            var trunkType = ResolveType(_graph.steps[0].atomic);
            var gates = ComposerCatalog.GatesFor(trunkType).ToList();
            if (gates.Count == 0)
            {
                wrap.Add(MakeHint("主干不是产出族原子——无法挂产出条件"));
            }
            else
            {
                var labels = gates.Select(ComposerCatalog.GateLabel).ToList();
                var dd = new DropdownField { choices = labels };
                dd.AddToClassList("text-input");
                int cur = gates.FindIndex(g => g.Id == branch.conditionId);
                if (cur < 0) { cur = 0; branch.conditionId = gates[0].Id; }
                dd.index = cur;
                dd.RegisterValueChangedCallback(_ =>
                {
                    int i = dd.index;
                    if (i >= 0 && i < gates.Count)
                    {
                        branch.conditionId = gates[i].Id;
                        RefreshSlots(); // 预算行随门刷新
                    }
                });
                wrap.Add(dd);
            }
            row.Add(wrap);
            return row;
        }

        private VisualElement MakeGateBudgetLine(EffectStepData branch)
        {
            var label = new Label(GateBudgetText(branch));
            label.AddToClassList("hint");
            return label;
        }

        private string GateBudgetText(EffectStepData branch)
        {
            var trunkType = ResolveType(_graph.steps[0].atomic);
            var gates = ComposerCatalog.GatesFor(trunkType).ToList();
            var gate = gates.FirstOrDefault(g => g.Id == branch.conditionId) ?? gates.FirstOrDefault();
            if (gate == null) return "主干不是产出族原子——无法挂产出条件";
            int budget = ComposerCatalog.GateBudget(gate);
            var reward = branch.thenSteps?.FirstOrDefault();
            float cost = reward != null
                ? CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { CardEffectConverter.ConvertAtomForUI(reward) })
                : 0f;
            string state = cost > budget ? $"（超出 {cost - budget:0.#}——保存前请调整）" : "";
            return $"【奖励{budget}】当前 {cost:0.#}/{budget}{state}";
        }

        /// <summary>原子引用 → 枚举（行缺失回退 DealDamage——门行兜底口径同旧）。</summary>
        private static AtomicEffectType ResolveType(AtomicEffectEntry a)
        {
            var row = AtomicEffectTable.GetByHashId(a?.refId);
            return row != null && Enum.TryParse<AtomicEffectType>(row.EnumName, out var t)
                ? t : AtomicEffectType.DealDamage;
        }

        private void ToggleExpand(SelKind kind, int index)
        {
            if (_sel == kind && (kind != SelKind.Parallel || _selIndex == index))
            {
                _sel = SelKind.None;
                _selIndex = -1;
            }
            else
            {
                _sel = kind;
                _selIndex = index;
            }
            RefreshSlots();
        }

        // ---- 并列槽操作 ----
        private void MoveParallel(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= _graph.steps.Count || target < 0 || target >= _graph.steps.Count) return;
            (_graph.steps[index], _graph.steps[target]) = (_graph.steps[target], _graph.steps[index]);
            if (_sel == SelKind.Parallel && _selIndex == index) _selIndex = target;
            else if (_sel == SelKind.Parallel && _selIndex == target) _selIndex = index;
            RefreshSlots();
        }

        private void RemoveAtom(SelKind kind, int index)
        {
            switch (kind)
            {
                case SelKind.Parallel:
                    _graph.steps.RemoveAt(index);
                    _sel = SelKind.None;
                    break;
                case SelKind.FreeReward:
                    _graph.header.AtomicEffects = null;
                    _sel = SelKind.None;
                    break;
                case SelKind.GateReward:
                    _graph.steps[1].thenSteps.Clear();
                    _sel = SelKind.None;
                    break;
                case SelKind.FreeTrunk:
                    _graph.header.EngineKind = (int)BranchEngineKind.None;
                    _graph.header.EngineParam = 0;
                    _sel = SelKind.None;
                    break;
                case SelKind.GateTrunk:
                    _sel = SelKind.None; // 主干不可删（模式数据形依赖）——换原子用拖拽覆盖
                    ShowToast("主干槽请拖入新原子覆盖");
                    return;
                case SelKind.Cost:
                    _graph.header.Costs = _graph.header.Costs != null
                        ? _graph.header.Costs.Where(c => c == null || (CostType)c.CostType != CostType.Payload).ToList()
                        : null;
                    _sel = SelKind.None;
                    RefreshCostZone();
                    RefreshName();
                    return;
            }
            RefreshSlots();
        }

        // ---- drop 资格与落槽 ----

        // 并列槽可落：主动位原子（MountKinds 含 0）且**非错边**（内容契约：效果区禁错边——错边只能进代价槽）
        // 或关键词条目（关键词=默认 Self 的授予原子）
        private bool ParallelCanDrop(object payload)
            => (payload is LibPayload lp && lp.CanBeActiveAtom && !lp.IsWrongSideOnly) || payload is KeywordCatalogEntry;

        private void DropParallel(object payload)
        {
            if (_graph.steps.Count >= 3) { ShowToast("并列上限 3 个原子"); return; }
            if (payload is KeywordCatalogEntry kw)
            {
                _graph.steps.Add(new EffectStepData
                {
                    kind = 0,
                    atomic = new AtomicEffectEntry { refId = CardCore.Attribute.AtomicEffectTable.GetByEnumName(kw.AtomicEffect)?.HashId ?? kw.AtomicEffect, value = 1, str = kw.Id },
                });
                // 关键词授予惯例（同旧版）：Self + Permanent
                _graph.header.SelectionMode = (int)CardCore.SelectionMode.Self;
                _graph.header.Duration = (int)DurationType.Permanent;
            }
            else if (payload is LibPayload lp)
            {
                _graph.steps.Add(new EffectStepData { kind = 0, atomic = lp.NewEntry() });
            }
            _sel = SelKind.Parallel;
            _selIndex = _graph.steps.Count - 1;
            RefreshSlots();
        }

        private void DropFreeTrunk(LibPayload lp)
        {
            var engine = ComposerCatalog.TrunkToEngine(lp.Type);
            _graph.header.EngineKind = (int)engine;
            _graph.header.EngineParam = engine == BranchEngineKind.Countdown
                ? Mathf.Max(0, lp.PreviewValue)
                : Mathf.Clamp(lp.PreviewValue, 1, 5);
            _sel = SelKind.FreeTrunk;
            _selIndex = 0;
            RefreshSlots();
        }

        private bool RewardCanDrop(object payload)
            => payload is LibPayload lp && lp.CanBeBranchReward && !lp.IsWrongSideOnly;

        private void DropFreeReward(LibPayload lp)
        {
            _graph.header.AtomicEffects = new List<AtomicEffectEntry> { lp.NewEntry() };
            _sel = SelKind.FreeReward;
            _selIndex = 0;
            RefreshSlots();
        }

        private bool GateRewardCanDrop(object payload, EffectStepData branch)
        {
            if (payload is not LibPayload lp || !lp.CanBeBranchReward || lp.IsWrongSideOnly) return false;
            return RewardWithinBudget(lp.Entry(), branch);
        }

        private bool RewardWithinBudget(AtomicEffectEntry entry, EffectStepData branch)
        {
            var trunkType = ResolveType(_graph.steps[0].atomic);
            var gates = ComposerCatalog.GatesFor(trunkType).ToList();
            var gate = gates.FirstOrDefault(g => g.Id == branch.conditionId) ?? gates.FirstOrDefault();
            if (gate == null) return false;
            var inst = CardEffectConverter.ConvertAtomForUI(entry);
            if (inst == null) return false;
            return CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { inst })
                <= ComposerCatalog.GateBudget(gate);
        }

        private void DropGateTrunk(LibPayload lp)
        {
            _graph.steps[0] = new EffectStepData { kind = 0, atomic = lp.NewEntry() };
            // 门可能失效：不在新主干适用族 → 重置为首项
            var branch = _graph.steps[1];
            var gates = ComposerCatalog.GatesFor(lp.Type).ToList();
            if (!gates.Any(g => g.Id == branch.conditionId))
                branch.conditionId = gates.FirstOrDefault()?.Id;
            _sel = SelKind.GateTrunk;
            _selIndex = 0;
            RefreshSlots();
        }

        private void DropGateReward(LibPayload lp)
        {
            _graph.steps[1].thenSteps = new List<AtomicEffectEntry> { lp.NewEntry() };
            _sel = SelKind.GateReward;
            _selIndex = 0;
            RefreshSlots();
        }

        // ======================================== 左：效果设置（效果级编排字段） ========================================

        private void RefreshEffectSettings()
        {
            _effectSettings.Clear();
            var h = _graph.header;

            var row0 = new VisualElement(); row0.AddToClassList("toolbar");
            row0.Add(MakeIntField("速度", h.BaseSpeed, v => h.BaseSpeed = v));
            row0.Add(MakeIntField("触发上限(0默认/1一回合一次/-1无限)", h.TriggerLimitPerTurn, v => h.TriggerLimitPerTurn = v));
            _effectSettings.Add(row0);

            var row1 = new VisualElement(); row1.AddToClassList("toolbar");
            // ForTurns 仅存于指示物时钟（2026-09-14 收缩：效果级 DurationValue 退役）——效果级下拉不提供
            var durNames = Enum.GetNames(typeof(DurationType)).Where(n => n != nameof(DurationType.ForTurns)).ToList();
            var dur = new DropdownField("持续") { choices = durNames };
            dur.AddToClassList("text-input");
            dur.index = Mathf.Clamp(h.Duration >= 0 ? h.Duration : 0, 0, durNames.Count - 1);
            dur.RegisterValueChangedCallback(_ => h.Duration = dur.index);
            row1.Add(dur);

            var modeChoices = SelectionModes.Select(m => m.label).ToList();
            var sel = new DropdownField("选择模式") { choices = modeChoices };
            sel.AddToClassList("text-input");
            int selIdx = Mathf.Max(0, SelectionModes.ToList().FindIndex(m => m.value == h.SelectionMode));
            sel.index = selIdx;
            sel.RegisterValueChangedCallback(_ => h.SelectionMode = SelectionModes[sel.index].value);
            row1.Add(sel);
            _effectSettings.Add(row1);

            var row2 = new VisualElement(); row2.AddToClassList("toolbar");
            // -1=任意（2026-09-14 并入 DynamicTargetCount：玩家自选数量=原子计 0 费+整卡不可作地牌）
            row2.Add(MakeIntField("数量(>0=N/0全部/-1任意)", h.TargetCount, v => h.TargetCount = v));

            var dropChoices = new List<string> { "战场", "手牌", "牌库" };
            var dropVals = new List<int> { (int)Zone.Battlefield, (int)Zone.Hand, (int)Zone.Deck };
            var drop = new DropdownField("落区") { choices = dropChoices };
            drop.AddToClassList("text-input");
            int di = dropVals.IndexOf(h.SummonDropZone);
            drop.index = di >= 0 ? di : 0;
            drop.RegisterValueChangedCallback(_ => h.SummonDropZone = dropVals[Mathf.Max(0, drop.index)]);
            row2.Add(drop);
            _effectSettings.Add(row2);

            // 每个可选项的说明（用户定案：效果编辑的选项前置介绍——选项是什么/怎么选）
            _effectSettings.Add(MakeHint("速度：0=只能自己回合的主阶段发动；1=瞬间基准（对手回合也能发动/响应）"));
            _effectSettings.Add(MakeHint("触发上限：触发式每回合次数——0=默认（一回合一次）；N=每回合 N 次；-1=无限"));
            _effectSettings.Add(MakeHint("持续：一次性效果选 Once；永久持续选 Permanent；ForTurns 需配「持续值」=回合数"));
            _effectSettings.Add(MakeHint("选择模式：目标怎么选——自身=源卡不弹窗；手动=弹窗选；全域=全取不弹；随机=按种子随机不弹"));
            _effectSettings.Add(MakeHint("数量：固定选 N 个（0=全部、-1=任意）；动态数量=运行时自选个数（计费 0 且不可作地牌）"));
            _effectSettings.Add(MakeHint("落区：衍生物（召唤）的生成位置——战场/手牌/牌库"));
        }

        // ======================================== 右：双模式展示区 ========================================

        private void OnReloadAtoms()
        {
            AtomicEffectTable.Reload();
            _filterTypeZh = null;
            BuildFilterBar();
            SetRightMode(RightMode.Atoms);
            RefreshLibrary();
            RefreshKeywords();
            int count = _libraryList?.contentContainer?.childCount ?? 0;
            ShowToast($"原子表已重读：{count} 行入列");
        }

        private void OnLoadEffectsTable()
        {
            SetRightMode(RightMode.Effects);
        }

        private void SetRightMode(RightMode mode)
        {
            _rightMode = mode;
            _effectsList.style.display = mode == RightMode.Effects ? DisplayStyle.Flex : DisplayStyle.None;
            _atomPanel.style.display = mode == RightMode.Atoms ? DisplayStyle.Flex : DisplayStyle.None;
            // 效果分类下拉只对原子库有意义——效果表模式下禁用（近似搜索对两模式都生效）
            _filterTypeDropdown.SetEnabled(mode == RightMode.Atoms);

            // 按钮态指示（当前模式高亮）
            _reloadAtomsBtn.EnableInClassList("btn--primary", mode == RightMode.Atoms);
            _loadEffectsBtn.EnableInClassList("btn--primary", mode == RightMode.Effects);

            if (mode == RightMode.Effects) RefreshEffectsList();
        }

        private void RefreshEffectsList()
        {
            _effectsList.Clear();
            EffectsLibrary.Reload(); // 2026-09-14：外部可能直改 Effects.json——进效果表即重读
            var effects = EffectLibrarySerializer.LoadAll();
            if (effects.Count == 0)
            {
                _effectsList.Add(MakeHint("效果库为空——左侧编辑后「保存」即入库"));
                return;
            }
            foreach (var fx in effects)
            {
                var captured = fx;
                var summaryText = AtomText.RenderEffectSummary(captured);

                // 近似搜索联动（按名+摘要过滤）
                if (!string.IsNullOrEmpty(_filterName.value)
                    && !(captured.name ?? "").Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)
                    && !summaryText.Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)) continue;

                var row = new VisualElement();
                row.AddToClassList("list-row");
                row.AddToClassList("library-row");

                var name = new Label($"{captured.name}（{captured.id}）");
                name.AddToClassList("list-row__name");
                row.Add(name);

                var summary = new Label(summaryText);
                summary.AddToClassList("list-row__meta");
                row.Add(summary);

                row.RegisterCallback<ClickEvent>(_ => LoadEffectFromLibrary(captured));
                _effectsList.Add(row);
            }
        }

        /// <summary>点击效果表条目 → 载入左侧编辑（保存覆盖原名，自动改名清旧档）。</summary>
        private void LoadEffectFromLibrary(EffectGraphData fx)
        {
            if (_editingCard != null)
            {
                ShowToast("卡编辑模式不可载入库效果（先保存或返回）");
                return;
            }
            _graph = fx;
            _graph.header ??= new CardEffectData();
            _graph.steps ??= new List<EffectStepData>();
            _loadedFromLibraryId = _graph.id;

            _mode = InferMode(_graph);
            _sel = SelKind.None;
            _selIndex = -1;
            SyncTimingDropdown();
            _activationDropdown.index = Mathf.Clamp(_graph.header.ActivationType, 0, ActivationNames.Length - 1);
            SyncActivationVisibility();
            RefreshAll();
            ShowToast($"已载入：{_graph.name}（编辑后保存覆盖）");
        }

        // ---- 筛选栏（原子库模式） ----

        private void BuildFilterBar()
        {
            // 效果分类下拉（UI 全中文）：全部 + 表行中文名（行级身份——洗回/洗入各自成项）
            var types = new List<string> { "全部" };
            types.AddRange(AllTableRows().Select(r => r.DisplayName).Where(n => !string.IsNullOrEmpty(n))
                .Distinct().OrderBy(n => n, StringComparer.CurrentCulture));
            _filterTypeDropdown.choices = types;
            _filterTypeDropdown.index = 0;
            _filterTypeDropdown.RegisterValueChangedCallback(_ =>
            {
                _filterTypeZh = _filterTypeDropdown.index <= 0 ? null : types[_filterTypeDropdown.index];
                RefreshLibrary();
            });

            // 近似搜索：中文名 ∪ 描述模板 两列并集（效果表模式联动按名+摘要）
            _filterName.RegisterValueChangedCallback(_ => { RefreshLibrary(); RefreshKeywords(); RefreshEffectsList(); });
        }

        private static IEnumerable<AtomicEffectConfig> AllTableRows()
            => AtomicEffectTable.GetAll().Where(r => r != null && !string.IsNullOrEmpty(r.EnumName));

        private void RefreshLibrary()
        {
            _libraryList.Clear();
            foreach (var row in AllTableRows())
            {
                if (!Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) continue;
                if (_filterTypeZh != null && row.DisplayName != _filterTypeZh) continue;
                // 近似搜索：中文名 ∪ 描述模板 两列并集
                if (!string.IsNullOrEmpty(_filterName.value)
                    && !(row.DisplayName ?? "").Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)
                    && !(row.Description ?? "").Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)) continue;

                _libraryList.Add(MakeLibraryRow(new LibPayload(type, row)));
            }
        }

        private VisualElement MakeLibraryRow(LibPayload payload)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");
            row.AddToClassList("library-row");

            // 圆点直读表 EffectColor 列（config.Tags 承载颜色名）——不经 ElementAffinity 间接层
            var dot = new VisualElement();
            dot.AddToClassList("color-dot");
            dot.AddToClassList(ChipClass(ColorFilter.OfColorName(payload.Cfg.Tags)));
            row.Add(dot);

            var name = new Label(payload.Cfg.DisplayName);
            name.AddToClassList("list-row__name");
            row.Add(name);

            var tpl = new Label(payload.Cfg.Description);
            tpl.AddToClassList("list-row__meta");
            row.Add(tpl);

            // 拖拽源 + 点击=追加到首个合格槽（兜底交互）
            DragController.AttachPayload(row, payload, payload.Cfg.DisplayName);
            _drag.RegisterDragSource(row);
            row.RegisterCallback<ClickEvent>(_ => QuickAdd(payload));

            return row;
        }

        /// <summary>点击库行 = 等价于拖到当前形态的首个合格槽。</summary>
        private void QuickAdd(LibPayload payload)
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                    if (ParallelCanDrop(payload)) DropParallel(payload);
                    else ShowToast("该原子不可作主动效果（MountKinds 不含主动位）");
                    break;
                case ComposeMode.FreeBranch:
                    if (_graph.header.EngineKind == (int)BranchEngineKind.None && payload.IsEngineTrunk) DropFreeTrunk(payload);
                    else if ((_graph.header.AtomicEffects == null || _graph.header.AtomicEffects.Count == 0) && RewardCanDrop(payload)) DropFreeReward(payload);
                    else ShowToast("槽已占用或该原子不合资格");
                    break;
                case ComposeMode.OutcomeGate:
                    if (payload.CanBeGateTrunk && (_sel == SelKind.GateTrunk || _graph.steps[0].atomic == null)) DropGateTrunk(payload);
                    else if (GateRewardCanDrop(payload, _graph.steps[1])) DropGateReward(payload);
                    else ShowToast("不合槽位资格（产出族/预算/挂载位）");
                    break;
            }
        }

        private void RefreshKeywords()
        {
            _keywordList.Clear();
            foreach (var kw in KeywordCatalog.LoadAll())
            {
                if (!string.IsNullOrEmpty(_filterName.value) && !kw.DisplayName.Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)) continue;
                var captured = kw;
                var row = new VisualElement();
                row.AddToClassList("list-row");
                row.AddToClassList("library-row");

                var dot = new VisualElement();
                dot.AddToClassList("color-dot");
                dot.AddToClassList(ChipClass(kw.Color));
                row.Add(dot);
                var name = new Label(kw.DisplayName);
                name.AddToClassList("list-row__name");
                row.Add(name);

                DragController.AttachPayload(row, captured, kw.DisplayName);
                _drag.RegisterDragSource(row);
                row.RegisterCallback<ClickEvent>(_ => QuickAddKeyword(captured));
                _keywordList.Add(row);
            }
        }

        private void QuickAddKeyword(KeywordCatalogEntry kw)
        {
            if (_mode != ComposeMode.Parallel) { ShowToast("关键词只可加入并列组合"); return; }
            DropParallel(kw);
        }

        // ======================================== 保存 ========================================

        private void OnSave()
        {
            // 效果名自动构成（中文名＋组合方式＋中文名）——只读展示，保存时落账
            _graph.name = AutoName();
            _graph.header.DisplayName = _graph.name;

            // 卡编辑模式：写回卡牌并返回（不落效果库文件）
            if (_editingCard != null)
            {
                _editingCard.Effects ??= new List<CardEffectData>();
                var effect = BuildCardEffect();
                if (_editingIndex >= 0 && _editingIndex < _editingCard.Effects.Count)
                    _editingCard.Effects[_editingIndex] = effect;
                else
                    _editingCard.Effects.Add(effect);
                _editingCard = null;
                _editingIndex = -1;
                Manager.Back();
                return;
            }

            // 效果库模式：内容变更换 id——按旧 id 清档防重复（同内容同 id 天然 upsert 覆盖）
            var path = EffectLibrarySerializer.Save(_graph); // Save 内计算/回填 graph.id
            if (!string.IsNullOrEmpty(_loadedFromLibraryId) && _loadedFromLibraryId != _graph.id)
            {
                EffectLibrarySerializer.DeleteById(_loadedFromLibraryId);
            }
            _loadedFromLibraryId = _graph.id;
            ShowToast(path == null ? "保存失败" : $"已保存（覆盖）：{_graph.name}（{_graph.id}）");
            if (_rightMode == RightMode.Effects) RefreshEffectsList();
        }

        // ======================================== 卡编辑往返（深拷贝） ========================================

        /// <summary>卡内效果 → 编辑图（JsonUtility 往返深拷贝）。</summary>
        private static EffectGraphData CardEffectToGraph(CardEffectData effect)
        {
            if (effect == null) return new EffectGraphData("新效果");
            var header = JsonUtility.FromJson<CardEffectData>(JsonUtility.ToJson(effect));
            var steps = effect.Steps != null
                ? JsonUtility.FromJson<ListStepWrap>(JsonUtility.ToJson(new ListStepWrap { items = effect.Steps })).items
                : new List<EffectStepData>();
            return new EffectGraphData(string.IsNullOrEmpty(effect.DisplayName) ? "新效果" : effect.DisplayName)
            {
                header = header,
                steps = steps ?? new List<EffectStepData>(),
            };
        }

        /// <summary>编辑图 → 卡内效果（编排字段全量拷贝；引擎通道=header.EngineKind+AtomicEffects）。</summary>
        private CardEffectData BuildCardEffect()
        {
            var effect = JsonUtility.FromJson<CardEffectData>(JsonUtility.ToJson(_graph.header));
            effect.DisplayName = _graph.name;
            effect.Steps = _graph.steps != null && _graph.steps.Count > 0
                ? JsonUtility.FromJson<ListStepWrap>(JsonUtility.ToJson(new ListStepWrap { items = _graph.steps })).items
                : null;
            // 并列/有限分支：扁平投影（converter 双通道兼容——Steps 非空走 Steps）
            effect.AtomicEffects = _graph.header.EngineKind != (int)BranchEngineKind.None
                ? _graph.header.AtomicEffects
                : ProjectLinear(_graph.steps);
            return effect;
        }

        [Serializable]
        private sealed class ListStepWrap { public List<EffectStepData> items; }

        private static List<AtomicEffectEntry> ProjectLinear(List<EffectStepData> steps)
        {
            var flat = new List<AtomicEffectEntry>();
            if (steps == null) return flat;
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.kind == 0 && step.atomic != null) flat.Add(step.atomic);
                else if (step.kind == 1 && step.thenSteps != null) flat.AddRange(step.thenSteps);
            }
            return flat;
        }

        // ======================================== 小工具 ========================================

        private static VisualElement MakeIntField(string label, int value, Action<int> onChanged)
        {
            var field = new IntegerField(label) { value = value };
            field.AddToClassList("int-input");
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return field;
        }

        private static string ChipClass(UIColor color)
        {
            return color switch
            {
                UIColor.Red => "chip--red",
                UIColor.Blue => "chip--blue",
                UIColor.Green => "chip--green",
                UIColor.Gray => "chip--gray",
                UIColor.Black => "chip--black",
                UIColor.White => "chip--white",
                _ => "chip--all",
            };
        }

        private void ShowToast(string message) => _toast.text = message;

        // ======================================== 库行负载（表行 ↔ 落槽资格的唯一桥） ========================================

        /// <summary>
        /// 原子库行负载：表行 + 枚举。落槽资格全部经此判（MountKinds 位）——
        /// 拖拽与点击（QuickAdd）共用，保证两路同口径。
        /// </summary>
        public sealed class LibPayload
        {
            public readonly AtomicEffectType Type;
            public readonly AtomicEffectConfig Cfg;
            public LibPayload(AtomicEffectType type, AtomicEffectConfig cfg) { Type = type; Cfg = cfg; }

            /// <summary>预览默认值（新原子 Value=1）。</summary>
            public int PreviewValue => 1;

            public bool IsEngineTrunk => ComposerCatalog.IsEngineTrunk(Type);
            public bool CanBeGateTrunk => ComposerCatalog.CanBeGateTrunk(Type);

            private HashSet<MountKind> Mounts => MountKindExtensions.ParseCsv(Cfg?.MountKinds ?? "");

            public bool CanBeActiveAtom => !IsEngineTrunk && Mounts.Contains(MountKind.ActiveEffect);
            public bool CanBeBranchReward => !IsEngineTrunk && Mounts.Contains(MountKind.BranchReward);

            /// <summary>错边原子（内容契约：只能进代价栏）——p≠0 且表级域锁错侧，或 p=0 单侧域锁。
            /// 与 CardEffectConverter 代价校验同口径（表级域判定；实例收窄后的运行时错边照发黑/白）。</summary>
            public bool IsWrongSideOnly
            {
                get
                {
                    if (Cfg == null) return false;
                    float p = UnityEngine.Mathf.Clamp(Cfg.Polarity, -1f, 1f);
                    var kinds = Cfg.GetTargetKindList();
                    if (p != 0f) return CostDerivationService.WrongSide(p, kinds);
                    return CostDerivationService.SideLock(kinds) != 0;
                }
            }

            /// <summary>落槽生成的新原子条目（引用型：refId=表行ID，value=1）。</summary>
            public AtomicEffectEntry NewEntry() => Entry();

            public AtomicEffectEntry Entry()
                => new AtomicEffectEntry { refId = Cfg?.HashId, value = 1 };
        }
    }
}
