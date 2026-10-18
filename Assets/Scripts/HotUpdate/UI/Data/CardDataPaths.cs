using System.IO;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌用户数据路径收口（2026-09-24 立；2026-10-18 热更迁移：种子源从 StreamingAssets 迁至
    /// Assets/CardData 与 Assets/TutorialData——进 YooAsset 热更包对应组，随包可热更）：
    /// - **编辑器**：Assets/CardData（合成卡即入池、git 可追踪用户数据，即改即见）。
    /// - **打包后**：persistentDataPath/Card（玩家包内只读）——首启 EnsureBootstrap 经
    ///   HotUpdateAssets.LoadText 从热更包读种子写入（旧 File.Copy 通道在安卓 jar 下本就失效，退役）。
    /// - **服务器（TideServer）**：TidePaths 无装配回落——DataPath 直探 Assets/CardData 读仓库源
    ///   （CardData 存在则引导空转，只读不开局外存档）。
    /// 全部卡牌数据读写（Cards/Effects/卡组）一律经此取路径，不得再直连 StreamingAssets。
    /// </summary>
    public static class CardDataPaths
    {
        /// <summary>卡牌种子文件（热更包 CardData 组 → persistent；新增种子文件须同步此表）。</summary>
        private static readonly string[] CardSeedFiles = { "Cards.json", "Effects.json", "红·快攻.json" };

        /// <summary>教学表/卡组引用种子（热更包 TutorialData 组 → persistent）。教学音频不在此列——
        /// mp3 以 AudioClip 资产进包，BattleScreen.PlayTutorialVoiceAsync 直取。</summary>
        private static readonly string[] TutorialSeedFiles =
            { "TutorialCards.json", "TutorialEffects.json", "basics.json", "basics_ai.json", "测试卡组.json" };

        /// <summary>玩家端种子读取器（宿主装配模式，同 TideLog.Sink）：Unity 侧由 HotUpdateEntry
        /// 装配为 HotUpdateAssets.LoadText（真机从热更包读种子）；TideServer 不装配——EnsureBootstrap
        /// 空转不落盘，直接走 CardDir 的仓库源回落。参数为 "CardData/x.json" 形式相对路径。</summary>
        public static System.Func<string, string> PackTextReader;

        /// <summary>卡牌用户数据目录（编辑器=仓库源；玩家=persistent 首启引导；TideServer=仓库源回落）。</summary>
        public static string CardDir
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(TidePaths.DataPath, "CardData");
#else
                string persistent = Path.Combine(TidePaths.PersistentDataPath, "Card");
                if (Directory.Exists(persistent)) return persistent;
                string source = Path.Combine(TidePaths.DataPath, "CardData");
                if (Directory.Exists(source)) return source; // TideServer：无装配直读仓库源
                return persistent; // 设备首启：EnsureBootstrap 负责建目录
#endif
            }
        }

        /// <summary>目录内文件完整路径。</summary>
        public static string FileIn(string fileName) => Path.Combine(CardDir, fileName);

        /// <summary>教学资源目录内文件完整路径（教学卡表/卡组引用，2026-10-17 教学卡分家定案：
        /// 不与正式卡 CardData 混放；2026-10-18 迁 Assets/TutorialData，真机经 EnsureTutorialBootstrap
        /// 从热更包引导到 persistent，只读）。</summary>
        public static string TutorialFileIn(string fileName) => Path.Combine(TutorialDir, fileName);

        private static string TutorialDir
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(TidePaths.DataPath, "TutorialData");
#else
                string persistent = Path.Combine(TidePaths.PersistentDataPath, "Tutorial");
                if (Directory.Exists(persistent)) return persistent;
                string source = Path.Combine(TidePaths.DataPath, "TutorialData");
                if (Directory.Exists(source)) return source; // TideServer：无装配直读仓库源
                return persistent;
#endif
            }
        }

        /// <summary>
        /// 玩家端首启引导：persistent 缺目录时从热更包读种子写入（幂等；编辑器空操作）。各读写入口调用。
        /// </summary>
        public static void EnsureBootstrap()
        {
#if !UNITY_EDITOR
            SeedDirectory(Path.Combine(TidePaths.PersistentDataPath, "Card"), "CardData", CardSeedFiles);
            SeedDirectory(Path.Combine(TidePaths.PersistentDataPath, "Tutorial"), "TutorialData", TutorialSeedFiles);
#endif
        }

        private static void SeedDirectory(string destDir, string packRelative, string[] seedFiles)
        {
            if (Directory.Exists(destDir)) return;
            if (PackTextReader == null) return; // 未装配（TideServer/无宿主）：不落盘，读取走仓库源回落
            try
            {
                Directory.CreateDirectory(destDir);
                foreach (string file in seedFiles)
                {
                    string text = PackTextReader(packRelative + "/" + file);
                    if (string.IsNullOrEmpty(text))
                    {
                        TideLog.Warn($"[CardDataPaths] 热更包缺种子 {file}（将视为空数据启动）");
                        continue;
                    }
                    File.WriteAllText(Path.Combine(destDir, file), text);
                }
            }
            catch (System.Exception ex)
            {
                TideLog.Warn($"[CardDataPaths] 种子数据引导失败（将视为空数据启动）：{ex.Message}");
            }
        }
    }
}
