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
    public sealed partial class BattleScreen : UIScreen
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
        // 英雄区/临时元素（2026-10-16 技能格撤销定案）：技能格退役——技能发动=角色格头顶【发动】钮
        //（OnClickHeroZone 链路）、技能介绍=角色格悬停两侧同挂（2026-11-02 定案：角色描述含技能描述）、
        // 角色生命/弹药=角色格悬停（黑白元素格已随 2026-10-28 元素缩编退役）。
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
        private BattlePlayhead _director;     // 剧本播放器（2026-11-11 演出统一 S1·Battle/Playback/BattlePlayhead.cs——原步进导演能力搬迁；字段名沿用，S5 全量换口径时再正名）
        private LocalFilmSource _localSource; // 本地实时帧生产者（2026-11-11 S1·Battle/Playback/LocalFilmSource.cs——投影器三钩子→帧）
        private BattleOpeningDirector _opening;     // 五段式开局导演（2026-10-19 掷币演出挪战场·Battle/BattleOpeningDirector.cs）

        // ---- 网络模式（2026-10-21 P2P 复活）：本屏不建 BattleController——快照驱动渲染 + intent 上行 ----
        // 房主侧引擎也只归服务器泵（NetSessionServer.Current），UI 一律走客户端路径（座位过滤防全信息泄漏）。
        private NetGameClient _netClient;           // 网络局客户端（OnEnter 消费 BattleEntry.Client 一次性移交）
        private MsgGameStateSync _netSnapshot;      // 最近快照（网络反问候选/守卫栈条目查询用）
        private string[] _netSubmittedDeckIds;      // 网络五段式段五提交的卡组（manifest 对账用，2026-10-28）
        private string[] _netSubmittedTokenIds;     // P9b 提交的衍生物区（摘要对账用；随机预组=null）
        private List<DeckTokenBinding> _netSubmittedTokenBindings; // P9b 提交的槽绑定（摘要对账用）
        private int _netTurnSecondsLeft = -1;       // v14 回合计时：快照携带的剩余秒数（<0=无计时）
        private float _netTurnClockAt;              // v14 回合计时：该快照收到时刻（realtimeSinceStartup）
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
            // 剧本播放器（2026-11-11 演出统一 S1；二期 A 起 LiveData/Replay 同帧体）：先于投影器构造
            //——投影器钩子经 LocalFilmSource 产帧投它；依赖全走 Func 惰性取（_choreo 等构建期可能未就绪）
            _director = new BattlePlayhead(() => Core, () => _projector, () => _field,
                () => _chainStage, () => _choreo, RefreshFans);
            // 演出执行器（二期 A：本地实时/回放共用——投影器演出 op 经 Present 路由到它）
            _fxExecutor = new BattleFxOpExecutor(() => _field);
            // 帧生产者+演出单源路由（二期 A）：轮内 op 挂帧随播、轮外 op 即时执行；轮外行即时入列
            _localSource = new LocalFilmSource(_director, () => Core, () => _projector, () => _choreo,
                op => _fxExecutor.ExecuteAsync(op).Forget(),
                line => _log?.AppendLines(new[] { line }));
            _hudTicker = new BattleHudTicker(() => _view, NetTurnClockText);
            _tutorial = new BattleTutorialDirector(() => Root, () => Core, () => _ctrl, () => _field, _toast,
                ShowTipPromptAsync, () => Manager.Back(), RefreshField, RefreshFans, OnEndTurnFan,
                () => _gameEnded, v => _gameEnded = v,
                () => Manager.Replace<BattleScreen>(), // 教学三重开战场（2026-10-19）：弹栈重入=五段开场重放
                () => _director != null && _director.Busy); // 表现在途探针（2026-10-20 运势段被截修复）：回放导演 Busy
            _projector = new BattleProjector(() => Core, () => _ctrl, ApplyProjection, RefreshView,
                () => _fanFlyArmed = true, ArmDrawFlyIn,
                e => { if (e?.Card != null) _field?.InterruptCasting(e.Card.RuntimeId); },
                OnGameOverFan, () => _pendingPins.Clear(),
                () => _director != null && _director.Busy, // 表现在途探针（2026-10-20「真相源=行可见」；二期 A 接 BattlePlayhead.Busy——轮始即真，接替旧日志 Hold 口径）
                _localSource); // 生命周期钩子+演出 op 单源路由（Present：轮内挂帧/轮外即时）

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
                    CollapseSinkRatio = 0.8f, // 扇外点击收扇到最小（2026-11-12）：只露卡顶 ~40px 名条——悬停临时展开
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
            // 对方手牌扇点击（2026-11-03 Information 定案）：点任意卡开「对方手牌」查阅窗
            //（全卡背+被展示卡正面含状态）；己方扇不挂 OnCardClick——卡体点击留给出牌流
            _oppFan?.SetInteractions(new FanInteractions { OnCardClick = _ => OpenInformation(Zone.Hand, P2) });

            // 旧版战场 UI 绑定复活（2026-10-13 补回未 3D 化部分——信息条/中栏/战报/overlay 弹层；
            // 重绑冲突已在函数内剔除，board 六行等 3D 化部分缺失=null 静默）
            BindLegacyPrefabNodes();

            // 战斗日志开关（2026-10-13 基础教学：日志改喂 log-list——btn-log 切换高度 0↔700。
            // 2026-10-18 拆分：订阅白名单/行渲染/开关动画收进 BattleLogController，本屏只 Bind+转发）
            if (_log == null) _log = new BattleLogController(p => p == P1); // 跨 Mount 存留（_logArmed 跨局口径与旧字段一致）
            _log.Bind(FindScroll("log-list"), FindOptional("log-list"));
            _log.BindSeed(FindOptional("log-list")); // 种子行（lbl-seed/btn-copy-seed——2026-11-11 日志复现链）
            BindBattleButton("btn-log", "对局日志", _log.Toggle);

            // 不入连锁（2026-10-15）：守卫响应窗口让过口——2026-10-17 短暂挪入 Region 后挪回根级
            //（战斗拒绝连锁：守卫窗口 Region 关也要能点——点击=让过，等价右键/ESC），平时隐藏。
            // 网络局（2026-10-21）：让过=PassPriority intent（屏侧包装分发——本地/网络同键不同义）
            _decision.Bind(BindBattleButton("btn-notchain", "不连锁", OnNotChainClick)); // 三义按钮归决策中枢（2026-10-18 B3）

            // Region 区域面板骨架（prefab 用户节点：点棋盘墓地/除外/发动区/卡组/临时元素格打开——
            // 2026-10-18 拆分：骨架/开关/列卡池化收进 BattleRegionPanel，屏侧只留守卫转发）
            _regionPanelCtl = new BattleRegionPanel(() => Core, () => P1);
            _regionPanelCtl.Build(FindOptional("Select")); // 2026-11-03 prefab 改版：Region 节点更名 Select
            _infoCtl = new BattleInformationPanel(() => Core, LandsOfOwnerIdx); // 区域卡查阅窗（Information 节点——同日改版）
            _infoCtl.Build(FindOptional("Information"));

            UiKit.Updater.Attach(Root, () =>
            {
                FlushRefreshQueues(); // 合帧冲刷（2026-10-18 性能）：事件订阅只置脏——连锁结算同帧多事件合并为一次对账
                _selfFan?.Tick();
                _oppFan?.Tick();
                _log.Tick();         // 日志面板高度动画（BattleLogController·2026-10-18 拆分）
                _toast.Tick();      // error-tip 滑入/到时收（BattleToastHub·2026-10-18 拆分）
                _director?.Tick();  // 步进回放看门狗（2026-10-19：超时无进展强制收口）
                _netSource?.Tick(); // 联机帧生产者（二期 C：空闲期放行积压行+落地暂存快照）
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
                    : BattleEntry.Mode == BattleMode.Network ? (BattleEntry.NetSpectator ? "联机 · 观战" : "联机 · P2P")
                    : BattleEntry.Mode == BattleMode.Replay ? "复盘"
                    : "本地 · AI";

            // 战场 3D 摆位 + 装台（2026-10-13 战场重做；2026-10-18 网络簇整删后仅本地直连）
            _field = new BattleFieldView();
            _field.HeroLifeShownChanged = OnHeroLifeShownChanged; // 角色HP读数节拍（2026-10-20）：命中拍释放时同拍重写 2D 生命标签
            // 战斗编舞（2026-10-13）：3D 寻路箭头选攻击目标 + 战斗演出（走位/受击/死亡漩涡）；
            // 事件订阅须先于本屏刷新订阅（宣言即 hold 位姿），OnEnter 建立即满足。
            // 网络局不订引擎事件（2026-10-21）：房主侧引擎事件来自服务器房（全信息），订阅会触发
            // 走位/受击演出读本地 Core（null）——网络局表现走快照对账+轻事件，编舞器不参与。
            // 复盘局同网络局不订事件（2026-11-11 演出统一 S2）：无引擎可订，战斗演出走 CombatShot 回放器。
            bool replayMode = BattleEntry.Mode == BattleMode.Replay;
            bool netMode = BattleEntry.Mode == BattleMode.Network && BattleEntry.Client != null;
            if (netMode || replayMode) NetCellPins.ClearAll(); // 跨局钉格清（RuntimeId 每局重起，防串号）
            _choreo = new CombatChoreographer(_field, () => _view, RefreshField, listenEvents: !netMode && !replayMode, selfPlayer: () => P1);
            _choreo.ReplayDriven = true; // 战斗演出改由步进回放导演驱动（2026-10-19 严格结算序：攻击步到位才开演）
            _cellPicker = new BattleCellPicker();
            BattleStageDirector.MountBoardAndDawnAsync().Forget(); // 转场③④：装载棋盘（分区表注入）→ 日出
            // 棋盘就绪后首刷（英雄柱/初始摆位）：staged 开局局挪到段五起步——段一~四无 Core 恒不就绪，
            // 45s deadline 会在选卡组期空转（2026-10-19 掷币演出挪战场）。网络局无 Core（首刷=首快照）；
            // 复盘局首刷=剧本帧 #0（StartReplayFan 等棋盘就绪后 Load）
            if (!StagedOpeningActive() && !netMode && !replayMode)
                RefreshFieldWhenBoardReady().Forget();

            _regionSelector?.ForceIdle(); // 跨局残留清算（2026-10-17）：上局退出时在途选择孤悬的 IsOpen/busy 在此钉回
            if (replayMode)
                StartReplayFan();
            else if (netMode)
                StartNetFan();
            else
            {
                // 本地局舞台（二期 A·LiveData）：帧体视图/演出/行/战斗全走播放器同一协议（回放同构；
                // 战斗=编舞器捕获开演保持本地精度）。先于 StartLocalFan 挂台——staged 开局段五才开局也不漏帧。
                _director.SetLiveSink(new LiveStageSink(this));
                StartLocalFan();
            }
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
            _localSource?.Dispose(); // 帧生产者行录制退订（2026-11-11 S2——MatchLogService.Appended）
            _projector?.Dispose(); // 投影订阅退订（B2 同上口径）
            //（结业镜像局会话标记 2026-10-19 随三课制整删——此处原 ClearAssessment 调用退役）
            TutorialGuide.End();            // 引导闸卸载防跨局残留

            _decision.Shutdown(); // 守卫/响应窗收口协议（顺序依据见 BattleDecisionHub——2026-10-18 B3）
            _tutorial?.CloseIntroPanel(); // 介绍段配音卡片防跨屏残留（2026-10-17）
            CloseTipPrompt(); // TipPanel 提示弹窗防跨屏残留（2026-10-17）
            _unitButtons.Dispose(); // 单位动作 3D 按钮组防跨屏残留（2026-10-26 替弹窗）
            BattleRevealDirector.End(); // 语音联动显隐全恢复防跨屏残留（2026-10-17）
            //（教学守卫应答器卸载 2026-11-02 随教学三连战改版整删——_tutorialResponder 链退役）
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

    }
}
