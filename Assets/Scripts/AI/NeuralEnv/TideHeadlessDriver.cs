using System;
using System.Collections.Generic;
using CardCore;
using SynergyUI;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// 无头驱动器的单步决策快照：当前回合玩家的观测 + 合法动作表。
    /// （TideObservation.Build + LegalActionEnumerator.Enumerate 的一次组合封装。）
    /// </summary>
    public sealed class TideStepResult
    {
        public TideObservation Obs;         // 状态快照（cards_/global_，当前回合玩家视角）
        public LegalActionEnumerator Legal; // 合法动作表（含 Features 张量，Actions.Count × NAction）
        public float Reward;                // 本步奖励（终局 ±1 或 λ·ΔΦ 塑形）
        public bool Done;                   // 对局是否结束
        public Player Winner;               // 终局胜者（未结束为 null）
        public string Reason;               // 终局原因（未结束为 null）
        public int Turn;                    // 当前全局回合数
        public int ModelSeat;               // 模型座次（0=P1 先手 / 1=P2）；自对弈为 -1
    }

    /// <summary>
    /// 无头对局驱动器：把「模型在环」的决策循环收敛成 reset/step 两步接口。
    /// 复用 AiBattleDriver.RunFullGame 的无头初始化骨架（InitGame + 双方 IsAI + 棋盘接线），
    /// 但把决策点替换为「读动作 → GameActions 调用 → 快照 Φ 做差算塑形奖励」。
    ///
    /// 时序（每回合）：
    ///   Standby → SkipElementPool → 回合初自动横置地牌产费 → Main（模型决策点）
    ///   → 模型选动作 → 应用 → 排干栈 →（EndTurn 时先结算战斗）→ 阶段推进折返
    ///   → 下一决策点（可能是对方回合）。
    ///
    /// 奖励（actor-centric 自对弈，reward 恒归属「刚行动的那一方」）：
    ///   终局 ±1 主导；非终局每步 λ·(Φ_after − Φ_before)，Φ = 己方战场价值 − 对方战场价值。
    ///
    /// v1 文档化简化：
    ///  - 地牌横置由驱动回合初自动完成（色匹配启发式，逻辑复刻 SimpleAI.TapAllLands），不建模；
    ///    模型打出的地牌要下一回合初才被横置产费（比 SimpleAI 的「立即横置」更接近 MTG 惯例）。
    ///  - 出牌/施放目标由引擎自动解析（PlayCard 传 null targets）——目标枚举留 v2。
    ///  - 战斗：模型逐个 DeclareAttack，EndTurn 时统一 ResolveCombat 结算伤害。
    /// </summary>
    public sealed class TideHeadlessDriver
    {
        private const float ShapingLambda = 0.05f;   // 塑形系数（终端 ±1 恒为主导）
        private const int MaxSettleAttempts = 32;    // 排干栈重试上限（镜像 SimpleAI）
        private const int PhaseAdvanceGuard = 64;    // 阶段推进循环保险
        private const float NoProgressPenalty = 0.05f; // 无进展动作扣分（即时信号：引导避开无效动作）
        private const int MaxActionsPerTurn = 128;     // 单回合动作数上限（可选操作变多后翻倍；仍防换状态翻转死循环）

        private GameCore _core;
        private GameBoard.BoardState _board;
        private LegalActionEnumerator _legal;
        private bool _gameOver;
        private Player _winner;
        private string _reason;

        // vs SimpleAI 对手位：_modelPlayer != null 时，非模型回合由 SimpleAI 整回合自动打
        private Player _modelPlayer;
        private BattleController _aiCtrl;
        private readonly SimpleAI _simpleAI = new SimpleAI();

        // 回合内无进展防护：引擎拒绝（枚举/引擎口径漂移）的动作按签名本回合摘除 + 小额扣分——
        // 否则确定性策略会无限重选同一无效动作，回合冻结烧满步数上限、对局永不自然终局
        private readonly HashSet<string> _bannedThisTurn = new HashSet<string>();
        private int _banTurnStamp = -1;
        private int _actionsThisTurn;

        public bool IsGameOver => _gameOver;
        public Player Winner => _winner;

        /// <summary>模型座次（0=P1 先手 / 1=P2）；自对弈为 -1。协议 info.modelSeat 用。</summary>
        public int ModelSeat
            => _modelPlayer == null || _core == null ? -1
             : (ReferenceEquals(_modelPlayer, _core.Player1) ? 0 : 1);

        public TideHeadlessDriver()
        {
            // 订阅终局（全局事件总线；Dispose 反订阅，避免跨局泄漏）
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
        }

        /// <summary>初始化一局（自对弈：双方座位都由模型驱动）。返回首个决策点观测。</summary>
        public TideStepResult Reset(List<CardData> deck1, List<CardData> deck2)
            => ResetCore(deck1, deck2, null);

        /// <summary>
        /// 初始化一局 vs SimpleAI：modelIsP1 指定模型座次，另一座位整回合由 SimpleAI 自动打。
        /// obs/合法动作/reward 恒为模型视角（对手回合在内部自动完成，Python 只见模型决策点）；
        /// 非终局塑形的 ΔΦ 口径随之变为「模型行动 + 对手整回合响应」的弧长，终局 ±1 仍按模型胜负。
        /// </summary>
        public TideStepResult Reset(List<CardData> deck1, List<CardData> deck2, bool modelIsP1)
            => ResetCore(deck1, deck2, modelIsP1);

        private TideStepResult ResetCore(List<CardData> deck1, List<CardData> deck2, bool? modelIsP1)
        {
            // 变形目标形态解析器：组合根注入（镜像 AiBattleDriver / BattleController）
            CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;

            _core = GameCore.Instance;
            _core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            _core.Player1.IsAI = true; // 目标/范围选择自动应答（不弹窗，异步同步完成）
            _core.Player2.IsAI = true;
            _modelPlayer = modelIsP1.HasValue ? (modelIsP1.Value ? _core.Player1 : _core.Player2) : null;
            _aiCtrl = modelIsP1.HasValue ? new BattleController() : null;

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
            _bannedThisTurn.Clear(); // 新对局：无进展防护状态归零（stamp 由 BuildResult 兜底重置）
            _actionsThisTurn = 0;

            AdvanceToDecision();
            return BuildResult(0f);
        }

        /// <summary>执行一步：应用动作下标 → 结算 → 推进 → 算奖励 → 返回下一决策点。</summary>
        public TideStepResult Step(int actionIndex)
        {
            if (_gameOver) return BuildResult(0f); // 已终局：无动作可执行

            var me = _core.TurnEngine.TurnPlayer;
            float before = FieldValueReward.ComputePotential(_core, me);

            // 越界下标：保守兜底——不推进、奖励 0，重观测同状态
            if (_legal == null || actionIndex < 0 || actionIndex >= _legal.Actions.Count)
                return BuildResult(0f);

            var a = _legal.Actions[actionIndex];
            _actionsThisTurn++;

            if (_actionsThisTurn > MaxActionsPerTurn)
            {
                // 保险：同回合动作数超限（如换状态翻转的死循环、EndTurn 被静默拒绝）→
                // 视同 EndTurn 强制收口，保证回合流动（疲劳/战斗最终能终结对局）
                ResolveCombat();
                if (!_gameOver) GameActions.DrainStack(_core, 64);
                if (!_gameOver) GameActions.EndTurn(_core, me);
            }
            else if (a.Type == TideActionType.EndTurn)
            {
                // EndTurn（镜像 SimpleAI 收尾时序，全部落在 Main 相位内）：
                //   先结算本回合已宣言的攻击（伤害落地）→ 排干死亡触发 → 才 EndTurn（Main→End）。
                //   若先 EndTurn 再结算，死亡触发会在 End 相位结算，与「直到阶段结束」类效果语义漂移。
                ResolveCombat();
                if (!_gameOver) GameActions.DrainStack(_core, MaxSettleAttempts);
                if (!_gameOver) GameActions.EndTurn(_core, me);
            }
            else
            {
                bool ok = LegalActionEnumerator.Apply(_core, me, a);
                if (!_gameOver) GameActions.DrainStack(_core, MaxSettleAttempts);

                if (!ok)
                {
                    // 引擎拒绝（枚举/引擎口径漂移）：本回合摘除该动作（重观测不再出现）+ 小额扣分，
                    // 不推进。摘除保证最坏情况把无效动作各试一次后只剩 EndTurn → 回合必然流动。
                    _bannedThisTurn.Add(a.Signature);
                    _legal.RemoveAll(_bannedThisTurn);
                    return BuildResult(NoProgressPenalty);
                }
            }

            // 推进到下一决策点（End→Standby 折返 / Standby→Main / 回合初横置；可能触发疲劳判负）
            AdvanceToDecision();

            // 奖励：终局 ±1 主导（不再叠加塑形，避免 Φ 在终局无界）；否则势能塑形 λ·ΔΦ
            float reward;
            if (_gameOver)
            {
                reward = ReferenceEquals(_winner, me) ? 1f : -1f;
            }
            else
            {
                float after = FieldValueReward.ComputePotential(_core, me);
                reward = FieldValueReward.ShapingDelta(before, after, ShapingLambda);
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

        /// <summary>
        /// 推进到「当前回合玩家的主阶段决策点」：
        ///   Standby → SkipElementPool → 回合初横置产费 → Main；
        ///   End → CheckPhaseTransition → 下一回合 Standby（循环折返）；
        ///   已在 Main → 直接返回。
        /// vs SimpleAI 模式：非模型回合不逐相位推进，直接整回合交给 SimpleAI（镜像 AiBattleDriver）。
        /// </summary>
        private void AdvanceToDecision()
        {
            for (int i = 0; i < PhaseAdvanceGuard && !_gameOver; i++)
            {
                var me = _core.TurnEngine.TurnPlayer;

                // vs SimpleAI：对手回合整段自动打（TakeTurn 自带回合准备与 EndTurn，不折返）
                if (_modelPlayer != null && me != _modelPlayer)
                {
                    RunSimpleAiTurn();
                    continue;
                }

                var phase = _core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby;
                if (phase == PhaseType.Main) return;
                if (phase == PhaseType.Standby)
                {
                    GameActions.SkipElementPool(_core, me);
                    TapAllLands(me);
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

        /// <summary>战斗结算（镜像 BattleController.ResolveCombat）：EndAttackDeclaration → EndBlockDeclaration 伤害落地。</summary>
        private void ResolveCombat()
        {
            var combat = _core.CombatSystem;
            if (!combat.InCombat) return;
            combat.EndAttackDeclaration();
            if (combat.InCombat)
                combat.EndBlockDeclaration();
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

            var obs = TideObservation.Build(_core);
            _legal = new LegalActionEnumerator();
            _legal.Enumerate(_core, _core.TurnEngine.TurnPlayer);
            _legal.RemoveAll(_bannedThisTurn); // 本回合已判无效的动作不再出现在选项表
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
            };
        }

        // ===================================================== 地牌横置（复刻 SimpleAI 色匹配启发式） =====================================================

        /// <summary>回合初横置全部未横置地牌：产色按手牌费用需求匹配，无需求色回落剩余指示物最多的颜色。</summary>
        private void TapAllLands(Player me)
        {
            var demand = BuildColorDemand(me);
            foreach (var land in new List<PooledCard>(_core.ElementPool.GetPooledCards(me)))
            {
                var color = SelectLandColor(land, demand);
                if (color.HasValue)
                    GameActions.GainElementFromToken(_core, me, land, color.Value);
            }
        }

        /// <summary>手牌费用色需求直方图（决定横置产色的优先级）。</summary>
        private Dictionary<ManaType, int> BuildColorDemand(Player me)
        {
            var demand = new Dictionary<ManaType, int>();
            foreach (var card in _core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (!(card is IHasCost hc) || hc.Cost == null) continue;
                foreach (var kv in hc.Cost)
                {
                    var color = (ManaType)kv.Key;
                    int amount = (int)Math.Ceiling(kv.Value);
                    demand[color] = demand.TryGetValue(color, out var v) ? v + amount : amount;
                }
            }
            return demand;
        }

        /// <summary>单地产色决策：需求色优先（需求量大者优先），无需求色取剩余指示物最多者。</summary>
        private static ManaType? SelectLandColor(PooledCard land, Dictionary<ManaType, int> demand)
        {
            ManaType? best = null;
            int bestScore = 0;
            foreach (var color in land.GetAvailableColors())
            {
                if (demand.TryGetValue(color, out var d) && d > bestScore)
                {
                    best = color;
                    bestScore = d;
                }
            }
            if (best != null) return best;

            ManaType? most = null;
            int mostTokens = 0;
            foreach (var kv in land.Tokens)
            {
                if (kv.Value > mostTokens)
                {
                    mostTokens = kv.Value;
                    most = kv.Key;
                }
            }
            return most;
        }
    }
}
