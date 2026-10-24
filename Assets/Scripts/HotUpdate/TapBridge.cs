using System;
using System.Linq;
using SynergyUI;
using UnityEngine;

namespace Tide.HotUpdate
{
    /// <summary>
    /// Tide.TapTap 程序集对主热更程序集的挂载缝（2026-10-24 TapTap 预编译隔离定案）。
    /// 双出包风味：联机（TapTap）包带 Tide.TapTap 热更程序集——启动扫域装上，登录/云存档/
    /// 联机 TapTap 面板全量可用；本地单机包不编译不打该程序集——Module 恒 null，一切
    /// TapTap 入口随之缺席（局域网对战保留在 LobbyScreen 本体）。主程序集在本缝之外
    /// 不得再出现任何 TapSDK 引用（Tide.HotUpdate 保持 SDK-free，同 CardCore 对 TideServer 纪律）。
    /// </summary>
    public interface ITapModule
    {
        /// <summary>启动装配（原 CloudSave.TapCloudSaveBootstrap.Init：SDK Init→静默登录→启动同步旁路）。</summary>
        void InitOnStartup();

        /// <summary>主菜单 Build 期挂件：登录徽章 Attach + 云存档入口构建。</summary>
        void OnMainMenuBuilt(MainMenuScreen menu);

        /// <summary>主菜单进场：静默续登 Warmup + 云存档事件订阅 + 挂起恢复询问补问。</summary>
        void OnMainMenuEnter(MainMenuScreen menu);

        /// <summary>主菜单离场：云存档事件退订 + 关面板。</summary>
        void OnMainMenuExit(MainMenuScreen menu);

        /// <summary>联机屏 TapTap 段（登录/建房/房间列表/加入）。仅联机风味可达。</summary>
        ITapLobbySection CreateLobbySection(LobbyScreen screen);
    }

    /// <summary>联机屏 TapTap 段缝（实现住 Tide.TapTap；面板 RectTransform 交回 LobbyScreen 统一换页）。</summary>
    public interface ITapLobbySection
    {
        /// <summary>建 TapTap 面板并往联机入口按钮排插入「TapTap 联机」入口钮（含全部绑定）。</summary>
        void Build(RectTransform root, RectTransform entryButtonsRow);

        /// <summary>拆会话收口（原 TeardownSession 的 TapTap 段：退房+断对战服务）。</summary>
        void Teardown();

        /// <summary>昵称兜底（TapTap 登录名；未登录返回空）。</summary>
        string FallbackNickname();
    }

    /// <summary>
    /// TapTap 模块桥（静态，进程级）。安装路径双保险：HotUpdateEntry.Start 显式扫域装一次
    /// （真机=HotUpdateLoader 先 Assembly.Load 两个热更 DLL 再进入口；联机风味 Tide.TapTap
    /// 在域内即被装上）；编辑器直开 Main 场景 Play 不经 Launch 链——首访问 Module 惰性补装。
    /// 扫不到=本地单机风味，Module=null，一切入口安全退位。
    /// </summary>
    public static class TapBridge
    {
        private const string TapAssemblyName = "Tide.TapTap";
        private const string ModuleTypeName = "SynergyUI.Online.TapModule";

        private static ITapModule _module;
        private static bool _installAttempted;

        /// <summary>TapTap 模块（null=本地单机风味）。未装先惰性补装一次（编辑器直开场景 Play 的兜底）。</summary>
        public static ITapModule Module
        {
            get
            {
                if (_module == null && !_installAttempted) InstallFromLoadedAssemblies();
                return _module;
            }
        }

        /// <summary>TapTap 模块是否已装（联机风味 true——UI 显隐/入口拦截用）。</summary>
        public static bool HasModule => Module != null;

        /// <summary>扫域安装（幂等；HotUpdateEntry.Start 调用）。找不到实现=单机风味，只留一条日志。</summary>
        public static void InstallFromLoadedAssemblies()
        {
            _installAttempted = true;
            if (_module != null) return;
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == TapAssemblyName);
                if (assembly == null)
                {
                    Debug.Log("[TapBridge] Tide.TapTap 未加载（本地单机风味）——TapTap 登录/云存档/联机面板缺席");
                    return;
                }
                var type = assembly.GetType(ModuleTypeName, throwOnError: false);
                if (type == null)
                {
                    Debug.LogError($"[TapBridge] {TapAssemblyName} 缺 {ModuleTypeName}（按单机风味继续）");
                    return;
                }
                _module = (ITapModule)Activator.CreateInstance(type);
                Debug.Log("[TapBridge] TapTap 模块已安装（联机风味）");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TapBridge] TapTap 模块安装失败（按单机风味继续）：{ex}");
            }
        }
    }
}
