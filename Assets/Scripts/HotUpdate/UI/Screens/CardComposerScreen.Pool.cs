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
    public sealed partial class CardComposerScreen : UIScreen
    {
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
            // 2026-11-09：Unity Mathf.Round 是银行家舍入（4.5→4）本就不对；改逐色进位同款 ceil 兜底
            //（调用方多已传进位后整数，两者一致——防直传真值时显示偏低）
            if (num != null) num.text = Mathf.CeilToInt(cost.Total - 1e-4f).ToString();
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

    }
}
