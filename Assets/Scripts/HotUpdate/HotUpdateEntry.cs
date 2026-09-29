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
            Debug.Log($"[HotUpdate] Tide.HotUpdate v{Version} 启动（程序集入口调用成功，热更链路可用）");
        }
    }
}
