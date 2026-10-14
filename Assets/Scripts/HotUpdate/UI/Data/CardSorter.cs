using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 卡池全局排序（2026-09-24 定案：颜色灰→红→蓝→绿→黑→白，再按费用小→大，全局统一口径）。
    /// 卡牌合成/卡组构建等界面共用；过滤延续效果组合界面（颜色 chip + 谓词）。
    ///
    /// 卡主色判定：非灰费用额最高者（并列取红→蓝→绿→黑→白序——同额时靠前的色优先）；
    /// 无彩色费用（纯灰/零费）归灰。过滤口径与排序口径分离：过滤看费用构成是否含该色。
    /// </summary>
    public static class CardSorter
    {
        /// <summary>排序色序（灰红蓝绿黑白定案序；All 不参与排序）。</summary>
        public static int ColorRank(UIColor color)
        {
            switch (color)
            {
                case UIColor.Gray: return 0;
                case UIColor.Red: return 1;
                case UIColor.Blue: return 2;
                case UIColor.Green: return 3;
                case UIColor.Black: return 4;
                case UIColor.White: return 5;
                default: return 0;
            }
        }

        /// <summary>卡主色（排序/分组口径）：非灰费用额最高者，并列取红→蓝→绿→黑→白序；无彩色=灰。</summary>
        public static UIColor PrimaryColor(CardData card)
        {
            if (card == null || card.Cost == null || card.Cost.IsZero) return UIColor.Gray;
            UIColor best = UIColor.Gray;
            float bestAmount = 0f;
            // 迭代序即并列时的优先序；严格 > 保证同额取靠前的色
            foreach (var mana in RankOrder)
            {
                var amount = card.Cost[mana];
                if (amount > bestAmount)
                {
                    best = UiOf(mana);
                    bestAmount = amount;
                }
            }
            return best;
        }

        /// <summary>过滤口径：卡费用构成中是否含该色（与主色无关——多色卡可被任一构成色筛中）。</summary>
        public static bool HasCostColor(CardData card, UIColor color)
        {
            if (card == null || card.Cost == null || color == UIColor.All) return true;
            return card.Cost[ToMana(color)] > 0f;
        }

        /// <summary>全局统一排序：颜色（灰红蓝绿黑白）→ 费用小→大 → 名称（当前文化序）。</summary>
        public static IEnumerable<CardData> Sort(IEnumerable<CardData> cards)
        {
            return cards
                .OrderBy(c => ColorRank(PrimaryColor(c)))
                .ThenBy(c => c.TotalCost)
                .ThenBy(c => c.CardName ?? "", System.StringComparer.CurrentCulture);
        }

        /// <summary>类型中文（卡池分组/预览行）：生物 / 瞬间 / 结界，其他超类回退枚举名。</summary>
        public static string TypeName(CardData card)
        {
            switch (card?.Supertype)
            {
                case Cardtype.Creature: return "生物";
                case Cardtype.Spell: return "瞬间";
                case Cardtype.Enchantment: return "结界";
                default: return card?.Supertype.ToString() ?? "?";
            }
        }

        private static readonly ManaType[] RankOrder =
        {
            ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Black, ManaType.White,
        };

        private static UIColor UiOf(ManaType mana)
        {
            switch (mana)
            {
                case ManaType.Red: return UIColor.Red;
                case ManaType.Blue: return UIColor.Blue;
                case ManaType.Green: return UIColor.Green;
                case ManaType.Black: return UIColor.Black;
                case ManaType.White: return UIColor.White;
                default: return UIColor.Gray;
            }
        }

        private static ManaType ToMana(UIColor color)
        {
            switch (color)
            {
                case UIColor.Red: return ManaType.Red;
                case UIColor.Blue: return ManaType.Blue;
                case UIColor.Green: return ManaType.Green;
                case UIColor.Black: return ManaType.Black;
                case UIColor.White: return ManaType.White;
                default: return ManaType.Gray;
            }
        }
    }

    // ======================================== 搜索查询（2026-10-13 三界面统一改版） ========================================

    /// <summary>
    /// 三界面共用搜索语义：空白切词·多词 AND·全字段语料并集。
    /// 熟手一个搜索框直达（卡级语料覆盖卡名/关键词/组合后效果全文——找卡时记得的往往是
    /// "那张能抽牌的卡"）；新手分面 chips 与搜索词同语义（点「守护」=输「守护」）。
    /// 词表零新增：关键词=keywords 合成表、卡型=CardSorter.TypeName、类别词=原子表 Tags——全部既有单源派生。
    /// 与 CardSorter 同文件收口（新文件进不了已生成的 csproj 是老坑）。
    /// </summary>
    public static class SearchQuery
    {
        /// <summary>查询处于激活态（存在任一非空词）。</summary>
        public static bool IsActive(string query) => Tokens(query).Count > 0;

        /// <summary>空白切词（多词 AND 的词源）。</summary>
        public static List<string> Tokens(string query)
            => string.IsNullOrWhiteSpace(query)
                ? new List<string>()
                : new List<string>(query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>多词 AND：每词均被 corpus 包含才命中（单词时即原单段子串语义）；
        /// 空查询恒过（=不过滤）。大小写不敏感。</summary>
        public static bool Match(string corpus, string query)
        {
            var tokens = Tokens(query);
            if (tokens.Count == 0) return true;
            if (string.IsNullOrEmpty(corpus)) return false;
            for (int i = 0; i < tokens.Count; i++)
                if (corpus.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            return true;
        }

        /// <summary>名称命中（搜索态排序加权用）：任一词被卡名包含——命中行前置，组内保持全局序。</summary>
        public static bool NameHit(CardData card, string query)
        {
            if (card == null) return false;
            var name = card.CardName ?? "";
            foreach (var t in Tokens(query))
                if (name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        /// <summary>卡级语料：卡名∪ID∪卡型∪关键词(中文名∪id)∪Tags∪技能资格∪组合后效果全文。
        /// 效果全文与卡面显示同源（AtomText.RenderEffectSummary 同一包装——CardComposerScreen.MakePoolRow
        /// / DeckBuilderScreen.BuildPreview 的调用照抄于此）；多效果增量叠加全收：搜任一组成部分措辞命中整卡。
        /// 调用方按需缓存（每键全池重算偏贵）。</summary>
        public static string BuildCorpus(CardData card)
        {
            if (card == null) return "";
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(card.CardName)) parts.Add(card.CardName);
            if (!string.IsNullOrEmpty(card.ID)) parts.Add(card.ID);
            parts.Add(CardSorter.TypeName(card));
            if (card.Keywords != null)
                foreach (var k in card.Keywords)
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    parts.Add(k);
                    var def = CardLoader.GetKeywordDefinition(k);
                    if (!string.IsNullOrEmpty(def?.nameZh)) parts.Add(def.nameZh);
                }
            if (card.Tags != null)
                foreach (var t in card.Tags)
                    if (!string.IsNullOrEmpty(t)) parts.Add(t);
            if (HeroSkillSystem.CanBeSkillCard(card)) parts.Add("技能");
            if (card.Effects != null)
                foreach (var fx in card.Effects)
                {
                    if (fx == null) continue;
                    var summary = AtomText.RenderEffectSummary(
                        new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps });
                    if (!string.IsNullOrEmpty(summary)) parts.Add(summary);
                }
            return string.Join("\n", parts);
        }
    }
}
