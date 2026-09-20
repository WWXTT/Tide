using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 一条 hex 边的连接类型（垂直侧壁版）。
    /// 等高：两板直接共边密铺，零额外几何；有高差：高格在共享边发垂直陡壁。
    /// </summary>
    public enum EdgeKind
    {
        /// <summary>等高：相邻板共边密铺（地形边界为硬边）</summary>
        Equal,
        /// <summary>邻居更高：本格无几何，陡壁由邻居生成</summary>
        Higher,
        /// <summary>邻居更低：本格在共享边发垂直陡壁（本格生成）</summary>
        Lower,
        /// <summary>邻居在地图外：陡壁直落 bottomY（与轮廓 Cap 相接）</summary>
        Boundary,
        /// <summary>邻居尚未流式加载：临时陡壁直落 bottomY，邻居加载后按实际类型重建</summary>
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

        // ---- 网格 helpers（板/陡壁）----

        /// <summary>方向 d 的边线单位外法线（指向该方向邻居）= normalize(两角点之和)</summary>
        public float3 GetEdgeNormal(HexDirection direction)
            => math.normalize(Corners[(int)direction] + Corners[(int)direction + 1]);

        /// <summary>
        /// 板角点：方向 d 与 d+1 两条内缩边线的交点（cell 局部坐标，未扰动）。
        /// 边线定义 { p : dot(p, n_k) = l_k }，n_k = GetEdgeNormal(k)，l 为该边内缩距离。
        /// 相邻边法线夹角 60°，det = ±sin60° 恒非零，Cramer 求解稳定。
        /// 垂直版板 = 全宽名义六边形（l = IR），本函数供内环（再内缩 fade）等内缩六边形取角。
        /// 必须是纯函数：相邻 cell 对共享角点各自调用要得到同一结果。
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
        /// 采样仅使用 position 的 x/z；缺图（尺寸 0）或哨兵不匹配返回中性值 0.5
        /// （顶点扰动零位移、高度居中、阈值过滤半通过）。
        ///
        /// ⚠ 绝不可把 BlobArray 拷贝到局部变量再索引：m_OffsetPtr 是「相对字段自身地址」
        /// 的偏移（见 BlobBuilder.CreateBlobAssetReference 的 patch），拷贝后索引 = 栈地址 +
        /// blob 内偏移 = 野指针，Mono 把访问冲突报成 NullReferenceException（09-19 NRE 事故根因）。
        /// 必须通过 ref cfg 原地索引字段。
        /// </summary>
        public static float4 SampleNoise(ref HexMapConfigBlob cfg, float3 position, HexNoiseKind kind)
        {
            // 布局哨兵：失败 = blob 来自旧布局/陈旧烘焙缓存，全部采样降级为中性值
            if (math.asint(cfg.BlobSanity) != 0x5EEDB10B)
                return new float4(0.5f, 0.5f, 0.5f, 0.5f);

            int2 size;
            float scale;
            switch (kind)
            {
                case HexNoiseKind.Mountain:
                    size = cfg.MountainNoiseSize;
                    scale = cfg.NoiseScales.y;
                    break;
                case HexNoiseKind.Detail:
                    size = cfg.DetailNoiseSize;
                    scale = cfg.NoiseScales.z;
                    break;
                case HexNoiseKind.Curl:
                    size = cfg.CurlNoiseSize;
                    scale = cfg.NoiseScales.w;
                    break;
                default:
                    size = cfg.HeightNoiseSize;
                    scale = cfg.NoiseScales.x;
                    break;
            }

            // 缺图（尺寸 0）：不能进索引器——空数组的 m_OffsetPtr 为 0（Allocate 长度≤0 时不
            // 注册 patch），索引会静默读到根结构体字段，返回 0.5 中性值
            if (size.x <= 0 || size.y <= 0)
                return new float4(0.5f, 0.5f, 0.5f, 0.5f);

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
            int index = py * size.x + px;

            // 原地索引（字段经 ref cfg 定位在 blob 内，偏移还原才正确）
            switch (kind)
            {
                case HexNoiseKind.Mountain: return cfg.MountainNoisePixels[index];
                case HexNoiseKind.Detail:   return cfg.DetailNoisePixels[index];
                case HexNoiseKind.Curl:     return cfg.CurlNoisePixels[index];
                default:                    return cfg.HeightNoisePixels[index];
            }
        }
    }
}
