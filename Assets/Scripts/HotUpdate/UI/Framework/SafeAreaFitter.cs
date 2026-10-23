using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 异形屏安全区适配，双模式（2026-10-23 UI 适配 Phase A 定案）——按屏根「原始锚点」自动判定：
    /// - Stage（原始锚点=固定点，Main/Card/Effect/Deck 的 1920×1080 设计台）：设计尺寸、锚点/轴心
    ///   不动，整体定位到安全区中心；安全区装不下设计尺寸时等比缩小
    ///   （localScale=min(1, safeW/设计W, safeH/设计H)），绝不拉伸——任意比例不裁切不变形，
    ///   留边由 UIBootstrap 的 backdrop 画框填充。
    /// - Inset（原始锚点=拉伸，BattleUI/纯代码屏）：旧行为——锚点对齐 safeArea、偏移清零。
    /// 模式与设计尺寸在首个 OnEnable 从原始锚点/sizeDelta 缓存（本组件接管锚点后不再重判）。
    /// 真机 safeArea 晚到（启动数帧后才正确）由 Update 轮询自愈；分辨率/比例变化由父画布
    /// 尺寸哨兵捕获（Expand 下画布尺寸=比例的直接体现）。挂载父必须是全屏层（Canvas 根）。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>验证钩子：非 null 时替代 Screen.safeArea（屏幕像素口径），运行时恒 null。</summary>
        internal static Rect? DebugSafeAreaOverride;

        private RectTransform _rt;
        private Rect _applied;
        private Vector2 _appliedCanvasSize;
        private bool _modeLocked;
        private bool _stage;
        private Vector2 _designSize;
        private Vector2 _origAnchor;
        private Vector2 _origPivot;

        /// <summary>Stage 模式=设计台居中缩放（UIBootstrap 据此开关 backdrop 画框）。</summary>
        internal bool IsStage => _stage;

        private void OnEnable()
        {
            if (_rt == null) _rt = (RectTransform)transform;
            if (!_modeLocked)
            {
                _origAnchor = _rt.anchorMin;
                _origPivot = _rt.pivot;
                _designSize = _rt.sizeDelta; // 点锚根 rect.size==sizeDelta，不依赖布局时序
                _stage = _rt.anchorMin == _rt.anchorMax && _designSize.x > 1f && _designSize.y > 1f;
                _modeLocked = true;
            }
            Apply();
        }

        private void Update() => Apply(); // safeArea 变化无回调，轮询（哨兵相等即零成本早退）

        private void OnRectTransformDimensionsChange() => Apply(); // 分辨率/父层尺寸变化

        private void Apply()
        {
            if (_rt == null) return;
            var sa = DebugSafeAreaOverride ?? UnityEngine.Screen.safeArea;
            if (sa.width <= 0f || sa.height <= 0f) return; // 启动竞态：留待轮询
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            var pr = parent.rect;
            if (pr.width <= 0f || pr.height <= 0f) return; // Canvas 首帧未定尺寸：留待轮询
            if (sa == _applied && pr.size == _appliedCanvasSize) return;
            _applied = sa;
            _appliedCanvasSize = pr.size;

            // Overlay Canvas 根 rect=屏幕的画布单位像——safeArea 按屏幕归一化即画布内归一化坐标
            var w = (float)UnityEngine.Screen.width;
            var h = (float)UnityEngine.Screen.height;
            var sxMin = pr.xMin + pr.width * (sa.xMin / w);
            var sxMax = pr.xMin + pr.width * (sa.xMax / w);
            var syMin = pr.yMin + pr.height * (sa.yMin / h);
            var syMax = pr.yMin + pr.height * (sa.yMax / h);

            if (!_stage)
            {
                // Inset：锚点对齐 safeArea、偏移清零
                _rt.anchorMin = new Vector2(sa.xMin / w, sa.yMin / h);
                _rt.anchorMax = new Vector2(sa.xMax / w, sa.yMax / h);
                _rt.offsetMin = Vector2.zero;
                _rt.offsetMax = Vector2.zero;
                return;
            }

            // Stage：设计尺寸不动，中心对齐安全区中心；装不下时整体等比缩小（绝不拉伸）
            _rt.anchorMin = _origAnchor;
            _rt.anchorMax = _origAnchor;
            _rt.pivot = _origPivot;
            var scale = Mathf.Min(1f, (sxMax - sxMin) / _designSize.x, (syMax - syMin) / _designSize.y);
            _rt.localScale = new Vector3(scale, scale, 1f);
            var pivotPos = new Vector2(
                Mathf.Lerp(sxMin, sxMax, _origPivot.x),
                Mathf.Lerp(syMin, syMax, _origPivot.y));
            var anchorRef = new Vector2(
                Mathf.Lerp(pr.xMin, pr.xMax, _origAnchor.x),
                Mathf.Lerp(pr.yMin, pr.yMax, _origAnchor.y));
            _rt.anchoredPosition = pivotPos - anchorRef;

            // 设计台矩形（画布坐标）交给 backdrop 画框——留边/避让区填色
            var size = _designSize * scale;
            var center = pivotPos - new Vector2(_origPivot.x - 0.5f, _origPivot.y - 0.5f) * size;
            UIBootstrap.FrameBackdrop(parent, Rect.MinMaxRect(
                center.x - size.x / 2f, center.y - size.y / 2f,
                center.x + size.x / 2f, center.y + size.y / 2f));
        }
    }
}
