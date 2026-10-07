using System;
using System.Collections.Generic;

namespace SynergyUI
{
    /// <summary>
    /// 可存档卡组 —— 仅记录卡组名与卡牌 ID 列表。
    /// 运行时通过 CardCatalog 按 ID 还原 CardData，再交给
    /// CardLoader.BuildDeck 构建实际对战卡组。
    ///
    /// 注：项目原本没有任何 Deck 存档类型（CardLoader.BuildDeck 只是把一份
    /// 扁平卡表按份数复制），本类是新增的持久化结构。
    /// 字段用 public 字段而非属性，以便 Unity JsonUtility 序列化。
    /// </summary>
    [Serializable]
    public class DeckData
    {
        public string name;
        public List<string> cardIds = new List<string>();

        /// <summary>英雄技能标记卡 ID（2026-10-07 卡牌化改版）：须为 cardIds 内一张
        /// 「恰好一个主动效果的结界」（HeroSkillSystem.CanBeSkillCard 资格）；开局从牌库
        /// 抽出落技能栏。null/空 = 对局无英雄技能。旧档缺省 null 天然兼容。</summary>
        public string skillCardId;

        public DeckData() { }

        public DeckData(string name)
        {
            this.name = name;
        }
    }
}
