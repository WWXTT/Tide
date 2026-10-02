using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tide.验证
{
    /// <summary>一次性布局诊断（EffectComposer 槽区 0 高取证）。</summary>
    public static class UguiLayoutProbe
    {
        [MenuItem("Tools/验证/UI布局探针")]
        public static void Run()
        {
            var sb = new StringBuilder("[UguiLayoutProbe]\n");
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t.name != "slot-title" && t.name != "slot") continue;
                var txt = t.GetComponentInChildren<TMP_Text>(true);
                var le = t.GetComponent<LayoutElement>();
                var rt = (RectTransform)t;
                if (txt != null)
                    sb.AppendLine($"  {t.name}: text=\"{txt.text}\" font={(txt.font != null ? txt.font.name : "NULL")} fontSize={txt.fontSize} " +
                        $"prefW={txt.preferredWidth} prefH={txt.preferredHeight} rect={rt.rect.width:0}x{rt.rect.height:0} " +
                        $"LE={(le != null ? $"prefH={le.preferredHeight} minH={le.minHeight} enabled={le.enabled}" : "none")}");
                else
                    sb.AppendLine($"  {t.name}: rect={rt.rect.width:0}x{rt.rect.height:0} " +
                        $"LE={(le != null ? $"prefW={le.preferredWidth} prefH={le.preferredHeight} minW={le.minWidth} minH={le.minHeight}" : "none")} " +
                        $"lg={(t.GetComponent<VerticalLayoutGroup>() != null ? "VLG ctrl=" + t.GetComponent<VerticalLayoutGroup>().childControlHeight : "-")}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
