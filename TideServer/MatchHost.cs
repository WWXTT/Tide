using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CardCore.Network;

namespace TideServer
{
    /// <summary>
    /// 单局子进程宿主（2026-09-27 独立权威服务器，网络协议.md §14）：现成的 NetSessionServer
    /// （M2 组合根：TcpHost + NetRoom 状态机）+ 自旋泵（~60Hz，与 batchmode 宿主同节奏）。
    ///
    /// stdout 行协议（网关消费，机器可读）：
    ///   READY port=&lt;N&gt;        监听就绪（--port 0 时 N 为 OS 分配的实际端口）
    ///   PHASE &lt;Waiting|DeckSubmit|Playing|Finished&gt;   房间阶段迁移
    ///   EXIT reason=&lt;...&gt;     进程退出
    /// 诊断日志一律走 stderr（不污染行协议）。引擎静态单例约束（GameCore/EventManager 进程级）
    /// 由「一局一进程」天然满足——这是 网络协议.md §13.1 预留的专用服务器方向。
    /// </summary>
    internal static class MatchHost
    {
        public static int Run(Dictionary<string, string> options)
        {
            int port = Program.GetInt(options, "port", 0);
            int tickMs = Program.GetInt(options, "tick-ms", 15);
            int? seed = Program.GetNullableInt(options, "seed");
            // 网关房号透传（2026-09-27）：子进程 RoomState 广播的 RoomId 必须与网关一致，
            // 客户端回传 RoomId（LobbyJoinRoom/LobbyAddAi）才能在网关侧命中同一房间。
            string roomId = options.TryGetValue("room-id", out var rid) && !string.IsNullOrEmpty(rid)
                ? rid
                : $"match-{Environment.TickCount % 100000}";

            var log = new Action<string>(m => Console.Error.WriteLine($"[match] {m}"));
            var server = new NetSessionServer(roomId, log, seed);

            Console.CancelKeyPress += (_, e) => { e.Cancel = false; }; // Ctrl+C 走默认终止即可（TCP 断开即对局作废）
            try
            {
                server.Start(port);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[match] 监听失败: {ex.Message}");
                return 1;
            }
            Console.WriteLine($"READY port={server.Port}");
            Console.Out.Flush();

            var lastPhase = server.Room.Phase.ToString();
            Console.WriteLine($"PHASE {lastPhase}");
            Console.Out.Flush();

            // 心跳行（网关探活兜底——PHASE 不变时也让 stdout 有流动可判死）
            var lastBeat = DateTime.UtcNow;
            try
            {
                while (true)
                {
                    server.Pump();

                    var phase = server.Room.Phase.ToString();
                    if (phase != lastPhase)
                    {
                        lastPhase = phase;
                        Console.WriteLine($"PHASE {phase}");
                        Console.Out.Flush();
                    }
                    if ((DateTime.UtcNow - lastBeat).TotalSeconds >= 10)
                    {
                        lastBeat = DateTime.UtcNow;
                        Console.WriteLine($"BEAT phase={lastPhase}");
                        Console.Out.Flush();
                    }

                    Thread.Sleep(tickMs);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[match] 泵异常: {ex}");
                Console.WriteLine("EXIT reason=crash");
                return 1;
            }
            finally
            {
                try { server.Stop(); } catch { }
                Console.WriteLine("EXIT reason=stop");
                Console.Out.Flush();
            }
        }

        /// <summary>子进程探测工具：父进程是否存活（网关健康检查辅助）。</summary>
        public static bool IsAlive(Process process)
            => process != null && !process.HasExited;
    }
}
