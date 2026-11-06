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

    /// <summary>上行：卡组提交（对局开始前）。CardIds 为 Cards.json 卡 ID（2026-11-05 起为冻结
    /// 稳定 ID，非内容哈希——内容核对走 Digest）；Digest 为提交方本地按同一卡组计算的内容摘要
    ///（三段），服务器与自身计算结果比对。
    /// SkillCardId（2026-10-07 卡牌化）：英雄技能标记卡 ID——服务器宽松校验（须 ∈ CardIds
    /// 且为合格技能结界，否则按无技能开局），不参与 Digest 摘要口径。
    /// TokenCardIds/TokenBindings（2026-10-29 P9b 协议 v5）：衍生物区与槽绑定——参与 Digest
    /// 摘要（TokenSig）并由服务器校验（槽须绑定、绑定值 ∈ 区、模板类型合规；详见
    /// NetMatchHandshake.ValidateDeckSubmit）。随机预组直提时两块为空数组（模板仍在 CardIds 内）。</summary>
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
    }

    /// <summary>下行：对局清单（双座位 DeckSubmit 校验通过后下发）。
    /// OwnCardIds=己方卡组回显（与提交序一致）；对手只给数量（卡组构成隐藏）；
    /// OwnDeckDigest=服务器按该座位卡组计算的原子行摘要，客户端与本地计算比对。</summary>
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
    }
}
