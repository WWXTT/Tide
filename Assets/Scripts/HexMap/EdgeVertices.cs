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
    }
}
