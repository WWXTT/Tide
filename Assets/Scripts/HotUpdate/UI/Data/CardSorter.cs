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
            if (card == null || card.Cost == null || card.Cost.Count == 0) return UIColor.Gray;
            UIColor best = UIColor.Gray;
            float bestAmount = 0f;
            // 迭代序即并列时的优先序；严格 > 保证同额取靠前的色
            foreach (var mana in RankOrder)
            {
                if (card.Cost.TryGetValue((int)mana, out var amount) && amount > bestAmount)
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
            return card.Cost.TryGetValue((int)ToMana(color), out var amount) && amount > 0f;
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
}
