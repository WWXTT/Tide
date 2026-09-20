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
    /// 垂直侧壁版：板铺满整格、格间高差走竖直陡壁，地表 y = 所属格板高、法线恒 +Y，
    /// 无需边分类与带内插值；名义归属经逆扰动判定，与 HexMeshJob.Perturb 同一扰动场。
    /// </summary>
    public static class HexTerrainHeightSampler
    {
        /// <summary>
        /// 世界点 → 地表 y 与近似法线。owner = 点所在名义六边形的 cell（内部逆扰动后判定）；
        /// 板内 → 板 y；越过板缘即进入邻格名义域 → 邻格板 y（壁是竖直过渡面，零宽度）。
        /// </summary>
        public static float WorldHeight(float3 worldPos, in HexMetrics metrics,
            ref HexMapConfigBlob blob, in HexCellDataSource grid, out float3 normal)
        {
            normal = new float3(0f, 1f, 0f);

            // 扰动逆解：查询点在扰动后空间，名义 cell 归属要在未扰动空间判定
            float2 nominal = Unperturb(worldPos.xz, ref blob);
            var ownerOffset = HexCoordinates.FromPosition(
                new float3(nominal.x, 0f, nominal.y), in metrics).ToOffsetCoordinates();

            if (!grid.TryGetCell(ownerOffset, out var owner))
            {
                // 流式洞/图外兜底：钳到图内最近格，用其板 y（道路/植被都限制在图内，属防御路径）
                ownerOffset = math.clamp(ownerOffset, int2.zero, blob.CellCount - 1);
                if (!grid.TryGetCell(ownerOffset, out owner))
                    return 0f;
            }

            return owner.Position.y;
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
    }
}
