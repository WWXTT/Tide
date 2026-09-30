using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace Tide.Launch
{
    /// <summary>
    /// YooAsset 3.0 原生 API 的顺序补丁流程：初始化包裹 →（联机）版本号 → 清单 → 下载。
    /// 替代旧项目的 GameFramework+UniFramework 状态机方案，直接用 UniTask 顺序流。
    /// </summary>
    public static class PatchFlow
    {
        public static async UniTask<bool> RunAsync(ResourcePackage package, EPlayMode playMode, string hostServerUrl)
        {
            InitializePackageOperation initOp = InitializePackage(package, playMode, hostServerUrl);
            if (!await WaitOp(initOp, "初始化资源包"))
                return false;

            // 所有模式统一走 版本号→激活清单：SetActiveManifest 只在 LoadPackageManifestAsync 内发生，
            // 缺了这步 LoadSceneAsync 会抛 "Active package manifest not found"（模拟/离线版本号由本地文件系统提供）
            var versionOp = package.RequestPackageVersionAsync();
            if (!await WaitOp(versionOp, "获取资源版本"))
                return false;
            PatchConsole.Info($"资源版本：{versionOp.PackageVersion}");

            var manifestOp = package.LoadPackageManifestAsync(new LoadPackageManifestOptions(versionOp.PackageVersion, 60));
            if (!await WaitOp(manifestOp, "加载资源清单"))
                return false;

            // HostPlayMode 显式下载全部资源包；WebPlayMode 按需流式加载，跳过下载器
            if (playMode == EPlayMode.HostPlayMode)
            {
                var downloader = package.CreateResourceDownloader(new ResourceDownloaderOptions(8, 3));
                if (downloader.TotalDownloadCount > 0)
                {
                    PatchConsole.Info($"发现 {downloader.TotalDownloadCount} 个文件待更新");
                    downloader.StartDownload();
                    while (!downloader.IsDone)
                    {
                        PatchConsole.Progress(
                            downloader.CurrentDownloadCount, downloader.TotalDownloadCount,
                            downloader.CurrentDownloadBytes, downloader.TotalDownloadBytes);
                        await UniTask.Yield();
                    }
                    if (downloader.Status != EOperationStatus.Succeeded)
                    {
                        PatchConsole.Error($"资源下载失败：{downloader.Error}");
                        return false;
                    }
                }
            }

            return true;
        }

        private static InitializePackageOperation InitializePackage(ResourcePackage package, EPlayMode playMode, string hostServerUrl)
        {
            switch (playMode)
            {
                case EPlayMode.EditorSimulateMode:
                {
#if UNITY_EDITOR
                    var buildResult = EditorSimulateBuildInvoker.Build(Launch.PackageName, (int)EBundleType.VirtualAssetBundle);
                    var options = new EditorSimulateModeOptions();
                    options.EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
                    return package.InitializePackageAsync(options);
#else
                    throw new NotSupportedException("EditorSimulateMode 仅编辑器可用");
#endif
                }
                case EPlayMode.OfflinePlayMode:
                {
                    var options = new OfflinePlayModeOptions();
                    options.BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
                    return package.InitializePackageAsync(options);
                }
                case EPlayMode.HostPlayMode:
                {
                    var options = new HostPlayModeOptions();
                    options.BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
                    options.CacheFileSystemParameters = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(new TideRemoteService(hostServerUrl));
                    return package.InitializePackageAsync(options);
                }
                case EPlayMode.WebPlayMode:
                {
                    var options = new WebPlayModeOptions();
                    options.WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
                    return package.InitializePackageAsync(options);
                }
                default:
                    throw new NotSupportedException($"未支持的运行模式：{playMode}");
            }
        }

        /// <summary>等待 YooAsset 操作完成并汇报结果（自带进度轮询）</summary>
        internal static async UniTask<bool> WaitOp(AsyncOperationBase op, string stage)
        {
            PatchConsole.Info($"{stage}…");
            while (!op.IsDone)
            {
                PatchConsole.Spin(op.Progress);
                await UniTask.Yield();
            }
            if (op.Status != EOperationStatus.Succeeded)
            {
                PatchConsole.Error($"{stage}失败：{op.Error}");
                return false;
            }
            return true;
        }
    }

    /// <summary>远端资源地址服务：单 CDN 源，地址形如 {baseUrl}/{fileName}</summary>
    public sealed class TideRemoteService : IRemoteService
    {
        private readonly string _baseUrl;

        public TideRemoteService(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public IReadOnlyList<string> GetRemoteUrls(string fileName)
        {
            return new[] { $"{_baseUrl}/{fileName}" };
        }
    }
}
