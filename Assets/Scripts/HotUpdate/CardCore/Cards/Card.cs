namespace CardCore
{
    /// <summary>
    /// 卡牌基类 - 继承自 Entity
    /// 其他属性通过接口形式按需添加
    /// 使用 partial 以允许扩展字段定义
    /// </summary>
    public partial class Card : Entity
    {
        /// <summary>
        /// 卡牌唯一标识ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>结界触发耐久累计（2026-11-13 A 口径）：每次自动效果发动 += 时机系数 f，
        /// 满 1 扣 1 耐久并回余——卡面耐久恒整数。随卡实例走，重进场/复制体各自新起算。</summary>
        internal float DurabilityCostAccumulator;

        public Card() : base(createTimestamp: false) { }
    }
}
