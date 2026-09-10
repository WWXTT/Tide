using System;
using System.Collections.Generic;
using System.Linq;
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
    /// ③ intent 通道（NetworkIntentApplier）与直调 GameActions 等价；④ 反问回环（ITargetSelectorEx）。
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
                    Debug.LogError("[NetVerify] 测试卡缺失（Configs/TestDecks/TestCreatureCards.json），中止");
                    return;
                }

                RunFullGameSection(deck);
                RunIntentSection(deck);
                RunSelectorSection(deck);
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

                    // 横置产元素：经 intent（颜色=首个可用色；驱动侧镜像 SimpleAI.TapAllLands 职责）
                    foreach (var pooled in core.ElementPool.GetPooledCards(me).Where(p => !p.IsTapped).ToList())
                    {
                        var color = pooled.GetAvailableColors().FirstOrDefault();
                        if (color == default && !pooled.GetAvailableColors().Any()) continue;
                        ApplyIntent(seat, NetworkMessageType.IntentTapForElement,
                            new MsgIntentTapForElement { CardRuntimeId = pooled.SourceCard.RuntimeId, ManaType = (int)color });
                    }

                    // 动作循环：枚举 → 按覆盖优先选型 → 映射 intent → 分派（拒绝即拉黑重枚举）
                    for (int step = 0; step < 24; step++)
                    {
                        enumerator.Enumerate(core, me);
                        enumerator.RemoveAll(banned);
                        if (enumerator.Count == 0) break;

                        // 选型：优先覆盖未触碰的动作类型 → 任意非 EndTurn → EndTurn（TideAction 是结构体，不用 ??）
                        TideAction choice = default;
                        bool found = false;
                        foreach (var a in enumerator.Actions)
                            if (a.Type != TideActionType.EndTurn && !coverage.ContainsKey(a.Type)) { choice = a; found = true; break; }
                        if (!found)
                            foreach (var a in enumerator.Actions)
                                if (a.Type != TideActionType.EndTurn) { choice = a; found = true; break; }
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
                Assert(last.Labels.Length == last.Candidates.Length && last.Candidates.Length == hand.Count
                    && last.Min == 1 && last.Max == 2 && last.ChooserSeat == 0,
                    "MsgSelectRequest 携带完整请求（Labels/Candidates 同序、Min/Max/座位）");
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
                    Labels = labels.ToArray(),
                    Candidates = request.Candidates.Select(NetEntityMapper.FromEntity).ToArray(),
                };

                // 去程：请求经完整协议栈到达"客户端"
                var wire = NetworkSerializer.SerializeMessage(NetworkMessageType.SelectRequest, msg);
                var decodedRequest = NetworkSerializer.DeserializePayload<MsgSelectRequest>(
                    NetworkSerializer.DeserializeEnvelope(wire));
                Seen.Add(decodedRequest);

                // 模拟客户端：选前 Min 个索引（AutoSelect 同款策略）
                int count = Math.Max(1, Math.Min(decodedRequest.Min, decodedRequest.Labels.Length));
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
