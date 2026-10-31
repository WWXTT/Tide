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
    /// 否则走 PlayCard（主阶段+回合玩家门禁）。ModeIndex=抉择模式（宣言即公开）。
    /// AwakenCast（2026-10-29 P6 苏醒改版，协议 v3 新增）：苏醒施放——仅主阶段 PlayCard
    /// 分支生效（响应出牌忽略），生物免净费灰份额转入场沉睡层数（P6c：只免灰，彩色含绿照付）。
    /// 永久物落格（2026-10-28）：快照不承载棋盘坐标（位置权威=客户端表现层）——选格钉在
    /// 客户端 NetCellPins（BuildNet 消费），无需协议带格。</summary>
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

        [MemoryPackOrder(TagTable.MsgIntentPlayCard_AwakenCast)]
        public bool AwakenCast;
    }

    // MsgIntentTapForElement 已删（2026-10-11 现取现用：无手动横置意图）——枚举值 101 留洞

    /// <summary>放地进元素池（主阶段，不限次数，各自地牌槽上限约束）。
    /// 2026-10-11 锁色定案：ModeIndex 复用为锁色选择（-1=缺省主色，否则=ManaType 枚举值）。</summary>
    [MemoryPackable]
    public partial class MsgIntentAddToElementPool
    {
        [MemoryPackOrder(TagTable.MsgIntentAddToElementPool_CardRuntimeId)]
        public uint CardRuntimeId;

        [MemoryPackOrder(TagTable.MsgIntentAddToElementPool_ModeIndex)]
        public int ModeIndex;
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

    /// <summary>激活主动能力。EffectId = EffectDefinition.Id（稳定寻址键；
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

    /// <summary>发动英雄技能（2026-10-21 协议补课：技能格 2026-10-15 加入，此后协议首个新 intent）。
    /// 守卫与效果解析全在服务器侧 HeroSkillSystem.ActivateAsync（回合玩家+主阶段+每回合横置闸+
    /// 沉默+CanActivate 预检）；effectIndex=多效果技能的抉择（单效果恒 0，UI 弹窗择定后随 intent 公开）。</summary>
    [MemoryPackable]
    public partial class MsgIntentActivateHeroSkill
    {
        [MemoryPackOrder(TagTable.MsgIntentActivateHeroSkill_EffectIndex)]
        public int EffectIndex;
    }

    /// <summary>充能（2026-10-29 P7 建造入协议 v4；2026-10-31 P12 建造→充能只换语义，消息名/号不动）：
    /// 主阶段对手牌「带充能效果的卡」逐色充能——服务器 GameActions.ActivateCharge（速度 0 主动效果
    /// 入栈，结算时现付 1 点 Color 色元素 + 放 1 层同色费用降低指示物 + 展示这张卡；上限=该色
    /// 代价抵扣前卡费；不限次数/回合，跨回合保留，换区清除、元素不退）。Color=ManaType 枚举值
    ///（四色；任意位服务器拒绝）。可用性单源 GameActions.CanCharge——UI 置灰/AI 枚举/引擎终审同口径。</summary>
    [MemoryPackable]
    public partial class MsgIntentBuildCard
    {
        [MemoryPackOrder(TagTable.MsgIntentBuildCard_CardRuntimeId)]
        public uint CardRuntimeId;

        [MemoryPackOrder(TagTable.MsgIntentBuildCard_Color)]
        public int Color;
    }

    // ============================================================
    // 反问请求（服务器 → 特定玩家）与应答（玩家 → 服务器）。
    // 覆盖 TargetSelectionService 全部反问通道（实体选择/单选/卡位选择，皆索引语义）。
    // PositionPicker 预留：同步委托 M1 保持缺省行为（详见协议文档"已知限制"）。
    // ============================================================

    /// <summary>反问请求：候选集已由引擎筛选，客户端回传选中索引（MsgSelectResponse.Indices）。
    /// 实体反问（Candidates 非空）Labels 恒空——卡名客户端按 CardId 查表渲染（2026-09-22 线上去文本）；
    /// Candidates 为空表示纯选项反问（RequestOneIndexAsync），此时 Labels 是语义载荷（选项文本）。</summary>
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
