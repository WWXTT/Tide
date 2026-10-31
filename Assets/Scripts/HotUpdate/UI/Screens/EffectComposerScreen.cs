using Cysharp.Threading.Tasks;
using CardCore;
using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UInputField = TMPro.TMP_InputField;

namespace SynergyUI
{
    /// <summary>
    /// 效果合成界面（2026-10-01 预制体化：静态层级来自 Assets/Art/Prefab/EffectUI.prefab，
    /// Build 深度按名绑定+闭包接线；槽位区/原子库/效果表/光环面板运行时重建。
    /// 交互/文案/校验语义与历史 UITK 版对齐（修订史见 tag uitk-ui-final 版头注释）；
        /// 2026-10-02 起 uGUI-native：排版按 25645c0^ 的 EffectComposer.uxml/Common.uss 历史稿还原，
        /// 左=编辑栏（模式条+槽位区）/ 右=展示区（原子库 / 效果表双模式）+ 筛选。
        /// 2026-10-05 设置盒/数值区 prefab 静态化（tpl-slot 模板克隆）；2026-10-09 设置盒拆除：
        /// 发动参数=activate-bar 静态组（发动速度组 2026-10-15 退役删除——恒 0 不可改，速度语义
        /// 移交法术提速份额/引擎光环），作用范围/目标数量/目标随机随逐原子目标制下沉每原子卡面行
        ///（header.TargetKinds 恒空——各原子按自身表域独立解析）；选择模式/持续档/落区收口不再手编。
        /// 2026-10-07 持续行预制体化：atom-card/reward-slot 烘焙 aura-duration 行（「持续时间」+三档下拉），
        /// 仅赋予类（关键词/指示物原子）显示；代码生成 dd-dur 与光环槽持续回合数行退役。
        /// 2026-10-07 光环条目迁入槽下作卡（aura-entry-card——克隆 atom-card，编辑走卡内 editor）；
        /// 条目列表区就地编辑与面板 aura-cost 费用预览退役（费用顶栏同源独占）。
        /// 2026-10-07 兜底构建全退（全屏统一）：缺节点只 LogError 不代码创建（槽/奖励区/条目卡/
        /// row 模板/guide-frame/描述条）；复合检索退役（BindDropdown 单名 TMP、BindableButton 单名、
        /// row 模板单名、FillRowParts 按名不按位、费用/计数不再父级回退）。
        /// 2026-10-08 右栏行定案不显示名字：FillRowParts 只填 meta 单列（name 节点绑定与 title 形参退役）。
    /// 2026-10-23 五分类定案：并列/自由分支/有限分支/连接光环/规则光环 五形态（ComposeMode 五值）——
    /// 分支两形态从并列槽级载荷升为独立形态（并列回归纯两原子）；有限分支门槛卡=克隆 aura-card 点选
    ///（dd-trunk 下拉路径退役删除）；产出条件不再提供新建入口（历史 settle=2 载入只读展示）；
    /// 光环拆连接（箭头+条目）/范围（仪典步）两面，aura 判定统一走 IsAuraMode。
    /// 2026-10-23 布局对齐卡组 v2（EffectUI.prefab 同批手术）：左900/右950、面板 Rounded8+1px 边框、
    /// 形态 chips 独立行（14号/间距8/标签「形态」）、右栏行模板化 44 高双行（费用26+名称14+标签12+
    /// 操作钮，次行描述小字）、顶栏费用=徽标+一行摘要（明细悬浮进描述条——desc-strip 单行滚动）、
    /// 空槽常驻「从右侧选原子填入」占位。
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

        /// <summary>组合形态（2026-10-23 五分类定案）：并列（两原子）/ 自由分支（引擎主干+Then 奖励）/
        /// 有限分支（普通原子+局面门槛+Then 奖励）/ 连接光环（箭头+条目）/ 规则光环（仪典步）。
        /// 前三类共享槽级 branch 载荷（settle=Engine/Gate/Outcome），后两类共享光环面板。</summary>
        public enum ComposeMode { Parallel, FreeBranch, LimitedBranch, LinkAura, RangeAura }

        /// <summary>光环双形态统一判定（连接∪范围）——本屏显隐与 CardComposerScreen 五处调用共用。</summary>
        public static bool IsAuraMode(ComposeMode mode)
            => mode == ComposeMode.LinkAura || mode == ComposeMode.RangeAura;

        /// <summary>右栏双模式：原子库（点击装配）/ 效果表（点击载入编辑）。</summary>
        private enum RightMode { Atoms, Effects }

        /// <summary>由数据推断组合形态（2026-10-23 五分类，先光环后分支）：
        /// ①箭头/增益·关键词条目→连接光环；②仪典步或属性降低条目（2026-10-30 入场地光环）→场地光环；
        /// ③首原子引擎主干行→自由分支（含模拟/变形动态主干——2026-10-30 挪入）；
        /// ④首原子 Gate/历史 Outcome 载荷→有限分支（产出条件不再新建，载入只读保留）；
        /// ⑤非引擎行挂引擎载荷→并列单行只读（2026-10-26 定案：五分类前旧形状——现值=关键词获得
        /// 「自由分支·拼点→奖励」三条；自由分支主干已收窄为引擎行，此形状入自由分支必报
        /// 「主干必须是引擎行」——改按并列单行展示不报错，载荷不拆（摘要照显奖励段，保存原样往返））；
        /// ⑥其余→并列。条目归属（2026-10-30）：负属性条目=场地光环数据、其余=连接光环——
        /// 单槽整组替换制下新数据不混合；混合旧档以连接内容优先。</summary>
        public static ComposeMode InferMode(EffectGraphData g)
        {
            if (g?.header == null) return ComposeMode.Parallel;
            bool hasFieldStat = g.header.LinkAuras != null && g.header.LinkAuras.Any(
                a => a != null && !string.IsNullOrEmpty(a.stat) && a.value < 0);
            bool hasLinkContent = g.header.ArrowDirections != 0
                || (g.header.LinkAuras != null && g.header.LinkAuras.Any(
                    a => a != null && (string.IsNullOrEmpty(a.stat) || a.value > 0)));
            if (hasLinkContent) return ComposeMode.LinkAura;
            if (HasRuleAuraStep(g) || hasFieldStat) return ComposeMode.RangeAura;
            var first = g.steps?.FirstOrDefault(s => s?.kind == 0 && s.atomic != null)?.atomic;
            if (first != null)
            {
                if (ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(first.refId)))
                    return ComposeMode.FreeBranch;
                if (first.branch != null && (BranchSettleKind)first.branch.settle == BranchSettleKind.Engine)
                    return ComposeMode.Parallel; // 引擎载荷只配引擎主干行——非引擎行的旧载荷按并列单行展示
                if (first.branch != null && !BranchEntryRules.IsPhantom(first.branch))
                    return ComposeMode.LimitedBranch;
            }
            return ComposeMode.Parallel;
        }

        // ---- 规则级光环（2026-10-03 定案；2026-10-04 光环改造后 9 行——全局唯一、作用双方、无箭头；普通光环才开箭头预选。
        // 引擎合约=ModifyGameRule 原子挂载体登场效果（str=规则短名），不占 LinkAura 连接位、不写 arrows） ----

        /// <summary>仪典表行 EnumName → 规则短名（RuleAuraComponents 常量，与原子表规则光环族行一一对应；
        /// 2026-10-04 光环改造：丰盈→HealOverflow、蚕褪承接 DamageCap；
        /// 蚕褪→离散改名（2026-10-04 伤害二值离散化，短名承袭）；舍身仪典 2026-10-16 关键词化出族；
        /// 时光仪典（原轮回，DoubleTurn）2026-10-29 阶段3a 删除——额外回合改 TakeExtraTurn 原子行）。
        /// 疾风仪典 2026-10-31 整链删除。</summary>
        private static readonly Dictionary<string, string> RuleAuraIds = new Dictionary<string, string>
        {
            { "三相仪典", RuleAuraComponents.ElementConversion },
            { "苦痛仪典", RuleAuraComponents.BloodPact },     // 2026-10-07 用户改名（原血偿仪典）
            { "丰盈仪典", RuleAuraComponents.HealOverflow },
            { "离散仪典", RuleAuraComponents.DamageCap },
            { "窥渊仪典", RuleAuraComponents.LockRevealed },
            { "归土仪典", RuleAuraComponents.GraveyardPlay },
            // 疾风仪典已删（2026-10-31 整链退役——常量/枚举位/算速口同批）
            { "纳川仪典", RuleAuraComponents.HandLimitNoFatigue },
            // 涌泉仪典（疲劳免疫）2026-10-30 表行删除——常量代码侧保留专供教学直挂，不在合成器投放
            // 战斗改写仪典（2026-10-05 定案迁唯一光环；2026-10-07 降级连接光环——条目库投放+方向档）
            { "毒蚀仪典", RuleAuraComponents.CombatToxin },
            { "霜蚀仪典", RuleAuraComponents.CombatFreeze },
            // 眠蚀仪典已删（2026-10-31 整链退役——常量/枚举位/钩子同批）
            // 舍身仪典已删（2026-10-16 关键词化：舍身改关键词族投放（GrantBattleMage——2026-10-30 战斗法师改版），不在仪典目录）
            { "放血仪典", RuleAuraComponents.HealInversion },  // 2026-10-09 治疗转伤害（受光环影响一方的治疗改写为等量伤害；表行 79ef61f3；2026-10-30 用户更名——历名暗牧仪典/惩戒仪典）
            // 禁令族（2026-10-29）：行为限制光环——受光环影响方按 ScopeHits 判命中（单位/角色同门）
            { "禁攻令", RuleAuraComponents.NoAttackAura },
            { "禁守令", RuleAuraComponents.NoGuardAura },
            { "禁发令", RuleAuraComponents.NoActivateAura },
            { "禁地令", RuleAuraComponents.NoLandAura },
            // 禁令族 1b 扩充（2026-10-29；2026-10-30 起随极性钉对方单侧；禁祭令表行已删）
            { "禁疗令", RuleAuraComponents.NoHealAura },
            { "禁寻令", RuleAuraComponents.NoSearchAura },
            { "禁魔令", RuleAuraComponents.NoSpellAura },
            { "禁召令", RuleAuraComponents.NoCreatureAura },
            { "禁抽令", RuleAuraComponents.NoDrawAura },
            // 语料原生禁令族（2026-10-29 阶段2；禁非生物令 2026-10-30 表行删除）
            { "禁低费令", RuleAuraComponents.NoLowCostAura },
            { "禁高费令", RuleAuraComponents.NoHighCostAura },
            // 禁指示物令已删（2026-10-31 用户定案：指示物承载了大部分游戏规则，不能禁止）
            // 禁扰令已删（2026-10-31 用户定案：弃改版整链退役）
            // 信息轴光环（2026-10-29 阶段3c，改写族——范围三档可选，不入固定双方名单）
            { "占卜加成", RuleAuraComponents.ScryBonusAura },   // 受影响一方占卜数量+1（消费口=LookAtTopCardsHandler）
            { "灵视", RuleAuraComponents.RevealedPunishAura },  // 使用已展示手牌者受2伤且其对手回2
            // 阶段3b 改写光环（2026-10-29，改写族——范围三档可选，不入固定双方名单）
            { "延迟死亡", RuleAuraComponents.DelayedDeathAura },       // 单位死亡→下个回合结束时死亡（DeathRules 改写）
            { "牌库顶当手牌", RuleAuraComponents.TopDeckAsHandAura },  // 牌库顶当手牌使用（TopDeckPlaySource 授权）
            { "铭刻", RuleAuraComponents.InscriptionAura },            // 单位被添加指示物时层数+1（AddCounters 咽喉）
        };

        /// <summary>保护行 EnumName → 档位 str（2026-10-30 指示物化定案：目标=己方单位含角色
        ///〔数量档 1/N/全 由效果级承载〕；持续=减半/全免/生命锁护盾指示物层数——持有者回合末−1
        /// 归零，换区清）：表行本身不带 str——合成落槽时按行写入 AtomicEffectEntry.str（Mode，
        /// GrantProtectionHandler 消费）。全体档「·全体→Mode|All」三键已删（2026-10-30 用户定案：
        /// 指示物没有全体的说法，只作用于挂载单位）。</summary>
        private static readonly Dictionary<string, string> ProtectionIds = new Dictionary<string, string>
        {
            { "减半", "Halve" },
            { "免疫", "Null" },
            { "生命锁", "Floor" },
        };

        /// <summary>跳过三阶段行 EnumName（=装载后 DisplayName）→ 相位档 str（2026-10-29 阶段3a，
        /// 同 RuleAuraIds 做法）：SkipTurnHandler 按 str 分派 Whole/Standby/Main/End 档；
        /// 整回合行「跳过回合」不在字典——str 空=Whole 档。</summary>
        private static readonly Dictionary<string, string> SkipTurnIds = new Dictionary<string, string>
        {
            { "跳过结束阶段", "End" },
            { "跳过准备阶段", "Standby" },
            { "跳过主要阶段", "Main" },
        };

        /// <summary>规则光环默认范围中文（行级——右栏库行说明用）：固定双方规则
        /// （IsFixedBothRule——三相/归土/纳川+禁攻/禁守/禁发/禁地）=对双方（UI 不提供单侧选择）；
        /// 其余禁令族=仅对方（极性 -1 钉单侧，2026-10-30 起受光环影响的一方）；改写族按三档缺省对双方。
        /// 实际生效范围逐步存于仪典步 entry.value（AutoName/设置盒按步读取，2026-10-07）。
        /// 键=DisplayName（中文短名——装载后 EnumName 列落在 DisplayName，英文枚举名在 EnumName）。</summary>
        private static string RuleAuraScopeOf(AtomicEffectConfig cfg)
        {
            if (cfg == null || !RuleAuraIds.TryGetValue(cfg.DisplayName, out var rid)) return "对双方生效";
            if (RuleAuraComponents.IsFixedBothRule(rid)) return "对双方生效";
            return cfg.Polarity <= -0.5f ? "仅对方生效" : cfg.Polarity >= 0.5f ? "仅己方生效" : "对双方生效";
        }

        /// <summary>规则光环行识别（2026-10-05 统一标记定案）：原子表 MountKinds 位 RuleAura 驱动——
        /// 9 行双方仪典 + 2 行改写仪典（毒蚀/霜蚀；疫蚀 2026-10-09 随剧毒指示物删除退役；
        /// 眠蚀 2026-10-31 整链删除）；RuleAuraIds 字典只剩 str 短名映射职责。</summary>
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

        /// <summary>光环投放步行识别（仪典+改写；2026-10-31 阶段D：行类型派生——
        /// Aura* 新型 ∪ 未翻新旧共享型 ModifyGameRule，RuleAuraComponents.IsAuraLike 单源；
        /// MorphSystem.IsRuleAuraEntry 同口径）。</summary>
        private static bool IsRitualStepRow(AtomicEffectConfig cfg)
            => cfg != null
               && Enum.TryParse<AtomicEffectType>(cfg.EnumName ?? "", out var t)
               && RuleAuraComponents.IsAuraLike(t);

        // ---- 状态 ----
        private EffectGraphData _graph = new EffectGraphData("新效果");
        private ComposeMode _mode = ComposeMode.Parallel;
        private RightMode _rightMode = RightMode.Atoms;

        // 槽内卡片展开状态（代价栏已上移卡组合层）
        // 2026-10-05 两槽定案：原 GateTrunk/GateReward/FreeTrunk/FreeReward 并入 Parallel（主干槽）
        // 与 Reward（槽内 Then 奖励选中态——_selRewardSlot 标记当前编辑哪槽的奖励）
        // 2026-10-23 五分类：Gate=有限分支门槛卡选中态（右栏切 11 条门槛列表）；分支恒挂首主干
        private enum SelKind { None, Parallel, Reward, Gate }

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

        // 预设效果只读载入源（2026-10-28 定案）：从效果表「预设卡组效果」段载入时记源 id——
        // 保存恒另存新条目（同内容拦存防跨表同 id 冗余）、源教学表不可覆盖不可删
        //（删除钮随 _loadedFromLibraryId=null 隐藏；首次成功保存后才转为常规库条目可改可删）
        private string _presetSourceId;

        // 预设载入快照（2026-10-28）：无文名内容哈希——DisplayName 载入后会被 OnSave 重渲
        //（老预设文案模板 vs 新渲染口径天然漂移），身份比对须剔除名字段防「未改也判改」
        private string _presetSnapshotHash;

        /// <summary>预设内容指纹：header 克隆后清 DisplayName 再哈希——载入/保存两次同口径，
        /// 文案重渲漂移不参与「是否修改」判定。</summary>
        private static string PresetContentHash(EffectGraphData g)
        {
            var header = JsonUtility.FromJson<CardEffectData>(JsonUtility.ToJson(g.header));
            header.DisplayName = "";
            return ContentHasher.HashEffect(new EffectGraphData("") { header = header, steps = g.steps ?? new List<EffectStepData>() });
        }

        private TipPromptHost _tips; // TipPanel 提示弹窗宿主（2026-10-18 统一定案；惰性建——字段初始化器不能引用实例 Root）
        private TipPromptHost Tips => _tips ??= new TipPromptHost(() => Root);

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

        // ---- 右栏两列表（2026-10-21 回收式虚拟化）：条目装配 + 行池复用 ----
        private UiKit.RecycledList _atomRows;     // list-library=原子表/光环条目库
        private UiKit.RecycledList _effectRows;   // list-effects=效果表
        private readonly List<TableRowData> _atomItems = new List<TableRowData>();
        private readonly List<TableRowData> _effectItems = new List<TableRowData>();

        /// <summary>右栏列表条目（原子行/光环行/效果行共用形态；IsHint=提示行）。
        /// 2026-10-24 改版：行尾操作钮退役——整行点击即装载；首行=费用方格+名称(18)+标签(16 右对齐)，
        /// Meta 降为次行单行小字（悬浮整行经描述条看全文）。</summary>
        private sealed class TableRowData
        {
            public bool IsHint;
            public string Name;
            public string Tag;
            public string Meta;
            public Color Dot;
            public ElementCost Costs;   // null=清空方格
            public Action OnClick;
            public float H;
        }
        private RectTransform _modeBar, _timingRow;
        private TMP_Text _nameLabel;
        private TMP_Text _costLabel; // 顶栏费用文本 lbl-effect-cost（2026-10-30 区域扩容改版：多行明细——构成/合计+实付/代价→净费分行直显；悬浮描述条留全文）
        private RectTransform _costBadgeNode; // 费用徽标 cost-badge（2026-10-26 改版：Circle32 合计徽标退役——公用 CostSquares 预制体实例逐色块展示；无费用隐藏）
        private CostSquaresView _costBadgeView;
        private Button _deleteBtn;            // 删除当前库效果（仅效果表载入态可用）
        private UInputField _filterName;
        private UiKit.Dropdown _timingDropdown, _activationDropdown;
        // 发动参数（2026-10-09 prefab 重排·activate-bar 静态组 way/timing——speed 组
        // 2026-10-15 随发动速度退役删除，BaseSpeed 恒 0 不可改；**take-effect 次数组 2026-10-29 P9
        // 次数档软删除役**——节点常驻 prefab 不动、整组隐藏）：节点常驻不随克隆重建——
        // 写入走当前 _graph.header（次数选项已删，值对齐只剩方式/时机下拉）
        private RectTransform _activateBar, _wayGroup;
        private RectTransform _limitGroup; // P9：仅剩「隐藏 take-effect 组」职责（次数下拉/参数行已删）
        // P5 主动效果代价行（2026-10-29 建 · 2026-10-30 用户定案整删）：activate-bar 第二行
        // 代码建组退役——效果级代价不再在此编辑（header.Costs 存量数据保留，顶栏净费段照显）
        private TMP_Text _tableToggleLabel;      // 合并表切换按钮的标签（原 btn-reload-atoms/btn-load-effects）
        private List<TriggerTiming> _timings;

        // 筛选状态（2026-10-13 分类下拉退役：dropdown-filter-type 节点已删，pool-filter 颜色行接管）：
        // 颜色=单选（选色即筛费用含该色的原子，默认「全部」；激活态=chip Outline 开关）。
        // 近似搜索（2026-10-13 多词AND+语料扩展）：
        // 原子库=名∪描述∪Tags∪MountKinds∪TargetKinds；效果表=名∪id∪组成简称∪摘要全文∪引用卡(ID·名·2026-10-26)；光环库=标题∪meta
        private UIColor _colorFilter = UIColor.All;
        private readonly Dictionary<UIColor, Button> _filterChips = new Dictionary<UIColor, Button>();
        private RectTransform _poolFilter; // 颜色行容器（chip-{Color} 预制体烘焙，Build 一次接线）

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
            TriggerTiming.OnStandbyPhase => "准备阶段：回合开始系统处理（自动抽牌/倒计时等）之后的第一个阶段（2026-10-20 定案：回合开始收归系统专用，效果时点挂这里）。",
            TriggerTiming.OnDealDamage => "造成伤害时：这张卡每次造成伤害。",
            TriggerTiming.OnTakeDamage => "受到伤害时：这张卡每次受到伤害。",
            TriggerTiming.OnTurnStart => "回合开始时：每回合的系统起点（系统专用——效果时点请挂准备阶段）。",
            TriggerTiming.OnTurnEnd => "回合结束时：每个回合结束的时点。",
            TriggerTiming.OnAttack => "攻击宣言时：这张卡宣言攻击。",
            TriggerTiming.OnAttacked => "被攻击时：这张卡被指定为攻击目标。",
            TriggerTiming.OnBlockDeclare => "阻拦宣言时：阻拦被宣言。",
            TriggerTiming.OnCardPlayed => "使用卡牌时：使用宣言的时点（付费前，含法术）。",
            TriggerTiming.OnSpellCast => "施放法术时：有法术被使用。",
            TriggerTiming.OnTap => "横置时：这张卡被横置。",
            TriggerTiming.OnUntap => "重置时：这张卡被重置。",
            TriggerTiming.OnTargeted => "被指定为目标时：有效果以这张卡为目标。",
            TriggerTiming.OnOtherCreatureEnter => "其他生物进场时（这张卡自己进场不算）。",
            TriggerTiming.OnScry => "占卜时：任一玩家结算占卜（查看并排列牌库顶）的那一刻——双方占卜都触发。",
            _ => null,
        };

        // 原子目标数量档（2026-10-09 逐原子目标制）：仅 1/2/3；0=全部档退役，AOE 另出专用原子行。
        // 选择模式由 目标数×各原子自身域 推导（CardEffectConverter）——不再手选。
        // 发动速度档 2026-10-15 退役：activate-bar/speed 组删除，新组合 BaseSpeed 恒 0
        //（引擎 0 速语义完备见 SpeedSystem；速度>0 只剩法术提速份额与存量数据）
        // 2026-10-29 P9 作用次数下拉退役；2026-10-30 定案：效果无每回合生效次数上限，
        // 组合阶段不再有次数选项（take-effect 组隐藏）——平衡由时机期望系数承担。
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
        /// 引擎主干行（位 5，2026-10-05 回表）**不在锁定期**——玩家直接在原子库选，填槽即自由分支。</summary>
        private static bool IsLockedKeywordRow(AtomicEffectConfig r)
            => r != null && (IsFixedBattleAtom(r.EnumName)
                || ComposerCatalog.HasMountBit(r, MountKind.SystemInternal));

        // ======================================== 构建 ========================================

        protected override string PrefabName => "EffectUI";
        protected override string RootName => "effect-composer";

        protected override void Build()
        {
            // ---- 顶栏（2026-10-24 三行重排：行1=返回+标题；行2=效果名；行3=费用预览——
            // 保存挪左栏组合侧、删除挪右栏切换钮旁，均校验/载入态驱动显隐） ----
            UiKit.Described(BindButton("btn-back", () => Manager.Back()),
                "返回：回到上一界面，这里正在编辑的内容不会保存。");
            _nameLabel = FindText("lbl-effect-name");
            // 费用显示=文本 lbl-effect-cost（2026-10-05 预制体改版：CostSquares 方格退役）——
            // 参数有效时展示完整费用计算（RefreshName 驱动）；无效组合不显示内容
            _costLabel = Find("lbl-effect-cost")?.GetComponent<TMP_Text>();
            if (_costLabel != null)
                UiKit.Described(_costLabel,
                    "费用预览：合计费用＋黑白获得；悬浮此处看完整费用构成（逐原子公式链）。"
                    + "分支 Then 奖励免费（不计合计）；奖励超出可实现费用时按槽区红框提示调整后才能保存。");
            // 费用徽标（2026-10-26 改版）：prefab 侧=公用 CostSquares 预制体实例（根挂 CostSquaresView）——
            // 逐色块展示合计费用（与右栏行/卡面费用方格同构），0/无效组合隐藏
            _costBadgeNode = Find("cost-badge") as RectTransform;
            _costBadgeView = _costBadgeNode != null ? _costBadgeNode.GetComponentInChildren<CostSquaresView>(true) : null;
            if (_costBadgeView == null)
                Debug.LogError("[EffectComposer] 顶栏缺 cost-badge 费用徽标（CostSquares 实例）——检查 EffectUI.prefab");
            // 发动参数（2026-10-09 prefab 重排）：activate-bar 静态组（way/timing/take-effect，
            // 每组=标签+子节点 dropdown；speed 组 2026-10-15 随发动速度退役删除）；原 root 级
            // dropdown-activation/dropdown-timing/timing-row 与设置盒 speed/limit 随设置盒拆除退役
            _activateBar = Find("activate-bar");
            if (_activateBar == null)
                Debug.LogError("[EffectComposer] 缺 activate-bar（发动参数条）——检查 EffectUI.prefab");
            _wayGroup = UiKit.FindDeep(_activateBar, "way") as RectTransform;
            _activationDropdown = BindActivateBarDropdown("way",
                ActivationChoices.Select(c => c.label).ToList(), 0);
            _timingDropdown = BindActivateBarDropdown("timing", new List<string> { "—" }, 0);
            _timingRow = UiKit.FindDeep(_activateBar, "timing") as RectTransform;
            // 作用次数选项已整删（2026-10-29 软删 → 2026-10-30 定案无次数上限）：
            // take-effect 组整组隐藏（prefab 节点不动），不再由玩家声明、运行时也不再有闸门。
            _limitGroup = UiKit.FindDeep(_activateBar, "take-effect") as RectTransform;
            if (_limitGroup != null) _limitGroup.gameObject.SetActive(false);
            // P5 代价行已整删（2026-10-30）：activate-bar 不再代码建 cost-row
            // 表切换按钮（原 btn-reload-atoms/btn-load-effects 两钮合并）：按下在 原子库↔效果表 间切换，
            // 标签常显"将切到哪边"。烘焙节点名 btn-switch-table（prefab 接管后删旧两钮）。
            UiKit.Described(UiKit.BindableButton("btn-switch-table", Root, "切换效果表", SwitchTable,
                UiStyle.BtnPrimary, UiStyle.White),
                "切换右栏显示：原子库 ↔ 效果表（效果表里点击一条可载入继续编辑）。");
            _tableToggleLabel = UiKit.FindDeep(Root, "btn-switch-table")?.GetComponentInChildren<TMP_Text>(true);
            _saveBtn = BindButton("btn-save", OnSave);
            _saveBtn?.gameObject.SetActive(false); // 校验全合规才出现（ValidateZones 驱动；Build 先隐藏）
            UiKit.Described(_saveBtn,
                "保存：编辑卡牌时写回这张卡的效果；从效果库进入则存回效果库。"
                + "按钮在必填内容填齐、校验通过后才会出现。");
            // 删除按钮：仅"从效果表载入的库效果"可删（卡编辑/新建态隐藏）——2026-10-24 挪到
            // 右栏「切换效果表」旁；prefab 烘焙 btn-delete 优先
            _deleteBtn = UiKit.BindableButton("btn-delete", Root, "删除", OnDeleteEffect,
                UiStyle.BtnDanger, UiStyle.White);
            _deleteBtn?.gameObject.SetActive(false);
            UiKit.Described(_deleteBtn,
                "删除：删掉效果库里的这条效果（只在编辑效果库条目时可用；编辑卡牌效果的保存不受影响）。");

            // ---- 主体 ----
            _modeBar = Find("mode-bar");
            _slotArea = FindScroll("slot-area");
            PrepareSlotTemplates(); // prefab 烘焙的 slot/settings 样板转隐藏模板（克隆用）

            // 描述条（左栏底部——悬浮/按下控件才出现；2026-10-16 DescTag 悬浮化，校验错误文本共用）
            EnsureDescStrip();

            // 右：展示区（双模式）
            _poolFilter = Find("pool-filter");
            BindPoolFilter(); // 颜色行 chip-{Color} 一次接线（单选，默认「全部」——激活态=Outline）
            _filterName = FindInput("field-filter-name");
            if (_filterName != null)
                _filterName.onValueChanged.AddListener(_ =>
                {
                    RefreshLibrary();
                    if (_rightMode == RightMode.Effects) RefreshEffectsList();
                });

            _libraryList = FindScroll("list-library"); // 2026-10-03 手改预制体重命名（原 library-list）
            _effectsList = FindScroll("list-effects");

            // 2026-10-21 回收式虚拟化：原子表/效果表行池复用（筛选/搜索只重绑可见行——VLG 参数由 Attach 吸收）
            _atomRows = UiKit.RecycledList.Attach(_libraryList)
                ?.Configure(i => _atomItems[i].IsHint ? 1 : 0, AtomRowFactory, BindAtomRow);
            _effectRows = UiKit.RecycledList.Attach(_effectsList)
                ?.Configure(i => _effectItems[i].IsHint ? 1 : 0, EffectRowFactory, BindEffectRow);

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
            Tips.Close(); // 在途提示弹窗清理
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
            // 变形/召唤模板解析器：组合根注入（CardCore 不依赖 UI 层——同 BattleController 口径）。
            // 2026-10-12 补：此前仅战斗/联机/无头接线，合成器费用预览里 SummonToken 模板解析恒
            // null → 召唤计价显示 0；进本屏即接上（幂等——静态委托重复赋同值无副作用）。
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;

            // 卡编辑会话（一次性消费）：进入即改写 _graph 为该效果的深拷贝
            var (card, index) = ComposerSession.Take();
            if (card != null)
            {
                _editingCard = card;
                _editingIndex = index;
                _graph = CardEffectToGraph(index >= 0 && index < card.Effects?.Count ? card.Effects[index] : null,
                    out var note);
                _loadedFromLibraryId = null;
                _presetSourceId = null;
                _presetSnapshotHash = null;
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
                _presetSourceId = null;
                _presetSnapshotHash = null;
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

            // 空库自诊断：正常应 ≥91 行（87+改写仪典 4 行）——为空说明表未装载或初始化半途出错（查 Console）。
            // 2026-10-21 虚拟化后 content.childCount≠条数——读回收列表条数
            int libCount = _atomRows?.Count ?? 0;
            if (libCount == 0)
                ShowToast("原子库为空（表加载失败或初始化异常——查 Console）");
        }

        // ======================================== 元信息（顶栏） ========================================

        private void BuildTimingDropdown()
        {
            if (_timingDropdown == null) return; // 预制体缺节点——BindDropdown 已报，时机编辑不可用
            // 全中文时机下拉（主动三档 Activate_* 不入——主动不设时机；系统自用时点不入
            // ——OnAtomicEffect* 三段/OnSummon 召唤进场/OnPhase* 阶段开始结束/OnGameStart 游戏开始，
            // 2026-10-09 定案不开放玩家选择；**回合开始 2026-10-20 起同样收归系统专用**——
            // 自动抽牌/倒计时递减等系统行为专属，效果时点改挂「准备阶段」）
            //
            // 2026-10-30 定案：系数=该时机的**期望触发次数**（不再是折扣）——下拉尾缀 ×N 与计价同源
            //（TimingCostFactorOf 单口）；系数在 ValueSystemConfig 表 Category=TimingFactor 逐时机行改，
            // 改表即同步这里显示与顶栏费用。一次性时机（×1）排前，分类单源=FiresOncePerPaidLifetime。
            _timings = TimingZh.Keys.Where(t => t != TriggerTiming.OnTurnStart)
                .OrderBy(t => CostDerivationService.FiresOncePerPaidLifetime(t) ? 0 : 1) // LINQ 稳定排序——组内维持表序
                .ToList();
            var labels = _timings
                .Select(t => $"{TimingZh[t]} ×{CostDerivationService.TimingCostFactorOf(t):0.###}")
                .ToList();
            _timingDropdown.SetOptions(labels, 0);
            SyncTimingDropdown();
            _timingDropdown.Changed += (idx, _) =>
            {
                if (idx >= 0 && idx < _timings.Count) _graph.header.TriggerTiming = (int)_timings[idx];
                RefreshName(); // 时机换档改计价——顶栏合计实时重推导
            };
            float onceF = CostDerivationService.TimingCostFactorOf(TriggerTiming.OnPlay);
            float repF = CostDerivationService.TimingCostFactorOf(TriggerTiming.OnDraw);
            _timingDropdown.Describe(
                $"触发时机（自动效果按「付清这张卡后预计触发几次」计价）：一次性时机 ×{onceF:0.###}＝登场/死亡/离场等"
                + $"（弹回重打=新付费）；回合/阶段/抽牌/横置族 ×{repF:0.###}＝按生物存活期望 1.5 回合计；"
                + "攻击/受伤/造成伤害/事件观察族等期望未标定，表内暂定 2——可逐时机调。"
                + "系数在 ValueSystemConfig 表 TimingFactor 段改，改表即同步这里显示的 ×N 与计价。"
                + "自由分支奖励仍限每张卡每回合 1 次；其余效果无次数限制，仅同一效果每回合超过 30 次按自循环截断。点选项看各时机说明。",
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
                    : "主动：由你手动发动——发动时才支付费用并横置这张卡。");
            if (_activationDropdown != null)
                _activationDropdown.Changed += (idx, _) =>
                {
                    if (idx >= 0 && idx < ActivationChoices.Length)
                    {
                        // 宿主形状闸（2026-10-15 深夜改版）：法术只挂主动、结界禁主动——非法选择弹回不改值
                        var pick = ActivationChoices[idx];
                        // 主干发动方式钉（2026-10-30）：模拟/变形仅主动、倒计时/元素充盈仅自动——非法选择弹回
                        var pin = EngineActivationPin();
                        if (pin != null && pick.value != (int)pin.Value)
                        {
                            ShowToast(pin.Value == EffectActivationType.Voluntary
                                ? "该主干仅主动（模拟/变形=主动效果）"
                                : "该引擎仅自动（倒计时/元素充盈=固定自动）");
                            SyncActivationVisibility(); // 弹回当前合法值
                            return;
                        }
                        if (HostForbidsVoluntary && pick.value == (int)EffectActivationType.Voluntary)
                        {
                            ShowToast("结界只挂自动/光环——主动效果已随 2026-10-15 改版退役");
                            SyncActivationVisibility(); // 弹回当前合法值
                            return;
                        }
                        if (HostRequiresVoluntary && pick.value != (int)EffectActivationType.Voluntary)
                        {
                            ShowToast("法术只挂主动效果（施放时弹窗抉择其一）");
                            SyncActivationVisibility();
                            return;
                        }
                        _graph.header.ActivationType = pick.value;
                    }
                    SyncActivationVisibility();
                };
        }

        /// <summary>宿主形状谓词（2026-10-15 深夜三类卡改版）：结界禁主动（只挂自动/光环）、
        /// 法术只挂主动（1~3 个，施放时弹窗抉择其一）。效果库模式（无宿主）两态全开。</summary>
        private bool HostForbidsVoluntary
            => _editingCard != null && _editingCard.Supertype == Cardtype.Enchantment;

        private bool HostRequiresVoluntary
            => _editingCard != null && _editingCard.Supertype == Cardtype.Spell;

        /// <summary>宿主禁挂连接光环（改版配套）：生物（连接光环专属结界）与法术（只挂主动效果）。</summary>
        private bool HostBlocksAura
            => _editingCard != null && _editingCard.Supertype != Cardtype.Enchantment;

        /// <summary>发动方式/时机的可见性与档位锁定（2026-10-05 两槽定案）：
        /// 并列=主动/自动两态全开（主动档时机钉 Activate_*、离开回落 OnPlay——2026-10-02 定案不变；
        /// 分支钉强制档已删——分支时机由槽级 branch 载荷承载，不再锁效果头）；
        /// 光环=无时机/方式（载体入场即生效，离场/被无效即失效）——时机钉 OnPlay、隐藏两下拉。</summary>
        private void SyncActivationVisibility()
        {
            if (_timingRow == null && _activationDropdown == null) return;
            bool aura = IsAuraMode(_mode);
            var h = _graph.header;

            // 宿主形状归一（2026-10-15 深夜改版——载入旧数据/切换宿主时兜底）：
            // 结界禁主动（钉回自动）；法术只挂主动（钉 Voluntary+Activate_*）。
            if (!aura)
            {
                if (HostForbidsVoluntary && h.ActivationType == (int)EffectActivationType.Voluntary)
                    h.ActivationType = (int)EffectActivationType.Automatic;
                if (HostRequiresVoluntary && h.ActivationType != (int)EffectActivationType.Voluntary)
                    h.ActivationType = (int)EffectActivationType.Voluntary;
            }

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
        }

        /// <summary>发动行显隐（2026-10-05 定案；2026-10-09 activate-bar 迁移；速度组 2026-10-15 退役；
        /// **生效次数组 2026-10-30 定案整删**——take-effect 常驻隐藏、不再写盘；
        /// P5 代价行 2026-10-30 整删——本口暂无逐行显隐项，保留联动锚点）。</summary>
        private void SyncSettingsRowVisibility()
        {
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

        // 生效次数下拉已整删（2026-10-29 P9 软删；2026-10-30 定案：效果无每回合生效次数上限——
        // 运行时无闸门、计价无次数档，唯一保留的每回合 1 次是自由分支引擎奖励〔BranchEngines 自限〕）。

        // ======================================== 主动效果代价行（2026-10-29 P5 建 · 2026-10-30 用户定案整删） ========================================
        // cost-row 代码建组（标签+下拉+参数输入）与候选/回显/写入链全部退役——效果组合界面不再编辑
        // 效果级代价；header.Costs 存量数据保留（运行时净费闸照常消费），顶栏「代价→净费」段照显。

        /// <summary>P5 代价段文案（主动效果带代价行才非空）：「代价（自伤）：弃牌 1（价2）→ 净费 0.5绿
        ///（实付 1绿）」——发动费在合计段（前缀），此处接抵扣与净费；净费带小数时尾注逐色进1后的
        /// 对战实付（2026-10-30 支付进1定案）；空行不显示。</summary>
        private static string EffectCostFeeText(EffectDefinition def)
        {
            if (def == null || !def.IsActivatedEffect) return null;
            var special = def.Costs != null
                ? def.Costs.Where(c => c != null && c.Type != CostType.ElementConsume).ToList()
                : null;
            if (special == null || special.Count == 0) return null;
            int pts = EffectActivationGate.DeductionPoints(special);
            if (pts <= 0) return null;
            var net = EffectActivationGate.NetBill(CostDerivationService.DeriveElementCosts(def, 0), special);
            var atom = special.SelectMany(c => c.Payloads ?? new List<CardCore.AtomicEffectInstance>())
                .FirstOrDefault(a => a != null);
            string arm = atom != null && atom.Polarity < 0f ? "自伤" : "资敌";
            string name = atom != null
                ? (AtomicEffectTable.GetByType(atom.Type)?.DisplayName ?? atom.Type.ToString())
                : "代价";
            int val = atom != null ? Math.Max(1, atom.Value) : 1;
            var netText = $"净费 {CostZh(net)}";
            var netPay = PayZh(net);
            if (netPay != CostZh(net)) netText += $"（实付 {netPay}·进1）";
            return $"代价（{arm}）：{name} {val}（价{pts}）→ {netText}";
        }

        // ======================================== 效果名自动构成 ========================================

        /// <summary>自动名（2026-10-05 两槽定案）：并列=逐槽 AtomText.RenderAtomEntry+BranchSuffix 拼接
        ///（空槽不占位）；光环=条目列表×箭头数；规则级光环=仪典名（双方生效·全局唯一，无箭头）。
        /// FreeBranch/OutcomeGate 专属文案分支已随形态删除。</summary>
        private string AutoName()
        {
            var h = _graph.header;
            if (IsAuraMode(_mode))
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
                        const string Prefix = "场地光环：";
                        if (desc.StartsWith(Prefix)) desc = desc.Substring(Prefix.Length);
                        ruleParts.Add($"场地光环：【{scopeZh}】{desc}");
                    }
                    else ruleParts.Add($"【{scopeZh}】{rc.Description}");
                }
                if (ruleParts.Count > 0) return string.Join("；", ruleParts);

                // 连接条目（2026-10-07 句式；2026-10-12 改稿）：作用面条目=【范围】前缀+「受光环影响的…」
                //（关键词条目含角色尾注）；连接方向档=「×N箭头，箭头指向的…」——箭头数前置，主语换成指向者
                var scopedParts = new List<string>();
                var arrowParts = new List<string>();
                foreach (var a in h.LinkAuras ?? new List<LinkAuraData>())
                {
                    if (a == null) continue;
                    string subject = a.scope > 0 ? "受光环影响的" : "箭头指向的";
                    string body;
                    if (!string.IsNullOrEmpty(a.stat))
                        body = $"{subject}生物{StatZhOf(a.stat)}{a.value:+0;-0}";
                    else if (!string.IsNullOrEmpty(a.keyword))
                        body = $"{subject}单位获得{KeywordZh(a.keyword)}"; // 关键词条目恒含角色（2026-10-09 裁定）
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
                        all.Add(n > 0 ? $"×{n}箭头，{string.Join("，", arrowParts)}" : string.Join("，", arrowParts));
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
            return IsAuraMode(_mode) ? AutoName()
                : (string.IsNullOrEmpty(full) ? AutoName() : full);
        }

        private void RefreshName()
        {
            _nameLabel.text = CurrentDisplayName();
            // 2026-10-30 区域扩容改版：顶栏=徽标＋多行明细（构成/合计+实付/代价→净费）；悬浮描述条留完整构成
            if (_costLabel != null)
            {
                _costLabel.text = CostSummaryText();
                var costTag = _costLabel.GetComponent<UiKit.DescTag>();
                if (costTag != null) costTag.Text = EffectCostText();
            }
            if (_costBadgeView != null)
            {
                var badgeCost = CostBadgeCost();
                bool hasCost = badgeCost != null && !badgeCost.IsZero;
                if (hasCost) _costBadgeView.SetCosts(badgeCost);
                else _costBadgeView.Clear();
                if (_costBadgeNode != null) _costBadgeNode.gameObject.SetActive(hasCost);
            }
        }

        /// <summary>费用完整构成（2026-10-23 改版：不再直显顶栏——悬浮费用区经描述条单行滚动展开）：
        /// 并列=主干段（参数合规才显示：效果锚价完整构成——逐原子贡献＋合计＋黑白获得）
        /// ＋分支段（2026-10-09 定案：只声明「分支（奖励免费）」——奖励锚价/上限不随段列出，
        /// 超上限提示由槽区校验红框承载、保存已禁；分支段独立于主干校验恒显——超限时主干段隐藏，
        /// 分支段在案提示）；光环=条目平价直列（2026-10-12：箭头费入条目价、预览卡携带预选箭头，
        /// 无「费用（光环条目）：」冠语）；主干校验违规/转换失败/推导异常→对应段空串。</summary>
        private string EffectCostText()
        {
            try
            {
                if (IsAuraMode(_mode)) return AuraTopCostText();
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

        /// <summary>顶栏费用多行明细（2026-10-30 区域扩容改版：一行摘要→分行直显，2026-10-23 缩进
        /// 悬浮的逐原子公式链随区域扩容回迁顶栏）：
        /// ① 构成行=「费用：逐原子 基准×系数链＝贡献」（ParallelCostBreakdown 单源）；
        /// ② 合计行=合计真值（真实小数）＋对战实付（逐色进1——整数费两值同则省略）＋分支段；
        /// ③ 代价行=「代价（臂）：名 值（价P）→ 净费 X（实付 Y·进1）」（带代价行才显）。
        /// 光环=「合计 X（N 条目）」（光环价入卡价非对战现付——不附实付段）；转换失败只留分支段。
        /// 完整构成仍留悬浮描述条（EffectCostText——与顶栏同源不冲突）。</summary>
        private string CostSummaryText()
        {
            try
            {
                if (IsAuraMode(_mode))
                {
                    var (auraTotal, n) = AuraCostTotal();
                    return n > 0 ? $"合计 {CostZh(auraTotal)}（{n} 条目）" : "";
                }
                string branch = BranchCostText();
                if (!ZonesAllValid()) return branch;
                var (body, total, def) = ParallelCostBreakdown();
                if (def == null) return branch; // 转换失败=旧口径：只留分支段
                var lines = new List<string>();
                if (body.Length > 0) lines.Add(body); // ① 构成行（逐原子公式链）
                var sumLine = $"合计 {CostZh(total)}"; // ② 合计行：真值＋对战实付＋分支
                var pay = PayZh(total);
                if (pay != CostZh(total)) sumLine += $"｜对战实付 {pay}（小数逐色进1）";
                if (branch.Length > 0) sumLine += "｜" + branch;
                lines.Add(sumLine);
                var costSeg = EffectCostFeeText(def); // ③ 代价→净费行（存量代价数据照显）
                if (!string.IsNullOrEmpty(costSeg)) lines.Add(costSeg);
                return string.Join("\n", lines);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>费用徽标费用（2026-10-26 改版：逐色 ElementCost 喂 CostSquares——口径与旧合计数值同源：
        /// 光环=条目合计；并列=推导合计）。无效组合/无费用=null——徽标隐藏。</summary>
        private ElementCost CostBadgeCost()
        {
            try
            {
                if (IsAuraMode(_mode))
                {
                    var (total, n) = AuraCostTotal();
                    return n > 0 ? total : null;
                }
                if (!ZonesAllValid()) return null;
                var def = CardEffectConverter.ConvertOne(BuildCardEffect(), "COMPOSER_COST_PREVIEW");
                if (def == null) return null;
                var badge = CostDerivationService.DeriveElementCosts(def, 0);
                // P5（2026-10-29）：带代价行的主动效果——徽标显示净费（发动费−代价抵扣＝实际支付）
                var special = def.Costs != null
                    ? def.Costs.Where(c => c != null && c.Type != CostType.ElementConsume).ToList()
                    : null;
                if (def.IsActivatedEffect && special != null && special.Count > 0
                    && EffectActivationGate.DeductionPoints(special) > 0)
                    badge = EffectActivationGate.NetBill(badge, special);
                return badge;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>光环条目费用合计（Stage A 明细按色累加；n=条目行数——AuraTopCostText 同源）。</summary>
        private (ElementCost total, int n) AuraCostTotal()
        {
            var lines = CardCore.CardCostService.Derive(AuraPreviewCard()).Breakdown
                .Where(l => l != null && l.Stage == "A").ToList();
            var total = new ElementCost();
            foreach (var l in lines)
            {
                if (l.Color.HasValue) total[l.Color.Value] += l.Value;
                else total[(int)ManaType.Gray] += l.Value;
            }
            return (total, lines.Count);
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
        /// （与 RewardDerivedCost 同手法）。逐原子 shim 各自为真值（2026-10-30 真实小数定案：
        /// 合成期不再逐色取整），分账加总与合计允许浮点尾差——合计行才是真值。
        /// 2026-10-10 参与变量写全：逐原子不再只报数——展开「基准×系数链＝结果」
        ///（AtomCostFormula 与 ComputeAtomCost 同批判定原语，如 造成伤害5：基准1红×5×速度档2＝10红）。</summary>
        private string ParallelCostText()
        {
            var (body, total, _) = ParallelCostBreakdown();
            return body.Length == 0 ? "" : body + $"｜合计 {CostZh(total)}";
        }

        /// <summary>并列费用分账单源（ParallelCostText 悬浮全文与顶栏明细 CostSummaryText 共用）：
        /// body=「费用：逐原子公式链」（空组合=""）；total=合计真值（真实小数）；
        /// def=装载后效果定义（代价段 EffectCostFeeText 消费——省一次重复 ConvertOne）。</summary>
        private (string body, ElementCost total, EffectDefinition def) ParallelCostBreakdown()
        {
            var def = CardEffectConverter.ConvertOne(BuildCardEffect(), "COMPOSER_COST_PREVIEW");
            if (def == null) return ("", new ElementCost(), null);
            var total = CostDerivationService.DeriveElementCosts(def, 0);

            var parts = new List<string>();
            foreach (var atom in BillableAtomsOf(def))
            {
                var shim = PricingShim(def, atom);
                var atomCost = CostZh(CostDerivationService.DeriveElementCosts(shim, 0));
                var (baseExpr, factors) = CostDerivationService.AtomCostFormula(atom, shim,
                    CostDerivationService.PricingVisitDomain(shim, atom));
                var chain = factors.Count == 0 ? baseExpr : baseExpr + "×" + string.Join("×", factors);
                // 引擎附加费（附加诅咒/祝福分支减半入价）非乘法链——尾注展开，防「公式≠结果」
                var b = atom.Branch;
                if (b != null && b.Settle == BranchSettleKind.Engine
                    && (b.EngineKind == BranchEngineKind.CurseOnDraw || b.EngineKind == BranchEngineKind.BlessingOnDraw))
                {
                    float half = CostDerivationService.EngineBranchSurchargeHalf(b.Then);
                    if (half > 0f) chain += $"＋附加减半{half:0.#}";
                }
                parts.Add($"{AtomPartLabel(atom)}：{chain}＝{atomCost}");
            }
            // 黑白获得尾注已删（2026-10-28 P1：错边/代价不再产出元素——DeriveElementGrants 退役）
            return (parts.Count == 0 ? "" : "费用：" + string.Join("＋", parts), total, def);
        }

        /// <summary>计费原子遍历（镜像 CostDerivationService.VisitBillableAtoms 主序列口径——
        /// 主干原子；分支 Then 奖励免费不列——分支段另见 BranchCostText。抉择首模式扫描已随
        /// 2026-10-31 P12 抉择退役删除——converter 不再生成 Choice 步）。</summary>
        private static IEnumerable<AtomicEffectInstance> BillableAtomsOf(EffectDefinition def)
        {
            if (def.Steps != null && def.Steps.Count > 0)
            {
                foreach (var step in def.Steps)
                {
                    if (step == null) continue;
                    if (step.Kind == RuntimeStepKind.Atomic && step.Atomic != null)
                        yield return step.Atomic;
                }
            }
            else if (def.Effects != null)
            {
                foreach (var atom in def.Effects)
                    if (atom != null) yield return atom;
            }
        }

        /// <summary>单原子计价 shim：克隆影响计价的全部效果头字段（持续/目标数量/选择模式/
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

        /// <summary>费用文本化：非零色按序拼「N色」（如 2红1蓝；全零=0）。
        /// 2026-10-12 小数直通：效果级预览按真值显示小数（0.6绿/3.6红——整数不带小数位）；
        /// 卡层合成价由 CardCostService L3 整卡取整，不经此口。</summary>
        private static string CostZh(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            var parts = new List<string>();
            foreach (var color in cost.NonzeroColors())
                parts.Add($"{cost[color]:0.##}{ColorZh(color)}");
            return string.Join("", parts);
        }

        /// <summary>对战实付文本（2026-10-30 支付进1定案）：费用经支付终点 NormalizeBill
        /// 逐色向上取整后的实际扣费账单（大于 0 也消耗 1）——与引擎实付同单源。</summary>
        private static string PayZh(ElementCost cost)
        {
            var bill = ElementPaymentValidator.NormalizeBill(cost);
            var c = new ElementCost();
            foreach (var kv in bill) c[kv.Key] = kv.Value;
            return CostZh(c);
        }

        /// <summary>光环模式顶栏费用：条目平价（Stage A 明细，AuraPreviewCard 同源——
        /// 2026-10-07 面板 aura-cost 行退役后顶栏独占；2026-10-12 删「费用（光环条目）：」
        /// 冠语——行首直入条目公式；箭头费已入条目价，预览卡携带预选箭头同计）。</summary>
        private string AuraTopCostText()
        {
            var lines = CardCore.CardCostService.Derive(AuraPreviewCard()).Breakdown
                .Where(l => l != null && l.Stage == "A").ToList();
            if (lines.Count == 0) return "";
            var parts = lines.Select(l =>
                $"{l.Label}＝{l.Value:0.#}{(l.Color.HasValue ? ColorZh(l.Color.Value) : "")}");
            return string.Join("；", parts);
        }

        private static string ColorZh(ManaType m) => m switch
        {
            ManaType.Red => "红",
            ManaType.Blue => "蓝",
            ManaType.Green => "绿",
            ManaType.Gray => "灰",
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

        /// <summary>删除按钮显隐（2026-10-24 改版）：仅"从效果表载入的库效果"显示——
        /// 按钮已挪到右栏「切换效果表」旁，卡编辑/新建态直接隐藏。</summary>
        private void UpdateDeleteState()
        {
            if (_deleteBtn != null)
            {
                bool loaded = _editingCard == null && !string.IsNullOrEmpty(_loadedFromLibraryId);
                _deleteBtn.gameObject.SetActive(loaded);
                _deleteBtn.interactable = loaded;
            }
        }

        /// <summary>删除当前载入的库效果（btn-delete）：按源 id 清档后重置为新建空效果。
        /// 2026-10-16 强制删除机制：被卡引用时 TipPanel 提示+允许强制（ReferenceAudit 反查，
        /// 2026-10-18 弹窗统一）——强删后引用卡装载时该效果被跳过（卡组合界面卡池行标记⚠断链）。
        /// 不重建两下拉——防 Changed 重复订阅（Build*Dropdown 只在进屏时挂一次）。</summary>
        private void OnDeleteEffect()
        {
            if (_editingCard != null || string.IsNullOrEmpty(_loadedFromLibraryId)) return;
            var removed = _loadedFromLibraryId;
            var refs = ReferenceAudit.CardsReferencingEffect(removed);
            if (refs.Count > 0)
            {
                ConfirmForceDeleteEffectAsync(removed, refs).Forget();
                return;
            }
            DeleteEffectFromLibrary(removed, 0);
        }

        /// <summary>强删确认（TipPanel 双键）：取消/拆屏=null/否=不动。</summary>
        private async UniTaskVoid ConfirmForceDeleteEffectAsync(string removed, List<string> refs)
        {
            if (await Tips.ShowAsync(
                    $"效果 {removed}（{_graph?.name}）被 {refs.Count} 张卡引用：\n{string.Join("、", refs)}\n\n"
                    + "强制删除后这些卡的效果引用断链（卡组合界面卡池行会标记⚠，卡装载时该效果被跳过）。",
                    "强制删除", "取消") != true)
                return;
            DeleteEffectFromLibrary(removed, refs.Count);
        }

        /// <summary>保存替换旧版效果的去留询问（TipPanel 二选一，fire-and-forget）：删除旧版=引用卡
        /// 断链（同删效果口径）；保留旧版=新旧共存。null（拆屏/顶替）=按保留处理不提示——数据口径
        /// 与保留一致（旧版留存），只是没有确认 toast。</summary>
        private async UniTaskVoid HandleReplacedLegacyAsync(string oldId, List<string> refs)
        {
            var picked = await Tips.ShowAsync(
                $"旧版 {oldId} 被 {refs.Count} 张卡引用：{string.Join("、", refs)}。\n\n"
                + "「保留旧版」=新旧共存，引用卡继续用旧版；「删除旧版」=引用卡断链（装载时该效果被跳过）。",
                "删除旧版", "保留旧版");
            if (picked == true)
            {
                EffectLibrarySerializer.DeleteById(oldId);
                ShowToast($"旧版 {oldId} 已删——{refs.Count} 张卡引用断链（卡组合界面已标记⚠）");
                if (_rightMode == RightMode.Effects) RefreshEffectsList();
            }
            else if (picked == false)
                ShowToast($"已保留旧版 {oldId}（{refs.Count} 张卡仍引用旧版）");
        }

        private void DeleteEffectFromLibrary(string removed, int brokenCards)
        {
            EffectLibrarySerializer.DeleteById(removed);
            _loadedFromLibraryId = null;
            _presetSourceId = null;
                _presetSnapshotHash = null;
            _graph = CardEffectToGraph(null, out _);
            _mode = InferMode(_graph);
            _expandedAtoms.Clear();
            _expandedRewards.Clear();
            EnsureDefaultSelection();
            SyncTimingDropdown();
            SyncActivationVisibility();
            RefreshAll();
            SetRightMode(RightMode.Atoms);
            ShowToast(brokenCards > 0
                ? $"效果 {removed} 已删——{brokenCards} 张卡引用断链（卡组合界面已标记⚠）"
                : $"效果 {removed} 已从效果库删除");
        }

        private void RefreshModeBar()
        {
            if (_modeBar == null) return;
            // 不清重建：模式片按名绑定烘焙节点（mode-Parallel/FreeBranch/LimitedBranch/LinkAura/RangeAura）；
            // 芯片文案以预制体为准（2026-10-09 定案：代码不覆写 UI 设置好的文本——BindableButton 传 null
            // 跳过回写），刷新只更新态色/接线；缺节点 BindableButton 已 LogError 跳过（兜底构建全退）
            MakeModeChip(ComposeMode.Parallel,
                "并列形态：最多两个槽，各放一颗原子组成效果，两原子并列生效——要给原子挂条件或引擎请切分支形态");
            MakeModeChip(ComposeMode.FreeBranch,
                "自由分支：主干是一颗事件引擎（倒计时/拼点/运势等），事件达成时发放下方 Then 奖励——奖励在 1-5 费区间自由调整，门槛随计费推导；"
                + "模拟/变形也在此形态作主干（仅主动；引用卡与发动时机留到卡牌合成界面设置）");
            MakeModeChip(ComposeMode.LimitedBranch,
                "有限分支：一颗主干原子加一个局面门槛，当前局面达标时发放 Then 奖励——不达标不奖励也不惩罚（奖励免费）");
            MakeModeChip(ComposeMode.LinkAura,
                "连接光环：只放增益条目（属性/关键词），效果挂在连接箭头指向的单位上——箭头必选（至少一支），在光环槽底部选");
            MakeModeChip(ComposeMode.RangeAura,
                "场地光环：挂一条仪典或属性降低条目持续生效——仪典同名全局唯一；作用面随极性钉死（增益仅己方、减益仅对方），属性降低双方可叠加");

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

        /// <summary>形态切换（2026-10-23 五分类，best-effort 数据搬运）：
        /// →并列：保留前 2 原子，拆分支载荷（奖励随之丢弃）；
        /// →自由分支：保留首个引擎行（无则清空提示先选引擎主干），奖励随载荷保留；
        /// →有限分支：保留首个非引擎原子，门槛/奖励保留（引擎载荷不跟普通原子）；
        /// →连接光环：清 steps（仪典步不入）、箭头重选（光环字段只在光环形态存续）；
        /// →规则光环：清 LinkAuras/箭头，保留仪典步。切换后收起展开卡。</summary>
        private void SwitchMode(ComposeMode newMode)
        {
            if (newMode == _mode) return;
            var h = _graph.header;
            _graph.steps ??= new List<EffectStepData>();

            switch (newMode)
            {
                case ComposeMode.Parallel:
                {
                    // 保留前 2 原子，拆分支载荷（奖励随之丢弃——toast 明示）
                    var kept = new List<EffectStepData>();
                    int seen = 0;
                    foreach (var s in _graph.steps)
                    {
                        if (s?.kind == 0)
                        {
                            if (seen >= ParallelSlotCount) continue;
                            seen++;
                            if (s.atomic != null) s.atomic.branch = null;
                        }
                        kept.Add(s);
                    }
                    _graph.steps.Clear();
                    _graph.steps.AddRange(kept);
                    h.AtomicEffects = null; // 扁平/奖励通道退役——防夹带
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
                    ShowToast("切换为并列——已保留前两颗原子，分支载荷与 Then 奖励已丢弃");
                    break;
                }
                case ComposeMode.FreeBranch:
                {
                    h.AtomicEffects = null;
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
                    var engine = _graph.steps.FirstOrDefault(s => s?.kind == 0 && s.atomic != null
                        && ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(s.atomic.refId)));
                    _graph.steps.Clear();
                    if (engine != null)
                    {
                        _graph.steps.Add(new EffectStepData { kind = 0, atomic = engine.atomic });
                        ShowToast("切换为自由分支——已保留引擎主干与 Then 奖励");
                    }
                    else ShowToast("自由分支主干=引擎行——无可保留主干已清空，先到右侧选引擎主干");
                    break;
                }
                case ComposeMode.LimitedBranch:
                {
                    h.AtomicEffects = null;
                    h.ArrowDirections = 0;
                    h.LinkAuras = null;
                    int dropped = 0;
                    EffectStepData trunk = null;
                    foreach (var s in _graph.steps)
                    {
                        if (s?.kind != 0 || s.atomic == null
                            || ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(s.atomic.refId)))
                        {
                            if (s != null) dropped++;
                            continue;
                        }
                        trunk = s;
                        break;
                    }
                    _graph.steps.Clear();
                    if (trunk != null)
                    {
                        if (trunk.atomic.branch != null
                            && (BranchSettleKind)trunk.atomic.branch.settle == BranchSettleKind.Engine)
                            trunk.atomic.branch = null; // 引擎载荷不跟普通原子
                        _graph.steps.Add(trunk);
                        ShowToast(trunk.atomic.branch != null
                            ? $"切换为有限分支——主干/门槛/奖励已保留" + (dropped > 0 ? $"（丢弃 {dropped} 步）" : "")
                            : $"切换为有限分支——主干已保留，门槛未选（点门槛卡到右栏选择）" + (dropped > 0 ? $"（丢弃 {dropped} 步）" : ""));
                    }
                    else ShowToast("有限分支主干=普通原子——无可保留主干，先到右侧选一颗原子");
                    break;
                }
                case ComposeMode.LinkAura:
                {
                    // 连接光环（效果层定案）＝箭头+增益条目随效果合成；仪典步不入（切场地光环再配）；
                    // 属性降低条目属场地光环（2026-10-30 挪入）——切入即清，只留增益/关键词条目
                    h.AtomicEffects = null;
                    h.ArrowDirections = 0; // 切入箭头恒 0——连接方向的光环在光环槽底部选箭头
                    _graph.steps.Clear();
                    if (h.LinkAuras != null)
                        h.LinkAuras = h.LinkAuras.Where(a => a != null
                            && (string.IsNullOrEmpty(a.stat) || a.value > 0)).ToList();
                    ShowToast("连接光环：只放增益条目（属性/关键词）——右侧光环库点击换选（新选即替换上一个）；箭头在光环槽底部必选");
                    break;
                }
                case ComposeMode.RangeAura:
                {
                    // 场地光环＝仪典步/属性降低条目专用面：箭头清空、增益·关键词条目清空（属连接光环），
                    // 仪典步与负属性条目保留（2026-10-30 属性降低入场地光环）
                    h.AtomicEffects = null;
                    h.ArrowDirections = 0;
                    if (h.LinkAuras != null)
                        h.LinkAuras = h.LinkAuras.Where(a => a != null
                            && !string.IsNullOrEmpty(a.stat) && a.value < 0).ToList();
                    _graph.steps.RemoveAll(s => s == null || s.kind != 0 || s.atomic == null
                        || !IsRuleAuraRow(AtomicEffectTable.GetByHashId(s.atomic.refId)));
                    ShowToast("场地光环：右侧仪典库/属性降低条目点击选择（新选即替换；属性降低双方可叠加）");
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
                    // 并列（2026-10-23 五分类回归纯并列）：两槽原子、无分支——分支两形态独立成 chip
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
                                MakeAtomCard(slot, step.atomic, idx, SelKind.Parallel);
                            else MakeReadonlyStepBadge(slot, step); // 抉择等只读
                        }
                        else
                        {
                            // 空槽占位引导（2026-10-23 改版）：空槽卡区内常驻一行「从右侧选原子填入」
                            if (i == _graph.steps.Count) firstEmptySlot ??= slot;
                            MakeHint(slot, "从右侧选原子填入");
                        }
                    }
                    if (_graph.steps.Count >= ParallelSlotCount)
                        MakeHint(_slotArea.Content, "已满 2 原子——点击槽选中后，右侧点击即替换");
                    // 校验：并列模式至少一个原子（只读抉择步计入内容——卡组成阶段产物）
                    if (firstEmptySlot != null)
                        _slotZones.Add(MakeZone(firstEmptySlot, () =>
                            !_graph.steps.Any(s => s != null && ((s.kind == 0 && s.atomic != null) || s.kind == 2))
                                ? "效果为空——并列模式至少需要一个原子（选中槽后点击右侧库行）" : null));
                    // 限重复（2026-10-09 全局唯一 → 2026-10-19 放开修订：仅引擎主干/仪典）：
                    // 库已隐不可再添，本区仅拦载入旧数据/旁路（隐藏交互、不拒绝）
                    _slotZones.Add(MakeZone(_slotArea.Content, DuplicateAtomError));
                    break;
                }
                case ComposeMode.FreeBranch:
                {
                    // 自由分支：单主干槽（引擎行 / 模拟·变形动态主干）+ 槽内 Then 奖励块（引擎预设锁效果头）
                    var slot = MakeSlot("主干（引擎行/模拟·变形）");
                    if (slot == null) break; // 模板缺失——MakeSlot 已报
                    MarkSelected(slot, SelKind.Parallel, 0);
                    SelectOnClick(slot, SelKind.Parallel, 0, false);
                    var atom = FirstKind0Atom();
                    if (atom != null)
                    {
                        MakeAtomCard(slot, atom, 0, SelKind.Parallel);
                        if (ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(atom.refId)))
                        {
                            var engineKind = ComposerCatalog.EngineKindOf(ResolveType(atom));
                            if (engineKind == BranchEngineKind.None)
                            {
                                // 动态主干（2026-10-30 模拟/变形挪入自由分支·均仅主动）：真实引擎种类为 None——
                                // 不挂引擎载荷/无 Then 奖励块（无引擎事件可发奖）；运行时按普通原子执行
                                //（EffectExecutionEngine 只跳 EngineKindOf≠None 的行）。
                                // 归属定案（2026-10-30 用户修正）：引用卡与发动时机都在卡牌合成界面设置
                                //（选中后显示卡牌列表，不带衍生物的卡均可选；卡合成改版时落地——
                                // 本屏不为其做专属 UI，暂沿用通用效果头缺省）
                                atom.branch = null;
                                MakeHint(slot, "动态主干（模拟/变形·仅主动）：发动时执行——引用卡不在此填，"
                                    + "在卡牌合成界面选择（选中后显示卡牌列表，不带衍生物的卡均可选）；发动时机同样迁至卡牌合成界面设置");
                            }
                            else
                            {
                                var b = atom.branch ?? new BranchEntryData { settle = (int)BranchSettleKind.Engine };
                                b.settle = (int)BranchSettleKind.Engine; // 引擎载荷落槽即挂（载入/换行双保险）
                                if (engineKind != BranchEngineKind.None) b.engine = (int)engineKind;
                                atom.branch = b;
                                MakeHint(slot, $"{ComposerCatalog.EngineZhOf(engineKind)}门槛=Then 奖励锚价合计"
                                    + "（随奖励实时推导，无需参数；奖励在 1-5 费区间内自由调整，门槛随计费变；"
                                    + "同一张卡每回合只触发一次）"
                                    + $"——见顶栏预览「自由分支·{ComposerCatalog.EngineZhOf(engineKind)}x」");
                                MakeRewardSlot(slot, b);
                            }
                        }
                        else
                        {
                            atom.branch = null; // 引擎载荷只配引擎主干行（旧形状静默归位）
                            MakeHint(slot, "该行不是引擎主干——到右侧重新选择（自由分支主干=引擎行/模拟/变形）");
                        }
                    }
                    else MakeHint(slot, "主干为空——到右侧选择引擎行（倒计时/拼点/运势/死亡计数/元素充盈…）或模拟/变形");
                    _slotZones.Add(MakeZone(slot, () =>
                    {
                        var a = FirstKind0Atom();
                        if (a == null) return "主干为空——到右侧选择引擎主干行（或模拟/变形）";
                        return ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(a.refId))
                            ? null : "自由分支主干必须是引擎行（或模拟/变形）——到右侧重新选择";
                    }));
                    break;
                }
                case ComposeMode.LimitedBranch:
                {
                    // 有限分支：单主干槽（普通原子）+ 门槛卡（点选）+ 槽内 Then 奖励块
                    var slot = MakeSlot("主干（原子槽 1）");
                    if (slot == null) break; // 模板缺失——MakeSlot 已报
                    MarkSelected(slot, SelKind.Parallel, 0);
                    SelectOnClick(slot, SelKind.Parallel, 0, false);
                    var atom = FirstKind0Atom();
                    if (atom != null)
                    {
                        MakeAtomCard(slot, atom, 0, SelKind.Parallel);
                        if (atom.branch != null
                            && (BranchSettleKind)atom.branch.settle == BranchSettleKind.Engine)
                            atom.branch = null; // 引擎载荷不配普通原子（切形态残留兜底）
                    }
                    else MakeHint(slot, "主干为空——到右侧选择一颗普通原子（引擎行请切「自由分支」形态）");
                    MakeGateCard(slot, atom);
                    var lb = atom?.branch;
                    if (lb != null && !BranchEntryRules.IsPhantom(lb)) MakeRewardSlot(slot, lb);
                    _slotZones.Add(MakeZone(slot, () =>
                    {
                        var a = FirstKind0Atom();
                        if (a == null) return "主干为空——到右侧选择一颗普通原子";
                        return ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(a.refId))
                            ? "有限分支主干须为普通原子——引擎行请切「自由分支」形态" : null;
                    }));
                    break;
                }
                case ComposeMode.LinkAura:
                case ComposeMode.RangeAura:
                {
                    MakeAuraPanel(_slotArea.Content);
                    break;
                }
            }

            // 设置盒已随 2026-10-09 prefab 重排拆除：发动四参数=activate-bar 静态组（Build 一次绑定），
            // 数量/随机/衍生物=每原子卡面行（MakeAtomCard/MakeRewardSlot 逐卡绑定）
            ApplyChargeHeaderLock(); // 充能在案→主动/主动钉档/速度 0（P12，先于引擎锁——末尾控件同步取终值）
            ApplyEngineHeaderLock(); // 引擎在案→效果头四值钉预设+四控件禁用（2026-10-09 九项定案，含载入/换引擎路径）
            RefreshName();
            ValidateZones(); // 槽区重建后立即校验
        }

        // ======================================== 充能形态（2026-10-31 P12：建造→充能） ========================================

        /// <summary>图内是否含充能原子（主序列/奖励位扫描——ChargeShapeError/ApplyChargeHeaderLock 共用）。</summary>
        private static bool GraphHasChargeAtom(EffectGraphData g)
        {
            if (g?.steps == null) return false;
            foreach (var s in g.steps)
            {
                if (s == null) continue;
                if (ComposerCatalog.IsChargeRow(AtomicEffectTable.GetByHashId(s.atomic?.refId))) return true;
                if (s.thenSteps != null)
                    foreach (var a in s.thenSteps)
                        if (ComposerCatalog.IsChargeRow(AtomicEffectTable.GetByHashId(a?.refId))) return true;
            }
            return false;
        }

        /// <summary>充能效果头钉档（P12）：图内含充能原子 → 主动 / Activate_Active / 速度 0
        ///（CardEffectConverter 同口径收口——此处编辑期先行，防合成器缺省值漂移；充能图无引擎，两锁不冲突）。</summary>
        private void ApplyChargeHeaderLock()
        {
            var h = _graph?.header;
            if (h == null || !GraphHasChargeAtom(_graph)) return;
            h.ActivationType = (int)EffectActivationType.Voluntary;
            h.TriggerTiming = (int)TriggerTiming.Activate_Active;
            h.BaseSpeed = 0;
        }

        /// <summary>充能图形态契约（P12）：恰好 1 个充能原子、无分支/Then、无目标域——
        /// 违规文本（null=合规）。ValidateZones 消费（保存禁用+描述条提示；转换器同口径构筑期拦截）。</summary>
        private string ChargeShapeError()
        {
            if (!GraphHasChargeAtom(_graph)) return null;
            int atomCount = 0;
            foreach (var s in _graph.steps)
            {
                if (s == null) continue;
                if (s.kind == 1) return "充能图不可带条件分支（充能=单原子）";
                if (s.kind == 2 || (s.choices != null && s.choices.Count > 0)) return "充能图不可带抉择分支";
                if (s.atomic?.branch != null) return "充能原子不可挂条件/引擎载荷";
                if (s.thenSteps != null && s.thenSteps.Count > 0) return "充能图不可带 Then 奖励";
                if (s.kind == 0 && s.atomic != null) atomCount++;
            }
            if (atomCount != 1) return "充能图=恰好 1 个充能原子（不可与其他原子组合）";
            if (_graph.header?.TargetKinds != null && _graph.header.TargetKinds.Count > 0)
                return "充能图无目标域（充能只作用于这张手牌）";
            return null;
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

        /// <summary>进入/切模式/载入时的默认选中（2026-10-23 五分类）：并列=原子 1；分支=主干；
        /// 光环双形态=唯一光环槽（仪典入槽）。门槛选中态（Gate）只由点门槛卡进入。</summary>
        private void EnsureDefaultSelection()
        {
            _selSlot = SelKind.Parallel;
            _selSlotIndex = 0;
        }

        /// <summary>每次刷新前校正当选中槽（模式变化/删除原子/清门槛后防悬挂）；
        /// 并列槽允许 0..min(已填数,槽位数-1)（首空槽可追加）；分支形态恒主干槽，
        /// 奖励选中态须仍有分支载荷，门槛选中态（Gate）只在有限分支。</summary>
        private void ClampSelection()
        {
            // 展开态防悬挂（2026-10-06 多卡独立展开）：键=对象身份——只保留仍挂在当前 steps 上的原子/分支
            var stepsNow = _graph.steps ?? new List<EffectStepData>();
            _expandedAtoms.RemoveWhere(a => !stepsNow.Any(s => s?.kind == 0 && ReferenceEquals(s.atomic, a)));
            _expandedRewards.RemoveWhere(br => !stepsNow.Any(s => s?.kind == 0 && ReferenceEquals(s.atomic?.branch, br)));

            switch (_mode)
            {
                case ComposeMode.Parallel:
                    if (_selSlot != SelKind.Parallel) { _selSlot = SelKind.Parallel; _selSlotIndex = 0; }
                    int max = Mathf.Min(stepsNow.Count, ParallelSlotCount - 1);
                    if (_selSlotIndex > max) _selSlotIndex = max;
                    break;
                case ComposeMode.FreeBranch:
                case ComposeMode.LimitedBranch:
                    _selSlotIndex = 0; // 分支形态单主干——选中恒槽 0
                    if (_selSlot == SelKind.Reward && BranchOfSlot(0) == null) _selSlot = SelKind.Parallel;
                    if (_selSlot == SelKind.Gate && _mode != ComposeMode.LimitedBranch) _selSlot = SelKind.Parallel;
                    if (_selSlot != SelKind.Parallel && _selSlot != SelKind.Reward && _selSlot != SelKind.Gate)
                        _selSlot = SelKind.Parallel;
                    break;
                case ComposeMode.LinkAura:
                case ComposeMode.RangeAura:
                    _selSlot = SelKind.Parallel; // 光环单槽：选中恒槽 0（奖励/门槛选中态不适用）
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
                // 模板内 gate-card 预先收起（2026-10-30 预制体化：有限分支克隆体由 MakeGateCard 按需激活）
                var gateCard = tpl.Find("gate-card");
                if (gateCard != null) gateCard.gameObject.SetActive(false);
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
            var hint = UiKit.Label("hint", parent, text, RowSmallSize, UiStyle.TextHint, wrap: true);
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
                // 2026-10-24 定案：换行/溢出表现归预制体（Text Wrapping Mode 在 prefab 上改）——代码只填字
                text.text = (expanded ? "▼ " : "▶ ") + AtomText.RenderAtomEntry(atom, _graph.header);
                UiKit.Size(text, fw: 1f);
            }

            if (head != null)
            {
                bool par = selKind == SelKind.Parallel && _mode == ComposeMode.Parallel; // 换序仅并列双槽有意义
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
            // 模拟目标数量钉 1（2026-10-30 定案：目标恒单个——数量下拉禁改）；其余行走默认
            BindAtomTargetRow(card, atom, RefreshCard,
                pinCountOne: IsImitateEntry(atom));
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

            // ---- 场地光环原子（位 6 RuleAura）：Value=作用范围（RuleAuraScope）非数值——数值/随机不显示；
            // 作用面随极性钉死（2026-10-30：±1 行单侧唯一——+1 仅己方/−1 仅对方，单侧计价 ×1.5）----
            if (mounts.Contains(MountKind.RuleAura))
            {
                valueNode?.gameObject.SetActive(false);
                sliderNode?.gameObject.SetActive(false);
                MakeHint(inject, "仪典无数值参数：作用面随极性钉死——增益仪典仅己方、减益仪典仅对方（单侧档计价 ×1.5）；中性改写行三档在卡面「作用范围」行选");
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

            // 模拟/变形动态主干提示（2026-10-30 挪入自由分支·均仅主动）：引用卡在卡牌合成界面选
            //（选中后显示卡牌列表，不带衍生物的卡均可选）、模拟目标数量恒 1
            if (type == AtomicEffectType.Morph)
                MakeHint(inject, "模拟（仅主动）：变成场上目标单位（跳过其场地光环）——目标数量恒 1；"
                    + "发动主动效果按变形前后费差以任意元素补差");
            else if (type == AtomicEffectType.GrantMorph)
                MakeHint(inject, "变形（仅主动）：引用卡不在此填——在卡牌合成界面选择（选中后显示卡牌列表，"
                    + "不带衍生物的卡均可选）；目标数量恒 1，发动主动效果按变形前后费差以任意元素补差");
        }

        // ======================================== 门槛卡与分支主干（2026-10-23 五分类） ========================================

        /// <summary>分支形态主干落槽（自由=引擎行自动挂 Engine 载荷；有限=普通原子、保留既有门槛/奖励载荷
        /// ——门槛与奖励属效果不属原子，换主干原子不丢）。落位后选中停在主干槽、展开新卡。</summary>
        private void ReplaceBranchTrunk(AtomicEffectEntry entry, bool keepBranch)
        {
            _graph.steps ??= new List<EffectStepData>();
            var old = _graph.steps.Count > 0 && _graph.steps[0].kind == 0 ? _graph.steps[0].atomic : null;
            if (keepBranch && old?.branch != null) entry.branch = old.branch;
            _graph.steps.Clear();
            _graph.steps.Add(new EffectStepData { kind = 0, atomic = entry });
            _expandedAtoms.Add(entry);
            _selSlot = SelKind.Parallel;
            _selSlotIndex = 0;
            RefreshSlots();
        }

        /// <summary>右栏门槛行点击 → 写入 branch{settle=Gate, gateId, then=原奖励保留}——
        /// 历史 Outcome 载荷被整体换成 Gate（产出条件不再新建，2026-10-23 裁定）；
        /// 写入后选中自动切奖励块（下一步必是填奖励），右栏随 RefreshLibrary 切奖励候选。</summary>
        private void ApplySituationGate(ComposerCatalog.GateSpec gate)
        {
            var atom = FirstKind0Atom();
            if (atom == null) { ShowToast("先到右侧选一颗主干原子——门槛挂在主干上"); return; }
            var b = atom.branch ?? new BranchEntryData();
            b.then ??= new List<AtomicEffectEntry>();
            b.settle = (int)BranchSettleKind.Gate;
            b.gateId = gate.Id;
            b.outcomeId = null;
            b.engine = 0; b.engineParam = 0; b.condParam = 0; b.condStr = null;
            atom.branch = b;
            _selSlot = SelKind.Reward; // 门槛已选——下一步必是填奖励
            _selRewardSlot = 0;
            RefreshSlots();
            RefreshLibrary();
            ShowToast("门槛已选——到右侧挑一个奖励原子");
        }

        /// <summary>门槛卡（2026-10-23 五分类·有限分支；2026-10-30 预制体化）：tpl-slot 内烘焙
        /// gate-card（head/text + des），克隆 aura-card 退役——代码只填字接线，不建结构。
        /// 空态=引导点选；已选=◈ 名【奖励B】；历史 Outcome 载荷只读展示「产出条件（旧版）」。
        /// 点卡=选中门槛（右栏切 11 条门槛列表）；换选门槛即整条覆盖（prefab 未烘 del——清空口不提供）。
        /// 选中态=焦点底色（与原子槽/奖励块同款）。</summary>
        private void MakeGateCard(RectTransform slot, AtomicEffectEntry atom)
        {
            var card = slot.Find("gate-card") as RectTransform;
            if (card == null)
            {
                Debug.LogError("[EffectComposer] tpl-slot 缺 gate-card 门槛卡——检查 EffectUI.prefab");
                return;
            }
            card.gameObject.SetActive(true);

            var b = atom?.branch;
            bool phantom = b == null || BranchEntryRules.IsPhantom(b);
            bool legacyOutcome = !phantom && (BranchSettleKind)b.settle == BranchSettleKind.Outcome;
            var gate = !phantom && (BranchSettleKind)b.settle == BranchSettleKind.Gate ? GateSpecOf(b.gateId) : null;

            var text = UiKit.FindDeep(card, "text")?.GetComponent<TMP_Text>();
            if (text == null) Debug.LogError("[EffectComposer] gate-card 缺 text 摘要文本——门槛卡不可显；检查 EffectUI.prefab");
            else
            {
                text.text = gate != null
                    ? $"◈ {gate.DisplayName}【奖励{ComposerCatalog.GateBudget(gate)}】"
                    : legacyOutcome
                        ? $"产出条件（旧版）：{ComposerCatalog.OutcomeConditions.FirstOrDefault(g => g.Id == b.outcomeId)?.DisplayName ?? b.outcomeId}"
                        : "门槛：未选——点击本卡后在右侧选择有限分支";
                UiKit.Size(text, fw: 1f);
            }
            var des = UiKit.FindDeep(card, "des")?.GetComponent<TMP_Text>();
            if (des == null) Debug.LogError("[EffectComposer] gate-card 缺 des 参数文本——检查 EffectUI.prefab");
            else
            {
                des.text = "有限分支：达标发放下方奖励，不达标不奖励也不惩罚（奖励免费）";
                UiKit.Size(des, fw: 1f);
            }

            // 点卡=选中门槛（右栏切门槛列表）——整个卡区都可点：卡根+标题行双路接线
            //（prefab head 烘焙了自带 Button 会吃掉点击不冒泡到卡根——2026-10-23 用户反馈修正）
            void SelectGate()
            {
                if (FirstKind0Atom() == null) { ShowToast("先选主干原子——门槛挂在主干上"); return; }
                SelectSlot(SelKind.Gate, 0);
            }
            var img = card.GetComponent<Image>();
            var btn = card.gameObject.GetComponent<Button>();
            if (btn == null)
            {
                btn = card.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(SelectGate);
            var head = UiKit.FindDeep(card, "head");
            if (head != null)
            {
                foreach (var headBtn in head.GetComponents<Button>())
                {
                    headBtn.transition = Selectable.Transition.None;
                    headBtn.onClick.RemoveAllListeners();
                    headBtn.onClick.AddListener(SelectGate);
                }
            }
            UiKit.Described(btn,
                "门槛：当前局面达标时发放下方 Then 奖励——点击本卡任意位置后到右侧点选一条局面门槛（11 条，表驱动）");
            if (_selSlot == SelKind.Gate && img != null)
            {
                img.enabled = true;
                img.color = new Color(70f / 255f, 110f / 255f, 170f / 255f, 0.32f); // 焦点态底（与槽/奖励块同款）
            }

            // 校验：门槛未选（主干空由主干区报——此处只管有主干没门槛）
            _slotZones.Add(MakeZone(card, () =>
            {
                var a = FirstKind0Atom();
                if (a == null) return null;
                return a.branch == null || BranchEntryRules.IsPhantom(a.branch)
                    ? "门槛未选——点击门槛卡后到右侧选择有限分支" : null;
            }));
        }

        /// <summary>gateId → 目录项（局面门）。</summary>
        private static ComposerCatalog.GateSpec GateSpecOf(string gateId)
            => ComposerCatalog.SituationGates.FirstOrDefault(g => g.Id == gateId);

        /// <summary>槽内 Then 奖励槽区（2026-10-06 预制体化；2026-10-23 五分类：分支两形态共用——
        /// 自由分支=引擎载荷（free），有限分支=门槛/产出载荷）：content 烘焙 reward-slot → tpl-reward-slot
        /// 跨级克隆进槽，视觉不再代码构建——slot-title 标题行 + head 摘要行 + editor 参数区。
        /// 非自由主干（局面门/产出条件——预算=门槛 Budget/条件 2）奖励参数按预算自动生成不可控
        /// ——editor 恒隐藏、head 摘要无展开箭头（点击=选中奖励块）；
        /// 自由主干（引擎）奖励无上限引擎封顶 5——维持手控：head 点击展开 editor 编辑数值。</summary>
        private void MakeRewardSlot(RectTransform slot, BranchEntryData b)
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
                title.text = $"Then 奖励（预算 {RewardCapOf(b)} 以内）";
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
                    b.then?.Clear();
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
                    else SelectSlot(SelKind.Reward, 0);                // 非自由主干=仅选中（参数自动生成，无可编辑）
                });
            }

            // 原子表行四参数 des + 每原子目标行/衍生物行（2026-10-09 下沉定案；持续行随层化退役）：
            // 仅自由主干（引擎）奖励手控参数——非自由主干参数随预算自动生成，行隐藏
            var rewardCfg = AtomicEffectTable.GetByHashId(reward?.refId);
            BindAtomDes(area, rewardCfg);
            if (free && reward != null)
            {
                // 费用指示物（四色增减 8 行）奖励数量钉 1（2026-10-09 定案，见 BindAtomTargetRow/IsAddCostDownRow）
                BindAtomTargetRow(area, reward, RefreshText, pinCountOne: IsAddCostDownRow(rewardCfg));
                BindAtomDerivativeRow(area, reward);

                // 奖励发动方式（2026-10-20 定案：奖励=一般效果按自身发动方式发射，主干只管触发判定）：
                // 克隆区内现有下拉代码建（prefab 不动，同「目标选择」做法）。
                // 倒计时不建（2026-10-20 固定触发定案：主干/奖励不拆开——归零恒自动直发，tactivate 不适用）。
                var srcDd = (BranchEngineKind)b.engine == BranchEngineKind.Countdown
                    ? null
                    : area.GetComponentInChildren<TMP_Dropdown>(true);
                if (srcDd != null)
                {
                    var tgo = UnityEngine.Object.Instantiate(srcDd.gameObject, area, false);
                    tgo.name = "dd-tactivate";
                    var trt = (RectTransform)tgo.transform;
                    trt.SetAsLastSibling();
                    trt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, trt.rect.width + 50f);
                    BindTierDropdown(trt, new[] { -1, 2, 1 }, b.tactivate,
                        v =>
                        {
                            b.tactivate = v;
                            RefreshText();
                        },
                        "奖励发动：条件达成后奖励按此方式发射——主动=待发由你在响应窗发动（0 费上栈可连锁）；自动=达成即直发；未声明=随主干（旧口径）。",
                        labelOf: v => v == -1 ? "随主干" : v == 2 ? "主动" : "自动",
                        optionDesc: v => v == -1 ? "未声明（默认）：奖励随主干的发动方式（向后兼容旧数据）。"
                            : v == 2 ? "主动：条件达成后奖励待发，由你在响应窗中发动（0 费上栈，可被连锁）。"
                            : "自动：条件达成后奖励立即结算（不经响应窗）。");
                }
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
            if (_selSlot == SelKind.Reward && _selRewardSlot == 0)
            {
                img.enabled = true; // 防御：组件禁用态强制开启——选中色必可见
                img.color = new Color(70f / 255f, 110f / 255f, 170f / 255f, 0.32f);
            }

            // 整区点击=选中该奖励块（head 有自己的按钮——其余区域冒泡到本区）
            var btn = area.gameObject.GetComponent<Button>();
            if (btn == null)
            {
                btn = area.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
            }
            btn.onClick.AddListener(() => SelectSlot(SelKind.Reward, 0));

            // 校验：分支未设奖励（converter 折叠为无分支——条件空转）+ 可实现费用内。
            // 2026-10-09 定案：上限统一走 RewardFilterCap——自由分支（引擎）恒 5（自平衡统一后引擎预算全 -1；
            // 此前 -1 直通=「校验恒满足」根因）；有限分支=门槛 Budget。超限=GuideFrame 红框脉冲+描述条文本+禁存。
            _slotZones.Add(MakeZone(area, () =>
            {
                if (b.then == null || b.then.Count == 0)
                    return free
                        ? "Then 奖励未填——点本区选中后到右栏挑一个奖励原子（1-5 费）"
                        : "Then 奖励未填——点本区选中后到右栏挑一个奖励原子（≤门槛预算），或删除门槛";
                int cap = RewardCapOf(b);
                float cost = RewardCost(b.then[0]);
                return cost > cap
                    ? $"奖励超出分支可实现费用（{cost:0.#}/{cap}）——"
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
                ? $"抉择步骤（kind=2）——2026-10-31 P12 已退役：法术多主动效果在施放时择一，此遗留数据不生效"
                : $"步骤 kind={step.kind}（只读）",
                RowSmallSize, UiStyle.TextBody, wrap: true);
            UiKit.Size(text, fw: 1f);
        }

        // ---- 分支载荷工具（2026-10-05 两槽定案；2026-10-23 五分类：分支恒挂首主干） ----

        private BranchEntryData BranchOfSlot(int slotIndex)
        {
            var steps = _graph.steps ?? new List<EffectStepData>();
            if (slotIndex < 0 || slotIndex >= steps.Count) return null;
            var s = steps[slotIndex];
            return s?.kind == 0 ? s.atomic?.branch : null;
        }

        /// <summary>分支奖励预算（两槽定案）：Gate=门预算（表 Budget 列——SituationGates/BuildGatePremium 同源）；
        /// Outcome=产出条件预算；Engine=引擎预算（2026-10-09 自平衡统一：全引擎 -1 无上限——门槛与预算同源于 Then 锚价推导）。</summary>
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

        /// <summary>奖励可实现费用上限（2026-10-09 定案·四口共用）：有限分支=门预算（表 Budget 列 1/2；
        /// DrawnInStandbyThisTurn=3）；产出条件=条件预算（恒 2）；自由分支=引擎分支恒 5（自平衡统一后
        /// 引擎预算全 -1——玩家在 1-5 费区间自由调奖励、门槛随计费推导）
        /// ——库展示、落槽校验、槽区保存校验（超限禁存）与费用预览分支段共用一口。</summary>
        private static int RewardFilterCap(BranchEntryData b)
        {
            int budget = BranchBudget(b);
            return budget >= 0 ? budget : 5;
        }

        /// <summary>预言族奖励预算覆盖（2026-10-29 阶段3c 定案）：预言分支（ProphecyHit/Miss·Outcome）
        /// 的 Then 锚价上限随猜法——**类别 ≤1、卡名 ≤3**（猜中概率越小预算越大）。
        /// 判定源=卡名预言主干行（ProphecyNextCard）的 str 维度（Name: 前缀=卡名档）；
        /// 无预言主干的手写数据按类别档 1 收口。其余分支照旧 RewardFilterCap。</summary>
        private int RewardCapOf(BranchEntryData b)
        {
            int cap = RewardFilterCap(b);
            if (b == null || (BranchSettleKind)b.settle != BranchSettleKind.Outcome
                || (b.outcomeId != "ProphecyHit" && b.outcomeId != "ProphecyMiss"))
                return cap;
            if (_graph?.steps != null)
                foreach (var s in _graph.steps)
                {
                    if (s?.kind != 0 || s.atomic == null) continue;
                    if (AtomicEffectTable.GetByHashId(s.atomic.refId)?.EnumName
                        != nameof(AtomicEffectType.ProphecyNextCard)) continue;
                    return s.atomic.str != null
                           && s.atomic.str.StartsWith("Name:", System.StringComparison.Ordinal) ? 3 : 1;
                }
            return 1;
        }

        /// <summary>奖励原子是否过筛选上限（value=1 锚价口径=库行费用方格同源；转换失败=不过）。
        /// 2026-10-06 修正：推导价与表价（ManaList=行方格展示价）取大比对——检索行宣言名空时推导价
        /// 落单维度档 1 而表价 3（ExactCard 档），旧口径曾致 3 费行漏进 ≤2 的门预算筛选
        /// （库展示与落槽校验共用本口，一并修复）。</summary>
        private static bool RewardWithinFilterCap(AtomicEffectEntry entry, int cap)
        {
            var inst = CardEffectConverter.ConvertAtomForUI(entry);
            if (inst == null) return false;
            float derived = CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance> { inst });
            float declared = AtomicEffectTable.GetByHashId(entry?.refId)?.TotalUnitCost ?? 0f;
            return Mathf.Max(derived, declared) <= cap;
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
            return RewardWithinFilterCap(lp.Entry(), RewardCapOf(b));
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

        /// <summary>组合内限重复行已用 refId 集（主干槽+Then 奖励——与 DuplicateAtomError 同口径）。
        /// 2026-10-19 放开定案后仅引擎主干/仪典行（ComposerCatalog.DuplicateRestricted）用过即隐
        /// （不拒绝），换掉/删除后随 RefreshLibrary 回归；其余行可重复不隐藏。</summary>
        private HashSet<string> RestrictedUsedRefIds()
        {
            var set = new HashSet<string>();
            void Add(string refId)
            {
                if (string.IsNullOrEmpty(refId)) return;
                if (ComposerCatalog.DuplicateRestricted(AtomicEffectTable.GetByHashId(refId))) set.Add(refId);
            }
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                Add(s.atomic.refId);
                foreach (var r in s.atomic.branch?.then ?? new List<AtomicEffectEntry>())
                    if (r != null) Add(r.refId);
            }
            return set;
        }

        /// <summary>校验区口径的限重复检查（null=合规）：库隐藏口径下正常组合不会产生重复
        /// ——本区仅拦载入的旧数据/旁路落位（报错时保存禁用，删其一即解）。
        /// 2026-10-19 放开定案：仅引擎主干/仪典行限重复，其余行可重复。</summary>
        private string DuplicateAtomError()
        {
            var seen = new HashSet<string>();
            string dupName = null;
            void Check(AtomicEffectEntry a)
            {
                if (a == null || !string.IsNullOrEmpty(dupName) || string.IsNullOrEmpty(a.refId)) return;
                if (!ComposerCatalog.DuplicateRestricted(AtomicEffectTable.GetByHashId(a.refId))) return;
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
                : $"重复效果「{dupName}」——引擎主干/模拟·变形/仪典在一个组合中只能出现一次（并列槽与奖励均计入）";
        }

        private static bool IsSummonTokenEntry(AtomicEffectEntry a)
            => AtomicEffectTable.GetByHashId(a?.refId)?.EnumName == nameof(AtomicEffectType.SummonToken);

        /// <summary>变形原子（原变形赋予，2026-10-30 用户改名）：与召唤衍生物共用「引用卡ID」输入行
        ///（2026-10-30 起效果组合期不再填——卡组构筑期绑定，行隐藏）。</summary>
        private static bool IsGrantMorphEntry(AtomicEffectEntry a)
            => AtomicEffectTable.GetByHashId(a?.refId)?.EnumName == nameof(AtomicEffectType.GrantMorph);

        /// <summary>模拟原子（原临摹，2026-10-30 用户改名·挪入自由分支）：目标数量固定 1——数量档禁改。</summary>
        private static bool IsImitateEntry(AtomicEffectEntry a)
            => AtomicEffectTable.GetByHashId(a?.refId)?.EnumName == nameof(AtomicEffectType.Morph);

        /// <summary>宣言编码行（2026-10-29 阶段3c 信息轴；2026-10-30 族改版后四行共用——
        /// 箭头宣言退役；2026-10-31 窃取入伙=五行）：derivative 输入行写 str（"维度:值"，
        /// ProphecyDimension 编码；输入域按行分化，见 DeclarationStrDomainHint）。</summary>
        private static bool IsDeclarationStrEntry(AtomicEffectEntry a)
        {
            var name = AtomicEffectTable.GetByHashId(a?.refId)?.EnumName;
            return name == nameof(AtomicEffectType.DeclareHand)
                   || name == nameof(AtomicEffectType.DeclareDeck)
                   || name == nameof(AtomicEffectType.DeclareDeckTop)
                   || name == nameof(AtomicEffectType.ProphecyNextCard)
                   || name == nameof(AtomicEffectType.RevealByDimension);
        }

        /// <summary>宣言编码输入域提示（2026-10-30 族改版：按行分化——强夺/窃取只收卡名、
        /// 预言只收类别、卡名预言双档、筛选展示维度全量）。</summary>
        private static string DeclarationStrDomainHint(AtomicEffectEntry a)
        {
            var name = AtomicEffectTable.GetByHashId(a?.refId)?.EnumName;
            if (name == nameof(AtomicEffectType.DeclareHand))
                return "宣言编码：Name:卡名（如 Name:青眼白龙——强夺宣言卡名，从对手手牌获得该卡；"
                       + "运行时只收卡名档，其他编码作未命中）";
            if (name == nameof(AtomicEffectType.DeclareDeck))
                return "宣言编码：Name:卡名（如 Name:青眼白龙——窃取宣言卡名，从对手牌库获得该卡；"
                       + "运行时只收卡名档，其他编码作未命中）";
            if (name == nameof(AtomicEffectType.DeclareDeckTop))
                return "宣言编码：Type:Creature / Type:Spell / Type:Enchantment（3 选 1——隐秘宣言类别，"
                       + "对手下回合首张同类卡发动无效；运行时只收类别档）";
            if (name == nameof(AtomicEffectType.ProphecyNextCard))
                return "宣言编码（维度:值）：卡名=Name:卡名（奖励预算 3）/"
                       + "类别=Type:Creature|Spell|Enchantment（预算 1）";
            return "宣言编码（维度:值）：Type:Creature|Spell|Enchantment / Name:卡名 / Color:Red|Blue|Green|Gray / "
                   + "CostParity:Odd|Even / CostExact:0-7 / LinkArrow:方向（筛选展示维度全量）";
        }

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
        /// 数量/随机下沉每原子；2026-10-20 增 dd-select 目标选择方式=预选/结算期）：写 atom.count/atom.rand
        /// /atom.defer（未声明=首档占位/显 off 不写回——回落效果级）；
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

            // 目标数量按名取 dd-num（行内现有多枚下拉：dd-num/dd-select——不再靠「首个下拉」隐式命中）
            var ddNumNode = UiKit.FindDeep(row, "dd-num");
            if (ddNumNode == null)
                Debug.LogError("[EffectComposer] placeholder 行缺 dd-num 下拉（目标数量不可编）——检查 EffectUI.prefab");
            var ddCount = ddNumNode == null ? null : BindTierDropdown(ddNumNode, CountTiers, atom.count,
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
                    atom.rand = on ? 1 : 0; // 随机=正交标志——不弹窗从完整候选域抽取（潜行挡不住）
                    refresh?.Invoke();
                });
                UiKit.Described(tg,
                    "目标随机：开启后这个原子不再弹窗选目标，改为在其作用范围内随机抽取（潜行挡不住随机）。");
            }

            // 目标选择方式（2026-10-20 用户定案）：预选=发动前声明期选（默认·可取消撤销发动）/
            // 结算期=效果处理时选（必须选不可取消，类自动效果）。prefab 静态下拉 dd-select
            //（槽/奖励两模板 placeholder 行各一枚；dd-num=目标数量是独立有效参数不可挪用）；
            // 与随机正交——随机开启=无弹选语境，本下拉禁用。
            TMP_Dropdown ddDefer = null;
            var deferNode = UiKit.FindDeep(row, "dd-select");
            if (deferNode == null)
                Debug.LogError("[EffectComposer] placeholder 行缺 dd-select 下拉（预选/结算期不可编）——检查 EffectUI.prefab");
            else
            {
                ddDefer = BindTierDropdown(deferNode, new[] { 0, 1 }, atom.defer,
                    v =>
                    {
                        atom.defer = v;
                        refresh?.Invoke();
                    },
                    "目标选择：预选=发动前先选好目标，选择时可取消=撤销本次发动（默认）；结算期=扣费后效果处理时才选，必须选不可取消。",
                    labelOf: v => v == 0 ? "预选" : "结算期",
                    optionDesc: v => v == 0
                        ? "预选（默认）：发动宣言前先选目标——发动区就位→选目标（可取消撤销）→连锁检测→扣费。"
                        : "结算期：效果处理时才选目标——类似自动效果必须选、不可取消（费已付卡已承诺）。");
                if (ddDefer != null) ddDefer.interactable = !hiddenDomain && atom.rand != 1;
            }
            if (tg != null && ddDefer != null)
                tg.onValueChanged.AddListener(on => ddDefer.interactable = !hiddenDomain && !on);
        }

        /// <summary>每原子引用卡行（derivative）——2026-10-31 阶段C 按原子表「参数」列分流：
        /// CardRef（变形/模拟/召唤衍生物/附加诅咒/附加祝福）＝「选择卡牌」控件（点按开
        /// 搜索+列表弹层，仅真实生物/结界卡——召唤只列生物），选中写卡 ID 进 str、已选显卡名缩略；
        /// CardName/Dimension（检索/强夺/窃取/筛选展示/预言/卡名预言）＝战斗期弹窗接管
        ///（本行隐藏；旧预置 str 保留兼容＝战斗弹窗默认值，不再在此输入）。
        /// 召唤衍生物保留 num 数量下拉（1-3）。槽卡与奖励卡共用。</summary>
        private void BindAtomDerivativeRow(RectTransform host, AtomicEffectEntry atom)
        {
            var row = UiKit.FindDeep(host, "derivative");
            if (row == null)
            {
                Debug.LogError("[EffectComposer] atom-card 缺 derivative 行（引用卡ID/召唤数量）——检查 EffectUI.prefab");
                return;
            }
            bool isSummon = IsSummonTokenEntry(atom);
            var cfg = AtomicEffectTable.GetByHashId(atom?.refId);
            bool isCardRef = cfg != null && cfg.Param == AtomicParamKind.CardRef;
            bool isDeclareStr = IsDeclarationStrEntry(atom);

            if (isCardRef)
            {
                row.gameObject.SetActive(true);
                BindCardRefField(row, atom, isSummon);
            }
            else
            {
                // CardName/Dimension 行（2026-10-31 阶段C）：合成器停用输入——战斗结算期弹窗
                //（卡名文本窗/维度选择窗）；旧 str 只作默认值保留。非参数行照旧隐藏。
                row.gameObject.SetActive(false);
            }

            var numNode = UiKit.FindDeep(row, "num");
            // 数量下拉仅召唤显示（2026-10-13 变形赋予恒 1；宣言/维度行无数量——隐藏）
            if (numNode != null) numNode.gameObject.SetActive(isCardRef && isSummon);
            if (!isSummon)
            {
                if (isCardRef) atom.value = 1; // 变形/模拟恒 1（原口径）
                return;
            }

            var numDd = numNode?.GetComponent<TMP_Dropdown>();
            if (numDd == null) Debug.LogError("[EffectComposer] derivative 行缺 num 下拉——召唤数量不可编；检查 EffectUI.prefab");
            else
            {
                // 档位收窄 1-3（2026-10-10 定案，原 1-5）：旧档 4/5 落数据绑定时归一（>3 收 3、
                // 其余非法值落 1），显示与数据一致后才挂监听
                var tiers = new List<int> { 1, 2, 3 };
                int idx = tiers.IndexOf(atom.value);
                if (idx < 0)
                {
                    atom.value = atom.value > 3 ? 3 : 1;
                    idx = tiers.IndexOf(atom.value);
                }
                numDd.ClearOptions();
                numDd.AddOptions(tiers.Select(v => v.ToString()).ToList());
                numDd.SetValueWithoutNotify(idx);
                numDd.RefreshShownValue();
                numDd.onValueChanged.RemoveAllListeners();
                numDd.onValueChanged.AddListener(i =>
                {
                    atom.value = tiers[Mathf.Clamp(i, 0, tiers.Count - 1)];
                    RefreshSlots();
                });
                UiKit.Described(numDd, "召唤数量（1-3）：一次召唤几个衍生物——每个都是独立实例，战场满位时后续入墓。");
            }
        }

        /// <summary>CardRef 行选择控件（2026-10-31 阶段C）：InputField 退化为只读显示位
        ///（已选=卡名缩略+ID，未选=占位提示），点按本体开卡牌选择弹层。</summary>
        private void BindCardRefField(RectTransform row, AtomicEffectEntry atom, bool creatureOnly)
        {
            var idField = UiKit.FindDeep(row, "InputField")?.GetComponentInChildren<TMP_InputField>(true);
            if (idField == null)
            {
                Debug.LogError("[EffectComposer] derivative 行缺 InputField——引用卡不可选；检查 EffectUI.prefab");
                return;
            }
            idField.interactable = false;
            idField.readOnly = true;
            RefreshCardRefField(idField, atom.str, creatureOnly);
            var btn = idField.gameObject.GetComponent<Button>();
            if (btn == null) btn = idField.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OpenCardRefPicker(atom, creatureOnly,
                () => RefreshCardRefField(idField, atom.str, creatureOnly)));
            UiKit.Described(idField, creatureOnly
                ? "选择卡牌（召唤衍生物）：指向一张真实生物卡——实例=该卡全参数复制、恒落战场。"
                  + "未选则保存被拦（与装载期校验同口径）。"
                : "选择卡牌（变形/模拟/诅咒/祝福载荷）：指向一张真实生物或结界卡——选中写卡 ID 进原子。"
                  + "未选则保存被拦（与装载期校验同口径）。");
        }

        /// <summary>引用卡显示位刷新：已选=卡名缩略（ID 备查·断链照实报）；未选=占位提示。</summary>
        private static void RefreshCardRefField(TMP_InputField field, string str, bool creatureOnly)
        {
            CardData picked = string.IsNullOrEmpty(str) ? null : CardCatalog.GetById(str);
            field.SetTextWithoutNotify(picked != null
                ? $"已选：{(string.IsNullOrEmpty(picked.CardName) ? picked.ID : picked.CardName)}"
                : "");
            if (field.placeholder is TMP_Text ph)
                ph.text = string.IsNullOrEmpty(str)
                    ? (creatureOnly ? "（未选——点此选择生物卡）" : "（未选——点此选择生物/结界卡）")
                    : (picked == null ? $"[断链] {str}" : $"（点此重选·现 {str}）");
        }

        /// <summary>卡牌选择弹层（2026-10-31 阶段C）：搜索 + 列表（仅真实生物/结界卡——
        /// 剔除衍生物模板与教学卡；召唤只列生物）。选中写 atom.str 并刷新槽位摘要。</summary>
        private UiKit.Modal _cardRefModal;

        private void OpenCardRefPicker(AtomicEffectEntry atom, bool creatureOnly, Action onPicked)
        {
            if (_cardRefModal != null) { _cardRefModal.Close(); _cardRefModal = null; } // 重复点开=换卡窗
            var rowName = AtomicEffectTable.GetByHashId(atom?.refId)?.DisplayName ?? "引用卡";
            var modal = UiKit.ModalBox(Root, $"选择卡牌——{rowName}{(creatureOnly ? "（仅生物）" : "（生物/结界）")}",
                width: 660f, height: 600f);
            _cardRefModal = modal;

            var searchRow = UiKit.Row("search", modal.Panel, spacing: 8f);
            UiKit.Size(UiKit.Label("lbl", searchRow, "搜索：", 13, UiStyle.TextSecondary), w: 40f);
            var searchField = UiKit.InputField("input", searchRow, "卡名 / ID 关键词", "",
                width: 400f);
            UiKit.Button("btn-close", searchRow, "关闭", () =>
            {
                _cardRefModal?.Close();
                _cardRefModal = null;
            }, height: 30f, fontSize: 13);

            var scroll = UiKit.ScrollColumn("list", modal.Panel);
            UiKit.Size(scroll.Rect, fw: 1f, fh: 1f);

            void Rebuild()
            {
                for (int i = scroll.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(scroll.Content.GetChild(i).gameObject);
                string key = (searchField.text ?? "").Trim();
                var candidates = CardSorter.Sort(CardCatalog.LoadAll()
                        .Where(c => CardRefCandidateOk(c, creatureOnly))
                        .Where(c => string.IsNullOrEmpty(key)
                                    || (c.CardName ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)
                                    || (c.ID ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                const int cap = 150;
                int shown = 0;
                foreach (var card in candidates)
                {
                    if (shown++ >= cap) break;
                    var pick = card;
                    var line = UiKit.Row("card-" + pick.ID, scroll.Content, spacing: 8f);
                    UiKit.Size(UiKit.Label("cost", line, ((int)pick.TotalCost).ToString(), 13, UiStyle.TextFaint), w: 30f);
                    UiKit.Size(UiKit.Label("name", line,
                        string.IsNullOrEmpty(pick.CardName) ? pick.ID : pick.CardName, 13), w: 200f);
                    UiKit.Size(UiKit.Label("meta", line, CardRefMetaOf(pick), 12, UiStyle.TextFaint), fw: 1f);
                    bool current = atom.str == pick.ID;
                    UiKit.Button("btn-pick", line, current ? "已选 ✓" : "选择", () =>
                    {
                        atom.str = pick.ID;
                        RefreshSlots(); // 摘要（{引用卡}→卡名）与装载校验随写刷新
                        onPicked?.Invoke();
                        _cardRefModal?.Close();
                        _cardRefModal = null;
                    }, bg: current ? UiStyle.SelectedRowBg : null, height: 30f, fontSize: 13);
                }
                UiKit.Label("count", scroll.Content,
                    candidates.Count == 0
                        ? "（无匹配卡——换关键词）"
                        : shown < candidates.Count ? $"共 {candidates.Count} 张，仅列前 {cap} 张——用搜索缩小范围"
                        : $"共 {candidates.Count} 张", 12, UiStyle.TextHint);
            }

            searchField.onValueChanged.RemoveAllListeners();
            searchField.onValueChanged.AddListener(_ => Rebuild());
            Rebuild();
        }

        /// <summary>选择器候选资格（2026-10-31 阶段C）：真实生物/结界卡——剔除衍生物模板
        ///（IsTokenTemplate）与教学卡；召唤衍生物只收生物（装载期 ValidateMountHosts 同口径）。</summary>
        private static bool CardRefCandidateOk(CardData card, bool creatureOnly)
        {
            if (card == null || string.IsNullOrEmpty(card.ID) || card.IsTokenTemplate) return false;
            if (CardCatalog.IsTeachingCard(card)) return false;
            return creatureOnly
                ? card.Supertype == Cardtype.Creature
                : card.Supertype == Cardtype.Creature || card.Supertype == Cardtype.Enchantment;
        }

        /// <summary>候选行 meta 文本：类型 + 攻/血（生物，缺项显 ?）或类型标注。</summary>
        private static string CardRefMetaOf(CardData card)
            => card.Supertype == Cardtype.Creature
                ? $"生物 {(card.Power.HasValue ? card.Power.Value.ToString() : "?")}/{(card.Life.HasValue ? card.Life.Value.ToString() : "?")}"
                : card.Supertype == Cardtype.Enchantment ? "结界" : card.Supertype.ToString();

        /// <summary>极性→合法作用面方向码（2026-10-30 收窄定案）：+1→{己方}；-1→{对方}；
        /// 0→{己方,双方,对方} 三选（中性改写行保留双方档）。仅仪典卡作用面下拉消费
        ///（连接光环条目作用范围已删、场地属性降低条目恒对方）。</summary>
        private static List<int> AllowedScopesOfPolarity(float p)
            => p >= 0.5f ? new List<int> { 1 } : p <= -0.5f ? new List<int> { 3 } : new List<int> { 1, 2, 3 };

        /// <summary>档位下拉静态绑定（两段式描述）：desc=头部总述；optionDesc=选中项介绍
        ///（index→文本，null/空＝保持总述）。标签由档位值生成（默认 -1/0=「全部」；可传 labelOf
        /// 直显数字）；现值不在档内=首档占位显示不写回（存量兼容——改选才落值，
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

        /// <summary>当前效果引擎种类（与 InferMode 同口径：**仅引擎主干行**算引擎在案——行身份∪其槽级
        /// 引擎载荷；非引擎行挂的旧引擎载荷按并列单行展示（2026-10-26 定案），不再触发效果头锁定。
        /// null=无引擎）。</summary>
        private BranchEngineKind? EngineKindOfGraph()
        {
            var atom = _graph?.steps?.FirstOrDefault(s => s?.kind == 0 && s.atomic != null)?.atomic;
            if (atom == null) return null;
            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (!ComposerCatalog.IsEngineTrunkRow(cfg)) return null;
            if (atom.branch != null && (BranchSettleKind)atom.branch.settle == BranchSettleKind.Engine
                && Enum.IsDefined(typeof(BranchEngineKind), atom.branch.engine))
                return (BranchEngineKind)atom.branch.engine;
            if (cfg != null && Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)) // 行身份兜底（引擎行落槽即挂载荷——双保险）
            {
                var k = ComposerCatalog.EngineKindOf(t);
                if (k != BranchEngineKind.None) return k;
            }
            return null;
        }

        /// <summary>自由分支主干发动方式单值钉（2026-10-30 用户定案）：
        /// 模拟（Morph）/变形（GrantMorph）动态主干=仅主动；倒计时/元素充盈=仅自动
        ///（引擎预设钉死组）；其余引擎=null（自动/主动双选——预设仅作落槽缺省）。
        /// 无引擎主干（并列/有限分支/光环）=null（宿主形状闸照旧管辖）。
        /// 消费：ApplyEngineHeaderLock（钉值+锁下拉）、发动方式下拉 Changed 闸（非法选择弹回）。</summary>
        private EffectActivationType? EngineActivationPin()
        {
            var atom = _graph?.steps?.FirstOrDefault(s => s?.kind == 0 && s.atomic != null)?.atomic;
            if (atom == null) return null;
            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (!ComposerCatalog.IsEngineTrunkRow(cfg)) return null;
            if (Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t))
            {
                if (t == AtomicEffectType.Morph || t == AtomicEffectType.GrantMorph)
                    return EffectActivationType.Voluntary;
                if (t == AtomicEffectType.EngineCountdown || t == AtomicEffectType.EngineManaSurplus)
                    return EffectActivationType.Automatic;
            }
            return null;
        }

        /// <summary>引擎效果头锁定（2026-10-09 自由分支九项定案；速度组 2026-10-15 退役；2026-10-16 解绑；
        /// 2026-10-30 发动方式定案）：首槽引擎在案 → 时机/速度/生效次数按 ComposerCatalog.EngineHeaderPresetOf
        /// 归一；发动方式三分——①模拟/变形动态主干=仅主动（钉死+锁下拉）；②倒计时/元素充盈=仅自动
        ///（钉死组，预设归一+锁下拉）；③其余引擎=自动/主动**双选**（ComposerCatalog.EngineActivationFree——
        /// 预设仅作落槽缺省，下拉解锁，合法声明保留；主动档时机钉 Activate_*、自动档时机归预设）。
        /// **FreeTiming 组（仅运势）时机不再钉死**——时机可改（掷骰恒自动）；速度/生效次数仍钉。
        /// 落槽瞬间（引擎种类切换）且时机仍为图缺省
        /// OnPlay(0) → 补预设缺省 OnTurnStart（防新效果落在「奖励等下次入场」死档）；其余刷新不动用户值。
        /// 引擎主干零计价——limit=-1 档无费用影响。
        /// 消费点：RefreshSlots 尾部（覆盖引擎落槽/换引擎/载入三路径）。</summary>
        private BranchEngineKind? _lastEngineLockKind; // 落槽种类跟踪：FreeTiming 缺省只在切换瞬间补，刷新不覆写用户值

        private void ApplyEngineHeaderLock()
        {
            var h = _graph?.header;
            if (h == null) return;
            var currentKind = EngineKindOfGraph();
            var preset = currentKind is { } kind ? ComposerCatalog.EngineHeaderPresetOf(kind) : null;
            bool locked = preset != null;
            bool freeTiming = locked && preset.Value.FreeTiming;
            bool justLanded = currentKind != _lastEngineLockKind; // null→kind 落槽 / kindA→kindB 换引擎
            _lastEngineLockKind = currentKind;

            // 发动方式单值钉（2026-10-30）：模拟/变形=主动、倒计时/元素充盈=自动；null=双选
            var actPin = EngineActivationPin();

            if (locked)
            {
                if (actPin != null)
                {
                    // 钉死组（倒计时/元素充盈）：预设归一——发动方式恒预设 Automatic
                    h.ActivationType = (int)preset.Value.Activation;
                    h.TriggerTiming = preset.Value.Activation == EffectActivationType.Voluntary
                        ? (int)TriggerTiming.Activate_Active : (int)preset.Value.Timing;
                }
                else
                {
                    // 双选组：预设 Activation 仅落槽缺省——合法声明值（1 自动/2 主动）保留
                    if (justLanded || (h.ActivationType != (int)EffectActivationType.Automatic
                                     && h.ActivationType != (int)EffectActivationType.Voluntary))
                        h.ActivationType = (int)preset.Value.Activation;
                    bool vol = h.ActivationType == (int)EffectActivationType.Voluntary;
                    if (freeTiming)
                    {
                        // 2026-10-16 解绑（仅运势在册）：时机声明值优先——只在落槽瞬间补缺省
                        if (justLanded && h.TriggerTiming == 0) h.TriggerTiming = (int)preset.Value.Timing;
                    }
                    else
                    {
                        h.TriggerTiming = (int)(vol ? TriggerTiming.Activate_Active : preset.Value.Timing);
                    }
                }
                h.BaseSpeed = preset.Value.Speed;
                h.TriggerLimitPerTurn = preset.Value.Limit;
            }
            else if (actPin != null)
            {
                // 动态主干（模拟/变形）：无引擎预设——仅主动钉死 + 主动钉档时机
                h.ActivationType = (int)EffectActivationType.Voluntary;
                h.TriggerTiming = (int)TriggerTiming.Activate_Active;
            }

            // 值对齐先行，禁用态最后落——防同步链覆写锁定
            _activationDropdown?.SetIndex(ActivationIndex(h.ActivationType));
            SyncActivationVisibility();
            SyncTimingDropdown();

            // 禁用面（2026-10-30 修订）：发动下拉=单值钉死组（模拟/变形/倒计时/元素充盈）锁定、
            // 双选引擎解锁；时机=预设锁（FreeTiming 组解锁；动态主干随主动档隐藏时机行）；
            // 速度组已退役无控件；次数选项已整删。
            if (_activationDropdown != null)
                _activationDropdown.Interactable = actPin == null;
            if (_timingDropdown != null)
                _timingDropdown.Interactable = !locked || freeTiming;
        }

        /// <summary>费用指示物行判定（四色增减 8 行，2026-10-31 四色化）——奖励槽数量钉 1 定案配套
        ///（BindAtomTargetRow；handler 恒 1 层，数量档对全族无效）。</summary>
        private static bool IsAddCostDownRow(AtomicEffectConfig cfg)
            => cfg != null && Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)
               && (t == AtomicEffectType.AddCostUpGray || t == AtomicEffectType.AddCostUpRed
                   || t == AtomicEffectType.AddCostUpBlue || t == AtomicEffectType.AddCostUpGreen
                   || t == AtomicEffectType.AddCostDownGray || t == AtomicEffectType.AddCostDownRed
                   || t == AtomicEffectType.AddCostDownBlue || t == AtomicEffectType.AddCostDownGreen);

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
            SetRightMode(RightMode.Atoms); // 内含 RefreshLibrary（颜色筛选跨模式保留）
            ShowToast($"原子表已重读：{_libraryList?.Content?.childCount ?? 0} 行入列");
        }

        private void SetRightMode(RightMode mode)
        {
            _rightMode = mode;
            // 面板包装层已删（2026-10-03 手改）：显隐直接驱动滚动区本体（UiKit.Scroll 包装类走 .Rect）
            _libraryList.Rect.gameObject.SetActive(mode == RightMode.Atoms);
            _effectsList.Rect.gameObject.SetActive(mode == RightMode.Effects);

            // 筛选栏两模式共用：颜色筛选/搜索跨模式保留（原分类下拉随 dropdown-filter-type 退役）

            // 合并切换按钮标签：常显"将切到哪边"
            if (_tableToggleLabel != null)
                _tableToggleLabel.text = mode == RightMode.Atoms ? "切换效果表" : "切换原子表";

            if (mode == RightMode.Effects) RefreshEffectsList();
            else RefreshLibrary();
        }

        private void RefreshEffectsList()
        {
            if (_effectRows == null) return;
            EffectsLibrary.Reload(); // 外部可能直改 Effects.json——进效果表即重读
            var effects = EffectLibrarySerializer.LoadAll();
            _effectItems.Clear();
            // 卡 hash 反查语料（2026-10-26）：效果表语料此前只有效果自身 id——输卡 ID（如
            // C_1DBF77BC）或卡名查不到"这张卡用了哪些效果行"。此处把「引用本效果的卡（ID·名）」
            // 并入语料补齐反向（卡池侧 CorpusOf 已含卡 ID，hash 搜卡那边本就通）。LoadAll 带缓存，
            // 每刷重建小映射开销可忽略；教学卡同入（教学表 effectIds 同样解析到库行）。
            var effectUsers = new Dictionary<string, List<string>>();
            foreach (var c in CardCatalog.LoadAll())
            {
                if (c?.Effects == null || c.Effects.Count == 0) continue;
                string label = string.IsNullOrEmpty(c.CardName) ? c.ID : c.ID + "·" + c.CardName;
                foreach (var fx in c.Effects)
                {
                    if (fx == null || string.IsNullOrEmpty(fx.Id)) continue;
                    if (!effectUsers.TryGetValue(fx.Id, out var list)) effectUsers[fx.Id] = list = new List<string>();
                    if (!list.Contains(label)) list.Add(label);
                }
            }
            if (effects.Count == 0)
            {
                // 2026-10-28：主库空不再短路——预设卡组效果段照列（主表被清理/新装档常态为空）
                AddTableHint(_effectItems, _effectsList, "效果库为空——左侧编辑后「保存」即入库；下方列出预设卡组使用的组合效果");
                AppendPresetEffectRows(effects, effectUsers);
                _effectRows.SetItems(_effectItems.Count, i => _effectItems[i].H);
                return;
            }
            string q = _filterName.text;
            foreach (var fx in effects)
            {
                var captured = fx;
                var parts = EffectAtomCfgs(captured); // 组成部分表行（分类/色点/简称检索共用）

                // 颜色筛选：任一组成部分费用含选中色即入选（不计价效果仅「全部」下出现）
                if (_colorFilter != UIColor.All && !parts.Any(c => AtomMatchesColor(c, _colorFilter))) continue;

                // 近似检索（2026-10-13 语料扩展+多词 AND）：名∪id∪组成简称∪完整摘要文本∪引用卡
                if (SearchQuery.IsActive(q))
                {
                    string abbr = string.Concat(parts.Select(c => c?.DisplayName ?? ""));
                    string corpus = string.Join("\n", captured.name, captured.id, abbr,
                        AtomText.RenderEffectSummary(captured),
                        effectUsers.TryGetValue(captured.id, out var refs) ? string.Join("\n", refs) : "");
                    if (!SearchQuery.Match(corpus, q)) continue;
                }

                _effectItems.Add(new TableRowData
                {
                    Name = captured.name,                                          // 行名（2026-10-23 行模板化恢复显示）
                    Tag = string.Join("·", parts.Select(c => c?.DisplayName ?? "")), // 组成简称
                    Meta = AtomText.RenderEffectSummary(captured), // 完整效果描述（2026-10-03 定案：不再简写）
                    Dot = DotColor(ColorFilter.OfTagsCsv(parts.FirstOrDefault()?.Tags)),
                    Costs = captured.header?.AnchorCost,
                    OnClick = () => LoadEffectFromLibrary(captured),
                    H = TableRowH,
                });
            }
            AppendPresetEffectRows(effects, effectUsers);
            _effectRows.SetItems(_effectItems.Count, i => _effectItems[i].H);
        }

        /// <summary>预设卡组用到的组合效果段（2026-10-28 定案）：教学效果表只读展示——
        /// DeckCatalog 预组 → 卡 → 效果 id 引用集；主表已含同 id（玩家另存过同内容副本）不重复列。
        /// 行点击载入左栏（LoadPresetEffect）——修改保存=另存新条目，源档不可覆盖不可删。</summary>
        private void AppendPresetEffectRows(List<EffectGraphData> mainEffects, Dictionary<string, List<string>> effectUsers)
        {
            // 预组卡组 → 引用效果 id → 卡组名（Tag 展示「预设·卡组名」）
            var presetRefs = new Dictionary<string, List<string>>();
            foreach (var deck in DeckCatalog.LoadPresets())
            {
                if (deck?.cardIds == null) continue;
                foreach (var cardId in deck.cardIds)
                {
                    var card = CardCatalog.GetById(cardId);
                    if (card?.Effects == null) continue;
                    foreach (var fx in card.Effects)
                    {
                        if (fx == null || string.IsNullOrEmpty(fx.Id)) continue;
                        if (!presetRefs.TryGetValue(fx.Id, out var decks)) presetRefs[fx.Id] = decks = new List<string>();
                        if (!decks.Contains(deck.name)) decks.Add(deck.name);
                    }
                }
            }
            if (presetRefs.Count == 0) return;

            var mainIds = new HashSet<string>();
            foreach (var f in mainEffects)
                if (f != null && !string.IsNullOrEmpty(f.id)) mainIds.Add(f.id);

            string q = _filterName.text;
            int shown = 0;
            foreach (var fx in EffectLibrarySerializer.LoadAll(true))
            {
                var captured = fx;
                if (captured == null || string.IsNullOrEmpty(captured.id)) continue;
                if (!presetRefs.ContainsKey(captured.id)) continue; // 只列预组真正用到的
                if (mainIds.Contains(captured.id)) continue;        // 玩家已另存同内容副本——主表行为准
                var parts = EffectAtomCfgs(captured);

                // 颜色筛选/近似检索与主表行同口径：名∪id∪组成简称∪摘要∪卡组名∪引用卡
                if (_colorFilter != UIColor.All && !parts.Any(c => AtomMatchesColor(c, _colorFilter))) continue;
                if (SearchQuery.IsActive(q))
                {
                    string abbr = string.Concat(parts.Select(c => c?.DisplayName ?? ""));
                    string corpus = string.Join("\n", captured.name, captured.id, abbr,
                        AtomText.RenderEffectSummary(captured),
                        string.Join("\n", presetRefs[captured.id]),
                        effectUsers.TryGetValue(captured.id, out var refs) ? string.Join("\n", refs) : "");
                    if (!SearchQuery.Match(corpus, q)) continue;
                }

                if (shown == 0)
                    AddTableHint(_effectItems, _effectsList,
                        "以下为预设卡组使用的组合效果（只读）——点击载入查看；修改后保存会另存为新效果，原效果不可改删");
                shown++;
                _effectItems.Add(new TableRowData
                {
                    Name = captured.name,
                    Tag = "预设·" + string.Join("·", presetRefs[captured.id]),
                    Meta = AtomText.RenderEffectSummary(captured) + "（预设效果——保存另存为新效果，不可删除）",
                    Dot = DotColor(ColorFilter.OfTagsCsv(parts.FirstOrDefault()?.Tags)),
                    Costs = captured.header?.AnchorCost,
                    OnClick = () => LoadPresetEffect(captured),
                    H = TableRowH,
                });
            }
        }

        /// <summary>点击预设效果行 → 载入左栏编辑（2026-10-28 定案）：保存恒另存新条目（同内容拦存）、
        /// 源教学表不可覆盖不可删——删除钮随 _loadedFromLibraryId=null 隐藏（UpdateDeleteState）。</summary>
        private void LoadPresetEffect(EffectGraphData fx)
        {
            if (_editingCard != null)
            {
                ShowToast("卡编辑模式不可载入预设效果（先保存或返回）");
                return;
            }
            _graph = fx;
            _graph.header ??= new CardEffectData();
            _graph.steps ??= new List<EffectStepData>();
            _loadedFromLibraryId = null;
            _presetSourceId = _graph.id;
            var note = NormalizeLoadedGraph(_graph);

            _mode = InferMode(_graph);
            _expandedAtoms.Clear();
            _expandedRewards.Clear();
            EnsureDefaultSelection();
            SyncTimingDropdown();
            _activationDropdown?.SetIndex(ActivationIndex(_graph.header.ActivationType));
            SyncActivationVisibility();
            RefreshAll();
            UpdateDeleteState(); // 预设载入=删除钮隐藏
            // 快照在归一化/效果头对齐（RefreshAll 链内 SyncActivationVisibility 等可能钉时机）全部落定后拍
            _presetSnapshotHash = PresetContentHash(_graph);
            ShowToast(note != null
                ? $"已载入预设效果：{_graph.name}（修改保存=另存新效果）——{note}"
                : $"已载入预设效果：{_graph.name}（修改保存=另存新效果，原预设不可改删）");
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
            _presetSourceId = null;
                _presetSnapshotHash = null; // 常规库条目接管——预设只读态退出
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

        // ---- 筛选栏（2026-10-13 分类下拉退役：pool-filter 颜色行接管） ----

        /// <summary>颜色行绑定：pool-filter 预制体烘焙 chip-{Color}，Build 一次接线；单选——
        /// 点色即筛费用含该色的原子、点「全部」复位（再点已选色不动作）；激活态=Outline 开关，
        /// 默认「全部」亮。缺 chip 容错跳过（本 prefab 无灰 chip——灰费行仅在「全部」下出现）。</summary>
        private void BindPoolFilter()
        {
            if (_poolFilter == null)
            {
                Debug.LogError("[EffectComposer] 缺 pool-filter（颜色筛选行）——检查 EffectUI.prefab");
                return;
            }
            foreach (UIColor color in Enum.GetValues(typeof(UIColor)))
            {
                var captured = color;
                var chipRt = UiKit.FindDeep(_poolFilter, $"chip-{captured}");
                var chip = chipRt != null ? chipRt.GetComponentInChildren<Button>(true) : null;
                if (chip == null) continue;
                chip.onClick.AddListener(() =>
                {
                    if (_colorFilter == captured) return;
                    _colorFilter = captured;
                    UpdateFilterChips();
                    RefreshLibrary();
                    if (_rightMode == RightMode.Effects) RefreshEffectsList();
                });
                _filterChips[captured] = chip;
            }
            UpdateFilterChips();
        }

        /// <summary>激活态切换（单选）：选中 chip 加描边；Outline 缺失时补挂（同 CardComposerScreen 口径）。</summary>
        private void UpdateFilterChips()
        {
            foreach (var kv in _filterChips)
            {
                bool active = kv.Key == _colorFilter;
                var ol = kv.Value.GetComponent<Outline>();
                if (active && ol == null)
                {
                    ol = kv.Value.gameObject.AddComponent<Outline>();
                    ol.effectColor = UiStyle.TextPrimary;
                    ol.effectDistance = new Vector2(1.5f, -1.5f);
                }
                if (ol != null) ol.enabled = active;
            }
        }

        /// <summary>选色判定：「全部」恒过；选色要求费用含该色份额（>0）——不计价行仅在全模式下出现。</summary>
        private static bool AtomMatchesColor(AtomicEffectConfig cfg, UIColor color)
        {
            if (color == UIColor.All) return true;
            var cost = cfg?.ManaList;
            return cost != null && cost[ManaOf(color)] > 0f;
        }

        private static ManaType ManaOf(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return ManaType.Red;
                case UIColor.Blue: return ManaType.Blue;
                case UIColor.Green: return ManaType.Green;
                default: return ManaType.Gray; // 白/黑 chip（UI 遗留）落灰
            }
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

        /// <summary>原子行搜索语料（2026-10-13）：名∪描述模板∪Tags∪MountKinds∪TargetKinds
        ///（后三者中文 CSV 直收——搜「关键词」「指示物」「连接光环」等类别词即中）。</summary>
        private static string AtomCorpus(AtomicEffectConfig row)
            => row == null ? "" : string.Join("\n",
                row.DisplayName, row.Description, row.Tags, row.MountKinds, row.TargetKinds);

        /// <summary>当前选中槽的可放置性谓词（库只展示可用项——与分类/搜索取交集；2026-10-23 五分类）。
        /// 附带中文上下文（右栏顶部 AddTableHint 提示行消费）。并列=普通主干原子（排除引擎行）；
        /// 自由分支主干=仅引擎行（位 5）；有限分支主干=普通原子（排引擎/门槛行）；奖励块=BranchReward
        /// 位+非错边+预算内（自由恒 5、有限=门槛 Budget）。门槛选中态/光环形态不走本口（RefreshLibrary 分流）。</summary>
        private Func<LibPayload, bool> CurrentSlotPredicate(out string contextZh)
        {
            switch (_mode)
            {
                case ComposeMode.Parallel:
                {
                    contextZh = _selSlotIndex == 1
                        ? "并列槽 2（主动原子·非错边）"
                        : "并列槽 1（主动原子·非错边）";
                    // 五分类：并列无分支——引擎行不入并列槽（要引擎请切自由分支）
                    return lp => lp.CanBeTrunk && !lp.IsWrongSideOnly && !lp.IsEngineTrunk
                                 && (lp.Cfg?.GetTargetKindList()?.Count ?? 0) > 0;
                }
                case ComposeMode.FreeBranch:
                {
                    if (_selSlot == SelKind.Reward)
                    {
                        // 自由分支奖励：上限恒 5（引擎预算 -1 无上限——玩家在 1-5 费区间自由调，
                        // 门槛随计费推导、超 5 红闪禁存；预言分支走 RewardCapOf 猜法档 1/3）
                        var b = BranchOfSlot(0);
                        int cap = RewardCapOf(b);
                        contextZh = $"Then 奖励（非错边——仅示 ≤{cap} 费原子）";
                        return lp => b != null && lp.CanBeBranchReward && !lp.IsWrongSideOnly
                                     && RewardWithinFilterCap(lp.Entry(), cap);
                    }
                    contextZh = "主干（引擎行——倒计时/拼点/运势/死亡计数/元素充盈/团结/附加诅咒·祝福，或模拟/变形动态主干）";
                    return lp => lp.IsEngineTrunk;
                }
                case ComposeMode.LimitedBranch:
                {
                    if (_selSlot == SelKind.Reward)
                    {
                        // 有限分支奖励：上限=该门槛 Budget（RewardFilterCap 单源；预言分支 RewardCapOf 猜法档）
                        var b = BranchOfSlot(0);
                        int cap = RewardCapOf(b);
                        contextZh = b != null
                            ? $"Then 奖励（非错边——仅示 ≤{cap} 费原子）"
                            : null; // 无门槛无奖励口——门槛卡区校验已提示
                        return lp => b != null && lp.CanBeBranchReward && !lp.IsWrongSideOnly
                                     && RewardWithinFilterCap(lp.Entry(), cap);
                    }
                    contextZh = "主干（普通原子·非错边——引擎行请切自由分支）";
                    return lp => lp.CanBeTrunk && !lp.IsWrongSideOnly && !lp.IsEngineTrunk
                                 && (lp.Cfg?.GetTargetKindList()?.Count ?? 0) > 0;
                }
                case ComposeMode.LinkAura:
                case ComposeMode.RangeAura:
                    contextZh = null; // 光环库走 RefreshAuraLibrary 分流
                    return null;
            }
            contextZh = null;
            return null;
        }

        private void RefreshLibrary()
        {
            if (_atomRows == null) return;
            _atomItems.Clear();

            // 光环双形态：右栏=光环条目库（连接）/仪典库（范围）——点击换选，编辑在左栏
            if (IsAuraMode(_mode))
            {
                RefreshAuraLibrary();
                _atomRows.SetItems(_atomItems.Count, i => _atomItems[i].H);
                return;
            }

            // 有限分支·门槛选中态：右栏=11 条门槛（表驱动——颜色筛选不适用，门槛无费用色；搜索照旧可筛）
            if (_mode == ComposeMode.LimitedBranch && _selSlot == SelKind.Gate)
            {
                AddTableHint(_atomItems, _libraryList, "当前：填入门槛——点一条即填入（达标发奖，不达标不奖不惩）");
                foreach (var g in ComposerCatalog.SituationGates)
                {
                    var captured = g;
                    if (!SearchQuery.Match(captured.DisplayName ?? "", _filterName.text)) continue;
                    _atomItems.Add(new TableRowData
                    {
                        Name = captured.DisplayName,
                        Meta = ComposerCatalog.GateLabel(captured),
                        Dot = DotColor(UIColor.All),
                        Costs = null, // 门槛行无费用方格
                        OnClick = () => ApplySituationGate(captured),
                        H = TableRowH,
                    });
                }
                if (_atomItems.Count == 1) // 只剩提示行=门槛目录为空
                    AddTableHint(_atomItems, _libraryList, "门槛目录为空——原子表未装载门槛行（查 Console）");
                _atomRows.SetItems(_atomItems.Count, i => _atomItems[i].H);
                return;
            }

            var pred = CurrentSlotPredicate(out var contextZh);
            if (!string.IsNullOrEmpty(contextZh)) // 顶部上下文提示行（2026-10-23 五分类：告知右栏当前填什么）
                AddTableHint(_atomItems, _libraryList, $"当前：{contextZh}");
            // 限重复隐藏（2026-10-09 隐藏口径 → 2026-10-19 放开修订）：引擎主干/仪典已入组合即隐
            // ——用过即隐不拒绝，换掉/删除后随本刷新回归；其余行可重复不隐。与颜色/搜索/槽位可用性取交集
            var usedRefIds = RestrictedUsedRefIds();
            var payloads = new List<LibPayload>();
            foreach (var row in AllTableRows())
            {
                if (!Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) continue;
                // 锁定关键词（攻击/守卫 + 系统内部行）直接隐藏——不可组合成效果
                if (IsLockedKeywordRow(row)) continue;
                if (usedRefIds.Contains(row.HashId)) continue;
                if (!AtomMatchesColor(row, _colorFilter)) continue;
                // 近似搜索（2026-10-13 语料扩展+多词 AND）：名∪描述∪Tags∪MountKinds∪TargetKinds
                if (!SearchQuery.Match(AtomCorpus(row), _filterName.text)) continue;

                var payload = new LibPayload(type, row);
                // 槽位可用性：与颜色/搜索取交集——只展示当前选中槽真正可落的原子
                if (pred != null && !pred(payload)) continue;
                payloads.Add(payload);
            }
            // 费用升序（2026-10-13 定案）：按表行 ManaList 合计（小数直通口径）排——不计价行合计 0
            // 列最前；同价保表序（OrderBy 稳定排序）
            foreach (var payload in payloads.OrderBy(p => p.Cfg?.ManaList?.Total ?? 0f))
                _atomItems.Add(new TableRowData
                {
                    Name = payload.Cfg.DisplayName,
                    Tag = payload.Cfg.Tags,
                    Meta = payload.Cfg.Description + CounterClearSuffix(payload.Cfg.Tags), // 2026-10-13 定案：指示物类行统一追加清除方式
                    Dot = DotColor(ColorFilter.OfTagsCsv(payload.Cfg.Tags)),
                    Costs = AtomCosts(payload.Cfg),
                    OnClick = () => QuickAdd(payload),
                    H = TableRowH,
                });
            _atomRows.SetItems(_atomItems.Count, i => _atomItems[i].H);
        }

        /// <summary>指示物类原子介绍栏统一后缀（2026-10-13 定案）：清除方式按表行 Tags 四分类标注
        ///（CounterClass 的表侧镜像——衰退=回合末倒数+换区清 / 换区清除=常驻不自动消退 /
        /// 生效自减=生效消耗层·换区不清）。非指示物行返回空串；系统型无玩家表行，不设分支。</summary>
        private static string CounterClearSuffix(string tagsCsv)
        {
            if (string.IsNullOrEmpty(tagsCsv)) return "";
            if (tagsCsv.Contains("衰退型"))
                return "；清除方式：持有者回合结束−1层、归零消失，换区清除";
            if (tagsCsv.Contains("换区清除型"))
                return "；清除方式：换区清除（常驻，不随时间消退）";
            if (tagsCsv.Contains("生效自减型"))
                return "；清除方式：生效时消耗层，不随换区清除（跨区存活），净化可清";
            return "";
        }

        /// <summary>原子单价 → 方格明细（费用构成各色份额取整；序数序=枚举序；null=不计价行）。</summary>
        private static ElementCost AtomCosts(AtomicEffectConfig cfg)
        {
            var cost = cfg?.ManaList;
            if (cost == null || cost.IsZero) return null;
            return cost;
        }

        // ---- 右栏行工厂/绑定（2026-10-21 回收式虚拟化；行视觉以模板为单一来源） ----

        // 2026-10-24 观感改版：字号阶梯下两档 14→18、12→16（行模板/提示行同口径），
        // 行高随字号 44→64、提示行量高用本常量——不动 UiStyle 全局档（仅本屏）
        private const float TableRowH = 64f;
        private const int RowSmallSize = 16; // 提示行/次行小字（原 UiStyle.SmallSize=12）

        /// <summary>提示条目装配（量高：按内容宽测 preferred 高）。</summary>
        private static void AddTableHint(List<TableRowData> items, UiKit.Scroll list, string text) =>
            items.Add(new TableRowData
            {
                IsHint = true,
                Meta = text,
                H = UiKit.MeasureTextHeight(text, list.Content.rect.width - 16f, RowSmallSize) + 12f,
            });

        /// <summary>右栏行工厂路由（kind 分池：0 数据行 / 1 提示行）。两列表行结构同构——闭包区分父级。</summary>
        private UiKit.RecycledList.MakeFn AtomRowFactory(int kind) => kind == 1 ? MakeTableHintRow : MakeTableRow;
        private UiKit.RecycledList.MakeFn EffectRowFactory(int kind) => kind == 1 ? MakeTableHintRowEx : MakeTableRowEx;

        /// <summary>数据行结构：克隆模板「row」——虚拟化接管后模板停靠在 tpl-park（content 只装活行）。</summary>
        private RectTransform MakeTableRow()
        {
            var tpl = UiKit.CloneTemplate("row", UiKit.RecycledList.TemplatePark(_libraryList));
            if (tpl == null)
            {
                Debug.LogError("[EffectComposer] 列表缺 row 模板（tpl-park/content 直属、不激活）——本行跳过；检查 EffectUI.prefab");
                return UiKit.Node("row-miss", _libraryList.Content);
            }
            return tpl;
        }

        private RectTransform MakeTableRowEx()
        {
            var tpl = UiKit.CloneTemplate("row", UiKit.RecycledList.TemplatePark(_effectsList));
            if (tpl == null)
            {
                Debug.LogError("[EffectComposer] 列表缺 row 模板（tpl-park/content 直属、不激活）——本行跳过；检查 EffectUI.prefab");
                return UiKit.Node("row-miss", _effectsList.Content);
            }
            return tpl;
        }

        /// <summary>提示行结构：16 号·暗色·自动换行（原子表侧）。</summary>
        private RectTransform MakeTableHintRow()
        {
            var rt = UiKit.Node("hint", _libraryList.Content);
            var lbl = UiKit.Label("lbl", rt, "", RowSmallSize, UiStyle.TextHint, wrap: true);
            UiKit.StretchInset(lbl.rectTransform, 4f, 4f);
            return rt;
        }

        /// <summary>提示行结构（效果表侧）。</summary>
        private RectTransform MakeTableHintRowEx()
        {
            var rt = UiKit.Node("hint", _effectsList.Content);
            var lbl = UiKit.Label("lbl", rt, "", RowSmallSize, UiStyle.TextHint, wrap: true);
            UiKit.StretchInset(lbl.rectTransform, 4f, 4f);
            return rt;
        }

        /// <summary>右栏行绑定（原子表侧）：meta 文本 + 费用方格（无实例回落 dot 染色）+ 点击监听。</summary>
        private void BindAtomRow(RectTransform row, int index) => BindTableRow(row, _atomItems[index]);

        /// <summary>右栏行绑定（效果表侧）。</summary>
        private void BindEffectRow(RectTransform row, int index) => BindTableRow(row, _effectItems[index]);

        /// <summary>右栏行绑定共用（2026-10-24 改版）：首行=费用方格(32)+名称(18)+标签(16 右对齐)，
        /// 次行=meta 单行小字（溢出省略）；悬浮整行经描述条看全文。行尾操作钮已退役——
        /// 整行点击即装载（与原按钮同源 OnClick）；行宽绑定时钉到列宽（左右对齐有稳定右缘）。</summary>
        private void BindTableRow(RectTransform row, TableRowData item)
        {
            if (item.IsHint)
            {
                var lbl = row.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = item.Meta;
                return;
            }

            // 固定行宽=预制体侧（list content VLG childForceExpandWidth——行模板被拉伸至列宽），
            // 代码不再钉宽（2026-10-24 定案：UI 表现归预制体，好手改）
            var nameT = UiKit.FindDeep(row, "name")?.GetComponent<TMP_Text>();
            if (nameT == null)
                Debug.LogError("[EffectComposer] row 模板缺 name 文本——本行名称丢失；检查 EffectUI.prefab");
            else nameT.text = item.Name ?? "";
            var tagT = UiKit.FindDeep(row, "tag")?.GetComponent<TMP_Text>();
            if (tagT != null) tagT.text = item.Tag ?? "";
            var descT = UiKit.FindDeep(row, "desc")?.GetComponent<TMP_Text>();
            if (descT == null)
                Debug.LogError("[EffectComposer] row 模板缺 desc 文本——本行描述丢失；检查 EffectUI.prefab");
            else descT.text = item.Meta ?? "";

            var rowView = row.GetComponentInChildren<CostSquaresView>(true);
            if (rowView != null)
            {
                if (item.Costs != null && !item.Costs.IsZero) rowView.SetCosts(item.Costs);
                else rowView.Clear();
            }
            else SetPartColor(row, "dot", item.Dot);

            var tbtn = row.GetComponent<Button>();
            if (tbtn == null)
            {
                tbtn = row.gameObject.AddComponent<Button>();
                tbtn.transition = Selectable.Transition.None;
                if (tbtn.targetGraphic == null) tbtn.targetGraphic = row.GetComponent<Image>();
            }
            var onClick = item.OnClick;
            tbtn.onClick.RemoveAllListeners();
            tbtn.onClick.AddListener(() => onClick?.Invoke()); // 2026-10-28 修复：行点击装载（整行曾被误删致右栏不可选）
            UiKit.Described(tbtn, item.Meta); // 悬浮行=完整描述（指示物清除方式等后缀全量）上描述条
        }

        private static void SetPartColor(RectTransform row, string child, Color color)
        {
            var img = UiKit.FindDeep(row, child)?.GetComponent<Image>();
            if (img != null) img.color = color;
        }

        /// <summary>右栏·光环库（点击换选——2026-10-23 五分类双面）：
        /// 连接光环=属性光环（位 3 表驱动：设置攻击力/生命力系统行）+ 关键词光环（CanMountAsAura——
        /// 默认可−消耗型拉黑），**不列规则光环行**（仪典属规则光环）；
        /// 规则光环=仪典行（位 6）——只列规则光环。单槽定案：库点击即整组替换上一个选中；
        /// 近似搜索按显示名过滤。</summary>
        private void RefreshAuraLibrary()
        {
            // 宿主光环闸（2026-10-15 改版）：光环=结界专属——生物（禁挂改版）/法术（只挂主动）不出库
            if (HostBlocksAura)
            {
                AddTableHint(_atomItems, _libraryList, _editingCard.Supertype == Cardtype.Creature
                    ? "生物不可挂光环（2026-10-15 改版——光环专属结界）"
                    : "法术只挂主动效果（施放时抉择其一），不可挂光环");
                return;
            }

            void AddRow(string title, string meta, UIColor color, Action onAdd)
            {
                // 近似搜索（2026-10-13 多词 AND）：标题∪meta
                if (!SearchQuery.Match(title + "\n" + meta, _filterName.text)) return;
                _atomItems.Add(new TableRowData
                {
                    Name = title,
                    Meta = meta,
                    Dot = DotColor(color),
                    OnClick = onAdd,
                    H = TableRowH,
                });
            }

            void AddAura(LinkAuraData entry)
            {
                // 2026-10-09 单槽定案：条目整组替换（规则原子随选清空——库点击即替代上一个选中）；
                // 场地光环面（2026-10-30 属性降低入场地光环）：条目恒 scope=对方、无箭头
                _graph.steps?.Clear();
                _graph.header.LinkAuras = new List<LinkAuraData> { entry };
                if (_mode == ComposeMode.RangeAura) _graph.header.ArrowDirections = 0;
                RefreshSlots(); // 左栏槽卡/校验/费用随选择刷新（右栏行保持——可连点换选）
            }

            // 规则级光环：落 steps 为 ModifyGameRule 登场原子（str=规则短名）——引擎 RuleAuraSystem
            // 多槽（2026-10-07 唯一性改版：异名共存、同名在场禁打出）；作用范围=entry.value
            //（RuleAuraScope：缺省极性驱动——2026-10-30 收窄：负面族(负极性)缺省对方、增益族缺省己方、
            // 中性缺省双方；极性 ±1 行单侧档唯一、卡面不可改，计价 ×1.5）；
            // 不占 LinkAura 连接位（无箭头）。新选即整组替换（属性降低条目同槽互斥）
            void AddRuleAura(AtomicEffectConfig cfg)
            {
                // 阶段D（2026-10-31）：规则 id 从行类型派生（RuleAuraComponents 映射单源）——
                // 新型行不再写 str（投放/同名闸/构筑唯一槽全走类型）；表 EffectType 未翻新行
                //（仍=ModifyGameRule）回落 RuleAuraIds 显示名映射并写 str（handler 过渡回落）
                Enum.TryParse<AtomicEffectType>(cfg.EnumName ?? "", out var rt);
                var rid = RuleAuraComponents.RuleIdOfType(rt)
                          ?? (RuleAuraIds.TryGetValue(cfg.DisplayName, out var r) ? r : cfg.DisplayName);
                bool fixedBoth = RuleAuraComponents.IsFixedBothRule(rid); // 固定双方行不提供单侧选择（2026-10-29）
                _graph.steps = new List<EffectStepData> { new EffectStepData { kind = 0,
                    atomic = new AtomicEffectEntry { refId = cfg.HashId,
                        value = fixedBoth ? (int)RuleAuraScope.Both
                            : (cfg.Polarity <= -0.5f ? (int)RuleAuraScope.Opponent
                            : cfg.Polarity >= 0.5f ? (int)RuleAuraScope.Own
                            : (int)RuleAuraScope.Both),
                        str = rt == AtomicEffectType.ModifyGameRule ? rid : null } } };
                _graph.header.LinkAuras = null; // 仪典与属性降低条目同槽互斥——落仪典清条目
                _graph.header.ArrowDirections = 0; // 规则级无箭头（MakeAuraPanel 范围面分支同置——落位即写一致）
                RefreshSlots();
            }

            // ---- 场地光环面（2026-10-30 自规则光环改名）：仪典行（位 6）+ 属性降低条目（自连接光环挪入） ----
            if (_mode == ComposeMode.RangeAura)
            {
                AddTableHint(_atomItems, _libraryList, "当前：仪典/属性降低——点击选择（新选即替换；仪典作用面随极性钉死，属性降低仅对对方生物生效）");
                foreach (var r in AllTableRows())
                {
                    var rule = r;
                    if (!IsRuleAuraRow(rule)) continue;
                    AddRow($"场地光环·{rule.DisplayName}",
                        $"{rule.Description}——{RuleAuraScopeOf(rule)}，同名在场时不可再打出（异名可共存），载体离场/被无效即失效",
                        ColorFilter.OfTagsCsv(rule.Tags),
                        () => AddRuleAura(rule));
                }
                // 属性降低（2026-10-30 挪入场地光环）：源行=属性减少（AddMinusOne）光环化，每行拆
                // 攻/生/属性三档；条目恒仅对方、双方各一份可叠加（不入仪典唯一性——LinkAura 多源累加）
                var minusRow = AllTableRows().FirstOrDefault(x =>
                    Enum.TryParse<AtomicEffectType>(x?.EnumName ?? "", out var t)
                    && t == AtomicEffectType.AddMinusOne);
                if (minusRow != null)
                {
                    var mcolor = ColorFilter.OfAtomic(AtomicEffectType.AddMinusOne);
                    foreach (var (statZh, statId) in new[]
                         { ("攻击力", "Power"), ("生命值", "Life"), ("属性", "Both") })
                    {
                        string zh = statZh, sid = statId;
                        AddRow($"属性降低·{zh}−1",
                            $"{zh}−1——受光环影响的对方生物持续降低（数值可在条目卡调大）；双方各挂一份可叠加，载体离场/被无效即失效",
                            mcolor, () => AddAura(new LinkAuraData { stat = sid, value = -1, scope = 3 }));
                    }
                }
                return;
            }

            // ---- 连接光环面（2026-10-30 只留增益）：属性 + 关键词条目（仪典/属性降低不入——切「场地光环」配） ----
            AddTableHint(_atomItems, _libraryList, "当前：增益条目——点击换选（新选即替换上一个；只作用于连接箭头指向的单位，箭头在光环槽底部必选）");

            // 属性光环（2026-10-07 深夜改源；2026-10-08 库行细分定案；2026-10-30 只留 +1——
            // 属性减少挪场地光环）：一般效果「属性增加」（原子表位 3 行）的光环化——
            // 每源行拆 攻/生/属性 三行（种类=库行结构即语义）；计价取源行走通用光环化公式。
            foreach (var r in AllTableRows())
            {
                var cfg = r;
                if (IsRuleAuraRow(cfg) || !ComposerCatalog.HasMountBit(cfg, MountKind.LinkAura)) continue;
                if (!Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var st)) continue;
                int sign; UIColor color;
                switch (st)
                {
                    // 色取表行派生单源（2026-10-29 P4 色序清尾：白/黑 UIColor 成员已删，±1 行表色=灰）
                    case AtomicEffectType.AddPlusOne: sign = 1; color = ColorFilter.OfAtomic(st); break;
                    default: continue; // 位 3 非属性行不进光环面；属性减少（−1）已挪场地光环
                }
                foreach (var (statZh, statId) in new[]
                     { ("攻击力", "Power"), ("生命值", "Life"), ("属性", "Both") })
                {
                    string zh = statZh, sid = statId;
                    AddRow($"属性光环·{zh}+{sign}",
                        $"{zh}+{sign}——源行光环化计价；只作用于箭头指向的生物（无作用面档/全体档），属性只作用于生物",
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
        }

        /// <summary>点击库行 = 替换左侧选中槽（2026-10-23 五分类路由）；选中槽不合适该原子时 toast 提示。
        /// 错边原子不在效果区落位（代价栏已上移卡组合层）。光环双形态/门槛选中态不走本口
        ///（条目/仪典/门槛行各有专属 OnClick）。</summary>
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
                    if (!ParallelCanDrop(payload)) { ShowToast("该原子不可作并列主干（引擎行请切自由分支/场地光环不入效果栏）"); break; }
                    if (payload.IsKeywordAtom) ApplyKeywordHeader();
                    ReplaceParallel(InferredTrunkEntry(payload));
                    RefreshLibrary(); // 全局唯一（隐藏口径）：落位原子从库中隐藏——库随落位刷新
                    break;
                case ComposeMode.FreeBranch:
                    if (_selSlot == SelKind.Reward)
                    {
                        var fb = BranchOfSlot(0);
                        if (fb == null) { ShowToast("主干未定——先到右侧选引擎主干行"); break; }
                        if (!RewardCanDrop(payload, fb)) { ShowToast("选中块=奖励——挂载位不合或超出预算（≤5 费）"); break; }
                        if (payload.IsKeywordAtom) ApplyKeywordHeader();
                        DropBranchReward(payload, 0);
                    }
                    else
                    {
                        if (!payload.IsEngineTrunk) { ShowToast("自由分支主干=引擎行或模拟/变形（倒计时/拼点/运势…）——其余原子请切并列/有限分支"); break; }
                        ReplaceBranchTrunk(InferredTrunkEntry(payload), keepBranch: false); // 引擎行自动挂 Engine 载荷
                    }
                    RefreshLibrary();
                    break;
                case ComposeMode.LimitedBranch:
                    if (_selSlot == SelKind.Gate) break; // 门槛列表分流——行点击走 ApplySituationGate
                    if (_selSlot == SelKind.Reward)
                    {
                        var lb = BranchOfSlot(0);
                        if (lb == null) { ShowToast("先选门槛——奖励挂在门槛下（点门槛卡到右栏选择）"); break; }
                        if (!RewardCanDrop(payload, lb)) { ShowToast("选中块=奖励——挂载位不合或超出门槛预算"); break; }
                        if (payload.IsKeywordAtom) ApplyKeywordHeader();
                        DropBranchReward(payload, 0);
                    }
                    else
                    {
                        if (payload.IsEngineTrunk) { ShowToast("引擎行是自由分支主干——请切「自由分支」形态"); break; }
                        if (!ParallelCanDrop(payload)) { ShowToast("该原子不可作主干（场地光环/门槛行不入效果栏）"); break; }
                        if (payload.IsKeywordAtom) ApplyKeywordHeader();
                        ReplaceBranchTrunk(EntryForEffectSlot(payload), keepBranch: true); // 换主干保留门槛/奖励
                    }
                    RefreshLibrary();
                    break;
            }
        }

        /// <summary>库行 → 效果槽条目：关键词/指示物类统一为赋予形态
        ///（kinds 按 GrantTargetKinds 推导——关键词=双方生物、指示物=任意卡 {1..8}；
        /// 关键词另带 str=运行时关键词 id），其余原样；跳过三阶段行（2026-10-29 阶段3a）
        /// 按 EnumName 查 SkipTurnIds 写相位档 str。</summary>
        private static AtomicEffectEntry EntryForEffectSlot(LibPayload payload)
        {
            var entry = payload.Entry();
            // 跳过三阶段行：str=相位档（End/Standby/Main）——SkipTurnHandler 消费；
            // 整回合行不在字典，str 保持空=Whole 档
            if (payload.Type == AtomicEffectType.SkipTurn
                && payload.Cfg != null
                && SkipTurnIds.TryGetValue(payload.Cfg.DisplayName, out var skipMode))
                entry.str = skipMode;
            // 延时保护行（2026-10-30 改版）：str=Mode 档——GrantProtectionHandler 消费
            //（表行不带 str，与 SkipTurn/RuleAura 同法；手写 Effects.json 须显式写 str；
            // 全体档 Mode|All 已删——指示物只作用于挂载单位）
            if (payload.Type == AtomicEffectType.GrantProtection
                && payload.Cfg != null
                && ProtectionIds.TryGetValue(payload.Cfg.DisplayName, out var protection))
                entry.str = protection;
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
        /// 通常原子→无分支（有限分支默认无门槛——条件由槽内门槛卡手选）。关键词形态沿用。</summary>
        private static AtomicEffectEntry InferredTrunkEntry(LibPayload payload)
        {
            var entry = EntryForEffectSlot(payload);
            var engineKind = ComposerCatalog.EngineKindOf(payload.Type);
            if (engineKind != BranchEngineKind.None)
            {
                int param;
                if (ComposerCatalog.IsDerivedThresholdEngine(engineKind) || engineKind == BranchEngineKind.Countdown)
                    param = 0; // 派生门槛（拼点/死亡计数/元素充盈）参数=死数据；倒计时 0=自动换算
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

        /// <summary>光环编辑面板（2026-10-23 五分类双面；2026-10-30 改版）：
        /// 连接光环=光环槽（增益条目卡——aura-card 克隆、编辑走卡内 editor，恒箭头档无作用面下拉）
        /// + 槽底连接箭头盒（arrow-picker 预制体实例，必选≥1 支），条目库点击整组替换；
        /// 场地光环=场地光环槽（仪典卡+属性降低条目卡——作用面随极性钉死/属性降低恒对方）——无箭头。
        /// 两面共用 tpl-aura-slot 模板；单槽单选——库点击即替换上一个选中。</summary>
        private void MakeAuraPanel(RectTransform parent)
        {
            var h = _graph.header;
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
            else { auraTitle.text = _mode == ComposeMode.RangeAura ? "场地光环（仪典/属性降低）" : "光环槽（增益条目）"; UiKit.Size(auraTitle, fw: 1f); }
            MarkSelected(slot, SelKind.Parallel, 0);
            SelectOnClick(slot, SelKind.Parallel, 0, false);

            // ---- 场地光环面（2026-10-30 属性降低入场地光环）：仪典步 + 负属性条目（无箭头） ----
            if (_mode == ComposeMode.RangeAura)
            {
                h.ArrowDirections = 0; // 场地光环恒无箭头（仪典按作用面、属性降低按对方侧——写盘一致）
                h.LinkAuras ??= new List<LinkAuraData>();
                // 条目归属归一：本面只留负属性条目（增益/关键词条目属连接光环——防御旧档/旁路），
                // scope 恒对方（3）——极性 −1 单侧口径
                if (h.LinkAuras.Count > 0)
                {
                    h.LinkAuras = h.LinkAuras.Where(a => a != null
                        && !string.IsNullOrEmpty(a.stat) && a.value < 0).ToList();
                    foreach (var a in h.LinkAuras) a.scope = 3;
                }
                MakeHint(parent, "场地光环＝仪典/属性降低：载体入场即挂，离场/被无效即失效——不占连接位；"
                    + "仪典同名全局唯一（作用面随极性钉死：增益仅己方、减益仅对方，单侧档计价 ×1.5）；"
                    + "属性降低仅对方生物生效，双方各挂一份可叠加");
                int ritual = 0, foreign = 0;
                foreach (var s in _graph.steps ?? new List<EffectStepData>())
                {
                    if (s?.kind != 0 || s.atomic == null) { if (s != null) foreign++; continue; }
                    if (!IsRitualStepRow(AtomicEffectTable.GetByHashId(s.atomic.refId))) { foreign++; continue; }
                    if (ritual == 0) MakeAuraRuleCard(slot, s.atomic); // 仪典卡=摘要+作用面（光环专用卡）
                    ritual++;
                }
                for (int i = 0; i < h.LinkAuras.Count; i++)
                    MakeAuraEntryCard(slot, i); // 属性降低条目卡（数值可调——恒负、恒对方）
                if (ritual == 0 && h.LinkAuras.Count == 0)
                    MakeHint(slot, "尚无内容——右侧仪典库/属性降低条目点击选择（新选即替换）");
                else if (ritual > 1)
                    MakeHint(slot, $"已装 {ritual} 条仪典——仪典同名全局唯一、通常一条就够（多的入场时会被送墓）");
                if (foreign > 0)
                    MakeHint(slot, $"另有 {foreign} 步非仪典内容（历史数据）——本面不展示，保存时原样保留");
                _slotZones.Add(MakeZone(slot, () => RuleAtomCount(_graph) == 0 && ValidAuraCount(h.LinkAuras) == 0
                    ? "场地光环至少配一条仪典或属性降低条目——右侧点击选择" : null));
                return;
            }

            // ---- 连接光环面（2026-10-30 只留增益）：条目+箭头（仪典/属性降低不入——切「场地光环」配） ----
            MakeHint(parent, "光环＝持续增益：作用对象=箭头指向格的当前占据者（断链/离场即失效）——"
                + "无作用面档/全体档；关键词条目恒含角色（指向角色格=投递角色），属性条目只作用于生物。");
            h.LinkAuras ??= new List<LinkAuraData>();
            // 条目归属归一：本面只留增益/关键词条目（负属性条目属场地光环——防御旧档/旁路），
            // 恒箭头档（scope=0，作用范围下拉已随 2026-10-30 删退役）；空条目一并剔除
            if (h.LinkAuras.Count > 0)
            {
                h.LinkAuras = h.LinkAuras.Where(a => a != null
                    && (string.IsNullOrEmpty(a.stat) || a.value > 0)).ToList();
                foreach (var a in h.LinkAuras) a.scope = 0;
            }
            foreach (var s in _graph.steps ?? new List<EffectStepData>())
                if (s?.kind == 0 && s.atomic != null)
                    MakeReadonlyStepBadge(slot, s); // 历史仪典/原子步只读（连接面不编辑——切场地光环处理）
            for (int i = 0; i < h.LinkAuras.Count; i++)
                MakeAuraEntryCard(slot, i);
            if (h.LinkAuras.Count == 0)
                MakeHint(slot, "尚无光环——右侧增益库点击选择（属性/关键词；新选即替换旧选）");
            MakeHint(slot, "条目费=行锚×量级×箭头数（×1.2 按箭头数累乘；关键词条目恒含角色=箭头×5 档）；光环默认持续——不收持续费");

            // ---- 连接箭头（2026-10-09 迁入光环槽底部——跨级克隆 tpl-arrows 进 slot；
            //      存在连接方向档条目才显示；作用面档条目不依赖箭头） ----
            _arrowCountLabel = null; // 随面板重建作废（防悬挂引用写已销毁对象）
            if (AnyArrowModeEntry(h))
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
                    // 校验：连接光环必须搭配至少一支箭头（无箭头=永无受益者；2026-10-30 条目恒箭头档——必选）
                    _slotZones.Add(MakeZone(arrowsBox, () => (HexDirection)h.ArrowDirections == HexDirection.None
                        ? "未选箭头——连接光环必须搭配至少一支箭头（无箭头=永无受益者）；场地光环（仪典/属性降低）则不需要箭头" : null));
                }
            }
            // 校验：至少一条有效光环内容 + 关键词条目须位 5 可挂（仪典唯一性属规则光环面）
            _slotZones.Add(MakeZone(slot, () =>
            {
                if (ValidAuraCount(h.LinkAuras) == 0)
                    return "无有效光环内容——至少配一条条目（空条目保存时剔除）";
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
            // 阶段D（2026-10-31）：规则 id 从行类型派生优先（RuleAuraComponents 映射）——
            // 新型行不写 str；未翻新行回落 atom.str
            var rrid = Enum.TryParse<AtomicEffectType>(rcfg?.EnumName ?? "", out var rt)
                ? RuleAuraComponents.RuleIdOfType(rt) ?? atom.str
                : atom.str;
            if (RuleAuraComponents.IsFixedBothRule(rrid))
            {
                // 固定双方规则（2026-10-29 1b 定案）：许可/禁止族 UI 不提供单侧选择——
                // 实例 value 恒写 Both、隐藏作用范围下拉行（改写族照旧走下拉）
                atom.value = (int)RuleAuraScope.Both;
                if (range != null) range.gameObject.SetActive(false);
            }
            else if (range == null || rangeDd == null || rcfg == null)
            {
                Debug.LogError("[EffectComposer] 仪典卡缺 actuating-range/aura-kinds 或表行——作用范围不可编；检查 EffectUI.prefab");
            }
            else
            {
                UiKit.FindDeep(range, "player")?.gameObject.SetActive(false);
                var opts = AllowedScopesOfPolarity(rcfg.Polarity);
                if (opts.Count == 1)
                {
                    // 极性 ±1 行（2026-10-30 收窄定案）：作用面钉单侧——+1 仅己方 / −1 仅对方，
                    // 无可选项：隐藏下拉行、value 直写极性侧（单侧档计价 ×1.5，见 CostDerivation）
                    atom.value = opts[0] - 1;
                    range.gameObject.SetActive(false);
                }
                else
                {
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
                        "作用范围：仪典对哪一侧生效——中性改写行三档可选（±1 行已钉单侧）。",
                        i =>
                        {
                            if (i < 0 || i >= opts.Count) return null;
                            int v = opts[i];
                            return v == 1 ? "己方：只为你（光环控制者）一侧生效（单侧档计价 ×1.5）。"
                                : v == 3 ? "对方：只为对手一侧生效（单侧档计价 ×1.5）。"
                                : "双方：两侧都生效（对称面，表价即实价）。";
                        });
                }
            }
        }

        /// <summary>光环条目卡（2026-10-07 光环专用卡；2026-10-30 作用范围退役）：aura-card 直编辑——
        /// head 摘要+删除；field-value=属性数值（关键词条目隐藏）。连接光环面：条目恒箭头档
        ///（scope=0，作用对象=箭头指向格占据者）、数值恒正（只留增益）；场地光环面（2026-10-30
        /// 属性降低挪入）：条目恒仅对方（scope=3）、数值恒负可调。作用范围下拉行两面皆隐藏；
        /// 条目种类（攻/生/属性、关键词选择）随 2026-10-08 定案在右栏库行细分。</summary>
        private void MakeAuraEntryCard(RectTransform slot, int index)
        {
            var aura = _graph.header.LinkAuras[index];
            var card = CloneAuraCard(slot);
            if (card == null) return;
            bool isStat = !string.IsNullOrEmpty(aura.stat);
            bool field = _mode == ComposeMode.RangeAura; // 场地光环面：属性降低条目（恒负·恒对方）

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

            // ---- 数值（属性条目）：连接面恒正（只留增益，≤0 收 1）；场地面恒负（≤−1 收 −1） ----
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
                            if (int.TryParse(input.text, out var v))
                            {
                                aura.value = field ? Math.Min(v, -1) : Math.Max(v, 1);
                                RefreshSlots();
                            }
                            else input.text = aura.value.ToString();
                        });
                        UiKit.Described(input, field
                            ? "数值：受光环影响的对方生物持续降低的量——恒为负数（至少 −1），数值越大降得越多；"
                              + "双方各挂一份时叠加生效。"
                            : "数值：光环给箭头指向单位的属性加成——恒为正数（只留增益），数值越大费用越高。"
                              + "多支箭头指向同一个单位时，每一支都各自生效一次。");
                    }
                }
            }

            // ---- 作用面（2026-10-30 下拉退役）：连接面恒箭头档（scope=0）、场地面恒仅对方（scope=3）——
            //      行隐藏；数值钳制已保证极性不翻转（无门控收敛需求） ----
            aura.scope = field ? 3 : 0;
            UiKit.FindDeep(card, "actuating-range")?.gameObject.SetActive(false);
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

        /// <summary>光环计价预览卡：效果级光环无宿主卡——临时 CardData 承条目＋预选箭头
        ///（2026-10-12 裁定翻转旧「预览不计箭头」口径：箭头档条目在预览同样按箭头位数计费）。
        /// 顶栏 AuraTopCostText 同源只读（2026-10-07 面板 aura-cost 行退役）。</summary>
        private CardData AuraPreviewCard()
        {
            var h = _graph.header;
            return new CardData
            {
                CardName = "AURA_PREVIEW",
                Supertype = Cardtype.Enchantment,
                ArrowDirections = (HexDirection)h.ArrowDirections,
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

            // 充能形态（2026-10-31 P12）：单原子/无分支/无域——违规=保存禁用（无专区，只进描述条）
            string chargeErr = ChargeShapeError();
            if (chargeErr != null) errs.Add(chargeErr);

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
            // 保存显隐（2026-10-24 改版）：按钮在左栏组合侧——校验全合规才出现（出现即可点；
            // 空组合本就被「效果为空/主干为空」红框拦下，天然涵盖"必要填充完成"口径）
            if (_saveBtn != null) _saveBtn.gameObject.SetActive(!any);
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
            if (IsAuraMode(_mode))
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
            // 2026-10-16 强制删除机制：旧版仍被卡引用时弹窗二选一——「保留旧版」新旧共存（引用卡
            // 不受影响）/「删除旧版」引用卡断链（同删效果口径）。
            // 预设效果只读载入（2026-10-28 定案）：与载入快照同内容（无文名指纹）→拦存，
            // 防把预设原样拷进主表造语义重复条目；内容有变=新 id 自然落主表为独立新效果
            bool fromPreset = !string.IsNullOrEmpty(_presetSourceId);
            if (fromPreset && PresetContentHash(_graph) == _presetSnapshotHash)
            {
                ShowToast("内容未修改——预设效果只读；调整内容后再保存会另存为新效果");
                return;
            }

            var path = EffectLibrarySerializer.Save(_graph); // Save 内计算/回填 graph.id
            string replacedId = null;
            if (!string.IsNullOrEmpty(_loadedFromLibraryId) && _loadedFromLibraryId != _graph.id)
            {
                replacedId = _loadedFromLibraryId;
                var refs = ReferenceAudit.CardsReferencingEffect(replacedId);
                if (refs.Count > 0)
                {
                    // 2026-10-18 弹窗统一：TipPanel 二选一，fire-and-forget——保存流程不被弹窗阻塞
                    //（同旧 ConfirmBox 回调时序：此处立即闭链继续保存，旧版去留弹窗后置处理）。
                    HandleReplacedLegacyAsync(replacedId, refs).Forget();
                    _loadedFromLibraryId = null; // 闭链；旧版去留交给弹窗（本保存提示走共存口径）
                    replacedId = null;
                }
                else
                {
                    EffectLibrarySerializer.DeleteById(replacedId);
                    _loadedFromLibraryId = null;
                }
            }
            if (path != null) TutorialCreationFlow.NotifyEffectsChanged(); // 第三课走查步检测（未开课零行为）
            if (fromPreset && path != null)
            {
                // 首次另存成功：预设只读态闭链——已落玩家库的新条目转为常规库条目（后续可改可删）
                _loadedFromLibraryId = _graph.id;
                _presetSourceId = null;
                _presetSnapshotHash = null;
                ShowToast($"已另存为新效果：{_graph.name}（{_graph.id}）——原预设不变");
                if (_rightMode == RightMode.Effects) RefreshEffectsList();
                UpdateDeleteState();
                return;
            }
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
            // 形态防御（2026-10-23 五分类）：并列=纯两原子（分支载荷一律拆）；连接光环=steps 恒空
            //（条目在 LinkAuras）；规则光环=只留仪典步（条目/杂步属其他形态）
            if (_mode == ComposeMode.Parallel)
                foreach (var s in saved)
                    if (s?.kind == 0 && s.atomic != null) s.atomic.branch = null;
            else if (_mode == ComposeMode.LinkAura)
                saved.RemoveAll(s => s.kind == 0);
            else if (_mode == ComposeMode.RangeAura)
                saved.RemoveAll(s => s.kind == 0
                    && !IsRitualStepRow(AtomicEffectTable.GetByHashId(s.atomic?.refId)));
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
            if (!IsAuraMode(_mode))
            {
                effect.ArrowDirections = 0;
                effect.LinkAuras = null;
            }
            else if (_mode == ComposeMode.RangeAura || HasRuleAuraStep(_graph))
            {
                effect.ArrowDirections = 0; // 规则光环/规则级光环不占连接位（UI 已关箭头——写盘一致）
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
                _ => UiStyle.DotGray, // Gray/All（白/黑已随 2026-10-29 P4 色序清尾删除）
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
            // 2026-10-05 晚间回表定案：引擎主干行回表（位 5）——填槽即自由分支。

            private HashSet<MountKind> Mounts => MountKindExtensions.ParseCsv(Cfg?.MountKinds ?? "");

            /// <summary>关键词类原子（位 0 Keyword）——库行点击同关键词面板：恒为他人赋予形态
            ///（kinds=GrantTargetKinds 覆写 + str=关键词 id）。</summary>
            public bool IsKeywordAtom => Cfg != null && Mounts.Contains(MountKind.Keyword);

            /// <summary>指示物类原子（位 1 Counter，2026-10-07 与关键词同型）：落槽=赋予任意卡形态
            ///（kinds=GrantTargetKinds 覆写 {1..8}；持续由 CounterSpec 承载，无关键词 id/头部惯例）。</summary>
            public bool IsCounterAtom => Cfg != null && Mounts.Contains(MountKind.Counter);

            /// <summary>引擎主干行（位 5）：填入槽即自由分支（引擎参数+奖励槽直出）；
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
            /// 与 CardEffectConverter 代价校验同口径（2026-10-28 P1：WrongSide 删除——侧别判定内联，
            /// 行为等价；代价栏合法载体=p<0 锁己方，其余形态 UI 处置归 P4）。</summary>
            public bool IsWrongSideOnly
            {
                get
                {
                    if (Cfg == null) return false;
                    float p = UnityEngine.Mathf.Clamp(Cfg.Polarity, -1f, 1f);
                    var kinds = Cfg.GetTargetKindList();
                    int side = CostDerivationService.SideLock(kinds);
                    if (p > 0f) return side == 1;   // 有益锁对方（对对手有益）
                    if (p < 0f) return side == -1;  // 有害锁己方（自伤——代价合法载体）
                    return side != 0;               // 零极性单侧锁
                }
            }

            /// <summary>落槽生成的新原子条目（引用型：refId=表行ID，value=1）。</summary>
            public AtomicEffectEntry Entry()
                => new AtomicEffectEntry { refId = Cfg?.HashId, value = 1 };
        }
    }
}
