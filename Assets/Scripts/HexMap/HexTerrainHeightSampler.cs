using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 主线程 cell 数据源：封 NativeHashMap + EntityManager，供采样器分类边/取邻居。
    /// （Burst 场景未来再泛型化为 ComponentLookup 版；当前调用方全是托管系统。）
    /// </summary>
    public struct HexCellDataSource
    {
        public EntityManager Em;
        public NativeHashMap<int2, Entity> Lookup;

        /// <summary>取 cell（越界 / 未加载 / 地形未生成 → false）</summary>
        public bool TryGetCell(int2 offset, out HexCellData data)
        {
            data = default;
            return Lookup.TryGetValue(offset, out var e) &&
                   Em.HasComponent<HexCellData>(e) &&
                   (data = Em.GetComponentData<HexCellData>(e)).Elevation >= 0;
        }

        /// <summary>取 offset 沿方向 d 的邻居（地图外/未加载 → false）</summary>
        public bool TryGetNeighbor(int2 offset, HexDirection d, out HexCellData data)
            => TryGetCell(HexBoundary.NeighborOffset(offset, d), out data);
    }

    /// <summary>
    /// 世界坐标 → 地表 y 的解析采样器（道路缎带 / 植被落点 / 未来玩法拾取共用）。
    /// 分类逻辑与 HexMeshJob.ClassifyEdge/GetEdgeInset 同一套规则：
    /// 直接调 HexMetrics.GetEdgeInset / GetEdgeNormal，不复制公式。
    /// 精度目标 ±0.05（坡带 ~1.73 宽上可接受；缎带 +0.08 抬升、植被 yOffset 掩盖残余）。
    /// </summary>
    public static class HexTerrainHeightSampler
    {
        /// <summary>
        /// 世界点 → 地表 y 与近似法线。owner = 点所在名义六边形的 cell（内部逆扰动后判定）。
        /// 板内 → 板 y；越过板缘 → 按该边类型（桥/坡/边界）在带内插值；
        /// 角落区两邻边任取溢出最大者（两侧带端点值一致，误差为吸收三角面积级，可忽略）。
        /// </summary>
        public static float WorldHeight(float3 worldPos, in HexMetrics metrics,
            ref HexMapConfigBlob blob, in HexCellDataSource grid, out float3 normal)
        {
            // 扰动逆解：查询点在扰动后空间，名义 cell 归属要在未扰动空间判定
            float2 nominal = Unperturb(worldPos.xz, ref blob);
            var ownerOffset = HexCoordinates.FromPosition(
                new float3(nominal.x, 0f, nominal.y), in metrics).ToOffsetCoordinates();

            if (!grid.TryGetCell(ownerOffset, out var owner))
            {
                // 流式洞/图外兜底：钳到图内最近格，用其板 y（道路/植被都限制在图内，属防御路径）
                ownerOffset = math.clamp(ownerOffset, int2.zero, blob.CellCount - 1);
                if (!grid.TryGetCell(ownerOffset, out owner))
                {
                    normal = new float3(0f, 1f, 0f);
                    return 0f;
                }
            }

            float2 local = nominal - owner.Position.xz;
            float plateY = owner.Position.y;

            // 逐边溢出量：o_d = dot(local, n_d) − inset_d（inset_d 按边类型，同 GetEdgeInset）
            float maxO = 0f;
            int bestD = -1;
            EdgeKind bestKind = EdgeKind.Bridge;
            for (int d = 0; d < 6; d++)
            {
                var dir = (HexDirection)d;
                var kind = ClassifyEdge(ownerOffset, owner, dir, ref blob, grid, out var neighbor);
                float inset = metrics.GetEdgeInset(kind);
                float2 n = metrics.GetEdgeNormal(dir).xz;
                float o = math.dot(local, n) - inset;

                if (o > maxO)
                {
                    maxO = o;
                    bestD = d;
                    bestKind = kind;
                    _ = neighbor; // 带内计算按需重取，避免闭包提升
                }
            }

            if (bestD < 0)
            {
                normal = new float3(0f, 1f, 0f);
                return plateY; // 板内
            }

            var bestDir = (HexDirection)bestD;
            grid.TryGetNeighbor(ownerOffset, bestDir, out var bestNeighbor);

            switch (bestKind)
            {
                case EdgeKind.Bridge:
                {
                    // 等高平桥：两侧板高仅差扰动项，带内线性过渡
                    float width = metrics.InnerRadius - metrics.GetEdgeInset(EdgeKind.Bridge);
                    float t = math.saturate(maxO / math.max(width, 1e-4f));
                    normal = new float3(0f, 1f, 0f);
                    return math.lerp(plateY, bestNeighbor.Position.y, t);
                }
                case EdgeKind.SlopeHigher:
                {
                    // 本格高：坡带占本格面积，从本格板缘 (y=plateY) 斜降到邻居板高
                    float width = metrics.SlopeInset;
                    float t = math.saturate(maxO / math.max(width, 1e-4f));
                    float toY = bestNeighbor.Position.y;
                    float3 e = metrics.GetEdgeNormal(bestDir); // 指向低侧（角点 y=0 → 法线 y=0）
                    normal = HexMetrics.SlopeNormal(e, plateY - toY, width);
                    return math.lerp(plateY, toY, t);
                }
                case EdgeKind.Boundary:
                {
                    // 地图边界坡：板缘直落 bottomY（HexMeshJob.GetBottomY 同式）
                    float width = metrics.SlopeInset;
                    float t = math.saturate(maxO / math.max(width, 1e-4f));
                    float toY = -metrics.ElevationStep;
                    float3 e = new float3(metrics.GetEdgeNormal(bestDir).x, 0f, metrics.GetEdgeNormal(bestDir).z);
                    normal = HexMetrics.SlopeNormal(e, plateY - toY, width);
                    return math.lerp(plateY, toY, t);
                }
                default:
                    // SlopeLower/Streaming：板铺满整格（inset=IR），理论上 maxO≤0 不会选中——防御性板 y
                    normal = new float3(0f, 1f, 0f);
                    return plateY;
            }
        }

        public static float WorldHeight(float3 worldPos, in HexMetrics metrics,
            ref HexMapConfigBlob blob, in HexCellDataSource grid)
            => WorldHeight(worldPos, in metrics, ref blob, in grid, out _);

        /// <summary>
        /// 扰动逆解：mesh 顶点 v' = v + PerturbOffset(v)（v = 名义位置）。
        /// 不动点迭代 2 次 p' = p − Offset(p')；扰动是平滑噪声场的平移，收敛误差 &lt; 0.01。
        /// </summary>
        public static float2 Unperturb(float2 xz, ref HexMapConfigBlob blob)
        {
            float2 p = xz;
            for (int i = 0; i < 2; i++)
                p = xz - PerturbOffset(p, ref blob);
            return p;
        }

        /// <summary>
        /// 名义位置的形状扰动位移（HexMeshJob.Perturb 的 x/z 偏移部分，y 恒 0）。
        /// 必须与 Perturb 逐位一致，否则逆解后板缘判定的归属格漂移。
        /// </summary>
        public static float2 PerturbOffset(float2 xz, ref HexMapConfigBlob blob)
        {
            float3 p = new float3(xz.x, 0f, xz.y);
            float amplitude = HexMetrics.CellPerturbAmplitude(ref blob, p);
            if (amplitude <= 0f)
                return float2.zero;

            float4 sample = HexMetrics.SampleNoise(ref blob, p, HexNoiseKind.Detail);
            return new float2(
                (sample.x * 2f - 1f) * amplitude,
                (sample.z * 2f - 1f) * amplitude);
        }

        /// <summary>
        /// 边分类（与 HexMeshJob.ClassifyEdge 同规则，数据源换成 HexCellDataSource）。
        /// 邻居缺失时：图外 → Boundary；图内未加载 → Streaming。
        /// </summary>
        private static EdgeKind ClassifyEdge(int2 ownerOffset, in HexCellData owner, HexDirection d,
            ref HexMapConfigBlob blob, in HexCellDataSource grid, out HexCellData neighbor)
        {
            if (!grid.TryGetNeighbor(ownerOffset, d, out neighbor))
            {
                var nOff = HexBoundary.NeighborOffset(ownerOffset, d);
                return HexBoundary.IsOutsideMap(nOff, blob.CellCount)
                    ? EdgeKind.Boundary
                    : EdgeKind.Streaming;
            }
            if (neighbor.Elevation == owner.Elevation)
                return EdgeKind.Bridge;
            return neighbor.Elevation > owner.Elevation ? EdgeKind.SlopeLower : EdgeKind.SlopeHigher;
        }
    }
}
