namespace CardCore.Network
{
    /// <summary>
    /// 联机总闸（2026-10-01 GameJam「涌现」参赛期关闭；2026-10-21 P2P 定案复活）。
    /// 现行拓扑（P2P，2026-10-21）：房主进程内宿主 NetSessionServer，客人直连——
    /// 专用服务器（TideServer）退役出生产拓扑（verify 套件保留为门禁）。
    /// 开启（true）时：
    /// - UIBootstrap 帧驱闸生效：会话服务器运行期停本地帧驱，由服务器泵接管引擎；
    /// - Editor NetLobbyHost 不随 Play 自启（自启已摘除）——P2P 房间由运行时 UI 显式创建。
    /// 关回 false 可整体窒息联机路径（紧急开关）。
    /// </summary>
    public static class NetGate
    {
        /// <summary>联机是否启用。</summary>
        public static bool OnlineEnabled => true;
    }
}
