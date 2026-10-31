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
    /// 效果发动类型（2026-10-16 称谓统一定案：效果只分**主动/自动**两种——旧「启动式/激活式/自由发动」
    /// 称谓全部统一为「主动」，承载时机=TriggerTiming.Activate_* 三档（主阶段/瞬间/响应）。
    /// 条件发动（自动）：基于事件自动触发，不参与速度比较（"xx时"时点响应，恒可入栈）；
    /// 速度发动（主动）：玩家自主决定，受记速器门槛约束。
    /// 强制（Mandatory）=系统内部使用：无条件触发，**不开放**（合成器发动方式下拉只有 自动/主动）、
    /// 不动——仅作内部实现细节保留。
    /// </summary>
    public enum EffectActivationType
    {
        /// <summary>强制发动 — 必须发动，满足条件自动入栈（系统内部使用，不开放）</summary>
        Mandatory,

        /// <summary>自动发动 — 满足条件自动发动</summary>
        Automatic,

        /// <summary>主动发动（旧称：启动式/激活式/自由发动）— 玩家选择是否发动，受记速器门槛约束</summary>
        Voluntary,
    }

    /// <summary>
    /// 全局速度计数器（记速器）——记录当前连锁中已发动的最高速度。
    /// 发动门槛（2026-10-18 定案，取代 09-13「回合方 ≥ / 非回合方严格大于」双轨与
    /// 10-17 归属台账两条口径，权威注以本类为准）：
    /// ① 所有**主动（速度）发动**入栈一律要求 速度 **严格大于** 记速器——同速连锁
    ///    无论连锁自己还是对手一律禁止（「自己不能同速连锁自己」由此公理级成立，
    ///    归属台账随之退役）；回合归属退出速度系统（时点/回合合法性由 CheckTiming 承担）。
    /// ② 唯一例外：**空栈首动免门槛**——新连锁的第一个发动不查速度（0 速主阶段
    ///    出牌/主动效果/宣攻靠此豁免；空栈时记速器恒 0，豁免与 Reset 生命周期对齐）。
    /// ③ 条件发动（强制/自动触发）不走速度、恒可入栈、不抬记速器（2026-09-13 定案保持）。
    /// 记速器只在发动**更高**速度时抬升（非无条件 +1）；连锁全部结算完成后 Reset 归 0。
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
        /// 检查效果是否可以发动（2026-10-18 定案：全严格大于 + 空栈首动豁免）：
        /// - 条件发动（强制/自动，"xx时"触发）不走速度，恒通过；
        /// - 速度发动（主动）：速度**严格大于**记速器才可入栈——同速连锁（无论对象归属）
        ///   一律禁止；回合方/非回合方同一裁决（isTurnPlayer 参数随旧双轨口径退役）；
        /// - 例外：stackEmpty=空栈首动（新连锁第一个发动）免门槛；
        /// - 结算中不允许速度发动入栈。
        /// </summary>
        public bool CanActivate(int effectSpeed, EffectActivationType activationType, bool stackEmpty)
        {
            if (activationType != EffectActivationType.Voluntary)
                return true; // 条件发动：不参与速度比较

            if (_isResolving)
                return false; // 结算中不允许速度发动

            if (stackEmpty)
                return true; // 空栈首动免门槛（2026-10-18）

            return effectSpeed > _currentSpeed;
        }

        /// <summary>
        /// 开始结算（禁止速度发动入栈）
        /// </summary>
        public void BeginResolution()
        {
            _isResolving = true;
        }

        /// <summary>
        /// 重置记速器（连锁全部结算完成后）。2026-10-17 晚修订：攻宣压制（峰值跨连锁转移）已移除——
        /// 「守卫后不能立即再攻击」的节奏诉求由表现层承担（ScriptedAi.PacingWait 每击等演出播完），
        /// 速度压制会造成每守卫一次吃掉 AI 一击的交替瘫痪。
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
        /// 2026-10-15 深夜改版：法术效果发动速度=卡的发动速度——免费点=槽数+速度(3−N)
        ///（KindSpeedBonusOf 动态单源）计入本口；生物/结界从手牌使用恒 0 速（结界禁主动后速度通道退役）。
        ///（疾风仪典场上加速 2026-10-31 整链删除。）
        /// </summary>
        public static int GetCardCastSpeed(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null || data.Effects.Count == 0)
                return 0; // 裸卡/无效果卡基速 0
            int max = 0;
            foreach (var effect in data.Effects)
                if (effect != null && effect.BaseSpeed > max)
                    max = effect.BaseSpeed;
            if (data.Supertype == Cardtype.Spell)
                max += CardCompositionCost.KindSpeedBonusOf(data); // 法术提速计入整卡施放速度门
            return max;
        }
    }
}
