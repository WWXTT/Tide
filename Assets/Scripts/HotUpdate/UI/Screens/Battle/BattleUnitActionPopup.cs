using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>
    /// 单位可用动作弹窗（2026-10-23 v4 §10.2 定案）：替退役的格前三键（BattleTileButtons/
    /// BattleHex 三键子件已删）——点击**有可行动作**的己方单位/角色格，在单位上方弹出面板，
    /// 只列可用项（攻击/守卫/发动 + 关闭）；可用位图沿用 BattleScreen.ReconcileTileButtons
    /// 现有逻辑（攻击=我方主阶段未横置生物攻&gt;0 / 发动=主动效果或待发奖励 / 守卫=守卫窗口候选）。
    /// 定位=单位世界坐标投影到屏幕、贴上方；靠边自动翻边/钳位（边距 16px、底部让位手牌扇区
    /// 32% 屏高、顶边 8px）。关闭=点空白（BattleScreen 点击路由兜）/执行动作/阶段或回合变化/
    /// 进入选目标/网络局（均由 BattleScreen 驱动调 Close）。
    /// 纯代码 UI（UiKit.Panel/Button），随 BattleScreen 根创建、离屏销毁。
    /// </summary>
    internal sealed class BattleUnitActionPopup
    {
        public enum Kind { Attack, Guard, Activate }

        private RectTransform _panel;      // 惰建（Canvas 直挂——世界锚点投影按 canvas 系换算）
        private GameObject _attack, _guard, _activate;
        private TMP_Text _title;
        private Action<Kind> _handler;

        public bool IsOpen { get; private set; }

        /// <summary>展示/换目标（重复 Show=换锚换内容）。attack/guard/activate=本格可用项；
        /// onAction=点击动作回调（弹窗先自关再回调——执行动作即关闭）。</summary>
        public void Show(RectTransform screenRoot, Vector3 worldAnchor, string title,
                         bool attack, bool guard, bool activate, Action<Kind> onAction)
        {
            Ensure(screenRoot);
            _title.text = title;
            _attack.SetActive(attack);
            _guard.SetActive(guard);
            _activate.SetActive(activate);
            _handler = onAction;
            IsOpen = true;
            _panel.gameObject.SetActive(true);
            Place(worldAnchor);
            Debug.Log($"[UnitActionPopup] 开：{title}（攻击={attack} 守卫={guard} 发动={activate}）");
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _panel.gameObject.SetActive(false);
            Debug.Log("[UnitActionPopup] 关");
        }

        /// <summary>离屏销毁（BattleScreen 退出时调，防跨屏残留）。</summary>
        public void DestroyUI()
        {
            IsOpen = false;
            if (_panel != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_panel.gameObject);
                else UnityEngine.Object.DestroyImmediate(_panel.gameObject);
                _panel = null;
            }
        }

        // ======================================== 内部 ========================================

        private void Ensure(RectTransform screenRoot)
        {
            if (_panel != null) return;
            var canvasRt = screenRoot.GetComponentInParent<Canvas>().transform as RectTransform;
            var overlay = UiKit.Overlay("unit-action-popup", canvasRt);
            _panel = UiKit.Panel("panel", overlay, pad: 8f, spacing: 6f);
            var fitter = _panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.MinSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 148f);

            _title = UiKit.Label("title", _panel, "-", 13, UiStyle.TextSecondary, TextAnchor.MiddleCenter);
            _attack = UiKit.Button("btn-attack", _panel, "攻击", () => Fire(Kind.Attack),
                                    UiStyle.BtnPrimary, UiStyle.White, 126f).gameObject;
            _guard = UiKit.Button("btn-guard", _panel, "守卫", () => Fire(Kind.Guard),
                                  UiStyle.BtnBg, UiStyle.TextBody, 126f).gameObject;
            _activate = UiKit.Button("btn-activate", _panel, "发动", () => Fire(Kind.Activate),
                                     UiStyle.BtnBg, UiStyle.TextBody, 126f).gameObject;
            UiKit.MiniButton("btn-close", _panel, "关闭", Close);
            _panel.gameObject.SetActive(false);
        }

        private void Fire(Kind kind)
        {
            var h = _handler;
            Close(); // 执行动作即关闭（§10.2）
            h?.Invoke(kind);
        }

        /// <summary>单位头顶锚点→屏幕投影→贴上方；越顶边翻到下方；两侧/底部钳位
        ///（边距 16px、底部 32% 屏高让位手牌扇区——弹窗永不压手牌）。</summary>
        private void Place(Vector3 worldAnchor)
        {
            var canvas = _panel.GetComponentInParent<Canvas>();
            var canvasRt = (RectTransform)canvas.transform;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var sp = RectTransformUtility.WorldToScreenPoint(cam, worldAnchor + Vector3.up * 1.35f);
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, cam, out lp);

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_panel); // 钳位前拿到实测尺寸
            var size = _panel.rect.size;
            float halfW = size.x * 0.5f + 16f;
            float x = Mathf.Clamp(lp.x, halfW, canvasRt.rect.width - halfW);
            // 底部手牌让位线：弹窗底沿不得低于 32% 屏高
            float minY = canvasRt.rect.height * 0.32f;
            bool above = lp.y + 14f + size.y <= canvasRt.rect.height - 8f;
            _panel.pivot = above ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
            float y = above ? Mathf.Max(lp.y + 14f, minY) : Mathf.Max(lp.y - 14f, minY + size.y);
            _panel.anchoredPosition = new Vector2(x, y);
        }
    }
}
