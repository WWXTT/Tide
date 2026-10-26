using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// 战场提示出口（2026-10-18 BattleScreen 功能拆分第一块外迁）：toast 双层横幅 + error-tip
    /// 顶部滑入错误提示。纯 C# 类——Build 期 Bind 控件、Updater 每帧 Tick（error-tip 动画）、
    /// 教学/守卫横幅文案经 SetBase 注入（UpdatePromptBanner 留在 Screen——属教学导演知识，随 Step 6 外迁）。
    /// lbl-toast 退役（2026-10-17）后的屏上出口兜底（2026-10-24）：教学/守卫底层文案常驻 error-tip、
    /// 顶层临时提示覆盖 2 秒到点回落常驻——分流单点=RenderToast；将来补回新横幅（Bind 传回 lbl-toast
    /// 控件）后本兜底自动失效。
    /// </summary>
    internal sealed class BattleToastHub
    {
        private TMP_Text _lblToast;
        private GameObject _toastPanel;            // lbl-toast 面板本体（自带底板 Image——显隐单口：文本空≠隐藏）
        private string _toastBase, _toastOverlay;  // toast 双层（2026-10-16 横幅合并）：底层=教学/守卫横幅，顶层=选择/响应/拒绝临时提示——顶层清空回落底层
        // error-tip（2026-10-17 顶部滑入错误提示）：临时文案（攻击失败原因等 Show 顶层）全量路由至此；
        // 2026-10-24 兼 lbl-toast 退役：教学/守卫底层文案的常驻兜底宿主
        private RectTransform _errorTip;
        private TMP_Text _errorTipLabel;
        private float _errorTipShownAt = -999f;
        private float _errorTipHold = ErrorTipHold; // 当前停留档：<=0=常驻（不计时隐藏·底层兜底）·>0=临时秒数
        private const float ErrorTipSlide = 0.18f; // 顶部滑入时长（秒）
        private const float ErrorTipHold = 2f;     // 临时档停留时长（秒，到点回落常驻/隐藏）

        /// <summary>控件绑定（Build 期一次；error-tip 的 raycast 关闭防截盘面点击在此处理——
        /// 底层文案常驻后不挡盘面是硬边界，Image/TMP 双保险）。</summary>
        public void Bind(TMP_Text lblToast, GameObject toastPanel, RectTransform errorTip)
        {
            _lblToast = lblToast;
            _toastPanel = toastPanel;
            _errorTip = errorTip;
            if (_errorTip == null) return;
            _errorTipLabel = _errorTip.GetComponentInChildren<TMP_Text>(true);
            if (_errorTipLabel != null) _errorTipLabel.raycastTarget = false; // 常驻兜底不可拦截盘面点击
            var img = _errorTip.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
            _errorTip.gameObject.SetActive(false);
        }

        /// <summary>toast 顶层临时提示（选择/响应窗开闭、拒绝原因等）：空文本=清顶层并回落底层横幅
        ///（教学步骤/守卫提示）。面板底板随有效文案起落——空=藏板（2026-10-16 修复：底板常绘，
        /// 旧口径只清文本=空横幅滞留屏）。error-tip 路由统一由 RenderToast 分流（2026-10-24）。</summary>
        public void Show(string message)
        {
            _toastOverlay = string.IsNullOrEmpty(message) ? null : message;
            RenderToast();
        }

        /// <summary>底层横幅文案写入（教学步骤/守卫提示——UpdatePromptBanner 单口）。</summary>
        public void SetBase(string text)
        {
            _toastBase = text;
            RenderToast();
        }

        /// <summary>双层清空（OnExit 首步——僵尸订阅防跨局残留口径）。</summary>
        public void ClearLayers() => _toastBase = _toastOverlay = null;

        /// <summary>面板隐藏（OnExit 尾段——error-tip 一并收起，常驻兜底防跨局残留）。</summary>
        public void Hide()
        {
            if (_toastPanel != null) _toastPanel.SetActive(false);
            HideErrorTip();
        }

        /// <summary>toast 渲染单口（error-tip 兜底分流收口于此——2026-10-24）：顶层优先、底层兜底，
        /// 两者皆空=藏板。横幅宿主在位=按原口径写 lbl-toast，顶层文案仍镜像 error-tip 临时 2 秒；
        /// 宿主缺失（lbl-toast 退役过渡面）=底层文案常驻 error-tip（hold=0）、顶层临时 2 秒到点回落
        /// 常驻、双空收起。将来补回新横幅（Bind 传回控件）后兜底自动失效。</summary>
        private void RenderToast()
        {
            string text = _toastOverlay ?? _toastBase;
            bool show = !string.IsNullOrEmpty(text);
            if (_toastPanel != null && _toastPanel.activeSelf != show)
                _toastPanel.SetActive(show);
            if (_lblToast != null)
            {
                _lblToast.text = show ? text : "";
                if (!string.IsNullOrEmpty(_toastOverlay)) ShowErrorTip(_toastOverlay, ErrorTipHold); // 原 Show 镜像行为保留
            }
            else if (!string.IsNullOrEmpty(_toastOverlay)) ShowErrorTip(_toastOverlay, ErrorTipHold); // 临时覆盖
            else if (!string.IsNullOrEmpty(_toastBase)) ShowErrorTip(_toastBase, 0f);                 // 底层常驻兜底（教学/守卫）
            else HideErrorTip();                                                                       // 双空=收起防残留
        }

        /// <summary>error-tip 动画（每帧）：顶部往下滑入（ease-out）→ 临时档（hold>0）停留至 hold 秒
        /// 回落（过渡面底层有文案=回落常驻档，否则隐藏）；常驻档（hold<=0）滑入完成后即停——不计时。</summary>
        public void Tick()
        {
            if (_errorTip == null || !_errorTip.gameObject.activeSelf) return;
            float t = Time.unscaledTime - _errorTipShownAt;
            if (_errorTipHold > 0f && t >= _errorTipHold)
            {
                // 到点回落：横幅宿主在位=兜底失效直接隐藏（原行为）；过渡面有底层文案=回落常驻档
                if (_lblToast == null && !string.IsNullOrEmpty(_toastBase)) ShowErrorTip(_toastBase, 0f);
                else _errorTip.gameObject.SetActive(false);
                return;
            }
            float k = Mathf.Clamp01(t / ErrorTipSlide);
            k = 1f - (1f - k) * (1f - k); // ease-out
            float y = (1f - k) * (_errorTip.rect.height + 8f); // 起始=面板整体藏在顶边之上
            var p = _errorTip.anchoredPosition;
            if (!Mathf.Approximately(p.y, y)) _errorTip.anchoredPosition = new Vector2(p.x, y);
        }

        /// <summary>顶部滑入错误提示显示口：文字替换后须显示态强排自动布局（与 hover-info 同口径——
        /// 隐藏态刷新无效）；计时重置=新消息覆盖旧动画；hold<=0=常驻档（Tick 不计时，换文案/收起才重动）。</summary>
        private void ShowErrorTip(string message, float hold)
        {
            if (_errorTip == null || _errorTipLabel == null || string.IsNullOrEmpty(message)) return;
            _errorTipHold = hold;
            _errorTipLabel.text = message;
            if (!_errorTip.gameObject.activeSelf) _errorTip.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_errorTip);
            _errorTipShownAt = Time.unscaledTime;
        }

        /// <summary>error-tip 收起（常驻兜底的退出路径之一；Hide=OnExit 防跨局残留）。</summary>
        private void HideErrorTip()
        {
            if (_errorTip != null) _errorTip.gameObject.SetActive(false);
        }

        /// <summary>教学 intro 推进：横幅面板挂 Button（2026-10-16 教学一两段式——点横幅推进；
        /// 幂等——面板已有 Button 不重复挂，transition 关闭防视觉副作用）。</summary>
        public void AttachIntroClick(Action onClick)
        {
            if (_toastPanel == null || _toastPanel.GetComponent<Button>() != null) return;
            var btn = _toastPanel.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick());
        }
    }
}
