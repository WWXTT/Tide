#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CardCore.Tools
{
    public static class RefreshMemoryPackOrder
    {
        private const string OUTPUT_PATH = "Assets/Scripts/CardCore/Serialization/TagTable.cs";
        private const uint SYNERGY_TAG_SEED = 0x53796E67;

        [MenuItem("Tools/刷新属性排序（新增序列化字段时使用）")]
        public static void GenerateTagTable()
        {
            var tags = GetTagDefinitions();
            var generated = GenerateCode(tags);

            // 检测碰撞
            var usedTags = new System.Collections.Generic.Dictionary<int, string>();
            bool hasCollision = false;

            foreach (var (className, propName, tag) in tags)
            {
                if (usedTags.TryGetValue(tag, out var existing))
                {
                    Debug.LogError($"Tag collision: {className}.{propName} vs {existing} = {tag}");
                    hasCollision = true;
                }
                else
                {
                    usedTags[tag] = $"{className}.{propName}";
                }
            }

            if (hasCollision)
            {
                Debug.LogError("存在标签碰撞，请检查并修改属性名！");
                return;
            }

            File.WriteAllText(OUTPUT_PATH, generated);
            AssetDatabase.Refresh();
            Debug.Log($"TagTable.cs 已生成，共 {tags.Length} 个标签");
        }

        private static (string className, string propName, int tag)[] GetTagDefinitions()
        {
            var raw = new (string, string)[]
            {
                // ---- CardData ----
                ("CardData", "ID"), ("CardData", "Supertype"), ("CardData", "CardName"),
                ("CardData", "Illustration"), ("CardData", "Life"), ("CardData", "Power"),
                ("CardData", "Cost"), ("CardData", "Effects"), ("CardData", "CreationTicks"),
                ("CardData", "Tags"), ("CardData", "Keywords"),

                // ---- EffectData ----
                ("EffectData", "Abbreviation"), ("EffectData", "Initiative"), ("EffectData", "Parameters"),
                ("EffectData", "Speed"), ("EffectData", "ManaType"), ("EffectData", "Description"),
                ("EffectData", "EffectTag"),
                ("EffectData", "DisplayName"), ("EffectData", "IsOptional"), ("EffectData", "Duration"),
                ("EffectData", "Tags"), ("EffectData", "Costs"), ("EffectData", "ActivationConditions"),
                ("EffectData", "TriggerConditions"), ("EffectData", "Steps"),

                // ---- ActivationConditionData ----
                ("ActivationConditionData", "Type"), ("ActivationConditionData", "Value"),
                ("ActivationConditionData", "Value2"), ("ActivationConditionData", "StringValue"),
                ("ActivationConditionData", "Negate"),

                // ---- EffectCostEntry ----
                ("EffectCostEntry", "CostType"), ("EffectCostEntry", "Value"),
                ("EffectCostEntry", "ManaType"), ("EffectCostEntry", "TurnDuration"),

                // ---- EffectStepData ----
                ("EffectStepData", "Kind"), ("EffectStepData", "Atomic"), ("EffectStepData", "Condition"),
                ("EffectStepData", "ThenSteps"), ("EffectStepData", "ElseSteps"),
                ("EffectStepData", "ConditionId"), ("EffectStepData", "ConditionParam"),
                ("EffectStepData", "ConditionStringParam"), ("EffectStepData", "Choices"),

                // ---- EffectChoiceData（抉择模式条目）----
                ("EffectChoiceData", "Label"), ("EffectChoiceData", "Steps"),

                // ---- CostEntryDTO ----
                ("CostEntryDTO", "ManaType"), ("CostEntryDTO", "Value"),

                // ---- RuntimeCardState ----
                ("RuntimeCardState", "ID"), ("RuntimeCardState", "Power"), ("RuntimeCardState", "Life"),
                ("RuntimeCardState", "MaxLife"), ("RuntimeCardState", "BaseCost"), ("RuntimeCardState", "CostModifier"),
                ("RuntimeCardState", "Armor"), ("RuntimeCardState", "DamagePrevention"),
                ("RuntimeCardState", "IsTapped"), ("RuntimeCardState", "IsFrozen"),
                ("RuntimeCardState", "IsNegated"), ("RuntimeCardState", "IsNullified"),
                ("RuntimeCardState", "Zone"), ("RuntimeCardState", "TargetFlags"),
                ("RuntimeCardState", "Keywords"), ("RuntimeCardState", "Counters"),
                ("RuntimeCardState", "WasDepletedAsLand"), ("RuntimeCardState", "RemainingLandTokens"),

                // ---- CounterEntryDTO ----
                ("CounterEntryDTO", "Key"), ("CounterEntryDTO", "Value"),

                // ---- ManaEntryDTO ----
                ("ManaEntryDTO", "ManaType"), ("ManaEntryDTO", "Value"),

                // ---- AtomicEffectInstance ----
                ("AtomicEffectInstance", "Type"), ("AtomicEffectInstance", "Value"),
                ("AtomicEffectInstance", "Value2"), ("AtomicEffectInstance", "StringValue"),
                ("AtomicEffectInstance", "ManaTypeParam"), ("AtomicEffectInstance", "ZoneParam"),
                ("AtomicEffectInstance", "Duration"),
                ("AtomicEffectInstance", "TargetTypeOverride"), ("AtomicEffectInstance", "TargetFilterOverride"),
                ("AtomicEffectInstance", "TargetCountOverride"), ("AtomicEffectInstance", "DynamicTargetCount"),
                ("AtomicEffectInstance", "Drawbacks"),

                // ---- CostInstance ----
                ("CostInstance", "Type"), ("CostInstance", "Value"), ("CostInstance", "ManaType"),

                // ---- ActivationCondition ----
                ("ActivationCondition", "Type"), ("ActivationCondition", "Value"), ("ActivationCondition", "Value2"),

                // ---- EffectDefinition ----
                ("EffectDefinition", "Id"), ("EffectDefinition", "DisplayName"), ("EffectDefinition", "Description"),
                ("EffectDefinition", "BaseSpeed"), ("EffectDefinition", "ActivationType"),
                ("EffectDefinition", "TriggerTiming"), ("EffectDefinition", "IsOptional"),
                ("EffectDefinition", "Duration"), ("EffectDefinition", "Effects"),
                ("EffectDefinition", "Costs"), ("EffectDefinition", "Tags"),
                ("EffectDefinition", "SourceCardId"), ("EffectDefinition", "TargetType"),
                ("EffectDefinition", "EffectTag"), ("EffectDefinition", "ActivationConditions"),
                ("EffectDefinition", "TriggerConditions"),

                // ---- NetworkMessage ----
                ("NetworkMessage", "MessageType"), ("NetworkMessage", "SequenceId"),
                ("NetworkMessage", "Payload"), ("NetworkMessage", "Timestamp"),

                // ---- MsgPlayCard ----
                ("MsgPlayCard", "CardID"), ("MsgPlayCard", "FromZone"), ("MsgPlayCard", "ToZone"),
                ("MsgPlayCard", "ChosenManaType"),

                // ---- MsgActivateEffect ----
                ("MsgActivateEffect", "SourceCardID"), ("MsgActivateEffect", "EffectTag"),
                ("MsgActivateEffect", "ActivationSpeed"), ("MsgActivateEffect", "TargetIDs"),

                // ---- MsgGameStateSync ----
                ("MsgGameStateSync", "CurrentTurn"), ("MsgGameStateSync", "CurrentPhase"),
                ("MsgGameStateSync", "Players"), ("MsgGameStateSync", "BattlefieldCards"),
                ("MsgGameStateSync", "Stack"),

                // ---- PlayerState ----
                ("PlayerState", "Name"), ("PlayerState", "Life"), ("PlayerState", "MaxHealth"),
                ("PlayerState", "DeckCount"), ("PlayerState", "HandCount"),
            };

            var result = new (string className, string propName, int tag)[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                result[i] = (raw[i].Item1, raw[i].Item2, StableTag(raw[i].Item1, raw[i].Item2));
            return result;
        }

        private static string GenerateCode((string className, string propName, int tag)[] tags)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 此文件由 Tools/刷新MemoryPackOrder 自动生成，请勿手动修改");
            sb.AppendLine("// 如需添加新属性，请修改 RefreshMemoryPackOrder.cs 中的 GetTagDefinitions()");
            sb.AppendLine();
            sb.AppendLine("namespace CardCore.Serialization");
            sb.AppendLine("{");
            sb.AppendLine("    public static class TagTable");
            sb.AppendLine("    {");

            string currentClass = null;
            foreach (var (className, propName, tag) in tags)
            {
                if (currentClass != className)
                {
                    sb.AppendLine();
                    sb.AppendLine($"        // ---- {className} ----");
                    currentClass = className;
                }

                string varPrefix = GetVarPrefix(className);
                sb.AppendLine($"        public const int {varPrefix}_{propName} = {tag};");
            }

            sb.AppendLine();
            sb.AppendLine($"        // 共 {tags.Length} 个标签");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetVarPrefix(string className)
        {
            return className switch
            {
                "RuntimeCardState" => "RCS",
                "AtomicEffectInstance" => "AEI",
                "CostInstance" => "CI",
                "ActivationCondition" => "AC",
                "EffectDefinition" => "ED",
                "NetworkMessage" => "NM",
                "MsgPlayCard" => "MPC",
                "MsgActivateEffect" => "MAE",
                "MsgGameStateSync" => "MGSS",
                "PlayerState" => "PS",
                _ => className
            };
        }

        // MurmurHash32 实现（与 MurmurHash3.cs 保持一致）
        private static uint Hash32(byte[] data, uint seed)
        {
            const uint c1 = 0xcc9e2d51, c2 = 0x1b873593;
            const int r1 = 15, r2 = 13;
            const uint m = 5, n = 0xe6546b64;

            int length = data.Length, blocks = length / 4;
            uint hash = seed;

            for (int i = 0; i < blocks; i++)
            {
                uint k = BitConverter.ToUInt32(data, i * 4);
                if (!BitConverter.IsLittleEndian) k = ReverseBytes(k);
                k *= c1; k = RotateLeft(k, r1); k *= c2;
                hash ^= k; hash = RotateLeft(hash, r2); hash = hash * m + n;
            }

            int tailIndex = blocks * 4;
            uint tail = 0;
            switch (length & 3)
            {
                case 3: tail ^= (uint)data[tailIndex + 2] << 16; goto case 2;
                case 2: tail ^= (uint)data[tailIndex + 1] << 8; goto case 1;
                case 1: tail ^= data[tailIndex];
                        tail *= c1; tail = RotateLeft(tail, r1); tail *= c2;
                        hash ^= tail; break;
            }

            hash ^= (uint)length;
            hash ^= hash >> 16; hash *= 0x85ebca6b;
            hash ^= hash >> 13; hash *= 0xc2b2ae35;
            hash ^= hash >> 16;
            return hash;
        }

        private static uint RotateLeft(uint x, int r) => (x << r) | (x >> (32 - r));
        private static uint ReverseBytes(uint v) =>
            (v & 0x000000FFU) << 24 | (v & 0x0000FF00U) << 8 |
            (v & 0x00FF0000U) >> 8 | (v & 0xFF000000U) >> 24;

        private static int StableTag(string className, string propertyName)
        {
            string input = $"{className}.{propertyName}";
            byte[] data = Encoding.UTF8.GetBytes(input);
            return (int)(Hash32(data, SYNERGY_TAG_SEED) & 0x7FFFFFFF);
        }
    }
}
#endif