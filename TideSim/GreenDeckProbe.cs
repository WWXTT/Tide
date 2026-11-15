using System;
using System.Collections.Generic;
using System.Linq;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 绿预组数据探针（2026-11-15 临时，跑完删）：打印指定卡的推导费 / 效果选择模式 / 效果推导价 /
    /// 内容指纹——用于手改绿卡效果（枯竭全取、删附加步）后回填 cost 与 hash 列。
    /// 用法：dotnet run --project TideSim -- --green-probe [卡 id…]（缺省=绿预组 11 卡）
    /// </summary>
    internal static class GreenDeckProbe
    {
        private static readonly string[] DefaultIds =
        {
            "C_F245F2F9", "C_C3B25F32", "C_A92F5E99", "C_60D27A90", "C_21E4EAC1",
            "C_EEF587D5", "C_91CE77A8", "C_76B63B4D", "C_D1D6C529", "C_B0EF3C08", "C_AC7FC01F",
        };

        public static int Run(string[] ids)
        {
            CardCatalog.LoadAll();
            // 2026-11-16：召唤衍生物的动态费用要解析模板卡；探针也注入解析器（与 AiDuelRunner 同口径）。
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            if (ids == null || ids.Length == 0) ids = DefaultIds;
            int fail = 0;
            foreach (var id in ids)
            {
                var card = CardCatalog.GetById(id);
                if (card == null) { Console.WriteLine($"✗ 卡池无 {id}"); fail++; continue; }

                var derived = CardCore.CardCostService.Derive(card);
                Console.WriteLine($"== {card.CardName}({card.ID}) {card.Supertype} "
                    + $"{(card.Supertype == CardCore.Cardtype.Creature ? card.Power + "/" + card.Life : "")}");
                Console.WriteLine($"   cardCost=[{Fmt(derived.DerivedCost)}] Σ={derived.DerivedTotal} 档={derived.DerivedTier}"
                    + $"  hash={SynergyUI.ContentHasher.HashCard(card)} seal={SynergyUI.ContentHasher.SealCard(card)}"
                    + $" stored={card.ConfigHash ?? "(null)"}");
                Console.WriteLine($"   S={derived.S} K={derived.K} E={derived.EAnchor} 代价价={derived.PayloadPrice} 实抵={derived.PayloadApplied}"
                    + $" 抵扣色=[{derived.PayloadScopeZh}]");
                foreach (var b in derived.Breakdown)
                    Console.WriteLine($"     · {b.Stage} {b.Label} = {b.Value:0.##}{(b.Color.HasValue ? " " + b.Color : "")}");

                var fxData = card.Effects ?? new List<CardCore.CardEffectData>();
                foreach (var fx in fxData)
                {
                    if (fx == null) continue;
                    Console.WriteLine($"   fxData {fx.Id} anchor=[{Fmt(fx.AnchorCost)}]"
                        + $" hash={SynergyUI.ContentHasher.HashEffectOf(fx)} seal={SynergyUI.ContentHasher.SealEffectOf(fx)}");
                }
                foreach (var def in CardCore.CardEffectConverter.ConvertAll(fxData, card.ID))
                {
                    if (def == null) continue;
                    var cost = CardCore.CostDerivationService.DeriveElementCosts(def, 0);
                    var net = CardCore.EffectActivationGate.NetBill(cost, def.Costs);
                    Console.WriteLine($"   fx {def.Id} mode={def.SelectionMode} cnt={def.TargetCount} "
                        + $"domain=[{string.Join(",", def.TargetDomain ?? new List<int>())}] "
                        + $"推导价=[{Fmt(cost)}] 净费=[{Fmt(net)}] pinned={def.AnchorCostPinned} name={def.DisplayName}");
                }
            }
            return fail == 0 ? 0 : 1;
        }

        private static string Fmt(CardCore.ElementCost c)
        {
            if (c == null) return "";
            var v = c.v ?? new float[0];
            var parts = new List<string>();
            string[] names = { "灰", "红", "蓝", "绿", "任意" };
            for (int i = 0; i < v.Length && i < names.Length; i++)
                if (v[i] != 0f) parts.Add($"{names[i]}{v[i]:0.##}");
            return parts.Count == 0 ? "0" : string.Join("+", parts);
        }
    }
}
