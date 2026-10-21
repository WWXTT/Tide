using System;
using System.Collections.Generic;
using System.Threading;
using CardCore.Network;

namespace TideServer
{
    /// <summary>
    /// 外部客人进程（2026-10-21 P2P 双端联机冒烟基建）：
    /// 以 NetAiFiller（独立线程 AI 客户端）连入指定房主——编辑器/另一进程当房主时，
    /// 本命令扮演远端客人打到终局，验证跨进程 TCP 全链（JoinRoom→DeckSubmit→整局）。
    /// 用法：TideServer aiguest --port 28000 [--nick 名字] [--turn-cap 60] [--timeout 180]
    /// 退出码：0=到达终局（收到 GameOverEvent + 房间 Finished）；1=超时/断线。
    /// </summary>
    internal static class AiGuest
    {
        public static int Run(Dictionary<string, string> options)
        {
            int port = Program.GetInt(options, "port", 28000);
            int turnCap = Program.GetInt(options, "turn-cap", 60);
            int timeoutSec = Program.GetInt(options, "timeout", 180);
            string nick = options.TryGetValue("nick", out var n) && !string.IsNullOrEmpty(n) ? n : "远端客人";

            LoadCheck.Run();

            var filler = NetAiFiller.FromPlayPool(nick);
            if (filler == null)
            {
                Console.WriteLine("[AiGuest] 卡池为空——卡表未装载，退出");
                return 1;
            }

            Console.WriteLine($"[AiGuest] 连接 127.0.0.1:{port}（{nick}，回合上限 {turnCap}）");
            filler.Start(port);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (filler.Thread != null && filler.Thread.IsAlive && sw.Elapsed.TotalSeconds < timeoutSec)
                Thread.Sleep(200);

            bool finished = filler.Thread != null && !filler.Thread.IsAlive;
            Console.WriteLine($"[AiGuest] 结束（{sw.Elapsed.TotalSeconds:F1}s）：{(finished ? "客户端线程自然退出（终局）" : "超时或仍在局中")}");
            return finished ? 0 : 1;
        }
    }
}
