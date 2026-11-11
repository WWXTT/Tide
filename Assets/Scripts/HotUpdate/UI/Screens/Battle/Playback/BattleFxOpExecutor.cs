using System;
using CardCore;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>回放演出执行器（2026-11-11 演出统一 S2·方案 §2.2）：BattleFxOp → BattleFieldView
    /// ref 版入口（PlaybackFx.cs）分发。颜色/锚点/座位全由 op 捕获时携带的数据决定，不查引擎。
    /// 演出异步体自带视觉门配对（BeginVisual/EndVisual）——本口 fire-and-forget 后由播放器
    /// WaitVisualsIdleAsync 等待收束（与 Live 闭包路径同节拍协议）。异常单条吞并告警（回放容错——
    /// 单条演出缺件不得炸整局复盘）。</summary>
    public sealed class BattleFxOpExecutor
    {
        private readonly Func<BattleFieldView> _field;

        public BattleFxOpExecutor(Func<BattleFieldView> field)
        {
            _field = field;
        }

        /// <summary>执行一条演出指令（目标不在视图=对应 ref 版入口静默返回，与 Live 口径一致）。</summary>
        public UniTask ExecuteAsync(BattleFxOp op)
        {
            var f = _field();
            if (op == null || f == null) return UniTask.CompletedTask;
            try
            {
                switch (op.Kind)
                {
                    case BattleFxKind.Damage: f.PlayDamageByRefAsync(op).Forget(); break;
                    case BattleFxKind.Heal: f.PlayHealByRefAsync(op).Forget(); break;
                    case BattleFxKind.Flight: f.PlayFlightByRefAsync(op).Forget(); break;
                    case BattleFxKind.DeathHold: f.HoldForDeathByRef(op.Target != null ? op.Target.Rid : 0); break;
                    case BattleFxKind.Death: f.PresentDeathByRefAsync(op).Forget(); break;
                    case BattleFxKind.Arrival: // 入场粒子（EnqueueArrival 本就是 ref 口：rid+seat+实付）
                        if (op.Target != null) f.EnqueueArrival(op.Target.Rid, op.Seat, op.Paid);
                        break;
                    case BattleFxKind.Paid: f.PlayCastPayByRef(op.Seat, op.Paid); break;
                    case BattleFxKind.Fatigue: // 疲劳受击（HeroFatigueHitAsync 本就是 ref 口：seat+伤害）
                        if (op.Seat >= 0) f.HeroFatigueHitAsync(op.Seat, op.Amount).Forget();
                        break;
                    case BattleFxKind.CounterFloat: f.PlayCounterFloatByRefAsync(op).Forget(); break;
                    case BattleFxKind.CounterBurst:
                    case BattleFxKind.KeywordBurst: f.PlayGrantBurstByRefAsync(op).Forget(); break;
                    case BattleFxKind.DeckMark:
                        f.PlayDeckMarkByRefAsync(op.Target != null && op.Target.IsPlayer ? op.Target.Seat : op.Seat, op.Flag).Forget();
                        break;
                    case BattleFxKind.Clash: f.PlayClashByRefAsync(op).Forget(); break;
                    case BattleFxKind.LuckRoll: f.PlayLuckRollByRefAsync(op).Forget(); break;
                    case BattleFxKind.PhaseAnnounce: // HUD 阶段格（与屏侧 OnPhaseAnnounced 同组字单源式）
                        BattleHudRow.SetPhase(PhaseAnnounceText((PhaseType)op.Reason, op.Seat == 0), op.Seat == 0);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FilmFx] 演出指令执行异常（{op.Kind}，已跳过）：{ex.Message}");
            }
            return UniTask.CompletedTask;
        }

        /// <summary>阶段播报文案（与 BattleScreen.PhaseTileText 同式——回放无引擎，直接按捕获相位组字）。</summary>
        private static string PhaseAnnounceText(PhaseType phase, bool mine)
            => $"【{(mine ? "己方" : "对手")}{BattleView.PhaseZh(phase)}阶段】";
    }
}
