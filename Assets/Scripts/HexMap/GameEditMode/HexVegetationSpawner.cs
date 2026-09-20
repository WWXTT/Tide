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
    /// 植被落子器（自 HexVegetationSpawnSystem.SpawnOne 提取，行为零变化）：
    /// archetype + (Mesh,Material)→MaterialMeshInfo 注册缓存 + 单实例创建全套
    /// （LocalTransform/HexScatterCell/HexScatterPrototype/RenderMeshUtility/
    /// LocalToWorld 直写/RenderBounds）。散布系统与游戏内植被笔刷共用，
    /// 保证手动实例与散布实例逐位同构（重放不跳变）。
    ///
    /// 缓存按 World 分桶：MaterialMeshInfo 的 BatchID 与 archetype 都是 World 级资源，
    /// 域重载关闭时旧 World 条目在访问时剪枝。
    /// </summary>
    public static class HexVegetationSpawner
    {
        private static readonly Dictionary<World, Dictionary<(Mesh mesh, Material material), MaterialMeshInfo>>
            _registered = new();

        private static readonly Dictionary<World, EntityArchetype> _archetypes = new();

        /// <summary>创建一个植被实例（散布/手动/覆写重放同一入口）</summary>
        public static void SpawnInstance(World world, EntityManager em, in float3 pos, in quaternion rot,
            float scale, int2 cell, int ruleIndex, HexScatterRule rule)
        {
            var archetype = GetArchetype(world, em);
            if (world.GetExistingSystemManaged<EntitiesGraphicsSystem>() == null)
                return;   // 无渲染桥：跳过（原 SpawnOne 同行为）
            var mmi = GetMaterialMeshInfo(world, rule);

            var entity = em.CreateEntity(archetype);
            em.SetSharedComponentManaged(entity, new HexScatterPrototype { PrototypeIndex = ruleIndex });
            em.SetComponentData(entity, new HexScatterCell { Offset = cell });
            em.SetComponentData(entity, new LocalTransform { Position = pos, Rotation = rot, Scale = scale });

            var desc = new RenderMeshDescription(
                UnityEngine.Rendering.ShadowCastingMode.On, receiveShadows: true);
            RenderMeshUtility.AddComponents(entity, em, desc, mmi);

            // AddComponents 后 LocalToWorld 可能残留零矩阵 → 按 TRS 直写（HexMeshWriteSystem 同坑）
            em.SetComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(pos, rot, new float3(scale)),
            });

            var b = rule.mesh.bounds;
            em.SetComponentData(entity, new RenderBounds
            {
                Value = new AABB
                {
                    Center = new float3(b.center.x, b.center.y, b.center.z) * scale,
                    Extents = new float3(b.extents.x, b.extents.y, b.extents.z) * scale,
                },
            });
        }

        /// <summary>
        /// 植被笔刷手动放一格：count 株哈希极坐标落点（与散布 CollectPendingInstances 同式
        /// ——同格同原型生成的位置确定性一致 → 重放后与笔刷当时所见逐位相同）。
        /// 返回实际放置数（规则 mesh/material 缺失 = 0）。
        /// </summary>
        public static int SpawnCellManual(World world, EntityManager em, int2 offset, int ruleIndex,
            HexScatterRule rule, in HexMetrics metrics, ref HexMapConfigBlob blob,
            HexCellDataSource grid, uint featureSeed, int count)
        {
            if (rule == null || rule.mesh == null || rule.material == null)
                return 0;

            // cell 中心（与流式系统同式，注意 y/2 整数除法）
            float2 center = new float2(
                (offset.x + offset.y * 0.5f - offset.y / 2) * (metrics.InnerRadius * 2f),
                offset.y * (metrics.OuterRadius * 1.5f));

            for (int i = 0; i < count; i++)
            {
                uint h = math.hash(new uint3(
                    (uint)(offset.x + 32768),
                    (uint)(offset.y + 32768),
                    math.hash(new uint2((uint)(ruleIndex * 256 + i), featureSeed))));
                var rng = new Unity.Mathematics.Random(h == 0u ? 0x6E624EB7u : h);

                // 极坐标落点：0.75×IR 圆盘 ⊂ 板内缩六边形（不落坡带）
                float rho = math.sqrt(rng.NextFloat()) * 0.75f * metrics.InnerRadius;
                float theta = rng.NextFloat(math.PI * 2f);
                float2 local = new float2(math.cos(theta), math.sin(theta)) * rho;
                float3 pos = new float3(center.x + local.x, 0f, center.y + local.y);
                pos.y = HexTerrainHeightSampler.WorldHeight(pos, in metrics, ref blob, grid) + rule.yOffset;

                SpawnInstance(world, em, pos,
                    rule.randomYRotation ? quaternion.RotateY(rng.NextFloat(math.PI * 2f)) : quaternion.identity,
                    rng.NextFloat(rule.scaleRange.x, rule.scaleRange.y),
                    offset, ruleIndex, rule);
            }
            return count;
        }

        /// <summary>
        /// 重散布完成后重放手动覆写：一次遍历把 HexScatterCell 实例按格分桶 →
        /// 逐覆写格销毁（Clear/Manual 都销毁散布结果）→ Manual 按记录重放。
        /// 手动植被因此活过 R 重生成 / V 重散布 / 存档加载。
        /// </summary>
        public static void ApplyManualOverrides(World world, EntityManager em,
            HexMapFeatureSettings settings, in HexMetrics metrics, ref HexMapConfigBlob blob,
            NativeHashMap<int2, Entity> lookup)
        {
            if (HexManualVegetationState.Overrides.Count == 0)
                return;

            // 分桶：offset → 该格实例列表
            var byCell = new Dictionary<int2, List<Entity>>();
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<HexScatterCell>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                var c = em.GetComponentData<HexScatterCell>(e);
                if (!byCell.TryGetValue(c.Offset, out var list))
                    byCell[c.Offset] = list = new List<Entity>();
                list.Add(e);
            }

            var grid = new HexCellDataSource { Em = em, Lookup = lookup };
            bool warnedRange = false;
            foreach (var kv in HexManualVegetationState.Overrides)
            {
                // 覆写格：先销散布结果（Clear 到此为止）
                if (byCell.TryGetValue(kv.Key, out var list))
                    foreach (var e in list)
                        if (em.Exists(e))
                            em.DestroyEntity(e);

                if (kv.Value.Mode != HexVegOverrideMode.Manual)
                    continue;

                int ri = kv.Value.PrototypeIndex;
                if (settings == null || settings.scatterRules == null ||
                    ri < 0 || ri >= settings.scatterRules.Count)
                {
                    if (!warnedRange)
                    {
                        Debug.LogWarning($"[HexMap] 手动植被原型越界（{ri}），跳过重放——规则表变更过？");
                        warnedRange = true;
                    }
                    continue;
                }

                SpawnCellManual(world, em, kv.Key, ri, settings.scatterRules[ri],
                    in metrics, ref blob, grid, settings.featureSeed, kv.Value.Count);
            }
        }

        // ── World 级缓存（访问时剪枝死 World）───────────────────

        private static EntityArchetype GetArchetype(World world, EntityManager em)
        {
            PruneDeadWorlds();
            if (!_archetypes.TryGetValue(world, out var archetype))
            {
                archetype = em.CreateArchetype(
                    typeof(LocalTransform),
                    typeof(HexScatterInstance),
                    typeof(HexScatterCell),
                    typeof(HexScatterPrototype));
                _archetypes[world] = archetype;
            }
            return archetype;
        }

        private static MaterialMeshInfo GetMaterialMeshInfo(World world, HexScatterRule rule)
        {
            PruneDeadWorlds();
            if (!_registered.TryGetValue(world, out var cache))
                _registered[world] = cache = new Dictionary<(Mesh, Material), MaterialMeshInfo>();

            var key = (rule.mesh, rule.material);
            if (!cache.TryGetValue(key, out var mmi))
            {
                var egs = world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                if (egs == null)
                    return default;
                mmi = new MaterialMeshInfo(egs.RegisterMaterial(rule.material), egs.RegisterMesh(rule.mesh));
                cache[key] = mmi;
            }
            return mmi;
        }

        private static void PruneDeadWorlds()
        {
            if (_archetypes.Count > 0)
                RemoveDead(_archetypes);
            if (_registered.Count > 0)
                RemoveDead(_registered);
        }

        private static void RemoveDead<TValue>(Dictionary<World, TValue> dict)
        {
            List<World> dead = null;
            foreach (var w in dict.Keys)
            {
                if (w == null || !w.IsCreated)
                    (dead ??= new List<World>()).Add(w);
            }
            if (dead != null)
                foreach (var w in dead)
                    dict.Remove(w);
        }
    }
}
