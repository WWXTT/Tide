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
            if (!HexMapCellEditUtil.PickCell(ray, EntityManager, ref blob, in metrics,
                    streaming.CellLookup, out var cellEntity, out _))
                return;

            if (elevate)
            {
                bool lower = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                int delta = lower ? -1 : 1;
                var cell = EntityManager.GetComponentData<HexCellData>(cellEntity);
                HexMapCellEditUtil.ApplyElevation(EntityManager, cellEntity,
                    cell.Elevation + delta, ref blob);
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

        private void ApplyTerrain(Entity cellEntity, int terrainIndex, ref HexMapConfigBlob blob)
        {
            var em = EntityManager;
            var cell = em.GetComponentData<HexCellData>(cellEntity);

            if (cell.TerrainIndex == terrainIndex)
                return;

            cell.TerrainIndex = terrainIndex;
            em.SetComponentData(cellEntity, cell);
            HexMapCellEditUtil.MarkCellAndNeighborsDirty(em, cellEntity);
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
