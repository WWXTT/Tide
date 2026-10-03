using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手牌扇形（2026-10-02）的一次性 prefab 装配工具（幂等，可重复执行）：
/// ACard.prefab 补默认隐藏的 back 卡背节点（CardOverlayCard.SetFaceUp 翻面用）。
/// 原 MainUI.btn-hand-fan 入口装配段已随原型屏退役删除（2026-10-03 扇形直接接入 BattleScreen 本地对战）。
/// </summary>
public static class HandFanPrefabSetup
{
    private const string ACardPath = "Assets/Art/UI/ACard.prefab";

    [MenuItem("Tools/验证/手牌扇形/Prefab装配（ACard卡背）")]
    public static void Run()
    {
        var log = new StringBuilder();
        SetupAcardBack(log);
        AssetDatabase.SaveAssets();
        Debug.Log("[HandFanPrefabSetup] 装配完成\n" + log);
    }

    /// <summary>ACard 补 back：全拉伸 Image 占位卡背 + 描边，置末位盖住正面区块，默认隐藏。</summary>
    private static void SetupAcardBack(StringBuilder log)
    {
        var root = PrefabUtility.LoadPrefabContents(ACardPath);
        try
        {
            if (DeepFind(root.transform, "back") != null)
            {
                log.AppendLine("ACard: back 已存在，跳过");
                return;
            }

            var go = new GameObject("back", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling(); // 渲染在全部正面区块之上

            var img = go.GetComponent<Image>();
            img.color = new Color(0.118f, 0.129f, 0.157f, 1f); // 占位卡背：深于 bg 的深蓝黑
            img.raycastTarget = true;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.62f, 0.78f, 0.35f);
            outline.effectDistance = new Vector2(2f, -2f);

            go.SetActive(false); // 默认隐藏（同 land/mark-* 烘焙约定）
            PrefabUtility.SaveAsPrefabAsset(root, ACardPath);
            log.AppendLine("ACard: back 已创建（默认隐藏、末位兄弟）");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform DeepFind(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name == name) return t;
        return null;
    }
}
