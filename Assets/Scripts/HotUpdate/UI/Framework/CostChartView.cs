using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>
    /// 费用分布图（2026-11-10 S7c）：**粗粒度 4 档**版本——联机屏卡组池行与掷币段共用。
    /// 分档（2026-11-10 修正）：【0-2】【3-5】【6-8】【9+】（cost≤2→1，≤5→2，≤8→3，≥9→4），
    /// 与卡组界面细分档 0..8+ 同一起点。
    /// 卡组界面（DeckBuilder）仍用 stats-row 的 9 档细分布（0..8+），不走本组件。
    /// 预制体 Assets/Art/Prefab/CostChart.prefab：4 列 col-1..col-4，列下 bar / count / tier；
    /// 柱高/张数位置由本组件直写 RectTransform（不依赖 LayoutGroup，避免滚动列表 content
    /// childControlHeight=false 吞高度的问题）。
    /// </summary>
    public sealed class CostChartView : MonoBehaviour
    {
        public const string PrefabName = "CostChart";
        public const int BucketCount = 4;
        public const float ChartWidth = 150f;
        public const float ChartHeight = 78f;

        /// <summary>分段标签正本在代码（改段定案 2026-11-10）——预制体烘焙的 1-3/3-6/6-9 已过期，
        /// EnsureCache 首绑时重写。</summary>
        private static readonly string[] TierLabels = { "0-2", "3-5", "6-8", "9+" };

        private const float MaxBarHeight = 44f;
        private const float MinBarHeight = 2f;
        private const float TierHeight = 14f;

        private RectTransform[] _bars;
        private TMP_Text[] _counts;

        /// <summary>绑定 4 桶数据（下标 0..3 = 1-3 / 3-6 / 6-9 / 9+）。</summary>
        public void Bind(int[] buckets)
        {
            EnsureCache();
            int max = 1;
            if (buckets != null)
                for (int i = 0; i < BucketCount && i < buckets.Length; i++)
                    if (buckets[i] > max) max = buckets[i];

            for (int i = 0; i < BucketCount; i++)
            {
                int count = buckets != null && i < buckets.Length ? Mathf.Max(0, buckets[i]) : 0;
                float h = count > 0 ? Mathf.Max(MinBarHeight, MaxBarHeight * count / max) : MinBarHeight;
                var bar = _bars[i];
                if (bar != null)
                {
                    bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
                    bar.pivot = new Vector2(0.5f, 0f);
                    bar.anchoredPosition = new Vector2(0f, TierHeight);
                    bar.sizeDelta = new Vector2(bar.sizeDelta.x, h);
                }
                if (_counts[i] != null)
                {
                    _counts[i].text = count > 0 ? count.ToString() : "";
                    ((RectTransform)_counts[i].transform).anchoredPosition =
                        new Vector2(0f, TierHeight + h + 2f);
                }
            }
        }

        private void EnsureCache()
        {
            if (_bars != null) return;
            _bars = new RectTransform[BucketCount];
            _counts = new TMP_Text[BucketCount];
            for (int i = 0; i < BucketCount; i++)
            {
                var col = transform.Find("col-" + (i + 1));
                if (col == null) continue;
                _bars[i] = col.Find("bar") as RectTransform;
                var count = col.Find("count");
                _counts[i] = count != null ? count.GetComponent<TMP_Text>() : null;
                var tier = col.Find("tier");
                var tierText = tier != null ? tier.GetComponent<TMP_Text>() : null;
                if (tierText != null) tierText.text = TierLabels[i];
            }
        }

        /// <summary>按预制体实例化（挂 parent，固定宽高）。加载失败回退 null。
        /// 预制体根无 ILayoutElement 贡献者——父布局组（childControl=true）会把裸实例压成 0 尺寸，
        /// 故 LE 钉 preferred=固定宽高（UiKit.Size 双轨：父不控轴时退回直写 rect）。</summary>
        public static CostChartView Create(RectTransform parent)
        {
            if (parent == null) return null;
            var go = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>(PrefabName, UiKit.PrefabPath(PrefabName));
            if (go == null)
            {
                Debug.LogError("[CostChart] 预制体缺失：" + UiKit.PrefabPath(PrefabName));
                return null;
            }
            var inst = Instantiate(go, parent, false);
            inst.name = PrefabName;
            var rt = (RectTransform)inst.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ChartWidth, ChartHeight);
            UiKit.Size(rt, w: ChartWidth, h: ChartHeight);
            return inst.GetComponent<CostChartView>();
        }

        /// <summary>按卡组主列表统计 4 桶【0-2】【3-5】【6-8】【9+】（cost≤2→0，≤5→1，≤8→2，≥9→3；
        /// TotalCost 四舍五入——0 费卡归第 1 桶，与卡组界面细分档 0..8+ 同一起点）。</summary>
        public static int[] BucketsOf(IReadOnlyList<string> cardIds)
        {
            var buckets = new int[BucketCount];
            if (cardIds == null) return buckets;
            for (int i = 0; i < cardIds.Count; i++)
            {
                var card = CardCatalog.GetById(cardIds[i]);
                if (card == null) continue;
                int cost = Mathf.RoundToInt(card.TotalCost);
                int idx = cost <= 2 ? 0 : cost <= 5 ? 1 : cost <= 8 ? 2 : 3;
                buckets[idx]++;
            }
            return buckets;
        }

        /// <summary>9 桶 manifest 直方图（协议口径 1..9+：&lt;1 归 1、&gt;9 归 9，桶 i=费用 i+1）
        /// 合成 4 粗桶：桶 0..1（费 1-2）→【0-2】、2..4（费 3-5）→【3-5】、5..7（费 6-8）→【6-8】、
        /// 8（费 9+）→【9+】——精确对应零损失；协议不动（NetRoom 仍算 9 桶），粗化只在此口发生。</summary>
        public static int[] CoarseFromNine(int[] nine)
        {
            var buckets = new int[BucketCount];
            if (nine == null) return buckets;
            for (int i = 0; i < nine.Length && i < 9; i++)
            {
                int idx = i <= 1 ? 0 : i <= 4 ? 1 : i <= 7 ? 2 : 3;
                buckets[idx] += Mathf.Max(0, nine[i]);
            }
            return buckets;
        }

        /// <summary>卡组四色计数单源文案（stats-row 颜色分布/联机屏卡组行 lbl-color 共用；格式=
        /// 烘焙占位同款）：两行「● 红 n&lt;TAB&gt;● 蓝 n」/「● 绿 n&lt;TAB&gt;● 灰 n」——彩点富文本
        /// 色取 UiStyle.Dot* 令牌（与 stats-row 烘焙值一致）；多色卡在构成色各计一次。</summary>
        public static string ColorLineOf(IReadOnlyList<string> cardIds)
        {
            int red = 0, blue = 0, green = 0, gray = 0;
            if (cardIds != null)
                for (int i = 0; i < cardIds.Count; i++)
                {
                    var card = CardCatalog.GetById(cardIds[i]);
                    if (card == null) continue;
                    if (CardSorter.HasCostColor(card, UIColor.Red)) red++;
                    if (CardSorter.HasCostColor(card, UIColor.Blue)) blue++;
                    if (CardSorter.HasCostColor(card, UIColor.Green)) green++;
                    if (CardSorter.HasCostColor(card, UIColor.Gray)) gray++;
                }
            return ColorLineOf(red, blue, green, gray);
        }

        /// <summary>四色计数 → 文案（2026-11-13 拆口）：格式与 ColorLineOf(cardIds) 单源同款；
        /// 卡组构筑屏改喂 DeckStatsAccountant 单元口径计数（代价色优先/全 0 灰），经由本口呈现。</summary>
        public static string ColorLineOf(int red, int blue, int green, int gray)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(UiStyle.DotRed)}>●</color> 红 {red}\t"
                + $"<color=#{ColorUtility.ToHtmlStringRGB(UiStyle.DotBlue)}>●</color> 蓝 {blue}\n"
                + $"<color=#{ColorUtility.ToHtmlStringRGB(UiStyle.DotGreen)}>●</color> 绿 {green}\t"
                + $"<color=#{ColorUtility.ToHtmlStringRGB(UiStyle.DotGray)}>●</color> 灰 {gray}";
        }

        // ================= deck-row 预制体绑定单源（联机屏卡组池行 / 战局摘要 self/opp 行共用） =================

        /// <summary>deck-row 绑定（有完整卡表）：deckname 文本 + body 4 桶柱组 + header 颜色行。</summary>
        public static void BindDeckRow(RectTransform row, string deckName, IReadOnlyList<string> cardIds)
        {
            if (row == null) return;
            SetDeckRowName(row, deckName);
            var colorLabel = DeckRowColorLabel(row);
            if (colorLabel != null) colorLabel.text = ColorLineOf(cardIds);
            BindDeckRowBars(row, BucketsOf(cardIds));
        }

        /// <summary>deck-row 绑定（对手·manifest 9 桶直方图口径——对手卡表对局结束才揭晓）：
        /// 费用 4 桶=CoarseFromNine，颜色行置空（MatchSummary 到手后可再走 BindDeckRow 补全）。</summary>
        public static void BindDeckRowHistogram(RectTransform row, string deckName, int[] nine)
        {
            if (row == null) return;
            SetDeckRowName(row, deckName);
            var colorLabel = DeckRowColorLabel(row);
            if (colorLabel != null) colorLabel.text = "";
            BindDeckRowBars(row, CoarseFromNine(nine));
        }

        private static void SetDeckRowName(RectTransform row, string deckName)
        {
            var t = UiKit.FindDeep(row, "deckname")?.GetComponent<TMP_Text>();
            if (t != null && !string.IsNullOrEmpty(deckName)) t.text = deckName;
        }

        /// <summary>deck-row 内「颜色分布」正文 label：header 下标题之外的第一个 TMP
        ///（烘焙占位=ColorLineOf 同款彩点文案）。</summary>
        private static TMP_Text DeckRowColorLabel(RectTransform row)
        {
            var header = UiKit.FindDeep(row, "header");
            if (header == null) return null;
            TMP_Text body = null;
            foreach (var t in header.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.text == "颜色分布") continue; // 标题 label
                if (body == null) body = t;
            }
            return body;
        }

        /// <summary>deck-row 费用 4 档柱组绑定：body 下 stat-bar 按烘焙 tier 文本对桶（0-2/3-5/6-8/9+）
        /// ——柱高=张数/最大×52（bar 的 LE preferredHeight，空 2），count=张数（0 藏字）。
        /// 认不出的 tier 跳过（宁缺勿错桶）。</summary>
        private static void BindDeckRowBars(RectTransform row, int[] buckets)
        {
            var body = UiKit.FindDeep(row, "body");
            if (body == null) return;
            int max = 1;
            if (buckets != null)
                foreach (var b in buckets)
                    if (b > max) max = b;
            for (int i = 0; i < body.childCount; i++)
            {
                var col = body.GetChild(i) as RectTransform;
                if (col == null || col.name != "stat-bar") continue;
                var tier = UiKit.FindDeep(col, "tier")?.GetComponent<TMP_Text>();
                int idx = CoarseTierIndexOf(tier != null ? tier.text : null);
                if (idx < 0) continue;
                int count = buckets != null && idx < buckets.Length ? buckets[idx] : 0;
                var bar = UiKit.FindDeep(col, "bar");
                var barLe = bar != null ? bar.GetComponent<LayoutElement>() : null;
                if (barLe != null)
                    barLe.preferredHeight = count > 0
                        ? Mathf.Max(6f, (int)(52f * count / max)) : 2f;
                var countText = UiKit.FindDeep(col, "count")?.GetComponent<TMP_Text>();
                if (countText != null) countText.text = count > 0 ? count.ToString() : "";
            }
        }

        /// <summary>烘焙 tier 文本→粗桶序（0-2→0 / 3-5→1 / 6-8→2 / 9+→3）；认不出返回 -1。</summary>
        private static int CoarseTierIndexOf(string tier)
        {
            switch ((tier ?? "").Trim())
            {
                case "0-2": return 0;
                case "3-5": return 1;
                case "6-8": return 2;
                case "9+": return 3;
                default: return -1;
            }
        }
    }
}
