using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    // ============================================================
    // 开局握手（2026-09-22 定案，同日按"只核对卡组引用"口径收缩）：
    //
    // **不做全池对比**（不比对整个 Cards.json/Effects.json，无"共享牌库版本"概念）——
    // 只核对**卡组引用闭包**：卡/效果 ID 本身是内容哈希（ID 同则内容同），
    // 唯一需要内容核对的是闭包引用到的**原子表行**（行 ID 稳定、内容是平衡层可变），
    // 以 NetDeckDigest 摘要收口。对手侧与未来涉及面更广的校验遵循**用到再核对**原则
    //（对局过程中按实际出现的 ID/行核对），开局一律不做全量对比。
    //
    // 流程（M2 会话层驱动；M1 定消息 + 校验逻辑，回环验证器覆盖）：
    //   1. 客户端 → 服务器 DeckSubmit(111)：卡组 cardIds + 本地闭包原子行摘要。
    //   2. 服务器 NetMatchHandshake.ValidateDeckSubmit：卡表命中 / 卡组不重复 /
    //      效果-原子引用闭环 / 本地摘要与服务器按同一卡组计算的摘要比对——
    //      任一失败 = Error 帧(99) 拒绝，不再打出"缺卡对局"。
    //   3. 双座位通过后，服务器 → 各客户端 MatchManifest(125)：己方卡组回显 +
    //      对手仅数量（卡组构成隐藏）+ 服务器按该卡组计算的摘要——客户端核对后进局。
    // 详见根目录 网络协议.md §11。
    // ============================================================

    /// <summary>卡组引用闭包的原子行摘要：闭包内全部 refId 对应表行内容（排序后哈希）。
    /// 行数仅供拒绝文案诊断。卡/效果层无需内容摘要——ID 即内容哈希，命中即一致。</summary>
    [MemoryPackable]
    public partial class NetDeckDigest
    {
        [MemoryPackOrder(TagTable.NDD_AtomicRowsHash)]
        public string AtomicRowsHash;

        [MemoryPackOrder(TagTable.NDD_AtomicRowCount)]
        public int AtomicRowCount;
    }

    /// <summary>上行：卡组提交（对局开始前）。CardIds 为 Cards.json 卡 ID（C_ 前缀内容哈希）；
    /// Digest 为提交方本地按同一卡组计算的原子行摘要，服务器与自身计算结果比对。</summary>
    [MemoryPackable]
    public partial class MsgDeckSubmit
    {
        [MemoryPackOrder(TagTable.MDS_DeckName)]
        public string DeckName;

        [MemoryPackOrder(TagTable.MDS_CardIds)]
        public string[] CardIds;

        [MemoryPackOrder(TagTable.MDS_Digest)]
        public NetDeckDigest Digest;
    }

    /// <summary>下行：对局清单（双座位 DeckSubmit 校验通过后下发）。
    /// OwnCardIds=己方卡组回显（与提交序一致）；对手只给数量（卡组构成隐藏）；
    /// OwnDeckDigest=服务器按该座位卡组计算的原子行摘要，客户端与本地计算比对。</summary>
    [MemoryPackable]
    public partial class MsgMatchManifest
    {
        [MemoryPackOrder(TagTable.MMM_OwnSeat)]
        public int OwnSeat;

        [MemoryPackOrder(TagTable.MMM_OwnCardIds)]
        public string[] OwnCardIds;

        [MemoryPackOrder(TagTable.MMM_OpponentCardCount)]
        public int OpponentCardCount;

        [MemoryPackOrder(TagTable.MMM_OwnDeckDigest)]
        public NetDeckDigest OwnDeckDigest;
    }
}
