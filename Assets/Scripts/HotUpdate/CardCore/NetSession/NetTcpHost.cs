using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CardCore.Network
{
    /// <summary>
    /// 单条客户端连接（M2 会话层传输件）：一条读线程 + 一条入站队列。
    ///
    /// 线程模型（网络协议.md §9.4 单逻辑线程定案的落地）：
    /// - 读线程只做 socket 读 → FrameDecoder（每连接独立实例，非线程安全）→ 反序列化 →
    ///   入站队列。IO 线程不碰引擎、不碰房间状态。
    /// - 出站**单写者 = 逻辑线程**：Send 只允许宿主泵（NetSessionServer.Pump）调用——
    ///   per-connection 流无并发写，无锁。写失败置 IsDisconnected，由泵统一回收。
    /// </summary>
    public sealed class NetClientConnection
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly Thread _readThread;
        private readonly FrameCodec.FrameDecoder _decoder = new FrameCodec.FrameDecoder();
        private readonly ConcurrentQueue<NetworkMessage> _inbound = new ConcurrentQueue<NetworkMessage>();

        /// <summary>IO 线程发现断开（读 EOF/异常）或逻辑线程写失败时置位；由 Pump 轮询回收。</summary>
        internal volatile bool IsDisconnected;

        // ---- 房间侧附加状态（逻辑线程独占读写）----
        /// <summary>进房后的椅子位（0/1 玩家位；-1 观战；-2 未进房——首条消息必须是 JoinRoom）。</summary>
        public int ChairSeat { get; set; } = -2;
        /// <summary>本局引擎座位（换先手后与 ChairSeat 可能不同；快照视角/intent 座位用它）。观战=-1。</summary>
        public int MatchSeat { get; set; } = -1;
        /// <summary>进房昵称（RoomState 广播用）。</summary>
        public string Nickname { get; set; } = "";
        /// <summary>事件流出队游标（NetEventProjector 全局列表下标，房间按局重置）。</summary>
        public int EventCursor;
        /// <summary>静默点快照脏标记（本 tick 有事件/intent 且转入静默时置位，发完清零）。</summary>
        public bool SnapshotDirty;

        internal NetClientConnection(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "NetSession-Read" };
        }

        internal void Start() => _readThread.Start(_client);

        /// <summary>出站：封帧直写（仅逻辑线程调用）。失败置断线标记，不抛——房间由 Pump 统一收口。</summary>
        public void Send(NetworkMessage message)
        {
            if (IsDisconnected) return;
            try
            {
                var frame = FrameCodec.Frame(message);
                _stream.Write(frame, 0, frame.Length);
                _stream.Flush();
            }
            catch (Exception)
            {
                IsDisconnected = true;
            }
        }

        /// <summary>出站便捷口：信封化 + 直写（仅逻辑线程）。</summary>
        public void SendPayload<T>(NetworkMessageType type, T payload) where T : class
            => Send(NetworkSerializer.BuildEnvelope(type, payload));

        /// <summary>逻辑线程收件：取空返回 false。</summary>
        internal bool TryReceive(out NetworkMessage message) => _inbound.TryDequeue(out message);

        /// <summary>读线程主体：阻塞读 → 喂解码器 → 逐帧入队。任何退出路径都置断线标记。</summary>
        private void ReadLoop(object state)
        {
            var buffer = new byte[16 * 1024];
            try
            {
                while (true)
                {
                    int read = _stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break; // 对端优雅关闭
                    _decoder.Append(buffer, 0, read);
                    while (_decoder.TryDecode(out var message))
                        _inbound.Enqueue(message);
                }
            }
            catch (Exception)
            {
                // 帧损坏抛 InvalidOperationException / 连接重置抛 IOException：统一按断线收口
            }
            finally
            {
                IsDisconnected = true;
            }
        }

        /// <summary>关闭连接（逻辑线程调用；读线程随流关闭退出）。</summary>
        internal void Close()
        {
            IsDisconnected = true;
            try { _client.Close(); } catch (Exception) { }
        }
    }

    /// <summary>
    /// TCP 监听宿主（M2）：后台接受线程 + 已接受连接列表。连接的生命周期事件
    /// （新连接/断开）不在 IO 线程回调——全部经 Poll() 在逻辑线程发现，保持单逻辑线程纪律。
    /// </summary>
    public sealed class NetTcpHost
    {
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private readonly ConcurrentQueue<TcpClient> _accepted = new ConcurrentQueue<TcpClient>();
        private readonly List<NetClientConnection> _connections = new List<NetClientConnection>();

        /// <summary>实际绑定端口（Start(port=0) 时由 OS 分配——验证器用）。</summary>
        public int Port { get; private set; }

        /// <summary>当前活跃连接快照（逻辑线程调用）。</summary>
        public IReadOnlyList<NetClientConnection> Connections => _connections;

        /// <summary>开始监听。port=0 由 OS 分配空闲口。</summary>
        public void Start(int port)
        {
            if (_running) return;
            _running = true;
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "NetSession-Accept" };
            _acceptThread.Start();
        }

        /// <summary>停止监听并关闭全部连接。</summary>
        public void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _listener.Stop(); } catch (Exception) { }
            foreach (var conn in _connections)
                conn.Close();
            _connections.Clear();
        }

        /// <summary>
        /// 逻辑线程泵：吸收新接受连接（启动读线程）、剔除断开连接（回调宿主收口）。
        /// 返回本 tick 剔除的断开连接列表（调用方处理房间侧作废）。
        /// </summary>
        public List<NetClientConnection> Poll()
        {
            while (_accepted.TryDequeue(out var client))
            {
                var conn = new NetClientConnection(client);
                _connections.Add(conn);
                conn.Start();
            }

            var dropped = new List<NetClientConnection>();
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                var conn = _connections[i];
                if (!conn.IsDisconnected) continue;
                _connections.RemoveAt(i);
                conn.Close();
                dropped.Add(conn);
            }
            return dropped;
        }

        private void AcceptLoop()
        {
            try
            {
                while (_running)
                {
                    var client = _listener.AcceptTcpClient(); // 阻塞；Stop 关 listener 即退出
                    _accepted.Enqueue(client);
                }
            }
            catch (Exception)
            {
                // Stop() 触发的 SocketException：正常退出
            }
        }
    }
}
