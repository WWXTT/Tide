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
    /// 左=编辑栏（模式条+代价区+槽位区）/ 右=展示区（原子库 / 效果表双模式）。
    /// 2026-09-21 定案：**拖拽已弃用（DragController 删除）**——全部交互走点击；
    /// 同日交互重做：**槽位选中制**——左侧默认选中原子1（首个槽），右侧点击库行
    /// =**替换选中槽的原子**（不再是顺序追加；点击左侧槽可切换选中；填入空槽后自动前进到下一空槽）。
    /// 效果设置栏已移除（2026-09-21）——效果级编排字段（速度/触发上限/持续/选择模式/数量/落区）
    /// 移入每个原子卡/主干卡的展开区编辑（整个效果共用）。
    ///
    /// 组合三态（用户定案，InferMode 推断防互串）：
    ///   并列     = steps 里 1-3 个 kind=0 原子（无时序选项——全部按现行逐一结算，引擎零改动）；
    ///   自由分支 = steps 空 + header.EngineKind/EngineParam（主干=拼点/运势/倒计时表行）+ AtomicEffects[0]=奖励；
    ///   有限分支 = steps=[kind=0 主干, kind=1 分支（conditionId ∈ OutcomeGates，thenSteps=奖励单原子）]，
    ///              门文本带【奖励x】，奖励推导费 ≤ x（添加时校验）。
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
    /// 有限分支条件目录用代码侧 ComposerCatalog；Drawbacks 已随减费归入代价体系退役（2026-09-16）。
    ///
    /// 2026-09-22 六项修订：
    ///   ① 效果表模式对齐原子库三件套——共用右栏筛选（效果分类=内含原子名并集）+ 近似搜索（名∪id∪简称）+ 行色点；
    ///   ② 锁定关键词移出原子库（不可组合），在关键词区"不可编辑"分组固定展示——攻击/守卫（生物卡默认携带）
    ///      与拼点/运势/倒计时（固定引擎机制）；自由分支主干随之改为**主干槽内下拉直选**（不再经库行点击落槽）；
    ///   ③ 效果表行单行化：去全部简化描述，只留 名+id 一行（.single-line 省略号截断）；
    ///   ④ 代价槽纳入槽位选中制——**任何可挂载原子都能入代价**（2026-09-22 二轮语义修订：真正的区别是作用单位）：
    ///      填入时作用域自动改写错侧（有益→对手 / 有害→己方 / 中性须表级单侧锁），形成 &gt;1 费拦截
    ///     （口径同装载期 WrongSide/SideLock/PayloadUnitGrant）；代价卡内联编辑锁定作用域并实时显示全价。
    ///   ⑤ 目标域/效果级"持续"下拉全汉化（原为枚举英文名）；
    ///   ⑥ 下拉弹窗限高 300px（触发时机 27 项约见一半，纵向滚动；USS .unity-generic-menu）。
    ///
    /// 2026-09-22 五轮（可用性收口）：
    ///   ① 目标域下拉+目标随机合并一行；选项按 Polarity×槽位限选（效果区 p&gt;0 只己方侧/p&lt;0 只对方侧，
    ///      代价区只错侧成员——与装载期内容契约同源）；改动即重渲——{target} 按实例域渲染（AtomText.TargetNoun）；
    ///   ② 关键词恒为**他人赋予形态**（kinds={1,2}+Single+UntilEndOfTurn，"自己"两态已删）；可落并列/奖励槽
    ///      （Grant 行位 4 ∩ 门预算）；库中 Grant 行点击同口径（EntryForEffectSlot 归一）；
    ///   ③ 库/关键词面板按**当前选中槽过滤可用项**（CurrentSlotPredicate——与分类/搜索取交集，
    ///      库顶上下文标签显示过滤口径），玩家不再逐个试错。
    /// </summary>
    public sealed class EffectComposerScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/EffectComposer";

        // ======================================== 组合形态 ========================================

        /// <summary>组合三态（UI/存读共用推断，防三种形态互串）。</summary>
        public enum ComposeMode { Parallel, FreeBranch, OutcomeGate, Aura }

        /// <summary>右栏双模式：原子库（点击装配）/ 效果表（点击载入编辑）。</summary>
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

        // 槽位选中（2026-09-21 交互重做）：右侧点击=替换选中槽；默认原子1（首个槽）
        private SelKind _selSlot = SelKind.Parallel;
        private int _selSlotIndex = 0;

        // 卡编辑会话（null=效果库模式——保存到效果库文件）
        private CardData _editingCard;
        private int _editingIndex = -1;

        // 从效果表载入的源效果 id（编辑换内容=换 id——保存后清旧档防重复；null=非效果表载入）
        private string _loadedFromLibraryId;

        // ---- UI 引用 ----
        private ScrollView _slotArea, _libraryList, _keywordList, _effectsList;
        private VisualElement _modeBar, _atomPanel, _costZone;
        private Label _toast, _nameLabel, _costLabel, _activationHint, _timingLabel, _libContext, _kwHeader;
        private TextField _filterName;
        private DropdownField _timingDropdown, _activationDropdown, _filterTypeDropdown;
        private Button _reloadAtomsBtn, _loadEffectsBtn;
        private List<TriggerTiming> _timings;

        // 筛选状态（2026-09-22 筛选栏两模式共用）：原子库=表行中文名；效果表=内含原子名。
        // 近似搜索：原子库=中文名∪描述模板；效果表=名∪id∪组成简称。颜色筛选已删——圆点直读表色
        private string _filterTypeZh; // null = 全部
        private List<string> _filterTypeChoices = new List<string> { "全部" };

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
            ("选1·单范围", (int)CardCore.SelectionMode.Single),
            ("选N·单范围", (int)CardCore.SelectionMode.Multiple),
            ("全取·单范围", (int)CardCore.SelectionMode.Whole),
            ("选1·多范围", (int)CardCore.SelectionMode.SingleUnion),
            ("选N·多范围", (int)CardCore.SelectionMode.MultipleUnion),
            ("全取·多范围", (int)CardCore.SelectionMode.WholeUnion),
        };

        // 目标域中文（2026-09-22 五轮公共化）：移至 AtomText.TargetKindZhMap（与 {target} 渲染单一来源）

        // 效果级持续档中文（2026-09-22 汉化；按值映射，ForTurns 仅存于指示物时钟不提供）
        private static readonly (string label, int value)[] DurationChoices =
        {
            ("一次性", (int)DurationType.Once),
            ("永久", (int)DurationType.Permanent),
            ("到回合结束", (int)DurationType.UntilEndOfTurn),
            ("到对手回合结束", (int)DurationType.UntilNextTurn),
            ("离场清除", (int)DurationType.UntilLeaveBattlefield),
            ("条件持续", (int)DurationType.WhileCondition),
        };

        /// <summary>固定战斗原子（2026-09-22）：攻击/守卫=生物卡默认携带的主动能力
        ///（README L2 底盘预算：免费额度覆盖 攻(1)+守(1)，NoAttack/NoGuard opt-out）——
        /// 移出原子库不可组合，在关键词区"不可编辑"分组固定展示。</summary>
        private static bool IsFixedBattleAtom(string enumName)
            => enumName == "Attack" || enumName == "Guard";

        // 固定引擎主干（2026-09-22）：拼点/运势/倒计时/死亡计数/元素充盈/手牌序位=不可修改关键词——移出原子库，
        // 自由分支主干在主干槽内下拉直选（表行中文名经 TrunkZh 取表 Display 名）
        private static readonly BranchEngineKind[] TrunkEngines =
        {
            BranchEngineKind.None, BranchEngineKind.Clash, BranchEngineKind.LuckRoll, BranchEngineKind.Countdown,
            BranchEngineKind.DeathToll, BranchEngineKind.ManaSurplus, BranchEngineKind.NthHandCard,
        };

        private static bool IsEngineTrunkRow(AtomicEffectConfig r)
            => r != null && Enum.TryParse<AtomicEffectType>(r.EnumName, out var t) && ComposerCatalog.IsEngineTrunk(t);

        /// <summary>锁定关键词行（移出原子库、不可组合）：攻击/守卫 + 拼点/运势/倒计时。</summary>
        private static bool IsLockedKeywordRow(AtomicEffectConfig r)
            => r != null && (IsFixedBattleAtom(r.EnumName) || IsEngineTrunkRow(r));

        /// <summary>目标域可选集（2026-09-22 五轮）：按 Polarity×槽位过滤表域——
        /// 效果区：p&gt;0 只己方侧 / p&lt;0 只对方侧 / p=0 不限（"表默认"=双侧域由调用方另附，合法）；
        /// 代价区：只错侧（p&gt;0→对方侧 / p&lt;0→己方侧 / p=0→表级单侧锁的那侧）。
        /// 空=无可选（不显示下拉）。口径与装载期 WrongSide/SideLock 内容契约同源。</summary>
        private static List<int> AllowedTargetKinds(AtomicEffectConfig cfg, List<int> tableKinds, bool forCost)
        {
            float p = UnityEngine.Mathf.Clamp(cfg?.Polarity ?? 0f, -1f, 1f);
            if (forCost)
            {
                if (p != 0f)
                {
                    bool wantEnemy = p > 0f;
                    return tableKinds.Where(k => TargetKindRules.IsEnemySide(k) == wantEnemy).ToList();
                }
                return CostDerivationService.SideLock(tableKinds) != 0 ? tableKinds : new List<int>();
            }
            if (p == 0f) return tableKinds;
            bool ownOnly = p > 0f; // 有益=效果区只己方侧；有害=只对方侧
            return tableKinds.Where(k => TargetKindRules.IsEnemySide(k) != ownOnly).ToList();
        }

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
            _libContext = Q<Label>("lbl-lib-context");
            _kwHeader = Q<Label>("lbl-keyword-header");
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
            _mode = InferMode(_graph);
            EnsureDefaultSelection();
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
            var cfg = AtomicEffectTable.GetByHashId(entry.refId);
            if (cfg == null) return entry.refId;
            string name = cfg.DisplayName ?? entry.refId;
            // 关键词原子两态命名（2026-09-21）：赋予他人=主动效果——「赋予xx」；自己=关键词本体名
            bool toOthers = entry.kinds != null && entry.kinds.Count > 0
                && !(entry.kinds.Count == 1 && entry.kinds[0] == (int)TargetKind.Self);
            if (toOthers && MountKindExtensions.ParseCsv(cfg.MountKinds ?? "").Contains(MountKind.Keyword))
                return "赋予" + name;
            return name;
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
            _modeBar.Add(MakeModeChip("光环（连接箭头）", ComposeMode.Aura));

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
                    h.EngineKind = (int)BranchEngineKind.None; // 待选主干
                    h.EngineParam = 0;
                    h.AtomicEffects = seed != null ? new List<AtomicEffectEntry> { seed } : new List<AtomicEffectEntry>();
                    ShowToast("切换为自由分支——主干在主干槽下拉选择（拼点/运势/倒计时），奖励点击库行填入");
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
                case ComposeMode.Aura:
                {
                    // 光环（2026-09-22 定案）＝卡面字段（LinkAuras+箭头），不是效果——仅在卡编辑会话可用；
                    // 作用对象不可指定（运行时 live-query 箭头指向格占据者）；效果数据清空防误存
                    if (_editingCard == null)
                    {
                        ShowToast("光环是卡面字段（LinkAuras+箭头）——仅卡编辑会话可用（从卡组成界面进入效果编辑）");
                        return;
                    }
                    h.EngineKind = (int)BranchEngineKind.None;
                    h.EngineParam = 0;
                    h.AtomicEffects = null;
                    _graph.steps.Clear();
                    ShowToast("光环模式：编辑卡面箭头与光环条目（作用对象=箭头指向格占据者，不可指定）；保存不改动效果槽");
                    break;
                }
            }

            _mode = newMode;
            _sel = SelKind.None;
            _selIndex = -1;
            EnsureDefaultSelection();
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
            ClampSelection();
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
                        MarkSelected(slot, SelKind.Parallel, i);
                        // 越过空档的槽不可选（保持无空档填充）
                        SelectOnClick(slot, SelKind.Parallel, i, i > _graph.steps.Count);
                        if (i < _graph.steps.Count)
                        {
                            var step = _graph.steps[i];
                            if (step.kind == 0) slot.Add(MakeAtomCard(step.atomic, idx, SelKind.Parallel));
                            else slot.Add(MakeReadonlyStepBadge(step)); // 抉择等只读
                        }
                        else if (i == _graph.steps.Count)
                        {
                            slot.Add(MakeHint("空槽——选中后点击右侧原子库填入"));
                        }
                        else slot.Add(MakeHint("（先填前面的槽）"));
                        _slotArea.Add(slot);
                    }
                    if (_graph.steps.Count >= 3)
                        _slotArea.Add(MakeHint("已满 3 原子——点击槽选中后，右侧点击即替换"));
                    break;
                }
                case ComposeMode.FreeBranch:
                {
                    // 主干槽（引擎条件·2026-09-22 改下拉直选）：拼点/运势/倒计时=固定机制关键词，
                    // 已移出原子库——不再经库行点击落槽，主干类型由槽内下拉直接选择
                    var trunkSlot = MakeSlot("主干（条件引擎）", "drop-slot", "drop-slot--trunk");
                    var trunkLabels = TrunkEngines
                        .Select(e => e == BranchEngineKind.None ? "（未选择）" : TrunkZh(e)).ToList();
                    var trunkDd = new DropdownField("主干类型") { choices = trunkLabels };
                    trunkDd.AddToClassList("text-input");
                    int tcur = Array.IndexOf(TrunkEngines, (BranchEngineKind)h.EngineKind);
                    trunkDd.index = tcur >= 0 ? tcur : 0;
                    trunkDd.RegisterValueChangedCallback(_ =>
                    {
                        var engine = TrunkEngines[Mathf.Max(0, trunkDd.index)];
                        h.EngineKind = (int)engine;
                        h.EngineParam = engine == BranchEngineKind.None ? 0 : 1; // 倒计时 x=1（可改 0=自动换算）；拼点/运势默认 x=1
                        _sel = SelKind.FreeTrunk; // 展开主干卡便于调参
                        // 主干已定——自动前进到奖励槽（若空）
                        if (engine == BranchEngineKind.None || h.AtomicEffects == null || h.AtomicEffects.Count == 0)
                            _selSlot = SelKind.FreeReward;
                        RefreshSlots();
                        RefreshName();
                        RefreshRightPanels(); // 选中可能切到奖励槽——库重过滤（2026-09-22 五轮）
                    });
                    trunkSlot.Add(trunkDd);
                    if (h.EngineKind != (int)BranchEngineKind.None)
                    {
                        trunkSlot.Add(MakeTrunkCard((BranchEngineKind)h.EngineKind, h.EngineParam));
                    }
                    else trunkSlot.Add(MakeHint("从上方下拉选择引擎（拼点/运势/倒计时/死亡计数/元素充盈/手牌序位）；奖励在下方奖励槽（点击库行填入）"));
                    _slotArea.Add(trunkSlot);

                    var arrow = new Label("条件达成 →");
                    arrow.AddToClassList("hint");
                    arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
                    _slotArea.Add(arrow);

                    // 奖励槽（单原子；死亡计数/元素充盈带预算=x——2026-09-22 奖励预算制）
                    var rewardSlot = MakeSlot("奖励（单原子）", "drop-slot", "drop-slot--reward");
                    MarkSelected(rewardSlot, SelKind.FreeReward, 0);
                    SelectOnClick(rewardSlot, SelKind.FreeReward, 0, false);
                    var reward = h.AtomicEffects?.FirstOrDefault();
                    if (reward != null) rewardSlot.Add(MakeAtomCard(reward, 0, SelKind.FreeReward));
                    else rewardSlot.Add(MakeHint("选中后点击库中的奖励原子填入（须开放分支奖励挂载）"));
                    _slotArea.Add(rewardSlot);

                    if (ComposerCatalog.EngineRewardBudget((BranchEngineKind)h.EngineKind, h.EngineParam) > 0)
                    {
                        var bl = new Label(FreeRewardBudgetText());
                        bl.AddToClassList("hint");
                        _slotArea.Add(bl);
                    }
                    break;
                }
                case ComposeMode.Aura:
                {
                    if (_editingCard == null)
                    {
                        _slotArea.Add(MakeHint("光环模式需要卡编辑会话（从卡组成界面进入效果编辑）——光环是卡面字段（LinkAuras+箭头）"));
                        break;
                    }
                    _slotArea.Add(MakeAuraPanel());
                    break;
                }
                case ComposeMode.OutcomeGate:
                {
                    EnsureGateShape();
                    var trunk = _graph.steps[0];
                    var branch = _graph.steps[1];

                    // 主干槽（产出族原子）
                    var trunkSlot = MakeSlot("主干（产出原子）", "drop-slot", "drop-slot--trunk");
                    MarkSelected(trunkSlot, SelKind.GateTrunk, 0);
                    SelectOnClick(trunkSlot, SelKind.GateTrunk, 0, false);
                    trunkSlot.Add(MakeAtomCard(trunk.atomic, 0, SelKind.GateTrunk));
                    _slotArea.Add(trunkSlot);

                    // 门行：条件下拉（中文+【奖励x】）内联——不再走检查器
                    _slotArea.Add(MakeGateRow(branch));

                    // 改写门（2026-09-22 拦截式）：伤害不发生改为施加指示物——无奖励槽、无预算行
                    if (CardCore.BranchConditionEvaluator.IsRewriteCondition(branch.conditionId))
                    {
                        _slotArea.Add(MakeHint("改写门：该伤害原子的结算改为对目标施加对应指示物（固定 1 层，伤害不发生）——无奖励槽；差价自动计入卡费"));
                        break;
                    }

                    // 奖励槽（单原子 + 预算）
                    var rewardSlot = MakeSlot("奖励（单原子·预算内）", "drop-slot", "drop-slot--reward");
                    MarkSelected(rewardSlot, SelKind.GateReward, 0);
                    SelectOnClick(rewardSlot, SelKind.GateReward, 0, false);
                    var reward = branch.thenSteps?.FirstOrDefault();
                    if (reward != null) rewardSlot.Add(MakeAtomCard(reward, 0, SelKind.GateReward));
                    else rewardSlot.Add(MakeHint("选中后点击库中的奖励原子填入（推导费 ≤ 门预算）"));
                    _slotArea.Add(rewardSlot);

                    _slotArea.Add(MakeGateBudgetLine(branch));
                    break;
                }
            }

            RefreshName();
        }

        // ======================================== 槽位选中（右侧点击=替换选中槽） ========================================

        private void MarkSelected(VisualElement slot, SelKind kind, int index)
        {
            if (_selSlot == kind && _selSlotIndex == index)
                slot.AddToClassList("drop-slot--selected");
        }

        /// <summary>槽点击=选中（整槽可点；卡片摘要点击会冒泡到槽——选中和展开并存）。</summary>
        private void SelectOnClick(VisualElement slot, SelKind kind, int index, bool disabled)
        {
            slot.RegisterCallback<ClickEvent>(_ =>
            {
                if (disabled) { ShowToast("请按顺序填入——先选前面的槽"); return; }
                if (_selSlot == kind && _selSlotIndex == index) return;
                SelectSlot(kind, index);
            });
        }

        private void SelectSlot(SelKind kind, int index)
        {
            _selSlot = kind;
            _selSlotIndex = index;
            RefreshSlots();
            RefreshCostZone(); // 代价槽在独立区（2026-09-22 纳入选中制）——选中态需随切槽刷新
            RefreshRightPanels(); // 库/关键词按选中槽重过滤（2026-09-22 五轮）
        }

        /// <summary>右栏两列表刷新（2026-09-22 五轮）：选中槽/门变化后按可用性重过滤。</summary>
        private void RefreshRightPanels()
        {
            RefreshLibrary();
            RefreshKeywords();
        }

        /// <summary>进入/切模式/载入时的默认选中：并列=原子1；自由分支=奖励槽（主干已改下拉直选，
        /// 库行点击唯一落点=奖励）；有限分支=主干。</summary>
        private void EnsureDefaultSelection()
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                    _selSlot = SelKind.Parallel;
                    _selSlotIndex = 0;
                    break;
                case ComposeMode.FreeBranch:
                    _selSlot = SelKind.FreeReward; // 主干=下拉直选（2026-09-22）——库行点击唯一落点=奖励槽
                    _selSlotIndex = 0;
                    break;
                case ComposeMode.OutcomeGate:
                    _selSlot = SelKind.GateTrunk;
                    break;
            }
        }

        /// <summary>每次刷新前校正当选中槽（模式变化/删除原子后防悬挂）；并列槽允许 0..min(已填数,2)。
        /// 代价槽（2026-09-22 纳入选中制）在任何模式下都合法——不随模式重置。</summary>
        private void ClampSelection()
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                    int max = Mathf.Min(_graph.steps?.Count ?? 0, 2);
                    if (_selSlot != SelKind.Parallel && _selSlot != SelKind.Cost)
                    {
                        _selSlot = SelKind.Parallel;
                        _selSlotIndex = 0;
                    }
                    else if (_selSlot == SelKind.Parallel && _selSlotIndex > max) _selSlotIndex = max;
                    break;
                case ComposeMode.FreeBranch:
                    if (_selSlot != SelKind.FreeReward && _selSlot != SelKind.Cost)
                    {
                        _selSlot = SelKind.FreeReward; // 主干已改下拉直选——选中槽只余奖励
                        _selSlotIndex = 0;
                    }
                    break;
                case ComposeMode.OutcomeGate:
                    if (_selSlot != SelKind.GateTrunk && _selSlot != SelKind.GateReward && _selSlot != SelKind.Cost)
                        _selSlot = SelKind.GateTrunk;
                    break;
            }
        }

        // ======================================== 代价区（错边原子槽，位于效果编辑上方） ========================================

        /// <summary>当前代价栏的 Payload 原子（无则 null）。</summary>
        private AtomicEffectEntry PayloadCostEntry()
            => _graph.header.Costs?.FirstOrDefault(c => c != null
               && (CostType)c.CostType == CostType.Payload && c.payload != null)?.payload;

        private void RefreshCostZone()
        {
            _costZone.Clear();
            var header = new Label("代价（错侧作用·限 1 费）——任何原子都可作代价：有益→作用于对手 / 有害→作用于己方（付费步强制执行·全价补偿黑/白）");
            header.AddToClassList("panel__header");
            _costZone.Add(header);

            // 2026-09-22 纳入槽位选中制：点击可选中（FillCostSlot 负责错侧改写+1 费限价）
            var slot = MakeSlot("代价槽（点击选中）", "drop-slot", "drop-slot--reward");
            MarkSelected(slot, SelKind.Cost, 0);
            SelectOnClick(slot, SelKind.Cost, 0, false);
            var payload = PayloadCostEntry();
            if (payload != null) slot.Add(MakeAtomCard(payload, 0, SelKind.Cost));
            else slot.Add(MakeHint("点击选中后，点击库中的任意原子填入——作用域自动改写到错侧（有益→对手 / 有害→己方）；形成 &gt;1 费的会被拦截"));
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

            // ---- 展开态：内联编辑（原检查器的 MountKinds 驱动面）----
            if (expanded)
                card.Add(MakeAtomEditorInline(atom, selKind, text, expanded));

            return card;
        }

        /// <summary>原子内联编辑器（检查器移除后的替代——挂在卡片展开区）。
        /// headLabel=卡片摘要行（值/随机改动原地重渲，不重建槽区——2026-09-21 修复滑条拖动中失焦）。</summary>
        private VisualElement MakeAtomEditorInline(AtomicEffectEntry atom, SelKind selKind, Label headLabel, bool expanded)
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

            // 代价全价实时行（2026-09-22 二轮）：限 1 费——值/随机改动随 RefreshTexts 刷新
            Label priceLine = null;
            if (selKind == SelKind.Cost)
            {
                priceLine = new Label(PayloadPriceText(atom));
                priceLine.AddToClassList("hint");
                box.Add(priceLine);
            }

            // 原地刷新（2026-09-21）：值/随机改动只更新描述/摘要/顶栏——不再 RefreshSlots 整区重建
            // （重建会销毁拖动中的滑条 → 拖动提前"失去选中"；顺带修复数值输入框逐字符失焦）
            void RefreshTexts()
            {
                desc.text = AtomText.Render(cfg, atom, _graph.header);
                if (headLabel != null)
                    headLabel.text = (expanded ? "▼ " : "▶ ") + AtomText.RenderAtomEntry(atom);
                if (priceLine != null) priceLine.text = PayloadPriceText(atom);
                RefreshName();
            }

            // ---- 关键词类原子（MountKinds 含 Keyword 位）：系数全部固定于代码——只编辑「赋予持续」。
            //      2026-09-22 五轮用户拍板：**只要他人赋予形态**（效果即赋予一个生物该关键词）——
            //      "自己（永久）"本体两态已删（卡面自带关键词走卡牌关键词字段，不经效果合成）。
            //      代价位不出现关键词（KeywordRewardMountable/CanBeCost 均排除）。
            if (mounts.Contains(MountKind.Keyword) && selKind != SelKind.Cost)
            {
                if (selKind == SelKind.GateReward && _graph.steps.Count > 1)
                    box.Add(MakeHint(GateBudgetText(_graph.steps[1])));

                // 赋予持续三档（2026-09-21 收缩：1/2 回合同档=持续到自己回合结束）
                var durChoices = new List<string> { "持续到自己回合结束", "离场清除", "永久" };
                var durVals = new List<int>
                {
                    (int)DurationType.UntilEndOfTurn,
                    (int)DurationType.UntilLeaveBattlefield,
                    (int)DurationType.Permanent,
                };
                var durDd = new DropdownField("赋予持续") { choices = durChoices };
                durDd.AddToClassList("text-input");
                int dcur = durVals.IndexOf(_graph.header.Duration);
                durDd.index = dcur >= 0 ? dcur : 0;
                durDd.RegisterValueChangedCallback(_ =>
                {
                    _graph.header.Duration = durVals[Mathf.Max(0, durDd.index)];
                    RefreshTexts(); // 持续档影响计价折扣——费用随改随刷
                });
                box.Add(durDd);

                box.Add(MakeHint("关键词=赋予一个生物该关键词（弹窗选一，默认持续到自己回合结束，可改离场清除/永久）；卡面自带关键词不经效果合成"));
                return box; // 数值/随机/目标域/效果设置等编辑项一律不显示
            }

            // Value（原子唯一可编辑数值；无 {value} 模板则只读说明）
            if (hasValue)
            {
                var v = new IntegerField("数值") { value = atom.value };
                v.AddToClassList("int-input");
                v.RegisterValueChangedCallback(e =>
                {
                    atom.value = e.newValue;
                    RefreshTexts();
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
                    RefreshTexts();
                });
                box.Add(slider);
                box.Add(MakeHint("计价按名义数值——随机只影响结算分布（锚点不漂移）"));
            }

            // 目标域（2026-09-22 五轮重做）：下拉+目标随机合并**一行**；选项按 Polarity×槽位限选；
            // 改动即 RefreshTexts——{target} 按实例域渲染，描述实时反映作用对象。
            // 限选口径与装载期内容契约同源：效果区禁错侧收窄（p>0 只己方侧 / p<0 只对方侧 / p=0 不限，
            // "表默认"=双侧域合法保留）；代价区只列错侧成员（无表默认——任何选择都不破坏错侧改写）。
            var tableKinds = cfg?.GetTargetKindList() ?? new List<int>();
            var allowedKinds = AllowedTargetKinds(cfg, tableKinds, selKind == SelKind.Cost);
            if (allowedKinds.Count > 0)
            {
                var row = new VisualElement(); row.AddToClassList("toolbar");
                bool offerDefault = selKind != SelKind.Cost; // 表默认（双侧域）效果区合法；代价区只错侧
                var labels = new List<string>();
                if (offerDefault) labels.Add("表默认");
                labels.AddRange(allowedKinds.Select(k => AtomText.TargetKindZhOf((TargetKind)k)));

                var dd = new DropdownField("目标域") { choices = labels };
                dd.AddToClassList("text-input");
                int offset = offerDefault ? 1 : 0;
                int cur = atom.kinds != null && atom.kinds.Count == 1
                    ? offset + allowedKinds.IndexOf(atom.kinds[0]) : 0;
                dd.index = Mathf.Max(0, cur);
                dd.RegisterValueChangedCallback(_ =>
                {
                    int i = dd.index - offset;
                    atom.kinds = i < 0 ? null : new List<int> { allowedKinds[i] };
                    RefreshTexts(); // {target} 随实例域变化——描述/摘要原地重渲
                });
                row.Add(dd);

                // 目标随机（不弹窗·按种子随机选）——与目标域同一行；代价区无选择窗口不参与
                if (selKind != SelKind.Cost)
                {
                    var rand = new Toggle("目标随机") { value = _graph.header.RandomTarget != 0 };
                    rand.RegisterValueChangedCallback(e =>
                    {
                        // 2026-09-16 随机移出枚举为正交标志——只翻标志，不再覆写选择模式
                        _graph.header.RandomTarget = e.newValue ? 1 : 0;
                        RefreshTexts();
                    });
                    row.Add(rand);
                }
                box.Add(row);
            }

            // 位 2：指示物持续提示
            if (mounts.Contains(MountKind.Counter))
                box.Add(MakeHint("指示物原子：持续规则由指示物本身承载——效果级持续档仅供参考"));

            // 位 8：触发上限锁定
            if (mounts.Contains(MountKind.TriggerCapImmutable))
                box.Add(MakeHint("触发上限锁定：该原子恒无限（TriggerLimitPerTurn 被覆写，不可限）"));

            // DrawCard：抽牌减费缺陷已随减费归入代价体系退役（2026-09-16）——减负表达走代价栏 Payload 原子
            if (type == AtomicEffectType.SearchDeck)
            {
                box.Add(MakeHint("检索按维度档计费：字符串字段填宣言卡名（ExactCard=3），空=单维度 1"));
            }

            // 代价位说明（2026-09-22 五轮）：目标域下拉已恢复——只列错侧成员，选哪个都在契约内
            if (selKind == SelKind.Cost)
                box.Add(MakeHint("代价原子：目标域只列错侧（有益→对手 / 有害→己方 / 中性=表级单侧锁）；全价限 1 费"));

            // 有限分支奖励：预算行
            if (selKind == SelKind.GateReward && _graph.steps.Count > 1)
                box.Add(MakeHint(GateBudgetText(_graph.steps[1])));

            // 效果设置内联（2026-09-21 左栏移除）——效果级共用字段随卡展开编辑（代价槽除外）
            if (selKind != SelKind.Cost)
                box.Add(MakeEffectSettingsBox());

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
                ComposerCatalog.EngineParamRange(engine, out int pMin, out int pMax);
                string label = engine == BranchEngineKind.Countdown ? "回合数（0=自动换算）" : $"参数 x（{pMin}-{pMax}）";
                var field = new IntegerField(label) { value = _graph.header.EngineParam };
                field.AddToClassList("int-input");
                field.RegisterValueChangedCallback(e =>
                {
                    _graph.header.EngineParam = engine == BranchEngineKind.Countdown
                        ? Mathf.Max(0, e.newValue)
                        : Mathf.Clamp(e.newValue, pMin, pMax);
                    field.SetValueWithoutNotify(_graph.header.EngineParam);
                    RefreshSlots();
                });
                box.Add(field);
                box.Add(MakeHint("主干只是条件——奖励在下方奖励槽（单原子，不占卡费）。"));
                box.Add(MakeEffectSettingsBox());
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
                        if (CardCore.BranchConditionEvaluator.IsRewriteCondition(branch.conditionId))
                            branch.thenSteps?.Clear(); // 改写门无奖励槽——切换即清空遗留奖励
                        RefreshSlots(); // 预算行/奖励槽随门刷新
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
            if (CardCore.BranchConditionEvaluator.IsRewriteCondition(branch?.conditionId))
                return "改写门无奖励槽（伤害不发生，改为施加指示物；差价自动入卡费）";
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

        /// <summary>自由分支奖励预算文本（2026-09-22 奖励预算制）：死亡计数/元素充盈 预算=x；
        /// 既有三引擎自平衡（拼点门槛/倒计时回合/运势概率）无上限。</summary>
        private string FreeRewardBudgetText()
        {
            var h = _graph.header;
            int budget = ComposerCatalog.EngineRewardBudget((BranchEngineKind)h.EngineKind, h.EngineParam);
            var reward = h.AtomicEffects?.FirstOrDefault();
            float cost = reward != null
                ? CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { CardEffectConverter.ConvertAtomForUI(reward) })
                : 0f;
            string state = cost > budget ? $"（超出 {cost - budget:0.#}——保存前请调整）" : "";
            return $"【奖励预算 {budget}】当前 {cost:0.#}/{budget}{state}";
        }

        /// <summary>自由分支奖励预算校验（无预算引擎恒过）。</summary>
        private bool FreeRewardWithinBudget(AtomicEffectEntry entry)
        {
            var h = _graph.header;
            int budget = ComposerCatalog.EngineRewardBudget((BranchEngineKind)h.EngineKind, h.EngineParam);
            if (budget < 0) return true;
            var inst = CardEffectConverter.ConvertAtomForUI(entry);
            if (inst == null) return false;
            return CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { inst }) <= budget;
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
            if (kind == SelKind.Cost) RefreshCostZone(); // 代价卡在独立区——展开态需单独刷新
        }

        // ---- 并列槽操作 ----
        private void MoveParallel(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= _graph.steps.Count || target < 0 || target >= _graph.steps.Count) return;
            (_graph.steps[index], _graph.steps[target]) = (_graph.steps[target], _graph.steps[index]);
            if (_sel == SelKind.Parallel && _selIndex == index) _selIndex = target;
            else if (_sel == SelKind.Parallel && _selIndex == target) _selIndex = index;
            if (_selSlot == SelKind.Parallel && _selSlotIndex == index) _selSlotIndex = target;
            else if (_selSlot == SelKind.Parallel && _selSlotIndex == target) _selSlotIndex = index;
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
                    _sel = SelKind.None; // 主干不可删（模式数据形依赖）——换原子用点击覆盖
                    ShowToast("主干槽请点击新原子覆盖");
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

        // ---- 落槽资格与写入（2026-09-21：点击替换选中槽） ----

        // 并列槽可落：主动位原子（MountKinds 含 0）且**非错边**（内容契约：效果区禁错边——错边只能进代价槽）
        // 或关键词条目（关键词=默认 Self 的授予原子）
        private bool ParallelCanDrop(object payload)
            => (payload is LibPayload lp && lp.CanBeActiveAtom && !lp.IsWrongSideOnly) || payload is KeywordCatalogEntry;

        private bool RewardCanDrop(object payload)
            => payload is LibPayload lp && lp.CanBeBranchReward && !lp.IsWrongSideOnly;

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
            // 主干已定——自动前进到奖励槽（若空）
            if (branch.thenSteps == null || branch.thenSteps.Count == 0)
                _selSlot = SelKind.GateReward;
            RefreshSlots();
            RefreshRightPanels(); // 主干/门变化——库按新门预算重过滤（2026-09-22 五轮）
        }

        private void DropGateReward(LibPayload lp)
        {
            _graph.steps[1].thenSteps = new List<AtomicEffectEntry> { EntryForEffectSlot(lp) };
            if (lp.IsKeywordAtom) ApplyKeywordHeader(); // 关键词=他人赋予形态（2026-09-22 五轮）
            _sel = SelKind.GateReward;
            _selIndex = 0;
            _selSlot = SelKind.GateReward;
            RefreshSlots();
        }

        // ======================================== 效果设置（2026-09-21 左栏移除——随卡片展开区内联） ========================================

        /// <summary>效果级编排字段（速度/触发上限/持续/选择模式/数量/落区）——整个效果共用，
        /// 在每个原子卡/主干卡的展开区编辑（写入同一 header）。</summary>
        private VisualElement MakeEffectSettingsBox()
        {
            var box = new VisualElement();
            box.AddToClassList("sub-item");
            var h = _graph.header;

            var title = new Label("效果设置（整个效果共用）");
            title.AddToClassList("panel__header");
            box.Add(title);

            var row0 = new VisualElement(); row0.AddToClassList("toolbar");
            row0.Add(MakeIntField("速度", h.BaseSpeed, v => h.BaseSpeed = v));
            row0.Add(MakeIntField("触发上限(0默认/1一回合一次/-1无限)", h.TriggerLimitPerTurn, v => h.TriggerLimitPerTurn = v));
            box.Add(row0);

            var row1 = new VisualElement(); row1.AddToClassList("toolbar");
            // 持续档全中文（2026-09-22 汉化；按值映射——ForTurns 仅存于指示物时钟，效果级不提供）
            var dur = new DropdownField("持续") { choices = DurationChoices.Select(d => d.label).ToList() };
            dur.AddToClassList("text-input");
            int dcur = Array.FindIndex(DurationChoices, d => d.value == h.Duration);
            dur.index = dcur >= 0 ? dcur : 0;
            dur.RegisterValueChangedCallback(_ => h.Duration = DurationChoices[Mathf.Max(0, dur.index)].value);
            row1.Add(dur);

            var modeChoices = SelectionModes.Select(m => m.label).ToList();
            var sel = new DropdownField("选择模式") { choices = modeChoices };
            sel.AddToClassList("text-input");
            int selIdx = Mathf.Max(0, SelectionModes.ToList().FindIndex(m => m.value == h.SelectionMode));
            sel.index = selIdx;
            sel.RegisterValueChangedCallback(_ => h.SelectionMode = SelectionModes[sel.index].value);
            row1.Add(sel);
            box.Add(row1);

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
            box.Add(row2);

            // 每个可选项的说明（用户定案：效果编辑的选项前置介绍——选项是什么/怎么选）
            box.Add(MakeHint("速度：0=只能自己回合的主阶段发动；1=瞬间基准（对手回合也能发动/响应）"));
            box.Add(MakeHint("触发上限：触发式每回合次数——0=默认（一回合一次）；N=每回合 N 次；-1=无限"));
            box.Add(MakeHint("持续：一次性效果选「一次性」；永久持续选「永久」；其余为限时/条件档"));
            box.Add(MakeHint("选择模式：目标怎么选——无目标=不弹窗；单/多范围=弹窗选；全取=不弹；多范围=多域并集"));
            box.Add(MakeHint("数量：固定选 N 个（0=全部、-1=任意）；动态数量=运行时自选个数（计费 0 且不可作地牌）"));
            box.Add(MakeHint("落区：衍生物（召唤）的生成位置——战场/手牌/牌库"));
            return box;
        }

        // ======================================== 右：双模式展示区 ========================================

        private void OnReloadAtoms()
        {
            AtomicEffectTable.Reload();
            SetRightMode(RightMode.Atoms); // 内含 RebuildFilterTypeChoices（重置"全部"）+ RefreshLibrary
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

            // 筛选栏两模式共用（2026-09-22）：分类选项随模式重建（原子库=表行中文名；效果表=内含原子名并集）
            RebuildFilterTypeChoices();

            // 按钮态指示（当前模式高亮）
            _reloadAtomsBtn.EnableInClassList("btn--primary", mode == RightMode.Atoms);
            _loadEffectsBtn.EnableInClassList("btn--primary", mode == RightMode.Effects);

            if (mode == RightMode.Effects) RefreshEffectsList();
            else RefreshLibrary();
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
            string q = _filterName.value;
            foreach (var fx in effects)
            {
                var captured = fx;
                var parts = EffectAtomCfgs(captured); // 组成部分表行（分类/色点/简称检索共用）

                // 效果分类（2026-09-22）：任一组成部分（主干/原子/奖励）的表行中文名命中即入选
                if (_filterTypeZh != null && !parts.Any(c => c?.DisplayName == _filterTypeZh)) continue;

                // 近似检索（2026-09-22）：名 ∪ id ∪ 组成简称（内含原子中文名并串）
                if (!string.IsNullOrEmpty(q))
                {
                    string abbr = string.Concat(parts.Select(c => c?.DisplayName ?? ""));
                    if (!(captured.name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                        && !(captured.id ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                        && !abbr.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var row = new VisualElement();
                row.AddToClassList("list-row");
                row.AddToClassList("library-row");

                // 色点（2026-09-22）：首个组成部分的表色——与原子库行同口径（圆点直读表色）
                var dot = new VisualElement();
                dot.AddToClassList("color-dot");
                dot.AddToClassList(ChipClass(ColorFilter.OfColorName(parts.FirstOrDefault()?.Tags)));
                row.Add(dot);

                // 单行（2026-09-22）：去全部简化描述——只留 名+id 一行（超宽省略号截断）
                var name = new Label(captured.name);
                name.AddToClassList("list-row__name");
                name.AddToClassList("single-line");
                row.Add(name);

                var id = new Label(captured.id);
                id.AddToClassList("list-row__meta");
                row.Add(id);

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
            EnsureDefaultSelection();
            SyncTimingDropdown();
            _activationDropdown.index = Mathf.Clamp(_graph.header.ActivationType, 0, ActivationNames.Length - 1);
            SyncActivationVisibility();
            RefreshAll();
            ShowToast($"已载入：{_graph.name}（编辑后保存覆盖）");
        }

        // ---- 筛选栏（两模式共用：分类下拉选项随模式重建） ----

        private void BuildFilterBar()
        {
            // 回调只注册一次（OnEnter 唯一调用；重读原子表只重建选项——防重复挂回调）
            _filterTypeDropdown.RegisterValueChangedCallback(_ =>
            {
                int i = _filterTypeDropdown.index;
                _filterTypeZh = i <= 0 ? null : _filterTypeChoices[Mathf.Max(0, i)];
                RefreshLibrary();
                if (_rightMode == RightMode.Effects) RefreshEffectsList();
            });

            // 近似搜索：原子库=中文名∪描述模板；效果表=名∪id∪组成简称（RefreshEffectsList 内判模式）
            _filterName.RegisterValueChangedCallback(_ =>
            {
                RefreshLibrary();
                RefreshKeywords();
                if (_rightMode == RightMode.Effects) RefreshEffectsList();
            });

            RebuildFilterTypeChoices();
        }

        /// <summary>分类选项随模式重建并重置为"全部"：原子库=表行中文名（锁定关键词行除外——
        /// 攻击/守卫/拼点/运势/倒计时 已移入关键词区固定展示）；效果表=库内效果的内含原子名并集
        ///（2026-09-22 效果表对齐原子库三件套）。</summary>
        private void RebuildFilterTypeChoices()
        {
            var types = new List<string> { "全部" };
            if (_rightMode == RightMode.Effects)
                types.AddRange(EffectCategoryNames());
            else
                types.AddRange(AllTableRows()
                    .Where(r => !IsLockedKeywordRow(r))
                    .Select(r => r.DisplayName).Where(n => !string.IsNullOrEmpty(n))
                    .Distinct().OrderBy(n => n, StringComparer.CurrentCulture));
            _filterTypeChoices = types;
            _filterTypeZh = null;
            _filterTypeDropdown.choices = types;
            _filterTypeDropdown.index = 0; // "全部"（值未变不触发回调，上面已直置 null）
        }

        /// <summary>效果分类名（效果表模式）：库内全部效果的内含原子中文名并集（引擎主干行计入）。</summary>
        private static IEnumerable<string> EffectCategoryNames()
        {
            var names = new HashSet<string>();
            foreach (var fx in EffectLibrarySerializer.LoadAll())
                foreach (var cfg in EffectAtomCfgs(fx))
                    if (!string.IsNullOrEmpty(cfg.DisplayName)) names.Add(cfg.DisplayName);
            return names.OrderBy(n => n, StringComparer.CurrentCulture);
        }

        /// <summary>效果的组成部分表行（分类/色点/简称检索共用）：引擎主干行 + 各步骤原子 + 分支奖励。</summary>
        private static List<AtomicEffectConfig> EffectAtomCfgs(EffectGraphData fx)
        {
            var list = new List<AtomicEffectConfig>();
            if (fx?.header == null) return list;
            var h = fx.header;
            if (h.EngineKind != (int)BranchEngineKind.None)
            {
                var trunkRow = AtomicEffectTable.GetByEnumName("BranchEngine" + (BranchEngineKind)h.EngineKind);
                if (trunkRow != null) list.Add(trunkRow);
                if (h.AtomicEffects != null)
                    foreach (var a in h.AtomicEffects) AddRefRow(list, a);
            }
            if (fx.steps != null)
            {
                foreach (var s in fx.steps)
                {
                    if (s == null) continue;
                    if (s.kind == 0) AddRefRow(list, s.atomic);
                    else if (s.kind == 1 && s.thenSteps != null)
                        foreach (var a in s.thenSteps) AddRefRow(list, a);
                }
            }
            return list;
        }

        private static void AddRefRow(List<AtomicEffectConfig> list, AtomicEffectEntry a)
        {
            var cfg = AtomicEffectTable.GetByHashId(a?.refId);
            if (cfg != null) list.Add(cfg);
        }

        private static IEnumerable<AtomicEffectConfig> AllTableRows()
            => AtomicEffectTable.GetAll().Where(r => r != null && !string.IsNullOrEmpty(r.EnumName));

        /// <summary>当前选中槽的可放置性谓词（2026-09-22 五轮：库只展示可用项——与分类/搜索取交集，
        /// 玩家不再逐个试错）。附带中文上下文（库顶标签显示）。</summary>
        private Func<LibPayload, bool> CurrentSlotPredicate(out string contextZh)
        {
            if (_selSlot == SelKind.Cost)
            {
                contextZh = "代价槽（错侧作用·限 1 费）";
                return lp => lp.CanBeCost;
            }
            switch (_mode)
            {
                case ComposeMode.Parallel:
                    contextZh = $"并列槽 {_selSlotIndex + 1}（主动原子·非错边）";
                    return lp => lp.CanBeActiveAtom && !lp.IsWrongSideOnly;
                case ComposeMode.FreeBranch:
                {
                    int budget = ComposerCatalog.EngineRewardBudget((BranchEngineKind)_graph.header.EngineKind, _graph.header.EngineParam);
                    contextZh = budget > 0
                        ? $"自由分支奖励（预算内·非错边——预算 {budget}）"
                        : "自由分支奖励（开放奖励挂载）";
                    return lp => lp.CanBeBranchReward && !lp.IsWrongSideOnly && FreeRewardWithinBudget(lp.Entry());
                }
                case ComposeMode.OutcomeGate:
                    if (_selSlot == SelKind.GateTrunk)
                    {
                        contextZh = "有限分支主干（产出族/通用门主干）";
                        return lp => lp.CanBeGateTrunk;
                    }
                    if (CardCore.BranchConditionEvaluator.IsRewriteCondition(_graph.steps[1].conditionId))
                    {
                        contextZh = "改写门——无奖励槽（改写即分支效果）";
                        return lp => false;
                    }
                    contextZh = "有限分支奖励（预算内·非错边）";
                    return lp => lp.CanBeBranchReward && !lp.IsWrongSideOnly
                        && RewardWithinBudget(lp.Entry(), _graph.steps[1]);
                case ComposeMode.Aura:
                    contextZh = "光环模式——不可指定作用对象（箭头指向格占据者，运行时解析）";
                    return lp => false;
            }
            contextZh = null;
            return null;
        }

        private void RefreshLibrary()
        {
            _libraryList.Clear();
            var pred = CurrentSlotPredicate(out var context);
            if (_libContext != null)
                _libContext.text = context != null ? $"已按选中槽过滤：{context}" : "";
            foreach (var row in AllTableRows())
            {
                if (!Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) continue;
                // 锁定关键词（2026-09-22）：攻击/守卫 + 拼点/运势/倒计时——移出原子库，
                // 在关键词区"不可编辑"固定展示（主干经下拉直选）
                if (IsLockedKeywordRow(row)) continue;
                if (_filterTypeZh != null && row.DisplayName != _filterTypeZh) continue;
                // 近似搜索：中文名 ∪ 描述模板 两列并集
                if (!string.IsNullOrEmpty(_filterName.value)
                    && !(row.DisplayName ?? "").Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)
                    && !(row.Description ?? "").Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)) continue;

                var payload = new LibPayload(type, row);
                // 槽位可用性（2026-09-22 五轮）：与分类/搜索取交集——只展示当前选中槽真正可落的原子
                if (pred != null && !pred(payload)) continue;
                _libraryList.Add(MakeLibraryRow(payload));
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

            // 点击=追加到首个合格槽（QuickAdd）
            row.RegisterCallback<ClickEvent>(_ => QuickAdd(payload));

            return row;
        }

        /// <summary>点击库行 = **替换左侧选中槽的原子**（2026-09-21 交互重做：顺序填充→选中替换；
        /// 选中槽不合适该原子时 toast 提示——点击左侧槽可切换选中）。
        /// 2026-09-22 二轮语义修订：**代价槽=任何可挂载原子都能填**——真正的区别是作用单位，
        /// 填入时由 UI 把作用域改写到错侧（有益→对手 / 有害→己方），并拦截形成 &gt;1 费的代价。</summary>
        private void QuickAdd(LibPayload payload)
        {
            // 代价槽选中：任意可挂载原子入代价（FillCostSlot 负责错侧改写+1 费限价）
            if (_selSlot == SelKind.Cost)
            {
                FillCostSlot(payload.Entry());
                return;
            }

            // 表级错边原子（域锁错侧——如 弃牌/送墓 锁己方域）：效果区禁错边——直达代价槽
            if (payload.IsWrongSideOnly)
            {
                FillCostSlot(payload.NewEntry());
                return;
            }

            switch (_mode)
            {
                case ComposeMode.Parallel:
                    if (!ParallelCanDrop(payload)) { ShowToast("该原子不可作主动效果（MountKinds 不含主动位）"); break; }
                    if (payload.IsKeywordAtom) ApplyKeywordHeader();
                    ReplaceParallel(EntryForEffectSlot(payload));
                    break;
                case ComposeMode.FreeBranch:
                    // 主干=槽内下拉直选（2026-09-22）——库行点击唯一落点=奖励槽；引擎行已移出库（防御兜底）
                    if (payload.IsEngineTrunk) { ShowToast("引擎主干=固定机制——主干在主干槽下拉直选"); break; }
                    if (!RewardCanDrop(payload)) { ShowToast("选中槽=奖励——该原子不开放分支奖励挂载"); break; }
                    if (!FreeRewardWithinBudget(payload.Entry())) { ShowToast("选中槽=奖励——超出引擎奖励预算（奖励预算=x）"); break; }
                    if (payload.IsKeywordAtom) ApplyKeywordHeader();
                    ReplaceFreeReward(EntryForEffectSlot(payload));
                    break;
                case ComposeMode.OutcomeGate:
                    if (_selSlot == SelKind.GateTrunk)
                    {
                        if (!payload.CanBeGateTrunk) { ShowToast("选中槽=主干——需要产出族原子（或点奖励槽切换选中）"); break; }
                        DropGateTrunk(payload);
                    }
                    else
                    {
                        if (!GateRewardCanDrop(payload, _graph.steps[1])) { ShowToast("选中槽=奖励——挂载位不合或超出门预算"); break; }
                        DropGateReward(payload);
                    }
                    break;
            }
        }

        /// <summary>库行 → 效果槽条目：关键词类（Grant*）统一为**他人赋予形态**
        ///（kinds={1,2}+str=运行时关键词 id——与关键词面板同口径；2026-09-22 五轮），其余原样。</summary>
        private static AtomicEffectEntry EntryForEffectSlot(LibPayload payload)
        {
            var entry = payload.Entry();
            if (!payload.IsKeywordAtom) return entry;
            entry.kinds = new List<int> { (int)TargetKind.OwnLivingUnit, (int)TargetKind.EnemyLivingUnit };
            // 关键词 id 与 CardLoader.LoadKeywords 同源：TryGetKeywordId 映射，否则枚举名去 Grant 前缀
            var enumName = payload.Cfg.EnumName;
            var kid = enumName;
            if (Enum.TryParse<AtomicEffectType>(enumName, out var gt))
            {
                if (!CardCore.Attribute.Handlers.GrantKeywordHandlerFactory.TryGetKeywordId(gt, out var mapped))
                    mapped = enumName.StartsWith("Grant") ? enumName.Substring("Grant".Length) : enumName;
                kid = mapped;
            }
            entry.str = kid;
            return entry;
        }

        /// <summary>填入代价槽（2026-09-22 二轮）：**作用单位改写错侧 + 限 1 费**。
        /// 改写规则（与装载期 WrongSide/SideLock 同口径）：有益(p&gt;0)→实例域取表域的对手侧成员、
        /// 有害(p&lt;0)→取己方侧成员（表域无该侧成员=该原子无法作用于错误对象，拒）；
        /// 中性(p=0)须表域单侧锁定（双侧/无目标=既非代价也非收益，拒）。
        /// 限价：PayloadUnitGrant &gt;1 拦截（值编辑后超限在代价卡展开区实时提示）。</summary>
        private void FillCostSlot(AtomicEffectEntry entry)
        {
            var cfg = AtomicEffectTable.GetByHashId(entry?.refId);
            if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type))
            {
                ShowToast("原子表引用缺失——无法作代价");
                return;
            }
            if (ComposerCatalog.IsEngineTrunk(type)) { ShowToast("引擎主干（拼点/运势/倒计时）不可作代价"); return; }

            var kinds = cfg.GetTargetKindList();
            float p = UnityEngine.Mathf.Clamp(cfg.Polarity, -1f, 1f);
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

            // 构筑期限价（2026-09-21 定案：只允许装形成 1 费的代价）——UI 在放置口收口为拦截
            var inst = CardEffectConverter.ConvertPayloadForDisplay(entry);
            int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            if (price > 1)
            {
                ShowToast($"该原子作代价将形成 {price} 费——构筑期只允许 1 费（调低数值或换原子）");
                return;
            }

            _graph.header.Costs = new List<CostEntry>
            {
                new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = entry },
            };
            _sel = SelKind.Cost;
            _selIndex = 0;
            _selSlot = SelKind.Cost; // 填入后保持代价槽选中（可再次点击覆盖）
            RefreshCostZone();
            RefreshName();
            RefreshRightPanels(); // 库切到代价视图（错侧·≤1费）（2026-09-22 五轮）
            ShowToast($"已填入代价槽（{price} 费·错侧作用）——再次点击可覆盖");
        }

        /// <summary>代价全价展示行（限 1 费——口径同装载期 PayloadUnitGrant）。</summary>
        private static string PayloadPriceText(AtomicEffectEntry entry)
        {
            var inst = CardEffectConverter.ConvertPayloadForDisplay(entry);
            int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            return price <= 1
                ? $"代价全价：{price} / 限 1 费"
                : $"代价全价：{price}——超限（构筑期只允许 1 费代价，保存前请调低数值或换原子）";
        }

        /// <summary>替换选中并列槽（空槽=追加并自动前进到下一空槽；满槽=原地换原子，选中不动）。</summary>
        private void ReplaceParallel(AtomicEffectEntry entry)
        {
            _graph.steps ??= new List<EffectStepData>();
            int s = Mathf.Clamp(_selSlotIndex, 0, 2);
            bool wasEmpty = s >= _graph.steps.Count;
            if (wasEmpty)
                _graph.steps.Add(new EffectStepData { kind = 0, atomic = entry });
            else if (_graph.steps[s].kind == 0)
                _graph.steps[s].atomic = entry;
            else
                _graph.steps[s] = new EffectStepData { kind = 0, atomic = entry }; // 只读步（抉择等）整步替换
            _sel = SelKind.Parallel;
            _selIndex = s;
            _selSlotIndex = wasEmpty ? Mathf.Min(_graph.steps.Count, 2) : s;
            RefreshSlots();
        }

        private void ReplaceFreeReward(AtomicEffectEntry entry)
        {
            _graph.header.AtomicEffects = new List<AtomicEffectEntry> { entry };
            _sel = SelKind.FreeReward;
            _selIndex = 0;
            RefreshSlots();
        }

        private void RefreshKeywords()
        {
            _keywordList.Clear();

            // ---- 槽位可用性（2026-09-22 五轮）：关键词=他人赋予原子——可落并列槽/奖励槽（位 4），
            //      主干/代价不可放（面板只留提示，锁定分组照常展示） ----
            bool kwAllowed =
                (_mode == ComposeMode.Parallel && _selSlot == SelKind.Parallel)
                || (_mode == ComposeMode.FreeBranch && _selSlot == SelKind.FreeReward)
                || (_mode == ComposeMode.OutcomeGate && _selSlot == SelKind.GateReward);
            if (_kwHeader != null)
            {
                _kwHeader.text = !kwAllowed
                    ? "可编辑关键词（当前选中槽不可放——点击效果/奖励槽切换）"
                    : _mode == ComposeMode.Parallel ? "可编辑关键词（点击加入并列槽——赋予一个生物）"
                    : _mode == ComposeMode.OutcomeGate ? "可编辑关键词（点击加入奖励槽——门预算内）"
                    : "可编辑关键词（点击加入奖励槽——赋予一个生物）";
            }

            if (kwAllowed)
            {
                foreach (var kw in KeywordCatalog.LoadAll())
                {
                    if (!string.IsNullOrEmpty(_filterName.value) && !kw.DisplayName.Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)) continue;
                    // 奖励槽：须开放分支挂载（Grant 行位 4）；门奖励再 ∩ 预算
                    if (_mode != ComposeMode.Parallel && !KeywordRewardMountable(kw)) continue;
                    if (_mode == ComposeMode.OutcomeGate && !RewardWithinBudget(KeywordEntry(kw), _graph.steps[1])) continue;
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

                    row.RegisterCallback<ClickEvent>(_ => QuickAddKeyword(captured));
                    _keywordList.Add(row);
                }
            }

            // ---- 不可编辑关键词（2026-09-22）：攻击/守卫=生物卡默认携带的固定能力；
            //      拼点/运势/倒计时=固定引擎机制（自由分支主干在主干槽下拉直选） ----
            // 均移出原子库（不可组合成效果），在此固定展示——降透明度示只读，点击只提示
            var lockedRows = AllTableRows().Where(IsLockedKeywordRow).ToList();
            if (lockedRows.Count > 0)
            {
                var lockedHeader = new Label("不可编辑关键词（固定·无法修改）");
                lockedHeader.AddToClassList("panel__header");
                lockedHeader.style.marginTop = 6;
                _keywordList.Add(lockedHeader);

                foreach (var tableRow in lockedRows)
                {
                    var captured = tableRow;
                    if (!string.IsNullOrEmpty(_filterName.value)
                        && !(captured.DisplayName ?? "").Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)
                        && !(captured.Description ?? "").Contains(_filterName.value, StringComparison.OrdinalIgnoreCase)) continue;

                    var row = new VisualElement();
                    row.AddToClassList("list-row");
                    row.AddToClassList("library-row");
                    row.AddToClassList("library-row--locked");

                    var dot = new VisualElement();
                    dot.AddToClassList("color-dot");
                    dot.AddToClassList(ChipClass(ColorFilter.OfColorName(captured.Tags)));
                    row.Add(dot);
                    var name = new Label(captured.DisplayName);
                    name.AddToClassList("list-row__name");
                    row.Add(name);
                    var desc = new Label(captured.Description);
                    desc.AddToClassList("list-row__meta");
                    row.Add(desc);

                    row.RegisterCallback<ClickEvent>(_ => ShowToast(IsEngineTrunkRow(captured)
                        ? $"{captured.DisplayName}：固定机制——自由分支主干在主干槽下拉直选，不可组合"
                        : $"{captured.DisplayName}：固定能力（生物卡默认携带，移除走 NoAttack/NoGuard）——不可在此编辑或组合"));
                    _keywordList.Add(row);
                }
            }
        }

        private void QuickAddKeyword(KeywordCatalogEntry kw)
        {
            if (_selSlot == SelKind.Cost) { ShowToast("选中槽=代价——关键词不可作代价（点击效果/奖励槽切换选中）"); return; }

            // 关键词=赋予他人形态（2026-09-22 五轮用户拍板）：kinds={1,2}+Single+UntilEndOfTurn——
            // "效果即赋予一个生物该关键词"；自己永久本体形态已废（卡面自带走卡牌关键词字段）
            var entry = KeywordEntry(kw);

            switch (_mode)
            {
                case ComposeMode.Parallel:
                    ApplyKeywordHeader();
                    ReplaceParallel(entry);
                    return;
                case ComposeMode.FreeBranch:
                    if (!KeywordRewardMountable(kw)) { ShowToast("该关键词未开放分支奖励挂载"); return; }
                    ApplyKeywordHeader();
                    ReplaceFreeReward(entry);
                    return;
                case ComposeMode.OutcomeGate:
                    if (_selSlot == SelKind.GateTrunk) { ShowToast("选中槽=主干——关键词不可作主干（点奖励槽切换选中）"); return; }
                    if (!KeywordRewardMountable(kw)) { ShowToast("该关键词未开放分支奖励挂载"); return; }
                    if (!RewardWithinBudget(entry, _graph.steps[1])) { ShowToast("选中槽=奖励——该关键词超出门预算"); return; }
                    ApplyKeywordHeader();
                    _graph.steps[1].thenSteps = new List<AtomicEffectEntry> { entry };
                    _sel = SelKind.GateReward;
                    _selIndex = 0;
                    _selSlot = SelKind.GateReward;
                    RefreshSlots();
                    return;
            }
        }

        /// <summary>关键词授予惯例（2026-09-22 五轮：他人形态）：Single + 持续到自己回合结束（1/2 回合同档）。</summary>
        private void ApplyKeywordHeader()
        {
            _graph.header.SelectionMode = (int)CardCore.SelectionMode.Single;
            _graph.header.Duration = (int)DurationType.UntilEndOfTurn;
            _graph.header.RandomTarget = 0; // 赋予目标由弹窗选一——不做随机
        }

        /// <summary>关键词 → 他人赋予原子条目：域={1,2}（双方生物/角色，表级 NoRole 已滤角色）。</summary>
        private static AtomicEffectEntry KeywordEntry(KeywordCatalogEntry kw)
            => new AtomicEffectEntry
            {
                refId = CardCore.Attribute.AtomicEffectTable.GetByEnumName(kw.AtomicEffect)?.HashId ?? kw.AtomicEffect,
                value = 1,
                str = kw.Id,
                kinds = new List<int> { (int)TargetKind.OwnLivingUnit, (int)TargetKind.EnemyLivingUnit },
            };

        /// <summary>关键词可否挂为分支奖励：Grant 表行 MountKinds 含位 4（BranchReward）。</summary>
        private static bool KeywordRewardMountable(KeywordCatalogEntry kw)
        {
            var row = CardCore.Attribute.AtomicEffectTable.GetByEnumName(kw.AtomicEffect);
            return row != null && MountKindExtensions.ParseCsv(row.MountKinds ?? "").Contains(MountKind.BranchReward);
        }

        // ======================================== 光环模式（2026-09-22 定案：连接光环=卡面字段，不可指定作用对象） ========================================

        /// <summary>光环编辑面板：连接箭头六向勾选 + 光环条目（属性/关键词）+ 光环费用预览。
        /// 作用对象**不可指定**——运行时 live-query 箭头指向格的当前占据者（断链/离场即失效）；
        /// 每支命中箭头各享受一次全部声明（按箭头叠加）；箭头数进卡级累乘 ×1.2^(n-1)。</summary>
        private VisualElement MakeAuraPanel()
        {
            var panel = new VisualElement();
            panel.Add(MakeHint("光环＝连接箭头持续效果：作用对象=箭头指向格的**当前占据者**（运行时 live-query，断链/离场即失效）——不可指定作用对象；每支命中箭头各享受一次声明（按箭头叠加）。"));

            // ---- 连接箭头（六向勾选，直接写卡面）----
            var arrowsBox = new VisualElement(); arrowsBox.AddToClassList("sub-item");
            var ah = new Label($"连接箭头（当前 {CountArrowBits(_editingCard.ArrowDirections)} 支——计价按箭头受益面：条目平价 + 卡级累乘 ×1.2^(n-1)）");
            ah.AddToClassList("panel__header");
            arrowsBox.Add(ah);
            var arrowRow = new VisualElement(); arrowRow.AddToClassList("toolbar"); arrowRow.style.flexWrap = Wrap.Wrap;
            foreach (var dir in new[] { HexDirection.Up, HexDirection.Down, HexDirection.UpperLeft,
                                         HexDirection.UpperRight, HexDirection.LowerLeft, HexDirection.LowerRight })
            {
                var d = dir;
                var t = new Toggle(ArrowZh(d)) { value = _editingCard.ArrowDirections.HasFlag(d) };
                t.AddToClassList("toggle");
                t.RegisterValueChangedCallback(e =>
                {
                    _editingCard.ArrowDirections = e.newValue
                        ? _editingCard.ArrowDirections | d
                        : _editingCard.ArrowDirections & ~d;
                    RefreshSlots(); // 箭头数与费用预览联动
                });
                arrowRow.Add(t);
            }
            arrowsBox.Add(arrowRow);
            arrowsBox.Add(MakeHint("光环必须搭配至少一支箭头（无箭头=永无受益者，保存拦截）；方向按持有玩家视角声明。"));
            panel.Add(arrowsBox);

            // ---- 光环条目 ----
            var listBox = new VisualElement(); listBox.AddToClassList("sub-item");
            var lh = new Label("光环条目（属性修正 stat+value / 关键词 keyword——二选一）");
            lh.AddToClassList("panel__header");
            listBox.Add(lh);
            _editingCard.LinkAuras ??= new List<LinkAuraData>();
            for (int i = 0; i < _editingCard.LinkAuras.Count; i++)
                listBox.Add(MakeAuraEntryRow(i));

            var addRow = new VisualElement(); addRow.AddToClassList("toolbar");
            var addStat = new Button(() =>
            {
                _editingCard.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 1 });
                RefreshSlots();
            }) { text = "+属性光环" };
            var addArmor = new Button(() =>
            {
                _editingCard.LinkAuras.Add(new LinkAuraData { keyword = CardCore.Attribute.KeywordRules.Armor });
                RefreshSlots();
            }) { text = "+坚韧（绿1/条）" };
            var addGuardian = new Button(() =>
            {
                _editingCard.LinkAuras.Add(new LinkAuraData { keyword = CardCore.Attribute.KeywordRules.Guardian });
                RefreshSlots();
            }) { text = "+守护（白1/条）" };
            foreach (var b in new[] { addStat, addArmor, addGuardian })
            {
                b.AddToClassList("btn"); b.AddToClassList("btn--mini");
                addRow.Add(b);
            }
            listBox.Add(addRow);
            panel.Add(listBox);

            // ---- 费用预览（Stage A 行——CardCostService 光环计价同源）----
            _auraCostLabel = new Label(AuraCostText());
            _auraCostLabel.AddToClassList("hint");
            panel.Add(_auraCostLabel);
            return panel;
        }

        /// <summary>光环费用预览标签（就地刷新防输入丢焦）。</summary>
        private Label _auraCostLabel;

        /// <summary>光环条目行：类型切换（属性/关键词）+ 对应编辑器 + 删除。</summary>
        private VisualElement MakeAuraEntryRow(int index)
        {
            var aura = _editingCard.LinkAuras[index];
            var row = new VisualElement();
            row.AddToClassList("step-card");

            var bar = new VisualElement(); bar.AddToClassList("toolbar");
            bool isStat = !string.IsNullOrEmpty(aura.stat);
            var type = new DropdownField("类型") { choices = new List<string> { "属性", "关键词" } };
            type.AddToClassList("text-input");
            type.index = isStat ? 0 : 1;
            type.RegisterValueChangedCallback(_ =>
            {
                if (type.index == 0) { aura.stat = "Power"; aura.keyword = null; }
                else { aura.stat = null; aura.keyword = CardCore.Attribute.KeywordRules.Armor; }
                RefreshSlots();
            });
            bar.Add(type);

            if (isStat)
            {
                var stat = new DropdownField("属性") { choices = new List<string> { "攻击力", "生命值" } };
                stat.AddToClassList("text-input");
                stat.index = aura.stat.Equals("Life", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                stat.RegisterValueChangedCallback(_ =>
                {
                    aura.stat = stat.index == 1 ? "Life" : "Power";
                    RefreshAuraCost();
                });
                bar.Add(stat);
                bar.Add(MakeIntField("值(±)", aura.value, v => { aura.value = v; RefreshAuraCost(); }));
            }
            else
            {
                var kw = new TextField("关键词id") { value = aura.keyword ?? "" };
                kw.AddToClassList("text-input");
                kw.RegisterValueChangedCallback(e => { aura.keyword = e.newValue; RefreshAuraCost(); });
                bar.Add(kw);
                bar.Add(MakeHint("坚韧=Armor 守护=Guardian"));
            }

            var del = new Button(() => { _editingCard.LinkAuras.RemoveAt(index); RefreshSlots(); }) { text = "删除" };
            del.AddToClassList("btn"); del.AddToClassList("btn--mini"); del.AddToClassList("btn--danger");
            bar.Add(del);
            row.Add(bar);
            return row;
        }

        /// <summary>光环费用预览文本（CardCostService.Derive 的 Stage=="A" 行同源——含箭头累乘）。</summary>
        private string AuraCostText()
        {
            if (_editingCard == null) return "光环费：—";
            try
            {
                var lines = CardCore.CardCostService.Derive(_editingCard).Breakdown
                    .Where(l => l != null && l.Stage == "A").ToList();
                if (lines.Count == 0)
                    return "光环费：—（未声明条目；坚韧=绿1/条、守护=白1/条、属性=光环档 1.5/+1）";
                var parts = lines.Select(l =>
                    $"{l.Label}＝{l.Value:0.#}{(l.Color.HasValue ? ColorZh(l.Color.Value) : "")}").ToList();
                return "光环费：" + string.Join("；", parts);
            }
            catch
            {
                return "光环费：—（计算异常）";
            }
        }

        /// <summary>就地刷新光环费用行（值/属性微调不重建面板——防输入丢焦）。</summary>
        private void RefreshAuraCost()
        {
            if (_auraCostLabel != null) _auraCostLabel.text = AuraCostText();
        }

        private static int CountArrowBits(HexDirection d)
        {
            int n = 0, v = (int)d;
            while (v != 0) { n += v & 1; v >>= 1; }
            return n;
        }

        private static string ArrowZh(HexDirection d) => d switch
        {
            HexDirection.Up => "上",
            HexDirection.Down => "下",
            HexDirection.UpperLeft => "左上",
            HexDirection.UpperRight => "右上",
            HexDirection.LowerLeft => "左下",
            _ => "右下",
        };

        // ======================================== 保存 ========================================

        private void OnSave()
        {
            // 光环模式（2026-09-22 定案）：光环=卡面字段（LinkAuras+箭头）——保存只校验+返回，效果槽不动
            if (_mode == ComposeMode.Aura)
            {
                if (_editingCard == null) { ShowToast("光环模式仅在卡编辑会话可用"); return; }
                _editingCard.LinkAuras ??= new List<LinkAuraData>();
                _editingCard.LinkAuras.RemoveAll(a =>
                    a == null || (string.IsNullOrEmpty(a.stat) && string.IsNullOrEmpty(a.keyword)));
                if (_editingCard.LinkAuras.Count == 0)
                { ShowToast("光环模式：未声明任何有效光环条目（stat/keyword 至少一项）"); return; }
                if (_editingCard.ArrowDirections == HexDirection.None)
                { ShowToast("光环必须搭配箭头——无箭头=永无受益者（CardLoader 构筑校验同口径）；请在光环面板勾选箭头"); return; }
                ShowToast($"已保存连接光环 ×{_editingCard.LinkAuras.Count}（箭头 ×{CountArrowBits(_editingCard.ArrowDirections)}，费用自动入整卡推导）——效果槽未改动");
                _editingCard = null;
                _editingIndex = -1;
                Manager.Back();
                return;
            }

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
        /// 点击（QuickAdd）消费，保证口径唯一。
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

            /// <summary>关键词类原子（MountKinds 含 Keyword 位）——库行点击同关键词面板：
            /// 恒为他人赋予形态（2026-09-22 五轮用户拍板）。</summary>
            public bool IsKeywordAtom => Cfg != null && Mounts.Contains(MountKind.Keyword);

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

            /// <summary>可否作代价（2026-09-22 五轮库过滤）：与 FillCostSlot 同口径的表级判定 +
            /// 默认值(value=1)全价 ≤1（放置口仍复检——值编辑超限另有实时提示）。若 p≠0 只要有错侧成员即可
            ///（不限表级锁——FillCostSlot 会把实例域改写过去）。</summary>
            public bool CanBeCost
            {
                get
                {
                    if (Cfg == null || IsEngineTrunk) return false;
                    float p = UnityEngine.Mathf.Clamp(Cfg.Polarity, -1f, 1f);
                    var kinds = Cfg.GetTargetKindList();
                    bool sideOk;
                    if (p != 0f)
                    {
                        bool wantEnemy = p > 0f;
                        sideOk = kinds.Any(k => TargetKindRules.IsEnemySide(k) == wantEnemy);
                    }
                    else sideOk = CostDerivationService.SideLock(kinds) != 0;
                    if (!sideOk) return false;
                    var inst = CardEffectConverter.ConvertPayloadForDisplay(Entry());
                    int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
                    return price <= 1;
                }
            }

            /// <summary>落槽生成的新原子条目（引用型：refId=表行ID，value=1）。</summary>
            public AtomicEffectEntry NewEntry() => Entry();

            public AtomicEffectEntry Entry()
                => new AtomicEffectEntry { refId = Cfg?.HashId, value = 1 };
        }
    }
}
