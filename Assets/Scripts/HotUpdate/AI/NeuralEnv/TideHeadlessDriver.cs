using System;
using System.Collections.Generic;
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
    }

    /// <summary>
    /// 无头对局驱动器：把「模型在环」的决策循环收敛成 reset/step 两步接口。
    /// 复用 AiBattleDriver.RunFullGame 的无头初始化骨架（InitGame + 双方 IsAI + 棋盘接线），
    /// 但把决策点替换为「读动作 → GameActions 调用 → 快照 Φ 做差算塑形奖励」。
    ///
    /// 时序（每回合）：
    ///   Standby → SkipElementPool → 回合初自动横置地牌产费 → Main（模型决策点）
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
        // TideHeadlessServer.HandleReset 的 shapingLambda 字段，>0 生效，0 用本默认值）
        private float _shapingLambda = 0.005f;
        private const int MaxSettleAttempts = 32;    // 排干栈重试上限（镜像 SimpleAI）
        private const int PhaseAdvanceGuard = 256;   // 阶段推进循环保险（v2 响应分支并入同一循环后加大）
        private const int ResponseWindowGuard = 64;  // 单次窗口推进的 Pass 轮数保险
        private const float NoProgressPenalty = 0.05f; // 无进展动作扣分（即时信号：引导避开无效动作）
        private const int MaxActionsPerTurn = 256;   // 单回合动作数上限（v2 契约第 4 节 128→256：
                                                     // 响应步计入；仍防换状态翻转死循环）
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

        /// <summary>按局覆盖塑形系数 λ（reset 协议透传；≤0 忽略保持默认）。</summary>
        public void SetShapingLambda(float lambda)
        {
            if (lambda > 0f) _shapingLambda = lambda;
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
            CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;

            _core = GameCore.Instance;
            _core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
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
            CombatSystem.AdjacentResolver = _board.Neighbors;
            GameBoard.LinkAuraSystem.Attach(_board); // 连接光环（三轨制）：箭头指向格占据者享受 linkAuras

            _gameOver = false;
            _winner = null;
            _reason = null;
            _decider = null;
            _responseStop = false;
            _bannedThisTurn.Clear(); // 新对局：无进展防护状态归零（stamp 由 BuildResult 兜底重置）
            _actionsThisTurn = 0;
            _stallCounter = 0;

            AdvanceToDecision();
            return BuildResult(0f);
        }

        /// <summary>执行一步：应用动作下标 → 结算/响应窗口 → 推进 → 算奖励 → 返回下一决策点。</summary>
        public TideStepResult Step(int actionIndex)
        {
            if (_gameOver) return BuildResult(0f); // 已终局：无动作可执行

            var me = _decider ?? _core.TurnEngine.TurnPlayer; // 本步决策座次（Main=回合玩家 / 响应=优先权方）
            // 奖励势能剔除手牌（2026-09-22 定案）：含手牌时「每回合摸牌」是白拿的正奖励流
            float before = FieldValueReward.ComputePotential(_core, me, includeHand: false);

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
            else
            {
                bool ok = LegalActionEnumerator.Apply(_core, me, a);
                if (!ok)
                {
                    // 引擎拒绝（枚举/引擎口径漂移）：本回合摘除该动作（重观测不再出现）+ 小额扣分，
                    // 不推进。摘除保证最坏情况把无效动作各试一次后只剩 EndTurn/Pass → 回合必然流动。
                    // 2026-09-22 符号修复：此前误传 +NoProgressPenalty（正奖励）——拒绝动作反而
                    // +0.05，构成可刷的奖励泉（每回合把会被拒的动作挨个试一遍）；取负恢复扣分本意。
                    _bannedThisTurn.Add(a.Signature);
                    _legal.RemoveAll(_bannedThisTurn);
                    return BuildResult(-NoProgressPenalty);
                }
                // 应用成功：不立即 DrainStack——对方回合响应窗口由 AdvanceToDecision 的
                // 响应循环接管（候选>0 停靠问模型 / 候选=0 自动让过 = 旧双 Pass 行为）
            }

            // 推进到下一决策点（End→Standby 折返 / Standby→Main / 回合初横置 / 响应窗口；可能触发疲劳判负）
            AdvanceToDecision();

            // 奖励：终局 ±1 主导（不再叠加塑形，避免 Φ 在终局无界）；否则势能塑形 λ·ΔΦ
            float reward;
            if (_gameOver)
            {
                reward = ReferenceEquals(_winner, me) ? 1f : -1f;
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
            EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            CombatSystem.AdjacentResolver = null;
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
        }

        private int SeatOf(Player p)
            => p == null || _core == null ? -1 : (ReferenceEquals(p, _core.Player1) ? 0 : 1);

        /// <summary>
        /// 推进到下一决策点（v2 泛化：Main 停靠 + 响应停靠）：
        ///   栈非空（或空栈待发先上栈）→ 响应窗口：候选&gt;0 停靠（decider=优先权方），
        ///     候选=0 驱动自动 Pass 直到窗口关闭（双 Pass 结算）——与旧 DrainStack 双 Pass 语义等价；
        ///   栈空 → 相位推进：Standby → SkipElementPool → 回合初横置产费 → Main 停靠（decider=回合玩家）；
        ///     End → CheckPhaseTransition → 下一回合 Standby（循环折返）。
        /// vs SimpleAI 模式：非模型回合不逐相位推进，直接整回合交给 SimpleAI（镜像 AiBattleDriver）；
        /// 模型回合内的响应窗口也自动让过（TryResolveResponseWindow 内的 vs 分支）。
        /// </summary>
        private void AdvanceToDecision()
        {
            for (int i = 0; i < PhaseAdvanceGuard && !_gameOver; i++)
            {
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
                    TapAllLands(turnPlayer);
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

        private TideStepResult BuildResult(float reward)
        {
            // 回合切换：清空摘除表与动作计数（新回合同一动作可能重新合法——地牌重置/新抽牌等）
            int turn = _core.TurnEngine.TurnNumber;
            if (_banTurnStamp != turn)
            {
                _banTurnStamp = turn;
                _bannedThisTurn.Clear();
                _actionsThisTurn = 0;
            }

            // 停靠座次兜底：终局/异常推进未设 _decider 时回落回合玩家（obs 仍可构建）
            if (_decider == null) _decider = _core.TurnEngine.TurnPlayer;

            var obs = TideObservation.Build(_core, _decider, _responseStop);
            _legal = new LegalActionEnumerator();
            if (_responseStop) _legal.EnumerateResponses(_core, _decider);
            else _legal.Enumerate(_core, _decider);
            _legal.RemoveAll(_bannedThisTurn); // 本回合已判无效的动作不再出现在选项表
            _legal.AppendFallback(_responseStop); // 空表冻结兜底（2026-09-30：停靠态与引擎瞬态错位/摘除清空时补 PassPriority/EndTurn 保流动）
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
            };
        }

        // ===================================================== 地牌横置（复刻 SimpleAI 色匹配启发式） =====================================================

        /// <summary>回合初横置全部未横置地牌：产色按手牌费用需求匹配，无需求色回落剩余指示物最多的颜色。</summary>
        /// <summary>横置全部地牌（2026-09-14 收敛到共享 LandTapPolicy——混付感知产色）。</summary>
        private void TapAllLands(Player me)
            => LandTapPolicy.TapAllLands(_core, me);
    }
}
