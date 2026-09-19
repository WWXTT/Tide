using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 地形生成系统：为新建的 cell 采样噪声 → 计算 elevation + 按分带规则分配 TerrainIndex
    /// （HexMapTerrainMath.TerrainIndexFor，空分带表时全 0 层；手动刷地块可覆盖，
    /// 直到 ResetElevation 重置回分带值）。采样完成后更新 Position.y（加入高程扰动），
    /// 并标记 cell 脏（触发网格重建）。
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HexChunkStreamingSystem))]
    public partial struct HexTerrainGenerationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HexMapConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponent<HexMapConfig>(configEntity);
            ref var blob = ref config.Blob.Value;

            // 只查询带 TerrainPending 标签的新建 cell（生成后移除标签，避免每帧全表扫描）
            var query = SystemAPI.QueryBuilder()
                .WithAll<HexCellData, TerrainPending>()
                .Build();

            if (query.IsEmpty)
                return;

            var cellEntities = query.ToEntityArray(Allocator.TempJob);
            var cellDataArray = query.ToComponentDataArray<HexCellData>(Allocator.TempJob);

            // 并行采样噪声 + 计算地形
            var job = new TerrainGenerationJob
            {
                Blob = config.Blob,
                CellData = cellDataArray,
            };
            var jobHandle = job.Schedule(cellDataArray.Length, 64);
            jobHandle.Complete();

            // 写回结果并标记 cell 脏
            var em = state.EntityManager;

            for (int i = 0; i < cellEntities.Length; i++)
            {
                var cellEntity = cellEntities[i];
                var newData = cellDataArray[i];

                em.SetComponentData(cellEntity, newData);

                // 标记 cell 为脏（触发网格重建）
                if (em.HasComponent<CellDirty>(cellEntity))
                {
                    em.SetComponentEnabled<CellDirty>(cellEntity, true);
                }

                // 移除待生成标记
                em.RemoveComponent<TerrainPending>(cellEntity);
            }

            cellEntities.Dispose();
            cellDataArray.Dispose();
        }

        [BurstCompile]
        private struct TerrainGenerationJob : IJobParallelFor
        {
            [ReadOnly] public BlobAssetReference<HexMapConfigBlob> Blob;
            public NativeArray<HexCellData> CellData;

            public void Execute(int index)
            {
                ref var blob = ref Blob.Value;
                var cell = CellData[index];

                // 跳过已生成的 cell（Elevation >= 0 为已生成，-1 为待生成哨兵值）
                if (cell.Elevation >= 0)
                    return;

                var position = cell.Position;
                bool isBoundary = HexBoundary.IsBoundary(in cell, blob.CellCount);

                // 统一公式（HexMapTerrainMath）：边界 cell 恒 0，内部按噪声。
                // 与重生成重置/特征路径共用，公式漂移 = 重生成结果与初次生成不一致
                int elevation = HexMapTerrainMath.ElevationFromNoise(
                    ref blob, position, isBoundary, out float y);
                position.y = y;

                cell.Elevation = elevation;
                cell.TerrainIndex = HexMapTerrainMath.TerrainIndexFor(ref blob, elevation);
                cell.Position = position;

                CellData[index] = cell;
            }
        }
    }
}
