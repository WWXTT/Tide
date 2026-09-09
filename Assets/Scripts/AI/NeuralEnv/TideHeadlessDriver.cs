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

        private GameCore _core;
        private GameBoard.BoardState _board;
        private LegalActionEnumerator _legal;
        private bool _gameOver;
        private Player _winner;
        private string _reason;

        public bool IsGameOver => _gameOver;
        public Player Winner => _winner;

        public TideHeadlessDriver()
        {
            // 订阅终局（全局事件总线；Dispose 反订阅，避免跨局泄漏）
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
        }

        /// <summary>初始化一局（双方同卡组自对弈入口；deck 可为不同卡组）。返回首个决策点观测。</summary>
        public TideStepResult Reset(List<CardData> deck1, List<CardData> deck2)
        {
            // 变形目标形态解析器：组合根注入（镜像 AiBattleDriver / BattleController）
            CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;

            _core = GameCore.Instance;
            _core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            _core.Player1.IsAI = true; // 目标/范围选择自动应答（不弹窗，异步同步完成）
            _core.Player2.IsAI = true;

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
            if (a.Type == TideActionType.EndTurn)
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
                // 应用动作（引擎拒绝则静默——奖励 0 重观测，不推进）
                LegalActionEnumerator.Apply(_core, me, a);
                if (!_gameOver) GameActions.DrainStack(_core, MaxSettleAttempts);
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
        /// </summary>
        private void AdvanceToDecision()
        {
            for (int i = 0; i < PhaseAdvanceGuard && !_gameOver; i++)
            {
                var me = _core.TurnEngine.TurnPlayer;
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
            var obs = TideObservation.Build(_core);
            _legal = new LegalActionEnumerator();
            _legal.Enumerate(_core, _core.TurnEngine.TurnPlayer);
            return new TideStepResult
            {
                Obs = obs,
                Legal = _legal,
                Reward = reward,
                Done = _gameOver,
                Winner = _winner,
                Reason = _reason,
                Turn = _core.TurnEngine.TurnNumber,
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
