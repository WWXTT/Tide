using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    // ============================================================
    // 开局握手（2026-09-22 定案，同日按"只核对卡组引用"口径收缩）：
    //
    // **不做全池对比**（不比对整个 Cards.json/Effects.json，无"共享牌库版本"概念）——
    // 只核对**卡组引用闭包**。2026-11-05 ID 冻结收口：三层 ID（原子行/效果/卡）已冻结，
    // **ID 命中 ≠ 内容一致**（冻结后同 ID 可载不同内容）——闭包内三层各带内容指纹核对：
    //   · 原子表行：行 ID 稳定（平衡层，改内容不改 ID）→ AtomicRowsHash（闭包行内容摘要）；
    //   · 效果层：EffectSlimDto.hash（内容指纹列）→ ContentSig；
    //   · 卡层：CardConfigEntry.hash（内容指纹列，含效果 ID 序列）→ ContentSig。
    // 以 NetDeckDigest 三段摘要收口。对手侧与未来涉及面更广的校验遵循**用到再核对**原则
    //（对局过程中按实际出现的 ID/行核对），开局一律不做全量对比。
    //
    // 流程（M2 会话层驱动；M1 定消息 + 校验逻辑，回环验证器覆盖）：
    //   1. 客户端 → 服务器 DeckSubmit(110)：卡组 cardIds + 本地闭包摘要（三段）。
    //   2. 服务器 NetMatchHandshake.ValidateDeckSubmit：卡表命中 / 卡组不重复 /
    //      效果-原子引用闭环 / 本地摘要与服务器按同一卡组计算的摘要比对——
    //      任一失败 = Error 帧(123) 拒绝，不再打出"缺卡对局"。
    //   3. 双座位通过后，服务器 → 各客户端 MatchManifest(120)：己方卡组回显 +
    //      对手仅数量（卡组构成隐藏）+ 服务器按该卡组计算的摘要——客户端核对后进局。
    // 详见根目录 网络协议.md §11。
    // ============================================================

    /// <summary>卡组引用闭包的内容摘要（三段）：AtomicRowsHash=闭包内全部 refId 对应原子表行
    /// 内容（排序后哈希）；ContentSig=闭包内全部卡（CardConfigEntry.hash）与引用效果
    ///（EffectSlimDto.hash）的指纹摘要（2026-11-05 ID 冻结收口，协议 v8——补上「冻结后同 ID
    /// 可载不同内容」的核对缺口：两端同 ID 不同内容在握手期即拒，不再打出内容分裂局）。
    /// 行数仅供拒绝文案诊断。TokenSig（2026-10-29 P9b 协议 v5）：衍生物区 + 槽绑定摘要
    ///（排序后哈希）——效果 ID 不含绑定，同卡表不同绑定的两套牌摘要必须不同。</summary>
    [MemoryPackable]
    public partial class NetDeckDigest
    {
        [MemoryPackOrder(TagTable.NDD_AtomicRowsHash)]
        public string AtomicRowsHash;

        [MemoryPackOrder(TagTable.NDD_AtomicRowCount)]
        public int AtomicRowCount;

        [MemoryPackOrder(TagTable.NDD_TokenSig)]
        public string TokenSig;

        [MemoryPackOrder(TagTable.NDD_ContentSig)]
        public string ContentSig;
    }

    /// <summary>衍生物槽绑定条目（P9b 协议 v5）：(卡ID, 效果序号) → 模板卡 ID。
    /// 效果序号 = 提交卡组该卡 CardData.Effects 下标（客户端绑定面板与服务器装载同序）。</summary>
    [MemoryPackable]
    public partial class MsgDeckTokenBinding
    {
        [MemoryPackOrder(TagTable.MTB_CardId)]
        public string CardId;

        [MemoryPackOrder(TagTable.MTB_EffectIndex)]
        public int EffectIndex;

        [MemoryPackOrder(TagTable.MTB_TemplateId)]
        public string TemplateId;
    }

    /// <summary>自建卡内容 payload（2026-11-09 协议 v11 自建内容跨端定案）：提交方卡组闭包中
    /// 「非基础池」的卡（出自玩家 Cards.json 或内容包）随 DeckSubmit 携带完整内容 JSON——
    /// 接收端装进瞬态覆盖层（TransientNetContent）参与校验/渲染，对局结束随终局摘要落
    /// 对局记录（EncounterRecord.cards）。卡 ID=冻结稳定 ID，内容核对走 Digest 三层指纹。</summary>
    [MemoryPackable]
    public partial class MsgCardPayload
    {
        [MemoryPackOrder(TagTable.MCP_CardId)]
        public string CardId;

        [MemoryPackOrder(TagTable.MCP_CardJson)]
        public string CardJson;
    }

    /// <summary>自建效果 payload：效果完整内容 JSON（效果 ID=内容哈希，同 ID 即同内容）。</summary>
    [MemoryPackable]
    public partial class MsgEffectPayload
    {
        [MemoryPackOrder(TagTable.MEP_EffectId)]
        public string EffectId;

        [MemoryPackOrder(TagTable.MEP_EffectJson)]
        public string EffectJson;
    }

    /// <summary>内容包原子行 payload：效果闭包引用的、基础原子表之外的词条行（RowJson=原子行 JSON）。
    /// 接收端 add-if-missing 进瞬态覆盖层；「导入对手卡组」时同语义入本地。</summary>
    [MemoryPackable]
    public partial class MsgAtomRowPayload
    {
        [MemoryPackOrder(TagTable.MAR_RefId)]
        public string RefId;

        [MemoryPackOrder(TagTable.MAR_RowJson)]
        public string RowJson;
    }

    /// <summary>上行：卡组提交（对局开始前）。CardIds 为 Cards.json 卡 ID（2026-11-05 起为冻结
    /// 稳定 ID，非内容哈希——内容核对走 Digest）；Digest 为提交方本地按同一卡组计算的内容摘要
    ///（三段），服务器与自身计算结果比对。
    /// SkillCardId（2026-10-07 卡牌化）：英雄技能标记卡 ID——服务器宽松校验（须 ∈ CardIds
    /// 且为合格技能结界，否则按无技能开局），不参与 Digest 摘要口径。
    /// TokenCardIds/TokenBindings（2026-10-29 P9b 协议 v5）：衍生物区与槽绑定——参与 Digest
    /// 摘要（TokenSig）并由服务器校验（槽须绑定、绑定值 ∈ 区、模板类型合规；详见
    /// NetMatchHandshake.ValidateDeckSubmit）。随机预组直提时两块为空数组（模板仍在 CardIds 内）。
    /// Added（2026-11-09 协议 v11 自建内容跨端）：卡组闭包中非基础池的卡/效果/原子行完整内容——
    /// 服务器装瞬态覆盖层后校验（此前对端缺卡直接拒局，自建卡无法跨玩家对战）；对局结束随
    /// 终局摘要回传双方落对局记录。字段为 null/空数组=无自建内容（存量行为）。</summary>
    [MemoryPackable]
    public partial class MsgDeckSubmit
    {
        [MemoryPackOrder(TagTable.MDS_DeckName)]
        public string DeckName;

        [MemoryPackOrder(TagTable.MDS_CardIds)]
        public string[] CardIds;

        [MemoryPackOrder(TagTable.MDS_Digest)]
        public NetDeckDigest Digest;

        [MemoryPackOrder(TagTable.MDS_SkillCardId)]
        public string SkillCardId;

        [MemoryPackOrder(TagTable.MDS_TokenCardIds)]
        public string[] TokenCardIds;

        [MemoryPackOrder(TagTable.MDS_TokenBindings)]
        public MsgDeckTokenBinding[] TokenBindings;

        [MemoryPackOrder(TagTable.MDS_AddedCards)]
        public MsgCardPayload[] AddedCards;

        [MemoryPackOrder(TagTable.MDS_AddedEffects)]
        public MsgEffectPayload[] AddedEffects;

        [MemoryPackOrder(TagTable.MDS_AddedRows)]
        public MsgAtomRowPayload[] AddedRows;
    }

    /// <summary>下行：对局清单（双座位 DeckSubmit 校验通过后下发）。
    /// OwnCardIds=己方卡组回显（与提交序一致）；对手只给数量（卡组构成隐藏）；
    /// OwnDeckDigest=服务器按该座位卡组计算的原子行摘要，客户端与本地计算比对。
    /// OpponentCards/Effects/Rows（2026-11-09 协议 v11）：对手卡组闭包中非基础池的卡/效果/原子行
    /// 内容——供客人端渲染对手自建卡（快照按 CardId 查本地表会缺行）；**只传内容定义不传卡组
    /// 构成**，局内隐藏口径不变（数量照旧，多张同名卡不可推断）。终局后完整卡组经 MsgMatchSummary 下发。</summary>
    [MemoryPackable]
    public partial class MsgMatchManifest
    {
        [MemoryPackOrder(TagTable.MMM_OwnSeat)]
        public int OwnSeat;

        [MemoryPackOrder(TagTable.MMM_OwnCardIds)]
        public string[] OwnCardIds;

        [MemoryPackOrder(TagTable.MMM_OpponentCardCount)]
        public int OpponentCardCount;

        [MemoryPackOrder(TagTable.MMM_OwnDeckDigest)]
        public NetDeckDigest OwnDeckDigest;

        [MemoryPackOrder(TagTable.MMM_OpponentCards)]
        public MsgCardPayload[] OpponentCards;

        [MemoryPackOrder(TagTable.MMM_OpponentEffects)]
        public MsgEffectPayload[] OpponentEffects;

        [MemoryPackOrder(TagTable.MMM_OpponentRows)]
        public MsgAtomRowPayload[] OpponentRows;

        /// <summary>对手卡组费用分布（S7b 2026-11-10 掷币段展示）：9 桶=费用 1..9+（TotalCost
        /// 四舍五入、&lt;1 归 1、&gt;9 归 9）；对手未提交（预组替落）=空数组——客户端按全 0 桶渲染。</summary>
        [MemoryPackOrder(TagTable.MMM_OpponentCostHistogram)]
        public int[] OpponentCostHistogram;
    }

    /// <summary>下行：终局摘要（2026-11-09 协议 v11 对局记录定案）：对局结束时双座位各收一份，
    /// 视角=收件人的对手——对手昵称/开位 ID/完整卡组（名字/主列表/技能/衍生物区+槽绑定——
    /// 对局已结束，局内「卡组构成隐藏」口径不再适用）+对手自建内容 payload+收件人胜负。
    /// 客户端收到后落 EncounterRecord（含战报导出）并随云存档同步；「导入对手卡组」在对局
    /// 记录屏操作，默认不进玩家库。对 AI 局不下发（无对手内容可记）。</summary>
    [MemoryPackable]
    public partial class MsgMatchSummary
    {
        [MemoryPackOrder(TagTable.MMS_MatchId)]
        public string MatchId;

        [MemoryPackOrder(TagTable.MMS_FinishedAtUnix)]
        public long FinishedAtUnix;

        [MemoryPackOrder(TagTable.MMS_OpponentName)]
        public string OpponentName;

        [MemoryPackOrder(TagTable.MMS_OpponentOpenId)]
        public string OpponentOpenId;

        [MemoryPackOrder(TagTable.MMS_Win)]
        public bool Win; // 收件人视角：true=收件人获胜

        [MemoryPackOrder(TagTable.MMS_OwnDeckName)]
        public string OwnDeckName;

        [MemoryPackOrder(TagTable.MMS_OpponentDeckName)]
        public string OpponentDeckName;

        [MemoryPackOrder(TagTable.MMS_OpponentCardIds)]
        public string[] OpponentCardIds;

        [MemoryPackOrder(TagTable.MMS_OpponentSkillCardId)]
        public string OpponentSkillCardId;

        [MemoryPackOrder(TagTable.MMS_OpponentTokenCardIds)]
        public string[] OpponentTokenCardIds;

        [MemoryPackOrder(TagTable.MMS_OpponentTokenBindings)]
        public MsgDeckTokenBinding[] OpponentTokenBindings;

        [MemoryPackOrder(TagTable.MMS_Cards)]
        public MsgCardPayload[] Cards;

        [MemoryPackOrder(TagTable.MMS_Effects)]
        public MsgEffectPayload[] Effects;

        [MemoryPackOrder(TagTable.MMS_AtomRows)]
        public MsgAtomRowPayload[] AtomRows;

        [MemoryPackOrder(TagTable.MMS_Log)]
        public string Log; // 战报 markdown 全文（服务器引擎事件流渲染——客人端无本地引擎，随摘要携带）

        /// <summary>排位局标记（S8）：客户端只对 Ranked 终局按卡组内容指纹记账（DeckRatingStore）。</summary>
        [MemoryPackOrder(TagTable.MMS_Ranked)]
        public bool Ranked;
    }
}
