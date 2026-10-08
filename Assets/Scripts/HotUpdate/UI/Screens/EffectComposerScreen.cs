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
        /// 2026-10-07 持续行预制体化：atom-card/reward-slot 烘焙 aura-duration 行（「持续时间」+三档下拉），
        /// 仅赋予类（关键词/指示物原子）显示；代码生成 dd-dur 与光环槽持续回合数行退役。
        /// 2026-10-07 光环条目迁入槽下作卡（aura-entry-card——克隆 atom-card，编辑走卡内 editor）；
        /// 条目列表区就地编辑与面板 aura-cost 费用预览退役（费用顶栏同源独占）。
        /// 2026-10-07 兜底构建全退（全屏统一）：缺节点只 LogError 不代码创建（槽/奖励区/条目卡/gate-row/
        /// row 模板/guide-frame/描述条）；复合检索退役（BindDropdown 单名 TMP、BindableButton 单名、
        /// row 模板单名、FillRowParts 按名不按位、费用/计数不再父级回退）。
        /// 2026-10-08 右栏行定案不显示名字：FillRowParts 只填 meta 单列（name 节点绑定与 title 形参退役）。
    /// 2026-10-05 两槽定案：形态只余 并列（两槽）/光环——自由分支·事件引擎、有限分支·产出条件
    /// 折叠进并列形态的每槽分支编辑区（槽级 atomic.branch 载荷：结算方式下拉+条件/引擎参数+Then 奖励槽）。
    /// 核心定案：槽位选中制（右侧点击=替换选中槽）；组合形态由 InferMode 推断防互串；
    /// 校验实时化（区域闪烁红框+保存禁用）；属性描述选中才出现（描述条）；
    /// 光环=箭头+条目随效果合成（挂卡并集，效果层只预选不计箭头费）；
    /// 代价栏已上移卡组合层（错边原子在卡编辑界面填装）。
    /// </summary>
    public sealed class EffectComposerScreen : UIScreen
    {
        // ======================================== 组合形态 ========================================

        /// <summary>组合形态（2026-10-05 两槽定案）：并列（两槽·槽级分支载荷）/ 光环（连接箭头）。
        /// 原自由分支·事件引擎与有限分支·产出条件的编辑能力折叠进并列形态的每槽分支编辑区。</summary>
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
            { "疫蚀仪典", RuleAuraComponents.CombatVenom },
            { "舍身仪典", RuleAuraComponents.CombatRedirect }, // 2026-10-07 改写回归（伤害转投对手角色）
        };

        /// <summary>规则光环默认范围中文（行级——右栏库行说明用）：改写仪典=仅己方（落槽缺省）、
        /// 其余=对双方。实际生效范围逐步存于仪典步 entry.value（AutoName/设置盒按步读取，2026-10-07）。
        /// 键=DisplayName（中文短名——装载后 EnumName 列落在 DisplayName，英文枚举名在 EnumName）。</summary>
        private static string RuleAuraScopeOf(AtomicEffectConfig cfg)
            => cfg != null && RuleAuraIds.TryGetValue(cfg.DisplayName, out var rid)
                ? RuleAuraSystem.RuleAuraScopeZh(rid) : "对双方生效";

        /// <summary>规则光环行识别（2026-10-05 统一标记定案）：原子表 MountKinds 位 RuleAura 驱动——
        /// 9 行双方仪典 + 4 行改写仪典（毒蚀/霜蚀/眠蚀/疫蚀）；RuleAuraIds 字典只剩 str 短名映射职责。</summary>
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
        // 效果级设置盒（2026-10-05 模板化·每次重建随槽克隆）：四下拉+随机 Toggle——
        // 有原子才克隆显示（SyncSettingsPanel，追加在槽位之后）；speed/limit 显隐随 SyncActivationVisibility 联动
        private TMP_Dropdown _ddSpeed, _ddLimit, _ddKinds, _ddNum;
        private Toggle _tgRand;
        // 展开卡文本重渲集合（设置盒目标行写入联动——2026-10-06 多卡并存展开，逐卡登记）
        private readonly List<Action> _settingsRefreshers = new List<Action>();
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
            TriggerTiming.OnPhaseStart => "阶段开始时：每个阶段开始都算（一回合多阶段，可多次）。",
            TriggerTiming.OnPhaseEnd => "阶段结束时：每个阶段结束都算（可多次）。",
            TriggerTiming.OnAttack => "攻击宣言时：这张卡宣言攻击。",
            TriggerTiming.OnAttacked => "被攻击时：这张卡被指定为攻击目标。",
            TriggerTiming.OnBlockDeclare => "阻拦宣言时：阻拦被宣言。",
            TriggerTiming.OnCardPlayed => "使用卡牌时：使用宣言的时点（付费前，含法术）。",
            TriggerTiming.OnSpellCast => "施放法术时：有法术被使用。",
            TriggerTiming.OnTap => "横置时：这张卡被横置。",
            TriggerTiming.OnUntap => "重置时：这张卡被重置。",
            TriggerTiming.OnTargeted => "被指定为目标时：有效果以这张卡为目标。",
            TriggerTiming.OnSummon => "进场时：任意进场方式都算（打出/召唤/复活/衍生物/控制权变更）。",
            TriggerTiming.OnOtherCreatureEnter => "其他生物进场时（这张卡自己进场不算）。",
            TriggerTiming.OnGameStart => "游戏开始时：每局固定一发——作用次数自动锁 1。",
            TriggerTiming.OnAtomicEffectActivation => "原子效果发动时。",
            TriggerTiming.OnAtomicEffectStartApplying => "原子效果开始作用时。",
            TriggerTiming.OnAtomicEffectResolution => "原子效果结算完成时。",
            _ => null,
        };

        // 设置盒档位表（2026-10-05 七项定案+下拉化）：速度仅主动三档 0/1/2（0=普通档——仅自己回合主阶段，
        // 2026-10-05 用户定案自 1/2/3 改档；引擎 0 速语义本就完备，见 SpeedSystem）；作用次数仅非主动（-1=全部/不限）；
        // 目标数量 0=全部（作用范围内全取）。选择模式由 目标数×作用范围 推导（CardEffectConverter）——不再手选
        private static readonly int[] SpeedTiers = { 0, 1, 2 };
        private static readonly int[] LimitTiers = { 1, 2, 3, -1 };
        private static readonly int[] CountTiers = { 1, 2, 3, 0 };

        // 描述条文案（原子/奖励原子两处共用——面向新玩家白话口径，不谈名义值/锚点等内部概念）
        private const string ValueFieldDesc =
            "数值：效果的强度，比如伤害点数、抽牌张数。费用按这里填的数值计算；开了「数值随机」后，实际结算会在数值上下浮动。";
        private const string AmpSliderDesc =
            "数值随机：实际结算的数值会在填写的数值上下随机浮动，比例越大浮动越大。费用仍按填写的数值计算，不会因为随机而变贵。";

        /// <summary>固定战斗原子：攻击/守卫=生物卡默认携带——移出原子库不可组合。</summary>
        private static bool IsFixedBattleAtom(string enumName)
            => enumName == "Attack" || enumName == "Guard";

        // 分支事件引擎（2026-10-05 两槽定案：Grant 已删除——无条件赋予=无分支槽原子；
        // 2026-10-08 附加诅咒/附加祝福入列=八引擎；显示名代码侧维护）
        private static readonly BranchEngineKind[] BranchEngines =
        {
            BranchEngineKind.Countdown, BranchEngineKind.LuckRoll, BranchEngineKind.Clash,
            BranchEngineKind.DeathToll, BranchEngineKind.ManaSurplus, BranchEngineKind.NthHandCard,
            BranchEngineKind.CurseOnDraw, BranchEngineKind.BlessingOnDraw,
        };

        /// <summary>引擎中文名（槽内引擎下拉/预算行共用——代码侧单一来源）。</summary>
        private static string EngineZh(BranchEngineKind engine) => engine switch
        {
            BranchEngineKind.Countdown => "倒计时",
            BranchEngineKind.LuckRoll => "运势",
            BranchEngineKind.Clash => "拼点",
            BranchEngineKind.DeathToll => "死亡计数",
            BranchEngineKind.ManaSurplus => "元素充盈",
            BranchEngineKind.NthHandCard => "手牌序位",
            BranchEngineKind.CurseOnDraw => "附加诅咒",
            BranchEngineKind.BlessingOnDraw => "附加祝福",
            _ => engine.ToString(),
        };

        /// <summary>锁定关键词行（移出原子库、不可组合、不展示）：攻击/守卫
        /// + 位 6 系统内部行（2026-10-04：修改攻击力/生命值/费用——留给系统，不暴露给玩家组合）。
        /// 引擎主干行（位 7，2026-10-05 回表）**不在锁定期**——玩家直接在原子库选，填槽即自由分支。</summary>
        private static bool IsLockedKeywordRow(AtomicEffectConfig r)
            => r != null && (IsFixedBattleAtom(r.EnumName)
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
            UiKit.Described(BindButton("btn-back", () => Manager.Back()),
                "返回：回到上一界面，这里正在编辑的内容不会保存。");
            _nameLabel = FindText("lbl-effect-name");
            // 费用显示=文本 lbl-effect-cost（2026-10-05 预制体改版：CostSquares 方格退役）——
            // 参数有效时展示完整费用计算（RefreshName 驱动）；无效组合不显示内容
            _costLabel = Find("lbl-effect-cost")?.GetComponent<TMP_Text>();
            _activationDropdown = BindDropdown("dropdown-activation",
                ActivationChoices.Select(c => c.label).ToList(), 0);
            _timingDropdown = BindDropdown("dropdown-timing", new List<string> { "—" }, 0);
            _timingRow = Find("timing-row");
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
            // 全中文时机下拉（主动三档 Activate_* 不入——主动不设时机）
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

            if (_activationDropdown?.Root != null)
                _activationDropdown?.Root.gameObject.SetActive(!aura);
            _timingRow?.gameObject.SetActive(!aura && h.ActivationType != 2);

            SyncSettingsRowVisibility(); // 发动行显隐联动（设置盒随最新绑定卡对齐）
            ApplyOnceTimingLimitLock();  // 方式切换钉时机后重裁必然单发锁（主动档隐藏时机=不生效）
        }

        /// <summary>发动行显隐（2026-10-05 定案）：主动=速度档（自动响应无速度）；非主动（自动/系统）=作用次数档。</summary>
        private void SyncSettingsRowVisibility()
        {
            bool voluntary = _graph.header.ActivationType == (int)EffectActivationType.Voluntary;
            if (_ddSpeed != null) _ddSpeed.gameObject.SetActive(voluntary);
            if (_ddLimit != null) _ddLimit.gameObject.SetActive(!voluntary);
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
                        body = $"受光环影响的生物获得{KeywordZh(a.keyword)}";
                    else continue;
                    if (a.scope > 0)
                        scopedParts.Add($"【{LinkScopeZh(a.scope)}】{body}{(a.role && !string.IsNullOrEmpty(a.keyword) ? "（含角色）" : "")}");
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

        /// <summary>光环关键词中文名（守护特判；"Armor"→坚韧为旧数据兼容显示——2026-10-08
        /// 指示物化后 Armor 已不入光环库，仅存量脏值经此映射；其余原样返回 id）。</summary>
        private static string KeywordZh(string keywordId) => keywordId switch
        {
            "Armor" => "坚韧",
            "Guardian" => "守护",
            null or "" => "？",
            _ => keywordId,
        };

        /// <summary>条目 stat 短名（Both=属性——攻生同值 ±1/±1，2026-10-07 深夜三档）。</summary>
        private static string StatZhOf(string stat)
            => stat.Equals("Life", StringComparison.OrdinalIgnoreCase) ? "生命"
             : stat.Equals("Both", StringComparison.OrdinalIgnoreCase) ? "属性" : "攻击";

        /// <summary>条目级作用范围短名（2026-10-08 actuating-range：0=连接方向/1己/2双/3对）。</summary>
        private static string LinkScopeZh(int scope)
            => scope == 1 ? "己方" : scope == 2 ? "双方" : scope == 3 ? "对方" : "连接方向";

        private void RefreshName()
        {
            // 顶栏效果名=完整效果描述（2026-10-03 定案：不再简写组合名——空效果回落自动名占位）；
            // 光环形态（2026-10-07）改走 AutoName 三段句式（仪典/改写/条目——范围前置、描述取表行）
            var full = AtomText.RenderEffectSummary(_graph);
            _nameLabel.text = _mode == ComposeMode.Aura ? AutoName()
                : (string.IsNullOrEmpty(full) ? AutoName() : full);
            if (_costLabel != null) _costLabel.text = EffectCostText();
        }

        /// <summary>顶栏费用文本（lbl-effect-cost，2026-10-05 预制体改版方格→文本）：
        /// 参数全部合规才显示——并列=效果锚价完整构成（逐原子贡献＋合计＋黑白获得）；
        /// 光环=条目平价（箭头属卡面资产、挂卡并集后由卡层计——非本效果层费用项，不列）；
        /// 无效组合（任一校验区违规）/转换失败/推导异常一律空串。</summary>
        private string EffectCostText()
        {
            if (!ZonesAllValid()) return "";
            try
            {
                return _mode == ComposeMode.Aura ? AuraTopCostText() : ParallelCostText();
            }
            catch
            {
                return "";
            }
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
        /// 主干原子 + 抉择首模式原子；分支 Then 奖励免费不列）。</summary>
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
        /// 触发时机/组合域），Effects 只装该原子——推导值＝该原子在当前参数下的真实贡献。</summary>
        private static EffectDefinition PricingShim(EffectDefinition def, AtomicEffectInstance atom) =>
            new EffectDefinition
            {
                Id = "COST_PART_PREVIEW",
                DisplayName = def.DisplayName,
                ActivationType = def.ActivationType,
                TriggerTiming = def.TriggerTiming,
                Duration = def.Duration,
                DurationValue = def.DurationValue,
                SelectionMode = def.SelectionMode,
                TargetCount = def.TargetCount,
                RandomTarget = def.RandomTarget,
                TriggerLimitPerTurn = def.TriggerLimitPerTurn,
                TargetDomain = def.TargetDomain,
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
            // 不清重建：模式片按名绑定烘焙节点（mode-Parallel/mode-Aura；无则建一次），刷新只更新文本/态色/接线
            MakeModeChip("并列（两槽）", ComposeMode.Parallel,
                "并列形态：最多两个槽，各放一颗原子组成效果；每个槽还可以加分支条件（条件成立才有奖励）。");
            MakeModeChip("光环（连接/作用面）", ComposeMode.Aura,
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

        private void MakeModeChip(string label, ComposeMode mode, string desc)
        {
            bool active = mode == _mode;
            UiKit.Described(UiKit.BindableButton($"mode-{mode}", _modeBar, label, () => SwitchMode(mode),
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
                    ShowToast("光环模式：左侧选箭头+光环条目；仪典等规则原子点击右栏即入原子槽——挂卡时箭头自动并集");
                    break;
                }
            }

            // 并列相同目标（2026-10-05）：效果级作用范围只在并列形态存续——切出即清防夹带
            if (newMode != ComposeMode.Parallel) h.TargetKinds = null;

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
            _settingsRefreshers.Clear(); // 展开卡重绑前清（MakeAtomCard/MakeRewardSlot 重新登记）
            ClampSelection();
            _graph.steps ??= new List<EffectStepData>();

            switch (_mode)
            {
                case ComposeMode.Parallel:
                {
                    // 2026-10-05 两槽定案：槽位循环 for i<2；每槽=原子卡+分支编辑区（结算方式/参数/Then 奖励）
                    RectTransform firstEmptySlot = null;
                    for (int i = 0; i < ParallelSlotCount; i++)
                    {
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
                                MakeBranchEditor(slot, step.atomic, idx); // 槽内分支编辑区（两槽定案）
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
                    // 两槽主干域交集校验（2026-10-05 两槽定案）：引擎载荷槽=条件本身（零域）豁免
                    _slotZones.Add(MakeZone(_slotArea.Content, ParallelDomainConflict));
                    break;
                }
                case ComposeMode.Aura:
                {
                    MakeAuraPanel(_slotArea.Content);
                    break;
                }
            }

            SyncSettingsPanel(); // 设置盒显隐/重置（content 级单例——原子槽有无原子）
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
        /// ——Build 时分别转隐藏模板 tpl-slot / tpl-settings（ClearChildren 放行 tpl-* 不销毁）；
        /// MakeSlot 每槽克隆，SyncSettingsPanel 有原子时克隆 settings 追加在槽位之后；缺失回退旧运行时构建。
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

            // 赋予类持续行（2026-10-07 预制体化：atom-card 烘焙 aura-duration——关键词/指示物原子显示，收起态也在卡面）
            BindGrantDurationRow(card, atom);

            if (expanded)
            {
                // 设置盒目标行写入联动重渲（多卡并存——逐卡登记）
                var refresh = MakeAtomEditorInline(card, atom, selKind, text);
                if (refresh != null) _settingsRefreshers.Add(refresh);
            }
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

        /// <summary>赋予类持续行绑定（2026-10-07 预制体化：atom-card/reward-slot 烘焙 aura-duration 行
        /// ——「持续时间」标签 + aura-kinds TMP 下拉；代码生成 dd-dur 与光环槽持续回合数行退役）。
        /// 仅关键词授予类显示：MountKinds 含关键词（位 1）＝给目标添加关键词——效果级持续档
        /// 参与计价（Grant 梯 1.2/1.6/2.0）。指示物原子（位 2）持续由 CounterSpec 四分类承载、
        /// 计价=表行锚×max(1,层数)——2026-10-08 起不再显示本行（旧「或指示物」口径随层即持续定案退役）。
        /// 持续档=效果级唯一真相（header.Duration 三档；1/2 回合同档=持续到自己回合结束，
        /// 文案「换区清除」=UntilLeaveBattlefield）——改动走 RefreshSlots 全量重绑（各行同步显真值）。</summary>
        private void BindGrantDurationRow(RectTransform host, AtomicEffectEntry atom)
        {
            var row = UiKit.FindDeep(host, "aura-duration");
            var dd = row != null ? UiKit.FindDeep(row, "aura-kinds")?.GetComponent<TMP_Dropdown>() : null;
            if (row == null || dd == null)
            {
                Debug.LogError("[EffectComposer] 缺 aura-duration 行或 aura-kinds 下拉（2026-10-07 持续行预制体化）——检查 EffectUI.prefab");
                return;
            }
            var cfg = AtomicEffectTable.GetByHashId(atom?.refId);
            var mounts = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
            bool grant = mounts.Contains(MountKind.Keyword); // 仅关键词授予类（指示物原子持续档不生效，隐藏）
            row.gameObject.SetActive(grant); // 非赋予类（含空奖励槽）整行隐藏
            if (!grant) return;

            var durChoices = new List<string> { "持续到自己回合结束", "换区清除", "永久" };
            var durVals = new List<int>
            {
                (int)DurationType.UntilEndOfTurn,
                (int)DurationType.UntilLeaveBattlefield,
                (int)DurationType.Permanent,
            };
            int dcur = durVals.IndexOf(_graph.header.Duration);
            var durDd = new UiKit.Dropdown(dd, durChoices, dcur >= 0 ? dcur : 0, (idx, _) =>
            {
                _graph.header.Duration = durVals[Mathf.Clamp(idx, 0, durVals.Count - 1)];
                RefreshSlots(); // 效果级唯一真相——整区重绑同步各行+费用+校验（含奖励预算重适配）
            });
            durDd.Describe(
                "持续时间：效果送给目标的东西能保持多久——点选项看各档说明。",
                idx => idx switch
                {
                    0 => "持续到自己回合结束：最便宜，到你的回合结束就消失。",
                    1 => "换区清除：目标离开战场时失效（中等价）。",
                    _ => "永久：一直有效（最贵）。",
                });
        }

        /// <summary>原子内联编辑器（prefab 静态 editor 节点：field-value 输入行[内含 slider-amp]——2026-10-07 重组后注入位退役）。
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
            // prefab 静态主干下拉控件（dd-trunk*）=历史主干卡遗留——原子卡一律隐藏（两槽定案后引擎行已删）
            UiKit.FindDeep(editor, "dd-trunk-title")?.gameObject.SetActive(false);
            UiKit.FindDeep(editor, "dd-trunk")?.gameObject.SetActive(false);
            // 2026-10-07 prefab 重组：editor 只剩 field-value（内含数值输入+slider-amp）——原 placeholder
            // 注入位退役（field-value 下的 placeholder=「效果数值」标签，勿当注入位），动态提示直接挂 editor 根。
            var inject = editor;

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

            // ---- 关键词类原子（MountKinds 含 Keyword 位）：持续档在卡面 aura-duration 行
            //（BindGrantDurationRow——2026-10-07 预制体化），编辑器内无参数可编 ----
            if (mounts.Contains(MountKind.Keyword))
            {
                valueNode?.gameObject.SetActive(false);
                sliderNode?.gameObject.SetActive(false);

                return RefreshTexts; // 数值/随机不显示；效果级设置盒照常（MakeAtomCard 层绑定）
            }

            // ---- 规则光环原子（位 8 RuleAura）：Value=作用范围（RuleAuraScope）非数值——数值/随机不显示；
            // 范围在效果级设置盒「作用范围」下拉编辑（光环形态 BindAuraScopeSettings 专用绑定）----
            if (mounts.Contains(MountKind.RuleAura))
            {
                valueNode?.gameObject.SetActive(false);
                sliderNode?.gameObject.SetActive(false);
                MakeHint(inject, "仪典无数值参数：作用范围（己方/双方/对方）在下方设置盒选择——双方档费用减半");
                return RefreshTexts;
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

            // 检索按维度档计费提示
            if (type == AtomicEffectType.SearchDeck)
                MakeHint(inject, "检索按维度档计费：字符串字段填宣言卡名（ExactCard=3），空=单维度 1");

            return RefreshTexts;
        }

        // ======================================== 槽内分支编辑区（2026-10-05 两槽定案） ========================================

        /// <summary>槽内分支编辑区（2026-10-05 晚间定案：两流程）：
        /// ① 引擎主干行（位 7）——填入即自由分支：无 gate-row，引擎标识+参数行+Then 奖励槽直出；
        /// ② 通常原子——单 gate-row（默认「无」）：族内产出条件 ∪ 局面门（附加诅咒=诅咒门特例），
        /// 选条件后建奖励槽；settle 按条件 id 推导（产出条件→Outcome 纯奖励 / 局面门→Gate 对赌）。
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

                var engRow = UiKit.Row("branch-engine", slot, spacing: 6f);
                UiKit.Label("lbl", engRow, "引擎", UiStyle.MiniSize, UiStyle.TextDim);
                UiKit.Label("name", engRow,
                    $"{EngineZh(engineKind)}（自由分支·事件驱动）", UiStyle.MiniSize, UiStyle.TextBody);

                var paramRow = UiKit.Row("branch-param", slot, spacing: 6f);
                UiKit.Label("lbl", paramRow, "参数", UiStyle.MiniSize, UiStyle.TextDim);
                ComposerCatalog.EngineParamRange((BranchEngineKind)b.engine, out var pmin, out var pmax);
                var pf = UiKit.IntField("field-engine-param", paramRow, $"{pmin}-{pmax}",
                    Mathf.Clamp(b.engineParam, pmin, pmax), v =>
                    {
                        b.engineParam = Mathf.Clamp(v, pmin, pmax); // EngineParamRange 钳制
                        RefreshName();
                        ValidateZones(); // 参数即预算（死亡计数/元素充盈/手牌序位）——原地复验
                    }, width: 80f);
                UiKit.Described(pf,
                    "参数：这个分支的判定条件——运势＝要掷出的点数线；死亡计数＝双方累计死亡数；"
                    + "元素充盈＝元素盈余量；手牌序位＝本回合打出的第几张牌；倒计时＝还剩几回合。"
                    + "倒计时填 0 表示按奖励费用自动换算回合数（1 费＝1 回合，至少 1 回合）。"
                    + "附加诅咒/附加祝福＝往牌库里投放的卡数（诅咒进对手牌库、祝福进自己牌库——"
                    + "抽到该卡的玩家触发分支效果并移除指示物，一次性）。");
                if ((BranchEngineKind)b.engine == BranchEngineKind.Countdown)
                    MakeHint(slot, "倒计时参数 0=按 Then 奖励推导费自动换算回合（1费=1回合，向上取整下限 1）");

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
                    nb.gateId = null; nb.gateParam = 0; nb.gateStr = null;
                    nb.outcomeId = null; nb.engine = 0; nb.engineParam = 0;
                    if (ComposerCatalog.IsOutcomeCondition(pick.Id))
                    {
                        nb.settle = (int)BranchSettleKind.Outcome; // 产出条件=纯奖励
                        nb.outcomeId = pick.Id;
                    }
                    else
                    {
                        nb.settle = (int)BranchSettleKind.Gate; // 局面门=对赌（未达成逆转惩罚）
                        nb.gateId = pick.Id;
                    }
                    atom.branch = nb;
                }
                RefreshSlots(); // 奖励槽随条件建/撤
                RefreshRightPanels(); // 库过滤随对赌口径刷新
            }
            var dd = new UiKit.Dropdown(gateDd, labels, cur, OnGateChanged);
            dd.Describe(
                "分支条件：给这个槽的效果加前置条件，条件成立才发下方奖励——点选项看各条件的玩法。",
                idx =>
                {
                    var pick = choices[Mathf.Clamp(idx, 0, choices.Count - 1)];
                    if (string.IsNullOrEmpty(pick.Id)) return "无：不加条件，效果直接结算。";
                    if (pick.Id == ComposerCatalog.CurseGateId)
                        return "抽到该卡时：这张卡被抽到时结算下方载荷（时机固定，附加诅咒专用）。";
                    if (ComposerCatalog.IsOutcomeCondition(pick.Id))
                        return $"{pick.DisplayName}——产出条件：本槽效果真办成这件事时才发放下方奖励（延迟验证，奖励免费）。";
                    return $"{pick.DisplayName}——对赌：达成拿奖励；没达成则奖励反转成惩罚强制执行。";
                });

            if (b != null)
                MakeRewardSlot(slot, b, slotIndex);
        }

        /// <summary>gate-row 条目集（两流程定案）：[无] + 族内产出条件 + 局面门全集；
        /// AddCurse 主干=[无]+诅咒门特例。首项恒「无」（Id=null → branch 置空）。</summary>
        private static readonly ComposerCatalog.GateSpec NoneGateSpec =
            new ComposerCatalog.GateSpec { Id = null, DisplayName = "无" };

        private static List<ComposerCatalog.GateSpec> GateRowChoices(AtomicEffectType trunk)
        {
            var list = new List<ComposerCatalog.GateSpec> { NoneGateSpec };
            if (trunk == AtomicEffectType.AddCurse)
            {
                list.Add(CurseGateSpec);
                return list;
            }
            list.AddRange(ComposerCatalog.OutcomeConditionsFor(trunk));
            list.AddRange(ComposerCatalog.SituationGates);
            return list;
        }

        /// <summary>槽内 Then 奖励槽区（2026-10-06 预制体化：content 烘焙 reward-slot → tpl-reward-slot
        /// 跨级克隆进槽，视觉不再代码构建——slot-title 标题行 + head 摘要行 + editor 参数区）。
        /// 显隐定案（2026-10-06）：无分支不克隆（整区隐藏）；非自由主干（局面门/产出条件——预算恒 1/2）
        /// 奖励参数按预算自动生成不可控——editor 恒隐藏、head 摘要无展开箭头（点击=选中奖励槽）；
        /// 自由主干（引擎）奖励预算可达 9/无上限——维持手控：head 点击展开 editor 编辑数值。</summary>
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

            // 赋予类持续行（2026-10-07 预制体化：reward-slot 烘焙 aura-duration——奖励为关键词/指示物原子时显示；
            // 非自由主干数值仍自动生成，持续档手控后 AutoFitRewardParams 在重建时重适配预算）
            BindGrantDurationRow(area, reward);

            // ---- editor：参数区（非自由主干恒隐藏——2026-10-06 参数不可控定案）----
            var editor = UiKit.FindDeep(area, "editor") as RectTransform;
            if (editor == null) Debug.LogError("[EffectComposer] tpl-reward-slot 缺 editor 参数区——检查 EffectUI.prefab");
            else editor.gameObject.SetActive(expanded);
            if (editor != null)
                UiKit.FindDeep(editor, "dd-trunk-title")?.gameObject.SetActive(false); // 遗留主干下拉标签——奖励区恒隐
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
                _settingsRefreshers.Add(RefreshText); // 设置盒目标行写入联动重渲（多卡并存——逐卡登记）
            }

            // 选中态高亮（2026-10-06 二次修正：淡黄只跟奖励槽选中走——选别处即褪色；编辑器展开不携带色）
            if (_selSlot == SelKind.Reward && _selRewardSlot == slotIndex)
            {
                img.enabled = true; // 防御：组件禁用态强制开启——选中色必可见
                img.color = new Color(232f / 255f, 208f / 255f, 96f / 255f, 0.35f);
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

            // 校验：分支未设奖励（converter 折叠为无分支——条件空转）+ 预算内
            _slotZones.Add(MakeZone(area, () =>
            {
                if (b.then == null || b.then.Count == 0)
                    return $"槽 {slotIndex + 1} 分支未设奖励——条件空转（装载按无分支处理）：点本区选中后到右栏填入，或改回「无分支」";
                int budget = BranchBudget(b);
                if (budget < 0) return null;
                float cost = RewardCost(b.then[0]);
                return cost > budget
                    ? $"槽 {slotIndex + 1} 奖励超出分支预算（{cost:0.#}/{budget}）——"
                      + (free ? "调低数值或换原子" : "参数已按预算自动生成仍超出——换更便宜的原子")
                    : null;
            }));
            RefreshText();
        }

        /// <summary>非自由主干（局面门/产出条件，2026-10-06 定案）奖励参数自动生成——玩家不可控：
        /// 该类奖励预算恒 1/2，数值=推导费不超预算的最大整数（锚价随值线性单调，自 1 上探；零锚价行回落 1）、
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
        private BranchEntryData BranchOfSlot(int slotIndex)
        {
            var steps = _graph.steps ?? new List<EffectStepData>();
            if (slotIndex < 0 || slotIndex >= steps.Count) return null;
            var s = steps[slotIndex];
            return s?.kind == 0 ? s.atomic?.branch : null;
        }

        /// <summary>引擎槽判定（主干=条件本身、零目标域——两槽域交集/效果级目标豁免）：
        /// 引擎主干行（位 7）恒为引擎载荷；旧形状（通常原子挂引擎载荷）效果库空置期一并认。</summary>
        private static bool IsEngineBranchSlot(AtomicEffectEntry a)
        {
            if (a == null) return false;
            if (a.branch != null && (BranchSettleKind)a.branch.settle == BranchSettleKind.Engine) return true;
            var row = AtomicEffectTable.GetByHashId(a.refId);
            return ComposerCatalog.IsEngineTrunkRow(row);
        }

        /// <summary>两槽主干域交集校验（两槽定案）：≥2 个非引擎槽各自带域且交集为空 → 错误文本。</summary>
        private string ParallelDomainConflict()
        {
            var domains = new List<List<int>>();
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                if (IsEngineBranchSlot(s.atomic)) continue;
                var cfg = AtomicEffectTable.GetByHashId(s.atomic.refId);
                var domain = AllowedTargetKinds(cfg, cfg?.GetTargetKindList() ?? new List<int>());
                if (domain != null && domain.Count > 0) domains.Add(domain);
            }
            if (domains.Count < 2) return null;
            var inter = domains[0];
            for (int i = 1; i < domains.Count && inter.Count > 0; i++)
                inter = inter.Intersect(domains[i]).ToList();
            return inter.Count == 0 ? "两槽主干目标域交集为空——并列共享目标不可解析（引擎载荷槽=零域豁免）" : null;
        }

        /// <summary>诅咒门目录项（合成特例——CurseGateId 不在 SituationGates 表内，预算走 GatePremium 同源）。</summary>
        private static readonly ComposerCatalog.GateSpec CurseGateSpec = new ComposerCatalog.GateSpec
        {
            Id = ComposerCatalog.CurseGateId,
            DisplayName = "抽到该卡时（时机固定）",
            ProducerTag = ComposerCatalog.CurseProducerTag,
        };

        /// <summary>gateId → 目录项（局面门 ∪ 诅咒门合成项）。</summary>
        private static ComposerCatalog.GateSpec GateSpecOf(string gateId)
            => gateId == ComposerCatalog.CurseGateId ? CurseGateSpec
                : ComposerCatalog.SituationGates.FirstOrDefault(g => g.Id == gateId);

        /// <summary>分支奖励预算（两槽定案）：Gate=门预算（GatePremium 同源）；Outcome=产出条件预算；
        /// Engine=引擎预算（死亡计数/元素充盈/手牌序位=x，其余自平衡 -1 无上限）。</summary>
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

        /// <summary>奖励筛选上限（2026-10-06 定案）：有限分支=门预算（GatePremium 恒 1/2）；
        /// 自由分支=引擎预算，无上限引擎（拼点/运势/倒计时）封顶 6——库展示与落槽校验共用一口。</summary>
        private static int RewardFilterCap(BranchEntryData b)
        {
            int budget = BranchBudget(b);
            return budget >= 0 ? budget : 6;
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
        }

        // ---- 落槽资格与写入（点击替换选中槽） ----

        // 并列主干槽可落：主动位原子∪引擎主干行，且非错边（内容契约：效果区禁错边——错边只能进代价槽）
        private bool ParallelCanDrop(object payload)
            => payload is LibPayload lp && lp.CanBeTrunk && !lp.IsWrongSideOnly;

        // 槽内 Then 奖励可落：开放奖励挂载位+非错边+筛选上限内（两槽定案；2026-10-06 上限口径
        // ——有限分支=门预算，自由分支=引擎预算·无上限封顶 6）；
        // 局面门对赌（2026-10-05，诅咒门豁免）：奖励还须可逆转
        private bool RewardCanDrop(object payload, BranchEntryData b)
        {
            if (!(payload is LibPayload lp)) return false;
            if (!lp.CanBeBranchReward || lp.IsWrongSideOnly) return false;
            if (IsGateBet(b) && !lp.IsReversible) return false;
            return RewardWithinFilterCap(lp.Entry(), b);
        }

        /// <summary>局面门对赌载荷判定（Gate 且非诅咒——未达成走逆转惩罚，奖励须可逆转）。</summary>
        private static bool IsGateBet(BranchEntryData b)
            => b != null && (BranchSettleKind)b.settle == BranchSettleKind.Gate
               && b.gateId != ComposerCatalog.CurseGateId;

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

            // ---- 发动行：速度（仅主动三档 0/1/2）/ 作用次数（非主动四档；必然单发时机锁 1）----
            _ddSpeed = BindTierDropdown(UiKit.FindDeep(settings, "speed"), SpeedTiers, h.BaseSpeed,
                v => h.BaseSpeed = v,
                "发动速度：主动效果的出手快慢——点选项看各档说明。",
                v => v.ToString(),
                v => v switch
                {
                    0 => "普通档：只能在自己回合的主要阶段发动，不能响应。",
                    1 => "瞬间档：可以在对手回合发动、当作响应使用。",
                    _ => "高速档：比 1 速更快——能响应 1 速的效果，抢先更容易。",
                });
            _ddLimit = BindTierDropdown(UiKit.FindDeep(settings, "limit"), LimitTiers, h.TriggerLimitPerTurn,
                v =>
                {
                    h.TriggerLimitPerTurn = v;
                    RefreshName(); // 作用次数影响计价——费用预览随改随刷（2026-10-05 档位化）
                },
                "作用次数：自动效果每回合最多能生效几次——点选项看各档说明。",
                optionDesc: v => v switch
                {
                    1 => "每回合最多 1 次（基础价）。",
                    2 => "每回合最多 2 次（费用 1.5 倍）。",
                    3 => "每回合最多 3 次（费用 2 倍）。",
                    _ => "不限次数（费用 4 倍）；回合开始/结束等必然单发时机会被系统锁 1 不可改。",
                });
            ApplyOnceTimingLimitLock(); // 必然单发时机锁 1+禁改（2026-10-05 晚间定案）

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
                UiKit.DescribedOptions(_ddKinds,
                    "作用范围：效果能作用到哪一类东西——可选项是两个槽的原子都支持的范围。",
                    i => i >= 0 && i < domain.Count
                        ? $"{AtomText.TargetKindZhOf((TargetKind)domain[i])}：效果只作用于这一类目标，选定后对整个效果统一生效。"
                        : null);
            }
            else if (h.TargetKinds != null) h.TargetKinds = null; // 非并列/交集空——清空防夹带

            _ddNum = BindTierDropdown(UiKit.FindDeep(settings, "dd-num"), CountTiers, h.TargetCount,
                v =>
                {
                    h.TargetCount = v;
                    RefreshName(); // 目标数量影响计价——费用预览随改随刷（2026-10-05 档位化）
                },
                "目标数量：效果发动时选取几个目标——点选项看各档说明。",
                optionDesc: v => v switch
                {
                    1 => "发动时弹窗由你选 1 个目标（基础价）。",
                    2 => "弹窗由你选 2 个目标（费用 1.5 倍）。",
                    3 => "弹窗由你选 3 个目标（费用 2 倍）。",
                    _ => "全部：范围内所有目标直接生效、不弹窗（费用 3 倍）。",
                });

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
                UiKit.Described(_tgRand,
                    "目标随机：开启后不再弹窗选目标，改为在作用范围内随机抽取（潜行、扰魔挡不住随机）。"
                    + "目标数量和作用范围照常生效。");
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
            if (_mode == ComposeMode.Aura)
                BindAuraScopeSettings(node); // 仪典作用范围（2026-10-07 范围化）；普通光环无设置盒
            else
                BindEffectSettings(node, () => { foreach (var rf in _settingsRefreshers) rf?.Invoke(); });
        }

        /// <summary>当前形态原子槽是否至少有一个原子（并列=steps 主干；光环=仪典原子步——2026-10-07 范围化：
        /// 仪典步触发设置盒克隆供「作用范围」编辑；普通光环（箭头/条目）仍无设置盒）。</summary>
        private bool HasAnyAtom() => _mode switch
        {
            ComposeMode.Parallel => _graph.steps.Any(s => s?.kind == 0 && s.atomic != null),
            ComposeMode.Aura => HasRuleAuraStep(_graph),
            _ => false,
        };

        /// <summary>光环形态设置盒（2026-10-07 范围化；2026-10-08 收敛仪典专用）：仅「作用范围」一行
        ///（复用 dd-kinds 下拉）。仪典/改写投放步→写步 value（RuleAuraScope 序=方向码−1）；
        /// 连接光环条目的作用面已迁至条目卡 actuating-range 行（条目级，2026-10-08 定案）——
        /// hasEntries 路径退役，header.AuraScope 恒 0（由条目 scope 承载）。
        /// 选项按投放步极性门控（+1→{己方,双方}；-1→{对方,双方}；0→三选）。</summary>
        private void BindAuraScopeSettings(RectTransform settings)
        {
            // 光环投放步定位（仪典/改写——全局唯一槽，混合多行时校验区已提醒，取首条）
            EffectStepData step = null;
            AtomicEffectConfig cfg = null;
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                var c = AtomicEffectTable.GetByHashId(s.atomic.refId);
                if (!IsRitualStepRow(c)) continue;
                step = s;
                cfg = c;
                break;
            }
            if (step == null) return; // 无仪典步不出设置盒（条目作用面在条目卡编辑）

            foreach (var hidden in new[] { "speed", "limit", "dd-num", "rand" })
                UiKit.FindDeep(settings, hidden)?.gameObject.SetActive(false);

            var node = UiKit.FindDeep(settings, "dd-kinds");
            var dd = node != null ? DdOf(node) : null;
            if (dd == null) return;

            var opts = AllowedScopesOfPolarity(cfg.Polarity);
            int cur = step.atomic.value + 1; // RuleAuraScope 序 → 方向码
            if (!opts.Contains(cur)) cur = opts.Contains(2) ? 2 : opts[opts.Count - 1];
            step.atomic.value = cur - 1;

            dd.ClearOptions();
            dd.AddOptions(opts.Select(LinkScopeZh).ToList());
            dd.SetValueWithoutNotify(Mathf.Max(0, opts.IndexOf(cur)));
            dd.RefreshShownValue();
            dd.onValueChanged.RemoveAllListeners();
            dd.onValueChanged.AddListener(i =>
            {
                int v = opts[Mathf.Clamp(i, 0, opts.Count - 1)];
                if (v > 0) step.atomic.value = v - 1;
                RefreshName(); // 范围影响计价——费用预览随改随刷
            });
            UiKit.DescribedOptions(dd,
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
        /// 消费点：BindEffectSettings 渲染后、时机下拉变更、发动方式钉时机后（SyncActivationVisibility）。</summary>
        private void ApplyOnceTimingLimitLock()
        {
            var h = _graph.header;
            bool once = TimingFiresOncePerTurn((TriggerTiming)h.TriggerTiming);
            if (once && h.TriggerLimitPerTurn != 1)
            {
                h.TriggerLimitPerTurn = 1;
                RefreshName(); // 计价回落 1×——费用预览随改随刷
            }
            if (_ddLimit == null) return;
            _ddLimit.interactable = !once; // 必然单发不可改（位 8 上限锁定已随 2026-10-07 删值退役）
            if (once)
            {
                _ddLimit.SetValueWithoutNotify(0); // 首档=1
                _ddLimit.RefreshShownValue();
            }
        }

        /// <summary>prefab 静态下拉取件（节点在而组件缺=数据层可见错误，不静默）。</summary>
        private static TMP_Dropdown DdOf(RectTransform node)
        {
            var dd = node?.GetComponent<TMP_Dropdown>() ?? node?.GetComponentInChildren<TMP_Dropdown>(true);
            if (node != null && dd == null)
                Debug.LogError($"[EffectComposer] 节点 {node.name} 缺 TMP_Dropdown 组件（2026-10-05 prefab 静态化）");
            return dd;
        }

        /// <summary>并列原子表域交集（极性限选后；全部 kind=0 原子）——效果级作用范围候选集。
        /// 引擎载荷槽（2026-10-05 两槽定案）=条件本身零域——不参与效果级目标，恒豁免。</summary>
        private List<int> EffectDomainIntersection() => ParallelDomainExcept(-1);

        /// <summary>并列域交集（排除 excludeIndex 槽的原子——替换选槽时旧原子不计入；
        /// 引擎载荷槽零域豁免）。</summary>
        private List<int> ParallelDomainExcept(int excludeIndex)
        {
            List<int> inter = null;
            var steps = _graph.steps ?? new List<EffectStepData>();
            for (int i = 0; i < steps.Count; i++)
            {
                if (i == excludeIndex) continue;
                var s = steps[i];
                if (s?.kind != 0 || s.atomic == null) continue;
                if (IsEngineBranchSlot(s.atomic)) continue; // 引擎载荷槽：主干=条件本身（零域）
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

        /// <summary>表切换（合并按钮）：原子库↔效果表来回切；切回原子库时重读表（承接原 btn-reload-atoms 语义）。
        /// 2026-10-06 原子表工坊：重读后须重放玩家改价（Reload 重建基线会把 overlay 冲掉）+
        /// 身份缓存失效——不然工坊改价在这里静默丢失。</summary>
        private void SwitchTable()
        {
            if (_rightMode == RightMode.Atoms)
            {
                SetRightMode(RightMode.Effects);
                return;
            }
            AtomicEffectTable.Reload();
            AtomicTableOverlay.Instance.EnsureApplied(); // 表代际已跳变：启用中即重放偏离行
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
        /// 附带中文上下文（库顶标签显示）。两槽定案：主干槽=ActiveEffect 位+非错边+域兼容；
        /// 奖励槽（SelKind.Reward）=BranchReward 位+非错边+分支预算内。</summary>
        private Func<LibPayload, bool> CurrentSlotPredicate(out string contextZh)
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                {
                    if (_selSlot == SelKind.Reward)
                    {
                        // 槽内 Then 奖励槽选中：按 BranchReward 位+筛选上限过滤（2026-10-06 定案：
                        // 有限分支=门预算（恒 1/2）；自由分支=引擎预算·无上限引擎封顶 6——只示 0-上限费原子）；
                        // 局面门对赌（2026-10-05）：奖励还须可逆转（未达成逆转惩罚的落点）
                        var b = BranchOfSlot(_selRewardSlot);
                        int budget = BranchBudget(b);
                        int cap = RewardFilterCap(b);
                        bool gateBet = b != null && (BranchSettleKind)b.settle == BranchSettleKind.Gate
                                       && b.gateId != ComposerCatalog.CurseGateId;
                        contextZh = budget > 0
                            ? $"槽 {_selRewardSlot + 1} 分支奖励（非错边{(gateBet ? "·可逆转（对赌）" : "")}——仅示 ≤{cap} 费原子）"
                            : $"槽 {_selRewardSlot + 1} 分支奖励（非错边{(gateBet ? "·可逆转（对赌）" : "")}——仅示 0-{cap} 费原子）";
                        return lp => lp.CanBeBranchReward && !lp.IsWrongSideOnly
                                     && (!gateBet || lp.IsReversible)
                                     && RewardWithinFilterCap(lp.Entry(), b);
                    }
                    // 并列相同目标（2026-10-05）：效果级作用范围=并列原子表域交集单选——库过滤收口
                    // 「域非空∧交集非空」（行自身域空不可入；与既有原子[除选中槽]交集空不可入；
                    // 引擎主干行/引擎载荷槽零域豁免——不算交集约束）
                    contextZh = $"并列槽 {_selSlotIndex + 1}（主动原子/引擎主干·非错边·作用域兼容）";
                    var others = ParallelDomainExcept(_selSlotIndex);
                    return lp => lp.CanBeTrunk && !lp.IsWrongSideOnly
                                 && (lp.IsEngineTrunk || RowDomainCompatible(lp.Cfg, others));
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
            foreach (var row in AllTableRows())
            {
                if (!Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) continue;
                // 锁定关键词（攻击/守卫 + 系统内部行）直接隐藏——不可组合成效果
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

        /// <summary>右栏·光环条目库（点击添加）：属性光环（位 3 表驱动：设置攻击力/生命力系统行）
        /// + 关键词光环（CanMountAsAura——默认可−消耗型拉黑）。点击追加到左栏条目列表（可重复）；
        /// 近似搜索按显示名过滤。</summary>
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
                _graph.header.LinkAuras ??= new List<LinkAuraData>();
                _graph.header.LinkAuras.Add(entry);
                RefreshSlots(); // 左栏条目卡/校验/费用随添加刷新（右栏行保持——可连点重复添加）
            }

            // 规则级光环：落 steps 为 ModifyGameRule 登场原子（str=规则短名）——引擎 RuleAuraSystem
            // 多槽（2026-10-07 唯一性改版：异名共存、同名在场禁打出）；作用范围=entry.value
            //（RuleAuraScope：缺省极性驱动——负面族(负极性)缺省对方、其余缺省双方，设置盒可改·极性门控）；
            // 不占 LinkAura 连接位（无箭头）；2026-10-06 与并列共用槽插入（ReplaceParallel 同一落位规范）
            void AddRuleAura(AtomicEffectConfig cfg)
            {
                ReplaceParallel(new AtomicEffectEntry { refId = cfg.HashId,
                    value = cfg.Polarity <= -0.5f ? (int)RuleAuraScope.Opponent : (int)RuleAuraScope.Both,
                    str = RuleAuraIds.TryGetValue(cfg.DisplayName, out var r) ? r : cfg.DisplayName });
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
                        if (!RewardCanDrop(payload, b)) { ShowToast("选中槽=奖励——挂载位不合、不可逆转（局面门对赌）或超出分支预算"); break; }
                        if (payload.IsKeywordAtom) ApplyKeywordHeader(); // 关键词=他人赋予形态
                        DropBranchReward(payload, _selRewardSlot);
                    }
                    else
                    {
                        if (!ParallelCanDrop(payload)) { ShowToast("该原子不可作主干（规则光环/仅连接光环节点不入效果栏）"); break; }
                        if (payload.IsKeywordAtom) ApplyKeywordHeader();
                        ReplaceParallel(InferredTrunkEntry(payload));
                    }
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

        /// <summary>主干落槽（2026-10-05 晚间定案）：引擎主干行→自动建引擎载荷（settle=3+区间默认参数）；
        /// 通常原子→无分支（有限分支默认无门槛——条件由槽内 gate-row 手选）。关键词形态沿用。</summary>
        private static AtomicEffectEntry InferredTrunkEntry(LibPayload payload)
        {
            var entry = EntryForEffectSlot(payload);
            var engineKind = ComposerCatalog.EngineKindOf(payload.Type);
            if (engineKind != BranchEngineKind.None)
            {
                ComposerCatalog.EngineParamRange(engineKind, out var mn, out var mx);
                entry.branch = new BranchEntryData
                {
                    settle = (int)BranchSettleKind.Engine,
                    engine = (int)engineKind,
                    engineParam = engineKind == BranchEngineKind.Countdown
                        ? 0 // 倒计时缺省 0=按 Then 推导费自动换算
                        : Mathf.Clamp(1, mn, mx),
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
        /// （arrow-picker 预制体实例）+共用原子槽+条目；规则级光环（仪典）=无箭头·双方生效·全局唯一
        /// （2026-10-03 定案）——含规则原子即关闭箭头预选（规则光环不占连接位）。
        /// 原子槽与并列形态共用 tpl-slot（规则原子走槽插入）；设置盒不随光环克隆（SyncSettingsPanel）。
        /// 2026-10-07 条目迁入槽下作卡（aura-entry-card 克隆 tpl-slot 的 atom-card——编辑走卡内 editor，
        /// 与原子卡同模式）；条目列表区就地编辑与面板 aura-cost 费用预览退役（费用由顶栏同源独占）。</summary>
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
                    + "战斗改写四仪典（毒蚀/霜蚀/眠蚀/疫蚀）仅持有者（光环控制者）的生物生效（2026-10-05）");
            }
            else
            {
                MakeHint(parent, "光环＝持续效果：连接方向档作用对象=箭头指向格的当前占据者（断链/离场即失效）；"
                    + "作用面档（己方/对方/双方）=整侧生物，关键词条目可选「是否包含角色」（计费按 5 单位档）。");
            }

            // ---- 连接箭头（prefab 烘焙 arrows 盒克隆——存在连接方向档条目才显示；规则级/作用面档关闭） ----
            if (!hasRule && AnyArrowModeEntry(h))
            {
                var arrowsBox = UiKit.CloneTemplate("tpl-arrows", parent);
                if (arrowsBox == null)
                    Debug.LogError("[EffectComposer] 槽区缺 arrows 烘焙节点（2026-10-06 箭头预制体化）——检查 EffectUI.prefab");
                else
                {
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
                        + "光环持续作用于箭头所指格子上的单位。方向以这张卡持有者的视角为准，至少要选一个方向。"
                        + "同一张卡上所有光环的箭头会合并计费，箭头越多费用涨得越快。");
                    // 校验：普通光环必须搭配至少一支箭头（无箭头=永无受益者）
                    _slotZones.Add(MakeZone(arrowsBox, () => (HexDirection)h.ArrowDirections == HexDirection.None
                        ? "未选箭头——光环必须搭配至少一支箭头（无箭头=永无受益者）；规则级光环则不需要箭头" : null));
                }
            }

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
                    MakeHint(slot, "尚无条目——右侧光环条目库点击添加（属性/关键词/规则级，可重复）");
                MakeHint(slot, "条目费=源行光环化×单回合档；作用面累乘在条目：己/对方=4 单位、双方=2、含角色=5（×1.2 累乘）");
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
                        if (a.role && ComposerCatalog.RoleChannelBlocked(a.keyword))
                            return $"关键词「{a.keyword}」仅生物（表行 NoRole）——不可声明含角色（2026-10-08 定案）";
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
            UiKit.FindDeep(card, "field-value")?.gameObject.SetActive(false);       // 仪典无数值
            UiKit.FindDeep(card, "actuating-range")?.gameObject.SetActive(false);   // 仪典作用域按表行——条目级范围行不适用
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
                    if (aura.scope == 0) aura.role = false; // 连接方向档的角色通道=箭头实际指向角色格（独立机制）
                    RefreshSlots(); // 箭头盒显隐/player 行/费用/校验随档位重建
                }).Describe(
                    "作用范围：这条光环向哪里投射。连接方向=随卡面箭头指向的格子（箭头在上方盒中选）；"
                    + "己方/对方/双方=整侧生物。属性增加只作用于生物——角色只吃角色攻击力原子（该原子不可作光环）。",
                    idx =>
                    {
                        int v = optVals[Mathf.Clamp(idx, 0, optVals.Count - 1)];
                        return v == 0 ? "连接方向：作用对象=箭头指向格的当前占据者——在上方「连接箭头」盒逐向点亮。"
                            : v == 1 ? "己方：只为你（光环控制者）一侧的生物生效。"
                            : v == 3 ? "对方：只为对手一侧的生物生效。"
                            : "双方：两侧生物都生效——费用减半（对称让利）。";
                    });
            }

            // ---- 是否包含角色（player Toggle——仅作用面档·关键词条目；属性条目恒隐藏）----
            var playerNode = rangeRow != null ? UiKit.FindDeep(rangeRow, "player") : null;
            var playerTg = playerNode != null
                ? playerNode.GetComponent<Toggle>() ?? playerNode.GetComponentInChildren<Toggle>(true)
                : null;
            bool roleEditable = aura.scope > 0 && !isStat
                && !ComposerCatalog.RoleChannelBlocked(aura.keyword); // NoRole 行=仅生物，无角色选项
            if (!roleEditable && aura.role) aura.role = false; // 收敛残留：切关键词/翻档后旧 role=true 不可达即清除
            if (playerTg == null)
            {
                if (roleEditable)
                    Debug.LogError("[EffectComposer] aura-card 缺 player Toggle（是否包含角色）——检查 EffectUI.prefab");
            }
            else
            {
                playerNode.gameObject.SetActive(roleEditable);
                if (roleEditable)
                {
                    playerTg.SetIsOnWithoutNotify(aura.role);
                    playerTg.onValueChanged.RemoveAllListeners();
                    playerTg.onValueChanged.AddListener(on =>
                    {
                        aura.role = on;
                        RefreshName(); // 含角色按 5 单位档计费——顶栏费用随开关刷新
                        ValidateZones();
                    });
                    UiKit.Described(playerTg,
                        "是否包含角色：开启后这条关键词条目同时作用于该侧角色（计费按 5 个单位）。"
                        + "属性条目无此选项——属性增加只作用于生物，角色攻击力走专用弹药原子。"
                        + "仅生物关键词（表行 NoRole，如再生/禁魔石）同样无此选项（2026-10-08 定案）。");
                }
            }
        }

        /// <summary>条目摘要（2026-10-08 作用面版）：作用面档=【范围】前缀（关键词条目含角色尾注）；
        /// 属性=「属性：攻击 +1」（Both=属性——攻生同值 ±1/±1）；关键词=「关键词：帷幕」（库外脏值直显 id）。</summary>
        private static string AuraEntryText(LinkAuraData aura)
        {
            if (aura == null) return "（空条目）";
            string scopeZh = aura.scope > 0 ? $"【{LinkScopeZh(aura.scope)}】" : "";
            if (!string.IsNullOrEmpty(aura.stat))
                return $"{scopeZh}属性：{StatZhOf(aura.stat)} {(aura.value >= 0 ? "+" : "")}{aura.value}";
            var c = ComposerCatalog.AuraKeywordChoices().FirstOrDefault(k => k.id == aura.keyword);
            string roleZh = aura.scope > 0 && aura.role ? "（含角色）" : "";
            return $"{scopeZh}关键词：{c.label ?? aura.keyword ?? "？"}{roleZh}";
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
            if (path != null) TutorialCreationFlow.NotifyEffectsChanged(); // 第三课走查步检测（未开课零行为）
            ShowToast(path == null ? "保存失败" : $"已保存（覆盖）：{_graph.name}（{_graph.id}）");
            if (_rightMode == RightMode.Effects) RefreshEffectsList();
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

        /// <summary>载入归一化（2026-10-05 两槽定案）：①扁平 AtomicEffects（Steps 空）展开为槽位；
        /// ②遗留 kind=1 门步骤折入前一个 kind=0 原子的 branch 载荷——产出条件族（IsOutcomeCondition）
        /// → settle=2/outcomeId，其余（局面/诅咒门）→ settle=1/gateId；thenSteps→then；
        /// elseSteps 丢弃（条件不达成不发奖励）；落单门步剔除。③主干>2 截断保留前 2。
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

            // 遗留 kind=1 门步骤折叠（同 converter FoldBranchSteps 口径）
            var folded = new List<EffectStepData>();
            EffectStepData prev = null;
            int orphanGates = 0, droppedElse = 0;
            foreach (var s in steps)
            {
                if (s == null) continue;
                if (s.kind == 1)
                {
                    if (prev == null) { orphanGates++; continue; }
                    if (s.elseSteps != null && s.elseSteps.Count > 0) droppedElse++;
                    bool outcome = ComposerCatalog.IsOutcomeCondition(s.conditionId);
                    var b = prev.atomic.branch ?? new BranchEntryData();
                    b.settle = outcome ? (int)BranchSettleKind.Outcome : (int)BranchSettleKind.Gate;
                    if (outcome) b.outcomeId = s.conditionId;
                    else b.gateId = s.conditionId;
                    b.gateParam = s.conditionParam;
                    b.gateStr = s.conditionStringParam;
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
            effect.Steps = saved.Count > 0
                ? JsonUtility.FromJson<ListStepWrap>(JsonUtility.ToJson(new ListStepWrap { items = saved })).items
                : null;
            // 扁平投影（converter 双通道兼容——Steps 非空走 Steps）
            effect.AtomicEffects = ProjectLinear(saved);
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

            /// <summary>引擎主干行（位 5）：填入槽即自由分支（无 gate-row，
            /// 引擎参数+奖励槽直出）；零锚价零域，不参与主序列执行与两槽域交集。</summary>
            public bool IsEngineTrunk => Mounts.Contains(MountKind.EngineTrunk);

            /// <summary>主干槽可落（2026-10-07 位 0 删除后派生，单一来源=ComposerCatalog.CanBeTrunkRow）：
            /// 非规则光环即可（守护/坚韧已先后退出可挂面——配对制/指示物化）；引擎行经位 5 走主干槽，
            /// 系统/攻守行有资格但被库过滤（IsLockedKeywordRow）隐藏。</summary>
            public bool CanBeTrunk => ComposerCatalog.CanBeTrunkRow(Cfg);

            /// <summary>槽内 Then 奖励资格（2026-10-07 位 3 删除后派生=CanBeRewardRow）：
            /// 派生主干资格 ∩ 非引擎行（预算/可逆转/非错边过滤在调用点）。</summary>
            public bool CanBeBranchReward => ComposerCatalog.CanBeRewardRow(Cfg);

            /// <summary>可逆转（2026-10-05 局面门对赌）：与代价栏同口径（PayloadCostDomain 三路）——
            /// 局面门分支的 Then 奖励必须可逆转，未达成惩罚才有落点。</summary>
            public bool IsReversible => CostDerivationService.PayloadCostDomain(Cfg).eligible;

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
