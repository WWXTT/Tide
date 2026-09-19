using Unity.Entities;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 特征覆写对流式回归的幂等补挂：
    /// cell 卸载重建后高程回到噪声值、特征标签丢失，本系统按 HexFeatureState 的
    /// 覆写表对账——cell 不存在（已卸载）→ 从 Applied 移除；cell 存在、地形已生成、
    /// 未 Applied → 套用高程 + 补标签 + 标脏、加入 Applied。
    /// 用户手编之后的 cell 不会被回滚（Applied 已标记），直到该 cell 卸载重建。
    /// 地图仅数百 cell，逐帧全量对账无性能顾虑。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HexTerrainGenerationSystem))]
    public partial class HexFeatureOverlaySystem : SystemBase
    {
        private HexChunkStreamingSystem _streaming;
        private EntityQuery _stateQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<HexFeatureState>();
            _stateQuery = GetEntityQuery(typeof(HexFeatureState));
        }

        protected override void OnUpdate()
        {
            // 托管单例：GetSingletonEntity<T> 泛型只接受非托管类型，走 EntityQuery
            var state = EntityManager.GetComponentData<HexFeatureState>(
                _stateQuery.GetSingletonEntity());
            bool hasOverrides = state.ElevationOverrides.Count > 0;
            bool hasTags = state.Rivers.Count > 0 || state.Lakes.Count > 0 || state.Roads.Count > 0;
            if (!hasOverrides && !hasTags)
                return;

            if (_streaming == null)
            {
                _streaming = World.GetExistingSystemManaged<HexChunkStreamingSystem>();
                if (_streaming == null)
                    return;
            }

            var em = EntityManager;
            var lookup = _streaming.CellLookup;

            // ---- 高程覆写对账 ----
            if (hasOverrides)
            {
                var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
                var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity);
                ref var blob = ref config.ValueRO.Blob.Value;

                foreach (var kv in state.ElevationOverrides)
                {
                    if (!lookup.TryGetValue(kv.Key, out var e) || !em.HasComponent<HexCellData>(e))
                    {
                        state.OverlayApplied.Remove(kv.Key);   // cell 已卸载，重建后需重套
                        continue;
                    }
                    if (state.OverlayApplied.Contains(kv.Key))
                        continue;

                    var cell = em.GetComponentData<HexCellData>(e);
                    if (cell.Elevation < 0)
                        continue;   // 地形未生成（TerrainPending），等下一轮

                    HexMapCellEditUtil.ApplyElevation(em, e, kv.Value, ref blob);
                    state.OverlayApplied.Add(kv.Key);
                }
            }

            // ---- 标签补挂（幂等：仅补缺失的）----
            foreach (var river in state.Rivers)
            {
                for (int i = 0; i < river.Cells.Count; i++)
                {
                    if (lookup.TryGetValue(river.Cells[i], out var e) &&
                        em.HasComponent<HexCellData>(e) && !em.HasComponent<HexRiverCell>(e))
                    {
                        em.AddComponentData(e, new HexRiverCell
                        {
                            RiverId = river.RiverId,
                            WaterY = river.WaterY[i],
                            IsBank = 0,
                        });
                    }
                }

                for (int i = 0; i < river.BankCells?.Count; i++)
                {
                    if (lookup.TryGetValue(river.BankCells[i], out var e) &&
                        em.HasComponent<HexCellData>(e) && !em.HasComponent<HexRiverCell>(e))
                    {
                        em.AddComponentData(e, new HexRiverCell
                        {
                            RiverId = river.RiverId,
                            WaterY = river.BankWaterY[i],
                            IsBank = 1,
                        });
                    }
                }
            }

            foreach (var lake in state.Lakes)
            {
                for (int i = 0; i < lake.Cells.Count; i++)
                {
                    if (lookup.TryGetValue(lake.Cells[i], out var e) &&
                        em.HasComponent<HexCellData>(e) && !em.HasComponent<HexLakeCell>(e))
                    {
                        em.AddComponentData(e, new HexLakeCell
                        {
                            LakeId = lake.LakeId,
                            WaterY = lake.WaterY,
                        });
                    }
                }
            }

            foreach (var road in state.Roads)
            {
                for (int i = 0; i < road.Cells.Count; i++)
                {
                    if (lookup.TryGetValue(road.Cells[i], out var e) &&
                        em.HasComponent<HexCellData>(e) && !em.HasComponent<HexRoadCell>(e))
                    {
                        em.AddComponentData(e, new HexRoadCell { RoadId = road.RoadId });
                    }
                }
            }
        }
    }
}
