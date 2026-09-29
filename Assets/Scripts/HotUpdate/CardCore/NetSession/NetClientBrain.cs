using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Serialization;
using SynergyUI;

namespace CardCore.Network
{
    /// <summary>大脑的发送通道（客户端宿主实现：验证器=SocketTestClient，独立客户端=NetClientHost）。</summary>
    public interface IBrainChannel
    {
        void Send<T>(NetworkMessageType type, T payload) where T : class;
    }

    /// <summary>
    /// headless 客户端大脑（M3，2026-09-23）：快照驱动的确定性决策层——
    /// SimpleAI 启发式的**客户端移植**（客户端没有引擎，只有 MsgGameStateSync + 反问/错误流）。
    ///
    /// 确定性关键 = **修订锁步**：只在「收到新快照帧」或「收到新 Error 帧」后行动一次——
    /// 行动序列是服务器下行序列的纯函数，与 wall-clock / TCP 粘包/合批无关（同种子对拍的基石）。
    /// 被拒动作无新快照，靠错误计数解锁重试；tried 集合防同帧拒绝风暴。
    ///
    /// 决策阶梯（每次机会取第一个可行动作）：
    ///   ①反问应答（不受锁步限制——服务器在等）→ ②栈上让行（优先权在我 → Pass；
    ///   **先于行动方判断**——响应窗口的持有者常非 ActiveSeat，门错位即互相等待死锁）
    ///   → ③Standby 跳过 → ④Main：横置下一张未横置地（产色=剩余指示物首色）→ 放地
    ///   （每回合 1 张，本地卡表判生物资格——地牌资格=卡组正式生物）→ 出牌（手牌快照序，
    ///   付不起由服务器 Error 拒+本回合拉黑）→ 攻击（首个未横置且本回合未宣战过的己方
    ///   战场生物 → 对方角色）→ EndTurn。
    /// 卡表查本地 CardCatalog（与客户端本地渲染同源——"线上只传 ID"定案）。
    /// </summary>
    public sealed class NetClientBrain
    {
        private readonly Dictionary<string, CardData> _cardById = new Dictionary<string, CardData>();

        // ---- 客户端侧状态（宿主喂入）----
        public int MyEngineSeat = -1;
        public MsgRoomState RoomState;
        public MsgGameStateSync Snapshot;
        public readonly List<MsgSelectRequest> PendingSelects = new List<MsgSelectRequest>();
        public int SnapshotsSeen;
        public int ErrorsSeen;
        public int ConcedeCount;

        // ---- 锁步游标：上次行动时的 (快照帧号, 错误帧数)——任一前进才允许再行动 ----
        private int _actedAtSnapshot = -1;
        private int _actedAtErrors = -1;

        // ---- 回合内状态（快照 CurrentTurn 变化时重置）----
        private int _turnStamp = -1;
        private bool _landDropped;
        private readonly HashSet<uint> _attackedThisTurn = new HashSet<uint>();
        private readonly HashSet<uint> _triedPlayThisTurn = new HashSet<uint>();

        public NetClientBrain()
        {
            foreach (var card in CardCatalog.LoadAll())
                _cardById[card.ID] = card;
        }

        /// <summary>喂入新快照（宿主收到 GameStateSyncV2 时调用）。</summary>
        public void OnSnapshot(MsgGameStateSync snapshot)
        {
            Snapshot = snapshot;
            SnapshotsSeen++;
        }

        /// <summary>决策并发送至多一条 intent（锁步；无行动则静默）。</summary>
        public void Think(IBrainChannel channel, int turnConcedeCap)
        {
            // 回合上限兜底：确定性认输（同种子两局在同一点触发）
            if (Snapshot != null && Snapshot.CurrentTurn > turnConcedeCap && ConcedeCount == 0)
            {
                ConcedeCount = 1;
                channel.Send<object>(NetworkMessageType.IntentConcede, null);
                return;
            }

            // ① 反问优先——服务器在等应答，不受锁步限制
            if (PendingSelects.Count > 0)
            {
                var req = PendingSelects[0];
                PendingSelects.RemoveAt(0);
                int pool = req.Candidates != null && req.Candidates.Length > 0
                    ? req.Candidates.Length
                    : (req.Labels?.Length ?? 0);
                int count = Math.Max(1, Math.Min(req.Min, pool));
                channel.Send(NetworkMessageType.SelectResponse, new MsgSelectResponse
                {
                    RequestId = req.RequestId,
                    Indices = Enumerable.Range(0, count).ToArray(),
                });
                return;
            }

            if (Snapshot == null || MyEngineSeat < 0) return;
            // 锁步门：快照与错误计数都未前进 → 不对旧快照重复出手
            if (SnapshotsSeen == _actedAtSnapshot && ErrorsSeen == _actedAtErrors) return;

            var snap = Snapshot;

            // ③ 栈上让行——**先于行动方判断**：我的施放在栈上等对手响应时，对手不是
            // ActiveSeat 但持有优先权——此时不让行 = 双方互等死锁（M3 首跑实锤）。
            if (snap.PrioritySeat == MyEngineSeat && snap.StackV2 != null && snap.StackV2.Length > 0)
            {
                MarkActed();
                channel.Send<object>(NetworkMessageType.IntentPassPriority, null);
                return;
            }

            if (snap.ActiveSeat != MyEngineSeat) return; // 非我回合：等下一帧

            if (snap.CurrentTurn != _turnStamp)
            {
                _turnStamp = snap.CurrentTurn;
                _landDropped = false;
                _attackedThisTurn.Clear();
                _triedPlayThisTurn.Clear();
            }

            var phase = (PhaseType)snap.CurrentPhase;

            // ② Standby → Main
            if (phase == PhaseType.Standby)
            {
                MarkActed();
                channel.Send<object>(NetworkMessageType.IntentSkipStandby, null);
                return;
            }

            if (phase != PhaseType.Main) return;

            // ④-1 横置地：己方元素池区未横置且有剩余指示物的地牌，产色=指示物首色（枚举序）
            var pooled = ZoneCards(snap, MyEngineSeat, Zone.ElementPool);
            var land = pooled.FirstOrDefault(c => !c.IsTapped && HasTokens(c));
            if (land != null)
            {
                MarkActed();
                channel.Send(NetworkMessageType.IntentTapForElement, new MsgIntentTapForElement
                {
                    CardRuntimeId = land.RuntimeId,
                    ManaType = (int)FirstTokenColor(land),
                });
                return;
            }

            // ④-2 放地（每回合 1 张）：手牌首张生物（地牌资格=卡组正式生物）
            var hand = ZoneCards(snap, MyEngineSeat, Zone.Hand);
            if (!_landDropped)
            {
                var creature = hand.FirstOrDefault(IsCreature);
                if (creature != null)
                {
                    MarkActed();
                    _landDropped = true;
                    channel.Send(NetworkMessageType.IntentAddToElementPool,
                        new MsgIntentAddToElementPool { CardRuntimeId = creature.RuntimeId });
                    return;
                }
            }

            // ④-3 出牌：手牌快照序（每张每回合只试一次——付不起被拒后本回合不重试）
            var play = hand.FirstOrDefault(c => !_triedPlayThisTurn.Contains(c.RuntimeId));
            if (play != null)
            {
                MarkActed();
                _triedPlayThisTurn.Add(play.RuntimeId);
                channel.Send(NetworkMessageType.IntentPlayCard, new MsgIntentPlayCard
                {
                    CardRuntimeId = play.RuntimeId,
                    FromZone = (int)Zone.Hand,
                    ModeIndex = 0,
                    Targets = null, // 目标交引擎反问解析（① 应答覆盖）
                });
                return;
            }

            // ④-4 攻击：首个未横置、力量>0、本回合未宣战过的己方战场生物 → 对方角色
            var attacker = ZoneCards(snap, MyEngineSeat, Zone.Battlefield)
                .FirstOrDefault(c => !c.IsTapped && c.Power > 0 && !_attackedThisTurn.Contains(c.RuntimeId));
            if (attacker != null)
            {
                MarkActed();
                _attackedThisTurn.Add(attacker.RuntimeId);
                channel.Send(NetworkMessageType.IntentDeclareAttack, new MsgIntentDeclareAttack
                {
                    Attacker = new NetEntityRef
                    {
                        RuntimeId = attacker.RuntimeId,
                        Seat = MyEngineSeat,
                        IsPlayer = false,
                        CardId = attacker.ID,
                    },
                    Target = new NetEntityRef { Seat = 1 - MyEngineSeat, IsPlayer = true },
                });
                return;
            }

            // ④-5 EndTurn
            MarkActed();
            channel.Send<object>(NetworkMessageType.IntentEndTurn, null);
        }

        private void MarkActed()
        {
            _actedAtSnapshot = SnapshotsSeen;
            _actedAtErrors = ErrorsSeen;
        }

        private static SerializableRuntimeCardState[] ZoneCards(MsgGameStateSync snap, int seat, Zone zone)
            => snap.ZoneCards.FirstOrDefault(z => z.Seat == seat && (Zone)z.Zone == zone)?.Cards
               ?? Array.Empty<SerializableRuntimeCardState>();

        private static bool HasTokens(SerializableRuntimeCardState card)
            => card.RemainingLandTokens != null && card.RemainingLandTokens.Any(t => t.Value > 0);

        private static ManaType FirstTokenColor(SerializableRuntimeCardState card)
        {
            var best = card.RemainingLandTokens
                .Where(t => t.Value > 0)
                .OrderBy(t => t.ManaType)
                .First();
            return (ManaType)best.ManaType;
        }

        private bool IsCreature(SerializableRuntimeCardState card)
            => _cardById.TryGetValue(card.ID, out var data) && data.Supertype == Cardtype.Creature;
    }
}
