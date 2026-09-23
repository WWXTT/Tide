using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using CardCore.AI.NeuralEnv;
using CardCore.Editor.Tests;
using CardCore.Network;
using CardCore.Serialization;
using Cysharp.Threading.Tasks;
using SynergyUI;
using MemoryPack;
using UnityEngine;
using UnityEditor;

namespace CardCore.Editor
{
    /// <summary>
    /// 网络协议回环验证（M1）：进程内 SimpleAI vs SimpleAI 一局，验证协议三件套自洽——
    /// ① 事件投影（NetEventProjector）产出完整无损；② 快照（NetSnapshotBuilder）与活体对账；
    /// ③ intent 通道（NetworkIntentApplier）与直调 GameActions 等价；④ 反问回环（ITargetSelectorEx）；
    /// ⑤ 开局握手（NetMatchHandshake：卡组引用闭包校验——缺卡/重复/原子行摘要漂移/回显篡改拒绝）。
    /// 全部走真实序列化路径：MemoryPack DTO → NetworkSerializer 信封 → FrameCodec 4B 前缀帧 →
    /// 流式解码（半包/粘包切分）→ 反序列化 → 逐字段深度比较。
    /// 菜单：Tools/网络协议回环验证。
    /// </summary>
    public static class NetProtocolLoopbackVerifier
    {
        private static int _pass;
        private static int _fail;

        private static void Assert(bool condition, string message)
        {
            if (condition) { _pass++; }
            else { _fail++; Debug.LogError($"[NetVerify] FAIL: {message}"); }
        }

        private static void Section(string title)
            => Debug.Log($"[NetVerify] ══ {title} ══");

        [MenuItem("Tools/网络协议回环验证")]
        public static void Run()
        {
            _pass = 0;
            _fail = 0;
            Debug.Log("[NetVerify] 开始：网络协议回环验证（M1）");

            try
            {
                var deck = AiBattleE2E.LoadStandardDeck();
                if (deck == null || deck.Count == 0)
                {
                    Debug.LogError("[NetVerify] 测试卡缺失（Cards.json 卡池为空——LoadStandardDeck=CardCatalog 全池非仪式卡），中止");
                    return;
                }

                RunFullGameSection(deck);
                RunIntentSection(deck);
                RunSelectorSection(deck);
                RunHandshakeSection();
                RunSocketSection();
                RunM3Section();
            }
            catch (Exception ex)
            {
                _fail++;
                Debug.LogError($"[NetVerify] 异常中止：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }

            Debug.Log($"[NetVerify] 完成：PASS {_pass} / FAIL {_fail}{(_fail == 0 ? " ✅" : " ❌")}");
        }

        // ============================================================
        // 第一段：整局事件流 + 快照（双 SimpleAI 驱动）
        // ============================================================

        private static void RunFullGameSection(List<CardData> deck)
        {
            Section("1. 整局事件流与快照");

            var core = GameCore.Instance;
            MsgGameStateSync midStackSnapshot = null;

            void OnStackAdd(StackAddEvent e)
            {
                // 中段采样：首个整卡施放上栈（双 Pass 结算前——栈非空快照的直接回归点）
                if (midStackSnapshot == null && e.AddedObject is EffectInstance inst && inst.IsCardCast)
                    midStackSnapshot = NetSnapshotBuilder.Build(core, 0);
            }

            EventManager.Instance.Subscribe<StackAddEvent>(OnStackAdd);
            try
            {
                var result = new AiBattleDriver().RunFullGame(deck, deck, 60);
                Assert(result.Completed, $"对局应自然结束（错误 {result.Errors.Count} 条：{string.Join(" | ", result.Errors.Take(3))}）");
                Assert(result.Errors.Count == 0, "对局过程零异常");
            }
            finally
            {
                EventManager.Instance.Unsubscribe<StackAddEvent>(OnStackAdd);
            }

            // ---- 1a. 投影完整性：与 MatchLogService（另一个 AnyPublished 消费者）必然同量 ----
            var events = NetEventProjector.Events;
            Assert(events.Count == MatchLogService.Entries.Count,
                $"投影量 {events.Count} == 日志量 {MatchLogService.Entries.Count}（AnyPublished 双消费者同量）");
            Assert(NetEventProjector.ErrorCount == 0, $"投影零异常（ErrorCount={NetEventProjector.ErrorCount}）");
            Assert(!NetEventProjector.WasTruncated, "未触顶缓冲上限");
            for (int i = 1; i < events.Count; i++)
            {
                if (events[i].EventId <= events[i - 1].EventId)
                {
                    Assert(false, $"EventId 应严格递增：[{i - 1}]={events[i - 1].EventId} → [{i}]={events[i].EventId}");
                    break;
                }
            }
            Assert(true, "EventId 严格递增（AnyPublished 恰一次保序）");
            if (events.Count == 0)
            {
                Assert(false, "事件流为空——对局未产生事件，后续段落无法验证");
                return;
            }
            Debug.Log($"[NetVerify] 事件总量 {events.Count}，类型 {events.Select(e => e.EventType).Distinct().Count()} 种");

            // ---- 1a-2. 线上去文本口径（2026-09-22）：执行摘要事件只传身份，拼好文本不进线格式 ----
            var summaryEvents = events.Where(e => e.EventType == nameof(CardCore.EffectExecutionSummaryEvent)).ToList();
            if (summaryEvents.Count == 0)
            {
                Assert(false, "整局无执行摘要事件——线上去文本口径未被覆盖（效果未发动？）");
            }
            else
            {
                foreach (var se in summaryEvents)
                {
                    var names = se.Params.Select(p => p.FieldName).ToHashSet();
                    if (names.Contains("Description") || names.Contains("Instance"))
                    {
                        Assert(false, $"执行摘要事件 {se.EventId} 携带 Description/Instance 参数（线上去文本回归）");
                        break;
                    }
                    if (!names.Contains("EffectId") || !names.Contains("Source") || !names.Contains("Controller"))
                    {
                        Assert(false, $"执行摘要事件 {se.EventId} 缺身份参数 EffectId/Source/Controller");
                        break;
                    }
                }
                Assert(true, $"执行摘要事件 ×{summaryEvents.Count}：只传 EffectId/Source/Controller，不传拼好文本");
            }

            // ---- 1b. 信封 + 帧无损：批次 64 与单条两口径，帧按三段切分（半包/粘包） ----
            var roundTripped = 0;
            for (int offset = 0; offset < events.Count; offset += 64)
            {
                var batch = new MsgNetEventBatch { Events = events.Skip(offset).Take(64).ToArray() };
                var decoded = RoundTripBatch(batch);
                AssertDeepEqual(batch.Events, decoded.Events, $"批次@{offset}");
                roundTripped += batch.Events.Length;
            }
            Assert(roundTripped == events.Count, $"批次口径覆盖全部事件（{roundTripped}/{events.Count}）");

            var single = new MsgNetEventBatch { Events = new[] { events[events.Count / 2] } };
            AssertDeepEqual(single.Events, RoundTripBatch(single).Events, "单条口径");

            // ---- 1c. 终局快照对账（双视角） ----
            VerifySnapshot(core, 0);
            VerifySnapshot(core, 1);

            // ---- 1d. 中段栈快照（现有粒度错误的直接回归：终局栈空测不到） ----
            if (midStackSnapshot != null)
            {
                var sampled = midStackSnapshot.StackV2;
                // 对局结束后栈已清空——只校验采样快照自身的结构完整性与信封无损
                Assert(sampled != null && sampled.Length >= 1, "中段快照含栈条目");
                var top = sampled[sampled.Length - 1];
                Assert(top.IsCardCast, "采样栈顶为整卡施放");
                Assert(top.Source != null && top.Source.RuntimeId != 0, "栈顶源卡引用有效");
                var rt = RoundTripSnapshot(midStackSnapshot);
                Assert(rt.StackV2.Length == sampled.Length
                    && rt.StackV2[rt.StackV2.Length - 1].Source?.RuntimeId == top.Source?.RuntimeId
                    && rt.StackV2[rt.StackV2.Length - 1].ModeIndex == top.ModeIndex,
                    "中段快照（含栈）信封往返无损");
                Debug.Log($"[NetVerify] 中段栈采样：栈深 {sampled.Length}，顶 mode={top.ModeIndex}");
            }
            else
            {
                Assert(false, "中段栈采样缺失（整局未出现整卡施放上栈？）");
            }
        }

        /// <summary>MsgNetEventBatch 全链路往返：DTO → 信封 → 帧（三段切分喂解码器）→ 解码 → 反序列化。</summary>
        private static MsgNetEventBatch RoundTripBatch(MsgNetEventBatch batch)
        {
            var wire = NetworkSerializer.SerializeMessage(NetworkMessageType.NetEventBatch, batch);
            var envelope = NetworkSerializer.DeserializeEnvelope(wire);
            var frame = FrameCodec.Frame(envelope);

            var decoder = new FrameCodec.FrameDecoder();
            decoder.Append(frame.Take(2).ToArray());          // 前缀内切一刀
            decoder.Append(frame.Skip(2).Take(frame.Length / 3).ToArray());
            decoder.Append(frame.Skip(2 + frame.Length / 3).ToArray());

            if (!decoder.TryDecode(out var decoded) || decoder.TryDecode(out _))
                throw new InvalidOperationException("帧解码失败或多余帧");
            return NetworkSerializer.DeserializePayload<MsgNetEventBatch>(decoded);
        }

        private static MsgGameStateSync RoundTripSnapshot(MsgGameStateSync snapshot)
        {
            var wire = NetworkSerializer.SerializeMessage(NetworkMessageType.GameStateSyncV2, snapshot);
            var envelope = NetworkSerializer.DeserializeEnvelope(wire);
            var frame = FrameCodec.Frame(envelope);
            var decoder = new FrameCodec.FrameDecoder();
            decoder.Append(frame);
            if (!decoder.TryDecode(out var decoded))
                throw new InvalidOperationException("快照帧解码失败");
            return NetworkSerializer.DeserializePayload<MsgGameStateSync>(decoded);
        }

        private static void AssertDeepEqual(NetEvent[] expected, NetEvent[] actual, string label)
        {
            if (expected.Length != actual.Length)
            {
                Assert(false, $"{label} 事件数不符：{expected.Length} vs {actual.Length}");
                return;
            }
            for (int i = 0; i < expected.Length; i++)
            {
                if (!EqualEvent(expected[i], actual[i]))
                {
                    Assert(false, $"{label}[{i}] 事件 {expected[i].EventType} 深度比较失败");
                    return;
                }
            }
            Assert(true, $"{label} 信封+帧往返无损（{expected.Length} 条）");
        }

        private static bool EqualEvent(NetEvent a, NetEvent b)
        {
            if (a.EventType != b.EventType || a.EventId != b.EventId || a.TurnNumber != b.TurnNumber
                || a.Params?.Length != b.Params?.Length) return false;
            if (a.Params == null) return true;
            for (int p = 0; p < a.Params.Length; p++)
            {
                var pa = a.Params[p];
                var pb = b.Params[p];
                if (pa.FieldName != pb.FieldName || pa.Kind != pb.Kind
                    || pa.IntValue != pb.IntValue || Math.Abs(pa.FloatValue - pb.FloatValue) > 1e-9
                    || pa.StringValue != pb.StringValue
                    || pa.EntityRefs?.Length != pb.EntityRefs?.Length) return false;
                if (pa.EntityRefs == null) continue;
                for (int r = 0; r < pa.EntityRefs.Length; r++)
                {
                    var ra = pa.EntityRefs[r];
                    var rb = pb.EntityRefs[r];
                    if (ra.RuntimeId != rb.RuntimeId || ra.Seat != rb.Seat
                        || ra.IsPlayer != rb.IsPlayer || ra.CardId != rb.CardId) return false;
                }
            }
            return true;
        }

        /// <summary>快照对账：PlayerState 与活体、区域卡与 FromCard 双向、隐藏信息口径、字节确定性。</summary>
        private static void VerifySnapshot(GameCore core, int viewerSeat)
        {
            var snapshot = NetSnapshotBuilder.Build(core, viewerSeat);

            // 对账一：PlayerState 各字段 == 活体
            foreach (var ps in snapshot.Players)
            {
                var player = ps.Seat == 0 ? core.Player1 : core.Player2;
                Assert(ps.Life == player.Life && ps.MaxHealth == player.MaxHealth
                    && ps.DeckCount == core.ZoneManager.GetCards(player, Zone.Deck).Count
                    && ps.HandCount == core.ZoneManager.GetCards(player, Zone.Hand).Count
                    && ps.FatigueCount == player.FatigueCount
                    && ps.GraveyardCount == core.ZoneManager.GetCards(player, Zone.Graveyard).Count
                    && ps.ExileCount == core.ZoneManager.GetCards(player, Zone.Exile).Count
                    && ps.LandCap == core.ElementPool.GetLandCap(player)
                    && ps.IsAI == player.IsAI,
                    $"P{ps.Seat} PlayerState 与活体一致（viewer={viewerSeat}）");

                // bank 逐色比对（快照只列 >0 条目）
                foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
                {
                    int live = core.ElementPool.GetAvailableManaCount(type, player);
                    int sent = ps.ElementBank?.Where(e => e.ManaType == (int)type).Sum(e => e.Value) ?? 0;
                    if (live != sent) { Assert(false, $"P{ps.Seat} bank {type}：live={live} sent={sent}"); break; }
                }
            }

            // 对账二：区域卡与 FromCard 现算 DTO 全等（活体真值）
            foreach (var zoneCards in snapshot.ZoneCards)
            {
                var player = zoneCards.Seat == 0 ? core.Player1 : core.Player2;
                var live = core.ZoneManager.GetCards(player, (Zone)zoneCards.Zone);
                Assert(zoneCards.Cards.Length == live.Count,
                    $"区域 {zoneCards.Zone}(seat{zoneCards.Seat}) 卡数 {zoneCards.Cards.Length} == 活体 {live.Count}");
                for (int i = 0; i < live.Count; i++)
                    Assert(EqualCardState(zoneCards.Cards[i], SerializableRuntimeCardState.FromCard(live[i])),
                        $"区域 {zoneCards.Zone}[{i}] RuntimeId={live[i].RuntimeId} 卡状态与活体全等");
            }

            // 对账三：隐藏信息口径（己方手牌可见、对方只见数量；牌库只数量）
            var myHand = snapshot.Hands.First(h => h.Seat == viewerSeat);
            var oppHand = snapshot.Hands.First(h => h.Seat != viewerSeat);
            var liveMine = core.ZoneManager.GetCards(viewerSeat == 0 ? core.Player1 : core.Player2, Zone.Hand);
            var liveOpp = core.ZoneManager.GetCards(viewerSeat == 0 ? core.Player2 : core.Player1, Zone.Hand);
            Assert(myHand.Count == liveMine.Count
                && myHand.OwnRuntimeIds != null
                && myHand.OwnRuntimeIds.OrderBy(x => x).SequenceEqual(liveMine.Select(c => c.RuntimeId).OrderBy(x => x)),
                $"viewer{viewerSeat} 见己方手牌 RuntimeId 全集");
            Assert(oppHand.Count == liveOpp.Count
                && (oppHand.OwnRuntimeIds == null || oppHand.OwnRuntimeIds.Length == 0),
                $"viewer{viewerSeat} 对对方手牌只见数量（{oppHand.Count}）");
            Assert(snapshot.ZoneCards.All(z => (Zone)z.Zone != Zone.Deck), "牌库不进 ZoneCards（只数量）");

            // 对账四：字节确定性（同状态连拍两次，MemoryPack 字节全等——Counters 排序回归）
            var again = NetSnapshotBuilder.Build(core, viewerSeat);
            var b1 = MemoryPackSerializer.Serialize(snapshot);
            var b2 = MemoryPackSerializer.Serialize(again);
            Assert(b1.SequenceEqual(b2), $"viewer{viewerSeat} 快照字节确定（{b1.Length}B）");

            // 对账五：往返无损
            var rt = RoundTripSnapshot(snapshot);
            Assert(rt.ViewerSeat == snapshot.ViewerSeat
                && rt.Players.Length == snapshot.Players.Length
                && rt.ZoneCards.Length == snapshot.ZoneCards.Length
                && rt.Hands.Length == snapshot.Hands.Length,
                $"viewer{viewerSeat} 快照信封往返结构无损");
        }

        private static bool EqualCardState(SerializableRuntimeCardState a, SerializableRuntimeCardState b)
        {
            if (a.ID != b.ID || a.RuntimeId != b.RuntimeId || a.Power != b.Power || a.Life != b.Life
                || a.MaxLife != b.MaxLife || a.IsTapped != b.IsTapped || a.IsFrozen != b.IsFrozen
                || a.ControllerSeat != b.ControllerSeat || a.Zone != b.Zone
                || !(a.Keywords ?? Array.Empty<string>()).SequenceEqual(b.Keywords ?? Array.Empty<string>()))
                return false;

            // CounterEntryDTO 是 class——SequenceEqual 比引用，必须逐值比较
            var ca = (a.Counters ?? Array.Empty<CounterEntryDTO>()).OrderBy(c => c.Key).ToArray();
            var cb = (b.Counters ?? Array.Empty<CounterEntryDTO>()).OrderBy(c => c.Key).ToArray();
            if (ca.Length != cb.Length) return false;
            for (int i = 0; i < ca.Length; i++)
                if (ca[i].Key != cb[i].Key || ca[i].Value != cb[i].Value) return false;
            return true;
        }

        // ============================================================
        // 第二段：intent 通道（整局经 NetworkIntentApplier 驱动，与直调等价的最小口径）
        // ============================================================

        private static void RunIntentSection(List<CardData> deck)
        {
            Section("2. intent 通道（枚举→映射→信封→分派）");

            var core = GameCore.Instance;
            GameOverEvent gameOver = null;
            void OnGameOver(GameOverEvent e) => gameOver = e;
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
            try
            {
                core.InitGame(CardLoader.BuildDeck(deck, 1), CardLoader.BuildDeck(deck, 1));
                core.Player1.IsAI = true; // 结算期残余交互走自动选择（与 AI 局同口径）
                core.Player2.IsAI = true;
                CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;

                var enumerator = new LegalActionEnumerator();
                var banned = new HashSet<string>();
                var coverage = new Dictionary<TideActionType, int>();
                int bannedTurnStamp = -1; // 拉黑按回合清（镜像 TideHeadlessDriver._bannedThisTurn）：
                                          // 同一动作下回合可能重新合法（费用攒够/场面变化）

                // 泛型保持具体声明类型：MemoryPack 按声明类型找 formatter（object 无注册）；
                // 空载荷 intent 用 byte[0] 占位（原生支持，applier 不读这些 payload）
                bool ApplyIntent<T>(int seat, NetworkMessageType type, T payload) where T : class
                {
                    var wire = NetworkSerializer.SerializeMessage(type, payload);
                    var envelope = NetworkSerializer.DeserializeEnvelope(wire);
                    return NetworkIntentApplier.Apply(core, seat, envelope, out _);
                }
                var emptyPayload = Array.Empty<byte>();

                for (int turn = 0; turn < 60 && gameOver == null; turn++)
                {
                    var me = core.TurnEngine.TurnPlayer;
                    int seat = ReferenceEquals(me, core.Player1) ? 0 : 1;

                    // Standby → Main：经 intent
                    if (core.TurnEngine.CurrentPhase?.Phase == PhaseType.Standby)
                        Assert(ApplyIntent(seat, NetworkMessageType.IntentSkipStandby, emptyPayload), $"IntentSkipStandby@T{turn}");

                    if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) continue;

                    if (bannedTurnStamp != core.TurnEngine.TurnNumber)
                    {
                        bannedTurnStamp = core.TurnEngine.TurnNumber;
                        banned.Clear();
                    }

                    // 周期诊断（Attack 覆盖回归定位用）：场面/手牌/地牌走势
                    if (turn % 10 == 0)
                        Debug.Log($"[NetVerify][intent] T{turn} seat{seat}"
                            + $" hand={core.ZoneManager.GetCards(me, Zone.Hand).Count}"
                            + $" bf0={core.ZoneManager.GetCards(core.Player1, Zone.Battlefield).Count}"
                            + $" bf1={core.ZoneManager.GetCards(core.Player2, Zone.Battlefield).Count}"
                            + $" lands0={core.ElementPool.GetPooledCards(core.Player1).Count}"
                            + $" lands1={core.ElementPool.GetPooledCards(core.Player2).Count}");

                    // 横置产元素：经 intent（颜色=首个可用色；驱动侧镜像 SimpleAI.TapAllLands 职责）
                    foreach (var pooled in core.ElementPool.GetPooledCards(me).Where(p => !p.IsTapped).ToList())
                    {
                        var color = pooled.GetAvailableColors().FirstOrDefault();
                        if (color == default && !pooled.GetAvailableColors().Any()) continue;
                        ApplyIntent(seat, NetworkMessageType.IntentTapForElement,
                            new MsgIntentTapForElement { CardRuntimeId = pooled.SourceCard.RuntimeId, ManaType = (int)color });
                    }

                    // 动作循环：枚举 → 选型 → 映射 intent → 分派（拒绝即本回合拉黑重枚举）
                    bool landPlayedThisTurn = false;
                    for (int step = 0; step < 24; step++)
                    {
                        enumerator.Enumerate(core, me);
                        enumerator.RemoveAll(banned);
                        if (enumerator.Count == 0) break;

                        // 选型（2026-09-23 修复 Attack×0 回归）：①优先覆盖未触碰类型
                        // ②非 EndTurn 且非 PlayLand ③PlayLand（每回合至多 1 张——地牌资格=只有
                        // 生物可作地牌，无节制铺地会把生物全部吞成地、战场常年空、攻击与
                        // 激活永远覆盖不到；引擎不限次数，驱动按 TCG 惯例配速）④EndTurn
                        //（TideAction 是结构体，不用 ??）
                        TideAction choice = default;
                        bool found = false;
                        foreach (var a in enumerator.Actions)
                            if (a.Type != TideActionType.EndTurn && a.Type != TideActionType.PlayLand
                                && !coverage.ContainsKey(a.Type)) { choice = a; found = true; break; }
                        if (!found)
                            foreach (var a in enumerator.Actions)
                                if (a.Type != TideActionType.EndTurn && a.Type != TideActionType.PlayLand) { choice = a; found = true; break; }
                        if (!found && !landPlayedThisTurn)
                            foreach (var a in enumerator.Actions)
                                if (a.Type == TideActionType.PlayLand) { choice = a; found = true; break; }
                        if (!found)
                            foreach (var a in enumerator.Actions)
                                if (a.Type == TideActionType.EndTurn) { choice = a; found = true; break; }
                        if (!found) break;

                        bool accepted;
                        switch (choice.Type)
                        {
                            case TideActionType.PlayCard:
                                accepted = ApplyIntent(seat, NetworkMessageType.IntentPlayCard, new MsgIntentPlayCard
                                {
                                    CardRuntimeId = choice.Card.RuntimeId,
                                    Targets = null,
                                    FromZone = (int)Zone.Hand,
                                    ModeIndex = choice.ModeIndex,
                                });
                                break;
                            case TideActionType.PlayLand:
                                accepted = ApplyIntent(seat, NetworkMessageType.IntentAddToElementPool,
                                    new MsgIntentAddToElementPool { CardRuntimeId = choice.Card.RuntimeId });
                                break;
                            case TideActionType.Activate:
                                accepted = ApplyIntent(seat, NetworkMessageType.IntentActivateEffect, new MsgIntentActivateEffect
                                {
                                    SourceCardRuntimeId = choice.Card.RuntimeId,
                                    EffectId = choice.Effect?.Id,
                                    Targets = null,
                                });
                                break;
                            case TideActionType.Attack:
                                accepted = ApplyIntent(seat, NetworkMessageType.IntentDeclareAttack, new MsgIntentDeclareAttack
                                {
                                    Attacker = NetEntityMapper.FromCard(choice.Card),
                                    Target = NetEntityMapper.FromEntity(choice.Target),
                                });
                                break;
                            default:
                                accepted = ApplyIntent(seat, NetworkMessageType.IntentEndTurn, emptyPayload);
                                break;
                        }

                        if (accepted)
                        {
                            coverage[choice.Type] = coverage.GetValueOrDefault(choice.Type) + 1;
                            if (choice.Type == TideActionType.PlayLand) landPlayedThisTurn = true;
                            if (choice.Type == TideActionType.EndTurn) break;
                            GameActions.DrainStack(core); // 结算上栈的 cast（驱动侧职责，同 SimpleAI.SettleStack）
                        }
                        else
                        {
                            banned.Add(choice.Signature);
                        }
                    }

                    // End→Standby 折返（编辑器无帧泵的既定惯例，同 AiBattleDriver）
                    if (gameOver == null)
                        core.TurnEngine.CheckPhaseTransition();
                }

                foreach (var type in new[] { TideActionType.PlayCard, TideActionType.PlayLand, TideActionType.Attack, TideActionType.EndTurn })
                    Assert(coverage.GetValueOrDefault(type) >= 1, $"intent 覆盖 {type} × {coverage.GetValueOrDefault(type)} ≥ 1");
                Debug.Log($"[NetVerify] intent 覆盖：{string.Join(", ", coverage.Select(k => $"{k.Key}×{k.Value}"))}");

                // Concede：经 intent 认输
                if (gameOver == null)
                {
                    Assert(ApplyIntent(1, NetworkMessageType.IntentConcede, emptyPayload), "IntentConcede 被接受");
                    Assert(gameOver != null && gameOver.Reason == GameOverReason.Concede
                        && ReferenceEquals(gameOver.Winner, core.Player1),
                        "认输判定：胜者=P1、原因=Concede");
                }
            }
            finally
            {
                EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            }
        }

        // ============================================================
        // 第三段：反问回环（ITargetSelectorEx → MsgSelectRequest/Response 全链路同步回环）
        // ============================================================

        private static void RunSelectorSection(List<CardData> deck)
        {
            Section("3. 反问回环（选择协议）");

            var core = GameCore.Instance;
            core.InitGame(CardLoader.BuildDeck(deck, 1), CardLoader.BuildDeck(deck, 1));
            core.Player1.IsAI = false; // 关键：P1 非 AI + 选择器在位 → 走 ITargetSelectorEx 分支
            core.Player2.IsAI = true;

            var selector = new LoopbackTargetSelector();
            selector.Attach();
            try
            {
                // ---- 3a. 实体反问（RequestAsync 直测：候选=手牌实体） ----
                var hand = core.ZoneManager.GetCards(core.Player1, Zone.Hand);
                var chosen = TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = hand.Cast<Entity>().ToList(),
                    MinCount = 1,
                    MaxCount = 2,
                    Chooser = core.Player1,
                    Title = "回环测试",
                    AllowCancel = false,
                }).GetAwaiter().GetResult();

                Assert(chosen.Count == 1 && chosen[0].RuntimeId == hand[0].RuntimeId,
                    "实体反问：模拟客户端选 [0] → 引擎拿到候选[0]（索引→实体映射经网络往返）");
                var last = selector.Seen.Last();
                Assert(last.Labels.Length == 0 && last.Candidates.Length == hand.Count
                    && last.Min == 1 && last.Max == 2 && last.ChooserSeat == 0,
                    "MsgSelectRequest 携带完整请求（实体反问 Labels 恒空——卡名客户端按 CardId 查表、Min/Max/座位）");
                Assert(last.Candidates[0].RuntimeId == hand[0].RuntimeId
                    && last.Candidates[0].CardId == hand[0].ID,
                    "候选实体引用双轨正确（RuntimeId+模板 CardId）");

                // ---- 3b. 手牌上限弃牌（真实引擎反问点：GameCore.OnTurnEnded → EnforceHandLimitAsync） ----
                selector.Seen.Clear();
                int before = core.ZoneManager.GetCards(core.Player1, Zone.Hand).Count;
                int limit = RuleHooks.GetHandLimit(core.Player1);
                int over = limit + 2 - before;
                for (int i = 0; i < over; i++)
                    core.ZoneManager.DrawCard(core.Player1);

                Assert(GameActions.SkipElementPool(core, core.Player1), "进入主阶段");
                Assert(GameActions.EndTurn(core, core.Player1), "结束 P1 回合（触发手牌上限弃牌反问）");
                // EnforceHandLimitAsync 为 fire-and-forget，但回环选择器同步完成 → 链路同步收敛
                int after = core.ZoneManager.GetCards(core.Player1, Zone.Hand).Count;
                Assert(after == limit, $"手牌上限弃牌：{limit + 2} → {after}（限 {limit}）");
                Assert(selector.Seen.Any(r => r.Title == "手牌上限"), "弃牌反问走了 Ex 分支（真实引擎调用点）");

                // ---- 3c. 卸载后恢复 headless 路径 ----
            }
            finally
            {
                selector.Detach();
            }

            Assert(TargetSelectionService.Current == null, "卸载后 Current 归空");
            var auto = TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = core.ZoneManager.GetCards(core.Player1, Zone.Hand).Cast<Entity>().ToList(),
                MinCount = 1,
                MaxCount = 1,
                Chooser = core.Player1, // 非 AI 但 Current==null → AutoSelect（headless 默认）
                AllowCancel = false,
            }).GetAwaiter().GetResult();
            Assert(auto.Count == 1, "headless 自动选择路径不受影响（未装 Ex 时走原分支）");
        }

        // ============================================================
        // 第五段：开局握手（DeckSubmit/MatchManifest + 卡组引用闭包校验，2026-09-22）
        // 口径：只核对卡组引用闭包（卡/效果 ID 命中即一致 + 闭包原子行内容摘要）——不做全池对比。
        // ============================================================

        private static void RunHandshakeSection()
        {
            Section("5. 开局握手（卡组引用闭包校验）");

            // 真实用户数据路径（Cards.json/Effects.json/原子表）——握手校验的对象就是卡组引用
            var pool = CardCatalog.LoadAll();
            Assert(pool.Count > 0, "卡池非空（Cards.json 有卡——握手段落依赖真实用户数据）");
            if (pool.Count == 0) return;

            var deckIds = pool.Select(c => c.ID).ToArray();
            var totalRows = CardCore.Attribute.AtomicEffectTable.GetAll().Count();
            var digest = NetMatchHandshake.ComputeDeckDigest(deckIds);
            Assert(!string.IsNullOrEmpty(digest.AtomicRowsHash) && digest.AtomicRowCount > 0,
                "闭包原子行摘要非空（卡组确有原子引用）");
            Assert(digest.AtomicRowCount <= totalRows,
                $"摘要只覆盖卡组引用的行（{digest.AtomicRowCount} ≤ 全表 {totalRows}——非全量对比口径）");
            var digestAgain = NetMatchHandshake.ComputeDeckDigest(deckIds);
            Assert(digest.AtomicRowsHash == digestAgain.AtomicRowsHash,
                "摘要计算确定性（同数据两次计算全等）");

            // ---- 5a. 正例：真实卡池组卡组提交 → 服务器通过 + 线格式往返 + 双座位下发 + 客户端双向校验 ----
            var submit = new MsgDeckSubmit
            {
                DeckName = "回环验证卡组",
                CardIds = deckIds,
                Digest = digest,
            };
            Assert(NetMatchHandshake.ValidateDeckSubmit(submit, out _),
                $"正例卡组提交通过（{deckIds.Length} 张，含效果/原子引用闭环检查）");

            var decodedSubmit = RoundTripMessage(NetworkMessageType.DeckSubmit, submit);
            Assert(decodedSubmit.DeckName == submit.DeckName
                   && decodedSubmit.CardIds.SequenceEqual(submit.CardIds)
                   && decodedSubmit.Digest.AtomicRowsHash == digest.AtomicRowsHash,
                "DeckSubmit 信封+帧往返无损（卡组 ID 与闭包摘要）");

            foreach (var seat in new[] { 0, 1 })
            {
                var manifestMsg = NetMatchHandshake.BuildMatchManifest(seat, deckIds, deckIds.Length);
                var decodedManifest = RoundTripMessage(NetworkMessageType.MatchManifest, manifestMsg);
                Assert(NetMatchHandshake.VerifyMatchManifest(decodedManifest, deckIds, out var clientReason),
                    $"座位 {seat} 客户端对局清单双向校验通过{(clientReason != null ? "（" + clientReason + "）" : "")}");
            }

            // ---- 5b. 反例：缺卡拒绝（卡表无此 ID——原先会静默打成缺卡对局） ----
            var withGhost = new MsgDeckSubmit
            {
                DeckName = "缺卡卡组",
                CardIds = deckIds.Concat(new[] { "C_DEADBEEF" }).ToArray(),
                Digest = NetMatchHandshake.ComputeDeckDigest(deckIds.Concat(new[] { "C_DEADBEEF" }).ToArray()),
            };
            Assert(!NetMatchHandshake.ValidateDeckSubmit(withGhost, out var ghostReason)
                   && ghostReason.Contains("卡表缺失"),
                $"缺卡提交被拒（reason 含\"卡表缺失\"：{ghostReason}）");

            // ---- 5c. 反例：重复卡拒绝（README 铁律：卡组构成不重复） ----
            var withDup = new MsgDeckSubmit
            {
                DeckName = "重复卡组",
                CardIds = deckIds.Concat(new[] { deckIds[0] }).ToArray(),
                Digest = digest,
            };
            Assert(!NetMatchHandshake.ValidateDeckSubmit(withDup, out var dupReason)
                   && dupReason.Contains("重复"),
                $"重复卡提交被拒（reason 含\"重复\"：{dupReason}）");

            // ---- 5d. 反例：闭包原子行摘要漂移 / 缺失 ----
            var driftSubmit = new MsgDeckSubmit
            {
                DeckName = "摘要漂移",
                CardIds = deckIds,
                Digest = new NetDeckDigest { AtomicRowsHash = "00000000", AtomicRowCount = digest.AtomicRowCount },
            };
            Assert(!NetMatchHandshake.ValidateDeckSubmit(driftSubmit, out var driftReason)
                   && driftReason.Contains("原子行摘要不一致"),
                $"原子行摘要漂移被拒（{driftReason}）");

            var noDigestSubmit = new MsgDeckSubmit { DeckName = "无摘要", CardIds = deckIds, Digest = null };
            Assert(!NetMatchHandshake.ValidateDeckSubmit(noDigestSubmit, out var noDigestReason)
                   && noDigestReason.Contains("未随提交携带"),
                $"缺摘要提交被拒（{noDigestReason}）");

            // ---- 5e. 反例：客户端侧——服务器摘要漂移 / 卡组回显不符 ----
            var tamperedManifest = NetMatchHandshake.BuildMatchManifest(0, deckIds, deckIds.Length);
            tamperedManifest.OwnDeckDigest = new NetDeckDigest
            {
                AtomicRowsHash = "FFFFFFFF",
                AtomicRowCount = digest.AtomicRowCount,
            };
            Assert(!NetMatchHandshake.VerifyMatchManifest(tamperedManifest, deckIds, out var clientDrift)
                   && clientDrift.Contains("原子行摘要不一致"),
                $"客户端拒绝摘要漂移的服务器（{clientDrift}）");

            var echoTampered = NetMatchHandshake.BuildMatchManifest(0,
                deckIds.Reverse().ToArray(), deckIds.Length);
            Assert(!NetMatchHandshake.VerifyMatchManifest(echoTampered, deckIds, out var echoReason)
                   && echoReason.Contains("回显"),
                $"客户端拒绝卡组回显不符（{echoReason}）");
        }

        // ============================================================
        // 第六段：真实 socket 会话（M2 冒烟，2026-09-23）：
        // 进程内起 NetSessionServer（真实 TCP）+ 双玩家客户端 + 观战客户端——
        // 房间分座/抢座拒绝 → DeckSubmit→MatchManifest 握手 → intent 拒绝 Error 帧 →
        // 脚本化整局（SkipStandby/出牌/放地/让行/EndTurn，回合上限后认输兜底）→
        // 隐藏过滤（玩家不见对方抽的牌）/观战全信息/快照往返 全链路断言。
        // ============================================================

        private static void RunSocketSection()
        {
            Section("6. 真实 socket 会话（M2 冒烟）");

            var pool = CardCatalog.LoadAll();
            Assert(pool.Count >= 2, $"卡池 ≥2 张（socket 段依赖真实用户数据，实际 {pool.Count}）");
            if (pool.Count < 2) return;

            var deckIds = pool.Select(c => c.ID).Distinct().Take(30).ToArray();
            var submit = new MsgDeckSubmit
            {
                DeckName = "socket冒烟",
                CardIds = deckIds,
                Digest = NetMatchHandshake.ComputeDeckDigest(deckIds),
            };

            var server = new NetSessionServer("verify", null);
            server.Start(0); // OS 分配空闲口
            try
            {
                var a = new SocketTestClient(); a.Connect(server.Port);
                var b = new SocketTestClient(); b.Connect(server.Port);
                var spec = new SocketTestClient(); spec.Connect(server.Port);

                // ---- 6a. 分座：A 指定 0 / 抢座反例 / B 指定 1 / 观战 ----
                a.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify", WantSeat = 0, Nickname = "A" });
                PumpAndReceive(server, 300, a);
                Assert(a.LastRoomState != null
                    && a.LastRoomState.Phase == (int)NetRoomPhase.Waiting
                    && a.LastRoomState.Players[0].Connected && a.LastRoomState.Players[0].Nickname == "A",
                    "A 分到椅子 0（RoomState=Waiting）");

                var rogue = new SocketTestClient(); rogue.Connect(server.Port);
                rogue.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify", WantSeat = 0, Nickname = "抢座" });
                PumpAndReceive(server, 300, rogue);
                Assert(rogue.Errors.Any(e => e.Context == "JoinRoom" && e.Reason.Contains("占用")),
                    "抢已占座位被拒（Error 帧 Context=JoinRoom）");
                rogue.Close();

                b.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify", WantSeat = 1, Nickname = "B" });
                spec.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify", AsSpectator = true, Nickname = "S" });
                PumpAndReceive(server, 300, a, b, spec);
                Assert(a.LastRoomState != null && a.LastRoomState.Phase == (int)NetRoomPhase.DeckSubmit
                    && a.LastRoomState.Players[1].Connected && a.LastRoomState.SpectatorCount == 1,
                    "双玩家位满 → DeckSubmit（观战 1 人）");

                // ---- 6b. 握手（真实 socket 驱动 §11 消息流）----
                a.Send(NetworkMessageType.DeckSubmit, submit);
                b.Send(NetworkMessageType.DeckSubmit, submit);
                for (int i = 0; i < 100 && (a.Manifest == null || b.Manifest == null); i++)
                    PumpAndReceive(server, 30, a, b, spec);
                Assert(a.Manifest != null && b.Manifest != null, "双座位收到 MatchManifest");
                if (a.Manifest == null || b.Manifest == null) return;

                Assert(a.Manifest.OwnSeat + b.Manifest.OwnSeat == 1, "引擎座位互补（换先手映射）");
                int firstSeat = a.LastRoomState?.FirstSeatThisMatch ?? -1;
                Assert(firstSeat == 0 || firstSeat == 1, $"RoomState 广播本局先手椅位（{firstSeat}）");
                Assert(a.Manifest.OwnSeat == (firstSeat == 0 ? 0 : 1)
                    && b.Manifest.OwnSeat == (firstSeat == 0 ? 1 : 0),
                    "椅位→引擎座位映射与先手一致（A=椅0）");
                a.MyEngineSeat = a.Manifest.OwnSeat;
                b.MyEngineSeat = b.Manifest.OwnSeat;
                Assert(NetMatchHandshake.VerifyMatchManifest(a.Manifest, deckIds, out var reasonA),
                    $"A 客户端侧清单校验通过{(reasonA != null ? "（" + reasonA + "）" : "")}");
                Assert(NetMatchHandshake.VerifyMatchManifest(b.Manifest, deckIds, out _),
                    "B 客户端侧清单校验通过");

                // ---- 6c. 拒绝语义：必失败 intent → Error 帧回发（§5 M2 落地）----
                a.Send(NetworkMessageType.IntentPlayCard,
                    new MsgIntentPlayCard { CardRuntimeId = 999999, FromZone = (int)Zone.Hand });
                PumpAndReceive(server, 300, a, b, spec);
                Assert(a.Errors.Any(e => e.Context == "IntentPlayCard"),
                    "非法 intent 被 Error 帧拒绝（RuntimeId 不存在）");

                // ---- 6d. 整局脚本（双客户端 intent 驱动；回合上限后认输兜底）----
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool conceded = false;
                while (sw.ElapsedMilliseconds < 90000)
                {
                    server.Pump();
                    a.Receive(5); b.Receive(5); spec.Receive(5);

                    if (a.Events.Any(e => e.EventType == nameof(GameOverEvent))) break;

                    var snap = a.LastSnapshot ?? b.LastSnapshot;
                    if (!conceded && snap != null && snap.CurrentTurn > 30)
                    {
                        conceded = true;
                        a.Send<object>(NetworkMessageType.IntentConcede, null);
                        continue;
                    }

                    ScriptClient(a);
                    ScriptClient(b);
                }

                for (int i = 0; i < 50; i++) // 终局冲刷
                {
                    server.Pump();
                    a.Receive(10); b.Receive(10); spec.Receive(10);
                    if (a.LastRoomState?.Phase == (int)NetRoomPhase.Finished
                        && b.LastRoomState?.Phase == (int)NetRoomPhase.Finished
                        && spec.LastRoomState?.Phase == (int)NetRoomPhase.Finished)
                        break;
                }

                Assert(a.Events.Any(e => e.EventType == nameof(GameOverEvent)), "整局到达终局（GameOverEvent 到达客户端）");
                Assert(a.LastRoomState?.Phase == (int)NetRoomPhase.Finished
                    && b.LastRoomState?.Phase == (int)NetRoomPhase.Finished
                    && spec.LastRoomState?.Phase == (int)NetRoomPhase.Finished,
                    "三方 RoomState(Finished)（正常终局/断线作废统一收口）");
                Assert(!a.Disconnected && !b.Disconnected && !spec.Disconnected, "全程无意外断线");

                // ---- 6e. 回合推进与快照 ----
                Assert(a.SnapshotsReceived >= 2 && b.SnapshotsReceived >= 2, "双客户端持续收到快照（稳定决策点取样）");
                int lastTurn = Math.Max(a.LastSnapshot?.CurrentTurn ?? 0, b.LastSnapshot?.CurrentTurn ?? 0);
                Assert(lastTurn >= 3, $"回合推进 ≥3（实际 {lastTurn}——SkipStandby/EndTurn/折返链路通）");

                // ---- 6f. 隐藏过滤（玩家侧）与观战全信息（2026-09-23 定案）----
                Assert(a.Events.Count > 0 && spec.Events.Count > 0, "事件流到达（玩家+观战）");
                var aOppDraws = a.Events.Where(e => e.EventType == nameof(CardDrawEvent))
                    .Where(e => SeatOfParam(e, "Player") == 1 - a.MyEngineSeat).ToList();
                Assert(aOppDraws.Count > 0, "A 的事件流含对手抽牌事件（抽牌发生本身可见）");
                Assert(aOppDraws.All(e => HiddenOrAbsent(e, "DrawnCard")),
                    "A 的对手抽牌事件不携带 DrawnCard 引用（服务器出队按座位过滤）");
                var aSelfDraws = a.Events.Where(e => e.EventType == nameof(CardDrawEvent))
                    .Where(e => SeatOfParam(e, "Player") == a.MyEngineSeat).ToList();
                Assert(aSelfDraws.Count > 0 && aSelfDraws.All(e => !HiddenOrAbsent(e, "DrawnCard")),
                    "A 的自己抽牌事件携带 DrawnCard（己方手牌可见）");

                var specDraws = spec.Events.Where(e => e.EventType == nameof(CardDrawEvent)).ToList();
                Assert(specDraws.Count > 0 && specDraws.Any(e => !HiddenOrAbsent(e, "DrawnCard")),
                    "观战全信息：抽牌事件携带 DrawnCard");

                var specSnap = spec.FirstSnapshot ?? spec.LastSnapshot;
                Assert(specSnap != null && specSnap.Hands.All(h => h.Count == 0
                    || (h.OwnRuntimeIds != null && h.OwnRuntimeIds.Length == h.Count)),
                    "观战快照双方手牌全展开（OwnRuntimeIds 满员）");
                Assert(specSnap != null && specSnap.ZoneCards.Count(z => (Zone)z.Zone == Zone.Hand) == 2,
                    "观战快照含双方手牌区（fullInfo）");
                var aLast = a.LastSnapshot;
                Assert(aLast != null
                    && (aLast.Hands.First(h => h.Seat == 1 - a.MyEngineSeat).OwnRuntimeIds?.Length ?? 0) == 0,
                    "玩家快照对方手牌只见数量");

                Debug.Log($"[NetVerify] socket 冒烟：事件 A×{a.Events.Count}/B×{b.Events.Count}/观战×{spec.Events.Count}，" +
                    $"快照 A×{a.SnapshotsReceived}/B×{b.SnapshotsReceived}，Error A×{a.Errors.Count}/B×{b.Errors.Count}，" +
                    $"终局回合 {lastTurn}（先手椅位 {firstSeat}）");

                a.Close(); b.Close(); spec.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        /// <summary>窗口内循环泵 + 客户端收下行（覆盖"字节还在路上"的时序窗口——
        /// 单拍泵会赶在读线程入队前空转，断言随机失败）。</summary>
        private static void PumpAndReceive(NetSessionServer server, int totalMs, params SocketTestClient[] clients)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(totalMs);
            while (DateTime.UtcNow < deadline)
            {
                server.Pump();
                foreach (var c in clients)
                    c.Receive(10);
            }
        }

        /// <summary>
        /// 脚本客户端决策（黑盒：只依据自己收到的快照/反问）：
        /// 反问优先应答 → 栈上让行（PrioritySeat+StackV2）→ Standby 跳过 → Main 尝试出牌/放地后 EndTurn。
        /// 非法尝试被服务器拒绝即收 Error 帧——拒绝面本身是验证点，脚本只保证回合必然流动。
        /// </summary>
        private static void ScriptClient(SocketTestClient c)
        {
            if (c.PendingSelects.Count > 0)
            {
                var req = c.PendingSelects[0];
                c.PendingSelects.RemoveAt(0);
                int poolCount = req.Candidates != null && req.Candidates.Length > 0
                    ? req.Candidates.Length
                    : (req.Labels?.Length ?? 0);
                int count = Math.Max(1, Math.Min(req.Min, poolCount));
                c.Send(NetworkMessageType.SelectResponse, new MsgSelectResponse
                {
                    RequestId = req.RequestId,
                    Indices = Enumerable.Range(0, count).ToArray(),
                });
                return;
            }

            var snap = c.LastSnapshot;
            if (snap == null || snap.ActiveSeat != c.MyEngineSeat) return;

            // 栈上有对象且优先权在我 → 让行（响应窗口放行；要响应的客户端走 PlayCardInResponse 同一消息位）
            if (snap.PrioritySeat == c.MyEngineSeat && snap.StackV2 != null && snap.StackV2.Length > 0)
            {
                c.Send<object>(NetworkMessageType.IntentPassPriority, null);
                return;
            }

            var phase = (PhaseType)snap.CurrentPhase;
            if (phase == PhaseType.Standby)
            {
                c.Send<object>(NetworkMessageType.IntentSkipStandby, null);
                return;
            }
            if (phase == PhaseType.Main)
            {
                if (c.LastActedTurn < snap.CurrentTurn)
                {
                    c.LastActedTurn = snap.CurrentTurn;
                    var handIds = snap.Hands.FirstOrDefault(h => h.Seat == c.MyEngineSeat)?.OwnRuntimeIds;
                    if (handIds != null && handIds.Length > 0)
                    {
                        c.Send(NetworkMessageType.IntentPlayCard, new MsgIntentPlayCard
                        {
                            CardRuntimeId = handIds[0],
                            FromZone = (int)Zone.Hand,
                            ModeIndex = 0,
                        });
                        c.Send(NetworkMessageType.IntentAddToElementPool,
                            new MsgIntentAddToElementPool { CardRuntimeId = handIds[handIds.Length - 1] });
                    }
                    return; // 本帧动作上栈，等快照同步后再 EndTurn
                }
                c.Send<object>(NetworkMessageType.IntentEndTurn, null);
            }
        }

        /// <summary>取事件实体字段的座位（仅玩家引用有座位；其余 null）。</summary>
        private static int? SeatOfParam(NetEvent e, string fieldName)
        {
            var p = e.Params?.FirstOrDefault(x => x.FieldName == fieldName);
            if (p == null || p.Kind != NetParamKind.Entity || p.EntityRefs == null || p.EntityRefs.Length == 0)
                return null;
            var r = p.EntityRefs[0];
            return r != null && r.IsPlayer ? r.Seat : (int?)null;
        }

        /// <summary>字段被隐藏过滤（整体丢弃）或被置 Null（字段在、值隐）。</summary>
        private static bool HiddenOrAbsent(NetEvent e, string fieldName)
        {
            var p = e.Params?.FirstOrDefault(x => x.FieldName == fieldName);
            if (p == null) return true;
            return p.Kind == NetParamKind.Null;
        }

        /// <summary>
        /// socket 测试客户端（黑盒）：真实 TCP + FrameCodec，收到的下行按类型分派到公开字段。
        /// 只用于验证器——正式客户端 UI 消费侧是 M3 范畴。可选挂 NetClientBrain（M3 对拍），
        /// 下行自动喂入大脑。
        /// </summary>
        private sealed class SocketTestClient : IBrainChannel
        {
            private readonly TcpClient _tcp = new TcpClient();
            private NetworkStream _stream;
            private readonly FrameCodec.FrameDecoder _decoder = new FrameCodec.FrameDecoder();
            private readonly byte[] _buf = new byte[16 * 1024];

            public NetClientBrain Brain;

            public int MyEngineSeat = -1;
            public int LastActedTurn;
            public bool Disconnected;
            public MsgRoomState LastRoomState;
            public MsgMatchManifest Manifest;
            public MsgGameStateSync LastSnapshot;
            public MsgGameStateSync FirstSnapshot;
            public int SnapshotsReceived;
            public readonly List<NetEvent> Events = new List<NetEvent>();
            public readonly List<MsgSelectRequest> PendingSelects = new List<MsgSelectRequest>();
            public readonly List<MsgError> Errors = new List<MsgError>();

            public void Connect(int port)
            {
                _tcp.Connect("127.0.0.1", port);
                _stream = _tcp.GetStream();
            }

            public void Send<T>(NetworkMessageType type, T payload) where T : class
            {
                if (Disconnected || _stream == null) return;
                try
                {
                    var frame = FrameCodec.Frame(NetworkSerializer.BuildEnvelope(type, payload));
                    _stream.Write(frame, 0, frame.Length);
                    _stream.Flush();
                }
                catch (Exception) { Disconnected = true; }
            }

            /// <summary>收一段时间（有数据续期），解码分派全部下行。</summary>
            public void Receive(int timeoutMs)
            {
                if (Disconnected || _stream == null) return;
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    if (!_stream.DataAvailable) { Thread.Sleep(2); continue; }
                    int n;
                    try { n = _stream.Read(_buf, 0, _buf.Length); }
                    catch (Exception) { Disconnected = true; return; }
                    if (n <= 0) { Disconnected = true; return; }
                    _decoder.Append(_buf, 0, n);
                    while (_decoder.TryDecode(out var msg))
                        Dispatch(msg);
                    deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                }
            }

            private void Dispatch(NetworkMessage msg)
            {
                switch (msg.Type)
                {
                    case NetworkMessageType.RoomState:
                        LastRoomState = NetworkSerializer.DeserializePayload<MsgRoomState>(msg);
                        if (Brain != null) Brain.RoomState = LastRoomState;
                        break;
                    case NetworkMessageType.MatchManifest:
                        Manifest = NetworkSerializer.DeserializePayload<MsgMatchManifest>(msg);
                        if (Brain != null) Brain.MyEngineSeat = Manifest.OwnSeat;
                        break;
                    case NetworkMessageType.GameStateSyncV2:
                        LastSnapshot = NetworkSerializer.DeserializePayload<MsgGameStateSync>(msg);
                        if (FirstSnapshot == null) FirstSnapshot = LastSnapshot;
                        SnapshotsReceived++;
                        Brain?.OnSnapshot(LastSnapshot);
                        break;
                    case NetworkMessageType.NetEventBatch:
                        var batch = NetworkSerializer.DeserializePayload<MsgNetEventBatch>(msg);
                        if (batch?.Events != null) Events.AddRange(batch.Events);
                        break;
                    case NetworkMessageType.SelectRequest:
                        var request = NetworkSerializer.DeserializePayload<MsgSelectRequest>(msg);
                        PendingSelects.Add(request);
                        Brain?.PendingSelects.Add(request);
                        break;
                    case NetworkMessageType.Error:
                        var error = NetworkSerializer.DeserializePayload<MsgError>(msg);
                        Errors.Add(error);
                        if (Brain != null) Brain.ErrorsSeen++;
                        break;
                }
            }

            public void Close()
            {
                Disconnected = true;
                try { _tcp.Close(); } catch (Exception) { }
            }
        }

        // ============================================================
        // 第七段：M3 对拍（2026-09-23）——双 headless 客户端（NetClientBrain）经真实 TCP
        // 打完整局；同种子两局：服务器事件流逐值相等 + 终局快照字节相等（"本地直连"口径=
        // 同种子重复局：同一大脑+同一服务器栈，传输不扰动即等价）；换种子第三局：流必须不同
        // （种子敏感性，防假对拍）。对拍前置：Entity.RuntimeId / GameEventBase.EventId 计数器
        // 局边界归零（否则跨局自增漂移无法逐值比）+ NetEntityDirectory 缓存清空。
        // ============================================================

        private sealed class M3RunRecord
        {
            public NetEvent[] Events;
            public byte[] SnapshotSeat0;
            public byte[] SnapshotSeat1;
            public int FinalTurn;
            public int FirstSeat;
            public bool ReachedGameOver;
            public bool ClientDisconnected;
            public int SnapshotCount;
        }

        private static void RunM3Section()
        {
            Section("7. M3 对拍（同种子双整局一致 + 种子敏感）");

            var pool = CardCatalog.LoadAll();
            Assert(pool.Count >= 2, $"卡池 ≥2（M3 段依赖真实用户数据，实际 {pool.Count}）");
            if (pool.Count < 2) return;

            var deckIds = pool.Select(c => c.ID).Distinct().Take(30).ToArray();
            var submit = new MsgDeckSubmit
            {
                DeckName = "m3对拍",
                CardIds = deckIds,
                Digest = NetMatchHandshake.ComputeDeckDigest(deckIds),
            };

            var run1 = RunM3NetworkGame(777, submit);
            var run2 = RunM3NetworkGame(777, submit);
            var run3 = RunM3NetworkGame(888, submit);

            // ---- 整局性 ----
            Assert(run1.ReachedGameOver && run2.ReachedGameOver && run3.ReachedGameOver,
                "三局都到达终局（GameOverEvent 到达客户端）");
            Assert(!run1.ClientDisconnected && !run2.ClientDisconnected && !run3.ClientDisconnected,
                "三局全程无意外断线");
            Assert(run1.SnapshotCount > 10 && run2.SnapshotCount > 10, "客户端持续收到快照（锁步驱动）");

            // ---- 同种子对拍：事件流逐值相等 ----
            Assert(run1.Events.Length == run2.Events.Length && run1.Events.Length > 0,
                $"同种子两局事件总量相等且非空（{run1.Events.Length} vs {run2.Events.Length}）");
            int mismatchAt = -1;
            for (int i = 0; i < Math.Min(run1.Events.Length, run2.Events.Length); i++)
            {
                if (!EqualEvent(run1.Events[i], run2.Events[i])) { mismatchAt = i; break; }
            }
            Assert(mismatchAt < 0,
                $"同种子两局事件流逐值相等（首个差异 @{mismatchAt}：{run1.Events[Math.Max(0, mismatchAt)]?.EventType}）");
            Assert(run1.SnapshotSeat0.SequenceEqual(run2.SnapshotSeat0)
                && run1.SnapshotSeat1.SequenceEqual(run2.SnapshotSeat1),
                "同种子两局终局快照字节相等（双视角）");
            Assert(run1.FinalTurn == run2.FinalTurn && run1.FirstSeat == run2.FirstSeat,
                $"同种子两局终局回合/先手一致（{run1.FinalTurn}/{run1.FirstSeat} vs {run2.FinalTurn}/{run2.FirstSeat}）");

            // ---- 种子敏感性：换种子流必须不同 ----
            bool seedDiverged = run1.Events.Length != run3.Events.Length
                || !Enumerable.Range(0, Math.Min(run1.Events.Length, run3.Events.Length))
                    .All(i => EqualEvent(run1.Events[i], run3.Events[i]));
            Assert(seedDiverged, "换种子事件流不同（种子真实参与洗牌/先手——防假对拍）");

            Debug.Log($"[NetVerify] M3 对拍：seed777 双局 {run1.Events.Length} 事件全等" +
                $"（终局回合 {run1.FinalTurn}，先手椅位 {run1.FirstSeat}，快照 {run1.SnapshotSeat0.Length}B）；" +
                $"seed888 局 {run3.Events.Length} 事件（已发散）");
        }

        /// <summary>跑一局种子钉死的网络整局（双 brain 客户端 + 真实 TCP），采集服务器侧对拍记录。</summary>
        private static M3RunRecord RunM3NetworkGame(int seed, MsgDeckSubmit submit)
        {
            // 对拍前置：身份计数器归零（两局各自从 1 起）+ 解析缓存清空（防旧 RuntimeId 串号）
            Entity.ResetRuntimeIdCounterForVerification();
            GameEventBase.ResetEventIdCounterForVerification();
            NetEntityDirectory.Clear();

            var record = new M3RunRecord();
            var server = new NetSessionServer("m3", null, seed);
            server.Start(0);
            try
            {
                var a = new SocketTestClient { Brain = new NetClientBrain() };
                var b = new SocketTestClient { Brain = new NetClientBrain() };
                a.Connect(server.Port);
                b.Connect(server.Port);

                // 进房（先双方就座 → DeckSubmit 阶段，再提交——防提交赶在阶段迁移前被拒）
                a.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "m3", WantSeat = 0, Nickname = "A" });
                b.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "m3", WantSeat = 1, Nickname = "B" });
                for (int i = 0; i < 200 && !(a.LastRoomState?.Phase == (int)NetRoomPhase.DeckSubmit
                    && b.LastRoomState?.Phase == (int)NetRoomPhase.DeckSubmit); i++)
                {
                    server.Pump();
                    a.Receive(10); b.Receive(10);
                }

                a.Send(NetworkMessageType.DeckSubmit, submit);
                b.Send(NetworkMessageType.DeckSubmit, submit);
                for (int i = 0; i < 300 && (a.Manifest == null || b.Manifest == null); i++)
                {
                    server.Pump();
                    a.Receive(10); b.Receive(10);
                }

                // 锁步整局：每 tick 服务器泵 → 客户端收 → 大脑决策（修订锁步——行动是下行序列的纯函数）
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 120000)
                {
                    server.Pump();
                    a.Receive(5); b.Receive(5);
                    if (a.Events.Any(e => e.EventType == nameof(GameOverEvent))) break;
                    a.Brain.Think(a, turnConcedeCap: 40);
                    b.Brain.Think(b, turnConcedeCap: 40);
                }

                // 终局冲刷（事件/快照/RoomState 收尾）
                for (int i = 0; i < 60; i++)
                {
                    server.Pump();
                    a.Receive(10); b.Receive(10);
                    if (a.LastRoomState?.Phase == (int)NetRoomPhase.Finished
                        && b.LastRoomState?.Phase == (int)NetRoomPhase.Finished)
                        break;
                }

                record.ReachedGameOver = a.Events.Any(e => e.EventType == nameof(GameOverEvent));
                record.ClientDisconnected = a.Disconnected || b.Disconnected;
                record.FinalTurn = a.LastSnapshot?.CurrentTurn ?? -1;
                record.FirstSeat = a.LastRoomState?.FirstSeatThisMatch ?? -1;
                record.SnapshotCount = a.SnapshotsReceived;

                // 服务器侧采集（终局冲刷后、下一局重置前）：本局事件流 + 双视角终局快照字节
                record.Events = NetEventProjector.Events.ToArray();
                var core = GameCore.Instance;
                record.SnapshotSeat0 = MemoryPackSerializer.Serialize(NetSnapshotBuilder.Build(core, 0));
                record.SnapshotSeat1 = MemoryPackSerializer.Serialize(NetSnapshotBuilder.Build(core, 1));

                a.Close(); b.Close();
            }
            finally
            {
                server.Stop();
            }
            return record;
        }

        /// <summary>单消息全链路往返：DTO → 信封 → 帧 → 解码 → 反序列化（握手段专用）。</summary>
        private static T RoundTripMessage<T>(NetworkMessageType type, T msg) where T : class
        {
            var wire = NetworkSerializer.SerializeMessage(type, msg);
            var envelope = NetworkSerializer.DeserializeEnvelope(wire);
            var frame = FrameCodec.Frame(envelope);
            var decoder = new FrameCodec.FrameDecoder();
            decoder.Append(frame);
            if (!decoder.TryDecode(out var decoded))
                throw new InvalidOperationException("握手帧解码失败");
            return NetworkSerializer.DeserializePayload<T>(decoded);
        }

        /// <summary>
        /// 回环选择器：SelectAsync 内 MsgSelectRequest → 信封 → 帧 → 解码（去程），
        /// 模拟客户端选索引，MsgSelectResponse → 信封 → 帧 → 解码（回程）——全同步。
        /// </summary>
        private sealed class LoopbackTargetSelector : ITargetSelectorEx
        {
            public readonly List<MsgSelectRequest> Seen = new List<MsgSelectRequest>();

            public void Attach() => TargetSelectionService.Current = this;

            public void Detach()
            {
                if (ReferenceEquals(TargetSelectionService.Current, this))
                    TargetSelectionService.Current = null;
            }

            public UniTask<List<int>> SelectAsync(TargetSelectionRequest request, IReadOnlyList<string> labels)
            {
                var msg = new MsgSelectRequest
                {
                    RequestId = Seen.Count + 1,
                    ChooserSeat = NetEntityMapper.SeatOf(request.Chooser),
                    Title = request.Title,
                    Hint = request.Hint,
                    AllowCancel = request.AllowCancel,
                    TimeoutSeconds = request.TimeoutSeconds,
                    Min = request.MinCount,
                    Max = request.MaxCount,
                    // 与 NetworkTargetSelector 同口径（2026-09-22 线上去文本）：实体反问不传 Labels
                    Labels = request.Candidates.Count > 0 ? Array.Empty<string>() : labels.ToArray(),
                    Candidates = request.Candidates.Select(NetEntityMapper.FromEntity).ToArray(),
                };

                // 去程：请求经完整协议栈到达"客户端"
                var wire = NetworkSerializer.SerializeMessage(NetworkMessageType.SelectRequest, msg);
                var decodedRequest = NetworkSerializer.DeserializePayload<MsgSelectRequest>(
                    NetworkSerializer.DeserializeEnvelope(wire));
                Seen.Add(decodedRequest);

                // 模拟客户端：选前 Min 个索引（AutoSelect 同款策略）——
                // 实体反问 Labels 为空，候选数以 Candidates 为准（卡名客户端按 CardId 查表渲染）
                int pool = decodedRequest.Candidates.Length > 0 ? decodedRequest.Candidates.Length : decodedRequest.Labels.Length;
                int count = Math.Max(1, Math.Min(decodedRequest.Min, pool));
                var indices = Enumerable.Range(0, count).ToArray();

                // 回程：应答经完整协议栈返回"服务器"
                var resp = new MsgSelectResponse { RequestId = decodedRequest.RequestId, Indices = indices };
                var respWire = NetworkSerializer.SerializeMessage(NetworkMessageType.SelectResponse, resp);
                var decodedResponse = NetworkSerializer.DeserializePayload<MsgSelectResponse>(
                    NetworkSerializer.DeserializeEnvelope(respWire));

                return UniTask.FromResult(decodedResponse.Indices.ToList());
            }

            public UniTask<int> SelectOneAsync(Player chooser, IReadOnlyList<string> options, string title)
                => UniTask.FromResult(0);

            public UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
                int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
                => throw new NotSupportedException("回环选择器只经 Ex 路径消费");
        }
    }
}
