using CloudSaveMenu = CloudSave.CloudSaveMenuController;

namespace SynergyUI.Online
{
    /// <summary>
    /// TapTap 模块（Tide.TapTap 程序集对 TapBridge 挂载缝的实现，2026-10-24 预编译隔离）：
    /// 主菜单挂件（登录徽章/云存档入口）与联机屏 TapTap 段的总分发。
    /// 由 HotUpdateEntry.Start 经 TapBridge.InstallFromLoadedAssemblies 反射实例化——
    /// 本类型须保持无参构造 + 完整类型名 SynergyUI.Online.TapModule（TapBridge 按名扫描）。
    /// 本地单机风味整个程序集不编译不打包，本类缺席即一切 TapTap 功能缺席。
    /// </summary>
    public sealed class TapModule : Tide.HotUpdate.ITapModule
    {
        private readonly CloudSaveMenu _cloudMenu = new CloudSaveMenu();

        /// <summary>启动装配：云存档链路（SDK Init→静默登录→启动同步旁路；编辑器/PC 空转只留日志）。</summary>
        public void InitOnStartup() => CloudSave.TapCloudSaveBootstrap.Init();

        /// <summary>主菜单挂件（2026-11-22 登录迁移定案：登录/云存档/退出登录全在设置面板账号区）：
        /// TapLoginBadge 注入 SettingsLoginSlot + 云存档入口注入 SettingsCloudSlot。</summary>
        public void OnMainMenuBuilt(MainMenuScreen menu)
        {
            TapLoginBadge.AttachToSettings(menu);
            _cloudMenu.Build(menu);
        }

        public void OnMainMenuEnter(MainMenuScreen menu)
        {
            TapAccountService.Warmup();            // 静默续登（有缓存账号无感登录；不支持平台 no-op）
            _cloudMenu.OnEnter(menu);
        }

        public void OnMainMenuExit(MainMenuScreen menu) => _cloudMenu.OnExit(menu);

        public Tide.HotUpdate.ITapLobbySection CreateLobbySection(LobbyScreen screen)
            => new TapLobbySection(screen);
    }
}
