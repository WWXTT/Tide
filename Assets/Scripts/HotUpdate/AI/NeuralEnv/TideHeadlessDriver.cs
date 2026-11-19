using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCore;
using SynergyUI;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// 无头驱动器的单步决策快照：当前决策座次的观测 + 合法动作表。
    /// （TideObservation.Build + LegalActionEnumerator.Enumerate/EnumerateResponses 的一次组合封装。）
    /// </summary>
    public sealed class TideStepResult
    {
        public TideObservation Obs;         // 状态快照（cards_/global_，决策座次视角）
        public LegalActionEnumerator Legal; // 合法动作表（含 Features 张量，Actions.Count × NAction）
        public float Reward;                // 本步奖励（终局 ±1 或 λ·ΔΦ 塑形）
        public bool Done;                   // 对局是否结束
        public Player Winner;               // 终局胜者（未结束为 null）
        public string Reason;               // 终局原因（未结束为 null）
        public int Turn;                    // 当前全局回合数
        public int ModelSeat;               // 模型座次（0=P1 先手 / 1=P2）；自对弈为 -1
        /// <summary>本停靠点决策座次（0=P1 / 1=P2；Main=回合玩家，响应窗口=优先权持有方）。
        /// 协议 info.decisionSeat / info.toPlay 数据源（v2，2026-09-30 契约第 5 节）；终局为 -1。</summary>
        public int DecisionSeat;
        /// <summary>本停靠点是否响应窗口（true=EnumerateResponses 的动作表：Guard/RespondPlay/…/PassPriority）。</summary>
        public bool ResponseStop;
        /// <summary>L1/L2 循环收口在本步触发（协议 info.loopPruned——L3 训练剪枝外显口，
        /// 2026-11-18 循环治理 §5.4；仅收口那一步为 true）。</summary>
        public bool LoopPruned;
        /// <summary>收口种类（协议 info.loopReason："cycle"/"stall"/"budget"，未触发 ""——
        /// stats 分开计 loop_events 与 loop_budget 的依据）。</summary>
        public string LoopReason;
        /// <summary>本回合已用动作数（含本步；收口步=触发时计数。协议 info.turnActions——
        /// max_turn_actions 指标数据源）。</summary>
        public int TurnActions;
    }

    /// <summary>
    /// 无头对局驱动器：把「模型在环」的决策循环收敛成 reset/step 两步接口。
    /// 复用 AiBattleDriver.RunFullGame 的无头初始化骨架（InitGame + 双方 IsAI + 棋盘接线），
    /// 但把决策点替换为「读动作 → GameActions 调用 → 快照 Φ 做差算塑形奖励」。
    ///
    /// 时序（每回合）：
    ///   回合开始（StartNewTurn 自动过 Standby → Main；下方 Standby 分支只是防御兜底）
    ///   → 模型选动作 → 应用 → 响应窗口（v2）→ 阶段推进折返 → 下一决策点（可能是对方回合
    ///   或对方回合内的响应窗口）。
    ///
    /// 决策点两类（v2，2026-09-30 动作空间二期）：
    ///  - Main 停靠：回合玩家主阶段（栈空）——Enumerate 全动作表；
    ///  - 响应停靠：栈非空时对优先权持有方收集候选（CollectAvailableResponses + 手牌响应卡），
    ///    候选 &gt; 0 才停靠问模型（自对弈两座次都问；候选=0 驱动自动让过——停靠步数有界）。
    ///    vs SimpleAI 模式响应窗口双侧自动让过（契约第 6 节：SimpleAI 不做守卫/响应、模型在
    ///    SimpleAI 回合不获决策点——vs 红轴 0.75 早停口径与旧数字可比；响应能力由自对弈训练）。
    ///
    /// 奖励（actor-centric 自对弈，reward 恒归属「刚行动的那一方」= 本步决策座次）：
    ///   终局 ±1 主导；非终局每步 λ·(Φ_after − Φ_before)，Φ = 己方战场价值 − 对方战场价值
    ///   （2026-09-22：λ 默认 0.005 且 Φ 剔除手牌——塑形累计须 ≪ 终局 ±1，否则学拖局）。
    ///   λ=0 显式下发 = 关闭塑形（非终局恒 0 纯终局 ±1 基线，2026-11-02 起可关）。
    ///   响应步计入 λ·ΔΦ 塑形（弧长变长——λ 维持观察，必要时再调，契约风险节）。
    ///
    /// 防死循环兜底（三层）：引擎速度门（响应互相抬记速器，速度不足自然无候选）+
    /// MaxActionsPerTurn（响应动作也计数，超限强排+强制 EndTurn）+ EndTurn/PassPriority 恒排
    /// 动作表末位（截断兜底依赖）。
    ///
    /// 文档化简化（v1 沿袭）：地牌横置由驱动回合初自动完成（LandTapPolicy 混付感知），不建模。
    /// </summary>
    public sealed class TideHeadlessDriver
    {
        // 塑形系数（2026-09-22 定案 0.05→0.005：λ=0.05 时双方各自整局塑形累计 ≈ +3.1，
        // 是终局 ±1 的 3 倍，自对弈收敛到拖局刷塑形；reset 协议可按局覆盖——
        // TideHeadlessServer.HandleReset 的 shapingSet=1 显式下发，含 0=关闭塑形）
        private float _shapingLambda = 0.005f;
        private const int MaxSettleAttempts = 32;    // 排干栈重试上限（镜像 SimpleAI）
        private const int PhaseAdvanceGuard = 256;   // 阶段推进循环保险（v2 响应分支并入同一循环后加大）
        private const int ResponseWindowGuard = 64;  // 单次窗口推进的 Pass 轮数保险
        private const float NoProgressPenalty = 0.05f; // 无进展动作扣分（即时信号：引导避开无效动作）
        private const int MaxActionsPerTurn = 256;   // 单回合动作数上限（v2 契约第 4 节 128→256：
                                                     // 响应步计入；仍防换状态翻转死循环）
        // L2 回合预算（2026-11-18 循环治理 §5.3）：单回合动作数到预算即强制收口本回合——
        // 模拟真人墙钟（AI 没有墙钟，合法循环下会无限刷步），只进 headless 驱动，不进正式规则；
        // 不判和、不改奖励（收口只损失节奏）。取值 60（用户拍板）：真人典型回合 ~10 步，
        // 蓝碑阵铺场等多步回合也远低于 60。MaxActionsPerTurn=256 保留为硬保险（防收口实现 bug）。
        private const int TurnActionBudget = 60;
        private bool _budgetClosedThisTurn;          // 本回合预算收口已执行（60~256 之间不重复收口）
        private int _loopBudgetCount;                // L2 预算收口累计次数（阶段 3 起 进协议指标，只增不清）

        // L1 无进展指纹（2026-11-18 循环治理 §5.2）：识别合法净零循环（绿：牺牲地→回收墓地→铺地→
        // 再牺牲）——同回合内局面指纹重现（回环）或连续停滞 → 收口本回合（不判和、不改奖励）。
        // 指纹只含"净变化"项，不含实例 ID/卡编号/时间戳（否则永不相等）；增长型循环交给 L2 预算。
        // 作用域：仅 Main 相决策点；响应窗沿用现有 StallDrawThreshold（方案 §5.2）。
        private const int LoopStallThreshold = 8;    // 停滞：连续 8 次 Main 相决策指纹不变
        // 自检开关（§7.1-② 探针级验收演示用）：true 时停滞阈值降为 1（验证检测器可触发）。
        // public static + 环境变量 TIDE_LOOP_SELFTEST=1 双通道：batchmode/探针起进程时带环境变量
        // 即生效（静态初始化读一次，无需反射/改码重编译），编辑器内也可直赋值翻转；默认恒 false。
        public static bool LoopGuardSelfTest =
            Environment.GetEnvironmentVariable("TIDE_LOOP_SELFTEST") == "1";
        // R1 环形缓冲（方案 §5.2 原文口径，2026-11-18 用户定案）：本回合最近 32 条指纹，
        // 更早的滑出窗口（周期 >32 步的慢循环交给 L2 预算兜底）；回合切换清空。
        private const int FpRingCapacity = 32;
        private readonly Queue<ulong> _fpRing = new Queue<ulong>();       // 指纹时序（淘汰队首用）
        private readonly HashSet<ulong> _fpRingSet = new HashSet<ulong>(); // 指纹成员（O(1) 命中判）
        private ulong _lastFingerprint;              // 上一个 Main 相决策指纹（停滞比较用）
        private bool _hasLastFingerprint;            // _lastFingerprint 有效位（ulong 0 是合法哈希值）
        private int _stallCount;                     // 连续同指纹决策计数
        private int _loopCycleCount;                 // L1 回环收口累计（阶段 3 起 进协议指标，只增不清）
        private int _loopStallCount;                 // L1 停滞收口累计（同上）
        private bool _loopPrunedThisStep;            // L3-A：本步触发过 L1/L2 收口（BuildResult 消费后即清——仅收口那一步外显）
        private string _loopReasonThisStep;          // L3-A：收口种类（cycle/stall/budget；与上同生命周期）
        // 停滞判和（2026-09-30 v2 修复，当晚扩展）：连续 N 步动作表只剩兜底行（Main=EndTurn-only /
        // 响应停靠=PassPriority-only）→ 判和收口。覆盖两种实测死法：①资源耗尽终局无类疲劳规则
        // 不自然终结（每局烧满 2000 步，且长局每步耗时随回合数增长，u537/u236 两跑 0.7s→500s 爬升）；
        // ②响应停靠被引擎瞬态错位卡死（疑似 IsResolving 卡 true：停靠判定不查它、枚举守卫查它——
        // 补行兜底后该状态唯一行=PassPriority，PassPriority 也须计入停滞）。64 步 ≈ 双方各 32 回合
        // 零真实选项；对手仍有任何可行动作（出牌/攻击/放地/真响应）都会使计数归零，不会误杀。
        private const int StallDrawThreshold = 64;
        private int _stallCounter;

        private GameCore _core;
        private GameBoard.BoardState _board;
        private LegalActionEnumerator _legal;
        private bool _gameOver;
        private Player _winner;
        private string _reason;

        // 当前停靠点（BuildResult 按它构建观测与动作表；Step 按它归属奖励）
        private Player _decider;
        private bool _responseStop;
        private bool _discardStop; // P3（2026-11-19 §4.1）：手牌上限弃牌停靠（legal=DiscardChoice 行）

        // vs 脚本对手位：_modelPlayer != null 时，非模型回合由 SimpleAI 整回合自动打
        // （策略每局按 Reset 注入重建——主题评估用 AutoMatch，缺省 General）
        private Player _modelPlayer;
        private BattleController _aiCtrl;
        private SimpleAI _simpleAI = new SimpleAI();

        // 回合内无进展防护：引擎拒绝（枚举/引擎口径漂移）的动作按签名本回合摘除 + 小额扣分——
        // 否则确定性策略会无限重选同一无效动作，回合冻结烧满步数上限、对局永不自然终局
        private readonly HashSet<string> _bannedThisTurn = new HashSet<string>();
        private int _banTurnStamp = -1;
        private int _actionsThisTurn;

        public bool IsGameOver => _gameOver;
        public Player Winner => _winner;

        /// <summary>按局覆盖塑形系数 λ（reset 协议透传）。显式调用即生效——
        /// 0 = 关闭塑形：非终局奖励恒 0、无进展动作不扣分，只留终局 ±1（λ=0 基线消融，
        /// 2026-11-02 修复：旧口径仅 lambda&gt;0 生效，λ=0 根本发不下去、塑形永远关不掉）；
        /// 不调用保持默认 0.005（旧客户端兼容口径）。</summary>
        public void SetShapingLambda(float lambda)
        {
            _shapingLambda = lambda;
        }

        /// <summary>模型座次（0=P1 先手 / 1=P2）；自对弈为 -1。协议 info.modelSeat 用。</summary>
        public int ModelSeat
            => _modelPlayer == null || _core == null ? -1
             : (ReferenceEquals(_modelPlayer, _core.Player1) ? 0 : 1);

        /// <summary>当前决策座次（0=P1 / 1=P2；未初始化/终局 -1）。</summary>
        public int DecisionSeat => SeatOf(_decider);

        public TideHeadlessDriver()
        {
            // 订阅终局（全局事件总线；Dispose 反订阅，避免跨局泄漏）
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
        }

        /// <summary>初始化一局（自对弈：双方座位都由模型驱动）。返回首个决策点观测。</summary>
        public TideStepResult Reset(List<CardData> deck1, List<CardData> deck2)
            => ResetCore(deck1, deck2, null);

        /// <summary>
        /// 初始化一局 vs 脚本对手：modelIsP1 指定模型座次，另一座位整回合由 SimpleAI 自动打
        /// （opponentStrategy 注入决策偏好——主题评估传 AiStrategy.AutoMatch(对手卡组)，缺省通用）。
        /// obs/合法动作/reward 恒为模型视角（对手回合在内部自动完成，Python 只见模型决策点）；
        /// 响应窗口双侧自动让过（v2 契约口径——模型在 SimpleAI 回合不获决策点）；
        /// 非终局塑形的 ΔΦ 口径为「模型行动 + 对手整回合响应」的弧长，终局 ±1 仍按模型胜负。
        /// </summary>
        public TideStepResult Reset(List<CardData> deck1, List<CardData> deck2, bool modelIsP1,
            AiStrategy opponentStrategy = null)
            => ResetCore(deck1, deck2, modelIsP1, opponentStrategy);

        private TideStepResult ResetCore(List<CardData> deck1, List<CardData> deck2, bool? modelIsP1,
            AiStrategy opponentStrategy = null)
        {
            // 变形目标形态解析器：组合根注入（镜像 AiBattleDriver / BattleController）
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;

            _core = GameCore.Instance;
            // RL 环境（2026-10-07 卡牌化口径）：双方 deck 自动挑一张合格结界充当技能
            //（与挑战模式 AI 随机卡组同口径——deck 无构筑标记）
            _core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1),
                skillCardId1: HeroSkillSystem.AutoPickSkillCard(deck1),
                skillCardId2: HeroSkillSystem.AutoPickSkillCard(deck2));
            // 规则对齐（2026-11-20，AI训练说明 §4.6）：后手（P2）开局 +SecondPlayerArmorBonus 护甲——
            // 与 AiDuelRunner（TideSim/窗口批量）/联机/本地对局同口径。此前 headless 缺失，
            // 导致训练分布 ≠ 实战，且六格 P1 虚高（脚本 P2 少甲）/P2 虚低（模型 P2 少甲）。
            if (GameCore.SecondPlayerArmorBonus > 0)
                _core.Player2.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter,
                    GameCore.SecondPlayerArmorBonus, _core.Player2);
            _core.Player1.IsAI = true; // 目标/范围选择自动应答（不弹窗，异步同步完成）
            _core.Player2.IsAI = true;
            _modelPlayer = modelIsP1.HasValue ? (modelIsP1.Value ? _core.Player1 : _core.Player2) : null;
            _aiCtrl = modelIsP1.HasValue ? new BattleController() : null;
            if (modelIsP1.HasValue)
                _simpleAI = new SimpleAI(opponentStrategy); // vs 模式：每局按注入策略重建（自对弈不用）

            // 棋盘占用层（派生，单向读核心）：为碾压关键词注入邻接解析 + 连接光环接线（核心不绑棋盘，宿主接线）
            _board?.Dispose();
            GameBoard.LinkAuraSystem.Detach(); // 上局接线归零（静态扩展点惯例）
            _board = new GameBoard.BoardState(_core, _core.Player1, _core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.FlankNeighbors; // 碾压=左右同排生物（2026-10-04 语义修订）
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole; // 碾压攻角色=角色格左右邻格（2026-10-16 改版）
            BranchEngines.UnityAdjacentCountOf = _board.AdjacentOccupantCount; // 团结=六邻单位数（2026-10-20 效果序位换轴）
            GameBoard.LinkAuraSystem.Attach(_board); // 连接光环（三轨制）：箭头指向格占据者享受 linkAuras

            _gameOver = false;
            _winner = null;
            _reason = null;
            _decider = null;
            _responseStop = false;
            _discardStop = false;
            // P3（2026-11-19 §4.1）：模型座手牌上限弃牌改停靠决策（DiscardChoice 行）——
            // 自对弈（_modelPlayer=null）双座皆模型；vs 模式仅模型座
            HandLimitDiscardGate.Reset();
            HandLimitDiscardGate.Bind(p => _modelPlayer == null || ReferenceEquals(p, _modelPlayer));
            _bannedThisTurn.Clear(); // 新对局：无进展防护状态归零（stamp 由 BuildResult 兜底重置）
            _actionsThisTurn = 0;
            _budgetClosedThisTurn = false; // L2 预算收口状态归零（_loopBudgetCount 只增不清）
            _stallCounter = 0;
            _fpRing.Clear();      // L1 指纹环形缓冲归零（跨局残留会误报回环；计数器只增不清）
            _fpRingSet.Clear();
            _stallCount = 0;
            _hasLastFingerprint = false;
            _loopPrunedThisStep = false; // L3-A 单步标志归零

            AdvanceToDecision();
            return BuildResult(0f);
        }

        /// <summary>执行一步：应用动作下标 → 结算/响应窗口 → 推进 → 算奖励 → 返回下一决策点。</summary>
        public TideStepResult Step(int actionIndex)
        {
            if (_gameOver) return BuildResult(0f); // 已终局：无动作可执行

            var me = _decider ?? _core.TurnEngine.TurnPlayer; // 本步决策座次（Main=回合玩家 / 响应=优先权方）
            // 奖励势能剔除手牌（2026-09-22 定案）：含手牌时「每回合摸牌」是白拿的正奖励流；
            // λ=0 基线跳过计算（非终局奖励恒 0，势能白算）
            float before = _shapingLambda > 0f
                ? FieldValueReward.ComputePotential(_core, me, includeHand: false)
                : 0f;

            // 越界下标：保守兜底——不推进、奖励 0，重观测同状态。但必须计入停滞与回合动作数：
            // 冻结态下模型全掩码均匀采样，每步仅 1/512 概率落在唯一兜底行上，其余全走本分支——
            // 不计数则 StallDraw 永远到不了阈值（64×512≈3.3 万步空转），2026-09-30 复发实证。
            if (_legal == null || actionIndex < 0 || actionIndex >= _legal.Actions.Count)
            {
                if (_legal != null && _legal.Count == 1)
                {
                    var t = _legal.Actions[0].Type;
                    if ((t == TideActionType.EndTurn || t == TideActionType.PassPriority)
                        && ++_stallCounter >= StallDrawThreshold)
                    {
                        _gameOver = true;
                        _winner = null;
                        _reason = "StallDraw";
                    }
                }
                _actionsThisTurn++;
                return BuildResult(0f);
            }

            var a = _legal.Actions[actionIndex];
            _actionsThisTurn++;

            // 停滞判和：唯一行=兜底动作（EndTurn/PassPriority）连续达标 → 判和收口（winner=null →
            // Python 平局 reward 0，2026-09-29 口径）。放在一切动作分支之前——该状态下任何推进都无意义。
            if (_legal.Count == 1 && (a.Type == TideActionType.EndTurn || a.Type == TideActionType.PassPriority))
            {
                if (++_stallCounter >= StallDrawThreshold)
                {
                    _gameOver = true;
                    _winner = null;
                    _reason = "StallDraw";
                    return BuildResult(0f);
                }
            }
            else
            {
                _stallCounter = 0;
            }

            // L2 回合预算收口（2026-11-18 循环治理 §5.3）：尽力收口——排干栈 → Main 相强 EndTurn；
            // EndTurn 可能被引擎拒绝（阶段/栈态），此时只打日志不重试（MaxActionsPerTurn=256 硬保险兜底）。
            if (_actionsThisTurn >= TurnActionBudget && !_budgetClosedThisTurn)
            {
                _budgetClosedThisTurn = true;
                _loopBudgetCount++;
                _loopPrunedThisStep = true; // L3-A：本步即收口步（info.loopPruned）
                _loopReasonThisStep = "budget";
                CloseTurnByLoopGuard();
                Console.WriteLine($"[LoopGuard] budget turn={_core.TurnEngine.TurnNumber} seat={SeatOf(_decider)} actions={_actionsThisTurn} n={_legal?.Count ?? 0} hand={_core.ZoneManager.GetCards(_decider, Zone.Hand)?.Count ?? 0}");
            }

            if (_actionsThisTurn > MaxActionsPerTurn)
            {
                // 保险：同回合动作数超限（如换状态翻转死循环、模型互挂响应）→
                // 强制收口（响应窗口先强排干，Main 再强制 EndTurn），保证回合流动（疲劳/战斗最终能终结对局）
                if (!_gameOver && !_core.StackEngine.IsEmpty) GameActions.DrainStack(_core, 64);
                if (!_gameOver && _core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main
                    && _core.StackEngine.IsEmpty)
                    GameActions.EndTurn(_core, _core.TurnEngine.TurnPlayer);
            }
            else if (a.Type == TideActionType.EndTurn)
            {
                // EndTurn（镜像 SimpleAI 收尾时序）：Main 停靠点栈本应已空（响应循环已收口）；
                // 保险先排干悬挂栈对象（否则 EndTurn 被栈空守卫静默拒绝），死亡触发在 Main 相位内结算。
                if (!_gameOver && !_core.StackEngine.IsEmpty) GameActions.DrainStack(_core, MaxSettleAttempts);
                if (!_gameOver) GameActions.EndTurn(_core, me);
            }
            else if (a.Type == TideActionType.PassPriority)
            {
                // 响应让过：直接 Pass（双 Pass 触发结算），推进交响应循环
                GameActions.PassPriority(_core, me);
            }
            else if (a.Type == TideActionType.DiscardChoice)
            {
                // P3（2026-11-19 §4.1）手牌上限弃牌：交选择释放门——EnforceHandLimitAsync
                // 余段（入墓+CardDiscardEvent）在 Complete 调用线程内联继续；钳到 over 张防多弃
                var snap = HandLimitDiscardGate.Snapshot();
                var picked = new List<Card>();
                if (a.Targets != null)
                    foreach (var e2 in a.Targets)
                        if (e2 is Card dc && snap.cards != null && snap.cards.Contains(dc)
                            && picked.Count < Math.Max(1, snap.over))
                            picked.Add(dc);
                HandLimitDiscardGate.Complete(picked);
                _discardStop = false;
            }
            else
            {
                bool ok = LegalActionEnumerator.Apply(_core, me, a);
                if (!ok)
                {
                    // 引擎拒绝（枚举/引擎口径漂移）：本回合摘除该动作（重观测不再出现）+ 小额扣分，
                    // 不推进。摘除保证最坏情况把无效动作各试一次后只剩 EndTurn/Pass → 回合必然流动。
                    // 2026-09-22 符号修复：此前误传 +NoProgressPenalty（正奖励）——拒绝动作反而
                    // +0.05，构成可刷的奖励泉（每回合把会被拒的动作挨个试一遍）；取负恢复扣分本意。
                    // λ=0 基线下不扣分（保持「reward 只有终局 ±1」的纯终局口径；流动靠摘除保证）。
                    _bannedThisTurn.Add(a.Signature);
                    _legal.RemoveAll(_bannedThisTurn);
                    return BuildResult(_shapingLambda > 0f ? -NoProgressPenalty : 0f);
                }
                // 应用成功：不立即 DrainStack——对方回合响应窗口由 AdvanceToDecision 的
                // 响应循环接管（候选>0 停靠问模型 / 候选=0 自动让过 = 旧双 Pass 行为）
            }

            // 推进到下一决策点（End→Standby 折返 / Standby→Main / 回合初横置 / 响应窗口；可能触发疲劳判负）
            AdvanceToDecision();

            // L1 指纹检测（2026-11-18 循环治理 §5.2）：净零循环/停滞 → 收口本回合。只查 Main 相
            // 新决策点；放在奖励计算与 BuildResult 之前——收口可能推进回合/触发终局（疲劳），
            // 奖励按收口后终态归属、obs 为收口后决策点（不返回已作废的循环态动作表）。
            // 拒绝/越界两条早退路径不检测：状态原地不动、指纹必在本回合集合里，检测=每次必误报 cycle；
            // 那两条路径已有摘除 + StallDrawThreshold 兜底。
            CheckLoopFingerprint(a);

            // 奖励：终局 ±1 主导（不再叠加塑形，避免 Φ 在终局无界）；λ=0 基线非终局恒 0
            //（纯终局口径——塑形与无进展扣分全部关闭）；否则势能塑形 λ·ΔΦ
            float reward;
            if (_gameOver)
            {
                reward = ReferenceEquals(_winner, me) ? 1f : -1f;
            }
            else if (_shapingLambda <= 0f)
            {
                reward = 0f;
            }
            else
            {
                float after = FieldValueReward.ComputePotential(_core, me, includeHand: false);
                reward = FieldValueReward.ShapingDelta(before, after, _shapingLambda);
            }

            return BuildResult(reward);
        }

        /// <summary>释放：反订阅终局、撤销棋盘接线（静态扩展点归零）。</summary>
        public void Dispose()
        {
            HandLimitDiscardGate.Reset(); // P3 §4.1：解绑+清挂起
            HandLimitDiscardGate.Bind(null);
            EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
            BranchEngines.UnityAdjacentCountOf = null; // 团结邻接注入同步归零
            GameBoard.LinkAuraSystem.Detach(); // 连接光环接线同步归零
            _board?.Dispose();
            _board = null;
        }

        // ===================================================== 内部 =====================================================

        private void OnGameOver(GameOverEvent e)
        {
            _gameOver = true;
            _winner = e.Winner;
            _reason = e.Reason.ToString();
            HandLimitDiscardGate.CompleteDefault(); // P3 §4.1：终局释放挂起弃牌（防泄漏死等）
        }

        private int SeatOf(Player p)
            => p == null || _core == null ? -1 : (ReferenceEquals(p, _core.Player1) ? 0 : 1);

        /// <summary>
        /// 推进到下一决策点（v2 泛化：Main 停靠 + 响应停靠）：
        ///   栈非空（或空栈待发先上栈）→ 响应窗口：候选&gt;0 停靠（decider=优先权方），
        ///     候选=0 驱动自动 Pass 直到窗口关闭（双 Pass 结算）——与旧 DrainStack 双 Pass 语义等价；
        ///   栈空 → 相位推进：若仍处 Standby 走兼容口 SkipElementPool 入 Main（正常流程 StartNewTurn
        ///     已自动推进，此分支仅防御）→ Main 停靠（decider=回合玩家）；
        ///     End → CheckPhaseTransition → 下一回合 Standby（循环折返）。
        /// vs SimpleAI 模式：非模型回合不逐相位推进，直接整回合交给 SimpleAI（镜像 AiBattleDriver）；
        /// 模型回合内的响应窗口也自动让过（TryResolveResponseWindow 内的 vs 分支）。
        /// </summary>
        private void AdvanceToDecision()
        {
            for (int i = 0; i < PhaseAdvanceGuard && !_gameOver; i++)
            {
                // P3（2026-11-19 §4.1）手牌上限弃牌停靠：模型座挂起请求先于一切推进停靠
                //（回合末事件在 EndTurn/相位泵内已同步 Park；停靠步由 Step 应用 DiscardChoice 释放）
                if (HandLimitDiscardGate.HasPending)
                {
                    var snap = HandLimitDiscardGate.Snapshot();
                    _decider = snap.player ?? _core.TurnEngine.TurnPlayer;
                    _responseStop = true; // 决策上下文=窗口语义（g[49]=1）
                    _discardStop = true;
                    return;
                }

                var turnPlayer = _core.TurnEngine.TurnPlayer;

                // vs SimpleAI：对手回合整段自动打（TakeTurn 自带回合准备与 EndTurn，不折返）
                if (_modelPlayer != null && turnPlayer != _modelPlayer)
                {
                    RunSimpleAiTurn();
                    continue;
                }

                var engine = _core.StackEngine;

                // 响应窗口：栈上有待响应对象（空栈待发先上栈——触发式由本循环泵，镜像 DrainStack）
                if (!engine.IsEmpty || engine.HasPendingEffects)
                {
                    if (engine.IsEmpty)
                    {
                        engine.ProcessPendingEffects();
                        if (engine.IsEmpty) continue; // 上栈空转（触发上限丢弃等）→ 重查稳定态
                    }
                    if (!TryResolveResponseWindow()) return; // 停靠（_decider/_responseStop 已设）
                    continue;                                 // 窗口关闭（结算完成）→ 循环重查
                }

                var phase = _core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby;
                if (phase == PhaseType.Main)
                {
                    _decider = turnPlayer;
                    _responseStop = false;
                    return;
                }
                if (phase == PhaseType.Standby)
                {
                    GameActions.SkipElementPool(_core, turnPlayer);
                    continue;
                }
                if (phase == PhaseType.End)
                {
                    _core.TurnEngine.CheckPhaseTransition();
                    continue;
                }
                return; // 未知相位兜底（不应发生）
            }
        }

        /// <summary>
        /// 响应窗口推进：对当前优先权方轮询——
        ///   vs SimpleAI 模式：双侧自动让过（契约第 6 节口径）；
        ///   自对弈：候选（CollectAvailableResponses + 手牌响应卡）&gt; 0 停靠问模型，候选=0 自动 Pass。
        /// 返回 true = 窗口关闭（栈空，双 Pass 结算完成）；false = 停靠（响应决策点）。
        /// 与引擎 FinishResolution「强制批不开窗」的口径差异：本循环对上栈对象统一开窗（有候选才停）——
        /// 对纯强制桶（亡语等）是「多给响应机会」的超集行为，模型可让过；无头旧行为（DrainStack 全自动
        /// 双 Pass）是其特例。速度门保证互挂响应自然收敛（每响应抬高记速器，速度不足即无候选）。
        /// </summary>
        private bool TryResolveResponseWindow()
        {
            var engine = _core.StackEngine;
            for (int i = 0; i < ResponseWindowGuard && !_gameOver; i++)
            {
                if (engine.IsEmpty) return true; // 窗口关闭（结算链完成）
                var holder = engine.CurrentPriorityHolder;
                if (holder == null) return true; // 无优先权方（不应发生）——保守视为关闭

                // vs SimpleAI：响应窗口双侧自动让过（SimpleAI 不做守卫/响应、模型不响应对手回合）
                if (_modelPlayer != null)
                {
                    if (!GameActions.PassPriority(_core, holder)) return true;
                    continue;
                }

                // 自对弈：候选 > 0 停靠（两座次都问模型）；候选 = 0 驱动自动让过
                if (GameActions.CollectAvailableResponses(_core, holder).Count > 0
                    || LegalActionEnumerator.HasRespondableHandCard(_core, holder))
                {
                    _decider = holder;
                    _responseStop = true;
                    return false;
                }
                if (!GameActions.PassPriority(_core, holder)) return true;
            }
            return true; // 轮数保险：视为关闭（外层 PhaseAdvanceGuard 再兜底）
        }

        /// <summary>
        /// SimpleAI 整回合驱动（镜像 AiBattleDriver 时序）：TakeTurn 内部完成回合准备 →
        /// 动作耗尽 → 战斗 → EndTurn（只推进到 End 相位），此处补一次 CheckPhaseTransition
        /// 完成 End→Standby 折返。异常不炸训练服务：按异常中止收口（无胜者 → Python 记平局）。
        /// </summary>
        private void RunSimpleAiTurn()
        {
            try
            {
                _simpleAI.TakeTurn(_aiCtrl);
                if (!_gameOver)
                    _core.TurnEngine.CheckPhaseTransition();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TideHeadless] SimpleAI 回合异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                _gameOver = true;
                _winner = null;
                _reason = "SimpleAIError";
            }
        }

        // ===================================================== L1/L2 循环治理（2026-11-18 方案 §5.2/§5.3） =====

        /// <summary>L1/L2 收口本回合（方案 §5.2/§5.3 共用动作）：排干栈 → Main 相强 EndTurn。
        /// 尽力而为：EndTurn 可能被引擎拒绝（阶段/栈态）——不抛异常、不重试；回合不流动的最终
        /// 兜底仍是 MaxActionsPerTurn=256 硬保险。不判和、不改奖励（收口只损失节奏）。</summary>
        private void CloseTurnByLoopGuard()
        {
            if (!_gameOver && !_core.StackEngine.IsEmpty) GameActions.DrainStack(_core, 64);
            if (!_gameOver && _core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main
                && _core.StackEngine.IsEmpty)
                GameActions.EndTurn(_core, _core.TurnEngine.TurnPlayer);
            // P3 §4.1：强收路径释放挂起弃牌（默认组）——防门泄漏卡死后续停靠
            if (HandLimitDiscardGate.HasPending)
            {
                HandLimitDiscardGate.CompleteDefault();
                _discardStop = false;
            }
        }

        /// <summary>L1 指纹检测（方案 §5.2）：仅 Main 相决策点（响应窗沿用 StallDrawThreshold）。
        /// 回环=本回合内指纹重现（净零循环，含"动作应用后状态原地踏步"）；停滞=连续
        /// LoopStallThreshold 次指纹不变（防御分支——回环判据已覆盖连续重复，保留双判据）。
        /// 触发即：计数 → 收口本回合 → 摘除触发动作（EndTurn 被拒、回合未切换时保证换动作；
        /// 回合已切换则随回合清空自然失效）→ 打日志。trigger=本步刚应用成功的动作。</summary>
        private void CheckLoopFingerprint(TideAction trigger)
        {
            if (_gameOver || _responseStop) return;
            ulong fp = ComputeFingerprint();
            string reason = null;
            if (_fpRingSet.Contains(fp))
            {
                reason = "cycle"; // 环形窗口内指纹重现 → 净零循环（命中不重入队，保持原时序位）
            }
            else
            {
                FpRingAdd(fp);
                int threshold = LoopGuardSelfTest ? 1 : LoopStallThreshold; // 自检：阈值降 1 验证检测器可触发
                if (_hasLastFingerprint && fp == _lastFingerprint)
                {
                    if (++_stallCount >= threshold) reason = "stall";
                }
                else
                {
                    _stallCount = 0;
                }
            }
            _lastFingerprint = fp;
            _hasLastFingerprint = true;
            if (reason == null) return;

            if (reason == "cycle") _loopCycleCount++;
            else _loopStallCount++;
            _loopPrunedThisStep = true; // L3-A：本步即收口步（info.loopPruned）
            _loopReasonThisStep = reason;
            CloseTurnByLoopGuard();
            if (trigger.Type != TideActionType.EndTurn && trigger.Type != TideActionType.PassPriority)
            {
                _bannedThisTurn.Add(trigger.Signature);
                _legal?.RemoveAll(_bannedThisTurn);
            }
            Console.WriteLine($"[LoopGuard] {reason} turn={_core.TurnEngine.TurnNumber} seat={SeatOf(_decider)} actions={_actionsThisTurn} n={_legal?.Count ?? 0} hand={_core.ZoneManager.GetCards(_decider, Zone.Hand)?.Count ?? 0}");
        }

        /// <summary>L1 局面指纹（方案 §5.2；2026-11-18 实测修正×2）：回合号/双方生命+指示物摘要
        /// （角色护甲/攻击弹药等都在 Player 实体指示物里）/双方战场+技能区永久物状态多重集
        /// （排序后拼接，忽略实例 ID 与卡编号）/双方手牌·牌库·墓地·除外张数/元素池张数与横置数。
        /// 不含手牌内容（成本高且同名互换不算进展）；增长型循环交给 L2。
        /// 修正原因：方案原文战场只含名字、角色只有生命——正常战斗（伤害只改生物血攻、护甲吸收、
        /// 横置、耐久-1、**角色护甲吸伤**）不换名字不换区不改生命，指纹原地复现 → 6 局误报
        /// cycle 53 次；逐卡扩入 攻/血/横置/指示物摘要、角色扩入指示物摘要后这些"有进展"的
        /// 操作均可分，绿套净零循环（牺牲地→回收→铺地回到原点）仍精确命中回环。</summary>
        private ulong ComputeFingerprint()
        {
            var zm = _core.ZoneManager;
            var sb = new StringBuilder();
            sb.Append(_core.TurnEngine.TurnNumber).Append('|');
            foreach (var p in new[] { _core.Player1, _core.Player2 })
            {
                sb.Append(p.Life).Append('|').Append(CountersKeyOf(p)).Append('|');
                foreach (var z in new[] { Zone.Hand, Zone.Deck, Zone.Graveyard, Zone.Exile })
                    sb.Append(zm.GetCards(p, z)?.Count ?? 0).Append(',');
                var board = (zm.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                    .Concat(zm.GetCards(p, Zone.FieldZone) ?? new List<Card>())
                    .Select(CardStateKeyOf).OrderBy(x => x, StringComparer.Ordinal);
                sb.Append(string.Join("/", board)).Append(',');
                var pool = _core.ElementPool.GetPooledCards(p);
                sb.Append(pool.Count).Append(',').Append(pool.Count(x => x.IsTapped)).Append('|');
            }
            return Fnv1a64(sb.ToString());
        }

        /// <summary>永久物状态键：名字|攻|血|横置|指示物摘要（指纹用——同名卡按可视状态区分）。</summary>
        private static string CardStateKeyOf(Card c)
        {
            int power = 0, life = 0;
            c.TryGetPower(out power);
            c.TryGetLife(out life);
            bool tapped = c is ITappable t && t.IsTapped;
            return CardNameOf(c) + "|" + power + "|" + life + "|" + (tapped ? 1 : 0)
                + "|" + CountersKeyOf(c);
        }

        /// <summary>实体指示物摘要（排序 key:layer 拼接；角色护甲/弹药、卡的耐久/增减益全覆盖）。</summary>
        private static string CountersKeyOf(Entity e)
        {
            var counters = e.EnumerateCounters()
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key + ":" + kv.Value);
            return string.Join(";", counters);
        }

        /// <summary>卡面名字（同名视为同物——指纹不用实例 ID/卡编号）。</summary>
        private static string CardNameOf(Card c) => (c as CardWrapper)?.GetData()?.CardName ?? "?";

        /// <summary>指纹入环形缓冲（超容量滑出队首，成员集同步剔除）。</summary>
        private void FpRingAdd(ulong fp)
        {
            _fpRing.Enqueue(fp);
            _fpRingSet.Add(fp);
            if (_fpRing.Count > FpRingCapacity)
                _fpRingSet.Remove(_fpRing.Dequeue());
        }

        /// <summary>FNV-1a 64 位（指纹哈希，自实现不引依赖；逐 char 累积，等值性判定足够）。</summary>
        private static ulong Fnv1a64(string s)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= (ulong)s[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }

        private TideStepResult BuildResult(float reward)
        {
            // L3-A 外显（§5.4）：turnActions 取"本步结算时"的计数（收口步=触发计数 60——
            // 下方回合切换复位发生在其后，正常步=本回合已用步数）；loopPruned 单步消费即清。
            bool prunedThisStep = _loopPrunedThisStep;
            string reasonThisStep = _loopReasonThisStep ?? "";
            _loopPrunedThisStep = false;
            _loopReasonThisStep = null;
            int turnActionsAtBuild = _actionsThisTurn;

            // 回合切换：清空摘除表与动作计数（新回合同一动作可能重新合法——地牌重置/新抽牌等）
            int turn = _core.TurnEngine.TurnNumber;
            if (_banTurnStamp != turn)
            {
                _banTurnStamp = turn;
                _bannedThisTurn.Clear();
                _actionsThisTurn = 0;
                _budgetClosedThisTurn = false; // L2 预算收口状态随回合切换复位
                _fpRing.Clear();              // L1 指纹环形缓冲按回合重开（回合号在指纹内，跨回合本就必不等）
                _fpRingSet.Clear();
                _stallCount = 0;
            }

            // 停靠座次兜底：终局/异常推进未设 _decider 时回落回合玩家（obs 仍可构建）
            if (_decider == null) _decider = _core.TurnEngine.TurnPlayer;

            // v3：传入棋盘占用层——观测位置特征（单位格 TryGetCell；board 已在 Reset 接线）
            var obs = TideObservation.Build(_core, _decider, _responseStop, _board);
            _legal = new LegalActionEnumerator();
            if (_discardStop)
            {
                // P3 §4.1：弃牌停靠步——legal=DiscardChoice 组合行（独立枚举口；
                // 默认组保底恒 ≥1 行，不补 PassPriority/EndTurn 兜底防误用）
                var snap = HandLimitDiscardGate.Snapshot();
                if (snap.cards != null)
                    _legal = LegalActionEnumerator.EnumerateDiscardChoices(_core, _decider, snap.cards, snap.over);
            }
            else if (_responseStop) _legal.EnumerateResponses(_core, _decider);
            else _legal.Enumerate(_core, _decider);
            if (!_discardStop)
            {
                _legal.RemoveAll(_bannedThisTurn); // 本回合已判无效的动作不再出现在选项表
                _legal.AppendFallback(_responseStop); // 空表冻结兜底（2026-09-30：停靠态与引擎瞬态错位/摘除清空时补 PassPriority/EndTurn 保流动）
            }
            return new TideStepResult
            {
                Obs = obs,
                Legal = _legal,
                Reward = reward,
                Done = _gameOver,
                Winner = _winner,
                Reason = _reason,
                Turn = _core.TurnEngine.TurnNumber,
                ModelSeat = ModelSeat,
                DecisionSeat = _gameOver ? -1 : SeatOf(_decider),
                ResponseStop = _responseStop,
                LoopPruned = prunedThisStep,   // L3-A：收口那一步 true（协议 info.loopPruned）
                LoopReason = reasonThisStep,  // 协议 info.loopReason（cycle/stall/budget/""）
                TurnActions = turnActionsAtBuild, // 协议 info.turnActions（本回合已用步数）
            };
        }

    }
}
