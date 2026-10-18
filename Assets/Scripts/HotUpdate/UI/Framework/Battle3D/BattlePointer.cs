using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SynergyUI
{
    /// <summary>
    /// 战场指针统一源（2026-10-18 安卓滑动输入定案；同日晚修订=取消箭头中央偏置）：箭头选择类
    /// 交互（攻击目标/放地出牌选格/守卫窗）与棋盘点击的鼠标/触摸归一采样。PC 口径零变化
    /// （位置=鼠标实时、确认=左键按下、取消=右键/ESC）。触摸口径：长按 0.3s（或滑出容差）进入
    /// 滑动——滑动中箭头终点=手指原始点位，不偏置（手指盖住箭头时，合法性经箭头身体变绿仍可
    /// 观察，不再特地把箭头露出；选位悬停另有脚下瞄准贴片随箭头起落），裁决/落点恒=手指原始
    /// 点位；抬起=确定（合法=确认；非法时快速点按才取消——滑动是探索性输入，红色落点只收滑动
    /// 不收窗口；未及长按的抬起=快速点按，同 PC 点击口径）。
    /// 起手压 Canvas UI 的触摸整次不裁决棋盘（点击归 UI）；多点触控锁定起手手指。
    /// Sample() 每帧首个消费者调用（同帧幂等）；纯输入采样，无协程无场景对象。
    /// </summary>
    public static class BattlePointer
    {
        private const float LongPressSeconds = 0.30f; // 长按进入滑动判定时长

        private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private static PointerEventData _uiProbe; // 复用（滑动期每帧 UI 射线免 GC）
        private static EventSystem _probeOwner;

        private static int _sampledFrame = -1;
        private static int _fingerId = -1;   // 锁定的起手手指（-1=无触摸在身）
        private static float _pressTime;
        private static Vector2 _pressPos;
        private static bool _pressOnUI;        // 起手压 Canvas UI：整次触摸不裁决棋盘
        private static bool _engaged;          // 滑动已开始（长按达成或滑出容差）
        private static Vector2 _raw;           // 手指实时点（=裁决/落点/箭头终点）

        /// <summary>触摸手势在身（起手手指未抬起）。</summary>
        public static bool TouchTracked => _fingerId >= 0;

        /// <summary>滑动进行中——箭头跟随期（触摸无悬停：未滑动时箭头应隐藏）。</summary>
        public static bool SlideActive => _fingerId >= 0 && _engaged;

        /// <summary>本帧有效点 UI 命中（PC=IsPointerOverGameObject 原口径；触摸=对落点做 UI 射线）。</summary>
        public static bool OverUI { get; private set; }

        /// <summary>本帧确认触发：PC=左键按下；触摸=不压 UI 起手、抬在非 UI 上的抬起（快速点按或滑动收尾）。</summary>
        public static bool ConfirmPressed { get; private set; }

        /// <summary>确认是否来自滑动收尾（false=快速点按）——触摸滑动落非法格只收滑动不取消窗口的依据。</summary>
        public static bool ConfirmWasSlide { get; private set; }

        /// <summary>确认触发落点（=手指原始点位/鼠标位）。</summary>
        public static Vector2 ConfirmPoint { get; private set; }

        /// <summary>裁决点（射线/点击落点）：触摸=手指原始点位；PC=鼠标。</summary>
        public static Vector2 AimPoint => _fingerId >= 0 ? _raw : (Vector2)Input.mousePosition;

        /// <summary>每帧采样（消费者循环入口调用；同帧幂等）。</summary>
        public static void Sample()
        {
            if (_sampledFrame == Time.frameCount) return;
            _sampledFrame = Time.frameCount;
            ConfirmPressed = false;
            ConfirmWasSlide = false;

            int idx = _fingerId >= 0 ? FindTouchIndex(_fingerId) : -1;
            if (idx >= 0)
            {
                var t = Input.GetTouch(idx);
                _raw = t.position;
                bool ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                if (!_engaged && !ended
                        && (Time.unscaledTime - _pressTime >= LongPressSeconds
                            || (_raw - _pressPos).sqrMagnitude > SlopPixelsSq()))
                {
                    _engaged = true;
                    Debug.Log($"[BattlePointer] 滑动开始（长按 {(Time.unscaledTime - _pressTime):F2}s）——箭头随手指，抬起确定");
                }

                if (ended)
                {
                    // 抬起=确定（Canceled=系统打断不裁决）；落点=手指原始点位，快速点按与滑动收尾同口
                    if (t.phase == TouchPhase.Ended && !_pressOnUI && !OverUIAt(_raw))
                    {
                        ConfirmPressed = true;
                        ConfirmWasSlide = _engaged;
                        ConfirmPoint = _raw;
                        OverUI = false;
                    }
                    else OverUI = true;
                    _fingerId = -1;
                    _engaged = false;
                    return;
                }
                OverUI = OverUIAt(_raw); // 滑动期悬停让路判定（与 PC 同口径消费）
                return;
            }
            if (_fingerId >= 0) _fingerId = -1; // 锁定指消失（系统吞帧）：静默复位

            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    _fingerId = t.fingerId;
                    _pressTime = Time.unscaledTime;
                    _pressPos = _raw = t.position;
                    _pressOnUI = OverUIAt(t.position);
                    _engaged = false;
                    OverUI = _pressOnUI;
                }
                return;
            }

            // 纯鼠标（原口径）
            OverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            ConfirmPressed = Input.GetMouseButtonDown(0);
            ConfirmPoint = Input.mousePosition;
        }

        /// <summary>滑动起手容差（平方）：≈0.25cm（DPI 不可得时兜底 24px）。</summary>
        private static float SlopPixelsSq()
        {
            float slop = Screen.dpi > 10f ? Screen.dpi * 0.098f : 24f;
            return slop * slop;
        }

        private static int FindTouchIndex(int fingerId)
        {
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).fingerId == fingerId) return i;
            return -1;
        }

        /// <summary>点位 UI 命中（触摸相位的 IsPointerOverGameObject 在抬起帧不可靠，走点位射线）。</summary>
        private static bool OverUIAt(Vector2 screenPoint)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (_uiProbe == null || _probeOwner != es)
            {
                _uiProbe = new PointerEventData(es);
                _probeOwner = es;
            }
            _uiProbe.position = screenPoint;
            _uiHits.Clear();
            es.RaycastAll(_uiProbe, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
