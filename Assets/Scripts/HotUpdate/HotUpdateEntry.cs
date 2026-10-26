using UnityEngine;

namespace Tide.HotUpdate
{
    /// <summary>
    /// 热更程序集入口。由 Tide.Launch 通过反射调用（编辑器直调已编译程序集，真机 Assembly.Load DLL）。
    /// 当前为验证热更链路的桩入口；后续 CardCore / UI / 网络表现层代码分批迁入本程序集。
    /// 注意：WebGL 构建下本程序集直接打进包内且入口不被调用，游戏仍由 Main 场景自有启动路径引导。
    /// </summary>
    public static class HotUpdateEntry
    {
        public const string Version = "0.1.0";

        public static void Start()
        {
            InstallUnityHost();

            // 共享源配置读取装配（2026-09-30 合并定案）：CardCore 配置表统一 TidePaths.ReadConfigText，
            // 真机经此走 YooAsset 热更包（地址=文件名 AddressByFileName）；编辑器由 HotUpdateAssets 内部 File 兜底。
            CardCore.TidePaths.SetTextLoader(LoadConfigText);

            // TapTap 模块（2026-10-24 预编译隔离）：扫域安装 Tide.TapTap 程序集（本地单机风味
            // 不打包该程序集——登录/云存档/联机 TapTap 面板整体缺席）。云存档链路
            // Init→静默登录(basic_info)→启动同步全链旁路（仅 Android 真机），失败不阻断本地玩法。
            TapBridge.InstallFromLoadedAssemblies();
            // 隐私合规闸（2026-10-24 TapTap 上架）：仅联机风味有第三方 SDK 初始化；
            // 本地单机（Module=null）不涉及个人信息采集，不参与同意流程。
            if (TapBridge.HasModule)
            {
                if (SynergyUI.PrivacyConsent.IsGranted) TapBridge.Module?.InitOnStartup();
                else Debug.Log("[PrivacyConsent] 首启未同意隐私政策——TapTap 模块初始化延后至同意弹窗裁决");
            }

            Debug.Log($"[HotUpdate] Tide.HotUpdate v{Version} 启动（程序集入口调用成功，热更链路可用）");
        }

        /// <summary>
        /// 真机热更装配（2026-10-18 热更管线恢复）：TideUnityRuntime 在 AOT 侧被热更宏编译掉后，
        /// TidePaths 三路径 / TideLog 接收器 / EventManager 判活由热更侧装配；编辑器下重复装配幂等。
        /// </summary>
        private static void InstallUnityHost()
        {
            CardCore.TidePaths.SetRoots(
                Application.dataPath,
                Application.streamingAssetsPath,
                Application.persistentDataPath);

            CardCore.TideLog.Sink = (level, message) =>
            {
                switch (level)
                {
                    case CardCore.TideLogLevel.Info: Debug.Log(message); break;
                    case CardCore.TideLogLevel.Warning: Debug.LogWarning(message); break;
                    case CardCore.TideLogLevel.Error: Debug.LogError(message); break;
                }
            };

            CardCore.EventManager.AliveTester = target => !(target is UnityEngine.Object destroyed) || destroyed;

            // 玩家端种子读取器：CardDataPaths.EnsureBootstrap 经此从热更包读种子写 persistent
            // （宿主装配模式，TideServer 不装配即空转；地址=文件名，与 CardData/TutorialData 组寻址一致）
            SynergyUI.CardDataPaths.PackTextReader = relative =>
                HotUpdateAssets.LoadText(System.IO.Path.GetFileNameWithoutExtension(relative), relative);
        }

        private static string LoadConfigText(string relativePath)
            => HotUpdateAssets.LoadText(
                System.IO.Path.GetFileNameWithoutExtension(relativePath), relativePath);
    }
}
