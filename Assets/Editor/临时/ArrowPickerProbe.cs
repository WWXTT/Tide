using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tide.EditorTools
{
    /// <summary>一次性运行时探查（2026-10-06 arrow-picker 变色排查）：Play 态扫描全部已加载场景的
    /// arrow-picker / arrows 盒，打印按钮与 tri 的颜色/启用态/CanvasGroup/射线命中栈；
    /// 对每个方向按钮 onClick.Invoke() 两次（toggle 语义，净状态还原），观察两次间 tri 颜色是否翻转。
    /// 报告落 Temp/ArrowPickerProbe.txt。</summary>
    public static class ArrowPickerProbe
    {
        [MenuItem("Tools/临时/箭头Picker运行时探查")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("play=" + Application.isPlaying);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                sb.AppendLine("scene[" + i + "]=" + s.name + " loaded=" + s.isLoaded);
            }

            var all = Object.FindObjectsOfType<RectTransform>(true);
            var pickers = new List<RectTransform>();
            var boxes = new List<RectTransform>();
            foreach (var rt in all)
            {
                if (rt.name == "arrow-picker") pickers.Add(rt);
                if (rt.name == "arrows" || rt.name == "tpl-arrows") boxes.Add(rt);
            }
            sb.AppendLine("arrow-picker 实例数=" + pickers.Count + "；arrows/tpl-arrows 盒数=" + boxes.Count);
            foreach (var box in boxes)
                sb.AppendLine("  盒 " + box.name + " activeSelf=" + box.gameObject.activeSelf
                              + " activeInHierarchy=" + box.gameObject.activeInHierarchy
                              + " path=" + Ancestors(box));

            var es = Object.FindObjectOfType<EventSystem>();
            foreach (var picker in pickers)
            {
                sb.AppendLine("== picker path=" + Ancestors(picker)
                              + " activeSelf=" + picker.gameObject.activeSelf
                              + " activeInHierarchy=" + picker.gameObject.activeInHierarchy);
                foreach (var g in picker.GetComponentsInParent<CanvasGroup>(true))
                    sb.AppendLine("  CanvasGroup@" + g.name + " alpha=" + g.alpha
                                  + " blocksRaycasts=" + g.blocksRaycasts + " interactable=" + g.interactable);
                var canvas = picker.GetComponentInParent<Canvas>(true);
                sb.AppendLine("  canvas=" + (canvas != null
                    ? canvas.name + " enabled=" + canvas.enabled + " active=" + canvas.gameObject.activeInHierarchy
                    : "无"));

                foreach (var d in new[] { "R-T", "R", "R-D", "L-D", "L", "L-T" })
                {
                    var node = FindDeep(picker, d);
                    if (node == null) { sb.AppendLine("  " + d + "：节点缺失"); continue; }
                    var btn = node.GetComponent<Button>();
                    var tri = FindDeep(node, "tri");
                    var triImg = tri != null ? tri.GetComponent<Image>() : null;
                    var bg = node.GetComponent<Image>();
                    sb.AppendLine("  " + d + " activeInHierarchy=" + node.gameObject.activeInHierarchy
                                  + " btn=" + (btn == null ? "无" : "interactable=" + btn.interactable
                                      + " transition=" + btn.transition
                                      + " target=" + (btn.targetGraphic != null ? btn.targetGraphic.name : "null"))
                                  + " | bg=" + ImgDesc(bg)
                                  + " | tri=" + ImgDesc(triImg)
                                  + " cr=" + (triImg != null ? Hex(triImg.canvasRenderer.GetColor()) : "-"));
                    if (btn == null || triImg == null) continue;

                    var c0 = triImg.color;
                    btn.onClick.Invoke();
                    var c1 = triImg.color;
                    btn.onClick.Invoke();
                    var c2 = triImg.color;
                    sb.AppendLine("    Invoke×2: tri " + Hex(c0) + " → " + Hex(c1) + " → " + Hex(c2)
                                  + (c0 == c1 ? "  【不变色】" : "  【变色正常】"));
                    DumpTopHits(sb, es, node);
                }
            }
            if (pickers.Count == 0) sb.AppendLine("（无 arrow-picker 实例——若正在 Play：界面未打开或绑定名不符）");
            System.IO.File.WriteAllText("Temp/ArrowPickerProbe.txt", sb.ToString());
            Debug.Log("[ArrowPickerProbe] 报告已写 Temp/ArrowPickerProbe.txt\n" + sb);
        }

        private static string ImgDesc(Image img)
        {
            if (img == null) return "无";
            return (img.enabled ? "on" : "OFF") + " color=" + Hex(img.color)
                   + " sprite=" + (img.sprite != null ? img.sprite.name : "null")
                   + " mat=" + (img.material != null ? img.material.name : "null");
        }

        private static RectTransform FindDeep(RectTransform root, string name)
        {
            var direct = root.Find(name);
            if (direct != null) return (RectTransform)direct;
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
                if (t != root && t.name == name) return t;
            return null;
        }

        /// <summary>按钮中心点的 GraphicRaycaster 命中栈（首位=实际接收点击的图形）。</summary>
        private static void DumpTopHits(StringBuilder sb, EventSystem es, RectTransform node)
        {
            if (es == null) { sb.AppendLine("    命中：EventSystem 缺失"); return; }
            var canvas = node.GetComponentInParent<Canvas>(true);
            var raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            if (raycaster == null) { sb.AppendLine("    命中：GraphicRaycaster 缺失"); return; }
            var sp = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, node.position);
            var ped = new PointerEventData(es) { position = sp };
            var results = new List<RaycastResult>();
            raycaster.Raycast(ped, results);
            var names = new List<string>();
            for (int i = 0; i < results.Count && i < 4; i++)
            {
                var r = results[i];
                names.Add((i == 0 ? "TOP:" : i + ":") + r.gameObject.name
                          + (r.gameObject.transform.parent != null ? "@" + r.gameObject.transform.parent.name : ""));
            }
            sb.AppendLine("    命中(" + sp.x.ToString("F0") + "," + sp.y.ToString("F0") + ") "
                          + (names.Count > 0 ? string.Join(" | ", names) : "空——点不到"));
        }

        private static string Ancestors(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
            return sb.ToString();
        }

        private static string Hex(Color c)
            => "#" + ColorUtility.ToHtmlStringRGB(c) + "(" + c.a.ToString("F2") + ")";
    }
}
