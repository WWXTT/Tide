#if UNITY_EDITOR || !TIDE_HYBRID_YOO
using CardCore;
#endif
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CardCore.UnityHost
{
    /// <summary>
    /// Unity 宿主装配（2026-09-27 去 Unity 化）：把纯 C# 基础设施的宿主钩子装上 UnityEngine 语义——
    /// TidePaths（Application 三路径）/ TideLog（Debug 接收器）/ EventManager.AliveTester（UO 判活）。
    /// 编辑器域加载即装（覆盖验证器/batchmode）；玩家构建 BeforeSceneLoad 再装一次（幂等）。
    /// 本文件只在 Unity 编译集内（Assets/Scripts 根，不在 TideServer 的 CardCore 共享源里）。
    /// 2026-10-18 热更管线恢复：Tide.HotUpdate 被 HybridCLR 排除出 AOT 包后，本文件若在真机构建里
    /// 保留编译期引用，UnityLinker 会解析不到 Tide.HotUpdate 直接致命错——故真机+热更宏下整体编译掉，
    /// 装配职责移交热更侧 HotUpdateEntry.Start（同逻辑幂等）；编辑器直启 Main 与关宏构建仍由本文件装配。
    /// </summary>
#if UNITY_EDITOR || !TIDE_HYBRID_YOO
    public static class TideUnityRuntime
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            TidePaths.SetRoots(
                Application.dataPath,
                Application.streamingAssetsPath,
                Application.persistentDataPath);

            TideLog.Sink = (level, message) =>
            {
                switch (level)
                {
                    case TideLogLevel.Info: Debug.Log(message); break;
                    case TideLogLevel.Warning: Debug.LogWarning(message); break;
                    case TideLogLevel.Error: Debug.LogError(message); break;
                }
            };

            // 订阅者判活：UnityEngine.Object 目标已销毁 = 无效（旧 EventManager 内联逻辑原样上移）
            EventManager.AliveTester = target => !(target is UnityEngine.Object destroyed) || destroyed;
        }
    }
#endif
}
