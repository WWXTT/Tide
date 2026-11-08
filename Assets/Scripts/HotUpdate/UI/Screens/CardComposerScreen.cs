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
        /// · 卡池行 meta 法术补「速度X」（速度归卡类别的同一口径——单源 CardOverlayItem.EffectSpeedOf；
        ///   生物=攻/血、结界=耐久不变）；
        /// · 中栏卡面单击=全屏放大（DeckBuilder OpenZoom 同款：运行时遮罩+360×648 槽×5/3 缩放，
        ///   点遮罩还原；CardUI.prefab 无烘焙节点——遮罩运行时建）；
        /// · 图库占位项退役（画廊只列真实可选图；未选立绘=无高亮格，卡面回落烘焙占位图）；
        /// · 效果库补预设段（AppendPresetEffects）：预组卡组效果只读并入——可引用挂载、「编辑」
        ///   跳效果合成器预设只读态（修改保存=另存新效果，与预设同内容拦存；源档不可覆盖不可删）。
        /// </summary>
    public sealed class CardComposerScreen : UIScreen
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

        /// <summary>生物基础属性上限（2026-11-01 定案 9；2026-11-03 放宽 12）：攻/血至多 12——输入即时钳制回写，
        /// 保存闸兜底拦「载入旧卡>12 未触碰」的直存（编辑触碰即被输入钳制收口）。</summary>
        private const int MaxCreatureStat = 12;

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

        // ======================================== 中栏：卡面即编辑器 ========================================

        private void BindMiddleCard()
        {
            // 卡面=只读实时预览（2026-11-06 布局改版）：ACardView 按卡型点亮类型块/立绘/徽章/效果行，
            // 与手牌/战场渲染同源（FromCardDataFull 静态快照，无运行态订阅）
            _cardFace = Find("ACard");
            if (_cardFace != null)
            {
                _previewView = _cardFace.GetComponent<ACardView>() ?? _cardFace.gameObject.AddComponent<ACardView>();
                _previewView.Bind();
                // 单击卡面=放大展示（2026-11-06：DeckBuilder 预览同款——运行时补 Button，
                // 编辑走下方表单/槽行，卡面本体只读+可放大）
                var faceBtn = _cardFace.GetComponent<UnityEngine.UI.Button>();
                if (faceBtn == null)
                {
                    faceBtn = _cardFace.gameObject.AddComponent<UnityEngine.UI.Button>();
                    faceBtn.transition = UnityEngine.UI.Button.Transition.None;
                }
                faceBtn.onClick.RemoveAllListeners();
                faceBtn.onClick.AddListener(() => OpenZoom(_card));
                Describe(faceBtn, "单击放大查看全卡（点空白遮罩还原）——卡面为实时预览，编辑请用右侧表单与效果槽行");
            }

            // 编辑区锚点：CardPreView 直属子树（fields/add-slot/form-scroll 与 ACard 实例平级）——
            // 直查路径绑定，防深扫与卡面内部同名节点（name/effect/passives/keys）串线
            _editorRoot = Find("CardPreView");
            _fields = _editorRoot != null ? _editorRoot.Find("fields") as RectTransform : null;
            if (_fields == null)
                Debug.LogError("[CardComposer] 屏内缺 CardPreView/fields（CardUI.prefab）——表单输入区不可用");

            // 费用公式文本节点（fields/cost-row 下；节点名带尾空格 "CostSquares "）——
            // 按名前缀深扫防尾空格丢失；开启自动换行（公式长于行宽）
            _typeCostText = FindCostFormulaText(_fields);
            if (_typeCostText != null) _typeCostText.enableWordWrapping = true;

            _nameInput = FindInputUnder(_fields, "name");
            if (_nameInput != null)
                _nameInput.onValueChanged.AddListener(v => { _card.CardName = v; RefreshPreview(); });

            _attributeZone = UiKit.FindDeep(_fields, "attribute");
            _attackInput = BindIntInput(_attributeZone, "attack",
                v => { _card.Power = ClampStat(v, _attackInput); Recalculate(); },
                $"生物攻击力——计入自动算费（区间 [免费属性底, {MaxCreatureStat}]；底=3−攻守−首槽，动态）", MaxCreatureStat);
            _lifeInput = BindIntInput(_attributeZone, "life",
                v => { _card.Life = ClampStat(v, _lifeInput); Recalculate(); },
                $"生物生命值——计入自动算费（区间 [免费属性底, {MaxCreatureStat}]；底=3−攻守−首槽，动态）", MaxCreatureStat);
            _durabilityInput = BindIntInput(_attributeZone, "durability",
                v => { _card.Durability = v; Recalculate(); }, "结界耐久——免费点全投耐久，装/卸效果时即时重算");

            // 场地开关（2026-10-29 P8 定案）：结界专属 isField 标记——钮建在耐久行壳内
            //（行壳显隐=开关显隐，SetAttrActive 同拍）；运行时建件（UiKit 工厂），prefab 零改。
            var durRow = _durabilityInput != null ? _durabilityInput.transform.parent as RectTransform : null;
            if (durRow != null)
            {
                _fieldToggle = UiKit.MiniButton("field-toggle", durRow, "场地:关", OnFieldToggleClicked, null, null, 88f);
                _fieldToggleLabel = _fieldToggle != null ? _fieldToggle.GetComponentInChildren<TMP_Text>() : null;
                Describe(_fieldToggle, "场地卡（P8）：结界专属标记——打出后固定落在己方场地格（每方一张），"
                    + "再打场地=顶替（旧场地入墓）。改标记=换卡 ID（内容哈希）");
            }

            // v2 只读预览层（name-view/kind-view/face-note）已随 ACardView 实时渲染退役（prefab 节点同批删除）

            var formCt = FormContent();
            _effectZone = formCt != null ? formCt.Find("effect") as RectTransform : null;
            _passivesZone = formCt != null ? formCt.Find("passives") as RectTransform : null;
            _keysZone = formCt != null ? formCt.Find("keys") as RectTransform : null;

            // 三区节点=单槽模板（cost 模板已随 2026-11-06 代价挪构筑期退役）：prefab 恒 inactive，
            // 槽行=运行时克隆；捕获主 Outline 原色（选中行变白/取消还原），登记模板供克隆
            RegisterSlotTemplate(ZoneId.Effect, _effectZone);
            RegisterSlotTemplate(ZoneId.Passives, _passivesZone);
            RegisterSlotTemplate(ZoneId.Keys, _keysZone);
            BindBasicAbility();
            BindAddSlotButtons();
        }

        /// <summary>登记槽模板并捕获主 Outline 原色（缺节点 LogError 空转不炸）。
        /// 模板登记即压回 inactive（2026-11-06 prefab 改版后模板可能烘成激活态——CloneTemplateFrom
        /// 只收 inactive 模板：激活态会静默不克隆，且模板自身常驻显示成三条空行）。</summary>
        private void RegisterSlotTemplate(ZoneId id, RectTransform zone)
        {
            if (zone == null) return;
            zone.gameObject.SetActive(false);
            _slotTemplates[id] = zone;
            var ol = zone.GetComponent<Outline>();
            if (ol != null) _slotOutlineBase[id] = (ol.effectColor, ol.enabled);
        }

        // ---- setderivant（2026-11-06 改版）：主动/自动槽行内「设置衍生物」钮——效果含召唤衍生物/
        // 变形原子才显形（用户定案）；点击开选卡器（候选过滤单源 TokenTemplateCandidateOk），选中写
        // 原子 str=定义期直绑（保存闸对未绑 CardRef 原子照拦）。旧 derive 行模板/addderive 钮已随
        // prefab 布局改版退役 ----

        /// <summary>槽行 setderivant 钮接线（MakeSlotRow 逐行调用）：显隐=该效果是否含
        /// SummonToken/GrantMorph 槽（空槽/关键词行/无槽效果恒隐）；目标=该效果第一个未绑槽
        /// （全绑=首个槽，重选）；标签随绑定对账（已绑显卡名）。</summary>
        private void BindSetDerivant(RectTransform row, int dataIndex)
        {
            var btnRt = UiKit.FindDeep(row, "setderivant");
            if (btnRt == null) return; // keys 模板/旧 prefab 兼容——无钮空转
            var mine = DeckTokenSupport.EnumerateSlots(_card)
                .Where(s => s.EffectIndex == dataIndex).ToList();
            btnRt.gameObject.SetActive(mine.Count > 0);
            if (mine.Count == 0) return;
            var btn = btnRt.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) return;
            var target = mine.FirstOrDefault(s => s.NeedsBinding) ?? mine[0];
            var label = UiKit.FindDeep(btnRt, "label")?.GetComponent<TMP_Text>();
            var tpl = string.IsNullOrEmpty(target.FixedTemplateId)
                ? null : CardCatalog.GetById(target.FixedTemplateId);
            if (label != null)
                label.text = tpl != null
                    ? (target.IsMorph ? "变形:" : "衍生物:") + (string.IsNullOrEmpty(tpl.CardName) ? tpl.ID : tpl.CardName)
                    : "设置衍生物";
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Debug.Log("[UI点击] setderivant 效果#" + (dataIndex + 1));
                OpenDerivePicker(target);
            });
            Describe(btn, (target.IsMorph ? "变形" : "召唤衍生物") + "槽：点按从卡表选模板卡——候选须"
                + (target.IsMorph ? "生物/结界" : "生物") + "·费≥1·不带代价、自身不含衍生物/变形（不可嵌套）；选中即写进效果引用");
        }

        /// <summary>衍生物模板选卡器（卡表全量+三条过滤+搜索）：选中写原子 str=定义期直绑
        ///（运行时解析=实例绑定→原子 str，同 SummonTokenHandler.ResolveBoundTemplateId）。</summary>
        private void OpenDerivePicker(DeckTokenSupport.TokenSlot slot)
        {
            if (_deriveModal != null) { _deriveModal.Close(); _deriveModal = null; } // 重复点开=换卡窗
            bool creatureOnly = !slot.IsMorph; // 召唤=仅生物；变形=生物/结界
            var kindZh = slot.IsMorph ? "变形" : "召唤衍生物";
            var modal = UiKit.ModalBox(Root,
                $"选择模板卡——{kindZh}（{(creatureOnly ? "仅生物" : "生物/结界")}·费≥1·无代价·不可嵌套）",
                width: 660f, height: 600f);
            _deriveModal = modal;

            var searchRow = UiKit.Row("search", modal.Panel, spacing: 8f);
            UiKit.Size(UiKit.Label("lbl", searchRow, "搜索：", 13, UiStyle.TextSecondary), w: 40f);
            var searchField = UiKit.InputField("input", searchRow, "卡名 / ID 关键词", "", width: 400f);
            UiKit.Button("btn-close", searchRow, "关闭", () =>
            {
                _deriveModal?.Close();
                _deriveModal = null;
            }, height: 30f, fontSize: 13);

            var scroll = UiKit.ScrollColumn("list", modal.Panel);
            UiKit.Size(scroll.Rect, fw: 1f, fh: 1f);

            void Rebuild()
            {
                for (int i = scroll.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(scroll.Content.GetChild(i).gameObject);
                string key = (searchField.text ?? "").Trim();
                var candidates = CardSorter.Sort(CardCatalog.LoadAll()
                        .Where(c => DeckTokenSupport.TokenTemplateCandidateOk(c, creatureOnly))
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
                    UiKit.Size(UiKit.Label("meta", line, DeriveMetaOf(pick), 12, UiStyle.TextFaint), fw: 1f);
                    bool current = slot.FixedTemplateId == pick.ID;
                    UiKit.Button("btn-pick", line, current ? "已选 ✓" : "选择", () =>
                    {
                        slot.Atom.str = pick.ID; // 定义期直绑
                        Recalculate();           // 卡面效果文案 {引用卡}→卡名 同拍
                        RefreshSlots();          // setderivant 标签随绑定对账
                        _deriveModal?.Close();
                        _deriveModal = null;
                    }, bg: current ? UiStyle.SelectedRowBg : null, height: 30f, fontSize: 13);
                }
                UiKit.Label("count", scroll.Content,
                    candidates.Count == 0
                        ? "（无匹配卡——候选须为费≥1、无代价、不含衍生物/变形的真实卡）"
                        : shown < candidates.Count ? $"共 {candidates.Count} 张，仅列前 {cap} 张——用搜索缩小范围"
                        : $"共 {candidates.Count} 张", 12, UiStyle.TextHint);
            }

            searchField.onValueChanged.RemoveAllListeners();
            searchField.onValueChanged.AddListener(_ => Rebuild());
            Rebuild();
        }

        /// <summary>候选行 meta 文本：类型 + 攻/血（生物，缺项显 ?）或类型标注（同 CardRef 选卡口径）。</summary>
        private static string DeriveMetaOf(CardData card)
            => card.Supertype == Cardtype.Creature
                ? $"生物 {(card.Power.HasValue ? card.Power.Value.ToString() : "?")}/{(card.Life.HasValue ? card.Life.Value.ToString() : "?")}"
                : card.Supertype == Cardtype.Enchantment ? "结界" : card.Supertype.ToString();

        // ---- add-slot 三钮：新建空槽（容量按型闸；新建即选中→右栏按槽类型过滤） ----

        private void BindAddSlotButtons()
        {
            // addcost 已随 2026-11-06 代价挪构筑期退役（prefab 同批删除；既有代价行编辑口=效果合成器）
            BindAddSlot(ZoneId.Effect, "addeffect", "添加主动效果",
                "新建空主动槽（生物×1 / 法术×1~3·施放时择一发动；结界禁主动）");
            BindAddSlot(ZoneId.Passives, "addpassives", "添加自动效果",
                "新建空自动槽（生物=自动×1；结界=光环恰好 1〔必填〕+自动×2；法术无）");
            BindAddSlot(ZoneId.Keys, "addkeys", "添加关键词",
                "新建空关键词槽（生物专属·至多 3，每关掉一个攻击/守卫能力 +1）");
        }

        private void BindAddSlot(ZoneId id, string node, string label, string desc)
        {
            var btn = BindButton(node, () => AddEmptySlot(id));
            if (btn == null) return;
            _addButtons[id] = btn;
            Describe(btn, desc);
        }

        /// <summary>新建空槽：容量闸（已用=已填+空槽）；新建即选中（右栏自动过滤到该类型）。</summary>
        private void AddEmptySlot(ZoneId id)
        {
            int cap = SlotCapacity(id);
            if (cap <= 0)
            {
                ShowToast(TypeZh(id) + "槽与本卡类型不匹配——不可添加");
                return;
            }
            if (FilledSlotCount(id) + EmptySlotCount(id) >= cap)
            {
                ShowToast($"{TypeZh(id)}槽已满（{FilledSlotCount(id)}/{cap}）——先删除槽内条目再添加");
                return;
            }
            _emptySlots[id] = EmptySlotCount(id) + 1;
            Debug.Log("[UI点击] add-slot " + id);
            SelectSlot(id, -1);
        }

        /// <summary>add 三钮显隐/可用随卡型与容量（容量 0 整钮隐藏；满槽禁用）。</summary>
        private void SyncAddSlotButtons()
        {
            foreach (var kv in _addButtons)
            {
                int cap = SlotCapacity(kv.Key);
                kv.Value.gameObject.SetActive(cap > 0);
                kv.Value.interactable = FilledSlotCount(kv.Key) + EmptySlotCount(kv.Key) < cap;
            }
        }

        //（EffectShapeBlocksCost 已随 2026-11-08 代价效果级化整删——无消费方：形态互斥在
        //  EffectComposerScreen.EffectCostConflictOf 效果级单源判定。）

        private static UInputField FindInputUnder(RectTransform scope, string name) =>
            scope != null ? UiKit.FindDeep(scope, name)?.GetComponentInChildren<UInputField>(true) : null;

        /// <summary>费用公式节点深扫（v2：节点在表单网格 cost-row 下；名带尾空格按前缀匹配）。</summary>
        private static TMP_Text FindCostFormulaText(RectTransform scope)
        {
            if (scope == null) return null;
            for (int i = 0; i < scope.childCount; i++)
            {
                var c = scope.GetChild(i);
                if (c.name.StartsWith("CostSquares", StringComparison.Ordinal))
                {
                    var t = c.GetComponent<TMP_Text>();
                    if (t != null) return t;
                }
                var deep = FindCostFormulaText(c as RectTransform);
                if (deep != null) return deep;
            }
            return null;
        }

        private UInputField BindIntInput(RectTransform scope, string node, Action<int> onChanged, string desc,
            int max = int.MaxValue)
        {
            var field = FindInputUnder(scope, node);
            if (field == null) return null;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.onValueChanged.AddListener(s =>
            {
                if (!int.TryParse(s, out var v)) return;
                if (v > max)
                {
                    v = max;
                    field.SetTextWithoutNotify(v.ToString()); // 回写钳后值——输入框显示与卡数据不脱钩
                }
                onChanged(v);
            });
            Describe(field, desc);
            return field;
        }

        private void SyncNameInput()
        {
            if (_nameInput != null) _nameInput.SetTextWithoutNotify(_card.CardName ?? "");
        }

        /// <summary>生物属性输入钳制（2026-11-03 底盘分配改版）：区间=[免费属性底, MaxCreatureStat]。
        /// 底=FreeAttrBaseOf（3−攻守−首槽，与计价同源）：免费 n/n 固定获得、不可偏移
        ///（无法做成 5-1 之类偏斜底）——越界输入即时钳制回写。非生物不钳。</summary>
        private int ClampStat(int v, UInputField field)
        {
            if (_kind != CardKind.Creature) return v;
            int min = CardCompositionCost.FreeAttrBaseOf(_card);
            int clamped = Math.Max(min, Math.Min(MaxCreatureStat, v));
            if (clamped != v && field != null)
                field.SetTextWithoutNotify(clamped.ToString()); // 回写钳后值——输入框显示与卡数据不脱钩
            return clamped;
        }

        /// <summary>结构变动后托底（2026-11-03 底盘分配改版）：已填属性低于新底则抬到底
        ///（「免费属性 n/n 固定获得」——如删技能/关攻守使底上升时自动补齐；
        /// 未填(null)不强制——留待保存闸拦截）。字段经 SetAttrTexts 静默回显。</summary>
        private void ApplyStatFloors()
        {
            if (_kind != CardKind.Creature) return;
            int min = CardCompositionCost.FreeAttrBaseOf(_card);
            bool changed = false;
            if (_card.Power.HasValue && _card.Power.Value < min) { _card.Power = min; changed = true; }
            if (_card.Life.HasValue && _card.Life.Value < min) { _card.Life = min; changed = true; }
            if (changed) SetAttrTexts();
        }

        /// <summary>属性输入框显隐+回写（生物=攻/血、结界=耐久、法术=attribute 整区隐藏）。
        /// 攻/血文本回显收窄到 SetAttrTexts（留空=未填，保存闸口径）；耐久恒回显数字。
        /// v2：输入在表单网格行壳内（icon+label+input）——行壳随输入一起显隐。</summary>
        private void SyncAttributeInputs()
        {
            if (_attributeZone != null)
                _attributeZone.gameObject.SetActive(HasStats || HasDurability);
            SetAttrActive(_attackInput, HasStats);
            SetAttrActive(_lifeInput, HasStats);
            SetAttrActive(_durabilityInput, HasDurability);
            if (_durabilityInput != null)
                _durabilityInput.SetTextWithoutNotify(_card.Durability.ToString());
        }

        /// <summary>输入框连同其行壳显隐（行壳=输入的直接父级，若非 attribute 区本身）。</summary>
        private void SetAttrActive(UInputField field, bool on)
        {
            if (field == null) return;
            var go = field.gameObject;
            var row = field.transform.parent as RectTransform;
            if (row != null && row != _attributeZone && row.name.EndsWith("-row", StringComparison.Ordinal))
                go = row.gameObject;
            go.SetActive(on);
        }

        /// <summary>攻/血输入框回显（2026-10-16 属性必填定案）：留空=未填（新建/清空后保存拦截）；
        /// 载入旧卡 null 视作 0 已在 LoadCardForEdit 归一——此处只回显非空值。</summary>
        private void SetAttrTexts()
        {
            if (_attackInput != null)
                _attackInput.SetTextWithoutNotify(
                    _kind == CardKind.Creature && _card.Power.HasValue ? _card.Power.Value.ToString() : "");
            if (_lifeInput != null)
                _lifeInput.SetTextWithoutNotify(
                    _kind == CardKind.Creature && _card.Life.HasValue ? _card.Life.Value.ToString() : "");
            SyncFieldToggle(); // 场地开关文案回显（P8）：切型/载入/新建同拍
        }

        // ======================================== 槽位选中制（2026-10-16 二次改版） ========================================

        /// <summary>类型中文名（提示语/标题用）。</summary>
        private static string TypeZh(ZoneId id) => id switch
        {
            ZoneId.Cost => "代价",
            ZoneId.Effect => "主动",
            ZoneId.Passives => "自动",
            _ => "关键词",
        };

        /// <summary>槽容量按型（单源：add 四钮/填入闸/保存闸共用同一口径）。
        /// 结界 passives=光环1+自动2 合一（光环恰好 1 由保存闸拦截——填入时不强分）。
        /// 代价槽已随 2026-11-08 代价效果级化整退役（容量 0=全区隐藏）——代价编辑口=
        /// 效果合成器有限分支「支付代价」门（代价行挂 CardEffectData.Costs）。</summary>
        private int SlotCapacity(ZoneId id) => id switch
        {
            ZoneId.Cost => 0, // 代价槽退役（2026-11-08 效果级化）——编辑走效果合成器
            ZoneId.Effect => ActiveSlotCapacity,
            ZoneId.Passives => _kind == CardKind.Enchantment ? AuraSlotCapacity + AutoSlotCapacity : AutoSlotCapacity,
            _ => KeywordCapacity,
        };

        /// <summary>该类型已填槽数（数据侧计数；空槽另计 EmptySlotCount）。</summary>
        private int FilledSlotCount(ZoneId id) => id switch
        {
            ZoneId.Cost => 0, // 代价槽退役（2026-11-08）——卡级代价行不存在
            ZoneId.Effect => ActiveEffectCount,
            ZoneId.Passives => AuraEffectCount + AutoEffectCount,
            _ => _card.Keywords?.Count ?? 0,
        };

        private int EmptySlotCount(ZoneId id) =>
            _emptySlots.TryGetValue(id, out var n) ? n : 0;

        /// <summary>该槽类型是否与当前卡型匹配（不匹配的已填槽仍渲染、可删——保存闸兜底拦）。</summary>
        private bool TypeEnabledForKind(ZoneId id) => id switch
        {
            ZoneId.Cost => true,
            //（P12「结界主动槽只收充能/法术充能位」已随 2026-11-05 充能系统动作化改版收回——
            //  充能不再是卡上效果：结界禁主动（容量 0），法术主动槽=1~3。）
            ZoneId.Effect => true,
            ZoneId.Passives => _kind != CardKind.Spell,
            _ => _kind == CardKind.Creature,
        };

        /// <summary>选中槽（再点同槽=取消）：行内主 Outline 变白；右栏自动切效果库并按槽类型过滤。</summary>
        private void SelectSlot(ZoneId type, int index)
        {
            if (_selType == type && _selIndex == index)
            {
                DeselectSlot();
                return;
            }
            _selType = type;
            _selIndex = index;
            ShowRightView(false);
            RefreshSlots();
            RefreshLibrary();
        }

        /// <summary>取消选中（右栏回全量效果图；槽行重建还原描边）。</summary>
        private void DeselectSlot()
        {
            _selType = null;
            _selIndex = -1;
            RefreshSlots();
            RefreshLibrary();
        }

        /// <summary>卡型可见性（2026-10-16 二次改版；2026-11-06 opt-in 修订）：四区模板恒 inactive（槽行运行时克隆）；
        /// 此处只管 add 四钮（容量 0 隐藏）——basicability 恒显（充能资格全卡型，行可见性归 SyncBasicAbility）。</summary>
        private void SyncZoneVisibility()
        {
            SyncBasicAbility();
            SyncAddSlotButtons();
            if (_selType != null && SlotCapacity(_selType.Value) == 0) DeselectSlot();
        }

        // ---- basicability（2026-11-06 opt-in 改版：ac/dc/rc 三开关正向绑 AttackAbility/GuardAbility/
        //      ChargeAbility，默认关=不带——「添加了才有」；ac/dc 生物专属、rc 全卡型） ----

        private void BindBasicAbility()
        {
            _basicAbilityZone = UiKit.FindDeep(_fields, "basicability");
            _attackToggle = BindAbilityToggle("ac", "具备攻击",
                on => _card.AttackAbility = on,
                "具备攻击能力（basicability，2026-11-06 opt-in）：默认不带；开启占 1 点底盘预算，关闭释放（关键词槽 +1、免费属性底抬升）",
                affectsKeywordSlots: true);
            _guardToggle = BindAbilityToggle("dc", "具备守卫",
                on => _card.GuardAbility = on,
                "具备守卫能力（basicability，2026-11-06 opt-in）：默认不带；开启占 1 点底盘预算，关闭释放（关键词槽 +1、免费属性底抬升）",
                affectsKeywordSlots: true);
            _chargeToggle = BindAbilityToggle("rc", "具备充能",
                on => _card.ChargeAbility = on,
                "具备充能能力（basicability，2026-11-06 opt-in）：默认不带；开启=该卡可宣充能（己方主阶段空栈、一回合一次、结算期选色免费落层），卡费固定 +1 灰",
                affectsKeywordSlots: false);
            SyncBasicAbility();
        }

        private Toggle BindAbilityToggle(string node, string label, Action<bool> apply, string desc, bool affectsKeywordSlots)
        {
            var rt = _basicAbilityZone != null ? UiKit.FindDeep(_basicAbilityZone, node) : null;
            var toggle = rt != null ? rt.GetComponent<Toggle>() : null;
            if (toggle == null) return null;
            ConvertLegacyLabelToTmp(rt, label); // prefab 手加的 legacy Text → TMP（全量 TMP 约定）
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(on =>
            {
                apply(on);
                _card.ResetCache();
                if (affectsKeywordSlots) RefreshSlots(); // 关键词槽容量随攻/守开关变化（add 按钮态同步；充能不影响）
                Recalculate();
                ShowToast(on ? $"已具备：{label}" : affectsKeywordSlots
                    ? $"已关闭：{label}——关键词槽 +1（现上限 {KeywordCapacity}）"
                    : $"已关闭：{label}——该卡不再可宣充能");
            });
            Describe(toggle, desc);
            return toggle;
        }

        /// <summary>legacy Text 标签运行时换装 TMP（cardtype 换装先例）：沿用原 rect，只换文本组件。</summary>
        private static void ConvertLegacyLabelToTmp(RectTransform toggleRt, string text)
        {
            var legacy = toggleRt.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (legacy == null) return;
            var src = legacy.rectTransform;
            var anchors = (src.anchorMin, src.anchorMax, src.pivot, src.sizeDelta, src.anchoredPosition);
            UnityEngine.Object.Destroy(legacy);
            var label = UiKit.Label("label", toggleRt, text, UiStyle.SmallSize, UiStyle.TextBody);
            var rt = label.rectTransform;
            rt.anchorMin = anchors.Item1;
            rt.anchorMax = anchors.Item2;
            rt.pivot = anchors.Item3;
            rt.sizeDelta = anchors.Item4;
            rt.anchoredPosition = anchors.Item5;
        }

        /// <summary>三开关回显与行可见性（2026-11-06 opt-in）：区恒显（充能资格全卡型）；
        /// 攻/守行仅生物可见（资格清空归 ApplyKindToCard 切型口，与关键词「切型即清」同口径）。</summary>
        private void SyncBasicAbility()
        {
            if (_basicAbilityZone != null)
                _basicAbilityZone.gameObject.SetActive(true);
            if (_attackToggle != null)
            {
                _attackToggle.SetIsOnWithoutNotify(_card.AttackAbility);
                _attackToggle.gameObject.SetActive(_kind == CardKind.Creature);
            }
            if (_guardToggle != null)
            {
                _guardToggle.SetIsOnWithoutNotify(_card.GuardAbility);
                _guardToggle.gameObject.SetActive(_kind == CardKind.Creature);
            }
            if (_chargeToggle != null) _chargeToggle.SetIsOnWithoutNotify(_card.ChargeAbility);
        }

        // ---- 场地开关（2026-10-29 P8）：结界专属 isField 标记 ----

        private void OnFieldToggleClicked()
        {
            _card.IsField = !_card.IsField;
            SyncFieldToggle();
            ShowToast(_card.IsField
                ? "已标记场地卡——场地卡只能放在场地格，每方一张"
                : "已取消场地标记（回到普通结界）");
        }

        /// <summary>钮文案回显（场地:开/关）。空转安全——钮只在结界面板的耐久行壳内存在。</summary>
        private void SyncFieldToggle()
        {
            if (_fieldToggleLabel != null)
                _fieldToggleLabel.text = _card.IsField ? "场地:开" : "场地:关";
        }

        // ---- 容量与效果分型（单源：保存闸/槽行/右栏过滤共用） ----

        //（P12「结界主动槽 1（只收充能）/法术主动槽 4（含充能位）」已随 2026-11-05 充能系统动作化
        //  改版收回——充能不再是卡上效果：法术主动槽=1~3（与 CardLoader「总效果数 ≤4」含自动位
        //  同口径），结界禁主动（容量 0），生物照旧 ×1。）
        private int ActiveSlotCapacity => _kind == CardKind.Spell ? 3 : (_kind == CardKind.Creature ? 1 : 0);
        private int AutoSlotCapacity => _kind == CardKind.Enchantment ? 2 : (_kind == CardKind.Creature ? 1 : 0);
        private int AuraSlotCapacity => _kind == CardKind.Enchantment ? 1 : 0;

        /// <summary>关键词上限（生物专属，2026-11-06 opt-in 口径）：3 + 攻/守资格缺位各 +1（最高 5；
        /// 充能资格不占底盘——不影响关键词槽）。</summary>
        private int KeywordCapacity => _kind == CardKind.Creature
            ? 3 + (_card.AttackAbility ? 0 : 1) + (_card.GuardAbility ? 0 : 1)
            : 0;

        /// <summary>光环形态判据（与效果合成器 InferMode 同源）：连接/规则光环都走光环形态效果图（IsAuraMode）。</summary>
        private static bool IsAuraEffect(CardEffectData fx)
        {
            if (fx == null) return false;
            var graph = new EffectGraphData(fx.DisplayName ?? "") { header = fx, steps = fx.Steps };
            return EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(graph));
        }

        private static bool IsActiveGraph(EffectGraphData g)
        {
            var fx = g?.header;
            if (fx == null) return false;
            var t = (TriggerTiming)fx.TriggerTiming;
            return t == TriggerTiming.Activate_Active || t == TriggerTiming.Activate_Instant;
        }

        // ---- P9（2026-10-29）宿主适用分区：EffectEligibility 单源——候选不静默隐藏 ----

        private static Cardtype HostOf(CardKind kind) => kind switch
        {
            CardKind.Enchantment => Cardtype.Enchantment,
            CardKind.Spell => Cardtype.Spell,
            _ => Cardtype.Creature,
        };

        /// <summary>宿主×图 适用判定的**唯一分区谓词**（效果库槽视图过滤与「隐藏计数行」共用；
        /// 验证器按此断言「反复触发图不在生物候选里出现」）。null=可挂；非空=拒绝原因。</summary>
        public static string HostRejectReason(Cardtype host, EffectGraphData g)
        {
            bool auraShape = EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(g));
            return EffectEligibility.RejectReason(host, EligibilityProbeDef(g), auraShape);
        }

        /// <summary>图 → 适用判定探针 def（只装激活方式/时机/原子类型——不跑完整装载转换，
        /// 不触发费用/校验副作用；原子来自 steps 优先、扁平 AtomicEffects 兜底）。</summary>
        private static CardCore.EffectDefinition EligibilityProbeDef(EffectGraphData g)
        {
            var def = new CardCore.EffectDefinition
            {
                Id = "ELIG_PROBE",
                TriggerTiming = (TriggerTiming)(g?.header?.TriggerTiming ?? 0),
                ActivationType = (EffectActivationType)(g?.header?.ActivationType ?? 0),
                Effects = new List<CardCore.AtomicEffectInstance>(),
            };
            void Add(AtomicEffectEntry e)
            {
                var inst = e == null ? null : CardEffectConverter.ConvertAtomForUI(e);
                if (inst != null) def.Effects.Add(inst);
            }
            bool fromSteps = false;
            foreach (var s in g?.steps ?? new List<EffectStepData>())
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                fromSteps = true;
                Add(s.atomic);
            }
            if (!fromSteps && g?.header?.AtomicEffects != null)
                foreach (var e in g.header.AtomicEffects) Add(e);
            return def;
        }

        private int ActiveEffectCount
        {
            get
            {
                int n = 0;
                foreach (var fx in _card.Effects ?? new List<CardEffectData>()) if (IsActiveEffect(fx)) n++;
                return n;
            }
        }

        private int AuraEffectCount
        {
            get
            {
                int n = 0;
                foreach (var fx in _card.Effects ?? new List<CardEffectData>()) if (IsAuraEffect(fx)) n++;
                return n;
            }
        }

        private int AutoEffectCount
        {
            get
            {
                int n = 0;
                foreach (var fx in _card.Effects ?? new List<CardEffectData>())
                    if (fx != null && !IsAuraEffect(fx) && !IsActiveEffect(fx)) n++;
                return n;
            }
        }

        // ---- 卡面刷新（Recalculate 统一收口） ----

        /// <summary>卡面实时预览（2026-11-06 布局改版）：ACardView 静态快照——类型块/立绘/徽章/
        /// 效果行全量重绘（与手牌/战场渲染同源；FromCardDataFull 无运行态订阅，幂等）。</summary>
        private void RefreshPreview()
        {
            if (_previewView != null)
                _previewView.Apply(CardOverlayItem.FromCardDataFull(_card, "ComposerPreview"), CardOverlayLayout.Full);
        }

        private void RefreshCardFace(CardCostCalculator.Result result)
        {
            RefreshPreview();

            // fields/cost-row "CostSquares " 文本节点=卡费文字公式（2026-10-16 二次改版：方格退役，
            // 口式参考效果组合界面费用公式——逐项「标签＝值」＋折扣＋合计/分布）
            if (_typeCostText != null)
                _typeCostText.text = CardCostFormulaText(result);

            // 悬浮费用构成（完整逐行明细；文本公式是它的紧凑投影）——挂费用公式节点
            if (_typeCostText != null && result != null)
            {
                var lines = new List<string>();
                foreach (var l in result.Breakdown) lines.Add($"{l.Label} {l.Value:0.0}");
                if (result.Grants.Count > 0)
                    lines.Add("获得 " + string.Join(" ", result.Grants.Select(kv => $"{(ManaType)kv.Key} {(int)kv.Value}")));
                lines.Add($"总档位 D={result.Total:0}");
                Describe(_typeCostText, "卡牌费用（自动算费，手动声明已退役）——构成：\n" + string.Join("\n", lines));
            }
        }

        // ---- 卡面放大（2026-11-06）：全屏遮罩 + 放大卡居中（DeckBuilder OpenZoom 同款） ----

        /// <summary>放大当前组合卡：遮罩点亮、卡面层池化挂入放大槽（点卡面不关闭——控制器给卡实例
        /// 挂的空 OnClick 吞点击；点遮罩/空白还原）。</summary>
        private void OpenZoom(CardData card)
        {
            if (card == null) return;
            EnsureZoomOverlay();
            if (_zoomOverlay == null || _zoomSlot == null) return;
            _zoomBindings.Clear();
            _zoomBindings.Add(new CardOverlayBinding
            {
                Slot = _zoomSlot,
                Item = CardOverlayItem.FromCardDataFull(card, "ComposerZoom"),
                Layout = CardOverlayLayout.Full,
            });
            CardOverlayController.Instance.Bind(_zoomBindings);
            if (_zoomSlot.childCount > 0)
                ((RectTransform)_zoomSlot.GetChild(0)).localScale = Vector2.one * ZoomCardScale;
            _zoomOverlay.SetAsLastSibling(); // 置顶（表单/弹层之上）
            _zoomOverlay.gameObject.SetActive(true);
            Debug.Log("[UI点击] 卡面放大（组合预览）");
        }

        /// <summary>放大遮罩懒建：半透明全屏底（Button=点按还原）+ 居中 360×648 放大槽。</summary>
        private void EnsureZoomOverlay()
        {
            if (_zoomOverlay != null) return;
            var go = new GameObject("zoom-overlay", typeof(RectTransform));
            _zoomOverlay = (RectTransform)go.transform;
            _zoomOverlay.SetParent(Root, false);
            UiKit.Stretch(_zoomOverlay);
            var mask = go.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.6f);
            mask.raycastTarget = true;
            var maskBtn = go.AddComponent<UnityEngine.UI.Button>();
            maskBtn.transition = UnityEngine.UI.Button.Transition.None;
            maskBtn.onClick.AddListener(CloseZoom);
            var slotGo = new GameObject("zoom-card", typeof(RectTransform));
            _zoomSlot = (RectTransform)slotGo.transform;
            _zoomSlot.SetParent(_zoomOverlay, false);
            _zoomSlot.anchorMin = _zoomSlot.anchorMax = _zoomSlot.pivot = new Vector2(0.5f, 0.5f);
            _zoomSlot.anchoredPosition = Vector2.zero;
            _zoomSlot.sizeDelta = new Vector2(360f, 648f);
            _zoomOverlay.gameObject.SetActive(false);
        }

        /// <summary>还原放大态：关遮罩+清绑定（UGUI 卡面层回收放大卡实例；未开过=空转）。</summary>
        private void CloseZoom()
        {
            if (_zoomOverlay == null || !_zoomOverlay.gameObject.activeSelf) return;
            _zoomBindings.Clear();
            CardOverlayController.ClearActive();
            _zoomOverlay.gameObject.SetActive(false);
        }

        // ======================================== 左栏：卡池筛选 ========================================

        /// <summary>过滤 chips 绑定（颜色行预制体烘焙 chip-{Color}；缺 chip 容错跳过——2026-10-16 prefab 无 chip-Gray）。
        /// 多选定案：再点同色移除；「全部」=清空。白/黑 chip 已随 2026-10-29 P4 色序清尾失活
        ///（UIColor 成员删除后不接线——防「可见但点不动」）。</summary>
        private void BindPoolFilter()
        {
            if (_poolFilter == null) return;
            foreach (var legacy in new[] { "chip-Black", "chip-White" })
            {
                var dead = UiKit.FindDeep(_poolFilter, legacy);
                if (dead != null) dead.gameObject.SetActive(false);
            }
            foreach (UIColor color in Enum.GetValues(typeof(UIColor)))
            {
                var captured = color;
                var chipRt = UiKit.FindDeep(_poolFilter, $"chip-{captured}");
                var chip = chipRt != null ? chipRt.GetComponentInChildren<UnityEngine.UI.Button>(true) : null;
                if (chip == null) continue;
                chip.onClick.AddListener(() =>
                {
                    if (captured == UIColor.All) _colorFilterSet.Clear();
                    else if (!_colorFilterSet.Remove(captured)) _colorFilterSet.Add(captured);
                    UpdatePoolFilter();
                    RefreshPool();
                });
                Describe(chip, captured == UIColor.All
                    ? "颜色筛选「全部」：清空已选颜色，不过滤"
                    : $"{ManaTypeNames.ZhNameOf(ManaOf(captured))}筛选（可多选）：只保留费用颜色都在所选集合内的卡；「全部」=清空");
                _filterChips[captured] = chip;
            }
            UpdatePoolFilter();
        }

        private void UpdatePoolFilter()
        {
            foreach (var kv in _filterChips)
            {
                bool active = kv.Key == UIColor.All ? _colorFilterSet.Count == 0 : _colorFilterSet.Contains(kv.Key);
                var img = kv.Value.GetComponent<Image>();
                if (img != null)
                {
                    if (!_filterChipBase.TryGetValue(kv.Key, out var baseCol)) _filterChipBase[kv.Key] = baseCol = img.color;
                    img.color = active ? UiStyle.ChipActiveBg : baseCol;
                }
                var ol = kv.Value.GetComponent<Outline>();
                if (active && ol == null)
                {
                    ol = kv.Value.gameObject.AddComponent<Outline>();
                    ol.effectColor = UiStyle.ChipActiveEdge;
                    ol.effectDistance = new Vector2(1.5f, -1.5f);
                }
                if (ol != null)
                {
                    ol.effectColor = UiStyle.ChipActiveEdge;
                    ol.enabled = active;
                }
            }
        }

        // ChipColors/Rgb（关键词 chip 配色）随 tpl-kwchip 退役删除——关键词槽行走统一模板视觉。

        // 2026-10-27 字阶对齐 DeckUI v2 三档——本屏局部常量（其他屏共用 UiStyle，不动全局）：
        // 25=按钮/chips、18=行名·输入框（行名字号烤在 prefab 模板）、16=meta·提示·费用数字。
        private const int RowMetaSize = 16;
        private const int BtnTextSize = 25;

        private const float PoolRowH = 56f;   // 卡行高=模板行（56·圆片费标+名称+右侧标签，2026-10-27 对齐 DeckUI 行规格）
        private const float PoolBtnH = 40f;   // 空态按钮行高
        private HashSet<string> _poolLibIds;  // 本轮刷新的库效果 id 集（断链标记，bind 时消费）
        private Color _poolRowBgDefault = Color.white; // 卡行模板底色（选中态还原基准，工厂首次捕获）

        private void RefreshPool()
        {
            if (_poolRows == null) return;
            var pool = CardCatalog.LoadPlayPool(); // 教学专用卡/衍生物不进编辑器卡池预览
            _poolLibIds = ReferenceAudit.LibraryEffectIds(); // 卡池行⚠断链标记（删库效果联动）

            // 预组段（2026-10-31 参考效果库预设段定案）：主表回设计态清空后卡池常态空——
            // 预组卡组引用的卡只读并入展示（同 ID 副本已在主池则不重复列）；载入走另存通道
            var presets = PresetPoolCards(pool);

            IEnumerable<CardData> cards = pool;
            IEnumerable<CardData> presetCards = presets;
            if (_colorFilterSet.Count > 0)
            {
                cards = cards.Where(c => CostColorsWithin(c, _colorFilterSet));
                presetCards = presetCards.Where(c => CostColorsWithin(c, _colorFilterSet));
            }
            var tokens = _searchField != null ? TokenizeSearch(_searchField.text) : new List<string>();
            if (tokens.Count > 0)
            {
                cards = cards.Where(c => MatchCorpus(CorpusOf(c), tokens));
                presetCards = presetCards.Where(c => MatchCorpus(CorpusOf(c), tokens));
            }

            var sorted = CardSorter.Sort(cards).ToList();
            var sortedPresets = CardSorter.Sort(presetCards).ToList();
            if (tokens.Count > 0) // 搜索态名称命中置前
            {
                sorted = sorted.OrderByDescending(c => NameHitsAny(c, tokens)).ToList();
                sortedPresets = sortedPresets.OrderByDescending(c => NameHitsAny(c, tokens)).ToList();
            }

            _poolItems.Clear();
            // 排序仍按组分（生物前/法术后·2024-09-24 口径）；分组标题 UI 已删（2024-10-21）
            foreach (var c in sorted.Where(c => c.Supertype == Cardtype.Creature))
                _poolItems.Add(new PoolRowData { Kind = 1, Card = c, H = PoolRowH });
            foreach (var c in sorted.Where(c => c.Supertype != Cardtype.Creature))
                _poolItems.Add(new PoolRowData { Kind = 1, Card = c, H = PoolRowH });

            if (sortedPresets.Count > 0)
            {
                const string t = "—— 预设卡组卡（教学数据·载入改动保存＝另存新卡，源档不可删）——";
                _poolItems.Add(new PoolRowData
                {
                    Kind = 2, Text = t,
                    H = UiKit.MeasureTextHeight(t, _poolList.Content.rect.width - 16f, RowMetaSize) + 8f,
                });
                foreach (var c in sortedPresets.Where(c => c.Supertype == Cardtype.Creature))
                    _poolItems.Add(new PoolRowData { Kind = 1, Card = c, Preset = true, H = PoolRowH });
                foreach (var c in sortedPresets.Where(c => c.Supertype != Cardtype.Creature))
                    _poolItems.Add(new PoolRowData { Kind = 1, Card = c, Preset = true, H = PoolRowH });
            }
            else if (sorted.Count == 0)
                AddPoolEmptyItems(pool.Count == 0 && presets.Count == 0, tokens);

            string poolSig = string.Join("|", _poolItems.Select(x => x.Card != null ? x.Card.ID : x.Kind + ":" + x.Text));
            if (_poolItems.Count > 0 && poolSig == _poolSig && _poolRows.Count == _poolItems.Count)
            {
                _poolRows.RebindVisible(); // 数据未变（点选载入仅选中高亮变）：不回顶重铺
                return;
            }
            _poolSig = poolSig;
            _poolRows.SetItems(_poolItems.Count, i => _poolItems[i].H);
        }

        /// <summary>预组卡组引用的卡集（预设段数据源·同效果库 AppendPresetEffectRows 口径）：
        /// DeckCatalog 预组 → cardIds → 逐 id 取卡；主池已存同 ID 副本不重复列（主表行为准）；
        /// 衍生物模板卡不进池（同 LoadPlayPool 口径）；多卡组同卡去重。</summary>
        private static List<CardData> PresetPoolCards(List<CardData> pool)
        {
            var poolIds = new HashSet<string>(pool.Select(c => c.ID));
            var result = new List<CardData>();
            var seen = new HashSet<string>();
            foreach (var deck in DeckCatalog.LoadPresets())
            {
                if (deck?.cardIds == null) continue;
                foreach (var cardId in deck.cardIds)
                {
                    if (string.IsNullOrEmpty(cardId) || !seen.Add(cardId)) continue;
                    if (poolIds.Contains(cardId)) continue;
                    var card = CardCatalog.GetById(cardId);
                    if (card == null || card.IsTokenTemplate) continue;
                    result.Add(card);
                }
            }
            return result;
        }

        /// <summary>卡池行工厂路由（kind 分池：1 卡行 / 2 提示 / 3 清筛选钮）。</summary>
        private UiKit.RecycledList.MakeFn PoolRowFactory(int kind)
        {
            switch (kind)
            {
                case 2: return MakePoolHintRow;
                case 3: return MakePoolClearRow;
                default: return MakePoolCardRow;
            }
        }

        /// <summary>提示行结构（工厂）：16 号·暗色·自动换行。</summary>
        private RectTransform MakePoolHintRow()
        {
            var rt = UiKit.Node("hint", _poolList.Content);
            var lbl = UiKit.Label("lbl", rt, "", RowMetaSize, UiStyle.TextHint, wrap: true);
            UiKit.StretchInset(lbl.rectTransform, 4f, 4f);
            return rt;
        }

        /// <summary>清筛选钮行结构（工厂）：左置按钮（清颜色+搜索词，空态用；25 号字·DeckUI 按钮档）。</summary>
        private RectTransform MakePoolClearRow()
        {
            var rt = UiKit.Node("clr", _poolList.Content);
            var btn = UiKit.Button("btn", rt, "清除全部", null, null, null, 110f, PoolBtnH, BtnTextSize);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 0.5f);
            brt.anchoredPosition = new Vector2(4f, 0f);
            return rt;
        }

        /// <summary>卡行结构（工厂）：list-effects 自带 inactive row 模板克隆（Image+Button+CostSquares+meta）。</summary>
        private RectTransform MakePoolCardRow()
        {
            var row = CloneRow(_poolList);
            if (row == null) return UiKit.Node("row-miss", _poolList.Content);
            var img = row.GetComponent<Image>();
            if (img != null) _poolRowBgDefault = img.color; // 选中态还原基准
            return row;
        }

        /// <summary>卡池绑定：kind 分派（卡行/提示/清筛选钮）。</summary>
        private void BindPoolRow(RectTransform row, int index)
        {
            var item = _poolItems[index];
            switch (item.Kind)
            {
                case 2:
                {
                    var lbl = row.GetComponentInChildren<TMP_Text>(true);
                    if (lbl != null) lbl.text = item.Text;
                    return;
                }
                case 3:
                {
                    var btn = row.GetComponentInChildren<UnityEngine.UI.Button>(true);
                    if (btn == null) return;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        _colorFilterSet.Clear();
                        if (_searchField != null) _searchField.SetTextWithoutNotify("");
                        UpdatePoolFilter();
                        RefreshPool();
                    });
                    Describe(btn, "清空颜色/搜索词全部筛选，恢复完整卡池");
                    return;
                }
            }

            // ---- 卡行（2026-10-27 行模板：f18 名称+右侧 f16 标签+26 圆片费标；详情走悬浮） ----
            var card = item.Card;
            bool selected = !string.IsNullOrEmpty(_editingOriginalId) && card.ID == _editingOriginalId;
            bool broken = (card.Effects ?? new List<CardEffectData>())
                .Any(fx => fx != null && !string.IsNullOrEmpty(fx.Id) && !_poolLibIds.Contains(fx.Id));

            var keywords = (card.Keywords ?? new List<string>())
                .Select(KeywordZh).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var summaries = (card.Effects ?? new List<CardEffectData>())
                .Select(fx => AtomText.RenderEffectSummary(
                    new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, card))
                .Where(s => !string.IsNullOrEmpty(s)).ToList();
            var detailParts = new List<string>();
            if (keywords.Count > 0) detailParts.Add(string.Join("·", keywords));
            if (summaries.Count > 0) detailParts.Add(string.Join("｜", summaries));

            string displayName = (string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName);
            if (item.Preset) displayName = "［预设］" + displayName;
            var nameText = UiKit.FindDeep(row, "name")?.GetComponent<TMP_Text>();
            if (nameText != null)
            {
                nameText.text = (broken ? "⚠ " : "") + displayName;
                nameText.color = broken ? UiStyle.ErrorRed : new Color(0.88f, 0.9f, 0.93f); // 行池复用须显式还原
            }
            var metaText = UiKit.FindDeep(row, "meta")?.GetComponent<TMP_Text>();
            if (metaText != null) metaText.text = TypeStatLine(card);
            BindCostBadge(row, card.Cost);

            var img = row.GetComponent<Image>();
            if (img != null) img.color = selected ? UiStyle.SelectedRowBg : _poolRowBgDefault; // 池行复用须显式还原
            var btn2 = row.GetComponent<UnityEngine.UI.Button>();
            if (btn2 != null)
            {
                btn2.onClick.RemoveAllListeners();
                btn2.onClick.AddListener(() =>
                {
                    if (item.Preset) LoadCardForEdit(card, presetSource: true);
                    else LoadCardForEdit(card);
                });
            }
            Describe(row, (item.Preset
                    ? $"点击载入预设卡「{displayName}」到中间编辑（预设=教学数据源档，保存＝另存新卡、不可删）"
                    : $"点击载入「{displayName}」到中间编辑（保存=覆盖替换旧卡）")
                + $"｜{TypeStatLine(card)}"
                + (detailParts.Count > 0 ? "\n" + string.Join("\n", detailParts) : "")
                + (broken ? "\n⚠该卡有效果引用断链（库效果已删/换代），装载时该效果会被跳过" : ""));
        }

        /// <summary>费用徽标写值（v2 行模板：cost 圆片+数字；无费用数据=隐藏——池行复用须显式还原）。</summary>
        private static void BindCostBadge(RectTransform row, ElementCost cost)
        {
            var badge = UiKit.FindDeep(row, "cost");
            if (badge == null) return;
            if (cost == null || cost.IsZero)
            {
                badge.gameObject.SetActive(false);
                return;
            }
            badge.gameObject.SetActive(true);
            var num = badge.GetComponentInChildren<TMP_Text>(true);
            if (num != null) num.text = ((int)Mathf.Round(cost.Total)).ToString();
        }

        private static string TypeStatLine(CardData card)
        {
            switch (card.Supertype)
            {
                case Cardtype.Creature:
                    return $"生物 {(card.Power ?? 0)}/{(card.Life ?? 0)}";
                case Cardtype.Enchantment:
                    return $"{(card.IsField ? "场地" : "")}结界 耐久{CardCompositionCost.TotalDurabilityOf(card)}"; // 2026-10-17：恒有耐久（派生单源）；场地标记（P8）前缀
                case Cardtype.Spell:
                {
                    // 速度归卡类别（2026-11-06 定案·单源 EffectSpeedOf=与引擎 GetCardCastSpeed 同口）：
                    // 法术 meta 补「速度X」对齐生物/结界的额外信息档；生物速度恒0、结界无速度概念不显
                    var speed = CardOverlayItem.EffectSpeedOf(card);
                    return "法术" + (speed.HasValue ? $" 速度{speed.Value}" : "");
                }
                default:
                    return CardSorter.TypeName(card);
            }
        }

        private static string KeywordZh(string keywordId)
        {
            var def = CardLoader.GetKeywordDefinition(keywordId);
            return string.IsNullOrEmpty(def?.nameZh) ? keywordId : def.nameZh;
        }

        // ======================================== 检索与分面 ========================================

        // 语义契约：空白切词、多词 AND（非连续子串）、OrdinalIgnoreCase、无词不过滤。
        private static List<string> TokenizeSearch(string query) =>
            string.IsNullOrWhiteSpace(query)
                ? new List<string>()
                : query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();

        private static bool MatchCorpus(string corpus, List<string> tokens) =>
            tokens.All(t => corpus.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool NameHitsAny(CardData card, List<string> tokens) =>
            tokens.Any(t => (card.CardName ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>卡级语料：卡名∪ID∪卡型中文∪关键词∪Tags∪效果全文∪原子中文短名。缓存按卡 ID（内容寻址）。</summary>
        private static string CorpusOf(CardData card)
        {
            if (card == null) return "";
            if (CorpusCache.TryGetValue(card.ID ?? "", out var cached)) return cached;
            var parts = new List<string> { card.CardName ?? "", card.ID ?? "", CardSorter.TypeName(card) };
            foreach (var k in card.Keywords ?? new List<string>())
            {
                parts.Add(k);
                var def = CardLoader.GetKeywordDefinition(k);
                if (!string.IsNullOrEmpty(def?.nameZh)) parts.Add(def.nameZh);
            }
            parts.AddRange(card.Tags ?? new List<string>());
            foreach (var fx in card.Effects ?? new List<CardEffectData>())
            {
                if (fx == null) continue;
                if (!string.IsNullOrEmpty(fx.DisplayName)) parts.Add(fx.DisplayName);
                parts.Add(AtomText.RenderEffectSummary(
                    new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, card));
            }
            foreach (var n in CardAtomShortNames(card)) parts.Add(n);
            var corpus = string.Join("\n", parts.Where(s => !string.IsNullOrEmpty(s)));
            CorpusCache[card.ID ?? ""] = corpus;
            return corpus;
        }

        /// <summary>卡内效果原子的表行中文短名集合（config.DisplayName；含分支 then 奖励原子）。</summary>
        private static HashSet<string> CardAtomShortNames(CardData card)
        {
            var names = new HashSet<string>();
            if (card?.Effects == null) return names;
            foreach (var fx in card.Effects)
            {
                if (fx == null) continue;
                foreach (var atom in ProjectLinear(fx.Steps))
                {
                    if (atom == null || string.IsNullOrEmpty(atom.refId)) continue;
                    var cfg = AtomicEffectTable.GetByHashId(atom.refId);
                    if (!string.IsNullOrEmpty(cfg?.DisplayName)) names.Add(cfg.DisplayName);
                }
            }
            return names;
        }

        private static readonly UIColor[] FilterColorOrder =
            { UIColor.Gray, UIColor.Red, UIColor.Blue, UIColor.Green }; // 白/黑已随 2026-10-28 P2 席位收缩移除

        /// <summary>颜色多选子集语义：费用所用色集 ⊆ 选中集；0 费/无费用数据仅在未选任何色时显示。</summary>
        private static bool CostColorsWithin(CardData card, HashSet<UIColor> selected) =>
            CostColorsWithin(card?.Cost, selected);

        private static bool CostColorsWithin(ElementCost cost, HashSet<UIColor> selected)
        {
            if (cost == null || cost.IsZero) return false;
            foreach (var color in FilterColorOrder)
                if (cost[ManaOf(color)] > 0f && !selected.Contains(color)) return false;
            return true;
        }

        private static ManaType ManaOf(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return ManaType.Red;
                case UIColor.Blue: return ManaType.Blue;
                case UIColor.Green: return ManaType.Green;
                default: return ManaType.Gray; // All/灰落灰（白/黑已随 2026-10-29 P4 色序清尾删除）
            }
        }

        // ---- 效果类别 chips：已随 2026-10-16 槽位化定案退役（右栏只留颜色多选+模糊串搜索） ----

        /// <summary>空态可解释（条目装配）：库空 vs 无匹配分开说；无匹配时列出当前生效筛选 +「清除全部」。</summary>
        private void AddPoolEmptyItems(bool poolEmpty, List<string> tokens)
        {
            if (poolEmpty)
            {
                const string t = "卡池为空（0 张）——检查 StreamingAssets/Card/Cards.json 与 Console 的 [CardCatalog]/[CardLoader] 警告";
                _poolItems.Add(new PoolRowData
                {
                    Kind = 2, Text = t,
                    H = UiKit.MeasureTextHeight(t, _poolList.Content.rect.width - 16f, RowMetaSize) + 8f,
                });
                return;
            }
            var constraints = new List<string>();
            if (_colorFilterSet.Count > 0)
                constraints.Add("色：" + string.Join("/", _colorFilterSet.Select(c => c.ToString())));
            if (tokens.Count > 0)
                constraints.Add("词：" + string.Join(" ", tokens));
            var text = constraints.Count == 0
                ? "（无卡牌可显示）"
                : "（无匹配卡牌）当前筛选：" + string.Join(" ＋ ", constraints);
            _poolItems.Add(new PoolRowData
            {
                Kind = 2, Text = text,
                H = UiKit.MeasureTextHeight(text, _poolList.Content.rect.width - 16f, RowMetaSize) + 8f,
            });
            if (constraints.Count > 0)
                _poolItems.Add(new PoolRowData { Kind = 3, Text = "清除全部", H = PoolBtnH });
        }

        // ======================================== 载入编辑 / 新建 / 删除 ========================================

        /// <summary>载入已有卡编辑（覆盖替换语义；深拷贝容器、共享效果实例——效果合成器写回=整实例替换）。
        /// presetSource=true＝预设段预组卡（2026-10-31 参考效果库预设段）：另存通道——保存不触发
        /// 覆盖替换闸（源档=教学数据，改后内容哈希必换新 ID=另存新卡；被预组卡组引用不拦截）。</summary>
        private void LoadCardForEdit(CardData src, bool presetSource = false)
        {
            // 2026-11-05 ID 冻结：编辑既有卡带上原 id——保存走原地覆盖，卡组/存档引用不断。
            // 预设段=另存语义，故意清空 id 让其按内容重新分配。
            _card.ID = presetSource ? null : src.ID;
            _card.CardName = src.CardName;
            _card.Supertype = src.Supertype;
            _card.Illustration = src.Illustration;
            _card.Power = src.Power;
            _card.Life = src.Life;
            _card.Durability = src.Durability;
            _card.IsField = src.IsField && src.Supertype == Cardtype.Enchantment; // 场地标记（P8）：非结界脏数据不带入
            _card.Keywords = src.Keywords != null ? new List<string>(src.Keywords) : new List<string>();
            _card.Tags = src.Tags != null ? new List<string>(src.Tags) : new List<string>();
            _card.Effects = src.Effects != null ? new List<CardEffectData>(src.Effects) : new List<CardEffectData>();
            _card.Cost = src.Cost != null ? src.Cost.Clone() : null;
            //（卡级 PayloadCost 已随 2026-11-08 代价效果级化整删——代价随效果 Costs 走效果数据）
            _card.ArrowDirections = src.ArrowDirections;
            _card.LinkAuras = src.LinkAuras != null ? new List<LinkAuraData>(src.LinkAuras) : null;
            // 系统动作资格位（2026-11-06 opt-in）：攻/守仅生物带入（脏数据不带入——装载端同降级口径）
            _card.AttackAbility = src.AttackAbility && src.Supertype == Cardtype.Creature;
            _card.GuardAbility = src.GuardAbility && src.Supertype == Cardtype.Creature;
            _card.ChargeAbility = src.ChargeAbility;
            _card.SpeedBonus = src.SpeedBonus;
            _card.ResetCache();
            _card.AggregateEffectAuras(true);
            _editingOriginalId = src.ID;
            _editingPresetSource = presetSource;
            _kind = MapKind(src.Supertype);
            ResetSlotState(); // 槽位选中/空槽随载入复位（追加态）

            // 载入归一（2026-10-16 槽位化）：生物属性 null 视作 0（必填口径只看输入框留空）；
            // 非生物关键词违规旧卡直接清空（关键词=生物专属），防隐藏区无法移除的死锁
            bool purgedKeywords = _kind != CardKind.Creature && _card.Keywords.Count > 0;
            if (purgedKeywords) _card.Keywords = new List<string>();
            if (_kind == CardKind.Creature)
            {
                _card.Power = _card.Power ?? 0;
                _card.Life = _card.Life ?? 0;
            }

            SyncNameInput();
            SyncKindButtons();
            RebuildCardPanels();
            ShowToast((purgedKeywords ? "该卡带本体关键词——已按「关键词为生物专属」清空；" : "")
                + $"编辑：{_card.CardName ?? src.ID}"
                + (presetSource ? "（预设卡——保存＝另存新卡，源档不动）" : "（保存将覆盖替换旧卡）"));
        }

        private void OnNewCard()
        {
            _card.ID = null; // 2026-11-05 ID 冻结：新建卡无 id，保存时按内容指纹分配
            _card.CardName = $"新建·{++_newCardSeq}"; // 默认名（2026-10-23 定案：不补零、无上限）；玩家可改
            _card.Supertype = Cardtype.Creature;
            _card.Illustration = null; // 立绘复位（画廊高亮随 RefreshCardFace 对账）
            _card.Power = 0;
            _card.Life = 0;
            _card.Durability = 0;
            _card.IsField = false; // 场地标记复位（P8）
            _card.Keywords = new List<string>();
            _card.Tags = new List<string>();
            _card.Effects = new List<CardEffectData>();
            _card.Cost = null;
            //（卡级 PayloadCost 已随 2026-11-08 代价效果级化整删）
            _card.ArrowDirections = default;
            _card.LinkAuras = null;
            _card.AttackAbility = _card.GuardAbility = _card.ChargeAbility = false; // 资格位复位（2026-11-06 opt-in：新建默认不带）
            _card.SpeedBonus = 0;
            _card.ResetCache();
            _editingOriginalId = null;
            _editingPresetSource = false;
            _kind = CardKind.Creature;
            ResetSlotState(); // 槽位选中/空槽复位（追加态）

            SyncNameInput();
            SyncKindButtons();
            RebuildCardPanels();
        }

        /// <summary>槽位选中态与空槽全清（载入/新建复位；不触右栏刷新——调用方随全量刷新）。</summary>
        private void ResetSlotState()
        {
            _selType = null;
            _selIndex = -1;
            _emptySlots.Clear();
        }

        private void RebuildCardPanels()
        {
            SyncZoneVisibility();
            SyncBasicAbility();
            BuildArtGallery(); // 画廊高亮随载入/新建对账（类型换源后的统一收口）
            RefreshSlots();
            SetAttrTexts(); // 攻/血留空口径：新建空、载入回显
            _card.AggregateEffectAuras(true);
            Recalculate();
            RefreshPool();
        }

        /// <summary>删除卡池中的当前卡（2026-10-16 强制删除机制定案）：无引用直接删；
        /// 被卡组引用→TipPanel 提示+允许强制（2026-10-18 弹窗统一）——强删后卡组断链，卡组界面按
        /// DeckCompositionValidator.MissingIds 标记「不可用」。</summary>
        private void OnDeleteCard()
        {
            if (_editingPresetSource)
            {
                ShowToast("预设卡为教学数据源档，不可删——载入改动保存即另存新卡");
                return;
            }
            if (string.IsNullOrEmpty(_editingOriginalId))
            {
                ShowToast("未在编辑已有卡——先在右栏「卡池」点击要删的卡载入，再点删除");
                return;
            }
            var refs = ReferenceAudit.DecksReferencingCard(_editingOriginalId);
            if (refs.Count == 0)
            {
                DeleteCurrentCard(0);
                return;
            }
            ConfirmForceDeleteCardAsync(refs).Forget();
        }

        /// <summary>强删确认（TipPanel 双键）：取消/拆屏=null/否=不动。</summary>
        private async UniTaskVoid ConfirmForceDeleteCardAsync(List<string> refs)
        {
            if (await Tips.ShowAsync(
                    $"「{_card.CardName}」被 {refs.Count} 个卡组引用：\n{string.Join("、", refs)}\n\n"
                    + "强制删除后这些卡组将断链（卡组界面会标记「断链·不可用」）。",
                    "强制删除", "取消") != true)
                return;
            DeleteCurrentCard(refs.Count);
        }

        private void DeleteCurrentCard(int brokenDecks)
        {
            if (!CardConfigSerializer.Remove(_editingOriginalId))
            {
                ShowToast("删除失败：卡表中未找到该 ID（可能已被外部修改——重进界面刷新卡池）");
                return;
            }
            CardCatalog.Invalidate();
            CorpusCache.Clear();
            TutorialCreationFlow.NotifyCardsChanged(); // 第三课走查步检测（未开课零行为）
            var deleted = _card.CardName;
            OnNewCard();
            RefreshLibrary();
            ShowToast(brokenDecks > 0
                ? $"已删除「{deleted}」——{brokenDecks} 个卡组引用断链，卡组界面已标记不可用"
                : $"已从卡表删除「{deleted}」");
        }

        // ======================================== 中栏表单逻辑 ========================================

        private static CardKind MapKind(Cardtype type)
        {
            switch (type)
            {
                case Cardtype.Spell: return CardKind.Spell;
                case Cardtype.Enchantment: return CardKind.Enchantment;
                default: return CardKind.Creature;
            }
        }

        // ---------- 业务类型 → 后端字段 ----------
        // 设置 Supertype；清掉与新类型无关的额外字段（保持模型干净）。
        private void ApplyKindToCard()
        {
            switch (_kind)
            {
                case CardKind.Creature:
                    _card.Supertype = Cardtype.Creature;
                    _card.Durability = 0; // 耐久是结界专属
                    _card.IsField = false; // 场地标记是结界专属（P8）
                    _card.SpeedBonus = 0; // 字段已退役（2026-10-15 改版：速度动态算不落字段）
                    if (_card.Illustration == "Title") _card.Illustration = null; // Title=法术/结界默认图，生物清回占位
                    // 生物禁挂连接光环（2026-10-15 改版）：切类型即清光环与箭头（防旧数据残留）
                    _card.LinkAuras = new List<LinkAuraData>();
                    _card.ArrowDirections = HexDirection.None;
                    break;
                case CardKind.Spell:
                    _card.Supertype = Cardtype.Spell;
                    _card.Power = null;
                    _card.Life = null;
                    _card.Durability = 0;
                    _card.IsField = false; // 场地标记是结界专属（P8）
                    _card.AttackAbility = _card.GuardAbility = false; // 攻/守=生物专属（2026-11-06 opt-in，切型即清）
                    _card.SpeedBonus = 0;
                    _card.Illustration = "Title"; // 2026-11-06 默认图定案：法术自动选 Title（占位项可清空）
                    _card.LinkAuras = new List<LinkAuraData>(); // 法术只挂主动效果
                    _card.ArrowDirections = HexDirection.None;
                    _card.Keywords = new List<string>(); // 关键词=生物专属（2026-10-16 槽位化）
                    break;
                case CardKind.Enchantment:
                    _card.Supertype = Cardtype.Enchantment;
                    _card.Power = null;
                    _card.Life = null; // 结界=耐久体（2026-09-24 定案），不用攻/血
                    _card.AttackAbility = _card.GuardAbility = false; // 攻/守=生物专属（2026-11-06 opt-in，切型即清）
                    _card.SpeedBonus = 0; // 速度通道退役（2026-10-15 改版：结界禁主动，免费点全投耐久）
                    _card.Illustration = "Title"; // 2026-11-06 默认图定案：结界自动选 Title（占位项可清空）
                    _card.Keywords = new List<string>(); // 关键词=生物专属（2026-10-16 槽位化）
                    break;
            }
        }

        private bool HasStats => _kind == CardKind.Creature;
        private bool HasDurability => _kind == CardKind.Enchantment;

        // ======================================== 效果槽（effect 区=主动 / passives 区=其余） ========================================

        /// <summary>主动效果判据（CardOverlayItem.FromCardDataFull 同源）：首个主动上 effect 区，其余归被动。</summary>
        private static bool IsActiveEffect(CardEffectData fx)
        {
            if (fx == null) return false;
            var t = (TriggerTiming)fx.TriggerTiming;
            return t == TriggerTiming.Activate_Active || t == TriggerTiming.Activate_Instant;
        }

        /// <summary>效果级净费（2026-11-06 方格改版数据源）：带效果级代价的主动效果=发动费−抵扣
        ///（EffectActivationGate.NetBill 单源：先灰后最高费用色、逐色 ≥0）；无代价行 null
        ///（调用方回落锚价/模式费账单）。原「发动费→抵扣→净费」公式文本随本改版退役——
        /// 信息由 CostSquares 双组承载（费用组=净费、代价组=EffectPayloadCostOf）。</summary>
        internal static ElementCost ActiveEffectNetCost(CardEffectData fx)
        {
            var (fee, pts, net, atom) = ActiveEffectCostParts(fx);
            return fee == null ? null : net;
        }

        /// <summary>效果级代价分色面 → ElementCost（Cost2 代价组数据源，2026-11-06）：经 ConvertOne
        /// 契约转换（违约行剔除同运行时）→ PayloadGrantByColor 按原子可产出色记账。
        /// null=无效果级代价（调用方回落卡级施放代价 PayloadCost）。</summary>
        internal static ElementCost EffectPayloadCostOf(CardEffectData fx)
        {
            if (fx?.Costs == null) return null;
            var def = CardEffectConverter.ConvertOne(fx, "COMPOSER_COST_PREVIEW");
            var special = def?.Costs != null
                ? def.Costs.Where(c => c != null && c.Type != CostType.ElementConsume).ToList()
                : null;
            if (special == null || special.Count == 0) return null;
            var grant = CardCostService.PayloadGrantByColor(special);
            if (grant == null || grant.Count == 0) return null;
            var cost = new ElementCost();
            foreach (var kv in grant) cost[kv.Key] = kv.Value;
            return cost.IsZero ? null : cost;
        }

        /// <summary>P5 净费四段（发动费/抵扣点/净费/首个代价原子）：数据口=fx.Costs 首个 payload 条目，经
        /// CardEffectConverter 装载（契约校验同运行时）→ EffectActivationGate 抵扣；无代价行 fee=null。
        /// internal（P4 2026-10-29）：ACard 卡面内联同源复用。</summary>
        internal static (ElementCost fee, int pts, ElementCost net, CardCore.AtomicEffectInstance atom) ActiveEffectCostParts(CardEffectData fx)
        {
            if (!IsActiveEffect(fx) || fx?.Costs == null) return (null, 0, null, null);
            // payload/payloads 两形态同收（2026-11-06 修复：此前只认 legacy ce.payload——
            // 新形态 payloads 的代价行文本有、净费公式/抵扣读数恒缺）
            bool hasPayload = false;
            foreach (var ce in fx.Costs)
            {
                var ps = ce?.AllPayloads();
                if (ps == null) continue;
                foreach (var p in ps)
                    if (p != null && !string.IsNullOrEmpty(p.refId)) { hasPayload = true; break; }
                if (hasPayload) break;
            }
            if (!hasPayload) return (null, 0, null, null);
            var def = CardEffectConverter.ConvertOne(fx, "COMPOSER_NET_PREVIEW");
            var special = def?.Costs != null
                ? def.Costs.Where(c => c != null && c.Type != CostType.ElementConsume).ToList()
                : null;
            if (special == null || special.Count == 0) return (null, 0, null, null); // 契约违约被剔除——不显示
            int pts = EffectActivationGate.DeductionPoints(special);
            if (pts <= 0) return (null, 0, null, null);
            var fee = CostDerivationService.DeriveElementCosts(def, 0);
            var atom = special.SelectMany(c => c.Payloads ?? new List<CardCore.AtomicEffectInstance>())
                .FirstOrDefault(a => a != null);
            return (fee, pts, EffectActivationGate.NetBill(fee, special), atom);
        }

        /// <summary>槽行重建（2026-10-16 二次改版）：四类槽行=克隆对应单槽模板（text 标题/des 内容/delete 删除钮）；
        /// 已填槽在前、空槽在后；选中槽=白描边+行底色。效果全文写进 des。</summary>
        private void RefreshSlots()
        {
            EnsureCardLists();
            if (_editorRoot == null) return;
            // 删槽后选中下标越界回收（空槽 -1 不动）
            if (_selIndex >= 0)
            {
                if (_selType == ZoneId.Effect || _selType == ZoneId.Passives)
                {
                    if (_selIndex >= _card.Effects.Count) _selIndex = -1;
                }
                else if (_selType == ZoneId.Keys && _selIndex >= _card.Keywords.Count)
                {
                    _selIndex = -1;
                }
            }
            ClearRuntimeChildren(FormContent());
            BuildSlots(ZoneId.Effect); // cost 区已随 2026-11-06 代价挪构筑期退役（模板删除）
            BuildSlots(ZoneId.Passives);
            BuildSlots(ZoneId.Keys);
            SyncAddSlotButtons();
            RefreshPreview(); // 卡面效果行随槽内容同拍（多数路径随后还有 Recalculate 兜底）
        }

        /// <summary>逐槽行渲染：已填（数据下标）在前、空槽（_emptySlots）在后；
        /// 与卡型不匹配的已填槽仍渲染（可删——保存闸兜底拦）。</summary>
        private void BuildSlots(ZoneId id)
        {
            if (!_slotTemplates.TryGetValue(id, out var tpl) || tpl == null) return;
            int filled = FilledSlotCount(id);
            int empties = EmptySlotCount(id);
            if (filled == 0 && empties == 0) return;
            int ordinal = 0;
            for (int i = 0; i < filled; i++) MakeSlotRow(id, tpl, i, ordinal++);
            for (int e = 0; e < empties; e++) MakeSlotRow(id, tpl, -1, ordinal++);
        }

        /// <summary>中栏表单滚动内容（2026-11-06 布局改版：CardPreView 内联 form-scroll/viewport/content）——
        /// 槽模板与克隆行的共同父（与 ACard 卡面实例的内部 form-scroll 是两棵树）。</summary>
        private RectTransform FormContent()
        {
            if (_formContent == null && _editorRoot != null)
                _formContent = _editorRoot.Find("form-scroll/viewport/content") as RectTransform;
            return _formContent;
        }

        /// <summary>单槽行（克隆模板）：text=序号标题、des=效果全文/关键词/代价文案（空槽=操作提示）、
        /// delete=删除（已填删数据/空槽删占位）；行点击=选中该槽（右栏按槽类型过滤，点选候选填入）。</summary>
        private void MakeSlotRow(ZoneId id, RectTransform tpl, int dataIndex, int ordinal)
        {
            var formCt = FormContent();
            var row = formCt != null ? UiKit.CloneTemplateFrom(formCt, tpl.name, formCt) : null;
            if (row == null) return;
            row.name = "rt-slot";
            // 紧跟同类型模板之后排布（四类分组：cost→effect→passives→keys，add-slot 恒末位）
            row.SetSiblingIndex(tpl.GetSiblingIndex() + 1 + ordinal);

            bool selected = _selType == id && _selIndex == dataIndex;
            var img = row.GetComponent<Image>();
            if (img != null && selected) img.color = UiStyle.SelectedRowBg;
            var ol = row.GetComponent<Outline>();
            if (ol != null)
            {
                if (selected)
                {
                    ol.effectColor = UiStyle.TextPrimary; // 选中=白描边
                    ol.enabled = true;
                }
                else if (_slotOutlineBase.TryGetValue(id, out var b))
                {
                    ol.effectColor = b.color;
                    ol.enabled = b.on;
                }
            }

            var title = UiKit.FindDeep(row, "text")?.GetComponent<TMP_Text>();
            if (title != null) title.text = SlotTitle(id, ordinal);
            var des = UiKit.FindDeep(row, "des")?.GetComponent<TMP_Text>();
            if (des != null)
            {
                des.text = SlotDesText(id, dataIndex);
                des.color = dataIndex < 0 ? UiStyle.TextHint
                    : (!TypeEnabledForKind(id) ? UiStyle.ErrorRed : UiStyle.TextBody);
            }

            var del = UiKit.FindDeep(row, "delete")?.GetComponent<UnityEngine.UI.Button>();
            if (del != null)
            {
                var capturedIndex = dataIndex;
                del.onClick.RemoveAllListeners();
                del.onClick.AddListener(() =>
                {
                    Debug.Log("[UI点击] 槽delete " + id + " #" + (capturedIndex + 1));
                    DeleteSlot(id, capturedIndex);
                });
                Describe(del, capturedIndex < 0
                    ? $"删除这个空{TypeZh(id)}槽（未填内容，不影响卡数据）"
                    : $"删除该{TypeZh(id)}槽内容（卡面光环随剩余内容重算；要改内容=删后重挂）");
            }

            var btn = row.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                var capturedIndex = dataIndex;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    Debug.Log("[UI点击] 槽行选中 " + id + " #" + (capturedIndex + 1));
                    SelectSlot(id, capturedIndex);
                });
            }
            Describe((Component)btn ?? row, SlotRowDesc(id, dataIndex));
            BindSetDerivant(row, dataIndex); // 显隐=该效果含衍生物/变形原子（无钮模板空转）
        }

        private static string SlotTitle(ZoneId id, int ordinal) =>
            (id == ZoneId.Effect ? "主动槽" : id == ZoneId.Passives ? "自动槽" : "关键词")
            + (char)('①' + ordinal) + "：";

        /// <summary>槽行 des 内容：已填=渲染摘要单遍（效果名即全文定案下不再名+摘要并列）/关键词；
        /// 空槽=操作提示。与卡型不匹配的已填槽前缀⚠（保存闸兜底）。
        ///（代价槽已随 2026-11-08 代价效果级化整退役——代价=效果 Costs，编辑在效果合成器。）</summary>
        private string SlotDesText(ZoneId id, int dataIndex)
        {
            if (dataIndex < 0)
                return $"（空槽）点击行选中 → 右栏按「{TypeZh(id)}」过滤 → 点候选项填入此槽";
            string body;
            if (id == ZoneId.Keys)
            {
                body = KeywordZh(_card.Keywords[dataIndex]);
            }
            else
            {
                var fx = _card.Effects[dataIndex];
                var name = string.IsNullOrEmpty(fx?.DisplayName) ? $"效果 #{dataIndex + 1}" : fx.DisplayName;
                // des 只显渲染摘要单遍（2026-11-06 修复双写）：效果名=保存时渲染全文（效果合成器
                // 2026-10-03 定案 CurrentDisplayName=RenderEffectSummary）——名+摘要并列把同一效果
                // 写两遍，渲染文案改版后两遍句式还会分叉（「；若」旧名 vs「，如果」新析）。
                // 与卡面 FromCardDataFull/卡池行同源；空摘要（无步骤）回落名字。
                string prefix = id == ZoneId.Passives && IsAuraEffect(fx) ? "[光环] " : "";
                var summary = fx == null ? "" : AtomText.RenderEffectSummary(
                    new EffectGraphData(name) { header = fx, steps = fx.Steps }, _card);
                body = string.IsNullOrEmpty(summary) ? prefix + name : prefix + summary;
                // 卡级代价显示口已随 2026-11-06「代价全面并入效果级」定案退役——效果级 Costs 的
                // 「，【代价】…」由 RenderEffectSummary 拼进 summary；净费/代价读数在中栏 ACard 行级方格
            }
            return TypeEnabledForKind(id) ? body : "⚠与本卡类型不匹配（保存会被拦）——删除或换型\n" + body;
        }

        private string SlotRowDesc(ZoneId id, int dataIndex) => dataIndex < 0
            ? $"选中这个空{TypeZh(id)}槽：右栏自动按「{TypeZh(id)}」过滤候选，点选填入此槽"
            : id switch
            {
                ZoneId.Keys => "关键词槽：选中后右栏列出关键词目录（点选=替换）",
                _ => "效果槽：选中后右栏列出对应形态的效果（点选=整实例替换此槽）",
            };

        /// <summary>槽删除：已填=删数据（效果/关键词），空槽=回收占位。
        ///（代价槽已随 2026-11-08 代价效果级化整退役——代价随效果删除。）</summary>
        private void DeleteSlot(ZoneId id, int dataIndex)
        {
            if (dataIndex < 0)
            {
                _emptySlots[id] = Mathf.Max(0, EmptySlotCount(id) - 1);
                if (_selType == id && _selIndex < 0) DeselectSlot();
                else RefreshSlots();
                return;
            }
            switch (id)
            {
                case ZoneId.Effect:
                case ZoneId.Passives:
                    RemoveAttachedAt(dataIndex); // 内部已刷新+重算
                    return;
                default:
                    _card.Keywords.RemoveAt(dataIndex);
                    _card.ResetCache();
                    break;
            }
            if (_selType == id && _selIndex == dataIndex) DeselectSlot();
            else RefreshSlots();
            Recalculate();
        }

        /// <summary>填入成功后回收一个该类型空槽（选中空槽填入=显式消费；全量视图追加=兜底回收防空槽累计超容）。</summary>
        private void ConsumeEmptySlot(ZoneId id)
        {
            _emptySlots[id] = Mathf.Max(0, EmptySlotCount(id) - 1);
        }

        private void RemoveAttachedAt(int index)
        {
            if (index < 0 || index >= _card.Effects.Count) return;
            _card.Effects.RemoveAt(index);
            // 光环随效果回收：移除效果后按剩余效果重算卡面箭头+光环
            _card.AggregateEffectAuras(true);
            _card.ResetCache();
            if ((_selType == ZoneId.Effect || _selType == ZoneId.Passives) && _selIndex == index) DeselectSlot();
            else RefreshSlots();
            Recalculate();
        }

        // ======================================== 代价栏（已整退役 2026-11-08 代价效果级化） ========================================
        //（FillPayloadCost/CostEligibleRow/CostRowPreview/BuildLibraryPayloadView/_costParamValue
        //  随卡级代价整删——代价唯一编辑口=效果合成器有限分支「支付代价」门，数据=CardEffectData.Costs；
        //  代价行文案/抵扣读数随效果行渲染（EffectPayloadCostOf 行级代价组）。）

        /// <summary>代价全价合计（逐原子 PayloadUnitGrant 求和——门槛/补偿同锚）。</summary>
        private static int PayloadTotalGrant(List<CardCore.AtomicEffectEntry> atoms)
        {
            int total = 0;
            foreach (var e in atoms ?? new List<CardCore.AtomicEffectEntry>())
            {
                var inst = CardEffectConverter.ConvertPayloadForDisplay(e);
                if (inst != null) total += CostDerivationService.PayloadUnitGrant(inst);
            }
            return total;
        }

        // ======================================== 关键词（keys 槽；生物专属） ========================================

        // 关键词区随 2026-10-16 二次改版并入统一槽行渲染（tpl-kwchip chip 模板已随 prefab 消失）：
        // 关键词=keys 类型槽行（克隆 keys 模板），删除走槽行 delete，添加走 addkeys 新建空槽+右栏目录点选。

        /// <summary>右栏关键词候选行点击 → 填入选中关键词槽（选中已填槽=替换；空槽/未选=追加；
        /// 去重+容量闸；保存时以 effectIds 直接引用原子 refId）。</summary>
        private void FillKeyword(KeywordCatalogEntry kw)
        {
            if (kw == null || string.IsNullOrEmpty(kw.Id)) return;
            if (_kind != CardKind.Creature)
            {
                ShowToast("关键词为生物专属——法术/结界不可装关键词");
                return;
            }
            bool replacing = _selType == ZoneId.Keys && _selIndex >= 0 && _selIndex < _card.Keywords.Count;
            if (_card.Keywords.Where((k, i) => i != (_selType == ZoneId.Keys ? _selIndex : -1)).Contains(kw.Id))
            {
                ShowToast($"已有关键词：{kw.DisplayName}");
                return;
            }
            if (!replacing && _card.Keywords.Count + 1 > KeywordCapacity)
            {
                ShowToast($"关键词槽已满（{_card.Keywords.Count}/{KeywordCapacity}）——可关闭攻击/守卫资格腾出槽位");
                return;
            }
            if (replacing) _card.Keywords[_selIndex] = kw.Id;
            else _card.Keywords.Add(kw.Id);
            _card.ResetCache();
            ConsumeEmptySlot(ZoneId.Keys);
            RefreshSlots();
            Recalculate();
            ShowToast(replacing ? $"已替换关键词槽 {_selIndex + 1}：{kw.DisplayName}" : $"已添加：{kw.DisplayName}");
        }

        // ========================================（系统效果直加段已删 2026-11-05） ========================================

        // 充能直加行（AddChargeDirectRow/FillChargeDirect）已随 2026-11-05 充能系统动作化改版删除：
        // 充能不再是卡上效果——改为任意手牌卡可宣的速度0系统动作（引擎 GameActions.DeclareCharge，
        // 结算期反问元素类别），卡组合界面不再提供充能挂载。

        // ======================================== 右栏：效果库列表（zone 选中=按区过滤·回收式） ========================================

        private const float LibHeaderH = 28f;    // 表头行高
        private const float LibNewBtnH = 40f;    // 「新建效果」钮行高
        private const float LibRowH = 56f;       // 效果/候选行高=模板行（56，2026-10-27 对齐 DeckUI 行规格）

        /// <summary>效果库刷新（2026-10-16 二次改版）：选中槽 → 按槽类型过滤候选——cost=原子表一般效果行直引
        ///（2026-10-31 代价槽改版：单原子引用·色锁抵扣）/ effect=主动图 / passives=光环+自动图（结界分组）/
        /// keys=关键词目录；未选槽=全量效果图。代价与关键词是两个原子表直加口。
        /// 颜色多选+模糊串搜索两列表通用（类别 chips 已退役）。2026-10-21 虚拟化：装配条目+SetItems，
        /// 行结构建一次（LibRowFactory）、数据反复绑（BindLibRow）。</summary>
        private void RefreshLibrary()
        {
            if (_libRows == null) return;
            _libItems.Clear();
            if (_selType == null)
                BuildLibraryFullView();
            else
            {
                switch (_selType.Value)
                {
                    case ZoneId.Effect: BuildLibraryActiveView(); break;
                    case ZoneId.Passives: BuildLibraryPassivesView(); break;
                    case ZoneId.Keys: BuildLibraryKeywordsView(); break;
                }
            }
            string libSig = string.Join("|", _libItems.Select(x => x.Kind + ":" + x.Text + ":" + (x.Tag ?? "")));
            if (_libItems.Count > 0 && libSig == _libSig && _libRows.Count == _libItems.Count)
            {
                _libRows.RebindVisible(); // 数据未变：不回顶重铺（与卡池同口径）
                return;
            }
            _libSig = libSig;
            _libRows.SetItems(_libItems.Count, i => _libItems[i].H);
        }

        // ---- 条目装配（纯数据，不动 UI） ----

        private void LibHeader(string text) =>
            _libItems.Add(new LibRowData { Kind = 0, Text = text, H = LibHeaderH });

        private void LibHint(string text) =>
            _libItems.Add(new LibRowData
            {
                Kind = 1,
                Text = text,
                H = UiKit.MeasureTextHeight(text, _libraryList.Content.rect.width - 16f, RowMetaSize) + 8f,
            });

        private void LibNewBtn() =>
            _libItems.Add(new LibRowData { Kind = 2, H = LibNewBtnH });

        private void LibEffect(EffectGraphData graph, string summaryOverride = null, string tag = null)
        {
            var name = string.IsNullOrEmpty(graph.name) ? "(未命名)" : graph.name;
            var summary = summaryOverride ?? AtomText.RenderEffectSummary(graph);
            if (!RowPassFilters(name + "\n" + summary, graph.header?.AnchorCost)) return;
            _libItems.Add(new LibRowData
            {
                Kind = 3, Graph = graph, Text = name, SummaryOverride = summary, H = LibRowH,
                // v2 右侧标签：显式指定优先，否则按形态推导
                Tag = tag ?? (EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(graph)) ? "光环"
                    : (IsActiveGraph(graph) ? "主动" : "自动")),
            });
        }

        private void LibCandidate(string title, string summary, Action onClick, string tag = null)
        {
            if (!RowPassFilters(title + "\n" + (summary ?? ""), null)) return;
            _libItems.Add(new LibRowData
            {
                Kind = 4, Text = title, // v2：单行显示（summary 参与过滤；悬浮经 SummaryOverride 展示）
                ClickAction = onClick, Tag = tag, SummaryOverride = summary, H = LibRowH,
            });
        }

        // ---- 预设段（2026-11-06 定案）：预组卡组引用的组合效果并入效果库展示——可引用挂载、
        //      行内「编辑」跳效果合成器预设只读态（修改保存=另存新效果，源档不可覆盖不可删） ----

        /// <summary>预设卡组用到的组合效果集（同效果组合界面 AppendPresetEffectRows 口径）：
        /// DeckCatalog 预组 → 卡 → 效果 id 引用集 → 预设表装载；主表已含同 id（玩家另存过同内容
        /// 副本）不重复列（主表行为准）。引用不断链：ReferenceAudit.LibraryEffectIds 本就含预设表。</summary>
        private static List<EffectGraphData> PresetEffectGraphs()
        {
            var presetIds = new HashSet<string>();
            foreach (var deck in DeckCatalog.LoadPresets())
            {
                if (deck?.cardIds == null) continue;
                foreach (var cardId in deck.cardIds)
                {
                    var card = CardCatalog.GetById(cardId);
                    if (card?.Effects == null) continue;
                    foreach (var fx in card.Effects)
                        if (fx != null && !string.IsNullOrEmpty(fx.Id)) presetIds.Add(fx.Id);
                }
            }
            if (presetIds.Count == 0) return new List<EffectGraphData>();
            var mainIds = new HashSet<string>(EffectLibrarySerializer.LoadAll()
                .Where(g => !string.IsNullOrEmpty(g?.id)).Select(g => g.id));
            return EffectLibrarySerializer.LoadAll(true)
                .Where(g => g != null && !string.IsNullOrEmpty(g.id) && presetIds.Contains(g.id) && !mainIds.Contains(g.id))
                .ToList();
        }

        /// <summary>预设段效果行（与 LibEffect 同结构·Preset 标记）：Tag=形态+预设后缀；
        /// 行点击=引用填槽（FillSelectedSlot 走主段同一形状闸）。</summary>
        private void LibPresetEffect(EffectGraphData graph)
        {
            var name = string.IsNullOrEmpty(graph.name) ? "(未命名)" : graph.name;
            var summary = AtomText.RenderEffectSummary(graph);
            if (!RowPassFilters(name + "\n" + summary, graph.header?.AnchorCost)) return;
            _libItems.Add(new LibRowData
            {
                Kind = 3, Graph = graph, Text = name, SummaryOverride = summary, H = LibRowH, Preset = true,
                Tag = (EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(graph)) ? "光环"
                        : IsActiveGraph(graph) ? "主动" : "自动") + "·预设",
            });
        }

        /// <summary>预设段落行（分节头+效果行）：shape=当前视图形态过滤（null=全量浏览）；
        /// partitionByHost=true 时与主段同口径宿主分区（可挂行照列+隐藏计数行——P9 候选不静默隐藏，
        /// 缓冲先清防吃主段残留）。</summary>
        private void AppendPresetEffects(Func<EffectGraphData, bool> shape, string sectionZh, bool partitionByHost)
        {
            var graphs = PresetEffectGraphs().Where(g => shape == null || shape(g)).ToList();
            List<EffectGraphData> eligible;
            if (partitionByHost)
            {
                _hiddenPending.Clear(); // 预设段独立分区——不吃主段残留缓冲
                PartitionByHost(graphs, HostOf(_kind), out eligible);
            }
            else eligible = graphs;
            if (eligible.Count == 0 && (!partitionByHost || _hiddenPending.Count == 0)) return;
            LibHeader($"{sectionZh}（预设 {eligible.Count}）——预组卡组效果·可引用挂载；编辑=另存新效果（源档只读）");
            if (eligible.Count == 0) LibHint("预设段没有该形态的效果");
            foreach (var g in eligible) LibPresetEffect(g);
            if (partitionByHost) LibHiddenSection();
        }

        private void BuildLibraryFullView()
        {
            LibNewBtn();
            var graphs = EffectLibrarySerializer.LoadAll();
            LibHeader($"效果库（{graphs.Count}）——点击=填入中间选中槽；「编辑」=改库");
            if (graphs.Count == 0)
                LibHint("效果库为空——「新建效果」或在效果合成器保存即入库；下方列出预设卡组使用的组合效果");
            foreach (var graph in graphs)
                LibEffect(graph);
            // 预设段（2026-11-06 定案）：预组卡组效果只读并入——全量浏览不做宿主分区（挂载闸在点选时）
            AppendPresetEffects(null, "预设卡组效果", partitionByHost: false);
        }

        //（BuildLibraryPayloadView 已随 2026-11-08 代价效果级化整删——代价槽退役，
        //  代价原子候选/编辑=效果合成器「支付代价」门（CostGateCandidate 单源）。）

        private void BuildLibraryActiveView()
        {
            LibNewBtn();
            //（充能直加段已随 2026-11-05 充能系统动作化改版删除——充能不再是卡上效果。）
            // P9（2026-10-29）：宿主不匹配候选不静默隐藏——分区（可挂行照列 + 隐藏计数行）
            var host = HostOf(_kind);
            var graphs = EffectLibrarySerializer.LoadAll()
                .Where(g => !EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(g)) && IsActiveGraph(g))
                .ToList();
            PartitionByHost(graphs, host, out var eligible);
            graphs = eligible;
            LibHeader(_kind switch
            {
                CardKind.Spell => $"主动效果（{graphs.Count}）——点击=填入主动槽（1~3·施放时择一发动）",
                CardKind.Enchantment => $"主动效果（{graphs.Count}）——结界禁主动（充能已是系统动作，任意手牌卡可宣）",
                _ => $"主动效果（{graphs.Count}）——点击=填入主动槽（生物×1）",
            });
            if (graphs.Count == 0)
            {
                LibHint("库中没有主动形态的效果——「新建效果」在效果合成器里做主动效果");
            }
            foreach (var graph in graphs)
                LibEffect(graph);
            LibHiddenSection();
            // 预设段：主动形态（非光环）——与主段同口径宿主分区
            AppendPresetEffects(
                g => !EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(g)) && IsActiveGraph(g),
                "预设·主动效果", partitionByHost: true);
        }

        private void BuildLibraryPassivesView()
        {
            LibNewBtn();
            var graphs = EffectLibrarySerializer.LoadAll();
            bool Aura(EffectGraphData g) => EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(g));
            // P9（2026-10-29）：宿主不匹配候选不静默隐藏——自动/光环段各自分区（可挂行照列 + 隐藏计数行）
            var host = HostOf(_kind);
            if (_kind == CardKind.Enchantment)
            {
                var auras = graphs.Where(Aura).ToList();
                var autos = graphs.Where(g => !Aura(g) && !IsActiveGraph(g)).ToList();
                PartitionByHost(auras, host, out var aurasOk);
                PartitionByHost(autos, host, out var autosOk);
                LibHeader($"光环（{aurasOk.Count}）——必填·恰好 1（规则或连接）");
                if (aurasOk.Count == 0) LibHint("库中没有光环形态的效果——「新建效果」在效果合成器里做光环");
                foreach (var g in aurasOk) LibEffect(g);
                LibHiddenSection();
                AppendPresetEffects(Aura, "预设·光环", partitionByHost: true);
                LibHeader($"自动效果（{autosOk.Count}）——至多 2（每槽占 1 点免费预算，盈余转入场耐久）");
                if (autosOk.Count == 0) LibHint("库中没有自动/触发形态的效果");
                foreach (var g in autosOk) LibEffect(g);
                LibHiddenSection();
                AppendPresetEffects(g => !Aura(g) && !IsActiveGraph(g), "预设·自动效果", partitionByHost: true);
            }
            else
            {
                var autos = graphs.Where(g => !Aura(g) && !IsActiveGraph(g)).ToList();
                PartitionByHost(autos, host, out var autosOk);
                LibHeader($"自动效果（{autosOk.Count}）——点击=填入自动槽（生物×1）");
                if (autosOk.Count == 0) LibHint("库中没有自动/触发形态的效果");
                foreach (var g in autosOk) LibEffect(g);
                LibHiddenSection();
                AppendPresetEffects(g => !Aura(g) && !IsActiveGraph(g), "预设·自动效果", partitionByHost: true);
            }
        }

        // ---- P9 隐藏区（宿主不匹配候选显形） ----

        /// <summary>待显形分区缓冲（PartitionByHost 填、LibHiddenSection 消费后清空——多段视图间不串）。</summary>
        private readonly List<(EffectGraphData graph, string reason)> _hiddenPending = new List<(EffectGraphData, string)>();

        /// <summary>按宿主分区：可挂的经 eligible 出、不可挂的进隐藏缓冲（原因=EffectEligibility 单源）。</summary>
        private void PartitionByHost(List<EffectGraphData> graphs, Cardtype host, out List<EffectGraphData> eligible)
        {
            eligible = new List<EffectGraphData>(graphs.Count);
            foreach (var g in graphs)
            {
                var reason = HostRejectReason(host, g);
                if (reason == null) eligible.Add(g);
                else _hiddenPending.Add((g, reason));
            }
        }

        private string KindZh() => KindDefs.First(k => k.kind == _kind).zh;

        /// <summary>隐藏区落行：缓冲非空 → 常驻计数行「有 N 个效果因宿主不匹配被隐藏 ▸」；
        /// 展开后逐行「效果名（原因）＋去修改」（跳效果合成器改库——复用库效果编辑入口）。
        /// 行内容不参与搜索/色筛（信息行非候选）。</summary>
        private void LibHiddenSection()
        {
            if (_hiddenPending.Count == 0) return;
            int n = _hiddenPending.Count;
            var snapshot = _hiddenPending.ToList();
            _hiddenPending.Clear();
            bool expanded = _hiddenExpanded;
            _libItems.Add(new LibRowData
            {
                Kind = 5,
                Text = expanded ? $"▾ 收起（{n} 个效果因{KindZh()}不匹配被隐藏）" : $"有 {n} 个效果因宿主不匹配被隐藏 ▸",
                ClickAction = () => { _hiddenExpanded = !expanded; RefreshLibrary(); },
                H = LibRowH,
            });
            if (!expanded) return;
            foreach (var (graph, reason) in snapshot)
            {
                var captured = graph;
                var why = reason;
                _libItems.Add(new LibRowData
                {
                    Kind = 6,
                    Text = string.IsNullOrEmpty(captured.name) ? "(未命名)" : captured.name,
                    Reason = why,
                    Graph = captured,
                    ClickAction = () => OnEditLibraryEffect(captured),
                    H = LibRowH,
                });
            }
        }

        private void BuildLibraryKeywordsView()
        {
            var catalog = KeywordCatalog.LoadAll();
            LibHeader($"关键词目录（{catalog.Count}）——点击=填入关键词槽（生物专属·至多 {KeywordCapacity}）");
            if (catalog.Count == 0)
            {
                LibHint("关键词目录为空——检查原子表 Grant 族行");
                return;
            }
            foreach (var kw in catalog)
            {
                var captured = kw;
                LibCandidate(captured.DisplayName, "", () => FillKeyword(captured), "关键词");
            }
        }

        // ---- 行工厂/绑定（结构建一次，数据反复绑） ----

        /// <summary>效果库行工厂路由（kind 分池：0 表头 / 1 提示 / 2 新建钮 / 3 效果行 / 4 候选行 /
        /// 5 隐藏计数行 / 6 隐藏明细行——P9 宿主不匹配显形）。</summary>
        private UiKit.RecycledList.MakeFn LibRowFactory(int kind)
        {
            switch (kind)
            {
                case 0: return MakeLibHeaderRow;
                case 1: return MakeLibHintRow;
                case 2: return MakeLibNewBtnRow;
                case 4: return MakeLibCandidateRow;
                case 5: return MakeLibHiddenToggleRow;
                case 6: return MakeLibHiddenItemRow;
                default: return MakeLibEffectRowStruct;
            }
        }

        private RectTransform MakeLibHeaderRow()
        {
            var rt = UiKit.Node("hdr", _libraryList.Content);
            var lbl = UiKit.Label("lbl", rt, "", UiStyle.HeaderSize, UiStyle.TextSecondary,
                TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.StretchInset(lbl.rectTransform, 4f, 2f);
            return rt;
        }

        private RectTransform MakeLibHintRow()
        {
            var rt = UiKit.Node("hint", _libraryList.Content);
            var lbl = UiKit.Label("lbl", rt, "", RowMetaSize, UiStyle.TextHint, wrap: true);
            UiKit.StretchInset(lbl.rectTransform, 4f, 4f);
            return rt;
        }

        /// <summary>「新建效果」钮行结构：满宽主按钮（监听 bind 时挂；25 号字·DeckUI 按钮档）。</summary>
        private RectTransform MakeLibNewBtnRow()
        {
            var rt = UiKit.Node("newbtn", _libraryList.Content);
            var btn = UiKit.Button("btn", rt, "＋ 新建效果（挂到当前卡）", null, null, null, null, LibNewBtnH, BtnTextSize);
            UiKit.Stretch((RectTransform)btn.transform);
            return rt;
        }

        /// <summary>效果行结构：list-library 模板克隆 + 行内「编辑」按钮（锚右居中；64×40·25 号字，DeckUI 行内钮规格）。</summary>
        private RectTransform MakeLibEffectRowStruct()
        {
            var row = CloneRow(_libraryList);
            if (row == null) return UiKit.Node("row-miss", _libraryList.Content);
            var edit = UiKit.Button("edit", row, "编辑", null, null, null, 64f, 40f, BtnTextSize);
            var ert = (RectTransform)edit.transform;
            ert.anchorMin = ert.anchorMax = ert.pivot = new Vector2(1f, 0.5f);
            ert.anchoredPosition = new Vector2(-8f, 0f);
            return row;
        }

        /// <summary>候选行结构：模板克隆（无编辑钮——关键词/代价原子等非效果图数据源）。</summary>
        private RectTransform MakeLibCandidateRow()
        {
            var row = CloneRow(_libraryList);
            return row != null ? row : UiKit.Node("row-miss", _libraryList.Content);
        }

        /// <summary>P9 隐藏计数行结构：满宽次级按钮（展开/收起切换）。</summary>
        private RectTransform MakeLibHiddenToggleRow()
        {
            var rt = UiKit.Node("hidden-toggle", _libraryList.Content);
            var btn = UiKit.Button("btn", rt, "", null, null, null, null, LibRowH, BtnTextSize);
            UiKit.Stretch((RectTransform)btn.transform);
            return rt;
        }

        /// <summary>P9 隐藏明细行结构：模板克隆（name=效果名 / meta=原因）＋行内「去修改」钮
        ///（规格同效果行编辑钮——64×40·25 号字）。</summary>
        private RectTransform MakeLibHiddenItemRow()
        {
            var row = CloneRow(_libraryList);
            if (row == null) return UiKit.Node("row-miss", _libraryList.Content);
            var edit = UiKit.Button("fix", row, "去修改", null, null, null, 84f, 40f, BtnTextSize);
            var ert = (RectTransform)edit.transform;
            ert.anchorMin = ert.anchorMax = ert.pivot = new Vector2(1f, 0.5f);
            ert.anchoredPosition = new Vector2(-8f, 0f);
            return row;
        }

        /// <summary>效果库绑定：kind 分派（表头/提示/新建钮/效果行/候选行）。</summary>
        private void BindLibRow(RectTransform row, int index)
        {
            var item = _libItems[index];
            switch (item.Kind)
            {
                case 0:
                case 1:
                {
                    var lbl = row.GetComponentInChildren<TMP_Text>(true);
                    if (lbl != null) lbl.text = item.Text;
                    return;
                }
                case 2:
                {
                    var btn = row.GetComponentInChildren<UnityEngine.UI.Button>(true);
                    if (btn == null) return;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        // 选中已填效果槽=替换；空槽/未选/非效果槽=-1 追加（保存时挂回）
                        int idx = (_selType == ZoneId.Effect || _selType == ZoneId.Passives) && _selIndex >= 0 ? _selIndex : -1;
                        ComposerSession.BeginCardEdit(_card, idx);
                        Manager.Show<EffectComposerScreen>();
                    });
                    Describe(btn, "新建效果：跳转效果组合界面为当前卡编一条新效果，保存后自动挂回本卡（选中槽=替换该槽）");
                    return;
                }
                case 4:
                {
                    var title = item.Text.Split('\n')[0];
                    var nameT = UiKit.FindDeep(row, "name")?.GetComponent<TMP_Text>();
                    if (nameT != null)
                    {
                        nameT.text = title;
                        nameT.color = new Color(0.88f, 0.9f, 0.93f);
                    }
                    var metaT = UiKit.FindDeep(row, "meta")?.GetComponent<TMP_Text>();
                    if (metaT != null) metaT.text = item.Tag ?? "";
                    BindCostBadge(row, null); // 候选行无费用数据——徽标隐藏（池行复用还原）
                    var btn = row.GetComponent<UnityEngine.UI.Button>();
                    var act = item.ClickAction;
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => { Debug.Log("[UI点击] 候选行 " + title); act?.Invoke(); });
                    }
                    Describe(row, $"点击=填入当前选中区/槽：{title}"
                        + (string.IsNullOrEmpty(item.SummaryOverride) ? "" : "\n" + item.SummaryOverride));
                    return;
                }
                case 5:
                {
                    // P9 隐藏计数行：满宽钮=展开/收起切换
                    var btn = row.GetComponentInChildren<UnityEngine.UI.Button>(true);
                    if (btn == null) return;
                    var lbl = btn.GetComponentInChildren<TMP_Text>(true);
                    if (lbl != null) lbl.text = item.Text;
                    var act = item.ClickAction;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => { Debug.Log("[UI点击] " + item.Text); act?.Invoke(); });
                    Describe(row, "展开/收起因宿主卡型不匹配而被隐藏的效果列表（附原因与修改入口）");
                    return;
                }
                case 6:
                {
                    // P9 隐藏明细行：name=效果名 / meta=原因；行点击与「去修改」同=跳效果合成器改库
                    var nameT = UiKit.FindDeep(row, "name")?.GetComponent<TMP_Text>();
                    if (nameT != null)
                    {
                        nameT.text = item.Text;
                        nameT.color = new Color(0.62f, 0.66f, 0.7f); // 置灰系——区别可挂候选
                    }
                    var metaT = UiKit.FindDeep(row, "meta")?.GetComponent<TMP_Text>();
                    if (metaT != null) metaT.text = item.Reason ?? "";
                    BindCostBadge(row, null);
                    var act = item.ClickAction;
                    var itemRowBtn = row.GetComponent<UnityEngine.UI.Button>();
                    if (itemRowBtn != null)
                    {
                        itemRowBtn.onClick.RemoveAllListeners();
                        itemRowBtn.onClick.AddListener(() => { Debug.Log("[UI点击] 隐藏明细行（去修改）" + item.Text); act?.Invoke(); });
                    }
                    var fixBtn = UiKit.FindDeep(row, "fix")?.GetComponent<UnityEngine.UI.Button>();
                    if (fixBtn != null)
                    {
                        fixBtn.onClick.RemoveAllListeners();
                        fixBtn.onClick.AddListener(() => { Debug.Log("[UI点击] 去修改：" + item.Text); act?.Invoke(); });
                    }
                    Describe(row, $"因「{item.Reason}」不能挂到{KindZh()}——去修改=跳效果合成器改这条库效果（改时机/形态后可挂）");
                    return;
                }
            }

            // ---- 效果行（v2：f14 名称+右侧 f12 标签+费标；摘要走悬浮） ----
            var graph = item.Graph;
            var name = item.Preset ? "［预设］" + item.Text : item.Text; // 预设段前缀（同卡池预设卡行口径）
            var summary = item.SummaryOverride ?? "";
            var name2 = UiKit.FindDeep(row, "name")?.GetComponent<TMP_Text>();
            if (name2 != null)
            {
                name2.text = name;
                name2.color = new Color(0.88f, 0.9f, 0.93f);
            }
            var meta2 = UiKit.FindDeep(row, "meta")?.GetComponent<TMP_Text>();
            if (meta2 != null) meta2.text = item.Tag ?? "";
            BindCostBadge(row, graph?.header?.AnchorCost);
            var rowBtn = row.GetComponent<UnityEngine.UI.Button>();
            if (rowBtn != null)
            {
                rowBtn.onClick.RemoveAllListeners();
                rowBtn.onClick.AddListener(() =>
                {
                    Debug.Log("[UI点击] 效果库行 " + name);
                    FillSelectedSlot(graph);
                });
            }
            Describe(row, item.Preset
                ? $"点击=引用「{item.Text}」填入中间选中槽位（预设效果只读）；「编辑」=载入效果合成器——"
                  + "修改保存会另存为新效果（与预设同内容拦存），原预设不可改删"
                : $"点击=把「{name}」填入中间选中槽位（未选槽=追加到末尾）"
                  + (string.IsNullOrEmpty(summary) ? "" : "\n" + summary));
            var editBtn = UiKit.FindDeep(row, "edit")?.GetComponent<UnityEngine.UI.Button>();
            if (editBtn != null)
            {
                editBtn.gameObject.SetActive(true); // 池行复用显式还原（预设编辑走另存通道——钮恒在）
                editBtn.onClick.RemoveAllListeners();
                editBtn.onClick.AddListener(() => OnEditLibraryEffect(graph));
                if (item.Preset)
                    Describe(editBtn, "编辑预设效果：载入效果合成器预设只读态——改动保存=另存新效果，源档不变");
            }
        }

        /// <summary>右栏行统一过滤器（颜色多选 + 模糊串搜索，2026-10-16 类别 chips 退役后两列表通用）。
        /// 无费用数据的行（关键词/代价原子按全价文本展示）在选中颜色时不显示。</summary>
        private bool RowPassFilters(string corpus, ElementCost cost)
        {
            var tokens = _searchField != null ? TokenizeSearch(_searchField.text) : new List<string>();
            if (tokens.Count > 0 && !MatchCorpus(corpus ?? "", tokens)) return false;
            if (_colorFilterSet.Count > 0 && !CostColorsWithin(cost, _colorFilterSet)) return false;
            return true;
        }

        /// <summary>全局编辑库效果（2026-10-13 定案）：跳效果合成器改 Effects.json，引用它的所有卡随之更新。
        /// 当前卡已挂载快照若因改库换代（内容哈希换 id），返回时孤儿检测提示重挂。</summary>
        private void OnEditLibraryEffect(EffectGraphData graph)
        {
            if (string.IsNullOrEmpty(graph?.id))
            {
                ShowToast("该效果尚未入库（无 id）——先在效果合成器保存");
                return;
            }
            ComposerSession.BeginLibraryEffectEdit(graph.id);
            Manager.Show<EffectComposerScreen>();
            UnityEngine.Debug.Log($"[CardComposer] 库效果编辑跳转：{graph.id} {graph.name}");
        }

        /// <summary>挂载快照孤儿检测：快照 id 非空且库中已无此 id → 该库效果已被编辑换代或删除——提示重挂。</summary>
        private void WarnStaleAttachedEffects()
        {
            if (_card.Effects == null || _card.Effects.Count == 0) return;
            var libIds = ReferenceAudit.LibraryEffectIds();
            foreach (var fx in _card.Effects)
            {
                if (fx != null && !string.IsNullOrEmpty(fx.Id) && !libIds.Contains(fx.Id))
                {
                    ShowToast($"效果「{fx.DisplayName}」的库版本已更新/移除——移除后重新挂载以取新版");
                    return;
                }
            }
        }

        /// <summary>右栏效果列表点选 → 填入对应槽（2026-10-16 二次改版：按选中槽类型路由）：
        /// 选中已填效果槽=整实例替换；选中空槽/未选=追加。形状闸（2026-10-15 深夜改版）：
        /// 生物/法术禁挂连接光环；法术只收主动、结界禁主动。</summary>
        private void FillSelectedSlot(EffectGraphData graph)
        {
            // 光环形态闸：连接光环=结界专属（生物禁挂改版；法术只挂主动效果）
            if ((_kind == CardKind.Creature || _kind == CardKind.Spell)
                && EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(graph)))
            {
                ShowToast(_kind == CardKind.Creature
                    ? "生物不可挂连接光环（2026-10-15 改版——光环专属结界）"
                    : "法术只挂主动效果（施放时择一发动），不可挂光环");
                return;
            }
            // 时机形态闸：法术只收主动（Active/Instant）；结界禁主动
            //（P12「结界主动只收充能」已随 2026-11-05 充能系统动作化改版收回——充能不再是卡上效果。）
            int t = graph?.header?.TriggerTiming ?? -1;
            bool activated = t == (int)TriggerTiming.Activate_Active || t == (int)TriggerTiming.Activate_Instant
                             || t == (int)TriggerTiming.Activate_Response;
            if (_kind == CardKind.Enchantment && activated)
            {
                ShowToast("结界禁主动效果（只挂自动/光环——充能已是系统动作，任意手牌卡可宣）");
                return;
            }
            if (_kind == CardKind.Spell && !activated)
            {
                ShowToast("法术只挂主动效果（施放时择一发动）——自动/触发/响应式不收");
                return;
            }

            // 槽类型路由：选中效果槽按槽型对表（主动槽只收主动图；自动槽不收主动图）；
            // 未选/非效果槽（右栏视图本就按槽过滤，走不到）按形状归类
            bool activeGraph = IsActiveEffect(graph?.header);
            bool auraGraph = EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(graph));
            bool slotTargeted = _selType == ZoneId.Effect || _selType == ZoneId.Passives;
            var target = slotTargeted ? _selType.Value : (activeGraph ? ZoneId.Effect : ZoneId.Passives);
            if (target == ZoneId.Effect && !activeGraph)
            {
                ShowToast("主动槽只收主动效果——请选中自动槽后再挂自动/光环形态");
                return;
            }
            if (target == ZoneId.Passives && activeGraph)
            {
                ShowToast("自动槽不收主动效果——请选中主动槽后再挂主动形态");
                return;
            }

            // 容量闸（按型槽上限——替换已选槽放行，追加须有空位）
            bool replacing = slotTargeted && _selIndex >= 0 && _selIndex < _card.Effects.Count;
            if (!replacing)
            {
                if (auraGraph)
                {
                    if (AuraEffectCount >= AuraSlotCapacity)
                    {
                        ShowToast("光环槽 1/1 已满——先删除现有光环，或选中该槽后替换");
                        return;
                    }
                }
                else if (activeGraph)
                {
                    if (ActiveEffectCount >= ActiveSlotCapacity)
                    {
                        ShowToast($"主动槽已满（{ActiveEffectCount}/{ActiveSlotCapacity}）——先删除现有效果，或选中槽位后替换");
                        return;
                    }
                }
                else if (AutoEffectCount >= AutoSlotCapacity)
                {
                    ShowToast($"自动槽已满（{AutoEffectCount}/{AutoSlotCapacity}）——先删除现有效果，或选中槽位后替换");
                    return;
                }
            }
            var snap = SnapshotEffect(graph);
            if (replacing)
            {
                _card.Effects[_selIndex] = snap;
                ShowToast($"已替换槽位 {_selIndex + 1}：{snap.DisplayName ?? "效果"}");
            }
            else
            {
                _card.Effects.Add(snap);
                ConsumeEmptySlot(target); // 选中空槽填入=显式消费；全量视图追加=兜底回收
                ShowToast($"已追加效果：{snap.DisplayName ?? "效果"}（选中槽位后点选可替换）");
            }
            // 光环上移效果层：挂载即并集入卡面（多光环取并集）——重算式
            _card.AggregateEffectAuras(true);
            _card.ResetCache();
            RefreshSlots();
            Recalculate();
        }

        // 效果图 → 卡内嵌效果（编排字段全量拷贝；branch 载荷随步骤原子走）。
        private static CardEffectData SnapshotEffect(EffectGraphData graph)
        {
            var src = graph.header ?? new CardEffectData();
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
                ArrowDirections = src.ArrowDirections, // 箭头随效果快照
                LinkAuras = src.LinkAuras != null && src.LinkAuras.Count > 0
                    ? new List<LinkAuraData>(src.LinkAuras) : null,
                ActivationConditions = src.ActivationConditions,
                TriggerConditions = src.TriggerConditions,
                Costs = src.Costs,
                Tags = src.Tags,
                Steps = graph.steps != null ? new List<EffectStepData>(graph.steps) : null,
                AtomicEffects = ProjectLinear(graph.steps),
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

        // ======================================== 右栏视图切换 ========================================

        private void ShowRightView(bool cards)
        {
            _rightShowCards = cards;
            if (_libraryList?.Rect != null) _libraryList.Rect.gameObject.SetActive(!cards);
            if (_poolList?.Rect != null) _poolList.Rect.gameObject.SetActive(cards);
            if (_filterRow != null) _filterRow.gameObject.SetActive(true); // 颜色+搜索两列表通用（2026-10-16 类别 chips 退役）
            if (_btnSwitchTable != null)
            {
                var t = _btnSwitchTable.GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.text = cards ? "切换效果库" : "切换卡池"; // 按钮文案=去向
            }
        }

        // ======================================== 左栏：立绘画廊（回收式） ========================================

        // 2026-10-13 立绘随卡类型定案：生物=YGOMonstersRig 模型名（卡面立绘+战场 3D 同名双源）；
        // 2026-10-15 法术/结界=无合适立绘（缺省空白不放图）——画廊无选项。
        // 2026-10-21 虚拟化：贴片结构建一次（MakeArtTile 工厂），数据/高亮反复绑（BindArtTile）——
        // 换类型才重铺，载入/新建卡只重绑可见格（滚动位置不再被重置）。
        private CardKind? _galleryForKind;

        private void BuildArtGallery(bool force = false)
        {
            if (_galleryList == null) return;
            if (!force && _galleryForKind == _kind) { _galleryList.RebindVisible(); return; } // 同类型：仅对账高亮
            _galleryForKind = _kind;
            _artNames = new List<string>();
            if (_kind == CardKind.Creature)
            {
                foreach (var n in CreatureArtNames())
                    if (string.IsNullOrEmpty(_galleryQuery) || n.IndexOf(_galleryQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        _artNames.Add(n);
            }
            else
            {
                // 2026-11-06 默认图定案：法术/结界各一张默认图 Title（Assets/Art/Icons/Title.png——
                // ACard.prefab 同图已烘为两类型块的立绘占位，未选时卡面回落同一张图）
                if (string.IsNullOrEmpty(_galleryQuery) || "Title".IndexOf(_galleryQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                    _artNames.Add("Title");
            }
            // 2026-11-06 去占位定案：占位项（清空立绘）退役——画廊只列真实可选图；
            // 未选立绘（null）=无高亮格，卡面回落烘焙占位图
            if (_galleryCount != null)
                _galleryCount.text = _artNames.Count + " 张";
            _galleryList.SetGrid(_artNames.Count); // 格几何（184×184·间8·2 列）由 Attach 从 GLG 吸收（2026-10-27 左栏 400 两列化）
        }

        /// <summary>生物立绘可选名（=模型名，与战场 MonsterModelResolver 同源：StreamingAssets bundle
        /// 内嵌 #names 名单，全平台同一条路径）。打包侧只收「prefab+预览图齐全」的目录——名单即全部可选。
        /// 旧编辑器直扫 YGOMonstersRig 目录版已随 2026-10-17 StreamingAssets 化退役（#if 空体正是
        /// CS0161 根因：非编辑器编译下方法体无 yield 无 return）。</summary>
        private static IEnumerable<string> CreatureArtNames()
        {
            return Tide.HotUpdate.MonsterRigAssets.Names();
        }

        /// <summary>贴片结构（v2 工厂，只建一次）：184×184 满格=Rounded6 底板+统一方形缩略（preserveAspect）
        /// +底部半透明名称条（f12 覆盖在缩略下部）+按钮+金框选中描边。内部无布局组（GLG 控格）。</summary>
        private RectTransform MakeArtTile()
        {
            var tile = UiKit.Node("tile", _galleryContent);
            var bg = tile.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.RoundedSprite; // 程序生成圆角（同 UiKit.Button 口径，无 bundle 依赖）
            bg.color = new Color(0.16f, 0.18f, 0.22f, 1f);
            bg.raycastTarget = true;

            var thumb = UiKit.Node("thumb", tile);
            var img = thumb.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            UiKit.Stretch(thumb); // 184×184 满格（GLG 定格）

            var labelBg = UiKit.Node("label-bg", thumb).gameObject.AddComponent<Image>();
            labelBg.color = new Color(0f, 0f, 0f, 0.55f);
            labelBg.raycastTarget = false;
            var lbRt = labelBg.rectTransform;
            lbRt.anchorMin = new Vector2(0f, 0f);
            lbRt.anchorMax = new Vector2(1f, 0f);
            lbRt.pivot = new Vector2(0.5f, 0f);
            lbRt.sizeDelta = new Vector2(0f, 20f);

            var label = UiKit.Label("label", thumb, "", UiStyle.SmallSize, UiStyle.TextBody, TextAnchor.MiddleLeft);
            var lRt = label.rectTransform;
            lRt.anchorMin = new Vector2(0f, 0f);
            lRt.anchorMax = new Vector2(1f, 0f);
            lRt.pivot = new Vector2(0.5f, 0f);
            lRt.sizeDelta = new Vector2(-8f, 20f);
            lRt.anchoredPosition = new Vector2(0f, 1f);
            label.overflowMode = TextOverflowModes.Ellipsis;

            var btn = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            var ol = tile.gameObject.AddComponent<Outline>(); // 选中金框（bind 对账开关）
            ol.effectColor = Gold;
            ol.effectDistance = new Vector2(2f, -2f);
            ol.enabled = false;
            return tile;
        }

        /// <summary>图库选中金框（v2）。</summary>
        private static readonly Color Gold = new Color(0.91f, 0.75f, 0.35f);

        /// <summary>贴片绑定：立绘图/名称/选中描边/点击监听（原 HighlightArtTile 高亮并入此处）。
        /// 2026-11-06 去占位：数据源无 null 项——占位分支退役，全部条目都是真实可选图。</summary>
        private void BindArtTile(RectTransform tile, int index)
        {
            var spriteName = _artNames[index];

            var thumb = tile.Find("thumb") as RectTransform;
            var img = thumb != null ? thumb.GetComponent<Image>() : null;
            if (img != null)
            {
                var sp = CardArtLibrary.Load(spriteName); // 按名称两级回落（内部带缓存）
                img.sprite = sp; // 池行复用须显式清旧图
                img.preserveAspect = true;
                img.color = sp != null ? Color.white : new Color(0f, 0f, 0f, 0.25f);
            }
            var label = thumb != null ? thumb.Find("label")?.GetComponent<TMP_Text>() : null;
            if (label != null) label.text = spriteName;

            var ol = tile.GetComponent<Outline>();
            if (ol != null) ol.enabled = (_card.Illustration ?? "") == spriteName; // 高亮=与当前立绘对账

            var btn = tile.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) return;
            var captured = spriteName;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Debug.Log("[UI点击] 立绘 " + captured);
                _card.Illustration = captured;
                Recalculate(); // 卡面立绘随 RefreshCardFace 对账
                _galleryList.RebindVisible(); // 画廊高亮同帧对账（原 HighlightArtTile 全量重描）
                ShowToast("立绘已选：" + captured);
            });
            Describe(tile, $"选用立绘 {captured}——卡面立绘与战场 3D 模型同名双源");
        }

        // ======================================== 自动算费（终值直写） ========================================

        /// <summary>卡费文字公式（2026-10-16 二次改版：type 区方格退役，参考效果组合界面费用公式）：
        /// 构成项逐段「S/K/A/E 标签＝值」＋L3 挂载折扣×f ＝ D 合计（建议分布）＋黑白获得。
        /// 完整逐行明细见 type 区悬浮 desc-strip。</summary>
        private static string CardCostFormulaText(CardCostCalculator.Result r)
        {
            if (r == null) return "费用构成";
            var parts = new List<string>();
            float factor = 1f;
            foreach (var l in r.Breakdown)
            {
                if (string.IsNullOrEmpty(l.Label) || l.Label[0] != '[') continue;
                int close = l.Label.IndexOf(']');
                if (close <= 1) continue;
                var stage = l.Label.Substring(1, close - 1);
                if (stage == "f")
                {
                    if (l.Value > 0f) factor = l.Value;
                    continue;
                }
                if (stage != "S" && stage != "K" && stage != "A" && stage != "E") continue;
                if (Mathf.Approximately(l.Value, 0f)) continue; // 主动效果「构筑期不计锚价」等 0 值行不列
                parts.Add(FormulaPartLabel(stage, l.Label) + "＝" + l.Value.ToString("0.##"));
            }
            var text = "费用：" + (parts.Count > 0 ? string.Join("＋", parts) : "0")
                + (factor < 1f ? "×f" + factor.ToString("0.###") : "");
            if (r.PayloadPrice > 0)
            {
                // P4 §1.2 + 2026-10-31 色锁定案：三段式显净费（构成合计=抵扣前推导费；实抵 N=min(全价,可产出色容量)，
                // 只作用于代价原子可产出色——明细行「[P] 代价抵扣 −N（限X·至多清零该色）」在费用区悬浮构成里）。
                text += "＝D" + ((int)r.PrePayloadCost.Total).ToString("0") + "（" + CostZh(r.PrePayloadCost) + "）"
                        + $" − 代价抵扣 {r.PayloadApplied}（限{r.PayloadScopeZh}）"
                        + " ＝ 净费 D" + r.Total.ToString("0") + "（" + CostZh(r.SuggestedCost) + "）";
            }
            else
                text += "＝D" + r.Total.ToString("0") + "（" + CostZh(r.SuggestedCost) + "）";
            var grants = r.Grants.Where(kv => kv.Value != 0)
                .Select(kv => ManaTypeNames.ZhNameOf((ManaType)kv.Key) + Mathf.Abs(kv.Value).ToString("0.#"))
                .ToList();
            if (grants.Count > 0) text += "｜获得 " + string.Join("", grants);
            return text;
        }

        /// <summary>构成项短标签：S 保留身材/耐久变量（「S(攻2/血3)」）；K/A/E 去尾部括注（「K关键词 Guard」）。</summary>
        private static string FormulaPartLabel(string stage, string label)
        {
            var body = label.Substring(label.IndexOf(']') + 1).Trim(); // 去 "[X] " 前缀
            if (stage == "S")
                return "S" + body.Replace("身材费 ", "").Replace("耐久费 ", "");
            int cut = body.IndexOf('(');
            int cut2 = body.IndexOf('（');
            if (cut2 >= 0 && (cut < 0 || cut2 < cut)) cut = cut2;
            if (cut > 0) body = body.Substring(0, cut).Trim();
            return stage + body;
        }

        /// <summary>费用文本化（效果组合界面 CostZh 同口径）：非零色按序拼「N色」；全零=0。</summary>
        private static string CostZh(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            var parts = new List<string>();
            foreach (var color in cost.NonzeroColors())
                parts.Add(cost[color].ToString("0.##") + ManaTypeNames.ZhNameOf(color));
            return string.Join("", parts);
        }

        private void Recalculate()
        {
            ApplyStatFloors(); // 2026-11-03 底盘改版：结构变动后免费属性底托底（先托底再算费）
            var result = CardCostCalculator.Calculate(_card);

            // 自动算费直写（2026-10-16 定案：手动声明/采纳/建议标签全退役）——费用恒=内容推导分布。
            //（2026-10-22 恒推导定案后 CostDict=SuggestedCost=DerivedCost，无声明回显分支可绕。）
            var suggested = result.SuggestedCost;
            bool same = _card.Cost != null && suggested != null && _card.Cost.v.SequenceEqual(suggested.v);
            if (!same)
            {
                _card.Cost = suggested != null && !suggested.IsZero ? suggested.Clone() : null;
                _card.ResetCache();
            }

            RefreshCardFace(result);
        }

        // ======================================== 保存（覆盖替换） ========================================

        private void OnSave()
        {
            ApplyTextLists();
            EnsureCardLists();

            // 减费三选一保存闸（2026-11-02 定案·单源=EffectComposerScreen.DiscountConflictOf）：
            // ① 同一效果只能三选一（有限分支/自由分支/代价）；② 并列两效果禁代价；
            // ③ 光环类（结界的场地光环/仪典行）禁代价；④ 代价行全价 ≤3（2026-11-03 收回——当日放宽 4 作废）。
            string discountConflict = EffectComposerScreen.DiscountConflictOf(_card);
            if (discountConflict != null)
            {
                ShowToast(discountConflict);
                return;
            }

            // 形状拦截（2026-10-15 深夜三类卡改版；2026-11-05 充能系统动作化修订）：
            // 法术=主动 1~3（总效果数 ≤4）；结界=主动全拒（充能不再是卡上效果，P12 修订收回）。
            if (_kind == CardKind.Spell)
            {
                int count = _card.Effects?.Count ?? 0;
                int activeCount = 0;
                foreach (var e in _card.Effects ?? new List<CardEffectData>())
                {
                    int t = e?.TriggerTiming ?? -1;
                    if (t != (int)TriggerTiming.Activate_Active && t != (int)TriggerTiming.Activate_Instant)
                    {
                        ShowToast($"法术效果「{e.DisplayName ?? e.Id}」须为主动效果（法术只挂主动，自动/触发/响应式不收）");
                        return;
                    }
                    activeCount++;
                }
                if (count < 1 || count > 4 || activeCount < 1 || activeCount > 3)
                {
                    ShowToast($"法术须挂 1~3 个主动效果（现 {activeCount}——施放时择一发动）");
                    return;
                }
            }
            else if (_kind == CardKind.Enchantment)
            {
                foreach (var e in _card.Effects ?? new List<CardEffectData>())
                {
                    int t = e?.TriggerTiming ?? -1;
                    if (t == (int)TriggerTiming.Activate_Active || t == (int)TriggerTiming.Activate_Instant
                        || t == (int)TriggerTiming.Activate_Response)
                    {
                        ShowToast($"结界效果「{e.DisplayName ?? e.Id}」为主动效果——结界只挂自动/光环（充能已是系统动作）");
                        return;
                    }
                }
            }

            // 连接光环归属闸（2026-10-15 改版）：聚合后卡面光环只准结界携带
            if (_kind != CardKind.Enchantment && _card.LinkAuras != null && _card.LinkAuras.Count > 0)
            {
                ShowToast(_kind == CardKind.Creature
                    ? "生物不可挂连接光环（2026-10-15 改版）——移除光环后保存"
                    : "法术只挂主动效果——移除光环后保存");
                return;
            }

            // 槽位容量闸（2026-10-16 槽位化定案：生物 主动1/自动1/关键词3+攻守开关；结界 光环恰好1/自动2）
            if (_kind == CardKind.Creature)
            {
                if (ActiveEffectCount > 1)
                {
                    ShowToast($"生物主动槽 0~1（现 {ActiveEffectCount} 个）——删除多余主动效果后保存");
                    return;
                }
                if (AutoEffectCount > 1)
                {
                    ShowToast($"生物自动槽 0~1（现 {AutoEffectCount} 个）——删除多余自动效果后保存");
                    return;
                }
                if (string.IsNullOrWhiteSpace(_attackInput?.text) || string.IsNullOrWhiteSpace(_lifeInput?.text))
                {
                    ShowToast("生物必填项：攻击/生命（留空=未填）");
                    return;
                }
                if ((_card.Power ?? 0) > MaxCreatureStat || (_card.Life ?? 0) > MaxCreatureStat)
                {
                    ShowToast($"生物基础属性上限 {MaxCreatureStat}——攻/血至多 {MaxCreatureStat}，调整后保存");
                    return;
                }
                // 免费属性底闸（2026-11-03 底盘分配改版）：兜底拦「载入旧卡低于新底未触碰」的直存
                //（编辑触碰即被输入钳制/托底收口）；底=FreeAttrBaseOf（3−攻守−首槽），n/n 固定不可偏移。
                int statFloor = CardCompositionCost.FreeAttrBaseOf(_card);
                if ((_card.Power ?? 0) < statFloor || (_card.Life ?? 0) < statFloor)
                {
                    ShowToast($"生物属性下限=免费属性底 {statFloor}（攻/血各至少 {statFloor}——免费底 n/n 固定获得），调整后保存");
                    return;
                }
            }
            else if (_kind == CardKind.Enchantment)
            {
                if (AuraEffectCount == 0)
                {
                    ShowToast("结界必须恰好 1 个光环效果（规则或连接）——点被动区选中后从右栏挂载");
                    return;
                }
                if (AuraEffectCount > 1)
                {
                    ShowToast($"结界光环槽恰好 1（现 {AuraEffectCount} 个）——删除多余光环后保存");
                    return;
                }
                if (AutoEffectCount > 2)
                {
                    ShowToast($"结界自动槽 0~2（现 {AutoEffectCount} 个）——删除多余自动效果后保存");
                    return;
                }
            }
            if (_kind != CardKind.Creature && _card.Keywords != null && _card.Keywords.Count > 0)
            {
                ShowToast("关键词为生物专属——清空关键词或切换为生物后保存");
                return;
            }

            // 参数列 CardRef 选择闸（2026-10-31 阶段C）：变形/模拟/召唤衍生物等引用卡原子未选卡
            // → 拦截保存——与装载期 ValidateAtomParams 同口径（引擎主干行带 then 载荷豁免：
            // 附加诅咒/附加祝福的载荷通道=branch.then，不收 str）。
            string missing = CardRefMissingMessage();
            if (missing != null)
            {
                ShowToast(missing);
                return;
            }

            // 名称缺省（2026-10-16 槽位化）：留空=用卡 ID 当卡名。
            // 2026-11-05 ID 冻结：编辑既有卡沿用其 id（名字不再参与身份）；新建/另存才按内容指纹分配。
            bool nameFromId = string.IsNullOrWhiteSpace(_card.CardName);
            string savedId = string.IsNullOrEmpty(_card.ID)
                ? "C_" + ContentHasher.HashCard(_card)
                : _card.ID;
            if (nameFromId) _card.CardName = savedId;
            bool replacing = !_editingPresetSource // 预设卡恒另存（源档=教学数据，覆盖替换概念不适用）
                             && !string.IsNullOrEmpty(_editingOriginalId) && _editingOriginalId != savedId;

            // 覆盖替换前置检查（2026-09-24 定案）：旧卡被卡组引用则拦截（删除会使卡组引用悬空）
            if (replacing)
            {
                var refs = ReferenceAudit.DecksReferencingCard(_editingOriginalId);
                if (refs.Count > 0)
                {
                    ShowToast($"旧卡仍被卡组引用（{string.Join("、", refs)}）——先到卡组构建中替换该卡再保存");
                    return;
                }
            }

            var path = CardConfigSerializer.Save(_card, null, replacing ? _editingOriginalId : null);
            if (path == null)
            {
                ShowToast("保存失败");
                return;
            }
            // savedId 已在保存前算定（= ToEntry 落盘 id：既有卡沿用原 id，新卡=内容指纹）
            CardCatalog.Invalidate();
            CorpusCache.Clear();
            TutorialCreationFlow.NotifyCardsChanged(); // 第三课走查步检测（未开课零行为）

            ShowToast(replacing
                ? $"已覆盖替换：{_card.CardName}（{_editingOriginalId} → {savedId}）"
                : $"已保存到卡表：{System.IO.Path.GetFileName(path)}（{savedId}）");

            OnNewCard();
            RefreshLibrary();
        }

        private void ApplyTextLists()
        {
            _card.CardName = string.IsNullOrWhiteSpace(_nameInput?.text) ? null : _nameInput.text.Trim(); // null=保存时缺省成 ID
            // 标签输入已退役（2026-10-16 prefab 重做）：_card.Tags 载入/保存透传，不再从输入框覆写
        }

        /// <summary>CardRef 未选检查（2026-10-31 阶段C 保存闸）：枚举全效果原子（与装载期
        /// ValidateAtomParams 同一嵌套枚举），参数列=CardRef 且 str 为空 → 返回拦截文案；
        /// null=全齐可存。引擎主干行带 then 载荷豁免（附加诅咒/附加祝福——载荷通道=branch.then）。</summary>
        private string CardRefMissingMessage()
        {
            foreach (var eff in _card.Effects ?? new List<CardEffectData>())
            {
                if (eff == null) continue;
                foreach (var atom in CardLoader.EnumerateAtomEntries(eff))
                {
                    var row = AtomicEffectTable.GetByHashId(atom?.refId);
                    if (row == null || row.Param != AtomicParamKind.CardRef) continue;
                    if (ComposerCatalog.IsEngineTrunkRow(row)
                        && atom.branch?.then != null && atom.branch.then.Count > 0)
                        continue; // 引擎主干行：载荷走 branch.then（同装载期豁免）
                    if (string.IsNullOrEmpty(atom.str))
                        return $"效果「{eff.DisplayName ?? eff.Id}」的原子「{row.DisplayName}」未选择模板卡"
                               + "——在卡面衍生物槽（或进效果组合点该原子的「选择卡牌」）选卡后再保存";
                }
            }
            return null;
        }

        /// <summary>操作反馈（2026-10-16 toast 退役定案）：Console 必落，desc-strip 尽力显示
        /// （悬浮事件随后自然覆盖——两机制共存）。</summary>
        private void ShowToast(string message)
        {
            Debug.Log("[CardComposer] " + message);
            SetDescStrip(message);
        }

        // ======================================== 列表行模板克隆 ========================================

        /// <summary>克隆列表自带 inactive row 模板（Image+Button+CostSquares+meta）——
        /// tpl-root 体系已随 2026-10-16 prefab 重做消失，行视觉以两列表模板为单一来源。</summary>
        private RectTransform CloneRow(UiKit.Scroll list)
        {
            if (list?.Content == null) return null;
            // 模板已被虚拟化接管搬去 tpl-park（content 只装活行）——从停靠点克隆
            var clone = UiKit.CloneTemplateFrom(UiKit.RecycledList.TemplatePark(list), "row", list.Content);
            if (clone == null)
                Debug.LogError("[CardComposer] 缺行模板 row（tpl-park/content 直属 inactive row）——该行跳过");
            return clone;
        }

        /// <summary>只清本屏运行时行（rt- 前缀）——prefab 静态子（标题/方格/des）不动。</summary>
        private static void ClearRuntimeChildren(RectTransform zone)
        {
            if (zone == null) return;
            for (int i = zone.childCount - 1; i >= 0; i--)
            {
                var child = zone.GetChild(i);
                if (!child.name.StartsWith("rt-", StringComparison.Ordinal)) continue;
                child.SetParent(null);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}
