using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 整张地图的只读配置（Blob 资产）：
    /// 尺寸、HexMetrics 全部常量、噪声图像素、地形类型参考色。
    /// 由 HexMapAuthoring 构建一次，供流式生成 / 地形生成 / 网格 Job 共享读取。
    /// </summary>
    public struct HexMapConfigBlob
    {
        // ---- 尺寸 ----
        /// <summary>地图 offset 坐标系下的 cell 数</summary>
        public int2 CellCount;

        // ---- HexMetrics 常量 ----
        public float OuterRadius;
        public float InnerRadius;
        public float SolidFactor;
        public float BlendFactor;
        public float ElevationStep;
        public int TerracesPerSlope;
        /// <summary>形状扰动：六边形半径的随机缩放范围（1 = 不扰动）</summary>
        public float2 CellPerturbRange;
        public float NoiseScale;
        public float NoiseSampleRange;
        public float2 NoiseSampleOrigin;
        /// <summary>高度扰动：台阶落差的随机缩放范围（1 = 不扰动）</summary>
        public float2 ElevationPerturbRange;

        // ---- 地形生成 ----
        public int MaxElevation;

        // ---- 噪声图 ----
        public int2 NoiseSize;
        public BlobArray<float4> NoisePixels;
    }
}
