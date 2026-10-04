using CardCore;
using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.WSA;
using UInputField = TMPro.TMP_InputField;

namespace SynergyUI
{
    /// <summary>
    /// 效果合成界面（2026-10-01 预制体化：静态层级来自 Assets/Art/UI/EffectUI.prefab，
    /// Build 深度按名绑定+闭包接线；槽位区/原子库/效果表/光环面板运行时重建。
    /// 交互/文案/校验语义与历史 UITK 版对齐（修订史见 tag uitk-ui-final 版头注释）；
        /// 2026-10-02 起 uGUI-native：排版按 25645c0^ 的 EffectComposer.uxml/Common.uss 历史稿还原，
        /// 左=编辑栏（模式条+槽位区）/ 右=展示区（原子库 / 效果表双模式）+ 筛选。
        /// 2026-10-05 设置盒/数值区 prefab 静态化（tpl-slot 模板克隆）：速度/作用次数按主动·自动显隐、
        /// 作用范围=并列原子表域交集单选（header.TargetKinds）、目标数量四档下拉；选择模式/持续档/落区收口不再手编。
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

        /// <summary>由数据推断组合形态：引擎通道优先 → 光环声明（含规则光环步）→ 含 kind=1 步骤=有限分支 → 其余并列。</summary>
        public static ComposeMode InferMode(EffectGraphData g)
        {
            if (g?.header != null && g.header.EngineKind != (int)BranchEngineKind.None) return ComposeMode.FreeBranch;
            if (g?.header != null && (g.header.ArrowDirections != 0
                || (g.header.LinkAuras != null && g.header.LinkAuras.Count > 0))) return ComposeMode.Aura;
            if (HasRuleAuraStep(g)) return ComposeMode.Aura;
            if (g?.steps != null && g.steps.Any(s => s?.kind == 1)) return ComposeMode.OutcomeGate;
            return ComposeMode.Parallel;
        }

        // ---- 规则级光环（2026-10-03 定案；2026-10-04 光环改造后 9 行——全局唯一、作用双方、无箭头；普通光环才开箭头预选。
        // 引擎合约=ModifyGameRule 原子挂载体登场效果（str=规则短名），不占 LinkAura 连接位、不写 arrows） ----

        /// <summary>仪典表行 EnumName → 规则短名（RuleAuraComponents 九常量，与原子表规则光环族 9 行一一对应；
        /// 2026-10-04 光环改造：丰盈→HealOverflow、蚕褪承接 DamageCap、疾风→CastSpeedUp、轮回承接 DoubleTurn；
        /// 蚕褪→离散改名（2026-10-04 伤害二值离散化，短名承袭））。</summary>
        private static readonly Dictionary<string, string> RuleAuraIds = new Dictionary<string, string>
        {
            { "三相仪典", RuleAuraComponents.ElementConversion },
            { "血偿仪典", RuleAuraComponents.BloodPact },
            { "丰盈仪典", RuleAuraComponents.HealOverflow },
            { "离散仪典", RuleAuraComponents.DamageCap },
            { "窥渊仪典", RuleAuraComponents.LockRevealed },
            { "归土仪典", RuleAuraComponents.GraveyardPlay },
            { "疾风仪典", RuleAuraComponents.CastSpeedUp },
            { "轮回仪典", RuleAuraComponents.DoubleTurn },
            { "纳川仪典", RuleAuraComponents.HandLimitNoFatigue },
        };

        private static bool IsRuleAuraRow(AtomicEffectConfig cfg)
            => cfg != null && RuleAuraIds.ContainsKey(cfg.EnumName);

        /// <summary>steps 中的规则光环原子步（光环形态专有——SwitchMode 切出即清，形态互斥）。</summary>
        private static bool HasRuleAuraStep(EffectGraphData g)
        {
            if (g?.steps == null) return false;
            foreach (var s in g.steps)
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                if (IsRuleAuraRow(AtomicEffectTable.GetByHashId(s.atomic.refId))) return true;
            }
            return false;
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
        private RectTransform _modeBar, _timingRow;
        private TMP_Text _nameLabel, _libContext;
        private CostSquaresView _costSquares; // 顶栏费用=彩色方格（2026-10-03 定案，公用预制体）
        private Button _deleteBtn;            // 删除当前库效果（仅效果表载入态可用）
        private UInputField _filterName;
        private UiKit.Dropdown _timingDropdown, _activationDropdown, _filterTypeDropdown;
        // 效果级设置盒（2026-10-05 模板化·每次重建随槽克隆）：四下拉+随机 Toggle——
        // 有原子才克隆显示（SyncSettingsPanel，追加在槽位之后）；speed/limit 显隐随 SyncActivationVisibility 联动
        private TMP_Dropdown _ddSpeed, _ddLimit, _ddKinds, _ddNum;
        private Toggle _tgRand;
        private Action _settingsRefresh; // 当前展开卡的文本重渲（设置盒目标行写入联动）
        private TMP_Text _tableToggleLabel;      // 合并表切换按钮的标签（原 btn-reload-atoms/btn-load-effects）
        private List<TriggerTiming> _timings;

        // 筛选状态：原子库=表行中文名；效果表=内含原子名。近似搜索：原子库=中文名∪描述模板；效果表=名∪id∪组成简称
        private string _filterTypeZh; // null = 全部
        private List<string> _filterTypeChoices = new List<string> { "全部" };

        // 发动方式（2026-10-04 定案：强制退出玩家选择——留系统自用[分支/光环/宣判锁档]，
        // 玩家只能在 自动/主动 上选；数据层 Mandatory 照旧装载）。
        // EffectActivationType 原值：0=强制(系统) 1=自动 2=主动——下拉显示序与枚举解耦。
        private static readonly (string label, int value)[] ActivationChoices =
        {
            ("自动", (int)EffectActivationType.Automatic),
            ("主动", (int)EffectActivationType.Voluntary),
        };

        /// <summary>ActivationType → 下拉显示序（系统强制档显示为"自动"占位——不写回，改选才落值）。</summary>
        private static int ActivationIndex(int activationType)
        {
            for (int i = 0; i < ActivationChoices.Length; i++)
                if (ActivationChoices[i].value == activationType) return i;
            return 0;
        }

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

        // 设置盒档位表（2026-10-05 七项定案+下拉化）：速度仅主动三档；作用次数仅非主动（-1=全部/不限）；
        // 目标数量 0=全部（作用范围内全取）。选择模式由 目标数×作用范围 推导（CardEffectConverter）——不再手选
        private static readonly int[] SpeedTiers = { 1, 2, 3 };
        private static readonly int[] LimitTiers = { 1, 2, 3, -1 };
        private static readonly int[] CountTiers = { 1, 2, 3, 0 };

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

        /// <summary>锁定关键词行（移出原子库、不可组合、不展示）：攻击/守卫 + 位 9 引擎行
        /// + 位 11 系统内部行（2026-10-04：修改攻击力/生命值/费用——留给系统，不暴露给玩家组合）。</summary>
        private static bool IsLockedKeywordRow(AtomicEffectConfig r)
            => r != null && (IsFixedBattleAtom(r.EnumName) || IsEngineTrunkRow(r)
                || ComposerCatalog.HasMountBit(r, MountKind.SystemInternal));

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

        protected override string PrefabName => "EffectUI";
        protected override string RootName => "effect-composer";

        protected override void Build()
        {
            // ---- 顶栏 ----
            BindButton("btn-back", () => Manager.Back());
            _nameLabel = FindText("lbl-effect-name");
            // 费用显示=彩色方格（2026-10-03 定案）：Mount 组件优先——烘焙的 CostSquares 实例
            // （拖入 prefab 后由引用者自由命名，如 lbl-effect-cost）直接复用；位置/整体缩放归 prefab
            _costSquares = CostSquaresView.Mount(Find("topbar") ?? Root, "cost-squares");
            _activationDropdown = BindDropdown("dropdown-activation", Root, Find("topbar"),
                ActivationChoices.Select(c => c.label).ToList(), 0, width: 90f);
            _timingDropdown = BindDropdown("dropdown-timing", Root, Find("timing-row"),
                new List<string> { "—" }, 0, width: 140f);
            // 手改预制体后时机行并入 dropdown-timing 节点本体（TMP 挂节点上）——
            // timing-row 不存在时"主动档隐藏时机"退回驱动下拉节点自身
            _timingRow = Find("timing-row") ?? _timingDropdown?.Root;
            // 表切换按钮（原 btn-reload-atoms/btn-load-effects 两钮合并）：按下在 原子库↔效果表 间切换，
            // 标签常显"将切到哪边"。烘焙节点名 btn-switch-table（prefab 接管后删旧两钮）。
            UiKit.BindableButton("btn-switch-table", Root, "显示效果表",
                SwitchTable, UiStyle.BtnPrimary, UiStyle.White, Find("topbar"));
            _tableToggleLabel = UiKit.FindDeep(Root, "btn-switch-table")?.GetComponentInChildren<TMP_Text>(true);
            _saveBtn = BindButton("btn-save", OnSave);
            // 删除按钮：仅"从效果表载入的库效果"可删（卡编辑/新建态禁用）——prefab 烘焙 btn-delete 优先
            _deleteBtn = UiKit.BindableButton("btn-delete", Root, "删除", OnDeleteEffect,
                UiStyle.BtnDanger, UiStyle.White, Find("topbar"));

            // ---- 主体 ----
            _modeBar = Find("mode-bar");
            _slotArea = FindScroll("slot-area");
            PrepareSlotTemplates(); // prefab 烘焙的 slot/settings 样板转隐藏模板（克隆用）

            // 描述条（左栏底部——选中才出现）
            EnsureDescStrip();

            // 右：展示区（双模式）
            _filterTypeDropdown = BindDropdown("dropdown-filter-type", Root, Find("filter-row"),
                new List<string> { "全部" }, 0, width: 150f);
            _filterName = FindInput("field-filter-name");
            if (_filterName != null)
                _filterName.onValueChanged.AddListener(_ =>
                {
                    RefreshLibrary();
                    if (_rightMode == RightMode.Effects) RefreshEffectsList();
                });

            _libContext = FindOptional("lbl-lib-context")?.GetComponentInChildren<TMP_Text>(true);
            _libraryList = FindScroll("list-library"); // 2026-10-03 手改预制体重命名（原 library-list）
            _effectsList = FindScroll("list-effects");

            // 校验红框闪烁 + 描述条路由 + 泵：挂在屏根（层级销毁自动失效）
            UiKit.Updater.Attach(Root, Tick);
            UiKit.DescRequested += OnDescRequested;
        }

        /// <summary>描述条绑定/补建（预制体烘焙优先；缺失时在左栏代码补建原样式）。</summary>
        private void EnsureDescStrip()
        {
            _descStripNode = UiKit.FindDeep(Root, "desc-strip");
            var lblRt = UiKit.FindDeep(_descStripNode, "lbl-prop-desc");
            _descStrip = lblRt != null
                ? lblRt.GetComponent<TMP_Text>()
                : UiKit.Label("lbl-prop-desc", _descStripNode, "", UiStyle.MiniSize,
                    UiStyle.DescStrip, TextAnchor.UpperLeft, wrap: true);
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
            _timingDropdown.Describe(
                "触发时机：效果在哪个时点自动入栈（条件发动不走速度，满足即入栈）；主动发动方式下不设时机");
        }

        private void SyncTimingDropdown()
        {
            int current = _timings.IndexOf((TriggerTiming)_graph.header.TriggerTiming);
            _timingDropdown.SetIndex(current < 0 ? 0 : current);
        }

        private void BuildActivationDropdown()
        {
            _activationDropdown.SetOptions(ActivationChoices.Select(c => c.label).ToList(), ActivationIndex(_graph.header.ActivationType));
            SyncActivationVisibility();
            _activationDropdown.Describe(
                "发动方式（2026-10-04 起强制档退出玩家选择、留系统自用）——自动：条件达成时弹窗询问是否发动（可选触发式）；主动：只能在自己回合的主要阶段主动发动（=启动式：构筑期不计元素锚价、发动时现付+横置，2026-10-02 定案）");
            _activationDropdown.Changed += (idx, _) =>
            {
                if (idx >= 0 && idx < ActivationChoices.Length)
                    _graph.header.ActivationType = ActivationChoices[idx].value;
                SyncActivationVisibility();
            };
        }

        /// <summary>发动方式/时机的可见性与档位锁定（2026-10-03 规则定案）：
        /// 并列=可选三态（主动档时机钉 Activate_*、离开回落 OnPlay——2026-10-02 定案不变）；
        /// 分支（自由/有限）=发动方式固定强制、不可选发动时机（时机由各分支的执行条件承载）
        /// ——残留主动档回落 OnPlay；光环=无时机/方式（载体入场即生效，离场/被无效即失效）
        /// ——时机钉 OnPlay。锁定形态隐藏两下拉并写死档位。</summary>
        private void SyncActivationVisibility()
        {
            if (_timingRow == null && _activationDropdown == null) return;
            bool branch = _mode == ComposeMode.FreeBranch || _mode == ComposeMode.OutcomeGate;
            bool aura = _mode == ComposeMode.Aura;
            bool metaLocked = branch || aura;
            var h = _graph.header;

            if (metaLocked)
            {
                h.ActivationType = 0; // 强制档（分支定案；光环=入场生效无需发动方式）
                _activationDropdown?.SetIndex(0);
                if (aura) h.TriggerTiming = (int)TriggerTiming.OnPlay;
                else if (h.TriggerTiming == (int)TriggerTiming.Activate_Active
                         || h.TriggerTiming == (int)TriggerTiming.Activate_Instant
                         || h.TriggerTiming == (int)TriggerTiming.Activate_Response)
                    h.TriggerTiming = (int)TriggerTiming.OnPlay; // 时机在分支执行条件里——残留主动档回落
            }
            else
            {
                bool voluntary = h.ActivationType == 2;
                int t = h.TriggerTiming;
                if (voluntary)
                {
                    if (t != (int)TriggerTiming.Activate_Active
                        && t != (int)TriggerTiming.Activate_Instant
                        && t != (int)TriggerTiming.Activate_Response)
                        h.TriggerTiming = (int)TriggerTiming.Activate_Active;
                }
                else if (t == (int)TriggerTiming.Activate_Active
                         || t == (int)TriggerTiming.Activate_Instant
                         || t == (int)TriggerTiming.Activate_Response)
                {
                    h.TriggerTiming = (int)TriggerTiming.OnPlay;
                }
            }

            if (_activationDropdown?.Root != null)
                _activationDropdown.Root.gameObject.SetActive(!metaLocked);
            _timingRow?.gameObject.SetActive(!metaLocked && h.ActivationType != 2);

            SyncSettingsRowVisibility(); // 发动行显隐联动（设置盒随最新绑定卡对齐）
        }

        /// <summary>发动行显隐（2026-10-05 定案）：主动=速度档（自动响应无速度）；非主动（自动/系统）=作用次数档。</summary>
        private void SyncSettingsRowVisibility()
        {
            bool voluntary = _graph.header.ActivationType == (int)EffectActivationType.Voluntary;
            if (_ddSpeed != null) _ddSpeed.gameObject.SetActive(voluntary);
            if (_ddLimit != null) _ddLimit.gameObject.SetActive(!voluntary);
        }

        // ======================================== 效果名自动构成 ========================================

        /// <summary>自动名 = 中文名＋组合方式＋中文名（并列"＋"、两分支"→"；空槽以"…"占位；
        /// 光环=条目列表×箭头数；规则级光环=仪典名（双方生效·全局唯一，无箭头）。</summary>
        private string AutoName()
        {
            var h = _graph.header;
            if (_mode == ComposeMode.Aura)
            {
                var ruleParts = new List<string>();
                foreach (var s in _graph.steps ?? new List<EffectStepData>())
                {
                    if (s?.kind != 0 || s.atomic == null) continue;
                    var rc = AtomicEffectTable.GetByHashId(s.atomic.refId);
                    if (IsRuleAuraRow(rc)) ruleParts.Add(rc.EnumName);
                }
                if (ruleParts.Count > 0)
                    return $"规则光环[{string.Join("，", ruleParts)}]（双方生效·全局唯一）";

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
            // 顶栏效果名=完整效果描述（2026-10-03 定案：不再简写组合名——空效果回落自动名占位）
            var full = AtomText.RenderEffectSummary(_graph);
            _nameLabel.text = string.IsNullOrEmpty(full) ? AutoName() : full;
            _costSquares?.SetCosts(AutoCosts());
        }

        /// <summary>自动费用预览=**效果锚价**（效果组合阶段纯表累加、无减免抵消——ConvertOne +
        /// DeriveElementCosts 实时推导，含改写门差价；代价不参与——代价栏已上移卡组合层）。
        /// 2026-10-03 起以彩色方格呈现（原「红2 灰1」文本口径同源）；2026-10-04 位置数组口径。</summary>
        private ElementCost AutoCosts()
        {
            try
            {
                var def = CardEffectConverter.ConvertOne(BuildCardEffect(), "COMPOSER_COST_PREVIEW");
                if (def == null) return null;
                var costs = CostDerivationService.DeriveElementCosts(def, 0);
                return costs == null || costs.IsZero ? null : costs;
            }
            catch
            {
                return null;
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
            SyncActivationVisibility(); // 显隐随形态（分支/光环锁定——2026-10-03 定案）
            UpdateDeleteState();
            RefreshSlots();
            RefreshLibrary();
            RefreshName();
        }

        /// <summary>删除按钮可用性：仅"从效果表载入的库效果"（卡编辑/新建态禁用）。</summary>
        private void UpdateDeleteState()
        {
            if (_deleteBtn != null)
                _deleteBtn.interactable = _editingCard == null && !string.IsNullOrEmpty(_loadedFromLibraryId);
        }

        /// <summary>删除当前载入的库效果（btn-delete）：按源 id 清档后重置为新建空效果。
        /// 不重建两下拉——防 Changed 重复订阅（Build*Dropdown 只在进屏时挂一次）。</summary>
        private void OnDeleteEffect()
        {
            if (_editingCard != null || string.IsNullOrEmpty(_loadedFromLibraryId)) return;
            var removed = _loadedFromLibraryId;
            EffectLibrarySerializer.DeleteById(removed);
            _loadedFromLibraryId = null;
            _graph = CardEffectToGraph(null);
            _mode = InferMode(_graph);
            _sel = SelKind.None;
            _selIndex = -1;
            EnsureDefaultSelection();
            SyncTimingDropdown();
            SyncActivationVisibility();
            RefreshAll();
            SetRightMode(RightMode.Atoms);
            ShowToast($"效果 {removed} 已从效果库删除");
        }

        private void RefreshModeBar()
        {
            if (_modeBar == null) return;
            // 不清重建：模式片按名绑定烘焙节点（mode-Parallel 等；无则建一次），刷新只更新文本/态色/接线
            MakeModeChip("并列（1-3 原子）", ComposeMode.Parallel);
            MakeModeChip("自由分支（引擎条件）", ComposeMode.FreeBranch);
            MakeModeChip("有限分支（产出条件）", ComposeMode.OutcomeGate);
            MakeModeChip("光环（连接箭头）", ComposeMode.Aura);

            // 卡编辑模式提示（常驻节点按名绑定——非编辑时隐藏；新建才定宽，烘焙宽以节点为准）
            bool tagExisted = _modeBar.Find("editing-tag") != null;
            var tag = UiKit.BindableLabel("editing-tag", _modeBar,
                _editingCard != null ? $"正在编辑：{_editingCard.CardName} #{_editingIndex + 1}" : "",
                UiStyle.MiniSize, UiStyle.TextHint);
            var tagNode = _modeBar.Find("editing-tag");
            if (tagNode != null) tagNode.gameObject.SetActive(_editingCard != null);
            if (!tagExisted && tag != null) UiKit.Size(tag, w: 200f);
        }

        private void MakeModeChip(string label, ComposeMode mode)
        {
            bool active = mode == _mode;
            UiKit.BindableButton($"mode-{mode}", _modeBar, label, () => SwitchMode(mode),
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

            // 并列相同目标（2026-10-05）：效果级作用范围只在并列形态存续——切出即清防夹带
            if (newMode != ComposeMode.Parallel) h.TargetKinds = null;

            _mode = newMode;
            _sel = SelKind.None;
            _selIndex = -1;
            EnsureDefaultSelection();
            // 自由分支切入且未选引擎——展开主干卡（引擎下拉在卡内，2026-10-05 prefab 静态化）
            if (newMode == ComposeMode.FreeBranch && _graph.header.EngineKind == (int)BranchEngineKind.None)
                _sel = SelKind.FreeTrunk;
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
            _settingsRefresh = null; // 展开卡重绑前清（MakeAtomCard/MakeTrunkCard 重新登记）
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
                        else if (i == _graph.steps.Count) firstEmptySlot ??= slot; // 空槽提示已去——slot-title 即空槽位
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
                    // 主干槽（引擎条件）：引擎下拉移入主干卡内（2026-10-05 prefab 静态化）——未选引擎卡也常驻
                    var trunkSlot = MakeSlot("主干（条件引擎）", UiStyle.SlotTrunkEdge);
                    MakeTrunkCard(trunkSlot, (BranchEngineKind)h.EngineKind, h.EngineParam);
                    // 校验：主干引擎必选
                    _slotZones.Add(MakeZone(trunkSlot, () => h.EngineKind == (int)BranchEngineKind.None
                        ? "未选择条件引擎——展开主干卡选择（拼点/运势/倒计时/死亡计数/元素充盈/手牌序位）" : null));

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

            SyncSettingsPanel(); // 设置盒显隐/重置（content 级单例——原子槽有无原子）
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

        /// <summary>槽高亮（2026-10-05 实测修正：与展开卡一致）——展开卡所在槽即高亮；
        /// 无展开时回落选中槽（库点击落点提示）。</summary>
        private void MarkSelected(RectTransform slot, SelKind kind, int index)
        {
            bool expandedHere = _sel == kind && (kind != SelKind.Parallel || _selIndex == index)
                && kind != SelKind.FreeTrunk; // 与 MakeAtomCard 展开判定同式
            bool selected = _sel == SelKind.None && _selSlot == kind && _selSlotIndex == index;
            if (expandedHere || selected)
            {
                var img = slot.GetComponent<Image>();
                if (img != null) img.color = new Color(70f / 255f, 110f / 255f, 170f / 255f, 0.32f); // 焦点态底（USS .drop-slot--selected）
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

        /// <summary>槽位/设置盒模板接管（2026-10-05 prefab 静态化）：prefab 在 slot-area/content 烘焙了
        /// slot 样板（slot-title + atom-card[head/editor]）与 settings 样板（row0 目标行+row1 发动行）
        /// ——Build 时分别转隐藏模板 tpl-slot / tpl-settings（ClearChildren 放行 tpl-* 不销毁）；
        /// MakeSlot 每槽克隆，SyncSettingsPanel 有原子时克隆 settings 追加在槽位之后；缺失回退旧运行时构建。</summary>
        private void PrepareSlotTemplates()
        {
            var content = _slotArea?.Content;
            if (content == null) return;
            var tpl = content.Find("slot") as RectTransform;
            if (tpl != null)
            {
                tpl.name = "tpl-slot";
                tpl.gameObject.SetActive(false);
                // 模板内 atom-card 预先收起（克隆体由 MakeAtomCard 按需激活；editor 展开态才激活）
                var card = tpl.Find("atom-card");
                if (card != null) card.gameObject.SetActive(false);
            }
            var settings = content.Find("settings");
            if (settings != null)
            {
                settings.name = "tpl-settings";
                settings.gameObject.SetActive(false);
            }
            var gateRow = content.Find("gate-row");
            if (gateRow != null)
            {
                gateRow.name = "tpl-gate-row"; // 限定分支门槛行（dd-trunk=条件下拉）
                gateRow.gameObject.SetActive(false);
            }
        }

        private RectTransform MakeSlot(string title, Color border)
        {
            var slot = UiKit.CloneTemplate("tpl-slot", _slotArea.Content);
            if (slot == null)
            {
                slot = UiKit.Column("slot", _slotArea.Content, spacing: 6f, pad: 8f);
            }
            var img = slot.GetComponent<Image>();
            if (img == null)
            {
                img = slot.gameObject.AddComponent<Image>();
                img.sprite = UiKit.RoundedSprite;
                img.type = Image.Type.Sliced;
                img.color = UiStyle.DropSlotBg;
            }
            img.raycastTarget = true; // 槽整体可点（选中）
            var ol = slot.GetComponent<Outline>() ?? slot.gameObject.AddComponent<Outline>();
            ol.effectColor = border;
            ol.effectDistance = new Vector2(1.5f, -1.5f);
            UiKit.Size(slot, fw: 1f);
            var lbl = UiKit.FindDeep(slot, "slot-title")?.GetComponent<TMP_Text>()
                ?? UiKit.Label("slot-title", slot, title, UiStyle.SmallSize, UiStyle.TextDim);
            lbl.text = title;
            UiKit.Size(lbl, fw: 1f);
            return slot;
        }

        private static void MakeHint(RectTransform parent, string text)
        {
            var hint = UiKit.Label("hint", parent, text, UiStyle.MiniSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(hint, fw: 1f);
        }

        // 槽内原子卡：摘要行（描述+↑↓删除，点击展开）+ 展开态=内联编辑（prefab 静态模板：head/settings/editor）。
        private void MakeAtomCard(RectTransform slot, AtomicEffectEntry atom, int index, SelKind selKind)
        {
            bool expanded = _sel == selKind
                && (selKind != SelKind.Parallel || _selIndex == index)
                && selKind != SelKind.FreeTrunk;

            var card = slot.Find("atom-card") as RectTransform;
            if (card == null)
            {
                Debug.LogError("[EffectComposer] 槽内缺 atom-card 模板节点（2026-10-05 prefab 静态化）——检查 EffectUI.prefab");
                card = UiKit.Column("atom-card", slot, spacing: 4f);
                UiKit.Size(card, fw: 1f);
            }
            card.gameObject.SetActive(true);

            // 展开区（editor）默认收起——展开卡在下方按需激活（设置盒=content 级单例，不随卡）
            UiKit.FindDeep(card, "editor")?.gameObject.SetActive(false);

            var head = UiKit.FindDeep(card, "head");
            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>()
                ?? (head != null ? UiKit.Label("text", head, "", UiStyle.SmallSize, UiStyle.TextBody) : null);
            if (text != null)
            {
                text.text = (expanded ? "▼ " : "▶ ") + AtomText.RenderAtomEntry(atom, _graph.header);
                UiKit.Size(text, fw: 1f);
            }

            if (head != null)
            {
                bool par = selKind == SelKind.Parallel;
                BindCardButton(UiKit.FindDeep(card, "up"), "↑", par, () => MoveParallel(index, -1));
                BindCardButton(UiKit.FindDeep(card, "down"), "↓", par, () => MoveParallel(index, 1));
                BindCardButton(UiKit.FindDeep(card, "del"), "删除", true, () => RemoveAtom(selKind, index));
                var headBtn = head.gameObject.GetComponent<Button>() ?? head.gameObject.AddComponent<Button>();
                headBtn.transition = Selectable.Transition.None;
                headBtn.targetGraphic = head.GetComponent<Image>();
                headBtn.onClick.RemoveAllListeners();
                headBtn.onClick.AddListener(() => ToggleExpand(selKind, index));
            }

            if (expanded)
                _settingsRefresh = MakeAtomEditorInline(card, atom, selKind, text); // 设置盒目标行写入联动重渲
        }

        /// <summary>prefab 烘焙的卡片头部小按钮绑定（组件缺失补建；visible=false 直接隐藏）。</summary>
        private static void BindCardButton(RectTransform node, string label, bool visible, Action onClick)
        {
            if (node == null) return;
            node.gameObject.SetActive(visible);
            if (!visible) return;
            var lbl = node.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = label;
            var btn = node.gameObject.GetComponent<Button>() ?? node.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = node.GetComponent<Image>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick());
        }

        /// <summary>原子内联编辑器（prefab 静态 editor 节点：field-value 输入 + slider-amp 滑条 + placeholder 注入位）。
        /// headLabel=卡片摘要行（值/随机改动原地重渲，不重建槽区——修复滑条拖动中失焦）。
        /// 返回=RefreshTexts（设置盒目标行联动重渲用）。</summary>
        private Action MakeAtomEditorInline(RectTransform card, AtomicEffectEntry atom, SelKind selKind, TMP_Text headLabel)
        {
            var editor = UiKit.FindDeep(card, "editor");
            if (editor == null)
            {
                Debug.LogError("[EffectComposer] atom-card 缺 editor 节点——数值/随机不可编辑");
                return null;
            }
            editor.gameObject.SetActive(true);
            // 主干引擎下拉（dd-trunk-title/dd-trunk）=自由分支主干卡专有——原子卡一律隐藏
            UiKit.FindDeep(editor, "dd-trunk-title")?.gameObject.SetActive(false);
            UiKit.FindDeep(editor, "dd-trunk")?.gameObject.SetActive(false);
            var inject = UiKit.FindDeep(editor, "placeholder") ?? editor; // hints/关键词三档 注入位

            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type))
            {
                MakeHint(inject, $"（原子表引用缺失：{atom.refId ?? "空"}）");
                return null;
            }
            var mounts = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
            bool hasValue = (cfg?.Description ?? "").Contains("{value}");

            // 原地刷新：值/随机改动只更新摘要/顶栏——不再 RefreshSlots 整区重建
            // （描述行已去重——head 摘要即 RenderAtomEntry 全量渲染）
            void RefreshTexts()
            {
                if (headLabel != null)
                    headLabel.text = "▼ " + AtomText.RenderAtomEntry(atom, _graph.header);
                RefreshName();
                ValidateZones(); // 值/随机改动可能触发预算超限——原地复验
            }

            var valueNode = UiKit.FindDeep(editor, "field-value");
            var sliderNode = UiKit.FindDeep(editor, "slider-amp");

            // ---- 关键词类原子（MountKinds 含 Keyword 位）：只要他人赋予形态——只编辑「赋予持续」 ----
            if (mounts.Contains(MountKind.Keyword))
            {
                valueNode?.gameObject.SetActive(false);
                sliderNode?.gameObject.SetActive(false);
                if (selKind == SelKind.GateReward && _graph.steps.Count > 1)
                    MakeHint(inject, GateBudgetText(_graph.steps[1]));

                // 赋予持续三档（1/2 回合同档=持续到自己回合结束；文案"换区清除"=2026-10-05 七项定案）——持续档只在赋予类保留
                var durChoices = new List<string> { "持续到自己回合结束", "换区清除", "永久" };
                var durVals = new List<int>
                {
                    (int)DurationType.UntilEndOfTurn,
                    (int)DurationType.UntilLeaveBattlefield,
                    (int)DurationType.Permanent,
                };
                int dcur = durVals.IndexOf(_graph.header.Duration);
                var durDd = new UiKit.Dropdown("dd-dur", inject, Root, durChoices, dcur >= 0 ? dcur : 0, (idx, _) =>
                {
                    _graph.header.Duration = durVals[Mathf.Max(0, idx)];
                    RefreshTexts(); // 持续档影响计价折扣——费用随改随刷
                }, width: 200f);
                UiKit.Described(durDd.Root.GetComponent<Button>(),
                    "关键词=赋予一个生物该关键词（弹窗选一）；默认持续到自己回合结束，可改离场清除/永久——"
                    + "计价随档变化；卡面自带关键词不经效果合成（走卡牌关键词字段）");
                return RefreshTexts; // 数值/随机不显示；效果级设置盒照常（MakeAtomCard 层绑定）
            }

            // ---- Value（TMP 输入框静态绑定；无 {value} 模板则隐藏+只读说明）----
            bool showValue = hasValue;
            if (valueNode != null)
            {
                valueNode.gameObject.SetActive(showValue);
                if (showValue)
                {
                    var input = valueNode.GetComponent<TMP_InputField>()
                        ?? valueNode.GetComponentInChildren<TMP_InputField>(true);
                    if (input != null)
                    {
                        input.contentType = TMP_InputField.ContentType.IntegerNumber;
                        input.text = atom.value.ToString();
                        input.onEndEdit.RemoveAllListeners();
                        input.onEndEdit.AddListener(_ =>
                        {
                            if (int.TryParse(input.text, out var v))
                            {
                                atom.value = v;
                                RefreshTexts();
                            }
                            else input.text = atom.value.ToString();
                        });
                        UiKit.Described(input, "原子唯一可编辑数值（名义值——计价与描述按它渲染；结算读 ±随机掷值）");
                    }
                }
            }
            if (!showValue) MakeHint(inject, "无数值参数（该原子 Value 不参与语义）");

            // ---- MountKind 7：数值随机滑条（uGUI Slider 静态绑定；0-100% → RandomAmplitude）----
            bool showAmp = mounts.Contains(MountKind.RandomMount) && hasValue;
            if (sliderNode != null)
            {
                sliderNode.gameObject.SetActive(showAmp);
                if (showAmp)
                {
                    var sl = sliderNode.GetComponent<Slider>() ?? sliderNode.GetComponentInChildren<Slider>(true);
                    if (sl != null)
                    {
                        sl.minValue = 0;
                        sl.maxValue = 100;
                        sl.wholeNumbers = true;
                        sl.SetValueWithoutNotify(Mathf.RoundToInt(atom.amp * 100f));
                        sl.onValueChanged.RemoveAllListeners();
                        sl.onValueChanged.AddListener(v =>
                        {
                            atom.amp = v / 100f;
                            RefreshTexts();
                        });
                        var slLbl = UiKit.FindDeep(sliderNode, "label")?.GetComponent<TMP_Text>();
                        if (slLbl != null) slLbl.text = "数值随机 %";
                        UiKit.Described(sl,
                            "计价按名义数值——随机只影响结算分布（锚点不漂移）；span=round(|名义值|×幅度) 均匀随机");
                    }
                }
            }

            // 原子级目标域下拉/目标随机已上移设置盒（2026-10-05 并列相同目标定案——作用范围升级效果级 TargetKinds）

            // 位 2：指示物持续提示
            if (mounts.Contains(MountKind.Counter))
                MakeHint(inject, "指示物原子：持续规则由指示物本身承载——效果级持续档仅供参考");

            // 位 8：触发上限锁定（设置盒作用次数档同步锁死）
            if (mounts.Contains(MountKind.TriggerCapImmutable))
                MakeHint(inject, "触发上限锁定：该原子恒无限（TriggerLimitPerTurn 被覆写，不可限）");

            // 检索按维度档计费提示
            if (type == AtomicEffectType.SearchDeck)
                MakeHint(inject, "检索按维度档计费：字符串字段填宣言卡名（ExactCard=3），空=单维度 1");

            // 有限分支奖励：预算行
            if (selKind == SelKind.GateReward && _graph.steps.Count > 1)
                MakeHint(inject, GateBudgetText(_graph.steps[1]));

            return RefreshTexts;
        }

        // 自由分支主干卡（未选引擎也常驻）：摘要行+展开态=引擎下拉（静态 dd-trunk）+效果级设置盒；
        // field-value/slider-amp 主干卡不适用（引擎参数随选择置默认）。
        private void MakeTrunkCard(RectTransform slot, BranchEngineKind engine, int param)
        {
            bool expanded = _sel == SelKind.FreeTrunk;
            var card = slot.Find("atom-card") as RectTransform;
            if (card == null)
            {
                Debug.LogError("[EffectComposer] 主干槽缺 atom-card 模板节点——检查 EffectUI.prefab");
                return;
            }
            card.gameObject.SetActive(true);
            UiKit.FindDeep(card, "editor")?.gameObject.SetActive(false);

            bool hasEngine = engine != BranchEngineKind.None;
            var head = UiKit.FindDeep(card, "head");
            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = (expanded ? "▼ " : "▶ ")
                    + (hasEngine ? AtomText.TrunkText(engine, param) : "主干（条件引擎）——未选择");
                UiKit.Size(text, fw: 1f);
            }

            if (head != null)
            {
                BindCardButton(UiKit.FindDeep(card, "up"), "↑", false, null);
                BindCardButton(UiKit.FindDeep(card, "down"), "↓", false, null);
                BindCardButton(UiKit.FindDeep(card, "del"), "移除", hasEngine, () =>
                {
                    _graph.header.EngineKind = (int)BranchEngineKind.None;
                    _graph.header.EngineParam = 0;
                    _sel = SelKind.None;
                    RefreshSlots();
                });
                var headBtn = head.gameObject.GetComponent<Button>() ?? head.gameObject.AddComponent<Button>();
                headBtn.transition = Selectable.Transition.None;
                headBtn.targetGraphic = head.GetComponent<Image>();
                headBtn.onClick.RemoveAllListeners();
                headBtn.onClick.AddListener(() => ToggleExpand(SelKind.FreeTrunk, 0));
            }

            if (!expanded) return;

            var editor = UiKit.FindDeep(card, "editor");
            if (editor == null)
            {
                Debug.LogError("[EffectComposer] 主干卡缺 editor 节点——引擎不可选");
                return;
            }
            editor.gameObject.SetActive(true);

            // 引擎下拉（2026-10-05 prefab 静态化：dd-trunk-title 容器+dd-trunk 下拉移入卡内）
            UiKit.FindDeep(editor, "dd-trunk-title")?.gameObject.SetActive(true);
            var dd = DdOf(UiKit.FindDeep(editor, "dd-trunk"));
            if (dd != null)
            {
                dd.ClearOptions();
                dd.AddOptions(TrunkEngines.Select(e => e == BranchEngineKind.None ? "（未选择）" : TrunkZh(e)).ToList());
                dd.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(TrunkEngines, engine)));
                dd.RefreshShownValue();
                dd.onValueChanged.RemoveAllListeners();
                dd.onValueChanged.AddListener(i =>
                {
                    var eng = TrunkEngines[Mathf.Clamp(i, 0, TrunkEngines.Length - 1)];
                    var h = _graph.header;
                    h.EngineKind = (int)eng;
                    h.EngineParam = eng == BranchEngineKind.None ? 0 : 1; // 倒计时 x=1；拼点/运势默认 x=1
                    _sel = SelKind.FreeTrunk; // 保持展开——切换后重建（设置盒/奖励区/预算随引擎刷新）
                    if (eng == BranchEngineKind.None || h.AtomicEffects == null || h.AtomicEffects.Count == 0)
                        _selSlot = SelKind.FreeReward; // 主干已定——自动前进到奖励槽（若空）
                    RefreshSlots();
                    RefreshName();
                    RefreshRightPanels();
                });
                UiKit.Described(dd,
                    "条件引擎——拼点：比双方牌库随机生物攻击力差 / 运势：2d6 双＞x / 倒计时：回合递减归零发奖 / "
                    + "死亡计数：本回合双方合计死亡≥x / 元素充盈：付费后 bank 最多色＞x / 手牌序位：此卡为本回合第 x 张");
            }

            // field-value/slider-amp 主干卡不适用
            UiKit.FindDeep(editor, "field-value")?.gameObject.SetActive(false);
            UiKit.FindDeep(editor, "slider-amp")?.gameObject.SetActive(false);

            _settingsRefresh = () => // 设置盒目标行写入联动（主干卡无原子描述——只刷名称/校验）
            {
                RefreshName();
                ValidateZones();
            };
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
        /// <summary>门槛行（2026-10-05 prefab 静态化：tpl-gate-row 内 dd-trunk）——选项随主干原子类型
        /// （产出族条件，标签含【奖励x】预算）；改写门切换即清空遗留奖励。主干非产出族=提示行回落。</summary>
        private void MakeGateRow(RectTransform parent, EffectStepData branch)
        {
            var trunkType = ResolveType(_graph.steps[0].atomic);
            var gates = ComposerCatalog.GatesFor(trunkType).ToList();
            if (gates.Count == 0)
            {
                MakeHint(parent, "主干不是产出族原子——无法挂产出条件");
                return;
            }

            var row = UiKit.CloneTemplate("tpl-gate-row", parent); // 落在主干槽与奖励槽之间（创建序即层级序）
            var dd = row != null ? DdOf(UiKit.FindDeep(row, "dd-trunk")) : null;
            if (dd == null) return;

            int cur = gates.FindIndex(g => g.Id == branch.conditionId);
            if (cur < 0) { cur = 0; branch.conditionId = gates[0].Id; }
            dd.ClearOptions();
            dd.AddOptions(gates.Select(ComposerCatalog.GateLabel).ToList());
            dd.SetValueWithoutNotify(cur);
            dd.RefreshShownValue();
            dd.onValueChanged.RemoveAllListeners();
            dd.onValueChanged.AddListener(idx =>
            {
                if (idx >= 0 && idx < gates.Count)
                {
                    branch.conditionId = gates[idx].Id;
                    if (CardCore.BranchConditionEvaluator.IsRewriteCondition(branch.conditionId))
                        branch.thenSteps?.Clear(); // 改写门无奖励槽——切换即清空遗留奖励
                    RefreshSlots(); // 预算行/奖励槽随门刷新
                }
            });
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

        // ======================================== 效果设置（prefab 静态设置盒·效果级共用） ========================================

        /// <summary>效果级设置盒绑定（2026-10-05 模板克隆制·七项定案收口）：row0=目标行
        /// dd-kinds（作用范围·并列原子表域交集内单选→header.TargetKinds）+ dd-num（目标数量 1/2/3/全部）
        /// + rand（随机目标正交标志）；row1=speed（1/2/3·仅主动）/limit（作用次数 1/2/3/全部·非主动）
        /// ——speed/limit 显隐随 SyncActivationVisibility 联动；整盒克隆/显隐归 SyncSettingsPanel。
        /// 选择模式=目标数×作用范围推导（不再手选）；持续档只在关键词类原子内联三档；落区按原子写死（停写）。</summary>
        private void BindEffectSettings(RectTransform settings, Action refreshTexts)
        {
            if (settings == null) return;
            var h = _graph.header;

            // ---- 发动行：速度（仅主动三档）/ 作用次数（非主动四档；位 8 恒无限锁死）----
            _ddSpeed = BindTierDropdown(UiKit.FindDeep(settings, "speed"), SpeedTiers, h.BaseSpeed,
                v => h.BaseSpeed = v,
                "发动速度：主动效果档位——1=瞬间基准（对手回合也能发动/响应）；自动效果不设速度（满足即入栈）");
            _ddLimit = BindTierDropdown(UiKit.FindDeep(settings, "limit"), LimitTiers, h.TriggerLimitPerTurn,
                v => h.TriggerLimitPerTurn = v,
                "作用次数：非主动效果每回合可生效次数（全部=不限）；计价连乘 1.2^(N-1)；主动效果不限——不显示");
            if (_ddLimit != null && EffectHasMountBit(MountKind.TriggerCapImmutable))
                _ddLimit.interactable = false; // 位 8 数据驱动：表声明恒无限——锁输入，装载期覆写 -1

            // ---- 目标行：作用范围（交集单选）/ 目标数量 / 随机目标 ----
            var domain = _mode == ComposeMode.Parallel ? EffectDomainIntersection() : new List<int>();
            bool showKinds = domain.Count > 0;
            var kindsNode = UiKit.FindDeep(settings, "dd-kinds");
            if (kindsNode != null) kindsNode.gameObject.SetActive(showKinds);
            _ddKinds = showKinds ? DdOf(kindsNode) : null;
            if (showKinds && _ddKinds != null)
            {
                // 越界/多值声明清理（防夹带）：交集外声明回落未声明（converter 交集推导）
                if (h.TargetKinds != null && (h.TargetKinds.Count != 1 || !domain.Contains(h.TargetKinds[0])))
                    h.TargetKinds = null;
                _ddKinds.ClearOptions();
                _ddKinds.AddOptions(domain.Select(k => AtomText.TargetKindZhOf((TargetKind)k)).ToList());
                int cur = h.TargetKinds != null ? domain.IndexOf(h.TargetKinds[0]) : -1;
                _ddKinds.SetValueWithoutNotify(Mathf.Max(0, cur)); // 未声明=首档占位不写（回落交集推导）
                _ddKinds.RefreshShownValue();
                _ddKinds.onValueChanged.RemoveAllListeners();
                _ddKinds.onValueChanged.AddListener(i =>
                {
                    h.TargetKinds = new List<int> { domain[Mathf.Max(0, i)] };
                    refreshTexts?.Invoke(); // {target} 按效果级域渲染——描述/摘要原地重渲
                });
                UiKit.Described(_ddKinds,
                    "作用范围：并列原子表域交集内统一选择（效果级 TargetKinds 单值）；选择模式由 目标数×作用范围 自动推导");
            }
            else if (h.TargetKinds != null) h.TargetKinds = null; // 非并列/交集空——清空防夹带

            _ddNum = BindTierDropdown(UiKit.FindDeep(settings, "dd-num"), CountTiers, h.TargetCount,
                v => h.TargetCount = v,
                "目标数量：全部=作用范围内全取（不弹窗·期望计价）；1/2/3=弹窗选 N；选择模式随之自动推导");

            var randNode = UiKit.FindDeep(settings, "rand");
            _tgRand = randNode != null
                ? randNode.GetComponent<Toggle>() ?? randNode.GetComponentInChildren<Toggle>(true)
                : null;
            if (_tgRand != null)
            {
                _tgRand.SetIsOnWithoutNotify(h.RandomTarget != 0);
                _tgRand.onValueChanged.RemoveAllListeners();
                _tgRand.onValueChanged.AddListener(on =>
                {
                    // 随机移出枚举为正交标志——只翻标志，不再覆写选择模式
                    h.RandomTarget = on ? 1 : 0;
                    refreshTexts?.Invoke();
                });
                UiKit.Described(_tgRand, "目标随机：不弹窗按种子从完整范围抽取（扰魔/潜行仍可被随机命中）——与目标数/范围正交");
            }

            SyncSettingsRowVisibility(); // 展开卡重绑后立即对齐发动行显隐
        }

        /// <summary>设置盒显隐（2026-10-05 简化定案）：tpl-settings 随每次槽区重建克隆——
        /// 任一形态原子槽有原子才克隆显示（追加在槽位之后）；全空不创建（旧克隆随 ClearContent 自然销毁）。</summary>
        private void SyncSettingsPanel()
        {
            _ddSpeed = _ddLimit = _ddKinds = _ddNum = null; // 旧克隆绑定引用随重建作废
            _tgRand = null;
            if (!HasAnyAtom()) return;
            var node = UiKit.CloneTemplate("tpl-settings", _slotArea.Content);
            if (node == null) return;
            BindEffectSettings(node, () => _settingsRefresh?.Invoke());
        }

        /// <summary>当前形态原子槽是否至少有一个原子（并列=steps；自由分支=奖励原子；有限分支=主干/奖励；光环=无）。</summary>
        private bool HasAnyAtom() => _mode switch
        {
            ComposeMode.Parallel => _graph.steps.Any(s => s?.kind == 0 && s.atomic != null),
            ComposeMode.FreeBranch => _graph.header.AtomicEffects != null && _graph.header.AtomicEffects.Count > 0,
            ComposeMode.OutcomeGate => _graph.steps.Count >= 2
                && (_graph.steps[0].atomic != null || (_graph.steps[1].thenSteps?.Count ?? 0) > 0),
            _ => false,
        };

        /// <summary>档位下拉静态绑定：标签由档位值生成（-1/0=「全部」）；现值不在档内=首档占位显示不写回
        /// （存量兼容——改选才落值，同发动方式的系统强制档占位策略）。</summary>
        private static TMP_Dropdown BindTierDropdown(RectTransform node, int[] tiers, int current,
            Action<int> write, string desc)
        {
            var dd = DdOf(node);
            if (dd == null) return null;
            dd.ClearOptions();
            dd.AddOptions(tiers.Select(v => v == -1 || v == 0 ? "全部" : v.ToString()).ToList());
            dd.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(tiers, current)));
            dd.RefreshShownValue();
            dd.onValueChanged.RemoveAllListeners();
            dd.onValueChanged.AddListener(i => write(tiers[Mathf.Clamp(i, 0, tiers.Length - 1)]));
            UiKit.Described(dd, desc);
            return dd;
        }

        /// <summary>prefab 静态下拉取件（节点在而组件缺=数据层可见错误，不静默）。</summary>
        private static TMP_Dropdown DdOf(RectTransform node)
        {
            var dd = node?.GetComponent<TMP_Dropdown>() ?? node?.GetComponentInChildren<TMP_Dropdown>(true);
            if (node != null && dd == null)
                Debug.LogError($"[EffectComposer] 节点 {node.name} 缺 TMP_Dropdown 组件（2026-10-05 prefab 静态化）");
            return dd;
        }

        /// <summary>并列原子表域交集（极性限选后；全部 kind=0 原子）——效果级作用范围候选集。</summary>
        private List<int> EffectDomainIntersection() => ParallelDomainExcept(-1);

        /// <summary>并列域交集（排除 excludeIndex 槽的原子——替换选槽时旧原子不计入）。</summary>
        private List<int> ParallelDomainExcept(int excludeIndex)
        {
            List<int> inter = null;
            var steps = _graph.steps ?? new List<EffectStepData>();
            for (int i = 0; i < steps.Count; i++)
            {
                if (i == excludeIndex) continue;
                var s = steps[i];
                if (s?.kind != 0 || s.atomic == null) continue;
                var cfg = AtomicEffectTable.GetByHashId(s.atomic.refId);
                var allowed = AllowedTargetKinds(cfg, cfg?.GetTargetKindList() ?? new List<int>());
                inter = inter == null ? allowed : inter.Intersect(allowed).ToList();
                if (inter.Count == 0) break;
            }
            inter?.Sort();
            return inter ?? new List<int>();
        }

        /// <summary>并列库行域兼容：行自身域非空，且与既有并列域交集非空（首批=域非空即可）。</summary>
        private static bool RowDomainCompatible(AtomicEffectConfig cfg, List<int> current)
        {
            var domain = AllowedTargetKinds(cfg, cfg?.GetTargetKindList() ?? new List<int>());
            if (domain == null || domain.Count == 0) return false;
            return current.Count == 0 || domain.Any(current.Contains);
        }

        // ======================================== 右：双模式展示区 ========================================

        /// <summary>表切换（合并按钮）：原子库↔效果表来回切；切回原子库时重读表（承接原 btn-reload-atoms 语义）。</summary>
        private void SwitchTable()
        {
            if (_rightMode == RightMode.Atoms)
            {
                SetRightMode(RightMode.Effects);
                return;
            }
            AtomicEffectTable.Reload();
            SetRightMode(RightMode.Atoms); // 内含 RebuildFilterTypeChoices（重置"全部"）+ RefreshLibrary
            ShowToast($"原子表已重读：{_libraryList?.Content?.childCount ?? 0} 行入列");
        }

        private void SetRightMode(RightMode mode)
        {
            _rightMode = mode;
            // 面板包装层已删（2026-10-03 手改）：显隐直接驱动滚动区本体（UiKit.Scroll 包装类走 .Rect）
            _libraryList.Rect.gameObject.SetActive(mode == RightMode.Atoms);
            _effectsList.Rect.gameObject.SetActive(mode == RightMode.Effects);

            // 筛选栏两模式共用：分类选项随模式重建（原子库=表行中文名；效果表=内含原子名并集）
            RebuildFilterTypeChoices();

            // 合并切换按钮标签：常显"将切到哪边"
            if (_tableToggleLabel != null)
                _tableToggleLabel.text = mode == RightMode.Atoms ? "显示效果表" : "显示原子表";

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

                var row = MakeListRow(_effectsList.Content, captured.name, "meta",
                    AtomText.RenderEffectSummary(captured), // 完整效果描述（2026-10-03 定案：不再简写）
                    DotColor(ColorFilter.OfColorName(parts.FirstOrDefault()?.Tags)),
                    captured.header?.AnchorCost,
                    () => LoadEffectFromLibrary(captured));
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
            _activationDropdown.SetIndex(ActivationIndex(_graph.header.ActivationType));
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
                {
                    // 并列相同目标（2026-10-05）：效果级作用范围=并列原子表域交集单选——库过滤收口
                    // 「域非空∧交集非空」（行自身域空不可入；与既有原子[除选中槽]交集空不可入）
                    contextZh = $"并列槽 {_selSlotIndex + 1}（主动原子·非错边·作用域兼容）";
                    var others = ParallelDomainExcept(_selSlotIndex);
                    return lp => lp.CanBeActiveAtom && !lp.IsWrongSideOnly && RowDomainCompatible(lp.Cfg, others);
                }
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

        private void MakeLibraryRow(LibPayload payload) =>
            MakeListRow(_libraryList.Content, payload.Cfg.DisplayName, "tpl", payload.Cfg.Description,
                DotColor(ColorFilter.OfColorName(payload.Cfg.Tags)), AtomCosts(payload.Cfg), () => QuickAdd(payload));

        /// <summary>原子单价 → 方格明细（费用构成各色份额取整；序数序=枚举序；null=不计价行）。</summary>
        private static ElementCost AtomCosts(AtomicEffectConfig cfg)
        {
            var cost = cfg?.ManaList;
            if (cost == null || cost.IsZero) return null;
            return cost;
        }

        /// <summary>右栏列表行（原子库/效果表/光环库三处共用，2026-10-03 模板约定；2026-10-04 费用=位置数组）：
        /// content 下有不激活的模板（名字 tpl-row 或 row 均可）→ 克隆填充（文本按名优先、位置兜底；
        /// 行内费用=模板换入的 CostSquares 实例（按组件找，不看名字），无实例回落 dot 色点染色；
        /// 行点击 Button），布局样式全以模板为准；无模板走原代码构建。</summary>
        private RectTransform MakeListRow(RectTransform content, string title,
            string secondChild, string secondText, Color dot, ElementCost costs, Action onClick)
        {
            var tpl = UiKit.CloneTemplate("tpl-row", content) ?? UiKit.CloneTemplate("row", content);
            if (tpl == null)
            {
                var row = UiKit.Row("row", content, spacing: 8f, pad: 6f);
                var bg = UiKit.BgRow(row);
                bg.raycastTarget = true;
                UiKit.Size(row, fw: 1f, h: 34f);
                if (costs != null)
                {
                    var v = CostSquaresView.Mount(row);
                    v.SetCosts(costs);
                }
                else UiKit.Dot("dot", row, dot);
                var name = UiKit.Label("name", row, title, UiStyle.SmallSize, UiStyle.TextBody);
                UiKit.Size(name, fw: 1f);
                UiKit.Label(secondChild, row, secondText, UiStyle.MiniSize, UiStyle.TextFaint);
                var btn = row.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => onClick());
                return row;
            }

            FillRowParts(tpl, title, secondChild, secondText);
            // 行内费用方格（模板换入的 CostSquares 实例，按组件找）；无实例回落旧 dot 染色
            var rowView = tpl.GetComponentInChildren<CostSquaresView>(true);
            if (rowView != null)
            {
                if (costs != null && !costs.IsZero) rowView.SetCosts(costs);
                else rowView.Clear();
            }
            else SetPartColor(tpl, "dot", dot);
            // 模板行宽兜底（2026-10-03）：模板没拉满时克隆体只有几十像素宽（实测 28px 细条）——
            // 视觉上等于空表。统一铺满 content 宽 + LE 弹性宽（VLG 控宽时优先）；高度仍以模板为准。
            float cw = Mathf.Max(content.rect.width,
                content.parent is RectTransform vp ? vp.rect.width : 0f);
            if (tpl.rect.width < cw * 0.5f)
                tpl.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cw);
            UiKit.Size(tpl, fw: 1f);
            var tbtn = tpl.GetComponent<Button>();
            if (tbtn == null)
            {
                tbtn = tpl.gameObject.AddComponent<Button>();
                tbtn.transition = Selectable.Transition.None;
                if (tbtn.targetGraphic == null) tbtn.targetGraphic = tpl.GetComponent<Image>();
            }
            tbtn.onClick.RemoveAllListeners();
            tbtn.onClick.AddListener(() => { Debug.Log("[UI点击] 库行"); onClick(); });
            return tpl;
        }

        /// <summary>模板行文本填充：按名优先（name/第二列约定名），缺名回落位置序——
        /// 层级第一个 TMP=主文本、第二个 TMP=第二列。模板子物体可自定义命名，不必拘泥约定名。
        /// meta-only 模板（2026-10-03 定案：行内只留一段文本、写完整效果描述）——唯一 TMP 吃 meta。</summary>
        private static void FillRowParts(RectTransform row, string title, string secondChild, string secondText)
        {
            var tmps = row.GetComponentsInChildren<TMP_Text>(true);
            if (tmps.Length == 1)
            {
                tmps[0].text = secondText ?? title ?? "";
                return;
            }

            TMP_Text nameT = null, secondT = null;
            var nameNode = UiKit.FindDeep(row, "name");
            if (nameNode != null) nameT = nameNode.GetComponentInChildren<TMP_Text>(true);
            if (secondChild != null)
            {
                var secondNode = UiKit.FindDeep(row, secondChild);
                if (secondNode != null) secondT = secondNode.GetComponentInChildren<TMP_Text>(true);
            }
            if (nameT == null && tmps.Length > 0) nameT = tmps[0];
            if (secondT == null && secondChild != null && tmps.Length > 1) secondT = tmps[1];
            if (nameT != null) nameT.text = title ?? "";
            if (secondT != null) secondT.text = secondText ?? "";
        }

        private static void SetPartColor(RectTransform row, string child, Color color)
        {
            var img = UiKit.FindDeep(row, child)?.GetComponent<Image>();
            if (img != null) img.color = color;
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
                MakeListRow(_libraryList.Content, title, "meta", meta, DotColor(color), null, onAdd);
            }

            void AddAura(LinkAuraData entry)
            {
                _graph.header.LinkAuras ??= new List<LinkAuraData>();
                _graph.header.LinkAuras.Add(entry);
                RefreshSlots(); // 左栏条目列表/校验/费用随添加刷新（右栏行保持——可连点重复添加）
            }

            // 规则级光环：落 steps 为 ModifyGameRule 登场原子（str=规则短名）——引擎 RuleAuraSystem
            // 全局唯一/双方生效；不占 LinkAura 连接位（无箭头——普通光环才开箭头预选）
            void AddRuleAura(AtomicEffectConfig cfg)
            {
                _graph.steps ??= new List<EffectStepData>();
                _graph.steps.Add(new EffectStepData { kind = 0,
                    atomic = new AtomicEffectEntry { refId = cfg.HashId, value = 1, str = RuleAuraIds[cfg.EnumName] } });
                RefreshSlots();
                RefreshName();
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
            foreach (var r in AllTableRows())
            {
                var rule = r;
                if (!IsRuleAuraRow(rule)) continue;
                AddRow($"规则光环·{rule.EnumName}", $"{rule.DisplayName}——全局唯一·双方生效，入场即挂（新的登场把旧的送墓），离场/被无效即失效；无箭头",
                    ColorFilter.OfColorName(rule.Tags),
                    () => AddRuleAura(rule));
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

        /// <summary>光环编辑面板：普通光环=六向箭头选择器+条目；规则级光环（仪典）=无箭头·双方生效·
        /// 全局唯一（2026-10-03 定案）——含规则条目即关闭箭头预选（规则光环不占连接位）。</summary>
        private void MakeAuraPanel(RectTransform parent)
        {
            var h = _graph.header;
            h.LinkAuras ??= new List<LinkAuraData>();
            bool hasRule = HasRuleAuraStep(_graph);
            if (hasRule)
            {
                h.ArrowDirections = 0; // 规则级光环：无箭头（引擎侧不读连接位——写盘一致）
                MakeHint(parent, "规则级光环＝全局唯一·作用双方：载体入场即挂（新的登场把旧的送墓），"
                    + "离场/被无效即失效——无箭头、不占连接位；普通光环（连接箭头）才需要箭头预选");
            }
            else
            {
                MakeHint(parent, "光环＝连接箭头持续效果：作用对象=箭头指向格的当前占据者（断链/离场即失效）——不可指定作用对象。");
            }

            // ---- 连接箭头（六向三角选择器——仅普通光环；规则级关闭） ----
            if (!hasRule)
            {
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
                // 校验：普通光环必须搭配至少一支箭头（无箭头=永无受益者）
                _slotZones.Add(MakeZone(arrowsBox, () => (HexDirection)h.ArrowDirections == HexDirection.None
                    ? "未选箭头——光环必须搭配至少一支箭头（无箭头=永无受益者）；规则级光环则不需要箭头" : null));
            }

            // ---- 光环条目（右栏点击添加，左栏只展示/编辑）----
            var listBox = UiKit.Column("entries", parent, spacing: 4f, pad: 8f);
            var lbBg = listBox.gameObject.AddComponent<Image>();
            lbBg.sprite = UiKit.RoundedSprite;
            lbBg.type = Image.Type.Sliced;
            lbBg.color = UiStyle.ListBg;
            UiKit.Size(listBox, fw: 1f);
            var lh = UiKit.Label("title", listBox,
                hasRule ? "光环条目（规则级·双方生效——可再叠加普通条目，规则不占连接位）"
                        : "光环条目（右栏点击添加——属性修正 stat+value / 关键词 keyword）",
                UiStyle.HeaderSize, UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(lh, fw: 1f);
            for (int i = _graph.steps.Count - 1; i >= 0; i--)
            {
                var st = _graph.steps[i];
                if (st?.kind != 0 || st.atomic == null) continue;
                if (IsRuleAuraRow(AtomicEffectTable.GetByHashId(st.atomic.refId)))
                    MakeRuleAuraRow(listBox, i);
            }
            for (int i = 0; i < h.LinkAuras.Count; i++)
                MakeAuraEntryRow(listBox, i);
            if (h.LinkAuras.Count == 0 && !hasRule)
                MakeHint(listBox, "尚无条目——右侧光环条目库点击添加（属性/关键词/规则级，可重复）");
            MakeHint(listBox, "坚韧=绿1/条、守护=白1/条、属性=光环档 1.5/+1（效果层只计条目）；箭头费挂卡并集后由卡层计");
            // 校验：至少一条有效光环条目（规则级条目也算——仪典原子在 steps）+ 关键词条目须位 10 可挂
            _slotZones.Add(MakeZone(listBox, () =>
            {
                if (ValidAuraCount(h.LinkAuras) == 0 && !hasRule)
                    return "无有效光环条目——stat/keyword/规则级 至少配一条（空条目保存时剔除）";
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

        /// <summary>规则光环条目行（steps 中的仪典原子）：只读展示 + 删除。</summary>
        private void MakeRuleAuraRow(RectTransform parent, int stepIndex)
        {
            var step = _graph.steps[stepIndex];
            var cfg = AtomicEffectTable.GetByHashId(step.atomic?.refId);
            var row = UiKit.Row("rule-entry", parent, spacing: 6f);
            UiKit.BgRow(row);
            UiKit.Size(row, fw: 1f, h: 32f);
            var lbl = UiKit.Label("name", row,
                $"规则光环·{cfg?.EnumName ?? step.atomic?.refId}（双方生效·全局唯一）",
                UiStyle.SmallSize, UiStyle.TextBody);
            UiKit.Size(lbl, fw: 1f);
            UiKit.MiniButton("del", row, "删除",
                () => { _graph.steps.RemoveAt(stepIndex); RefreshSlots(); RefreshName(); }, UiStyle.BtnDanger);
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
            // 并列相同目标（2026-10-05）：效果级作用范围只在并列形态合法——防夹带（SwitchMode 切形态时亦清）
            if (_mode != ComposeMode.Parallel) effect.TargetKinds = null;
            if (_mode != ComposeMode.Aura)
            {
                effect.ArrowDirections = 0;
                effect.LinkAuras = null;
            }
            else if (HasRuleAuraStep(_graph))
            {
                effect.ArrowDirections = 0; // 规则级光环不占连接位（UI 已关箭头预选——写盘一致）
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

        private void ShowToast(string message)
        {
            _descStripNode.GetComponentInChildren<TMP_Text>().text = message;
        }

        /// <summary>清空容器（先摘父再 Destroy，防 Destroy 延迟导致的同帧占位）；
        /// 不激活的隐藏模板保留（tpl-* 前缀，或名为 row 的不激活模板——动态行的克隆源，
        /// 2026-10-03 模板约定；代码建的行恒为激活态，不误伤）。</summary>
        private static void ClearChildren(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (!child.gameObject.activeSelf
                    && (child.name.StartsWith("tpl-") || child.name == "row")) continue;
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
