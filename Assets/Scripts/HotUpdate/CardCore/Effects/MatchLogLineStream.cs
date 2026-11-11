using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>最终入史行（2026-11-11 联机战报行流 C 案）：服务端逐视角渲染产物，
    /// 本地 UI（BattleLogController 委托消费）与线上行流下发共用同一载体。</summary>
    public struct MatchLogLine
    {
        public string Text;    // 行文本（MatchLogRenderer 唯一映射口径）
        public string Class;   // log-line--*（本地 UI 直用）
        public byte Kind;      // Class 的线上编码（MatchLogLineKind——行流 1B）
        public byte Flags;     // MatchLogLineFlags（时序旗标——客户端面板开合/终局收口）
        public uint EventId;   // 源事件 id（去重/对拍锚；事件流内严格递增）
    }

    /// <summary>行语义类编码（log-line--* 的 1B 线上形态；与 Class 字符串双向单源）。</summary>
    public enum MatchLogLineKind : byte
    {
        Default = 0, // 默认行（抽牌数量/支付明细等无 class 行）
        Turn = 1,    // log-line--turn   回合骨架
        Phase = 2,   // log-line--phase  阶段/账目
        Combat = 3,  // log-line--combat 战斗/死亡
        Effect = 4,  // log-line--effect 效果/指示物
        Card = 5,    // log-line--card   卡的打出/移动
        System = 6,  // log-line--system 连锁叙事/终局
    }

    /// <summary>行时序旗标（服务端判定单源，客户端只消费——日志面板自动开合/终局收口；
    /// 判据与 BattleLogController.AutoToggleForChain 同款收敛于此）。</summary>
    [Flags]
    public enum MatchLogLineFlags : byte
    {
        None = 0,
        FirstDraw = 1,   // 回合自然抽（挂起后放行——对拍锚，客户端无消费）
        TurnStart = 2,   // 回合骨架行
        ChainStart = 4,  // 真连锁开始逆处理（RealCount≥2）——客户端自动展开日志面板
        ChainEnd = 8,    // 连锁结束（真连锁收尾）——客户端自动收起（只收自动开的）
        GameOver = 16,   // ★ 终局行——客户端据此收口对局 UI
    }

    /// <summary>
    /// 战报行流（2026-11-11 C 案 P1）：**时序整理单源**——建局静默（首回合头行前不入史）、
    /// 回合自然抽挂起（挂至〔准备阶段〕行落定后放行）、阶段行放行三规则自
    /// BattleLogController.ProcessEntry 平移（行为逐字等价），服务端（NetRoom 三视角）
    /// 与本地 UI 共用同一实现，杜绝行序二次分叉。
    /// 消费模式：Append(entry, sink) 产出 0..n 行追加进 sink（调用方自备、自清），呈现/下发归调用方。
    /// 线程：引擎发布线程同步调用（MatchLogService.Appended 回调内），无锁（沿用 NetEventProjector 纪律）。
    /// </summary>
    public sealed class MatchLogLineStream
    {
        private readonly Func<Player, bool> _isSelf; // null=中立实名口径（观战/导出——R1 遮蔽不生效）
        private readonly bool _redactEffectSummary;  // R2：效果摘要只出短名（联机座位 true / 本地·观战 false）
        private readonly bool _redactHidden;         // R1：隐藏区入手遮蔽（E3 修订：联机座位 true；本地木桩/观战/导出 false）
        private bool _armed;                          // 建局静默（首个回合头行前不入史）
        private readonly List<MatchLogLine> _standby = new List<MatchLogLine>(2); // 挂起的回合自然抽
        private MatchLogLineFlags _pendingFlags;      // 空行事件旗标前递兜底（现役旗标判据均必搭产出行，防御位）

        /// <param name="isSelf">观侧判定（座位归属）；null=中立实名（观战/导出）。</param>
        /// <param name="redactEffectSummary">R2：效果摘要只出短名、不出 Description（联机座位=true）。</param>
        /// <param name="redactHidden">R1：对手隐藏区入手遮「未知卡」（E3：联机座位=true——本地木桩
        /// isSelf 非 null 但对手是 AI，卡名照出=本地逐字不变铁律；观战 isSelf=null 全信息）。</param>
        public MatchLogLineStream(Func<Player, bool> isSelf, bool redactEffectSummary, bool redactHidden)
        {
            _isSelf = isSelf;
            _redactEffectSummary = redactEffectSummary;
            _redactHidden = redactHidden;
        }

        /// <summary>喂一条日志条目，产出 0..n 行追加到 sink（含挂起放行/旗标前递）。</summary>
        public void Append(MatchLogEntry entry, List<MatchLogLine> sink)
        {
            var e = entry.Event;
            if (e is TurnStartEvent)
            {
                _armed = true;
                Flush(sink); // 兜底放行上回合挂起残留（正常在阶段行已放空）
            }
            if (!_armed) return; // 建局事件（起手抽牌/护甲注入等）不入史——首个回合头行前静默
            var line = MatchLogRenderer.Render(e, _isSelf, _redactEffectSummary, _redactHidden);
            var flags = FlagsOf(e);
            if (string.IsNullOrEmpty(line.Text))
            {
                // 空行：不出行。放行判定在行落定之后——空阶段行（Standby）不触发放行，与旧实现等价。
                if (flags != 0) _pendingFlags |= flags; // 旗标前递兜底（防丢——正常判据必搭产出行）
                return;
            }
            if (e is CardDrawEvent d && d.FirstDrawOfTurn)
            {
                _standby.Add(new MatchLogLine { Text = line.Text, Class = line.Class, Kind = KindOf(line.Class),
                    Flags = (byte)(flags | MatchLogLineFlags.FirstDraw), EventId = entry.EventId }); // 回合自然抽：挂至〔准备阶段〕行后
                return;
            }
            Emit(sink, line.Text, line.Class, flags | _pendingFlags, entry.EventId);
            if (e is PhaseStartEvent) Flush(sink); // 〔准备阶段〕行落定后放行挂起的自然抽
        }

        /// <summary>放行挂起的回合自然抽（〔准备阶段〕行落定后调；兜底点=下回合头行前/对局结束）。</summary>
        public void Flush(List<MatchLogLine> sink)
        {
            for (int i = 0; i < _standby.Count; i++)
            {
                var line = _standby[i];
                Emit(sink, line.Text, line.Class, (MatchLogLineFlags)line.Flags, line.EventId);
            }
            _standby.Clear();
        }

        /// <summary>跨局复位（武装/挂起/前递旗标清空）。</summary>
        public void Reset()
        {
            _armed = false;
            _standby.Clear();
            _pendingFlags = MatchLogLineFlags.None;
        }

        private void Emit(List<MatchLogLine> sink, string text, string cls, MatchLogLineFlags flags, uint eventId)
        {
            sink.Add(new MatchLogLine { Text = text, Class = cls, Kind = KindOf(cls), Flags = (byte)flags, EventId = eventId });
            _pendingFlags = MatchLogLineFlags.None;
        }

        /// <summary>时序旗标判定（ChainStart/ChainEnd 与本地连锁自动开合同判据；单件窗口=普通处理不旗）。</summary>
        private static MatchLogLineFlags FlagsOf(IGameEvent e)
        {
            var f = MatchLogLineFlags.None;
            if (e is TurnStartEvent) f |= MatchLogLineFlags.TurnStart;
            if (e is StackResolutionStartEvent s && s.TopItem != null && !s.TopItem.IsPhaseWindow && s.RealCount >= 2)
                f |= MatchLogLineFlags.ChainStart; // 真连锁（结算开始时栈上真实对象≥2）
            if (e is StackEmptyEvent se && se.HadRealItems && se.WasChain)
                f |= MatchLogLineFlags.ChainEnd;   // 真连锁收尾（单件/纯阶段窗排干不旗）
            if (e is GameOverEvent) f |= MatchLogLineFlags.GameOver;
            return f;
        }

        // ==== Class ↔ Kind 单源（线上 1B 编码与 UI 字符串 class 双向；未知值回落 Default/""）====

        public static byte KindOf(string cls) => cls switch
        {
            "log-line--turn" => (byte)MatchLogLineKind.Turn,
            "log-line--phase" => (byte)MatchLogLineKind.Phase,
            "log-line--combat" => (byte)MatchLogLineKind.Combat,
            "log-line--effect" => (byte)MatchLogLineKind.Effect,
            "log-line--card" => (byte)MatchLogLineKind.Card,
            "log-line--system" => (byte)MatchLogLineKind.System,
            _ => (byte)MatchLogLineKind.Default,
        };

        public static string ClassOf(byte kind) => kind switch
        {
            (byte)MatchLogLineKind.Turn => "log-line--turn",
            (byte)MatchLogLineKind.Phase => "log-line--phase",
            (byte)MatchLogLineKind.Combat => "log-line--combat",
            (byte)MatchLogLineKind.Effect => "log-line--effect",
            (byte)MatchLogLineKind.Card => "log-line--card",
            (byte)MatchLogLineKind.System => "log-line--system",
            _ => "",
        };
    }
}
