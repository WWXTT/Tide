using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>
    /// 费用色块视图（2026-10-24 改版：不再按数量展开小方格——每色生成一个色块，
    /// 块上 TMP 文本显示数量；模板 tpl-square=色块 Image+数量文本，公用预制体手改已备，
    /// 旧烘焙模板无文本时自愈补建）。仅横向排布（竖向/田字 Fill 随按数量方格一并退役）：
    /// 非零色按 DisplayRank 定案序（红蓝绿黑白灰）单行均布，块尺寸默认=模板尺寸、
    /// 拥挤时等比收缩到 cell（数量文本字号同比例缩放，块再小数字仍嵌在块内）；
    /// SetCosts / 根尺寸变化时自动重排。
    /// 底色=UiStyle.ManaBlock* 七组配色单源（六色+任意；与战场3D 地牌锁色环同源共用），
    /// 数字色=DigitColor 配对（深调压饱和底+柔和近白数字、白块反转浅底深字，
    /// 对比度全组 ≥4.5:1——不刺眼且能看清）。
    /// 根上有 LayoutGroup 时跳过重排（排布仍归引用者烘焙，2026-10-03 旧契约保留）。
    /// 自愈三兜底：无模板→代码自建 tpl-square；无预制体→Mount 纯代码节点；引用者烘焙
    /// 同名节点→直接复用（不实例化）。
    /// 2026-10-15 生成序定案沿用：红蓝绿黑白灰（三色优先、灰垫底——组件内排序，调用方传入序不保留）。
    /// </summary>
    public sealed class CostSquaresView : MonoBehaviour
    {
        public const string PrefabName = "CostSquares";
        public const string TemplateName = "tpl-square";

        private RectTransform _template;
        private TMP_Text _templateText; // 模板数量文本（实例字号缩放基准；模板无文本时自愈补建）
        private readonly List<RectTransform> _squares = new List<RectTransform>(); // 本视图生成的色块（Destroy 帧末生效，定位不看 childCount）
        private readonly List<int> _squareMana = new List<int>();  // 与 _squares 平行的块色（ManaType 序号）
        private readonly List<int> _squareValue = new List<int>(); // 与 _squares 平行的块上数量

        /// <summary>色块生成序（2026-10-15；2026-10-28 P2 席位收缩后无黑白）：三色红蓝绿优先、灰垫底。
        /// 未知色与灰同档排末尾。</summary>
        private static readonly ManaType[] DisplayOrder =
        {
            ManaType.Red, ManaType.Blue, ManaType.Green,
        };

        private static int DisplayRank(ManaType m)
        {
            int i = Array.IndexOf(DisplayOrder, m);
            return i >= 0 ? i : DisplayOrder.Length; // 灰及未来扩色恒排最后
        }

        /// <summary>数量数字色（共用柔和近白一档——白块反转档已随白席位删除）。</summary>
        private static readonly Color DigitLight = new Color(238f / 255f, 241f / 255f, 246f / 255f);

        /// <summary>色块底色（2026-10-24 配色定案，单源=UiStyle.ManaBlock*——与战场3D 地牌
        /// 锁色环同源共用）：Dot* 是深底小圆点的提亮饱和版，直接作大色块底承数字对比不足
        /// （白字对 DotGreen 仅约 2.2:1）且高饱和刺眼——深调压饱和底色：四色色相保持可辨、
        /// 深底不刺眼，数字对比全组 ≥4.5:1。</summary>
        private static Color BlockColor(ManaType m) => UiStyle.ManaBlockColor(m);

        private static Color DigitColor(ManaType m) => DigitLight;

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
        /// 全零/空则清空。色块生成序=DisplayRank 定案序（红蓝绿黑白灰），非枚举序。</summary>
        public void SetCosts(ElementCost costs)
        {
            if (costs == null || costs.IsZero) { Clear(); return; }
            SetCosts(costs.NonzeroColors()
                .Select(c => new ElementCostRef { mana = (int)c, value = (int)costs[c] })
                .Where(r => r.value > 0));
        }

        /// <summary>瘦引用口径（ElementCostRef 列表）。空则清空。
        /// 2026-10-24 改版：每色一个色块+块上数量文本（同色多条目合并数量，数量恒显示）；
        /// 生成前按 DisplayRank 排序（三色优先、灰垫底，调用方传入序不保留）；
        /// 根上有 LayoutGroup 的烘焙实例排布归引用者，本组件不越权。</summary>
        public void SetCosts(IEnumerable<ElementCostRef> costs)
        {
            EnsureTemplate();

            var merged = new Dictionary<int, int>(); // 每色一块：同色多条目合并数量
            if (costs != null)
                foreach (var c in costs)
                    if (c != null && c.value > 0)
                        merged[c.mana] = merged.TryGetValue(c.mana, out var prev) ? prev + c.value : c.value;
            var list = merged
                .Select(kv => (mana: kv.Key, value: kv.Value))
                .OrderBy(p => DisplayRank((ManaType)p.mana))
                .ToList();

            // 差量短路（2026-10-18 性能）：卡面事件级刷新极常见值未变——同「色序+数量」直退，
            // 免整组销毁重建（Instantiate/Destroy+布局脏）与重排
            if (list.Count == _squares.Count)
            {
                bool same = true;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].mana != _squareMana[i] || list[i].value != _squareValue[i]) { same = false; break; }
                if (same) return;
            }

            // 原位对账：同位改色改数 / 多余销毁 / 缺口补建——色块除色与数量外无独立状态，可安全复用
            int overlap = Mathf.Min(list.Count, _squares.Count);
            for (int i = 0; i < overlap; i++)
            {
                if (list[i].mana == _squareMana[i] && list[i].value == _squareValue[i]) continue;
                _squareMana[i] = list[i].mana;
                _squareValue[i] = list[i].value;
                ApplySquare(_squares[i], list[i].mana, list[i].value);
            }
            for (int i = _squares.Count - 1; i >= list.Count; i--)
            {
                _squares[i].gameObject.SetActive(false); // 先失活：Destroy 帧末生效，失活让 Relayout 本帧就按新块数均布
                Destroy(_squares[i].gameObject);
                _squares.RemoveAt(i);
                _squareMana.RemoveAt(i);
                _squareValue.RemoveAt(i);
            }
            for (int i = _squares.Count; i < list.Count; i++)
            {
                var sq = Instantiate(_template.gameObject, transform, false);
                sq.name = "square";
                sq.SetActive(true);
                _squares.Add((RectTransform)sq.transform);
                _squareMana.Add(list[i].mana);
                _squareValue.Add(list[i].value);
                ApplySquare(_squares[i], list[i].mana, list[i].value);
            }
            Relayout();
        }

        public void Clear() => SetCosts((IEnumerable<ElementCostRef>)null);

        /// <summary>色块落值：Image 染块底色 + 数量文本写数（单行不折行、不吃射线）；
        /// 底色/数字色=六组配色定案（BlockColor/DigitColor，覆盖模板文本默认色）。</summary>
        private void ApplySquare(RectTransform sq, int mana, int value)
        {
            var img = sq.GetComponent<Image>();
            if (img != null)
            {
                img.color = BlockColor((ManaType)mana);
                img.raycastTarget = false;
            }
            var t = sq.GetComponentInChildren<TMP_Text>(true);
            if (t == null) return;
            t.text = value.ToString();
            t.textWrappingMode = TextWrappingModes.NoWrap; // 数量恒单行（多位数不让模板折行设置折成两行）
            t.raycastTarget = false;
            t.color = DigitColor((ManaType)mana);
        }

        private void OnEnable() => Relayout();

        /// <summary>根 RectTransform 尺寸变化（引用者 Band/锚点拉伸/缩放后）→ 重新均布。</summary>
        private void OnRectTransformDimensionsChange() => Relayout();

        /// <summary>对外显式重排口：引用者改根尺寸后显式补调一次（非拉伸锚实例收不到
        /// OnRectTransformDimensionsChange；ACardZoom 退役后暂无调用方，留作通用口）。</summary>
        public void RefreshLayout() => Relayout();

        /// <summary>自读根 rect 宽高，把已生成色块单行均布（2026-10-24 改版：仅横向）：
        /// cell=宽/块数×高，色块锚 cell 中心、尺寸默认=模板尺寸、拥挤时等比缩到 cell
        /// （数量文本字号按同一比例缩放）——色块再多也整体落在根 rect 内。
        /// 根上有 LayoutGroup / 宽高未就绪 / 无色块时跳过。</summary>
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

            float cellW = rect.width / n, cellH = rect.height;
            var tplSize = _template.sizeDelta; // 烘焙模板=作者定的色块默认尺寸（拥挤时的上限）
            bool tplValid = tplSize.x > 0f && tplSize.y > 0f;
            // 等比钳制：默认=模板尺寸；色块过多时按 cell 收缩（保持正方形不变形），永远不超根范围
            float k = tplValid ? Mathf.Min(1f, cellW / tplSize.x, cellH / tplSize.y) : 0f;
            var sqSize = tplValid
                ? new Vector2(tplSize.x * k, tplSize.y * k)
                : new Vector2(Mathf.Min(cellW, cellH), Mathf.Min(cellW, cellH)); // 模板尺寸非法兜底=cell 内切方
            for (int i = 0; i < n; i++)
            {
                var sq = _squares[active[i]];
                sq.anchorMin = sq.anchorMax = new Vector2(0f, 1f);
                sq.pivot = new Vector2(0.5f, 0.5f);
                sq.anchoredPosition = new Vector2((i + 0.5f) * cellW, -0.5f * cellH);
                sq.sizeDelta = sqSize;
                var t = sq.GetComponentInChildren<TMP_Text>(true);
                if (t != null && _templateText != null)
                    t.fontSize = _templateText.fontSize * k; // 字号随块等比缩放（模板字号=模板尺寸档基准）
            }
        }

        private void EnsureTemplate()
        {
            if (_template != null) return;
            var t = transform.Find(TemplateName);
            if (t != null)
            {
                _template = (RectTransform)t;
            }
            else
            {
                var go = new GameObject(TemplateName, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = new Vector2(30f, 30f); // 代码模板给与烘焙预制体一致的默认色块尺寸（RectTransform 原生默认 100×100）
                var img = go.GetComponent<Image>();
                img.color = Color.white;
                img.raycastTarget = false;
                go.SetActive(false);
                _template = rt;
            }
            // 旧烘焙模板自愈：无数量文本（2026-10-24 改版前产物）→ 补建（铺满色块、居中、白字）
            _templateText = _template.GetComponentInChildren<TMP_Text>(true);
            if (_templateText == null) _templateText = BuildTemplateText(_template);
        }

        /// <summary>代码兜底数量文本：铺满色块、居中、白字（与公用预制体模板同构）。</summary>
        private static TMP_Text BuildTemplateText(RectTransform square)
        {
            var t = UiKit.Label("count", square, "0", 25, Color.white, TextAnchor.MiddleCenter);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return t;
        }
    }
}
