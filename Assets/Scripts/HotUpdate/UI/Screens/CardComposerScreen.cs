using Cysharp.Threading.Tasks;
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
    /// 卡牌合成界面（2026-10-01 预制体化；2026-10-16 prefab 用户重做对接）：
    /// 顶栏=返回/类型三钮（biology 生物·barrier 结界·spell 法术，取代旧下拉）/保存/删除；
    /// 左栏=美术画廊（立绘点选）+desc-strip 悬浮讲解条；
    /// 中栏=静态 ACard 卡面**即编辑器**（form-panel/ACard：卡名/攻击/生命/耐久为输入框直写、
    /// type 区方格=自动算费终值〔悬浮 desc-strip 看构成明细〕、cost 区=代价 payload、
    /// effect 区=主动效果槽行、passives 区=自动效果槽行、keys 区=本体关键词）；
    /// 右栏=btn-switch-table 切换「效果库(list-library) ↔ 卡池(list-effects)」——
    /// 效果库行点击=填入中间选中槽（形状闸/时机闸保留）；卡池行点击=整卡载入中间编辑。
    ///
    /// 2026-10-16 定案（prefab 重做配套）：
    /// · toast/标签输入/建议档位+采纳/算费明细列表/六色声明费手动行 全部退役——
    ///   操作反馈走 Console+desc-strip；费用=内容推导分布自动直写（CardCostCalculator.SuggestedCost
    ///   =DerivedCost，2026-10-22 恒推导后无声明回显分支），构成明细改 type 区悬浮；
    /// · 效果槽操作收窄=选中+删除（无调序、无行内编辑跳转——编辑=删后重挂，库级编辑走效果库行「编辑」）；
    /// · btn-delete=删除卡池中的当前卡：被卡组引用时弹窗提示、允许强制删除（ReferenceAudit 反查），
    ///   强删后卡组界面按 DeckCompositionValidator 标记断链不可用；
    /// · tpl-root 七模板已随 prefab 重做消失：动态控件一律 UiKit 原语；列表行从各 list 自带的
    ///   inactive row 模板克隆（Image+Button+CostSquares+meta）。
    ///
    /// 数据模型（2026-09-24 关键词引用化定案）：卡只做卡层增量（名称/类型/身材/耐久），效果与关键词
    /// 全部走 effectIds 引用。编辑旧卡保存=覆盖替换（旧卡被卡组引用则拦截）。
    ///
    /// 2026-10-16 槽位化定案（四 zone 按钮 + 隐藏模板）——同日晚二次改版（prefab 用户重做配套）：
    /// · cost/effect/passives/keys 四区节点降级为**单槽模板**（prefab 内恒 inactive）：每个槽行=克隆模板
    ///   （text 标题＋des 内容＋delete 删除钮），效果全文写进 des；tpl-slot / tpl-kwchip 模板已随 prefab 消失；
    /// · add-slot 四钮（addcost/addeffect/addpassives/addkeys）＝新建空槽：容量按型闸（add 按钮禁用/隐藏），
    ///   新建即选中该空槽（右栏自动切效果库并按槽类型过滤）；点槽行＝选中/取消，右栏候选点击填入对应槽；
    /// · 容量按型：生物=代价1+主动1+自动1+关键词3（每关一个攻/守 +1）；法术=主动1~3（速度加成=3−槽数）；
    ///   结界=光环恰好 1（必填，规则或连接；保存闸）+自动0~2（passives 槽容量 3=光环1+自动2）；
    ///   代价三类卡通用；关键词生物专属（切型即清）；
    /// · basicability 区（具备攻击/具备守卫/具备充能 Toggle）：**正向绑定资格位，默认不带**
    ///   （2026-11-06 opt-in 改版——攻/守/充能三系统动作统一「添加了才有」；ac/dc 生物专属、rc 全卡型；旧 NoAttack/NoGuard 反相绑定退役）；
    /// · 名称可缺省——留空保存=用生成的卡 ID 当卡名；生物属性必填（输入框留空=未填拦保存）；
    /// · type 区 ID 钮：仅在保存过卡后出现（num 显卡 ID，点击复制——方便衍生物填 ID）；
    /// · type 区 "CostSquares " 文本节点＝卡费文字公式（方格退役，口径参考效果组合界面费用公式）；
    /// · 右栏类别 chips 退役——颜色多选+模糊串搜索两列表通用。
    ///
    /// 2026-10-23 v2 三栏重排（对齐卡组 v2 观感）：左 520=图库（3 列 160 格+搜索+金框选中）/
    /// 中 800=卡面 360×648 只读预览+表单两列网格（名称/类别/费用｜攻/血/耐久 带图标输入）+
    /// 追加四钮 88×32/右 568=行模板（44 高·26 圆片费标·f14 名称·右侧 meta 标签·操作钮）；
    /// 行描述全部走 desc-strip 悬停（行内不再塞全文）；chips 选中=ChipActive 底+描边。
    ///
    /// 2026-10-27 三栏占比重定（prefab 手改 400/800/650 配套）：左 400=图库两列 184 格（内容区 376=2×184+8 恰满）/
    /// 右 650=字阶对齐 DeckUI v2 三档（25=按钮·chips/18=行名·输入框/16=meta·提示·费用数字），
    /// 行规格同步（行高 56·列表间距 4·表头行 44 容纳 25 号切换钮·chips 72×40）。
    ///
    /// 2026-11-06 布局改版配套（prefab 手改：编辑控件挪出 ACard 实例）：
    /// · 中栏拆两树——CardPreView 下 ACard 实例=只读实时预览（AddComponent&lt;ACardView&gt;+FromCardDataFull
    ///   静态快照，DeckBuilder preview 同款）；编辑控件=CardPreView 直属 fields（name/费用公式/basicability/
    ///   attribute）+form-scroll（effect/passives/keys 三槽模板）——直查路径绑定防与卡面内部同名节点串线；
    /// · 代价槽整口退役（prefab 删 cost 模板+addcost；编辑口=效果合成器「支付代价」门，2026-11-06 定案）；
    /// · 美术图库：法术/结界=Title 默认图一张（Art/Icons/Title.png，与 ACard.prefab 烘焙占位同图），
    ///   切型自动选中（生物清回占位）；
        /// · setderivant 槽行钮=衍生物/变形槽新入口（旧 derive 行模板+addderive+放大预览退役）：
        ///   效果含 SummonToken/GrantMorph 原子才显形，点击开选卡器写原子 str=定义期直绑。
        ///
        /// 2026-11-06 二次四改：
        /// · 卡池行缩略写法统一到 CardRowText（2026-11-13 定案「费用｜名称｜类别｜基础属性参数」：
        ///   费用=消耗+代价；生物 属性x/x、结界/场地 耐久x、法术 速度x——与卡组构筑界面同源）；
        /// · 中栏卡面单击=全屏放大（DeckBuilder OpenZoom 同款：运行时遮罩+360×648 槽×5/3 缩放，
        ///   点遮罩还原；CardUI.prefab 无烘焙节点——遮罩运行时建）；
        /// · 图库占位项退役（画廊只列真实可选图；未选立绘=无高亮格，卡面回落烘焙占位图）；
        /// · 效果库补预设段（AppendPresetEffects）：预组卡组效果只读并入——可引用挂载、「编辑」
        ///   跳效果合成器预设只读态（修改保存=另存新效果，与预设同内容拦存；源档不可覆盖不可删）。
        /// </summary>
    public sealed partial class CardComposerScreen : UIScreen
    {
        protected override string PrefabName => "CardUI";
        protected override string RootName => "card-composer";
        // 业务类型（UI 维度，非裸 Cardtype）：生物 / 法术（一次性单发）/ 结界（耐久体）。
        // 速度由免费额度按卡型默认分配（2026-10-15 定案）：法术=效果槽1+速2 / 结界=槽1+耐久+速1 / 生物=属性+攻+守。
        private enum CardKind { Creature, Spell, Enchantment }

        // 顶栏类型钮（prefab 重做：三图标按钮取代 dropdown-cardtype；选中态=Outline 描边同 chips 视觉）
        private static readonly (CardKind kind, string zh, string node)[] KindDefs =
        {
            (CardKind.Creature, "生物", "biology"),
            (CardKind.Enchantment, "结界", "barrier"),
            (CardKind.Spell, "法术", "spell"),
        };

        /// <summary>生物基础属性上限（2026-11-01 定案 9；2026-11-03 放宽 12；2026-11-14 用户定案放宽 99）：
        /// 攻/血至多 99——输入即时钳制回写，保存闸兜底拦「载入旧卡>99 未触碰」的直存
        ///（编辑触碰即被输入钳制收口）。上限只为挡误输，不再当平衡闸。</summary>
        private const int MaxCreatureStat = 99;

        private readonly CardData _card = new CardData
        {
            CardName = null, // 名称可缺省（2026-10-16 槽位化）：留空保存=用生成的卡 ID 当卡名
            Supertype = Cardtype.Creature,
            Power = 0,
            Life = 0,
        };

        /// <summary>覆盖替换（2026-09-24 定案）：正在编辑的旧卡 ID；null=新建。效果合成器往返期间保留。</summary>
        private string _editingOriginalId;
        /// <summary>当前编辑卡来自预设段（预组卡·另存通道）：保存不触发覆盖替换闸、删除被拦。</summary>
        private bool _editingPresetSource;

        /// <summary>新建卡默认名序号（2026-10-23 定案：新建·1、新建·2…从 1 起、不补零、无上限；玩家可改）。</summary>
        private static int _newCardSeq;

        private TipPromptHost _tips; // TipPanel 提示弹窗宿主（2026-10-18 统一定案；惰性建——字段初始化器不能引用实例 Root）
        private TipPromptHost Tips => _tips ??= new TipPromptHost(() => Root);

        /// <summary>跨屏待编辑卡 ID（2026-10-16 卡组界面「编辑」跳转）：UIManager.Show 无法
        /// 传参——静态会话仿 ComposerSession；OnEnterInternal 消费后即清（一次性，防残留串局）。</summary>
        public static string PendingEditCardId;

        private CardKind _kind = CardKind.Creature;

        // ---- 右栏筛选态（2026-10-16 槽位化：类别 chips 退役，只留颜色多选 + 模糊串搜索，两列表通用） ----
        private readonly HashSet<UIColor> _colorFilterSet = new HashSet<UIColor>();
        // 语料缓存：卡 ID 内容寻址（内容变 ID 变，天然无陈旧）；进屏/保存成功仍整体清空。
        private static readonly Dictionary<string, string> CorpusCache = new Dictionary<string, string>();

        private UiKit.Scroll _poolList;      // list-effects=卡池（右栏卡视图）
        private UiKit.Scroll _libraryList;   // list-library=效果库（右栏效果视图）
        private RectTransform _poolFilter;
        private RectTransform _filterRow;
        private UInputField _searchField;
        private UnityEngine.UI.Button _btnSwitchTable;
        private bool _rightShowCards = true; // 默认卡池（延续原主入口）

        // ---- 中栏（2026-11-06 prefab 布局改版：编辑控件挪出 ACard 实例，卡面=只读预览） ----
        private RectTransform _cardFace;   // CardPreView 下烘焙 ACard 实例（ACardView 实时渲染预览）
        private ACardView _previewView;    // 卡面实时渲染（DeckBuilder preview 同款：FromCardDataFull 静态快照）
        private RectTransform _editorRoot; // CardPreView：fields/add-slot/form-scroll 的共同父
        private RectTransform _fields;     // fields 表单区（name/cost-row/basicability/attribute）——编辑控件锚点
        private RectTransform _formContent; // CardPreView 内联 form-scroll/viewport/content——槽行克隆/清理挂这里
        private RectTransform _attributeZone, _effectZone, _passivesZone, _keysZone;
        private TMP_Text _typeCostText;   // fields/cost-row "CostSquares " 文本节点：卡费文字公式（名带尾空格）
        private UInputField _nameInput, _attackInput, _lifeInput, _durabilityInput;
        // 场地开关（2026-10-29 P8）：结界专属标记——运行时建钮挂耐久行壳（prefab 零改、不新增美术）
        private UnityEngine.UI.Button _fieldToggle;
        private TMP_Text _fieldToggleLabel;
        private readonly Dictionary<CardKind, UnityEngine.UI.Button> _kindButtons = new Dictionary<CardKind, UnityEngine.UI.Button>();
        private readonly Dictionary<CardKind, Color> _kindBtnBase = new Dictionary<CardKind, Color>(); // v2：chip 原底色（选中换 ChipActiveBg/还原）

        // ---- 衍生物槽（2026-11-06 setderivant 改版）：主动/自动槽行内「设置衍生物」钮——效果含
        // 召唤衍生物/变形原子才显形；点击开选卡器，选中写原子 str=定义期直绑（v2 只读预览层已随
        // ACardView 实时渲染退役） ----
        private UiKit.Modal _deriveModal; // 选卡弹层（重复点开=换卡窗）

        // ---- 卡面放大（2026-11-06）：单击中栏卡面=全屏遮罩+放大卡（DeckBuilder OpenZoom 同款）。
        // CardUI.prefab 无烘焙遮罩节点——运行时 UiKit 原语建（field-toggle/derive 弹层同口径） ----
        private RectTransform _zoomOverlay;  // 全屏遮罩（半透明底+点按还原），懒建一次
        private RectTransform _zoomSlot;     // 居中 360×648 放大槽（ACard 实例由 CardOverlayController 池化挂入）
        private readonly List<CardOverlayBinding> _zoomBindings = new List<CardOverlayBinding>();
        /// <summary>放大卡整体同比缩放（DeckBuilder 同款：设计矩形 360×648 ×1080/648——文字随 localScale
        /// 同比渲染，字号三档恒定）。</summary>
        private const float ZoomCardScale = 1080f / 648f;

        // ---- 左栏画廊（2026-10-21 回收式虚拟化：~103 贴片只保留可见格，滚动/换类型零重建） ----
        private RectTransform _galleryContent;
        private UiKit.RecycledList _galleryList;
        private List<string> _artNames; // 立绘名数据源（2026-11-06 去占位：只列真实图——生物=模型立绘，法术/结界=Title）
        private UInputField _artSearchField; // v2：左栏图库搜索（按立绘名子串过滤）
        private TMP_Text _galleryCount;      // v2：图库表头计数
        private string _galleryQuery = "";

        // ---- 右栏两列表（同日虚拟化：行池复用，筛选/搜索只重绑可见行） ----
        private UiKit.RecycledList _poolRows;   // list-effects=卡池
        private UiKit.RecycledList _libRows;    // list-library=效果库
        private readonly List<PoolRowData> _poolItems = new List<PoolRowData>();
        private readonly List<LibRowData> _libItems = new List<LibRowData>();
        // 数据签名（2026-10-27 点选回顶修复）：条目序列未变（点选载入只改选中高亮）→只重绑可见行，
        // 不走 SetItems 整表重置（换数据才回顶重铺）。引擎 Count 并查防"重挂新引擎仍跳过铺行"。
        private string _poolSig;
        private string _libSig;

        /// <summary>卡池条目（卡行/提示/清筛选钮——kind 决定行结构分池）。</summary>
        private sealed class PoolRowData
        {
            public int Kind;               // 1=卡行 2=提示 3=清筛选钮
            public string Text;
            public CardData Card;
            public bool Preset;            // 预组段卡行（预设卡组引用的教学卡——载入走另存通道）
            public float H;
        }

        /// <summary>效果库条目（表头/提示/新建钮/效果行/候选行/隐藏区两行）。</summary>
        private sealed class LibRowData
        {
            public int Kind;               // 0=表头 1=提示 2=新建钮 3=效果行 4=候选行 5=隐藏计数行 6=隐藏明细行（P9）
            public string Text;
            public EffectGraphData Graph;
            public string SummaryOverride;
            public Action ClickAction;
            public string Tag;             // v2：右侧类型标签（代价/主动/光环/自动/关键词…）
            public string Reason;          // P9 隐藏明细行：宿主不匹配原因（EffectEligibility.RejectReason）
            public bool Preset;            // 2026-11-06 预设段：预组卡组引用的组合效果（可引用·编辑走另存新效果）
            public float H;
        }

        // P9（2026-10-29）宿主不匹配候选显形：分区展开态（计数行点击切换；换卡/换槽重置）
        private bool _hiddenExpanded;

        // 卡池颜色过滤 chips（预制体烘焙，Build 一次绑定；激活态=底色+Outline，v2 同卡组 chips 视觉）。
        private readonly Dictionary<UIColor, UnityEngine.UI.Button> _filterChips = new Dictionary<UIColor, UnityEngine.UI.Button>();
        private readonly Dictionary<UIColor, Color> _filterChipBase = new Dictionary<UIColor, Color>(); // v2：chip 原底色

        // ---- 悬浮介绍（2026-10-16：与效果组合界面同机制——UiKit.Described/DescRequested） ----
        private TMP_Text _descStrip;               // pool-panel 内 desc-strip 下 lbl-prop-desc
        private RectTransform _descStripNode;

        // ---- 槽位选中制（2026-10-16 二次改版：add-slot 四钮新建槽 + 槽行选中制） ----
        // 四区节点=单槽模板（prefab 恒 inactive）；每个槽行=克隆模板（text/des/delete）。
        // 选中=(类型, 数据下标)：Effect/Passives=_card.Effects 下标、Keys=_card.Keywords 下标、Cost 单槽不记下标；
        // -1=该类型的空槽（UI 态，保存忽略）。右栏按选中槽类型过滤候选，点选填入对应槽。
        private enum ZoneId { Cost, Effect, Passives, Keys }
        private ZoneId? _selType;   // 选中槽类型；null=未选（右栏全量效果图）
        private int _selIndex = -1; // 选中已填槽的数据下标；-1=空槽
        private readonly Dictionary<ZoneId, int> _emptySlots = new Dictionary<ZoneId, int>(); // 各类型空槽数（UI 态）
        private readonly Dictionary<ZoneId, RectTransform> _slotTemplates = new Dictionary<ZoneId, RectTransform>(); // 四区模板（克隆源）
        private readonly Dictionary<ZoneId, (Color color, bool on)> _slotOutlineBase = new Dictionary<ZoneId, (Color, bool)>(); // 模板主 Outline 原色（选中变白/取消还原）
        private readonly Dictionary<ZoneId, UnityEngine.UI.Button> _addButtons = new Dictionary<ZoneId, UnityEngine.UI.Button>(); // add-slot 四钮
        //（_costParamValue 已随 2026-11-08 代价效果级化整删——代价槽退役）

        // ---- basicability 区（2026-11-06 opt-in 改版：prefab ac/dc/rc 三 Toggle「具备攻击/具备守卫/具备充能」
        //      → AttackAbility/GuardAbility/ChargeAbility 正向绑定，默认关=不带；ac/dc 生物专属、rc 全卡型） ----
        private RectTransform _basicAbilityZone;
        private Toggle _attackToggle, _guardToggle, _chargeToggle;

        // ======================================== 装配 ========================================

        protected override void Build()
        {
            // ---- 悬浮描述条（先绑：后续所有 Describe 依赖它；事件路由 Build 挂/OnExit 摘） ----
            EnsureDescStrip();
            UiKit.DescRequested += OnDescRequested;

            // ---- 顶栏 ----
            Describe(BindButton("btn-back", () => Manager.Back()),
                "返回上级界面（不会自动保存——未保存的改动先点「保存」再离开）");
            Describe(BindButton("btn-save", OnSave),
                "保存到卡表：按内容哈希生成卡 ID；编辑旧卡=覆盖替换（旧卡仍被卡组引用时会被拦截）");
            Describe(BindButton("btn-delete", OnDeleteCard),
                "删除卡池中的当前卡——被卡组引用时弹窗确认，可强制删除（卡组界面会标记断链不可用）");
            BindKindButtons();

            // ---- 中栏卡面（卡面即编辑器：输入框直写 + 各区刷新层） ----
            BindMiddleCard();

            // ---- 左栏美术画廊 ----
            var gallery = Find("art-gallery");
            if (gallery != null)
            {
                // prefab 漏挂补齐（Viewport 无 RectMask2D 滚动会溢出显示）
                var viewport = UiKit.FindDeep(gallery, "Viewport") ?? UiKit.FindDeep(gallery, "viewport");
                if (viewport != null && viewport.GetComponent<RectMask2D>() == null)
                    viewport.gameObject.AddComponent<RectMask2D>();
            }
            _galleryContent = gallery != null ? UiKit.FindDeep(gallery, "Content") : null;
            if (_galleryContent == null)
                Debug.LogError("[CardComposer] 缺 art-gallery/Content（CardUI.prefab 左栏）——立绘画廊跳过");
            else
            {
                // 2026-10-21 虚拟化接管：GLG 几何（210×270·间16）被 Attach 吸收，~103 贴片只活可见格
                _galleryList = UiKit.RecycledList.Attach(
                    new UiKit.Scroll { Rect = gallery.GetComponent<ScrollRect>(), Content = _galleryContent });
                _galleryList?.Configure(i => 0, k => MakeArtTile, BindArtTile);
            }
            _galleryForKind = null; // Build=每次进屏重建（Attach 出的是全新空列表）：缓存实例的换型守卫须复位，否则二次进屏画廊永不被喂数据
            // v2：图库搜索框（按立绘名子串过滤）+ 表头计数
            _artSearchField = FindInput("field-art-search");
            if (_artSearchField != null)
            {
                Describe(_artSearchField, "图库搜索：按立绘名子串过滤（空=全量；生物=模型立绘，法术/结界=Title 默认图）");
                _artSearchField.onValueChanged.RemoveAllListeners();
                _artSearchField.onValueChanged.AddListener(q => { _galleryQuery = q ?? ""; BuildArtGallery(true); });
            }
            var hdrGallery = Find("hdr-gallery");
            _galleryCount = hdrGallery != null ? UiKit.FindDeep(hdrGallery, "count")?.GetComponent<TMP_Text>() : null;
            BuildArtGallery();

            // ---- 右栏：效果库 ↔ 卡池 ----
            _btnSwitchTable = BindButton("btn-switch-table", () => ShowRightView(!_rightShowCards));
            Describe(_btnSwitchTable, "右栏切换：效果库（点选挂到中间选中槽）↔ 卡池（浏览/检索已有卡，点击载入中间编辑）");
            _libraryList = FindScroll("list-library");
            _poolList = FindScroll("list-effects");
            _libRows = UiKit.RecycledList.Attach(_libraryList)
                ?.Configure(i => _libItems[i].Kind, LibRowFactory, BindLibRow);
            _poolRows = UiKit.RecycledList.Attach(_poolList)
                ?.Configure(i => _poolItems[i].Kind, PoolRowFactory, BindPoolRow);
            _filterRow = Find("filter-row");
            _poolFilter = Find("pool-filter");
            _searchField = FindInput("field-filter-name");
            Describe(_searchField, "搜索（右栏两列表通用）：多个关键词 AND 匹配；卡池=卡名/关键词/效果全文，效果库=名称+摘要；名称命中的卡排前");
            if (_searchField != null)
                _searchField.onValueChanged.AddListener(_ => { RefreshPool(); RefreshLibrary(); });
            ShowRightView(_rightShowCards);

            BindPoolFilter();
        }

        public override void OnEnter()
        {
            try
            {
                OnEnterInternal();
            }
            catch (Exception e)
            {
                // 「卡池 0 张」类故障可见化，不再无声吞掉。
                UnityEngine.Debug.LogException(e);
                Debug.LogError("[CardComposer] 界面装载异常（卡池/表单可能不完整）——查 Console");
            }
        }

        public override void OnExit()
        {
            UiKit.DescRequested -= OnDescRequested;
            Tips.Close(); // 在途提示弹窗清理
            if (_deriveModal != null) { _deriveModal.Close(); _deriveModal = null; } // 在途选卡弹层
            CloseZoom(); // 在途放大态（UGUI 卡面层回收——CardOverlayController 全局池）
        }

        private void OnEnterInternal()
        {
            // 卡组界面「编辑」跳转（2026-10-16）：消费待编辑卡 ID——载入后走下方常规全量刷新
            var pending = PendingEditCardId;
            PendingEditCardId = null;
            if (!string.IsNullOrEmpty(pending))
            {
                var src = CardCatalog.GetById(pending);
                if (src != null) LoadCardForEdit(src);
                else Debug.LogWarning($"[CardComposer] 待编辑卡不存在：{pending}（可能已被强删）");
            }

            CorpusCache.Clear(); // 语料整体重建（RenderEffectSummary 全池重算偏贵，仅进屏/保存时清）
            EnsureCardLists();
            SyncKindButtons();
            SyncNameInput();
            SyncZoneVisibility();
            SyncBasicAbility();
            SyncAttributeInputs();
            _card.AggregateEffectAuras(true);
            RefreshSlots(); // 槽行随进屏对账（含 setderivant 显隐/标签）
            Recalculate();
            RefreshLibrary();
            RefreshPool();
            WarnStaleAttachedEffects(); // 库效果编辑返回的换代孤儿检测
        }

        // ======================================== 悬浮介绍 ========================================

        /// <summary>描述条单名绑定：pool-panel 内 desc-strip 下 lbl-prop-desc；缺节点 LogError 留空——不代码补建。
        /// 初始隐藏（有悬浮才出现；空串=隐藏）。2026-10-16 起兼做操作反馈（toast 退役定案——悬浮事件随后自然覆盖）。</summary>
        private void EnsureDescStrip()
        {
            _descStripNode = Find("desc-strip");
            var lbl = _descStripNode != null ? UiKit.FindDeep(_descStripNode, "lbl-prop-desc") : null;
            _descStrip = lbl?.GetComponent<TMP_Text>();
            if (_descStrip == null)
            {
                Debug.LogError("[CardComposer] desc-strip 缺 lbl-prop-desc 文本——检查 CardUI.prefab");
                return;
            }
            _descStrip.text = "";
            _descStripNode.gameObject.SetActive(false);
        }

        /// <summary>给控件挂悬浮介绍（幂等：同物体重复挂=覆盖文本）——直接委托 UiKit.Described。</summary>
        private static void Describe(Component c, string desc) => UiKit.Described(c, desc);

        /// <summary>描述条统一写口（空串=隐藏）。</summary>
        private void SetDescStrip(string text)
        {
            if (_descStrip == null || _descStripNode == null) return;
            _descStrip.text = text ?? "";
            _descStripNode.gameObject.SetActive(!string.IsNullOrEmpty(_descStrip.text));
        }

        private void OnDescRequested(string desc) => SetDescStrip(desc);

        // 容器空值防护
        private void EnsureCardLists()
        {
            _card.Keywords ??= new List<string>();
            _card.Tags ??= new List<string>();
            _card.Effects ??= new List<CardEffectData>();
        }

        // ======================================== 顶栏：类型三钮 ========================================

        private void BindKindButtons()
        {
            foreach (var def in KindDefs)
            {
                var captured = def.kind;
                var btn = BindButton(def.node, () => SetKind(captured));
                if (btn == null) continue;
                Describe(btn, captured switch
                {
                CardKind.Creature => "生物：有攻击/生命可参战，可挂自动效果（禁连接光环）",
                CardKind.Spell => "法术：挂 1~3 个主动效果（施放时择一发动），速度=3−槽数动态算",
                _ => "结界：耐久体，只挂自动效果与连接光环（禁主动——充能=资格位，任意卡型在基础能力区勾选「具备充能」）",
                });
                _kindButtons[captured] = btn;
            }
            SyncKindButtons();
        }

        /// <summary>切卡型（取代旧 dropdown-cardtype）：数据口径沿 ApplyKindToCard，画廊随类型换源；
        /// zone 显隐/容量状态行随类型刷新（2026-10-16 槽位化）。</summary>
        private void SetKind(CardKind kind)
        {
            if (_kind == kind) return;
            bool lostKeywords = kind != CardKind.Creature && _card.Keywords != null && _card.Keywords.Count > 0;
            _kind = kind;
            ApplyKindToCard();
            DropEmptySlotsForKind(); // 新卡型不匹配的空槽回收（已填槽保留可删）
            SyncKindButtons();
            SyncZoneVisibility();
            SyncBasicAbility();
            SyncAttributeInputs();
            SetAttrTexts();
            BuildArtGallery();
            RefreshSlots();
            Recalculate();
            if (lostKeywords) ShowToast("已切型——本体关键词已清空（关键词为生物专属）");
        }

        /// <summary>切型清理：与新卡型不匹配的空槽直接回收（已填槽保留可删——保存闸兜底）。</summary>
        private void DropEmptySlotsForKind()
        {
            foreach (ZoneId id in Enum.GetValues(typeof(ZoneId)))
                if (!TypeEnabledForKind(id)) _emptySlots[id] = 0;
        }

        private void SyncKindButtons()
        {
            foreach (var kv in _kindButtons)
            {
                bool on = kv.Key == _kind;
                var img = kv.Value.GetComponent<Image>();
                if (img != null)
                {
                    if (!_kindBtnBase.TryGetValue(kv.Key, out var baseCol)) _kindBtnBase[kv.Key] = baseCol = img.color;
                    img.color = on ? UiStyle.ChipActiveBg : baseCol; // v2：选中=ChipActive 底（同卡组 chips）
                }
                var ol = kv.Value.GetComponent<Outline>();
                if (on && ol == null)
                {
                    ol = kv.Value.gameObject.AddComponent<Outline>();
                    ol.effectColor = UiStyle.ChipActiveEdge;
                    ol.effectDistance = new Vector2(1.5f, -1.5f);
                }
                if (ol != null)
                {
                    ol.effectColor = UiStyle.ChipActiveEdge;
                    ol.enabled = on;
                }
            }
        }

    }
}
