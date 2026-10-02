using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using CardCore.Network;
using CardCore.Serialization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>对战模式。</summary>
    public enum BattleMode
    {
        /// <summary>本地 vs 极简 AI（GameCore 直连）。</summary>
        LocalAI,
        /// <summary>网络对局（快照渲染 + intent 上行）。</summary>
        Network,
        /// <summary>教学固定局（2026-10-02）：双方卡组顺序=摸牌序列（锁牌库序）、机器人逐回合剧本演出
        /// （ScriptedAi）、响应窗口全让过——渲染/交互与本地 AI 局完全同轨。</summary>
        Tutorial,
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
        public static int Port = 8090;
        public static string Nickname = "player";

        /// <summary>匹配界面移交的现成连接（非空=已进房+已提交卡组，跳过连接与 JoinRoom，
        /// 一次性消费防跨局残留）。null=调试直连路径（本屏自建连接）。</summary>
        public static NetGameClient Client;

        /// <summary>教学局关卡 id（TutorialLibrary 条目；缺省 "basics" 基础教学）。
        /// 入口方接线：BattleEntry.Mode = BattleMode.Tutorial 后 Show&lt;BattleScreen&gt;。</summary>
        public static string TutorialId = "basics";
    }

    /// <summary>
    /// 对战界面（2026-10-02 战场定案 3D + 部分透明 UI 重做：旧 BattleUI.prefab 已删除、本屏为空屏桩，
    /// 原绑定/对局逻辑封存待 3D 层重接）。
    /// 历史口径（2026-10-01 预制体化，封存代码对照用）：静态层级来自 BattleUI.prefab，Build 深度按名
    /// 绑定；战场格子/手牌槽复用烘焙节点（按序对应棋盘 x 位），战报行/弹窗运行时构建。
    /// 本地 AI（GameCore/GameActions 直连）与网络（MsgGameStateSync 快照全量渲染 +
    /// NetEventBatch 中文战报 + intent 上行 + SelectRequest 反问弹窗 + PassPriority 让行）。
    /// 战场格子化：13×8 棋盘的双方 2×9 单位区 + 1×9 地牌行按 z 序纵向铺开、奇数行
    /// 半格偏移（odd-r 六角视觉）；本地格位=BoardState 权威，网络=ZoneCards 列表序
    /// 按 BoardLayout 同规重建（零协议改动）。卡面=ACard.prefab 挂进槽位
    /// （CardOverlayController 池化管理）。
    /// </summary>
    public sealed class BattleScreen : UIScreen
    {
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

        // UGUI 卡牌层绑定（卡面挂屏内槽位；RefreshView 全量重建收集）
        private readonly List<CardOverlayBinding> _cardBindings = new List<CardOverlayBinding>();

        // ---- 控件引用（Build 填充） ----
        private TMP_Text _lblMode, _lblToast;
        private TMP_Text _lblPhase, _lblStack, _lblPriority, _lblActivation;
        private Button _btnGrave, _btnSkill, _btnPass, _btnEnd, _btnConcede;
        private RectTransform _oppLands, _oppUnitsNear, _oppUnitsFar;
        private RectTransform _selfUnitsFar, _selfUnitsNear, _selfLands;
        private RectTransform _selfHand;
        private UiKit.Scroll _logList;
        private RectTransform _overlay, _selectorLayer;
        private UiKit.Modal _modal;                     // 当前通用弹窗（选择/模式/响应/胜负）
        private Button _overlayCancel;

        // 信息栏动态文本（FillBar 刷新）
        private TMP_Text _oppName, _oppLife, _oppDeck, _oppHand, _oppGrave, _oppFatigue, _oppBank;
        private TMP_Text _selfName, _selfLife, _selfDeck, _selfHandCount, _selfGrave, _selfFatigue, _selfBank;
        private TMP_Text _oppSkillLabel, _selfSkillLabel;
        private CanvasGroup _oppSkillGroup, _selfSkillGroup;

        private bool IsNetwork => BattleEntry.Mode == BattleMode.Network;
        private GameCore Core => _ctrl?.Core;
        private Player P1 => _ctrl?.P1;
        private Player P2 => _ctrl?.P2;

        // ======================================== 构建 ========================================

        // 2026-10-02 定案：战场改为 3D + 部分透明 UI 重做，旧 uGUI 预制体已删除（不具指导意义）。
        // 本屏退化为纯代码空屏桩（UIScreen 纯代码路径：Build 首行自建 Root）；
        // 原预制体绑定逻辑封存于 BindLegacyPrefabNodes、对局启动/刷新链全部保留，待 3D 层重接。
        protected override string RootName => "battle";

        protected override void Build()
        {
            Root = UiKit.Screen(RootName, Parent);
            UiKit.Button("btn-back", Root, "← 返回主菜单（战场 UI 待 3D 重做）", () => Manager.Back());
        }

        /// <summary>封存：2026-10-01 预制体化时代的 BattleUI.prefab 深度按名绑定（预制体已删）。
        /// 保留供 3D 层重接时对照字段清单与接线口径，勿删。</summary>
        private void BindLegacyPrefabNodes()
        {
            _overlay = FindOptional("overlay") ?? UiKit.Overlay("overlay", Root);
            _selectorLayer = FindOptional("selector-overlay") ?? UiKit.Overlay("selector-overlay", Root);

            // ---- 工具栏 ----
            BindButton("btn-back", OnBack);
            _lblMode = FindText("lbl-mode");
            _lblToast = FindText("lbl-toast");

            // ---- 对手信息栏（上）----
            _oppName = FindText("lbl-opp-name");
            _oppLife = FindText("lbl-opp-life");
            _oppDeck = FindText("lbl-opp-deck");
            _oppHand = FindText("lbl-opp-hand");
            _oppGrave = FindText("lbl-opp-grave");
            _oppFatigue = FindText("lbl-opp-fatigue");
            (_oppSkillLabel, _oppSkillGroup) = BindSkillChip("opp-skill");
            _oppBank = FindText("lbl-opp-bank");

            // ---- 战场六行（对手在上；odd-r 奇数行右偏半格）----
            _oppLands = Find("opp-lands");
            _oppUnitsNear = Find("opp-units-near");
            _oppUnitsFar = Find("opp-units-far");

            // ---- 中区：回合/阶段/栈/操作 ----
            _lblPhase = FindText("lbl-phase");
            _lblStack = FindText("lbl-stack");
            _lblPriority = FindText("lbl-priority");
            _lblActivation = FindText("lbl-activation");
            _btnGrave = BindButton("btn-grave-play", OnGraveyardPlay);
            _btnSkill = BindButton("btn-activate-skill", OnActivateSkill);
            _btnPass = BindButton("btn-pass-priority", OnPassPriority);
            _btnEnd = BindButton("btn-end-turn", OnEndTurn);
            _btnConcede = BindButton("btn-concede", OnConcede);

            _selfUnitsFar = Find("self-units-far");
            _selfUnitsNear = Find("self-units-near");
            _selfLands = Find("self-lands");

            // ---- 我方信息栏（下）----
            (_selfSkillLabel, _selfSkillGroup) = BindSkillChip("self-skill");
            _selfBank = FindText("lbl-self-bank");
            _selfFatigue = FindText("lbl-self-fatigue");
            _selfGrave = FindText("lbl-self-grave");
            _selfDeck = FindText("lbl-self-deck");
            _selfHandCount = FindText("lbl-self-hand");
            _selfLife = FindText("lbl-self-life");
            _selfName = FindText("lbl-self-name");

            // ---- 我方手牌条 / 战报栏（右）----
            _selfHand = Find("self-hand");
            _logList = FindScroll("log-list");
        }

        /// <summary>技能 chip 绑定（文本在子 label；CanvasGroup 缺失则补挂）。</summary>
        private (TMP_Text label, CanvasGroup group) BindSkillChip(string name)
        {
            var rt = Find(name);
            if (rt == null) return (null, null);
            var lbl = rt.GetComponentInChildren<TMP_Text>(true);
            var group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            return (lbl, group);
        }

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
            _modal = null;

            // 比赛禁联机（NetGate）：网络模式入口已全部屏蔽，此处防御性回落——
            // 万一 Mode 被残留置为 Network（如旧状态/外部注入），也按本地 AI 开局，绝不外连
            if (IsNetwork && !NetGate.OnlineEnabled)
                BattleEntry.Mode = BattleMode.LocalAI;

            if (_lblMode != null)
                _lblMode.text = IsNetwork ? $"网络 · {BattleEntry.Host}:{BattleEntry.Port}"
                    : BattleEntry.Mode == BattleMode.Tutorial ? $"教学 · {BattleEntry.TutorialId}"
                    : "本地 · AI";
            if (_btnPass != null)
                _btnPass.gameObject.SetActive(IsNetwork);

            if (IsNetwork)
                Debug.Log("[BattleScreen] 空屏桩（战场 UI 待 3D 重做，对局逻辑代码保留）——网络模式入口暂不可用");
            else
                Debug.Log("[BattleScreen] 空屏桩（战场 UI 待 3D 重做，对局逻辑代码保留）——本地模式未启动对局");
        }

        public override void OnExit()
        {
            _running = false;
            _net?.Close();
            _net = null;

            // 本地局棋盘接线归零（静态扩展点不清理会跨界面残留——验证器 S5 锁定此口径）
            _ctrl?.Shutdown();
            _ctrl = null;

            // 仅在仍是本界面注册时解除，避免覆盖其它界面的注册
            if (TargetSelectionService.Current is UiTargetSelector)
                TargetSelectionService.Current = null;
            if (ResponseWindowService.HumanResponder == ShowResponsePopupAsync)
                ResponseWindowService.HumanResponder = null;

            // UGUI 卡牌层回收（卡面独立于界面树池化，切屏必须显式清）
            CardOverlayController.ClearActive();
        }

        private void OnBack() => Manager.Back();

        // ======================================== 本地模式 ========================================

        private void StartLocal()
        {
            _ctrl = new BattleController();
            if (BattleEntry.Mode == BattleMode.Tutorial && !StartTutorialGame(_ctrl))
                _ctrl.StartNewGame(); // 教学配置缺失等异常回落普通局（不炸界面）

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
            TargetSelectionService.Current = new UiTargetSelector(_selectorLayer);
            // 响应窗口（发动弹窗）：人类候选→简版选择弹窗
            ResponseWindowService.HumanResponder = ShowResponsePopupAsync;

            RefreshLocal();
        }

        /// <summary>教学固定局开局（2026-10-02）：TutorialConfig 指定双方卡组顺序（=摸牌序列，锁牌库序）
        /// 与机器人逐回合剧本（ScriptedAi 按条演出），rngSeed 钉效果随机；机器人响应窗口全让过
        /// （EnablePassiveAiResponder——永不反制玩家）。返回 false=配置缺失回落普通局。</summary>
        private bool StartTutorialGame(BattleController ctrl)
        {
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            if (tutorial == null)
            {
                ShowToast($"教学配置缺失：{BattleEntry.TutorialId}（回落本地 AI 局）");
                return false;
            }
            var playerDeck = TutorialLibrary.BuildDeck(tutorial.playerDeck);
            var aiDeck = TutorialLibrary.BuildDeck(tutorial.aiDeck);
            if (playerDeck.Count < GameCore.OpeningHandSize || aiDeck.Count < GameCore.OpeningHandSize)
            {
                ShowToast("教学卡组配置不足（回落本地 AI 局）");
                return false;
            }
            ctrl.StartNewGame(playerDeck, aiDeck, new ScriptedAi(tutorial),
                lockDeckOrder: true, rngSeed: tutorial.rngSeed);
            ctrl.EnablePassiveAiResponder();
            return true;
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
            ShowOverlay(win ? "胜利" : "失败", $"{(win ? "我方" : "对手")}获胜（{e.Reason}）。");
            SetCancelText("返回主菜单");
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
                    ShowOverlay("连接失败", $"{BattleEntry.Host}:{BattleEntry.Port} — {ex.Message}");
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

        private int _lastViewerSeat;

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
                    ShowOverlay(win ? "胜利" : "失败", "对局结束。");
                    SetCancelText("返回主菜单");
                }
            }
        }

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
                        picked = await new UiTargetSelector(_selectorLayer).SelectIndicesAsync(
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

            _cardBindings.Clear(); // 卡牌层绑定随全量重建收集（BuildHexRow/BuildHand 填充）

            // 观众席记录（网络事件文本用）
            if (IsNetwork && _view.NetViewerSeat >= 0) _lastViewerSeat = _view.NetViewerSeat;

            // 信息栏
            FillBar(d.Opp, opp: true);
            FillBar(d.Self, opp: false);

            // 战场（行序=棋盘 z 序；对手在上）
            BuildHexRow(_oppLands, d.OppLands, z: 1, mine: false, isLand: true);
            BuildHexRow(_oppUnitsNear, d.OppUnits, z: 2, mine: false, isLand: false);
            BuildHexRow(_oppUnitsFar, d.OppUnits, z: 3, mine: false, isLand: false);
            BuildHexRow(_selfUnitsFar, d.SelfUnits, z: 4, mine: true, isLand: false);
            BuildHexRow(_selfUnitsNear, d.SelfUnits, z: 5, mine: true, isLand: false);
            BuildHexRow(_selfLands, d.SelfLands, z: 6, mine: true, isLand: true);

            // 中栏
            _lblPhase.text = d.PhaseText;
            _lblStack.text = string.Join(" ｜ ", d.StackLines);
            _lblPriority.text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";
            _lblActivation.text = d.ActivationTexts.Count > 0 ? string.Join("；", d.ActivationTexts) : "";

            // 我方手牌
            BuildHand(d);

            UpdateButtons(d);

            // 卡牌层挂载：卡实例直接挂进本视图刚建好的槽位
            CardOverlayController.Instance.Bind(_cardBindings);
        }

        private void FillBar(BattlePlayerView p, bool opp)
        {
            string aiTag = p.IsAI ? "（AI）" : "";
            if (opp)
            {
                _oppName.text = $"对手{aiTag}";
                _oppLife.text = $"生命 {p.Life}/{p.MaxHealth}";
                _oppDeck.text = $"牌库 {p.DeckCount}";
                _oppHand.text = $"手牌 {p.HandCount}";
                _oppGrave.text = $"墓地 {p.GraveyardCount}";
                _oppFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                _oppBank.text = $"地牌上限 {p.LandCap} · 元素池 {p.BankText}";
                if (_oppSkillLabel != null)
                    _oppSkillLabel.text = p.Skill != null
                        ? $"技能：{p.Skill.Name}{(p.Skill.IsTapped ? "（已横置）" : "")} {p.Skill.CostText}"
                        : "技能：—";
                if (_oppSkillGroup != null)
                    _oppSkillGroup.alpha = p.Skill != null && p.Skill.IsTapped ? 0.45f : 1f;
            }
            else
            {
                _selfName.text = "我方";
                _selfLife.text = $"生命 {p.Life}/{p.MaxHealth}";
                _selfDeck.text = $"牌库 {p.DeckCount}";
                _selfHandCount.text = $"手牌 {p.HandCount}";
                _selfGrave.text = $"墓地 {p.GraveyardCount}";
                _selfFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                _selfBank.text = $"地牌上限 {p.LandCap} · 元素池 {p.BankText}";
                if (_selfSkillLabel != null)
                    _selfSkillLabel.text = p.Skill != null
                        ? $"技能：{p.Skill.Name}{(p.Skill.IsTapped ? "（已横置）" : "")} {p.Skill.CostText}"
                        : "技能：—";
                if (_selfSkillGroup != null)
                    _selfSkillGroup.alpha = p.Skill != null && p.Skill.IsTapped ? 0.45f : 1f;
            }
        }

        /// <summary>一行 9 格（x=2..10）：复用预制体烘焙 cell（按序对应 x，保留用户样式调整）；
        /// 不足补建、多余销毁。空格=底座，有卡则卡实例挂满该格。</summary>
        private void BuildHexRow(RectTransform row, List<BattleCardView> cards, int z, bool mine, bool isLand)
        {
            if (row == null) return;
            float w = isLand ? 76f : 96f;
            float h = isLand ? 62f : 74f;
            EnsureChildCount(row, 9, "cell", w, h);

            for (int i = 0; i < row.childCount; i++)
            {
                var cell = (RectTransform)row.GetChild(i);
                ClearChildren(cell); // 清烘焙残留；卡面实例随后由 CardOverlayController 挂入

                var card = cards.FirstOrDefault(c => c.X == i + 2 && c.Z == z);
                if (card == null) continue;

                // 点击语义不变（我方单位=攻击开窗、我方地牌=产元素；对方卡不可点）
                var captured = card;
                _cardBindings.Add(new CardOverlayBinding
                {
                    Slot = cell,
                    Item = CardOverlayItem.FromBattle(captured,
                        isLand ? CardOverlayLayout.Land : CardOverlayLayout.Compact),
                    Layout = isLand ? CardOverlayLayout.Land : CardOverlayLayout.Compact,
                    OnClick = mine
                        ? (isLand ? (Action)(() => OnClickMyLand(captured)) : () => OnClickMyUnit(captured))
                        : null,
                });
            }
        }

        /// <summary>手牌条：复用预制体烘焙 hand-slot（数量随手牌增删——不足补建、多余销毁）。</summary>
        private void BuildHand(BattleViewData d)
        {
            if (_selfHand == null) return;
            int count = d.SelfHand?.Count ?? 0;
            EnsureChildCount(_selfHand, count, "hand-slot", 132f, 184f);

            for (int i = 0; i < count; i++)
            {
                var slot = (RectTransform)_selfHand.GetChild(i);
                ClearChildren(slot);
                var captured = d.SelfHand[i];
                _cardBindings.Add(new CardOverlayBinding
                {
                    Slot = slot,
                    Item = CardOverlayItem.FromBattle(captured, CardOverlayLayout.Full),
                    Layout = CardOverlayLayout.Full,
                    OnClick = () => OnClickHandCard(captured),
                });
            }
        }

        /// <summary>子节点数量对齐（复用预制体烘焙子物体；多余摘父销毁、缺失代码补建默认样式）。</summary>
        private static void EnsureChildCount(RectTransform parent, int count, string childName, float w, float h)
        {
            while (parent.childCount > count)
            {
                var last = parent.GetChild(parent.childCount - 1);
                last.SetParent(null);
                UnityEngine.Object.Destroy(last.gameObject);
            }
            while (parent.childCount < count)
            {
                var rt = UiKit.Node(childName, parent);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = UiKit.RoundedSprite;
                img.type = Image.Type.Sliced;
                img.color = UiStyle.CellBg;
                var ol = rt.gameObject.AddComponent<Outline>();
                ol.effectColor = new Color(38f / 255f, 42f / 255f, 51f / 255f, 1f);
                ol.effectDistance = Vector2.one;
                UiKit.Size(rt, w: w, h: h);
            }
        }

        private void UpdateButtons(BattleViewData d)
        {
            bool myMain = d.MyTurn && d.Phase == PhaseType.Main && !_gameEnded;

            _btnGrave.interactable = myMain && d.Self.GraveyardCount > 0;
            // 网络协议无技能 intent 通道（IntentActivateEffect 寻址卡面效果定义，技能卡不挂原子）——本地可用，网络暂禁
            _btnSkill.interactable = !IsNetwork && myMain && !_gameEnded;
            _btnPass.interactable = IsNetwork && d.StackNotEmpty && d.MyPriority && !_gameEnded;
            _btnEnd.interactable = d.MyTurn && !_gameEnded;
            _btnConcede.interactable = !_gameEnded;
        }

        // ======================================== 操作派发（本地直调 GameActions / 网络转 intent） ========================================

        private bool GateMyMain()
        {
            if (_gameEnded || _view == null) return false;
            if (!_view.MyTurn || _view.Phase != PhaseType.Main)
            {
                // 静默 return 会让点击毫无反馈（联机反馈"使用糊"的主因之一）——给一句可读提示
                ShowToast(!_view.MyTurn ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return false;
            }
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
                    // 自动横置口径（2026-09-30）：bank 不足但地牌可产所需元素亦放行——付费步自动横置
                    if (core != null && !core.ElementPool.CanPayCostWithAutoTap(
                            GameActions.GetCardCost(card.CoreCard, idx), P1))
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
        // （跳过准备阶段按钮已删：准备阶段纯自动推进——TurnEngine.StartNewTurn 内
        //   AdvanceFromStandby 直调，2026-09-24 定案；UI 不再有停等入口。）

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
            _modal?.Close();
            _modal = UiKit.ModalBox(_overlay, title, width: 460f, height: 460f);
            UiKit.Label("hint", _modal.Panel, hint, UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            var list = UiKit.ScrollColumn("list", _modal.Panel, spacing: 4f);
            UiKit.Size(list.Rect.transform, fw: 1f, fh: 1f);
            foreach (var opt in options)
            {
                var captured = opt;
                AddOverlayRow(list.Content, opt.Item1, () => { captured.Item2(); });
            }
            AddCancelButton();
        }

        private void ShowChoiceOverlay(string title, string hint, List<string> options, Action<int> onPick)
        {
            _modal?.Close();
            _modal = UiKit.ModalBox(_overlay, title, width: 460f, height: 460f);
            UiKit.Label("hint", _modal.Panel, hint, UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            var list = UiKit.ScrollColumn("list", _modal.Panel, spacing: 4f);
            UiKit.Size(list.Rect.transform, fw: 1f, fh: 1f);
            for (int i = 0; i < options.Count; i++)
            {
                var idx = i;
                AddOverlayRow(list.Content, options[idx], () => onPick(idx));
            }
            AddCancelButton();
        }

        private void ShowOverlay(string title, string hint)
        {
            _modal?.Close();
            _modal = UiKit.ModalBox(_overlay, title, width: 460f, height: 260f);
            UiKit.Label("hint", _modal.Panel, hint, UiStyle.SmallSize, UiStyle.TextDim, wrap: true);
            AddCancelButton();
        }

        private void AddCancelButton()
        {
            var bar = UiKit.Row("bar", _modal.Panel, spacing: 8f);
            var spacer = UiKit.Label("spacer", bar, "");
            UiKit.Size(spacer, fw: 1f);
            _overlayCancel = UiKit.Button("overlay-cancel", bar, "取消", OnOverlayCancel, height: 34f);
        }

        private void SetCancelText(string text)
        {
            if (_overlayCancel != null)
            {
                var lbl = _overlayCancel.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = text;
                var le = _overlayCancel.GetComponent<LayoutElement>();
                if (le != null) le.preferredWidth = lbl.preferredWidth + 28f;
            }
        }

        private void AddOverlayRow(RectTransform content, string text, Action onClick)
        {
            var row = UiKit.Node("row", content);
            var img = row.gameObject.AddComponent<Image>();
            img.sprite = UiKit.RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = UiStyle.RowBg;
            var lbl = UiKit.Label("label", row, text, UiStyle.BodySize, UiStyle.TextBody,
                TextAnchor.MiddleLeft);
            UiKit.StretchInset(lbl.rectTransform, 10f, 4f);
            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            UiKit.Size(row, h: 38f);
        }

        private void CloseOverlay()
        {
            _modal?.Close();
            _modal = null;
            _overlayCancel = null;
        }

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

        private void ShowToast(string message) => _lblToast.text = message;

        // ======================================== 战报 ========================================

        private void AppendLog(string text, string cls)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_logList?.Content == null) return;

            var (color, style) = cls switch
            {
                "log-line--turn" => (UiStyle.LogTurn, FontStyle.Bold),
                "log-line--combat" => (UiStyle.LogCombat, FontStyle.Normal),
                "log-line--system" => (UiStyle.LogSystem, FontStyle.Normal),
                _ => (UiStyle.LogBase, FontStyle.Normal),
            };
            var line = UiKit.Label("line", _logList.Content, text, UiStyle.MiniSize, color, style: style, wrap: true);
            UiKit.Size(line, fw: 1f);

            while (_logList.Content.childCount > 300)
            {
                var first = _logList.Content.GetChild(0);
                first.SetParent(null);
                UnityEngine.Object.Destroy(first.gameObject);
            }
            _logList.ScrollToBottom();
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
    }
}
