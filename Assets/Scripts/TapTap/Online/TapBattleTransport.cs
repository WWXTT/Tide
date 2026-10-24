using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCore;
using CardCore.Network;
using MemoryPack;
using TapSDK.OnlineBattle;

namespace SynergyUI.Online
{
    /// <summary>
    /// 全进程唯一的 TapTap 回调路由（2026-10-23）：SDK 回调在**原生线程**触发
    ///（P/Invoke MonoPInvokeCallback），这里只做转发——角色对象（房主传输/客人传输）
    /// 收到后自行入队，生命周期与收发全部在逻辑线程 Poll 发现（NetTransports 线程纪律）。
    /// 房主进程只注册 Host；客人进程只注册 Client——房主自己的客户端走 TCP 回环不经 TapTap。
    /// </summary>
    internal sealed class TapBattleRouter : ITapOnlineBattleCallback
    {
        private static TapBattleRouter _instance;
        private static readonly object RegisterLock = new object();

        private volatile TapBattleTransportHost _host;
        private volatile TapClientTransport _client;

        public static void EnsureRegistered()
        {
            lock (RegisterLock)
            {
                if (_instance == null)
                {
                    _instance = new TapBattleRouter();
                    TapTapOnlineBattle.RegisterOnlineBattleCallback(_instance);
                }
            }
        }

        public static void SetHost(TapBattleTransportHost host) { EnsureRegistered(); _instance._host = host; }
        public static void ClearHost(TapBattleTransportHost host)
        {
            if (_instance != null && ReferenceEquals(_instance._host, host)) _instance._host = null;
        }

        public static void SetClient(TapClientTransport client) { EnsureRegistered(); _instance._client = client; }
        public static void ClearClient(TapClientTransport client)
        {
            if (_instance != null && ReferenceEquals(_instance._client, client)) _instance._client = null;
        }

        // ---- ITapOnlineBattleCallback：原生线程 → 只转发/入队，不碰会话状态 ----

        public void OnCustomMessageReceived(string playerId, string msg)
        {
            _host?.NativeOnCustomMessage(playerId, msg);
            _client?.NativeOnCustomMessage(playerId, msg);
        }

        public void OnPlayerEntered(string roomId, PlayerInfo playerInfo)
        {
            if (playerInfo != null && !string.IsNullOrEmpty(playerInfo.id))
                _host?.NativeOnPlayerEntered(playerInfo.id);
        }

        public void OnPlayerLeft(string roomId, string roomOwnerId, string playerId)
        {
            _host?.NativeOnPlayerLeft(playerId);
            _client?.NativeOnRoomOwnerChanged(roomOwnerId, playerId);
        }

        public void OnPlayerOffline(string roomId, string roomOwnerId, string playerId)
        {
            _host?.NativeOnPlayerLeft(playerId);
            _client?.NativeOnRoomOwnerChanged(roomOwnerId, playerId);
        }

        public void OnPlayerKicked(string roomId, string playerId)
        {
            _host?.NativeOnPlayerLeft(playerId);
            _client?.NativeOnKicked(playerId);
        }

        public void OnDisconnected(int code, string msg)
        {
            _host?.NativeOnServiceDisconnected();
            _client?.NativeOnServiceDisconnected();
        }

        public void OnBattleServiceError()
        {
            _host?.NativeOnServiceDisconnected();
            _client?.NativeOnServiceDisconnected();
        }

        // ---- 联机路径用不到的（帧同步/属性），空实现满足接口 ----
        public void OnRoomPropertiesChanged(string id, string name, string customProperties) { }
        public void OnPlayerCustomPropertiesChanged(string playerId, string properties) { }
        public void OnPlayerCustomStatusChanged(string playerId, int status) { }
        public void OnFrameSyncStarted(FrameSyncInfo frameSyncInfo) { }
        public void OnFrameReceived(FrameData frameData) { }
        public void OnFrameSyncStopped(string roomId, int frameSyncId, int reason) { }
    }

    /// <summary>
    /// 房主侧：一个远端玩家 = 一条 channel（INetChannel 的 TapTap 实现）。
    /// 出站 = 定向消息（SendCustomMessage type=1 指定该玩家——**绝不广播**：
    /// 快照/事件按座位过滤过，广播会泄露隐藏手牌）。经共享 TapSendPump 限频（12/秒）。
    /// </summary>
    public sealed class TapBattleChannel : INetChannel
    {
        private readonly TapBattleTransportHost _owner;
        private readonly TapMessageSequencer _sequencer = new TapMessageSequencer();
        private readonly TapMessageReassembler _reassembler = new TapMessageReassembler();
        private readonly ConcurrentQueue<NetworkMessage> _inbound = new ConcurrentQueue<NetworkMessage>();
        private readonly object _feedLock = new object();
        private volatile bool _disconnected;

        /// <summary>TapTap 侧玩家 ID（消息定向接收者；调试展示用）。</summary>
        public string PlayerId { get; }

        public bool IsDisconnected => _disconnected || _owner.SendFailed;

        // ---- 房间侧附加状态（逻辑线程独占；经 INetChannel 暴露给会话层）----
        public int ChairSeat { get; set; } = -2;
        public int MatchSeat { get; set; } = -1;
        public string Nickname { get; set; } = "";
        public int EventCursor { get; set; }
        public bool SnapshotDirty { get; set; }

        internal TapBattleChannel(TapBattleTransportHost owner, string playerId)
        {
            _owner = owner;
            PlayerId = playerId;
        }

        /// <summary>出站：信封 → 分片 → 共享限频队列（仅逻辑线程调用）。</summary>
        public void Send(NetworkMessage message)
        {
            if (IsDisconnected) return;
            byte[] envelope;
            try { envelope = MemoryPackSerializer.Serialize(message); }
            catch (Exception) { _disconnected = true; return; }
            var chunks = TapMessageCodec.Chunk(_sequencer.Next(), envelope);
            foreach (var c in chunks)
                _owner.EnqueueOutbound(c, PlayerId);
        }

        public void SendPayload<T>(NetworkMessageType type, T payload) where T : class
            => Send(NetworkSerializer.BuildEnvelope(type, payload));

        public bool TryReceive(out NetworkMessage message) => _inbound.TryDequeue(out message);

        /// <summary>入站喂片（路由器→原生线程调用）：重组 → 完整信封反序列化入队。</summary>
        internal void FeedNative(string wire)
        {
            lock (_feedLock)
            {
                byte[] envelope;
                bool alive;
                try { alive = _reassembler.Feed(wire, out envelope); }
                catch (Exception) { alive = false; envelope = null; }
                if (!alive) { _disconnected = true; return; }
                if (envelope == null) return;
                try { _inbound.Enqueue(MemoryPackSerializer.Deserialize<NetworkMessage>(envelope)); }
                catch (Exception) { _disconnected = true; }
            }
        }

        internal void MarkDisconnected() => _disconnected = true;

        public void Close()
        {
            _disconnected = true;
            _owner.OnChannelClosed(this);
        }
    }

    /// <summary>
    /// 房主侧传输宿主（INetTransportHost 的 TapTap 实现）：绑定当前 TapTap 房间——
    /// 玩家进房事件 → 新 channel；离房/掉线/被踢 → 断线剔除；Poll 里顺带排干共享出站泵。
    /// Port 无语义（恒 0——回环走组合宿主的 TCP 腿）。
    /// </summary>
    public sealed class TapBattleTransportHost : INetTransportHost
    {
        private readonly ConcurrentQueue<string> _playerJoined = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _playerLeft = new ConcurrentQueue<string>();
        private readonly ConcurrentDictionary<string, TapBattleChannel> _channels =
            new ConcurrentDictionary<string, TapBattleChannel>();
        private readonly List<INetChannel> _snapshot = new List<INetChannel>();
        private readonly TapSendPump _sendPump;
        private volatile bool _stopped;

        public TapBattleTransportHost()
        {
            _sendPump = new TapSendPump((msg, receiver) =>
                TapTapOnlineBattle.SendCustomMessage(msg, 1, new List<string> { receiver }));
        }

        internal bool SendFailed => _sendPump.Failed;

        public int Port => 0;

        /// <summary>绑回调路由（port 参数无语义——组合宿主会传 0）。</summary>
        public void Start(int port) => TapBattleRouter.SetHost(this);

        public void Stop()
        {
            _stopped = true;
            _sendPump.Clear();
            TapBattleRouter.ClearHost(this);
            foreach (var ch in _channels.Values)
                ch.MarkDisconnected();
            _channels.Clear();
            _snapshot.Clear();
            TapLobbyController.MarkRoomStale(); // 未走 LeaveRoom 的关闭路径——下次连接前退净
        }

        public List<INetChannel> Poll()
        {
            if (_stopped) return new List<INetChannel>();

            // 吸收生命周期事件（逻辑线程发现——IO 侧只入队）
            while (_playerJoined.TryDequeue(out var joinedId))
            {
                if (_channels.TryGetValue(joinedId, out var existing)) continue; // 重复事件
                var channel = new TapBattleChannel(this, joinedId);
                _channels[joinedId] = channel;
                TideLog.Info($"[TapBattle] 远端玩家入传输：{joinedId}");
            }

            var dropped = new List<INetChannel>();
            while (_playerLeft.TryDequeue(out var leftId))
            {
                if (_channels.TryRemove(leftId, out var ch))
                {
                    ch.MarkDisconnected();
                    dropped.Add(ch);
                }
            }

            // 断线剔除（重组判死/发送链路故障——FlushDownlink 下一轮自然不剔除后再发）
            foreach (var kv in _channels)
            {
                if (!kv.Value.IsDisconnected) continue;
                if (_channels.TryRemove(kv.Key, out var dead))
                {
                    dead.MarkDisconnected();
                    dropped.Add(dead);
                }
            }

            _sendPump.Pump(); // 排干限频出站

            _snapshot.Clear();
            _snapshot.AddRange(_channels.Values);
            return dropped;
        }

        public IReadOnlyList<INetChannel> Connections => _snapshot;

        // ---- 路由器（原生线程）入口：只入队 ----
        internal void NativeOnPlayerEntered(string playerId) => _playerJoined.Enqueue(playerId);
        internal void NativeOnPlayerLeft(string playerId) => _playerLeft.Enqueue(playerId);
        internal void NativeOnServiceDisconnected()
        {
            foreach (var ch in _channels.Values) ch.MarkDisconnected();
        }
        internal void NativeOnCustomMessage(string playerId, string msg)
        {
            if (_channels.TryGetValue(playerId, out var ch))
                ch.FeedNative(msg);
        }

        internal void EnqueueOutbound(string chunk, string receiverId) => _sendPump.Enqueue(chunk, receiverId);

        internal void OnChannelClosed(TapBattleChannel channel)
        {
            _channels.TryRemove(channel.PlayerId, out _);
        }
    }
}
