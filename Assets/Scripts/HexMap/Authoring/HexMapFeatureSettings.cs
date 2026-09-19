using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    public enum PoiType { RiverSpring, RoadNode }

    [Serializable]
    public struct HexPoiData
    {
        public PoiType Type;        // RiverSpring / RoadNode
        public int2 CellOffset;     // 主数据（拾取时写入）
        public float3 WorldPos;     // 辅助显示/半径盘参考
        public float Radius;        // gizmo 半径盘；泉水=水源范围，路点=端点缓冲
        public string Note;
    }

    /// <summary>
    /// 地形网格重做参数（板/坡/桥/角闭合 + 六边形单元变异）。
    /// 变异的取值逐格在 mesh 构建时按坐标哈希烘焙，这里只存范围与开关。
    /// ≤0 的距离项在 Build 内回退默认值，因此旧资产缺省序列化数据也能得到合理配置。
    /// </summary>
    [Serializable]
    public struct HexMeshRewriteSettings
    {
        [Tooltip("坡带宽度 d（世界单位）：高 cell 顶面从共享边内缩的距离，坡占高 cell 面积。≤0 = 默认 InnerRadius×BlendFactor")]
        public float slopeInset;
        [Tooltip("坡/桥沿边横向细分数")]
        [Range(1, 8)] public int slopeSubdivisions;
        [Tooltip("rim 法线融合系数：0=硬边，1=全融合（smoothnormal 风格烘焙，材质改不动）")]
        [Range(0f, 1f)] public float rimNormalBlend;
        [Tooltip("六边形单元变异（逐格 UV 旋转/缩放/偏移，边界与远距淡回纯平铺）")]
        public bool variationEnabled;
        [Tooltip("逐格缩放范围（1=不变）")]
        public Vector2 variationScaleRange;
        [Tooltip("板内环距：变异权重从内环 1 渐到边环 0 的环宽。≤0 = 默认 InnerRadius×0.45")]
        public float variationFadeWidth;
        [Tooltip("变异哈希种子")]
        public uint variationSeed;

        public static HexMeshRewriteSettings Default => new HexMeshRewriteSettings
        {
            slopeInset = 0f,
            slopeSubdivisions = 4,
            rimNormalBlend = 1f,
            variationEnabled = true,
            variationScaleRange = new Vector2(0.85f, 1.25f),
            variationFadeWidth = 0f,
            variationSeed = 0x5EEDu,
        };
    }

    [Serializable]
    public struct HexRiverSettings
    {
        public int springNoiseThresholdElevation; // 泉眼最小高程；0=自动 ceil(MaxElevation*0.7)
        public int springMinSpacing;              // 泉眼最小间距（cell）
        public int maxSprings;                    // 程序泉眼上限
        public int initialCarveAllowance;         // 起步挖掘预算（高程步）
        public int maxCarveAllowance;             // 成熟河挖掘预算
        public float widthGrowPerStep;            // 每步宽度增速
        public float widthTwoCellThreshold;       // ≥此宽度标记 2 格宽
        public float dryUpWidth;                  // < 此宽度干涸
        public int minRiverLength;                // < 此长度丢弃
        public int maxLakeCells;                  // 湖泊面积上限
        public int maxElevationDropPerStep;       // 河床相邻格最大落差
        public int riverBedTerrainIndex;          // 河床地形索引，-1=不改

        public static HexRiverSettings Default => new HexRiverSettings
        {
            springNoiseThresholdElevation = 0,   // 0 = 自动（ceil(MaxElevation*0.7)，读取时归一化）
            springMinSpacing = 4,
            maxSprings = 3,
            initialCarveAllowance = 1,
            maxCarveAllowance = 3,
            widthGrowPerStep = 0.06f,
            widthTwoCellThreshold = 1.6f,
            dryUpWidth = 0.5f,
            minRiverLength = 6,
            maxLakeCells = 40,
            maxElevationDropPerStep = 1,
            riverBedTerrainIndex = -1,
        };
    }

    [Serializable]
    public struct HexRoadSettings
    {
        public int connectionsPerNode;   // 每节点目标连接数
        public int maxConnectionsTotal;  // 单节点连接上限
        public float minConnectionAngle; // 同节点两路最小夹角（度）
        public float maxHeightCost;      // |Δe| 权重
        public float riverCrossCost;     // 穿河代价
        public float boundaryCost;       // 走边界圈代价
        public float roadHalfWidth;      // 缎带半宽；0=自动 0.5*InnerRadius
        public float sampleStep;         // 缎带纵向前进步长
        public float uvScale;            // 纵向 UV 密度（/世界单位）

        public static HexRoadSettings Default => new HexRoadSettings
        {
            connectionsPerNode = 2,
            maxConnectionsTotal = 3,
            minConnectionAngle = 60f,
            maxHeightCost = 2f,
            riverCrossCost = 40f,
            boundaryCost = 5f,
            roadHalfWidth = 0f,          // 0 = 自动（0.5*InnerRadius，读取时归一化）
            sampleStep = 2.0f,
            uvScale = 0.1f,
        };
    }

    [Serializable]
    public class HexScatterRule
    {
        public string name;
        public Mesh mesh;                                 // 原型 mesh（FBX 子资产可直接引用，不用 prefab）
        public Material material;
        public List<float> densityPerTerrain = new();     // 按地形索引的每 cell 期望株数（0/缺项=该地形不放）
        public Vector2 scaleRange = new(0.8f, 1.3f);
        public float yOffset = 0f;
        public bool randomYRotation = true;
        [Tooltip("cell 高程过滤（含）")]
        public int elevationMin = 0, elevationMax = 99;
        [Tooltip("坡度代理：cell 与 6 邻居最大高差上限")]
        public int maxNeighborElevationDiff = 2;
        [Tooltip("距河/路最小 BFS 距离（cell）")]
        public int riverMarginCells = 1, roadMarginCells = 1;
        [Tooltip(">0 时聚簇噪声过滤（0=不过滤）")]
        public float clumpNoiseThreshold = 0f;
        public float clumpNoiseScale = 1f;
    }

    /// <summary>
    /// HexMap 唯一配置资产：地形参数/引用（噪声、尺寸、高程、扰动、网格重做、流式、地形材质）
    /// + 特征配置（河流/道路/植被参数 + POI 列表 + 水路材质）。
    /// 场景里的 HexMapAuthoring 只持有一个本资产引用，全部数值在资产上编辑。
    /// 放 ScriptableObject 而非挂 HexMapAuthoring 字段的原因：POI 在 Play 模式下放置
    /// （只有 Play 中才有 cell 实体可拾取），场景组件在 Play 中的修改退出即还原，
    /// 资产修改可持久。
    ///
    /// TerrainIndex ↔ 贴图数组层序映射（全工程唯一表，扩层时同步更新此注释）：
    /// 0/1=现有两层 | 2=GrassGreen 3=GrassYellow 4=Dirt 5=CliffDark 6=Gravel
    /// 7=Sand 8=SandCracks 9=Snow 10=CliffBright 11=CliffRed/Pink（预留）
    /// </summary>
    [CreateAssetMenu(menuName = "HexMap/Feature Settings", fileName = "HexMapFeatureSettings")]
    public class HexMapFeatureSettings : ScriptableObject
    {
        [Header("噪声图（NoiseMapGenerator 生成 1024×1024 可平铺）")]
        [Tooltip("Height / Perlin：主高度（elevation 基础）")]
        public Texture2D heightNoise;
        [Tooltip("Mountain / RidgedFBM：山脉（按强度加权叠加到高度，谷地保留）")]
        public Texture2D mountainNoise;
        [Tooltip("Detail / Worley：侵蚀岩石细节——顶点扰动、高度扰动、植被聚簇。建议 SplitFirst3Octaves 输出（R/G/B=三个八度）让三路去相关；灰度图时三路同值")]
        public Texture2D detailNoise;
        [Tooltip("Curl：流场扭曲（高度采样域扭曲，x/z 换轴采样去相关）")]
        public Texture2D curlNoise;
        [Tooltip("各噪声世界→UV 缩放（x=高度 y=山脉 z=细节 w=Curl）")]
        public Vector4 noiseScales = new Vector4(0.003f, 0.003f, 0.003f, 0.003f);
        [Range(0.05f, 1f)] public float noiseSampleRange = 1f;
        public int noiseSeed = 1;
        [Range(0f, 1f)]
        [Tooltip("山脉叠加强度：h = p + strength·r·(1−p)（Perlin 谷地保留、Ridged 峰隆起）")]
        public float mountainStrength = 0.5f;
        [Tooltip("Curl 域扭曲强度（世界单位，0=不扭曲）")]
        public float curlWarpStrength = 15f;

        [Header("地图尺寸 (cells)")]
        public int cellCountX = 10;
        public int cellCountZ = 20;

        [Header("地形生成")]
        public int maxElevation = 10;

        [Header("扰动")]
        [Tooltip("形状扰动范围：六边形半径的随机缩放比例（1 = 不扰动）")]
        public Vector2 cellPerturbRange = new Vector2(0.85f, 1.25f);
        [Tooltip("高度扰动范围：台阶落差的随机缩放比例（1 = 不扰动）")]
        public Vector2 elevationPerturbRange = new Vector2(0.9f, 1.1f);

        [Header("网格重做 (板/坡/桥/角)")]
        public HexMeshRewriteSettings meshSettings = HexMeshRewriteSettings.Default;

        [Header("地形渲染")]
        public Material terrainMaterial;

        [Header("流式")]
        [Tooltip("加载半径（cell 单位）。摄像机周围此距离内的 cell 保持加载")]
        public float loadRadius = 20f;
        [Tooltip("卸载半径（cell 单位）。超出此距离的 cell 会被卸载。应大于 loadRadius")]
        public float unloadRadius = 30f;
        [Tooltip("每帧最多创建的 cell 数")]
        public int maxCellCreationsPerFrame = 100;
        [Tooltip("每帧最多重建网格的 cell 数")]
        public int maxMeshBuildsPerFrame = 100;

        [Header("特征 (河流/道路/植被)")]
        [Tooltip("启用世界特征生成系统（河流/湖泊/道路/植被散布）")]
        public bool enableFeatures = true;

        [Header("全局")]
        public uint featureSeed = 0x516E2u;

        [Header("河流")]
        public HexRiverSettings rivers = HexRiverSettings.Default;

        [Header("道路")]
        public HexRoadSettings roads = HexRoadSettings.Default;

        [Header("植被")]
        public List<HexScatterRule> scatterRules = new();

        [Header("POI（放置模式编辑）")]
        public List<HexPoiData> pois = new();

        [Header("材质")]
        public Material waterMaterial;   // 缺省时用 HexWater.mat
        public Material roadMaterial;    // 缺省时用 HexRoad.mat

        private void OnValidate()
        {
            // 地形/流式参数防呆（与 Install 时的钳制口径一致）
            cellCountX = Mathf.Max(1, cellCountX);
            cellCountZ = Mathf.Max(1, cellCountZ);
            maxElevation = Mathf.Max(1, maxElevation);
            loadRadius = Mathf.Max(1f, loadRadius);
            unloadRadius = Mathf.Max(loadRadius + 1f, unloadRadius);
            maxCellCreationsPerFrame = Mathf.Max(1, maxCellCreationsPerFrame);
            maxMeshBuildsPerFrame = Mathf.Max(1, maxMeshBuildsPerFrame);
            noiseScales = new Vector4(
                Mathf.Max(0.0001f, noiseScales.x), Mathf.Max(0.0001f, noiseScales.y),
                Mathf.Max(0.0001f, noiseScales.z), Mathf.Max(0.0001f, noiseScales.w));
            curlWarpStrength = Mathf.Max(0f, curlWarpStrength);

            var r = rivers;
            r.springMinSpacing = Mathf.Max(0, r.springMinSpacing);
            r.maxSprings = Mathf.Max(0, r.maxSprings);
            r.initialCarveAllowance = Mathf.Max(0, r.initialCarveAllowance);
            r.maxCarveAllowance = Mathf.Max(r.initialCarveAllowance, r.maxCarveAllowance);
            r.widthGrowPerStep = Mathf.Max(0f, r.widthGrowPerStep);
            r.widthTwoCellThreshold = Mathf.Max(1f, r.widthTwoCellThreshold);
            r.dryUpWidth = Mathf.Max(0f, r.dryUpWidth);
            r.minRiverLength = Mathf.Max(1, r.minRiverLength);
            r.maxLakeCells = Mathf.Max(1, r.maxLakeCells);
            r.maxElevationDropPerStep = Mathf.Max(1, r.maxElevationDropPerStep);
            r.riverBedTerrainIndex = Mathf.Max(-1, r.riverBedTerrainIndex);
            rivers = r;

            var d = roads;
            d.connectionsPerNode = Mathf.Max(1, d.connectionsPerNode);
            d.maxConnectionsTotal = Mathf.Max(d.connectionsPerNode, d.maxConnectionsTotal);
            d.minConnectionAngle = Mathf.Clamp(d.minConnectionAngle, 0f, 180f);
            d.maxHeightCost = Mathf.Max(0f, d.maxHeightCost);
            d.riverCrossCost = Mathf.Max(0f, d.riverCrossCost);
            d.boundaryCost = Mathf.Max(0f, d.boundaryCost);
            d.roadHalfWidth = Mathf.Max(0f, d.roadHalfWidth);
            d.sampleStep = Mathf.Max(0.25f, d.sampleStep);
            d.uvScale = Mathf.Max(0.001f, d.uvScale);
            roads = d;

            featureSeed = featureSeed == 0 ? 1u : featureSeed;   // 0 是 Random(uint seed) 的非法种子
        }
    }
}
