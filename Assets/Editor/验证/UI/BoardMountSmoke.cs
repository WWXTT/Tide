using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HexMap;
using SynergyUI;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tide.验证
{
    /// <summary>
    /// 棋盘装台冒烟（2026-10-03，数据层证据）：主菜单 → 本地对战（吞屏过渡）→
    /// 等 HexBoardBinding 就绪 → 断言棋盘矩形 104 格全部存在/平整(elevation=0)/纯色层(terrainIndex)、
    /// 相机对盘取景、占位标记 54 个（单位 36+地牌 18）挂在棋盘根下 → 返回主菜单后
    /// 棋盘根/绑定/标记全清、黑洞复现。用法：进 Play 后执行菜单 Tools/验证/棋盘装台冒烟。
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

                // 1. 进本地对战（吞屏 0.5s 后切屏，BattleScreen.OnEnter 装台）
                if (!ClickNamed("btn-battle")) { Fail("主菜单本地对战按钮缺失"); return; }

                var binding = await WaitForBoardBinding(40f);
                if (binding == null) { Fail("装台超时：HexBoardBinding 未就绪（棋盘装载链未跑通）"); return; }

                // 2. 棋盘矩形 104 格：存在/平整/纯色层（独立于 SelfCheck 复算一遍）
                int total = 0, missing = 0, notFlat = 0, wrongTerrain = 0;
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                for (int z = 0; z < binding.Height; z++)
                {
                    for (int x = 0; x < binding.Width; x++)
                    {
                        total++;
                        if (!binding.TryGetCell(x, z, out var e)) { missing++; continue; }
                        var cd = em.GetComponentData<HexCellData>(e);
                        if (cd.Elevation != 0) notFlat++;
                        if (cd.TerrainIndex != binding.BoardTerrainIndex) wrongTerrain++;
                    }
                }
                sb.AppendLine($"  step2 棋盘矩形：{binding.Width}×{binding.Height}={total} 缺失={missing} 不平整={notFlat} 非纯色层={wrongTerrain}");
                if (missing + notFlat + wrongTerrain > 0)
                { Fail($"棋盘矩形不达标（缺失{missing}/不平整{notFlat}/非纯色{wrongTerrain}）——纯色层装配工具跑了吗？"); return; }

                // 3. 绑定层形状与逻辑棋盘一致（13×8）
                bool shapeOk = binding.Width == GameBoard.BoardLayout.Width && binding.Height == GameBoard.BoardLayout.Height;
                sb.AppendLine($"  step3 矩形形状：{binding.Width}×{binding.Height} vs 逻辑 {GameBoard.BoardLayout.Width}×{GameBoard.BoardLayout.Height} → {(shapeOk ? "一致" : "不一致!")}");
                if (!shapeOk) { Fail("棋盘矩形与逻辑棋盘 13×8 不一致（检查 BattleHexMapSettings.boardRegion）"); return; }

                // 4. 占位标记：54 个挂在棋盘根下
                var markersParent = FindTransform("HexBoardMarkers");
                int markers = markersParent != null ? markersParent.childCount : 0;
                int unitMarkers = CountNamed(markersParent, "marker-unit-");
                int landMarkers = CountNamed(markersParent, "marker-land-");
                sb.AppendLine($"  step4 占位标记：总数={markers}（单位 {unitMarkers} + 地牌 {landMarkers}）");
                if (unitMarkers != 36 || landMarkers != 18) { Fail($"占位标记数量异常（单位{unitMarkers}/36 地牌{landMarkers}/18）"); return; }

                // 5. 全图外围装载（32×32 全量在档）
                var streaming = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<HexChunkStreamingSystem>();
                int mapCells = streaming.CellLookup.Count;
                sb.AppendLine($"  step5 地图装载：{mapCells} 格（期望 {HexMapRuntime.CellCount.x}×{HexMapRuntime.CellCount.y}={HexMapRuntime.CellCount.x * HexMapRuntime.CellCount.y}）");

                // 6. 返回主菜单：拆台全清 + 黑洞复现
                if (!ClickNamed("btn-back")) { Fail("返回按钮缺失"); return; }
                await Task.Delay(800);
                bool boardGone = FindTransform("HexBoardMarkers") == null && FindTransform("HexBoardRoot") == null;
                bool bindGone = HexBoardBinding.Current == null;
                var bh = GameObject.Find("BlackHole");
                bool bhBack = bh != null && bh.activeInHierarchy;
                sb.AppendLine($"  step6 拆台：棋盘根清={boardGone} 绑定清={bindGone} 黑洞复现={bhBack}");
                if (!boardGone || !bindGone || !bhBack) { Fail("拆台未清干净或黑洞未复现"); return; }

                sb.Insert(0, "[PASS]\n");
                Debug.Log(sb.ToString());
            }
            catch (System.Exception ex)
            {
                Fail($"异常：{ex}");
            }
        }

        private static async Task<HexBoardBinding> WaitForBoardBinding(float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (HexBoardBinding.Current != null && HexBoardBinding.Current.AllCellsReady)
                    return HexBoardBinding.Current;
                await Task.Delay(200);
            }
            return null;
        }

        private static int CountNamed(Transform parent, string prefix)
        {
            if (parent == null) return 0;
            int n = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name.StartsWith(prefix))
                    n++;
            return n;
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

        private static void Fail(string why) => Debug.LogError($"[BoardMountSmoke][FAIL] {why}");
    }
}
