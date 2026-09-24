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
            var rawTags = GetRawTagDefinitions();
            var generated = GenerateCode(tags, rawTags);

            // 检测碰撞（哈希条目 + 手工定值条目合并查重）
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
            foreach (var (constName, value, _) in rawTags)
            {
                if (usedTags.TryGetValue(value, out var existing))
                {
                    Debug.LogError($"Tag collision: 手工定值 {constName} vs {existing} = {value}");
                    hasCollision = true;
                }
                else
                {
                    usedTags[value] = constName;
                }
            }

            if (hasCollision)
            {
                Debug.LogError("存在标签碰撞，请检查并修改属性名！");
                return;
            }

            File.WriteAllText(OUTPUT_PATH, generated);
            AssetDatabase.Refresh();
            Debug.Log($"TagTable.cs 已生成，共 {tags.Length + rawTags.Length} 个标签（含 {rawTags.Length} 个手工定值保留位）");
        }

        /// <summary>
        /// 手工定值条目（2026-09-23 增设）：历史上手工分配、哈希公式推不出的标签值——
        /// **改值=线格式字段 ID 变更**，一律原值保留。曾因只存在于 TagTable.cs 手改区，
        /// 被一次"刷新属性排序"整体重生成冲掉（编译 CS0117 事故）——现统一登记在此，
        /// 生成器按原值透传，重生成不再丢条目。新增此类条目：这里加一行 + TagTable.cs 同步。
        /// </summary>
        private static (string constName, int value, string note)[] GetRawTagDefinitions() => new[]
        {
            // SerializableAtomicEffectEntry.amp 专用（复用 AEI_ 前缀命名；2026-09-13 数值随机）
            ("AEI_Amplitude", 1052954154, "SerializableAtomicEffectEntry.amp（数值随机 RandomAmplitude）"),
            // 抉择卡放地模式（2026-09-21：与出牌同口径），紧邻 CardRuntimeId=713270002 手工递增
            ("MsgIntentAddToElementPool_ModeIndex", 713270003, "MsgIntentAddToElementPool.ModeIndex（抉择放地）"),
        };

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
                // 2026-09-10 目标域模型：原子新字段 + Mana 条目（旧 AEI_Value2/ManaTypeParam/ZoneParam/Duration/
                // Target*Override/DynamicTargetCount 标签留作保留位——传输链休眠中，无兼容负担；
                // Drawbacks 标签已随减费归入代价体系退役 2026-09-16）
                ("AtomicEffectInstance", "TargetKinds"),
                ("AtomicEffectInstance", "ManaList"),
                ("SerializableManaAmount", "manaType"),
                ("SerializableManaAmount", "amount"),

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
                // M1 快照扩展（2026-09-10）
                ("PlayerState", "Seat"), ("PlayerState", "IsAI"), ("PlayerState", "FatigueCount"),
                ("PlayerState", "LandCap"), ("PlayerState", "ElementBank"),
                ("PlayerState", "GraveyardCount"), ("PlayerState", "ExileCount"),
                // 退役保留位（2026-09-14 抵消系统退役——字段已删，值保留防字段 ID 复用歧义）
                ("PlayerState", "OffsetOpponentDrawUsed"), ("PlayerState", "OffsetOpponentHealUsed"),

                // ---- M1 网络协议（2026-09-10，详见 根目录 网络协议.md）----

                // ---- NetworkMessage 信封 ----
                ("NetworkMessage", "ProtocolVersion"),

                // ---- NetEntityRef（实体引用双轨：卡=RuntimeId、玩家=座位）----
                ("NetEntityRef", "RuntimeId"), ("NetEntityRef", "Seat"),
                ("NetEntityRef", "IsPlayer"), ("NetEntityRef", "CardId"),

                // ---- NetEvent（通用事件信封，schema-less 投影）----
                ("NetEvent", "EventType"), ("NetEvent", "EventId"),
                ("NetEvent", "TurnNumber"), ("NetEvent", "Params"),

                // ---- NetParam（事件字段投影）----
                ("NetParam", "FieldName"), ("NetParam", "Kind"), ("NetParam", "IntValue"),
                ("NetParam", "FloatValue"), ("NetParam", "StringValue"), ("NetParam", "EntityRefs"),

                // ---- MsgGameStateSync 扩展（V2 全量快照）----
                ("MsgGameStateSync", "ViewerSeat"), ("MsgGameStateSync", "ActiveSeat"),
                ("MsgGameStateSync", "PrioritySeat"), ("MsgGameStateSync", "ZoneCards"),
                ("MsgGameStateSync", "Hands"), ("MsgGameStateSync", "StackV2"),

                // ---- StackItemDTO（栈条目：EffectInstance 投影。EffectDisplayName 已删——2026-09-22
                //      线上去文本，显示名客户端按 EffectId 查表；旧 Stack=SerializableEffectDefinition[] 字段同日删除。
                //      EffectDisplayName 值留保留位防字段 ID 复用歧义）----
                ("StackItemDTO", "Source"), ("StackItemDTO", "IsCardCast"), ("StackItemDTO", "EffectId"),
                ("StackItemDTO", "EffectDisplayName"),
                ("StackItemDTO", "ModeIndex"),
                ("StackItemDTO", "Targets"), ("StackItemDTO", "ActivationSpeed"),
                ("StackItemDTO", "StackObjectType"),

                // ---- NetZoneCards（区域全量）----
                ("NetZoneCards", "Seat"), ("NetZoneCards", "Zone"), ("NetZoneCards", "Cards"),

                // ---- CardHandInfo（手牌隐藏信息口径：己方传 RuntimeId、对方只数量）----
                ("CardHandInfo", "Seat"), ("CardHandInfo", "Count"), ("CardHandInfo", "OwnRuntimeIds"),

                // ---- 上行 intent ----
                ("MsgIntentPlayCard", "CardRuntimeId"), ("MsgIntentPlayCard", "Targets"),
                ("MsgIntentPlayCard", "FromZone"), ("MsgIntentPlayCard", "ModeIndex"),
                ("MsgIntentTapForElement", "CardRuntimeId"), ("MsgIntentTapForElement", "ManaType"),
                ("MsgIntentAddToElementPool", "CardRuntimeId"),
                ("MsgIntentDeclareAttack", "Attacker"), ("MsgIntentDeclareAttack", "Target"),
                ("MsgIntentDeclareBlock", "Blocker"), ("MsgIntentDeclareBlock", "Attacker"),
                ("MsgIntentActivateEffect", "SourceCardRuntimeId"), ("MsgIntentActivateEffect", "EffectId"),
                ("MsgIntentActivateEffect", "Targets"), ("MsgIntentActivateEffect", "PaidBoost"),

                // ---- 反问请求/应答 ----
                ("MsgSelectRequest", "RequestId"), ("MsgSelectRequest", "ChooserSeat"),
                ("MsgSelectRequest", "Title"), ("MsgSelectRequest", "Hint"),
                ("MsgSelectRequest", "AllowCancel"), ("MsgSelectRequest", "TimeoutSeconds"),
                ("MsgSelectRequest", "Min"), ("MsgSelectRequest", "Max"),
                ("MsgSelectRequest", "Labels"), ("MsgSelectRequest", "Candidates"),
                ("MsgSelectResponse", "RequestId"), ("MsgSelectResponse", "Indices"),

                // ---- MsgNetEventBatch（事件流批次下行）----
                ("MsgNetEventBatch", "Events"),

                // ---- RuntimeCardState 快照补齐 ----
                ("RuntimeCardState", "RuntimeId"), ("RuntimeCardState", "ControllerSeat"),

                // ---- 开局握手（2026-09-22：DeckSubmit/MatchManifest + 卡组闭包原子行摘要）----
                ("NetDeckDigest", "AtomicRowsHash"), ("NetDeckDigest", "AtomicRowCount"),
                ("MsgDeckSubmit", "DeckName"), ("MsgDeckSubmit", "CardIds"), ("MsgDeckSubmit", "Digest"),
                ("MsgMatchManifest", "OwnSeat"), ("MsgMatchManifest", "OwnCardIds"),
                ("MsgMatchManifest", "OpponentCardCount"), ("MsgMatchManifest", "OwnDeckDigest"),

                // ---- 会话层（M2，2026-09-23：Error 载荷 + 房间状态机，见 网络协议.md §12）----
                ("MsgError", "Reason"), ("MsgError", "Context"),
                ("MsgJoinRoom", "RoomId"), ("MsgJoinRoom", "WantSeat"),
                ("MsgJoinRoom", "AsSpectator"), ("MsgJoinRoom", "Nickname"),
                ("MsgRoomState", "RoomId"), ("MsgRoomState", "Phase"),
                ("MsgRoomState", "Players"), ("MsgRoomState", "SpectatorCount"),
                ("MsgRoomState", "FirstSeatThisMatch"),
                ("MsgRoomSeatInfo", "Seat"), ("MsgRoomSeatInfo", "Nickname"),
                ("MsgRoomSeatInfo", "Connected"),

                // ---- 大厅层（L1，2026-09-24：房间列表/自动匹配/AI 填位，见 网络协议.md §13）----
                ("MsgLobbyHello", "Nickname"),
                ("MsgLobbyState", "Rooms"), ("MsgLobbyState", "QueuedCount"),
                ("MsgLobbyRoomInfo", "RoomId"), ("MsgLobbyRoomInfo", "Name"),
                ("MsgLobbyRoomInfo", "Phase"), ("MsgLobbyRoomInfo", "PlayerCount"),
                ("MsgLobbyRoomInfo", "Nicknames"), ("MsgLobbyRoomInfo", "SpectatorCount"),
                ("MsgLobbyCreateRoom", "RoomName"),
                ("MsgLobbyJoinRoom", "RoomId"),
                ("MsgLobbyAddAi", "RoomId"), ("MsgLobbyAddAi", "Nickname"),
            };

            var result = new (string className, string propName, int tag)[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                result[i] = (raw[i].Item1, raw[i].Item2, StableTag(raw[i].Item1, raw[i].Item2));
            return result;
        }

        private static string GenerateCode((string className, string propName, int tag)[] tags,
            (string constName, int value, string note)[] rawTags)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 此文件由 Tools/刷新MemoryPackOrder 自动生成，请勿手动修改");
            sb.AppendLine("// 如需添加新属性，请修改 RefreshMemoryPackOrder.cs 中的 GetTagDefinitions()");
            sb.AppendLine("// 手工定值标签登记 GetRawTagDefinitions()（只改 TagTable.cs 的手改区会被下次重生成冲掉）");
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

            // 手工定值保留位（原值透传——见 GetRawTagDefinitions 的注释）
            sb.AppendLine();
            sb.AppendLine("        // ---- 手工定值保留位（历史手工分配的值，哈希公式推不出；改值=线格式字段 ID 变更，勿动）----");
            foreach (var (constName, value, note) in rawTags)
                sb.AppendLine($"        public const int {constName} = {value}; // {note}");

            sb.AppendLine();
            sb.AppendLine($"        // 共 {tags.Length + rawTags.Length} 个标签（含 {rawTags.Length} 个手工定值保留位）");
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
                "NetEntityRef" => "NER",
                "NetEvent" => "NE",
                "NetParam" => "NP",
                "StackItemDTO" => "SID",
                "NetZoneCards" => "NZC",
                "CardHandInfo" => "CHI",
                "MsgSelectRequest" => "MSelR",
                "MsgSelectResponse" => "MSelP",
                "MsgNetEventBatch" => "MNEB",
                "NetDeckDigest" => "NDD",
                "MsgDeckSubmit" => "MDS",
                "MsgMatchManifest" => "MMM",
                "MsgError" => "MErr",
                "MsgJoinRoom" => "MJR",
                "MsgRoomState" => "MRS",
                "MsgRoomSeatInfo" => "MRSI",
                "MsgLobbyHello" => "MLH",
                "MsgLobbyState" => "MLS",
                "MsgLobbyRoomInfo" => "MLRI",
                "MsgLobbyCreateRoom" => "MLCR",
                "MsgLobbyJoinRoom" => "MLJR",
                "MsgLobbyAddAi" => "MLAA",
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