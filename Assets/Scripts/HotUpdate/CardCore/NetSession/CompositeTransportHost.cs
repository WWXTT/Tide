using System;
using System.Collections.Generic;

namespace CardCore.Network
{
    /// <summary>
    /// 组合传输宿主（2026-10-23 TapTap 联机接入）：把两条 INetTransportHost 聚合成一条——
    /// 房主侧 = NetTcpHost（127.0.0.1 回环：房主自身客户端 + AI 填位，现有路径零改动）
    /// + TapBattleTransportHost（远端客人经 TapTap 房间中转）。Poll/Connections 汇聚，
    /// 断开剔除统一上交 NetSessionServer.Pump 的既有收口。
    ///
    /// Port 语义取第一腿（TCP）——TapTap 腿无端口概念（Start 的 port 参数被其忽略）；
    /// 建议房主侧以 port=0 起听（OS 分配，回环专用，不再对外展示 IP:端口）。
    /// </summary>
    public sealed class CompositeTransportHost : INetTransportHost
    {
        private readonly INetTransportHost _primary;
        private readonly INetTransportHost _secondary;
        private readonly List<INetChannel> _merged = new List<INetChannel>();

        public CompositeTransportHost(INetTransportHost primary, INetTransportHost secondary)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
        }

        /// <summary>主腿（TCP）实际绑定端口——回环客户端连它。</summary>
        public int Port => _primary.Port;

        public void Start(int port)
        {
            _primary.Start(port);
            _secondary.Start(0); // TapTap 腿无端口语义
        }

        public void Stop()
        {
            try { _primary.Stop(); } catch (Exception) { }
            try { _secondary.Stop(); } catch (Exception) { }
        }

        public List<INetChannel> Poll()
        {
            var dropped = _primary.Poll();
            dropped.AddRange(_secondary.Poll());
            RebuildSnapshot();
            return dropped;
        }

        public IReadOnlyList<INetChannel> Connections => _merged;

        private void RebuildSnapshot()
        {
            _merged.Clear();
            _merged.AddRange(_primary.Connections);
            _merged.AddRange(_secondary.Connections);
        }
    }
}
