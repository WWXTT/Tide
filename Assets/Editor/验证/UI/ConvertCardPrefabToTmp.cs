using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tide.验证
{
    /// <summary>
    /// 卡面 prefab 转 TMP（2026-10-01 全量 TMP 化收尾）：把 BattleCardUGUI.prefab 里的
    /// legacy Text 逐个替换为 TextMeshProUGUI（拷贝文本/字号/对齐/颜色/raycast），
    /// 字体资产指 SC-Heavy SDF（运行时 CardOverlayCard.Bind 会覆写为动态字体）。
    /// 幂等——重复执行无额外转换。
    /// </summary>
    public static class ConvertCardPrefabToTmp
    {
        private const string PrefabPath = "Assets/UI/Res/BattleCardUGUI.prefab";
        private const string SdfPath = "Assets/Packages/TextMesh Pro/Resources/Fonts & Materials/SC-Heavy SDF.asset";

        [MenuItem("Tools/验证/卡面Prefab转TMP")]
        public static void Run()
        {
            var sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
            if (sdf == null) Debug.LogWarning($"[TMP转换] 找不到 {SdfPath}——TMP 组件将用默认字体（运行时动态覆写兜底）");

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            int n = 0;
            try
            {
                foreach (var t in contents.GetComponentsInChildren<Text>(true))
                {
                    var go = t.gameObject;
                    // 先取值再销毁——Unity 6 禁止 Text 与 TextMeshProUGUI 同物体共存
                    var text = t.text;
                    var fontSize = t.fontSize;
                    var style = t.fontStyle;
                    var color = t.color;
                    var align = t.alignment;
                    var raycast = t.raycastTarget;
                    Object.DestroyImmediate(t);
                    var tmp = go.AddComponent<TextMeshProUGUI>();
                    tmp.text = text;
                    tmp.fontSize = fontSize;
                    tmp.fontStyle = (FontStyles)style;
                    tmp.color = color;
                    tmp.alignment = Map(align);
                    tmp.raycastTarget = raycast;
                    if (sdf != null) tmp.font = sdf;
                    n++;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log($"[TMP转换] {PrefabPath}：{n} 个 Text → TextMeshProUGUI");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static TextAlignmentOptions Map(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.TopLeft,
        };
    }
}
