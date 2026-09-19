using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 地形/世界编辑系统（Play 模式，主线程），按 HexEditorToolState.Mode 分发：
    ///
    /// Terrain（默认，行为=改造前）：
    /// - 左键点击/拖动：把 cell 的 TerrainIndex 涂成当前笔刷地形
    /// - 数字键 0-9（含小键盘）：切换笔刷地形索引
    /// - 右键点击/拖动：抬升高度；Shift+右键：降低
    ///
    /// PlaceSpring / PlaceRoadNode / DeletePoi（游戏内 POI 编辑）：
    /// - 左键单击：放置/删除 POI（写 HexPoiRuntime.Pois，R 重生成后生效）
    ///
    /// 拾取不依赖物理（cell 网格没有 collider）：沿视线对六边形列做射线步进，
    /// 首次降到某 cell 顶面（Position.y）以下即为命中，天然支持不同高度的地形。
    ///
    /// 守卫：面板文本框聚焦时不响应数字键；指针悬停面板时不响应鼠标
    /// （HexEditorToolState 默认值下两守卫恒为假 = 无面板时行为不变）。
    ///
    /// 编辑后把该 cell 与 6 路邻居标 CellDirty（连接区域嵌入了双方的索引/高度），
    /// 由 HexMeshWriteSystem 在 Presentation 阶段按帧预算自动重建网格。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HexTerrainGenerationSystem))]
    public partial class HexMapEditingSystem : SystemBase
    {
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

            // 面板文本输入框聚焦中：数字键/快捷键不抢输入
            if (HexEditorToolState.TextInputFocused)
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
                    HexEditorToolState.SetBrushTerrain(math.clamp(i, 0, HexEditorToolState.TerrainLimit));
                    Debug.Log($"[HexMap] 笔刷地形 = {HexEditorToolState.BrushTerrainIndex}" +
                              $"（上限 {HexEditorToolState.TerrainLimit}）");
                }
            }

            // 指针悬停编辑面板：不吃地图鼠标（点击/拖动全让给 UI）
            if (HexEditorToolState.PointerOverUI)
                return;

            bool paint = Input.GetMouseButton(0);
            bool elevate = Input.GetMouseButton(1);
            bool placeClick = Input.GetMouseButtonDown(0);   // POI 放置/删除用单击语义（非拖动）
            if (!paint && !elevate && !placeClick)
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

            switch (HexEditorToolState.Mode)
            {
                case HexEditorToolMode.PlaceSpring:
                case HexEditorToolMode.PlaceRoadNode:
                    if (placeClick && HexMapCellEditUtil.PickCell(ray, EntityManager, ref blob, in metrics,
                            streaming.CellLookup, out var pe, out var pOffset))
                        PlacePoi(pe, pOffset);
                    break;

                case HexEditorToolMode.DeletePoi:
                    if (placeClick && HexMapCellEditUtil.PickCell(ray, EntityManager, ref blob, in metrics,
                            streaming.CellLookup, out _, out var dOffset))
                        DeletePoiNear(dOffset);
                    break;

                default:
                    if (!paint && !elevate)
                        break;

                    if (!HexMapCellEditUtil.PickCell(ray, EntityManager, ref blob, in metrics,
                            streaming.CellLookup, out var cellEntity, out _))
                        break;

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
                        HexMapCellEditUtil.ApplyTerrain(EntityManager, cellEntity,
                            HexEditorToolState.BrushTerrainIndex);
                    }
                    break;
            }
        }

        // ── POI 放置 / 删除（游戏内编辑器，替代旧 SceneView 工具）──

        private void PlacePoi(Entity cellEntity, int2 offset)
        {
            var type = HexEditorToolState.Mode == HexEditorToolMode.PlaceSpring
                ? PoiType.RiverSpring
                : PoiType.RoadNode;

            var cell = EntityManager.GetComponentData<HexCellData>(cellEntity);
            HexPoiRuntime.Pois.Add(new HexPoiData
            {
                Type = type,
                CellOffset = offset,
                WorldPos = cell.Position,
                Radius = HexEditorToolState.PoiRadius,
                Note = "",
            });
            Debug.Log($"[HexMap] 已放置 {type} POI @ {offset.x},{offset.y}" +
                      $"（{HexPoiRuntime.Pois.Count} 个，按 R 重生成生效）");
        }

        private void DeletePoiNear(int2 offset)
        {
            float rSq = HexEditorToolState.PoiRadius * HexEditorToolState.PoiRadius;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < HexPoiRuntime.Pois.Count; i++)
            {
                var p = HexPoiRuntime.Pois[i];
                float dx = p.CellOffset.x - offset.x;
                float dz = p.CellOffset.y - offset.y;
                float dSq = dx * dx + dz * dz;
                if (dSq <= rSq && dSq < bestDist)
                {
                    bestDist = dSq;
                    best = i;
                }
            }

            if (best < 0)
                return;

            var removed = HexPoiRuntime.Pois[best];
            HexPoiRuntime.Pois.RemoveAt(best);
            Debug.Log($"[HexMap] 已删除 {removed.Type} POI @ {removed.CellOffset.x},{removed.CellOffset.y}" +
                      $"（按 R 重生成生效）");
        }

        // ── 基础设施 ────────────────────────────────────────────────

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
        /// 从材质绑定的 albedo 数组读取层数，作为笔刷索引上限——
        /// 涂出数组范围会再次触发越界采样的闪烁噪点。结果同步进 HexEditorToolState
        /// （面板笔刷按钮行按此上限生成）。
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
                _limitResolved = true;
                HexEditorToolState.TerrainLimit = array.depth - 1;
                if (HexEditorToolState.BrushTerrainIndex > HexEditorToolState.TerrainLimit)
                    HexEditorToolState.BrushTerrainIndex = HexEditorToolState.TerrainLimit;
            }
        }
    }
}
