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
        /// 2026-10-05 设置盒/数值区 prefab 静态化（tpl-slot 模板克隆）；2026-10-09 设置盒拆除：
        /// 发动四参数=activate-bar 静态组，作用范围/目标数量/目标随机随逐原子目标制下沉每原子卡面行
        ///（header.TargetKinds 恒空——各原子按自身表域独立解析）；选择模式/持续档/落区收口不再手编。
        /// 2026-10-07 持续行预制体化：atom-card/reward-slot 烘焙 aura-duration 行（「持续时间」+三档下拉），
        /// 仅赋予类（关键词/指示物原子）显示；代码生成 dd-dur 与光环槽持续回合数行退役。
        /// 2026-10-07 光环条目迁入槽下作卡（aura-entry-card——克隆 atom-card，编辑走卡内 editor）；
        /// 条目列表区就地编辑与面板 aura-cost 费用预览退役（费用顶栏同源独占）。
        /// 2026-10-07 兜底构建全退（全屏统一）：缺节点只 LogError 不代码创建（槽/奖励区/条目卡/gate-row/
        /// row 模板/guide-frame/描述条）；复合检索退役（BindDropdown 单名 TMP、BindableButton 单名、
        /// row 模板单名、FillRowParts 按名不按位、费用/计数不再父级回退）。
        /// 2026-10-08 右栏行定案不显示名字：FillRowParts 只填 meta 单列（name 节点绑定与 title 形参退役）。
    /// 2026-10-05 两槽定案：形态只余 并列（两槽）/光环——自由分支·事件引擎、有限分支·产出条件
    /// 折叠进并列形态的槽级 atomic.branch 载荷（结算方式下拉+条件/引擎参数+Then 奖励槽）；
    /// 2026-10-09 并列/分支互斥：分支编辑区仅第一槽——任一分支已开则第二槽隐藏、第二槽有原子则分支区隐藏。
    /// 核心定案：槽位选中制（右侧点击=替换选中槽）；组合形态由 InferMode 推断防互串；
    /// 校验实时化（区域闪烁红框+保存禁用）；属性描述选中才出现（描述条）；
    /// 光环=箭头+条目随效果合成（挂卡并集，效果层只预选不计箭头费）；
    /// 代价栏已上移卡组合层（错边原子在卡编辑界面填装）。
    /// </summary>
    public sealed class EffectComposerScreen : UIScreen
    {
        // ======================================== 组合形态 ========================================

        /// <summary>组合形态（2026-10-05 两槽定案）：并列（两槽）/ 光环（连接箭头）。
        /// 分支编辑能力折叠进并列形态的槽级 branch 载荷——2026-10-09 互斥定案：
        /// 分支编辑区只在第一槽，与第二槽互斥（开了分支没有第二槽，填了第二槽没有分支盒）。</summary>
        public enum ComposeMode { Parallel, Aura }

        /// <summary>右栏双模式：原子库（点击装配）/ 效果表（点击载入编辑）。</summary>
        private enum RightMode { Atoms, Effects }

        /// <summary>由数据推断组合形态（两槽定案）：光环声明（箭头/光环条目/规则光环步）→ Aura；
        /// 其余（含槽级分支载荷）一律并列。引擎头字段判断已随载荷化删除。</summary>
        public static ComposeMode InferMode(EffectGraphData g)
        {
            if (g?.header != null && (g.header.ArrowDirections != 0
                || (g.header.LinkAuras != null && g.header.LinkAuras.Count > 0))) return ComposeMode.Aura;
            if (HasRuleAuraStep(g)) return ComposeMode.Aura;
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
            { "苦痛仪典", RuleAuraComponents.BloodPact },     // 2026-10-07 用户改名（原血偿仪典）
            { "丰盈仪典", RuleAuraComponents.HealOverflow },
            { "离散仪典", RuleAuraComponents.DamageCap },
            { "窥渊仪典", RuleAuraComponents.LockRevealed },
            { "归土仪典", RuleAuraComponents.GraveyardPlay },
            { "疾风仪典", RuleAuraComponents.CastSpeedUp },
            { "时光仪典", RuleAuraComponents.DoubleTurn },     // 2026-10-07 用户改名（原轮回仪典）
            { "纳川仪典", RuleAuraComponents.HandLimitNoFatigue },
            // 战斗改写仪典（2026-10-05 定案迁唯一光环；2026-10-07 降级连接光环——条目库投放+方向档）
            { "毒蚀仪典", RuleAuraComponents.CombatToxin },
            { "霜蚀仪典", RuleAuraComponents.CombatFreeze },
            { "眠蚀仪典", RuleAuraComponents.CombatSleep },
            { "舍身仪典", RuleAuraComponents.CombatRedirect }, // 2026-10-07 改写回归（伤害转投对手角色）；疫蚀 2026-10-09 随剧毒指示物删除退役
            { "暗牧仪典", RuleAuraComponents.HealInversion },  // 2026-10-09 治疗转伤害（受光环影响一方的治疗改写为等量伤害；黑8 表行 9dfed4f8；用户命名——初拟「反疗仪典」）
        };

        /// <summary>规则光环默认范围中文（行级——右栏库行说明用）：改写仪典=仅己方（落槽缺省）、
        /// 其余=对双方。实际生效范围逐步存于仪典步 entry.value（AutoName/设置盒按步读取，2026-10-07）。
        /// 键=DisplayName（中文短名——装载后 EnumName 列落在 DisplayName，英文枚举名在 EnumName）。</summary>
        private static string RuleAuraScopeOf(AtomicEffectConfig cfg)
            => cfg != null && RuleAuraIds.TryGetValue(cfg.DisplayName, out var rid)
                ? RuleAuraSystem.RuleAuraScopeZh(rid) : "对双方生效";

        /// <summary>规则光环行识别（2026-10-05 统一标记定案）：原子表 MountKinds 位 RuleAura 驱动——
        /// 9 行双方仪典 + 3 行改写仪典（毒蚀/霜蚀/眠蚀；疫蚀 2026-10-09 随剧毒指示物删除退役）；RuleAuraIds 字典只剩 str 短名映射职责。</summary>
        private static bool IsRuleAuraRow(AtomicEffectConfig cfg)
            => ComposerCatalog.HasMountBit(cfg, MountKind.RuleAura);

        /// <summary>steps 中的光环投放原子步（光环形态专有——SwitchMode 切出即清，形态互斥）。
        /// 2026-10-07 改写降级：改写行 MountKinds=连接光环——步识别改走 EffectType（全部 ModifyGameRule 行）。</summary>
        private static bool HasRuleAuraStep(EffectGraphData g)
        {
            if (g?.steps == null) return false;
            foreach (var s in g.steps)
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                if (IsRitualStepRow(AtomicEffectTable.GetByHashId(s.atomic.refId))) return true;
            }
            return false;
        }

        /// <summary>光环投放步行识别（仪典+改写）：EffectType=ModifyGameRule 的全部表行
        ///（2026-10-07 改写降级后 MountKinds 两族分裂，行识别不再可靠——EffectType 单一来源）。</summary>
        private static bool IsRitualStepRow(AtomicEffectConfig cfg)
            => cfg != null && cfg.EnumName == nameof(AtomicEffectType.ModifyGameRule);

        // ---- 状态 ----
        private EffectGraphData _graph = new EffectGraphData("新效果");
        private ComposeMode _mode = ComposeMode.Parallel;
        private RightMode _rightMode = RightMode.Atoms;

        // 槽内卡片展开状态（代价栏已上移卡组合层）
        // 2026-10-05 两槽定案：原 GateTrunk/GateReward/FreeTrunk/FreeReward 并入 Parallel（主干槽）
        // 与 Reward（槽内 Then 奖励选中态——_selRewardSlot 标记当前编辑哪槽的奖励）
        private enum SelKind { None, Parallel, Reward }

        // 展开态集合（2026-10-06 定案：多卡独立展开——点击其他卡不再收起已展开的卡）：
        // 键=对象身份——换序/删除免下标维护，ClampSelection 渲染前剪除悬挂条目
        private readonly HashSet<AtomicEffectEntry> _expandedAtoms = new HashSet<AtomicEffectEntry>();
        private readonly HashSet<BranchEntryData> _expandedRewards = new HashSet<BranchEntryData>();

        // 槽位选中：右侧点击=替换选中槽；默认原子1（首个槽）
        private SelKind _selSlot = SelKind.Parallel;
        private int _selSlotIndex = 0;

        // 奖励槽选中态（SelKind.Reward）：当前编辑哪槽的 Then 奖励（0/1——两槽）
        private int _selRewardSlot;

        /// <summary>并列槽位数（2026-10-05 两槽定案）。</summary>
        private const int ParallelSlotCount = 2;

        // 卡编辑会话（null=效果库模式——保存到效果库文件）
        private CardData _editingCard;
        private int _editingIndex = -1;

        // 从效果表载入的源效果 id（编辑换内容=换 id——保存后清旧档防重复；null=非效果表载入）
        private string _loadedFromLibraryId;

        // ---- 校验区：不合规设置=GuideFrame 遮罩高亮框（shader 红边闪烁）+错误文本上描述条，保存按钮禁用 ----
        private sealed class ZoneEntry
        {
            public RectTransform Zone;
            public Func<string> Check; // null/空=合规；否则=错误提示文本
            public bool Invalid;
        }
        private readonly List<ZoneEntry> _slotZones = new List<ZoneEntry>();
        private Button _saveBtn;
        private bool _zoneErrShown; // 描述条当前展示的是校验错误（转合规时清一次——不误清控件描述）

        /// <summary>引导高亮框（单例）：prefab 烘焙的 guide-frame 节点（默认在 EffectUI 根下）；
        /// 提示时临时挂到目标区下拉伸填充，无违规回根下隐藏——闪烁由 SynergyUI/GuideFrame shader 自驱。</summary>
        private Image _guideFrame;

        // ---- 属性描述条：描述只在控件选中时出现——不常驻 ----
        private TMP_Text _descStrip;
        private RectTransform _descStripNode;

        // 光环箭头六按钮表（2026-10-06 arrow-picker 预制体化）：按钮简称 T/D/L/R=上/下/左/右，
        // 映射=棋盘六邻接方位名（持卡者视角）：Up=右上(R-T)、UpperRight=右(R)、LowerRight=右下(R-D)、
        // Down=左下(L-D)、LowerLeft=左(L)、UpperLeft=左上(L-T)；三角朝向已按方位烘焙在预制体内
        private static readonly (HexDirection dir, string node)[] ArrowButtons =
        {
            (HexDirection.Up, "R-T"), (HexDirection.UpperRight, "R"),
            (HexDirection.LowerRight, "R-D"), (HexDirection.Down, "L-D"),
            (HexDirection.LowerLeft, "L"), (HexDirection.UpperLeft, "L-T"),
        };

        // ---- UI 引用 ----
        private UiKit.Scroll _slotArea, _libraryList, _effectsList;
        private RectTransform _modeBar, _timingRow;
        private TMP_Text _nameLabel;
        private TMP_Text _costLabel; // 顶栏费用文本 lbl-effect-cost（2026-10-05 预制体改版：方格→文本；无效组合不显示）
        private Button _deleteBtn;            // 删除当前库效果（仅效果表载入态可用）
        private UInputField _filterName;
        private UiKit.Dropdown _timingDropdown, _activationDropdown, _filterTypeDropdown;
        // 发动四参数（2026-10-09 prefab 重排·activate-bar 静态四组 way/timing/speed/take-effect）：
        // 节点常驻不随克隆重建——写入走当前 _graph.header，值对齐见 SyncActivateBarValues；
        // 速度/生效次数组显隐随 SyncActivationVisibility 联动
        private RectTransform _activateBar, _wayGroup;
        private RectTransform _speedGroup, _limitGroup;
        private TMP_Dropdown _ddSpeed, _ddLimit;
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

        // 触发时机中文：单一来源=AtomText.TimingZh 全量表（2026-10-09 时机前缀定案配套——
        // 描述开头前缀与本下拉共用一份）；下拉滤掉系统自用七值（不开放玩家选择：OnAtomicEffect*
        // 三段/OnSummon 召唤进场/OnPhase* 阶段开始结束，及 OnGameStart 游戏开始——2026-10-09
        // 同日收编系统自用）与主动三档（主动不设时机），另排除观察者/角色时点
        // （OnOtherCreatureDeath/OnRoleDeath——旧下拉本就未开放），下拉维持 20 项面
        private static readonly HashSet<TriggerTiming> SystemOnlyTimings = new HashSet<TriggerTiming>
        {
            TriggerTiming.OnAtomicEffectActivation,
            TriggerTiming.OnAtomicEffectStartApplying,
            TriggerTiming.OnAtomicEffectResolution,
            TriggerTiming.OnSummon,
            TriggerTiming.OnPhaseStart,
            TriggerTiming.OnPhaseEnd,
            TriggerTiming.OnGameStart,
            TriggerTiming.OnOtherCreatureDeath,
            TriggerTiming.OnRoleDeath,
        };

        private static readonly Dictionary<TriggerTiming, string> TimingZh =
            AtomText.TimingZh.Where(kv => !SystemOnlyTimings.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

        /// <summary>时机逐项一句话介绍（两段式描述——选中才显示；语义按 TriggerTiming 枚举文档）。</summary>
        private static string TimingDescZh(TriggerTiming t) => t switch
        {
            TriggerTiming.OnPlay => "登场时：这张卡通过发动区检验、进入战场的那一刻（被无效则不触发）。",
            TriggerTiming.OnDeath => "死亡时：这张卡死亡进墓地（亡语）。",
            TriggerTiming.OnDestroy => "破坏时：这张卡因「破坏」死亡。",
            TriggerTiming.OnExile => "除外时：这张卡被除外。",
            TriggerTiming.OnReturnFromGraveyard => "从墓地回到战场时（复活）。",
            TriggerTiming.OnLeaveBattlefield => "离场时：从战场去任何其他区域。",
            TriggerTiming.OnDraw => "抽牌时：你抽到牌的那一刻。",
            TriggerTiming.OnDealDamage => "造成伤害时：这张卡每次造成伤害。",
            TriggerTiming.OnTakeDamage => "受到伤害时：这张卡每次受到伤害。",
            TriggerTiming.OnTurnStart => "回合开始时：每回合固定一发——作用次数自动锁 1。",
            TriggerTiming.OnTurnEnd => "回合结束时：每回合固定一发——作用次数自动锁 1。",
            TriggerTiming.OnAttack => "攻击宣言时：这张卡宣言攻击。",
            TriggerTiming.OnAttacked => "被攻击时：这张卡被指定为攻击目标。",
            TriggerTiming.OnBlockDeclare => "阻拦宣言时：阻拦被宣言。",
            TriggerTiming.OnCardPlayed => "使用卡牌时：使用宣言的时点（付费前，含法术）。",
            TriggerTiming.OnSpellCast => "施放法术时：有法术被使用。",
            TriggerTiming.OnTap => "横置时：这张卡被横置。",
            TriggerTiming.OnUntap => "重置时：这张卡被重置。",
            TriggerTiming.OnTargeted => "被指定为目标时：有效果以这张卡为目标。",
            TriggerTiming.OnOtherCreatureEnter => "其他生物进场时（这张卡自己进场不算）。",
            _ => null,
        };

        // 设置盒档位表（2026-10-05 七项定案+下拉化）：速度仅主动三档 0/1/2（0=普通档——仅自己回合主阶段，
        // 2026-10-05 用户定案自 1/2/3 改档；引擎 0 速语义本就完备，见 SpeedSystem）；作用次数仅非主动（-1=全部/不限）；
        // 目标数量仅 1/2/3（2026-10-09 逐原子目标制：0=全部档退役，AOE 另出专用原子行）。
        // 选择模式由 目标数×各原子自身域 推导（CardEffectConverter）——不再手选
        private static readonly int[] SpeedTiers = { 0, 1, 2 };
        private static readonly int[] LimitTiers = { 1, 2, 3, -1 };
        private static readonly int[] CountTiers = { 1, 2, 3 };

        // 描述条文案（原子/奖励原子两处共用——面向新玩家白话口径，不谈名义值/锚点等内部概念）
        private const string ValueFieldDesc =
            "数值：效果的强度，比如伤害点数、抽牌张数。费用按这里填的数值计算；开了「数值随机」后，实际结算会在数值上下浮动。";
        private const string AmpSliderDesc =
            "数值随机：实际结算的数值会在填写的数值上下随机浮动，比例越大浮动越大。费用仍按填写的数值计算，不会因为随机而变贵。";

        /// <summary>固定战斗原子：攻击/守卫=生物卡默认携带——移出原子库不可组合。</summary>
        private static bool IsFixedBattleAtom(string enumName)
            => enumName == "Attack" || enumName == "Guard";

        /// <summary>锁定关键词行（移出原子库、不可组合、不展示）：攻击/守卫
        /// + 位 6 系统内部行（2026-10-04：修改攻击力/生命值/费用——留给系统，不暴露给玩家组合）。
        /// 引擎主干行（位 7，2026-10-05 回表）**不在锁定期**——玩家直接在原子库选，填槽即自由分支。</summary>
        private static bool IsLockedKeywordRow(AtomicEffectConfig r)
            => r != null && (IsFixedBattleAtom(r.EnumName)
                || ComposerCatalog.HasMountBit(r, MountKind.SystemInternal));

        // ======================================== 构建 ========================================

        protected override string PrefabName => "EffectUI";
        protected override string RootName => "effect-composer";

        protected override void Build()
        {
            // ---- 顶栏 ----
            UiKit.Described(BindButton("btn-back", () => Manager.Back()),
                "返回：回到上一界面，这里正在编辑的内容不会保存。");
            _nameLabel = FindText("lbl-effect-name");
            // 费用显示=文本 lbl-effect-cost（2026-10-05 预制体改版：CostSquares 方格退役）——
            // 参数有效时展示完整费用计算（RefreshName 驱动）；无效组合不显示内容
            _costLabel = Find("lbl-effect-cost")?.GetComponent<TMP_Text>();
            if (_costLabel != null)
                UiKit.Described(_costLabel,
                    "费用预览：主干各原子费用＋合计；分支 Then 奖励免费（不计合计）。"
                    + "分支槽奖励超出可实现费用时按槽区红框提示调整后才能保存。");
            // 发动四参数（2026-10-09 prefab 重排）：activate-bar 静态四组（way/timing/speed/take-effect，
            // 每组=标签+子节点 dropdown）；原 root 级 dropdown-activation/dropdown-timing/timing-row
            // 与设置盒 speed/limit 随设置盒拆除退役
            _activateBar = Find("activate-bar");
            if (_activateBar == null)
                Debug.LogError("[EffectComposer] 缺 activate-bar（发动四参数条）——检查 EffectUI.prefab");
            _wayGroup = UiKit.FindDeep(_activateBar, "way") as RectTransform;
            _activationDropdown = BindActivateBarDropdown("way",
                ActivationChoices.Select(c => c.label).ToList(), 0);
            _timingDropdown = BindActivateBarDropdown("timing", new List<string> { "—" }, 0);
            _timingRow = UiKit.FindDeep(_activateBar, "timing") as RectTransform;
            BindActivateBarTiers(); // 速度/生效次数（静态一次绑定——写入走当前 _graph.header）
            // 表切换按钮（原 btn-reload-atoms/btn-load-effects 两钮合并）：按下在 原子库↔效果表 间切换，
            // 标签常显"将切到哪边"。烘焙节点名 btn-switch-table（prefab 接管后删旧两钮）。
            UiKit.Described(UiKit.BindableButton("btn-switch-table", Root, "显示效果表", SwitchTable,
                UiStyle.BtnPrimary, UiStyle.White),
                "切换右栏显示：原子库 ↔ 效果表（效果表里点击一条可载入继续编辑）。");
            _tableToggleLabel = UiKit.FindDeep(Root, "btn-switch-table")?.GetComponentInChildren<TMP_Text>(true);
            _saveBtn = BindButton("btn-save", OnSave);
            UiKit.Described(_saveBtn,
                "保存：编辑卡牌时写回这张卡的效果；从效果库进入则存回效果库。"
                + "存在不合规设置时不可点——按红框提示调整后再保存。");
            // 删除按钮：仅"从效果表载入的库效果"可删（卡编辑/新建态禁用）——prefab 烘焙 btn-delete 优先
            _deleteBtn = UiKit.BindableButton("btn-delete", Root, "删除", OnDeleteEffect,
                UiStyle.BtnDanger, UiStyle.White);
            UiKit.Described(_deleteBtn,
                "删除：删掉效果库里的这条效果（只在编辑效果库条目时可用；编辑卡牌效果的保存不受影响）。");

            // ---- 主体 ----
            _modeBar = Find("mode-bar");
            _slotArea = FindScroll("slot-area");
            PrepareSlotTemplates(); // prefab 烘焙的 slot/settings 样板转隐藏模板（克隆用）

            // 描述条（左栏底部——选中才出现）
            EnsureDescStrip();

            // 右：展示区（双模式）
            _filterTypeDropdown = BindDropdown("dropdown-filter-type",
                new List<string> { "全部" }, 0);
            _filterName = FindInput("field-filter-name");
            if (_filterName != null)
                _filterName.onValueChanged.AddListener(_ =>
                {
                    RefreshLibrary();
                    if (_rightMode == RightMode.Effects) RefreshEffectsList();
                });

            _libraryList = FindScroll("list-library"); // 2026-10-03 手改预制体重命名（原 library-list）
            _effectsList = FindScroll("list-effects");

            // 描述条路由（红框闪烁已迁 shader——GuideFrame 自驱，无 C# 帧泵）
            UiKit.DescRequested += OnDescRequested;
        }

        /// <summary>描述条单名绑定（2026-10-07 兜底退役）：desc-strip 下 lbl-prop-desc；
        /// 缺节点 LogError 留空——不再代码补建。</summary>
        private void EnsureDescStrip()
        {
            _descStripNode = Find("desc-strip");
            var lbl = UiKit.FindDeep(_descStripNode, "lbl-prop-desc");
            _descStrip = lbl?.GetComponent<TMP_Text>();
            if (_descStrip == null)
                Debug.LogError("[EffectComposer] desc-strip 缺 lbl-prop-desc 文本——检查 EffectUI.prefab");
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

        /// <summary>描述条统一写口（空串=隐藏）：控件功能描述（DescRequested）与校验错误文本共用。</summary>
        private void SetDescStrip(string text)
        {
            if (_descStrip == null) return;
            _descStrip.text = text ?? "";
            _descStripNode.gameObject.SetActive(!string.IsNullOrEmpty(_descStrip.text));
        }

        private void OnDescRequested(string desc) => SetDescStrip(desc);

        private void OnEnterInternal()
        {
            BuildFilterCallbacks(); // 分类下拉回调（Build 后一次；重读原子表只重建选项）

            // 卡编辑会话（一次性消费）：进入即改写 _graph 为该效果的深拷贝
            var (card, index) = ComposerSession.Take();
            if (card != null)
            {
                _editingCard = card;
                _editingIndex = index;
                _graph = CardEffectToGraph(index >= 0 && index < card.Effects?.Count ? card.Effects[index] : null,
                    out var note);
                _loadedFromLibraryId = null;
                string msg = index >= 0 ? $"编辑效果 #{index + 1}（保存写回卡牌）" : "新建效果（保存追加到卡牌）";
                if (note != null) msg += $"——载入归一化：{note}"; // 两槽定案：遗留门步骤折叠/主干截断可见化
                ShowToast(msg);
            }
            else
            {
                // 效果库模式：进屏=新组合（2026-10-09 修复）。此前不复位——上次会话的组合与
                // _loadedFromLibraryId 跨访问残留，原位改组另一效果再保存会把上一条库效果当
                // 「旧档」删除（两条独立效果互相覆盖的根因之一）。卡编辑态返回未保存同样在此清。
                _editingCard = null;
                _editingIndex = -1;
                _graph = CardEffectToGraph(null, out _);
                _loadedFromLibraryId = null;
                _expandedAtoms.Clear();
                _expandedRewards.Clear();
            }

            BuildTimingDropdown();
            BuildActivationDropdown();
            _mode = InferMode(_graph);
            EnsureDefaultSelection();
            RefreshAll();
            SetRightMode(RightMode.Atoms);
            ValidateZones(); // 初始校验（载入数据可能自带违规——如超预算奖励）

            // 空库自诊断：正常应 ≥91 行（87+改写仪典 4 行）——为空说明表未装载或初始化半途出错（查 Console）
            int libCount = _libraryList?.Content?.childCount ?? 0;
            if (libCount == 0)
                ShowToast($"原子库为空（表加载失败或初始化异常——查 Console；分类项 {_filterTypeChoices?.Count ?? 0}）");
        }

        // ======================================== 元信息（顶栏） ========================================

        private void BuildTimingDropdown()
        {
            if (_timingDropdown == null) return; // 预制体缺节点——BindDropdown 已报，时机编辑不可用
            // 全中文时机下拉（主动三档 Activate_* 不入——主动不设时机；系统自用时点不入
            // ——OnAtomicEffect* 三段/OnSummon 召唤进场/OnPhase* 阶段开始结束/OnGameStart 游戏开始，
            // 2026-10-09 定案不开放玩家选择）
            _timings = TimingZh.Keys.ToList();
            var labels = _timings.Select(t => TimingZh[t]).ToList();
            _timingDropdown.SetOptions(labels, 0);
            SyncTimingDropdown();
            _timingDropdown.Changed += (idx, _) =>
            {
                if (idx >= 0 && idx < _timings.Count) _graph.header.TriggerTiming = (int)_timings[idx];
                ApplyOnceTimingLimitLock(); // 切到必然单发时机：作用次数即时锁 1 禁改
            };
            _timingDropdown.Describe(
                "触发时机：自动效果在什么时候生效；主动发动的效果由你手动发动，不选时机。",
                idx => idx >= 0 && idx < _timings.Count ? TimingDescZh(_timings[idx]) : null);
        }

        private void SyncTimingDropdown()
        {
            if (_timingDropdown == null) return;
            int current = _timings.IndexOf((TriggerTiming)_graph.header.TriggerTiming);
            // 不在下拉的时机（主动三档/系统自用时点：OnAtomicEffect* 三段、OnSummon、OnPhase*、OnGameStart）：
            // 占位显示首项、不写回（SetIndex 默认不通知——header 真值保留，改选才落值）
            _timingDropdown.SetIndex(current < 0 ? 0 : current);
        }

        private void BuildActivationDropdown()
        {
            _activationDropdown?.SetOptions(ActivationChoices.Select(c => c.label).ToList(), ActivationIndex(_graph.header.ActivationType));
            SyncActivationVisibility();
            _activationDropdown?.Describe(
                "发动方式：自动看条件弹窗询问，主动由你手动发动——点选项看各自说明。",
                idx => idx == 0
                    ? "自动：条件达成时游戏弹窗问你发不发动；什么时候问由「触发时机」决定，不比拼速度。"
                    : "主动：由你手动发动——发动时才支付费用并横置这张卡；速度 1 以上还能在对手回合当响应打出（见「发动速度」）。");
            if (_activationDropdown != null)
                _activationDropdown.Changed += (idx, _) =>
                {
                    if (idx >= 0 && idx < ActivationChoices.Length)
                        _graph.header.ActivationType = ActivationChoices[idx].value;
                    SyncActivationVisibility();
                };
        }

        /// <summary>发动方式/时机的可见性与档位锁定（2026-10-05 两槽定案）：
        /// 并列=主动/自动两态全开（主动档时机钉 Activate_*、离开回落 OnPlay——2026-10-02 定案不变；
        /// 分支钉强制档已删——分支时机由槽级 branch 载荷承载，不再锁效果头）；
        /// 光环=无时机/方式（载体入场即生效，离场/被无效即失效）——时机钉 OnPlay、隐藏两下拉。</summary>
        private void SyncActivationVisibility()
        {
            if (_timingRow == null && _activationDropdown == null) return;
            bool aura = _mode == ComposeMode.Aura;
            var h = _graph.header;

            if (aura)
            {
                h.ActivationType = 0; // 强制档（光环=入场生效无需发动方式）
                _activationDropdown?.SetIndex(0);
                h.TriggerTiming = (int)TriggerTiming.OnPlay;
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

            _wayGroup?.gameObject.SetActive(!aura); // 方式组整体（标签+下拉）——光环无发动方式
            _timingRow?.gameObject.SetActive(!aura && h.ActivationType != 2);

            SyncSettingsRowVisibility(); // 发动行显隐联动（设置盒随最新绑定卡对齐）
            ApplyOnceTimingLimitLock();  // 方式切换钉时机后重裁必然单发锁（主动档隐藏时机=不生效）
        }

        /// <summary>发动行显隐（2026-10-05 定案；2026-10-09 activate-bar 迁移）：主动=速度组显/次数组隐，
        /// 非主动（自动/系统）反之；光环形态四组全隐（无发动概念——设置盒拆除口径并轨）。</summary>
        private void SyncSettingsRowVisibility()
        {
            bool aura = _mode == ComposeMode.Aura;
            bool voluntary = !aura && _graph.header.ActivationType == (int)EffectActivationType.Voluntary;
            if (_speedGroup != null) _speedGroup.gameObject.SetActive(voluntary);
            if (_limitGroup != null) _limitGroup.gameObject.SetActive(!aura && !voluntary);
        }

        /// <summary>activate-bar 组内下拉绑定（2026-10-09 prefab 重排：组名 → 子节点 dropdown）。</summary>
        private UiKit.Dropdown BindActivateBarDropdown(string group, List<string> options, int index)
        {
            var node = UiKit.FindDeep(_activateBar, group);
            var tmp = node != null ? node.GetComponentInChildren<TMP_Dropdown>(true) : null;
            if (tmp == null)
            {
                Debug.LogError($"[EffectComposer] activate-bar/{group} 缺 dropdown——检查 EffectUI.prefab");
                return null;
            }
            return new UiKit.Dropdown(tmp, options, index, null);
        }

        /// <summary>速度/生效次数静态绑定（2026-10-09 设置盒拆除迁入 activate-bar）：
        /// 档位与描述沿 2026-10-05 定案；写入走当前 _graph.header（图更换不失效），
        /// 值对齐见 SyncActivateBarValues（静态节点不随克隆重建）。</summary>
        private void BindActivateBarTiers()
        {
            if (_activateBar == null) return;
            _speedGroup = UiKit.FindDeep(_activateBar, "speed") as RectTransform;
            _limitGroup = UiKit.FindDeep(_activateBar, "take-effect") as RectTransform;
            _ddSpeed = BindTierDropdown(_speedGroup, SpeedTiers, _graph.header.BaseSpeed,
                v =>
                {
                    _graph.header.BaseSpeed = v;
                    RefreshName(); // 速度影响计价（2026-10-09 入价：0 普通 ×1 / 1 瞬间 ×1.5 / 2 高速 ×2）——费用预览随改随刷
                }, // 动态读当前图——静态节点跨会话存续
                "发动速度：主动效果的出手快慢——越快响应权越强、费用越贵；点选项看各档说明。",
                v => v.ToString(),
                v => v switch
                {
                    0 => "普通档：只能在自己回合的主要阶段发动，不能响应（基础价）。",
                    1 => "瞬间档：可以在对手回合发动、当作响应使用（费用 1.5 倍）。",
                    _ => "高速档：比 1 速更快——能响应 1 速的效果，抢先更容易（费用 2 倍）。",
                });
            _ddLimit = BindTierDropdown(_limitGroup, LimitTiers, _graph.header.TriggerLimitPerTurn,
                v =>
                {
                    _graph.header.TriggerLimitPerTurn = v;
                    RefreshName(); // 作用次数影响计价——费用预览随改随刷
                },
                "作用次数：自动效果每回合最多能生效几次——点选项看各档说明。",
                optionDesc: v => v switch
                {
                    1 => "每回合最多 1 次（基础价）。",
                    2 => "每回合最多 2 次（费用 1.5 倍）。",
                    3 => "每回合最多 3 次（费用 2 倍）。",
                    _ => "不限次数（费用 4 倍）；回合开始/结束等必然单发时机会被系统锁 1 不可改。",
                });
        }

        /// <summary>activate-bar 值对齐（静态节点——图更换/形态切换后重显真值）。</summary>
        private void SyncActivateBarValues()
        {
            var h = _graph?.header;
            if (h == null) return;
            if (_ddSpeed != null)
            {
                _ddSpeed.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(SpeedTiers, h.BaseSpeed)));
                _ddSpeed.RefreshShownValue();
            }
            if (_ddLimit != null)
            {
                _ddLimit.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(LimitTiers, h.TriggerLimitPerTurn)));
                _ddLimit.RefreshShownValue();
            }
            ApplyOnceTimingLimitLock();
        }

        // ======================================== 效果名自动构成 ========================================

        /// <summary>自动名（2026-10-05 两槽定案）：并列=逐槽 AtomText.RenderAtomEntry+BranchSuffix 拼接
        ///（空槽不占位）；光环=条目列表×箭头数；规则级光环=仪典名（双方生效·全局唯一，无箭头）。
        /// FreeBranch/OutcomeGate 专属文案分支已随形态删除。</summary>
        private string AutoName()
        {
            var h = _graph.header;
            if (_mode == ComposeMode.Aura)
            {
                // 投放步（仪典/改写，2026-10-07 定案句式）：仪典=「规则光环：【范围】描述」
                //（表 Description 已带「规则光环：」前缀——剥离后插范围）；改写（降级连接光环）
                //=「【范围】受光环影响的…」（表文案原样）。
                var ruleParts = new List<string>();
                foreach (var s in _graph.steps ?? new List<EffectStepData>())
                {
                    if (s?.kind != 0 || s.atomic == null) continue;
                    var rc = AtomicEffectTable.GetByHashId(s.atomic.refId);
                    if (!IsRitualStepRow(rc)) continue;
                    string scopeZh = RuleAuraSystem.RuleAuraScopeZh(s.atomic.value);
                    if (IsRuleAuraRow(rc))
                    {
                        var desc = rc.Description ?? "";
                        const string Prefix = "规则光环：";
                        if (desc.StartsWith(Prefix)) desc = desc.Substring(Prefix.Length);
                        ruleParts.Add($"规则光环：【{scopeZh}】{desc}");
                    }
                    else ruleParts.Add($"【{scopeZh}】{rc.Description}");
                }
                if (ruleParts.Count > 0) return string.Join("；", ruleParts);

                // 连接条目：统一「受光环影响的…」句式（2026-10-07）；2026-10-08 条目级作用面：
                // 作用面条目=【范围】前缀（关键词条目含角色尾注），连接方向档条目归尾组×N箭头
                var scopedParts = new List<string>();
                var arrowParts = new List<string>();
                foreach (var a in h.LinkAuras ?? new List<LinkAuraData>())
                {
                    if (a == null) continue;
                    string body;
                    if (!string.IsNullOrEmpty(a.stat))
                        body = $"受光环影响的生物{StatZhOf(a.stat)}{a.value:+0;-0}";
                    else if (!string.IsNullOrEmpty(a.keyword))
                        body = $"受光环影响的单位获得{KeywordZh(a.keyword)}"; // 关键词条目恒含角色（2026-10-09 裁定）
                    else continue;
                    if (a.scope > 0)
                        scopedParts.Add($"【{LinkScopeZh(a.scope)}】{body}");
                    else arrowParts.Add(body);
                }
                if (scopedParts.Count > 0 || arrowParts.Count > 0)
                {
                    var all = new List<string>(scopedParts);
                    if (arrowParts.Count > 0)
                    {
                        int n = CountArrowBits((HexDirection)h.ArrowDirections);
                        all.Add(n > 0 ? $"{string.Join("，", arrowParts)}×{n}箭头" : string.Join("，", arrowParts));
                    }
                    return string.Join("；", all);
                }
            }
            // 并列：逐槽 主干描述+分支后缀（BranchSuffix 无载荷返回空串）
            var parts = new List<string>();
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                string body = AtomText.RenderAtomEntry(s.atomic, h);
                string suffix = AtomText.BranchSuffix(s.atomic);
                parts.Add(suffix.Length > 0 ? $"{body}{suffix}" : body);
            }
            return parts.Count == 0 ? "（空）" : string.Join("＋", parts);
        }

        /// <summary>光环关键词中文名（2026-10-09 改口径：与卡组合/构筑/悬浮卡三处 KeywordZh 同源
        /// ——关键词定义 nameZh=原子表中文短名，关键词不单独开表；未登记回落旧脏值映射，
        /// "Armor"→坚韧为 2026-10-08 指示物化前的旧数据兼容显示，再回落原样 id）。</summary>
        private static string KeywordZh(string keywordId)
        {
            var def = CardLoader.GetKeywordDefinition(keywordId);
            if (!string.IsNullOrEmpty(def?.nameZh)) return def.nameZh;
            return keywordId switch
            {
                "Armor" => "坚韧",
                "Guardian" => "守护",
                null or "" => "？",
                _ => keywordId,
            };
        }

        /// <summary>条目 stat 短名（Both=属性——攻生同值 ±1/±1，2026-10-07 深夜三档）。</summary>
        private static string StatZhOf(string stat)
            => stat.Equals("Life", StringComparison.OrdinalIgnoreCase) ? "生命"
             : stat.Equals("Both", StringComparison.OrdinalIgnoreCase) ? "属性" : "攻击";

        /// <summary>条目级作用范围短名（2026-10-08 actuating-range：0=连接方向/1己/2双/3对）。</summary>
        private static string LinkScopeZh(int scope)
            => scope == 1 ? "己方" : scope == 2 ? "双方" : scope == 3 ? "对方" : "连接方向";

        /// <summary>顶栏效果名=完整效果描述（2026-10-03 定案：不再简写组合名——空效果回落自动名占位）；
        /// 光环形态（2026-10-07）改走 AutoName 三段句式（仪典/改写/条目——范围前置、描述取表行）。
        /// 2026-10-09 单源化：顶栏预览与保存（graph.name/header.DisplayName）同取此串——
        /// 名字同时是效果 id 的哈希基底（HashEffect NM 段），两处不同源会出现「预览对、存后名字不一致」。</summary>
        private string CurrentDisplayName()
        {
            var full = AtomText.RenderEffectSummary(_graph);
            return _mode == ComposeMode.Aura ? AutoName()
                : (string.IsNullOrEmpty(full) ? AutoName() : full);
        }

        private void RefreshName()
        {
            _nameLabel.text = CurrentDisplayName();
            if (_costLabel != null) _costLabel.text = EffectCostText();
        }

        /// <summary>顶栏费用文本（lbl-effect-cost，2026-10-05 预制体改版方格→文本）：
        /// 并列=主干段（参数合规才显示：效果锚价完整构成——逐原子贡献＋合计＋黑白获得）
        /// ＋分支段（2026-10-09 定案：只声明「分支（奖励免费）」——奖励锚价/上限不随段列出，
        /// 超上限提示由槽区校验红框承载、保存已禁；分支段独立于主干校验恒显——超限时主干段隐藏，
        /// 分支段在案提示）；光环=条目平价（箭头属卡面资产、挂卡并集后由卡层计——非本效果层
        /// 费用项，不列）；主干校验违规/转换失败/推导异常→对应段空串。</summary>
        private string EffectCostText()
        {
            try
            {
                if (_mode == ComposeMode.Aura) return AuraTopCostText();
                string trunk = ZonesAllValid() ? ParallelCostText() : "";
                string branch = BranchCostText();
                if (trunk.Length == 0) return branch;
                return branch.Length == 0 ? trunk : trunk + "｜" + branch;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>分支段文本（2026-10-09 定案）：引擎/其余分支只声明「分支（奖励免费）」；**附加诅咒/祝福
        /// 例外**（同日附加指示物定案）——不再奖励免费：按分支实际填入的诅咒/祝福费用减半向上取整入价
        ///（EngineBranchSurchargeHalf 与卡价 DeriveElementCosts.EngineBranchSurcharge 同源）；
        /// 超上限提示由槽区校验红框承载（保存已禁），未设奖励的分支不列。</summary>
        private string BranchCostText()
        {
            var steps = _graph.steps ?? new List<EffectStepData>();
            for (int i = 0; i < steps.Count; i++)
            {
                var b = steps[i]?.kind == 0 ? steps[i].atomic?.branch : null;
                if (b?.then?.FirstOrDefault() == null) continue;
                var ek = Enum.IsDefined(typeof(BranchEngineKind), b.engine) ? (BranchEngineKind)b.engine : BranchEngineKind.None;
                if (ek == BranchEngineKind.CurseOnDraw || ek == BranchEngineKind.BlessingOnDraw)
                {
                    var inst = CardEffectConverter.ConvertAtomForUI(b.then[0]);
                    float half = inst != null
                        ? CostDerivationService.EngineBranchSurchargeHalf(new List<AtomicEffectInstance> { inst })
                        : 0f;
                    return $"分支（{ComposerCatalog.EngineZhOf(ek)}费减半：{half:0.#}）";
                }
                return "分支（奖励免费）";
            }
            return "";
        }

        /// <summary>全部校验区合规（无副作用查询：只跑 Check 判空，不动红框/错误标签——与 ValidateZones 互不干扰）。</summary>
        private bool ZonesAllValid()
        {
            foreach (var z in _slotZones)
            {
                if (z?.Zone == null || z.Check == null) continue;
                string err = null;
                try { err = z.Check(); } catch { /* 校验异常按合规处理——同 ValidateZones 口径 */ }
                if (!string.IsNullOrEmpty(err)) return false;
            }
            return true;
        }

        /// <summary>并列效果费用构成（效果锚价口径：ConvertOne+DeriveElementCosts 纯表累加、
        /// 无减免抵消；代价不参与——代价栏已上移卡组合层）。逐原子贡献=同头单原子 shim 单独推导
        /// （与 RewardDerivedCost 同手法；DeriveElementCosts 按原子独立累加，shim 分账与整效果推导一致）。</summary>
        private string ParallelCostText()
        {
            var def = CardEffectConverter.ConvertOne(BuildCardEffect(), "COMPOSER_COST_PREVIEW");
            if (def == null) return "";
            var total = CostDerivationService.DeriveElementCosts(def, 0);

            var parts = new List<string>();
            foreach (var atom in BillableAtomsOf(def))
                parts.Add($"{AtomPartLabel(atom)}＝{CostZh(CostDerivationService.DeriveElementCosts(PricingShim(def, atom), 0))}");
            var text = parts.Count == 0 ? "" : "费用：" + string.Join("＋", parts) + $"｜合计 {CostZh(total)}";

            // 错边原子出计价转黑白获得（构筑期口径；运行时按实际命中发放）
            var grants = CostDerivationService.DeriveElementGrants(def, 0);
            if (grants != null && !grants.IsZero)
                text = (text.Length > 0 ? text + "｜" : "费用：") + $"获得 {CostZh(grants)}";
            return text;
        }

        /// <summary>计费原子遍历（镜像 CostDerivationService.VisitBillableAtoms 主序列口径——
        /// 主干原子 + 抉择首模式原子；分支 Then 奖励免费不列——分支段另见 BranchCostText）。</summary>
        private static IEnumerable<AtomicEffectInstance> BillableAtomsOf(EffectDefinition def)
        {
            if (def.Steps != null && def.Steps.Count > 0)
            {
                foreach (var step in def.Steps)
                {
                    if (step == null) continue;
                    if (step.Kind == RuntimeStepKind.Atomic && step.Atomic != null)
                        yield return step.Atomic;
                    else if (step.Kind == RuntimeStepKind.Choice && step.Choices != null && step.Choices.Count > 0)
                    {
                        var chosen = step.Choices[0];
                        if (chosen == null) continue;
                        foreach (var s in chosen)
                            if (s != null && s.Kind == RuntimeStepKind.Atomic && s.Atomic != null)
                                yield return s.Atomic;
                    }
                }
            }
            else if (def.Effects != null)
            {
                foreach (var atom in def.Effects)
                    if (atom != null) yield return atom;
            }
        }

        /// <summary>单原子计价 shim：克隆影响计价的全部效果头字段（持续/目标数量/选择模式/作用次数/
        /// 触发时机/发动速度/逐原子目标制/组合域），Effects 只装该原子——推导值＝该原子在当前参数下的真实贡献。</summary>
        private static EffectDefinition PricingShim(EffectDefinition def, AtomicEffectInstance atom) =>
            new EffectDefinition
            {
                Id = "COST_PART_PREVIEW",
                DisplayName = def.DisplayName,
                ActivationType = def.ActivationType,
                TriggerTiming = def.TriggerTiming,
                BaseSpeed = def.BaseSpeed,             // 速度入价（2026-10-09）：分账与合计同乘
                Duration = def.Duration,
                DurationValue = def.DurationValue,
                SelectionMode = def.SelectionMode,
                TargetCount = def.TargetCount,
                RandomTarget = def.RandomTarget,
                TriggerLimitPerTurn = def.TriggerLimitPerTurn,
                TargetDomain = def.TargetDomain,
                PerAtomTargets = def.PerAtomTargets,   // 对称减半逐原子口径（2026-10-09）：分账不回落共享臂
                Effects = new List<AtomicEffectInstance> { atom },
            };

        /// <summary>原子行短标签：中文短名＋数值（有 {value} 模板才带值——同槽卡摘要口径）。</summary>
        private static string AtomPartLabel(AtomicEffectInstance atom)
        {
            var cfg = AtomicEffectTable.GetByType(atom.Type);
            if (cfg == null) return atom.Type.ToString();
            bool hasValue = (cfg.Description ?? "").Contains("{value}");
            return hasValue ? $"{cfg.DisplayName}{atom.Value}" : cfg.DisplayName;
        }

        /// <summary>费用文本化：非零色按序拼「N色」（如 2红1蓝；全零=0）。</summary>
        private static string CostZh(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            var parts = new List<string>();
            foreach (var color in cost.NonzeroColors())
                parts.Add($"{(int)cost[color]}{ColorZh(color)}");
            return string.Join("", parts);
        }

        /// <summary>光环模式顶栏费用：条目平价（Stage A 明细，AuraPreviewCard 同源——
        /// 2026-10-07 面板 aura-cost 行退役后顶栏独占）；
        /// 不提箭头费（箭头属卡面资产，挂卡并集后由卡层计，非本效果层费用变动项）。</summary>
        private string AuraTopCostText()
        {
            var lines = CardCore.CardCostService.Derive(AuraPreviewCard()).Breakdown
                .Where(l => l != null && l.Stage == "A").ToList();
            if (lines.Count == 0) return "";
            var parts = lines.Select(l =>
                $"{l.Label}＝{l.Value:0.#}{(l.Color.HasValue ? ColorZh(l.Color.Value) : "")}");
            return "费用（光环条目）：" + string.Join("；", parts);
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
            SyncActivateBarValues(); // activate-bar 静态节点——图更换后重显真值（2026-10-09 设置盒拆除）
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
            _graph = CardEffectToGraph(null, out _);
            _mode = InferMode(_graph);
            _expandedAtoms.Clear();
            _expandedRewards.Clear();
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
            // 不清重建：模式片按名绑定烘焙节点（mode-Parallel/mode-Aura）；芯片文案以预制体为准
            //（2026-10-09 定案：代码不覆写 UI 设置好的文本——BindableButton 传 null 跳过回写），
            // 刷新只更新态色/接线
            MakeModeChip(ComposeMode.Parallel,
                "并列形态：最多两个槽，各放一颗原子组成效果；或只放一个原子、给它加分支条件（条件成立才有奖励）——两原子并列与分支互斥，不可兼得。");
            MakeModeChip(ComposeMode.Aura,
                "光环形态：效果挂在卡上持续生效，作用于连接箭头指向的单位；在下方配条目（属性/关键词）。");

            // 卡编辑模式提示（常驻节点按名绑定——非编辑时隐藏；新建才定宽，烘焙宽以节点为准）
            bool tagExisted = _modeBar.Find("editing-tag") != null;
            var tag = UiKit.BindableLabel("editing-tag", _modeBar,
                _editingCard != null ? $"正在编辑：{_editingCard.CardName} #{_editingIndex + 1}" : "",
                UiStyle.MiniSize, UiStyle.TextHint);
            var tagNode = _modeBar.Find("editing-tag");
            if (tagNode != null) tagNode.gameObject.SetActive(_editingCard != null);
            if (!tagExisted && tag != null) UiKit.Size(tag, w: 200f);
        }

        private void MakeModeChip(ComposeMode mode, string desc)
        {
            bool active = mode == _mode;
            // label=null：不覆写预制体文本（UiKit.BindableButton text==null 跳过回写）——只换态色/挂接线
            UiKit.Described(UiKit.BindableButton($"mode-{mode}", _modeBar, null, () => SwitchMode(mode),
                active ? UiStyle.ChipActiveBg : UiStyle.BtnBg,
                active ? UiStyle.ChipActiveText : UiStyle.TextBody), desc);
        }

        /// <summary>形态切换（两形态互切，2026-10-05 两槽定案）：best-effort 数据搬运（原子尽量保留），
        /// 切换后收起展开卡。光环字段只在光环形态存续——切出即清（防非光环效果夹带箭头落库）；
        /// 切入光环清原子步；并列切出清光环字段。原 FreeBranch/OutcomeGate 分支已随形态删除。</summary>
        private void SwitchMode(ComposeMode newMode)
        {
            if (newMode == _mode) return;
            var h = _graph.header;
            _graph.steps ??= new List<EffectStepData>();

            switch (newMode)
            {
                case ComposeMode.Parallel:
                {
                    // 切出光环：清 LinkAuras/箭头，留一枚种子原子（分支载荷随原子保留）
                    AtomicEffectEntry seed = FirstKind0Atom();
                    h.AtomicEffects = null; // 扁平/奖励通道退役——防夹带
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
                    _graph.steps.Clear();
                    if (seed != null) _graph.steps.Add(new EffectStepData { kind = 0, atomic = seed });
                    ShowToast("切换为并列（原子已尽量保留）");
                    break;
                }
                case ComposeMode.Aura:
                {
                    // 光环（效果层定案）＝箭头+光环条目随效果合成——任意会话可用；
                    // 作用对象不可指定（运行时 live-query 箭头指向格占据者）；原子数据清空防夹带。
                    h.AtomicEffects = null;
                    _graph.steps.Clear();
                    ShowToast("光环模式：右侧光环库点击选择（新选即替换上一个）；连接方向的光环在光环槽底部选箭头");
                    break;
                }
            }

            // 逐原子目标制（2026-10-09）：header.TargetKinds 恒空（作用范围不再手选）——切形态清残留防夹带
            h.TargetKinds = null;

            _mode = newMode;
            _expandedAtoms.Clear();
            _expandedRewards.Clear();
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
            ResetGuideFrame(); // 高亮框可能挂在即将销毁的区下——先回家（EffectUI 根）防连带销毁
            ClearContent(_slotArea.Content);
            _slotZones.Clear(); // 槽区校验区随重建
            ClampSelection();
            _graph.steps ??= new List<EffectStepData>();

            switch (_mode)
            {
                case ComposeMode.Parallel:
                {
                    // 2026-10-05 两槽定案：槽位循环 for i<2；2026-10-09 并列/分支互斥：分支盒与第二槽
                    // 默认齐开、单向互斥——①任一槽分支已开（引擎主干行填槽即分支/gate 下拉非「无」）
                    // → 第二槽整槽不出（分支=单原子形态）；②第二槽已有步（原子/只读）→ 第一槽分支
                    // 编辑区不渲染（并列=无分支形态），第二槽永不渲染分支区。判定单源=StepBranchActive
                    //（按行身份∪载荷——引擎行载荷是落槽期挂的，只查 branch 会漏第一拍）。
                    bool branchOn = _graph.steps.Any(StepBranchActive);
                    RectTransform firstEmptySlot = null;
                    for (int i = 0; i < ParallelSlotCount; i++)
                    {
                        if (i == 1 && branchOn) break; // 分支形态：第二槽隐藏（含空槽占位与"满 2"提示）
                        int idx = i;
                        var slot = MakeSlot($"原子槽 {i + 1}");
                        if (slot == null) continue; // 模板缺失——该槽跳过（MakeSlot 已报）
                        MarkSelected(slot, SelKind.Parallel, i);
                        // 越过空档的槽不可选（保持无空档填充）
                        SelectOnClick(slot, SelKind.Parallel, i, i > _graph.steps.Count);
                        if (i < _graph.steps.Count)
                        {
                            var step = _graph.steps[i];
                            if (step.kind == 0 && step.atomic != null)
                            {
                                MakeAtomCard(slot, step.atomic, idx, SelKind.Parallel);
                                // 分支编辑区仅第一槽、且第二槽为空（互斥②；第二槽引擎行被库过滤拦在门外）
                                if (i == 0 && _graph.steps.Count < ParallelSlotCount)
                                    MakeBranchEditor(slot, step.atomic, idx);
                            }
                            else MakeReadonlyStepBadge(slot, step); // 抉择等只读
                        }
                        else if (i == _graph.steps.Count) firstEmptySlot ??= slot; // 空槽提示已去——slot-title 即空槽位
                    }
                    if (_graph.steps.Count >= ParallelSlotCount)
                        MakeHint(_slotArea.Content, "已满 2 原子——点击槽选中后，右侧点击即替换");
                    // 校验：并列模式至少一个原子（只读抉择步计入内容——卡组成阶段产物）
                    if (firstEmptySlot != null)
                        _slotZones.Add(MakeZone(firstEmptySlot, () =>
                            !_graph.steps.Any(s => s != null && ((s.kind == 0 && s.atomic != null) || s.kind == 2))
                                ? "效果为空——并列模式至少需要一个原子（选中槽后点击右侧库行）" : null));
                    // 全局唯一（2026-10-09 定案·库隐藏口径）：同原子只出现一次——库已隐不可再添，
                    // 本区仅拦载入旧数据/旁路（隐藏交互、不拒绝）
                    _slotZones.Add(MakeZone(_slotArea.Content, DuplicateAtomError));
                    break;
                }
                case ComposeMode.Aura:
                {
                    MakeAuraPanel(_slotArea.Content);
                    break;
                }
            }

            // 设置盒已随 2026-10-09 prefab 重排拆除：发动四参数=activate-bar 静态组（Build 一次绑定），
            // 数量/随机/衍生物=每原子卡面行（MakeAtomCard/MakeRewardSlot 逐卡绑定）
            ApplyEngineHeaderLock(); // 引擎在案→效果头四值钉预设+四控件禁用（2026-10-09 九项定案，含载入/换引擎路径）
            RefreshName();
            ValidateZones(); // 槽区重建后立即校验
        }


        // ======================================== 槽位选中（右侧点击=替换选中槽） ========================================

        /// <summary>槽高亮（2026-10-05 实测修正：与展开卡一致）——展开卡所在槽即高亮；
        /// 无展开时回落选中槽（库点击落点提示）。选中态=焦点底色（Outline 描边已随闪光体系退役）。</summary>
        /// <summary>槽高亮（2026-10-06 二次修正）：颜色只跟选中槽走（单选——选别处即褪色）；
        /// 展开态独立多卡并存（不收缩），不再携带高亮。选中态=焦点底色（Outline 描边已退役）。</summary>
        private void MarkSelected(RectTransform slot, SelKind kind, int index)
        {
            bool selected = _selSlot == kind && _selSlotIndex == index;
            if (selected)
            {
                var img = slot.GetComponent<Image>();
                if (img != null) img.color = new Color(70f / 255f, 110f / 255f, 170f / 255f, 0.32f); // 焦点态底（USS .drop-slot--selected）
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
                bool already = kind == SelKind.Reward
                    ? _selSlot == SelKind.Reward && _selRewardSlot == index
                    : _selSlot == kind && _selSlotIndex == index;
                if (already) return;
                SelectSlot(kind, index);
            });
        }

        private void SelectSlot(SelKind kind, int index)
        {
            if (kind == SelKind.Reward) _selRewardSlot = index;
            else _selSlotIndex = index;
            // 2026-10-06 定案：选中槽不再收起任何展开卡（多卡独立展开）
            _selSlot = kind;
            RefreshSlots();
            RefreshRightPanels(); // 库按选中槽重过滤
        }

        /// <summary>右栏列表刷新：选中槽/门变化后按可用性重过滤。</summary>
        private void RefreshRightPanels()
        {
            RefreshLibrary();
        }

        /// <summary>进入/切模式/载入时的默认选中：并列=原子1；光环=无槽。</summary>
        private void EnsureDefaultSelection()
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                    _selSlot = SelKind.Parallel;
                    _selSlotIndex = 0;
                    break;
                case ComposeMode.Aura:
                    _selSlot = SelKind.Parallel; // 2026-10-06 光环共用原子槽（仪典入槽）——默认首槽
                    _selSlotIndex = 0;
                    break;
            }
        }

        /// <summary>每次刷新前校正当选中槽（模式变化/删除原子/清分支后防悬挂）；
        /// 并列槽允许 0..min(已填数,槽位数-1)（首空槽可追加）；奖励选中态目标槽须仍有分支载荷。</summary>
        private void ClampSelection()
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                    if (_selSlot == SelKind.Reward)
                    {
                        if (BranchOfSlot(_selRewardSlot) == null)
                        {
                            _selSlot = SelKind.Parallel;
                            _selSlotIndex = 0;
                        }
                    }
                    else if (_selSlot != SelKind.Parallel)
                    {
                        _selSlot = SelKind.Parallel;
                        _selSlotIndex = 0;
                    }
                    int max = Mathf.Min(_graph.steps?.Count ?? 0, ParallelSlotCount - 1);
                    if (_selSlot == SelKind.Parallel && _selSlotIndex > max) _selSlotIndex = max;
                    // 并列/分支互斥（2026-10-09）：任一槽分支已开 → 第二槽隐藏——选中钉回第一槽
                    //（防开分支前残留的第二槽选中指向隐藏槽、库按第二槽口径过滤的旁路落位）
                    if (_selSlot == SelKind.Parallel && _selSlotIndex > 0
                        && (_graph.steps ?? new List<EffectStepData>()).Any(StepBranchActive))
                        _selSlotIndex = 0;
                    // 展开态防悬挂（2026-10-06 多卡独立展开）：键=对象身份——只保留仍挂在当前 steps 上的原子/分支
                    var stepsNow = _graph.steps ?? new List<EffectStepData>();
                    _expandedAtoms.RemoveWhere(a => !stepsNow.Any(s => s?.kind == 0 && ReferenceEquals(s.atomic, a)));
                    _expandedRewards.RemoveWhere(br => !stepsNow.Any(s => s?.kind == 0 && ReferenceEquals(s.atomic?.branch, br)));
                    break;
                case ComposeMode.Aura:
                    // 2026-10-06 光环单槽定案：选中恒槽 0（奖励选中态不适用）
                    _selSlot = SelKind.Parallel;
                    _selSlotIndex = 0;
                    break;
            }
        }

        /// <summary>槽位/设置盒模板接管（2026-10-05 prefab 静态化）：prefab 在 slot-area/content 烘焙了
        /// slot 样板（slot-title + atom-card[head/editor]）与 settings 样板（row0 目标行+row1 发动行）
        /// ——Build 时分别转隐藏模板 tpl-slot 等（ClearChildren 放行 tpl-* 不销毁）；
        /// MakeSlot 每槽克隆；设置盒模板已随 2026-10-09 prefab 重排拆除（发动四参数=activate-bar 静态组）。
        /// 2026-10-06 箭头盒同批接管：烘焙 arrows 节点（head + arrow-picker 预制体实例）→ tpl-arrows，
        /// 光环形态克隆、并列形态不克隆（隐藏）。</summary>
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
            var gateRow = content.Find("gate-row");
            if (gateRow != null)
            {
                gateRow.name = "tpl-gate-row"; // 限定分支门槛行（dd-trunk=条件下拉）
                gateRow.gameObject.SetActive(false);
            }
            var rewardSlot = content.Find("reward-slot");
            if (rewardSlot != null)
            {
                // Then 奖励槽（2026-10-06 预制体化：slot-title 标题 + head 摘要 + editor 参数区）——
                // MakeRewardSlot 每槽跨级克隆；无分支不克隆（整区隐藏）
                rewardSlot.name = "tpl-reward-slot";
                rewardSlot.gameObject.SetActive(false);
            }
            var arrows = content.Find("arrows");
            if (arrows != null)
            {
                arrows.name = "tpl-arrows"; // 光环连接箭头盒（head + arrow-picker 预制体实例）
                arrows.gameObject.SetActive(false);
            }
            var auraSlot = content.Find("aura-slot") as RectTransform;
            if (auraSlot != null)
            {
                // 光环专用槽（2026-10-07 定案：光环不再与普通原子槽混用——tpl-slot 只归并列）
                auraSlot.name = "tpl-aura-slot";
                auraSlot.gameObject.SetActive(false);
                var acard = auraSlot.Find("aura-card");
                if (acard != null) acard.gameObject.SetActive(false); // 卡片按需激活（克隆体各自实例化）
            }
        }

        /// <summary>槽位克隆（2026-10-07 兜底退役）：tpl-slot 模板克隆+按名绑定；缺模板返回 null
        ///（调用方跳过该槽）、缺件 LogError——不再代码构建槽体。</summary>
        private RectTransform MakeSlot(string title)
        {
            var slot = UiKit.CloneTemplate("tpl-slot", _slotArea.Content);
            if (slot == null)
            {
                Debug.LogError("[EffectComposer] 缺 tpl-slot 槽模板（克隆失败）——检查 EffectUI.prefab");
                return null;
            }
            // 闪光提示的 Outline 已退役（违规提示=GuideFrame 遮罩框）——烘焙模板自带的 Outline 克隆时剥除
            foreach (var ol in slot.GetComponents<Outline>()) UnityEngine.Object.Destroy(ol);
            var img = slot.GetComponent<Image>();
            if (img == null) Debug.LogError("[EffectComposer] tpl-slot 根缺背景 Image——检查 EffectUI.prefab");
            else img.raycastTarget = true; // 槽整体可点（选中）
            UiKit.Size(slot, fw: 1f);
            var lbl = UiKit.FindDeep(slot, "slot-title")?.GetComponent<TMP_Text>();
            if (lbl == null) Debug.LogError("[EffectComposer] tpl-slot 缺 slot-title 文本——检查 EffectUI.prefab");
            else
            {
                lbl.text = title;
                UiKit.Size(lbl, fw: 1f);
            }
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
            // 展开态（2026-10-06 多卡独立展开）：键=原子对象身份——各卡互不收起
            bool expanded = _expandedAtoms.Contains(atom);

            var card = slot.Find("atom-card") as RectTransform;
            if (card == null)
            {
                Debug.LogError("[EffectComposer] 槽内缺 atom-card 模板节点（2026-10-05 prefab 静态化）——检查 EffectUI.prefab");
                return; // 兜底构建已退役（2026-10-07）——缺失即缺卡
            }
            card.gameObject.SetActive(true);

            // 展开区（editor）默认收起——展开卡在下方按需激活（设置盒=content 级单例，不随卡）
            UiKit.FindDeep(card, "editor")?.gameObject.SetActive(false);

            var head = UiKit.FindDeep(card, "head");
            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>();
            if (text == null) Debug.LogError("[EffectComposer] atom-card 缺 text 摘要文本——检查 EffectUI.prefab");
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
                headBtn.onClick.AddListener(() => ToggleExpand(atom));
            }

            // 原子表行四参数 des（2026-10-09 定案：作用范围/筛选标签/极性/费用基准——卡面常显）
            BindAtomDes(card, AtomicEffectTable.GetByHashId(atom?.refId));

            // 每原子目标行/衍生物行（2026-10-09 下沉定案：placeholder=数量+随机、derivative=召唤配置）
            void RefreshCard()
            {
                if (text != null)
                    text.text = (_expandedAtoms.Contains(atom) ? "▼ " : "▶ ")
                        + AtomText.RenderAtomEntry(atom, _graph.header);
                RefreshName();
                ValidateZones();
            }
            BindAtomTargetRow(card, atom, RefreshCard);
            BindAtomDerivativeRow(card, atom);

            if (expanded)
                MakeAtomEditorInline(card, atom, selKind, text);
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

        // 赋予类持续行已随 2026-10-09 指示物层化定案退役（原 BindGrantDurationRow/aura-duration 行删除）：
        // 关键词/指示物持续由各自机制承载，效果级 Duration 不再编辑——保存路径哨兵清空回落 Once 锚

        /// <summary>原子内联编辑器（prefab 静态 editor 节点：field-value 输入行[内含 slider-amp]）。
        /// headLabel=卡片摘要行（值/随机改动原地重渲，不重建槽区——修复滑条拖动中失焦）。
        /// 2026-10-09 持续行/每原子目标行迁出编辑器：持续档随指示物层化退役；数量/随机/衍生物=卡面常显行。</summary>
        private void MakeAtomEditorInline(RectTransform card, AtomicEffectEntry atom, SelKind selKind, TMP_Text headLabel)
        {
            var editor = UiKit.FindDeep(card, "editor");
            if (editor == null)
            {
                Debug.LogError("[EffectComposer] atom-card 缺 editor 节点——数值/随机不可编辑");
                return;
            }
            editor.gameObject.SetActive(true);
            // 2026-10-09 prefab 重组：editor 只剩 field-value（内含数值输入+slider-amp）——
            // 动态提示直接挂 editor 根（原 dd-trunk* 主干下拉遗留已随设置盒重排清场）
            var inject = editor;

            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type))
            {
                MakeHint(inject, $"（原子表引用缺失：{atom.refId ?? "空"}）");
                return;
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

            // ---- 关键词类原子（MountKinds 含 Keyword 位）：卡面行已承载参数——编辑器内无参数可编 ----
            if (mounts.Contains(MountKind.Keyword))
            {
                valueNode?.gameObject.SetActive(false);
                sliderNode?.gameObject.SetActive(false);
                return;
            }

            // ---- 规则光环原子（位 8 RuleAura）：Value=作用范围（RuleAuraScope）非数值——数值/随机不显示；
            // 范围在仪典卡 actuating-range 行编辑（2026-10-09 设置盒拆除迁入）----
            if (mounts.Contains(MountKind.RuleAura))
            {
                valueNode?.gameObject.SetActive(false);
                sliderNode?.gameObject.SetActive(false);
                MakeHint(inject, "仪典无数值参数：作用范围（己方/双方/对方）在卡面「作用范围」行选择——双方档费用减半");
                return;
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
                        UiKit.Described(input, ValueFieldDesc);
                    }
                }
            }
            if (!showValue) MakeHint(inject, "无数值参数（该原子 Value 不参与语义）");

            // ---- 数值随机滑条（uGUI Slider 静态绑定；0-100% → RandomAmplitude；
            // 2026-10-07 黑名单翻转：含 {value} 默认可随机，标「不可随机」位的行禁随机）----
            bool showAmp = hasValue && !mounts.Contains(MountKind.NoRandom);
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
                        UiKit.Described(sl, AmpSliderDesc);
                    }
                }
            }

            // 原子级目标域下拉/目标随机已上移设置盒（2026-10-05 并列相同目标定案——作用范围升级效果级 TargetKinds）

            // 位 2：指示物持续提示（2026-10-08 层即持续定案：持续与计价均由指示物规格承载——
            //     计价=表行锚×max(1,层数)，效果级持续档不生效）
            if (mounts.Contains(MountKind.Counter))
                MakeHint(inject, "指示物原子：持续与计价均由指示物规格承载（表行锚×max(1,层数)，四分类见 Tags）——效果级持续档不生效");

            // 检索按维度档计费提示（2026-10-09 定案：宣言在对局中以文本输入——组合期不设输入口）
            if (type == AtomicEffectType.SearchDeck)
                MakeHint(inject, "检索按维度档计费：宣言在对局中以文本输入（组合期不填——空=单维度 1 档）");
        }

        // ======================================== 槽内分支编辑区（2026-10-05 两槽定案） ========================================

        /// <summary>槽内分支编辑区（2026-10-05 晚间定案：两流程；2026-10-09 互斥：仅第一槽渲染——
        /// RefreshSlots 控制，第二槽有原子即整区不出，第二槽自身永不渲染分支区）：
        /// ① 引擎主干行（位 7）——填入即自由分支：无 gate-row，参数行+Then 奖励槽直出
        ///   （引擎标识行 branch-engine 已退役 2026-10-09——费用与可实现费用上限上移顶栏费用预览）；
        /// ② 通常原子——单 gate-row（默认「无」）：族内产出条件 ∪ 局面门（有限分支），
        ///   选条件后建奖励槽；settle 按条件 id 推导（产出条件→Outcome / 局面门→Gate 纯条件奖励——
        ///   达标发奖，不达标不奖不惩，2026-10-09 定案）。
        /// gate-row 走 prefab 静态模板（content 直属 tpl-gate-row 经 CloneTemplateFrom 跨级克隆进槽，
        /// dd-trunk=条件 TMP 下拉）；缺模板 LogError 跳过本槽分支编辑（2026-10-07 兜底退役）。</summary>
        private void MakeBranchEditor(RectTransform slot, AtomicEffectEntry atom, int slotIndex)
        {
            var type = ResolveType(atom);
            var b = atom.branch;

            // ---- ① 引擎主干行：填入即自由分支（引擎身份=行身份，切引擎=换库行） ----
            var trunkRow = AtomicEffectTable.GetByHashId(atom.refId);
            if (ComposerCatalog.IsEngineTrunkRow(trunkRow))
            {
                var engineKind = ComposerCatalog.EngineKindOf(type);
                b ??= new BranchEntryData { settle = (int)BranchSettleKind.Engine };
                b.settle = (int)BranchSettleKind.Engine;
                if (engineKind != BranchEngineKind.None) b.engine = (int)engineKind;
                atom.branch = b;

                MakeHint(slot, $"{ComposerCatalog.EngineZhOf(engineKind)}门槛=Then 奖励锚价合计"
                        + "（随奖励实时推导，无需参数；奖励在 1-5 费区间内自由调整，门槛随计费变）"
                        + $"——见顶栏预览「自由分支·{ComposerCatalog.EngineZhOf(engineKind)}x」");

                MakeRewardSlot(slot, b, slotIndex);
                return;
            }

            // ---- ② 通常原子：单 gate-row（默认「无」） ----
            if (b != null && (BranchSettleKind)b.settle == BranchSettleKind.Engine)
            {
                atom.branch = null; // 引擎载荷只配引擎主干行（效果库空置期——旧形状静默归位）
                b = null;
            }

            var choices = GateRowChoices(type);
            int cur = 0; // 默认「无」
            if (b != null)
            {
                string curId = (BranchSettleKind)b.settle == BranchSettleKind.Outcome ? b.outcomeId : b.gateId;
                int found = choices.FindIndex(g => g.Id == curId);
                if (found > 0) cur = found;
            }

            // gate-row=prefab 静态模板（content 直属 tpl-gate-row 跨级克隆进槽）：label=「分支」名、
            // dd-trunk=条件 TMP 下拉（选项/取值经 UiKit.Dropdown(TMP) 统一进出）；缺失跳过（兜底退役）。
            var gateRow = UiKit.CloneTemplateFrom(_slotArea.Content, "tpl-gate-row", slot);
            var gateDd = gateRow != null
                ? UiKit.FindDeep(gateRow, "dd-trunk")?.GetComponent<TMP_Dropdown>() : null;
            if (gateRow == null || gateDd == null)
            {
                if (gateRow != null) UnityEngine.Object.Destroy(gateRow.gameObject); // 残缺克隆体弃用
                Debug.LogError("[EffectComposer] gate-row 模板缺失或缺 dd-trunk（TMP_Dropdown）——本槽分支编辑跳过；检查 EffectUI.prefab");
                return; // 兜底构建已退役（2026-10-07）——缺模板即无分支行
            }
            // 槽布局组控宽——LE 只补 flexibleWidth 铺满（不经 Size：其会把模板烘焙的 minHeight=40 重置掉）
            var gateLe = gateRow.GetComponent<LayoutElement>();
            if (gateLe == null) gateLe = gateRow.gameObject.AddComponent<LayoutElement>();
            gateLe.flexibleWidth = 1f;
            var gateLbl = UiKit.FindDeep(gateRow, "lbl-gate")?.GetComponentInChildren<TMP_Text>(); // 2026-10-07 预制体改名 label→lbl-gate
            if (gateLbl != null) gateLbl.text = "分支";

            var labels = choices.Select(g => g.Id == null ? "无" : ComposerCatalog.GateLabel(g)).ToList();
            void OnGateChanged(int idx, string _)
            {
                var pick = choices[Mathf.Clamp(idx, 0, choices.Count - 1)];
                if (string.IsNullOrEmpty(pick.Id))
                {
                    atom.branch = null; // 「无」=无分支
                }
                else
                {
                    var nb = atom.branch ?? new BranchEntryData();
                    nb.then ??= new List<AtomicEffectEntry>();
                    nb.gateId = null; nb.condParam = 0; nb.condStr = null;
                    nb.outcomeId = null; nb.engine = 0; nb.engineParam = 0;
                    if (ComposerCatalog.IsOutcomeCondition(pick.Id))
                    {
                        nb.settle = (int)BranchSettleKind.Outcome; // 产出条件=纯奖励
                        nb.outcomeId = pick.Id;
                    }
                    else
                    {
                        nb.settle = (int)BranchSettleKind.Gate; // 局面门=有限分支（达标奖励/不达标无事）
                        nb.gateId = pick.Id;
                    }
                    atom.branch = nb;
                }
                RefreshSlots(); // 奖励槽随条件建/撤
                RefreshRightPanels(); // 库过滤随条件预算刷新
            }
            var dd = new UiKit.Dropdown(gateDd, labels, cur, OnGateChanged);
            dd.Describe(
                "分支条件：给这个槽的效果加前置条件，条件成立才发下方奖励——点选项看各条件的玩法。",
                idx =>
                {
                    var pick = choices[Mathf.Clamp(idx, 0, choices.Count - 1)];
                    if (string.IsNullOrEmpty(pick.Id)) return "无：不加条件，效果直接结算。";
                    if (ComposerCatalog.IsOutcomeCondition(pick.Id))
                        return $"{pick.DisplayName}——产出条件：本槽效果真办成这件事时才发放下方奖励（延迟验证，奖励免费）。";
                    return $"{pick.DisplayName}——有限分支：当前局面满足条件时发放下方奖励；不满足则不发（不奖励也不惩罚，奖励免费）。";
                });

            if (!BranchEntryRules.IsPhantom(b)) // 幽灵分支（JsonUtility 物化 settle=0）不建奖励槽——与下拉「无」一致
                MakeRewardSlot(slot, b, slotIndex);
        }

        /// <summary>gate-row 条目集（两流程定案）：[无] + 族内产出条件 + 局面门全集（有限分支——
        /// 任意原子可挂，与产出无关）。首项恒「无」（Id=null → branch 置空）。</summary>
        private static readonly ComposerCatalog.GateSpec NoneGateSpec =
            new ComposerCatalog.GateSpec { Id = null, DisplayName = "无" };

        private static List<ComposerCatalog.GateSpec> GateRowChoices(AtomicEffectType trunk)
        {
            var list = new List<ComposerCatalog.GateSpec> { NoneGateSpec };
            list.AddRange(ComposerCatalog.OutcomeConditionsFor(trunk));
            list.AddRange(ComposerCatalog.SituationGates);
            return list;
        }

        /// <summary>gateId → 目录项（局面门）。</summary>
        private static ComposerCatalog.GateSpec GateSpecOf(string gateId)
            => ComposerCatalog.SituationGates.FirstOrDefault(g => g.Id == gateId);

        /// <summary>槽内 Then 奖励槽区（2026-10-06 预制体化：content 烘焙 reward-slot → tpl-reward-slot
        /// 跨级克隆进槽，视觉不再代码构建——slot-title 标题行 + head 摘要行 + editor 参数区）。
        /// 显隐定案（2026-10-06）：无分支不克隆（整区隐藏）；非自由主干（局面门/产出条件——预算恒 1/2/3）
        /// 奖励参数按预算自动生成不可控——editor 恒隐藏、head 摘要无展开箭头（点击=选中奖励槽）；
        /// 自由主干（引擎）奖励无上限引擎封顶 5——维持手控：head 点击展开 editor 编辑数值。</summary>
        private void MakeRewardSlot(RectTransform slot, BranchEntryData b, int slotIndex)
        {
            bool free = (BranchSettleKind)b.settle == BranchSettleKind.Engine;
            var reward = b.then?.FirstOrDefault();
            if (reward != null) AutoFitRewardParams(reward, b); // 非自由主干：参数随预算自动生成（落槽/换门/载入统一归一）

            var area = UiKit.CloneTemplateFrom(_slotArea.Content, "tpl-reward-slot", slot);
            var img = area != null ? area.GetComponent<Image>() : null;
            if (area == null || img == null)
            {
                if (area != null) UnityEngine.Object.Destroy(area.gameObject); // 残缺克隆体先弃——防空行残留
                Debug.LogError("[EffectComposer] 缺 tpl-reward-slot 模板或根缺背景 Image——本槽奖励区跳过；检查 EffectUI.prefab");
                return; // 兜底构建已退役（2026-10-07）
            }
            img.raycastTarget = true; // 整区可点（选中奖励槽）
            // 防御（2026-10-06）：克隆体结构子节点强制激活——不依赖 prefab 烘焙的 active 状态
            UiKit.FindDeep(area, "slot-title")?.gameObject.SetActive(true);
            UiKit.FindDeep(area, "head")?.gameObject.SetActive(true);

            var title = UiKit.FindDeep(area, "slot-title")?.GetComponent<TMP_Text>();
            if (title == null) Debug.LogError("[EffectComposer] tpl-reward-slot 缺 slot-title——检查 EffectUI.prefab");
            else
            {
                title.text = $"槽 {slotIndex + 1}·Then 奖励（单原子·预算内）";
                UiKit.Size(title, fw: 1f);
            }

            // ---- head：奖励摘要行（空槽=填入提示）----
            bool expanded = free && _expandedRewards.Contains(b);
            var head = UiKit.FindDeep(area, "head") as RectTransform;
            var text = head != null ? UiKit.FindDeep(head, "text")?.GetComponent<TMP_Text>() : null;
            if (head == null || text == null)
                Debug.LogError("[EffectComposer] tpl-reward-slot 缺 head/text 摘要行——检查 EffectUI.prefab");
            else UiKit.Size(text, fw: 1f);
            void RefreshText()
            {
                if (text != null)
                    text.text = reward != null
                        ? (free ? (expanded ? "▼ " : "▶ ") : "") + AtomText.RenderRewardAtomEntry(reward) // 「新的目标」口径（2026-10-08）：奖励结算=合法范围重选
                        : "选中后点击库中的奖励原子填入（推导费 ≤ 分支预算）";
                RefreshName();
                ValidateZones(); // 参数/预算变化原地复验
            }
            var delNode = head != null ? UiKit.FindDeep(head, "del") : null;
            if (delNode == null)
                Debug.LogError("[EffectComposer] tpl-reward-slot 缺 del 删除按钮——检查 EffectUI.prefab（兜底创建已退役）");
            else
            {
                BindCardButton(delNode, "删除", true, () =>
                {
                    BranchOfSlot(slotIndex)?.then?.Clear();
                    _expandedRewards.Remove(b);
                    RefreshSlots();
                    RefreshLibrary(); // 全局唯一（隐藏口径）：奖励删除后该原子回归库
                });
                delNode.gameObject.SetActive(reward != null);
            }
            if (head != null)
            {
                var headBtn = head.gameObject.GetComponent<Button>() ?? head.gameObject.AddComponent<Button>();
                headBtn.transition = Selectable.Transition.None;
                headBtn.targetGraphic = head.GetComponent<Image>();
                headBtn.onClick.RemoveAllListeners();
                headBtn.onClick.AddListener(() =>
                {
                    if (free) ToggleRewardExpand(b);                   // 自由主干=展开手控（多卡独立展开）
                    else SelectSlot(SelKind.Reward, slotIndex);        // 非自由主干=仅选中（参数自动生成，无可编辑）
                });
            }

            // 原子表行四参数 des + 每原子目标行/衍生物行（2026-10-09 下沉定案；持续行随层化退役）：
            // 仅自由主干（引擎）奖励手控参数——非自由主干参数随预算自动生成，行隐藏
            var rewardCfg = AtomicEffectTable.GetByHashId(reward?.refId);
            BindAtomDes(area, rewardCfg);
            if (free && reward != null)
            {
                // 费用减少指示物（AddCostDown）奖励数量钉 1（2026-10-09 定案，见 BindAtomTargetRow）
                BindAtomTargetRow(area, reward, RefreshText, pinCountOne: IsAddCostDownRow(rewardCfg));
                BindAtomDerivativeRow(area, reward);
            }
            else
            {
                UiKit.FindDeep(area, "placeholder")?.gameObject.SetActive(false);
                UiKit.FindDeep(area, "derivative")?.gameObject.SetActive(false);
            }

            // ---- editor：参数区（非自由主干恒隐藏——2026-10-06 参数不可控定案）----
            var editor = UiKit.FindDeep(area, "editor") as RectTransform;
            if (editor == null) Debug.LogError("[EffectComposer] tpl-reward-slot 缺 editor 参数区——检查 EffectUI.prefab");
            else editor.gameObject.SetActive(expanded);
            if (editor != null && expanded && reward != null)
            {
                var cfg = AtomicEffectTable.GetByHashId(reward.refId);
                bool hasValue = (cfg?.Description ?? "").Contains("{value}");
                var valueNode = UiKit.FindDeep(editor, "field-value");
                if (valueNode != null)
                {
                    valueNode.gameObject.SetActive(hasValue);
                    if (hasValue)
                    {
                        var input = valueNode.GetComponent<TMP_InputField>()
                            ?? valueNode.GetComponentInChildren<TMP_InputField>(true);
                        if (input != null)
                        {
                            input.contentType = TMP_InputField.ContentType.IntegerNumber;
                            input.text = reward.value.ToString();
                            input.onEndEdit.RemoveAllListeners();
                            input.onEndEdit.AddListener(_ =>
                            {
                                if (int.TryParse(input.text, out var v))
                                {
                                    reward.value = v;
                                    RefreshText();
                                }
                                else input.text = reward.value.ToString();
                            });
                            UiKit.Described(input, ValueFieldDesc);
                        }
                    }
                }
                if (!hasValue) MakeHint(editor, "该原子无可调参数（无数值模板）——行为由描述与目标域决定");
            }

            // 选中态高亮（2026-10-09：选中色改焦点蓝与原子槽一致（MarkSelected 同色）——只跟奖励槽选中走，选别处即褪色；编辑器展开不携带色）
            if (_selSlot == SelKind.Reward && _selRewardSlot == slotIndex)
            {
                img.enabled = true; // 防御：组件禁用态强制开启——选中色必可见
                img.color = new Color(70f / 255f, 110f / 255f, 170f / 255f, 0.32f);
            }

            // 整区点击=选中该奖励槽（head 有自己的按钮——其余区域冒泡到本区）
            var btn = area.gameObject.GetComponent<Button>();
            if (btn == null)
            {
                btn = area.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
            }
            btn.onClick.AddListener(() => SelectSlot(SelKind.Reward, slotIndex));

            // 校验：分支未设奖励（converter 折叠为无分支——条件空转）+ 可实现费用内。
            // 2026-10-09 定案：上限统一走 RewardFilterCap——自由分支（引擎）恒 5（自平衡统一后引擎预算全 -1；
            // 此前 -1 直通=「校验恒满足」根因）。超限=GuideFrame 红框脉冲（shader _Time 闪）+描述条文本+禁存。
            _slotZones.Add(MakeZone(area, () =>
            {
                if (b.then == null || b.then.Count == 0)
                    return $"槽 {slotIndex + 1} 分支未设奖励——条件空转（装载按无分支处理）：点本区选中后到右栏填入，或改回「无分支」";
                int cap = RewardFilterCap(b);
                float cost = RewardCost(b.then[0]);
                return cost > cap
                    ? $"槽 {slotIndex + 1} 奖励超出分支可实现费用（{cost:0.#}/{cap}）——"
                      + (free ? "调低数值或换原子" : "参数已按预算自动生成仍超出——换更便宜的原子")
                    : null;
            }));
            RefreshText();
        }

        /// <summary>非自由主干（局面门/产出条件，2026-10-06 定案）奖励参数自动生成——玩家不可控：
        /// 该类奖励预算恒 1/2/3，数值=推导费不超预算的最大整数（锚价随值线性单调，自 1 上探；零锚价行回落 1）、
        /// 随机幅度清零。渲染期统一归一（落槽/换门缩预算/载入旧数据三路同口）。
        /// 自由主干（引擎）预算可达 9/无上限——维持手控，不经此函数。</summary>
        private static void AutoFitRewardParams(AtomicEffectEntry reward, BranchEntryData b)
        {
            if (reward == null || b == null) return;
            if ((BranchSettleKind)b.settle == BranchSettleKind.Engine) return;
            int budget = BranchBudget(b);
            if (budget < 0) return;
            reward.amp = 0f; // 随机幅度=参数之一——自动生成恒 0
            var cfg = AtomicEffectTable.GetByHashId(reward.refId);
            if (cfg == null || !(cfg.Description ?? "").Contains("{value}")) return;
            reward.value = 1;
            if (RewardCost(reward) <= 0f) return; // 零锚价行——数值不参与预算，维持默认 1
            int best = 1;
            for (int v = 2; v <= 99; v++)
            {
                reward.value = v;
                if (RewardCost(reward) > budget) break;
                best = v;
            }
            reward.value = best;
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

        // ---- 分支载荷工具（2026-10-05 两槽定案） ----

        /// <summary>槽序 → 该槽原子的分支载荷（无原子/无载荷=null）。</summary>
        /// <summary>槽位分支已开（并列/分支互斥判定单源，2026-10-09 定案）：引擎主干行（填槽即自由分支
        /// ——载荷落槽期才挂，须按行身份判）或非幽灵 branch 载荷（gate 下拉非「无」）。</summary>
        private static bool StepBranchActive(EffectStepData step)
        {
            if (step?.kind != 0 || step.atomic == null) return false;
            if (ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(step.atomic.refId))) return true;
            return step.atomic.branch != null && !BranchEntryRules.IsPhantom(step.atomic.branch);
        }

        private BranchEntryData BranchOfSlot(int slotIndex)
        {
            var steps = _graph.steps ?? new List<EffectStepData>();
            if (slotIndex < 0 || slotIndex >= steps.Count) return null;
            var s = steps[slotIndex];
            return s?.kind == 0 ? s.atomic?.branch : null;
        }

        /// <summary>分支奖励预算（两槽定案）：Gate=门预算（GatePremium 同源）；Outcome=产出条件预算；
        /// Engine=引擎预算（2026-10-09 自平衡统一：全引擎 -1 无上限——门槛与预算同源于 Then 锚价推导）。</summary>
        private static int BranchBudget(BranchEntryData b)
        {
            if (b == null) return -1;
            switch ((BranchSettleKind)b.settle)
            {
                case BranchSettleKind.Gate:
                    var gate = GateSpecOf(b.gateId);
                    return gate != null ? ComposerCatalog.GateBudget(gate) : -1;
                case BranchSettleKind.Outcome:
                    var oc = ComposerCatalog.OutcomeConditions.FirstOrDefault(g => g.Id == b.outcomeId);
                    return oc != null ? ComposerCatalog.GateBudget(oc) : -1;
                case BranchSettleKind.Engine:
                    return ComposerCatalog.EngineRewardBudget((BranchEngineKind)b.engine, b.engineParam);
                default:
                    return -1;
            }
        }

        /// <summary>奖励可实现费用上限（2026-10-09 定案·四口共用）：有限分支=门预算（GatePremium 1/2；
        /// DrawnInStandbyThisTurn=3）；产出条件=条件预算（恒 2）；自由分支=引擎分支恒 5（自平衡统一后
        /// 引擎预算全 -1——玩家在 1-5 费区间自由调奖励、门槛随计费推导）
        /// ——库展示、落槽校验、槽区保存校验（超限禁存）与费用预览分支段共用一口。</summary>
        private static int RewardFilterCap(BranchEntryData b)
        {
            int budget = BranchBudget(b);
            return budget >= 0 ? budget : 5;
        }

        /// <summary>奖励原子是否过筛选上限（value=1 锚价口径=库行费用方格同源；转换失败=不过）。
        /// 2026-10-06 修正：推导价与表价（ManaList=行方格展示价）取大比对——检索行宣言名空时推导价
        /// 落单维度档 1 而表价 3（ExactCard 档），旧口径曾致 3 费行漏进 ≤2 的门预算筛选
        /// （库展示与落槽校验共用本口，一并修复）。</summary>
        private static bool RewardWithinFilterCap(AtomicEffectEntry entry, BranchEntryData b)
        {
            var inst = CardEffectConverter.ConvertAtomForUI(entry);
            if (inst == null) return false;
            float derived = CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { inst });
            float declared = AtomicEffectTable.GetByHashId(entry?.refId)?.TotalUnitCost ?? 0f;
            return Mathf.Max(derived, declared) <= RewardFilterCap(b);
        }

        /// <summary>原子引用 → 枚举（行缺失回退 DealDamage——门行兜底口径同旧）。</summary>
        private static AtomicEffectType ResolveType(AtomicEffectEntry a)
        {
            var row = AtomicEffectTable.GetByHashId(a?.refId);
            return row != null && Enum.TryParse<AtomicEffectType>(row.EnumName, out var t)
                ? t : AtomicEffectType.DealDamage;
        }

        /// <summary>槽内原子卡展开切换（2026-10-06 定案：多卡独立展开——点击其他卡不再收起已展开卡；
        /// 键=原子对象身份，换序/删除免下标维护，悬挂条目由 ClampSelection 渲染前剪除）。</summary>
        private void ToggleExpand(AtomicEffectEntry atom)
        {
            if (atom == null) return;
            if (!_expandedAtoms.Remove(atom)) _expandedAtoms.Add(atom);
            RefreshSlots();
        }

        /// <summary>槽内 Then 奖励 editor 展开切换（仅自由主干可展开；键=槽分支载荷）。</summary>
        private void ToggleRewardExpand(BranchEntryData b)
        {
            if (b == null) return;
            if (!_expandedRewards.Remove(b)) _expandedRewards.Add(b);
            RefreshSlots();
        }

        // ---- 并列槽操作 ----
        private void MoveParallel(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= _graph.steps.Count || target < 0 || target >= _graph.steps.Count) return;
            (_graph.steps[index], _graph.steps[target]) = (_graph.steps[target], _graph.steps[index]);
            // 展开键=对象身份——换序不动展开态（2026-10-06 多卡独立展开）
            if (_selSlot == SelKind.Parallel && _selSlotIndex == index) _selSlotIndex = target;
            else if (_selSlot == SelKind.Parallel && _selSlotIndex == target) _selSlotIndex = index;
            RefreshSlots();
        }

        private void RemoveAtom(SelKind kind, int index)
        {
            switch (kind)
            {
                case SelKind.Parallel:
                    _expandedAtoms.Remove(_graph.steps[index]?.atomic); // 悬挂条目 ClampSelection 兜底剪除
                    _graph.steps.RemoveAt(index);
                    break;
                case SelKind.Reward:
                    var br = BranchOfSlot(index);
                    br?.then?.Clear();
                    if (br != null) _expandedRewards.Remove(br);
                    break;
            }
            RefreshSlots();
            RefreshLibrary(); // 全局唯一（隐藏口径）：删除后原子回归库
        }

        // ---- 落槽资格与写入（点击替换选中槽） ----

        // 并列主干槽可落：主动位原子∪引擎主干行，且非错边（内容契约：效果区禁错边——错边只能进代价槽）
        private bool ParallelCanDrop(object payload)
            => payload is LibPayload lp && lp.CanBeTrunk && !lp.IsWrongSideOnly;

        // 槽内 Then 奖励可落：开放奖励挂载位+非错边+可实现费用上限内（两槽定案；2026-10-09 上限口径
        // ——产出条件=条件预算，自由分支=引擎预算·无上限封顶 5）；
        private bool RewardCanDrop(object payload, BranchEntryData b)
        {
            if (!(payload is LibPayload lp)) return false;
            if (!lp.CanBeBranchReward || lp.IsWrongSideOnly) return false;
            return RewardWithinFilterCap(lp.Entry(), b);
        }

        /// <summary>槽内 Then 奖励落槽（两槽定案）：写入该槽 branch.then（单奖励槽）。
        /// 2026-10-06 定案：非自由主干奖励参数按预算自动生成不可控——落槽不展开（所属原子展开关闭）；
        /// 自由主干（引擎）维持展开手控。参数归一在 MakeRewardSlot 渲染期 AutoFitRewardParams 统一执行。</summary>
        private void DropBranchReward(LibPayload lp, int slotIndex)
        {
            var b = BranchOfSlot(slotIndex);
            if (b == null) return;
            b.then = new List<AtomicEffectEntry> { EntryForEffectSlot(lp) };
            bool free = (BranchSettleKind)b.settle == BranchSettleKind.Engine;
            if (free) _expandedRewards.Add(b); // 自由主干=落槽展开手控；非自由主干不展开（参数自动生成）
            _selSlot = SelKind.Reward;
            _selRewardSlot = slotIndex;
            RefreshSlots();
        }

        // ---- 全局唯一规则（2026-10-09 定案·隐藏口径）+ 衍生物行 ----

        /// <summary>组合内已用原子 refId 集（主干槽+Then 奖励——与 DuplicateAtomError 同口径）。
        /// 库列表按此隐藏：用过即隐（不拒绝），换掉/删除后随 RefreshLibrary 回归。</summary>
        private HashSet<string> UsedAtomRefIds()
        {
            var set = new HashSet<string>();
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                if (!string.IsNullOrEmpty(s.atomic.refId)) set.Add(s.atomic.refId);
                foreach (var r in s.atomic.branch?.then ?? new List<AtomicEffectEntry>())
                    if (r != null && !string.IsNullOrEmpty(r.refId)) set.Add(r.refId);
            }
            return set;
        }

        /// <summary>校验区口径的全局唯一检查（null=合规）：库隐藏口径下正常组合不会产生重复
        /// ——本区仅拦载入的旧数据/旁路落位（报错时保存禁用，删其一即解）。</summary>
        private string DuplicateAtomError()
        {
            var seen = new HashSet<string>();
            string dupName = null;
            void Check(AtomicEffectEntry a)
            {
                if (a == null || !string.IsNullOrEmpty(dupName) || string.IsNullOrEmpty(a.refId)) return;
                if (!seen.Add(a.refId))
                    dupName = AtomicEffectTable.GetByHashId(a.refId)?.DisplayName ?? a.refId;
            }
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                Check(s.atomic);
                foreach (var r in s.atomic.branch?.then ?? new List<AtomicEffectEntry>()) Check(r);
            }
            return dupName == null ? null
                : $"重复效果「{dupName}」——全局规则：一个组合效果中同一效果只能出现一次（并列槽与奖励均计入）";
        }

        private static bool IsSummonTokenEntry(AtomicEffectEntry a)
            => AtomicEffectTable.GetByHashId(a?.refId)?.EnumName == nameof(AtomicEffectType.SummonToken);

        // ======================================== 每原子行与卡面 des（2026-10-09 下沉定案） ========================================

        /// <summary>原子表行四参数 des 文本绑定（2026-10-09 定案格式——卡面常显，槽/奖励/光环卡共用；
        /// 行缺失 cfg=null 显「—」）。</summary>
        private static void BindAtomDes(RectTransform host, AtomicEffectConfig cfg)
        {
            var des = UiKit.FindDeep(host, "des")?.GetComponent<TMP_Text>();
            if (des == null)
            {
                Debug.LogError("[EffectComposer] 卡面缺 des 参数文本——检查 EffectUI.prefab");
                return;
            }
            des.text = cfg != null ? AtomParamLine(cfg) : "—";
            UiKit.Size(des, fw: 1f);
        }

        /// <summary>原子表行四参数文本（2026-10-09 定案格式）：作用范围=TargetKinds 原文（逐原子自身域）、
        /// 筛选标签=TargetFilter 原文（域内属性筛选）、极性=±1/0、费用基准=ManaList 非零色文本化
        ///（保留小数锚）；空项显「无」。</summary>
        private static string AtomParamLine(AtomicEffectConfig cfg)
        {
            string scope = string.IsNullOrEmpty(cfg?.TargetKinds) ? "无" : cfg.TargetKinds;
            string filter = string.IsNullOrEmpty(cfg?.TargetFilter) ? "无" : cfg.TargetFilter;
            float p = UnityEngine.Mathf.Clamp(cfg?.Polarity ?? 0f, -1f, 1f);
            string polarity = p > 0.5f ? "+1" : p < -0.5f ? "−1" : "0";
            var cost = cfg?.ManaList;
            string mana = cost == null || cost.IsZero ? "无"
                : string.Join("", cost.NonzeroColors().Select(c => $"{cost[c]:0.#}{ColorZh(c)}"));
            return $"作用范围：{scope} 筛选标签：{filter} 极性：{polarity} 费用基准：{mana}";
        }

        /// <summary>每原子目标行（placeholder：dd-num 目标数量 1/2/3 + rand 随机目标 Toggle——2026-10-09
        /// 数量/随机下沉每原子）：写 atom.count/atom.rand（未声明=首档占位/显 off 不写回——回落效果级）；
        /// 零域原子（引擎主干/条件行）无目标可选，整行隐藏。槽卡与奖励卡共用。
        /// 2026-10-09 自由分支定案两钉：①隐蔽域强制随机——表行**原生** TargetKinds 含 对方手牌/对方牌库
        ///（隐蔽信息不可指定，随机必须开启不可改；己方牌库可自查不入列；指示物授予注入的 {1..8} 全域
        /// 不参与判定——单位指示物仍可手指定；已展示卡运行时经预选透传点名）；②pinCountOne——
        /// 费用减少指示物奖励数量钉 1（分支奖励计价=REWARD_SHIM TargetCount=1，数量本不改价——钉 1 使
        /// UI=计价=行为）。</summary>
        private void BindAtomTargetRow(RectTransform host, AtomicEffectEntry atom, Action refresh, bool pinCountOne = false)
        {
            var row = UiKit.FindDeep(host, "placeholder");
            if (row == null)
            {
                Debug.LogError("[EffectComposer] atom-card 缺 placeholder 行（目标数量/随机目标）——检查 EffectUI.prefab");
                return;
            }
            var cfg = AtomicEffectTable.GetByHashId(atom?.refId);
            bool hasDomain = (cfg?.GetTargetKindList()?.Count ?? 0) > 0;
            row.gameObject.SetActive(hasDomain && atom != null);
            if (!hasDomain || atom == null) return;

            var kindList = cfg.GetTargetKindList();
            bool hiddenDomain = kindList.Contains((int)TargetKind.EnemyHand) || kindList.Contains((int)TargetKind.EnemyDeck);
            if (hiddenDomain) atom.rand = 1;
            if (pinCountOne) atom.count = 1;

            var ddCount = BindTierDropdown(row, CountTiers, atom.count,
                v =>
                {
                    atom.count = v;
                    refresh?.Invoke(); // 数量影响计价/摘要——随改随刷
                },
                "目标数量：这个原子发动时选取几个目标——点选项看各档说明。",
                optionDesc: v => v switch
                {
                    1 => "发动时弹窗由你选 1 个目标（基础价）。",
                    2 => "弹窗由你选 2 个目标（费用 1.5 倍）。",
                    _ => "弹窗由你选 3 个目标（费用 2 倍）。",
                });
            if (pinCountOne && ddCount != null) ddCount.interactable = false; // 费用减少奖励数量恒 1

            var tgNode = UiKit.FindDeep(row, "rand");
            var tg = tgNode != null
                ? tgNode.GetComponent<Toggle>() ?? tgNode.GetComponentInChildren<Toggle>(true)
                : null;
            if (tg == null) Debug.LogError("[EffectComposer] placeholder 行缺 rand Toggle——随机目标不可编；检查 EffectUI.prefab");
            else
            {
                tg.SetIsOnWithoutNotify(atom.rand == 1);
                tg.onValueChanged.RemoveAllListeners();
                if (hiddenDomain) tg.interactable = false; // 隐蔽域：随机必须开启，不可修改
                else tg.onValueChanged.AddListener(on =>
                {
                    atom.rand = on ? 1 : 0; // 随机=正交标志——不弹窗从完整候选域抽取（潜行/扰魔挡不住）
                    refresh?.Invoke();
                });
                UiKit.Described(tg,
                    "目标随机：开启后这个原子不再弹窗选目标，改为在其作用范围内随机抽取（潜行、扰魔挡不住随机）。");
            }
        }

        /// <summary>每原子衍生物行（derivative：InputField=模板卡 ID str、num=召唤数量 1-5 value）——
        /// 仅召唤衍生物（SummonToken）原子显示（2026-10-09 迁自设置盒——原全局扫描 FindSummonTokenAtom 退役）。
        /// 槽卡与奖励卡共用。</summary>
        private void BindAtomDerivativeRow(RectTransform host, AtomicEffectEntry atom)
        {
            var row = UiKit.FindDeep(host, "derivative");
            if (row == null)
            {
                Debug.LogError("[EffectComposer] atom-card 缺 derivative 行（衍生物ID/召唤数量）——检查 EffectUI.prefab");
                return;
            }
            bool isSummon = IsSummonTokenEntry(atom);
            row.gameObject.SetActive(isSummon);
            if (!isSummon) return;

            var idField = UiKit.FindDeep(row, "InputField")?.GetComponentInChildren<TMP_InputField>(true);
            if (idField == null) Debug.LogError("[EffectComposer] derivative 行缺 InputField——召唤ID不可编；检查 EffectUI.prefab");
            else
            {
                idField.SetTextWithoutNotify(atom.str ?? "");
                idField.onEndEdit.RemoveAllListeners();
                idField.onEndEdit.AddListener(_ =>
                {
                    atom.str = idField.text.Trim();
                    RefreshSlots(); // 摘要（{衍生物}→模板卡名）与装载校验随写刷新
                });
                UiKit.Described(idField,
                    "衍生物模板 ID：指向一张真实生物卡——实例=该卡全参数复制、恒落战场（白板模板如 "
                    + "token_0_1/token_1_1/token_2_2/token_3_3）。填不存在的 ID 装载时构筑期拦截。");
            }

            var numDd = UiKit.FindDeep(row, "num")?.GetComponent<TMP_Dropdown>();
            if (numDd == null) Debug.LogError("[EffectComposer] derivative 行缺 num 下拉——召唤数量不可编；检查 EffectUI.prefab");
            else
            {
                var tiers = new List<int> { 1, 2, 3, 4, 5 };
                numDd.ClearOptions();
                numDd.AddOptions(tiers.Select(v => v.ToString()).ToList());
                numDd.SetValueWithoutNotify(Mathf.Clamp(tiers.IndexOf(Mathf.Max(1, atom.value)), 0, tiers.Count - 1));
                numDd.RefreshShownValue();
                numDd.onValueChanged.RemoveAllListeners();
                numDd.onValueChanged.AddListener(i =>
                {
                    atom.value = tiers[Mathf.Clamp(i, 0, tiers.Count - 1)];
                    RefreshSlots();
                });
                UiKit.Described(numDd, "召唤数量：一次召唤几个衍生物——每个都是独立实例，战场满位时后续入墓。");
            }
        }

        /// <summary>极性→合法作用面方向码（2026-10-07 门控口径）：+1→{己方,双方}；-1→{对方,双方}；
        /// 0→三选。仪典设置盒与条目卡 actuating-range 下拉共用。</summary>
        private static List<int> AllowedScopesOfPolarity(float p)
            => p >= 0.5f ? new List<int> { 1, 2 } : p <= -0.5f ? new List<int> { 2, 3 } : new List<int> { 1, 2, 3 };

        /// <summary>档位下拉静态绑定（两段式描述）：desc=头部总述；optionDesc=选中项介绍
        ///（index→文本，null/空＝保持总述）。标签由档位值生成（默认 -1/0=「全部」；速度档传 labelOf
        /// 直显数字——0=普通档不是"全部"）；现值不在档内=首档占位显示不写回（存量兼容——改选才落值，
        /// 同发动方式的系统强制档占位策略）。</summary>
        private static TMP_Dropdown BindTierDropdown(RectTransform node, int[] tiers, int current,
            Action<int> write, string desc, Func<int, string> labelOf = null, Func<int, string> optionDesc = null)
        {
            var dd = DdOf(node);
            if (dd == null) return null;
            labelOf ??= v => v == -1 || v == 0 ? "全部" : v.ToString();
            dd.ClearOptions();
            dd.AddOptions(tiers.Select(labelOf).ToList());
            dd.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(tiers, current)));
            dd.RefreshShownValue();
            dd.onValueChanged.RemoveAllListeners();
            dd.onValueChanged.AddListener(i => write(tiers[Mathf.Clamp(i, 0, tiers.Length - 1)]));
            UiKit.DescribedOptions(dd, desc, optionDesc != null
                ? i => optionDesc(tiers[Mathf.Clamp(i, 0, tiers.Length - 1)]) : null);
            return dd;
        }

        /// <summary>时机是否必然一回合至多触发一次（2026-10-05 晚间定案）：
        /// 回合开始/结束（每回合各一发）、游戏开始/角色死亡（每局一发）——结构上不可能同回合二次触发。
        /// 阶段开始/结束（一回合多阶段可多发）、事件族（抽牌/伤害/攻击/宣言等可重复）、
        /// 自我换区族（回手/复活环可再造）均不锁。时机计价系数维持 1（不接计价——同日定案）。</summary>
        private static bool TimingFiresOncePerTurn(TriggerTiming t)
            => t == TriggerTiming.OnTurnStart || t == TriggerTiming.OnTurnEnd
               || t == TriggerTiming.OnGameStart || t == TriggerTiming.OnRoleDeath;

        /// <summary>必然单发时机的作用次数锁：值钉 1（计价 1×——更高档对必然单发是纯付费无收益）、下拉禁用。
        /// 消费点：activate-bar 绑定后（SyncActivateBarValues）、时机下拉变更、发动方式钉时机后（SyncActivationVisibility）。</summary>
        private void ApplyOnceTimingLimitLock()
        {
            var h = _graph.header;
            // 引擎锁定态豁免（2026-10-09 自由分支九项定案）：倒计时/运势=回合开始但无限循环
            //（倒计时归零重挂/运势每回合独立判定）——次数钉引擎预设「无限」，不锁 1
            bool engineLocked = EngineKindOfGraph() != null;
            bool once = TimingFiresOncePerTurn((TriggerTiming)h.TriggerTiming) && !engineLocked;
            if (once && h.TriggerLimitPerTurn != 1)
            {
                h.TriggerLimitPerTurn = 1;
                RefreshName(); // 计价回落 1×——费用预览随改随刷
            }
            if (_ddLimit == null) return;
            _ddLimit.interactable = !once && !engineLocked; // 必然单发/引擎锁定均不可改（引擎态由 ApplyEngineHeaderLock 统一禁用）
            if (once)
            {
                _ddLimit.SetValueWithoutNotify(0); // 首档=1
                _ddLimit.RefreshShownValue();
            }
        }

        /// <summary>当前效果引擎种类（首槽原子 branch 载荷∪行身份——与 StepBranchActive 同口径；null=无引擎）。</summary>
        private BranchEngineKind? EngineKindOfGraph()
        {
            var atom = _graph?.steps?.FirstOrDefault(s => s?.kind == 0 && s.atomic != null)?.atomic;
            if (atom == null) return null;
            if (atom.branch != null && (BranchSettleKind)atom.branch.settle == BranchSettleKind.Engine
                && Enum.IsDefined(typeof(BranchEngineKind), atom.branch.engine))
                return (BranchEngineKind)atom.branch.engine;
            var cfg = AtomicEffectTable.GetByHashId(atom.refId); // 行身份兜底（引擎行落槽即挂载荷——双保险）
            if (cfg != null && Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t))
            {
                var k = ComposerCatalog.EngineKindOf(t);
                if (k != BranchEngineKind.None) return k;
            }
            return null;
        }

        /// <summary>引擎效果头锁定（2026-10-09 自由分支九项定案）：首槽引擎在案 → 效果头四值
        ///（发动方式/时机/速度/生效次数）钉 ComposerCatalog.EngineHeaderPresetOf，四控件禁用；
        /// 引擎移除即恢复可编辑（值保留供用户改）。引擎主干零计价——limit=-1 档无费用影响。
        /// 消费点：RefreshSlots 尾部（覆盖引擎落槽/换引擎/载入三路径）。</summary>
        private void ApplyEngineHeaderLock()
        {
            var h = _graph?.header;
            if (h == null) return;
            var preset = EngineKindOfGraph() is { } kind ? ComposerCatalog.EngineHeaderPresetOf(kind) : null;
            bool locked = preset != null;

            if (locked)
            {
                bool voluntary = preset.Value.Activation == EffectActivationType.Voluntary;
                h.ActivationType = (int)preset.Value.Activation;
                h.TriggerTiming = (int)(voluntary ? TriggerTiming.Activate_Active : preset.Value.Timing);
                h.BaseSpeed = preset.Value.Speed;
                h.TriggerLimitPerTurn = preset.Value.Limit;
            }

            // 值对齐先行（同步链内含必然单发锁——引擎态已在其中豁免），禁用态最后落——防同步链覆写锁定
            _activationDropdown?.SetIndex(ActivationIndex(h.ActivationType));
            SyncActivationVisibility();
            SyncTimingDropdown();
            SyncActivateBarValues();

            if (_activationDropdown != null) _activationDropdown.Interactable = !locked;
            if (_timingDropdown != null) _timingDropdown.Interactable = !locked;
            if (_ddSpeed != null) _ddSpeed.interactable = !locked;
            if (locked) _ddLimit.interactable = false;
            else ApplyOnceTimingLimitLock(); // 引擎移除——必然单发锁恢复对次数下拉的管辖
        }

        /// <summary>费用减少指示物（AddCostDown）行判定——奖励槽数量钉 1 定案配套（BindAtomTargetRow）。</summary>
        private static bool IsAddCostDownRow(AtomicEffectConfig cfg)
            => cfg != null && Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)
               && t == AtomicEffectType.AddCostDown;

        /// <summary>prefab 静态下拉取件（节点在而组件缺=数据层可见错误，不静默）。</summary>
        private static TMP_Dropdown DdOf(RectTransform node)
        {
            var dd = node?.GetComponent<TMP_Dropdown>() ?? node?.GetComponentInChildren<TMP_Dropdown>(true);
            if (node != null && dd == null)
                Debug.LogError($"[EffectComposer] 节点 {node.name} 缺 TMP_Dropdown 组件（2026-10-05 prefab 静态化）");
            return dd;
        }

        // ======================================== 右：双模式展示区 ========================================

        /// <summary>表切换（合并按钮）：原子库↔效果表来回切；切回原子库时重读表（承接原 btn-reload-atoms 语义）
        /// 并失效身份缓存——不然指纹/下标仍按旧表计算。</summary>
        private void SwitchTable()
        {
            if (_rightMode == RightMode.Atoms)
            {
                SetRightMode(RightMode.Effects);
                return;
            }
            AtomicEffectTable.Reload();
            CardIdentityService.InvalidateTableCache();
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

                MakeListRow(_effectsList.Content,
                    AtomText.RenderEffectSummary(captured), // 完整效果描述（2026-10-03 定案：不再简写）
                    DotColor(ColorFilter.OfTagsCsv(parts.FirstOrDefault()?.Tags)),
                    captured.header?.AnchorCost,
                    () => LoadEffectFromLibrary(captured));
            }
        }

        /// <summary>点击效果表条目 → 载入左侧编辑（保存覆盖原名，自动改名清旧档）。
        /// 载入即归一化（2026-10-05 两槽定案）：遗留 kind=1 门步骤折入槽级 branch 载荷——UI 恒载荷形态。</summary>
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
            var note = NormalizeLoadedGraph(_graph);

            _mode = InferMode(_graph);
            _expandedAtoms.Clear();
            _expandedRewards.Clear();
            EnsureDefaultSelection();
            SyncTimingDropdown();
            _activationDropdown?.SetIndex(ActivationIndex(_graph.header.ActivationType));
            SyncActivationVisibility();
            RefreshAll();
            ShowToast(note != null
                ? $"已载入：{_graph.name}（编辑后保存覆盖）——{note}"
                : $"已载入：{_graph.name}（编辑后保存覆盖）");
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
            _filterTypeDropdown?.SetOptions(types, 0);
        }

        /// <summary>OnEnter 里挂分类回调（Build 之后一次）。</summary>
        private void BuildFilterCallbacks()
        {
            if (_filterTypeDropdown != null)
                _filterTypeDropdown.Changed += (idx, _) =>
            {
                _filterTypeZh = idx <= 0 ? null : _filterTypeChoices[Mathf.Max(0, idx)];
                RefreshLibrary();
                if (_rightMode == RightMode.Effects) RefreshEffectsList();
            };
        }

        /// <summary>效果分类名（效果表模式）：库内全部效果的内含原子中文名并集（分支 then 奖励计入）。</summary>
        private static IEnumerable<string> EffectCategoryNames()
        {
            var names = new HashSet<string>();
            foreach (var fx in EffectLibrarySerializer.LoadAll())
                foreach (var cfg in EffectAtomCfgs(fx))
                    if (!string.IsNullOrEmpty(cfg.DisplayName)) names.Add(cfg.DisplayName);
            return names.OrderBy(n => n, StringComparer.CurrentCulture);
        }

        /// <summary>效果的组成部分表行（分类/色点/简称检索共用）：各步骤原子 + 槽级分支 then 奖励
        /// + 遗留 kind=1 门奖励（两槽定案：引擎主干行已删，条件不再占表行）。</summary>
        private static List<AtomicEffectConfig> EffectAtomCfgs(EffectGraphData fx)
        {
            var list = new List<AtomicEffectConfig>();
            if (fx?.header == null) return list;
            if (fx.steps != null)
            {
                foreach (var s in fx.steps)
                {
                    if (s == null) continue;
                    if (s.kind == 0)
                    {
                        AddRefRow(list, s.atomic);
                        if (s.atomic?.branch?.then != null)
                            foreach (var a in s.atomic.branch.then) AddRefRow(list, a);
                    }
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
        /// 附带中文上下文（库顶标签显示）。主干槽=派生主干资格+非错边+域非空（引擎主干行仅第一槽——互斥）；
        /// 奖励槽（SelKind.Reward）=BranchReward 位+非错边+分支预算内。</summary>
        private Func<LibPayload, bool> CurrentSlotPredicate(out string contextZh)
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                {
                    if (_selSlot == SelKind.Reward)
                    {
                        // 槽内 Then 奖励槽选中：按 BranchReward 位+可实现费用上限过滤（2026-10-09 自平衡统一口径：
                        // 产出条件=条件预算（恒 2）；自由分支（引擎）恒按上限 5 过滤——玩家在 1-5 费区间
                        // 自由调奖励，门槛随计费推导、超 5 红闪禁存；不再随参数缩过滤面）
                        var b = BranchOfSlot(_selRewardSlot);
                        int budget = BranchBudget(b);
                        int cap = RewardFilterCap(b);
                        contextZh = budget > 0
                            ? $"槽 {_selRewardSlot + 1} 分支奖励（非错边——仅示 ≤{cap} 费原子）"
                            : $"槽 {_selRewardSlot + 1} 分支奖励（非错边——仅示 0-{cap} 费原子）";
                        return lp => lp.CanBeBranchReward && !lp.IsWrongSideOnly
                                     && RewardWithinFilterCap(lp.Entry(), b);
                    }
                    // 逐原子目标制（2026-10-09）：不做槽间域交集过滤——各原子按自身表域独立解析；
                    // 仅保留行自身域非空（零域行除引擎主干/载荷外无目标可解析，不可入主干槽）。
                    // 并列/分支互斥（同日定案）：引擎主干行=分支载体只进第一槽——第二槽纯并列原子
                    bool second = _selSlotIndex == 1;
                    contextZh = second
                        ? "并列槽 2（主动原子·非错边——与分支互斥，不放引擎主干）"
                        : "并列槽 1（主动原子/引擎主干·非错边）";
                    return lp => lp.CanBeTrunk && !lp.IsWrongSideOnly
                                 && (!second || !lp.IsEngineTrunk)
                                 && (lp.IsEngineTrunk || (lp.Cfg?.GetTargetKindList()?.Count ?? 0) > 0);
                }
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

            var pred = CurrentSlotPredicate(out _); // 过滤缘由说明行 lbl-lib-context 已移除（2026-10-07 定案）
            // 全局唯一（2026-10-09 隐藏口径）：已入组合（主干/奖励）的原子从库中隐藏——用过即隐不拒绝，
            // 换掉/删除后随本刷新回归；与分类/搜索/槽位可用性取交集
            var usedRefIds = UsedAtomRefIds();
            foreach (var row in AllTableRows())
            {
                if (!Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) continue;
                // 锁定关键词（攻击/守卫 + 系统内部行）直接隐藏——不可组合成效果
                if (IsLockedKeywordRow(row)) continue;
                if (usedRefIds.Contains(row.HashId)) continue;
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
            MakeListRow(_libraryList.Content, payload.Cfg.Description, // 2026-10-08 定案：原子行不显示名字——描述独占 meta 单列
                DotColor(ColorFilter.OfTagsCsv(payload.Cfg.Tags)), AtomCosts(payload.Cfg), () => QuickAdd(payload));

        /// <summary>原子单价 → 方格明细（费用构成各色份额取整；序数序=枚举序；null=不计价行）。</summary>
        private static ElementCost AtomCosts(AtomicEffectConfig cfg)
        {
            var cost = cfg?.ManaList;
            if (cost == null || cost.IsZero) return null;
            return cost;
        }

        /// <summary>右栏列表行（原子库/效果表/光环库三处共用，2026-10-03 模板约定；2026-10-04 费用=位置数组）：
        /// content 直属不激活模板「row」克隆填充（2026-10-07 单名定案——tpl-row 别名与代码建行退役；
        /// 2026-10-08 定案：行不显示名字——文本只有 meta 单列，name 节点绑定退役；行内费用=模板换入的
        /// CostSquares 实例（按组件找），无实例回落 dot 色点染色；行点击 Button），布局样式全以模板为准；
        /// 缺模板 LogError 返回 null。</summary>
        private RectTransform MakeListRow(RectTransform content, string metaText,
            Color dot, ElementCost costs, Action onClick)
        {
            var tpl = UiKit.CloneTemplate("row", content);
            if (tpl == null)
            {
                Debug.LogError("[EffectComposer] 列表缺 row 模板（content 直属、不激活）——本行跳过；检查 EffectUI.prefab");
                return null;
            }

            FillRowParts(tpl, metaText);
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

        /// <summary>模板行文本填充（2026-10-08 定案）：行不显示名字——唯一文本列=meta 节点
        ///（name 节点绑定退役，不再按位置序兜底）；缺节点 LogError。</summary>
        private static void FillRowParts(RectTransform row, string metaText)
        {
            var metaT = UiKit.FindDeep(row, "meta")?.GetComponentInChildren<TMP_Text>(true);
            if (metaT == null)
            {
                Debug.LogError("[EffectComposer] row 模板缺 meta 文本节点——本行文本丢失；检查 EffectUI.prefab");
                return;
            }
            metaT.text = metaText ?? "";
        }

        private static void SetPartColor(RectTransform row, string child, Color color)
        {
            var img = UiKit.FindDeep(row, child)?.GetComponent<Image>();
            if (img != null) img.color = color;
        }

        /// <summary>右栏·光环条目库（点击换选）：属性光环（位 3 表驱动：设置攻击力/生命力系统行）
        /// + 关键词光环（CanMountAsAura——默认可−消耗型拉黑）。2026-10-09 单槽定案：光环槽唯一选择
        /// ——库点击即整组替换上一个选中（规则↔条目互斥，不再追加叠加）；近似搜索按显示名过滤。</summary>
        private void RefreshAuraLibrary()
        {
            void AddRow(string title, string meta, UIColor color, Action onAdd)
            {
                if (!string.IsNullOrEmpty(_filterName.text)
                    && !title.Contains(_filterName.text, StringComparison.OrdinalIgnoreCase)
                    && !meta.Contains(_filterName.text, StringComparison.OrdinalIgnoreCase)) return;
                MakeListRow(_libraryList.Content, meta, DotColor(color), null, onAdd);
            }

            void AddAura(LinkAuraData entry)
            {
                // 2026-10-09 单槽定案：条目整组替换（规则原子随选清空——库点击即替代上一个选中）
                _graph.steps?.Clear();
                _graph.header.LinkAuras = new List<LinkAuraData> { entry };
                RefreshSlots(); // 左栏槽卡/校验/费用随选择刷新（右栏行保持——可连点换选）
            }

            // 规则级光环：落 steps 为 ModifyGameRule 登场原子（str=规则短名）——引擎 RuleAuraSystem
            // 多槽（2026-10-07 唯一性改版：异名共存、同名在场禁打出）；作用范围=entry.value
            //（RuleAuraScope：缺省极性驱动——负面族(负极性)缺省对方、其余缺省双方，设置盒可改·极性门控）；
            // 不占 LinkAura 连接位（无箭头）。2026-10-09 单槽定案：steps 整组重建+连接条目随选清空
            // ——替代上一个选中（残留多行规则原子一并清，不再走 ReplaceParallel 追加语义）
            void AddRuleAura(AtomicEffectConfig cfg)
            {
                _graph.steps = new List<EffectStepData> { new EffectStepData { kind = 0,
                    atomic = new AtomicEffectEntry { refId = cfg.HashId,
                        value = cfg.Polarity <= -0.5f ? (int)RuleAuraScope.Opponent : (int)RuleAuraScope.Both,
                        str = RuleAuraIds.TryGetValue(cfg.DisplayName, out var r) ? r : cfg.DisplayName } } };
                _graph.header.LinkAuras = null;
                _graph.header.ArrowDirections = 0; // 规则级无箭头（MakeAuraPanel hasRule 分支同置——落位即写一致）
                RefreshSlots();
            }

            // 属性光环（2026-10-07 深夜改源；2026-10-08 库行细分定案）：一般效果「属性增加/属性减少」
            //（原子表位 3 行）的光环化——每源行拆 攻/生/属性 三行（卡面种类下拉已随 actuating-range
            // 改版退役，种类=库行结构即语义）；计价按符号取源行（白1/黑1）走通用光环化公式。
            foreach (var r in AllTableRows())
            {
                var cfg = r;
                if (IsRuleAuraRow(cfg) || !ComposerCatalog.HasMountBit(cfg, MountKind.LinkAura)) continue;
                if (!Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var st)) continue;
                int sign; UIColor color;
                switch (st)
                {
                    case AtomicEffectType.AddPlusOne: sign = 1; color = UIColor.White; break;
                    case AtomicEffectType.AddMinusOne: sign = -1; color = UIColor.Black; break;
                    default: continue; // 位 3 非属性行不进光环面
                }
                foreach (var (statZh, statId) in new[]
                     { ("攻击力", "Power"), ("生命值", "Life"), ("属性", "Both") })
                {
                    string zh = statZh, sid = statId;
                    AddRow($"属性光环·{zh}{(sign > 0 ? "+" : "−")}1",
                        $"{zh}{(sign > 0 ? "+" : "−")}1——源行光环化计价；作用面在条目卡选（含角色=5 单位档），属性只作用于生物",
                        color, () => AddAura(new LinkAuraData { stat = sid, value = sign }));
                }
            }
            foreach (var c in ComposerCatalog.AuraKeywordChoices())
            {
                var captured = c;
                AddRow($"关键词光环·{captured.label}",
                    captured.desc,
                    ColorFilter.OfKeyword(captured.id),
                    () => AddAura(new LinkAuraData { keyword = captured.id }));
            }
            foreach (var r in AllTableRows())
            {
                var rule = r;
                if (!IsRuleAuraRow(rule)) continue;
                AddRow($"规则光环·{rule.DisplayName}", $"{rule.Description}——{RuleAuraScopeOf(rule)}，同名在场时不可再打出（异名可共存），载体离场/被无效即失效；无箭头",
                    ColorFilter.OfTagsCsv(rule.Tags),
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
                    if (_selSlot == SelKind.Reward)
                    {
                        // 槽内 Then 奖励槽选中（两槽定案）：落 branch.then——挂载位+可逆转+分支预算校验
                        var b = BranchOfSlot(_selRewardSlot);
                        if (b == null) { ShowToast("该槽未启用分支——先在槽内 gate-row 选择条件"); break; }
                        if (!RewardCanDrop(payload, b)) { ShowToast("选中槽=奖励——挂载位不合或超出条件预算"); break; }
                        if (payload.IsKeywordAtom) ApplyKeywordHeader(); // 关键词=他人赋予形态
                        DropBranchReward(payload, _selRewardSlot);
                    }
                    else
                    {
                        if (!ParallelCanDrop(payload)) { ShowToast("该原子不可作主干（规则光环/仅连接光环节点不入效果栏）"); break; }
                        // 互斥兜底：引擎主干=分支载体只进第一槽（库已按槽过滤——拦旁路与旧选中态）
                        if (_selSlotIndex == 1 && payload.IsEngineTrunk)
                        { ShowToast("引擎主干是分支载体——只进原子槽 1（与第二槽互斥）"); break; }
                        if (payload.IsKeywordAtom) ApplyKeywordHeader();
                        ReplaceParallel(InferredTrunkEntry(payload));
                    }
                    RefreshLibrary(); // 全局唯一（2026-10-09 隐藏口径）：落位原子从库中隐藏——库随落位刷新
                    break;
            }
        }

        /// <summary>库行 → 效果槽条目：关键词/指示物类统一为赋予形态
        ///（kinds 按 GrantTargetKinds 推导——关键词=双方生物、指示物=任意卡 {1..8}；
        /// 关键词另带 str=运行时关键词 id），其余原样。</summary>
        private static AtomicEffectEntry EntryForEffectSlot(LibPayload payload)
        {
            var entry = payload.Entry();
            if (!payload.IsKeywordAtom && !payload.IsCounterAtom) return entry;
            entry.kinds = GrantTargetKinds(payload.Cfg);
            if (!payload.IsKeywordAtom) return entry; // 指示物：持续由 CounterSpec 承载，无关键词 id
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

        /// <summary>主干落槽（2026-10-05 晚间定案）：引擎主干行→自动建引擎载荷（settle=3；派生门槛引擎
        /// 门槛=Then 锚价推导、参数恒 0，倒计时 0=按奖励推导费换算，运势/附加诅咒·祝福按区间下限）；
        /// 通常原子→无分支（有限分支默认无门槛——条件由槽内 gate-row 手选）。关键词形态沿用。</summary>
        private static AtomicEffectEntry InferredTrunkEntry(LibPayload payload)
        {
            var entry = EntryForEffectSlot(payload);
            var engineKind = ComposerCatalog.EngineKindOf(payload.Type);
            if (engineKind != BranchEngineKind.None)
            {
                int param;
                if (ComposerCatalog.IsDerivedThresholdEngine(engineKind) || engineKind == BranchEngineKind.Countdown)
                    param = 0; // 派生门槛（拼点/死亡计数/元素充盈/手牌序位）参数=死数据；倒计时 0=自动换算
                else
                {
                    ComposerCatalog.EngineParamRange(engineKind, out var mn, out var mx);
                    param = Mathf.Clamp(1, mn, mx); // 运势点数线/附加诅咒·祝福张数：区间下限起
                }
                entry.branch = new BranchEntryData
                {
                    settle = (int)BranchSettleKind.Engine,
                    engine = (int)engineKind,
                    engineParam = param,
                };
            }
            return entry;
        }

        /// <summary>替换选中并列槽（2026-10-05 操作规范定案：库点击只替换选中位置）：
        /// 空槽=追加、满槽=原地换原子/整步——落位后选中停在原槽，**不自动前进**（往下填=手点下一槽）。</summary>
        private void ReplaceParallel(AtomicEffectEntry entry)
        {
            _graph.steps ??= new List<EffectStepData>();
            int s = Mathf.Clamp(_selSlotIndex, 0, ParallelSlotCount - 1);
            bool wasEmpty = s >= _graph.steps.Count;
            if (wasEmpty)
                _graph.steps.Add(new EffectStepData { kind = 0, atomic = entry });
            else if (_graph.steps[s].kind == 0)
                _graph.steps[s].atomic = entry;
            else
                _graph.steps[s] = new EffectStepData { kind = 0, atomic = entry }; // 只读步（抉择等）整步替换
            _expandedAtoms.Add(entry); // 落槽即展开新卡（多卡独立展开——旧卡不收起）
            _selSlotIndex = s; // 不自动前进——要填下一槽先手点该槽（规范：右侧点击只替换左侧选中位）
            RefreshSlots();
        }

        /// <summary>关键词授予惯例（他人形态）：Single + 持续到自己回合结束（1/2 回合同档）。</summary>
        private void ApplyKeywordHeader()
        {
            _graph.header.SelectionMode = (int)CardCore.SelectionMode.Single;
            _graph.header.Duration = (int)DurationType.UntilEndOfTurn;
            _graph.header.RandomTarget = 0; // 赋予目标由弹窗选一——不做随机
        }

        /// <summary>授予目标域（2026-10-07 仅可转换定案）：关键词行（位 0）→ 双方生物 {1,2}；
        /// 指示物行（位 1）→ 任意卡 {1..8}（双方单位/手牌/牌库——存储态"自己"的赋予形态域）；
        /// 其余 → null=表行默认域。</summary>
        private static List<int> GrantTargetKinds(AtomicEffectConfig cfg)
        {
            if (ComposerCatalog.HasMountBit(cfg, MountKind.Keyword))
                return new List<int> { (int)TargetKind.OwnLivingUnit, (int)TargetKind.EnemyLivingUnit };
            if (ComposerCatalog.HasMountBit(cfg, MountKind.Counter))
                return new List<int>
                {
                    (int)TargetKind.OwnLivingUnit, (int)TargetKind.EnemyLivingUnit,
                    (int)TargetKind.OwnNonLivingUnit, (int)TargetKind.EnemyNonLivingUnit,
                    (int)TargetKind.OwnHand, (int)TargetKind.EnemyHand,
                    (int)TargetKind.OwnDeck, (int)TargetKind.EnemyDeck,
                };
            return null;
        }

        // ======================================== 光环模式（箭头+条目随效果合成，挂卡并集） ========================================

        // 箭头计数标签（就地刷新防输入丢焦；光环条目费用由顶栏 AuraTopCostText 同源独占——面板行已退役）
        private TMP_Text _arrowCountLabel;

        /// <summary>光环编辑面板（2026-10-06 箭头预制体化+槽共用）：普通光环=prefab 烘焙 arrows 盒
        /// （arrow-picker 预制体实例）+光环槽+条目；规则级光环（仪典）=无箭头·双方生效·全局唯一
        /// （2026-10-03 定案）——含规则原子即关闭箭头预选（规则光环不占连接位）。
        /// 2026-10-09 定案：arrows 盒迁入光环槽底部（跨级克隆 tpl-arrows 进 slot——槽区仅 aura-slot
        /// 一个槽）；单槽单选——库点击整组替换上一个选中（RefreshAuraLibrary 落位）。
        /// 2026-10-07 条目迁入槽下作卡（aura-card 克隆——编辑走卡内 editor，与原子卡同模式）；
        /// 条目列表区就地编辑与面板 aura-cost 费用预览退役（费用由顶栏同源独占）。</summary>
        private void MakeAuraPanel(RectTransform parent)
        {
            var h = _graph.header;
            h.LinkAuras ??= new List<LinkAuraData>();
            bool hasRule = HasRuleAuraStep(_graph);
            if (hasRule)
            {
                h.ArrowDirections = 0; // 规则级光环：无箭头（引擎侧不读连接位——写盘一致）
                MakeHint(parent, "规则级光环＝全局唯一：载体入场即挂（新的登场把旧的送墓），离场/被无效即失效"
                    + "——无箭头、不占连接位；普通光环（连接箭头）才需要箭头预选。作用域按行：仪典族对双方生效；"
                    + "战斗改写仪典（毒蚀/霜蚀/眠蚀）仅持有者（光环控制者）的生物生效（2026-10-05）");
            }
            else
            {
                MakeHint(parent, "光环＝持续效果：连接方向档作用对象=箭头指向格的当前占据者（断链/离场即失效）；"
                    + "作用面档（己方/对方/双方）=整侧生物，关键词条目可选「是否包含角色」（计费按 5 单位档）。");
            }

            // ---- 连接箭头已迁入光环槽底部（2026-10-09）——克隆随下方光环槽块 ----

            // ---- 光环槽（2026-10-07 专用定案：光环单独用 tpl-aura-slot——不再与普通原子槽混用；
            //      仪典卡/条目卡统一走 aura-card，编辑在卡面直给） ----
            {
                var slot = UiKit.CloneTemplate("tpl-aura-slot", _slotArea.Content);
                if (slot == null)
                {
                    Debug.LogError("[EffectComposer] 缺 tpl-aura-slot 光环槽模板——光环槽区跳过（校验区随缺）；检查 EffectUI.prefab");
                    return;
                }
                foreach (var ol in slot.GetComponents<Outline>()) UnityEngine.Object.Destroy(ol);
                var slotImg = slot.GetComponent<Image>();
                if (slotImg != null) slotImg.raycastTarget = true; // 整槽可点（选中）
                var auraTitle = UiKit.FindDeep(slot, "slot-title")?.GetComponent<TMP_Text>();
                if (auraTitle == null) Debug.LogError("[EffectComposer] tpl-aura-slot 缺 slot-title——检查 EffectUI.prefab");
                else { auraTitle.text = "光环槽"; UiKit.Size(auraTitle, fw: 1f); }
                MarkSelected(slot, SelKind.Parallel, 0);
                SelectOnClick(slot, SelKind.Parallel, 0, false);
                if (_graph.steps.Count > 0)
                {
                    var step = _graph.steps[0];
                    if (step.kind == 0 && step.atomic != null)
                        MakeAuraRuleCard(slot, step.atomic); // 仪典卡=摘要+删除（光环专用卡，无参数区）
                    else MakeReadonlyStepBadge(slot, step); // 抉择等只读
                }
                for (int i = 0; i < h.LinkAuras.Count; i++)
                    MakeAuraEntryCard(slot, i);
                if (h.LinkAuras.Count == 0 && !hasRule)
                    MakeHint(slot, "尚无光环——右侧光环库点击选择（属性/关键词/规则级；新选即替换旧选）");
                MakeHint(slot, "条目费=源行光环化×单回合档；作用面累乘在条目：己/对方=4 单位、双方=2、含角色=5（×1.2 累乘）");

                // ---- 连接箭头（2026-10-09 迁入光环槽底部——arrows 归 aura-slot 下面；跨级克隆
                //      tpl-arrows（模板在槽区 content）进 slot。存在连接方向档条目才显示；
                //      规则级/作用面档关闭） ----
                _arrowCountLabel = null; // 随面板重建作废（防悬挂引用写已销毁对象）
                if (!hasRule && AnyArrowModeEntry(h))
                {
                    var arrowsBox = UiKit.CloneTemplateFrom(_slotArea.Content, "tpl-arrows", slot);
                    if (arrowsBox == null)
                        Debug.LogError("[EffectComposer] 槽区缺 arrows 烘焙节点（2026-10-06 箭头预制体化）——检查 EffectUI.prefab");
                    else
                    {
                        UiKit.Size(arrowsBox, fw: 1f); // 槽内铺满宽（高度仍由模板接管）
                        var title = UiKit.FindDeep(arrowsBox, "title")?.GetComponent<TMP_Text>();
                        if (title != null) title.text = "连接箭头";
                        var head = UiKit.FindDeep(arrowsBox, "head");
                        if (head == null)
                            Debug.LogError("[EffectComposer] arrows 盒缺 head 行——箭头计数标签不显示；检查 EffectUI.prefab");
                        _arrowCountLabel = head != null
                            ? UiKit.Label("count", head, ArrowCountText(), UiStyle.MiniSize, UiStyle.TextHint)
                            : null;
                        var picker = BindArrowPicker(arrowsBox);
                        UiKit.Described(picker.GetComponent<Button>() ?? picker.gameObject.AddComponent<Button>(),
                            "连接箭头：光环的作用方向。点击某个方向点亮或取消（白=已选），"
                            + "光环持续作用于箭头所指格子上的单位。方向以这张卡持有者的视角为准，"
                            + "至少要选一个方向；箭头越多费用涨得越快。");
                        // 校验：普通光环必须搭配至少一支箭头（无箭头=永无受益者）
                        _slotZones.Add(MakeZone(arrowsBox, () => (HexDirection)h.ArrowDirections == HexDirection.None
                            ? "未选箭头——光环必须搭配至少一支箭头（无箭头=永无受益者）；规则级光环则不需要箭头" : null));
                    }
                }
                // 校验：至少一条有效光环内容（条目或槽内规则原子）+ 关键词条目须位 5 可挂 + 多仪典全局唯一提醒
                _slotZones.Add(MakeZone(slot, () =>
                {
                    if (ValidAuraCount(h.LinkAuras) == 0 && !hasRule)
                        return "无有效光环内容——条目/规则级原子 至少配一条（空条目保存时剔除）";
                    if (RuleAtomCount(_graph) >= 2)
                        return "多行规则原子：仪典全局唯一，入场时新的把旧的送墓——通常一行就够";
                    foreach (var a in h.LinkAuras)
                    {
                        if (a == null || string.IsNullOrEmpty(a.keyword)) continue;
                        if (!ComposerCatalog.IsAuraMountableKeyword(a.keyword))
                            return $"关键词「{a.keyword}」不可作光环——表行标「不可作为连接光环」"
                                 + "（真消耗型生效后移除，与光环 live-query 持续语义冲突）；请改选下拉中的可挂关键词";
                    }
                    return null;
                }));
            }
        }

        /// <summary>steps 中的规则光环原子数（光环槽内仪典计数——全局唯一提醒用）。</summary>
        private static int RuleAtomCount(EffectGraphData g)
        {
            int n = 0;
            foreach (var s in g?.steps ?? new List<EffectStepData>())
                if (s?.kind == 0 && s.atomic != null && IsRuleAuraRow(AtomicEffectTable.GetByHashId(s.atomic.refId))) n++;
            return n;
        }

        /// <summary>是否存在连接方向档条目（2026-10-08 条目级作用面）：箭头盒显隐与箭头校验的适用条件
        ///——全部条目走作用面档时无箭头语义（空条目表=默认箭头档，保旧默认视图）。</summary>
        private static bool AnyArrowModeEntry(CardEffectData h)
            => h.LinkAuras == null || h.LinkAuras.All(a => a == null || a.scope == 0);

        /// <summary>六向箭头绑定（2026-10-06 arrow-picker 预制体化）：按钮名 T/D/L/R=上/下/左/右
        ///（L-T=左上…R-D=右下——见 ArrowButtons 表）；三角朝向/间距/底格由预制体烘焙，代码只挂
        /// 选中态变色——未选 #A1A1A1、选中纯白。</summary>
        private RectTransform BindArrowPicker(RectTransform arrowsBox)
        {
            var h = _graph.header;
            var picker = UiKit.FindDeep(arrowsBox, "arrow-picker");
            if (picker == null)
            {
                Debug.LogError("[EffectComposer] arrows 盒缺 arrow-picker 预制体实例——检查 EffectUI.prefab");
                return arrowsBox;
            }
            foreach (var (dir, nodeName) in ArrowButtons)
            {
                var node = UiKit.FindDeep(picker, nodeName);
                var btn = node?.GetComponent<Button>();
                // 变色目标=按钮自身的底格 Image（方向标注层）；tri 子节点（DropdownArrow 刻痕）保持预制体
                // 烘焙色只作方向标记——2026-10-06 实测定案：刻痕太小，选中态亮底格才可见
                var padImg = node?.GetComponent<Image>();
                if (btn == null || padImg == null)
                {
                    Debug.LogError($"[EffectComposer] arrow-picker 缺按钮「{nodeName}」或其底格 Image——检查 arrow-picker.prefab");
                    continue;
                }
                void Sync()
                {
                    bool on = ((HexDirection)h.ArrowDirections).HasFlag(dir);
                    padImg.color = on ? Color.white
                        : new Color(161f / 255f, 161f / 255f, 161f / 255f); // 未选 #A1A1A1
                    // [ArrowDebug] 变色排查临时日志：面板构建×6 + 每次点击各打一条（按钮名+写入色）
                    Debug.Log($"[ArrowDebug] {nodeName} 选中={on} 底格写入=#{ColorUtility.ToHtmlStringRGB(padImg.color)}"
                        + $" Arrows={(HexDirection)h.ArrowDirections}");
                }
                Sync();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    var cur = (HexDirection)h.ArrowDirections;
                    h.ArrowDirections = (int)(cur.HasFlag(dir) ? cur & ~dir : cur | dir);
                    Sync();
                    if (_arrowCountLabel != null) _arrowCountLabel.text = ArrowCountText();
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

        /// <summary>光环卡克隆源（2026-10-07 光环专用卡）：tpl-aura-slot 内 pristine aura-card——
        /// head 摘要(text+del) + field-value 数值 + aura-duration 行(placeholder 标签 + aura-kinds 下拉)。
        /// 缺卡 LogError 返回 null——不代码补建。</summary>
        private RectTransform CloneAuraCard(RectTransform slot)
        {
            var tplCard = _slotArea.Content.Find("tpl-aura-slot")?.Find("aura-card") as RectTransform;
            var card = tplCard != null
                ? UnityEngine.Object.Instantiate(tplCard, slot, false) as RectTransform
                : null;
            if (card == null)
            {
                Debug.LogError("[EffectComposer] tpl-aura-slot 缺 aura-card——本卡跳过；检查 EffectUI.prefab");
                return null;
            }
            card.gameObject.SetActive(true);
            return card;
        }

        /// <summary>仪典卡（光环专用）：aura-card 摘要+删除——仪典无参数（field-value/种类行隐藏）；
        /// 普通原子卡（atom-card）不再进光环槽（2026-10-07 不混用定案）。</summary>
        private void MakeAuraRuleCard(RectTransform slot, AtomicEffectEntry atom)
        {
            var card = CloneAuraCard(slot);
            if (card == null) return;
            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>();
            if (text == null) Debug.LogError("[EffectComposer] aura-card 缺 text 摘要文本——检查 EffectUI.prefab");
            else
            {
                text.text = "◈ " + AtomText.RenderAtomEntry(atom, _graph.header);
                UiKit.Size(text, fw: 1f);
            }
            var head = UiKit.FindDeep(card, "head");
            var delNode = head != null ? UiKit.FindDeep(head, "del") : null;
            if (delNode == null) Debug.LogError("[EffectComposer] aura-card 缺 del——仪典不可删；检查 EffectUI.prefab");
            else BindCardButton(delNode, "删除", true, () => RemoveAtom(SelKind.Parallel, 0));
            UiKit.FindDeep(card, "field-value")?.gameObject.SetActive(false); // 仪典无数值

            // 原子表行四参数 des（2026-10-09 定案——卡面常显）
            BindAtomDes(card, AtomicEffectTable.GetByHashId(atom?.refId));

            // 仪典作用范围（2026-10-09 设置盒拆除迁入 actuating-range 行）：绑 step.atomic.value
            //（RuleAuraScope 序=方向码−1；选项极性门控）——原 BindAuraScopeSettings/dd-kinds 复用退役；
            // 仪典无「含角色」概念——player Toggle 恒隐（prefab 未烘焙亦不报缺）
            var range = UiKit.FindDeep(card, "actuating-range") as RectTransform;
            var rangeDd = range != null ? UiKit.FindDeep(range, "aura-kinds")?.GetComponent<TMP_Dropdown>() : null;
            var rcfg = AtomicEffectTable.GetByHashId(atom?.refId);
            if (range == null || rangeDd == null || rcfg == null)
            {
                Debug.LogError("[EffectComposer] 仪典卡缺 actuating-range/aura-kinds 或表行——作用范围不可编；检查 EffectUI.prefab");
            }
            else
            {
                UiKit.FindDeep(range, "player")?.gameObject.SetActive(false);
                var opts = AllowedScopesOfPolarity(rcfg.Polarity);
                int cur = atom.value + 1; // RuleAuraScope 序 → 方向码
                if (!opts.Contains(cur)) cur = opts.Contains(2) ? 2 : opts[opts.Count - 1];
                atom.value = cur - 1;

                rangeDd.ClearOptions();
                rangeDd.AddOptions(opts.Select(LinkScopeZh).ToList());
                rangeDd.SetValueWithoutNotify(Mathf.Max(0, opts.IndexOf(cur)));
                rangeDd.RefreshShownValue();
                rangeDd.onValueChanged.RemoveAllListeners();
                rangeDd.onValueChanged.AddListener(i =>
                {
                    int v = opts[Mathf.Clamp(i, 0, opts.Count - 1)];
                    if (v > 0) atom.value = v - 1;
                    RefreshName(); // 范围影响计价——费用预览随改随刷
                });
                UiKit.DescribedOptions(rangeDd,
                    "作用范围：仪典对哪一侧生效——可选项由极性决定。",
                    i =>
                    {
                        if (i < 0 || i >= opts.Count) return null;
                        int v = opts[i];
                        return v == 1 ? "己方：只为你（光环控制者）一侧生效。"
                            : v == 3 ? "对方：只为对手一侧生效。"
                            : "双方：两侧都生效——费用减半（对称让利）。";
                    });
            }
        }

        /// <summary>光环条目卡（2026-10-07 光环专用卡；2026-10-08 作用面改版）：aura-card 直编辑——
        /// head 摘要+删除；field-value=属性数值（关键词条目隐藏）；actuating-range 行（原 aura-duration
        /// 更名）=条目级作用范围——aura-kinds 下拉填 连接方向/己方/对方/双方（极性门控），连接方向档
        /// 才显示槽外箭头盒；作用面档显示 player Toggle=是否包含角色（仅关键词条目——属性增加不作用于
        /// 角色，角色只吃角色攻击力原子且该原子不可作光环）。条目种类（攻/生/属性、关键词选择）随
        /// 2026-10-08 定案迁右栏库行细分——卡面不再有种类下拉。</summary>
        private void MakeAuraEntryCard(RectTransform slot, int index)
        {
            var aura = _graph.header.LinkAuras[index];
            var card = CloneAuraCard(slot);
            if (card == null) return;
            bool isStat = !string.IsNullOrEmpty(aura.stat);

            // 原子表行四参数 des（2026-10-09 定案）：属性条目按符号取源行（Add±One——与计价同口径）；
            // 关键词条目无表行参数——des 隐藏
            if (isStat)
                BindAtomDes(card, AtomicEffectTable.GetByType(aura.value >= 0
                    ? AtomicEffectType.AddPlusOne : AtomicEffectType.AddMinusOne));
            else
                UiKit.FindDeep(card, "des")?.gameObject.SetActive(false);

            var head = UiKit.FindDeep(card, "head");
            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>();
            if (text == null) Debug.LogError("[EffectComposer] aura-card 缺 text 摘要文本——检查 EffectUI.prefab");
            if (text != null) UiKit.Size(text, fw: 1f);
            void RefreshText()
            {
                if (text != null) text.text = AuraEntryText(aura);
                RefreshName(); // 顶栏费用（AuraTopCostText 同源）随条目改动刷新
                ValidateZones();
            }
            RefreshText();

            var delNode = head != null ? UiKit.FindDeep(head, "del") : null;
            if (delNode == null) Debug.LogError("[EffectComposer] aura-card 缺 del——条目不可删；检查 EffectUI.prefab");
            else BindCardButton(delNode, "删除", true, () =>
            {
                _graph.header.LinkAuras.RemoveAt(index);
                RefreshSlots();
            });

            // ---- 数值（属性条目）----
            var valNode = UiKit.FindDeep(card, "field-value");
            if (valNode == null) Debug.LogError("[EffectComposer] aura-card 缺 field-value——检查 EffectUI.prefab");
            else
            {
                valNode.gameObject.SetActive(isStat);
                if (isStat)
                {
                    var input = valNode.GetComponentInChildren<TMP_InputField>(true);
                    if (input != null)
                    {
                        input.contentType = TMP_InputField.ContentType.IntegerNumber;
                        input.text = aura.value.ToString();
                        input.onEndEdit.RemoveAllListeners();
                        input.onEndEdit.AddListener(_ =>
                        {
                            if (int.TryParse(input.text, out var v)) { aura.value = v; RefreshSlots(); }
                            else input.text = aura.value.ToString();
                            // RefreshSlots 全量重建：数值正负翻转改变极性门控的可选作用面
                        });
                        UiKit.Described(input,
                            "数值：光环给范围内单位的属性增减——正数加强、负数削弱，数值越大费用越高。"
                            + "多支箭头指向同一个单位时，每一支都各自生效一次。");
                    }
                }
            }

            // ---- 作用范围（actuating-range 行——2026-10-08 条目级定案；placeholder 行标签已烘焙「作用范围」）----
            var rangeRow = UiKit.FindDeep(card, "actuating-range");
            var rangeDd = rangeRow != null ? UiKit.FindDeep(rangeRow, "aura-kinds")?.GetComponent<TMP_Dropdown>() : null;
            if (rangeDd == null)
                Debug.LogError("[EffectComposer] aura-card 缺 actuating-range 行或 aura-kinds 下拉——检查 EffectUI.prefab");
            else
            {
                // 极性门控（口径沿 2026-10-07）：增益→{己方,双方}、减益→{双方,对方}、中性→三选；
                // 连接方向档恒可选。当前值越门控（数值翻号残留）收敛到首个合法作用面档。
                float pol = isStat
                    ? (aura.value >= 0 ? 1f : -1f)
                    : (AtomicEffectTable.GetByEnumName(
                           CardLoader.GetKeywordDefinition(aura.keyword ?? "")?.atomicEffect ?? "")?.Polarity ?? 0f);
                var allowed = AllowedScopesOfPolarity(pol);
                if (aura.scope > 0 && !allowed.Contains(aura.scope)) aura.scope = allowed[0];
                var optVals = new List<int> { 0 };
                optVals.AddRange(allowed);
                int cur = Math.Max(0, optVals.IndexOf(aura.scope));
                new UiKit.Dropdown(rangeDd, optVals.Select(LinkScopeZh).ToList(), cur, (idx, _) =>
                {
                    aura.scope = optVals[idx];
                    RefreshSlots(); // 箭头盒显隐/费用/校验随档位重建
                }).Describe(
                    "作用范围：这条光环向哪里投射。连接方向=随卡面箭头指向的格子（箭头在光环槽底部的「连接箭头」盒中选）；"
                    + "己方/对方/双方=整侧生物；关键词条目同时作用于该侧角色（2026-10-09 裁定——恒含角色）。"
                    + "属性增加只作用于生物——角色只吃角色攻击力原子（该原子不可作光环）。",
                    idx =>
                    {
                        int v = optVals[Mathf.Clamp(idx, 0, optVals.Count - 1)];
                        return v == 0 ? "连接方向：作用对象=箭头指向格的当前占据者（指向角色格=投递角色）——在光环槽底部的「连接箭头」盒逐向点亮。"
                            : v == 1 ? "己方：只为你（光环控制者）一侧生效——生物+角色（关键词条目，5 单位档计费）。"
                            : v == 3 ? "对方：只为对手一侧生效——生物+角色（关键词条目，5 单位档计费）。"
                            : "双方：两侧都生效——费用减半（对称让利）。";
                    });
            }
        }

        /// <summary>条目摘要（2026-10-08 作用面版）：作用面档=【范围】前缀；
        /// 属性=「属性：攻击 +1」（Both=属性——攻生同值 ±1/±1）；关键词=「关键词：帷幕」（库外脏值直显 id）。
        /// 含角色尾注已随 2026-10-09 裁定退役（关键词条目恒含角色，不再逐条目标注）。</summary>
        private static string AuraEntryText(LinkAuraData aura)
        {
            if (aura == null) return "（空条目）";
            string scopeZh = aura.scope > 0 ? $"【{LinkScopeZh(aura.scope)}】" : "";
            if (!string.IsNullOrEmpty(aura.stat))
                return $"{scopeZh}属性：{StatZhOf(aura.stat)} {(aura.value >= 0 ? "+" : "")}{aura.value}";
            var c = ComposerCatalog.AuraKeywordChoices().FirstOrDefault(k => k.id == aura.keyword);
            return $"{scopeZh}关键词：{c.label ?? aura.keyword ?? "？"}";
        }

        /// <summary>光环计价预览卡：效果级光环无宿主卡——临时 CardData 只承条目；
        /// ArrowDirections 恒 None（箭头=卡面资产，效果层预选不计费）。
        /// 顶栏 AuraTopCostText 同源只读（2026-10-07 面板 aura-cost 行退役）。</summary>
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

        private static int CountArrowBits(HexDirection d)
        {
            int n = 0, v = (int)d;
            while (v != 0) { n += v & 1; v >>= 1; }
            return n;
        }

        // ======================================== 校验区（不合规=区域闪烁红框+提示，保存禁用） ========================================

        /// <summary>注册一个校验区（Zone=区域元素；Check 返回 null=合规，否则=错误提示文本）。</summary>
        private ZoneEntry MakeZone(RectTransform zone, Func<string> check)
            => new ZoneEntry { Zone = zone, Check = check };

        /// <summary>引导高亮框外扩边距（px）——框比区域大一圈，视觉上"框住"目标
        ///（描边只有内半段可渲染——外半段无片元，pad 一并补偿）。</summary>
        private const float ZoneFramePad = 6f;

        /// <summary>取引导高亮框（prefab 烘焙 guide-frame——EffectUI 根下带 Image；2026-10-07 兜底退役：
        /// 缺节点 LogError 一次并返回 null——校验红框不可用。材质共享，shader v2 自推导几何不接收 C# 尺寸）。</summary>
        private Image GuideFrame()
        {
            if (_guideFrame != null) return _guideFrame;
            if (_guideFrameMissing) return null;
            var node = UiKit.FindDeep(Root, "guide-frame");
            var img = node != null ? node.GetComponent<Image>() : null;
            if (img == null)
            {
                _guideFrameMissing = true; // 只报一次——防校验高频刷新刷屏
                Debug.LogError("[EffectComposer] 缺 guide-frame 高亮框（根下带 Image）——校验红框不可用；检查 EffectUI.prefab");
                return null;
            }
            if (img.material == null) img.material = UiKit.GuideFrameMaterial();
            img.raycastTarget = false;
            img.enabled = true; // 防御：prefab 烘焙/历史状态可能禁用组件——每次取用强制可见
            img.color = UiStyle.ErrorRed; // shader 顶点色相乘（shader 缺失回退材质时=静态红块）
            _guideFrame = img;
            return img;
        }

        private bool _guideFrameMissing;

        /// <summary>高亮框聚焦某区：临时挂到该区下、锚点拉伸外扩 pad（矩形由变换系统实时跟随父级）；
        /// ignoreLayout 防布局流排、SetAsLastSibling 顶层。</summary>
        private void FocusGuideFrame(RectTransform zone)
        {
            var img = GuideFrame();
            if (img == null) return;
            var rt = (RectTransform)img.transform;
            if (rt.parent != zone) rt.SetParent(zone, false);
            (rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-ZoneFramePad, -ZoneFramePad);
            rt.offsetMax = new Vector2(ZoneFramePad, ZoneFramePad);
            rt.SetAsLastSibling();
            rt.gameObject.SetActive(true);
        }

        /// <summary>高亮框回家（EffectUI 根下末位）并隐藏——无违规/槽区重建前调用，
        /// 防止挂在即将销毁的区下随之消失。</summary>
        private void ResetGuideFrame()
        {
            if (_guideFrame == null) return;
            var rt = (RectTransform)_guideFrame.transform;
            rt.SetParent(Root, false);
            rt.SetAsLastSibling();
            rt.gameObject.SetActive(false);
        }

        /// <summary>奖励原子推导费（校验/预算行共用口径）。</summary>
        private static float RewardCost(AtomicEffectEntry entry)
        {
            var inst = CardEffectConverter.ConvertAtomForUI(entry);
            return inst != null
                ? CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { inst }) : 0f;
        }

        /// <summary>统一校验收口（2026-10-05 改版：区内红字 zone-error 删除）：违规区域 GuideFrame 遮罩高亮
        /// + 错误文本上描述条 lbl-prop-desc（多区违规以「；」相连；转合规只清一次——不误清控件功能描述）；
        /// 存在任何违规 → 保存按钮禁用。返回 true=全部合规。</summary>
        private bool ValidateZones()
        {
            // 第一遍：跑 Check 判定（数据驱动——与矩形无关）
            RectTransform focus = null; // 单框焦点：首个违规区（全部错误文本见描述条）
            var errs = new List<string>();
            foreach (var z in _slotZones)
            {
                if (z?.Zone == null) continue;
                string err = null;
                try { err = z.Check?.Invoke(); } catch { /* 校验异常按合规处理——不阻塞编辑 */ }
                z.Invalid = !string.IsNullOrEmpty(err);
                if (z.Invalid) { errs.Add(err); focus ??= z.Zone; }
            }

            // 高亮框：违规→临时挂到首个违规区下拉伸填充；全合规→回 EffectUI 根下隐藏
            if (focus != null) FocusGuideFrame(focus);
            else ResetGuideFrame();

            bool any = errs.Count > 0;
            if (any)
            {
                _zoneErrShown = true;
                SetDescStrip(string.Join("；", errs));
            }
            else if (_zoneErrShown)
            {
                _zoneErrShown = false;
                SetDescStrip("");
            }
            if (_saveBtn != null) _saveBtn.interactable = !any;
            return !any;
        }

        // ======================================== 保存 ========================================

        private void OnSave()
        {
            // 校验兜底：违规时保存按钮已禁用——此处防御性复查
            if (!ValidateZones()) { ShowToast("存在不合规设置——红框区域按提示调整后再保存"); return; }

            // 效果名=顶栏预览同源串（CurrentDisplayName 单源化）；只读展示，保存时落账
            _graph.name = CurrentDisplayName();
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

            // 效果库模式：清旧档只认「从效果表载入」的源 id（编辑换内容=换 id，旧档被新版替代）。
            // 2026-10-09 修复：此前保存后把 _graph.id 回填为源 id——下一次原位改组别的效果再保存
            // 会把上一条独立库效果误删（1点伤害↔1点穿透互相覆盖根因）。删毕即闭链，不再重新武装。
            var path = EffectLibrarySerializer.Save(_graph); // Save 内计算/回填 graph.id
            string replacedId = null;
            if (!string.IsNullOrEmpty(_loadedFromLibraryId) && _loadedFromLibraryId != _graph.id)
            {
                replacedId = _loadedFromLibraryId;
                EffectLibrarySerializer.DeleteById(replacedId);
                _loadedFromLibraryId = null;
            }
            if (path != null) TutorialCreationFlow.NotifyEffectsChanged(); // 第三课走查步检测（未开课零行为）
            ShowToast(path == null ? "保存失败"
                : replacedId != null ? $"已保存：{_graph.name}（{_graph.id}）——旧版 {replacedId} 已被替代"
                : $"已保存：{_graph.name}（{_graph.id}）");
            if (_rightMode == RightMode.Effects) RefreshEffectsList();
            UpdateDeleteState(); // 清旧档闭链后 _loadedFromLibraryId 可能为 null——删除按钮同步回落禁用
        }

        // ======================================== 卡编辑往返（深拷贝） ========================================

        /// <summary>卡内效果 → 编辑图（JsonUtility 往返深拷贝 + 载入归一化——两槽定案：
        /// 遗留 kind=1 门步骤折入槽级 branch 载荷，UI 恒载荷形态）。note=归一化提示（null=无变动）。</summary>
        private static EffectGraphData CardEffectToGraph(CardEffectData effect, out string note)
        {
            note = null;
            if (effect == null) return new EffectGraphData("新效果");
            var header = JsonUtility.FromJson<CardEffectData>(JsonUtility.ToJson(effect));
            var steps = effect.Steps != null
                ? JsonUtility.FromJson<ListStepWrap>(JsonUtility.ToJson(new ListStepWrap { items = effect.Steps })).items
                : new List<EffectStepData>();
            var graph = new EffectGraphData(string.IsNullOrEmpty(effect.DisplayName) ? "新效果" : effect.DisplayName)
            {
                header = header,
                steps = steps ?? new List<EffectStepData>(),
            };
            note = NormalizeLoadedGraph(graph);
            return graph;
        }

        /// <summary>幽灵分支剥离（递归进 branch.then 奖励原子）——JsonUtility 深拷贝/落盘物化的
        /// settle=0 空对象清 null；真分支（settle∈{Gate,Outcome,Engine}）不动。</summary>
        private static void StripPhantomBranch(AtomicEffectEntry a)
        {
            if (a == null || a.branch == null) return;
            if (BranchEntryRules.IsPhantom(a.branch)) { a.branch = null; return; }
            if (a.branch.then != null)
                foreach (var r in a.branch.then) StripPhantomBranch(r);
        }

        /// <summary>载入归一化（2026-10-05 两槽定案）：①扁平 AtomicEffects（Steps 空）展开为槽位；
        /// ②遗留 kind=1 门步骤折入前一个 kind=0 原子的 branch 载荷——产出条件族（IsOutcomeCondition）
        /// → settle=2/outcomeId；非产出条件剔除；thenSteps→then；
        /// elseSteps 丢弃（条件不达成不发奖励）；落单门步剔除。③主干>2 截断保留前 2。
        /// ④幽灵分支剥离（2026-10-09）——JsonUtility 深拷贝/落盘物化的 settle=0 空分支对象统一清 null
        /// ⑤并列/分支互斥归一（2026-10-09）——并列图两槽齐时槽级 branch 载荷拆除。
        /// header.AtomicEffects 恒清（扁平/奖励通道退役——防夹带）。返回提示文本（null=无变动）。</summary>
        private static string NormalizeLoadedGraph(EffectGraphData g)
        {
            var notes = new List<string>();
            var steps = g.steps ??= new List<EffectStepData>();

            if (steps.Count == 0 && g.header.AtomicEffects != null && g.header.AtomicEffects.Count > 0)
            {
                foreach (var a in g.header.AtomicEffects)
                    if (a != null) steps.Add(new EffectStepData { kind = 0, atomic = a });
                notes.Add("扁平原子已展开为槽位");
            }
            g.header.AtomicEffects = null;

            // 幽灵分支剥离（2026-10-09）：与效果库读档 EffectSlim.ToEntry 同口径——此处收卡编辑深拷贝
            // 与夹带数据；步骤原子/遗留门奖励原子递归清（branch.then 奖励原子顺带剥）
            foreach (var s in steps)
            {
                if (s == null) continue;
                if (s.atomic != null) StripPhantomBranch(s.atomic);
                if (s.thenSteps != null) foreach (var r in s.thenSteps) if (r != null) StripPhantomBranch(r);
                if (s.elseSteps != null) foreach (var r in s.elseSteps) if (r != null) StripPhantomBranch(r);
            }

            // 旧→新迁移（2026-10-08 条目级作用面）：效果级 AuraScope 盖戳到 scope==0 的条目，
            // header 恒清零（作用面编辑在条目卡 actuating-range——保存侧不再写非零）
            if (g.header.AuraScope != 0)
            {
                int stamped = 0;
                foreach (var a in g.header.LinkAuras ?? new List<LinkAuraData>())
                    if (a != null && a.scope == 0) { a.scope = g.header.AuraScope; stamped++; }
                g.header.AuraScope = 0;
                if (stamped > 0) notes.Add($"光环作用面已迁至条目卡（{stamped} 条盖戳）");
            }

            // 遗留 kind=1 门步骤折叠（同 converter FoldBranchSteps 口径）：产出条件归 Outcome；
            // 非产出条件——剔除并记数
            var folded = new List<EffectStepData>();
            EffectStepData prev = null;
            int orphanGates = 0, droppedElse = 0, droppedForeign = 0;
            foreach (var s in steps)
            {
                if (s == null) continue;
                if (s.kind == 1)
                {
                    if (prev == null) { orphanGates++; continue; }
                    if (s.elseSteps != null && s.elseSteps.Count > 0) droppedElse++;
                    if (!ComposerCatalog.IsOutcomeCondition(s.conditionId)) { droppedForeign++; continue; }
                    var b = prev.atomic.branch ?? new BranchEntryData();
                    b.settle = (int)BranchSettleKind.Outcome;
                    b.outcomeId = s.conditionId;
                    b.condParam = s.conditionParam;
                    b.condStr = s.conditionStringParam;
                    b.then = s.thenSteps != null
                        ? new List<AtomicEffectEntry>(s.thenSteps) : new List<AtomicEffectEntry>();
                    prev.atomic.branch = b;
                    continue;
                }
                folded.Add(s);
                prev = s.kind == 0 && s.atomic != null ? s : null;
            }
            steps.Clear();
            steps.AddRange(folded);
            if (orphanGates > 0) notes.Add($"{orphanGates} 个无前置主干的门步骤已剔除");
            if (droppedElse > 0) notes.Add($"{droppedElse} 个分支的 else 奖励已丢弃（条件不达成不发奖励）");
            if (droppedForeign > 0) notes.Add($"{droppedForeign} 个非产出条件分支已剔除");

            // 主干>2 截断（两槽定案：截断显示前 2 + 告警）——仅并列形态；
            // 光环图（箭头/条目/规则光环步）不占两槽，规则仪典原子可多行，不截断
            bool auraGraph = g.header.ArrowDirections != 0
                || (g.header.LinkAuras != null && g.header.LinkAuras.Count > 0)
                || HasRuleAuraStep(g);
            int trunks = steps.Count(s => s?.kind == 0);
            if (!auraGraph && trunks > ParallelSlotCount)
            {
                var kept = new List<EffectStepData>();
                int seen = 0;
                foreach (var s in steps)
                {
                    if (s?.kind == 0)
                    {
                        if (seen >= ParallelSlotCount) continue;
                        seen++;
                    }
                    kept.Add(s);
                }
                steps.Clear();
                steps.AddRange(kept);
                notes.Add($"主干超两槽——已截断保留前 {ParallelSlotCount}（丢弃 {trunks - ParallelSlotCount} 个）");
            }

            // 并列/分支互斥归一（2026-10-09 定案）：并列图两槽齐=并列形态——槽级 branch 载荷一律拆除
            //（含 kind=1 折叠产物与引擎行残留：分支只存活于单原子形态；第二槽引擎行拆成裸原子，用户可手删）
            if (!auraGraph && steps.Count >= ParallelSlotCount)
            {
                int clearedBranches = 0;
                foreach (var s in steps)
                    if (s?.kind == 0 && s.atomic?.branch != null)
                    { s.atomic.branch = null; clearedBranches++; }
                if (clearedBranches > 0)
                    notes.Add($"并列两槽与分支互斥——已拆除 {clearedBranches} 槽残留分支载荷");
            }
            return notes.Count > 0 ? string.Join("；", notes) : null;
        }

        /// <summary>编辑图 → 卡内效果（编排字段全量拷贝）。两槽定案：保存只写 kind=0（含 branch 载荷）
        /// /kind=2 步骤；AtomicEffects=ProjectLinear 扁平投影（converter Steps 非空走 Steps——投影为兜底）。
        /// 光环字段只在光环形态存续——其余形态防夹带清空。</summary>
        private CardEffectData BuildCardEffect()
        {
            var effect = JsonUtility.FromJson<CardEffectData>(JsonUtility.ToJson(_graph.header));
            effect.DisplayName = _graph.name;
            // 防御：UI 恒载荷形态（载入已归一化）——此处仍滤掉任何残留 kind=1 门步骤与空步
            var saved = (_graph.steps ?? new List<EffectStepData>())
                .Where(s => s != null && s.kind != 1).ToList();
            // 互斥防御（2026-10-09）：并列形态两槽齐时槽级分支载荷不该存在（载入已归一、UI 已互斥）
            //——写盘前兜底清空；光环形态不动（切形态保留的种子分支载荷合法）
            if (_mode == ComposeMode.Parallel && saved.Count >= ParallelSlotCount)
                foreach (var s in saved)
                    if (s?.kind == 0 && s.atomic != null) s.atomic.branch = null;
            effect.Steps = saved.Count > 0
                ? JsonUtility.FromJson<ListStepWrap>(JsonUtility.ToJson(new ListStepWrap { items = saved })).items
                : null;
            // 扁平投影（converter 双通道兼容——Steps 非空走 Steps）
            effect.AtomicEffects = ProjectLinear(saved);
            // 逐原子目标制（2026-10-09）：header.TargetKinds 恒空防夹带（作用范围不再手选——各原子自身域独立解析）
            effect.TargetKinds = null;
            // 目标数量档 1/2/3（全部档 2026-10-09 退役，AOE 另出专用原子行）：存量 0/-1 归一未声明
            //（converter 回落 1）——效果级值此后仅作未声明原子的回落，编辑口在每原子卡面行
            if (effect.TargetCount == 0 || effect.TargetCount == -1) effect.TargetCount = -2;
            // 持续行退役（2026-10-09 指示物层化定案）：UI 不再编辑持续档——哨兵清空回落 Once 锚（1.0×）
            effect.Duration = -1;
            effect.DurationValue = 0;
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

        /// <summary>扁平投影：kind=0 原子 + 其槽级 branch.then 奖励 + 遗留 kind=1 门奖励。</summary>
        private static List<AtomicEffectEntry> ProjectLinear(List<EffectStepData> steps)
        {
            var flat = new List<AtomicEffectEntry>();
            if (steps == null) return flat;
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.kind == 0 && step.atomic != null)
                {
                    flat.Add(step.atomic);
                    if (step.atomic.branch?.then != null) flat.AddRange(step.atomic.branch.then);
                }
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

            // 2026-10-05 两槽定案：IsEngineTrunk/CanBeGateTrunk/CanBeGrantReward 已随
            // ComposerCatalog 旧 API（IsEngineTrunk/CanBeGateTrunk）与 Grant 引擎删除——
            // 引擎主干不再是原子（表内引擎行已删），主干/奖励资格只看挂载位。
            // 2026-10-05 晚间回表定案：引擎主干行回表（位 7）——填槽即自由分支。

            private HashSet<MountKind> Mounts => MountKindExtensions.ParseCsv(Cfg?.MountKinds ?? "");

            /// <summary>关键词类原子（位 0 Keyword）——库行点击同关键词面板：恒为他人赋予形态
            ///（kinds=GrantTargetKinds 覆写 + str=关键词 id）。</summary>
            public bool IsKeywordAtom => Cfg != null && Mounts.Contains(MountKind.Keyword);

            /// <summary>指示物类原子（位 1 Counter，2026-10-07 与关键词同型）：落槽=赋予任意卡形态
            ///（kinds=GrantTargetKinds 覆写 {1..8}；持续由 CounterSpec 承载，无关键词 id/头部惯例）。</summary>
            public bool IsCounterAtom => Cfg != null && Mounts.Contains(MountKind.Counter);

            /// <summary>引擎主干行（位 5）：填入槽即自由分支（无 gate-row，引擎参数+奖励槽直出）；
            /// 仅进第一槽（2026-10-09 互斥——分支与第二槽不可兼得）；零锚价零域，不参与主序列执行。</summary>
            public bool IsEngineTrunk => Mounts.Contains(MountKind.EngineTrunk);

            /// <summary>主干槽可落（2026-10-07 位 0 删除后派生，单一来源=ComposerCatalog.CanBeTrunkRow）：
            /// 非规则光环即可（守护/坚韧已先后退出可挂面——配对制/指示物化）；引擎行经位 5 走主干槽，
            /// 系统/攻守行有资格但被库过滤（IsLockedKeywordRow）隐藏。</summary>
            public bool CanBeTrunk => ComposerCatalog.CanBeTrunkRow(Cfg);

            /// <summary>槽内 Then 奖励资格（2026-10-07 位 3 删除后派生=CanBeRewardRow）：
            /// 派生主干资格 ∩ 非引擎行（预算/非错边过滤在调用点）。</summary>
            public bool CanBeBranchReward => ComposerCatalog.CanBeRewardRow(Cfg);

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
            public AtomicEffectEntry Entry()
                => new AtomicEffectEntry { refId = Cfg?.HashId, value = 1 };
        }
    }
}
