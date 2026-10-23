using System;
using Cysharp.Threading.Tasks;
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
    /// ③ 战场棋盘装台（2026-10-13 战场缩水定案：BattleHexBoard 运行时拼装 9×6 核心带——
    ///    BattleHex 单枚 mesh×54 格 + 分区材质，同步即就绪）
    /// ④ 主光源 x -20°→45°（战场日出）。
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

        // ---- v6 双读数视角（2026-10-23 §11.1 定案）：战斗机位=棋盘正上方直下（pitch 90°）----
        // 菜单仍是场景手摆机位（黑洞是相机子物体、随相机取景，不受影响）；装台快照→应用战斗位，
        // 拆台还原。高度以「棋盘占画面纵向 ~60%、四周留出碗形环境」为准。
        private const float BattleCamX = 7.35f;   // 盘心（棋盘世界包围盒中心）
        private const float BattleCamZ = 3.75f;
        private const float BattleCamH = 12.5f;  // 定档：盘面≈51%宽×62%高（aspect 1.778 实测反推）
        private const float BattleCamPitch = 90f; // 正俯视：六边形正形读作 90°
        // v6 21:35 DSH 修订·压光：主光 6→2.5 + 环境光压暗——渲染值贴近材质值（棋盘 60~72/环境 40~55/
        // 整帧 mean≤65）。菜单态快照还原，与机位同链。
        private const float BattleLightIntensity = 2.5f;
        private static readonly Color BattleAmbient = new Color(0.19f, 0.195f, 0.205f); // 量图回调（环境 40~55·背光坡托底）
        private static readonly Color BattleClear = new Color(0.043f, 0.039f, 0.035f); // #0B0A09 兜底
        private static Pose _camSnapshot;                  // 装台时场景（菜单）位姿快照
        private static CameraClearFlags _camClearSnapshot; // 同上：清屏方式
        private static Color _camBgSnapshot;               // 同上：清屏色
        private static float _lightIntensitySnapshot;      // 同上：主光强度
        private static UnityEngine.Rendering.AmbientMode _ambientModeSnapshot; // 同上：环境光模式/颜色
        private static Color _ambientColorSnapshot;
        private static bool _camPosed;                     // 战斗机位在途（拆台还原后清）

        private static readonly int HorizonId = Shader.PropertyToID("_HorizonScale");

        private static Renderer _blackHole;          // 相机下的黑洞球体（MeshRenderer）
        private static Material _blackHoleMat;       // 实例材质（renderer.material，改视界不污染资产）
        private static float _menuHorizonScale = HorizonFrom; // 首次解析时读材质烘焙值
        private static Transform _light;             // 主光源
        private static Camera _worldCam;             // 场景相机（v4 起位姿=场景控制，本类只读引用）
        private static GameObject _stageRoot;        // 战场总根（v3 prefab 态=BattleStage；回落态=棋盘根）
        private static GameObject _boardRoot;        // 棋盘 Board 节点（prefab 态=stage 子节点；回落=stage 自身）
        private static Transform _mapRoot;           // 运行时战场临时对象总根「Map」（棋盘/BattleFieldView/箭头等）
        private static bool _mapOwned;               // Map 是否运行时自建（场景手摆颗拆台不销毁）
        private static bool _swallowing;             // 吞屏重入防抖
        private static bool _mounting;               // 装台重入防抖

        /// <summary>世界相机（战场点击射线/3D→屏幕投影用；装台前可能 null——调用方判空）。</summary>
        public static Camera WorldCam => _worldCam;

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
            _boardRoot = null;
            _stageRoot = null;
            _mapRoot = null;
            _mapOwned = false;
            _swallowing = false;
            _mounting = false;
            _camPosed = false;
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
            if (_swallowing)
            {
                Debug.Log("[BattleStage] 吞屏在途——本次进战场请求被忽略（重入防抖）");
                return;
            }
            if (!ResolveScene())
            {
                onSwallowed?.Invoke();
                return;
            }

            _swallowing = true;
            try
            {
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

        /// <summary>装台③④：BattleHexBoard 运行时拼装核心带棋盘（BattleHex 单枚 mesh×55 tile
        /// ——8×7 盘 56 格位、中线 x7 槽缺口；同步即就绪）→ 相机对盘取景 → 棋格画外浮起拼装（≤2s，与日出并行）→
        /// 主光源 -20°→45° 日出。mesh 缺失只报错不阻塞日出。</summary>
        public static async UniTaskVoid MountBoardAndDawnAsync()
        {
            if (_mounting) return;
            _mounting = true;
            try
            {
                // ③ 棋盘装台（2026-10-13 缩盘定案二段：9×6 全盘运行时拼装——
                // BattleHexMeshBuilder 只产单枚六边形 mesh，摆位/配色在此装配）
                if (_stageRoot == null)
                {
                    // 临时战场地块总根「Map」（2026-10-14 晚定案）：运行时临时对象（棋盘根/BattleFieldView/
                    // 攻击箭头等）统一收进 Map，不堆积场景根目录。优先认领场景里用户手摆的 Map 节点
                    //（GameObject.Find 只见激活态；缺/未激活才运行时自建，拆台只销毁自建的那颗）。
                    if (_mapRoot == null)
                    {
                        var existing = GameObject.Find("Map");
                        _mapOwned = existing == null;
                        _mapRoot = (existing != null ? existing : new GameObject("Map")).transform;
                    }
                    // v3 预制体化（2026-10-23 13:00）：Build 返回战场总根——prefab 态=BattleStage
                    //（Board/Env/Stairs 子节点），回落态=运行时拼装的棋盘根。总根挂 Map；
                    // Grid 认 Board 子节点（回落=根自身）；Env 由 ArenaStage 认领。
                    var built = BattleHexBoard.Build();
                    if (built != null)
                    {
                        _stageRoot = built;
                        _stageRoot.transform.SetParent(_mapRoot, false); // Map 恒 identity，局部坐标=世界坐标
                        var board = _stageRoot.transform.Find("Board");
                        var env = _stageRoot.transform.Find("Env");
                        var stairs = _stageRoot.transform.Find("Stairs");
                        _boardRoot = board != null ? board.gameObject : _stageRoot;
                        BattleBoardGrid.Mount(_boardRoot.transform);
                        BattleBoardGrid.SelfCheck(); // 数据层自检（格数/mesh/材质/格心公式）
                        BattleZoneProps.Mount(); // 特殊格道具：卡组卡堆+墓地/除外漩涡（挂 tile 下随浮起同走）
                        BattleHudRow.Mount(); // 功能地块行（2026-10-13 按钮上盘：返回/结束回合，独立根）
                        if (env != null) BattleArenaStage.Attach(env, stairs); // prefab 态：Env 已烘焙
                        else BattleArenaStage.Mount(_mapRoot); // 回落态：运行时铺（独立 EnvRoot）
                    }
                }

                if (_boardRoot != null)
                {
                    // 2026-10-23 v4 用户定案：相机由场景控制（Main.unity 里手摆机位），代码不再设置
                    // 位姿——原 FrameBoardCamera 定档/菜单位姿快照还原整链退役；装台仍用场景相机算
                    // 浮起出生视锥（_worldCam 由 ResolveScene 解析，只读不写）。
                    BattleBoardAssemble.PlayAsync(_boardRoot.transform, _worldCam).Forget();
                }

                // v6 双读数视角：战斗机位=正俯视直下（§11.1）。菜单位姿在 ApplyBattlePose 快照、
                // 拆台还原——黑洞是相机子物体随相机取景，菜单视觉不受影响。浮起演出 v3 起逐格轻落
                // 不用相机（cam 兼容保留参数），正俯视机位下安全。
                ApplyBattlePose();

                // ④ 战场日出：主光源 x -20° → 45°
                await TweenLightX(BattleLightX, LightUpSeconds);
            }
            finally
            {
                _mounting = false;
            }
        }

        /// <summary>战场拆台（BattleScreen.OnExit）：销毁棋盘实例 + 清格位服务与地块特效索引。
        /// 菜单舞台（黑洞/光源/相机）由界面钩子 OnScreenActivated 恢复，此处不越权。
        /// 场景手摆的 Map 收纳根不销毁（_mapOwned 只在运行时自建时为真）；其余临时子根
        /// 由各持有方自毁（棋盘根在此、BattleFieldView 在 Dispose），Map 只做挂载点。</summary>
        public static void DismountBoard()
        {
            RestoreMenuPose(); // v6：还原菜单（场景手摆）机位——先于销毁，防中途异常漏还原
            BattleTileFx.Unmount(); // 特效对象挂 tile 下随根销毁，此处只清索引
            BattleZoneProps.Unmount(); // 特殊格道具同口径（卡堆/漩涡随根销毁，运行时 mesh 显式销毁）
            BattleHudRow.Unmount(); // 功能地块行（独立根+运行时材质实例显式销毁）
            BattlePriorityGlow.Unmount(); // 优先权底部光条（Map 下独立根，显式销毁+清引用）
            BattleArenaStage.Unmount(); // 祭坛环境（EnvRoot 显式销毁——场景手摆 Map 根不随之拆）
            BattleBoardGrid.Unmount();
            if (_stageRoot != null)
            {
                UnityEngine.Object.Destroy(_stageRoot); // v3：销毁战场总根（Board/Env/Stairs 一并收——prefab 态 Env 挂 stage 下）
                _stageRoot = null;
            }
            _boardRoot = null;
            if (_mapRoot != null && _mapOwned)
            {
                UnityEngine.Object.Destroy(_mapRoot.gameObject);
            }
            _mapRoot = null;
        }

        /// <summary>运行时战场临时对象总根「Map」（装台认领/自建，拆台仅销毁自建颗；null=未装台）——
        /// 临时创建的战场地块等收进本根，不堆积场景根目录（BattleFieldView/攻击箭头等同收）。</summary>
        public static Transform MapRoot => _mapRoot;

        // ======================================== 内部 ========================================

        /// <summary>场景引用解析（按名；2026-10-13 修复：场景 BlackHole 默认 inactive（m_IsActive:0），
        /// GameObject.Find 只见激活态——首轮解析必失败、菜单黑洞态/吞屏/取景整链失明。
        /// 现含 inactive 再找一次；命中后菜单态 SetActive(true) 由调用方负责）。</summary>
        private static bool ResolveScene()
        {
            if (_blackHole != null && _light != null && _worldCam != null) return true;

            var bhGo = GameObject.Find("BlackHole");
            if (bhGo == null)
            {
                foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.name != "BlackHole") continue;
                    bhGo = t.gameObject;
                    break;
                }
            }
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

        /// <summary>v6 战斗机位（§11.1）+压光（21:35 修订）：棋盘正上方直下 + 主光 2.5 + 环境光压暗。
        /// 装台应用、拆台还原；重复装台只快照一次。</summary>
        private static void ApplyBattlePose()
        {
            if (!ResolveScene() || _worldCam == null) return;
            if (!_camPosed)
            {
                _camSnapshot = new Pose(_worldCam.transform.position, _worldCam.transform.rotation);
                _camClearSnapshot = _worldCam.clearFlags;
                _camBgSnapshot = _worldCam.backgroundColor;
                _lightIntensitySnapshot = _light != null ? _light.GetComponent<Light>().intensity : 6f;
                _ambientModeSnapshot = RenderSettings.ambientMode;
                _ambientColorSnapshot = RenderSettings.ambientLight;
                _camPosed = true;
            }
            _worldCam.transform.SetPositionAndRotation(
                new Vector3(BattleCamX, BattleCamH, BattleCamZ), Quaternion.Euler(BattleCamPitch, 0f, 0f));
            _worldCam.clearFlags = CameraClearFlags.SolidColor;
            _worldCam.backgroundColor = BattleClear; // #0B0A09：碗沿外不应可见，防御漏视
            if (_light != null)
            {
                var l = _light.GetComponent<Light>();
                if (l != null) l.intensity = BattleLightIntensity;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; // 压暗环境光（菜单快照还原）
            RenderSettings.ambientLight = BattleAmbient;
        }

        /// <summary>v6 拆台还原菜单态（机位/清屏/主光强度/环境光——黑洞随相机子物体同走）。</summary>
        private static void RestoreMenuPose()
        {
            if (!_camPosed) return;
            if (_worldCam != null)
            {
                _worldCam.transform.SetPositionAndRotation(_camSnapshot.position, _camSnapshot.rotation);
                _worldCam.clearFlags = _camClearSnapshot;
                _worldCam.backgroundColor = _camBgSnapshot;
            }
            if (_light != null)
            {
                var l = _light.GetComponent<Light>();
                if (l != null) l.intensity = _lightIntensitySnapshot;
            }
            RenderSettings.ambientMode = _ambientModeSnapshot;
            RenderSettings.ambientLight = _ambientColorSnapshot;
            _camPosed = false;
        }

        /// <summary>战场相机：2026-10-23 v4=场景控制（菜单态沿用）；v6 §11.1 起**战斗位**由本类
        /// 定档（正俯视直下+清屏 #0B0A09），装台应用/拆台还原（ApplyBattlePose/RestoreMenuPose）。</summary>
    }
}
