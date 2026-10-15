using System;

namespace SynergyUI
{
    /// <summary>
    /// 对局操作计时（2026-10-14 定案·仅真人对战）：双方各基础 300 秒，当前优先权持有方掉秒
    /// （栈空主阶段=回合方——持有方由调用方从快照/引擎现值推得）；回合结束该回合玩家 +60 秒；
    /// 一方归零=对方胜利（超时判负）。人机对局不建钟（BattleHudRow 时间格显示 ∞）。
    /// 纯表现层挂钟：不给引擎/无头侧注入墙钟（TideServer verify 确定性不受影响）；
    /// 网络对局现为客户端快照驱动，服务器权威计时待联机复活时迁移。
    /// </summary>
    public sealed class MatchClock
    {
        public const double BaseSeconds = 300;
        public const double TurnEndBonusSeconds = 60;

        private readonly double[] _remaining = { BaseSeconds, BaseSeconds };
        private bool _ended;

        /// <summary>已超时终局（Tick 不再掉秒）。</summary>
        public bool Ended => _ended;

        /// <summary>座位剩余秒（seat 0/1）。</summary>
        public double Remaining(int seat) => seat >= 0 && seat < 2 ? _remaining[seat] : 0;

        /// <summary>推进墙钟：holderSeat（-1=暂无持有方，暂停）。返回超时方座位（-1=未超时）。</summary>
        public int Tick(double deltaSeconds, int holderSeat)
        {
            if (_ended || holderSeat < 0 || holderSeat > 1 || deltaSeconds <= 0) return -1;
            _remaining[holderSeat] -= deltaSeconds;
            if (_remaining[holderSeat] > 0) return -1;
            _remaining[holderSeat] = 0;
            _ended = true;
            return holderSeat;
        }

        /// <summary>回合结束加时：该回合玩家 +60 秒。</summary>
        public void AddTurnEndBonus(int seat)
        {
            if (_ended || seat < 0 || seat > 1) return;
            _remaining[seat] += TurnEndBonusSeconds;
        }

        /// <summary>显示文案 m:ss（归零=0:00）。</summary>
        public static string Format(double seconds)
        {
            int s = Math.Max(0, (int)seconds);
            return s / 60 + ":" + (s % 60).ToString("00");
        }
    }
}
