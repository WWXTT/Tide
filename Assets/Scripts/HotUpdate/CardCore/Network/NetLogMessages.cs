using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    /// <summary>
    /// 战报行流 DTO（2026-11-11 C 案，协议 v15）：日志从「事件投影的副产品」升为服务端渲染的
    /// 一等产物——服务端用 MatchLogRenderer 逐视角渲染成最终行（MatchLogLine）后按此形态下发，
    /// 客户端零渲染纯 append。行序/遮蔽/旗标全部由服务端单源定（MatchLogLineStream）。
    /// 实测 13.7–49.9 KB/局 ≈ 事件流的 8.5%。
    ///
    /// 战报行：Kind=语义色编码（MatchLogLineKind）、Flags=时序旗标（MatchLogLineFlags——
    /// 面板自动开合/终局收口）、EventId=源事件锚（对拍/排查用；自然抽挂起放行会乱序，
    /// 不得作去重键）、Text=服务端渲染好的最终文本。
    /// </summary>
    [MemoryPackable]
    public partial class NetLogLine
    {
        [MemoryPackOrder(TagTable.NLL_Kind)]
        public byte Kind;

        [MemoryPackOrder(TagTable.NLL_Flags)]
        public byte Flags;

        [MemoryPackOrder(TagTable.NLL_EventId)]
        public uint EventId;

        [MemoryPackOrder(TagTable.NLL_Text)]
        public string Text;
    }

    /// <summary>战报行流批次（下行；64 行/批按成员 LogCursor 切片）。TCP 有序 ⇒ 客户端按到达序 append。</summary>
    [MemoryPackable]
    public partial class MsgNetLogBatch
    {
        [MemoryPackOrder(TagTable.MNLB_Lines)]
        public NetLogLine[] Lines;
    }
}
