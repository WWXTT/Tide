using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tide.验证
{
    /// <summary>
    /// 战场美化截图工具（2026-10-23 战场美化 P-1 定案；当日二版=过弹窗）：Play 模式下走真实入口
    /// （主菜单 btn-teststart → 木桩测试局），**依次点掉两道弹窗**再等棋盘就绪+交班开战：
    ///   ① dummy-options 弹窗（木桩测试设置）→ 点 btn-start「开始」
    ///   ② dummy-deck 弹窗（掷币后选卡组）→ 点 btn-fight「开战」
    /// 首版教训：只等 Ready+浮起收尾会在弹窗期抓图（UI 被压/无手牌无单位）——弹窗链必须真实走完。
    /// 就绪判据 = Ready && 浮起收尾 && 两弹窗消失 && [BattleOpening] 段五交班，再稳 3s 抓三张：
    ///   s&lt;n&gt;_ui_&lt;时间戳&gt;.png    = 含 UI 的完整 Game 视图（ScreenCapture.CaptureScreenshot，帧末落盘）
    ///   s&lt;n&gt;_env_&lt;时间戳&gt;.png   = 根 Canvas 全关后纯环境图（关后补 100ms 再抓，抓完恢复）
    ///   s&lt;n&gt;_close_&lt;时间戳&gt;.png = 贴地掠射特写（相机临时低角度看缝下台面/法阵，拍完还原机位；
    ///                              仅自查用不进验收主图——2026-10-23 DSH 修订要求）
    /// 存 .ztmp/p4/shots/（DSH 目录约定），Console 打 [ArtShot] 数据行（path/boardReady/tiles/tileScale/
    /// cam(含相机名)/envObjects/popups/engaged）——供监督逐字节比对时间戳防旧图。
    /// 用法：进 Play 后菜单 Tools/战场/美化截图/&lt;s0..s4&gt;（阶段标签）。
    /// </summary>
    public static class BattleArtShot
    {
        private const string MenuRoot = "Tools/战场/美化截图";
        private const string OutputDir = ".ztmp/p4/shots"; // 相对工程根（DSH 目录约定）

        private static bool _busy;
        private static bool _sawRunning;    // 捕到过 BattleBoardAssemble.Running=true（拼装窗口在途标志）
        private static bool _assemblePass;  // 收到 [BattleBoardAssemble][PASS] 落位日志
        private static bool _engaged;       // 收到 [BattleOpening] 段五交班日志（弹窗链走完、真开局）

        [MenuItem(MenuRoot + "/s0 基线")]
        public static void RunS0() => Run("s0");

        [MenuItem(MenuRoot + "/s1")]
        public static void RunS1() => Run("s1");

        [MenuItem(MenuRoot + "/s2")]
        public static void RunS2() => Run("s2");

        [MenuItem(MenuRoot + "/s3")]
        public static void RunS3() => Run("s3");

        [MenuItem(MenuRoot + "/s4")]
        public static void RunS4() => Run("s4");

        [MenuItem(MenuRoot + "/s6")]
        public static void RunS6() => Run("s6");

        private static async void Run(string stageTag)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[ArtShot] 需在 Play 模式执行（先进 Play 再跑菜单 {MenuRoot}/s*）");
                return;
            }
            if (_busy)
            {
                Debug.LogWarning("[ArtShot] 上一次截图仍在进行——忽略本次触发");
                return;
            }

            _busy = true;
            _sawRunning = false;
            _assemblePass = false;
            _engaged = false;
            Application.logMessageReceived += OnLog;
            try
            {
                // 1. 真实入口进战：btn-teststart（可能被新手引导隐藏——自行激活，同 BattleScreenMountSmoke 口径）
                if (!SynergyUI.BattleBoardGrid.Ready)
                {
                    var testBtn = await WaitTestButtonAsync(30f);
                    if (testBtn == null && !SynergyUI.BattleBoardGrid.Ready)
                    {
                        Debug.LogError("[ArtShot] FAIL：30s 内未见 btn-teststart 且棋盘未装台");
                        return;
                    }
                    if (testBtn != null)
                    {
                        ActivateUp(testBtn.transform);
                        testBtn.onClick.Invoke();
                        Debug.Log("[ArtShot] 已点 btn-teststart（木桩测试入口）");
                    }
                }
                else Debug.Log("[ArtShot] 棋盘已就绪——跳过入口点击（同局重跑口径）");

                // 2. 过两道弹窗（首版教训：漏掉=拍在弹窗期作废）。棋盘已就绪的重跑局弹窗早已不在，
                //    PassPopupAsync 找不到面板=直接放行，不阻塞。
                await PassPopupAsync("dummy-options", "btn-start", 25f);

                string dir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), OutputDir));
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"); // coin 与 ui/env/close 同一时间戳配对
                string uiPath = Path.Combine(dir, $"{stageTag}_ui_{stamp}.png");
                string envPath = Path.Combine(dir, $"{stageTag}_env_{stamp}.png");
                string closePath = Path.Combine(dir, $"{stageTag}_close_{stamp}.png");

                // 2.5 掷币演出截图（v3 13:00 交付项）：dummy-options 确认后掷币（~3.7s 存在期）——
                //     捕到 coin-toss 后延迟到落定段抓一张；固定先后手局无硬币则记日志跳过
                string coinPath = Path.Combine(dir, $"{stageTag}_coin_{stamp}.png");
                await CaptureCoinAsync(coinPath);

                // v6（§11.3）：远景全景=同俯仰拉高机位看整碗
                string panPath = Path.Combine(dir, $"{stageTag}_pan_{stamp}.png");
                string annotPath = Path.Combine(dir, $"{stageTag}_annot_{stamp}.png");
                string swatchPath = Path.Combine(dir, $"{stageTag}_swatch_{stamp}.png");
                if (stageTag == "s6")
                {
                    Camera camP = Camera.main; // 场景相机未打 MainCamera 标签——回落取第一个启用相机
                    if (camP == null)
                        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                            if (c.enabled) { camP = c; break; }
                    if (camP != null)
                    {
                        var pose0 = camP.transform.position;
                        camP.transform.position = pose0 + Vector3.up * 9f;
                        await Task.Delay(100);
                        await CaptureAsync(panPath);
                        camP.transform.position = pose0;
                    }
                }

                await PassPopupAsync("dummy-deck", "btn-fight", 45f); // 中间隔掷币演出 ~4s

                // 3. 等棋盘就绪（装台异步 ≈2s）
                if (!await WaitUntilAsync(() => SynergyUI.BattleBoardGrid.Ready, 25f))
                {
                    Debug.LogError("[ArtShot] FAIL：BattleBoardGrid.Ready=false（25s 超时）");
                    return;
                }

                // 4. 等浮起拼装收尾。Running 在 PlayAsync 启动前置 false，轮询可能落在启动间隙——
                //    以「Running 出现过」或「拼装 PASS 日志」为收尾依据（防误判拼装未开跑）。
                if (!await WaitUntilAsync(() =>
                    {
                        if (SynergyUI.BattleBoardAssemble.Running) { _sawRunning = true; return false; }
                        return _sawRunning || _assemblePass;
                    }, 10f))
                    Debug.LogWarning("[ArtShot] 未捕到浮起拼装窗口（降级静态盘？）——按就绪口径继续抓取");

                // 5. 弹窗清场断言：抓图时刻两弹窗必须不存在（存在=还在弹窗期，抓图作废）
                bool popupsClear = await WaitUntilAsync(() =>
                    FindTransform("dummy-options") == null && FindTransform("dummy-deck") == null, 10f);

                // 6. 稳定等待 3s（战报/手牌/演出落定）
                await Task.Delay(3000);

                // 7. 数据行素材：格数/格缩放/相机/环境件数
                var boardRoot = SynergyUI.BattleBoardGrid.Root;
                int tiles = 0;
                var tileScale = Vector3.one;
                if (boardRoot != null)
                    foreach (Transform child in boardRoot)
                    {
                        if (tiles == 0) tileScale = child.localScale;
                        tiles++;
                    }

                var cam = Camera.main;
                if (cam == null)
                    foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        if (c.enabled) { cam = c; break; }
                string camInfo = cam != null
                    ? $"{cam.name}@({cam.transform.position.x:0.##},{cam.transform.position.y:0.##},{cam.transform.position.z:0.##})/{cam.transform.eulerAngles.x:0.#}"
                    : "null";

                int envObjects = 0;
                var mapRoot = SynergyUI.BattleStageDirector.MapRoot;
                var envRoot = mapRoot != null ? FindChild(mapRoot, "EnvRoot") : null;
                if (envRoot != null) envObjects = envRoot.childCount;
                else if (mapRoot != null)
                {
                    // v3 预制体态：Env 是 BattleStage 子节点（不再叫 EnvRoot）
                    var stage = FindChild(mapRoot, "BattleStage");
                    var env = stage != null ? stage.Find("Env") : null;
                    if (env != null) envObjects = env.childCount;
                }

                // 8. 抓两张（同一时间戳配对；等文件落盘再抓下一张）
                await CaptureAsync(uiPath);

                var off = new List<Canvas>();
                Pose? camPose = null;
                try
                {
                    foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        if (c.enabled && c.isRootCanvas) { c.enabled = false; off.Add(c); }
                    await Task.Delay(100); // 关屏后补一帧等待（DSH 复核项：防当帧仍含 UI）
                    await CaptureAsync(envPath);

                    // 贴地特写（2026-10-23 DSH 修订要求·P0' 缝下特写口径）：低位掠射斜穿多排地缝，
                    // 看缝下地面瓦片/法阵——仅自查用，不进验收主图。拍完还原定档机位
                    if (cam != null)
                    {
                        camPose = new Pose(cam.transform.position, cam.transform.rotation);
                        cam.transform.position = new Vector3(5.6f, 0.42f, -1.5f);
                        cam.transform.rotation = Quaternion.LookRotation(
                            new Vector3(8.6f, -0.78f, 3.6f) - cam.transform.position);
                        await Task.Delay(100);
                        await CaptureAsync(closePath);
                    }

                    // v6（§11.3 交付）：标注图（中心90°/四周30–45°）+ 配色 swatch（材质实值读回）
                    if (stageTag == "s6")
                    {
                        await Task.Delay(100);
                        await CaptureAsync(annotPath, () => BuildAnnotOverlay());
                        await CaptureAsync(swatchPath, () => BuildSwatchOverlay());
                    }
                }
                finally
                {
                    if (camPose.HasValue && cam != null)
                        cam.transform.SetPositionAndRotation(camPose.Value.position, camPose.Value.rotation);
                    foreach (var c in off) if (c != null) c.enabled = true;
                }

                bool boardReady = SynergyUI.BattleBoardGrid.Ready && !SynergyUI.BattleBoardAssemble.Running;
                string v6 = stageTag == "s6" ? await V6VerifyAsync(cam) : "";
                Debug.Log($"[ArtShot] path={uiPath} ; {envPath} boardReady={boardReady} tiles={tiles}" +
                          $" tileScale=({tileScale.x:0.###},{tileScale.y:0.###},{tileScale.z:0.###})" +
                          $" cam=({camInfo}) envObjects={envObjects} popups={(popupsClear ? "clear" : "REMAIN!")} engaged={_engaged}" +
                          (v6.Length > 0 ? " " + v6 : ""));
                Debug.Log($"[ArtShot] files ui={(File.Exists(uiPath) ? new FileInfo(uiPath).Length / 1024 + "KB" : "missing!")}" +
                          $" env={(File.Exists(envPath) ? new FileInfo(envPath).Length / 1024 + "KB" : "missing!")}" +
                          $" close={(File.Exists(closePath) ? new FileInfo(closePath).Length / 1024 + "KB" : "missing!")}" +
                          $" canvasesOff={off.Count} camRestored={camPose.HasValue}");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                _busy = false;
            }
        }

        private static void OnLog(string condition, string _, LogType type)
        {
            if (condition == null) return;
            if (condition.StartsWith("[BattleBoardAssemble]", StringComparison.Ordinal))
            {
                _sawRunning = true; // 任何拼装日志都证明窗口开过
                if (condition.Contains("PASS")) _assemblePass = true;
            }
            if (condition.StartsWith("[BattleOpening]", StringComparison.Ordinal) && condition.Contains("段五"))
                _engaged = true; // 段五交班开战=弹窗链走完、引擎真开局
        }

        /// <summary>等指定名弹窗出现并点掉其确认钮；超时未见=放行（非弹窗路径/已过窗）。</summary>
        private static async Task PassPopupAsync(string panelName, string confirmButton, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var panel = FindTransform(panelName);
                if (panel != null)
                {
                    var btn = FindChild(panel, confirmButton)?.GetComponentInChildren<Button>(true);
                    if (btn != null)
                    {
                        btn.onClick.Invoke();
                        Debug.Log($"[ArtShot] 已点 {panelName}/{confirmButton}（过弹窗）");
                        return;
                    }
                    Debug.LogWarning($"[ArtShot] {panelName} 缺按钮 {confirmButton}——再等下一轮");
                }
                await Task.Delay(200);
            }
            Debug.LogWarning($"[ArtShot] {seconds:0}s 内未见弹窗 {panelName}（放行：重跑局/非弹窗路径）");
        }

        /// <summary>掷币演出截图（v3 交付项）：等 coin-toss 出现（≤15s）→ 延迟 1.5s 抓落定段
        ///（飞行1.35s+小弹跳，延迟抓=落定微倾可读面）；未见（固定先后手/已错过）记日志跳过。</summary>
        private static async Task CaptureCoinAsync(string path)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (FindTransform("coin-toss") != null)
                {
                    await Task.Delay(1500);
                    await CaptureAsync(path);
                    Debug.Log($"[ArtShot] 掷币截图：{path}（{(File.Exists(path) ? new FileInfo(path).Length / 1024 + "KB" : "missing!")}）");
                    return;
                }
                await Task.Delay(100);
            }
            Debug.LogWarning("[ArtShot] 15s 内未见 coin-toss（固定先后手局无掷币）——掷币截图跳过");
        }

        /// <summary>等 btn-teststart 出现（场景引导期可能迟建）；期间棋盘已就绪=已在战场，返回 null 免点。</summary>
        private static async Task<Button> WaitTestButtonAsync(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SynergyUI.BattleBoardGrid.Ready) return null;
                var btn = FindButton("btn-teststart");
                if (btn != null) return btn;
                await Task.Delay(200);
            }
            return null;
        }

        /// <summary>截图并等文件落盘（CaptureScreenshot 帧末写入；半写防御再稳 300ms）。</summary>
        private static async Task CaptureAsync(string path)
        {
            ScreenCapture.CaptureScreenshot(path); // supersize=1 默认
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (File.Exists(path))
                {
                    await Task.Delay(300);
                    return;
                }
                await Task.Delay(100);
            }
            Debug.LogWarning($"[ArtShot] 截图 10s 未落盘：{path}");
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> pred, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (pred()) return true;
                await Task.Delay(100);
            }
            return pred();
        }

        private static Button FindButton(string name)
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.name == name) return b; // 含隐藏——btn-teststart 会被新手引导隐藏
            return null;
        }

        /// <summary>全场景按名找 Transform（含隐藏；弹窗/面板实例名唯一）。</summary>
        private static Transform FindTransform(string name)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>沿祖先链激活隐藏节点（引导隐藏的 test 按钮自行打开——只动 activeSelf）。</summary>
        private static void ActivateUp(Transform t)
        {
            if (t == null) return;
            var chain = new List<Transform>();
            for (var p = t; p != null; p = p.parent) chain.Add(p);
            chain.Reverse(); // 自根向下激活
            foreach (var p in chain)
                if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
        }

        private static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
                var hit = FindChild(c, name);
                if (hit != null) return hit;
            }
            return null;
        }

        // ======================================== v6（§11.3 交付与自检） ========================================

        /// <summary>带临时屏幕覆盖层的截图：建覆盖→抓→销毁（标注图/swatch 用）。</summary>
        private static async Task CaptureAsync(string path, Func<GameObject> overlay)
        {
            var go = overlay != null ? overlay() : null;
            try { await Task.Delay(150); await CaptureAsync(path); }
            finally { if (go != null) UnityEngine.Object.Destroy(go); }
        }

        /// <summary>标注图覆盖层：中心=90° 正俯视；四角=四周坡面 30–45° 俯视。</summary>
        private static GameObject BuildAnnotOverlay()
        {
            var root = new GameObject("v6-annot-overlay");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var gold = new Color(0.776f, 0.635f, 0.294f);
            Label(canvas.transform, Screen.width / 2f, Screen.height / 2f - 6f, "中心棋盘 = 90° 正俯视", 46, gold);
            var dirs = new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
            foreach (var d in dirs)
            {
                float x = Screen.width / 2f + d.x * Screen.width * 0.335f;
                float y = Screen.height / 2f + d.y * Screen.height * 0.365f;
                Label(canvas.transform, x, y - 36f, "四周坡面 = 30–45° 俯视", 30, gold);
                Bar(canvas.transform, x, y, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 130f, gold);
            }
            return root;
        }

        private static void Label(Transform parent, float x, float y, string text, int size, Color c)
        {
            var go = new GameObject("lbl");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1000, size + 24);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static void Bar(Transform parent, float x, float y, float deg, float len, Color c)
        {
            var go = new GameObject("bar");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(len, 9);
            rt.anchoredPosition = new Vector2(x, y);
            rt.localEulerAngles = new Vector3(0, 0, deg);
        }

        /// <summary>配色 swatch 覆盖层：色块=已应用材质/灯光的实值读回（读回即「已应用」证据）。</summary>
        private static GameObject BuildSwatchOverlay()
        {
            var root = new GameObject("v6-swatch-overlay");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var bg = new GameObject("bg");
            bg.transform.SetParent(canvas.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.043f, 0.039f, 0.035f);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;

            Func<string, Color> matColor = name =>
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Battle3D/Materials/" + name + ".mat");
                return m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.magenta;
            };
            Func<string, Color> lightColor = name =>
            {
                var t = FindTransform(name);
                var l = t != null ? t.GetComponentInChildren<Light>(true) : null;
                return l != null ? l.color : Color.magenta;
            };
            Func<Color, string> hex = c => ColorUtility.ToHtmlStringRGB(c);
            Func<Color, int> luma = c => Mathf.RoundToInt(255f * (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b));

            var rows = new List<KeyValuePair<Color, string>>
            {
                new KeyValuePair<Color, string>(matColor("BattleTile_Zone00_Backdrop"), "兜底/背景 #0B0A09"),
                new KeyValuePair<Color, string>(matColor("BattleTile_Zone01_Edge"), "棋盘沟/缝 #14120F"),
                new KeyValuePair<Color, string>(matColor("BattleTile_Zone02_P1Unit"), "己方格（低饱和青蓝）#4A6478 · luma " + luma(matColor("BattleTile_Zone02_P1Unit"))),
                new KeyValuePair<Color, string>(matColor("BattleTile_Zone09_P2Unit"), "对方格（压暗锈红）#6B4038 · luma " + luma(matColor("BattleTile_Zone09_P2Unit"))),
                new KeyValuePair<Color, string>(matColor("BattleTile_Zone03_P1Land"), "己方功能区（暖灰偏蓝）"),
                new KeyValuePair<Color, string>(matColor("BattleTile_Zone10_P2Land"), "对方功能区（暖灰偏锈）"),
                new KeyValuePair<Color, string>(matColor("BattleTile_Zone20_Middle"), "中线信息条底 #1A1816 @85%（a=" + matColor("BattleTile_Zone20_Middle").a.ToString("0.##") + "）"),
                new KeyValuePair<Color, string>(matColor("BattleDim_Rock"), "台面石地/岩体 tint（×贴图）→ #2B2723 口径"),
                new KeyValuePair<Color, string>(matColor("BattleBowlRock_A"), "碗面·坡脚带（暖亮·三带分层之一）"),
                new KeyValuePair<Color, string>(matColor("BattleBowlRock_B"), "碗面·中带"),
                new KeyValuePair<Color, string>(matColor("BattleBowlRock_C"), "碗面·外带（最暗）"),
                new KeyValuePair<Color, string>(matColor("BattlePlantMoss_A"), "植物A 灰绿 #56694C"),
                new KeyValuePair<Color, string>(matColor("BattlePlantMoss_B"), "植物B 灰青 #5F6F7A"),
                new KeyValuePair<Color, string>(matColor("BattlePlantMoss_C"), "植物C 灰紫 #6B5566"),
                new KeyValuePair<Color, string>(SynergyUI.BattlePriorityGlow.SelfColor, "唯一强调（优先权/选中/回合）#C6A24B"),
                new KeyValuePair<Color, string>(lightColor("fire-cold"), "篝火·冷蓝 #4A6FA5"),
                new KeyValuePair<Color, string>(lightColor("fire-warm"), "篝火·暖橙 #B5652E"),
                new KeyValuePair<Color, string>(new Color(0.902f, 0.878f, 0.831f), "正文/数字 #E6E0D4（不用纯白）"),
            };
            float y = Screen.height - 70f;
            Label(canvas.transform, 60f, Screen.height - 44f, "战场 v6 配色 swatch（实值读回 hex：" , 30, new Color(0.9f, 0.88f, 0.83f));
            foreach (var row in rows)
            {
                var chip = new GameObject("chip");
                chip.transform.SetParent(canvas.transform, false);
                var img = chip.AddComponent<Image>();
                img.color = row.Key;
                var crt = (RectTransform)chip.transform;
                crt.anchorMin = crt.anchorMax = Vector2.zero;
                crt.sizeDelta = new Vector2(170, 34);
                crt.anchoredPosition = new Vector2(130f, y);
                Label(canvas.transform, 260f, y, "#" + hex(row.Key) + "  " + row.Value, 26, new Color(0.9f, 0.88f, 0.83f));
                y -= 56f;
            }
            return root;
        }

        /// <summary>v6 数据层自检：取景占比/点选3·3/全格遮挡清查/碗沿边缘覆盖/灯与碰撞/三角面预算。
        /// 边缘覆盖给 bowl-terrain 临时挂 MeshCollider 取证，测完即删（稳态环境零碰撞不变）。</summary>
        private static async Task<string> V6VerifyAsync(Camera cam)
        {
            await Task.Yield();
            var sb = new System.Text.StringBuilder();
            sb.Append("aspect=" + (Screen.width * 1f / Screen.height).ToString("0.###"));
            if (cam != null)
            {
                var b = BoardScreenBounds(cam);
                sb.Append(" boardScreen=(" + b.width.ToString("0.###") + "w×" + b.height.ToString("0.###") + "h)");
                sb.Append(" camPose=(" + cam.transform.position.x.ToString("0.##") + "," + cam.transform.position.y.ToString("0.##") + "," + cam.transform.position.z.ToString("0.##") + ")/" + cam.transform.eulerAngles.x.ToString("0.#"));
            }
            if (cam != null && SynergyUI.BattleBoardGrid.Ready)
            {
                int pickOk = 0, blocked = 0;
                foreach (Transform tile in SynergyUI.BattleBoardGrid.Root.transform)
                {
                    var sp = cam.WorldToScreenPoint(tile.position + Vector3.up * 0.5f);
                    if (sp.z < 0) continue;
                    RaycastHit hit;
                    if (Physics.Raycast(cam.ScreenPointToRay(sp), out hit, 300f) &&
                        hit.collider != null && hit.collider.gameObject.name == tile.name)
                    {
                        if (tile.name == "tile-0-0" || tile.name == "tile-5-2" || tile.name == "tile-7-6") pickOk++;
                    }
                    else blocked++;
                }
                sb.Append(" pick=" + pickOk + "/3 tileBlocked=" + blocked + "/55");
            }
            if (cam != null)
            {
                var bowlGo = GameObject.Find("bowl-terrain");
                MeshCollider tempCol = null;
                if (bowlGo != null)
                {
                    tempCol = bowlGo.AddComponent<MeshCollider>();
                    var mf = bowlGo.GetComponent<MeshFilter>();
                    if (mf != null) tempCol.sharedMesh = mf.sharedMesh;
                }
                int covered = 0, total = 0;
                for (int i = 0; i < 72; i++)
                {
                    float t = i / 72f * 4f;
                    float px, py;
                    if (t < 1f) { px = t * Screen.width; py = 2f; }
                    else if (t < 2f) { px = Screen.width - 2f; py = (t - 1f) * Screen.height; }
                    else if (t < 3f) { px = (3f - t) * Screen.width; py = Screen.height - 2f; }
                    else { px = 2f; py = (4f - t) * Screen.height; }
                    total++;
                    RaycastHit hit;
                    if (Physics.Raycast(cam.ScreenPointToRay(new Vector2(px, py)), out hit, 600f)) covered++;
                }
                if (tempCol != null) UnityEngine.Object.DestroyImmediate(tempCol);
                sb.Append(" edgeCover=" + covered + "/" + total);
            }
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int lit = 0;
            foreach (var l in lights) if (l.isActiveAndEnabled) lit++;
            var mapRoot = SynergyUI.BattleStageDirector.MapRoot;
            var stage = mapRoot != null ? FindChild(mapRoot, "BattleStage") : null;
            var env = stage != null ? stage.Find("Env") : null;
            int envTris = 0, envRends = 0, envCols = 0;
            if (env != null)
            {
                foreach (var r in env.GetComponentsInChildren<MeshRenderer>(false))
                {
                    envRends++;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) envTris += mf.sharedMesh.triangles.Length / 3;
                }
                envCols = env.GetComponentsInChildren<Collider>(true).Length;
            }
            sb.Append(" lights=" + lit + " envColliders=" + envCols + " envTris=" + envTris + " envRenderers=" + envRends);
            sb.Append(" stats(draws=" + UnityEditor.UnityStats.drawCalls + ",tris=" + UnityEditor.UnityStats.triangles + ")");
            return sb.ToString();
        }

        /// <summary>棋盘 55 格中心屏幕投影包围（归一化）——棋盘占画面读数。</summary>
        private static Rect BoardScreenBounds(Camera cam)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (Transform tile in SynergyUI.BattleBoardGrid.Root.transform)
            {
                var sp = cam.WorldToScreenPoint(tile.position);
                if (sp.x < x0) x0 = sp.x;
                if (sp.x > x1) x1 = sp.x;
                if (sp.y < y0) y0 = sp.y;
                if (sp.y > y1) y1 = sp.y;
            }
            return new Rect(x0 / Screen.width, y0 / Screen.height,
                (x1 - x0) / Screen.width, (y1 - y0) / Screen.height);
        }
    }
}
