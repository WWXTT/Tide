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
        // ======================================== 构建 ========================================

        protected override string PrefabName => "EffectUI";
        protected override string RootName => "effect-composer";

        protected override void Build()
        {
            // ---- 顶栏（2026-10-24 三行重排：行1=返回+标题；行2=效果名；行3=费用预览——
            // 保存挪左栏组合侧、删除挪右栏切换钮旁，均校验/载入态驱动显隐） ----
            UiKit.Described(BindButton("btn-back", () => Manager.Back()),
                "返回：回到上一界面，这里正在编辑的内容不会保存。");
            _nameLabel = FindText("lbl-effect-name");
            // 费用显示=文本 lbl-effect-cost（2026-10-05 预制体改版：CostSquares 方格退役）——
            // 参数有效时展示完整费用计算（RefreshName 驱动）；无效组合不显示内容
            _costLabel = Find("lbl-effect-cost")?.GetComponent<TMP_Text>();
            if (_costLabel != null)
                UiKit.Described(_costLabel,
                    "费用预览：合计费用＋黑白获得；悬浮此处看完整费用构成（逐原子公式链）。"
                    + "分支奖励免费（不计合计）；代价是费用抵扣（不是奖励——全价≤3、同色逐行抵扣，"
                    + "预览显示「全费−代价=净费」）；奖励超出可实现费用时按槽区红框提示调整后才能保存。");
            // 费用徽标（2026-10-26 改版）：prefab 侧=公用 CostSquares 预制体实例（根挂 CostSquaresView）——
            // 逐色块展示合计费用（与右栏行/卡面费用方格同构），0/无效组合隐藏
            _costBadgeNode = Find("cost-badge") as RectTransform;
            _costBadgeView = _costBadgeNode != null ? _costBadgeNode.GetComponentInChildren<CostSquaresView>(true) : null;
            if (_costBadgeView == null)
                Debug.LogError("[EffectComposer] 顶栏缺 cost-badge 费用徽标（CostSquares 实例）——检查 EffectUI.prefab");
            // 发动参数（2026-10-09 prefab 重排）：activate-bar 静态组（way/timing/take-effect，
            // 每组=标签+子节点 dropdown；speed 组 2026-10-15 随发动速度退役删除）；原 root 级
            // dropdown-activation/dropdown-timing/timing-row 与设置盒 speed/limit 随设置盒拆除退役
            _activateBar = Find("activate-bar");
            if (_activateBar == null)
                Debug.LogError("[EffectComposer] 缺 activate-bar（发动参数条）——检查 EffectUI.prefab");
            _wayGroup = UiKit.FindDeep(_activateBar, "way") as RectTransform;
            _activationDropdown = BindActivateBarDropdown("way",
                ActivationChoices.Select(c => c.label).ToList(), 0);
            _timingDropdown = BindActivateBarDropdown("timing", new List<string> { "—" }, 0);
            _timingRow = UiKit.FindDeep(_activateBar, "timing") as RectTransform;
            // 作用次数选项已整删（2026-10-29 软删 → 2026-10-30 定案无次数上限）：
            // take-effect 组整组隐藏（prefab 节点不动），不再由玩家声明、运行时也不再有闸门。
            _limitGroup = UiKit.FindDeep(_activateBar, "take-effect") as RectTransform;
            if (_limitGroup != null) _limitGroup.gameObject.SetActive(false);
            // P5 代价行已整删（2026-10-30）：activate-bar 不再代码建 cost-row
            // 表切换按钮（原 btn-reload-atoms/btn-load-effects 两钮合并）：按下在 原子库↔效果表 间切换，
            // 标签常显"将切到哪边"。烘焙节点名 btn-switch-table（prefab 接管后删旧两钮）。
            UiKit.Described(UiKit.BindableButton("btn-switch-table", Root, "切换效果表", SwitchTable,
                UiStyle.BtnPrimary, UiStyle.White),
                "切换右栏显示：原子库 ↔ 效果表（效果表里点击一条可载入继续编辑）。");
            _tableToggleLabel = UiKit.FindDeep(Root, "btn-switch-table")?.GetComponentInChildren<TMP_Text>(true);
            _saveBtn = BindButton("btn-save", OnSave);
            _saveBtn?.gameObject.SetActive(false); // 校验全合规才出现（ValidateZones 驱动；Build 先隐藏）
            UiKit.Described(_saveBtn,
                "保存：编辑卡牌时写回这张卡的效果；从效果库进入则存回效果库。"
                + "按钮在必填内容填齐、校验通过后才会出现。");
            // 删除按钮：仅"从效果表载入的库效果"可删（卡编辑/新建态隐藏）——2026-10-24 挪到
            // 右栏「切换效果表」旁；prefab 烘焙 btn-delete 优先
            _deleteBtn = UiKit.BindableButton("btn-delete", Root, "删除", OnDeleteEffect,
                UiStyle.BtnDanger, UiStyle.White);
            _deleteBtn?.gameObject.SetActive(false);
            UiKit.Described(_deleteBtn,
                "删除：删掉效果库里的这条效果（只在编辑效果库条目时可用；编辑卡牌效果的保存不受影响）。");

            // ---- 主体 ----
            _modeBar = Find("mode-bar");
            _slotArea = FindScroll("slot-area");
            PrepareSlotTemplates(); // prefab 烘焙的 slot/settings 样板转隐藏模板（克隆用）

            // 描述条（左栏底部——悬浮/按下控件才出现；2026-10-16 DescTag 悬浮化，校验错误文本共用）
            EnsureDescStrip();

            // 右：展示区（双模式）
            _poolFilter = Find("pool-filter");
            BindPoolFilter(); // 颜色行 chip-{Color} 一次接线（单选，默认「全部」——激活态=Outline）
            _filterName = FindInput("field-filter-name");
            if (_filterName != null)
                _filterName.onValueChanged.AddListener(_ =>
                {
                    RefreshLibrary();
                    if (_rightMode == RightMode.Effects) RefreshEffectsList();
                });

            _libraryList = FindScroll("list-library"); // 2026-10-03 手改预制体重命名（原 library-list）
            _effectsList = FindScroll("list-effects");

            // 2026-10-21 回收式虚拟化：原子表/效果表行池复用（筛选/搜索只重绑可见行——VLG 参数由 Attach 吸收）
            _atomRows = UiKit.RecycledList.Attach(_libraryList)
                ?.Configure(i => _atomItems[i].IsHint ? 1 : 0, AtomRowFactory, BindAtomRow);
            _effectRows = UiKit.RecycledList.Attach(_effectsList)
                ?.Configure(i => _effectItems[i].IsHint ? 1 : 0, EffectRowFactory, BindEffectRow);

            // 描述条路由（红框闪烁已迁 shader——GuideFrame 自驱，无 C# 帧泵）
            UiKit.DescRequested += OnDescRequested;
        }

        /// <summary>描述条单名绑定（2026-10-07 兜底退役）：desc-strip 下 lbl-prop-desc；
        /// 缺节点 LogError 留空——不再代码补建。</summary>
        private void EnsureDescStrip()
        {
            _descStripNode = Find("desc-strip");
            var lbl = UiKit.FindDeep(_descStripNode, "lbl-prop-desc");
            _descStrip = lbl?.GetComponent<TMP_Text>();
            if (_descStrip == null)
                Debug.LogError("[EffectComposer] desc-strip 缺 lbl-prop-desc 文本——检查 EffectUI.prefab");
        }

        private RectTransform RootParent() => Parent;

        public override void OnEnter()
        {
            try
            {
                OnEnterInternal();
            }
            catch (Exception e)
            {
                // OnEnter 中途异常会让槽位/原子库静默空白——打日志+toast 可见化
                UnityEngine.Debug.LogException(e);
            }
        }

        public override void OnExit()
        {
            UiKit.DescRequested -= OnDescRequested;
            Tips.Close(); // 在途提示弹窗清理
        }

        /// <summary>描述条统一写口（空串=隐藏）：控件功能描述（DescRequested）与校验错误文本共用。</summary>
        private void SetDescStrip(string text)
        {
            if (_descStrip == null) return;
            _descStrip.text = text ?? "";
            _descStripNode.gameObject.SetActive(!string.IsNullOrEmpty(_descStrip.text));
        }

        private void OnDescRequested(string desc) => SetDescStrip(desc);

        private void OnEnterInternal()
        {
            _costGateOriginalJson = null; // 代价门快照复位（每次进屏重算——SynthesizeCostGateView 写入）
            // 变形/召唤模板解析器：组合根注入（CardCore 不依赖 UI 层——同 BattleController 口径）。
            // 2026-10-12 补：此前仅战斗/联机/无头接线，合成器费用预览里 SummonToken 模板解析恒
            // null → 召唤计价显示 0；进本屏即接上（幂等——静态委托重复赋同值无副作用）。
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;

            // 卡编辑会话（一次性消费）：进入即改写 _graph 为该效果的深拷贝
            var (card, index) = ComposerSession.Take();
            if (card != null)
            {
                _editingCard = card;
                _editingIndex = index;
                _graph = CardEffectToGraph(index >= 0 && index < card.Effects?.Count ? card.Effects[index] : null,
                    out var note);
                _loadedFromLibraryId = null;
                _presetSourceId = null;
                _presetSnapshotHash = null;
                _returnToCardComposer = false; // 卡编辑保存恒 Back（5204 口径）——回跳标记不复用
                string msg = index >= 0 ? $"编辑效果 #{index + 1}（保存写回卡牌）" : "新建效果（保存追加到卡牌）";
                if (note != null) msg += $"——载入归一化：{note}"; // 两槽定案：遗留门步骤折叠/主干截断可见化
                ShowToast(msg);
                SynthesizeCostGateView(); // 代价门视图合成（2026-11-08 效果级化）：本效果 header.Costs →「支付代价」门分支
            }
            else
            {
                // 效果库模式：进屏=新组合（2026-10-09 修复）。此前不复位——上次会话的组合与
                // _loadedFromLibraryId 跨访问残留，原位改组另一效果再保存会把上一条库效果当
                // 「旧档」删除（两条独立效果互相覆盖的根因之一）。卡编辑态返回未保存同样在此清。
                _editingCard = null;
                _editingIndex = -1;
                _graph = CardEffectToGraph(null, out _);
                _loadedFromLibraryId = null;
                _presetSourceId = null;
                _presetSnapshotHash = null;
                _returnToCardComposer = false; // 上次会话回跳标记不跨访问残留
                _expandedAtoms.Clear();
                _expandedRewards.Clear();
            }

            // 库效果编辑会话（2026-11-06 通道补全——此前过渡态只跳转不载入）：卡组合界面右栏「编辑」
            // 按 id 载入编辑图。主表命中=常规覆盖语义（LoadEffectFromLibrary）；仅预设表命中=预设
            // 只读态（LoadPresetEffect：修改保存另存新效果·同内容拦存·源档不可覆盖不可删）；
            // 两处皆无=提示后停留新组合。ReturnToCardComposer=库模式保存成功后回跳。
            var libEditId = ComposerSession.EditingLibraryEffectId;
            bool returnToComposer = ComposerSession.ReturnToCardComposer;
            ComposerSession.EditingLibraryEffectId = null;
            ComposerSession.ReturnToCardComposer = false;
            bool presetLoaded = false;
            if (!string.IsNullOrEmpty(libEditId))
            {
                _returnToCardComposer = returnToComposer;
                var mainHit = EffectLibrarySerializer.LoadAll().FirstOrDefault(g => g != null && g.id == libEditId);
                if (mainHit != null)
                    LoadEffectFromLibrary(mainHit);
                else
                {
                    var presetHit = EffectLibrarySerializer.LoadAll(true).FirstOrDefault(g => g != null && g.id == libEditId);
                    if (presetHit != null) { LoadPresetEffect(presetHit); presetLoaded = true; }
                    else ShowToast($"库效果不存在：{libEditId}（可能已被删除——留在效果表重选）");
                }
                UnityEngine.Debug.Log($"[EffectComposer] 库效果编辑载入：{libEditId}"
                    + (presetLoaded ? "（预设只读态）" : "") + (returnToComposer ? "·保存后回卡组合" : ""));
            }

            BuildTimingDropdown();
            BuildActivationDropdown();
            _mode = InferMode(_graph);
            EnsureDefaultSelection();
            RefreshAll();
            SetRightMode(RightMode.Atoms);
            ValidateZones(); // 初始校验（载入数据可能自带违规——如超预算奖励）
            if (presetLoaded)
                _presetSnapshotHash = PresetContentHash(_graph); // 进屏链（时机下拉重建等）全部落定后复拍——防「未改也判改」

            // 空库自诊断：正常应 ≥91 行（87+改写仪典 4 行）——为空说明表未装载或初始化半途出错（查 Console）。
            // 2026-10-21 虚拟化后 content.childCount≠条数——读回收列表条数
            int libCount = _atomRows?.Count ?? 0;
            if (libCount == 0)
                ShowToast("原子库为空（表加载失败或初始化异常——查 Console）");
        }

        // ======================================== 元信息（顶栏） ========================================

        private void BuildTimingDropdown()
        {
            if (_timingDropdown == null) return; // 预制体缺节点——BindDropdown 已报，时机编辑不可用
            // 全中文时机下拉（主动三档 Activate_* 不入——主动不设时机；系统自用/不面向玩家的时点不入
            // ——OnAtomicEffect* 三段 / OnSummon 召唤进场 / OnPhase* 阶段起止 / OnGameStart 游戏开始 /
            // OnOtherCreatureDeath / OnRoleDeath，2026-10-09 定案）。
            //
            // **2026-11-14 档位制**：自动效果按一回合内触发次数分三档，档位单源=TimingWeights.TierOf
            //（表 Bucket）：
            // · 一回合一次（PerTurn）：攻击/守卫/回合开始/准备阶段/回合结束/登场/进场/游戏开始/被攻击/
            //   横置/重置——默认每回合至多触发一次，时机系数恒 1；生物/结界可挂（攻/守/横置/重置=生物专属）。
            // · 稀有（Rare）：被破坏/被除外/死亡/离场/墓地回场/角色死亡/占卜——比一回合一次更难触发，
            //   按概率给系数（表 Factor）；生物/结界可挂。
            // · 一回合多次（Multi）：抽牌/伤害/使用卡牌/结算观察/阶段起止/其他生物进出场/施法——
            //   只能挂结界，每次触发扣 1 点耐久；生物/法术拒。
            // 宿主过滤=EffectEligibility.HostCanTakeTiming 单源；效果库模式无宿主全开。
            bool hostFilters = _editingCard != null;
            _timings = TimingZh.Keys
                .Where(t => !hostFilters || EffectEligibility.HostCanTakeTiming(_editingCard.Supertype, t))
                .OrderBy(t => (int)TimingWeights.TierOf(t)) // 一回合一次→稀有→一回合多次（LINQ 稳定排序——组内维持表序）
                .ToList();
            var labels = _timings
                .Select(t => $"{TimingZh[t]} · {TimingTierZh(t)}")
                .ToList();
            _timingDropdown.SetOptions(labels, 0);
            SyncTimingDropdown();
            _timingDropdown.Changed += (idx, _) =>
            {
                if (idx >= 0 && idx < _timings.Count) _graph.header.TriggerTiming = (int)_timings[idx];
                RefreshName(); // 时机换档改计价——顶栏合计实时重推导
            };
            float perTurnF = CostDerivationService.TimingCostFactorOf(TriggerTiming.OnAttack);
            float rareF = CostDerivationService.TimingCostFactorOf(TriggerTiming.OnDestroy);
            float multiF = CostDerivationService.TimingCostFactorOf(TriggerTiming.OnDraw);
            _timingDropdown.Describe(
                $"触发时机（一回合一次 ×{perTurnF:0.###} / 稀有 如被破坏 ×{rareF:0.###} / 一回合多次 ×{multiF:0.###}）："
                + "一回合一次=攻击/守卫/回合开始/回合结束/准备阶段/登场/进场/被攻击——默认每回合只发生一次，系数恒 1"
                + "（攻/守/横置/重置只有生物能挂）；"
                + "稀有=被破坏/被除外/死亡/离场/墓地回场/角色死亡/占卜——更难以触发，沿用按概率得到的系数"
                + "（如 被破坏 0.55、被除外 0.12）；"
                + "一回合多次=抽牌/伤害/使用卡牌/结算观察/阶段起止/其他生物进出场等——只能挂在结界上，"
                + "每次触发扣 1 点耐久，耐久归零销毁。"
                + "系数单源=Configs/TimingWeights.json（Bucket 档位 + Factor 系数），改表即同步这里与计价。"
                + "其余效果无次数限制，仅同一效果每回合超过 30 次按自循环截断。点选项看各时机说明。",
                idx => idx >= 0 && idx < _timings.Count ? TimingDescZh(_timings[idx]) : null);
        }

        /// <summary>时机档位标签（2026-11-14 档位制）：一回合一次（生物动作族/双宿主分别标注）/
        /// 稀有 / 一回合多次（结界专属）。</summary>
        private static string TimingTierZh(TriggerTiming t)
        {
            var tier = TimingWeights.TierOf(t);
            if (tier == TimingTier.Multi) return "一回合多次·结界专属";
            if (tier == TimingTier.Rare) return "稀有";
            if (tier == TimingTier.Active) return "主动";
            if (EffectEligibility.IsTapAnchoredTiming(t)) return "一回合一次·生物专属";
            if (EffectEligibility.IsDualHostTiming(t)) return "一回合一次·双宿主";
            return "一回合一次";
        }

        private void SyncTimingDropdown()
        {
            if (_timingDropdown == null) return;
            int current = _timings.IndexOf((TriggerTiming)_graph.header.TriggerTiming);
            // 不在下拉的时机（主动三档/系统自用时点：OnAtomicEffect* 三段、OnSummon、OnPhase*、OnGameStart）：
            // 占位显示首项、不写回（SetIndex 默认不通知——header 真值保留，改选才落值）
            _timingDropdown.SetIndex(current < 0 ? 0 : current);
        }

        private void BuildActivationDropdown()
        {
            _activationDropdown?.SetOptions(ActivationChoices.Select(c => c.label).ToList(), ActivationIndex(_graph.header.ActivationType));
            SyncActivationVisibility();
            _activationDropdown?.Describe(
                "发动方式：自动看条件弹窗询问，主动由你手动发动——点选项看各自说明。",
                idx => idx == 0
                    ? "自动：条件达成时游戏弹窗问你发不发动；什么时候问由「触发时机」决定，不比拼速度。"
                    : "主动：由你手动发动——发动时才支付费用并横置这张卡。");
            if (_activationDropdown != null)
                _activationDropdown.Changed += (idx, _) =>
                {
                    if (idx >= 0 && idx < ActivationChoices.Length)
                    {
                        // 宿主形状闸（2026-10-15 深夜改版）：法术只挂主动、结界禁主动——非法选择弹回不改值
                        var pick = ActivationChoices[idx];
                        // 主干发动方式钉（2026-11-08 规则地基）：四类主动行仅主动、其余引擎行仅自动——非法选择弹回
                        var pin = EngineActivationPin();
                        if (pin != null && pick.value != (int)pin.Value)
                        {
                            ShowToast(pin.Value == EffectActivationType.Voluntary
                                ? "该主干仅主动（附加诅咒/附加祝福=主动效果）"
                                : "该引擎仅自动（评估口内生于引擎事件）");
                            SyncActivationVisibility(); // 弹回当前合法值
                            return;
                        }
                        if (HostForbidsVoluntary && pick.value == (int)EffectActivationType.Voluntary)
                        {
                            ShowToast("结界只挂自动/光环——主动效果已随 2026-10-15 改版退役");
                            SyncActivationVisibility(); // 弹回当前合法值
                            return;
                        }
                        if (HostRequiresVoluntary && pick.value != (int)EffectActivationType.Voluntary)
                        {
                            ShowToast("法术只挂主动效果（施放时弹窗抉择其一）");
                            SyncActivationVisibility();
                            return;
                        }
                        _graph.header.ActivationType = pick.value;
                    }
                    SyncActivationVisibility();
                };
        }

        /// <summary>宿主形状谓词（2026-10-15 深夜三类卡改版）：结界禁主动（只挂自动/光环）、
        /// 法术只挂主动（1~3 个，施放时弹窗抉择其一）。效果库模式（无宿主）两态全开。</summary>
        private bool HostForbidsVoluntary
            => _editingCard != null && _editingCard.Supertype == Cardtype.Enchantment;

        private bool HostRequiresVoluntary
            => _editingCard != null && _editingCard.Supertype == Cardtype.Spell;

        /// <summary>宿主禁挂连接光环（改版配套）：生物（连接光环专属结界）与法术（只挂主动效果）。</summary>
        private bool HostBlocksAura
            => _editingCard != null && _editingCard.Supertype != Cardtype.Enchantment;

        /// <summary>发动方式/时机的可见性与档位锁定（2026-10-05 两槽定案）：
        /// 并列=主动/自动两态全开（主动档时机钉 Activate_*、离开回落 OnPlay——2026-10-02 定案不变；
        /// 分支钉强制档已删——分支时机由槽级 branch 载荷承载，不再锁效果头）；
        /// 光环=无时机/方式（载体入场即生效，离场/被无效即失效）——时机钉 OnPlay、隐藏两下拉。</summary>
        private void SyncActivationVisibility()
        {
            if (_timingRow == null && _activationDropdown == null) return;
            bool aura = IsAuraMode(_mode);
            var h = _graph.header;

            // 宿主形状归一（2026-10-15 深夜改版——载入旧数据/切换宿主时兜底）：
            // 结界禁主动（钉回自动）；法术只挂主动（钉 Voluntary+Activate_*）。
            if (!aura)
            {
                if (HostForbidsVoluntary && h.ActivationType == (int)EffectActivationType.Voluntary)
                    h.ActivationType = (int)EffectActivationType.Automatic;
                if (HostRequiresVoluntary && h.ActivationType != (int)EffectActivationType.Voluntary)
                    h.ActivationType = (int)EffectActivationType.Voluntary;
            }

            if (aura)
            {
                h.ActivationType = 0; // 强制档（光环=入场生效无需发动方式）
                _activationDropdown?.SetIndex(0);
                h.TriggerTiming = (int)TriggerTiming.OnPlay;
            }
            else
            {
                bool voluntary = h.ActivationType == 2;
                int t = h.TriggerTiming;
                if (voluntary)
                {
                    if (t != (int)TriggerTiming.Activate_Active
                        && t != (int)TriggerTiming.Activate_Instant
                        && t != (int)TriggerTiming.Activate_Response)
                        h.TriggerTiming = (int)TriggerTiming.Activate_Active;
                }
                else if (t == (int)TriggerTiming.Activate_Active
                         || t == (int)TriggerTiming.Activate_Instant
                         || t == (int)TriggerTiming.Activate_Response)
                {
                    h.TriggerTiming = (int)TriggerTiming.OnPlay;
                }
            }

            _wayGroup?.gameObject.SetActive(!aura); // 方式组整体（标签+下拉）——光环无发动方式
            _timingRow?.gameObject.SetActive(!aura && h.ActivationType != 2);

            SyncSettingsRowVisibility(); // 发动行显隐联动（设置盒随最新绑定卡对齐）
        }

        /// <summary>发动行显隐（2026-10-05 定案；2026-10-09 activate-bar 迁移；速度组 2026-10-15 退役；
        /// **生效次数组 2026-10-30 定案整删**——take-effect 常驻隐藏、不再写盘；
        /// P5 代价行 2026-10-30 整删——本口暂无逐行显隐项，保留联动锚点）。</summary>
        private void SyncSettingsRowVisibility()
        {
        }

        /// <summary>activate-bar 组内下拉绑定（2026-10-09 prefab 重排：组名 → 子节点 dropdown）。</summary>
        private UiKit.Dropdown BindActivateBarDropdown(string group, List<string> options, int index)
        {
            var node = UiKit.FindDeep(_activateBar, group);
            var tmp = node != null ? node.GetComponentInChildren<TMP_Dropdown>(true) : null;
            if (tmp == null)
            {
                Debug.LogError($"[EffectComposer] activate-bar/{group} 缺 dropdown——检查 EffectUI.prefab");
                return null;
            }
            return new UiKit.Dropdown(tmp, options, index, null);
        }

        // 生效次数下拉已整删（2026-10-29 P9 软删；2026-10-30 定案：效果无每回合生效次数上限——
        // 运行时无闸门、计价无次数档，唯一保留的每回合 1 次是自由分支引擎奖励〔BranchEngines 自限〕）。

    }
}
