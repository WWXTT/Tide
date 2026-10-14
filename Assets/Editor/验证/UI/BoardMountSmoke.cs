using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoard;
using SynergyUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tide.验证
{
    /// <summary>
    /// 棋盘装台冒烟（2026-10-13 缩盘定案二段：BattleHex 单枚 mesh 运行时拼装 9×6 全盘）：
    /// 主菜单 → 本地对战（吞屏过渡）→ 等棋盘实例 + 浮起拼装落位（Running 门禁）→ 断言：
    /// ① BattleBoardRoot 实例 + 拼装规格（54 tile=9×6 全盘；逐 tile mesh=BattleHex 六棱柱 38 顶点/72 索引+材质非空）；
    /// ② 分区对拍（BoardLayout 独立复算核心带 zone 直方图 + 逐 tile 材质色与 DefaultColor 对拍）；
    /// ③ 逐 tile 世界坐标与格心公式独立复算对拍（54 格）；
    /// ④ BattleFieldView 英雄柱落位（双方角色格世界坐标——缩盘后角色格入盘，柱落盘内格位）；
    /// ⑤ 像素可见性（GameView 真实帧读采样格中心非黑——2026-10-13 教训：
    ///    HexMap 时代 BRG 批次整体不渲染而数据层冒烟全绿，从此可见性必须入冒烟）；
    /// ⑥ 返回主菜单后棋盘实例全清 + 格位服务复位 + 黑洞复现。
    /// 结果同步落盘 Temp/BoardMountSmoke.txt（并行会话清 Console 防丢证据）。
    /// 用法：进 Play 后执行菜单 Tools/验证/棋盘装台冒烟。
    /// </summary>
    public static class BoardMountSmoke
    {
        private const string MenuPath = "Tools/验证/棋盘装台冒烟";

        [MenuItem(MenuPath)]
        public static async void Run()
        {
            var sb = new StringBuilder("[BoardMountSmoke]\n");
            try
            {
                // 0. 回主菜单（上次冒烟中断可能停在对战场）
                for (int i = 0; i < 5 && FindTransform("main-menu") == null; i++)
                {
                    if (!ClickNamed("btn-back")) break;
                    await Task.Delay(600);
                }
                if (FindTransform("main-menu") == null) { Fail("无法回到主菜单（前置态异常）"); return; }

                // 1. 进本地对战（吞屏 0.5s 后切屏，BattleScreen.OnEnter 装台——同步即就绪）
                if (!ClickNamed("btn-battle")) { Fail("主菜单本地对战按钮缺失"); return; }

                float deadline = Time.realtimeSinceStartup + 30f;
                while (BattleBoardGrid.Root == null && Time.realtimeSinceStartup < deadline)
                    await Task.Delay(100);
                if (BattleBoardGrid.Root == null) { Fail("装台超时：BattleBoardGrid.Root 未就绪"); return; }

                // 1.5 等浮起拼装落位（2026-10-13 棋格从画外浮起：动画期间 tile 位姿在飞行中，
                //     不与格心公式对拍；窗口 2s+8s 兜底，超时即红——未落位/死循环都拦在这）
                float asmDeadline = Time.realtimeSinceStartup + BattleBoardAssemble.WindowSeconds + 8f;
                while (BattleBoardAssemble.Running && Time.realtimeSinceStartup < asmDeadline)
                    await Task.Delay(100);
                if (BattleBoardAssemble.Running) { Fail("浮起拼装超时未落位（Running 恒真）"); return; }

                var root = BattleBoardGrid.Root;
                // 2. 拼装规格：9×6=54 tile；逐 tile 名可解析（tile-{x}-{z}）、mesh=BattleHex（六棱柱 38 顶点/72 索引）、材质非空
                var tiles = new List<(int x, int z, Transform t)>();
                int meshBad = 0, matBad = 0;
                foreach (Transform child in root)
                {
                    var f = child.GetComponent<MeshFilter>();
                    var r = child.GetComponent<MeshRenderer>();
                    var parts = child.name.Split('-');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int tx) && int.TryParse(parts[2], out int tz)
                        && f != null && f.sharedMesh != null && f.sharedMesh.vertexCount == 38
                        && f.sharedMesh.triangles.Length == 72)
                        tiles.Add((tx, tz, child));
                    else meshBad++;
                    if (r == null || r.sharedMaterial == null) matBad++;
                }
                bool shapeOk = tiles.Count == 54 && meshBad == 0 && matBad == 0;
                sb.AppendLine($"  step2 拼装：tile={tiles.Count}(期望54) mesh异常={meshBad} 缺材质={matBad} → {(shapeOk ? "一致" : "不符!")}");
                if (!shapeOk) { Fail("拼装规格与 9×6 全盘不符（mesh=BattleHex 六棱柱口径）"); return; }

                // 3. 分区对拍：全盘 zone 直方图（BoardLayout 独立复算）+ 逐 tile 材质色与 DefaultColor 对拍
                var expectHist = new Dictionary<byte, int>();
                for (int z = 0; z < BoardLayout.Height; z++)
                    for (int x = 0; x < BoardLayout.Width; x++)
                    {
                        byte zone = BattleBoardZones.ZoneOf(BoardLayout.RoleOf(x, z), BoardLayout.OwnerOf(x, z));
                        expectHist[zone] = expectHist.TryGetValue(zone, out var n) ? n + 1 : 1;
                    }
                int zoneBad = 0, colorBad = 0;
                var histText = new StringBuilder();
                foreach (var kv in expectHist) histText.Append($"z{kv.Key}:{kv.Value} ");
                foreach (var (x, z, t) in tiles)
                {
                    byte zone = BattleBoardZones.ZoneOf(BoardLayout.RoleOf(x, z), BoardLayout.OwnerOf(x, z));
                    if (!expectHist.ContainsKey(zone)) zoneBad++;
                    var m = t.GetComponent<MeshRenderer>().sharedMaterial;
                    var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                          : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.black;
                    var d = c - BattleBoardZones.DefaultColor(zone);
                    if (Mathf.Abs(d.r) + Mathf.Abs(d.g) + Mathf.Abs(d.b) > 0.003f) colorBad++;
                }
                sb.AppendLine($"  step3 分区对拍：{histText.ToString().Trim()} 格色偏差={colorBad} 未知分区={zoneBad}");
                if (zoneBad != 0 || colorBad != 0) { Fail("tile 分区/配色与 BoardLayout 对拍不符"); return; }

                // 4. 逐 tile 世界坐标公式对拍（54 格独立复算——摆位+格位服务全链）
                int posBad = 0;
                foreach (var (x, z, t) in tiles)
                {
                    var expect = root.TransformPoint(new Vector3(
                        (x + (z & 1) * 0.5f) * BattleBoardGrid.InnerRadius * 2f, 0f,
                        z * BattleBoardGrid.OuterRadius * 1.5f));
                    if ((t.position - expect).sqrMagnitude > 1e-6f) posBad++;
                }
                sb.AppendLine($"  step4 格心公式对拍：{tiles.Count} 格 偏差={posBad}");
                if (posBad != 0) { Fail("tile 摆位与格心公式复算不一致"); return; }

                // 5. 英雄柱落位（BattleFieldView 双方角色格）
                var fieldRoot = FindTransform("BattleFieldView");
                var heroSelf = FindTransform("hero-self");
                var heroOpp = FindTransform("hero-opp");
                int heroBad = 0;
                if (heroSelf == null || heroOpp == null) heroBad = 2;
                else
                {
                    var c0 = BoardLayout.CharacterCell(0);
                    var c1 = BoardLayout.CharacterCell(1);
                    if ((heroSelf.position - BattleBoardGrid.WorldPositionOf(c0.x, c0.z) - new Vector3(0f, 0.6f, 0f)).sqrMagnitude > 0.01f) heroBad++;
                    if ((heroOpp.position - BattleBoardGrid.WorldPositionOf(c1.x, c1.z) - new Vector3(0f, 0.6f, 0f)).sqrMagnitude > 0.01f) heroBad++;
                }
                sb.AppendLine($"  step5 英雄柱：fieldView={(fieldRoot != null)} 落位偏差={heroBad}");
                if (fieldRoot == null || heroBad != 0) { Fail("英雄柱缺失或落位偏差"); return; }

                // 6. 像素可见性（GameView 真实帧读格心非黑——防"数据对但没渲染"）
                await Task.Delay(400); // 等日出 tween 起效后的真实帧
                EditorApplication.QueuePlayerLoopUpdate();
                await Task.Delay(400);
                var cam = root.GetComponentInParent<Camera>() ?? FindCamera();
                if (cam == null) { Fail("世界相机未找到"); return; }
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                int darkCells = 0;
                foreach (var (x, z) in new[] { (1, 3), (7, 4), (1, 1), (7, 2), (8, 4) })
                {
                    var sp = cam.WorldToScreenPoint(BattleBoardGrid.WorldPositionOf(x, z) + Vector3.up * 0.05f);
                    var px = tex.GetPixel((int)sp.x, (int)sp.y);
                    if (px.r + px.g + px.b < 0.06f) darkCells++;
                }
                Object.DestroyImmediate(tex);
                sb.AppendLine($"  step6 像素可见性：5 格采样 全黑格数={darkCells}");
                if (darkCells > 0) { Fail($"棋盘有 {darkCells}/5 采样格在 GameView 帧中纯黑（渲染缺失）"); return; }

                // 7. 返回主菜单：拆台全清 + 黑洞复现
                if (!ClickNamed("btn-back")) { Fail("返回按钮缺失"); return; }
                await Task.Delay(800);
                bool boardGone = FindTransform("BattleBoardRoot") == null;
                bool gridDown = !BattleBoardGrid.Ready;
                var bh = GameObject.Find("BlackHole");
                bool bhBack = bh != null && bh.activeInHierarchy;
                sb.AppendLine($"  step7 拆台：棋盘根清={boardGone} 格位复位={gridDown} 黑洞复现={bhBack}");
                if (!boardGone || !gridDown || !bhBack) { Fail("拆台未清干净或黑洞未复现"); return; }

                sb.Insert(0, "[PASS]\n");
                Debug.Log(sb.ToString());
                WriteResult(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Fail($"异常：{ex}");
            }
        }

        private static Camera FindCamera()
        {
            return Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name != "UIBootstrap" && c.targetTexture == null && c.enabled);
        }

        /// <summary>结果落盘（并行会话清 Console 会吞掉 PASS/FAIL——Temp/*.txt 探针口径留证）。</summary>
        private static void WriteResult(string text)
        {
            try
            {
                string dir = System.IO.Path.Combine(Application.dataPath, "..", "Temp");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "BoardMountSmoke.txt"),
                    System.DateTime.Now.ToString("HH:mm:ss ") + text);
            }
            catch (System.Exception) { /* 落盘失败不影响验证本体 */ }
        }

        private static bool ClickNamed(string name)
        {
            var btn = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name);
            if (btn == null) return false;
            var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(btn.gameObject, ped, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private static Transform FindTransform(string name)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name);
        }

        private static void Fail(string why)
        {
            Debug.LogError($"[BoardMountSmoke][FAIL] {why}");
            WriteResult("[FAIL] " + why + "\n");
        }
    }
}
