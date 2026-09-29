using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace CardCore.Serialization
{
    [MemoryPackable]
    public partial class SerializableRuntimeCardState
    {
        [MemoryPackOrder(TagTable.RCS_ID)]
        public string ID;

        [MemoryPackOrder(TagTable.RCS_Power)]
        public int Power;

        [MemoryPackOrder(TagTable.RCS_Life)]
        public int Life;

        [MemoryPackOrder(TagTable.RCS_MaxLife)]
        public int MaxLife;

        [MemoryPackOrder(TagTable.RCS_BaseCost)]
        public int BaseCost;

        [MemoryPackOrder(TagTable.RCS_CostModifier)]
        public int CostModifier;

        [MemoryPackOrder(TagTable.RCS_IsTapped)]
        public bool IsTapped;

        [MemoryPackOrder(TagTable.RCS_IsNegated)]
        public bool IsNegated;

        [MemoryPackOrder(TagTable.RCS_IsNullified)]
        public bool IsNullified;

        [MemoryPackOrder(TagTable.RCS_Zone)]
        public int Zone;

        [MemoryPackOrder(TagTable.RCS_TargetFlags)]
        public int TargetFlags;

        [MemoryPackOrder(TagTable.RCS_Keywords)]
        public string[] Keywords;

        [MemoryPackOrder(TagTable.RCS_Counters)]
        public CounterEntryDTO[] Counters;

        [MemoryPackOrder(TagTable.RCS_WasDepletedAsLand)]
        public bool WasDepletedAsLand;

        [MemoryPackOrder(TagTable.RCS_RemainingLandTokens)]
        public ManaEntryDTO[] RemainingLandTokens;

        // ---- M1 网络快照扩展（2026-09-10）：单向导出字段，ApplyToCard 不回灌 ----
        // RuntimeId 只读（Entity 构造期分配）；ControllerSeat 经 SetController 走 Player 对象（DTO 无核心上下文）；
        // IsFrozen 是计算值（FreezeCounter>0），回灌须走指示物写入——三者均为快照展示/对账口径。

        [MemoryPackOrder(TagTable.RCS_RuntimeId)]
        public uint RuntimeId;

        [MemoryPackOrder(TagTable.RCS_ControllerSeat)]
        public int ControllerSeat;

        [MemoryPackOrder(TagTable.RCS_IsFrozen)]
        public bool IsFrozen;

        public static SerializableRuntimeCardState FromCard(Card card)
        {
            var dto = new SerializableRuntimeCardState
            {
                ID = card.ID,
                Power = card._power,
                Life = card._life,
                MaxLife = card._maxLife,
                BaseCost = card._baseCost,
                CostModifier = card._costModifier,
                IsTapped = card._isTapped,
                IsNegated = card._isNegated,
                IsNullified = card._isNullified,
                Zone = (int)card._zone,
                TargetFlags = (int)card._targetFlags,
                WasDepletedAsLand = card.WasDepletedAsLand,
                RuntimeId = card.RuntimeId,
                ControllerSeat = SeatOfController(card),
                IsFrozen = card.IsFrozen(),
            };

            dto.Keywords = card._keywords?.ToArray() ?? Array.Empty<string>();
            dto.Counters = card._counters?.Select(kvp => new CounterEntryDTO { Key = kvp.Key, Value = kvp.Value }).ToArray()
                           ?? Array.Empty<CounterEntryDTO>();
            dto.RemainingLandTokens = card._remainingLandTokens?
                .Where(kv => kv.Value > 0)
                .Select(kv => new ManaEntryDTO { ManaType = (int)kv.Key, Value = kv.Value }).ToArray()
                ?? Array.Empty<ManaEntryDTO>();

            return dto;
        }

        /// <summary>控制器座位（P1=0/P2=1/其他=-1）。快照对账口径；回灌走 SetController 不经 DTO。</summary>
        private static int SeatOfController(Card card)
        {
            var controller = card.GetController();
            var core = GameCore.Instance;
            if (controller == null || core == null) return -1;
            if (ReferenceEquals(controller, core.Player1)) return 0;
            if (ReferenceEquals(controller, core.Player2)) return 1;
            return -1;
        }

        public void ApplyToCard(Card card)
        {
            card.ID = ID;
            card._power = Power;
            card._life = Life;
            card._maxLife = MaxLife;
            card._baseCost = BaseCost;
            card._costModifier = CostModifier;
            card._isTapped = IsTapped;
            card._isNegated = IsNegated;
            card._isNullified = IsNullified;
            card._zone = (Zone)Zone;
            card._targetFlags = (EffectTargetFlags)TargetFlags;

            if (Keywords != null)
            {
                card._keywords.Clear();
                foreach (var kw in Keywords)
                    card._keywords.Add(kw);
            }

            if (Counters != null)
            {
                card._counters.Clear();
                foreach (var c in Counters)
                    card._counters[c.Key] = c.Value;
            }

            card.WasDepletedAsLand = WasDepletedAsLand;
            card._remainingLandTokens = RemainingLandTokens == null || RemainingLandTokens.Length == 0
                ? null
                : RemainingLandTokens.Where(e => e.Value > 0)
                    .ToDictionary(e => (ManaType)e.ManaType, e => e.Value);
        }
    }

    [MemoryPackable]
    public partial class CounterEntryDTO
    {
        [MemoryPackOrder(TagTable.CounterEntryDTO_Key)]
        public string Key;

        [MemoryPackOrder(TagTable.CounterEntryDTO_Value)]
        public int Value;
    }

    [MemoryPackable]
    public partial class ManaEntryDTO
    {
        [MemoryPackOrder(TagTable.ManaEntryDTO_ManaType)]
        public int ManaType;

        [MemoryPackOrder(TagTable.ManaEntryDTO_Value)]
        public int Value;
    }
}