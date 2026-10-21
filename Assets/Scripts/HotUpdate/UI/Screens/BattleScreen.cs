using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
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
        private Button _btnGrave, _btnConcede;
        // 英雄区/临时元素（2026-10-16 技能格撤销定案）：技能格退役——技能发动=角色格【发动】键
        //（OnClickHeroZone 链路）、技能介绍=己方【发动】键悬停（对手无键→对手角色格悬停兜底）、角色生命/弹药=
        // 角色格悬停与格前三键、黑白钱包=地牌行黑白元素格（LandCellElementOrder）。
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
                (hitName, btnKind) => _hoverProv.Handle(hitName, btnKind),
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
                () => _log != null && _log.IsHeld); // 日志缓冲探针（2026-10-20「UI 真相源=日志」）：HP 读数按日志可见性即时/随步对账

            // 悬浮完整卡面（ACard 预览）2026-10-15 退役：prefab 节点已删，场上卡详情=hover-info 四段文本

            // 双方手牌扇区（配置承自原型屏：底部己方全正面拱形弧、顶部对手缩小卡背扇）
            // 挂载层= prefab 节点（2026-10-14 晚定案：self/opp-fan-layer 烘焙进 BattleUI.prefab，
            // 层级序=用户可调；代码只查找不补建，缺层=对应扇不渲染）
            var selfLayer = FanLayer("self-fan-layer");
            if (selfLayer != null)
                _selfFan = new HandFanView(selfLayer, new HandFanView.Config
                {
                    Center = new Vector2(0f, -375f), // 卡心离屏底 ~165px（悬停抬起后不越界）
                });
            var oppLayer = FanLayer("opp-fan-layer");
            if (oppLayer != null)
                _oppFan = new HandFanView(oppLayer, new HandFanView.Config
                {
                    TopSide = true,
                    Center = new Vector2(0f, 375f),
                    BaseScale = 0.72f,
                    MaxFanWidth = 830f,
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
            //（战斗拒绝连锁：守卫窗口 Region 关也要能点——点击=让过，等价右键/ESC），平时隐藏
            _decision.Bind(BindBattleButton("btn-notchain", "不连锁", _decision.OnNotChainClick)); // 三义按钮归决策中枢（2026-10-18 B3）

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
            // 守卫横幅/教学步骤/终局）无节点=回落 Console 日志。绑定保留 null 安全（过渡 prefab 兼容）
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
            if (UiKit.FindDeep(Root, "btn-grave-play") != null)
                _btnGrave = BindButton("btn-grave-play", () => OnGraveyardPlay().Forget());
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


            if (_lblMode != null)
                _lblMode.text = BattleEntry.Mode == BattleMode.Tutorial ? $"教学 · {BattleEntry.TutorialId}"
                    : BattleEntry.Mode == BattleMode.Challenge ? $"挑战 · 难度{BattleEntry.ChallengeDifficulty}"
                    : "本地 · AI";

            // 战场 3D 摆位 + 装台（2026-10-13 战场重做；2026-10-18 网络簇整删后仅本地直连）
            _field = new BattleFieldView();
            _field.HeroLifeShownChanged = OnHeroLifeShownChanged; // 角色HP读数节拍（2026-10-20）：命中拍释放时同拍重写 2D 生命标签
            // 战斗编舞（2026-10-13）：3D 寻路箭头选攻击目标 + 战斗演出（走位/受击/死亡漩涡吸走）；
            // 事件订阅须先于本屏刷新订阅（宣言即 hold 位姿），OnEnter 建立即满足
            _choreo = new CombatChoreographer(_field, () => _view, RefreshField, listenEvents: true, selfPlayer: () => P1);
            _choreo.ReplayDriven = true; // 战斗演出改由步进回放导演驱动（2026-10-19 严格结算序：攻击步到位才开演）
            _cellPicker = new BattleCellPicker();
            BattleStageDirector.MountBoardAndDawnAsync().Forget(); // 转场③④：装载棋盘（分区表注入）→ 日出
            // 棋盘就绪后首刷（英雄柱/初始摆位）：staged 开局局挪到段五起步——段一~四无 Core 恒不就绪，
            // 45s deadline 会在选卡组期空转（2026-10-19 掷币演出挪战场）
            if (!StagedOpeningActive())
                RefreshFieldWhenBoardReady().Forget();

            _regionSelector?.ForceIdle(); // 跨局残留清算（2026-10-17）：上局退出时在途选择孤悬的 IsOpen/busy 在此钉回
            StartLocalFan();
        }

        public override void OnExit()
        {
            // 静态订阅先行退订+toast 双层清空（2026-10-17 僵尸订阅修复）：必须排在一切可能抛错的
            // 清理（守卫窗续跑/Region 收口/箭头回收）之前——OnExit 中途异常截断会让静态事件
            // OnStepAdvanced 摄着本实例跨局复活（残留 _toastOverlay 每步重渲+访问已毁 UI）。
            _tutorial?.UnhookBanner(); // 静态引导订阅退订（BattleTutorialDirector·2026-10-18 拆分）
            _toast.ClearLayers(); // toast 双层清空（2026-10-17 僵尸订阅口径——2026-10-18 收进 BattleToastHub）
            _running = false;
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
            _regionSelector = null;
            if (_decision != null && ResponseWindowService.HumanResponder == _decision.ShowResponseAsync)
                ResponseWindowService.HumanResponder = null;
        }

        // ======================================== 本地模式 ========================================
        //（2026-10-15 彻底清除）旧一代本地启动 StartLocal 已删——零调用者的迁移残留，其订阅表与
        // 现役 StartLocalFan 漂移两次咬人（立牌滞留/入场演出失效）。本地局唯一入口=StartLocalFan；
        // 随迁已删：AppendLocal/OnGameOver（仅旧表引用）。RefreshLocal 仍被网络等路径共用故保留。

        // ======================================== 本地对局（扇形手牌过渡面，2026-10-03 接入） ========================================

        /// <summary>本局是否五段式开局（2026-10-19 掷币演出挪战场）：BattleEntry.StagedOpening 限定
        /// 普通对战（LocalAI）与教学三变体（Tutorial·lunzhuan，均主菜单注入）——教学一二/挑战/
        /// 测试镜像入口恒 false，立即启动路径零变化。</summary>
        private bool StagedOpeningActive()
            => BattleEntry.StagedOpening
               && (BattleEntry.Mode == BattleMode.LocalAI
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
                // 五段式开局（2026-10-19 掷币演出挪战场）：先战场演出（地块就位→角色就位→掷硬币→
                // 战场选卡组），引擎启动整体推迟到段五——BattleOpeningDirector 段末回调
                // EngageStagedBattle 交班。段一~四无 Core/无事件订阅可撞；段二预置柱由段五首次
                // 对账按名收养；AI 先手 kickoff 随 Engage 尾段（防硬币对方先手局冻结的同款洞）。
                WireLocalUi();
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
        /// 全随机选择回落无头兜底分支丢硬币结果的潜在洞顺带堵上——兜底分支保留给测试路径）。</summary>
        private void EngageStagedBattle(List<CardData> myDeck, string mySkill, List<CardData> aiDeck,
            string aiSkill, bool? playerFirst)
        {
            _opening?.Dispose();
            _opening = null;
            RefreshFieldWhenBoardReady().Forget();
            EngageLocalBattle(null, null, null, null, null, myDeck, mySkill, aiDeck, aiSkill,
                playerFirst, forceMatch: true);
        }

        /// <summary>本地对局启动后半段（原 StartLocalFan 尾段整体抽出·2026-10-19 五段式开局复用——
        /// 顺序与时序红线零变化）：_log.Wire→WireEarly→模式分支（StartNewGame/教学装配）→Wire→
        /// 扇/Region 接线→补偿刷→AI 先手 kickoff。</summary>
        private void EngageLocalBattle(List<CardData> mirrorDeck, string mirrorSkill,
            List<CardData> challengeDeck, string challengeSkill, TutorialScenario testScenario,
            List<CardData> matchDeck = null, string matchSkill = null, List<CardData> aiDeck = null,
            string aiSkill = null, bool? playerFirst = null, bool forceMatch = false)
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
                if (!_tutorial.StartGame(_ctrl, matchDeck, aiDeck, playerFirst, matchSkill, aiSkill))
                {
                    BattleEntry.Mode = BattleMode.LocalAI; // 回落局不计教学进度（胜负都不落档）
                    _ctrl.StartNewGame(matchDeck, aiDeck,
                        MakePacedAi(aiDeck != null ? AiStrategy.AutoMatch(aiDeck) : null),
                        mySkillCardId: matchSkill, aiSkillCardId: aiSkill,
                        playerFirst: playerFirst, secondArmorBonus: 5);
                }
            }
            else if (matchDeck != null || forceMatch)
                // 对局准备流程（2026-10-19 挪入战场五段式开局：掷币+选卡组结果段五注入，旧
                // PreMatchScreen 当日整删）：AI 策略按其卡组自适配（卡组缺=缺省策略）；硬币后手
                // 角色 +5 护甲（StartNewGame 内注入）。forceMatch=staged 局恒走本分支（null 卡组
                // 随机兜底——掷币结果不因全随机选择漏进无头兜底分支）
                _ctrl.StartNewGame(matchDeck, aiDeck,
                    MakePacedAi(aiDeck != null ? AiStrategy.AutoMatch(aiDeck) : null),
                    mySkillCardId: matchSkill, aiSkillCardId: aiSkill,
                    playerFirst: playerFirst, secondArmorBonus: 5);
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
                var challAiDeck = BattleController.RandomDeckFrom(CardCatalog.LoadAll());
                _ctrl.StartNewGame(challengeDeck, challAiDeck, MakePacedAi(AiStrategy.AutoMatch(challAiDeck)),
                    aiLandCapBonus: difficulty, aiExtraOpeningDraws: difficulty,
                    mySkillCardId: challengeSkill,
                    aiSkillCardId: HeroSkillSystem.AutoPickSkillCard(challAiDeck));
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
            // 手牌扇交互接线（2026-10-14）：悬停显「放置/发动」按钮+可发动流光（对手扇不注入=无按钮）
            _selfFan?.SetInteractions(new FanInteractions
            {
                CanUse = FanCanUse,
                CanPlaceLand = FanCanPlaceLand,
                OnUse = OnFanUse,
                OnPlaceLand = OnFanPlaceLand,
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

            _selfFan?.SetCards(self);
            _oppFan?.SetCards(opp);
            EvaluateFanPlayability(); // 可发动流光随对账刷新（费用/上限/阶段变化即时反映）
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
            BattleHudRow.SetPhase(PhaseTileText(d)); // 阶段显示格（中线 x0 平贴文字）同频喂文
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
            BattleHudRow.SetPhase(PhaseTileText(d)); // 阶段显示格（中线 x0 平贴文字）同频喂文
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

            if (_btnGrave != null) _btnGrave.interactable = myMain && d.Self.GraveyardCount > 0;
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
            // 法术（2026-10-15 改版）：modeIndex=施放时效果抉择——模式数=效果数（1~3 主动）
            if (data.Supertype == Cardtype.Spell)
                return Math.Max(1, data.Effects?.Count ?? 1);
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
        private async UniTaskVoid PromptModeThenPlay(BattleCardView card, bool fromGrave)
        {
            var data = DataOf(card);
            bool isSpell = data?.Supertype == Cardtype.Spell;
            var labels = isSpell ? SpellEffectLabels(data) : FindChoiceLabels(data);
            int modeCount = isSpell
                ? Math.Max(2, data.Effects?.Count ?? 1)
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

            // 法术：手牌路径手选发动位→声明流（2026-10-20：必须先到发动区）；墓地路径自动落位直入声明流；
            // 永久物抉择照旧直出选格
            if (isSpell)
            {
                if (fromGrave) SpellDeclarationFlow(card, idx, fromGrave: true).Forget();
                else BeginSpellCastCellPicking(card, card.CoreCard, idx);
            }
            else
                PlayCardFinal(card, null, idx, fromGrave);
        }

        /// <summary>法术各效果的显示名（抉择窗选项；DisplayName 空=「作用 N」）。</summary>
        private static List<string> SpellEffectLabels(CardData data)
        {
            var labels = new List<string>();
            if (data?.Effects == null) return labels;
            int i = 1;
            foreach (var eff in data.Effects)
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
        /// 本口仍是终态单点（法术带 targets、生物/结界 targets=null 照旧）。</summary>
        private void PlayCardFinal(BattleCardView card, List<Entity> targets, int modeIndex, bool fromGrave)
        {
            var core = Core;
            if (core == null || P1 == null) return;

            // 2026-10-16 定案（法术目标口径已被 2026-10-20 改案取代）：指向性法术目标统一在
            // 声明期收集（SpellDeclarationFlow→CollectSpellDeclarationTargetsAsync——可取消=撤销发动）；
            // defer 原子（效果处理时选）结算期反问（必须选）；牌库等隐藏区/区域自结算类效果
            // 不弹选直接结算（引擎 None 模式早退单源）。
            bool ok = fromGrave
                ? GameActions.PlayCardFromGraveyard(core, P1, card.CoreCard, targets, modeIndex)
                : GameActions.PlayCard(core, P1, card.CoreCard, targets, Zone.Hand, modeIndex);
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
                _toast.Show(fromGrave ? "无法使用（配额已用或不可支付）" : "无法打出（费用/条件不满足）");
            }
            RefreshLocal();
        }

        // LandFlow（手牌直点放地的旧选色弹窗链）已随 2026-10-16 弹窗退役整删：放地现役入口=
        // OnFanPlaceLand 落格箭头（同格多候选色→PickLandColorAsync 走 Region 卡牌列表）。

        // ---- 法术发动声明流（2026-10-20 改案·用户定案：手选发动位→发动区就位→选目标（可取消）→连锁检测→扣费→效果） ----

        /// <summary>法术出牌选发动位（2026-10-20 用户定案：手牌使用规范=必须先到发动区——玩家手选
        /// 发动位置再选目标）：候选=己方单位区空格（与永久物出牌选格同款箭头交互、同源占用口径
        /// ——含施放占格卡）。确认→登记选格单（PlayCardFinal 声明成功即 PinCard——PlaceCasting
        /// 钉优先落同格）→SpellDeclarationFlow（贴片预览=所选格+声明期目标收集，可取消）；
        /// 取消恢复手牌。教学闸预检前置（防选完位置才被拒）。</summary>
        private void BeginSpellCastCellPicking(BattleCardView view, Card card, int modeIndex)
        {
            var board = _ctrl?.Board;
            if (board == null) return;

            // 教学闸预检（同 SpellDeclarationFlow 口径——选位前先验）
            if (RuleHooks.TutorialPlayGate?.Invoke(P1, card) is string gateReject)
            {
                _toast.Show($"教学引导：{gateReject}");
                return;
            }

            var options = new List<BattleCellPicker.CellOption>();
            foreach (var cell in BoardLayout.UnitCells(0))
                if (board.CardAt(cell.x, cell.z) == null)
                    options.Add(new BattleCellPicker.CellOption { X = cell.x, Z = cell.z, ColorIndex = -1 });
            if (options.Count == 0) { _toast.Show("单位区已满——无法发动（法术须先到发动区占位）"); return; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            _cellPicker.Begin(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options, opt =>
            {
                RestoreFan();
                _pendingPins[card.RuntimeId] = (opt.X, opt.Z);
                Debug.Log($"[HandPlay] 法术发动位：{view.Name} → ({opt.X},{opt.Z})（声明成功即钉）");
                SpellDeclarationFlow(view, modeIndex, fromGrave: false, pickedX: opt.X, pickedZ: opt.Z).Forget();
            }, RestoreFan);
        }

        /// <summary>法术发动声明流：发动位/模式/费用已过 → 预览态（贴片预览——手牌路径=玩家所选格，
        /// 墓地路径自动 first-free + 手牌扇藏卡=「卡已放发动区」观感）→ 声明期目标收集（逐原子问选，
        /// 可取消）→ 取消=撤销发动（PlayCard 未调：使用宣言触发等声明副作用零，选格单一并
        /// 作废，回退绝对干净）→ 确认=预览贴片无缝转正 + PlayCardFinal（真声明：进发动区+上栈+PinCard
        /// 钉所选格）→ SettleResponseWindow（连锁检测=引擎现状；无连锁结算期扣费+粒子+效果表现）。
        /// 无交互原子（全取/随机/全 defer）：收集器同步完成空表——预览同帧转正零可见闪烁
        ///（UniTask 同步前缀保证）。</summary>
        private async UniTaskVoid SpellDeclarationFlow(BattleCardView view, int modeIndex, bool fromGrave,
            int pickedX = -1, int pickedZ = -1)
        {
            var core = Core;
            var card = view?.CoreCard;
            if (core == null || P1 == null || card == null) return;

            // 教学闸预检（声明期先验——防选完目标才被引擎拒；PlayCard 内终审保留同口径）
            if (!fromGrave && RuleHooks.TutorialPlayGate?.Invoke(P1, card) is string gateReject)
            {
                _pendingPins.Remove(card.RuntimeId); // 选位单作废（手选发动位路径）
                _toast.Show($"教学引导：{gateReject}");
                return;
            }

            // 落格：手牌路径=玩家所选格（选格单已登记）；墓地路径自动 first-free 镜像
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
                PlayCardFinal(view, targets, modeIndex, fromGrave);
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

        /// <summary>扇区卡 → 引擎手牌卡（RuntimeId 对账；不在手=已过态返回 null）。</summary>
        private Card FanCardOf(HandFanCard fan)
        {
            if (fan == null || Core == null || P1 == null) return null;
            foreach (var c in Core.ZoneManager.GetCards(P1, Zone.Hand))
                if (c != null && c.RuntimeId == fan.RuntimeId) return c;
            return null;
        }

        /// <summary>GateFanMyMain 静默版（按钮/流光逐帧评估用——不弹 toast）。</summary>
        private bool FanGateQuiet()
        {
            if (_gameEnded || _ctrl == null || Core == null || P1 == null) return false;
            var phase = Core.TurnEngine?.CurrentPhase;
            return _ctrl.TurnPlayer == P1 && phase != null && phase.Phase == PhaseType.Main;
        }

        /// <summary>「放置」按钮条件：可作地牌（正式三型/非衍生物/非临时/非锁定）+ 有可锁产色
        /// + 地牌池有余位（各自上限口径：PooledCards.Count &lt; GetLandCap）。</summary>
        private bool FanCanPlaceLand(HandFanCard fan)
        {
            if (!FanGateQuiet()) return false;
            var card = FanCardOf(fan);
            if (card == null || Core?.ElementPool == null) return false;
            return ElementPoolSystem.CanServeAsLand(card)
                && ElementPoolSystem.GetLandColorChoices(card).Count > 0
                && Core.ElementPool.CanAddLand(P1);
        }

        /// <summary>「发动」按钮条件：任一模式 CanAffordCast（含声明承诺 pending——代价补偿
        /// 黑/白不预计算垫本卡，2026-10-16 恢复旧序）；永久物另需单位区空格（满场不可出）。</summary>
        private bool FanCanUse(HandFanCard fan)
            => FanGateQuiet() && CardCastableNow(FanCardOf(fan));

        /// <summary>卡当前可发动（无主阶段闸——闸由调用方外置）；法术=可付即可，永久物另需空格。</summary>
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
                return Core.ZoneManager.GetCards(P1, Zone.Battlefield).Count < BoardLayout.UnitCells(0).Count;
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
            if (!GateFanMyMain()) return;
            var card = FanCardOf(fan);
            if (card == null) return;
            if (Core?.ElementPool == null || !ElementPoolSystem.CanServeAsLand(card) || !Core.ElementPool.CanAddLand(P1))
                return;

            var choices = ElementPoolSystem.GetLandColorChoices(card);
            if (choices.Count == 0) { _toast.Show("该卡无费用颜色可锁定（0费/纯黑白不可作地牌）"); return; }

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

        /// <summary>「发动」点击（2026-10-14 定案；2026-10-20 手选发动位改案）：
        /// 法术=模式窗（多作用）→费用/教学预检→**箭头手选发动区落格**（手牌使用规范=必须先到
        /// 发动区）→SpellDeclarationFlow（贴片预览+声明期选目标，可取消撤销）；
        /// 永久物（生物/结界）=模式窗（多模式）→箭头选己方单位区空格（所选格=最终落位，
        /// 入场后补钉）→确认出牌。取消恢复手牌。</summary>
        private void OnFanUse(HandFanCard fan)
        {
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
                if (GetModeCount(view) >= 2) PromptModeThenPlay(view, fromGrave: false).Forget();
                else
                {
                    // 费用预检（与模式窗同口径——防选完位置目标才被引擎拒）
                    if (Core == null || !Core.ElementPool.CanPayCostWithAutoTap(
                            GameActions.GetCardCost(card, 0), P1))
                    {
                        _toast.Show("费用不足——该作用不可发动");
                        return;
                    }
                    BeginSpellCastCellPicking(view, card, 0);
                }
                return;
            }
            if (CostDerivationService.GetModeCount(data) >= 2)
            {
                PromptModeThenPickCell(view, card).Forget();
                return;
            }
            BeginUseCellPicking(view, card, 0);
        }

        /// <summary>模式窗变体（永久物出牌）：选完模式进入选格而非直接出牌——文案口径同
        /// PromptModeThenPlay，验费用 CanAffordCast 全口径（含声明承诺；代价补偿不预计算——2026-10-16 恢复旧序）。
        /// 2026-10-16 弹窗退役：选项=Region 面板卡牌列表。</summary>
        private async UniTaskVoid PromptModeThenPickCell(BattleCardView view, Card card)
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
            if (Core == null || !GameActions.CanAffordCast(Core, P1, card, idx))
            {
                _toast.Show("费用不足——该分支不可发动");
                return;
            }
            BeginUseCellPicking(view, card, idx);
        }

        /// <summary>永久物出牌选格：候选=己方单位区空格（占用=BoardState 现值，含施放占格卡）；
        /// 箭头自角色格拉出（原发动格锚随发动格拆除退役，2026-10-15）。
        /// 确认→登记选格单（PlayCardFinal 声明成功即转 BoardState 钉——施放类型图标与入场转正同钉单源）
        /// →PlayCardFinal（需目标的卡在链内弹目标窗）。取消恢复手牌。</summary>
        private void BeginUseCellPicking(BattleCardView view, Card card, int modeIndex)
        {
            var board = _ctrl?.Board;
            if (board == null) return;

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
                Debug.Log($"[HandPlay] 永久物出牌选格：{view.Name} → ({opt.X},{opt.Z})（声明成功即钉）");
                PlayCardFinal(view, null, modeIndex, fromGrave: false);
            }, RestoreFan);
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
            if (target is Card tc && (tc.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) > 0
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
        /// UI 命中让路且不关弹窗（销毁按钮会吞掉 onClick，原口径）；单位模型 collider 已拆，
        /// 打格反查是既定通路。2026-10-15 定案：落格=展示信息，攻击/守卫/发动拆到格前三键
        /// （BattleHex.prefab 手搭件，BattleTileButtons 高亮），三键命中归并所属格路由。
        /// 2026-10-18 安卓滑动输入：鼠标/触摸统一走 BattlePointer（触摸点击=抬起裁决、
        /// 落点=手指原始点位；PC 口径零变化）。</summary>
        private void PollFieldPointer()
        {
            BattlePointer.Sample(); // 鼠标/触摸统一采样（同帧幂等）——本屏与选择循环共源
            _decision?.PollProbe(); // 取证心跳（2026-10-17 口径，2026-10-18 B3 收口进决策中枢；快照供数在装配处）

            if (_opening != null && _opening.Active)
            {
                // 五段式开场闸（2026-10-19 掷币演出挪战场）：段一~四无 Core，游戏点击分支全拦——
                // 指针交开场导演（deckpick- 选卡组分发/其余点击=演出段快进），悬停层清空防残文案。
                _hoverProv.Clear();
                _opening.PollPointer();
                return;
            }

            ReconcileTileButtons(); // 格前三键每帧对账先行（让路路径同样收键——编舞/选格中不可操作）
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
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
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
            // 格前三键命中 → 归并所属格（按钮=tile 子件）：hover 按格走（落格=展示信息），
            // btnKind 记动作供点击分流；守卫窗口吃所有点击（三键=格的延伸）
            string btnKind = null;
            if (hitName == "Attack" || hitName == "Defense" || hitName == "Activate")
            {
                for (var p = hitCollider.transform.parent; p != null; p = p.parent)
                    if (p.name.StartsWith("tile-", System.StringComparison.Ordinal))
                    {
                        btnKind = hitName;
                        hitName = p.name;
                        break;
                    }
                if (btnKind == null) hitName = ""; // 键不在格上（异常挂载）→ 视作无命中
            }
            if (_decision != null && _decision.GuardWindowOpen) { _decision.HandlePointer(hitName, cam, btnKind); return; } // 守卫窗口：点守卫生物/角色/攻击者
            _hoverProv.Handle(hitName, btnKind);
            if (BattlePointer.ConfirmPressed) HandleFieldClick(hitName, btnKind);
        }

        // ---- 悬停路由 ----

        // ---- 悬停信息文案 ----

        /// <summary>阶段显示格文字（【己方主要阶段】式——BattleHudRow.SetPhase 喂文，8 字 2.6 号贴格宽）。</summary>
        private static string PhaseTileText(BattleViewData d)
            => $"【{(d.MyTurn ? "己方" : "对手")}{BattleView.PhaseZh(d.Phase)}阶段】";

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

        /// <summary>左键点击路由（btnKind=格前三键命中归并前的动作名——Attack/Defense/Activate；
        /// null=格体/其他命中）。三键=所属单位/角色格操作入口：Attack→攻击目标选择（单位=ShowUnitActions
        /// 全门禁、角色=OnClickSelfHero 弹药门+箭头选目标），Activate→单位主动效果/英雄技能；
        /// Activate→单位主动效果；Defense 由守卫窗口独占消费（点键=点格选守卫者），窗口未开=引导提示。
        /// 格体命中=展示信息语义：只收弹窗不开操作（2026-10-15 定案，单位操作全走格前三键）。</summary>
        private void HandleFieldClick(string hitName, string btnKind)
        {
            // 格前三键（2026-10-15 定案：单位本体退出射线，攻击/守卫/发动拆到格前；
            // 2026-10-16 角色格同款三键：攻击=弹药门+箭头选目标，发动=英雄技能）
            if (btnKind != null)
            {
                int bx, bz;
                bool hasTile = TryParseTile(hitName, out bx, out bz);
                // 角色格三键：Attack→OnClickSelfHero（弹药/网络门禁沿用），Activate→OnClickHeroZone；
                // Defense 由守卫窗口独占消费（点键=点格选角色守卫），窗口未开=引导提示
                if (hasTile && bx == BoardLayout.P1CharX && bz == BoardLayout.P1CharZ)
                {
                    CloseRegion();
                    if (btnKind == "Attack") OnClickSelfHero();
                    else if (btnKind == "Activate") OnClickHeroZone();
                    else _toast.Show("守卫在对手攻击宣言后的守卫窗口内可用（白键=角色可拦截）");
                    return;
                }
                var btnUnit = hasTile
                    ? (_view.SelfUnits ?? new List<BattleCardView>())
                        .FirstOrDefault(c => c != null && c.X == bx && c.Z == bz && !c.IsDead)
                    : null;
                if (btnKind == "Attack")
                {
                    CloseRegion();
                    if (btnUnit == null) return; // 键下单位已离场（对账竞态）
                    ShowUnitActions(btnUnit, bx, bz); // 门禁/教学/横置/攻0 拒绝 toast 全沿用
                    return;
                }
                if (btnKind == "Activate")
                {
                    CloseRegion();
                    if (btnUnit != null) OnClickUnitEffectButton(btnUnit).Forget();
                    return;
                }
                // Defense：守卫窗口开启时点击被 HandleGuardPointer 消费（点键=点格）；
                // 窗口未开=守卫是响应动作，引导即可
                CloseRegion();
                _toast.Show("守卫在对手攻击宣言后的守卫窗口内可用（白键=该生物可拦截）");
                return;
            }

            // 功能地块（BattleHudRow 中线状态行：结束回合格可点；两时间/连锁速度格纯展示——
            // 未认领名走 TryParseTile 失败分支收弹窗，无需另接点击）
            if (hitName == "hud-tile-end-turn")
            {
                Debug.Log("[HUD地块] 点击：结束回合");
                CloseRegion();
                if (IsTargetSelecting()) return; // Region 抉择/反问在身：先完成选择再推进（非模态须自防重入）
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
                HandleZoneCellClick(role, BoardLayout.OwnerOf(x, z));
                return;
            }

            // 单位格体=展示信息（悬停已出四段卡信息）：只收弹窗，操作走格前三键
            var unit = (_view.SelfUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
            if (unit != null) { CloseRegion(); return; }

            var land = (_view.SelfLands ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z);
            if (land != null) { OnClickMyLand(land); return; }
            CloseRegion();
        }

        /// <summary>特殊格点击：墓地/除外/发动区/卡组/临时元素→Region 面板；角色格→技能详情+操作组。
        /// 2026-10-17 点击门槛（用户定案）：墓地/除外须有卡才开列表面（无卡=点击无反馈，信息走悬停）；
        /// 卡组是隐藏区，只有存在被展示的卡（RevealRules 信息轴）才开——列表只列被展示的卡。</summary>
        private void HandleZoneCellClick(CellRole role, int ownerIdx)
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
                    if (ExposedDeckCards(owner).Count > 0) OpenRegion(RegionKind.Deck, owner);
                    break;
                // CellRole.Skill 已随技能格撤销（2026-10-16）退役：技能发动入口=角色格【发动】键
                //（HandleFieldClick btnKind 路由——OnClickHeroZone 链路），介绍=角色格悬停。
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

        // ---- 格前三键对账（2026-10-15 定案：攻击/守卫/发动拆到格前，高亮=可用） ----

        private readonly List<BattleTileButtons.Entry> _tileBtnBuf = new List<BattleTileButtons.Entry>();

        // 三键效果缓存（2026-10-18 性能）：UnitActivatedEffects/HeroHasActivatableSkill 每次跑完整效果
        // 定义转换（ConvertAll+LINQ），改事件级重算、帧间复用；SelfUnits 每次 RefreshField 重建，
        // 引用比对兜底 + RefreshField 尾置脏（StackEmpty 订阅兜住变形/临摹等改写效果的结算路径）
        private readonly Dictionary<BattleCardView, bool> _unitActivateFlags = new Dictionary<BattleCardView, bool>();
        private readonly Dictionary<BattleCardView, bool> _unitPendingFlags = new Dictionary<BattleCardView, bool>();
        private bool _activateFlagsDirty = true;
        private List<BattleCardView> _activateFlagsFor;
        private bool _heroActivateFlag;

        /// <summary>三键每帧对账（幂等差量，BattleTileButtons 落盘）：编舞选目标/放格选格=全收；
        /// 守卫窗口=守卫候选亮蓝（窗口独占，攻/发键暗灰）；常态=己方单位格——攻击=生物未横置攻&gt;0
        /// 且我方主阶段、发动=有主动效果且我方主阶段、守卫=仅窗口内亮。
        /// 角色格（2026-10-16 同款三键）：攻击=有弹药且我方主阶段、发动=技能挂主动效果且我方主阶段、
        /// 守卫=窗口内且角色在候选（弹药&gt;0）。
        /// 深判定（费用/横置代价/耐久/每回合一次）留引擎裁决，高亮不预判，拒绝走点击 toast。</summary>
        private void ReconcileTileButtons()
        {
            if (_view == null || !BattleBoardGrid.Ready
                || (_choreo != null && _choreo.IsTargeting)
                || (_cellPicker != null && _cellPicker.Active))
            {
                BattleTileButtons.Apply(null);
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
            _tileBtnBuf.Clear();
            if (units != null)
            {
                foreach (var c in units)
                {
                    if (c == null || c.IsDead || c.CoreCard == null || c.X < 0 || c.Z < 0) continue;
                    var e = new BattleTileButtons.Entry { X = c.X, Z = c.Z };
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
                    _tileBtnBuf.Add(e);
                }
            }

            // 角色格三键（2026-10-16）：Attack=弹药>0、Activate=技能挂主动效果、
            // Guard=守卫窗口内且角色在候选（ReferenceEquals 匹配 SourcePlayer——引擎侧弹药门同源）
            if (P1 != null)
            {
                var hc = BoardLayout.CharacterCell(0);
                var hero = new BattleTileButtons.Entry { X = hc.x, Z = hc.z };
                if (_decision != null && _decision.GuardWindowOpen)
                    hero.Guard = _decision.GuardCandidates != null
                                 && _decision.GuardCandidates.Exists(o => o != null && ReferenceEquals(o.SourcePlayer, P1));
                else
                {
                    hero.Attack = myMain && P1.GetPower() > 0;
                    hero.Activate = myMain && _heroActivateFlag;
                }
                _tileBtnBuf.Add(hero);
            }
            BattleTileButtons.Apply(_tileBtnBuf);
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

        // ---- Region 面板守卫转发（2026-10-18 拆分——本体在 BattleRegionPanel；选择面占用期间
        //      不开关面板=窗口蒸发教训 2026-10-15，守卫留在屏侧因 _regionSelector 是决策流中枢） ----
        private void OpenRegion(RegionKind kind, Player owner)
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return; // 目标选择进行中：Region 被选择面占用
            _regionPanelCtl?.Open(kind, owner);
        }

        private void CloseRegion()
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return; // 选择进行中（弃牌等反问占用选择面）
            _regionPanelCtl?.Close();
        }

        private void RefreshRegion()
        {
            if (_regionSelector != null && _regionSelector.IsOpen) return;
            _regionPanelCtl?.Refresh();
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

        /// <summary>墓地使用（2026-10-16 弹窗退役：旧列表弹窗→Region 真卡面选牌，单选强制）。
        /// 选后多模式卡续接 PromptModeThenPlay（同 Region 择一），单模式直出。</summary>
        private async UniTaskVoid OnGraveyardPlay()
        {
            if (!GateMyMain()) return;

            var grave = Core.ZoneManager.GetCards(P1, Zone.Graveyard);
            if (grave == null || grave.Count == 0) { _toast.Show("墓地为空"); return; }
            if (_regionSelector == null) { _toast.Show("Region 面板缺失——墓地使用不可用"); return; }

            var req = new TargetSelectionRequest
            {
                Candidates = grave.Cast<Entity>().ToList(),
                MinCount = 1,
                MaxCount = 1,
                Chooser = P1,
                Title = "墓地使用（视为手牌中使用，每回合一次）",
            };
            var labels = new List<string>(grave.Count);
            foreach (var c in grave) labels.Add(CardDisplayName(c));
            var picked = await _regionSelector.SelectAsync(req, labels, 60f);
            if (picked == null || picked.Count == 0) return;

            var gc = grave[picked[0]];
            var view = new BattleCardView
            {
                CardId = gc.ID,
                CoreCard = gc,
                Runtime = SerializableRuntimeCardState.FromCard(gc),
                Name = CardDisplayName(gc),
            };
            var data = (gc as CardWrapper)?.GetData();
            if (data != null)
            {
                view.CostText = BattleView.CostTextOf(data.Cost);
                view.SupertypeText = BattleView.SupertypeZh(data.Supertype);
            }
            if (GetModeCount(view) >= 2) PromptModeThenPlay(view, fromGrave: true).Forget();
            else SpellDeclarationFlow(view, 0, fromGrave: true).Forget();
        }

        // 技能=一回合一次重复用的法术（2026-10-18 深夜修订）：与同名法术**不同速不同费**——
        // 发动速度恒 0（不吃法术槽位盈余）；免元素锚价，但多效果（抉择）技能每次发动另收 1 灰。
        // 多效果技能与法术同款施放时抉择（Region 面板 PickRegionOptionAsync——2026-10-16 弹窗
        // 退役后的唯一抉择 UI，AI 侧 TideActionType.HeroSkill 的 ModeIndex 同为效果序号）。

        private async UniTaskVoid ActivateSkillAsync(int effectIndex = 0)
        {
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
