using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// 本地战报控制器（2026-10-18 BattleScreen 功能拆分第二块外迁——「对局日志=信息展现中间层」
    /// 北极星的第一块地基）：事件订阅白名单 + 时序整理（武装/挂起）+ 行入列渲染 + 面板开合动画。
    /// 纯 C# 类：Build 期 Bind 控件、StartNewGame 之前 Wire 订阅（时序红线——见 Wire 注释）、
    /// Updater 每帧 Tick、OnExit 时 Dispose 退订（EventManager 直订自管退订，不走 UIScreen 基类登记）。
    /// 行文本渲染单源=BattleEventText.ForLocal（本类只做订阅白名单+时序整理+入列）。
    /// </summary>
    internal sealed class BattleLogController
    {
        private readonly Func<Player, bool> _isSelf; // 观侧判（本地局人类恒 P1）
        private readonly List<Action> _unsubscribers = new List<Action>();

        private UiKit.Scroll _logList;
        private RectTransform _logLineTemplate;    // content 直属隐藏模板 lbl（战报行克隆源，样式归 prefab）
        private bool _logArmed;                    // 战报武装（首个回合行前静默：建局事件不入史，2026-10-17）
        private readonly List<(string text, string cls)> _standbyDeferred = new(); // 挂起的回合自然抽牌行（待〔准备阶段〕行落定，2026-10-17）
        private RectTransform _logListRect;        // log-list 高度动画目标（0↔700）
        private float _logListTarget;

        private const float LogListOpenHeight = 700f; // 展开定高（2026-10-16 晚改版：固定 0↔700，超出在面板内滚动）
        private const float LogListSpeed = 3500f; // px/s（700px≈0.2s 展开）

        public BattleLogController(Func<Player, bool> isSelf) => _isSelf = isSelf;

        /// <summary>控件绑定（Build 期一次）：滚动区+面板矩形；ClickSwallow 截断面板内点击冒泡
        ///（滚日志不误触 btn-log 收起）；初始收起（烘焙 700→开局不闪空面板）。</summary>
        public void Bind(UiKit.Scroll logList, RectTransform logListRect)
        {
            _logList = logList;
            _logListRect = logListRect;
            if (_logListRect != null)
            {
                _logListRect.gameObject.AddComponent<ClickSwallow>();
                _logListRect.sizeDelta = new Vector2(_logListRect.sizeDelta.x, 0f);
            }
        }

        /// <summary>本地战报事件桥（2026-10-18 B1 日志中间层收敛）：改订 MatchLogService.Appended
        /// 全事件流，行文本经唯一映射 MatchLogRenderer.Render(e, isSelf) 渲染——原 14 事件白名单与
        /// BattleEventText 三面口径退役。时序口径不变：①接线在 StartNewGame 之前（首回合头行不漏）；
        /// ②首个回合行前静默（_logArmed 挡建局事件——起手抽/护甲注入不入史）；③回合自然抽牌挂起
        /// （_standbyDeferred——引擎构造期订阅先抽牌，直接落行会写到回合/阶段头上，挂到〔准备阶段〕
        /// 行落定后放行）。</summary>
        public void Wire()
        {
            MatchLogService.Appended += OnAppended;
            _unsubscribers.Add(() => MatchLogService.Appended -= OnAppended);
        }

        private void OnAppended(MatchLogEntry entry)
        {
            var e = entry.Event;
            if (e is TurnStartEvent)
            {
                _logArmed = true;
                FlushStandbyDeferred(); // 兜底放行上回合挂起残留（正常在阶段行已放空）
            }
            if (!_logArmed) return; // 建局事件（起手抽牌/护甲注入等）不入史——首个回合行前静默
            var line = MatchLogRenderer.Render(e, _isSelf);
            if (string.IsNullOrEmpty(line.Text)) return;
            if (e is CardDrawEvent d && d.FirstDrawOfTurn)
            {
                _standbyDeferred.Add((line.Text, line.Class)); // 回合自然抽：挂至〔准备阶段〕行后
                return;
            }
            Append(line.Text, line.Class);
            if (e is PhaseStartEvent) FlushStandbyDeferred(); // 〔准备阶段〕行落定后放行挂起的自然抽
        }

        /// <summary>退订（OnExit 调用——Deactivate 内 FlushSubscriptions 与 OnExit 同步相邻无帧隙，语义等价）。</summary>
        public void Dispose()
        {
            foreach (var unsub in _unsubscribers) unsub();
            _unsubscribers.Clear();
        }

        /// <summary>放行挂起的回合自然抽牌行（〔准备阶段〕行落定后调用；兜底点=下回合头行前）。</summary>
        private void FlushStandbyDeferred()
        {
            for (int i = 0; i < _standbyDeferred.Count; i++)
                Append(_standbyDeferred[i].text, _standbyDeferred[i].cls);
            _standbyDeferred.Clear();
        }

        /// <summary>外部行入口（终局呈现等非订阅路径）。</summary>
        public void Append(string text, string cls)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_logList?.Content == null) return;

            // 2026-10-16 定案：战报行不再代码新建——克隆 content 直属隐藏模板 lbl（用户手调的
            // TMP 字体/字号/对齐/折行/CSF 全由 prefab 接管），代码只填文本与分类色。
            if (_logLineTemplate == null)
            {
                for (int i = 0; i < _logList.Content.childCount; i++)
                {
                    var ch = _logList.Content.GetChild(i);
                    if (ch.name == "lbl" && !ch.gameObject.activeSelf)
                    {
                        _logLineTemplate = (RectTransform)ch;
                        break;
                    }
                }
                if (_logLineTemplate == null)
                {
                    Debug.LogWarning("[BattleLog] log-list content 缺隐藏模板 lbl，战报行丢弃");
                    return;
                }
            }

            var (color, style) = cls switch
            {
                "log-line--turn" => (UiStyle.LogTurn, FontStyle.Bold),
                "log-line--phase" => (UiStyle.LogPhase, FontStyle.Normal),
                "log-line--combat" => (UiStyle.LogCombat, FontStyle.Normal),
                "log-line--effect" => (UiStyle.LogEffect, FontStyle.Normal), // 淡绿：效果发动/结算/指示物
                "log-line--card" => (UiStyle.LogCard, FontStyle.Normal),     // 灰：卡的打出/放置
                "log-line--system" => (UiStyle.LogSystem, FontStyle.Normal),
                _ => (UiStyle.LogBase, FontStyle.Normal),
            };
            var lineGo = UnityEngine.Object.Instantiate(_logLineTemplate.gameObject, _logList.Content, false);
            lineGo.name = "lbl";
            lineGo.SetActive(true);
            var label = lineGo.GetComponent<TMP_Text>();
            label.text = text;
            label.color = color;
            label.fontStyle = (FontStyles)style; // 枚举位同值（Normal/Bold）

            while (_logList.Content.childCount > 300)
            {
                var first = _logList.Content.GetChild(0);
                if ((RectTransform)first == _logLineTemplate)
                    first = _logList.Content.childCount > 1 ? _logList.Content.GetChild(1) : null;
                if (first == null) break;
                first.SetParent(null);
                UnityEngine.Object.Destroy(first.gameObject);
            }
            _logList.ScrollToBottom(); // 新增即滚底（2026-10-16 晚改版：行高由 VLG+CSF 自适应，见 prefab log-list content）
        }

        /// <summary>btn-log 切换面板高度目标。</summary>
        public void Toggle()
        {
            _logListTarget = _logListTarget > 0f ? 0f : LogListOpenHeight;
            Debug.Log($"[BattleLog] btn-log 切换：目标高度 {_logListTarget:0}（展开={_logListTarget > 0f}）");
        }

        /// <summary>面板高度动画（Updater 每帧）。</summary>
        public void Tick()
        {
            if (_logListRect == null) return;
            float h = Mathf.MoveTowards(_logListRect.sizeDelta.y, _logListTarget, Time.deltaTime * LogListSpeed);
            if (Mathf.Approximately(h, _logListRect.sizeDelta.y)) return;
            _logListRect.sizeDelta = new Vector2(_logListRect.sizeDelta.x, h);
            if (_logListTarget > 0f && h >= _logListTarget - 0.5f) _logList?.ScrollToBottom();
        }

        /// <summary>吞掉点击（IPointerClickHandler 实现即截断向父级 Button 冒泡——展开的日志面板内
        /// 点击/拖拽不得触发 btn-log 收起）。</summary>
        private sealed class ClickSwallow : MonoBehaviour, IPointerClickHandler
        {
            public void OnPointerClick(PointerEventData e) { }
        }
    }
}
