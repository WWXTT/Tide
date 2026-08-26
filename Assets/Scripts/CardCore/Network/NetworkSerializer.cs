using System;
using MemoryPack;

namespace CardCore.Network
{
    public static class NetworkSerializer
    {
        private static uint _sequenceCounter;

        public static byte[] SerializeMessage<T>(NetworkMessageType type, T payload) where T : class
        {
            var message = new NetworkMessage
            {
                Type = type,
                SequenceId = ++_sequenceCounter,
                Payload = MemoryPackSerializer.Serialize(payload),
                Timestamp = DateTime.UtcNow.Ticks,
            };
            return MemoryPackSerializer.Serialize(message);
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
