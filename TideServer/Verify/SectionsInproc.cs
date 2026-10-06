using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI.NeuralEnv;
using CardCore.Network;
using MemoryPack;
using SynergyUI;

namespace TideServer.Verify
{
    /// <summary>
    /// 进程内验证段（V1/V2/V3/V5/V6/V7）——断言逐条对标 Unity NetProtocolLoopbackVerifier
    /// （Assets/Editor/验证/NetProtocolLoopbackVerifier.cs），纯 .NET 控制台复刻。
    /// 整局驱动 = 进程内 NetSessionServer + 双 NetClientBrain（M3 骨架）——Unity 版第一段的
    /// AiBattleDriver（Editor 程序集）不参与，网络栈覆盖面反而更全。
    /// </summary>
    internal static class SectionInproc
    {
        private static List<CardData> _deck;         // 非仪式卡池（V2/V3 直调用）
        private static string[] _deckIds;            // 前 30 张 ID（V6/V7 网络局）

        public static void RunProjection(List<CardData> deck) { _deck = deck; RunProjectionSection(); }
        public static void RunIntent(List<CardData> deck) { _deck = deck; RunIntentSection(); }
        public static void RunSelector(List<CardData> deck) { _deck = deck; RunSelectorSection(); }
        public static void RunHandshake() => RunHandshakeSection();
        public static void RunSocket() => RunSocketSection();
        public static void RunDeterminism(List<CardData> deck)
        {
            _deck = deck;
            // 预热（2026-09-27 服务器版特有）：GameCore 单例/组合根的一次性惰性初始化会创建实体
            //（首局 RuntimeId 整体偏移）——Unity 版 M3 靠前置五段隐式热身，独立跑本段必须显式预热，
            // 否则首局与后两局前置状态不对称，同种子对拍必炸。
            GameCore.Instance.InitGame(CardLoader.BuildDeck(_deck, 1), CardLoader.BuildDeck(_deck, 1), 1);
            RunM3Section();
        }

        private static string[] DeckIds()
        {
            return _deckIds ??= CardCatalog.LoadAll().Select(c => c.ID).Distinct().Take(30).ToArray();
        }

        // ============================================================
        // 第一段：整局事件流 + 快照（网络整局驱动：进程内 NetSessionServer + 双 brain）
        // ============================================================

        private static void RunProjectionSection()
        {
            var core = GameCore.Instance;
            MsgGameStateSync midStackSnapshot = null;

            void OnStackAdd(StackAddEvent e)
            {
                // 中段采样：首个整卡施放上栈（双 Pass 结算前——栈非空快照的直接回归点）
                if (midStackSnapshot == null && e.AddedObject is EffectInstance inst && inst.IsCardCast)
                    midStackSnapshot = NetSnapshotBuilder.Build(core, 0);
            }

            EventManager.Instance.Subscribe<StackAddEvent>(OnStackAdd);
            GameRunRecord result;
            try
            {
                result = RunNetworkGame(20260927, BuildSubmit("投影整局"));
            }
            finally
            {
                EventManager.Instance.Unsubscribe<StackAddEvent>(OnStackAdd);
            }

            VerifySuite.Assert(result.ReachedGameOver, "对局应自然到达终局（GameOverEvent 到达客户端）");
            VerifySuite.Assert(!result.ClientDisconnected, "对局过程无断线");

            // ---- 1a. 投影完整性：与 MatchLogService（另一个 AnyPublished 消费者）必然同量 ----
            var events = NetEventProjector.Events;
            VerifySuite.Assert(events.Count == MatchLogService.Entries.Count,
                $"投影量 {events.Count} == 日志量 {MatchLogService.Entries.Count}（AnyPublished 双消费者同量）");
            VerifySuite.Assert(NetEventProjector.ErrorCount == 0, $"投影零异常（ErrorCount={NetEventProjector.ErrorCount}）");
            VerifySuite.Assert(!NetEventProjector.WasTruncated, "未触顶缓冲上限");
            for (int i = 1; i < events.Count; i++)
            {
                if (events[i].EventId <= events[i - 1].EventId)
                {
                    VerifySuite.Assert(false, $"EventId 应严格递增：[{i - 1}]={events[i - 1].EventId} → [{i}]={events[i].EventId}");
                    break;
                }
            }
            VerifySuite.Assert(true, "EventId 严格递增（AnyPublished 恰一次保序）");
            if (events.Count == 0)
            {
                VerifySuite.Assert(false, "事件流为空——对局未产生事件，后续断言无法验证");
                return;
            }
            VerifySuite.Log($"事件总量 {events.Count}，类型 {events.Select(e => e.EventType).Distinct().Count()} 种");

            // ---- 1a-2. 线上去文本口径：执行摘要事件只传身份，拼好文本不进线格式 ----
            var summaryEvents = events.Where(e => e.EventType == nameof(EffectExecutionSummaryEvent)).ToList();
            if (summaryEvents.Count == 0)
            {
                // Effects.json 2026-10-04 起为用户清空态（真效果入库前预期——记忆 effects-json-reset-real-content，
                // V9.h 载荷候选同款软跳过口径）：卡池全无效果 → 全场无执行摘要事件属数据态而非回归；
                // 硬断言待真效果回库后自然恢复（本分支零覆盖即恢复为 Assert(false)）。
                VerifySuite.Log("V1 跳过：卡池无效果发动（Effects.json 清空态）——执行摘要去文本口径待真效果回库回归");
            }
            else
            {
                foreach (var se in summaryEvents)
                {
                    var names = se.Params.Select(p => p.FieldName).ToHashSet();
                    if (names.Contains("Description") || names.Contains("Instance"))
                    {
                        VerifySuite.Assert(false, $"执行摘要事件 {se.EventId} 携带 Description/Instance 参数（线上去文本回归）");
                        break;
                    }
                    if (!names.Contains("EffectId") || !names.Contains("Source") || !names.Contains("Controller"))
                    {
                        VerifySuite.Assert(false, $"执行摘要事件 {se.EventId} 缺身份参数 EffectId/Source/Controller");
                        break;
                    }
                }
                VerifySuite.Assert(true, $"执行摘要事件 ×{summaryEvents.Count}：只传 EffectId/Source/Controller，不传拼好文本");
            }

            // ---- 1b. 信封 + 帧无损：批次 64 与单条两口径，帧按三段切分（半包/粘包） ----
            var roundTripped = 0;
            for (int offset = 0; offset < events.Count; offset += 64)
            {
                var batch = new MsgNetEventBatch { Events = events.Skip(offset).Take(64).ToArray() };
                var decoded = VerifyCommon.RoundTripBatch(batch);
                VerifyCommon.AssertDeepEqual(batch.Events, decoded.Events, $"批次@{offset}");
                roundTripped += batch.Events.Length;
            }
            VerifySuite.Assert(roundTripped == events.Count, $"批次口径覆盖全部事件（{roundTripped}/{events.Count}）");

            var single = new MsgNetEventBatch { Events = new[] { events[events.Count / 2] } };
            VerifyCommon.AssertDeepEqual(single.Events, VerifyCommon.RoundTripBatch(single).Events, "单条口径");

            // ---- 1c. 终局快照对账（双视角） ----
            VerifyCommon.VerifySnapshot(core, 0);
            VerifyCommon.VerifySnapshot(core, 1);

            // ---- 1d. 中段栈快照（终局栈空测不到的直接回归点） ----
            if (midStackSnapshot != null)
            {
                var sampled = midStackSnapshot.StackV2;
                VerifySuite.Assert(sampled != null && sampled.Length >= 1, "中段快照含栈条目");
                var top = sampled[sampled.Length - 1];
                VerifySuite.Assert(top.IsCardCast, "采样栈顶为整卡施放");
                VerifySuite.Assert(top.Source != null && top.Source.RuntimeId != 0, "栈顶源卡引用有效");
                var rt = VerifyCommon.RoundTripSnapshot(midStackSnapshot);
                VerifySuite.Assert(rt.StackV2.Length == sampled.Length
                    && rt.StackV2[rt.StackV2.Length - 1].Source?.RuntimeId == top.Source?.RuntimeId
                    && rt.StackV2[rt.StackV2.Length - 1].ModeIndex == top.ModeIndex,
                    "中段快照（含栈）信封往返无损");
                VerifySuite.Log($"中段栈采样：栈深 {sampled.Length}，顶 mode={top.ModeIndex}");
            }
            else
            {
                VerifySuite.Assert(false, "中段栈采样缺失（整局未出现整卡施放上栈？）");
            }
        }

        // ============================================================
        // 第二段：intent 通道（整局经 NetworkIntentApplier 驱动，与直调等价的最小口径）
        // ============================================================

        private static void RunIntentSection()
        {
            var core = GameCore.Instance;
            GameOverEvent gameOver = null;
            void OnGameOver(GameOverEvent e) => gameOver = e;
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
            try
            {
                core.InitGame(CardLoader.BuildDeck(_deck, 1), CardLoader.BuildDeck(_deck, 1));
                core.Player1.IsAI = true; // 结算期残余交互走自动选择（与 AI 局同口径）
                core.Player2.IsAI = true;
                CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;

                var enumerator = new LegalActionEnumerator();
                var banned = new HashSet<string>();
                var coverage = new Dictionary<TideActionType, int>();
                int bannedTurnStamp = -1; // 拉黑按回合清：同一动作下回合可能重新合法（费用攒够/场面变化）

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
                        VerifySuite.Assert(ApplyIntent(seat, NetworkMessageType.IntentSkipStandby, emptyPayload), $"IntentSkipStandby@T{turn}");

                    if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) continue;

                    if (bannedTurnStamp != core.TurnEngine.TurnNumber)
                    {
                        bannedTurnStamp = core.TurnEngine.TurnNumber;
                        banned.Clear();
                    }

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

                        // 选型：①优先覆盖未触碰类型 ②非 EndTurn 且非 PlayLand ③PlayLand（每回合至多 1 张）④EndTurn
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

                    // End→Standby 折返（无帧泵宿主的既定惯例）
                    if (gameOver == null)
                        core.TurnEngine.CheckPhaseTransition();
                }

                foreach (var type in new[] { TideActionType.PlayCard, TideActionType.PlayLand, TideActionType.Attack, TideActionType.EndTurn })
                    VerifySuite.Assert(coverage.GetValueOrDefault(type) >= 1, $"intent 覆盖 {type} × {coverage.GetValueOrDefault(type)} ≥ 1");
                VerifySuite.Log($"intent 覆盖：{string.Join(", ", coverage.Select(k => $"{k.Key}×{k.Value}"))}");

                // Concede：经 intent 认输
                if (gameOver == null)
                {
                    VerifySuite.Assert(ApplyIntent(1, NetworkMessageType.IntentConcede, emptyPayload), "IntentConcede 被接受");
                    VerifySuite.Assert(gameOver != null && gameOver.Reason == GameOverReason.Concede
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

        private static void RunSelectorSection()
        {
            var core = GameCore.Instance;
            core.InitGame(CardLoader.BuildDeck(_deck, 1), CardLoader.BuildDeck(_deck, 1));
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

                VerifySuite.Assert(chosen.Count == 1 && chosen[0].RuntimeId == hand[0].RuntimeId,
                    "实体反问：模拟客户端选 [0] → 引擎拿到候选[0]（索引→实体映射经网络往返）");
                var last = selector.Seen.Last();
                VerifySuite.Assert(last.Labels.Length == 0 && last.Candidates.Length == hand.Count
                    && last.Min == 1 && last.Max == 2 && last.ChooserSeat == 0,
                    "MsgSelectRequest 携带完整请求（实体反问 Labels 恒空——卡名客户端按 CardId 查表、Min/Max/座位）");
                VerifySuite.Assert(last.Candidates[0].RuntimeId == hand[0].RuntimeId
                    && last.Candidates[0].CardId == hand[0].ID,
                    "候选实体引用双轨正确（RuntimeId+模板 CardId）");

                // ---- 3b. 手牌上限弃牌（真实引擎反问点：GameCore.OnTurnEnded → EnforceHandLimitAsync） ----
                selector.Seen.Clear();
                int before = core.ZoneManager.GetCards(core.Player1, Zone.Hand).Count;
                int limit = RuleHooks.GetHandLimit(core.Player1);
                int over = limit + 2 - before;
                for (int i = 0; i < over; i++)
                    core.ZoneManager.DrawCard(core.Player1);

                // 2026-09-24 回归推进口径：准备阶段自动进主（StartNewTurn 即 AdvanceFromStandby）
                VerifySuite.Assert(core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main,
                    "准备阶段已自动推进入主阶段（2026-09-24 口径）");
                VerifySuite.Assert(GameActions.EndTurn(core, core.Player1), "结束 P1 回合（触发手牌上限弃牌反问）");
                // EnforceHandLimitAsync 为 fire-and-forget，但回环选择器同步完成 → 链路同步收敛
                int after = core.ZoneManager.GetCards(core.Player1, Zone.Hand).Count;
                VerifySuite.Assert(after == limit, $"手牌上限弃牌：{limit + 2} → {after}（限 {limit}）");
                VerifySuite.Assert(selector.Seen.Any(r => r.Title == "手牌上限"), "弃牌反问走了 Ex 分支（真实引擎调用点）");
            }
            finally
            {
                selector.Detach();
            }

            // ---- 3c. 卸载后恢复 headless 路径 ----
            VerifySuite.Assert(TargetSelectionService.Current == null, "卸载后 Current 归空");
            var auto = TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = core.ZoneManager.GetCards(core.Player1, Zone.Hand).Cast<Entity>().ToList(),
                MinCount = 1,
                MaxCount = 1,
                Chooser = core.Player1, // 非 AI 但 Current==null → AutoSelect（headless 默认）
                AllowCancel = false,
            }).GetAwaiter().GetResult();
            VerifySuite.Assert(auto.Count == 1, "headless 自动选择路径不受影响（未装 Ex 时走原分支）");
        }

        // ============================================================
        // 第五段：开局握手（DeckSubmit/MatchManifest + 卡组引用闭包校验）
        // ============================================================

        private static void RunHandshakeSection()
        {
            var pool = CardCatalog.LoadAll();
            VerifySuite.Assert(pool.Count > 0, "卡池非空（握手段依赖真实用户数据）");
            if (pool.Count == 0) return;

            var deckIds = pool.Select(c => c.ID).ToArray();
            var totalRows = CardCore.Attribute.AtomicEffectTable.GetAll().Count();
            var digest = NetMatchHandshake.ComputeDeckDigest(deckIds);
            // Effects.json 2026-10-04 起为用户清空态（真效果入库前预期——V9.h 同款软跳过口径）：
            // 卡池无效果 → 引用闭包为空属数据态而非回归；「摘要非空」与 5d 漂移反例待真效果回库恢复。
            bool closureEmpty = string.IsNullOrEmpty(digest.AtomicRowsHash) || digest.AtomicRowCount == 0;
            if (closureEmpty)
                VerifySuite.Log("V5 跳过：卡组闭包原子引用为空（Effects.json 清空态）——摘要非空/漂移反例待真效果回库回归");
            else
                VerifySuite.Assert(!string.IsNullOrEmpty(digest.AtomicRowsHash) && digest.AtomicRowCount > 0,
                    "闭包原子行摘要非空（卡组确有原子引用）");
            VerifySuite.Assert(digest.AtomicRowCount <= totalRows,
                $"摘要只覆盖卡组引用的行（{digest.AtomicRowCount} ≤ 全表 {totalRows}——非全量对比口径）");
            var digestAgain = NetMatchHandshake.ComputeDeckDigest(deckIds);
            VerifySuite.Assert(digest.AtomicRowsHash == digestAgain.AtomicRowsHash,
                "摘要计算确定性（同数据两次计算全等）");

            // ---- 5a. 正例：真实卡池组卡组提交 → 服务器通过 + 线格式往返 + 双座位下发 + 客户端双向校验 ----
            var submit = new MsgDeckSubmit
            {
                DeckName = "回环验证卡组",
                CardIds = deckIds,
                Digest = digest,
            };
            VerifySuite.Assert(NetMatchHandshake.ValidateDeckSubmit(submit, out _),
                $"正例卡组提交通过（{deckIds.Length} 张，含效果/原子引用闭环检查）");

            var decodedSubmit = VerifyCommon.RoundTripMessage(NetworkMessageType.DeckSubmit, submit);
            VerifySuite.Assert(decodedSubmit.DeckName == submit.DeckName
                   && decodedSubmit.CardIds.SequenceEqual(submit.CardIds)
                   && decodedSubmit.Digest.AtomicRowsHash == digest.AtomicRowsHash,
                "DeckSubmit 信封+帧往返无损（卡组 ID 与闭包摘要）");

            foreach (var seat in new[] { 0, 1 })
            {
                var manifestMsg = NetMatchHandshake.BuildMatchManifest(seat, deckIds, deckIds.Length);
                var decodedManifest = VerifyCommon.RoundTripMessage(NetworkMessageType.MatchManifest, manifestMsg);
                VerifySuite.Assert(NetMatchHandshake.VerifyMatchManifest(decodedManifest, deckIds, out var clientReason),
                    $"座位 {seat} 客户端对局清单双向校验通过{(clientReason != null ? "（" + clientReason + "）" : "")}");
            }

            // ---- 5b. 反例：缺卡拒绝 ----
            var withGhost = new MsgDeckSubmit
            {
                DeckName = "缺卡卡组",
                CardIds = deckIds.Concat(new[] { "C_DEADBEEF" }).ToArray(),
                Digest = NetMatchHandshake.ComputeDeckDigest(deckIds.Concat(new[] { "C_DEADBEEF" }).ToArray()),
            };
            VerifySuite.Assert(!NetMatchHandshake.ValidateDeckSubmit(withGhost, out var ghostReason)
                   && ghostReason.Contains("卡表缺失"),
                $"缺卡提交被拒（reason 含\"卡表缺失\"：{ghostReason}）");

            // ---- 5c. 反例：重复卡拒绝 ----
            var withDup = new MsgDeckSubmit
            {
                DeckName = "重复卡组",
                CardIds = deckIds.Concat(new[] { deckIds[0] }).ToArray(),
                Digest = digest,
            };
            VerifySuite.Assert(!NetMatchHandshake.ValidateDeckSubmit(withDup, out var dupReason)
                   && dupReason.Contains("重复"),
                $"重复卡提交被拒（reason 含\"重复\"：{dupReason}）");

            // ---- 5d. 反例：闭包原子行摘要漂移 / 缺失 ----
            var driftSubmit = new MsgDeckSubmit
            {
                DeckName = "摘要漂移",
                CardIds = deckIds,
                Digest = new NetDeckDigest { AtomicRowsHash = "00000000", AtomicRowCount = digest.AtomicRowCount },
            };
            if (closureEmpty)
                VerifySuite.Log("V5 跳过：摘要漂移反例（闭包空无从比对——真效果回库后恢复硬断言）");
            else
                VerifySuite.Assert(!NetMatchHandshake.ValidateDeckSubmit(driftSubmit, out var driftReason)
                       && driftReason.Contains("原子行摘要不一致"),
                    $"原子行摘要漂移被拒（{driftReason}）");

            var noDigestSubmit = new MsgDeckSubmit { DeckName = "无摘要", CardIds = deckIds, Digest = null };
            VerifySuite.Assert(!NetMatchHandshake.ValidateDeckSubmit(noDigestSubmit, out var noDigestReason)
                   && noDigestReason.Contains("未随提交携带"),
                $"缺摘要提交被拒（{noDigestReason}）");

            // ---- 5e. 反例：客户端侧——服务器摘要漂移 / 卡组回显不符 ----
            var tamperedManifest = NetMatchHandshake.BuildMatchManifest(0, deckIds, deckIds.Length);
            tamperedManifest.OwnDeckDigest = new NetDeckDigest
            {
                AtomicRowsHash = "FFFFFFFF",
                AtomicRowCount = digest.AtomicRowCount,
            };
            VerifySuite.Assert(!NetMatchHandshake.VerifyMatchManifest(tamperedManifest, deckIds, out var clientDrift)
                   && clientDrift.Contains("原子行摘要不一致"),
                $"客户端拒绝摘要漂移的服务器（{clientDrift}）");

            var echoTampered = NetMatchHandshake.BuildMatchManifest(0,
                deckIds.Reverse().ToArray(), deckIds.Length);
            VerifySuite.Assert(!NetMatchHandshake.VerifyMatchManifest(echoTampered, deckIds, out var echoReason)
                   && echoReason.Contains("回显"),
                $"客户端拒绝卡组回显不符（{echoReason}）");
        }

        // ============================================================
        // 第六段：真实 socket 会话（进程内 M2 冒烟）
        // ============================================================

        private static void RunSocketSection()
        {
            var deckIds = DeckIds();
            var submit = BuildSubmit("socket冒烟");

            var server = new NetSessionServer("verify", null);
            server.Start(0); // OS 分配空闲口
            try
            {
                var a = new VerifyClient(); a.Connect("127.0.0.1", server.Port);
                var b = new VerifyClient(); b.Connect("127.0.0.1", server.Port);
                var spec = new VerifyClient(); spec.Connect("127.0.0.1", server.Port);

                // ---- 6a. 分座：A 指定 0 / 抢座反例 / B 指定 1 / 观战 ----
                a.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify", WantSeat = 0, Nickname = "A" });
                VerifyPump.PumpAndReceive(server, 300, a);
                VerifySuite.Assert(a.LastRoomState != null
                    && a.LastRoomState.Phase == (int)NetRoomPhase.Waiting
                    && a.LastRoomState.Players[0].Connected && a.LastRoomState.Players[0].Nickname == "A",
                    "A 分到椅子 0（RoomState=Waiting）");

                var rogue = new VerifyClient(); rogue.Connect("127.0.0.1", server.Port);
                rogue.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify", WantSeat = 0, Nickname = "抢座" });
                VerifyPump.PumpAndReceive(server, 300, rogue);
                VerifySuite.Assert(rogue.Errors.Any(e => e.Context == "JoinRoom" && e.Reason.Contains("占用")),
                    "抢已占座位被拒（Error 帧 Context=JoinRoom）");
                rogue.Close();

                b.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify", WantSeat = 1, Nickname = "B" });
                spec.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify", AsSpectator = true, Nickname = "S" });
                VerifyPump.PumpAndReceive(server, 300, a, b, spec);
                VerifySuite.Assert(a.LastRoomState != null && a.LastRoomState.Phase == (int)NetRoomPhase.DeckSubmit
                    && a.LastRoomState.Players[1].Connected && a.LastRoomState.SpectatorCount == 1,
                    "双玩家位满 → DeckSubmit（观战 1 人）");

                // ---- 6b. 握手（真实 socket 驱动 §11 消息流）----
                a.Send(NetworkMessageType.DeckSubmit, submit);
                b.Send(NetworkMessageType.DeckSubmit, submit);
                for (int i = 0; i < 100 && (a.Manifest == null || b.Manifest == null); i++)
                    VerifyPump.PumpAndReceive(server, 30, a, b, spec);
                VerifySuite.Assert(a.Manifest != null && b.Manifest != null, "双座位收到 MatchManifest");
                if (a.Manifest == null || b.Manifest == null) return;

                VerifySuite.Assert(a.Manifest.OwnSeat + b.Manifest.OwnSeat == 1, "引擎座位互补（换先手映射）");
                int firstSeat = a.LastRoomState?.FirstSeatThisMatch ?? -1;
                VerifySuite.Assert(firstSeat == 0 || firstSeat == 1, $"RoomState 广播本局先手椅位（{firstSeat}）");
                VerifySuite.Assert(a.Manifest.OwnSeat == (firstSeat == 0 ? 0 : 1)
                    && b.Manifest.OwnSeat == (firstSeat == 0 ? 1 : 0),
                    "椅位→引擎座位映射与先手一致（A=椅0）");
                a.MyEngineSeat = a.Manifest.OwnSeat;
                b.MyEngineSeat = b.Manifest.OwnSeat;
                VerifySuite.Assert(NetMatchHandshake.VerifyMatchManifest(a.Manifest, deckIds, out var reasonA),
                    $"A 客户端侧清单校验通过{(reasonA != null ? "（" + reasonA + "）" : "")}");
                VerifySuite.Assert(NetMatchHandshake.VerifyMatchManifest(b.Manifest, deckIds, out _),
                    "B 客户端侧清单校验通过");

                // ---- 6c. 拒绝语义：必失败 intent → Error 帧回发 ----
                a.Send(NetworkMessageType.IntentPlayCard,
                    new MsgIntentPlayCard { CardRuntimeId = 999999, FromZone = (int)Zone.Hand });
                VerifyPump.PumpAndReceive(server, 300, a, b, spec);
                VerifySuite.Assert(a.Errors.Any(e => e.Context == "IntentPlayCard"),
                    "非法 intent 被 Error 帧拒绝（RuntimeId 不存在）");

                // ---- 6d. 整局脚本（双客户端 intent 驱动；回合上限后认输兜底）----
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool conceded = false;
                while (sw.ElapsedMilliseconds < 90000)
                {
                    server.Pump();
                    a.Receive(5); b.Receive(5); spec.Receive(5);

                    if (a.SawGameOver) break;

                    var snap = a.LastSnapshot ?? b.LastSnapshot;
                    if (!conceded && snap != null && snap.CurrentTurn > 30)
                    {
                        conceded = true;
                        a.Send<object>(NetworkMessageType.IntentConcede, null);
                        continue;
                    }

                    ScriptBrain.Act(a);
                    ScriptBrain.Act(b);
                }

                for (int i = 0; i < 50; i++) // 终局冲刷
                {
                    server.Pump();
                    a.Receive(10); b.Receive(10); spec.Receive(10);
                    if (a.RoomFinished && b.RoomFinished && spec.RoomFinished)
                        break;
                }

                VerifySuite.Assert(a.SawGameOver, "整局到达终局（GameOverEvent 到达客户端）");
                VerifySuite.Assert(a.RoomFinished && b.RoomFinished && spec.RoomFinished,
                    "三方 RoomState(Finished)（正常终局/断线作废统一收口）");
                VerifySuite.Assert(!a.Disconnected && !b.Disconnected && !spec.Disconnected, "全程无意外断线");

                // ---- 6e. 回合推进与快照 ----
                VerifySuite.Assert(a.SnapshotsReceived >= 2 && b.SnapshotsReceived >= 2, "双客户端持续收到快照（稳定决策点取样）");
                int lastTurn = Math.Max(a.LastSnapshot?.CurrentTurn ?? 0, b.LastSnapshot?.CurrentTurn ?? 0);
                VerifySuite.Assert(lastTurn >= 3, $"回合推进 ≥3（实际 {lastTurn}——SkipStandby/EndTurn/折返链路通）");

                // ---- 6f. 隐藏过滤（玩家侧）与观战全信息 ----
                VerifySuite.Assert(a.Events.Count > 0 && spec.Events.Count > 0, "事件流到达（玩家+观战）");
                var aOppDraws = a.Events.Where(e => e.EventType == nameof(CardDrawEvent))
                    .Where(e => ScriptBrain.SeatOfParam(e, "Player") == 1 - a.MyEngineSeat).ToList();
                VerifySuite.Assert(aOppDraws.Count > 0, "A 的事件流含对手抽牌事件（抽牌发生本身可见）");
                VerifySuite.Assert(aOppDraws.All(e => ScriptBrain.HiddenOrAbsent(e, "DrawnCard")),
                    "A 的对手抽牌事件不携带 DrawnCard 引用（服务器出队按座位过滤）");
                var aSelfDraws = a.Events.Where(e => e.EventType == nameof(CardDrawEvent))
                    .Where(e => ScriptBrain.SeatOfParam(e, "Player") == a.MyEngineSeat).ToList();
                VerifySuite.Assert(aSelfDraws.Count > 0 && aSelfDraws.All(e => !ScriptBrain.HiddenOrAbsent(e, "DrawnCard")),
                    "A 的自己抽牌事件携带 DrawnCard（己方手牌可见）");

                var specDraws = spec.Events.Where(e => e.EventType == nameof(CardDrawEvent)).ToList();
                VerifySuite.Assert(specDraws.Count > 0 && specDraws.Any(e => !ScriptBrain.HiddenOrAbsent(e, "DrawnCard")),
                    "观战全信息：抽牌事件携带 DrawnCard");

                var specSnap = spec.FirstSnapshot ?? spec.LastSnapshot;
                VerifySuite.Assert(specSnap != null && specSnap.Hands.All(h => h.Count == 0
                    || (h.OwnRuntimeIds != null && h.OwnRuntimeIds.Length == h.Count)),
                    "观战快照双方手牌全展开（OwnRuntimeIds 满员）");
                VerifySuite.Assert(specSnap != null && specSnap.ZoneCards.Count(z => (Zone)z.Zone == Zone.Hand) == 2,
                    "观战快照含双方手牌区（fullInfo）");
                var aLast = a.LastSnapshot;
                VerifySuite.Assert(aLast != null
                    && (aLast.Hands.First(h => h.Seat == 1 - a.MyEngineSeat).OwnRuntimeIds?.Length ?? 0) == 0,
                    "玩家快照对方手牌只见数量");

                VerifySuite.Log($"socket 冒烟：事件 A×{a.Events.Count}/B×{b.Events.Count}/观战×{spec.Events.Count}，" +
                    $"快照 A×{a.SnapshotsReceived}/B×{b.SnapshotsReceived}，Error A×{a.Errors.Count}/B×{b.Errors.Count}，" +
                    $"终局回合 {lastTurn}（先手椅位 {firstSeat}）");

                a.Close(); b.Close(); spec.Close();
            }
            finally
            {
                server.Stop();
            }
        }

        // ============================================================
        // 第七段：M3 对拍（同种子双整局一致 + 种子敏感）
        // ============================================================

        private static void RunM3Section()
        {
            var submit = BuildSubmit("m3对拍");

            var run1 = RunNetworkGame(777, submit);
            var run2 = RunNetworkGame(777, submit);
            var run3 = RunNetworkGame(888, submit);

            // ---- 整局性 ----
            VerifySuite.Assert(run1.ReachedGameOver && run2.ReachedGameOver && run3.ReachedGameOver,
                "三局都到达终局（GameOverEvent 到达客户端）");
            VerifySuite.Assert(!run1.ClientDisconnected && !run2.ClientDisconnected && !run3.ClientDisconnected,
                "三局全程无意外断线");
            VerifySuite.Assert(run1.SnapshotCount > 10 && run2.SnapshotCount > 10, "客户端持续收到快照（锁步驱动）");

            // ---- 同种子对拍：事件流逐值相等 ----
            VerifySuite.Assert(run1.Events.Length == run2.Events.Length && run1.Events.Length > 0,
                $"同种子两局事件总量相等且非空（{run1.Events.Length} vs {run2.Events.Length}）");
            int mismatchAt = -1;
            for (int i = 0; i < Math.Min(run1.Events.Length, run2.Events.Length); i++)
            {
                if (!VerifyCommon.EqualEvent(run1.Events[i], run2.Events[i])) { mismatchAt = i; break; }
            }
            VerifySuite.Assert(mismatchAt < 0,
                $"同种子两局事件流逐值相等（首个差异 @{mismatchAt}：{run1.Events[Math.Max(0, mismatchAt)]?.EventType}）");
            if (mismatchAt >= 0)
            {
                // 失败诊断：逐参数对比首个差异事件 + 前几个事件序列（定位分叉根因用）
                VerifySuite.Log("  ---- run1 前 5 事件 ----");
                for (int i = 0; i < Math.Min(5, run1.Events.Length); i++)
                    VerifySuite.Log($"    [{i}] {run1.Events[i].EventType} id={run1.Events[i].EventId} " +
                        string.Join(" | ", (run1.Events[i].Params ?? Array.Empty<NetParam>()).Select(Describe)));
                VerifySuite.Log("  ---- run2 前 5 事件 ----");
                for (int i = 0; i < Math.Min(5, run2.Events.Length); i++)
                    VerifySuite.Log($"    [{i}] {run2.Events[i].EventType} id={run2.Events[i].EventId} " +
                        string.Join(" | ", (run2.Events[i].Params ?? Array.Empty<NetParam>()).Select(Describe)));
                var x = run1.Events[mismatchAt];
                var y = run2.Events[mismatchAt];
                VerifySuite.Log($"  差异诊断 @{mismatchAt} {x.EventType} vs {y.EventType}（EventId {x.EventId}/{y.EventId}，Turn {x.TurnNumber}/{y.TurnNumber}）");
                for (int p = 0; p < Math.Max(x.Params?.Length ?? 0, y.Params?.Length ?? 0); p++)
                {
                    var pa = x.Params != null && p < x.Params.Length ? x.Params[p] : null;
                    var pb = y.Params != null && p < y.Params.Length ? y.Params[p] : null;
                    VerifySuite.Log($"    [{p}] {Describe(pa)}  vs  {Describe(pb)}");
                }
            }
            else
            {
                VerifySuite.Log($"M3 对拍：seed777 双局 {run1.Events.Length} 事件全等" +
                    $"（终局回合 {run1.FinalTurn}，先手椅位 {run1.FirstSeat}，快照 {run1.SnapshotSeat0.Length}B）；" +
                    $"seed888 局 {run3.Events.Length} 事件（已发散）");
            }
            VerifySuite.Assert(run1.SnapshotSeat0.SequenceEqual(run2.SnapshotSeat0)
                && run1.SnapshotSeat1.SequenceEqual(run2.SnapshotSeat1),
                "同种子两局终局快照字节相等（双视角）");
            VerifySuite.Assert(run1.FinalTurn == run2.FinalTurn && run1.FirstSeat == run2.FirstSeat,
                $"同种子两局终局回合/先手一致（{run1.FinalTurn}/{run1.FirstSeat} vs {run2.FinalTurn}/{run2.FirstSeat}）");

            // ---- 种子敏感性：换种子流必须不同 ----
            bool seedDiverged = run1.Events.Length != run3.Events.Length
                || !Enumerable.Range(0, Math.Min(run1.Events.Length, run3.Events.Length))
                    .All(i => VerifyCommon.EqualEvent(run1.Events[i], run3.Events[i]));
            VerifySuite.Assert(seedDiverged, "换种子事件流不同（种子真实参与洗牌/先手——防假对拍）");
        }

        /// <summary>失败诊断：单参数可读化（对拍分叉定位用）。</summary>
        private static string Describe(NetParam p)
        {
            if (p == null) return "<缺>";
            string entity = "";
            if (p.EntityRefs != null && p.EntityRefs.Length > 0)
            {
                var parts = p.EntityRefs.Select(r =>
                {
                    var kind = r.IsPlayer ? "P" : "C";
                    return $"rt{r.RuntimeId}/s{r.Seat}/{kind}{r.CardId}";
                });
                entity = $" Ent[{string.Join(",", parts)}]";
            }
            return $"{p.FieldName}={p.Kind}:{p.IntValue}/{p.FloatValue:0.###}/{p.StringValue}{entity}";
        }

        // ============================================================ 共享：进程内网络整局（M3 骨架，V1/V7 复用） ============================================================

        private static MsgDeckSubmit BuildSubmit(string name)
        {
            var deckIds = DeckIds();
            return new MsgDeckSubmit
            {
                DeckName = name,
                CardIds = deckIds,
                Digest = NetMatchHandshake.ComputeDeckDigest(deckIds),
            };
        }

        /// <summary>跑一局种子钉死的网络整局（双 brain 客户端 + 真实 TCP，进程内泵直调），采集服务器侧对拍记录。</summary>
        public static GameRunRecord RunNetworkGame(int seed, MsgDeckSubmit submit, int turnConcedeCap = 40)
        {
            // 对拍前置：身份计数器归零（两局各自从 1 起）+ 解析缓存清空（防旧 RuntimeId 串号）
            Entity.ResetRuntimeIdCounterForVerification();
            GameEventBase.ResetEventIdCounterForVerification();
            NetEntityDirectory.Clear();

            var record = new GameRunRecord();
            var server = new NetSessionServer("verify-game", null, seed);
            server.Start(0);
            try
            {
                var a = new VerifyClient { Brain = new NetClientBrain() };
                var b = new VerifyClient { Brain = new NetClientBrain() };
                a.Connect("127.0.0.1", server.Port);
                b.Connect("127.0.0.1", server.Port);

                // 进房（先双方就座 → DeckSubmit 阶段，再提交——防提交赶在阶段迁移前被拒）
                a.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify-game", WantSeat = 0, Nickname = "A" });
                b.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "verify-game", WantSeat = 1, Nickname = "B" });
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
                    if (a.SawGameOver) break;
                    a.Brain.Think(a, turnConcedeCap);
                    b.Brain.Think(b, turnConcedeCap);
                }

                // 终局冲刷（事件/快照/RoomState 收尾）
                for (int i = 0; i < 60; i++)
                {
                    server.Pump();
                    a.Receive(10); b.Receive(10);
                    if (a.RoomFinished && b.RoomFinished)
                        break;
                }

                record.ReachedGameOver = a.SawGameOver;
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
    }
}
