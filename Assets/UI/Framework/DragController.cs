using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 跨容器拖拽控制器（2026-09-14 合成器重做）——运行时 UI Toolkit 无内建跨列表拖拽
    /// （ListView.reorderable 只支持同列表整行换位；库→编辑栏是异构槽位），从零实现：
    ///
    /// - 注册式落区（DropZone）：VisualElement + 资格判定 CanDrop + 落下回调 OnDrop；
    ///   悬停时命中 worldBound 且资格通过 → 加 drop-slot--target 高亮。
    /// - 按下累计位移超阈值（默认 8px）才算拖拽开始——与点击（ClickEvent）和 ScrollView 滚动共存。
    /// - 拖拽开始后对根容器 CapturePointer——移出源元素仍持续接收 Move/Up。
    /// - ghost = 绝对定位半透明 Label（pointer-events:none），跟随指针。
    /// 用法：源行 RegisterDragSource(row)；row.userData = (payload, title)（AttachPayload）；
    ///       槽位 RegisterZone(el, canDrop, onDrop)。
    /// </summary>
    public sealed class DragController
    {
        /// <summary>一个可落区：元素 + 资格 + 落下行为。</summary>
        public sealed class DropZone
        {
            public VisualElement El;
            public Func<object, bool> CanDrop;
            public Action<object, VisualElement> OnDrop;
        }

        private readonly List<DropZone> _zones = new List<DropZone>();
        private readonly float _threshold;

        private VisualElement _root;      // 拖拽期间的根容器（事件路由 + ghost 宿主）
        private VisualElement _pressSource;
        private VisualElement _ghost;
        private object _payload;
        private bool _dragging;
        private Vector2 _pressPos;

        public DragController(float threshold = 8f)
        {
            _threshold = threshold;
        }

        public bool IsDragging => _dragging;

        // ======================================== 注册 ========================================

        /// <summary>注册落区（同一元素重复注册幂等跳过）。</summary>
        public void RegisterZone(VisualElement el, Func<object, bool> canDrop, Action<object, VisualElement> onDrop)
        {
            if (el == null) return;
            if (_zones.Any(z => ReferenceEquals(z.El, el))) return;
            _zones.Add(new DropZone { El = el, CanDrop = canDrop, OnDrop = onDrop });
        }

        /// <summary>注销某元素的全部落区（界面重建时防悬挂）。</summary>
        public void UnregisterZone(VisualElement el)
        {
            _zones.RemoveAll(z => ReferenceEquals(z.El, el));
        }

        /// <summary>清空全部落区（整屏重建前调用）。</summary>
        public void ClearZones() => _zones.Clear();

        /// <summary>把拖拽负载挂到源元素上（行重建时重新挂；RegisterDragSource 消费）。</summary>
        public static void AttachPayload(VisualElement source, object payload, string title)
        {
            source.userData = (payload, title);
        }

        /// <summary>
        /// 把一个元素注册为拖拽源：按下→累计位移超阈值→开始拖拽（ghost+捕获+落区高亮）；
        /// 松手在合格落区 → OnDrop；否则取消。未超阈值的按下不拦截（点击/滚动照常）。
        /// 负载与 ghost 标题读 userData（AttachPayload 挂的 (payload, title) 元组）。
        /// </summary>
        public void RegisterDragSource(VisualElement source)
        {
            source.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || _dragging) return;
                _pressSource = source;
                _pressPos = e.position;
                _root = RootOf(source);
                if (_root == null) return;
                // 按下期先观察移动（不捕获——点击语义不被破坏）；超阈值才升级为拖拽
                _root.RegisterCallback<PointerMoveEvent>(OnPressedMove, TrickleDown.TrickleDown);
                _root.RegisterCallback<PointerUpEvent>(OnPressedUp, TrickleDown.TrickleDown);
            });
        }

        // ======================================== 按下期（阈值判定） ========================================

        private void OnPressedMove(PointerMoveEvent e)
        {
            if (_pressSource == null) return;
            if (!_dragging && Vector2.Distance(e.position, _pressPos) > _threshold)
            {
                _root.UnregisterCallback<PointerMoveEvent>(OnPressedMove, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerUpEvent>(OnPressedUp, TrickleDown.TrickleDown);
                BeginDrag(e);
            }
        }

        private void OnPressedUp(PointerUpEvent e)
        {
            // 未超阈值的松手 = 点击，交给 ClickEvent 处理
            DetachPressed();
        }

        private void DetachPressed()
        {
            if (_root != null)
            {
                _root.UnregisterCallback<PointerMoveEvent>(OnPressedMove, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerUpEvent>(OnPressedUp, TrickleDown.TrickleDown);
            }
            _pressSource = null;
        }

        // ======================================== 拖拽期 ========================================

        private void BeginDrag(PointerMoveEvent e)
        {
            if (_pressSource?.userData is not (object payload, string title) || payload == null)
            {
                DetachPressed();
                return;
            }
            _payload = payload;

            _dragging = true;
            _root.CapturePointer(e.pointerId);
            _root.RegisterCallback<PointerMoveEvent>(OnDragMove, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerUpEvent>(OnDragUp, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerLeaveEvent>(OnDragCancel, TrickleDown.TrickleDown);

            _ghost = new Label(title) { style = { position = Position.Absolute } };
            _ghost.AddToClassList("drag-ghost");
            _root.Add(_ghost);
            MoveGhost(e);
            _pressSource = null;
        }

        private void OnDragMove(PointerMoveEvent e)
        {
            if (!_dragging) return;
            MoveGhost(e);
            foreach (var z in _zones)
            {
                if (z.El == null) continue;
                bool hit = z.El.worldBound.Contains(e.position) && z.CanDrop(_payload);
                z.El.EnableInClassList("drop-slot--target", hit);
            }
        }

        private void OnDragUp(PointerUpEvent e)
        {
            if (!_dragging) { Release(e.pointerId); return; }
            var hit = _zones.FirstOrDefault(z => z.El != null
                && z.El.worldBound.Contains(e.position) && z.CanDrop(_payload));
            Release(e.pointerId);
            hit?.OnDrop(_payload, hit.El);
        }

        private void OnDragCancel(PointerLeaveEvent e)
        {
            if (!_dragging) return;
            Release(e.pointerId);
        }

        private void Release(int pointerId)
        {
            _dragging = false;
            if (_root != null)
            {
                if (_root.HasPointerCapture(pointerId)) _root.ReleasePointer(pointerId);
                _root.UnregisterCallback<PointerMoveEvent>(OnDragMove, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerUpEvent>(OnDragUp, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerLeaveEvent>(OnDragCancel, TrickleDown.TrickleDown);
            }
            _ghost?.RemoveFromHierarchy();
            _ghost = null;
            _payload = null;
            foreach (var z in _zones) z.El?.EnableInClassList("drop-slot--target", false);
        }

        private void MoveGhost(PointerMoveEvent e)
        {
            if (_ghost == null || _root == null) return;
            var rb = _root.worldBound;
            _ghost.style.left = e.position.x - rb.xMin;
            _ghost.style.top = e.position.y - rb.yMin;
        }

        /// <summary>元素所在树的根（沿父链走到顶——UIManager 每屏重装根容器，ghost 与捕获挂这里）。</summary>
        private static VisualElement RootOf(VisualElement el)
        {
            var cur = el;
            while (cur?.parent != null) cur = cur.parent;
            return cur;
        }
    }
}
