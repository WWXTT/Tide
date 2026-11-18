using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 场地特殊化规则单源（2026-11-18 定案·docs/场地特殊化_修改计划.md P5/P6）：
    /// 场地卡（CardData.IsField，落己方场地格）是区域词元目标、不是战场单位——
    /// ① 每次受伤固定扣 1 耐久（战斗/法术/效果同口径，与伤害量无关；防护层先照常算，
    /// 只在「落耐久」一口钳 1）；② 指示物效果对场地无效（正量投放 0 落账；入场耐久初始化
    /// 走 CounterGrantKind.Init 豁免）；③ 关键词效果对场地无效（效果授予/箭头光环投递空转；
    /// 印刷关键词不拦——不是效果）。
    /// 调用点禁止各写一份判定——一律转发本口。计价不加分（本批不改价）；网络协议不改。
    /// </summary>
    public static class FieldRules
    {
        /// <summary>是否场地卡（CardExtensions.IsFieldCard 转发单源）。</summary>
        public static bool IsField(Card card) => card != null && card.IsFieldCard();

        /// <summary>场地每次受伤固定 1 耐久；非场地原样返回（Legacy/Chain 两条伤害路径同口）。</summary>
        public static int DurabilityDamage(Card target, int incoming)
            => IsField(target) ? 1 : incoming;

        /// <summary>指示物效果投放闸：效果类正量对**战场上的场地**一律 0 落账（CounterGrantKind.Effect）；
        /// 引擎初始化（Init，如入场耐久）与负量（移除/伤害扣耐久）不拦。
        /// 2026-11-18 验收修复：作用域=战场——手牌/牌库/墓地里的场地卡仍是普通卡（费用指示物等照常）。</summary>
        public static bool BlocksCounterGrant(Entity entity)
            => entity is Card c && c.GetZone() == Zone.Battlefield && IsField(c);

        /// <summary>关键词效果授予闸：**战场上的场地** + 非 Printed 轨（效果授予 Temp/Setting/光环投递）空转；
        /// 卡面印刷关键词（Printed——CardData ctor 直写）不拦，不是「效果」；区外场地卡照常。</summary>
        public static bool BlocksKeywordGrant(Entity entity, KeywordLane lane)
            => lane != KeywordLane.Printed && entity is Card c && c.GetZone() == Zone.Battlefield && IsField(c);
    }
}
