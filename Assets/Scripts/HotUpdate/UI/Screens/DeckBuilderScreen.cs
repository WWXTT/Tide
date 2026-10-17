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
    /// 卡组构建界面（2026-10-16 三栏改版）：
    /// 左=当前卡组（卡名行·同卡聚合 ×册数·展示/删除两钮·异常卡标记）+ 卡组名/新建/保存 +
    /// 底部统计滚动区（构成检测/费用曲线/颜色分布/类型比例）；
    /// 中=只读卡面（CardOverlayItem.FromCardDataFull 六区快照，与手牌/合成器同族）+
    /// 英雄技能栏（右侧选主动技能卡填入·常显发动费）；
    /// 右=卡池（颜色+卡型两套过滤+搜索；行点击预览·添加/编辑/技能）+ 卡组列表
    /// （行点击载入左栏编辑·✕删除·不可用标记）。
    ///
    /// 构筑规则（2026-10-11 定案）：① 必须标记英雄技能（资格=HeroSkillSystem.CanBeSkillCard
    /// ——法术且 1~3 个主动效果），技能栏空则不可保存；② 唯一效果（唯一门/同名仪典）每卡组
    /// 限 1 张——添加口+保存口双拦；③ 卡组 30-60 张、卡可重复任意张。
    /// 主题双色约束（2026-10-15）：红/绿/蓝开头卡组拒加主题外颜色卡（出牌费+主动锚价两面）。
    ///
    /// 异常卡口径（2026-10-16 全局对齐定案）：断链卡（卡表无此 ID）无法展示——展示钮灰仅可
    /// 删除；形状违规/主题违色卡可展示但红字标记；整套含任一异常即不可保存，卡组列表/挑战/
    /// 对战入口按 DeckCompositionValidator.UnusableTag 同源标记（单源 Usable）。
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

        // 英雄技能标记卡 ID（技能栏常显；null=未设置——保存口必选拦截；资格自愈同旧版）。
        private string _skillCardId;

        // 卡组名缓存：屏实例按类型缓存，但预制体层级每进屏重建——跳卡牌编辑返回时由此恢复输入框。
        private string _currentDeckName;

        // 右栏卡组列表选中名（null=新建态）；保存成功后指向保存名（行高亮对账）。
        private string _selectedDeckName;

        // 中栏只读卡面绑定（CardOverlayController 把 ACard 挂进 card-slot 拉伸铺满）。
        private readonly List<CardOverlayBinding> _previewBindings = new List<CardOverlayBinding>();
        private CardData _previewCard;

        // 两套过滤（2026-10-16 收敛定案）：色 chips（预制体烘焙）+ 卡型 chips（列表首行运行时建）。
        private readonly Dictionary<UIColor, UButton> _filterChips = new Dictionary<UIColor, UButton>();
        private UIColor _colorFilter = UIColor.All;
        private string _kindFilterZh; // null = 全部（技能=可标记英雄技能的法术，独立第四档）
        private static readonly string[] KindFilterChoices = { "生物", "法术", "结界", "技能" };

        // ---- 绑定节点 ----
        private UiKit.Scroll _catalogList;
        private UiKit.Scroll _deckList;
        private UiKit.Scroll _decksList;
        private UiKit.Scroll _statsZone;
        private RectTransform _cardSlot;
        private RectTransform _overlay;
        private RectTransform _catalogFilter;
        private TMP_Text _summary;
        private TMP_Text _toast;
        private TMP_Text _previewNote;
        private TMP_Text _skillLabel;
        private TMP_Text _skillCostLabel;
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
                // 主题约束随卡组名实时换算（构成检测块）；名缓存供跳编辑返回恢复
                _nameField.onValueChanged.AddListener(v =>
                {
                    _currentDeckName = v;
                    RefreshStats();
                });
            BindButton("btn-new", OnNewDeck);
            BindButton("btn-save", OnSave);
            _summary = FindText("lbl-summary");
            _toast = FindText("lbl-toast");
            _deckList = FindScroll("list-deck");
            _statsZone = FindScroll("stats-zone");

            // ---- 中栏：只读卡面 + 英雄技能栏 ----
            _cardSlot = Find("card-slot");
            _previewNote = FindText("preview-note");
            _skillLabel = FindText("lbl-skill");
            _skillCostLabel = FindText("lbl-skill-cost");
            _skillClearBtn = BindButton("btn-skill-clear", OnClearSkill);

            // ---- 右栏：卡池过滤 / 搜索 / 卡组列表 ----
            _catalogFilter = Find("catalog-filter");
            _catalogList = FindScroll("list-catalog");
            _searchField = FindInput("field-search");
            if (_searchField != null)
                _searchField.onValueChanged.AddListener(_ => RefreshCatalog());
            _decksList = FindScroll("list-decks");

            BindFilterRow();
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
            // UGUI 卡牌层回收（中栏卡面独立池化，切屏必须显式清）
            CardOverlayController.ClearActive();
        }

        // ======================================== 右栏：卡池列表 ========================================

        /// <summary>颜色 chips 绑定（预制体烘焙 chip-{Color}，一次接线；激活态=Outline 开关）。</summary>
        private void BindFilterRow()
        {
            if (_catalogFilter == null) return;
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

        /// <summary>激活态切换：当前过滤色的 chip 加描边（预制体化后不重建行，只开关 Outline）。</summary>
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

        private void RefreshCatalog()
        {
            ClearContent(_catalogList.Content);
            BuildKindRow(); // 卡型 chips 行=内容首行，随刷新重建

            IEnumerable<CardData> cards = CardCatalog.LoadPlayPool(); // 教学专用卡不进构筑卡池
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

            if (sorted.Count == 0)
            {
                MakeEmptyState(query);
                return;
            }
            foreach (var card in sorted)
                MakeCatalogRow(card);
        }

        /// <summary>卡型 chips 行（内容首行）：全部/生物/法术/结界/技能。</summary>
        private void BuildKindRow()
        {
            var kindRow = UiKit.Row("facet-kind", _catalogList.Content, spacing: 4f, pad: 4f);
            UiKit.Size(kindRow, fw: 1f);
            MakeChip("全部", kindRow, _kindFilterZh == null, () => { _kindFilterZh = null; RefreshCatalog(); });
            foreach (var kind in KindFilterChoices)
            {
                var captured = kind;
                MakeChip(captured, kindRow, _kindFilterZh == captured,
                    () => { _kindFilterZh = captured; RefreshCatalog(); });
            }
        }

        /// <summary>chip：MiniButton+激活描边（随内容全量重建，激活态建行时烧入）。</summary>
        private static void MakeChip(string label, RectTransform parent, bool active, Action onClick)
        {
            var chip = UiKit.MiniButton("chip-" + label, parent, label, onClick);
            if (!active) return;
            var ol = chip.gameObject.AddComponent<Outline>();
            ol.effectColor = UiStyle.TextPrimary;
            ol.effectDistance = new Vector2(1.5f, -1.5f);
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

        /// <summary>空结果可解释：列出当前生效的全部筛选 +「清除全部」。</summary>
        private void MakeEmptyState(string query)
        {
            var parts = new List<string>();
            if (_colorFilter != UIColor.All) parts.Add($"色：{ColorZh(_colorFilter)}");
            if (_kindFilterZh != null) parts.Add($"型：{_kindFilterZh}");
            if (SearchQuery.IsActive(query)) parts.Add($"词：{query.Trim()}");
            var hint = UiKit.Label("empty", _catalogList.Content,
                parts.Count > 0 ? $"（无匹配卡牌）当前筛选：{string.Join(" + ", parts)}" : "（无卡牌可显示）",
                UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
            UiKit.Size(hint, fw: 1f);
            if (parts.Count > 0)
                UiKit.MiniButton("btn-clear-filters", _catalogList.Content, "清除全部筛选", ClearAllFilters);
        }

        private void ClearAllFilters()
        {
            _colorFilter = UIColor.All;
            _kindFilterZh = null;
            if (_searchField != null) _searchField.SetTextWithoutNotify("");
            UpdateFilterChips(); // 色 chips 为 prefab 常驻，单独恢复
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

        /// <summary>卡池行：费用徽标+卡名+类型身材，行点击=中栏预览；行尾「技能/编辑/添加」——
        /// 技能钮仅资格卡显示（填入中栏英雄技能栏）；编辑=跳卡牌编辑界面。</summary>
        private void MakeCatalogRow(CardData card)
        {
            var captured = card;
            var row = UiKit.Row("row", _catalogList.Content, spacing: 8f, pad: 6f);
            var rowBg = UiKit.BgRow(row);
            rowBg.raycastTarget = true; // 行背景可点（行点击=预览；行内按钮在上层，互不串扰）
            UiKit.Size(row, h: 40f);

            CostBadge("cost", row, ((int)card.TotalCost).ToString());
            var name = UiKit.Label("name", row, NameOf(card), UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            UiKit.Label("meta", row, TypeStatLine(card), UiStyle.SmallSize, UiStyle.TextFaint);
            if (HeroSkillSystem.CanBeSkillCard(card))
                UiKit.MiniButton("skill", row, "技能", () => SetSkill(captured));
            UiKit.MiniButton("edit", row, "编辑", () => EditCard(captured));
            UiKit.MiniButton("add", row, "添加", () => AddCard(captured.ID));

            var rowBtn = row.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            rowBtn.targetGraphic = rowBg;
            rowBtn.onClick.AddListener(() => ShowPreview(captured));
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
            if (_deckCardIds.Count >= MaxDeckCards)
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
                // 主题双色约束：红/绿/蓝开头的卡组拒加主题外颜色卡（两层费用同检测器口径）
                var theme = CurrentTheme();
                if (theme != null)
                {
                    var foreign = DeckCompositionValidator.CheckCard(card, theme.Value);
                    if (foreign.Count > 0)
                    {
                        var v = foreign[0];
                        ShowToast($"「{NameOf(card)}」{v.Surface} 含" +
                                  $"{DeckCompositionValidator.ColorZh(v.Color)}{v.Amount:0.#}——" +
                                  $"{DeckCompositionValidator.ColorZh(theme.Value)}+灰卡组不可添加");
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

        /// <summary>移出一册（2026-10-16 聚合行语义：同卡多册只减 1；技能标记不随移除清除）。</summary>
        private void RemoveCopy(string cardId)
        {
            _deckCardIds.Remove(cardId);
            RefreshAll();
        }

        /// <summary>左栏当前卡组（同卡聚合 ×册数，保序=首次入组序）：行=[费用徽标|卡名×N|异常
        /// 标记|展示|删除]。断链卡（卡表无此 ID）无法展示——展示钮灰、仅可删除；形状违规/主题
        /// 违色卡可展示、行尾红字标记。</summary>
        private void RefreshDeckList()
        {
            ClearContent(_deckList.Content);
            var theme = CurrentTheme();

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

            if (ids.Count == 0)
            {
                var hint = UiKit.Label("empty", _deckList.Content,
                    "（空卡组——右侧卡池点「添加」入组）", UiStyle.SmallSize, UiStyle.TextHint);
                UiKit.Size(hint, fw: 1f);
                return;
            }

            for (int i = 0; i < ids.Count; i++)
                MakeDeckRow(ids[i], CardCatalog.GetById(ids[i]), counts[i], theme);
        }

        private void MakeDeckRow(string cardId, CardData card, int copies, ManaType? theme)
        {
            var row = UiKit.Row("row", _deckList.Content, spacing: 6f, pad: 6f);
            UiKit.BgRow(row).raycastTarget = false;
            UiKit.Size(row, h: 38f);

            if (card == null)
            {
                // 断链卡：无卡面数据可展示——展示钮灰（interactable=false），只能删除
                var miss = UiKit.Label("name", row, $"[断链] {cardId} ×{copies}",
                    UiStyle.BodySize, UiStyle.DotRed);
                UiKit.Size(miss, fw: 1f);
                var showBtn = UiKit.MiniButton("show", row, "展示", null);
                showBtn.interactable = false;
                UiKit.MiniButton("del", row, "删除", () => RemoveCopy(cardId),
                    bg: UiStyle.BtnDanger, fg: UiStyle.White);
                return;
            }

            var captured = card;
            CostBadge("cost", row, ((int)card.TotalCost).ToString());
            var name = UiKit.Label("name", row,
                NameOf(card) + (copies > 1 ? $" ×{copies}" : ""), UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);

            var flags = CardFlags(card, theme);
            if (flags.Count > 0)
                UiKit.Label("flag", row, "✘" + string.Join("/", flags), UiStyle.SmallSize, UiStyle.DotRed);

            UiKit.MiniButton("show", row, "展示", () => ShowPreview(captured));
            UiKit.MiniButton("del", row, "删除", () => RemoveCopy(cardId),
                bg: UiStyle.BtnDanger, fg: UiStyle.White);
        }

        /// <summary>单卡异常标记（左栏行尾红字/中栏 note，短词）：形状违规 / 主题违色。</summary>
        private static List<string> CardFlags(CardData card, ManaType? theme)
        {
            var flags = new List<string>();
            if (DeckCompositionValidator.CheckShape(card).Count > 0) flags.Add("形状");
            if (theme != null && DeckCompositionValidator.CheckCard(card, theme.Value).Count > 0) flags.Add("违色");
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
                _summary.text = $"卡数 {_deckCardIds.Count}（需 {MinDeckCards}-{MaxDeckCards}） · " +
                                $"合计费用 {(int)totalCost}";
        }

        // ======================================== 中栏：只读卡面 + 技能栏 ========================================

        private void ShowPreview(CardData card)
        {
            _previewCard = card;
            BuildPreview();
            // 行点击路径不走 RefreshAll——卡面层单独重绑
            CardOverlayController.Instance.Bind(_previewBindings);
        }

        /// <summary>中栏卡面：FromCardDataFull 六区静态快照（与手牌/合成器同族，去运行态）；
        /// 异常卡在 note 行给出首条明细（形状违规人读串 / 违色 Detail）。</summary>
        private void BuildPreview()
        {
            _previewBindings.Clear();
            var card = _previewCard;
            if (_previewNote == null) return;
            if (card == null)
            {
                _previewNote.text = "点击左右列表的行，在此显示卡面";
                return;
            }
            _previewBindings.Add(new CardOverlayBinding
            {
                Slot = _cardSlot,
                Item = CardOverlayItem.FromCardDataFull(card, "Preview"),
                Layout = CardOverlayLayout.Full,
            });

            string note = "";
            var shapeErrs = DeckCompositionValidator.CheckShape(card);
            if (shapeErrs.Count > 0)
                note = "⚠ " + shapeErrs[0];
            else
            {
                var theme = CurrentTheme();
                if (theme != null)
                {
                    var vs = DeckCompositionValidator.CheckCard(card, theme.Value);
                    if (vs.Count > 0) note = "⚠ 主题违色——" + vs[0].Detail;
                }
            }
            _previewNote.text = note; // 空=无异常（卡面下方留白）
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

        // ======================================== 统计区（左栏底部滚动区） ========================================

        private void RefreshStats()
        {
            if (_statsZone == null) return;
            ClearContent(_statsZone.Content);
            var cards = _deckCardIds
                .Select(id => CardCatalog.GetById(id))
                .Where(c => c != null)
                .ToList();

            BuildCompositionCheck();
            BuildCostCurve(cards);
            BuildColorDistribution(cards);
            BuildTypeRatio(cards);
        }

        /// <summary>当前卡组名主题色（红/绿/蓝首字；其余 null=无约束）——
        /// 添加口/保存口/统计区检测块/行内标记共用。</summary>
        private ManaType? CurrentTheme()
            => DeckCompositionValidator.ThemeColorOf(
                _nameField != null ? _nameField.text : _currentDeckName);

        /// <summary>构成检测块（统计区首块常显）：主题双色约束结果+断链+形状——加卡/删卡/改名
        /// 实时刷新。违色在添加口已拦，此处兜底旧档载入与卡数据热改；口径=DeckCompositionValidator。</summary>
        private void BuildCompositionCheck()
        {
            var zone = _statsZone.Content;
            UiKit.Label("check-header", zone, "构成检测", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);

            var report = DeckCompositionValidator.Check(
                _nameField != null ? _nameField.text : _currentDeckName, _deckCardIds);

            if (report.ThemeColor == null)
            {
                var hint = UiKit.Label("check-theme", zone,
                    "无主题约束（卡组名以 红/绿/蓝 开头才启用「主题色+灰」双色检测）",
                    UiStyle.SmallSize, UiStyle.TextHint, wrap: true);
                UiKit.Size(hint, fw: 1f);
                if (report.MissingIds.Count == 0) return;
            }
            else
            {
                var line = UiKit.Label("check-theme", zone,
                    $"主题约束：{DeckCompositionValidator.ColorZh(report.ThemeColor.Value)}+灰",
                    UiStyle.SmallSize, UiStyle.TextSecondary);
                UiKit.Size(line, fw: 1f);
            }

            if (report.Ok)
            {
                var ok = UiKit.Label("check-ok", zone,
                    report.CardCount == 0 ? "（空卡组）" : "✔ 通过——无主题外颜色、无断链",
                    UiStyle.SmallSize, UiStyle.DotGreen);
                UiKit.Size(ok, fw: 1f);
                return;
            }

            const int maxRows = 8; // 超出折叠计数——违例长列表滚动区放不下
            int shown = 0;
            foreach (var v in report.Violations)
            {
                if (shown++ >= maxRows) break;
                var row = UiKit.Label("check-bad", zone,
                    $"✘ {v.CardName} ×{v.Copies}｜{v.Surface}｜" +
                    $"{DeckCompositionValidator.ColorZh(v.Color)}{v.Amount:0.#}",
                    UiStyle.SmallSize, UiStyle.DotRed, wrap: true);
                UiKit.Size(row, fw: 1f);
            }
            if (report.Violations.Count > maxRows)
            {
                var more = UiKit.Label("check-more", zone,
                    $"……另有 {report.Violations.Count - maxRows} 项违色",
                    UiStyle.SmallSize, UiStyle.DotRed);
                UiKit.Size(more, fw: 1f);
            }
            foreach (var err in report.ShapeErrors)
            {
                var se = UiKit.Label("check-shape", zone, "✘ " + err,
                    UiStyle.SmallSize, UiStyle.DotRed, wrap: true);
                UiKit.Size(se, fw: 1f);
            }
            foreach (var id in report.MissingIds)
            {
                var miss = UiKit.Label("check-missing", zone,
                    $"✘ 断链卡 {id}——卡表中无此 ID", UiStyle.SmallSize, UiStyle.DotRed, wrap: true);
                UiKit.Size(miss, fw: 1f);
            }
        }

        /// <summary>费用曲线：1-9 档（>9 并入 9+）条形分布。</summary>
        private void BuildCostCurve(List<CardData> cards)
        {
            var zone = _statsZone.Content;
            var tiers = new int[10]; // 0 位弃用；1..9；>9 并入 9（标签显示 9+）
            foreach (var card in cards)
            {
                int tier = Mathf.Clamp(Mathf.RoundToInt(card.TotalCost), 1, 9);
                tiers[tier]++;
            }

            UiKit.Label("curve-header", zone, "费用曲线", UiStyle.HeaderSize,
                UiStyle.TextSecondary, TextAnchor.LowerLeft, FontStyle.Bold);

            var chart = UiKit.Row("curve", zone, spacing: 2f);
            chart.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft; // 条形自底生长
            UiKit.Size(chart, fw: 1f, h: 64f);
            int max = tiers.Skip(1).Max();
            for (int tier = 1; tier <= 9; tier++)
            {
                int count = tiers[tier];
                var cell = UiKit.Column("tier-" + tier, chart, spacing: 0f);
                cell.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerCenter;
                UiKit.Size(cell, w: 26f, fh: 1f);

                var bar = UiKit.Node("bar", cell);
                var barImg = bar.gameObject.AddComponent<Image>();
                barImg.color = new Color(0.27f, 0.43f, 0.71f, 0.9f);
                UiKit.Size(bar, w: 14f, h: count > 0 ? Mathf.Max(6f, (int)(52f * count / Mathf.Max(1, max))) : 2f);

                var countLabel = UiKit.Label("count", cell, count > 0 ? count.ToString() : "",
                    UiStyle.SmallSize, UiStyle.TextFaint, TextAnchor.MiddleCenter);
                var tierLabel = UiKit.Label("tier", cell, tier == 9 ? "9+" : tier.ToString(),
                    UiStyle.SmallSize, UiStyle.TextHint, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>颜色分布：六色计数（色点+数字）。</summary>
        private void BuildColorDistribution(List<CardData> cards)
        {
            var row = UiKit.Row("colors", _statsZone.Content, spacing: 4f);

            foreach (UIColor color in new[] { UIColor.Red, UIColor.Blue, UIColor.Green, UIColor.Gray, UIColor.Black, UIColor.White })
            {
                int count = cards.Count(c => CardSorter.HasCostColor(c, color));
                UiKit.Dot($"dot-{color}", row, DotColor(color));
                var num = UiKit.Label("num", row, $"{count}", UiStyle.SmallSize, UiStyle.TextFaint);
                UiKit.Size(num, w: 26f);
            }
        }

        private static Color DotColor(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return UiStyle.DotRed;
                case UIColor.Blue: return UiStyle.DotBlue;
                case UIColor.Green: return UiStyle.DotGreen;
                case UIColor.Black: return UiStyle.DotBlack;
                case UIColor.White: return UiStyle.DotWhite;
                default: return UiStyle.DotGray;
            }
        }

        /// <summary>类型比例：生物/法术/结界计数。</summary>
        private void BuildTypeRatio(List<CardData> cards)
        {
            int creatures = cards.Count(c => c.Supertype == Cardtype.Creature);
            int spells = cards.Count(c => c.Supertype == Cardtype.Spell);
            int enchantments = cards.Count(c => c.Supertype == Cardtype.Enchantment);
            int other = cards.Count - creatures - spells - enchantments;

            var line = UiKit.Label("type-ratio", _statsZone.Content,
                $"类型：生物 {creatures} · 法术 {spells} · 结界 {enchantments}"
                + (other > 0 ? $" · 其他 {other}" : ""),
                UiStyle.SmallSize, UiStyle.TextDim);
            UiKit.Size(line, fw: 1f);
        }

        // ======================================== 右栏：卡组列表 ========================================

        /// <summary>卡组列表刷新：行=[卡组名|张数·不可用标记|✕删除]；行点击=载入左栏编辑。
        /// 不可用口径=DeckCompositionValidator.UnusableTag（断链/形状/违色任一即不可用，
        /// 与挑战/对战入口全局同源——2026-10-16 对齐定案）。</summary>
        private void RefreshDecksList()
        {
            if (_decksList == null) return;
            ClearContent(_decksList.Content);
            var decks = DeckSerializer.LoadAll();
            if (decks.Count == 0)
            {
                var hint = UiKit.Label("empty", _decksList.Content,
                    "（无存档卡组——左栏构筑后点「保存」）", UiStyle.SmallSize, UiStyle.TextHint);
                UiKit.Size(hint, fw: 1f);
                return;
            }
            foreach (var deck in decks)
            {
                if (deck == null || string.IsNullOrEmpty(deck.name)) continue;
                MakeDeckListRow(deck);
            }
        }

        private void MakeDeckListRow(DeckData deck)
        {
            var row = UiKit.Row("deck-row", _decksList.Content, spacing: 8f, pad: 6f);
            var bg = UiKit.BgRow(row);
            if (deck.name == _selectedDeckName) bg.color = UiStyle.SelectedRowBg;
            bg.raycastTarget = true;
            UiKit.Size(row, h: 34f);

            var name = UiKit.Label("name", row, deck.name, UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            var tag = DeckCompositionValidator.UnusableTag(deck);
            UiKit.Label("meta", row,
                $"{deck.cardIds?.Count ?? 0} 张" + (tag.Length > 0 ? "·" + tag : ""),
                UiStyle.SmallSize, tag.Length > 0 ? UiStyle.DotRed : UiStyle.TextFaint);

            var captured = deck.name;
            UiKit.MiniButton("del", row, "✕", () => ConfirmDeleteDeck(captured),
                bg: UiStyle.BtnDanger, fg: UiStyle.White);

            var rowBtn = row.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            rowBtn.targetGraphic = bg;
            rowBtn.onClick.AddListener(() => LoadDeck(captured));
        }

        private void ConfirmDeleteDeck(string name)
        {
            UiKit.ConfirmBox(Root, "删除卡组",
                $"确定删除卡组「{name}」？该操作不可恢复。",
                "删除", () => DeleteDeck(name));
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

            // UGUI 卡面层重绑（中栏卡面挂 card-slot；滚动裁剪由 RectMask2D/Mask 自动处理）
            CardOverlayController.Instance.Bind(_previewBindings);
        }

        // ======================================== 保存 ========================================

        private void OnSave()
        {
            var name = string.IsNullOrWhiteSpace(_nameField != null ? _nameField.text : _currentDeckName)
                ? "新卡组"
                : (_nameField != null ? _nameField.text : _currentDeckName).Trim();

            // 构筑规则③：卡组 30-60 张（卡可重复任意张）
            if (_deckCardIds.Count < MinDeckCards || _deckCardIds.Count > MaxDeckCards)
            {
                ShowToast($"卡组须 {MinDeckCards}-{MaxDeckCards} 张（当前 {_deckCardIds.Count} 张）");
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

            // 异常卡硬门槛（2026-10-16 全局对齐）：断链/形状违规/主题违色任一即不可保存
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
            if (composition.Violations.Count > 0)
            {
                var v0 = composition.Violations[0];
                ShowToast($"主题约束 {DeckCompositionValidator.ColorZh(composition.ThemeColor.Value)}+灰：" +
                          $"{composition.Violations.Count} 项违色（首项 {v0.CardName}｜{v0.Surface}｜" +
                          $"{DeckCompositionValidator.ColorZh(v0.Color)}{v0.Amount:0.#}）——不可保存");
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

        /// <summary>费用徽标（蓝色小圆片+数字，对标 .cost-badge）。</summary>
        private static TMP_Text CostBadge(string name, RectTransform parent, string value)
        {
            var rt = UiKit.Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiKit.CircleSprite;
            img.color = UiStyle.BtnPrimary;
            var lbl = UiKit.Label("label", rt, value, UiStyle.SmallSize, UiStyle.White,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.StretchInset(lbl.rectTransform, 2f, 0f);
            UiKit.Size(rt, w: 26f, h: 24f);
            return lbl;
        }

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
