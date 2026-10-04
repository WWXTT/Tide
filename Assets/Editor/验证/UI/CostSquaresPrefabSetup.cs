using System.IO;
using SynergyUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tide.验证
{
    /// <summary>
    /// 费用方格公用预制体装配（2026-10-03 定案：费用=对应颜色小方格排列，公用预制体统一引用，
    /// 引用者自行调整整体大小）。幂等创建/修补 Assets/Art/UI/CostSquares.prefab：
    /// 根 cost-squares（RectTransform + CostSquaresView，横排居中）+ 不激活模板 tpl-square
    /// （居中锚点 Image 方块）。用法：Tools/验证/UI费用方格/Prefab装配。
    /// </summary>
    public static class CostSquaresPrefabSetup
    {
        private const string PrefabPath = UiKit.PrefabFolder + "CostSquares.prefab";

        [MenuItem("Tools/验证/UI费用方格/Prefab装配")]
        public static void Run()
        {
            GameObject contents = File.Exists(PrefabPath)
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : BuildNew();
            try
            {
                // 幂等修补：组件/模板缺失才补（不碰引用者对实例的烘焙调整）
                if (contents.GetComponent<CostSquaresView>() == null)
                {
                    var v = contents.AddComponent<CostSquaresView>();
                    v.SquareSize = 14f;
                    v.Spacing = 2f;
                }
                if (contents.transform.Find(CostSquaresView.TemplateName) == null)
                    BuildTemplate((RectTransform)contents.transform);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            Debug.Log($"[CostSquaresPrefabSetup] 装配完成：{PrefabPath}");
        }

        private static GameObject BuildNew()
        {
            var root = new GameObject("cost-squares", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(0f, 14f);
            var view = root.AddComponent<CostSquaresView>();
            view.SquareSize = 14f;
            view.Spacing = 2f;
            BuildTemplate(rt);
            return root;
        }

        private static void BuildTemplate(RectTransform root)
        {
            var go = new GameObject(CostSquaresView.TemplateName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(14f, 14f);
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            go.SetActive(false);
        }
    }
}
