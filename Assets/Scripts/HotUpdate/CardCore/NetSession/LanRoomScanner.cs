using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CardCore;

namespace CardCore.Network
{
    /// <summary>扫描到的局域网房间条目（键 = 房主地址:游戏端口）。</summary>
    public sealed class LanRoomEntry
    {
        public string Host;      // 房主可达地址（探测目标或应答来源）
        public int TcpPort;      // 游戏连接端口（28000 系）
        public string RoomId;
        public string HostNick;
        public string Seats;     // 如 "1/2"
        public string Phase;     // Waiting/DeckSubmit/Playing/Finished
        public int LastSeenMs;

        public string Key => Host + ":" + TcpPort;
    }

    /// <summary>
    /// 局域网房间扫描器（2026-10-28，与 LanRoomBeacon 成对）：客人进加入面板即扫描，自动发现直入。
    /// 双通道：UDP 收广播信标 + 主动探测（255.255.255.255 真机局域网广播 / 10.0.2.2 MuMu 等模拟器
    /// slirp 宿主别名 / 127.0.0.1 同机双端与 adb forward 场景）；TCP 探测 127.0.0.1、10.0.2.2 兜底
    /// （UDP 被 NAT 拦截时仍可发现）。单后台线程扫描，UI 线程 Snapshot() 轮询（条目 8s 过期）。
    /// 纯 BCL（TideServer 共享源同编译）。
    /// </summary>
    public sealed class LanRoomScanner : IDisposable
    {
        private static readonly string[] UdpProbeHosts = { "255.255.255.255", "10.0.2.2", "127.0.0.1" };
        private static readonly string[] TcpProbeHosts = { "127.0.0.1", "10.0.2.2" };
        private const int ScanDurationMs = 12000; // 单轮扫描时长（过后 Scanning=false，UI 提示重扫）
        private const int EntryTtlMs = 8000;

        private readonly Dictionary<string, LanRoomEntry> _rooms = new Dictionary<string, LanRoomEntry>();
        private readonly object _lock = new object();
        private Thread _thread;
        private UdpClient _receiver;
        private volatile bool _scanning;

        public bool Scanning => _scanning;
        public int StartedAtMs { get; private set; }

        /// <summary>开始/重启一轮扫描（旧线程收口后再起）。</summary>
        public void StartScan()
        {
            StopScan();
            StartedAtMs = Environment.TickCount;
            _scanning = true;
            _thread = new Thread(ScanLoop) { IsBackground = true, Name = "LanRoomScanner" };
            _thread.Start();
        }

        private void ScanLoop()
        {
            try
            {
                _receiver = new UdpClient();
                _receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _receiver.Client.Bind(new IPEndPoint(IPAddress.Any, LanRoomBeacon.DiscoveryPort));
                _receiver.EnableBroadcast = true;
                _receiver.Client.ReceiveTimeout = 150;
            }
            catch (Exception) { _receiver = null; } // 28001 被占（同机房主信标等）——只剩 TCP 探测路径

            var probe = Encoding.UTF8.GetBytes("PROBE");
            var deadline = Environment.TickCount + ScanDurationMs;
            while (_scanning && Environment.TickCount < deadline)
            {
                // 出向：UDP 探测三目标 + TCP 探测两目标
                if (_receiver != null)
                    foreach (var h in UdpProbeHosts)
                        try { _receiver.Send(probe, probe.Length, h, LanRoomBeacon.DiscoveryPort); }
                        catch (Exception) { }
                foreach (var h in TcpProbeHosts) TcpProbe(h);

                // 入向：1.2s 窗口收广播信标/应答
                var roundEnd = Environment.TickCount + 1200;
                while (_scanning && Environment.TickCount < roundEnd)
                {
                    if (_receiver == null) { Thread.Sleep(150); continue; }
                    try
                    {
                        var ep = new IPEndPoint(IPAddress.Any, 0);
                        var data = _receiver.Receive(ref ep);
                        HandlePayloadText(Encoding.UTF8.GetString(data), ep.Address.ToString());
                    }
                    catch (SocketException) { /* 150ms 超时空转 */ }
                    catch (ObjectDisposedException) { break; }
                }
            }
            _scanning = false;
        }

        /// <summary>TCP 探测：短超时连 28001，读到 TIDEROOM 行即登记（模拟器 slirp 与 adb forward 场景主力路径）。</summary>
        private void TcpProbe(string host)
        {
            TcpClient c = null;
            try
            {
                c = new TcpClient();
                var ar = c.BeginConnect(host, LanRoomBeacon.DiscoveryPort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(600)) { c.Close(); return; }
                c.EndConnect(ar);
                c.ReceiveTimeout = 600;
                var buf = new byte[1024];
                int n;
                try { n = c.GetStream().Read(buf, 0, buf.Length); }
                catch (Exception) { n = 0; }
                if (n > 0) HandlePayloadText(Encoding.UTF8.GetString(buf, 0, n), host);
            }
            catch (Exception) { /* 不可达/超时——静默 */ }
            finally
            {
                try { c?.Close(); } catch (Exception) { }
            }
        }

        /// <summary>解析 TIDEROOM|ver|roomId|hostNick|tcpPort|seats|phase 行（TCP 带换行取首行）。</summary>
        private void HandlePayloadText(string text, string sourceIp)
        {
            if (string.IsNullOrEmpty(text)) return;
            var line = text.Split('\n')[0].Trim();
            if (!line.StartsWith(LanRoomBeacon.Prefix, StringComparison.Ordinal)) return;
            var p = line.Split('|');
            if (p.Length < 7) return;
            if (!int.TryParse(p[4], out var tcpPort) || tcpPort <= 0 || tcpPort >= 65536) return;
            var entry = new LanRoomEntry
            {
                Host = sourceIp,
                TcpPort = tcpPort,
                RoomId = p[2],
                HostNick = p[3],
                Seats = p[5],
                Phase = p[6],
                LastSeenMs = Environment.TickCount,
            };
            lock (_lock) { _rooms[entry.Key] = entry; }
        }

        /// <summary>当前有效房间快照（过期条目顺带清理；按 Key 排序保证 UI 行序稳定）。</summary>
        public List<LanRoomEntry> Snapshot()
        {
            var now = Environment.TickCount;
            var result = new List<LanRoomEntry>();
            lock (_lock)
            {
                var expired = new List<string>();
                foreach (var kv in _rooms)
                {
                    if (now - kv.Value.LastSeenMs > EntryTtlMs) { expired.Add(kv.Key); continue; }
                    result.Add(kv.Value);
                }
                foreach (var k in expired) _rooms.Remove(k);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result;
        }

        public void StopScan()
        {
            _scanning = false;
            try { _receiver?.Close(); } catch (Exception) { } // 关接收口即刻打断 Receive
            _receiver = null;
            var t = _thread;
            if (t != null && t.IsAlive) t.Join(1000);
            _thread = null;
            lock (_lock) { _rooms.Clear(); }
        }

        public void Dispose() => StopScan();
    }
}
