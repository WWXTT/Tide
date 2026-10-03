using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 对战屏预制体装配（2026-10-03，幂等=整树重建）：
/// 重建 Assets/Art/UI/BattleUI.prefab——本地对战扇形手牌过渡面的静态层级
/// （旧 BattleUI 已于 2026-10-02 随「战场 3D 重做」定案删除，本版=过渡面口径）：
///   根=透明底（只留射线拦截语义，不挡 3D 战场）+纵向布局；btn-back（布局内）；
///   btn-end-turn（右下锚点+显式尺寸、默认隐藏——仅本地对局 OnEnter 激活）；
///   self/opp-fan-layer 全屏挂载层（HandFanView 卡牌画布）。
/// overlay/selector-overlay 不烘焙（运行时 UiKit.Overlay 补建——批量整形定案口径）。
/// 结构对齐 BattleScreen.Build 的纯代码兜底路径（bind-or-create 双轨同构）。
/// 勿把 BattleUI.prefab 纳入「UI预制体批量整形」：btn-back 的尺寸 LE 非 ignoreLayout，
/// 会被其第 2 步摘除（摘除后布局组按无 sprite Image 的 preferred=0 塌缩）——
/// 样式若被其它工具改动，重跑本脚本即按代码口径恢复。
/// </summary>
public static class BattleUiPrefabSetup
{
    private const string PrefabPath = "Assets/Art/UI/BattleUI.prefab";
    private const string FontSdfPath =
        "Assets/Packages/TextMesh Pro/Resources/Fonts & Materials/庞门正道标题体 SDF.asset";

    // 主题常量自 UiStyle 抄录（编辑器程序集不引热更程序集——UiPrefabBatchReshape 同口径）
    private static readonly Color ScreenBgTransparent =
        new Color(24f / 255f, 27f / 255f, 33f / 255f, 0f);
    private static readonly Color BtnBg = new Color(54f / 255f, 60f / 255f, 72f / 255f, 1f);
    private static readonly Color TextBody = new Color(225f / 255f, 229f / 255f, 236f / 255f, 1f);

    [MenuItem("Tools/验证/对战UI/BattleUI预制体装配")]
    public static void Run()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSdfPath);
        if (font == null)
        {
            Debug.LogError($"[BattleUiPrefabSetup] 主字体缺失：{FontSdfPath}（先跑 Tools/验证/UI预制体批量整形 重建主字体）");
            return;
        }

        var rootGo = new GameObject("BattleUI", typeof(RectTransform));
        try
        {
            var root = (RectTransform)rootGo.transform;
            Stretch(root);

            var img = rootGo.AddComponent<Image>(); // UiKit.Screen 同构，但烘焙透明（战场 3D 可见）
            img.color = ScreenBgTransparent;
            img.raycastTarget = true;

            var vlg = rootGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var backRt = MakeButton(root, "btn-back", "← 返回主菜单", font);
            var backLe = backRt.gameObject.AddComponent<LayoutElement>();
            backLe.minWidth = 30f;                       // UiKit.Size 口径：未给的维显式 -1
            backLe.minHeight = -1f;
            backLe.preferredWidth = LabelOf(backRt).preferredWidth + 28f;
            backLe.preferredHeight = 36f;
            backLe.flexibleWidth = -1f;
            backLe.flexibleHeight = -1f;

            var endRt = MakeButton(root, "btn-end-turn", "结束回合", font);
            endRt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; // 逃逸屏根纵向布局
            endRt.anchorMin = endRt.anchorMax = new Vector2(1f, 0f);
            endRt.pivot = new Vector2(0.5f, 0.5f);
            endRt.anchoredPosition = new Vector2(-24f, 240f); // 手牌扇侧翼上方
            endRt.sizeDelta = new Vector2(LabelOf(endRt).preferredWidth + 28f, 36f);
            endRt.gameObject.SetActive(false);           // 默认隐藏（OnEnter 按模式激活）

            MakeFanLayer(root, "self-fan-layer");
            MakeFanLayer(root, "opp-fan-layer");

            PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BattleUiPrefabSetup] 装配完成：{PrefabPath}\n{Dump(root)}");
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    /// <summary>UiKit.Button 同构复刻：纯色底（无 sprite，同现有 prefab 烘焙口径）、
    /// ColorTint 悬停/按压、子 label TMP 居中（庞门 14 Bold）。返回按钮 RectTransform。</summary>
    private static RectTransform MakeButton(RectTransform parent, string name, string text, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Stretch(rt); // btn-back 由布局接管尺寸；btn-end-turn 由锚点+sizeDelta 接管

        var img = go.AddComponent<Image>();
        img.color = BtnBg;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f); // 对标 .btn:hover 提亮
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);     // 对标 .btn:active 压暗
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        var lblGo = new GameObject("label", typeof(RectTransform));
        var lblRt = (RectTransform)lblGo.transform;
        lblRt.SetParent(rt, false);
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = new Vector2(8f, 2f);   // UiKit.StretchInset(8, 2)
        lblRt.offsetMax = new Vector2(-8f, -2f);

        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.font = font;
        lbl.text = text;
        lbl.fontSize = 14;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = TextBody;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        lbl.overflowMode = TextOverflowModes.Overflow;
        lbl.raycastTarget = false;
        lbl.richText = false;
        // 自动大小同批量整形口径：上限=设计字号、下限 min(设计,12)——只缩不放
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = 12f;
        lbl.fontSizeMax = 14f;
        return rt;
    }

    private static TMP_Text LabelOf(RectTransform button) =>
        button.GetComponentInChildren<TMP_Text>(true);

    /// <summary>扇区挂载层：全屏铺开做卡牌画布，逃逸屏根纵向布局（同 BattleScreen.FanLayer）。</summary>
    private static void MakeFanLayer(RectTransform root, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        Stretch(rt);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>结构取证（数据层证据口径——截图验收禁令）：树+rect 关键值落 Console。</summary>
    private static string Dump(RectTransform root)
    {
        var sb = new StringBuilder();
        DumpNode(root, 0, sb);
        return sb.ToString();
    }

    private static void DumpNode(Transform t, int depth, StringBuilder sb)
    {
        var rt = (RectTransform)t;
        sb.AppendLine(new string(' ', depth * 2) + t.name
            + $" active={t.gameObject.activeSelf}"
            + $" size=({rt.sizeDelta.x:F0},{rt.sizeDelta.y:F0})"
            + $" anchor=({rt.anchorMin.x:F1},{rt.anchorMin.y:F1})-({rt.anchorMax.x:F1},{rt.anchorMax.y:F1})"
            + $" pos=({rt.anchoredPosition.x:F0},{rt.anchoredPosition.y:F0})");
        for (int i = 0; i < t.childCount; i++) DumpNode(t.GetChild(i), depth + 1, sb);
    }
}
