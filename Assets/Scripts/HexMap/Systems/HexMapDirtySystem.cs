using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HexMap
{
    /// <summary>
    /// 脏标记系统：当 cell 变更时，标记该 cell 为脏（触发网格重建）。
    ///
    /// 当前为空实现（仅骨架）。增量编辑功能（改变 cell 高度/地形）需要：
    /// 1. 监听 cell 数据变更（通过 ComponentLookup 或事件）
    /// 2. 标记 cell 的 CellDirty
    /// 3. 遍历 cell 的 6 路邻居，标记相邻 cell（因为邻居的连接区域也会受影响）
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HexTerrainGenerationSystem))]
    public partial struct HexMapDirtySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 当前无需运行
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 将来实现：
            // - 查询标记"待更新"的 cell（通过 tag 组件）
            // - 读取其 Neighbors
            // - 标记该 cell 及相邻 cell 的 CellDirty
        }
    }
}
