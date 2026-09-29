#if !UNITY_WEBGL
using System;
using System.Net.Sockets;
using CardCore.Network;
using UnityEngine;

namespace CardCore.Network
{
    /// <summary>
    /// 运行时网络对局客户端（2026-09-24 阶段四）：连接会话服务器 → 进房 → 提交卡组 →
    /// 快照/事件流/反问下行 + intent 上行。从 Editor-only 的 NetClientHost.Client 移植
    /// （连接/FrameCodec 解帧/收发/下行分派同款），改为事件回调驱动——宿主每帧调 Pump()
    /// 在主线程分派下行（UI 线程安全）。连接配置（ip/port/昵称/卡组选择）属匹配界面职责，
    /// 本组件只收就绪参数；WebGL 下 TcpClient 不可用，整类条件编译隔离（不堵死）。
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
        public event Action<MsgError> OnError;
        public event Action OnDisconnected;

        public bool Disconnected { get; private set; }
        public string Remote { get; private set; } = "";

        private readonly TcpClient _tcp = new TcpClient();
        private NetworkStream _stream;
        private readonly FrameCodec.FrameDecoder _decoder = new FrameCodec.FrameDecoder();
        private readonly byte[] _buf = new byte[16 * 1024];

        public void Connect(string host, int port)
        {
            Remote = $"{host}:{port}";
            _tcp.Connect(host, port);
            _stream = _tcp.GetStream();
        }

        public void Send<T>(NetworkMessageType type, T payload) where T : class
        {
            if (Disconnected || _stream == null) return;
            try
            {
                var frame = FrameCodec.Frame(NetworkSerializer.BuildEnvelope(type, payload));
                _stream.Write(frame, 0, frame.Length);
                _stream.Flush();
            }
            catch (Exception) { MarkDisconnected(); }
        }

        /// <summary>非阻塞收一轮（DataAvailable 轮询，无 Sleep）+ 主线程分派下行。宿主每帧调用。
        /// 半途帧由解码器缓冲跨 Pump 续接，不丢数据。</summary>
        public void Pump()
        {
            if (Disconnected || _stream == null) return;
            while (_stream.DataAvailable)
            {
                int n;
                try { n = _stream.Read(_buf, 0, _buf.Length); }
                catch (Exception) { MarkDisconnected(); return; }
                if (n <= 0) { MarkDisconnected(); return; }
                _decoder.Append(_buf, 0, n);
                while (_decoder.TryDecode(out var msg))
                    Dispatch(msg);
            }
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

                case NetworkMessageType.Error:
                    var err = NetworkSerializer.DeserializePayload<MsgError>(msg);
                    if (err != null)
                    {
                        Debug.Log($"[NetGameClient] 拒绝（{err.Context}）：{err.Reason}");
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
            try { _stream?.Close(); } catch { }
            try { _tcp.Close(); } catch { }
        }
    }
}
#endif
