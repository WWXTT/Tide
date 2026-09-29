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

        public Card() : base(createTimestamp: false) { }
    }
}
