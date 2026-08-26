using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 单个六边形 cell 的数据（对应旧版 HexCell 的数据部分）。
    /// Position 为地图空间坐标（含高程 × 台阶高度 + 高程扰动后的 y）。
    /// </summary>
    public struct HexCellData : IComponentData
    {
        /// <summary>轴向坐标</summary>
        public HexCoordinates Coordinates;

        /// <summary>地图空间位置（y 已含高程扰动）</summary>
        public float3 Position;

        /// <summary>高度等级（0 = 水平面）</summary>
        public int Elevation;

        /// <summary>地形类型索引（splat 索引，写入顶点 UV1）</summary>
        public int TerrainIndex;
    }

    /// <summary>
    /// 地图边界几何（Burst 安全）。底面矩形与边界连接区落点共用同一套计算，
    /// 任何一处不一致都会让裙边与底面之间出现缝隙。
    /// </summary>
    public static class HexBoundary
    {
        /// <summary>
        /// 该 cell 是否位于地图最外一圈（offset 坐标系）。
        /// 外圈 cell 恒定 elevation 0、不接受编辑：相邻边界的连接区在共享角点两侧
        /// 必须等高，高度不一致时两条裙边会在角点处错层开缝。
        /// </summary>
        public static bool IsBoundary(int2 offsetCoords, int2 cellCount)
        {
            return offsetCoords.x <= 0 || offsetCoords.x >= cellCount.x - 1 ||
                   offsetCoords.y <= 0 || offsetCoords.y >= cellCount.y - 1;
        }

        public static bool IsBoundary(in HexCellData cell, int2 cellCount)
        {
            return IsBoundary(cell.Coordinates.ToOffsetCoordinates(), cellCount);
        }

        /// <summary>
        /// 地图俯视矩形 (xMin, zMin, xMax, zMax)：整个六边形交错排列区域的包围盒。
        ///
        /// 底面与边界连接区共用该矩形：底面铺满整个区域（含奇偶行错位与行间 V 形缺口），
        /// 边界 cell 的连接区直接下探到矩形的边缘上，两者严格闭合。
        /// </summary>
        public static float4 GetMapRect(float outerRadius, float innerRadius, int2 cellCount)
        {
            float xMin = -innerRadius; // 偶数行最左 cell 的西向角
            float xMax = 2f * cellCount.x * innerRadius; // 奇数行最右 cell 的东向角
            float zMin = -outerRadius; // 第 0 行的南向角
            float zMax = (cellCount.y - 1) * 1.5f * outerRadius + outerRadius; // 末行的北向角
            return new float4(xMin, zMin, xMax, zMax);
        }

        /// <summary>
        /// 邻居的 offset 坐标（含奇偶行偏移，与 HexChunkStreamingSystem.LinkNeighbors 一致）。
        /// 用于区分「邻居在地图外（永久缺失）」和「邻居尚未流式加载（暂时缺失）」。
        /// </summary>
        public static int2 NeighborOffset(int2 offset, HexDirection direction)
        {
            int odd = offset.y & 1;
            switch (direction)
            {
                case HexDirection.W:  return new int2(offset.x - 1, offset.y);
                case HexDirection.E:  return new int2(offset.x + 1, offset.y);
                case HexDirection.SE: return new int2(offset.x + odd, offset.y - 1);
                case HexDirection.SW: return new int2(offset.x - 1 + odd, offset.y - 1);
                case HexDirection.NE: return new int2(offset.x + odd, offset.y + 1);
                case HexDirection.NW: return new int2(offset.x - 1 + odd, offset.y + 1);
                default: return offset;
            }
        }

        /// <summary>邻居坐标是否在地图外（该方向永远不会有 cell，属于地图边界）</summary>
        public static bool IsOutsideMap(int2 neighborOffset, int2 cellCount)
        {
            return neighborOffset.x < 0 || neighborOffset.x >= cellCount.x ||
                   neighborOffset.y < 0 || neighborOffset.y >= cellCount.y;
        }

        /// <summary>
        /// 把一点沿「矩形中心 → 该点」的射线投射到矩形边界上（y 不变）。
        ///
        /// 必须是「位置的纯函数」：相邻边界 cell 在共享角点上要算出完全相同的落点，
        /// 各自的连接区才能逐点重合、裙边不开缝。从（一定在矩形内的）中心出发求交，
        /// 点被扰动推出矩形外时依然能得到边界上的落点。
        /// </summary>
        public static float3 ProjectToRectEdge(float3 p, float4 rect)
        {
            float2 o = new float2((rect.x + rect.z) * 0.5f, (rect.y + rect.w) * 0.5f);
            float2 d = new float2(p.x, p.z) - o;

            // 射线最先撞到的前向边界（slab 法，中心严格在矩形内，距离恒为正）
            float t = float.MaxValue;
            if (d.x > 1e-6f) t = math.min(t, (rect.z - o.x) / d.x);
            else if (d.x < -1e-6f) t = math.min(t, (rect.x - o.x) / d.x);
            if (d.y > 1e-6f) t = math.min(t, (rect.w - o.y) / d.y);
            else if (d.y < -1e-6f) t = math.min(t, (rect.y - o.y) / d.y);

            if (t == float.MaxValue || t <= 0f)
                return p; // 点即地图中心（边界角点不可能出现），保守原样返回

            return new float3(o.x + d.x * t, p.y, o.y + d.y * t);
        }
    }
}
