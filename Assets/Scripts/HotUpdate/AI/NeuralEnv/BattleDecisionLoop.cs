using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCore;
using Cysharp.Threading.Tasks; // RunExternalTurnChain 的 RunWrapAsync().Forget()（UniTask 扩展）
using SynergyUI;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>Apply 结果状态（driver 据此归类奖励：Rejected=引擎拒绝摘除、其余早退=0、
    /// Advanced=正常推进走奖励计算）。2026-11-27 S1 随 BattleDecisionLoop 抽取建。</summary>
    public enum LoopStepStatus
    {
        /// <summary>正常路径：动作已应用（或门收口/停靠续跑/EndTurn 等价物）+ 已推进 + L1 指纹已检。</summary>
        Advanced,
        /// <summary>引擎拒绝（枚举/引擎口径漂移）：本回合摘除该动作，不推进（原 Step 摘除分支）。</summary>
        Rejected,
        /// <summary>越界下标：保守兜底——不推进、奖励 0（原 Step 越界分支）。</summary>
        OutOfBounds,
        /// <summary>停滞判和收口（唯一兜底行连续达标 → 判和，无胜者；原 Step 判和分支）。</summary>
        StallDraw,
        /// <summary>A6 拾取序列开闸（首步行 PickMode=0）：引擎状态冻结，本步不计奖励不推进。</summary>
        GateOpen,
        /// <summary>闸内拾取/提交步但序列未收口：同上，重建闸表。</summary>
        GatePending,
    }

    /// <summary>Apply 结果（2026-11-27 S1：原 Step 的 BuildResult 早退改状态码外显；奖励归 driver）。</summary>
    public sealed class LoopStepOutcome
    {
        public LoopStepStatus Status;
    }

    /// <summary>S3-1 开手交棒钩子返回值（driver→loop）：模型座回合开始停靠口的三态。</summary>
    public enum ModelTurnIntercept
    {
        /// <summary>无需处理/交棒已完成——loop 落本停靠口问模型（原 CompleteHandoff 后落穿）。</summary>
        None,
        /// <summary>开手脚本链已在途（原 AdvanceToDecision 的 _scriptTurnChainActive 防御 return）。</summary>
        ChainInFlight,
        /// <summary>钩子刚起了开手脚本链（原 RunOpeningTurnAsync 分支：防御停车判定后 continue 重查）。</summary>
        ChainStarted,
    }

    /// <summary>策略接口（S2 游戏侧接入，计划 §2）：游戏侧=OnnxTidePolicy（argmax）；
    /// 训练侧不经本接口（宿主=TCP 外发协议，driver 保留「每决策点发一次请求」现状——
    /// S1 有意未引入，S2 仅游戏侧消费）。Select 返回停靠表行下标。</summary>
    public interface IBattlePolicy
    {
        /// <summary>停靠点决策：obs=决策座次观测，legal=停靠动作表；返回行下标。</summary>
        int Select(TideObservation obs, LegalActionEnumerator legal);
        /// <summary>GRU rstate 归零（换局；宿主负责调）。</summary>
        void Reset();
    }

    /// <summary>停靠点观测+动作表+外显计数（原 TideHeadlessDriver.BuildResult 前半，
    /// 2026-11-27 S1 迁入 loop：四路枚举分发 / 回合切换复位 / L1-L3 单步外显单源）。</summary>
    public sealed class BattleDecisionStop
    {
        public TideObservation Obs;         // 决策座次观测（TideObservation.Build 产物）
        public LegalActionEnumerator Legal; // 本停靠动作表（含 Features；Main/Responses/Parked/PickSteps/Discard 五路之一）
        public Player Decider;              // 停靠座次（兜底已处理：终局/异常回落回合玩家）
        public bool ResponseStop;           // 响应/弃牌窗口语义（g[49]=1；协议 info.responseStop 数据源）
        public bool LoopPruned;             // L1/L2 收口在本步触发（协议 info.loopPruned；单步消费即清）
        public string LoopReason;           // 收口种类 cycle/stall/budget/""（协议 info.loopReason）
        public int TurnActions;             // 本回合已用动作数（协议 info.turnActions）
    }

    /// <summary>
    /// 逐步决策循环（2026-11-27 S1 从 TideHeadlessDriver 抽取，训练与游戏共用——本阶段只服务
    /// 训练侧 driver，游戏侧接入见 docs/逐步驱动共享_重构计划.md S2）：
    /// 决策点推进（Main / Standby 响应窗 / 目标拾取步 / 弃牌停靠 / 终局）、停靠枚举、
    /// 动作应用、L1/L2/L3 循环收口、D2d 停靠泵——**逐行迁移自 driver，逻辑零改动**
    /// （S1 验收=同种子对拍逐位一致；只允许成员归属搬家与下列三处结构性替换：
    /// ① TrackModelBehavior → AfterActionApplied 钩子；② S3-1 交棒块 → InterceptModelTurn 钩子；
    /// ③ Step 早退 BuildResult(x) → LoopStepStatus 状态码）。
    ///
    /// 职责边界（计划 §2）：loop 只吃「已初始化好的 GameCore + 棋盘 + 座次装配 + 配置」；
    /// 奖励计算（λ·ΔΦ/链势能/战斗伤害修正）、行为仪表、S3-1 交棒、S3-3 链闩位、协议 info 组装
    /// 全部留在 driver。常量（预算/阈值/保险）随迁且数值不动——观测/动作布局（v22）与超参零改动。
    ///
    /// 时序与防死循环口径（原 driver 类注全文随迁）：
    ///   回合开始（StartNewTurn 自动过 Standby → Main；Standby 分支只是防御兜底）
    ///   → 模型选动作 → 应用 → 响应窗口（v2）→ 阶段推进折返 → 下一决策点（可能是对方回合
    ///   或对方回合内的响应窗口）。
    ///   决策点两类（v2，2026-09-30 动作空间二期）：Main 停靠（回合玩家主阶段，栈空——Enumerate
    ///   全动作表）/ 响应停靠（栈非空时对优先权持有方收集候选，候选&gt;0 才停靠问模型；
    ///   vs 模式 D2c/D2d：模型座停靠、脚本座走策略响应；自对弈两座都问）。
    ///   防死循环三层：引擎速度门 + MaxActionsPerTurn（响应动作也计数，超限强排+强制 EndTurn）
    ///   + EndTurn/PassPriority 恒排动作表末位（截断兜底依赖）；L1 指纹 / L2 预算见下。
    /// </summary>
    public sealed class BattleDecisionLoop
    {
        // ==== 循环治理常量（原 driver 同值随迁；观测/动作布局与超参不动是 S1 硬约束）====
        internal const int MaxSettleAttempts = 32;    // 排干栈重试上限（镜像 SimpleAI）
        internal const int PhaseAdvanceGuard = 256;   // 阶段推进循环保险（v2 响应分支并入同一循环后加大）
        public const int ResponseWindowGuard = 64;    // 单次窗口推进的 Pass 轮数保险（driver 全脚本局残留排干共用）
        public const float NoProgressPenalty = 0.05f; // 无进展动作扣分（即时信号：引导避开无效动作；奖励归 driver）
        internal const int MaxActionsPerTurn = 256;   // 单回合动作数上限（v2 契约第 4 节 128→256：响应步计入）
        // L2 回合预算（2026-11-18 循环治理 §5.3）：单回合动作数到预算即强制收口本回合——
        // 模拟真人墙钟（AI 没有墙钟，合法循环下会无限刷步），只进 headless 驱动，不进正式规则；
        // 不判和、不改奖励（收口只损失节奏）。取值 60（用户拍板）。MaxActionsPerTurn=256 保留为硬保险。
        internal const int TurnActionBudget = 60;
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
        private bool _loopPrunedThisStep;            // L3-A：本步触发过 L1/L2 收口（BuildStop 消费后即清——仅收口那一步外显）
        private string _loopReasonThisStep;          // L3-A：收口种类（cycle/stall/budget；与上同生命周期）
        // 停滞判和（2026-09-30 v2 修复，当晚扩展）：连续 N 步动作表只剩兜底行（Main=EndTurn-only /
        // 响应停靠=PassPriority-only）→ 判和收口。64 步 ≈ 双方各 32 回合零真实选项；对手仍有任何
        // 可行动作都会使计数归零，不会误杀。完整背景见 git 历史与 docs/循环治理方案。
        private const int StallDrawThreshold = 64;
        private int _stallCounter;

        // ==== 核心引用与停靠态（原 driver 同名状态随迁）====
        private GameCore _core;
        private GameBoard.BoardState _board;
        private LegalActionEnumerator _legal;
        private bool _gameOver;
        private Player _winner;
        private string _reason;

        // 当前停靠点（BuildStop 按它构建观测与动作表；Apply 按它归属）
        private Player _decider;
        private bool _responseStop;
        private bool _discardStop; // P3/A0：手牌上限弃牌停靠（legal=DiscardChoice 行，每张手牌一行；仍超限会连续多步）

        // ---- D2d（2026-11-22 对手回合响应停靠）：脚本回合异步链 + 模型座停靠 ----
        // 脚本回合链在途（含停靠挂起——finally 才清；AdvanceToDecision 据此识别「链已停靠，
        // 不得再起第二条链」）。链 = RunExternalTurnChain 起的 TakeTurnAsync 包装，不阻塞调用线程。
        private bool _scriptTurnChainActive;
        // 当前停靠：泵 await 挂起的 TCS + 泵收集的候选表（Apply 按行→候选映射完成 TCS 续跑，
        // 续跑内联在本线程——探针实证无 player loop 依赖，见 2026-11-22 tcs_continuation_probe）。
        private Cysharp.Threading.Tasks.UniTaskCompletionSource<ResponseOption> _parkTcs;
        private List<ResponseOption> _parkOptions;

        // vs 装配引用（driver ResetCore 装配后经 BeginGame 注入）：_modelPlayer != null 时
        // 非模型回合由 _simpleAI 整回合自动打（策略每局重建）；_aiCtrl 为链的 TakeTurnAsync 宿主。
        private Player _modelPlayer;
        private BattleController _aiCtrl;
        private SimpleAI _simpleAI;

        // S2 游戏模式（2026-11-27 逐步驱动共享，用户口径：loop 只在「神经座自己的决策点」停靠，
        // 人类/非神经座的响应窗维持现状）：三处分支差异——
        // ① 对手回合不起链（宿主驱动：人类走 UI / 脚本座走各自 TakeTurn）；
        // ② End 相不折返（翻回合归宿主——镜像旧 NeuralAI/SimpleAI 的 TakeTurn 契约：
        //    AiBattleDriver 外层 CheckPhaseTransition / 活体局引擎帧泵）；
        // ③ 非决策座的响应窗：AI 座走 AiResponder（PickFor）现状链、人类座自动让过（不弹窗）。
        private bool _gameMode;

        // 回合内无进展防护：引擎拒绝（枚举/引擎口径漂移）的动作按签名本回合摘除 + 小额扣分——
        // 否则确定性策略会无限重选同一无效动作，回合冻结烧满步数上限、对局永不自然终局
        private readonly HashSet<string> _bannedThisTurn = new HashSet<string>();
        private int _banTurnStamp = -1;
        private int _actionsThisTurn;

        // ==== 宿主钩子（driver 外挂；loop 不持训练专属状态）====
        /// <summary>动作引擎应用成功后即调（推进前——回合号口径与原 driver Step 内联位一致）。
        /// driver 挂 TrackModelBehavior（B4 行为仪表：Apply 成功才计数）。</summary>
        public Action<TideAction> AfterActionApplied;
        /// <summary>S3-1 开手交棒钩子（模型座回合开始停靠口调；null=无交棒语义恒直接停靠）。
        /// driver 侧实现：交棒前起开手脚本链（ChainStarted/ChainInFlight），触发/兜底则 CompleteHandoff
        /// 后返回 None 落停靠口。（原 AdvanceToDecision 内联 handoff 块，2026-11-27 S1 换钩子。）</summary>
        public Func<ModelTurnIntercept> InterceptModelTurn;

        // ==== 终态/停靠只读口（driver 协议与奖励消费）====
        public bool IsGameOver => _gameOver;
        public Player Winner => _winner;
        public string Reason => _reason;
        /// <summary>当前停靠座次（未初始化/终局为 null——driver 侧 DecisionSeat 有兜底口径）。</summary>
        public Player Decider => _decider;
        public bool ResponseStop => _responseStop;
        /// <summary>脚本回合链在途（driver 侧 S3-1 钩子判 ChainInFlight 用）。</summary>
        public bool ScriptTurnChainActive => _scriptTurnChainActive;

        /// <summary>每局开局装配（driver ResetCore 调，须在首次 Advance/Apply 之前）：
        /// 清上局停靠链/循环治理状态（含引擎泵在途标记防御复位——旧泵 finally 永不回=false 会令
        /// 新局相位窗泵全部让路），落本局核心引用与 vs 装配。modelPlayer/opponentAI=null = 自对弈。
        /// 常量计数器（_loopBudgetCount/_loopCycleCount/_loopStallCount）只增不清（协议指标口径）。</summary>
        public void BeginGame(GameCore core, GameBoard.BoardState board, Player modelPlayer,
            BattleController aiCtrl, SimpleAI opponentAI)
        {
            _gameMode = false; // 训练模式（游戏模式走 BeginGameObjectMode 置位）
            _parkTcs = null;
            _parkOptions = null;
            _scriptTurnChainActive = false;
            if (GameCore.Instance != null) GameCore.Instance.ResponsePumpInFlight = false;
            _core = core;
            _board = board;
            _modelPlayer = modelPlayer;
            _aiCtrl = aiCtrl;
            _simpleAI = opponentAI;
            _gameOver = false;
            _winner = null;
            _reason = null;
            _decider = null;
            _responseStop = false;
            _discardStop = false;
            TargetPickGate.Reset(); // A6（2026-11-24 逐目标单选）：新对局弃残留拾取序列
            _bannedThisTurn.Clear(); // 新对局：无进展防护状态归零（stamp 由 BuildStop 兜底重置）
            _actionsThisTurn = 0;
            _budgetClosedThisTurn = false; // L2 预算收口状态归零（_loopBudgetCount 只增不清）
            _stallCounter = 0;
            _fpRing.Clear();      // L1 指纹环形缓冲归零（跨局残留会误报回环；计数器只增不清）
            _fpRingSet.Clear();
            _stallCount = 0;
            _hasLastFingerprint = false;
            _loopPrunedThisStep = false; // L3-A 单步标志归零
        }

        /// <summary>S2 游戏模式装配（NeuralAI.TakeTurn 调）：决策座=神经座（**唯一停靠位**，
        /// 口径：loop 只停神经座自己的决策点）；对手回合无链（宿主驱动：人类走 UI、脚本座走各自
        /// TakeTurn）。状态归零复用 BeginGame。同局镜像神经局换座用 <see cref="SetDecisionSeat"/>。</summary>
        public void BeginGameObjectMode(GameCore core, GameBoard.BoardState board, Player decisionSeat,
            BattleController aiCtrl)
        {
            BeginGame(core, board, decisionSeat, aiCtrl, null);
            _gameMode = true;
        }

        /// <summary>换决策座（S2 同局镜像神经局：NeuralAI 单实例轮流驱动两座——每 TakeTurn 前调；
        /// 停靠态在 RunUntilTurnEnd 入口 AdvanceToDecision 归位，无残留）。</summary>
        public void SetDecisionSeat(Player seat) => _modelPlayer = seat;

        /// <summary>上局残留停靠链清引用（driver ResetCore 前段防御位，早于 InitGame 调）：
        /// 悬挂的 TCS 不完成（不在旧核心状态上续跑，链随引用断开交 GC）；泵在途标记防御性复位
        ///（internal set 同程序集可写，旧泵 finally 永不回=false 会令新局相位窗泵全部让路）。
        /// BeginGame 会重复执行同样清理（幂等，双保险）。</summary>
        public void DetachParkedChains()
        {
            _parkTcs = null;
            _parkOptions = null;
            _scriptTurnChainActive = false;
            if (GameCore.Instance != null) GameCore.Instance.ResponsePumpInFlight = false;
        }

        /// <summary>终局事件收口（driver 的 GameOverEvent 订阅转发）：置终局三态 + 两门清理
        ///（原 driver OnGameOver 前半，2026-11-27 S1 迁入；_handoffMiss 归 driver 侧）。</summary>
        public void MarkGameOver(Player winner, string reason)
        {
            _gameOver = true;
            _winner = winner;
            _reason = reason;
            HandLimitDiscardGate.CompleteDefault(); // P3 §4.1：终局释放挂起弃牌（防泄漏死等）
            TargetPickGate.Reset(); // A6：终局弃拾取序列（纯驱动层状态，直接清）
        }

        /// <summary>直接置终局（driver 全脚本局 MaxTurns/GameTimeout/SimpleAIError 收口用——
        /// 不经事件、不做门清理，与原 driver 直接字段写逐位一致）。</summary>
        public void ForceTerminal(Player winner, string reason)
        {
            _gameOver = true;
            _winner = winner;
            _reason = reason;
        }

        /// <summary>
        /// 执行一步动作应用 → 推进 → L1 指纹检测（原 driver Step L734-947 骨架逐行迁移；
        /// 奖励/仪表/协议组装留在 driver）。返回状态码：非 Advanced 的四类对应原早退
        /// BuildResult(0f/扣分) 路径——driver 据此给 0 奖励或拒绝扣分，不再推进（本方法内已不推进）。
        /// </summary>
        public LoopStepOutcome Apply(int actionIndex)
        {
            var me = _decider ?? _core.TurnEngine.TurnPlayer; // 本步决策座次（Main=回合玩家 / 响应=优先权方）

            // 越界下标：保守兜底——不推进、奖励 0，重观测同状态。但必须计入停滞与回合动作数：
            // 冻结态下模型全掩码均匀采样，每步仅 1/512 概率落在唯一兜底行上，其余全走本分支——
            // 不计数则 StallDraw 永远到不了阈值（64×512≈3.3 万步空转），2026-09-30 复发实证。
            if (_legal == null || actionIndex < 0 || actionIndex >= _legal.Actions.Count)
            {
                TargetPickGate.Reset(); // A6：越界=弃拾取序列（闸内表恒非空，越界必为异常采样）
                // D2d：停靠步越界也必须放行泵（否则链悬挂死锁后续对局）——按让过完成 TCS
                if (_parkTcs != null)
                {
                    var stale = _parkTcs;
                    _parkTcs = null;
                    _parkOptions = null;
                    stale.TrySetResult(null); // 内联续跑（可能再次停靠——BuildStop 渲染新停靠）
                }
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
                return new LoopStepOutcome { Status = LoopStepStatus.OutOfBounds };
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
                    return new LoopStepOutcome { Status = LoopStepStatus.StallDraw };
                }
            }
            else
            {
                _stallCounter = 0;
            }

            // A6（2026-11-24 逐目标单选）：多步拾取序列——首步行（PickPlan!=null）开闸并记录
            // 第一拾取；闸内拾取步不 Apply/不推进（引擎状态冻结，BuildStop 重枚举闸行——不走
            // AdvanceToDecision，它会推进相位；指纹也跳过：状态未变必误报回环）。
            // 提交行（[10]=0 且 Targets 非空）→ Submit 锁定当前可变 spec；Fixed 收满自动前移。
            // 收口后 BuildFinal 按计划序规范化拼全集塞回 a：主表来源落入下方 Apply 链；
            // D2d 停靠来源（闸记忆 ResponseOption）写回目标集后 TrySetResult——泵内联应用并推进，
            // 不走下方动作链（与 _parkTcs 分支的双应用防护同理）。
            // v22 S2a（2026-11-26 二期 S2a）：**选项提交行**（PickMode=1，Targets=null）走
            // OpenAndAutoComplete 一次性完成（共享序选满+BuildFinal），随后与逐步收口同流——
            // 主表来源落入下方 Apply 链、停靠来源完成 TCS 泵内联应用。逐步行（PickMode=0）不变。
            bool gatePumpApplied = false;
            if (a.PickPlan != null && !TargetPickGate.HasPending)
            {
                if (a.PickMode == 1)
                {
                    a = TargetPickGate.OpenAndAutoComplete(_core, me, a, ParkedOptionAt(actionIndex));
                    var macroParked = TargetPickGate.ConsumeParked();
                    if (macroParked != null)
                    {
                        macroParked.Targets = a.Targets;          // 声明期预选目标=共享序全集（同 D2d 口径）
                        macroParked.DynamicCounts = a.DynamicCounts;
                        var gateTcs = _parkTcs;
                        _parkTcs = null;
                        _parkOptions = null;
                        gateTcs?.TrySetResult(macroParked); // 泵内联应用并推进（可能再次停靠）
                        gatePumpApplied = true;
                    }
                }
                else
                {
                    TargetPickGate.Open(a, ParkedOptionAt(actionIndex)); // 首步行目标=第一拾取
                    return new LoopStepOutcome { Status = LoopStepStatus.GateOpen }; // 门控行恒 ≥2 步或含可变——开闸后必有余步
                }
            }
            if (TargetPickGate.HasPending)
            {
                if (a.PicksRemaining == 0 && a.Targets != null && a.Targets.Count > 0)
                    TargetPickGate.Submit(); // 提交行：锁定当前可变 spec（末位=序列收口）
                else
                    TargetPickGate.Pick(a); // 拾取步：本步只收 1 个目标
                if (!TargetPickGate.Completed)
                    return new LoopStepOutcome { Status = LoopStepStatus.GatePending }; // 还有剩余步：重建闸表（状态不变，不指纹不计奖励）
                a = TargetPickGate.BuildFinal(_core, me);
                var parkedChoice = TargetPickGate.ConsumeParked();
                if (parkedChoice != null)
                {
                    parkedChoice.Targets = a.Targets;          // 声明期预选目标=规范化全集（同 D2d 分支口径）
                    parkedChoice.DynamicCounts = a.DynamicCounts;
                    var gateTcs = _parkTcs;
                    _parkTcs = null;
                    _parkOptions = null;
                    gateTcs?.TrySetResult(parkedChoice); // 泵内联应用并推进（可能再次停靠）
                    gatePumpApplied = true;
                }
            }

            // L2 回合预算收口（2026-11-18 循环治理 §5.3）：尽力收口——排干栈 → Main 相强 EndTurn；
            // EndTurn 可能被引擎拒绝（阶段/栈态），此时只打日志不重试（MaxActionsPerTurn=256 硬保险兜底）。
            // D2d：停靠步不收口（本步完成 TCS 后链内联自推进；停靠链上强排干/EndTurn 会与挂起泵
            // 双主冲突——速度门+ResponseWindowGuard+StallDraw 已兜住停靠链的有界性）。
            if (_actionsThisTurn >= TurnActionBudget && !_budgetClosedThisTurn && _parkTcs == null)
            {
                _budgetClosedThisTurn = true;
                _loopBudgetCount++;
                _loopPrunedThisStep = true; // L3-A：本步即收口步（info.loopPruned）
                _loopReasonThisStep = "budget";
                CloseTurnByLoopGuard();
                Console.WriteLine($"[LoopGuard] budget turn={_core.TurnEngine.TurnNumber} seat={SeatOf(_decider)} actions={_actionsThisTurn} n={_legal?.Count ?? 0} hand={_core.ZoneManager.GetCards(_decider, Zone.Hand)?.Count ?? 0}");
            }

            if (!gatePumpApplied && _parkTcs != null)
            {
                // D2d（2026-11-22 对手回合响应停靠）续跑：本步=脚本回合响应窗的模型选择。
                // 所选行按 ParkedOptionOfRow 映射回泵收集的 ResponseOption（RespondPlay 行的
                // Targets/DynamicCounts 写回候选——泵 PlayCardInResponse 按声明期预选目标消费）；
                // PassPriority 兜底行=让过（null）。TrySetResult 后泵在本线程内联应用并推进：
                // 可能再次停靠（AdvanceToDecision 见链在途直接返回，BuildStop 渲染新停靠）或
                // 整回合完成（链内折返相位）→ 公共尾推进到下一决策点。应用不走下方动作链
                // （LegalActionEnumerator.Apply 会与泵的 ApplyResponse 双应用）。
                var tcs = _parkTcs;
                var opts = _parkOptions;
                _parkTcs = null;
                _parkOptions = null;
                ResponseOption choice = null;
                if (a.Type != TideActionType.PassPriority)
                {
                    int oi = _legal.ParkedOptionOfRow[Math.Min(actionIndex, _legal.ParkedOptionOfRow.Count - 1)];
                    if (oi >= 0 && oi < opts.Count)
                    {
                        choice = opts[oi];
                        if (a.Type == TideActionType.RespondPlay)
                        {
                            choice.Targets = a.Targets;          // 声明期预选目标随行（2026-10-20 口径）
                            choice.DynamicCounts = a.DynamicCounts;
                        }
                    }
                }
                if (Environment.GetEnvironmentVariable("TIDE_DEBUG_D2D") == "1")
                    Console.WriteLine($"[D2d-resume] turn={_core.TurnEngine.TurnNumber} choice={(choice == null ? "pass" : choice.Kind.ToString())}");
                tcs.TrySetResult(choice);
            }
            else if (_actionsThisTurn > MaxActionsPerTurn)
            {
                // 保险：同回合动作数超限（如换状态翻转死循环、模型互挂响应）→
                // 强制收口（响应窗口先强排干，Main 再强制 EndTurn），保证回合流动（疲劳/战斗最终能终结对局）
                TargetPickGate.Reset(); // A6：强收前弃拾取序列（未 Apply 的卡声明不存在，直接收口）
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
                // P3（2026-11-19 §4.1 建·A0 2026-11-24 逐张弃）：本步只交 1 张——
                // EnforceHandLimitAsync 余段（单卡入墓+CardDiscardEvent）在 Complete 调用线程
                // 内联继续，随后重取手牌/重算上限，仍超限会再挂起（AdvanceToDecision 循环顶
                // 再停靠 → 连续弃牌步，9 张/上限 7 恰 2 步）；钳到 1 张防多弃
                var snap = HandLimitDiscardGate.Snapshot();
                var picked = new List<Card>();
                if (a.Targets != null)
                    foreach (var e2 in a.Targets)
                        if (e2 is Card dc && snap.cards != null && snap.cards.Contains(dc)
                            && picked.Count < 1)
                            picked.Add(dc);
                HandLimitDiscardGate.Complete(picked);
                _discardStop = false;
            }
            else
            {
                bool ok = LegalActionEnumerator.Apply(_core, me, a);
                if (!ok)
                {
                    // 引擎拒绝（枚举/引擎口径漂移）：本回合摘除该动作（重观测不再出现），
                    // 不推进。摘除保证最坏情况把无效动作各试一次后只剩 EndTurn/Pass → 回合必然流动。
                    // 扣分归 driver（λ=0 基线不扣分——保持「reward 只有终局 ±1」纯终局口径）。
                    _bannedThisTurn.Add(a.Signature);
                    _legal.RemoveAll(_bannedThisTurn);
                    return new LoopStepOutcome { Status = LoopStepStatus.Rejected };
                }
                // 应用成功：不立即 DrainStack——对方回合响应窗口由 AdvanceToDecision 的
                // 响应循环接管（候选>0 停靠问模型 / 候选=0 自动让过 = 旧双 Pass 行为）。
                // 行为仪表（B4）经钩子即时计（推进前——回合号口径与原内联位一致）。
                AfterActionApplied?.Invoke(a);
            }

            // 推进到下一决策点（End→Standby 折返 / Standby→Main / 回合初横置 / 响应窗口；可能触发疲劳判负）
            AdvanceToDecision();

            // L1 指纹检测（2026-11-18 循环治理 §5.2）：净零循环/停滞 → 收口本回合。只查 Main 相
            // 新决策点；放在 BuildStop 之前——收口可能推进回合/触发终局（疲劳），
            // 观测为收口后决策点（不返回已作废的循环态动作表）。
            // 拒绝/越界两条早退路径不检测：状态原地不动、指纹必在本回合集合里，检测=每次必误报 cycle；
            // 那两条路径已有摘除 + StallDrawThreshold 兜底。
            CheckLoopFingerprint(a);

            return new LoopStepOutcome { Status = LoopStepStatus.Advanced };
        }

        /// <summary>
        /// 推进到下一决策点（v2 泛化：Main 停靠 + 响应停靠）：
        ///   栈非空（或空栈待发先上栈）→ 响应窗口：候选&gt;0 停靠（decider=优先权方），
        ///     候选=0 驱动自动 Pass 直到窗口关闭（双 Pass 结算）——与旧 DrainStack 双 Pass 语义等价；
        ///   栈空 → 相位推进：若仍处 Standby 走兼容口 SkipElementPool 入 Main（正常流程 StartNewTurn
        ///     已自动推进，此分支仅防御）→ Main 停靠（decider=回合玩家）；
        ///     End → CheckPhaseTransition → 下一回合 Standby（循环折返）。
        /// vs SimpleAI 模式：非模型回合不逐相位推进，整回合交 SimpleAI 异步链（D2d：TakeTurnAsync
        /// 不阻塞，链内响应窗经 OnEngineAsyncResponse 停靠模型座）；模型回合内的响应窗口按 D2c
        /// 口径处理（模型座停靠/脚本座策略响应，见 TryResolveResponseWindow）。
        /// S3-1（2026-11-27 S1 换钩子）：模型座回合开始的交棒评估由 InterceptModelTurn 钩子承担
        ///（driver 侧 HandoffTriggerMet/CompleteHandoff/开手脚本链——原内联块三态语义逐位保留）。
        /// </summary>
        public void AdvanceToDecision()
        {
            for (int i = 0; i < PhaseAdvanceGuard && !_gameOver; i++)
            {
                // P3（2026-11-19 §4.1 建·A0 2026-11-24 逐张弃）手牌上限弃牌停靠：模型座挂起请求
                // 先于一切推进停靠（回合末事件在 EndTurn/相位泵内已同步 Park；停靠步由 Apply 应用
                // DiscardChoice 释放——释放后仍超限会再次 Park，本循环自然形成连续弃牌步）
                if (HandLimitDiscardGate.HasPending)
                {
                    var snap = HandLimitDiscardGate.Snapshot();
                    _decider = snap.player ?? _core.TurnEngine.TurnPlayer;
                    _responseStop = true; // 决策上下文=窗口语义（g[49]=1）
                    _discardStop = true;
                    return;
                }

                var turnPlayer = _core.TurnEngine.TurnPlayer;

                // vs SimpleAI：对手回合整段自动打（D2d 异步链——TakeTurnAsync 自带回合准备与 EndTurn，
                // 不折返）。链停靠模型座（Apply 续跑后本步内再停靠）→ 停靠即决策点，直接返回；
                // 整回合无停靠 = 链同步跑完（相位已在链内折返）→ 循环重查。
                if (_modelPlayer != null && turnPlayer != _modelPlayer)
                {
                    if (_simpleAI == null) return;      // S2 游戏模式：对手回合归宿主驱动（人类 UI/脚本座各自 TakeTurn）——不起链
                    if (_scriptTurnChainActive) return; // D2d：链已停靠——不得再起第二条链
                    RunSimpleAiTurnAsync();
                    if (_parkTcs != null) return;       // D2d：脚本回合响应窗停靠模型座
                    continue;
                }

                // S3-1 开手交棒钩子位（原内联 handoff 块，2026-11-27 S1 抽钩子——三态语义不变）：
                // 交棒前模型座回合整段交开手脚本（ChainStarted——防御停车判定后重查；ChainInFlight=
                // 开手链在途防御 return）；触发/兜底交棒后返回 None 落本停靠口问模型。
                if (_modelPlayer != null && ReferenceEquals(turnPlayer, _modelPlayer)
                    && InterceptModelTurn != null)
                {
                    switch (InterceptModelTurn())
                    {
                        case ModelTurnIntercept.ChainInFlight:
                            return; // 开手链在途（防御——链上不应再停靠）
                        case ModelTurnIntercept.ChainStarted:
                            if (_parkTcs != null) return; // 防御：开手链不应停靠（模型座回合语境）
                            continue;
                    }
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
                    if (_gameMode) return; // S2 游戏模式：End 相不折返——翻回合归宿主（旧 TakeTurn 契约）
                    _core.TurnEngine.CheckPhaseTransition();
                    continue;
                }
                return; // 未知相位兜底（不应发生）
            }
        }

        /// <summary>
        /// 响应窗口推进：对当前优先权方轮询——
        ///   vs 模式（D2c 2026-11-22）：模型座候选&gt;0 停靠问模型 / 脚本座策略响应（PickFor），
        ///     双侧无候选（或策略不作主）自动 Pass；
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

                // vs 模式（D2c 2026-11-22 模型侧响应停靠，取代旧「双侧自动让过」）：
                //   模型座 = 候选>0 停靠问模型（判据与自对弈分支同款；停靠/续跑复用自对弈
                //   已跑通路径——BuildStop/EnumerateResponses/Apply 全按 _decider 驱动，g[49]=1）；
                //   脚本座 = 策略响应（ResetCore 装配的 AiResponder=PickFor，经 GameActions.
                //   TryApplyAiResponse 单轮决策+应用——与 AiDuelRunner/TideSim 同源链）；
                //   无候选/不作主/应用失败 → Pass（双 Pass 触发结算推进窗口）。
                // 不变项：ResponseWindowGuard 轮数保险、速度门（候选收集内含）、
                // MaxActionsPerTurn/循环治理、观测布局与动作空间（零改动）。
                if (_modelPlayer != null)
                {
                    if (ReferenceEquals(holder, _modelPlayer))
                    {
                        if (GameActions.CollectAvailableResponses(_core, holder).Count > 0
                            || LegalActionEnumerator.HasRespondableHandCard(_core, holder))
                        {
                            _decider = holder;
                            _responseStop = true;
                            return false; // 停靠：模型响应决策点（decisionContext=1）
                        }
                    }
                    else if (holder.IsAI && GameActions.TryApplyAiResponse(_core, holder))
                    {
                        // 脚本座策略响应发动成功 → 优先权已翻转，继续轮询。
                        // S2 游戏模式：人类座（IsAI=false）不经策略口——直接落到下方 PassPriority
                        // 自动让过（口径：人类响应窗维持现状=神经座回合内不弹窗；训练侧双座恒
                        // IsAI=true，本守卫为死代码，零漂移）。
                        continue;
                    }
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
        /// SimpleAI 整回合驱动（D2d 2026-11-22 异步化；2026-11-27 S1 随决策循环迁入 loop 并参数化）：
        /// TakeTurnAsync 起链**不阻塞**调用线程——链内响应窗（出牌/攻宣/自愿桶）经引擎泵问
        /// AiAsyncResponder：模型座停靠 TCS（OnEngineAsyncResponse），脚本座同步策略响应。
        /// 无候选整回合 = 全链同步跑完（PacingWait=null 契约，时序/线程与旧同步口一致）；
        /// 有停靠 = 链挂起、控制返回调用方（AdvanceToDecision 见 _parkTcs 即返），模型 Apply 的
        /// TrySetResult 后续跑**内联在本线程**（2026-11-22 探针实证，无 player loop 依赖）。
        /// 链内完成 End→Standby 折返（原同步口的补泵时点）；异常按 SimpleAIError 收口
        /// （无胜者 → Python 记平局，不炸训练服务）。driver 侧 S3-1 开手脚本回合经
        /// RunExternalTurnChain(_openingAi, "开手脚本") 复用同一链结构。
        /// </summary>
        private void RunSimpleAiTurnAsync() => RunExternalTurnChain(_simpleAI, "SimpleAI ");

        /// <summary>外链回合驱动（vs 脚本对手回合 / driver 侧开手脚本回合共用骨架）。
        /// logLabel 仅影响异常日志前缀（"SimpleAI " / "开手脚本"——与原两条内联方法逐字一致）。</summary>
        public void RunExternalTurnChain(SimpleAI ai, string logLabel)
        {
            _scriptTurnChainActive = true;
            RunWrapAsync().Forget();
            async Cysharp.Threading.Tasks.UniTask RunWrapAsync()
            {
                try
                {
                    await ai.TakeTurnAsync(_aiCtrl);
                    if (!_gameOver)
                        _core.TurnEngine.CheckPhaseTransition();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[TideHeadless] {logLabel}回合异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                    _gameOver = true;
                    _winner = null;
                    _reason = "SimpleAIError";
                }
                finally
                {
                    _scriptTurnChainActive = false;
                }
            }
        }

        /// <summary>
        /// D2d 引擎异步响应口（driver ResetCore vs 分支注册为 ResponseWindowService.AiAsyncResponder）：
        /// **脚本回合**（链在途且回合方=脚本——排除链尾折返后模型回合的相位窗泵，防双主停靠冲突）
        /// 的模型座 → 停靠（_parkTcs 挂起泵，停靠观测/续跑复用响应停靠契约：_decider=模型、
        /// _responseStop=true、g[49]=1）；其余语境（脚本座 / 模型自己回合的相位窗泵 / 自对弈残留）
        /// → 返回**完成态**的同步决策 DecideAiResponse（=未注册时的原路，零漂移）。
        /// </summary>
        public Cysharp.Threading.Tasks.UniTask<ResponseOption> OnEngineAsyncResponse(
            GameCore core, Player holder, List<ResponseOption> options)
        {
            if (_scriptTurnChainActive && _modelPlayer != null
                && ReferenceEquals(holder, _modelPlayer)
                && !ReferenceEquals(core.TurnEngine.TurnPlayer, _modelPlayer))
            {
                _parkTcs = new Cysharp.Threading.Tasks.UniTaskCompletionSource<ResponseOption>();
                _parkOptions = options;
                _decider = holder;
                _responseStop = true;
                if (Environment.GetEnvironmentVariable("TIDE_DEBUG_D2D") == "1")
                    Console.WriteLine($"[D2d-park] turn={core.TurnEngine.TurnNumber} opts={options.Count} "
                        + $"[{string.Join(",", options.Select(o => o == null ? "?" : o.Kind.ToString()))}]");
                return _parkTcs.Task;
            }
            return Cysharp.Threading.Tasks.UniTask.FromResult(GameActions.DecideAiResponse(core, holder, options));
        }

        // ===================================================== S2 游戏模式（2026-11-27 逐步驱动共享） =====

        /// <summary>S2 游戏模式主入口（计划 §2「RunUntilTurnEnd(policy)」，NeuralAI.TakeTurn 调）：
        /// 驱动**当前回合玩家（=决策座，神经座）**的整个回合——推进到停靠（Main / 响应窗 /
        /// A6 拾取步 / 终局）→ policy 决策 → 应用，直到回合收口（End 相——不折返，翻回合归宿主，
        /// 镜像旧 NeuralAI/SimpleAI 的 TakeTurn 契约：AiBattleDriver 外层 CheckPhaseTransition /
        /// 活体局引擎帧泵）或终局。
        /// 口径（用户 2026-11-27 定案）：loop 只停神经座自己的决策点——
        ///   · 非决策座的响应窗：AI 座走 AiResponder（PickFor）现状链、人类座自动让过（不弹窗）；
        ///   · 对手回合（宿主驱动）内神经座的响应窗：宿主泵经 ResponseWindowService.AiAsyncResponder
        ///     问本 loop（<see cref="DecideResponseInline"/>——训练 D2d 等价）。
        /// 停靠语义与训练逐点一致（观测/动作表/闸步/L1-L3 收口同源）——「逐步驱动=唯一口径」。</summary>
        public void RunUntilTurnEnd(IBattlePolicy policy)
        {
            var seat = _modelPlayer;
            if (seat == null || _core == null) return;
            if (!ReferenceEquals(_core.TurnEngine.TurnPlayer, seat)) return; // 入口守卫（镜像旧 NeuralAI.TakeTurn）
            AdvanceToDecision(); // 入口归位停靠态（清上一停靠残留 _decider/_responseStop；泵 Standby/相位窗/终局触发）
            int guard = 0;
            while (!_gameOver && ReferenceEquals(_core.TurnEngine.TurnPlayer, seat))
            {
                if (_core.TurnEngine.CurrentPhase?.Phase == PhaseType.End) break; // 回合已收口——翻回合归宿主
                var stop = BuildStop();
                if (stop.Legal.Count == 0) break; // 不应发生（AppendFallback 兜底行恒在）——防御出口
                int idx = policy.Select(stop.Obs, stop.Legal);
                if (idx < 0 || idx >= stop.Legal.Count) idx = stop.Legal.Count - 1; // 决策失败=末位兜底行（EndTurn/PassPriority 恒排末位）
                var outcome = Apply(idx);
                if (outcome.Status == LoopStepStatus.GateOpen || outcome.Status == LoopStepStatus.GatePending)
                    continue; // A6 闸步：Apply 内已不推进/不折相位——BuildStop 重枚举闸表，逐步问 policy
                if (++guard > 1024) break; // 防御保险（MaxActionsPerTurn/L1-L3 在 Apply 内已收口）
            }
        }

        /// <summary>S2 游戏模式内联响应决策（宿主泵经 ResponseWindowService.AiAsyncResponder 调——
        /// 对手回合内神经座的响应窗停靠，训练 D2d 的游戏侧等价：不停靠 TCS，停靠即决策）：
        /// 泵给定候选 1:1 枚举行（EnumerateParkedResponses，与训练停靠表同源）→ policy 选行 →
        /// 行→ResponseOption 映射；含 A6 多步拾取（逐步问 policy 直到收口——镜像训练 Apply 的
        /// 闸分支语义）。返回 null=让过（PassPriority 兜底行/防御路径）。窗口期引擎状态冻结，
        /// 本方法零引擎副作用（只动 TargetPickGate 静态态，路径出口全部清闸）。</summary>
        public ResponseOption DecideResponseInline(Player holder, List<ResponseOption> options, IBattlePolicy policy)
        {
            if (holder == null || options == null || options.Count == 0 || _core == null)
                return null; // 防御：空候选=让过（泵在候选=0 时本就不问）
            var legal = new LegalActionEnumerator();
            legal.EnumerateParkedResponses(_core, holder, options);
            if (legal.Count == 0) return null;
            int idx = policy.Select(TideObservation.Build(_core, holder, true, _board), legal);
            if (idx < 0 || idx >= legal.Actions.Count) return null; // 决策失败=让过（保守）
            var a = legal.Actions[idx];
            if (a.Type == TideActionType.PassPriority) return null; // 兜底行=让过

            ResponseOption OptionOf(int row)
            {
                int oi = row >= 0 && row < legal.ParkedOptionOfRow.Count ? legal.ParkedOptionOfRow[row] : -1;
                return oi >= 0 && oi < options.Count ? options[oi] : null;
            }

            // A6：多步拾取（首步行带 PickPlan）——逐步问 policy（镜像训练 Apply 闸分支；
            // v22 S2a 选项行 PickMode=1 走 OpenAndAutoComplete 一次性完成）
            if (a.PickPlan != null && !TargetPickGate.HasPending)
            {
                if (a.PickMode == 1)
                {
                    a = TargetPickGate.OpenAndAutoComplete(_core, holder, a, OptionOf(idx));
                    var macro = TargetPickGate.ConsumeParked();
                    if (macro != null)
                    {
                        macro.Targets = a.Targets;          // 共享序全集（同 D2d 口径）
                        macro.DynamicCounts = a.DynamicCounts;
                        return macro;
                    }
                    // 开闸失败防御：原行直发——落入下方主映射（a 已是完成态）
                }
                else
                {
                    TargetPickGate.Open(a, OptionOf(idx)); // 首步行目标=第一拾取
                    int spin = 0;
                    while (TargetPickGate.HasPending && !TargetPickGate.Completed)
                    {
                        if (++spin > 128) { TargetPickGate.Reset(); return null; } // 防御保险：弃闸=让过
                        var gl = LegalActionEnumerator.EnumeratePickSteps(_core, holder);
                        if (gl.Count == 0) { TargetPickGate.Reset(); return null; }
                        int gi = policy.Select(TideObservation.Build(_core, holder, true, _board), gl);
                        if (gi < 0 || gi >= gl.Count) { TargetPickGate.Reset(); return null; }
                        var ga = gl.Actions[gi];
                        if (ga.PicksRemaining == 0 && ga.Targets != null && ga.Targets.Count > 0)
                            TargetPickGate.Submit(); // 提交行：锁定当前可变 spec
                        else
                            TargetPickGate.Pick(ga); // 拾取步：收 1 个目标
                    }
                    if (TargetPickGate.Completed)
                    {
                        a = TargetPickGate.BuildFinal(_core, holder);
                        var parked = TargetPickGate.ConsumeParked();
                        if (parked != null)
                        {
                            parked.Targets = a.Targets;          // 规范化全集（同 D2d 分支口径）
                            parked.DynamicCounts = a.DynamicCounts;
                            return parked;
                        }
                        TargetPickGate.Reset(); // parked 缺失防御：弃闸
                        return null;
                    }
                    TargetPickGate.Reset(); // 未收口防御（Pick 内部弃闸等）：让过
                    return null;
                }
            }
            var choice = OptionOf(idx);
            if (choice != null && a.Type == TideActionType.RespondPlay)
            {
                choice.Targets = a.Targets;          // 声明期预选目标随行（2026-10-20 口径，镜像泵续跑）
                choice.DynamicCounts = a.DynamicCounts;
            }
            return choice;
        }

        private int SeatOf(Player p)
            => p == null || _core == null ? -1 : (ReferenceEquals(p, _core.Player1) ? 0 : 1);

        /// <summary>A6（2026-11-24 逐目标单选）：停靠表所选行 → 泵候选对象（D2d 停靠来源的
        /// ResponseOption 引用，闸提交时写回规范化目标集）；非停靠表/兜底行 = null。</summary>
        private ResponseOption ParkedOptionAt(int row)
        {
            if (_parkTcs == null || _parkOptions == null || _legal == null) return null;
            var map = _legal.ParkedOptionOfRow;
            if (row < 0 || row >= map.Count) return null;
            int oi = map[row];
            return oi >= 0 && oi < _parkOptions.Count ? _parkOptions[oi] : null;
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
            // P3 §4.1 建·A0 2026-11-24 逐张弃：强收路径释放挂起弃牌（CompleteDefault 内部循环
            // 放行快照首卡直到不再挂起）——防门泄漏卡死后续停靠
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

        /// <summary>
        /// 构建当前停靠点的观测+动作表+外显计数（原 driver BuildResult 前半，2026-11-27 S1 迁入；
        /// TideStepResult 协议组装留在 driver）。副作用与原一致：L3-A 单步外显消费即清、
        /// 回合切换时清摘除表/动作计数/L1 环形缓冲、四路枚举分发（弃牌/闸内/停靠/Main）+ 摘除 + 兜底行。
        /// </summary>
        public BattleDecisionStop BuildStop()
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

            // v3：传入棋盘占用层——观测位置特征（单位格 TryGetCell；board 已在 BeginGame 接线）
            var obs = TideObservation.Build(_core, _decider, _responseStop, _board);
            _legal = new LegalActionEnumerator();
            if (_discardStop)
            {
                // P3 §4.1 建·A0 2026-11-24 逐张弃：弃牌停靠步——legal=DiscardChoice 行
                //（每张手牌一行，独立枚举口；空手防御行保底恒 ≥1 行，不补
                // PassPriority/EndTurn 兜底防误用）
                var snap = HandLimitDiscardGate.Snapshot();
                if (snap.cards != null)
                    _legal = LegalActionEnumerator.EnumerateDiscardChoices(_core, _decider, snap.cards);
            }
            else if (TargetPickGate.HasPending)
            {
                // A6（2026-11-24 逐目标单选）：闸内拾取步——行集=当前 spec 剩余候选 + 可变档提交行
                //（独立枚举口 EnumeratePickSteps；不折叠/不摘除/无兜底行——闸内表恒非空，
                // 防御强制提交行保底；先于 _responseStop——停靠表内开闸的序列也走闸表）
                _legal = LegalActionEnumerator.EnumeratePickSteps(_core, _decider);
            }
            else if (_responseStop)
            {
                // D2d：脚本回合停靠步——行集来自泵给定候选（1:1 映射，Apply 据此完成 TCS）；
                // 自对弈/D2c 模型回合停靠照旧重收集
                if (_parkTcs != null) _legal.EnumerateParkedResponses(_core, _decider, _parkOptions);
                else _legal.EnumerateResponses(_core, _decider);
            }
            else _legal.Enumerate(_core, _decider);
            if (!_discardStop && !TargetPickGate.HasPending)
            {
                _legal.RemoveAll(_bannedThisTurn); // 本回合已判无效的动作不再出现在选项表
                _legal.AppendFallback(_responseStop); // 空表冻结兜底（2026-09-30：停靠态与引擎瞬态错位/摘除清空时补 PassPriority/EndTurn 保流动）
            }
            return new BattleDecisionStop
            {
                Obs = obs,
                Legal = _legal,
                Decider = _decider,
                ResponseStop = _responseStop,
                LoopPruned = prunedThisStep,       // L3-A：收口那一步 true（协议 info.loopPruned）
                LoopReason = reasonThisStep,       // 协议 info.loopReason（cycle/stall/budget/""）
                TurnActions = turnActionsAtBuild,  // 协议 info.turnActions（本回合已用步数）
            };
        }
    }
}
