using System;
using System.Diagnostics;
using System.Net.Sockets;
using CardCore.Network;

namespace TideServer.Gateway
{
    /// <summary>
    /// 网关侧房间记录（2026-09-27 独立权威服务器）：一个房间 = 一个 match 子进程 + 两条中继隧道。
    /// 阶段/端口经子进程 stdout 行协议获知（READY/PHASE/BEAT/EXIT）；对局流量网关不解析。
    /// </summary>
    internal sealed class GatewayRoom
    {
        public string RoomId;
        public string Name;
        public Process Child;
        public int Port;                       // 子进程实际监听口（READY 上报）
        public string Phase = "Starting";      // 子进程上报的阶段（Waiting/DeckSubmit/Playing/Finished）
        public readonly string[] ChairNicknames = new string[2];
        public readonly GatewayClient[] Chairs = new GatewayClient[2];
        public int AiCount;
        public DateTime CreatedAt = DateTime.UtcNow;
        public DateTime LastStdoutAt = DateTime.UtcNow;
        public DateTime? EmptySince;                    // 全员离开（椅子空）起始时刻（泵侧维护）
        public bool HasHostedMatch;                     // 开过局——打过局的空房不留（回收策略）
        public bool Dead;                       // 子进程退出/崩溃，待清理

        public int FreeChairCount => (Chairs[0] == null ? 1 : 0) + (Chairs[1] == null ? 1 : 0);
        public bool IsEmpty => Chairs[0] == null && Chairs[1] == null;

        /// <summary>直连子进程的调试信息（RoomState 之外的运维口径）。</summary>
        public override string ToString() => $"{RoomId}「{Name}」 phase={Phase} port={Port} chairs={2 - FreeChairCount}/2";
    }

    /// <summary>
    /// 网关侧客户端连接（2026-09-27）：大厅相位（帧解析→上行入队）→ 入座后切换为
    /// 「解帧→原样重封→隧道」上行中继 + 「隧道→客户端」原始字节下行中继。
    /// 上行重封原因：入座切换必须以完整帧为原子边界（FrameDecoder 半包缓冲不可迁移），
    /// MemoryPack 对同一 NetworkMessage 状态的序列化是字节确定的（字段序固定、Payload 透传）。
    /// </summary>
    internal sealed class GatewayClient
    {
        public readonly TcpClient Tcp;
        public NetworkStream Stream;
        public readonly FrameCodec.FrameDecoder Decoder = new FrameCodec.FrameDecoder();
        public readonly object WriteLock = new object();     // 大厅期=泵线程写；入座后=隧道读线程写
        public string Nickname = "";
        public volatile GatewayRoom Room;                    // 入座后非空
        public volatile int ChairSeat = -1;
        public volatile NetworkStream Tunnel;                // 入座后的子进程侧流
        public readonly object TunnelWriteLock = new object();
        public volatile bool Closed;
        public readonly System.Threading.Thread ReaderThread;

        public GatewayClient(TcpClient tcp)
        {
            Tcp = tcp;
            ReaderThread = new System.Threading.Thread(ReadLoop) { IsBackground = true };
        }

        /// <summary>上行读循环（独立线程）：大厅期解帧入队；入座后逐帧原样转发进隧道。</summary>
        private void ReadLoop()
        {
            var buf = new byte[16 * 1024];
            try
            {
                while (!Closed)
                {
                    int n = Stream.Read(buf, 0, buf.Length);
                    if (n <= 0) break;
                    var tunnel = Tunnel;
                    if (tunnel == null)
                    {
                        Decoder.Append(buf, 0, n);
                        NetworkMessage msg;
                        while (Decoder.TryDecode(out msg))
                            GatewayHost.EnqueueUplink(this, msg);
                    }
                    else
                    {
                        // 逐帧转发：半包字节留在 Decoder 里凑满再封（原子边界，见类注）。
                        // 大厅消息（130-135）例外——转投网关处理：真实客户端入座后仍会发
                        // LobbyAddAi 等（§13.2），子进程不认大厅消息，直传=房内操作死路。
                        Decoder.Append(buf, 0, n);
                        NetworkMessage msg;
                        while (Decoder.TryDecode(out msg))
                        {
                            if (GatewayHost.IsLobbyUplink(msg.Type))
                            {
                                GatewayHost.EnqueueUplink(this, msg);
                                continue;
                            }
                            var frame = FrameCodec.Frame(msg);
                            lock (TunnelWriteLock)
                            {
                                if (Tunnel != null) tunnel.Write(frame, 0, frame.Length);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 读/解码异常：记名后按断线收尾（帧损坏=连接数据损坏的既定语义）
                Console.Error.WriteLine($"[gateway] 客户端读循环异常（{Nickname}）：{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Closed = true;
                GatewayHost.EnqueueDisconnect(this);
                CloseTunnel(); // 客户端断 → 隧道关（子进程按既定断线语义处理）
                try { Tcp.Close(); } catch { }
            }
        }

        /// <summary>下行中继循环（独立线程）：子进程 → 客户端，原始字节泵（无需感知帧）。</summary>
        public static void TunnelDownLoop(GatewayClient client, NetworkStream tunnel)
        {
            var buf = new byte[16 * 1024];
            try
            {
                while (!client.Closed)
                {
                    int n = tunnel.Read(buf, 0, buf.Length);
                    if (n <= 0) break;
                    lock (client.WriteLock)
                    {
                        if (!client.Closed) client.Stream.Write(buf, 0, n);
                    }
                }
            }
            catch { }
            finally
            {
                tunnel.Close();
                client.Close(); // 子进程侧断 → 客户端断（房间回收由网关泵处理）
            }
        }

        public void Close()
        {
            Closed = true;
            try { Tcp.Close(); } catch { }
        }

        public void CloseTunnel()
        {
            var tunnel = Tunnel;
            Tunnel = null;
            try { tunnel?.Close(); } catch { }
        }
    }
}
