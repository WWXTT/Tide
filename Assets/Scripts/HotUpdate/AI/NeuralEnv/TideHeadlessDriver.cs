using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCore;
using Cysharp.Threading.Tasks; // D2d：RunWrapAsync().Forget()（UniTask 扩展）
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
        // ---- 2026-11-23 模型行为仪表（B4 用户口径：「牺牲 + 回收」与 14 费大哥落地）----
        /// <summary>本局模型侧「牺牲」发动次数（Apply 成功才计）。</summary>
        public int ModelSacrifices;
        /// <summary>本局模型侧「归根」打出次数（Apply 成功才计）。</summary>
        public int ModelRecalls;
        /// <summary>本局是否走通「先牺牲 → 后归根」链（1=走通，0=未走通）。</summary>
        public int ModelChain;
        /// <summary>本局模型侧「系命者 阿格洛斯」（14 费）落地次数。</summary>
        public int ModelAgros;
        /// <summary>首次落地时的全局回合号（0=从未落地）。绿自家 T5 ≈ 全局第 9(P1)/10(P2) 回合。</summary>
        public int ModelAgrosTurnMin;
        // ---- 2026-11-23 追加：绿成长 / 红法术线（用户口径：招牌操作是否真的学到）----
        /// <summary>绿「沃土」成功打出次数（PlayCard，地落不计）——地成长指标。</summary>
        public int ModelGrow;
        /// <summary>全局 T1 首次决策时的地数（-1=未记录）。</summary>
        public int ModelLandT1;
        /// <summary>全局 T3（≥3 回合）首次决策时的地数（-1=未记录）。</summary>
        public int ModelLandT3;
        /// <summary>红「血偿」成功打出次数。</summary>
        public int ModelBlood;
        /// <summary>红「活体炸弹」成功打出次数。</summary>
        public int ModelBomb;
        /// <summary>是否有过「同回合血偿+炸弹都打出」的斩杀尝试（1=有）。</summary>
        public int ModelCombo;
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
    ///    vs 模式（D2c 2026-11-22，取代旧「双侧自动让过」）：模型座同样停靠问模型（候选>0）；
    ///    脚本座走策略响应（AiResponder=PickFor，与 AiDuelRunner 同源）。
    ///    D2d（2026-11-22 对手回合响应停靠）：脚本回合改异步链（TakeTurnAsync 不阻塞），链内
    ///    响应窗经引擎 AiAsyncResponder 对**模型座也停靠**（Step 完成映射回的 ResponseOption
    ///    续跑，内联无 player loop 依赖）——响应类动作（乱锋/冻结/回根/回收）在 vs 模式
    ///    双方回合内可学可用。
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

        // ---- D2d（2026-11-22 对手回合响应停靠）：脚本回合异步链 + 模型座停靠 ----
        // 脚本回合链在途（含停靠挂起——finally 才清；AdvanceToDecision 据此识别「链已停靠，
        // 不得再起第二条链」）。链 = RunSimpleAiTurnAsync 起的 TakeTurnAsync 包装，不阻塞调用线程。
        private bool _scriptTurnChainActive;

        // ---- 2026-11-23 模型行为仪表（B4 用户口径）：卡 ID 精确计数，Apply 成功才计 ----
        // 与 Python 侧 action_usage_stats 的「选中了某行」口径互补：那边回答「采样有没有覆盖」，
        // 这边回答「真的落地没有」。每局 ResetCore 清零，随 info 回传（协议 mSac/mRec/mChain/…）。
        private const string IdGreenSacrifice = "C_AC7FC01F"; // 绿英雄技能「牺牲」
        private const string IdGreenRecall = "C_D1A00009";    // 绿「归根」（牌库 11 张，8 费）
        private const string IdGreenAgros = "C_B0EF3C08";     // 绿「系命者 阿格洛斯」（14 费）
        private int _mSac, _mRec, _mAgros, _mAgrosTurnMin, _mChain;
        private bool _mSacSeen;
        // 2026-11-23 战斗伤害计价：本步内「打到我 / 打到对手」的战斗伤害点数（Φ 已按生命 1.0 计过，
        // 末尾按 −0.5 修正到用户口径的 0.5 费/点；法术/效果伤害不在此列，保持 1.0）
        private int _combatDmgSelfStep, _combatDmgOppStep;

        // ---- 2026-11-23 追加（用户口径：红/绿的「招牌操作」是否真的学到）----
        // 绿：成长 = 沃土打出（PlayCard，不是地落）+ 全局 T1/T3 的地数快照；
        // 红：血偿 + 活体炸弹 各打了多少张 + 是否有"同回合两法术都打"的斩杀尝试。
        private const string IdGreenGrow = "C_A92F5E99";   // 绿「沃土」（6 费，铺地成长）
        private const string IdRedBlood = "C_E105FABA";   // 红「血偿」（3 费，直伤 5）
        private const string IdRedBomb = "C_5705F9FE";    // 红「活体炸弹」（0 费，参照攻伤害）
        private int _mGrow, _mLandT1, _mLandT3;
        private int _mBlood, _mBomb, _mCombo;
        private int _mSpellTurn = -1;      // 本轮记录的血偿/炸弹所属回合
        private bool _mBloodThisTurn, _mBombThisTurn;

        /// <summary>地数（地牌池张数）——成长指标口径：池里每张=一线永久产出。</summary>
        private int LandCountOf(Player p)
            => _core?.ElementPool?.GetPooledCards(p)?.Count ?? 0;

        /// <summary>2026-11-23：回合边界快照地数（全局 T1 首次决策 = T1 地数；T3 同理）。
        /// 在 Step 入口调（Apply 之前），保证记的是"本步动手前"的盘面。</summary>
        private void SnapshotGrowth(Player me)
        {
            if (me == null) return;
            int t = _core.TurnEngine.TurnNumber;
            if (_mLandT1 < 0) _mLandT1 = LandCountOf(me);
            if (t >= 3 && _mLandT3 < 0) _mLandT3 = LandCountOf(me);
        }

        // 调试开关（TIDE_DEBUG_B4=1）：逐条打印被仪表看到的动作（校验卡 ID/回合/座次口径）
        private static readonly bool _debugB4 =
            Environment.GetEnvironmentVariable("TIDE_DEBUG_B4") == "1";

        /// <summary>2026-11-23 行为仪表：记录一次「模型侧已成功应用」的动作（Apply 返回 true 后调）。
        /// 只认卡 ID，不认费用档——避免被减费/同名歧义干扰（如 蓝·蛰伏 与 绿·牺牲 同为无域原子技能）。</summary>
        private void TrackModelBehavior(TideAction a)
        {
            var id = a.Card?.ID;
            if (string.IsNullOrEmpty(id)) return;
            if (_debugB4)
                Console.WriteLine($"[B4] turn={_core.TurnEngine.TurnNumber} type={a.Type} id={id} "
                    + $"name={(a.Card as IHasName)?.CardName} seat={SeatOf(_modelPlayer)}");
            // ⚠ 口径关键（2026-11-23 实测踩坑）：绿套地牌池是用卡做的，「阿格洛斯/归根 当土地打出」
            // 走的是 PlayLand（实测 turn=2 就把 14 费卡当地落场）。因此「召唤成功」必须要求
            // PlayCard/RespondPlay，否则会把地落当成召唤（agros_turn=2 那种假阳性）。
            bool asCard = a.Type == TideActionType.PlayCard || a.Type == TideActionType.RespondPlay;
            if (id == IdGreenSacrifice)
            {
                _mSac++;
                _mSacSeen = true;
            }
            else if (id == IdGreenRecall)
            {
                if (!asCard) return;           // 归根当地落不算「回收」
                _mRec++;
                if (_mSacSeen) _mChain = 1;    // 先牺牲 → 后归根 = 走通回收链
            }
            else if (id == IdGreenAgros)
            {
                if (!asCard) return;           // 阿格洛斯当地落不算「召唤」
                _mAgros++;
                int t = _core.TurnEngine.TurnNumber;
                if (_mAgrosTurnMin == 0 || t < _mAgrosTurnMin) _mAgrosTurnMin = t;
            }
            // ---- 2026-11-23 追加：绿成长 / 红法术线 ----
            else if (id == IdGreenGrow)
            {
                if (asCard) _mGrow++;          // 沃土「打出来」= 成长（地落不算）
            }
            else if (id == IdRedBlood || id == IdRedBomb)
            {
                if (!asCard) return;
                int t = _core.TurnEngine.TurnNumber;
                if (t != _mSpellTurn) { _mSpellTurn = t; _mBloodThisTurn = _mBombThisTurn = false; }
                if (id == IdRedBlood) { _mBlood++; _mBloodThisTurn = true; }
                else { _mBomb++; _mBombThisTurn = true; }
                if (_mBloodThisTurn && _mBombThisTurn) _mCombo = 1; // 同回合两法术 = 斩杀尝试
            }
        }
        // 当前停靠：泵 await 挂起的 TCS + 泵收集的候选表（Step 按行→候选映射完成 TCS 续跑，
        // 续跑内联在本线程——探针实证无 player loop 依赖，见 2026-11-22 tcs_continuation_probe）。
        private Cysharp.Threading.Tasks.UniTaskCompletionSource<ResponseOption> _parkTcs;
        private List<ResponseOption> _parkOptions;
        private bool _discardStop; // P3（2026-11-19 §4.1）：手牌上限弃牌停靠（legal=DiscardChoice 行）

        // vs 脚本对手位：_modelPlayer != null 时，非模型回合由 SimpleAI 整回合自动打
        // （策略每局按 Reset 注入重建——主题评估用 AutoMatch，缺省 General）
        private Player _modelPlayer;
        private BattleController _aiCtrl;
        private SimpleAI _simpleAI = new SimpleAI();

        // 全脚本局（opponent="script"，2026-11-22 归因取证）：双座各自 SimpleAI（AutoMatch(己方卡组)），
        // 座位↔实例绑定（_scriptAi1=P1 / _scriptAi2=P2）；仅 scriptVsScript 模式使用
        private SimpleAI _scriptAi1;
        private SimpleAI _scriptAi2;

        // 全脚本局回合上限：镜像 AiDuelRunner.DuelConfig.MaxTurns 默认 200——脚本局偶发不终结
        // （资源互耗）时的收口保险；到顶 = MaxTurns 无胜者（Python 记平局）
        private const int ScriptMaxTurns = 200;

        // 全脚本局单局墙钟上限（A1，2026-11-22 红绿归因计划）：回合粒度检查——单局即使每回合
        // 都在推进也不得超过此时长（绿镜像 ~0.25s/回合 × 200 回合 ≈ 50s，180s 只作病态局保险：
        // 回合越来越慢/单回合内卡长循环时强制收口）。到顶 = GameTimeout 无胜者（Python 记平局，
        // reasons 落 "GameTimeout"）；Python 侧 EnvTimeout（默认 240s）才是真挂死兜底（杀 Unity 重启）。
        // 已实证：smoke 4 局完局 + --game-timeout 2 强制触发 EnvTimeout 重启续跑链（2026-11-22 主仓）。
        private const float ScriptGameTimeoutSec = 180f;

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
            // 2026-11-23 战斗伤害计价（用户口径）：战斗伤害按 0.5 费/点计（法术/效果伤害仍 1.0）。
            // 势能里生命是 1.0/点一刀切，故本步末尾对「战斗伤害」做 −0.5 修正（见 Step）。
            EventManager.Instance.Subscribe<CombatDamageEvent>(OnCombatDamage);
        }

        /// <summary>2026-11-23：本步战斗伤害记账（区分打到我/打到对手，用于 0.5 计价修正）。</summary>
        private void OnCombatDamage(CombatDamageEvent e)
        {
            if (e == null || e.Damage <= 0) return;
            if (e.Defender is Player p && _modelPlayer != null && ReferenceEquals(p, _modelPlayer))
                _combatDmgSelfStep += e.Damage;
            else
                _combatDmgOppStep += e.Damage;
        }

        /// <summary>初始化一局（自对弈：双方座位都由模型驱动）。返回首个决策点观测。
        /// rngSeed（2026-11-22 评估协议 v2）：非 null 重播双随机流，缺省不重播。
        /// skillCardId1/2（2026-11-22 阶段 D2a）：显式英雄技能卡（主题卡组正牌技能——与
        /// TideSim/挑战/联机同源 DeckData.skillCardId）；null = AutoPickSkillCard 旧径。</summary>
        public TideStepResult Reset(List<CardData> deck1, List<CardData> deck2, int? rngSeed = null,
            string skillCardId1 = null, string skillCardId2 = null)
            => ResetCore(deck1, deck2, null, rngSeed: rngSeed,
                skillCardId1: skillCardId1, skillCardId2: skillCardId2);

        /// <summary>
        /// 初始化一局 vs 脚本对手：modelIsP1 指定模型座次，另一座位整回合由 SimpleAI 自动打
        /// （opponentStrategy 注入决策偏好——主题评估传 AiStrategy.AutoMatch(对手卡组)，缺省通用）。
        /// obs/合法动作/reward 恒为模型视角（Python 只见模型决策点）；
        /// 响应窗口（D2c 2026-11-22，取代旧「双侧自动让过」）：模型座候选>0 停靠问模型、
        /// 脚本座走策略响应（PickFor）。D2d（2026-11-22 对手回合响应停靠）：脚本回合异步链
        /// （TakeTurnAsync 不阻塞），链内响应窗对模型座同样停靠（AiAsyncResponder→TCS，
        /// Step 续跑）——ΔΦ 弧长与 D2c 前口径差异 = 双方回合内的连锁步；
        /// 非终局塑形的 ΔΦ 口径为「模型行动 + 对手整回合响应」的弧长，终局 ±1 仍按模型胜负。
        /// rngSeed（2026-11-22 评估协议 v2）：非 null → 经 InitGame 同种子重播双随机流
        /// （GameRng 效果随机 + ZoneContainer 洗牌）——同种子同卡组同座次 = 逐局可精确重播，
        /// 配对评估（两检查点打同一批对局）的前提；缺省 null 沿用旧行为不重播。
        /// skillCardId1/2（2026-11-22 阶段 D2a）：显式技能卡（正牌主题技能），null = AutoPick 旧径。
        /// </summary>
        public TideStepResult Reset(List<CardData> deck1, List<CardData> deck2, bool modelIsP1,
            AiStrategy opponentStrategy = null, int? rngSeed = null,
            string skillCardId1 = null, string skillCardId2 = null)
            => ResetCore(deck1, deck2, modelIsP1, opponentStrategy, rngSeed: rngSeed,
                skillCardId1: skillCardId1, skillCardId2: skillCardId2);

        /// <summary>
        /// 初始化一局全脚本对局（协议 opponent="script"，2026-11-22 「绿打红 0%」归因取证）：
        /// 双方整回合由各自 SimpleAI(AutoMatch(己方卡组)) 自动打到终局——reset 请求即跑完整局，
        /// 返回恒为终态（done=true；reward=0 占位，胜负由 Python 按 info.winner×info.modelSeat 判，
        /// 不发 step）。modelSideIsP1 只决定 info.modelSeat 回显（modelTheme 卡组侧座次——胜率统计
        /// 口径同 vs 模式，可与 0/96 模型矩阵同表对比）。
        /// 环境语义与 vs 模式逐项对齐（归因实验的立身之本）：卡组装载/InitGame/后手护甲/棋盘接线
        /// 同 ResetCore；手牌上限弃牌双座自动代选（无模型座停靠）。
        /// **阶段 D 修复（2026-11-22）后与 vs 模式的语义差异**：D2c 起 vs 模式脚本座同样经
        /// AiStrategyRegistry+AiResponder 走策略响应（同一装配，见 ResetCore vs 分支）——脚本局与
        /// vs 局的响应口径已收敛；剩余差异 = 脚本局无模型座（整局脚本驱动、弃牌自动代选），
        /// vs 局模型座停靠决策；技能卡两模式均可显式传正牌主题技能。
        /// 棋盘绑定（D1）两种模式已统一：_aiCtrl.BindBoard(_board)。
        /// rngSeed 同 vs 模式 Reset（2026-11-22 评估协议 v2）：非 null 重播双随机流，脚本局同样可钉。
        /// </summary>
        public TideStepResult ResetScriptGame(List<CardData> deck1, List<CardData> deck2, bool modelSideIsP1,
            int? rngSeed = null, string skillCardId1 = null, string skillCardId2 = null)
            => ResetCore(deck1, deck2, modelSideIsP1, null, scriptVsScript: true, rngSeed: rngSeed,
                skillCardId1: skillCardId1, skillCardId2: skillCardId2);

        private TideStepResult ResetCore(List<CardData> deck1, List<CardData> deck2, bool? modelIsP1,
            AiStrategy opponentStrategy = null, bool scriptVsScript = false, int? rngSeed = null,
            string skillCardId1 = null, string skillCardId2 = null)
        {
            // 变形目标形态解析器：组合根注入（镜像 AiBattleDriver / BattleController）
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;

            // D2d（2026-11-22）：上局残留的停靠链清引用——悬挂的 TCS 不完成（不在旧核心状态上
            // 续跑，链随本驱动引用断开交 GC）；泵在途标记防御性复位（internal set 同程序集可写，
            // 旧泵 finally 永不回=false 会令新局相位窗泵全部让路）。
            _parkTcs = null;
            _parkOptions = null;
            _scriptTurnChainActive = false;
            if (GameCore.Instance != null) GameCore.Instance.ResponsePumpInFlight = false;

            _core = GameCore.Instance;
            // RL 环境（2026-10-07 卡牌化口径）：双方 deck 挑一张合格法术充当技能——
            // **阶段 D2a（2026-11-22）**：显式 skillCardId 优先（主题卡组正牌技能，与
            // TideSim DeckSim/挑战/联机同源）；null 才 AutoPick（卡组内最低锚价法术——
            // 随机 30/掺卡等无构筑配置卡组的旧径）。此前 headless 恒 AutoPick，主题局技能
            // 与 TideSim 分叉（绿·牺牲→卡组内最便宜法术），是「脚本绿在 headless 被削」的
            // 首要嫌疑（红绿0胜归因计划 D2a/§3 阶段 D）。
            // rngSeed（2026-11-22 评估协议 v2）：透传 InitGame 重播双随机流（效果+洗牌），
            // null = 不重播（训练热路径旧行为）
            _core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1),
                rngSeed: rngSeed,
                skillCardId1: skillCardId1 ?? HeroSkillSystem.AutoPickSkillCard(deck1),
                skillCardId2: skillCardId2 ?? HeroSkillSystem.AutoPickSkillCard(deck2));
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
            {
                _simpleAI = new SimpleAI(opponentStrategy); // vs 模式：每局按注入策略重建（自对弈不用）
                // 阶段 D2c（2026-11-22 模型侧响应停靠）：vs 模式脚本座响应接线——镜像 AiDuelRunner/
                // D2b 脚本局：AiStrategyRegistry.Bind(脚本玩家) + ResponseWindowService.AiResponder=PickFor。
                // 两处消费：① 本驱动响应循环的脚本座（TryResolveResponseWindow → GameActions.
                // TryApplyAiResponse 单轮决策+应用）；② 脚本回合内引擎响应窗泵——脚本座策略响应，
                // 与 TideSim 同源。模型座不绑定——PickFor 查不到返回 null，回落 DefaultAiRespond
                // 守卫启发（模型的响应决策权不被同步口代行）。
                // 每局 Clear 重绑（Player 每局新建，不清会跨局累积泄漏）；Dispose 还原全局 responder。
                AiStrategyRegistry.Clear();
                AiStrategyRegistry.Bind(ReferenceEquals(_modelPlayer, _core.Player1) ? _core.Player2 : _core.Player1,
                    _simpleAI.Strategy); // SimpleAI 构造口单源（null 回落 General）
                ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;
                // D2d（2026-11-22 对手回合响应停靠）：注册引擎异步响应口——脚本回合（链在途且回合方
                // =脚本）的模型座停靠问模型（OnEngineAsyncResponse 内分流：其余语境返回完成态的
                // 同步决策 DecideAiResponse=未注册原路，零漂移）。未注册该口的模式（自对弈/脚本局）
                // 泵走旧同步链，逐位不变。
                ResponseWindowService.AiAsyncResponder = OnEngineAsyncResponse;
            }
            else
            {
                // 自对弈不注册（模型两座都走本驱动的响应循环）；残留回调清零防上局 vs 局串入
                ResponseWindowService.AiAsyncResponder = null;
            }

            // 棋盘占用层（派生，单向读核心）：为碾压关键词注入邻接解析 + 连接光环接线（核心不绑棋盘，宿主接线）
            _board?.Dispose();
            GameBoard.LinkAuraSystem.Detach(); // 上局接线归零（静态扩展点惯例）
            _board = new GameBoard.BoardState(_core, _core.Player1, _core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            // v20（2026-11-20 计划 §2.4 装配缺口修复）：MoveUnitRules 接线——与 AiDuelRunner/
            // BattleController 同口。此前训练环境读不到 MoveUnitRules.Current → 召唤聚集回落
            // first-free、P2 永久物落格退化几何全格不筛占用、移动行恒空——本 Attach 后
            // 召唤落位计划/永久物钉格/移动行在训练中真正生效。
            GameBoard.MoveUnitRules.Attach(_board);
            CombatSystem.AdjacentResolver = _board.FlankNeighbors; // 碾压=左右同排生物（2026-10-04 语义修订）
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole; // 碾压攻角色=角色格左右邻格（2026-10-16 改版）
            BranchEngines.UnityAdjacentCountOf = _board.AdjacentOccupantCount; // 团结=六邻单位数（2026-10-20 效果序位换轴）
            GameBoard.LinkAuraSystem.Attach(_board); // 连接光环（三轨制）：箭头指向格占据者享受 linkAuras
            // 阶段 D1（2026-11-22 红绿0胜归因计划）：脚本侧 BattleController 绑定同一棋盘——
            // 镜像 AiDuelRunner.AttachBoard 的 ctrl?.BindBoard。此前 Board=null，SimpleAI 的
            // TryPinPlacement 直接跳过（出永久物不钉格，策略落格/未来聚集布局失效）。
            // vs 模式与脚本局统一绑定：模型自身落格走动作空间+MoveUnitRules，不经此口，零影响。
            _aiCtrl?.BindBoard(_board);

            _gameOver = false;
            _winner = null;
            _reason = null;
            _decider = null;
            _responseStop = false;
            _discardStop = false;
            // 2026-11-23 行为仪表：每局清零（B4「牺牲+回收」/ 14 费大哥落地计数）
            _mSac = _mRec = _mAgros = _mAgrosTurnMin = _mChain = 0;
            _mSacSeen = false;
            // 2026-11-23 追加：红/绿招牌操作（成长 / 法术斩杀线）每局清零
            _mGrow = _mBlood = _mBomb = _mCombo = 0;
            _mLandT1 = _mLandT3 = -1;
            _mSpellTurn = -1;
            _mBloodThisTurn = _mBombThisTurn = false;
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

            if (scriptVsScript)
            {
                // 全脚本局（2026-11-22 归因取证）：双方 SimpleAI(AutoMatch(己方卡组))；无模型座停靠
                // ——手牌上限弃牌双座自动代选（否则 EnforceHandLimitAsync 挂起死等永不来的 step）；
                // _modelPlayer 保留（info.modelSeat 回显 modelTheme 侧座次），但决策全部由脚本接管。
                // 阶段 D2b（2026-11-22）：镜像 AiDuelRunner 的响应接线——AiStrategyRegistry.Bind +
                // ResponseWindowService.AiResponder=PickFor。此前响应窗 AiResponder=null → 引擎回落
                // 内置守卫启发（DefaultAiRespond），脚本侧**没有策略响应**（RespondPlay/归根回收等
                // 反制类决策全缺）——TideSim 基线是有策略响应的口径，这是脚本局语义分叉的主嫌之一。
                // D2c（2026-11-22）起 vs 模式在 ResetCore vs 分支同样装配（模型座停靠+脚本座 PickFor），
                // 本 finally 清零后由 vs 局 reset 重新装配——两种模式同一响应口径。
                var s1 = AiStrategy.AutoMatch(deck1);
                var s2 = AiStrategy.AutoMatch(deck2);
                _scriptAi1 = new SimpleAI(s1);
                _scriptAi2 = new SimpleAI(s2);
                AiStrategyRegistry.Bind(_core.Player1, s1);
                AiStrategyRegistry.Bind(_core.Player2, s2);
                ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;
                HandLimitDiscardGate.Bind(p => false);
                try
                {
                    RunScriptGameToEnd();
                }
                finally
                {
                    // 镜像 AiDuelRunner.Run 的 finally：注册表/响应器归零，防串入后续局
                    //（vs 局 reset 自带重装配，见上方 vs 分支；D2d 异步口一并清）
                    AiStrategyRegistry.Clear();
                    ResponseWindowService.AiResponder = null;
                    ResponseWindowService.AiAsyncResponder = null;
                }
                return BuildResult(0f);
            }

            AdvanceToDecision();
            return BuildResult(0f);
        }

        /// <summary>执行一步：应用动作下标 → 结算/响应窗口 → 推进 → 算奖励 → 返回下一决策点。</summary>
        public TideStepResult Step(int actionIndex)
        {
            if (_gameOver) return BuildResult(0f); // 已终局：无动作可执行

            var me = _decider ?? _core.TurnEngine.TurnPlayer; // 本步决策座次（Main=回合玩家 / 响应=优先权方）
            // 2026-11-23 成长指标：本步动手前的 T1/T3 地数快照（红/绿招牌操作分析用）
            SnapshotGrowth(me);
            // 2026-11-23 战斗伤害计价：每步清零（本步内的 CombatDamageEvent 由 OnCombatDamage 记账）
            _combatDmgSelfStep = _combatDmgOppStep = 0;
            // 2026-11-23 用户定案（塑形口径 v2）：势能改为 **含手牌**——与定价体系对齐
            //（一张卡 ≈ 2 费；从卡组抽 2 费、从墓地回收 2 费，详见 FieldValueReward 权重表）。
            // 旧 2026-09-22「剔除手牌」是为抑制"摸牌白拿奖励"，但那条已由 λ 0.05→0.005 与
            // 「横置=产出元素」新口径取代；若再出现拖局刷塑形，回滚点=此处改回 false。
            // λ=0 基线跳过计算（非终局奖励恒 0，势能白算）
            float before = _shapingLambda > 0f
                ? FieldValueReward.ComputePotential(_core, me, includeHand: true)
                : 0f;

            // 越界下标：保守兜底——不推进、奖励 0，重观测同状态。但必须计入停滞与回合动作数：
            // 冻结态下模型全掩码均匀采样，每步仅 1/512 概率落在唯一兜底行上，其余全走本分支——
            // 不计数则 StallDraw 永远到不了阈值（64×512≈3.3 万步空转），2026-09-30 复发实证。
            if (_legal == null || actionIndex < 0 || actionIndex >= _legal.Actions.Count)
            {
                // D2d：停靠步越界也必须放行泵（否则链悬挂死锁后续对局）——按让过完成 TCS
                if (_parkTcs != null)
                {
                    var stale = _parkTcs;
                    _parkTcs = null;
                    _parkOptions = null;
                    stale.TrySetResult(null); // 内联续跑（可能再次停靠——BuildResult 渲染新停靠）
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

            if (_parkTcs != null)
            {
                // D2d（2026-11-22 对手回合响应停靠）续跑：本步=脚本回合响应窗的模型选择。
                // 所选行按 ParkedOptionOfRow 映射回泵收集的 ResponseOption（RespondPlay 行的
                // Targets/DynamicCounts 写回候选——泵 PlayCardInResponse 按声明期预选目标消费）；
                // PassPriority 兜底行=让过（null）。TrySetResult 后泵在本线程内联应用并推进：
                // 可能再次停靠（AdvanceToDecision 见链在途直接返回，BuildResult 渲染新停靠）或
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
                // 2026-11-23 行为仪表（B4）：只在 Apply 成功后计数 → 数的是「真的落地」。
                TrackModelBehavior(a);
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
                float after = FieldValueReward.ComputePotential(_core, me, includeHand: true); // v2 同口径
                float delta = after - before;
                // 2026-11-23 战斗伤害 0.5 费/点（用户口径）：Φ 把生命按 1.0 计，战斗伤害要按 0.5 →
                // 打对手 +1.0×dmg 多算了，故 −0.5×dmg；自己挨打 −1.0 要收窄成 −0.5，故 +0.5×dmg。
                // 法术/效果伤害（DamageEvent 非战斗）不在本修正内，保持 1.0。
                delta += 0.5f * (_combatDmgSelfStep - _combatDmgOppStep);
                reward = _shapingLambda * delta;
            }

            return BuildResult(reward);
        }

        /// <summary>释放：反订阅终局、撤销棋盘接线（静态扩展点归零）。</summary>
        public void Dispose()
        {
            HandLimitDiscardGate.Reset(); // P3 §4.1：解绑+清挂起
            HandLimitDiscardGate.Bind(null);
            // D2c/D2d（2026-11-22）：vs 模式响应接线还原（镜像 AiDuelRunner.Run finally）——
            // 注册表/AiResponder/AiAsyncResponder 是全局静态，不还原会串入同进程后续局
            AiStrategyRegistry.Clear();
            ResponseWindowService.AiResponder = null;
            ResponseWindowService.AiAsyncResponder = null;
            EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            EventManager.Instance.Unsubscribe<CombatDamageEvent>(OnCombatDamage); // 2026-11-23 战斗伤害记账
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
            BranchEngines.UnityAdjacentCountOf = null; // 团结邻接注入同步归零
            GameBoard.LinkAuraSystem.Detach(); // 连接光环接线同步归零
            GameBoard.MoveUnitRules.Detach(); // v20：与 Attach 成对清理（静态不清理会指向过期占用层）
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
        /// vs SimpleAI 模式：非模型回合不逐相位推进，整回合交 SimpleAI 异步链（D2d：TakeTurnAsync
        /// 不阻塞，链内响应窗经 AiAsyncResponder 停靠模型座，见 RunSimpleAiTurnAsync/OnEngineAsyncResponse）；
        /// 模型回合内的响应窗口按 D2c 口径处理（模型座停靠/脚本座策略响应，见 TryResolveResponseWindow）。
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

                // vs SimpleAI：对手回合整段自动打（D2d 异步链——TakeTurnAsync 自带回合准备与 EndTurn，
                // 不折返）。链停靠模型座（Step 续跑后本步内再停靠）→ 停靠即决策点，直接返回；
                // 整回合无停靠 = 链同步跑完（相位已在链内折返）→ 循环重查。
                if (_modelPlayer != null && turnPlayer != _modelPlayer)
                {
                    if (_scriptTurnChainActive) return; // D2d：链已停靠——不得再起第二条链
                    RunSimpleAiTurnAsync();
                    if (_parkTcs != null) return;       // D2d：脚本回合响应窗停靠模型座
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
                //   已跑通路径——BuildResult/EnumerateResponses/Apply 全按 _decider 驱动，g[49]=1）；
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
                    else if (GameActions.TryApplyAiResponse(_core, holder))
                    {
                        continue; // 脚本座策略响应发动成功 → 优先权已翻转，继续轮询
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
        /// SimpleAI 整回合驱动（D2d 2026-11-22 异步化，取代旧同步 RunSimpleAiTurn）：
        /// TakeTurnAsync 起链**不阻塞**调用线程——链内响应窗（出牌/攻宣/自愿桶）经引擎泵问
        /// AiAsyncResponder：模型座停靠 TCS（本驱动 OnEngineAsyncResponse），脚本座同步策略响应。
        /// 无候选整回合 = 全链同步跑完（PacingWait=null 契约，时序/线程与旧同步口一致）；
        /// 有停靠 = 链挂起、控制返回调用方（AdvanceToDecision 见 _parkTcs 即返），模型 Step 的
        /// TrySetResult 后续跑**内联在本线程**（2026-11-22 探针实证，无 player loop 依赖）。
        /// 链内完成 End→Standby 折返（原同步口的补泵时点）；异常按 SimpleAIError 收口
        /// （无胜者 → Python 记平局，不炸训练服务）。
        /// </summary>
        private void RunSimpleAiTurnAsync()
        {
            _scriptTurnChainActive = true;
            RunWrapAsync().Forget();
            async Cysharp.Threading.Tasks.UniTask RunWrapAsync()
            {
                try
                {
                    await _simpleAI.TakeTurnAsync(_aiCtrl);
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
                finally
                {
                    _scriptTurnChainActive = false;
                }
            }
        }

        /// <summary>
        /// D2d 引擎异步响应口（注册于 ResetCore vs 分支）：**脚本回合**（链在途且回合方=脚本——
        /// 排除链尾折返后模型回合的相位窗泵，防双主停靠冲突）的模型座 → 停靠（_parkTcs 挂起泵，
        /// 停靠观测/续跑复用响应停靠契约：_decider=模型、_responseStop=true、g[49]=1）；
        /// 其余语境（脚本座 / 模型自己回合的相位窗泵 / 自对弈残留）→ 返回**完成态**的同步决策
        /// DecideAiResponse（=未注册时的原路，零漂移）。
        /// </summary>
        private Cysharp.Threading.Tasks.UniTask<ResponseOption> OnEngineAsyncResponse(
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

        /// <summary>
        /// 全脚本局主循环（2026-11-22 归因取证，opponent="script"）：镜像 AiDuelRunner.Run 的
        /// 回合推进（回合玩家 SimpleAI 整回合 TakeTurn → End→Standby 补泵），环境细节对齐
        /// 本驱动的 vs 模式（棋盘接线 D1 已统一；响应接线 D2b/D2c 已统一——窗口由 TakeTurn
        /// 内部的响应窗泵就近收口，见下方残留排干注释）。
        /// 异常按 SimpleAIError 收口（无胜者=平局，不炸训练服务）；回合上限到顶未终结 =
        /// MaxTurns 收口（无胜者=平局）；单局墙钟超 ScriptGameTimeoutSec = GameTimeout 收口
        /// （无胜者=平局，A1 2026-11-22——绿镜像慢局/病态局不再无限占时）。
        /// </summary>
        private void RunScriptGameToEnd()
        {
            var wall = System.Diagnostics.Stopwatch.StartNew();
            for (int t = 0; t < ScriptMaxTurns && !_gameOver; t++)
            {
                if (wall.Elapsed.TotalSeconds > ScriptGameTimeoutSec)
                {
                    _gameOver = true;
                    _winner = null;
                    _reason = "GameTimeout";
                    return;
                }
                var p = _core.TurnEngine.TurnPlayer;
                var ai = ReferenceEquals(p, _core.Player1) ? _scriptAi1 : _scriptAi2;
                try
                {
                    ai.TakeTurn(_aiCtrl);
                    if (!_gameOver)
                        _core.TurnEngine.CheckPhaseTransition();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[TideHeadless] 全脚本局 SimpleAI 回合异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                    _gameOver = true;
                    _winner = null;
                    _reason = "SimpleAIError";
                    return;
                }
                // 回合残留栈/待发效果：排干（**D2b 后仅防御性**——TakeTurn 内部的响应窗泵已由
                // AiResponder=PickFor 走策略决策，正常回合此循环零命中；仅当栈异常外溢时防卡死）
                for (int i = 0; i < ResponseWindowGuard && !_gameOver; i++)
                {
                    var engine = _core.StackEngine;
                    if (engine.IsEmpty && !engine.HasPendingEffects) break;
                    if (engine.IsEmpty)
                    {
                        engine.ProcessPendingEffects();
                        continue;
                    }
                    var holder = engine.CurrentPriorityHolder;
                    if (holder == null || !GameActions.PassPriority(_core, holder)) break;
                }
            }
            if (!_gameOver)
            {
                _gameOver = true;
                _winner = null;
                _reason = "MaxTurns";
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
            else if (_responseStop)
            {
                // D2d：脚本回合停靠步——行集来自泵给定候选（1:1 映射，Step 据此完成 TCS）；
                // 自对弈/D2c 模型回合停靠照旧重收集
                if (_parkTcs != null) _legal.EnumerateParkedResponses(_core, _decider, _parkOptions);
                else _legal.EnumerateResponses(_core, _decider);
            }
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
                // 2026-11-23 行为仪表（B4 用户口径）：模型侧「牺牲 + 回收」与 14 费大哥落地计数，
                // 每局累计、ResetCore 清零；协议 info.mSac/mRec/mChain/mAgros/mAgrosTurn。
                ModelSacrifices = _mSac,
                ModelRecalls = _mRec,
                ModelChain = _mChain,
                ModelAgros = _mAgros,
                ModelAgrosTurnMin = _mAgrosTurnMin,
                // 2026-11-23 追加：绿成长（沃土打出 / T1·T3 地数）+ 红法术线（血偿/炸弹/同回合斩杀尝试）
                ModelGrow = _mGrow,
                ModelLandT1 = _mLandT1,
                ModelLandT3 = _mLandT3,
                ModelBlood = _mBlood,
                ModelBomb = _mBomb,
                ModelCombo = _mCombo,
            };
        }

    }
}
