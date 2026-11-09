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
            var badgeCost = graph?.header?.AnchorCost; // 2026-11-09 逐色进位：效果库行费标=卡侧整数口径（效果组合界面另直读真值）
            BindCostBadge(row, badgeCost == null ? null : ElementPaymentValidator.CeilBill(badgeCost));
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

    }
}
