using System.IO;
using System.Text;
using SynergyUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tide.验证
{
    /// <summary>
    /// 效果表渲染探针（2026-10-03 数据层诊断）：编辑模式下实例化 EffectUI.prefab
    /// （HideAndDontSave，不入场景），量 right 列面板几何 + 模拟 RefreshEffectsList 的
    /// 模板行克隆与文本填充 + 布局重建后 content 高度——定位"效果表显示空"的渲染侧断点。
    /// 结果写 Temp/EffectsListProbe.txt（Console 可能被过滤器挡住，以文件为准）。
    /// 用法：Tools/验证/效果表渲染探针。
    /// </summary>
    public static class EffectsListProbe
    {
        private const string MenuPath = "Tools/验证/效果表渲染探针";
        private const string ReportPath = "Temp/EffectsListProbe.txt";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var sb = new StringBuilder("[EffectsListProbe]\n");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiKit.PrefabFolder + "EffectUI.prefab");
            if (prefab == null)
            {
                File.WriteAllText(ReportPath, "[EffectsListProbe] prefab 加载失败");
                return;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var root = (RectTransform)inst.transform;
                var effectsPanel = UiKit.FindDeep(root, "effects-panel");
                var atomPanel = UiKit.FindDeep(root, "atom-panel");
                var right = (atomPanel.parent as RectTransform) ?? UiKit.FindDeep(root, "right");
                sb.AppendLine("== right 直属子物体几何 ==");
                foreach (RectTransform ch in right)
                {
                    var r = ch.rect;
                    sb.AppendLine($"  {ch.name}: active={ch.gameObject.activeSelf} " +
                        $"anchors=({ch.anchorMin.x:0.##},{ch.anchorMin.y:0.##})-({ch.anchorMax.x:0.##},{ch.anchorMax.y:0.##}) " +
                        $"pos={ch.anchoredPosition} sizeDelta={ch.sizeDelta} rect={r.width:0.#}x{r.height:0.#}");
                }
                if (effectsPanel != null) sb.AppendLine($"effects-panel.activeSelf={effectsPanel.gameObject.activeSelf}");
                if (atomPanel != null) sb.AppendLine($"atom-panel.activeSelf={atomPanel.gameObject.activeSelf}");

                var sr = UiKit.FindDeep(root, "list-effects")?.GetComponent<ScrollRect>();
                sb.AppendLine($"list-effects: ScrollRect={(sr != null)} content={(sr != null && sr.content != null ? sr.content.name : "<null>")}");
                if (sr != null && sr.content != null)
                {
                    var content = sr.content;
                    var tplProbe = content.Find("row");
                    sb.AppendLine($"content.children={content.childCount} 模板row={(tplProbe != null ? (tplProbe.gameObject.activeSelf ? "激活(违规)" : "不激活(合规)") : "缺失")}");
                    for (int i = 0; i < 3; i++)
                    {
                        var row = UiKit.CloneTemplate("row", content);
                        if (row == null) { sb.AppendLine($"clone#{i}=NULL"); break; }
                        var nameT = row.Find("name")?.GetComponentInChildren<TMP_Text>(true);
                        var metaT = row.Find("meta")?.GetComponentInChildren<TMP_Text>(true);
                        var tmps = row.GetComponentsInChildren<TMP_Text>(true);
                        if (nameT == null && tmps.Length > 0) nameT = tmps[0];
                        if (metaT == null && tmps.Length > 1) metaT = tmps[1];
                        if (nameT != null) nameT.text = "测试效果" + i;
                        if (metaT != null) metaT.text = "ABCD123" + i;
                        sb.AppendLine($"clone#{i}: nameTMP={(nameT != null ? nameT.name : "<无>")} metaTMP={(metaT != null ? metaT.name : "<无>")} " +
                            $"rowRect={row.rect.width:0.#}x{row.rect.height:0.#} active={row.gameObject.activeSelf}");
                    }
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                    sb.AppendLine($"布局重建后: content.children={content.childCount} contentRect={content.rect.width:0.#}x{content.rect.height:0.#} " +
                        $"viewport={(sr.viewport != null ? sr.viewport.rect.width.ToString("0.#") + "x" + sr.viewport.rect.height.ToString("0.#") : "-")}");
                    foreach (RectTransform ch in content)
                        sb.AppendLine($"  child {ch.name}: active={ch.gameObject.activeSelf} y={ch.anchoredPosition.y:0.#} h={ch.rect.height:0.#}");
                }
            }
            finally
            {
                Object.DestroyImmediate(inst);
                Directory.CreateDirectory("Temp");
                File.WriteAllText(ReportPath, sb.ToString());
            }
        }
    }
}
