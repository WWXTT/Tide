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
    /// 费用色块视图（2026-11-03 双组改版）：根下两组色块——Cost1=费用组成（SetCosts）、
    /// Cost2=代价组成（SetPayloadCosts）。组标签节点按名解析（Cost1/Cost2，prefab 手摆）；
        /// tpl-square 模板**按需复制**：色块克隆自模板，按 DisplayRank 色序（红蓝绿灰万能）
        /// 排序插到所属组标签之后；模板自身（含同名多块）恒失活永不显示；**有代价才显示
        /// Cost2**（无代价组恒藏，只显示 Cost1）；费用=0 显单块灰「0」占位（null 才清空，2026-11-03）。
    /// 本组件**不再控制色块尺寸/位置/字号**（2026-11-03 删除自动布局，含 2026-10-30
    /// 的等比放大/收缩与字号缩放）：色块即模板尺寸，排布归引用者（根 LayoutGroup/
    /// ContentSizeFitter——公用预制体已带 HorizontalLayoutGroup）。
    /// 底色=UiStyle.ManaBlock* 五组配色单源（四色+任意；与战场3D 地牌锁色环同源共用），
    /// 数字色=DigitColor 配对（深调压饱和底+柔和近白数字，对比度全组 ≥4.5:1）。
    /// 无 Cost1/Cost2 标签（代码兜底/旧烘焙结构）＝匿名单组：SetCosts 照常（色块尾挂
    /// 根下），代价组无处安放、SetPayloadCosts 空转。
    /// 自愈兜底：无模板→代码自建 tpl-square；无预制体→Mount 纯代码节点；引用者烘焙
    /// 同名节点→直接复用（不实例化）。
    /// </summary>
    public sealed class CostSquaresView : MonoBehaviour
    {
        public const string PrefabName = "CostSquares";
        public const string TemplateName = "tpl-square";
        public const string FeeGroupName = "Cost1";      // 费用组成组标签
        public const string PayloadGroupName = "Cost2";  // 代价组成组标签

        /// <summary>一组色块状态（标签锚 + 原位对账三平行表）。</summary>
        private sealed class Group
        {
            public RectTransform Label; // 组标签（null=匿名单组——色块尾挂根下）
            public readonly List<RectTransform> Squares = new List<RectTransform>(); // 本组色块（Destroy 帧末生效，定位不看 childCount）
            public readonly List<int> SquareMana = new List<int>();   // 与 Squares 平行的块色（ManaType 序号）
            public readonly List<float> SquareValue = new List<float>(); // 与 Squares 平行的块上数量（真实小数）
        }

        private RectTransform _template;
        private Group _feeGroup;      // 费用组成（SetCosts）
        private Group _payloadGroup;  // 代价组成（SetPayloadCosts；无 Cost2 容器时为 null）

        /// <summary>色块生成序（2026-10-29 P4 五类定案：红→蓝→绿→灰→万能）。
        /// 未知色与万能同档排末尾。</summary>
        private static readonly ManaType[] DisplayOrder =
        {
            ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Gray, ManaType.Any,
        };

        private static int DisplayRank(ManaType m)
        {
            int i = Array.IndexOf(DisplayOrder, m);
            return i >= 0 ? i : DisplayOrder.Length; // 未知色恒排最后
        }

        /// <summary>数量数字色（共用柔和近白一档——白块反转档已随白席位删除）。</summary>
        private static readonly Color DigitLight = new Color(238f / 255f, 241f / 255f, 246f / 255f);

        /// <summary>色块底色（2026-10-24 配色定案，单源=UiStyle.ManaBlock*——与战场3D 地牌
        /// 锁色环同源共用）：Dot* 是深底小圆点的提亮饱和版，直接作大色块底承数字对比不足
        ///（白字对 DotGreen 仅约 2.2:1）且高饱和刺眼——深调压饱和底色：四色色相保持可辨、
        /// 深底不刺眼，数字对比全组 ≥4.5:1。</summary>
        private static Color BlockColor(ManaType m) => UiStyle.ManaBlockColor(m);

        private static Color DigitColor(ManaType m) => DigitLight;

        /// <summary>挂载：① parent 子树已有组件实例（引用者直接拖入公用预制体，拖入名=CostSquares）
        /// → 直接复用；② 同名烘焙节点（cost-squares）→ 补组件复用；③ 实例化公用预制体
        ///（HotUpdateAssets 地址=CostSquares、编辑器兜底路径）；④ 预制体缺失 → 纯代码节点兜底。
        ///（旧「跑装配菜单可补建」菜单已不存在——纯代码兜底即终态。）</summary>
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
                    Debug.LogWarning("[CostSquares] 公用预制体缺失——纯代码兜底");
                    go = new GameObject(nodeName, typeof(RectTransform));
                    ((RectTransform)go.transform).SetParent(parent, false);
                }
                go.name = nodeName;
                node = (RectTransform)go.transform;
            }
            return node.GetComponent<CostSquaresView>() ?? node.gameObject.AddComponent<CostSquaresView>();
        }

        /// <summary>0费占位（2026-11-03）：费用=0 显单块灰「0」不空白；null 仍是显式清空
        ///（Clear/无账单口径）。代价组无此待遇——有代价才显示 Cost2。绕过 MergeSorted
        ///（其口径滤 value&gt;0，0 占位进不去）。</summary>
        private static readonly List<(int mana, float value)> ZeroFeeSquares =
            new List<(int mana, float value)> { ((int)ManaType.Gray, 0f) };

        /// <summary>费用主口径（2026-10-04 位置数组：ElementCost，下标=ManaType 枚举序号）。
        /// null=清空**费用组**（不动代价组）；全零=单块灰「0」占位（2026-11-03——0费卡不再空白）。
        /// 色块生成序=DisplayRank 定案序（红蓝绿灰
        /// 万能），非枚举序。数量=真实小数直通（2026-10-30 定案——块上文本 0.## 直显，
        /// 支付终点才进1）。</summary>
        public void SetCosts(ElementCost costs)
        {
            if (costs == null) { SetCosts((IEnumerable<ElementCostRef>)null); return; }
            if (costs.IsZero) { EnsureGroups(); ApplyCosts(_feeGroup, ZeroFeeSquares); return; }
            SetCosts(costs.NonzeroColors()
                .Select(c => new ElementCostRef { mana = (int)c, value = costs[c] })
                .Where(r => r.value > 0f));
        }

        /// <summary>瘦引用口径（ElementCostRef 列表）。空则清空费用组。
        /// 每色一个色块+块上数量文本（同色多条目合并数量，数量恒显示）；
        /// 生成前按 DisplayRank 排序（红蓝绿灰万能——调用方传入序不保留）；
        /// 色块=模板按需克隆，尺寸/排布本组件不管（归引用者）。</summary>
        public void SetCosts(IEnumerable<ElementCostRef> costs)
        {
            EnsureGroups();
            ApplyCosts(_feeGroup, MergeSorted(costs));
        }

        /// <summary>代价组成（2026-11-03 双组改版）：色块插 Cost2 标签后；**有代价才显示
        /// Cost2**，空/全零=清空代价组并隐藏 Cost2。无 Cost2 容器（匿名单组结构）空转。
        /// 数据源=CardCostService.PayloadGrantByColor（施放期代价按色生成面——与净费抵扣/
        /// 地牌门槛同源口径），经 CardOverlayItem.PayloadCost 传入。</summary>
        public void SetPayloadCosts(ElementCost costs)
        {
            EnsureGroups();
            if (_payloadGroup == null) return; // 无 Cost2 容器（代码兜底/旧结构）
            var refs = costs == null || costs.IsZero ? null
                : costs.NonzeroColors()
                    .Select(c => new ElementCostRef { mana = (int)c, value = costs[c] })
                    .Where(r => r.value > 0f);
            ApplyCosts(_payloadGroup, MergeSorted(refs));
        }

        /// <summary>两组全清（费用组 + 代价组，Cost2 隐藏）。</summary>
        public void Clear()
        {
            SetCosts((IEnumerable<ElementCostRef>)null);
            SetPayloadCosts(null);
        }

        /// <summary>条目合并+排序：同色合并数量（真实小数）→ DisplayRank 定案序。</summary>
        private static List<(int mana, float value)> MergeSorted(IEnumerable<ElementCostRef> costs)
        {
            var merged = new Dictionary<int, float>(); // 每色一块：同色多条目合并数量（真实小数）
            if (costs != null)
                foreach (var c in costs)
                    if (c != null && c.value > 0f)
                        merged[c.mana] = merged.TryGetValue(c.mana, out var prev) ? prev + c.value : c.value;
            return merged
                .Select(kv => (mana: kv.Key, value: kv.Value))
                .OrderBy(p => DisplayRank((ManaType)p.mana))
                .ToList();
        }

        /// <summary>组内落块（原位对账）：同位改色改数 / 多余销毁 / 缺口按需克隆补建——
        /// 色块除色与数量外无独立状态，可安全复用。新块从模板克隆（保持模板尺寸与结构），
        /// 兄弟序插到组标签后（DisplayRank 序即组内次序）；代价组同步 Cost2 标签显隐。</summary>
        private void ApplyCosts(Group g, List<(int mana, float value)> list)
        {
            // 差量短路（2026-10-18 性能）：卡面事件级刷新极常见值未变——同「色序+数量」直退，
            // 免整组销毁重建（Instantiate/Destroy+布局脏）（小数带容差比——推导值浮点尾噪不触发重建）。
            // 组标签显隐与块数同构，短路时无需同步。
            if (list.Count == g.Squares.Count)
            {
                bool same = true;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].mana != g.SquareMana[i] || Mathf.Abs(list[i].value - g.SquareValue[i]) > 1e-4f) { same = false; break; }
                if (same) return;
            }

            int overlap = Mathf.Min(list.Count, g.Squares.Count);
            for (int i = 0; i < overlap; i++)
            {
                if (list[i].mana == g.SquareMana[i] && Mathf.Abs(list[i].value - g.SquareValue[i]) <= 1e-4f) continue;
                g.SquareMana[i] = list[i].mana;
                g.SquareValue[i] = list[i].value;
                ApplySquare(g.Squares[i], list[i].mana, list[i].value);
            }
            for (int i = g.Squares.Count - 1; i >= list.Count; i--)
            {
                g.Squares[i].gameObject.SetActive(false); // 先失活：本帧不再渲染
                if (Application.isPlaying) Destroy(g.Squares[i].gameObject); // 编辑模式 Destroy 被拒（探针/工具链）
                else DestroyImmediate(g.Squares[i].gameObject);
                g.Squares.RemoveAt(i);
                g.SquareMana.RemoveAt(i);
                g.SquareValue.RemoveAt(i);
            }
            for (int i = g.Squares.Count; i < list.Count; i++)
            {
                var sq = Instantiate(_template.gameObject, transform, false);
                sq.name = "square";
                sq.SetActive(true);
                g.Squares.Add((RectTransform)sq.transform);
                g.SquareMana.Add(list[i].mana);
                g.SquareValue.Add(list[i].value);
                ApplySquare(g.Squares[i], list[i].mana, list[i].value);
                if (g.Label != null) sq.transform.SetSiblingIndex(g.Label.GetSiblingIndex() + 1 + i); // 排序插组标签后
            }

            // 代价组标签显隐（2026-11-03）：有代价才显示 Cost2，否则只显示 Cost1
            if (g == _payloadGroup && g.Label != null && g.Label.gameObject.activeSelf != (list.Count > 0))
                g.Label.gameObject.SetActive(list.Count > 0);
        }

        /// <summary>色块落值：Image 染块底色 + 数量文本写数（单行不折行、不吃射线）；
        /// 底色/数字色=六组配色定案（BlockColor/DigitColor，覆盖模板文本默认色）。
        /// 数量=真实小数（2026-10-30）：0.## 直显（2 →「2」、0.5 →「0.5」、2.4 →「2.4」）。
        /// 不写尺寸/字号（2026-11-03 删除自动布局——克隆即模板态）。</summary>
        private void ApplySquare(RectTransform sq, int mana, float value)
        {
            var img = sq.GetComponent<Image>();
            if (img != null)
            {
                img.color = BlockColor((ManaType)mana);
                img.raycastTarget = false;
            }
            var t = sq.GetComponentInChildren<TMP_Text>(true);
            if (t == null) return;
            t.text = value.ToString("0.##");
            t.textWrappingMode = TextWrappingModes.NoWrap; // 数量恒单行（多位数不让模板折行设置折成两行）
            t.raycastTarget = false;
            t.color = DigitColor((ManaType)mana);
        }

        /// <summary>组解析（首次调用惰性执行）：根下有 Cost1+Cost2 标签＝双组模式；
        /// 否则匿名单组（代码兜底/旧烘焙结构——SetCosts 照常，代价组 null 空转）。
        /// 代价标签初始隐藏（有代价才显示）。</summary>
        private void EnsureGroups()
        {
            if (_feeGroup != null) return;
            EnsureTemplate();
            var label1 = transform.Find(FeeGroupName) as RectTransform;
            var label2 = transform.Find(PayloadGroupName) as RectTransform;
            if (label1 != null && label2 != null)
            {
                _feeGroup = new Group { Label = label1 };
                _payloadGroup = new Group { Label = label2 };
                if (label2.gameObject.activeSelf) label2.gameObject.SetActive(false);
            }
            else
            {
                _feeGroup = new Group(); // 匿名单组：色块尾挂根下
                _payloadGroup = null;
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
                rt.sizeDelta = new Vector2(30f, 30f); // 代码模板默认色块尺寸（RectTransform 原生默认 100×100）
                var img = go.GetComponent<Image>();
                img.color = Color.white;
                img.raycastTarget = false;
                go.SetActive(false);
                _template = rt;
            }
            // 模板永不显示（色块按需复制）：同名节点一并失活（prefab 手摆多块占位 tolerated）
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (c.name == TemplateName && c.gameObject.activeSelf) c.gameObject.SetActive(false);
            }
            // 旧烘焙模板自愈：无数量文本（2026-10-24 改版前产物）→ 补建（铺满色块、居中、白字）
            if (_template.GetComponentInChildren<TMP_Text>(true) == null) BuildTemplateText(_template);
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
