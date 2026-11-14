using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UInputField = TMPro.TMP_InputField;
using UButton = UnityEngine.UI.Button;

namespace SynergyUI
{
    /// <summary>
    /// 卡组构建界面（2026-10-23 v2 空间重排 + 模板化，deck-ui-plan 定案）：
    /// 左 648=当前卡组（名称行/汇总/卡表滚动 452/统计区**非滚动** 356——构成检测+曲线/颜色双列+类型行）；
    /// 中 392=只读卡面（SlotBg 底板 240×432 居中）+ 异常提示 + 英雄技能栏；
        /// 右 824=卡池（色/型 chips 固定面板内不滚动 + 搜索 + 滚动填充剩余高度）+ 卡组库（预组 3 只读可复制
        /// + 我的卡组，固定 260 贴底）。中栏卡面可点击放大（全屏遮罩+600×1080，独立 Zoom 绑定）。
    ///
    /// 视觉全部走 prefab 烘焙模板（DeckUI.prefab 各面板 templates 根下 tpl-*，隐藏约定见
    /// UiKit.CloneTemplateFrom）：行/chip/统计块/分区标题/空态一律克隆模板 + 绑文本/显隐/onClick，
    /// 代码不再现搭视觉节点。色 chips 预制体烘焙（带色），卡型 chips 由 tpl-chip 克隆进 facet-kind
    /// 一次、刷新只切选中态（Outline 烘焙 disabled，只切 enabled）。
    ///
        /// 构筑规则（2026-10-11 定案）：① 必须标记英雄技能（资格=HeroSkillSystem.CanBeSkillCard
        /// ——法术且 1~3 个主动效果），技能栏空=构成约束不过、不可保存；② 唯一效果（唯一门/同名
        /// 仪典）每卡组限 1 张——添加口+保存口双拦；③ 卡组 30-60 张、卡可重复任意张。
        /// 黑白代价覆盖（2026-10-26 定案）：需黑/白元素的卡须至少带 1 张对应补偿色的代价卡
        /// （数量不限）——构成约束不过、不可保存。
        /// 主题双色约束已删（2026-10-26 用户定案）：预组构建期的临时检查，不面向玩家。
        ///
        /// 异常卡口径（2026-10-16 全局对齐定案）：断链卡（卡表无此 ID）无法展示——仅可
        /// 删除；形状违规卡可展示但红字标记；断链/形状任一即不可保存，卡组列表/挑战/
        /// 对战入口按 DeckCompositionValidator.UnusableTag 同源标记（单源 Usable）。
        /// 地源不足（2026-10-26）：仅构成检测块提示「无法释放」，不拦保存。
    ///
    /// 编辑跳转：卡池行「编辑」→ CardComposerScreen.PendingEditCardId（UIManager.Show 无法
    /// 传参，仿 ComposerSession 静态会话）→ Show&lt;CardComposerScreen&gt;；返回本屏 OnEnter
    /// 按缓存恢复卡组名并全量刷新（卡池/卡组可能已在编辑界面变化）。
    /// </summary>
    public sealed class DeckBuilderScreen : UIScreen
    {
        protected override string PrefabName => "DeckUI";
        protected override string RootName => "deck-builder";

        // 构筑张数上下限（2026-10-11 定案规则③）：30-60 张、卡可重复任意张。
        private const int MinDeckCards = 30;
        private const int MaxDeckCards = 60;

        // 当前正在构筑的卡组（卡牌 ID 列表，同卡多册多条目）。
        private readonly List<string> _deckCardIds = new List<string>();

        // ---- 衍生物区/槽绑定（2026-11-13 自动引用改版：衍生物定义收口卡组合界面）----
        // 区=自动引用（CollectReferencedTemplateIds 单源 ∪ str 空槽旧档绑定目标，随加删卡
        // 实时重算——RecomputeTokenZone；不再手动管理，不设容量上限）；
        // 绑定=(卡ID, 效果序号)→模板 ID——仅 str 空槽的旧档兜底（str 为权威，非空槽绑定即清）。
        private readonly List<string> _tokenCardIds = new List<string>();
        private readonly List<DeckTokenBinding> _tokenBindings = new List<DeckTokenBinding>();

        /// <summary>衍生物模板卡判定（2026-10-22 额外区配套）：入组开局落 Zone.Extra 不进牌库——
        /// 不计入 30-60 构筑张数（添加口/汇总/保存口三处同口径）。</summary>
        private static bool IsTokenTemplateId(string id)
            => CardCatalog.GetById(id)?.IsTokenTemplate == true;

        /// <summary>构筑张数口径：排除衍生物模板卡后的张数。</summary>
        private int DeckBuildCount => _deckCardIds.Count(id => !IsTokenTemplateId(id));

        private TipPromptHost _tips; // TipPanel 提示弹窗宿主（2026-10-18 统一定案；惰性建——字段初始化器不能引用实例 Root）
        private TipPromptHost Tips => _tips ??= new TipPromptHost(() => Root);

        // 英雄技能标记卡 ID（技能栏常显；null=未设置——保存口必选拦截；资格自愈同旧版）。
        private string _skillCardId;

        // 卡组名缓存：屏实例按类型缓存，但预制体层级每进屏重建——跳卡牌编辑返回时由此恢复输入框。
        private string _currentDeckName;

        // 右栏卡组列表选中名（null=新建态）；保存成功后指向保存名（行高亮对账）。
        private string _selectedDeckName;

        // 中栏预览（2026-10-28 preview-panel 重构）：烘焙 ACard（preview-stage/ACard 360×648 原尺寸）
        // 直挂 ACardView——不再经 CardOverlayController 填 card-slot；费用=烘焙 CostSquares 直填。
        private CardData _previewCard;      // 当前预览卡（数据源；null=空态藏卡）
        private ACardView _previewView;     // 烘焙卡面 View（Build 挂一次，幂等）

        // 放大态绑定（验收2）：独立 Key="Zoom" 与 card-slot 的 "Preview" 并存——Bind 同键只挂一处，
        // 异键双挂互不冲突；遮罩/放大卡槽烘焙在 overlay/zoom-overlay（默认隐藏，点卡面开启）。
        private readonly List<CardOverlayBinding> _zoomBindings = new List<CardOverlayBinding>();
        private RectTransform _zoomOverlay;
        private RectTransform _zoomSlot;
        private CostSquaresView _previewCost;  // 烘焙费用条（preview-stage/CostSquares）

        // ---- P4 §2（2026-10-29）衍生物选中才展开：预览区右下角展开宿主 + 第二张卡绑定 ----
        private RectTransform _previewStage;      // Build 期捕获（=烘焙 ACard 的父；null=缺宿主降级）
        private RectTransform _tokenExpansion;    // 展开区根（preview-stage 内运行时建；隐藏=无槽卡）
        private RectTransform _tokenSlot;         // 第二张卡槽（360×648·localScale 0.5 → 180×324）
        private CardOverlayBinding _tokenBinding; // 第二张卡绑定（卡面层池化；null=未绑定/失效）
        private RectTransform _zoomTokenSlot;     // 放大态第二张卡槽（zoom-overlay 内运行时建·懒建）

        // 两套过滤（2026-10-23 v2）：色 chips（预制体烘焙 facet-color）+ 卡型 chips（tpl-chip 克隆进
        // facet-kind 一次，固定面板内不随列表滚动；刷新只切选中态）。
        private readonly Dictionary<UIColor, UButton> _filterChips = new Dictionary<UIColor, UButton>();
        private readonly Dictionary<string, UButton> _kindChips = new Dictionary<string, UButton>(); // ""=全部
        private UIColor _colorFilter = UIColor.All;
        private string _kindFilterZh; // null = 全部（技能=可标记英雄技能的法术，独立第四档）
        private static readonly string[] KindFilterChoices = { "生物", "法术", "结界", "技能" };

        // ---- 绑定节点 ----
        private UiKit.Scroll _catalogList;
        private UiKit.Scroll _deckList;
        private UiKit.Scroll _decksList;
        private RectTransform _statsZone;   // v2：非滚动统计容器（stats-row 预制体实例 + tip 常驻）
        private RectTransform _statsRow;    // 2026-11-10 预制体化：stats-row 嵌套实例（烘焙正本——代码只绑定不生成）
        private TMP_Text _statsTip;         // stats-zone/tip：构成检测提示（兜底单条——行池缺失时）
        private readonly List<TMP_Text> _tipLines = new List<TMP_Text>(); // tip 行池（每行独立色，2026-11-13 去富文本）
        private RectTransform _overlay;
        private RectTransform _catalogFilter; // facet-color（v2 改名，旧 catalog-filter）
        private RectTransform _facetKind;     // 卡型 chips 固定容器
        private RectTransform _deckTemplates; // deck-panel/templates（隐藏模板根）
        private RectTransform _catTemplates;  // catalog-panel/templates
        private TMP_Text _summary;
        private TMP_Text _toast;
        // 2026-11-13 技能栏 effect 行改版：skill-slot = label（卡名标题）+ effect 行实例
        //（第 1 行 + 克隆源，多主动技能克隆 effect2/3…）——label/effect 均 scoped 于 skill-slot
        //（防与 tpl-empty/label、预览 ACard/effect 串名）。
        private RectTransform _skillSlot;
        private TMP_Text _skillLabel;
        private RectTransform _skillTemplate;
        private readonly List<RectTransform> _skillRowClones = new List<RectTransform>();
        private TMP_Text _deckCount;    // hdr-deck 右侧「N 种 / M 张」
        private TMP_Text _catalogCount; // hdr-catalog 右侧「N 张」
        private UInputField _nameField;
        private UInputField _searchField;

        protected override void Build()
        {
            _overlay = Find("overlay"); // 弹层挂载点（下拉弹层/确认框）

            // ---- 顶栏 ----
            BindButton("btn-back", () => Manager.Back());

            // ---- 左栏：卡组名 / 新建 / 保存 / 当前卡组 / 统计 ----
            _nameField = FindInput("field-deck-name");
            if (_nameField != null)
                // 名缓存供跳卡牌编辑返回恢复（主题约束已删——改名不再触统计区重建）
                _nameField.onValueChanged.AddListener(v => _currentDeckName = v);
            BindButton("btn-new", OnNewDeck);
            BindButton("btn-save", OnSave);
            _summary = FindText("lbl-summary");
            _toast = FindText("lbl-toast");
            _deckList = FindScroll("list-deck");
            _statsZone = Find("stats-zone"); // v2 非滚动（RectMask 都不再需要——纯容器）
            _statsRow = Find("stats-row");   // 2026-11-10 预制体化：嵌套 stats-row 实例（烘焙真源）
            _statsTip = FindText("tip");     // 构成检测提示位（兜底单条——行池缺失时整条单色）
            BindTipLinePool();               // 2026-11-13 去富文本：tip 行池（每行独立 TMP 独立颜色）
            _deckCount = FindUnder(Find("hdr-deck"), "count")?.GetComponentInChildren<TMP_Text>(true);

            // ---- 中栏：只读卡面 + 英雄技能栏（2026-10-28 preview-panel 重构：烘焙 ACard 360×648
            // 原尺寸直挂 ACardView，点卡面放大=运行时补 Button；费用=preview-stage/CostSquares 直填） ----
            var bakedCard = Find("ACard") as RectTransform;
            if (bakedCard != null)
            {
                _previewStage = bakedCard.parent as RectTransform; // P4 §2：衍生物展开宿主（缺=降级不展开）
                _previewView = bakedCard.GetComponent<ACardView>() ?? bakedCard.gameObject.AddComponent<ACardView>();
                _previewView.Bind();
                var previewBtn = bakedCard.GetComponent<UButton>();
                if (previewBtn == null)
                {
                    previewBtn = bakedCard.gameObject.AddComponent<UButton>();
                    previewBtn.transition = UButton.Transition.None;
                }
                previewBtn.onClick.AddListener(() => { if (_previewCard != null) OpenZoom(_previewCard); });
            }
            else Debug.LogError("[DeckBuilder] preview-stage 缺烘焙 ACard 卡面——检查 DeckUI.prefab");
            var bakedCost = Find("CostSquares") as RectTransform;
            _previewCost = bakedCost != null ? bakedCost.GetComponentInChildren<CostSquaresView>(true) : null;
            if (_previewCost == null)
                Debug.LogError("[DeckBuilder] preview-stage 缺烘焙 CostSquares 费用条——检查 DeckUI.prefab");
            // 2026-11-13 技能栏改版：绑定 skill-slot 子树（旧 lbl-skill/lbl-skill-cost 全屏寻名
            // 与烘焙结构不匹配——RefreshSkillSlot 曾静默空转，改 scoped 绑定烘焙真身）
            _skillSlot = Find("skill-slot");
            _skillLabel = UiKit.FindDeep(_skillSlot, "label")?.GetComponentInChildren<TMP_Text>(true);
            _skillTemplate = UiKit.FindDeep(_skillSlot, "effect") as RectTransform;

            // 放大态遮罩接线（验收2）：点遮罩=还原（放大卡本体无 OnClick，点卡面不关闭）
            _zoomOverlay = Find("zoom-overlay");
            _zoomSlot = Find("zoom-card");
            var zoomMask = UiKit.FindDeep(_zoomOverlay, "zoom-mask");
            var zoomMaskBtn = zoomMask != null ? zoomMask.GetComponentInChildren<UButton>(true) : null;
            if (zoomMaskBtn != null)
                zoomMaskBtn.onClick.AddListener(() => { Debug.Log("[UI点击] zoom-mask"); CloseZoom(); });

            // ---- 右栏：卡池过滤 / 搜索 / 卡组列表 ----
            _catalogFilter = Find("facet-color"); // v2 改名（旧 catalog-filter）
            _facetKind = Find("facet-kind");
            _catalogList = FindScroll("list-catalog");
            _catalogCount = FindUnder(Find("hdr-catalog"), "count")?.GetComponentInChildren<TMP_Text>(true);
            _searchField = FindInput("field-search");
            if (_searchField != null)
                _searchField.onValueChanged.AddListener(_ => RefreshCatalog());
            _decksList = FindScroll("list-decks");

            // 模板根（面板下隐藏 templates——不进滚动 content，刷新不连坐）
            _deckTemplates = FindUnder(Find("deck-panel"), "templates");
            _catTemplates = FindUnder(Find("catalog-panel"), "templates");

            BindFilterRow(); // 色 chips（烘焙）一次接线 + 卡型 chips（tpl-chip 克隆进 facet-kind 一次）
        }

        public override void OnEnter()
        {
            // 跳卡牌编辑返回（或首次进屏）：恢复卡组名 + 全量刷新（卡池/卡组可能已在编辑界面变化）
            if (_nameField != null)
                _nameField.SetTextWithoutNotify(_currentDeckName ?? "");
            RefreshDecksList();
            RefreshAll();
        }

        public override void OnExit()
        {
            // UGUI 卡牌层回收（中栏卡面独立池化，切屏必须显式清）；放大/衍生物展开绑定随清——
            // 遮罩节点随屏销毁，残留绑定会在下一屏 Bind 时往已毁槽位挂卡
            _zoomBindings.Clear();
            _tokenBinding = null; // P4 §2：第二卡绑定同清（token-expansion 节点随屏销毁）
            CardOverlayController.ClearActive();
            Tips.Close(); // 在途提示弹窗清理
        }

        // ======================================== 模板绑定小件（克隆+绑文本/显隐/onClick） ========================================

        /// <summary>克隆模板到容器（缺模板/模板激活=断线，LogError 暴露不静默）。</summary>
        private RectTransform Clone(RectTransform templates, string tplName, RectTransform parent)
        {
            var row = UiKit.CloneTemplateFrom(templates, tplName, parent);
            if (row == null)
                Debug.LogError($"[DeckBuilder] 模板克隆失败：{tplName}（检查 DeckUI.prefab {templates?.parent?.name}/templates）");
            return row;
        }

        /// <summary>按名取行内 TMP 写文本（可选换色——断链红字/选中态等数据态用）。</summary>
        private static void SetText(RectTransform scope, string node, string text, Color? color = null)
        {
            var t = UiKit.FindDeep(scope, node)?.GetComponentInChildren<TMP_Text>(true);
            if (t == null) return;
            t.text = text ?? "";
            if (color.HasValue) t.color = color.Value;
        }

        /// <summary>按名取行内节点切显隐（模板全件烘焙，代码只切）。</summary>
        private static void SetVisible(RectTransform scope, string node, bool visible)
        {
            var rt = UiKit.FindDeep(scope, node);
            if (rt != null) rt.gameObject.SetActive(visible);
        }

        /// <summary>行内按钮接线（克隆体复用安全：先清旧监听）。</summary>
        private static UButton BindRowButton(RectTransform row, string node, Action onClick)
        {
            var rt = UiKit.FindDeep(row, node);
            var btn = rt != null ? rt.GetComponentInChildren<UButton>(true) : null;
            if (btn == null) return null;
            btn.onClick.RemoveAllListeners();
            if (onClick != null)
                btn.onClick.AddListener(() => { Debug.Log($"[UI点击] {node}"); onClick(); });
            return btn;
        }

        /// <summary>费用徽标写值（圆片烘焙在模板 cost 节点）：口径单源=CardRowText.FeeTextOf
        /// （2026-11-13 用户定案：费用=消耗+代价；卡组合界面卡池行同源——法术含施放锚价，
        /// 生物/结界=卡级净费+非主动行代价）。</summary>
        private static void BindCost(RectTransform row, CardData card)
            => CardRowText.BindCostNode(row, card);

        /// <summary>空态克隆：文本 +（可选）「清除全部筛选」钮。</summary>
        private RectTransform MakeEmpty(RectTransform templates, RectTransform content, string text, bool clearButton)
        {
            var empty = Clone(templates, "tpl-empty", content);
            if (empty == null) return null;
            SetText(empty, "label", text);
            SetVisible(empty, "btn-clear", clearButton);
            if (clearButton) BindRowButton(empty, "btn-clear", ClearAllFilters);
            UiKit.Size(empty, fw: 1f);
            return empty;
        }

        // ======================================== 右栏：卡池列表 ========================================

        /// <summary>色 chips 绑定（预制体烘焙 chip-{Color}，一次接线）+ 卡型 chips 克隆进 facet-kind
        /// （v2 固定面板内不滚动）；激活态=烘焙 Outline 开关（默认 disabled，代码只切 enabled）。</summary>
        private void BindFilterRow()
        {
            if (_catalogFilter != null)
            {
                // 白/黑 chip 失活（2026-10-29 P4 色序清尾）：UIColor 白/黑成员已删，
                // 预制体残烘的 chip-Black/chip-White 不再接线——显式失活防「可见但点不动」
                foreach (var legacy in new[] { "chip-Black", "chip-White" })
                {
                    var dead = UiKit.FindDeep(_catalogFilter, legacy);
                    if (dead != null) dead.gameObject.SetActive(false);
                }
                foreach (UIColor color in Enum.GetValues(typeof(UIColor)))
                {
                    var captured = color;
                    var chipRt = UiKit.FindDeep(_catalogFilter, $"chip-{captured}");
                    var chip = chipRt != null ? chipRt.GetComponentInChildren<UButton>(true) : null;
                    if (chip == null) continue; // 缺 chip 由 FindDeep 静默——此处仅容错跳过
                    chip.onClick.AddListener(() =>
                    {
                        _colorFilter = captured;
                        UpdateFilterChips();
                        RefreshCatalog();
                    });
                    _filterChips[captured] = chip;
                }
                UpdateFilterChips();
            }

            // 卡型 chips：模板克隆一次（全部/生物/法术/结界/技能），刷新只切选中态不重建
            if (_facetKind != null && _catTemplates != null)
            {
                BindKindChip(null);
                foreach (var kind in KindFilterChoices)
                    BindKindChip(kind);
                UpdateKindChips();
            }
        }

        private void BindKindChip(string kind)
        {
            var captured = kind; // null=全部
            var chip = Clone(_catTemplates, "tpl-chip", _facetKind);
            if (chip == null) return;
            SetText(chip, "label", captured ?? "全部");
            var btn = chip.GetComponentInChildren<UButton>(true);
            if (btn == null) return;
            btn.onClick.AddListener(() =>
            {
                _kindFilterZh = captured;
                UpdateKindChips();
                RefreshCatalog();
            });
            _kindChips[captured ?? ""] = btn;
        }

        /// <summary>激活态切换（两套 chips 同口径）：选中 chip 开烘焙 Outline。</summary>
        private void UpdateFilterChips()
        {
            foreach (var kv in _filterChips)
            {
                var ol = kv.Value.GetComponent<Outline>();
                if (ol != null) ol.enabled = kv.Key == _colorFilter;
            }
        }

        private void UpdateKindChips()
        {
            foreach (var kv in _kindChips)
            {
                var ol = kv.Value.GetComponent<Outline>();
                if (ol != null) ol.enabled = kv.Key == (_kindFilterZh ?? "");
            }
        }

        /// <summary>右栏卡池刷新：源=CardCatalog.LoadBuildPool（单源见该函数注释），此后只剩
        /// 色/型/搜索三道展示筛选与排序——不在这里另加卡源口径。</summary>
        private void RefreshCatalog()
        {
            ClearContent(_catalogList.Content);

            // 卡池=CardCatalog.LoadBuildPool（2026-11-13 用户定案「卡池显示全卡——自定义和预组的全部卡」）：
            // 全表去衍生物模板/训练池/瞬态网络内容，**保留教学卡**。旧口径 = LoadPlayPool ∪「玩家卡组
            // 已引用的卡」漏掉预组：三套预组卡全带 tags=教学 被玩家池过滤，预组又不进玩家档引用 →
            // 新装/未存过卡组时卡池近乎空池；预设段另存的自建卡同样继承「教学」tag 被漏。
            // 随机组卡/卡牌编辑器等其余 LoadPlayPool 消费点口径不变。
            IEnumerable<CardData> cards = CardCatalog.LoadBuildPool();
            if (_colorFilter != UIColor.All)
                cards = cards.Where(c => CardSorter.HasCostColor(c, _colorFilter));
            if (_kindFilterZh != null)
                cards = cards.Where(c => KindMatch(c, _kindFilterZh));
            string query = _searchField != null ? _searchField.text : null;
            // 语料并集（名∪id∪卡型∪关键词∪Tags∪效果全文）多词 AND——搜「守护」命中带守护关键词的卡
            if (SearchQuery.IsActive(query))
                cards = cards.Where(c => SearchQuery.Match(SearchQuery.BuildCorpus(c), query));

            var sorted = CardSorter.Sort(cards).ToList();
            if (SearchQuery.IsActive(query))
                // 搜索态排序加权：名命中前置（稳定排序——命中组内保持全局 色→费→名 序）
                sorted = sorted.OrderByDescending(c => SearchQuery.NameHit(c, query)).ToList();

            if (_catalogCount != null) _catalogCount.text = $"{sorted.Count} 张";

            if (sorted.Count == 0)
            {
                var parts = new List<string>();
                if (_colorFilter != UIColor.All) parts.Add($"色：{ColorZh(_colorFilter)}");
                if (_kindFilterZh != null) parts.Add($"型：{_kindFilterZh}");
                if (SearchQuery.IsActive(query)) parts.Add($"词：{query.Trim()}");
                MakeEmpty(_catTemplates, _catalogList.Content,
                    parts.Count > 0 ? $"（无匹配卡牌）当前筛选：{string.Join(" + ", parts)}" : "（无卡牌可显示）",
                    clearButton: parts.Count > 0);
                return;
            }
            foreach (var card in sorted)
                MakeCatalogRow(card);
        }

        /// <summary>卡型谓词：技能=HeroSkillSystem.CanBeSkillCard（法术且 1~3 个主动效果）——
        /// 独立第四档；「法术」档含技能资格卡（技能卡本就是法术）。</summary>
        private static bool KindMatch(CardData c, string kindZh)
        {
            switch (kindZh)
            {
                case "生物": return c.Supertype == Cardtype.Creature;
                case "法术": return c.Supertype == Cardtype.Spell;
                case "结界": return c.Supertype == Cardtype.Enchantment;
                case "技能": return HeroSkillSystem.CanBeSkillCard(c);
                default: return true;
            }
        }

        private void ClearAllFilters()
        {
            _colorFilter = UIColor.All;
            _kindFilterZh = null;
            if (_searchField != null) _searchField.SetTextWithoutNotify("");
            UpdateFilterChips(); // chips 常驻（色=烘焙/型=克隆），单独恢复选中态
            UpdateKindChips();
            RefreshCatalog();
        }

        /// <summary>色名中文（空结果解释行用——chips 文案烘焙在预制体，这里补中文映射；
        /// 白/黑已随 2026-10-29 P4 色序清尾删除）。</summary>
        private static string ColorZh(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return "红";
                case UIColor.Blue: return "蓝";
                case UIColor.Green: return "绿";
                default: return "灰";
            }
        }

        /// <summary>卡池行（tpl-row-card 克隆）：费用徽标+卡名+类型身材，行点击=中栏预览；
        /// 行尾「编辑/添加」——编辑=跳卡牌编辑界面。英雄技能设置改版（2026-11-13）：行尾「技能」
        /// 钮退役——选中卡后点中栏预览的主动效果行即切换英雄技能（WirePreviewEffectRows）。</summary>
        private void MakeCatalogRow(CardData card)
        {
            var captured = card;
            var row = Clone(_catTemplates, "tpl-row-card", _catalogList.Content);
            if (row == null) return;

            BindCost(row, card);
            SetText(row, "name", NameOf(card));
            SetText(row, "meta", CardRowText.TypeStatOf(card));
            SetVisible(row, "btn-skill", false); // 2026-11-13 技能钮退役（prefab 节点保留，代码隐藏可逆）
            BindRowButton(row, "btn-edit", () => EditCard(captured));
            BindRowButton(row, "btn-add", () => AddCard(captured.ID));

            var rowBtn = row.GetComponent<UButton>();
            if (rowBtn != null)
            {
                rowBtn.onClick.RemoveAllListeners();
                rowBtn.onClick.AddListener(() => ShowPreview(captured));
            }
        }

        /// <summary>跳卡牌编辑界面（2026-10-16）：UIManager.Show 无法传参——静态会话带卡 ID，
        /// CardComposerScreen.OnEnter 消费后走 LoadCardForEdit。注意覆盖替换闸：该卡仍被卡组
        /// 引用时，改内容保存会被「旧卡被卡组引用」拦截（先删卡组内册数再编辑保存，回来重新添加）。</summary>
        private void EditCard(CardData card)
        {
            CardComposerScreen.PendingEditCardId = card.ID;
            Manager.Show<CardComposerScreen>();
        }

        // ======================================== 左栏：当前卡组 ========================================

        private void AddCard(string id)
        {
            // 2026-11-13 预组只读查看：构成不给加减（左栏 ± 已隐藏，此为卡池「添加」口兜底闸）
            if (DeckCatalog.IsPreset(_selectedDeckName))
            {
                ShowToast("预组只读——点「复制」生成自己的副本后修改");
                return;
            }
            // 构筑规则③：30-60 张内重复任意张——上限在添加口即时反馈
            //（衍生物模板卡不计入：入组开局落 Zone.Extra 额外区不进牌库，2026-10-22 口径）
            if (DeckBuildCount >= MaxDeckCards)
            {
                ShowToast($"卡组已达上限 {MaxDeckCards} 张");
                return;
            }
            // 构筑规则②：限量槽（唯一门/同名仪典=每卡组 1 张；容量门=每 10 张牌库卡 1 张——
            // 2026-11-02 命运一抽改版）。容量按「添加后」的卡组规模投影：卡组长大会、上限随之升高
            var card = CardCatalog.GetById(id);
            if (card != null)
            {
                foreach (var slot in ComposerCatalog.DeckUniqueSlots(card))
                {
                    var cap = ComposerCatalog.SlotCapacity(slot.Key, DeckBuildCount + 1);
                    if (DeckCardKeyCount(slot.Key) + 1 > cap)
                    {
                        ShowToast(cap == 1
                            ? $"「{slot.Display}」每卡组限 1 张——已有 {NameOf(DeckCardHoldingKey(slot.Key))}"
                            : $"「{slot.Display}」已达当前上限（卡组每 10 张可带 1 张，{DeckBuildCount + 1} 张上限 {cap} 张）");
                        return;
                    }
                }
            }
            _deckCardIds.Add(id);
            RefreshAll();
        }

        /// <summary>卡组内已持有指定构筑限量键的卡（无=null）——唯一门提示文案用。</summary>
        private CardData DeckCardHoldingKey(string key)
        {
            foreach (var id in _deckCardIds)
            {
                var c = CardCatalog.GetById(id);
                if (c != null && ComposerCatalog.DeckUniqueSlots(c).Any(u => u.Key == key))
                    return c;
            }
            return null;
        }

        /// <summary>卡组内持有指定构筑限量键的卡数——容量门添加拦截用。</summary>
        private int DeckCardKeyCount(string key)
        {
            int n = 0;
            foreach (var id in _deckCardIds)
            {
                var c = CardCatalog.GetById(id);
                if (c != null && ComposerCatalog.DeckUniqueSlots(c).Any(u => u.Key == key)) n++;
            }
            return n;
        }

        private static string NameOf(CardData card)
            => card == null ? "?" : string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName;

        /// <summary>移出一册（同卡聚合行语义：同卡多册只减 1；技能标记不随移除清除）。</summary>
        private void RemoveCopy(string cardId)
        {
            if (DeckCatalog.IsPreset(_selectedDeckName)) return; // 预组只读（± 隐藏后的兜底闸）
            _deckCardIds.Remove(cardId);
            RefreshAll();
        }

        /// <summary>左栏当前卡组（同卡聚合 ×册数，保序=首次入组序）：行=[费用徽标|卡名×N|异常
        /// 标记|删除]（tpl-row-deck 克隆）。断链卡（卡表无此 ID）无法展示——仅可删除；形状违规卡
        /// 行尾红字标记；带槽卡（P9b）flag 显示槽绑定状态、行点击=绑定面板（其余行点击=中栏预览）。
        /// 「衍生物区 / 引用卡」分区：P9b 衍生物区（模板可删+管理入口，容量计数在分区标题）+
        /// 卡组效果固定绑定引用的外部卡（只读可预览）。</summary>
        private void RefreshDeckList()
        {
            ClearContent(_deckList.Content);

            // 同卡聚合（保序）
            var ids = new List<string>();
            var counts = new List<int>();
            var indexOf = new Dictionary<string, int>();
            foreach (var id in _deckCardIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (indexOf.TryGetValue(id, out var at))
                {
                    counts[at]++;
                    continue;
                }
                indexOf[id] = ids.Count;
                ids.Add(id);
                counts.Add(1);
            }

            if (_deckCount != null)
                _deckCount.text = $"{ids.Count} 种 / {DeckBuildCount} 张";

            if (ids.Count == 0)
            {
                MakeEmpty(_deckTemplates, _deckList.Content, "（空卡组——右侧卡池点「添加」入组）", false);
                return;
            }

            // 普通卡行（P9b：模板不进 cardIds——区独立于卡表行）
            for (int i = 0; i < ids.Count; i++)
                MakeDeckRow(ids[i], CardCatalog.GetById(ids[i]), counts[i]);

            // 分区：引用卡（2026-11-13 自动引用改版）——卡组效果引用的模板卡随组自动收进
            // 衍生物区（RecomputeTokenZone 单源），此处只读展示（不可删——断引用须移除引用卡）
            var externalRefs = CollectExternalReferences();
            if (externalRefs.Count > 0)
            {
                MakeSectionHeader($"引用卡（自动）·{externalRefs.Count}", _deckList.Content);
                foreach (var refId in externalRefs)
                    MakeReferenceRow(refId);
            }
        }

        private void MakeDeckRow(string cardId, CardData card, int copies)
        {
            var row = Clone(_deckTemplates, "tpl-row-deck", _deckList.Content);
            if (row == null) return;

            // 2026-11-13 预组只读查看：构成行 ± 双钮全隐（加删口另有兜底闸），行点击预览保留
            bool readOnly = DeckCatalog.IsPreset(_selectedDeckName);
            var rowBtn = row.GetComponent<UButton>();
            if (rowBtn != null) rowBtn.onClick.RemoveAllListeners();

            if (card == null)
            {
                // 断链卡：无卡面数据可展示——只能减册（+ 不给——卡表无此卡加不进有效册）
                SetVisible(row, "cost", false);
                SetText(row, "name", $"[断链] {cardId} ×{copies}", UiStyle.DotRed);
                SetVisible(row, "flag", false);
                SetVisible(row, "btn-add", false);
                if (readOnly) SetVisible(row, "btn-del", false);
                else BindRowButton(row, "btn-del", () => RemoveCopy(cardId));
                return;
            }

            var captured = card;
            BindCost(row, card);
            SetText(row, "name", NameOf(card) + (copies > 1 ? $" ×{copies}" : ""));

            // 异常/槽位标记：形状违规红字优先；带槽卡（str 空=旧档未设置槽）显示「衍生物槽N·缺M」
            //（缺=红——补设须到卡组合界面「设置衍生物」；全兜底=灰 ✓）
            var flags = CardFlags(card);
            var slots = DeckTokenSupport.EnumerateSlots(card).Where(s => s.NeedsBinding).ToList();
            int missing = slots.Count(s => BindingOf(s.CardId, s.EffectIndex) == null);
            if (flags.Count > 0)
            {
                SetVisible(row, "flag", true);
                SetText(row, "flag", "✘" + string.Join("/", flags), UiStyle.DotRed);
            }
            else if (slots.Count > 0)
            {
                SetVisible(row, "flag", true);
                SetText(row, "flag",
                    missing > 0 ? $"衍生物槽{slots.Count}·缺{missing}" : $"衍生物槽{slots.Count}✓",
                    missing > 0 ? UiStyle.DotRed : UiStyle.TextHint);
            }
            else SetVisible(row, "flag", false);

            // 2026-11-13 改版：删除钮退役——行尾 +/− 双钮（+ 加册=AddCard 口径含限量拦截，
            // − 减册=RemoveCopy；计数归零行自然消失）；预组只读查看态双钮全隐
            if (readOnly)
            {
                SetVisible(row, "btn-add", false);
                SetVisible(row, "btn-del", false);
            }
            else
            {
                BindRowButton(row, "btn-add", () => AddCard(cardId));
                BindRowButton(row, "btn-del", () => RemoveCopy(cardId));
            }
            // P4 §2（2026-10-29）：带槽卡行点击=中栏预览并展开衍生物（读当前卡组绑定）
            if (rowBtn != null)
            {
                rowBtn.onClick.AddListener(() => ShowPreview(captured));
            }
        }

        /// <summary>引用卡行（2026-11-13 自动引用改版）：卡组效果引用但不在卡组内的卡
        /// （SummonToken/GrantMorph 目标）——行点击=预览、无 +/−（要断引用须移除引用它的卡）；
        /// flag 位灰字「引用」区分。断链引用红字。</summary>
        private void MakeReferenceRow(string refId)
        {
            var row = Clone(_deckTemplates, "tpl-row-deck", _deckList.Content);
            if (row == null) return;

            var rowBtn = row.GetComponent<UButton>();
            if (rowBtn != null) rowBtn.onClick.RemoveAllListeners();
            SetVisible(row, "btn-add", false);
            SetVisible(row, "btn-del", false);

            var card = CardCatalog.GetById(refId);
            if (card == null)
            {
                SetVisible(row, "cost", false);
                SetText(row, "name", "[断链] " + refId, UiStyle.DotRed);
                SetVisible(row, "flag", false);
                return;
            }

            BindCost(row, card);
            SetText(row, "name", NameOf(card));
            SetText(row, "flag", "引用", UiStyle.TextHint);
            if (rowBtn != null)
                rowBtn.onClick.AddListener(() => ShowPreview(card));
        }

        /// <summary>外部引用行枚举：衍生物区（自动=全部引用 ∪ 旧档绑定目标，RecomputeTokenZone
        /// 已在 RefreshAll 先行重算）中不在主卡组内的 id——区内含在组卡时只列外部的
        ///（在组普通卡留在普通区不重复列）。收集口径单源=DeckTokenSupport.CollectReferencedTemplateIds。</summary>
        private List<string> CollectExternalReferences()
        {
            var inDeck = new HashSet<string>(_deckCardIds);
            return _tokenCardIds.Where(id => !string.IsNullOrEmpty(id) && !inDeck.Contains(id)).ToList();
        }

        /// <summary>单卡异常标记（左栏行尾红字/中栏 note，短词）：形状违规。</summary>
        private static List<string> CardFlags(CardData card)
        {
            var flags = new List<string>();
            if (DeckCompositionValidator.CheckShape(card).Count > 0) flags.Add("形状");
            return flags;
        }

        private void RefreshSummary()
        {
            float totalCost = 0f;
            foreach (var id in _deckCardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card != null) totalCost += card.TotalCost;
            }
            if (_summary != null)
            {
                int tokens = _tokenCardIds.Count; // P9b：区独立于 cardIds
                _summary.text = $"卡数 {DeckBuildCount}（需 {MinDeckCards}-{MaxDeckCards}）"
                                + (tokens > 0 ? $" + 衍生物{tokens}（额外区）" : "")
                                + $" · 合计费用 {(int)totalCost}";
            }
        }

        // ======================================== 中栏：只读卡面 + 技能栏 ========================================

        private void ShowPreview(CardData card)
        {
            _previewCard = card;
            BuildPreview();
            // 行点击路径不走 RefreshAll——卡面层单独重绑
            RebindCardLayer();
        }

        /// <summary>中栏卡面（2026-10-28 preview-panel 重构）：烘焙 ACard 直挂 View——
        /// FromCardDataFull 六区静态快照（与手牌/合成器同族，去运行态），360×648 原尺寸不缩放；
        /// 空卡=藏卡；点卡面放大全屏（Button 在 Build 接线）。
        /// P4 §2（2026-10-29）：选中带槽卡 → 预览区右下展开衍生物（BuildTokenExpansion）。</summary>
        private void BuildPreview()
        {
            var card = _previewCard;
            if (card == null)
            {
                if (_previewView != null) _previewView.gameObject.SetActive(false);
                RefreshPreviewCost(null);
                SetTokenExpansion(null, null);
                return;
            }
            var item = CardOverlayItem.FromCardDataFull(card, "Preview");
            if (_previewView != null)
            {
                _previewView.gameObject.SetActive(true);
                _previewView.Apply(item, CardOverlayLayout.Full);
                WirePreviewEffectRows(item); // 2026-11-13 技能设置改版：主动效果行点击=弹窗确认后切换英雄技能
            }
            RefreshPreviewCost(item);
            BuildTokenExpansion(card);
            // 形状校验提示行（preview-note）已随 2026-10-28 preview-panel 重构退役——
            // 违规明细仍由构成块/保存拦截展示（DeckCompositionValidator.CheckShape 单源不变）
        }

        /// <summary>预览主动效果行点击接线（2026-11-13 技能设置改版）：点行=弹窗确认后切换英雄技能
        ///（2026-11-13 补确认：确认前置在 ConfirmSetSkill，SetSkill 内仍留资格闸兜底）。
        /// 多主动=ActiveEffectRows 序（ACardView 克隆行）、
        /// 单主动=模板行（ActiveEffectText 非空才可见）；effect 行无 Button（prefab 只烘 Outline）
        /// ——运行时补挂属「补挂非视觉组件」。ACardView.Apply 复用/重建行对象，每次全量重接
        ///（RemoveAllListeners 防叠加）。行点击会拦截卡面根点击（放大）——UGUI 射线命中最深
        /// 处理器即本行，符合「点行设技能、点卡面放大」语义。</summary>
        private void WirePreviewEffectRows(CardOverlayItem item)
        {
            if (_previewView == null || _previewCard == null) return;
            var rows = new List<RectTransform>();
            if (_previewView.ActiveEffectRows.Count > 0)
                rows.AddRange(_previewView.ActiveEffectRows);
            else if (!string.IsNullOrEmpty(item?.ActiveEffectText) && _previewView.EffectTemplateRow != null)
                rows.Add(_previewView.EffectTemplateRow);
            foreach (var row in rows)
            {
                if (row == null || !row.gameObject.activeSelf) continue;
                var btn = row.GetComponent<UButton>();
                if (btn == null)
                {
                    btn = row.gameObject.AddComponent<UButton>();
                    btn.transition = UButton.Transition.None;
                }
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    Debug.Log("[UI点击] preview-effect-row");
                    ConfirmSetSkill(_previewCard); // 2026-11-13：先弹窗确认，防误触直接换掉角色技能
                });
            }
        }

        /// <summary>预览卡费条（2026-10-28 preview-panel 重构）：preview-stage/CostSquares 烘焙
        /// 实例直填——空卡=Clear 清空；0费=单块灰「0」占位（2026-11-03，条本体常驻，prefab 布局恒定）。
        /// P4 §1（2026-10-29）：item.Costs 已是净费口径（CardNetCost.NetCostOf 实时推导）。
        /// Cost2 代价组（2026-11-06 预览补齐）：与手牌同口径——FromCardDataFull 已填 PayloadCost
        ///（施放期代价色构成），null=藏 Cost2（SetPayloadCosts 现成语义）。</summary>
        private void RefreshPreviewCost(CardOverlayItem item)
        {
            if (_previewCost == null) return;
            if (item != null)
            {
                _previewCost.SetCosts(item.Costs ?? new ElementCost());
                _previewCost.SetPayloadCosts(item.PayloadCost);
            }
            else _previewCost.Clear();
        }

        // ======================================== P4 §2：衍生物选中才展开（2026-10-29） ========================================

        /// <summary>选中卡的衍生物展开（P4 §2；2026-11-13 自动引用改版）：固定槽（原子 str 直书，
        /// 卡组合界面设置）按槽解析；str 空槽（旧档）读兜底绑定。模板可解析 → 右下半尺寸第二张卡 +
        /// 逐槽明细行；未设置/引用失效 → 占位行+原因（不静默消失）；无槽卡 → 整个展开区隐藏。</summary>
        private void BuildTokenExpansion(CardData card)
        {
            var slots = DeckTokenSupport.EnumerateSlots(card);
            if (slots.Count == 0) { SetTokenExpansion(null, null); return; }

            var rows = new List<string>();
            CardData firstBound = null;
            foreach (var g in slots.GroupBy(s => s.EffectIndex).OrderBy(g => g.Key))
            {
                var s0 = g.First();
                string kind = s0.IsMorph ? "变形" : "衍生物";
                string label = $"效果{s0.EffectIndex + 1}（{s0.EffectDisplayName ?? "?"}）{kind}槽";
                string templateId = s0.NeedsBinding ? BindingOf(card.ID, s0.EffectIndex) : s0.FixedTemplateId;
                if (string.IsNullOrEmpty(templateId))
                {
                    rows.Add($"{label} · 未设置（卡组合界面「设置衍生物」）");
                    continue;
                }
                var tpl = CardCatalog.GetById(templateId);
                if (tpl == null)
                {
                    rows.Add($"{label} · 引用失效：模板 {templateId} 在卡表查不到");
                    continue;
                }
                rows.Add($"{label} → {NameOf(tpl)}");
                if (firstBound == null) firstBound = tpl;
            }
            SetTokenExpansion(rows, firstBound);
        }

        /// <summary>展开区落值：rows=null → 整区隐藏并撤第二卡绑定；否则常驻明细行 +
        ///（有可解析模板时）第二张卡挂 token-expansion/slot（半尺寸叠卡右下）。</summary>
        private void SetTokenExpansion(List<string> rows, CardData boundCard)
        {
            var host = EnsureTokenExpansionHost();
            if (host == null) { _tokenBinding = null; return; }
            if (rows == null || rows.Count == 0)
            {
                host.gameObject.SetActive(false);
                _tokenBinding = null;
                RebindCardLayer();
                return;
            }
            host.gameObject.SetActive(true);
            var body = UiKit.FindDeep(host, "rows")?.GetComponentInChildren<TMP_Text>(true);
            if (body != null) body.text = string.Join("\n", rows);
            if (_tokenSlot != null) _tokenSlot.gameObject.SetActive(boundCard != null);
            _tokenBinding = boundCard != null && _tokenSlot != null
                ? new CardOverlayBinding
                {
                    Slot = _tokenSlot,
                    Item = CardOverlayItem.FromCardDataFull(boundCard, "TokenPreview"),
                    Layout = CardOverlayLayout.Full,
                }
                : null;
            RebindCardLayer();
        }

        /// <summary>展开区宿主（preview-stage 内运行时建，一次）：右下角叠卡位 + 明细行 + 绑定入口。
        /// 缺 preview-stage（烘焙 ACard 缺父）→ null 降级（数据仍走校验行，不崩）。</summary>
        private RectTransform EnsureTokenExpansionHost()
        {
            if (_tokenExpansion != null) return _tokenExpansion;
            if (_previewStage == null) return null;

            var host = new GameObject("token-expansion", typeof(RectTransform)).transform as RectTransform;
            host.SetParent(_previewStage, false);
            host.anchorMin = host.anchorMax = new Vector2(1f, 0f);
            host.pivot = new Vector2(1f, 0f);
            host.anchoredPosition = new Vector2(-2f, 2f);
            host.sizeDelta = new Vector2(200f, 350f);

            // 第二张卡槽：设计矩形 360×648 · 半尺寸呈现（卡面层子实例 Stretch 铺满槽）
            var slotGo = new GameObject("slot", typeof(RectTransform));
            _tokenSlot = (RectTransform)slotGo.transform;
            _tokenSlot.SetParent(host, false);
            _tokenSlot.anchorMin = _tokenSlot.anchorMax = new Vector2(0.5f, 0.5f);
            _tokenSlot.pivot = new Vector2(0.5f, 0.5f);
            _tokenSlot.sizeDelta = new Vector2(360f, 648f);
            _tokenSlot.localScale = Vector2.one * 0.52f; // → 约 187×337，叠在预览卡右下角

            // 标题 + 明细行（占位行/绑定去向都在此，不静默消失）
            UiKit.Label("head", host, "衍生物", 13, UiStyle.TextSecondary);
            UiKit.Label("rows", host, "", 12, UiStyle.TextHint);

            // 设置入口（2026-11-13 改版：绑定面板退役——跳卡组合界面「设置衍生物」直写 str）
            UiKit.Button("btn-bind", host, "设置衍生物…", () =>
            {
                if (_previewCard != null) EditCard(_previewCard);
            }, height: 24f, fontSize: 12);
            return _tokenExpansion = host;
        }

        /// <summary>放大预览卡（验收2）：全屏遮罩 + 600×1080 放大卡居中（仍走卡面层池化——
        /// 2026-10-28 重构后中栏预览=烘焙 ACard 直挂，控制器只剩放大卡一职）；放大卡本体不接
        /// OnClick——点遮罩/空白还原。尺寸口径（2026-10-28 新卡 360×648 分数锚替换）：zoom-card
        /// 槽位尺寸直接驱动（见 RebindCardLayer），不再实例 localScale ×3。
        /// P4 §2（2026-10-29）：带绑定槽卡放大=主卡右侧并排第二张衍生物卡。</summary>
        private void OpenZoom(CardData card)
        {
            if (card == null || _zoomOverlay == null || _zoomSlot == null) return;
            _zoomBindings.Clear();
            _zoomBindings.Add(new CardOverlayBinding
            {
                Slot = _zoomSlot,
                Item = CardOverlayItem.FromCardDataFull(card, "Zoom"),
                Layout = CardOverlayLayout.Full,
            });
            // 第二张衍生物卡（首个可解析绑定；槽懒建一次，CloseZoom 隐藏）
            var slots = DeckTokenSupport.EnumerateSlots(card);
            CardData boundToken = null;
            foreach (var g in slots.GroupBy(s => s.EffectIndex).OrderBy(g => g.Key))
            {
                var s0 = g.First();
                var templateId = s0.NeedsBinding ? BindingOf(card.ID, s0.EffectIndex) : s0.FixedTemplateId;
                var tpl = string.IsNullOrEmpty(templateId) ? null : CardCatalog.GetById(templateId);
                if (tpl != null) { boundToken = tpl; break; }
            }
            if (boundToken != null)
            {
                if (_zoomTokenSlot == null)
                {
                    var go = new GameObject("zoom-token-card", typeof(RectTransform));
                    _zoomTokenSlot = (RectTransform)go.transform;
                    _zoomTokenSlot.SetParent(_zoomOverlay, false);
                    _zoomTokenSlot.anchorMin = _zoomTokenSlot.anchorMax = new Vector2(0.5f, 0.5f);
                    _zoomTokenSlot.pivot = new Vector2(0.5f, 0.5f);
                    _zoomTokenSlot.anchoredPosition = new Vector2(640f, 0f);
                    _zoomTokenSlot.sizeDelta = new Vector2(360f, 648f);
                }
                _zoomTokenSlot.gameObject.SetActive(true);
                _zoomBindings.Add(new CardOverlayBinding
                {
                    Slot = _zoomTokenSlot,
                    Item = CardOverlayItem.FromCardDataFull(boundToken, "ZoomToken"),
                    Layout = CardOverlayLayout.Full,
                });
            }
            else if (_zoomTokenSlot != null) _zoomTokenSlot.gameObject.SetActive(false);
            _zoomOverlay.gameObject.SetActive(true);
            RebindCardLayer();
        }

        /// <summary>还原放大态：关遮罩+清放大绑定（卡面层回收放大卡实例）。</summary>
        private void CloseZoom()
        {
            if (_zoomBindings.Count == 0) return;
            _zoomBindings.Clear();
            if (_zoomTokenSlot != null) _zoomTokenSlot.gameObject.SetActive(false);
            if (_zoomOverlay != null) _zoomOverlay.gameObject.SetActive(false);
            RebindCardLayer();
        }

        /// <summary>放大卡缩放（2026-10-28 晚用户定案回退：整体一起放大——文字随 localScale
        /// 同比渲染，版式恒同比；600×1080 视觉=设计矩形 360×648 × 5/3）。字号三档烘在 prefab：
        /// 名称 25（18-25 自动缩号）/关键词行 18/效果文本 16——缩放不改字号。</summary>
        private const float ZoomCardScale = 1080f / 648f;

        /// <summary>放大卡槽=设计矩形（每次重绑前归位；prefab 内 zoom-card 旧值不作数）。</summary>
        private static readonly Vector2 ZoomCardDesignSize = new Vector2(360f, 648f);

        /// <summary>UGUI 卡面层重绑（preview-panel 重构后=放大卡+衍生物第二卡；中栏预览=烘焙 ACard
        /// 直挂不经控制器）。放大=设计矩形槽 + 实例 localScale×ZoomCardScale（整体同比放大，
        /// 2026-10-28 晚回退定案——文字随变换缩放，字号三档恒定不动态改）；
        /// 预览区第二卡=token-expansion/slot 自带 localScale 0.52（槽级缩放，子实例随之）。</summary>
        private void RebindCardLayer()
        {
            if (_zoomSlot != null)
            {
                _zoomSlot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ZoomCardDesignSize.x);
                _zoomSlot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ZoomCardDesignSize.y);
            }
            var all = new List<CardOverlayBinding>(_zoomBindings.Count + 1);
            all.AddRange(_zoomBindings);
            if (_tokenBinding != null) all.Add(_tokenBinding); // P4 §2：预览区第二张衍生物卡
            CardOverlayController.Instance.Bind(all);

            if (_zoomSlot != null && _zoomSlot.childCount > 0)
                ((RectTransform)_zoomSlot.GetChild(0)).localScale = Vector2.one * ZoomCardScale;
            if (_zoomTokenSlot != null && _zoomTokenSlot.gameObject.activeSelf && _zoomTokenSlot.childCount > 0)
                ((RectTransform)_zoomTokenSlot.GetChild(0)).localScale = Vector2.one * ZoomCardScale;
        }

        /// <summary>技能栏刷新（2026-11-13 effect 行改版）：skill-slot = label（卡名标题）+
        /// effect 行（烘焙实例=第 1 行；多主动技能克隆 effect2/3…，Instantiate 共用预制体）。
        /// 行数据与法术主动效果行同源（CardOverlayItem：标题/全文/费用方格双组）——所见即
        /// 预览卡面同款；速度徽章固定「发动速度0」（技能恒速 0，不走 EffectSpeedOf 类别底盘）。
        /// 未设置=空态提示、行全隐。</summary>
        private void RefreshSkillSlot()
        {
            if (_skillLabel == null) return;
            var marked = string.IsNullOrEmpty(_skillCardId) ? null : CardCatalog.GetById(_skillCardId);
            if (marked == null)
            {
                _skillLabel.text = "英雄技能：未设置——选一张法术，点其预览的主动效果切换（保存前必须设置）";
                HideSkillRows();
                return;
            }

            _skillLabel.text = "英雄技能：" + NameOf(marked);
            if (_skillTemplate == null) return; // prefab 缺烘焙 effect 行——标题仍可用

            var item = CardOverlayItem.FromCardDataFull(marked, "SkillSlot");
            bool multi = item.ActiveEffectLines != null && item.ActiveEffectLines.Count > 1;
            if (multi) _skillLabel.text += $"（{item.ActiveEffectLines.Count} 个作用择一发动，每回合一次）";

            // 行集=模板行(+克隆)：旧克隆先清场（换技能/行数收敛），克隆源清费用方格防拷入
            // 未对账色块（ACardView 双块坑）、克隆后剥幽灵块（双保险）
            for (int i = _skillRowClones.Count - 1; i >= 0; i--)
            {
                if (_skillRowClones[i] != null) UnityEngine.Object.Destroy(_skillRowClones[i].gameObject);
                _skillRowClones.RemoveAt(i);
            }
            var rows = new List<RectTransform> { _skillTemplate };
            if (multi)
            {
                var tplCost = _skillTemplate.Find("text/lbl-skill-cost")?.GetComponent<CostSquaresView>();
                if (tplCost != null) tplCost.Clear();
                for (int i = 0; i < item.ActiveEffectLines.Count - 1; i++)
                {
                    var clone = UnityEngine.Object.Instantiate(_skillTemplate.gameObject, _skillSlot, false).transform as RectTransform;
                    ACardView.StripGhostSquares(clone);
                    clone.name = "effect" + (_skillRowClones.Count + 2); // effect2、effect3…
                    clone.SetSiblingIndex(_skillTemplate.GetSiblingIndex() + _skillRowClones.Count);
                    _skillRowClones.Add(clone);
                    rows.Add(clone);
                }
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                row.gameObject.SetActive(true);
                if (multi)
                {
                    SetText(row, "text",
                        i < (item.ActiveEffectLabels?.Count ?? 0) ? item.ActiveEffectLabels[i] : null);
                    SetText(row, "des", item.ActiveEffectLines[i]);
                    ACardView.SetRowCost(row,
                        i < (item.ActiveEffectCosts?.Count ?? 0) ? item.ActiveEffectCosts[i] : null,
                        i < (item.ActiveEffectPayloadCosts?.Count ?? 0) ? item.ActiveEffectPayloadCosts[i] : null);
                }
                else
                {
                    SetText(row, "des", item.ActiveEffectText); // 标题留烘焙默认「主动效果」
                    ACardView.SetRowCost(row, item.ActiveEffectCost, item.ActiveEffectPayloadCost);
                }
                // 速度徽章：技能恒速 0（HeroSkillSystem 口径），与法术行的类别推导无关
                SetVisible(row, "lbl-skill-speed", true);
                SetText(row, "lbl-skill-speed", "发动速度0");
            }
        }

        /// <summary>技能行清场（未设置）：克隆行销毁、模板行隐藏。</summary>
        private void HideSkillRows()
        {
            for (int i = _skillRowClones.Count - 1; i >= 0; i--)
            {
                if (_skillRowClones[i] != null) UnityEngine.Object.Destroy(_skillRowClones[i].gameObject);
                _skillRowClones.RemoveAt(i);
            }
            if (_skillTemplate != null) _skillTemplate.gameObject.SetActive(false);
        }

        /// <summary>切换英雄技能（2026-11-13 入口改版：点中栏预览的主动效果行；旧卡池行「技能」
        /// 钮退役）：资格=法术且 1~3 主动——不合格 toast 原因；选新即替换，无取消钮。
        /// 调用口只有确认弹窗后的 ConfirmSetSkill（2026-11-13 用户定案：点效果行不再直换技能）。</summary>
        private void SetSkill(CardData card)
        {
            var reason = HeroSkillSystem.SkillIneligibleReason(card);
            if (reason != null)
            {
                ShowToast("不可标记：" + reason);
                return;
            }
            _skillCardId = card.ID;
            ShowPreview(card); // 所见即所标：顺带展示该卡
            RefreshAll();
            ShowToast($"英雄技能已切换：{NameOf(card)}——开局落技能栏（不占卡组张数），每回合一次");
        }

        /// <summary>设英雄技能确认前置（2026-11-13 用户定案）：预览主动效果行点击=先弹窗确认
        /// 再换技能——旧口径直换，误触即丢原技能。前置拦截：同一张卡已是技能=只 toast；
        /// 资格不合格（生物/无主动/主动数超限）=只 toast（不为必失败的操作弹确认框）。</summary>
        private void ConfirmSetSkill(CardData card)
        {
            if (card == null) return;
            if (_skillCardId == card.ID)
            {
                ShowToast($"「{NameOf(card)}」已是当前英雄技能");
                return;
            }
            var reason = HeroSkillSystem.SkillIneligibleReason(card);
            if (reason != null)
            {
                ShowToast("不可标记：" + reason);
                return;
            }
            ConfirmSetSkillAsync(card).Forget();
        }

        /// <summary>设技能确认弹窗（TipPanel 双键，2026-10-18 弹窗统一口径）：文案区分「首次设置」
        /// 与「替换既有技能」（替换带原技能名）；确认后才走 SetSkill（资格闸仍在其内兜底）。</summary>
        private async UniTaskVoid ConfirmSetSkillAsync(CardData card)
        {
            var current = string.IsNullOrEmpty(_skillCardId) ? null : CardCatalog.GetById(_skillCardId);
            string msg = current == null
                ? $"把「{NameOf(card)}」设为英雄技能？\n英雄技能开局落技能栏（不占卡组张数），每回合一次。"
                : $"英雄技能将从「{NameOf(current)}」换成「{NameOf(card)}」——确定切换？";
            if (await Tips.ShowAsync(msg, "设为技能", "取消") != true) return;
            SetSkill(card);
        }

        // ======================================== 统计区（左栏底部·非滚动容器） ========================================

        /// <summary>统计区刷新（2026-11-10 预制体化；2026-11-13 口径改版）：stats-row 预制体
        ///（嵌套进 stats-zone）为烘焙正本——费用曲线 10 档柱组（tier 0..8/9+）+颜色分布，代码只按名
        /// 绑定填数；计数口径=DeckStatsAccountant 支付单元（卡单元+每主动效果单元，代价色优先/全 0 灰）；
        /// 构成检测提示行写 stats-zone/tip 行池（纯文本+直接设色，不再插富文本）。</summary>
        private void RefreshStats()
        {
            if (_statsZone == null) return;
            var cards = _deckCardIds
                .Select(id => CardCatalog.GetById(id))
                .Where(c => c != null)
                .ToList();
            // 2026-11-13 新口径：卡单元（卡费档）+ 每个主动效果各一单元（效果档）——
            // 代价>费按代价、0 费无代价灰、主动效果不再恒 0 费（DeckStatsAccountant 单源）
            var stats = DeckStatsAccountant.Account(cards);

            RefreshStatsTip();
            RefreshCostCurve(stats.Tiers);
            RefreshColorDistribution(stats);
        }

        /// <summary>构成检测 → stats-zone/tip（口径不变：DeckCompositionValidator 硬约束=断链/形状/
        /// 技能/黑白代价（任一不过不可保存）+ 地源 advisory（不拦保存——施放费须有足够可放地的卡，
        /// 整卡全施放面不可付才报「无法释放」）——加卡/删卡实时刷新。
        /// 2026-11-13 去富文本：纯文本行 + 直接设行颜色（tip 行池每行独立 TMP）。</summary>
        private void RefreshStatsTip()
        {
            if (_statsTip == null && _tipLines.Count == 0) return;
            var report = DeckCompositionValidator.Check(
                _nameField != null ? _nameField.text : _currentDeckName, _deckCardIds);
            var landShort = DeckCompositionValidator.CheckLandSupport(_deckCardIds);
            var costCap = DeckCompositionValidator.CheckCostOverNaturalCap(_deckCardIds);
            var bwGaps = DeckCompositionValidator.CheckBWCostCoverage(_deckCardIds);
            var tokenErrors = DeckTokenSupport.ValidateDeck(_deckCardIds, _tokenCardIds, _tokenBindings);
            bool skillOk = !string.IsNullOrEmpty(_skillCardId);

            RenderTipLines(BuildStatsTipLines(report, landShort, costCap, bwGaps, tokenErrors, skillOk));
        }

        /// <summary>tip 行构造（纯文本+颜色，无富文本标签——探针可断言不含 '&lt;'）：
        /// 绿 ✔ 通过行 / 红 ✘ 硬约束与 advisory（地源、费用超自动上限）/ 灰 空卡组提示。分项折叠同旧口径
        ///（衍生物 2 行、地源 2 行、费用上限 2 行）。</summary>
        internal static List<(string text, Color color)> BuildStatsTipLines(
            DeckCompositionValidator.Report report, List<DeckCompositionValidator.LandShortage> landShort,
            List<DeckCompositionValidator.CostCapGap> costCap,
            List<DeckCompositionValidator.BWGap> bwGaps, List<string> tokenErrors, bool skillOk)
        {
            var lines = new List<(string, Color)>();

            // ---- 硬约束（任一不过=不可保存）----
            if (report.CardCount > 0 && report.Ok && skillOk && bwGaps.Count == 0 && tokenErrors.Count == 0)
                lines.Add(("✔ 构成约束通过（断链/形状/技能/黑白代价/衍生物引用）"
                           + (landShort.Count == 0 ? "·地源足" : ""), UiStyle.DotGreen));
            else if (report.CardCount == 0)
                lines.Add(("（空卡组——右侧卡池点「添加」入组）", UiStyle.TextHint));

            foreach (var id in report.MissingIds)
                lines.Add(($"✘ 断链卡 {id}——卡表中无此 ID（该卡可能已被删除），请移除", UiStyle.DotRed));
            foreach (var err in report.ShapeErrors)
                lines.Add(($"✘ {err}", UiStyle.DotRed));
            if (!skillOk)
                lines.Add(("✘ 未设置英雄技能（选一张法术，点其预览的主动效果切换）——无法保存",
                    UiStyle.DotRed));
            foreach (var gap in bwGaps)
                lines.Add(($"✘ {gap.CardName} 等卡施放需{DeckCompositionValidator.ColorZh(gap.Color)}元素，"
                           + $"卡组没有能补偿{DeckCompositionValidator.ColorZh(gap.Color)}的代价卡（至少带 1 张）——无法保存",
                    UiStyle.DotRed));
            // 衍生物引用（硬约束——最多显两行）
            int tokenShown = 0;
            foreach (var err in tokenErrors)
            {
                if (tokenShown++ >= 2) break;
                lines.Add(($"✘ {err}", UiStyle.DotRed));
            }
            if (tokenErrors.Count > 2)
                lines.Add(($"……另有 {tokenErrors.Count - 2} 项衍生物引用问题", UiStyle.DotRed));

            // ---- 地源不足（advisory，不拦保存；两行折叠）----
            const int maxLandRows = 2;
            int landShown = 0;
            foreach (var s in landShort)
            {
                if (landShown++ >= maxLandRows) break;
                lines.Add(($"✘ {s.CardName} ×{s.Copies}｜{s.Surface}｜{s.Detail}——无法释放", UiStyle.DotRed));
            }
            if (landShort.Count > maxLandRows)
                lines.Add(($"……另有 {landShort.Count - maxLandRows} 张卡地源不足无法释放", UiStyle.DotRed));

            // ---- 费用超自动上限（advisory，不拦保存；2026-11-14 用户定案；两行折叠）----
            // 只在本卡组**不带成长**时出现（CheckCostOverNaturalCap 已按成长口径过滤）：
            // 地牌上限只能自然曲线长，自动上限=LandCurveCap（现值 10），超过的卡本局打不出。
            const int maxCostRows = 2;
            int costShown = 0;
            foreach (var gap in costCap)
            {
                if (costShown++ >= maxCostRows) break;
                lines.Add(($"✘ {gap.CardName} ×{gap.Copies}｜费用 {gap.Cost}｜超过自动上限 {gap.Cap}"
                           + "（本卡组不带「成长」）——本局打不出", UiStyle.DotRed));
            }
            if (costCap.Count > maxCostRows)
                lines.Add(($"……另有 {costCap.Count - maxCostRows} 张卡费用超自动上限", UiStyle.DotRed));
            return lines;
        }

        /// <summary>tip 行池绑定（2026-11-13 去富文本改版）：stats-zone/tip 下烘焙 tip-line-0..N
        ///（每行独立 TMP 独立颜色；行数自适应=预制体手调，断号即尽）；池空降级单条 _statsTip。</summary>
        private void BindTipLinePool()
        {
            _tipLines.Clear();
            if (_statsZone == null) return;
            for (int i = 0; i < 32; i++) // 防御上限——行池不会烘焙超过此数
            {
                var node = UiKit.FindDeep(_statsZone, "tip-line-" + i);
                var tmp = node != null ? node.GetComponent<TMP_Text>() : null;
                if (tmp == null) break;
                _tipLines.Add(tmp);
            }
        }

        /// <summary>tip 渲染：行池=逐行纯文本+直接设色、未用行隐藏、超出池折叠「……另有 N 项」；
        /// 兜底（预制体未烘焙行池）=单条 _statsTip 整条单色（有错红/全过绿/空灰）。</summary>
        private void RenderTipLines(List<(string text, Color color)> lines)
        {
            if (_tipLines.Count > 0)
            {
                if (lines.Count > _tipLines.Count)
                {
                    int hidden = lines.Count - _tipLines.Count + 1; // 折叠行自身占一行
                    lines = lines.GetRange(0, _tipLines.Count - 1);
                    lines.Add(($"……另有 {hidden} 项未展开", UiStyle.TextHint));
                }
                for (int i = 0; i < _tipLines.Count; i++)
                {
                    bool used = i < lines.Count;
                    if (_tipLines[i].gameObject.activeSelf != used)
                        _tipLines[i].gameObject.SetActive(used);
                    if (used)
                    {
                        _tipLines[i].text = lines[i].text;
                        _tipLines[i].color = lines[i].color;
                    }
                }
            }
            else if (_statsTip != null)
            {
                Color c = UiStyle.TextHint;
                if (lines.Count == 1) c = lines[0].color;
                else if (lines.Count > 1) c = UiStyle.DotRed; // 多行=必有 ✘（绿通过恒单行）
                _statsTip.text = string.Join("\n", lines.Select(l => l.text));
                _statsTip.color = c;
            }
        }

        /// <summary>费用曲线（2026-11-13 新口径）：喂 DeckStatsAccountant 支付单元档位
        ///（卡单元+每主动效果单元——tier 标签 0..8、9+ 与粗分档【0-2】【3-5】【6-8】【9+】同源）。
        /// 柱高=单元数/最大值×52（bar 的 LE preferredHeight，空值 2），count=单元数（0 藏字）；
        /// 列序无关——按烘焙 tier 文本对桶。</summary>
        private void RefreshCostCurve(int[] tiers)
        {
            var body = _statsRow != null ? UiKit.FindDeep(_statsRow, "body") : null;
            if (body == null || tiers == null || tiers.Length < 10) return;
            int max = Mathf.Max(1, tiers.Max());
            for (int i = 0; i < body.childCount; i++)
            {
                var col = body.GetChild(i) as RectTransform;
                if (col == null || col.name != "stat-bar") continue;
                int t = TierIndexOf(UiKit.FindDeep(col, "tier")?.GetComponent<TMP_Text>());
                if (t < 0) continue; // 烘焙标签改样——跳过该列（宁缺勿错桶）
                var bar = UiKit.FindDeep(col, "bar");
                var barLe = bar != null ? bar.GetComponent<LayoutElement>() : null;
                if (barLe != null)
                    barLe.preferredHeight = tiers[t] > 0
                        ? Mathf.Max(6f, (int)(52f * tiers[t] / max)) : 2f;
                SetText(col, "count", tiers[t] > 0 ? tiers[t].ToString() : "");
            }
        }

        /// <summary>烘焙 tier 标签→桶序：「0」..「8」直取、「9+」→9；认不出（标签被手改变样）
        /// 返回 -1（调用方跳过该列）。</summary>
        private static int TierIndexOf(TMP_Text tier)
        {
            var s = tier != null ? (tier.text ?? "").Trim() : "";
            if (s == "9+") return 9;
            return int.TryParse(s, out int v) && v >= 0 && v <= 8 ? v : -1;
        }

        /// <summary>颜色分布（2026-11-13 新口径）：写「颜色分布」标题 header 的正文 label
        ///（标题之外那个 TMP——烘焙占位即彩点富文本）。计数=DeckStatsAccountant 支付单元
        ///（有代价→代价色、无代价→费色、全 0→灰、多色各计一次）；文案格式=CostChartView.ColorLineOf
        /// 计数重载单源（两行制表，色值=UiStyle.Dot* 令牌——与烘焙占位一致）。</summary>
        private void RefreshColorDistribution(DeckStatsAccountant.Stats stats)
        {
            var content = StatsHeaderBodyLabel("颜色分布");
            if (content == null || stats == null) return;
            content.text = CostChartView.ColorLineOf(stats.Red, stats.Blue, stats.Green, stats.Gray);
        }

        /// <summary>stats-row 两 header 同名——按标题文本定位（「费用曲线」/「颜色分布」），
        /// 返回该 header 的正文 TMP=标题 label 之外的第一个 label（费用曲线 header 无正文→null）。</summary>
        private TMP_Text StatsHeaderBodyLabel(string title)
        {
            if (_statsRow == null) return null;
            for (int i = 0; i < _statsRow.childCount; i++)
            {
                var header = _statsRow.GetChild(i) as RectTransform;
                if (header == null || header.name != "header") continue;
                TMP_Text titleText = null, bodyText = null;
                foreach (var t in header.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.text == title) { titleText = t; continue; }
                    if (bodyText == null) bodyText = t;
                }
                if (titleText == null) continue; // 非目标 header
                return bodyText;
            }
            return null;
        }

        // ======================================== 右栏：卡组列表 ========================================

        /// <summary>卡组列表刷新（2026-10-22 预组分栏·2026-10-23 模板化·2026-11-13 预组可点开）：
        /// 上方独立「预组卡组（只读·复制后可改）」区块——行点击=只读载入左栏查看构成（± 隐藏，
        /// 修改走复制）；下方「我的卡组」玩家档区块，行=[卡组名|张数·不可用标记|✕删除]，
        /// 行点击=载入左栏编辑。不可用口径=DeckCompositionValidator.UnusableTag（断链/形状
        /// 任一即不可用，与挑战/对战入口全局同源——2026-10-16 对齐定案）。</summary>
        private void RefreshDecksList()
        {
            if (_decksList == null) return;
            ClearContent(_decksList.Content);

            // ---- 预组卡组（只读）分栏 ----
            var presets = DeckCatalog.LoadPresets();
            if (presets.Count > 0)
            {
                MakeSectionHeader("预组卡组（只读·复制后可改）", _decksList.Content);
                foreach (var preset in presets)
                {
                    if (preset == null || string.IsNullOrEmpty(preset.name)) continue;
                    MakePresetRow(preset);
                }
            }
            MakeSectionHeader("我的卡组", _decksList.Content);

            var decks = DeckSerializer.LoadAll();
            if (decks.Count == 0)
            {
                MakeEmpty(_catTemplates, _decksList.Content, "（无存档卡组——左栏构筑后点「保存」）", false);
                return;
            }
            foreach (var deck in decks)
            {
                if (deck == null || string.IsNullOrEmpty(deck.name)) continue;
                MakeDeckListRow(deck);
            }
        }

        /// <summary>卡组列表行（tpl-row-decklib 克隆）。</summary>
        private RectTransform MakeDeckLibRow()
        {
            var row = Clone(_catTemplates, "tpl-row-decklib", _decksList.Content);
            if (row == null) return null;
            var rowBtn = row.GetComponent<UButton>();
            if (rowBtn != null) rowBtn.onClick.RemoveAllListeners();
            return row;
        }

        private void MakeDeckListRow(DeckData deck)
        {
            var row = MakeDeckLibRow();
            if (row == null) return;

            SetText(row, "name", deck.name);
            var tag = DeckCompositionValidator.UnusableTag(deck);
            SetText(row, "meta", DeckCountText(deck) + (tag.Length > 0 ? "·" + tag : ""),
                tag.Length > 0 ? UiStyle.DotRed : (Color?)null);
            SetVisible(row, "btn-copy", false); // 玩家档无复制钮

            var captured = deck.name;
            BindRowButton(row, "btn-del", () => ConfirmDeleteDeck(captured));

            var bg = row.GetComponent<Image>();
            if (bg != null) bg.color = deck.name == _selectedDeckName ? UiStyle.SelectedRowBg : UiStyle.RowBg;

            var rowBtn = row.GetComponent<UButton>();
            if (rowBtn != null)
                rowBtn.onClick.AddListener(() => LoadDeck(captured));
        }

        /// <summary>卡组列表分区标题行（tpl-section 克隆；右栏卡组库分栏与左栏衍生物/引用卡分区共用）。</summary>
        private void MakeSectionHeader(string title, RectTransform content)
        {
            var header = Clone(_catTemplates, "tpl-section", content);
            if (header == null) return;
            SetText(header, "label", title);
            UiKit.Size(header, fw: 1f);
        }

        /// <summary>卡组张数文案口径（预组/玩家档通用）：正卡数+衍生物M（模板不计张数——额外区配套；
        /// P9b：模板自 tokenCardIds 取，读取时旧档已规范化迁出 cardIds）。</summary>
        private static string DeckCountText(DeckData deck)
        {
            int main = deck?.cardIds?.Count ?? 0;
            int tokens = deck?.tokenCardIds?.Count ?? 0;
            return tokens > 0 ? $"{main} 张+衍生物{tokens}" : $"{main} 张";
        }

        /// <summary>预组行：[名·预组|张数|复制钮]；行点击=只读载入左栏查看构成（2026-11-13 改版：
        /// ± 双钮隐藏+加删口拦截，选中高亮对账）——修改仍走「复制」（引用复制防脆弱浅拷贝）。</summary>
        private void MakePresetRow(DeckData preset)
        {
            var row = MakeDeckLibRow();
            if (row == null) return;

            SetText(row, "name", preset.name + "·预组");
            var tag = DeckCompositionValidator.UnusableTag(preset);
            SetText(row, "meta", DeckCountText(preset) + (tag.Length > 0 ? "·" + tag : ""),
                tag.Length > 0 ? UiStyle.DotRed : (Color?)null);
            SetVisible(row, "btn-del", false); // 预组不可删

            var captured = preset;
            BindRowButton(row, "btn-copy", () => ConfirmCopyPreset(captured.name));

            var bg = row.GetComponent<Image>();
            if (bg != null) bg.color = preset.name == _selectedDeckName ? UiStyle.SelectedRowBg : UiStyle.RowBg;

            var rowBtn = row.GetComponent<UButton>();
            if (rowBtn != null)
                rowBtn.onClick.AddListener(() => LoadPresetDeck(captured));
        }

        /// <summary>预组只读载入（2026-11-13）：构成进左栏查看——± 隐藏、AddCard/RemoveCopy 兜底
        /// 拦截、原名保存被既有预组名闸拦；装载体同 LoadDeck（技能自愈/衍生物区/绑定随档）。
        /// 改动仍走「复制」成玩家副本（防改名另存出引用教学 id 的浅拷贝）。</summary>
        private void LoadPresetDeck(DeckData preset)
        {
            _deckCardIds.Clear();
            if (preset.cardIds != null)
                _deckCardIds.AddRange(preset.cardIds);
            _skillCardId = null;
            if (!string.IsNullOrEmpty(preset.skillCardId)
                && CardCatalog.GetById(preset.skillCardId) is { } skill
                && HeroSkillSystem.CanBeSkillCard(skill))
            {
                _skillCardId = preset.skillCardId;
            }
            _tokenCardIds.Clear();
            if (preset.tokenCardIds != null)
                _tokenCardIds.AddRange(preset.tokenCardIds.Where(id => !string.IsNullOrEmpty(id)));
            _tokenBindings.Clear();
            if (preset.tokenBindings != null)
                _tokenBindings.AddRange(preset.tokenBindings.Where(b => b != null && !string.IsNullOrEmpty(b.cardId)));
            _selectedDeckName = preset.name;
            _currentDeckName = preset.name;
            if (_nameField != null)
                _nameField.SetTextWithoutNotify(preset.name);
            _previewCard = null;
            RefreshDecksList(); // 选中高亮对账
            RefreshAll();
            ShowToast($"已载入预组（只读）：{preset.name}——点「复制」生成副本后可修改");
        }

        private void ConfirmCopyPreset(string presetName)
            => ConfirmCopyPresetAsync(presetName).Forget();

        /// <summary>复制预组确认（TipPanel 双键）：文案注明引用复制口径（卡/衍生物模板/技能卡
        /// 直引预组原件，卡与效果本体不重复落表），确认后走 DeckCopyService 引用复制并载入副本。</summary>
        private async UniTaskVoid ConfirmCopyPresetAsync(string presetName)
        {
            var deck = DeckCatalog.Load(presetName);
            if (deck == null || deck.cardIds == null || deck.cardIds.Count == 0)
            {
                ShowToast("预组读取失败");
                return;
            }
            int tokens = deck.tokenCardIds?.Count ?? 0; // P9b：区独立（读取已规范化）
            int main = deck.cardIds.Count;
            string skillPart = string.IsNullOrEmpty(deck.skillCardId) ? "" : "、技能卡";
            if (await Tips.ShowAsync(
                    $"复制预组「{presetName}」为可修改副本？\n" +
                    $"副本将直接引用卡组中的 {main} 张卡（含 {tokens} 张衍生物模板）{skillPart}，卡与效果本体不重复复制。",
                    "复制", "取消") != true)
                return;
            var newName = DeckCopyService.CopyPreset(presetName, out var error);
            if (newName == null)
            {
                ShowToast("复制失败：" + error);
                return;
            }
            RefreshDecksList(); // 副本进「我的卡组」区块
            LoadDeck(newName);
            ShowToast($"已复制为「{newName}」——副本可随意修改");
        }

        private void ConfirmDeleteDeck(string name)
            => ConfirmDeleteDeckAsync(name).Forget();

        /// <summary>删除卡组确认（2026-10-18 弹窗统一：TipPanel 双键，替代 ConfirmBox 现搭弹窗）。</summary>
        private async UniTaskVoid ConfirmDeleteDeckAsync(string name)
        {
            if (await Tips.ShowAsync($"删除卡组：确定删除「{name}」？该操作不可恢复。", "删除", "取消") != true)
                return;
            DeleteDeck(name);
        }

        private void DeleteDeck(string name)
        {
            if (!DeckSerializer.Delete(name))
            {
                ShowToast("删除失败（卡组不存在）");
                return;
            }
            if (_selectedDeckName == name)
            {
                // 删除的正是编辑中的卡组——左栏回新建态
                _selectedDeckName = null;
                _deckCardIds.Clear();
                _skillCardId = null;
                _tokenCardIds.Clear();
                _tokenBindings.Clear();
                _previewCard = null;
                RefreshAll();
            }
            RefreshDecksList();
            ShowToast($"已删除卡组：{name}");
        }

        /// <summary>载入卡组到左栏编辑：技能标记读取+自愈（旧档 null/资格失效→清）。
        /// 衍生物区/槽绑定随档读取（P9b；绑定自愈=清掉卡组里已无该卡的条目）。
        /// 未保存改动会被直接覆盖（与旧下拉载入同语义，toast 提示）。</summary>
        private void LoadDeck(string name)
        {
            var deck = DeckSerializer.Load(name);
            if (deck == null)
            {
                ShowToast("读取失败");
                return;
            }
            _deckCardIds.Clear();
            if (deck.cardIds != null)
                _deckCardIds.AddRange(deck.cardIds);
            _skillCardId = null;
            if (!string.IsNullOrEmpty(deck.skillCardId)
                && CardCatalog.GetById(deck.skillCardId) is { } skill
                && HeroSkillSystem.CanBeSkillCard(skill))
            {
                _skillCardId = deck.skillCardId;
            }
            _tokenCardIds.Clear();
            if (deck.tokenCardIds != null)
                _tokenCardIds.AddRange(deck.tokenCardIds.Where(id => !string.IsNullOrEmpty(id)));
            _tokenBindings.Clear();
            if (deck.tokenBindings != null)
                _tokenBindings.AddRange(deck.tokenBindings.Where(b => b != null && !string.IsNullOrEmpty(b.cardId)));
            _selectedDeckName = name;
            _currentDeckName = deck.name;
            if (_nameField != null)
                _nameField.SetTextWithoutNotify(deck.name);
            _previewCard = null;
            RefreshDecksList(); // 选中高亮对账
            RefreshAll();
            ShowToast($"已载入：{deck.name}（改动须点「保存」生效）");
        }

        /// <summary>新建空卡组：清卡组/技能/衍生物区与绑定/卡组列表选中/卡名（未保存改动直接放弃——先保存再新建）。</summary>
        private void OnNewDeck()
        {
            _deckCardIds.Clear();
            _skillCardId = null;
            _tokenCardIds.Clear();
            _tokenBindings.Clear();
            _selectedDeckName = null;
            _currentDeckName = "";
            _previewCard = null;
            if (_nameField != null)
                _nameField.SetTextWithoutNotify("");
            RefreshDecksList();
            RefreshAll();
            ShowToast("已新建空卡组（保存前需 30-60 张 + 设置英雄技能）");
        }

        // ======================================== 刷新总口 ========================================

        /// <summary>自动衍生物区重算（2026-11-13 改版）：先清退 str 已非空槽的旧档绑定
        ///（str 为权威——防旧绑定在运行时遮蔽卡组合界面新设的模板），再重建区=
        /// DeckTokenSupport.CollectReferencedTemplateIds（卡组全部 str 引用，含代价载荷）∪
        /// 余下 str 空槽绑定的目标（旧档兜底，运行时实例绑定优先解析）。随加删卡实时跟随，
        /// 不再手动管理；RefreshAll 首位调用（列表分区/统计/保存读的都是重算后的区）。</summary>
        private void RecomputeTokenZone()
        {
            // ① 绑定清退：只留 str 空槽（NeedsBinding）的条目
            var keepSlots = new Dictionary<string, HashSet<int>>();
            foreach (var id in _deckCardIds.Distinct())
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue;
                var needs = new HashSet<int>();
                foreach (var s in DeckTokenSupport.EnumerateSlots(card))
                    if (s.NeedsBinding) needs.Add(s.EffectIndex);
                if (needs.Count > 0) keepSlots[id] = needs;
            }
            for (int i = _tokenBindings.Count - 1; i >= 0; i--)
            {
                var b = _tokenBindings[i];
                if (b == null) { _tokenBindings.RemoveAt(i); continue; }
                if (keepSlots.TryGetValue(b.cardId, out var needs) && needs.Contains(b.effectIndex))
                    continue; // str 空槽——兜底绑定保留
                _tokenBindings.RemoveAt(i);
            }

            // ② 区重建：全部引用 ∪ 余下绑定目标
            _tokenCardIds.Clear();
            foreach (var id in DeckTokenSupport.CollectReferencedTemplateIds(_deckCardIds))
                _tokenCardIds.Add(id);
            foreach (var b in _tokenBindings)
                if (!string.IsNullOrEmpty(b?.templateId) && !_tokenCardIds.Contains(b.templateId))
                    _tokenCardIds.Add(b.templateId);
        }

        private void RefreshAll()
        {
            RecomputeTokenZone(); // 自动引用区先行（后续列表分区/统计/保存全读重算后的区）
            RefreshCatalog();
            RefreshDeckList();
            RefreshSummary();
            RefreshStats();
            BuildPreview();
            RefreshSkillSlot();

            // UGUI 卡面层重绑（中栏卡面挂 card-slot；滚动裁剪由 RectMask2D 自动处理）
            RebindCardLayer();
        }

        // ======================================== 保存 ========================================

        private void OnSave()
        {
            var name = string.IsNullOrWhiteSpace(_nameField != null ? _nameField.text : _currentDeckName)
                ? "新卡组"
                : (_nameField != null ? _nameField.text : _currentDeckName).Trim();

            // 预组名闸（2026-10-22 三套主题卡组迁教学）：预组只读，同名玩家档会遮蔽预组——禁止保存。
            if (DeckCatalog.IsPreset(name))
            {
                ShowToast($"「{name}」是只读预组卡组名，请换个名字");
                return;
            }

            // 构筑规则③：卡组 30-60 张（卡可重复任意张；衍生物模板卡不计数——落额外区不进牌库）
            if (DeckBuildCount < MinDeckCards || DeckBuildCount > MaxDeckCards)
            {
                ShowToast($"卡组须 {MinDeckCards}-{MaxDeckCards} 张（当前 {DeckBuildCount} 张）");
                return;
            }

            // 构筑规则②：限量槽超额（唯一门/仪典=每卡组 1 张；容量门=每 10 张牌库卡 1 张——
            // 2026-11-02 命运一抽改版）——添加口已拦，此处兜底旧档载入/卡数据热改
            var slotFirst = new Dictionary<string, ComposerCatalog.DeckUniqueSlot>();
            var slotCounts = new Dictionary<string, int>();
            foreach (var id in _deckCardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue;
                foreach (var slot in ComposerCatalog.DeckUniqueSlots(card))
                {
                    if (!slotFirst.ContainsKey(slot.Key)) slotFirst[slot.Key] = slot;
                    slotCounts[slot.Key] = slotCounts.TryGetValue(slot.Key, out var n) ? n + 1 : 1;
                }
            }
            foreach (var kv in slotCounts)
            {
                var cap = ComposerCatalog.SlotCapacity(kv.Key, DeckBuildCount);
                if (kv.Value <= cap) continue;
                var slot = slotFirst[kv.Key];
                ShowToast(cap == 1
                    ? $"「{slot.Display}」每卡组限 1 张——请先移除多余的一张"
                    : $"「{slot.Display}」超出上限：卡组每 10 张可带 1 张（{DeckBuildCount} 张上限 {cap} 张，现有 {kv.Value} 张）");
                return;
            }

            // 异常卡硬门槛（2026-10-16 全局对齐；主题违色已随主题约束删除——2026-10-26）：
            // 断链/形状违规任一即不可保存
            var composition = DeckCompositionValidator.Check(name, _deckCardIds);
            if (composition.MissingIds.Count > 0)
            {
                ShowToast($"卡组含 {composition.MissingIds.Count} 张断链卡（卡表无此 ID）——不可保存");
                return;
            }
            if (composition.ShapeErrors.Count > 0)
            {
                ShowToast($"卡组含 {composition.ShapeErrors.Count} 项类型形状违规（首项：{composition.ShapeErrors[0]}）——不可保存");
                return;
            }

            // 黑白代价覆盖（2026-10-26 定案）：需黑/白元素的卡须至少带 1 张对应补偿色的代价卡
            var bwGaps = DeckCompositionValidator.CheckBWCostCoverage(_deckCardIds);
            if (bwGaps.Count > 0)
            {
                var g0 = bwGaps[0];
                ShowToast($"卡组有卡施放需{DeckCompositionValidator.ColorZh(g0.Color)}元素（如 {g0.CardName}），"
                          + $"但没有能补偿{DeckCompositionValidator.ColorZh(g0.Color)}的代价卡——至少带 1 张对应代价卡才可保存");
                return;
            }

            // 衍生物区/槽绑定（P9b 硬约束）：带槽效果须绑定、绑定值 ∈ 区、模板类型合规、
            // 区容量 ≤6 无重复——首错直达具体卡与槽
            var tokenErrors = DeckTokenSupport.ValidateDeck(_deckCardIds, _tokenCardIds, _tokenBindings);
            if (tokenErrors.Count > 0)
            {
                ShowToast(tokenErrors[0] + (tokenErrors.Count > 1 ? $"（共 {tokenErrors.Count} 项）" : "") + "——无法保存");
                return;
            }

            // 技能标记自愈：资格失效 → 清标记；构筑规则①：未标记不可保存
            if (!string.IsNullOrEmpty(_skillCardId))
            {
                var markedCard = CardCatalog.GetById(_skillCardId);
                if (markedCard == null || !HeroSkillSystem.CanBeSkillCard(markedCard))
                {
                    _skillCardId = null;
                    ShowToast("技能标记已清除（卡池无此卡或资格失效）——请重新设置英雄技能");
                }
            }
            if (string.IsNullOrEmpty(_skillCardId))
            {
                ShowToast("必须设置英雄技能（选一张法术，点其预览的主动效果切换；不占卡组张数）");
                return;
            }

            var deck = new DeckData(name)
            {
                cardIds = new List<string>(_deckCardIds),
                skillCardId = _skillCardId,
                tokenCardIds = new List<string>(_tokenCardIds), // P9b 衍生物区随组落盘
                tokenBindings = _tokenBindings
                    .Where(b => b != null && _deckCardIds.Contains(b.cardId)) // P9b 绑定（剔卡组外陈旧条目）
                    .Select(b => new DeckTokenBinding(b.cardId, b.effectIndex, b.templateId))
                    .ToList(),
            };
            var path = DeckSerializer.Save(deck);
            if (path != null) TutorialCreationFlow.NotifyDecksChanged(); // 第三课走查步检测（未开课零行为）
            ShowToast(path == null ? "保存失败" : $"已保存：{name}");
            if (path == null) return;

            _selectedDeckName = name;
            _currentDeckName = name;
            RefreshDecksList(); // 保存行高亮对账
        }

        // ======================================== 衍生物区（2026-11-13 自动引用改版：绑定面板/区管理弹层随手动通道退役整删） ========================================

        /// <summary>槽绑定读值（旧档兜底通道）：null=未设置。仅 str 空槽会有值
        ///（RecomputeTokenZone 已清退 str 非空槽的绑定——str 为权威）。</summary>
        private string BindingOf(string cardId, int effectIndex)
        {
            foreach (var b in _tokenBindings)
                if (b.cardId == cardId && b.effectIndex == effectIndex)
                    return b.templateId;
            return null;
        }

        // ======================================== 小件 ========================================

        // 行尾「类别+基础属性参数」单源=CardRowText.TypeStatOf（2026-11-13 统一卡行缩略写法，
        // 卡组合界面同源）——本屏不再自带一份。

        // 临时提示文本（无需事件，直接写 Label）。
        private void ShowToast(string message)
        {
            if (_toast != null) _toast.text = message;
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
