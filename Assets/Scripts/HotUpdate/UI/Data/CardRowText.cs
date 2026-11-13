using CardCore;
using TMPro;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>卡行缩略写法单源（2026-11-13 用户定案：「费用｜名称｜类别｜基础属性参数」）：
    /// 卡组合界面（CardComposerScreen 卡池行）与卡组构筑界面（DeckBuilderScreen 卡池行/卡组行）
    /// 共用本类，两屏不再各写一套（旧口径：卡组合徽标只读 card.Cost、构筑读 TotalCost+代价，
    /// 法术一边缺速度、一边缺锚价）。
    ///
    /// 字段口径：
    /// · **费用 = 消耗 + 代价**。生物/结界=卡级净费（card.TotalCost，非主动效果行代价已在推导 5c
    ///   抵扣）+ 非主动效果行施放期代价（DeckStatsAccountant.CardPayloadOf 单源）＝抵扣前推导价；
    ///   法术卡级费恒 0（主动锚价构筑期不计卡费，施放时择一现付），消耗=所选主动效果的净锚价
    ///   （CardComposerScreen.ActiveEffectNetCost ?? AnchorCost），代价=该效果级代价——逐模式取
    ///   「净锚价+代价」最高者（与 CardCostService 最大模式费 / 施放账单 GetCardCost 同向）。
    /// · **类别+基础属性**：生物「生物 属性攻/血」、结界「结界 耐久N」、场地「场地 耐久N」
    ///   （类别名=CardSorter.TypeName 同源）、法术「法术 速度N」（CardOverlayItem.EffectSpeedOf 同源）。
    /// </summary>
    public static class CardRowText
    {
        /// <summary>费用＝消耗+代价（整数，四舍五入——卡费推导已是逐色进位整数，此处只并代价）。</summary>
        public static int FeeOf(CardData card)
        {
            if (card == null) return 0;
            if (card.Supertype == Cardtype.Spell) return Mathf.RoundToInt(SpellFeeOf(card));
            float payload = DeckStatsAccountant.CardPayloadOf(card)?.Total ?? 0f;
            return Mathf.RoundToInt(card.TotalCost + payload);
        }

        /// <summary>费用数字文本（行首圆片费标；0 费显「0」——与行格式「费用」列对齐）。</summary>
        public static string FeeTextOf(CardData card) => FeeOf(card).ToString();

        /// <summary>法术费用（择一模式，取最高）：净锚价+效果级代价；无主动效果=0。
        /// 净锚价=带代价行时的抵扣后实付（ActiveEffectNetCost），无代价行=锚价快照（卡面同款回落，
        /// 二者互斥——无代价行时 payload 必为 null，省一次转换）。</summary>
        private static float SpellFeeOf(CardData card)
        {
            float best = 0f;
            if (card.Effects == null) return best;
            foreach (var fx in card.Effects)
            {
                if (!CardComposerScreen.IsActiveEffect(fx)) continue;
                var net = CardComposerScreen.ActiveEffectNetCost(fx);
                float fee;
                float payload = 0f;
                if (net != null)
                {
                    fee = net.Total; // 已扣代价——加回代价即抵扣前锚价
                    payload = CardComposerScreen.EffectPayloadCostOf(fx)?.Total ?? 0f;
                }
                else fee = fx.AnchorCost?.Total ?? 0f;
                if (fee + payload > best) best = fee + payload;
            }
            return best;
        }

        /// <summary>类别+基础属性参数（行尾 meta）：生物 属性x/x、结界/场地 耐久x、法术 速度x。</summary>
        public static string TypeStatOf(CardData card)
        {
            if (card == null) return "";
            string kind = CardSorter.TypeName(card); // 「场地」单列（P8）——类别名同源
            switch (card.Supertype)
            {
                case Cardtype.Creature:
                    return $"{kind} 属性{card.Power ?? 0}/{card.Life ?? 0}";
                case Cardtype.Enchantment:
                    return $"{kind} 耐久{CardCompositionCost.TotalDurabilityOf(card)}";
                case Cardtype.Spell:
                    var speed = CardOverlayItem.EffectSpeedOf(card);
                    return speed.HasValue ? $"{kind} 速度{speed.Value}" : kind;
                default:
                    return kind;
            }
        }

        /// <summary>费用写值：行模板 cost 节点常显（池化行复用须还原显隐），数字=FeeTextOf。</summary>
        public static void BindCostNode(RectTransform row, CardData card)
        {
            var node = UiKit.FindDeep(row, "cost");
            if (node == null) return;
            if (!node.gameObject.activeSelf) node.gameObject.SetActive(true);
            var text = node.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = FeeTextOf(card);
        }
    }
}
