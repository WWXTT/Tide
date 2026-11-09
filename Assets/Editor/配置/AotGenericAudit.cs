// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停；恢复=开宏+HybridCLR enable+补包三步（见 查缺补漏.md「待办总账」第 1 条；编辑器菜单 Tools/热更/1-6）
#if TIDE_HYBRID_YOO
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tide.HotUpdateTools
{
    /// <summary>
    /// AOT 泛型缺口审计（2026-11-09 阶段C，«AOT泛型门禁_实施计划» §2C）：
    /// 热更 DLL 里的值类型泛型实例化若在 AOT 侧未锚定，真机 HybridCLR 首调即 MissingMethodException
    ///（本日 RunOnThreadPool&lt;bool&gt; 真机联机 100% 失败实录）。本入口调外部工具
    /// tools/AotAudit（net10.0 零 NuGet 依赖，规则单源）对基线 Config/aot_generic_baseline.txt 对账：
    /// 新增未处置 → LogError 清单+处置指引并拦打包（「真机才发现」前移到「打包前拦截」）。
    /// 计划 §2C 的 dnlib 内置回退未做：本仓 CI（TideServer/verify.ps1 三工程编译）本就硬依赖
    /// dotnet，同一规则双实现反而引入漂移面；dotnet 缺席=明确报错指路，门禁不静默放行（§5 意图保持）。
    /// </summary>
    public static class AotGenericAudit
    {
        private const string ToolProject = "tools/AotAudit";
        private const string HotDllDir = "Assets/HotUpdateDlls";
        private const string Baseline = "Config/aot_generic_baseline.txt";
        private const string AnchorDir = "Assets/Scripts/Launch";
        private const int TimeoutMs = 180000; // 首跑含 dotnet 编译工具本身，留足

        [MenuItem("Tools/热更/6. AOT 缺口审计")]
        public static void RunFromMenu()
        {
            bool pass = Run();
            if (pass) Debug.Log("[AotAudit] 菜单审计通过（详见上方清单行）");
        }

        /// <summary>打包前置门禁（YooPackBuilder.BuildPlayerPackageCore 调用）。返回 true=通过；
        /// false=新增未处置条目或环境错误（已 LogError 清单与指引）。</summary>
        internal static bool RunGate() => Run();

        private static bool Run()
        {
            if (!Directory.Exists(HotDllDir) || Directory.GetFiles(HotDllDir, "*.dll.bytes").Length == 0)
            {
                Debug.LogError($"[AotAudit] {HotDllDir} 无热更 DLL——先跑 Tools/热更/4 编译拷贝");
                return false;
            }
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{ToolProject}\" -- \"{HotDllDir}\" --baseline \"{Baseline}\" --anchor-dir \"{AnchorDir}\"",
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            System.Diagnostics.Process p;
            try
            {
                p = System.Diagnostics.Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Debug.LogError("[AotAudit] dotnet 不可用（PATH 无 .NET SDK）——安装 .NET 10 SDK 后重试；"
                    + "命令行同口径验证：dotnet run --project tools/AotAudit -- Assets/HotUpdateDlls --baseline Config/aot_generic_baseline.txt");
                return false;
            }
            using var proc = p;
            var stderrTask = proc.StandardError.ReadToEndAsync(); // 先挂异步读，防双缓冲死锁
            string stdout = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(TimeoutMs))
            {
                proc.Kill();
                Debug.LogError($"[AotAudit] 工具执行超时（{TimeoutMs / 1000}s）——直接命令行复现："
                    + $"dotnet run --project {ToolProject} -- {HotDllDir} --baseline {Baseline}");
                return false;
            }
            string stderr = stderrTask.Result.Trim();
            bool pass = proc.ExitCode == 0;
            foreach (var raw in stdout.Replace("\r", "").Split('\n'))
            {
                if (raw.Length == 0) continue;
                if (pass || raw.StartsWith("[AotAudit]")) Debug.Log("[AotAudit→] " + raw);
                else Debug.LogError("[AotAudit→] " + raw); // [新增未处置]/[处置指引] 等清单行以红色落 Console
            }
            if (stderr.Length > 0) Debug.LogError("[AotAudit][stderr] " + stderr);
            if (proc.ExitCode == 2)
                Debug.LogError("[AotAudit] 环境错误（exit 2）：参数/基线格式问题或工具损坏——命令行复现同上");
            else if (proc.ExitCode == 3)
                Debug.LogError("[AotAudit] 已拦截：热更 DLL 出现基线未处置的 AOT 泛型缺口（见上列[新增未处置]）——"
                    + "处置后更新基线 Config/aot_generic_baseline.txt 再出包（补锚点→anchored / 评估安全→safe 写理由 / 编译器生成名→noise）");
            return pass;
        }
    }
}
#endif
