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
    /// 职责：六工具模式/选项切换（写 HexEditorToolState，HexMapEditingSystem 读）、
    /// 特征重生成 / 植被重散布 / 确定性验证、世界存档保存与加载（HexWorldSaveService）。
    /// 快捷键：F5 保存 / F9 重载选中 / R 重生成(保留手编) / Shift+R 完整重置 / V 植被
    /// （文本输入框聚焦时全部让位）。
    ///
    /// 存档下拉：打开时自动刷新列表，选中即加载（放弃当前修改按存档重载）；
    /// 无独立刷新按钮。选项行为中文条目（数字键按序号快选，0 恒为删除/清除）。
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

        /// <summary>程序化刷新下拉列表期间抑制「选中即加载」回调</summary>
        private bool _suppressDropdownCallback;

        /// <summary>选项行缓存签名（模式+上限）——变化才重建按钮行</summary>
        private (HexEditorToolMode mode, int limit, int option) _optionRowSig;

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
            _optionRowSig = default;
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

            // 工具模式（六模式）
            UIBinder.BindButton(root, "btn-mode-terrain",
                () => HexEditorToolState.SetMode(HexEditorToolMode.Terrain));
            UIBinder.BindButton(root, "btn-mode-height",
                () => HexEditorToolState.SetMode(HexEditorToolMode.Height));
            UIBinder.BindButton(root, "btn-mode-water",
                () => HexEditorToolState.SetMode(HexEditorToolMode.Water));
            UIBinder.BindButton(root, "btn-mode-road",
                () => HexEditorToolState.SetMode(HexEditorToolMode.Road));
            UIBinder.BindButton(root, "btn-mode-veg",
                () => HexEditorToolState.SetMode(HexEditorToolMode.Vegetation));
            UIBinder.BindButton(root, "btn-mode-poi",
                () => HexEditorToolState.SetMode(HexEditorToolMode.Poi));

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

            var dropdown = root.Q<DropdownField>("dropdown-loads");
            // 打开即刷新列表（保当前值，不触发加载）——替代旧「刷新」按钮
            dropdown.RegisterCallback<PointerDownEvent>(_ => RefreshSaveList());
            // 选中即加载（放弃当前未保存修改，按存档重载）
            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (_suppressDropdownCallback || string.IsNullOrEmpty(evt.newValue))
                    return;
                StartFlow(LoadFlow());
            });

            RefreshSaveList();
            RefreshHighlights();
        }

        // ── 选项行（随模式重建；中文条目，数字键按序号快选）────────

        /// <summary>当前模式的选项条目（index 与数字键/HexEditorToolState.OptionIndex 对应）</summary>
        private static (int index, string label)[] OptionEntries(HexEditorToolMode mode)
        {
            switch (mode)
            {
                case HexEditorToolMode.Terrain:
                {
                    var entries = new (int, string)[HexEditorToolState.TerrainLimit + 1];
                    for (int i = 0; i <= HexEditorToolState.TerrainLimit; i++)
                    {
                        string name = i < HexEditorToolState.TerrainLayerNames.Length
                            ? HexEditorToolState.TerrainLayerNames[i]
                            : $"层";
                        entries[i] = (i, $"{i}·{name}");
                    }
                    return entries;
                }

                case HexEditorToolMode.Height:
                {
                    var entries = new (int, string)[HexEditorToolState.HeightLimit + 1];
                    for (int i = 0; i <= HexEditorToolState.HeightLimit; i++)
                        entries[i] = (i, $"高度{i}");
                    return entries;
                }

                case HexEditorToolMode.Water:
                    return new[] { (0, "0·清除水"), (1, "1·河流"), (2, "2·湖泊") };

                case HexEditorToolMode.Road:
                    return new[] { (0, "0·清路"), (1, "1·道路") };

                case HexEditorToolMode.Poi:
                    return new[] { (0, "0·删POI"), (1, "1·泉水"), (2, "2·路点") };

                case HexEditorToolMode.Vegetation:
                {
                    var rules = ResolveScatterRules();
                    int count = rules?.Count ?? HexEditorToolState.VegPrototypeCount;
                    var entries = new (int, string)[count + 1];
                    entries[0] = (0, "0·清除");
                    for (int i = 1; i <= count; i++)
                    {
                        string name = (rules != null && i - 1 < rules.Count && !string.IsNullOrEmpty(rules[i - 1].name))
                            ? rules[i - 1].name
                            : $"原型{i}";
                        entries[i] = (i, $"{i}·{name}");
                    }
                    return entries;
                }

                default:
                    return System.Array.Empty<(int, string)>();
            }
        }

        /// <summary>选项行标题（随模式）</summary>
        private static string OptionTitle(HexEditorToolMode mode) => mode switch
        {
            HexEditorToolMode.Terrain => "贴图（数字键快选，拖动涂刷）",
            HexEditorToolMode.Height => "高度（数字键 0-9 直接设定，拖动应用）",
            HexEditorToolMode.Water => "水（拖动画河/湖，0 清除）",
            HexEditorToolMode.Road => "路（拖动画路，0 清除）",
            HexEditorToolMode.Vegetation => "植被（拖动放置，0 清除）",
            HexEditorToolMode.Poi => "POI（拖动放置，R 重生成生效）",
            _ => "选项（数字键快选）",
        };

        private void RebuildOptionRow()
        {
            var row = _root?.Q<VisualElement>("option-row");
            if (row == null)
                return;

            row.Clear();
            foreach (var (index, label) in OptionEntries(HexEditorToolState.Mode))
            {
                int i = index;
                var b = new Button(() => HexEditorToolState.SetOption(i))
                {
                    text = label,
                    name = $"btn-option-{i}",
                };
                b.AddToClassList("btn--mini");
                row.Add(b);
            }
            UIBinder.SetText(_root, "lbl-option-title", OptionTitle(HexEditorToolState.Mode));
        }

        /// <summary>模式/选项按钮高亮刷新（Changed 事件 + 每帧轮询双触发：
        /// 贴图数组层数/植被规则数是编辑系统晚解析的，轮询兜底补建按钮行）</summary>
        private void RefreshHighlights()
        {
            if (_root == null)
                return;

            Mode(HexEditorToolMode.Terrain, "btn-mode-terrain");
            Mode(HexEditorToolMode.Height, "btn-mode-height");
            Mode(HexEditorToolMode.Water, "btn-mode-water");
            Mode(HexEditorToolMode.Road, "btn-mode-road");
            Mode(HexEditorToolMode.Vegetation, "btn-mode-veg");
            Mode(HexEditorToolMode.Poi, "btn-mode-poi");

            var sig = (HexEditorToolState.Mode, HexEditorToolState.OptionLimit, HexEditorToolState.OptionIndex);
            if (!sig.Equals(_optionRowSig))
            {
                _optionRowSig = sig;
                RebuildOptionRow();
            }
            foreach (var (index, _) in OptionEntries(HexEditorToolState.Mode))
            {
                var b = _root.Q<Button>($"btn-option-{index}");
                b?.EnableInClassList("btn--primary", index == HexEditorToolState.OptionIndex);
            }
        }

        private void Mode(HexEditorToolMode mode, string buttonName)
            => _root.Q<Button>(buttonName)?.EnableInClassList("btn--primary",
                HexEditorToolState.Mode == mode);

        private void RefreshSaveList()
        {
            var dd = _root?.Q<DropdownField>("dropdown-loads");
            if (dd == null)
                return;

            _suppressDropdownCallback = true;
            string current = dd.value;
            dd.choices = HexWorldSerializer.ListSaveNames();
            // 保持当前选中（仍在列表内）——SetValueWithoutNotify 不触发加载
            dd.SetValueWithoutNotify(dd.choices.Contains(current) ? current : null);
            _suppressDropdownCallback = false;
        }

        // ── 快捷键 ────────────────────────────────────────────────

        private void Update()
        {
            if (HexEditorToolState.TextInputFocused)
                return;   // 输入框聚焦：快捷键让位

            RefreshHighlights();   // 晚解析上限（贴图层数/规则数）兜底补建选项行

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
            if (path != null)
            {
                // 新存档成为当前选中（不触发加载——就是刚存的世界）
                var dd = _root?.Q<DropdownField>("dropdown-loads");
                _suppressDropdownCallback = true;
                dd?.SetValueWithoutNotify(save.name);
                _suppressDropdownCallback = false;
            }
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

            Status("恢复中…（放弃当前未保存修改）");
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

        /// <summary>植被选项行名称用：解析散布规则表（世界未就绪返回 null）</summary>
        private static List<HexScatterRule> ResolveScatterRules()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return null;
            var em = world.EntityManager;
            var q = em.CreateEntityQuery(typeof(HexFeatureConfig));
            if (q.IsEmpty)
                return null;
            return em.GetComponentData<HexFeatureConfig>(q.GetSingletonEntity()).Settings?.scatterRules;
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
