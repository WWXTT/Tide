using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using CardCore.Editor.Tests;

namespace CardCore.Editor.TideHeadless
{
    /// <summary>
    /// 验证器 headless 批处理入口（v22 二期 S2a，docs/二期训练架构_实施计划.md §4 S2-4）。
    /// 现状：SpecMaster / LAE / Deck / StableIndex 四个验证器只有编辑器菜单，仓库无 headless
    /// 入口（唯一 -executeMethod 是 TideHeadlessServer.Main）——v22 的 B 系列验收要可复跑的
    /// stdout + 退出码，不能靠人点菜单。本类把各验证器的 Run() 结果打到 stdout（batchmode
    /// 下同时进 -logFile）并按失败数置进程退出码。
    ///
    /// 用法（Unity batchmode；子命令=命令行里 -executeMethod 值之后的第一个已知 token）：
    ///   Unity.exe -batchmode -nographics -projectPath &lt;repo&gt; -logFile &lt;log&gt; \
    ///            -executeMethod CardCore.Editor.TideHeadless.AiVerifierBatch.Main spec
    /// 子命令：
    ///   spec     → SpecMasterVerifier（规范总校验；结果经 logMessageReceived 捕获，解析
    ///              「汇总：PASS x / FAIL y」行 + [FAIL] 行兜底）
    ///   lae      → LaeMultiTargetVerifier（返回报告字符串，按「失败 N/M 项」/ [FAIL] 计数）
    ///   deck     → TideObservationDeckVerifier（同上）
    ///   stable   → StableIndexVerifier（Run(out fails, out total) 直读）
    ///   identity → StableIndexVerifier 同源别名（P-1 稳定下标=身份下标对账，无独立 identity 验证器）
    /// 退出码：0=该验证器零失败；1=有失败/异常；2=子命令缺失或未知。
    /// </summary>
    public static class AiVerifierBatch
    {
        public static void Main()
        {
            // 子命令解析：全参数扫第一个已知 token（Unity 对 -executeMethod 后的裸 token
            // 传递不可靠——首跑实测 -executeMethod <method> spec 的 spec 不在
            // GetCommandLineArgs 里，不能只看方法名后一位）。找不到=打印实际参数便于排查。
            var args = Environment.GetCommandLineArgs();
            string[] known = { "spec", "lae", "deck", "stable", "identity" };
            string cmd = args.FirstOrDefault(a => known.Contains(a));
            if (cmd == null)
            {
                Console.WriteLine("[AiVerifierBatch] 未给子命令（spec|lae|deck|stable|identity）——"
                                  + "实际收到：" + string.Join(" ", args));
                EditorApplication.Exit(2);
                return;
            }

            int exit;
            try
            {
                exit = Run(cmd);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AiVerifierBatch] {cmd} 异常中断：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                exit = 1;
            }
            Console.WriteLine($"[AiVerifierBatch] {cmd} → {(exit == 0 ? "PASS" : "FAIL")}（exit={exit}）");
            // batchmode：Console.WriteLine 已同步走 stdout/-logFile，直接退出（delayCall 在
            // -executeMethod 后不保证有泵，不留尾巴）
            EditorApplication.Exit(exit);
        }

        private static int Run(string cmd)
        {
            switch (cmd)
            {
                case "spec":
                {
                    // SpecMasterVerifier.Run() 是 void（Debug.Log 输出）——订阅日志捕获，
                    // 解析「汇总：PASS x / FAIL y（WARN z）」+ [FAIL] 行兜底
                    var captured = new System.Collections.Generic.List<string>();
                    void OnLog(string condition, string stack, LogType type)
                        => captured.Add(condition ?? "");
                    Application.logMessageReceived += OnLog;
                    string report;
                    try
                    {
                        SpecMasterVerifier.Run();
                        report = string.Join("\n", captured);
                    }
                    finally
                    {
                        Application.logMessageReceived -= OnLog;
                    }
                    Console.WriteLine(report);
                    var m = Regex.Match(report, @"汇总：PASS \d+ / FAIL (\d+)");
                    int fails = m.Success ? int.Parse(m.Groups[1].Value)
                        : Regex.Matches(report, @"\[FAIL\]").Count;
                    return fails == 0 ? 0 : 1;
                }
                case "lae":
                    return CountFromReport(LaeMultiTargetVerifier.Run());
                case "deck":
                    return CountFromReport(TideObservationDeckVerifier.Run());
                case "stable":
                case "identity": // P-1 稳定下标=身份下标对账（无独立 identity 验证器，同源别名）
                {
                    StableIndexVerifier.Run(out int fails, out int total);
                    Console.WriteLine($"[AiVerifierBatch] stable fails={fails}/{total}");
                    return fails == 0 ? 0 : 1;
                }
                default:
                    Console.WriteLine($"[AiVerifierBatch] 未知子命令 {cmd}");
                    return 2;
            }
        }

        /// <summary>报告字符串计失败（各验证器统一收口行：「…失败 N/M 项」或 [FAIL] 行兜底）。</summary>
        private static int CountFromReport(string report)
        {
            Console.WriteLine(report);
            var m = Regex.Match(report, @"失败 (\d+)/(\d+) 项");
            if (m.Success) return int.Parse(m.Groups[1].Value) == 0 ? 0 : 1;
            return Regex.Matches(report, @"\[FAIL\]").Count == 0 ? 0 : 1;
        }
    }
}
