using System.IO;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌用户数据路径收口（2026-09-24，WebGL 不堵死定案的落地缝）：
    /// - **编辑器**：StreamingAssets/Card（现状不变——合成卡即入池、git 可追踪用户数据）。
    /// - **打包后**：persistentDataPath/Card（StreamingAssets 在玩家包内只读）——首次使用时从
    ///   包内 StreamingAssets/Card 同步拷贝种子数据（Standalone/移动端 File.Copy 可用）。
    /// - **WebGL 缺口（如实声明）**：WebGL 的 StreamingAssets 只能 UnityWebRequest 异步读取，
    ///   同步引导不可行——异步种子拷贝属 WebGL 出包里程碑，届时仅改本类，读写双端零改动。
    /// 全部卡牌数据读写（Cards/Effects/卡组）一律经此取路径，不得再直连 streamingAssetsPath。
    /// </summary>
    public static class CardDataPaths
    {
        /// <summary>卡牌用户数据目录（编辑器=包内可写区，玩家=persistentData）。</summary>
        public static string CardDir =>
#if UNITY_EDITOR
            Path.Combine(Application.streamingAssetsPath, "Card");
#else
            Path.Combine(Application.persistentDataPath, "Card");
#endif

        /// <summary>目录内文件完整路径。</summary>
        public static string FileIn(string fileName) => Path.Combine(CardDir, fileName);

        /// <summary>
        /// 玩家端首启引导：persistentData 目录缺失时从包内 StreamingAssets/Card 拷贝种子
        /// （幂等——目录已存在即返回；编辑器空操作）。各读写入口调用。
        /// </summary>
        public static void EnsureBootstrap()
        {
#if !UNITY_EDITOR
            if (Directory.Exists(CardDir)) return;
            try
            {
                Directory.CreateDirectory(CardDir);
                var seed = Path.Combine(Application.streamingAssetsPath, "Card");
                if (Directory.Exists(seed))
                    foreach (var file in Directory.GetFiles(seed, "*.json"))
                        File.Copy(file, Path.Combine(CardDir, Path.GetFileName(file)), false);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CardDataPaths] 种子数据引导失败（将视为空数据启动）：{ex.Message}");
            }
#endif
        }
    }
}
