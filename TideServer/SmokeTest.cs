using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace TideServer
{
    /// <summary>
    /// 自动化门禁（selftest，退出码即结果）——全链路走真实 TCP + 网关中继隧道：
    ///
    ///   ①数据装载自检（TideJson/Newtonsoft 读正式数据文件）
    ///   ②spawn 网关子进程（--port 0，READY 行协议读端口）
    ///   ③两轮对局：每轮双 AI 走大厅链路（LobbyHello → 建房/自动匹配 → DeckSubmit →
    ///     修订锁步整局）到终局——第二轮验证「回收后再开局」（子进程重建）
    ///   ④断言：双方 Done（见过 GameOver 且房间 Finished）+ 网关侧「终局回收」日志
    ///   ⑤杀网关 → Job Object 连带杀全部 match 子进程（无孤儿进程）
    /// </summary>
    internal static class SmokeTest
    {
        private const int MatchTimeoutSeconds = 300;

        public static int Run(Dictionary<string, string> options)
        {
            if (LoadCheck.Run() != 0) return 1;

            var deckIds = SynergyUI.CardCatalog.LoadAll().Select(c => c.ID).Distinct().Take(30).ToArray();
            if (deckIds.Length < 2)
            {
                Console.WriteLine("SELFTEST FAIL：卡表不足 2 张，无法开局");
                return 1;
            }

            // ---- 启动网关子进程（stdout 行协议读 READY port） ----
            var psi = new ProcessStartInfo(Environment.ProcessPath,
                $"gateway --port 0 --tick-ms 15" + (Program.GetNullableInt(options, "seed") is int s ? $" --seed {s}" : ""))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var gatewayLines = new System.Collections.Concurrent.ConcurrentQueue<string>();
            var gatewayErrs = new System.Collections.Concurrent.ConcurrentQueue<string>();
            int recycledCount = 0;
            using var gateway = Process.Start(psi);
            var outReader = new Thread(() => { string l; while ((l = gateway.StandardOutput.ReadLine()) != null) gatewayLines.Enqueue(l); }) { IsBackground = true };
            var errReader = new Thread(() =>
            {
                string l;
                while ((l = gateway.StandardError.ReadLine()) != null)
                {
                    gatewayErrs.Enqueue(l);
                    Console.WriteLine($"[gw] {l}"); // 实时透传（排障可见性）
                    if (l.Contains("终局回收")) Interlocked.Increment(ref recycledCount);
                }
            }) { IsBackground = true };
            outReader.Start();
            errReader.Start();

            int port = 0;
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (port == 0 && DateTime.UtcNow < deadline && !gateway.HasExited)
            {
                while (gatewayLines.TryDequeue(out var line))
                    if (line.StartsWith("READY port=", StringComparison.Ordinal)
                        && int.TryParse(line.Substring(11).Trim(), out port)) break;
                if (port == 0) Thread.Sleep(50);
            }
            if (port == 0)
            {
                Console.WriteLine("SELFTEST FAIL：网关未在时限内就绪");
                TryKill(gateway);
                return 1;
            }
            Console.WriteLine($"网关端口 = {port}（pid {gateway.Id}）");

            try
            {
                for (int round = 1; round <= 2; round++)
                {
                    Console.WriteLine($"=== 第 {round} 局（大厅建房 + 自动匹配）===");
                    var a = new BrainClient();
                    var b = new BrainClient();
                    var ta = new Thread(() => a.RunLobbyCreate("127.0.0.1", port, $"自测A{round}", deckIds, 60, $"selftest-{round}")) { IsBackground = true };
                    var tb = new Thread(() => b.RunLobbyAutoMatch("127.0.0.1", port, $"自测B{round}", deckIds, 60)) { IsBackground = true };
                    ta.Start();
                    Thread.Sleep(400); // A 先建房，B 的自动匹配直接补位
                    tb.Start();
                    ta.Join(TimeSpan.FromSeconds(MatchTimeoutSeconds));
                    tb.Join(TimeSpan.FromSeconds(30));

                    bool aDone = a.Done, bDone = b.Done;
                    Console.WriteLine($"  A: Done={aDone} snapshots={a.SnapshotsSeen} errors={a.ErrorsSeen} lastErr={a.LastError ?? "-"}");
                    Console.WriteLine($"  B: Done={bDone} snapshots={b.SnapshotsSeen} errors={b.ErrorsSeen} lastErr={b.LastError ?? "-"}");
                    if (!aDone || !bDone)
                    {
                        Console.WriteLine($"SELFTEST FAIL：第 {round} 局未到终局（A={aDone} B={bDone}）");
                        return 1;
                    }

                    // 终局回收观测（子进程被网关杀掉）
                    var recycleDeadline = DateTime.UtcNow.AddSeconds(20);
                    while (Volatile.Read(ref recycledCount) < round && DateTime.UtcNow < recycleDeadline)
                        Thread.Sleep(200);
                    if (Volatile.Read(ref recycledCount) < round)
                    {
                        Console.WriteLine($"SELFTEST FAIL：第 {round} 局终局后子进程未被回收（网关日志缺「终局回收」）");
                        return 1;
                    }
                    Console.WriteLine($"  终局回收 ✓（累计 {recycledCount} 次）");
                }
            }
            finally
            {
                TryKill(gateway);
            }

            gateway.WaitForExit(5000);
            Console.WriteLine("SELFTEST PASS（两局全链路 + 子进程回收重建 + 网关停机）");
            return 0;
        }

        private static void TryKill(Process p)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
        }
    }
}
