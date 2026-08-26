using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 地形生成系统：为新建的 cell 采样噪声 → 计算 elevation。
    /// 噪声只决定高度；地形类型统一初始化为 0，之后由编辑操作手动修改。
    /// 采样完成后更新 Position.y（加入高程扰动），并标记 cell 脏（触发网格重建）。
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

                int elevation;
                if (isBoundary)
                {
                    // 边界 cell 固定 elevation = 0 且不做高度扰动：
                    // 相邻边界的连接区在共享角点两侧必须等高，高度差会在裙边角点处错层开缝
                    elevation = 0;
                    position.y = 0f;
                }
                else
                {
                    // 内部 cell 按噪声生成高度
                    var noiseSample = HexMetrics.SampleNoise(ref blob, position);
                    elevation = (int)math.round(noiseSample.w * blob.MaxElevation);
                    elevation = math.clamp(elevation, 0, blob.MaxElevation);

                    // 更新 Position.y（高程 × 台阶 + 有界扰动，与编辑路径共用同一公式）
                    position.y = HexMetrics.ElevationToY(ref blob, elevation, noiseSample.y);
                }

                cell.Elevation = elevation;
                cell.TerrainIndex = 0;
                cell.Position = position;

                CellData[index] = cell;
            }
        }
    }
}
