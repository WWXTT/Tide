using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using CardCore.Network;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 联机帧生产者（二期 C·2026-11-11 方案 §3-C）：FxOnly 事件流 + 行流 + 快照 → BattleFrame 投
    /// <see cref="BattlePlayhead"/>（LiveData——与本地实时同一播放器/同一帧体/同一舞台协议）。
    /// 帧切割：StackResolutionEnd=步帧、StackEmpty=轮末（BeginRound/NotifyRoundEmpty 转播放器）；
    /// 演出 op 由 <see cref="NetFxMapper"/> 从 NetEvent 映射（白名单事件，座位按观看视角重排）；
    /// 行按 EventId 归帧（帧落地时合并 pending），轮外/空闲期行经 <see cref="Tick"/> 即时入列；
    /// 快照=剧本帧 #0 / 空闲期直刷（播放期暂存最新一份待空闲落地——防终态直出抢帧；服务器快照
    /// 只在静默点取样，步内局面本就无快照，与旧直刷口径等价）。
    /// 终局：行流 GameOver 旗标 / 房态 Finished / 断线三者先到即封卷——座位玩家录 BattleFilm 落盘
    ///（观战 viewerSeat=-1 不录制 v1）；MatchLogFull 到达后按 EventId 回填 FullText 重写剧本，
    /// 未到达（断线）保留遮蔽版（已知限制）。matchId=首快照生成一次（EncounterStore.NewMatchId），
    /// 与终局 EncounterRecord 共用（方案 §1.3）。
    /// </summary>
    public sealed class NetFilmSource
    {
        private readonly BattlePlayhead _playhead;      // 可空（防御）
        private readonly Func<MsgGameStateSync, BattleViewData> _buildView; // BattleView.BuildNet
        private readonly Action<BattleViewData> _applyView;                 // 空闲期视图直刷（投影器 ApplySnapshot+刷扇）
        private readonly Action<IReadOnlyList<BattleLogLine>> _emitLines;   // 行入列（连锁开合/终局收口在屏侧回调内）
        private readonly string _selfName, _oppName;    // 剧本头（移交快照）
        private readonly bool _record;                  // 座位玩家=true / 观战=false

        /// <summary>本局剧本（录制座位玩家；观战/未开局=null）。</summary>
        public BattleFilm Film { get; private set; }

        /// <summary>对局 id（首快照生成一次——剧本文件名与 EncounterRecord.matchId 共用）。</summary>
        public string MatchId { get; private set; }

        /// <summary>观看视角引擎座位（首快照定格；-1=观战）。</summary>
        public int ViewerSeat { get; private set; } = int.MinValue;

        private readonly List<BattleFxOp> _pendingPre = new();
        private readonly List<BattleFxOp> _pendingOps = new();
        private CombatShot _stepCombat;                 // 攻击宣言步的战斗演出载荷（随步帧消费）
        private bool _stepAttack;                       // 当前轮内已见攻击宣言
        private bool _inRound;                          // StackResolutionStart↔StackEmpty
        private readonly List<BattleLogLine> _pendingLines = new(); // 待归帧行（帧落地合并/空闲期放出）
        private readonly List<BattleLogLine> _allLines = new();     // 全量行录制（终局未归帧行按 EventId 补归）
        private BattleViewData _deferredView;           // 播放期暂存的最新快照视图（空闲落地）
        private bool _finalized;                        // 剧本已封卷（终局三信号幂等）
        private uint _lastEventId;                      // 事件流尾锚

        public NetFilmSource(BattlePlayhead playhead,
            Func<MsgGameStateSync, BattleViewData> buildView,
            Action<BattleViewData> applyView,
            Action<IReadOnlyList<BattleLogLine>> emitLines,
            string selfName, string oppName, bool record)
        {
            _playhead = playhead;
            _buildView = buildView;
            _applyView = applyView;
            _emitLines = emitLines;
            _selfName = selfName ?? "";
            _oppName = oppName ?? "";
            _record = record;
        }

        // ==== 下行三路入口（BattleScreen.Net 转发） ====

        /// <summary>事件批次（FxOnly 白名单）：帧切割 + 演出 op 映射挂帧。</summary>
        public void OnEventBatch(NetEvent[] events)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                if (e == null) continue;
                _lastEventId = e.EventId;
                switch (e.EventType)
                {
                    case nameof(StackResolutionStartEvent):
                        _inRound = true;
                        _stepAttack = false;
                        _stepCombat = null;
                        _playhead?.BeginRound();
                        break;

                    case nameof(StackResolutionEndEvent):
                        BuildStepFrame(e);
                        break;

                    case nameof(StackEmptyEvent):
                        _inRound = false;
                        _playhead?.NotifyRoundEmpty(); // 尾行留 pending——空闲期 Tick 放出（或下一轮首帧合并）
                        break;

                    case nameof(AttackDeclarationEvent):
                        _stepAttack = true;
                        _stepCombat = new CombatShot
                        {
                            Attacker = MapRef(e, "Attacker"),
                            Target = MapRef(e, "Target"),
                            TargetX = -1, TargetZ = -1, // 无格坐标——目标不在视图时回放器自跳
                        };
                        break;

                    default:
                        MapFx(e);
                        break;
                }
            }
        }

        /// <summary>步帧构造（StackResolutionEnd）：挂轮内 op/攻击演出/可归帧行，投播放器+录剧本。</summary>
        private void BuildStepFrame(NetEvent endEvent)
        {
            var frame = new BattleFrame
            {
                Kind = BattleFrameKind.Step,
                LastEventId = endEvent.EventId,
                Remaining = MapInt(endEvent, "StackSize"),
                IsAttack = _stepAttack,
                Combat = _stepCombat,
                Label = _stepAttack ? "攻击宣言" : "结算",
            };
            _stepAttack = false;
            _stepCombat = null;
            if (_pendingPre.Count > 0) { frame.PreOps.AddRange(_pendingPre); _pendingPre.Clear(); }
            if (_pendingOps.Count > 0) { frame.Ops.AddRange(_pendingOps); _pendingOps.Clear(); }
            // 行归帧：EventId <= 本帧锚的 pending 行随帧（更晚到的行留待下一帧/空闲——行流同拍
            // 晚于事件流到达，首帧行常落在次帧，顺序仍=到达序）
            if (_pendingLines.Count > 0)
            {
                int frameIndex = Film?.Frames.Count ?? 0;
                for (int i = _pendingLines.Count - 1; i >= 0; i--)
                {
                    if (_pendingLines[i].EventId > frame.LastEventId) continue;
                    var line = _pendingLines[i];
                    line.FrameIndex = frameIndex;
                    frame.Lines.Add(line);
                    _pendingLines.RemoveAt(i);
                }
                frame.Lines.Reverse(); // 逆扫保序——还原到达序
            }
            Film?.Frames.Add(frame);
            _playhead?.Enqueue(frame);
        }

        /// <summary>行批次（NetLogBatch）：转 BattleLogLine 挂 pending（帧落地合并/空闲期放出）+ 全量录制。</summary>
        public void OnLogLines(NetLogLine[] lines)
        {
            if (lines == null) return;
            foreach (var l in lines)
            {
                if (l == null) continue;
                var line = new BattleLogLine
                {
                    EventId = l.EventId,
                    FrameIndex = -1,
                    Text = l.Text,
                    FullText = null, // MatchLogFull 到达后按 EventId 回填（无遮蔽版）
                    Kind = l.Kind,
                    Flags = l.Flags,
                    Class = MatchLogLineStream.ClassOf(l.Kind),
                };
                _pendingLines.Add(line);
                _allLines.Add(line);
            }
            Tick(); // 空闲期立即放（播放期 pending 等帧——本调用幂等）
        }

        /// <summary>快照：首帧定格视角+建剧本头+帧 #0；此后播放期暂存、空闲落地（防终态直出抢帧）。</summary>
        public void OnSnapshot(MsgGameStateSync snap)
        {
            if (snap == null || _buildView == null) return;
            if (ViewerSeat == int.MinValue)
            {
                ViewerSeat = snap.ViewerSeat;
                MatchId = CardCore.EncounterStore.NewMatchId();
                if (_record)
                    Film = new BattleFilm
                    {
                        Header = new BattleFilmHeader
                        {
                            Version = 1,
                            MatchId = MatchId,
                            Mode = (int)BattleMode.Network,
                            ViewerSeat = snap.ViewerSeat,
                            SelfName = string.IsNullOrEmpty(_selfName) ? "我方" : _selfName,
                            OppName = string.IsNullOrEmpty(_oppName) ? "对手" : _oppName,
                            Seed = 0, // 种子经 RoomState 展示（快照不带）；剧本头留 0
                            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        },
                    };
            }
            var view = _buildView(snap);
            if (Film != null && Film.Frames.Count == 0 && view != null)
                Film.Frames.Add(new BattleFrame { Kind = BattleFrameKind.View, View = view, Label = "开局", LastEventId = _lastEventId });
            if (_playhead != null && _playhead.Busy) _deferredView = view; // 播放期暂存——空闲落地
            else _applyView?.Invoke(view);
        }

        /// <summary>终局全量行（MatchLogFull，分批到达）：按 EventId 回填 FullText，剧本重写落盘。</summary>
        public void OnLogFull(NetLogLine[] lines)
        {
            if (lines == null || Film == null) return;
            var byId = new Dictionary<uint, string>();
            foreach (var l in lines)
                if (l != null && !byId.ContainsKey(l.EventId)) byId[l.EventId] = l.Text;
            int backfilled = 0;
            foreach (var line in _allLines)
            {
                if (line.FullText != null || !byId.TryGetValue(line.EventId, out var full)) continue;
                line.FullText = full;
                backfilled++;
            }
            if (_finalized)
            {
                try
                {
                    BattleFilmStore.Save(Film); // 已封卷——无遮蔽版重写（复盘时间轴取 FullText ?? Text）
                    Debug.Log($"[NetFilm] 终局全量行回填 {backfilled} 行 → 剧本重写 {Film.Header.MatchId}");
                }
                catch (Exception ex) { Debug.LogWarning($"[NetFilm] 全量回填重写失败（不影响对局）：{ex.Message}"); }
            }
        }

        /// <summary>终局信号（行流 GameOver 旗标 / 房态 Finished / 断线——三者先到即封，幂等）。</summary>
        public void OnMatchFinished()
        {
            if (Film == null || _finalized) return;
            _finalized = true;
            _inRound = false;
            AssignRemainingLines();
            try
            {
                BattleFilmStore.Save(Film);
                Debug.Log($"[NetFilm] 联机剧本已录制：{Film.Frames.Count} 帧 / {_allLines.Count} 行 → {Film.Header.MatchId}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetFilm] 剧本落盘失败（不影响对局）：{ex.Message}");
            }
        }

        /// <summary>帧驱动（BattleScreen Updater 每帧调）：空闲期放行积压行 + 落地暂存快照。</summary>
        public void Tick()
        {
            if (_playhead != null && _playhead.Busy) return;
            if (_deferredView != null)
            {
                var v = _deferredView;
                _deferredView = null;
                _applyView?.Invoke(v);
            }
            if (_pendingLines.Count > 0)
            {
                var lines = _pendingLines.ToArray();
                _pendingLines.Clear();
                _emitLines?.Invoke(lines);
            }
        }

        /// <summary>终局行归帧收尾：未归帧行（轮外/终局行）按「第一个 LastEventId >= EventId 的帧」归位。</summary>
        private void AssignRemainingLines()
        {
            var frames = Film.Frames;
            for (int i = 0; i < _allLines.Count; i++)
            {
                var line = _allLines[i];
                if (line.FrameIndex >= 0) continue;
                int f = 0;
                while (f < frames.Count && frames[f].LastEventId < line.EventId) f++;
                if (f >= frames.Count) f = frames.Count - 1;
                line.FrameIndex = f;
                frames[f].Lines.Add(line);
            }
        }

        // ==== 演出映射（NetFxMapper 消费口） ====

        private void MapFx(NetEvent e)
        {
            var ops = NetFxMapper.Map(e, ViewSeatOf);
            if (ops == null) return;
            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                if (op == null) continue;
                if (op.Kind == BattleFxKind.DeathHold) _pendingPre.Add(op);
                else _pendingOps.Add(op);
            }
        }

        /// <summary>引擎座位 → 观看视角座位（0=我方 1=对手；观战=-1 按引擎座位原样——BuildNet 以座位 0 为底）。</summary>
        private int ViewSeatOf(int engineSeat)
            => ViewerSeat < 0 || engineSeat < 0 ? engineSeat : (engineSeat == ViewerSeat ? 0 : 1);

        private FxRef MapRef(NetEvent e, string field) => NetFxMapper.FxRefOf(NetFxMapper.RefOf(e, field), ViewSeatOf);
        private static int MapInt(NetEvent e, string field) => NetFxMapper.IntOf(e, field);
    }

    /// <summary>NetEvent → BattleFxOp 映射（二期 C·方案 §3-C：字段从 NetParam 读——Entity→引用/
    /// Enum→IntValue/bool→Int；缺省字段按表现层可缺省）。与本地投影器 op 构造同规则同判据
    ///（白名单/极性/仅施加爆发等），不依赖引擎对象。</summary>
    public static class NetFxMapper
    {
        /// <summary>映射一个白名单事件为 0..n 条演出指令（非演出事件返回 null）。</summary>
        public static List<BattleFxOp> Map(NetEvent e, Func<int, int> viewSeatOf)
        {
            if (e == null) return null;
            switch (e.EventType)
            {
                case nameof(AtomicDamageEvent):
                {
                    if (IntOf(e, "IsCombatDamage") != 0) break; // 战斗伤害走 CombatDamageEvent op（防双播）
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.Damage,
                        Source = FxRefOf(RefOf(e, "Source"), viewSeatOf),
                        Target = FxRefOf(RefOf(e, "Target"), viewSeatOf),
                        Amount = IntOf(e, "Damage"),
                        Tint = TintOf(e, "EffectTint"),
                        CastX = -1, CastZ = -1, // 事件流无法术位坐标——弹体自来源锚起飞
                    });
                }
                case nameof(CombatDamageEvent):
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.Damage,
                        Source = FxRefOf(RefOf(e, "Attacker"), viewSeatOf),
                        Target = FxRefOf(RefOf(e, "Defender"), viewSeatOf),
                        Amount = IntOf(e, "Damage"),
                        Tint = -1,
                        CastX = -1, CastZ = -1,
                    });
                case nameof(HealEvent):
                {
                    var amount = IntOf(e, "Amount") - IntOf(e, "Overfill"); // 溢出截断实效（与本地同式）
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.Heal,
                        Target = FxRefOf(RefOf(e, "Target"), viewSeatOf),
                        Amount = amount,
                    });
                }
                case nameof(CardDestroyEvent):
                {
                    var target = FxRefOf(RefOf(e, "DestroyedCard"), viewSeatOf);
                    if (target == null) break;
                    return new List<BattleFxOp>
                    {
                        // 死亡预持（PreOps——先于 View 防对账抢先销毁尸体；净路径恒即时持票）
                        new BattleFxOp { Kind = BattleFxKind.DeathHold, Target = target },
                        new BattleFxOp
                        {
                            Kind = BattleFxKind.Death,
                            Source = FxRefOf(RefOf(e, "Source"), viewSeatOf),
                            Target = target,
                            Reason = IntOf(e, "Reason"),
                            Tint = TintOf(e, "EffectTint"),
                            CastX = -1, CastZ = -1,
                        },
                    };
                }
                case nameof(CardPutToBattlefieldEvent):
                {
                    if (IntOf(e, "Source") != (int)EnterSource.CastPlayed) break; // 仅打出入场（与本地同闸）
                    var target = FxRefOf(RefOf(e, "Card"), viewSeatOf);
                    if (target == null) break;
                    var controller = RefOf(e, "Controller");
                    int seat = controller != null && controller.IsPlayer ? viewSeatOf(controller.Seat) : -1;
                    var paid = PrintedCostOf(target.CardId); // 实付不可映射（PaidCost 投影 Opaque）——印刷费色近似
                    if (paid == null) break;
                    return One(new BattleFxOp { Kind = BattleFxKind.Arrival, Target = target, Seat = seat, Paid = paid });
                }
                case nameof(CounterChangedEvent):
                {
                    var ops = new List<BattleFxOp>(2);
                    var counterType = StrOf(e, "CounterType");
                    int amount = IntOf(e, "Amount");
                    bool curseBless = counterType == CounterRules.CurseCounter
                                      || counterType == CounterRules.BlessingCounter;
                    if (curseBless)
                    {
                        if (amount > 0) // 爆发只在施加（消层无粒子——与本地同闸）
                            ops.Add(new BattleFxOp
                            {
                                Kind = BattleFxKind.DeckMark,
                                Target = FxRefOf(RefOf(e, "Target"), viewSeatOf),
                                Amount = amount,
                                Flag = counterType == CounterRules.BlessingCounter,
                            });
                    }
                    else if (TintOf(e, "EffectTint") >= 0 && amount > 0)
                    {
                        var polarity = CounterRules.Find(counterType)?.Polarity ?? CounterPolarity.Neutral;
                        if (polarity != CounterPolarity.Neutral) // 中性不播（与本地同闸）
                            ops.Add(new BattleFxOp
                            {
                                Kind = BattleFxKind.CounterBurst,
                                Source = FxRefOf(RefOf(e, "Source"), viewSeatOf),
                                Target = FxRefOf(RefOf(e, "Target"), viewSeatOf),
                                Tint = TintOf(e, "EffectTint"),
                                Flag = polarity == CounterPolarity.Negative,
                            });
                    }
                    ops.Add(new BattleFxOp // 值浮字（2026-11-13 P3 统一入口）：全指示物类型经
                    {                       // ValueFloatRules 拼标签+极性（与本地 Projector 同单源；"*"/零增量/未知 id 拦截）
                        Kind = BattleFxKind.ValueFloat,
                        Target = FxRefOf(RefOf(e, "Target"), viewSeatOf),
                        Text = CardCore.Effects.ValueFloatRules.TryBuild(counterType, amount, out var vfLabel, out var vfPolarity)
                            ? vfLabel : null,
                        Amount = amount,
                        Polarity = vfPolarity,
                    });
                    return ops;
                }
                case nameof(StatModifyEvent):
                {
                    // 属性直改值浮字（2026-11-13 P3·与本地同式）：差 0 不出；标签单源 ValueFloatRules
                    int oldV = IntOf(e, "OldValue"), newV = IntOf(e, "NewValue");
                    if (newV == oldV) break;
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.ValueFloat,
                        Target = FxRefOf(RefOf(e, "Target"), viewSeatOf),
                        Text = CardCore.Effects.ValueFloatRules.StatDeltaLabel((StatType)IntOf(e, "StatType"), newV - oldV),
                        Amount = newV - oldV,
                        Polarity = newV > oldV ? 1 : -1,
                    });
                }
                case nameof(StatSetEvent):
                {
                    int oldV = IntOf(e, "OldValue"), newV = IntOf(e, "NewValue");
                    if (newV == oldV) break;
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.ValueFloat,
                        Target = FxRefOf(RefOf(e, "Target"), viewSeatOf),
                        Text = CardCore.Effects.ValueFloatRules.StatDeltaLabel((StatType)IntOf(e, "StatType"), newV - oldV),
                        Amount = newV - oldV,
                        Polarity = newV > oldV ? 1 : -1,
                    });
                }
                case nameof(LifeChangeEvent):
                {
                    // 角色生命值浮字（2026-11-13 P3·Cause 分流与本地一致）：伤害/回复/疲劳路径的
                    // LifeChange 不出值（各自 op 已承载——防双播）；Cause 缺省 0=None=旧端伴随口径
                    //（不出）。Lifelink/Modify/Set/Lock 才出 ±N。
                    int oldL = IntOf(e, "OldLife"), newL = IntOf(e, "NewLife");
                    int cause = IntOf(e, "Cause");
                    bool visible = cause == (int)LifeChangeCause.Lifelink
                                   || cause == (int)LifeChangeCause.Modify
                                   || cause == (int)LifeChangeCause.Set
                                   || cause == (int)LifeChangeCause.Lock;
                    if (!visible || newL == oldL) break;
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.ValueFloat,
                        Target = FxRefOf(RefOf(e, "Player"), viewSeatOf),
                        Text = CardCore.Effects.ValueFloatRules.Sign(newL - oldL),
                        Amount = newL - oldL,
                        Polarity = newL > oldL ? 1 : -1,
                    });
                }
                case nameof(KeywordEvent):
                {
                    if (IntOf(e, "IsAdd") == 0) break; // 仅赋予落地（IsAdd 唯一发布口）
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.KeywordBurst,
                        Source = FxRefOf(RefOf(e, "Source"), viewSeatOf),
                        Target = FxRefOf(RefOf(e, "Target"), viewSeatOf),
                        Tint = TintOf(e, "EffectTint"),
                        Flag = IntOf(e, "IsDebuff") != 0,
                    });
                }
                case nameof(ClashResolvedEvent):
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.Clash,
                        Target = FxRefOf(RefOf(e, "EngineCard"), viewSeatOf),
                        D1 = IntOf(e, "MyPower"), D2 = IntOf(e, "TheirPower"),
                        Threshold = IntOf(e, "Threshold"), Flag = IntOf(e, "Success") != 0,
                    });
                case nameof(LuckRolledEvent):
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.LuckRoll,
                        Target = FxRefOf(RefOf(e, "EngineCard"), viewSeatOf),
                        D1 = IntOf(e, "D1"), D2 = IntOf(e, "D2"), Flag = IntOf(e, "Success") != 0,
                    });
                case nameof(EffectFlightEvent):
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.Flight,
                        Source = FxRefOf(RefOf(e, "Source"), viewSeatOf),
                        Target = FxRefOf(RefOf(e, "Target"), viewSeatOf),
                        Tint = TintOf(e, "Tint"),
                        CastX = -1, CastZ = -1,
                    });
                case nameof(FatigueEvent):
                {
                    var player = RefOf(e, "Player");
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.Fatigue,
                        Seat = player != null && player.IsPlayer ? viewSeatOf(player.Seat) : -1,
                        Amount = IntOf(e, "Damage"),
                    });
                }
                case nameof(PhaseStartEvent):
                {
                    var player = RefOf(e, "ActivePlayer");
                    return One(new BattleFxOp
                    {
                        Kind = BattleFxKind.PhaseAnnounce,
                        Seat = player != null && player.IsPlayer ? viewSeatOf(player.Seat) : -1,
                        Reason = IntOf(e, "Phase"),
                    });
                }
            }
            return null;
        }

        private static List<BattleFxOp> One(BattleFxOp op) => new List<BattleFxOp> { op };

        // ---- NetParam 读取口（schema-less：FieldName 寻址；缺字段=类型缺省） ----

        public static NetParam ParamOf(NetEvent e, string field)
            => e.Params?.FirstOrDefault(p => p != null && p.FieldName == field);

        public static NetEntityRef RefOf(NetEvent e, string field)
        {
            var p = ParamOf(e, field);
            if (p == null || p.Kind != NetParamKind.Entity || p.EntityRefs == null || p.EntityRefs.Length == 0)
                return null;
            var r = p.EntityRefs[0];
            if (r == null || (!r.IsPlayer && r.RuntimeId == 0)) return null; // None 占位（Seat=-1/rid=0）
            return r;
        }

        public static int IntOf(NetEvent e, string field)
        {
            var p = ParamOf(e, field);
            return p != null && (p.Kind == NetParamKind.Int || p.Kind == NetParamKind.Enum) ? (int)p.IntValue : 0;
        }

        public static string StrOf(NetEvent e, string field)
            => ParamOf(e, field)?.StringValue;

        public static int TintOf(NetEvent e, string field)
        {
            var p = ParamOf(e, field);
            return p != null && p.Kind == NetParamKind.Enum ? (int)p.IntValue : -1; // Null/缺省=-1（无基准回落灰）
        }

        public static FxRef FxRefOf(NetEntityRef r, Func<int, int> viewSeatOf)
        {
            if (r == null) return null;
            return new FxRef
            {
                Rid = r.IsPlayer ? 0 : r.RuntimeId,
                CardId = r.CardId,
                IsPlayer = r.IsPlayer,
                Seat = r.Seat >= 0 && viewSeatOf != null ? viewSeatOf(r.Seat) : r.Seat,
            };
        }

        /// <summary>印刷费色（入场粒子颜色近似——实付不可映射）：恒推导费模式 0 非零色。</summary>
        private static Dictionary<int, float> PrintedCostOf(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            var data = CardCatalog.GetById(cardId);
            if (data == null) return null;
            var cost = CardCostService.GetRuntimeCost(data, 0);
            var paid = new Dictionary<int, float>();
            foreach (var m in cost.NonzeroColors())
                paid[(int)m] = cost[m];
            return paid.Count > 0 ? paid : null;
        }
    }
}
