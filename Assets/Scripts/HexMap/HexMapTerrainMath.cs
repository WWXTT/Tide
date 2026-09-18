using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 地形数学共享件：噪声→高程的统一公式 + hex 距离。
    /// HexTerrainGenerationSystem（Job）与特征/存档/重生成路径（主线程）必须共用同一公式，
    /// 否则重生成结果与初次生成漂移。
    /// </summary>
    public static class HexMapTerrainMath
    {
        /// <summary>
        /// 噪声采样 → 高程与世界 y（与旧 TerrainGenerationJob 完全同公式）：
        /// elevation = round(noise.w × MaxElevation) 钳 0..Max；y = ElevationToY（含扰动）。
        /// 边界圈恒 0（初始状态偏好，运行期可自由编辑）。
        /// </summary>
        public static int ElevationFromNoise(ref HexMapConfigBlob blob, float3 position, bool isBoundary, out float y)
        {
            if (isBoundary)
            {
                y = 0f;
                return 0;
            }

            var noise = HexMetrics.SampleNoise(ref blob, position);
            int elevation = (int)math.round(noise.w * blob.MaxElevation);
            elevation = math.clamp(elevation, 0, blob.MaxElevation);
            y = HexMetrics.ElevationToY(ref blob, elevation, noise.y);
            return elevation;
        }

        /// <summary>
        /// 两个 offset 坐标的 hex 网格距离（轴坐标立方距离 (|dx|+|dy|+|dz|)/2）。
        /// 用于泉眼间距、回路防护、寻路启发。
        /// </summary>
        public static int HexDistance(int2 a, int2 b)
        {
            // offset → axial（与 HexCoordinates.FromOffsetCoordinates 同式）
            int ax = a.x - a.y / 2, az = a.y;
            int bx = b.x - b.y / 2, bz = b.y;
            int ay = -ax - az, by = -bx - bz;
            return (math.abs(ax - bx) + math.abs(ay - by) + math.abs(az - bz)) / 2;
        }
    }
}
