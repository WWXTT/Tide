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
        /// <summary>挑战模式（2026-10-06）：玩家自选卡组，电脑按难度 N 获得非对称优势——
        /// 地牌槽上限 7+N（N≥7 触顶 14）、起手 6+N（InitGame 加成参数）。渲染/交互与本地 AI 局同轨。</summary>
        Challenge,
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

        /// <summary>镜像局卡组（2026-10-06 最后一课/考核局）：非空=双方同用这份卡组开局
        /// （AI 操纵玩家卡组的镜像）。一次性消费防跨局残留（StartLocalFan 开局即置空）。
        /// 注入方=TutorialCreationFlow/TutorialFlow（Mode=LocalAI + 本字段）。</summary>
        public static List<CardData> MirrorDeck;

        /// <summary>镜像局技能标记卡 ID（2026-10-07 卡牌化）：与 MirrorDeck 成对一次性消费——
        /// 双方同用（镜像口径）；null=镜像局无技能。</summary>
        public static string MirrorSkillCardId;

        /// <summary>测试开局预设（2026-10-16）：非空=镜像局改走教学注入管线（InitGame 跳起手 +
        /// TutorialScenarioSeeder 静默铺场 + StartGame 起跳回合，StartNewGame scenario 分支）。
        /// 一次性消费防跨局残留（StartLocalFan 开局即置空）。注入方=TestStartFlow。</summary>
        public static TutorialScenario Scenario;

        /// <summary>挑战模式难度（2026-10-06，1..9 单值联动）：电脑地牌槽上限 7+N（N≥7 触顶 14）、起手 6+N。
        /// 与 TutorialId 同口径：注入方设值后保持到终局（ReportBattleResult 读取落档），
        /// 每次进挑战模式都会重设，无跨局残留问题。</summary>
        public static int ChallengeDifficulty = 1;

        /// <summary>挑战模式玩家卡组（2026-10-06）：null=随机卡组（RandomDeckFrom 兜底）。
        /// 一次性消费防跨局残留（StartLocalFan 开局即置空）。</summary>
        public static List<CardData> ChallengeDeck;

        /// <summary>挑战模式玩家技能标记卡 ID（2026-10-07 卡牌化）：与 ChallengeDeck 成对
        /// 一次性消费；null=无技能。AI 侧随机卡组在 StartLocalFan 自动挑合格结界。</summary>
        public static string ChallengeSkillCardId;

        /// <summary>临时（2026-10-14 双方快攻脚本全自动对局测试，验收后移除）：true=普通本地局
        /// 双方都换成红快攻脚本 AI（P1 也交 AI，StartLocalFan 监护循环逐回合驱动）。
        /// 默认 false，本地测试行为零变化；仅 execute_code 会话内置 true。</summary>
        public static bool BothAiRushTest;
    }

    /// <summary>
    /// 对战界面（2026-10-02 战场定案 3D + 部分透明 UI 重做：旧 BattleUI.prefab 已删除；
    /// 2026-10-03 本地模式先以「双扇手牌 + 结束回合」过渡面运行——HandFanView 自原型屏移植接入，
    /// 出牌交互与原绑定/对局逻辑封存待 3D 层重接；同日过渡面预制体化：静态层级回归
    /// Assets/Art/Prefab/BattleUI.prefab（Tools/验证/对战UI/BattleUI预制体装配 幂等重建），
    /// Build=按名绑定（2026-10-07 兜底退役：缺节点 LogError 跳过，不代码补建）。
    /// 历史口径（2026-10-01 预制体化，封存代码对照用）：静态层级来自 BattleUI.prefab，Build 深度按名
    /// 绑定；战场格子/手牌槽复用烘焙节点（按序对应棋盘 x 位），战报行/弹窗运行时构建。
    /// 本地 AI（GameCore/GameActions 直连）与网络（MsgGameStateSync 快照全量渲染 +
    /// NetEventBatch 中文战报 + intent 上行 + SelectRequest 反问弹窗 + PassPriority 让行）。
    /// 战场=3D 棋盘（BattleFieldView 对账渲染）；手牌=扇区（HandFanView·ACard 实例只出自
    /// 扇区/Region 列卡/prefab 预览位——旧 2D 格子卡牌层挂载链 2026-10-14 晚拆除）。
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

        // ---- 控件引用（Build 填充） ----
        private TMP_Text _lblMode, _lblToast;
        private GameObject _toastPanel;            // lbl-toast 面板本体（自带底板 Image——显隐单口：文本空≠隐藏）
        private string _toastBase, _toastOverlay;  // toast 双层（2026-10-16 横幅合并）：底层=教学/守卫横幅，顶层=选择/响应/拒绝临时提示——顶层清空回落底层
        private TMP_Text _lblPhase, _lblStack, _lblPriority;
        private Button _btnGrave, _btnSkill, _btnPass, _btnConcede;
        private Button _btnNotChain; // 不入连锁：守卫响应窗口让过口，仅窗口期可见（2026-10-15）
        private int _notChainSerial;                                       // 不连锁倒计时代次（收口即+1 作废旧循环）
        private bool _responsePassAll;                                     // 整窗让过旗（不连锁/倒计时归零/切屏——分档询问中两档同拒）
        private const int NotChainSeconds = 10;                            // 对手回合主动效果：连锁窗口秒数（超时=拒绝连锁）
        // 英雄区/临时元素（2026-10-16 技能格撤销定案）：技能格退役——技能发动=角色格【发动】键
        //（OnClickHeroZone 链路）、技能介绍=己方【发动】键悬停（对手无键→对手角色格悬停兜底）、角色生命/弹药=
        // 角色格悬停与格前三键、黑白钱包=地牌行黑白元素格（LandCellElementOrder）。
        // 2026-10-14 晚：旧 2D 棋盘六行/手牌条字段退役（战场=3D 层、手牌=扇区）——
        // CardOverlayController 卡面挂载链随行拆除，本屏不再产生扇区外的 ACard 实例。
        private UiKit.Scroll _logList;
        private RectTransform _logLineTemplate;    // content 直属隐藏模板 lbl（战报行克隆源，样式归 prefab）
        // 旧 overlay/modal 现搭弹窗层（_overlay/_selectorLayer/_modal/_overlayCancel）已随 2026-10-16
        // 定案整删：纯选项/响应抉择统一走 Region 面板卡牌列表（PickRegionOptionAsync/RegionTargetSelector），
        // 终局提示走 toast+战报——全屏遮罩拦棋盘点选（守卫窗口卡死）的坑不再有。

        // ---- 手牌扇形过渡面（2026-10-03 本地对战接入：原型屏 HandFanScreen 移植为本屏本地呈现） ----
        private HandFanView _selfFan, _oppFan;          // 双方手牌扇区（对账键=引擎卡 RuntimeId）

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

        // ---- 双方操作计时（2026-10-14 定案·仅真人对战：基础300秒/优先权持有方掉秒/回合结束+60秒/
        //     超时对方胜；人机对局不建钟，HUD 时间格显示 ∞） ----
        private MatchClock _matchClock;
        private double _hudTimeAccum;                              // HUD 时间格刷新节流（4Hz）
        private int _clockPrevTurn = -1, _clockPrevActive = -1;    // 回合切换检测（+60 归属=刚结束回合方）

        // ---- 棋盘指针/区域面板/日志开关（2026-10-13 基础教学三问题：悬停信息、Region 列卡、btn-log） ----
        // 悬浮完整卡面退役（2026-10-15 用户删除 BattleUI.prefab 的 ACard 节点）：场上卡（生物/结界/角色技能）
        // 悬停信息改走 hover-info 四段文本（主动/自动/关键词/状态，见 CardHoverInfo）
        private RectTransform _hoverInfo;          // 悬停信息位（场上卡四段信息与区域/功能格说明共用）
        private TMP_Text _hoverInfoLabel;
        private string _hoverKey;                  // 当前悬停内容键（防逐帧重绘）
        private RectTransform _logListRect;        // log-list 高度动画目标（0↔700）
        private float _logListTarget;

        // 信息栏动态文本（FillBar 刷新）
        private TMP_Text _oppName, _oppLife, _oppDeck, _oppHand, _oppGrave, _oppFatigue, _oppBank;
        private TMP_Text _selfName, _selfLife, _selfDeck, _selfHandCount, _selfGrave, _selfFatigue, _selfBank;

        private bool IsNetwork => BattleEntry.Mode == BattleMode.Network;
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
            // 原因统一悬停就地展示）：2026-10-14 晚 prefab 化——节点与摆位归 prefab（hover-info+lbl），
            // 这里只查找绑定标签（富文本/初始隐藏），缺节点=悬停文本静默不显
            _hoverInfo = FindOptional("hover-info");
            if (_hoverInfo != null)
            {
                _hoverInfoLabel = _hoverInfo.GetComponentInChildren<TMP_Text>(true);
                if (_hoverInfoLabel != null) _hoverInfoLabel.richText = true;
                _hoverInfo.gameObject.SetActive(false);
            }

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

            // 战斗日志开关（2026-10-13 基础教学：日志改喂 log-list——btn-log 切换高度 0↔700；
            // ClickSwallow 截断面板内点击冒泡，滚日志不误触收起）
            BindBattleButton("btn-log", "对局日志", ToggleLogList);
            _logListRect = FindOptional("log-list");
            if (_logListRect != null)
            {
                _logListRect.gameObject.AddComponent<ClickSwallow>();
                _logListRect.sizeDelta = new Vector2(_logListRect.sizeDelta.x, 0f); // 初始收起（烘焙 700→开局不闪空面板）
            }

            // 不入连锁（2026-10-15）：守卫响应窗口让过口——横幅提示已并入 lbl-toast（2026-10-16），
            // 窗口期按钮反馈=本按钮点亮；点击=让过（等价右键/ESC），平时隐藏
            _btnNotChain = BindBattleButton("btn-notchain", "不连锁", OnNotChain);
            if (_btnNotChain != null) _btnNotChain.gameObject.SetActive(false);

            // Region 区域面板骨架（prefab 用户节点：点棋盘墓地/除外/发动区/卡组/临时元素格打开）
            BuildRegionPanel();

            UiKit.Updater.Attach(Root, () =>
            {
                _selfFan?.Tick();
                _oppFan?.Tick();
                TickLogList();      // 日志面板高度动画
                PollFieldPointer(); // 3D 棋盘悬停/点击（区域格/功能格/单位地牌统一入口——2026-10-13）
                TickMatchClock();   // 双方操作计时（真人对战掉秒/超时判负；人机刷 ∞ 显示）
                PollFreezeProbe(); // 冻结取证探针（按 T 输出快照）：排查通过后注释本行退役，同类问题再启用
            });
        }

        // ======================================== 冻结取证探针（2026-10-15） ========================================

        // 入场演出链路取证计数（T 键快照消费；排查后随 PollFreezeProbe 退役）：付费→入场事件→入队→消费全链打点
        private int _probePay, _probePut, _probePutCast, _probePutOther, _probeEnq;
        private string _probePaidLast = "-", _probePutLast = "-", _probeEnqLast = "-";

        /// <summary>按 T 输出对局冻结取证快照：引擎等待链（栈/优先权/结算/待发/反问）+ UI 窗口态
        ///（守卫/效果窗/倒计时）+ 编舞与演出在途 + AttackDeclarationEvent 订阅健康度（对手攻击
        /// 无箭头取证）一次看全。纯只读（私有成员反射），任何一段异常不拖垮其余段。</summary>
        private void PollFreezeProbe()
        {
            if (!Input.GetKeyDown(KeyCode.T)) return;
            var sb = new System.Text.StringBuilder("[冻结取证]");
            try
            {
                var core = Core;
                if (core == null) sb.Append(" Core=null");
                else
                {
                    var te = core.TurnEngine;
                    var tp = te != null ? te.TurnPlayer : null;
                    sb.Append($" 回合方={tp?.Name ?? "无"}(AI={tp?.IsAI}) 回合数={te?.TurnNumber} 终局={core.IsGameOver}");
                    var se = core.StackEngine;
                    if (se != null)
                    {
                        sb.Append($" | 栈高={se.StackSize}(空={se.IsEmpty}) 结算中={se.IsResolving} 待发={se.HasPendingEffects}"
                            + $" 优先权={se.CurrentPriorityHolder?.Name ?? "无"} 阶段={core.TurnEngine?.CurrentPhase?.Phase}(真值·StackEngine.CurrentPhase是死字段勿读)");
                        foreach (var obj in se.GetStackContents())
                            sb.Append($"\n  栈项: 攻宣={obj.IsAttackDeclaration} 守宣={obj.IsGuardDeclaration}"
                                + $" 源={obj.Source} 控={obj.Controller?.Name} 速={obj.ActivationSpeed}");
                    }
                }
            }
            catch (System.Exception e) { sb.Append($" 引擎段异常={e.Message}"); }

            try
            {
                sb.Append($" | 反问在途={CardCore.TargetSelectionService.PendingRequests}"
                    + $" 选择器={(CardCore.TargetSelectionService.Current != null ? CardCore.TargetSelectionService.Current.GetType().Name : "null→自动")}"
                    + $" Region选择面={(SynergyUI.RegionTargetSelector.IsOpen ? "开(窗口应可见)" : "关")}"
                    + $" 人类响应器={(CardCore.ResponseWindowService.HumanResponder != null ? "已注册" : "未注册")}");
            }
            catch (System.Exception e) { sb.Append($" 反问段异常={e.Message}"); }

            try
            {
                sb.Append($" | 守卫窗={(_guardTcs != null ? "开" : "关")}(候选{_guardOptions?.Count ?? 0})"
                    + $" 倒计时代次{_notChainSerial}"
                    + $" 我方回合={(_view != null && _view.MyTurn)} 单位己{_view?.SelfUnits?.Count ?? 0}/敌{_view?.OppUnits?.Count ?? 0}");
            }
            catch (System.Exception e) { sb.Append($" 窗口段异常={e.Message}"); }

            try
            {
                sb.Append($" | 编舞={(_choreo != null ? "在" : "无")}{(_choreo != null && _choreo.IsTargeting ? "(选择中)" : "")}");
                if (_choreo != null)
                {
                    var ct = typeof(CombatChoreographer);
                    foreach (var fn in new[] { "_capturing", "_arrowLive", "_attackerId", "_targetId" })
                    {
                        var f = ct.GetField(fn, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (f != null) sb.Append($" {fn}={f.GetValue(_choreo)}");
                    }
                }
                if (_field != null)
                {
                    var ft = _field.GetType();
                    var vf = ft.GetField("_visualsInFlight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var hf = ft.GetField("_heldUnits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (vf != null) sb.Append($" 演出在途={vf.GetValue(_field)}");
                    if (hf != null) sb.Append($" hold单位={(hf.GetValue(_field) as System.Collections.ICollection)?.Count ?? -1}");
                }
            }
            catch (System.Exception e) { sb.Append($" 演出段异常={e.Message}"); }

            try
            {
                // 入场演出链路（2026-10-15 对战无过渡取证）：付费→入场事件(源)→入队→待消费→特效资产
                sb.Append($"\n[入场链] 付费事件={_probePay}(最近 {_probePaidLast})"
                          + $" 入场事件={_probePut}(Cast={_probePutCast} 其他={_probePutOther} 最近源={_probePutLast})"
                          + $" 入队={_probeEnq}(最近 {_probeEnqLast})");
                if (_field != null)
                {
                    var arrd = _field.GetType().GetField("_arrivals",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(_field);
                    sb.Append($" 待消费载荷={(arrd as System.Collections.ICollection)?.Count ?? -1}条");
                }
                var cat = typeof(BattleCastArrival);
                var cAsset = cat.GetField("_asset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null);
                var cTried = cat.GetField("_assetTried", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null);
                sb.Append($" 特效资产={(cAsset != null ? "已载" : "未载")}(tried={cTried})");
            }
            catch (System.Exception e) { sb.Append($" 入场链段异常={e.Message}"); }

            try
            {
                // 订阅健康度：AttackDeclarationEvent 广播条目的接收者类型（编舞器缺席=对手攻击无箭头根因）
                var dict = typeof(CardCore.EventManager).GetField("_broadcastHandlers",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(CardCore.EventManager.Instance) as System.Collections.IDictionary;
                sb.Append(" | 攻宣订阅=[");
                bool found = false;
                if (dict != null)
                    foreach (System.Collections.DictionaryEntry de in dict)
                    {
                        if (!(de.Key is System.Type kt) || kt != typeof(CardCore.AttackDeclarationEvent)) continue;
                        foreach (var entry in (System.Collections.IEnumerable)de.Value)
                        {
                            var orig = entry?.GetType().GetField("Original",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance)?.GetValue(entry) as System.Delegate;
                            sb.Append($"{orig?.Target?.GetType().Name ?? "静态"} ");
                            found = true;
                        }
                    }
                sb.Append(found ? "]" : "0 条——编舞器未订阅=对手攻击无箭头+无演出]");
            }
            catch (System.Exception e) { sb.Append($" 订阅段异常={e.Message}"); }

            Debug.Log(sb.ToString());
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
            // lbl-toast（2026-10-14 晚兜底口径 + 2026-10-16 显隐单口）：prefab 无此节点即无 toast——提示回落 Console；
            // 面板底板（Image）常绘且 prefab 默认 active——绑定即隐藏，ShowToast 空文本=藏板
            //（否则自动取目标等无窗路径走完后，空底板横幅滞留屏上）
            _lblToast = FindTextOptional("lbl-toast");
            _toastPanel = FindOptional("lbl-toast")?.gameObject;
            if (_toastPanel != null) _toastPanel.SetActive(false);
            EnsurePromptBanner(); // 教学步骤/守卫提示（2026-10-16 横幅合并：文案进 toast 底层，guide-banner 节点退役隐藏）

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
            // lbl-activation（发动区文本摘要）已随 2026-10-15 发动格拆除退役：施放中卡改立牌上盘
            //（BattleFieldView 施放占格）；prefab 本无此节点，写入点先前即静默空转
            if (UiKit.FindDeep(Root, "btn-grave-play") != null)
                _btnGrave = BindButton("btn-grave-play", () => OnGraveyardPlay().Forget());
            if (UiKit.FindDeep(Root, "btn-pass-priority") != null)
                _btnPass = BindButton("btn-pass-priority", OnPassPriority);
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
            _logList = FindScroll("log-list");
        }

        // ======================================== 引导/守卫提示面（2026-10-14：教学闸早已生效，缺的是看得见） ========================================

        /// <summary>引导/守卫提示接线（教学步骤事件驱动，幂等——重入只补绑防重复订阅）。
        /// 2026-10-16 用户定案：横幅提示合并 lbl-toast——guide-banner 节点退役（找到即隐藏
        /// 不再写入），文案改写 toast 底层（UpdatePromptBanner→_toastBase）。</summary>
        private void EnsurePromptBanner()
        {
            if (_guideHooked) return;
            var legacy = UiKit.FindDeep(Root, "guide-banner") as RectTransform;
            if (legacy != null) legacy.gameObject.SetActive(false); // 旧横幅退役隐藏（防 prefab 默认 active 残影）
            TutorialGuide.OnStepAdvanced += OnGuideStepAdvanced;
            _guideHooked = true;
        }

        private void OnGuideStepAdvanced(int stepIndex) => UpdatePromptBanner();

        /// <summary>toast 底层横幅文案：overrideText（守卫提示等）优先；否则教学「第x/n步 hint」；
        /// 无内容=清底层。顶层临时提示（选择/响应窗）在位时遮住底层，关窗清空自动回落。</summary>
        private void UpdatePromptBanner(string overrideText = null)
        {
            string text = overrideText;
            if (text == null && TutorialGuide.Active && TutorialGuide.Current != null)
            {
                int idx = TutorialGuide.StepIndex;
                text = $"【教学 第{idx + 1}/{TutorialGuide.Steps.Count}步】{TutorialGuide.Current.hint}";
            }
            _toastBase = text;
            RenderToast();
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

            // 比赛禁联机（NetGate）：网络模式入口已全部屏蔽，此处防御性回落——
            // 万一 Mode 被残留置为 Network（如旧状态/外部注入），也按本地 AI 开局，绝不外连
            if (IsNetwork && !NetGate.OnlineEnabled)
                BattleEntry.Mode = BattleMode.LocalAI;

            // 操作计时（2026-10-14）：仅真人对战建钟（NetGate 回落后此处必为本地=不建）；
            // 人机对局 _matchClock 恒 null——TickMatchClock 只刷 ∞ 显示
            _matchClock = IsNetwork ? new MatchClock() : null;
            _hudTimeAccum = 0;
            _clockPrevTurn = _clockPrevActive = -1;

            if (_lblMode != null)
                _lblMode.text = IsNetwork ? $"网络 · {BattleEntry.Host}:{BattleEntry.Port}"
                    : BattleEntry.Mode == BattleMode.Tutorial ? $"教学 · {BattleEntry.TutorialId}"
                    : BattleEntry.Mode == BattleMode.Challenge ? $"挑战 · 难度{BattleEntry.ChallengeDifficulty}"
                    : "本地 · AI";
            if (_btnPass != null)
                _btnPass.gameObject.SetActive(IsNetwork);

            // 战场 3D 摆位 + 装台（2026-10-13 战场重做：本地/网络两路统一——
            // 网络快照经 OnNetSnapshot→_field.Apply 同轨渲染，交互桩维持 NetGate 现状）
            _field = new BattleFieldView();
            // 战斗编舞（2026-10-13）：3D 寻路箭头选攻击目标 + 战斗演出（走位/受击/死亡漩涡吸走）；
            // 事件订阅须先于本屏刷新订阅（宣言即 hold 位姿），OnEnter 建立即满足
            _choreo = new CombatChoreographer(_field, () => _view, RefreshField, listenEvents: !IsNetwork, selfPlayer: () => P1);
            _cellPicker = new BattleCellPicker();
            BattleStageDirector.MountBoardAndDawnAsync().Forget(); // 转场③④：装载棋盘（分区表注入）→ 日出
            RefreshFieldWhenBoardReady().Forget();                 // 棋盘就绪后首刷（英雄柱/初始摆位）

            if (IsNetwork)
            {
                Debug.Log("[BattleScreen] 网络空屏桩（对局交互暂缺，战场渲染已接快照）——联机入口按 NetGate 现状禁用");
                return;
            }

            StartLocalFan();
        }

        public override void OnExit()
        {
            _running = false;
            _net?.Close();
            _net = null;
            CloseActionPopup(); // 3D 点击操作弹层（2026-10-13）
            TutorialFlow.ClearAssessment(); // 考核标记防跨局残留（未终局退出路径）
            TutorialGuide.End();            // 引导闸卸载防跨局残留

            // 守卫窗口解挂起（防切屏卡死 AI 回合 await）+ 拦截箭头回收 + 提示横幅退订
            _responsePassAll = true;         // 整窗让过旗先置（ResolveGuardPrompt 同步续跑分档流程，防降档再开窗）
            if (_guardTcs != null) ResolveGuardPrompt(null, "界面退出——让过");
            _notChainSerial++;               // 作废在途响应窗倒计时（2026-10-16 弹窗退役后收口单源）
            _regionSelector?.Cancel();       // Region 选择面同步收口（空结果——防销毁后异步收尾触已毁节点）
            _guardArrow.Dispose();
            if (_guideHooked) { TutorialGuide.OnStepAdvanced -= OnGuideStepAdvanced; _guideHooked = false; }
            _toastBase = _toastOverlay = null; // toast 双层清空（2026-10-16 横幅合并后收口单点）
            if (_toastPanel != null) _toastPanel.SetActive(false);

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
            _matchClock = null; // 操作计时随屏退役（人机局本就未建）
            HideAiCastStage(); // AI 出牌舞台防御性收幕（正常路径由 SimpleAI 的 finally 收）
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
            if (ResponseWindowService.HumanResponder == ShowResponsePopupAsync)
                ResponseWindowService.HumanResponder = null;
        }

        // ======================================== 本地模式 ========================================
        //（2026-10-15 彻底清除）旧一代本地启动 StartLocal 已删——零调用者的迁移残留，其订阅表与
        // 现役 StartLocalFan 漂移两次咬人（立牌滞留/入场演出失效）。本地局唯一入口=StartLocalFan；
        // 随迁已删：AppendLocal/OnGameOver（仅旧表引用）。RefreshLocal 仍被网络等路径共用故保留。

        /// <summary>教学固定局开局（2026-10-02）：TutorialConfig 指定双方卡组顺序（=摸牌序列，锁牌库序）
        /// 与机器人逐回合剧本（ScriptedAi 按条演出），rngSeed 钉效果随机；机器人响应窗口全让过
        /// （EnablePassiveAiResponder——永不反制玩家）。返回 false=配置缺失回落普通局。
        /// 预设场面（2026-10-06 教学直入）：scenario 非 null=组卡取双方场面并集、
        /// 不走起手/摸牌序列断言，TutorialScenarioSeeder 直入起跳回合。</summary>
        private bool StartTutorialGame(BattleController ctrl)
        {
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            if (tutorial == null)
            {
                ShowToast($"教学配置缺失：{BattleEntry.TutorialId}（回落本地 AI 局）");
                return false;
            }
            if (tutorial.scenario != null)
            {
                var playerCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.scenario.player));
                var aiCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.scenario.ai));
                ctrl.StartNewGame(playerCards, aiCards, new ScriptedAi(tutorial),
                    rngSeed: tutorial.rngSeed, scenario: tutorial.scenario);
                ctrl.EnablePassiveAiResponder();
                TutorialGuide.Begin(tutorial); // 暂停引导（2026-10-06）：guide 为空时内部不激活
                return true;
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
            TutorialGuide.Begin(tutorial);
            return true;
        }

        // ======================================== 本地对局（扇形手牌过渡面，2026-10-03 接入） ========================================

        /// <summary>本地对局启动（原型屏 HandFanScreen 移植接入）：开局即建双扇手牌，引擎手牌事件
        /// 幂等对账刷新。战场 3D 重做前的最小对战面——出牌交互暂缺，回合由「结束回合」推进。</summary>
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
            if (mirrorDeck != null)
                // 最后一课/考核镜像局：AI 打同卡组（技能标记双方同用——镜像口径）。
                // 神经 AI 暂离（2026-10-14 用户定案）：战场/观测编码改动后旧导出 tide_policy
                // 输入形状不匹配（36≠49，推理即炸）——先换红快攻脚本 AI 顶住，
                // 大部分改动收敛后重接（改回 aiDriver: _neuralAi 即可，驱动已留推理异常兜底）
                // testScenario 非空=测试开局（TestStartFlow）：镜像局改走教学注入管线
                //（跳起手+Seeder 静默铺场+起跳回合），null=行为与原镜像局完全一致
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
                var aiDeck = BattleController.RandomDeckFrom(CardCatalog.LoadAll());
                _ctrl.StartNewGame(challengeDeck, aiDeck, MakePacedAi(AiStrategy.AutoMatch(aiDeck)),
                    aiLandCapBonus: difficulty, aiExtraOpeningDraws: difficulty,
                    mySkillCardId: challengeSkill,
                    aiSkillCardId: HeroSkillSystem.AutoPickSkillCard(aiDeck));
            }
            else if (BattleEntry.Mode == BattleMode.Tutorial)
            {
                if (!StartTutorialGame(_ctrl))
                    _ctrl.StartNewGame(); // 教学配置缺失回落普通局
            }
            else if (BattleEntry.BothAiRushTest)
            {
                // 临时（2026-10-14 双方快攻脚本全自动对局测试，验收后整段移除）：
                // 灰费镜像卡组×2 + 红快攻策略驱动双方（复刻 AiBattleDriver 双 AI 泵——P1 也交 AI，
                // SimpleAI 驱动「当前回合玩家」，监护循环见 RunBothAiRushTestLoop）。
                // 不用随机卡组：随机彩虹池在锁色地牌经济下全色涝死（CanAffordCast 恒 false，
                // AI 只放地不出牌）——首局实测单位 0/0 纯铺地疲劳局的根因。
                var rushDeck = BuildRushTestMirrorDeck();
                string rushSkill = null;
                foreach (var c in rushDeck)
                    if (c.Supertype == Cardtype.Enchantment) { rushSkill = c.ID; break; }
                _ctrl.StartNewGame(rushDeck, rushDeck,
                    aiDriver: new SimpleAI(AiStrategy.RedAggro),
                    mySkillCardId: rushSkill, aiSkillCardId: rushSkill);
            }
            else
                // 普通本地局兜底（2026-10-13 补）：LocalAI 无镜像时原链无分支命中=空战场不 InitGame
                // 2026-10-14 人机节奏定案：P2 走等待模式——每个动作等表现层演完再执行下一个
                _ctrl.StartNewGame(aiDriver: MakePacedAi());

            // 引擎事件多但刷新幂等：统一对账重建（口径同原原型屏）；
            // RefreshField（2026-10-13 战场重做）=BattleViewData 构建→BattleFieldView 3D 摆位
            Subscribe<CardEnterHandEvent>(_ => RefreshFans());
            Subscribe<CardPlayEvent>(_ => { RefreshFans(); RefreshField(); });            // 出牌→单位/地牌落格
            Subscribe<CardZoneChangeEvent>(_ => { RefreshFans(); RefreshField(); });
            Subscribe<CardMoveEvent>(_ => { RefreshFans(); RefreshField(); });
            Subscribe<CardDestroyEvent>(_ => { RefreshFans(); RefreshField(); });
            // 施放立牌收口（2026-10-15 发动区改版回归修复）：入场/离开发动区/发动失败即时刷新——
            // 白板卡结算零事件（无指示物/无效果），缺此订阅立牌滞留、3D 模型要等回合结束才上；
            // （旧 StartLocal 块本有 CardPutToBattlefieldEvent 订阅，迁 StartLocalFan 时丢失）
            Subscribe<CardPutToBattlefieldEvent>(e => { CaptureArrival(e); RefreshFans(); RefreshField(); }); // CaptureArrival=入场演出载荷组装（2026-10-15 出场浮现）
            Subscribe<CardLeaveActivationEvent>(_ => { RefreshFans(); RefreshField(); }); // 结算入墓/失败回退：立牌即清
            Subscribe<CardActivationFailedEvent>(_ => { RefreshFans(); RefreshField(); }); // 满场入墓口无离区事件
            Subscribe<StackEmptyEvent>(_ => RefreshField()); // 栈清空=一批结算完成的全量对账兜底（与旧 StartLocal 对齐）
            Subscribe<CounterChangedEvent>(_ => { RefreshFans(); RefreshField(); }); // 指示物→对手卡翻面/攻生→头顶数值
            Subscribe<RevealCardsEvent>(_ => RefreshFans());
            // 棋盘数字条（2026-10-14 技能格/地牌格/上限格上盘）：黑白钱包与地牌横置跨回合清零（TurnStart）、
            // 角色生命（LifeChange）、钱包入账/支付（ElementPool 两事件）——RefreshField 全量对账覆盖
            Subscribe<TurnStartEvent>(_ => RefreshField());
            Subscribe<PhaseStartEvent>(_ => RefreshField()); // TurnStart 先于切阶段发布：缺此订阅 _view.Phase 滞留上回合 End（回合≥2 点单位被误拒）
            Subscribe<LifeChangeEvent>(_ => RefreshField());
            Subscribe<ElementPoolGainEvent>(_ => RefreshField());
            Subscribe<ElementPoolPayEvent>(e => { CapturePaid(e); RefreshField(); }); // CapturePaid=最近实付账单（入场演出颜色口径）
            // 放地入池（2026-10-14）：入池路径只发本事件（移区不发 CardMove/ZoneChange）——
            // 手牌收卡+地格计数即时对账；AI 回合放地同轨（LandFinal 直调 RestoreFan 为玩家侧兜底）
            Subscribe<ElementPoolAddEvent>(_ => { RefreshFans(); RefreshField(); });
            // 攻击宣言（2026-10-14 优先权半场高光）：宣言即优先权翻转（守卫窗口对手持有）——
            // 无此订阅则红蓝切换要等下一结算事件才反映
            Subscribe<AttackDeclarationEvent>(_ => RefreshField());
            Subscribe<GameOverEvent>(OnGameOverFan);

            // 本地战报桥（2026-10-16 定案：主动/自动效果**发动**与**结算结果**进 log-list——
            // BattleEventText.ForLocal 此前零接线=本地局日志除系统行外恒空；网络局照旧走 ForNet）
            WireLocalBattleLog();

            // 出牌选格钉已在声明成功点直转 BoardState（2026-10-15 发动格拆除——见 PlayCardFinal）；
            // 旧「入场事件补钉」订阅（OnCardEnterForPendingPin）随施放占格机制退役
            Subscribe<TurnStartEvent>(_ => _pendingPins.Clear());

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
            // 指向性法术目标统一走结算期反问（与生物技能同 UI 同源）。
            _regionSelector = _regionPanel != null
                // 2026-10-16：boardCellOf=场上候选红瞄准面片；isLocalPlayer=角色候选【己方/对方角色】判侧（本地局人类恒 P1）；
                // roleCellOf=角色候选的红瞄准面片落格（选中才上盘·2026-10-16 晚定案）
                // buildPrompt/onPrompt（2026-10-16 lbl-toast 定案）：开窗 toast=选择目的说明（完整效果文），
                // 主动效果亮「取消」——详见 BuildSelectPrompt/OnSelectPrompt
                ? new RegionTargetSelector(_regionPanel, BoardCellOf, p => p == P1, RoleCellOf, BuildSelectPrompt, OnSelectPrompt)
                : null;
            TargetSelectionService.Current = _regionSelector;

            // 响应窗口（2026-10-14 接回）：守卫候选走棋盘箭头交互（效果类候选回落弹窗）
            ResponseWindowService.HumanResponder = ShowResponsePopupAsync;

            // 临时（2026-10-14 双方快攻测试）：P1 也交 AI + 终局证据日志 + 回合监护泵
            if (BattleEntry.BothAiRushTest)
            {
                if (P1 != null) P1.IsAI = true;
                Subscribe<GameOverEvent>(e => Debug.Log("[双方快攻测试] 终局：" +
                    (e.Winner == P1 ? "P1 胜" : "P2 胜") + "（" + e.Reason + "，" + e.TotalTurns + " 回合）"));
                RunBothAiRushTestLoop().Forget();
            }

            RefreshFans();
            RefreshField(); // 补偿刷（2026-10-13）：订阅注册前已发生的静默变化（教学场景注入零事件）兜底
            UpdatePromptBanner(); // 教学首步文案（Begin 在 StartTutorialGame 内已触发过事件，此处补首帧）
            Debug.Log("[BattleScreen] 本地对局已启动（扇形手牌过渡面；战场 3D 重做中）");
        }

        /// <summary>目标候选的场上占格（2026-10-16 红瞄准面片）：BattleFieldView 单源查询——
        /// 单位（生物/结界/衍生物）与施放占格立牌在场上即返回数据格；不在场（手牌/墓地/牌库等）
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
            GameActions.EndTurn(Core, P1);
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
        /// + 最小间隔拍（2026-10-16 用户定案：1 秒）。AI-vs-AI（BothAiRushTest/编辑器批量/无头）
        /// 不注入=全速，完全不管渲染层。2026-10-16 出牌舞台：同批注入开演/收幕回调——
        /// 对手出牌先亮（左上角卡面+指向箭头）1 秒再结算。</summary>
        private SimpleAI MakePacedAi(AiStrategy strategy = null)
        {
            return new SimpleAI(strategy)
            {
                PacingWait = async () =>
                {
                    if (_field != null) await _field.WaitVisualsIdleAsync();
                },
                OnCastStaged = ShowAiCastStage,
                OnCastStageEnd = HideAiCastStage,
            };
        }

        // ---- AI 出牌舞台（2026-10-16 用户定案：对手出牌先亮 1 秒再结算）----
        // 左上角 ACard 卡面 + 施放立牌→目标的指向箭头（红=威胁指向）；SimpleAI 声明成功后
        // OnCastStaged 开演、结算前 OnCastStageEnd 收幕（finally 保证终局/异常不漏收）。

        private GameObject _aiStageCardPrefab; // ACard 预制体（HotUpdateAssets 惰性加载，Region 列卡同源）
        private GameObject _aiStageCard;       // 左上角卡面实例（连出多张时先拆旧再建新）
        private readonly List<BattleAttackArrow> _aiStageArrows = new List<BattleAttackArrow>();

        /// <summary>舞台开演：亮出对手所出的卡（左上角全信息卡面）+ 目标指向箭头。
        /// 箭头只对场上单位/角色目标出（手牌/牌库等对象无棋盘锚）；起点=施放立牌占格
        /// （CardPlayEvent 同步对账已上盘），缺件回落对手角色格。</summary>
        private void ShowAiCastStage(Card card, List<Entity> targets)
        {
            HideAiCastStage();
            if (card == null || Root == null) return;

            if (_aiStageCardPrefab == null)
                _aiStageCardPrefab = HotUpdateAssets.Load<GameObject>("ACard", UiKit.PrefabPath("ACard"));
            if (_aiStageCardPrefab != null)
            {
                var go = UnityEngine.Object.Instantiate(_aiStageCardPrefab, Root, false);
                go.name = "ai-cast-stage";
                var rt = (RectTransform)go.transform;
                rt.SetAsLastSibling();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // 屏幕左上角
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(24f, -24f);
                var cv = go.AddComponent<ACardView>();
                cv.Bind();
                cv.Apply(CardOverlayItem.FromLiveCard(card), CardOverlayLayout.Full);
                _aiStageCard = go;
            }

            Vector3 from = AiCastOrigin(card);
            foreach (var t in targets ?? new List<Entity>(0))
            {
                var anchor = StageTargetAnchor(t);
                if (anchor == null) continue;
                var arrow = new BattleAttackArrow();
                arrow.Show(from, anchor.Value, BattleAttackArrow.AttackSkin);
                _aiStageArrows.Add(arrow);
            }
        }

        /// <summary>舞台收幕：卡面销毁 + 箭头冻结渐隐（0.3s）后回收。</summary>
        private void HideAiCastStage()
        {
            if (_aiStageCard != null) { UnityEngine.Object.Destroy(_aiStageCard); _aiStageCard = null; }
            foreach (var a in _aiStageArrows) FadeDisposeArrowAsync(a).Forget();
            _aiStageArrows.Clear();
        }

        private static async UniTaskVoid FadeDisposeArrowAsync(BattleAttackArrow arrow)
        {
            if (arrow == null) return;
            await arrow.FadeOutAsync(0.3f);
            arrow.Dispose();
        }

        /// <summary>箭头起点：施放立牌占格心上抬（TryGetOnBoardCell 含立牌占格账）；缺件回落对手角色格心。</summary>
        private Vector3 AiCastOrigin(Card card)
        {
            if (_field != null && card != null && _field.TryGetOnBoardCell(card.RuntimeId, out int x, out int z))
                return BattleBoardGrid.WorldPositionOf(x, z) + new Vector3(0f, 0.6f, 0f);
            var c = BoardLayout.CharacterCell(1);
            return BattleBoardGrid.WorldPositionOf(c.x, c.z) + new Vector3(0f, 0.6f, 0f);
        }

        /// <summary>目标锚点：场上单位=模型 wrapper/格心（UnitAnchorOf 同口径）；角色=角色格心上抬
        ///（守卫窗口角色锚同款）。非场上对象（手牌/牌库/未落格）=null 不出箭头。</summary>
        private Vector3? StageTargetAnchor(Entity t)
        {
            if (t is Card c)
            {
                var v = (_view?.SelfUnits ?? new List<BattleCardView>())
                    .Concat(_view?.OppUnits ?? new List<BattleCardView>())
                    .FirstOrDefault(u => u != null && !u.IsDead && ReferenceEquals(u.CoreCard, c));
                return v != null ? UnitAnchorOf(v) : (Vector3?)null;
            }
            if (t is Player p)
            {
                var cell = BoardLayout.CharacterCell(p == P1 ? 0 : 1);
                return BattleBoardGrid.WorldPositionOf(cell.x, cell.z) + new Vector3(0f, 0.6f, 0f);
            }
            return null;
        }

        /// <summary>临时（2026-10-14 双方快攻测试，验收后移除）：灰费一致快攻镜像卡组——
        /// 9 张纯灰推导价测试卡按重复张数凑 30（构筑规则允许重复；卡 ID 查名解析）。</summary>
        private static List<CardData> BuildRushTestMirrorDeck()
        {
            var byName = new Dictionary<string, CardData>();
            foreach (var c in SynergyUI.CardCatalog.LoadPlayPool()) byName[c.CardName] = c;
            var plan = new[]
            {
                new[] { "试作·猎犬", "5" }, new[] { "试作·斥候", "5" }, new[] { "试作·重枪兵", "3" },
                new[] { "试作·狂战士", "3" }, new[] { "试作·火弹", "5" }, new[] { "试作·战号", "2" },
                new[] { "试作·磨刀", "2" }, new[] { "试作·急救", "2" }, new[] { "试作·演武场", "3" },
            };
            var deck = new List<CardData>();
            foreach (var row in plan)
            {
                CardData c;
                if (!byName.TryGetValue(row[0], out c)) { Debug.LogError("[双方快攻测试] 池缺卡：" + row[0]); continue; }
                int n = int.Parse(row[1]);
                for (int i = 0; i < n; i++) deck.Add(c);
            }
            Debug.Log("[双方快攻测试] 镜像卡组 " + deck.Count + " 张（灰费；技能=" +
                (deck.FirstOrDefault(x => x.Supertype == Cardtype.Enchantment) != null ? "试作·演武场" : "无") + "）");
            return deck;
        }

        /// <summary>临时（2026-10-14 双方快攻测试，验收后移除）：双 AI 回合监护泵——
        /// 驱动当前 AI 回合→等引擎泵翻面（同 OnEndTurnFan 口径），终局或 200 回合护栏退出。
        /// 首帧延迟让本屏事件订阅/补偿刷先就位（AI 事件不漏刷）。</summary>
        private async Cysharp.Threading.Tasks.UniTaskVoid RunBothAiRushTestLoop()
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            for (int guard = 0; guard < 200 && !_gameEnded && Core != null; guard++)
            {
                var tp = _ctrl.TurnPlayer;
                if (tp == null || !tp.IsAI) { await Cysharp.Threading.Tasks.UniTask.Yield(); continue; }
                await _ctrl.RunAiTurnAsync();
                // 回合切换延迟一拍生效（EndTurn 排队推进、引擎泵下一帧才翻 TurnPlayer）
                for (int i = 0; i < 120 && !_gameEnded && Core != null && _ctrl.TurnPlayer == tp; i++)
                    await Cysharp.Threading.Tasks.UniTask.Yield();
            }
            Debug.Log("[双方快攻测试] 对局循环退出：gameEnded=" + _gameEnded);
        }

        /// <summary>终局弹窗（扇形过渡面口径；旧链 OnGameOver 的 RefreshLocal 依赖已删预制体字段，不走）。
        /// 2026-10-16 弹窗退役：终局呈现=toast+战报行，退出=认输键改「返回主菜单」直回（MarkGameOverUi）。</summary>
        private void OnGameOverFan(GameOverEvent e)
        {
            _gameEnded = true;
            bool win = e.Winner == P1;
            TutorialFlow.ReportBattleResult(BattleEntry.Mode, win); // 教学逻辑层：考核局/教学局结果落档
            MarkGameOverUi($"{(win ? "胜利" : "失败")}——{(win ? "我方" : "对手")}获胜（{e.Reason}）");
        }

        /// <summary>终局呈现（2026-10-16 旧弹窗退役）：提示走 toast+战报；认输键文案改「返回主菜单」
        ///（OnConcede 终局分支直回——旧弹窗取消键职责迁移，btn-back 已退役本屏无其他退出口）。</summary>
        private void MarkGameOverUi(string msg)
        {
            ShowToast(msg);
            AppendLog(msg, "log-line--system");
            if (_btnConcede != null)
            {
                var lbl = _btnConcede.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = "返回主菜单";
            }
        }

        /// <summary>幂等全量对账：己方手牌全正面；对手手牌默认背面，带 Exposed 指示物的翻正面。</summary>
        private void RefreshFans()
        {
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

        private int _lastCoreNullLogFrame = -999; // RefreshField 核心未就绪跳过的节流日志帧

        /// <summary>摆位刷新（渲染唯一输入=BattleViewData）：本地=BuildLocal 直连构建；
        /// 网络=_view 已由 OnNetSnapshot 构建（此处只 Apply）。</summary>
        private void RefreshField()
        {
            if (!IsNetwork)
            {
                if (Core == null || P1 == null || P2 == null)
                {
                    if (Time.frameCount - _lastCoreNullLogFrame > 120)
                    {
                        _lastCoreNullLogFrame = Time.frameCount;
                        Debug.Log($"[BattleField] 刷新跳过：对局核心未就绪（core={Core != null}, p1={P1 != null}, p2={P2 != null}）");
                    }
                    return;
                }
                _view = BattleView.BuildLocal(Core, _ctrl.Board, P1, P2);
            }
            _field?.Apply(_view);
            RefreshBars(); // 2D 信息栏（2026-10-13 补回的旧控件）随 3D 对账同频刷新
            RefreshRegion(); // Region 区域面板开着则同频重列（区域变化走本刷新链）
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
            RefreshChainSpeedTile();                 // 连锁速度格（中线 x3）=引擎记速器实值（2026-10-15 接线）
            if (_lblStack != null) _lblStack.text = string.Join(" ｜ ", d.StackLines);
            if (_lblPriority != null)
                _lblPriority.text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";
            UpdateButtons(d);
        }

        // ======================================== 双方操作计时（2026-10-14：真人对战掉秒/超时判负；人机显 ∞） ========================================

        /// <summary>操作计时驱动（每帧）：网络真人局按快照优先权持有方掉秒、回合切换 +60 归
        /// 刚结束回合方、归零=对方胜利（客户端表现层判定——服务器权威计时待联机复活迁移）；
        /// 人机局不建钟只刷 ∞ 显示。时间格装台晚于本屏——4Hz 周期重写直至就绪（幂等）。</summary>
        private void TickMatchClock()
        {
            if (_matchClock != null)
            {
                var d = _view;
                if (d != null)
                {
                    int viewer = MyNetSeat;
                    // 优先权持有方掉秒（栈空主阶段快照 PrioritySeat=回合方，同口径）
                    int timedOut = _matchClock.Tick(Time.deltaTime, d.MyPriority ? viewer : 1 - viewer);
                    if (timedOut >= 0 && !_netGameOver && !_gameEnded)
                    {
                        bool iLost = timedOut == viewer;
                        _netGameOver = true;
                        Debug.Log($"[BattleClock] {(iLost ? "我方" : "对手")}操作超时——{(iLost ? "对手" : "我方")}胜利");
                        MarkGameOverUi(iLost ? "超时判负——操作时间耗尽，对手胜利" : "对手超时——操作时间耗尽，我方胜利");
                    }

                    // 回合切换（TurnNumber 变化）=上一回合结束：+60 归刚结束回合方
                    if (_clockPrevTurn >= 0 && d.TurnNumber != _clockPrevTurn && _clockPrevActive >= 0)
                        _matchClock.AddTurnEndBonus(_clockPrevActive);
                    _clockPrevTurn = d.TurnNumber;
                    _clockPrevActive = d.MyTurn ? viewer : 1 - viewer;
                }
            }

            _hudTimeAccum += Time.deltaTime;
            if (_hudTimeAccum < 0.25f) return;
            _hudTimeAccum = 0;
            RefreshChainSpeedTile(); // 记速器 4Hz 兜底重写（格装台晚于本屏/栈事件间隙同周期覆盖）
            RefreshTempGainTiles(); // 临时元素产量双格 4Hz 兜底重写（获得事件→RefreshField 已重建 _view）
            if (_matchClock == null)
            {
                // 人机对局：时间无限（不建钟不掉秒）
                BattleHudRow.SetStatusValue(BattleHudRow.SelfTimeCol, "∞");
                BattleHudRow.SetStatusValue(BattleHudRow.OppTimeCol, "∞");
                return;
            }
            int v = MyNetSeat;
            BattleHudRow.SetStatusValue(BattleHudRow.SelfTimeCol, MatchClock.Format(_matchClock.Remaining(v)));
            BattleHudRow.SetStatusValue(BattleHudRow.OppTimeCol, MatchClock.Format(_matchClock.Remaining(1 - v)));
        }

        /// <summary>连锁速度格（中线 x3）喂文（2026-10-15 接线）：本地引擎记速器实值——
        /// 0=无连锁、N=本连锁已发动的最高速度、结算中附「·结算」标记；
        /// 网络快照未承载（ChainSpeed=-1）保持占位「—」（联机复活随 MGSS 协议补字段）。
        /// 未装台/缺格静默（SetStatusValue 幂等——RefreshBars/RefreshView/4Hz 三路同喂）。</summary>
        private void RefreshChainSpeedTile()
        {
            var d = _view;
            if (d == null || d.ChainSpeed < 0) return;
            BattleHudRow.SetStatusValue(BattleHudRow.ChainSpeedCol,
                d.ChainResolving ? d.ChainSpeed + "·结算" : d.ChainSpeed.ToString());
        }

        /// <summary>临时元素产量双格（中线 x2 己方 / x4 对方）喂文（2026-10-16 全类型累计钳）：
        /// 本周期累计产出/地牌上限——六色同池共一配额、自己回合开始清零（跨对方回合保留）。
        /// 未装台/缺格静默（SetStatusValue 幂等——获得事件重建 _view + 4Hz 兜底同喂）。</summary>
        private void RefreshTempGainTiles()
        {
            var d = _view;
            if (d == null) return;
            BattleHudRow.SetStatusValue(BattleHudRow.SelfTempCol, $"{d.Self.TempGainedThisTurn}/{d.Self.LandCap}");
            BattleHudRow.SetStatusValue(BattleHudRow.OppTempCol, $"{d.Opp.TempGainedThisTurn}/{d.Opp.LandCap}");
        }

        /// <summary>临时元素产量双格悬停（2026-10-16 全类型累计钳）：周期口径+双方读数。</summary>
        private string TempGainTileInfo(bool self)
        {
            var pv = self ? _view?.Self : _view?.Opp;
            int gained = pv?.TempGainedThisTurn ?? 0;
            int cap = pv?.LandCap ?? 0;
            return $"{(self ? "己方" : "对方")}临时元素产出\n"
                 + $"本周期（自己回合开始清零，跨对方回合保留）累计 {gained}/{cap}\n"
                 + "全类型六色同池共一配额：代价补偿黑白、错边转化、生物产色等一切临时元素生成都计入；"
                 + "配额耗尽后再生成不入账（支付不受影响——钱包照付）";
        }

        /// <summary>连锁速度格悬停（记速器实值+双方发动门槛，2026-09-13 引擎口径）。</summary>
        private string ChainSpeedTileInfo()
        {
            var d = _view;
            if (d == null || d.ChainSpeed < 0)
                return "连锁速度（记速器）\n（网络对局快照未承载此数值——联机复活时随协议补）";
            string gate = d.MyTurn
                ? $"我方回合：我方再发动需速度 ≥ {d.ChainSpeed}，对手响应需速度 > {d.ChainSpeed}"
                : $"对手回合：对手再发动需速度 ≥ {d.ChainSpeed}，我方响应需速度 > {d.ChainSpeed}";
            string state = d.ChainResolving ? "\n（连锁结算中——禁止新的速度发动）" : "";
            return $"连锁速度（记速器）：{d.ChainSpeed}{state}\n{gate}\n连锁结算完毕后归 0";
        }

        /// <summary>首刷就绪门（2026-10-13 教学局首刷竞速修复）：本地局=棋盘+对局核心齐备——
        /// 首刷曾只等棋盘就绪，会跑赢 StartNewGame（教学场景注入零事件、TurnStart 早于订阅注册），
        /// Core 未就位静默白刷且此后无补偿，预设单位永不渲染；网络局维持只等棋盘（_view 走快照不经 Core）。</summary>
        private bool BoardReadyForFirstPaint()
            => BattleBoardGrid.Ready && (IsNetwork || (Core != null && P1 != null && P2 != null));

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
            _probePay++;
            _probePaidLast = $"seat{seat}:{paid.Count}色";
        }

        /// <summary>CastPlayed 入场 → 取该 seat 最近实付账单组装配荷；无账单/零费回落卡面印刷费色，
        /// 仍无则不登记（模型即时显现）。付费→入场为同步紧邻事件；触发器付费插队=颜色近似（已注释知）。</summary>
        private void CaptureArrival(CardPutToBattlefieldEvent e)
        {
            _probePut++;
            _probePutLast = e.Source.ToString();
            if (e.Source != EnterSource.CastPlayed || _field == null) { _probePutOther++; return; }
            _probePutCast++;
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
            {
                _field.EnqueueArrival(e.Card.RuntimeId, seat, paid);
                _probeEnq++;
                _probeEnqLast = $"rid={e.Card.RuntimeId} seat={seat} {paid.Count}色";
            }
            else _probeEnqLast = "账单空未入队";
        }

        private void RefreshLocal()
        {
            if (Core == null || P1 == null || P2 == null) return;
            _view = BattleView.BuildLocal(Core, _ctrl.Board, P1, P2);
            RefreshView();
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
                    ShowToast($"连接失败：{BattleEntry.Host}:{BattleEntry.Port} — {ex.Message}");
                    AppendLog($"连接失败：{BattleEntry.Host}:{BattleEntry.Port} — {ex.Message}", "log-line--system");
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
            _field?.Apply(_view); // 战场 3D 摆位（2026-10-13：网络快照与本地同轨渲染）
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
                    MarkGameOverUi(win ? "胜利——对局结束" : "失败——对局结束");
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
                    else if (_regionSelector != null)
                        picked = await _regionSelector.SelectIndicesAsync(
                            labels, min, max,
                            string.IsNullOrEmpty(req.Title) ? "请选择" : req.Title,
                            req.Hint ?? "", req.AllowCancel,
                            req.TimeoutSeconds > 0 ? req.TimeoutSeconds : 60f);
                    else
                    {
                        // Region 缺失（旧 UiTargetSelector 弹窗兜底已删，2026-10-16）：自动代选前 min 个
                        picked = new List<int>();
                        for (int i = 0; i < min && i < labels.Count; i++) picked.Add(i);
                    }

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
            FillBar(d.Opp, opp: true);
            FillBar(d.Self, opp: false);

            // 战场=3D 层渲染（BattleFieldView 对账）；旧 2D 棋盘六行/手牌条/卡牌层挂载链已退役
            //（2026-10-14 晚拆除——ACard 实例只出自手牌扇区/Region 列卡/prefab 预览位）

            // 中栏（2026-10-13 基础教学：极简 prefab 无中栏节点——null 判空，缺件零炸）
            if (_lblPhase != null) _lblPhase.text = d.PhaseText;
            BattleHudRow.SetPhase(PhaseTileText(d)); // 阶段显示格（中线 x0 平贴文字）同频喂文
            RefreshChainSpeedTile();                 // 连锁速度格（中线 x3）=引擎记速器实值（2026-10-15 接线）
            if (_lblStack != null) _lblStack.text = string.Join(" ｜ ", d.StackLines);
            if (_lblPriority != null)
                _lblPriority.text = d.StackNotEmpty && d.MyPriority ? "◇ 优先权在我（可响应/让行）" : "";

            // 我方手牌=扇区（RefreshFans 对账），旧手牌条退役
            UpdateButtons(d);
        }

        private void FillBar(BattlePlayerView p, bool opp)
        {
            string aiTag = p.IsAI ? "（AI）" : "";
            if (opp)
            {
                if (_oppName != null) _oppName.text = $"对手{aiTag}";
                if (_oppLife != null) _oppLife.text = $"生命 {p.Life}/{p.MaxHealth}";
                if (_oppDeck != null) _oppDeck.text = $"牌库 {p.DeckCount}";
                if (_oppHand != null) _oppHand.text = $"手牌 {p.HandCount}";
                if (_oppGrave != null) _oppGrave.text = $"墓地 {p.GraveyardCount}";
                if (_oppFatigue != null) _oppFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                if (_oppBank != null) _oppBank.text = $"地牌上限 {p.LandCap} · 临时元素 {p.BankText}";
            }
            else
            {
                if (_selfName != null) _selfName.text = "我方";
                if (_selfLife != null) _selfLife.text = $"生命 {p.Life}/{p.MaxHealth}";
                if (_selfDeck != null) _selfDeck.text = $"牌库 {p.DeckCount}";
                if (_selfHandCount != null) _selfHandCount.text = $"手牌 {p.HandCount}";
                if (_selfGrave != null) _selfGrave.text = $"墓地 {p.GraveyardCount}";
                if (_selfFatigue != null) _selfFatigue.text = p.FatigueCount > 0 ? $"疲劳 {p.FatigueCount}" : "";
                if (_selfBank != null) _selfBank.text = $"地牌上限 {p.LandCap} · 临时元素 {p.BankText}";
            }
        }

        private void UpdateButtons(BattleViewData d)
        {
            bool myMain = d.MyTurn && d.Phase == PhaseType.Main && !_gameEnded;

            if (_btnGrave != null) _btnGrave.interactable = myMain && d.Self.GraveyardCount > 0;
            // 网络协议无技能 intent 通道（2026-10-07 卡牌化后技能卡已挂真实效果定义，
            // 理论可走 IntentActivateEffect 寻址，但通道/验签未做——本地可用，网络暂禁）
            if (_btnSkill != null) _btnSkill.interactable = !IsNetwork && myMain && !_gameEnded;
            if (_btnPass != null) _btnPass.interactable = IsNetwork && d.StackNotEmpty && d.MyPriority && !_gameEnded;
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
                ShowToast(!_view.MyTurn ? "对手回合，暂不可操作" : "当前不是主要阶段");
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
            return string.IsNullOrEmpty(card.CardId) ? null : CardCatalog.GetById(card.CardId);
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
                // 本地=运行口径费（GetCardCost，法术含所选效果锚价）；网络=声明费（无引擎对象，服务器校验权威）
                string cost = IsNetwork
                    ? (data != null ? BattleView.CostTextOf(data.Cost) : "?")
                    : CostText(LocalCostOf(card, i));
                options.Add($"{name}（费用 {cost}）");
            }

            int? picked = await PickRegionOptionAsync(isSpell ? "选择要发动的效果" : "选择模式", options);
            if (picked == null) return; // 缺 Region=无交互（本已点「发动」，静默取消）
            int idx = picked.Value;

            if (!IsNetwork)
            {
                var core = Core;
                // 自动横置口径（2026-09-30）：bank 不足但地牌可产所需元素亦放行——付费步自动横置
                if (core != null && !core.ElementPool.CanPayCostWithAutoTap(
                        GameActions.GetCardCost(card.CoreCard, idx), P1))
                {
                    ShowToast("费用不足——该作用不可发动");
                    RefreshLocal();
                    return;
                }
            }

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

            // 2026-10-16 定案：法术声明期目标预选（PromptLocalTargetThenPlay 旧列表弹窗）退役——
            // 指向性法术目标统一在 cast 结算期经 TargetSelectionService 反问（Region 面板，与生物
            // 技能同源）；牌库等隐藏区/区域自结算类效果不弹选直接结算（引擎 None 模式早退单源）。
            bool ok = fromGrave
                ? GameActions.PlayCardFromGraveyard(core, P1, card.CoreCard, targets, modeIndex)
                : GameActions.PlayCard(core, P1, card.CoreCard, targets, Zone.Hand, modeIndex);
            if (ok)
            {
                // 出牌选格钉提前到声明成功即转 BoardState（2026-10-15 发动格拆除）：施放立牌=玩家所选格，
                // 入场经 AssignCells 钉优先原地转正——同钉单源（旧「入场事件补钉」机制随施放占格退役）
                if (_pendingPins.Remove(card.RuntimeId, out var pin))
                    _ctrl?.Board?.PinCard(card.CoreCard, pin.x, pin.z);
                _ctrl.SettleResponseWindow().Forget();
            }
            else
            {
                _pendingPins.Remove(card.RuntimeId); // 声明失败：选格单作废
                ShowToast(fromGrave ? "无法使用（配额已用或不可支付）" : "无法打出（费用/条件不满足）");
            }
            RefreshLocal();
        }

        // LandFlow（手牌直点放地的旧选色弹窗链）已随 2026-10-16 弹窗退役整删：放地现役入口=
        // OnFanPlaceLand 落格箭头（同格多候选色→PickLandColorAsync 走 Region 卡牌列表）。

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

        /// <summary>放地终态：本地=AddToElementPool(锁色)；网络=intent 上行（ModeIndex 复用为锁色枚举值）。</summary>
        private void LandFinal(BattleCardView card, ManaType color)
        {
            if (IsNetwork)
            {
                _net?.Send(NetworkMessageType.IntentAddToElementPool, new MsgIntentAddToElementPool
                {
                    CardRuntimeId = card.RuntimeId,
                    ModeIndex = (int)color,
                });
                return;
            }
            bool ok = Core != null && GameActions.AddToElementPool(Core, P1, card.CoreCard, color);
            if (!ok) ShowToast("无法放入元素池（上限/资格）");
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

        /// <summary>己方手牌可发动流光评估（随 RefreshFans 对账刷新；非主阶段全熄）。</summary>
        private void EvaluateFanPlayability()
        {
            if (_selfFan == null || Core == null || P1 == null) return;
            bool gate = FanGateQuiet();
            foreach (var card in Core.ZoneManager.GetCards(P1, Zone.Hand))
                _selfFan.SetPlayable(card.RuntimeId, gate && CardCastableNow(card));
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
            if (choices.Count == 0) { ShowToast("该卡无费用颜色可锁定（0费/纯黑白不可作地牌）"); return; }

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
            if (options.Count == 0) { ShowToast("地牌行无空位（颜色数超格）"); return; }

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

        /// <summary>「发动」点击（2026-10-14 定案）：法术不占格=直通现有出牌链（模式窗/目标弹窗在链内）；
        /// 永久物（生物/结界）=模式窗（多模式）→藏手牌→箭头选己方单位区空格（所选格=最终落位，
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
                // 2026-10-15 改版：>1 个作用弹抉择窗（选择要发动的效果）；单作用直通
                if (GetModeCount(view) >= 2) PromptModeThenPlay(view, fromGrave: false).Forget();
                else PlayCardFinal(view, null, 0, fromGrave: false);
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
                ShowToast("费用不足——该分支不可发动");
                return;
            }
            BeginUseCellPicking(view, card, idx);
        }

        /// <summary>永久物出牌选格：候选=己方单位区空格（占用=BoardState 现值，含施放占格卡）；
        /// 箭头自角色格拉出（原发动格锚随发动格拆除退役，2026-10-15）。
        /// 确认→登记选格单（PlayCardFinal 声明成功即转 BoardState 钉——施放立牌与入场转正同钉单源）
        /// →PlayCardFinal（需目标的卡在链内弹目标窗）。取消恢复手牌。</summary>
        private void BeginUseCellPicking(BattleCardView view, Card card, int modeIndex)
        {
            var board = _ctrl?.Board;
            if (board == null) return;

            var options = new List<BattleCellPicker.CellOption>();
            foreach (var cell in BoardLayout.UnitCells(0))
                if (board.CardAt(cell.x, cell.z) == null)
                    options.Add(new BattleCellPicker.CellOption { X = cell.x, Z = cell.z, ColorIndex = -1 });
            if (options.Count == 0) { ShowToast("单位区已满——无法打出"); return; }

            _choreo?.CancelTargeting();
            _selfFan?.SetVisible(false);
            var origin = BoardLayout.CharacterCell(0);
            _cellPicker.Begin(BattleBoardGrid.WorldPositionOf(origin.x, origin.z), options, opt =>
            {
                RestoreFan();
                if (!IsNetwork)
                {
                    _pendingPins[card.RuntimeId] = (opt.X, opt.Z);
                    Debug.Log($"[HandPlay] 永久物出牌选格：{view.Name} → ({opt.X},{opt.Z})（声明成功即钉）");
                }
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
            if (card.IsTapped) { ShowToast("该单位已横置"); return; }
            if (card.Runtime != null && card.Runtime.Power <= 0) { ShowToast("该单位攻击力为 0"); return; }
            if (card.X < 0 || card.Z < 0 || _choreo == null)
            {
                ShowToast("单位位置未知，无法攻击");
                return;
            }

            _choreo.BeginTargeting(card, target =>
            {
                if (IsNetwork)
                {
                    int oppSeat = _view != null && _view.NetViewerSeat >= 0 ? 1 - _view.NetViewerSeat : 1;
                    SendAttackIntent(card, target == null
                        ? new NetEntityRef { Seat = oppSeat, IsPlayer = true }
                        : NetRefOf(target, oppSeat));
                    return;
                }

                Entity captured = target != null && target.CoreCard != null ? (Entity)target.CoreCard : P2;
                if (captured == null || !_ctrl.DeclareAttack(P1, card.CoreCard, captured))
                {
                    ShowToast("无法对该目标攻击");
                    RefreshLocal();
                    return;
                }
                _ctrl.SettleResponseWindow().Forget();
                RefreshLocal();
            });
        }

        // ======================================== 守卫窗口（2026-10-14：对手攻击时点守卫生物出箭头拦截） ========================================

        /// <summary>守卫候选走棋盘交互（用户定案：不弹列表）——点我方守卫生物/角色格=选中出拦截箭头
        /// （悬停攻击者绿、其余黄），再点攻击者=确认；点其他守卫者=换人；右键/ESC=让过；
        /// 点空地=取消选择（窗口仍在，防误触让过）。
        /// moreTiers（2026-10-16 分档询问）=让过守卫后同轮还有速度2+响应要问（文案提示；
        /// 「不连锁」按钮=整窗两档同拒）。</summary>
        private UniTask<ResponseOption> BeginGuardPrompt(Player holder, List<ResponseOption> guards,
            bool moreTiers = false)
        {
            _guardTcs = new UniTaskCompletionSource<ResponseOption>();
            _guardOptions = guards;
            _guardAttacker = guards[0].AttackInstance?.Source;
            _guarder = null;
            _guarderIsHero = false;
            string atkName = GuardAttackerName();
            string tgtName = EntityDisplay(AttackTargetOf(guards[0]));
            bool heroCanGuard = HasHeroGuardOption();
            UpdatePromptBanner($"对手的 {atkName} 攻击 {tgtName}！点我方守卫生物{(heroCanGuard ? "/角色" : "")}拦截"
                + (moreTiers ? "（右键/ESC 让过——让过后还可发动速度2响应）" : "（右键/ESC 让过）"));
            if (_btnNotChain != null)
            {
                SetNotChainLabel("不连锁"); // 复位（上轮倒计时可能残留【N】文案）
                _btnNotChain.gameObject.SetActive(true);
            }
            Debug.Log($"[守卫窗口] {atkName} → {tgtName}：{guards.Count} 个守卫候选（含角色={(heroCanGuard ? "是" : "否")}"
                + $"{(moreTiers ? "；速度1档（让过后问速度2+）" : "")}；棋盘点选，右键让过）");
            return _guardTcs.Task;
        }

        /// <summary>守卫候选里是否有角色（SourcePlayer 匹配 P1——引擎侧资格=攻击弹药&gt;0）。</summary>
        private bool HasHeroGuardOption()
            => _guardOptions != null && P1 != null
               && _guardOptions.Exists(o => o != null && ReferenceEquals(o.SourcePlayer, P1));

        /// <summary>攻击者显示名（单位卡=卡名；角色=玩家名）。</summary>
        private string GuardAttackerName()
            => _guardAttacker is Card ac ? CardDisplayName(ac) : (_guardAttacker as Player)?.Name ?? "角色";

        private static Entity AttackTargetOf(ResponseOption opt)
            => opt?.AttackInstance?.Targets is List<Entity> t && t.Count > 0 ? t[0] : null;

        private void HandleGuardPointer(string hitName, Camera cam, string btnKind = null)
        {
            HandleFieldHover(hitName, btnKind); // 悬停卡面/信息保留（选守卫者时也要能看牌；键上=键解说）

            int x, z;
            bool hasCell = TryParseTile(hitName, out x, out z);
            var atkCell = GuardAttackerCell();
            bool onAttacker = hasCell && atkCell != null && atkCell.Value.x == x && atkCell.Value.z == z;

            // 拦截箭头（2026-10-15 定案：守卫=蓝）：守卫者→鼠标拉长；悬到攻击者=吸附目标格变绿（选对了）
            if (_guarder != null || _guarderIsHero)
            {
                if (onAttacker)
                    _guardArrow.Show(GuarderAnchor(), GuardAttackerAnchor(), BattleAttackArrow.ValidSkin);
                else if (CombatChoreographer.MouseGroundPoint(cam, out Vector3 mousePt))
                    _guardArrow.Show(GuarderAnchor(), mousePt, BattleAttackArrow.GuardSkin);
                else
                    _guardArrow.Hide();
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                ResolveGuardPrompt(null, "让过（右键/ESC）");
                return;
            }
            if (!Input.GetMouseButtonDown(0)) return;

            if (!hasCell) { SelectGuarder(null); return; }

            // 我方角色格=角色守卫入口（2026-10-16：点格体或【守卫】键均=选角色为守卫者）
            var myChar = BoardLayout.CharacterCell(0);
            if (x == myChar.x && z == myChar.z && HasHeroGuardOption())
            {
                SelectGuarder(null, hero: true);
                return;
            }

            var mine = (_view.SelfUnits ?? new List<BattleCardView>())
                .FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
            if (mine != null)
            {
                if (_guardOptions.All(o => !ReferenceEquals(o.SourceCard, mine.CoreCard)))
                {
                    ShowToast("该生物不能守卫（已横置或无守卫能力）");
                    return;
                }
                SelectGuarder(mine);
                return;
            }
            if (_guarder == null && !_guarderIsHero) return;
            if (onAttacker)
            {
                var opt = _guarderIsHero
                    ? _guardOptions.FirstOrDefault(o => ReferenceEquals(o.SourcePlayer, P1))
                    : _guardOptions.FirstOrDefault(o => ReferenceEquals(o.SourceCard, _guarder.CoreCard));
                ResolveGuardPrompt(opt, $"守卫确认：{(_guarderIsHero ? "角色" : _guarder.Name)} 拦截 {GuardAttackerName()}");
                return;
            }
            SelectGuarder(null); // 点空地/非攻击者=只取消选择（不让过）
        }

        private void SelectGuarder(BattleCardView v, bool hero = false)
        {
            bool isHero = v == null && hero;
            if (_guarder == v && _guarderIsHero == isHero) return;
            _guarder = v;
            _guarderIsHero = isHero;
            if (v == null && !isHero) _guardArrow.Hide();
            else Debug.Log($"[守卫窗口] 选守卫者 {(isHero ? "角色" : v.Name)}——点攻击者确认，右键让过");
        }

        private void ResolveGuardPrompt(ResponseOption opt, string reason)
        {
            Debug.Log($"[守卫窗口] {reason}");
            _guardArrow.Hide();
            _guarder = null;
            _guarderIsHero = false;
            _guardOptions = null;
            _guardAttacker = null;
            var tcs = _guardTcs;
            _guardTcs = null;
            _notChainSerial++; // 作废在途倒计时（2026-10-16 混合窗口=守卫+Region 并行，收口单源）
            UpdatePromptBanner(); // 复位回教学步骤文案（无则隐藏）
            if (_btnNotChain != null) _btnNotChain.gameObject.SetActive(false);
            tcs?.TrySetResult(opt);
        }

        /// <summary>不入连锁按钮（2026-10-16 弹窗退役后收口单源）：守卫窗口/Region 响应面
        /// 任一在身=立即让过（Region 收口空表=Pass）；均未开时无操作（防误触）。
        /// 分档询问期=整窗两档同拒（_responsePassAll 防「速度1让过→速度2又被问」）。</summary>
        private void OnNotChain()
        {
            bool guardOpen = _guardTcs != null;
            bool regionPick = RegionTargetSelector.IsOpen;
            if (!guardOpen && !regionPick) return;
            _notChainSerial++; // 作废在途倒计时
            _responsePassAll = true; // 整窗让过：不连锁=拒绝连锁，后续档不再问
            Debug.Log("[响应窗口] 不连锁按钮——立即让过（整窗）");
            if (guardOpen) ResolveGuardPrompt(null, "不入连锁（按钮让过）");
            _regionSelector?.Cancel();
        }

        private BattleCardView GuardAttackerView()
            => (_view.OppUnits ?? new List<BattleCardView>())
                .FirstOrDefault(c => !c.IsDead && ReferenceEquals(c.CoreCard, _guardAttacker));

        /// <summary>守卫窗口攻击者格（单位=其落格；对方角色攻击=其角色格——2026-10-16）。</summary>
        private (int x, int z)? GuardAttackerCell()
        {
            if (_guardAttacker is Card) { var v = GuardAttackerView(); return v != null ? ((int x, int z)?)(v.X, v.Z) : null; }
            if (_guardAttacker is Player) { var c = BoardLayout.CharacterCell(1); return (c.x, c.z); }
            return null;
        }

        /// <summary>守卫窗口攻击者世界锚点（单位=模型 wrapper/格心；角色=角色格心上抬——立柱半高）。</summary>
        private Vector3 GuardAttackerAnchor()
        {
            var v = GuardAttackerView();
            if (v != null) return UnitAnchorOf(v);
            var cell = GuardAttackerCell();
            return cell != null
                ? BattleBoardGrid.WorldPositionOf(cell.Value.x, cell.Value.z) + new Vector3(0f, 0.6f, 0f)
                : Vector3.zero;
        }

        /// <summary>守卫者世界锚点（角色守卫=我方角色格心上抬，与立柱半高同口径）。</summary>
        private Vector3 GuarderAnchor()
        {
            if (_guarderIsHero)
            {
                var c = BoardLayout.CharacterCell(0);
                return BattleBoardGrid.WorldPositionOf(c.x, c.z) + new Vector3(0f, 0.6f, 0f);
            }
            return UnitAnchorOf(_guarder);
        }

        /// <summary>单位世界锚点：模型 wrapper 优先，回落格心（与编舞器 AttackerPos 同口径）。</summary>
        private Vector3 UnitAnchorOf(BattleCardView v)
        {
            if (v == null) return Vector3.zero;
            if (_field != null && _field.TryGetUnitBody(v.RuntimeId, out var wrapper, out _))
                return wrapper.position;
            return BattleBoardGrid.WorldPositionOf(v.X, v.Z);
        }

        /// <summary>点【攻击】键/角色攻击（2026-10-16 与生物同款箭头选目标）：箭头自角色格拉出，
        /// 悬停对方单位/角色格（目标=角色回传 null→P2），点击确认→角色攻击宣言——引擎侧 Player
        /// 攻击者已全面支持（CanDeclareAttack 弹药同门、结算后烧光弹药）。
        /// 伪视图钉角色格、RuntimeId 保持 0（=编舞器 IdOf 的角色哨兵，与 AI 角色攻击宣言同口径）。
        /// 网络模式 intent 已支持 IsPlayer 攻击者，但网络 UI 是空屏桩——同技能按钮先例 toast 拒绝。</summary>
        private void OnClickSelfHero()
        {
            if (IsTargetSelecting()) return; // 目标选择期不开新选择（两语义裁定·见 OnClickHeroZone）
            if (!GateFanMyMain()) return;
            if (IsNetwork) { ShowToast("网络模式角色攻击暂未开放"); return; }
            var core = Core;
            if (core == null || P1 == null) return;
            int ammo = P1.GetPower(); // Player 分支=HeroAttackCounter 弹药层数钳非负
            if (ammo <= 0) { ShowToast("角色无弹药（攻击力 0）"); return; }
            if (_choreo == null) { ShowToast("战场编舞未就绪，无法发起角色攻击"); return; }

            var cell = BoardLayout.CharacterCell(0);
            var heroView = new BattleCardView { Name = "角色", X = cell.x, Z = cell.z };
            _choreo.BeginTargeting(heroView, target =>
            {
                Entity captured = target != null && target.CoreCard != null ? (Entity)target.CoreCard : P2;
                if (captured == null || !_ctrl.DeclareAttack(P1, P1, captured))
                {
                    ShowToast("无法对该目标攻击");
                    RefreshField();
                    return;
                }
                _ctrl.SettleResponseWindow().Forget();
                RefreshField();
            });
        }

        // ======================================== 3D 棋盘指针（2026-10-13 悬停/点击统一入口） ========================================

        private RectTransform _actionPopup; // 已随 2026-10-16 角色格三键改版退役（恒 null，CloseActionPopup 空防保留）

        // ---- 守卫窗口（2026-10-14 棋盘箭头交互：点守卫生物出线→点攻击者确认）----
        // 2026-10-16 角色守卫：守卫者可以是角色（弹药>0 候选——点角色格选中）；攻击者也可以是
        // 对方角色（角色攻击宣言）——确认=点对方角色格。
        private UniTaskCompletionSource<ResponseOption> _guardTcs;
        private List<ResponseOption> _guardOptions;
        private Entity _guardAttacker;             // 本次被响应攻击的攻击者（引擎实体：单位卡或角色）
        private BattleCardView _guarder;           // 已选守卫者（null 且 _guarderIsHero=角色，双 null=未选）
        private bool _guarderIsHero;               // 守卫者=我方角色（2026-10-16 角色守卫）
        private readonly BattleAttackArrow _guardArrow = new BattleAttackArrow();

        // ---- 镜像局神经 AI（2026-10-14 暂离）：战场改动后旧导出 tide_policy 观测形状不符（36≠49），
        //      现役镜像局=红快攻脚本 AI；大部分改动收敛后重接：镜像分支改回 aiDriver: _neuralAi ----
        private readonly NeuralAiDriver _neuralAi = new NeuralAiDriver();

        // ---- 引导/守卫提示（2026-10-16 横幅合并：文案走 toast 底层，guide-banner 节点已退役）----
        private bool _guideHooked;

        /// <summary>3D 棋盘指针轮询（UiKit.Updater.Tick）：每帧一次共享射线——
        /// 悬停路由（单位格=四段卡信息/特殊格与功能地块=hover-info 文本），左键点击路由
        /// （功能地块/特殊格 Region 面板/角色格角色与技能详情/地牌信息）。
        /// UI 命中让路且不关弹窗（销毁按钮会吞掉 onClick，原口径）；单位模型 collider 已拆，
        /// 打格反查是既定通路。2026-10-15 定案：落格=展示信息，攻击/守卫/发动拆到格前三键
        /// （BattleHex.prefab 手搭件，BattleTileButtons 高亮），三键命中归并所属格路由。</summary>
        private void PollFieldPointer()
        {
            ReconcileTileButtons(); // 格前三键每帧对账先行（让路路径同样收键——编舞/选格中不可操作）
            if (_choreo != null && _choreo.IsTargeting)
            {
                ClearHover(); // 攻击目标选择中：悬停/点击由编舞器消费（寻路箭头跟随），本屏路由让路
                return;
            }
            if (_cellPicker != null && _cellPicker.Active)
            {
                ClearHover(); // 放地/出牌选格中：点击由选格器消费（箭头跟随+候选格高亮），本屏路由让路
                return;
            }
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearHover();
                return;
            }
            var cam = BattleStageDirector.WorldCam;
            if (cam == null || _view == null)
            {
                ClearHover();
                if (Input.GetMouseButtonDown(0)) { CloseActionPopup(); CloseRegion(); }
                return;
            }
            RaycastHit hit;
            bool hasHit = Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit, 300f);
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
            if (_guardTcs != null) { HandleGuardPointer(hitName, cam, btnKind); return; } // 守卫窗口：点守卫生物/角色/攻击者
            HandleFieldHover(hitName, btnKind);
            if (Input.GetMouseButtonDown(0)) HandleFieldClick(hitName, btnKind);
        }

        // ---- 悬停路由 ----

        /// <summary>悬停展示：功能地块/特殊格=信息文本；单位格（生物+结界，双方）与角色格（角色+技能卡）
        /// =四段悬停信息（CardHoverInfo：主动/自动/关键词/状态）。格前三键命中已归并所属格——btnKind 非空时
        /// 键解说优先（落格卡信息让路，格体命中仍出卡信息）。</summary>
        private void HandleFieldHover(string hitName, string btnKind = null)
        {
            if (btnKind != null) { SetHoverInfo(TileButtonHoverInfo(btnKind, hitName)); return; }
            if (hitName == "hud-tile-end-turn") { SetHoverInfo(EndTurnTileInfo()); return; }
            if (hitName == "hud-tile-phase") { SetHoverInfo(PhaseTileInfo()); return; }
            if (hitName == "hud-tile-opp-time") { SetHoverInfo("对手剩余操作时间\n（引擎操作计时未接线——占位显示）"); return; }
            if (hitName == "hud-tile-chain-speed") { SetHoverInfo(ChainSpeedTileInfo()); return; }
            if (hitName == "hud-tile-self-temp") { SetHoverInfo(TempGainTileInfo(true)); return; }
            if (hitName == "hud-tile-opp-temp") { SetHoverInfo(TempGainTileInfo(false)); return; }
            if (hitName == "hud-tile-self-time") { SetHoverInfo("己方剩余操作时间\n（引擎操作计时未接线——占位显示）"); return; }

            // 施放占格立牌（2026-10-15 发动格拆除）：发动中的卡=公开信息——四段卡信息直出
            if (hitName.StartsWith(BattleFieldView.CastingCardPrefix, System.StringComparison.Ordinal))
            {
                var casting = CastingCardAt(hitName);
                if (casting != null) SetHoverInfo(CardHoverInfo(casting));
                else ClearHover();
                return;
            }

            int hx, hz;
            if (!TryParseTile(hitName, out hx, out hz)) { ClearHover(); return; }

            var role = BoardLayout.RoleOf(hx, hz);
            int ownerIdx = BoardLayout.OwnerOf(hx, hz);
            if (role == CellRole.Character)
            {
                // 角色格（2026-10-16 技能格撤销）：角色信息（生命/弹药）；
                // 技能介绍已迁【发动】键悬停（对手格无键=介绍留此兜底）
                SetHoverInfo(HeroHoverInfo(ownerIdx));
                return;
            }
            if (role == CellRole.Land)
            {
                SetHoverInfo(LandCellInfo(hx, hz, ownerIdx));
                return;
            }
            if (role != CellRole.Unit)
            {
                SetHoverInfo(ZoneCellInfo(role, ownerIdx));
                return;
            }

            var unit = UnitAt(hx, hz);
            if (unit != null && IsPreviewableUnit(unit))
            {
                SetHoverInfo(CardHoverInfo(unit));
                return;
            }
            ClearHover(); // 地牌/空格不出信息（2026-10-13 用户定案范围=生物+结界）
        }

        private void ClearHover()
        {
            if (_hoverKey == null) return;
            _hoverKey = null;
            if (_hoverInfo != null && _hoverInfo.gameObject.activeSelf) _hoverInfo.gameObject.SetActive(false);
        }

        /// <summary>场上卡悬停信息（2026-10-15 定案，ACard 预览随 prefab 节点删除退役）：只展示四段——
        /// 主动效果（带发动费用）/自动效果（带本回合生效次数）/关键词（带完整解说）/状态（指示物完整解说
        /// +受到的连接光环清单），空段整段省略。数据口径与手牌卡面（CardOverlayItem.FromHand）同链：
        /// 主动=TriggerTiming∈{Activate_Active, Activate_Instant}，其余（含响应式）归自动；
        /// 解说=KeywordEffectMapper/CounterText；光环=LinkAuraSystem.GetApplicableAuras 反查。</summary>
        private string CardHoverInfo(BattleCardView card)
        {
            var data = DataOf(card);
            string title = string.IsNullOrEmpty(card.Name)
                ? (string.IsNullOrEmpty(data?.CardName) ? card.CardId ?? "?" : data.CardName)
                : card.Name;
            var sections = new List<string> { title };

            var executor = card.CoreCard != null && GameCore.Instance != null
                ? GameCore.Instance.StackEngine.GetExecutor() : null;

            List<string> actives = null, autos = null;
            if (data?.Effects != null)
            {
                foreach (var fx in data.Effects)
                {
                    if (fx == null) continue;
                    var timing = (TriggerTiming)fx.TriggerTiming;
                    var text = AtomText.RenderEffectSummary(new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, data);
                    if (string.IsNullOrEmpty(text)) continue;
                    if (timing == TriggerTiming.Activate_Active || timing == TriggerTiming.Activate_Instant)
                    {
                        string cost = fx.AnchorCost != null && !fx.AnchorCost.IsZero
                            ? "（费用 " + BattleView.CostTextOf(fx.AnchorCost) + "）" : "";
                        (actives ??= new List<string>()).Add(text + cost);
                    }
                    else
                    {
                        (autos ??= new List<string>())
                            .Add(text + CardOverlayItem.UsageSuffix(fx, executor, card.CoreCard, "本回合 "));
                    }
                }
            }
            if (actives != null) sections.Add("【主动效果】\n" + string.Join("\n", actives));
            if (autos != null) sections.Add("【自动效果】\n" + string.Join("\n", autos));

            // 关键词：运行态优先（含授予/净化合并），回落印刷；完整解说=关键词表 description
            var keywords = card.Runtime?.Keywords ?? data?.Keywords?.ToArray();
            if (keywords != null && keywords.Length > 0)
            {
                var lines = new List<string>();
                foreach (var kw in keywords)
                {
                    if (string.IsNullOrEmpty(kw)) continue;
                    string desc = KeywordEffectMapper.GetDescription(kw);
                    lines.Add(string.IsNullOrEmpty(desc)
                        ? KeywordEffectMapper.GetNameZh(kw)
                        : $"{KeywordEffectMapper.GetNameZh(kw)}：{desc}");
                }
                if (lines.Count > 0) sections.Add("【关键词】\n" + string.Join("\n", lines));
            }

            // 状态：运行态指示物（层≠0 且剔内部读数层；完整解说=CounterText 含生命周期）+ 受到的连接光环
            //（2026-10-15 补：live-query 反查来源，与加成计算同判定链；光环关键词不物化不入 _keywords，
            // 故与上方关键词段不重复；同源同增益叠加计 ×N——箭头叠加计数语义）
            var statusLines = new List<string>();
            // 守护/舍身配对（2026-10-16 定案）：发动结成后在状态段书写当前绑定对象——
            // 守护=FindWard 正查（配对在即展示，被守护者死亡不清链）；舍身=FindVictim 活性口
            //（目标死亡期间伤害不改写，状态同样不报，复活即复显）。
            if (card.CoreCard != null)
            {
                var ward = GuardianRules.FindWard(card.CoreCard);
                if (ward != null) statusLines.Add("当前在守护：" + HoverEntityName(ward));
                var victim = MartyrdomRules.FindVictim(card.CoreCard);
                if (victim != null) statusLines.Add("当前在舍身：" + HoverEntityName(victim));
            }
            if (card.Runtime?.Counters != null)
            {
                foreach (var c in card.Runtime.Counters)
                {
                    if (c == null || c.Value == 0) continue;
                    var spec = CardCore.Attribute.CounterRules.Find(c.Key);
                    if (spec != null && spec.LayerRole == CardCore.Attribute.CounterLayerRole.Reading) continue;
                    statusLines.Add(CounterText.Line(c.Key, c.Value));
                }
            }
            if (card.CoreCard != null)
            {
                var grants = LinkAuraSystem.GetApplicableAuras(card.CoreCard)
                    .Select(a => (name: CardNameOf(a.source), grant: AuraGrantText(a.aura)))
                    .Where(t => !string.IsNullOrEmpty(t.grant))
                    .GroupBy(t => (t.name, t.grant))
                    .Select(g => g.Count() > 1 ? $"{g.Key.name}：{g.Key.grant}×{g.Count()}" : $"{g.Key.name}：{g.Key.grant}")
                    .ToList();
                if (grants.Count > 0) statusLines.Add("受光环｜" + string.Join("；", grants));
            }
            if (statusLines.Count > 0) sections.Add("【状态】\n" + string.Join("\n", statusLines));

            return string.Join("\n", sections);
        }

        /// <summary>引擎卡显示名（光环来源行用）：CardWrapper 数据面卡名，查表兜底。</summary>
        private static string CardNameOf(Card core)
        {
            var name = (core as CardWrapper)?.GetData()?.CardName;
            if (!string.IsNullOrEmpty(name)) return name;
            var data = string.IsNullOrEmpty(core.ID) ? null : CardCatalog.GetById(core.ID);
            return string.IsNullOrEmpty(data?.CardName) ? core.ID : data.CardName;
        }

        /// <summary>状态行实体名（守护/舍身绑定对象）：卡=CardNameOf 同链；角色=【己方/对方角色】
        ///（RegionTargetSelector 候选同口径：本地局人类恒 P1）。</summary>
        private string HoverEntityName(Entity e)
        {
            if (e is Card c) return CardNameOf(c);
            if (e is Player p) return p == P1 ? "【己方角色】" : "【对方角色】";
            return e != null ? e.ToString() : "?";
        }

        /// <summary>光环条目授予文案（与计价明细 CardCostService 同词表）：属性±N/生命±N/攻击±N/关键词中文短名。</summary>
        private static string AuraGrantText(LinkAuraData aura)
        {
            if (!string.IsNullOrEmpty(aura.stat))
            {
                bool isLife = aura.stat.Equals("Life", StringComparison.OrdinalIgnoreCase);
                bool isBoth = aura.stat.Equals("Both", StringComparison.OrdinalIgnoreCase);
                return $"{(isBoth ? "属性" : isLife ? "生命" : "攻击")}{(aura.value >= 0 ? "+" : "")}{aura.value}";
            }
            if (!string.IsNullOrEmpty(aura.keyword))
                return KeywordEffectMapper.GetNameZh(aura.keyword);
            return null;
        }

        /// <summary>悬停信息文本（文本未变不重写）。</summary>
        private void SetHoverInfo(string text)
        {
            if (string.IsNullOrEmpty(text)) { ClearHover(); return; }
            if (_hoverKey == "info:" + text) return;
            _hoverKey = "info:" + text;
            if (_hoverInfo == null) return;
            if (_hoverInfoLabel.text != text) _hoverInfoLabel.text = text;
            if (!_hoverInfo.gameObject.activeSelf) _hoverInfo.gameObject.SetActive(true);
        }

        private static bool TryParseTile(string hitName, out int x, out int z)
        {
            x = z = -1;
            if (string.IsNullOrEmpty(hitName) || !hitName.StartsWith("tile-")) return false;
            var parts = hitName.Split('-');
            return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z);
        }

        /// <summary>格位反查战场单位（双方）。</summary>
        private BattleCardView UnitAt(int x, int z)
        {
            return (_view.SelfUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead)
                   ?? (_view.OppUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
        }

        /// <summary>悬停信息范围（2026-10-13 用户定案）：生物+结界；查表 miss（无类型数据）保守放行。</summary>
        private static bool IsPreviewableUnit(BattleCardView unit)
        {
            var data = DataOf(unit);
            return data == null || data.Supertype == Cardtype.Creature || data.Supertype == Cardtype.Enchantment;
        }

        // ---- 悬停信息文案 ----

        private string EndTurnTileInfo()
        {
            if (_view == null) return "结束回合";
            string state = _view.MyTurn
                ? (_view.Phase == PhaseType.Main ? "当前可结束——点击推进回合" : $"当前为{BattleView.PhaseZh(_view.Phase)}阶段（主要阶段才可操作）")
                : "对手回合进行中…";
            return "结束回合\n" + _view.PhaseText + "\n" + state;
        }

        /// <summary>阶段显示格悬停（2026-10-14 定案：中线 x0 平贴当前阶段文字——本格纯展示，点击只收弹窗）。</summary>
        private string PhaseTileInfo()
        {
            if (_view == null) return "阶段显示";
            return "阶段显示\n" + _view.PhaseText + "\n回合结构：准备 → 主要 → 结束";
        }

        /// <summary>阶段显示格文字（【己方主要阶段】式——BattleHudRow.SetPhase 喂文，8 字 2.6 号贴格宽）。</summary>
        private static string PhaseTileText(BattleViewData d)
            => $"【{(d.MyTurn ? "己方" : "对手")}{BattleView.PhaseZh(d.Phase)}阶段】";

        /// <summary>格前三键悬停解说（2026-10-15：三键上盘后无解说补齐；2026-10-16 角色格同款三键）。
        /// 灰态原因与 ReconcileTileButtons 高亮门禁同源；深判定（费用/横置代价/耐久）留引擎裁决不预写。</summary>
        private string TileButtonHoverInfo(string btnKind, string tileName)
        {
            string who = "";
            bool heroCell = false;
            int x, z;
            if (TryParseTile(tileName, out x, out z))
            {
                var u = UnitAt(x, z);
                if (u != null) who = $"（{u.Name}）";
                else if (x == BoardLayout.P1CharX && z == BoardLayout.P1CharZ) { heroCell = true; who = "（角色）"; }
            }
            if (heroCell)
            {
                int ammo = P1 != null ? P1.GetPower() : 0;
                switch (btnKind)
                {
                    case "Attack":
                        return $"角色攻击{who}\n点击发起角色攻击：拉箭头选目标（对方单位或角色）后宣言。\n"
                             + $"白=有攻击弹药（当前 {ammo}）且己方主要阶段；灰=弹药 0 或不可操作。\n"
                             + "攻击结算后弹药全部烧光（与反伤共用同一份）。";
                    case "Defense":
                        return $"角色守卫{who}\n响应动作：对手攻击宣言后的守卫窗口内，点角色格（或本键）选中角色为守卫者，"
                             + "再点攻击者确认——拦截=攻击目标改写为角色，承受战斗并以弹药反伤（反伤后弹药烧光）。\n窗口外恒灰。";
                    default: // Activate（2026-10-16 晚迁移：技能四段介绍从角色格悬停挪到本键）
                    {
                        var skill = _view?.Self?.Skill;
                        string skillText = skill != null ? CardHoverInfo(skill) : "本局无技能卡";
                        return $"发动英雄技能{who}\n点击发动英雄主动技能（多个效果弹选择窗；费用/每回合一次由引擎在发动时裁决）。\n"
                             + "白=有技能且己方主要阶段；灰=无技能或不可操作。\n"
                             + skillText;
                    }
                }
            }
            switch (btnKind)
            {
                case "Attack":
                    return $"攻击{who}\n点击宣告该生物发起攻击：拉箭头选目标后宣言。\n"
                         + "白=此刻可宣告；灰=不可用（需己方主要阶段、未横置且攻击力>0）。";
                case "Defense":
                    return $"守卫{who}\n响应动作：对手攻击宣言后的守卫窗口内，点击该生物选中为守卫者（再点攻击者确认拦截，右键/ESC 让过）。\n"
                         + "窗口外恒灰——守卫不可主动发动。";
                default: // Activate
                    return $"发动主动效果{who}\n点击发动该单位的主动效果（多个效果弹选择窗；费用/横置/耐久由引擎在发动时裁决）。\n"
                         + "白=有主动效果且己方主要阶段；灰=当前无主动效果可发动。";
            }
        }

        /// <summary>特殊格悬停信息（数量+点击引导；技能格/上限格/中线=2026-10-14 改版新增）。</summary>
        private string ZoneCellInfo(CellRole role, int ownerIdx)
        {
            var owner = ownerIdx == 0 ? P1 : P2;
            string who = owner == P1 ? "我方" : "对手";
            var pv = owner == P1 ? _view?.Self : _view?.Opp;
            switch (role)
            {
                case CellRole.Graveyard:
                    return $"{who}墓地 · {pv?.GraveyardCount ?? 0} 张\n点击查看";
                case CellRole.Exile:
                    return $"{who}除外区 · {ZoneCount(owner, Zone.Exile)} 张\n点击查看";
                case CellRole.Deck:
                    return $"{who}卡组 · 剩 {pv?.DeckCount ?? ZoneCount(owner, Zone.Deck)} 张（隐藏）";
                // CellRole.Skill 已随技能格撤销（2026-10-16）退役：技能介绍=己方【发动】键悬停（对手=角色格悬停）
                case CellRole.Middle:
                    return null; // 中线格全数认领为功能格（2026-10-16 产量双格补 lx2/lx4 后无空格）——已改名 hud-tile-*，走各自悬停分支
                default:
                    return null;
            }
        }

        /// <summary>角色格悬停信息（2026-10-16 技能格撤销改版；当晚迁移）：角色本体（生命/护甲/攻击弹药）。
        /// 己方技能四段介绍已迁到格前【发动】键悬停（TileButtonHoverInfo）；对手角色格无键可悬，
        /// 技能是公开信息——介绍留在此处兜底。无技能局=占位行。</summary>
        private string HeroHoverInfo(int ownerIdx)
        {
            var owner = ownerIdx == 0 ? P1 : P2;
            var pv = ownerIdx == 0 ? _view?.Self : _view?.Opp;
            string who = ownerIdx == 0 ? "我方" : "对手";
            int life = pv?.Life ?? owner?.Life ?? 0;
            int armor = pv?.Armor ?? 0;
            int ammo = owner != null ? owner.GetPower() : 0;
            var lines = new List<string>
            {
                $"{who}角色 · {owner?.Name ?? "?"}　生命 {life}{(armor > 0 ? $"（护甲+{armor}）" : "")}　攻击弹药 {ammo}",
            };
            if (ownerIdx == 0)
                lines.Add("操作=格前三键：攻击/守卫（需攻击弹药）、发动技能（技能详情=【发动】键悬停）");
            else
            {
                var skill = pv?.Skill;
                if (skill != null) lines.Add(CardHoverInfo(skill));
                else lines.Add("本局无技能卡");
            }
            return string.Join("\n", lines);
        }

        /// <summary>角色【发动】键高亮门禁（2026-10-16 三键改版）：有技能卡且挂主动效果；
        /// 深判定（费用/每回合一次/阶段）留引擎在发动时裁决，拒绝走点击 toast。</summary>
        private bool HeroHasActivatableSkill()
            => HeroSkillSystem.SkillEffectsOf(HeroSkillSystem.ResolveSkillCard(Core, P1)).Count > 0;

        private int ZoneCount(Player owner, Zone zone)
            => Core != null ? (Core.ZoneManager.GetCards(owner, zone)?.Count ?? 0) : 0;

        /// <summary>地牌格悬停信息（2026-10-14 晚固定元素格）：格身份=LandCellElementOrder 序（灰红蓝绿黑白）；
        /// 计数报「可用/上限」（可用=未横置+钱包该色；黑白=钱包余额；
        /// 上限=红蓝绿灰该色已放入地牌张数、黑白格只显余量——2026-10-16 全类型累计钳改案）。</summary>
        private string LandCellInfo(int x, int z, int ownerIdx)
        {
            var pv = ownerIdx == 0 ? _view?.Self : _view?.Opp;
            var lands = ownerIdx == 0 ? _view?.SelfLands : _view?.OppLands;
            // 固定元素身份（玩家视角左→右 灰红蓝绿黑白）
            var cells = BoardLayout.LandCells(ownerIdx);
            ManaType element = ManaType.Gray;
            for (int i = 0; i < cells.Count && i < BoardLayout.LandCellElementOrder.Length; i++)
                if (cells[i].x == x && cells[i].z == z)
                    element = BoardLayout.LandCellElementOrder[i];
            bool isBw = element == ManaType.White || element == ManaType.Black;
            string head = $"{(ownerIdx == 0 ? "我方" : "对手")}地牌 · {BattleView.ManaZh(element)}元素格";
            var toks = new List<string>();
            if (lands != null)
            {
                foreach (var g in lands.Where(c => c.X == x && c.Z == z && c.Runtime != null)
                                       .GroupBy(c => c.Runtime.LockedColor))
                {
                    int untapped = g.Count(c => !c.IsTapped);
                    int wallet = pv != null && g.Key >= 0 && g.Key < pv.WalletByColor.Length ? pv.WalletByColor[g.Key] : 0;
                    string color = g.Key >= 0 ? BattleView.ManaZh((ManaType)g.Key) : "?";
                    toks.Add($"{color} {untapped + wallet}/{g.Count()}");
                }
            }
            // 地牌上限显示迁移（2026-10-14 晚：上限格退役）——地牌格悬停就地报上限（本批措辞定案：【】值域）
            string capLine = $"当前地牌上限【{pv?.LandCap ?? 0}】（每回合开始双方同步加一，封顶10；效果可再提升）";
            if (isBw)
                return $"{head}（黑白不产地牌，只进钱包）\n使用对自己有害的代价产生\n"
                     + $"全类型累计生成钳：所有颜色临时元素每周期（自己回合开始清零）合计不超过当前地牌上限【{pv?.LandCap ?? 0}】——产量看中线「己方/对方临时」格\n"
                     + $"钱包余额 {(pv != null ? pv.WalletByColor[(int)element] : 0)}（黑白格面数字=余量，不显上限）";
            return toks.Count == 0
                ? $"{head}（暂无该色地牌）\n{capLine}"
                : $"{head}\n{string.Join("\n", toks)}\n可用=未横置+钱包该色，上限=该色地牌张数；付费时自动横置\n{capLine}";
        }

        // ---- 点击路由 ----

        /// <summary>施放立牌反查（cast-{RuntimeId} 命中名 → 视图数据施放占格卡；未命中=null）。</summary>
        private BattleCardView CastingCardAt(string hitName)
        {
            if (string.IsNullOrEmpty(hitName)
                || !hitName.StartsWith(BattleFieldView.CastingCardPrefix, System.StringComparison.Ordinal))
                return null;
            if (!uint.TryParse(hitName.Substring(BattleFieldView.CastingCardPrefix.Length), out uint rid))
                return null;
            var cards = _view?.CastingCards;
            if (cards == null) return null;
            foreach (var c in cards)
                if (c != null && c.RuntimeId == rid) return c;
            return null;
        }

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
                    CloseActionPopup();
                    CloseRegion();
                    if (btnKind == "Attack") OnClickSelfHero();
                    else if (btnKind == "Activate") OnClickHeroZone();
                    else ShowToast("守卫在对手攻击宣言后的守卫窗口内可用（白键=角色可拦截）");
                    return;
                }
                var btnUnit = hasTile
                    ? (_view.SelfUnits ?? new List<BattleCardView>())
                        .FirstOrDefault(c => c != null && c.X == bx && c.Z == bz && !c.IsDead)
                    : null;
                if (btnKind == "Attack")
                {
                    CloseRegion();
                    if (btnUnit == null) { CloseActionPopup(); return; } // 键下单位已离场（对账竞态）
                    ShowUnitActions(btnUnit, bx, bz); // 门禁/教学/横置/攻0 拒绝 toast 全沿用
                    return;
                }
                if (btnKind == "Activate")
                {
                    CloseActionPopup();
                    CloseRegion();
                    if (btnUnit != null) OnClickUnitEffectButton(btnUnit).Forget();
                    return;
                }
                // Defense：守卫窗口开启时点击被 HandleGuardPointer 消费（点键=点格）；
                // 窗口未开=守卫是响应动作，引导即可
                CloseActionPopup();
                CloseRegion();
                ShowToast("守卫在对手攻击宣言后的守卫窗口内可用（白键=该生物可拦截）");
                return;
            }

            // 功能地块（BattleHudRow 中线状态行：结束回合格可点；两时间/连锁速度格纯展示——
            // 未认领名走 TryParseTile 失败分支收弹窗，无需另接点击）
            if (hitName == "hud-tile-end-turn")
            {
                Debug.Log("[HUD地块] 点击：结束回合");
                CloseActionPopup();
                CloseRegion();
                if (IsTargetSelecting()) return; // Region 抉择/反问在身：先完成选择再推进（非模态须自防重入）
                // 仅己方回合可点（2026-10-14 定案）：对手回合就地提示，不再静默吞（引擎闸仍在 OnEndTurnFan 兜底）
                if (Core == null || _ctrl == null || _ctrl.TurnPlayer != P1)
                {
                    ShowToast("对手回合进行中，暂不可结束回合");
                    return;
                }
                OnEndTurnFan();
                return;
            }

            // 施放占格立牌（2026-10-15 发动格拆除）：点击=该方发动区列卡（Region 面板——反制指向/查看入口，
            // 原发动格点击入口随格拆除迁移至此）
            var casting = CastingCardAt(hitName);
            if (casting != null)
            {
                CloseActionPopup();
                OpenRegion(RegionKind.Activation, BoardLayout.OwnerOf(casting.X, casting.Z) == 0 ? P1 : P2);
                return;
            }

            int x, z;
            if (!TryParseTile(hitName, out x, out z)) { CloseActionPopup(); CloseRegion(); return; }

            // 特殊格分流（两侧列 x0/x7 不承载单位/地牌——BoardLayout 保证）
            var role = BoardLayout.RoleOf(x, z);
            if (role != CellRole.Unit && role != CellRole.Land && role != CellRole.Edge)
            {
                CloseActionPopup();
                HandleZoneCellClick(role, BoardLayout.OwnerOf(x, z));
                return;
            }

            // 单位格体=展示信息（悬停已出四段卡信息）：只收弹窗，操作走格前三键
            var unit = (_view.SelfUnits ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
            if (unit != null) { CloseActionPopup(); CloseRegion(); return; }

            var land = (_view.SelfLands ?? new List<BattleCardView>()).FirstOrDefault(c => c.X == x && c.Z == z);
            if (land != null) { CloseActionPopup(); OnClickMyLand(land); return; }
            CloseActionPopup();
            CloseRegion();
        }

        /// <summary>特殊格点击：墓地/除外/发动区/卡组/临时元素→Region 面板；角色格→技能详情+操作组。</summary>
        private void HandleZoneCellClick(CellRole role, int ownerIdx)
        {
            var owner = ownerIdx == 0 ? P1 : P2;
            if (owner == null) return;
            switch (role)
            {
                case CellRole.Graveyard: OpenRegion(RegionKind.Graveyard, owner); break;
                case CellRole.Exile: OpenRegion(RegionKind.Exile, owner); break;
                // CellRole.Activation 已随发动格拆除（2026-10-15）退役：Region 发动区入口迁至
                // 点击施放立牌（cast-{rid} 路由——见 HandleFieldClick）
                case CellRole.Deck: OpenRegion(RegionKind.Deck, owner); break;
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
            CloseActionPopup();
            Debug.Log(owner == P1
                ? "[角色格] 我方角色格——操作走格前三键（悬停可看角色与技能介绍）"
                : "[角色格] 查看对方角色（悬停出信息）");
        }

        /// <summary>格前【攻击】键入口（2026-10-15 三键改版；原「点单位格直达」链路原样沿用）：
        /// 主阶段直达 3D 箭头选目标（横置/攻 0 门禁在 OnClickMyUnit 内 toast）；
        /// 结界等非生物单位无攻击宣言。教学期门禁照旧。</summary>
        private void ShowUnitActions(BattleCardView card, int x, int z)
        {
            CloseActionPopup();
            if (IsTargetSelecting()) return; // 选择期点击=目标语义（两语义裁定）
            if (!GateMyMain()) return;       // 非我方主阶段（GateMyMain 自带 toast）
            // 教学期前置提示（引擎闸仍兜底——闸一直都在，这里把拒绝原因提前到点击时可见）
            var step = TutorialGuide.Active ? TutorialGuide.Current : null;
            if (step != null)
            {
                if (step.type == "attack" && step.attacker != card.CardId)
                {
                    ShowToast($"引导要求用 {TutorialGuide.NameOf(step.attacker)} 攻击（{card.Name} 暂不可操作）");
                    return;
                }
                if (step.type == "guard") { ShowToast(step.hint ?? "生物保留待守卫"); return; }
                if (step.type == "end") { ShowToast(step.hint ?? "引导要求结束回合"); return; }
            }
            if (!IsCreatureCard(card)) { ShowToast("该单位无法攻击"); return; }
            Debug.Log($"[BattleScreen] 攻击键点击：{card.Name}（{x},{z}）→ 攻击目标选择");
            OnClickMyUnit(card);
        }

        // ---- 格前三键对账（2026-10-15 定案：攻击/守卫/发动拆到格前，高亮=可用） ----

        private readonly List<BattleTileButtons.Entry> _tileBtnBuf = new List<BattleTileButtons.Entry>();

        /// <summary>三键每帧对账（幂等差量，BattleTileButtons 落盘）：编舞选目标/放格选格/网络局=全收；
        /// 守卫窗口=守卫候选亮蓝（窗口独占，攻/发键暗灰）；常态=己方单位格——攻击=生物未横置攻&gt;0
        /// 且我方主阶段、发动=有主动效果且我方主阶段、守卫=仅窗口内亮。
        /// 角色格（2026-10-16 同款三键）：攻击=有弹药且我方主阶段、发动=技能挂主动效果且我方主阶段、
        /// 守卫=窗口内且角色在候选（弹药&gt;0）。
        /// 深判定（费用/横置代价/耐久/每回合一次）留引擎裁决，高亮不预判，拒绝走点击 toast。</summary>
        private void ReconcileTileButtons()
        {
            if (IsNetwork || _view == null || !BattleBoardGrid.Ready
                || (_choreo != null && _choreo.IsTargeting)
                || (_cellPicker != null && _cellPicker.Active))
            {
                BattleTileButtons.Apply(null);
                return;
            }

            bool myMain = !_gameEnded && _view.MyTurn && _view.Phase == PhaseType.Main && !IsTargetSelecting();
            _tileBtnBuf.Clear();
            var units = _view.SelfUnits;
            if (units != null)
            {
                foreach (var c in units)
                {
                    if (c == null || c.IsDead || c.CoreCard == null || c.X < 0 || c.Z < 0) continue;
                    var e = new BattleTileButtons.Entry { X = c.X, Z = c.Z };
                    if (_guardTcs != null)
                        e.Guard = _guardOptions != null
                                  && _guardOptions.Exists(o => o != null && ReferenceEquals(o.SourceCard, c.CoreCard));
                    else
                    {
                        e.Attack = myMain && IsCreatureCard(c) && !c.IsTapped
                                   && (c.Runtime == null || c.Runtime.Power > 0);
                        e.Activate = myMain && UnitActivatedEffects(c).Count > 0;
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
                if (_guardTcs != null)
                    hero.Guard = _guardOptions != null
                                 && _guardOptions.Exists(o => o != null && ReferenceEquals(o.SourcePlayer, P1));
                else
                {
                    hero.Attack = myMain && P1.GetPower() > 0;
                    hero.Activate = myMain && HeroHasActivatableSkill();
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
        /// 单效果直达发动，多效果 Region 卡牌列表择一（2026-10-16 弹窗退役）；目标不在此弹——
        /// 引擎结算期反问经 TargetSelectionService（Current=RegionTargetSelector）弹 Region 选卡面板。
        /// 门禁时点/优先权/费用/横置/耐久全在 GameActions.ActivateEffect 裁决，拒绝 toast。</summary>
        private async UniTaskVoid OnClickUnitEffectButton(BattleCardView card)
        {
            if (card?.CoreCard == null) return;
            if (IsTargetSelecting()) return; // 选择期点击=目标语义（两语义裁定）
            if (!GateMyMain()) return;
            var defs = UnitActivatedEffects(card);
            if (defs.Count == 0) return;
            if (defs.Count == 1) { ActivateUnitEffect(card, defs[0]); return; }

            var options = new List<string>(defs.Count);
            for (int i = 0; i < defs.Count; i++)
                options.Add(string.IsNullOrEmpty(defs[i].DisplayName) ? $"主动效果 {i + 1}" : defs[i].DisplayName);
            int? picked = await PickRegionOptionAsync("选择效果", options);
            if (picked == null) return;
            if (picked.Value >= 0 && picked.Value < defs.Count) ActivateUnitEffect(card, defs[picked.Value]);
        }

        /// <summary>发动单位主动效果（本地局）：入栈后无响应候选自动双 Pass（技能发动同款）；
        /// 生物横置/结界耐久消耗随 RefreshField 对账刷新。</summary>
        private void ActivateUnitEffect(BattleCardView card, EffectDefinition def)
        {
            Debug.Log($"[BattleScreen] 单位效果发动：{card.Name}（{card.CardId}）→ {def.DisplayName ?? def.Id}");
            bool ok = GameActions.ActivateEffect(Core, P1, def, card.CoreCard);
            if (!ok) ShowToast("无法发动（费用/横置/耐久/阶段门禁）");
            else _ctrl.SettleResponseWindow().Forget();
            RefreshField();
        }

        // 操作按钮组（角色格）已随 2026-10-16 三键改版退役：角色操作=格前三键（Attack/Defense/
        // Activate，与生物同款）；CloseActionPopup 保留为既有收口调用的空防（_actionPopup 恒 null）。

        private void CloseActionPopup()
        {
            if (_actionPopup != null) { UnityEngine.Object.Destroy(_actionPopup.gameObject); _actionPopup = null; }
        }

        // ======================================== Region 区域面板（2026-10-13 基础教学：区域按钮上盘——点特殊格列卡） ========================================

        /// <summary>面板当前展示的区域。</summary>
        private enum RegionKind { None, Graveyard, Exile, Activation, Deck }

        private RegionKind _regionKind;
        private Player _regionOwner;
        private RectTransform _regionPanel;
        private RegionTargetSelector _regionSelector; // 结算期反问选择器（网络反问复用；null=自动代选）
        private TMP_Text _regionTitle;
        private UiKit.Scroll _regionGrid;   // 列卡网格（墓地/除外/发动区）
        private RectTransform _regionLines; // 文本行（卡组数量/临时元素钱包）
        private GameObject _regionCardPrefab;

        /// <summary>Region 面板骨架（prefab 用户节点：标题+关闭+卡片网格/文本行两态；默认隐藏）。</summary>
        private void BuildRegionPanel()
        {
            _regionPanel = FindOptional("Region");
            if (_regionPanel == null) return;

            var img = _regionPanel.GetComponent<Image>();
            if (img != null) img.color = new Color(0.07f, 0.08f, 0.12f, 0.92f); // 白底改深衬（文字可读）

            var vg = _regionPanel.GetComponent<VerticalLayoutGroup>()
                     ?? _regionPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            vg.spacing = 8f;
            vg.padding = new RectOffset(12, 12, 10, 10);
            vg.childControlWidth = true;
            vg.childControlHeight = true;
            vg.childForceExpandWidth = false;
            vg.childForceExpandHeight = false;
            vg.childAlignment = TextAnchor.UpperLeft;

            var titleRow = UiKit.Row("title", _regionPanel, spacing: 8f);
            _regionTitle = UiKit.Label("lbl", titleRow, "", UiStyle.BodySize, UiStyle.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.Size(_regionTitle, fw: 1f);
            UiKit.Button("btn-close", titleRow, "关闭", CloseRegion, height: 30f);

            _regionLines = UiKit.Column("lines", _regionPanel, spacing: 6f);
            UiKit.Size(_regionLines, fw: 1f, fh: 1f);
            _regionLines.gameObject.SetActive(false);

            _regionGrid = UiKit.ScrollGrid("cards", _regionPanel, cellW: 232f, cellH: 296f, spacing: 10f);
            UiKit.Size(_regionGrid.Rect.transform, fw: 1f, fh: 1f);

            // 选择三件（label-num/btn-select/viewport）不参与浏览态 VLG 排版（保持 prefab 摆位；
            // viewport 拉伸锚点被 VLG 接管则横滑面崩坏）；选择面由 RegionTargetSelector 自管显隐
            foreach (var kid in new[] { "label-num", "btn-select", "viewport" })
            {
                var t = _regionPanel.Find(kid);
                if (t == null) continue;
                var le = t.GetComponent<LayoutElement>();
                if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
            }

            _regionPanel.gameObject.SetActive(false);
        }

        /// <summary>Region 选择面三件（label-num/btn-select/viewport）显隐——浏览态全藏；
        /// 选择面打开时由 RegionTargetSelector 自显（节点名与 prefab 单源）。</summary>
        private void SetRegionSelectNodes(bool visible)
        {
            if (_regionPanel == null) return;
            foreach (var kid in new[] { "label-num", "btn-select", "viewport" })
            {
                var t = _regionPanel.Find(kid);
                if (t != null && t.gameObject.activeSelf != visible) t.gameObject.SetActive(visible);
            }
        }

        private void OpenRegion(RegionKind kind, Player owner)
        {
            if (RegionTargetSelector.IsOpen) return; // 目标选择进行中：Region 被选择面占用
            if (_regionPanel == null) { Debug.Log($"[Region] 面板节点缺失，无法展示 {kind}"); return; }
            _regionKind = kind;
            _regionOwner = owner;
            _regionPanel.gameObject.SetActive(true);
            RefreshRegion();
        }

        private void CloseRegion()
        {
            if (_regionPanel == null || !_regionPanel.gameObject.activeSelf) return;
            if (RegionTargetSelector.IsOpen) return; // 目标选择进行中（弃牌等反问占用选择面）：关面板=窗口蒸发
            _regionPanel.gameObject.SetActive(false); // 而选择请求悬空干等超时（2026-10-15 冻结）；Open/Refresh 同款闭流
            _regionKind = RegionKind.None;
            _regionOwner = null;
            Debug.Log("[Region] 关闭");
        }

        /// <summary>面板内容重列（打开时/区域变化刷新链调用；未开着零开销）。</summary>
        private void RefreshRegion()
        {
            if (_regionPanel == null || RegionTargetSelector.IsOpen
                || !_regionPanel.gameObject.activeSelf || _regionOwner == null) return;
            SetRegionSelectNodes(false); // 浏览态藏选择三件（label-num/btn-select/viewport）
            string who = _regionOwner == P1 ? "我方" : "对手";
            bool ownerIsOpp = _regionOwner != P1;

            Zone? zone = _regionKind switch
            {
                RegionKind.Graveyard => Zone.Graveyard,
                RegionKind.Exile => Zone.Exile,
                RegionKind.Activation => Zone.Activation,
                _ => null,
            };
            if (zone.HasValue)
            {
                var cards = Core != null ? Core.ZoneManager.GetCards(_regionOwner, zone.Value) : null;
                string zoneName = _regionKind == RegionKind.Graveyard ? "墓地" : _regionKind == RegionKind.Exile ? "除外区" : "发动区";
                _regionTitle.text = $"{who}{zoneName} · {cards?.Count ?? 0} 张";
                FillRegionCards(cards, ownerIsOpp);
                Debug.Log($"[Region] {who}{zoneName} 列卡 {cards?.Count ?? 0} 张");
                return;
            }
            if (_regionKind == RegionKind.Deck)
            {
                int n = ZoneCount(_regionOwner, Zone.Deck);
                _regionTitle.text = $"{who}卡组";
                FillRegionLines(new List<string> { $"剩余 {n} 张", "（隐藏区域——不展示卡面）" });
                Debug.Log($"[Region] {who}卡组数量行：{n} 张");
            }
            // 临时元素 Region 面板（2026-10-14 技能格定案退役）：黑白钱包=技能格数字条、
            // 彩色钱包余额=地牌格「剩余」并入显示，不再有独立列表面板
        }

        /// <summary>列卡：ACard 实例按次重建（手牌同口径 FromRuntime→FromHand；自持实例不入全局池）。</summary>
        private void FillRegionCards(List<Card> cards, bool ownerIsOpp)
        {
            _regionLines.gameObject.SetActive(false);
            _regionGrid.Rect.gameObject.SetActive(true);
            ClearChildren(_regionGrid.Content);
            if (cards == null || cards.Count == 0) return;
            if (_regionCardPrefab == null)
                _regionCardPrefab = HotUpdateAssets.Load<GameObject>("ACard", UiKit.PrefabPath("ACard"));
            if (_regionCardPrefab == null)
            {
                Debug.LogError("[Region] ACard.prefab 缺失——无法列卡");
                return;
            }
            int i = 0;
            foreach (var c in cards)
            {
                var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(c), c, ownerIsOpp);
                var item = CardOverlayItem.FromHand(view);
                var go = UnityEngine.Object.Instantiate(_regionCardPrefab, _regionGrid.Content, false);
                go.name = "region-card-" + i++;
                var cv = go.AddComponent<ACardView>();
                cv.Bind();
                cv.Apply(item, CardOverlayLayout.Full);
            }
        }

        private void FillRegionLines(List<string> lines)
        {
            _regionGrid.Rect.gameObject.SetActive(false);
            _regionLines.gameObject.SetActive(true);
            ClearChildren(_regionLines);
            foreach (var line in lines)
            {
                var lbl = UiKit.Label("line", _regionLines, line, UiStyle.SmallSize, UiStyle.TextBody,
                    TextAnchor.MiddleLeft, wrap: true);
                UiKit.Size(lbl, fw: 1f);
            }
        }

        // TempWalletLines（临时元素 Region 面板行）：2026-10-14 技能格定案随 RegionKind.Temp 一并退役
        // ======================================== 战报面板开关（2026-10-13 基础教学：btn-log 切换 log-list 高度 0↔700） ========================================

        private const float LogListOpenHeight = 700f; // 展开定高（2026-10-16 晚改版：固定 0↔700，超出在面板内滚动）
        private const float LogListSpeed = 3500f; // px/s（700px≈0.2s 展开）

        private void ToggleLogList()
        {
            _logListTarget = _logListTarget > 0f ? 0f : LogListOpenHeight;
            Debug.Log($"[BattleLog] btn-log 切换：目标高度 {_logListTarget:0}（展开={_logListTarget > 0f}）");
        }

        private void TickLogList()
        {
            if (_logListRect == null) return;
            float h = Mathf.MoveTowards(_logListRect.sizeDelta.y, _logListTarget, Time.deltaTime * LogListSpeed);
            if (Mathf.Approximately(h, _logListRect.sizeDelta.y)) return;
            _logListRect.sizeDelta = new Vector2(_logListRect.sizeDelta.x, h);
            if (_logListTarget > 0f && h >= _logListTarget - 0.5f) _logList?.ScrollToBottom();
        }

        /// <summary>吞掉点击（IPointerClickHandler 实现即截断向父级 Button 冒泡——展开的日志面板内
        /// 点击/拖拽不得触发 btn-log 收起）。</summary>
        private sealed class ClickSwallow : MonoBehaviour, IPointerClickHandler
        {
            public void OnPointerClick(PointerEventData e) { }
        }

        // ======================================== 英雄区（2026-10-13 区域变化：技能与角色同区） ========================================

        /// <summary>是否有进行中的目标选择/弹窗：本屏列表模态在身，或引擎结算期反问在途。
        /// <b>两语义裁定（2026-10-13 用户定案）：目标选择期点击角色=「把角色选为目标」；
        /// 主动阶段（我方主阶段·无待选）点击角色=「发动技能」。二者不得混流——选择期一切
        /// 角色点击先走目标语义，实体点选接线属 3D 层目标选择重做（当前列表模态自带遮罩挡点击）。</summary>
        private bool IsTargetSelecting()
        {
            if (RegionTargetSelector.IsOpen) return true;                // Region 选择面在身（反问/响应窗/选项抉择——2026-10-16 弹窗退役）
            if (TargetSelectionService.PendingRequests > 0) return true; // 引擎结算期反问在途
            if (_choreo != null && _choreo.IsTargeting) return true;     // 3D 寻路箭头选目标在途（2026-10-13）
            return false;
        }

        /// <summary>点英雄区（左键）：发动英雄技能（2026-10-13 区域变化定案——英雄技能与角色共用本区，
        /// 主动阶段选中英雄即发动技能）；未携带技能的局回落角色攻击。网络模式同旧技能按钮口径 toast 拒绝。
        /// 目标选择期点击不走技能（见 IsTargetSelecting 两语义裁定）。</summary>
        private void OnClickHeroZone()
        {
            if (IsTargetSelecting()) return; // 选择期点击=目标语义，由选择器接管（模态遮罩本就挡住；防御性双保险）
            if (!GateFanMyMain()) return;
            if (IsNetwork) { ShowToast("网络模式英雄技能暂未开放"); return; }
            var skill = HeroSkillSystem.ResolveSkillCard(Core, P1);
            if (skill == null) { ShowToast("本局无技能卡"); return; } // 无技能局：发动键本就不亮（防直点兜底）
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
                ShowToast(_ctrl.TurnPlayer != P1 ? "对手回合，暂不可操作" : "当前不是主要阶段");
                return false;
            }
            return true;
        }

        // RefreshHeroZone/TempTokenColor（HUD 英雄区按钮+临时元素指示条刷新）：2026-10-14 定案随
        // btn-self-hero/temp-elements 一并退役——角色生命/弹药=角色格悬停、技能=角色格悬停+三键
        //（2026-10-16 技能格撤销后口径，黑白钱包=地牌行黑白元素格）。

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

        /// <summary>墓地使用（2026-10-16 弹窗退役：旧列表弹窗→Region 真卡面选牌，单选强制）。
        /// 选后多模式卡续接 PromptModeThenPlay（同 Region 择一），单模式直出。</summary>
        private async UniTaskVoid OnGraveyardPlay()
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
            if (_regionSelector == null) { ShowToast("Region 面板缺失——墓地使用不可用"); return; }

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
            else PlayCardFinal(view, null, 0, fromGrave: true);
        }

        // OnActivateSkill（多效果技能的旧列表弹窗链）已随 2026-10-16 弹窗退役整删：
        // 零调用方死代码（技能入口=OnClickHeroZone→ActivateSkillAsync，效果数>1 的抉择
        // 由引擎结算期反问/ActivateHeroSkill 链路承担）。

        private async UniTaskVoid ActivateSkillAsync(int effectIndex = 0)
        {
            bool ok = await GameActions.ActivateHeroSkill(Core, P1, effectIndex);
            if (!ok) ShowToast("无法发动技能（费用/本回合已用/阶段）");
            else _ctrl.SettleResponseWindow().Forget(); // 无候选自动双 Pass，无害
            RefreshField(); // 地牌横置/角色弹药随对账刷新（旧渲染链字段已不绑）
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

        /// <summary>投降（2026-10-16 弹窗退役：确认=Region 卡牌列表，「取消」置首——超时代选
        /// 只会落到取消，不会误投降）。终局后本键=返回主菜单（MarkGameOverUi 改文案）。</summary>
        private async UniTaskVoid OnConcede()
        {
            if (_gameEnded) { Manager.Back(); return; } // 终局：退出通道（旧弹窗取消键职责迁移）
            int? picked = await PickRegionOptionAsync("投降确认", new List<string> { "取消", "确认投降（本局判负）" });
            if (picked != 1) return; // 缺 Region/取消=不动
            if (IsNetwork)
                _net?.Send<object>(NetworkMessageType.IntentConcede, null);
            else
            {
                Core.EndGame(P2, GameOverReason.Concede);
                RefreshLocal();
            }
        }

        // ======================================== 本地响应窗口（发动弹窗·沿用旧语义） ========================================

        /// <summary>人类响应窗口（2026-10-16 深夜定案：按速度分档顺序询问）。流程：
        /// ①速度1档（守卫=速度1响应）先问——棋盘箭头交互；若响应：守卫入栈、泵翻转优先权问对手
        /// 跟响应，记速器升档后后续窗口天然只剩速度2+（引擎既有口径：非回合方响应须严格高于
        /// 连锁当前最高速度，攻击=速度0上栈）。②速度1让过——同轮降档问速度2+（诱发/手牌响应卡
        /// =Region 面板卡牌列表，min=0：不选+确定=让过；对手回合 btn-notchain 10 秒倒计时）。
        /// 两档皆让=Pass。旧全屏列表弹窗已整删（遮罩拦棋盘=守卫卡死根因）。</summary>
        private async UniTask<ResponseOption> ShowResponsePopupAsync(Player holder, List<ResponseOption> options)
        {
            var guards = options.Where(o => o.Kind == ResponseOption.ResponseKind.GuardAbility
                                            && (o.SourceCard != null || o.SourcePlayer != null)).ToList();
            var rest = options.Where(o => !guards.Contains(o)).ToList();
            _responsePassAll = false;
            // lbl-toast 定案（2026-10-16）：响应窗开=报「在响应什么」（守卫档另有 banner 攻击指令）；
            // ②档 Region 面板的 onPrompt 会以同源文本+择卡指引覆写，关窗/整窗收尾 finally 清空
            string srcText = DescribeResponseSource();
            ShowToast(srcText);
            try
            {
            // ① 速度1档：守卫棋盘（无倒计时——同纯守卫窗口口径；不连锁按钮=整窗两档同拒）
            if (guards.Count > 0)
            {
                var guardOpt = await BeginGuardPrompt(holder, guards, moreTiers: rest.Count > 0);
                if (guardOpt != null) return guardOpt; // 守卫入栈→泵问对手跟响应→后续窗口自然只剩速度2+
                if (_responsePassAll || rest.Count == 0) return null;
                Debug.Log("[响应窗口] 速度1让过——降档询问速度2+（Region）");
            }
            else if (rest.Count == 0)
                return null;

            // ② 速度2+档：Region 卡牌列表（缺 Region=无交互让过；守卫档已在此之前保住棋盘）
            if (_regionSelector == null) return null;
            var labels = new List<string>(rest.Select(o => o.Label));
            if (holder == P1 && (_view == null || !_view.MyTurn)) // 仅对手回合计时（超时=拒绝连锁）
                NotChainCountdownAsync(++_notChainSerial).Forget();
            else if (_btnNotChain != null) // 我方回合：无倒计时，不连锁按钮=显式让过口（守卫档收口已隐藏）
            {
                SetNotChainLabel("不连锁");
                _btnNotChain.gameObject.SetActive(true);
            }
            var picked = await _regionSelector.SelectIndicesAsync(labels, 0, 1, "响应窗口",
                srcText + "\n——选一张响应卡响应，或点「不连锁」让过", false, 60f);
            FinishResponsePickUi();
            return picked != null && picked.Count > 0 ? rest[picked[0]] : null;
            }
            finally
            {
                ShowToast(""); // 整窗收口清 toast（②档关窗 prompt 已清，此处兜底守卫档早退路径）
            }
        }

        /// <summary>纯 Region 响应面收尾（守卫不在身时）：作废在途倒计时+按钮复位隐藏
        ///（混合态守卫在身由 ResolveGuardPrompt 统一收口）。</summary>
        private void FinishResponsePickUi()
        {
            if (_guardTcs != null) return;
            _notChainSerial++;
            if (_btnNotChain != null)
            {
                SetNotChainLabel("不连锁");
                _btnNotChain.gameObject.SetActive(false);
            }
        }

        /// <summary>不连锁倒计时（2026-10-15；2026-10-16 改挂窗口状态）：按钮文案「不连锁【N】」
        /// 逐秒倒数；代次不符（选卡/守卫定/让过/切屏已收口）即报废；归零=放弃选择（拒绝连锁）——
        /// Region 响应面收口空表（=Pass）+守卫窗口让过。</summary>
        private async UniTaskVoid NotChainCountdownAsync(int serial)
        {
            for (int s = NotChainSeconds; s >= 1; s--)
            {
                SetNotChainLabel($"不连锁【{s}】");
                if (_btnNotChain != null) _btnNotChain.gameObject.SetActive(true);
                await UniTask.Delay(1000);
                if (serial != _notChainSerial) return;
            }
            if (serial != _notChainSerial) return;
            Debug.Log($"[响应窗口] 倒计时归零——{NotChainSeconds} 秒未操作，放弃选择（拒绝连锁）");
            _responsePassAll = true; // 整窗让过：归零后不再降档询问
            _regionSelector?.Cancel();
            if (_guardTcs != null) ResolveGuardPrompt(null, "倒计时归零——守卫让过");
        }

        private void SetNotChainLabel(string text)
        {
            if (_btnNotChain == null) return;
            var lbl = _btnNotChain.GetComponentInChildren<TMP_Text>(true);
            if (lbl != null) lbl.text = text;
        }

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

        // ======================================== 选择/响应提示（2026-10-16 lbl-toast 定案） ========================================
        // lbl-toast 的出现时机只有两类：①效果发动的目标选择——介绍完整效果（说明选目标来干嘛）；
        // ②响应时——说明响应的是对方的什么发动。与 btn-notchain 基本成对出现：
        // 主动效果（生物/法术）可「取消不发动」（AllowCancel→空目标落空）；可连锁响应可「不连锁」；
        // 自动/强制发动不可拒——不选=超时默认代选（Service AutoPick 既有口径）。
        // 场面信息（地牌/单位/格子介绍）不走 toast——hover-info 单源（LandCellInfo/CardHoverInfo）。

        /// <summary>选择弹窗 toast 文案：PromptSource（宿主卡）+PromptEffectId 查卡表
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

        /// <summary>选择面 toast/取消键驱动（RegionTargetSelector.onPrompt 注入）：
        /// 开窗（text≠null）=toast 写选择目的；主动效果（allowCancel）亮 btn-notchain「取消」——
        /// 点击走既有 OnNotChain 收口=Cancel 选择（空表→AllowCancel 路径=不发动）；
        /// 关窗（text=null）=清 toast+收取消键（仅本屏亮出的才收——响应窗的不连锁自管，勿误关）。</summary>
        private void OnSelectPrompt(string text, bool allowCancel)
        {
            if (text == null)
            {
                ShowToast("");
                if (_selectCancelShown)
                {
                    _selectCancelShown = false;
                    if (_btnNotChain != null) _btnNotChain.gameObject.SetActive(false);
                }
                return;
            }
            ShowToast(text);
            if (allowCancel && _btnNotChain != null)
            {
                _selectCancelShown = true;
                SetNotChainLabel("取消");
                _btnNotChain.gameObject.SetActive(true);
            }
        }

        private bool _selectCancelShown; // 「取消」键是否本屏选择面亮出（关窗收口判据——响应窗自管的 不连锁 不碰）

        /// <summary>响应窗「在响应什么」（2026-10-16 定案）：栈顶未结算项→发动方+卡/效果名
        ///（BattleEventText.StackAdd 行同款口径）——施放/攻击宣言/效果发动各有措辞。</summary>
        private string DescribeResponseSource()
        {
            var top = Core != null ? Core.StackEngine.PeekStack() : null;
            if (top == null) return "对方行动结算中——可响应或让过";
            string who = top.Controller == P1 ? "我方" : "对方";
            if (top.IsCardCast && top.CastCard != null)
                return $"{who}使用【{CardDisplayName(top.CastCard)}】——可响应（选响应卡）或让过（不连锁）";
            if (top.IsAttackDeclaration)
            {
                string atk = top.Source is Card ac ? CardDisplayName(ac) : "角色";
                string tgt = top.Targets != null && top.Targets.Count > 0 ? EntityDisplay(top.Targets[0]) : "";
                return $"{who}的{atk}攻击{tgt}——可响应（守卫/响应卡）或让过（不连锁）";
            }
            if (top.IsGuardDeclaration) return "守卫宣言结算中——可响应或让过（不连锁）";
            if (top.IsSBA) return "系统动作结算中——可响应或让过（不连锁）";
            string src = top.Source is Card sc ? CardDisplayName(sc)
                : top.Source is Player sp ? (sp == P1 ? "我方角色" : "对方角色") : "?";
            string fxName = top.Definition?.DisplayName;
            bool voluntary = top.Definition != null
                && top.Definition.ActivationType == EffectActivationType.Voluntary;
            return voluntary
                ? $"{who}发动【{src}「{fxName}」】——可响应（选响应卡）或让过（不连锁）"
                : $"【{src}】触发「{fxName}」——可响应（选响应卡）或让过（不连锁）";
        }

        /// <summary>toast 顶层临时提示（选择/响应窗开闭、拒绝原因等）：空文本=清顶层并回落底层横幅
        ///（教学步骤/守卫提示）。面板底板随有效文案起落——空=藏板（2026-10-16 修复：底板常绘，
        /// 旧口径只清文本=空横幅滞留屏）。</summary>
        private void ShowToast(string message)
        {
            _toastOverlay = string.IsNullOrEmpty(message) ? null : message;
            RenderToast();
        }

        /// <summary>toast 渲染单口：顶层优先、底层兜底，两者皆空=藏板；无 toast 控件（过渡面）回落日志。</summary>
        private void RenderToast()
        {
            string text = _toastOverlay ?? _toastBase;
            bool show = !string.IsNullOrEmpty(text);
            if (_toastPanel != null && _toastPanel.activeSelf != show)
                _toastPanel.SetActive(show);
            if (_lblToast != null) _lblToast.text = show ? text : "";
            else if (show) Debug.Log($"[BattleScreen] {text}"); // 过渡面无 toast 控件（教学配置缺失等回落提示走日志）
        }

        // ======================================== 战报 ========================================

        /// <summary>本地战报事件桥（2026-10-16 定案）：主动/自动效果发动（StackAdd 精滤）+
        /// 结算结果（伤害/生命/抽卡/出牌/死亡/疲劳/回合/终局）进 log-list——渲染单源
        /// BattleEventText.ForLocal（此前该入口零接线，本地局日志除系统行外恒空）。</summary>
        private void WireLocalBattleLog()
        {
            LogLocalLine<TurnStartEvent>();
            LogLocalLine<CardDrawEvent>();
            LogLocalLine<CardPlayEvent>();
            LogLocalLine<StackAddEvent>();
            LogLocalLine<CombatDamageEvent>();
            LogLocalLine<LifeChangeEvent>();
            LogLocalLine<LifeLossEvent>();
            LogLocalLine<CardDestroyEvent>();
            LogLocalLine<CounterChangedEvent>(); // 指示物落定（谁获得/失去几层什么——触发行只报效果名）
            LogLocalLine<FatigueEvent>();
            LogLocalLine<GameOverEvent>();
        }

        private void LogLocalLine<T>() where T : class, IGameEvent
            => Subscribe<T>(e =>
            {
                var line = BattleEventText.ForLocal(e, p => p == P1);
                if (!string.IsNullOrEmpty(line.Text)) AppendLog(line.Text, line.Class);
            });

        private void AppendLog(string text, string cls)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_logList?.Content == null) return;

            // 2026-10-16 定案：战报行不再代码新建——克隆 content 直属隐藏模板 lbl（用户手调的
            // TMP 字体/字号/对齐/折行/CSF 全由 prefab 接管），代码只填文本与分类色。
            if (_logLineTemplate == null)
            {
                for (int i = 0; i < _logList.Content.childCount; i++)
                {
                    var ch = _logList.Content.GetChild(i);
                    if (ch.name == "lbl" && !ch.gameObject.activeSelf)
                    {
                        _logLineTemplate = (RectTransform)ch;
                        break;
                    }
                }
                if (_logLineTemplate == null)
                {
                    Debug.LogWarning("[BattleScreen] log-list content 缺隐藏模板 lbl，战报行丢弃");
                    return;
                }
            }

            var (color, style) = cls switch
            {
                "log-line--turn" => (UiStyle.LogTurn, FontStyle.Bold),
                "log-line--combat" => (UiStyle.LogCombat, FontStyle.Normal),
                "log-line--system" => (UiStyle.LogSystem, FontStyle.Normal),
                _ => (UiStyle.LogBase, FontStyle.Normal),
            };
            var lineGo = UnityEngine.Object.Instantiate(_logLineTemplate.gameObject, _logList.Content, false);
            lineGo.name = "lbl";
            lineGo.SetActive(true);
            var label = lineGo.GetComponent<TMP_Text>();
            label.text = text;
            label.color = color;
            label.fontStyle = (FontStyles)style; // 枚举位同值（Normal/Bold）

            while (_logList.Content.childCount > 300)
            {
                var first = _logList.Content.GetChild(0);
                if ((RectTransform)first == _logLineTemplate)
                    first = _logList.Content.childCount > 1 ? _logList.Content.GetChild(1) : null;
                if (first == null) break;
                first.SetParent(null);
                UnityEngine.Object.Destroy(first.gameObject);
            }
            _logList.ScrollToBottom(); // 新增即滚底（2026-10-16 晚改版：行高由 VLG+CSF 自适应，见 prefab log-list content）
        }

        // ======================================== 抉择标签（本地；沿用旧语义） ========================================

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
