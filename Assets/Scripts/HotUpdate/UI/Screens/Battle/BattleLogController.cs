using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;
using CardCore.Network;
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
        // 时序整理单源（2026-11-11 C 案 P1）：建局静默/自然抽挂起/阶段行放行下沉 MatchLogLineStream，
        // 本地与服务端行流共用同一实现——本地 ProcessEntry 只委托消费，行序不再二次分叉。
        private readonly MatchLogLineStream _stream;
        private readonly List<MatchLogLine> _sink = new List<MatchLogLine>(8); // Append 产出暂存（每条清空）
        private RectTransform _logListRect;        // log-list 高度动画目标（0↔700）
        private float _logListTarget;
        /// <summary>面板开↔收起实际切换通知（2026-11-11 btn-abstract 常驻改版）：手动 Toggle、
        /// 本地/联机连锁自动开合、联机开局复位同一单源发出——btn-abstract 卡组摘要行随日志
        /// 关闭/重开自动隐藏等消费方挂此事件。</summary>
        public event Action PanelToggled;
        private bool _autoOpened;                   // 连锁自动展开标记（2026-11-03：连锁结束只收自己开的——手动展开不关）
        private int _activeLineCount;               // 在役行数（池化口径：trim 按活动行计，池中 inactive 不算——childCount 含 inactive 不可用）
        private readonly Stack<TMP_Text> _linePool = new(); // 回收行池（2026-10-24 对象池定案：超限行回收复用不再 Destroy）
        private TMP_Text _seedLabel;                // 种子行（lbl-seed——BattleUI 烘焙，可缺）
        private UnityEngine.UI.Button _seedCopyBtn; // 复制钮（btn-copy-seed——点击=种子数字进剪贴板）
        private string _seedDigits = "";            // 当前展示种子（复制源）

        private const float LogListOpenHeight = 700f; // 展开定高（2026-10-16 晚改版：固定 0↔700，超出在面板内滚动）
        private const float LogListSpeed = 3500f; // px/s（700px≈0.2s 展开）

        public BattleLogController(Func<Player, bool> isSelf)
        {
            _isSelf = isSelf;
            // 本地口径：双 redact=false（效果摘要照出 Description、对手 AI 的检索入手照出卡名——
            // 与导出/改造前逐字一致；E3 修订：R1 遮蔽只属联机服务座行流）
            _stream = new MatchLogLineStream(isSelf, redactEffectSummary: false, redactHidden: false);
        }

        /// <summary>控件绑定（Build 期一次）：滚动区+面板矩形；ClickSwallow 截断面板内点击冒泡
        ///（滚日志不误触 btn-log 收起）；初始收起（烘焙 700→开局不闪空面板）。</summary>
        public void Bind(UiKit.Scroll logList, RectTransform logListRect)
        {
            _logList = logList;
            _logListRect = logListRect;
            // 在役行数点账（池化配套）：跨 Mount 存留的旧活动行入账——trim/池化都以「活动行」为口径
            _activeLineCount = 0;
            if (_logList?.Content != null)
            {
                for (int i = 0; i < _logList.Content.childCount; i++)
                {
                    var ch = _logList.Content.GetChild(i);
                    if (ch.gameObject.activeSelf && ch.name == "lbl") _activeLineCount++;
                }
            }
            if (_logListRect != null)
            {
                _logListRect.gameObject.AddComponent<ClickSwallow>();
                _logListRect.sizeDelta = new Vector2(_logListRect.sizeDelta.x, 0f);
            }
        }

        /// <summary>种子行绑定（Build 期；节点由 BattleUI 烘焙在 log-list 面板内——lbl-seed 文本位 +
        /// btn-copy-seed 复制钮，未烘焙 LogWarning 一次提示补位；排版随预制体）。</summary>
        public void BindSeed(RectTransform scope)
        {
            if (scope == null) return;
            _seedLabel = UiKit.FindDeep(scope, "lbl-seed")?.GetComponent<TMP_Text>();
            _seedCopyBtn = UiKit.FindDeep(scope, "btn-copy-seed")?.GetComponent<UnityEngine.UI.Button>();
            if (_seedLabel == null || _seedCopyBtn == null)
            {
                Debug.LogWarning("[BattleLog] 日志种子行未烘焙（lbl-seed / btn-copy-seed）——随机种子展示待预制体补位");
                return;
            }
            _seedCopyBtn.onClick.RemoveAllListeners();
            _seedCopyBtn.onClick.AddListener(() =>
            {
                if (_seedDigits.Length > 0) GUIUtility.systemCopyBuffer = _seedDigits;
            });
        }

        /// <summary>种子展示刷新（2026-11-11 日志复现链）：本地=GameRng.Capture（StartNewGame 已钉）；
        /// 联机=RoomState.Seed（服务器开局定死回显）。值进复制源+文案。</summary>
        public void ShowSeed(int seed)
        {
            _seedDigits = seed.ToString();
            if (_seedLabel != null) _seedLabel.text = "随机种子：" + _seedDigits;
        }

        /// <summary>本地战报事件桥（2026-10-18 B1 日志中间层收敛）：改订 MatchLogService.Appended
        /// 全事件流，行文本经唯一映射 MatchLogRenderer.Render(e, isSelf) 渲染——原 14 事件白名单与
        /// BattleEventText 三面口径退役。时序口径不变：①接线在 StartNewGame 之前（首回合头行不漏）；
        /// ②首个回合行前静默与③回合自然抽挂起/阶段行放行（2026-11-11 C 案下沉行流 _stream 承担
        /// ——建局事件不入史；自然抽挂到〔准备阶段〕行落定后放行）。</summary>
        public void Wire()
        {
            MatchLogService.Appended += OnAppended;
            _unsubscribers.Add(() => MatchLogService.Appended -= OnAppended);
        }

        private void OnAppended(MatchLogEntry entry)
        {
            AutoToggleForChain(entry.Event); // 连锁自动开合（置于 Hold 检查前——结算回放缓冲期事件也照常驱动面板）
            if (_holding) { _heldEntries.Add(entry); return; } // 结算回放期缓冲（2026-10-19 步进放行）
            ProcessEntry(entry);
        }

        /// <summary>连锁自动开合（2026-11-03 用户定案）：真连锁开始逆处理→展开、连锁结束→收起。
        /// 收口只针对本类自动开的展开——玩家手动展开在连锁期保持开（连锁结束不关）；手动 Toggle
        /// 即接管（解除自动标记）。纯阶段窗折返（主/结束阶段窗排干）不触发。
        /// 真连锁=结算开始时栈上真实对象 ≥2（2026-11-04 与战报行同判据 RealCount——单件窗口
        /// 是普通处理「开始处理」，不再自动弹面板）。</summary>
        private void AutoToggleForChain(IGameEvent e)
        {
            if (e is StackResolutionStartEvent s && s.TopItem != null && !s.TopItem.IsPhaseWindow
                && s.RealCount >= 2) // 单件窗口=普通处理，不开面板
            {
                if (_logListTarget <= 0f)
                {
                    SetLogTarget(LogListOpenHeight);
                    _autoOpened = true;
                }
            }
            else if (e is StackEmptyEvent se && se.HadRealItems)
            {
                if (_autoOpened)
                {
                    SetLogTarget(0f);
                    _autoOpened = false;
                }
            }
        }

        /// <summary>单条入史（即时路径与回放放行共用）：时序整理（武装/挂起/放行）已下沉
        /// MatchLogLineStream（2026-11-11 C 案 P1）——本方法只委托行流并逐行入列，行为与
        /// 改造前逐字等价（三规则的唯一实现挪进共享类，服务端行流产出同一行序）。</summary>
        private void ProcessEntry(MatchLogEntry entry)
        {
            _stream.Append(entry, _sink);
            for (int i = 0; i < _sink.Count; i++)
                Append(_sink[i].Text, _sink[i].Class);
            _sink.Clear();
        }

        /// <summary>退订（OnExit 调用——Deactivate 内 FlushSubscriptions 与 OnExit 同步相邻无帧隙，语义等价）。</summary>
        public void Dispose()
        {
            foreach (var unsub in _unsubscribers) unsub();
            _unsubscribers.Clear();
        }

        // ==== 联机局入口（2026-11-11 C 案 v15 行流：服务端已完成渲染与时序整理，客户端纯入列） ====
        // 联机局无本地引擎→MatchLogService.Appended 无事件，Wire 不调用；行源=NetLogBatch
        //（服务端 MatchLogLineStream 产出——建局静默/自然抽挂起/阶段行放行/连锁旗标全在服务端单源）。

        /// <summary>联机局开局重置（StartNetFan 调，须先于行流到达）：时序态（本类行流与联机共用
        /// Reset）/连锁自动开合标记/回放缓冲复位——本类跨 Mount 存留，防上一局状态串进新局。</summary>
        public void ResetForNetMatch()
        {
            _stream.Reset();      // 行流时序态（2026-11-11 C 案）
            _autoOpened = false;
            SetLogTarget(0f); // 单源写入：上一局日志若开着，此处收起也走 PanelToggled（摘要行随之隐藏）
            _holding = false;
            _heldEntries.Clear();
        }

        /// <summary>联机局连锁自动开合（旗标由服务端判定下发）：真连锁结算开始→展开、连锁结束→
        /// 收起（只收自动开的）。</summary>
        public void NetChainAutoToggle(bool chainStart, bool chainEnd)
        {
            if (chainStart)
            {
                if (_logListTarget <= 0f)
                {
                    SetLogTarget(LogListOpenHeight);
                    _autoOpened = true;
                }
            }
            else if (chainEnd && _autoOpened)
            {
                SetLogTarget(0f);
                _autoOpened = false;
            }
        }

        /// <summary>联机局行入口（v15 行流）：Kind→Class 走行流单源表，纯入列——零时序整理、
        /// 零渲染（服务端已定稿）。</summary>
        public void AppendServerLine(NetLogLine line)
        {
            if (line == null || string.IsNullOrEmpty(line.Text)) return;
            Append(line.Text, MatchLogLineStream.ClassOf(line.Kind));
        }

        // ==== 结算回放逐步放行（2026-10-19 逆结算步进定案） ====
        // Hold 后新条目缓冲不入史；回放导演每放一步调 ReleaseUpTo(全局条数边界)——边界计数源=
        // MatchLogService.Entries.Count（步快照捕获点同帧读），事件同步发布无竞态，缓冲序=落史序。
        // ReleaseAll=轮末收尾/看门狗/退屏全放。行流时序态（武装/挂起）在放出时照常运行。

        private bool _holding;
        private int _holdBase; // Hold 时 MatchLogService.Entries.Count（全局序换算基准）
        private readonly List<MatchLogEntry> _heldEntries = new List<MatchLogEntry>();

        /// <summary>开始缓冲（回放导演激活时调；幂等）。</summary>
        public void Hold()
        {
            if (_holding) return;
            _holding = true;
            _holdBase = MatchLogService.Entries.Count;
        }

        /// <summary>缓冲在途（Hold↔Release 窗口）——日志可见性判别口（2026-10-20 定案「UI 真相源=
        /// 日志」）：false=新行此刻入史可见（消费方可即时对账，如角色HP读数渲染宣言帧中间态）；
        /// true=行缓冲中、随回放步放行（终值归步进快照/命中节拍）。比 SuppressAutoFlush 早一拍
        /// （Hold 在 StackResolutionStart、抑制在首个 StackResolutionEnd）——正好拦住步内事件。</summary>
        public bool IsHeld => _holding;

        /// <summary>放行至全局条数边界（步快照捕获点的日志水位）：按序放出缓冲条目。</summary>
        public void ReleaseUpTo(int globalCount)
        {
            if (!_holding) return;
            int take = Mathf.Clamp(globalCount - _holdBase, 0, _heldEntries.Count);
            for (int i = 0; i < take; i++) ProcessEntry(_heldEntries[i]);
            if (take > 0) _heldEntries.RemoveRange(0, take);
        }

        /// <summary>全量放行并结束缓冲（收尾/看门狗/退屏调；幂等于未 Hold）。</summary>
        public void ReleaseAll()
        {
            if (!_holding) return;
            foreach (var e in _heldEntries) ProcessEntry(e);
            _heldEntries.Clear();
            _holding = false;
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
            var label = TakeLine();
            if (label == null) return;
            _activeLineCount++;
            label.text = text;
            label.color = color;
            label.fontStyle = (FontStyles)style; // 枚举位同值（Normal/Bold）

            while (_activeLineCount > MaxActiveLines) RecycleOldestLine();
            _logList.ScrollToBottom(); // 新增即滚底（2026-10-16 晚改版：行高由 VLG+CSF 自适应，见 prefab log-list content）
        }

        private const int MaxActiveLines = 300; // 在役行上限（同旧口径 300：超出回收最旧行）

        /// <summary>取一行（2026-10-24 对象池）：优先出池复用（回队尾+重激活），池空才克隆模板；
        /// 池中 fake-null（Content 重建随毁）逐个丢弃。复用行文本/色/字形由调用方全覆盖写。</summary>
        private TMP_Text TakeLine()
        {
            while (_linePool.Count > 0)
            {
                var pooled = _linePool.Pop();
                if (pooled == null) continue; // Unity fake-null：随 Content 销毁的僵尸丢弃
                var go = pooled.gameObject;
                go.name = "lbl"; // 回役复名（池化期名 lbl#pool——防模板扫描误中）
                go.transform.SetAsLastSibling(); // 池行保留旧层级位——VLG 按层级排版，复用必须回队尾
                go.SetActive(true);
                return pooled;
            }
            var lineGo = UnityEngine.Object.Instantiate(_logLineTemplate.gameObject, _logList.Content, false);
            lineGo.name = "lbl";
            lineGo.SetActive(true);
            return lineGo.GetComponent<TMP_Text>();
        }

        /// <summary>回收最旧活动行入池（改名 lbl#pool+熄活；模板/池行/非战报常驻行跳过；
        /// 无行可回收=计数漂移就地清零防死循环）。2026-11-11 btn-abstract 常驻 content（active
        /// 且层级在首）——回收只认名 lbl 的战报行，否则按钮会被改名熄活吃掉。</summary>
        private void RecycleOldestLine()
        {
            var content = _logList.Content;
            for (int i = 0; i < content.childCount; i++)
            {
                var ch = content.GetChild(i);
                if (!ch.gameObject.activeSelf || ch.name != "lbl") continue; // 模板/池行（inactive）+ 常驻非战报行（btn-abstract）
                ch.gameObject.name = "lbl#pool";
                ch.gameObject.SetActive(false);
                _activeLineCount--;
                _linePool.Push(ch.GetComponent<TMP_Text>());
                return;
            }
            _activeLineCount = 0; // 实物与计数漂移（理论不可达）——校正退出
        }

        /// <summary>btn-log 切换面板高度目标（手动接管：解除连锁自动开合标记）。</summary>
        public void Toggle()
        {
            _autoOpened = false; // 手动操作即接管——连锁结束不再替用户收面板
            SetLogTarget(_logListTarget > 0f ? 0f : LogListOpenHeight);
            Debug.Log($"[BattleLog] btn-log 切换：目标高度 {_logListTarget:0}（展开={_logListTarget > 0f}）");
        }

        /// <summary>目标高度单源写入（2026-11-11）：开↔收起实际切换时发 PanelToggled——
        /// 手动/本地连锁/联机连锁/开局复位全路径同口，btn-abstract 摘要行随日志关闭/重开自动隐藏。</summary>
        private void SetLogTarget(float target)
        {
            if (Mathf.Approximately(_logListTarget, target)) return;
            _logListTarget = target;
            PanelToggled?.Invoke();
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
