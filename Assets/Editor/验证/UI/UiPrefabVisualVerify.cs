using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tide.验证
{
    /// <summary>
    /// UI 预制体视觉验证（2026-10-02 UI 系统性修复配套，幂等可重跑）：
    /// 编辑器态把 Assets/Art/UI 下每个屏幕 prefab 实例化到隔离画布，强制布局后真实渲染
    /// 成 PNG（Temp/ui_verify/&lt;名&gt;.png）并统计 TMP_SubMeshUI 数量——SubMeshUI&gt;0 说明
    /// 文本仍走 fallback 字体渲染（动态图集容量不足或缺字），是字体侧根因修复的验收门。
    /// 列表区为空属预期（运行时进屏由代码填充，预制体只留容器）。
    /// </summary>
    public static class UiPrefabVisualVerify
    {
        private static readonly string[] Paths =
        {
            "Assets/Art/UI/MainUI.prefab",
            // BattleUI.prefab 已删除（2026-10-02 战场定案 3D 重做）
            "Assets/Art/UI/CardUI.prefab",
            "Assets/Art/UI/DeckUI.prefab",
            "Assets/Art/UI/EffectUI.prefab",
            "Assets/Art/UI/ACard.prefab",
        };

        [MenuItem("Tools/验证/UI预制体视觉验证")]
        public static void Run()
        {
            Directory.CreateDirectory("Temp/ui_verify");
            var sb = new System.Text.StringBuilder();
            foreach (var path in Paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    sb.AppendLine(Path.GetFileName(path) + " -> 加载失败");
                    continue;
                }
                sb.AppendLine(VerifyOne(path, asset));
            }
            Debug.Log("[UI视觉验证] " + sb.ToString());
        }

        /// <summary>单 prefab 验证：实例化挂到场景 UIBootstrap 画布（复刻 ManageUGui.Capture
        /// 的借用-恢复流程——自建画布在编辑器态对部分 prefab 不出画面），渲染 PNG + 统计 SubMeshUI。</summary>
        private static string VerifyOne(string path, GameObject asset)
        {
            var sceneCanvas = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.isRootCanvas && c.name == "UIBootstrap");
            if (sceneCanvas == null)
                return Path.GetFileName(path) + " -> 场景缺 UIBootstrap 画布，跳过";

            int uiLayer = LayerMask.NameToLayer("UI");
            var camGo = new GameObject("__ui_verify_cam__") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture rt = null;
            Texture2D shot = null;
            GameObject inst = null;
            var oldMode = sceneCanvas.renderMode;
            var oldCam = sceneCanvas.worldCamera;
            var oldPlane = sceneCanvas.planeDistance;
            var oldScale = sceneCanvas.scaleFactor;
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 1000f;
                cam.enabled = false;
                cam.cullingMask = 1 << uiLayer;

                sceneCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                sceneCanvas.worldCamera = cam;
                sceneCanvas.planeDistance = 100f;

                inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                var r = (RectTransform)inst.transform;
                r.SetParent(sceneCanvas.transform, false);
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
                if (path.EndsWith("ACard.prefab"))
                {
                    // 卡面模板非全屏：屏幕中央 280x380 摆一张
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                    r.pivot = new Vector2(0.5f, 0.5f);
                    r.anchoredPosition = Vector2.zero;
                    r.sizeDelta = new Vector2(280f, 380f);
                }
                SetLayerRecursive(r, uiLayer);
                sceneCanvas.scaleFactor = 1f;

                // 预光栅：TMP 动态字体首次请求字形的那一帧渲染为空——渲染前把实例全部
                // 文本字符 TryAddCharacters 进各自字体图集，规避单帧截图抓到空字
                var glyphCache = new Dictionary<TMP_FontAsset, HashSet<char>>();
                foreach (var t in inst.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.font == null || string.IsNullOrEmpty(t.text)) continue;
                    if (!glyphCache.TryGetValue(t.font, out var set)) glyphCache[t.font] = set = new HashSet<char>();
                    foreach (var c in t.text) if (!char.IsWhiteSpace(c)) set.Add(c);
                }
                foreach (var kv in glyphCache) kv.Key.TryAddCharacters(kv.Value.Select(c => (uint)c).ToArray());

                const int w = 1920, h = 1080;
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;

                // 编辑器态同步顺序（CanvasScaler/LayoutGroup 不 tick）：Force→Rebuild→Force，双渲染
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(sceneCanvas.transform as RectTransform);
                Canvas.ForceUpdateCanvases();
                cam.Render(); // 第一次：驱动 TMP 完成文本网格生成
                Canvas.ForceUpdateCanvases();
                cam.Render(); // 第二次：图集纹理已上传，截图拿到有字的画面

                int subMesh = inst.GetComponentsInChildren<TMP_SubMeshUI>(true).Length;
                int tmpCount = inst.GetComponentsInChildren<TMP_Text>(true).Length;
                int graphCount = 0;
                foreach (var g in inst.GetComponentsInChildren<Graphic>(true)) if (g.enabled) graphCount++;

                // 逐文本取证：渲染后 vertexCount=0 说明网格未生成（填充问题），>0 不可见则是位置/裁剪问题
                var dbg = new System.Text.StringBuilder();
                foreach (var t in inst.GetComponentsInChildren<TMP_Text>(true))
                {
                    var wc = new Vector3[4];
                    t.rectTransform.GetWorldCorners(wc);
                    dbg.Append(t.name).Append("[").Append(string.IsNullOrEmpty(t.text) ? "<空>" : t.text.Length > 10 ? t.text.Substring(0, 10) : t.text)
                        .Append("]chars=").Append(t.textInfo != null ? t.textInfo.characterCount : -1)
                        .Append(",cull=").Append(t.canvasRenderer.cull)
                        .Append("@y").Append(wc[0].y.ToString("F0")).Append("-").Append(wc[1].y.ToString("F0"))
                        .Append(",x").Append(wc[0].x.ToString("F0")).Append("-").Append(wc[3].x.ToString("F0"))
                        .Append("; ");
                    if (dbg.Length > 1200) { dbg.Append("…"); break; }
                }
                Debug.Log("[UI视觉验证-文本] " + Path.GetFileNameWithoutExtension(path) + "：" + dbg.ToString());

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                shot = new Texture2D(w, h, TextureFormat.RGBA32, false);
                shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                shot.Apply();
                RenderTexture.active = prev;

                var name = Path.GetFileNameWithoutExtension(path);
                File.WriteAllBytes("Temp/ui_verify/" + name + ".png", shot.EncodeToPNG());
                Debug.Log("[UI视觉验证] " + name + "：TMP=" + tmpCount + "，SubMeshUI=" + subMesh
                    + (subMesh > 0 ? " ⚠仍有fallback渲染" : " ✓")
                    + $"，active={inst.activeSelf}/{inst.activeInHierarchy}，rootRect={r.rect.size}"
                    + $"，graphics={graphCount}");
                return name + "：TMP=" + tmpCount + "，SubMeshUI=" + subMesh
                    + (subMesh > 0 ? " ⚠仍有fallback渲染" : " ✓") + "，png=Temp/ui_verify/" + name + ".png";
            }
            finally
            {
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                sceneCanvas.renderMode = oldMode;
                sceneCanvas.worldCamera = oldCam;
                sceneCanvas.planeDistance = oldPlane;
                sceneCanvas.scaleFactor = oldScale;
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        private static void SetLayerRecursive(RectTransform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i) as RectTransform, layer);
        }
    }
}
