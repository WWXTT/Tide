using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 特征生成编排系统：等全图 cell 就绪（流式 + 地形生成完毕）→ 执行一次整图生成
    /// （快照 → 河流 → [道路,步骤 4] → 提交 → 水面/缎带网格[步骤 3/4] → 植被标记）。
    /// 特征一次性整图生成而非逐 chunk：河流/道路需要全图高程视野，
    /// 当前 loadRadius=15 覆盖全图，进 Play 数帧内全部就绪。
    /// 河流/湖泊雕刻走快照 → 提交时经 HexMapCellEditUtil.ApplyElevation 写回并标脏，
    /// 网格代码零改动（阶梯坡面由现有 SlopeHigher/SlopeLower 几何自动表达）。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HexFeatureOverlaySystem))]
    public partial class HexFeatureGenerationSystem : SystemBase
    {
        private HexChunkStreamingSystem _streaming;
        private EntityQuery _pendingQuery;
        private EntityQuery _featureQuery;
        private bool _generated;
        private double _nextWarnTime;

        protected override void OnCreate()
        {
            RequireForUpdate<HexMapConfig>();
            _featureQuery = GetEntityQuery(typeof(HexFeatureConfig), typeof(HexFeatureState));
            RequireForUpdate(_featureQuery);
            _pendingQuery = GetEntityQuery(ComponentType.ReadOnly<TerrainPending>());
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();

            if (_streaming == null)
            {
                _streaming = World.GetExistingSystemManaged<HexChunkStreamingSystem>();
                if (_streaming == null)
                    return;   // 无流式系统（理论不可达）——请求保留，等下帧
            }

            // 重生成请求消费（菜单/POI 放置工具写入；须在 _streaming 赋值后，ResetFeatures 用到）
            if (em.HasComponent<HexFeatureRegenerateRequest>(configEntity))
            {
                var req = em.GetComponentData<HexFeatureRegenerateRequest>(configEntity);
                em.RemoveComponent<HexFeatureRegenerateRequest>(configEntity);
                ResetFeatures(req.ResetElevation);
                _generated = false;
            }

            if (_generated)
                return;

            var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;
            ref var blob = ref config.Blob.Value;
            int expected = blob.CellCount.x * blob.CellCount.y;

            bool ready = _streaming.CellLookup.Count >= expected && _pendingQuery.IsEmpty;
            if (!ready)
            {
                if (World.Time.ElapsedTime >= _nextWarnTime)
                {
                    _nextWarnTime = World.Time.ElapsedTime + 1.0;
                    Debug.LogWarning(
                        $"[HexMap] 特征生成等待全图就绪（{_streaming.CellLookup.Count}/{expected} cell，" +
                        $"待生成地形 {_pendingQuery.CalculateEntityCount()}）。" +
                        "loadRadius 小于地图时会一直挂起——重生成菜单会调用 EnsureAllLoaded 强制补建");
                }
                return;
            }

            _generated = true;
            GenerateFeatures();
        }

        /// <summary>整图生成：快照 → 河流（→ 道路[步骤 4]）→ 提交 → 验证</summary>
        private void GenerateFeatures()
        {
            var em = EntityManager;
            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;
            ref var blob = ref config.Blob.Value;

            var featureEntity = _featureQuery.GetSingletonEntity();
            var featureConfig = em.GetComponentData<HexFeatureConfig>(featureEntity);
            var state = em.GetComponentData<HexFeatureState>(featureEntity);
            var settings = featureConfig.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[HexMap] featureSettings 资产引用丢失，特征生成跳过");
                return;
            }

            var snapshot = HexFeatureSnapshot.Build(em, _streaming.CellLookup, ref blob);

            if (featureConfig.EnableRivers)
                HexRiverGenerator.Generate(snapshot, settings.rivers, settings.pois, state);

            if (featureConfig.EnableRoads)
                HexRoadGenerator.Generate(snapshot, settings.roads, settings.pois, state);

            CommitSnapshot(snapshot, state, settings);

            // 水面网格（步骤 3）。材质缺省时运行时兜底（HexWater.shader），
            // 正式 HexWater.mat 资产随美术迁移（步骤 0/8.4）后在 Settings 里指定
            if (featureConfig.EnableRivers && (state.Rivers.Count > 0 || state.Lakes.Count > 0))
            {
                var egs = World.GetExistingSystemManaged<Unity.Rendering.EntitiesGraphicsSystem>();
                if (egs != null)
                {
                    var waterMat = featureConfig.WaterMaterial ?? CreateDefaultWaterMaterial();
                    if (waterMat != null)
                        HexWaterMeshBuilder.Build(snapshot, state, ref blob, waterMat, em, egs);
                }
                else
                {
                    Debug.LogWarning("[HexMap] EntitiesGraphicsSystem 不存在，水面网格跳过");
                }
            }

            // 道路缎带网格（步骤 4）。Lit 灰兜底（DanbaidongRP 保留 URP Lit 原名）
            if (featureConfig.EnableRoads && state.Roads.Count > 0)
            {
                var egs = World.GetExistingSystemManaged<Unity.Rendering.EntitiesGraphicsSystem>();
                if (egs != null)
                {
                    var roadMat = featureConfig.RoadMaterial ?? CreateDefaultRoadMaterial();
                    if (roadMat != null)
                        HexRoadMeshBuilder.Build(snapshot, state, ref blob,
                            settings.roads.roadHalfWidth, settings.roads.sampleStep,
                            settings.roads.uvScale, roadMat, em, egs);
                }
                else
                {
                    Debug.LogWarning("[HexMap] EntitiesGraphicsSystem 不存在，道路缎带跳过");
                }
            }

            ValidateResult(snapshot, state);

            state.VegetationDirty = true;   // 植被系统（步骤 5）消费
            state.GenerationSerial++;       // 编辑器确定性验证的完成信号

            Debug.Log("[HexMap] 特征生成完成" +
                      $"：河流 {state.Rivers.Count} 条（总格数 {TotalCells(state.Rivers)}）" +
                      $"、湖泊 {state.Lakes.Count} 个（总格数 {TotalCells(state.Lakes)}）");
        }

        // ── 提交（计划 4.6）：快照比对 → ApplyElevation + 地形改写 + 标签 ──

        private void CommitSnapshot(HexFeatureSnapshot snap, HexFeatureState state,
            HexMapFeatureSettings settings)
        {
            var em = EntityManager;
            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;
            ref var blob = ref config.Blob.Value;
            var lookup = _streaming.CellLookup;

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

        // ── 验证（步骤 2 门禁的日志断言） ──────────────────────────

        private static void ValidateResult(HexFeatureSnapshot snap, HexFeatureState state)
        {
            int violations = 0;

            foreach (var river in state.Rivers)
            {
                // 水面单调不升 + 剖面 |Δe| ≤ 1（阶梯河）
                for (int i = 1; i < river.Cells.Count; i++)
                {
                    if (river.WaterY[i] > river.WaterY[i - 1] + 1e-4f)
                        violations++;
                    int de = snap.GetElev(river.Cells[i]) - snap.GetElev(river.Cells[i - 1]);
                    if (math.abs(de) > 1)
                        violations++;
                }

                Debug.Log($"[HexMap] 河 #{river.RiverId}：" +
                          $"起点 {river.Cells[0].x},{river.Cells[0].y} → " +
                          $"终点 {river.Cells[^1].x},{river.Cells[^1].y}" +
                          $"，长度 {river.Cells.Count}（河岸 {river.BankCells?.Count ?? 0}）" +
                          $"，结束类型 {river.End}");
            }

            foreach (var lake in state.Lakes)
            {
                Debug.Log($"[HexMap] 湖 #{lake.LakeId}：格数 {lake.Cells.Count}，" +
                          $"水位 {lake.Level}（y={lake.WaterY:F1}），" +
                          $"溢流 {(lake.SpillCell.x >= 0 ? $"{lake.SpillCell.x},{lake.SpillCell.y}" : "无（封闭）")}");
            }

            foreach (var road in state.Roads)
            {
                Debug.Log($"[HexMap] 路 #{road.RoadId}：POI {road.FromPoi} ↔ {road.ToPoi}，" +
                          $"格数 {road.Cells.Count}，剖面落差 " +
                          $"{(road.Elevations != null && road.Elevations.Count > 0 ? math.abs(road.Elevations[^1] - road.Elevations[0]) : 0)}");
            }

            if (violations > 0)
                Debug.LogError($"[HexMap] 河流剖面断言失败 {violations} 处（水面回升/落差>1），" +
                               "请检查行走算法");
        }

        // ── 重生成清理 ──────────────────────────────────────────────

        /// <summary>
        /// 清空特征运行状态（网格实体/植被/标签/覆写表）；
        /// resetElevation=true 时全部 cell 高程重置回噪声值（边界圈=0）。
        /// </summary>
        private void ResetFeatures(bool resetElevation)
        {
            var em = EntityManager;
            var featureEntity = _featureQuery.GetSingletonEntity();
            var state = em.GetComponentData<HexFeatureState>(featureEntity);

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

            em.DestroyEntity(GetEntityQuery(ComponentType.ReadOnly<HexScatterInstance>()));

            em.RemoveComponent(GetEntityQuery(ComponentType.ReadOnly<HexRiverCell>()),
                ComponentType.ReadWrite<HexRiverCell>());
            em.RemoveComponent(GetEntityQuery(ComponentType.ReadOnly<HexLakeCell>()),
                ComponentType.ReadWrite<HexLakeCell>());
            em.RemoveComponent(GetEntityQuery(ComponentType.ReadOnly<HexRoadCell>()),
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
                var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
                var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;
                ref var blob = ref config.Blob.Value;

                foreach (var kv in _streaming.CellLookup)
                {
                    var cell = em.GetComponentData<HexCellData>(kv.Value);
                    bool isBoundary = HexBoundary.IsBoundary(cell, blob.CellCount);
                    int elevation = HexMapTerrainMath.ElevationFromNoise(
                        ref blob, cell.Position, isBoundary, out _);
                    if (elevation != cell.Elevation)
                        HexMapCellEditUtil.ApplyElevation(em, kv.Value, elevation, ref blob);
                }
            }

            Debug.Log($"[HexMap] 特征状态已清空（ResetElevation={resetElevation}），" +
                      "全图就绪后重新生成");
        }

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

        private static int TotalCells(System.Collections.Generic.IEnumerable<RiverPath> rivers)
        {
            int n = 0;
            foreach (var r in rivers)
                n += r.Cells.Count;
            return n;
        }

        private static int TotalCells(System.Collections.Generic.IEnumerable<LakeData> lakes)
        {
            int n = 0;
            foreach (var l in lakes)
                n += l.Cells.Count;
            return n;
        }
    }
}
