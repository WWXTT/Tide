using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 六边形一条边上的 5 个顶点（用于边的细分三角化）。
    /// 与旧版 EdgeVertices 一致：v1/v5 为两端点，v2/v3/v4 为插值点。
    /// </summary>
    public struct EdgeVertices
    {
        public float3 v1, v2, v3, v4, v5;

        public EdgeVertices(float3 corner1, float3 corner2)
        {
            v1 = corner1;
            v2 = math.lerp(corner1, corner2, 0.25f);
            v3 = math.lerp(corner1, corner2, 0.5f);
            v4 = math.lerp(corner1, corner2, 0.75f);
            v5 = corner2;
        }

        /// <summary>
        /// 阶梯插值：通过两条边计算阶梯连接区域每一段的顶点（与旧版 TerraceLerp 一致）
        /// </summary>
        public static EdgeVertices TerraceLerp(in EdgeVertices a, in EdgeVertices b, int step, in HexMetrics metrics)
        {
            EdgeVertices result;
            result.v1 = metrics.TerraceLerp(a.v1, b.v1, step);
            result.v2 = metrics.TerraceLerp(a.v2, b.v2, step);
            result.v3 = metrics.TerraceLerp(a.v3, b.v3, step);
            result.v4 = metrics.TerraceLerp(a.v4, b.v4, step);
            result.v5 = metrics.TerraceLerp(a.v5, b.v5, step);
            return result;
        }
    }
}
