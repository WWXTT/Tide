// 此文件由 Tools/刷新属性排序（新增序列化字段时使用）自动生成，请勿手动修改
// 如需添加新属性，请修改 RefreshMemoryPackOrder.cs 中的 GetTagDefinitions()
// 生成口径（2026-11-11 起＝保值生成）：已有常量沿用现表现值（整行含注释原样保留），仅新字段
// 按 Murmur32("类.属性", 0x53796E67) & 0x7FFFFFFF 取号；末尾「冻结值」段＝历史手算批，重算
// 它们＝改 MemoryPackOrder＝改已上线线格式，禁止（工具守门会拦）。

namespace CardCore.Serialization
{
    public static class TagTable
    {

        // ---- CardData ----
        public const int CardData_ID = 1411655954;
        public const int CardData_Supertype = 1626364500;
        public const int CardData_CardName = 1077727027;
        public const int CardData_Illustration = 1354286104;
        public const int CardData_Life = 1970882503;
        public const int CardData_Power = 1948219662;
        public const int CardData_Cost = 629661927;
        public const int CardData_Effects = 872057058;
        public const int CardData_CreationTicks = 262055788;
        public const int CardData_Tags = 1046472445;
        public const int CardData_Keywords = 136987752;

        // ---- EffectData ----
        public const int EffectData_Abbreviation = 2081364203;
        public const int EffectData_Initiative = 1768077001;
        public const int EffectData_Parameters = 646202389;
        public const int EffectData_Speed = 822306448;
        public const int EffectData_ManaType = 1186904746;
        public const int EffectData_Description = 32483303;
        public const int EffectData_EffectTag = 142036358;
        public const int EffectData_DisplayName = 702727069;
        public const int EffectData_IsOptional = 1782308357;
        public const int EffectData_Duration = 41851234;
        public const int EffectData_Tags = 1179894075;
        public const int EffectData_Costs = 1734743378;
        public const int EffectData_ActivationConditions = 1900780975;
        public const int EffectData_TriggerConditions = 1513029761;
        public const int EffectData_Steps = 2041515697;

        // ---- ActivationConditionData ----
        public const int ActivationConditionData_Type = 1100210516;
        public const int ActivationConditionData_Value = 1932353973;
        public const int ActivationConditionData_Value2 = 1768808773;
        public const int ActivationConditionData_StringValue = 1142407955;
        public const int ActivationConditionData_Negate = 1185546117;

        // ---- EffectCostEntry ----
        public const int EffectCostEntry_CostType = 195771993;
        public const int EffectCostEntry_Value = 1722419697;
        public const int EffectCostEntry_ManaType = 1936670076;
        public const int EffectCostEntry_TurnDuration = 2020030928;

        // ---- EffectStepData ----
        public const int EffectStepData_Kind = 1310020535;
        public const int EffectStepData_Atomic = 1312599287;
        public const int EffectStepData_Condition = 912000955;
        public const int EffectStepData_ThenSteps = 1582440487;
        public const int EffectStepData_ElseSteps = 885670630;
        public const int EffectStepData_ConditionId = 1271963687;
        public const int EffectStepData_ConditionParam = 1620806380;
        public const int EffectStepData_ConditionStringParam = 914519353;
        public const int EffectStepData_Choices = 1595595327;

        // ---- EffectChoiceData ----
        public const int EffectChoiceData_Label = 2001727622;
        public const int EffectChoiceData_Steps = 1201939277;

        // ---- CostEntryDTO ----
        public const int CostEntryDTO_ManaType = 724021260;
        public const int CostEntryDTO_Value = 1598529736;

        // ---- RuntimeCardState ----
        public const int RCS_ID = 244684840;
        public const int RCS_Power = 976050218;
        public const int RCS_Life = 1969295376;
        public const int RCS_MaxLife = 922071213;
        public const int RCS_BaseCost = 1630071019;
        public const int RCS_CostModifier = 1414743304;
        public const int RCS_Armor = 683926072;
        public const int RCS_DamagePrevention = 1221822787;
        public const int RCS_IsTapped = 1985384084;
        public const int RCS_IsFrozen = 1428218013;
        public const int RCS_IsNegated = 2118117958;
        public const int RCS_IsNullified = 364844435;
        public const int RCS_Zone = 204131997;
        public const int RCS_Keywords = 804741910;
        public const int RCS_Counters = 292357109;

        // ---- CounterEntryDTO ----
        public const int CounterEntryDTO_Key = 524926912;
        public const int CounterEntryDTO_Value = 2046667806;

        // ---- ManaEntryDTO ----
        public const int ManaEntryDTO_ManaType = 694914454;
        public const int ManaEntryDTO_Value = 1933258115;

        // ---- AtomicEffectInstance ----
        public const int AEI_Type = 42652807;
        public const int AEI_Value = 1129914700;
        public const int AEI_Value2 = 408156925;
        public const int AEI_StringValue = 1334908506;
        public const int AEI_ManaTypeParam = 1791664335;
        public const int AEI_ZoneParam = 1641778537;
        public const int AEI_Duration = 1966912189;
        public const int AEI_TargetTypeOverride = 1454135056;
        public const int AEI_TargetFilterOverride = 1665166535;
        public const int AEI_TargetCountOverride = 1101957053;
        public const int AEI_DynamicTargetCount = 1960296747;
        public const int AEI_TargetKinds = 1052954153;
        public const int AEI_ManaList = 902140877;
        public const int AEI_Domain = 1316499356;

        // ---- SerializableAtomicEffectEntry ----
        public const int AEI_Amplitude = 1647853197;

        // ---- SerializableManaAmount ----
        public const int SerializableManaAmount_manaType = 549926884;
        public const int SerializableManaAmount_amount = 433477152;

        // ---- CostInstance ----
        public const int CI_Type = 57119624;
        public const int CI_Value = 832738714;
        public const int CI_ManaType = 1850397482;

        // ---- ActivationCondition ----
        public const int AC_Type = 1310782459;
        public const int AC_Value = 1634179753;
        public const int AC_Value2 = 21033891;

        // ---- EffectDefinition ----
        public const int ED_Id = 548492777;
        public const int ED_DisplayName = 1300219100;
        public const int ED_Description = 1631759857;
        public const int ED_BaseSpeed = 1508539253;
        public const int ED_ActivationType = 306060205;
        public const int ED_TriggerTiming = 984178182;
        public const int ED_IsOptional = 640126085;
        public const int ED_Duration = 1013231263;
        public const int ED_Effects = 973707135;
        public const int ED_Costs = 531244005;
        public const int ED_Tags = 79464002;
        public const int ED_SourceCardId = 863828489;
        public const int ED_TargetType = 953648607;
        public const int ED_EffectTag = 550171593;
        public const int ED_ActivationConditions = 685342510;
        public const int ED_TriggerConditions = 1516982108;

        // ---- NetworkMessage ----
        public const int NM_MessageType = 790136053;
        public const int NM_SequenceId = 885822028;
        public const int NM_Payload = 1388875668;
        public const int NM_Timestamp = 1097508369;

        // ---- MsgPlayCard ----
        public const int MPC_CardID = 1094377983;
        public const int MPC_FromZone = 2059120564;
        public const int MPC_ToZone = 2126325883;
        public const int MPC_ChosenManaType = 1841415904;

        // ---- MsgActivateEffect ----
        public const int MAE_SourceCardID = 784640788;
        public const int MAE_EffectTag = 926061686;
        public const int MAE_ActivationSpeed = 1788861467;
        public const int MAE_TargetIDs = 502958227;

        // ---- MsgGameStateSync ----
        public const int MGSS_CurrentTurn = 2035353205;
        public const int MGSS_CurrentPhase = 1337617274;
        public const int MGSS_Players = 1169411298;
        public const int MGSS_BattlefieldCards = 574488751;
        public const int MGSS_Stack = 272663845;

        // ---- PlayerState ----
        public const int PS_Name = 1780016741;
        public const int PS_Life = 1638819233;
        public const int PS_MaxHealth = 459554504;
        public const int PS_DeckCount = 1940827978;
        public const int PS_HandCount = 38995153;
        public const int PS_Seat = 1342382195;
        public const int PS_IsAI = 1076858202;
        public const int PS_FatigueCount = 2096412322;
        public const int PS_LandCap = 1466244150;
        public const int PS_ElementBank = 3481387;
        public const int PS_GraveyardCount = 1652629631;
        public const int PS_ExileCount = 1217138263;
        public const int PS_Counters = 497079627;

        // ---- NetworkMessage ----
        public const int NM_ProtocolVersion = 965419062;

        // ---- NetEntityRef ----
        public const int NER_RuntimeId = 1758868744;
        public const int NER_Seat = 624100802;
        public const int NER_IsPlayer = 1709381960;
        public const int NER_CardId = 124142063;

        // ---- NetEvent ----
        public const int NE_EventType = 554855030;
        public const int NE_EventId = 1713183649;
        public const int NE_TurnNumber = 1233211344;
        public const int NE_Params = 801770002;

        // ---- NetParam ----
        public const int NP_FieldName = 1421498712;
        public const int NP_Kind = 1212109321;
        public const int NP_IntValue = 176539587;
        public const int NP_FloatValue = 1743336148;
        public const int NP_StringValue = 1140726418;
        public const int NP_EntityRefs = 384621897;

        // ---- MsgGameStateSync ----
        public const int MGSS_ViewerSeat = 1961466223;
        public const int MGSS_ActiveSeat = 406303112;
        public const int MGSS_PrioritySeat = 329487059;
        public const int MGSS_ZoneCards = 1095299921;
        public const int MGSS_Hands = 1371201158;
        public const int MGSS_StackV2 = 1464117104;
        public const int MGSS_RevealedZoneCards = 2054002893;
        public const int MGSS_TurnSecondsLeft = 224912618;   // v14 服务器权威回合计时（与 RefreshMemoryPackOrder 同口径：Murmur32("MsgGameStateSync.TurnSecondsLeft", 0x53796E67) & 0x7FFFFFFF）

        // ---- StackItemDTO ----
        public const int SID_Source = 887547856;
        public const int SID_IsCardCast = 6974238;
        public const int SID_EffectId = 1282903965;
        public const int SID_ModeIndex = 982635335;
        public const int SID_Targets = 1694824900;
        public const int SID_ActivationSpeed = 548007896;
        public const int SID_StackObjectType = 554634774;
        public const int SID_References = 192158638;

        // ---- NetZoneCards ----
        public const int NZC_Seat = 687121728;
        public const int NZC_Zone = 495592385;
        public const int NZC_Cards = 1243020982;

        // ---- CardHandInfo ----
        public const int CHI_Seat = 715909855;
        public const int CHI_Count = 1339118341;
        public const int CHI_OwnRuntimeIds = 1399075956;

        // ---- MsgIntentPlayCard ----
        public const int MsgIntentPlayCard_CardRuntimeId = 1178557754;
        public const int MsgIntentPlayCard_Targets = 1450138392;
        public const int MsgIntentPlayCard_FromZone = 933788219;
        public const int MsgIntentPlayCard_ModeIndex = 1969366828;
        public const int MsgIntentPlayCard_References = 271658474;
        public const int MsgIntentPlayCard_CellX = 591000053;
        public const int MsgIntentPlayCard_CellZ = 986477751;

        // ---- MsgIntentBuildCard ----
        public const int MsgIntentBuildCard_CardRuntimeId = 77691148;
        public const int MsgIntentBuildCard_Color = 2040733387;

        // ---- MsgIntentAddToElementPool ----
        public const int MsgIntentAddToElementPool_CardRuntimeId = 713270002;
        public const int MsgIntentAddToElementPool_ModeIndex = 408308435;

        // ---- MsgIntentDeclareAttack ----
        public const int MsgIntentDeclareAttack_Attacker = 439144961;
        public const int MsgIntentDeclareAttack_Target = 1630252404;

        // ---- MsgIntentDeclareBlock ----
        public const int MsgIntentDeclareBlock_Blocker = 207940836;
        public const int MsgIntentDeclareBlock_Attacker = 807317854;

        // ---- MsgIntentActivateEffect ----
        public const int MsgIntentActivateEffect_SourceCardRuntimeId = 919179242;
        public const int MsgIntentActivateEffect_EffectId = 976844325;
        public const int MsgIntentActivateEffect_Targets = 795110869;
        public const int MsgIntentActivateEffect_PaidBoost = 1309359023;
        public const int MsgIntentActivateEffect_References = 16323924;

        // ---- MsgIntentActivateHeroSkill ----
        public const int MsgIntentActivateHeroSkill_EffectIndex = 1738103103;

        // ---- MsgSelectRequest ----
        public const int MSelR_RequestId = 1404564517;
        public const int MSelR_ChooserSeat = 499080371;
        public const int MSelR_Title = 1588971438;
        public const int MSelR_Hint = 1371349148;
        public const int MSelR_AllowCancel = 1480366595;
        public const int MSelR_TimeoutSeconds = 143938716;
        public const int MSelR_Min = 457187271;
        public const int MSelR_Max = 1324456978;
        public const int MSelR_Labels = 108139152;
        public const int MSelR_Candidates = 1387773462;
        public const int MSelR_SpecKey = 317799922;

        // ---- MsgSelectResponse ----
        public const int MSelP_RequestId = 1596262577;
        public const int MSelP_Indices = 2029117393;

        // ---- MsgTextRequest ----
        public const int MTxtR_RequestId = 34353602;
        public const int MTxtR_ChooserSeat = 532063247;
        public const int MTxtR_Title = 1682907019;
        public const int MTxtR_Default = 698403013;
        public const int MTxtR_MaxLen = 1059541441;
        public const int MTxtR_TimeoutSeconds = 718083082;

        // ---- MsgTextResponse ----
        public const int MTxtP_RequestId = 898162932;
        public const int MTxtP_Text = 1765521786;

        // ---- MsgNetEventBatch ----
        public const int MNEB_Events = 1087901504;

        // ---- RuntimeCardState ----
        public const int RCS_RuntimeId = 1598792242;
        public const int RCS_ControllerSeat = 1105098362;
        public const int RCS_BuildPaid = 81947996;
        public const int RCS_LockedColor = 1684315828;
        public const int RCS_DelayedDeath = 747753030;
        public const int RCS_CellX = 1722965036;
        public const int RCS_CellZ = 1859498192;

        // ---- NetDeckDigest ----
        public const int NDD_AtomicRowsHash = 1017044835;
        public const int NDD_AtomicRowCount = 1928083177;
        public const int NDD_TokenSig = 571524600;
        public const int NDD_ContentSig = 1755961784;

        // ---- MsgDeckSubmit ----
        public const int MDS_DeckName = 1095059584;
        public const int MDS_CardIds = 339936047;
        public const int MDS_Digest = 216745245;
        public const int MDS_SkillCardId = 1834786867;
        public const int MDS_TokenCardIds = 1248031784;
        public const int MDS_TokenBindings = 1711324094;
        public const int MDS_AddedCards = 1607500155;
        public const int MDS_AddedEffects = 2097256202;
        public const int MDS_AddedRows = -267714343;

        // ---- MsgDeckTokenBinding ----
        public const int MTB_CardId = 1865613195;
        public const int MTB_EffectIndex = 865932852;
        public const int MTB_TemplateId = 1023722011;

        // ---- MsgMatchManifest ----
        public const int MMM_OwnSeat = 2014680492;
        public const int MMM_OwnCardIds = 1186475388;
        public const int MMM_OpponentCardCount = 750558643;
        public const int MMM_OwnDeckDigest = 1791650654;
        public const int MMM_OpponentCards = -784281127;   // v11 对手自建内容 payload（渲染用；卡组构成仍隐藏）
        public const int MMM_OpponentEffects = 132756444;
        public const int MMM_OpponentRows = -576926193;
        public const int MMM_OpponentCostHistogram = -1156878767; // S7b：对手费用直方图（DSH 同源 Hash32 实算抄入·零撞号）

        // ---- MsgCardPayload ----
        public const int MCP_CardId = 562067335;
        public const int MCP_CardJson = -525724844;

        // ---- MsgEffectPayload ----
        public const int MEP_EffectId = -1167984450;
        public const int MEP_EffectJson = -1046722447;

        // ---- MsgAtomRowPayload ----
        public const int MAR_RefId = 673218868;
        public const int MAR_RowJson = 1081615994;

        // ---- MsgMatchSummary ----
        public const int MMS_MatchId = 517542727;
        public const int MMS_FinishedAtUnix = -1388488232;
        public const int MMS_OpponentName = -1165407645;
        public const int MMS_OpponentOpenId = -2030439723;
        public const int MMS_Win = 514214013;
        public const int MMS_OwnDeckName = -89498147;
        public const int MMS_OpponentDeckName = 1253992418;
        public const int MMS_OpponentCardIds = -444598832;
        public const int MMS_OpponentSkillCardId = 892439674;
        public const int MMS_OpponentTokenCardIds = -548054461;
        public const int MMS_OpponentTokenBindings = -1463178445;
        public const int MMS_Cards = 1544258080;
        public const int MMS_Effects = -2090101919;
        public const int MMS_AtomRows = 1250763953;
        public const int MMS_Log = 219159277;    // 战报 markdown 全文（服务器渲染——客人端无本地引擎）
        public const int MMS_Ranked = 1261688995; // S8：排位局记账标记（同上配方实算）

        // ---- MsgError ----
        public const int MErr_Reason = 259789666;
        public const int MErr_Context = 1082803286;
        public const int MErr_Channel = 379140953; // v17：拒绝帧归属通道（NetworkChannel；0=Room）

        // ---- MsgJoinRoom ----
        public const int MJR_RoomId = 883321649;
        public const int MJR_WantSeat = 1047967055;
        public const int MJR_AsSpectator = 1232633580;
        public const int MJR_Nickname = 841072657;
        public const int MJR_OpenId = 981065552;   // v11：TapTap openId（对局记录对手身份；局域网空）
        public const int MJR_IsAi = 1542445315;    // v11：AI 填位标记（对局记录跳过 AI 局）
        public const int MJR_Password = 1021396469; // S5：房间密码（空=无密码）
        public const int MJR_DeckName = 1371443254; // S5：进房上报的选中卡组名
        public const int MJR_Ranked = 653408864;          // S8：排位房进房标记
        public const int MJR_DeckFingerprint = 605637085; // S8：卡组内容指纹（排位匹配/记账键）
        public const int MJR_WinRate = 1410631775;        // S8：胜率千分比（-1=无战绩）
        public const int MJR_IsHost = 1596988382;         // v17：房主自报（P2P 房主进程回环入座）

        // ---- MsgRankedAck ----
        public const int MRA_Accepted = 961448421; // S8：排位接受应答（同上配方实算）

        // ---- MsgRoomState ----
        public const int MRS_RoomId = 2098723049;
        public const int MRS_Phase = 450102664;
        public const int MRS_Players = 1958229836;
        public const int MRS_SpectatorCount = 1394321412;
        public const int MRS_FirstSeatThisMatch = 1986740545;
        public const int MRS_FirstMoveMode = 945073816;
        public const int MRS_ShuffleMode = 796602489;
        public const int MRS_GuestReady = 1593869558;
        public const int MRS_DeckPickSecondsLeft = 1036775331;
        public const int MRS_Seed = -210878657; // v13：本局随机种子（开局定死回显；0=未开局。手算全量强转口径）
        public const int MRS_HostName = 1059879627; // v17：房主昵称（空=无房主；房主可落任意椅/观战）

        // ---- MsgRoomSeatInfo ----
        public const int MRSI_Seat = 914327639;
        public const int MRSI_Nickname = 807757571;
        public const int MRSI_Connected = 531597507;
        public const int MRSI_DeckName = 1935616342; // S5：座位卡组名（同源哈希；零撞号已核）
        public const int MRSI_Ready = 1336235016;    // v17：逐座准备态（非房主座位才可置 true）
        public const int MRSI_IsAi = 617273901;      // v17：AI 填位标记（座位卡「AI」）
        public const int MRSI_IsHost = 870912976;    // v17：本座位是否房主（房主观战=两座皆 false）

        // ---- MsgRoomSettings ----
        public const int MRSet_FirstMoveMode = 142664050;
        public const int MRSet_ShuffleMode = 535227049;
        public const int MRSet_Seed = -191934453; // v13：房主钉种子（0=随机。手算全量强转口径）

        // ---- MsgRoomReady ----
        public const int MRReady_Ready = 1268044497;

        // ---- MsgLobbyHello ----
        public const int MLH_Nickname = 1532381561;

        // ---- MsgLobbyState ----
        public const int MLS_Rooms = 1570248777;
        public const int MLS_QueuedCount = 124003943;

        // ---- MsgLobbyRoomInfo ----
        public const int MLRI_RoomId = 1765793158;
        public const int MLRI_Name = 1340740691;
        public const int MLRI_Phase = 96593306;
        public const int MLRI_PlayerCount = 1334109926;
        public const int MLRI_Nicknames = 304988505;
        public const int MLRI_SpectatorCount = 207819560;

        // ---- MsgLobbyCreateRoom ----
        public const int MLCR_RoomName = 921235863;

        // ---- MsgLobbyJoinRoom ----
        public const int MLJR_RoomId = 815627303;

        // ---- MsgLobbyAddAi ----
        public const int MLAA_RoomId = 97705085;
        public const int MLAA_Nickname = 1153192151;

        // ---- MsgNetLogBatch ----
        public const int MNLB_Lines = 191093110;  // 战报行批（NetLogLine[]）

        // ---- NetLogLine ----
        public const int NLL_Kind = 778670979;    // 语义色编码（MatchLogLineKind 0..6）
        public const int NLL_Flags = 194371587;   // 时序旗标（MatchLogLineFlags）
        public const int NLL_EventId = 660569595; // 源事件锚（挂起放行会乱序，勿作去重键）
        public const int NLL_Text = 1583350492;   // 服务端渲染好的最终文本

        // ---- MsgSeatSwap ----
        public const int MSSw_ToSpectator = 2064321602; // 座位↔观战切换方向（true=转观战释放椅位）

        // ---- 冻结值（非公式推导，禁止重算：已上线线格式契约）----
        // 来源：全量强转口径 / v11 早期手算配方；现值即协议布局。改值＝改字段序列化顺序。
        // MDS_AddedCards = 1607500155
        // MDS_AddedEffects = 2097256202
        // MDS_AddedRows = -267714343
        // MMM_OpponentCards = -784281127
        // MMM_OpponentEffects = 132756444
        // MMM_OpponentRows = -576926193
        // MMM_OpponentCostHistogram = -1156878767
        // MCP_CardId = 562067335
        // MCP_CardJson = -525724844
        // MEP_EffectId = -1167984450
        // MEP_EffectJson = -1046722447
        // MAR_RefId = 673218868
        // MAR_RowJson = 1081615994
        // MMS_MatchId = 517542727
        // MMS_FinishedAtUnix = -1388488232
        // MMS_OpponentName = -1165407645
        // MMS_OpponentOpenId = -2030439723
        // MMS_Win = 514214013
        // MMS_OwnDeckName = -89498147
        // MMS_OpponentDeckName = 1253992418
        // MMS_OpponentCardIds = -444598832
        // MMS_OpponentSkillCardId = 892439674
        // MMS_OpponentTokenCardIds = -548054461
        // MMS_OpponentTokenBindings = -1463178445
        // MMS_Cards = 1544258080
        // MMS_Effects = -2090101919
        // MMS_AtomRows = 1250763953
        // MMS_Log = 219159277
        // MJR_OpenId = 981065552
        // MJR_IsAi = 1542445315
        // MRS_Seed = -210878657
        // MRSet_Seed = -191934453

        // 共 326 个标签（保值生成：294 条公式值 + 32 条冻结值）
    }
}
