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
    ///   回合已由 TurnEngine.StartNewTurn 自动推进到主阶段（SkipElementPool 兼容口在 Main 恒返回 false，调用无副作用）
    ///   → 决策循环：枚举合法动作 → TideObservation 观测 → 策略 argmax → Apply → 排干栈
    ///     （引擎拒绝的动作按签名本回合摘除，保证回合必然流动）
    ///   → EndTurn：先结算本回合攻击 → 排干死亡触发 → EndTurn。
    /// rstate 局内连续传递：新对局由宿主调 ResetEpisode()（镜像训练 env 复位口径）。
    /// </summary>
    public sealed class NeuralAI
    {
        private const int MaxSettleAttempts = 32;  // 排干栈重试上限（镜像 TideHeadlessDriver）
        private const int MaxActionsPerTurn = 256; // 单回合动作数上限（v2 契约 128→256，与 TideHeadlessDriver 同步）

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

            // 回合准备：跳过产元素阶段（2026-10-11 现取现用：无回合初横置——付费时系统自动横置）
            GameActions.SkipElementPool(core, me);

            for (int step = 0; step < MaxActionsPerTurn && !core.IsGameOver; step++)
            {
                _legal.Enumerate(core, me);
                _legal.RemoveAll(_bannedThisTurn);
                if (_legal.Count == 0) break; // 不应发生（EndTurn 兜底恒在）

                // v3：传 BattleController 的棋盘占用层——观测位置特征（AttachBoard 已接线）
                int idx = _policy.Select(TideObservation.Build(core, me, false, ctrl.Board), _legal);
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

        /// <summary>EndTurn 时序（镜像 TideHeadlessDriver.Step）：排干 → 结束回合。
        /// 2026-09-16 战斗接入栈机器：攻击动作 Apply 后即经 DrainStack 逐攻击结算，
        /// 战斗收口（旧 ResolveCombat）退役。</summary>
        private static void EndTurnSequence(GameCore core, Player me)
        {
            if (!core.IsGameOver) GameActions.DrainStack(core, MaxSettleAttempts);
            if (!core.IsGameOver) GameActions.EndTurn(core, me);
        }

    }
}
