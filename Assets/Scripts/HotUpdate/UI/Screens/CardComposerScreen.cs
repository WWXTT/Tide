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
    /// 卡牌合成界面（2026-10-01 预制体化：静态层级来自 Assets/Art/Prefab/CardUI.prefab，
    /// Build 深度按名绑定+闭包接线；滚动列表行/动态表单/代价栏运行时重建）——三栏（2026-10-13 六区改版）：
    /// 左=美术栏（立绘画廊点选设置 Illustration；美术可选项未来扩展入场特效等资源列表）、
    /// 中=卡牌全部可编辑项按 ACard 六区组织（顶部 ACard 实时预览：类型/立绘/声明费→名称→
    /// 属性/底盘/光环→效果槽位→关键词→tags/代价栏/算费/保存；中栏整体滚动）、
    /// 右=切换式列表（效果列表↔卡列表：效果行点选=填入中间选中槽〔参考效果组合界面的槽位交互〕；
    /// 卡行点击=整卡数据载入中间编辑）。
    /// 2026-10-13 晚补齐（用户验收）：控件呈现层以效果组合界面为参照——CardUI.prefab 根下
    /// tpl-root 七棵模板（从 EffectUI.prefab 拷贝的同款子树+自身 chip 克隆），本屏运行时控件
    /// 一律走模板克隆（TplDropdown/TplInput/TplToggle/TplButton/tpl-lib-row），不再 UiKit 手搓。
    ///
    /// 数据模型（2026-09-24 关键词引用化定案）：卡只做卡层增量（名称/类型/身材/耐久/声明费/代价栏），
    /// 效果与关键词全部走 effectIds 引用——**直接引用原子 refId=本体关键词**（本界面关键词区），
    /// 引用 Effects.json 组合效果=效果挂载（其中的赋予族=赋予关键词，赋予亦可作用于自己）。
    /// 编辑旧卡保存=覆盖替换（删旧存新；旧卡被卡组引用则拦截——内容哈希 ID 机制下无法原地覆盖）。
    /// 超模校验已按 2026-09-24 定案移除——卡层只算减费与建议档位，不做 D≤C 合规判断。
    /// </summary>
    public sealed class CardComposerScreen : UIScreen
    {
        protected override string PrefabName => "CardUI";
        protected override string RootName => "card-composer";
        // 业务类型（UI 维度，非裸 Cardtype）：生物 / 法术（一次性单发）/ 结界（耐久体）。
        // 术语（2026-10-10 收口；2026-10-15 速度定案修订·已落地）：原「瞬间」改口「法术」；发动速度不再是
        // 效果自带参数（效果组合界面速度组已删，新组合 BaseSpeed 恒 0）——速度由三个免费额度按卡型
        // 默认分配：法术=效果槽1+发动速度2（SpeedBonus 缺省 2，可下调换抵价）/ 结界装主动效果=
        // 效果槽1+耐久1+发动速度1（速+1 与耐久加成由 CardCompositionCost 动态算，无控件）/
        // 生物=属性1+攻击1+守卫1（免费点只减灰桶 S）。结界「剩余转耐久」二选一已退役（2026-10-15 拆分定案）。
        private enum CardKind { Creature, Spell, Enchantment }

        private static readonly (CardKind kind, string name)[] KindNames =
        {
            (CardKind.Creature, "生物"), (CardKind.Spell, "法术"), (CardKind.Enchantment, "结界"),
        };

        private readonly CardData _card = new CardData
        {
            CardName = "新卡牌",
            Supertype = Cardtype.Creature,
            Power = 0,
            Life = 0,
        };

        /// <summary>覆盖替换（2026-09-24 定案）：正在编辑的旧卡 ID；null=新建。效果合成器往返期间保留。</summary>
        private string _editingOriginalId;

        private CardKind _kind = CardKind.Creature;

        // ---- 左栏筛选态（2026-10-13 检索改版） ----
        // 颜色多选（定案：像箭头盒多选，子集语义——见 CostColorsWithin）；类别=单选+「全部」
        //（词=卡内原子的表行中文短名，与效果组合界面分类下拉同源 config.DisplayName）；搜索=多词AND语料匹配。
        private readonly HashSet<UIColor> _colorFilterSet = new HashSet<UIColor>();
        private string _categoryFilter; // null = 全部
        private readonly Dictionary<string, UnityEngine.UI.Button> _categoryChips = new Dictionary<string, UnityEngine.UI.Button>();
        private RectTransform _categoryChipRow; // runtime 建行（pool-filter 内颜色行下方，prefab 已扩高腾位）
        private string _categoryChipSignature; // 词表签名——池词表变化才重建类别行
        // 语料缓存（交接稿配套）：卡 ID 内容寻址（内容变 ID 变，天然无陈旧）；进屏/保存成功仍整体清空。
        private static readonly Dictionary<string, string> CorpusCache = new Dictionary<string, string>();

        private UiKit.Scroll _poolList;
        private UiKit.Scroll _attachedList;
        private UiKit.Scroll _breakdownList;
        private RectTransform _dynamicForm;
        private RectTransform _payloadZone;
        private RectTransform _keywordZone;
        private RectTransform _effectLibraryZone;
        private RectTransform _poolFilter;
        private TMP_Text _toast;
        private TMP_Text _suggested;
        private TMP_Text _costLabel;
        private UInputField _nameField;
        private UInputField _tagsField;
        private UInputField _searchField;
        private UiKit.Dropdown _cardtypeDropdown;

        // ---- 六区改版（2026-10-13）：中栏 ACard 预览 / 立绘链路 / 效果槽选中制 / 右栏切换 ----
        private ACardView _previewView;                 // 中栏实时卡面（静态 Full：RuntimeCard=null 不订阅事件）
        private TMP_Text _artCurLabel;                  // type 区当前立绘指示（选择在左栏画廊）
        private RectTransform _costManualZone;          // 六色声明费容器（type 区；dynamic-form 只留卡型相关字段）
        private RectTransform _galleryContent;          // 左栏美术画廊滚动内容
        private readonly Dictionary<string, RectTransform> _artTiles = new Dictionary<string, RectTransform>();
        private int _selectedSlot = -1;                 // 效果槽选中制：[0,Count)=替换该槽；-1=尾部空槽（追加态，默认）
        private readonly List<RectTransform> _rightFxNodes = new List<RectTransform>();   // 右栏效果视图组
        private readonly List<RectTransform> _rightCardNodes = new List<RectTransform>(); // 右栏卡池视图组
        private bool _rightShowCards = true;            // 默认卡列表（延续原卡池主入口）
        private Button _btnViewFx, _btnViewCards;       // 切换钮（Outline 点亮当前侧）

        // 控件模板根（CardUI.prefab 根下 tpl-root，常驻不激活；2026-10-13 晚补齐：子树拷自 EffectUI.prefab）
        private RectTransform _tplRoot;

        // 卡池颜色过滤 chips（预制体烘焙，Build 一次绑定；激活态=Outline 开关）。
        private readonly Dictionary<UIColor, UnityEngine.UI.Button> _filterChips = new Dictionary<UIColor, UnityEngine.UI.Button>();

        private ElementCost _lastSuggestedCost;

        protected override void Build()
        {
            // ---- 工具栏 ----
            BindButton("btn-back", () => Manager.Back());
            _nameField = FindInput("field-card-name");
            _tagsField = FindInput("field-tags");
            BindButton("btn-new", OnNewCard);
            BindButton("btn-save", OnSave);
            _toast = FindText("lbl-toast");

            // ---- 三栏容器/滚动区 ----
            _poolFilter = Find("pool-filter");
            _searchField = FindInput("field-search");
            if (_searchField != null)
                _searchField.onValueChanged.AddListener(_ => RefreshPool());
            _poolList = FindScroll("list-pool");

            // 类型下拉：真 TMP_Dropdown（首次 Build 把旧按钮头原位换装成 tpl-dropdown 克隆，幂等）
            var typeTmp = EnsureCardtypeDropdown();
            if (typeTmp != null)
            {
                _cardtypeDropdown = new UiKit.Dropdown(typeTmp,
                    KindNames.Select(k => k.name).ToList(), 0,
                    onChanged: (idx, _) =>
                    {
                        if (idx >= 0 && idx < KindNames.Length)
                        {
                            _kind = KindNames[idx].kind;
                            ApplyKindToCard();
                            BuildDynamicForm();
                            BuildArtGallery(); // 画廊随卡类型换源（生物=模型目录 / 法术结界=缺省 Title）
                            Recalculate();
                        }
                    });
            }

            // ---- 六区改版（2026-10-13）：中栏 ACard 预览 / 左栏立绘画廊 / 卡名即时回写 ----
            BuildPreview();
            _costManualZone = Find("cost-manual-zone");
            _galleryContent = UiKit.FindDeep(Find("art-gallery"), "Content");
            BuildArtGallery();
            if (_nameField != null)
                _nameField.onValueChanged.AddListener(v => { _card.CardName = v; RefreshPreview(); });

            // 右栏切换式列表（效果 ↔ 卡池）
            _btnViewFx = BindButton("btn-view-effects", () => ShowRightView(false));
            _btnViewCards = BindButton("btn-view-cards", () => ShowRightView(true));
            foreach (var n in new[] { "hdr-effects", "btn-new-effect", "effect-library-zone" })
                _rightFxNodes.Add(Find(n));
            foreach (var n in new[] { "btn-new", "pool-filter", "field-search", "list-pool" })
                _rightCardNodes.Add(Find(n));
            ShowRightView(_rightShowCards);

            _dynamicForm = Find("dynamic-form");
            _payloadZone = Find("payload-zone");
            _suggested = FindText("lbl-suggested");
            _costLabel = FindText("lbl-cost");
            BindButton("btn-adopt", OnAdoptCost);
            _breakdownList = FindScroll("list-breakdown");
            BindButton("btn-new-effect", () =>
            {
                ComposerSession.BeginCardEdit(_card, -1); // -1 = 新建（保存时追加）
                Manager.Show<EffectComposerScreen>();
            });

            _effectLibraryZone = Find("effect-library-zone");
            _attachedList = FindScroll("list-attached");
            _keywordZone = Find("keyword-zone");

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
                // 对齐 EffectComposer 口径（2026-10-13）：装载中途异常会让卡池/表单静默空白——
                // 「卡池 0 张」类故障可见化，不再无声吞掉。
                UnityEngine.Debug.LogException(e);
                if (_toast != null) _toast.text = "界面装载异常（卡池/表单可能不完整）——查 Console";
            }
        }

        private void OnEnterInternal()
        {
            CorpusCache.Clear(); // 语料整体重建（RenderEffectSummary 全池重算偏贵，仅进屏/保存时清）
            EnsureCardLists();
            _kind = MapKind(_card.Supertype);
            _cardtypeDropdown?.SetIndex(KindIndex(_kind));
            BuildDynamicForm();
            BuildManualCostRow(); // 六色声明费（type 区容器；卡型无关，独立于 dynamic-form 生命周期）
            BuildPayloadZone(); // 代价栏（2026-09-23 上移卡组合层）
            BuildEffectLibraryZone();
            BuildKeywordZone();
            // 光环上移效果层（2026-09-23）：从效果合成器返回/重进时按挂载效果重算卡面箭头+光环
            _card.AggregateEffectAuras(true);
            RefreshAttached();
            Recalculate();
            RefreshPool();
            WarnStaleAttachedEffects(); // 库效果编辑返回的换代孤儿检测（过渡期兜底，消费分支落地后保留）
        }

        // 容器空值防护（CardData 字段缺省可能为 null——CSV/加载路径都各自赋过，这里统一兜底）
        private void EnsureCardLists()
        {
            _card.Keywords ??= new List<string>();
            _card.Tags ??= new List<string>();
            _card.Effects ??= new List<CardEffectData>();
        }

        // ======================================== 左栏：卡池 ========================================

        /// <summary>过滤 chips 绑定（颜色行预制体烘焙 chip-{Color}，一次接线；颜色/文案由预制体烘焙）。
        /// 2026-10-13 多选定案：像箭头盒多选——再点同色移除；「全部」=清空；类别行为 runtime 建（见 RebuildCategoryChips）。</summary>
        private void BindPoolFilter()
        {
            if (_poolFilter == null) return;
            foreach (UIColor color in Enum.GetValues(typeof(UIColor)))
            {
                var captured = color;
                var chipRt = UiKit.FindDeep(_poolFilter, $"chip-{captured}");
                var chip = chipRt != null ? chipRt.GetComponentInChildren<UnityEngine.UI.Button>(true) : null;
                if (chip == null) continue; // 缺 chip 容错跳过
                chip.onClick.AddListener(() =>
                {
                    if (captured == UIColor.All) _colorFilterSet.Clear();
                    else if (!_colorFilterSet.Remove(captured)) _colorFilterSet.Add(captured); // toggle：已在选中=移除，否则加入
                    UpdatePoolFilter();
                    RefreshPool();
                });
                _filterChips[captured] = chip;
            }
            UpdatePoolFilter();
        }

        /// <summary>激活态切换：多选语义——选中的色 chip 加描边（「全部」在未选任何色时点亮）；
        /// 类别行同式（单选）。预制体化后不重建行，只开关 Outline。</summary>
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
            SyncCategoryChipStates();
        }

        private static (Color bg, Color fg) ChipColors(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return (Rgb(150, 56, 56), Rgb(255, 235, 235));
                case UIColor.Blue: return (Rgb(52, 84, 150), Rgb(232, 240, 255));
                case UIColor.Green: return (Rgb(52, 120, 72), Rgb(232, 255, 240));
                case UIColor.Gray: return (Rgb(80, 86, 98), Rgb(232, 235, 240));
                default: return (UiStyle.BtnBg, UiStyle.TextBody); // All
            }
        }

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        private void RefreshPool()
        {
            ClearContent(_poolList.Content);
            var pool = CardCatalog.LoadPlayPool(); // 教学专用卡/衍生物不进编辑器卡池预览（2026-10-06/09 隔离）
            RebuildCategoryChips(pool);

            IEnumerable<CardData> cards = pool;
            if (_colorFilterSet.Count > 0)
                cards = cards.Where(c => CostColorsWithin(c, _colorFilterSet));
            if (!string.IsNullOrEmpty(_categoryFilter))
                cards = cards.Where(c => CardAtomShortNames(c).Contains(_categoryFilter));
            var tokens = _searchField != null ? TokenizeSearch(_searchField.text) : new List<string>();
            if (tokens.Count > 0)
                cards = cards.Where(c => MatchCorpus(CorpusOf(c), tokens));

            var sorted = CardSorter.Sort(cards).ToList();
            if (tokens.Count > 0)
                sorted = sorted.OrderByDescending(c => NameHitsAny(c, tokens)).ToList(); // 搜索态名称命中置前（LINQ 稳定排序，底层全局序保留）

            if (sorted.Count == 0)
            {
                MakePoolEmptyState(pool.Count == 0, tokens);
                return;
            }

            // 分组（2026-09-24 定案）：生物 / 法术（瞬间+结界）
            AddPoolGroup("生物", sorted.Where(c => c.Supertype == Cardtype.Creature).ToList());
            AddPoolGroup("法术（瞬间/结界）", sorted.Where(c => c.Supertype != Cardtype.Creature).ToList());
        }

        private void AddPoolGroup(string title, List<CardData> cards)
        {
            if (cards.Count == 0) return;
            var header = UiKit.Label("group", _poolList.Content, $"{title}（{cards.Count}）",
                UiStyle.HeaderSize, UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(header, fw: 1f);
            foreach (var card in cards)
                MakePoolRow(card);
        }

        /// <summary>卡池预览行：费用徽标+名称+类型/身材，次行=关键词+效果摘要（AtomText 单一来源）。</summary>
        private void MakePoolRow(CardData card)
        {
            var row = UiKit.Column("row", _poolList.Content, spacing: 2f, pad: 6f);
            var bg = UiKit.BgRow(row);
            bool selected = !string.IsNullOrEmpty(_editingOriginalId) && card.ID == _editingOriginalId;
            if (selected) bg.color = UiStyle.SelectedRowBg;
            bg.raycastTarget = true;
            UiKit.Size(row, fw: 1f);

            var top = UiKit.Row("top", row, spacing: 8f);
            CostBadge("cost", top, ((int)card.TotalCost).ToString());
            var name = UiKit.Label("name", top,
                string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName,
                UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            UiKit.Label("meta", top, TypeStatLine(card), UiStyle.SmallSize, UiStyle.TextFaint);

            var keywords = (card.Keywords ?? new List<string>())
                .Select(k => KeywordZh(k)).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var summaries = (card.Effects ?? new List<CardEffectData>())
                .Select(fx => AtomText.RenderEffectSummary(
                    new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, card))
                .Where(s => !string.IsNullOrEmpty(s)).ToList();
            var detailParts = new List<string>();
            if (keywords.Count > 0) detailParts.Add(string.Join("·", keywords));
            if (summaries.Count > 0) detailParts.Add(string.Join("｜", summaries));
            if (detailParts.Count > 0)
            {
                var detail = UiKit.Label("detail", row, string.Join("｜", detailParts),
                    UiStyle.SmallSize, UiStyle.TextFaint, wrap: true);
                UiKit.Size(detail, fw: 1f);
            }

            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => LoadCardForEdit(card));
        }

        private static string TypeStatLine(CardData card)
        {
            switch (card.Supertype)
            {
                case Cardtype.Creature:
                    return $"生物 {(card.Power ?? 0)}/{(card.Life ?? 0)}";
                case Cardtype.Enchantment:
                    return card.Durability > 0 ? $"结界 耐久{card.Durability}" : "结界";
                default:
                    return CardSorter.TypeName(card);
            }
        }

        private static string KeywordZh(string keywordId)
        {
            var def = CardLoader.GetKeywordDefinition(keywordId);
            return string.IsNullOrEmpty(def?.nameZh) ? keywordId : def.nameZh;
        }

        // ======================================== 检索与分面（2026-10-13 交接稿：多词AND·语料并集·类别chips） ========================================

        // SearchQuery 助手规划落 CardSorter.cs（并行会话域，本会话勿动）——落地后以同语义替换
        // 以下 TokenizeSearch/MatchCorpus/NameHitsAny 即可；语义即契约：空白切词、多词 AND
        //（非连续子串）、OrdinalIgnoreCase、无词不过滤。
        private static List<string> TokenizeSearch(string query) =>
            string.IsNullOrWhiteSpace(query)
                ? new List<string>()
                : query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();

        private static bool MatchCorpus(string corpus, List<string> tokens) =>
            tokens.All(t => corpus.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool NameHitsAny(CardData card, List<string> tokens) =>
            tokens.Any(t => (card.CardName ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>卡级语料（交接稿字段表）：卡名∪ID∪卡型中文∪关键词(中文+原始id)∪Tags∪效果全文
        ///（逐效果 AtomText 摘要，与 MakePoolRow 显示同源）∪原子中文短名（补收——保证「点类别词=搜该词」
        /// 同语义：渲染全文是代入数值后的模板句，短名词只在此列稳定命中）。缓存按卡 ID（内容寻址）。</summary>
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

        /// <summary>卡内效果原子的表行中文短名集合（config.DisplayName——与效果组合界面分类下拉同源；
        /// 含分支 then 奖励原子，ProjectLinear 同口径展平）。类别 chips 词表与过滤共用。</summary>
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

        /// <summary>颜色多选子集语义（2026-10-13 定案）：卡费用所用色集 ⊆ 选中集——
        /// 选红+灰 → 纯红/纯灰/红灰混合入选、红蓝卡出局；0 费卡仅在未选任何色时显示（所用色集为空不匹配）。</summary>
        private static bool CostColorsWithin(CardData card, HashSet<UIColor> selected)
        {
            if (card == null || card.Cost == null || card.Cost.IsZero) return false;
            foreach (var color in FilterColorOrder)
                if (card.Cost[ManaOf(color)] > 0f && !selected.Contains(color)) return false;
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

        // ---- 效果类别 chips（交接稿：颜色行下方 runtime 建行，单选+「全部」复位） ----

        private const string CategoryAllKey = "\0all";

        /// <summary>类别行随池词表重建（签名比对防抖）。行节点绝对定位于 pool-filter 内颜色行下方
        ///（颜色行 y∈[-42,-2]，类别行占 y∈[-88,-46]——prefab 已扩高 44→92 腾位）。</summary>
        private void RebuildCategoryChips(List<CardData> pool)
        {
            var vocab = new SortedSet<string>();
            foreach (var card in pool)
                foreach (var name in CardAtomShortNames(card))
                    vocab.Add(name);
            string sig = string.Join("\u0001", vocab);
            if (_categoryChipRow != null && sig == _categoryChipSignature)
            {
                SyncCategoryChipStates();
                return;
            }
            _categoryChipSignature = sig;
            if (_categoryFilter != null && !vocab.Contains(_categoryFilter)) _categoryFilter = null; // 词表换代失效复位
            if (_categoryChipRow != null)
            {
                _categoryChipRow.SetParent(null);
                UnityEngine.Object.Destroy(_categoryChipRow.gameObject);
            }
            _categoryChips.Clear();
            if (_poolFilter == null) return;

            var rowGo = new GameObject("category-chips", typeof(RectTransform));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(_poolFilter, false);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(0f, -46f);
            row.sizeDelta = new Vector2(_poolFilter.rect.width, 40f); // 容器宽（右栏化后随 549 收缩）
            var lg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            lg.spacing = 4f;
            lg.childControlWidth = true;
            lg.childControlHeight = true;
            lg.childForceExpandWidth = false;
            lg.childForceExpandHeight = false;
            _categoryChipRow = row;

            MakeCategoryChip(row, "全部", CategoryAllKey);
            foreach (var word in vocab)
                MakeCategoryChip(row, word, word);
            SyncCategoryChipStates();
        }

        private void MakeCategoryChip(RectTransform parent, string label, string key)
        {
            var captured = key;
            var chip = TplClone("tpl-chip", parent); // CardUI 自身 chip-All 克隆——与颜色行视觉同源
            if (chip == null) return;
            SetTplText(chip, label);
            var btn = chip.GetComponent<Button>();
            if (btn == null) return;
            btn.onClick.AddListener(() =>
            {
                _categoryFilter = captured == CategoryAllKey || captured == _categoryFilter ? null : captured; // 单选，再点取消
                SyncCategoryChipStates();
                RefreshPool();
            });
            _categoryChips[captured] = btn;
        }

        private void SyncCategoryChipStates()
        {
            foreach (var kv in _categoryChips)
            {
                bool active = kv.Key == CategoryAllKey ? _categoryFilter == null : kv.Key == _categoryFilter;
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

        /// <summary>空态可解释（交接稿配套）：库空 vs 无匹配分开说；无匹配时列出当前生效筛选 +「清除全部」——
        /// 新手卡死的最大原因是不知道什么在约束列表。</summary>
        private void MakePoolEmptyState(bool poolEmpty, List<string> tokens)
        {
            var col = UiKit.Column("empty", _poolList.Content, spacing: 6f, pad: 8f);
            if (poolEmpty)
            {
                var l1 = UiKit.Label("t", col,
                    "卡池为空（0 张）——检查 StreamingAssets/Card/Cards.json 与 Console 的 [CardCatalog]/[CardLoader] 警告",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(l1, fw: 1f);
                return;
            }
            var constraints = new List<string>();
            if (_colorFilterSet.Count > 0)
                constraints.Add("色：" + string.Join("/", _colorFilterSet.Select(c => c.ToString())));
            if (!string.IsNullOrEmpty(_categoryFilter))
                constraints.Add("类：" + _categoryFilter);
            if (tokens.Count > 0)
                constraints.Add("词：" + string.Join(" ", tokens));
            var label = UiKit.Label("t", col,
                constraints.Count == 0 ? "（无卡牌可显示）" : "（无匹配卡牌）当前筛选：" + string.Join(" ＋ ", constraints),
                UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(label, fw: 1f);
            if (constraints.Count > 0)
            {
                TplButton(col, "清除全部", () =>
                {
                    _colorFilterSet.Clear();
                    _categoryFilter = null;
                    if (_searchField != null) _searchField.SetTextWithoutNotify("");
                    UpdatePoolFilter();
                    RefreshPool();
                }, width: 96f);
            }
        }

        // ======================================== 载入编辑 / 新建 ========================================

        /// <summary>载入已有卡编辑（2026-09-24 定案：覆盖替换语义）。
        /// 深拷贝容器、共享效果实例——效果合成器写回=整实例替换（EffectComposerScreen）、
        /// 入口即深拷贝，不污染卡池缓存实例；保存前池中原件不动。</summary>
        private void LoadCardForEdit(CardData src)
        {
            _card.CardName = src.CardName;
            _card.Supertype = src.Supertype;
            _card.Illustration = src.Illustration; // 立绘（2026-10-13 六区改版：Cards.json illustration 列往返）
            _card.Power = src.Power;
            _card.Life = src.Life;
            _card.Durability = src.Durability;
            _card.Keywords = src.Keywords != null ? new List<string>(src.Keywords) : new List<string>();
            _card.Tags = src.Tags != null ? new List<string>(src.Tags) : new List<string>();
            _card.Effects = src.Effects != null ? new List<CardEffectData>(src.Effects) : new List<CardEffectData>();
            _card.Cost = src.Cost != null ? src.Cost.Clone() : null;
            _card.PayloadCost = src.PayloadCost; // 单条 Payload：填装走整体替换
            _card.ArrowDirections = src.ArrowDirections;
            _card.LinkAuras = src.LinkAuras != null ? new List<LinkAuraData>(src.LinkAuras) : null;
            _card.NoAttack = src.NoAttack;
            _card.NoGuard = src.NoGuard;
            _card.SpeedBonus = src.SpeedBonus;
            _card.ResetCache();
            _card.AggregateEffectAuras(true);
            _editingOriginalId = src.ID;
            _kind = MapKind(src.Supertype);
            _selectedSlot = -1; // 槽位选中态随载入复位（追加态）

            _nameField.SetTextWithoutNotify(_card.CardName ?? "");
            _tagsField.SetTextWithoutNotify(string.Join(",", _card.Tags ?? new List<string>()));
            _cardtypeDropdown?.SetIndex(KindIndex(_kind));
            RebuildCardPanels();
            ShowToast($"编辑：{_card.CardName}（保存将覆盖替换旧卡）");
        }

        private void OnNewCard()
        {
            _card.CardName = "新卡牌";
            _card.Supertype = Cardtype.Creature;
            _card.Illustration = null; // 立绘复位（画廊高亮随 RefreshPreview 对账）
            _card.Power = 0;
            _card.Life = 0;
            _card.Durability = 0;
            _card.Keywords = new List<string>();
            _card.Tags = new List<string>();
            _card.Effects = new List<CardEffectData>();
            _card.Cost = null;
            _card.PayloadCost = null;
            _card.ArrowDirections = default;
            _card.LinkAuras = null;
            _card.NoAttack = _card.NoGuard = false; // SurplusToDurability 已删（2026-10-15 拆分定案）
            _card.SpeedBonus = 0;
            _card.ResetCache();
            _editingOriginalId = null;
            _kind = CardKind.Creature;
            _selectedSlot = -1;

            _nameField.SetTextWithoutNotify("新卡牌");
            _tagsField.SetTextWithoutNotify("");
            _cardtypeDropdown?.SetIndex(KindIndex(_kind));
            RebuildCardPanels();
            ShowToast("已新建空白卡牌");
        }

        private void RebuildCardPanels()
        {
            BuildDynamicForm();
            BuildManualCostRow(); // 声明费初值随载入/新建刷新（type 区独立容器）
            BuildArtGallery(); // 画廊高亮随载入/新建对账（类型换源后的统一收口）
            BuildPayloadZone();
            BuildKeywordZone();
            _card.AggregateEffectAuras(true);
            RefreshAttached();
            Recalculate();
            RefreshPool();
        }

        // ======================================== 中栏：表单 ========================================

        private static int KindIndex(CardKind kind)
        {
            for (int i = 0; i < KindNames.Length; i++)
                if (KindNames[i].kind == kind) return i;
            return 0;
        }

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
        // 立绘随类型：生物=模型名（画廊点选）；法术/结界=无合适立绘，缺省空白不放图
        //（2026-10-15 定案——不写 illustration，ACardView 侧清 sprite；画廊无选项）。
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
                    // 2026-10-15 改版：速度=3−槽数动态算（槽位从速度扣：1槽=速2/2槽=速1/3槽=速0），
                    // SpeedBonus 字段退役恒 0；出牌费不含效果锚价（发动现付所选效果锚价）。
                    _card.SpeedBonus = 0;
                    _card.LinkAuras = new List<LinkAuraData>(); // 法术只挂主动效果
                    _card.ArrowDirections = HexDirection.None;
                    break;
                case CardKind.Enchantment:
                    _card.Supertype = Cardtype.Enchantment;
                    _card.Power = null;
                    _card.Life = null; // 结界=耐久体（2026-09-24 定案），不用攻/血
                    _card.SpeedBonus = 0; // 速度通道退役（2026-10-15 改版：结界禁主动，免费点全投耐久）
                    break;
            }
        }

        private bool HasStats => _kind == CardKind.Creature;
        private bool HasDurability => _kind == CardKind.Enchantment;

        // ---------- 动态表单（按类型切换字段；2026-10-13 属性全量开控件定案） ----------
        private void BuildDynamicForm()
        {
            ClearChildren(_dynamicForm);

            // 攻 / 血 + 战斗底盘 opt-out（生物）
            if (HasStats)
            {
                var statRow = UiKit.Row("stats", _dynamicForm, spacing: 8f);
                MakeLabeledInt(statRow, "攻击", _card.Power ?? 0, v => { _card.Power = v; Recalculate(); });
                MakeLabeledInt(statRow, "生命", _card.Life ?? 0, v => { _card.Life = v; Recalculate(); });
                TplToggle(statRow, "不可攻击", _card.NoAttack, v => { _card.NoAttack = v; Recalculate(); });
                TplToggle(statRow, "不可守护", _card.NoGuard, v => { _card.NoGuard = v; Recalculate(); });
            }

            // 耐久（结界；耐久体定案 2026-09-24）——「剩余转耐久」开关已随 2026-10-15 拆分定案退役：
            // 结界禁主动效果后免费点全投耐久（DurabilityBonusOf=剩余，装/卸效果即时重算）。
            if (HasDurability)
            {
                var durRow = UiKit.Row("durability", _dynamicForm, spacing: 8f);
                MakeLabeledInt(durRow, "耐久", _card.Durability, v => { _card.Durability = v; Recalculate(); });
            }

            // （提速份额控件已随 2026-10-15 改版退役：法术速度=3−槽数动态算，不可手调）

            // 本体光环=结界专属（生物禁挂 2026-10-15 改版；法术只挂主动效果）
            if (_kind == CardKind.Enchantment)
                BuildLinkAuraZone();
        }

        // 带标签的整数输入（横排；输入框=EffectUI 同款模板克隆）。
        private void MakeLabeledInt(RectTransform parent, string label, int value, Action<int> onChanged)
        {
            var row = UiKit.Row("field", parent, spacing: 6f);
            UiKit.Label("label", row, label, UiStyle.SmallSize, UiStyle.TextDim);
            TplInt(row, label, value, onChanged, width: 70f);
        }

        // ---------- 声明费六色手动行（2026-10-13 补全属性；六区改版：type 区独立容器） ----------
        // 下标=ManaType 枚举序[灰,红,蓝,绿,白,黑]（ElementCost.v 直写）；与建议档位采纳共存——
        // 手改即整组写入分布；小数份额来自「采纳建议」（手动档只写整数，计价小数直通口径不受影响）。
        private void BuildManualCostRow()
        {
            var host = _costManualZone ?? _dynamicForm; // 容器缺省回落 dynamic-form（prefab 未升级兜底）
            if (host == null) return;
            ClearChildren(host);
            var row = UiKit.Row("cost-manual", host, spacing: 6f);
            for (int i = 0; i < ElementCost.Length; i++)
            {
                var mana = (ManaType)i;
                var captured = mana;
                int cur = _card.Cost != null ? (int)_card.Cost[mana] : 0;
                TplInt(row, ManaTypeNames.ZhNameOf(mana), cur, v =>
                {
                    _card.Cost ??= new ElementCost();
                    _card.Cost.v[(int)captured] = Math.Max(0, v);
                    _card.ResetCache();
                    RefreshCostLabel();
                    Recalculate();
                }, width: 52f);
            }
        }

        // ---------- 本体光环 LinkAuras（2026-10-13 补全属性；数据结构与效果层光环同源 LinkAuraData） ----------
        // stat 条目：Power/Life/Both ± value；关键词条目：keyword id（值无语义）；scope：0=随卡面箭头/1己方/2双方/3对方。
        private static readonly string[] AuraScopeNames = { "箭头", "己方", "双方", "对方" };

        private void BuildLinkAuraZone()
        {
            var hdr = UiKit.Label("aura-hdr", _dynamicForm, "本体光环（属性/关键词 · 作用面）",
                UiStyle.HeaderSize, UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(hdr, fw: 1f);
            _card.LinkAuras ??= new List<LinkAuraData>();

            for (int i = 0; i < _card.LinkAuras.Count; i++)
                MakeLinkAuraRow(_card.LinkAuras[i]);

            // 添加行：条目（三属性档 + 关键词目录）＋作用面
            var keywords = KeywordCatalog.LoadAll();
            var kinds = new List<string> { "攻击修正", "生命修正", "双重修正" };
            kinds.AddRange(keywords.Select(k => "关键词·" + k.DisplayName));
            int kindIdx = 0, scopeIdx = 0;
            var row = UiKit.Row("aura-add", _dynamicForm, spacing: 6f);
            TplDropdown(row, kinds, 0, (idx, _) => kindIdx = idx, width: 150f);
            TplDropdown(row, new List<string>(AuraScopeNames), 0, (idx, _) => scopeIdx = idx, width: 80f);
            TplButton(row, "添加光环", () =>
            {
                var aura = new LinkAuraData { scope = scopeIdx };
                if (kindIdx <= 2)
                {
                    aura.stat = kindIdx == 0 ? "Power" : kindIdx == 1 ? "Life" : "Both";
                    aura.value = 1;
                }
                else
                {
                    var kw = keywords[kindIdx - 3];
                    if (kw == null || string.IsNullOrEmpty(kw.Id)) return;
                    aura.keyword = kw.Id;
                }
                _card.LinkAuras.Add(aura);
                _card.ResetCache();
                BuildDynamicForm(); // 行增删整建（小表单可接受；值编辑不重建防输入焦点丢失）
                Recalculate();
            });
        }

        private void MakeLinkAuraRow(LinkAuraData aura)
        {
            var captured = aura;
            var row = UiKit.Row("aura-row", _dynamicForm, spacing: 6f);
            var lbl = UiKit.Label("name", row, LinkAuraSummary(captured), UiStyle.SmallSize, UiStyle.TextBody);
            UiKit.Size(lbl, fw: 1f);
            if (!string.IsNullOrEmpty(captured.stat))
                TplInt(row, "值", captured.value,
                    v => { captured.value = v; _card.ResetCache(); Recalculate(); }, width: 46f);
            TplDropdown(row, new List<string>(AuraScopeNames),
                Mathf.Clamp(captured.scope, 0, 3),
                (sel, _) => { captured.scope = sel; _card.ResetCache(); Recalculate(); }, width: 80f);
            TplButton(row, "移除", () =>
            {
                _card.LinkAuras.Remove(captured);
                _card.ResetCache();
                BuildDynamicForm();
                Recalculate();
            });
        }

        private static string LinkAuraSummary(LinkAuraData aura)
        {
            string what;
            if (string.IsNullOrEmpty(aura.stat))
                what = "关键词·" + KeywordZh(aura.keyword);
            else
            {
                var statZh = aura.stat == "Power" ? "攻击" : aura.stat == "Life" ? "生命" : "双重";
                what = statZh + (aura.value >= 0 ? "+" : "") + aura.value;
            }
            return what + "·" + AuraScopeNames[Mathf.Clamp(aura.scope, 0, 3)];
        }

        // ---------- 代价栏（2026-09-23 定案：上移卡组合层；2026-10-04 规则改造 + 本轮全套口径）----------
        // 单卡单条 Payload：任意单向效果不限价——填装时逆转选择范围到错误一侧
        //（表域双侧收窄 / 正确侧单向镜像 / 恰为错误侧免写，统一口 CostDerivationService.PayloadCostDomain）；
        // 代价也是效果栏：占 1 效果槽（底盘 3 灰同口径）；全价不并入卡费——使用时单独过地牌门槛
        //（全价>地牌槽上限 → 整卡不可用）；打出时先扣卡费再强制执行，生效后按执行前快照全价补偿黑/白。

        private void BuildPayloadZone()
        {
            ClearChildren(_payloadZone);
            var header = UiKit.Label("header", _payloadZone, "代价（逆转错侧·单卡单条·占1效果槽）",
                UiStyle.HeaderSize, UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(header, fw: 1f);

            var pe = _card.PayloadCost?.payload;
            if (pe != null && !string.IsNullOrEmpty(pe.refId))
            {
                var row = UiKit.Row("payload-row", _payloadZone, spacing: 8f);
                var name = UiKit.Label("name", row, AtomText.RenderAtomEntry(pe),
                    UiStyle.BodySize, UiStyle.TextBody, wrap: true);
                UiKit.Size(name, fw: 1f);
                var price = UiKit.Label("price", row, PayloadPriceText(pe),
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                TplButton(row, "移除", () =>
                {
                    _card.PayloadCost = null;
                    _card.ResetCache();
                    BuildPayloadZone();
                    Recalculate();
                });
                return;
            }

            // 未填装：候选下拉（与 FillPayloadCost 同口径的表级过滤——PayloadCostDomain 可入，不限价）
            var rows = PayloadCandidates().ToList();
            var choices = new List<string> { "（选择代价原子）" };
            choices.AddRange(rows.Select(r => r.DisplayName));
            TplDropdown(_payloadZone, choices, 0, (idx, _) =>
            {
                int i = idx - 1;
                if (i >= 0 && i < rows.Count)
                    FillPayloadCost(new AtomicEffectEntry { refId = rows[i].HashId, value = 1 });
            }, width: 220f);
        }

        /// <summary>填装代价栏（2026-10-04 定案：任意单向效果·逆转选择范围·不限价）。
        /// 有效域统一走 CostDerivationService.PayloadCostDomain：p≠0 强制错误侧——表域双侧收窄、
        /// 正确侧单向**镜像逆转**（MirrorDomain）、恰为错误侧免写；p=0 须表域单侧锁定。
        /// 不再限价（09-21「等价1」退役）——全价不并入卡费：占 1 效果槽（底盘同口径），
        /// 使用时单独过地牌门槛（全价>上限整卡不可用），生效后按快照全价补偿黑/白（产出不封）。</summary>
        private void FillPayloadCost(AtomicEffectEntry entry)
        {
            var cfg = AtomicEffectTable.GetByHashId(entry?.refId);
            if (cfg == null || !Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type))
            {
                ShowToast("原子表引用缺失——无法作代价");
                return;
            }
            if (ComposerCatalog.HasMountBit(cfg, MountKind.SystemInternal))
            { ShowToast("系统内部原子（数值修改原语）不可作代价"); return; }
            if (ComposerCatalog.HasMountBit(cfg, MountKind.Keyword))
            { ShowToast("关键词类原子不可作代价（代价位不出现关键词）"); return; }

            var (kinds, eligible) = CostDerivationService.PayloadCostDomain(cfg);
            if (!eligible)
            {
                ShowToast("该原子无法逆转到错误一侧（纯自身域有益 / 中性双侧域）——不可作代价");
                return;
            }
            entry.kinds = kinds; // null=免写（表默认同效）；镜像域可超出表行域——逆转语义

            _card.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = entry };
            _card.ResetCache();
            BuildPayloadZone();
            Recalculate();
            var inst = CardEffectConverter.ConvertPayloadForDisplay(entry);
            int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            ShowToast($"已填装代价（全价 {price}·逆转错侧·占1效果槽）——打出时先扣卡费再强制执行，生效后按全价得黑/白；全价超过地牌槽上限时整卡不可用");
        }

        /// <summary>代价候选行（表级，2026-10-04 新准入）：非关键词/非系统内部 + PayloadCostDomain 可入
        ///（p≠0 双侧收窄或正确侧镜像逆转 / p=0 单侧锁）——不限价，下拉只列真正可装的原子。</summary>
        private static IEnumerable<AtomicEffectConfig> PayloadCandidates()
        {
            foreach (var row in AtomicEffectTable.GetAll())
            {
                if (row == null || string.IsNullOrEmpty(row.EnumName)) continue;
                if (!Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) continue;
                if (ComposerCatalog.HasMountBit(row, MountKind.Keyword)) continue;
                if (ComposerCatalog.HasMountBit(row, MountKind.SystemInternal)) continue;

                if (!CostDerivationService.PayloadCostDomain(row).eligible) continue;
                yield return row;
            }
        }

        /// <summary>代价全价展示行（口径同装载期 PayloadUnitGrant；全价作地牌门槛与补偿基准，不并入卡费）。</summary>
        private static string PayloadPriceText(AtomicEffectEntry entry)
        {
            var inst = CardEffectConverter.ConvertPayloadForDisplay(entry);
            int price = inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            return $"全价：{price}（过地牌门槛·生效后补偿黑/白·占1效果槽）";
        }

        // ======================================== 六区改版（2026-10-13）：中栏 ACard 预览 / 左栏立绘画廊 / 右栏切换 ========================================

        /// <summary>中栏卡面预览装配：实例化 ACard.prefab + ACardView（静态 Full——
        /// FromCardDataFull 无 RuntimeCard，不订阅事件、无 stats 芯片）；缩放 1.35 顶对齐。</summary>
        private void BuildPreview()
        {
            var slot = Find("preview-zone");
            if (slot == null)
            {
                Debug.LogError("[CardComposer] 缺 preview-zone（CardUI.prefab 中栏）——卡面预览跳过");
                return;
            }
            var prefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>("ACard", UiKit.PrefabPath("ACard"));
            if (prefab == null)
            {
                Debug.LogError("[CardComposer] ACard.prefab 缺失：" + UiKit.PrefabPath("ACard") + "——卡面预览跳过");
                return;
            }
            var go = UnityEngine.Object.Instantiate(prefab.gameObject, slot, false);
            go.name = "ACard";
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -10f);
            rt.localScale = Vector3.one * 1.35f;
            _previewView = go.GetComponent<ACardView>() ?? go.AddComponent<ACardView>();
            _previewView.SetFaceUp(true);
            _artCurLabel = FindText("lbl-art-current");
        }

        /// <summary>预览重绘：全部 _card 变更经 Recalculate 汇入；卡名/立绘即时回写处直调。
        /// 立绘指示（type 区）与左栏画廊高亮同步对账。</summary>
        private void RefreshPreview()
        {
            if (_previewView != null)
                _previewView.Apply(CardOverlayItem.FromCardDataFull(_card, "compose"), CardOverlayLayout.Full);
            if (_artCurLabel != null)
                _artCurLabel.text = _kind == CardKind.Creature
                    ? "立绘：" + (string.IsNullOrEmpty(_card.Illustration)
                        ? "（占位——左栏点选）" : _card.Illustration)
                    : "立绘：（无——法术/结界缺省空白）";
            HighlightArtTile();
        }

        // ---- 左栏美术画廊（美术可选项未来扩展：入场特效等资源列表在本栏目追加分区） ----
        // 2026-10-13 立绘随卡类型定案：生物=YGOMonstersRig 模型名（卡面立绘+战场 3D 同名双源）；
        // 2026-10-15 法术/结界=无合适立绘（缺省空白不放图）——画廊无选项。

        private void BuildArtGallery()
        {
            if (_galleryContent == null)
            {
                Debug.LogError("[CardComposer] 缺 art-gallery/Content（CardUI.prefab 左栏）——立绘画廊跳过");
                return;
            }
            ClearChildren(_galleryContent);
            _artTiles.Clear();
            if (_kind == CardKind.Creature)
            {
                MakeArtTile(null); // 占位项（清空立绘=预制体占位图，ACardView 同口径）
                foreach (var name in CreatureArtNames())
                    MakeArtTile(name);
            }
            HighlightArtTile();
        }

        /// <summary>生物立绘可选名（=模型目录名，与战场 MonsterModelResolver 同源根目录）。
        /// 编辑器直读 YGOMonstersRig 目录且要求存在同名预览图（无图不可选——渲染侧同名找图的硬前提）。</summary>
        private static IEnumerable<string> CreatureArtNames()
        {
#if UNITY_EDITOR
            var root = System.IO.Path.Combine(Application.dataPath, "Art/YGOMonstersRig");
            if (!System.IO.Directory.Exists(root)) yield break;
            var names = new List<string>();
            foreach (var dir in System.IO.Directory.GetDirectories(root))
            {
                var n = System.IO.Path.GetFileName(dir);
                if (!System.IO.File.Exists(System.IO.Path.Combine(dir, n + ".png"))) continue;
                names.Add(n);
            }
            names.Sort(System.StringComparer.Ordinal);
            foreach (var n in names) yield return n;
#endif
        }

        private void MakeArtTile(string spriteName)
        {
            var tile = UiKit.Column("tile", _galleryContent, spacing: 4f, pad: 6f);
            var bg = tile.gameObject.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.19f, 0.22f, 0.8f);
            bg.raycastTarget = true;

            var thumb = UiKit.Node("thumb", tile);
            var img = thumb.gameObject.AddComponent<Image>();
            if (string.IsNullOrEmpty(spriteName))
            {
                var hint = UiKit.Label("hint", thumb, "占位", UiStyle.BodySize, UiStyle.TextHint,
                    TextAnchor.MiddleCenter);
                UiKit.StretchInset(hint.rectTransform, 2f, 0f);
            }
            else
            {
                var sp = CardArtLibrary.Load(spriteName); // 按名称两级回落：Sprites 单图 → 模型预览子精灵
                if (sp != null) { img.sprite = sp; img.preserveAspect = true; }
                else img.color = new Color(0f, 0f, 0f, 0.25f);
            }
            UiKit.Size(thumb, w: 198f, h: 200f);
            var label = UiKit.Label("label", tile, string.IsNullOrEmpty(spriteName) ? "（占位）" : spriteName,
                UiStyle.SmallSize, UiStyle.TextBody, wrap: true);
            UiKit.Size(label, fw: 1f);

            var captured = spriteName;
            var btn = tile.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() =>
            {
                Debug.Log("[UI点击] 立绘 " + (string.IsNullOrEmpty(captured) ? "占位" : captured));
                _card.Illustration = string.IsNullOrEmpty(captured) ? null : captured;
                RefreshPreview();
                ShowToast(string.IsNullOrEmpty(captured) ? "立绘已清空（占位图）" : "立绘已选：" + captured);
            });
            _artTiles[spriteName ?? ""] = tile;
        }

        /// <summary>画廊选中高亮（Outline）——与当前 _card.Illustration 对账，幂等。</summary>
        private void HighlightArtTile()
        {
            var cur = _card.Illustration ?? "";
            foreach (var kv in _artTiles)
            {
                bool active = kv.Key == cur;
                var ol = kv.Value.GetComponent<Outline>();
                if (active && ol == null)
                {
                    ol = kv.Value.gameObject.AddComponent<Outline>();
                    ol.effectColor = UiStyle.TextPrimary;
                    ol.effectDistance = new Vector2(2f, -2f);
                }
                if (ol != null) ol.enabled = active;
            }
        }

        // ---- 右栏切换式列表（效果 ↔ 卡池；效果行点选=填入中间选中槽，卡行点击=载入编辑） ----

        private void ShowRightView(bool cards)
        {
            _rightShowCards = cards;
            foreach (var n in _rightFxNodes)
                if (n != null) n.gameObject.SetActive(!cards);
            foreach (var n in _rightCardNodes)
                if (n != null) n.gameObject.SetActive(cards);
            foreach (var b in new[] { _btnViewFx, _btnViewCards })
            {
                if (b == null) continue;
                bool on = (b == _btnViewFx) != cards; // 效果钮点亮=当前非卡视图
                var ol = b.GetComponent<Outline>();
                if (on && ol == null)
                {
                    ol = b.gameObject.AddComponent<Outline>();
                    ol.effectColor = UiStyle.TextPrimary;
                    ol.effectDistance = new Vector2(1.5f, -1.5f);
                }
                if (ol != null) ol.enabled = on;
            }
        }

        // ======================================== 右栏：效果挂载 ========================================

        /// <summary>效果库列表（2026-10-13 列表化改版：下拉收起改为可见滚动列表，行=名称+AtomText 摘要+添加/编辑）。
        /// 添加=快照挂载（原下拉能力不变）；编辑=全局编辑库效果（定案：保存改 Effects.json，引用该效果的所有卡随之变）。</summary>
        private void BuildEffectLibraryZone()
        {
            ClearChildren(_effectLibraryZone);
            var graphs = EffectLibrarySerializer.LoadAll();
            var hdr = UiKit.Label("hdr", _effectLibraryZone,
                $"效果库（{graphs.Count}）——添加挂载 / 编辑改库", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(hdr, fw: 1f);
            var scroll = UiKit.ScrollColumn("lib-list", _effectLibraryZone);
            UiKit.Size(scroll.Rect, fw: 1f, h: 640f);
            if (graphs.Count == 0)
            {
                var hint = UiKit.Label("hint", scroll.Content,
                    "效果库为空——「新建效果」或在效果合成器保存即入库",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(hint, fw: 1f);
                return;
            }
            foreach (var graph in graphs)
                MakeLibraryEffectRow(scroll.Content, graph);
        }

        /// <summary>效果库行（2026-10-13 晚：行=EffectUI 效果表行模板克隆——名称+费用方格+整行点击；
        /// 行点击/「添加」=快照挂载，「编辑」=全局编辑库效果）。</summary>
        private void MakeLibraryEffectRow(RectTransform parent, EffectGraphData graph)
        {
            var captured = graph;
            var name = string.IsNullOrEmpty(captured.name) ? "(未命名)" : captured.name;
            var row = UiKit.Column("row", parent, spacing: 2f, pad: 6f);
            UiKit.BgRow(row);
            UiKit.Size(row, fw: 1f);
            var top = UiKit.Row("top", row, spacing: 6f);
            var libRow = TplLibRow(top, name, () => FillSelectedSlot(captured)); // 行点击=填入中间选中槽（槽位制）
            if (libRow == null)
            {
                var lbl = UiKit.Label("name", top, name, UiStyle.BodySize, UiStyle.TextBody);
                UiKit.Size(lbl, fw: 1f);
            }
            TplButton(top, "填入", () => FillSelectedSlot(captured));
            TplButton(top, "编辑", () => OnEditLibraryEffect(captured));
            var summary = UiKit.Label("summary", row, AtomText.RenderEffectSummary(captured),
                UiStyle.SmallSize, UiStyle.TextFaint, wrap: true);
            UiKit.Size(summary, fw: 1f);
        }

        /// <summary>全局编辑库效果（2026-10-13 定案）：跳效果合成器改 Effects.json。暂存卡组合界面
        /// =屏实例按 Type 缓存天然保留 _card（效果合成器往返期间保留，既有口径）。当前卡已挂载快照
        /// 若因改库换代（内容哈希换 id），返回时孤儿检测提示重挂（WarnStaleAttachedEffects）。</summary>
        private void OnEditLibraryEffect(EffectGraphData graph)
        {
            if (string.IsNullOrEmpty(graph?.id))
            {
                ShowToast("该效果尚未入库（无 id）——先在效果合成器保存");
                return;
            }
            ComposerSession.BeginLibraryEffectEdit(graph.id);
            Manager.Show<EffectComposerScreen>();
            // 过渡期（EffectComposerScreen 消费分支未接入，该文件并行会话占用中）：自动载入暂缺——
            // Console 提示手动路径；消费分支落地后此跳转为「载入该效果+保存后自动返回」。
            UnityEngine.Debug.Log($"[CardComposer] 库效果编辑跳转（过渡期）：{graph.id} {graph.name}"
                + "——在效果合成器右栏「切换效果表」后点击该效果载入编辑");
        }

        /// <summary>挂载快照孤儿检测（过渡期兜底，消费分支落地后保留）：快照 id 非空且库中已无此 id
        /// → 该库效果已被编辑换代（内容哈希换 id）或删除——提示重挂取新版。</summary>
        private void WarnStaleAttachedEffects()
        {
            if (_card.Effects == null || _card.Effects.Count == 0) return;
            var libIds = new HashSet<string>(EffectLibrarySerializer.LoadAll()
                .Where(g => !string.IsNullOrEmpty(g.id)).Select(g => g.id));
            foreach (var fx in _card.Effects)
            {
                if (fx != null && !string.IsNullOrEmpty(fx.Id) && !libIds.Contains(fx.Id))
                {
                    ShowToast($"效果「{fx.DisplayName}」的库版本已更新/移除——移除后重新挂载以取新版");
                    return;
                }
            }
        }

        /// <summary>右栏效果列表点选 → 填入中间选中槽（槽位制 2026-10-13，参考效果组合界面交互）：
        /// 选中已挂槽=整实例替换；空槽（默认 -1）=追加。首个主动效果上 ACard effect 区
        ///（FromCardDataFull 拆分）——挂载顺序即显示顺序，槽行 ↑↓ 可调。
        /// 形状闸（2026-10-15 深夜改版）：生物/法术禁挂连接光环；法术只收主动、结界禁主动。</summary>
        private void FillSelectedSlot(EffectGraphData graph)
        {
            // 光环形态闸：连接光环=结界专属（生物禁挂改版；法术只挂主动效果）
            if ((_kind == CardKind.Creature || _kind == CardKind.Spell)
                && EffectComposerScreen.InferMode(graph) == EffectComposerScreen.ComposeMode.Aura)
            {
                ShowToast(_kind == CardKind.Creature
                    ? "生物不可挂连接光环（2026-10-15 改版——光环专属结界）"
                    : "法术只挂主动效果（施放时抉择其一），不可挂光环");
                return;
            }
            // 时机形态闸：法术只收主动（Active/Instant）；结界禁一切启动式
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
            var snap = SnapshotEffect(graph);
            if (_selectedSlot >= 0 && _selectedSlot < _card.Effects.Count)
            {
                _card.Effects[_selectedSlot] = snap;
                ShowToast($"已替换槽位 {_selectedSlot + 1}：{snap.DisplayName ?? "效果"}");
            }
            else
            {
                _card.Effects.Add(snap);
                ShowToast($"已追加效果：{snap.DisplayName ?? "效果"}（选中槽位后点选可替换）");
            }
            // 光环上移效果层（2026-09-23）：挂载即并集入卡面（多光环取并集）——重算式
            _card.AggregateEffectAuras(true);
            _card.ResetCache();
            RefreshAttached();
            Recalculate();
        }

        // 效果图 → 卡内嵌效果（2026-09-14 修复：编排字段全量拷贝；2026-10-05 两槽定案：引擎通道载荷化——
        // branch 载荷随步骤原子走，快照不再分路）。
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
                ArrowDirections = src.ArrowDirections, // 光环上移效果层（2026-09-23）——箭头随效果快照
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

        private void RefreshAttached()
        {
            ClearContent(_attachedList.Content);
            var effects = _card.Effects;
            if (_selectedSlot >= effects.Count) _selectedSlot = -1; // 删槽后越界回收
            for (int i = 0; i < effects.Count; i++)
            {
                var index = i;
                var label = string.IsNullOrEmpty(effects[i].DisplayName) ? $"效果 #{i + 1}" : effects[i].DisplayName;
                MakeAttachedRow(label, effects[i], index == _selectedSlot,
                    () => SelectSlot(index),
                    () => MoveAttached(index, -1),
                    () => MoveAttached(index, 1),
                    () => RemoveAttachedAt(index),
                    () =>
                    {
                        // 跳转效果合成器编辑该效果（2026-09-14 合成器重做：静态会话传上下文）
                        ComposerSession.BeginCardEdit(_card, index);
                        Manager.Show<EffectComposerScreen>();
                    });
            }
            MakeEmptySlotRow();
        }

        /// <summary>选中效果槽（槽位制）：行点击=选中，右栏「效果列表」点选即填入该槽；
        /// -1=尾部空槽（追加态，默认）。</summary>
        private void SelectSlot(int index)
        {
            if (_selectedSlot == index) return;
            _selectedSlot = index;
            RefreshAttached();
        }

        /// <summary>槽位上下移（挂载顺序=ACard effect/passives 拆分序——首个主动效果上 effect 区）。</summary>
        private void MoveAttached(int index, int delta)
        {
            int to = index + delta;
            if (index < 0 || index >= _card.Effects.Count || to < 0 || to >= _card.Effects.Count) return;
            var fx = _card.Effects[index];
            _card.Effects.RemoveAt(index);
            _card.Effects.Insert(to, fx);
            if (_selectedSlot == index) _selectedSlot = to;
            else if (_selectedSlot == to) _selectedSlot = index;
            _card.ResetCache();
            RefreshAttached();
            Recalculate();
        }

        private void RemoveAttachedAt(int index)
        {
            if (index >= 0 && index < _card.Effects.Count)
            {
                _card.Effects.RemoveAt(index);
                if (_selectedSlot == index) _selectedSlot = -1; // 选中槽被删→回追加态
                // 光环随效果回收（2026-09-23）：移除效果后按剩余效果重算卡面箭头+光环
                _card.AggregateEffectAuras(true);
                _card.ResetCache();
                RefreshAttached();
                Recalculate();
            }
        }

        /// <summary>尾部空槽行（追加目标）：默认选中态——右栏点效果不选槽直接追加。</summary>
        private void MakeEmptySlotRow()
        {
            var row = UiKit.Column("slot-empty", _attachedList.Content, spacing: 2f, pad: 6f);
            var bg = UiKit.BgRow(row);
            if (_selectedSlot == -1) bg.color = UiStyle.SelectedRowBg;
            bg.raycastTarget = true;
            UiKit.Size(row, fw: 1f);
            var top = UiKit.Row("top", row, spacing: 6f);
            var lbl = UiKit.Label("name", top, "＋ 空槽位（追加）——选中后右侧「效果列表」点选效果填入",
                UiStyle.SmallSize, UiStyle.TextHint);
            UiKit.Size(lbl, fw: 1f);
            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => { Debug.Log("[UI点击] 空槽位"); SelectSlot(-1); });
        }

        /// <summary>挂载效果槽行（2026-10-13 槽位制）：整行点击=选中槽（右栏效果列表点选填入）；
        /// ↑↓调序（顺序=ACard effect/passives 显示序）/ 编辑跳效果合成器 / 移除；选中行高亮。
        /// 摘要 AtomText 单一来源。</summary>
        private void MakeAttachedRow(string name, CardEffectData effect, bool selected,
            Action onSelect, Action onUp, Action onDown, Action onRemove, Action onEdit)
        {
            var row = UiKit.Column("row", _attachedList.Content, spacing: 2f, pad: 6f);
            var bg = UiKit.BgRow(row);
            if (selected) bg.color = UiStyle.SelectedRowBg;
            bg.raycastTarget = true;
            UiKit.Size(row, fw: 1f);

            var top = UiKit.Row("top", row, spacing: 6f);
            var libRow = TplLibRow(top, name, onSelect); // 行点击=选中槽（效果组合界面同款交互）
            if (libRow == null)
            {
                var label = UiKit.Label("name", top, name, UiStyle.BodySize, UiStyle.TextBody);
                UiKit.Size(label, fw: 1f);
            }
            TplButton(top, "↑", onUp, width: 36f);
            TplButton(top, "↓", onDown, width: 36f);
            TplButton(top, "编辑", onEdit);
            TplButton(top, "移除", onRemove);

            var graph = new EffectGraphData(name) { header = effect, steps = effect?.Steps };
            var summary = UiKit.Label("summary", row, AtomText.RenderEffectSummary(graph, _card),
                UiStyle.SmallSize, UiStyle.TextFaint, wrap: true);
            UiKit.Size(summary, fw: 1f);
        }

        // ======================================== 右栏：关键词（本体=直接引用原子） ========================================

        private void BuildKeywordZone()
        {
            ClearChildren(_keywordZone);
            EnsureCardLists();

            var header = UiKit.Label("header", _keywordZone, "本体关键词", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(header, fw: 1f);

            // 现有关键词 chips（颜色沿用效果组合界面的 chip 体系）
            var chips = UiKit.Row("chips", _keywordZone, spacing: 4f);
            if (_card.Keywords.Count == 0)
            {
                var none = UiKit.Label("none", chips, "（无本体关键词——直接引用原子效果即关键词）",
                    UiStyle.SmallSize, UiStyle.TextHint);
                UiKit.Size(none, fw: 1f);
            }
            else
            {
                foreach (var kw in _card.Keywords.ToList())
                {
                    var captured = kw;
                    var (bg, fg) = ChipColors(ColorFilter.OfKeyword(captured));
                    var chip = UiKit.Row("chip", chips, spacing: 2f, pad: 4f);
                    var chipBg = chip.gameObject.AddComponent<Image>();
                    chipBg.sprite = UiKit.RoundedSprite;
                    chipBg.type = Image.Type.Sliced;
                    chipBg.color = bg;
                    var lbl = UiKit.Label("label", chip, KeywordZh(captured),
                        UiStyle.SmallSize, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
                    UiKit.Size(chip, h: 26f);
                    UiKit.MiniButton("del", chip, "✕", () =>
                    {
                        _card.Keywords.Remove(captured);
                        _card.ResetCache();
                        BuildKeywordZone();
                        Recalculate();
                    }, fg: fg);
                }
            }

            // 添加下拉（目录=Grant 族原子，中文名；赋予他人形态走效果合成器）
            var catalog = KeywordCatalog.LoadAll();
            var choices = new List<string> { "（添加本体关键词）" };
            choices.AddRange(catalog.Select(k => k.DisplayName));
            TplDropdown(_keywordZone, choices, 0, (idx, _) =>
            {
                int i = idx - 1;
                if (i < 0 || i >= catalog.Count) return;
                var kw = catalog[i];
                if (_card.Keywords.Contains(kw.Id))
                {
                    ShowToast($"已有关键词：{kw.DisplayName}");
                }
                else
                {
                    _card.Keywords.Add(kw.Id); // 保存时经 TryGetAtomRefId 反查 refId 写入 effectIds
                    _card.ResetCache();
                    ShowToast($"已添加：{kw.DisplayName}（保存=effectIds 直接引用原子 refId）");
                }
                BuildKeywordZone();
                Recalculate();
            }, width: 240f);

            var hint = UiKit.Label("hint", _keywordZone,
                "关键词=卡直接引用原子（本体）；「赋予」类关键词经效果合成器组合后按效果挂载",
                UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(hint, fw: 1f);
        }

        // ======================================== 自动算费 ========================================

        private void Recalculate()
        {
            var result = CardCostCalculator.Calculate(_card);
            _lastSuggestedCost = result.CostDict;

            ClearContent(_breakdownList.Content);
            foreach (var line in result.Breakdown)
            {
                var row = UiKit.Row("row", _breakdownList.Content, spacing: 8f, pad: 4f);
                UiKit.BgRow(row);
                var label = UiKit.Label("label", row, line.Label, UiStyle.SmallSize, UiStyle.TextBody);
                UiKit.Size(label, fw: 1f);
                UiKit.Label("value", row, line.Value.ToString("0.0"), UiStyle.SmallSize, UiStyle.TextFaint);
            }

            // 超模校验已移除（2026-09-24 定案）：卡层只算减费与建议档位——展示 D 与黑白获得，不做合规判断
            var grantStr = result.Grants.Count > 0
                ? "｜获得 " + string.Join(" ", result.Grants.Select(kv => $"{(ManaType)kv.Key} {(int)kv.Value}"))
                : "";
            _suggested.text = $"建议档位 {result.ManaCost}（D={result.Total:0}）{grantStr}";
            RefreshCostLabel();
            RefreshPreview(); // 六区改版：全部 _card 变更汇入中栏 ACard 预览（本方法是统一收口）
        }

        // 采纳：建议费用分布整组写入（位置数组）。
        private void OnAdoptCost()
        {
            if (_lastSuggestedCost == null || _lastSuggestedCost.IsZero)
            {
                ShowToast("无建议费用可采纳（D=0 保持空，打出按默认灰 1 计）");
                return;
            }
            _card.Cost = _lastSuggestedCost.Clone();
            _card.ResetCache();
            RefreshCostLabel();
            ShowToast("已采纳建议分布：" + _lastSuggestedCost.ToString());
        }

        private void RefreshCostLabel()
        {
            if (_card.Cost == null || _card.Cost.IsZero)
            {
                _costLabel.text = "当前费用 (无)";
                return;
            }
            _costLabel.text = "当前费用 " + _card.Cost.ToString();
        }

        // ======================================== 保存（覆盖替换） ========================================

        private void OnSave()
        {
            ApplyTextLists();
            EnsureCardLists();

            // 形状拦截（2026-10-15 深夜三类卡改版）：法术=1~3 个启动式效果（施放时弹窗抉择其一）；
            // 结界=禁一切启动式（只挂自动/光环）。装载侧同口（CardLoader.ValidateSpellShape/ValidateEnchantShape）。
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

            // 连接光环归属闸（2026-10-15 改版）：聚合后卡面光环（卡面直书+效果携带并集）只准结界携带
            if (_kind != CardKind.Enchantment && _card.LinkAuras != null && _card.LinkAuras.Count > 0)
            {
                ShowToast(_kind == CardKind.Creature
                    ? "生物不可挂连接光环（2026-10-15 改版）——移除光环后保存"
                    : "法术只挂主动效果——移除光环后保存");
                return;
            }

            var newId = "C_" + ContentHasher.HashCard(_card);
            bool replacing = !string.IsNullOrEmpty(_editingOriginalId) && _editingOriginalId != newId;

            // 覆盖替换前置检查（2026-09-24 定案）：旧卡被卡组引用则拦截（删除会使卡组引用悬空）
            if (replacing)
            {
                var refs = DeckSerializer.LoadAll()
                    .Where(d => d.cardIds != null && d.cardIds.Contains(_editingOriginalId))
                    .Select(d => d.name)
                    .ToList();
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
            CorpusCache.Clear(); // 保存后语料整体重建（交接稿配套）
            TutorialCreationFlow.NotifyCardsChanged(); // 第三课走查步检测（未开课零行为）

            ShowToast(replacing
                ? $"已覆盖替换：{_card.CardName}（{_editingOriginalId} → {newId}）"
                : $"已保存到卡表：{System.IO.Path.GetFileName(path)}");

            OnNewCard();
        }

        private void ApplyTextLists()
        {
            _card.CardName = string.IsNullOrWhiteSpace(_nameField.text) ? "新卡牌" : _nameField.text.Trim();
            _card.Tags = SplitCsv(_tagsField.text);
        }

        private static List<string> SplitCsv(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }
            return raw.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private void ShowToast(string message)
        {
            _toast.text = message;
        }

        // ======================================== EffectUI 同款控件模板（2026-10-13 晚补齐） ========================================
        // tpl-root 七模板=从 EffectUI.prefab 拷贝的同款子树（dropdown/input/toggle/button/lib-row/atom-card）
        // +CardUI 自身 chip 克隆——克隆走 UiKit.CloneTemplateFrom（模板须自身不激活）；
        // 缺模板与 EffectComposer 同口径：LogError 返回 null，本控件跳过不炸屏。

        private RectTransform TplClone(string name, RectTransform parent)
        {
            if (_tplRoot == null) _tplRoot = Find("tpl-root");
            var clone = UiKit.CloneTemplateFrom(_tplRoot, name, parent);
            if (clone == null)
                Debug.LogError($"[CardComposer] 缺模板 {name}（CardUI.prefab 根下 tpl-root，模板自身不激活）——该控件跳过");
            return clone;
        }

        private static void SetTplText(RectTransform node, string text)
        {
            var t = node != null ? node.GetComponentInChildren<TMP_Text>(true) : null;
            if (t != null) t.text = text ?? "";
        }

        /// <summary>
        /// dropdown-cardtype 换装：旧版=按钮头+运行时弹层（2026-10-07 式），这里首次 Build 时
        /// 原位替换成 tpl-dropdown 克隆出的真 TMP_Dropdown（弹层由 TMP Template 自管，EffectUI 同款）；
        /// 位置/尺寸/层级/激活态全继承旧节点，幂等——已是 TMP_Dropdown 则直接返回。
        /// </summary>
        private TMP_Dropdown EnsureCardtypeDropdown()
        {
            var node = Find("dropdown-cardtype");
            if (node == null)
            {
                Debug.LogError("[CardComposer] 缺 dropdown-cardtype 节点——类型下拉跳过");
                return null;
            }
            var existing = node.GetComponent<TMP_Dropdown>();
            if (existing != null) return existing;
            var clone = TplClone("tpl-dropdown", (RectTransform)node.parent);
            var tmp = clone != null ? clone.GetComponent<TMP_Dropdown>() : null;
            if (tmp == null) return null;
            clone.name = "dropdown-cardtype";
            clone.anchorMin = node.anchorMin;
            clone.anchorMax = node.anchorMax;
            clone.anchoredPosition = node.anchoredPosition;
            clone.sizeDelta = node.sizeDelta;
            clone.pivot = node.pivot;
            clone.localScale = node.localScale;
            var oldLe = node.GetComponent<LayoutElement>();
            var le = clone.GetComponent<LayoutElement>();
            if (oldLe != null && le != null)
            {
                le.minWidth = oldLe.minWidth;
                le.preferredWidth = oldLe.preferredWidth;
                le.flexibleWidth = oldLe.flexibleWidth;
            }
            clone.SetSiblingIndex(node.GetSiblingIndex());
            clone.gameObject.SetActive(node.gameObject.activeSelf);
            UnityEngine.Object.Destroy(node.gameObject);
            return tmp;
        }

        /// <summary>下拉（EffectUI activate-bar 同款 TMP_Dropdown 克隆；选项/取值经 UiKit.Dropdown 统一进出）。</summary>
        private UiKit.Dropdown TplDropdown(RectTransform parent, List<string> options, int index,
            Action<int, string> onChanged = null, float? width = null)
        {
            var clone = TplClone("tpl-dropdown", parent);
            var tmp = clone != null ? clone.GetComponent<TMP_Dropdown>() : null;
            if (tmp == null) return null;
            if (width.HasValue) UiKit.Size(clone, w: width);
            return new UiKit.Dropdown(tmp, options, index, onChanged);
        }

        /// <summary>文本输入（EffectUI 过滤框同款克隆；placeholder 写模板内同名文本节点）。</summary>
        private UInputField TplInput(RectTransform parent, string placeholder, string initial,
            Action<string> onChanged = null, float? width = null, bool integer = false)
        {
            var clone = TplClone("tpl-input", parent);
            var field = clone != null ? clone.GetComponent<UInputField>() : null;
            if (field == null) return null;
            SetTplText(UiKit.FindDeep(clone, "placeholder"), placeholder);
            field.contentType = integer
                ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.Standard;
            if (onChanged != null) field.onValueChanged.AddListener(onChanged.Invoke);
            field.SetTextWithoutNotify(initial ?? "");
            if (width.HasValue) UiKit.Size(clone, w: width);
            return field;
        }

        /// <summary>整数输入（口径同 UiKit.IntField：非法输入不回调）。</summary>
        private UInputField TplInt(RectTransform parent, string placeholder, int initial,
            Action<int> onChanged, float? width = null)
        {
            var field = TplInput(parent, placeholder, initial.ToString(), null, width, integer: true);
            if (field != null)
                field.onValueChanged.AddListener(s => { if (int.TryParse(s, out var v)) onChanged(v); });
            return field;
        }

        /// <summary>开关（EffectUI 原子卡 rand 同款克隆；Label 文本可改）。</summary>
        private Toggle TplToggle(RectTransform parent, string label, bool initial, Action<bool> onChanged)
        {
            var clone = TplClone("tpl-toggle", parent);
            var tg = clone != null ? clone.GetComponent<Toggle>() : null;
            if (tg == null) return null;
            SetTplText(clone, label);
            tg.SetIsOnWithoutNotify(initial);
            tg.onValueChanged.AddListener(onChanged.Invoke);
            return tg;
        }

        /// <summary>按钮（EffectUI 原子卡 del 同款克隆；label 子节点文本可改，宽度同步 LE 防布局组控宽顶死）。</summary>
        private Button TplButton(RectTransform parent, string label, Action onClick, float? width = null)
        {
            var clone = TplClone("tpl-button", parent);
            var btn = clone != null ? clone.GetComponent<Button>() : null;
            if (btn == null) return null;
            SetTplText(clone, label);
            if (width.HasValue)
            {
                UiKit.Size(clone, w: width);
                var le = clone.GetComponent<LayoutElement>();
                if (le != null) le.preferredWidth = width.Value;
            }
            btn.onClick.AddListener(() => { Debug.Log("[UI点击] " + label); onClick(); });
            return btn;
        }

        /// <summary>列表行（EffectUI 效果表行同款克隆：底图+整行点击+费用方格+meta 单列文本）。
        /// 点击日志与宽度兜底口径同 EffectComposerScreen.MakeListRow。</summary>
        private RectTransform TplLibRow(RectTransform parent, string metaText, Action onClick)
        {
            var clone = TplClone("tpl-lib-row", parent);
            if (clone == null) return null;
            var meta = UiKit.FindDeep(clone, "meta")?.GetComponentInChildren<TMP_Text>(true);
            if (meta != null) meta.text = metaText ?? "";
            var csv = clone.GetComponentInChildren<CostSquaresView>(true);
            if (csv != null) csv.Clear(); // 卡组合侧不算行级费用——方格清空占位（换入实例保留待用）
            UiKit.Size(clone, fw: 1f);
            var btn = clone.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { Debug.Log("[UI点击] 效果行"); onClick(); });
            }
            return clone;
        }

        // ======================================== 通用 ========================================

        /// <summary>费用徽标（蓝色小圆片+数字）。</summary>
        private static void CostBadge(string name, RectTransform parent, string value)
        {
            var rt = UiKit.Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiKit.CircleSprite;
            img.color = UiStyle.BtnPrimary;
            var lbl = UiKit.Label("label", rt, value, UiStyle.SmallSize, UiStyle.White,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.StretchInset(lbl.rectTransform, 2f, 0f);
            UiKit.Size(rt, w: 26f, h: 24f);
        }

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
    }
}
