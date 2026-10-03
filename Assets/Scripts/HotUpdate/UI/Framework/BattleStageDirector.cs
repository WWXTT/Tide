using System;
using Cysharp.Threading.Tasks;
using HexMap;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 战场舞台导演（2026-10-03）：主菜单黑洞 ↔ 战场棋盘的可见性与进出场过渡。
    ///
    /// 场景约定（Main.unity）：
    /// - 黑洞 = 相机下子物体 "BlackHole"（自动跟随相机，勿移动/旋转）；
    /// - 主光源 = "Directional Light"——菜单俯角 x=-20°（黑洞氛围），战场高角 x=45°（棋盘日照）；
    /// - 相机角度在战场可自由摆位（黑洞随相机走）；UI 全程不挡世界相机（战场底图透明）。
    ///
    /// 进战场四步（2026-10-03 用户定案）：
    /// ① 主光源 x 压到 -20°（0s） ② 视界 _HorizonScale 0.02→0.2（0.5s）吞屏后隐藏黑洞
    /// ③ HexMapAuthoring 装载棋盘（现成生成管线，不改逻辑） ④ 主光源 x -20°→45°（战场日出）。
    ///
    /// 挂点：UIManager.Activate 统一分发舞台态（主菜单=黑洞态 / 其它屏隐藏黑洞；
    /// 战场装台由 BattleScreen.OnEnter 驱动，时序含棋盘装载）。静态缓存场景引用 +
    /// 菜单相机位姿快照（吞屏前抓取，回菜单还原）。
    /// </summary>
    public static class BattleStageDirector
    {
        // ---- 过渡参数（①②为定案值；④时长未定案取 1.2s，可调） ----
        private const float SwallowSeconds = 0.5f;  // ② 视界膨胀时长
        private const float HorizonFrom = 0.02f;    // ② 起点（= BlackHole.mat 烘焙的菜单常态）
        private const float HorizonTo = 0.2f;       // ② 终点（shader 属性上限，全屏吞没）
        private const float MenuLightX = -20f;      // ① 菜单/过渡俯角
        private const float BattleLightX = 45f;     // ④ 战场高角
        private const float LightUpSeconds = 1.2f;  // ④ 日出时长

        private const string SettingsAddress = "BattleHexMapSettings";
        private const string SettingsAssetPath = "Assets/Art/HexMap/BattleHexMapSettings.asset";

        // ---- 占位标记（2026-10-03 绑定可视化：双方单位行/地牌行，战场 3D 重做前肉眼核对抽象区↔实际格） ----
        private static readonly Color MarkerColorP1 = new Color(0.29f, 0.50f, 0.83f); // 蓝=己方
        private static readonly Color MarkerColorP2 = new Color(0.83f, 0.35f, 0.35f); // 红=对方

        private static readonly int HorizonId = Shader.PropertyToID("_HorizonScale");

        private static Renderer _blackHole;          // 相机下的黑洞球体（MeshRenderer）
        private static Material _blackHoleMat;       // 实例材质（renderer.material，改视界不污染资产）
        private static float _menuHorizonScale = HorizonFrom; // 首次解析时读材质烘焙值
        private static Transform _light;             // 主光源
        private static Camera _worldCam;
        private static Pose? _menuCamPose;           // 吞屏前菜单位姿快照（回菜单还原）
        private static GameObject _boardRoot;        // HexMapAuthoring 宿主
        private static bool _swallowing;             // 吞屏重入防抖
        private static bool _mounting;               // 装台重入防抖

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：进 Play 早期清全部静态缓存。
        /// SubsystemRegistration 早于场景加载/UIBootstrap.OnEnable——上局已销毁的黑洞材质/引用
        /// 绝不漏进本局（2026-10-03 实机踩坑：MissingReference on Material）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _blackHole = null;
            _blackHoleMat = null;
            _light = null;
            _worldCam = null;
            _menuCamPose = null;
            _boardRoot = null;
            _swallowing = false;
            _mounting = false;
        }

        // ======================================== 舞台分发（UIManager.Activate 挂点） ========================================

        /// <summary>界面装屏后的舞台态分发：主菜单=黑洞态；战场自行装台（OnEnter 时序）；其它屏隐藏黑洞。
        /// 编辑态（场景载入/重编译 UIBootstrap 也会走 Show）不导演——免改脏场景、免编辑态材质实例泄漏。</summary>
        public static void OnScreenActivated(UIScreen screen)
        {
            if (!Application.isPlaying) return;
            if (screen is MainMenuScreen) ShowMenuStage();
            else if (screen is BattleScreen) { /* 装台由 BattleScreen.OnEnter → MountBoardAndDawnAsync 驱动 */ }
            else HideBlackHole();
        }

        /// <summary>菜单舞台：黑洞可见（视界回常态）+ 主光源菜单俯角 + 相机回菜单位姿（有快照才还原）。</summary>
        public static void ShowMenuStage()
        {
            if (!ResolveScene()) return;
            _blackHole.gameObject.SetActive(true);
            if (_blackHoleMat == null)
                _blackHoleMat = _blackHole.material; // 材质实例中途被销毁（异常路径）→ 从渲染器重取
            if (_blackHoleMat != null)
                _blackHoleMat.SetFloat(HorizonId, _menuHorizonScale);
            SetLightX(MenuLightX);
            if (_menuCamPose.HasValue)
                _worldCam.transform.SetPositionAndRotation(_menuCamPose.Value.position, _menuCamPose.Value.rotation);
        }

        /// <summary>立即隐藏黑洞（非战场的其它界面）。</summary>
        public static void HideBlackHole()
        {
            if (!ResolveScene()) return;
            _blackHole.gameObject.SetActive(false);
        }

        // ======================================== 进战场过渡 ①②（吞屏） ========================================

        /// <summary>过渡①②：主光源压到 -20°（0s）→ 视界 0.02→0.2 膨胀（0.5s）→ 隐藏黑洞，
        /// 然后回调 onSwallowed 切战场屏（屏内 OnEnter 继续③④）。场景缺件（如调试场景）直接回调不过渡。</summary>
        public static async UniTaskVoid SwallowToBattleAsync(Action onSwallowed)
        {
            if (_swallowing) return;
            if (!ResolveScene())
            {
                onSwallowed?.Invoke();
                return;
            }

            _swallowing = true;
            try
            {
                _menuCamPose = new Pose(_worldCam.transform.position, _worldCam.transform.rotation);

                // ① 主光源压到 -20°（0s，瞬时）
                SetLightX(MenuLightX);

                // ② 视界膨胀吞屏（0.5s，smoothstep 缓动；realtime 计时对卡顿免疫）
                _blackHole.gameObject.SetActive(true);
                float t0 = Time.realtimeSinceStartup;
                while (true)
                {
                    if (_blackHole == null || _blackHoleMat == null) return; // Play 中途停止：对象已销毁，直接弃演
                    float k = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / SwallowSeconds);
                    _blackHoleMat.SetFloat(HorizonId, Mathf.Lerp(HorizonFrom, HorizonTo, Mathf.SmoothStep(0f, 1f, k)));
                    if (k >= 1f) break;
                    await UniTask.Yield();
                }
                _blackHole.gameObject.SetActive(false);
            }
            finally
            {
                _swallowing = false;
            }

            onSwallowed?.Invoke();
        }

        // ======================================== 战场装台 ③④（棋盘 + 日出） ========================================

        /// <summary>装台③④：装载 HexMap 棋盘（等配置单例+首批格子，现成流式管线）→
        /// 相机对盘取景 → 主光源 -20°→45° 日出。资产缺失/超时不阻塞战场（只亮光）。</summary>
        public static async UniTaskVoid MountBoardAndDawnAsync()
        {
            if (_mounting) return;
            _mounting = true;
            try
            {
                // ③ 装载棋盘：HexMapAuthoring 走现成 Install 路径（OnEnable 协程等 Default World）
                if (_boardRoot == null)
                {
                    var settings = Tide.HotUpdate.HotUpdateAssets.Load<HexMapFeatureSettings>(
                        SettingsAddress, SettingsAssetPath);
                    if (settings == null)
                    {
                        Debug.LogError($"[BattleStage] HexMapFeatureSettings 缺失：{SettingsAssetPath}（棋盘跳过）");
                    }
                    else
                    {
                        // 先禁用再装配：AddComponent 在激活物上当场触发 OnEnable→Install 协程
                        // （Default World 已就绪时同步跑完），字段赋值必须赶在激活之前——
                        // 否则 Install 读到 null 配置直接报错（2026-10-03 实机踩坑）
                        _boardRoot = new GameObject("HexBoardRoot");
                        _boardRoot.SetActive(false);
                        _boardRoot.AddComponent<HexMapAuthoring>().featureSettings = settings;
                        _boardRoot.SetActive(true); // 此刻 OnEnable→Install，配置已就位
                    }
                }

                if (_boardRoot != null)
                {
                    await UntilBoardReady(15f);   // 配置单例 + 首批格子
                    FrameBoardCamera();           // 先对盘取景（装载期间画面已就位）

                    // 等全图装载（32×32≈1024 格，100 格/帧预算约 1s；超时不阻塞日出）
                    await UntilFullMapLoaded(20f);

                    // 绑定 + 占位标记（数据层自检走 HexBoardBinding.SelfCheck 日志）
                    HexBoardBinding.Current = HexBoardBinding.Capture();
                    if (HexBoardBinding.Current != null)
                    {
                        HexBoardBinding.Current.SelfCheck();
                        BuildBoardMarkers(HexBoardBinding.Current);
                    }
                    else
                    {
                        Debug.LogWarning("[BattleStage] 棋盘矩形未启用（BattleHexMapSettings.boardRegion）——绑定层跳过");
                    }
                }

                // ④ 战场日出：主光源 x -20° → 45°（棋盘+外围齐了再升）
                await TweenLightX(BattleLightX, LightUpSeconds);
            }
            finally
            {
                _mounting = false;
            }
        }

        /// <summary>战场拆台（BattleScreen.OnExit）：卸全部格子 + 销毁棋盘根（标记随根销毁）+ 清绑定。
        /// 菜单舞台（黑洞/光源/相机）由界面钩子 OnScreenActivated 恢复，此处不越权。</summary>
        public static void DismountBoard()
        {
            HexBoardBinding.Current = null;
            var streaming = StreamingSystemOrNull();
            try
            {
                streaming?.UnloadAll();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BattleStage] UnloadAll 异常（继续拆根）：{ex.Message}");
            }
            if (_boardRoot != null)
            {
                UnityEngine.Object.Destroy(_boardRoot);
                _boardRoot = null;
            }
        }

        // ======================================== 内部 ========================================

        /// <summary>场景引用解析（按名；GameObject.Find 只见激活态——首次必在菜单态解析成功后缓存）。</summary>
        private static bool ResolveScene()
        {
            if (_blackHole != null && _light != null && _worldCam != null) return true;

            var bhGo = GameObject.Find("BlackHole");
            if (bhGo != null)
            {
                _blackHole = bhGo.GetComponent<Renderer>();
                if (_blackHole != null)
                {
                    _blackHoleMat = _blackHole.material; // 实例化：运行时改视界不写回资产
                    _menuHorizonScale = _blackHoleMat.GetFloat(HorizonId); // 首态即菜单常态
                }
                _worldCam = bhGo.GetComponentInParent<Camera>();
            }
            if (_worldCam == null) _worldCam = Camera.main;

            var lightGo = GameObject.Find("Directional Light");
            if (lightGo != null) _light = lightGo.transform;

            if (_blackHole == null || _light == null || _worldCam == null)
            {
                Debug.LogWarning("[BattleStage] 场景缺件（BlackHole/Directional Light/Camera）——舞台过渡停用");
                return false;
            }
            return true;
        }

        private static void SetLightX(float x)
        {
            if (_light == null) return;
            var e = _light.eulerAngles;
            _light.eulerAngles = new Vector3(x, e.y, e.z);
        }

        /// <summary>主光源 x 缓动（y/z 冻结；fromX 归一化到 ±180 防 340→45 绕远路）。</summary>
        private static async UniTask TweenLightX(float toX, float seconds)
        {
            if (_light == null) return;
            float fromX = Mathf.DeltaAngle(0f, _light.eulerAngles.x);
            float yaw = _light.eulerAngles.y;
            float roll = _light.eulerAngles.z;
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                if (_light == null) return; // Play 中途停止：光源已销毁，弃演
                float k = seconds <= 0f ? 1f : Mathf.Clamp01((Time.realtimeSinceStartup - t0) / seconds);
                _light.eulerAngles = new Vector3(
                    Mathf.Lerp(fromX, toX, Mathf.SmoothStep(0f, 1f, k)), yaw, roll);
                if (k >= 1f) break;
                await UniTask.Yield();
            }
        }

        /// <summary>等棋盘就绪：HexMapConfig 单例安装完成 + 流式系统产出首批格子（轮询超时防卡死）。</summary>
        private static async UniTask UntilBoardReady(float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            World world = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                world = World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    using (var q = world.EntityManager.CreateEntityQuery(typeof(HexMapConfig)))
                        if (!q.IsEmpty) break;
                }
                await UniTask.Yield();
            }

            var streaming = StreamingSystemOrNull();
            if (streaming == null) return;
            while (Time.realtimeSinceStartup < deadline && streaming.CellLookup.Count == 0)
                await UniTask.Yield();
        }

        /// <summary>等全图装载（CellLookup == cellCount 乘积；超时返回不阻塞——外围缺格不影响棋盘日出）。</summary>
        private static async UniTask UntilFullMapLoaded(float timeoutSeconds)
        {
            var streaming = StreamingSystemOrNull();
            if (streaming == null) return;
            int expected = HexMapRuntime.IsValid
                ? HexMapRuntime.CellCount.x * HexMapRuntime.CellCount.y : 0;
            if (expected <= 0) return;

            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (streaming.CellLookup.Count >= expected) return;
                await UniTask.Yield();
            }
            Debug.LogWarning($"[BattleStage] 全图装载超时（{streaming.CellLookup.Count}/{expected}）——继续日出");
        }

        private static HexChunkStreamingSystem StreamingSystemOrNull()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return null;
            return world.GetExistingSystemManaged<HexChunkStreamingSystem>();
        }

        /// <summary>战场相机取景：优先棋盘矩形居中俯拍（外围地形作背景延展），无矩形回落整图。
        /// 角度可自由改——黑洞已隐藏不干扰。流式相机数据同步指向取景中心。</summary>
        private static void FrameBoardCamera()
        {
            if (_worldCam == null) return;

            int mapX = HexMapRuntime.IsValid ? HexMapRuntime.CellCount.x : 10;
            int mapZ = HexMapRuntime.IsValid ? HexMapRuntime.CellCount.y : 10;
            int minX = 0, minZ = 0, maxX = mapX - 1, maxZ = mapZ - 1;
            if (HexMapRuntime.HasBoard)
            {
                minX = HexMapRuntime.BoardRectMin.x; maxZ = HexMapRuntime.BoardRectMax.y;
                maxX = HexMapRuntime.BoardRectMax.x; minZ = HexMapRuntime.BoardRectMin.y;
            }

            float ir2 = HexMapConfigBuilder.InnerRadius * 2f;
            float or15 = HexMapConfigBuilder.OuterRadius * 1.5f;
            // offset→世界（与 HexChunkStreamingSystem.CreateCell 同式）：x=(ox+z*0.5−z/2)·IR2, z=z·1.5R
            Vector3 Corner(int x, int z) => new Vector3((x + z * 0.5f - z / 2) * ir2, 0f, z * or15);
            var c0 = Corner(minX, minZ);
            var c1 = Corner(maxX, maxZ);
            var center = (c0 + c1) * 0.5f;
            float w = (maxX - minX + 1.5f) * ir2;
            float d = (maxZ - minZ + 1) * or15;
            float dist = Mathf.Max(w, d);

            var pos = center + new Vector3(0f, dist * 0.85f, -dist * 0.95f);
            _worldCam.transform.position = pos;
            _worldCam.transform.rotation = Quaternion.LookRotation((center - pos).normalized, Vector3.up);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                using (var q = world.EntityManager.CreateEntityQuery(typeof(HexMapCameraData)))
                    if (!q.IsEmpty)
                    {
                        var e = q.GetSingletonEntity();
                        var data = world.EntityManager.GetComponentData<HexMapCameraData>(e);
                        data.Position = new float3(center.x, 0f, center.z);
                        world.EntityManager.SetComponentData(e, data);
                    }
            }
        }

        // ======================================== 占位标记（绑定可视化） ========================================

        /// <summary>双方单位行(2×9)+地牌行(9)各摆占位标记：单位=小方块、地牌=扁板；
        /// 蓝红区分双方。父级 HexBoardMarkers 挂棋盘根下，随拆台销毁。</summary>
        private static void BuildBoardMarkers(HexBoardBinding board)
        {
            if (_boardRoot == null) return;
            var parent = new GameObject("HexBoardMarkers");
            parent.transform.SetParent(_boardRoot.transform, false);

            int built = 0, missing = 0;
            for (int p = 0; p < 2; p++)
            {
                var color = p == 0 ? MarkerColorP1 : MarkerColorP2;
                foreach (var (x, z) in GameBoard.BoardLayout.UnitCells(p))
                {
                    if (TryPlaceMarker(parent, board, x, z, color, land: false)) built++;
                    else missing++;
                }
                foreach (var (x, z) in GameBoard.BoardLayout.LandCells(p))
                {
                    if (TryPlaceMarker(parent, board, x, z, color, land: true)) built++;
                    else missing++;
                }
            }
            Debug.Log($"[BattleStage] 占位标记：{built} 个（单位 36 + 地牌 18），缺格 {missing}");
        }

        private static bool TryPlaceMarker(GameObject parent, HexBoardBinding board, int x, int z,
            Color color, bool land)
        {
            if (!board.TryGetCell(x, z, out _)) return false;
            var pos = board.WorldPositionOf(x, z);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = land ? $"marker-land-{x}-{z}" : $"marker-unit-{x}-{z}";
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>()); // 纯视觉，不进物理
            marker.transform.SetParent(parent.transform, false);

            if (land)
            {
                marker.transform.localScale = new Vector3(0.34f, 0.05f, 0.34f);
                marker.transform.position = pos + new Vector3(0f, 0.03f, 0f);
            }
            else
            {
                marker.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
                marker.transform.position = pos + new Vector3(0f, 0.08f, 0f);
            }

            var mat = marker.GetComponent<Renderer>().material; // 实例材质：着色不污染资产
            mat.color = color;
            return true;
        }
    }
}
