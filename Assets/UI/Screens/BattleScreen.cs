using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using CardCore.Network;
using CardCore.Serialization;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>对战模式。</summary>
    public enum BattleMode
    {
        /// <summary>本地 vs 极简 AI（GameCore 直连）。</summary>
        LocalAI,
        /// <summary>网络对局（快照渲染 + intent 上行）。</summary>
        Network,
    }

    /// <summary>
    /// 对战入口参数（UIManager.Show 无参构造——静态传参）。
    /// 连接配置属匹配界面（阶段三）职责；当前注入方=主菜单调试按钮（默认本机）与
    /// 匹配界面（Client 接续：已进房/已提交卡组的现成连接，本屏消费后接管生命周期）。
    /// </summary>
    public static class BattleEntry
    {
        public static BattleMode Mode = BattleMode.LocalAI;
        public static string Host = "127.0.0.1";
        public static int Port = 7777;
        public static string Nickname = "player";

        /// <summary>匹配界面移交的现成连接（非空=已进房+已提交卡组，跳过连接与 JoinRoom，
        /// 一次性消费防跨局残留）。null=调试直连路径（本屏自建连接）。</summary>
        public static NetGameClient Client;
    }

    /// <summary>
    /// 对战界面（2026-09-24 阶段四重做，2D HUD 双模式）：
    /// 本地 AI（GameCore/GameActions 直连）与网络（MsgGameStateSync 快照全量渲染 +
    /// NetEventBatch 中文战报 + intent 上行 + SelectRequest 反问弹窗 + PassPriority 让行）。
    /// 战场格子化：13×8 棋盘的双方 2×9 单位区 + 1×9 地牌行按 z 序纵向铺开、奇数行
    /// 半格偏移（odd-r 六角视觉）——箭头光环的位置语义可见；本地格位=BoardState 权威，
    /// 网络=ZoneCards 列表序按 BoardLayout 同规重建（零协议改动）。
    /// 交互语义沿用旧屏（模式选择时序/目标弹窗/攻击开窗/响应窗口），旧 Battle.uxml 不复用。
    /// </summary>
    public sealed class BattleScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/Battle";

        private BattleController _ctrl;                 // 本地模式
        private NetGameClient _net;                     // 网络模式
        private BattleViewData _view;                   // 当前视图（渲染唯一输入）
        private readonly Queue<MsgSelectRequest> _pendingSelects = new Queue<MsgSelectRequest>();
        private bool _selectBusy;
        private int _netRoomPhase = -1;
        private bool _netDeckSent;
        private bool _netGameOver;
        private bool _running;
        private bool _gameEnded;

        private bool IsNetwork => BattleEntry.Mode == BattleMode.Network;
        private GameCore Core => _ctrl?.Core;
        private Player P1 => _ctrl?.P1;
        private Player P2 => _ctrl?.P2;

        // ======================================== 生命周期 ========================================

        public override void OnEnter()
        {
            _gameEnded = false;
            _netGameOver = false;
            _selectBusy = false;
            _pendingSelects.Clear();
            _netDeckSent = false;
            _netRoomPhase = -1;
            _view = null;
            _running = true;

            UIBinder.BindButton(Root, "btn-back", OnBack);
            UIBinder.BindButton(Root, "btn-skip-standby", OnSkipStandby);
            UIBinder.BindButton(Root, "btn-grave-play", OnGraveyardPlay);
            UIBinder.BindButton(Root, "btn-activate-skill", OnActivateSkill);
            UIBinder.BindButton(Root, "btn-pass-priority", OnPassPriority);
            UIBinder.BindButton(Root, "btn-end-turn", OnEndTurn);
            UIBinder.BindButton(Root, "btn-concede", OnConcede);
            UIBinder.BindButton(Root, "overlay-cancel", OnOverlayCancel);

            Q<Button>("btn-pass-priority").style.display = IsNetwork ? DisplayStyle.Flex : DisplayStyle.None;
            Q<Label>("lbl-mode").text = IsNetwork ? $"网络 · {BattleEntry.Host}:{BattleEntry.Port}" : "本地 · AI";

            if (IsNetwork)
                StartNetAsync().Forget();
            else
                StartLocal();
        }

        public override void OnExit()
        {
            _running = false;
            _net?.Close();
            _net = null;

            // 仅在仍是本界面注册时解除，避免覆盖其它界面的注册
            if (TargetSelectionService.Current is UiTargetSelector)
                TargetSelectionService.Current = null;
            if (ResponseWindowService.HumanResponder == ShowResponsePopupAsync)
                ResponseWindowService.HumanResponder = null;
        }

        private void OnBack() => Manager.Back();

        // ======================================== 本地模式 ========================================

        private void StartLocal()
        {
            _ctrl = new BattleController();
            _ctrl.StartNewGame();

            // 引擎事件多但刷新幂等：统一全量重建；战报行同时追加。
            Subscribe<TurnStartEvent>(e => { AppendLocal(e); RefreshLocal(); });
            Subscribe<PhaseStartEvent>(_ => RefreshLocal());
            Subscribe<CardDrawEvent>(e => AppendLocal(e));
            Subscribe<CardPlayEvent>(e => { AppendLocal(e); RefreshLocal(); });
            Subscribe<CardZoneChangeEvent>(_ => RefreshLocal());
            Subscribe<CardPutToBattlefieldEvent>(_ => RefreshLocal());
            Subscribe<CardLeaveBattlefieldEvent>(_ => RefreshLocal());
            Subscribe<LifeChangeEvent>(e => { AppendLocal(e); RefreshLocal(); });
            Subscribe<CombatDamageEvent>(e => AppendLocal(e));
            Subscribe<CardDestroyEvent>(e => { AppendLocal(e); RefreshLocal(); });
            Subscribe<FatigueEvent>(e => AppendLocal(e));
            Subscribe<ElementPoolAddEvent>(_ => RefreshLocal());
            Subscribe<ElementPoolPayEvent>(_ => RefreshLocal());
            Subscribe<StackEmptyEvent>(_ => RefreshLocal());
            Subscribe<GameOverEvent>(OnGameOver);

            // 通用目标选择器（效果引擎结算期反问）
            TargetSelectionService.Current = new UiTargetSelector(Root);
            // 响应窗口（发动弹窗）：人类候选→简版选择弹窗
            ResponseWindowService.HumanResponder = ShowResponsePopupAsync;

            RefreshLocal();
        }

        private void AppendLocal(IGameEvent e)
        {
            var line = BattleEventText.ForLocal(e, p => p == P1);
            AppendLog(line.Text, line.Class);
        }

        private void RefreshLocal()
        {
            if (Core == null || P1 == null || P2 == null) return;
            _view = BattleView.BuildLocal(Core, _ctrl.Board, P1, P2);
            RefreshView();
        }

        private void OnGameOver(GameOverEvent e)
        {
            _gameEnded = true;
            bool win = e.Winner == P1;
            ShowOverlay(win ? "胜利" : "失败", $"{(win ? "我方" : "对手")}获胜（{e.Reason}）。", null, null);
            Q<Button>("overlay-cancel").text = "返回主菜单";
            RefreshLocal();
        }

        // ======================================== 网络模式 ========================================

        private async UniTaskVoid StartNetAsync()
        {
            _ctrl = null;
            bool injected = BattleEntry.Client != null; // 匹配界面接续（阶段三）：已进房+已提交卡组
            _net = BattleEntry.Client ?? new NetGameClient();
            BattleEntry.Client = null; // 一次性消费
            _net.OnRoomState += OnNetRoomState;
            _net.OnManifest += OnNetManifest;
            _net.OnSnapshot += OnNetSnapshot;
            _net.OnEventBatch += OnNetEventBatch;
            _net.OnSelectRequest += OnNetSelectRequest;
            _net.OnError += OnNetError;
            _net.OnDisconnected += () => AppendLog("与服务器的连接已断开", "log-line--combat");

            if (injected)
            {
                _netDeckSent = true; // 卡组已在匹配界面提交
                AppendLog($"接续匹配连接 {_net.Remote}", "log-line--system");
            }
            else
            {
                try
                {
                    _net.Connect(BattleEntry.Host, BattleEntry.Port);
                }
                catch (Exception ex)
                {
                    ShowOverlay("连接失败", $"{BattleEntry.Host}:{BattleEntry.Port} — {ex.Message}", null, null);
                    return;
                }

                AppendLog($"已连接 {_net.Remote}", "log-line--system");
                _net.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom
                {
                    RoomId = "",
                    WantSeat = -1, // 任意空位（双客户端先后就座 0/1）
                    Nickname = string.IsNullOrEmpty(BattleEntry.Nickname)
                        ? "player" + UnityEngine.Random.Range(100, 999)
                        : BattleEntry.Nickname,
                });
            }

            // 主循环：每帧泵收发 + 房间状态机（卡组提交）；终局后继续泵收尾战报，退出由用户返回触发
            while (_running && !_net.Disconnected)
            {
                _net.Pump();

                if (_netRoomPhase == (int)NetRoomPhase.DeckSubmit && !_netDeckSent)
                    SendNetDeck();

                await UniTask.Yield();
            }
        }

        private void SendNetDeck()
        {
            _netDeckSent = true;
            // 测试期随机卡组（卡组选定属匹配阶段职责）
            var deckIds = BattleController.RandomDeckFrom(CardCatalog.LoadAll())
                .Select(c => c.ID).ToArray();
            _net.Send(NetworkMessageType.DeckSubmit, new MsgDeckSubmit
            {
                DeckName = BattleEntry.Nickname,
                CardIds = deckIds,
                Digest = NetMatchHandshake.ComputeDeckDigest(deckIds),
            });
            AppendLog($"已提交随机卡组（{deckIds.Length} 张）", "log-line--system");
        }

        private void OnNetRoomState(MsgRoomState room)
        {
            _netRoomPhase = room?.Phase ?? -1;
            if (_netRoomPhase == (int)NetRoomPhase.Playing && !_netGameOver)
                AppendLog("对局进行中", "log-line--system");
            if (_netRoomPhase == (int)NetRoomPhase.Finished)
                AppendLog("房间已结束（可返回）", "log-line--system");
        }

        private void OnNetManifest(MsgMatchManifest manifest)
        {
            if (manifest == null) return;
            AppendLog(manifest.OwnSeat == 0
                ? "对局开始：我方先手"
                : "对局开始：我方后手", "log-line--turn");
        }

        private void OnNetSnapshot(MsgGameStateSync snap)
        {
            _view = BattleView.BuildNet(snap);
            RefreshView();
        }

        private void OnNetEventBatch(NetEvent[] events)
        {
            int viewer = _lastViewerSeat; // viewerSeat 以最新快照为准
            foreach (var e in events)
            {
                var line = BattleEventText.ForNet(e, viewer);
                AppendLog(line.Text, line.Class);
                if (e.EventType == "GameOverEvent")
                {
                    _netGameOver = true;
                    _gameEnded = true;

                    // 终局弹窗（Winner 实体引用按座位判胜负）
                    var winnerParam = e.Params != null
                        ? e.Params.FirstOrDefault(p => p.FieldName == "Winner")
                        : null;
                    var winnerRef = winnerParam != null && winnerParam.EntityRefs != null && winnerParam.EntityRefs.Length > 0
                        ? winnerParam.EntityRefs[0] : null;
                    bool win = winnerRef != null && winnerRef.IsPlayer && winnerRef.Seat == viewer;
                    ShowOverlay(win ? "胜利" : "失败", "对局结束。", null, null);
                    Q<Button>("overlay-cancel").text = "返回主菜单";
                }
            }
        }

        private int _lastViewerSeat;

        private void OnNetSelectRequest(MsgSelectRequest req)
        {
            _pendingSelects.Enqueue(req);
            DrainNetSelects().Forget();
        }

        /// <summary>反问串行处理（一次一弹窗；候选=实体反问按 CardId 查表名，空候选=纯选项 Labels）。</summary>
        private async UniTaskVoid DrainNetSelects()
        {
            if (_selectBusy) return;
            _selectBusy = true;
            try
            {
                while (_pendingSelects.Count > 0 && _running)
                {
                    var req = _pendingSelects.Dequeue();
                    var labels = req.Candidates != null && req.Candidates.Length > 0
                        ? req.Candidates.Select(r => BattleView.EntityTextOf(r, _lastViewerSeat)).ToList()
                        : (req.Labels ?? Array.Empty<string>()).ToList();
                    int min = Math.Max(1, req.Min);
                    int max = Math.Max(min, req.Max);

                    List<int> picked;
                    if (labels.Count == 0)
                        picked = new List<int>();
                    else
                        picked = await new UiTargetSelector(Root).SelectIndicesAsync(
                            labels, min, max,
                            string.IsNullOrEmpty(req.Title) ? "请选择" : req.Title,
                            req.Hint ?? "", req.AllowCancel,
                            req.TimeoutSeconds > 0 ? req.TimeoutSeconds : 60f);

                    if (_net != null)
                        _net.Send(NetworkMessageType.SelectResponse, new MsgSelectResponse
                        {
                            RequestId = req.RequestId,
                            Indices = (picked ?? new List<int>()).ToArray(),
                        });
                }
            }
            finally { _selectBusy = false; }
        }

        private void OnNetError(MsgError err)
        {
            ShowToast($"被拒（{err?.Context}）：{err?.Reason}");
            AppendLog($"操作被拒（{err?.Context}）：{err?.Reason}", "log-line--combat");
        }

        // ======================================== 全量渲染（两种模式统一输入 BattleViewData） ========================================

        private void RefreshView()
        {
            var d = _view;
            if (d == null) return;

            // 观众席记录（网络事件文本用）
            if (IsNetwork && _view.NetViewerSeat >= 0) _lastViewerSeat = _view.NetViewerSeat;

            // 信息栏
            FillBar(d.Opp, "lbl-opp", "opp-skill", oppSide: true);
            FillBar(d.Self, "lbl-self", "self-skill", oppSide: false);

            // 战场（行序=棋盘 z 序；对手在上）
            BuildHexRow(Q<VisualElement>("opp-lands"), d.OppLands, z: 1, mine: false, isLand: true);
            BuildHexRow(Q<VisualElement>("opp-units-near"), d.OppUnits, z: 2, mine: false, isLand: false);
            BuildHexRow(Q<VisualElement>("opp-units-far"), d.OppUnits, z: 3, mine: false, isLand: false);
            BuildHexRow(Q<VisualElement>("self-units-far"), d.SelfUnits, z: 4, mine: true, isLand: false);
            BuildHexRow(Q<VisualElement>("self-units-near"), d.SelfUnits, z: 5, mine: true, isLand: false);
            BuildHexRow(Q<VisualElement>("self-lands"), d.SelfLands, z: 6, mine: true, isLand: true);

            // 中栏
            Q<Label>("lbl-phase").text = d.PhaseText;
            Q<Label>("lbl-stack").text = string.Join(" ｜ ", d.StackLines);
            Q<Label>("lbl-priority").text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";
            Q<Label>("lbl-activation").text = d.ActivationTexts.Count > 0 ? string.Join("；", d.ActivationTexts) : "";

            // 我方手牌
            BuildHand(d);

            UpdateButtons(d);
        }

        private void FillBar(BattlePlayerView p, string prefix, string skillName, bool oppSide)
        {
            string aiTag = p.IsAI ? "（AI）" : "";
            Q<Label>($"{prefix}-name").text = oppSide ? $"对手{aiTag}" : "我方";
            Q<Label>($"{prefix}-life").text = $"生命 {p.Life}/{p.MaxHealth}";
            Q<Label>($"{prefix}-deck").text = $"牌库 {p.DeckCount}";
            Q<Label>($"{prefix}-hand").text = $"手牌 {p.HandCount}";
            Q<Label>($"{prefix}-grave").text = $"墓地 {p.GraveyardCount}";
            Q<Label>($"{prefix}-fatigue").text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
            Q<Label>($"{prefix}-bank").text = $"地牌上限 {p.LandCap} · 元素池 {p.BankText}";

            var chip = Q<VisualElement>(skillName);
            chip.Clear();
            if (p.Skill != null)
            {
                var lbl = new Label($"技能：{p.Skill.Name}{(p.Skill.IsTapped ? "（已横置）" : "")} {p.Skill.CostText}");
                chip.Add(lbl);
                if (p.Skill.IsTapped) chip.AddToClassList("skill-chip--tapped");
                else chip.RemoveFromClassList("skill-chip--tapped");
            }
            else
            {
                chip.Add(new Label("技能：—"));
            }
        }

        /// <summary>一行 9 格（x=2..10）；空格=底座，有卡嵌 battle-card。</summary>
        private void BuildHexRow(VisualElement row, List<BattleCardView> cards, int z, bool mine, bool isLand)
        {
            row.Clear();
            for (int x = 2; x <= 10; x++)
            {
                var cell = new VisualElement();
                cell.AddToClassList("hex-cell");
                if (isLand) cell.AddToClassList("hex-cell--land");

                var card = cards.FirstOrDefault(c => c.X == x && c.Z == z);
                if (card != null)
                    cell.Add(MakeCardElement(card, mine, isLand));

                row.Add(cell);
            }
        }

        private VisualElement MakeCardElement(BattleCardView card, bool mine, bool isLand)
        {
            var el = new VisualElement();
            el.AddToClassList("battle-card");
            if (card.IsTapped) el.AddToClassList("battle-card--tapped");
            if (isLand) el.style.width = Length.Percent(100);

            var name = new Label(card.Name) { name = "card-name" };
            name.AddToClassList("battle-card__name");
            el.Add(name);

            var body = isLand
                ? (card.LandTokensText ?? "（耗尽）") + (card.IsTapped ? " 横置" : "")
                : card.StatsText;
            var stats = new Label(body);
            stats.AddToClassList("battle-card__stats");
            el.Add(stats);

            if (!isLand && !string.IsNullOrEmpty(card.CostText))
            {
                var cost = new Label($"费 {card.CostText}");
                cost.AddToClassList("battle-card__stats");
                el.Add(cost);
            }

            // 六向箭头指示点（箭头光环位置语义；位=屏幕方向）
            if (!isLand && card.ArrowFlags != 0)
            {
                AddArrowDot(el, "ne", card.ArrowFlags, BattleView.ArrowNE);
                AddArrowDot(el, "e", card.ArrowFlags, BattleView.ArrowE);
                AddArrowDot(el, "se", card.ArrowFlags, BattleView.ArrowSE);
                AddArrowDot(el, "sw", card.ArrowFlags, BattleView.ArrowSW);
                AddArrowDot(el, "w", card.ArrowFlags, BattleView.ArrowW);
                AddArrowDot(el, "nw", card.ArrowFlags, BattleView.ArrowNW);
            }

            // 交互：我方手牌（出牌/放地）、我方单位（攻击）、我方地牌（产元素）
            if (mine)
            {
                if (isLand) el.RegisterCallback<ClickEvent>(_ => OnClickMyLand(card));
                else el.RegisterCallback<ClickEvent>(_ => OnClickMyUnit(card));
            }
            return el;
        }

        private static void AddArrowDot(VisualElement parent, string dir, int flags, int bit)
        {
            var dot = new VisualElement();
            dot.AddToClassList("arrow-dot");
            dot.AddToClassList($"arrow-dot--{dir}");
            if ((flags & bit) != 0) dot.AddToClassList("arrow-dot--on");
            parent.Add(dot);
        }

        private void BuildHand(BattleViewData d)
        {
            var hand = Q<VisualElement>("self-hand");
            hand.Clear();
            foreach (var card in d.SelfHand)
            {
                var el = MakeCardElement(card, mine: false, isLand: false);
                var captured = card;
                el.RegisterCallback<ClickEvent>(_ => OnClickHandCard(captured));
                hand.Add(el);
            }
        }

        private void UpdateButtons(BattleViewData d)
        {
            bool myMain = d.MyTurn && d.Phase == PhaseType.Main && !_gameEnded;
            bool myStandby = d.MyTurn && d.Phase == PhaseType.Standby && !_gameEnded;

            SetEnabled("btn-skip-standby", myStandby);
            SetEnabled("btn-grave-play", myMain && d.Self.GraveyardCount > 0);
            // 网络协议无技能 intent 通道（IntentActivateEffect 寻址卡面效果定义，技能卡不挂原子）——本地可用，网络暂禁
            SetEnabled("btn-activate-skill", !IsNetwork && myMain && !_gameEnded);
            SetEnabled("btn-pass-priority", IsNetwork && d.StackNotEmpty && d.MyPriority && !_gameEnded);
            SetEnabled("btn-end-turn", d.MyTurn && !_gameEnded);
            SetEnabled("btn-concede", !_gameEnded);
        }

        private void SetEnabled(string name, bool enabled) => Q<Button>(name).SetEnabled(enabled);

        // ======================================== 操作派发（本地直调 GameActions / 网络转 intent） ========================================

        private bool GateMyMain()
        {
            if (_gameEnded || _view == null) return false;
            if (!_view.MyTurn || _view.Phase != PhaseType.Main) return false;
            return true;
        }

        /// <summary>点手牌：抉择卡先模式窗；生物弹「打出/放地」二选一；其余直接打出（网络 Targets=null 交服务器反问）。</summary>
        private void OnClickHandCard(BattleCardView card)
        {
            if (!GateMyMain()) return;

            bool isModal = GetModeCount(card) >= 2;
            bool isCreature = IsCreatureCard(card);

            if (isModal)
            {
                PromptModeThenPlay(card, fromGrave: false);
                return;
            }

            if (isCreature)
            {
                ShowChoiceOverlay("使用生物", $"{card.Name}：作为单位打出，或横置放入元素池（地牌）？",
                    new List<string> { "作为单位打出", "放入元素池（地牌）" }, idx =>
                    {
                        CloseOverlay();
                        if (idx == 0) PlayCardFinal(card, null, 0, fromGrave: false);
                        else LandFinal(card, 0);
                    });
                return;
            }

            PlayCardFinal(card, null, 0, fromGrave: false);
        }

        private static bool IsCreatureCard(BattleCardView card)
        {
            return DataOf(card)?.Supertype == Cardtype.Creature;
        }

        private static int GetModeCount(BattleCardView card)
        {
            var data = DataOf(card);
            return data == null ? 1 : CostDerivationService.GetModeCount(data);
        }

        /// <summary>卡的静态数据：本地=引擎对象直读；网络=CardId 查本地卡表（引擎对象缺失）。</summary>
        private static CardData DataOf(BattleCardView card)
        {
            if (card.CoreCard != null) return (card.CoreCard as CardWrapper)?.GetData();
            return string.IsNullOrEmpty(card.CardId) ? null : CardCatalog.GetById(card.CardId);
        }

        /// <summary>抉择模式窗（定案时序：先选模式→验费用→目标→出牌）。网络侧费用校验交服务器（Error 反馈）。</summary>
        private void PromptModeThenPlay(BattleCardView card, bool fromGrave)
        {
            var data = DataOf(card);
            var labels = FindChoiceLabels(data);
            int modeCount = Math.Max(2, CostDerivationService.GetModeCount(data));

            var options = new List<string>();
            for (int i = 0; i < modeCount; i++)
            {
                string name = i < labels.Count && !string.IsNullOrEmpty(labels[i]) ? labels[i] : $"模式 {i + 1}";
                // 本地=运行口径费（GetCardCost）；网络=声明费（无引擎对象，服务器校验权威）
                string cost = IsNetwork
                    ? (data != null ? BattleView.CostTextOf(data.Cost) : "?")
                    : CostText(LocalCostOf(card, i));
                options.Add($"{name}（费用 {cost}）");
            }

            ShowChoiceOverlay("选择模式", $"为 {card.Name} 选择要发动的分支：", options, idx =>
            {
                CloseOverlay();

                if (!IsNetwork)
                {
                    var core = Core;
                    if (core != null && !core.ElementPool.CanPayCost(GameActions.GetCardCost(card.CoreCard, idx), P1))
                    {
                        ShowToast("费用不足——该分支不可发动");
                        RefreshLocal();
                        return;
                    }
                }

                PlayCardFinal(card, null, idx, fromGrave);
            });
        }

        private static Dictionary<int, float> LocalCostOf(BattleCardView card, int modeIndex)
        {
            if (card.CoreCard == null) return null;
            return GameActions.GetCardCost(card.CoreCard, modeIndex);
        }

        private static string CostText(Dictionary<int, float> cost)
            => cost == null || cost.Count == 0 ? "0" : BattleView.CostTextOf(cost);

        /// <summary>出牌终态：本地=声明期目标预选+PlayCard+响应窗口泵；网络=intent 上行（Targets=null 服务器反问）。</summary>
        private void PlayCardFinal(BattleCardView card, List<Entity> targets, int modeIndex, bool fromGrave)
        {
            if (IsNetwork)
            {
                _net?.Send(NetworkMessageType.IntentPlayCard, new MsgIntentPlayCard
                {
                    CardRuntimeId = card.RuntimeId,
                    Targets = null, // 声明期目标交引擎反问（SelectRequest 弹窗）——NetClientBrain 同款
                    FromZone = fromGrave ? (int)Zone.Graveyard : (int)Zone.Hand,
                    ModeIndex = modeIndex,
                });
                return;
            }

            var core = Core;
            if (core == null || P1 == null) return;

            // 指向性法术：声明期目标弹窗（本地候选）
            if (targets == null)
            {
                var atomic = FindTargetingAtomic(card.CoreCard, modeIndex);
                if (atomic != null)
                {
                    PromptLocalTargetThenPlay(card, atomic, modeIndex, fromGrave);
                    return;
                }
            }

            bool ok = fromGrave
                ? GameActions.PlayCardFromGraveyard(core, P1, card.CoreCard, targets, modeIndex)
                : GameActions.PlayCard(core, P1, card.CoreCard, targets, Zone.Hand, modeIndex);
            if (ok)
                _ctrl.SettleResponseWindow().Forget();
            else if (LockRevealedAura.IsLockedThisTurn(card.CoreCard))
                ShowToast("该卡本回合被锁定，不可使用");
            else
                ShowToast(fromGrave ? "无法使用（配额已用或不可支付）" : "无法打出（费用/条件不满足）");
            RefreshLocal();
        }

        /// <summary>本地声明期目标弹窗（候选=TargetResolver 引擎筛选）。</summary>
        private void PromptLocalTargetThenPlay(BattleCardView card, AtomicEffectConfig cfg, int modeIndex, bool fromGrave)
        {
            var core = Core;
            var ctx = new EffectExecutionContext
            {
                Source = card.CoreCard,
                Controller = P1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var resolver = new TargetResolver(core.ZoneManager);
            var candidates = resolver.GetCandidates(cfg.GetTargetKindList(), cfg.TargetFilter, ctx);
            if (!string.IsNullOrEmpty(cfg.TargetFilter))
                candidates = resolver.ApplyFilters(candidates, resolver.ParseFilters(cfg.TargetFilter), ctx);

            if (candidates.Count == 0)
            {
                // 无候选：按无目标直接结算（引擎自动解析兜底）
                PlayCardDirect(card, null, modeIndex, fromGrave);
                return;
            }

            var options = new List<Tuple<string, Action>>();
            foreach (var entity in candidates)
            {
                var captured = entity;
                options.Add(Tuple.Create<string, Action>(EntityDisplay(captured), () =>
                {
                    CloseOverlay();
                    PlayCardDirect(card, new List<Entity> { captured }, modeIndex, fromGrave);
                }));
            }
            ShowPickOverlay("选择目标", $"为 {card.Name} 选择目标：", options);
        }

        private void PlayCardDirect(BattleCardView card, List<Entity> targets, int modeIndex, bool fromGrave)
        {
            var core = Core;
            bool ok = fromGrave
                ? GameActions.PlayCardFromGraveyard(core, P1, card.CoreCard, targets, modeIndex)
                : GameActions.PlayCard(core, P1, card.CoreCard, targets, Zone.Hand, modeIndex);
            if (ok)
                _ctrl.SettleResponseWindow().Forget();
            else
                ShowToast("无法打出（费用/条件不满足）");
            RefreshLocal();
        }

        /// <summary>放地入元素池（生物地牌资格；抉择模式产指示物口径 MVP 取 0 档）。</summary>
        private void LandFinal(BattleCardView card, int modeIndex)
        {
            if (IsNetwork)
            {
                _net?.Send(NetworkMessageType.IntentAddToElementPool, new MsgIntentAddToElementPool
                {
                    CardRuntimeId = card.RuntimeId,
                    ModeIndex = modeIndex,
                });
                return;
            }
            bool ok = Core != null && GameActions.AddToElementPool(Core, P1, card.CoreCard, modeIndex);
            if (!ok) ShowToast("无法放入元素池（上限/资格）");
            RefreshLocal();
        }

        /// <summary>点我方地牌：横置产元素（多色指示物弹选色）。</summary>
        private void OnClickMyLand(BattleCardView card)
        {
            if (!GateMyMain()) return;
            if (card.IsTapped) { ShowToast("该地牌已横置"); return; }

            var colors = (card.Runtime?.RemainingLandTokens ?? Array.Empty<ManaEntryDTO>())
                .Where(t => t.Value > 0)
                .Select(t => (ManaType)t.ManaType)
                .Distinct()
                .ToList();
            if (colors.Count == 0) { ShowToast("该地牌已无指示物"); return; }

            if (colors.Count == 1)
            {
                TapLandFinal(card, colors[0]);
                return;
            }

            var options = new List<Tuple<string, Action>>();
            foreach (var c in colors)
            {
                var captured = c;
                options.Add(Tuple.Create<string, Action>($"产出 {BattleView.ManaZh(captured)} 元素", () =>
                {
                    CloseOverlay();
                    TapLandFinal(card, captured);
                }));
            }
            ShowPickOverlay("横置地牌", $"{card.Name}：选择要产出的元素颜色：", options);
        }

        private void TapLandFinal(BattleCardView card, ManaType type)
        {
            if (IsNetwork)
            {
                _net?.Send(NetworkMessageType.IntentTapForElement, new MsgIntentTapForElement
                {
                    CardRuntimeId = card.RuntimeId,
                    ManaType = (int)type,
                });
                return;
            }
            var core = Core;
            if (core == null) return;
            var pooled = core.ElementPool.GetPooledCards(P1)?
                .FirstOrDefault(l => l?.SourceCard == card.CoreCard);
            if (pooled == null) { ShowToast("找不到该地牌"); return; }
            if (!GameActions.GainElementFromToken(core, P1, pooled, type))
                ShowToast("无法横置产元素");
            RefreshLocal();
        }

        /// <summary>点我方单位：攻击目标选择（对手角色+对方单位）→ 攻击宣言。</summary>
        private void OnClickMyUnit(BattleCardView card)
        {
            if (!GateMyMain()) return;
            if (card.IsTapped) { ShowToast("该单位已横置"); return; }
            if (card.Runtime != null && card.Runtime.Power <= 0) { ShowToast("该单位攻击力为 0"); return; }

            var options = new List<Tuple<string, Action>>();

            if (IsNetwork)
            {
                int mySeat = _view != null && _view.NetViewerSeat >= 0 ? _view.NetViewerSeat : 0;
                int oppSeat = 1 - mySeat;
                options.Add(Tuple.Create<string, Action>("对手角色", () =>
                {
                    CloseOverlay();
                    SendAttackIntent(card, new NetEntityRef { Seat = oppSeat, IsPlayer = true });
                }));
                foreach (var unit in (_view.OppUnits ?? new List<BattleCardView>())
                             .Where(u => !u.IsDead))
                {
                    var captured = unit;
                    options.Add(Tuple.Create<string, Action>($"{captured.Name} [{captured.StatsText}]", () =>
                    {
                        CloseOverlay();
                        SendAttackIntent(card, NetRefOf(captured, oppSeat));
                    }));
                }
            }
            else
            {
                var core = Core;
                var targets = new List<Entity> { P2 };
                targets.AddRange(core.ZoneManager.GetCards(P2, Zone.Battlefield).Cast<Entity>());
                foreach (var entity in targets)
                {
                    var captured = entity;
                    options.Add(Tuple.Create<string, Action>(EntityDisplay(captured), () =>
                    {
                        CloseOverlay();
                        if (!_ctrl.DeclareAttack(P1, card.CoreCard, captured))
                        {
                            ShowToast("无法对该目标攻击");
                            RefreshLocal();
                            return;
                        }
                        _ctrl.SettleResponseWindow().Forget();
                        RefreshLocal();
                    }));
                }
            }

            ShowPickOverlay("选择攻击目标", $"用 {card.Name} 攻击：", options);
        }

        private int MyNetSeat => _view != null && _view.NetViewerSeat >= 0 ? _view.NetViewerSeat : 0;

        private static NetEntityRef NetRefOf(BattleCardView card, int seat) => new NetEntityRef
        {
            RuntimeId = card.RuntimeId,
            Seat = seat,
            IsPlayer = false,
            CardId = card.CardId,
        };

        private void SendAttackIntent(BattleCardView attacker, NetEntityRef target)
        {
            _net?.Send(NetworkMessageType.IntentDeclareAttack, new MsgIntentDeclareAttack
            {
                Attacker = NetRefOf(attacker, MyNetSeat),
                Target = target,
            });
        }

        // ---- 中栏按钮 ----

        private void OnSkipStandby()
        {
            if (_gameEnded || _view == null || !_view.MyTurn || _view.Phase != PhaseType.Standby) return;
            if (IsNetwork)
                _net?.Send<object>(NetworkMessageType.IntentSkipStandby, null);
            else
            {
                GameActions.SkipElementPool(Core, P1);
                RefreshLocal();
            }
        }

        private void OnGraveyardPlay()
        {
            if (!GateMyMain()) return;

            if (IsNetwork)
            {
                // 网络侧：弹本地墓地候选（快照 ZoneCards 公开区）→ IntentPlayCard FromZone=Graveyard
                AppendLog("网络模式：点击手牌区操作，墓地使用=从墓地点选（快照公开区）", "log-line--system");
                ShowToast("网络模式墓地使用：待快照墓地明细面板（后续迭代）");
                return;
            }

            var grave = Core.ZoneManager.GetCards(P1, Zone.Graveyard);
            if (grave == null || grave.Count == 0) { ShowToast("墓地为空"); return; }

            var options = new List<Tuple<string, Action>>();
            foreach (var c in grave)
            {
                var captured = c;
                var view = new BattleCardView
                {
                    CardId = c.ID,
                    CoreCard = c,
                    Runtime = SerializableRuntimeCardState.FromCard(c),
                    Name = CardDisplayName(c),
                };
                var data = (c as CardWrapper)?.GetData();
                if (data != null)
                {
                    view.CostText = BattleView.CostTextOf(data.Cost);
                    view.SupertypeText = BattleView.SupertypeZh(data.Supertype);
                }
                options.Add(Tuple.Create<string, Action>(
                    $"{view.Name}（费 {view.CostText}）", () =>
                    {
                        CloseOverlay();
                        if (GetModeCount(view) >= 2) PromptModeThenPlay(view, fromGrave: true);
                        else PlayCardFinal(view, null, 0, fromGrave: true);
                    }));
            }
            ShowPickOverlay("墓地使用", "选一张牌，视为手牌中使用（每回合一次）：", options);
        }

        private void OnActivateSkill()
        {
            if (!GateMyMain()) return;
            if (IsNetwork)
            {
                ShowToast("网络模式技能发动：协议 intent 通道待增补");
                return;
            }
            ActivateSkillAsync().Forget();
        }

        private async UniTaskVoid ActivateSkillAsync()
        {
            bool ok = await GameActions.ActivateHeroSkill(Core, P1);
            if (!ok) ShowToast("无法发动技能（费用/横置/阶段）");
            else _ctrl.SettleResponseWindow().Forget(); // 无候选自动双 Pass，无害
            RefreshLocal();
        }

        private void OnPassPriority()
        {
            if (IsNetwork && _view != null && _view.StackNotEmpty && _view.MyPriority && !_gameEnded)
                _net?.Send<object>(NetworkMessageType.IntentPassPriority, null);
        }

        private async void OnEndTurn()
        {
            if (_gameEnded || _view == null || !_view.MyTurn) return;

            if (IsNetwork)
            {
                _net?.Send<object>(NetworkMessageType.IntentEndTurn, null);
                return;
            }

            GameActions.EndTurn(Core, P1);
            // 交给 AI 跑完 P2 的整个回合（战斗窗口处可暂停等人类响应弹窗），再统一刷新
            if (!_gameEnded && _ctrl.TurnPlayer == P2)
                await _ctrl.RunAiTurnAsync();
            RefreshLocal();
        }

        private void OnConcede()
        {
            if (_gameEnded) return;
            ShowChoiceOverlay("投降确认", "确定投降吗？本局将判负。", new List<string> { "确认投降", "取消" }, idx =>
            {
                CloseOverlay();
                if (idx != 0) return;
                if (IsNetwork)
                    _net?.Send<object>(NetworkMessageType.IntentConcede, null);
                else
                {
                    Core.EndGame(P2, GameOverReason.Concede);
                    RefreshLocal();
                }
            });
        }

        // ======================================== 本地响应窗口（发动弹窗·沿用旧语义） ========================================

        /// <summary>人类响应弹窗：候选列表 + 「跳过」行；返回所选或 null=Pass。</summary>
        private UniTask<ResponseOption> ShowResponsePopupAsync(Player holder, List<ResponseOption> options)
        {
            var tcs = new UniTaskCompletionSource<ResponseOption>();
            var labels = new List<string>(options.Select(o => o.Label)) { "跳过（让过优先权）" };
            ShowChoiceOverlay("响应窗口", $"{holder?.Name}：发动一个响应，或跳过", labels, idx =>
            {
                CloseOverlay();
                tcs.TrySetResult(idx >= options.Count ? null : options[idx]);
            });
            return tcs.Task;
        }

        // ======================================== 弹窗通用 ========================================

        private void ShowPickOverlay(string title, string hint, List<Tuple<string, Action>> options)
        {
            Q<Button>("overlay-cancel").text = "取消";
            Q<Label>("overlay-title").text = title;
            Q<Label>("overlay-hint").text = hint;
            var list = Q<ScrollView>("overlay-list");
            list.Clear();
            foreach (var opt in options)
            {
                var row = new VisualElement();
                row.AddToClassList("list-row");
                var label = new Label(opt.Item1);
                label.AddToClassList("list-row__name");
                row.Add(label);
                var captured = opt;
                row.RegisterCallback<ClickEvent>(_ => captured.Item2());
                list.Add(row);
            }
            Q<VisualElement>("overlay").style.display = DisplayStyle.Flex;
        }

        private void ShowChoiceOverlay(string title, string hint, List<string> options, Action<int> onPick)
        {
            var list = Q<ScrollView>("overlay-list");
            Q<Button>("overlay-cancel").text = "取消";
            Q<Label>("overlay-title").text = title;
            Q<Label>("overlay-hint").text = hint;
            list.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                var idx = i;
                var row = new VisualElement();
                row.AddToClassList("list-row");
                var label = new Label(options[idx]);
                label.AddToClassList("list-row__name");
                row.Add(label);
                row.RegisterCallback<ClickEvent>(_ => onPick(idx));
                list.Add(row);
            }
            Q<VisualElement>("overlay").style.display = DisplayStyle.Flex;
        }

        private void ShowOverlay(string title, string hint, List<Entity> unused, Action<Entity> unusedPick)
        {
            Q<Label>("overlay-title").text = title;
            Q<Label>("overlay-hint").text = hint;
            Q<ScrollView>("overlay-list").Clear();
            Q<VisualElement>("overlay").style.display = DisplayStyle.Flex;
        }

        private void CloseOverlay() => Q<VisualElement>("overlay").style.display = DisplayStyle.None;

        private void OnOverlayCancel()
        {
            if (_gameEnded) Manager.Back();
            else CloseOverlay();
        }

        private string EntityDisplay(Entity e)
        {
            if (e is Player p)
            {
                string who = p == P1 ? "我方" : "对手";
                return $"{who}角色（生命 {p.Life}）";
            }
            if (e is Card c) return $"{CardDisplayName(c)} [{c.GetPower()}/{c.GetLife()}]";
            return e.ToString();
        }

        private static string CardDisplayName(Card c)
        {
            if (c is CardWrapper w)
            {
                var d = w.GetData();
                return string.IsNullOrEmpty(d?.CardName) ? d?.ID ?? "?" : d.CardName;
            }
            return c != null ? c.ToString() : "?";
        }

        private void ShowToast(string message) => Q<Label>("lbl-toast").text = message;

        // ======================================== 战报 ========================================

        private void AppendLog(string text, string cls)
        {
            if (string.IsNullOrEmpty(text)) return;
            var list = Q<ScrollView>("log-list");
            if (list == null) return;

            var line = new Label(text);
            line.AddToClassList("log-line");
            if (!string.IsNullOrEmpty(cls)) line.AddToClassList(cls);
            list.Add(line);

            while (list.childCount > 300) list.RemoveAt(0);
            list.ScrollTo(line);
        }

        // ======================================== 声明期目标原子（本地；沿用旧语义） ========================================

        /// <summary>返回该卡第一个需玩家选择目标的原子配置；无则 null。</summary>
        private static AtomicEffectConfig FindTargetingAtomic(Card card, int modeIndex = 0)
        {
            if (!(card is CardWrapper wrapper)) return null;
            var defs = CardEffectConverter.ConvertAll(wrapper.GetData().Effects, wrapper.GetData().ID);
            foreach (var def in defs)
            {
                if (def.IsActivatedEffect) continue; // 施放即结算的才会自动跑

                if (def.Steps != null && def.Steps.Count > 0)
                {
                    foreach (var atomic in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, modeIndex))
                    {
                        var cfg = AtomicEffectTable.GetByType(atomic.Type);
                        if (cfg != null && cfg.GetTargetKindList().Count > 0)
                            return cfg;
                    }
                    continue;
                }

                foreach (var atomic in def.Effects)
                {
                    var cfg = AtomicEffectTable.GetByType(atomic.Type);
                    if (cfg != null && cfg.GetTargetKindList().Count > 0)
                        return cfg;
                }
            }
            return null;
        }

        /// <summary>抉择模式显示名（第一个 Choice 步骤的 label 列表；无则空表）。</summary>
        private static List<string> FindChoiceLabels(CardData data)
        {
            var labels = new List<string>();
            if (data?.Effects == null) return labels;
            foreach (var eff in data.Effects)
            {
                if (eff?.Steps == null) continue;
                foreach (var step in eff.Steps)
                {
                    if (step?.choices == null || step.choices.Count < 2) continue;
                    foreach (var c in step.choices)
                        labels.Add(c?.label);
                    return labels;
                }
            }
            return labels;
        }
    }
}
