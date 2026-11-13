using System;
using System.Linq;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 数据指纹复算探针（2026-11-13 临时用）：按卡 id 打印 ContentHasher 的卡/效果内容指纹，
    /// 供手工改数据（如活体炸弹去掉门奖励）后回填 JSON 的 hash 列（ID 冻结口径：id 不变、hash 跟着内容走）。
    /// 用法：dotnet run --project TideSim -- --data-hash C_5705F9FE [更多 id…]
    /// </summary>
    internal static class DataHashProbe
    {
        public static int Run(string[] ids)
        {
            CardCatalog.LoadAll();
            int fail = 0;
            foreach (var id in ids)
            {
                var card = CardCatalog.GetById(id);
                if (card == null)
                {
                    Console.WriteLine($"✗ 卡池无 {id}");
                    fail++;
                    continue;
                }
                Console.WriteLine($"{id} {card.CardName}  cardHash={ContentHasher.HashCard(card)}"
                                  + $"  seal={ContentHasher.SealCard(card)}  stored={card.ConfigHash ?? "(null)"}");
                foreach (var fx in card.Effects ?? new System.Collections.Generic.List<CardCore.CardEffectData>())
                {
                    if (fx == null) continue;
                    Console.WriteLine($"    fx {fx.Id}  hash={ContentHasher.HashEffectOf(fx)}"
                                      + $"  seal={ContentHasher.SealEffectOf(fx)}  name={fx.DisplayName}");
                }
            }
            return fail == 0 ? 0 : 1;
        }
    }
}
