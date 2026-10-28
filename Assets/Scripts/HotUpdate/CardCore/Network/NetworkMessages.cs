using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    /// <summary>
    /// 网络消息类型（M1 协议定版 2026-09-10）。
    /// 值一经分配永不重排：0-99 为早期草稿保留段（多为死值，仅作历史占位）；
    /// 100+ 上行 intent（客户端→服务器）；120+ 下行（服务器→客户端）。
    /// 详见根目录 网络协议.md。
    /// </summary>
    public enum NetworkMessageType : int
    {
        None = 0,
        PlayerJoin = 1,
        PlayerReady = 2,
        GameStart = 3,
        DrawCard = 10,
        PlayCard = 11,
        DeclareAttack = 12,
        DeclareBlock = 13,
        PassPriority = 14,
        ActivateEffect = 15,
        GameStateSync = 20,
        CardStateSync = 21,
        EffectActivationRequest = 30,
        EffectActivationResponse = 31,
        EffectResolution = 32,
        GameOver = 50,
        Error = 99,

        // ---- 上行 intent（客户端→服务器，payload 见 IntentMessages.cs）----
        IntentPlayCard = 100,        // 三路合一：FromZone=Hand/Graveyard；服务器按"栈非空且持优先权"分派 PlayCardInResponse
        // IntentTapForElement = 101 已删（2026-10-11 现取现用：无手动横置）——枚举值留洞保序
        IntentAddToElementPool = 102,// 放地进元素池
        IntentSkipStandby = 103,     // 跳过准备阶段
        IntentDeclareAttack = 104,
        IntentDeclareBlock = 105,    // 预留：引擎有 API 零调用
        IntentActivateEffect = 106,
        IntentPassPriority = 107,
        IntentEndTurn = 108,
        IntentConcede = 109,         // 认输：EndGame(opponent, Concede)
        SelectResponse = 110,        // 反问应答（MsgSelectResponse）
        DeckSubmit = 111,            // 开局握手：卡组提交（MsgDeckSubmit，见 网络协议.md §11）
        JoinRoom = 112,              // 会话层：进房分座/观战登记（MsgJoinRoom，见 网络协议.md §12）
        IntentActivateHeroSkill = 113, // 英雄技能发动（MsgIntentActivateHeroSkill——2026-10-21 协议补课：技能格 10-15 加入后首个新 intent；离散位在会话层消息之后）

        // ---- 下行（服务器→客户端）----
        SelectRequest = 120,         // 反问请求（MsgSelectRequest）
        NetEventBatch = 121,         // 事件流批次（MsgNetEventBatch）
        GameStateSyncV2 = 122,       // 全量快照（MsgGameStateSync，viewerSeat 视角）
        Ping = 123,
        Pong = 124,
        MatchManifest = 125,         // 开局握手：对局清单（MsgMatchManifest，见 网络协议.md §11）
        RoomState = 126,             // 会话层：房间全量状态广播（MsgRoomState，见 网络协议.md §12）

        // ---- 大厅层（L1，2026-09-24：房间列表 + 自动匹配 + AI 填位，见 网络协议.md §13）----
        LobbyHello = 130,            // 上行：进大厅（MsgLobbyHello）→ 服务器回 LobbyState
        LobbyState = 131,            // 下行：大厅全量状态（MsgLobbyState：房间列表 + 排队数）
        LobbyCreateRoom = 132,       // 上行：创建房间（MsgLobbyCreateRoom）——单房串行：房间占用即拒
        LobbyJoinRoom = 133,         // 上行：从房间列表加入（MsgLobbyJoinRoom）→ 内部走 JoinRoom 分座
        LobbyAutoMatch = 134,        // 上行：自动匹配排队/取消（空载荷；重复发送 = 取消排队）
        LobbyAddAi = 135,            // 上行：AI 填位（MsgLobbyAddAi）——服务器本机回环 NetClientBrain 入座

        // ---- 房间就绪层（2026-10-28 开局流程改版：房主配模式+客人准备+房主开局；见 网络协议.md §12.2）----
        RoomSettings = 136,          // 上行：房主配置模式（MsgRoomSettings：先后手方式+洗牌；仅椅位0、仅 Waiting）
        RoomReady = 137,             // 上行：准备 toggle（MsgRoomReady；Waiting 相位）
        RoomStart = 138,             // 上行：房主开始（空载荷；校验双方就座+客人已准备 → 进选卡组相位）
    }

    /// <summary>协议版本常量。信封 ProtocolVersion 读到 0 视为 legacy（无版本草稿期数据）。
    /// v2（2026-10-28 P2 元素席位收缩）：ManaType 白/黑退役、Any 6→4，位置数组 7→5 重排——
    /// 裸 int 色号/数组布局全线漂移，新旧端互拒不兼容（无兼容层）。</summary>
    public static class NetworkProtocol
    {
        public const ushort Version = 2;
    }

    [MemoryPackable]
    public partial class NetworkMessage
    {
        [MemoryPackOrder(TagTable.NM_MessageType)]
        public NetworkMessageType Type;

        [MemoryPackOrder(TagTable.NM_SequenceId)]
        public uint SequenceId;

        [MemoryPackOrder(TagTable.NM_Payload)]
        public byte[] Payload;

        [MemoryPackOrder(TagTable.NM_Timestamp)]
        public long Timestamp;

        /// <summary>协议版本（M1 起填 NetworkProtocol.Version；0=legacy）。</summary>
        [MemoryPackOrder(TagTable.NM_ProtocolVersion)]
        public ushort ProtocolVersion;
    }
}
