#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CardCore.Tools
{
    public static class RefreshMemoryPackOrder
    {
        private const string OUTPUT_PATH = "Assets/Scripts/HotUpdate/CardCore/Serialization/TagTable.cs"; // 热更重组后实际位置（旧 Assets/Scripts/CardCore 已不存在，2026-10-10 修正）
        private const uint SYNERGY_TAG_SEED = 0x53796E67;

        /// <summary>常量行（保值读取用）：缩进 + 名 + 值 + 分号 + 行尾注释原样捕获。</summary>
        private static readonly Regex ConstLineRegex =
            new Regex(@"^(\s*)public const int (\w+) = (-?\d+);(.*)$", RegexOptions.Compiled);

        /// <summary>
        /// 生成标签表（2026-11-11 改为**保值生成**）。
        ///
        /// **为什么改**：v11/v13/S7b 有一批手算标签（全量强转口径 / 早期配方），与当前公式
        /// `Murmur32("类.属性", 0x53796E67) &amp; 0x7FFFFFFF` 不一致；旧版"全量重算"会在无提示的情况下
        /// 改写 30+ 个**已上线**的 MemoryPackOrder —— 而 MemoryPackOrder = 字段序列化顺序，改值即改
        /// 线格式。真实缺口：`MsgRoomSettings.Seed`/`MsgRoomState.Seed`（v13）压根没登记，跑工具会直接
        /// 删掉常量；另有 29 个 v11 手算值 + 1 个 S7b 直方图会被静默改成公式值。
        ///
        /// **现在**：已有常量一律沿用现表**现值**（值 + 原行含注释原样保留），只给新字段按公式取号；
        /// 守门把"会改/会删/会撞/解析不全"四种情况全部变成显式报错，绝不静默改写。
        /// </summary>
        [MenuItem("Tools/刷新属性排序（新增序列化字段时使用）")]
        public static void GenerateTagTable()
        {
            var defs = GetTagDefinitions();
            var existing = ReadExisting();

            // ---- 守门 1：登记表内部重复（(类,属性) 二次登记＝复制粘贴事故）----
            var seenDefs = new HashSet<string>();
            var dupDefs = new List<string>();
            foreach (var (cls, prop, _) in defs)
                if (!seenDefs.Add(cls + "." + prop)) dupDefs.Add(cls + "." + prop);
            if (dupDefs.Count > 0)
            {
                Debug.LogError("[TagTable] 登记重复，已中止：" + string.Join("、", dupDefs));
                return;
            }

            // ---- 保值组装：有现值→沿用（原行一并保留）；无→按公式取号（新字段）----
            var tags = new (string className, string propName, int tag, string line)[defs.Length];
            var frozen = new List<string>();  // 现值≠公式：历史手算批（冻结，禁止重算）
            var fresh = new List<string>();   // 本次新字段（按公式取号）
            for (int i = 0; i < defs.Length; i++)
            {
                var (cls, prop, _) = defs[i];
                string name = GetVarPrefix(cls) + "_" + prop;
                if (existing.TryGetValue(name, out var old))
                {
                    tags[i] = (cls, prop, old.Value, old.Line); // ★ 保值：值与原行（含注释）原样沿用
                    if (old.Value != StableTag(cls, prop)) frozen.Add(name);
                }
                else
                {
                    tags[i] = (cls, prop, StableTag(cls, prop), null);
                    fresh.Add(name);
                }
            }

            // ---- 守门 2：最终值全表唯一（新字段撞上冻结值＝生成器歧义——改字段名，不许改已上线值）----
            var byValue = new Dictionary<int, string>();
            var collisions = new List<string>();
            foreach (var (cls, prop, tag, _) in tags)
            {
                string who = cls + "." + prop;
                if (byValue.TryGetValue(tag, out var other)) collisions.Add($"{tag}: {other} vs {who}");
                else byValue[tag] = who;
            }
            if (collisions.Count > 0)
            {
                Debug.LogError("[TagTable] 标签值碰撞，已中止（改字段名，勿改已上线值）：" + string.Join("；", collisions));
                return;
            }

            // ---- 守门 3：旧表里有、登记表里没有的常量（重生成会删掉它 → 引用处编译错）----
            var produced = new HashSet<string>();
            foreach (var (cls, prop, _, _) in tags) produced.Add(GetVarPrefix(cls) + "_" + prop);
            var dropped = new List<string>();
            foreach (var name in existing.Keys) if (!produced.Contains(name)) dropped.Add(name);
            if (dropped.Count > 0)
            {
                Debug.LogError("[TagTable] 以下常量未登记（重生成会删除；若确属退役请先手工删表再跑）："
                    + string.Join("、", dropped));
                return;
            }

            // ---- 守门 4：常量行解析不全（格式漂移会让"保值"静默失效）----
            int rawConstLines = CountRawConstLines();
            if (rawConstLines != existing.Count)
            {
                Debug.LogError($"[TagTable] 常量行解析不全（原文 {rawConstLines} 行 / 解析 {existing.Count} 行）——检查格式，已中止");
                return;
            }

            File.WriteAllText(OUTPUT_PATH, GenerateCode(tags, frozen), new UTF8Encoding(true)); // Unity 口径：UTF-8 BOM
            AssetDatabase.Refresh();
            Debug.Log($"[TagTable.cs] 已生成：共 {tags.Length} 个标签（保值 {tags.Length - fresh.Count}、新算 {fresh.Count}"
                + (fresh.Count > 0 ? "：" + string.Join("、", fresh) : "")
                + $"）；非公式值 {frozen.Count} 条已冻结：{string.Join("、", frozen)}");
        }

        /// <summary>读现表：常量名 → (值, 原行)。原行原样沿用＝保值的同时保住手工注释（v13/S7b 溯源注释不丢）。</summary>
        private static Dictionary<string, (int Value, string Line)> ReadExisting()
        {
            var map = new Dictionary<string, (int, string)>();
            if (!File.Exists(OUTPUT_PATH)) return map; // 首建（空仓）＝全公式取号
            foreach (var line in File.ReadAllLines(OUTPUT_PATH))
            {
                var m = ConstLineRegex.Match(line);
                if (m.Success) map[m.Groups[2].Value] = (int.Parse(m.Groups[3].Value), line);
            }
            return map;
        }

        /// <summary>原文里 "public const int" 出现次数（守门 4 对账用）。</summary>
        private static int CountRawConstLines()
        {
            if (!File.Exists(OUTPUT_PATH)) return 0;
            int n = 0;
            foreach (var line in File.ReadAllLines(OUTPUT_PATH))
                if (line.IndexOf("public const int ", StringComparison.Ordinal) >= 0) n++;
            return n;
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
                ("RuntimeCardState", "Zone"),
                // TargetFlags 已删（2026-10-13）·WasDepletedAsLand/RemainingLandTokens 已删
                //（2026-10-11 资源重做退役）——2026-10-28 回写工具：此前历届手改 TagTable 未回写，
                // 旧工具再生成会复活退役常量并冲掉手工值（本次 TagTable 回滚+工具对齐定案）
                ("RuntimeCardState", "Keywords"), ("RuntimeCardState", "Counters"),

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
                ("AtomicEffectInstance", "Domain"), // 并行会话 2026-11-08 引擎奖励效果化：MemoryPackDTOs 尾部追加（AEI_Domain）——注册行缺失阻塞编译，代补
                ("SerializableAtomicEffectEntry", "Amplitude"), // amp 数值随机（2026-10-31 阶段D：手工定值退役并入哈希）
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
                // 角色指示物（2026-10-10 角色参战遗留收口：角色攻击弹药等 Player 层指示物同步）
                ("PlayerState", "Counters"),

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
                ("MsgGameStateSync", "RevealedZoneCards"), // 展示卡·信息轴（2026-10-31 阶段D：手工定值退役并入哈希）
                ("MsgGameStateSync", "TurnSecondsLeft"), // v14 服务器权威回合计时（HUD 可用时间倒计时）

                // ---- StackItemDTO（栈条目：EffectInstance 投影。EffectDisplayName/旧 Stack 字段
                //      已删（2026-09-22 线上去文本，墓碑 2026-10-10 清理）；显示名客户端按 EffectId 查表）----
                ("StackItemDTO", "Source"), ("StackItemDTO", "IsCardCast"), ("StackItemDTO", "EffectId"),
                ("StackItemDTO", "ModeIndex"),
                ("StackItemDTO", "Targets"), ("StackItemDTO", "ActivationSpeed"),
                ("StackItemDTO", "StackObjectType"),
                ("StackItemDTO", "References"), // 参照选位（2026-11-08 目标选择重构·协议 v10）

                // ---- NetZoneCards（区域全量）----
                ("NetZoneCards", "Seat"), ("NetZoneCards", "Zone"), ("NetZoneCards", "Cards"),

                // ---- CardHandInfo（手牌隐藏信息口径：己方传 RuntimeId、对方只数量）----
                ("CardHandInfo", "Seat"), ("CardHandInfo", "Count"), ("CardHandInfo", "OwnRuntimeIds"),

                // ---- 上行 intent ----
                ("MsgIntentPlayCard", "CardRuntimeId"), ("MsgIntentPlayCard", "Targets"),
                ("MsgIntentPlayCard", "FromZone"), ("MsgIntentPlayCard", "ModeIndex"),
                ("MsgIntentPlayCard", "References"), // 参照选位（2026-11-08 目标选择重构·协议 v10）
                ("MsgIntentPlayCard", "CellX"), ("MsgIntentPlayCard", "CellZ"), // 落格坐标（2026-11-12 协议 v19）
                ("MsgIntentBuildCard", "CardRuntimeId"), ("MsgIntentBuildCard", "Color"), // 2026-10-29 P7 建造（协议 v4）
                // MsgIntentTapForElement 已删（2026-10-11 现取现用：无手动横置）——两个 tag 常量同批移除（2026-10-22）
                ("MsgIntentAddToElementPool", "CardRuntimeId"),
                ("MsgIntentAddToElementPool", "ModeIndex"), // 抉择放地模式（2026-10-31 阶段D：手工定值退役并入哈希）
                ("MsgIntentDeclareAttack", "Attacker"), ("MsgIntentDeclareAttack", "Target"),
                ("MsgIntentDeclareBlock", "Blocker"), ("MsgIntentDeclareBlock", "Attacker"),
                ("MsgIntentActivateEffect", "SourceCardRuntimeId"), ("MsgIntentActivateEffect", "EffectId"),
                ("MsgIntentActivateEffect", "Targets"), ("MsgIntentActivateEffect", "PaidBoost"),
                ("MsgIntentActivateEffect", "References"), // 参照选位（2026-11-08 目标选择重构·协议 v10）
                ("MsgIntentActivateHeroSkill", "EffectIndex"),

                // ---- 反问请求/应答 ----
                ("MsgSelectRequest", "RequestId"), ("MsgSelectRequest", "ChooserSeat"),
                ("MsgSelectRequest", "Title"), ("MsgSelectRequest", "Hint"),
                ("MsgSelectRequest", "AllowCancel"), ("MsgSelectRequest", "TimeoutSeconds"),
                ("MsgSelectRequest", "Min"), ("MsgSelectRequest", "Max"),
                ("MsgSelectRequest", "Labels"), ("MsgSelectRequest", "Candidates"),
                ("MsgSelectRequest", "SpecKey"), // spec 计划关联键（2026-11-08 目标选择重构·协议 v10）
                ("MsgSelectResponse", "RequestId"), ("MsgSelectResponse", "Indices"),
                ("MsgTextRequest", "RequestId"), ("MsgTextRequest", "ChooserSeat"),
                ("MsgTextRequest", "Title"), ("MsgTextRequest", "Default"),
                ("MsgTextRequest", "MaxLen"), ("MsgTextRequest", "TimeoutSeconds"),
                ("MsgTextResponse", "RequestId"), ("MsgTextResponse", "Text"),

                // ---- MsgNetEventBatch（事件流批次下行）----
                ("MsgNetEventBatch", "Events"),

                // ---- RuntimeCardState 快照补齐 ----
                ("RuntimeCardState", "RuntimeId"), ("RuntimeCardState", "ControllerSeat"),
                ("RuntimeCardState", "BuildPaid"), // 2026-10-29 P7 建造已建额（协议 v4 快照手牌卡状态）
                ("RuntimeCardState", "LockedColor"), // 地牌锁色（2026-10-31 阶段D：手工定值退役并入哈希）
                ("RuntimeCardState", "DelayedDeath"), // 延迟死亡标记（2026-10-31 阶段D：手工定值退役并入哈希）
                ("RuntimeCardState", "CellX"), ("RuntimeCardState", "CellZ"), // 快照落格（2026-11-12 协议 v19 绝对坐标）

                // ---- 开局握手（2026-09-22：DeckSubmit/MatchManifest + 卡组闭包原子行摘要）----
                ("NetDeckDigest", "AtomicRowsHash"), ("NetDeckDigest", "AtomicRowCount"),
                ("NetDeckDigest", "TokenSig"), // 2026-10-29 P9b 衍生物区/槽绑定摘要（协议 v5）
                ("NetDeckDigest", "ContentSig"), // 2026-11-05 ID 冻结收口：卡/效果层内容指纹摘要（协议 v8）
                ("MsgDeckSubmit", "DeckName"), ("MsgDeckSubmit", "CardIds"), ("MsgDeckSubmit", "Digest"),
                ("MsgDeckSubmit", "SkillCardId"), // 英雄技能标记卡（2026-10-31 阶段D：手工定值退役并入哈希；不参与 Digest）
                ("MsgDeckSubmit", "TokenCardIds"), ("MsgDeckSubmit", "TokenBindings"), // P9b 两块（协议 v5）
                ("MsgDeckSubmit", "AddedCards"), ("MsgDeckSubmit", "AddedEffects"),
                ("MsgDeckSubmit", "AddedRows"), // v11 自建内容三载荷（当时手加未登记——S5 补登）
                ("MsgDeckTokenBinding", "CardId"), ("MsgDeckTokenBinding", "EffectIndex"),
                ("MsgDeckTokenBinding", "TemplateId"), // P9b 绑定条目（协议 v5）
                ("MsgMatchManifest", "OwnSeat"), ("MsgMatchManifest", "OwnCardIds"),
                ("MsgMatchManifest", "OpponentCardCount"), ("MsgMatchManifest", "OwnDeckDigest"),
                ("MsgMatchManifest", "OpponentCards"), ("MsgMatchManifest", "OpponentEffects"),
                ("MsgMatchManifest", "OpponentRows"), // v11 对手自建内容三载荷（S5 补登）
                ("MsgMatchManifest", "OpponentCostHistogram"), // S7b：对手费用直方图

                // ---- v11 自建内容跨端 payload（2026-11-09；当时手加 TagTable 未登记——S5 补登防刷新洗掉）----
                ("MsgCardPayload", "CardId"), ("MsgCardPayload", "CardJson"),
                ("MsgEffectPayload", "EffectId"), ("MsgEffectPayload", "EffectJson"),
                ("MsgAtomRowPayload", "RefId"), ("MsgAtomRowPayload", "RowJson"),

                // ---- MsgMatchSummary（v11 终局摘要：双座位各收一份，视角=收件人的对手）----
                ("MsgMatchSummary", "MatchId"), ("MsgMatchSummary", "FinishedAtUnix"),
                ("MsgMatchSummary", "OpponentName"), ("MsgMatchSummary", "OpponentOpenId"),
                ("MsgMatchSummary", "Win"), ("MsgMatchSummary", "OwnDeckName"),
                ("MsgMatchSummary", "OpponentDeckName"), ("MsgMatchSummary", "OpponentCardIds"),
                ("MsgMatchSummary", "OpponentSkillCardId"), ("MsgMatchSummary", "OpponentTokenCardIds"),
                ("MsgMatchSummary", "OpponentTokenBindings"), ("MsgMatchSummary", "Cards"),
                ("MsgMatchSummary", "Effects"), ("MsgMatchSummary", "AtomRows"),
                ("MsgMatchSummary", "Log"),
                ("MsgMatchSummary", "Ranked"), // S8：排位局记账标记

                // ---- 会话层（M2，2026-09-23：Error 载荷 + 房间状态机，见 网络协议.md §12）----
                ("MsgError", "Reason"), ("MsgError", "Context"),
                ("MsgError", "Channel"), // v17：拒绝帧归属通道（房间/对战）
                ("MsgJoinRoom", "RoomId"), ("MsgJoinRoom", "WantSeat"),
                ("MsgJoinRoom", "AsSpectator"), ("MsgJoinRoom", "Nickname"),
                // v11（2026-11-09）当时手加进 TagTable 未登记于此——补登（否则刷新会洗掉这两条）
                ("MsgJoinRoom", "OpenId"), ("MsgJoinRoom", "IsAi"),
                // S5（2026-11-10）房间密码+选卡上报
                ("MsgJoinRoom", "Password"), ("MsgJoinRoom", "DeckName"),
                // S8（2026-11-10）排位模式：进房上报排位标记/指纹/胜率
                ("MsgJoinRoom", "Ranked"), ("MsgJoinRoom", "DeckFingerprint"), ("MsgJoinRoom", "WinRate"),
                ("MsgJoinRoom", "IsHost"), // v17：房主自报（P2P 房主进程回环入座）

                // ---- MsgRankedAck（S8 排位接受应答）----
                ("MsgRankedAck", "Accepted"),
                ("MsgRoomState", "RoomId"), ("MsgRoomState", "Phase"),
                ("MsgRoomState", "Players"), ("MsgRoomState", "SpectatorCount"),
                ("MsgRoomState", "FirstSeatThisMatch"),
                ("MsgRoomState", "FirstMoveMode"), ("MsgRoomState", "ShuffleMode"),
                ("MsgRoomState", "GuestReady"), ("MsgRoomState", "DeckPickSecondsLeft"),
                // v13 本局随机种子（开局定死回显）：2026-11-11 补登记——现值 -210878657 为全量强转口径，
                // 属"冻结值"（见 GenerateTagTable 保值生成），登记后跑工具沿用现值、不改线格式
                ("MsgRoomState", "Seed"),
                ("MsgRoomState", "HostName"), // v17：房主昵称（空=无房主）
                ("MsgRoomSeatInfo", "Seat"), ("MsgRoomSeatInfo", "Nickname"),
                ("MsgRoomSeatInfo", "Connected"),
                ("MsgRoomSeatInfo", "DeckName"), // S5：座位卡组名
                ("MsgRoomSeatInfo", "Ready"),    // v17：逐座准备态
                ("MsgRoomSeatInfo", "IsAi"),     // v17：AI 填位
                ("MsgRoomSeatInfo", "IsHost"),   // v17：座位是否房主

                // ---- 房间就绪层（2026-10-28 开局流程改版：房主配模式+客人准备+房主开局）----
                ("MsgRoomSettings", "FirstMoveMode"), ("MsgRoomSettings", "ShuffleMode"),
                // v13 房主钉种子（0=随机）：2026-11-11 补登记——现值 -191934453 为全量强转口径（冻结值）
                ("MsgRoomSettings", "Seed"),
                ("MsgRoomReady", "Ready"),

                // ---- 大厅层（L1，2026-09-24：房间列表/自动匹配/AI 填位，见 网络协议.md §13）----
                ("MsgLobbyHello", "Nickname"),
                ("MsgLobbyState", "Rooms"), ("MsgLobbyState", "QueuedCount"),
                ("MsgLobbyRoomInfo", "RoomId"), ("MsgLobbyRoomInfo", "Name"),
                ("MsgLobbyRoomInfo", "Phase"), ("MsgLobbyRoomInfo", "PlayerCount"),
                ("MsgLobbyRoomInfo", "Nicknames"), ("MsgLobbyRoomInfo", "SpectatorCount"),
                ("MsgLobbyCreateRoom", "RoomName"),
                ("MsgLobbyJoinRoom", "RoomId"),
                ("MsgLobbyAddAi", "RoomId"), ("MsgLobbyAddAi", "Nickname"),

                // ---- MsgNetLogBatch / NetLogLine（v15 C 案战报行流，2026-11-11：与 TagTable 手算抄入值同配方同值）----
                ("MsgNetLogBatch", "Lines"),
                ("NetLogLine", "Kind"), ("NetLogLine", "Flags"),
                ("NetLogLine", "EventId"), ("NetLogLine", "Text"),

                // ---- MsgSeatSwap（v16 多成员房间，2026-11-11：座位↔观战切换）----
                ("MsgSeatSwap", "ToSpectator"),
            };

            var result = new (string className, string propName, int tag)[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                result[i] = (raw[i].Item1, raw[i].Item2, StableTag(raw[i].Item1, raw[i].Item2));
            return result;
        }

        private static string GenerateCode((string className, string propName, int tag, string line)[] tags,
            List<string> frozen)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 此文件由 Tools/刷新属性排序（新增序列化字段时使用）自动生成，请勿手动修改");
            sb.AppendLine("// 如需添加新属性，请修改 RefreshMemoryPackOrder.cs 中的 GetTagDefinitions()");
            sb.AppendLine("// 生成口径（2026-11-11 起＝保值生成）：已有常量沿用现表现值（整行含注释原样保留），仅新字段");
            sb.AppendLine("// 按 Murmur32(\"类.属性\", 0x53796E67) & 0x7FFFFFFF 取号；末尾「冻结值」段＝历史手算批，重算");
            sb.AppendLine("// 它们＝改 MemoryPackOrder＝改已上线线格式，禁止（工具守门会拦）。");
            sb.AppendLine();
            sb.AppendLine("namespace CardCore.Serialization");
            sb.AppendLine("{");
            sb.AppendLine("    public static class TagTable");
            sb.AppendLine("    {");

            string currentClass = null;
            foreach (var (className, propName, tag, line) in tags)
            {
                if (currentClass != className)
                {
                    sb.AppendLine();
                    sb.AppendLine($"        // ---- {className} ----");
                    currentClass = className;
                }

                // 保值行：原样写回（注释/格式不丢）；新字段：标准行
                sb.AppendLine(line ?? $"        public const int {GetVarPrefix(className)}_{propName} = {tag};");
            }

            if (frozen.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("        // ---- 冻结值（非公式推导，禁止重算：已上线线格式契约）----");
                sb.AppendLine("        // 来源：全量强转口径 / v11 早期手算配方；现值即协议布局。改值＝改字段序列化顺序。");
                foreach (var (className, propName, tag, _) in tags)
                {
                    string name = GetVarPrefix(className) + "_" + propName;
                    if (frozen.Contains(name)) sb.AppendLine($"        // {name} = {tag}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"        // 共 {tags.Length} 个标签（保值生成：{tags.Length - frozen.Count} 条公式值 + {frozen.Count} 条冻结值）");
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
                "SerializableAtomicEffectEntry" => "AEI", // 沿用 AEI_ 前缀（旧线格式命名承袭，2026-10-31 阶段D 起哈希生成）
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
                "MsgTextRequest" => "MTxtR",
                "MsgTextResponse" => "MTxtP",
                "MsgNetEventBatch" => "MNEB",
                "NetDeckDigest" => "NDD",
                "MsgDeckSubmit" => "MDS",
                "MsgDeckTokenBinding" => "MTB",
                "MsgMatchManifest" => "MMM",
                "MsgRankedAck" => "MRA",        // S8 排位接受应答
                "MsgCardPayload" => "MCP",       // v11 自建内容三 payload + 终局摘要（S5 补登——与 TagTable 既有前缀对齐）
                "MsgEffectPayload" => "MEP",
                "MsgAtomRowPayload" => "MAR",
                "MsgMatchSummary" => "MMS",
                "MsgError" => "MErr",
                "MsgJoinRoom" => "MJR",
                "MsgRoomState" => "MRS",
                "MsgRoomSeatInfo" => "MRSI",
                "MsgRoomSettings" => "MRSet",
                "MsgRoomReady" => "MRReady",
                "MsgLobbyHello" => "MLH",
                "MsgLobbyState" => "MLS",
                "MsgLobbyRoomInfo" => "MLRI",
                "MsgLobbyCreateRoom" => "MLCR",
                "MsgLobbyJoinRoom" => "MLJR",
                "MsgLobbyAddAi" => "MLAA",
                "MsgNetLogBatch" => "MNLB",       // v15 C 案战报行流（2026-11-11）
                "NetLogLine" => "NLL",            // v15 同上
                "MsgSeatSwap" => "MSSw",          // v16 多成员房间（2026-11-11）
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