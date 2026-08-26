using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace CardCore.Serialization
{
    [MemoryPackable]
    public partial class SerializableCardData
    {
        [MemoryPackOrder(TagTable.CardData_ID)]
        public string ID;

        [MemoryPackOrder(TagTable.CardData_Supertype)]
        public int Supertype;

        [MemoryPackOrder(TagTable.CardData_CardName)]
        public string CardName;

        [MemoryPackOrder(TagTable.CardData_Illustration)]
        public string Illustration;

        [MemoryPackOrder(TagTable.CardData_Life)]
        public int? Life;

        [MemoryPackOrder(TagTable.CardData_Power)]
        public int? Power;

        [MemoryPackOrder(TagTable.CardData_Cost)]
        public CostEntryDTO[] Cost;

        [MemoryPackOrder(TagTable.CardData_Effects)]
        public SerializableCardEffectData[] Effects;

        [MemoryPackOrder(TagTable.CardData_CreationTicks)]
        public long CreationTicks;

        [MemoryPackOrder(TagTable.CardData_Tags)]
        public string[] Tags;

        [MemoryPackOrder(TagTable.CardData_Keywords)]
        public string[] Keywords;

        public static SerializableCardData FromCardData(CardData data)
        {
            var dto = new SerializableCardData
            {
                ID = data.ID,
                Supertype = (int)data.Supertype,
                CardName = data.CardName,
                Illustration = data.Illustration,
                Life = data.Life,
                Power = data.Power,
                CreationTicks = data.CreationTime.Ticks,
            };

            dto.Cost = data.Cost?.Select(kvp => new CostEntryDTO { ManaType = kvp.Key, Value = kvp.Value }).ToArray()
                       ?? Array.Empty<CostEntryDTO>();

            dto.Effects = data.Effects?.Select(SerializableCardEffectData.FromCardEffectData).ToArray()
                          ?? Array.Empty<SerializableCardEffectData>();

            dto.Tags = data.Tags?.ToArray() ?? Array.Empty<string>();
            dto.Keywords = data.Keywords?.ToArray() ?? Array.Empty<string>();

            return dto;
        }

        public CardData ToCardData()
        {
            var data = new CardData
            {
                ID = ID,
                Supertype = (Cardtype)Supertype,
                CardName = CardName,
                Illustration = Illustration,
                Life = Life,
                Power = Power,
                CreationTime = new DateTime(CreationTicks),
            };

            if (Cost != null)
            {
                data.Cost = new Dictionary<int, float>();
                foreach (var entry in Cost)
                    data.Cost[entry.ManaType] = entry.Value;
            }

            if (Effects != null)
            {
                data.Effects = Effects.Select(e => e.ToCardEffectData()).ToList();
            }

            if (Tags != null)
                data.Tags = Tags.ToList();
            if (Keywords != null)
                data.Keywords = Keywords.ToList();

            return data;
        }
    }

    [MemoryPackable]
    public partial class CostEntryDTO
    {
        [MemoryPackOrder(TagTable.CostEntryDTO_ManaType)]
        public int ManaType;

        [MemoryPackOrder(TagTable.CostEntryDTO_Value)]
        public float Value;
    }

    [MemoryPackable]
    public partial class SerializableCardEffectData
    {
        [MemoryPackOrder(TagTable.EffectData_Abbreviation)]
        public string Id;

        [MemoryPackOrder(TagTable.EffectData_Description)]
        public string Description;

        [MemoryPackOrder(TagTable.EffectData_Initiative)]
        public int TriggerTiming;

        [MemoryPackOrder(TagTable.EffectData_Speed)]
        public int ActivationType;

        [MemoryPackOrder(TagTable.EffectData_Parameters)]
        public int BaseSpeed;

        [MemoryPackOrder(TagTable.EffectData_ManaType)]
        public SerializableAtomicEffectEntry[] AtomicEffects;

        [MemoryPackOrder(TagTable.EffectData_DisplayName)]
        public string DisplayName;

        [MemoryPackOrder(TagTable.EffectData_IsOptional)]
        public bool IsOptional;

        [MemoryPackOrder(TagTable.EffectData_Duration)]
        public int Duration;

        [MemoryPackOrder(TagTable.EffectData_Tags)]
        public string[] Tags;

        [MemoryPackOrder(TagTable.EffectData_Costs)]
        public SerializableEffectCostEntry[] Costs;

        [MemoryPackOrder(TagTable.EffectData_ActivationConditions)]
        public SerializableActivationConditionData[] ActivationConditions;

        [MemoryPackOrder(TagTable.EffectData_TriggerConditions)]
        public SerializableActivationConditionData[] TriggerConditions;

        [MemoryPackOrder(TagTable.EffectData_Steps)]
        public SerializableEffectStepData[] Steps;

        public static SerializableCardEffectData FromCardEffectData(CardEffectData data)
        {
            var dto = new SerializableCardEffectData
            {
                Id = data.Id,
                DisplayName = data.DisplayName,
                Description = data.Description,
                TriggerTiming = data.TriggerTiming,
                ActivationType = data.ActivationType,
                BaseSpeed = data.BaseSpeed,
                IsOptional = data.IsOptional,
                Duration = data.Duration,
            };
            dto.AtomicEffects = data.AtomicEffects?.Select(SerializableAtomicEffectEntry.FromEntry).ToArray()
                                ?? Array.Empty<SerializableAtomicEffectEntry>();
            dto.Tags = data.Tags?.ToArray() ?? Array.Empty<string>();
            dto.Costs = data.Costs?.Select(SerializableEffectCostEntry.FromEntry).ToArray()
                        ?? Array.Empty<SerializableEffectCostEntry>();
            dto.ActivationConditions = data.ActivationConditions?.Select(SerializableActivationConditionData.FromData).ToArray()
                                       ?? Array.Empty<SerializableActivationConditionData>();
            dto.TriggerConditions = data.TriggerConditions?.Select(SerializableActivationConditionData.FromData).ToArray()
                                    ?? Array.Empty<SerializableActivationConditionData>();
            dto.Steps = data.Steps?.Select(SerializableEffectStepData.FromData).ToArray()
                        ?? Array.Empty<SerializableEffectStepData>();
            return dto;
        }

        public CardEffectData ToCardEffectData()
        {
            return new CardEffectData
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                TriggerTiming = TriggerTiming,
                ActivationType = ActivationType,
                BaseSpeed = BaseSpeed,
                IsOptional = IsOptional,
                Duration = Duration,
                AtomicEffects = AtomicEffects?.Select(e => e.ToEntry()).ToList() ?? new List<AtomicEffectEntry>(),
                Tags = Tags?.ToList() ?? new List<string>(),
                Costs = Costs?.Select(e => e.ToEntry()).ToList() ?? new List<CostEntry>(),
                ActivationConditions = ActivationConditions?.Select(e => e.ToData()).ToList() ?? new List<ActivationConditionData>(),
                TriggerConditions = TriggerConditions?.Select(e => e.ToData()).ToList() ?? new List<ActivationConditionData>(),
                Steps = Steps?.Select(e => e.ToData()).ToList() ?? new List<EffectStepData>(),
            };
        }
    }

    [MemoryPackable]
    public partial class SerializableActivationConditionData
    {
        [MemoryPackOrder(TagTable.ActivationConditionData_Type)]
        public int Type;

        [MemoryPackOrder(TagTable.ActivationConditionData_Value)]
        public int Value;

        [MemoryPackOrder(TagTable.ActivationConditionData_Value2)]
        public int Value2;

        [MemoryPackOrder(TagTable.ActivationConditionData_StringValue)]
        public string StringValue;

        [MemoryPackOrder(TagTable.ActivationConditionData_Negate)]
        public bool Negate;

        public static SerializableActivationConditionData FromData(ActivationConditionData data)
        {
            return new SerializableActivationConditionData
            {
                Type = data.Type,
                Value = data.Value,
                Value2 = data.Value2,
                StringValue = data.StringValue,
                Negate = data.Negate,
            };
        }

        public ActivationConditionData ToData()
        {
            return new ActivationConditionData
            {
                Type = Type,
                Value = Value,
                Value2 = Value2,
                StringValue = StringValue,
                Negate = Negate,
            };
        }
    }

    [MemoryPackable]
    public partial class SerializableEffectCostEntry
    {
        [MemoryPackOrder(TagTable.EffectCostEntry_CostType)]
        public int CostType;

        [MemoryPackOrder(TagTable.EffectCostEntry_Value)]
        public int Value;

        [MemoryPackOrder(TagTable.EffectCostEntry_ManaType)]
        public int ManaType;

        [MemoryPackOrder(TagTable.EffectCostEntry_TurnDuration)]
        public int TurnDuration;

        public static SerializableEffectCostEntry FromEntry(CostEntry data)
        {
            return new SerializableEffectCostEntry
            {
                CostType = data.CostType,
                Value = data.Value,
                ManaType = data.ManaType,
                TurnDuration = data.TurnDuration,
            };
        }

        public CostEntry ToEntry()
        {
            return new CostEntry
            {
                CostType = CostType,
                Value = Value,
                ManaType = ManaType,
                TurnDuration = TurnDuration,
            };
        }
    }

    [MemoryPackable]
    public partial class SerializableEffectStepData
    {
        [MemoryPackOrder(TagTable.EffectStepData_Kind)]
        public int Kind;

        [MemoryPackOrder(TagTable.EffectStepData_Atomic)]
        public SerializableAtomicEffectEntry Atomic;

        [MemoryPackOrder(TagTable.EffectStepData_Condition)]
        public SerializableActivationConditionData Condition;

        [MemoryPackOrder(TagTable.EffectStepData_ThenSteps)]
        public SerializableAtomicEffectEntry[] ThenSteps;

        [MemoryPackOrder(TagTable.EffectStepData_ElseSteps)]
        public SerializableAtomicEffectEntry[] ElseSteps;

        [MemoryPackOrder(TagTable.EffectStepData_ConditionId)]
        public string ConditionId;

        [MemoryPackOrder(TagTable.EffectStepData_ConditionParam)]
        public int ConditionParam;

        [MemoryPackOrder(TagTable.EffectStepData_ConditionStringParam)]
        public string ConditionStringParam;

        public static SerializableEffectStepData FromData(EffectStepData data)
        {
            return new SerializableEffectStepData
            {
                Kind = data.kind,
                Atomic = data.atomic != null ? SerializableAtomicEffectEntry.FromEntry(data.atomic) : null,
                Condition = data.condition != null ? SerializableActivationConditionData.FromData(data.condition) : null,
                ThenSteps = data.thenSteps?.Select(SerializableAtomicEffectEntry.FromEntry).ToArray()
                            ?? Array.Empty<SerializableAtomicEffectEntry>(),
                ElseSteps = data.elseSteps?.Select(SerializableAtomicEffectEntry.FromEntry).ToArray()
                            ?? Array.Empty<SerializableAtomicEffectEntry>(),
                ConditionId = data.conditionId,
                ConditionParam = data.conditionParam,
                ConditionStringParam = data.conditionStringParam,
            };
        }

        public EffectStepData ToData()
        {
            return new EffectStepData
            {
                kind = Kind,
                atomic = Atomic?.ToEntry(),
                condition = Condition?.ToData(),
                thenSteps = ThenSteps?.Select(e => e.ToEntry()).ToList() ?? new List<AtomicEffectEntry>(),
                elseSteps = ElseSteps?.Select(e => e.ToEntry()).ToList() ?? new List<AtomicEffectEntry>(),
                conditionId = ConditionId,
                conditionParam = ConditionParam,
                conditionStringParam = ConditionStringParam,
            };
        }
    }

    [MemoryPackable]
    public partial class SerializableAtomicEffectEntry
    {
        [MemoryPackOrder(TagTable.AEI_Type)]
        public string EffectType;

        [MemoryPackOrder(TagTable.AEI_Value)]
        public int Value;

        [MemoryPackOrder(TagTable.AEI_Value2)]
        public int Value2;

        [MemoryPackOrder(TagTable.AEI_StringValue)]
        public string StringValue;

        [MemoryPackOrder(TagTable.AEI_ManaTypeParam)]
        public int ManaTypeParam;

        [MemoryPackOrder(TagTable.AEI_ZoneParam)]
        public int ZoneParam;

        [MemoryPackOrder(TagTable.AEI_Duration)]
        public int Duration;

        [MemoryPackOrder(TagTable.AEI_TargetTypeOverride)]
        public int TargetTypeOverride = -1;

        [MemoryPackOrder(TagTable.AEI_TargetFilterOverride)]
        public string TargetFilterOverride = "";

        [MemoryPackOrder(TagTable.AEI_TargetCountOverride)]
        public int TargetCountOverride = -2;

        [MemoryPackOrder(TagTable.AEI_DynamicTargetCount)]
        public bool DynamicTargetCount;

        [MemoryPackOrder(TagTable.AEI_Drawbacks)]
        public string[] Drawbacks;

        public static SerializableAtomicEffectEntry FromEntry(AtomicEffectEntry entry)
        {
            return new SerializableAtomicEffectEntry
            {
                EffectType = entry.EffectType,
                Value = entry.Value,
                Value2 = entry.Value2,
                StringValue = entry.StringValue,
                ManaTypeParam = entry.ManaTypeParam,
                ZoneParam = entry.ZoneParam,
                Duration = entry.Duration,
                TargetTypeOverride = entry.TargetTypeOverride,
                TargetFilterOverride = entry.TargetFilterOverride,
                TargetCountOverride = entry.TargetCountOverride,
                DynamicTargetCount = entry.DynamicTargetCount,
                Drawbacks = entry.Drawbacks?.ToArray() ?? System.Array.Empty<string>(),
            };
        }

        public AtomicEffectEntry ToEntry()
        {
            return new AtomicEffectEntry
            {
                EffectType = EffectType,
                Value = Value,
                Value2 = Value2,
                StringValue = StringValue,
                ManaTypeParam = ManaTypeParam,
                ZoneParam = ZoneParam,
                Duration = Duration,
                TargetTypeOverride = TargetTypeOverride,
                TargetFilterOverride = TargetFilterOverride ?? "",
                TargetCountOverride = TargetCountOverride,
                DynamicTargetCount = DynamicTargetCount,
                Drawbacks = Drawbacks != null ? new List<string>(Drawbacks) : new List<string>(),
            };
        }
    }
}
