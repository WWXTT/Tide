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
        /// 噪声采样 → 高程与世界 y（四张噪声图分工）：
        /// 1. Curl 流场域扭曲：采样位置沿流场偏移（x/z 用换轴采样去相关）
        /// 2. Perlin 主高度 + Ridged 山脉加权叠加：h = p + strength·r·(1−p)（谷地保留、峰不削顶）
        /// 3. elevation = round(saturate(h) × MaxElevation) 钳 0..Max
        /// 4. y = ElevationToY（高度扰动取 Detail 图 .y）
        /// 边界圈恒 0（初始状态偏好，运行期可自由编辑）。
        /// </summary>
        public static int ElevationFromNoise(ref HexMapConfigBlob blob, float3 position, bool isBoundary, out float y)
        {
            if (isBoundary)
            {
                y = 0f;
                return 0;
            }

            // Curl 流场域扭曲
            float3 p = position;
            if (blob.CurlWarpStrength > 0f && blob.CurlNoiseSize.x > 0)
            {
                float cx = HexMetrics.SampleNoise(ref blob, position, HexNoiseKind.Curl).x;
                float cz = HexMetrics.SampleNoise(ref blob,
                    new float3(position.z, position.y, position.x), HexNoiseKind.Curl).x;
                float2 warp = (new float2(cx, cz) - 0.5f) * blob.CurlWarpStrength;
                p += new float3(warp.x, 0f, warp.y);
            }

            float perlin = HexMetrics.SampleNoise(ref blob, p, HexNoiseKind.Height).x;
            float ridged = HexMetrics.SampleNoise(ref blob, p, HexNoiseKind.Mountain).x;
            float h = math.saturate(perlin + blob.MountainStrength * ridged * (1f - perlin));
            int elevation = (int)math.round(h * blob.MaxElevation);
            elevation = math.clamp(elevation, 0, blob.MaxElevation);

            float noiseY = HexMetrics.SampleNoise(ref blob, position, HexNoiseKind.Detail).y;
            y = HexMetrics.ElevationToY(ref blob, elevation, noiseY);
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
