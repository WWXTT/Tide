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
    /// 地图边界判定（Burst 安全）。地形生成、网格生成、编辑三处共用同一判定，
    /// 任何一处不一致都会让「边界恒为 0 高度」的前提失效，进而在侧面出现错层裂缝。
    /// </summary>
    public static class HexBoundary
    {
        /// <summary>
        /// 该 cell 是否位于地图最外一圈（offset 坐标系）。
        /// 外圈 cell 恒定 elevation 0、不接受编辑，使统一侧面可以是竖直长方形。
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
        /// 地图俯视裁剪矩形 (xMin, zMin, xMax, zMax)。
        ///
        /// 取「所有行都被完整覆盖」的最大矩形，而不是六边形外轮廓的包围盒：
        /// 奇数行整体右移 InnerRadius，行间又有 V 形缺口，包围盒会包含没有几何的空隙。
        /// 顶面裁剪、底面、侧面三者共用同一矩形，边界才能严格重合。
        /// </summary>
        public static float4 GetMapRect(float outerRadius, float innerRadius, int2 cellCount)
        {
            float xMin = 0f;
            float xMax = (2 * cellCount.x - 1) * innerRadius;
            float zMin = -0.5f * outerRadius;
            float zMax = (cellCount.y - 1) * 1.5f * outerRadius + 0.5f * outerRadius;
            return new float4(xMin, zMin, xMax, zMax);
        }

        /// <summary>把顶点的 x/z 夹到裁剪矩形内（y 不变）。超出矩形的角点被拉到边界线上。</summary>
        public static float3 ClampToRect(float3 p, float4 rect)
        {
            p.x = math.clamp(p.x, rect.x, rect.z);
            p.z = math.clamp(p.z, rect.y, rect.w);
            return p;
        }
    }
}
