using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>
    /// 手牌拖拽 2D 箭头（2026-11-15 全拖拽改版）：从拖拽卡顶部中心指向指针——表达「打到哪」。
    /// 纯 C# 类绑定烘焙节点（BattleUI.prefab drag-arrow-layer 下 drag-arrow 线段 + drag-arrow-head
    /// 箭头，占位白块可换美术）；不占射线（raycastTarget=false）。
    /// 显示规则（定稿）：**法术不显示**；生物/结界=青蓝；地牌=绿；非法=红——颜色由宿主屏按
    /// 落点分类+合法性逐帧喂（CanUse/CanPlaceLand 钩子），本类只管几何与染色。
    /// 不用世界空间箭头（BattleAttackArrow 只服务 3D 选格）。
    /// </summary>
    public sealed class HandDragArrowView
    {
        public static readonly Color FieldColor = new Color(0.36f, 0.78f, 0.95f); // 战场=青蓝
        public static readonly Color LandColor = new Color(0.38f, 0.92f, 0.48f);  // 地牌=绿
        public static readonly Color BadColor = new Color(0.95f, 0.34f, 0.30f);   // 非法=红

        private RectTransform _layer, _line, _head;
        private Image _lineImg, _headImg;
        private bool _missingWarned;

        /// <summary>绑定烘焙节点（宿主屏 BattleUI 根下按名查找；缺件=不显示并告警一次）。</summary>
        public void Bind(Transform uiRoot)
        {
            var layer = uiRoot != null ? uiRoot.Find("drag-arrow-layer") : null;
            if (layer == null)
            {
                if (!_missingWarned)
                {
                    _missingWarned = true;
                    Debug.LogWarning("[HandDrag] BattleUI.prefab 缺 drag-arrow-layer 节点——拖拽箭头不显示（重跑烘焙菜单恢复）");
                }
                return;
            }
            _layer = layer as RectTransform;
            _line = layer.Find("drag-arrow") as RectTransform;
            _head = layer.Find("drag-arrow-head") as RectTransform;
            _lineImg = _line != null ? _line.GetComponent<Image>() : null;
            _headImg = _head != null ? _head.GetComponent<Image>() : null;
            if (_lineImg != null) _lineImg.raycastTarget = false;
            if (_headImg != null) _headImg.raycastTarget = false;
            Hide();
        }

        public bool Ready => _layer != null && _line != null && _head != null;

        public void Hide()
        {
            if (!Ready) return;
            if (_line.gameObject.activeSelf) _line.gameObject.SetActive(false);
            if (_head.gameObject.activeSelf) _head.gameObject.SetActive(false);
        }

        /// <summary>逐帧几何：起=拖拽卡顶中心（世界→层内），止=指针屏幕位；色=合法性档。</summary>
        public void Update(RectTransform card, Vector2 screenPointer, Color color)
        {
            if (!Ready || card == null) { Hide(); return; }
            var canvas = _layer.GetComponentInParent<Canvas>();
            if (canvas == null) { Hide(); return; }
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var topWorld = card.TransformPoint(new Vector3(0f, card.rect.height * 0.5f, 0f));
            var topScreen = (Vector2)RectTransformUtility.WorldToScreenPoint(uiCam, topWorld);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, topScreen, uiCam, out var a)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screenPointer, uiCam, out var b))
            { Hide(); return; }
            var delta = b - a;
            float dist = delta.magnitude;
            if (dist < 16f) { Hide(); return; } // 指针贴卡顶：箭头无意义
            float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            _line.anchoredPosition = a;
            _line.localEulerAngles = new Vector3(0f, 0f, ang);
            _line.sizeDelta = new Vector2(Mathf.Max(4f, dist - 22f), 8f);
            _head.anchoredPosition = b;
            _head.localEulerAngles = new Vector3(0f, 0f, ang);
            _lineImg.color = color;
            _headImg.color = color;
            if (!_line.gameObject.activeSelf) _line.gameObject.SetActive(true);
            if (!_head.gameObject.activeSelf) _head.gameObject.SetActive(true);
        }
    }
}
