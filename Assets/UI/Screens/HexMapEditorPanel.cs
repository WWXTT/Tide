using System.Collections;
using System.Collections.Generic;
using HexMap;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 游戏内 HexMap 世界编辑器面板（常驻右上角工具条，非 UIScreen——UIManager 是
    /// 全屏栈式会清根，不适合常驻面板；本类镜像 UIBootstrap 的装配方式）。
    ///
    /// 职责：工具模式/笔刷切换（写 HexEditorToolState，HexMapEditingSystem 读）、
    /// 特征重生成 / 植被重散布 / 确定性验证、世界存档保存与加载（HexWorldSaveService）。
    /// 快捷键：F5 保存 / F9 加载 / R 重生成(保留手编) / Shift+R 完整重置 / V 植被
    /// （文本输入框聚焦时全部让位）。
    ///
    /// 指针穿透：场景无 EventSystem，旧 Input 不会被 UI Toolkit 吞掉——
    /// 用 PointerEnter/Leave 维护 HexEditorToolState.PointerOverUI，编辑系统据此避让。
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class HexMapEditorPanel : MonoBehaviour
    {
        private const string PanelSettingsResourcePath = "SynergyPanelSettings";

        private PanelRenderer _renderer;
        private VisualElement _root;
        private VisualElement _panel;
        private int _panelVersion = -1;
        private bool _collapsed;
        private Coroutine _flow;

        // ── 装配（UIBootstrap 同款）──────────────────────────────

        private void OnEnable()
        {
            _renderer = GetComponent<PanelRenderer>();

            if (_renderer.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
                if (panelSettings == null)
                {
                    Debug.LogError(
                        $"[HexEditorPanel] 找不到 PanelSettings: Resources/{PanelSettingsResourcePath}");
                    return;
                }
                _renderer.panelSettings = panelSettings;
            }

            // 场景里的 PanelRenderer.sourceAsset 留空（省去手写 UXML 资产引用）——
            // 这里从 Resources 指定 visualTreeAsset，装载后触发 reload 回调
            if (_renderer.visualTreeAsset == null)
            {
                var vta = Resources.Load<VisualTreeAsset>("UXML/HexMapEditorPanel");
                if (vta == null)
                {
                    Debug.LogError("[HexEditorPanel] 找不到 UXML: Resources/UXML/HexMapEditorPanel.uxml");
                    return;
                }
                _renderer.visualTreeAsset = vta;
            }

            _renderer.RegisterUIReloadCallback(OnUIReload);
            HexEditorToolState.Changed += RefreshHighlights;
        }

        private void OnDisable()
        {
            HexEditorToolState.Changed -= RefreshHighlights;
            if (_renderer != null)
                _renderer.UnregisterUIReloadCallback(OnUIReload);
            HexEditorToolState.PointerOverUI = false;
            HexEditorToolState.TextInputFocused = false;
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (version == _panelVersion)
                return;   // 双发防重复初始化
            _panelVersion = version;
            _root = root;
            Bind(root);
        }

        // ── 控件接线 ───────────────────────────────────────────────

        private void Bind(VisualElement root)
        {
            _panel = root.Q<VisualElement>("hex-editor-panel");

            // 指针穿透桥：悬停面板 = 编辑系统避让（面板外照常涂地图）
            _panel.RegisterCallback<PointerEnterEvent>(_ => HexEditorToolState.PointerOverUI = true);
            _panel.RegisterCallback<PointerLeaveEvent>(_ => HexEditorToolState.PointerOverUI = false);

            // 折叠
            UIBinder.BindButton(root, "btn-collapse", () =>
            {
                _collapsed = !_collapsed;
                root.Q<VisualElement>("content")?.SetDisplay(!_collapsed);
                _panel.Q<Button>("btn-collapse").text = _collapsed ? "+" : "—";
            });

            // 工具模式
            UIBinder.BindButton(root, "btn-mode-terrain",
                () => HexEditorToolState.SetMode(HexEditorToolMode.Terrain));
            UIBinder.BindButton(root, "btn-mode-spring",
                () => HexEditorToolState.SetMode(HexEditorToolMode.PlaceSpring));
            UIBinder.BindButton(root, "btn-mode-roadnode",
                () => HexEditorToolState.SetMode(HexEditorToolMode.PlaceRoadNode));
            UIBinder.BindButton(root, "btn-mode-delpoi",
                () => HexEditorToolState.SetMode(HexEditorToolMode.DeletePoi));

            // POI 半径
            UIBinder.BindField(root.Q<Slider>("slider-poi-radius"), HexEditorToolState.PoiRadius,
                v => HexEditorToolState.PoiRadius = v);

            // 特征操作
            UIBinder.BindButton(root, "btn-regen-features", () => StartFlow(RegenFlow(false)));
            UIBinder.BindButton(root, "btn-regen-full", () => StartFlow(RegenFlow(true)));
            UIBinder.BindButton(root, "btn-regen-vegetation", RegenerateVegetation);
            UIBinder.BindButton(root, "btn-verify", () => StartFlow(VerifyFlow()));

            // 存档
            var nameField = root.Q<TextField>("field-save-name");
            nameField.RegisterCallback<FocusEvent>(_ => HexEditorToolState.TextInputFocused = true);
            nameField.RegisterCallback<FocusOutEvent>(_ => HexEditorToolState.TextInputFocused = false);
            UIBinder.BindButton(root, "btn-save", () => StartFlow(SaveFlow()));
            UIBinder.BindButton(root, "btn-load", () => StartFlow(LoadFlow()));
            UIBinder.BindButton(root, "btn-refresh", RefreshSaveList);

            RebuildBrushRow();
            RefreshSaveList();
            RefreshHighlights();
        }

        /// <summary>笔刷地形按钮行（按材质贴图数组层数动态生成）</summary>
        private void RebuildBrushRow()
        {
            var row = _root?.Q<VisualElement>("brush-row");
            if (row == null)
                return;
            row.Clear();
            for (int i = 0; i <= HexEditorToolState.TerrainLimit; i++)
            {
                int index = i;
                var b = new Button(() => HexEditorToolState.SetBrushTerrain(index))
                {
                    text = i.ToString(),
                    name = $"btn-brush-{i}",
                };
                b.AddToClassList("btn--mini");
                row.Add(b);
            }
        }

        /// <summary>模式/笔刷按钮高亮刷新（HexEditorToolState.Changed 触发）</summary>
        private void RefreshHighlights()
        {
            if (_root == null)
                return;

            Mode(HexEditorToolMode.Terrain, "btn-mode-terrain");
            Mode(HexEditorToolMode.PlaceSpring, "btn-mode-spring");
            Mode(HexEditorToolMode.PlaceRoadNode, "btn-mode-roadnode");
            Mode(HexEditorToolMode.DeletePoi, "btn-mode-delpoi");

            if (HexEditorToolState.TerrainLimit != _lastBrushLimit)
            {
                _lastBrushLimit = HexEditorToolState.TerrainLimit;
                RebuildBrushRow();   // 编辑系统解析出数组层数后补建按钮
            }
            for (int i = 0; i <= HexEditorToolState.TerrainLimit; i++)
            {
                var b = _root.Q<Button>($"btn-brush-{i}");
                b?.EnableInClassList("btn--primary", i == HexEditorToolState.BrushTerrainIndex);
            }
        }

        private int _lastBrushLimit = -1;

        private void Mode(HexEditorToolMode mode, string buttonName)
            => _root.Q<Button>(buttonName)?.EnableInClassList("btn--primary",
                HexEditorToolState.Mode == mode);

        private void RefreshSaveList()
        {
            var dd = _root?.Q<DropdownField>("dropdown-loads");
            if (dd == null)
                return;
            dd.choices = HexWorldSerializer.ListSaveNames();
            if (dd.index < 0 || dd.index >= dd.choices.Count)
                dd.index = dd.choices.Count > 0 ? 0 : -1;
        }

        // ── 快捷键 ────────────────────────────────────────────────

        private void Update()
        {
            if (HexEditorToolState.TextInputFocused)
                return;   // 输入框聚焦：快捷键让位

            if (Input.GetKeyDown(KeyCode.F5))
                StartFlow(SaveFlow());
            else if (Input.GetKeyDown(KeyCode.F9))
                StartFlow(LoadFlow());
            else if (Input.GetKeyDown(KeyCode.R))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                StartFlow(RegenFlow(shift));
            }
            else if (Input.GetKeyDown(KeyCode.V))
                RegenerateVegetation();
        }

        // ── 操作流（协程：等待世界/全图就绪后执行）────────────────

        private void StartFlow(IEnumerator flow)
        {
            if (_flow != null)
                StopCoroutine(_flow);
            _flow = StartCoroutine(flow);
        }

        private void RegenerateVegetation()
        {
            var world = ResolveWorld();
            if (world == null)
                return;
            var q = world.EntityManager.CreateEntityQuery(typeof(HexFeatureState));
            if (q.IsEmpty)
            {
                Status("特征系统未安装");
                return;
            }
            world.EntityManager.GetComponentData<HexFeatureState>(q.GetSingletonEntity()).VegetationDirty = true;
            Status("植被重散布已请求");
        }

        private IEnumerator RegenFlow(bool full)
        {
            string fail = null;
            yield return WaitForReady(w => fail = w);
            if (fail != null)
            {
                Status(fail);
                yield break;
            }

            var world = ResolveWorld();
            world.GetExistingSystemManaged<HexChunkStreamingSystem>().EnsureAllLoaded();
            HexFeatureCommitUtil.RequestRegenerate(world, full);
            Status(full ? "已请求完整重置（高程回噪声 + 特征重跑）" : "已请求重生成（保留手编地形）");
        }

        private IEnumerator SaveFlow()
        {
            string fail = null;
            yield return WaitForReady(w => fail = w);
            if (fail != null)
            {
                Status(fail);
                yield break;
            }

            Status("采集中…");
            yield return EnsureAllLoadedAndWait();

            var world = ResolveWorld();
            var save = HexWorldSaveService.Capture(world);
            if (save == null)
            {
                Status("采集失败（详见 Console）");
                yield break;
            }

            save.name = SaveName();
            string path = HexWorldSerializer.Save(save);
            Status(path != null ? $"已保存：{path}" : "保存失败（详见 Console）");
            RefreshSaveList();
        }

        private IEnumerator LoadFlow()
        {
            var world0 = ResolveWorld();
            if (world0 == null)
            {
                Status("等待地图安装…");
                yield break;
            }

            string name = SelectedSave();
            if (string.IsNullOrEmpty(name))
            {
                Status("没有可加载的存档");
                RefreshSaveList();
                yield break;
            }

            var save = HexWorldSerializer.Load(name);
            if (save == null)
            {
                Status($"读档失败：{name}");
                yield break;
            }

            // 指纹校验（不符仅警告——高程全量以存档为准）
            string current = HexWorldFingerprint.Compute(CurrentSettings(world0));
            if (current != save.settingsFingerprint)
                Debug.LogWarning($"[HexWorld] 配置指纹不符（存档 {save.settingsFingerprint} / 当前 {current}），" +
                                 "继续加载：Detail 扰动类数据可能有视觉级偏差");

            Status("恢复中…");
            yield return EnsureAllLoadedAndWait();

            var world = ResolveWorld();
            HexWorldSaveService.Apply(world, save);   // 同步单步：系统组观察不到半状态
            Status($"已加载：{save.name}");
            RefreshSaveList();
        }

        private IEnumerator VerifyFlow()
        {
            string fail = null;
            yield return WaitForReady(w => fail = w);
            if (fail != null)
            {
                Status(fail);
                yield break;
            }

            var world = ResolveWorld();
            world.GetExistingSystemManaged<HexChunkStreamingSystem>().EnsureAllLoaded();

            Status("确定性验证 1/2 生成中…");
            uint h1;
            yield return WaitForGeneration();
            h1 = CurrentHash();

            Status("确定性验证 2/2 生成中…");
            yield return WaitForGeneration();
            uint h2 = CurrentHash();

            if (h1 == h2 && h1 != 0)
                Debug.Log($"[HexMap] 确定性验证通过：两次生成哈希相等 0x{h1:X8}");
            else if (h1 == 0 || h2 == 0)
                Debug.LogWarning("[HexMap] 确定性验证中断（状态不可用）");
            else
                Debug.LogError($"[HexMap] 确定性验证失败：哈希不等 0x{h1:X8} vs 0x{h2:X8}");
            Status("确定性验证完成（详见 Console）");
        }

        /// <summary>请求一次重生成并等待完成（GenerationSerial 自增）</summary>
        private IEnumerator WaitForGeneration()
        {
            var world = ResolveWorld();
            int target = CurrentSerial() + 1;
            HexFeatureCommitUtil.RequestRegenerate(world, false);
            while (CurrentSerial() < target)
                yield return null;
        }

        // ── 世界解析 / 等待 ────────────────────────────────────────

        /// <summary>等 World/配置/特征单例全部就绪（每帧轮询；最多 30 秒超时）</summary>
        private IEnumerator WaitForReady(System.Action<string> onFail)
        {
            float timeout = Time.realtimeSinceStartup + 30f;
            while (ResolveWorld() == null)
            {
                if (Time.realtimeSinceStartup > timeout)
                {
                    onFail("等待地图安装超时（HexMapAuthoring 没进场景？）");
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>全图补建 + 等 TerrainPending 清零（新 cell 高程填完后才能安全读）</summary>
        private IEnumerator EnsureAllLoadedAndWait()
        {
            var world = ResolveWorld();
            var streaming = world.GetExistingSystemManaged<HexChunkStreamingSystem>();
            streaming.EnsureAllLoaded();

            var em = world.EntityManager;
            var pending = em.CreateEntityQuery(ComponentType.ReadOnly<TerrainPending>());
            int expected = ExpectedCells(world);
            while (pending.CalculateEntityCount() > 0 || streaming.CellLookup.Count < expected)
                yield return null;
        }

        private World ResolveWorld()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return null;
            if (world.EntityManager.CreateEntityQuery(typeof(HexMapConfig)).IsEmpty)
                return null;
            return world;
        }

        private int ExpectedCells(World world)
        {
            var em = world.EntityManager;
            var configEntity = em.CreateEntityQuery(typeof(HexMapConfig)).GetSingletonEntity();
            ref var blob = ref em.GetComponentData<HexMapConfig>(configEntity).Blob.Value;
            return blob.CellCount.x * blob.CellCount.y;
        }

        private HexFeatureState FeatureState(World world)
        {
            var q = world.EntityManager.CreateEntityQuery(typeof(HexFeatureState));
            return q.IsEmpty ? null : world.EntityManager.GetComponentData<HexFeatureState>(q.GetSingletonEntity());
        }

        private HexMapFeatureSettings CurrentSettings(World world)
        {
            var q = world.EntityManager.CreateEntityQuery(typeof(HexFeatureConfig));
            return q.IsEmpty ? null : world.EntityManager.GetComponentData<HexFeatureConfig>(q.GetSingletonEntity()).Settings;
        }

        private int CurrentSerial() => FeatureState(ResolveWorld())?.GenerationSerial ?? -1;

        private uint CurrentHash()
        {
            var state = FeatureState(ResolveWorld());
            return state == null ? 0 : HexFeatureCommitUtil.HashState(state);
        }

        // ── 小工具 ────────────────────────────────────────────────

        private string SaveName()
        {
            var f = _root?.Q<TextField>("field-save-name");
            var s = f?.value?.Trim();
            return string.IsNullOrEmpty(s) ? "World1" : s;
        }

        private string SelectedSave()
        {
            var dd = _root?.Q<DropdownField>("dropdown-loads");
            return dd != null && !string.IsNullOrEmpty(dd.value) ? dd.value : SaveName();
        }

        private void Status(string message)
        {
            Debug.Log($"[HexEditor] {message}");
            UIBinder.SetText(_root, "lbl-status", message);
        }

        // ── POI gizmo（Scene 视图开发辅助；打包后 Game 视图不显示——开发期工具可接受）──

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || HexPoiRuntime.Pois == null)
                return;
            foreach (var p in HexPoiRuntime.Pois)
            {
                Gizmos.color = p.Type == PoiType.RiverSpring
                    ? new Color(0.3f, 0.6f, 1f, 0.9f)
                    : new Color(1f, 0.65f, 0.2f, 0.9f);
            }
        }
    }

    /// <summary>VisualElement 显隐扩展（UI Toolkit 无 SetActive）</summary>
    internal static class VisualElementDisplayExtensions
    {
        public static void SetDisplay(this VisualElement e, bool visible)
            => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
