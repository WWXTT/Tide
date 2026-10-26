// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停；恢复=开宏+HybridCLR enable+补包三步（见 查缺补漏.md「待办总账」第 1 条；编辑器菜单 Tools/热更/1-5）
#if TIDE_HYBRID_YOO
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor.Settings;
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
        // 2026-10-24 增补两项（TapTap 预编译隔离）：manifest 与 TapTap 源码目录——风味切换必然触碰
        // 两者 mtime，另一风味的旧快照自动判过期，杜绝跨风味误复用
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
            "Packages/manifest.json",
            "Assets/Scripts/TapTap",
        };

        [MenuItem("Tools/打包/YooAsset资源打包（当前平台）")]
        public static bool BuildCurrentTarget()
        {
            // 风味一致性预检（2026-10-24）：混合态（如 pref=单机但源码在场）下打的快照会名不副实——拦下指路
            if (!CheckFlavorConsistency(CurrentFlavor)) return false;

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
            var snapshotDir = WriteSnapshot(parameters.BuildTarget, CurrentFlavor);

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

        /// <summary>快照目录带风味维度（2026-10-24 TapTap 预编译隔离）：联机=现状路径零迁移；
        /// 单机=BuiltinSnapshot-Local——两风味快照互不串门，切换必然触发全量重建。</summary>
        private static string SnapshotDir(BuildTarget target, BuildFlavor flavor)
        {
            string snapshotName = flavor == BuildFlavor.Local ? "BuiltinSnapshot-Local" : "BuiltinSnapshot";
            return Path.Combine(BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                target.ToString(), Launch.PackageName, snapshotName);
        }

        private static string BuiltinPackageDir()
        {
            return Path.Combine(BundleBuilderHelper.GetStreamingAssetsRoot(), Launch.PackageName);
        }

        /// <summary>首包目录（Yoo 构建产物，含 BuiltinCatalog）快照到 Bundles/{平台}/…/BuiltinSnapshot，并落新鲜度戳。
        /// 戳首行=风味名（EnsureStreamingAssetsReady 读回校验防串门）；新鲜度基准仍是本文件自身 mtime(UTC)。</summary>
        private static string WriteSnapshot(BuildTarget target, BuildFlavor flavor)
        {
            var srcDir = BuiltinPackageDir();
            var dstDir = SnapshotDir(target, flavor);
            if (Directory.Exists(dstDir)) Directory.Delete(dstDir, true);
            Directory.CreateDirectory(dstDir);
            foreach (var f in Directory.GetFiles(srcDir))
            {
                if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                File.Copy(f, Path.Combine(dstDir, Path.GetFileName(f)), true);
            }
            File.WriteAllText(Path.Combine(dstDir, StampFileName),
                flavor + "\n" + DateTime.UtcNow.ToString("o"));
            return dstDir;
        }

        /// <summary>快照整目录拷回首包目录（清旧防残影；戳文件不拷）。</summary>
        private static void SyncStreamingAssets(BuildTarget target, BuildFlavor flavor)
        {
            var srcDir = SnapshotDir(target, flavor);
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

        /// <summary>读快照戳首行的风味名（旧格式时间戳/读失败一律按 TapTap——存量快照都是联机风味）。</summary>
        private static BuildFlavor ReadStampFlavor(string stampPath)
        {
            try
            {
                var first = File.ReadLines(stampPath).FirstOrDefault() ?? "";
                return first == BuildFlavor.Local.ToString() ? BuildFlavor.Local : BuildFlavor.TapTap;
            }
            catch { return BuildFlavor.TapTap; }
        }

        /// <summary>资源就绪门：快照新鲜→整目录同步复用（秒级，跳过 DLL 编译与 bundle 构建）；缺失/过期/风味不符→全量重建。</summary>
        private static bool EnsureStreamingAssetsReady(BuildFlavor flavor)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var stampPath = Path.Combine(SnapshotDir(target, flavor), StampFileName);
            if (File.Exists(stampPath))
            {
                var stampFlavor = ReadStampFlavor(stampPath);
                if (stampFlavor != flavor)
                {
                    Debug.Log($"[Yoo打包] 首包快照风味不符（戳={stampFlavor}，当前={flavor}），走全量重建");
                    return BuildCurrentTarget();
                }
                var stampUtc = File.GetLastWriteTimeUtc(stampPath);
                string newestFile;
                if (!AnyInputNewerThan(stampUtc.AddSeconds(2), out newestFile))
                {
                    SyncStreamingAssets(target, flavor);
                    Debug.Log("[Yoo打包] 复用首包快照（跳过资源重建）：" + SnapshotDir(target, flavor)
                        + "，快照时间 " + stampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    return true;
                }
                Debug.Log("[Yoo打包] 首包快照已过期（最新变动：" + newestFile + "），走全量重建");
            }
            else
            {
                Debug.Log("[Yoo打包] 当前平台无该风味首包快照（" + SnapshotDir(target, flavor) + "），走全量重建");
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
        public static void BuildPlayerPackage() => BuildPlayerPackageAuto(BuildFlavor.TapTap);

        [MenuItem("Tools/打包/一键出玩家包-本地单机（当前平台）")]
        public static void BuildLocalPlayerPackage() => BuildPlayerPackageAuto(BuildFlavor.Local);

        /// <summary>一键出包入口（2026-10-24 自动切风味）：环境一致→直出（零新增交互）；
        /// 不一致→确认后自动切换并挂起，编译重载完成由 FlavorBuildContinuation 自动续跑出包。</summary>
        private static void BuildPlayerPackageAuto(BuildFlavor flavor)
        {
            if (FlavorEnvironmentMatches(flavor))
            {
                BuildPlayerPackageCore(flavor);
                return;
            }
            var from = CurrentFlavor;
            if (!EditorUtility.DisplayDialog("切换风味并出包",
                $"当前是{FlavorName(from)}环境，出{FlavorName(flavor)}包需先切换风味：\n\n"
                + (flavor == BuildFlavor.Local
                    ? "· manifest 移除 4 个 com.taptap.sdk.* 包（SDK/原生库不再编译）\n"
                      + "· Assets/Scripts/TapTap 改名 TapTap~（不编译不打包）\n"
                      + "· HybridCLR 热更列表收窄为 Tide.HotUpdate 单程序集\n"
                      + "· 删除已拷贝的 Tide.TapTap.dll.bytes"
                    : "· manifest/源码目录/HybridCLR 热更列表自动恢复，gradle taptap 依赖按留底回放")
                + "\n\n切换并等编译重载后将自动继续出包（无需再点菜单）。继续？", "切换并出包", "取消"))
                return;
            if (flavor == BuildFlavor.Local) ApplyLocalFlavor();
            else ApplyTapTapFlavor();
            FlavorBuildContinuation.ArmPendingBuild(flavor);
        }

        private static string FlavorName(BuildFlavor flavor)
            => flavor == BuildFlavor.Local ? "本地单机" : "联机（TapTap）";

        /// <summary>出包主体（internal=自动续跑共用）。返回值=出包成败（2026-10-24 自动切风味升级）。</summary>
        internal static bool BuildPlayerPackageCore(BuildFlavor flavor)
        {
            // 风味环境预检（2026-10-24 TapTap 预编译隔离）：源码目录/manifest/EditorPrefs 三处一致才放行
            //（防在单机环境下用联机菜单把联机快照覆盖成脏包，反之亦然）
            if (!CheckFlavorConsistency(flavor)) return false;

            // DSH 沙箱 Low 标签防护（2026-10-24 出包三连败根因，详见 EnsureBurstToolchainWritable 注释）
            if (!EnsureBurstToolchainWritable()) return false;

            // 安卓风味守卫：单机=兜底摘净 taptap maven 行；联机=缺行即拦（缺原生依赖装机登录必坏）
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.Android)
            {
                if (flavor == BuildFlavor.Local) StripTapGradleDeps();
                else if (!GradleHasTapDeps())
                {
                    Debug.LogError("[出包] TapTap 风味安卓包缺 taptap maven 依赖（mainTemplate.gradle）——"
                        + "跑 External Dependency Manager/Android Resolver/Resolve 后重试");
                    return false;
                }
            }

            // 前置校验：Launch 唯一场景（Main 由 YooAsset 打包，双进会致 UnityLinker 解析不到热更程序集）
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length != 1 || scenes[0] != "Assets/Scenes/Launch.unity")
            {
                Debug.LogError("[出包] BuildSettings 场景表须为 Launch 唯一（当前：" + string.Join(",", scenes) + "）——先跑 Tools/热更/2 与 3");
                return false;
            }

            if (!EnsureStreamingAssetsReady(flavor)) return false; // 快照新鲜=秒级同步复用；过期/缺失/风味不符=全量重建；失败即中止出包

            string ext = target == BuildTarget.Android ? ".apk"
                : (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows) ? ".exe" : "";
            string outDir = Path.Combine("Builds", target.ToString());
            string productName = flavor == BuildFlavor.Local ? "Tide-Local" : "Tide";
            Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outDir, productName + ext),
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[出包] 成功（{flavor} 风味） → {options.locationPathName}"
                    + $"（{report.summary.totalSize / 1024 / 1024} MB；HybridCLR 已剥离热更程序集，AOT 补充 dll 见上一步日志）");
                if (flavor == BuildFlavor.Local)
                {
                    if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
                        AssertNoTapNativePlugins(options.locationPathName); // 单机包零 TapTap 原生件的数据层断言
                    Debug.Log("[出包] 本地单机包已出——开发环境恢复菜单：Tools/打包/风味/切回联机TapTap（恢复）");
                }
                return true;
            }
            Debug.LogError("[出包] 失败：" + report.summary.result + "（详见 Editor.log）");
            return false;
        }

        // ── DSH 沙箱 Low 完整性标签防护（2026-10-24 定案）────────────────────────────
        // 根因：DeepSeek Harness（DSH）把工程目录设为工作区时，其 Windows 沙箱（app.asar 内
        // runner.js）给整棵工程树写三条可继承 ACE：Low 强制完整性标签 + S-1-4-* 能力 SID 写授权 +
        // Everyone 防删 DENY，且每次 DSH 会话重新刷写（USN 日志实测一次 19.7 万条 ACL 变更）。
        // 后果：Library/PackageCache/com.unity.burst@*/.Runtime/bcl.exe 继承 Low 标签 →
        // Burst AOT 编译器以 Low 完整性进程运行 → 写不进 Medium 标签的 %TEMP%（lld 响应文件）→
        // 出包末段 AsyncPluginsFromLinker 步骤必败：
        // "Burst internal compiler error: System.UnauthorizedAccessException: 对路径的访问被拒绝"
        //（该步在构建收尾处，白跑约 20 分钟才报错，故出包入口先行拦截）。
        // 修法（O(1)，不动系统 %TEMP% 全局标签）：把 .Runtime 下各 exe 的完整性标签提回 Medium。
        // 写 SACL 需管理员权限：编辑器提权时自动修复；未提权则自动修复失败——给出手动命令并中止出包。
        // 手动等价（管理员终端，<hash> 为 burst 包哈希）：
        //   icacls "Library\PackageCache\com.unity.burst@<hash>\.Runtime\*.exe" /setintegritylevel medium
        // 撤销本防护 = 删掉 BuildPlayerPackageCore 开头的调用即可。
        private static bool EnsureBurstToolchainWritable()
        {
            var pkgRoot = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");
            if (!Directory.Exists(pkgRoot)) return true; // 异常环境无 PackageCache：不拦出包
            var broken = new List<string>();
            foreach (var runtimeDir in Directory.GetDirectories(pkgRoot, "com.unity.burst@*")
                         .Select(d => Path.Combine(d, ".Runtime")).Where(Directory.Exists))
            {
                foreach (var exe in Directory.GetFiles(runtimeDir, "*.exe"))
                {
                    if (!HasLowIntegrityLabel(exe)) continue;
                    if (TrySetIntegrityLevel(exe, "medium")) continue; // 已提回 Medium：放行
                    broken.Add(exe);
                }
            }
            if (broken.Count == 0) return true;
            Debug.LogError("[出包] Burst 编译器带 DSH 沙箱的 Low 完整性标签（Low 进程写不进 %TEMP%，"
                + "构建末段必败于 AsyncPluginsFromLinker）。自动修复需管理员权限未成功——"
                + "请在管理员终端执行以下命令后重试出包：\n"
                + string.Join("\n", broken.Select(p => $"icacls \"{p}\" /setintegritylevel medium")));
            return false;
        }

        /// <summary>icacls 读完整性标签（读免权限；本机 zh-CN 下标签行仍是英文 "Mandatory Label\Low…"，
        /// 若极端本地化则 ASCII 匹配失效=放行不拦，最坏回到无防护的旧行为）</summary>
        private static bool HasLowIntegrityLabel(string path)
        {
            var (code, output) = RunIcacls($"\"{path}\"");
            if (code != 0) return false; // 读不到标签：检测故障不应阻断出包
            return output.Split('\n').Any(l => l.IndexOf("Mandatory", StringComparison.OrdinalIgnoreCase) >= 0
                && l.IndexOf("Low", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool TrySetIntegrityLevel(string path, string level)
        {
            var (code, output) = RunIcacls($"\"{path}\" /setintegritylevel {level}");
            if (code == 0) return true;
            Debug.LogWarning($"[出包] icacls /setintegritylevel {level} 失败（exit {code}，多半编辑器未提权）：{output}");
            return false;
        }

        private static (int code, string output) RunIcacls(string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "icacls",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(10000);
                    return (p.ExitCode, output);
                }
            }
            catch (Exception e)
            {
                return (-1, e.Message);
            }
        }

        // ── 出包风味（2026-10-24 TapTap 预编译隔离定案）──────────────────────────────
        // 联机（TapTap）= 现状全功能；本地单机 = 摘 TapTap（SDK UPM 包 + Tide.TapTap 源码目录 +
        // HybridCLR 第二热更程序集 + 已拷贝的 .dll.bytes）。切换是编辑器环境的临时变换——
        // 每个菜单一步完整执行，域重载发生在菜单返回之后（EditorPrefs 持久化风味状态防丢）。

        // Steam=第三风味预留槽（2026-10-24 占位定案，未实现）：传输缝已留（NetTransports.cs INetChannel/INetTransportHost）；
        // 扩展=本枚举加 Steam 成员 + 按 Local 模式做切换收窄/恢复（摘 Steamworks 包/源码目录），快照目录与产物命名随 flavor 分叉。
        internal enum BuildFlavor { TapTap, Local }

        private const string TapSourceFolder = "Assets/Scripts/TapTap";
        private const string HiddenTapSourceFolder = "Assets/Scripts/TapTap~"; // ~ 后缀=Unity 整体忽略
        private const string StashFolder = "Library/TideFlavorStash";          // manifest 备份与源码目录 .meta 暂存
        private const string ManifestPath = "Packages/manifest.json";
        private const string MainTemplateGradlePath = "Assets/Plugins/Android/mainTemplate.gradle";
        private const string StashedGradlePath = StashFolder + "/mainTemplate.taptap.gradle"; // 摘除前整文件留底（gradle 闭环）
        private const string FlavorPrefKey = "Tide.BuildFlavor";

        private static readonly string[] TapManifestLines =
        {
            "    \"com.taptap.sdk.cloudsave\": \"file:../tapsdk-dist/CloudSave\",",
            "    \"com.taptap.sdk.core\": \"file:../tapsdk-dist/Core\",",
            "    \"com.taptap.sdk.login\": \"file:../tapsdk-dist/Login\",",
            "    \"com.taptap.sdk.onlinebattle\": \"file:../tapsdk-dist/OnlineBattle\",",
        };

        private static BuildFlavor CurrentFlavor =>
            EditorPrefs.GetString(FlavorPrefKey, BuildFlavor.TapTap.ToString()) == BuildFlavor.Local.ToString()
                ? BuildFlavor.Local : BuildFlavor.TapTap;

        private static bool TapSourceVisible => Directory.Exists(TapSourceFolder);

        private static bool ManifestHasTapPackages()
            => File.Exists(ManifestPath) && File.ReadAllText(ManifestPath).Contains("com.taptap.sdk.");

        /// <summary>风味环境三处一致判定（源码目录/manifest/EditorPrefs）——静默版，自动出包路径用。</summary>
        internal static bool FlavorEnvironmentMatches(BuildFlavor expected)
            => expected == BuildFlavor.TapTap
                ? (TapSourceVisible && ManifestHasTapPackages() && CurrentFlavor == BuildFlavor.TapTap)
                : (!TapSourceVisible && !ManifestHasTapPackages() && CurrentFlavor == BuildFlavor.Local);

        /// <summary>风味环境与目标一致才放行出包/资源打包（混合态=切换中断残局——拦下指路切换菜单）。</summary>
        private static bool CheckFlavorConsistency(BuildFlavor expected)
        {
            if (FlavorEnvironmentMatches(expected)) return true;
            Debug.LogError($"[出包] 风味环境不符（期望 {expected}）：TapTap 源码目录{(TapSourceVisible ? "在场" : "已藏")}、"
                + $"manifest{(ManifestHasTapPackages() ? "含" : "无")} TapTap 包、EditorPrefs 风味={CurrentFlavor}——先跑 "
                + (expected == BuildFlavor.TapTap ? "Tools/打包/风味/切回联机TapTap（恢复）" : "Tools/打包/风味/切到本地单机（摘除TapTap）"));
            return false;
        }

        [MenuItem("Tools/打包/风味/切到本地单机（摘除TapTap）")]
        public static void SwitchToLocalFlavor()
        {
            if (!TapSourceVisible && !ManifestHasTapPackages())
            {
                EditorPrefs.SetString(FlavorPrefKey, BuildFlavor.Local.ToString());
                SetHotAssembliesForFlavor(BuildFlavor.Local);
                Debug.Log("[风味] 已处于本地单机环境（源码藏+manifest 净）——可直接出单机包（Tools/打包/一键出玩家包-本地单机）");
                return;
            }
            if (!EditorUtility.DisplayDialog("切到本地单机（摘除 TapTap）",
                "将临时摘除 TapTap（出单机包用，可随时恢复）：\n"
                + "· Packages/manifest.json 移除 4 个 com.taptap.sdk.* 包（SDK/原生库不再编译）\n"
                + "· Assets/Scripts/TapTap 源码目录改名 TapTap~（Unity 忽略——不编译不打包）\n"
                + "· HybridCLR 热更列表收窄为 Tide.HotUpdate 单程序集\n"
                + "· 删除 Assets/HotUpdateDlls 里已拷贝的 Tide.TapTap.dll.bytes\n\n"
                + "编译重载后生效。继续？", "切换", "取消"))
                return;
            ApplyLocalFlavor();
        }

        /// <summary>单机风味落地（菜单确认后/自动化直调共用，幂等——已是单机态则只补预检日志）。</summary>
        public static void ApplyLocalFlavor()
        {
            Directory.CreateDirectory(StashFolder);
            // manifest：摘除前留备份（救援/恢复优先用备份回放）
            if (ManifestHasTapPackages())
                File.Copy(ManifestPath, Path.Combine(StashFolder, "manifest.taptap.json"), true);
            File.WriteAllLines(ManifestPath,
                File.ReadAllLines(ManifestPath).Where(l => !l.TrimStart().StartsWith("\"com.taptap.sdk.")).ToArray());
            // 源码目录隐藏（~ 后缀 Unity 整体忽略；根 .meta 暂存保 GUID，内部 .meta 随目录走）
            if (TapSourceVisible)
            {
                string metaPath = TapSourceFolder + ".meta";
                if (File.Exists(metaPath)) File.Move(metaPath, Path.Combine(StashFolder, "TapTap.meta"));
                Directory.Move(TapSourceFolder, HiddenTapSourceFolder);
            }
            SetHotAssembliesForFlavor(BuildFlavor.Local);
            string tapBytes = "Assets/HotUpdateDlls/" + HotUpdateLoader.OptionalTapAssemblyName + ".dll.bytes";
            if (File.Exists(tapBytes)) AssetDatabase.DeleteAsset(tapBytes);
            EditorPrefs.SetString(FlavorPrefKey, BuildFlavor.Local.ToString());
            AssetDatabase.Refresh();
            UnityEditor.PackageManager.Client.Resolve();
            Debug.Log("[风味] 本地单机切换已提交——等编译重载完成后即可开发/出单机包"
                + "（Tools/打包/一键出玩家包-本地单机）；恢复：Tools/打包/风味/切回联机TapTap（恢复）。"
                + "git 注意：此状态下 TapTap/ 目录显示为删除属预期瞬态——先恢复再提交");
        }

        [MenuItem("Tools/打包/风味/切回联机TapTap（恢复）")]
        public static void SwitchBackToTapTapFlavor() => ApplyTapTapFlavor();

        /// <summary>联机风味落地（菜单/自动出包共用，幂等；2026-10-24 自动切风味升级提取——与 ApplyLocalFlavor 对称，
        /// 含 gradle 留底回放闭环：单机包摘过的 taptap maven 行按留底恢复，无留底则仍靠 EDM4U 重注入）。</summary>
        public static void ApplyTapTapFlavor()
        {
            if (TapSourceVisible && ManifestHasTapPackages())
            {
                EditorPrefs.SetString(FlavorPrefKey, BuildFlavor.TapTap.ToString());
                SetHotAssembliesForFlavor(BuildFlavor.TapTap);
                Debug.Log("[风味] 已处于联机（TapTap）环境——预检通过");
                return;
            }
            // manifest 恢复：优先回放切换时的备份；无备份（如手动动过）则按字母序补插 4 行
            string backup = Path.Combine(StashFolder, "manifest.taptap.json");
            if (File.Exists(backup)) File.Copy(backup, ManifestPath, true);
            else InsertTapManifestLines();
            // 源码目录还原（根 .meta 从暂存取回保 GUID；丢了 Unity 会重新生成，asmdef/文件 GUID 不受影响）
            if (!TapSourceVisible && Directory.Exists(HiddenTapSourceFolder))
            {
                Directory.Move(HiddenTapSourceFolder, TapSourceFolder);
                string metaPath = TapSourceFolder + ".meta";
                string stashedMeta = Path.Combine(StashFolder, "TapTap.meta");
                if (File.Exists(stashedMeta) && !File.Exists(metaPath)) File.Move(stashedMeta, metaPath);
            }
            RestoreTapGradleDeps();
            SetHotAssembliesForFlavor(BuildFlavor.TapTap);
            EditorPrefs.SetString(FlavorPrefKey, BuildFlavor.TapTap.ToString());
            AssetDatabase.Refresh();
            UnityEditor.PackageManager.Client.Resolve();
            Debug.Log("[风味] 已切回联机（TapTap）——等编译重载后，下次出包/资源打包会自动重编 Tide.TapTap.dll；"
                + (ManifestHasTapPackages() ? "" : "⚠ manifest 恢复后仍缺 TapTap 包——请手动跑一次 Package Manager Resolve"));
        }

        /// <summary>HybridCLR 热更列表按风味收窄/恢复（与 HotUpdateSetupTool.SetupHybridClr 同 API 同口径）。</summary>
        private static void SetHotAssembliesForFlavor(BuildFlavor flavor)
        {
            var settings = HybridCLRSettings.LoadOrCreate();
            settings.hotUpdateAssemblies = flavor == BuildFlavor.Local
                ? new[] { HotUpdateLoader.HotAssemblyName }
                : new[] { HotUpdateLoader.HotAssemblyName, HotUpdateLoader.OptionalTapAssemblyName };
            HybridCLRSettings.Save();
            AssetDatabase.SaveAssets();
        }

        /// <summary>manifest 补插 4 行 TapTap 包（按字母序插进 dependencies 块——无备份时的恢复兜底）。</summary>
        private static void InsertTapManifestLines()
        {
            if (ManifestHasTapPackages()) return;
            var lines = new List<string>(File.ReadAllLines(ManifestPath));
            int insertAt = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].TrimStart();
                if (!t.StartsWith("\"com.")) continue; // 只看 dependencies 键行
                if (string.CompareOrdinal(t, "\"com.taptap") > 0) { insertAt = i; break; }
            }
            lines.InsertRange(insertAt, TapManifestLines);
            File.WriteAllLines(ManifestPath, lines);
        }

        /// <summary>mainTemplate.gradle 是否含 taptap maven 依赖（联机安卓包预检用）。</summary>
        private static bool GradleHasTapDeps()
            => File.Exists(MainTemplateGradlePath) && File.ReadAllLines(MainTemplateGradlePath).Any(l => l.Contains("com.taptap.sdk"));

        /// <summary>单机安卓包兜底：摘包后 EDM4U 理应随 resolve 清空注入块；未清则文本摘净 taptap 行。
        /// 真摘除前整文件留底（仅首次——防把已摘态存成备份），切回联机由 RestoreTapGradleDeps 回放成对。</summary>
        private static void StripTapGradleDeps()
        {
            if (!File.Exists(MainTemplateGradlePath)) return;
            var lines = File.ReadAllLines(MainTemplateGradlePath);
            var kept = lines.Where(l => !l.Contains("com.taptap.sdk")).ToArray();
            if (kept.Length == lines.Length)
            {
                Debug.Log("[风味] mainTemplate.gradle 已无 taptap maven 依赖（EDM4U 随摘包清空）");
                return;
            }
            Directory.CreateDirectory(StashFolder);
            if (!File.Exists(StashedGradlePath))
                File.Copy(MainTemplateGradlePath, StashedGradlePath, true);
            File.WriteAllLines(MainTemplateGradlePath, kept);
            AssetDatabase.ImportAsset(MainTemplateGradlePath);
            Debug.Log($"[风味] mainTemplate.gradle 已文本摘除 taptap maven 行 {lines.Length - kept.Length} 条（EDM4U 未自清的兜底；留底 {StashedGradlePath}）");
        }

        /// <summary>gradle 恢复（ApplyTapTapFlavor 调用）：留底在场且当前缺 taptap 行→整文件回放；
        /// 无留底或行已在则不动（EDM4U 随 Resolve 重注入的老路仍有效，出包守卫兜底拦截）。</summary>
        internal static void RestoreTapGradleDeps()
        {
            if (!File.Exists(StashedGradlePath) || GradleHasTapDeps()) return;
            File.Copy(StashedGradlePath, MainTemplateGradlePath, true);
            AssetDatabase.ImportAsset(MainTemplateGradlePath);
            Debug.Log("[风味] mainTemplate.gradle 已回放留底（taptap maven 依赖恢复）：" + StashedGradlePath);
        }

        /// <summary>本地单机 PC 包零 TapTap 原生件断言（数据层验收——tapsdkcore/cloudsave_sdk/onlinebattle 逐一排查）。</summary>
        private static void AssertNoTapNativePlugins(string exePath)
        {
            string dataDir = Path.ChangeExtension(exePath, null) + "_Data";
            string pluginsDir = Path.Combine(dataDir, "Plugins");
            if (!Directory.Exists(pluginsDir))
            {
                Debug.Log("[出包] PASS：本地单机包无 Plugins 目录（零原生插件）");
                return;
            }
            var all = Directory.GetFiles(pluginsDir, "*", SearchOption.AllDirectories);
            var hits = all.Where(f =>
            {
                var n = Path.GetFileName(f).ToLowerInvariant();
                return n.Contains("tapsdk") || n.Contains("cloudsave_sdk") || n.Contains("onlinebattle") || n.Contains("taptap");
            }).ToArray();
            if (hits.Length == 0)
                Debug.Log($"[出包] PASS：本地单机包 Plugins 无 TapTap 原生件（{all.Length} 个文件全检）");
            else
                Debug.LogError("[出包] FAIL：本地单机包发现 TapTap 原生件：" + string.Join(", ", hits));
        }
    }
}
#endif
