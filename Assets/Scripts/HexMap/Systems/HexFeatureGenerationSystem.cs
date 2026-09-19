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
    /// 提交/网格构建/状态重置的实体逻辑在 HexFeatureCommitUtil（与存档加载路径共用）。
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

            var config0 = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;

            // 重生成请求消费（游戏内面板/存档加载写入；须在 _streaming 赋值后，ResetFeatures 用到）
            if (em.HasComponent<HexFeatureRegenerateRequest>(configEntity))
            {
                var req = em.GetComponentData<HexFeatureRegenerateRequest>(configEntity);
                em.RemoveComponent<HexFeatureRegenerateRequest>(configEntity);
                HexFeatureCommitUtil.ResetFeatures(em, _streaming.CellLookup,
                    em.GetComponentData<HexFeatureState>(_featureQuery.GetSingletonEntity()),
                    req.ResetElevation, ref config0.Blob.Value);
                _generated = false;
            }

            if (_generated)
                return;

            ref var blob = ref config0.Blob.Value;
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
                        "loadRadius 小于地图时会一直挂起——面板操作会调用 EnsureAllLoaded 强制补建");
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
                HexRiverGenerator.Generate(snapshot, settings.rivers, HexPoiRuntime.Pois, state);

            if (featureConfig.EnableRoads)
                HexRoadGenerator.Generate(snapshot, settings.roads, HexPoiRuntime.Pois, state);

            HexFeatureCommitUtil.CommitSnapshot(em, _streaming.CellLookup, snapshot, state, settings, ref blob);
            HexFeatureCommitUtil.BuildFeatureMeshes(World, em, snapshot, state, featureConfig, ref blob);

            ValidateResult(snapshot, state);

            state.VegetationDirty = true;   // 植被系统（步骤 5）消费
            state.GenerationSerial++;       // 确定性验证的完成信号

            Debug.Log("[HexMap] 特征生成完成" +
                      $"：河流 {state.Rivers.Count} 条（总格数 {TotalCells(state.Rivers)}）" +
                      $"、湖泊 {state.Lakes.Count} 个（总格数 {TotalCells(state.Lakes)}）");
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
