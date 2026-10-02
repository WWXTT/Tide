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
    /// 效果合成界面（2026-10-01 预制体化：静态层级来自 Assets/Art/UI/EffectUI.prefab，
    /// Build 深度按名绑定+闭包接线；槽位区/原子库/效果表/光环面板运行时重建。
    /// 交互/文案/校验语义与历史 UITK 版对齐（修订史见 tag uitk-ui-final 版头注释）；
    /// 2026-10-02 起 uGUI-native：排版按 25645c0^ 的 EffectComposer.uxml/Common.uss 历史稿还原，
    /// 下拉弹层直接挂屏根（历史版无 overlay 挂载节点，勿再引入）。
    /// 左=编辑栏（模式条+槽位区）/ 右=展示区（原子库 / 效果表双模式）+ 筛选。
    /// 核心定案：槽位选中制（右侧点击=替换选中槽）；组合三态（并列/自由分支/有限分支/光环，
    /// InferMode 推断防互串）；校验实时化（区域闪烁红框+保存禁用）；属性描述选中才出现（描述条）；
    /// 光环=箭头+条目随效果合成（挂卡并集，效果层只预选不计箭头费）；
    /// 代价栏已上移卡组合层（错边原子在卡编辑界面填装）。
    /// </summary>
    public sealed class EffectComposerScreen : UIScreen
    {
        // ======================================== 组合形态 ========================================

        /// <summary>组合三态（UI/存读共用推断，防三种形态互串）。</summary>
        public enum ComposeMode { Parallel, FreeBranch, OutcomeGate, Aura }

        /// <summary>右栏双模式：原子库（点击装配）/ 效果表（点击载入编辑）。</summary>
        private enum RightMode { Atoms, Effects }

        /// <summary>由数据推断组合形态：引擎通道优先 → 光环声明 → 含 kind=1 步骤=有限分支 → 其余并列。</summary>
        public static ComposeMode InferMode(EffectGraphData g)
        {
            if (g?.header != null && g.header.EngineKind != (int)BranchEngineKind.None) return ComposeMode.FreeBranch;
            if (g?.header != null && (g.header.ArrowDirections != 0
                || (g.header.LinkAuras != null && g.header.LinkAuras.Count > 0))) return ComposeMode.Aura;
            if (g?.steps != null && g.steps.Any(s => s?.kind == 1)) return ComposeMode.OutcomeGate;
            return ComposeMode.Parallel;
        }

        // ---- 状态 ----
        private EffectGraphData _graph = new EffectGraphData("新效果");
        private ComposeMode _mode = ComposeMode.Parallel;
        private RightMode _rightMode = RightMode.Atoms;

        // 槽内卡片展开状态（并列下标存 _selIndex；FreeTrunk 无下标；代价栏已上移卡组合层）
        private enum SelKind { None, Parallel, GateTrunk, GateReward, FreeTrunk, FreeReward }
        private SelKind _sel = SelKind.None;
        private int _selIndex = -1;

        // 槽位选中：右侧点击=替换选中槽；默认原子1（首个槽）
        private SelKind _selSlot = SelKind.Parallel;
        private int _selSlotIndex = 0;

        // 卡编辑会话（null=效果库模式——保存到效果库文件）
        private CardData _editingCard;
        private int _editingIndex = -1;

        // 从效果表载入的源效果 id（编辑换内容=换 id——保存后清旧档防重复；null=非效果表载入）
        private string _loadedFromLibraryId;

        // ---- 校验区：不合规设置=区域闪烁红框+红字提示，保存按钮禁用 ----
        private sealed class ZoneEntry
        {
            public RectTransform Zone;
            public Outline Outline;
            public Color BaseColor;
            public Func<string> Check; // null/空=合规；否则=错误提示文本
            public TMP_Text ErrLabel;
            public bool Invalid;
        }
        private readonly List<ZoneEntry> _slotZones = new List<ZoneEntry>();
        private Button _saveBtn;
        private bool _flashOn;
        private float _flashAccumMs;

        // ---- 属性描述条：描述只在控件选中时出现——不常驻 ----
        private TMP_Text _descStrip;
        private RectTransform _descStripNode;

        // 光环箭头六向表：deg=自正上方顺时针角度（与 HexDirection 几何一致）；
        // zh=棋盘实际方位名（BoardMath.MapArrow——三角形校准后指向的就是该方位）
        private static readonly (HexDirection dir, float deg, string zh)[] ArrowDirs =
        {
            (HexDirection.Up, 0f, "右上"), (HexDirection.UpperRight, 60f, "右"), (HexDirection.LowerRight, 120f, "右下"),
            (HexDirection.Down, 180f, "左下"), (HexDirection.LowerLeft, 240f, "左"), (HexDirection.UpperLeft, 300f, "左上"),
        };

        // ---- UI 引用 ----
        private UiKit.Scroll _slotArea, _libraryList, _effectsList;
        private RectTransform _modeBar, _atomPanel, _effectsPanel, _timingRow;
        private TMP_Text _toast, _nameLabel, _costLabel, _libContext;
        private UInputField _filterName;
        private UiKit.Dropdown _timingDropdown, _activationDropdown, _filterTypeDropdown;
        private Button _reloadAtomsBtn, _loadEffectsBtn;
        private List<TriggerTiming> _timings;

        // 筛选状态：原子库=表行中文名；效果表=内含原子名。近似搜索：原子库=中文名∪描述模板；效果表=名∪id∪组成简称
        private string _filterTypeZh; // null = 全部
        private List<string> _filterTypeChoices = new List<string> { "全部" };

        // 发动方式：0=强制 1=自动 2=主动（EffectActivationType 同序）
        private static readonly string[] ActivationNames = { "强制", "自动", "主动" };

        // 触发时机中文（主动三档不入下拉）
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

        // 效果级持续档中文（按值映射，ForTurns 仅存于指示物时钟不提供）
        private static readonly (string label, int value)[] DurationChoices =
        {
            ("一次性", (int)DurationType.Once),
            ("永久", (int)DurationType.Permanent),
            ("到回合结束", (int)DurationType.UntilEndOfTurn),
            ("到对手回合结束", (int)DurationType.UntilNextTurn),
            ("离场清除", (int)DurationType.UntilLeaveBattlefield),
            ("条件持续", (int)DurationType.WhileCondition),
        };

        /// <summary>固定战斗原子：攻击/守卫=生物卡默认携带——移出原子库不可组合。</summary>
        private static bool IsFixedBattleAtom(string enumName)
            => enumName == "Attack" || enumName == "Guard";

        // 固定引擎主干（拼点/运势/倒计时/死亡计数/元素充盈/手牌序位=不可修改关键词）——移出原子库
        private static readonly BranchEngineKind[] TrunkEngines =
        {
            BranchEngineKind.None, BranchEngineKind.Clash, BranchEngineKind.LuckRoll, BranchEngineKind.Countdown,
            BranchEngineKind.DeathToll, BranchEngineKind.ManaSurplus, BranchEngineKind.NthHandCard,
        };

        private static bool IsEngineTrunkRow(AtomicEffectConfig r)
            => ComposerCatalog.HasMountBit(r, MountKind.FreeBranchTrunk); // 位 9 数据驱动

        /// <summary>锁定关键词行（移出原子库、不可组合、不展示）：攻击/守卫 + 位 9 引擎行。</summary>
        private static bool IsLockedKeywordRow(AtomicEffectConfig r)
            => r != null && (IsFixedBattleAtom(r.EnumName) || IsEngineTrunkRow(r));

        /// <summary>目标域可选集：按 Polarity 过滤表域——效果区：p&gt;0 只己方侧 / p&lt;0 只对方侧 / p=0 不限。
        /// 口径与装载期 WrongSide/SideLock 内容契约同源。</summary>
        private static List<int> AllowedTargetKinds(AtomicEffectConfig cfg, List<int> tableKinds)
        {
            float p = UnityEngine.Mathf.Clamp(cfg?.Polarity ?? 0f, -1f, 1f);
            if (p == 0f) return tableKinds;
            bool ownOnly = p > 0f; // 有益=效果区只己方侧；有害=只对方侧
            return tableKinds.Where(k => TargetKindRules.IsEnemySide(k) != ownOnly).ToList();
        }

        // ======================================== 构建 ========================================

        protected override string PrefabAddress => "EffectUI";
        protected override string PrefabAssetPath => "Assets/Art/UI/EffectUI.prefab";
        protected override string RootName => "effect-composer";

        protected override void Build()
        {
            // ---- 顶栏 ----
            BindButton("btn-back", () => Manager.Back());
            _nameLabel = FindText("lbl-effect-name");
            _costLabel = FindText("lbl-effect-cost");
            _activationDropdown = BindDropdown("dropdown-activation", Root, Find("topbar"),
                ActivationNames.ToList(), 0, width: 90f);
            _timingRow = Find("timing-row");
            _timingDropdown = BindDropdown("dropdown-timing", Root, _timingRow,
                new List<string> { "—" }, 0, width: 140f);
            _toast = FindText("lbl-toast");
            _reloadAtomsBtn = BindButton("btn-reload-atoms", OnReloadAtoms);
            _loadEffectsBtn = BindButton("btn-load-effects", OnLoadEffectsTable);
            _saveBtn = BindButton("btn-save", OnSave);

            // ---- 主体 ----
            _modeBar = Find("mode-bar");
            _slotArea = FindScroll("slot-area");

            // 描述条（左栏底部——选中才出现）
            EnsureDescStrip();

            // 右：展示区（双模式）
            _atomPanel = Find("atom-panel");
            _filterTypeDropdown = BindDropdown("dropdown-filter-type", Root, Find("filter-row"),
                new List<string> { "全部" }, 0, width: 150f);
            _filterName = FindInput("field-filter-name");
            if (_filterName != null)
                _filterName.onValueChanged.AddListener(_ =>
                {
                    RefreshLibrary();
                    if (_rightMode == RightMode.Effects) RefreshEffectsList();
                });
            _libContext = FindText("lbl-lib-context");
            _libraryList = FindScroll("library-list");

            _effectsPanel = Find("effects-panel");
            _effectsList = FindScroll("list-effects");

            // 右栏框架配额（2026-10-02：转换烘焙的 LayoutElement 残留会给 VLG 喂假 preferred，
            // 过滤行曾被拉到 876 高、列表塌成 0——运行时钉死：过滤行 30 高、说明行 18、列表占满其余）
            if (_atomPanel != null)
            {
                var filterRow = UiKit.FindDeep(_atomPanel, "filter-row");
                if (filterRow != null) UiKit.Size(filterRow, fw: 1f, h: 30f, minH: 30f);
                var filterDrop = UiKit.FindDeep(_atomPanel, "dropdown-filter-type");
                if (filterDrop != null) UiKit.Size(filterDrop, w: 200f, minW: 200f, h: 30f);
                if (_filterName != null) UiKit.Size(_filterName.transform, w: 200f, minW: 200f, h: 30f);
                if (_libContext != null) UiKit.Size(_libContext.transform, fw: 1f, h: 18f, minH: 0f);
                if (_libraryList != null) UiKit.Size(_libraryList.Rect.transform, fw: 1f, fh: 1f);
            }
            if (_effectsPanel != null) _effectsPanel.gameObject.SetActive(false);

            // 校验红框闪烁 + 描述条路由 + 泵：挂在屏根（层级销毁自动失效）
            UiKit.Updater.Attach(Root, Tick);
            UiKit.DescRequested += OnDescRequested;
        }

        /// <summary>描述条绑定/补建（预制体烘焙优先；缺失时在左栏代码补建原样式）。</summary>
        private void EnsureDescStrip()
        {
            _descStripNode = UiKit.FindDeep(Root, "desc-strip");
            if (_descStripNode == null)
            {
                var left = UiKit.FindDeep(Root, "left") ?? Root;
                _descStripNode = UiKit.Node("desc-strip", left);
                var dsBg = _descStripNode.gameObject.AddComponent<Image>();
                dsBg.sprite = UiKit.RoundedSprite;
                dsBg.type = Image.Type.Sliced;
                dsBg.color = UiStyle.DescStripBg;
                var dsOl = _descStripNode.gameObject.AddComponent<Outline>();
                dsOl.effectColor = UiStyle.DescStripBorder;
                dsOl.effectDistance = Vector2.one;
                UiKit.Size(_descStripNode, fw: 1f, minH: 30f);
            }

            var lblRt = UiKit.FindDeep(_descStripNode, "lbl-prop-desc");
            _descStrip = lblRt != null
                ? lblRt.GetComponent<TMP_Text>()
                : UiKit.Label("lbl-prop-desc", _descStripNode, "", UiStyle.MiniSize,
                    UiStyle.DescStrip, TextAnchor.UpperLeft, wrap: true);
            UiKit.StretchInset(_descStrip.rectTransform, 8f, 4f);
            _descStripNode.gameObject.SetActive(false);
        }

        private RectTransform RootParent() => Parent;

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
                if (_toast != null) _toast.text = "界面初始化异常：" + e.Message;
            }
        }

        public override void OnExit()
        {
            UiKit.DescRequested -= OnDescRequested;
        }

        private void OnDescRequested(string desc)
        {
            if (_descStrip == null) return;
            _descStrip.text = desc ?? "";
            bool show = !string.IsNullOrEmpty(desc);
            _descStripNode.gameObject.SetActive(show);
        }

        /// <summary>帧泵：500ms 翻转闪烁态——违规区红框双态切换（其余帧空转）。</summary>
        private void Tick()
        {
            _flashAccumMs += Time.unscaledDeltaTime * 1000f;
            if (_flashAccumMs < 500f) return;
            _flashAccumMs = 0f;
            _flashOn = !_flashOn;
            foreach (var z in _slotZones)
            {
                if (z?.Zone == null || !z.Invalid || z.Outline == null) continue;
                z.Outline.effectColor = _flashOn
                    ? UiStyle.ErrorRed
                    : new Color(UiStyle.ErrorRed.r, UiStyle.ErrorRed.g, UiStyle.ErrorRed.b, 0.28f);
            }
        }

        private void OnEnterInternal()
        {
            BuildFilterCallbacks(); // 分类下拉回调（Build 后一次；重读原子表只重建选项）

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
            _mode = InferMode(_graph);
            EnsureDefaultSelection();
            RefreshAll();
            SetRightMode(RightMode.Atoms);
            ValidateZones(); // 初始校验（载入数据可能自带违规——如超预算奖励）

            // 空库自诊断：正常应 ≥87 行——为空说明表未装载或初始化半途出错（查 Console）
            int libCount = _libraryList?.Content?.childCount ?? 0;
            if (libCount == 0)
                ShowToast($"原子库为空（表加载失败或初始化异常——查 Console；分类项 {_filterTypeChoices?.Count ?? 0}）");
        }

        // ======================================== 元信息（顶栏） ========================================

        private void BuildTimingDropdown()
        {
            // 全中文时机下拉（主动三档 Activate_* 不入——主动不设时机）
            _timings = TimingZh.Keys.ToList();
            var labels = _timings.Select(t => TimingZh[t]).ToList();
            _timingDropdown.SetOptions(labels, 0);
            SyncTimingDropdown();
            _timingDropdown.Changed += (idx, _) =>
            {
                if (idx >= 0 && idx < _timings.Count) _graph.header.TriggerTiming = (int)_timings[idx];
            };
            UiKit.Described(_timingDropdown.Root.GetComponent<Button>(),
                "触发时机：效果在哪个时点自动入栈（条件发动不走速度，满足即入栈）；主动发动方式下不设时机");
        }

        private void SyncTimingDropdown()
        {
            int current = _timings.IndexOf((TriggerTiming)_graph.header.TriggerTiming);
            _timingDropdown.SetIndex(current < 0 ? 0 : current);
        }

        private void BuildActivationDropdown()
        {
            int act = _graph.header.ActivationType;
            _activationDropdown.SetOptions(ActivationNames.ToList(), act >= 0 && act < ActivationNames.Length ? act : 0);
            SyncActivationVisibility();
            UiKit.Described(_activationDropdown.Root.GetComponent<Button>(),
                "发动方式——强制：条件达成时不询问直接发动；自动：条件达成时弹窗询问是否发动（可选触发式）；主动：只能在自己回合的主要阶段主动发动（=启动式：构筑期不计元素锚价、发动时现付+横置，2026-10-02 定案）");
            _activationDropdown.Changed += (idx, _) =>
            {
                _graph.header.ActivationType = idx;
                SyncActivationVisibility();
            };
        }

        /// <summary>主动（=2）只在自己主阶段发动——隐藏触发时机；其余显示时机。
        /// 2026-10-02 定案：主动=启动式——主动档时机钉 Activate_Active（converter 对漏网数据双向钉死，
        /// 此处保证写盘一致）；离开主动档若时机仍为 Activate_* → 回落 OnPlay（触发式默认档）。</summary>
        private void SyncActivationVisibility()
        {
            if (_timingRow == null) return;
            bool voluntary = _graph.header.ActivationType == 2;
            int t = _graph.header.TriggerTiming;
            if (voluntary)
            {
                if (t != (int)TriggerTiming.Activate_Active
                    && t != (int)TriggerTiming.Activate_Instant
                    && t != (int)TriggerTiming.Activate_Response)
                    _graph.header.TriggerTiming = (int)TriggerTiming.Activate_Active;
            }
            else if (t == (int)TriggerTiming.Activate_Active
                     || t == (int)TriggerTiming.Activate_Instant
                     || t == (int)TriggerTiming.Activate_Response)
            {
                _graph.header.TriggerTiming = (int)TriggerTiming.OnPlay;
            }
            _timingRow.gameObject.SetActive(!voluntary);
        }

        // ======================================== 效果名自动构成 ========================================

        /// <summary>自动名 = 中文名＋组合方式＋中文名（并列"＋"、两分支"→"；空槽以"…"占位；
        /// 光环=条目列表×箭头数）。</summary>
        private string AutoName()
        {
            var h = _graph.header;
            if (_mode == ComposeMode.Aura)
            {
                var auraParts = new List<string>();
                foreach (var a in h.LinkAuras ?? new List<LinkAuraData>())
                {
                    if (a == null) continue;
                    if (!string.IsNullOrEmpty(a.stat))
                        auraParts.Add($"{(a.stat.Equals("Life", StringComparison.OrdinalIgnoreCase) ? "生命" : "攻击")}{a.value:+0;-0}");
                    else if (!string.IsNullOrEmpty(a.keyword)) auraParts.Add(KeywordZh(a.keyword));
                }
                int n = CountArrowBits((HexDirection)h.ArrowDirections);
                return $"光环[{string.Join("，", auraParts)}]×{n}箭头";
            }
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
            // 关键词原子两态命名：赋予他人=主动效果——「赋予xx」；自己=关键词本体名
            bool toOthers = entry.kinds != null && entry.kinds.Count > 0
                && !(entry.kinds.Count == 1 && entry.kinds[0] == (int)TargetKind.Self);
            if (toOthers && MountKindExtensions.ParseCsv(cfg.MountKinds ?? "").Contains(MountKind.Keyword))
                return "赋予" + name;
            return name;
        }

        private static string RewardZh(AtomicEffectEntry entry) => entry == null ? "…" : AtomZh(entry);

        /// <summary>光环关键词中文名（坚韧/守护特判，其余原样返回 id）。</summary>
        private static string KeywordZh(string keywordId) => keywordId switch
        {
            "Armor" => "坚韧",
            "Guardian" => "守护",
            null or "" => "？",
            _ => keywordId,
        };

        private static string TrunkZh(BranchEngineKind engine)
            => AtomicEffectTable.GetByEnumName("BranchEngine" + engine)?.DisplayName ?? engine.ToString();

        private void RefreshName()
        {
            _nameLabel.text = AutoName();
            _costLabel.text = "费用：" + AutoCostText();
        }

        /// <summary>自动费用预览=**效果锚价**（效果组合阶段纯表累加、无减免抵消——ConvertOne +
        /// DeriveElementCosts 实时推导，含改写门差价；代价不参与——代价栏已上移卡组合层）。</summary>
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
            RefreshLibrary();
            RefreshName();
        }

        private void RefreshModeBar()
        {
            ClearChildren(_modeBar);
            MakeModeChip("并列（1-3 原子）", ComposeMode.Parallel);
            MakeModeChip("自由分支（引擎条件）", ComposeMode.FreeBranch);
            MakeModeChip("有限分支（产出条件）", ComposeMode.OutcomeGate);
            MakeModeChip("光环（连接箭头）", ComposeMode.Aura);

            // 卡编辑模式提示
            if (_editingCard != null)
            {
                var tag = UiKit.Label("editing-tag", _modeBar,
                    $"正在编辑：{_editingCard.CardName} #{_editingIndex + 1}",
                    UiStyle.MiniSize, UiStyle.TextHint);
                UiKit.Size(tag, w: 200f);
            }
        }

        private void MakeModeChip(string label, ComposeMode mode)
        {
            bool active = mode == _mode;
            UiKit.MiniButton($"mode-{mode}", _modeBar, label, () => SwitchMode(mode),
                active ? UiStyle.ChipActiveBg : UiStyle.BtnBg,
                active ? UiStyle.ChipActiveText : UiStyle.TextBody);
        }

        /// <summary>形态切换：best-effort 数据搬运（原子尽量保留），切换后收起展开卡。
        /// 光环字段只在光环形态存续——切出即清，防止非光环效果夹带箭头落库。</summary>
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
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
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
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
                    ShowToast("切换为自由分支——主干在主干槽下拉选择，奖励点击库行填入");
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
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
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
                    // 光环（效果层定案）＝箭头+光环条目随效果合成——任意会话可用；
                    // 作用对象不可指定（运行时 live-query 箭头指向格占据者）；原子数据清空防夹带。
                    h.EngineKind = (int)BranchEngineKind.None;
                    h.EngineParam = 0;
                    h.AtomicEffects = null;
                    _graph.steps.Clear();
                    ShowToast("光环模式：左侧选箭头，右栏点击添加光环条目——挂卡时自动并入卡面（多光环取并集）");
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
            ClearContent(_slotArea.Content);
            _slotZones.Clear(); // 槽区校验区随重建
            ClampSelection();
            var h = _graph.header;
            _graph.steps ??= new List<EffectStepData>();

            switch (_mode)
            {
                case ComposeMode.Parallel:
                {
                    RectTransform firstEmptySlot = null;
                    for (int i = 0; i < 3; i++)
                    {
                        int idx = i;
                        var slot = MakeSlot($"原子槽 {i + 1}", SlotBorder(SelKind.Parallel));
                        MarkSelected(slot, SelKind.Parallel, i);
                        // 越过空档的槽不可选（保持无空档填充）
                        SelectOnClick(slot, SelKind.Parallel, i, i > _graph.steps.Count);
                        if (i < _graph.steps.Count)
                        {
                            var step = _graph.steps[i];
                            if (step.kind == 0) MakeAtomCard(slot, step.atomic, idx, SelKind.Parallel);
                            else MakeReadonlyStepBadge(slot, step); // 抉择等只读
                        }
                        else if (i == _graph.steps.Count)
                        {
                            MakeHint(slot, "空槽——选中后点击右侧原子库填入");
                            firstEmptySlot ??= slot;
                        }
                        else MakeHint(slot, "（先填前面的槽）");
                    }
                    if (_graph.steps.Count >= 3)
                        MakeHint(_slotArea.Content, "已满 3 原子——点击槽选中后，右侧点击即替换");
                    // 校验：并列模式至少一个原子（只读抉择步计入内容——卡组成阶段产物）
                    if (firstEmptySlot != null)
                        _slotZones.Add(MakeZone(firstEmptySlot, () =>
                            !_graph.steps.Any(s => s != null && ((s.kind == 0 && s.atomic != null) || s.kind == 2))
                                ? "效果为空——并列模式至少需要一个原子（选中槽后点击右侧库行）" : null));
                    break;
                }
                case ComposeMode.FreeBranch:
                {
                    // 主干槽（引擎条件·下拉直选）：拼点/运势/倒计时=固定机制关键词，已移出原子库
                    var trunkSlot = MakeSlot("主干（条件引擎）", UiStyle.SlotTrunkEdge);
                    var trunkLabels = TrunkEngines
                        .Select(e => e == BranchEngineKind.None ? "（未选择）" : TrunkZh(e)).ToList();
                    var trunkDd = new UiKit.Dropdown("dd-trunk", trunkSlot, Root, trunkLabels, 0, onChanged: null, width: 220f);
                    UiKit.Described(trunkDd.Root.GetComponent<Button>(),
                        "条件引擎——拼点：比双方牌库顶费用差 / 运势：2d6 双＞x / 倒计时：回合递减归零发奖 / "
                        + "死亡计数：本回合双方合计死亡≥x / 元素充盈：付费后 bank 最多色＞x / 手牌序位：此卡为本回合第 x 张；参数在主干卡展开区调");
                    int tcur = Array.IndexOf(TrunkEngines, (BranchEngineKind)h.EngineKind);
                    trunkDd.SetIndex(tcur >= 0 ? tcur : 0);
                    trunkDd.Changed += (idx, _) =>
                    {
                        var engine = TrunkEngines[Mathf.Max(0, idx)];
                        h.EngineKind = (int)engine;
                        h.EngineParam = engine == BranchEngineKind.None ? 0 : 1; // 倒计时 x=1（可改 0=自动换算）；拼点/运势默认 x=1
                        _sel = SelKind.FreeTrunk; // 展开主干卡便于调参
                        // 主干已定——自动前进到奖励槽（若空）
                        if (engine == BranchEngineKind.None || h.AtomicEffects == null || h.AtomicEffects.Count == 0)
                            _selSlot = SelKind.FreeReward;
                        RefreshSlots();
                        RefreshName();
                        RefreshRightPanels();
                    };
                    if (h.EngineKind != (int)BranchEngineKind.None)
                        MakeTrunkCard(trunkSlot, (BranchEngineKind)h.EngineKind, h.EngineParam);
                    else MakeHint(trunkSlot, "从上方下拉选择引擎；奖励在下方奖励槽（点击库行填入）");
                    // 校验：主干引擎必选
                    _slotZones.Add(MakeZone(trunkSlot, () => h.EngineKind == (int)BranchEngineKind.None
                        ? "未选择条件引擎——从上方下拉选择（拼点/运势/倒计时/死亡计数/元素充盈/手牌序位）" : null));

                    var arrow = UiKit.Label("arrow", _slotArea.Content, "条件达成 →",
                        UiStyle.MiniSize, UiStyle.TextHint, TextAnchor.MiddleCenter);
                    UiKit.Size(arrow, fw: 1f);

                    // 奖励槽（单原子；死亡计数/元素充盈带预算=x——奖励预算制）
                    var rewardSlot = MakeSlot("奖励（单原子）", UiStyle.SlotRewardEdge);
                    MarkSelected(rewardSlot, SelKind.FreeReward, 0);
                    SelectOnClick(rewardSlot, SelKind.FreeReward, 0, false);
                    var reward = h.AtomicEffects?.FirstOrDefault();
                    if (reward != null) MakeAtomCard(rewardSlot, reward, 0, SelKind.FreeReward);
                    else MakeHint(rewardSlot, "选中后点击库中的奖励原子填入（须开放分支奖励挂载）");
                    // 校验：奖励必填 + 预算内
                    _slotZones.Add(MakeZone(rewardSlot, () =>
                    {
                        var r = h.AtomicEffects?.FirstOrDefault();
                        if (r == null) return "奖励为空——选中奖励槽后点击库行填入";
                        int budget = ComposerCatalog.EngineRewardBudget((BranchEngineKind)h.EngineKind, h.EngineParam);
                        if (budget < 0) return null;
                        float cost = RewardCost(r);
                        return cost > budget ? $"奖励超出引擎预算（{cost:0.#}/{budget}）——调低数值或换原子" : null;
                    }));

                    if (ComposerCatalog.EngineRewardBudget((BranchEngineKind)h.EngineKind, h.EngineParam) > 0)
                        MakeHint(_slotArea.Content, FreeRewardBudgetText());
                    break;
                }
                case ComposeMode.Aura:
                {
                    MakeAuraPanel(_slotArea.Content);
                    break;
                }
                case ComposeMode.OutcomeGate:
                {
                    EnsureGateShape();
                    var trunk = _graph.steps[0];
                    var branch = _graph.steps[1];

                    // 主干槽（产出原子）
                    var trunkSlot = MakeSlot("主干（产出原子）", UiStyle.SlotTrunkEdge);
                    MarkSelected(trunkSlot, SelKind.GateTrunk, 0);
                    SelectOnClick(trunkSlot, SelKind.GateTrunk, 0, false);
                    MakeAtomCard(trunkSlot, trunk.atomic, 0, SelKind.GateTrunk);
                    // 校验：主干原子必填
                    _slotZones.Add(MakeZone(trunkSlot, () => trunk.atomic == null
                        ? "主干为空——选中主干槽后点击库行选择原子（产出族或通用门主干）" : null));

                    // 门行：条件下拉（中文+【奖励x】）内联
                    MakeGateRow(_slotArea.Content, branch);

                    // 改写门（拦截式）：伤害不发生改为施加指示物——无奖励槽、无预算行
                    if (CardCore.BranchConditionEvaluator.IsRewriteCondition(branch.conditionId))
                    {
                        MakeHint(_slotArea.Content,
                            "改写门：该伤害原子的结算改为对目标施加对应指示物（固定 1 层，伤害不发生）——无奖励槽；差价自动计入卡费");
                        break;
                    }

                    // 奖励槽（单原子 + 预算）
                    var rewardSlot = MakeSlot("奖励（单原子·预算内）", UiStyle.SlotRewardEdge);
                    MarkSelected(rewardSlot, SelKind.GateReward, 0);
                    SelectOnClick(rewardSlot, SelKind.GateReward, 0, false);
                    var reward = branch.thenSteps?.FirstOrDefault();
                    if (reward != null) MakeAtomCard(rewardSlot, reward, 0, SelKind.GateReward);
                    else MakeHint(rewardSlot, "选中后点击库中的奖励原子填入（推导费 ≤ 门预算）");
                    // 校验：奖励必填 + 门预算内
                    _slotZones.Add(MakeZone(rewardSlot, () =>
                    {
                        var r = branch.thenSteps?.FirstOrDefault();
                        if (r == null) return "奖励为空——选中奖励槽后点击库行填入（须在门预算内）";
                        var gate = CurrentGate(branch);
                        if (gate == null) return null;
                        int budget = ComposerCatalog.GateBudget(gate);
                        float cost = RewardCost(r);
                        return cost > budget ? $"奖励超出门预算（{cost:0.#}/{budget}）——调低数值或换原子" : null;
                    }));

                    MakeHint(_slotArea.Content, GateBudgetText(branch));
                    break;
                }
            }

            RefreshName();
            ValidateZones(); // 槽区重建后立即校验
        }

        private static Color SlotBorder(SelKind kind) => kind switch
        {
            SelKind.GateTrunk or SelKind.FreeTrunk => UiStyle.SlotTrunkEdge,
            SelKind.GateReward or SelKind.FreeReward => UiStyle.SlotRewardEdge,
            _ => UiStyle.DropSlotEdge,
        };


        // ======================================== 槽位选中（右侧点击=替换选中槽） ========================================

        private void MarkSelected(RectTransform slot, SelKind kind, int index)
        {
            if (_selSlot == kind && _selSlotIndex == index)
            {
                var img = slot.GetComponent<Image>();
                if (img != null) img.color = new Color(70f / 255f, 110f / 255f, 170f / 255f, 0.32f); // 选中态底（USS .drop-slot--selected）
                var ol = slot.GetComponent<Outline>();
                if (ol != null) ol.effectColor = UiStyle.ChipActiveEdge;
            }
        }

        /// <summary>槽点击=选中（整槽可点；卡片摘要点击会冒泡到槽——选中和展开并存）。</summary>
        private void SelectOnClick(RectTransform slot, SelKind kind, int index, bool disabled)
        {
            var btn = slot.gameObject.GetComponent<Button>();
            if (btn == null)
            {
                btn = slot.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = slot.GetComponent<Image>();
            }
            btn.onClick.AddListener(() =>
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
            RefreshRightPanels(); // 库按选中槽重过滤
        }

        /// <summary>右栏列表刷新：选中槽/门变化后按可用性重过滤。</summary>
        private void RefreshRightPanels()
        {
            RefreshLibrary();
        }

        /// <summary>进入/切模式/载入时的默认选中：并列=原子1；自由分支=奖励槽；有限分支=主干；光环=无槽。</summary>
        private void EnsureDefaultSelection()
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                    _selSlot = SelKind.Parallel;
                    _selSlotIndex = 0;
                    break;
                case ComposeMode.FreeBranch:
                    _selSlot = SelKind.FreeReward; // 主干=下拉直选——库行点击唯一落点=奖励槽
                    _selSlotIndex = 0;
                    break;
                case ComposeMode.OutcomeGate:
                    _selSlot = SelKind.GateTrunk;
                    break;
                case ComposeMode.Aura:
                    _selSlot = SelKind.None;
                    _selSlotIndex = -1;
                    break;
            }
        }

        /// <summary>每次刷新前校正当选中槽（模式变化/删除原子后防悬挂）；并列槽允许 0..min(已填数,2)。</summary>
        private void ClampSelection()
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                    int max = Mathf.Min(_graph.steps?.Count ?? 0, 2);
                    if (_selSlot != SelKind.Parallel)
                    {
                        _selSlot = SelKind.Parallel;
                        _selSlotIndex = 0;
                    }
                    else if (_selSlot == SelKind.Parallel && _selSlotIndex > max) _selSlotIndex = max;
                    break;
                case ComposeMode.FreeBranch:
                    if (_selSlot != SelKind.FreeReward)
                    {
                        _selSlot = SelKind.FreeReward;
                        _selSlotIndex = 0;
                    }
                    break;
                case ComposeMode.OutcomeGate:
                    if (_selSlot != SelKind.GateTrunk && _selSlot != SelKind.GateReward)
                        _selSlot = SelKind.GateTrunk;
                    break;
                case ComposeMode.Aura:
                    _selSlot = SelKind.None;
                    _selSlotIndex = -1;
                    break;
            }
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

        private RectTransform MakeSlot(string title, Color border)
        {
            var slot = UiKit.Column("slot", _slotArea.Content, spacing: 6f, pad: 8f);
            var img = slot.gameObject.AddComponent<Image>();
            img.sprite = UiKit.RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = UiStyle.DropSlotBg;
            img.raycastTarget = true; // 槽整体可点（选中）
            var ol = slot.gameObject.AddComponent<Outline>();
            ol.effectColor = border;
            ol.effectDistance = new Vector2(1.5f, -1.5f);
            UiKit.Size(slot, fw: 1f);
            var lbl = UiKit.Label("slot-title", slot, title, UiStyle.SmallSize, UiStyle.TextDim);
            UiKit.Size(lbl, fw: 1f);
            return slot;
        }

        private static void MakeHint(RectTransform parent, string text)
        {
            var hint = UiKit.Label("hint", parent, text, UiStyle.MiniSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(hint, fw: 1f);
        }

        // 槽内原子卡：摘要行（描述+↑↓删除，点击展开）+ 展开态=内联参数编辑（原检查器内容）。
        private void MakeAtomCard(RectTransform slot, AtomicEffectEntry atom, int index, SelKind selKind)
        {
            bool expanded = _sel == selKind
                && (selKind != SelKind.Parallel || _selIndex == index)
                && selKind != SelKind.FreeTrunk;

            var card = UiKit.Column("atom-card", slot, spacing: 4f);
            var cardBg = card.gameObject.AddComponent<Image>();
            cardBg.sprite = UiKit.RoundedSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = UiStyle.PanelBg;
            UiKit.Size(card, fw: 1f);

            // ---- 摘要行（点击=展开/收起）----
            var head = UiKit.Row("head", card, spacing: 6f, pad: 6f);
            var headBg = head.gameObject.AddComponent<Image>();
            headBg.sprite = UiKit.RoundedSprite;
            headBg.type = Image.Type.Sliced;
            headBg.color = UiStyle.RowBg;
            headBg.raycastTarget = true;
            UiKit.Size(head, fw: 1f, h: 34f);

            var text = UiKit.Label("text", head,
                (expanded ? "▼ " : "▶ ") + AtomText.RenderAtomEntry(atom),
                UiStyle.SmallSize, UiStyle.TextBody);
            UiKit.Size(text, fw: 1f);

            if (selKind == SelKind.Parallel)
            {
                UiKit.MiniButton("up", head, "↑", () => MoveParallel(index, -1), width: 26f);
                UiKit.MiniButton("down", head, "↓", () => MoveParallel(index, 1), width: 26f);
            }
            UiKit.MiniButton("del", head, "删除", () => RemoveAtom(selKind, index), UiStyle.BtnDanger);

            var headBtn = head.gameObject.AddComponent<Button>();
            headBtn.transition = Selectable.Transition.None;
            headBtn.targetGraphic = headBg;
            headBtn.onClick.AddListener(() => ToggleExpand(selKind, index));

            // ---- 展开态：内联编辑（原检查器的 MountKinds 驱动面）----
            if (expanded)
                MakeAtomEditorInline(card, atom, selKind, text, expanded);
        }

        /// <summary>原子内联编辑器（检查器移除后的替代——挂在卡片展开区）。
        /// headLabel=卡片摘要行（值/随机改动原地重渲，不重建槽区——修复滑条拖动中失焦）。</summary>
        private void MakeAtomEditorInline(RectTransform card, AtomicEffectEntry atom, SelKind selKind, TMP_Text headLabel, bool expanded)
        {
            var box = UiKit.Column("editor", card, spacing: 6f, pad: 8f);
            UiKit.Size(box, fw: 1f);

            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type))
            {
                MakeHint(box, $"（原子表引用缺失：{atom.refId ?? "空"}）");
                return;
            }
            var mounts = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
            bool hasValue = (cfg?.Description ?? "").Contains("{value}");

            // 实时描述行（值随机/目标随机动态渲染）
            var desc = UiKit.Label("desc", box, AtomText.Render(cfg, atom, _graph.header),
                UiStyle.MiniSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(desc, fw: 1f);

            // 原地刷新：值/随机改动只更新描述/摘要/顶栏——不再 RefreshSlots 整区重建
            void RefreshTexts()
            {
                desc.text = AtomText.Render(cfg, atom, _graph.header);
                if (headLabel != null)
                    headLabel.text = (expanded ? "▼ " : "▶ ") + AtomText.RenderAtomEntry(atom);
                RefreshName();
                ValidateZones(); // 值/随机改动可能触发预算超限——原地复验
            }

            // ---- 关键词类原子（MountKinds 含 Keyword 位）：只要他人赋予形态——只编辑「赋予持续」
            if (mounts.Contains(MountKind.Keyword))
            {
                if (selKind == SelKind.GateReward && _graph.steps.Count > 1)
                    MakeHint(box, GateBudgetText(_graph.steps[1]));

                // 赋予持续三档（1/2 回合同档=持续到自己回合结束）
                var durChoices = new List<string> { "持续到自己回合结束", "离场清除", "永久" };
                var durVals = new List<int>
                {
                    (int)DurationType.UntilEndOfTurn,
                    (int)DurationType.UntilLeaveBattlefield,
                    (int)DurationType.Permanent,
                };
                int dcur = durVals.IndexOf(_graph.header.Duration);
                var durDd = new UiKit.Dropdown("dd-dur", box, Root, durChoices, dcur >= 0 ? dcur : 0, (idx, _) =>
                {
                    _graph.header.Duration = durVals[Mathf.Max(0, idx)];
                    RefreshTexts(); // 持续档影响计价折扣——费用随改随刷
                }, width: 200f);
                UiKit.Described(durDd.Root.GetComponent<Button>(),
                    "关键词=赋予一个生物该关键词（弹窗选一）；默认持续到自己回合结束，可改离场清除/永久——"
                    + "计价随档变化；卡面自带关键词不经效果合成（走卡牌关键词字段）");
                return; // 数值/随机/目标域/效果设置等编辑项一律不显示
            }

            // Value（原子唯一可编辑数值；无 {value} 模板则只读说明）
            if (hasValue)
            {
                var v = UiKit.IntField("field-value", box, "数值", atom.value, val =>
                {
                    atom.value = val;
                    RefreshTexts();
                }, width: 110f);
                UiKit.Described(v, "原子唯一可编辑数值（名义值——计价与描述按它渲染；结算读 ±随机掷值）");
            }
            else
            {
                MakeHint(box, "无数值参数（该原子 Value 不参与语义）");
            }

            // MountKind 7：数值随机滑条（0-100% → RandomAmplitude；描述即时重渲）
            if (mounts.Contains(MountKind.RandomMount) && hasValue)
            {
                var slider = UiKit.IntSlider("slider-amp", box, "数值随机 %", 0, 100,
                    Mathf.RoundToInt(atom.amp * 100f), val =>
                    {
                        atom.amp = val / 100f;
                        RefreshTexts();
                    });
                UiKit.Described(slider.Slider,
                    "计价按名义数值——随机只影响结算分布（锚点不漂移）；span=round(|名义值|×幅度) 均匀随机");
            }

            // 目标域：下拉+目标随机合并一行；选项按 Polarity 限选；改动即重渲——{target} 按实例域渲染
            var tableKinds = cfg?.GetTargetKindList() ?? new List<int>();
            var allowedKinds = AllowedTargetKinds(cfg, tableKinds);
            if (allowedKinds.Count > 0)
            {
                var row = UiKit.Row("target-row", box, spacing: 6f);
                var labels = new List<string> { "表默认" };
                labels.AddRange(allowedKinds.Select(k => AtomText.TargetKindZhOf((TargetKind)k)));

                int cur = atom.kinds != null && atom.kinds.Count == 1
                    ? 1 + allowedKinds.IndexOf(atom.kinds[0]) : 0;
                var dd = new UiKit.Dropdown("dd-kinds", row, Root, labels, Mathf.Max(0, cur), (idx, _) =>
                {
                    int i = idx - 1;
                    atom.kinds = i < 0 ? null : new List<int> { allowedKinds[i] };
                    RefreshTexts(); // {target} 随实例域变化——描述/摘要原地重渲
                }, width: 170f);
                UiKit.Described(dd.Root.GetComponent<Button>(),
                    "目标域按极性限选：效果区有益只己方侧/有害只对方侧。"
                    + "改动即重渲——{target} 按实例域渲染。目标随机=不弹窗按种子从完整范围抽取（扰魔/潜行仍可被随机命中）");

                // 目标随机（不弹窗·按种子随机选）——与目标域同一行
                var rand = UiKit.Toggle("rand", row, "目标随机", _graph.header.RandomTarget != 0, val =>
                {
                    // 随机移出枚举为正交标志——只翻标志，不再覆写选择模式
                    _graph.header.RandomTarget = val ? 1 : 0;
                    RefreshTexts();
                });
            }

            // 位 2：指示物持续提示
            if (mounts.Contains(MountKind.Counter))
                MakeHint(box, "指示物原子：持续规则由指示物本身承载——效果级持续档仅供参考");

            // 位 8：触发上限锁定
            if (mounts.Contains(MountKind.TriggerCapImmutable))
                MakeHint(box, "触发上限锁定：该原子恒无限（TriggerLimitPerTurn 被覆写，不可限）");

            // 检索按维度档计费提示
            if (type == AtomicEffectType.SearchDeck)
                MakeHint(box, "检索按维度档计费：字符串字段填宣言卡名（ExactCard=3），空=单维度 1");

            // 有限分支奖励：预算行
            if (selKind == SelKind.GateReward && _graph.steps.Count > 1)
                MakeHint(box, GateBudgetText(_graph.steps[1]));

            // 效果设置内联——效果级共用字段随卡展开编辑
            MakeEffectSettingsBox(box);
        }

        // 自由分支主干卡：摘要行（引擎描述+移除）+ 展开态=参数编辑（header 通道无原子条目）。
        private void MakeTrunkCard(RectTransform slot, BranchEngineKind engine, int param)
        {
            bool expanded = _sel == SelKind.FreeTrunk;
            var card = UiKit.Column("trunk-card", slot, spacing: 4f);
            var cardBg = card.gameObject.AddComponent<Image>();
            cardBg.sprite = UiKit.RoundedSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = UiStyle.PanelBg;
            UiKit.Size(card, fw: 1f);

            var head = UiKit.Row("head", card, spacing: 6f, pad: 6f);
            var headBg = head.gameObject.AddComponent<Image>();
            headBg.sprite = UiKit.RoundedSprite;
            headBg.type = Image.Type.Sliced;
            headBg.color = UiStyle.RowBg;
            headBg.raycastTarget = true;
            UiKit.Size(head, fw: 1f, h: 34f);

            var text = UiKit.Label("text", head,
                (expanded ? "▼ " : "▶ ") + AtomText.TrunkText(engine, param),
                UiStyle.SmallSize, UiStyle.TextBody);
            UiKit.Size(text, fw: 1f);

            UiKit.MiniButton("del", head, "移除", () =>
            {
                _graph.header.EngineKind = (int)BranchEngineKind.None;
                _graph.header.EngineParam = 0;
                _sel = SelKind.None;
                RefreshSlots();
            }, UiStyle.BtnDanger);

            var headBtn = head.gameObject.AddComponent<Button>();
            headBtn.transition = Selectable.Transition.None;
            headBtn.targetGraphic = headBg;
            headBtn.onClick.AddListener(() => ToggleExpand(SelKind.FreeTrunk, 0));

            if (expanded)
            {
                var box = UiKit.Column("editor", card, spacing: 6f, pad: 8f);
                UiKit.Size(box, fw: 1f);
                ComposerCatalog.EngineParamRange(engine, out int pMin, out int pMax);
                string label = engine == BranchEngineKind.Countdown ? "回合数（0=自动换算）" : $"参数 x（{pMin}-{pMax}）";
                var field = UiKit.IntField("field-param", box, label, _graph.header.EngineParam, val =>
                {
                    _graph.header.EngineParam = engine == BranchEngineKind.Countdown
                        ? Mathf.Max(0, val)
                        : Mathf.Clamp(val, pMin, pMax);
                    RefreshSlots();
                }, width: 150f);
                UiKit.Described(field,
                    "主干只是条件——奖励在下方奖励槽（单原子，不占卡费）。倒计时 0=按奖励推导费自动换算回合（1费=1回合）；"
                    + "死亡计数/元素充盈/手牌序位的奖励预算=x（数值越大门槛越高、预算越宽）");
                MakeEffectSettingsBox(box);
            }
        }

        private static void MakeReadonlyStepBadge(RectTransform slot, EffectStepData step)
        {
            var badge = UiKit.Column("readonly", slot, spacing: 2f, pad: 6f);
            var bg = badge.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.RoundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = UiStyle.PanelBg;
            UiKit.Size(badge, fw: 1f);
            var text = UiKit.Label("text", badge, step.kind == 2
                ? $"抉择（{step.choices?.Count ?? 0} 模式）——卡组成阶段编辑，此处只读"
                : $"步骤 kind={step.kind}（只读）",
                UiStyle.SmallSize, UiStyle.TextBody, wrap: true);
            UiKit.Size(text, fw: 1f);
        }

        // 门行：条件下拉内联（中文标签带【奖励x】）。
        private void MakeGateRow(RectTransform parent, EffectStepData branch)
        {
            var row = UiKit.Column("gate-row", parent, spacing: 4f, pad: 6f);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.RoundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = UiStyle.PanelBg;
            UiKit.Size(row, fw: 1f);

            var wrap = UiKit.Row("bar", row, spacing: 6f);
            UiKit.Label("label", wrap, "条件：", UiStyle.SmallSize, UiStyle.TextDim);

            var trunkType = ResolveType(_graph.steps[0].atomic);
            var gates = ComposerCatalog.GatesFor(trunkType).ToList();
            if (gates.Count == 0)
            {
                MakeHint(row, "主干不是产出族原子——无法挂产出条件");
            }
            else
            {
                var labels = gates.Select(ComposerCatalog.GateLabel).ToList();
                int cur = gates.FindIndex(g => g.Id == branch.conditionId);
                if (cur < 0) { cur = 0; branch.conditionId = gates[0].Id; }
                var dd = new UiKit.Dropdown("dd-gate", wrap, Root, labels, cur, (idx, _) =>
                {
                    if (idx >= 0 && idx < gates.Count)
                    {
                        branch.conditionId = gates[idx].Id;
                        if (CardCore.BranchConditionEvaluator.IsRewriteCondition(branch.conditionId))
                            branch.thenSteps?.Clear(); // 改写门无奖励槽——切换即清空遗留奖励
                        RefreshSlots(); // 预算行/奖励槽随门刷新
                    }
                }, width: 220f);
            }
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

        /// <summary>自由分支奖励预算文本：死亡计数/元素充盈 预算=x；既有三引擎自平衡无上限。</summary>
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
            }
            RefreshSlots();
        }

        // ---- 落槽资格与写入（点击替换选中槽） ----

        // 并列槽可落：主动位原子且非错边（内容契约：效果区禁错边——错边只能进代价槽）
        private bool ParallelCanDrop(object payload)
            => payload is LibPayload lp && lp.CanBeActiveAtom && !lp.IsWrongSideOnly;

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
            RefreshRightPanels(); // 主干/门变化——库按新门预算重过滤
        }

        private void DropGateReward(LibPayload lp)
        {
            _graph.steps[1].thenSteps = new List<AtomicEffectEntry> { EntryForEffectSlot(lp) };
            if (lp.IsKeywordAtom) ApplyKeywordHeader(); // 关键词=他人赋予形态
            _sel = SelKind.GateReward;
            _selIndex = 0;
            _selSlot = SelKind.GateReward;
            RefreshSlots();
        }

        // ======================================== 效果设置（随卡片展开区内联） ========================================

        /// <summary>效果级编排字段（速度/触发上限/持续/选择模式/数量/落区）——整个效果共用，
        /// 在每个原子卡/主干卡的展开区编辑（写入同一 header）。</summary>
        private void MakeEffectSettingsBox(RectTransform parent)
        {
            var box = UiKit.Column("settings", parent, spacing: 6f, pad: 8f);
            var bg = box.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.RoundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = UiStyle.ListBg;
            UiKit.Size(box, fw: 1f);
            var h = _graph.header;

            var title = UiKit.Label("title", box, "效果设置（整个效果共用）", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(title, fw: 1f);

            var row0 = UiKit.Row("row0", box, spacing: 6f);
            var speed = UiKit.IntField("speed", row0, "速度", h.BaseSpeed, v => h.BaseSpeed = v, width: 100f);
            UiKit.Described(speed, "速度：0=只能自己回合的主阶段发动；1=瞬间基准（对手回合也能发动/响应）——可往上再调");
            var limit = UiKit.IntField("limit", row0, "触发上限", h.TriggerLimitPerTurn, v => h.TriggerLimitPerTurn = v, width: 100f);
            UiKit.Described(limit, "触发上限：触发式每回合次数——0=默认（一回合一次）；N=每回合 N 次（计价连乘 1.2^(N-1)）；-1=显式无限（×1.2³）");
            // 位 8 数据驱动：任一挂载原子含 TriggerCapImmutable → 恒无限、锁输入——表说了算
            if (EffectHasMountBit(MountKind.TriggerCapImmutable))
            {
                limit.interactable = false;
                MakeHint(row0, "触发上限锁定：效果内含位 8 原子——表声明恒无限（装载期覆写 -1）");
            }

            var row1 = UiKit.Row("row1", box, spacing: 6f);
            // 持续档全中文
            var dur = new UiKit.Dropdown("dd-dur", row1, Root,
                DurationChoices.Select(d => d.label).ToList(),
                Mathf.Max(0, Array.FindIndex(DurationChoices, d => d.value == h.Duration)),
                (idx, _) => h.Duration = DurationChoices[Mathf.Max(0, idx)].value, width: 140f);
            UiKit.Described(dur.Root.GetComponent<Button>(),
                "持续：一次性效果选「一次性」；永久持续选「永久」；其余为限时/条件档（1/2 回合已并档）——计价随档折扣");

            var sel = new UiKit.Dropdown("dd-sel", row1, Root,
                SelectionModes.Select(m => m.label).ToList(),
                Mathf.Max(0, SelectionModes.ToList().FindIndex(m => m.value == h.SelectionMode)),
                (idx, _) => h.SelectionMode = SelectionModes[Mathf.Max(0, idx)].value, width: 130f);
            UiKit.Described(sel.Root.GetComponent<Button>(),
                "选择模式：目标怎么选——无目标=不弹窗；单/多范围=弹窗选；全取=不弹对域内全部结算（期望 4 计价）；多范围=多域并集");

            var row2 = UiKit.Row("row2", box, spacing: 6f);
            // -1=任意（玩家自选数量=原子计 0 费+整卡不可作地牌）
            var count = UiKit.IntField("count", row2, "数量", h.TargetCount, v => h.TargetCount = v, width: 100f);
            UiKit.Described(count, "数量：固定选 N 个（0=全部、-1=任意=运行时自选个数——计费 0 且不可作地牌；-2=未声明回落表级）");

            var dropChoices = new List<string> { "战场", "手牌", "牌库" };
            var dropVals = new List<int> { (int)Zone.Battlefield, (int)Zone.Hand, (int)Zone.Deck };
            int di = dropVals.IndexOf(h.SummonDropZone);
            var drop = new UiKit.Dropdown("dd-drop", row2, Root, dropChoices, di >= 0 ? di : 0,
                (idx, _) => h.SummonDropZone = dropVals[Mathf.Max(0, idx)], width: 90f);
            UiKit.Described(drop.Root.GetComponent<Button>(), "落区：衍生物（召唤）的生成位置——战场/手牌/牌库");
        }

        // ======================================== 右：双模式展示区 ========================================

        private void OnReloadAtoms()
        {
            AtomicEffectTable.Reload();
            SetRightMode(RightMode.Atoms); // 内含 RebuildFilterTypeChoices（重置"全部"）+ RefreshLibrary
            int count = _libraryList?.Content?.childCount ?? 0;
            ShowToast($"原子表已重读：{count} 行入列");
        }

        private void OnLoadEffectsTable()
        {
            SetRightMode(RightMode.Effects);
        }

        private void SetRightMode(RightMode mode)
        {
            _rightMode = mode;
            _effectsPanel.gameObject.SetActive(mode == RightMode.Effects);
            _atomPanel.gameObject.SetActive(mode == RightMode.Atoms);

            // 筛选栏两模式共用：分类选项随模式重建（原子库=表行中文名；效果表=内含原子名并集）
            RebuildFilterTypeChoices();

            // 按钮态指示（当前模式高亮）
            var imgAtoms = _reloadAtomsBtn.GetComponent<Image>();
            var imgEffects = _loadEffectsBtn.GetComponent<Image>();
            if (imgAtoms != null) imgAtoms.color = mode == RightMode.Atoms ? UiStyle.BtnPrimary : UiStyle.BtnBg;
            if (imgEffects != null) imgEffects.color = mode == RightMode.Effects ? UiStyle.BtnPrimary : UiStyle.BtnBg;

            if (mode == RightMode.Effects) RefreshEffectsList();
            else RefreshLibrary();
        }

        private void RefreshEffectsList()
        {
            ClearContent(_effectsList.Content);
            EffectsLibrary.Reload(); // 外部可能直改 Effects.json——进效果表即重读
            var effects = EffectLibrarySerializer.LoadAll();
            if (effects.Count == 0)
            {
                MakeHint(_effectsList.Content, "效果库为空——左侧编辑后「保存」即入库");
                return;
            }
            string q = _filterName.text;
            foreach (var fx in effects)
            {
                var captured = fx;
                var parts = EffectAtomCfgs(captured); // 组成部分表行（分类/色点/简称检索共用）

                // 效果分类：任一组成部分的表行中文名命中即入选
                if (_filterTypeZh != null && !parts.Any(c => c?.DisplayName == _filterTypeZh)) continue;

                // 近似检索：名 ∪ id ∪ 组成简称
                if (!string.IsNullOrEmpty(q))
                {
                    string abbr = string.Concat(parts.Select(c => c?.DisplayName ?? ""));
                    if (!(captured.name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                        && !(captured.id ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                        && !abbr.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var row = UiKit.Row("row", _effectsList.Content, spacing: 8f, pad: 6f);
                var bg = UiKit.BgRow(row);
                bg.raycastTarget = true;
                UiKit.Size(row, fw: 1f, h: 34f);

                // 色点：首个组成部分的表色
                UiKit.Dot("dot", row, DotColor(ColorFilter.OfColorName(parts.FirstOrDefault()?.Tags)));

                var name = UiKit.Label("name", row, captured.name, UiStyle.SmallSize, UiStyle.TextBody);
                UiKit.Size(name, fw: 1f);
                UiKit.Label("id", row, captured.id, UiStyle.MiniSize, UiStyle.TextFaint);

                var btn = row.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => LoadEffectFromLibrary(captured));
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
            _activationDropdown.SetIndex(Mathf.Clamp(_graph.header.ActivationType, 0, ActivationNames.Length - 1));
            SyncActivationVisibility();
            RefreshAll();
            ShowToast($"已载入：{_graph.name}（编辑后保存覆盖）");
        }

        // ---- 筛选栏 ----

        /// <summary>分类选项随模式重建并重置为"全部"：原子库=表行中文名（锁定关键词行除外）；
        /// 效果表=库内效果的内含原子名并集。</summary>
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
            _filterTypeDropdown.SetOptions(types, 0);
        }

        /// <summary>OnEnter 里挂分类回调（Build 之后一次）。</summary>
        private void BuildFilterCallbacks()
        {
            _filterTypeDropdown.Changed += (idx, _) =>
            {
                _filterTypeZh = idx <= 0 ? null : _filterTypeChoices[Mathf.Max(0, idx)];
                RefreshLibrary();
                if (_rightMode == RightMode.Effects) RefreshEffectsList();
            };
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

        /// <summary>当前选中槽的可放置性谓词（库只展示可用项——与分类/搜索取交集）。
        /// 附带中文上下文（库顶标签显示）。</summary>
        private Func<LibPayload, bool> CurrentSlotPredicate(out string contextZh)
        {
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
            ClearContent(_libraryList.Content);

            // 光环模式：右栏=光环条目库（点击添加）——条目编辑在左栏，原子库按槽过滤口径不适用
            if (_mode == ComposeMode.Aura)
            {
                RefreshAuraLibrary();
                return;
            }

            var pred = CurrentSlotPredicate(out var context);
            if (_libContext != null)
                _libContext.text = context != null ? $"已按选中槽过滤：{context}" : "";
            foreach (var row in AllTableRows())
            {
                if (!Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) continue;
                // 锁定关键词（攻击/守卫 + 引擎行）直接隐藏——不可组合成效果
                if (IsLockedKeywordRow(row)) continue;
                if (_filterTypeZh != null && row.DisplayName != _filterTypeZh) continue;
                // 近似搜索：中文名 ∪ 描述模板 两列并集
                if (!string.IsNullOrEmpty(_filterName.text)
                    && !(row.DisplayName ?? "").Contains(_filterName.text, StringComparison.OrdinalIgnoreCase)
                    && !(row.Description ?? "").Contains(_filterName.text, StringComparison.OrdinalIgnoreCase)) continue;

                var payload = new LibPayload(type, row);
                // 槽位可用性：与分类/搜索取交集——只展示当前选中槽真正可落的原子
                if (pred != null && !pred(payload)) continue;
                MakeLibraryRow(payload);
            }
        }

        private void MakeLibraryRow(LibPayload payload)
        {
            var row = UiKit.Row("row", _libraryList.Content, spacing: 8f, pad: 6f);
            var bg = UiKit.BgRow(row);
            bg.raycastTarget = true;
            UiKit.Size(row, fw: 1f, h: 34f);

            // 圆点直读表 EffectColor 列（config.Tags 承载颜色名）
            UiKit.Dot("dot", row, DotColor(ColorFilter.OfColorName(payload.Cfg.Tags)));

            var name = UiKit.Label("name", row, payload.Cfg.DisplayName, UiStyle.SmallSize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            UiKit.Label("tpl", row, payload.Cfg.Description, UiStyle.MiniSize, UiStyle.TextFaint);

            // 点击=替换选中槽（QuickAdd）
            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => QuickAdd(payload));
        }

        /// <summary>右栏·光环条目库（点击添加）：属性光环 + 关键词光环（位 10 数据驱动）。
        /// 点击追加到左栏条目列表（可重复）；近似搜索按显示名过滤。</summary>
        private void RefreshAuraLibrary()
        {
            if (_libContext != null)
                _libContext.text = "光环条目（点击添加——属性/关键词；效果层只计条目平价，箭头费挂卡并集后计）";

            void AddRow(string title, string meta, UIColor color, Action onAdd)
            {
                if (!string.IsNullOrEmpty(_filterName.text)
                    && !title.Contains(_filterName.text, StringComparison.OrdinalIgnoreCase)
                    && !meta.Contains(_filterName.text, StringComparison.OrdinalIgnoreCase)) return;
                var row = UiKit.Row("row", _libraryList.Content, spacing: 8f, pad: 6f);
                var bg = UiKit.BgRow(row);
                bg.raycastTarget = true;
                UiKit.Size(row, fw: 1f, h: 34f);

                UiKit.Dot("dot", row, DotColor(color));
                var name = UiKit.Label("name", row, title, UiStyle.SmallSize, UiStyle.TextBody);
                UiKit.Size(name, fw: 1f);
                UiKit.Label("meta", row, meta, UiStyle.MiniSize, UiStyle.TextFaint);

                var btn = row.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => onAdd());
            }

            void AddAura(LinkAuraData entry)
            {
                _graph.header.LinkAuras ??= new List<LinkAuraData>();
                _graph.header.LinkAuras.Add(entry);
                RefreshSlots(); // 左栏条目列表/校验/费用随添加刷新（右栏行保持——可连点重复添加）
            }

            AddRow("属性光环·攻击力", "攻击力修正 stat+value——光环档 1.5/+1，按箭头叠加", UIColor.Red,
                () => AddAura(new LinkAuraData { stat = "Power", value = 1 }));
            AddRow("属性光环·生命值", "生命值修正 stat+value——光环档 1.5/+1，按箭头叠加", UIColor.Green,
                () => AddAura(new LinkAuraData { stat = "Life", value = 1 }));
            foreach (var c in ComposerCatalog.AuraKeywordChoices())
            {
                var captured = c;
                AddRow($"关键词光环·{captured.label}", "关键词持续光环——live-query，断链/离场即失效",
                    ColorFilter.OfKeyword(captured.id),
                    () => AddAura(new LinkAuraData { keyword = captured.id }));
            }
        }

        /// <summary>点击库行 = 替换左侧选中槽的原子；选中槽不合适该原子时 toast 提示。
        /// 错边原子不在效果区落位（代价栏已上移卡组合层）。</summary>
        private void QuickAdd(LibPayload payload)
        {
            // 表级错边原子（域锁错侧——如 弃牌/送墓 锁己方域）：效果区禁错边
            if (payload.IsWrongSideOnly)
            {
                ShowToast("错边原子只能作代价——代价栏已移至卡编辑界面（效果层不组合代价）");
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
                    // 主干=槽内下拉直选——库行点击唯一落点=奖励槽；引擎行已移出库（防御兜底）
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

        /// <summary>库行 → 效果槽条目：关键词类（Grant*）统一为他人赋予形态
        ///（kinds 按位 5/6 推导 + str=运行时关键词 id），其余原样。</summary>
        private static AtomicEffectEntry EntryForEffectSlot(LibPayload payload)
        {
            var entry = payload.Entry();
            if (!payload.IsKeywordAtom) return entry;
            entry.kinds = GrantTargetKinds(payload.Cfg);
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

        /// <summary>关键词授予惯例（他人形态）：Single + 持续到自己回合结束（1/2 回合同档）。</summary>
        private void ApplyKeywordHeader()
        {
            _graph.header.SelectionMode = (int)CardCore.SelectionMode.Single;
            _graph.header.Duration = (int)DurationType.UntilEndOfTurn;
            _graph.header.RandomTarget = 0; // 赋予目标由弹窗选一——不做随机
        }

        /// <summary>当前效果任一挂载原子（并列/主干/奖励/引擎奖励）的表行是否声明了某挂载位（位 8 锁输入用）。</summary>
        private bool EffectHasMountBit(MountKind bit)
        {
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s == null) continue;
                if (s.kind == 0 && AtomHasMountBit(s.atomic, bit)) return true;
                if (s.kind == 1 && s.thenSteps != null && s.thenSteps.Any(a => AtomHasMountBit(a, bit))) return true;
            }
            return _graph.header.AtomicEffects != null
                && _graph.header.AtomicEffects.Any(a => AtomHasMountBit(a, bit));
        }

        private static bool AtomHasMountBit(AtomicEffectEntry a, MountKind bit)
            => ComposerCatalog.HasMountBit(CardCore.Attribute.AtomicEffectTable.GetByHashId(a?.refId), bit);

        /// <summary>关键词授予目标域按 MountKinds 位 5/6 推导：
        /// 含 5（可赋予生物）→ 双方生物 {1,2}；只含 6（可赋予法术）→ null=表行默认域；
        /// 两者皆无 → 沿用旧默认 {1,2}。</summary>
        private static List<int> GrantTargetKinds(AtomicEffectConfig cfg)
        {
            var mounts = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
            if (mounts.Contains(MountKind.GrantOnSpell) && !mounts.Contains(MountKind.GrantOnCreature))
                return null;
            return new List<int> { (int)TargetKind.OwnLivingUnit, (int)TargetKind.EnemyLivingUnit };
        }

        // ======================================== 光环模式（箭头+条目随效果合成，挂卡并集） ========================================

        // 光环费用/箭头计数标签（就地刷新防输入丢焦）
        private TMP_Text _auraCostLabel;
        private TMP_Text _arrowCountLabel;

        /// <summary>光环编辑面板：六向三角箭头选择器 + 光环条目（属性/关键词）+ 光环费用预览。
        /// 作用对象不可指定——运行时 live-query 箭头指向格的当前占据者。</summary>
        private void MakeAuraPanel(RectTransform parent)
        {
            var h = _graph.header;
            h.LinkAuras ??= new List<LinkAuraData>();
            MakeHint(parent, "光环＝连接箭头持续效果：作用对象=箭头指向格的当前占据者（断链/离场即失效）——不可指定作用对象。");

            // ---- 连接箭头（六向三角选择器——三角形围成一圈，选中变蓝白）----
            var arrowsBox = UiKit.Column("arrows", parent, spacing: 4f, pad: 8f);
            var abBg = arrowsBox.gameObject.AddComponent<Image>();
            abBg.sprite = UiKit.RoundedSprite;
            abBg.type = Image.Type.Sliced;
            abBg.color = UiStyle.ListBg;
            UiKit.Size(arrowsBox, fw: 1f);

            var headRow = UiKit.Row("head", arrowsBox, spacing: 8f);
            var ah = UiKit.Label("title", headRow, "连接箭头", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            _arrowCountLabel = UiKit.Label("count", headRow, ArrowCountText(), UiStyle.MiniSize, UiStyle.TextHint);
            var picker = MakeArrowPicker(arrowsBox);
            UiKit.Described(picker.GetComponent<Button>() ?? picker.gameObject.AddComponent<Button>(),
                "连接箭头（预选）：点击朝向切换选中（蓝白=选中）。箭头属卡面资产——效果挂到卡上时与其他光环"
                + "**并集**后落卡面（箭头数取并集），箭头费 ×1.2^(n-1) 在卡层并集后计——效果层不计箭头费；"
                + "方向按持有玩家视角声明，至少配一支");
            // 校验：光环必须搭配至少一支箭头（无箭头=永无受益者）
            _slotZones.Add(MakeZone(arrowsBox, () => (HexDirection)h.ArrowDirections == HexDirection.None
                ? "未选箭头——光环必须搭配至少一支箭头（无箭头=永无受益者）" : null));

            // ---- 光环条目（右栏点击添加，左栏只展示/编辑）----
            var listBox = UiKit.Column("entries", parent, spacing: 4f, pad: 8f);
            var lbBg = listBox.gameObject.AddComponent<Image>();
            lbBg.sprite = UiKit.RoundedSprite;
            lbBg.type = Image.Type.Sliced;
            lbBg.color = UiStyle.ListBg;
            UiKit.Size(listBox, fw: 1f);
            var lh = UiKit.Label("title", listBox, "光环条目（右栏点击添加——属性修正 stat+value / 关键词 keyword）",
                UiStyle.HeaderSize, UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(lh, fw: 1f);
            for (int i = 0; i < h.LinkAuras.Count; i++)
                MakeAuraEntryRow(listBox, i);
            if (h.LinkAuras.Count == 0)
                MakeHint(listBox, "尚无条目——右侧光环条目库点击添加（属性/关键词，可重复）");
            MakeHint(listBox, "坚韧=绿1/条、守护=白1/条、属性=光环档 1.5/+1（效果层只计条目）；箭头费挂卡并集后由卡层计");
            // 校验：至少一条有效光环条目 + 关键词条目须位 10 可挂（消耗型拦截——数据驱动）
            _slotZones.Add(MakeZone(listBox, () =>
            {
                if (ValidAuraCount(h.LinkAuras) == 0)
                    return "无有效光环条目——stat/keyword 至少配一条（空条目保存时剔除）";
                foreach (var a in h.LinkAuras)
                {
                    if (a == null || string.IsNullOrEmpty(a.keyword)) continue;
                    if (!ComposerCatalog.IsAuraMountableKeyword(a.keyword))
                        return $"关键词「{a.keyword}」不可作光环——表行未声明 MountKinds 位 10"
                             + "（消耗型关键词移除即用掉，与光环持续语义冲突）；请改选下拉中的可挂关键词";
                }
                return null;
            }));

            // ---- 费用预览（临时卡只喂箭头+条目——箭头恒 None 只显条目平价）----
            _auraCostLabel = UiKit.Label("aura-cost", parent, AuraCostText(), UiStyle.MiniSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(_auraCostLabel, fw: 1f);
        }

        /// <summary>六向三角选择器：六个三角形围成一圈（自正上方顺时针），选中变蓝白；
        /// 命中区=三角形外更大的透明点击格。整体顺时针偏移 30°（odd-r 尖顶六边形邻居方向=30°+k·60°，
        /// BoardMath.MapArrow：Up→NE…——偏移后三角形指向的即六邻接格真实方向）。</summary>
        private RectTransform MakeArrowPicker(RectTransform parent)
        {
            var h = _graph.header;
            var picker = UiKit.Node("arrow-picker", parent);
            // 绝对定位容器：固定 132x132、不走布局拉伸子节点
            picker.anchorMin = picker.anchorMax = picker.pivot = new Vector2(0.5f, 0.5f);
            picker.sizeDelta = new Vector2(132f, 132f);
            UiKit.Size(picker, w: 132f, h: 132f);
            const float cx = 66f, cy = 66f, r = 47f;
            // 地图网格校准（复测定案 22.5°→30°）
            const float gridAlignDeg = 30f;
            foreach (var (dir, deg, zh) in ArrowDirs)
            {
                float ang = deg + gridAlignDeg;
                float rad = ang * Mathf.Deg2Rad;

                var hit = UiKit.Node("hit-" + zh, picker);
                hit.anchorMin = hit.anchorMax = hit.pivot = new Vector2(0.5f, 0.5f);
                hit.anchoredPosition = new Vector2(cx + Mathf.Sin(rad) * r - 66f, cy - Mathf.Cos(rad) * r - 66f);
                hit.sizeDelta = new Vector2(30f, 30f);
                var hitImg = hit.gameObject.AddComponent<Image>();
                hitImg.color = new Color(90f / 255f, 160f / 255f, 230f / 255f, 0.16f); // 选中底色（未选中淡）
                hitImg.raycastTarget = true;
                var hitBtn = hit.gameObject.AddComponent<Button>();
                hitBtn.transition = Selectable.Transition.None;

                var tri = UiKit.Node("tri", hit);
                tri.anchorMin = tri.anchorMax = tri.pivot = new Vector2(0.5f, 0.5f);
                tri.sizeDelta = new Vector2(20f, 26f);
                tri.localRotation = Quaternion.Euler(0f, 0f, -ang); // UGUI Z 轴逆时针为正
                var triImg = tri.gameObject.AddComponent<Image>();
                triImg.sprite = UiKit.TriangleSprite;
                triImg.raycastTarget = false;

                void Sync()
                {
                    bool on = ((HexDirection)h.ArrowDirections).HasFlag(dir);
                    triImg.color = on ? UiStyle.ArrowActive : new Color(96f / 255f, 104f / 255f, 118f / 255f);
                    hitImg.color = on
                        ? new Color(90f / 255f, 160f / 255f, 230f / 255f, 0.16f)
                        : new Color(0f, 0f, 0f, 0f);
                }
                Sync();
                hitBtn.onClick.AddListener(() =>
                {
                    var cur = (HexDirection)h.ArrowDirections;
                    h.ArrowDirections = (int)(cur.HasFlag(dir) ? cur & ~dir : cur | dir);
                    Sync();
                    if (_arrowCountLabel != null) _arrowCountLabel.text = ArrowCountText();
                    RefreshAuraCost();
                    RefreshName();
                    ValidateZones();
                });
            }
            return picker;
        }

        private string ArrowCountText()
        {
            int n = CountArrowBits((HexDirection)_graph.header.ArrowDirections);
            return n == 0 ? "（未选）" : $"（已选 {n} 支）";
        }

        /// <summary>有效光环条目数（stat/keyword 至少一项非空）。</summary>
        private static int ValidAuraCount(List<LinkAuraData> auras)
            => auras?.Count(a => a != null && (!string.IsNullOrEmpty(a.stat) || !string.IsNullOrEmpty(a.keyword))) ?? 0;

        /// <summary>光环条目行：类型切换（属性/关键词）+ 对应编辑器 + 删除。</summary>
        private void MakeAuraEntryRow(RectTransform parent, int index)
        {
            var aura = _graph.header.LinkAuras[index];
            var row = UiKit.Row("entry", parent, spacing: 6f);
            UiKit.BgRow(row);
            UiKit.Size(row, fw: 1f, h: 32f);

            bool isStat = !string.IsNullOrEmpty(aura.stat);
            var type = new UiKit.Dropdown("dd-type", row, Root,
                new List<string> { "属性", "关键词" }, isStat ? 0 : 1, (idx, _) =>
            {
                if (idx == 0) { aura.stat = "Power"; aura.keyword = null; }
                else { aura.stat = null; aura.keyword = CardCore.Attribute.KeywordRules.Armor; }
                RefreshSlots();
            }, width: 90f);

            if (isStat)
            {
                var stat = new UiKit.Dropdown("dd-stat", row, Root,
                    new List<string> { "攻击力", "生命值" },
                    aura.stat.Equals("Life", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                    (idx, _) =>
                    {
                        aura.stat = idx == 1 ? "Life" : "Power";
                        RefreshAuraCost();
                        RefreshName();
                        ValidateZones();
                    }, width: 90f);
                var val = UiKit.IntField("field-val", row, "值(±)", aura.value, v =>
                {
                    aura.value = v;
                    RefreshAuraCost();
                    RefreshName(); // 自动名含 ±值——原地同步
                    ValidateZones();
                }, width: 80f);
                UiKit.Described(val, "属性光环档 1.5/+1（与换区移除档同锚）；±修正量，按箭头叠加（每支命中箭头各享受一次）");
            }
            else
            {
                // 位 10 数据驱动：只列光环可挂关键词；消耗型不声明位 10 → 不可选——名单在表里不在代码里
                var choices = ComposerCatalog.AuraKeywordChoices();
                var labels = choices.Select(c => c.label).ToList();
                int ci = choices.FindIndex(c => c.id == (aura.keyword ?? ""));
                if (ci < 0 && !string.IsNullOrEmpty(aura.keyword))
                {
                    labels.Add($"{aura.keyword}（不可挂）"); // 存量脏值占位可见——校验区标红促改
                    ci = labels.Count - 1;
                }
                var kw = new UiKit.Dropdown("dd-kw", row, Root, labels, Mathf.Max(0, ci), (idx, _) =>
                {
                    if (idx >= 0 && idx < choices.Count) aura.keyword = choices[idx].id; // 占位项越界不改值
                    RefreshAuraCost();
                    RefreshName();
                    ValidateZones();
                }, width: 110f);
                UiKit.Described(kw.Root.GetComponent<Button>(),
                    "仅列光环可挂关键词（原子表 MountKinds 位 10）——消耗型关键词（移除即用掉，如圣盾/复生/潜行/法术护盾）"
                    + "与光环 live-query 持续语义冲突，表中不声明位 10 即不可挂；坚韧=绿1/条、守护=白1/条");
            }

            UiKit.MiniButton("del", row, "删除", () => { _graph.header.LinkAuras.RemoveAt(index); RefreshSlots(); }, UiStyle.BtnDanger);
        }

        /// <summary>光环费用预览文本（箭头属卡面资产——效果层只预选不计费）。
        /// 只显示条目平价；箭头累乘 ×1.2^(n-1) 在挂卡并集后由卡层计。</summary>
        private string AuraCostText()
        {
            try
            {
                var lines = CardCore.CardCostService.Derive(AuraPreviewCard()).Breakdown
                    .Where(l => l != null && l.Stage == "A").ToList();
                if (lines.Count == 0)
                    return "光环费（条目）：—（未声明条目；坚韧=绿1/条、守护=白1/条、属性=光环档 1.5/+1）";
                var parts = lines.Select(l =>
                    $"{l.Label}＝{l.Value:0.#}{(l.Color.HasValue ? ColorZh(l.Color.Value) : "")}").ToList();
                return "光环费（条目）：" + string.Join("；", parts) + "｜箭头费挂卡并集后计（×1.2^(n-1) 卡级）";
            }
            catch
            {
                return "光环费：—（计算异常）";
            }
        }

        /// <summary>光环计价预览卡：效果级光环无宿主卡——临时 CardData 只承条目；
        /// ArrowDirections 恒 None（箭头=卡面资产，效果层预选不计费）。</summary>
        private CardData AuraPreviewCard()
        {
            var h = _graph.header;
            return new CardData
            {
                CardName = "AURA_PREVIEW",
                Supertype = Cardtype.Enchantment,
                ArrowDirections = HexDirection.None,
                LinkAuras = h.LinkAuras != null && h.LinkAuras.Count > 0
                    ? new List<LinkAuraData>(h.LinkAuras) : new List<LinkAuraData>(),
            };
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

        // ======================================== 校验区（不合规=区域闪烁红框+提示，保存禁用） ========================================

        /// <summary>注册一个校验区（Zone=区域元素；Check 返回 null=合规，否则=错误提示文本）。</summary>
        private ZoneEntry MakeZone(RectTransform zone, Func<string> check)
        {
            return new ZoneEntry
            {
                Zone = zone,
                Outline = zone.GetComponent<Outline>(),
                BaseColor = zone.GetComponent<Outline>()?.effectColor ?? UiStyle.Border,
                Check = check,
            };
        }

        /// <summary>奖励原子推导费（校验/预算行共用口径）。</summary>
        private static float RewardCost(AtomicEffectEntry entry)
        {
            var inst = CardEffectConverter.ConvertAtomForUI(entry);
            return inst != null
                ? CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { inst }) : 0f;
        }

        /// <summary>当前门（主干类型+conditionId 解析；无效回 null）。</summary>
        private ComposerCatalog.GateSpec CurrentGate(EffectStepData branch)
        {
            var gates = ComposerCatalog.GatesFor(ResolveType(_graph.steps[0].atomic)).ToList();
            return gates.FirstOrDefault(g => g.Id == branch.conditionId) ?? gates.FirstOrDefault();
        }

        /// <summary>统一校验收口：违规区域红框（500ms 闪烁见 Tick）+ 区域内红字提示；
        /// 存在任何违规 → 保存按钮禁用。返回 true=全部合规。</summary>
        private bool ValidateZones()
        {
            bool any = false;
            foreach (var z in _slotZones)
            {
                if (z?.Zone == null) continue;
                string err = null;
                try { err = z.Check?.Invoke(); } catch { /* 校验异常按合规处理——不阻塞编辑 */ }
                bool invalid = !string.IsNullOrEmpty(err);
                z.Invalid = invalid;
                if (z.Outline == null) z.Outline = z.Zone.GetComponent<Outline>();
                if (invalid)
                {
                    any = true;
                    if (z.Outline != null)
                        z.Outline.effectColor = _flashOn ? UiStyle.ErrorRed
                            : new Color(UiStyle.ErrorRed.r, UiStyle.ErrorRed.g, UiStyle.ErrorRed.b, 0.28f);
                    if (z.ErrLabel == null)
                    {
                        z.ErrLabel = UiKit.Label("zone-error", z.Zone, "", UiStyle.MiniSize,
                            UiStyle.ErrorText, TextAnchor.UpperLeft, FontStyle.Bold, wrap: true);
                        UiKit.Size(z.ErrLabel, fw: 1f);
                    }
                    z.ErrLabel.text = "⚠ " + err;
                }
                else
                {
                    if (z.Outline != null) z.Outline.effectColor = z.BaseColor;
                    if (z.ErrLabel != null)
                    {
                        var t = z.ErrLabel.transform;
                        t.SetParent(null);
                        UnityEngine.Object.Destroy(z.ErrLabel.gameObject);
                        z.ErrLabel = null;
                    }
                }
            }
            if (_saveBtn != null) _saveBtn.interactable = !any;
            return !any;
        }

        // ======================================== 保存 ========================================

        private void OnSave()
        {
            // 校验兜底：违规时保存按钮已禁用——此处防御性复查
            if (!ValidateZones()) { ShowToast("存在不合规设置——红框区域按提示调整后再保存"); return; }

            // 效果名自动构成（中文名＋组合方式＋中文名；光环=条目×箭头数）——只读展示，保存时落账
            _graph.name = AutoName();
            _graph.header.DisplayName = _graph.name;
            if (_mode == ComposeMode.Aura)
            {
                // 空条目剔除（stat/keyword 双空=无效——与装载期过滤同口径）
                _graph.header.LinkAuras?.RemoveAll(a =>
                    a == null || (string.IsNullOrEmpty(a.stat) && string.IsNullOrEmpty(a.keyword)));
            }

            // 卡编辑模式：写回卡牌并返回（不落效果库文件）
            if (_editingCard != null)
            {
                _editingCard.Effects ??= new List<CardEffectData>();
                var effect = BuildCardEffect();
                if (_editingIndex >= 0 && _editingIndex < _editingCard.Effects.Count)
                    _editingCard.Effects[_editingIndex] = effect;
                else
                    _editingCard.Effects.Add(effect);
                // 光环上移效果层：效果携带的箭头/条目并集入卡面（重算式聚合），计价缓存随之失效
                _editingCard.AggregateEffectAuras(true);
                _editingCard.ResetCache();
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

        /// <summary>编辑图 → 卡内效果（编排字段全量拷贝；引擎通道=header.EngineKind+AtomicEffects）。
        /// 光环字段只在光环形态存续——其余形态防夹带清空。</summary>
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
            if (_mode != ComposeMode.Aura)
            {
                effect.ArrowDirections = 0;
                effect.LinkAuras = null;
            }
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

        private static Color DotColor(UIColor color)
        {
            return color switch
            {
                UIColor.Red => UiStyle.DotRed,
                UIColor.Blue => UiStyle.DotBlue,
                UIColor.Green => UiStyle.DotGreen,
                UIColor.Gray => UiStyle.DotGray,
                UIColor.Black => UiStyle.DotBlack,
                UIColor.White => UiStyle.DotWhite,
                _ => UiStyle.DotGray, // All
            };
        }

        private void ShowToast(string message) => _toast.text = message;

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
            /// 恒为他人赋予形态。</summary>
            public bool IsKeywordAtom => Cfg != null && Mounts.Contains(MountKind.Keyword);

            public bool CanBeActiveAtom => !IsEngineTrunk && Mounts.Contains(MountKind.ActiveEffect);
            public bool CanBeBranchReward => !IsEngineTrunk && Mounts.Contains(MountKind.BranchReward);

            /// <summary>错边原子（内容契约：只能进代价栏）——p≠0 且表级域锁错侧，或 p=0 单侧域锁。
            /// 与 CardEffectConverter 代价校验同口径。</summary>
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
