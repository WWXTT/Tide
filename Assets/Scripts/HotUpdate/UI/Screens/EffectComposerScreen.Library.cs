using Cysharp.Threading.Tasks;
using CardCore;
using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UInputField = TMPro.TMP_InputField;

namespace SynergyUI
{
    public sealed partial class EffectComposerScreen : UIScreen
    {
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
            // 费用升序（2026-11-06，与原子表 2026-10-13 定案同口径）：按效果锚价合计排——
            // 不计价/零费列最前；同价稳定排序保表序
            foreach (var fx in effects.OrderBy(f => f?.header?.AnchorCost?.Total ?? 0f))
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
                    Tag = EffectTableTag(captured, parts), // 表归属∪组合方式（2026-11-05：组成简称退役）
                    Meta = AtomText.RenderEffectSummary(captured), // 完整效果描述（2026-10-03 定案：不再简写）
                    Dot = DotColor(ColorFilter.OfTagsCsv(parts.FirstOrDefault()?.Tags)),
                    Costs = captured.header?.AnchorCost ?? new ElementCost(), // 0费显灰0（紧凑发射省略零费列→null 归一，CostSquares 统一口径）
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
            // 费用升序（2026-11-06）：预设段同口径——锚价合计升序，同价稳定
            foreach (var fx in EffectLibrarySerializer.LoadAll(true)
                         .OrderBy(f => f?.header?.AnchorCost?.Total ?? 0f))
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
                    Tag = EffectTableTag(captured, parts), // 表归属∪组合方式（2026-11-05：预设卡组名退役——出处仍在搜索语料）
                    Meta = AtomText.RenderEffectSummary(captured) + "（预设效果——保存另存为新效果，不可删除）",
                    Dot = DotColor(ColorFilter.OfTagsCsv(parts.FirstOrDefault()?.Tags)),
                    Costs = captured.header?.AnchorCost ?? new ElementCost(), // 0费显灰0（同主表口径）
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
            SynthesizeCostGateView(); // 代价门视图合成（2026-11-08 效果级化）：预设代价行同样进编辑视图

            _mode = InferMode(_graph);
            _expandedAtoms.Clear();
            _expandedRewards.Clear();
            EnsureDefaultSelection();
            SyncTimingDropdown();
            _activationDropdown?.SetIndex(ActivationIndex(_graph.header.ActivationType));
            SyncActivationVisibility();
            RefreshAll();
            UpdateDeleteState(); // 预设载入=删除钮隐藏
            // 快照在归一化/代价门合成/效果头对齐（RefreshAll 链内 SyncActivationVisibility 等可能钉时机）全部落定后拍
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
            SynthesizeCostGateView(); // 代价门视图合成（2026-11-08 效果级化）：库条目代价行进编辑视图

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
                row.DisplayName, row.Description, row.Tags, row.MountKinds, row.Domain); // B2：TargetKinds→Domain

        /// <summary>当前选中槽的可放置性谓词（库只展示可用项——与分类/搜索取交集；2026-10-23 五分类）。
        /// 附带中文上下文（右栏顶部 AddTableHint 提示行消费）。并列=普通主干原子（排除引擎行）；
        /// 自由分支主干=仅引擎行（位 5）；有限分支主干=普通原子（排引擎/门槛行）；奖励块=BranchReward
        /// 位+非错边+预算内（自由恒 6、有限=门槛 Budget 钳 1~3）。门槛选中态/光环形态不走本口（RefreshLibrary 分流）。</summary>
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
                        var b = BranchOfSlot(0);
                        // 附加诅咒/祝福（2026-11-06 载荷效果化）：无 Then 奖励口——载荷=主干卡「载荷效果」行
                        if (b != null && IsCurseEngine((BranchEngineKind)b.engine))
                        {
                            contextZh = "附加诅咒/祝福：载荷=效果组合引用——点主干卡「载荷效果」行选择（不填原子奖励）";
                            return _ => false;
                        }
                        // 引擎奖励分支（2026-11-08 阶段4·奖励=效果引用单通道）：库不再供应原子——
                        // then 原子通道对引擎奖励退役，选择走奖励区「奖励效果」行弹层
                        if (b != null && (BranchSettleKind)b.settle == BranchSettleKind.Engine)
                        {
                            contextZh = "引擎奖励=组合效果引用——点奖励区「奖励效果」行选择（不再填原子奖励）";
                            return _ => false;
                        }
                        // 自由分支奖励：上限恒 6（2026-11-02 减费三选一：原 5 放宽一档——玩家在 1-6 费区间自由调，
                        // 门槛随计费推导、超上限红闪禁存；预言分支走 RewardCapOf 押注行 Budget）
                        int cap = RewardCapOf(b);
                        contextZh = $"Then 奖励（非错边——仅示 ≤{cap} 费原子）";
                        return lp => b != null && lp.CanBeBranchReward && !lp.IsWrongSideOnly
                                     && RewardWithinFilterCap(lp.Entry(), cap);
                    }
                    contextZh = "主干（引擎行——倒计时/拼点/运势/死亡计数/元素充盈/团结/附加诅咒·祝福）";
                    return lp => lp.IsEngineTrunk;
                }
                case ComposeMode.LimitedBranch:
                {
                    if (_selSlot == SelKind.Reward)
                    {
                        var b = BranchOfSlot(0);
                        // 代价门（2026-11-06）：第二槽=代价原子——候选=一般效果行·双侧域·有计价色
                        //（CostGateCandidate；减免=全价口径，超3由槽区校验拦），不走奖励预算筛选
                        if (b != null && IsCostGate(b))
                        {
                            var costColors = CostGateColors();
                            contextZh = "代价（一般效果行·双侧域·与效果同色——减免等值费用 ≤3）";
                            return lp => CostGateCandidate(lp, costColors);
                        }
                        // 有限分支奖励：上限=该门槛 Budget（RewardFilterCap 单源；预言分支 RewardCapOf 猜法档）
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

        /// <param name="keepScroll">点选落位路径（QuickAdd/光环挂载）传 true：保位重铺
        /// （2026-11-07 修复——落位原子隐藏刷库曾整体回顶，滑动选中即弹回第一行）。
        /// 筛选/搜索/模式切换/门槛选择（列表内容整个换掉）保持回顶。</param>
        private void RefreshLibrary(bool keepScroll = false)
        {
            if (_atomRows == null) return;
            _atomItems.Clear();
            void Apply() =>
                _atomRows.SetItems(_atomItems.Count, i => _atomItems[i].H);

            // 光环双形态：右栏=光环条目库（连接）/仪典库（范围）——点击换选，编辑在左栏
            if (IsAuraMode(_mode))
            {
                RefreshAuraLibrary();
                if (keepScroll) _atomRows.SetItemsKeepScroll(_atomItems.Count, i => _atomItems[i].H);
                else Apply();
                return;
            }

            // 有限分支·门槛选中态：右栏=11 条门槛（表驱动——颜色筛选不适用，门槛无费用色；搜索照旧可筛）
            if (_mode == ComposeMode.LimitedBranch && _selSlot == SelKind.Gate)
            {
                AddTableHint(_atomItems, _libraryList, "当前：填入门槛——点一条即填入（达标发奖，不达标不奖不惩）");
                foreach (var g in ComposerCatalog.SituationGates)
                {
                    var captured = g;
                    // 支付代价门 2026-11-08 效果级化放开：库独立模式也可选——代价随效果落库（EffectSlim costs）
                    if (!SearchQuery.Match(captured.DisplayName ?? "", _filterName.text)) continue;
                    _atomItems.Add(new TableRowData
                    {
                        Meta = ComposerCatalog.GateLabel(captured),
                        Dot = DotColor(UIColor.All),
                        Costs = null, // 门槛行无费用方格
                        OnClick = () => ApplySituationGate(captured),
                        H = TableRowH,
                    });
                }
                if (_atomItems.Count == 1) // 只剩提示行=门槛目录为空
                    AddTableHint(_atomItems, _libraryList, "门槛目录为空——原子表未装载门槛行（查 Console）");
                Apply();
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
                    Tag = AtomicEffectTable.CategoryZhOf(payload.Cfg.TableCategory), // 所在原子表（2026-11-05：表色回填色名退役）
                    // 关键词行（2026-11-07 特判）：库行把 {target} 换成「己方战场生物」——不再是自指；
                    // 卡面效果文本另走 AtomText（按实际实例域/数量渲染）。
                    Meta = (payload.IsKeywordAtom
                            ? (payload.Cfg.Description ?? "").Replace("{target}", "己方战场生物")
                            : payload.Cfg.Description)
                           + CounterClearSuffix(payload.Cfg.Tags), // 2026-10-13 定案：指示物类行统一追加清除方式
                    Dot = DotColor(ColorFilter.OfTagsCsv(payload.Cfg.Tags)),
                    Costs = AtomCosts(payload.Cfg),
                    OnClick = () => QuickAdd(payload),
                    H = TableRowH,
                });

            // —— 我的词条分区（G5 合成器投影 2026-11-08 · 地基计划 §8）：内置行后追加，不新开界面。
            //    元数据从**展开体**派生（CompositeAtomProjection——价=Σ展开行单价·占槽数=展开原子数·
            //    描述=词条名+展开体行文案），不从词条自述读；搜索同筛（词条跨色，v1 不参与色筛）。
            var compositeDefs = CardCore.CompositeAtomTable.GetAll();
            if (compositeDefs.Count > 0)
            {
                AddTableHint(_atomItems, _libraryList, "—— 我的词条（虚拟原子·点击填入并列主干·展开计价）——");
                foreach (var def in compositeDefs.OrderBy(d => d.id, System.StringComparer.Ordinal))
                {
                    var captured = def;
                    var info = CardCore.CompositeAtomProjection.Project(captured);
                    if (!SearchQuery.Match(captured.name + " " + captured.id + " " + info.Description,
                            _filterName.text)) continue;
                    _atomItems.Add(new TableRowData
                    {
                        Tag = "词条",
                        Meta = info.Description,
                        Dot = DotColor(UIColor.All),
                        Costs = info.Cost != null && !info.Cost.IsZero ? info.Cost : null,
                        OnClick = () => QuickAddComposite(captured),
                        H = TableRowH,
                    });
                }
            }

            if (keepScroll) _atomRows.SetItemsKeepScroll(_atomItems.Count, i => _atomItems[i].H);
            else Apply();
        }

        /// <summary>词条落槽（G5）：v1 只作并列主干——写入 @ 引用步（value=Value 参缺省绑定）；
        /// 计价/校验经 ConvertAll 的展开面（CardEffectConverter 词条步展开）；保存写回 @ 引用步
        ///（重载不变形——展开只发生在转换/装载消费口，编辑图恒保引用形态）。</summary>
        private void QuickAddComposite(CardCore.CompositeAtomDef def)
        {
            if (_mode != ComposeMode.Parallel)
            {
                ShowToast("词条只能作并列主干（v1——分支奖励/门槛/光环不收词条）");
                return;
            }
            var entry = new AtomicEffectEntry { refId = def.id, value = 1 };
            foreach (var p in def.@params ?? new List<CardCore.CompositeAtomParam>())
                if (p != null && p.type != "Str" && p.defaultValue > 0) entry.value = p.defaultValue;
            ReplaceParallel(entry);
            RefreshLibrary(keepScroll: true);
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

        /// <summary>右栏行绑定共用（2026-10-24 改版）：首行=费用方格(32)+标签(16 右对齐)
        ///（2026-11-06：名称文本退役——效果无名字，模板 name 节点已删），
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
            var tagT = UiKit.FindDeep(row, "tag")?.GetComponent<TMP_Text>();
            if (tagT != null) tagT.text = item.Tag ?? "";
            var descT = UiKit.FindDeep(row, "desc")?.GetComponent<TMP_Text>();
            if (descT == null)
                Debug.LogError("[EffectComposer] row 模板缺 desc 文本——本行描述丢失；检查 EffectUI.prefab");
            else descT.text = item.Meta ?? "";

            var rowView = row.GetComponentInChildren<CostSquaresView>(true);
            if (rowView != null)
            {
                // 0费=单块灰「0」占位（2026-11-03 手牌定案·2026-11-06 全局统一——全零也 SetCosts）
                if (item.Costs != null) rowView.SetCosts(item.Costs);
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
            //（RuleAuraScope；2026-11-06 定案：落选缺省恒双方=表价档——极性 0 行卡面锁双方不可改，
            // ±1 行卡面可收窄极性单侧（+1 己方/−1 对方），计价 ×RuleAuraSingleSideFactor）；
            // 不占 LinkAura 连接位（无箭头）。新选即整组替换（属性降低条目同槽互斥）
            void AddRuleAura(AtomicEffectConfig cfg)
            {
                // 阶段D（2026-10-31）：规则 id 从行类型派生（RuleAuraComponents 映射单源）——
                // 新型行不再写 str（投放/同名闸/构筑唯一槽全走类型）；表 EffectType 未翻新行
                //（仍=ModifyGameRule）回落 RuleAuraIds 显示名映射并写 str（handler 过渡回落）
                Enum.TryParse<AtomicEffectType>(cfg.EnumName ?? "", out var rt);
                var rid = RuleAuraComponents.RuleIdOfType(rt)
                          ?? (RuleAuraIds.TryGetValue(cfg.DisplayName, out var r) ? r : cfg.DisplayName);
                _graph.steps = new List<EffectStepData> { new EffectStepData { kind = 0,
                    atomic = new AtomicEffectEntry { refId = cfg.HashId,
                        value = (int)RuleAuraScope.Both, // 缺省双方=表价档（极性 0 行唯一合法值）
                        str = rt == AtomicEffectType.ModifyGameRule ? rid : null } } };
                _graph.header.LinkAuras = null; // 仪典与属性降低条目同槽互斥——落仪典清条目
                _graph.header.ArrowDirections = 0; // 规则级无箭头（MakeAuraPanel 范围面分支同置——落位即写一致）
                RefreshSlots();
            }

            // ---- 场地光环面（2026-10-30 自规则光环改名）：仪典行（位 6）+ 属性降低条目（自连接光环挪入） ----
            if (_mode == ComposeMode.RangeAura)
            {
                AddTableHint(_atomItems, _libraryList, "当前：仪典/属性降低——点击选择（新选即替换；极性 0 仪典恒对双方，±1 仪典卡面可选单侧（计价 ×1.5），属性降低仅对对方生物生效）");
                foreach (var r in AllTableRows())
                {
                    var rule = r;
                    if (!IsRuleAuraRow(rule)) continue;
                    AddRow($"场地光环·{rule.DisplayName}",
                        $"{rule.Description}——{RuleAuraScopeOf(rule)}，同名在场时不可再打出（异名可共存），载体离场/被无效即失效",
                        ColorFilter.OfTagsCsv(rule.Tags),
                        () => AddRuleAura(rule));
                }
                // 属性降低（2026-10-30 挪入场地光环；2026-11-02 属性±行删除——源行改攻/血单轴两档）：
                // 源行=攻击力减少/生命值减少（AddPowerDown/AddLifeDown）行光环化；
                // 条目恒仅对方、双方各一份可叠加（不入仪典唯一性——LinkAura 多源累加）
                foreach (var mst in new[] { AtomicEffectType.AddPowerDown, AtomicEffectType.AddLifeDown })
                {
                    var mrow = AllTableRows().FirstOrDefault(x =>
                        Enum.TryParse<AtomicEffectType>(x?.EnumName ?? "", out var t) && t == mst);
                    if (mrow == null) continue;
                    var st2 = mst;
                    var mcolor = ColorFilter.OfAtomic(st2);
                    string mzh = st2 == AtomicEffectType.AddPowerDown ? "攻击力" : "生命值";
                    string msid = st2 == AtomicEffectType.AddPowerDown ? "Power" : "Life";
                    AddRow($"属性降低·{mzh}−1",
                        $"{mzh}−1——受光环影响的对方生物持续降低（数值可在条目卡调大）；双方各挂一份可叠加，载体离场/被无效即失效",
                        mcolor, () => AddAura(new LinkAuraData { stat = msid, value = -1, scope = 3 }));
                }
                return;
            }

            // ---- 连接光环面（2026-10-30 只留增益）：属性 + 关键词条目（仪典/属性降低不入——切「场地光环」配） ----
            AddTableHint(_atomItems, _libraryList, "当前：增益条目——点击换选（新选即替换上一个；只作用于连接箭头指向的单位，箭头在光环槽底部必选）");

            // 属性光环（2026-10-07 深夜改源；2026-10-08 库行细分定案；2026-10-30 只留 +1——
            // 属性减少挪场地光环；2026-11-02 属性±行删除——增益面=攻/血单轴增行各一条，不再拆三档）：
            // 攻击力增加/生命值增加（原子表位 3 行）的光环化；计价取源行走通用光环化公式。
            foreach (var r in AllTableRows())
            {
                var cfg = r;
                if (IsRuleAuraRow(cfg) || !ComposerCatalog.HasMountBit(cfg, MountKind.LinkAura)) continue;
                if (!Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var st)) continue;
                string statZh, statId; UIColor color;
                switch (st)
                {
                    // 色取表行派生单源（2026-10-29 P4 色序清尾：白/黑 UIColor 成员已删，±1 行表色=灰）
                    case AtomicEffectType.AddPowerUp: statZh = "攻击力"; statId = "Power"; color = ColorFilter.OfAtomic(st); break;
                    case AtomicEffectType.AddLifeUp: statZh = "生命值"; statId = "Life"; color = ColorFilter.OfAtomic(st); break;
                    default: continue; // 位 3 非增益属性行不进光环面；属性减少档在「场地光环」面配
                }
                const int sign = 1;
                AddRow($"属性光环·{statZh}+{sign}",
                    $"{statZh}+{sign}——源行光环化计价；只作用于箭头指向的生物（无作用面档/全体档）",
                    color, () => AddAura(new LinkAuraData { stat = statId, value = sign }));
            }
            foreach (var c in ComposerCatalog.AuraKeywordChoices())
            {
                var captured = c;
                AddRow($"关键词光环·{captured.label}",
                    captured.desc,
                    ColorFilter.OfKeyword(captured.id),
                    () => AddAura(new LinkAuraData { keyword = captured.id }));
            }

            // ---- 效果光环（2026-11-05）：引用效果库组合效果——授予资格单源
            // EffectEligibility.AuraGrantRejectReason（非主动∧多次档∧生物臂∧非可选∧无代价∧
            // 非引擎主干）；箭头指向的单位在覆盖期间视为持有该触发效果（live-query：断链/载体
            // 离场即失效；受益者失效/沉睡照发、仅绝缘体不收；元素费不逐次收——锚价随载体卡价）。
            // 2026-11-06 三修：①库源=主∪教学（此前只读主表=空壳恒「暂无」；训练表不入——不随包
            // 下发，玩家设备引用悬空）；②去重=已挂到本光环（已加连接箭头）的效果不再显示，
            // 删条目随刷新回归（同 RestrictedUsedRefIds 交互）；③挂上即刷库（隐藏即时生效）+
            // 色筛接入。条目制维持单槽整组替换（2026-11-06 定案：始终只能添加一个效果）。 ----
            var grantableFx = EffectLibrarySerializer.LoadAll()               // 主表
                .Concat(EffectLibrarySerializer.LoadAll(tutorial: true))      // 教学副表（重复 id 保主表）
                .GroupBy(g => g.id).Select(grp => grp.First())
                .Where(g => g?.header != null && EffectEligibility.AuraGrantRejectReason(g.header) == null)
                .ToList();
            var usedEffectRefs = new HashSet<string>(
                (_graph.header.LinkAuras ?? new List<LinkAuraData>())
                .Where(a => a != null && !string.IsNullOrEmpty(a.effectRef))
                .Select(a => a.effectRef));
            grantableFx.RemoveAll(g => usedEffectRefs.Contains(g.id));       // 已挂效果不显示
            if (grantableFx.Count == 0)
                AddTableHint(_atomItems, _libraryList,
                    "效果光环：效果库暂无可授予效果（要求多次触发时机·非主动·非可选·无代价·生物可挂口径——先在效果表组合）");
            foreach (var g in grantableFx)
            {
                var captured = g;
                var parts = EffectAtomCfgs(captured);
                if (_colorFilter != UIColor.All && !parts.Any(c => AtomMatchesColor(c, _colorFilter))) continue;
                var timing = (TriggerTiming)captured.header.TriggerTiming;
                string timingZh = AtomText.TimingZh.TryGetValue(timing, out var tz) ? tz : timing.ToString();
                AddRow($"效果光环·{captured.name}",
                    $"授予「{captured.name}」（{timingZh}触发）——箭头指向的单位视为持有该触发效果；断链/载体离场即失效，"
                    + "受益者失效/沉睡照发（仅绝缘体不收）；费用=效果锚价×箭头数，触发不另收元素费",
                    ColorFilter.OfTagsCsv(parts.FirstOrDefault()?.Tags),
                    () =>
                    {
                        AddAura(new LinkAuraData { effectRef = captured.id });
                        RefreshLibrary(keepScroll: true); // 已挂即隐——挂上刷库（属性/关键词仍整组替换不刷；保位）
                    });
            }
        }

        /// <summary>点击库行 = 替换左侧选中槽（2026-10-23 五分类路由）；选中槽不合适该原子时 toast 提示。
        /// 错边原子（单侧锁行）不入效果区——2026-11-03 双侧域定案后单侧行也不可作代价，直接拦。
        /// 光环双形态/门槛选中态不走本口（条目/仪典/门槛行各有专属 OnClick）。</summary>
        private void QuickAdd(LibPayload payload)
        {
            // 表级错边原子（域锁错侧——如 弃牌/送墓 锁己方域）：效果区禁错边
            if (payload.IsWrongSideOnly)
            {
                ShowToast("错边原子（单侧锁行）不可入效果区——单侧行也不可作代价（2026-11-03 双侧域定案），请换双侧行");
                return;
            }

            switch (_mode)
            {
                case ComposeMode.Parallel:
                    if (!ParallelCanDrop(payload)) { ShowToast("该原子不可作并列主干（引擎行请切自由分支/场地光环不入效果栏）"); break; }
                    if (payload.IsKeywordAtom) ApplyKeywordHeader();
                        ReplaceParallel(InferredTrunkEntry(payload));
                        RefreshLibrary(keepScroll: true); // 全局唯一（隐藏口径）：落位原子从库中隐藏——库随落位刷新（保位）
                    break;
                case ComposeMode.FreeBranch:
                    if (_selSlot == SelKind.Reward)
                    {
                        var fb = BranchOfSlot(0);
                        if (fb == null) { ShowToast("主干未定——先到右侧选引擎主干行"); break; }
                        if (IsCurseEngine((BranchEngineKind)fb.engine))
                        {
                            ShowToast("附加诅咒/祝福的载荷=效果组合引用（主动·极性匹配）——点主干卡「载荷效果」行选择，不再填原子奖励");
                            break;
                        }
                        // 引擎奖励分支（2026-11-08 阶段4）：then 原子通道退役——库已不供原子，
                        // 本闸拦旁路/旧选中态残留（防御，与 CurrentSlotPredicate 同口径）
                        if ((BranchSettleKind)fb.settle == BranchSettleKind.Engine)
                        {
                            ShowToast("引擎分支奖励=效果引用——点奖励区「奖励效果」行选择，不再填原子奖励");
                            break;
                        }
                        if (!RewardCanDrop(payload, fb)) { ShowToast("选中块=奖励——挂载位不合或超出预算（≤5 费）"); break; }
                        if (payload.IsKeywordAtom) ApplyKeywordHeader();
                        DropBranchReward(payload, 0);
                    }
                    else
                    {
                        if (!payload.IsEngineTrunk) { ShowToast("自由分支主干=引擎行（倒计时/拼点/运势…）——其余原子请切并列/有限分支"); break; }
                        ReplaceBranchTrunk(InferredTrunkEntry(payload), keepBranch: false); // 引擎行自动挂 Engine 载荷
                    }
                    RefreshLibrary(keepScroll: true);
                    break;
                case ComposeMode.LimitedBranch:
                    if (_selSlot == SelKind.Gate) break; // 门槛列表分流——行点击走 ApplySituationGate
                    if (_selSlot == SelKind.Reward)
                    {
                        var lb = BranchOfSlot(0);
                        if (lb == null) { ShowToast("先选门槛——奖励挂在门槛下（点门槛卡到右栏选择）"); break; }
                        if (!RewardCanDrop(payload, lb))
                        {
                            ShowToast(IsCostGate(lb)
                                ? "代价槽只收一般效果行·双侧域·与效果同色·有计价色的原子（减免 ≤3）"
                                : "选中块=奖励——挂载位不合或超出门槛预算");
                            break;
                        }
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
                    RefreshLibrary(keepScroll: true);
                    break;
            }
        }

        /// <summary>库行 → 效果槽条目：关键词/指示物类统一为赋予形态
        ///（kinds/domain 均不写=行默认域按极性定侧；关键词另带 str=运行时关键词 id），
        /// 其余原样；跳过三阶段行（2026-10-29 阶段3a）
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
            // 关键词/指示物（2026-11-07 关键词特判·2026-11-08 指示物对齐同口径）：entry.domain/
            // entry.kinds 均不写——落槽=行默认域按极性定侧（关键词=己方战场:生物；指示物 p≠0
            // 收窄单侧）；x个/全域由 atom.count 承载（0=全域）；域换选经 dd-domain 写 atom.domain。
            // 指示物 {1..8}「任意卡」旧存储态随之退役——显式 kinds 令渲染/产轴走 NarrowByKinds
            // 停在「双方」，与极性定侧后的域下拉占位错位（lbl-effect-name 不随单侧选择，2026-11-08 修复）。
            // 印刷通道（卡牌组合界面直加的本体关键词）不走本入口，免疫驱散。
            entry.kinds = null;
            entry.domain = null;
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

        /// <summary>关键词授予惯例（2026-11-07 特判：赋予己方战场生物·Temp 1 回合）：仅保留持续时间/非随机；数量由逐原子 dd-num 承载（x个/全域）。</summary>
        private void ApplyKeywordHeader()
        {
            _graph.header.SelectionMode = (int)CardCore.SelectionMode.Single;
            _graph.header.Duration = (int)DurationType.UntilEndOfTurn;
            _graph.header.RandomTarget = 0; // 赋予目标由弹窗选一——不做随机
        }

    }
}
