using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 异形屏安全区适配（2026-10-21）：把宿主 RectTransform 的锚点对齐 Screen.safeArea——
    /// 刘海/挖孔/圆角遮挡区不摆放 UI。挂载父必须是全屏层（Canvas 根），anchor 按
    /// safeArea/屏幕尺寸归一化；safeArea=全屏时（编辑器/PC 包/非异形屏）退化为普通
    /// stretch，零影响。真机 safeArea 晚到（启动数帧后才正确）由 Update 轮询自愈。
    /// UIScreen.Mount 统一挂到屏根——3D 相机照常全屏渲染，避让只作用于 UI 层。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _applied;

        private void OnEnable()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        private void Update() => Apply(); // safeArea 变化不触发任何回调，轮询（Rect 相等即零成本早退）

        private void OnRectTransformDimensionsChange() => Apply(); // 分辨率/父层尺寸变化

        private void Apply()
        {
            var sa = UnityEngine.Screen.safeArea;
            if (sa.width <= 0f || sa.height <= 0f) return; // 启动竞态：留待轮询
            if (sa == _applied) return;
            _applied = sa;

            // Overlay Canvas 根 rect=屏幕像素空间，safeArea 同口径——直接归一化为父锚点
            var w = (float)UnityEngine.Screen.width;
            var h = (float)UnityEngine.Screen.height;
            _rt.anchorMin = new Vector2(sa.xMin / w, sa.yMin / h);
            _rt.anchorMax = new Vector2(sa.xMax / w, sa.yMax / h);
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
