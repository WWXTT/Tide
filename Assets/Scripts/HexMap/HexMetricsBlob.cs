using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 一条 hex 边的连接类型（网格重做：板内缩与连接几何由此分发）。
    /// 归属：桥/坡由「方向 ∈ {NE,E,SE}」一侧生成；角落闭合独立按角落归属（最高格）。
    /// </summary>
    public enum EdgeKind
    {
        /// <summary>等高：平桥（solid 板之间，splat A→B 渐变带）</summary>
        Bridge,
        /// <summary>邻居更高：本格板铺满到共享边，坡带由邻居生成（占邻居面积）</summary>
        SlopeLower,
        /// <summary>邻居更低：本格板内缩 d，坡带占本格面积（本格生成）</summary>
        SlopeHigher,
        /// <summary>邻居在地图外：同 SlopeHigher，坡直落 bottomY 轮廓线（伪邻居 = 外部，高度 bottomY）</summary>
        Boundary,
        /// <summary>邻居尚未流式加载：临时竖墙（Down 到 bottomY），邻居加载后按实际类型重建</summary>
        Streaming,
    }

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
        /// <summary>坡带宽度 d：高 cell 板从共享边内缩的距离（世界单位）</summary>
        public float SlopeInset;
        /// <summary>形状扰动：六边形半径的随机缩放范围（1 = 不扰动）</summary>
        public float2 CellPerturbRange;
        /// <summary>各噪声世界→UV 缩放（x=Height y=Mountain z=Detail w=Curl）</summary>
        public float4 NoiseScales;
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
                SlopeInset = b.SlopeInset,
                CellPerturbRange = b.CellPerturbRange,
                NoiseScales = b.NoiseScales,
                ElevationPerturbRange = b.ElevationPerturbRange,
            };
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

        // ---- 网格重做 helpers（板/坡/角闭合）----

        /// <summary>方向 d 的边线单位外法线（指向该方向邻居）= normalize(两角点之和)</summary>
        public float3 GetEdgeNormal(HexDirection direction)
            => math.normalize(Corners[(int)direction] + Corners[(int)direction + 1]);

        /// <summary>
        /// 板角点：方向 d 与 d+1 两条内缩边线的交点（cell 局部坐标，未扰动）。
        /// 边线定义 { p : dot(p, n_k) = l_k }，n_k = GetEdgeNormal(k)，l 为该边内缩距离。
        /// 相邻边法线夹角 60°，det = ±sin60° 恒非零，Cramer 求解稳定；
        /// 内缩 l ∈ { IR 铺满, IR·sf 桥侧, IR−d 坡侧 } 任意组合都成立 → 板恒为凸六边形。
        /// 必须是纯函数：相邻 cell 对共享角点各自调用要得到同一结果（仅依赖 metrics 与两侧内缩级别）。
        /// </summary>
        public float3 GetPlateCorner(HexDirection direction, float l1, float l2)
        {
            float3 n1 = GetEdgeNormal(direction);
            float3 n2 = GetEdgeNormal(direction.Next());
            // Cramer：x = (l1·b2 − b1·l2)/det，z = (a1·l2 − l1·a2)/det，det = a1·b2 − a2·b1
            // （a=n.x, b=n.z；b1 是 n1.z 不是 n2.x —— 写错会让统一内缩的角点横向拉伸 1/0.866）
            float det = n1.x * n2.z - n2.x * n1.z;
            return new float3(
                (l1 * n2.z - n1.z * l2) / det,
                0f,
                (n1.x * l2 - l1 * n2.x) / det);
        }

        /// <summary>
        /// 坡面解析法线（逐边常量，不随扰动旋转）：n = normalize(Δh·e + d·up)，e = 指向低侧的边法线。
        /// Δh=0 退化为 +Y，d→0 退化为竖直面；与坡面任意切向量正交（含下坡切向 d·e−Δh·up）。
        /// </summary>
        public static float3 SlopeNormal(float3 edgeNormal, float dh, float slopeInset)
            => math.normalize(dh * edgeNormal + new float3(0f, slopeInset, 0f));

        /// <summary>边类型 → 本格板该方向的内缩距离：IR 铺满 / IR·sf 桥侧 / IR−d 坡侧</summary>
        public float GetEdgeInset(EdgeKind kind)
        {
            switch (kind)
            {
                case EdgeKind.Bridge: return InnerRadius * SolidFactor;
                case EdgeKind.SlopeHigher:
                case EdgeKind.Boundary: return InnerRadius - SlopeInset;
                default: return InnerRadius; // SlopeLower / Streaming：铺满到共享边
            }
        }

        /// <summary>
        /// 逐格 UV 变异常量（反平铺）：按 offset 坐标哈希确定性生成（重建稳定）。
        /// 返回 (s·cosθ, s·sinθ, ox, oy)——shader 侧用复数乘应用旋转+缩放、加平移；
        /// 贴图数组 Repeat wrap 下相位偏移自由，是打破「每格同相位铺满整张贴图」的最强手段。
        /// </summary>
        public static float4 CellVariation(int2 offset, uint seed, float2 scaleRange)
        {
            uint h = math.hash(new uint3((uint)(offset.x + 32768), (uint)(offset.y + 32768), seed));
            var rng = new Unity.Mathematics.Random(h == 0u ? 0x6E624EB7u : h);
            float4 r = rng.NextFloat4();
            float theta = r.x * math.PI * 2f;
            float s = math.lerp(scaleRange.x, scaleRange.y, r.y);
            return new float4(s * math.cos(theta), s * math.sin(theta), r.z, r.w);
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
        /// 对噪声图采样（像素存于 config blob，四张图按用途独立）。
        /// 采样仅使用 position 的 x/z；缺图（尺寸 0）返回中性值 0.5
        /// （顶点扰动零位移、高度居中、阈值过滤半通过）。
        /// </summary>
        public static float4 SampleNoise(ref HexMapConfigBlob cfg, float3 position, HexNoiseKind kind)
        {
            int2 size;
            float scale;
            BlobArray<float4> pixels;
            switch (kind)
            {
                case HexNoiseKind.Mountain:
                    size = cfg.MountainNoiseSize;
                    scale = cfg.NoiseScales.y;
                    pixels = cfg.MountainNoisePixels;
                    break;
                case HexNoiseKind.Detail:
                    size = cfg.DetailNoiseSize;
                    scale = cfg.NoiseScales.z;
                    pixels = cfg.DetailNoisePixels;
                    break;
                case HexNoiseKind.Curl:
                    size = cfg.CurlNoiseSize;
                    scale = cfg.NoiseScales.w;
                    pixels = cfg.CurlNoisePixels;
                    break;
                default:
                    size = cfg.HeightNoiseSize;
                    scale = cfg.NoiseScales.x;
                    pixels = cfg.HeightNoisePixels;
                    break;
            }

            // 在 [0,1) 上平铺
            float u = math.fmod(position.x * scale, 1f);
            float v = math.fmod(position.z * scale, 1f);
            if (u < 0f) u += 1f;
            if (v < 0f) v += 1f;

            // 映射到由种子决定的采样窗口（四张图共享同一窗口/种子）
            u = cfg.NoiseSampleOrigin.x + u * cfg.NoiseSampleRange;
            v = cfg.NoiseSampleOrigin.y + v * cfg.NoiseSampleRange;

            int px = math.clamp((int)(u * size.x), 0, size.x - 1);
            int py = math.clamp((int)(v * size.y), 0, size.y - 1);
            return pixels[py * size.x + px];
        }
    }
}
