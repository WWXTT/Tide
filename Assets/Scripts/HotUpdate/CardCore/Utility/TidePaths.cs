using System;
using System.IO;

namespace CardCore
{
    /// <summary>
    /// 跨宿主数据路径收口（2026-09-27 去 Unity 化）：取代共享源码里的 Application.dataPath /
    /// streamingAssetsPath / persistentDataPath 直引。
    ///
    /// - Unity 侧由 TideUnityRuntime 装配真实路径（编辑器与玩家构建均覆盖）。
    /// - 未装配时自定位：从程序集目录向上探测仓库布局（Assets/Configs/AttributeValueConfig.json）——
    ///   编辑器（Library/ScriptAssemblies → 上溯项目根）与 TideServer（TideServer/bin → 上溯仓库根）同一布局。
    /// - PersistentDataPath 无装配时回落 StreamingAssets（服务器只读仓库数据，与 CardDataPaths
    ///   玩家分支指向种子数据一致）；玩家构建必须经装配才有真实持久目录。
    /// </summary>
    public static class TidePaths
    {
        private static string _dataPath;
        private static string _streamingAssetsPath;
        private static string _persistentDataPath;
        private static Func<string, string> _textLoader;

        /// <summary>宿主装配（Unity：Application 三个路径原样传入）。</summary>
        public static void SetRoots(string dataPath, string streamingAssetsPath, string persistentDataPath)
        {
            _dataPath = dataPath;
            _streamingAssetsPath = streamingAssetsPath;
            _persistentDataPath = persistentDataPath;
        }

        /// <summary>宿主装配配置文本加载器（2026-09-30 合并定案）：真机 Launch→YooAsset 热更包。
        /// 入参=相对 DataPath 的路径，返回文本或 null；不装配=文件直读。</summary>
        public static void SetTextLoader(Func<string, string> relativePathToText)
            => _textLoader = relativePathToText;

        /// <summary>
        /// 配置文本读取收口：宿主装配的加载器优先（真机 YooAsset TextAsset，地址=文件名）；
        /// 回落 TidePaths 文件直读（编辑器直启与服务器同一仓库布局，保留直改 JSON 后 Reload 免重启）。
        /// 两级均未命中返回 null，调用方负责报错（口径同 HotUpdateAssets 回落链）。
        /// </summary>
        public static string ReadConfigText(string relativePath)
        {
            string viaHost = _textLoader?.Invoke(relativePath);
            if (!string.IsNullOrEmpty(viaHost)) return viaHost;

            string path = Path.Combine(DataPath, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>项目 Assets 目录（配置表 Assets/Configs 相对此路径）。</summary>
        public static string DataPath => _dataPath ??= LocateAssetsRoot();

        /// <summary>流资产目录（卡牌用户数据 Card/ 在其下）。</summary>
        public static string StreamingAssetsPath
            => _streamingAssetsPath ??= Path.Combine(DataPath, "StreamingAssets");

        /// <summary>持久数据目录（玩家构建=真实 persistent；无装配回落 StreamingAssets，见类注）。</summary>
        public static string PersistentDataPath
            => _persistentDataPath ??= Path.Combine(DataPath, "StreamingAssets");

        private static string LocateAssetsRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                var probe = Path.Combine(dir.FullName, "Assets", "Configs", "AttributeValueConfig.json");
                if (File.Exists(probe))
                    return Path.Combine(dir.FullName, "Assets");
            }
            return Directory.GetCurrentDirectory(); // 兜底：宿主应显式 SetRoots
        }
    }
}
