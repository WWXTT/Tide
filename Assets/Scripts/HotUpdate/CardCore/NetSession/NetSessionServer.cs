using System;

namespace CardCore.Network
{
    /// <summary>
    /// 会话服务器组合根（M2）：INetTransportHost（TCP 第一实现）+ NetRoom（单房间状态机）+ 逻辑线程泵。
    ///
    /// 单逻辑线程定案（网络协议.md §9.4）的落地形态：
    /// - IO 线程（accept/读）只入队；**一切房间/引擎状态只在本类 Pump 里碰**。
    /// - Pump 的宿主：编辑器菜单 = EditorApplication.update；batchmode = 阻塞自旋；
    ///   验证器 = 同步直调（三宿主同一份泵代码）；P2P 房主运行时 = UIBootstrap.Update（2026-10-21）。
    /// - 出站单写者 = Pump 线程（INetChannel.Send 注释）。
    /// - 引擎推进在 NetRoom.PumpEngine：相位由玩家 intent 驱动（与全部无头驱动器同口径，
    ///   不调 GameCore.Update——其空栈分支会自由滑阶段）；UniTask 链在逻辑线程同步收敛
    ///   （人类反问经 NetworkTargetSelector 的 TCS 内联完成）。
    /// </summary>
    public sealed class NetSessionServer
    {
        // TCP 第一实现（2026-10-21 P2P 定案：传输缝=INetTransportHost，换第二传输注入这里）
        // 2026-10-23 TapTap 接入：构造可注入组合宿主（TCP 回环 + TapBattleTransportHost）
        private readonly INetTransportHost _host;
        private readonly NetRoom _room;

        /// <summary>逻辑线程泵宿主接进来的回调（编辑器 update / batchmode 自旋共用）。</summary>
        private readonly System.Action<string> _log;

        public NetSessionServer(string roomId, System.Action<string> log = null, int? seed = null,
            INetTransportHost host = null)
        {
            _host = host ?? new NetTcpHost();
            _room = new NetRoom(roomId, seed);
            _log = log;
        }

        public NetRoom Room => _room;
        public int Port => _host.Port;

        /// <summary>是否有会话服务器实例在运行（2026-09-24）：UIBootstrap.Update 的引擎驱动闸
        /// （同 NetLobbyServer.IsRunning——同进程宿主对局期间 GameCore 由服务器泵独占驱动）。</summary>
        public static bool IsRunning { get; private set; }

        /// <summary>当前进程的会话服务器实例（Start 登记 / Stop 清空）——
        /// P2P 房主运行时（2026-10-21）：UIBootstrap.Update 经此取泵驱动引擎（单逻辑线程纪律）。</summary>
        public static NetSessionServer Current { get; private set; }

        /// <summary>开始监听（port=0 由 OS 分配空闲口）。</summary>
        public void Start(int port)
        {
            _host.Start(port);
            IsRunning = true;
            Current = this;
            _log?.Invoke($"[NetSession] 会话服务器监听 0.0.0.0:{_host.Port}（单房间单进程）");
        }

        /// <summary>停机：关监听、断全部连接、拆对局接线。</summary>
        public void Stop()
        {
            IsRunning = false;
            if (ReferenceEquals(Current, this)) Current = null;
            _host.Stop();
            _room.TeardownIfActive(); // 2026-10-03：兑现「拆对局接线」——不再 Pump 观察不到断线，
                                      // 反问桥若不在此卸载会残留全局（TargetSelectionService.Current）
            _log?.Invoke("[NetSession] 已停止");
        }

        /// <summary>
        /// 逻辑线程单步（宿主每 tick 调用）：连接收发 → 上行分派 → 引擎推进 → 下行出队。
        /// 单房间单进程：GameCore.Instance 即本房间的引擎（静态单例定案不动）。
        /// </summary>
        public void Pump()
        {
            // ① 连接生命周期：吸收新连接、回收断开（房间侧作废在 OnDisconnect 内收口）
            foreach (var dropped in _host.Poll())
                _room.OnDisconnect(dropped);
            _room.ResetIfEmpty();

            // ② 上行：逐连接排干入站队列 → 房间状态机/intent/反问分派
            foreach (var conn in _host.Connections)
            {
                while (conn.TryReceive(out var msg))
                    _room.OnMessage(GameCore.Instance, conn, msg);
            }

            // ③ 引擎推进（响应窗口排干 + End 折返——相位由 intent 驱动，见 NetRoom.PumpEngine）
            _room.PumpEngine(GameCore.Instance);

            // ④ 下行出队：RoomState 广播 → 事件流（按座位过滤）→ 静默点快照
            _room.FlushDownlink(GameCore.Instance);
        }
    }
}
