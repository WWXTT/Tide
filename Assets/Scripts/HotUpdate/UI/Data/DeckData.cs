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

        /// <summary>衍生物区（P9b 2026-10-29；2026-11-13 自动引用改版）：一组模板卡 ID=
        /// DeckTokenSupport.CollectReferencedTemplateIds(cardIds) ∪ str 空槽旧档绑定目标
        /// （构筑侧随加删卡实时重算，不设容量上限）。随组入局落 Zone.Extra（GameCore 按
        /// IsTokenTemplate 分流）。旧档（模板混在 cardIds 内）由 DeckSerializer 读取时
        /// 规范化迁入本字段——保存后 cardIds 不再携带模板。null=无。</summary>
        public List<string> tokenCardIds;

        /// <summary>衍生物槽绑定（P9b 旧档兜底通道）：（卡ID, 效果序号）→ 模板卡 ID——
        /// 仅 str 空槽生效（新卡一律卡组合界面直写 atom.str，str 为权威）；运行时按卡实例
        /// 解析（CardWrapper.TokenBindings，实例绑定优先、str 回落）。
        /// 绑定不进效果哈希、不进 CardData。null=无（无槽卡组/新档）。</summary>
        public List<DeckTokenBinding> tokenBindings;

        public DeckData() { }

        public DeckData(string name)
        {
            this.name = name;
        }
    }
}
