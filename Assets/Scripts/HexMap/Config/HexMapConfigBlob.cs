using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>噪声图用途（四张独立纹理，由 NoiseMapGenerator 工具生成）</summary>
    public enum HexNoiseKind
    {
        /// <summary>主高度（Perlin）：elevation 基础</summary>
        Height,
        /// <summary>山脉（RidgedFBM）：按 MountainStrength 叠加到高度</summary>
        Mountain,
        /// <summary>侵蚀/岩石细节（Worley）：顶点扰动、高度扰动、植被聚簇。
        /// 建议用 SplitFirst3Octaves 输出（R/G/B=三个八度）让三路去相关</summary>
        Detail,
        /// <summary>流场（Curl）：高度采样域扭曲</summary>
        Curl,
    }

    /// <summary>
    /// 整张地图的只读配置（Blob 资产）：
    /// 尺寸、HexMetrics 全部常量、噪声图像素、地形类型参考色。
    /// 由 HexMapAuthoring 构建一次，供流式生成 / 地形生成 / 网格 Job 共享读取。
    /// </summary>
    public struct HexMapConfigBlob
    {
        // ---- 布局哨兵 ----
        /// <summary>构建时写入魔数（asfloat(0x5EEDB10B)）。读取端校验失败 = blob 来自旧布局/外来路径
        /// （例如陈旧的 SubScene 烘焙缓存），采样降级为中性值 0.5 而非 NRE。</summary>
        public float BlobSanity;

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
        /// <summary>各噪声世界→UV 缩放（x=Height y=Mountain z=Detail w=Curl）</summary>
        public float4 NoiseScales;
        public float NoiseSampleRange;
        public float2 NoiseSampleOrigin;
        /// <summary>高度扰动：台阶落差的随机缩放范围（1 = 不扰动）</summary>
        public float2 ElevationPerturbRange;

        // ---- 地形生成 ----
        public int MaxElevation;
        /// <summary>山脉叠加强度（Ridged 对高度的贡献，0..1）</summary>
        public float MountainStrength;
        /// <summary>Curl 域扭曲强度（世界单位，0=不扭曲）</summary>
        public float CurlWarpStrength;
        /// <summary>
        /// 高程→TerrainIndex 分带（按 maxElevation 升序匹配首个命中；空表 → 全 0 层）。
        /// 生成 Job（Burst）与重置路径共用，见 HexMapTerrainMath.TerrainIndexFor。
        /// FixedList 上限 15 带（8B/带），Build 时截断。
        /// </summary>
        public FixedList128Bytes<HexTerrainBand> TerrainBands;

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

        // ---- 噪声图（四张独立，用途各异；缺图的采样返回中性值 0.5）----
        /// <summary>Height（Perlin）：主高度。xy=尺寸</summary>
        public int2 HeightNoiseSize;
        public BlobArray<float4> HeightNoisePixels;
        /// <summary>Mountain（RidgedFBM）：山脉叠加。xy=尺寸</summary>
        public int2 MountainNoiseSize;
        public BlobArray<float4> MountainNoisePixels;
        /// <summary>Detail（Worley）：侵蚀/岩石细节——顶点扰动、高度扰动、植被聚簇。xy=尺寸</summary>
        public int2 DetailNoiseSize;
        public BlobArray<float4> DetailNoisePixels;
        /// <summary>Curl：流场扭曲（高度采样域扭曲）。xy=尺寸</summary>
        public int2 CurlNoiseSize;
        public BlobArray<float4> CurlNoisePixels;
    }
}
