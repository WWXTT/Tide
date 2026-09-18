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
        /// <summary>形状扰动：六边形半径的随机缩放范围（1 = 不扰动）</summary>
        public float2 CellPerturbRange;
        public float NoiseScale;
        public float NoiseSampleRange;
        public float2 NoiseSampleOrigin;
        /// <summary>高度扰动：台阶落差的随机缩放范围（1 = 不扰动）</summary>
        public float2 ElevationPerturbRange;

        // ---- 地形生成 ----
        public int MaxElevation;

        // ---- 地形网格重做（板/坡/桥/角闭合）----
        /// <summary>坡带宽度 d（世界单位）：高 cell 板从共享边内缩的距离，坡占高 cell 面积</summary>
        public float SlopeInset;
        /// <summary>坡/桥沿边横向细分数（EdgeVertices v1..v5 = 4 段）</summary>
        public int SlopeSubdivisions;
        /// <summary>rim 法线融合系数 0..1（0=硬边回退，1=全融合）。烘焙进顶点，材质改不动</summary>
        public float RimNormalBlend;
        /// <summary>六边形单元变异开关（逐格 (θ,s,ox,oy) 烘焙，shader 边界/远距淡回纯平铺）</summary>
        public int VariationEnabled;
        /// <summary>逐格缩放范围</summary>
        public float2 VariationScaleRange;
        /// <summary>板内环距（变异权重从内环 1 渐到边环 0 的环宽）</summary>
        public float VariationFadeWidth;
        /// <summary>变异哈希种子</summary>
        public uint VariationSeed;

        // ---- 噪声图 ----
        public int2 NoiseSize;
        public BlobArray<float4> NoisePixels;
    }
}
