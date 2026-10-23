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
    /// · basicability 区（具备攻击/具备守卫 Toggle）反相绑 NoAttack/NoGuard；
    /// · 名称可缺省——留空保存=用生成的卡 ID 当卡名；生物属性必填（输入框留空=未填拦保存）；
    /// · type 区 ID 钮：仅在保存过卡后出现（num 显卡 ID，点击复制——方便衍生物填 ID）；
    /// · type 区 "CostSquares " 文本节点＝卡费文字公式（方格退役，口径参考效果组合界面费用公式）；
    /// · 右栏类别 chips 退役——颜色多选+模糊串搜索两列表通用。
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

        private readonly CardData _card = new CardData
        {
            CardName = null, // 名称可缺省（2026-10-16 槽位化）：留空保存=用生成的卡 ID 当卡名
            Supertype = Cardtype.Creature,
            Power = 0,
            Life = 0,
        };

        /// <summary>覆盖替换（2026-09-24 定案）：正在编辑的旧卡 ID；null=新建。效果合成器往返期间保留。</summary>
        private string _editingOriginalId;

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

        // ---- 中栏卡面（2026-10-16 prefab 二次重做：卡面即编辑器 + 单槽模板制） ----
        private RectTransform _cardFace;
        private RectTransform _typeZone, _attributeZone, _costZone, _effectZone, _passivesZone, _keysZone;
        private Image _typeBg, _maskImg, _artImg;
        private TMP_Text _typeCostText;   // type 区 "CostSquares " 文本节点：卡费文字公式（方格已随 prefab 重做退役）
        private UInputField _nameInput, _attackInput, _lifeInput, _durabilityInput;
        private readonly Dictionary<CardKind, UnityEngine.UI.Button> _kindButtons = new Dictionary<CardKind, UnityEngine.UI.Button>();

        // ---- ID 钮（type 区）：仅在保存过卡后出现——num 显卡 ID，点击复制（方便衍生物那边填 ID） ----
        private UnityEngine.UI.Button _idBtn;
        private TMP_Text _idNum;
        /// <summary>最近一次保存/载入编辑的卡 ID（null=本会话尚未保存过——ID 钮隐藏）。
        /// OnNewCard 不清（保存流程尾部即新建，ID 钮要跨复位存活）；删除卡时清。</summary>
        private string _lastSavedId;

        // ---- 左栏画廊（2026-10-21 回收式虚拟化：~103 贴片只保留可见格，滚动/换类型零重建） ----
        private RectTransform _galleryContent;
        private UiKit.RecycledList _galleryList;
        private List<string> _artNames; // 立绘名数据源（首项 null=占位；非生物=空表）

        // ---- 右栏两列表（同日虚拟化：行池复用，筛选/搜索只重绑可见行） ----
        private UiKit.RecycledList _poolRows;   // list-effects=卡池
        private UiKit.RecycledList _libRows;    // list-library=效果库
        private readonly List<PoolRowData> _poolItems = new List<PoolRowData>();
        private readonly List<LibRowData> _libItems = new List<LibRowData>();

        /// <summary>卡池条目（卡行/提示/清筛选钮——kind 决定行结构分池）。</summary>
        private sealed class PoolRowData
        {
            public int Kind;               // 1=卡行 2=提示 3=清筛选钮
            public string Text;
            public CardData Card;
            public float H;
        }

        /// <summary>效果库条目（表头/提示/新建钮/效果行/候选行）。</summary>
        private sealed class LibRowData
        {
            public int Kind;               // 0=表头 1=提示 2=新建钮 3=效果行 4=候选行
            public string Text;
            public EffectGraphData Graph;
            public Action<EffectGraphData> Click;
            public string SummaryOverride;
            public Action ClickAction;
            public float H;
        }

        // 卡池颜色过滤 chips（预制体烘焙，Build 一次绑定；激活态=Outline 开关）。
        private readonly Dictionary<UIColor, UnityEngine.UI.Button> _filterChips = new Dictionary<UIColor, UnityEngine.UI.Button>();

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
        private int? _costParamValue; // 代价槽 generate 参数（2026-10-21 单参数定案）：随键记录，换选原子时沿用；载入/新建/删除代价时清

        // ---- basicability 区（prefab 手加 ac/dc 两 Toggle「具备攻击/具备守卫」→ NoAttack/NoGuard 反相） ----
        private RectTransform _basicAbilityZone;
        private Toggle _attackToggle, _guardToggle;

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
            RefreshSlots();
            Recalculate();
            SyncIdButton();
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
                    CardKind.Spell => "法术：挂 1~3 个主动效果（施放时弹窗抉择其一），速度=3−槽数动态算",
                    _ => "结界：耐久体，只挂自动效果与连接光环（免费点全投耐久）",
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
                var ol = kv.Value.GetComponent<Outline>();
                if (on && ol == null)
                {
                    ol = kv.Value.gameObject.AddComponent<Outline>();
                    ol.effectColor = UiStyle.TextPrimary;
                    ol.effectDistance = new Vector2(1.5f, -1.5f);
                }
                if (ol != null) ol.enabled = on;
            }
        }

        // ======================================== 中栏：卡面即编辑器 ========================================

        private void BindMiddleCard()
        {
            _cardFace = Find("ACard");
            if (_cardFace == null) return; // Find 已 LogError；各引用保持 null 空转不炸

            _typeZone = UiKit.FindDeep(_cardFace, "type");
            _typeBg = FindImageUnder(_typeZone, "bg");
            _maskImg = FindImageUnder(_typeZone, "mask");
            _artImg = FindImageUnder(_typeZone, "art");
            // 费用公式文本节点（prefab 由 CostSquares 方格改 TMP，节点名带尾空格 "CostSquares "）——
            // 按名前缀找防尾空格丢失；开启自动换行（公式长于区宽）
            if (_typeZone != null)
            {
                for (int i = 0; i < _typeZone.childCount; i++)
                {
                    var c = _typeZone.GetChild(i);
                    if (!c.name.StartsWith("CostSquares", StringComparison.Ordinal)) continue;
                    _typeCostText = c.GetComponent<TMP_Text>();
                    if (_typeCostText != null)
                    {
                        _typeCostText.enableWordWrapping = true;
                        break;
                    }
                }
            }
            BindIdButton();

            _nameInput = FindInputUnder(_cardFace, "name");
            if (_nameInput != null)
                _nameInput.onValueChanged.AddListener(v => { _card.CardName = v; });

            _attributeZone = UiKit.FindDeep(_cardFace, "attribute");
            _attackInput = BindIntInput(_attributeZone, "attack",
                v => { _card.Power = v; Recalculate(); }, "生物攻击力——计入自动算费");
            _lifeInput = BindIntInput(_attributeZone, "life",
                v => { _card.Life = v; Recalculate(); }, "生物生命值——计入自动算费");
            _durabilityInput = BindIntInput(_attributeZone, "durability",
                v => { _card.Durability = v; Recalculate(); }, "结界耐久——免费点全投耐久，装/卸效果时即时重算");

            _costZone = UiKit.FindDeep(_cardFace, "cost");
            _effectZone = UiKit.FindDeep(_cardFace, "effect");
            _passivesZone = UiKit.FindDeep(_cardFace, "passives");
            _keysZone = UiKit.FindDeep(_cardFace, "keys");

            // 四区节点=单槽模板（2026-10-16 二次改版）：prefab 恒 inactive，槽行=运行时克隆；
            // 捕获主 Outline 原色（选中行变白/取消还原），登记模板供克隆
            RegisterSlotTemplate(ZoneId.Cost, _costZone);
            RegisterSlotTemplate(ZoneId.Effect, _effectZone);
            RegisterSlotTemplate(ZoneId.Passives, _passivesZone);
            RegisterSlotTemplate(ZoneId.Keys, _keysZone);
            BindBasicAbility();
            BindAddSlotButtons();
        }

        /// <summary>登记槽模板并捕获主 Outline 原色（缺节点 LogError 空转不炸）。</summary>
        private void RegisterSlotTemplate(ZoneId id, RectTransform zone)
        {
            if (zone == null) return;
            _slotTemplates[id] = zone;
            var ol = zone.GetComponent<Outline>();
            if (ol != null) _slotOutlineBase[id] = (ol.effectColor, ol.enabled);
        }

        // ---- ID 钮（type 区）：保存过卡才出现；num=卡 ID；点击复制到剪贴板 ----

        private void BindIdButton()
        {
            var idRt = _typeZone != null ? UiKit.FindDeep(_typeZone, "ID") : null;
            _idBtn = idRt?.GetComponent<UnityEngine.UI.Button>();
            _idNum = idRt != null ? UiKit.FindDeep(idRt, "num")?.GetComponent<TMP_Text>() : null;
            if (_idBtn == null)
            {
                Debug.LogError("[CardComposer] 缺 type/ID 按钮或其 num 文本（CardUI.prefab）——ID 复制跳过");
                return;
            }
            // num/Text 同 rect 全拉伸会重叠：num 左侧左对齐显 ID，Text 右侧右对齐当提示语
            if (_idNum != null)
            {
                var nrt = _idNum.rectTransform;
                nrt.anchorMin = new Vector2(0f, 0f);
                nrt.anchorMax = new Vector2(0.62f, 1f);
                nrt.offsetMin = new Vector2(10f, 0f);
                nrt.offsetMax = Vector2.zero;
                _idNum.fontSize = 24f;
                _idNum.alignment = TextAlignmentOptions.MidlineLeft;
            }
            var caption = UiKit.FindDeep(idRt, "Text")?.GetComponent<TMP_Text>();
            if (caption != null)
            {
                var crt = caption.rectTransform;
                crt.anchorMin = new Vector2(0.62f, 0f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.offsetMin = Vector2.zero;
                crt.offsetMax = new Vector2(-10f, 0f);
                caption.fontSize = 20f;
                caption.alignment = TextAlignmentOptions.MidlineRight;
            }
            _idBtn.onClick.RemoveAllListeners();
            _idBtn.onClick.AddListener(() =>
            {
                if (string.IsNullOrEmpty(_lastSavedId)) return;
                Debug.Log("[UI点击] 复制卡ID " + _lastSavedId);
                GUIUtility.systemCopyBuffer = _lastSavedId;
                ShowToast("已复制卡ID：" + _lastSavedId);
            });
            Describe(_idBtn, "点击复制当前保存卡的 ID（C_ 前缀内容哈希）——作衍生物/效果引用时粘贴此 ID");
        }

        /// <summary>ID 钮显隐+num 回显（保存成功/载入旧卡后出现；新建未保存不显示）。</summary>
        private void SyncIdButton()
        {
            if (_idBtn == null) return;
            _idBtn.gameObject.SetActive(!string.IsNullOrEmpty(_lastSavedId));
            if (_idNum != null && !string.IsNullOrEmpty(_lastSavedId)) _idNum.text = _lastSavedId;
        }

        // ---- add-slot 四钮：新建空槽（容量按型闸；新建即选中→右栏按槽类型过滤） ----

        private void BindAddSlotButtons()
        {
            BindAddSlot(ZoneId.Cost, "addcost", "添加代价",
                "新建一个空代价槽（三类卡通用·至多 1）：右栏列出效果库中可作代价的组装图（纯线性·逐原子可逆转）·逐原子全价·生效后补偿黑/白");
            BindAddSlot(ZoneId.Effect, "addeffect", "添加主动效果",
                "新建空主动槽（生物×1 / 法术×1~3·施放时抉择其一·速度加成=3−槽数 / 结界无）");
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

        /// <summary>add 四钮显隐/可用随卡型与容量（容量 0 整钮隐藏；满槽禁用）。</summary>
        private void SyncAddSlotButtons()
        {
            foreach (var kv in _addButtons)
            {
                int cap = SlotCapacity(kv.Key);
                kv.Value.gameObject.SetActive(cap > 0);
                kv.Value.interactable = FilledSlotCount(kv.Key) + EmptySlotCount(kv.Key) < cap;
            }
        }

        private static Image FindImageUnder(RectTransform scope, string name) =>
            scope != null ? UiKit.FindDeep(scope, name)?.GetComponent<Image>() : null;

        private static UInputField FindInputUnder(RectTransform scope, string name) =>
            scope != null ? UiKit.FindDeep(scope, name)?.GetComponentInChildren<UInputField>(true) : null;

        private UInputField BindIntInput(RectTransform scope, string node, Action<int> onChanged, string desc)
        {
            var field = FindInputUnder(scope, node);
            if (field == null) return null;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.onValueChanged.AddListener(s => { if (int.TryParse(s, out var v)) onChanged(v); });
            Describe(field, desc);
            return field;
        }

        private void SyncNameInput()
        {
            if (_nameInput != null) _nameInput.SetTextWithoutNotify(_card.CardName ?? "");
        }

        /// <summary>属性输入框显隐+回写（生物=攻/血、结界=耐久、法术=attribute 整区隐藏）。
        /// 攻/血文本回显收窄到 SetAttrTexts（留空=未填，保存闸口径）；耐久恒回显数字。</summary>
        private void SyncAttributeInputs()
        {
            if (_attributeZone != null)
                _attributeZone.gameObject.SetActive(HasStats || HasDurability);
            if (_attackInput != null)
                _attackInput.gameObject.SetActive(HasStats);
            if (_lifeInput != null)
                _lifeInput.gameObject.SetActive(HasStats);
            if (_durabilityInput != null)
            {
                _durabilityInput.gameObject.SetActive(HasDurability);
                _durabilityInput.SetTextWithoutNotify(_card.Durability.ToString());
            }
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
        /// 结界 passives=光环1+自动2 合一（光环恰好 1 由保存闸拦截——填入时不强分）。</summary>
        private int SlotCapacity(ZoneId id) => id switch
        {
            ZoneId.Cost => 1, // 三类卡通用·可选×1
            ZoneId.Effect => ActiveSlotCapacity,
            ZoneId.Passives => _kind == CardKind.Enchantment ? AuraSlotCapacity + AutoSlotCapacity : AutoSlotCapacity,
            _ => KeywordCapacity,
        };

        /// <summary>该类型已填槽数（数据侧计数；空槽另计 EmptySlotCount）。</summary>
        private int FilledSlotCount(ZoneId id) => id switch
        {
            ZoneId.Cost => _card.PayloadCost != null && _card.PayloadCost.AllPayloads().Count > 0 ? 1 : 0,
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
            ZoneId.Effect => _kind != CardKind.Enchantment,
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

        /// <summary>卡型可见性（2026-10-16 二次改版）：四区模板恒 inactive（槽行运行时克隆）；
        /// 此处只管 add 四钮（容量 0 隐藏）与 basicability（生物专属）；选中槽失效→取消。</summary>
        private void SyncZoneVisibility()
        {
            if (_basicAbilityZone != null)
                _basicAbilityZone.gameObject.SetActive(_kind == CardKind.Creature);
            SyncAddSlotButtons();
            if (_selType != null && SlotCapacity(_selType.Value) == 0) DeselectSlot();
        }

        // ---- basicability（具备攻击/具备守卫 → NoAttack/NoGuard 反相；关一个腾 1 个关键词槽） ----

        private void BindBasicAbility()
        {
            _basicAbilityZone = UiKit.FindDeep(_cardFace, "basicability");
            _attackToggle = BindAbilityToggle("ac", "具备攻击",
                on => _card.NoAttack = !on, "具备攻击能力（basicability）：关掉可腾出 1 个关键词槽（释放的免费点只减身材灰费）");
            _guardToggle = BindAbilityToggle("dc", "具备守卫",
                on => _card.NoGuard = !on, "具备守卫能力（basicability）：关掉可腾出 1 个关键词槽（释放的免费点只减身材灰费）");
            SyncBasicAbility();
        }

        private Toggle BindAbilityToggle(string node, string label, Action<bool> apply, string desc)
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
                RefreshSlots(); // 关键词槽容量随攻/守开关变化（add 按钮态同步）
                Recalculate();
                ShowToast(on ? $"已具备：{label}" : $"已关闭：{label}——关键词槽 +1（现上限 {KeywordCapacity}）");
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

        private void SyncBasicAbility()
        {
            if (_basicAbilityZone != null)
                _basicAbilityZone.gameObject.SetActive(_kind == CardKind.Creature);
            if (_attackToggle != null) _attackToggle.SetIsOnWithoutNotify(!_card.NoAttack);
            if (_guardToggle != null) _guardToggle.SetIsOnWithoutNotify(!_card.NoGuard);
        }

        // ---- 容量与效果分型（单源：保存闸/槽行/右栏过滤共用） ----

        private int ActiveSlotCapacity => _kind == CardKind.Spell ? 3 : (_kind == CardKind.Creature ? 1 : 0);
        private int AutoSlotCapacity => _kind == CardKind.Enchantment ? 2 : (_kind == CardKind.Creature ? 1 : 0);
        private int AuraSlotCapacity => _kind == CardKind.Enchantment ? 1 : 0;

        /// <summary>关键词上限（生物专属）：3 + 每关一个攻/守 +1（最高 5）。</summary>
        private int KeywordCapacity => _kind == CardKind.Creature
            ? 3 + (_card.NoAttack ? 1 : 0) + (_card.NoGuard ? 1 : 0)
            : 0;

        /// <summary>光环形态判据（与效果合成器 InferMode 同源）：连接/范围光环都走光环形态效果图（IsAuraMode）。</summary>
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

        // ---- type 区视觉（ACardView.TypeAssets 同源：生物=方/法术=三角/结界=圆） ----

        private static readonly Dictionary<Cardtype, (string bg, string mask)> TypeAssets = new()
        {
            { Cardtype.Creature, ("biology", "mask_square") },
            { Cardtype.Spell, ("spell", "mask_triangle") },
            { Cardtype.Enchantment, ("barrier", "mask_circle") },
        };
        private static readonly Dictionary<string, Sprite> SpriteCache = new();
        private static string SpritePath(string name) => $"Assets/Art/Battle3D/Icons/{name}.png";

        private void SetTypeVisuals(Cardtype type)
        {
            if (!TypeAssets.TryGetValue(type, out var assets))
                assets = ("biology", "mask_square");
            var bg = LoadSprite(assets.bg);
            if (_typeBg != null && bg != null) _typeBg.sprite = bg;
            var mask = LoadSprite(assets.mask);
            if (_maskImg != null && mask != null) _maskImg.sprite = mask;
        }

        private static Sprite LoadSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (SpriteCache.TryGetValue(name, out var s)) return s;
            s = Tide.HotUpdate.HotUpdateAssets.Load<Sprite>(name, SpritePath(name));
            SpriteCache[name] = s; // 加载失败也缓存 null，避免逐次重试
            return s;
        }

        // ---- 卡面刷新（Recalculate 统一收口） ----

        private void RefreshCardFace(CardCostCalculator.Result result)
        {
            SetTypeVisuals(_card.Supertype);

            // 立绘：生物=画廊选中（两级找图同 ACardView）；法术/结界缺省空白（清 sprite 防卡实例串位）
            if (_artImg != null)
            {
                if (_kind == CardKind.Creature && !string.IsNullOrEmpty(_card.Illustration))
                {
                    var sp = CardArtLibrary.Load(_card.Illustration);
                    if (sp != null) _artImg.sprite = sp;
                }
                else if (_artImg.sprite != null) _artImg.sprite = null;
            }

            // type 区 "CostSquares " 文本节点=卡费文字公式（2026-10-16 二次改版：方格退役，
            // 口式参考效果组合界面费用公式——逐项「标签＝值」＋折扣＋合计/分布）
            if (_typeCostText != null)
                _typeCostText.text = CardCostFormulaText(result);

            // 悬浮费用构成（完整逐行明细；文本公式是它的紧凑投影）
            if (_typeZone != null && result != null)
            {
                var lines = new List<string>();
                foreach (var l in result.Breakdown) lines.Add($"{l.Label} {l.Value:0.0}");
                if (result.Grants.Count > 0)
                    lines.Add("获得 " + string.Join(" ", result.Grants.Select(kv => $"{(ManaType)kv.Key} {(int)kv.Value}")));
                lines.Add($"总档位 D={result.Total:0}");
                Describe(_typeZone, "卡牌费用（自动算费，手动声明已退役）——构成：\n" + string.Join("\n", lines));
            }
        }

        // ======================================== 左栏：卡池筛选 ========================================

        /// <summary>过滤 chips 绑定（颜色行预制体烘焙 chip-{Color}；缺 chip 容错跳过——2026-10-16 prefab 无 chip-Gray）。
        /// 多选定案：再点同色移除；「全部」=清空。</summary>
        private void BindPoolFilter()
        {
            if (_poolFilter == null) return;
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

        // ChipColors/Rgb（关键词 chip 配色）随 tpl-kwchip 退役删除——关键词槽行走统一模板视觉。

        private const float PoolRowH = 60f;   // 卡行高=模板行（VLG ctrlH=False 原口径）
        private const float PoolBtnH = 36f;   // 空态按钮行高
        private HashSet<string> _poolLibIds;  // 本轮刷新的库效果 id 集（断链标记，bind 时消费）
        private Color _poolRowBgDefault = Color.white; // 卡行模板底色（选中态还原基准，工厂首次捕获）

        private void RefreshPool()
        {
            if (_poolRows == null) return;
            var pool = CardCatalog.LoadPlayPool(); // 教学专用卡/衍生物不进编辑器卡池预览
            _poolLibIds = ReferenceAudit.LibraryEffectIds(); // 卡池行⚠断链标记（删库效果联动）

            IEnumerable<CardData> cards = pool;
            if (_colorFilterSet.Count > 0)
                cards = cards.Where(c => CostColorsWithin(c, _colorFilterSet));
            var tokens = _searchField != null ? TokenizeSearch(_searchField.text) : new List<string>();
            if (tokens.Count > 0)
                cards = cards.Where(c => MatchCorpus(CorpusOf(c), tokens));

            var sorted = CardSorter.Sort(cards).ToList();
            if (tokens.Count > 0)
                sorted = sorted.OrderByDescending(c => NameHitsAny(c, tokens)).ToList(); // 搜索态名称命中置前

            _poolItems.Clear();
            if (sorted.Count == 0)
                AddPoolEmptyItems(pool.Count == 0, tokens);
            else
            {
                // 排序仍按组分（生物前/法术后·2026-09-24 口径）；分组标题 UI 已删（2026-10-21）
                foreach (var c in sorted.Where(c => c.Supertype == Cardtype.Creature))
                    _poolItems.Add(new PoolRowData { Kind = 1, Card = c, H = PoolRowH });
                foreach (var c in sorted.Where(c => c.Supertype != Cardtype.Creature))
                    _poolItems.Add(new PoolRowData { Kind = 1, Card = c, H = PoolRowH });
            }
            _poolRows.SetItems(_poolItems.Count, i => _poolItems[i].H);
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

        /// <summary>提示行结构（工厂）：12 号·暗色·自动换行。</summary>
        private RectTransform MakePoolHintRow()
        {
            var rt = UiKit.Node("hint", _poolList.Content);
            var lbl = UiKit.Label("lbl", rt, "", UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
            UiKit.StretchInset(lbl.rectTransform, 4f, 4f);
            return rt;
        }

        /// <summary>清筛选钮行结构（工厂）：左置迷你按钮（清颜色+搜索词，空态用）。</summary>
        private RectTransform MakePoolClearRow()
        {
            var rt = UiKit.Node("clr", _poolList.Content);
            var btn = UiKit.MiniButton("btn", rt, "清除全部", null);
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

            // ---- 卡行（原 MakePoolRow 全量口径） ----
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

            var text = (broken ? "⚠效果断链 " : "")
                + (string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName) + "  " + TypeStatLine(card);
            if (detailParts.Count > 0) text += "\n" + string.Join("｜", detailParts);
            var meta = row.GetComponentInChildren<TMP_Text>(true);
            if (meta != null) meta.text = text;

            var csv = row.GetComponentInChildren<CostSquaresView>(true);
            if (csv != null)
            {
                if (card.Cost != null && !card.Cost.IsZero) csv.SetCosts(card.Cost);
                else csv.Clear();
            }

            var img = row.GetComponent<Image>();
            if (img != null) img.color = selected ? UiStyle.SelectedRowBg : _poolRowBgDefault; // 池行复用须显式还原
            var btn2 = row.GetComponent<UnityEngine.UI.Button>();
            if (btn2 != null)
            {
                btn2.onClick.RemoveAllListeners();
                btn2.onClick.AddListener(() => LoadCardForEdit(card));
            }
            Describe(row, "点击载入该卡到中间编辑（保存=覆盖替换旧卡）"
                + (broken ? "——⚠该卡有效果引用断链（库效果已删/换代），装载时该效果会被跳过" : ""));
        }

        private static string TypeStatLine(CardData card)
        {
            switch (card.Supertype)
            {
                case Cardtype.Creature:
                    return $"生物 {(card.Power ?? 0)}/{(card.Life ?? 0)}";
                case Cardtype.Enchantment:
                    return $"结界 耐久{CardCompositionCost.TotalDurabilityOf(card)}"; // 2026-10-17：恒有耐久（派生单源）
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
            { UIColor.Gray, UIColor.Red, UIColor.Blue, UIColor.Green, UIColor.White, UIColor.Black };

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
                case UIColor.White: return ManaType.White;
                case UIColor.Black: return ManaType.Black;
                default: return ManaType.Gray;
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
                    H = UiKit.MeasureTextHeight(t, _poolList.Content.rect.width - 16f, UiStyle.SmallSize) + 8f,
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
                H = UiKit.MeasureTextHeight(text, _poolList.Content.rect.width - 16f, UiStyle.SmallSize) + 8f,
            });
            if (constraints.Count > 0)
                _poolItems.Add(new PoolRowData { Kind = 3, Text = "清除全部", H = PoolBtnH });
        }

        // ======================================== 载入编辑 / 新建 / 删除 ========================================

        /// <summary>载入已有卡编辑（覆盖替换语义；深拷贝容器、共享效果实例——效果合成器写回=整实例替换）。</summary>
        private void LoadCardForEdit(CardData src)
        {
            _card.CardName = src.CardName;
            _card.Supertype = src.Supertype;
            _card.Illustration = src.Illustration;
            _card.Power = src.Power;
            _card.Life = src.Life;
            _card.Durability = src.Durability;
            _card.Keywords = src.Keywords != null ? new List<string>(src.Keywords) : new List<string>();
            _card.Tags = src.Tags != null ? new List<string>(src.Tags) : new List<string>();
            _card.Effects = src.Effects != null ? new List<CardEffectData>(src.Effects) : new List<CardEffectData>();
            _card.Cost = src.Cost != null ? src.Cost.Clone() : null;
            _card.PayloadCost = src.PayloadCost; // 单条 Payload：填装走整体替换
            _costParamValue = null; // 参数以载入卡自身原子 value 为准
            _card.ArrowDirections = src.ArrowDirections;
            _card.LinkAuras = src.LinkAuras != null ? new List<LinkAuraData>(src.LinkAuras) : null;
            _card.NoAttack = src.NoAttack;
            _card.NoGuard = src.NoGuard;
            _card.SpeedBonus = src.SpeedBonus;
            _card.ResetCache();
            _card.AggregateEffectAuras(true);
            _editingOriginalId = src.ID;
            _kind = MapKind(src.Supertype);
            ResetSlotState(); // 槽位选中/空槽随载入复位（追加态）
            _lastSavedId = src.ID; // 载入=已保存卡：ID 钮可复制
            SyncIdButton();

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
                + $"编辑：{_card.CardName ?? src.ID}（保存将覆盖替换旧卡）");
        }

        private void OnNewCard()
        {
            _card.CardName = null; // 名称可缺省（留空保存=用生成的卡 ID 当卡名）
            _card.Supertype = Cardtype.Creature;
            _card.Illustration = null; // 立绘复位（画廊高亮随 RefreshCardFace 对账）
            _card.Power = 0;
            _card.Life = 0;
            _card.Durability = 0;
            _card.Keywords = new List<string>();
            _card.Tags = new List<string>();
            _card.Effects = new List<CardEffectData>();
            _card.Cost = null;
            _card.PayloadCost = null;
            _costParamValue = null;
            _card.ArrowDirections = default;
            _card.LinkAuras = null;
            _card.NoAttack = _card.NoGuard = false;
            _card.SpeedBonus = 0;
            _card.ResetCache();
            _editingOriginalId = null;
            _kind = CardKind.Creature;
            ResetSlotState(); // 槽位选中/空槽复位（追加态）；_lastSavedId 不清——保存流程尾部即新建，ID 钮跨复位存活

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
            _lastSavedId = null; // 卡已删——ID 钮随之隐藏（该 ID 不复存在，防误复制）
            SyncIdButton();
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
                    _card.SpeedBonus = 0; // 字段已退役（2026-10-15 改版：速度动态算不落字段）
                    // 生物禁挂连接光环（2026-10-15 改版）：切类型即清光环与箭头（防旧数据残留）
                    _card.LinkAuras = new List<LinkAuraData>();
                    _card.ArrowDirections = HexDirection.None;
                    break;
                case CardKind.Spell:
                    _card.Supertype = Cardtype.Spell;
                    _card.Power = null;
                    _card.Life = null;
                    _card.Durability = 0;
                    _card.SpeedBonus = 0;
                    _card.LinkAuras = new List<LinkAuraData>(); // 法术只挂主动效果
                    _card.ArrowDirections = HexDirection.None;
                    _card.Keywords = new List<string>(); // 关键词=生物专属（2026-10-16 槽位化）
                    break;
                case CardKind.Enchantment:
                    _card.Supertype = Cardtype.Enchantment;
                    _card.Power = null;
                    _card.Life = null; // 结界=耐久体（2026-09-24 定案），不用攻/血
                    _card.SpeedBonus = 0; // 速度通道退役（2026-10-15 改版：结界禁主动，免费点全投耐久）
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

        /// <summary>槽行重建（2026-10-16 二次改版）：四类槽行=克隆对应单槽模板（text 标题/des 内容/delete 删除钮）；
        /// 已填槽在前、空槽在后；选中槽=白描边+行底色。效果全文写进 des。</summary>
        private void RefreshSlots()
        {
            EnsureCardLists();
            if (_cardFace == null) return;
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
            ClearRuntimeChildren(_cardFace);
            BuildSlots(ZoneId.Cost);
            BuildSlots(ZoneId.Effect);
            BuildSlots(ZoneId.Passives);
            BuildSlots(ZoneId.Keys);
            SyncAddSlotButtons();
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

        /// <summary>单槽行（克隆模板）：text=序号标题、des=效果全文/关键词/代价文案（空槽=操作提示）、
        /// delete=删除（已填删数据/空槽删占位）；行点击=选中该槽（右栏按槽类型过滤，点选候选填入）。</summary>
        private void MakeSlotRow(ZoneId id, RectTransform tpl, int dataIndex, int ordinal)
        {
            var row = UiKit.CloneTemplateFrom(_cardFace, tpl.name, _cardFace);
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
            if (id == ZoneId.Cost) BindCostGenerate(row, dataIndex);
        }

        /// <summary>代价槽 generate 参数输入（2026-10-21 单参数定案）：prefab cost 模板下 TMP_InputField——
        /// 代价=单原子效果，参数（原子 value）由此框驱动：已填=回显并随键写回（des 文案与计价按
        /// 「generate 值＋所选原子」即时重生成，不整刷槽行防丢输入焦点）；空槽隐藏。
        /// 使用时强制发动为固定语义（cast 付费步执行），不提供开关。</summary>
        private void BindCostGenerate(RectTransform row, int dataIndex)
        {
            var field = UiKit.FindDeep(row, "generate")?.GetComponent<UInputField>();
            if (field == null) return; // prefab 缺 generate（旧 prefab 兼容）——参数输入跳过
            var atoms = dataIndex >= 0 ? _card.PayloadCost?.AllPayloads() : null;
            bool filled = atoms != null && atoms.Count > 0;
            field.gameObject.SetActive(filled);
            if (!filled) return;
            var atom = atoms[0];
            field.contentType = UInputField.ContentType.IntegerNumber;
            field.SetTextWithoutNotify(atom.value.ToString());
            field.onValueChanged.RemoveAllListeners();
            field.onValueChanged.AddListener(s =>
            {
                if (!int.TryParse(s, out var v)) return;
                atom.value = v;
                _costParamValue = v;
                _card.ResetCache();
                var des = UiKit.FindDeep(row, "des")?.GetComponent<TMP_Text>();
                if (des != null) des.text = SlotDesText(ZoneId.Cost, dataIndex);
                Recalculate();
            });
            Describe(field, "代价参数：使用时强制发动的数值——描述与计价按此值和所选原子自动生成");
        }

        private static string SlotTitle(ZoneId id, int ordinal) =>
            (id == ZoneId.Cost ? "代价槽" : id == ZoneId.Effect ? "主动槽" : id == ZoneId.Passives ? "自动槽" : "关键词")
            + (char)('①' + ordinal) + "：";

        /// <summary>槽行 des 内容：已填=效果全文（名称+摘要）/关键词/代价文案；空槽=操作提示。
        /// 与卡型不匹配的已填槽前缀⚠（保存闸兜底）。</summary>
        private string SlotDesText(ZoneId id, int dataIndex)
        {
            if (dataIndex < 0)
                return $"（空槽）点击行选中 → 右栏按「{TypeZh(id)}」过滤 → 点候选项填入此槽";
            string body;
            if (id == ZoneId.Cost)
            {
                var atoms = _card.PayloadCost?.AllPayloads() ?? new List<CardCore.AtomicEffectEntry>();
                var parts = new List<string>();
                foreach (var a in atoms)
                    if (a != null && !string.IsNullOrEmpty(a.refId)) parts.Add(AtomText.RenderAtomEntry(a));
                // 文案自动生成（2026-10-21 单参数定案）：generate 值代入所选原子模板 + 固定「使用时强制发动」前缀
                body = "使用时强制发动：" + string.Join("＋", parts) + PayloadGrantText(atoms);
            }
            else if (id == ZoneId.Keys)
            {
                body = KeywordZh(_card.Keywords[dataIndex]);
            }
            else
            {
                var fx = _card.Effects[dataIndex];
                var name = string.IsNullOrEmpty(fx?.DisplayName) ? $"效果 #{dataIndex + 1}" : fx.DisplayName;
                if (id == ZoneId.Passives && IsAuraEffect(fx)) name = "[光环] " + name;
                var summary = fx == null ? "" : AtomText.RenderEffectSummary(
                    new EffectGraphData(name) { header = fx, steps = fx.Steps }, _card);
                body = string.IsNullOrEmpty(summary) ? name : name + "\n" + summary;
            }
            return TypeEnabledForKind(id) ? body : "⚠与本卡类型不匹配（保存会被拦）——删除或换型\n" + body;
        }

        private string SlotRowDesc(ZoneId id, int dataIndex) => dataIndex < 0
            ? $"选中这个空{TypeZh(id)}槽：右栏自动按「{TypeZh(id)}」过滤候选，点选填入此槽"
            : id switch
            {
                ZoneId.Cost => "代价槽：选中后右栏列出可作代价的组装图（点选=替换）",
                ZoneId.Keys => "关键词槽：选中后右栏列出关键词目录（点选=替换）",
                _ => "效果槽：选中后右栏列出对应形态的效果（点选=整实例替换此槽）",
            };

        /// <summary>槽删除：已填=删数据（效果/关键词/代价），空槽=回收占位。</summary>
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
                case ZoneId.Cost:
                    _card.PayloadCost = null;
                    _costParamValue = null;
                    _card.ResetCache();
                    break;
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

        // ======================================== 代价栏（cost 槽·三类卡通用） ========================================

        /// <summary>填装代价槽（2026-10-21 代价简化定案：候选=效果库**单原子**图——一般主动/赋予，
        /// 组合图（多原子并列/原子带分支）出局；关键词仍是唯一保留原子表直加的槽类型）。
        /// 填入的原子按代价语义执行/计价/补偿。</summary>
        private void FillPayloadCost(EffectGraphData graph)
        {
            var atoms = CostEligibleAtoms(graph, out var reject);
            if (atoms == null)
            {
                ShowToast(reject);
                return;
            }
            if (_costParamValue.HasValue && atoms.Count > 0)
                atoms[0].value = _costParamValue.Value; // 换选原子沿用 generate 已填参数（单参数效果：参数不随原子走）
            _card.PayloadCost = new CostEntry
            {
                CostType = (int)CostType.Payload,
                Value = 1,
                payloads = atoms,
            };
            _card.ResetCache();
            ConsumeEmptySlot(ZoneId.Cost); // 填入空代价槽（或全量视图兜底）
            RefreshSlots();
            Recalculate();
            int totalPrice = PayloadTotalGrant(atoms);
            ShowToast($"已填装代价（全价 {totalPrice}·占1槽）——全价超过地牌槽上限时整卡不可用");
        }

        /// <summary>代价资格（2026-10-21 代价简化定案）：只收**单原子**效果（一般主动/赋予）——
        /// 恰好 1 个 kind:0 原子步骤、原子自身不带 branch（条件奖励/引擎分支的组装图出局），
        /// 且该原子可作代价（非关键词/非系统内部 + PayloadCostDomain 逆转域收窄写 kinds）。
        /// null=不可作代价（reject 带原因）；返回的单原子序列直接入 CostEntry.payloads。</summary>
        private static List<CardCore.AtomicEffectEntry> CostEligibleAtoms(EffectGraphData graph, out string reject)
        {
            reject = null;
            var steps = graph?.steps;
            if (steps == null || steps.Count == 0)
            {
                reject = "空效果（无原子步骤）不可作代价";
                return null;
            }
            if (steps.Count > 1)
            {
                reject = $"组合效果（{steps.Count} 原子并列）不可作代价——代价只收单原子效果（一般主动/赋予）";
                return null;
            }
            var atoms = new List<CardCore.AtomicEffectEntry>();
            foreach (var s in steps)
            {
                if (s == null || s.kind != 0 || s.atomic == null || string.IsNullOrEmpty(s.atomic.refId))
                {
                    reject = "组合效果（含条件分支/抉择步骤）不可作代价——代价只收单原子效果（一般主动/赋予）";
                    return null;
                }
                if (s.atomic.branch != null)
                {
                    reject = $"组合效果（「{graph.name}」原子带条件奖励/引擎分支）不可作代价——代价只收单原子效果（一般主动/赋予）";
                    return null;
                }
                atoms.Add(s.atomic);
            }
            foreach (var entry in atoms)
            {
                var cfg = AtomicEffectTable.GetByHashId(entry.refId);
                if (cfg == null)
                {
                    reject = "原子表引用缺失——无法作代价";
                    return null;
                }
                if (ComposerCatalog.HasMountBit(cfg, MountKind.SystemInternal))
                {
                    reject = $"「{cfg.DisplayName}」系统内部原子不可作代价";
                    return null;
                }
                if (ComposerCatalog.HasMountBit(cfg, MountKind.Keyword))
                {
                    reject = $"「{cfg.DisplayName}」关键词类原子不可作代价（关键词走专属槽）";
                    return null;
                }
                // 有效域=组合期已解析的 entry.kinds（「对敌方单位造成伤害」kinds=[2]），空表/null 回落表默认——
                // 按有效域整体镜像到错误侧，防表默认多值域收窄丢组合解析（名次回落泛称「目标」）；
                // 注意 kinds 可能是 DTO 往返物化的**空列表**（非 null）——空=未声明，须显式回落
                var (kinds, eligible) = CostDerivationService.PayloadCostDomain(
                    cfg, entry.kinds != null && entry.kinds.Count > 0 ? entry.kinds : cfg.GetTargetKindList());
                if (!eligible)
                {
                    reject = $"「{cfg.DisplayName}」无法逆转到错误一侧（纯自身域有益/中性双侧域）——不可作代价";
                    return null;
                }
                entry.kinds = kinds ?? entry.kinds; // 收窄/镜像写回；null=免写（保持组合解析/表默认，幂等）
            }
            return atoms;
        }

        /// <summary>代价候选行预览（2026-10-16 用户反馈补）：按**代价语义**渲染整图——逐原子
        /// 翻转/收窄后的文案（克隆条目解析，不动库对象）＋黑/白补偿后缀。
        /// 组合原文案（效果库口径）不含代价翻转，此口让填入前的列表就看到真实执行文本。</summary>
        private static string CostPreviewText(EffectGraphData graph)
        {
            var steps = graph?.steps;
            if (steps == null || steps.Count == 0) return "";
            var parts = new List<string>();
            var clones = new List<CardCore.AtomicEffectEntry>();
            foreach (var s in steps)
            {
                if (s == null || s.kind != 0 || s.atomic == null || string.IsNullOrEmpty(s.atomic.refId)) return "";
                var src = s.atomic;
                var clone = new CardCore.AtomicEffectEntry
                {
                    refId = src.refId,
                    value = src.value,
                    str = src.str,
                    amp = src.amp,
                    count = src.count,
                    rand = src.rand,
                    defer = src.defer,
                    kinds = src.kinds != null ? new List<int>(src.kinds) : null,
                    branch = src.branch,
                };
                var cfg = AtomicEffectTable.GetByHashId(clone.refId);
                if (cfg != null)
                {
                    var (kinds, _) = CostDerivationService.PayloadCostDomain(
                        cfg, clone.kinds != null && clone.kinds.Count > 0 ? clone.kinds : cfg.GetTargetKindList());
                    if (kinds != null) clone.kinds = kinds; // null=免写：保持组合解析（空表=未声明同免写）
                }
                clones.Add(clone);
                parts.Add(AtomText.RenderAtomEntry(clone));
            }
            return string.Join("＋", parts) + PayloadGrantText(clones);
        }

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

        /// <summary>代价槽 des 后缀（2026-10-16 用户定案格式）：「，获得N个黑/白元素」——
        /// 逐原子全价+极性色聚合（混合图黑白并列「，获得1黑1白元素」）；解析失败兜底回旧文案。
        /// 2026-10-21 改 public：卡面代价栏（CardOverlayItem.PayloadTextOf）同源复用。</summary>
        public static string PayloadGrantText(List<CardCore.AtomicEffectEntry> entries)
        {
            int black = 0, white = 0;
            foreach (var e in entries ?? new List<CardCore.AtomicEffectEntry>())
            {
                var inst = CardEffectConverter.ConvertPayloadForDisplay(e);
                int amount = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
                if (inst == null || amount <= 0) continue;
                if (CostCompensationService.PayloadGrantColor(inst) == ManaType.White) white += amount;
                else black += amount;
            }
            if (black == 0 && white == 0) return "，生效后补偿黑/白";
            if (black > 0 && white > 0) return $"，获得{black}黑{white}白元素";
            return black > 0 ? $"，获得{black}个黑元素" : $"，获得{white}个白元素";
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
                ShowToast($"关键词槽已满（{_card.Keywords.Count}/{KeywordCapacity}）——可关闭攻击/守卫能力腾出槽位");
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

        // ======================================== 右栏：效果库列表（zone 选中=按区过滤·回收式） ========================================

        private const float LibHeaderH = 28f;    // 表头行高
        private const float LibNewBtnH = 40f;    // 「新建效果」钮行高
        private const float LibRowH = 60f;       // 效果/候选行高=模板行

        /// <summary>效果库刷新（2026-10-16 二次改版）：选中槽 → 按槽类型过滤候选——cost=代价单原子图（一般主动/赋予·2026-10-21 简化定案）/
        /// effect=主动图 / passives=光环+自动图（结界分组）/ keys=关键词目录（唯一原子表直加口）；未选槽=全量效果图。
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
                    case ZoneId.Cost: BuildLibraryPayloadView(); break;
                    case ZoneId.Effect: BuildLibraryActiveView(); break;
                    case ZoneId.Passives: BuildLibraryPassivesView(); break;
                    case ZoneId.Keys: BuildLibraryKeywordsView(); break;
                }
            }
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
                H = UiKit.MeasureTextHeight(text, _libraryList.Content.rect.width - 16f, UiStyle.SmallSize) + 8f,
            });

        private void LibNewBtn() =>
            _libItems.Add(new LibRowData { Kind = 2, H = LibNewBtnH });

        private void LibEffect(EffectGraphData graph, Action<EffectGraphData> onClick = null,
            string summaryOverride = null)
        {
            var name = string.IsNullOrEmpty(graph.name) ? "(未命名)" : graph.name;
            var summary = summaryOverride ?? AtomText.RenderEffectSummary(graph);
            if (!RowPassFilters(name + "\n" + summary, graph.header?.AnchorCost)) return;
            _libItems.Add(new LibRowData
            {
                Kind = 3, Graph = graph, Text = name, SummaryOverride = summary,
                Click = onClick, H = LibRowH,
            });
        }

        private void LibCandidate(string title, string summary, Action onClick)
        {
            if (!RowPassFilters(title + "\n" + (summary ?? ""), null)) return;
            _libItems.Add(new LibRowData
            {
                Kind = 4, Text = string.IsNullOrEmpty(summary) ? title : title + "\n" + summary,
                ClickAction = onClick, H = LibRowH,
            });
        }

        private void BuildLibraryFullView()
        {
            LibNewBtn();
            var graphs = EffectLibrarySerializer.LoadAll();
            LibHeader($"效果库（{graphs.Count}）——点击=填入中间选中槽；「编辑」=改库");
            if (graphs.Count == 0)
            {
                LibHint("效果库为空——「新建效果」或在效果合成器保存即入库");
                return;
            }
            foreach (var graph in graphs)
                LibEffect(graph);
        }

        /// <summary>代价视图（2026-10-21 代价简化定案）：候选=效果库**单原子**图（一般主动/赋予）
        /// 且该原子可作代价——组合图（多原子并列/原子带条件奖励/引擎分支）出局；
        /// 代价槽不直列原子表（关键词是唯一原子表直加的槽类型）。行文案=**翻转后预览**
        /// （CostPreviewText：原子按代价语义镜像/收窄＋黑/白补偿——组合原文案不含翻转）。
        /// 点击=填入代价槽。</summary>
        private void BuildLibraryPayloadView()
        {
            var graphs = EffectLibrarySerializer.LoadAll()
                .Where(g => CostEligibleAtoms(g, out _) != null)
                .ToList();
            LibHeader($"代价效果（{graphs.Count}）——只收单原子效果（一般主动/赋予）·点击=填入代价槽（文案=翻转后执行语义·生效后补偿黑/白）");
            if (graphs.Count == 0)
            {
                LibHint("效果库没有可作代价的单原子效果——须为单原子（无并列组合·无分支奖励）且原子可逆转到错误一侧；可先在效果组合界面做单原子效果");
                return;
            }
            foreach (var graph in graphs)
                LibEffect(graph, FillPayloadCost, CostPreviewText(graph));
        }

        private void BuildLibraryActiveView()
        {
            LibNewBtn();
            var graphs = EffectLibrarySerializer.LoadAll()
                .Where(g => !EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(g)) && IsActiveGraph(g))
                .ToList();
            LibHeader(_kind == CardKind.Spell
                ? $"主动效果（{graphs.Count}）——点击=填入主动槽（1~3 个·施放抉择·速度加成=3−槽数）"
                : $"主动效果（{graphs.Count}）——点击=填入主动槽（生物×1）");
            if (graphs.Count == 0)
            {
                LibHint("库中没有主动形态的效果——「新建效果」在效果合成器里做主动效果");
                return;
            }
            foreach (var graph in graphs)
                LibEffect(graph);
        }

        private void BuildLibraryPassivesView()
        {
            LibNewBtn();
            var graphs = EffectLibrarySerializer.LoadAll();
            bool Aura(EffectGraphData g) => EffectComposerScreen.IsAuraMode(EffectComposerScreen.InferMode(g));
            if (_kind == CardKind.Enchantment)
            {
                var auras = graphs.Where(Aura).ToList();
                var autos = graphs.Where(g => !Aura(g) && !IsActiveGraph(g)).ToList();
                LibHeader($"光环（{auras.Count}）——必填·恰好 1（规则或连接）");
                if (auras.Count == 0) LibHint("库中没有光环形态的效果——「新建效果」在效果合成器里做光环");
                foreach (var g in auras) LibEffect(g);
                LibHeader($"自动效果（{autos.Count}）——至多 2（每槽占 1 点免费预算，盈余转入场耐久）");
                if (autos.Count == 0) LibHint("库中没有自动/触发形态的效果");
                foreach (var g in autos) LibEffect(g);
            }
            else
            {
                var autos = graphs.Where(g => !Aura(g) && !IsActiveGraph(g)).ToList();
                LibHeader($"自动效果（{autos.Count}）——点击=填入自动槽（生物×1）");
                if (autos.Count == 0) LibHint("库中没有自动/触发形态的效果");
                foreach (var g in autos) LibEffect(g);
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
                LibCandidate(captured.DisplayName, "", () => FillKeyword(captured));
            }
        }

        // ---- 行工厂/绑定（结构建一次，数据反复绑） ----

        /// <summary>效果库行工厂路由（kind 分池：0 表头 / 1 提示 / 2 新建钮 / 3 效果行 / 4 候选行）。</summary>
        private UiKit.RecycledList.MakeFn LibRowFactory(int kind)
        {
            switch (kind)
            {
                case 0: return MakeLibHeaderRow;
                case 1: return MakeLibHintRow;
                case 2: return MakeLibNewBtnRow;
                case 4: return MakeLibCandidateRow;
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
            var lbl = UiKit.Label("lbl", rt, "", UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
            UiKit.StretchInset(lbl.rectTransform, 4f, 4f);
            return rt;
        }

        /// <summary>「新建效果」钮行结构：满宽主按钮（监听 bind 时挂）。</summary>
        private RectTransform MakeLibNewBtnRow()
        {
            var rt = UiKit.Node("newbtn", _libraryList.Content);
            var btn = UiKit.Button("btn", rt, "＋ 新建效果（挂到当前卡）", null);
            UiKit.Stretch((RectTransform)btn.transform);
            return rt;
        }

        /// <summary>效果行结构：list-library 模板克隆 + 行内「编辑」小按钮（锚右居中，原口径）。</summary>
        private RectTransform MakeLibEffectRowStruct()
        {
            var row = CloneRow(_libraryList);
            if (row == null) return UiKit.Node("row-miss", _libraryList.Content);
            var edit = UiKit.MiniButton("edit", row, "编辑", null);
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
                    var meta = row.GetComponentInChildren<TMP_Text>(true);
                    if (meta != null) meta.text = item.Text;
                    var csv = row.GetComponentInChildren<CostSquaresView>(true);
                    if (csv != null) csv.Clear();
                    var btn = row.GetComponent<UnityEngine.UI.Button>();
                    var title = item.Text.Split('\n')[0];
                    var act = item.ClickAction;
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => { Debug.Log("[UI点击] 候选行 " + title); act?.Invoke(); });
                    }
                    Describe(row, $"点击=填入当前选中区/槽：{title}");
                    return;
                }
            }

            // ---- 效果行（原 MakeLibraryEffectRow 全量口径） ----
            var graph = item.Graph;
            var name = item.Text;
            var summary = item.SummaryOverride ?? "";
            var meta2 = row.GetComponentInChildren<TMP_Text>(true);
            if (meta2 != null) meta2.text = string.IsNullOrEmpty(summary) ? name : name + "\n" + summary;
            var csv2 = row.GetComponentInChildren<CostSquaresView>(true);
            if (csv2 != null)
            {
                var cost = graph.header?.AnchorCost;
                if (cost != null && !cost.IsZero) csv2.SetCosts(cost);
                else csv2.Clear();
            }
            var rowBtn = row.GetComponent<UnityEngine.UI.Button>();
            if (rowBtn != null)
            {
                var click = item.Click;
                rowBtn.onClick.RemoveAllListeners();
                rowBtn.onClick.AddListener(() =>
                {
                    Debug.Log("[UI点击] 效果库行 " + name);
                    if (click != null) click(graph);
                    else FillSelectedSlot(graph);
                });
            }
            Describe(row, item.Click != null
                ? $"点击=把「{name}」整图填入代价槽（逐原子全价·生效后补偿黑/白）"
                : $"点击=把「{name}」填入中间选中槽位（未选槽=追加到末尾）");
            var editBtn = UiKit.FindDeep(row, "edit")?.GetComponent<UnityEngine.UI.Button>();
            if (editBtn != null)
            {
                editBtn.onClick.RemoveAllListeners();
                editBtn.onClick.AddListener(() => OnEditLibraryEffect(graph));
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
                    : "法术只挂主动效果（施放时抉择其一），不可挂光环");
                return;
            }
            // 时机形态闸：法术只收主动（Active/Instant）；结界禁一切主动
            int t = graph?.header?.TriggerTiming ?? -1;
            bool activated = t == (int)TriggerTiming.Activate_Active || t == (int)TriggerTiming.Activate_Instant
                             || t == (int)TriggerTiming.Activate_Response;
            if (_kind == CardKind.Enchantment && activated)
            {
                ShowToast("结界只挂自动/光环——主动效果已随 2026-10-15 改版退役");
                return;
            }
            if (_kind == CardKind.Spell && !activated)
            {
                ShowToast("法术只挂主动效果（施放时弹窗抉择其一）——自动/触发/响应式不收");
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

        private void BuildArtGallery()
        {
            if (_galleryList == null) return;
            if (_galleryForKind == _kind) { _galleryList.RebindVisible(); return; } // 同类型：仅对账高亮
            _galleryForKind = _kind;
            _artNames = new List<string>();
            if (_kind == CardKind.Creature)
            {
                _artNames.Add(null); // 占位项（清空立绘=预制体占位图，ACardView 同口径）
                _artNames.AddRange(CreatureArtNames());
            }
            _galleryList.SetGrid(_artNames.Count); // 格几何（210×270·间16）由 Attach 从 GLG 吸收
        }

        /// <summary>生物立绘可选名（=模型名，与战场 MonsterModelResolver 同源：StreamingAssets bundle
        /// 内嵌 #names 名单，全平台同一条路径）。打包侧只收「prefab+预览图齐全」的目录——名单即全部可选。
        /// 旧编辑器直扫 YGOMonstersRig 目录版已随 2026-10-17 StreamingAssets 化退役（#if 空体正是
        /// CS0161 根因：非编辑器编译下方法体无 yield 无 return）。</summary>
        private static IEnumerable<string> CreatureArtNames()
        {
            return Tide.HotUpdate.MonsterRigAssets.Names();
        }

        /// <summary>贴片结构（工厂，只建一次）：底板+缩略图（占位字随绑显隐）+名称+按钮+选中描边。
        /// 内部 VLG（thumb/label 排版）不受 content 布局接管影响——只摘了 content 上的 GLG。</summary>
        private RectTransform MakeArtTile()
        {
            var tile = UiKit.Column("tile", _galleryContent, spacing: 4f, pad: 6f);
            var bg = tile.gameObject.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.19f, 0.22f, 0.8f);
            bg.raycastTarget = true;

            var thumb = UiKit.Node("thumb", tile);
            var img = thumb.gameObject.AddComponent<Image>();
            UiKit.Size(thumb, w: 198f, h: 200f);
            var hint = UiKit.Label("hint", thumb, "占位", UiStyle.BodySize, UiStyle.TextHint,
                TextAnchor.MiddleCenter);
            UiKit.StretchInset(hint.rectTransform, 2f, 0f);
            hint.gameObject.SetActive(false); // 占位项才显示（bind 开关）

            var label = UiKit.Label("label", tile, "", UiStyle.SmallSize, UiStyle.TextBody, wrap: true);
            UiKit.Size(label, fw: 1f);

            var btn = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            var ol = tile.gameObject.AddComponent<Outline>(); // 选中描边（bind 对账开关）
            ol.effectColor = UiStyle.TextPrimary;
            ol.effectDistance = new Vector2(2f, -2f);
            ol.enabled = false;
            return tile;
        }

        /// <summary>贴片绑定：立绘图/占位字/名称/选中描边/点击监听（原 HighlightArtTile 高亮并入此处）。</summary>
        private void BindArtTile(RectTransform tile, int index)
        {
            var spriteName = _artNames[index];
            bool placeholder = string.IsNullOrEmpty(spriteName);

            var thumb = tile.Find("thumb") as RectTransform;
            var img = thumb != null ? thumb.GetComponent<Image>() : null;
            if (img != null)
            {
                var sp = placeholder ? null : CardArtLibrary.Load(spriteName); // 按名称两级回落（内部带缓存）
                img.sprite = sp; // 池行复用须显式清旧图
                img.preserveAspect = true;
                img.color = sp != null ? Color.white : new Color(0f, 0f, 0f, 0.25f);
            }
            var hintGo = thumb != null ? thumb.Find("hint") : null;
            if (hintGo != null) hintGo.gameObject.SetActive(placeholder);
            var label = tile.Find("label")?.GetComponent<TMP_Text>();
            if (label != null) label.text = placeholder ? "（占位）" : spriteName;

            var ol = tile.GetComponent<Outline>();
            if (ol != null) ol.enabled = (_card.Illustration ?? "") == spriteName; // 高亮=与当前立绘对账

            var btn = tile.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) return;
            var captured = spriteName;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Debug.Log("[UI点击] 立绘 " + (string.IsNullOrEmpty(captured) ? "占位" : captured));
                _card.Illustration = string.IsNullOrEmpty(captured) ? null : captured;
                Recalculate(); // 卡面立绘随 RefreshCardFace 对账
                _galleryList.RebindVisible(); // 画廊高亮同帧对账（原 HighlightArtTile 全量重描）
                ShowToast(string.IsNullOrEmpty(captured) ? "立绘已清空（占位图）" : "立绘已选：" + captured);
            });
            Describe(tile, placeholder
                ? "清空立绘（卡面回到占位图）"
                : $"选用立绘 {captured}——卡面立绘与战场 3D 模型同名双源");
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
                + (factor < 1f ? "×f" + factor.ToString("0.###") : "")
                + "＝D" + r.Total.ToString("0") + "（" + CostZh(r.SuggestedCost) + "）";
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

            // 形状拦截（2026-10-15 深夜三类卡改版）：法术=1~3 个主动效果；结界=禁一切主动。
            if (_kind == CardKind.Spell)
            {
                int count = _card.Effects?.Count ?? 0;
                if (count < 1 || count > 3)
                {
                    ShowToast($"法术须挂 1~3 个主动效果（现 {count} 个——施放时弹窗抉择其一）");
                    return;
                }
                foreach (var e in _card.Effects)
                {
                    int t = e?.TriggerTiming ?? -1;
                    if (t != (int)TriggerTiming.Activate_Active && t != (int)TriggerTiming.Activate_Instant)
                    {
                        ShowToast($"法术效果「{e.DisplayName ?? e.Id}」须为主动效果（法术只挂主动，自动/触发/响应式不收）");
                        return;
                    }
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
                        ShowToast($"结界效果「{e.DisplayName ?? e.Id}」为主动效果——结界只挂自动/光环（2026-10-15 改版）");
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

            // 名称缺省（2026-10-16 槽位化）：留空=用生成的卡 ID 当卡名；
            // ToEntry 随后按新卡名重算正式 id——名称/ID 自洽，卡池行优先显卡名
            bool nameFromId = string.IsNullOrWhiteSpace(_card.CardName);
            var newId = "C_" + ContentHasher.HashCard(_card);
            if (nameFromId) _card.CardName = newId;
            bool replacing = !string.IsNullOrEmpty(_editingOriginalId) && _editingOriginalId != newId;

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
            CardCatalog.Invalidate();
            CorpusCache.Clear();
            TutorialCreationFlow.NotifyCardsChanged(); // 第三课走查步检测（未开课零行为）

            // 落盘真 ID（ToEntry 同源）：名字缺省时上方 newId 在回填卡名前算的——以保存后的最终内容重算
            _lastSavedId = "C_" + ContentHasher.HashCard(_card);
            SyncIdButton();
            ShowToast(replacing
                ? $"已覆盖替换：{_card.CardName}（{_editingOriginalId} → {_lastSavedId}）"
                : $"已保存到卡表：{System.IO.Path.GetFileName(path)}（{_lastSavedId}）——点 type 区 ID 钮可复制");

            OnNewCard();
            RefreshLibrary();
        }

        private void ApplyTextLists()
        {
            _card.CardName = string.IsNullOrWhiteSpace(_nameInput?.text) ? null : _nameInput.text.Trim(); // null=保存时缺省成 ID
            // 标签输入已退役（2026-10-16 prefab 重做）：_card.Tags 载入/保存透传，不再从输入框覆写
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
