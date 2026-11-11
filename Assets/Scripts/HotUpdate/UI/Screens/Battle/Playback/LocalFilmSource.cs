using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Serialization;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 本地实时帧生产者（2026-11-11 战斗演出统一重构 S1 起·Battle/Playback/LocalFilmSource.cs）：
    /// 投影器钩子（StackResolutionStart/End/StackEmpty）→ 建 BattleFrame。
    /// S2 扩展（2026-11-11 演出数据化+本地录制）：
    /// ① 帧累积进 <see cref="Film"/>（与播放器解耦——播放器只管 Live 表现，无头录制可传 null 播放器）；
    /// ② 自持一条 MatchLogLineStream（与 UI 战报同规则同源）订阅 MatchLogService.Appended 逐行录制，
    ///    终局按「第一个 LastEventId >= line.EventId 的帧」归帧写 Lines/FrameIndex（方案 §2.1）；
    /// ③ <see cref="Capture"/> 接投影器/屏侧演出钩子的 BattleFxOp（双轨录制——闭包照旧执行，op 只入剧本）；
    /// ④ 攻击步从编舞器拉 CombatShot；⑤ OnGameStart 补帧 #0（View 开局局面）+ 建剧本头；
    /// ⑥ OnGameOver 封卷（End 帧+行归帧+BattleFilmStore 落盘）。
    /// 轮外事件（回合切换/自然抽/疲劳——两次结算轮之间）不产帧：其演出 op 丢弃、行按 EventId 归帧
    /// （FrameIndex=-1 的帧前历史行照进时间轴）——v1 已知限制：轮外粒子表现回放由视图对账覆盖。
    /// </summary>
    public sealed class LocalFilmSource : IDisposable
    {
        private readonly BattlePlayhead _playhead;          // 可空：null=纯录制（无头验证/无表现）
        private readonly Func<GameCore> _core;
        private readonly Func<BattleProjector> _projector;
        private readonly Func<CombatChoreographer> _choreo; // 攻击步拉 CombatShot（可空）

        /// <summary>录制中剧本（GameStart 建、GameOver 封卷；null=未开局——镜像局续局/教学换场安全）。</summary>
        public BattleFilm Film { get; private set; }

        private readonly MatchLogLineStream _lineStream;    // 行录制（本地口径：双 redact=false，与 UI 战报逐字同源）
        private readonly List<BattleLogLine> _lines = new(); // 全量录制行（终局一次性归帧——挂起自然抽乱序，逐条即时归帧会错桶）
        private readonly List<BattleFxOp> _pendingPre = new(); // 轮内捕获的 PreOps（死亡预持）——随下一帧冲刷
        private readonly List<BattleFxOp> _pendingOps = new();  // 轮内捕获的 Ops——随下一帧冲刷
        private bool _inRound;                               // 结算轮内（StackResolutionStart↔StackEmpty）
        private bool _pendingFinalize;                       // GameOver 已至、栈解退中段——待轮末封卷（End 恒为末帧）
        private bool _finalized;                             // 本剧本已封卷（教学换场多次 GameOver 防双存）

        public LocalFilmSource(BattlePlayhead playhead, Func<GameCore> core, Func<BattleProjector> projector,
            Func<CombatChoreographer> choreo = null)
        {
            _playhead = playhead;
            _core = core;
            _projector = projector;
            _choreo = choreo;
            // 本地录制口径与 BattleLogController 一致（isSelf=人类恒 P1、双 redact=false——C 案 E3：R1 只属联机座位流）
            _lineStream = new MatchLogLineStream(p => p == _core()?.Player1, redactEffectSummary: false, redactHidden: false);
        }

        /// <summary>行录制接线（EngageLocalBattle 内与 _log.Wire() 同点位——先于 StartNewGame，首回合头行不漏）。</summary>
        public void Wire()
        {
            MatchLogService.Appended += OnLogAppended;
        }

        public void Dispose()
        {
            MatchLogService.Appended -= OnLogAppended;
        }

        private void OnLogAppended(MatchLogEntry entry)
        {
            if (Film == null || entry == null) return;
            var sink = new List<MatchLogLine>(2);
            _lineStream.Append(entry, sink);
            for (int i = 0; i < sink.Count; i++)
                _lines.Add(new BattleLogLine
                {
                    EventId = sink[i].EventId,
                    FrameIndex = -1, // 归帧在终局一次性完成（方案 §2.1 映射规则）
                    Text = sink[i].Text,
                    FullText = sink[i].Text, // 本地无遮蔽——FullText=Text（联机 S4 起终局全量行回填）
                    Kind = sink[i].Kind,
                    Flags = sink[i].Flags,
                    Class = sink[i].Class,
                });
        }

        // ==== 投影器生命周期钩子（BattleScreen 传给投影器；S1 起为播放器产帧，S2 起兼录剧本） ====

        /// <summary>开局（GameStartEvent=起手抽后）：建剧本+帧 #0（View 开局局面——回放先把棋盘/角色/手牌
        /// 落到开局态再播第一步；Live 不入播放队列——纯局面帧不建回放会话）。</summary>
        public void OnGameStart(GameStartEvent e)
        {
            _finalized = false;
            _pendingFinalize = false;
            _inRound = false;
            _lines.Clear();
            _pendingPre.Clear();
            _pendingOps.Clear();
            _lastPaid.Clear();
            _lineStream.Reset();
            var core = _core();
            var seedCp = GameRng.Capture();
            Film = new BattleFilm
            {
                Header = new BattleFilmHeader
                {
                    Version = 1,
                    MatchId = "local-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")
                              + (seedCp.Seeded ? "-" + seedCp.Seed : ""),
                    Mode = (int)BattleEntry.Mode,
                    ViewerSeat = 0,
                    SelfName = core?.Player1?.Name ?? "我方",
                    OppName = core?.Player2?.Name ?? "对手",
                    Seed = seedCp.Seeded ? seedCp.Seed : 0,
                    CreatedAtUnix = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds,
                },
            };
            var opening = _projector()?.CaptureSnapshot();
            if (opening != null)
                Film.Frames.Add(new BattleFrame { Kind = BattleFrameKind.View, View = opening, Label = "开局" });
        }

        /// <summary>逆结算回合开始：转播放器按住日志（会话延迟到首帧落地才建——空轮零开销）；标记轮内（op 录制窗口）。</summary>
        public void OnResolutionStart(StackResolutionStartEvent e)
        {
            _inRound = true;
            _playhead?.BeginRound();
        }

        /// <summary>逆结算单步完成：捕获快照+日志水位建 Step 帧入队（S1 原样）+ 挂轮内捕获的
        /// PreOps/Ops/CombatShot 入剧本（S2）。卡牌步快照补链头（2026-10-19 实测修复）：快照在对象
        /// 出栈后捕获，ChainCards 已不含本卡——无连锁的单发施放整段同步完成（无响应窗 await、置脏
        /// 对账被抑制丢弃），链上从未建过克隆=左侧恒无展示。补回后链舞台在回放期建槽（结算期只增不删口径不变）。</summary>
        public void OnStepResolved(StackResolutionEndEvent e)
        {
            var item = e?.ResolvedItem;
            var card = item != null && item.IsCardCast ? item.Source as Card : null;
            var snapshot = _projector()?.CaptureSnapshot();
            if (card != null && snapshot != null)
            {
                var core = _core();
                snapshot.ChainCards.Insert(0, BattleView.FromRuntime(
                    SerializableRuntimeCardState.FromCard(card), card,
                    core != null && item.Controller == core.Player2));
            }
            var entries = MatchLogService.Entries;
            var frame = new BattleFrame
            {
                Kind = BattleFrameKind.Step,
                View = snapshot,
                LogBoundary = entries.Count,
                LastEventId = entries.Count > 0 ? entries[entries.Count - 1].EventId : 0u, // 行→帧映射锚
                CardRid = card != null ? card.RuntimeId : 0u,
                Label = card != null ? MatchLogRenderer.Name(card) // 卡名单源=EffectText（观侧口径与战报一致）
                    : item != null && item.IsAttackDeclaration ? "攻击宣言"
                    : item != null && item.IsGuardDeclaration ? "守卫宣言"
                    : item != null && item.IsSBA ? "状态动作"
                    : item != null && item.IsPhaseWindow
                        ? (string.IsNullOrEmpty(item.PhaseWindowLabel) ? "阶段窗口" : item.PhaseWindowLabel + "窗口")
                        : "效果",
                IsAttack = item != null && item.IsAttackDeclaration,
                Remaining = e != null ? e.StackSize : 0,
            };
            // S2：轮内捕获的演出指令随帧入剧本（先冲刷再入队——帧归属唯一）
            FlushPendingInto(frame);
            if (frame.IsAttack && _choreo != null)
                frame.Combat = _choreo().CaptureShot(SeatOf); // 攻击步战斗演出（编舞器捕获态——出栈时伤害已齐）
            Film?.Frames.Add(frame);
            _playhead?.Enqueue(frame);
        }

        /// <summary>逆结算轮末（栈排干）：转播放器（空轮即时还原日志/有会话标记可收尾）；轮内窗口关闭。
        /// 终局已至（GameOver 落在栈解退中段）则此刻封卷——End 帧恒为末帧（余步帧先录完）。</summary>
        public void OnRoundStackEmpty(StackEmptyEvent e)
        {
            _inRound = false;
            if (_pendingFinalize)
            {
                _pendingFinalize = false;
                FinalizeFilm();
            }
            _playhead?.NotifyRoundEmpty();
        }

        // ==== 演出指令录制（投影器/屏侧演出钩子捕获时调——闭包照旧执行，op 只进剧本） ====

        /// <summary>演出指令捕获（结算轮内挂到下一帧；轮外丢弃——轮外表现回放由视图对账覆盖）。
        /// 例外=Paid（扣费粒子）：付费发生在入栈期（结算轮开始前），轮外照录挂下一结算步
        ///（出牌→付费→该卡结算步——即使被反制也有步帧；时序近似 v1）。
        /// pre=true 进 PreOps（死亡预持——回放在应用 View 之前执行）。</summary>
        public void Capture(BattleFxOp op, bool pre = false)
        {
            if (op == null || Film == null || _finalized) return;
            if (!_inRound && op.Kind != BattleFxKind.Paid) return;
            (pre ? _pendingPre : _pendingOps).Add(op);
        }

        // ---- 入场/付费录制（2026-11-11 P0-2 收口：自屏侧 CaptureArrival/CapturePaid 迁来——
        // 录制职责归帧生产者单源，无头/联机（无 BattleScreen）同样可录；规则与屏侧逐字同源） ----

        private readonly Dictionary<int, Dictionary<int, float>> _lastPaid = new(); // seat→最近实付（入场粒子颜色口径）

        /// <summary>付费事件（投影器透传）：记最近实付账本 + 录 Paid op（扣费粒子——与屏侧同闸：
        /// 「打出:」且栈上有施放中法术）。</summary>
        public void OnPaid(ElementPoolPayEvent e)
        {
            if (Film == null || e?.Player == null || _finalized) return;
            var paid = new Dictionary<int, float>();
            if (e.PaidCost != null)
                foreach (var kv in e.PaidCost)
                    if (kv.Value > 0.0001f) paid[kv.Key] = kv.Value;
            int seat = SeatOf(e.Player);
            _lastPaid[seat] = paid;
            if (paid.Count > 0 && e.SourceNote != null && e.SourceNote.StartsWith("打出:", StringComparison.Ordinal))
            {
                var spell = (_core()?.StackEngine?.GetPendingCastCards(e.Player) ?? new List<Card>())
                    .FirstOrDefault(c => c != null && c.IsSpellCard()); // S3b A 切源：读栈不读区
                if (spell != null)
                    Capture(new BattleFxOp { Kind = BattleFxKind.Paid, Seat = seat, Paid = paid });
            }
        }

        /// <summary>入场事件（投影器透传）：CastPlayed 入场粒子 op（颜色=最近实付，无账单回落印刷费
        /// ——与屏侧 CaptureArrival 同规则；零费不录）。</summary>
        public void OnArrival(CardPutToBattlefieldEvent e)
        {
            if (Film == null || e?.Source != EnterSource.CastPlayed || e.Card == null || _finalized) return;
            int seat = SeatOf(e.Controller);
            Dictionary<int, float> paid;
            if (_lastPaid.TryGetValue(seat, out var last) && last.Count > 0)
            {
                paid = last;
            }
            else
            {
                paid = new Dictionary<int, float>();
                var cost = GameActions.GetCardCost(e.Card); // 印刷费全口径（抉择/指示物层同引擎单源）
                foreach (var m in cost.NonzeroColors())
                    paid[(int)m] = cost[m];
            }
            if (paid.Count > 0)
                Capture(new BattleFxOp
                {
                    Kind = BattleFxKind.Arrival,
                    Target = FxRef.Of(e.Card, SeatOf),
                    Seat = seat,
                    Paid = paid,
                });
        }

        private void FlushPendingInto(BattleFrame frame)
        {
            if (_pendingPre.Count > 0) { frame.PreOps.AddRange(_pendingPre); _pendingPre.Clear(); }
            if (_pendingOps.Count > 0) { frame.Ops.AddRange(_pendingOps); _pendingOps.Clear(); }
        }

        /// <summary>终局（GameOverEvent）：End 帧（终局快照）+ 全量行归帧 + 落盘 Films/&lt;matchId&gt;.film.gz。
        /// GameOver 常落在栈解退中段（致命伤害触发）——此时只挂起封卷（余步帧照录），
        /// 轮末 StackEmpty 再封（End 恒为末帧）；轮外终局（认输等空栈路径）即刻封。
        /// 幂等（教学脚本化击杀的多次 GameOver 只封第一卷；后续 GameStart 换新剧本）。</summary>
        public void OnGameOver(GameOverEvent e)
        {
            if (Film == null || _finalized) return;
            if (_inRound)
            {
                _pendingFinalize = true;
                return;
            }
            FinalizeFilm();
        }

        /// <summary>封卷本体（轮末/轮外终局共用）：End 帧+行归帧+落盘。</summary>
        private void FinalizeFilm()
        {
            if (Film == null || _finalized) return;
            _finalized = true;
            _pendingFinalize = false;
            _inRound = false;
            var entries = MatchLogService.Entries;
            var endFrame = new BattleFrame
            {
                Kind = BattleFrameKind.End,
                View = _projector()?.CaptureSnapshot(), // 终局局面（null 安全——无投影器的无头路径跳过）
                Label = "终局",
                LastEventId = entries.Count > 0 ? entries[entries.Count - 1].EventId : 0u,
            };
            Film.Frames.Add(endFrame);
            AssignLinesToFrames(); // 全量行一次性归帧（挂起自然抽的乱序 EventId 在终局视角可整体二分）
            try
            {
                BattleFilmStore.Save(Film);
                Debug.Log($"[Film] 本地剧本已录制：{Film.Frames.Count} 帧 / {_lines.Count} 行 → {Film.Header.MatchId}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Film] 剧本落盘失败（不影响对局）：{ex.Message}");
            }
        }

        /// <summary>行→帧映射（方案 §2.1）：FrameIndex=第一个 LastEventId >= line.EventId 的帧；
        /// 帧的 LastEventId 单调不减（EventId 事件流内严格递增）——逐行线性推进双指针。
        /// 行按录制序（挂起放行序）挂帧——时间轴顺序=行序，帧桶内多行共存。</summary>
        private void AssignLinesToFrames()
        {
            var frames = Film.Frames;
            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                int f = 0;
                while (f < frames.Count && frames[f].LastEventId < line.EventId) f++;
                if (f >= frames.Count) f = frames.Count - 1; // 超末帧（终局行等）归 End 帧
                line.FrameIndex = f;
                frames[f].Lines.Add(line);
            }
        }

        /// <summary>座位归属（0=我方 1=对手；未知 -1）——FxRef 解析/编舞器 shot 捕获共用。</summary>
        private int SeatOf(Player p)
        {
            var core = _core();
            return p == null || core == null ? -1 : p == core.Player2 ? 1 : 0;
        }
    }
}
