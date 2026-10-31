#if !UNITY_WEBGL
using System;
using CardCore.Network;

namespace CardCore.Network
{
    /// <summary>
    /// 运行时网络对局客户端（2026-09-24 阶段四）：连接会话服务器 → 进房 → 提交卡组 →
    /// 快照/事件流/反问下行 + intent 上行。从 Editor-only 的 NetClientHost.Client 移植
    /// （连接/FrameCodec 解帧/收发/下行分派同款），改为事件回调驱动——宿主每帧调 Pump()
    /// 在主线程分派下行（UI 线程安全）。连接配置（ip/port/昵称/卡组选择）属匹配界面职责，
    /// 本组件只收就绪参数；WebGL 下 TcpClient 不可用，整类条件编译隔离（不堵死）。
    ///
    /// 2026-10-23 抽缝：IO 收发拆入 INetClientTransport（TCP 直连/TapTap 房间中转两实现），
    /// 会话逻辑（JoinRoom/握手/下行分派）不动。
    /// </summary>
    public sealed class NetGameClient
    {
        // ---- 下行回调（Pump 调用线程=主线程触发） ----
        public event Action<MsgLobbyState> OnLobbyState; // 大厅层（阶段三匹配界面，2026-09-24）
        public event Action<MsgRoomState> OnRoomState;
        public event Action<MsgMatchManifest> OnManifest;
        public event Action<MsgGameStateSync> OnSnapshot;
        public event Action<NetEvent[]> OnEventBatch;
        public event Action<MsgSelectRequest> OnSelectRequest;
        public event Action<MsgTextRequest> OnTextRequest; // 文本反问（2026-10-31 阶段C：卡名宣言输入窗）
        public event Action<MsgError> OnError;
        public event Action OnDisconnected;

        public bool Disconnected { get; private set; }
        public string Remote => _io != null ? _io.Remote : "";

        private INetClientTransport _io;

        /// <summary>TCP 直连（阻塞，失败抛——调用方在线程池上跑）。局域网/回环路径。</summary>
        public void Connect(string host, int port)
        {
            Connect(new TcpClientTransport(host, port));
        }

        /// <summary>注入传输（TapTap 房间中转等——构造时已就绪的传输实例）。</summary>
        public void Connect(INetClientTransport transport)
        {
            _io = transport;
        }

        public void Send<T>(NetworkMessageType type, T payload) where T : class
        {
            if (Disconnected || _io == null) return;
            _io.Send(NetworkSerializer.BuildEnvelope(type, payload));
        }

        /// <summary>非阻塞收一轮 + 主线程分派下行。宿主每帧调用。</summary>
        public void Pump()
        {
            if (Disconnected || _io == null) return;
            _io.Pump();
            if (_io.Disconnected) { MarkDisconnected(); return; }
            while (_io.TryReceive(out var msg))
                Dispatch(msg);
        }

        private void Dispatch(NetworkMessage msg)
        {
            switch (msg.Type)
            {
                case NetworkMessageType.LobbyState:
                    OnLobbyState?.Invoke(NetworkSerializer.DeserializePayload<MsgLobbyState>(msg));
                    break;

                case NetworkMessageType.RoomState:
                    var room = NetworkSerializer.DeserializePayload<MsgRoomState>(msg);
                    OnRoomState?.Invoke(room);
                    break;

                case NetworkMessageType.MatchManifest:
                    OnManifest?.Invoke(NetworkSerializer.DeserializePayload<MsgMatchManifest>(msg));
                    break;

                case NetworkMessageType.GameStateSyncV2:
                    var snap = NetworkSerializer.DeserializePayload<MsgGameStateSync>(msg);
                    if (snap != null) OnSnapshot?.Invoke(snap);
                    break;

                case NetworkMessageType.NetEventBatch:
                    var batch = NetworkSerializer.DeserializePayload<MsgNetEventBatch>(msg);
                    if (batch?.Events != null && batch.Events.Length > 0)
                        OnEventBatch?.Invoke(batch.Events);
                    break;

                case NetworkMessageType.SelectRequest:
                    OnSelectRequest?.Invoke(NetworkSerializer.DeserializePayload<MsgSelectRequest>(msg));
                    break;

                case NetworkMessageType.TextRequest:
                    OnTextRequest?.Invoke(NetworkSerializer.DeserializePayload<MsgTextRequest>(msg));
                    break;

                case NetworkMessageType.Error:
                    var err = NetworkSerializer.DeserializePayload<MsgError>(msg);
                    if (err != null)
                    {
                        TideLog.Info($"[NetGameClient] 拒绝（{err.Context}）：{err.Reason}");
                        OnError?.Invoke(err);
                    }
                    break;
            }
        }

        private void MarkDisconnected()
        {
            if (Disconnected) return;
            Disconnected = true;
            OnDisconnected?.Invoke();
        }

        public void Close()
        {
            Disconnected = true;
            _io?.Close();
        }
    }
}
#endif
