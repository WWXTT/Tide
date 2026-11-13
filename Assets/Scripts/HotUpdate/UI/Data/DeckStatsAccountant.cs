using System.Collections.Generic;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡组构筑统计口径单源（2026-11-13 定案）：费用曲线与颜色分布按「支付单元」统计——
    /// 每册卡 = 卡单元（卡费档）+ 每个主动效果各一单元（效果档），替代旧口径只按卡面
    /// TotalCost（主动效果恒 0 费、法术卡全落 0 档）。
    ///
    /// 单元规则（需求 5/6/7；2026-11-13 用户改版：曲线档改求和口径）：
    /// · 曲线档 = round(费用 + 代价总值)——费用与代价叠加计档（旧 max(费,代价) 口径被取代）；
    /// · 颜色 = 有代价→代价构成色；无代价→费用构成色；全 0→灰（req5）；多色各计一次；
    /// · 卡单元：费 = card.Cost（Derive 推导净费缓存——非主动效果的施放期代价已在 5c 抵扣），
    ///   代价 = 非主动效果行 fx.Costs 载荷之和（主动效果代价归各自效果单元，不双计）；
    ///   0 费且无代价的卡按灰计（req5）；
    /// · 效果单元（每个主动效果）：费 = ActiveEffectNetCost(fx) ?? fx.AnchorCost
    ///   （CardOverlayItem 同源先例——带代价行取净费、否则锚价），代价 = EffectPayloadCostOf(fx)；
    ///   全 0 效果条也计 0 档灰（纳入统计而非忽略——req6）。
    /// 消费：DeckBuilderScreen stats-row（曲线 10 档 + 颜色分布行）；行首费用徽标 BindCost
    /// 复用 CardPayloadOf（同口径 费用+代价）。
    /// </summary>
    public static class DeckStatsAccountant
    {
        /// <summary>统计产出：曲线 10 档（0..8 各一档、≥9 归第 10 档——与 stats-row 烘焙 tier 同源）
        /// + 四色计数（单元多色各计一次）+ 单元总数。</summary>
        public sealed class Stats
        {
            public readonly int[] Tiers = new int[10];
            public int Red;
            public int Blue;
            public int Green;
            public int Gray;
            public int UnitCount;
        }

        /// <summary>逐册计单元（cards=每册一条目——同卡多册多单元，与旧曲线口径一致）。</summary>
        public static Stats Account(IEnumerable<CardData> cards)
        {
            var st = new Stats();
            if (cards == null) return st;
            foreach (var card in cards)
            {
                if (card == null) continue;
                CountUnit(st, card.Cost, CardPayloadOf(card));
                if (card.Effects == null) continue;
                foreach (var fx in card.Effects)
                {
                    if (fx == null || !CardComposerScreen.IsActiveEffect(fx)) continue;
                    // 带代价行取净费（发动费−抵扣），否则锚价（装载期还原缓存）
                    var fee = CardComposerScreen.ActiveEffectNetCost(fx) ?? fx.AnchorCost;
                    CountUnit(st, fee, CardComposerScreen.EffectPayloadCostOf(fx));
                }
            }
            return st;
        }

        /// <summary>卡单元的施放期代价（非主动效果行——主动效果的代价归各自效果单元）：
        /// 逐行 EffectPayloadCostOf 分色面求和。BindCost 行首费用徽标同源复用（费用+代价口径）。</summary>
        public static ElementCost CardPayloadOf(CardData card)
        {
            if (card?.Effects == null) return null;
            ElementCost sum = null;
            foreach (var fx in card.Effects)
            {
                if (fx == null || CardComposerScreen.IsActiveEffect(fx)) continue;
                var payload = CardComposerScreen.EffectPayloadCostOf(fx);
                if (payload == null) continue;
                if (sum == null) sum = new ElementCost();
                sum[ManaType.Red] += payload[ManaType.Red];
                sum[ManaType.Blue] += payload[ManaType.Blue];
                sum[ManaType.Green] += payload[ManaType.Green];
                sum[ManaType.Gray] += payload[ManaType.Gray];
            }
            return sum != null && sum.IsZero ? null : sum;
        }

        /// <summary>单单元记账：档位=round(费+代价)钳 0..9（2026-11-13 求和口径）；色=代价色→费色→灰。</summary>
        private static void CountUnit(Stats st, ElementCost fee, ElementCost payload)
        {
            float feeTotal = fee?.Total ?? 0f;
            float payloadTotal = payload?.Total ?? 0f;
            st.UnitCount++;
            st.Tiers[Mathf.Clamp(Mathf.RoundToInt(feeTotal + payloadTotal), 0, 9)]++;
            if (payloadTotal > 0f) CountColors(st, payload);
            else if (feeTotal > 0f) CountColors(st, fee);
            else st.Gray++;
        }

        /// <summary>构成色计数（红/蓝/绿/灰四色；多色各计一次——同旧多色卡口径；Any 彩不计）。</summary>
        private static void CountColors(Stats st, ElementCost cost)
        {
            if (cost == null) return;
            if (cost[ManaType.Red] > 0f) st.Red++;
            if (cost[ManaType.Blue] > 0f) st.Blue++;
            if (cost[ManaType.Green] > 0f) st.Green++;
            if (cost[ManaType.Gray] > 0f) st.Gray++;
        }
    }
}

