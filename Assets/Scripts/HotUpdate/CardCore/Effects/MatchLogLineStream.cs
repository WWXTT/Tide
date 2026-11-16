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
        private readonly bool _richValues;           // D7（2026-11-13 P4）：行内数值 token 染色（本地 UI/服务端座位/观战 true；导出/控制台 false）
        private bool _armed;                          // 建局静默（首个回合头行前不入史）
        private readonly List<MatchLogLine> _standby = new List<MatchLogLine>(2); // 挂起的回合自然抽
        private MatchLogLineFlags _pendingFlags;      // 空行事件旗标前递兜底（现役旗标判据均必搭产出行，防御位）
        private readonly MatchLogDetailFold _fold;    // 效果明细折叠（观侧口径与本流一致）
        private readonly List<MatchLogLine> _foldEmits = new List<MatchLogLine>(4); // 折叠冲账缓冲（复用免逐事件分配）

        /// <param name="isSelf">观侧判定（座位归属）；null=中立实名（观战/导出）。</param>
        /// <param name="redactEffectSummary">R2：效果摘要只出短名、不出 Description（联机座位=true）。</param>
        /// <param name="redactHidden">R1：对手隐藏区入手遮「未知卡」（E3：联机座位=true——本地木桩
        /// isSelf 非 null 但对手是 AI，卡名照出=本地逐字不变铁律；观战 isSelf=null 全信息）。</param>
        /// <param name="richValues">D7：行内数值按行极性染绿/红（本地面板/服务端座位行流/观战=true；
        /// 默认 false=纯文本——导出 markdown 与 Editor 控制台不得含富文本标签）。</param>
        public MatchLogLineStream(Func<Player, bool> isSelf, bool redactEffectSummary, bool redactHidden,
            bool richValues = false)
        {
            _isSelf = isSelf;
            _redactEffectSummary = redactEffectSummary;
            _redactHidden = redactHidden;
            _richValues = richValues;
            _fold = new MatchLogDetailFold(isSelf);
        }

        /// <summary>喂一条日志条目，产出 0..n 行追加到 sink（含挂起放行/旗标前递/明细折叠冲账）。</summary>
        public void Append(MatchLogEntry entry, List<MatchLogLine> sink)
        {
            var e = entry.Event;
            if (e is TurnStartEvent)
            {
                _armed = true;
                Flush(sink); // 兜底放行上回合挂起残留（正常在阶段行已放空）
            }
            if (!_armed) return; // 建局事件（起手抽牌/护甲注入等）不入史——首个回合头行前静默
            var line = MatchLogRenderer.Render(e, _isSelf, _redactEffectSummary, _redactHidden, _richValues);
            var flags = FlagsOf(e);
            // 明细折叠（2026-11-17）：先行冲出聚合行（若本事件是收口边界），再判当前行是否被吸收。
            // 冲账行不携旗标；Emit 会清 _pendingFlags，此处先存后还原——旗标仍归当前边界行。
            var pendingBefore = _pendingFlags;
            bool absorbed = _fold.Feed(e, line, entry.EventId, _foldEmits);
            for (int i = 0; i < _foldEmits.Count; i++)
                Emit(sink, _foldEmits[i].Text, _foldEmits[i].Class, MatchLogLineFlags.None, _foldEmits[i].EventId);
            _foldEmits.Clear();
            _pendingFlags = pendingBefore;
            if (absorbed) return; // 可折叠事件在窗口内被吸收——收口时聚合出行
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

        /// <summary>跨局复位（武装/挂起/前递旗标/明细折叠清空）。</summary>
        public void Reset()
        {
            _armed = false;
            _standby.Clear();
            _pendingFlags = MatchLogLineFlags.None;
            _fold.Reset();
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

    /// <summary>
    /// 效果明细折叠器（2026-11-17 用户定案：单个效果触发的重复影响行折叠成一行）。
    /// 背景：效果执行摘要行（EffectExecutionSummaryEvent「　→ …」）已完整描述效果内容与逐目标
    /// 清单，同一效果内逐原子重复行（「获得 1 个任意临时元素（来自 牺牲）」×12、墓地回收
    /// 「获得 X（来自 墓地）+X 回到手牌」对×15）只刷屏不增信息。
    ///
    /// 规则定义（先定义后改码）：
    /// - 折叠窗口=一次栈结算：StackResolutionStart（非阶段窗）开窗；EffectExecutionSummary /
    ///   StackEmpty / TurnStart / GameOver 收口——收口时先冲聚合行再出边界行。窗口外行为不变。
    /// - 可折叠事件（仅窗口内被吸收不出行）：
    ///   ① ElementPoolGainEvent——同 (Player, GainedType, FromCard) 聚合，Amount 求和：
    ///      「获得 12 个任意临时元素（来自 牺牲）」；
    ///   ② CardEnterHandEvent（非抽牌且来区为公开区——牌库/无之外的区，天然不涉 R1 遮蔽）——
    ///      同 (Player, FromZone) 聚合卡清单（DescribeCards 同名聚合）：
    ///      「获得 沃土×4、小精灵×5（来自 墓地）」；其配对 CardReturnToHandEvent（同卡同组）一并
    ///      吸收——聚合时「回到手牌」行不再单出（获得行已完整表达去向）。
    /// - 单例不折叠：组内仅 1 条时原行原样冲出（含配对回手行）——单效果单目标的战报逐字不变。
    /// - 聚合行文本由 MatchLogRenderer 公共辅助按消费方观侧（isSelf）现拼，与 RenderCore 同式；
    ///   EventId=组内末条事件 id（对拍锚口径）；不携时序旗标。
    ///
    /// 消费方：MatchLogLineStream（本地 UI/联机座位流/观战流/全量流/回放录制同源受益）与
    /// MatchLogService 导出、Editor 控制台播报器——同一状态机三处复用，口径单源。
    /// 线程：与宿主（行流/日志服务）同线程同步调用，无锁。
    /// </summary>
    public sealed class MatchLogDetailFold
    {
        private readonly Func<Player, bool> _isSelf; // 观侧（null=中立实名——导出/控制台）
        private bool _windowOpen;                    // 折叠窗口（栈结算进行中）
        private readonly List<GainGroup> _gains = new List<GainGroup>(2);
        private readonly List<HandGroup> _hands = new List<HandGroup>(2);

        public MatchLogDetailFold(Func<Player, bool> isSelf) { _isSelf = isSelf; }

        /// <summary>喂入当前事件与其已渲染行（消费方自己的观侧参数渲染产物——单例冲出时逐字保真）。
        /// 返回 true=当前行被吸收（不出行）；emits=本次应先行外送的聚合冲账行（通常空）。</summary>
        public bool Feed(IGameEvent e, MatchLogRenderer.Line rendered, uint eventId, List<MatchLogLine> emits)
        {
            switch (e)
            {
                case TurnStartEvent _:
                case GameOverEvent _:
                    Flush(emits); // 回合/终局边界：兜底冲账（正常已在摘要行/栈排干冲空）
                    _windowOpen = false;
                    return false;
                case StackResolutionStartEvent srs:
                    if (srs.TopItem != null && !srs.TopItem.IsPhaseWindow)
                    {
                        Flush(emits); // 防御（上窗残留）；开新窗
                        _windowOpen = true;
                    }
                    return false;
                case StackEmptyEvent _:
                    Flush(emits);
                    _windowOpen = false;
                    return false;
                case EffectExecutionSummaryEvent _:
                    Flush(emits); // 摘要行收口：聚合行先行、摘要在后（明细→摘要旧序保持）
                    return false;
                case ElementPoolGainEvent g when _windowOpen:
                {
                    var grp = _gains.Find(x => x.Player == g.Player && x.Type == g.GainedType && x.From == g.FromCard);
                    if (grp == null)
                    {
                        grp = new GainGroup { Player = g.Player, Type = g.GainedType, From = g.FromCard,
                            First = ToLine(rendered, eventId) };
                        _gains.Add(grp);
                    }
                    grp.Sum += g.Amount;
                    grp.Count++;
                    grp.LastEventId = eventId;
                    return true;
                }
                case CardEnterHandEvent h when _windowOpen && !h.IsDraw
                    && h.FromZone != Zone.Deck && h.FromZone != Zone.None: // 公开来区才折叠（隐藏区走 R1 遮蔽逐行）
                {
                    var grp = _hands.Find(x => x.Player == h.Player && x.FromZone == h.FromZone);
                    if (grp == null)
                    {
                        grp = new HandGroup { Player = h.Player, FromZone = h.FromZone,
                            First = ToLine(rendered, eventId) };
                        _hands.Add(grp);
                    }
                    grp.Cards.Add(h.Card);
                    grp.LastEventId = eventId;
                    return true;
                }
                case CardCore.Attribute.CardReturnToHandEvent r when _windowOpen && HandGroupOf(r.Card, out var owner):
                    owner.HeldReturns.Add(ToLine(rendered, eventId)); // 单例冲出时原样还原（逐字不变）
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>冲出全部聚合行并清组（收口边界/终局兜底共用口）。</summary>
        public void Flush(List<MatchLogLine> emits)
        {
            foreach (var g in _gains)
            {
                if (g.Count == 1)
                {
                    emits.Add(g.First); // 单例：原行原样（EventId 已在 ToLine 定格）
                }
                else
                {
                    emits.Add(new MatchLogLine
                    {
                        Text = $"{MatchLogRenderer.Who(g.Player, _isSelf)} 获得 {g.Sum} 个" +
                               $"{MatchLogRenderer.ManaZh(g.Type)}临时元素（来自 {MatchLogRenderer.Name(g.From)}）",
                        Class = "log-line--effect",
                        Kind = (byte)MatchLogLineKind.Effect,
                        EventId = g.LastEventId,
                    });
                }
            }
            foreach (var h in _hands)
            {
                if (h.Cards.Count == 1)
                {
                    emits.Add(h.First);
                    for (int i = 0; i < h.HeldReturns.Count; i++) emits.Add(h.HeldReturns[i]);
                }
                else
                {
                    emits.Add(new MatchLogLine
                    {
                        Text = $"{MatchLogRenderer.Who(h.Player, _isSelf)} 获得 " +
                               $"{MatchLogRenderer.DescribeCards(h.Cards)}（来自 {MatchLogRenderer.ZoneZh(h.FromZone)}）",
                        Class = "log-line--effect",
                        Kind = (byte)MatchLogLineKind.Effect,
                        EventId = h.LastEventId,
                    });
                    // 聚合时「回到手牌」配对行不再单出——获得行已完整表达去向
                }
            }
            _gains.Clear();
            _hands.Clear();
        }

        /// <summary>跨局复位（MatchLogLineStream.Reset / 导出器终局后共用）。</summary>
        public void Reset()
        {
            _windowOpen = false;
            _gains.Clear();
            _hands.Clear();
        }

        /// <summary>卡→所属入手组（引用相等——配对判定只用同效果内同卡实例）。</summary>
        private bool HandGroupOf(Card card, out HandGroup owner)
        {
            for (int i = 0; i < _hands.Count; i++)
            {
                if (_hands[i].Cards.Contains(card)) { owner = _hands[i]; return true; }
            }
            owner = null;
            return false;
        }

        private static MatchLogLine ToLine(MatchLogRenderer.Line rendered, uint eventId) => new MatchLogLine
        {
            Text = rendered.Text,
            Class = rendered.Class,
            Kind = MatchLogLineStream.KindOf(rendered.Class),
            EventId = eventId,
        };

        private sealed class GainGroup
        {
            public Player Player;
            public ManaType Type;
            public Card From;
            public int Sum;
            public int Count;
            public MatchLogLine First;      // 首条原行（单例冲出保真）
            public uint LastEventId;        // 聚合行对拍锚=组内末条
        }

        private sealed class HandGroup
        {
            public Player Player;
            public Zone FromZone;
            public readonly List<Card> Cards = new List<Card>(8);
            public MatchLogLine First;
            public uint LastEventId;
            public readonly List<MatchLogLine> HeldReturns = new List<MatchLogLine>(8); // 吸收的配对回手行
        }
    }
}
