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
    /// 二期 A（2026-11-11 LiveData 统一）职责收拢为单源演出路由：
    /// ① 帧累积进 <see cref="Film"/>（与播放器解耦——播放器只管 Live 表现，无头录制可传 null 播放器）；
    /// ② 自持一条 MatchLogLineStream（与 UI 战报同规则同源）订阅 MatchLogService.Appended 逐行录制，
    ///    **建帧期即归帧填 Lines/FrameIndex**（实时随帧入列——接替旧日志 Hold/水位放行）；步帧之后的
    ///    轮末尾行经播放器收尾放出；轮外行（回合头/自然抽/阶段）即时入列；
    /// ③ <see cref="Present"/>=演出单源路由：轮内 op 挂帧（随帧经 IBattleStageSink 数据指令执行——
    ///    闭包延迟队列已退役），轮外 op 即时执行（DeathHold 例外：引擎时点恒即时持票）；
    /// ④ 攻击步从编舞器拉 CombatShot；⑤ OnGameStart 补帧 #0（View 开局局面）+ 建剧本头；
    /// ⑥ OnGameOver 封卷（End 帧+行归帧+BattleFilmStore 落盘）。
        /// 轮外事件（回合切换/自然抽/疲劳——两次结算轮之间）不产帧：行即时入列；演出 op 自
        /// 2026-11-13 阶段1（方案 A）起即时演 + 照录挂下一结算步/End 帧（OutOfBand 标记）——
        /// 回放轮外表现与 live 对齐（此前靠视图对账覆盖=粒子丢失，v1 已知限制就此收口）。
    /// </summary>
    public sealed class LocalFilmSource : IDisposable
    {
        private readonly BattlePlayhead _playhead;          // 可空：null=纯录制（无头验证/无表现）
        private readonly Func<GameCore> _core;
        private readonly Func<BattleProjector> _projector;
        private readonly Func<CombatChoreographer> _choreo; // 攻击步拉 CombatShot（可空）
        private readonly Action<BattleFxOp> _immediateFx;   // 轮外演出即时执行（屏侧注入——LiveData 执行器；null=无表现）
        private readonly Action<BattleLogLine> _immediateLine; // 轮外/无剧本行即时入列（屏侧注入——战报控制器）

        /// <summary>录制中剧本（GameStart 建、GameOver 封卷；null=未开局——镜像局续局/教学换场安全）。</summary>
        public BattleFilm Film { get; private set; }

        private readonly MatchLogLineStream _lineStream;    // 行录制（本地口径：双 redact=false，与 UI 战报逐字同源）
        private readonly List<BattleLogLine> _lines = new(); // 全量录制行（终局一次性归帧——挂起自然抽乱序，逐条即时归帧会错桶）
        private int _attachedCount;                          // 已归帧/已即时入列的行数（_lines 前缀——步帧建帧期消费）
        private readonly List<BattleFxOp> _pendingPre = new(); // 轮内捕获的 PreOps（死亡预持）——随下一帧冲刷
        private readonly List<BattleFxOp> _pendingOps = new();  // 轮内捕获的 Ops——随下一帧冲刷
        private bool _inRound;                               // 结算轮内（StackResolutionStart↔StackEmpty）
        private bool _pendingFinalize;                       // GameOver 已至、栈解退中段——待轮末封卷（End 恒为末帧）
        private bool _finalized;                             // 本剧本已封卷（教学换场多次 GameOver 防双存）

        private readonly string _matchId;                  // 外部指定剧本 id（2026-11-12 AI 复盘录制；null=自造 local-…）

        public LocalFilmSource(BattlePlayhead playhead, Func<GameCore> core, Func<BattleProjector> projector,
            Func<CombatChoreographer> choreo = null,
            Action<BattleFxOp> immediateFx = null, Action<BattleLogLine> immediateLine = null,
            string matchId = null)
        {
            _playhead = playhead;
            _core = core;
            _projector = projector;
            _choreo = choreo;
            _immediateFx = immediateFx;
            _immediateLine = immediateLine;
            _matchId = matchId;
            // 本地录制口径与 BattleLogController 一致（isSelf=人类恒 P1、双 redact=false——C 案 E3：R1 只属联机座位流）
            // richValues=true（2026-11-13 P4·D7）：本地实时面板与回放剧本行文本=染色后（TMP 富文本；
            // 回放 TideJson 原样存——旧剧本无标签照常显示）
            _lineStream = new MatchLogLineStream(p => p == _core()?.Player1, redactEffectSummary: false, redactHidden: false,
                richValues: true);
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
            if (entry == null) return;
            var sink = new List<MatchLogLine>(2);
            _lineStream.Append(entry, sink);
            for (int i = 0; i < sink.Count; i++)
                _lines.Add(new BattleLogLine
                {
                    EventId = sink[i].EventId,
                    FrameIndex = -1, // 归帧在建帧期/终局完成（方案 §2.1 映射规则）
                    Text = sink[i].Text,
                    FullText = sink[i].Text, // 本地无遮蔽——FullText=Text（联机 S4 起终局全量行回填）
                    Kind = sink[i].Kind,
                    Flags = sink[i].Flags,
                    Class = sink[i].Class,
                });
            if (Film == null || !_inRound)
                EmitImmediateLines(); // 轮外行（回合头/自然抽/阶段）与无剧本兜底：即时入列（不产帧）
        }

        /// <summary>未归帧行即时入列（轮外路径——引擎泵在播放期被冻结，与帧行无交错）。</summary>
        private void EmitImmediateLines()
        {
            for (int i = _attachedCount; i < _lines.Count; i++)
                _immediateLine?.Invoke(_lines[i]);
            _attachedCount = _lines.Count;
        }

        // ==== 投影器生命周期钩子（BattleScreen 传给投影器；产帧+录剧本单源） ====

        /// <summary>开局（GameStartEvent=起手抽后）：建剧本+帧 #0（View 开局局面——回放先把棋盘/角色/手牌
        /// 落到开局态再播第一步；Live 不入播放队列——纯局面帧不建回放会话）。</summary>
        public void OnGameStart(GameStartEvent e)
        {
            _finalized = false;
            _pendingFinalize = false;
            _inRound = false;
            _lines.Clear();
            _attachedCount = 0;
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
                    MatchId = _matchId ?? ("local-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")
                              + (seedCp.Seeded ? "-" + seedCp.Seed : "")),
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

        /// <summary>逆结算回合开始：转播放器标记轮始（会话延迟到首帧落地才建——空轮零开销）；标记轮内（op 录制窗口）。</summary>
        public void OnResolutionStart(StackResolutionStartEvent e)
        {
            _inRound = true;
            _playhead?.BeginRound();
        }

        /// <summary>逆结算单步完成：捕获快照建 Step 帧入队 + 未归帧行随帧填 Lines/FrameIndex + 挂轮内捕获的
        /// PreOps/Ops/CombatShot 入剧本。卡牌步快照补链头（2026-10-19 实测修复）：快照在对象
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
            // 轮内捕获的演出指令随帧入剧本（先冲刷再入队——帧归属唯一）
            FlushPendingInto(frame);
            // 未归帧行随帧填 Lines（二期 A：实时随帧入列；FrameIndex=剧本帧号——回放时间轴跳转目标）
            AttachLinesTo(frame);
            if (frame.IsAttack && _choreo != null)
                frame.Combat = _choreo().CaptureShot(SeatOf); // 攻击步战斗演出（编舞器捕获态——出栈时伤害已齐）
            Film?.Frames.Add(frame);
            _playhead?.Enqueue(frame);
        }

        /// <summary>逆结算轮末（栈排干）：未归帧行转播放器作轮末尾行（有会话=随收尾放出/空轮=即时入列）；
        /// 轮内窗口关闭。终局已至（GameOver 落在栈解退中段）则此刻封卷——End 帧恒为末帧（余步帧先录完）。</summary>
        public void OnRoundStackEmpty(StackEmptyEvent e)
        {
            _inRound = false;
            if (_pendingFinalize)
            {
                _pendingFinalize = false;
                FinalizeFilm();
            }
            if (_playhead != null)
            {
                if (_attachedCount < _lines.Count)
                {
                    var tail = _lines.GetRange(_attachedCount, _lines.Count - _attachedCount);
                    _attachedCount = _lines.Count;
                    _playhead.NotifyRoundEmpty(tail);
                }
                else
                    _playhead.NotifyRoundEmpty();
            }
            else
                EmitImmediateLines(); // 无头录制：尾行只入剧本，无 UI 可喂
        }

        // ==== 演出路由（二期 A 单源：投影器各钩子只构造 op 进此口——闭包路径退役） ====

        /// <summary>演出指令路由：轮内挂帧（随帧经舞台数据指令执行）；轮外即时执行 + 照录挂
        /// 下一结算步/End 帧（2026-11-13 阶段1·方案 A——粒子无法从局面重建，轮外 op 入剧本
        /// 回放才对齐 live；OutOfBand=true 标记已即时演，LiveData 帧回放跳过防双播，回放剧本
        /// 正常消费）。例外：Paid（扣费粒子）发生在入栈期（轮外）照录挂下一结算步并即时演
        ///（沿旧路径不标 OutOfBand——到位置时刻登记依赖即时执行）；DeathHold（死亡预持）
        /// 恒即时执行（引擎时点持票——防对账抢先销毁尸体），轮内另录进 PreOps（回放先于
        /// View 应用），轮外不录（PreOps 时序对不上后续帧）。pre=true 进 PreOps。</summary>
        public void Present(BattleFxOp op, bool pre = false)
        {
            if (op == null) return;
            if (Film == null || _finalized)
            {
                _immediateFx?.Invoke(op); // 未开局/已封卷——照演（表现不因录制态缺席）
                return;
            }
            if (op.Kind == BattleFxKind.DeathHold)
            {
                _immediateFx?.Invoke(op); // 引擎时点同步持票（与轮内外无关）
                if (_inRound) Capture(op, pre: true);
                return;
            }
            if (_inRound)
            {
                Capture(op, pre);
                return;
            }
            if (op.Kind == BattleFxKind.Paid)
            {
                Capture(op);             // 轮外付费挂下一结算步（出牌→付费→该卡结算步）
                _immediateFx?.Invoke(op);
                return;
            }
            // 轮外演出（阶段播报/自然抽飞入/疲劳/轮外一切 op）：即时演 + 照录挂下一帧
            //（易错点④：绝不挂当前帧——上一轮演出压到下一轮=错桶；终无后续步则 End 帧兜底冲刷）
            op.OutOfBand = true;
            Capture(op, pre);
            _immediateFx?.Invoke(op);
        }

        /// <summary>演出指令捕获（挂帧录制口——Present 的录制路径；验证器/无头直接调）。</summary>
        public void Capture(BattleFxOp op, bool pre = false)
        {
            if (op == null || Film == null || _finalized) return;
            (pre ? _pendingPre : _pendingOps).Add(op);
        }

        // ---- 入场/付费路由（2026-11-11 P0-2 收口自屏侧迁来；规则与屏侧逐字同源） ----

        private readonly Dictionary<int, Dictionary<int, float>> _lastPaid = new(); // seat→最近实付（入场粒子颜色口径）

        /// <summary>付费事件（投影器透传）：记最近实付账本 + 打出法术的扣费粒子走 Present
        ///（「打出:」且栈上有施放中法术）。</summary>
        public void OnPaid(ElementPoolPayEvent e)
        {
            if (e?.Player == null) return;
            var paid = new Dictionary<int, float>();
            if (e.PaidCost != null)
                foreach (var kv in e.PaidCost)
                    if (kv.Value > 0.0001f) paid[kv.Key] = kv.Value;
            int seat = SeatOf(e.Player);
            _lastPaid[seat] = paid;
            if (Film == null || _finalized) return;
            if (paid.Count > 0 && e.SourceNote != null && e.SourceNote.StartsWith("打出:", StringComparison.Ordinal))
            {
                var spell = (_core()?.StackEngine?.GetPendingCastCards(e.Player) ?? new List<Card>())
                    .FirstOrDefault(c => c != null && c.IsSpellCard()); // S3b A 切源：读栈不读区
                if (spell != null)
                    Present(new BattleFxOp { Kind = BattleFxKind.Paid, Seat = seat, Paid = paid });
            }
        }

        /// <summary>入场事件（投影器透传，2026-11-13 补位扩源）：CastPlayed 走原口径（2s 消融+地格汇聚，
        /// 颜色=最近实付，无账单回落印刷费）；其余来源（效果召唤/墓地复活/衍生物/控制权变更）此前
        /// **零表现**——本批一律记一条「轻入场」op（Flag=true，颜色=印刷费主色，无费=灰），实时与回放
        /// 都在单位脚下播 Aura 汇聚件。CastPlayed 的零费卡仍不演（对消融演出无意义）。</summary>
        public void OnArrival(CardPutToBattlefieldEvent e)
        {
            if (e?.Card == null || Film == null || _finalized) return;
            bool castPlayed = e.Source == EnterSource.CastPlayed;
            int seat = SeatOf(e.Controller);
            Dictionary<int, float> paid;
            if (castPlayed && _lastPaid.TryGetValue(seat, out var last) && last.Count > 0)
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
            if (!castPlayed || paid.Count > 0)
                Present(new BattleFxOp
                {
                    Kind = BattleFxKind.Arrival,
                    Target = FxRef.Of(e.Card, SeatOf),
                    Seat = seat,
                    Paid = paid,
                    Flag = !castPlayed, // true=轻入场（Aura）/ false=CastPlayed 消融
                });
        }

        private void FlushPendingInto(BattleFrame frame)
        {
            if (_pendingPre.Count > 0) { frame.PreOps.AddRange(_pendingPre); _pendingPre.Clear(); }
            if (_pendingOps.Count > 0) { frame.Ops.AddRange(_pendingOps); _pendingOps.Clear(); }
        }

        /// <summary>未归帧行随帧填 Lines/FrameIndex（二期 A：实时随帧入列——行序=产出序）。</summary>
        private void AttachLinesTo(BattleFrame frame)
        {
            if (_attachedCount >= _lines.Count) return;
            int frameIndex = Film?.Frames.Count ?? 0; // 本帧在剧本中的序号（帧 #0=开局 View 已在列）
            for (int i = _attachedCount; i < _lines.Count; i++)
            {
                _lines[i].FrameIndex = frameIndex;
                frame.Lines.Add(_lines[i]);
            }
            _attachedCount = _lines.Count;
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

        /// <summary>外部封卷口（2026-11-12 AI 复盘录制）：回合上限/异常路径没有 GameOver 落盘——
        /// 调用方在跑完对局后兜底封卷（End 帧+行归帧+落盘）。幂等：已封卷空转。</summary>
        public new void Finalize() => FinalizeFilm();

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
            FlushPendingInto(endFrame); // 轮外尾 op（终局前疲劳/阶段播报等）兜底冲刷——无后续步帧时不再丢
            Film.Frames.Add(endFrame);
            AssignLinesToFrames(); // 未归帧行（终局行/兜底）一次性归帧——挂起自然抽的乱序 EventId 在终局视角可整体二分
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

        /// <summary>行→帧映射（方案 §2.1，二期 A 收窄为未归帧行）：FrameIndex=第一个
        /// LastEventId >= line.EventId 的帧；帧的 LastEventId 单调不减（EventId 事件流内严格递增）
        /// ——逐行线性推进双指针。建帧期已随帧的行（FrameIndex>=0）不再动。</summary>
        private void AssignLinesToFrames()
        {
            var frames = Film.Frames;
            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (line.FrameIndex >= 0) continue; // 建帧期已归（实时随帧/轮外即时入列——序已定）
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
