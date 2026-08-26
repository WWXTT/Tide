using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 流式加载系统：根据摄像机位置动态创建/销毁 cell entities。
    /// 每帧限额创建/销毁（预算化），避免卡顿。
    ///
    /// 生成顺序：
    /// 1. 按区域坐标创建 cell 实体 + Neighbors buffer（初始为 Entity.Null）
    /// 2. 通过 坐标→实体 映射表为新建 cell 建立 6 路双向邻居链接（O(1) 查找）
    /// 3. 标记 CellDirty（触发网格生成）
    ///
    /// 注意：邻居查找/卸载必须走 _cellLookup 映射表，禁止对全表做线性扫描——
    /// 100×100 地图下 O(n²) 扫描（旧版 LinkNeighbors/FindCellAt）会直接卡死主线程。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(HexTerrainGenerationSystem))]
    public partial class HexChunkStreamingSystem : SystemBase
    {
        /// <summary>已加载 cell：offset 坐标 → 实体（邻居链接 / 卸载共用）</summary>
        private NativeHashMap<int2, Entity> _cellLookup;
        private EntityArchetype _cellArchetype;

        /// <summary>坐标 → 实体映射（HexMapEditingSystem 拾取用，只读，勿在外部增删）</summary>
        public NativeHashMap<int2, Entity> CellLookup => _cellLookup;

        protected override void OnCreate()
        {
            _cellLookup = new NativeHashMap<int2, Entity>(10000, Allocator.Persistent);

            // 预定义 archetype（TerrainPending：地形生成待处理标记，生成后移除）
#if ENABLE_HEX_DEBUG_LABEL
            _cellArchetype = EntityManager.CreateArchetype(
                typeof(HexCellData),
                typeof(Neighbors),
                typeof(CellDirty),
                typeof(TerrainPending),
                typeof(EntityDebugLabel));
#else
            _cellArchetype = EntityManager.CreateArchetype(
                typeof(HexCellData),
                typeof(Neighbors),
                typeof(CellDirty),
                typeof(TerrainPending));
#endif

            RequireForUpdate<HexMapConfig>();
            RequireForUpdate<HexMapCameraData>();
        }

        protected override void OnDestroy()
        {
            if (_cellLookup.IsCreated)
                _cellLookup.Dispose();
        }

        protected override void OnUpdate()
        {
            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponent<HexMapConfig>(configEntity);
            ref var blob = ref config.Blob.Value;

            var cameraData = SystemAPI.GetSingleton<HexMapCameraData>();

            // 摄像机所在 cell 坐标（地图空间 → cell 坐标）
            var metrics = HexMetrics.FromBlob(ref blob);
            var cellCoords = HexCoordinates.FromPosition(cameraData.Position, in metrics);
            var cameraOffset = cellCoords.ToOffsetCoordinates();

            var mapMax = new int2(blob.CellCount.x - 1, blob.CellCount.y - 1);

            // 加载范围：新 cell 在此矩形内创建
            int loadRadius = (int)math.ceil(config.LoadRadius);
            int2 min = math.max(int2.zero, cameraOffset - loadRadius);
            int2 max = math.min(mapMax, cameraOffset + loadRadius);

            // 卸载范围：只有超出这个更大的矩形才销毁。加载/卸载半径之差是滞回带，
            // 摄像机在加载边界附近抖动时不会反复重建同一批 cell（重建 = Job + Mesh 上传）
            int unloadRadius = (int)math.ceil(config.UnloadRadius);
            int2 keepMin = math.max(int2.zero, cameraOffset - unloadRadius);
            int2 keepMax = math.min(mapMax, cameraOffset + unloadRadius);

            var loadedKeys = _cellLookup.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < loadedKeys.Length; i++)
            {
                var key = loadedKeys[i];
                if (key.x < keepMin.x || key.x > keepMax.x || key.y < keepMin.y || key.y > keepMax.y)
                {
                    UnloadCell(key);
                    _cellLookup.Remove(key);
                }
            }
            loadedKeys.Dispose();

            // 加载新 cell（帧预算，行优先遍历）
            int createdThisFrame = 0;
            for (int z = min.y; z <= max.y && createdThisFrame < config.MaxCellCreationsPerFrame; z++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    if (createdThisFrame >= config.MaxCellCreationsPerFrame)
                        break;
                    if (_cellLookup.ContainsKey(new int2(x, z)))
                        continue;

                    CreateCell(new int2(x, z), ref blob, in metrics);
                    createdThisFrame++;
                }
            }
        }

        /// <summary>
        /// 创建单个 cell entity，登记到映射表并与已存在的邻居建立双向链接
        /// </summary>
        private void CreateCell(int2 offsetCoords, ref HexMapConfigBlob blob, in HexMetrics metrics)
        {
            var em = EntityManager;

            var cellEntity = em.CreateEntity(_cellArchetype);

            var coords = HexCoordinates.FromOffsetCoordinates(offsetCoords.x, offsetCoords.y);

            float3 position;
            position.x = (offsetCoords.x + offsetCoords.y * 0.5f - offsetCoords.y / 2) * (metrics.InnerRadius * 2f);
            position.y = 0f;
            position.z = offsetCoords.y * (metrics.OuterRadius * 1.5f);

            em.SetComponentData(cellEntity, new HexCellData
            {
                Coordinates = coords,
                Position = position,
                Elevation = -1, // 哨兵值：尚未生成地形（合法高度为 0..MaxElevation）
                TerrainIndex = 0,
            });

            // 初始化 6 路邻居 buffer
            var neighbors = em.GetBuffer<Neighbors>(cellEntity);
            neighbors.Length = 6;
            for (int d = 0; d < 6; d++)
                neighbors[d] = new Neighbors { Value = Entity.Null };

            // 启用 CellDirty
            em.SetComponentEnabled<CellDirty>(cellEntity, true);

#if ENABLE_HEX_DEBUG_LABEL
            // 编辑器分组标签
            em.SetComponentData(cellEntity, new EntityDebugLabel { Group = "HexCell" });
#endif

            // 登记到映射表，供后续新建的邻居反向链接
            _cellLookup.Add(offsetCoords, cellEntity);

            // 与已存在的邻居建立链接（对侧 cell 会被标脏以补建连接区域）
            LinkNeighbors(cellEntity, offsetCoords);
        }

        /// <summary>
        /// 为单个 cell 建立 6 路双向邻居链接（查找走 _cellLookup，O(1)）。
        /// 奇偶行偏移逻辑与旧版 HexGrid.CreateCell 一致：
        /// SE/SW 位于 z-1 行（几何南方向），NE/NW 位于 z+1 行（几何北方向）
        /// </summary>
        private void LinkNeighbors(Entity cellEntity, int2 offsetCoords)
        {
            int x = offsetCoords.x;
            int z = offsetCoords.y;
            int odd = z & 1;

            TryLink(cellEntity, HexDirection.W, new int2(x - 1, z));
            TryLink(cellEntity, HexDirection.E, new int2(x + 1, z));
            TryLink(cellEntity, HexDirection.SE, new int2(x + odd, z - 1));
            TryLink(cellEntity, HexDirection.SW, new int2(x - 1 + odd, z - 1));
            TryLink(cellEntity, HexDirection.NE, new int2(x + odd, z + 1));
            TryLink(cellEntity, HexDirection.NW, new int2(x - 1 + odd, z + 1));
        }

        /// <summary>
        /// 尝试把 cell 与指定坐标的 cell 建立双向链接。
        /// 对侧 cell 的网格可能已生成（缺与新 cell 的连接区域），将其标脏重建
        /// </summary>
        private void TryLink(Entity cellEntity, HexDirection direction, int2 neighborOffset)
        {
            Entity neighbor;
            if (!_cellLookup.TryGetValue(neighborOffset, out neighbor))
                return;

            var em = EntityManager;
            var cellNeighbors = em.GetBuffer<Neighbors>(cellEntity);
            if (cellNeighbors[(int)direction].Value != Entity.Null)
                return;

            cellNeighbors[(int)direction] = new Neighbors { Value = neighbor };

            var neighborNeighbors = em.GetBuffer<Neighbors>(neighbor);
            neighborNeighbors[(int)direction.Opposite()] = new Neighbors { Value = cellEntity };

            // 对侧 cell 需要补建与新 cell 的连接区域
            em.SetComponentEnabled<CellDirty>(neighbor, true);
        }

        /// <summary>
        /// 卸载 cell：清空其邻居的对侧引用（并标脏以去掉连接区域），销毁实体
        /// </summary>
        private void UnloadCell(int2 offsetCoords)
        {
            Entity cellEntity;
            if (!_cellLookup.TryGetValue(offsetCoords, out cellEntity))
                return;

            var em = EntityManager;
            var neighbors = em.GetBuffer<Neighbors>(cellEntity);
            for (int d = 0; d < neighbors.Length; d++)
            {
                var neighborEntity = neighbors[d].Value;
                if (neighborEntity == Entity.Null || !em.HasComponent<Neighbors>(neighborEntity))
                    continue;

                var neighborNeighbors = em.GetBuffer<Neighbors>(neighborEntity);
                neighborNeighbors[(int)((HexDirection)d).Opposite()] = new Neighbors { Value = Entity.Null };

                // 邻居需要重建以去掉与被卸载 cell 的连接区域
                em.SetComponentEnabled<CellDirty>(neighborEntity, true);
            }

            em.DestroyEntity(cellEntity);
        }
    }
}
