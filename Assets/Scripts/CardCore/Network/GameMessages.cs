using System;
using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    [MemoryPackable]
    public partial class MsgPlayCard
    {
        [MemoryPackOrder(TagTable.MPC_CardID)]
        public string CardID;

        [MemoryPackOrder(TagTable.MPC_FromZone)]
        public int FromZone;

        [MemoryPackOrder(TagTable.MPC_ToZone)]
        public int ToZone;

        [MemoryPackOrder(TagTable.MPC_ChosenManaType)]
        public int ChosenManaType;
    }

    [MemoryPackable]
    public partial class MsgActivateEffect
    {
        [MemoryPackOrder(TagTable.MAE_SourceCardID)]
        public string SourceCardID;

        [MemoryPackOrder(TagTable.MAE_EffectTag)]
        public long EffectTag;

        [MemoryPackOrder(TagTable.MAE_ActivationSpeed)]
        public int ActivationSpeed;

        [MemoryPackOrder(TagTable.MAE_TargetIDs)]
        public string[] TargetIDs;
    }

    [MemoryPackable]
    public partial class MsgGameStateSync
    {
        [MemoryPackOrder(TagTable.MGSS_CurrentTurn)]
        public int CurrentTurn;

        [MemoryPackOrder(TagTable.MGSS_CurrentPhase)]
        public int CurrentPhase;

        [MemoryPackOrder(TagTable.MGSS_Players)]
        public PlayerState[] Players;

        [MemoryPackOrder(TagTable.MGSS_BattlefieldCards)]
        public CardCore.Serialization.SerializableRuntimeCardState[] BattlefieldCards;

        [MemoryPackOrder(TagTable.MGSS_Stack)]
        public CardCore.Serialization.SerializableEffectDefinition[] Stack;
    }

    [MemoryPackable]
    public partial class PlayerState
    {
        [MemoryPackOrder(TagTable.PS_Name)]
        public string Name;

        [MemoryPackOrder(TagTable.PS_Life)]
        public int Life;

        [MemoryPackOrder(TagTable.PS_MaxHealth)]
        public int MaxHealth;

        [MemoryPackOrder(TagTable.PS_DeckCount)]
        public int DeckCount;

        [MemoryPackOrder(TagTable.PS_HandCount)]
        public int HandCount;
    }
}