namespace CardCore.Network
{
    /// <summary>
    /// 联机总闸（2026-10-01 聚光灯GameJam「涌现」参赛定案：比赛禁联机）。
    /// 关闭（false）时：
    /// - 主菜单不出现「联机大厅」「网络直连」入口；
    /// - BattleScreen 网络模式防御性回落本地 AI；
    /// - Editor 大厅宿主（NetLobbyHost）不再随 Play 自启，batchmode 宿主入口拒绝；
    /// - UIBootstrap 帧驱闸短路，不查询任何服务器托管态。
    /// 协议栈本体（NetSession/Network）与编辑器验证器保持编译可测——
    /// 赛后恢复联机只需把本属性改回 true。
    /// </summary>
    public static class NetGate
    {
        /// <summary>联机是否启用。GameJam 参赛期必须为 false。</summary>
        public static bool OnlineEnabled => false;
    }
}
