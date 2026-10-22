// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停；恢复=开宏+HybridCLR enable+补包三步（见 查缺补漏.md「待办总账」第 1 条；编辑器菜单 Tools/热更/1-5）
#if TIDE_HYBRID_YOO
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace Tide.Launch
{
    /// <summary>
    /// 启动引导：YooAsset 补丁流程 → HybridCLR 热更程序集加载 → 加载主场景。
    /// 挂在 Launch 场景（Assets/Scenes/Launch.unity，BuildSettings 第 0 个场景）；
    /// 主场景由 YooAsset 收集打包，不再进 BuildSettings。
    /// 编辑器下直接 Play 主场景仍走原有启动路径，不受影响。
    /// </summary>
    public sealed class Launch : MonoBehaviour
    {
        public const string PackageName = "DefaultPackage";

        /// <summary>主场景的 YooAsset 寻址地址（AddressByFileName，Main.unity → "Main"）</summary>
        public const string MainSceneLocation = "Main";

        [Header("资源运行模式")]
        [Tooltip("预留：接 CDN 后改 EffectivePlayMode 返回 HostPlayMode 并填 HostServerUrl。当前实际取值=代码分支：编辑器 EditorSimulateMode（全链模拟），真机 OfflinePlayMode（首包全量内置=视为已下载）")]
        public EPlayMode PlayMode = EPlayMode.EditorSimulateMode;

        [Header("热更资源服务器（HostPlayMode 用）")]
        public string HostServerUrl = "";

        /// <summary>实际运行模式（2026-10-18 定案）：出包自带全部资源、视为已下载——真机
        /// OfflinePlayMode 直读首包（零网络逻辑）；有热更地址后在真机分支改返 HostPlayMode
        /// 并填 HostServerUrl（PatchFlow 两种模式都已实现）。</summary>
        private EPlayMode EffectivePlayMode
        {
            get
            {
#if UNITY_EDITOR
                return EPlayMode.EditorSimulateMode;
#else
                return EPlayMode.OfflinePlayMode;
#endif
            }
        }

        private void Start()
        {
            Application.targetFrameRate = Application.isMobilePlatform ? 30 : 60;
            RunFlow().Forget();
        }

        private async UniTaskVoid RunFlow()
        {
            while (true)
            {
                YooAssets.Initialize();
                if (!YooAssets.TryGetPackage(PackageName, out ResourcePackage package))
                    package = YooAssets.CreatePackage(PackageName);

                if (!await PatchFlow.RunAsync(package, EffectivePlayMode, HostServerUrl))
                {
                    await PatchConsole.WaitRetryAsync();
                    continue;
                }

#if UNITY_EDITOR
                // 编辑器下热更程序集由 Unity 直接编译，不走 DLL 加载
                if (!HotUpdateLoader.InvokeEditorEntry())
                {
                    await PatchConsole.WaitRetryAsync();
                    continue;
                }
#elif !UNITY_WEBGL
                // WebGL 不支持 HybridCLR，热更代码直接打进包内，跳过 DLL 加载
                if (!await HotUpdateLoader.LoadDeviceAsync(package))
                {
                    await PatchConsole.WaitRetryAsync();
                    continue;
                }
#endif

                var handle = package.LoadSceneAsync(MainSceneLocation);
                while (!handle.IsDone)
                    await UniTask.Yield();
                if (handle.Status != EOperationStatus.Succeeded)
                {
                    PatchConsole.Error($"主场景加载失败：{MainSceneLocation}");
                    await PatchConsole.WaitRetryAsync();
                    continue;
                }

                handle.ActivateScene();
                PatchConsole.Info("启动完成");
                return;
            }
        }

        private void OnGUI()
        {
            PatchConsole.Draw();
        }
    }
}
#endif
