using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 地形编辑系统（Play 模式，主线程）：
    /// - 左键点击/拖动：把 cell 的 TerrainIndex 涂成当前笔刷地形
    /// - 数字键 0-9（含小键盘）：切换笔刷地形索引
    /// - 右键点击/拖动：抬升高度；Shift+右键：降低
    ///
    /// 拾取不依赖物理（cell 网格没有 collider）：沿视线对六边形列做射线步进，
    /// 首次降到某 cell 顶面（Position.y）以下即为命中，天然支持不同高度的地形。
    ///
    /// 编辑后把该 cell 与 6 路邻居标 CellDirty（连接区域嵌入了双方的索引/高度），
    /// 由 HexMeshWriteSystem 在 Presentation 阶段按帧预算自动重建网格。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HexTerrainGenerationSystem))]
    public partial class HexMapEditingSystem : SystemBase
    {
        /// <summary>默认笔刷 = 1：整图初始地形都是 0，默认 0 会导致点击无可见变化</summary>
        private int _activeTerrain = 1;
        private int _terrainLimit = 9;
        private bool _limitResolved;

        private bool _warnedNoCamera;
        private bool _warnedNoStreaming;

        protected override void OnCreate()
        {
            RequireForUpdate<HexMapConfig>();
        }

        protected override void OnUpdate()
        {
            if (!Application.isPlaying)
                return;

            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;
            ref var blob = ref config.Blob.Value;
            var metrics = HexMetrics.FromBlob(ref blob);

            ResolveTerrainLimit(config);

            // 数字键切换笔刷地形（限制在数组层数内，防止再次触发越界采样噪点）
            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                {
                    _activeTerrain = math.clamp(i, 0, _terrainLimit);
                    Debug.Log($"[HexMap] 笔刷地形 = {_activeTerrain}（上限 {_terrainLimit}）");
                }
            }

            bool paint = Input.GetMouseButton(0);
            bool elevate = Input.GetMouseButton(1);
            if (!paint && !elevate)
                return;

            var cam = ResolveCamera();
            if (cam == null)
                return;

            var streaming = World.GetExistingSystemManaged<HexChunkStreamingSystem>();
            if (streaming == null)
            {
                if (!_warnedNoStreaming)
                {
                    Debug.LogWarning("[HexMap] 找不到 HexChunkStreamingSystem，无法拾取 cell");
                    _warnedNoStreaming = true;
                }
                return;
            }

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!PickCell(ray, ref blob, in metrics, streaming.CellLookup, out var cellEntity))
                return;

            if (elevate)
            {
                bool lower = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                ApplyElevation(cellEntity, lower ? -1 : 1, ref blob);
            }
            else
            {
                ApplyTerrain(cellEntity, _activeTerrain, ref blob);
            }
        }

        private Camera ResolveCamera()
        {
            var cam = Camera.main;
            if (cam != null)
                return cam;

            if (!_warnedNoCamera)
            {
                Debug.LogWarning("[HexMap] Camera.main 为空（相机没挂 MainCamera tag？），回退到场景第一个相机");
                _warnedNoCamera = true;
            }
            return Object.FindFirstObjectByType<Camera>();
        }

        /// <summary>
        /// 射线步进拾取：沿视线步进，命中条件是「位于某 cell 列内且已降到该 cell
        /// 顶面以下」。步长 0.5（cell 宽 ~17，误差可忽略），视线向下且越过最低
        /// 地面后提前退出。
        /// </summary>
        private bool PickCell(Ray ray, ref HexMapConfigBlob blob, in HexMetrics metrics,
            NativeHashMap<int2, Entity> lookup, out Entity cellEntity)
        {
            cellEntity = Entity.Null;
            var em = EntityManager;
            const float step = 0.5f;
            // 视线朝下：走到穿过 y=0 平面再走 50 单位即可；否则走满诊断距离
            float descent = ray.direction.y < -0.05f
                ? ((float3)ray.origin).y / -ray.direction.y + 50f
                : 3000f;
            float maxDist = math.clamp(descent, 50f, 3000f);

            for (float t = step; t < maxDist; t += step)
            {
                float3 p = (float3)ray.origin + (float3)ray.direction * t;

                var offset = HexCoordinates.FromPosition(p, in metrics).ToOffsetCoordinates();
                bool inBounds = offset.x >= 0 && offset.x < blob.CellCount.x &&
                                offset.y >= 0 && offset.y < blob.CellCount.y;

                if (inBounds && lookup.TryGetValue(offset, out var e) && em.HasComponent<HexCellData>(e))
                {
                    var cell = em.GetComponentData<HexCellData>(e);
                    // Elevation >= 0：地形已生成（-1 是 TerrainPending 哨兵）
                    if (cell.Elevation >= 0 && p.y <= cell.Position.y + 0.75f)
                    {
                        cellEntity = e;
                        return true;
                    }
                }

                if (p.y < -10f && ray.direction.y < 0f)
                    return false;
            }
            return false;
        }

        private void ApplyTerrain(Entity cellEntity, int terrainIndex, ref HexMapConfigBlob blob)
        {
            var em = EntityManager;
            var cell = em.GetComponentData<HexCellData>(cellEntity);

            // 禁止编辑边界 cell：外圈恒为 0 高度是边界连接区不开缝的前提
            if (HexBoundary.IsBoundary(in cell, blob.CellCount))
                return;

            if (cell.TerrainIndex == terrainIndex)
                return;

            cell.TerrainIndex = terrainIndex;
            em.SetComponentData(cellEntity, cell);
            MarkCellAndNeighborsDirty(cellEntity);
        }

        private void ApplyElevation(Entity cellEntity, int delta, ref HexMapConfigBlob blob)
        {
            var em = EntityManager;
            var cell = em.GetComponentData<HexCellData>(cellEntity);

            // 禁止编辑边界 cell：外圈恒为 0 高度是边界连接区不开缝的前提
            if (HexBoundary.IsBoundary(in cell, blob.CellCount))
                return;

            int newElevation = math.clamp(cell.Elevation + delta, 0, blob.MaxElevation);
            if (newElevation == cell.Elevation)
                return;

            // 与 HexTerrainGenerationSystem 相同的 y 公式：台阶 × 噪声缩放。
            // SampleNoise 只用 x/z，用当前位置重采样结果稳定
            var noise = HexMetrics.SampleNoise(ref blob, cell.Position);
            cell.Elevation = newElevation;
            cell.Position = new float3(
                cell.Position.x,
                HexMetrics.ElevationToY(ref blob, newElevation, noise.y),
                cell.Position.z);

            em.SetComponentData(cellEntity, cell);
            MarkCellAndNeighborsDirty(cellEntity);
        }

        /// <summary>cell 与 6 路邻居全部标脏：边缘条带/扇面嵌入了双方的索引与高度</summary>
        private void MarkCellAndNeighborsDirty(Entity cellEntity)
        {
            var em = EntityManager;
            if (em.HasComponent<CellDirty>(cellEntity))
                em.SetComponentEnabled<CellDirty>(cellEntity, true);

            if (!em.HasBuffer<Neighbors>(cellEntity))
                return;

            var neighbors = em.GetBuffer<Neighbors>(cellEntity);
            for (int d = 0; d < neighbors.Length; d++)
            {
                var n = neighbors[d].Value;
                if (n != Entity.Null && em.HasComponent<CellDirty>(n))
                    em.SetComponentEnabled<CellDirty>(n, true);
            }
        }

        /// <summary>
        /// 从材质绑定的 albedo 数组读取层数，作为笔刷索引上限——
        /// 涂出数组范围会再次触发越界采样的闪烁噪点
        /// </summary>
        private void ResolveTerrainLimit(in HexMapConfig config)
        {
            if (_limitResolved)
                return;

            var material = config.TerrainMaterial.Value;
            if (material == null)
                return;

            if (material.GetTexture("_TerrainAlbedoArray") is Texture2DArray array)
            {
                _terrainLimit = array.depth - 1;
                _limitResolved = true;
                _activeTerrain = math.clamp(_activeTerrain, 0, _terrainLimit);
            }
        }
    }
}
