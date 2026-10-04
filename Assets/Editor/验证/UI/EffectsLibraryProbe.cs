using System.IO;
using System.Text;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace Tide.验证
{
    /// <summary>
    /// 效果库加载探针（2026-10-03 数据层诊断）：不进 Play，直接按 CardDataPaths 的口径
    /// 解析 Effects.json 路径并跑 EffectLibrarySerializer.LoadAll，报告路径/存在性/条目数
    /// ——用于"效果表显示空但文件存在"类断线定位。结果同时写 Temp/EffectsLibraryProbe.txt
    ///（Console 可能被过滤器挡住，以文件为准）。用法：Tools/验证/效果库加载探针。
    /// </summary>
    public static class EffectsLibraryProbe
    {
        private const string MenuPath = "Tools/验证/效果库加载探针";
        private const string ReportPath = "Temp/EffectsLibraryProbe.txt";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var sb = new StringBuilder("[EffectsLibraryProbe]\n");
            sb.AppendLine($"TidePaths.DataPath={TidePaths.DataPath}");
            sb.AppendLine($"TidePaths.StreamingAssetsPath={TidePaths.StreamingAssetsPath}");
            string path = CardDataPaths.FileIn("Effects.json");
            sb.AppendLine($"FilePath={path}");
            sb.AppendLine($"File.Exists={File.Exists(path)}");
            if (File.Exists(path))
            {
                string raw = File.ReadAllText(path);
                sb.AppendLine($"rawLength={raw.Length}");
                var all = EffectLibrarySerializer.LoadAll();
                sb.AppendLine($"LoadAll.count={all.Count}");
                if (all.Count > 0)
                    sb.AppendLine($"first.name={all[0].name} first.id={all[0].id}");
            }
            Debug.Log(sb.ToString());
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, sb.ToString());
        }
    }
}
