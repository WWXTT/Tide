using System;

namespace CardCore
{
    /// <summary>
    /// 速度等级常量（2026-09-13 定案：速度是效果自身属性，废除"阶段×回合归属"的基础速度）
    /// 原子效果速度默认 0；需要在对手回合发动/响应的，组合期自行调高
    /// </summary>
    public static class SpeedLevel
    {
        /// <summary>原子默认速度——游戏王「普通」档：回合玩家主阶段可用，无法在对手回合发动/响应</summary>
        public const int None = 0;

        /// <summary>瞬间基准速度——可在对手回合发动（记速器 0 时 1&gt;0）、可响应 0 速发动</summary>
        public const int Instant = 1;
    }

    /// <summary>
    /// 效果发动类型
    /// 条件发动：基于事件自动触发，不参与速度比较（"xx时"时点响应，恒可入栈）
    /// 速度发动：玩家自主决定，受记速器门槛约束
    /// </summary>
    public enum EffectActivationType
    {
        /// <summary>强制发动 — 必须发动，满足条件自动入栈</summary>
        Mandatory,

        /// <summary>自动发动 — 满足条件自动发动</summary>
        Automatic,

        /// <summary>自由发动 — 玩家选择是否发动，受记速器门槛约束</summary>
        Voluntary,
    }

    /// <summary>
    /// 全局速度计数器（记速器）——记录当前连锁中已发动的最高速度（2026-09-13 定案）。
    /// 发动门槛：回合持有者 速度 ≥ 记速器；非回合持有者 速度 &gt; 记速器（严格大于）。
    /// 记速器不是无条件 +1：只有发动高于当前值的卡才抬升（回合玩家连锁自己的 0 速不抬）。
    /// 连锁全部结算完成后 Reset 归 0。
    /// </summary>
    public class SpeedCounter
    {
        private int _currentSpeed = 0;
        private bool _isResolving = false;
        private int _peakSpeed = 0;

        /// <summary>当前记速器值（本连锁已发动的最高速度）</summary>
        public int CurrentSpeed => _currentSpeed;

        /// <summary>是否正在结算中</summary>
        public bool IsResolving => _isResolving;

        /// <summary>本轮连锁的最高记速器值</summary>
        public int PeakSpeed => _peakSpeed;

        /// <summary>
        /// 抬升记速器：只有发动速度**高于**当前值时才抬到该值，否则保持不变
        /// （"不是无条件 +1"定案——回合玩家连锁自己的 0 速不抬）。
        /// </summary>
        public void RaiseTo(int speed)
        {
            if (speed > _currentSpeed)
            {
                _currentSpeed = speed;
                if (_currentSpeed > _peakSpeed)
                    _peakSpeed = _currentSpeed;
            }
        }

        /// <summary>
        /// 检查效果是否可以发动（2026-09-13 定案口径）：
        /// - 条件发动（强制/自动，"xx时"触发）不走速度，恒通过；
        /// - 速度发动：回合持有者 速度 ≥ 记速器；非回合持有者 速度 &gt; 记速器（严格大于）；
        /// - 结算中不允许速度发动入栈。
        /// 例：对手发动 1 速 → 记速器 1 → 回合方可连锁 ≥1，非回合方只能连锁 ≥2。
        /// </summary>
        public bool CanActivate(int effectSpeed, bool isTurnPlayer, EffectActivationType activationType)
        {
            if (activationType != EffectActivationType.Voluntary)
                return true; // 条件发动：不参与速度比较

            if (_isResolving)
                return false; // 结算中不允许速度发动

            return isTurnPlayer ? effectSpeed >= _currentSpeed : effectSpeed > _currentSpeed;
        }

        /// <summary>
        /// 开始结算（禁止速度发动入栈）
        /// </summary>
        public void BeginResolution()
        {
            _isResolving = true;
        }

        /// <summary>
        /// 重置记速器（连锁全部结算完成后）
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
    /// 速度计算器（2026-09-13 定案：无"基础速度"——"阶段×回合归属"默认速度已废除）
    /// 发动速度 = 组合期声明的 BaseSpeed（原子默认 0）+ 对局内动态支付提速
    /// </summary>
    public static class SpeedCalculator
    {
        /// <summary>
        /// 协调怪兽提速费率：1速 = 1费（硬编码）
        /// </summary>
        public const int SPEED_COST_RATE = 1;

        /// <summary>计算发动速度 = BaseSpeed（组合期声明）+ 动态支付提速</summary>
        public static int CalculateSpeed(int baseSpeed, int paidBoost)
        {
            return baseSpeed + paidBoost;
        }

        /// <summary>
        /// 计算提速需要支付的额外费用
        /// </summary>
        public static int CalculateSpeedCost(int desiredBoost)
        {
            return desiredBoost * SPEED_COST_RATE;
        }

        /// <summary>
        /// 整卡施放速度（2026-09-13 定案）：卡面声明——其效果 BaseSpeed 的最大值。
        /// 缺省 0 = 游戏王「普通」档（仅回合玩家主阶段可打）；瞬间类组合期调 1 以上
        /// 才能在对手回合打出/响应（非回合方门槛为严格大于记速器）。
        /// 裸 Card（衍生物/临时卡，非 CardWrapper）恒为 0。
        /// </summary>
        public static int GetCardCastSpeed(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null || data.Effects.Count == 0) return 0;
            int max = 0;
            foreach (var effect in data.Effects)
                if (effect != null && effect.BaseSpeed > max)
                    max = effect.BaseSpeed;
            return max;
        }
    }
}
