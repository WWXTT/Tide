using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using MemoryPack;

namespace CardCore.Serialization
{
    [MemoryPackable]
    public partial class SerializableEffectDefinition
    {
        [MemoryPackOrder(TagTable.ED_Id)]
        public string Id;

        [MemoryPackOrder(TagTable.ED_DisplayName)]
        public string DisplayName;

        [MemoryPackOrder(TagTable.ED_Description)]
        public string Description;

        [MemoryPackOrder(TagTable.ED_BaseSpeed)]
        public int BaseSpeed;

        [MemoryPackOrder(TagTable.ED_ActivationType)]
        public int ActivationType;

        [MemoryPackOrder(TagTable.ED_TriggerTiming)]
        public int TriggerTiming;

        [MemoryPackOrder(TagTable.ED_IsOptional)]
        public bool IsOptional;

        [MemoryPackOrder(TagTable.ED_Duration)]
        public int Duration;

        [MemoryPackOrder(TagTable.ED_Effects)]
        public SerializableAtomicEffectInstance[] Effects;

        [MemoryPackOrder(TagTable.ED_Costs)]
        public SerializableCostInstance[] Costs;

        [MemoryPackOrder(TagTable.ED_Tags)]
        public string[] Tags;

        [MemoryPackOrder(TagTable.ED_SourceCardId)]
        public string SourceCardId;

        [MemoryPackOrder(TagTable.ED_TargetType)]
        public int TargetType;

        [MemoryPackOrder(TagTable.ED_EffectTag)]
        public long EffectTag;

        [MemoryPackOrder(TagTable.ED_ActivationConditions)]
        public SerializableActivationCondition[] ActivationConditions;

        [MemoryPackOrder(TagTable.ED_TriggerConditions)]
        public SerializableActivationCondition[] TriggerConditions;

        public void ComputeEffectTag()
        {
            if (!string.IsNullOrEmpty(Description))
                EffectTag = MurmurHash3.EffectTag(Description);
        }

        public static SerializableEffectDefinition FromDefinition(EffectDefinition def)
        {
            var dto = new SerializableEffectDefinition
            {
                Id = def.Id,
                DisplayName = def.DisplayName,
                Description = def.Description,
                BaseSpeed = def.BaseSpeed,
                ActivationType = (int)def.ActivationType,
                TriggerTiming = (int)def.TriggerTiming,
                IsOptional = def.IsOptional,
                Duration = (int)def.Duration,
                SourceCardId = def.SourceCardId,
                TargetType = (int)def.TargetType,
            };

            dto.Effects = def.Effects?.Select(SerializableAtomicEffectInstance.FromInstance).ToArray()
                          ?? Array.Empty<SerializableAtomicEffectInstance>();

            dto.Costs = def.Costs?.Select(SerializableCostInstance.FromInstance).ToArray()
                        ?? Array.Empty<SerializableCostInstance>();

            dto.Tags = def.Tags?.ToArray() ?? Array.Empty<string>();

            dto.ActivationConditions = def.ActivationConditions?.Select(SerializableActivationCondition.FromCondition).ToArray()
                                       ?? Array.Empty<SerializableActivationCondition>();

            dto.TriggerConditions = def.TriggerConditions?.Select(SerializableActivationCondition.FromCondition).ToArray()
                                    ?? Array.Empty<SerializableActivationCondition>();

            dto.ComputeEffectTag();
            return dto;
        }

        public EffectDefinition ToDefinition()
        {
            var def = new EffectDefinition
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                BaseSpeed = BaseSpeed,
                ActivationType = (EffectActivationType)ActivationType,
                TriggerTiming = (TriggerTiming)TriggerTiming,
                IsOptional = IsOptional,
                Duration = (DurationType)Duration,
                SourceCardId = SourceCardId,
                TargetType = (EffectTargetType)TargetType,
            };

            if (Effects != null)
                def.Effects = Effects.Select(e => e.ToInstance()).ToList();

            if (Costs != null)
                def.Costs = Costs.Select(c => c.ToInstance()).ToList();

            if (Tags != null)
                def.Tags = Tags.ToList();

            if (ActivationConditions != null)
                def.ActivationConditions = ActivationConditions.Select(a => a.ToCondition()).ToList();

            if (TriggerConditions != null)
                def.TriggerConditions = TriggerConditions.Select(t => t.ToCondition()).ToList();

            return def;
        }
    }

    [MemoryPackable]
    public partial class SerializableAtomicEffectInstance
    {
        [MemoryPackOrder(TagTable.AEI_Type)]
        public int Type;

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

        public static SerializableAtomicEffectInstance FromInstance(AtomicEffectInstance inst)
        {
            return new SerializableAtomicEffectInstance
            {
                Type = (int)inst.Type,
                Value = inst.Value,
                Value2 = inst.Value2,
                StringValue = inst.StringValue,
                ManaTypeParam = (int)inst.ManaTypeParam,
                ZoneParam = (int)inst.ZoneParam,
                Duration = (int)inst.Duration,
            };
        }

        public AtomicEffectInstance ToInstance()
        {
            return new AtomicEffectInstance
            {
                Type = (AtomicEffectType)Type,
                Value = Value,
                Value2 = Value2,
                StringValue = StringValue,
                ManaTypeParam = (ManaType)ManaTypeParam,
                ZoneParam = (Zone)ZoneParam,
                Duration = (DurationType)Duration,
            };
        }
    }

    [MemoryPackable]
    public partial class SerializableCostInstance
    {
        [MemoryPackOrder(TagTable.CI_Type)]
        public int Type;

        [MemoryPackOrder(TagTable.CI_Value)]
        public int Value;

        [MemoryPackOrder(TagTable.CI_ManaType)]
        public int ManaType;

        public static SerializableCostInstance FromInstance(CostInstance inst)
        {
            return new SerializableCostInstance
            {
                Type = (int)inst.Type,
                Value = inst.Value,
                ManaType = (int)inst.ManaType,
            };
        }

        public CostInstance ToInstance()
        {
            return new CostInstance
            {
                Type = (CostType)Type,
                Value = Value,
                ManaType = (ManaType)ManaType,
            };
        }
    }

    [MemoryPackable]
    public partial class SerializableActivationCondition
    {
        [MemoryPackOrder(TagTable.AC_Type)]
        public int Type;

        [MemoryPackOrder(TagTable.AC_Value)]
        public int Value;

        [MemoryPackOrder(TagTable.AC_Value2)]
        public int Value2;

        public static SerializableActivationCondition FromCondition(ActivationCondition cond)
        {
            return new SerializableActivationCondition
            {
                Type = (int)cond.Type,
                Value = cond.Value,
                Value2 = cond.Value2,
            };
        }

        public ActivationCondition ToCondition()
        {
            return new ActivationCondition
            {
                Type = (ConditionType)Type,
                Value = Value,
                Value2 = Value2,
            };
        }
    }
}