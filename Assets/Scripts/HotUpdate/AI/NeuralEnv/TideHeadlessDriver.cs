using System;
using System.Collections.Generic;
using System.Linq;
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
        // ---- 2026-11-23 考察项：攻击目标是否理解「打已横置单位不吃反击」----
        /// <summary>模型攻击「已横置且无警戒」单位次数（不吃反击 = 安全伤害）。</summary>
        public int ModelAtkSafe;
        /// <summary>模型攻击「未横置 / 有警戒」单位次数（会吃反击伤害）。</summary>
        public int ModelAtkRisky;
        /// <summary>模型攻击角色（打脸）次数。</summary>
        public int ModelAtkFace;
        /// <summary>攻击宣言事件全量（排查用）</summary>
        public int ModelAtkAny;
        // ---- S3-1（2026-11-26 二期 §5）：开手交棒（协议 info.handoff*；未命中进 stats.jsonl）----
        /// <summary>交棒档位（none/t3_lands/first_sac_done/genel_ready；none=关闭）。</summary>
        public string HandoffState;
        /// <summary>本局是否已交棒（true=模型接管；false=终局仍未触发）。</summary>
        public bool HandoffDone;
        /// <summary>交棒时全局回合号（0=未交棒）。</summary>
        public int HandoffTurn;
        /// <summary>交棒时模型座池地张数（0=未交棒）。</summary>
        public int HandoffLands;
        /// <summary>交棒时归根动态费（×0.1 粒度，6.0/8.0 并存口径；-1=未交棒或不适用）。</summary>
        public float HandoffGenCost;
        /// <summary>交棒时手牌是否含归根（genel_ready 档恒 true；其它档如实）。</summary>
        public bool HandoffGenInHand;
        /// <summary>未交棒原因（""=已交棒；"ended"=终局仍未触发）。</summary>
        public string HandoffMiss;
        /// <summary>交棒后第几步首次施放归根（1 起；-1=未交棒或交棒后整局未施放）——
        /// 「交棒后 3 步内施放率」的原始读数（S3-1 验收）。</summary>
        public int HandoffStepsToGenel;
        /// <summary>本次交棒是否为回合兜底强制（true=兜底/maxOpeningTurns，false=genel_ready 真实触发）
        /// ——S3-1 验收按本位拆分读，防兜底污染命中率与 gen_in_hand 统计。</summary>
        public bool HandoffForced;
        // ---- S3-3（2026-11-26 二期 §5）：链进度（协议 info.chainBits/chainDone；decider 座位）----
        /// <summary>decider 座位的链里程碑闩位（bit0..3；本步结算后的值——终局步=全量）。</summary>
        public int ChainBits;
        /// <summary>decider 座位是否到达链终点里程碑（bit3：绿=大哥落地 / 红=combo / 蓝=潮痕碑召唤）——
        /// aux 第三标签「本局是否到达目标态」的数据源。</summary>
        public bool ChainDone;
    }

    /// <summary>
    /// 无头对局驱动器：把「模型在环」的决策循环收敛成 reset/step 两步接口。
    /// 复用 AiBattleDriver.RunFullGame 的无头初始化骨架（InitGame + 双方 IsAI + 棋盘接线），
    /// 但把决策点替换为「读动作 → GameActions 调用 → 快照 Φ 做差算塑形奖励」。
    ///
    /// **2026-11-27 S1（逐步驱动共享重构）**：决策点循环本体（动作应用分支 / AdvanceToDecision /
    /// 响应窗停靠 TryResolveResponseWindow / D2d 停靠泵 / L1-L3 循环收口 / 停靠四路枚举）已逐行
    /// 迁入 <see cref="BattleDecisionLoop"/>（只搬不改——同种子对拍逐位一致，见
    /// docs/逐步驱动共享_重构计划.md）；本驱动保留训练侧职责：游戏装配（ResetCore）、
    /// 奖励（λ·ΔΦ 塑形 / S3-3 链势能 / 终局 ±1）、B4 行为仪表、S3-1 开手交棒、S3-3 链闩位、
    /// 全脚本局（RunScriptGameToEnd）、协议 info 组装。对外 reset/step 协议（请求字段与 info
    /// 键集合）逐字不变。
    ///
    /// 时序（每回合，决策循环口径见 BattleDecisionLoop 类注）：
    ///   回合开始（StartNewTurn 自动过 Standby → Main）
    ///   → 模型选动作 → 应用 → 响应窗口（v2）→ 阶段推进折返 → 下一决策点。
    ///
    /// 奖励（actor-centric 自对弈，reward 恒归属「刚行动的那一方」= 本步决策座次）：
    ///   终局 ±1 主导；非终局每步 λ·(Φ_after − Φ_before)，Φ = 己方战场价值 − 对方战场价值
    ///   （λ 默认 0.005；Φ 2026-11-23 起含手牌〔A4 2026-11-24 单价：手牌 3/张固定、地 2/块、
    ///   横置净 +1〕，囤牌收益由手牌 7 上限封顶——塑形累计须 ≪ 终局 ±1，否则学拖局；
    ///   旧 2026-09-22「剔除手牌」口径已废弃，回滚点=Step 两处 includeHand）。
    ///   λ=0 显式下发 = 关闭塑形（非终局恒 0 纯终局 ±1 基线，2026-11-02 起可关）。
    ///   响应步计入 λ·ΔΦ 塑形（弧长变长——λ 维持观察，必要时再调，契约风险节）。
    ///
    /// 文档化简化（v1 沿袭）：地牌横置由驱动回合初自动完成（LandTapPolicy 混付感知），不建模。
    /// </summary>
    public sealed class TideHeadlessDriver
    {
        // 塑形系数（2026-09-22 定案 0.05→0.005：λ=0.05 时双方各自整局塑形累计 ≈ +3.1，
        // 是终局 ±1 的 3 倍，自对弈收敛到拖局刷塑形；reset 协议可按局覆盖——
        // TideHeadlessServer.HandleReset 的 shapingSet=1 显式下发，含 0=关闭塑形）
        private float _shapingLambda = 0.005f;

        // ==== 决策点循环（2026-11-27 S1 抽取到 BattleDecisionLoop；原 L1/L2/L3 收口常量与
        // 停靠/摘除/指纹状态全部随迁，数值不动——观测/动作布局（v22）与超参零改动）====
        private readonly BattleDecisionLoop _loop = new BattleDecisionLoop();

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
        // 2026-11-23 考察项：模型侧攻击目标分类（safe=打已横置且无警戒的单位→不吃反击；
        // risky=未横置或有警戒→吃反击；face=打角色）
        private int _mAtkSafe, _mAtkRisky, _mAtkFace;
        /// <summary>攻击宣言事件全量计数（排查用：0=事件没发/没订阅上；&gt;0 但另三项=0 → 归属判定问题）。</summary>
        private int _mAtkAny;

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

        // ---- S3-1（2026-11-26 二期 §5）：开手交棒 —— 模型座先用脚本策略打到触发态，再把
        // 驱动权交回模型（单向，永不回脚本）。触发评估点=模型座回合开始（loop.AdvanceToDecision
        // 的 InterceptModelTurn 钩子位）；开手回合整段脚本驱动（含响应窗=开手策略 PickFor），
        // 中途不折返——genel_ready 若在开手回合内才达成，脚本会把本回合（含归根施放）打完，
        // 下回合开始才交棒。none=关闭（缺省，零行为变化）；三档按计划 §5。
        private string _handoffState = "none";
        private int _maxOpeningTurns;      // 兜底：全局回合 ≥ 此值强制交棒（0=不兜底；协议下发）
        private bool _handoffDone;         // 已交棒（handoffState=none 时恒 true=直通模型）
        private SimpleAI _openingAi;       // 模型座开手脚本（AutoMatch(模型卡组)——每局重建）
        private int _handoffTurn, _handoffLands, _handoffGenCostX10 = -1; // 交棒打点（费×10 存整数）
        private bool _handoffGenInHand;
        private string _handoffMiss = "";  // 终局未触发原因（"ended"=未交棒即终局；""=已交棒）
        // S3-1 验收读数（2026-11-26 补）：交棒后模型座决策步计数与首次归根施放步距——
        // 「交棒后 3 步内施放率」的数据源（开手回合动作不走 Step，天然只计交棒后）。
        private int _stepsAfterHandoff;       // 交棒后的模型座决策步（1 起，0=未交棒）
        private int _handoffStepsToGenel = -1; // 交棒后第几步首次施放归根（-1=整局未施放）
        private bool _handoffForced;           // 本次交棒=回合兜底强制（true）/ genel_ready 真实触发（false）

        // ---- S3-3（2026-11-26 二期 §5）：链进度势能 Φ_chain —— 三预组各自的 4 段里程碑闩位
        //（按座位独立计数；事件层更新，含脚本开手/对手回合）。闩位只进不出（单调——势能
        // 塑形 γ=1 telescoping 前提；G5 漏斗的「窗口内定序」口径不在此复刻）。
        // 绿 = 0:池地≥3  1:施放归根  2:重铺(归根施放后再放地)  3:阿格洛斯落地
        // 红 = 0:池地≥3  1:血偿打出   2:活体炸弹打出        3:同回合双法术(combo)
        // 蓝 = 0:池地≥2  1:深潭碑打出  2:潮汐施放           3:潮痕碑衍生物召唤
        // 卡组不命中三预组（随机 30/掺卡等）→ 主题空串，闩位恒 0（Φ=0，无链奖励）——
        // 契约例外（§2 非目标唯一豁免）只覆盖三预组目标态，随机卡组不写专用奖励。用户 2026-11-26
        // 「s3全面执行」指令=对 §5 S3-3 契约变更的签字。
        private float _chainLambda = 0f;           // 奖励系数（协议 chainLambda 显式下发；0=关奖励，
                                                   // 闩位计数照常——info.chainBits 是免费过程仪表）
        private readonly int[] _chainBits = new int[2];       // 每座位里程碑闩位 bit0..3
        private readonly string[] _seatTheme = { "", "" };    // 每座位卡组主题（green/red/blue/""）
        private readonly int[] _redSpellTurn = { -1, -1 };    // 红链：本回合已打出的法术记账
        private readonly bool[] _redBlood = new bool[2];
        private readonly bool[] _redBomb = new bool[2];

        // 核心引用（与 loop 共享同一实例：loop 持决策循环态，本侧持仪表/交棒/链记账所需的镜像）
        private GameCore _core;
        private GameBoard.BoardState _board;

        // vs 脚本对手位：_modelPlayer != null 时，非模型回合由 loop 内对手 SimpleAI 整回合自动打
        // （策略每局按 Reset 注入重建——主题评估用 AutoMatch，缺省 General；2026-11-27 S1 起
        // 对手实例随决策循环下放 BattleDecisionLoop，经 BeginGame 注入）
        private Player _modelPlayer;
        private BattleController _aiCtrl;

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

        public bool IsGameOver => _loop.IsGameOver;
        public Player Winner => _loop.Winner;

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
        public int DecisionSeat => SeatOf(_loop.Decider);

        public TideHeadlessDriver()
        {
            // 决策循环钩子（2026-11-27 S1 抽取后 driver 侧外挂，时位与原内联调用逐位一致）：
            // ① B4 行为仪表——动作引擎应用成功后、推进前即时计（原 Step 内联 TrackModelBehavior）；
            // ② S3-1 开手交棒——模型座回合开始停靠口的三态判定（原 AdvanceToDecision 内联块）。
            _loop.AfterActionApplied = TrackModelBehavior;
            _loop.InterceptModelTurn = DriverInterceptModelTurn;
            // 订阅终局（全局事件总线；Dispose 反订阅，避免跨局泄漏）
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
            // 2026-11-23 战斗伤害计价（用户口径）：战斗伤害按 0.5 费/点计（法术/效果伤害仍 1.0）。
            // 势能里生命是 1.0/点一刀切，故本步末尾对「战斗伤害」做 −0.5 修正（见 Step）。
            EventManager.Instance.Subscribe<CombatDamageEvent>(OnCombatDamage);
            // 2026-11-23 考察项（用户口径）：与**已横置**单位战斗时不吃反击伤害
            //（规则出处 `CardPipelineVerifier.Combat.cs` 8c：「普通横置不反击」；
            //  警戒 Vigilance 例外——已横置仍反击，故仍算 risky）。
            // 这一项反映模型是否理解「先横置、再打」——安全伤害 vs 挨反伤。
            EventManager.Instance.Subscribe<AttackDeclarationEvent>(OnAttackDeclared);
            // S3-3（2026-11-26 二期 §5）：链进度里程碑闩位——事件层按座位记账（含脚本回合；
            // λ=0 时闩位照常，info.chainBits 免费仪表）。
            EventManager.Instance.Subscribe<CardPlayEvent>(OnChainCardPlay);
            EventManager.Instance.Subscribe<ElementPoolAddEvent>(OnChainPoolAdd);
            EventManager.Instance.Subscribe<CardZoneChangeEvent>(OnChainZoneChange);
        }

        /// <summary>2026-11-23：攻击宣言记账（**先记全量**再分流，便于排查"事件到底有没有发"）。
        /// safe = 目标已横置且无警戒（不吃反击）；risky = 未横置/有警戒（吃反击）；face = 打角色。
        /// 归属判定用 `Attacker.GetController()`（比 AttackingPlayer 引用更稳，2026-11-23 实测后者恒 0）。</summary>
        private void OnAttackDeclared(AttackDeclarationEvent e)
        {
            if (e == null) return;
            _mAtkAny++;                       // 全量计数：0 = 事件没发/没订阅上
            if (_debugB4)
                Console.WriteLine($"[B4-atk] target={(e.Target is Card c0 ? c0.ID : (e.Target is Player pl ? "P:" + pl.Name : "?"))} "
                    + $"atk={(e.Attacker as Card)?.ID ?? "?"} ctrl={(e.AttackingPlayer != null ? e.AttackingPlayer.Name : "null")} "
                    + $"mine={(e.Attacker is Card ac && _modelPlayer != null && ReferenceEquals(ac.GetController(), _modelPlayer))}");
            bool mine = e.Attacker is Card ac2
                        && _modelPlayer != null && ReferenceEquals(ac2.GetController(), _modelPlayer);
            if (!mine)
            {
                // 兼容：Attacker 非卡（角色直攻）时退回 AttackingPlayer 判定
                if (!(e.AttackingPlayer != null && ReferenceEquals(e.AttackingPlayer, _modelPlayer))) return;
            }
            if (e.Target is Card tc)
            {
                bool tapped = tc.IsTapped();
                bool vigilant = tc.HasKeyword(CardCore.Attribute.KeywordRules.Vigilance);
                if (tapped && !vigilant) _mAtkSafe++;
                else _mAtkRisky++;
            }
            else if (e.Target is Player) _mAtkFace++;
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
            string skillCardId1 = null, string skillCardId2 = null,
            string handoffState = "none", int maxOpeningTurns = 0)
            => ResetCore(deck1, deck2, modelIsP1, opponentStrategy, rngSeed: rngSeed,
                skillCardId1: skillCardId1, skillCardId2: skillCardId2,
                handoffState: handoffState, maxOpeningTurns: maxOpeningTurns);

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
            string skillCardId1 = null, string skillCardId2 = null,
            string handoffState = "none", int maxOpeningTurns = 0)
        {
            // 变形目标形态解析器：组合根注入（镜像 AiBattleDriver / BattleController）
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;

            // D2d（2026-11-22）：上局残留的停靠链清引用——悬挂的 TCS 不完成（不在旧核心状态上
            // 续跑，链随本驱动引用断开交 GC）；泵在途标记防御性复位（internal set 同程序集可写，
            // 旧泵 finally 永不回=false 会令新局相位窗泵全部让路）。
            // 2026-11-27 S1：随决策循环迁 BattleDecisionLoop.DetachParkedChains（位置不变——InitGame 前防御位）。
            _loop.DetachParkedChains();

            _core = GameCore.Instance;
            // RL 环境（2026-10-07 卡牌化口径）：双方 deck 挑一张合格法术充当技能——
            // **阶段 D2a（2026-11-22）**：显式 skillCardId 优先（主题卡组正牌技能，与
            // TideSim DeckSim/挑战/联机同源 DeckData.skillCardId）；null 才 AutoPick（卡组内最低锚价法术——
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
            SimpleAI simpleAI = null; // vs 模式脚本对手（每局按注入策略重建；2026-11-27 S1 随决策循环经 BeginGame 注入 loop）
            if (modelIsP1.HasValue)
            {
                simpleAI = new SimpleAI(opponentStrategy); // vs 模式：每局按注入策略重建（自对弈不用）
                // S3-1（2026-11-26 二期）：开手交棒开启时，模型座也绑开手策略——开手回合的响应窗
                //（AiStrategyRegistry.PickFor）走脚本决策；交棒后停靠口（TryResolveResponseWindow
                // 的模型座分支 / D2d 泵停靠）先于脚本口命中，绑定惰性无害。
                if (!_handoffDone && _openingAi != null)
                    AiStrategyRegistry.Bind(_modelPlayer, _openingAi.Strategy);
                // 阶段 D2c（2026-11-22 模型侧响应停靠）：vs 模式脚本座响应接线——镜像 AiDuelRunner/
                // D2b 脚本局：AiStrategyRegistry.Bind(脚本玩家) + ResponseWindowService.AiResponder=PickFor。
                // 两处消费：① 决策循环响应窗口的脚本座（GameActions.TryApplyAiResponse 单轮决策+应用）；
                // ② 脚本回合内引擎响应窗泵——脚本座策略响应，与 TideSim 同源。模型座不绑定——
                // PickFor 查不到返回 null，回落 DefaultAiRespond 守卫启发（模型的响应决策权不被同步口代行）。
                // 每局 Clear 重绑（Player 每局新建，不清会跨局累积泄漏）；Dispose 还原全局 responder。
                AiStrategyRegistry.Clear();
                AiStrategyRegistry.Bind(ReferenceEquals(_modelPlayer, _core.Player1) ? _core.Player2 : _core.Player1,
                    simpleAI.Strategy); // SimpleAI 构造口单源（null 回落 General）
                ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;
                // D2d（2026-11-22 对手回合响应停靠）：注册引擎异步响应口——脚本回合（链在途且回合方
                // =脚本）的模型座停靠问模型（loop.OnEngineAsyncResponse 内分流：其余语境返回完成态的
                // 同步决策 DecideAiResponse=未注册原路，零漂移）。未注册该口的模式（自对弈/脚本局）
                // 泵走旧同步链，逐位不变。
                ResponseWindowService.AiAsyncResponder = _loop.OnEngineAsyncResponse;
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

            // 2026-11-23 行为仪表：每局清零（B4「牺牲+回收」/ 14 费大哥落地计数）
            _mSac = _mRec = _mAgros = _mAgrosTurnMin = _mChain = 0;
            _mSacSeen = false;
            // 2026-11-23 追加：红/绿招牌操作（成长 / 法术斩杀线）每局清零
            _mGrow = _mBlood = _mBomb = _mCombo = 0;
            _mLandT1 = _mLandT3 = -1;
            _mSpellTurn = -1;
            _mBloodThisTurn = _mBombThisTurn = false;
            // 2026-11-23 考察项：攻击目标分类（safe/risky/face）每局清零
            _mAtkSafe = _mAtkRisky = _mAtkFace = _mAtkAny = 0;
            // S3-3（2026-11-26 二期）：链进度闩位/主题每局清零重识别（deck1=P1 / deck2=P2）
            _chainBits[0] = _chainBits[1] = 0;
            _seatTheme[0] = DetectTheme(deck1);
            _seatTheme[1] = DetectTheme(deck2);
            _redSpellTurn[0] = _redSpellTurn[1] = -1;
            _redBlood[0] = _redBlood[1] = _redBomb[0] = _redBomb[1] = false;
            // S3-1（2026-11-26 二期）：开手交棒配置落位（none/非法=关，零行为变化）；
            // 开启时建模型座开手脚本（AutoMatch(模型卡组)）并给模型座绑响应策略——开手回合
            // 响应窗经 PickFor 同步决策（模型座停靠口对「回合方=模型」的语境不接管，见其守卫），
            // 交棒后绑定惰性无害（停靠口先于脚本口命中）。
            _handoffState = handoffState == "t3_lands" || handoffState == "first_sac_done"
                         || handoffState == "genel_ready" ? handoffState : "none";
            if (!string.IsNullOrEmpty(handoffState) && handoffState != "none" && _handoffState == "none")
                UnityEngine.Debug.LogWarning($"[TideHeadless] handoffState '{handoffState}' 非法"
                    + "（认 none/t3_lands/first_sac_done/genel_ready），本局关闭交棒");
            _maxOpeningTurns = maxOpeningTurns;
            _handoffTurn = 0;
            _handoffLands = 0;
            _handoffGenCostX10 = -1;
            _handoffGenInHand = false;
            _handoffMiss = "";
            _handoffDone = _handoffState == "none";
            _stepsAfterHandoff = 0;        // S3-1：交棒后步距读数每局清零
            _handoffStepsToGenel = -1;
            _handoffForced = false;
            if (!_handoffDone && modelIsP1.HasValue && _modelPlayer != null)
            {
                var modelDeck = ReferenceEquals(_modelPlayer, _core.Player1) ? deck1 : deck2;
                _openingAi = new SimpleAI(AiStrategy.AutoMatch(modelDeck));
            }
            else
            {
                _handoffDone = true; // 自对弈/防御：交棒只对 vs 模式有意义
                _openingAi = null;
            }
            // P3（2026-11-19 §4.1 建·A0 2026-11-24 逐张弃）：模型座手牌上限弃牌改停靠决策
            //（DiscardChoice 行=每张手牌一行，引擎有界循环逐张推进）——
            // 自对弈（_modelPlayer=null）双座皆模型；vs 模式仅模型座；
            // S3-1：交棒前模型座弃牌也归脚本代选（闭包实时读 _handoffDone——开手回合超限
            // 自动代弃，交棒后才停靠问模型）
            HandLimitDiscardGate.Reset();
            HandLimitDiscardGate.Bind(p => _modelPlayer == null
                || (_handoffDone && ReferenceEquals(p, _modelPlayer)));

            // 决策循环每局装配（2026-11-27 S1）：停靠链/循环治理状态归零 + 本局引用注入
            //（原 ResetCore 内联的终局/停靠/摘除/L1-L2 状态复位全部在 loop.BeginGame——
            // 须在任何 Advance/Apply/BuildStop 之前；scriptVsScript 的终态与枚举同样依赖 loop）。
            _loop.BeginGame(_core, _board, _modelPlayer, _aiCtrl, simpleAI);

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
                // S3-0a/0b（2026-11-26 二期 §5）：脚本局引擎侧动作行探针——启用（TIDE_PROBE_ACTIONS /
                // 编辑器静态赋值）才落流；未启用=零开销零行为。未启用时内联读取属性避免目录扫描。
                if (!string.IsNullOrEmpty(ScriptActionProbe.ActionsLogPath))
                    ScriptActionProbe.BeginGame(_core, deck1, deck2, rngSeed, "script");
                // S3-2（2026-11-26 二期 §5）：脚本 BC 录制（拦截匹配，见 ScriptBcRecorder 头注）——
                // 同一 scriptVsScript 驱动环，OutputDir 未设=零开销
                if (!string.IsNullOrEmpty(ScriptBcRecorder.OutputDir))
                    ScriptBcRecorder.BeginGame(_core, _board, rngSeed, deck1, deck2);
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
                    ScriptActionProbe.EndGame(_loop.Winner, _loop.Reason); // S3-0a：流收尾（未启用=零操作）
                    ScriptBcRecorder.EndGame(_loop.Winner, _loop.Reason,
                        _core?.TurnEngine?.TurnNumber ?? 0);      // S3-2：BC 流收尾（未启用=零操作）
                }
                return BuildResult(0f);
            }

            _loop.AdvanceToDecision();
            return BuildResult(0f);
        }

        /// <summary>执行一步：应用动作下标 → 结算/响应窗口 → 推进 → 算奖励 → 返回下一决策点。
        /// 2026-11-27 S1：决策点循环（动作应用分支 / 推进 / 响应窗停靠 / L1-L3 收口 / 停靠枚举）
        /// 已抽 BattleDecisionLoop.Apply/BuildStop（逐行迁移零改动）；本方法只保留训练侧职责——
        /// 奖励（λ·ΔΦ / S3-3 链势能 / 终局 ±1）与协议组装。状态码↔原早退路径逐位对应：
        /// Rejected=引擎拒绝摘除（扣分），OutOfBounds/StallDraw/GateOpen/GatePending=原 BuildResult(0f)。</summary>
        public TideStepResult Step(int actionIndex)
        {
            if (_loop.IsGameOver) return BuildResult(0f); // 已终局：无动作可执行

            var me = _loop.Decider ?? _core.TurnEngine.TurnPlayer; // 本步决策座次（Main=回合玩家 / 响应=优先权方）
            // S3-1 验收读数：交棒后步距计数（每模型座决策步 +1；开手回合不走 Step 故只计交棒后）
            if (_handoffState != "none" && _handoffDone && _handoffStepsToGenel < 0)
                _stepsAfterHandoff++;
            // 2026-11-23 成长指标：本步动手前的 T1/T3 地数快照（红/绿招牌操作分析用）
            SnapshotGrowth(me);
            // 2026-11-23 战斗伤害计价：每步清零（本步内的 CombatDamageEvent 由 OnCombatDamage 记账）
            _combatDmgSelfStep = _combatDmgOppStep = 0;
            // 2026-11-23 用户定案（塑形口径 v2）：势能改为 **含手牌**——与定价体系对齐
            //（手牌每张固定 3 费、地牌每块 2 费、横置产元素 +1——抽牌/墓地回收各 +3/张，
            //  权重=FieldValueReward A4 2026-11-24 定案值，旧「一张卡≈2费」口径已废弃）。
            // 旧 2026-09-22「剔除手牌」是为抑制"摸牌白拿奖励"，但那条已由 λ 0.05→0.005 与
            // 「横置=产出元素」新口径取代；若再出现拖局刷塑形，回滚点=此处改回 false。
            // λ=0 基线跳过计算（非终局奖励恒 0，势能白算）
            float before = _shapingLambda > 0f
                ? FieldValueReward.ComputePotential(_core, me, includeHand: true)
                : 0f;
            // S3-3：链进度势能基线（本步动手前 decider 的闩位——与 Φ 塑形独立，λ=0 塑形基线不影响）
            int chainBefore = SeatOf(me) is >= 0 and <= 1 ? _chainBits[SeatOf(me)] : 0;

            // 决策循环单源：应用→推进→L1 指纹（loop 内；B4 仪表经 AfterActionApplied 钩子在
            // 推进前即时计——与原 Step 内联位逐位一致，回合号口径不变）
            var outcome = _loop.Apply(actionIndex);

            // 奖励：终局 ±1 主导（不再叠加塑形，避免 Φ 在终局无界）；λ=0 基线非终局恒 0
            //（纯终局口径——塑形与无进展扣分全部关闭）；否则势能塑形 λ·ΔΦ
            float reward;
            if (outcome.Status == LoopStepStatus.Rejected)
            {
                // 引擎拒绝（枚举/引擎口径漂移）：小额扣分（即时信号：引导避开无效动作）。
                // 2026-09-22 符号修复：此前误传 +NoProgressPenalty（正奖励）——拒绝动作反而
                // +0.05，构成可刷的奖励泉；取负恢复扣分本意。λ=0 基线不扣分（保持「reward
                // 只有终局 ±1」纯终局口径；流动靠 loop 内摘除保证）。
                reward = _shapingLambda > 0f ? -BattleDecisionLoop.NoProgressPenalty : 0f;
            }
            else if (outcome.Status != LoopStepStatus.Advanced)
            {
                reward = 0f; // 越界/停滞判和/拾取开闸/闸内余步：原 Step 早退 BuildResult(0f)
            }
            else if (_loop.IsGameOver)
            {
                reward = ReferenceEquals(_loop.Winner, me) ? 1f : -1f;
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
            // S3-3 链进度势能（独立于 λ·ΔΦ 塑形与终局 ±1；γ=1 下 telescoping——轨迹总回报不变、
            // 只改入账位置，验收读入账步距不读总量）。λ_chain=0 关；里程碑只在 decider 自己的
            // 动作结算里闩立 → Δ≥0 单调。
            if (_chainLambda != 0f)
            {
                int seatMe = SeatOf(me);
                if (seatMe is >= 0 and <= 1)
                    reward += _chainLambda * ((_chainBits[seatMe] & 1) - (chainBefore & 1)
                                              + ((_chainBits[seatMe] & 2) - (chainBefore & 2)) / 2f
                                              + ((_chainBits[seatMe] & 4) - (chainBefore & 4)) / 4f
                                              + ((_chainBits[seatMe] & 8) - (chainBefore & 8)) / 8f) / 4f;
            }

            return BuildResult(reward);
        }

        /// <summary>按局覆盖链进度奖励系数（S3-3，reset 协议透传）。0=关奖励（闩位计数照常）；
        /// 计划初值 0.02（势能量级 ≪ 终局 ±1，与 §2 塑形历史坑同口径）；不调用保持 0（旧行为）。</summary>
        public void SetChainLambda(float lambda) => _chainLambda = lambda;

        /// <summary>闩位（bit0..3；幂等——重复达成不重复计）。</summary>
        private void LatchChain(int seat, int bit)
        {
            if (seat is < 0 or > 1) return;
            _chainBits[seat] |= 1 << bit;
        }

        /// <summary>卡名包含匹配（卡面全名如「系命者 阿格洛斯」——子串口径）。</summary>
        private static bool NameHas(Card c, string frag)
            => c != null && (ScriptActionProbe.CardNameOf(c) ?? "").Contains(frag);

        /// <summary>S3-3 里程碑①：池地闩位（绿/红=3 地、蓝=2 地——蓝套 T2 起放 2 地的曲线口径，
        /// BlueControlStrategy 头注）。</summary>
        private void OnChainPoolAdd(ElementPoolAddEvent e)
        {
            if (_writerProbeNoop()) return;
            int seat = SeatOf(e?.Player);
            string theme = seat is < 0 or > 1 ? "" : _seatTheme[seat];
            if (string.IsNullOrEmpty(theme)) return;
            int lands = LandCountOf(e.Player);
            if (theme == "blue" ? lands >= 2 : lands >= 3) LatchChain(seat, 0);
            // 绿②→③ 重铺：归根施放闩位已立后再放地（G5 replant 的势能化——去窗口化单调闩）
            if (theme == "green" && (_chainBits[seat] & 2) != 0) LatchChain(seat, 2);
        }

        /// <summary>S3-3 里程碑：出牌类（归根/阿格洛斯/血偿/炸弹/深潭碑/潮汐；红 combo 同回合双法术）。</summary>
        private void OnChainCardPlay(CardPlayEvent e)
        {
            if (_writerProbeNoop()) return;
            int seat = SeatOf(e?.Player);
            string theme = seat is < 0 or > 1 ? "" : _seatTheme[seat];
            if (string.IsNullOrEmpty(theme)) return;
            var card = e.PlayedCard;
            if (theme == "green")
            {
                if (NameHas(card, "归根")) LatchChain(seat, 1);
                else if (NameHas(card, "阿格洛斯")) LatchChain(seat, 3);
            }
            else if (theme == "red")
            {
                if (NameHas(card, "血偿") || NameHas(card, "活体炸弹"))
                {
                    int turn = _core.TurnEngine.TurnNumber;
                    if (turn != _redSpellTurn[seat])
                    {
                        _redSpellTurn[seat] = turn;
                        _redBlood[seat] = _redBomb[seat] = false;
                    }
                    if (NameHas(card, "血偿")) _redBlood[seat] = true;
                    else _redBomb[seat] = true;
                    if (_redBlood[seat] && _redBomb[seat]) LatchChain(seat, 3); // 同回合双法术=斩杀尝试
                }
            }
            else if (theme == "blue")
            {
                if (NameHas(card, "深潭碑")) LatchChain(seat, 1);
                else if (NameHas(card, "潮汐")) LatchChain(seat, 2);
            }
        }

        /// <summary>S3-3 蓝链④：潮痕碑衍生物入场战场（召唤 token 不走 CardPlayEvent——走移区）。</summary>
        private void OnChainZoneChange(CardZoneChangeEvent e)
        {
            if (_writerProbeNoop()) return;
            if (e?.Card == null || e.NewZone != Zone.Battlefield) return;
            int seat = SeatOf(e.Controller);
            string theme = seat is < 0 or > 1 ? "" : _seatTheme[seat];
            if (theme == "blue" && NameHas(e.Card, "潮痕碑")) LatchChain(seat, 3);
        }

        /// <summary>链事件空守卫：核心未初始化或局未开始时忽略（ResetCore 早段的事件火花）。</summary>
        private bool _writerProbeNoop() => _core == null;

        /// <summary>卡组主题识别（S3-3）：三预组各认一张签名卡；其余卡组=空串（无链奖励）。</summary>
        private static string DetectTheme(List<CardData> deck)
        {
            if (deck == null) return "";
            foreach (var cd in deck)
            {
                var n = cd?.CardName;
                if (string.IsNullOrEmpty(n)) continue;
                if (n.Contains("归根")) return "green";
                if (n.Contains("深潭碑")) return "blue";
                if (n.Contains("血偿")) return "red";
            }
            return "";
        }

        // ===================================================== S3-1 开手交棒 =====================================================

        /// <summary>交棒触发态评估（模型座回合开始时调；handoffState 语义见字段注释）。
        /// genel_ready = 手牌含归根（未在施放中）且引擎口径可付（CanAffordCast 含动态费 6.0/8.0
        /// ——不按印刷 8 费硬编码）且池地≥3（计划 §5 触发条件三要素）。</summary>
        private bool HandoffTriggerMet()
        {
            if (_modelPlayer == null || _core == null) return true;
            switch (_handoffState)
            {
                case "t3_lands": return LandCountOf(_modelPlayer) >= 3;
                case "first_sac_done": return _mSacSeen;
                case "genel_ready":
                    if (LandCountOf(_modelPlayer) < 3) return false;
                    var genel = (_core.ZoneManager.GetCards(_modelPlayer, Zone.Hand) ?? new List<Card>())
                        .FirstOrDefault(c => c != null && c.ID == IdGreenRecall && !_core.StackEngine.IsPendingCast(c));
                    if (genel == null) return false;
                    _handoffGenCostX10 = (int)Math.Round(GameActions.GetCardCost(genel, 0).Total * 10f);
                    return GameActions.CanAffordCast(_core, _modelPlayer, genel, 0);
                default: return true; // 未知档位=立即交棒（防御）
            }
        }

        /// <summary>完成交棒：打点（回合/池地/剩余元素/动态费/手牌是否含归根/是否强制）+ 摘除开手脚本。
        /// forced=**回合兜底**（maxOpeningTurns 到顶强制交棒，与 genel_ready 触发无关）——
        /// forced 局的 gen_in_hand 可为 false（手无归根）；S3-1 验收按 forced 拆分读，防兜底污染
        ///（2026-11-26 DSH：上批冒烟 5/7 局是兜底，gen_in_hand 2/7 由此解释）。</summary>
        private void CompleteHandoff(bool forced)
        {
            if (_handoffDone) return;
            _handoffDone = true;
            _handoffForced = forced;
            _handoffTurn = _core.TurnEngine.TurnNumber;
            _handoffLands = LandCountOf(_modelPlayer);
            var genel = (_core.ZoneManager.GetCards(_modelPlayer, Zone.Hand) ?? new List<Card>())
                .FirstOrDefault(c => c != null && c.ID == IdGreenRecall);
            _handoffGenInHand = genel != null;
            if (_handoffGenInHand)
                _handoffGenCostX10 = (int)Math.Round(GameActions.GetCardCost(genel, 0).Total * 10f);
            else if (_handoffState != "genel_ready")
                _handoffGenCostX10 = -1; // 非 genel_ready 档位不承诺归根在手
            _openingAi = null;           // 交棒后开手脚本退役（GC）
            _stepsAfterHandoff = 1;      // 交棒即起算（首个停靠步=第 1 步）
        }

        /// <summary>S3-1 开手交棒钩子（loop.AdvanceToDecision 的模型座回合停靠口调；
        /// 原 AdvanceToDecision 内联 handoff 块 + RunOpeningTurnAsync，2026-11-27 S1 换钩子——
        /// 三态语义逐位保留：交棒前模型座回合整段交开手脚本〔开手回合响应窗经 AiStrategyRegistry
        /// （模型座已绑开手策略）同步决策，不停靠〕；触发/兜底交棒后返回 None 落停靠口问模型）。</summary>
        private ModelTurnIntercept DriverInterceptModelTurn()
        {
            if (_handoffDone) return ModelTurnIntercept.None; // 交棒后（或 none 档）恒直通模型
            if (_loop.ScriptTurnChainActive) return ModelTurnIntercept.ChainInFlight; // 开手链在途（防御）
            bool forced = _maxOpeningTurns > 0 && _core.TurnEngine.TurnNumber >= _maxOpeningTurns;
            if (!forced && !HandoffTriggerMet())
            {
                _loop.RunExternalTurnChain(_openingAi, "开手脚本"); // 模型座开手回合整段脚本驱动
                return ModelTurnIntercept.ChainStarted;
            }
            CompleteHandoff(forced); // 触发（或 maxOpeningTurns 兜底）→ 交棒，loop 落本停靠口问模型
            return ModelTurnIntercept.None;
        }

        /// <summary>释放：反订阅终局、撤销棋盘接线（静态扩展点归零）。</summary>
        public void Dispose()
        {
            HandLimitDiscardGate.Reset(); // P3 §4.1：解绑+清挂起
            HandLimitDiscardGate.Bind(null);
            TargetPickGate.Reset(); // A6：释放弃残留拾取序列
            // D2c/D2d（2026-11-22）：vs 模式响应接线还原（镜像 AiDuelRunner.Run finally）——
            // 注册表/AiResponder/AiAsyncResponder 是全局静态，不还原会串入同进程后续局
            AiStrategyRegistry.Clear();
            ResponseWindowService.AiResponder = null;
            ResponseWindowService.AiAsyncResponder = null;
            EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            EventManager.Instance.Unsubscribe<CombatDamageEvent>(OnCombatDamage); // 2026-11-23 战斗伤害记账
            // S3-3：链进度闩位订阅随驱动器生命周期反订阅（防跨局/跨进程泄漏）
            EventManager.Instance.Unsubscribe<CardPlayEvent>(OnChainCardPlay);
            EventManager.Instance.Unsubscribe<ElementPoolAddEvent>(OnChainPoolAdd);
            EventManager.Instance.Unsubscribe<CardZoneChangeEvent>(OnChainZoneChange);
            ScriptActionProbe.UnsubscribeAll(); // S3-0a 探针（进程级一次性订阅）随 Dispose 归零
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
            // 终局三态 + 两门清理（弃牌门 CompleteDefault / 拾取闸 Reset）随决策循环在 loop 侧收口
            //（2026-11-27 S1 迁入 BattleDecisionLoop.MarkGameOver——原 OnGameOver 前半逐位一致）
            _loop.MarkGameOver(e.Winner, e.Reason.ToString());
            // S3-1：终局仍未交棒 → 未命中原因（协议 info.handoffMiss；命中率进 stats.jsonl 用）
            if (!_handoffDone) _handoffMiss = "ended";
        }

        private int SeatOf(Player p)
            => p == null || _core == null ? -1 : (ReferenceEquals(p, _core.Player1) ? 0 : 1);

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

        /// <summary>2026-11-23 行为仪表：记录一次「模型侧已成功应用」的动作（loop 的
        /// AfterActionApplied 钩子调——应用成功后、推进前，与原 Step 内联位时序一致）。
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
                // S3-1 验收读数：交棒后首次归根施放步距（开手回合不经过本函数——天然只计交棒后）
                if (_handoffState != "none" && _handoffDone && _handoffStepsToGenel < 0)
                    _handoffStepsToGenel = Math.Max(_stepsAfterHandoff, 1);
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

        /// <summary>
        /// 全脚本局主循环（2026-11-22 归因取证，opponent="script"）：镜像 AiDuelRunner.Run 的
        /// 回合推进（回合玩家 SimpleAI 整回合 TakeTurn → End→Standby 补泵），环境细节对齐
        /// 本驱动的 vs 模式（棋盘接线 D1 已统一；响应接线 D2b/D2c 已统一——窗口由 TakeTurn
        /// 内部的响应窗泵就近收口，见下方残留排干注释）。
        /// 异常按 SimpleAIError 收口（无胜者=平局，不炸训练服务）；回合上限到顶未终结 =
        /// MaxTurns 收口（无胜者=平局）；单局墙钟超 ScriptGameTimeoutSec = GameTimeout 收口
        /// （无胜者=平局，A1 2026-11-22——绿镜像慢局/病态局不再无限占时）。
        /// 2026-11-27 S1：终局三态读写改经 _loop（ForceTerminal/IsGameOver——原直接字段写逐位一致）。
        /// </summary>
        private void RunScriptGameToEnd()
        {
            var wall = System.Diagnostics.Stopwatch.StartNew();
            for (int t = 0; t < ScriptMaxTurns && !_loop.IsGameOver; t++)
            {
                if (wall.Elapsed.TotalSeconds > ScriptGameTimeoutSec)
                {
                    _loop.ForceTerminal(null, "GameTimeout");
                    return;
                }
                var p = _core.TurnEngine.TurnPlayer;
                var ai = ReferenceEquals(p, _core.Player1) ? _scriptAi1 : _scriptAi2;
                try
                {
                    ai.TakeTurn(_aiCtrl);
                    if (!_loop.IsGameOver)
                        _core.TurnEngine.CheckPhaseTransition();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[TideHeadless] 全脚本局 SimpleAI 回合异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                    _loop.ForceTerminal(null, "SimpleAIError");
                    return;
                }
                // 回合残留栈/待发效果：排干（**D2b 后仅防御性**——TakeTurn 内部的响应窗泵已由
                // AiResponder=PickFor 走策略决策，正常回合此循环零命中；仅当栈异常外溢时防卡死）
                for (int i = 0; i < BattleDecisionLoop.ResponseWindowGuard && !_loop.IsGameOver; i++)
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
            if (!_loop.IsGameOver)
            {
                _loop.ForceTerminal(null, "MaxTurns");
            }
        }

        private TideStepResult BuildResult(float reward)
        {
            // 停靠观测/动作表/L1-L3 外显/回合计数：loop.BuildStop 单源（四路枚举 + 回合切换复位，
            // 2026-11-27 S1 迁入 BattleDecisionLoop——本方法只组装训练协议 info 键，集合不变）。
            var stop = _loop.BuildStop();
            return new TideStepResult
            {
                Obs = stop.Obs,
                Legal = stop.Legal,
                Reward = reward,
                Done = _loop.IsGameOver,
                Winner = _loop.Winner,
                Reason = _loop.Reason,
                Turn = _core.TurnEngine.TurnNumber,
                ModelSeat = ModelSeat,
                DecisionSeat = _loop.IsGameOver ? -1 : SeatOf(stop.Decider),
                ResponseStop = stop.ResponseStop,
                LoopPruned = stop.LoopPruned,   // L3-A：收口那一步 true（协议 info.loopPruned）
                LoopReason = stop.LoopReason,  // 协议 info.loopReason（cycle/stall/budget/""）
                TurnActions = stop.TurnActions, // 协议 info.turnActions（本回合已用步数）
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
                ModelAtkSafe = _mAtkSafe,
                ModelAtkRisky = _mAtkRisky,
                ModelAtkFace = _mAtkFace,
                ModelAtkAny = _mAtkAny,
                // S3-1/S3-3（2026-11-26 二期）：交棒打点 + decider 链闩位（终局步=全量）
                HandoffState = _handoffState,
                HandoffDone = _handoffDone,
                HandoffTurn = _handoffTurn,
                HandoffLands = _handoffLands,
                HandoffGenCost = _handoffGenCostX10 / 10f,
                HandoffGenInHand = _handoffGenInHand,
                HandoffMiss = _handoffMiss ?? "",
                HandoffStepsToGenel = _handoffStepsToGenel,
                HandoffForced = _handoffForced,
                ChainBits = SeatOf(stop.Decider) is >= 0 and <= 1 ? _chainBits[SeatOf(stop.Decider)] : 0,
                ChainDone = SeatOf(stop.Decider) is >= 0 and <= 1 && (_chainBits[SeatOf(stop.Decider)] & 8) != 0,
            };
        }

    }
}
