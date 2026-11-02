using System;
using System.Collections.Generic;

namespace TideServer
{
    /// <summary>
    /// TideLab 入口（2026-11-02）：只承载红套作者工具的无头跑法。
    ///   TideLab redlab            只打印候选卡推导费（不落盘）
    ///   TideLab redgen            重建红·快攻（写 TutorialCards/Effects + 卡组 + 测试卡组）
    ///   TideLab redprobe [--games N] [--vs 卡组名] [--seed N] [--trace]
    ///   TideLab redcast / redloss 单卡可施放性 / 代价落点探针
    /// 与 TideServer 的同名子命令共用同一份 RedDeckLab 源码。
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
                : "redlab";

            switch (command)
            {
                case "redlab": return RedDeckLab.RunLab();
                case "redgen": return RedDeckLab.RunGen();
                case "redprobe": return RedDeckLab.RunProbe(options);
                case "redcast": return RedDeckLab.RunCast(options);
                case "redloss": return RedDeckLab.RunLoss(options);
                case "reddemo": return RedDeckLab.RunDemo(options);
                case "greendemo": return RedDeckLab.RunGreenDemo(options);
                case "greenfix": return RedDeckLab.RunCultivateFix(options);
                case "greenretune": return RedDeckLab.RunGreenRetune(options);
                case "greenretune2": return RedDeckLab.RunGreenRetune2(options);
                case "greenretune3": return RedDeckLab.RunGreenRetune3(options);
                case "greenretune4": return RedDeckLab.RunGreenRetune4(options);
                case "greenretune5": return RedDeckLab.RunGreenRetune5(options);
                case "greenretune6": return RedDeckLab.RunGreenRetune6(options);
                case "greenretune7": return RedDeckLab.RunGreenRetune7(options);
                case "greenretune8": return RedDeckLab.RunGreenRetune8(options);
                case "greenretune9": return RedDeckLab.RunGreenRetune9(options);
                case "greenretune10": return RedDeckLab.RunGreenRetune10(options);
                case "greenretune11": return RedDeckLab.RunGreenRetune11(options);
                case "greenretune12": return RedDeckLab.RunGreenRetune12(options);
                case "greenretune13": return RedDeckLab.RunGreenRetune13(options);
                case "greenretune14": return RedDeckLab.RunGreenRetune14(options);
                case "greenretune15": return RedDeckLab.RunGreenRetune15(options);
                case "greenretune16": return RedDeckLab.RunGreenRetune16(options);
                case "greenretune17": return RedDeckLab.RunGreenRetune17(options);
                case "gcalc": return RedDeckLab.RunCardCalc(options);
                case "t1": return RedDeckLab.RunTurn1Check(options);
                case "greenlab": return RedDeckLab.RunGreenLab(options);
                default:
                    Console.WriteLine($"未知子命令: {command}");
                    Console.WriteLine("用法: TideLab redlab|redgen|redprobe|redcast|redloss [--games N] [--vs 卡组名] [--seed N] [--trace]");
                    return 2;
            }
        }

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
    }
}
