using System.Collections.Generic;
using MemoryPack;

namespace CardCore.Serialization
{
    public static class CardDataSerializer
    {
        public static byte[] SerializeCardData(CardData data)
        {
            var dto = SerializableCardData.FromCardData(data);
            return MemoryPackSerializer.Serialize(dto);
        }

        public static CardData DeserializeCardData(byte[] data)
        {
            var dto = MemoryPackSerializer.Deserialize<SerializableCardData>(data);
            return dto.ToCardData();
        }

        public static byte[] SerializeCardDataList(List<CardData> cards)
        {
            var dtos = new SerializableCardData[cards.Count];
            for (int i = 0; i < cards.Count; i++)
                dtos[i] = SerializableCardData.FromCardData(cards[i]);
            return MemoryPackSerializer.Serialize(dtos);
        }

        public static List<CardData> DeserializeCardDataList(byte[] data)
        {
            var dtos = MemoryPackSerializer.Deserialize<SerializableCardData[]>(data);
            var result = new List<CardData>(dtos.Length);
            foreach (var dto in dtos)
                result.Add(dto.ToCardData());
            return result;
        }

        public static byte[] SerializeCardState(Card card)
        {
            var dto = SerializableRuntimeCardState.FromCard(card);
            return MemoryPackSerializer.Serialize(dto);
        }

        public static SerializableRuntimeCardState DeserializeCardState(byte[] data)
        {
            return MemoryPackSerializer.Deserialize<SerializableRuntimeCardState>(data);
        }
    }
}
