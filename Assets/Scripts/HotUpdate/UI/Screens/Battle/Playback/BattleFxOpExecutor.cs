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
        private readonly Action<int> _armDrawFlyIn; // 抽卡飞入扇区武装回调（座位→卡堆顶坐标入扇+卡堆脉冲；屏侧注入；null=静默跳过）

        public BattleFxOpExecutor(Func<BattleFieldView> field, Action<int> armDrawFlyIn = null)
        {
            _field = field;
            _armDrawFlyIn = armDrawFlyIn;
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
                    case BattleFxKind.Arrival: // 入场：CastPlayed=消融汇聚（EnqueueArrival）；其余来源=轻入场 Aura（2026-11-13 补位）
                        if (op.Target != null)
                        {
                            if (op.Flag) f.PlaySummonArrivalByRef(op.Target.Rid, op.Seat, op.Paid);
                            else f.EnqueueArrival(op.Target.Rid, op.Seat, op.Paid);
                        }
                        break;
                    case BattleFxKind.Paid: f.PlayCastPayByRef(op.Seat, op.Paid); break;
                    case BattleFxKind.Fatigue: // 疲劳受击（HeroFatigueHitAsync 本就是 ref 口：seat+伤害）
                        if (op.Seat >= 0) f.HeroFatigueHitAsync(op.Seat, op.Amount).Forget();
                        break;
                    case BattleFxKind.CounterFloat: f.PlayCounterFloatByRefAsync(op).Forget(); break;
                    case BattleFxKind.ValueFloat: f.PlayValueFloatByRefAsync(op).Forget(); break; // 值浮字统一入口（2026-11-13 P2：不占视觉门）
                    case BattleFxKind.CounterBurst:
                    case BattleFxKind.KeywordBurst: f.PlayGrantBurstByRefAsync(op).Forget(); break;
                    case BattleFxKind.DeckMark:
                        f.PlayDeckMarkByRefAsync(op.Target != null && op.Target.IsPlayer ? op.Target.Seat : op.Seat, op.Flag).Forget();
                        break;
                    case BattleFxKind.Clash: f.PlayClashByRefAsync(op).Forget(); break;
                    case BattleFxKind.LuckRoll: f.PlayLuckRollByRefAsync(op).Forget(); break;
                    case BattleFxKind.DrawFlyIn: // 抽卡飞入（2026-11-13 阶段1·L1-1）：卡堆脉冲+扇区起点武装
                        //（扇区是屏组件——回调注入；随后帧 View 应用 SetCards 建 新卡即弧线飞入）
                        if (op.Seat >= 0) _armDrawFlyIn?.Invoke(op.Seat);
                        break;
                    case BattleFxKind.LandPulse: f.PlayLandPulseByRef(op); break;       // 放地短闪（L2-2·阶段4）
                    case BattleFxKind.Depart: f.PlayDepartByRefAsync(op).Forget(); break; // 离场微拖尾（L2-2）
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
