using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>
    /// 费用方格视图（2026-10-03 定案：费用表达统一为对应颜色小方格排列）。
    /// 公用预制体 Assets/Art/UI/CostSquares.prefab 的组件——按费用明细克隆模板方格，
    /// 横排整行居中于根节点；引用者（各屏/卡面）经 Mount 挂载，用 SquareSize 或实例
    /// localScale 自行调整整体大小。颜色=UiStyle.Dot*（与列表行色点同款）。
    /// 自愈三兜底：无模板→代码自建 tpl-square；无预制体→Mount 纯代码节点；引用者烘焙
    /// 同名节点→直接复用（不实例化）。
    /// </summary>
    public sealed class CostSquaresView : MonoBehaviour
    {
        public const string PrefabName = "CostSquares";
        public const string TemplateName = "tpl-square";

        public float SquareSize = 14f;
        public float Spacing = 2f;

        private RectTransform _template;
        private bool _codeTemplate; // 模板为代码自建（无烘焙）——克隆体才给默认尺寸，烘焙模板不碰布局

        /// <summary>ManaType → 方格色。</summary>
        private static Color ManaColor(ManaType m) => m switch
        {
            ManaType.Red => UiStyle.DotRed,
            ManaType.Blue => UiStyle.DotBlue,
            ManaType.Green => UiStyle.DotGreen,
            ManaType.Black => UiStyle.DotBlack,
            ManaType.White => UiStyle.DotWhite,
            _ => UiStyle.DotGray,
        };

        /// <summary>挂载：① parent 子树已有组件实例（引用者直接拖入公用预制体，拖入名=CostSquares）
        /// → 直接复用；② 同名烘焙节点（cost-squares）→ 补组件复用；③ 实例化公用预制体
        /// （HotUpdateAssets 地址=CostSquares、编辑器兜底路径）；④ 预制体缺失 → 纯代码节点兜底
        /// （Console 提示，跑装配菜单可补建）。</summary>
        public static CostSquaresView Mount(RectTransform parent, string nodeName = "cost-squares")
        {
            if (parent == null) return null;
            var baked = parent.GetComponentInChildren<CostSquaresView>(true);
            if (baked != null) return baked;
            var node = parent.Find(nodeName) as RectTransform ?? UiKit.FindDeep(parent, nodeName);
            if (node == null)
            {
                GameObject go;
                var prefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>(PrefabName, UiKit.PrefabPath(PrefabName));
                if (prefab != null)
                {
                    go = Instantiate(prefab.gameObject, parent, false);
                }
                else
                {
                    Debug.LogWarning("[CostSquares] 公用预制体缺失——纯代码兜底（Tools/验证/UI费用方格/Prefab装配 可生成）");
                    go = new GameObject(nodeName, typeof(RectTransform));
                    ((RectTransform)go.transform).SetParent(parent, false);
                }
                go.name = nodeName;
                node = (RectTransform)go.transform;
            }
            return node.GetComponent<CostSquaresView>() ?? node.gameObject.AddComponent<CostSquaresView>();
        }

        /// <summary>费用主口径（2026-10-04 位置数组：ElementCost，下标=ManaType 枚举序号——
        /// 方格色序恒为枚举序，与字典插入序的旧不稳定显示告别）。全零/空则清空。</summary>
        public void SetCosts(ElementCost costs)
        {
            if (costs == null || costs.IsZero) { Clear(); return; }
            SetCosts(costs.NonzeroColors()
                .Select(c => new ElementCostRef { mana = (int)c, value = (int)costs[c] })
                .Where(r => r.value > 0));
        }

        /// <summary>瘦引用口径（ElementCostRef 列表）。空则清空。
        /// 2026-10-03 定案：只负责生成对应数量+颜色的方块——排布/尺寸全归引用者
        /// （烘焙模板进 LayoutGroup 或自由锚点自行控制）；仅代码自建模板兜底时给默认尺寸。</summary>
        public void SetCosts(IEnumerable<ElementCostRef> costs)
        {
            EnsureTemplate();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (c.name == TemplateName) continue;
                Destroy(c.gameObject);
            }

            var list = costs?.Where(c => c != null && c.value > 0).ToList() ?? new List<ElementCostRef>();
            foreach (var c in list)
            {
                var color = ManaColor((ManaType)c.mana);
                for (int k = 0; k < c.value; k++)
                {
                    var sq = Instantiate(_template.gameObject, transform, false);
                    sq.name = "square";
                    sq.SetActive(true);
                    if (_codeTemplate)
                        ((RectTransform)sq.transform).sizeDelta = new Vector2(SquareSize, SquareSize);
                    var img = sq.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = color;
                        img.raycastTarget = false;
                    }
                }
            }
        }

        public void Clear() => SetCosts((IEnumerable<ElementCostRef>)null);

        private void EnsureTemplate()
        {
            if (_template != null) return;
            var t = transform.Find(TemplateName);
            if (t != null)
            {
                _template = (RectTransform)t;
                return;
            }
            var go = new GameObject(TemplateName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.sizeDelta = new Vector2(SquareSize, SquareSize);
            _codeTemplate = true;
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            go.SetActive(false);
            _template = rt;
        }
    }
}
