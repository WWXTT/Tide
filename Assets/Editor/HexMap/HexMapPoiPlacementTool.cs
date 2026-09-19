using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HexMap.EditorTools
{
    /// <summary>
    /// POI 放置模式（计划 7.1）+ 特征 gizmo（计划 7.2），Scene 视图内完成：
    /// - 入口：Tools/HexMap/POI放置模式 切换（快捷键 P）
    /// - 仅 Play 模式可放置（Edit 无 cell 实体）；POI 写入 HexMapFeatureSettings 资产（持久）
    /// - 左键放置当前类型 POI（Undo 可撤销）；Alt+左键删除半径内最近 POI
    /// - 类型/半径：Scene 视图顶部工具条
    /// - R：写重生成请求（ResetElevation=false，保留手编地形只重跑特征）
    /// - 常显：POI 盘（泉=蓝/路=橙）；Play 中叠加河流（蓝点）/湖（水位线）/道路（黄折线）
    /// </summary>
    [InitializeOnLoad]
    public static class HexMapPoiPlacementTool
    {
        private static bool _enabled;
        private static PoiType _type = PoiType.RiverSpring;
        private static float _radius = 20f;

        /// <summary>gizmo 显示级别（计划 7.2：避免全图 gizmo 噪声）</summary>
        private enum GizmoLevel { All, POI, Rivers, Roads, None }
        private static GizmoLevel _gizmoLevel = GizmoLevel.All;

        static HexMapPoiPlacementTool()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        [MenuItem("Tools/HexMap/POI放置模式")]
        private static void ToggleMode()
        {
            _enabled = !_enabled;
            if (_enabled)
                SceneView.lastActiveSceneView?.Focus();
            Debug.Log($"[HexMap] POI 放置模式 {(_enabled ? "开" : "关")}（P 切换 / 左键放置 / Alt+左键删除 / R 重生成）");
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!_enabled)
                return;

            Handles.BeginGUI();
            GUILayout.BeginHorizontal("Toolbar");
            {
                GUILayout.Label("POI", GUILayout.Width(30));
                int sel = GUILayout.Toolbar(_type == PoiType.RiverSpring ? 0 : 1,
                    new[] { "泉水 RiverSpring", "路点 RoadNode" });
                _type = sel == 0 ? PoiType.RiverSpring : PoiType.RoadNode;
                GUILayout.Label("半径", GUILayout.Width(30));
                _radius = GUILayout.HorizontalSlider(_radius, 2f, 60f, GUILayout.Width(120));
                GUILayout.Label($"{_radius:F0}", GUILayout.Width(28));
                _gizmoLevel = (GizmoLevel)GUILayout.Toolbar((int)_gizmoLevel,
                    new[] { "All", "POI", "河湖", "路", "无" });
                if (GUILayout.Button("重生成(R)"))
                    RequestRegenerate(keepTerrain: true);
            }
            GUILayout.EndHorizontal();
            Handles.EndGUI();

            // 快捷键（Scene 视图焦点时）
            var ev = Event.current;
            if (ev.type == EventType.KeyDown)
            {
                if (ev.keyCode == KeyCode.P)
                {
                    ToggleMode();
                    ev.Use();
                    return;
                }
                if (ev.keyCode == KeyCode.R)
                {
                    RequestRegenerate(keepTerrain: true);
                    ev.Use();
                    return;
                }
            }

            DrawExistingPois();

            if (!Application.isPlaying)
            {
                Handles.BeginGUI();
                GUILayout.Label("POI 放置需 Play 模式（cell 实体不存在于 Edit）——POI 位置可先看已有盘");
                Handles.EndGUI();
                return;
            }

            // 热格预览 + 放置
            var ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
            if (TryPick(ray, out var hitPoint, out var offset))
            {
                Handles.color = _type == PoiType.RiverSpring ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.65f, 0.2f);
                Handles.DrawWireDisc(hitPoint, Vector3.up, _radius);
                Handles.Label(hitPoint + Vector3.up * 2f, $"{_type}\n({offset.x},{offset.y})");

                if (ev.type == EventType.MouseUp && ev.button == 0)
                {
                    var settings = FindSettings();
                    if (settings != null)
                    {
                        if (ev.alt)
                        {
                            // 删除半径内最近 POI
                            int nearest = -1;
                            float bestSq = _radius * _radius;
                            for (int i = 0; i < settings.pois.Count; i++)
                            {
                                float sq = math.distancesq(settings.pois[i].WorldPos, (float3)(Vector3)hitPoint);
                                if (sq < bestSq)
                                {
                                    bestSq = sq;
                                    nearest = i;
                                }
                            }
                            if (nearest >= 0)
                            {
                                Undo.RecordObject(settings, "Remove POI");
                                settings.pois.RemoveAt(nearest);
                                EditorUtility.SetDirty(settings);
                                Debug.Log($"[HexMap] 删除 POI #{nearest}");
                            }
                        }
                        else
                        {
                            Undo.RecordObject(settings, "Place POI");
                            settings.pois.Add(new HexPoiData
                            {
                                Type = _type,
                                CellOffset = offset,
                                WorldPos = hitPoint,
                                Radius = _radius,
                                Note = "",
                            });
                            EditorUtility.SetDirty(settings);
                            Debug.Log($"[HexMap] 放置 {_type} @({offset.x},{offset.y})，按 R 重生成查看效果");
                        }
                    }
                    ev.Use();
                }
            }

            DrawFeatureGizmos();
            SceneView.RepaintAll();
        }

        /// <summary>射线拾取（与编辑笔刷同一套步进逻辑）</summary>
        private static bool TryPick(Ray ray, out Vector3 hitPoint, out Unity.Mathematics.int2 offset)
        {
            hitPoint = default;
            offset = default;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var em = world.EntityManager;
            var streaming = world.GetExistingSystemManaged<HexChunkStreamingSystem>();
            if (streaming == null)
                return false;

            var configEntity = em.CreateEntityQuery(typeof(HexMapConfig)).GetSingletonEntity();
            var config = em.GetComponentData<HexMapConfig>(configEntity);
            ref var blob = ref config.Blob.Value;
            var metrics = HexMetrics.FromBlob(ref blob);

            if (HexMapCellEditUtil.PickCell(ray, em, ref blob, in metrics, streaming.CellLookup,
                    out var cellEntity, out offset))
            {
                hitPoint = em.GetComponentData<HexCellData>(cellEntity).Position;
                return true;
            }
            return false;
        }

        private static void RequestRegenerate(bool keepTerrain)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[HexMap] 需在 Play 模式执行");
                return;
            }
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var configEntity = em.CreateEntityQuery(typeof(HexMapConfig)).GetSingletonEntity();
            if (em.HasComponent<HexFeatureRegenerateRequest>(configEntity))
                em.SetComponentData(configEntity, new HexFeatureRegenerateRequest { ResetElevation = !keepTerrain });
            else
                em.AddComponentData(configEntity,
                    new HexFeatureRegenerateRequest { ResetElevation = !keepTerrain });
            Debug.Log("[HexMap] 已写入特征重生成请求");
        }

        private static HexMapFeatureSettings FindSettings()
        {
            // Play：走单例直引（最准）；兜底找场景 authoring
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var query = world.EntityManager.CreateEntityQuery(typeof(HexFeatureConfig));
                if (!query.IsEmpty)
                {
                    var s = world.EntityManager
                        .GetComponentData<HexFeatureConfig>(query.GetSingletonEntity()).Settings;
                    if (s != null)
                        return s;
                }
            }
            var authoring = Object.FindFirstObjectByType<HexMapAuthoring>();
            return authoring != null ? authoring.featureSettings : null;
        }

        // ── gizmo（计划 7.2）────────────────────────────────────────

        private static void DrawExistingPois()
        {
            if (_gizmoLevel is not (GizmoLevel.All or GizmoLevel.POI))
                return;

            var settings = FindSettings();
            if (settings == null)
                return;

            for (int i = 0; i < settings.pois.Count; i++)
            {
                var poi = settings.pois[i];
                Handles.color = poi.Type == PoiType.RiverSpring
                    ? new Color(0.3f, 0.6f, 1f, 0.9f)
                    : new Color(1f, 0.65f, 0.2f, 0.9f);
                Handles.DrawWireDisc(poi.WorldPos, Vector3.up, Mathf.Max(2f, poi.Radius));
                Handles.Label((Vector3)poi.WorldPos + Vector3.up * 1.5f, $"{poi.Type} #{i}");
            }
        }

        private static void DrawFeatureGizmos()
        {
            if (_gizmoLevel == GizmoLevel.None || _gizmoLevel == GizmoLevel.POI)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(HexFeatureState));
            if (query.IsEmpty)
                return;
            var state = em.GetComponentData<HexFeatureState>(query.GetSingletonEntity());

            if (_gizmoLevel is GizmoLevel.All or GizmoLevel.Rivers)
            {
                // 河流：河床格半透明蓝盘 @水面
                Handles.color = new Color(0.25f, 0.55f, 1f, 0.5f);
                foreach (var river in state.Rivers)
                {
                    for (int i = 0; i < river.Cells.Count; i++)
                    {
                        var p = CellCenterWorld(river.Cells[i], out bool ok);
                        if (ok)
                            Handles.DrawWireDisc(new Vector3(p.x, river.WaterY[i], p.z), Vector3.up, 3f);
                    }
                }

                // 湖：水位圈
                Handles.color = new Color(0.2f, 0.5f, 0.9f, 0.6f);
                foreach (var lake in state.Lakes)
                {
                    foreach (var c in lake.Cells)
                    {
                        var p = CellCenterWorld(c, out bool ok);
                        if (ok)
                            Handles.DrawWireDisc(new Vector3(p.x, lake.WaterY, p.z), Vector3.up, 5f);
                    }
                }
            }

            if (_gizmoLevel is GizmoLevel.All or GizmoLevel.Roads)
            {
                // 道路：黄色折线
                Handles.color = new Color(1f, 0.85f, 0.2f, 0.8f);
                foreach (var road in state.Roads)
                {
                    Vector3 prev = default;
                    bool has = false;
                    foreach (var c in road.Cells)
                    {
                        var p = CellCenterWorld(c, out bool ok);
                        if (!ok)
                            continue;
                        var v = new Vector3(p.x, p.y + 1f, p.z);
                        if (has)
                            Handles.DrawLine(prev, v);
                        prev = v;
                        has = true;
                    }
                }
            }
        }

        /// <summary>cell 中心世界坐标（含板面 y）——走 ECS 现值</summary>
        private static Vector3 CellCenterWorld(Unity.Mathematics.int2 offset, out bool ok)
        {
            ok = false;
            var world = World.DefaultGameObjectInjectionWorld;
            var streaming = world.GetExistingSystemManaged<HexChunkStreamingSystem>();
            if (streaming == null || !streaming.CellLookup.TryGetValue(offset, out var e))
                return default;
            var em = world.EntityManager;
            if (!em.HasComponent<HexCellData>(e))
                return default;
            var cell = em.GetComponentData<HexCellData>(e);
            ok = true;
            return new Vector3(cell.Position.x, cell.Position.y, cell.Position.z);
        }
    }
}
