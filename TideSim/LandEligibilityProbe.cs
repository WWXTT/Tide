using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>蓝套「可作地/产色」探针（2026-11-18 基础运转验收用）：
    /// 打印蓝套关键牌在 CardData 静态口径下的可作地资格、锁色候选、推导费。</summary>
    internal static class LandEligibilityProbe
    {
        public static int Run()
        {
            var all = CardCatalog.LoadAll();
            Console.WriteLine($"【蓝套可作地探针】卡表 {all.Count} 张");
            foreach (var name in new[] { "潮汐", "回潮", "乱锋", "潮痕碑", "深潭碑" })
            {
                var data = all.FirstOrDefault(c => c.CardName == name);
                if (data == null) { Console.WriteLine($"  ✗ {name} 未找到"); continue; }
                var colors = ElementPoolSystem.GetLandColorChoices(data);
                bool can = ElementPoolSystem.CanPlaceAsLand(data);
                var cost = CardCostService.Derive(data);
                var c = data.Cost;
                Console.WriteLine($"  {name,-6} {data.Supertype,-12} cost=[{c[ManaType.Gray]},{c[ManaType.Red]},{c[ManaType.Blue]},{c[ManaType.Green]}] " +
                                  $"derivedTier={cost.DerivedTier} 可作地={can} 锁色=[{string.Join(",", colors ?? new List<ManaType>())}]");
            }
            return 0;
        }
    }
}
