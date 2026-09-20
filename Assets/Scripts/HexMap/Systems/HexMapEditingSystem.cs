using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 地形/世界编辑系统（Play 模式，主线程），按 HexEditorToolState.Mode 分发。
    /// 所有模式统一「按住左键拖动连续涂刷」，笔画按拾取格去重（_lastAppliedOffset，
    /// 同格不重复应用）；数字键 0-9 = 当前模式的选项（0 恒为删除/清除）。
    ///
    /// Terrain（默认，行为=改造前）：左键拖涂地块贴图（选项=贴图层）；右键拖抬升 / Shift+右键降低。
    /// Height：左键拖把涂到的格设为选项高度（0-9）；右键拖 ±1 微调（可到 MaxElevation）。
    /// Water：选项 1=河 2=湖 0=清除水。一笔 = 一条河/一个湖（按下时 Begin，格随拖追加）；
    ///        改动当帧经 HexFeatureEditUtil.RefreshFeatureMeshes 重建水面（实时刷新）。
    /// Road：选项 1=路 0=清路（删除后断路自动拆段）；同样当帧重建缎带。
    /// Vegetation：选项 1..N=散布原型（每格放 clamp(密度,1,3) 株，缺省 2）、0=清除该格；
    ///        写 HexManualVegetationState（活过 R 重生成 / V 重散布 / 存档往返）。
    /// Poi：选项 1=泉水 2=路点（拖动连续放）、0=删除半径内最近 POI；R 重生成后生效。
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
        private bool _terrainLimitResolved;
        private bool _heightVegLimitResolved;

        private bool _warnedNoCamera;
        private bool _warnedNoStreaming;
        private bool _warnedNoFeatures;

        private EntityQuery _featureQuery;

        // ── 笔画状态（鼠标按住期间）──────────────────────────────

        /// <summary>本笔画当前追加中的河（Water 模式选项 1）</summary>
        private RiverPath _strokeRiver;

        /// <summary>本笔画当前追加中的湖（Water 模式选项 2）</summary>
        private LakeData _strokeLake;

        /// <summary>本笔画当前追加中的路（Road 模式选项 1）</summary>
        private RoadPath _strokeRoad;

        /// <summary>本笔画最近应用的格（同格去重；MouseDown 清空）</summary>
        private int2? _lastAppliedOffset;

        /// <summary>本帧特征列表被改过 → 帧头全量重建水/路网格（实时刷新，脏标志节流）</summary>
        private bool _featureMeshDirty;

        protected override void OnCreate()
        {
            RequireForUpdate<HexMapConfig>();
            _featureQuery = EntityManager.CreateEntityQuery(
                typeof(HexFeatureConfig), typeof(HexFeatureState));
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

            ResolveLimits(config);

            var streaming = World.GetExistingSystemManaged<HexChunkStreamingSystem>();

            // 上帧特征脏 → 本帧头重建水/路网格（HexVegetationSpawnSystem 等系统观察到完整状态）
            if (_featureMeshDirty)
            {
                _featureMeshDirty = false;
                FlushFeatureMeshRefresh(streaming, ref blob);
            }

            // 数字键 = 当前模式选项（0-9，SetOption 内按 OptionLimit 钳制）
            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                {
                    HexEditorToolState.SetOption(i);
                    Debug.Log($"[HexMap] {HexEditorToolState.Mode} 选项 = {HexEditorToolState.OptionIndex}" +
                              $"（上限 {HexEditorToolState.OptionLimit}）");
                }
            }

            // 指针悬停编辑面板：不吃地图鼠标（点击/拖动全让给 UI）
            if (HexEditorToolState.PointerOverUI)
                return;

            bool paint = Input.GetMouseButton(0);
            bool elevate = Input.GetMouseButton(1);
            if (!paint && !elevate)
            {
                EndStrokes();
                return;
            }

            var cam = ResolveCamera();
            if (cam == null)
                return;

            if (streaming == null)
            {
                if (!_warnedNoStreaming)
                {
                    Debug.LogWarning("[HexMap] 找不到 HexChunkStreamingSystem，无法拾取 cell");
                    _warnedNoStreaming = true;
                }
                return;
            }

            // 左键刚按下：起新笔画（清去重标记 + 按模式/选项开河/湖/路对象）
            if (Input.GetMouseButtonDown(0))
            {
                _lastAppliedOffset = null;
                BeginStroke();
            }

            var ray = cam.ScreenPointToRay(Input.mousePosition);

            switch (HexEditorToolState.Mode)
            {
                case HexEditorToolMode.Water:
                case HexEditorToolMode.Road:
                case HexEditorToolMode.Vegetation:
                case HexEditorToolMode.Poi:
                    if (!paint)
                        break;   // 这些模式只用左键
                    if (!HexMapCellEditUtil.PickCell(ray, EntityManager, ref blob, in metrics,
                            streaming.CellLookup, out var cellEntity, out var offset))
                        break;

                    if (!IsNewCell(offset))
                        break;   // 笔画去重：同格不重复应用
                    _lastAppliedOffset = offset;

                    var cellData = EntityManager.GetComponentData<HexCellData>(cellEntity);
                    ApplyFeatureBrush(cellEntity, offset, cellData, streaming, ref blob, in metrics);
                    break;

                case HexEditorToolMode.Height:
                    if (!HexMapCellEditUtil.PickCell(ray, EntityManager, ref blob, in metrics,
                            streaming.CellLookup, out var hEntity, out var hOffset))
                        break;

                    if (paint)
                    {
                        if (!IsNewCell(hOffset))
                            break;   // 同格已设过该高度
                        _lastAppliedOffset = hOffset;
                        HexMapCellEditUtil.ApplyElevation(EntityManager, hEntity,
                            HexEditorToolState.OptionIndex, ref blob);
                    }
                    else // 右键 ±1 微调（可到 MaxElevation）
                    {
                        bool lower = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                        var hc = EntityManager.GetComponentData<HexCellData>(hEntity);
                        HexMapCellEditUtil.ApplyElevation(EntityManager, hEntity,
                            hc.Elevation + (lower ? -1 : 1), ref blob);
                    }
                    break;

                default:   // Terrain：改造前行为（左涂贴图/右抬降，不按格去重——保持原样）
                    if (!HexMapCellEditUtil.PickCell(ray, EntityManager, ref blob, in metrics,
                            streaming.CellLookup, out var tEntity, out _))
                        break;

                    if (elevate)
                    {
                        bool lower = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                        var tc = EntityManager.GetComponentData<HexCellData>(tEntity);
                        HexMapCellEditUtil.ApplyElevation(EntityManager, tEntity,
                            tc.Elevation + (lower ? -1 : 1), ref blob);
                    }
                    else
                    {
                        HexMapCellEditUtil.ApplyTerrain(EntityManager, tEntity,
                            HexEditorToolState.OptionIndex);
                    }
                    break;
            }
        }

        // ── 特征笔刷（水/路/植被/POI 共用入口）──────────────────

        private void ApplyFeatureBrush(Entity cellEntity, int2 offset, in HexCellData cellData,
            HexChunkStreamingSystem streaming, ref HexMapConfigBlob blob, in HexMetrics metrics)
        {
            int option = HexEditorToolState.OptionIndex;

            // POI 不依赖特征单例（HexPoiRuntime 独立表）
            if (HexEditorToolState.Mode == HexEditorToolMode.Poi)
            {
                switch (option)
                {
                    case 1: PlacePoi(cellEntity, offset, cellData, PoiType.RiverSpring); break;
                    case 2: PlacePoi(cellEntity, offset, cellData, PoiType.RoadNode); break;
                    default: DeletePoiNear(offset); break;
                }
                return;
            }

            var featureEntity = ResolveFeatureEntity();
            if (featureEntity == Entity.Null)
            {
                if (!_warnedNoFeatures)
                {
                    Debug.LogWarning("[HexMap] 特征系统未安装，水/路/植被笔刷不可用（enableFeatures？）");
                    _warnedNoFeatures = true;
                }
                return;
            }
            var state = EntityManager.GetComponentData<HexFeatureState>(featureEntity);
            var featureConfig = EntityManager.GetComponentData<HexFeatureConfig>(featureEntity);

            switch (HexEditorToolState.Mode)
            {
                case HexEditorToolMode.Water:
                    if (option == 0)
                    {
                        HexFeatureEditUtil.RemoveWaterAt(state, EntityManager, streaming.CellLookup,
                            cellEntity, offset, blob.ElevationStep);
                        _featureMeshDirty = true;
                    }
                    else if (option == 1 && _strokeRiver != null)
                    {
                        HexFeatureEditUtil.AppendRiverCell(_strokeRiver, state, EntityManager,
                            cellEntity, offset, cellData.Position.y);
                        _featureMeshDirty = true;
                    }
                    else if (option == 2 && _strokeLake != null)
                    {
                        HexFeatureEditUtil.AppendLakeCell(_strokeLake, state, EntityManager,
                            cellEntity, offset, cellData.Elevation, blob.ElevationStep);
                        _featureMeshDirty = true;
                    }
                    break;

                case HexEditorToolMode.Road:
                    if (option == 0)
                    {
                        HexFeatureEditUtil.RemoveRoadAt(state, EntityManager, cellEntity, offset);
                        _featureMeshDirty = true;
                    }
                    else if (_strokeRoad != null)
                    {
                        HexFeatureEditUtil.AppendRoadCell(_strokeRoad, EntityManager,
                            cellEntity, offset, cellData.Elevation);
                        _featureMeshDirty = true;
                    }
                    break;

                case HexEditorToolMode.Vegetation:
                    ApplyVegetationBrush(offset, cellData, featureConfig, streaming, ref blob, in metrics);
                    break;
            }
        }

        private void ApplyVegetationBrush(int2 offset, in HexCellData cellData,
            HexFeatureConfig featureConfig, HexChunkStreamingSystem streaming,
            ref HexMapConfigBlob blob, in HexMetrics metrics)
        {
            int option = HexEditorToolState.OptionIndex;
            var rules = featureConfig.Settings?.scatterRules;

            if (option == 0)
            {
                DestroyVegCellInstances(offset);
                HexManualVegetationState.RecordClear(offset);
                return;
            }

            if (rules == null || option > rules.Count)
                return;
            var rule = rules[option - 1];
            if (rule == null || rule.mesh == null || rule.material == null)
                return;

            if (!featureConfig.EnableVegetation)
                return;   // 散布关闭：手动实例会被下一次整批重建清掉，不给放

            // 重涂 = 换原型：先清该格旧实例再落新株
            DestroyVegCellInstances(offset);
            int count = ResolveManualVegCount(rule, in cellData);
            var grid = new HexCellDataSource { Em = EntityManager, Lookup = streaming.CellLookup };
            int spawned = HexVegetationSpawner.SpawnCellManual(World, EntityManager, offset,
                option - 1, rule, in metrics, ref blob, grid,
                featureConfig.Settings.featureSeed, count);
            if (spawned > 0)
                HexManualVegetationState.RecordManual(offset, option - 1, count);
        }

        /// <summary>手动放置株数：取规则对该地形的密度四舍五入，无效则 2，钳 1..3</summary>
        private static int ResolveManualVegCount(HexScatterRule rule, in HexCellData cellData)
        {
            var d = rule.densityPerTerrain;
            float lambda = (d != null && cellData.TerrainIndex >= 0 && cellData.TerrainIndex < d.Count)
                ? math.max(0f, d[cellData.TerrainIndex])
                : 0f;
            int count = Mathf.RoundToInt(lambda);
            if (count <= 0)
                count = 2;
            return math.clamp(count, 1, 3);
        }

        private void DestroyVegCellInstances(int2 offset)
        {
            var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<HexScatterCell>());
            using var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var e in entities)
            {
                if (EntityManager.GetComponentData<HexScatterCell>(e).Offset.Equals(offset))
                    EntityManager.DestroyEntity(e);
            }
        }

        // ── 笔画生命周期 ──────────────────────────────────────────

        /// <summary>笔画去重：本笔画尚未应用过任何格，或拾取到新格（int2 无 == 运算符，用 Equals）</summary>
        private bool IsNewCell(int2 offset)
            => !_lastAppliedOffset.HasValue || !_lastAppliedOffset.Value.Equals(offset);

        /// <summary>左键按下：按模式+选项开新笔画对象（删除选项不需要对象）</summary>
        private void BeginStroke()
        {
            var featureEntity = ResolveFeatureEntity();
            if (featureEntity == Entity.Null)
                return;
            var state = EntityManager.GetComponentData<HexFeatureState>(featureEntity);

            if (HexEditorToolState.Mode == HexEditorToolMode.Water)
            {
                if (HexEditorToolState.OptionIndex == 1)
                    _strokeRiver = HexFeatureEditUtil.BeginRiver(state);
                else if (HexEditorToolState.OptionIndex == 2)
                    _strokeLake = HexFeatureEditUtil.BeginLake(state);
            }
            else if (HexEditorToolState.Mode == HexEditorToolMode.Road && HexEditorToolState.OptionIndex == 1)
            {
                _strokeRoad = HexFeatureEditUtil.BeginRoad(state);
            }
        }

        /// <summary>松开：清笔画对象；一笔没涂到任何格的空河/湖/路从 state 移除（防空水体上屏）</summary>
        private void EndStrokes()
        {
            var featureEntity = ResolveFeatureEntity();
            if (featureEntity != Entity.Null)
            {
                var state = EntityManager.GetComponentData<HexFeatureState>(featureEntity);
                if (_strokeRiver != null && _strokeRiver.Cells.Count == 0)
                    state.Rivers.Remove(_strokeRiver);
                if (_strokeLake != null && _strokeLake.Cells.Count == 0)
                    state.Lakes.Remove(_strokeLake);
                if (_strokeRoad != null && _strokeRoad.Cells.Count == 0)
                    state.Roads.Remove(_strokeRoad);
            }
            _strokeRiver = null;
            _strokeLake = null;
            _strokeRoad = null;
        }

        // ── POI 放置 / 删除（Poi 模式；R 重生成后生效）───────────

        private void PlacePoi(Entity cellEntity, int2 offset, in HexCellData cell, PoiType type)
        {
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

        private Entity ResolveFeatureEntity()
        {
            return _featureQuery.IsEmpty ? Entity.Null : _featureQuery.GetSingletonEntity();
        }

        /// <summary>特征脏刷新（上帧水/路笔刷改动 → 本帧头重建）</summary>
        private void FlushFeatureMeshRefresh(HexChunkStreamingSystem streaming, ref HexMapConfigBlob blob)
        {
            if (streaming == null || !streaming.CellLookup.IsCreated)
            {
                _featureMeshDirty = true;   // 流式未就绪：留到下帧再试
                return;
            }
            var featureEntity = ResolveFeatureEntity();
            if (featureEntity == Entity.Null)
                return;
            var state = EntityManager.GetComponentData<HexFeatureState>(featureEntity);
            var featureConfig = EntityManager.GetComponentData<HexFeatureConfig>(featureEntity);
            HexFeatureEditUtil.RefreshFeatureMeshes(World, EntityManager, state, featureConfig,
                streaming.CellLookup, ref blob);
        }

        /// <summary>
        /// 三类选项上限解析：
        /// - 贴图笔刷上限：材质 albedo 数组 depth−1（涂出数组范围会触发越界采样闪烁）；
        /// - 高度上限：min(9, MaxElevation)（数字键只有 0-9）；
        /// - 植被原型数：HexMapFeatureSettings.scatterRules.Count。
        /// 结果同步进 HexEditorToolState（面板选项行按各上限生成）。
        /// </summary>
        private void ResolveLimits(in HexMapConfig config)
        {
            if (!_heightVegLimitResolved)
            {
                _heightVegLimitResolved = true;
                ref var blob = ref config.Blob.Value;
                HexEditorToolState.HeightLimit = math.min(9, blob.MaxElevation);
                HexEditorToolState.VegPrototypeCount = ResolveVegPrototypeCount();
            }

            if (_terrainLimitResolved)
                return;

            var material = config.TerrainMaterial.Value;
            if (material == null)
                return;

            if (material.GetTexture("_TerrainAlbedoArray") is Texture2DArray array)
            {
                _terrainLimitResolved = true;
                HexEditorToolState.TerrainLimit = array.depth - 1;
                if (HexEditorToolState.OptionIndex > HexEditorToolState.OptionLimit)
                    HexEditorToolState.SetOption(HexEditorToolState.OptionLimit);
            }
        }

        private int ResolveVegPrototypeCount()
        {
            if (_featureQuery.IsEmpty)
                return 0;
            var settings = EntityManager.GetComponentData<HexFeatureConfig>(
                _featureQuery.GetSingletonEntity()).Settings;
            return settings?.scatterRules?.Count ?? 0;
        }
    }
}
