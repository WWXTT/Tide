// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停；恢复=开宏+HybridCLR enable+补包三步（见 查缺补漏.md「待办总账」第 1 条；编辑器菜单 Tools/热更/1-5）
#if TIDE_HYBRID_YOO
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tide.Launch;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace Tide.HotUpdateTools
{
    // Launch 类与 Tide.Launch 命名空间同名：别名必须写在命名空间声明内（同 HotUpdateSetupTool）
    using Launch = Tide.Launch.Launch;

    /// <summary>
    /// YooAsset 资源打包（2026-10-18 换装，替代旧 StreamingAssets 三件套：artstatic/monsterrig/配置镜像）：
    /// 模型名单刷新（#names 数据源）→ 热更 DLL 编译拷贝（复用 Tools/热更/4，HotUpdateDll 组数据源）→
    /// LegacyBuildPipeline 构建 DefaultPackage（LZ4）→ 首包产物 ClearAndCopyAll 拷入
    /// Assets/StreamingAssets/yoo/DefaultPackage——重出玩家包即带齐（OfflinePlayMode 直读该目录）。
    /// LegacyBuildPipeline 基于 BuildPipeline.BuildAssetBundles（旧三件套同引擎 API，零新增包依赖；
    /// SBP 需 com.unity.build-pipeline 未安装）。只打当前激活平台：Yoo 首包目录无平台子目录，
    /// 故每平台在 Bundles/{平台}/DefaultPackage/BuiltinSnapshot 留一份首包快照——
    /// 一键出包时快照新鲜（bundle 输入源无变动）则整目录拷回首包目录，免重编 DLL/重打 bundle，
    /// 切平台来回出包零重出（勿在 VFX/URP 导入进行中切目标——有存坏前科）。
    /// 资源/热更代码改动后快照即过期，出包自动全量重建；构建报告见 Bundles/{平台}/DefaultPackage/{版本}/。
    /// </summary>
    public static class YooPackBuilder
    {
        private const string RigRoot = "Assets/Art/YGOMonstersRig"; // 与 CollectRigComplete 同源
        private const string ManifestAsset = RigRoot + "/_manifest.txt"; // #names 数据源（双源：RigModelAddress）
        private const string StampFileName = "SnapshotStamp.tide"; // 快照新鲜度戳：mtime(UTC)=基准时刻；同步时跳过不拷

        // 打进 bundle 的全部输入源：任一文件 mtime 晚于快照戳 ⇒ 快照过期，出包前须全量重建（防"包里带旧美术/旧代码"）
        private static readonly string[] WatchedInputs =
        {
            "Assets/Scripts/HotUpdate",
            "Assets/HotUpdateDlls",
            "Assets/AotDlls",
            "Assets/Art",
            "Assets/CardData",
            "Assets/Configs",
            "Assets/TutorialData",
            "Assets/Scenes",
            "Assets/BundleCollectorSetting.asset",
        };

        [MenuItem("Tools/打包/YooAsset资源打包（当前平台）")]
        public static bool BuildCurrentTarget()
        {
            // ① 模型名单（进包 TextAsset，地址 #names；CollectRigComplete 按同口径过滤残缺目录）
            var names = RefreshRigManifest();
            if (names.Count == 0)
            {
                Debug.LogError("[Yoo打包] 源目录无可打包模型（" + RigRoot + "）——prefab+同名png 齐全才算");
                return false;
            }

            // ② 热更 DLL（HotUpdateDll 组数据源；失败即中止——包里带旧代码没有意义）
            HotUpdateSetupTool.CompileHotUpdateDlls();
            if (!File.Exists("Assets/HotUpdateDlls/" + HotUpdateLoader.HotAssemblyName + ".dll.bytes"))
            {
                Debug.LogError("[Yoo打包] 热更 DLL 拷贝失败（详见上一步日志），中止打包");
                return false;
            }

            // ③ YooAsset 构建（收集器配置由 Tools/热更/2 生成）
            var parameters = new LegacyBuildParameters
            {
                BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = EBuildPipeline.LegacyBuildPipeline.ToString(),
                BuildBundleType = (int)EBundleType.AssetBundle,
                BuildTarget = EditorUserBuildSettings.activeBuildTarget,
                PackageName = Launch.PackageName,
                PackageVersion = DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                FileNameStyle = EFileNameStyle.HashName,
                BundledCopyOption = EBundledCopyOption.ClearAndCopyAll, // 首包全量拷贝（清旧防残影）
                CompressOption = ECompressOption.LZ4,
                VerifyBuildingResult = true,
            };
            var result = new LegacyBuildPipeline().Run(parameters, true);
            if (!result.Success)
            {
                Debug.LogError("[Yoo打包] 失败：" + result.FailedTask + " —— " + result.ErrorInfo);
                return false;
            }

            // ④ 对账汇报（首包目录文件数/体积；构建报告在出包目录）
            var builtinDir = parameters.GetBundledRootDirectory();
            var files = Directory.GetFiles(builtinDir, "*", SearchOption.AllDirectories);
            var sizeMb = files.Sum(f => new FileInfo(f).Length) / 1024f / 1024f;

            // ⑤ 平台首包快照（含清单三件套+BuiltinCatalog，可整目录拷回首包目录；一键出包的新鲜度门据此免重出）
            var snapshotDir = WriteSnapshot(parameters.BuildTarget);

            Debug.Log("[Yoo打包] 完成（" + parameters.BuildTarget + "）：版本 " + parameters.PackageVersion
                + "，" + names.Count + " 模型，首包 " + files.Length + " 文件 " + sizeMb.ToString("F1") + " MB → "
                + builtinDir + "；快照 " + snapshotDir + "；出包目录 " + result.OutputPackageDirectory + "——重出玩家包即可带齐");
            return true;
        }

        /// <summary>刷新模型名单 TextAsset（先写盘再 Import，构建须吃到已导入资产）；返回名单。</summary>
        private static List<string> RefreshRigManifest()
        {
            var names = new List<string>();
            if (!Directory.Exists(RigRoot)) return names;
            foreach (var dir in Directory.GetDirectories(RigRoot))
            {
                var n = Path.GetFileName(dir);
                if (!File.Exists(RigRoot + "/" + n + "/" + n + ".prefab")) continue;
                if (!File.Exists(RigRoot + "/" + n + "/" + n + ".png")) continue;
                names.Add(n);
            }
            names.Sort(StringComparer.Ordinal);
            if (names.Count > 0)
            {
                File.WriteAllText(ManifestAsset, string.Join("\n", names) + "\n");
                AssetDatabase.ImportAsset(ManifestAsset);
            }
            return names;
        }

        // ── 平台首包快照：双平台各存一份可直接进 StreamingAssets 的完整首包内容 ──

        private static string SnapshotDir(BuildTarget target)
        {
            return Path.Combine(BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                target.ToString(), Launch.PackageName, "BuiltinSnapshot");
        }

        private static string BuiltinPackageDir()
        {
            return Path.Combine(BundleBuilderHelper.GetStreamingAssetsRoot(), Launch.PackageName);
        }

        /// <summary>首包目录（Yoo 构建产物，含 BuiltinCatalog）快照到 Bundles/{平台}/…/BuiltinSnapshot，并落新鲜度戳。</summary>
        private static string WriteSnapshot(BuildTarget target)
        {
            var srcDir = BuiltinPackageDir();
            var dstDir = SnapshotDir(target);
            if (Directory.Exists(dstDir)) Directory.Delete(dstDir, true);
            Directory.CreateDirectory(dstDir);
            foreach (var f in Directory.GetFiles(srcDir))
            {
                if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                File.Copy(f, Path.Combine(dstDir, Path.GetFileName(f)), true);
            }
            File.WriteAllText(Path.Combine(dstDir, StampFileName),
                DateTime.UtcNow.ToString("o")); // 内容仅供人读；新鲜度基准=本文件自身 mtime(UTC)
            return dstDir;
        }

        /// <summary>快照整目录拷回首包目录（清旧防残影；戳文件不拷）。</summary>
        private static void SyncStreamingAssets(BuildTarget target)
        {
            var srcDir = SnapshotDir(target);
            var dstDir = BuiltinPackageDir();
            if (Directory.Exists(dstDir)) Directory.Delete(dstDir, true);
            Directory.CreateDirectory(dstDir);
            foreach (var f in Directory.GetFiles(srcDir))
            {
                var name = Path.GetFileName(f);
                if (name == StampFileName || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                File.Copy(f, Path.Combine(dstDir, name), true);
            }
            AssetDatabase.Refresh();
        }

        /// <summary>资源就绪门：快照新鲜→整目录同步复用（秒级，跳过 DLL 编译与 bundle 构建）；缺失/过期→全量重建。</summary>
        private static bool EnsureStreamingAssetsReady()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var stampPath = Path.Combine(SnapshotDir(target), StampFileName);
            if (File.Exists(stampPath))
            {
                var stampUtc = File.GetLastWriteTimeUtc(stampPath);
                string newestFile;
                if (!AnyInputNewerThan(stampUtc.AddSeconds(2), out newestFile))
                {
                    SyncStreamingAssets(target);
                    Debug.Log("[Yoo打包] 复用首包快照（跳过资源重建）：" + SnapshotDir(target)
                        + "，快照时间 " + stampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    return true;
                }
                Debug.Log("[Yoo打包] 首包快照已过期（最新变动：" + newestFile + "），走全量重建");
            }
            else
            {
                Debug.Log("[Yoo打包] 当前平台无首包快照（" + SnapshotDir(target) + "），走全量重建");
            }
            return BuildCurrentTarget();
        }

        /// <summary>监视源里是否有文件晚于阈值（mtime 全目录扫描）；newestFile=最新那个文件（日志取证用）。</summary>
        private static bool AnyInputNewerThan(DateTime thresholdUtc, out string newestFile)
        {
            var newest = DateTime.MinValue;
            newestFile = null;
            foreach (var root in WatchedInputs)
            {
                DateTime t;
                if (File.Exists(root)) // 单文件监视项（BundleCollectorSetting.asset）
                {
                    t = File.GetLastWriteTimeUtc(root);
                    if (t > newest)
                    {
                        newest = t;
                        newestFile = root;
                    }
                    continue;
                }
                if (!Directory.Exists(root)) continue;
                foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    t = File.GetLastWriteTimeUtc(f);
                    if (t > newest)
                    {
                        newest = t;
                        newestFile = f;
                    }
                }
            }
            return newest > thresholdUtc;
        }

        [MenuItem("Tools/打包/一键出玩家包（当前平台）")]
        public static void BuildPlayerPackage()
        {
            // 前置校验：Launch 唯一场景（Main 由 YooAsset 打包，双进会致 UnityLinker 解析不到热更程序集）
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length != 1 || scenes[0] != "Assets/Scenes/Launch.unity")
            {
                Debug.LogError("[出包] BuildSettings 场景表须为 Launch 唯一（当前：" + string.Join(",", scenes) + "）——先跑 Tools/热更/2 与 3");
                return;
            }

            if (!EnsureStreamingAssetsReady()) return; // 快照新鲜=秒级同步复用；过期/缺失=全量重建；失败即中止出包

            var target = EditorUserBuildSettings.activeBuildTarget;
            string ext = target == BuildTarget.Android ? ".apk"
                : (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows) ? ".exe" : "";
            string outDir = Path.Combine("Builds", target.ToString());
            Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outDir, "Tide" + ext),
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log($"[出包] 成功 → {options.locationPathName}（{report.summary.totalSize / 1024 / 1024} MB；HybridCLR 已剥离热更程序集，AOT 补充 dll 见上一步日志）");
            else
                Debug.LogError("[出包] 失败：" + report.summary.result + "（详见 Editor.log）");
        }
    }
}
#endif
