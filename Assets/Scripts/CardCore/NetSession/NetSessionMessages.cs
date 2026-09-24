using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    // ============================================================
    // 会话层消息（M2，2026-09-23 定案）：房间状态机 + intent 拒绝/Error 载荷。
    // 会话语义见根目录 网络协议.md §12；线格式仍走 NetworkMessage 信封 + FrameCodec 帧。
    //
    // 流程：
    //   连接后第一条上行必须是 JoinRoom(112)（分座/观战登记）；
    //   双玩家位就绪 → 服务器广播 RoomState(126, DeckSubmit)；
    //   双座位 DeckSubmit(111) 校验通过 → MatchManifest(125) 下发（OwnSeat=本局引擎座位）→ 开局。
    //
    // 座位口径（易混淆处钉死）：
    //   JoinRoom.WantSeat = "椅子位"（进房占位，房间生命周期内稳定）；
    //   引擎座位（快照 viewerSeat / 反问 ChooserSeat / intent 座位）由换先手决定——
    //   本局先手连接的引擎座位恒为 0（Player1），MatchManifest.OwnSeat 是每局权威，
    //   RoomState.FirstSeatThisMatch 同时广播供 UI 展示。
    // ============================================================

    /// <summary>下行：Error 帧载荷（99）。intent 拒绝 / 握手拒绝 / 房间操作拒绝共用；
    /// Context 标注来源（如 "IntentPlayCard" / "DeckSubmit" / "JoinRoom"），Reason 为人可读原因。</summary>
    [MemoryPackable]
    public partial class MsgError
    {
        [MemoryPackOrder(TagTable.MErr_Reason)]
        public string Reason;

        [MemoryPackOrder(TagTable.MErr_Context)]
        public string Context;
    }

    /// <summary>上行：进房（连接后第一条消息）。WantSeat=0/1 指定椅子位（被占即拒）、-1 任意空位；
    /// AsSpectator=true 一律登记观战（观战全信息——2026-09-23 定案）。</summary>
    [MemoryPackable]
    public partial class MsgJoinRoom
    {
        [MemoryPackOrder(TagTable.MJR_RoomId)]
        public string RoomId;

        [MemoryPackOrder(TagTable.MJR_WantSeat)]
        public int WantSeat;

        [MemoryPackOrder(TagTable.MJR_AsSpectator)]
        public bool AsSpectator;

        [MemoryPackOrder(TagTable.MJR_Nickname)]
        public string Nickname;
    }

    /// <summary>下行：房间全量状态（任何成员变化/阶段迁移时广播）。
    /// Phase 取 NetRoomPhase 值；Players 恒为 2 元素（椅子位视角，未入座 Connected=false）；
    /// FirstSeatThisMatch 仅 Playing/Finished 有意义（-1=未开局）。</summary>
    [MemoryPackable]
    public partial class MsgRoomState
    {
        [MemoryPackOrder(TagTable.MRS_RoomId)]
        public string RoomId;

        [MemoryPackOrder(TagTable.MRS_Phase)]
        public int Phase;

        [MemoryPackOrder(TagTable.MRS_Players)]
        public MsgRoomSeatInfo[] Players;

        [MemoryPackOrder(TagTable.MRS_SpectatorCount)]
        public int SpectatorCount;

        [MemoryPackOrder(TagTable.MRS_FirstSeatThisMatch)]
        public int FirstSeatThisMatch;
    }

    /// <summary>房间椅子位信息（RoomState.Players 元素）。</summary>
    [MemoryPackable]
    public partial class MsgRoomSeatInfo
    {
        [MemoryPackOrder(TagTable.MRSI_Seat)]
        public int Seat;

        [MemoryPackOrder(TagTable.MRSI_Nickname)]
        public string Nickname;

        [MemoryPackOrder(TagTable.MRSI_Connected)]
        public bool Connected;
    }

    /// <summary>房间阶段（RoomState.Phase 取值；int 传输，新增值只追加）。</summary>
    public enum NetRoomPhase
    {
        /// <summary>等进房（双玩家位未满；允许观战先进）。</summary>
        Waiting = 0,
        /// <summary>双方已就座，等卡组提交。</summary>
        DeckSubmit = 1,
        /// <summary>对局进行中。</summary>
        Playing = 2,
        /// <summary>对局结束（正常终局或断线作废）。重开 = 全员断开后房间回 Waiting 再进。</summary>
        Finished = 3,
    }

    // ============================================================
    // 大厅层（L1，2026-09-24 定案）：房间列表 + 自动匹配 + AI 填位，见 网络协议.md §13。
    // 连接后可先 LobbyHello(130) 进大厅浏览房间，再 LobbyCreateRoom/LobbyJoinRoom/
    // LobbyAutoMatch 入房；也可直接 JoinRoom(112) 跳过大厅（M2 兼容路径）。
    // 单房串行：GameCore 进程单例 → 一个进程同时只一局；房间占用期间创建/配对排队等待，
    // 房间回收（Finished + 全员离开 → Waiting）后队列自动补位。真并发多局需专用服务器构建（后置）。
    // ============================================================

    /// <summary>上行：进大厅（连接后第一条，登记昵称）——服务器回 LobbyState。</summary>
    [MemoryPackable]
    public partial class MsgLobbyHello
    {
        [MemoryPackOrder(TagTable.MLH_Nickname)]
        public string Nickname;
    }

    /// <summary>下行：大厅全量状态（任何房间/队列变化时推送给大厅相位连接）。</summary>
    [MemoryPackable]
    public partial class MsgLobbyState
    {
        [MemoryPackOrder(TagTable.MLS_Rooms)]
        public MsgLobbyRoomInfo[] Rooms;

        [MemoryPackOrder(TagTable.MLS_QueuedCount)]
        public int QueuedCount;
    }

    /// <summary>房间列表条目（单房串行 v1：Rooms 恒 0..1 条——空房不列）。</summary>
    [MemoryPackable]
    public partial class MsgLobbyRoomInfo
    {
        [MemoryPackOrder(TagTable.MLRI_RoomId)]
        public string RoomId;

        [MemoryPackOrder(TagTable.MLRI_Name)]
        public string Name;

        [MemoryPackOrder(TagTable.MLRI_Phase)]
        public int Phase;

        [MemoryPackOrder(TagTable.MLRI_PlayerCount)]
        public int PlayerCount;

        [MemoryPackOrder(TagTable.MLRI_Nicknames)]
        public string[] Nicknames;

        [MemoryPackOrder(TagTable.MLRI_SpectatorCount)]
        public int SpectatorCount;
    }

    /// <summary>上行：创建房间（房间名；单房串行——现有房间未回收时拒绝）。</summary>
    [MemoryPackable]
    public partial class MsgLobbyCreateRoom
    {
        [MemoryPackOrder(TagTable.MLCR_RoomName)]
        public string RoomName;
    }

    /// <summary>上行：从列表加入指定房间（内部走 JoinRoom 分座；房间锁定/已满由房间侧拒绝）。</summary>
    [MemoryPackable]
    public partial class MsgLobbyJoinRoom
    {
        [MemoryPackOrder(TagTable.MLJR_RoomId)]
        public string RoomId;
    }

    /// <summary>上行：AI 填位（房内玩家请求）——服务器向自身回环发起 NetClientBrain 客户端
    /// 连接占用剩余空位；对房间而言就是一个普通 TCP 客户端（走完整握手与锁步）。</summary>
    [MemoryPackable]
    public partial class MsgLobbyAddAi
    {
        [MemoryPackOrder(TagTable.MLAA_RoomId)]
        public string RoomId;

        [MemoryPackOrder(TagTable.MLAA_Nickname)]
        public string Nickname;
    }
}
