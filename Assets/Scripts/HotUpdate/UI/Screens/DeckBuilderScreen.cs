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
        private RectTransform _statsZone;   // v2：非滚动统计容器（常驻 stats-row 行）
        private RectTransform _overlay;
        private RectTransform _catalogFilter; // facet-color（v2 改名，旧 catalog-filter）
        private RectTransform _facetKind;     // 卡型 chips 固定容器
        private RectTransform _deckTemplates; // deck-panel/templates（隐藏模板根）
        private RectTransform _catTemplates;  // catalog-panel/templates
        private TMP_Text _summary;
        private TMP_Text _toast;
        private TMP_Text _skillLabel;
        private TMP_Text _skillCostLabel;
        private TMP_Text _deckCount;    // hdr-deck 右侧「N 种 / M 张」
        private TMP_Text _catalogCount; // hdr-catalog 右侧「N 张」
        private UButton _skillClearBtn;
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
            _deckCount = FindUnder(Find("hdr-deck"), "count")?.GetComponentInChildren<TMP_Text>(true);

            // ---- 中栏：只读卡面 + 英雄技能栏（2026-10-28 preview-panel 重构：烘焙 ACard 360×648
            // 原尺寸直挂 ACardView，点卡面放大=运行时补 Button；费用=preview-stage/CostSquares 直填） ----
            var bakedCard = Find("ACard") as RectTransform;
            if (bakedCard != null)
            {
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
            _skillLabel = FindText("lbl-skill");
            _skillCostLabel = FindText("lbl-skill-cost");
            _skillClearBtn = BindButton("btn-skill-clear", OnClearSkill);

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
            // UGUI 卡牌层回收（中栏卡面独立池化，切屏必须显式清）；放大绑定随清——
            // 遮罩节点随屏销毁，残留绑定会在下一屏 Bind 时往已毁槽位挂卡
            _zoomBindings.Clear();
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

        /// <summary>费用徽标写值（圆片烘焙在模板 cost 节点）。</summary>
        private static void BindCost(RectTransform row, CardData card)
            => SetText(row, "cost", ((int)card.TotalCost).ToString());

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

        /// <summary>构筑卡池源（2026-10-27 纯引用副本配套）：玩家卡池 ∪ 被任意玩家卡组引用的卡
        /// （含技能卡，剔除衍生物模板）。预组原件带「教学」tag 被 LoadPlayPool 过滤——不并入则
        /// 纯引用副本可删不可加回、技能卡无法重新标记。某卡从所有卡组删净并保存后会从卡池消失
        /// （重新复制预组即可找回）。随机组卡/卡牌编辑器等其余 LoadPlayPool 消费点不受此口径影响。</summary>
        private static List<CardData> PlayPoolWithDeckReferenced()
        {
            var pool = CardCatalog.LoadPlayPool(); // 教学专用卡不进构筑卡池（副本引用卡并入见下）
            var poolIds = new HashSet<string>();
            foreach (var c in pool) poolIds.Add(c.ID);
            var referenced = new HashSet<string>();
            foreach (var d in DeckSerializer.LoadAll())
            {
                if (d?.cardIds == null) continue;
                foreach (var id in d.cardIds) referenced.Add(id);
                if (!string.IsNullOrEmpty(d.skillCardId)) referenced.Add(d.skillCardId);
            }
            var extra = CardCatalog.LoadAll()
                .Where(c => referenced.Contains(c.ID) && !poolIds.Contains(c.ID) && !CardCatalog.IsTokenCard(c))
                .ToList();
            if (extra.Count == 0) return pool;
            var result = new List<CardData>(pool.Count + extra.Count);
            result.AddRange(pool);
            result.AddRange(extra);
            return result;
        }

        private void RefreshCatalog()
        {
            ClearContent(_catalogList.Content);

            IEnumerable<CardData> cards = PlayPoolWithDeckReferenced();
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

        /// <summary>色名中文（空结果解释行用——chips 文案烘焙在预制体，这里补中文映射）。</summary>
        private static string ColorZh(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return "红";
                case UIColor.Blue: return "蓝";
                case UIColor.Green: return "绿";
                case UIColor.Black: return "黑";
                case UIColor.White: return "白";
                default: return "灰";
            }
        }

        /// <summary>卡池行（tpl-row-card 克隆）：费用徽标+卡名+类型身材，行点击=中栏预览；
        /// 行尾「技能/编辑/添加」——技能钮仅资格卡显示（填入中栏英雄技能栏）；编辑=跳卡牌编辑界面。</summary>
        private void MakeCatalogRow(CardData card)
        {
            var captured = card;
            var row = Clone(_catTemplates, "tpl-row-card", _catalogList.Content);
            if (row == null) return;

            BindCost(row, card);
            SetText(row, "name", NameOf(card));
            SetText(row, "meta", TypeStatLine(card));
            var skillBtn = BindRowButton(row, "btn-skill", () => SetSkill(captured));
            if (skillBtn != null) skillBtn.gameObject.SetActive(HeroSkillSystem.CanBeSkillCard(card));
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
            // 构筑规则③：30-60 张内重复任意张——上限在添加口即时反馈
            //（衍生物模板卡不计入：入组开局落 Zone.Extra 额外区不进牌库，2026-10-22 口径）
            if (DeckBuildCount >= MaxDeckCards)
            {
                ShowToast($"卡组已达上限 {MaxDeckCards} 张");
                return;
            }
            // 构筑规则②：唯一门/同名仪典每卡组限 1 张——同卡第二张与异卡同名都算冲突
            var card = CardCatalog.GetById(id);
            if (card != null)
            {
                foreach (var slot in ComposerCatalog.DeckUniqueSlots(card))
                {
                    var holder = DeckCardHoldingKey(slot.Key);
                    if (holder != null)
                    {
                        ShowToast($"「{slot.Display}」每卡组限 1 张——已有 {NameOf(holder)}");
                        return;
                    }
                }
            }
            _deckCardIds.Add(id);
            RefreshAll();
        }

        /// <summary>卡组内已持有指定构筑唯一键的卡（无=null）——添加拦截用。</summary>
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

        private static string NameOf(CardData card)
            => card == null ? "?" : string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName;

        /// <summary>移出一册（同卡聚合行语义：同卡多册只减 1；技能标记不随移除清除）。</summary>
        private void RemoveCopy(string cardId)
        {
            _deckCardIds.Remove(cardId);
            RefreshAll();
        }

        /// <summary>左栏当前卡组（同卡聚合 ×册数，保序=首次入组序）：行=[费用徽标|卡名×N|异常
        /// 标记|删除]（tpl-row-deck 克隆，行点击=中栏预览）。断链卡（卡表无此 ID）无法展示——
        /// 仅可删除；形状违规卡行尾红字标记。衍生物模板与效果引用的外部卡排在
        /// 「衍生物 / 引用卡」分区之后（验收6；汇总张数口径不变——衍生物不计 30-60）。</summary>
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

            // 普通卡行在前；衍生物模板卡行挪进分区（保序）
            var tokenRows = new List<int>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (IsTokenTemplateId(ids[i])) tokenRows.Add(i);
                else MakeDeckRow(ids[i], CardCatalog.GetById(ids[i]), counts[i]);
            }

            // 分区：衍生物模板（卡组条目，可删照旧）+ 卡组效果引用的外部卡（只读可预览）
            var externalRefs = CollectExternalReferences();
            if (tokenRows.Count > 0 || externalRefs.Count > 0)
            {
                MakeSectionHeader("衍生物 / 引用卡", _deckList.Content);
                foreach (int i in tokenRows)
                    MakeDeckRow(ids[i], CardCatalog.GetById(ids[i]), counts[i]);
                foreach (var refId in externalRefs)
                    MakeReferenceRow(refId);
            }
        }

        private void MakeDeckRow(string cardId, CardData card, int copies)
        {
            var row = Clone(_deckTemplates, "tpl-row-deck", _deckList.Content);
            if (row == null) return;

            var rowBtn = row.GetComponent<UButton>();
            if (rowBtn != null) rowBtn.onClick.RemoveAllListeners();

            if (card == null)
            {
                // 断链卡：无卡面数据可展示——只能删除
                SetVisible(row, "cost", false);
                SetText(row, "name", $"[断链] {cardId} ×{copies}", UiStyle.DotRed);
                SetVisible(row, "flag", false);
                BindRowButton(row, "btn-del", () => RemoveCopy(cardId));
                return;
            }

            var captured = card;
            BindCost(row, card);
            SetText(row, "name", NameOf(card) + (copies > 1 ? $" ×{copies}" : ""));

            var flags = CardFlags(card);
            SetVisible(row, "flag", flags.Count > 0);
            if (flags.Count > 0)
                SetText(row, "flag", "✘" + string.Join("/", flags), UiStyle.DotRed);

            BindRowButton(row, "btn-del", () => RemoveCopy(cardId));
            if (rowBtn != null)
                rowBtn.onClick.AddListener(() => ShowPreview(captured));
        }

        /// <summary>引用卡行（验收6）：卡组效果引用但不在卡组内的卡（SummonToken/GrantMorph 目标）——
        /// 行点击=预览、不可删（要断引用须移除引用它的卡）；flag 位灰字「引用」区分。断链引用红字。</summary>
        private void MakeReferenceRow(string refId)
        {
            var row = Clone(_deckTemplates, "tpl-row-deck", _deckList.Content);
            if (row == null) return;

            var rowBtn = row.GetComponent<UButton>();
            if (rowBtn != null) rowBtn.onClick.RemoveAllListeners();
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

        /// <summary>卡组效果引用的外部卡枚举（验收6）：SummonToken.str（召唤衍生物模板）与
        /// GrantMorph.str（变形赋予目标）两原子的 str=卡 ID——其余原子 str 语义不同（宣言卡名等）
        /// 不收。遍历口径=效果步骤/扁平原子/分支奖励/代价载荷全量（2026-10-26 起同构旧例
        /// DeckCopyService.RewriteCardReferences 已随复制零效果定案删除）；
        /// 已在卡组内的 id 不收（在组模板行已在分区内、在组普通卡留在普通区）。</summary>
        private List<string> CollectExternalReferences()
        {
            var seen = new HashSet<string>(_deckCardIds);
            var result = new List<string>();
            foreach (var id in _deckCardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue;
                foreach (var fx in card.Effects)
                {
                    if (fx == null) continue;
                    if (fx.Steps != null)
                        foreach (var step in fx.Steps)
                        {
                            if (step == null) continue;
                            CollectRefAtom(step.atomic, seen, result);
                            CollectRefAtoms(step.thenSteps, seen, result);
                            CollectRefAtoms(step.elseSteps, seen, result);
                        }
                    CollectRefAtoms(fx.AtomicEffects, seen, result);
                    if (fx.Costs != null)
                        foreach (var cost in fx.Costs)
                            CollectRefAtoms(cost != null ? cost.AllPayloads() : null, seen, result);
                }
                CollectRefAtoms(card.PayloadCost != null ? card.PayloadCost.AllPayloads() : null, seen, result);
            }
            return result;
        }

        private static void CollectRefAtoms(List<AtomicEffectEntry> atoms, HashSet<string> seen, List<string> result)
        {
            if (atoms == null) return;
            foreach (var atom in atoms) CollectRefAtom(atom, seen, result);
        }

        /// <summary>单原子引用判定：查原子表行（refId→EnumName），SummonToken/GrantMorph 才把
        /// str 当卡 ID；分支奖励原子列（倒计时召唤模板在此）一并下钻。</summary>
        private static void CollectRefAtom(AtomicEffectEntry atom, HashSet<string> seen, List<string> result)
        {
            if (atom == null) return;
            if (atom.branch != null && atom.branch.then != null)
                CollectRefAtoms(atom.branch.then, seen, result);
            if (string.IsNullOrEmpty(atom.str) || seen.Contains(atom.str)) return;
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByHashId(atom.refId);
            if (cfg == null) return;
            string summon = AtomicEffectType.SummonToken.ToString();
            string morph = AtomicEffectType.GrantMorph.ToString();
            if (cfg.EnumName != summon && cfg.EnumName != morph) return;
            seen.Add(atom.str);
            result.Add(atom.str);
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
                int tokens = _deckCardIds.Count(IsTokenTemplateId);
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
        /// 空卡=藏卡；点卡面放大全屏（Button 在 Build 接线）。</summary>
        private void BuildPreview()
        {
            var card = _previewCard;
            if (card == null)
            {
                if (_previewView != null) _previewView.gameObject.SetActive(false);
                RefreshPreviewCost(null);
                return;
            }
            var item = CardOverlayItem.FromCardDataFull(card, "Preview");
            if (_previewView != null)
            {
                _previewView.gameObject.SetActive(true);
                _previewView.Apply(item, CardOverlayLayout.Full);
            }
            RefreshPreviewCost(item);
            // 形状校验提示行（preview-note）已随 2026-10-28 preview-panel 重构退役——
            // 违规明细仍由构成块/保存拦截展示（DeckCompositionValidator.CheckShape 单源不变）
        }

        /// <summary>预览卡费条（2026-10-28 preview-panel 重构）：preview-stage/CostSquares 烘焙
        /// 实例直填——空卡/零费=Clear 清空（条本体常驻，prefab 布局恒定）。</summary>
        private void RefreshPreviewCost(CardOverlayItem item)
        {
            if (_previewCost == null) return;
            if (item != null && item.Costs != null && !item.Costs.IsZero) _previewCost.SetCosts(item.Costs);
            else _previewCost.Clear();
        }

        /// <summary>放大预览卡（验收2）：全屏遮罩 + 600×1080 放大卡居中（仍走卡面层池化——
        /// 2026-10-28 重构后中栏预览=烘焙 ACard 直挂，控制器只剩放大卡一职）；放大卡本体不接
        /// OnClick——点遮罩/空白还原。尺寸口径（2026-10-28 新卡 360×648 分数锚替换）：zoom-card
        /// 槽位尺寸直接驱动（见 RebindCardLayer），不再实例 localScale ×3。</summary>
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
            _zoomOverlay.gameObject.SetActive(true);
            RebindCardLayer();
        }

        /// <summary>还原放大态：关遮罩+清放大绑定（卡面层回收放大卡实例）。</summary>
        private void CloseZoom()
        {
            if (_zoomBindings.Count == 0) return;
            _zoomBindings.Clear();
            if (_zoomOverlay != null) _zoomOverlay.gameObject.SetActive(false);
            RebindCardLayer();
        }

        /// <summary>放大卡缩放（2026-10-28 晚用户定案回退：整体一起放大——文字随 localScale
        /// 同比渲染，版式恒同比；600×1080 视觉=设计矩形 360×648 × 5/3）。字号三档烘在 prefab：
        /// 名称 25（18-25 自动缩号）/关键词行 18/效果文本 16——缩放不改字号。</summary>
        private const float ZoomCardScale = 1080f / 648f;

        /// <summary>放大卡槽=设计矩形（每次重绑前归位；prefab 内 zoom-card 旧值不作数）。</summary>
        private static readonly Vector2 ZoomCardDesignSize = new Vector2(360f, 648f);

        /// <summary>UGUI 卡面层重绑（preview-panel 重构后=放大卡专属；中栏预览=烘焙 ACard 直挂
        /// 不经控制器）。放大=设计矩形槽 + 实例 localScale×ZoomCardScale（整体同比放大，
        /// 2026-10-28 晚回退定案——文字随变换缩放，字号三档恒定不动态改）。</summary>
        private void RebindCardLayer()
        {
            if (_zoomSlot != null)
            {
                _zoomSlot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ZoomCardDesignSize.x);
                _zoomSlot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ZoomCardDesignSize.y);
            }
            var all = new List<CardOverlayBinding>(_zoomBindings.Count);
            all.AddRange(_zoomBindings);
            CardOverlayController.Instance.Bind(all);

            if (_zoomSlot != null && _zoomSlot.childCount > 0)
                ((RectTransform)_zoomSlot.GetChild(0)).localScale = Vector3.one * ZoomCardScale;
        }

        /// <summary>技能栏刷新：已设置=卡名+发动费区间+取消钮；未设置=空态提示（保存口必选拦截）。</summary>
        private void RefreshSkillSlot()
        {
            if (_skillLabel == null) return;
            var marked = string.IsNullOrEmpty(_skillCardId) ? null : CardCatalog.GetById(_skillCardId);
            if (marked == null)
            {
                _skillLabel.text = "英雄技能：未设置——右侧选主动技能卡填入（保存前必须设置）";
                if (_skillCostLabel != null) _skillCostLabel.text = "";
                if (_skillClearBtn != null) _skillClearBtn.gameObject.SetActive(false);
                return;
            }

            _skillLabel.text = "英雄技能：" + NameOf(marked);
            // 发动费口径同旧统计区技能预览：各主动效果现付锚价（多效果显示区间）；每回合一次
            var anchors = HeroSkillSystem.SkillEffectsOf(new CardWrapper(marked))
                .Select(f => CostDerivationService.DeriveElementCosts(f))
                .Where(c => c != null).ToList();
            string costText;
            if (anchors.Count == 0)
                costText = "发动费用：无（每回合一次）";
            else if (anchors.Count <= 1)
                costText = $"发动费用：{DeckCompositionValidator.FormatCost(anchors[0])}" +
                           "（每回合一次，现付所选效果费用）";
            else
                costText = $"发动费用：{anchors.Min(a => a.Total):0.#}~{anchors.Max(a => a.Total):0.#} 区间" +
                           $"（{marked.Effects.Count} 个作用择一发动；每回合一次）";
            if (_skillCostLabel != null) _skillCostLabel.text = costText;
            if (_skillClearBtn != null) _skillClearBtn.gameObject.SetActive(true);
        }

        /// <summary>右侧卡池行「技能」钮：填入英雄技能栏（资格=法术且 1~3 主动——不合格 toast 原因）。</summary>
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
            ShowToast($"英雄技能已设置：{NameOf(card)}——开局落技能栏（不占卡组张数），每回合一次");
        }

        private void OnClearSkill()
        {
            _skillCardId = null;
            RefreshAll();
            ShowToast("已取消英雄技能标记");
        }

        // ======================================== 统计区（左栏底部·非滚动容器） ========================================

        private void RefreshStats()
        {
            if (_statsZone == null || _deckTemplates == null) return;
            ClearStatsClones();
            var cards = _deckCardIds
                .Select(id => CardCatalog.GetById(id))
                .Where(c => c != null)
                .ToList();

            BuildCompositionCheck();          // 构成检测（全宽 140）
            BuildCostCurve(cards);            // 曲线（左列 300×164）
            BuildColorDistribution(cards);    // 颜色（右列 300×164）
            BuildTypeRatio(cards);            // 类型行（全宽 20，无标题）
        }

        /// <summary>清统计区克隆块（stats-row 及其内部双列块都属刷新范围；stats-row 为预制体
        /// 常驻行本体保留——只清其子级，防曲线/颜色块跨刷新累积）。</summary>
        private void ClearStatsClones()
        {
            var row = UiKit.FindDeep(_statsZone, "stats-row");
            if (row != null)
            {
                for (int i = row.childCount - 1; i >= 0; i--)
                {
                    var inner = row.GetChild(i);
                    inner.SetParent(null);
                    UnityEngine.Object.Destroy(inner.gameObject);
                }
            }
            for (int i = _statsZone.childCount - 1; i >= 0; i--)
            {
                var child = _statsZone.GetChild(i);
                if (child.name == "stats-row") continue;
                child.SetParent(null);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        /// <summary>统计块克隆（tpl-stat）：写标题/块尺寸；返回块根。width=null=全宽入 stats-zone，
        /// 有 width=入 stats-row 双列。typeLine=true 隐藏标题行（类型行 20 高单行形态）。</summary>
        private RectTransform CloneStat(string title, float height, float? width, bool typeLine)
        {
            var parent = width.HasValue ? UiKit.FindDeep(_statsZone, "stats-row") : _statsZone;
            var block = Clone(_deckTemplates, "tpl-stat", parent);
            if (block == null) return null;
            var le = block.GetComponent<LayoutElement>();
            if (le != null)
            {
                if (width.HasValue) le.preferredWidth = width.Value;
                le.preferredHeight = height;
            }
            if (typeLine) SetVisible(block, "header", false);
            else
            {
                var headerRt = UiKit.FindDeep(block, "header");
                SetText(headerRt, "label", title);
            }
            return block;
        }

        /// <summary>块正文 TMP（body/label——富文本烘焙：构成检测多行/颜色分布彩点都走它）。</summary>
        private static TMP_Text StatBodyLabel(RectTransform block)
            => UiKit.FindDeep(UiKit.FindDeep(block, "body"), "label")?.GetComponent<TMP_Text>();

        /// <summary>构成检测块（统计区首块常显）：硬约束=断链/形状/技能/黑白代价（任一不过不可
        /// 保存）+ 地源（advisory 不拦保存——施放费须有足够可放地的卡，整卡全施放面不可付才报
        /// 「无法释放」）——加卡/删卡实时刷新；口径=DeckCompositionValidator（主题违色已随主题
        /// 约束删除——2026-10-26）。</summary>
        private void BuildCompositionCheck()
        {
            var block = CloneStat("构成检测", 150f, null, false);
            if (block != null) block.SetSiblingIndex(0); // stats-row 为常驻 idx0——检测块置顶（S3 评审 P1：曲线/颜色曾压顶）
            if (block == null) return;
            var body = StatBodyLabel(block);
            if (body == null) return;

            var report = DeckCompositionValidator.Check(
                _nameField != null ? _nameField.text : _currentDeckName, _deckCardIds);
            var landShort = DeckCompositionValidator.CheckLandSupport(_deckCardIds);
            var bwGaps = DeckCompositionValidator.CheckBWCostCoverage(_deckCardIds);
            bool skillOk = !string.IsNullOrEmpty(_skillCardId);

            var lines = new List<string>();
            string red = ColorUtility.ToHtmlStringRGB(UiStyle.DotRed);
            string green = ColorUtility.ToHtmlStringRGB(UiStyle.DotGreen);

            // ---- 硬约束（任一不过=不可保存）----
            if (report.CardCount > 0 && report.Ok && skillOk && bwGaps.Count == 0)
                lines.Add($"<color=#{green}>✔ 构成约束通过（断链/形状/技能/黑白代价）" +
                          (landShort.Count == 0 ? "·地源足" : "") + "</color>");
            else if (report.CardCount == 0)
                lines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(UiStyle.TextHint)}>" +
                          "（空卡组——右侧卡池点「添加」入组）</color>");

            foreach (var id in report.MissingIds)
                lines.Add($"<color=#{red}>✘ 断链卡 {id}——卡表中无此 ID（该卡可能已被删除），请移除</color>");
            foreach (var err in report.ShapeErrors)
                lines.Add($"<color=#{red}>✘ {err}</color>");
            if (!skillOk)
                lines.Add($"<color=#{red}>✘ 未设置英雄技能（中栏技能栏——右侧选主动技能卡点「技能」填入）——无法保存</color>");
            foreach (var gap in bwGaps)
                lines.Add($"<color=#{red}>✘ {gap.CardName} 等卡施放需{DeckCompositionValidator.ColorZh(gap.Color)}元素，" +
                          $"卡组没有能补偿{DeckCompositionValidator.ColorZh(gap.Color)}的代价卡（至少带 1 张）——无法保存</color>");

            // ---- 地源不足（advisory，不拦保存；区块放不下——两行折叠）----
            const int maxLandRows = 2;
            int landShown = 0;
            foreach (var s in landShort)
            {
                if (landShown++ >= maxLandRows) break;
                lines.Add($"<color=#{red}>✘ {s.CardName} ×{s.Copies}｜{s.Surface}｜{s.Detail}——无法释放</color>");
            }
            if (landShort.Count > maxLandRows)
                lines.Add($"<color=#{red}>……另有 {landShort.Count - maxLandRows} 张卡地源不足无法释放</color>");
            body.text = string.Join("\n", lines);
        }

        /// <summary>费用曲线：1-9 档（>9 并入 9+）条形分布（tpl-stat-bar ×9 克隆进 body）。
        /// 列内序 bar/tier/count（2026-10-26 顶歪修复）：tier=费用档贴柱底一行（x 轴），count=张数值
        /// 挪到最底且挂固定高度 LE（模板烘焙 17）——旧序 count 夹在柱与档位间，空值 TMP 高 0 vs
        /// 有值 16 → 柱底基线错 16px；现空值恒占行，基线/x 轴/数值行三线恒齐。</summary>
        private void BuildCostCurve(List<CardData> cards)
        {
            var block = CloneStat("费用曲线", 170f, 308f, false);
            if (block == null) return;
            var body = UiKit.FindDeep(block, "body");
            var bodyLabel = StatBodyLabel(block);
            if (bodyLabel != null) bodyLabel.gameObject.SetActive(false); // 曲线块用柱条不用文本
            if (body == null || _deckTemplates == null) return;

            var tiers = new int[10]; // 0 位弃用；1..9；>9 并入 9（标签显示 9+）
            foreach (var card in cards)
            {
                int tier = Mathf.Clamp(Mathf.RoundToInt(card.TotalCost), 1, 9);
                tiers[tier]++;
            }

            int max = tiers.Skip(1).Max();
            for (int tier = 1; tier <= 9; tier++)
            {
                int count = tiers[tier];
                var barCol = Clone(_deckTemplates, "tpl-stat-bar", body);
                if (barCol == null) continue;
                var bar = UiKit.FindDeep(barCol, "bar");
                var barLe = bar != null ? bar.GetComponent<LayoutElement>() : null;
                if (barLe != null)
                    barLe.preferredHeight = count > 0
                        ? Mathf.Max(6f, (int)(52f * count / Mathf.Max(1, max))) : 2f;
                SetText(barCol, "count", count > 0 ? count.ToString() : "");
                SetText(barCol, "tier", tier == 9 ? "9+" : tier.ToString());
            }
        }

        /// <summary>颜色分布：六色计数（彩点+数字，富文本——色值取 UiStyle.Dot* 令牌）。
        /// 两行制（2026-10-26 修复）：单行六组在运行时标签宽约 284px（块308−VLG边距16，曲线+颜色
        /// 双列均分 608 内宽）下，第 4 组（灰）恰落断行边界——TMP 按空格断行会把「●」留在上行尾、
        /// 色字挪下行（图标和文字不在一行）。显式两行（红蓝绿/灰黑白）消除断行歧义：三组/行
        /// 最宽形态（两位数计数）约 221px，余量 60px+。组内串含空格，勿再回并单行。
        /// 字体 PZD 无 U+00A0 字形——不可用不换行空格方案（会渲染缺字形）。</summary>
        private void BuildColorDistribution(List<CardData> cards)
        {
            var block = CloneStat("颜色分布", 170f, 308f, false);
            if (block == null) return;
            var body = StatBodyLabel(block);
            if (body == null) return;

            var pairs = new[] {
                (UIColor.Red, UiStyle.DotRed, "红"), (UIColor.Blue, UiStyle.DotBlue, "蓝"),
                (UIColor.Green, UiStyle.DotGreen, "绿"), (UIColor.Gray, UiStyle.DotGray, "灰"),
                (UIColor.Black, UiStyle.DotBlack, "黑"), (UIColor.White, UiStyle.DotWhite, "白") };
            var lines = new List<string>[] { new List<string>(), new List<string>() };
            for (int i = 0; i < pairs.Length; i++)
            {
                int count = cards.Count(c => CardSorter.HasCostColor(c, pairs[i].Item1));
                lines[i / 3].Add($"<color=#{ColorUtility.ToHtmlStringRGB(pairs[i].Item2)}>●</color> {pairs[i].Item3} {count}");
            }
            body.text = string.Join("   ", lines[0]) + "\n" + string.Join("   ", lines[1]);
        }

        /// <summary>类型比例：生物/法术/结界计数（单行无标题形态——header 显隐切换）。</summary>
        private void BuildTypeRatio(List<CardData> cards)
        {
            int creatures = cards.Count(c => c.Supertype == Cardtype.Creature);
            int spells = cards.Count(c => c.Supertype == Cardtype.Spell);
            int enchantments = cards.Count(c => c.Supertype == Cardtype.Enchantment);
            int other = cards.Count - creatures - spells - enchantments;

            var block = CloneStat(null, 20f, null, true);
            if (block == null) return;
            var body = StatBodyLabel(block);
            if (body != null)
                body.text = $"类型：生物 {creatures} · 法术 {spells} · 结界 {enchantments}"
                            + (other > 0 ? $" · 其他 {other}" : "");
        }

        // ======================================== 右栏：卡组列表 ========================================

        /// <summary>卡组列表刷新（2026-10-22 预组分栏·2026-10-23 模板化）：上方独立
        /// 「预组卡组（只读）」区块——预组不可载入编辑（防改名另存出引用教学 id 的脆弱浅拷贝），
        /// 改只能复制成玩家副本；下方「我的卡组」玩家档区块，行=[卡组名|张数·不可用标记|✕删除]，
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

        /// <summary>卡组张数文案口径（预组/玩家档通用）：正卡数+衍生物M（模板不计张数——额外区配套）。</summary>
        private static string DeckCountText(DeckData deck)
        {
            int total = deck?.cardIds?.Count ?? 0;
            int tokens = deck?.cardIds?.Count(IsTokenTemplateId) ?? 0;
            int main = total - tokens;
            return tokens > 0 ? $"{main} 张+衍生物{tokens}" : $"{main} 张";
        }

        /// <summary>预组行（只读）：[名·预组|张数|复制钮]；行点击=只读提示（不载入——防脆弱浅拷贝）。</summary>
        private void MakePresetRow(DeckData preset)
        {
            var row = MakeDeckLibRow();
            if (row == null) return;

            SetText(row, "name", preset.name + "·预组");
            var tag = DeckCompositionValidator.UnusableTag(preset);
            SetText(row, "meta", DeckCountText(preset) + (tag.Length > 0 ? "·" + tag : ""),
                tag.Length > 0 ? UiStyle.DotRed : (Color?)null);
            SetVisible(row, "btn-del", false); // 预组不可删

            var captured = preset.name;
            BindRowButton(row, "btn-copy", () => ConfirmCopyPreset(captured));

            var rowBtn = row.GetComponent<UButton>();
            if (rowBtn != null)
                rowBtn.onClick.AddListener(() =>
                    ShowToast("预组只读——点「复制」生成自己的副本后修改"));
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
            int tokens = deck.cardIds.Count(IsTokenTemplateId);
            int main = deck.cardIds.Count - tokens;
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
                _previewCard = null;
                RefreshAll();
            }
            RefreshDecksList();
            ShowToast($"已删除卡组：{name}");
        }

        /// <summary>载入卡组到左栏编辑：技能标记读取+自愈（旧档 null/资格失效→清）。
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
            _selectedDeckName = name;
            _currentDeckName = deck.name;
            if (_nameField != null)
                _nameField.SetTextWithoutNotify(deck.name);
            _previewCard = null;
            RefreshDecksList(); // 选中高亮对账
            RefreshAll();
            ShowToast($"已载入：{deck.name}（改动须点「保存」生效）");
        }

        /// <summary>新建空卡组：清卡组/技能/卡组列表选中/卡名（未保存改动直接放弃——先保存再新建）。</summary>
        private void OnNewDeck()
        {
            _deckCardIds.Clear();
            _skillCardId = null;
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

        private void RefreshAll()
        {
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

            // 构筑规则②：唯一门/同名仪典每卡组限 1 张——添加口已拦，此处兜底旧档载入/卡数据热改
            var seenKeys = new HashSet<string>();
            foreach (var id in _deckCardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue;
                foreach (var slot in ComposerCatalog.DeckUniqueSlots(card))
                {
                    if (!seenKeys.Add(slot.Key))
                    {
                        ShowToast($"「{slot.Display}」每卡组限 1 张（{NameOf(card)} 冲突）——请先移除多余的一张");
                        return;
                    }
                }
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
                ShowToast($"卡组有卡施放需{DeckCompositionValidator.ColorZh(g0.Color)}元素（如 {g0.CardName}），" +
                          $"但没有能补偿{DeckCompositionValidator.ColorZh(g0.Color)}的代价卡——至少带 1 张对应代价卡才可保存");
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
                ShowToast("必须设置英雄技能（中栏技能栏——右侧选主动技能卡点「技能」填入；不占卡组张数）");
                return;
            }

            var deck = new DeckData(name)
            {
                cardIds = new List<string>(_deckCardIds),
                skillCardId = _skillCardId,
            };
            var path = DeckSerializer.Save(deck);
            if (path != null) TutorialCreationFlow.NotifyDecksChanged(); // 第三课走查步检测（未开课零行为）
            ShowToast(path == null ? "保存失败" : $"已保存：{name}");
            if (path == null) return;

            _selectedDeckName = name;
            _currentDeckName = name;
            RefreshDecksList(); // 保存行高亮对账
        }

        // ======================================== 小件 ========================================

        private static string TypeStatLine(CardData card)
        {
            switch (card.Supertype)
            {
                case Cardtype.Creature:
                    return $"生物 {(card.Power ?? 0)}/{(card.Life ?? 0)}";
                case Cardtype.Enchantment:
                    return $"结界 耐久{CardCompositionCost.TotalDurabilityOf(card)}"; // 2026-10-17：恒有耐久（派生单源）
                case Cardtype.Spell:
                    return "法术";
                default:
                    return CardSorter.TypeName(card);
            }
        }

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
