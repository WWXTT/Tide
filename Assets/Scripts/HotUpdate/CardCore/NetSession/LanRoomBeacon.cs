using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CardCore;

namespace CardCore.Network
{
    /// <summary>
    /// 局域网房间信标（2026-10-28 联机加入流改造：房主开房即在局域网应答发现探测，客人不再手输 IP）。
    /// 28001 端口三路应答并行：
    /// ① UDP 应答——收到任意数据报即回一行房间信息（客人主动探测路径，模拟器 slirp NAT 经 10.0.2.2 可达）；
    /// ② UDP 周期广播——每 1s 向 255.255.255.255 广播（真机同一局域网被动发现；slirp 不转发入向广播，模拟器靠①）；
    /// ③ TCP 应答——连上即回一行即断（UDP 被 NAT/防火墙拦时的兜底路径）。
    /// 线程安全：payloadProvider 返回宿主主线程维护的快照串（volatile 语义由委托宿主保证），信标线程只读不碰游戏状态。
    /// 纯 BCL 线程/套接字（TideServer 共享源同编译，无 Unity 依赖）。
    /// 同机双端（编辑器双开）下 UDP 28001 独占绑定会失败——信标缺席只退化为手输 IP，不阻塞建房。
    /// </summary>
    public sealed class LanRoomBeacon : IDisposable
    {
        public const int DiscoveryPort = 28001;
        public const string Prefix = "TIDEROOM|";

        private readonly Func<string> _payloadProvider;
        private volatile bool _running;
        private bool _everStarted;
        private UdpClient _replySocket;     // 28001 收探测/回应答
        private UdpClient _broadcastSocket; // 临时口出广播（不与应答口抢收发）
        private TcpListener _tcp;           // 28001 TCP 应答
        private Thread _replyThread, _broadcastThread, _tcpThread;

        public LanRoomBeacon(Func<string> payloadProvider)
        {
            _payloadProvider = payloadProvider ?? throw new ArgumentNullException(nameof(payloadProvider));
        }

        public bool IsRunning => _running;

        /// <summary>启动信标（UDP/TCP 任一绑定成功即算活；全失败返回 false——加入方手输 IP 兜底）。</summary>
        public bool Start()
        {
            if (_running) return true;
            try
            {
                _replySocket = new UdpClient(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                _replySocket.Client.ReceiveTimeout = 400;
            }
            catch (Exception) { _replySocket = null; }
            try
            {
                _broadcastSocket = new UdpClient();
                _broadcastSocket.EnableBroadcast = true;
            }
            catch (Exception) { _broadcastSocket = null; }
            try
            {
                _tcp = new TcpListener(IPAddress.Any, DiscoveryPort);
                _tcp.Start();
            }
            catch (Exception) { _tcp = null; }
            if (_replySocket == null && _tcp == null)
            {
                TideLog.Warn("[LanBeacon] UDP/TCP 28001 均绑定失败——局域网发现不可用（手输 IP 仍可加入）");
                CleanupSockets();
                return false;
            }

            _running = true;
            _everStarted = true;
            _replyThread = _replySocket != null
                ? StartThread("LanBeacon-UdpReply", ReplyLoop)
                : null;
            _broadcastThread = _broadcastSocket != null
                ? StartThread("LanBeacon-Broadcast", BroadcastLoop)
                : null;
            _tcpThread = _tcp != null
                ? StartThread("LanBeacon-Tcp", TcpLoop)
                : null;
            TideLog.Info($"[LanBeacon] 信标已启动（UDP应答={_replySocket != null} 广播={_broadcastSocket != null} TCP应答={_tcp != null}）");
            return true;
        }

        public void Dispose() => Stop();

        public void Stop()
        {
            _running = false;
            CleanupSockets();
            JoinThread(_replyThread);
            JoinThread(_broadcastThread);
            JoinThread(_tcpThread);
            _replyThread = _broadcastThread = _tcpThread = null;
            if (_everStarted)
            {
                _everStarted = false;
                TideLog.Info("[LanBeacon] 信标已停止");
            }
        }

        private Thread StartThread(string name, Action loop)
        {
            var t = new Thread(() =>
            {
                try { loop(); }
                catch (Exception ex) { TideLog.Warn($"[LanBeacon] {name} 线程退出：{ex.GetType().Name} {ex.Message}"); }
            })
            { IsBackground = true, Name = name };
            t.Start();
            return t;
        }

        /// <summary>UDP 应答循环：收任意包即回房间信息（超时空转，关闭即退）。</summary>
        private void ReplyLoop()
        {
            var udp = _replySocket;
            while (_running && udp != null)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    var data = udp.Receive(ref remote);
                    if (data.Length == 0) continue;
                    var payload = Encoding.UTF8.GetBytes(Prefix + _payloadProvider());
                    udp.Send(payload, payload.Length, remote);
                }
                catch (SocketException) { /* ReceiveTimeout 到点空转 */ }
                catch (ObjectDisposedException) { break; }
            }
        }

        /// <summary>UDP 广播循环：每 1s 向局域网广播一次房间信息。</summary>
        private void BroadcastLoop()
        {
            var udp = _broadcastSocket;
            while (_running && udp != null)
            {
                try
                {
                    var payload = Encoding.UTF8.GetBytes(Prefix + _payloadProvider());
                    udp.Send(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
                }
                catch (Exception) { /* 广播失败静默（无网卡/断网）——应答路径不受影响 */ }
                Thread.Sleep(1000);
            }
        }

        /// <summary>TCP 应答循环：连上即回一行即断（不读对端——兼容任何探测实现）。</summary>
        private void TcpLoop()
        {
            var listener = _tcp;
            while (_running && listener != null)
            {
                try
                {
                    if (!listener.Pending()) { Thread.Sleep(300); continue; }
                    var client = listener.AcceptTcpClient();
                    try
                    {
                        client.SendTimeout = 500;
                        var bytes = Encoding.UTF8.GetBytes(Prefix + _payloadProvider() + "\n");
                        var stream = client.GetStream();
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                    finally { client.Close(); }
                }
                catch (SocketException) { /* Pending/Accept 瞬断 */ }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void CleanupSockets()
        {
            try { _replySocket?.Close(); } catch (Exception) { }
            try { _broadcastSocket?.Close(); } catch (Exception) { }
            try { _tcp?.Stop(); } catch (Exception) { }
            _replySocket = null;
            _broadcastSocket = null;
            _tcp = null;
        }

        private static void JoinThread(Thread t)
        {
            if (t == null) return;
            try { if (t.IsAlive) t.Join(800); } catch (Exception) { }
        }
    }
}
