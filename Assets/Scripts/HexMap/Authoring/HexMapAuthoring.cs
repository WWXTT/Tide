using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 运行时全局地图信息（供 MonoBehaviour 层读取，如 HexMapCamera 的边界钳制）。
    /// 由 HexMapAuthoring.Install() 在进入 Play 时写入。
    /// </summary>
    public static class HexMapRuntime
    {
        public static int2 CellCount;
        public static float InnerRadius;
        public static float OuterRadius;

        public static bool IsValid => CellCount.x > 0 && CellCount.y > 0;
    }

    /// <summary>
    /// HexMapConfigBlob 的构建逻辑，运行时安装与 Baker 烘焙共用。
    /// </summary>
    public static class HexMapConfigBuilder
    {
        // 与旧版 HexMetrics 常量一致
        public const float OuterRadius = 10f;
        public const float InnerRadius = OuterRadius * 0.866025404f;
        public const float SolidFactor = 0.8f;
        public const float ElevationStep = 3f;
        public const int TerracesPerSlope = 2;
        public const float NoiseScale = 0.003f;

        public static BlobAssetReference<HexMapConfigBlob> Build(
            Texture2D noiseSource,
            float noiseSampleRange,
            int noiseSeed,
            int cellCountX,
            int cellCountZ,
            int maxElevation,
            Vector2 cellPerturbRange,
            Vector2 elevationPerturbRange)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<HexMapConfigBlob>();

                root.CellCount = new int2(cellCountX, cellCountZ);

                root.OuterRadius = OuterRadius;
                root.InnerRadius = InnerRadius;
                root.SolidFactor = SolidFactor;
                root.BlendFactor = 1f - SolidFactor;
                root.ElevationStep = ElevationStep;
                root.TerracesPerSlope = TerracesPerSlope;
                root.NoiseScale = NoiseScale;
                // 扰动范围钳制：cell 缩放 0.5~1.5（防止六边形翻转），高度缩放 0.5~1.5（防止相邻 cell 高度交叉）
                root.CellPerturbRange = new float2(
                    math.clamp(cellPerturbRange.x, 0.5f, 1.5f),
                    math.clamp(cellPerturbRange.y, 0.5f, 1.5f));
                root.ElevationPerturbRange = new float2(
                    math.clamp(elevationPerturbRange.x, 0.5f, 1.5f),
                    math.clamp(elevationPerturbRange.y, 0.5f, 1.5f));

                root.MaxElevation = maxElevation;

                // 噪声图像素
                if (noiseSource != null)
                {
                    var pixels = noiseSource.GetPixels();
                    root.NoiseSize = new int2(noiseSource.width, noiseSource.height);
                    var blobPixels = builder.Allocate(ref root.NoisePixels, pixels.Length);
                    for (int i = 0; i < pixels.Length; i++)
                        blobPixels[i] = new float4(pixels[i].r, pixels[i].g, pixels[i].b, pixels[i].a);
                }
                else
                {
                    root.NoiseSize = int2.zero;
                    builder.Allocate(ref root.NoisePixels, 0);
                    Debug.LogWarning("[HexMap] noiseSource 未赋值，地形将为全 0 高度");
                }

                // 采样窗口：与旧版 HexGrid.ApplyNoiseSampling 一致
                float range = math.clamp(noiseSampleRange, 0.05f, 1f);
                root.NoiseSampleRange = range;
                float span = 1f - range;
                var rng = new System.Random(noiseSeed);
                root.NoiseSampleOrigin = new float2(
                    (float)rng.NextDouble() * span,
                    (float)rng.NextDouble() * span);

                return builder.CreateBlobAssetReference<HexMapConfigBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }
    }

    /// <summary>
    /// 地图作者组件（替代旧版 HexGrid）。
    /// 直接放在场景 GameObject 上即可：OnEnable 时手动构建 HexMapConfig 单例（无需 SubScene）。
    /// 放入 SubScene 时则由 HexMapAuthoringBaker 烘焙，两者共用同一构建逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public class HexMapAuthoring : MonoBehaviour
    {
        [Header("Noise Sampling")]
        public Texture2D noiseSource;
        [Range(0.05f, 1f)] public float noiseSampleRange = 1f;
        public int noiseSeed = 0;

        [Header("Map Size (cells)")]
        public int cellCountX = 1;
        public int cellCountZ = 1;

        [Header("Terrain Generation")]
        public int maxElevation = 6;

        [Header("Perturbation (Random Variation)")]
        [Tooltip("形状扰动范围：六边形半径的随机缩放比例（1 = 不扰动，0.8~1.2 推荐）")]
        public Vector2 cellPerturbRange = new Vector2(0.8f, 1.2f);
        [Tooltip("高度扰动范围：台阶落差的随机缩放比例（1 = 不扰动，0.8~1.2 推荐）")]
        public Vector2 elevationPerturbRange = new Vector2(0.8f, 1.2f);

        [Header("Rendering")]
        public Material terrainMaterial;

        [Header("Streaming")]
        [Tooltip("加载半径（cell 单位）。摄像机周围此距离内的 cell 保持加载")]
        public float loadRadius = 15f;
        [Tooltip("卸载半径（cell 单位）。超出此距离的 cell 会被卸载。应大于 loadRadius")]
        public float unloadRadius = 30f;
        [Tooltip("每帧最多创建的 cell 数")]
        public int maxCellCreationsPerFrame = 100;
        [Tooltip("每帧最多重建网格的 cell 数")]
        public int maxMeshBuildsPerFrame = 50;

        [Header("Texture Tiling")]
        [Tooltip("贴图平铺粒度（cell 数）：贴图整图铺满 N×N 个 cell 的世界区域。默认 1 = 每 cell 平铺一张，侧面/连接区与六边形用同一尺度，避免大落差时侧面贴图被拉伸")]
        public int textureTileCells = 1;

        /// <summary>
        /// 构建配置单例。可重复调用（先清理旧单例）。
        /// </summary>
        public void Install()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[HexMap] Default World 尚未创建，HexMapAuthoring.Install 跳过");
                return;
            }
            var em = world.EntityManager;

            // 清理旧配置（支持重复安装）
            using (var oldConfig = em.CreateEntityQuery(typeof(HexMapConfig)))
            {
                em.DestroyEntity(oldConfig);
            }

            var blob = HexMapConfigBuilder.Build(noiseSource, noiseSampleRange, noiseSeed,
                cellCountX, cellCountZ, maxElevation, cellPerturbRange, elevationPerturbRange);

            var configEntity = em.CreateEntity(typeof(HexMapConfig));
#if ENABLE_HEX_DEBUG_LABEL
            em.AddComponentData(configEntity, new EntityDebugLabel { Group = "HexMapConfig" });
#endif
            em.SetComponentData(configEntity, new HexMapConfig
            {
                Blob = blob,
                TerrainMaterial = terrainMaterial,
                LoadRadius = loadRadius,
                UnloadRadius = math.max(unloadRadius, loadRadius + 1f),
                MaxCellCreationsPerFrame = math.max(1, maxCellCreationsPerFrame),
                MaxMeshBuildsPerFrame = math.max(1, maxMeshBuildsPerFrame),
            });

            Debug.Log($"[HexMap] 配置已安装: CellCount=({cellCountX},{cellCountZ}), " +
                      $"InnerRadius={blob.Value.InnerRadius}, OuterRadius={blob.Value.OuterRadius}, " +
                      $"LoadRadius={loadRadius}");

            // 摄像机数据单例（若不存在则创建并初始化）
            using (var camQuery = em.CreateEntityQuery(typeof(HexMapCameraData)))
            {
                if (camQuery.IsEmpty)
                {
                    var camEntity = em.CreateEntity(typeof(HexMapCameraData));
#if ENABLE_HEX_DEBUG_LABEL
                    em.AddComponentData(camEntity, new EntityDebugLabel { Group = "HexMapCamera" });
#endif
                    em.SetComponentData(camEntity, new HexMapCameraData
                    {
                        Position = float3.zero
                    });
                }
            }

            // MonoBehaviour 层静态边界信息（HexMapCamera 使用）
            HexMapRuntime.CellCount = new int2(cellCountX, cellCountZ);
            HexMapRuntime.InnerRadius = HexMapConfigBuilder.InnerRadius;
            HexMapRuntime.OuterRadius = HexMapConfigBuilder.OuterRadius;

            // 贴图平铺尺寸全局常量（HexTerrain shader 读取 _ChunkWorldSize 做世界空间 UV；
            // 旧版按 chunk 尺寸写入，chunk 移除后改为按 N×N cell 的世界区域平铺）
            int tileCells = math.max(1, textureTileCells);
            float tileWorldX = tileCells * HexMapConfigBuilder.InnerRadius * 2f;
            float tileWorldZ = tileCells * HexMapConfigBuilder.OuterRadius * 1.5f;
            Shader.SetGlobalVector("_ChunkWorldSize", new Vector4(tileWorldX, tileWorldZ, 0f, 0f));
        }

        private void OnEnable()
        {
            // 延迟到 World 创建后再安装
            StartCoroutine(InstallWhenReady());
        }

        private System.Collections.IEnumerator InstallWhenReady()
        {
            // 等待 Default World 创建
            while (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                yield return null;
            }

            Install();
        }
    }

    /// <summary>
    /// SubScene 烘焙路径（可选）：与运行时 Install 共用 HexMapConfigBuilder。
    /// </summary>
    public class HexMapAuthoringBaker : Baker<HexMapAuthoring>
    {
        public override void Bake(HexMapAuthoring authoring)
        {
            DependsOn(authoring.noiseSource);
            DependsOn(authoring.terrainMaterial);

            var blob = HexMapConfigBuilder.Build(authoring.noiseSource, authoring.noiseSampleRange,
                authoring.noiseSeed, authoring.cellCountX, authoring.cellCountZ,
                authoring.maxElevation, authoring.cellPerturbRange, authoring.elevationPerturbRange);

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new HexMapConfig
            {
                Blob = blob,
                TerrainMaterial = authoring.terrainMaterial,
                LoadRadius = authoring.loadRadius,
                UnloadRadius = math.max(authoring.unloadRadius, authoring.loadRadius + 1f),
                MaxCellCreationsPerFrame = math.max(1, authoring.maxCellCreationsPerFrame),
                MaxMeshBuildsPerFrame = math.max(1, authoring.maxMeshBuildsPerFrame),
            });
        }
    }
}
