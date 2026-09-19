using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 植被散布系统（计划 6.1/6.2）：
    /// HexFeatureState.VegetationDirty == true（特征生成/重生成/菜单触发）→
    /// 整批销毁旧 HexScatterInstance → 逐 cell 哈希驱动散布（极坐标落点恒在板内，
    /// 0.75×IR 圆盘 ⊂ 最小板内缩六边形）→ ECS 实例化（预算 500/帧）。
    /// 过滤：高程窗 / 坡度代理（6 邻最大高差）/ 距河路 BFS 距离 / 聚簇噪声。
    /// 不做松弛 pass：cell 尺度小，格内哈希抖动 + 聚簇噪声已足够自然（开放问题记录）。
    /// 不挂接高程编辑（手编后植被陈旧——已知项；HexScatterCell tag 已预留局部刷新）。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HexFeatureGenerationSystem))]
    public partial class HexVegetationSpawnSystem : SystemBase
    {
        private const int MaxSpawnsPerFrame = 500;

        private struct PendingInstance
        {
            public float3 Pos;
            public quaternion Rot;
            public float Scale;
            public int2 Cell;
            public int RuleIndex;
        }

        private EntityQuery _featureQuery;
        private HexChunkStreamingSystem _streaming;
        private readonly List<PendingInstance> _pending = new();
        private readonly Dictionary<(Mesh mesh, Material material), MaterialMeshInfo> _registered = new();
        private EntityArchetype _instanceArchetype;
        private bool _archetypeReady;

        protected override void OnCreate()
        {
            _featureQuery = GetEntityQuery(typeof(HexFeatureConfig), typeof(HexFeatureState));
            RequireForUpdate(_featureQuery);
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var state = em.GetComponentData<HexFeatureState>(_featureQuery.GetSingletonEntity());
            var featureConfig = em.GetComponentData<HexFeatureConfig>(_featureQuery.GetSingletonEntity());

            bool starting = _pending.Count == 0;
            if (starting)
            {
                if (!state.VegetationDirty || !featureConfig.EnableVegetation)
                    return;
                CollectPendingInstances(state, featureConfig);
                if (_pending.Count > 0)
                {
                    // 整批重建：销毁旧实例（<2k 实体，无性能顾虑）
                    em.DestroyEntity(GetEntityQuery(ComponentType.ReadOnly<HexScatterInstance>()));
                }
                else
                {
                    state.VegetationDirty = false;   // 无规则/无落点，直接完成
                    return;
                }
            }

            if (_streaming == null)
            {
                _streaming = World.GetExistingSystemManaged<HexChunkStreamingSystem>();
                if (_streaming == null)
                    return;
            }

            var settings = featureConfig.Settings;
            int spawned = 0;
            int i = _pending.Count - 1;
            while (i >= 0 && spawned < MaxSpawnsPerFrame)
            {
                var inst = _pending[i];
                var rule = settings.scatterRules[inst.RuleIndex];
                SpawnOne(em, inst, rule);
                _pending.RemoveAt(i);
                spawned++;
                i--;
            }

            if (_pending.Count == 0)
                state.VegetationDirty = false;
        }

        // ── 6.1 散布算法（逐 cell 哈希驱动，确定性） ────────────────

        private void CollectPendingInstances(HexFeatureState state, HexFeatureConfig featureConfig)
        {
            _pending.Clear();
            var settings = featureConfig.Settings;
            if (settings == null || settings.scatterRules == null || settings.scatterRules.Count == 0)
                return;

            var em = EntityManager;
            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;
            ref var blob = ref config.Blob.Value;
            var metrics = HexMetrics.FromBlob(ref blob);
            var lookup = _streaming != null ? _streaming.CellLookup : default;
            if (!lookup.IsCreated)
                return;

            var grid = new HexCellDataSource { Em = em, Lookup = lookup };

            for (int ruleIndex = 0; ruleIndex < settings.scatterRules.Count; ruleIndex++)
            {
                var rule = settings.scatterRules[ruleIndex];
                if (rule == null || rule.mesh == null || rule.material == null)
                    continue;
                var densities = rule.densityPerTerrain;

                foreach (var kv in lookup)
                {
                    var offset = kv.Key;
                    var cell = em.GetComponentData<HexCellData>(kv.Value);
                    if (cell.Elevation < 0)
                        continue;

                    int terrain = cell.TerrainIndex;
                    if (terrain < 0 || terrain >= densities.Count)
                        continue;
                    float lambda = math.max(0f, densities[terrain]);
                    if (lambda <= 0f)
                        continue;

                    // 高程过滤
                    if (cell.Elevation < rule.elevationMin || cell.Elevation > rule.elevationMax)
                        continue;

                    // 坡度代理：与 6 邻居最大高差
                    int maxDiff = 0;
                    for (int d = 0; d < 6; d++)
                    {
                        if (grid.TryGetNeighbor(offset, (HexDirection)d, out var nb))
                            maxDiff = math.max(maxDiff, math.abs(nb.Elevation - cell.Elevation));
                    }
                    if (maxDiff > rule.maxNeighborElevationDiff)
                        continue;

                    // 距河/路 BFS 距离（距离图由特征生成填充；缺键 = 全图无河/路 → 不限制）
                    if (state.RiverDist.Count > 0 &&
                        state.RiverDist.TryGetValue(offset, out int rd) && rd < rule.riverMarginCells)
                        continue;
                    if (state.RoadDist.Count > 0 &&
                        state.RoadDist.TryGetValue(offset, out int od2) && od2 < rule.roadMarginCells)
                        continue;

                    // 聚簇噪声（Worley Detail 图 .x 通道 + 位置预缩放）
                    if (rule.clumpNoiseThreshold > 0f)
                    {
                        float3 samplePos = cell.Position * rule.clumpNoiseScale;
                        if (HexMetrics.SampleNoise(ref blob, samplePos, HexNoiseKind.Detail).x < rule.clumpNoiseThreshold)
                            continue;
                    }

                    // 期望株数：整数部分 + 哈希小数部分
                    int count = (int)math.floor(lambda);
                    uint fracHash = math.hash(new uint3(
                        (uint)(offset.x + 32768), (uint)(offset.y + 32768), settings.featureSeed));
                    var fracRng = new Unity.Mathematics.Random(fracHash == 0u ? 0x6E624EB7u : fracHash);
                    if (fracRng.NextFloat() < lambda - count)
                        count++;

                    // cell 中心（与流式系统同式，注意 y/2 整数除法）
                    float2 center = new float2(
                        (offset.x + offset.y * 0.5f - offset.y / 2) * (metrics.InnerRadius * 2f),
                        offset.y * (metrics.OuterRadius * 1.5f));

                    for (int i = 0; i < count; i++)
                    {
                        uint h = math.hash(new uint3(
                            (uint)(offset.x + 32768),
                            (uint)(offset.y + 32768),
                            math.hash(new uint2((uint)(ruleIndex * 256 + i), settings.featureSeed))));
                        var rng = new Unity.Mathematics.Random(h == 0u ? 0x6E624EB7u : h);

                        // 极坐标落点：0.75×IR 圆盘 ⊂ 板内缩六边形（不落坡带）
                        float rho = math.sqrt(rng.NextFloat()) * 0.75f * metrics.InnerRadius;
                        float theta = rng.NextFloat(math.PI * 2f);
                        float2 local = new float2(math.cos(theta), math.sin(theta)) * rho;
                        float3 pos = new float3(center.x + local.x, 0f, center.y + local.y);
                        pos.y = HexTerrainHeightSampler.WorldHeight(pos, in metrics, ref blob, grid) + rule.yOffset;

                        _pending.Add(new PendingInstance
                        {
                            Pos = pos,
                            Rot = rule.randomYRotation
                                ? quaternion.RotateY(rng.NextFloat(math.PI * 2f))
                                : quaternion.identity,
                            Scale = rng.NextFloat(rule.scaleRange.x, rule.scaleRange.y),
                            Cell = offset,
                            RuleIndex = ruleIndex,
                        });
                    }
                }
            }
        }

        // ── 6.2 ECS 实例化 ──────────────────────────────────────────

        private void SpawnOne(EntityManager em, in PendingInstance inst, HexScatterRule rule)
        {
            if (!_archetypeReady)
            {
                _instanceArchetype = em.CreateArchetype(
                    typeof(LocalTransform),
                    typeof(HexScatterInstance),
                    typeof(HexScatterCell),
                    typeof(HexScatterPrototype));
                _archetypeReady = true;
            }

            var key = (rule.mesh, rule.material);
            if (!_registered.TryGetValue(key, out var mmi))
            {
                var egs = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                if (egs == null)
                    return;
                mmi = new MaterialMeshInfo(egs.RegisterMaterial(rule.material), egs.RegisterMesh(rule.mesh));
                _registered[key] = mmi;
            }

            var entity = em.CreateEntity(_instanceArchetype);
            em.SetSharedComponentManaged(entity, new HexScatterPrototype { PrototypeIndex = inst.RuleIndex });
            em.SetComponentData(entity, new HexScatterCell { Offset = inst.Cell });
            em.SetComponentData(entity, new LocalTransform
            {
                Position = inst.Pos,
                Rotation = inst.Rot,
                Scale = inst.Scale,
            });

            var desc = new RenderMeshDescription(
                UnityEngine.Rendering.ShadowCastingMode.On, receiveShadows: true);
            RenderMeshUtility.AddComponents(entity, em, desc, mmi);

            // AddComponents 后 LocalToWorld 可能残留零矩阵 → 按 TRS 直写（HexMeshWriteSystem 同坑）
            em.SetComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(inst.Pos, inst.Rot, new float3(inst.Scale)),
            });

            var b = rule.mesh.bounds;
            em.SetComponentData(entity, new RenderBounds
            {
                Value = new AABB
                {
                    Center = new float3(b.center.x, b.center.y, b.center.z) * inst.Scale,
                    Extents = new float3(b.extents.x, b.extents.y, b.extents.z) * inst.Scale,
                },
            });
        }
    }
}
