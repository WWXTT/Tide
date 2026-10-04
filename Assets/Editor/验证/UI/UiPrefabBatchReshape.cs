using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>
/// UI 预制体批量整形（2026-10-01 预制体化定案，幂等可重跑）：
///   1. 双字体分工（2026-10-02 定案）：系统面板（主菜单/卡组构筑）→ 庞门正道标题体 SDF
///      （4096² 动态、SDFAA——多图集分页与非法渲染模式都是"文字不可见"的高危路径，禁触）；
///      卡牌与效果域（卡牌编辑器/效果合成器/ACard 卡面）→ SC-Heavy SDF（静态图集，缺字自检生效）；
///   2. LayoutElement 摘除——仅保留滚动区 content 子树内与 ignoreLayout=true 的（语义必需）；
///   3. 根三件套摘除（Canvas/CanvasScaler/GraphicRaycaster + 根孤儿 CanvasRenderer）——
///      Canvas 归场景 UIBootstrap 独占，屏幕预制体不允许嵌套 Canvas；
///   4. BattleUI 删除 CardFallback 空壳残骸（该 prefab 已于 2026-10-02 删除，分支保留备用）；
///   5. SC-Heavy 字符覆盖自检——动态图集下自动跳过（缺字按需光栅，口径失效）；
///   6. TMP Settings 默认字体 → 主字体（LiberationSans SDF 已删，链路分支自动跳过）；
///   7. YooAsset 收集器 UI 组 CollectPath：已删除的 Assets/UI/Res → Assets/Art/UI；
///   8. overlay/selector-overlay 空节点删除（弹层挂载点改由 UiKit.Overlay 运行时补建，
///      UIScreen.FindOptional 静默获取）；
///   9.（已并入 3）
///  10.（已并入 3）
///  11. TMP 字体自动大小全开：上限=烘焙设计字号、下限=min(设计,12)——只缩不放
///      （ACard 除外——CardOverlayCard 按形态写死字号 10-13）；
///  12. 动态残留子物体清理（运行时 ClearChildren/ClearContent 重建的容器清空烘焙死数据）；
///  13. TMP SubMesh 防回归 sweep（运行时 fallback 产物被误烘焙进来则删）；
///  14. 字重规则：标题加粗、非标题不加粗——标题=主菜单根 label（潮汐）/各屏工具栏
///      title / ACard 卡名 name；其余强制 Normal。
/// </summary>
public static class UiPrefabBatchReshape
{
    private const string FontSdfPath =
        "Assets/Packages/TextMesh Pro/Resources/Fonts & Materials/庞门正道标题体 SDF.asset";
    private const string FontTtfPath =
        "Assets/Packages/TextMesh Pro/Fonts/庞门正道标题体.ttf";
    private const string ScHeavySdfPath =
        "Assets/Packages/TextMesh Pro/Resources/Fonts & Materials/SC-Heavy SDF.asset";
    private const string TmpSettingsPath = "Assets/Packages/TextMesh Pro/Resources/TMP Settings.asset";
    private const string CollectorSettingPath = "Assets/BundleCollectorSetting.asset";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Art/UI/MainUI.prefab",
            // BattleUI.prefab 已重建（2026-10-03 过渡面）但由 BattleUiPrefabSetup 幂等装配、
            // 勿纳入批量整形：btn-back 的尺寸 LE 非 ignoreLayout 会被第 2 步摘除（布局组下塌缩）
            "Assets/Art/UI/CardUI.prefab",
        "Assets/Art/UI/DeckUI.prefab",
        "Assets/Art/UI/EffectUI.prefab",
        "Assets/Art/UI/ACard.prefab",
    };

    [MenuItem("Tools/验证/UI预制体批量整形")]
    public static void Run()
    {
        var pangmen = EnsurePrimaryPangmen();
        if (pangmen == null)
        {
            Debug.LogError($"[整形] 主字体构建失败：{FontTtfPath} / {FontSdfPath}");
            return;
        }
        var scHeavy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ScHeavySdfPath);
        if (scHeavy == null)
            Debug.LogWarning($"[整形] SC-Heavy SDF 缺失：{ScHeavySdfPath}——卡牌/效果域沿用庞门");
        var pangmenChars = pangmen.atlasPopulationMode == AtlasPopulationMode.Dynamic
            ? null : LoadFontChars(pangmen);
        var scChars = scHeavy != null && scHeavy.atlasPopulationMode != AtlasPopulationMode.Dynamic
            ? LoadFontChars(scHeavy) : null; // 静态图集才有缺字自检意义（动态按需光栅）

        foreach (var path in PrefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[整形] 跳过（不存在）：{path}");
                continue;
            }
            // 双字体分工（2026-10-02 定案）：系统面板（主菜单/卡组构筑）=庞门标题体；
            // 卡牌与效果域（卡牌编辑器/效果合成器/ACard 卡面）=SC-Heavy 宋体
            bool cardDomain = path.EndsWith("CardUI.prefab") || path.EndsWith("EffectUI.prefab")
                || path.EndsWith("ACard.prefab");
            var font = cardDomain && scHeavy != null ? scHeavy : pangmen;
            ReshapePrefab(path, font, cardDomain ? scChars : pangmenChars);
        }

        ApplyTmpSettings(pangmen);
        ApplyCollectorPath();
        AssetDatabase.SaveAssets();
        Debug.Log("[整形] 全部完成（字体/LE/Canvas/残骸/残留/字重/TMP设置/收集器）");
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
            // 1. 字体全量换庞门正道标题体 SDF + 文本组件强制启用（预制体捕获时 enabled 被误关——文本不可见根因之一）
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
                if (fontChars != null)
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

            // 3+10. 根摘嵌套 Canvas 体系（推广到全部 prefab）+ 根孤儿 CanvasRenderer（无 Graphic 配对的转换残留）
            int canvasRemoved = 0;
            foreach (var c in new Object[] { root.GetComponent<Canvas>(), root.GetComponent<CanvasScaler>(), root.GetComponent<GraphicRaycaster>() })
                if (c != null) { Object.DestroyImmediate(c); canvasRemoved++; }
            var rootCr = root.GetComponent<CanvasRenderer>();
            if (rootCr != null && root.GetComponent<Graphic>() == null)
            {
                Object.DestroyImmediate(rootCr);
                canvasRemoved++;
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

            // 9. overlay/selector-overlay 空节点删除：弹层挂载点归运行时 UiKit.Overlay 补建
            //    （置顶兄弟序+ignoreLayout，与烘焙版等价；UIScreen.FindOptional 静默获取）。
            //    空=无 Graphic 无子物体——防误伤同名实体节点。
            int overlayRemoved = 0;
            var doomedOverlays = new List<GameObject>();
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if ((tr.name == "overlay" || tr.name == "selector-overlay")
                    && tr.GetComponent<Graphic>() == null && tr.childCount == 0)
                    doomedOverlays.Add(tr.gameObject);
            foreach (var go in doomedOverlays) { Object.DestroyImmediate(go); overlayRemoved++; }

            // 11. TMP 字体自动大小（ACard 除外——CardOverlayCard.Apply 按形态写死字号 10-13，
            //     开自动大小会覆盖代码字号、三种形态排版区分失效）。
            //     口径（2026-10-02 视觉验收修正）：上限=烘焙设计字号、下限=min(设计,12)——
            //     只缩不放：文本装不下时自动收缩，但不超过设计尺寸（设计字号 11-20，
            //     若 min/max 放开到 18-72 会把高条形 rect 里的标签撑到重叠）。
            int autoSized = 0;
            if (!path.EndsWith("ACard.prefab"))
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    float baked = t.fontSize > 0f ? t.fontSize : 18f;
                    float max = Mathf.Max(baked, 12f);
                    float min = Mathf.Min(baked, 12f);
                    if (!t.enableAutoSizing
                        || Mathf.RoundToInt(t.fontSizeMin) != Mathf.RoundToInt(min)
                        || Mathf.RoundToInt(t.fontSizeMax) != Mathf.RoundToInt(max))
                        autoSized++;
                    t.enableAutoSizing = true;
                    t.fontSizeMin = min;
                    t.fontSizeMax = max;
                }

            // 14. 字重规则（2026-10-02 单字体定案）：标题加粗、非标题不加粗——
            //     标题=各屏工具栏 title / 主菜单根下直属 label（潮汐）/ ACard 卡名 name
            int boldSet = 0, normalSet = 0;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                bool isTitle = t.name == "title"
                    || (path.EndsWith("ACard.prefab") && t.name == "name")
                    || (path.EndsWith("MainUI.prefab") && t.name == "label" && t.transform.parent == root.transform);
                var want = isTitle ? FontStyles.Bold : FontStyles.Normal;
                if (t.fontStyle != want)
                {
                    t.fontStyle = want;
                    if (isTitle) boldSet++; else normalSet++;
                }
            }

            // 12. 动态残留清理：运行时每次进屏 ClearChildren/ClearContent 重建的容器，烘焙内容是死数据
            //     （BattleUI 的 cell×9/hand-slot×7 与 DeckUI/CardUI 的 chip-* 是刻意保留的模板，不在此列）
            int residueRemoved = 0;
            if (path.EndsWith("DeckUI.prefab"))
                residueRemoved += ClearScrollContent(root, "list-catalog") + ClearChildren(root, "stats-zone")
                    + ClearScrollContent(root, "preview-zone");
            else if (path.EndsWith("CardUI.prefab"))
                residueRemoved += ClearScrollContent(root, "list-pool") + ClearChildren(root, "dynamic-form")
                    + ClearChildren(root, "payload-zone") + ClearChildren(root, "effect-library-zone")
                    + ClearChildren(root, "keyword-zone") + ClearScrollContent(root, "list-breakdown");
            else if (path.EndsWith("EffectUI.prefab"))
                residueRemoved += ClearChildren(root, "mode-bar") + ClearScrollContent(root, "slot-area")
                    + ClearScrollContent(root, "list-library");

            // 13. TMP SubMesh 防回归：运行时 fallback 产物若被误烘焙进来则删（正常应为 0）
            int subMeshRemoved = 0;
            var doomedSubMesh = new HashSet<GameObject>();
            foreach (var sm in root.GetComponentsInChildren<TMPro.TMP_SubMeshUI>(true))
                doomedSubMesh.Add(sm.gameObject);
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("TMP SubMesh")) doomedSubMesh.Add(tr.gameObject);
            foreach (var go in doomedSubMesh) { Object.DestroyImmediate(go); subMeshRemoved++; }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[整形] {System.IO.Path.GetFileName(path)}：字体换 {fontSwaps}，文本启用 {textEnabled}，" +
                      $"LE 摘 {leRemoved}/留 {leKept}，Canvas 组件摘 {canvasRemoved}，CardFallback 删 {fallbackRemoved}，" +
                      $"overlay 删 {overlayRemoved}，自动大小 {autoSized}，加粗 {boldSet}/常规 {normalSet}，残留清 {residueRemoved}，SubMesh 删 {subMeshRemoved}" +
                      (missingChars.Count > 0 ? $"，⚠图集缺字：[{string.Join("", missingChars)}]" : "，缺字 0"));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>主字体：庞门正道标题体 SDF（2026-10-02 单字体定案）。图集不足 4096² 时以同路径重建：
    /// 4096² 动态、多图集支持——90pt 采样下单页即可容纳全部 UI 用字，实际永不触发分页（动态新增的
    /// 分页纹理在域重载后有悬空→文字不可见的先例，属高危路径）。重建后 guid 变化由全量字体换步骤
    /// 与 TMP Settings 重指吸收。material 与 atlasTexture 必须 AddObjectToAsset 收进资产文件。</summary>
    private static TMP_FontAsset EnsurePrimaryPangmen()
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
        if (ttf == null)
        {
            Debug.LogError($"[整形] 庞门源字体缺失：{FontTtfPath}");
            return null;
        }
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdfPath);
        if (existing != null && existing.atlasTexture != null && existing.atlasTexture.width >= 4096)
        {
            Debug.Log($"[整形] 主字体复用：{FontSdfPath}（动态 {existing.atlasTexture.width}²）");
            return existing;
        }
        // 先取旧资产采样口径（删除后对象失效，读不到）。渲染模式必须用枚举 SDFAA——
        // 传魔数会得到非法模式：字形非 SDF 数据，SDF shader 读出 alpha≈0 → 文字全隐形
        // （2026-10-02 事故根因：曾硬编码 6，而 SDFAA=4165/SMOOTH=4117）。
        int pointSize = 90, padding = 5;
        var renderMode = UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA;
        if (existing != null)
        {
            var so = new SerializedObject(existing);
            var ps = so.FindProperty("m_PointSize");
            if (ps != null) pointSize = ps.intValue;
            var pd = so.FindProperty("m_Padding");
            if (pd != null) padding = pd.intValue;
        }
        if (existing != null)
        {
            Debug.LogWarning($"[整形] 庞门 SDF 图集 {(existing.atlasTexture != null ? existing.atlasTexture.width.ToString() : "坏")}² 不足 4096²——删除重建（引用由批量步骤重指）");
            AssetDatabase.DeleteAsset(FontSdfPath);
        }
        var created = TMP_FontAsset.CreateFontAsset(ttf, pointSize, padding,
            renderMode, 4096, 4096,
            AtlasPopulationMode.Dynamic, true);
        AssetDatabase.CreateAsset(created, FontSdfPath);
        if (created.material != null) AssetDatabase.AddObjectToAsset(created.material, created);
        if (created.atlasTexture != null) AssetDatabase.AddObjectToAsset(created.atlasTexture, created);
        AssetDatabase.SaveAssets();
        Debug.Log($"[整形] 主字体重建：{FontSdfPath}（{pointSize}pt/pad{padding}/mode={renderMode}/4096²/动态）");
        return created;
    }

    /// <summary>TMP Settings 默认字体 → 主字体（庞门 SDF；LiberationSans SDF 已删，兜底链分支随之作废）。</summary>
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
                Debug.Log($"[整形] TMP Settings 默认字体 → {font.name}");
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

    /// <summary>深度按名查找（编辑器侧独立实现——UiKit 在热更程序集，此处不便引用）。</summary>
    private static Transform FindDeep(Transform scope, string name)
    {
        if (scope.name == name) return scope;
        for (int i = 0; i < scope.childCount; i++)
        {
            var hit = FindDeep(scope.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>清空容器全部直接子物体（容器本身保留；缺失告警防静默漏删）。</summary>
    private static int ClearChildren(GameObject root, string containerName)
    {
        var node = FindDeep(root.transform, containerName);
        if (node == null)
        {
            Debug.LogWarning($"[整形] 残留容器缺失：{containerName}（预期存在，请核对节点名）");
            return 0;
        }
        int n = node.childCount;
        for (int i = n - 1; i >= 0; i--) Object.DestroyImmediate(node.GetChild(i).gameObject);
        return n;
    }

    /// <summary>清空滚动区 content 的子物体（viewport/滚动条保留——只清行数据）。</summary>
    private static int ClearScrollContent(GameObject root, string scrollName)
    {
        var node = FindDeep(root.transform, scrollName);
        ScrollRect sr = node != null ? node.GetComponent<ScrollRect>() : null;
        RectTransform content = sr != null ? sr.content : null;
        if (content == null && node != null) content = FindDeep(node, "content") as RectTransform;
        if (content == null)
        {
            Debug.LogWarning($"[整形] 滚动区/content 缺失：{scrollName}（预期存在，请核对节点名）");
            return 0;
        }
        int n = content.childCount;
        for (int i = n - 1; i >= 0; i--) Object.DestroyImmediate(content.GetChild(i).gameObject);
        return n;
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
