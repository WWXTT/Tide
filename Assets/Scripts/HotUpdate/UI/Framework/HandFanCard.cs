using UnityEngine;
using UnityEngine.EventSystems;

namespace SynergyUI
{
    /// <summary>
    /// 手牌扇区卡实例（2026-10-02 手牌扇形原型）：运行时 AddComponent 到 ACard 实例根上
    /// （同 CardOverlayCard 的挂载模式——prefab 本体不带脚本）。承担两件事：
    /// ① IPointerEnter/Exit 悬停上报，转发给所属 HandFanView 重排目标；
    /// ② 布局 tween 状态（当前值/目标值），由 HandFanView.Tick 统一推进。
    /// </summary>
    public sealed class HandFanCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>对账键（引擎卡实例运行时 id）。</summary>
        public uint RuntimeId;

        /// <summary>所属扇区（Create 时回填）。</summary>
        [System.NonSerialized] public HandFanView Fan;

        /// <summary>卡面渲染（Create 时回填）。</summary>
        [System.NonSerialized] public CardOverlayCard Face;

        // ---- tween 状态（anchoredPosition / z 角 / 等比缩放） ----
        public Vector2 Pos;
        public float Rot;
        public float Scale = 1f;
        public Vector2 TargetPos;
        public float TargetRot;
        public float TargetScale = 1f;

        /// <summary>刚取用（池空新建或首次出池）：首帧吸附目标，不从旧位置飞入。</summary>
        public bool Fresh;

        public void OnPointerEnter(PointerEventData eventData) => Fan?.NotifyHover(this, true);
        public void OnPointerExit(PointerEventData eventData) => Fan?.NotifyHover(this, false);

        public void SetTarget(Vector2 pos, float rot, float scale)
        {
            TargetPos = pos;
            TargetRot = rot;
            TargetScale = scale;
        }

        /// <summary>当前值直接吸附目标（新建卡就地落位）。</summary>
        public void SnapToTarget()
        {
            Pos = TargetPos;
            Rot = TargetRot;
            Scale = TargetScale;
            ApplyTransform();
        }

        public void ApplyTransform()
        {
            var rt = (RectTransform)transform;
            rt.anchoredPosition = Pos;
            rt.localEulerAngles = new Vector3(0f, 0f, Rot);
            rt.localScale = new Vector3(Scale, Scale, 1f);
        }
    }
}
