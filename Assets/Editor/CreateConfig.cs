using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

namespace CardCore.Editor
{
    /// <summary>
    /// Luban 配置刷新工具
    /// </summary>
    public static class CreateConfig
    {
        [MenuItem("Tools/一键刷新配置")]
        public static void RefreshConfig()
        {
            string batpath = "C:\\Users\\Administrator\\Desktop\\Synergy\\Config";
            string genBatPath = Path.Combine(batpath, "export_attribute.bat");

            if (!File.Exists(genBatPath))
            {
                EditorUtility.DisplayDialog("错误", $"找不到配置生成脚本: {genBatPath}", "确定");
                return;
            }

            EditorUtility.DisplayProgressBar("刷新配置", "正在执行配置生成...", 0.5f);

            try
            {
                var process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/c {genBatPath}";
                process.StartInfo.WorkingDirectory = batpath;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    EditorUtility.DisplayDialog("成功", "配置刷新完成！", "确定");
                    AssetDatabase.Refresh();
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", $"配置生成失败，退出码: {process.ExitCode}", "确定");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("异常", $"执行失败: {e.Message}", "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
