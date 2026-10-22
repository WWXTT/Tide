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
        /// <summary>卡牌种子文件（热更包 CardData 组 → persistent；新增种子文件须同步此表）。
        /// 2026-10-22 三套主题卡组迁教学：卡组 json 与 50 张主题卡/38 条效果全部移入 TutorialData，
        /// 主表只留玩家自建内容（当前为空表，首启仍写入空壳保持路径存在）。</summary>
        private static readonly string[] CardSeedFiles = { "Cards.json", "Effects.json" };

        /// <summary>教学表/卡组引用种子（热更包 TutorialData 组 → persistent）。教学音频不在此列——
        /// mp3 以 AudioClip 资产进包，BattleScreen.PlayTutorialVoiceAsync 直取。</summary>
        private static readonly string[] TutorialSeedFiles =
        {
            "TutorialCards.json", "TutorialEffects.json", "basics.json", "basics_ai.json",
            "jinjie.json", "jinjie_ai.json", "测试卡组.json",
            "红·快攻.json", "蓝·控制.json", "绿·慢速.json",
        };

        /// <summary>预组卡组名（2026-10-22 定案）：三套主题卡组随包只读，文件在 TutorialData；
        /// 玩家侧同名档一律不列/不可保存（防旧安装残留与同名覆盖）——见 DeckSerializer 过滤与
        /// DeckBuilderScreen 保存闸。顺序=UI 展示序。</summary>
        public static readonly string[] PresetDeckNames = { "红·快攻", "蓝·控制", "绿·慢速" };

        /// <summary>是否预组卡组名（大小写/空白不归一——卡组名本身即文件名，保持精确匹配）。</summary>
        public static bool IsPresetDeckName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var n in PresetDeckNames)
                if (n == name) return true;
            return false;
        }

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

        /// <summary>教学数据目录（编辑器=仓库源 Assets/TutorialData；玩家=persistent/Tutorial；
        /// TideServer=仓库源回落）。2026-10-22 起同时承载三套预组卡组（只读）。</summary>
        public static string TutorialDir
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
        /// 玩家端引导（编辑器空操作，每次读写入口调用，幂等）：
        /// · **玩家数据 Card/**：只在文件缺失时补种子（绝不覆盖玩家自建卡/效果）；
        /// · **教学数据 Tutorial/**：从热更包**整体刷新**（只读内容，玩家从不写）——否则老版本
        ///   安装的 persistent/Tutorial 目录已存在时永远收不到新增教学表/预组卡组（2026-10-22
        ///   三套主题卡组迁教学即靠这条把 50 张卡/75 条效果/三个预组下发给存量玩家）。
        /// </summary>
        public static void EnsureBootstrap()
        {
#if !UNITY_EDITOR
            if (_bootstrapped || PackTextReader == null) return; // 未装配宿主：不置位，装配后重试
            SeedDirectory(Path.Combine(TidePaths.PersistentDataPath, "Card"), "CardData", CardSeedFiles,
                overwrite: false);
            SeedDirectory(Path.Combine(TidePaths.PersistentDataPath, "Tutorial"), "TutorialData", TutorialSeedFiles,
                overwrite: true);
            _bootstrapped = true;
#endif
        }

#if !UNITY_EDITOR
        private static bool _bootstrapped;
#endif

        /// <summary>种子落盘：overwrite=false 只在目标缺失时写（玩家数据）；true=每次覆盖（教学只读内容）。</summary>
        private static void SeedDirectory(string destDir, string packRelative, string[] seedFiles, bool overwrite)
        {
            if (PackTextReader == null) return; // 未装配（TideServer/无宿主）：不落盘，读取走仓库源回落
            try
            {
                Directory.CreateDirectory(destDir);
                foreach (string file in seedFiles)
                {
                    string dest = Path.Combine(destDir, file);
                    if (!overwrite && File.Exists(dest)) continue;
                    string text = PackTextReader(packRelative + "/" + file);
                    if (string.IsNullOrEmpty(text))
                    {
                        TideLog.Warn($"[CardDataPaths] 热更包缺种子 {file}（保留旧档/视为空数据）");
                        continue;
                    }
                    File.WriteAllText(dest, text);
                }
            }
            catch (System.Exception ex)
            {
                TideLog.Warn($"[CardDataPaths] 种子数据引导失败（将视为空数据启动）：{ex.Message}");
            }
        }
    }
}
