using System;

namespace CardCore
{
    /// <summary>
    /// 速度等级常量
    /// 基础速度由阶段+回合归属决定
    /// 卡牌可通过 BaseSpeed 效果加成或动态支付提速
    /// </summary>
    public static class SpeedLevel
    {
        /// <summary>基础速度 — 非主要阶段 / 非回合持有者</summary>
        public const int None = 0;

        /// <summary>普通速度 — 主要阶段 + 回合持有者</summary>
        public const int Normal = 1;
    }

    /// <summary>
    /// 效果发动类型
    /// 条件发动：基于事件自动触发，不参与速度比较
    /// 速度发动：玩家自主决定，受记速器限制
    /// </summary>
    public enum EffectActivationType
    {
        /// <summary>强制发动 — 必须发动，满足条件自动入栈</summary>
        Mandatory,

        /// <summary>自动发动 — 满足条件自动发动</summary>
        Automatic,

        /// <summary>自由发动 — 玩家选择是否发动，需要轮询</summary>
        Voluntary,
    }

    /// <summary>
    /// 全局速度计数器（记速器）
    /// 场上唯一的记速器，用于控制连锁深度
    /// 每个效果入栈后 +1，每结算一个 -1
    /// 速度发动的速度必须超过记速器才能发动
    /// </summary>
    public class SpeedCounter
    {
        private int _currentSpeed = 0;
        private bool _isResolving = false;
        private int _peakSpeed = 0;

        /// <summary>当前记速器值</summary>
        public int CurrentSpeed => _currentSpeed;

        /// <summary>是否正在结算中</summary>
        public bool IsResolving => _isResolving;

        /// <summary>本轮连锁的最高记速器值</summary>
        public int PeakSpeed => _peakSpeed;

        /// <summary>
        /// 记速器 +1（效果入栈时调用）
        /// </summary>
        public void Increment()
        {
            _currentSpeed++;
            if (_currentSpeed > _peakSpeed)
                _peakSpeed = _currentSpeed;
        }

        /// <summary>
        /// 检查效果是否可以发动
        /// 速度发动：速度必须超过记速器
        /// 条件发动：不参与速度比较（speed=Max 总是通过）
        /// 结算中只允许条件发动
        /// </summary>
        public bool CanActivate(int effectSpeed, EffectActivationType activationType)
        {
            // 结算中不允许速度发动
            if (_isResolving && activationType == EffectActivationType.Voluntary)
                return false;

            // 速度必须超过记速器
            if (effectSpeed <= _currentSpeed)
                return false;

            return true;
        }

        /// <summary>
        /// 开始结算（禁止速度发动入栈）
        /// </summary>
        public void BeginResolution()
        {
            _isResolving = true;
        }

        /// <summary>
        /// 结算完成一个效果，记速器 -1
        /// </summary>
        /// <returns>新的记速器值</returns>
        public int Decrement()
        {
            if (_currentSpeed > 0)
                _currentSpeed--;
            return _currentSpeed;
        }

        /// <summary>
        /// 重置记速器（连锁结算完成后）
        /// </summary>
        public void Reset()
        {
            _currentSpeed = 0;
            _isResolving = false;
            _peakSpeed = 0;
        }

        /// <summary>
        /// 获取状态描述
        /// </summary>
        public string GetStateDescription()
        {
            string state = _isResolving ? "结算中" : "等待中";
            return $"记速器: {_currentSpeed} ({state})";
        }
    }

    /// <summary>
    /// 速度修正器
    /// 用于临时修改效果速度
    /// </summary>
    [Serializable]
    public class SpeedModifier
    {
        /// <summary>固定加成</summary>
        public int FlatBonus = 0;

        /// <summary>是否忽略栈限制（特殊效果）</summary>
        public bool IgnoreStackLimit = false;

        /// <summary>
        /// 应用修正
        /// </summary>
        public int Apply(int baseSpeed)
        {
            return baseSpeed + FlatBonus;
        }
    }

    /// <summary>
    /// 速度提升支付
    /// 定义如何通过支付代价来提升速度
    /// </summary>
    [Serializable]
    public class SpeedBoostPayment
    {
        /// <summary>提升的速度值</summary>
        public int SpeedIncrease;

        /// <summary>最大提升次数（0 = 无限制）</summary>
        public int MaxUses = 0;
    }

    /// <summary>
    /// 速度计算器
    /// 速度三层来源：基础(阶段+回合归属) + 卡牌加成(BaseSpeed) + 动态支付(paidBoost)
    /// </summary>
    public static class SpeedCalculator
    {
        /// <summary>
        /// 协调怪兽提速费率：1速 = 1费（硬编码）
        /// </summary>
        public const int SPEED_COST_RATE = 1;

        /// <summary>
        /// 获取默认速度（由阶段 + 回合归属决定）
        /// 主阶段 + 回合持有者 = 1，其他情况 = 0
        /// </summary>
        public static int GetDefaultSpeed(Player activator, Player turnPlayer, PhaseType phase)
        {
            if (phase != PhaseType.Main) return 0;
            return activator == turnPlayer ? 1 : 0;
        }

        /// <summary>
        /// 计算最终发动速度 = 默认 + 卡牌加成(BaseSpeed) + 动态支付
        /// </summary>
        public static int CalculateSpeed(int defaultSpeed, int baseSpeed, int paidBoost)
        {
            return defaultSpeed + baseSpeed + paidBoost;
        }

        /// <summary>
        /// 计算提速需要支付的额外费用
        /// </summary>
        public static int CalculateSpeedCost(int desiredBoost)
        {
            return desiredBoost * SPEED_COST_RATE;
        }
    }
}
