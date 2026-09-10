using System;
using System.Collections.Generic;
using CardCore;
using SynergyUI;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// 神经网络 AI（ONNX 策略驱动整回合）。与 SimpleAI 同形（TakeTurn(BattleController)），
    /// 可直接替换 BattleController 里的 SimpleAI 实例——活体对局接入点。
    ///
    /// 回合时序镜像 TideHeadlessDriver（训练口径，别镜像 SimpleAI——它是不同的时序）：
    ///   SkipElementPool → 回合初自动横置地牌（色匹配启发式，不建模）
    ///   → 决策循环：枚举合法动作 → TideObservation 观测 → 策略 argmax → Apply → 排干栈
    ///     （引擎拒绝的动作按签名本回合摘除，保证回合必然流动）
    ///   → EndTurn：先结算本回合攻击 → 排干死亡触发 → EndTurn。
    /// rstate 局内连续传递：新对局由宿主调 ResetEpisode()（镜像训练 env 复位口径）。
    /// </summary>
    public sealed class NeuralAI
    {
        private const int MaxSettleAttempts = 32;  // 排干栈重试上限（镜像 TideHeadlessDriver）
        private const int MaxActionsPerTurn = 128; // 单回合动作数上限（防死循环保险）

        private readonly OnnxTidePolicy _policy;
        private readonly LegalActionEnumerator _legal = new LegalActionEnumerator();
        private readonly HashSet<string> _bannedThisTurn = new HashSet<string>();

        public NeuralAI(OnnxTidePolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        /// <summary>新对局：GRU 隐状态归零（宿主在 InitGame/StartNewGame 后调用）。</summary>
        public void ResetEpisode() => _policy.Reset();

        /// <summary>驱动当前回合玩家的整个回合（镜像 SimpleAI.TakeTurn 的调用约定）。</summary>
        public void TakeTurn(BattleController ctrl)
        {
            var core = ctrl.Core;
            var me = ctrl.TurnPlayer;
            if (core == null || me == null || me != core.TurnEngine.TurnPlayer) return;
            _bannedThisTurn.Clear();

            // 回合准备：跳过产元素阶段 → 回合初横置全部地牌（训练时由 driver 代办，不建模）
            GameActions.SkipElementPool(core, me);
            TapAllLands(core, me);

            for (int step = 0; step < MaxActionsPerTurn && !core.IsGameOver; step++)
            {
                _legal.Enumerate(core, me);
                _legal.RemoveAll(_bannedThisTurn);
                if (_legal.Count == 0) break; // 不应发生（EndTurn 兜底恒在）

                int idx = _policy.Select(TideObservation.Build(core), _legal);
                if (idx < 0) break;
                var a = _legal.Actions[idx];

                if (a.Type == TideActionType.EndTurn)
                {
                    EndTurnSequence(core, me);
                    return;
                }

                bool ok = LegalActionEnumerator.Apply(core, me, a);
                if (!core.IsGameOver) GameActions.DrainStack(core, MaxSettleAttempts);
                if (!ok)
                {
                    // 引擎拒绝（枚举/引擎口径漂移）：本回合摘除，重观测再选（镜像 driver）
                    _bannedThisTurn.Add(a.Signature);
                }
            }

            // 动作数超限 / 异常出口：强制收口，保证回合流动
            EndTurnSequence(core, me);
        }

        /// <summary>EndTurn 时序（镜像 TideHeadlessDriver.Step）：结算攻击 → 排干 → 结束回合。</summary>
        private static void EndTurnSequence(GameCore core, Player me)
        {
            ResolveCombat(core);
            if (!core.IsGameOver) GameActions.DrainStack(core, MaxSettleAttempts);
            if (!core.IsGameOver) GameActions.EndTurn(core, me);
        }

        /// <summary>战斗结算（镜像 BattleController.ResolveCombat / driver）：声明收口 → 阻挡收口伤害落地。</summary>
        private static void ResolveCombat(GameCore core)
        {
            var combat = core.CombatSystem;
            if (!combat.InCombat) return;
            combat.EndAttackDeclaration();
            if (combat.InCombat)
                combat.EndBlockDeclaration();
        }

        // ======================================== 地牌横置（复刻 TideHeadlessDriver 启发式） ========================================

        /// <summary>回合初横置全部未横置地牌：产色按手牌费用需求匹配，无需求色回落剩余指示物最多的颜色。</summary>
        private static void TapAllLands(GameCore core, Player me)
        {
            var demand = BuildColorDemand(core, me);
            foreach (var land in new List<PooledCard>(core.ElementPool.GetPooledCards(me)))
            {
                var color = SelectLandColor(land, demand);
                if (color.HasValue)
                    GameActions.GainElementFromToken(core, me, land, color.Value);
            }
        }

        /// <summary>手牌费用色需求直方图（决定横置产色的优先级）。</summary>
        private static Dictionary<ManaType, int> BuildColorDemand(GameCore core, Player me)
        {
            var demand = new Dictionary<ManaType, int>();
            foreach (var card in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
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
