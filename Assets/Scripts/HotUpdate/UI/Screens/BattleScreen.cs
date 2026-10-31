using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using CardCore.Network;
using CardCore.Serialization;
using GameBoard;
using Tide.HotUpdate;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using TMPro;

namespace SynergyUI
{
    // BattleMode/BattleEntry 已出文件（2026-10-18 功能拆分）：Battle/BattleEntry.cs。

    /// <summary>
    /// 对战界面（2026-10-02 战场定案 3D + 部分透明 UI 重做：旧 BattleUI.prefab 已删除；
    /// 2026-10-03 本地模式先以「双扇手牌 + 结束回合」过渡面运行——HandFanView 自原型屏移植接入，
    /// 出牌交互与原绑定/对局逻辑封存待 3D 层重接；同日过渡面预制体化：静态层级回归
    /// Assets/Art/Prefab/BattleUI.prefab（Tools/验证/对战UI/BattleUI预制体装配 幂等重建），
    /// Build=按名绑定（2026-10-07 兜底退役：缺节点 LogError 跳过，不代码补建）。
    /// 历史口径（2026-10-01 预制体化，封存代码对照用）：静态层级来自 BattleUI.prefab，Build 深度按名
    /// 绑定；战场格子/手牌槽复用烘焙节点（按序对应棋盘 x 位），战报行/弹窗运行时构建。
    /// 本地 AI（GameCore/GameActions 直连）。2026-10-18 网络簇整删：NetGate 禁联机期网络链为
    /// 休眠死代码（StartNetAsync 全工程零调用、OnEnter 网络分支空屏桩）——引擎协议在
    /// CardCore.Network 原样保留，复活走第二轮决策管线按新架构重写客户端接线，git 历史可回取。
    /// 战场=3D 棋盘（BattleFieldView 对账渲染）；手牌=扇区（HandFanView·ACard 实例只出自
    /// 扇区/Region 列卡/prefab 预览位——旧 2D 格子卡牌层挂载链 2026-10-14 晚拆除）。
    /// </summary>
    public sealed class BattleScreen : UIScreen
    {
        private BattleController _ctrl;                 // 本地模式（2026-10-18 网络簇整删后唯一模式）
        private BattleViewData _view => _projector?.View; // 当前视图（转发投影器——2026-10-18 B2 所有权单点）
        private bool _running;
        private bool _gameEnded;

        // ---- 控件引用（Build 填充） ----
        private TMP_Text _lblMode;
        private readonly BattleToastHub _toast = new BattleToastHub(); // toast 双层横幅+error-tip（2026-10-18 拆分·Battle/BattleToastHub.cs）
        private BattleLogController _log;                              // 战报控制器（2026-10-18 拆分·Battle/BattleLogController.cs；Build 惰性建存留跨局）
        private TMP_Text _lblPhase, _lblStack, _lblPriority;
        private Button _btnConcede;
        // 英雄区/临时元素（2026-10-16 技能格撤销定案）：技能格退役——技能发动=角色格【发动】键
        //（OnClickHeroZone 链路）、技能介绍=己方【发动】键悬停（对手无键→对手角色格悬停兜底）、角色生命/弹药=
        // 角色格悬停与格前三键（黑白元素格已随 2026-10-28 元素缩编退役）。
        // 2026-10-14 晚：旧 2D 棋盘六行/手牌条字段退役（战场=3D 层、手牌=扇区）——
        // CardOverlayController 卡面挂载链随行拆除，本屏不再产生扇区外的 ACard 实例。
        // 旧 overlay/modal 现搭弹窗层（_overlay/_selectorLayer/_modal/_overlayCancel）已随 2026-10-16
        // 定案整删：对局中目标/效果/模式抉择走 Region 面板卡牌列表（PickRegionOptionAsync/
        // RegionTargetSelector），提示类弹窗（胜局结算/败局重试/投降确认）复用 TipPanel（2026-10-17
        // 定案：非对局中目标选择不用 Region），终局提示走 toast+战报——全屏遮罩拦棋盘点选
        //（守卫窗口卡死）的坑不再有。

        // ---- 手牌扇形过渡面（2026-10-03 本地对战接入：原型屏 HandFanScreen 移植为本屏本地呈现） ----
        private HandFanView _selfFan, _oppFan;          // 双方手牌扇区（对账键=引擎卡 RuntimeId）
        private bool _fanFlyArmed;                      // 抽卡飞入武装（2026-10-17）：GameStartEvent 起武装——起手抽牌不飞、首个回合自然抽即飞

        // ---- 战场 3D 摆位（2026-10-13 战场重做·原型）：消费 BattleViewData 幂等对账（本地/网络同轨） ----
        private BattleFieldView _field;
        private CombatChoreographer _choreo;         // 战斗编舞（2026-10-13 攻击演出：3D 寻路箭头选目标+走位/反击/死亡漩涡）
        private BattleCellPicker _cellPicker;        // 通用选格器（2026-10-14 手牌出牌接线：放地色格/永久物空格箭头选择）

        /// <summary>最近一次实付账单（seat→每色实付，2026-10-15 出场粒子颜色口径）：
        /// ElementPoolPayEvent 捕获 → CastPlayed 入场时组装配荷。触发器付费插队属已知近似（颜色可能串）。</summary>
        private readonly Dictionary<int, Dictionary<int, float>> _lastPaidBySeat = new Dictionary<int, Dictionary<int, float>>();

        /// <summary>永久物出牌时玩家所选落位格（RuntimeId→格）：入场事件补钉
        ///（BoardState.Resync 尾清不在场钉——不能先钉后出牌）；回合切换清残单。</summary>
        private readonly Dictionary<uint, (int x, int z)> _pendingPins = new Dictionary<uint, (int x, int z)>();


        // ---- 棋盘指针/区域面板/日志开关（2026-10-13 基础教学三问题：悬停信息、Region 列卡、btn-log） ----
        // 悬浮完整卡面退役（2026-10-15 用户删除 BattleUI.prefab 的 ACard 节点）：场上卡（生物/结界/角色技能）
        // 悬停信息改走 hover-info 四段文本（主动/自动/关键词/状态，见 CardHoverInfo）
        private int _viewVersion => _projector?.ViewVersion ?? 0; // 战场视图版本（转发投影器）——悬停缓存失效源
        private BattleHoverInfoProvider _hoverProv; // 悬停文案族（2026-10-18 拆分·Battle/BattleHoverInfoProvider.cs）
        private BattleAiCastStage _castStage;        // AI 出牌威胁箭头（2026-10-18 拆分·Battle/BattleAiCastStage.cs；左上角卡面 2026-10-19 退役归链舞台）
        private BattleHudTicker _hudTicker;          // HUD 时间/速度/产量格（2026-10-18 拆分·Battle/BattleHudTicker.cs）
        private BattleTutorialDirector _tutorial;     // 教学导演（2026-10-18 拆分·Battle/BattleTutorialDirector.cs）
        private Button _tutorialReset;                // 常驻考验重置（btn-tretry·2026-10-19 教学局专用）
        private BattleProjector _projector;        // 唯一投影器（2026-10-18 B2 拆分·Battle/BattleProjector.cs）
        private BattleDecisionHub _decision;       // 决策中枢：守卫窗/响应窗/不连锁三义键（2026-10-18 B3 拆分·Battle/BattleDecisionHub.cs）
        private BattleChainStage _chainStage;      // 连锁链舞台：chain 节点克隆卡面（2026-10-19·Battle/BattleChainStage.cs）
        private BattleResolutionDirector _director; // 逆结算步进回放导演（2026-10-19·Battle/BattleResolutionDirector.cs）
        private BattleOpeningDirector _opening;     // 五段式开局导演（2026-10-19 掷币演出挪战场·Battle/BattleOpeningDirector.cs）

        // ---- 网络模式（2026-10-21 P2P 复活）：本屏不建 BattleController——快照驱动渲染 + intent 上行 ----
        // 房主侧引擎也只归服务器泵（NetSessionServer.Current），UI 一律走客户端路径（座位过滤防全信息泄漏）。
        private NetGameClient _netClient;           // 网络局客户端（OnEnter 消费 BattleEntry.Client 一次性移交）
        private MsgGameStateSync _netSnapshot;      // 最近快照（网络反问候选/守卫栈条目查询用）
        private string[] _netSubmittedDeckIds;      // 网络五段式段五提交的卡组（manifest 对账用，2026-10-28）
        private string[] _netSubmittedTokenIds;     // P9b 提交的衍生物区（摘要对账用；随机预组=null）
        private List<DeckTokenBinding> _netSubmittedTokenBindings; // P9b 提交的槽绑定（摘要对账用）
        private bool NetActive => _netClient != null && !_netClient.Disconnected;

        /// <summary>排队全量刷扇（转发投影器置脏——冲刷在 Updater 帧首执行）。</summary>
        private void QueueRefreshFans() => _projector.QueueFans();

        /// <summary>排队全量对账（转发投影器置脏）。</summary>
        private void QueueRefreshField() => _projector.QueueField();

        private void FlushRefreshQueues() => _projector?.Flush(RefreshFans);

        // 信息栏动态文本（FillBar 刷新）
        private TMP_Text _oppName, _oppLife, _oppDeck, _oppHand, _oppGrave, _oppFatigue, _oppBank;
        private TMP_Text _selfName, _selfLife, _selfDeck, _selfHandCount, _selfGrave, _selfFatigue, _selfBank;

        private GameCore Core => _ctrl?.Core;
        private Player P1 => _ctrl?.P1;
        private Player P2 => _ctrl?.P2;

        // ======================================== 构建 ========================================

        // 2026-10-02 定案：战场改为 3D + 部分透明 UI 重做，旧 uGUI 预制体已删除（不具指导意义）。
        // 2026-10-03 过渡面预制体化：静态层级回归 Assets/Art/Prefab/BattleUI.prefab
        // （Tools/验证/对战UI/BattleUI预制体装配 幂等重建），Build=按名绑定（2026-10-07 起缺节点不补建）；
        // 原预制体绑定逻辑封存于 BindLegacyPrefabNodes、对局启动/刷新链全部保留，待 3D 层重接。
        protected override string PrefabName => "BattleUI";
        protected override string RootName => "battle";

        protected override void Build()
        {
            // 战场=3D 棋盘可见（2026-10-03 定案：UI 不挡世界相机）——底图只留视觉透明；
            // raycastTarget 必须关（2026-10-13 3D 点击修复）：全屏透明 Image 会令
            // EventSystem.IsPointerOverGameObject 恒真，棋盘点击（HandleFieldClick）全被它挡死
            var bg = Root.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0f);
                bg.raycastTarget = false;
            }

            // 返回主菜单（2026-10-14 晚定案）：btn-back 退役——退出对局走 btn-concede 认输结算
            // （2026-10-16 弹窗退役补：终局后认输键改「返回主菜单」直回——旧弹窗取消键职责迁移）；
            // 结束回合（2026-10-14 晚退役）：btn-end-turn 删除——现役入口=棋盘 hud-tile-end-turn
            // （HandleFieldClick→OnEndTurnFan，仅己方回合可点+引擎闸兜底）；
            // overlay 弹层（2026-10-16 定案整删）：prefab 本无该节点——此前是 UiKit.Overlay 代码
            // 现搭的全屏遮罩层，现搭弹窗全部退役（选择/响应走 Region 面板，终局走 toast+战报）

            // 英雄区/临时元素（2026-10-14 定案退役·2026-10-16 技能格撤销后口径）：btn-self-hero 按钮
            // 与 temp-elements 指示条不再构建——技能发动=角色格【发动】键、角色信息=角色格悬停，
            // 黑白钱包=地牌行黑白元素格。

            // 悬停信息位（2026-10-13 基础教学定案：toast 节点不要——区域/功能格说明与操作拒绝
            // 原因统一悬停就地展示）：2026-10-14 晚 prefab 化——节点与摆位归 prefab（hover-info+lbl）。
            // 2026-10-18 拆分：悬停文案族（面板+全部文案构建+源键缓存）收进 BattleHoverInfoProvider
            _hoverProv = new BattleHoverInfoProvider(() => _view, () => Core, () => P1, () => P2,
                () => _viewVersion, DataOf);
            _hoverProv.Bind(FindOptional("hover-info")); // 缺节点=悬停文本静默不显
            _decision = new BattleDecisionHub(() => _view, () => Core, () => P1, () => P2, () => _field, _toast,
                hitName => _hoverProv.Handle(hitName),
                text => _tutorial.RefreshBanner(text),
                () => _regionSelector, CardDisplayName,
                () => $"targeting={(_choreo != null && _choreo.IsTargeting)} picker={(_cellPicker != null && _cellPicker.Active)} overUI={BattlePointer.OverUI} view={_view != null} cam={BattleStageDirector.WorldCam != null}");
            _castStage = new BattleAiCastStage(() => Root, () => _view, () => _field, () => P1, _decision.UnitAnchorOf);
            _chainStage = new BattleChainStage(); // 连锁链（2026-10-19）：chain 节点+模板已在 prefab，Bind 只定位隐藏
            _chainStage.Bind(Root);
            // 逆结算步进回放导演（2026-10-19）：先于投影器构造——投影器钩子要绑它的入口；
            // 依赖全走 Func 惰性取（_log/_choreo 等在其构建期可能尚未就绪）
            _director = new BattleResolutionDirector(() => Core, () => _projector, () => _log, () => _field,
                () => _chainStage, () => _choreo, RefreshFans);
            _hudTicker = new BattleHudTicker(() => _view);
            _tutorial = new BattleTutorialDirector(() => Root, () => Core, () => _ctrl, () => _field, _toast,
                ShowTipPromptAsync, () => Manager.Back(), RefreshField, RefreshFans, OnEndTurnFan,
                () => _gameEnded, v => _gameEnded = v,
                () => Manager.Replace<BattleScreen>(), // 教学三重开战场（2026-10-19）：弹栈重入=五段开场重放
                () => _director != null && _director.Busy); // 表现在途探针（2026-10-20 运势段被截修复）：回放导演 Busy
            _projector = new BattleProjector(() => Core, () => _ctrl, ApplyProjection, RefreshView,
                () => _fanFlyArmed = true, ArmDrawFlyIn,
                e => { if (e?.Card != null) _field?.InterruptCasting(e.Card.RuntimeId); },
                e => { if (e?.Player == null || _field == null) return; _field.HeroFatigueHitAsync(e.Player == P2 ? 1 : 0, e.Damage).Forget(); },
                CapturePaid, CaptureArrival, OnGameOverFan, () => _pendingPins.Clear(),
                _director.OnResolutionStart, _director.OnStepResolved, _director.OnRoundStackEmpty,
                (e, castCell) => _field?.PlayEffectDamageAsync(e.Source, e.Target, e.Damage, p => p == P2 ? 1 : 0, castCell, e.EffectTint).Forget(),
                e => _field?.PlayEffectHealAsync(e.Target, e.Amount - e.Overfill, p => p == P2 ? 1 : 0).Forget(),
                e => _field?.PlayCounterFloatAsync(e.Target, e.CounterType, e.Amount, p => p == P2 ? 1 : 0).Forget(),
                e => _field?.HoldForEffectDeath(e),
                (e, castCell) => _field?.PresentEffectDeathAsync(e.Source, e.DestroyedCard, e.Reason, p => p == P2 ? 1 : 0, castCell, e.EffectTint).Forget(),
                (e, castCell) => _field?.PlayEffectFlightAsync(e.Source, e.Target, p => p == P2 ? 1 : 0, castCell, e.Tint).Forget(),
                e => _field?.PlayClashResolvedAsync(e.EngineCard, e.MyPower, e.TheirPower, e.Threshold, e.Success, p => p == P2 ? 1 : 0).Forget(),
                e => _field?.PlayLuckRollAsync(e.EngineCard, e.D1, e.D2, e.Success, p => p == P2 ? 1 : 0).Forget(), // 运势掷骰：来源单位头顶浮字（投掷结果+通过与否·主动/自动两路同源）
                () => _log != null && _log.IsHeld, // 日志缓冲探针（2026-10-20「UI 真相源=日志」）：HP 读数按日志可见性即时/随步对账
                OnPhaseAnnounced, // 阶段播报（2026-10-26 阶段字事件序喂数）：PhaseStartEvent 载荷→阶段队列入队
                OnDeckMark); // 诅咒/祝福牌库标记（2026-10-26）：施加=卡堆顶爆发+脉冲；施加/消层都刷新牌库预览条

            // 悬浮完整卡面（ACard 预览）2026-10-15 退役：prefab 节点已删，场上卡详情=hover-info 四段文本

            // 双方手牌扇区（配置承自原型屏：底部己方全正面拱形弧、顶部对手缩小卡背扇）
            // 挂载层= prefab 节点（2026-10-14 晚定案：self/opp-fan-layer 烘焙进 BattleUI.prefab，
            // 层级序=用户可调；代码只查找不补建，缺层=对应扇不渲染）
            var selfLayer = FanLayer("self-fan-layer");
            if (selfLayer != null)
                _selfFan = new HandFanView(selfLayer, new HandFanView.Config
                {
                    Center = new Vector2(0f, -375f), // 卡心离屏底 ~165px（悬停抬起后不越界）
                    HoverScaleMul = 1.93f, // 悬停放大 1.38×1.4（2026-10-21 用户反馈仍看不清）——只调己方扇，对手卡背无放大需求
                    IdleSinkRatio = 0.65f, // 收缩再下沉（2026-10-21：收缩态仍压己方地牌池——配合数字/图标调换）
                });
            var oppLayer = FanLayer("opp-fan-layer");
            if (oppLayer != null)
                _oppFan = new HandFanView(oppLayer, new HandFanView.Config
                {
                    TopSide = true,
                    Center = new Vector2(0f, 375f),
                    BaseScale = 0.72f,
                    MaxFanWidth = 830f,
                    IdleSinkRatio = 0.65f, // 与己方扇同深（2026-10-21 对称收缩）——TopSide 自动上收镜像
                });

            // 旧版战场 UI 绑定复活（2026-10-13 补回未 3D 化部分——信息条/中栏/战报/overlay 弹层；
            // 重绑冲突已在函数内剔除，board 六行等 3D 化部分缺失=null 静默）
            BindLegacyPrefabNodes();

            // 战斗日志开关（2026-10-13 基础教学：日志改喂 log-list——btn-log 切换高度 0↔700。
            // 2026-10-18 拆分：订阅白名单/行渲染/开关动画收进 BattleLogController，本屏只 Bind+转发）
            if (_log == null) _log = new BattleLogController(p => p == P1); // 跨 Mount 存留（_logArmed 跨局口径与旧字段一致）
            _log.Bind(FindScroll("log-list"), FindOptional("log-list"));
            BindBattleButton("btn-log", "对局日志", _log.Toggle);

            // 不入连锁（2026-10-15）：守卫响应窗口让过口——2026-10-17 短暂挪入 Region 后挪回根级
            //（战斗拒绝连锁：守卫窗口 Region 关也要能点——点击=让过，等价右键/ESC），平时隐藏。
            // 网络局（2026-10-21）：让过=PassPriority intent（屏侧包装分发——本地/网络同键不同义）
            _decision.Bind(BindBattleButton("btn-notchain", "不连锁", OnNotChainClick)); // 三义按钮归决策中枢（2026-10-18 B3）

            // Region 区域面板骨架（prefab 用户节点：点棋盘墓地/除外/发动区/卡组/临时元素格打开——
            // 2026-10-18 拆分：骨架/开关/列卡池化收进 BattleRegionPanel，屏侧只留守卫转发）
            _regionPanelCtl = new BattleRegionPanel(() => Core, () => P1);
            _regionPanelCtl.Build(FindOptional("Region"));

            UiKit.Updater.Attach(Root, () =>
            {
                FlushRefreshQueues(); // 合帧冲刷（2026-10-18 性能）：事件订阅只置脏——连锁结算同帧多事件合并为一次对账
                _selfFan?.Tick();
                _oppFan?.Tick();
                _log.Tick();         // 日志面板高度动画（BattleLogController·2026-10-18 拆分）
                _toast.Tick();      // error-tip 滑入/到时收（BattleToastHub·2026-10-18 拆分）
                _director?.Tick();  // 步进回放看门狗（2026-10-19：超时无进展强制收口）
                TickTutorialResetButton(); // 常驻考验重置门控（2026-10-19）：显隐+可点性逐帧评估
                PollTutorialJumpKey();    // T 键测试跳段（2026-10-21）：教学局直跳最终考验
                PollFieldClearStep();     // 教学自由清场步（2026-10-19 教学二段一）：栈收口+表现播毕后空场即推进
                PollFieldPointer(); // 3D 棋盘悬停/点击（区域格/功能格/单位地牌统一入口——2026-10-13）
                _hudTicker.Tick();  // HUD 时间/速度/产量格（4Hz 节流；BattleHudTicker·2026-10-18 拆分）
                WatchdogStalePick(); // 选择/响应窗帧驱动看门狗（2026-10-17）：孤悬窗口强制收口——不依赖 UniTask.Delay
            });
        }

        /// <summary>选择/响应窗口看门狗（2026-10-17 去静态定案配套·MonoBehaviour 帧驱动）：
        /// Region 选择面开超 70s（墙钟 realtime）→ 强制收口。健康窗口不受影响（面板自身
        /// 60s 超时代选先到；本口只兜在途 await 链孤悬——那时 Delay 型自超时同样不会触发，
        /// 两度实测：连锁窗在途时退出对局→重开，窗口挂死、IsOpen 残留、全场点不动）。</summary>
        private void WatchdogStalePick()
        {
            var sel = _regionSelector;
            if (sel == null || !sel.IsOpen) return;
            float age = Time.realtimeSinceStartup - sel.OpenSinceRealtime;
            if (age <= 70f) return;
            Debug.Log($"[BattleScreen] 选择/响应窗开置 {age:F0}s 超时——看门狗强制收口（在途链孤悬）");
            sel.ForceIdle();
            _toast.Show("响应窗口超时——已自动让过");
        }

        /// <summary>按钮按名绑定（2026-10-07 兜底退役）：预制体节点接线点击（含 [UI点击] 日志）并回写文案
        ///（防烘焙文案漂移）；缺节点/缺 Button 组件 LogError 返回 null——不再代码补建。</summary>
        private Button BindBattleButton(string name, string text, Action onClick)
        {
            var node = Find(name);
            var btn = node != null ? node.GetComponentInChildren<Button>(true) : null;
            if (btn == null)
            {
                if (node != null) Debug.LogError($"[{GetType().Name}] 预制体节点无 Button 组件：{name}");
                return null; // 缺节点已由 Find 另报
            }
            var lbl = btn.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = text;
            btn.onClick.AddListener(() => { Debug.Log($"[UI点击] {name}"); onClick(); });
            return btn;
        }

        /// <summary>扇区挂载层单名绑定：prefab 烘焙节点（全屏铺开做卡牌画布）；
        /// 缺层 LogError 返回 null——对应手牌扇不渲染（Tick/SetCards 已空引用安全）。</summary>
        private RectTransform FanLayer(string name) => Find(name);

        /// <summary>静默版 FindText（可选控件——极简 prefab 缺失不 LogError，null 安全）。</summary>
        private TMP_Text FindTextOptional(string name)
        {
            var rt = FindOptional(name);
            return rt != null ? rt.GetComponentInChildren<TMP_Text>(true) : null;
        }

        /// <summary>旧版战场 UI 深度按名绑定（2026-10-13 复活：prefab 补回未 3D 化控件——用户定案
        /// "只管加，布局自调"）。与现役 Build 共存口径：btn-end-turn 不在此重绑
        /// （现役已绑，重复 AddListener 会双触发）；board 六行/self-hand/
        /// self-skill 不补（3D/扇面/英雄区已替代，FindOptional 缺失=null 静默）。</summary>
        private void BindLegacyPrefabNodes()
        {

            // ---- 工具栏（2026-10-13 基础教学：极简 prefab 大半控件已上盘/退役——全部改静默查找） ----
            _lblMode = FindTextOptional("lbl-mode");
            // lbl-toast 退役（2026-10-17 用户删节点：面板挪入 Region 更名 label-num=选择/响应提示位）：
            // 选择/响应提示由 RegionTargetSelector 直写 label-num（+计数）；其余 toast 文案（拒绝原因/
            // 守卫横幅/教学步骤/终局）无节点=2026-10-24 起兜底走 error-tip 顶部条（教学/守卫底层常驻、
            // 拒绝原因临时覆盖 2 秒到点回落——分流单点=BattleToastHub.RenderToast）。绑定保留 null 安全
            //（过渡 prefab 兼容——将来补回新横幅传回控件即自动切回横幅）
            // 2026-10-18 功能拆分：toast/error-tip 控件与渲染收进 BattleToastHub（本屏只 Bind）
            _toast.Bind(FindTextOptional("lbl-toast"), FindOptional("lbl-toast")?.gameObject, FindOptional("error-tip"));
            _tutorial.HookBanner(); // 教学步骤/守卫提示（横幅合并口径——BattleTutorialDirector·2026-10-18 拆分）

            // 常驻考验重置按钮（2026-10-19 用户定案：教学一败局重置升级为随时可点）：教学局非 Boss 课
            // 常显、Tick 逐帧门控可点性，点=回对应考验开始（BattleTutorialDirector.TryResetTrial）
            _tutorialReset = FindOptional("btn-tretry")?.GetComponent<Button>();
            if (_tutorialReset != null)
            {
                _tutorialReset.gameObject.SetActive(false);
                _tutorialReset.onClick.AddListener(OnTutorialResetClicked);
            }

            // ---- 对手信息栏（上）----
            _oppName = FindTextOptional("lbl-opp-name");
            _oppLife = FindTextOptional("lbl-opp-life");
            _oppDeck = FindTextOptional("lbl-opp-deck");
            _oppHand = FindTextOptional("lbl-opp-hand");
            _oppGrave = FindTextOptional("lbl-opp-grave");
            _oppFatigue = FindTextOptional("lbl-opp-fatigue");
            // opp-skill chip 退役（2026-10-14 晚）：对手技能=棋盘技能格/角色格悬停（ACard 卡面）
            _oppBank = FindTextOptional("lbl-opp-bank");

            // ---- 中区：回合/阶段/栈/操作 ----
            _lblPhase = FindTextOptional("lbl-phase");
            _lblStack = FindTextOptional("lbl-stack");
            _lblPriority = FindTextOptional("lbl-priority");
            // lbl-activation（发动区文本摘要）已随 2026-10-15 发动格拆除退役：施放中卡改类型图标上盘
            //（BattleFieldView 施放占格）；prefab 本无此节点，写入点先前即静默空转
            // btn-grave-play 绑定已随 2026-10-30 归土改版整删退役（墓地视手牌使用→回合末自动复活，
            // 无出牌入口；prefab 本无此节点，先前绑定即静默空转）
            // btn-pass-priority 绑定已随 2026-10-18 网络簇整删退役（让优先权=网络模式专属；prefab 节点留置不接线）
            _btnConcede = BindButton("btn-concede", () => OnConcede().Forget());

            // ---- 我方信息栏（下）----
            _selfBank = FindTextOptional("lbl-self-bank");
            _selfFatigue = FindTextOptional("lbl-self-fatigue");
            _selfGrave = FindTextOptional("lbl-self-grave");
            _selfDeck = FindTextOptional("lbl-self-deck");
            _selfHandCount = FindTextOptional("lbl-self-hand");
            _selfLife = FindTextOptional("lbl-self-life");
            _selfName = FindTextOptional("lbl-self-name");

            // ---- 战报栏（右）----
            // log-list 绑定已随 2026-10-18 拆分挪至 Build 的 BattleLogController.Bind
        }





        // ======================================== TipPanel 提示弹窗（2026-10-17 定案） ========================================
        // 提示类弹窗（胜局结算/败局重试/投降确认）统一复用 TipPanel 预制体；Region 面板只留给
        // 对局中的目标/效果/模式选择（非提示弹窗不占 Region——用户定案）。

        private GameObject _tipPromptPanel; // 当前提示实例（同屏单例：新提示顶替旧提示）
        private GameObject _tipPanelPrefab; // 惰性加载（TipPanel.prefab——教学导演侧另持一份引用，2026-10-18 拆分）
        private System.Threading.Tasks.TaskCompletionSource<bool?> _tipPromptTcs; // true=btn-ok / false=btn-no / null=未点即清（拆屏/顶替）

        /// <summary>TipPanel 提示弹窗：msg=正文，两键文案运行时填（noText=null 隐藏 btn-no 单键形态）。
        /// 返回 true=确定键、false=第二键；拆屏/被顶替/预制体缺失=null（调用方按取消口径处理）。</summary>
        private async UniTask<bool?> ShowTipPromptAsync(string msg, string okText, string noText)
        {
            if (_tipPanelPrefab == null)
                _tipPanelPrefab = HotUpdateAssets.Load<GameObject>("TipPanel", UiKit.PrefabPath("TipPanel"));
            if (_tipPanelPrefab == null)
            {
                Debug.LogWarning($"[BattleScreen] TipPanel.prefab 加载失败——提示弹窗不可用：{msg}");
                return null;
            }
            CloseTipPrompt();
            var panel = UnityEngine.Object.Instantiate(_tipPanelPrefab, Root, false);
            panel.name = "tip-prompt";
            var rt = panel.transform as RectTransform;
            var msgTmp = UiKit.FindDeep(rt, "msg")?.GetComponent<TMP_Text>();
            if (msgTmp != null) msgTmp.text = msg;
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool?>();
            BindTipPromptButton(rt, "btn-ok", okText, true, tcs);
            var noBtn = UiKit.FindDeep(rt, "btn-no");
            if (noText == null) noBtn?.gameObject.SetActive(false); // 单键形态
            else BindTipPromptButton(rt, "btn-no", noText, false, tcs);
            _tipPromptPanel = panel;
            _tipPromptTcs = tcs;
            return await tcs.Task;
        }

        /// <summary>提示键接线：文案写按钮 label（TMP 子节点），点击=记结果并关面板。</summary>
        private void BindTipPromptButton(RectTransform panel, string btnName, string text, bool result,
            System.Threading.Tasks.TaskCompletionSource<bool?> tcs)
        {
            var btnT = UiKit.FindDeep(panel, btnName);
            if (btnT == null) return;
            var lbl = btnT.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = text;
            var button = btnT.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() => { tcs.TrySetResult(result); CloseTipPrompt(); });
        }

        /// <summary>关提示面板（点击收尾/拆屏清理/新提示顶替共用）：未点即关=结果置 null。</summary>
        private void CloseTipPrompt()
        {
            if (_tipPromptPanel != null)
            {
                UnityEngine.Object.Destroy(_tipPromptPanel);
                _tipPromptPanel = null;
            }
            var tcs = _tipPromptTcs;
            _tipPromptTcs = null;
            tcs?.TrySetResult(null);
        }

        // ======================================== 生命周期 ========================================

        public override void OnEnter()
        {
            _gameEnded = false;
            _running = true;
            RuleHooks.OnAutoTargetTaken = OnAutoTargetTaken; // 免弹自动取反馈（2026-10-22 B5）——静态探针，OnExit 先行退订


            if (_lblMode != null)
                _lblMode.text = BattleEntry.DummyTest && BattleEntry.Mode == BattleMode.LocalAI ? "木桩 · 测试"
                    : BattleEntry.Mode == BattleMode.Tutorial ? $"教学 · {BattleEntry.TutorialId}"
                    : BattleEntry.Mode == BattleMode.Challenge ? $"挑战 · 难度{BattleEntry.ChallengeDifficulty}"
                    : BattleEntry.Mode == BattleMode.Network ? "联机 · P2P"
                    : "本地 · AI";

            // 战场 3D 摆位 + 装台（2026-10-13 战场重做；2026-10-18 网络簇整删后仅本地直连）
            _field = new BattleFieldView();
            _field.HeroLifeShownChanged = OnHeroLifeShownChanged; // 角色HP读数节拍（2026-10-20）：命中拍释放时同拍重写 2D 生命标签
            // 战斗编舞（2026-10-13）：3D 寻路箭头选攻击目标 + 战斗演出（走位/受击/死亡漩涡吸走）；
            // 事件订阅须先于本屏刷新订阅（宣言即 hold 位姿），OnEnter 建立即满足。
            // 网络局不订引擎事件（2026-10-21）：房主侧引擎事件来自服务器房（全信息），订阅会触发
            // 走位/受击演出读本地 Core（null）——网络局表现走快照对账+轻事件，编舞器不参与。
            bool netMode = BattleEntry.Mode == BattleMode.Network && BattleEntry.Client != null;
            if (netMode) NetCellPins.ClearAll(); // 跨局钉格清（RuntimeId 每局重起，防串号）
            _choreo = new CombatChoreographer(_field, () => _view, RefreshField, listenEvents: !netMode, selfPlayer: () => P1);
            _choreo.ReplayDriven = true; // 战斗演出改由步进回放导演驱动（2026-10-19 严格结算序：攻击步到位才开演）
            _cellPicker = new BattleCellPicker();
            BattleStageDirector.MountBoardAndDawnAsync().Forget(); // 转场③④：装载棋盘（分区表注入）→ 日出
            // 棋盘就绪后首刷（英雄柱/初始摆位）：staged 开局局挪到段五起步——段一~四无 Core 恒不就绪，
            // 45s deadline 会在选卡组期空转（2026-10-19 掷币演出挪战场）。网络局无 Core（首刷=首快照）
            if (!StagedOpeningActive() && !netMode)
                RefreshFieldWhenBoardReady().Forget();

            _regionSelector?.ForceIdle(); // 跨局残留清算（2026-10-17）：上局退出时在途选择孤悬的 IsOpen/busy 在此钉回
            if (netMode)
                StartNetFan();
            else
                StartLocalFan();
        }

        public override void OnExit()
        {
            // 静态订阅先行退订+toast 双层清空（2026-10-17 僵尸订阅修复）：必须排在一切可能抛错的
            // 清理（守卫窗续跑/Region 收口/箭头回收）之前——OnExit 中途异常截断会让静态事件
            // OnStepAdvanced 摄着本实例跨局复活（残留 _toastOverlay 每步重渲+访问已毁 UI）。
            _tutorial?.UnhookBanner(); // 静态引导订阅退订（BattleTutorialDirector·2026-10-18 拆分）
            RuleHooks.OnAutoTargetTaken = null; // B5 静态探针退订（OnEnter 注册——僵尸订阅同口径）
            _toast.ClearLayers(); // toast 双层清空（2026-10-17 僵尸订阅口径——2026-10-18 收进 BattleToastHub）
            _running = false;
            NetShutdown(); // 网络局连接/房主服务器收口（2026-10-21 P2P；本地局 no-op）
            _opening?.Dispose(); // 五段式开场收口（2026-10-19）：在途演出停、相机位姿还原、选卡组堆拆除
            _opening = null;
            _director?.Shutdown(); // 步进回放收闸（2026-10-19）：先于日志/投影器退订——hold 解除、缓冲日志全放
            _log?.Dispose(); // 战报订阅退订（2026-10-18 拆分——原随基类 FlushSubscriptions，Deactivate 内同帧等价）
            _projector?.Dispose(); // 投影订阅退订（B2 同上口径）
            //（结业镜像局会话标记 2026-10-19 随三课制整删——此处原 ClearAssessment 调用退役）
            TutorialGuide.End();            // 引导闸卸载防跨局残留

            _decision.Shutdown(); // 守卫/响应窗收口协议（顺序依据见 BattleDecisionHub——2026-10-18 B3）
            _tutorial?.CloseIntroPanel(); // 介绍段配音卡片防跨屏残留（2026-10-17）
            CloseTipPrompt(); // TipPanel 提示弹窗防跨屏残留（2026-10-17）
            _unitButtons.Dispose(); // 单位动作 3D 按钮组防跨屏残留（2026-10-26 替弹窗）
            BattleRevealDirector.End(); // 语音联动显隐全恢复防跨屏残留（2026-10-17）
            _tutorial?.UninstallResponder(); // 教学守卫应答器卸载（null=回落内置启发）
            _toast.Hide();

            // 手牌扇区实例回收（自持实例池，随挂载层销毁亦安全——显式清防重入）
            _selfFan?.Dispose();
            _oppFan?.Dispose();
            _selfFan = _oppFan = null;

            // 战场 3D 摆位回收（与棋盘拆台 DismountBoard 各自独立清根）；编舞器先于视图销毁
            // （epoch 代际使在途演出自弃，事件退订防跨局残留）
            _choreo?.Dispose();
            _choreo = null;
            _cellPicker?.Dispose();
            _cellPicker = null;
            _pendingPins.Clear();
            _castStage?.Hide(); // AI 出牌舞台防御性收幕（正常路径由 SimpleAI 的 finally 收）
            _chainStage?.Dissolve(); // 连锁链克隆防御性消散（2026-10-19：正常路径由回放导演轮末收）
            if (_field != null) _field.HeroLifeShownChanged = null; // 节拍回调摘线（实例互持，随 _field=null 一并断）
            _field?.Dispose();
            _field = null;

            // 战场拆台：卸棋盘格子+销毁棋盘根（黑洞/光源/相机由菜单舞台钩子恢复）
            BattleStageDirector.DismountBoard();

            // 本地局棋盘接线归零（静态扩展点不清理会跨界面残留——验证器 S5 锁定此口径）
            _ctrl?.Shutdown();
            _ctrl = null;

            // 仅在仍是本界面注册时解除，避免覆盖其它界面的注册
            if (TargetSelectionService.Current is RegionTargetSelector)
                TargetSelectionService.Current = null;
            CellSelectionService.Current = null; // 移动选格反问桥同卸（静态残留口径同上——2026-10-31 场面轴3c）
            _regionSelector = null;
            if (_decision != null && ResponseWindowService.HumanResponder == _decision.ShowResponseAsync)
                ResponseWindowService.HumanResponder = null;
        }

        /// <summary>免弹自动取目标反馈（2026-10-22 玩家反馈 B5「单目标自动选定无感知」）：
        /// toast 报目标名 + 目标格 0.7s 阵营色描边环脉冲（单格 ClearGlow 收口不动对账态）。
        /// 仅己方为 chooser 时提示——AI 结算自动取不弹（刷屏）。</summary>
        private void OnAutoTargetTaken(Player chooser, List<Entity> targets)
        {
            if (!_running || chooser == null || chooser != P1 || targets == null || targets.Count == 0) return;
            var names = string.Join("、", targets.Select(t => MatchLogRenderer.EntityName(t, p => p == P1)));
            _toast.Show($"已自动选定目标：{names}");
            PulseAutoTargetGlowAsync(targets).Forget();
        }

        /// <summary>B5 脉冲高亮：目标实体落格（角色等无格实体跳过）短亮后单格熄灭
        ///（2026-10-30 起逐格提示走描边环，波纹壳随半场面板定案退役）。</summary>
        private async UniTaskVoid PulseAutoTargetGlowAsync(List<Entity> targets)
        {
            var board = _ctrl?.Board;
            var cells = new List<(int x, int z)>();
            if (board != null)
                foreach (var t in targets)
                    if (t is Card c && board.TryGetCell(c, out int x, out int z))
                        cells.Add((x, z));
            if (cells.Count == 0) return;
            foreach (var (x, z) in cells)
                BattleTileFx.SetCellRing(x, z, BoardLayout.OwnerOf(x, z) == 1
                    ? BattleTileFx.UnitGlowColorOpp : BattleTileFx.UnitGlowColor);
            await UniTask.Delay(700, ignoreTimeScale: true);
            foreach (var (x, z) in cells)
                BattleTileFx.ClearGlow(x, z);
        }

        // ======================================== 网络模式（2026-10-21 P2P 复活） ========================================
        // 本屏无 BattleController/本地引擎（房主侧引擎也只归服务器泵——UI 一律客户端路径）：
        // 快照 → BuildNet → 投影器 ApplySnapshot（复用全套 3D 对账/信息栏/连锁链渲染）；
        // 交互收口 intent 上行（目标不预收——结算期 SelectRequest 反问回弹，服务器权威候选集）；
        // 反问 = Region 选项列表（实体候选按 CardId 查表渲染卡名）；
        // 表现 MVP = 战报轻日志+浮字级演出（伤害/治疗粒度的 3D 演出接 NetEvent 载荷属后续里程碑）。

        /// <summary>网络对局接入（LobbyScreen 移交的连接一次性消费）。</summary>
        private void StartNetFan()
        {
            _netClient = BattleEntry.Client;
            BattleEntry.Client = null;
            if (_netClient == null)
            {
                _toast.Show("网络对局连接缺失——返回主菜单");
                Manager.Back();
                return;
            }

            // 反问选择器（Region 选项列表）。不挂 TargetSelectionService/ResponseWindowService：
            // 网络局本端无引擎请求在途；房主侧更不得覆盖 NetRoom 已挂的 NetworkTargetSelector
            //（覆盖=引擎反问改走本地 Region 选择器→读全信息引擎→泄漏+流程错乱）。
            _regionSelector = _regionPanelCtl != null
                ? new RegionTargetSelector(_regionPanelCtl.PanelNode, BoardCellOf, p => false, RoleCellOf,
                    BuildSelectPrompt, _decision.OnSelectCancel)
                : null;

            // 手牌扇交互（网络版：可发动性按视图近似评估，真校验交服务器 Error 反馈）
            _selfFan?.SetInteractions(new FanInteractions
            {
                CanUse = NetFanCanUse,
                CanPlaceLand = NetFanCanPlaceLand,
                CanSpUse = NetFanCanSpUse,
                OnUse = NetOnFanUse,
                OnPlaceLand = NetOnFanPlaceLand,
                OnSpUse = NetOnFanSpUse,
            });

            _netClient.OnSnapshot += NetOnSnapshot;
            _netClient.OnEventBatch += NetOnEvents;
            _netClient.OnSelectRequest += NetOnSelectRequest;
            _netClient.OnTextRequest += NetOnTextRequest;
            _netClient.OnRoomState += NetOnRoomState;
            _netClient.OnManifest += NetOnManifest;
            _netClient.OnError += e => _toast.Show($"被拒（{e?.Context}）：{e?.Reason}");
            _netClient.OnDisconnected += NetOnDisconnected;

            // 网络五段式（2026-10-28 开局流程改版）：就绪开局（DeckSubmit 相位）即战场演出——
            // 地块/角色就位→掷币（服务器权威结果注入/固定模式快进）→网络选卡组面板（30s 服务器
            // 权威倒计时）→确认发 DeckSubmit 握手；对局开始=manifest 校验通过+首快照（既有链路）。
            if (StagedOpeningActive())
            {
                bool coinAnim = BattleEntry.NetCoinToss; // 一次性消费（防跨局残留，同 Client 口径）
                bool playerFirst = BattleEntry.NetPlayerFirst;
                _opening = new BattleOpeningDirector(() => _running, _field, lunzhuan: false,
                    engage: null, overlayParent: Root,
                    network: true, netPlayerFirst: playerFirst, netCoinAnimation: coinAnim,
                    netEngage: NetEngageStaged);
                _opening.RunAsync().Forget();
            }

            Debug.Log($"[BattleScreen] 网络对局已接入（{_netClient.Remote}）——{(StagedOpeningActive() ? "五段式开局（掷币+选卡组）" : "等待首快照")}");
        }

        /// <summary>网络五段式段五（确认出战）：UI 已关、对局开始前——发 DeckSubmit 握手（摘要主线程算）。
        /// tokenCardIds/tokenBindings（P9b 2026-10-29）：衍生物区/槽绑定随提交上送并计入摘要口径。</summary>
        private void NetEngageStaged(string[] cardIds, string skillCardId,
            string[] tokenCardIds, List<DeckTokenBinding> tokenBindings)
        {
            if (_netClient == null || cardIds == null || cardIds.Length == 0) return;
            _netSubmittedDeckIds = cardIds;
            _netSubmittedTokenIds = tokenCardIds;
            _netSubmittedTokenBindings = tokenBindings;
            var digest = NetMatchHandshake.ComputeDeckDigest(cardIds, tokenCardIds, tokenBindings); // 主线程（确认点击）——服务器泵同帧串行，天然安全
            _netClient.Send(NetworkMessageType.DeckSubmit, new MsgDeckSubmit
            {
                DeckName = "net",
                CardIds = cardIds,
                Digest = digest,
                SkillCardId = skillCardId,
                TokenCardIds = tokenCardIds ?? Array.Empty<string>(),
                TokenBindings = NetMatchHandshake.ToWireBindings(tokenBindings),
            });
            Debug.Log($"[BattleScreen] 已确认卡组（{cardIds.Length}张）——等待对手确认与开局…");
            _toast.Show("已确认卡组——等待对手与开局…");
        }

        /// <summary>开局清单到达（双方确认完毕）：校验回显→收口段四面板→对局开始（等首快照）。</summary>
        private void NetOnManifest(MsgMatchManifest manifest)
        {
            if (manifest == null) return;
            if (_netSubmittedDeckIds != null
                && !NetMatchHandshake.VerifyMatchManifest(manifest, _netSubmittedDeckIds,
                    _netSubmittedTokenIds, _netSubmittedTokenBindings, out var reason))
            {
                TideLog.Error($"[BattleScreen] MatchManifest 校验失败：{reason}——退回主菜单");
                _toast.Show($"开局清单校验失败：{reason}");
                Manager.Back();
                return;
            }
            _opening?.NetDeckPanel?.ForceClose(); // 服务器代提/双方已齐——面板收口（不再本地提交）
            Debug.Log($"[BattleScreen] 网络开局清单校验通过（OwnSeat={manifest.OwnSeat}）——对局开始");
        }

        /// <summary>快照到达：BuildNet → 投影器 ApplySnapshot（渲染扇出与本地同轨）+ 双扇对账。</summary>
        private void NetOnSnapshot(MsgGameStateSync snap)
        {
            if (snap == null) return;
            _netSnapshot = snap;
            _projector.ApplySnapshot(BattleView.BuildNet(snap));
            RefreshFansNet();
        }

        /// <summary>网络局双扇对账：己方手牌正面（快照 OwnRuntimeIds）；对手=数量背面+展示卡翻正面。</summary>
        private void RefreshFansNet()
        {
            if (BattleRevealDirector.SuppressHandFan) return;
            var d = _view;
            if (d == null) return;

            var self = new List<HandFanCardData>();
            foreach (var card in d.SelfHand)
                self.Add(new HandFanCardData
                {
                    RuntimeId = card.RuntimeId,
                    Item = CardOverlayItem.FromHand(card),
                    FaceUp = true,
                });
            _selfFan?.SetCards(self);

            // 对手手牌：背面按数量（RuntimeId 隐藏——合成键）；被展示（Exposed）的经 RevealedZoneCards 翻正面
            var exposed = new List<SerializableRuntimeCardState>();
            if (_netSnapshot?.RevealedZoneCards != null)
                foreach (var z in _netSnapshot.RevealedZoneCards)
                    if (z != null && (Zone)z.Zone == Zone.Hand && z.Seat == 1 - d.NetViewerSeat)
                        exposed.AddRange(z.Cards ?? Array.Empty<SerializableRuntimeCardState>());
            var opp = new List<HandFanCardData>();
            int backs = Math.Max(0, d.Opp.HandCount - exposed.Count);
            for (int i = 0; i < backs; i++)
                opp.Add(new HandFanCardData { RuntimeId = 0x80000000u + (uint)i, Item = BlankBack(0), FaceUp = false });
            foreach (var s in exposed)
            {
                var v = BattleView.FromRuntime(s, null, ownerIsOpponent: true);
                opp.Add(new HandFanCardData { RuntimeId = s.RuntimeId, Item = CardOverlayItem.FromHand(v), FaceUp = true });
            }
            _oppFan?.SetCards(opp);

            // 可发动性精确化（2026-10-28 取代「主阶段全亮」近似）：逐卡费用可付/永久物空格
            //（放地上限在 NetFanCanPlaceLand）——置灰口径与本地一致（SetPlayable 同源）
            if (_selfFan != null)
                foreach (var card in d.SelfHand)
                {
                    bool castable = NetCardCastableNet(card);
                    _selfFan.SetPlayable(card.RuntimeId, castable, false);
                }
        }

        // ---- 网络局交互（intent 上行） ----

        private bool NetFanGate()
        {
            var d = _view;
            return !_gameEnded && d != null && d.MyTurn && d.Phase == PhaseType.Main && !IsTargetSelecting();
        }

        private bool NetFanCanUse(HandFanCard fan)
            => NetFanGate() && NetCardCastableNet(NetHandViewOf(fan));

        /// <summary>网络「特殊使用」条件（2026-10-31 P12：充能+苏醒）：
        /// 充能=任意卡型带充能效果（颜色余量服务器 CanCharge 终审，客户端不预闸）；
        /// 生物苏醒=**卡面带 Awaken 关键词**才持续显示（点击 intent 上行，服务器终审）。</summary>
        private bool NetFanCanSpUse(HandFanCard fan)
        {
            if (!NetFanGate()) return false;
            var card = NetHandViewOf(fan);
            var data = DataOf(card);
            if (data == null) return false;
            if (ComposerCatalog.HasChargeEffect(data)) return true; // 充能（P12：协议 114 改义承载）
            if (data.Supertype == Cardtype.Creature) return GameActions.HasAwakenKeyword(data);
            return false;
        }

        /// <summary>网络逐卡可发动（2026-10-28 可用性精确化，取代「主阶段全亮」近似）：
        /// 费用任一模式可付（快照每色可用量）+ 永久物另需单位区空格——同本地 CardCastableNow
        /// 口径；数据源=快照（WalletByColor/单位占用），服务器仍真校验（Error+toast 兜底）。
        /// 已知近似：法术锚价（SpellCastAnchor 绑引擎卡实例）未并入，法术显示偏亮由服务器拒兜底。</summary>
        private bool NetCardCastableNet(BattleCardView card)
        {
            var data = DataOf(card);
            if (data == null) return false;
            int modes = Math.Max(1, CostDerivationService.GetModeCount(data));
            bool ok = false;
            for (int i = 0; i < modes && !ok; i++)
                ok = NetCanAfford(data, i);
            if (!ok) return false;
            if (data.Supertype != Cardtype.Spell)
            {
                if (data.IsField) return true; // 场地卡（P8）：场地格每方一张恒可落，不占单位区空格
                return NetEmptyUnitCells().Count > 0; // 永久物=单位区空格（法术恒自动落位）
            }
            return true;
        }

        /// <summary>网络可付判定：恒推导费用账单 vs 快照每色可用量（WalletByColor=
        /// GetAvailableManaCount：钱包+未横置地牌现取现用合计）。Any（万能货币）由付完
        /// 本色后的盈余垫付（链尾垫付同口径）。</summary>
        private bool NetCanAfford(CardData data, int modeIndex)
        {
            var d = _view;
            if (d == null) return false;
            var bill = ElementPaymentValidator.NormalizeBill(CardCostService.GetRuntimeCost(data, modeIndex));
            if (bill.Count == 0) return true;
            var avail = d.Self.WalletByColor;
            int surplus = 0;
            foreach (var kv in bill)
            {
                if (kv.Key == ManaType.Any) continue;
                int have = (int)kv.Key >= 0 && (int)kv.Key < avail.Length ? avail[(int)kv.Key] : 0;
                if (kv.Value > have) return false;
                surplus += have - kv.Value;
            }
            return !bill.TryGetValue(ManaType.Any, out var anyNeed) || anyNeed <= surplus;
        }

        private bool NetFanCanPlaceLand(HandFanCard fan)
        {
            if (!NetFanGate()) return false;
            var data = DataOf(NetHandViewOf(fan));
            if (data == null) return false;
            // 锁色资格=可锁产色候选非空（GetLandColorChoices 单源：卡面/各模式费 ∪ 主动效果锚价，去黑白）
            if (NetLandColorChoices(data).Count == 0) return false;
            // 地牌池上限（快照 LandCap vs 我方已放地牌数——同本地 CanAddLand 口径）
            var d = _view;
            return (d?.SelfLands?.Count ?? 0) < (d?.Self.LandCap ?? 0);
        }

        /// <summary>可锁产色（GetLandColorChoices 网络侧口径——CardData 单源重载直转，锚价开格 2026-10-23）。</summary>
        private static List<ManaType> NetLandColorChoices(CardData data)
            => ElementPoolSystem.GetLandColorChoices(data);

        /// <summary>扇区卡 → 视图手牌卡（RuntimeId 对账；不在手=已过态返回 null）。</summary>
        private BattleCardView NetHandViewOf(HandFanCard fan)
        {
            if (fan == null) return null;
            foreach (var c in _view?.SelfHand ?? new List<BattleCardView>())
                if (c != null && c.RuntimeId == fan.RuntimeId) return c;
            return null;
        }

        private void NetOnFanUse(HandFanCard fan)
        {
            var card = NetHandViewOf(fan);
            if (card == null || !NetFanGate())
            {
                if (card != null && !_gameEnded && _view != null && !_view.MyTurn) _toast.Show("对手回合，暂不可操作");
                return;
            }
            NetUseCardAsync(card, awakenCast: false).Forget();
        }

        /// <summary>网络「特殊使用」点击（2026-10-31 P12）：充能=IntentBuildCard（协议 114 改义）
        /// 上行（选色：单色直发/多色 Region 择一；服务器 CanCharge 终审）；生物苏醒=AwakenCast=true
        /// 直行；双入口并存（带充能+带苏醒）=Region 二选一。</summary>
        private void NetOnFanSpUse(HandFanCard fan)
        {
            var card = NetHandViewOf(fan);
            if (card == null || !NetFanGate()) return;
            var data = DataOf(card);
            if (data == null) return;
            bool canCharge = ComposerCatalog.HasChargeEffect(data);
            bool canAwaken = data.Supertype == Cardtype.Creature && GameActions.HasAwakenKeyword(data);
            if (canCharge && canAwaken)
            {
                NetSpUsePickAsync(card).Forget();
                return;
            }
            if (canCharge)
            {
                NetChargeAsync(card).Forget();
                return;
            }
            if (canAwaken)
                NetUseCardAsync(card, awakenCast: true).Forget();
        }

        /// <summary>网络特殊使用方式二选一（带充能+带苏醒的生物）。</summary>
        private async UniTaskVoid NetSpUsePickAsync(BattleCardView card)
        {
            int? picked = await PickRegionOptionAsync("选择特殊使用方式",
                new List<string> { "充能（付 1 点元素·减同色费用）", "苏醒召唤（免灰转沉睡·横置登场）" });
            if (picked == null) return;
            if (picked == 0) NetChargeAsync(card).Forget();
            else NetUseCardAsync(card, awakenCast: true).Forget();
        }

        /// <summary>网络充能：充能色=代价抵扣前卡费非零色（单色直发/多色 Region 择一）→
        /// IntentBuildCard（协议 114，P12 改义充能——服务器 ActivateCharge 终审）。</summary>
        private async UniTaskVoid NetChargeAsync(BattleCardView card)
        {
            var data = DataOf(card);
            if (data == null || _netClient == null) return;
            CardCostService.GetPrePayloadCost(data, 0, out var preCost, out _);
            var colors = new List<ManaType>();
            foreach (var c in preCost.NonzeroColors())
                if (c != ManaType.Any && preCost[c] > 0f) colors.Add(c);
            if (colors.Count == 0) { _toast.Show("该卡卡费无可充颜色"); return; }

            ManaType color;
            if (colors.Count == 1) color = colors[0];
            else
            {
                var options = new List<string>();
                foreach (var c in colors)
                    options.Add($"充能 {ManaTypeNames.ZhNameOf(c)}（现付 1 点该色元素）");
                int? picked = await PickRegionOptionAsync("选择充能颜色", options);
                if (picked == null) return;
                color = colors[picked.Value];
            }

            _netClient.Send(NetworkMessageType.IntentBuildCard, new MsgIntentBuildCard
            {
                CardRuntimeId = card.RuntimeId,
                Color = (int)color,
            });
            _toast.Show($"充能 +1{ManaTypeNames.ZhNameOf(color)} 已发动（服务器终审）");
        }

        /// <summary>网络出牌：模式抉择（Region）→ 永久物（生物/结界）
        /// 棋盘选格（光圈候选，同本地交互——钉 NetCellPins 随快照生效）→ IntentPlayCard
        ///（目标 null=结算期反问回弹）。法术恒自动落位（B4 定案）；选格钉=客户端表现层权威
        ///（快照不承载坐标）。AwakenCast=苏醒施放（P6c 只免灰转沉睡层数，服务器终审）——
        /// 2026-10-30 SpUse 改版：召唤方式由按钮直定（使用=false / 特殊使用苏醒=true），
        /// 二选弹窗与本地同步退役。</summary>
        private async UniTaskVoid NetUseCardAsync(BattleCardView card, bool awakenCast)
        {
            var data = DataOf(card);
            if (data == null) return;

            int modeIndex = 0;
            if (GetModeCount(card) >= 2)
            {
                var labels = data.Supertype == Cardtype.Spell ? SpellEffectLabels(data) : FindChoiceLabels(data);
                int modeCount = Math.Max(2, CostDerivationService.GetModeCount(data)); // P12：非充能主动效果数
                var options = new List<string>();
                for (int i = 0; i < modeCount; i++)
                {
                    string name = i < labels.Count && !string.IsNullOrEmpty(labels[i]) ? labels[i] : $"模式 {i + 1}";
                    options.Add(name);
                }
                int? picked = await PickRegionOptionAsync(
                    data.Supertype == Cardtype.Spell ? "选择要发动的效果" : "选择模式", options);
                if (picked == null) return; // 取消/缺 Region：本次不出牌
                modeIndex = picked.Value;
            }

            // 场地卡（2026-10-29 P8）：落位恒为己方场地格——视图确定性映射（FillUnitsNet），
            // 无需选格/钉；旧场地在位=顶替入墓（服务器 ReplaceExistingField 终局裁决）
            if (data.Supertype != Cardtype.Spell && data.IsField)
            {
                bool replacing = (_view?.SelfUnits ?? new List<BattleCardView>())
                    .Any(u => u != null && DataOf(u)?.IsField == true);
                _toast.Show(replacing ? "场地就位——将顶替己方现有场地（旧场地入墓）" : "场地就位（己方场地格）");
            }
            // 永久物（2026-10-28 用户反馈：结界/生物不再 first-free 自动落位）——棋盘选格后发
            else if (data.Supertype != Cardtype.Spell && !await NetPickUnitCellAsync(card)) return;

            _netClient.Send(NetworkMessageType.IntentPlayCard, new MsgIntentPlayCard
            {
                CardRuntimeId = card.RuntimeId,
                FromZone = (int)Zone.Hand,
                ModeIndex = modeIndex,
                AwakenCast = awakenCast, // 苏醒施放（2026-10-29 P6 协议 v3；按钮直定）
                Targets = null, // 声明期目标不预收：结算期经 SelectRequest 反问（服务器候选集权威）
            });
        }

        /// <summary>网络永久物选格（光圈候选=己方单位区空格，同本地 BeginUseCellPicking 交互）：
        /// 点格=写 NetCellPins 钉并返回 true（随发 intent，快照落位即所选格）；取消返回 false。
        /// 满区 toast 拦截。</summary>
        private async UniTask<bool> NetPickUnitCellAsync(BattleCardView card)
        {
            var options = NetEmptyUnitCells();
            if (options.Count == 0) { _toast.Show("单位区已满——无法打出"); return false; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            var picked = await PickCellAsync(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options);
            if (picked == null)
            {
                RestoreFanNet();
                return false;
            }
            NetCellPins.Pin(card.RuntimeId, picked.Opt.X, picked.Opt.Z);
            RestoreFanNet();
            Debug.Log($"[HandPlay] 网络永久物选格：{card.Name} → ({picked.Opt.X},{picked.Opt.Z})（钉随快照生效）");
            return true;
        }

        /// <summary>网络局空格候选（己方单位区未被单位/施放占用的格——视图坐标权威）。</summary>
        private List<BattleCellPicker.CellOption> NetEmptyUnitCells()
        {
            var occupied = new HashSet<int>();
            foreach (var u in _view?.SelfUnits ?? new List<BattleCardView>())
                if (u != null && u.X >= 0) occupied.Add(u.X * 100 + u.Z);
            foreach (var c in _view?.CastingCards ?? new List<BattleCardView>())
                if (c != null && c.X >= 0) occupied.Add(c.X * 100 + c.Z);
            var options = new List<BattleCellPicker.CellOption>();
            foreach (var cell in BoardLayout.UnitCells(0))
                if (!occupied.Contains(cell.x * 100 + cell.z))
                    options.Add(new BattleCellPicker.CellOption { X = cell.x, Z = cell.z, ColorIndex = -1 });
            return options;
        }

        private void NetOnFanPlaceLand(HandFanCard fan)
        {
            var card = NetHandViewOf(fan);
            if (card == null || !NetFanGate()) return;
            NetPlaceLandAsync(card).Forget();
        }

        /// <summary>网络放地（2026-10-28 改版：棋盘选格取代 Region 弹窗——同本地交互）：
        /// 候选格=可锁色 → BoardLayout 固定元素格（与 FillLandsNet/AssignLandCells 同构映射）；
        /// 点格→单色直发 IntentAddToElementPool（ModeIndex=锁色）；同格多候选色=Region 列表消歧
        ///（本地同款）；取消恢复手牌扇。</summary>
        private async UniTaskVoid NetPlaceLandAsync(BattleCardView card)
        {
            var colors = NetLandColorChoices(DataOf(card));
            if (colors == null || colors.Count == 0) return;

            // 候选格（cell → 候选色集）：颜色→固定元素格映射（观察者视角己方 owner=0）
            var cellColors = new Dictionary<(int x, int z), List<ManaType>>();
            var options = new List<BattleCellPicker.CellOption>();
            foreach (var color in colors)
            {
                var cell = BoardLayout.LandCellOfElement(0, color);
                if (cell.x < 0) continue;
                if (!cellColors.TryGetValue((cell.x, cell.z), out var list))
                {
                    list = new List<ManaType>();
                    cellColors[(cell.x, cell.z)] = list;
                    options.Add(new BattleCellPicker.CellOption { X = cell.x, Z = cell.z, ColorIndex = (int)color });
                }
                list.Add(color);
            }
            if (options.Count == 0) { _toast.Show("地牌行无空位（颜色数超格）"); return; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            var picked = await PickCellAsync(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options);
            if (picked == null) { RestoreFanNet(); return; } // 取消
            RestoreFanNet();

            var candidates = cellColors[(picked.Opt.X, picked.Opt.Z)];
            if (candidates.Count == 1)
            {
                NetLandFinal(card, candidates[0]);
                return;
            }
            // 同格多候选色：Region 列表消歧（本地 PickLandColorAsync 同款）
            var labels = candidates.Select(c => $"锁定 {BattleView.ManaZh(c)} 产色").ToList();
            int? idx = await PickRegionOptionAsync("选择地牌产色", labels);
            if (idx == null) return;
            NetLandFinal(card, candidates[idx.Value]);
        }

        private void NetLandFinal(BattleCardView card, ManaType color)
        {
            Debug.Log($"[HandPlay] 网络放地：{card.Name} → 锁定 {BattleView.ManaZh(color)}（棋盘选格确认）");
            _netClient.Send(NetworkMessageType.IntentAddToElementPool, new MsgIntentAddToElementPool
            {
                CardRuntimeId = card.RuntimeId,
                ModeIndex = (int)color, // 2026-10-11 锁色定案：ModeIndex 复用为 ManaType 枚举值
            });
        }

        /// <summary>网络局手牌扇恢复（选格取消/确认后——同本地 RestoreFan 口径，走网络刷新链）。</summary>
        private void RestoreFanNet()
        {
            _selfFan?.SetVisible(true);
            RefreshFansNet();
        }

        /// <summary>选格结果包装（AOT 约束：CellOption 是热更 struct——UniTask 泛型参禁热更
        /// struct，引用类型包装走已锚 &lt;object&gt; 共享形态；null=取消）。</summary>
        private sealed class NetCellPick
        {
            public BattleCellPicker.CellOption Opt;
        }

        /// <summary>选格异步包装（2026-10-28 网络放地/永久物选格共用：光圈候选+点格回调）。
        /// 取消=返回 null。</summary>
        private async UniTask<NetCellPick> PickCellAsync(
            Vector3 origin, List<BattleCellPicker.CellOption> options)
        {
            var tcs = new Cysharp.Threading.Tasks.UniTaskCompletionSource<NetCellPick>();
            _cellPicker.Begin(origin, options,
                opt => tcs.TrySetResult(new NetCellPick { Opt = opt }),
                () => tcs.TrySetResult(null));
            return await tcs.Task;
        }

        /// <summary>网络局点己方单位：响应窗口在身=守卫候选；我方主阶段=攻击宣言（Region 选目标——
        /// 3D 寻路箭头选择器依赖本地引擎路径，网络局列表选择）。</summary>
        private void NetUnitClick(BattleCardView card)
        {
            var d = _view;
            if (_gameEnded || d == null) return;
            if (d.StackNotEmpty && d.MyPriority)
            {
                NetGuardFlowAsync(card).Forget();
                return;
            }
            if (!d.MyTurn || d.Phase != PhaseType.Main)
            {
                _toast.Show(!d.MyTurn ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return;
            }
            if (card.IsTapped) { _toast.Show("该单位已横置"); return; }
            if (card.Runtime != null && card.Runtime.Power <= 0) { _toast.Show("该单位攻击力为 0"); return; }
            NetAttackFlowAsync(card).Forget();
        }

        private async UniTaskVoid NetAttackFlowAsync(BattleCardView card)
        {
            var d = _view;
            var options = new List<string> { "对方角色" };
            var targets = new List<NetEntityRef>
            {
                new NetEntityRef { Seat = 1 - d.NetViewerSeat, IsPlayer = true },
            };
            foreach (var u in d.OppUnits)
            {
                if (u == null) continue;
                options.Add(u.Name);
                targets.Add(new NetEntityRef
                {
                    RuntimeId = u.RuntimeId,
                    CardId = u.CardId,
                    Seat = 1 - d.NetViewerSeat,
                    IsPlayer = false,
                });
            }
            int? picked = await PickRegionOptionAsync("选择攻击目标", options);
            if (picked == null) return;
            _netClient.Send(NetworkMessageType.IntentDeclareAttack, new MsgIntentDeclareAttack
            {
                Attacker = new NetEntityRef
                {
                    RuntimeId = card.RuntimeId,
                    CardId = card.CardId,
                    Seat = d.NetViewerSeat,
                    IsPlayer = false,
                },
                Target = targets[picked.Value],
            });
        }

        /// <summary>网络守卫：守卫对象=栈上的攻击宣言（StackObjectType==AttackDeclaration，Source=攻击者）。</summary>
        private async UniTaskVoid NetGuardFlowAsync(BattleCardView blocker)
        {
            if (blocker.IsTapped) { _toast.Show("该单位已横置（守卫须未横置）"); return; }
            var snap = _netSnapshot;
            if (snap == null) return;
            var options = new List<string>();
            var attackers = new List<NetEntityRef>();
            foreach (var item in snap.StackV2 ?? Array.Empty<StackItemDTO>())
            {
                if (item == null || item.StackObjectType != (int)StackObjectType.AttackDeclaration) continue;
                if (item.Source == null || item.Source.IsPlayer) continue;
                options.Add($"拦截 {BattleView.EntityTextOf(item.Source, snap.ViewerSeat)} 的攻击");
                attackers.Add(item.Source);
            }
            if (options.Count == 0) { _toast.Show("没有可守卫的攻击"); return; }
            int? picked = await PickRegionOptionAsync("选择要守卫的攻击", options);
            if (picked == null) return;
            _netClient.Send(NetworkMessageType.IntentDeclareBlock, new MsgIntentDeclareBlock
            {
                Blocker = new NetEntityRef
                {
                    RuntimeId = blocker.RuntimeId,
                    CardId = blocker.CardId,
                    Seat = snap.ViewerSeat,
                    IsPlayer = false,
                },
                Attacker = attackers[picked.Value],
            });
        }

        /// <summary>网络局英雄技能发动（角色格【发动】键）：多效果 Region 抉择 → intent（服务器横置闸校验）。</summary>
        private async UniTaskVoid NetActivateSkillAsync()
        {
            var d = _view;
            if (_gameEnded || d == null || !d.MyTurn || d.Phase != PhaseType.Main) return;
            var skill = d.Self?.Skill;
            if (skill == null) { _toast.Show("本局无英雄技能"); return; }
            if (skill.IsTapped) { _toast.Show("技能本回合已发动"); return; }

            // 主动效果计数（CardData 口径——与 HeroSkillSystem.SkillEffectsOf 同谓词）
            var data = DataOf(skill);
            int count = 0;
            var names = new List<string>();
            if (data?.Effects != null)
                foreach (var e in data.Effects)
                {
                    int timing = e?.TriggerTiming ?? -1;
                    if (timing != (int)TriggerTiming.Activate_Active
                        && timing != (int)TriggerTiming.Activate_Instant) continue;
                    count++;
                    names.Add(string.IsNullOrEmpty(e.DisplayName) ? $"效果 {count}" : e.DisplayName);
                }
            if (count == 0) { _toast.Show("技能无可发动效果"); return; }

            int effectIndex = 0;
            if (count > 1)
            {
                var options = names.Select(n => $"{n}（付1灰·速度0）").ToList();
                int? picked = await PickRegionOptionAsync("选择要发动的效果", options);
                if (picked == null) return;
                effectIndex = picked.Value;
            }
            _netClient.Send(NetworkMessageType.IntentActivateHeroSkill,
                new MsgIntentActivateHeroSkill { EffectIndex = effectIndex });
        }

        /// <summary>网络局结束回合（hud 结束格）。</summary>
        private void NetEndTurn()
        {
            var d = _view;
            if (_gameEnded || d == null) return;
            if (!d.MyTurn) { _toast.Show("对手回合进行中，暂不可结束回合"); return; }
            _netClient.Send<object>(NetworkMessageType.IntentEndTurn, null);
        }

        /// <summary>网络局让行（btn-notchain：响应窗口/优先权在我时让过=PassPriority）。</summary>
        private void NetPassPriority()
            => _netClient.Send<object>(NetworkMessageType.IntentPassPriority, null);

        /// <summary>btn-notchain 分发（2026-10-21 网络复活）：网络局=让过 intent；本地局=决策中枢三义键。</summary>
        private void OnNotChainClick()
        {
            if (NetActive)
            {
                NetPassPriority();
                return;
            }
            _decision.OnNotChainClick();
        }

        // ---- 网络局反问（SelectRequest → Region 选项列表 → SelectResponse） ----

        private void NetOnSelectRequest(MsgSelectRequest req)
        {
            if (req == null || !NetActive) return;
            NetSelectAsync(req).Forget();
        }

        private async UniTaskVoid NetSelectAsync(MsgSelectRequest req)
        {
            int me = _view?.NetViewerSeat ?? _netSnapshot?.ViewerSeat ?? 0;
            List<string> options;
            if (req.Candidates != null && req.Candidates.Length > 0)
            {
                // 实体反问：候选按 CardId 查表渲染卡名（Labels 恒空——线上去文本口径）
                options = new List<string>(req.Candidates.Length);
                foreach (var r in req.Candidates)
                    options.Add(BattleView.EntityTextOf(r, me));
            }
            else
            {
                options = req.Labels?.ToList() ?? new List<string>();
            }

            string title = string.IsNullOrWhiteSpace(req.Title) ? "请选择" : req.Title;
            if (options.Count == 0)
            {
                // 空候选（无目标可用等）：直接空应答（引擎 AutoSelect 兜底同口径）
                _netClient.Send(NetworkMessageType.SelectResponse, new MsgSelectResponse
                {
                    RequestId = req.RequestId,
                    Indices = Array.Empty<int>(),
                });
                return;
            }

            int[] indices;
            if (req.Max > 1 && _regionSelector != null)
            {
                var picked = await _regionSelector.SelectIndicesAsync(
                    options, Math.Max(1, Math.Min(req.Min, options.Count)),
                    Math.Min(Math.Max(req.Max, 1), options.Count), title, "", false, 60f);
                indices = picked != null ? picked.ToArray() : Array.Empty<int>();
            }
            else
            {
                int? picked = await PickRegionOptionAsync(title, options);
                indices = picked == null ? Array.Empty<int>() : new[] { picked.Value };
            }
            if (!NetActive) return; // 断线后不再应答
            _netClient.Send(NetworkMessageType.SelectResponse, new MsgSelectResponse
            {
                RequestId = req.RequestId,
                Indices = indices,
            });
        }

        // ---- 网络局文本反问（2026-10-31 阶段C：TextRequest → 模态输入窗 → TextResponse）----

        private void NetOnTextRequest(MsgTextRequest req)
        {
            if (req == null || !NetActive) return;
            NetTextAsync(req).Forget();
        }

        private async UniTaskVoid NetTextAsync(MsgTextRequest req)
        {
            // 本地局同款模态输入窗（RegionTargetSelector.PromptTextAsync——中文输入法随 TMP）；
            // null=取消（Text=null 应答），服务器侧再按长度/座位校验收口
            string text = _regionSelector != null
                ? await _regionSelector.PromptTextAsync(
                    req.Title, req.Default, req.MaxLen, req.TimeoutSeconds > 0f ? req.TimeoutSeconds : 120f)
                : null;
            if (!NetActive) return; // 断线后不再应答
            _netClient.Send(NetworkMessageType.TextResponse, new MsgTextResponse
            {
                RequestId = req.RequestId,
                Text = text,
            });
        }

        // ---- 网络局下行事件（表现 MVP：终局+战报轻日志） ----

        private void NetOnEvents(NetEvent[] events)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                if (e == null) continue;
                if (e.EventType == nameof(GameOverEvent))
                {
                    var winner = NetParamEntity(e, nameof(GameOverEvent.Winner));
                    string reason = NetParamString(e, nameof(GameOverEvent.Reason));
                    int me = _view?.NetViewerSeat ?? -1;
                    bool win = me >= 0 && winner != null && winner.Seat == me;
                    if (!_gameEnded)
                    {
                        _gameEnded = true;
                        MarkGameOverUi($"{(win ? "胜利" : "失败")}——{(win ? "我方" : "对手")}获胜（{reason}）");
                    }
                    continue;
                }
                NetAppendLogLight(e);
            }
        }

        /// <summary>轻量战报行（schema-less 参数直取；完整战报中间层随演出里程碑接入）。</summary>
        private void NetAppendLogLight(NetEvent e)
        {
            int me = _view?.NetViewerSeat ?? -1;
            string line = null;
            switch (e.EventType)
            {
                case nameof(CardPlayEvent):
                    line = $"{WhoText(NetParamEntity(e, nameof(CardPlayEvent.Player)), me)} 打出 " +
                           $"{BattleView.EntityTextOf(NetParamEntity(e, nameof(CardPlayEvent.PlayedCard)), me)}";
                    break;
                case nameof(DamageEvent):
                    line = $"{BattleView.EntityTextOf(NetParamEntity(e, nameof(DamageEvent.Target)), me)} " +
                           $"受到 {NetParamInt(e, nameof(DamageEvent.Amount))} 点伤害";
                    break;
                case nameof(CardDrawEvent):
                    line = $"{WhoText(NetParamEntity(e, nameof(CardDrawEvent.Player)), me)} 抽了一张牌";
                    break;
            }
            if (line != null) _log.Append(line, "log-line--net");
        }

        private static string WhoText(NetEntityRef r, int viewerSeat)
            => r != null && r.IsPlayer ? (r.Seat == viewerSeat ? "我方" : "对手") : "?";

        private static NetParam NetParamOf(NetEvent e, string field)
            => e.Params?.FirstOrDefault(p => p != null && p.FieldName == field);

        private static NetEntityRef NetParamEntity(NetEvent e, string field)
            => NetParamOf(e, field)?.EntityRefs?.FirstOrDefault();

        private static long NetParamInt(NetEvent e, string field)
            => NetParamOf(e, field)?.IntValue ?? 0;

        private static string NetParamString(NetEvent e, string field)
            => NetParamOf(e, field)?.StringValue;

        private void NetOnRoomState(MsgRoomState room)
        {
            if (room == null) return;
            // 选卡组相位：服务器权威倒计时整秒校准转发（面板本地走字+此处校准）
            if (room.Phase == (int)NetRoomPhase.DeckSubmit)
                _opening?.NetDeckPanel?.OnServerSeconds(room.DeckPickSecondsLeft);
            // 对局中断线作废（M2 策略）：服务器广播 Error+Finished——留守侧收口为终局态
            if (room.Phase == (int)NetRoomPhase.Finished && !_gameEnded)
            {
                _gameEnded = true;
                MarkGameOverUi("对局作废（对手断线/房间关闭）——返回主菜单");
            }
        }

        private void NetOnDisconnected()
        {
            if (_gameEnded) return;
            _gameEnded = true;
            MarkGameOverUi("连接断开——对局作废（断线重连属后续里程碑）");
        }

        /// <summary>网络局收口（OnExit）：拆连接；房主侧会话服务器一并停机（重开=回房间屏重建）。</summary>
        private void NetShutdown()
        {
            if (_netClient != null)
            {
                _netClient.OnSnapshot -= NetOnSnapshot;
                _netClient.OnEventBatch -= NetOnEvents;
                _netClient.OnSelectRequest -= NetOnSelectRequest;
                _netClient.OnTextRequest -= NetOnTextRequest;
                _netClient.OnRoomState -= NetOnRoomState;
                _netClient.OnManifest -= NetOnManifest;
                _netClient.OnDisconnected -= NetOnDisconnected;
                NetClientRuntime.Unbind(_netClient);
                _netClient.Close();
                _netClient = null;
            }
            NetSessionServer.Current?.Stop(); // 房主侧：非 host 为 no-op
            _netSnapshot = null;
        }

        // ======================================== 本地模式 ========================================
        //（2026-10-15 彻底清除）旧一代本地启动 StartLocal 已删——零调用者的迁移残留，其订阅表与
        // 现役 StartLocalFan 漂移两次咬人（立牌滞留/入场演出失效）。本地局唯一入口=StartLocalFan；
        // 随迁已删：AppendLocal/OnGameOver（仅旧表引用）。RefreshLocal 仍被网络等路径共用故保留。

        // ======================================== 本地对局（扇形手牌过渡面，2026-10-03 接入） ========================================

        /// <summary>本局是否五段式开局（2026-10-19 掷币演出挪战场）：BattleEntry.StagedOpening 限定
        /// 普通对战（LocalAI）与教学三变体（Tutorial·lunzhuan，均主菜单注入）；2026-10-28 联机
        /// 开局流程改版并入（Network·DeckSubmit 相位进战场即五段式：掷币+网络选卡组面板）——
        /// 教学一二/挑战/测试镜像入口恒 false，立即启动路径零变化。</summary>
        private bool StagedOpeningActive()
            => BattleEntry.StagedOpening
               && (BattleEntry.Mode == BattleMode.LocalAI
                   || BattleEntry.Mode == BattleMode.Network
                   || (BattleEntry.Mode == BattleMode.Tutorial
                       && BattleEntry.TutorialId == TutorialProgressManager.LunzhuanId));

        /// <summary>本地对局启动（原型屏 HandFanScreen 移植接入）：开局即建双扇手牌，引擎手牌事件
        /// 幂等对账刷新。战场 3D 重做前的最小对战面——出牌交互暂缺，回合由「结束回合」推进。
        /// 2026-10-19 拆分：staged 局此处分流至 BattleOpeningDirector（地块→角色→硬币→选卡组
        /// 五段演出），段五回调 EngageStagedBattle；非 staged 局原位直调 EngageLocalBattle（行为零变）。</summary>
        private void StartLocalFan()
        {
            _ctrl = new BattleController();
            var mirrorDeck = BattleEntry.MirrorDeck; // 一次性消费（防跨局残留，同 Client 口径）
            BattleEntry.MirrorDeck = null;
            var mirrorSkill = BattleEntry.MirrorSkillCardId;
            BattleEntry.MirrorSkillCardId = null;
            var challengeDeck = BattleEntry.ChallengeDeck; // 一次性消费（挑战模式玩家卡组）
            BattleEntry.ChallengeDeck = null;
            var challengeSkill = BattleEntry.ChallengeSkillCardId;
            BattleEntry.ChallengeSkillCardId = null;
            var testScenario = BattleEntry.Scenario; // 一次性消费（测试开局预设，2026-10-16）
            BattleEntry.Scenario = null;
            // 对局准备流程五静态口（Match/Ai 卡组+技能+硬币先手）已随 2026-10-19 挪战场整删——
            // 掷币+选卡组在战场五段式开局内发生，结果由 EngageStagedBattle 直喂 EngageLocalBattle。

            if (StagedOpeningActive())
            {
                // 五段式开局（2026-10-19 掷币挪战场）：先战场演出（地块就位→角色就位→掷硬币→
                // 战场选卡组），引擎启动整体推迟到段五——BattleOpeningDirector 段末回调
                // EngageStagedBattle 交班。段一~四无 Core/无事件订阅可撞；段二预置柱由段五首次
                // 对账按名收养；AI 先手 kickoff 随 Engage 尾段（防硬币对方先手局冻结的同款洞）。
                WireLocalUi();
                if (BattleEntry.DummyTest)
                {
                    // 木桩测试（2026-10-22）：进入先不开始——选项面板确认后才走五段式开场
                    StartDummyOpeningAsync().Forget();
                    return;
                }
                _opening = new BattleOpeningDirector(() => _running, _field,
                    BattleEntry.Mode == BattleMode.Tutorial && BattleEntry.TutorialId == TutorialProgressManager.LunzhuanId,
                    EngageStagedBattle);
                _opening.RunAsync().Forget();
                return;
            }

            EngageLocalBattle(mirrorDeck, mirrorSkill, challengeDeck, challengeSkill, testScenario);
        }

        /// <summary>staged 局段五交班（BattleOpeningDirector 回调）：掷币/选卡组结果直喂
        /// EngageLocalBattle；开场导演先退役（指针闸让位）+首刷门随段五起（Core 就位即刷）。
        /// forceMatch=true=对局分支强制（null 卡组随机兜底+硬币先后手+后手 5 护甲；旧对局准备流程
        /// 全随机选择回落无头兜底分支丢硬币结果的潜在洞顺带堵上——兜底分支保留给测试路径）。
        /// myTokenBindings/aiTokenBindings（P9b）：所选卡组的衍生物槽绑定。</summary>
        private void EngageStagedBattle(List<CardData> myDeck, string mySkill, List<CardData> aiDeck,
            string aiSkill, bool? playerFirst,
            List<DeckTokenBinding> myTokenBindings = null, List<DeckTokenBinding> aiTokenBindings = null)
        {
            _opening?.Dispose();
            _opening = null;
            RefreshFieldWhenBoardReady().Forget();
            EngageLocalBattle(null, null, null, null, null, myDeck, mySkill, aiDeck, aiSkill,
                playerFirst, forceMatch: true, myTokenBindings, aiTokenBindings);
        }

        /// <summary>木桩测试开局（2026-10-22）：进入战场先不开始——模态弹选项面板（先后手/洗牌/
        /// 元素池），确认后按选项走五段式开场（固定先后手=开场跳过掷币段；掷硬币=照常）；
        /// 返回主菜单=直接退屏（无引擎已建，OnExit 常规收口即可）。三选项经 BattleEntry
        /// 静态口带给 EngageLocalBattle 木桩分支（段五交班时消费）。</summary>
        private async UniTaskVoid StartDummyOpeningAsync()
        {
            Debug.Log("[木桩] 选项面板已显示（等待确认）");
            // 2026-10-22 根因修复：dead 语义=「已死该退出」，须传 !_running。旧代码照抄
            // BattleOpeningDirector 的 () => _running 惯例（那边内部取反），导致面板刚建好
            // 循环即判死退出→恒走取消分支→当帧 Back 回主菜单（用户所见"点了没反应"）。
            var picked = await DummyOptionsPanel.ShowAsync(() => !_running, Root);
            if (!_running)
            {
                Debug.Log("[木桩] 面板拆屏退出（_running=false）");
                return; // 拆屏（面板随 Root 销毁自清）
            }
            if (picked == null)
            {
                Debug.Log("[木桩] 面板取消——返回主菜单");
                Manager.Back();
                return;
            }
            BattleEntry.DummyPlayerFirst = picked.PlayerFirst;
            BattleEntry.DummyShuffle = picked.Shuffle;
            BattleEntry.DummyFullPool = picked.FullPool;
            Debug.Log("[BattleScreen] 木桩选项：先后手=" + (picked.PlayerFirst == null ? "掷硬币"
                    : picked.PlayerFirst == true ? "我方先手" : "对方先手")
                + " 洗牌=" + picked.Shuffle + " 元素池充满=" + picked.FullPool);
            _opening = new BattleOpeningDirector(() => _running, _field, false, EngageStagedBattle,
                picked.PlayerFirst, dummy: true, overlayParent: Root, onCancelled: () => Manager.Back());
            _opening.RunAsync().Forget();
        }

        /// <summary>本地对局启动后半段（原 StartLocalFan 尾段整体抽出·2026-10-19 五段式开局复用——
        /// 顺序与时序红线零变化）：_log.Wire→WireEarly→模式分支（StartNewGame/教学装配）→Wire→
        /// 扇/Region 接线→补偿刷→AI 先手 kickoff。
        /// myTokenBindings/aiTokenBindings（P9b 2026-10-29）：卡组期衍生物槽绑定——随所用卡组
        /// 的分支注入（match/dummy/挑战/教学注入），镜像/随机无绑定（固定绑定走原子 str）。</summary>
        private void EngageLocalBattle(List<CardData> mirrorDeck, string mirrorSkill,
            List<CardData> challengeDeck, string challengeSkill, TutorialScenario testScenario,
            List<CardData> matchDeck = null, string matchSkill = null, List<CardData> aiDeck = null,
            string aiSkill = null, bool? playerFirst = null, bool forceMatch = false,
            List<DeckTokenBinding> myTokenBindings = null, List<DeckTokenBinding> aiTokenBindings = null)
        {
            // 本地战报桥须先于 StartNewGame 接线（时序红线——详见 BattleLogController.Wire 注释）
            _log.Wire();

            // 抽卡飞入（时序红线——先于 StartNewGame，详见 BattleProjector.WireEarly 注释）
            _projector.WireEarly();

            if (BattleEntry.Mode == BattleMode.Tutorial)
            {
                // 教学局（2026-10-18 教学二含对局准备流程注入的 matchDeck/aiDeck/硬币先手/技能——
                // 必须先于普通 matchDeck 分支接管，否则教学装配（引导+课规+剧本 AI）被匹配分支抢吞）；
                // 装配失败回落普通局（2026-10-18 晚修正）：必须同拍降级 Mode——旧口径回落局仍挂
                // Tutorial 标记，ReportBattleResult 会把这场无引导普通局的胜利误记成过课
                // （真机实录：热更配置档解析失败→Get(basics)=null→随机卡组白板局→打赢即
                // MarkTutorialCompleted，存档污染后教学按钮路由直跳教学二「教学一已通过」）。
                if (!_tutorial.StartGame(_ctrl, matchDeck, aiDeck, playerFirst, matchSkill, aiSkill,
                    myTokenBindings, aiTokenBindings))
                {
                    BattleEntry.Mode = BattleMode.LocalAI; // 回落局不计教学进度（胜负都不落档）
                    _ctrl.StartNewGame(matchDeck, aiDeck,
                        MakePacedAi(aiDeck != null ? AiStrategy.AutoMatch(aiDeck) : null),
                        mySkillCardId: matchSkill, aiSkillCardId: aiSkill,
                        playerFirst: playerFirst, secondArmorBonus: GameCore.SecondPlayerArmorBonus,
                        myTokenBindings: myTokenBindings, aiTokenBindings: aiTokenBindings);
                }
            }
            else if (BattleEntry.DummyTest)
                // 木桩测试局（2026-10-22；2026-10-23 选卡组弹窗+难度三档）：双方卡组由掷币后弹窗选定
                //（我方=玩家档+三套预组；木桩侧限三套主题预组；null=随机在此解析——场景管线的
                // hand/deck 引用须与卡组同源）。对手难度（BattleEntry.DummyDifficulty）：0=木桩（全让过
                // 不出牌）/1=脚本（2026-10-24 接入预组控制脚本：AutoMatch 主题策略——红·快攻→红快攻、
                // 蓝·控制→蓝控制、绿·慢速→绿慢速，预组卡全带各自 Theme 标签精确命中；出牌打法随预组，
                // 响应行为归驱动自身内置启发，不加外部约束）、2=AI（MakePacedAi+AutoMatch 正式对手）；
                // 被动响应器（响应窗全让过）只给木桩。洗牌关=锁牌库序（可复现）；
                // 元素池充满=按所选卡组组「地牌充满」预设（素材缺失回落常规开局）。
                // 后手护甲补偿（2026-10-30 修正）：本入口 2026-10-25 起承载正式本地对战（打AI 归
                // 木桩难度档），旧「纯测试床与正式局差异从简」口径作废——掷币/固定先后手的后手
                // 角色 +5 护甲（GameRules.SecondPlayerArmorBonus 迁表单源），与联机/教学同规则。
                // 例外：地牌充满预设走 scenario 分支（起跳场面自钉），维持测试预设从简不注入。
            {
                // 随机兜底=三套预组随机其一（2026-10-22；玩家池已空）——技能随预组回传
                string myRandomSkill = null, aiRandomSkill = null;
                var myDummy = matchDeck ?? BattleController.RandomDeck(out myRandomSkill);
                var aiDummy = aiDeck ?? BattleController.RandomDeck(out aiRandomSkill);
                string myDummySkill = matchSkill ?? myRandomSkill;
                string aiDummySkill = aiSkill ?? aiRandomSkill;
                bool aiOpponent = BattleEntry.DummyDifficulty == 2;
                bool scriptedOpponent = BattleEntry.DummyDifficulty == 1; // 脚本档：预组打法（行为随驱动自身）
                IAiTurnDriver dummyDriver = (aiOpponent || scriptedOpponent)
                    ? (IAiTurnDriver)MakePacedAi(AiStrategy.AutoMatch(aiDummy))
                    : MakeScriptedDummyAi();
                if (BattleEntry.DummyFullPool
                    && DummyTestFlow.TryBuildFullPool(myDummy, aiDummy, BattleEntry.DummyShuffle,
                        playerFirst != false, out var scen, out var mySeeded, out var aiSeeded))
                    _ctrl.StartNewGame(mySeeded, aiSeeded, dummyDriver,
                        scenario: scen, mySkillCardId: myDummySkill, aiSkillCardId: aiDummySkill);
                else
                    _ctrl.StartNewGame(myDummy, aiDummy, dummyDriver,
                        lockDeckOrder: !BattleEntry.DummyShuffle,
                        mySkillCardId: myDummySkill, aiSkillCardId: aiDummySkill, playerFirst: playerFirst,
                        secondArmorBonus: GameCore.SecondPlayerArmorBonus,
                        myTokenBindings: myTokenBindings, aiTokenBindings: aiTokenBindings);
                if (BattleEntry.DummyDifficulty == 0)
                    _ctrl.EnablePassiveAiResponder(); // 仅木桩：响应窗全让过；脚本/AI 档行为归驱动自身
            }
            else if (matchDeck != null || forceMatch)
                // 对局准备流程（2026-10-19 挪战场五段式开局：掷币+选卡组结果段五注入，旧
                // PreMatchScreen 当日整删）：AI 策略按其卡组自适配（卡组缺=缺省策略）；硬币后手
                // 角色 +5 护甲（StartNewGame 内注入）。forceMatch=staged 局恒走本分支（null 卡组
                // 随机兜底——掷币结果不因全随机选择漏进无头兜底分支）
                _ctrl.StartNewGame(matchDeck, aiDeck,
                    MakePacedAi(aiDeck != null ? AiStrategy.AutoMatch(aiDeck) : null),
                    mySkillCardId: matchSkill, aiSkillCardId: aiSkill,
                    playerFirst: playerFirst, secondArmorBonus: GameCore.SecondPlayerArmorBonus,
                    myTokenBindings: myTokenBindings, aiTokenBindings: aiTokenBindings);
            else if (mirrorDeck != null)
                // 测试镜像局（TestStartFlow btn-teststart 专用载体，2026-10-19 结业镜像局退役后唯一
                // 注入方）：AI 打同卡组（技能标记双方同用——镜像口径）。
                // testScenario 非空=测试开局：镜像局改走教学注入管线
                //（跳起手+Seeder 静默铺场+起跳回合），null=普通镜像开局
                _ctrl.StartNewGame(mirrorDeck, mirrorDeck,
                    aiDriver: MakePacedAi(AiStrategy.RedAggro),
                    scenario: testScenario,
                    mySkillCardId: mirrorSkill, aiSkillCardId: mirrorSkill);
            else if (BattleEntry.Mode == BattleMode.Challenge)
            {
                // 挑战模式（2026-10-06 单值联动）：难度 N = 电脑地牌槽 9+N、起手 6+N；
                // AI 卡组缺省随机 30 张，策略按卡组主色自适配（AutoMatch：标签优先回落费用主色）
                // 技能（2026-10-07 卡牌化）：玩家=卡组标记；AI 随机卡组自动挑一张合格结界
                var difficulty = Mathf.Clamp(BattleEntry.ChallengeDifficulty, 1, ChallengeProgressManager.MaxDifficulty);
                BattleEntry.ChallengeDifficulty = difficulty;
                var challTokens = BattleEntry.ChallengeTokenBindings; // 一次性消费（同 ChallengeDeck 口径）
                BattleEntry.ChallengeTokenBindings = null;
                string challAiRandomSkill;
                var challAiDeck = BattleController.RandomDeck(out challAiRandomSkill); // 三套预组随机其一（玩家池已空）
                _ctrl.StartNewGame(challengeDeck, challAiDeck, MakePacedAi(AiStrategy.AutoMatch(challAiDeck)),
                    aiLandCapBonus: difficulty, aiExtraOpeningDraws: difficulty,
                    mySkillCardId: challengeSkill,
                    aiSkillCardId: !string.IsNullOrEmpty(challAiRandomSkill)
                        ? challAiRandomSkill
                        : HeroSkillSystem.AutoPickSkillCard(challAiDeck),
                    myTokenBindings: challTokens); // P9b 挑战卡组绑定
            }
            else
                // 普通本地局兜底（2026-10-13 补）：LocalAI 无镜像时原链无分支命中=空战场不 InitGame
                // 2026-10-14 人机节奏定案：P2 走等待模式——每个动作等表现层演完再执行下一个
                _ctrl.StartNewGame(aiDriver: MakePacedAi());

            // 对账订阅表（2026-10-18 B2 投影单点化——唯一投影器 Battle/BattleProjector：
            // 事件→置脏/演出载荷钩子/终局流转；本屏不再直订引擎对账事件）
            _projector.Wire();

            WireLocalUi(); // 扇/Region/响应窗接线（2026-10-19 抽出——原位调用，行为零变）

            RefreshFans();
            RefreshField(); // 补偿刷（2026-10-13）：订阅注册前已发生的静默变化（教学场景注入零事件）兜底
            _tutorial.RefreshBanner(); // 教学首步文案（Begin 在 StartGame 内已触发过事件，此处补首帧）

            // 硬币 AI 先手（2026-10-18 修）：开局即对手回合——AI 驱动此前只挂在玩家结束回合链上
            //（OnEndTurnFan），AI 先手时首回合无人驱动=冻结在 P2 主阶段（教学二幻卡清算/普通硬币
            // 对局同洞）。放在订阅齐备之后 kickoff，AI 动作的视图对账已接线；教学介绍段（intro
            // 面板）不拦引擎回合——幻卡清算会在首张介绍卡背后完成，确认后即可见末日降临。
            if (_ctrl != null && _ctrl.TurnPlayer == P2 && !_gameEnded)
                RunAiFirstTurnAsync().Forget();

            Debug.Log("[BattleScreen] 本地对局已启动（扇形手牌过渡面；战场 3D 重做中）");
        }

        /// <summary>引擎无关接线（原 StartLocalFan 接线段抽出·2026-10-19 五段式开局复用，幂等）：
        /// 手牌扇交互/Region 目标选择器/响应窗口注册。staged 局在开场演出前先接（扇区/选择器就绪，
        /// 演出期无引擎请求在途）；非 staged 局保持原位（StartNewGame 后）调用。</summary>
        private void WireLocalUi()
        {
            // 手牌扇交互接线（2026-10-14）：悬停显「放置/使用/特殊使用」按钮+可发动流光（对手扇不注入=无按钮）
            _selfFan?.SetInteractions(new FanInteractions
            {
                CanUse = FanCanUse,
                CanPlaceLand = FanCanPlaceLand,
                CanSpUse = FanCanSpUse,
                OnUse = OnFanUse,
                OnPlaceLand = OnFanPlaceLand,
                OnSpUse = OnFanSpUse,
            });

            // 通用目标选择器（效果引擎结算期反问）：Region 面板选卡（2026-10-15 用户定案）。
            // 修复（2026-10-15）：此前注册只写在无人调用的 StartLocal——真实启动路径 StartLocalFan
            // 漏注册，结算期反问恒 ui=null→auto 静默代选（磨刀兽「没让选自动挑目标」根因）。
            // 2026-10-16 定案：UiTargetSelector 旧列表弹窗删除——不再作 Region 缺失兜底
            //（缺 Region=无交互反问，Service 自动代选）；法术声明期目标预选同批退役，
            // 指向性法术目标统一走结算期反问（与生物/角色技能同 UI 同源）。
            _regionSelector = _regionPanelCtl != null
                // 2026-10-16：boardCellOf=场上候选红瞄准面片；isLocalPlayer=角色候选【己方/对方角色】判侧（本地局人类恒 P1）；
                // roleCellOf=角色候选的红瞄准面片落格（选中才上盘·2026-10-16 晚定案）
                // buildPrompt/onCancelAvailable（2026-10-17 label-num 并入定案）：开窗=提示全文（完整
                // 效果文）直写 Region 面板 label-num；主动效果亮「取消」——详见 BuildSelectPrompt/OnSelectCancel
                ? new RegionTargetSelector(_regionPanelCtl.PanelNode, BoardCellOf, p => p == P1, RoleCellOf, BuildSelectPrompt, _decision.OnSelectCancel)
                : null;
            TargetSelectionService.Current = _regionSelector;

            // 移动原子选格反问桥（2026-10-31 场面轴3c）：结算期 MoveUnit handler 经此弹格——
            // 与召唤选格同交互；卸载随本界面清理段（AI/无头无桥=安全空转）
            CellSelectionService.Current = PickMoveDestinationAsync;

            // 响应窗口（2026-10-14 接回）：守卫候选走棋盘箭头交互（效果类候选回落弹窗）
            ResponseWindowService.HumanResponder = _decision.ShowResponseAsync;
        }

        /// <summary>开局即 AI 回合的驱动（硬币对方先手，2026-10-18）：跑完对手首回合后全量对账
        /// ——尾段与 OnEndTurnFan 同口径（RefreshLocal）。</summary>
        private async UniTaskVoid RunAiFirstTurnAsync()
        {
            if (_ctrl == null) return;
            await _ctrl.RunAiTurnAsync();
            RefreshLocal();
        }

        /// <summary>目标候选的场上占格（2026-10-16 红瞄准面片）：BattleFieldView 单源查询——
        /// 单位（生物/结界/衍生物）与施放占格类型图标在场上即返回数据格；不在场（手牌/墓地/牌库等）
        /// 返回 null（无面片）。纯读对账记账，不触引擎。</summary>
        private (int x, int z)? BoardCellOf(Card card)
        {
            if (_field != null && _field.TryGetOnBoardCell(card.RuntimeId, out int x, out int z))
                return (x, z);
            return null;
        }

        /// <summary>角色候选的红瞄准面片落格（2026-10-16 晚定案·选中才上盘）：引擎单源
        /// BoardState.CharacterCell（角色格布局常量，角色立柱同格）；控制器未挂板=无面片。</summary>
        private (int x, int z)? RoleCellOf(Player p)
        {
            var board = _ctrl?.Board;
            if (board == null || p == null) return null;
            var cell = board.CharacterCell(p);
            return (cell.x, cell.z);
        }

        /// <summary>结束己方回合并让 AI 跑完对手回合（手牌变化经事件链刷新扇区）。</summary>
        private async void OnEndTurnFan()
        {
            if (NetActive) { NetEndTurn(); return; } // 网络局：intent 上行（对手回合由真人对手驱动）
            if (_gameEnded || Core == null || _ctrl.TurnPlayer != P1) return;
            if ((_field != null && _field.VisualsBusy) // 表演中不许结束回合（2026-10-17）：演出与回合状态撕裂防护
                || (_director != null && _director.Busy)) // 步进回放在途（2026-10-19）：表现层未追平引擎真值
            {
                _toast.Show("战斗演出进行中——稍后再结束回合");
                return;
            }
            if (!GameActions.EndTurn(Core, P1))
            {
                // 2026-10-17：此前静默吞（连锁窗在途时栈空守卫拒绝→玩家只见「点了没反应」）——
                // 给可读反馈：优先提示有待处理窗口，否则按栈未清口径
                _toast.Show(_regionSelector != null && _regionSelector.IsOpen
                    ? "有响应/选择窗口待处理——先选卡或让过"
                    : "连锁尚未结算——暂时无法结束回合");
                return;
            }
            // 回合切换延迟一拍生效（EndTurn 排队推进、引擎泵下一帧才翻 TurnPlayer——同帧读
            // 仍 P1，曾因此从不驱动 AI 回合＝「结束回合后电脑不动」，2026-10-14）：
            // 先等翻到 P2 再交 AI 驱动。等待预算 1 秒防卡——但回合末反问（手牌上限弃牌等）开窗
            // 期间折返门（GameLoopController 等 PendingRequests==0）刻意挂起，玩家选卡远超 1 秒：
            // 反问在途不烧预算（2026-10-15 修复——旧版 60 帧后放弃等待，弃牌选完后 AI 回合
            // 无人驱动，整局冻结在对手主阶段）；反问自身有超时代选安全网，无无限等待风险。
            int idle = 0;
            while (!_gameEnded && Core != null && _ctrl.TurnPlayer != P2)
            {
                if (CardCore.TargetSelectionService.PendingRequests > 0) idle = 0;
                else if (++idle > 60) break;
                await Cysharp.Threading.Tasks.UniTask.Yield();
            }
            if (!_gameEnded && _ctrl.TurnPlayer == P2)
                await _ctrl.RunAiTurnAsync();
        }

        /// <summary>人机节奏驱动（2026-10-14 用户定案）：人类在场的分支（普通/镜像/挑战）给 P2 的
        /// 脚本 AI 注入 PacingWait——电脑每个动作后等表现层视觉在途门归零（等一件事演完再做下一件）
        /// + 最小间隔拍（2026-10-16 用户定案：1 秒）。AI-vs-AI（编辑器批量/无头）
        /// 不注入=全速，完全不管渲染层。2026-10-16 出牌舞台：同批注入开演/收幕回调——
        /// 对手出牌先亮（左上角卡面+指向箭头）1 秒再结算。</summary>
        private SimpleAI MakePacedAi(AiStrategy strategy = null)
        {
            return new SimpleAI(strategy)
            {
                PacingWait = async () =>
                {
                    if (_field != null) await _field.WaitVisualsIdleAsync();
                    if (_director != null) await _director.WaitIdleAsync(); // 步进回放追平才许下一个动作（2026-10-19）
                },
                OnCastStaged = _castStage.Show,
                OnCastStageEnd = _castStage.Hide,
            };
        }


        /// <summary>木桩驱动（2026-10-23 替 DummyAi；2026-10-24 起仅难度 0 木桩档使用——脚本档已接
        /// 预组主题策略见 EngageLocalBattle 木桩分支）：ScriptedAi 无剧本口径（跳产元素→排干栈→让过
        /// ——每回合行为与旧木桩一致），PacingWait 与 MakePacedAi 同拍；静默无剧本告警。</summary>
        private ScriptedAi MakeScriptedDummyAi()
            => new ScriptedAi(null)
            {
                WarnWithoutScript = false,
                PacingWait = async () =>
                {
                    if (_field != null) await _field.WaitVisualsIdleAsync();
                    if (_director != null) await _director.WaitIdleAsync();
                },
            };

        /// <summary>终局弹窗（扇形过渡面口径；旧链 OnGameOver 的 RefreshLocal 依赖已删预制体字段，不走）。
        /// 2026-10-16 弹窗退役：终局呈现=toast+战报行，退出=认输键改「返回主菜单」直回（MarkGameOverUi）。
        /// 2026-10-17 教学败局追加：考验「打输重来」——弹「是否重新进行」，确认=终局撤销+midTransition
        /// 全清重铺（援军登场+生命 20/20 重置（2026-10-17 晚改值））原地续打（用户剧本：打赢就过，打输重来）。</summary>
        private void OnGameOverFan(GameOverEvent e)
        {
            _gameEnded = true;
            // 脚本化续接（2026-10-19 教学二多段化）：上段考验的预设击杀（引导未完、当前步非 victory）
            // ——终局弹窗/过课落档全跳过，冻结交互等换场步异步续接（终局撤销+重铺下一段）。
            if (BattleEntry.Mode == BattleMode.Tutorial && _tutorial != null && _tutorial.IsScriptedContinuation)
                return;
            bool win = e.Winner == P1;
            TutorialFlow.ReportBattleResult(BattleEntry.Mode, win); // 教学逻辑层：考核局/教学局结果落档
            MarkGameOverUi($"{(win ? "胜利" : "失败")}——{(win ? "我方" : "对手")}获胜（{e.Reason}）");
            _tutorial.PromptBattleOver(win); // 教学终局分发（Retry/Complete——BattleTutorialDirector·2026-10-18 拆分）
        }

        /// <summary>终局呈现（2026-10-16 旧弹窗退役）：提示走 toast+战报；认输键文案改「返回主菜单」
        ///（OnConcede 终局分支直回——旧弹窗取消键职责迁移，btn-back 已退役本屏无其他退出口）。</summary>
        private void MarkGameOverUi(string msg)
        {
            _toast.Show(msg);
            _log.Append(msg, "log-line--system");
            if (_btnConcede != null)
            {
                var lbl = _btnConcede.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = "返回主菜单";
            }
        }

        /// <summary>抽卡飞入武装（2026-10-17 抽卡演出）：抽牌对账前把卡堆顶世界坐标注入对应
        /// 手牌扇（新卡从卡堆弧线飞入扇位）+ 卡堆 XZ 脉冲。起手抽牌（_fanFlyArmed=false）与
        /// 教学介绍段（SuppressHandFan——防起点滞留到解除后的对账误飞）不注入；卡堆/相机/
        /// 坐标转换任一失败静默=新建卡维持原吸附行为。</summary>
        private void ArmDrawFlyIn(Player player)
        {
            if (!_fanFlyArmed || BattleRevealDirector.SuppressHandFan) return;
            int owner = player == P2 ? 1 : 0;
            var fan = owner == 0 ? _selfFan : _oppFan;
            if (fan == null) return;
            var cam = BattleStageDirector.WorldCam;
            if (cam == null || !BattleZoneProps.DeckTopWorldPosition(owner, out var top)) return;
            if (fan.SetDrawOriginFromWorld(top, cam))
                BattleZoneProps.PulseDeck(owner);
        }

        /// <summary>幂等全量对账：己方手牌全正面；对手手牌默认背面，带 Exposed 指示物的翻正面。
        /// 教学介绍段闸（2026-10-17 语音联动显隐）：介绍到费用段前手牌区保持空。</summary>
        private void RefreshFans()
        {
            if (BattleRevealDirector.SuppressHandFan) return;
            if (Core?.ZoneManager == null || P1 == null || P2 == null) return;

            var self = new List<HandFanCardData>();
            foreach (var card in Core.ZoneManager.GetCards(P1, Zone.Hand))
            {
                self.Add(new HandFanCardData
                {
                    RuntimeId = card.RuntimeId,
                    // FromHand（六区口径+RuntimeCard 事件锚）；FromBattle 只填旧版 Stats/Keywords
                    // 纯文本，ACardView 不消费——2026-10-15 修复手牌属性/关键词芯片恒空白
                    Item = CardOverlayItem.FromHand(BattleView.FromRuntime(
                        SerializableRuntimeCardState.FromCard(card), card, ownerIsOpponent: false)),
                    FaceUp = true,
                });
            }

            var opp = new List<HandFanCardData>();
            foreach (var card in Core.ZoneManager.GetCards(P2, Zone.Hand))
            {
                bool exposed = RevealRules.IsExposed(card); // 信息轴「展示」：对双方公开
                opp.Add(new HandFanCardData
                {
                    RuntimeId = card.RuntimeId,
                    Item = exposed
                        ? CardOverlayItem.FromHand(BattleView.FromRuntime(
                            SerializableRuntimeCardState.FromCard(card), card, ownerIsOpponent: true))
                        : BlankBack(card.RuntimeId),
                    FaceUp = exposed,
                });
            }

            // 牌库顶当手牌（2026-10-29 阶段3b）：光环命中己方且牌库非空 → 手牌扇末尾显示牌库顶
            // 虚拟手牌（真实卡实例——出牌走 fromZone=Deck 授权路径；打出后顶牌自动换新。
            // 不计 7 张上限/不可弃/不可放地——放地按钮经 FanCanPlaceLand 手牌区限定；
            // 对手扇不显示=天然对手不可见）。
            var deckTop = TopDeckVirtualCard();
            if (deckTop != null)
                self.Add(new HandFanCardData
                {
                    RuntimeId = deckTop.RuntimeId,
                    Item = CardOverlayItem.FromHand(BattleView.FromRuntime(
                        SerializableRuntimeCardState.FromCard(deckTop), deckTop, ownerIsOpponent: false)),
                    FaceUp = true,
                });

            _selfFan?.SetCards(self);
            _oppFan?.SetCards(opp);
            EvaluateFanPlayability(); // 可发动流光随对账刷新（费用/上限/阶段变化即时反映）
        }

        /// <summary>牌库顶虚拟手牌（2026-10-29 阶段3b，本地局）：TopDeckAsHandAura 命中己方且
        /// 牌库非空 → 牌库顶卡（列表 index0——牌库序=牌库顶）；null=不显示（光环未开/牌库空/
        /// 网络局——快照不承载牌库序，联机侧暂不显示，随协议补字段接 BuildNet）。</summary>
        private Card TopDeckVirtualCard()
        {
            if (NetActive || Core?.ZoneManager == null || P1 == null) return null;
            if (!CardCore.RuleAuraSystem.ScopeHits(CardCore.RuleAuraComponents.TopDeckAsHandAura, P1))
                return null;
            var deck = Core.ZoneManager.GetCards(P1, Zone.Deck);
            return deck != null && deck.Count > 0 ? deck[0] : null;
        }

        /// <summary>背面卡的空内容（Key 仅为口径完整；文字被 back 覆盖不可见）。</summary>
        private static CardOverlayItem BlankBack(uint runtimeId)
            => new CardOverlayItem { Key = "R" + runtimeId };

        // ======================================== 战场 3D 摆位（2026-10-13 战场重做） ========================================

        /// <summary>摆位刷新（转发投影器——构建/扇出/版本所有权单点在 BattleProjector）。</summary>
        private void RefreshField() => _projector.RefreshField();

        /// <summary>渲染扇出（投影器 onApplied 钩子）：3D 对账+连锁链+2D 信息栏+Region+三键缓存置脏。</summary>
        private void ApplyProjection(BattleViewData d)
        {
            _field?.Apply(d);
            ReconcileDeckMarks(); // 牌库标记常驻氛围（2026-10-26）：与卡堆高度同链对账——标记在库即亮、抽走即灭
            _chainStage?.Refresh(d); // 连锁链克隆对账（2026-10-19：结算期只增不删——保持到逆结算完成；
                                     // 施放卡面呈现归链，左上角 cast-stage 已退役）
            RefreshBars(); // 2D 信息栏随 3D 对账同频刷新
            RefreshRegion(); // Region 区域面板开着则同频重列（区域变化走本刷新链）
            _activateFlagsDirty = true; // 三键效果缓存置脏：单位清单/效果定义可能已变（2026-10-18 性能）
        }

        /// <summary>2D 信息栏刷新（2026-10-13 补回）：双方数据条+中栏文本+操作按钮态。
        /// 控件缺失全静默（?. / null 判空——prefab 未补齐时零炸）。</summary>
        private void RefreshBars()
        {
            var d = _view;
            if (d == null) return;
            FillBar(d.Opp, opp: true);
            FillBar(d.Self, opp: false);
            if (_lblPhase != null) _lblPhase.text = d.PhaseText;
            if (NetActive) BattleHudRow.SetPhase(PhaseTileText(d), d.MyTurn); // 网络局维持快照喂数（联机时序另立任务）
            else BattleHudRow.ReconcilePhase(PhaseTileText(d), d.MyTurn); // 本地局正源=相位事件序（OnPhaseAnnounced 入队）；对账仅队列空闲时直写兜底（开局种子/教学换场自愈）
            _hudTicker.RefreshChainSpeed();                 // 连锁速度格（中线 x3）=引擎记速器实值（2026-10-15 接线）
            if (_lblStack != null) _lblStack.text = string.Join(" ｜ ", d.StackLines);
            if (_lblPriority != null)
                _lblPriority.text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";
            UpdateButtons(d);
        }


        /// <summary>首刷就绪门（2026-10-13 教学局首刷竞速修复）：棋盘+对局核心齐备——
        /// 首刷曾只等棋盘就绪，会跑赢 StartNewGame（教学场景注入零事件、TurnStart 早于订阅注册），
        /// Core 未就位静默白刷且此后无补偿，预设单位永不渲染。</summary>
        private bool BoardReadyForFirstPaint()
            => BattleBoardGrid.Ready && Core != null && P1 != null && P2 != null;

        /// <summary>装台完成后首刷：就绪前 Apply 会自跳过，这里只负责"就绪即补一刷"
        /// （空场英雄柱/初始摆位）；之后一切变化走事件订阅路径。</summary>
        private async UniTaskVoid RefreshFieldWhenBoardReady()
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (_running && !BoardReadyForFirstPaint() && Time.realtimeSinceStartup < deadline)
                await UniTask.Yield();
            Debug.Log($"[BattleField] 首刷触发（grid={BattleBoardGrid.Ready}, core={Core != null}, running={_running}）");
            try
            {
                RefreshField();
            }
            catch (Exception ex)
            {
                Debug.LogError("[BattleField] 首刷异常：" + ex);
            }
        }

        // ---- 出场粒子汇聚载荷组装（2026-10-15 用户定案：实付颜色口径；浓度 ∝ 费用） ----

        private void CapturePaid(ElementPoolPayEvent e)
        {
            int seat = e.Player == P1 ? 0 : 1;
            var paid = new Dictionary<int, float>();
            if (e.PaidCost != null)
                foreach (var kv in e.PaidCost)
                    if (kv.Value > 0.0001f) paid[kv.Key] = kv.Value;
            _lastPaidBySeat[seat] = paid;

            // 法术扣费粒子（2026-10-20 用户定案「沿用生物召唤元素粒子飘向使用区域」）：
            // 打出实付 + 付费者发动区是法术卡 → 实付色粒子自地格汇聚至施放贴片格。
            // 生物不动（CastPlayed 入场汇聚 EnqueueArrival 已覆盖，防双份）；效果费/技能费
            // SourceNote 非「打出:」口径天然排除。
            if (paid.Count > 0 && e.SourceNote != null && e.SourceNote.StartsWith("打出:", StringComparison.Ordinal))
            {
                var spell = (Core?.ZoneManager?.GetCards(e.Player, Zone.Activation) ?? new List<Card>())
                    .FirstOrDefault(c => c != null && c.IsSpellCard());
                if (spell != null && _ctrl?.Board != null
                    && _ctrl.Board.TryGetCastingCell(spell, out int cx, out int cz))
                    _field?.PlayCastPay(seat, paid, cx, cz);
            }
        }

        /// <summary>CastPlayed 入场 → 取该 seat 最近实付账单组装配荷；无账单/零费回落卡面印刷费色，
        /// 仍无则不登记（模型即时显现）。付费→入场为同步紧邻事件；触发器付费插队=颜色近似（已注释知）。</summary>
        private void CaptureArrival(CardPutToBattlefieldEvent e)
        {
            if (e.Source != EnterSource.CastPlayed || _field == null) return;
            int seat = e.Controller == P1 ? 0 : 1;

            Dictionary<int, float> paid;
            if (_lastPaidBySeat.TryGetValue(seat, out var last) && last.Count > 0)
            {
                paid = last;
            }
            else
            {
                paid = new Dictionary<int, float>();
                var cost = GameActions.GetCardCost(e.Card); // 印刷费全口径（抉择/指示物层同引擎单源）
                foreach (var m in cost.NonzeroColors())
                    paid[(int)m] = cost[m];
            }

            if (paid.Count > 0)
                _field.EnqueueArrival(e.Card.RuntimeId, seat, paid);
        }

        /// <summary>轻刷（转发投影器——构建+仅 2D 信息栏，原口径见 BattleProjector.RefreshLocal）。</summary>
        private void RefreshLocal() => _projector.RefreshLocal();

        // ======================================== 全量渲染（两种模式统一输入 BattleViewData） ========================================

        private void RefreshView()
        {
            var d = _view;
            if (d == null) return;

            // 信息栏
            FillBar(d.Opp, opp: true);
            FillBar(d.Self, opp: false);

            // 战场=3D 层渲染（BattleFieldView 对账）；旧 2D 棋盘六行/手牌条/卡牌层挂载链已退役
            //（2026-10-14 晚拆除——ACard 实例只出自手牌扇区/Region 列卡/prefab 预览位）

            // 中栏（2026-10-13 基础教学：极简 prefab 无中栏节点——null 判空，缺件零炸）
            if (_lblPhase != null) _lblPhase.text = d.PhaseText;
            if (NetActive) BattleHudRow.SetPhase(PhaseTileText(d), d.MyTurn); // 网络局维持快照喂数（联机时序另立任务）
            else BattleHudRow.ReconcilePhase(PhaseTileText(d), d.MyTurn); // 本地局正源=相位事件序（OnPhaseAnnounced 入队）；对账仅队列空闲时直写兜底（开局种子/教学换场自愈）
            _hudTicker.RefreshChainSpeed();                 // 连锁速度格（中线 x3）=引擎记速器实值（2026-10-15 接线）
            if (_lblStack != null) _lblStack.text = string.Join(" ｜ ", d.StackLines);
            if (_lblPriority != null)
                _lblPriority.text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";

            // 我方手牌=扇区（RefreshFans 对账），旧手牌条退役
            UpdateButtons(d);
        }

        private void FillBar(BattlePlayerView p, bool opp)
        {
            string aiTag = p.IsAI ? "（AI）" : "";
            // 生命标签走节拍展示值（2026-10-20 角色HP接入命中节拍）：攻击步冻结期角色柱缓冲终值，
            // 这里取当前展示值（旧值）防快照直出；未建柱/首写前回落数据值
            int lifeShown = p.Life;
            if (_field != null && _field.TryGetHeroLifeShown(opp ? 1 : 0, out var shown)) lifeShown = shown;
            if (opp)
            {
                if (_oppName != null) _oppName.text = $"对手{aiTag}";
                if (_oppLife != null) _oppLife.text = $"生命 {lifeShown}/{p.MaxHealth}";
                if (_oppDeck != null) _oppDeck.text = $"牌库 {p.DeckCount}";
                if (_oppHand != null) _oppHand.text = $"手牌 {p.HandCount}";
                if (_oppGrave != null) _oppGrave.text = $"墓地 {p.GraveyardCount}";
                if (_oppFatigue != null) _oppFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                if (_oppBank != null) _oppBank.text = $"地牌上限 {p.LandCap} · 临时元素 {p.BankText}";
            }
            else
            {
                if (_selfName != null) _selfName.text = "我方";
                if (_selfLife != null) _selfLife.text = $"生命 {lifeShown}/{p.MaxHealth}";
                if (_selfDeck != null) _selfDeck.text = $"牌库 {p.DeckCount}";
                if (_selfHandCount != null) _selfHandCount.text = $"手牌 {p.HandCount}";
                if (_selfGrave != null) _selfGrave.text = $"墓地 {p.GraveyardCount}";
                if (_selfFatigue != null) _selfFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                if (_selfBank != null) _selfBank.text = $"地牌上限 {p.LandCap} · 临时元素 {p.BankText}";
            }
        }

        /// <summary>角色柱HP读数落地回调（BattleFieldView 命中节拍释放时触发，2026-10-20）：
        /// 只同拍重写 2D 生命标签（FillBar 常规刷新走节拍展示值，这里补命中拍这一刻的即时跟进）。</summary>
        private void OnHeroLifeShownChanged(int owner, int life)
        {
            var d = _view;
            if (d == null) return;
            var p = owner == 1 ? d.Opp : d.Self;
            if (owner == 1) { if (_oppLife != null) _oppLife.text = $"生命 {life}/{p.MaxHealth}"; }
            else { if (_selfLife != null) _selfLife.text = $"生命 {life}/{p.MaxHealth}"; }
        }

        private void UpdateButtons(BattleViewData d)
        {
            bool myMain = d.MyTurn && d.Phase == PhaseType.Main && !_gameEnded;

            // _btnGrave 已随 2026-10-30 归土改版整删（墓地视手牌使用退役）
            // _btnSkill/_btnPass 已随 2026-10-18 网络簇整删退役（技能键早已无节点恒 null；让优先权=网络专属）
            // 认输键终局后仍可点=返回主菜单出口（2026-10-16 弹窗退役：旧弹窗取消键职责迁移至此）
            if (_btnConcede != null) _btnConcede.interactable = true;
        }

        // ======================================== 操作派发（本地直调 GameActions / 网络转 intent） ========================================

        private bool GateMyMain()
        {
            if (_gameEnded || _view == null) return false;
            if (IsTargetSelecting()) return false; // Region 抉择/反问在身不开新交互（非模态面板须自防重入，静默）
            if (!_view.MyTurn || _view.Phase != PhaseType.Main)
            {
                // 静默 return 会让点击毫无反馈（联机反馈"使用糊"的主因之一）——给一句可读提示
                _toast.Show(!_view.MyTurn ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return false;
            }
            return true;
        }

        private static bool IsCreatureCard(BattleCardView card)
        {
            return DataOf(card)?.Supertype == Cardtype.Creature;
        }

        private static int GetModeCount(BattleCardView card)
        {
            var data = DataOf(card);
            if (data == null) return 1;
            // P12 抉择默认化：法术模式数=非充能主动效果数（充能不进模式）——CostDerivationService 单源；
            // 永久物模式已随内嵌 Choice 退役恒 1（多模式生物/结界不再存在）
            return CostDerivationService.GetModeCount(data);
        }

        /// <summary>卡的静态数据：本地=引擎对象直读；网络=CardId 查本地卡表（引擎对象缺失）。</summary>
        private static CardData DataOf(BattleCardView card)
        {
            if (card.CoreCard != null) return (card.CoreCard as CardWrapper)?.GetData();
            return string.IsNullOrEmpty(card.CardId) ? null : CardCatalog.GetByInstanceId(card.CardId);
        }

        /// <summary>抉择模式窗（定案时序：先选模式→验费用→目标→出牌）。网络侧费用校验交服务器（Error 反馈）。
        /// 法术（2026-10-15 改版）：modeIndex=施放时效果抉择——选项=各主动效果显示名+现付费用
        ///（GetCardCost 单源含所选效果锚价），标题「选择要发动的效果」。
        /// 2026-10-16 弹窗退役：选项=Region 面板卡牌列表（PickRegionOptionAsync，min=1 强制选）。</summary>
        private async UniTaskVoid PromptModeThenPlay(BattleCardView card)
        {
            var data = DataOf(card);
            bool isSpell = data?.Supertype == Cardtype.Spell;
            var labels = isSpell ? SpellEffectLabels(data) : FindChoiceLabels(data);
            int modeCount = isSpell
                ? Math.Max(2, CostDerivationService.GetModeCount(data)) // P12：非充能主动效果数
                : Math.Max(2, CostDerivationService.GetModeCount(data));

            var options = new List<string>();
            for (int i = 0; i < modeCount; i++)
            {
                string name = i < labels.Count && !string.IsNullOrEmpty(labels[i]) ? labels[i] : $"模式 {i + 1}";
                // 运行口径费（GetCardCost，法术含所选效果锚价）
                options.Add($"{name}（费用 {CostText(LocalCostOf(card, i))}）");
            }

            int? picked = await PickRegionOptionAsync(isSpell ? "选择要发动的效果" : "选择模式", options);
            if (picked == null) return; // 缺 Region=无交互（本已点「发动」，静默取消）
            int idx = picked.Value;

            var core = Core;
            // 自动横置口径（2026-09-30）：bank 不足但地牌可产所需元素亦放行——付费步自动横置
            if (core != null && !core.ElementPool.CanPayCostWithAutoTap(
                    GameActions.GetCardCost(card.CoreCard, idx), P1))
            {
                _toast.Show("费用不足——该作用不可发动");
                RefreshLocal();
                return;
            }

            // 法术：自动落位（B4）直入声明流；永久物抉择照旧直出选格
            if (isSpell)
                BeginSpellCastAutoPlace(card, card.CoreCard, idx);
            else
                PlayCardFinal(card, null, idx);
        }

        /// <summary>法术各效果的显示名（抉择窗选项；DisplayName 空=「作用 N」）。</summary>
        /// <summary>法术各可施放效果的显示名（模式窗选项；P12：过滤充能效果——充能走特殊使用入口，
        /// 不算法术模式；DisplayName 空=「作用 N」）。</summary>
        private static List<string> SpellEffectLabels(CardData data)
        {
            var labels = new List<string>();
            if (data?.Effects == null) return labels;
            int i = 1;
            foreach (var eff in CostDerivationService.GetCastableEffects(data))
            {
                labels.Add(string.IsNullOrEmpty(eff?.DisplayName) ? $"作用 {i}" : eff.DisplayName);
                i++;
            }
            return labels;
        }

        private static ElementCost LocalCostOf(BattleCardView card, int modeIndex)
        {
            if (card.CoreCard == null) return null;
            return GameActions.GetCardCost(card.CoreCard, modeIndex);
        }

        private static string CostText(ElementCost cost)
            => cost == null || cost.IsZero ? "0" : BattleView.CostTextOf(cost);

        /// <summary>出牌终态：PlayCard+响应窗口泵（2026-10-18 网络簇整删——intent 上行分支随网络簇退役）。
        /// 2026-10-20 法术改版：法术经 SpellDeclarationFlow 进入（声明期预选目标+贴片预览+可取消撤销），
        /// 本口仍是终态单点（法术带 targets、生物/结界 targets=null 照旧）。
        /// awakenCast（2026-10-29 P6）：苏醒施放——仅手牌路径（墓地视手牌使用无苏醒变体）。
        /// 返回声明成败——法术流的乐观 UI（藏卡+转正贴片）由调用方按 false 清理（拒绝无引擎事件，
        /// 扇区/贴片不会随后续对账自愈）。</summary>
        private bool PlayCardFinal(BattleCardView card, List<Entity> targets, int modeIndex, bool awakenCast = false)
        {
            var core = Core;
            if (core == null || P1 == null) return false;

            // 2026-10-16 定案（法术目标口径已被 2026-10-20 改案取代）：指向性法术目标统一在
            // 声明期收集（SpellDeclarationFlow→CollectSpellDeclarationTargetsAsync——可取消=撤销发动）；
            // defer 原子（效果处理时选）结算期反问（必须选）；牌库等隐藏区/区域自结算类效果
            // 不弹选直接结算（引擎 None 模式早退单源）。
            string reject = null;
            // 出牌来源区（2026-10-29 阶段3b 牌库顶当手牌）：牌库顶虚拟手牌打出走 fromZone=Deck
            //（TopDeckPlaySource/IPlaySource 授权）；其余路径恒手牌（墓地施放已随 2026-10-30 归土改版退役）。
            bool ok = GameActions.PlayCard(core, P1, card.CoreCard, targets,
                card.CoreCard != null && card.CoreCard.GetZone() == Zone.Deck ? Zone.Deck : Zone.Hand,
                modeIndex, awakenCast, out reject);
            if (ok)
            {
                // 出牌选格钉提前到声明成功即转 BoardState（2026-10-15 发动格拆除）：施放类型图标=玩家所选格，
                // 入场经 AssignCells 钉优先原地转正——同钉单源（旧「入场事件补钉」机制随施放占格退役）
                if (_pendingPins.Remove(card.RuntimeId, out var pin))
                    _ctrl?.Board?.PinCard(card.CoreCard, pin.x, pin.z);
                _ctrl.SettleResponseWindow().Forget();
            }
            else
            {
                _pendingPins.Remove(card.RuntimeId); // 声明失败：选格单作废
                // 精确拒因直读（同网络 Error 帧口径——通用文案无法区分 费用/目标/代价，2026-09-26）
                _toast.Show($"无法打出（{reject ?? "费用/条件不满足"}）");
            }
            RefreshLocal();
            return ok;
        }

        // LandFlow（手牌直点放地的旧选色弹窗链）已随 2026-10-16 弹窗退役整删：放地现役入口=
        // OnFanPlaceLand 落格箭头（同格多候选色→PickLandColorAsync 走 Region 卡牌列表）。

        // ---- 法术发动声明流（2026-10-20 改案；2026-10-22 B4 反馈修订：恒自动落位→选目标（可取消）→连锁检测→扣费→效果） ----

        /// <summary>法术发动起手（2026-10-22 玩家反馈 B4 修订：手选发动位退役——法术发动位无战略
        /// 语义（施放占格临时），恒自动落位首空格，与墓地路径 PreviewCastingCell / 网络局服务器
        /// first-free 同口径；仅满区拦截。）：登记选格单（PlayCardFinal 声明成功即 PinCard——
        /// PlaceCasting 钉优先落同格）→SpellDeclarationFlow（贴片预览+声明期目标收集，可取消）。
        /// 教学闸预检前置（防落位后才被拒）。</summary>
        private void BeginSpellCastAutoPlace(BattleCardView view, Card card, int modeIndex)
        {
            var board = _ctrl?.Board;
            if (board == null) return;

            // 教学闸预检（同 SpellDeclarationFlow 口径——落位前先验）
            if (RuleHooks.TutorialPlayGate?.Invoke(P1, card) is string gateReject)
            {
                _toast.Show($"教学引导：{gateReject}");
                return;
            }

            (int x, int z) picked = (-1, -1);
            foreach (var cell in BoardLayout.UnitCells(0))
                if (board.CardAt(cell.x, cell.z) == null) { picked = (cell.x, cell.z); break; }
            if (picked.x < 0) { _toast.Show("单位区已满——无法发动（法术须先到发动区占位）"); return; }

            _choreo?.CancelTargeting();
            _pendingPins[card.RuntimeId] = picked;
            Debug.Log($"[HandPlay] 法术自动落位：{view.Name} → ({picked.x},{picked.z})（B4 免手选发动区）");
            _toast.Show("已自动落位发动区");
            SpellDeclarationFlow(view, modeIndex, pickedX: picked.x, pickedZ: picked.z).Forget();
        }

        /// <summary>法术发动声明流：发动位/模式/费用已过 → 预览态（贴片预览——手牌路径=玩家所选格，
        /// 墓地路径自动 first-free + 手牌扇藏卡=「卡已放发动区」观感）→ 声明期目标收集（逐原子问选，
        /// 可取消）→ 取消=撤销发动（PlayCard 未调：使用宣言触发等声明副作用零，选格单一并
        /// 作废，回退绝对干净）→ 确认=预览贴片无缝转正 + PlayCardFinal（真声明：进发动区+上栈+PinCard
        /// 钉所选格）→ SettleResponseWindow（连锁检测=引擎现状；无连锁结算期扣费+粒子+效果表现）。
        /// 无交互原子（全取/随机/全 defer）：收集器同步完成空表——预览同帧转正零可见闪烁
        ///（UniTask 同步前缀保证）。</summary>
        private async UniTaskVoid SpellDeclarationFlow(BattleCardView view, int modeIndex,
            int pickedX = -1, int pickedZ = -1)
        {
            var core = Core;
            var card = view?.CoreCard;
            if (core == null || P1 == null || card == null) return;

            // 教学闸预检（声明期先验——防选完目标才被引擎拒；PlayCard 内终审保留同口径）
            if (RuleHooks.TutorialPlayGate?.Invoke(P1, card) is string gateReject)
            {
                _pendingPins.Remove(card.RuntimeId); // 选位单作废（手选发动位路径）
                _toast.Show($"教学引导：{gateReject}");
                return;
            }

            // 落格：手牌路径=玩家所选格（选格单已登记）；无选格单自动 first-free 落位
            var (cellX, cellZ) = pickedX >= 0 ? (pickedX, pickedZ) : PreviewCastingCell();
            _field?.ShowCastingPreview(card, cellX, cellZ);
            _selfFan?.SetCardActive(card.RuntimeId, false);

            try
            {
                var targets = await GameActions.CollectSpellDeclarationTargetsAsync(card, P1, modeIndex);

                if (targets == null)
                {
                    // 玩家取消——撤销发动：拆预览+还原手牌扇+选格单作废（引擎零状态）
                    _field?.HideCastingPreview();
                    _selfFan?.SetCardActive(card.RuntimeId, true);
                    _pendingPins.Remove(card.RuntimeId);
                    RefreshLocal();
                    return;
                }

                // 确认：预览贴片转正（选格单经 PlayCardFinal 声明成功 PinCard——PlaceCasting 钉优先落同格）
                _field?.PromoteCastingPreview(card, cellX, cellZ);
                if (!PlayCardFinal(view, targets, modeIndex))
                {
                    // 声明被引擎终审拒（如弃牌代价·手牌无目标）：镜像取消路径清理。转正件已进
                    // _castings 正片表，HideCastingPreview 够不着——走 InterruptCasting 即清口
                    // （CardActivationFailedEvent 同款）；拒绝零引擎事件，贴片/藏卡不会随后续
                    // 对账自愈（2026-10-26 bug：失败后手牌空+发动标识滞留）。
                    _field?.InterruptCasting(card.RuntimeId);
                    _selfFan?.SetCardActive(card.RuntimeId, true);
                    RefreshLocal();
                }
            }
            catch (Exception ex)
            {
                // 异常兜底=按取消清理（2026-10-20 实测教训：续体异常打断声明流会滞留预览/藏卡不还=UI 卡死）
                Debug.LogException(ex);
                _field?.HideCastingPreview();
                _selfFan?.SetCardActive(card.RuntimeId, true);
                _pendingPins.Remove(card.RuntimeId);
                RefreshLocal();
            }
        }

        /// <summary>声明期贴片预览落格（墓地路径回落用——手牌路径已改玩家手选发动位 2026-10-20）：
        /// 镜像 BoardState.PlaceCasting 法术档（first-free——UnitCells(0) 首个无占用格；
        /// 预览期无状态变更，引擎届时落同格）。满格 (-1,-1)=无贴片预览。</summary>
        private (int x, int z) PreviewCastingCell()
        {
            var board = _ctrl?.Board;
            if (board == null) return (-1, -1);
            foreach (var c in BoardLayout.UnitCells(0))
                if (board.CardAt(c.x, c.z) == null)
                    return (c.x, c.z);
            return (-1, -1);
        }

        /// <summary>放地锁产色（2026-10-16 弹窗退役：旧选色弹窗→Region 卡牌列表，min=1 强制选；
        /// 单色直落）。null=缺 Region 静默取消（本次放置作废）。</summary>
        private async UniTaskVoid PickLandColorAsync(BattleCardView view, List<ManaType> colors)
        {
            if (colors == null || colors.Count == 0) return;
            if (colors.Count == 1)
            {
                LandFinal(view, colors[0]);
                return;
            }
            var options = new List<string>(colors.Count);
            foreach (var c in colors) options.Add($"锁定 {BattleView.ManaZh(c)} 产色");
            int? picked = await PickRegionOptionAsync("选择地牌产色", options);
            if (picked == null) return;
            LandFinal(view, colors[picked.Value]);
        }

        /// <summary>放地终态：AddToElementPool(锁色)。</summary>
        private void LandFinal(BattleCardView card, ManaType color)
        {
            bool ok = Core != null && GameActions.AddToElementPool(Core, P1, card.CoreCard, color);
            if (!ok) _toast.Show("无法放入元素池（上限/资格）");
            RestoreFan(); // 放地即时对账（2026-10-14）：手牌扇收卡+地格「可用/上限」上盘——
                          // RefreshLocal 只刷 2D 信息栏，扇区/3D 摆位原要等下一事件才补（入池只发 ElementPoolAddEvent）
        }

        // ======================================== 手牌扇出牌接线（2026-10-14 放置/发动按钮 + 箭头选格） ========================================

        /// <summary>扇区卡 → 引擎手牌卡（RuntimeId 对账；不在手=已过态返回 null）。
        /// 牌库顶虚拟手牌（2026-10-29 阶段3b）：手牌对账落空再查牌库顶（RuntimeId 同键——
        /// 「只可打出」的出牌链由此找到卡实例）。</summary>
        private Card FanCardOf(HandFanCard fan)
        {
            if (fan == null || Core == null || P1 == null) return null;
            foreach (var c in Core.ZoneManager.GetCards(P1, Zone.Hand))
                if (c != null && c.RuntimeId == fan.RuntimeId) return c;
            var top = TopDeckVirtualCard();
            return top != null && top.RuntimeId == fan.RuntimeId ? top : null;
        }

        /// <summary>GateFanMyMain 静默版（按钮/流光逐帧评估用——不弹 toast）。</summary>
        private bool FanGateQuiet()
        {
            if (_gameEnded || _ctrl == null || Core == null || P1 == null) return false;
            var phase = Core.TurnEngine?.CurrentPhase;
            return _ctrl.TurnPlayer == P1 && phase != null && phase.Phase == PhaseType.Main;
        }

        /// <summary>「放置」按钮条件：可作地牌（正式三型/非衍生物/非临时/非锁定）+ 有可锁产色
        /// + 地牌池有余位（各自上限口径：PooledCards.Count &lt; GetLandCap）。
        /// 牌库顶虚拟手牌（2026-10-29 阶段3b）「只可打出」——限手牌区，不放地。</summary>
        private bool FanCanPlaceLand(HandFanCard fan)
        {
            if (!FanGateQuiet()) return false;
            var card = FanCardOf(fan);
            if (card == null || card.GetZone() != Zone.Hand || Core?.ElementPool == null) return false;
            return ElementPoolSystem.CanServeAsLand(card)
                && ElementPoolSystem.GetLandColorChoices(card).Count > 0
                && Core.ElementPool.CanAddLand(P1);
        }

        /// <summary>「使用」按钮条件：任一模式 CanAffordCast（含声明承诺 pending——代价补偿
        /// 黑/白不预计算垫本卡，2026-10-16 恢复旧序）；永久物另需单位区空格（满场不可出）。
        /// 法术多主动（2026-10-31 P12 抉择默认化）：「使用」承载模式窗——多主动法术也走使用钮
        ///（GetModeCount&gt;1 时 OnFanUse 弹「选择要发动的效果」；单主动直放）。</summary>
        private bool FanCanUse(HandFanCard fan)
        {
            if (!FanGateQuiet()) return false;
            var card = FanCardOf(fan);
            if (card == null) return false;
            return CardCastableNow(card);
        }

        /// <summary>「特殊使用」按钮条件（2026-10-31 P12：充能+苏醒两入口）：
        /// 充能=任意卡型、卡上有充能效果且在手牌（GetHandCardChargeInfo.PayableNow 任一可付色——
        /// 点击时 CanCharge 终审）；
        /// 生物苏醒=仅卡面带 Awaken 关键词（P12 关键词化；双钮持续显示，点击时 CanAffordCast 验费，不预闸）。
        /// 法术不再有「特殊使用=抉择」——多主动法术由「使用」钮进入模式窗（C 段）。</summary>
        private bool FanCanSpUse(HandFanCard fan)
        {
            if (!FanGateQuiet()) return false;
            var card = FanCardOf(fan);
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return false;
            // 充能（P12）：任意 supertype——有充能效果且当前有可付颜色即亮
            if (card.GetZone() == Zone.Hand && ComposerCatalog.HasChargeEffect(data))
            {
                var info = GameActions.GetHandCardChargeInfo(Core, P1, card);
                foreach (var payable in info.PayableNow.Values)
                    if (payable) return true;
            }
            if (data.Supertype == Cardtype.Creature) return GameActions.HasAwakenKeyword(card);
            return false;
        }

        /// <summary>卡当前可发动（无主阶段闸——闸由调用方外置）；法术=可付即可，永久物另需空格。
        /// 场地卡（2026-10-29 P8）不占单位容量：落己方场地格恒可入（旧场地在位=顶替），免空格判定。</summary>
        private bool CardCastableNow(Card card)
        {
            if (card == null || Core == null || P1 == null) return false;
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return false;
            int modes = Math.Max(1, CostDerivationService.GetModeCount(data));
            bool ok = false;
            for (int i = 0; i < modes && !ok; i++)
                ok = GameActions.CanAffordCast(Core, P1, card, i);
            if (!ok) return false;
            if (data.Supertype != Cardtype.Spell)
            {
                if (data.IsField) return true; // 场地卡：场地格每方一张恒可落
                int n = 0;
                foreach (var c in Core.ZoneManager.GetCards(P1, Zone.Battlefield))
                    if (c == null || !c.IsFieldCard()) n++;
                return n < BoardLayout.UnitCells(0).Count;
            }
            return true;
        }

        /// <summary>己方手牌可发动流光评估（随 RefreshFans 对账刷新；非主阶段全熄）。
        /// 2026-10-19 分支金圈：可付且卡上有限分支（Gate）条件已达成=金色光圈，
        /// 区别于常规「费用可用」绿（教学二·命运一抽定案）。</summary>
        private void EvaluateFanPlayability()
        {
            if (_selfFan == null || Core == null || P1 == null) return;
            bool gate = FanGateQuiet();
            foreach (var card in Core.ZoneManager.GetCards(P1, Zone.Hand))
            {
                bool castable = gate && CardCastableNow(card);
                _selfFan.SetPlayable(card.RuntimeId, castable, castable && GateArmedNow(card));
            }

            // 牌库顶虚拟手牌（2026-10-29 阶段3b）：发动流光随对账刷新（打出后顶牌换新即重评）
            var deckTop = TopDeckVirtualCard();
            if (deckTop != null)
            {
                bool castable = gate && CardCastableNow(deckTop);
                _selfFan.SetPlayable(deckTop.RuntimeId, castable, castable && GateArmedNow(deckTop));
            }
        }

        /// <summary>卡上有限分支（settle=1 局面门）当前是否达成（金圈判据，2026-10-19 教学二）：
        /// 扫主动效果原子的槽级 Gate 载荷，用局面评估器预评（CastCard=该卡，与结算期
        /// ApplySituationGateAsync 的 HostCard 同口径）；无门/未知条件恒 false。局面门族
        /// 条件只读 context 局面（outcome 不参与），传全新 EffectOutcome 即可。</summary>
        private bool GateArmedNow(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null) return false;
            foreach (var fx in data.Effects)
            {
                if (fx?.Steps == null) continue;
                foreach (var step in fx.Steps)
                {
                    var b = step?.atomic?.branch;
                    if (b == null || b.settle != 1 || string.IsNullOrEmpty(b.gateId)) continue;
                    var ctx = new EffectExecutionContext
                    {
                        Controller = P1,
                        Source = card,
                        CastCard = card,
                        ZoneManager = Core.ZoneManager,
                        ElementPool = Core.ElementPool,
                        SimulatedPreview = true, // 预评口径：按「这张打出后」评估（首卡计 0、手牌剩 1 等）
                    };
                    if (BranchConditionEvaluator.Evaluate(b.gateId, new EffectOutcome(),
                            b.condParam, b.condStr, ctx))
                        return true;
                }
            }
            return false;
        }

        /// <summary>常驻考验重置按钮逐帧门控（2026-10-19）：教学局非 Boss 课常显（终局隐藏）；
        /// 可点=自由操作期（我方回合主阶段+无选择在身+无介绍卡在屏）且表现播毕（VisualsBusy）。</summary>
        private void TickTutorialResetButton()
        {
            if (_tutorialReset == null) return;
            bool visible = BattleEntry.Mode == BattleMode.Tutorial
                && BattleEntry.TutorialId != TutorialProgressManager.LunzhuanId
                && !_gameEnded;
            if (_tutorialReset.gameObject.activeSelf != visible)
                _tutorialReset.gameObject.SetActive(visible);
            if (!visible) return;
            bool free = FanGateQuiet() && !IsTargetSelecting()
                && _tutorial != null && !_tutorial.IntroPanelOpen
                && (_field == null || !_field.VisualsBusy);
            if (_tutorialReset.interactable != free)
                _tutorialReset.interactable = free;
        }

        private void OnTutorialResetClicked()
        {
            _tutorial?.TryResetTrial();
        }

        /// <summary>T 键测试跳段（2026-10-21 用户要求·临时调试口）：教学局按 T=直接跳到最终
        /// 考验段（教学二=段 8 命运一抽考验）。门控=重置按钮口径（玩家主阶段+无选择在身+无介绍
        /// 卡在屏）另加栈收口+回放导演闲——防结算/回放中途强跳撕裂换场重铺；非教学局零开销直返。</summary>
        private void PollTutorialJumpKey()
        {
            if (BattleEntry.Mode != BattleMode.Tutorial || _gameEnded) return;
            if (!Input.GetKeyDown(KeyCode.T)) return;
            if (!FanGateQuiet() || IsTargetSelecting()) return;
            if (_tutorial == null || _tutorial.IntroPanelOpen) return;
            if (Core != null && Core.StackEngine != null
                && (!Core.StackEngine.IsEmpty || Core.StackEngine.IsResolving)) return;
            if (_field != null && _field.VisualsBusy) return;
            if (_director != null && _director.Busy) return;
            _tutorial.JumpToFinalTrial();
        }

        /// <summary>教学自由步轮询（2026-10-19 教学二段一改版）：fieldclear 步在位时逐帧查
        /// 对手场上生物——清零即完成推进（下一 intro 解说卡弹出）。luckuse/deathtoll 自由步同口
        /// 接入（2026-10-20）。栈收口（防结算中途瞬态空场）且表现播毕（死亡退场演完再弹卡）
        /// 才查；非自由步在位零开销直返。
        /// 终局放行（2026-10-20 段六元素充盈）：等待步的完成击杀恰是预设剧本（充盈兽 3 伤=AI 3 血
        /// 一发终结）——IsScriptedContinuation 在位时终局弹窗已跳过、全靠本轮询推进到换场步续接，
        /// 此时不可因 _gameEnded 冻结（否则死锁在终局画面）；非续接终局（真打完）照旧冻结。</summary>
        private void PollFieldClearStep()
        {
            if (BattleEntry.Mode != BattleMode.Tutorial || !TutorialGuide.Active) return;
            var stepType = TutorialGuide.Current?.type;
            if (stepType != "fieldclear" && stepType != "luckuse" && stepType != "deathtoll") return;
            if (_gameEnded && (_tutorial == null || !_tutorial.IsScriptedContinuation)) return;
            if (Core == null || Core.StackEngine == null
                || !Core.StackEngine.IsEmpty || Core.StackEngine.IsResolving) return;
            if (_field != null && _field.VisualsBusy) return;
            // 回放导演在途（2026-10-20 运势段被截修复）：步间停顿/末步等待期视觉门瞬空——
            // 演出载荷还在导演手里没放出，栈空+门空双双穿透，步进早于演出收口
            if (_director != null && _director.Busy) return;
            if (stepType == "fieldclear") TutorialGuide.PollFieldClear();
            else if (stepType == "deathtoll") TutorialGuide.PollDeathToll(); // 死亡计数段自由步（2026-10-20）：计数满 6 触发奖励即推进
            else TutorialGuide.PollLuckUse(); // 运势段用毕步（2026-10-20）：全横置+无待发奖励才推进
        }

        /// <summary>选格/出牌流程收尾：恢复手牌扇+全量对账（按钮条件/流光随刷新重评估）。</summary>
        private void RestoreFan()
        {
            _selfFan?.SetVisible(true);
            RefreshFans();
            RefreshField();
        }

        /// <summary>「放置」点击（2026-10-14 定案）：藏手牌→放地箭头（候选=卡可锁色的落格：
        /// 已有色=色组现格、新色=BoardState 落格预览；高亮按元素色）→确认入池（LandFinal 同源）；
        /// 同格多候选新色=弹现选色窗消歧；取消（右键/ESC/无效格）恢复手牌。</summary>
        private void OnFanPlaceLand(HandFanCard fan)
        {
            if (NetActive) { NetOnFanPlaceLand(fan); return; } // 网络局：锁色选择+intent 上行
            if (!GateFanMyMain()) return;
            var card = FanCardOf(fan);
            if (card == null) return;
            if (Core?.ElementPool == null || !ElementPoolSystem.CanServeAsLand(card) || !Core.ElementPool.CanAddLand(P1))
                return;

            var choices = ElementPoolSystem.GetLandColorChoices(card);
            if (choices.Count == 0) { _toast.Show("该卡无可锁定产色（无费用元素/纯黑白不可作地牌）"); return; }

            var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false);
            var board = _ctrl?.Board;
            if (board == null) return;

            // 候选格（cell → 候选色集）：确定性预览——与入池后 AssignLandCells 落格一致
            var cellColors = new Dictionary<(int x, int z), List<ManaType>>();
            var options = new List<BattleCellPicker.CellOption>();
            foreach (var color in choices)
            {
                var cell = ExistingLandCellOf(color) ?? board.PreviewLandCell(P1, color);
                if (cell == null) continue;
                if (!cellColors.TryGetValue(cell.Value, out var list))
                {
                    list = new List<ManaType>();
                    cellColors[cell.Value] = list;
                    options.Add(new BattleCellPicker.CellOption { X = cell.Value.x, Z = cell.Value.z, ColorIndex = (int)color });
                }
                list.Add(color);
            }
            if (options.Count == 0) { _toast.Show("地牌行无空位（颜色数超格）"); return; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var hero = BoardLayout.CharacterCell(0);
            _cellPicker.Begin(BattleBoardGrid.WorldPositionOf(hero.x, hero.z), options, opt =>
            {
                RestoreFan();
                var colors = cellColors[(opt.X, opt.Z)];
                if (colors.Count == 1)
                {
                    LandFinal(view, colors[0]);
                    return;
                }
                PickLandColorAsync(view, colors).Forget(); // 同格多候选新色：Region 卡牌列表消歧（2026-10-16 弹窗退役）
            }, RestoreFan);
        }

        /// <summary>色 → 现有地牌色组落格（BoardState 反查；无该色组=null）。</summary>
        private (int x, int z)? ExistingLandCellOf(ManaType color)
        {
            var board = _ctrl?.Board;
            if (board == null || Core?.ElementPool == null) return null;
            foreach (var pc in Core.ElementPool.GetPooledCards(P1))
                if (pc?.SourceCard != null && pc.LockedColor == color && board.TryGetCell(pc.SourceCard, out int x, out int z))
                    return (x, z);
            return null;
        }

        /// <summary>「使用」点击（2026-10-14 定案；2026-10-30 SpUse 改版——召唤方式二选弹窗退役，
        /// 生物苏醒走 SpUse 按钮）：法术（单主动）=费用预检后直入声明流；多主动法术=「抉择」
        /// 钮承载（本口不可达，防御保留）；永久物=模式窗（多模式）→选格→确认出牌（恒普通召唤）。</summary>
        private void OnFanUse(HandFanCard fan)
        {
            if (NetActive) { NetOnFanUse(fan); return; } // 网络局：模式抉择+intent 上行（目标结算期反问）
            if (!GateFanMyMain()) return;
            var card = FanCardOf(fan);
            if (card == null) return;
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return;
            var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false);

            if (data.Supertype == Cardtype.Spell)
            {
                // 2026-10-15 改版：>1 个作用弹抉择窗（选择要发动的效果）；单作用费用预检后直入选位。
                // 2026-10-20 改版：手选发动位→声明流（预选+贴片预览+可取消撤销）
                // 2026-10-30 SpUse 改版：>1 主动=「抉择」钮承载，本口只达单主动（防御保留多主动分支）
                if (GetModeCount(view) >= 2) PromptModeThenPlay(view).Forget();
                else
                {
                    // 费用预检（与模式窗同口径——防选完位置目标才被引擎拒）
                    if (Core == null || !Core.ElementPool.CanPayCostWithAutoTap(
                            GameActions.GetCardCost(card, 0), P1))
                    {
                        _toast.Show("费用不足——该作用不可发动");
                        return;
                    }
                    BeginSpellCastAutoPlace(view, card, 0);
                }
                return;
            }
            if (CostDerivationService.GetModeCount(data) >= 2)
            {
                PromptModeThenPickCell(view, card).Forget();
                return;
            }
            SummonFlow(view, card, 0, awakenCast: false);
        }

        /// <summary>「特殊使用」点击（2026-10-31 P12：充能+苏醒两入口分流）：
        /// 充能=任意卡型带充能效果（ChargeCardAsync——选色→ActivateCharge 速度 0 主动效果入栈）；
        /// 生物苏醒=仅 Awaken 关键词（免灰转入场沉睡层数·横置登场，P6c——按钮即抉择，弹窗退役）；
        /// 两入口并存（带充能+带苏醒的生物）=Region 二选一。法术无特殊使用（多主动走「使用」模式窗）。</summary>
        private void OnFanSpUse(HandFanCard fan)
        {
            if (NetActive) { NetOnFanSpUse(fan); return; } // 网络局：充能/苏醒 intent 上行
            if (!GateFanMyMain()) return;
            var card = FanCardOf(fan);
            if (card == null) return;
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return;
            var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(card), card, false);

            bool canCharge = card.GetZone() == Zone.Hand && ComposerCatalog.HasChargeEffect(data);
            bool canAwaken = data.Supertype == Cardtype.Creature && GameActions.HasAwakenKeyword(card);
            if (canCharge && canAwaken)
            {
                PickSpUseModeAsync(view, card).Forget(); // 双入口并存：Region 二选一
                return;
            }
            if (canCharge)
            {
                ChargeCardAsync(card).Forget();
                return;
            }
            if (!canAwaken) return; // P12：无充能/无苏醒关键词的卡不响应（防御——按钮已闸）
            if (CostDerivationService.GetModeCount(data) >= 2)
            {
                PromptModeThenPickCell(view, card, awakenCast: true).Forget();
                return;
            }
            SummonFlow(view, card, 0, awakenCast: true);
        }

        /// <summary>特殊使用方式二选一（带充能+带苏醒的生物）：充能 / 苏醒召唤。</summary>
        private async UniTaskVoid PickSpUseModeAsync(BattleCardView view, Card card)
        {
            int? picked = await PickRegionOptionAsync("选择特殊使用方式",
                new List<string> { "充能（付 1 点元素·减同色费用）", "苏醒召唤（免灰转沉睡·横置登场）" });
            if (picked == null) return;
            if (picked == 0)
            {
                ChargeCardAsync(card).Forget();
                return;
            }
            var data = (card as CardWrapper)?.GetData();
            if (CostDerivationService.GetModeCount(data) >= 2)
                PromptModeThenPickCell(view, card, awakenCast: true).Forget();
            else
                SummonFlow(view, card, 0, awakenCast: true);
        }

        /// <summary>召唤出牌（验费→选格→PlayCardFinal；2026-10-30 SpUse 改版：awakenCast 由按钮
        /// 直定——使用=普通召唤 false / 特殊使用=苏醒 true，召唤方式弹选退役）。
        /// 按 CanAffordCast 全口径验费（含声明承诺；苏醒按免灰账单）。</summary>
        private void SummonFlow(BattleCardView view, Card card, int modeIndex, bool awakenCast)
        {
            if (Core == null || !GameActions.CanAffordCast(Core, P1, card, modeIndex, awakenCast))
            {
                _toast.Show(awakenCast ? "费用不足——苏醒召唤不可发动" : "费用不足——该分支不可发动");
                return;
            }
            BeginUseCellPicking(view, card, modeIndex, awakenCast);
        }

        /// <summary>充能（2026-10-31 P12：建造→充能）：可充色=卡费有该色且余量&gt;0 且现可付 1 点
        ///（GetHandCardChargeInfo 数据口）；多色=Region 择一（附余量/充后净费预览）；单色直发。
        /// ActivateCharge 把充能效果作为速度 0 主动效果入栈（响应窗口后结算：现付 1 点该色元素、
        /// 放 1 层同色费用降低指示物并展示此卡），卡留手牌——净费随 RefreshLocal 回写。</summary>
        private async UniTaskVoid ChargeCardAsync(Card card)
        {
            var info = GameActions.GetHandCardChargeInfo(Core, P1, card);
            var colors = new List<ManaType>();
            foreach (var kv in info.PayableNow)
                if (kv.Value && info.RemainingByColor.TryGetValue(kv.Key, out var remain) && remain > 0)
                    colors.Add(kv.Key);
            if (colors.Count == 0) { _toast.Show("无对应元素可充（每点充能=现付 1 点该色元素）"); return; }

            ManaType color;
            if (colors.Count == 1) color = colors[0];
            else
            {
                var options = new List<string>();
                foreach (var c in colors)
                {
                    int remain = info.RemainingByColor.TryGetValue(c, out var r) ? r : 0;
                    string after = info.NetCostAfterCharge.TryGetValue(c, out var nc) ? CostText(nc) : "—";
                    options.Add($"充能 {ManaTypeNames.ZhNameOf(c)}（余{remain}点 · 充后净费 {after}）");
                }
                int? picked = await PickRegionOptionAsync("选择充能颜色", options);
                if (picked == null) return; // 取消/缺 Region：本次不充
                color = colors[picked.Value];
            }

            if (GameActions.ActivateCharge(Core, P1, card, color, out var reason))
                _toast.Show($"充能 +1{ManaTypeNames.ZhNameOf(color)} 已发动（响应窗口后结算）");
            else
                _toast.Show($"充能失败：{reason}");
            RefreshLocal();
        }

        /// <summary>模式窗变体（永久物出牌）：选完模式进入召唤出牌（生物 2026-10-30 SpUse 改版：
        /// awakenCast 由入口按钮直定——使用=false 普通召唤 / 特殊使用=true 苏醒，召唤方式弹选退役）
        /// →选格——文案口径同 PromptModeThenPlay，验费用 CanAffordCast 全口径（含声明承诺；
        /// 代价补偿不预计算——2026-10-16 恢复旧序；苏醒按所选账单）。
        /// 2026-10-16 弹窗退役：选项=Region 面板卡牌列表。</summary>
        private async UniTaskVoid PromptModeThenPickCell(BattleCardView view, Card card, bool awakenCast = false)
        {
            var data = DataOf(view);
            var labels = FindChoiceLabels(data);
            int modeCount = Math.Max(2, CostDerivationService.GetModeCount(data));
            var options = new List<string>();
            for (int i = 0; i < modeCount; i++)
            {
                string name = i < labels.Count && !string.IsNullOrEmpty(labels[i]) ? labels[i] : $"模式 {i + 1}";
                options.Add($"{name}（费用 {CostText(LocalCostOf(view, i))}）");
            }
            int? picked = await PickRegionOptionAsync("选择模式", options);
            if (picked == null) return;
            int idx = picked.Value;
            SummonFlow(view, card, idx, awakenCast);
        }

        /// <summary>永久物出牌选格：候选=己方单位区空格（占用=BoardState 现值，含施放占格卡）；
        /// 箭头自角色格拉出（原发动格锚随发动格拆除退役，2026-10-15）。
        /// awakenCast（2026-10-29 P6；P6c 只免灰）：苏醒施放——净费免灰转入场沉睡层数、横置登场。
        /// 确认→登记选格单（PlayCardFinal 声明成功即转 BoardState 钉——施放类型图标与入场转正同钉单源）
        /// →PlayCardFinal（需目标的卡在链内弹目标窗）。取消恢复手牌。</summary>
        private void BeginUseCellPicking(BattleCardView view, Card card, int modeIndex, bool awakenCast = false)
        {
            var board = _ctrl?.Board;
            if (board == null) return;

            // 场地卡（2026-10-29 P8）：目标格恒为己方场地格 (0,2)——不弹选格；格上有旧场地=顶替入墓
            var playData = (card as CardWrapper)?.GetData();
            if (playData != null && playData.IsField)
            {
                var fc = BoardLayout.FieldCell(0);
                _choreo?.CancelTargeting();
                _pendingPins[card.RuntimeId] = fc;
                bool replacing = board.FieldOccupied(P1);
                Debug.Log($"[HandPlay] 场地卡直接落位：{view.Name} → ({fc.x},{fc.z}){(replacing ? "（顶替旧场地入墓）" : "")}");
                _toast.Show(replacing ? "场地就位——将顶替己方现有场地（旧场地入墓）" : "场地就位（己方场地格）");
                PlayCardFinal(view, null, modeIndex, awakenCast: awakenCast);
                return;
            }

            var options = new List<BattleCellPicker.CellOption>();
            foreach (var cell in BoardLayout.UnitCells(0))
                if (board.CardAt(cell.x, cell.z) == null)
                    options.Add(new BattleCellPicker.CellOption { X = cell.x, Z = cell.z, ColorIndex = -1 });
            if (options.Count == 0) { _toast.Show("单位区已满——无法打出"); return; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            _cellPicker.Begin(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options, opt =>
            {
                RestoreFan();
                _pendingPins[card.RuntimeId] = (opt.X, opt.Z);
                Debug.Log($"[HandPlay] 永久物出牌选格：{view.Name} → ({opt.X},{opt.Z})（声明成功即钉{(awakenCast ? "·苏醒施放" : "")}）");
                PlayCardFinal(view, null, modeIndex, awakenCast: awakenCast);
            }, RestoreFan);
        }

        /// <summary>移动原子选格（2026-10-31 场面轴3c MoveUnit）：CellSelectionService 反问桥实现——
        /// 与永久物出牌选格同交互（BattleCellPicker 箭头+候选空格），箭头自待移动单位当前格拉出
        ///（未落格回落角色格）。返回候选表索引（-1=取消）；候选/合法性由 MoveUnitRules 单源筛得。</summary>
        private async UniTask<int> PickMoveDestinationAsync(Card unit, IReadOnlyList<(int x, int z)> cells, string title)
        {
            var options = new List<BattleCellPicker.CellOption>();
            foreach (var c in cells)
                options.Add(new BattleCellPicker.CellOption { X = c.x, Z = c.z, ColorIndex = -1 });

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            if (unit != null && _ctrl?.Board != null && _ctrl.Board.TryGetCell(unit, out int ux, out int uz))
                origin = (ux, uz);
            if (!string.IsNullOrEmpty(title)) _toast.Show(title);

            var done = new UniTaskCompletionSource<int>();
            _cellPicker.Begin(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options, opt =>
            {
                RestoreFan();
                int idx = -1;
                for (int i = 0; i < cells.Count; i++)
                    if (cells[i].x == opt.X && cells[i].z == opt.Z) { idx = i; break; }
                done.TrySetResult(idx);
            }, () => { RestoreFan(); done.TrySetResult(-1); });
            return await done.Task;
        }

        /// <summary>点我方地牌（2026-10-11 现取现用 → 2026-10-14 按色叠格）：全自动结算口径不变——
        /// 付费时系统按账单自动横置（强制系统动作，无速度交互）。2026-10-16 lbl-toast 定案：
        /// 地牌格介绍属场面信息=hover-info 单源（LandCellInfo 悬停即有，含逐色可用/上限与地牌上限行），
        /// 点击不再写 toast（旧汇总提示会覆盖选择/响应提示位——牛头不对马嘴），留日志取证。</summary>
        private void OnClickMyLand(BattleCardView card)
        {
            if (card == null) return;
            var toks = new List<string>();
            var pv = _view?.Self;
            foreach (var g in (_view?.SelfLands ?? new List<BattleCardView>())
                     .Where(c => c.X == card.X && c.Z == card.Z && c.Runtime != null)
                     .GroupBy(c => c.Runtime.LockedColor))
            {
                int untapped = g.Count(c => !c.IsTapped);
                int wallet = pv != null && g.Key >= 0 && g.Key < pv.WalletByColor.Length ? pv.WalletByColor[g.Key] : 0;
                string color = g.Key >= 0 ? BattleView.ManaZh((ManaType)g.Key) : "?";
                toks.Add($"{color}{untapped + wallet}/{g.Count()}");
            }
            Debug.Log($"[地牌格] {card.Name} 点击：{(toks.Count > 0 ? string.Join("  ", toks) : "暂无地牌")}"
                      + "（逐色可用/上限详情=悬停地牌格 hover-info）");
        }

        /// <summary>点我方单位：3D 寻路箭头选攻击目标（2026-10-13 攻击宣言改版）→ 攻击宣言。
        /// 悬停对方单位/角色格实时画寻路路径（BFS 最短路），点目标=路径固定并回传目标；
        /// 确认后下游宣言链路不变（本地 DeclareAttack+结算 / 网络 intent）。
        /// 取代旧 ShowPickOverlay 列表弹窗（overlay 节点缺失时空转的依赖一并解除）。</summary>
        private void OnClickMyUnit(BattleCardView card)
        {
            if (NetActive) { NetUnitClick(card); return; } // 网络局：守卫/攻击 intent（列表选目标）
            if (!GateMyMain()) return;
            if (card.IsTapped) { _toast.Show("该单位已横置"); return; }
            if (card.Runtime != null && card.Runtime.Power <= 0) { _toast.Show("该单位攻击力为 0"); return; }
            if (card.X < 0 || card.Z < 0 || _choreo == null)
            {
                _toast.Show("单位位置未知，无法攻击");
                return;
            }

            _choreo.BeginTargeting(card, target =>
            {
                Entity captured = target != null && target.CoreCard != null ? (Entity)target.CoreCard : P2;
                if (captured == null || !_ctrl.DeclareAttack(P1, card.CoreCard, captured))
                {
                    _toast.Show(AttackFailReason(card.CoreCard, captured)); // 真实原因（2026-10-17 error-tip 定案）
                    RefreshLocal();
                    return;
                }
                _ctrl.SettleResponseWindow().Forget();
                RefreshLocal();
            });
        }

        /// <summary>攻击宣言失败原因（error-tip 文案，2026-10-17）：教学闸读同一 RuleHooks 钩子
        ///（拒绝文案直接可读）；潜行/隐密与引擎 CanAttackTarget 同口径复算；其余回落旧泛称。</summary>
        private string AttackFailReason(Entity attacker, Entity target)
        {
            var guide = RuleHooks.TutorialAttackGate?.Invoke(P1, attacker, target);
            if (guide != null) return guide;
            if (target is Card tc && (tc.GetEffectiveCounterCount(CardCore.Attribute.CounterRules.StealthCounter) > 0
                                      || tc.HasKeyword(KeywordRules.Concealed)))
                return "目标具有潜行/隐密——不可被指定为攻击目标";
            return "无法对该目标攻击";
        }

        // ======================================== 守卫窗口（2026-10-14：对手攻击时点守卫生物出箭头拦截） ========================================



        /// <summary>点【攻击】键/角色攻击（2026-10-16 与生物同款箭头选目标）：箭头自角色格拉出，
        /// 悬停对方单位/角色格（目标=角色回传 null→P2），点击确认→角色攻击宣言——引擎侧 Player
        /// 攻击者已全面支持（CanDeclareAttack 弹药同门、结算后烧光弹药）。
        /// 伪视图钉角色格、RuntimeId 保持 0（=编舞器 IdOf 的角色哨兵，与 AI 角色攻击宣言同口径）。</summary>
        private void OnClickSelfHero()
        {
            if (IsTargetSelecting()) return; // 目标选择期不开新选择（两语义裁定·见 OnClickHeroZone）
            if (!GateFanMyMain()) return;
            var core = Core;
            if (core == null || P1 == null) return;
            int ammo = P1.GetPower(); // Player 分支=HeroAttackCounter 弹药层数钳非负
            if (ammo <= 0) { _toast.Show("角色无弹药（攻击力 0）"); return; }
            if (_choreo == null) { _toast.Show("战场编舞未就绪，无法发起角色攻击"); return; }

            var cell = BoardLayout.CharacterCell(0);
            var heroView = new BattleCardView { Name = "角色", X = cell.x, Z = cell.z };
            _choreo.BeginTargeting(heroView, target =>
            {
                Entity captured = target != null && target.CoreCard != null ? (Entity)target.CoreCard : P2;
                if (captured == null || !_ctrl.DeclareAttack(P1, P1, captured))
                {
                    _toast.Show("无法对该目标攻击");
                    RefreshField();
                    return;
                }
                _ctrl.SettleResponseWindow().Forget();
                RefreshField();
            });
        }

        // ======================================== 3D 棋盘指针（2026-10-13 悬停/点击统一入口） ========================================



        /// <summary>3D 棋盘指针轮询（UiKit.Updater.Tick）：每帧一次共享射线——
        /// 悬停路由（单位格=四段卡信息/特殊格与功能地块=hover-info 文本），左键点击路由
        /// （功能地块/特殊格 Region 面板/角色格角色与技能详情/地牌信息）。
        /// UI 命中让路且不收钮（销毁按钮会吞掉 onClick，原口径）；单位模型 collider 已拆，
        /// 打格反查是既定通路。2026-10-26 头顶 3D 钮定案：点有可行动作的己方单位/
        /// 角色格在头顶弹 BattleUnitActionButtons（位图=ReconcileTileButtons；钮名 unit-act-* 前置路由）。
        /// 2026-10-18 安卓滑动输入：鼠标/触摸统一走 BattlePointer（触摸点击=抬起裁决、
        /// 落点=手指原始点位；PC 口径零变化）。</summary>
        private void PollFieldPointer()
        {
            BattlePointer.Sample(); // 鼠标/触摸统一采样（同帧幂等）——本屏与选择循环共源
            _decision?.PollProbe(); // 取证心跳（2026-10-17 口径，2026-10-18 B3 收口进决策中枢；快照供数在装配处）

            // 牌库预览条让路（2026-10-26 定案：真实的目标选择触发时自动关闭渲染目标选择）：
            // 列表型（Region 选择）+棋盘型（守卫窗/选格/攻击选目标）任一在身即收
            if (_deckPreviewOwner >= 0 && ((_regionSelector != null && _regionSelector.IsOpen)
                || (_cellPicker != null && _cellPicker.Active)
                || (_choreo != null && _choreo.IsTargeting)
                || (_decision != null && _decision.GuardWindowOpen)))
                CloseDeckPreview();

            if (_opening != null && _opening.Active)
            {
                // 五段式开场闸（2026-10-19 掷币演出挪战场）：段一~四无 Core，游戏点击分支全拦——
                // 指针交开场导演（deckpick- 选卡组分发/其余点击=演出段快进），悬停层清空防残文案。
                _hoverProv.Clear();
                _opening.PollPointer();
                return;
            }

            ReconcileTileButtons(); // 可用动作位图每帧对账先行（3D 动作钮触发/内容/关闭单源；让路路径同样收钮）
            if (_choreo != null && _choreo.IsTargeting)
            {
                _hoverProv.Clear(); // 攻击目标选择中：悬停/点击由编舞器消费（寻路箭头跟随），本屏路由让路
                return;
            }
            if (_cellPicker != null && _cellPicker.Active)
            {
                _hoverProv.Clear(); // 放地/出牌选格中：点击由选格器消费（箭头跟随+候选格高亮），本屏路由让路
                return;
            }
            if (BattlePointer.OverUI) // UI 让路口径统一（2026-10-22 B2）：无参 IsPointerOverGameObject
                // 触摸下基本恒 false（不采手指位），与选择循环（BattlePointer）口径分裂——曾致触摸经
                // UGUI 区漏判为棋盘点击；OverUI 在本方法开头 Sample() 已刷新（鼠标/触摸同源）。
            {
                _hoverProv.Clear();
                return;
            }
            var cam = BattleStageDirector.WorldCam;
            if (cam == null || _view == null)
            {
                _hoverProv.Clear();
                if (BattlePointer.ConfirmPressed) CloseRegion();
                return;
            }
            RaycastHit hit;
            // 裁决点（触摸=手指原始点位；确认帧=确认落点）——悬停与点击同射线
            var aim = BattlePointer.ConfirmPressed ? BattlePointer.ConfirmPoint : BattlePointer.AimPoint;
            bool hasHit = Physics.Raycast(cam.ScreenPointToRay(aim), out hit, 300f);
            var hitCollider = hasHit ? hit.collider : null;
            var hitName = hitCollider != null ? hitCollider.gameObject.name : "";
            // 守卫窗口：点守卫者/角色/攻击者全归决策中枢——可守卫的己方格先弹头顶 3D 动作钮（守卫钮=选守卫者，
            // v4 §10.2 两段式），攻击者/无动作格原口直进（悬停帧 btnKind 空照常路由）。
            // 按钮组开着时其他 3D 点击（点攻击者确认等）同口径先收钮（2026-10-24 关闭钮退役定案随迁）
            if (_decision != null && _decision.GuardWindowOpen)
            {
                if (BattlePointer.ConfirmPressed && TryUnitActionButton(hitName)) return; // 3D 动作钮前置路由（先于格点击）
                if (BattlePointer.ConfirmPressed && TryOpenUnitActions(hitName)) return;
                if (BattlePointer.ConfirmPressed) _unitButtons.Close();
                _decision.HandlePointer(hitName, cam);
                return;
            }
            _hoverProv.Handle(hitName);
            if (BattlePointer.ConfirmPressed) HandleFieldClick(hitName);
        }

        // ---- 悬停路由 ----

        // ---- 悬停信息文案 ----

        /// <summary>阶段显示格文字（【己方主要阶段】式——BattleHudRow.SetPhase 喂文，8 字 2.6 号贴格宽）。</summary>
        private static string PhaseTileText(BattleViewData d)
            => PhaseTileText(d.Phase, d.MyTurn);

        /// <summary>阶段显示格文字（相位+行方直组——2026-10-26 事件序喂数：OnPhaseAnnounced 用事件载荷组字，
        /// 与对账版同源单口）。</summary>
        private static string PhaseTileText(PhaseType phase, bool myTurn)
            => $"【{(myTurn ? "己方" : "对手")}{BattleView.PhaseZh(phase)}阶段】";

        /// <summary>相位事件播报（投影器 PhaseStartEvent 钩子，引擎发布序同步入队）：本地局阶段字正源
        ///（2026-10-26 定案：StartNewTurn 同帧连发 准备→主要，状态对账只读终态会吞瞬态——事件序逐条
        /// 入队按 1s/拍播出）。网络局不订引擎事件到不了这里，快照喂数维持旧径（联机时序另立任务）。</summary>
        private void OnPhaseAnnounced(PhaseStartEvent e)
        {
            if (e == null || e.ActivePlayer == null) return;
            bool mine = e.ActivePlayer == P1;
            BattleHudRow.SetPhase(PhaseTileText(e.Phase, mine), mine);
        }

        /// <summary>诅咒/祝福牌库标记钩子（CounterChangedEvent·2026-10-26）：施加（Amount>0）=
        /// 卡堆顶爆发+脉冲（PlayDeckMarkAsync——诅咒黑爆/祝福白新星）；施加与消层（抽到发作
        /// Amount=0）都刷新开着的牌库预览条。</summary>
        private void OnDeckMark(CounterChangedEvent e)
        {
            if (e == null || e.Target == null) return;
            if (e.Amount > 0)
                _field?.PlayDeckMarkAsync(e.Target, p => p == P2 ? 1 : 0,
                    e.CounterType == CounterRules.BlessingCounter).Forget();
            RefreshDeckPreviewIfOpen(e.Target as Card);
        }

        /// <summary>角色【发动】键高亮门禁（2026-10-16 三键改版）：有技能卡且挂主动效果；
        /// 深判定（费用/每回合一次/阶段）留引擎在发动时裁决，拒绝走点击 toast。</summary>
        private bool HeroHasActivatableSkill()
            => HeroSkillSystem.SkillEffectsOf(HeroSkillSystem.ResolveSkillCard(Core, P1)).Count > 0;

        // ---- 悬停族转发桩（2026-10-18 拆分——单源在 BattleHoverInfoProvider，屏内调用点零改动） ----
        private static bool TryParseTile(string hitName, out int x, out int z)
            => BattleHoverInfoProvider.TryParseTile(hitName, out x, out z);

        private int ZoneCount(Player owner, Zone zone)
            => BattleHoverInfoProvider.ZoneCountOf(Core, owner, zone);

        private List<Card> ExposedDeckCards(Player owner)
            => BattleHoverInfoProvider.ExposedDeckCardsOf(Core, owner);

        // ---- 点击路由 ----

        /// <summary>左键点击路由（2026-10-26 定案：格前/弹窗两代入口退役——点有可行动作的己方单位/
        /// 角色格在头顶弹 3D 动作按钮组 BattleUnitActionButtons，动作处理器经 OnUnitActionButton
        /// 沿用既有入口；其余落点=收钮后走原信息展示语义）。功能地块/特殊格/地牌/施放图标路由不变。</summary>
        private void HandleFieldClick(string hitName)
        {
            int deckPreviewWas = _deckPreviewOwner; // 牌库预览条（2026-10-26）：任何棋盘点击先收；同堆再点=toggle 关（HandleZoneCellClick 消费）
            CloseDeckPreview();

            // 3D 动作钮命中前置（2026-10-26）：执行该动作即收钮——先于「关钮」防点击被吞
            if (TryUnitActionButton(hitName)) return;
            // 点单位弹可用动作（§10.2 语义随迁）：命中格在位图中且有任一可用项=开钮组（换目标即重开）；
            // 其余任何落点（含点空白/无动作格/无命中）=收钮
            if (TryOpenUnitActions(hitName)) return;
            _unitButtons.Close();

            // 功能地块（BattleHudRow 中线状态行：结束回合格可点；两时间/连锁速度格纯展示——
            // 未认领名走 TryParseTile 失败分支收弹窗，无需另接点击）
                if (hitName == "hud-tile-end-turn")
                {
                    Debug.Log("[HUD地块] 点击：结束回合");
                    CloseRegion();
                    if (IsTargetSelecting()) return; // Region 抉择/反问在身：先完成选择再推进（非模态须自防重入）
                    if (NetActive) { OnEndTurnFan(); return; } // 网络局：视图闸在 NetEndTurn（Core 侧门不适用）
                    // 仅己方回合可点（2026-10-14 定案）：对手回合就地提示，不再静默吞（引擎闸仍在 OnEndTurnFan 兜底）
                if (Core == null || _ctrl == null || _ctrl.TurnPlayer != P1)
                {
                    _toast.Show("对手回合进行中，暂不可结束回合");
                    return;
                }
                OnEndTurnFan();
                return;
            }

            // 施放占格类型图标（2026-10-15 发动格拆除）：点击=该方发动区列卡（Region 面板——反制指向/查看入口，
            // 原发动格点击入口随格拆除迁移至此）
            var casting = _hoverProv.CastingCardAt(hitName);
            if (casting != null)
            {
                OpenRegion(RegionKind.Activation, BoardLayout.OwnerOf(casting.X, casting.Z) == 0 ? P1 : P2);
                return;
            }

            int x, z;
            if (!TryParseTile(hitName, out x, out z)) { CloseRegion(); return; }

            // 特殊格分流（两侧列 x0/x7 不承载单位/地牌——BoardLayout 保证）
            var role = BoardLayout.RoleOf(x, z);
            if (role != CellRole.Unit && role != CellRole.Land && role != CellRole.Edge)
            {
                HandleZoneCellClick(role, BoardLayout.OwnerOf(x, z), deckPreviewWas);
                return;
            }

            // 单位格体（有动作的已在方法头弹窗返回；此处=无动作单位）=展示信息（悬停已出四段卡信息）
            var unit = (_view.SelfUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
            if (unit != null) { CloseRegion(); return; }

            var land = (_view.SelfLands ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z);
            if (land != null) { OnClickMyLand(land); return; }
            CloseRegion();
        }

        /// <summary>特殊格点击：墓地/除外/发动区/卡组/额外区→Region 面板；角色格→技能详情+操作组。
        /// 2026-10-17 点击门槛（用户定案）：墓地/除外须有卡才开列表面（无卡=点击无反馈，信息走悬停）；
        /// 卡组是隐藏区，只有存在被展示的卡（RevealRules 信息轴）才开——列表只列被展示的卡。
        /// 额外区（2026-10-22 衍生物模板区·原技能格位）：有模板卡才开（同墓地/除外门槛）。
        /// 卡组有诅咒/祝福标记卡时优先开标记预览条（2026-10-26——复用目标选中栏，OpenDeckPreview）。</summary>
        private void HandleZoneCellClick(CellRole role, int ownerIdx, int deckPreviewWasOwner = -1)
        {
            var owner = ownerIdx == 0 ? P1 : P2;
            if (owner == null) return;
            switch (role)
            {
                case CellRole.Graveyard:
                    if (ZoneCount(owner, Zone.Graveyard) > 0) OpenRegion(RegionKind.Graveyard, owner);
                    break;
                case CellRole.Exile:
                    if (ZoneCount(owner, Zone.Exile) > 0) OpenRegion(RegionKind.Exile, owner);
                    break;
                // CellRole.Activation 已随发动格拆除（2026-10-15）退役：Region 发动区入口迁至
                // 点击施放类型图标（cast-{rid} 路由——见 HandleFieldClick）
                case CellRole.Deck:
                    // 标记卡预览优先（2026-10-26）：诅咒/祝福标记在库→复用选择栏列标记卡（非交互）；
                    // 同堆再点=保持收起（toggle）；无标记保留旧被展示卡口径
                    if (MarkedDeckCards(owner).Count > 0)
                    {
                        if (deckPreviewWasOwner != ownerIdx) OpenDeckPreview(ownerIdx);
                        break;
                    }
                    if (ExposedDeckCards(owner).Count > 0) OpenRegion(RegionKind.Deck, owner);
                    break;
                // CellRole.Skill 已随技能格撤销（2026-10-16）退役：技能发动入口=角色格【发动】键
                //（HandleFieldClick btnKind 路由——OnClickHeroZone 链路），介绍=角色格悬停。
                // CellRole.Extra 已随额外区格整删（2026-10-29 P11）：额外区无盘面入口
                //（Zone.Extra 本体保留——衍生物模板区，P9b 域）；万能格 Any 只做展示无点击。
                case CellRole.Character: OnClickCharacterCell(owner); break;
            }
        }

        /// <summary>点角色格格体（2026-10-16 三键改版定案）：格体=展示信息语义（与单位格同款——
        /// 悬停出角色+技能介绍），攻击/守卫/发动技能拆到格前三键；旧【发动技能/角色攻击】
        /// 操作弹窗退役（OpenActionPopup/AddActionButton 一并拆除）。</summary>
        private void OnClickCharacterCell(Player owner)
        {
            CloseRegion();
            Debug.Log(owner == P1
                ? "[角色格] 我方角色格——操作走格前三键（悬停可看角色与技能介绍）"
                : "[角色格] 查看对方角色（悬停出信息）");
        }

        /// <summary>格前【攻击】键入口（2026-10-15 三键改版；原「点单位格直达」链路原样沿用）：
        /// 主阶段直达 3D 箭头选目标（横置/攻 0 门禁在 OnClickMyUnit 内 toast）；
        /// 结界等非生物单位无攻击宣言。教学期门禁照旧。</summary>
        private void ShowUnitActions(BattleCardView card, int x, int z)
        {
            if (IsTargetSelecting()) return; // 选择期点击=目标语义（两语义裁定）
            if (!GateMyMain()) return;       // 非我方主阶段（GateMyMain 自带 toast）
            // 教学期前置提示（引擎闸仍兜底——闸一直都在，这里把拒绝原因提前到点击时可见）
            var step = TutorialGuide.Active ? TutorialGuide.Current : null;
            if (step != null)
            {
                // attacker 空=宽松步（2026-10-17 教学两段式：任意己方生物可攻击——与引擎闸 GateAttack 同口径）
                if (step.type == "attack" && !string.IsNullOrEmpty(step.attacker) && step.attacker != card.CardId)
                {
                    _toast.Show($"引导要求用 {TutorialGuide.NameOf(step.attacker)} 攻击（{card.Name} 暂不可操作）");
                    return;
                }
                if (step.type == "guard") { _toast.Show(step.hint ?? "生物保留待守卫"); return; }
                if (step.type == "end") { _toast.Show(step.hint ?? "引导要求结束回合"); return; }
            }
            if (!IsCreatureCard(card)) { _toast.Show("该单位无法攻击"); return; }
            Debug.Log($"[BattleScreen] 攻击键点击：{card.Name}（{x},{z}）→ 攻击目标选择");
            OnClickMyUnit(card);
        }

        // ---- 单位可用动作位图 + 头顶 3D 动作按钮组（2026-10-26：格前/弹窗两代入口退役，位图单源喂按钮组） ----

        private struct CellActions { public int X, Z; public bool Attack, Guard, Activate; }

        private readonly List<CellActions> _cellActions = new List<CellActions>();
        private readonly BattleUnitActionButtons _unitButtons = new BattleUnitActionButtons();
        private bool _actBtnMyTurn;         // 开按钮组时的回合/阶段印章（变化=关，§10.2 关闭条件随迁）
        private PhaseType _actBtnPhase;
        private int _actBtnTurnNo;

        // 三键效果缓存（2026-10-18 性能）：UnitActivatedEffects/HeroHasActivatableSkill 每次跑完整效果
        // 定义转换（ConvertAll+LINQ），改事件级重算、帧间复用；SelfUnits 每次 RefreshField 重建，
        // 引用比对兜底 + RefreshField 尾置脏（StackEmpty 订阅兜住变形/临摹等改写效果的结算路径）
        private readonly Dictionary<BattleCardView, bool> _unitActivateFlags = new Dictionary<BattleCardView, bool>();
        private readonly Dictionary<BattleCardView, bool> _unitPendingFlags = new Dictionary<BattleCardView, bool>();
        private bool _activateFlagsDirty = true;
        private List<BattleCardView> _activateFlagsFor;
        private bool _heroActivateFlag;

        /// <summary>可用动作位图每帧对账（2026-10-26 头顶 3D 钮改版：位图落 _cellActions 单源——
        /// 点单位弹按钮组的触发/内容/关闭都吃这里）：编舞选目标/放格选格/
        /// 网络局/反问在身=清空+收钮；守卫窗口=守卫候选亮（窗口独占）；常态=己方单位格——
        /// 攻击=生物未横置攻&gt;0 且我方主阶段、发动=有主动效果且我方主阶段、守卫=仅窗口内。
        /// 角色格：攻击=有弹药且我方主阶段、发动=技能挂主动效果、守卫=窗口内且角色在候选。
        /// 深判定（费用/横置代价/耐久/每回合一次）留引擎裁决，位图不预判，拒绝走点击 toast。</summary>
        private void ReconcileTileButtons()
        {
            if (_view == null || !BattleBoardGrid.Ready
                || (_choreo != null && _choreo.IsTargeting)
                || (_cellPicker != null && _cellPicker.Active)
                || NetActive || IsTargetSelecting())
            {
                _cellActions.Clear();
                _unitButtons.Close();
                return;
            }

            bool myMain = !_gameEnded && _view.MyTurn && _view.Phase == PhaseType.Main && !IsTargetSelecting();
            var units = _view.SelfUnits;
            if (!ReferenceEquals(_activateFlagsFor, units) || _activateFlagsDirty)
            {
                _activateFlagsFor = units;
                _activateFlagsDirty = false;
                _unitActivateFlags.Clear();
                _unitPendingFlags.Clear();
                int withPending = 0; // TEMP 诊断（定位后删）
                if (units != null)
                {
                    foreach (var c in units)
                    {
                        if (c == null || c.CoreCard == null) continue;
                        // 发动键点亮两源分记（2026-10-20 一步点击修订）：自身主动效果（发动即横置=
                        // 每回合一发闸——点亮须未横置，镜像攻击键，横置判定在对账现场取活值）**或
                        // 待发主动奖励**（自由分支 tactivate=2 条件达成入队——入队随引擎事件、发动
                        // 无横置闸，点亮不吃横置）。深判定（速度门）留引擎，亮了被拒走 toast
                        bool hasPending = PendingRewardsOf(c.CoreCard).Count > 0;
                        if (hasPending) withPending++;
                        _unitActivateFlags[c] = UnitActivatedEffects(c).Count > 0;
                        _unitPendingFlags[c] = hasPending;
                    }
                }
                Debug.Log($"[BR-Flags] 三键旗重算：单位数={units?.Count ?? 0} 含待发奖励={withPending} myTurn={_view?.MyTurn} phase={_view?.Phase}"); // TEMP 诊断（定位后删）
                _heroActivateFlag = P1 != null && HeroHasActivatableSkill();
            }
            _cellActions.Clear();
            if (units != null)
            {
                foreach (var c in units)
                {
                    if (c == null || c.IsDead || c.CoreCard == null || c.X < 0 || c.Z < 0) continue;
                    var e = new CellActions { X = c.X, Z = c.Z };
                    if (_decision != null && _decision.GuardWindowOpen)
                        e.Guard = _decision.GuardCandidates != null
                                  && _decision.GuardCandidates.Exists(o => o != null && ReferenceEquals(o.SourceCard, c.CoreCard));
                    else
                    {
                        e.Attack = myMain && IsCreatureCard(c) && !c.IsTapped
                                   && (c.Runtime == null || c.Runtime.Power > 0);
                        // 发动键：主动效果须未横置（引擎 ActivateEffect 横置闸的前置显示——一步点击
                        // 定案后发动即横置，亮死键=点两次案残留）；待发奖励不受横置影响
                        e.Activate = myMain && ((_unitActivateFlags.TryGetValue(c, out var hasDef) && hasDef && !c.IsTapped)
                                                || (_unitPendingFlags.TryGetValue(c, out var hasPend) && hasPend));
                    }
                    _cellActions.Add(e);
                }
            }

            // 角色格位图（2026-10-16）：Attack=弹药>0、Activate=技能挂主动效果、
            // Guard=守卫窗口内且角色在候选（ReferenceEquals 匹配 SourcePlayer——引擎侧弹药门同源）
            if (P1 != null)
            {
                var hc = BoardLayout.CharacterCell(0);
                var hero = new CellActions { X = hc.x, Z = hc.z };
                if (_decision != null && _decision.GuardWindowOpen)
                    hero.Guard = _decision.GuardCandidates != null
                                 && _decision.GuardCandidates.Exists(o => o != null && ReferenceEquals(o.SourcePlayer, P1));
                else
                {
                    hero.Attack = myMain && P1.GetPower() > 0;
                    hero.Activate = myMain && _heroActivateFlag;
                }
                _cellActions.Add(hero);
            }

            // 按钮组随行对账（§10.2 关闭条件随迁）：阶段或回合变化（印章对拍）——选目标/网络局在方法头已收
            if (_unitButtons.IsOpen
                && (_view.MyTurn != _actBtnMyTurn || _view.Phase != _actBtnPhase || _view.TurnNumber != _actBtnTurnNo))
                _unitButtons.Close();
            // 绑定格位图对账（2026-10-26 新增）：仍在位图=刷新可用项（守卫窗翻转/横置降档自适应）；
            // 失位（单位死亡/离场/格位变动）=收钮——位图单源裁决，钮组不自持可用性
            if (_unitButtons.IsOpen)
            {
                bool found = false, a = false, g = false, c = false;
                for (int i = 0; i < _cellActions.Count; i++)
                    if (_cellActions[i].X == _unitButtons.BoundX && _cellActions[i].Z == _unitButtons.BoundZ)
                    {
                        found = true;
                        a = _cellActions[i].Attack; g = _cellActions[i].Guard; c = _cellActions[i].Activate;
                        break;
                    }
                if (found) _unitButtons.SetBits(a, g, c);
                else _unitButtons.Close();
            }
        }

        /// <summary>§10.2（随迁 2026-10-26）：命中的格有可用动作（位图单源=_cellActions）→ 头顶弹
        /// 3D 动作按钮组。网络/无动作=false（调用方走原路由=收钮）。守卫窗口内同样先弹（守卫钮→PickGuarderAt）。</summary>
        private bool TryOpenUnitActions(string hitName)
        {
            if (NetActive) return false; // 网络局不弹（§10.2 关闭条件）
            int x, z;
            if (!TryParseTile(hitName, out x, out z)) return false;
            for (int i = 0; i < _cellActions.Count; i++)
            {
                if (_cellActions[i].X != x || _cellActions[i].Z != z) continue;
                if (!_cellActions[i].Attack && !_cellActions[i].Guard && !_cellActions[i].Activate) return false;
                OpenUnitActions3D(x, z, _cellActions[i]);
                return true;
            }
            return false;
        }

        /// <summary>3D 动作钮点击路由（2026-10-26）：命中名=本钮（unit-act-*）且钮组在开=执行动作
        /// （Fire 先关再回调）；其余=false（调用方走原格点击路由）。</summary>
        private bool TryUnitActionButton(string hitName)
        {
            if (!_unitButtons.IsOpen || !BattleUnitActionButtons.IsActionButton(hitName)) return false;
            _unitButtons.Fire(hitName);
            return true;
        }

        /// <summary>开/换钮组：锚=单位头顶位（2026-10-26 迁格后 BattleFieldView 头顶锚=模型头顶高
        /// +0.25——按钮组落旧读数正上方；角色=柱顶读数位；未建件回落格上 2.0），动作为零分配转发到既有入口。</summary>
        private void OpenUnitActions3D(int x, int z, CellActions e)
        {
            bool isHero = x == BoardLayout.P1CharX && z == BoardLayout.P1CharZ;
            BattleCardView unit = null;
            if (!isHero)
                unit = (_view.SelfUnits ?? new List<BattleCardView>())
                    .FirstOrDefault(c => c != null && c.X == x && c.Z == z && !c.IsDead);
            var statsAnchor = _field != null
                ? (isHero ? _field.HeroStatsAnchor(0)
                          : (unit != null ? _field.StatsAnchorOf(unit.RuntimeId) : null))
                : null;
            var anchor = statsAnchor ?? (BattleBoardGrid.WorldPositionOf(x, z) + Vector3.up * 2.0f);
            _actBtnMyTurn = _view.MyTurn; // 关闭印章（ReconcileTileButtons 对拍）
            _actBtnPhase = _view.Phase;
            _actBtnTurnNo = _view.TurnNumber;
            _unitButtons.Show(anchor, x, z, e.Attack, e.Guard, e.Activate,
                              kind => OnUnitActionButton(kind, isHero, unit, x, z));
        }

        /// <summary>钮组动作分流（§10.2：处理器全沿用三键/弹窗时代的现有入口）：
        /// 攻击=单位 ShowUnitActions/角色 OnClickSelfHero；发动=单位主动效果/角色技能；
        /// 守卫=守卫窗口专属（PickGuarderAt=原点格选守卫者同口）。</summary>
        private void OnUnitActionButton(BattleUnitActionButtons.Kind kind, bool isHero, BattleCardView unit, int x, int z)
        {
            if (kind == BattleUnitActionButtons.Kind.Guard)
            {
                if (_decision != null && _decision.GuardWindowOpen) _decision.PickGuarderAt(x, z);
                else _toast.Show("守卫在对手攻击宣言后的守卫窗口内可用");
                return;
            }
            CloseRegion();
            if (isHero)
            {
                if (kind == BattleUnitActionButtons.Kind.Attack) OnClickSelfHero();
                else OnClickHeroZone();
                return;
            }
            if (unit == null) return; // 单位已离场（对账竞态）
            if (kind == BattleUnitActionButtons.Kind.Attack) ShowUnitActions(unit, x, z);
            else OnClickUnitEffectButton(unit).Forget();
        }

        /// <summary>单位可发动效果清单（三键高亮/点击单源）：引擎效果定义中时机=主动两档
        ///（Activate_Active/Activate_Instant——响应式 Activate_Response 归响应窗口不在此口）。
        /// CoreCard 缺（网络模式/异常数据）=空。</summary>
        private static List<EffectDefinition> UnitActivatedEffects(BattleCardView card)
        {
            if (card?.CoreCard == null) return new List<EffectDefinition>();
            // 结界禁主动（2026-10-15 改版）：来源闸镜像——防御旧数据/运行时改写，不亮死按钮
            if (CardCore.Attribute.CounterRules.UsesDurabilityEconomy(card.CoreCard))
                return new List<EffectDefinition>();
            return GameActions.GetCardEffectDefinitions(card.CoreCard)
                .Where(d => d != null
                    && (d.TriggerTiming == TriggerTiming.Activate_Active
                        || d.TriggerTiming == TriggerTiming.Activate_Instant))
                .ToList();
        }

        /// <summary>格前【发动效果】键点击（2026-10-15 三键改版：单位由参数直给，原头顶按钮路由退役）：
        /// 待发优先（2026-10-20 主动运势两段式：第一次点击=掷骰入待发，第二次点击=发动待发）——
        /// 有待发奖励时不再与主动效果混选：单个直达发动、多个只在待发里择一；无待发才走主动效果
        /// （掷骰后生物已横置，本回合再掷被 ActivateEffect 横置闸拒，(本回合0/1) 闭环）。
        /// 目标不在此弹——引擎结算期反问经 TargetSelectionService（Current=RegionTargetSelector）
        /// 弹 Region 选卡面板。门禁时点/优先权/费用/横置/耐久全在 GameActions.ActivateEffect 裁决，
        /// 拒绝 toast。</summary>
        private async UniTaskVoid OnClickUnitEffectButton(BattleCardView card)
        {
            if (card?.CoreCard == null) return;
            if (IsTargetSelecting()) return; // 选择期点击=目标语义（两语义裁定）
            if (!GateMyMain()) return;
            var defs = UnitActivatedEffects(card);
            var pendings = PendingRewardsOf(card.CoreCard); // 待发主动奖励（自由分支·2026-10-20）
            if (pendings.Count > 0)
            {
                if (pendings.Count == 1)
                {
                    ActivatePendingReward(pendings[0]);
                    return;
                }
                var popts = new List<string>(pendings.Count);
                foreach (var pe in pendings)
                    popts.Add((string.IsNullOrEmpty(pe.Effect?.DisplayName) ? "待发奖励" : pe.Effect.DisplayName)
                              + "（奖励待发）");
                int? pp = await PickRegionOptionAsync("选择待发奖励", popts);
                if (pp == null || pp.Value < 0 || pp.Value >= pendings.Count) return;
                ActivatePendingReward(pendings[pp.Value]);
                return;
            }
            if (defs.Count == 0) return;
            if (defs.Count == 1)
            {
                ActivateUnitEffect(card, defs[0]);
                return;
            }

            var options = new List<string>(defs.Count);
            for (int i = 0; i < defs.Count; i++)
                options.Add(string.IsNullOrEmpty(defs[i].DisplayName) ? $"主动效果 {i + 1}" : defs[i].DisplayName);
            int? picked = await PickRegionOptionAsync("选择效果", options);
            if (picked == null) return;
            if (picked.Value < 0 || picked.Value >= defs.Count) return;
            ActivateUnitEffect(card, defs[picked.Value]);
        }

        /// <summary>该卡的待发主动奖励（BRANCH_REWARD·自由分支 tactivate=2 条件达成入队）：
        /// 发动键点亮与点击分流共用。速度门深判定留引擎（PlayerActivateVoluntary 裁决）。</summary>
        private List<PendingEffect> PendingRewardsOf(Card card)
        {
            var list = new List<PendingEffect>();
            if (card == null || Core?.StackEngine == null || P1 == null)
            {
                Debug.Log($"[BR-Pending] 查询跳过：card={(card as CardCore.IHasName)?.CardName} coreNull={Core?.StackEngine == null} p1Null={P1 == null}"); // TEMP 诊断（定位后删）
                return list;
            }
            var activatable = Core.StackEngine.GetActivatableVoluntaryEffects(P1);
            foreach (var pe in activatable)
                if (pe != null && ReferenceEquals(pe.Source, card)) list.Add(pe);
            // TEMP 诊断日志（2026-10-20 倒计时按钮不亮排查——定位后删）：只在有队列项或匹配时刷
            if (activatable.Count > 0 || list.Count > 0)
                Debug.Log($"[BR-Pending] 卡={(card as CardCore.IHasName)?.CardName} 队列可发动={activatable.Count}"
                    + $"（各源：{string.Join("|", activatable.Where(pe => pe != null).Select(pe => (pe.Source as CardCore.IHasName)?.CardName ?? pe.Source?.ToString()))}）"
                    + $" 本卡匹配={list.Count} 栈大小={Core.StackEngine.StackSize}");
            return list;
        }

        /// <summary>发动待发主动奖励（2026-10-20 自由分支定案）：入栈后走响应窗泵（对手可连锁；
        /// 无候选自动双 Pass 结算——技能发动同款）。速度门被拒回插队列不丢。</summary>
        private void ActivatePendingReward(PendingEffect pe)
        {
            var name = pe?.Effect?.DisplayName ?? "待发奖励";
            Debug.Log($"[BattleScreen] 待发奖励发动：{name}");
            bool ok = Core != null && Core.StackEngine.PlayerActivateVoluntary(pe);
            if (!ok) _toast.Show("无法发动（速度门禁）");
            else _ctrl.SettleResponseWindow().Forget();
            RefreshField();
        }

        /// <summary>发动单位主动效果（本地局）：入栈后无响应候选自动双 Pass（技能发动同款）；
        /// 生物横置/结界耐久消耗随 RefreshField 对账刷新。</summary>
        private void ActivateUnitEffect(BattleCardView card, EffectDefinition def)
        {
            Debug.Log($"[BattleScreen] 单位效果发动：{card.Name}（{card.CardId}）→ {def.DisplayName ?? def.Id}");
            bool ok = GameActions.ActivateEffect(Core, P1, def, card.CoreCard);
            if (!ok) _toast.Show("无法发动（费用/横置/耐久/阶段门禁）");
            else _ctrl.SettleResponseWindow().Forget();
            RefreshField();
        }

        private RegionTargetSelector _regionSelector; // 结算期反问选择器（null=自动代选）
        private BattleRegionPanel _regionPanelCtl;    // Region 浏览面板（2026-10-18 拆分·Battle/BattleRegionPanel.cs）
        private int _deckPreviewOwner = -1;           // 牌库预览条开着的属主（-1=关·2026-10-26 诅咒/祝福标记预览）

        // ---- Region 面板守卫转发（2026-10-18 拆分——本体在 BattleRegionPanel；选择面占用期间
        //      不开关面板=窗口蒸发教训 2026-10-15，守卫留在屏侧因 _regionSelector 是决策流中枢） ----
        private void OpenRegion(RegionKind kind, Player owner)
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return; // 目标选择进行中：Region 被选择面占用
            CloseDeckPreview(); // 预览条与浏览面板互斥（2026-10-26）
            _regionPanelCtl?.Open(kind, owner);
        }

        private void CloseRegion()
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return; // 选择进行中（弃牌等反问占用选择面）
            CloseDeckPreview(); // 2026-10-26：预览条随浏览面板同收（棋盘点击路径已在 HandleFieldClick 头收过，此处兜其他调用口）
            _regionPanelCtl?.Close();
        }

        private void RefreshRegion()
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return;
            _regionPanelCtl?.Refresh();
        }

        // ---- 牌库标记预览（2026-10-26 诅咒/祝福显示层补充——效果描述不提，纯可见性增强） ----

        private List<Card> MarkedDeckCards(Player owner)
            => BattleHoverInfoProvider.MarkedDeckCardsOf(Core, owner);

        /// <summary>牌库标记常驻氛围对账（ApplyProjection 链）：牌库中存在诅咒/祝福标记卡=对应
        /// 氛围件常亮。幂等全量——事件丢拍自愈，与 SetDeckCount 同生命周期节拍。</summary>
        private void ReconcileDeckMarks()
        {
            if (Core == null) return;
            for (int owner = 0; owner <= 1; owner++)
            {
                var p = owner == 0 ? P1 : P2;
                if (p == null) continue;
                bool curse = false, bless = false;
                var cards = Core.ZoneManager.GetCards(p, Zone.Deck);
                if (cards != null)
                    foreach (var c in cards)
                    {
                        if (c == null) continue;
                        if (!curse && c.GetCounterCount(CounterRules.CurseCounter) > 0) curse = true;
                        else if (!bless && c.GetCounterCount(CounterRules.BlessingCounter) > 0) bless = true;
                    }
                BattleZoneProps.SetDeckMarks(owner, curse, bless);
            }
        }

        /// <summary>打开牌库标记预览条（复用目标选中栏的非交互显示面）：列出该牌库被诅咒/祝福
        /// 标记的卡（ACardView 全卡面+指示物芯片）。空库/无选择器静默不开。</summary>
        private void OpenDeckPreview(int ownerIdx)
        {
            if (_regionSelector != null && BuildDeckPreview(ownerIdx, out var title, out var items))
            {
                _deckPreviewOwner = ownerIdx;
                _regionSelector.ShowPreview(title, items);
            }
        }

        private void CloseDeckPreview()
        {
            if (_deckPreviewOwner < 0) return;
            _deckPreviewOwner = -1;
            _regionSelector?.ClosePreview();
        }

        /// <summary>预览内容装配（标题+卡面）：诅咒在前祝福在后；false=该库无标记卡。</summary>
        private bool BuildDeckPreview(int ownerIdx, out string title, out List<CardOverlayItem> items)
        {
            title = null;
            items = null;
            var owner = ownerIdx == 0 ? P1 : P2;
            var marked = MarkedDeckCards(owner);
            if (owner == null || marked.Count == 0) return false;
            int curse = marked.Count(c => c.GetCounterCount(CounterRules.CurseCounter) > 0);
            items = marked.Select(CardOverlayItem.FromLiveCard).ToList();
            title = $"{(ownerIdx == 0 ? "己方" : "对方")}牌库 · 诅咒 {curse} · 祝福 {marked.Count - curse}";
            return true;
        }

        /// <summary>标记变化（施加/消层）刷新开着的预览条：属主匹配才重建，标记清空即收。</summary>
        private void RefreshDeckPreviewIfOpen(Card card)
        {
            if (_deckPreviewOwner < 0 || _regionSelector == null) return;
            var ownerPlayer = card != null ? (card.GetOwner() ?? card.GetController()) : null;
            if (ownerPlayer != null && (ownerPlayer == P1 ? 0 : 1) != _deckPreviewOwner) return;
            if (BuildDeckPreview(_deckPreviewOwner, out var title, out var items))
                _regionSelector.ShowPreview(title, items);
            else
                CloseDeckPreview();
        }

        // TempWalletLines（临时元素 Region 面板行）：2026-10-14 技能格定案随 RegionKind.Temp 一并退役
        // ======================================== 英雄区（2026-10-13 区域变化：技能与角色同区） ========================================

        /// <summary>是否有进行中的目标选择/弹窗：本屏列表模态在身，或引擎结算期反问在途。
        /// <b>两语义裁定（2026-10-13 用户定案）：目标选择期点击角色=「把角色选为目标」；
        /// 主动阶段（我方主阶段·无待选）点击角色=「发动技能」。二者不得混流——选择期一切
        /// 角色点击先走目标语义，实体点选接线属 3D 层目标选择重做（当前列表模态自带遮罩挡点击）。</summary>
        private bool IsTargetSelecting()
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return true;  // Region 选择面在身（反问/响应窗/选项抉择——2026-10-16 弹窗退役）
            if (TargetSelectionService.PendingRequests > 0) return true; // 引擎结算期反问在途
            if (_choreo != null && _choreo.IsTargeting) return true;     // 3D 寻路箭头选目标在途（2026-10-13）
            return false;
        }

        /// <summary>点英雄区（左键）：发动英雄技能（2026-10-13 区域变化定案——英雄技能与角色共用本区，
        /// 主动阶段选中英雄即发动技能）；未携带技能的局回落角色攻击。
        /// 目标选择期点击不走技能（见 IsTargetSelecting 两语义裁定）。</summary>
        private void OnClickHeroZone()
        {
            if (IsTargetSelecting()) return; // 选择期点击=目标语义，由选择器接管（模态遮罩本就挡住；防御性双保险）
            if (!GateFanMyMain()) return;
            var skill = HeroSkillSystem.ResolveSkillCard(Core, P1);
            if (skill == null) { _toast.Show("本局无技能卡"); return; } // 无技能局：发动键本就不亮（防直点兜底）
            ActivateSkillAsync().Forget();
        }

        /// <summary>过渡面主阶段闸（活数据口径）：GateMyMain 依赖 _view（旧渲染链刷新产物，扇形过渡面
        /// 恒 null——纯扇形流程无人调 RefreshLocal，首点必然静默失败）。本地局直接读 TurnEngine 现值。</summary>
        private bool GateFanMyMain()
        {
            if (_gameEnded) return false;
            if (IsTargetSelecting()) return false; // Region 抉择/反问在身不开新交互（同 GateMyMain，静默）
            if (_ctrl == null || Core == null || P1 == null) return false;
            var phase = Core.TurnEngine?.CurrentPhase;
            if (_ctrl.TurnPlayer != P1 || phase == null || phase.Phase != PhaseType.Main)
            {
                _toast.Show(_ctrl.TurnPlayer != P1 ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return false;
            }
            return true;
        }

        // RefreshHeroZone/TempTokenColor（HUD 英雄区按钮+临时元素指示条刷新）：2026-10-14 定案随
        // btn-self-hero/temp-elements 一并退役——角色生命/弹药=角色格悬停、技能=角色格悬停+三键
        //（2026-10-16 技能格撤销后口径，黑白钱包=地牌行黑白元素格）。

        // ---- 中栏按钮 ----
        // （跳过准备阶段按钮已删：准备阶段纯自动推进——TurnEngine.StartNewTurn 内
        //   AdvanceFromStandby 直调，2026-09-24 定案；UI 不再有停等入口。）

        // 墓地使用入口（OnGraveyardPlay）已随 2026-10-30 归土改版整删：墓地视手牌使用退役，
        // 归土=回合末自动复活（引擎 RuleAuraSystem.ReviveAtTurnEnd，无 UI 交互）。

        // 技能=一回合一次重复用的法术（2026-10-18 深夜修订）：与同名法术**不同速不同费**——
        // 发动速度恒 0（不吃法术槽位盈余）；免元素锚价，但多效果（抉择）技能每次发动另收 1 灰。
        // 多效果技能与法术同款施放时抉择（Region 面板 PickRegionOptionAsync——2026-10-16 弹窗
        // 退役后的唯一抉择 UI，AI 侧 TideActionType.HeroSkill 的 ModeIndex 同为效果序号）。

        private async UniTaskVoid ActivateSkillAsync(int effectIndex = 0)
        {
            if (NetActive) { NetActivateSkillAsync().Forget(); return; } // 网络局：intent 上行（服务器横置闸）
            var skill = HeroSkillSystem.ResolveSkillCard(Core, P1);
            var effects = skill != null ? HeroSkillSystem.SkillEffectsOf(skill) : null;
            if (effects != null && effects.Count > 1)
            {
                var options = new List<string>();
                for (int i = 0; i < effects.Count; i++)
                {
                    string name = string.IsNullOrEmpty(effects[i]?.DisplayName) ? $"效果 {i + 1}" : effects[i].DisplayName;
                    // 抉择技能定价=每次发动 1 灰（引擎 ActivationBill 同源）；发动速度恒 0
                    options.Add($"{name}（付1灰·速度0）");
                }
                int? picked = await PickRegionOptionAsync("选择要发动的效果", options);
                if (picked == null) return; // 缺 Region=无交互（本已点「发动」，静默取消）
                effectIndex = picked.Value;
            }
            bool ok = await GameActions.ActivateHeroSkill(Core, P1, effectIndex);
            if (!ok) _toast.Show("无法发动技能（本回合已用/阶段不对/抉择费1灰付不出）");
            else _ctrl.SettleResponseWindow().Forget(); // 无候选自动双 Pass，无害
            RefreshField(); // 地牌横置/角色弹药随对账刷新（旧渲染链字段已不绑）
        }

        /// <summary>投降确认（2026-10-17 提示弹窗改 TipPanel：确定、第二键=取消——无超时
        /// 不会误投降；Region 只留给对局中目标/效果选择）。终局后本键=返回主菜单（MarkGameOverUi 改文案）。</summary>
        private async UniTaskVoid OnConcede()
        {
            if (_gameEnded) { Manager.Back(); return; } // 终局：退出通道（旧弹窗取消键职责迁移）
            if (await ShowTipPromptAsync("投降确认", "确认", "取消") != true) return; // 取消/拆屏=不动
            if (NetActive)
            {
                // 网络局：认输 intent——终局经事件流回传（MarkGameOverUi），再走返回通道
                _netClient.Send<object>(NetworkMessageType.IntentConcede, null);
                return;
            }
            Core.EndGame(P2, GameOverReason.Concede);
            RefreshLocal();
        }

        // ======================================== 本地响应窗口（发动弹窗·沿用旧语义） ========================================

        // ======================================== 选项抉择（Region 卡牌列表） ========================================

        // 旧现搭弹窗层（ShowPickOverlay/ShowChoiceOverlay/ShowOverlay/AddCancelButton/
        // SetCancelText/AddOverlayRow/CloseOverlay/OnOverlayCancel——UiKit.Overlay 代码兜底 +
        // ModalBox 全屏遮罩）已随 2026-10-16 定案整删：遮罩拦棋盘点选（守卫窗口卡死）屡次坑人，
        ///BattleUI 内一切临时弹窗 UI 禁止回潮——纯选项抉择统一走下方 PickRegionOptionAsync。

        /// <summary>Region 卡牌列表选一项（2026-10-16 弹窗退役）：选项=最小卡面（卡名=选项文本），
        /// min=1 强制选（无取消口——已进入的流程选一项完成；超时代选首项）。null=缺 Region
        /// 或无选项（调用方按取消处理）。危险确认（如投降）把「取消」放首位——超时代选只会落取消。</summary>
        private async UniTask<int?> PickRegionOptionAsync(string title, IReadOnlyList<string> options)
        {
            if (_regionSelector == null || options == null || options.Count == 0) return null;
            var picked = await _regionSelector.SelectIndicesAsync(options, 1, 1, title, "", false, 60f);
            return picked != null && picked.Count > 0 ? (int?)picked[0] : null;
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

        // ======================================== 选择/响应提示（2026-10-17 label-num 并入定案） ========================================
        // lbl-toast 面板退役（用户挪入 Region 更名 label-num·与 btn-notchain 成对）：提示只有两类
        // ①效果发动的目标选择——介绍完整效果（说明选目标来干嘛）；②响应时——说明响应的是对方的
        // 什么发动。两类全文由 RegionTargetSelector 直写 label-num（+（x/y）计数），不再走顶层 toast。
        // 取消口成对不变：主动效果（生物/法术）可「取消不发动」（AllowCancel→空目标落空）；
        // 可连锁响应可「不连锁」；自动/强制发动不可拒——不选=超时默认代选（Service AutoPick 既有口径）。
        // 场面信息（地牌/单位/格子介绍）不走提示——hover-info 单源（LandCellInfo/CardHoverInfo）。

        /// <summary>选择窗 label-num 提示文案：PromptSource（宿主卡）+PromptEffectId 查卡表
        /// AtomText.RenderEffectSummary 渲染完整效果文（卡面/悬停同源）；查不到效果只报卡名+标题；
        /// 无宿主回落 Title（弃牌「回合结束，手牌超出X张」等自带说明的标题即正文）。</summary>
        private string BuildSelectPrompt(TargetSelectionRequest req)
        {
            string title = string.IsNullOrWhiteSpace(req.Title) ? "请选择目标" : req.Title;
            if (req.PromptSource is Card host)
            {
                var data = host is CardWrapper w ? w.GetData() : null;
                var fx = string.IsNullOrEmpty(req.PromptEffectId)
                    ? null
                    : data?.Effects?.FirstOrDefault(e => e != null && e.Id == req.PromptEffectId);
                if (fx != null)
                {
                    string body = AtomText.RenderEffectSummary(
                        new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, data);
                    string fxName = string.IsNullOrEmpty(fx.DisplayName) ? title : fx.DisplayName;
                    return $"【{CardDisplayName(host)}·{fxName}】{body}\n——为上述效果选择目标";
                }
                return $"【{CardDisplayName(host)}】{title}\n——选择目标";
            }
            return title;
        }




        // 声明期目标原子查找（FindTargetingAtomic/FirstTargetingAtomicOf）已随 2026-10-16
        // 法术声明期预选退役删除——目标选择统一走结算期反问（TargetSelectionService）。

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
