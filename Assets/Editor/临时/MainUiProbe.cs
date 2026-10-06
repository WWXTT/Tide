using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tide.EditorTools
{
    /// <summary>一次性探查（2026-10-06 MainUI 入口按钮装配前置）：打印 MainUI.prefab 层级与按钮区布局结构，报告落 Temp/MainUiProbe.txt。</summary>
    public static class MainUiProbe
    {
        [MenuItem("Tools/临时/MainUI结构探查")]
        public static void Run()
        {
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Art/UI/MainUI.prefab");
            try
            {
                var root = (RectTransform)prefab.transform;
                var sb = new StringBuilder();
                sb.AppendLine("ROOT=" + root.name + " children=" + root.childCount);
                Transform btn = null;
                var stack = new System.Collections.Generic.Stack<Transform>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var t = stack.Pop();
                    for (int i = t.childCount - 1; i >= 0; i--)
                    {
                        var c = t.GetChild(i);
                        var lg = c.GetComponent<LayoutGroup>();
                        sb.AppendLine(c.parent.name + "/" + c.name
                            + (lg != null ? " [Layout:" + lg.GetType().Name + "]" : "")
                            + (c.GetComponent<Button>() != null ? " [Button]" : ""));
                        if (c.name == "btn-battle") btn = c;
                        stack.Push(c);
                    }
                }
                if (btn != null)
                {
                    var parent = btn.parent;
                    var lg2 = parent.GetComponent<LayoutGroup>();
                    sb.AppendLine("btn-battle parent=" + parent.name
                        + " layout=" + (lg2 != null ? lg2.GetType().Name : "无（绝对定位）") + " children=" + parent.childCount);
                    var rt = (RectTransform)btn;
                    sb.AppendLine("btn-battle anchors=" + rt.anchorMin + "/" + rt.anchorMax
                        + " size=" + rt.sizeDelta + " pos=" + rt.anchoredPosition + " pivot=" + rt.pivot);
                    var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                    sb.AppendLine("btn-battle TMP=" + (tmp != null
                        ? tmp.transform.parent.name + "/" + tmp.gameObject.name + " text=" + tmp.text
                        : "无"));
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var sib = parent.GetChild(i) as RectTransform;
                        sb.AppendLine("  sib " + i + ": " + sib.name + " pos=" + sib.anchoredPosition + " size=" + sib.sizeDelta);
                    }
                }
                else sb.AppendLine("btn-battle NOT FOUND");
                System.IO.File.WriteAllText("Temp/MainUiProbe.txt", sb.ToString());
                Debug.Log("[MainUiProbe] 报告已写 Temp/MainUiProbe.txt");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
    }
}
