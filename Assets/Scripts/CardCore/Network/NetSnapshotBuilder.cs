using System;
using System.Linq;
using CardCore.Serialization;

namespace CardCore.Network
{
    /// <summary>
    /// 快照构造器（M1 协议下行）：GameCore → MsgGameStateSync（viewerSeat 视角）。
    ///
    /// - 隐藏信息：己方手牌传 RuntimeId（CardHandInfo.OwnRuntimeIds）、对方只数量；
    ///   牌库只数量；坟场/除外/战场/元素池/发动区/场地区/额外卡组全量公开。
    /// - 字节稳定：Counters 按键排序后序列化（同状态两次序列化 byte[] 相等——确定性回归断言用）。
    /// - 时点约束（M1 记录、M2 服务器收口）：人局 EndTurn 后可能赶上半完成结算
    ///   （EnforceHandLimitAsync / PassPriority 内部 .Forget() 异步）——服务器侧快照
    ///   前置静默检查（StackEngine.IsResolving + 栈空 + 无 pending）再取样。
    /// </summary>
    public static class NetSnapshotBuilder
    {
        /// <summary>公开区域（进 ZoneCards 全量）；Hand/Deck 不在此列——隐私经 CardHandInfo/Count 承载。</summary>
        private static readonly Zone[] PublicZones =
        {
            Zone.Battlefield, Zone.Graveyard, Zone.Exile,
            Zone.ElementPool, Zone.Activation, Zone.FieldZone, Zone.ExtraDeck,
        };

        public static MsgGameStateSync Build(GameCore core, int viewerSeat)
        {
            if (core == null) return null;

            var snapshot = new MsgGameStateSync
            {
                CurrentTurn = core.TurnEngine?.TurnNumber ?? 0,
                CurrentPhase = (int)(core.TurnEngine?.CurrentPhase?.Phase ?? PhaseType.Standby),
                ViewerSeat = viewerSeat,
                ActiveSeat = NetEntityMapper.SeatOf(core.TurnEngine?.TurnPlayer),
                PrioritySeat = NetEntityMapper.SeatOf(core.StackEngine?.CurrentPriorityHolder),
                Players = BuildPlayers(core),
                Hands = BuildHands(core, viewerSeat),
                StackV2 = BuildStack(core),
            };

            // 区域全量（公开区 + viewer 自己的手牌）
            var zones = new System.Collections.Generic.List<NetZoneCards>();
            foreach (var seat in new[] { 0, 1 })
            {
                var player = NetEntityDirectory.SeatToPlayer(core, seat);
                if (player == null) continue;

                foreach (var zone in PublicZones)
                    zones.Add(BuildZone(core, seat, player, zone));
                if (seat == viewerSeat)
                    zones.Add(BuildZone(core, seat, player, Zone.Hand));
            }
            snapshot.ZoneCards = zones.ToArray();

            // 旧字段兼容填充（Battlefield 全量）
            snapshot.BattlefieldCards = snapshot.ZoneCards
                .Where(z => z.Zone == (int)Zone.Battlefield)
                .SelectMany(z => z.Cards)
                .ToArray();

            return snapshot;
        }

        private static PlayerState[] BuildPlayers(GameCore core)
        {
            var players = new System.Collections.Generic.List<PlayerState>(2);
            foreach (var seat in new[] { 0, 1 })
            {
                var player = NetEntityDirectory.SeatToPlayer(core, seat);
                if (player == null) continue;

                var bank = new System.Collections.Generic.List<ManaEntryDTO>();
                foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
                {
                    int count = core.ElementPool.GetAvailableManaCount(type, player);
                    if (count > 0)
                        bank.Add(new ManaEntryDTO { ManaType = (int)type, Value = count });
                }

                players.Add(new PlayerState
                {
                    Seat = seat,
                    Name = player.Name,
                    Life = player.Life,
                    MaxHealth = player.MaxHealth,
                    DeckCount = core.ZoneManager.GetCards(player, Zone.Deck)?.Count ?? 0,
                    HandCount = core.ZoneManager.GetCards(player, Zone.Hand)?.Count ?? 0,
                    IsAI = player.IsAI,
                    FatigueCount = player.FatigueCount,
                    LandCap = core.ElementPool.GetLandCap(player),
                    ElementBank = bank.ToArray(),
                    OffsetDrainUsed = player.OffsetDrainUsed,
                    OffsetDiscardUsed = player.OffsetDiscardUsed,
                    OffsetMillUsed = player.OffsetMillUsed,
                    OffsetOpponentHealUsed = player.OffsetOpponentHealUsed,
                    OffsetOpponentDrawUsed = player.OffsetOpponentDrawUsed,
                    OffsetSendExtraUsed = player.OffsetSendExtraUsed,
                    GraveyardCount = core.ZoneManager.GetCards(player, Zone.Graveyard)?.Count ?? 0,
                    ExileCount = core.ZoneManager.GetCards(player, Zone.Exile)?.Count ?? 0,
                });
            }
            return players.ToArray();
        }

        private static CardHandInfo[] BuildHands(GameCore core, int viewerSeat)
        {
            var hands = new System.Collections.Generic.List<CardHandInfo>(2);
            foreach (var seat in new[] { 0, 1 })
            {
                var player = NetEntityDirectory.SeatToPlayer(core, seat);
                if (player == null) continue;

                var hand = core.ZoneManager.GetCards(player, Zone.Hand) ?? new System.Collections.Generic.List<Card>();
                var info = new CardHandInfo
                {
                    Seat = seat,
                    Count = hand.Count,
                    OwnRuntimeIds = seat == viewerSeat
                        ? hand.Select(c => c.RuntimeId).ToArray()
                        : Array.Empty<uint>(),
                };
                hands.Add(info);
            }
            return hands.ToArray();
        }

        private static StackItemDTO[] BuildStack(GameCore core)
        {
            var contents = core.StackEngine?.GetStackContents();
            if (contents == null || contents.Count == 0) return Array.Empty<StackItemDTO>();

            // 栈顶在数组尾（LIFO 结算）——按下标原序传输，客户端取末尾即栈顶
            return contents.Select(instance => new StackItemDTO
            {
                Source = NetEntityMapper.FromEntity(instance.Source),
                IsCardCast = instance.IsCardCast,
                EffectId = instance.Definition?.Id,
                EffectDisplayName = instance.Definition?.DisplayName,
                ModeIndex = instance.ModeIndex,
                Targets = instance.Targets?.Select(NetEntityMapper.FromEntity).ToArray() ?? Array.Empty<NetEntityRef>(),
                ActivationSpeed = instance.ActivationSpeed,
                StackObjectType = (int)instance.Type,
            }).ToArray();
        }

        private static NetZoneCards BuildZone(GameCore core, int seat, Player player, Zone zone)
        {
            var cards = core.ZoneManager.GetCards(player, zone) ?? new System.Collections.Generic.List<Card>();
            return new NetZoneCards
            {
                Seat = seat,
                Zone = (int)zone,
                Cards = cards.Select(ToSortedCardState).ToArray(),
            };
        }

        /// <summary>FromCard + Counters 键排序（字典迭代序不定，排序保证同状态字节稳定）。</summary>
        private static SerializableRuntimeCardState ToSortedCardState(Card card)
        {
            var dto = SerializableRuntimeCardState.FromCard(card);
            if (dto.Counters != null && dto.Counters.Length > 1)
                dto.Counters = dto.Counters.OrderBy(c => c.Key, StringComparer.Ordinal).ToArray();
            return dto;
        }
    }
}
