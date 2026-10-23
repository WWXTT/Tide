#if !UNITY_WEBGL
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using CardCore.Network;

namespace CardCore.Network
{
    /// <summary>
    /// 客户端侧传输缝（2026-10-23 TapTap 联机接入抽取）：NetGameClient 的会话逻辑
    ///（进房/握手/下行分派/事件回调）与具体 IO 解耦——TCP 直连（局域网）与 TapTap 房间
    /// 中转两个实现共用同一 NetGameClient。
    ///
    /// 线程纪律与 INetChannel 同款：出站 Send 仅逻辑线程调用；入站经 TryReceive 在逻辑线程
    /// 消费（IO 侧只生产队列）。
    /// </summary>
    public interface INetClientTransport
    {
        /// <summary>对端描述（日志/展示用，如 "192.168.1.2:28000" / "taptap:玩家ID"）。</summary>
        string Remote { get; }

        /// <summary>IO 侧发现断开或发送失败时置位；NetGameClient.Pump 观察后统一收口事件。</summary>
        bool Disconnected { get; }

        /// <summary>出站一条消息（仅逻辑线程调用）。失败置 Disconnected，不抛。</summary>
        void Send(NetworkMessage message);

        /// <summary>每帧驱动（逻辑线程）：TapTap 实现在此排干限频发送队列；TCP 实现空操作。</summary>
        void Pump();

        /// <summary>非阻塞收一件；取空返回 false。</summary>
        bool TryReceive(out NetworkMessage message);

        /// <summary>关闭连接/注销回调路由。</summary>
        void Close();
    }

    /// <summary>
    /// TCP 直连传输（NetGameClient 原 IO 逻辑原样搬移，行为等价重构）：阻塞 Connect +
    /// DataAvailable 轮询读 + FrameDecoder 半包续接。局域网/回环路径继续使用。
    /// </summary>
    public sealed class TcpClientTransport : INetClientTransport
    {
        private readonly TcpClient _tcp;
        private NetworkStream _stream;
        private readonly FrameCodec.FrameDecoder _decoder = new FrameCodec.FrameDecoder();
        private readonly ConcurrentQueue<NetworkMessage> _ready = new ConcurrentQueue<NetworkMessage>();
        private readonly byte[] _buf = new byte[16 * 1024];
        private volatile bool _disconnected;

        public string Remote { get; }
        public bool Disconnected => _disconnected;

        /// <summary>阻塞直连（失败抛——调用方在线程池上跑，见 LobbyScreen.ConnectAsync）。</summary>
        public TcpClientTransport(string host, int port)
        {
            Remote = $"{host}:{port}";
            _tcp = new TcpClient();
            try
            {
                _tcp.Connect(host, port);
                _stream = _tcp.GetStream();
            }
            catch (Exception)
            {
                TryCloseSocket();
                throw;
            }
        }

        public void Send(NetworkMessage message)
        {
            if (_disconnected || _stream == null) return;
            try
            {
                var frame = FrameCodec.Frame(message);
                _stream.Write(frame, 0, frame.Length);
                _stream.Flush();
            }
            catch (Exception) { _disconnected = true; }
        }

        public void Pump() { }

        /// <summary>非阻塞收一轮（DataAvailable 轮询）→ 解码器 → 就绪队列逐条吐出。</summary>
        public bool TryReceive(out NetworkMessage message)
        {
            if (!_disconnected && _stream != null)
            {
                while (_stream.DataAvailable)
                {
                    int n;
                    try { n = _stream.Read(_buf, 0, _buf.Length); }
                    catch (Exception) { _disconnected = true; break; }
                    if (n <= 0) { _disconnected = true; break; }
                    _decoder.Append(_buf, 0, n);
                    while (_decoder.TryDecode(out var decoded))
                        _ready.Enqueue(decoded);
                }
            }
            return _ready.TryDequeue(out message);
        }

        public void Close()
        {
            _disconnected = true;
            TryCloseSocket();
        }

        private void TryCloseSocket()
        {
            try { _stream?.Close(); } catch (Exception) { }
            try { _tcp?.Close(); } catch (Exception) { }
        }
    }
}
#endif
