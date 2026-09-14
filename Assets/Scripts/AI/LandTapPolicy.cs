using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine;

namespace CardCore.AI
{
    /// <summary>
    /// 拍地产色启发式（2026-09-14 统一混付配套）——吸收 SimpleAI / NeuralAI / TideHeadlessDriver
    /// 三处逐字副本，唯一实现。混付感知需求计算：
    /// ① 手牌费用色直方图 → ② 同色 bank 抵扣 → ③ 万用池（灰+黑+白 bank）抵四色剩余需求
    /// （黑白万用化后灰/黑白都能垫红蓝绿灰缺口；黑白本色费单向不可垫，见 ElementPaymentValidator）
    /// ——只有抵不掉的需求才驱动横置选色，避免过度拍纯色。
    /// 选色：需求色优先（需求量大者优先），无需求色回落剩余指示物最多者（并列取枚举序，确定性）。
    /// </summary>
    public static class LandTapPolicy
    {
        /// <summary>混付感知的手牌费用色需求（决定横置产色的优先级）。</summary>
        public static Dictionary<ManaType, int> BuildColorDemand(GameCore core, Player me)
        {
            // ① 原始直方图（含黑白需求项——SelectLandColor 只从地牌可用色里挑，天然忽略）
            var demand = new Dictionary<ManaType, int>();
            foreach (var card in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (!(card is IHasCost hc) || hc.Cost == null) continue;
                foreach (var kv in hc.Cost)
                {
                    var color = (ManaType)kv.Key;
                    int amount = (int)Math.Ceiling(kv.Value);
                    demand[color] = demand.TryGetValue(color, out var v) ? v + amount : amount;
                }
            }

            var bank = core.ElementPool.GetPool(me).AvailableMana;

            // ② 同色 bank 抵扣（黑白同色抵扣保留其 bank 供本色费用用）
            foreach (var color in demand.Keys.ToList())
            {
                int have = bank.TryGetValue(color, out var b) ? b : 0;
                if (have > 0)
                    demand[color] -= Math.Min(have, demand[color]);
            }

            // ③ 万用池（灰+黑+白 bank）抵四色剩余需求
            int wildPool = (bank.TryGetValue(ManaType.Gray, out var gray) ? gray : 0)
                         + (bank.TryGetValue(ManaType.Black, out var black) ? black : 0)
                         + (bank.TryGetValue(ManaType.White, out var white) ? white : 0);
            if (wildPool > 0)
            {
                foreach (var color in new[] { ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Gray })
                {
                    if (wildPool <= 0) break;
                    if (!demand.TryGetValue(color, out var need) || need <= 0) continue;
                    int cut = Math.Min(wildPool, need);
                    demand[color] -= cut;
                    wildPool -= cut;
                }
            }

            // ④ 清零项移除
            return demand.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        /// <summary>单地产色决策：需求色优先（需求量大者优先），无需求色取剩余指示物最多者。</summary>
        public static ManaType? SelectLandColor(PooledCard land, Dictionary<ManaType, int> demand)
        {
            ManaType? best = null;
            int bestScore = 0;
            foreach (var color in land.GetAvailableColors())
            {
                if (demand.TryGetValue(color, out var d) && d > bestScore)
                {
                    best = color;
                    bestScore = d;
                }
            }
            if (best != null) return best;

            ManaType? most = null;
            int mostTokens = 0;
            foreach (var kv in land.Tokens)
            {
                if (kv.Value > mostTokens)
                {
                    mostTokens = kv.Value;
                    most = kv.Key;
                }
            }
            return most;
        }

        /// <summary>横置全部地牌（三 AI 共用入口；产色失败静默跳过该地牌）。</summary>
        public static void TapAllLands(GameCore core, Player me)
        {
            var demand = BuildColorDemand(core, me);
            foreach (var land in new List<PooledCard>(core.ElementPool.GetPooledCards(me)))
            {
                var color = SelectLandColor(land, demand);
                if (color.HasValue)
                    GameActions.GainElementFromToken(core, me, land, color.Value);
            }
        }
    }
}
