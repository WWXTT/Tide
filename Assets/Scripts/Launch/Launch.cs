// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停，恢复步骤见项目根《热更插件解除与恢复.md》
#if TIDE_HYBRID_YOO
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace Tide.Launch
{
    /// <summary>
    /// 启动引导：YooAsset 补丁流程 → HybridCLR 热更程序集加载 → 加载主场景。
    /// 挂在 Launch 场景（Assets/Launch/Launch.unity，BuildSettings 第 0 个场景）；
    /// 主场景由 YooAsset 收集打包，不再进 BuildSettings。
    /// 编辑器下直接 Play 主场景仍走原有启动路径，不受影响。
    /// </summary>
    public sealed class Launch : MonoBehaviour
    {
        public const string PackageName = "DefaultPackage";

        /// <summary>主场景的 YooAsset 寻址地址（AddressByFileName，Main.unity → "Main"）</summary>
        public const string MainSceneLocation = "Main";

        [Header("资源运行模式")]
        [Tooltip("EditorSimulateMode=编辑器模拟；HostPlayMode=真机热更；OfflinePlayMode=单机；WebPlayMode=WebGL")]
        public EPlayMode PlayMode = EPlayMode.EditorSimulateMode;

        [Header("热更资源服务器（HostPlayMode 用）")]
        public string HostServerUrl = "";

        private void Start()
        {
            Application.targetFrameRate = 60;
            RunFlow().Forget();
        }

        private async UniTaskVoid RunFlow()
        {
            while (true)
            {
                YooAssets.Initialize();
                if (!YooAssets.TryGetPackage(PackageName, out ResourcePackage package))
                    package = YooAssets.CreatePackage(PackageName);

                if (!await PatchFlow.RunAsync(package, PlayMode, HostServerUrl))
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
