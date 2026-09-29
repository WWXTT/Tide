using System;
using MemoryPack;

namespace CardCore.Network
{
    public static class NetworkSerializer
    {
        private static uint _sequenceCounter;

        /// <summary>构造信封对象（不终序列化）——M2 会话层出站便捷口与 SerializeMessage 共用。</summary>
        public static NetworkMessage BuildEnvelope<T>(NetworkMessageType type, T payload) where T : class
        {
            return new NetworkMessage
            {
                Type = type,
                SequenceId = ++_sequenceCounter,
                Payload = payload == null ? Array.Empty<byte>() : MemoryPackSerializer.Serialize(payload),
                Timestamp = DateTime.UtcNow.Ticks,
                ProtocolVersion = NetworkProtocol.Version,
            };
        }

        public static byte[] SerializeMessage<T>(NetworkMessageType type, T payload) where T : class
        {
            return MemoryPackSerializer.Serialize(BuildEnvelope(type, payload));
        }

        public static NetworkMessage DeserializeEnvelope(byte[] data)
        {
            return MemoryPackSerializer.Deserialize<NetworkMessage>(data);
        }

        public static T DeserializePayload<T>(NetworkMessage message) where T : class
        {
            if (message.Payload == null || message.Payload.Length == 0)
                return null;
            return MemoryPackSerializer.Deserialize<T>(message.Payload);
        }
    }
}
