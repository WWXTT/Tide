using CardCore;
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
    /// </summary>
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
}
