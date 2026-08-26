#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace EntityHierarchy
{
    /// <summary>
    /// Scene 视图点选 ECS Entity（像素级拾取）。
    ///
    /// 参考 io.github.jonasdem.entityselection（Entities 0.11 时代基于 RenderMesh
    /// 的拾取方案），适配 Entities 1.4 / Entities Graphics / URP：
    /// 1. 收集所有 World 中带 MaterialMeshInfo + LocalToWorld 的可渲染 entity，
    ///    通过 EntitiesGraphicsSystem（运行时注册）或 RenderMeshArray（烘焙静态下标）
    ///    还原出 UnityEngine.Mesh
    /// 2. 用 Unlit 纯色 shader（EntityPickShader）把每个 entity 的 mesh 画进离屏 RT，
    ///    每个 entity 一个唯一 ID（候选列表下标 +1，0 保留给背景）。
    ///    ID 用 Vector 属性传入（而非 Color）：linear 色彩空间下引擎会对 Color
    ///    属性做 gamma 转换，破坏按字节编码的 ID；Vector 原样直达
    /// 3. 读回鼠标位置的像素颜色，反查出 entity，
    ///    走 EntitySelectionBridge 选中 → Inspector / 层级面板高亮 / Gizmo 自动联动
    ///
    /// 触发条件（尽量不干扰 Scene 原生交互）：
    /// - 左键且无修饰键
    /// - 该点没有 GameObject（有 GameObject 时让原生点击优先）
    /// - MouseDown 立即选中；MouseUp 时吞掉事件，防止 Scene 默认的
    ///   「点击空白处取消选择」清掉刚选中的 entity；拖框（位移超阈值）不受影响
    /// </summary>
    [InitializeOnLoad]
    internal static class EntityScenePicking
    {
        /// <summary>按下到抬起位移超过该值（平方）视为拖框/拖动，交回默认处理</summary>
        private const float ClickDragThresholdSq = 25f;

        private static readonly int IdProp = Shader.PropertyToID("_Id");

        private static Vector2 _mouseDownPos;
        private static bool _pickedOnDown;

        private static RenderTexture _pickRT;
        private static Texture2D _pixelTex;
        private static Material _pickMaterial;
        private static MaterialPropertyBlock _mpb;

        // 每次拾取重建（点击频率触发，无需缓存）
        private static readonly List<World> _worlds = new List<World>();
        private static readonly List<Entity> _entities = new List<Entity>();
        private static readonly List<Mesh> _meshes = new List<Mesh>();
        private static readonly List<Matrix4x4> _matrices = new List<Matrix4x4>();

        static EntityScenePicking()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        static void Cleanup()
        {
            SceneView.duringSceneGui -= OnSceneGUI;

            if (_pickRT != null)
            {
                _pickRT.Release();
                Object.DestroyImmediate(_pickRT);
                _pickRT = null;
            }
            if (_pixelTex != null)
            {
                Object.DestroyImmediate(_pixelTex);
                _pixelTex = null;
            }
            if (_pickMaterial != null)
            {
                Object.DestroyImmediate(_pickMaterial);
                _pickMaterial = null;
            }
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            var e = Event.current;
            if (e == null)
                return;

            if (e.type == EventType.MouseDown)
            {
                _pickedOnDown = false;

                // 只处理无修饰键的左键
                if (e.button != 0 || e.modifiers != EventModifiers.None)
                    return;

                _mouseDownPos = e.mousePosition;

                // 该点命中 GameObject 时让原生选择优先
                if (HandleUtility.PickGameObject(e.mousePosition, out _))
                    return;

                if (!TryPick(sceneView, e.mousePosition, out var world, out var entity))
                    return;

                // 选中后 Inspector / EntityHierarchy 面板 / EntitySceneGizmos 通过
                // Selection.selectionChanged 自动联动，这里只负责改 Selection
                if (EntitySelectionBridge.SelectEntity(world, entity))
                {
                    _pickedOnDown = true;
                    sceneView.Repaint();
                }
                return;
            }

            if (e.type != EventType.MouseUp || e.button != 0)
                return;

            // 本次按下选中了 entity 且抬起位移在点击阈值内 → 吞掉 MouseUp，
            // 阻止 Scene 默认的「点击空白处取消选择」清掉刚选中的 entity；
            // 拖框（位移超阈值）不吞，正常交回默认处理
            bool pickedThisClick = _pickedOnDown;
            _pickedOnDown = false;
            if (pickedThisClick &&
                (e.mousePosition - _mouseDownPos).sqrMagnitude <= ClickDragThresholdSq)
            {
                e.Use();
            }
        }

        /// <summary>
        /// 像素拾取主流程：收集候选 → 离屏渲染编码色 → 读像素反查 entity。
        /// </summary>
        static bool TryPick(SceneView sceneView, Vector2 guiPos, out World world, out Entity entity)
        {
            world = null;
            entity = Entity.Null;

            var camera = sceneView.camera;
            var material = PickMaterial;
            if (camera == null || material == null)
                return false;

            CollectRenderableEntities();
            if (_entities.Count == 0)
                return false;

            // 离屏 RT 与 Scene 相机同分辨率；显式 UNorm（非 sRGB）格式，
            // 保证 shader 输出的字节编码 ID 写入、读回全程无色彩空间转换
            int width = camera.pixelWidth;
            int height = camera.pixelHeight;
            if (width <= 0 || height <= 0)
                return false;

            if (_pickRT == null || _pickRT.width != width || _pickRT.height != height)
            {
                if (_pickRT != null)
                {
                    _pickRT.Release();
                    Object.DestroyImmediate(_pickRT);
                }
                var desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24)
                {
                    graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    sRGB = false,
                    msaaSamples = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                };
                _pickRT = new RenderTexture(desc) { filterMode = FilterMode.Point };
            }

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            var cmd = new CommandBuffer { name = "EntityHierarchy Entity Picking" };
            cmd.SetRenderTarget(_pickRT);
            cmd.ClearRenderTarget(true, true, Color.clear);
            cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

            for (int i = 0; i < _entities.Count; i++)
            {
                _mpb.SetVector(IdProp, IndexToVector(i + 1));
                var mesh = _meshes[i];
                int subMeshCount = Math.Max(1, mesh.subMeshCount);
                for (int s = 0; s < subMeshCount; s++)
                    cmd.DrawMesh(mesh, _matrices[i], material, s, 0, _mpb);
            }

            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Dispose();

            // 鼠标 GUI 坐标 → 相机 viewport → RT 像素。
            // 经 viewport 归一化天然抵消 cameraRect 偏移与 HiDPI 缩放差异。
            // 若实测拾取点垂直镜像（点击上方却选中下方），把 viewport.y 翻转为 1-y 即可
            var screenPx = HandleUtility.GUIPointToScreenPixelCoordinate(guiPos);
            var viewport = camera.ScreenToViewportPoint(screenPx);
            if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                return false; // 点在 Scene 渲染区外（工具栏 / Overlay 上）

            int px = Mathf.Clamp((int)(viewport.x * _pickRT.width), 0, _pickRT.width - 1);
            int py = Mathf.Clamp((int)(viewport.y * _pickRT.height), 0, _pickRT.height - 1);

            if (_pixelTex == null)
                _pixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);

            var prevActive = RenderTexture.active;
            RenderTexture.active = _pickRT;
            _pixelTex.ReadPixels(new Rect(px, py, 1, 1), 0, 0, false);
            _pixelTex.Apply(false);
            RenderTexture.active = prevActive;

            // 纹理为 Linear RGBA32 且编码色精确，Color→Color32 量化无损
            int index = ColorToIndex(_pixelTex.GetPixel(0, 0));
            if (index <= 0 || index > _entities.Count)
                return false;

            world = _worlds[index - 1];
            entity = _entities[index - 1];
            return world != null && world.IsCreated && world.EntityManager.Exists(entity);
        }

        /// <summary>
        /// 收集所有 World 中当前可渲染的 entity（Mesh + 世界矩阵）。
        /// </summary>
        static void CollectRenderableEntities()
        {
            _worlds.Clear();
            _entities.Clear();
            _meshes.Clear();
            _matrices.Clear();

            foreach (var world in World.All)
            {
                if (world == null || !world.IsCreated)
                    continue;

                var egs = world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                if (egs == null)
                    continue;

                var em = world.EntityManager;
                var query = em.CreateEntityQuery(
                    ComponentType.ReadOnly<MaterialMeshInfo>(),
                    ComponentType.ReadOnly<LocalToWorld>());
                using var entities = query.ToEntityArray(Allocator.Temp);

                foreach (var entity in entities)
                {
                    if (!em.Exists(entity))
                        continue;
                    if (!em.IsComponentEnabled<MaterialMeshInfo>(entity))
                        continue;

                    var mmi = em.GetComponentData<MaterialMeshInfo>(entity);
                    var mesh = ResolveMesh(em, egs, entity, mmi);
                    if (mesh == null)
                        continue;

                    _worlds.Add(world);
                    _entities.Add(entity);
                    _meshes.Add(mesh);
                    _matrices.Add((Matrix4x4)em.GetComponentData<LocalToWorld>(entity).Value);
                }
            }
        }

        /// <summary>
        /// 从 MaterialMeshInfo 还原 UnityEngine.Mesh：
        /// - Mesh >= 0：运行时 RegisterMesh 注册的 BatchMeshID（HexMap cell 即此路径）
        /// - Mesh < 0：RenderMeshArray 静态数组下标（烘焙实体），查 entity 自带的共享组件
        /// </summary>
        static Mesh ResolveMesh(EntityManager em, EntitiesGraphicsSystem egs, Entity entity,
            in MaterialMeshInfo mmi)
        {
            if (mmi.Mesh >= 0)
                return egs.GetMesh(mmi.MeshID);

            // Entities 1.x 中 HasComponent<T> 同样适用于托管共享组件
            if (em.HasComponent<RenderMeshArray>(entity))
            {
                var array = em.GetSharedComponentManaged<RenderMeshArray>(entity);
                if (array != null)
                    return array.GetMesh(mmi);
            }
            return null;
        }

        static Material PickMaterial
        {
            get
            {
                if (_pickMaterial != null)
                    return _pickMaterial;

                // 注意：不能回退到 Unlit/Color 之类内置 shader —— 它们的颜色属性是
                // _Color（Color 类型，会被 gamma 转换），收不到本工具注入的 _Id
                var shader = Shader.Find("Hidden/EntityHierarchy/EntityPick");
                if (shader == null)
                {
                    Debug.LogWarning("[EntityHierarchy] 找不到拾取用 shader（EntityPickShader 未导入？）");
                    return null;
                }

                _pickMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return _pickMaterial;
            }
        }

        /// <summary>
        /// 候选下标 → 唯一 ID（按字节拆到 RGBA，各自归一化到 0-1）。
        /// 0（全零）保留给背景。经 UNorm RT 量化后字节可精确还原。
        /// </summary>
        static Vector4 IndexToVector(int index) => new Vector4(
            (index & 0xFF) / 255f,
            ((index >> 8) & 0xFF) / 255f,
            ((index >> 16) & 0xFF) / 255f,
            ((index >> 24) & 0xFF) / 255f);

        static int ColorToIndex(Color32 c) => c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
    }
}
#endif
