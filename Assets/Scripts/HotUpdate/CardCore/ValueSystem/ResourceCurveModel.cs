using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 曲线形状分类（纯描述性，非卡组身份）：
    /// 快慢轴上只有两个锚形状，Balanced 是 50/50 混合的几何结果。
    /// </summary>
    public enum CurveShape
    {
        /// <summary>衰减（快）</summary>
        Decaying,
        /// <summary>上升（慢）</summary>
        Rising,
        /// <summary>近似平行线（快慢均衡的混合落点）</summary>
        Balanced
    }

    /// <summary>
    /// 资源曲线：CapAt(T) 给出地牌槽上限（场上地牌张数上限，兼作可出卡费用上限）。
    /// 当前 T 为「全局回合数」（双方同步推进：先手首回合 1，此后每回合开始 +1）。
    /// 超出 Horizon 后保持末端值（bank 语义下曲线只管地牌张数/费用门槛，不管 bank 存量——bank 无上限）。
    ///
    /// 此模型是曲线的唯一权威实现：规则引擎（ElementPoolSystem）执行它，
    /// ValueSystem 评估它，P3 模拟器调参它 —— 禁止出现第二个曲线实现（双引擎漂移）。
    /// </summary>
    public class ResourceCurve
    {
        private readonly int[] _caps;

        /// <summary>曲线覆盖的个人回合规数（此后保持末端值）</summary>
        public int Horizon { get; }

        /// <summary>形状分类（描述性）</summary>
        public CurveShape Shape { get; }

        /// <summary>快极权重 0..1（快慢轴落点，D5 软提示数据源）</summary>
        public float FastWeight { get; }

        public ResourceCurve(int[] capsPerTurn, CurveShape shape, float fastWeight)
        {
            _caps = capsPerTurn ?? throw new ArgumentNullException(nameof(capsPerTurn));
            if (_caps.Length == 0) throw new ArgumentException("capsPerTurn 不能为空", nameof(capsPerTurn));
            Horizon = _caps.Length;
            Shape = shape;
            FastWeight = fastWeight;
        }

        /// <summary>第 T 个个人回合的取指示物上限（T 从 1 起；越界取末端值）</summary>
        public int CapAt(int turn)
        {
            if (turn <= 1) return _caps[0];
            if (turn > Horizon) return _caps[Horizon - 1];
            return _caps[turn - 1];
        }

        /// <summary>
        /// 固定地牌槽曲线 [1,2,…,maxCap]，按全局回合数索引：
        /// 先手首回合 1，此后每个回合开始 +1（对手首回合即 2），最大 maxCap（默认 9）。
        /// 当前对局的地牌张数/费用上限即此曲线；变速效果仍经 SetCurve 换曲线（P2 挂点）。
        /// </summary>
        public static ResourceCurve StandardLandCurve(int maxCap = 9)
        {
            maxCap = Math.Max(1, maxCap);
            var caps = new int[maxCap];
            for (int i = 0; i < maxCap; i++) caps[i] = i + 1;
            return new ResourceCurve(caps, CurveShape.Rising, 0.5f);
        }

        /// <summary>调试/软提示用：曲线各回合上限的快照</summary>
        public IReadOnlyList<int> Caps => _caps;

        public override string ToString()
        {
            var head = string.Join(",", _caps.Take(8));
            var tail = Horizon > 8 ? ",…" : "";
            return $"{Shape}(快权重{FastWeight:0.00}) [{head}{tail}]";
        }
    }

    /// <summary>
    /// 曲线编译配置（P3 平衡调参的旋钮，全部可覆盖默认值）。
    /// </summary>
    [Serializable]
    public class DeckCurveCompilerConfig
    {
        /// <summary>轻卡段边界分位（费用 ≤ 此值的卡计入快极质量）</summary>
        public float CheapPercentile = 0.33f;
        /// <summary>重卡段边界分位（费用 ≥ 此值的卡计入慢极质量）</summary>
        public float HeavyPercentile = 0.67f;
        /// <summary>曲线覆盖的个人回合规数</summary>
        public int Horizon = 12;
        /// <summary>快极开局峰值 = 因数 × 轻卡段中位费用（快攻开局可倾泻多张轻卡）</summary>
        public float EarlyPeakFactor = 1.5f;
        /// <summary>慢极后期峰值 = 因数 × 全组 P90 费用</summary>
        public float LatePeakFactor = 1.3f;
        /// <summary>形状分类阈值：首尾段均值斜率超过此值判定为非 Balanced</summary>
        public float ShapeSlopeThreshold = 0.5f;

        public static DeckCurveCompilerConfig Default => new DeckCurveCompilerConfig();
    }

    /// <summary>
    /// 卡组 → 资源曲线 编译器（涌现公式，D2/D8）。
    ///
    /// 快慢只有两个锚形状：
    ///   Decay(T) = earlyPeak → lateSteady（快：前置预算，随后衰减）
    ///   Rise(T)  = lateSteady → latePeak（慢：迟滞收益，随后上升）
    /// 混合权重 wFast 由费用分布的轻/重卡质量占比决定 —— 卡组在快慢轴上的落点即曲线。
    /// 50/50 混合自然插值出近似平行线；不存在独立的「中速」身份。
    /// 颜色不参与推导（D6：颜色只管效果集合，与曲线维度正交）。
    /// </summary>
    public static class DeckCurveCompiler
    {
        /// <summary>从卡组费用分布编译资源曲线</summary>
        public static ResourceCurve Compile(IEnumerable<Card> deck, DeckCurveCompilerConfig config = null)
        {
            config ??= DeckCurveCompilerConfig.Default;

            var costs = ExtractCosts(deck);
            if (costs.Count == 0)
                return BuildFlatFallback(config);

            costs.Sort();

            float totalMass = costs.Sum();
            float cheapBound = Percentile(costs, config.CheapPercentile);
            float heavyBound = Percentile(costs, config.HeavyPercentile);

            // 轻卡质量占比 / 重卡质量占比 → 快慢轴落点
            float frontLoad = 0f, backLoad = 0f;
            var cheapCosts = new List<float>();
            foreach (var c in costs)
            {
                if (c <= cheapBound) { frontLoad += c; cheapCosts.Add(c); }
                else if (c >= heavyBound) backLoad += c;
            }
            if (cheapCosts.Count == 0) cheapCosts.Add(cheapBound > 0 ? cheapBound : 1f);

            float denom = frontLoad + backLoad;
            float wFast = denom > 0f ? frontLoad / denom : 0.5f;
            float wSlow = 1f - wFast;

            // 锚点量级（由卡组统计推导，全部经 config 可调）
            int lateSteady = Math.Max(1, (int)Math.Ceiling(Percentile(costs, 0.50f)));
            int earlyPeak = Math.Max(2, (int)Math.Ceiling(config.EarlyPeakFactor * Percentile(cheapCosts, 0.50f)));
            int latePeak = Math.Max(lateSteady, (int)Math.Ceiling(config.LatePeakFactor * Percentile(costs, 0.90f)));

            var caps = new int[config.Horizon];
            for (int t = 1; t <= config.Horizon; t++)
            {
                float s = Smoothstep((t - 1f) / Math.Max(1f, config.Horizon - 1f));
                float decay = earlyPeak + (lateSteady - earlyPeak) * s;
                float rise = lateSteady + (latePeak - lateSteady) * s;
                caps[t - 1] = Math.Max(1, (int)Math.Round(wFast * decay + wSlow * rise, MidpointRounding.AwayFromZero));
            }

            var shape = Classify(caps, config.ShapeSlopeThreshold);
            return new ResourceCurve(caps, shape, wFast);
        }

        /// <summary>按首尾段均值斜率分类（纯描述）</summary>
        private static CurveShape Classify(int[] caps, float threshold)
        {
            int third = Math.Max(1, caps.Length / 3);
            float head = (float)caps.Take(third).Average();
            float tail = (float)caps.Skip(caps.Length - third).Average();
            float slope = tail - head;
            if (slope < -threshold) return CurveShape.Decaying;
            if (slope > threshold) return CurveShape.Rising;
            return CurveShape.Balanced;
        }

        /// <summary>读取卡组各卡的总费用（费用字典求和；无费用卡计 0）</summary>
        private static List<float> ExtractCosts(IEnumerable<Card> deck)
        {
            var result = new List<float>();
            if (deck == null) return result;
            foreach (var card in deck)
            {
                if (card == null) continue;
                float sum = 0f;
                if (card is IHasCost hasCost && hasCost.Cost != null)
                    foreach (var kvp in hasCost.Cost)
                        sum += kvp.Value;
                result.Add(sum);
            }
            return result;
        }

        private static ResourceCurve BuildFlatFallback(DeckCurveCompilerConfig config)
        {
            var caps = new int[config.Horizon];
            for (int i = 0; i < caps.Length; i++) caps[i] = 1;
            return new ResourceCurve(caps, CurveShape.Balanced, 0.5f);
        }

        /// <summary>分位数（p∈[0,1]，线性取下标；输入须已升序）</summary>
        private static float Percentile(List<float> sortedAsc, float p)
        {
            if (sortedAsc == null || sortedAsc.Count == 0) return 0f;
            p = Math.Clamp(p, 0f, 1f);
            int idx = (int)Math.Floor(p * (sortedAsc.Count - 1));
            return sortedAsc[idx];
        }

        private static float Smoothstep(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }
}
