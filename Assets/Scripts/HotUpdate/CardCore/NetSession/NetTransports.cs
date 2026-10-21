using System.Collections.Generic;

namespace CardCore.Network
{
    /// <summary>
    /// 会话层对一条成员连接的全部依赖（传输无关，2026-10-21 P2P 定案抽取）：
    /// 出站帧写（单写者=逻辑线程）+ 入站队列（IO 侧生产，逻辑线程 TryReceive 消费）
    /// + 房间侧状态（椅位/引擎座位/昵称/游标/脏标记——逻辑线程独占）。
    ///
    /// TCP 实现 = NetClientConnection（NetTcpHost 监听接受）；未来 Steam P2P 等第二传输
    /// 实现本口 + INetTransportHost 即可挂进同一会话层——线格式（FrameCodec）/房间状态机/
    /// 引擎接线零改动（「留 Steam 口子不搭中继」定案的落点）。
    /// </summary>
    public interface INetChannel
    {
        /// <summary>IO 线程发现断开（读 EOF/异常）或逻辑线程写失败时置位；由 Pump 轮询回收。</summary>
        bool IsDisconnected { get; }

        /// <summary>进房后的椅子位（0/1 玩家位；-1 观战；-2 未进房——首条消息必须是 JoinRoom）。</summary>
        int ChairSeat { get; set; }

        /// <summary>本局引擎座位（换先手后与 ChairSeat 可能不同；快照视角/intent 座位用它）。观战=-1。</summary>
        int MatchSeat { get; set; }

        /// <summary>进房昵称（RoomState 广播用）。</summary>
        string Nickname { get; set; }

        /// <summary>事件流出队游标（NetEventProjector 全局列表下标，房间按局重置）。</summary>
        int EventCursor { get; set; }

        /// <summary>静默点快照脏标记（本 tick 有事件/intent 且转入静默时置位，发完清零）。</summary>
        bool SnapshotDirty { get; set; }

        /// <summary>出站：封帧直写（仅逻辑线程调用）。失败置断线标记，不抛。</summary>
        void Send(NetworkMessage message);

        /// <summary>出站便捷口：信封化 + 直写（仅逻辑线程）。</summary>
        void SendPayload<T>(NetworkMessageType type, T payload) where T : class;

        /// <summary>逻辑线程收件：取空返回 false。</summary>
        bool TryReceive(out NetworkMessage message);

        /// <summary>关闭连接（逻辑线程调用）。</summary>
        void Close();
    }

    /// <summary>
    /// 房主侧监听宿主抽象（2026-10-21 P2P）：生命周期事件（新连接/断开）不在 IO 线程回调——
    /// 全部经 Poll() 在逻辑线程发现（单逻辑线程纪律）。TCP 实现 = NetTcpHost。
    /// </summary>
    public interface INetTransportHost
    {
        /// <summary>实际绑定端口（Start(port=0) 时由 OS 分配）。</summary>
        int Port { get; }

        /// <summary>开始监听。port=0 由 OS 分配空闲口。</summary>
        void Start(int port);

        /// <summary>停止监听并关闭全部连接。</summary>
        void Stop();

        /// <summary>
        /// 逻辑线程泵：吸收新连接、剔除断开连接。返回本 tick 剔除的断开连接列表
        ///（调用方处理房间侧作废）。
        /// </summary>
        List<INetChannel> Poll();

        /// <summary>当前活跃连接快照（逻辑线程调用）。</summary>
        IReadOnlyList<INetChannel> Connections { get; }
    }
}
