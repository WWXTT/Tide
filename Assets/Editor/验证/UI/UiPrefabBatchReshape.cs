using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>
/// UI 预制体批量整形（2026-10-01 预制体化定案，幂等可重跑）：
///   1. 所有 TMP 文本字体 → SC-Heavy SDF（思源宋体 Heavy 静态图集，源 otf 已删但图集完整）；
///   2. LayoutElement 摘除——仅保留滚动区 content 子树内与 ignoreLayout=true 的（语义必需）；
///   3. MainUI 根摘除 Canvas/CanvasScaler/GraphicRaycaster（Canvas 归场景 UIBootstrap 独占，
///      屏幕预制体不允许嵌套 Canvas）；
///   4. BattleUI 删除 CardFallback 空壳残骸（旧卡面加载失败的兜底被误烘焙进预制体）；
///   5. SC-Heavy 字符覆盖自检——图集外字符打印清单（静态图集缺字预警）；
///   6. TMP Settings 默认字体 → SC-Heavy；LiberationSans SDF fallback 表补挂 SC-Heavy（漏网兜底）；
///   7. YooAsset 收集器 UI 组 CollectPath：已删除的 Assets/UI/Res → Assets/Art/UI。
/// </summary>
public static class UiPrefabBatchReshape
{
    private const string FontSdfPath =
        "Assets/Packages/TextMesh Pro/Resources/Fonts & Materials/SC-Heavy SDF.asset";
    private const string LiberationSdfPath =
        "Assets/Packages/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string TmpSettingsPath = "Assets/Packages/TextMesh Pro/Resources/TMP Settings.asset";
    private const string CollectorSettingPath = "Assets/BundleCollectorSetting.asset";

    private static readonly string[] PrefabPaths =
    {
        "Assets/Art/UI/MainUI.prefab",
        "Assets/Art/UI/BattleUI.prefab",
        "Assets/Art/UI/CardUI.prefab",
        "Assets/Art/UI/DeckUI.prefab",
        "Assets/Art/UI/EffectUI.prefab",
        "Assets/Art/UI/AtomicUI.prefab",
        "Assets/Art/UI/ACard.prefab",
    };

    [MenuItem("Tools/验证/UI预制体批量整形")]
    public static void Run()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdfPath);
        if (font == null)
        {
            Debug.LogError($"[整形] SC-Heavy SDF 缺失：{FontSdfPath}");
            return;
        }
        var fontChars = LoadFontChars(font);

        foreach (var path in PrefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[整形] 跳过（不存在）：{path}");
                continue;
            }
            ReshapePrefab(path, font, fontChars);
        }

        EnsureFallbackFont(font);
        ApplyTmpSettings(font);
        ApplyCollectorPath();
        AssetDatabase.SaveAssets();
        Debug.Log("[整形] 全部完成（字体/LE/Canvas/残骸/fallback/TMP设置/收集器）");
    }

    /// <summary>预制体结构取证：打印根下三层（含未激活）——剃层遗留/重复子树排查用。</summary>
    [MenuItem("Tools/验证/UI预制体结构Dump")]
    public static void DumpStructure()
    {
        foreach (var path in PrefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var sb = new System.Text.StringBuilder($"[Dump] {System.IO.Path.GetFileName(path)}\n");
                DumpNode(root.transform, 0, sb, 3);
                Debug.Log(sb.ToString());
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    private static void DumpNode(Transform t, int depth, System.Text.StringBuilder sb, int maxDepth)
    {
        if (depth > maxDepth) return;
        sb.AppendLine(new string(' ', depth * 2) + t.name + $" (ch={t.childCount})");
        for (int i = 0; i < t.childCount; i++)
            DumpNode(t.GetChild(i), depth + 1, sb, maxDepth);
    }

    private static void ReshapePrefab(string path, TMP_FontAsset font, HashSet<uint> fontChars)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 1. 字体全量换 SC-Heavy SDF + 文本组件强制启用（预制体捕获时 enabled 被误关——文本不可见根因）
            int fontSwaps = 0;
            int textEnabled = 0;
            var missingChars = new HashSet<char>();
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.font != font)
                {
                    t.font = font;
                    fontSwaps++;
                }
                if (!t.enabled)
                {
                    t.enabled = true;
                    textEnabled++;
                }
                foreach (var c in t.text ?? "")
                    if (!char.IsWhiteSpace(c) && !fontChars.Contains(c))
                        missingChars.Add(c);
            }

            // 2. LE 摘除：滚动 content 子树 + ignoreLayout 为保护区
            var keep = new HashSet<Transform>();
            foreach (var sr in root.GetComponentsInChildren<ScrollRect>(true))
            {
                if (sr.content == null) continue;
                keep.Add(sr.content);
                foreach (var tr in sr.content.GetComponentsInChildren<Transform>(true))
                    keep.Add(tr);
            }
            int leRemoved = 0, leKept = 0;
            foreach (var le in root.GetComponentsInChildren<LayoutElement>(true))
            {
                if (le == null) continue;
                if (le.ignoreLayout || keep.Contains(le.transform)) { leKept++; continue; }
                Object.DestroyImmediate(le);
                leRemoved++;
            }

            // 3. MainUI 根摘嵌套 Canvas 体系
            int canvasRemoved = 0;
            if (path.EndsWith("MainUI.prefab"))
            {
                foreach (var c in new Object[] { root.GetComponent<Canvas>(), root.GetComponent<CanvasScaler>(), root.GetComponent<GraphicRaycaster>() })
                    if (c != null) { Object.DestroyImmediate(c); canvasRemoved++; }
            }

            // 4. BattleUI 删 CardFallback 残骸
            int fallbackRemoved = 0;
            if (path.EndsWith("BattleUI.prefab"))
            {
                var doomed = new List<GameObject>();
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (tr.name == "CardFallback") doomed.Add(tr.gameObject);
                foreach (var go in doomed) { Object.DestroyImmediate(go); fallbackRemoved++; }
            }

            // EffectUI 残留 ScreenRoot 子节点取证（只记录不动——剃层遗留疑点）
            if (path.EndsWith("EffectUI.prefab"))
            {
                var stray = root.transform.Find("ScreenRoot");
                if (stray != null)
                {
                    var names = new List<string>();
                    for (int i = 0; i < stray.childCount; i++) names.Add(stray.GetChild(i).name);
                    Debug.Log($"[整形] EffectUI 存在残留 ScreenRoot 子节点：active={stray.gameObject.activeSelf}，子级=[{string.Join(",", names)}]");
                }
            }

            // EffectUI 剃层残留空壳清理：ScreenRoot→effect-composer 均无内容（无 TMP 文本）则整链删除
            // （实例根运行时会重命名为 effect-composer——留着会造成重名双节点）
            if (path.EndsWith("EffectUI.prefab"))
            {
                var stray = root.transform.Find("ScreenRoot");
                if (stray != null && stray.GetComponentsInChildren<TMP_Text>(true).Length == 0)
                {
                    Object.DestroyImmediate(stray.gameObject);
                    Debug.Log("[整形] EffectUI 删除残留空壳 ScreenRoot 子树");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[整形] {System.IO.Path.GetFileName(path)}：字体换 {fontSwaps}，文本启用 {textEnabled}，" +
                      $"LE 摘 {leRemoved}/留 {leKept}，Canvas 组件摘 {canvasRemoved}，CardFallback 删 {fallbackRemoved}" +
                      (missingChars.Count > 0 ? $"，⚠图集缺字：[{string.Join("", missingChars)}]" : "，缺字 0"));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private const string FallbackTtfPath = "Assets/Packages/TextMesh Pro/Fonts/庞门正道标题体.ttf";
    private const string FallbackSdfPath = "Assets/Packages/TextMesh Pro/Resources/Fonts & Materials/庞门正道标题体 SDF.asset";

    /// <summary>SC-Heavy 静态图集缺字兜底：项目内有源的庞门正道标题体做动态 SDF
    /// （按需光栅）挂进 SC-Heavy fallback 表——图集外字符（·（）「」—等标点与生僻卡名用字）不再空白。
    /// 注意：CreateFontAsset 的 material 与 atlasTexture 都必须 AddObjectToAsset 收进资产文件，
    /// 否则域重载后 m_AtlasTextures 悬空 → 渲染期 UnassignedReferenceException。</summary>
    private static void EnsureFallbackFont(TMP_FontAsset scHeavy)
    {
        var fb = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackSdfPath);
        bool broken = fb != null && fb.atlasTexture == null;
        if (broken)
        {
            Debug.LogWarning("[整形] 检测到坏兜底 SDF（图集纹理丢失）——删除重建");
            AssetDatabase.DeleteAsset(FallbackSdfPath);
            fb = null;
        }
        if (fb == null)
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(FallbackTtfPath);
            if (ttf == null)
            {
                Debug.LogWarning($"[整形] 兜底源字体缺失：{FallbackTtfPath}——SC-Heavy 图集外字符将渲染为空");
                return;
            }
            fb = TMP_FontAsset.CreateFontAsset(ttf);
            fb.atlasPopulationMode = AtlasPopulationMode.Dynamic; // 源字体在——缺字按需光栅
            AssetDatabase.CreateAsset(fb, FallbackSdfPath);
            if (fb.material != null) AssetDatabase.AddObjectToAsset(fb.material, fb);
            if (fb.atlasTexture != null) AssetDatabase.AddObjectToAsset(fb.atlasTexture, fb);
            AssetDatabase.SaveAssets();
            Debug.Log($"[整形] 创建兜底动态 SDF（含图集/材质子资产）：{FallbackSdfPath}");
        }

        // SC-Heavy fallback 表：清掉 null 残留（坏资产重建后旧引用悬空）再幂等追加
        var so = new SerializedObject(scHeavy);
        var table = so.FindProperty("m_FallbackFontAssetTable");
        if (table == null || !table.isArray) return;
        bool has = false;
        var keep = new List<Object>();
        for (int i = 0; i < table.arraySize; i++)
        {
            var e = table.GetArrayElementAtIndex(i).objectReferenceValue;
            if (e == null) continue;
            keep.Add(e);
            if (e == fb) has = true;
        }
        if (!has) keep.Add(fb);
        table.arraySize = keep.Count;
        for (int i = 0; i < keep.Count; i++)
            table.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(scHeavy);
        var keptNames = new List<string>();
        foreach (var o in keep) keptNames.Add(o != null ? o.name : "null");
        Debug.Log("[整形] SC-Heavy SDF fallback 表=[" + string.Join(",", keptNames) + "]");
    }

    /// <summary>TMP Settings 默认字体 → SC-Heavy；LiberationSans SDF fallback 补挂 SC-Heavy。</summary>
    private static void ApplyTmpSettings(TMP_FontAsset font)
    {
        var settings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath);
        if (settings != null)
        {
            var so = new SerializedObject(settings);
            var def = so.FindProperty("m_defaultFontAsset");
            if (def != null && def.objectReferenceValue != font)
            {
                def.objectReferenceValue = font;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                Debug.Log("[整形] TMP Settings 默认字体 → SC-Heavy SDF");
            }
        }

        var lib = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationSdfPath);
        if (lib != null)
        {
            var so = new SerializedObject(lib);
            var table = so.FindProperty("m_FallbackFontAssetTable");
            bool has = false;
            if (table != null && table.isArray)
                for (int i = 0; i < table.arraySize; i++)
                    if (table.GetArrayElementAtIndex(i).objectReferenceValue == font) has = true;
            if (!has && table != null)
            {
                table.arraySize += 1;
                table.GetArrayElementAtIndex(table.arraySize - 1).objectReferenceValue = font;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lib);
                Debug.Log("[整形] LiberationSans SDF fallback 表补挂 SC-Heavy（漏网文本缺中文兜底）");
            }
        }
    }

    /// <summary>YooAsset 收集器 UI 组 CollectPath：Assets/UI/Res（已删）→ Assets/Art/UI。</summary>
    private static void ApplyCollectorPath()
    {
        var setting = AssetDatabase.LoadAssetAtPath<Object>(CollectorSettingPath);
        if (setting == null)
        {
            Debug.LogWarning("[整形] BundleCollectorSetting 不存在，跳过收集器更新");
            return;
        }
        var so = new SerializedObject(setting);
        var groups = so.FindProperty("Groups");
        if (groups == null || !groups.isArray) return;
        bool changed = false;
        for (int gi = 0; gi < groups.arraySize; gi++)
        {
            var g = groups.GetArrayElementAtIndex(gi);
            if (g.FindPropertyRelative("GroupName").stringValue != "UI") continue;
            var collectors = g.FindPropertyRelative("Collectors");
            for (int ci = 0; ci < collectors.arraySize; ci++)
            {
                var cp = collectors.GetArrayElementAtIndex(ci).FindPropertyRelative("CollectPath");
                if (cp.stringValue == "Assets/UI/Res")
                {
                    cp.stringValue = "Assets/Art/UI";
                    changed = true;
                }
            }
        }
        if (changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(setting);
            Debug.Log("[整形] 收集器 UI 组 CollectPath → Assets/Art/UI");
        }
    }

    /// <summary>读字体字符表（经 SerializedObject——规避 TMP 版本 API 差异）。</summary>
    private static HashSet<uint> LoadFontChars(TMP_FontAsset font)
    {
        var chars = new HashSet<uint>();
        var so = new SerializedObject(font);
        var table = so.FindProperty("m_CharacterTable");
        if (table != null && table.isArray)
        {
            for (int i = 0; i < table.arraySize; i++)
            {
                var uni = table.GetArrayElementAtIndex(i).FindPropertyRelative("m_Unicode");
                if (uni != null) chars.Add((uint)uni.intValue);
            }
        }
        Debug.Log($"[整形] SC-Heavy SDF 字符表：{chars.Count} 字形");
        return chars;
    }
}
