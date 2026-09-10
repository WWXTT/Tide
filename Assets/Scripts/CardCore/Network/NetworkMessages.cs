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
        IntentTapForElement = 101,   // 横置产元素（颜色是玩家决策）
        IntentAddToElementPool = 102,// 放地进元素池
        IntentSkipStandby = 103,     // 跳过准备阶段
        IntentDeclareAttack = 104,
        IntentDeclareBlock = 105,    // 预留：引擎有 API 零调用
        IntentActivateEffect = 106,
        IntentPassPriority = 107,
        IntentEndTurn = 108,
        IntentConcede = 109,         // 认输：EndGame(opponent, Concede)
        SelectResponse = 110,        // 反问应答（MsgSelectResponse）

        // ---- 下行（服务器→客户端）----
        SelectRequest = 120,         // 反问请求（MsgSelectRequest）
        NetEventBatch = 121,         // 事件流批次（MsgNetEventBatch）
        GameStateSyncV2 = 122,       // 全量快照（MsgGameStateSync，viewerSeat 视角）
        Ping = 123,
        Pong = 124,
    }

    /// <summary>协议版本常量。信封 ProtocolVersion 读到 0 视为 legacy（无版本草稿期数据）。</summary>
    public static class NetworkProtocol
    {
        public const ushort Version = 1;
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
