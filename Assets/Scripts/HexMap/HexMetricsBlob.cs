using Unity.Collections;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 旧版 static HexMetrics 的 Burst 安全移植：
    /// 常量不再是编译期常量，而是从 HexMapConfigBlob 加载到本结构体的字段，
    /// 随 Job 按值传递。角点表使用 FixedList 避免托管数组。
    /// </summary>
    public struct HexMetrics
    {
        public float OuterRadius;
        public float InnerRadius;
        public float SolidFactor;
        public float BlendFactor;
        public float ElevationStep;
        public int TerracesPerSlope;
        public int TerraceSteps;
        public float HorizontalTerraceStepSize;
        public float VerticalTerraceStepSize;
        /// <summary>形状扰动：六边形半径的随机缩放范围（1 = 不扰动）</summary>
        public float2 CellPerturbRange;
        public float NoiseScale;
        /// <summary>高度扰动：台阶落差的随机缩放范围（1 = 不扰动）</summary>
        public float2 ElevationPerturbRange;

        /// <summary>7 个角点（第 7 个重复第一个，供 GetSecondCorner(NW) 使用，防止越界判断）</summary>
        public FixedList128Bytes<float3> Corners;

        public static HexMetrics FromBlob(ref HexMapConfigBlob b)
        {
            var m = new HexMetrics
            {
                OuterRadius = b.OuterRadius,
                InnerRadius = b.InnerRadius,
                SolidFactor = b.SolidFactor,
                BlendFactor = b.BlendFactor,
                ElevationStep = b.ElevationStep,
                TerracesPerSlope = b.TerracesPerSlope,
                CellPerturbRange = b.CellPerturbRange,
                NoiseScale = b.NoiseScale,
                ElevationPerturbRange = b.ElevationPerturbRange,
            };
            m.TerraceSteps = m.TerracesPerSlope * 2 + 1;
            m.HorizontalTerraceStepSize = 1f / m.TerraceSteps;
            m.VerticalTerraceStepSize = 1f / (m.TerracesPerSlope + 1);
            m.Corners.Add(new float3(0f, 0f, m.OuterRadius));
            m.Corners.Add(new float3(m.InnerRadius, 0f, 0.5f * m.OuterRadius));
            m.Corners.Add(new float3(m.InnerRadius, 0f, -0.5f * m.OuterRadius));
            m.Corners.Add(new float3(0f, 0f, -m.OuterRadius));
            m.Corners.Add(new float3(-m.InnerRadius, 0f, -0.5f * m.OuterRadius));
            m.Corners.Add(new float3(-m.InnerRadius, 0f, 0.5f * m.OuterRadius));
            m.Corners.Add(new float3(0f, 0f, m.OuterRadius));
            return m;
        }

        public float3 GetFirstCorner(HexDirection direction) => Corners[(int)direction];

        public float3 GetSecondCorner(HexDirection direction) => Corners[(int)direction + 1];

        public float3 GetFirstSolidCorner(HexDirection direction) => Corners[(int)direction] * SolidFactor;

        public float3 GetSecondSolidCorner(HexDirection direction) => Corners[(int)direction + 1] * SolidFactor;

        /// <summary>
        /// 颜色混合区域（bridge）向量：相邻两角点之和 × blendFactor
        /// </summary>
        public float3 GetBridge(HexDirection direction) => (Corners[(int)direction] + Corners[(int)direction + 1]) * BlendFactor;

        /// <summary>
        /// 阶梯连接区域的顶点插值：水平方向按步长比例、垂直方向仅在奇数步长变化
        /// </summary>
        public float3 TerraceLerp(float3 a, float3 b, int step)
        {
            float h = step * HorizontalTerraceStepSize;
            a.x += (b.x - a.x) * h;
            a.z += (b.z - a.z) * h;
            float v = ((step + 1) / 2) * VerticalTerraceStepSize;
            a.y += (b.y - a.y) * v;
            return a;
        }

        /// <summary>两个相邻 cell 的高度差类型</summary>
        public static HexEdgeType GetEdgeType(int elevation1, int elevation2)
        {
            if (elevation1 == elevation2) return HexEdgeType.Flat;
            int delta = elevation2 - elevation1;
            if (delta == 1 || delta == -1) return HexEdgeType.Slope;
            return HexEdgeType.Cliff;
        }

        /// <summary>
        /// 形状扰动的最大位移（世界单位）。
        /// CellPerturbRange 以「不扰动的六边形半径 = 1」为基准，例如 [0.8, 1.2] 表示
        /// 顶点最多偏离标称位置 0.2 个半径。取 range 两侧偏离 1 的较小值，保证结果对称落在区间内。
        /// </summary>
        public static float CellPerturbAmplitude(ref HexMapConfigBlob cfg)
        {
            float2 r = cfg.CellPerturbRange;
            float ratio = math.max(0f, math.min(r.y - 1f, 1f - r.x));
            return ratio * cfg.OuterRadius;
        }

        /// <summary>
        /// 顶点形状扰动的最大位移（世界单位），随位置到地图边缘的距离衰减：
        /// 地图边缘处恒为 0（边界完全不扰动，等效于该处 CellPerturbRange = (1,1)），
        /// 向内经过 falloff 距离（2×外半径，覆盖整个外圈 cell 的几何）线性恢复到全值。
        ///
        /// 必须按「位置」而非「所属 cell」计算：相邻 cell 的共享顶点（尤其边界与内部
        /// 之间的桥接顶点）在两侧的 Job 里要用同一振幅算出同一结果——
        /// 按 cell 判定会让桥接两侧振幅不同，交界处必然开缝。
        /// </summary>
        public static float CellPerturbAmplitude(ref HexMapConfigBlob cfg, float3 position)
        {
            float amplitude = CellPerturbAmplitude(ref cfg);
            if (amplitude <= 0f)
                return 0f;

            float4 rect = HexBoundary.GetMapRect(cfg.OuterRadius, cfg.InnerRadius, cfg.CellCount);
            float dist = math.min(
                math.min(position.x - rect.x, rect.z - position.x),
                math.min(position.z - rect.y, rect.w - position.z));

            float falloff = 2f * cfg.OuterRadius;
            if (dist >= falloff)
                return amplitude;
            if (dist <= 0f)
                return 0f;
            return amplitude * (dist / falloff);
        }

        /// <summary>
        /// 将整数 elevation 和噪声值映射到世界空间 Y 坐标（含高度扰动）。
        /// noiseSample: SampleNoise 返回的 float4，取 .y 作为高度扰动随机源。
        /// ElevationPerturbRange [min, max] 以台阶高度为单位，如 [0.8, 1.2] 表示
        /// 每层高度在基准 ±20% 范围内波动（取较小侧保证不与相邻层交叉）。
        /// </summary>
        public static float ElevationToY(ref HexMapConfigBlob cfg, int elevation, float noiseY)
        {
            float baseY = elevation * cfg.ElevationStep;
            float2 r = cfg.ElevationPerturbRange;
            float maxPerturbRatio = math.min(r.y - 1f, 1f - r.x);
            float perturbAmount = (noiseY * 2f - 1f) * maxPerturbRatio * cfg.ElevationStep;
            return baseY + perturbAmount;
        }

        /// <summary>
        /// 对彩色噪点图采样（像素存于 config blob）。采样仅使用 x/z，与旧版一致。
        /// </summary>
        public static float4 SampleNoise(ref HexMapConfigBlob cfg, float3 position)
        {
            if (cfg.NoiseSize.x == 0 || cfg.NoiseSize.y == 0)
                return float4.zero;

            // 在 [0,1) 上平铺
            float u = math.fmod(position.x * cfg.NoiseScale, 1f);
            float v = math.fmod(position.z * cfg.NoiseScale, 1f);
            if (u < 0f) u += 1f;
            if (v < 0f) v += 1f;

            // 映射到由种子决定的采样窗口
            u = cfg.NoiseSampleOrigin.x + u * cfg.NoiseSampleRange;
            v = cfg.NoiseSampleOrigin.y + v * cfg.NoiseSampleRange;

            int px = math.clamp((int)(u * cfg.NoiseSize.x), 0, cfg.NoiseSize.x - 1);
            int py = math.clamp((int)(v * cfg.NoiseSize.y), 0, cfg.NoiseSize.y - 1);
            return cfg.NoisePixels[py * cfg.NoiseSize.x + px];
        }
    }
}
