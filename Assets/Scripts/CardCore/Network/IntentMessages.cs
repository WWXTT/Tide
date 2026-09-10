using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    // ============================================================
    // 上行 intent（客户端 → 服务器）。M1 协议定版 2026-09-10。
    // 服务器权威结算：intent 经 NetworkIntentApplier 分派到 GameActions（同步 bool 契约，
    // 失败=false 无副作用），拒绝语义透传 Error 帧。
    // 详见根目录 网络协议.md。
    // ============================================================

    /// <summary>出牌三路合一：FromZone=Hand（主阶段出牌）/ Graveyard（墓地视手牌）。
    /// 服务器按"栈非空且请求者持优先权"分派 PlayCardInResponse（响应出牌，来源限手牌），
    /// 否则走 PlayCard（主阶段+回合玩家门禁）。ModeIndex=抉择模式（宣言即公开）。</summary>
    [MemoryPackable]
    public partial class MsgIntentPlayCard
    {
        [MemoryPackOrder(TagTable.MsgIntentPlayCard_CardRuntimeId)]
        public uint CardRuntimeId;

        [MemoryPackOrder(TagTable.MsgIntentPlayCard_Targets)]
        public NetEntityRef[] Targets;

        [MemoryPackOrder(TagTable.MsgIntentPlayCard_FromZone)]
        public int FromZone;

        [MemoryPackOrder(TagTable.MsgIntentPlayCard_ModeIndex)]
        public int ModeIndex;
    }

    /// <summary>横置一张己方未横置地牌产 1 元素（颜色是玩家决策——ManaType 必填）。</summary>
    [MemoryPackable]
    public partial class MsgIntentTapForElement
    {
        [MemoryPackOrder(TagTable.MsgIntentTapForElement_CardRuntimeId)]
        public uint CardRuntimeId;

        [MemoryPackOrder(TagTable.MsgIntentTapForElement_ManaType)]
        public int ManaType;
    }

    /// <summary>放地进元素池（主阶段，不限次数，地牌槽上限约束）。</summary>
    [MemoryPackable]
    public partial class MsgIntentAddToElementPool
    {
        [MemoryPackOrder(TagTable.MsgIntentAddToElementPool_CardRuntimeId)]
        public uint CardRuntimeId;
    }

    /// <summary>宣告攻击（炉石式主阶段随时攻击）。Target 可为玩家（座位号表达）或单位。</summary>
    [MemoryPackable]
    public partial class MsgIntentDeclareAttack
    {
        [MemoryPackOrder(TagTable.MsgIntentDeclareAttack_Attacker)]
        public NetEntityRef Attacker;

        [MemoryPackOrder(TagTable.MsgIntentDeclareAttack_Target)]
        public NetEntityRef Target;
    }

    /// <summary>宣告格挡（预留：引擎 CombatSystem.DeclareBlock 有 API 零调用，协议先行占位）。</summary>
    [MemoryPackable]
    public partial class MsgIntentDeclareBlock
    {
        [MemoryPackOrder(TagTable.MsgIntentDeclareBlock_Blocker)]
        public NetEntityRef Blocker;

        [MemoryPackOrder(TagTable.MsgIntentDeclareBlock_Attacker)]
        public NetEntityRef Attacker;
    }

    /// <summary>激活启动式能力。EffectId = EffectDefinition.Id（稳定寻址键；
    /// 不是 EffectTag——那是 Description 的内容哈希指纹，仅序列化侧使用）。</summary>
    [MemoryPackable]
    public partial class MsgIntentActivateEffect
    {
        [MemoryPackOrder(TagTable.MsgIntentActivateEffect_SourceCardRuntimeId)]
        public uint SourceCardRuntimeId;

        [MemoryPackOrder(TagTable.MsgIntentActivateEffect_EffectId)]
        public string EffectId;

        [MemoryPackOrder(TagTable.MsgIntentActivateEffect_Targets)]
        public NetEntityRef[] Targets;

        [MemoryPackOrder(TagTable.MsgIntentActivateEffect_PaidBoost)]
        public int PaidBoost;
    }

    // ============================================================
    // 反问请求（服务器 → 特定玩家）与应答（玩家 → 服务器）。
    // 覆盖 TargetSelectionService 全部反问通道（实体选择/单选/卡位选择，皆索引语义）。
    // PositionPicker 预留：同步委托 M1 保持缺省行为（详见协议文档"已知限制"）。
    // ============================================================

    /// <summary>反问请求：候选集已由引擎筛选，客户端回传选中索引（MsgSelectResponse.Indices）。
    /// Labels 与 Candidates 同序；Candidates 为空表示纯选项反问（RequestOneIndexAsync）。</summary>
    [MemoryPackable]
    public partial class MsgSelectRequest
    {
        [MemoryPackOrder(TagTable.MSelR_RequestId)]
        public int RequestId;

        [MemoryPackOrder(TagTable.MSelR_ChooserSeat)]
        public int ChooserSeat;

        [MemoryPackOrder(TagTable.MSelR_Title)]
        public string Title;

        [MemoryPackOrder(TagTable.MSelR_Hint)]
        public string Hint;

        [MemoryPackOrder(TagTable.MSelR_AllowCancel)]
        public bool AllowCancel;

        [MemoryPackOrder(TagTable.MSelR_TimeoutSeconds)]
        public float TimeoutSeconds;

        [MemoryPackOrder(TagTable.MSelR_Min)]
        public int Min;

        [MemoryPackOrder(TagTable.MSelR_Max)]
        public int Max;

        [MemoryPackOrder(TagTable.MSelR_Labels)]
        public string[] Labels;

        [MemoryPackOrder(TagTable.MSelR_Candidates)]
        public NetEntityRef[] Candidates;
    }

    /// <summary>反问应答：Indices 指向 MsgSelectRequest.Labels/Candidates 的下标。
    /// 越界/超量由引擎侧既有 AutoSelect 兜底（TargetSelectionService 安全网）。</summary>
    [MemoryPackable]
    public partial class MsgSelectResponse
    {
        [MemoryPackOrder(TagTable.MSelP_RequestId)]
        public int RequestId;

        [MemoryPackOrder(TagTable.MSelP_Indices)]
        public int[] Indices;
    }

    // 注意：MsgIntentSkipStandby / MsgIntentPassPriority / MsgIntentEndTurn / MsgIntentConcede
    // 无载荷——空 Payload 即语义，不设 DTO 类（NetworkIntentApplier 直接分派）。
}
