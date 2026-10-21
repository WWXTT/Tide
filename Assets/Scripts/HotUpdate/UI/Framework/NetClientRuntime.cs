using System;
using CardCore.Network;

namespace SynergyUI
{
    /// <summary>
    /// 联机客户端运行时枢纽（2026-10-21 P2P 复活）：持有当前 NetGameClient，
    /// 由 UIBootstrap.Update 每帧 Pump——屏幕是纯 C# 类无 Update，客户端收发统一在此驱动
    ///（下行回调经 Pump 在主线程分派，UI 线程安全）。大厅屏与战斗屏共用同一条连接，
    /// 移交经 BattleEntry.Client 一次性交接（防重新连接撞座位锁）。
    /// 房主侧：同进程另有 NetSessionServer 泵（UIBootstrap 先泵服务器再泵本客户端）。
    /// </summary>
    public static class NetClientRuntime
    {
        private static NetGameClient _client;

        /// <summary>当前活跃客户端（null=无联机会话）。绑定即接管 Pump。</summary>
        public static NetGameClient Active => _client;

        /// <summary>绑定客户端（顶替旧连接：先关旧的再接管 Pump）。</summary>
        public static void Bind(NetGameClient client)
        {
            if (_client != null && !ReferenceEquals(_client, client))
                _client.Close();
            _client = client;
        }

        /// <summary>解绑（仅当仍是同一实例；战斗屏接管后解绑不动新持有者）。</summary>
        public static void Unbind(NetGameClient client)
        {
            if (!ReferenceEquals(_client, client)) return;
            _client = null;
        }

        /// <summary>每帧泵（UIBootstrap.Update 调用；异常不倒 UI 帧——断线经 OnDisconnected 收口）。</summary>
        public static void Pump()
        {
            var client = _client;
            if (client == null) return;
            try { client.Pump(); }
            catch (Exception) { /* Pump 内部读写异常已按断线标记；外层防御不倒帧 */ }
        }
    }
}
