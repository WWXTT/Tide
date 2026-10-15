using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>填充方式：横向=单行均布；竖向=单列均布；田字=按容器长宽比取近方列数网格。</summary>
    public enum CostSquaresFill
    {
        Horizontal = 0,
        Vertical = 1,
        Grid = 2,
    }

    /// <summary>
    /// 费用方格视图（2026-10-03 定案：费用表达统一为对应颜色小方格排列）。
    /// 公用预制体 Assets/Art/Prefab/CostSquares.prefab 的组件——按费用明细克隆模板方格，
    /// 颜色=UiStyle.Dot*（与列表行色点同款）；引用者（各屏/卡面）经 Mount 挂载，
    /// 用实例 localScale 自行调整整体大小。
    /// 2026-10-13 拓展：组件自读根 RectTransform 宽高，把生成方格按填充方式
    /// （Fill：横向/竖向/田字）均分定位——每格占 cell=宽/列×高/行，方格居中于 cell、
    /// 尺寸默认=模板尺寸，元素过多时等比收缩到 cell（不重叠不变形不超根范围）；
    /// SetCosts / 改 Fill / 根尺寸变化时自动重排。
    /// 根上有 LayoutGroup 时跳过重排（排布仍归引用者烘焙，2026-10-03 旧契约保留）。
    /// 自愈三兜底：无模板→代码自建 tpl-square；无预制体→Mount 纯代码节点；引用者烘焙
    /// 同名节点→直接复用（不实例化）。
    /// 2026-10-15：生成序定案=红蓝绿黑白灰（三色优先、灰垫底——组件内排序，调用方传入序不保留）；
    /// 田字换行改同色整组制：同色段尽量不跨行（行尾留空整组挪新行），段长超过整行容量
    /// （实在放不下）才连续跨行、与后续色段混排。
    /// </summary>
    public sealed class CostSquaresView : MonoBehaviour
    {
        public const string PrefabName = "CostSquares";
        public const string TemplateName = "tpl-square";

        [SerializeField] private CostSquaresFill fill = CostSquaresFill.Horizontal;

        private RectTransform _template;
        private readonly List<RectTransform> _squares = new List<RectTransform>(); // 本视图生成的方格（Destroy 帧末生效，定位不看 childCount）
        private readonly List<int> _squareMana = new List<int>(); // 与 _squares 平行的每格 ManaType 序号（田字排布识别同色段用）

        /// <summary>方格生成序（2026-10-15）：三色红蓝绿优先、灰垫底；黑白沿用 CardSorter 的黑先白后。
        /// 未知色与灰同档排末尾。</summary>
        private static readonly ManaType[] DisplayOrder =
        {
            ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Black, ManaType.White,
        };

        private static int DisplayRank(ManaType m)
        {
            int i = Array.IndexOf(DisplayOrder, m);
            return i >= 0 ? i : DisplayOrder.Length; // 灰及未来扩色恒排最后
        }

        /// <summary>填充方式（外部可选，运行时改动立即重排）。</summary>
        public CostSquaresFill Fill
        {
            get => fill;
            set
            {
                if (fill == value) return;
                fill = value;
                Relayout();
            }
        }

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

        /// <summary>费用主口径（2026-10-04 位置数组：ElementCost，下标=ManaType 枚举序号）。
        /// 全零/空则清空。方格生成序=DisplayRank 定案序（红蓝绿黑白灰），非枚举序。</summary>
        public void SetCosts(ElementCost costs)
        {
            if (costs == null || costs.IsZero) { Clear(); return; }
            SetCosts(costs.NonzeroColors()
                .Select(c => new ElementCostRef { mana = (int)c, value = (int)costs[c] })
                .Where(r => r.value > 0));
        }

        /// <summary>瘦引用口径（ElementCostRef 列表）。空则清空。
        /// 负责生成对应数量+颜色的方块并按 Fill 分布定位（2026-10-13 拓展）；
        /// 生成前先按 DisplayRank 排序（三色优先、灰垫底，调用方传入序不保留）；
        /// 根上有 LayoutGroup 的烘焙实例排布归引用者，本组件不越权。</summary>
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
            list.Sort((a, b) => DisplayRank((ManaType)a.mana).CompareTo(DisplayRank((ManaType)b.mana)));
            _squares.Clear();
            _squareMana.Clear();
            foreach (var c in list)
            {
                var color = ManaColor((ManaType)c.mana);
                for (int k = 0; k < c.value; k++)
                {
                    var sq = Instantiate(_template.gameObject, transform, false);
                    sq.name = "square";
                    sq.SetActive(true);
                    var img = sq.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = color;
                        img.raycastTarget = false;
                    }
                    _squares.Add((RectTransform)sq.transform);
                    _squareMana.Add(c.mana);
                }
            }
            Relayout();
        }

        public void Clear() => SetCosts((IEnumerable<ElementCostRef>)null);

        private void OnEnable() => Relayout();

        /// <summary>根 RectTransform 尺寸变化（引用者 Band/锚点拉伸/缩放后）→ 重新均布。</summary>
        private void OnRectTransformDimensionsChange() => Relayout();

        /// <summary>自读根 rect 宽高，把已生成方格按 Fill 均分定位（2026-10-13 拓展）：
        /// 横向=单行、竖向=单列、田字=列数=round(sqrt(N×宽高比)) 的近方网格（行序=左上起行优先）；
        /// 田字换行=同色整组制（2026-10-15）：同色段尽量不跨行——段起点处当前行剩余格装不下整段
        /// 但整段 ≤ 一行容量 → 行尾留空、整段挪到新行头；整段 &gt; 一行容量（实在放不下）→连续排，
        /// 跨行尾部与后续色段混一行。
        /// cell=宽/列×高/行，方格锚 cell 中心、尺寸默认=模板尺寸、拥挤时等比缩到 cell
        /// ——元素再多也整体落在根 rect 内。根上有 LayoutGroup / 宽高未就绪 / 无方格时跳过。</summary>
        private void Relayout()
        {
            if (_template == null) return;
            if (GetComponent<LayoutGroup>() != null) return; // 排布归引用者（旧契约）
            var rt = (RectTransform)transform;
            var rect = rt.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var active = new List<int>(_squares.Count);
            for (int i = 0; i < _squares.Count; i++)
                if (_squares[i] != null && _squares[i].gameObject.activeSelf) active.Add(i);
            int n = active.Count;
            if (n == 0) return;

            int cols;
            switch (fill)
            {
                case CostSquaresFill.Vertical:
                    cols = 1;
                    break;
                case CostSquaresFill.Grid:
                    cols = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(n * rect.width / rect.height)), 1, n);
                    break;
                default:
                    cols = n;
                    break;
            }

            // 每个活动方格的线性格位（行优先连续序）
            var cell = new int[n];
            if (fill == CostSquaresFill.Grid && _squareMana.Count == _squares.Count)
            {
                int linear = 0;
                for (int a = 0; a < n;)
                {
                    int len = 1;
                    while (a + len < n && _squareMana[active[a + len]] == _squareMana[active[a]]) len++;
                    int spaceLeft = cols - linear % cols; // 当前行剩余格
                    if (len > spaceLeft && len <= cols) linear += spaceLeft; // 整组挪新行（行尾留空）
                    for (int j = 0; j < len; j++) cell[a + j] = linear + j;
                    linear += len; // 整段 > 一行容量时此处连续排（跨行混排=实在放不下）
                    a += len;
                }
            }
            else
            {
                for (int i = 0; i < n; i++) cell[i] = i;
            }
            int rows = cell[n - 1] / cols + 1; // 横向 cols=n→1 行；竖向 cols=1→n 行；田字=含留空的真实行数

            float cellW = rect.width / cols, cellH = rect.height / rows;
            var tplSize = _template.sizeDelta; // 烘焙模板=作者定的方格默认尺寸（拥挤时的上限）
            bool tplValid = tplSize.x > 0f && tplSize.y > 0f;
            // 等比钳制：默认=模板尺寸；元素过多时按 cell 收缩（保持正方形不变形），永远不超根范围
            float k = tplValid ? Mathf.Min(1f, cellW / tplSize.x, cellH / tplSize.y) : 0f;
            var sqSize = tplValid
                ? new Vector2(tplSize.x * k, tplSize.y * k)
                : new Vector2(Mathf.Min(cellW, cellH), Mathf.Min(cellW, cellH)); // 模板尺寸非法兜底=cell 内切方
            for (int i = 0; i < n; i++)
            {
                var sq = _squares[active[i]];
                sq.anchorMin = sq.anchorMax = new Vector2(0f, 1f);
                sq.pivot = new Vector2(0.5f, 0.5f);
                sq.anchoredPosition = new Vector2((cell[i] % cols + 0.5f) * cellW, -(cell[i] / cols + 0.5f) * cellH);
                sq.sizeDelta = sqSize;
            }
        }

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
            rt.sizeDelta = new Vector2(10f, 10f); // 代码模板给与烘焙预制体一致的默认方格尺寸（RectTransform 原生默认 100×100）
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            go.SetActive(false);
            _template = rt;
        }
    }
}
