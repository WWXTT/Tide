using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCore.Network;
using MemoryPack;
using TapSDK.OnlineBattle;

namespace SynergyUI.Online
{
    /// <summary>
    /// 客人侧传输（INetClientTransport 的 TapTap 实现，2026-10-23）：JoinRoom 成功后构造，
    /// 全部上下行只对房主 playerId 收发（type=1 定向）。出站经限频泵（Pump 每帧排干），
    /// 入站由路由器在原生线程喂重组器（锁内），完整信封入队供逻辑线程消费。
    /// </summary>
    public sealed class TapClientTransport : INetClientTransport
    {
        private readonly string _hostPlayerId;
        private readonly string _selfPlayerId;
        private readonly TapMessageSequencer _sequencer = new TapMessageSequencer();
        private readonly TapMessageReassembler _reassembler = new TapMessageReassembler();
        private readonly ConcurrentQueue<NetworkMessage> _inbound = new ConcurrentQueue<NetworkMessage>();
        private readonly TapSendPump _sendPump;
        private readonly object _feedLock = new object();
        private volatile bool _disconnected;

        public string Remote => $"taptap:{_hostPlayerId}";
        public bool Disconnected => _disconnected || _sendPump.Failed;

        /// <param name="hostPlayerId">房主 playerId（RoomInfo.ownerId——上下行唯一对端）。</param>
        /// <param name="selfPlayerId">本机 playerId（房主掉线后接班判断用）。</param>
        public TapClientTransport(string hostPlayerId, string selfPlayerId)
        {
            _hostPlayerId = hostPlayerId;
            _selfPlayerId = selfPlayerId;
            _sendPump = new TapSendPump((msg, receiver) =>
                TapTapOnlineBattle.SendCustomMessage(msg, 1, new List<string> { receiver }));
            TapBattleRouter.SetClient(this);
        }

        /// <summary>出站：信封 → 分片 → 限频队列（仅逻辑线程调用）。</summary>
        public void Send(NetworkMessage message)
        {
            if (Disconnected) return;
            byte[] envelope;
            try { envelope = MemoryPackSerializer.Serialize(message); }
            catch (Exception) { _disconnected = true; return; }
            var chunks = TapMessageCodec.Chunk(_sequencer.Next(), envelope);
            foreach (var c in chunks)
                _sendPump.Enqueue(c, _hostPlayerId);
        }

        /// <summary>每帧排干限频出站（NetClientRuntime.Pump → NetGameClient.Pump → 此处）。</summary>
        public void Pump() => _sendPump.Pump();

        public bool TryReceive(out NetworkMessage message) => _inbound.TryDequeue(out message);

        public void Close()
        {
            _disconnected = true;
            _sendPump.Clear();
            TapBattleRouter.ClearClient(this);
            TapLobbyController.MarkRoomStale(); // 未走 LeaveRoom 的关闭路径——下次连接前退净
        }

        // ---- 路由器（原生线程）入口 ----

        /// <summary>入站喂片：只认房主 playerId；重组判死 → 断线。</summary>
        internal void NativeOnCustomMessage(string fromPlayerId, string msg)
        {
            if (fromPlayerId != _hostPlayerId) return; // 观战/异常路由：忽略
            lock (_feedLock)
            {
                byte[] envelope;
                bool alive;
                try { alive = _reassembler.Feed(msg, out envelope); }
                catch (Exception) { alive = false; envelope = null; }
                if (!alive) { _disconnected = true; return; }
                if (envelope == null) return;
                try { _inbound.Enqueue(MemoryPackSerializer.Deserialize<NetworkMessage>(envelope)); }
                catch (Exception) { _disconnected = true; }
            }
        }

        /// <summary>房主离开/掉线：官方机制会把房主转移给本机——权威服务器没了，判断线。</summary>
        internal void NativeOnRoomOwnerChanged(string newOwnerId, string changedPlayerId)
        {
            if (changedPlayerId == _hostPlayerId && newOwnerId != _hostPlayerId)
                _disconnected = true;
        }

        internal void NativeOnKicked(string playerId)
        {
            if (playerId == _selfPlayerId)
                _disconnected = true;
        }

        internal void NativeOnServiceDisconnected() => _disconnected = true;
    }
}
