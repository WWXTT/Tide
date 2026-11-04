using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using CardCore;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// Region 面板目标选择器（2026-10-15 用户定案，取代本地局 UiTargetSelector 列表弹窗）：
    /// BattleUI.prefab 的 Region 节点（ScrollRect 横滑）= 选择面——viewport/content/ACard 模板
    /// 逐候选实例化卡面（实例隐藏 cost 费用行与 btn 层；模板已无卡费方格）、label-num 写
    /// 「{选择提示全文}（x/y）」（2026-10-17 lbl-toast 退役并入：原 toast 面板挪入 Region 更名
    /// label-num——Image 底板+子节点 label(TMP)，开窗写选择目的/响应说明全文+计数同面板；
    /// 提示空回落标题——标题即"在为什么选目标"，如回合末弃牌的「回合结束，手牌超出7张，
    /// 请选择丢弃」，再空回落「目标数量」）、btn-select（确定）在 x 满足 min..max
    /// 后显示，点击即确认收口。
    /// 实现 ITargetSelectorEx：SelectAsync 拿 Candidates 实体渲染 ACard 全卡面（FromLiveCard），
    /// 非卡实体/纯选项回落最小卡面（仅卡名）；超时自持（Ex 契约）——到点取当前有效选择，
    /// 不足 min 则 AutoPick 前 min 个。与 Region 浏览态（墓/除外/发动区列卡）互斥：
    /// 打开时压掉浏览三件（title/lines/cards）并复位 Region 活跃态；IsOpen 供
    /// BattleScreen.RefreshRegion/OpenRegion 闭流防串台。
    /// 红瞄准面片（2026-10-16 晚定案·选中才上盘）：开窗不铺候选面片，选中/取消选中在候选
    /// 地块 Show/Hide 红瞄准（与 sel-frame 同拍·全尺寸单档）。单位/施放立牌经注入的
    /// boardCellOf（BattleScreen→BattleFieldView.TryGetOnBoardCell）定位；角色候选经
    /// roleCellOf（BattleScreen→BoardState.CharacterCell）定位角色格——选中角色地块同样出面片。
    /// 关窗单口清（ClearAll）。
    /// 候选扩散波（2026-11-01 用户定案）：开窗期对候选地块逐帧补点亮生物可行动同款扩散波
    ///（SetUnitGlow 己方蓝口径——对账 ClearAll 会清波故逐帧回补，同 BattleCellPicker 先例）；
    /// 关窗随面片一并逐格 ClearUnitGlow。候选提示=波、选中标记=红面片，两层各管各。
    /// 角色候选（2026-10-16）：Player 实体→卡名【己方/对方角色】的 Hero 型占位卡（注入
    /// isLocalPlayer 判侧；ACardView 对 Hero 藏 type 区=不带生物立绘槽）。
    /// </summary>
    public sealed class RegionTargetSelector : ITargetSelectorEx
    {
        /// <summary>选择进行中（BattleScreen 浏览面板闭流判据）。2026-10-17 去静态定案：
        /// 改实例态随屏生灭——旧局退出时在途 Run 孤悬，静态残留会把新局点击路由闭死
        /// （重开即卡：手牌点不动、结束回合静默被拒——两次实测复现）。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>本次开窗的墙钟（realtimeSinceStartup；0=关）。帧驱动看门狗判超时用
        ///（不依赖 UniTask.Delay——在途 await 链孤悬时面板自身的 60s 超时同样不会触发）。</summary>
        public float OpenSinceRealtime { get; private set; }

        /// <summary>牌库预览条进行中（2026-10-26 诅咒/祝福显示层）：选择态（IsOpen/_busy）之外
        /// 的只读探针——真实选择（Run）或外部 ClosePreview 收起。</summary>
        public bool PreviewOpen => _previewActive;

        /// <summary>候选条（viewport）被 btn-hide 收起中（2026-11-03 需求5/6）：收起期区域点击可开
        /// Information 查阅。仅选择进行中（IsOpen）有意义——浏览态 viewport 本就藏。</summary>
        public bool ViewportHidden => IsOpen && _viewport != null && !_viewport.gameObject.activeSelf;

        private readonly RectTransform _region;
        private readonly GameObject _labelNumNode; // label-num 面板本体（TMP 在子节点 label 上——显隐须控面板）
        private readonly TMP_Text _labelNum;
        private readonly Button _btnSelect;
        private readonly RectTransform _content;
        private readonly RectTransform _template;
        private readonly RectTransform _viewport;
        private readonly Func<Card, (int x, int z)?> _boardCellOf; // 实体→场上格（null=宿主未注入，无面片）
        private readonly Func<Player, bool> _isLocalPlayer;        // 角色→己方/对方判别（null=一律按对方侧文案）
        private readonly Func<Player, (int x, int z)?> _roleCellOf; // 角色→角色格（null=宿主未注入，选中无面片）
        // 提示并入 label-num（2026-10-17 lbl-toast 退役定案）：开窗=面板写「本次选择来干嘛」
        // （效果发动的选择=完整效果文，由 BattleScreen 注入 buildPrompt 渲染；纯选项路径=hint
        // 回落 title）+「（x/y）」计数；allowCancel（主动效果可「取消不发动」）回调宿主亮
        // 根级 btn-notchain「取消」（守卫/响应窗口同键共用，显隐归 BattleScreen）
        private readonly Func<TargetSelectionRequest, string> _buildPrompt;
        private readonly Action<bool> _onCancelAvailable;
        private readonly Action _onWindowNeedsSpace; // Select 窗被占用（选择/下拉/文本/预览/btn-hide 显示）——屏侧收 Information（2026-11-03 需求4/7）
        private readonly Button _btnHide;          // Select/btn-hide：候选条临时收起口（2026-11-03 需求5——藏 viewport 非关窗）
        private readonly TMP_Text _btnHideLabel;   // btn-hide 子 label 文案（隐藏↔显示）
        private readonly TMP_InputField _inputStr; // Select/label-str：卡名宣言输入（2026-11-03 信息轴）
        private readonly TMP_Dropdown _dropdown;   // Select/Dropdown：类别/颜色/费用/维度档宣言（2026-11-03 信息轴）

        private readonly List<int> _picked = new List<int>();
        private readonly List<Image> _frames = new List<Image>();
        private (uint id, int x, int z)?[] _reticles; // 本轮候选地块登记（选中才上盘；索引对齐候选；null=无实体/纯选项路径）
        private const uint RoleAimIdBase = uint.MaxValue; // 角色候选面片伪键基（角色无 RuntimeId；高位不与卡的低位顺序号冲突）
        private int _min, _max;
        private string _title;
        private string _prompt; // 选择/响应提示全文（Run 写入·RefreshLabel 与计数合并渲染）
        private bool _busy;
        private UniTaskCompletionSource<List<int>> _activeTcs; // 在途选择收口口（Cancel 用）
        private UniTaskCompletionSource<string> _activeTextTcs; // 在途文本输入窗收口口（ForceIdle 清算用）
        private UiKit.Modal _activeTextModal;                   // 在途文本输入窗本体（同上）

        // ==== 牌库标记预览态（2026-10-26 诅咒/祝福显示层）：复用选择条的非交互显示面 ====
        private bool _previewActive;                                  // 预览进行中（与 _busy 选择态互斥）
        private bool _previewRegionWasActive;                         // 进预览前 Region 节点活跃态（关时还原）
        private List<KeyValuePair<GameObject, bool>> _previewHidden;  // 预览压掉的浏览三件（title/lines/cards 原显隐）

        /// <summary>外部收口在途选择（2026-10-16 响应窗口让过/守卫先定）：空表完成——
        /// Run 侧空表≠null 不触发超时代选，调用方按「未选」语义消费（响应窗=让过）。
        /// 未在途=无操作。</summary>
        public void Cancel() => _activeTcs?.TrySetResult(new List<int>());

        /// <summary>跨局强制收口（2026-10-17 卡死根因修复）：退出/进屏时清算在途状态——
        /// Cancel 只解 tcs，若 Run 的收尾链因屏体拆除而孤悬，busy 与静态 IsOpen 会残留：
        /// IsOpen=true 把新局点击路由闭死（一切按「选择期」拦截）、busy=true 让后续窗口
        /// 静默 AutoPick。此处不等收尾链，直接把三态钉回空闲（幂等，正常路径无操作）。</summary>
        public void ForceIdle()
        {
            ClosePreview(); // 预览态一并钉回（2026-10-26）
            _activeTcs?.TrySetResult(new List<int>());
            _activeTcs = null;
            _activeTextTcs?.TrySetResult(null); // 在途文本输入窗一并按取消收口（2026-10-31 阶段C）
            _activeTextTcs = null;
            _activeTextModal?.Close();
            _activeTextModal = null;
            if (_inputStr != null && _inputStr.gameObject.activeSelf) _inputStr.gameObject.SetActive(false); // 窗口内输入/下拉态一并收（2026-11-03）
            if (_dropdown != null && _dropdown.gameObject.activeSelf) _dropdown.gameObject.SetActive(false);
            _busy = false;
            IsOpen = false;
            OpenSinceRealtime = 0f;
        }

        public RegionTargetSelector(RectTransform region, Func<Card, (int x, int z)?> boardCellOf = null,
            Func<Player, bool> isLocalPlayer = null, Func<Player, (int x, int z)?> roleCellOf = null,
            Func<TargetSelectionRequest, string> buildPrompt = null, Action<bool> onCancelAvailable = null,
            Action onWindowNeedsSpace = null)
        {
            _region = region;
            var labelNum = region.Find("label-num") ?? UiKit.FindDeep(region, "label-num"); // 2026-11-03 prefab 改版：label-num 挪入 viewport 下——深找兼容两种摆位
            _labelNumNode = labelNum?.gameObject;
            _labelNum = labelNum?.GetComponentInChildren<TMP_Text>();
            _btnSelect = region.Find("btn-select")?.GetComponent<Button>();
            _viewport = region.Find("viewport") as RectTransform;
            _content = region.Find("viewport/content") as RectTransform;
            _template = region.Find("viewport/content/ACard") as RectTransform;
            var btnHide = region.Find("btn-hide");
            _btnHide = btnHide != null ? btnHide.GetComponent<Button>() : null;
            _btnHideLabel = btnHide != null ? btnHide.GetComponentInChildren<TMP_Text>(true) : null;
            if (_btnHide != null)
            {
                _btnHide.onClick.RemoveAllListeners();
                _btnHide.onClick.AddListener(OnBtnHideClicked);
                _btnHide.gameObject.SetActive(false); // 仅候选卡模式（Run）可见——浏览/预览/输入态由各开窗口自管
            }
            var labelStr = region.Find("label-str") ?? UiKit.FindDeep(region, "label-str");
            _inputStr = labelStr != null ? labelStr.GetComponentInChildren<TMP_InputField>(true) : null;
            var dropdownNode = region.Find("Dropdown") ?? UiKit.FindDeep(region, "Dropdown");
            _dropdown = dropdownNode != null ? dropdownNode.GetComponentInChildren<TMP_Dropdown>(true) : null;
            _boardCellOf = boardCellOf;
            _isLocalPlayer = isLocalPlayer;
            _roleCellOf = roleCellOf;
            _buildPrompt = buildPrompt;
            _onCancelAvailable = onCancelAvailable;
            _onWindowNeedsSpace = onWindowNeedsSpace;
            if (_labelNum == null || _btnSelect == null || _viewport == null || _template == null)
                Debug.LogError("[RegionTargetSelector] Region 缺节点（label-num/btn-select/viewport/content/ACard）——目标选择将退化自动选");
            if (_inputStr == null || _dropdown == null)
                Debug.LogWarning("[RegionTargetSelector] Select 缺 label-str/Dropdown——卡名输入退回弹窗、维度反问退回卡列表（prefab 2026-11-03 改版节点）");
        }

        /// <summary>btn-hide（2026-11-03 需求5/7）：收起=藏 viewport 让玩家观察场上信息（窗不关，
        /// label-num/计数随 viewport 一并藏）；再点显示=viewport 复原并让屏侧收 Information（同位互斥）。</summary>
        private void OnBtnHideClicked()
        {
            if (_viewport == null || !IsOpen) return; // 仅候选卡模式在身时有效（浏览/预览/输入态本按钮已藏）
            bool hide = _viewport.gameObject.activeSelf;
            _viewport.gameObject.SetActive(!hide);
            if (_btnHideLabel != null) _btnHideLabel.text = hide ? "显示" : "隐藏";
            if (!hide) _onWindowNeedsSpace?.Invoke(); // 切回显示：Select 需要完整窗位——Information 让位（需求7）
        }

        // ==================== ITargetSelectorEx（本地局主入口：实体候选 → ACard 卡面） ====================

        public async UniTask<List<int>> SelectAsync(TargetSelectionRequest request,
            IReadOnlyList<string> labels, float timeoutSeconds)
        {
            var items = new List<CardOverlayItem>(labels.Count);
            var reticles = new (uint id, int x, int z)?[labels.Count]; // 场上候选→面片登记（索引对齐）
            for (int i = 0; i < labels.Count; i++)
            {
                var e = request.Candidates != null && i < request.Candidates.Count ? request.Candidates[i] : null;
                if (e is Player p)
                {
                    // 角色候选（2026-10-16 定案）：非卡实体——卡名只写【己方/对方角色】，
                    // Hero 型占位卡不带生物立绘槽（ACardView 藏 type 区）。
                    // 2026-10-17 角色状态展示：stats 芯片带指示物（与 CardOverlayItem.FromHand 同滤法）
                    //——选目标时可见角色当前状态（法术护盾/耐久类层）。
                    var role = new CardOverlayItem
                    {
                        Key = "P" + i,
                        Name = _isLocalPlayer != null && _isLocalPlayer(p) ? "【己方角色】" : "【对方角色】",
                        Supertype = Cardtype.Hero,
                    };
                    foreach (var c in p.EnumerateCounters())
                    {
                        var spec = CardCore.Attribute.CounterRules.Find(c.Key);
                        if (spec != null && spec.LayerRole == CardCore.Attribute.CounterLayerRole.Reading) continue;
                        (role.CounterChips ??= new List<(string, int)>())
                            .Add((string.IsNullOrEmpty(spec?.DisplayName) ? c.Key : spec.DisplayName, c.Value));
                    }
                    items.Add(role);
                    if (_roleCellOf?.Invoke(p) is { } roleCell) // 选中角色→角色格出面片（2026-10-16 晚定案）
                        reticles[i] = (RoleAimIdBase - (uint)i, roleCell.x, roleCell.z);
                    continue;
                }
                var card = e as Card;
                var it = card != null ? CardOverlayItem.FromLiveCard(card) : null;
                // 墓地归属徽标（2026-11-02 洗回改版）：跨双方墓地的选择面按归属冠
                // 【己方墓地】/【对方墓地】前缀——卡面本身无归属信息，混合候选须可辨别
                //（【己方角色】前缀同款文案口径；非墓地卡不动）
                if (it != null) PrefixGraveyardBadge(card, it, request.Chooser);
                items.Add(it ?? new CardOverlayItem { Key = "L" + i, Name = labels[i] });
                if (card != null && _boardCellOf?.Invoke(card) is { } cell) // 在场上（单位/施放立牌）
                    reticles[i] = (card.RuntimeId, cell.x, cell.z);
            }
            // 提示并入 label-num（2026-10-17 定案）：效果发动的目标选择——buildPrompt 按 PromptSource/
            // EffectId 渲染完整效果文直写面板；取消口=AllowCancel（主动效果「取消不发动」）
            string prompt = _buildPrompt != null ? _buildPrompt(request) : request.Title;
            return await Run(items, reticles, request.MinCount, request.MaxCount, request.Title, timeoutSeconds,
                prompt, request.AllowCancel);
        }

        // ==================== 基接口（纯选项/网络兜底路径：仅卡名最小卡面） ====================

        /// <summary>墓地候选归属徽标：卡在任一侧墓地时给卡名冠【己方墓地】/【对方墓地】前缀
        ///（按 Chooser 判侧；非墓地卡不动）。牌库域折叠（CollapseDeckZoneCells）不在此径——
        /// 牌库卡在选择面前已折为角色区域格。</summary>
        private static void PrefixGraveyardBadge(Card card, CardOverlayItem it, Player chooser)
        {
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (card == null || zm == null || it == null) return;
            if (zm.IsCardInZone(card, core.Player1, Zone.Graveyard))
                it.Name = (core.Player1 == chooser ? "【己方墓地】" : "【对方墓地】") + it.Name;
            else if (zm.IsCardInZone(card, core.Player2, Zone.Graveyard))
                it.Name = (core.Player2 == chooser ? "【己方墓地】" : "【对方墓地】") + it.Name;
        }

        public async UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
            int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
        {
            // 下拉识别（2026-11-03 用户定案）：单选纯选项且全部标签带白名单前缀（类别：/颜色：/费用：/维度：
            // ——与 ProphecyHandlerUtil 维度目录单源对应）→ 不列卡，改 Select/Dropdown 下拉+确认；
            // 其余（响应窗卡名/自由文案抉择）照旧卡列表。候选卡走 SelectAsync 不经此径。
            if (min == 1 && max == 1 && _dropdown != null && _labelNum != null && IsChoiceDropdown(labels))
                return await RunChoice(labels, title, hint, timeoutSeconds);
            var items = new List<CardOverlayItem>(labels.Count);
            for (int i = 0; i < labels.Count; i++)
                items.Add(new CardOverlayItem { Key = "L" + i, Name = labels[i] });
            // 纯选项路径（响应窗/抉择）：提示=hint 优先（调用方写明「在响应什么」），回落 title——
            // 同写 label-num（2026-10-17 并入）
            string prompt = string.IsNullOrWhiteSpace(hint) ? title : hint;
            return await Run(items, null, min, max, title, timeoutSeconds, prompt, allowCancel); // 纯选项路径无实体→无面片
        }

        /// <summary>下拉识别白名单前缀（与 ProphecyHandlerUtil 维度目录单源注释对应）。</summary>
        private static readonly string[] ChoicePrefixes = { "类别：", "颜色：", "费用：", "维度：" };

        /// <summary>下拉识别：2..12 项且全部标签共同一白名单前缀（类别：/颜色：/费用：/维度：）。
        /// 响应窗标签=卡名、抉择标签=自由文案——不撞白名单；混前缀不放行（引擎两段式单档单请求，
        /// 收紧杜绝未来误撞）。</summary>
        private static bool IsChoiceDropdown(IReadOnlyList<string> labels)
        {
            if (labels == null || labels.Count < 2 || labels.Count > 12) return false;
            string common = null;
            foreach (var l in labels)
            {
                if (string.IsNullOrEmpty(l)) return false;
                string hit = null;
                foreach (var p in ChoicePrefixes)
                    if (l.StartsWith(p)) { hit = p; break; }
                if (hit == null) return false;
                if (common == null) common = hit;
                else if (common != hit) return false;
            }
            return true;
        }

        /// <summary>下拉单选（Select/Dropdown）：按引擎序 ClearOptions+重填（value=0=预设默认——
        /// 引擎侧已把预置排位 0），btn-select=确认；超时/AI 兜底取 0（与卡列表 AutoPick 对齐）。
        /// 占窗语义同 Run（_busy/IsOpen/看门狗/Cancel 口径）；无候选实例/面片/收起口。</summary>
        private async UniTask<List<int>> RunChoice(IReadOnlyList<string> labels, string title, string hint, float timeoutSeconds)
        {
            _onWindowNeedsSpace?.Invoke(); // Select 窗被下拉占用——Information 让位（需求4 同族）
            bool regionWasActive = _region.gameObject.activeSelf;
            var hidden = HideBrowseNodes();
            if (_inputStr != null && _inputStr.gameObject.activeSelf) _inputStr.gameObject.SetActive(false);
            if (_btnHide != null) _btnHide.gameObject.SetActive(false); // 下拉模式无候选条可收起

            _region.gameObject.SetActive(true);
            _labelNumNode.SetActive(true);
            _viewport.gameObject.SetActive(true); // label-num 在 viewport 下——提示可见须带 viewport
            _dropdown.gameObject.SetActive(true);
            _dropdown.ClearOptions();
            var opts = new List<string>(labels.Count);
            for (int i = 0; i < labels.Count; i++) opts.Add(labels[i]);
            _dropdown.AddOptions(opts);
            _dropdown.SetValueWithoutNotify(0);
            _dropdown.RefreshShownValue();
            string head = string.IsNullOrWhiteSpace(hint) ? title : hint;
            _labelNum.text = (string.IsNullOrWhiteSpace(head) ? "请选择" : head) + "——下拉选择后点「确认」";

            _busy = true; // 占窗互斥（Run 同款；引擎侧反问串行，重入兜底）
            IsOpen = true;
            OpenSinceRealtime = Time.realtimeSinceStartup;
            var tcs = new UniTaskCompletionSource<List<int>>();
            _activeTcs = tcs; // Cancel 外部收口挂点（空表→调用方按 0 兜底，与超时同义）
            _btnSelect.onClick.RemoveAllListeners();
            _btnSelect.onClick.AddListener(() => tcs.TrySetResult(new List<int> { _dropdown.value }));
            _btnSelect.gameObject.SetActive(true);

            float seconds = timeoutSeconds > 0f ? timeoutSeconds : 60f;
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(seconds))
                .ContinueWith(() => (List<int>)null);
            var (win, uiResult, _) = await UniTask.WhenAny(tcs.Task, timeoutTask);
            List<int> result = win == 0 ? uiResult : null;
            if (result == null) result = new List<int> { 0 }; // 超时=默认档（AutoPick 取 0 对齐）

            _btnSelect.onClick.RemoveAllListeners();
            _activeTcs = null;
            _dropdown.gameObject.SetActive(false);
            RestoreBrowseNodes(hidden);
            _region.gameObject.SetActive(regionWasActive);
            IsOpen = false;
            OpenSinceRealtime = 0f;
            _busy = false;
            return result;
        }

        public async UniTask<int> SelectOneAsync(Player chooser, IReadOnlyList<string> options,
            string title, float timeoutSeconds)
        {
            var picked = await SelectIndicesAsync(options, 1, 1, title, "", false, timeoutSeconds);
            return picked != null && picked.Count > 0 ? picked[0] : 0;
        }

        // ==================== 文本反问（2026-10-31 阶段C：卡名宣言等开放集输入） ====================

        /// <summary>ITargetSelectorEx 文本口：Select 窗内 label-str 输入（2026-11-03 改版——prefab
        /// 缺节点或选择条在身时退回模态弹窗），中文输入法随 TMP_InputField 标准内容类型可用。
        /// 超时自持（Ex 契约）——到点按取消收口返回 null
        ///（TargetSelectionService 口径：取消/超时=效果不结算、费用不退）。</summary>
        public async UniTask<string> SelectTextAsync(Player chooser, string title, string defaultValue,
            int maxLen, float timeoutSeconds)
        {
            return await PromptTextAsync(title, defaultValue, maxLen, timeoutSeconds);
        }

        /// <summary>文本反问窗（网络客户端反问复用同一实现——BattleScreen.NetOnTextRequest）。
        /// 返回玩家输入（原样，收口在 RequestTextAsync/TryCompleteText）；null=取消/超时。
        /// 2026-11-03 Select 窗改版：label-str（TMP_InputField）窗口内输入+btn-select 确认（回车
        /// 同效）取代旧 UiKit.ModalBox 弹窗；prefab 缺 label-str 或选择条在身（_busy——窗面共享
        /// label-num/btn-select 不可并行）退回旧模态。在途态入 _activeText* 记账，ForceIdle
        /// 跨局清算一并收口。</summary>
        public async UniTask<string> PromptTextAsync(string title, string defaultValue, int maxLen, float timeoutSeconds)
        {
            if (_region == null) return null; // 无宿主（极端：屏已拆）：按取消收口
            if (_busy || _inputStr == null || _labelNumNode == null || _labelNum == null || _btnSelect == null)
                return await PromptTextModalAsync(title, defaultValue, maxLen, timeoutSeconds); // 选择条共存口径保留——独立弹窗不抢窗面
            return await RunTextInputAsync(title, defaultValue, maxLen, timeoutSeconds);
        }

        /// <summary>窗口内文本输入（Select/label-str）：激活 Select 节点+压浏览三件+label-num 写
        /// 提示；btn-select=确认（此路径无 _busy，btn-select 无在途监听可安全接管）；空输入确认=
        /// 取消（口径沿用：取消/超时=效果不结算、费用不退）。收尾全量还原。</summary>
        private async UniTask<string> RunTextInputAsync(string title, string defaultValue, int maxLen, float timeoutSeconds)
        {
            _onWindowNeedsSpace?.Invoke(); // Select 窗被文本输入占用——Information 让位（需求4 同族）
            bool regionWasActive = _region.gameObject.activeSelf;
            var hidden = HideBrowseNodes();
            bool dropdownWas = _dropdown != null && _dropdown.gameObject.activeSelf;
            if (_dropdown != null) _dropdown.gameObject.SetActive(false);
            if (_btnHide != null) _btnHide.gameObject.SetActive(false); // 文本模式无候选条可收起

            _region.gameObject.SetActive(true);
            _labelNumNode.SetActive(true);
            _viewport.gameObject.SetActive(true); // label-num 在 viewport 下——提示可见须带 viewport
            _inputStr.gameObject.SetActive(true);
            _inputStr.text = defaultValue ?? "";
            if (maxLen > 0) _inputStr.characterLimit = maxLen;
            _inputStr.interactable = true;
            string head = string.IsNullOrWhiteSpace(title) ? "请输入宣言的卡名" : title;
            _labelNum.text = head + "——输入后点「确认」或回车；留空/超时=本次不结算（费用不退）";

            var tcs = new UniTaskCompletionSource<string>();
            _activeTextTcs = tcs;
            void Confirm() => tcs.TrySetResult(_inputStr.text);
            _btnSelect.onClick.RemoveAllListeners(); // 无 _busy 在途监听（Run/RunChoice 收尾已清）——安全接管
            _btnSelect.onClick.AddListener(Confirm);
            _btnSelect.gameObject.SetActive(true);
            _inputStr.onSubmit.RemoveAllListeners();
            _inputStr.onSubmit.AddListener(_ => Confirm()); // 回车确认（onSubmit 仅回车触发，失焦不收口）
            _inputStr.ActivateInputField(); // 直接落焦——免一次点击（IME 随激活拉起）

            float seconds = timeoutSeconds > 0f ? timeoutSeconds : 120f;
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(seconds))
                .ContinueWith(() => (string)null);
            var (win, uiResult, _) = await UniTask.WhenAny(tcs.Task, timeoutTask);
            _activeTextTcs = null;

            _inputStr.onSubmit.RemoveAllListeners();
            _inputStr.DeactivateInputField();
            _inputStr.gameObject.SetActive(false);
            _btnSelect.onClick.RemoveAllListeners();
            _btnSelect.gameObject.SetActive(false);
            if (dropdownWas && _dropdown != null) _dropdown.gameObject.SetActive(true);
            RestoreBrowseNodes(hidden);
            _region.gameObject.SetActive(regionWasActive);
            return win == 0 ? uiResult : null; // 超时=取消（TargetSelectionService 口径）
        }

        /// <summary>旧模态文本弹窗（2026-10-31 阶段C 原实现——2026-11-03 起为退化路径：prefab 缺
        /// label-str 或选择条在身时的兜底）。返回玩家输入；null=取消/超时。</summary>
        private async UniTask<string> PromptTextModalAsync(string title, string defaultValue, int maxLen, float timeoutSeconds)
        {
            var canvas = _region.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            var host = (RectTransform)canvas.transform; // 根画布：置顶兄弟序盖全场

            var modal = UiKit.ModalBox(host, string.IsNullOrWhiteSpace(title) ? "请输入" : title, width: 520f);
            UiKit.Size(UiKit.Label("hint", modal.Panel,
                "输入卡名后点「确认」；点「取消」或留空则本次效果不结算（已支付的费用不退）。",
                13, UiStyle.TextHint, wrap: true), fw: 1f);
            var input = UiKit.InputField("input", modal.Panel, "请输入卡名（支持中文输入法）",
                defaultValue ?? "", width: 480f);
            if (maxLen > 0) input.characterLimit = maxLen;

            var tcs = new UniTaskCompletionSource<string>();
            var bar = UiKit.Row("bar", modal.Panel, spacing: 10f);
            bar.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            UiKit.Button("btn-cancel", bar, "取消", () => tcs.TrySetResult(null));
            UiKit.Button("btn-ok", bar, "确认", () => tcs.TrySetResult(input.text),
                bg: UiStyle.BtnPrimary, fg: UiStyle.White);
            _activeTextTcs = tcs;
            _activeTextModal = modal;

            input.ActivateInputField(); // 直接落焦——免一次点击（IME 随激活拉起）

            float seconds = timeoutSeconds > 0f ? timeoutSeconds : 120f;
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(seconds))
                .ContinueWith(() => (string)null);
            var (win, uiResult, _) = await UniTask.WhenAny(tcs.Task, timeoutTask);
            _activeTextTcs = null;
            _activeTextModal = null;
            modal.Close();
            return win == 0 ? uiResult : null; // 超时=取消（TargetSelectionService 口径）
        }

        // ==================== 选择面装配与驱动 ====================

        private async UniTask<List<int>> Run(IReadOnlyList<CardOverlayItem> items,
            IReadOnlyList<(uint id, int x, int z)?> reticles,
            int min, int max, string title, float timeoutSeconds,
            string prompt = null, bool allowCancel = false)
        {
            bool usable = _region != null && _labelNum != null && _btnSelect != null
                && _content != null && _template != null && items.Count > 0 && max > 0;
            if (!usable || _busy) return AutoPick(items.Count, min); // 缺节点/重入：退化自动选

            if (_previewActive) ClosePreview(); // 真实目标选择开窗——「渲染目标选择」自动让位（2026-10-26 用户定案）
            _onWindowNeedsSpace?.Invoke(); // Select 窗被选择占用——Information 让位（2026-11-03 需求4：连锁/响应同径）
            _busy = true;
            IsOpen = true;
            OpenSinceRealtime = Time.realtimeSinceStartup;
            _min = min;
            _max = max;
            _title = string.IsNullOrWhiteSpace(title) ? null : title;
            _picked.Clear();
            bool regionWasActive = _region.gameObject.activeSelf;

            // 压掉浏览态三件（BuildRegionPanel 的代码子物体；名字单源在 BattleScreen）
            var hidden = HideBrowseNodes();

            _labelNumNode.SetActive(true); // 面板本体（TMP 在子节点上——须控面板而非文本节点）
            _viewport.gameObject.SetActive(true);
            if (_btnHide != null) // 收起口复位（2026-11-03 需求5）：开窗从显示态起，label 回「隐藏」
            {
                _btnHide.gameObject.SetActive(true);
                if (_btnHideLabel != null) _btnHideLabel.text = "隐藏";
            }
            _btnSelect.gameObject.SetActive(false);
            _region.gameObject.SetActive(true);
            _prompt = string.IsNullOrWhiteSpace(prompt) ? null : prompt;
            _onCancelAvailable?.Invoke(allowCancel); // 主动效果可取消=宿主亮「取消」键（提示已并入 label-num）
            if (_region.GetComponent<ScrollRect>() is { } scroll)
                scroll.horizontalNormalizedPosition = 0f; // 新一轮从最左起
            _btnSelect.onClick.RemoveAllListeners();

            ClearInstances();
            _frames.Clear();

            // 红瞄准面片（2026-10-16 晚定案·选中才上盘）：开窗只登记不上盘，选中/取消选中随 Toggle 对拍 Show/Hide
            _reticles = reticles != null ? CopyReticles(reticles, items.Count) : null;
            if (_reticles != null)
            {
                int onBoard = 0;
                for (int i = 0; i < _reticles.Length; i++) if (_reticles[i].HasValue) onBoard++;
                Debug.Log($"[RegionTarget] 选择窗开：候选{items.Count} 其中带地块{onBoard}——瞄准面片选中才上盘（现 {BattleTargetReticle.Count}），候选波逐帧补点亮（现 GlowOn={BattleTileFx.GlowOn}）");
            }

            var tcs = new UniTaskCompletionSource<List<int>>();
            _activeTcs = tcs; // Cancel 外部收口挂点（2026-10-16）
            for (int i = 0; i < items.Count; i++) BuildCard(i, items[i], tcs);
            RefreshLabel();
            _btnSelect.onClick.AddListener(() =>
            {
                if (_picked.Count >= _min && _picked.Count <= _max) tcs.TrySetResult(Sorted());
            });

            // 超时自持（Ex 契约）：到点取当前有效选择，不足 min 则代选前 min 个
            float seconds = timeoutSeconds > 0f ? timeoutSeconds : 60f;
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(seconds))
                .ContinueWith(() => (List<int>)null);
            // 候选波逐帧补点亮（2026-11-01 用户定案：可选区域候选提示沿用生物可行动同款扩散波）：
            // 逐帧原因同 BattleCellPicker——对账 Apply 开头 BattleTileFx.ClearAll 会清波，须持续回补；
            // 候选等同己方蓝口径（SetUnitGlow 默认档），选中标记仍走红瞄准贴片两不冲突
            while (!tcs.Task.Status.IsCompleted() && !timeoutTask.Status.IsCompleted())
            {
                PaintCandidateWaves();
                await UniTask.Yield();
            }
            var (win, uiResult, timeoutResult) = await UniTask.WhenAny(tcs.Task, timeoutTask);
            List<int> result = win == 0 ? uiResult : null;
            if (result == null)
                result = _picked.Count >= _min && _picked.Count <= _max
                    ? Sorted() : AutoPick(items.Count, _min);

            // 收尾：清实例+清面片+清候选波+还原浏览三件+Region 复位进入前活跃态
            _btnSelect.onClick.RemoveAllListeners();
            ClearInstances();
            _frames.Clear();
            if (_reticles != null) // 按登记逐 id 撤（2026-10-18 改：ClearAll 会连坐攻击链瞄准贴片——
                for (int i = 0; i < _reticles.Length; i++) // 连锁响应开窗时 CombatChoreographer 的贴片不受本窗关合影响）
                    if (_reticles[i].HasValue)
                    {
                        BattleTargetReticle.Hide(_reticles[i].Value.id);
                        BattleTileFx.ClearUnitGlow(_reticles[i].Value.x, _reticles[i].Value.z); // 撤候选波（2026-11-01）——真可行动单位随下个对账自动复原
                    }
            _reticles = null;
            RestoreBrowseNodes(hidden);
            _region.gameObject.SetActive(regionWasActive);
            IsOpen = false;
            OpenSinceRealtime = 0f;
            _busy = false;
            _activeTcs = null;
            _prompt = null;
            _onCancelAvailable?.Invoke(false); // 关窗收取消键（仅本屏亮出的才收——2026-10-16 定案沿用）
            if (_btnHide != null) _btnHide.gameObject.SetActive(false); // 收起口随窗收（不外泄到浏览/预览态）
            _viewport.gameObject.SetActive(true); // 收起态不外泄——下次开窗/预览从显示态起
            return result;
        }

        // ==================== 牌库标记预览（2026-10-26 诅咒/祝福·复用选择条的非交互显示面） ====================

        /// <summary>开牌库预览条（宿主=BattleScreen.OpenDeckPreview）：label-num 写标题（无 (x/y)
        /// 计数）、viewport 列全卡面、btn-select 恒藏——非交互展示，即用户口径的「渲染目标选择」。
        /// 与真实选择互斥：_busy 选择在身时静默不开；重复开=先清旧再装配。压浏览三件/Region
        /// 活跃态记忆还原与 Run 同款；真实选择 Run() 开窗时自动让位收起。</summary>
        public void ShowPreview(string title, IReadOnlyList<CardOverlayItem> items)
        {
            if (_region == null || _labelNum == null || _viewport == null || _content == null
                || _template == null || _btnSelect == null) return;
            if (_busy || items == null || items.Count == 0) return; // 真实选择在身/空表不开
            ClosePreview();
            _onWindowNeedsSpace?.Invoke(); // Select 窗被预览占用——Information 让位（2026-11-03 需求4 同族）

            _previewActive = true;
            _previewRegionWasActive = _region.gameObject.activeSelf;
            _previewHidden = HideBrowseNodes();
            _labelNumNode.SetActive(true);
            _labelNum.text = string.IsNullOrWhiteSpace(title) ? "牌库 · 被标记的卡" : title;
            _viewport.gameObject.SetActive(true);
            _btnSelect.gameObject.SetActive(false); // 预览无确认语义——确定键恒藏
            if (_btnHide != null) _btnHide.gameObject.SetActive(false); // 预览非交互——收起口同藏
            _region.gameObject.SetActive(true);
            if (_region.GetComponent<ScrollRect>() is { } scroll)
                scroll.horizontalNormalizedPosition = 0f; // 与 Run 同拍从最左起
            ClearInstances();
            for (int i = 0; i < items.Count; i++) BuildPreviewCard(i, items[i]);
        }

        /// <summary>收预览条：清实例+还原浏览三件与 Region 活跃态。未开=无操作（幂等）。</summary>
        public void ClosePreview()
        {
            if (!_previewActive) return;
            _previewActive = false;
            ClearInstances();
            RestoreBrowseNodes(_previewHidden);
            _previewHidden = null;
            _region.gameObject.SetActive(_previewRegionWasActive);
        }

        /// <summary>预览卡面：BuildCard 去交互版——无选中框/无点击 Button（纯展示）。
        /// 2026-10-30：候选卡按 AHandCard 战场尺寸渲染（ApplyBattleFaceScale 单源）。</summary>
        private void BuildPreviewCard(int idx, CardOverlayItem item)
        {
            var rt = UnityEngine.Object.Instantiate(_template, _content, false);
            rt.gameObject.SetActive(true);
            rt.name = "preview-" + idx;
            HandFanView.ApplyBattleFaceScale(rt);
            var cost = rt.Find("cost");
            if (cost != null) cost.gameObject.SetActive(false);
            var btnLayer = rt.Find("btn");
            if (btnLayer != null) btnLayer.gameObject.SetActive(false);
            var face = rt.gameObject.AddComponent<ACardView>();
            face.Bind();
            face.Apply(item, CardOverlayLayout.Full);
        }

        /// <summary>实例化一张候选卡：去 cost 费用行/btn 层，ACardView 全卡面 + 根 Button 点击切换。
        /// 2026-10-30：按 AHandCard 战场尺寸渲染（ApplyBattleFaceScale 单源）。</summary>
        private void BuildCard(int idx, CardOverlayItem item, UniTaskCompletionSource<List<int>> tcs)
        {
            var rt = UnityEngine.Object.Instantiate(_template, _content, false);
            rt.gameObject.SetActive(true);
            rt.name = "pick-" + idx;
            HandFanView.ApplyBattleFaceScale(rt);
            var cost = rt.Find("cost");
            if (cost != null) cost.gameObject.SetActive(false);
            var btnLayer = rt.Find("btn");
            if (btnLayer != null) btnLayer.gameObject.SetActive(false);
            var rootImg = rt.GetComponent<Image>();
            if (rootImg != null) rootImg.raycastTarget = true; // 2026-11-03 角色候选点不到修复：ACard 根
            // Image 烘焙 raycastTarget=0，而角色占位卡类型块/行区全隐=整卡无射线命中（看得到点不到）；
            // 统一开根底图射线=全卡可点（普通卡无行为差异——子块命中与根命中同冒泡到根 Button）

            var face = rt.gameObject.AddComponent<ACardView>();
            face.Bind();
            face.Apply(item, CardOverlayLayout.Full);

            // 选中高亮框（首子物体外扩 6px；ignoreLayout 防被卡片根 VLG 排版挪位）
            var frameGo = new GameObject("sel-frame", typeof(RectTransform));
            frameGo.transform.SetParent(rt, false);
            frameGo.transform.SetAsFirstSibling();
            var frt = (RectTransform)frameGo.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(-6f, -6f);
            frt.offsetMax = new Vector2(6f, 6f);
            var fimg = frameGo.AddComponent<Image>();
            fimg.sprite = UiKit.RoundedSprite;
            fimg.type = Image.Type.Sliced;
            fimg.color = UiStyle.SelectedRowBg;
            frameGo.AddComponent<LayoutElement>().ignoreLayout = true;
            frameGo.SetActive(false);
            _frames.Add(fimg);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                Toggle(idx);
                RefreshLabel();
            });
        }

        private void Toggle(int idx)
        {
            if (_picked.Contains(idx))
            {
                _picked.Remove(idx);
                if (_frames[idx] != null) _frames[idx].gameObject.SetActive(false);
                SyncReticles();
                return;
            }
            if (_picked.Count >= _max) return;
            if (_max == 1) // 单选互斥：清其它高亮
            {
                _picked.Clear();
                foreach (var f in _frames) if (f != null) f.gameObject.SetActive(false);
            }
            _picked.Add(idx);
            if (_frames[idx] != null) _frames[idx].gameObject.SetActive(true);
            SyncReticles();
        }

        /// <summary>面片与 _picked 全量对拍（选中=上盘/取消=下盘；含单选互斥同帧生效——被清的也下盘）。</summary>
        private void SyncReticles()
        {
            if (_reticles == null) return;
            for (int i = 0; i < _reticles.Length; i++)
                if (_reticles[i].HasValue)
                {
                    var r = _reticles[i].Value;
                    if (_picked.Contains(i)) BattleTargetReticle.Show(r.id, r.x, r.z);
                    else BattleTargetReticle.Hide(r.id);
                }
        }

        /// <summary>候选地块扩散波补点亮（2026-11-01 开窗期逐帧·生物可行动同款）：_reticles 有值的
        /// 候选=场上单位格/角色格（单位经 boardCellOf、角色经 roleCellOf 注入定位）；纯选项路径
        ///（_reticles==null）无实体不画。</summary>
        private void PaintCandidateWaves()
        {
            if (_reticles == null) return;
            for (int i = 0; i < _reticles.Length; i++)
                if (_reticles[i].HasValue)
                    BattleTileFx.SetUnitGlow(_reticles[i].Value.x, _reticles[i].Value.z);
        }

        /// <summary>按候选数定长拷贝（防外部数组复用跨窗串号；越界截断）。</summary>
        private static (uint id, int x, int z)?[] CopyReticles(IReadOnlyList<(uint id, int x, int z)?> src, int count)
        {
            var r = new (uint id, int x, int z)?[count];
            for (int i = 0; i < count && i < src.Count; i++) r[i] = src[i];
            return r;
        }

        private void RefreshLabel()
        {
            // 2026-10-17 提示并入：提示全文（效果文/响应说明）优先，空回落标题（如「回合结束，手牌超出
            // 7张，请选择丢弃」——标题即"在为什么选目标"），再空回落通用文案；计数（x/y）同面板尾随
            string head = _prompt ?? (string.IsNullOrWhiteSpace(_title) ? "目标数量" : _title);
            _labelNum.text = $"{head}（{_picked.Count}/{_max}）";
            bool ready = _picked.Count >= _min && _picked.Count <= _max;
            if (_btnSelect.gameObject.activeSelf != ready) _btnSelect.gameObject.SetActive(ready);
        }

        private List<int> Sorted()
        {
            var r = new List<int>(_picked);
            r.Sort();
            return r;
        }

        private static List<int> AutoPick(int count, int min)
        {
            var r = new List<int>();
            for (int i = 0; i < min && i < count; i++) r.Add(i);
            return r;
        }

        /// <summary>清 content 下全部实例（模板本体保留）。</summary>
        private void ClearInstances()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                if (!ReferenceEquals(_content.GetChild(i), _template))
                    Destroy(_content.GetChild(i).gameObject);
        }

        /// <summary>压掉浏览态三件（title/lines/cards——BattleRegionPanel 的代码子物体；名字单源
        /// 在 BattleScreen）。返回原显隐清单供 RestoreBrowseNodes 还原（Run/预览/文本/下拉共用）。</summary>
        private List<KeyValuePair<GameObject, bool>> HideBrowseNodes()
        {
            var hidden = new List<KeyValuePair<GameObject, bool>>();
            foreach (var kid in new[] { "title", "lines", "cards" })
            {
                var t = _region.Find(kid);
                if (t != null)
                {
                    hidden.Add(new KeyValuePair<GameObject, bool>(t.gameObject, t.gameObject.activeSelf));
                    t.gameObject.SetActive(false);
                }
            }
            return hidden;
        }

        private static void RestoreBrowseNodes(List<KeyValuePair<GameObject, bool>> hidden)
        {
            if (hidden == null) return;
            foreach (var kv in hidden) kv.Key.SetActive(kv.Value);
        }

        private static void Destroy(GameObject go)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
