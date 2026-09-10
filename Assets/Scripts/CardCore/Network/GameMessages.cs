using System;
using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    // ============================================================
    // 下行快照 DTO（M1 协议定版 2026-09-10，重写自早期草稿——旧 MsgPlayCard/
    // MsgActivateEffect 已被 IntentMessages 的 intent 集取代并删除，tags 留作保留位）。
    //
    // 隐藏信息口径（快照侧）：己方手牌传 RuntimeId、对方只传数量（CardHandInfo）；
    // 牌库只传数量；坟场/除外/战场/元素池/发动区/场地区/额外卡组全量公开。
    // 事件流侧的隐藏信息过滤发生在服务器出队时（M2），见 NetEventMessages 声明。
    // 详见根目录 网络协议.md。
    // ============================================================

    /// <summary>全量快照（服务器→客户端；重连/校验基点）。由 NetSnapshotBuilder.Build(core, viewerSeat) 构造。</summary>
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
        public SerializableRuntimeCardState[] BattlefieldCards; // 旧字段保留（tag 兼容）；V2 全量在 ZoneCards

        [MemoryPackOrder(TagTable.MGSS_Stack)]
        public SerializableEffectDefinition[] Stack; // 废弃不用（粒度错误：栈上是 EffectInstance 非定义）；恒 null，V2 用 StackV2

        [MemoryPackOrder(TagTable.MGSS_ViewerSeat)]
        public int ViewerSeat;

        [MemoryPackOrder(TagTable.MGSS_ActiveSeat)]
        public int ActiveSeat;

        [MemoryPackOrder(TagTable.MGSS_PrioritySeat)]
        public int PrioritySeat;

        [MemoryPackOrder(TagTable.MGSS_ZoneCards)]
        public NetZoneCards[] ZoneCards;

        [MemoryPackOrder(TagTable.MGSS_Hands)]
        public CardHandInfo[] Hands;

        [MemoryPackOrder(TagTable.MGSS_StackV2)]
        public StackItemDTO[] StackV2;
    }

    /// <summary>玩家状态（公开面 + 己方私密面经 Hands 单独承载）。</summary>
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

        // ---- M1 扩展 ----
        [MemoryPackOrder(TagTable.PS_Seat)]
        public int Seat;

        [MemoryPackOrder(TagTable.PS_IsAI)]
        public bool IsAI;

        [MemoryPackOrder(TagTable.PS_FatigueCount)]
        public int FatigueCount;

        [MemoryPackOrder(TagTable.PS_LandCap)]
        public int LandCap;

        /// <summary>bank 各色可用元素（跨回合保留，无上限）。</summary>
        [MemoryPackOrder(TagTable.PS_ElementBank)]
        public ManaEntryDTO[] ElementBank;

        // 代价抵消用量（单局上限，构筑期代价抵消口径）
        [MemoryPackOrder(TagTable.PS_OffsetDrainUsed)]
        public int OffsetDrainUsed;

        [MemoryPackOrder(TagTable.PS_OffsetDiscardUsed)]
        public int OffsetDiscardUsed;

        [MemoryPackOrder(TagTable.PS_OffsetMillUsed)]
        public int OffsetMillUsed;

        [MemoryPackOrder(TagTable.PS_OffsetOpponentHealUsed)]
        public int OffsetOpponentHealUsed;

        [MemoryPackOrder(TagTable.PS_OffsetOpponentDrawUsed)]
        public int OffsetOpponentDrawUsed;

        [MemoryPackOrder(TagTable.PS_OffsetSendExtraUsed)]
        public int OffsetSendExtraUsed;

        [MemoryPackOrder(TagTable.PS_GraveyardCount)]
        public int GraveyardCount;

        [MemoryPackOrder(TagTable.PS_ExileCount)]
        public int ExileCount;
    }

    /// <summary>栈条目：栈上是 EffectInstance（源卡+目标+modeIndex），不是效果定义。
    /// 法术效果的归因 Source=Player（GameActions 定案）——NetEntityRef 双轨天然覆盖。</summary>
    [MemoryPackable]
    public partial class StackItemDTO
    {
        [MemoryPackOrder(TagTable.SID_Source)]
        public NetEntityRef Source;

        [MemoryPackOrder(TagTable.SID_IsCardCast)]
        public bool IsCardCast;

        [MemoryPackOrder(TagTable.SID_EffectId)]
        public string EffectId;

        [MemoryPackOrder(TagTable.SID_EffectDisplayName)]
        public string EffectDisplayName;

        [MemoryPackOrder(TagTable.SID_ModeIndex)]
        public int ModeIndex;

        [MemoryPackOrder(TagTable.SID_Targets)]
        public NetEntityRef[] Targets;

        [MemoryPackOrder(TagTable.SID_ActivationSpeed)]
        public int ActivationSpeed;

        /// <summary>IStackObject.Type（StackObjectType 枚举 int）。</summary>
        [MemoryPackOrder(TagTable.SID_StackObjectType)]
        public int StackObjectType;
    }

    /// <summary>区域卡牌全量（公开区域）。Zone = Zone 枚举 int（枚举只追加不插值，见 Zones.cs）。</summary>
    [MemoryPackable]
    public partial class NetZoneCards
    {
        [MemoryPackOrder(TagTable.NZC_Seat)]
        public int Seat;

        [MemoryPackOrder(TagTable.NZC_Zone)]
        public int Zone;

        [MemoryPackOrder(TagTable.NZC_Cards)]
        public SerializableRuntimeCardState[] Cards;
    }

    /// <summary>手牌隐藏信息口径：Count 恒有；OwnRuntimeIds 仅填 viewer 自己的手牌（对方为空数组）。</summary>
    [MemoryPackable]
    public partial class CardHandInfo
    {
        [MemoryPackOrder(TagTable.CHI_Seat)]
        public int Seat;

        [MemoryPackOrder(TagTable.CHI_Count)]
        public int Count;

        [MemoryPackOrder(TagTable.CHI_OwnRuntimeIds)]
        public uint[] OwnRuntimeIds;
    }
}
