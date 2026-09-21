using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 特征提交共享件（自 HexFeatureGenerationSystem / HexMapFeatureMenus 提取，行为零变化）：
    /// 生成路径与存档加载路径共用同一套「提交 / 网格构建 / 状态重置 / 哈希 / 请求」，
    /// 保证两条路径写出的世界逐位一致。
    /// </summary>
    public static class HexFeatureCommitUtil
    {
        // ── 提交（计划 4.6）：快照比对 → ApplyElevation + 地形改写 + 标签 ──

        /// <summary>把快照写回 ECS：高程覆写 + ElevationOverrides/OverlayApplied 填充
        /// + 河床地形改写 + HexRiverCell/HexLakeCell/HexRoadCell 标签。</summary>
        public static void CommitSnapshot(EntityManager em, NativeHashMap<int2, Entity> lookup,
            HexFeatureSnapshot snap, HexFeatureState state, HexMapFeatureSettings settings,
            ref HexMapConfigBlob blob)
        {
            for (int z = 0; z < snap.CellCount.y; z++)
            {
                for (int x = 0; x < snap.CellCount.x; x++)
                {
                    var o = new int2(x, z);
                    int se = snap.Snap[x, z];
                    int oe = snap.Elev[x, z];
                    bool bed = snap.RiverBed[x, z];
                    bool bank = snap.RiverBank[x, z];
                    bool lake = snap.LakeCell[x, z];
                    bool road = snap.RoadCell[x, z];
                    if (se == oe && !bed && !bank && !lake && !road)
                        continue;

                    if (!lookup.TryGetValue(o, out var e))
                        continue;   // 理论不可达（生成前已确认全图就绪）

                    if (se != oe)
                    {
                        HexMapCellEditUtil.ApplyElevation(em, e, se, ref blob);
                        state.ElevationOverrides[o] = se;
                        state.OverlayApplied.Add(o);
                    }

                    // 河床/河岸地形改写（4.4，默认 -1 关闭）
                    if (settings.rivers.riverBedTerrainIndex >= 0 && (bed || bank))
                    {
                        var cell = em.GetComponentData<HexCellData>(e);
                        if (cell.TerrainIndex != settings.rivers.riverBedTerrainIndex)
                        {
                            cell.TerrainIndex = settings.rivers.riverBedTerrainIndex;
                            em.SetComponentData(e, cell);
                            HexMapCellEditUtil.MarkCellAndNeighborsDirty(em, e);
                        }
                    }

                    if (bed && !em.HasComponent<HexRiverCell>(e))
                        em.AddComponentData(e, new HexRiverCell
                        {
                            RiverId = snap.RiverIdOf[x, z],
                            WaterY = snap.RiverWaterY[x, z],
                            IsBank = 0,
                        });
                    else if (bank && !em.HasComponent<HexRiverCell>(e))
                        em.AddComponentData(e, new HexRiverCell
                        {
                            RiverId = snap.RiverIdOf[x, z],
                            WaterY = snap.RiverWaterY[x, z],
                            IsBank = 1,
                        });

                    if (lake && !em.HasComponent<HexLakeCell>(e))
                        em.AddComponentData(e, new HexLakeCell
                        {
                            LakeId = snap.LakeIdOf[x, z],
                            WaterY = snap.LakeWaterY[x, z],
                        });

                    if (road && !em.HasComponent<HexRoadCell>(e))
                        em.AddComponentData(e, new HexRoadCell { RoadId = snap.RoadIdOf[x, z] });
                }
            }
        }

        // ── 水面 + 道路网格构建（生成与存档加载共用；材质缺省时运行时兜底）──

        public static void BuildFeatureMeshes(World world, EntityManager em, HexFeatureSnapshot snap,
            HexFeatureState state, HexFeatureConfig featureConfig, ref HexMapConfigBlob blob)
        {
            var settings = featureConfig.Settings;

            // 水面网格：材质缺省时运行时兜底（HexWater.shader）
            if (featureConfig.EnableRivers && (state.Rivers.Count > 0 || state.Lakes.Count > 0))
            {
                var egs = world.GetExistingSystemManaged<Unity.Rendering.EntitiesGraphicsSystem>();
                if (egs != null)
                {
                    var waterMat = featureConfig.WaterMaterial ?? CreateDefaultWaterMaterial();
                    if (waterMat != null)
                        HexWaterMeshBuilder.Build(snap, state, ref blob, waterMat, em, egs);
                }
                else
                {
                    Debug.LogWarning("[HexMap] EntitiesGraphicsSystem 不存在，水面网格跳过");
                }
            }

            // 道路缎带网格：Lit 灰兜底（DanbaidongRP 保留 URP Lit 原名）
            if (featureConfig.EnableRoads && state.Roads.Count > 0)
            {
                var egs = world.GetExistingSystemManaged<Unity.Rendering.EntitiesGraphicsSystem>();
                if (egs != null)
                {
                    var roadMat = featureConfig.RoadMaterial ?? CreateDefaultRoadMaterial();
                    if (roadMat != null)
                        HexRoadMeshBuilder.Build(snap, state, ref blob,
                            settings.roads.roadHalfWidth, settings.roads.sampleStep,
                            settings.roads.uvScale, roadMat, em, egs);
                }
                else
                {
                    Debug.LogWarning("[HexMap] EntitiesGraphicsSystem 不存在，道路缎带跳过");
                }
            }
        }

        // ── 重生成清理 ──────────────────────────────────────────────

        /// <summary>
        /// 清空特征运行状态（网格实体/植被/标签/覆写表）；
        /// resetElevation=true 时全部 cell 高程重置回噪声值 + 地块回到分带值（边界圈=0）。
        /// </summary>
        public static void ResetFeatures(EntityManager em, NativeHashMap<int2, Entity> lookup,
            HexFeatureState state, bool resetElevation, ref HexMapConfigBlob blob)
        {
            // 先取托管 MeshReference 里的 Mesh 再销毁实体——否则 UnityEngine.Mesh
            // 无人引用也不会自动回收，反复重生成会累积泄漏
            foreach (var e in state.FeatureMeshEntities)
            {
                if (!em.Exists(e))
                    continue;
                if (em.HasComponent<MeshReference>(e))
                {
                    var mesh = em.GetComponentData<MeshReference>(e).Mesh;
                    if (mesh != null)
                        Object.Destroy(mesh);
                }
                em.DestroyEntity(e);
            }
            state.FeatureMeshEntities.Clear();

            em.DestroyEntity(em.CreateEntityQuery(ComponentType.ReadOnly<HexScatterInstance>()));

            em.RemoveComponent(em.CreateEntityQuery(ComponentType.ReadOnly<HexRiverCell>()),
                ComponentType.ReadWrite<HexRiverCell>());
            em.RemoveComponent(em.CreateEntityQuery(ComponentType.ReadOnly<HexLakeCell>()),
                ComponentType.ReadWrite<HexLakeCell>());
            em.RemoveComponent(em.CreateEntityQuery(ComponentType.ReadOnly<HexRoadCell>()),
                ComponentType.ReadWrite<HexRoadCell>());

            state.ElevationOverrides.Clear();
            state.OverlayApplied.Clear();
            state.Rivers.Clear();
            state.Lakes.Clear();
            state.Roads.Clear();
            state.RiverDist.Clear();
            state.RoadDist.Clear();
            state.VegetationDirty = false;

            if (resetElevation)
            {
                foreach (var kv in lookup)
                {
                    var cell = em.GetComponentData<HexCellData>(kv.Value);
                    int elevation = HexMapTerrainMath.ElevationFromNoise(
                        ref blob, cell.Position, out _);

                    // 地块类型回到分带值（高程不变也要刷——旧存档可能是全 0 层）
                    int terrain = HexMapTerrainMath.TerrainIndexFor(ref blob, elevation);
                    bool terrainChanged = terrain != cell.TerrainIndex;
                    if (terrainChanged)
                    {
                        cell.TerrainIndex = terrain;
                        em.SetComponentData(kv.Value, cell);
                    }

                    if (elevation != cell.Elevation)
                        HexMapCellEditUtil.ApplyElevation(em, kv.Value, elevation, ref blob);
                    else if (terrainChanged)
                        HexMapCellEditUtil.MarkCellAndNeighborsDirty(em, kv.Value);
                }
            }

            Debug.Log($"[HexMap] 特征状态已清空（ResetElevation={resetElevation}），" +
                      "全图就绪后重新生成");
        }

        // ── 重生成请求（收敛三处 HasComponent?Set:Add 重复）──────────

        /// <summary>向 HexMapConfig 实体写重生成请求（消费方：HexFeatureGenerationSystem）</summary>
        public static void RequestRegenerate(World world, bool resetElevation)
        {
            var em = world.EntityManager;
            var configEntity = em.CreateEntityQuery(typeof(HexMapConfig)).GetSingletonEntity();
            if (em.HasComponent<HexFeatureRegenerateRequest>(configEntity))
                em.SetComponentData(configEntity, new HexFeatureRegenerateRequest { ResetElevation = resetElevation });
            else
                em.AddComponentData(configEntity, new HexFeatureRegenerateRequest { ResetElevation = resetElevation });
        }

        // ── 状态哈希（确定性验证口径，计划 1.4；前移自 HexMapFeatureMenus）──

        /// <summary>路径 cell 序列 + 水面 + 高程覆写表拼接哈希（两次生成比对）</summary>
        public static uint HashState(HexFeatureState state)
        {
            unchecked
            {
                uint h = 0x9E3779B9u;
                foreach (var river in state.Rivers)
                {
                    h = h * 31u + (uint)river.RiverId;
                    h = h * 31u + (uint)river.End;
                    for (int i = 0; i < river.Cells.Count; i++)
                    {
                        h = h * 31u + (uint)river.Cells[i].x;
                        h = h * 31u + (uint)river.Cells[i].y;
                        h = h * 31u + (uint)Mathf.RoundToInt(river.WaterY[i] * 10f);
                    }
                }
                foreach (var lake in state.Lakes)
                {
                    h = h * 31u + (uint)lake.Level;
                    h = h * 31u + (uint)lake.Cells.Count;
                }
                foreach (var road in state.Roads)
                {
                    h = h * 31u + (uint)road.RoadId;
                    h = h * 31u + (uint)road.Cells.Count;
                }
                foreach (var kv in state.ElevationOverrides)
                {
                    h = h * 31u + (uint)kv.Key.x;
                    h = h * 31u + (uint)kv.Key.y;
                    h = h * 31u + (uint)kv.Value;
                }
                return h;
            }
        }

        // ── 运行时兜底材质 ──────────────────────────────────────────

        private static Material _defaultWaterMaterial;
        private static Material _defaultRoadMaterial;

        /// <summary>运行时兜底水面材质（Shader.Find；shader 未编译/丢失时返回 null 并警告一次）</summary>
        private static Material CreateDefaultWaterMaterial()
        {
            if (_defaultWaterMaterial != null)
                return _defaultWaterMaterial;

            var shader = Shader.Find("HexMap/Water");
            if (shader == null)
            {
                Debug.LogWarning("[HexMap] 找不到 HexMap/Water shader（尚未导入？），水面跳过");
                return null;
            }
            _defaultWaterMaterial = new Material(shader);
            return _defaultWaterMaterial;
        }

        /// <summary>运行时兜底道路材质：DanbaidongRP Lit（GUID 顶替但保留 URP 原名）灰</summary>
        private static Material CreateDefaultRoadMaterial()
        {
            if (_defaultRoadMaterial != null)
                return _defaultRoadMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[HexMap] 找不到 Universal Render Pipeline/Lit（DanbaidongRP），道路跳过");
                return null;
            }
            _defaultRoadMaterial = new Material(shader);
            _defaultRoadMaterial.SetColor("_BaseColor", new Color32(96, 92, 86, 255));
            _defaultRoadMaterial.SetFloat("_Smoothness", 0.08f);
            _defaultRoadMaterial.SetFloat("_Metallic", 0f);
            return _defaultRoadMaterial;
        }
    }
}
