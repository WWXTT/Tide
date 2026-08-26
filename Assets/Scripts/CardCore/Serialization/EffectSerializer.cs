using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace CardCore.Serialization
{
    public static class EffectSerializer
    {
        public static long ComputeEffectTag(string description)
        {
            if (string.IsNullOrEmpty(description))
                return 0;
            return MurmurHash3.EffectTag(description);
        }

        public static byte[] SerializeCardEffects(List<CardEffectData> effects)
        {
            var dtos = effects.Select(SerializableCardEffectData.FromCardEffectData).ToArray();
            return MemoryPackSerializer.Serialize(dtos);
        }

        public static List<CardEffectData> DeserializeCardEffects(byte[] data)
        {
            var dtos = MemoryPackSerializer.Deserialize<SerializableCardEffectData[]>(data);
            return dtos.Select(d => d.ToCardEffectData()).ToList();
        }

        public static byte[] SerializeEffectDefinitions(List<EffectDefinition> definitions)
        {
            var dtos = definitions.Select(SerializableEffectDefinition.FromDefinition).ToArray();
            return MemoryPackSerializer.Serialize(dtos);
        }

        public static List<EffectDefinition> DeserializeEffectDefinitions(byte[] data)
        {
            var dtos = MemoryPackSerializer.Deserialize<SerializableEffectDefinition[]>(data);
            return dtos.Select(d => d.ToDefinition()).ToList();
        }
    }
}
