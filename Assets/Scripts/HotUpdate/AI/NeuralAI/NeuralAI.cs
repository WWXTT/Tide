using System;
using System.Collections.Generic;
using CardCore;
using SynergyUI;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// 神经网络 AI（ONNX 策略驱动）。与 SimpleAI 同形（TakeTurn(BattleController)），
    /// 可直接替换 BattleController 里的 SimpleAI 实例——活体对局接入点。
    ///
    /// **S2（2026-11-27 逐步驱动共享）**：自带整回合循环已删——回合驱动移交
    /// <see cref="BattleDecisionLoop"/>（与训练 TideHeadlessDriver 同一决策点循环：Main 停靠 +
    /// 响应窗停靠 + A6 目标拾取步 + L1-L3 循环收口），逐步口径=训练口径
    /// （docs/逐步驱动共享_重构计划.md）。改前整回合驱动（只枚举回合玩家 Main 相、响应窗直接
    /// DrainStack 吞掉、目标自动代选）是弱棋的根因——同 checkpoint 逐步驱动 63.9% vs 整回合 19.4%
    /// （旧模型口径；当前红教师整回合 47.2%，见计划 §0/§6）。
    ///
    /// 口径（用户 2026-11-27 定案）：loop 只在「神经座自己的决策点」停靠；
    /// 人类/非神经座的响应窗维持现状（不弹新窗、不改 UX）——
    ///   · 神经座自己回合：<see cref="TakeTurn"/> → loop.RunUntilTurnEnd 全停靠逐步问 policy；
    ///   · 对手回合（人类/脚本座各自驱动）内神经座的响应窗：宿主泵经
    ///     ResponseWindowService.AiAsyncResponder 问本类（内联决策=训练 D2d 的游戏侧等价；
    ///     非神经座回落 GameActions.DecideAiResponse 现状链）；
    ///   · Script 座行为零改动（SimpleAI/TakeTurnAsync 原样，验证=同种子脚本局逐位一致）。
    /// rstate 局内连续传递：新对局由宿主调 ResetEpisode()（镜像训练 env 复位口径）。
    /// </summary>
    public sealed class NeuralAI
    {
        private readonly IBattlePolicy _policy; // S2：策略经接口消费（OnnxTidePolicy 实现；探针可装饰记日志）
        private BattleDecisionLoop _loop;   // 每局重建（换 GameCore 时；同局镜像神经局仅换决策座）
        private GameCore _core;
        private Player _seat;

        public NeuralAI(IBattlePolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        /// <summary>新对局：GRU rstate 归零 + loop 丢弃（首个 TakeTurn 懒重建；宿主在
        /// InitGame/StartNewGame 后调用——NeuralAiDriver.EnsureReady / AiBattleDriver 开局口）。</summary>
        public void ResetEpisode()
        {
            _policy.Reset();
            _loop = null;
            _core = null;
            _seat = null;
        }

        /// <summary>驱动当前回合玩家的整个回合（逐步：loop 只停神经座自己的决策点）。
        /// 调用约定镜像 SimpleAI.TakeTurn——回合收口后相位停在 End 相（不折返，翻回合归宿主：
        /// AiBattleDriver 外层 CheckPhaseTransition / 活体局引擎帧泵）。</summary>
        public void TakeTurn(BattleController ctrl)
        {
            var core = ctrl.Core;
            var me = ctrl.TurnPlayer;
            if (core == null || me == null || me != core.TurnEngine.TurnPlayer) return;
            EnsureLoop(ctrl, core, me);
            _loop.RunUntilTurnEnd(_policy);
        }

        /// <summary>loop/响应接线（换局重建；同局镜像神经局仅换决策座；同局同座幂等）：
        /// 注册 ResponseWindowService.AiAsyncResponder——**神经座**的响应窗（对手回合内）由
        /// loop.DecideResponseInline 内联问 policy（训练 D2d 等价）；其余座回落现状同步链
        /// （GameActions.DecideAiResponse：AiResponder=PickFor → DefaultAiRespond 守卫启发）。
        /// 人类座的弹窗（HumanResponder）在泵里先于 AI 口——本注册不触及（口径：不改 UX）。
        /// 解绑归宿主清理位（AiBattleDriver finally / BattleController.Shutdown）。</summary>
        private void EnsureLoop(BattleController ctrl, GameCore core, Player me)
        {
            if (!ReferenceEquals(_core, core))
            {
                _core = core;
                _loop = new BattleDecisionLoop();
                _loop.BeginGameObjectMode(core, ctrl.Board, me, ctrl);
                _seat = me;
                ResponseWindowService.AiAsyncResponder = OnEngineAsyncResponse;
            }
            else if (!ReferenceEquals(_seat, me))
            {
                // 同局镜像神经局（AiBattleDriver 双 Neural 共用一实例）：轮流驱动两座——换决策座
                //（停靠态在 RunUntilTurnEnd 入口 AdvanceToDecision 归位，无残留；rstate 单链贯穿
                // 两座=训练自对弈同口径）
                _seat = me;
                _loop.SetDecisionSeat(me);
            }
        }

        /// <summary>AiAsyncResponder 实现（镜像训练 driver 的 OnEngineAsyncResponse 分流，但不停靠
        /// TCS——游戏侧无外部 stepper，停靠即决策）：神经座→内联模型决策；其余→现状同步链（零漂移）。</summary>
        private Cysharp.Threading.Tasks.UniTask<ResponseOption> OnEngineAsyncResponse(
            GameCore core, Player holder, List<ResponseOption> options)
        {
            if (_loop != null && ReferenceEquals(holder, _seat) && ReferenceEquals(core, _core))
                return Cysharp.Threading.Tasks.UniTask.FromResult(
                    _loop.DecideResponseInline(holder, options, _policy));
            return Cysharp.Threading.Tasks.UniTask.FromResult(
                GameActions.DecideAiResponse(core, holder, options));
        }
    }
}
