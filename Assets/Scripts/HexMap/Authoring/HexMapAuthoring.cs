using System.Collections.Generic;
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
    /// 全部参数来自 HexMapFeatureSettings 资产（地形与特征的唯一配置处）。
    /// </summary>
    public static class HexMapConfigBuilder
    {
        // 与旧版 HexMetrics 常量一致
        public const float OuterRadius = 10f;
        public const float InnerRadius = OuterRadius * 0.866025404f;
        public const float SolidFactor = 0.8f;
        public const float ElevationStep = 3f;

        public static BlobAssetReference<HexMapConfigBlob> Build(HexMapFeatureSettings s)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<HexMapConfigBlob>();

                root.BlobSanity = math.asfloat(0x5EEDB10Bu);

                root.CellCount = new int2(s.cellCountX, s.cellCountZ);

                root.OuterRadius = OuterRadius;
                root.InnerRadius = InnerRadius;
                root.SolidFactor = SolidFactor;
                root.BlendFactor = 1f - SolidFactor;
                root.ElevationStep = ElevationStep;
                root.NoiseScales = new float4(
                    math.max(0.0001f, s.noiseScales.x), math.max(0.0001f, s.noiseScales.y),
                    math.max(0.0001f, s.noiseScales.z), math.max(0.0001f, s.noiseScales.w));
                // 扰动范围钳制：cell 缩放 0.5~1.5（防止六边形翻转），高度缩放 0.5~1.5（防止相邻 cell 高度交叉）
                root.CellPerturbRange = new float2(
                    math.clamp(s.cellPerturbRange.x, 0.5f, 1.5f),
                    math.clamp(s.cellPerturbRange.y, 0.5f, 1.5f));
                root.ElevationPerturbRange = new float2(
                    math.clamp(s.elevationPerturbRange.x, 0.5f, 1.5f),
                    math.clamp(s.elevationPerturbRange.y, 0.5f, 1.5f));

                // ---- 网格重做参数（防呆钳制；≤0 的距离项回退默认）----
                var mesh = s.meshSettings;
                float defaultInset = InnerRadius * (1f - SolidFactor);
                root.SlopeInset = mesh.slopeInset > 0f
                    ? math.clamp(mesh.slopeInset, 0.01f, InnerRadius * 0.5f)
                    : defaultInset;
                root.SlopeSubdivisions = math.clamp(mesh.slopeSubdivisions, 1, 8);
                root.RimNormalBlend = math.saturate(mesh.rimNormalBlend);
                root.VariationEnabled = mesh.variationEnabled ? 1 : 0;
                float sx = math.clamp(Mathf.Min(mesh.variationScaleRange.x, mesh.variationScaleRange.y), 0.5f, 2f);
                float sy = math.clamp(Mathf.Max(mesh.variationScaleRange.x, mesh.variationScaleRange.y), 0.5f, 2f);
                root.VariationScaleRange = new float2(sx, sy);
                root.VariationFadeWidth = mesh.variationFadeWidth > 0f
                    ? math.clamp(mesh.variationFadeWidth, 0.01f, InnerRadius)
                    : InnerRadius * 0.45f;
                root.VariationSeed = mesh.variationSeed;

                root.MaxElevation = s.maxElevation;
                root.MountainStrength = math.saturate(s.mountainStrength);
                root.CurlWarpStrength = math.max(0f, s.curlWarpStrength);

                // 地形分带：按上限升序匹配首个命中；容量截断（FixedList128Bytes ≤ 15 带）
                root.TerrainBands.Clear();
                if (s.terrainBands != null)
                {
                    var bands = new List<HexTerrainBand>(s.terrainBands);
                    bands.Sort((a, b) => a.maxElevation.CompareTo(b.maxElevation));
                    foreach (var b in bands)
                    {
                        if (b.terrainIndex < 0 || root.TerrainBands.Length >= 15)
                            continue;
                        root.TerrainBands.Add(b);
                    }
                }

                // 噪声图像素（四张独立；缺图 → 尺寸 0，采样返回中性值 0.5）
                // 注意：BlobBuilder 是 struct，必须 ref 传递——按值传副本会丢失分块账本，
                // 分配出的数组偏移无效 → 采样时 BlobArray 解引用 NRE
                BakeNoise(ref builder, s.heightNoise,
                    ref root.HeightNoiseSize, ref root.HeightNoisePixels, "heightNoise(Perlin)");
                BakeNoise(ref builder, s.mountainNoise,
                    ref root.MountainNoiseSize, ref root.MountainNoisePixels, "mountainNoise(Ridged)");
                BakeNoise(ref builder, s.detailNoise,
                    ref root.DetailNoiseSize, ref root.DetailNoisePixels, "detailNoise(Worley)");
                BakeNoise(ref builder, s.curlNoise,
                    ref root.CurlNoiseSize, ref root.CurlNoisePixels, "curlNoise(Curl)");

                // 采样窗口：与旧版 HexGrid.ApplyNoiseSampling 一致
                float range = math.clamp(s.noiseSampleRange, 0.05f, 1f);
                root.NoiseSampleRange = range;
                float span = 1f - range;
                var rng = new System.Random(s.noiseSeed);
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

        /// <summary>烘焙一张噪声图进 blob（RGBA 像素全保留；缺图 → 尺寸 0 + 警告）。
        /// builder 必须 ref 传（struct，按值传副本会丢失分块账本 → 数组偏移无效）。</summary>
        private static void BakeNoise(ref BlobBuilder builder, Texture2D tex,
            ref int2 size, ref BlobArray<float4> pixels, string what)
        {
            if (tex != null)
            {
                var raw = tex.GetPixels();
                size = new int2(tex.width, tex.height);
                var dst = builder.Allocate(ref pixels, raw.Length);
                for (int i = 0; i < raw.Length; i++)
                    dst[i] = new float4(raw[i].r, raw[i].g, raw[i].b, raw[i].a);
            }
            else
            {
                size = int2.zero;
                builder.Allocate(ref pixels, 0);
                Debug.LogWarning($"[HexMap] {what} 未赋值，该路采样退化为中性值 0.5");
            }
        }
    }

    /// <summary>
    /// 地图作者组件（替代旧版 HexGrid）。
    /// 全部引用与参数在 HexMapFeatureSettings 资产上，本组件只持有资产引用。
    /// 直接放在场景 GameObject 上即可：OnEnable 时手动构建 HexMapConfig 单例（无需 SubScene）。
    /// 放入 SubScene 时则由 HexMapAuthoringBaker 烘焙，两者共用同一构建逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public class HexMapAuthoring : MonoBehaviour
    {
        [Tooltip("HexMap 唯一配置资产（地形参数/引用 + 特征配置），右键 Create>HexMap>Feature Settings")]
        public HexMapFeatureSettings featureSettings;

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
            if (featureSettings == null)
            {
                Debug.LogError("[HexMap] featureSettings 资产未赋值，HexMapAuthoring 无法安装配置");
                return;
            }
            var s = featureSettings;
            var em = world.EntityManager;

            // 运行时 POI 播种（覆盖式）：settings.pois 降级为初始种子，
            // 之后游戏内放置/删除只改 HexPoiRuntime（存档读写也只认运行时表）
            HexPoiRuntime.ResetFrom(s);

            // 清理旧配置（支持重复安装）
            using (var oldConfig = em.CreateEntityQuery(typeof(HexMapConfig)))
            {
                em.DestroyEntity(oldConfig);
            }

            var blob = HexMapConfigBuilder.Build(s);

            var configEntity = em.CreateEntity(typeof(HexMapConfig));
#if ENABLE_HEX_DEBUG_LABEL
            em.AddComponentData(configEntity, new EntityDebugLabel { Group = "HexMapConfig" });
#endif
            em.SetComponentData(configEntity, new HexMapConfig
            {
                Blob = blob,
                TerrainMaterial = s.terrainMaterial,
                LoadRadius = s.loadRadius,
                UnloadRadius = math.max(s.unloadRadius, s.loadRadius + 1f),
                MaxCellCreationsPerFrame = math.max(1, s.maxCellCreationsPerFrame),
                MaxMeshBuildsPerFrame = math.max(1, s.maxMeshBuildsPerFrame),
            });

            Debug.Log($"[HexMap] 配置已安装: CellCount=({s.cellCountX},{s.cellCountZ}), " +
                      $"InnerRadius={blob.Value.InnerRadius}, OuterRadius={blob.Value.OuterRadius}, " +
                      $"LoadRadius={s.loadRadius}");
            Debug.Log("[HexMap] 噪声blob（尺寸×像素数，像素数=0 表示该路未接线）: " +
                      $"H {blob.Value.HeightNoiseSize.x}×{blob.Value.HeightNoisePixels.Length}, " +
                      $"M {blob.Value.MountainNoiseSize.x}×{blob.Value.MountainNoisePixels.Length}, " +
                      $"D {blob.Value.DetailNoiseSize.x}×{blob.Value.DetailNoisePixels.Length}, " +
                      $"C {blob.Value.CurlNoiseSize.x}×{blob.Value.CurlNoisePixels.Length}");

            // 特征单例（托管）：河流/道路/植被生成配置与运行状态。
            // 重复安装时先销毁旧单例（含重生成请求残留）
            using (var oldFeature = em.CreateEntityQuery(typeof(HexFeatureConfig), typeof(HexFeatureState)))
            {
                em.DestroyEntity(oldFeature);
            }
            if (s.enableFeatures)
            {
                var featureEntity = em.CreateEntity(typeof(HexFeatureConfig), typeof(HexFeatureState));
#if ENABLE_HEX_DEBUG_LABEL
                em.AddComponentData(featureEntity, new EntityDebugLabel { Group = "HexFeature" });
#endif
                em.SetComponentData(featureEntity, new HexFeatureConfig
                {
                    Settings = s,
                    WaterMaterial = s.waterMaterial,
                    RoadMaterial = s.roadMaterial,
                });
                em.SetComponentData(featureEntity, new HexFeatureState());
            }

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
            HexMapRuntime.CellCount = new int2(s.cellCountX, s.cellCountZ);
            HexMapRuntime.InnerRadius = HexMapConfigBuilder.InnerRadius;
            HexMapRuntime.OuterRadius = HexMapConfigBuilder.OuterRadius;

            // 贴图平铺尺寸全局常量（HexTerrain shader 读取 _ChunkWorldSize 做世界空间 UV）。
            // 双保险：SubScene/Baker 路径由 HexMapShaderGlobalsSystem 兜底写入。
            float tileWorldX = HexMapConfigBuilder.InnerRadius * 2f;
            float tileWorldZ = HexMapConfigBuilder.OuterRadius * 1.5f;
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
            var s = authoring.featureSettings;
            if (s == null)
            {
                Debug.LogWarning("[HexMap] HexMapAuthoring.featureSettings 未赋值，烘焙跳过");
                return;
            }
            DependsOn(s);
            DependsOn(s.heightNoise);
            DependsOn(s.mountainNoise);
            DependsOn(s.detailNoise);
            DependsOn(s.curlNoise);
            DependsOn(s.terrainMaterial);

            var blob = HexMapConfigBuilder.Build(s);

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new HexMapConfig
            {
                Blob = blob,
                TerrainMaterial = s.terrainMaterial,
                LoadRadius = s.loadRadius,
                UnloadRadius = math.max(s.unloadRadius, s.loadRadius + 1f),
                MaxCellCreationsPerFrame = math.max(1, s.maxCellCreationsPerFrame),
                MaxMeshBuildsPerFrame = math.max(1, s.maxMeshBuildsPerFrame),
            });

            // 特征单例（HexFeatureConfig/State 是托管组件，Baker 的泛型 AddComponent
            // 只接受非托管类型——SubScene 路径不装特征单例，特征系统只在 Install 路径生效。
            // 本项目 HexMapAuthoring 挂在场景 GameObject 上走 Install，SubScene 仅备用）
            if (s.enableFeatures)
            {
                Debug.LogWarning("[HexMap] SubScene/Baker 路径未安装特征单例（托管组件），" +
                                 "请用场景 GameObject 的 Install 路径启用特征生成");
            }
        }
    }
}
