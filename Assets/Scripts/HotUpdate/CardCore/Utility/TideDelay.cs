using System;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 跨宿主延迟（2026-09-27 去 Unity 化）：UniTask.Delay 依赖 Unity PlayerLoop
    /// （NuGet 构建不含该 API），双端统一改由 Task.Delay 支撑。
    /// 语义=真实时钟（不受 timeScale 影响）——只用于超时竞速安全网（本地 UI 兜底 /
    /// 网络反问 +1.5s 宽限），真实时钟正是超时语义想要的口径。
    /// </summary>
    internal static class TideDelay
    {
        public static async UniTask After(TimeSpan delay)
            => await System.Threading.Tasks.Task.Delay(delay);
    }
}
