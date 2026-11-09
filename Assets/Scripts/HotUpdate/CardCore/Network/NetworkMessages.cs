using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    /// <summary>
    /// 网络消息类型（M1 协议定版 2026-09-10；2026-10-31 阶段D 全量重排，无历史包袱）。
    /// 分组连续编号：None=0 → 上行 intent（100 起）→ 下行 → 大厅层 → 房间就绪层，组间不留空号。
    /// 详见根目录 网络协议.md。
    /// </summary>
    public enum NetworkMessageType : int
    {
        None = 0,

        // ---- 上行 intent（客户端→服务器，payload 见 IntentMessages.cs；2026-10-31 全量重排：
        //      IntentPlayCard 起连续无洞——原 101 空洞（IntentTapForElement 2026-10-11 删）随重排闭合）----
        IntentPlayCard = 100,        // 三路合一：FromZone=Hand/Graveyard；服务器按"栈非空且持优先权"分派 PlayCardInResponse
        IntentAddToElementPool = 101,// 放地进元素池
        IntentSkipStandby = 102,     // 跳过准备阶段
        IntentDeclareAttack = 103,
        IntentDeclareBlock = 104,    // 预留：引擎有 API 零调用
        IntentActivateEffect = 105,
        IntentPassPriority = 106,
        IntentEndTurn = 107,
        IntentConcede = 108,         // 认输：EndGame(opponent, Concede)
        SelectResponse = 109,        // 反问应答（MsgSelectResponse）
        DeckSubmit = 110,            // 开局握手：卡组提交（MsgDeckSubmit，见 网络协议.md §11）
        JoinRoom = 111,              // 会话层：进房分座/观战登记（MsgJoinRoom，见 网络协议.md §12）
        IntentActivateHeroSkill = 112, // 英雄技能发动（MsgIntentActivateHeroSkill——2026-10-21 协议补课；离散位在会话层消息之后）
        IntentBuildCard = 113,       // 充能（2026-11-05 系统动作化改义）：任意手牌卡宣充能（MsgIntentBuildCard；结算期反问选色——Color 恒 −1 协议兼容）
        TextResponse = 114,          // 文本反问应答（MsgTextResponse——2026-10-31 阶段C：卡名宣言开放集输入）

        // ---- 下行（服务器→客户端）----
        SelectRequest = 115,         // 反问请求（MsgSelectRequest）
        NetEventBatch = 116,         // 事件流批次（MsgNetEventBatch）
        GameStateSyncV2 = 117,       // 全量快照（MsgGameStateSync，viewerSeat 视角）
        Ping = 118,
        Pong = 119,
        MatchManifest = 120,         // 开局握手：对局清单（MsgMatchManifest，见 网络协议.md §11）
        RoomState = 121,             // 会话层：房间全量状态广播（MsgRoomState，见 网络协议.md §12）
        TextRequest = 122,           // 文本反问请求（MsgTextRequest——2026-10-31 阶段C；按 ChooserSeat 定向下发，预言类隐秘宣言不下发对手）
        Error = 123,                 // 下行通用拒绝帧（MsgError{Reason, Context}）——intent 拒绝/握手拒绝/房间拒绝/大厅拒绝共用

        // ---- 大厅层（L1，2026-09-24：房间列表 + 自动匹配 + AI 填位，见 网络协议.md §13）----
        LobbyHello = 124,            // 上行：进大厅（MsgLobbyHello）→ 服务器回 LobbyState
        LobbyState = 125,            // 下行：大厅全量状态（MsgLobbyState：房间列表 + 排队数）
        LobbyCreateRoom = 126,       // 上行：创建房间（MsgLobbyCreateRoom）——单房串行：房间占用即拒
        LobbyJoinRoom = 127,         // 上行：从房间列表加入（MsgLobbyJoinRoom）→ 内部走 JoinRoom 分座
        LobbyAutoMatch = 128,        // 上行：自动匹配排队/取消（空载荷；重复发送 = 取消排队）
        LobbyAddAi = 129,            // 上行：AI 填位（MsgLobbyAddAi）——服务器本机回环 NetClientBrain 入座

        // ---- 房间就绪层（2026-10-28 开局流程改版：房主配模式+客人准备+房主开局；见 网络协议.md §12.2）----
        RoomSettings = 130,          // 上行：房主配置模式（MsgRoomSettings：先后手方式+洗牌；仅椅位0、仅 Waiting）
        RoomReady = 131,             // 上行：准备 toggle（MsgRoomReady；Waiting 相位）
        RoomStart = 132,             // 上行：房主开始（空载荷；校验双方就座+客人已准备 → 进选卡组相位）
        MatchSummary = 133,          // 下行：终局摘要（MsgMatchSummary，2026-11-09 协议 v11 对局记录——
                                     // 双座位各收一份：对手完整卡组+自建内容 payload+胜负，客户端落 EncounterRecord）
    }

    /// <summary>协议版本常量。信封 ProtocolVersion 读到 0 视为 legacy（无版本草稿期数据）。
    /// v2（2026-10-28 P2 元素席位收缩）：ManaType 白/黑退役、Any 6→4，位置数组 7→5 重排——
    /// 裸 int 色号/数组布局全线漂移，新旧端互拒不兼容（无兼容层）。
    /// v3（2026-10-29 P6）：MsgIntentPlayCard 新增 AwakenCast 布尔——MemoryPack 载荷布局变更，
    /// 新旧端互拒不兼容（无兼容层）。该字段已于 v9 删除。
    /// v4（2026-10-29 P7 建造→2026-10-31 P12 改义充能）：上行 MsgIntentBuildCard（卡+色）语义换为充能，
    /// 下行快照 RuntimeCardState 新增 BuildPaid（手牌卡已建额——只随持有者手牌区下发）——
    /// MemoryPack 载荷布局变更，新旧端互拒（无兼容层）。
    /// v5（2026-10-29 P9b 衍生物槽绑定入卡组）：MsgDeckSubmit 新增 TokenCardIds/TokenBindings、
    /// 新消息 MsgDeckTokenBinding、NetDeckDigest 新增 TokenSig——卡组摘要口径扩展
    /// （同卡表不同绑定=不同摘要），MemoryPack 载荷布局变更，新旧端互拒不兼容（无兼容层）。
    /// v6（2026-10-31 阶段C 文本反问）：新消息 MsgTextRequest（下行）/MsgTextResponse（上行）
    /// ——卡名宣言等开放集输入（检索/强夺/窃取结算期反问）。纯新增消息、存量载荷布局零改；
    /// 升版防旧端（不识别两消息）与新端互连半开死等。
    /// v7（2026-10-31 阶段D 全量重排，无历史包袱）：NetworkMessageType 全量重排——0-99 草稿死值段
    /// 删除、101 空洞闭合、分组连续编号（上行/下行/大厅/就绪四段无空号）；TagTable 手工定值段
    /// 整体退役（六个常量并入哈希生成口径）。消息号与 MemoryPack 标签口径全线漂移——
    /// 新旧端互拒不兼容（无兼容层）。
    /// v8（2026-11-05 ID 冻结收口·摘要三层收口）：NetDeckDigest 新增 ContentSig（卡 ConfigHash +
    /// 效果 hash 指纹摘要）——ID 命中不再蕴含内容一致，旧端缺该段会被新端显式拒绝；
    /// 同批起接收侧对信封 ProtocolVersion 设闸（此前只写不校验），失配回显式 Error 帧而非
    /// 靠序列化错位自然崩。
    /// v9（2026-11-06 协议瘦身）：MsgIntentPlayCard 删除废弃字段 AwakenCast（v3 引入、
    /// 机制删除后曾留读兼容）——成员 5→4，MemoryPack 载荷布局变更，新旧端互拒（无兼容层）。
    /// v10（2026-11-08 目标选择重构步5）：MsgIntentPlayCard/MsgIntentActivateEffect 新增
    /// References（参照选位分角色上送）、StackItemDTO 新增 References、MsgSelectRequest 新增
    /// SpecKey（spec 计划显式关联键，§3.3）——布局变更，新旧端互拒（无兼容层）。
    /// v11（2026-11-09 自建内容跨端+对局记录）：MsgDeckSubmit 新增 AddedCards/AddedEffects/AddedRows
    ///（卡组闭包中非基础池内容完整 JSON——对端缺卡不再拒局）、MsgMatchManifest 新增
    /// OpponentCards/Effects/Rows（渲染用内容定义，卡组构成仍隐藏）、新消息 MsgMatchSummary
    ///（终局摘要：对手完整卡组+自建内容+胜负，落对局记录）——布局变更，新旧端互拒（无兼容层）。</summary>
    public static class NetworkProtocol
    {
        public const ushort Version = 11;

        /// <summary>接收侧版本闸（v8 起）：信封版本与本端不符 = 新旧端互拒（无兼容层）。
        /// 0=legacy 草稿期数据，同样失配。</summary>
        public static bool IsCompatible(ushort receivedVersion) => receivedVersion == Version;

        /// <summary>失配拒绝文案（Error 帧 / 断连日志共用单源）。</summary>
        public static string MismatchText(ushort receivedVersion)
            => $"协议不兼容：对端 v{receivedVersion}（本端 v{Version}）——请升级两端到相同版本";
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
