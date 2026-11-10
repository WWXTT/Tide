using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Network;
using Cysharp.Threading.Tasks;
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

        /// <summary>启动页登录区装配（2026-11-09 登录迁移：登录从主菜单徽章迁入启动页）：
        /// 在 <paramref name="screen"/>.LoginSlot 注入登录按钮/账号态并接线
        /// screen.NotifyLoginStateChanged（已登录→true 触发自动进主菜单）。
        /// 返回登录可用性：false=编辑器/未配置/平台不支持——启动页按游客自动放行。</summary>
        bool AttachLaunchLogin(LaunchScreen screen);

        /// <summary>主菜单 Build 期挂件：云存档入口构建（2026-11-09 起右上登录徽章退役，
        /// 账号信息并入右下云存档入口，登录入口只在启动页）。</summary>
        void OnMainMenuBuilt(MainMenuScreen menu);

        /// <summary>主菜单进场：静默续登 Warmup + 云存档事件订阅 + 挂起恢复询问补问。</summary>
        void OnMainMenuEnter(MainMenuScreen menu);

        /// <summary>主菜单离场：云存档事件退订 + 关面板。</summary>
        void OnMainMenuExit(MainMenuScreen menu);

        /// <summary>联机屏 TapTap 段（登录/建房/房间列表/加入）。仅联机风味可达。</summary>
        ITapLobbySection CreateLobbySection(LobbyScreen screen);
    }

    /// <summary>公开房间摘要（S6b 双源列表用）——中性 DTO：Tide.TapTap 侧从 TapSDK
    /// RoomBasicInfo/RoomInfo 映射。HotUpdate 程序集不得见 TapSDK 类型（隔离纪律）。</summary>
    public sealed class TapRoomEntry
    {
        public string RoomId;
        public string Name;
        public int PlayerCount;
        public int MaxPlayerCount;
        /// <summary>是否有密码（房间 customProperties 带 pwd——列表行提示用）。</summary>
        public bool HasPassword;
    }

    /// <summary>联机屏 TapTap 段缝（S6b 双源改版；实现住 Tide.TapTap）：
    /// 登录态/房间列表/建房/加入/退房全经本口；异步口失败=返回 null（读 LastError 文案）——
    /// 不用元组返回（HybridCLR AOT 纪律）。Build 留空实现（S6a 起 v2 三栏无独立 TapTap 面板）。</summary>
    public interface ITapLobbySection
    {
        /// <summary>已登录 TapTap（LoginName 非空）——房间列表双源判定。</summary>
        bool IsLoggedIn { get; }

        /// <summary>TapTap 登录名（未登录为空）。</summary>
        string LoginName { get; }

        /// <summary>最近一次异步口失败/超时的可读文案（对应方法返回 null 时有效）。</summary>
        string LastError { get; }

        /// <summary>登录+连接对战服务（幂等，已连接直过）。返回 null=成功，否则=可读错误。</summary>
        UniTask<string> EnsureLoginAsync();

        /// <summary>公开房间列表（已滤满员与 ranked 房）。返回 null=失败（读 LastError）。</summary>
        UniTask<List<TapRoomEntry>> ListRoomsAsync();

        /// <summary>TapTap 建房：SDK 建房（密码写 customProperties）+ 本地组合传输会话服务器
        ///（同 password）已 Start(0)。成功返回该服务器（BeginHostSession/回环入座由 LobbyScreen
        /// 收尾）；失败返回 null（读 LastError）。</summary>
        UniTask<NetSessionServer> CreateRoomAsync(string roomName, string password);

        /// <summary>加入指定房间：SDK JoinRoom + TapClientTransport 就绪的客户端
        ///（进房协议 JoinRoom〔含密码/卡组名〕由 LobbyScreen 统一发送——密码真门禁=NetRoom）。
        /// 失败返回 null（读 LastError）。</summary>
        UniTask<NetGameClient> JoinRoomAsync(string roomId, string password);

        /// <summary>退房+断对战服务。返回 null=成功（吞错口径沿用——超时兜底在控制器）。</summary>
        UniTask<string> LeaveAsync();

        /// <summary>S6a 起无独立面板——留空实现（接口兼容保留）。</summary>
        void Build(RectTransform root, RectTransform entryButtonsRow);

        /// <summary>拆会话收口（退房+断对战服务；LobbyScreen.TeardownSession 调）。</summary>
        void Teardown();

        /// <summary>昵称兜底（TapTap 登录名；未登录返回空）。</summary>
        string FallbackNickname();

        /// <summary>本机 TapTap openId（2026-11-09 协议 v11 对局记录：进房自报，随终局摘要回传
        /// 对手；未登录/单机风味返回空）。</summary>
        string OpenIdOf();
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
