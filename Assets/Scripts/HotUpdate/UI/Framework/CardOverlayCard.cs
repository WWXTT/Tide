using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// UGUI 卡实例视图：运行时由 CardOverlayController 挂到 BattleCardUGUI.prefab 实例上
    /// （prefab 只含命名层级、不带脚本——美术改样式零断链风险），按子物体名解析字段，
    /// Apply 按布局形态开关区块/排文字。子物体缺失时各区块空转不崩（prefab 未就绪可降级）。
    /// </summary>
    public sealed class CardOverlayCard : MonoBehaviour
    {
        // 六向箭头子物体名（位序与 BattleView 六向位对应）
        private static readonly string[] ArrowNames =
            { "arrow-ne", "arrow-e", "arrow-se", "arrow-sw", "arrow-w", "arrow-nw" };
        private static readonly int[] ArrowBits =
            { BattleView.ArrowNE, BattleView.ArrowE, BattleView.ArrowSE, BattleView.ArrowSW, BattleView.ArrowW, BattleView.ArrowNW };

        private Image _art;
        private TMP_Text _name, _cost, _type, _stats, _keywords, _effect, _land;
        private TMP_Text _markTapped, _markFrozen, _markDead, _btnRemove;
        private readonly Dictionary<string, GameObject> _arrows = new Dictionary<string, GameObject>();
        private CanvasGroup _group;

        /// <summary>构筑区右上角×（按钮由控制器挂）。</summary>
        public GameObject RemoveButton => _btnRemove != null ? _btnRemove.gameObject : null;

        public void Bind()
        {
            _art = FindComponent<Image>("art");
            _name = FindComponent<TMP_Text>("name");
            _cost = FindComponent<TMP_Text>("cost");
            _type = FindComponent<TMP_Text>("type");
            _stats = FindComponent<TMP_Text>("stats");
            _keywords = FindComponent<TMP_Text>("keywords");
            _effect = FindComponent<TMP_Text>("effect");
            _land = FindComponent<TMP_Text>("land");
            _markTapped = FindComponent<TMP_Text>("mark-tapped");
            _markFrozen = FindComponent<TMP_Text>("mark-frozen");
            _markDead = FindComponent<TMP_Text>("mark-dead");
            _btnRemove = FindComponent<TMP_Text>("btn-remove");
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            // 字体已由预制体烘焙为庞门正道标题体 SDF（批量整形脚本保证），运行时不再覆写。

            _arrows.Clear();
            foreach (var arrowName in ArrowNames)
            {
                var t = transform.Find(arrowName);
                if (t != null) _arrows[arrowName] = t.gameObject;
            }
        }

        public void Apply(CardOverlayItem item, CardOverlayLayout layout)
        {
            bool full = layout == CardOverlayLayout.Full;
            bool land = layout == CardOverlayLayout.Land;

            SetText(_name, item.Name, full ? 13 : land ? 10 : 11);
            SetText(_cost, string.IsNullOrEmpty(item.CostText) ? "" : "费 " + item.CostText, full ? 10 : 10);
            SetText(_type, item.TypeText, 10);
            SetText(_keywords, item.KeywordsText, 10);
            SetText(_effect, item.EffectText, 10);
            SetText(_stats, item.StatsText, full ? 12 : 11);
            SetText(_land, string.IsNullOrEmpty(item.LandTokensText) ? "（耗尽）" : item.LandTokensText, 11);

            if (full)
            {
                Band(_art != null ? _art.rectTransform : null, 0.62f, 1.00f, true);
                Band(Rt(_name), 0.545f, 0.62f, true);
                Band(Rt(_type), 0.480f, 0.545f, true);
                Band(Rt(_cost), 0.415f, 0.480f, true);
                Band(Rt(_keywords), 0.350f, 0.415f, true);
                Band(Rt(_effect), 0.100f, 0.350f, true);
                Band(Rt(_stats), 0.000f, 0.100f, true);
                Band(Rt(_land), 0, 1, false);
            }
            else if (land)
            {
                Band(_art != null ? _art.rectTransform : null, 0, 1, false);
                Band(Rt(_name), 0.62f, 1.00f, true);
                Band(Rt(_cost), 0, 1, false);
                Band(Rt(_type), 0, 1, false);
                Band(Rt(_keywords), 0, 1, false);
                Band(Rt(_effect), 0, 1, false);
                Band(Rt(_stats), 0, 1, false);
                Band(Rt(_land), 0.00f, 0.62f, true);
            }
            else // Compact：战场单位小卡——名称/费用/实时身材
            {
                Band(_art != null ? _art.rectTransform : null, 0, 1, false);
                Band(Rt(_name), 0.55f, 1.00f, true);
                Band(Rt(_cost), 0.30f, 0.55f, true);
                Band(Rt(_type), 0, 1, false);
                Band(Rt(_keywords), 0, 1, false);
                Band(Rt(_effect), 0, 1, false);
                Band(Rt(_stats), 0.00f, 0.30f, true);
                Band(Rt(_land), 0, 1, false);
            }

            SetActive(_markTapped, item.IsTapped);
            SetActive(_markFrozen, item.IsFrozen);
            SetActive(_markDead, item.IsDead);

            for (int i = 0; i < ArrowNames.Length; i++)
            {
                if (_arrows.TryGetValue(ArrowNames[i], out var go) && go != null)
                    go.SetActive((item.ArrowFlags & ArrowBits[i]) != 0);
            }

            if (_group != null) _group.alpha = item.IsTapped ? 0.62f : 1f;
        }

        public void SetRemoveVisible(bool visible) => SetActive(_btnRemove, visible);

        private T FindComponent<T>(string childName) where T : Component
        {
            var t = transform.Find(childName);
            return t != null ? t.GetComponent<T>() : null;
        }

        private static RectTransform Rt(TMP_Text t) => t != null ? t.rectTransform : null;

        private static void SetText(TMP_Text t, string value, int fontSize)
        {
            if (t == null) return;
            t.text = value ?? "";
            t.fontSize = fontSize;
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }

        /// <summary>把文本区块铺到卡面纵向频带（anchorMin.x=0 / anchorMax.x=1，内缩 3px）。</summary>
        private static void Band(RectTransform t, float y0, float y1, bool active)
        {
            if (t == null) return;
            if (t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
            if (!active) return;
            t.anchorMin = new Vector2(0f, y0);
            t.anchorMax = new Vector2(1f, y1);
            t.offsetMin = new Vector2(3f, 2f);
            t.offsetMax = new Vector2(-3f, -2f);
        }
    }
}
