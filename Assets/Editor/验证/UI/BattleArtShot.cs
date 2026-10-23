using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
                }
                finally
                {
                    if (camPose.HasValue && cam != null)
                        cam.transform.SetPositionAndRotation(camPose.Value.position, camPose.Value.rotation);
                    foreach (var c in off) if (c != null) c.enabled = true;
                }

                bool boardReady = SynergyUI.BattleBoardGrid.Ready && !SynergyUI.BattleBoardAssemble.Running;
                Debug.Log($"[ArtShot] path={uiPath} ; {envPath} boardReady={boardReady} tiles={tiles}" +
                          $" tileScale=({tileScale.x:0.###},{tileScale.y:0.###},{tileScale.z:0.###})" +
                          $" cam=({camInfo}) envObjects={envObjects} popups={(popupsClear ? "clear" : "REMAIN!")} engaged={_engaged}");
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
    }
}
