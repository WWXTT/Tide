using System;
using System.Collections.Generic;

namespace TideServer
{
    /// <summary>
    /// TideServer 入口（2026-09-27 独立权威服务器，网络协议.md §14）：
    ///
    ///   TideServer gateway [--port 7777] [--tick-ms 15] [--seed N]   网关：大厅 + 每局一进程监督者
    ///   TideServer match   [--port 0]    [--tick-ms 15] [--seed N]   单局子进程（网关 spawn，也可独立调试直连）
    ///   TideServer selftest [--seed N]                                自动化门禁：网关 + 双 AI 整局 + 子进程回收
    ///   TideServer loadcheck                                            数据装载自检（卡表/效果/原子表计数）
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
            catch { /* 控制台不支持 UTF8 时按默认编码继续 */ }

            var options = ParseArgs(args);
            var command = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
                ? args[0].ToLowerInvariant()
                : "gateway";

            switch (command)
            {
                case "gateway":
                    return new Gateway.GatewayHost(options).Run();
                case "match":
                    return MatchHost.Run(options);
                case "selftest":
                    return SmokeTest.Run(options);
                case "verify":
                    return Verify.VerifySuite.Run(options);
                case "loadcheck":
                    return LoadCheck.Run();
                default:
                    Console.WriteLine($"未知子命令: {command}");
                    Console.WriteLine("用法: TideServer gateway|match|selftest|verify|loadcheck [--port N] [--seed N] [--tick-ms N] [--quick] [--sections V1,V5]");
                    return 2;
            }
        }

        /// <summary>命令行解析：--key value 对（含 --key（无值）布尔形态）。</summary>
        public static Dictionary<string, string> ParseArgs(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
                var key = args[i].Substring(2);
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    result[key] = args[i + 1];
                    i++;
                }
                else result[key] = "1";
            }
            return result;
        }

        public static int GetInt(Dictionary<string, string> options, string key, int fallback)
        {
            return options.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;
        }

        public static int? GetNullableInt(Dictionary<string, string> options, string key)
        {
            if (!options.TryGetValue(key, out var raw) || !int.TryParse(raw, out var value)) return null;
            return value;
        }
    }
}
