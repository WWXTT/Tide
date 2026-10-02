using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tide.验证
{
    /// <summary>
    /// UGUI 层级尺寸 Dump（2026-10-01 UITK→UGUI 迁移验证工具）：
    /// 打印运行时 Canvas 树每个节点的 rect 尺寸/位置——布局全 0、错位问题的取证口。
    /// 用法：进 Play 后执行菜单 Tools/验证/UI层级尺寸Dump，读 Console 输出。
    /// </summary>
    public static class UguiHierarchyDump
    {
        private const string MenuPath = "Tools/验证/UI层级尺寸Dump";

        [MenuItem(MenuPath)]
        public static void Dump()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[UguiDump] 场景中无 Canvas（未进 Play 或 UI 未装配）");
                return;
            }

            // 多画布全打（按 sortingOrder 排序）
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(canvases, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            var sb = new StringBuilder();
            foreach (var c in canvases)
            {
                sb.AppendLine($"[UguiDump] Canvas={c.name} order={c.sortingOrder} mode={c.renderMode} scale={c.transform.lossyScale.x:F2}");
                Walk(c.transform, 1, sb);
            }
            Debug.Log(sb.ToString());
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            var rt = t as RectTransform;
            sb.AppendLine($"{new string(' ', depth * 2)}{t.name} : " +
                (rt != null
                    ? $"{Mathf.RoundToInt(rt.rect.width)}x{Mathf.RoundToInt(rt.rect.height)} @({Mathf.RoundToInt(rt.anchoredPosition.x)},{Mathf.RoundToInt(rt.anchoredPosition.y)})"
                    : "-"));
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1, sb);
        }
    }
}
