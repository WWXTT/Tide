using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>蓝套「可作地/产色」探针（2026-11-18 基础运转验收用；同日硬化为断言）：
    /// 打印蓝套关键牌在 CardData 静态口径下的可作地资格、锁色候选、推导费；
    /// 并断言「0 费卡被拒绝作地牌」（IsFreeCastCard 闸——端到端侧夹具已改直供，
    /// 本探针与 CardPipelineVerifier.TestLandEconomy 是该规则的自动化反证口）。</summary>
    internal static class LandEligibilityProbe
    {
        private static int _fail;

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            var all = CardCatalog.LoadAll();
            Console.WriteLine($"【蓝套可作地探针】卡表 {all.Count} 张");
            foreach (var name in new[] { "潮汐", "回潮", "乱锋", "潮痕碑", "深潭碑" })
            {
                var data = all.FirstOrDefault(c => c.CardName == name);
                if (data == null) { Console.WriteLine($"  ✗ {name} 未找到"); _fail++; continue; }
                var colors = ElementPoolSystem.GetLandColorChoices(data);
                bool can = ElementPoolSystem.CanPlaceAsLand(data);
                var cost = CardCostService.Derive(data);
                var c = data.Cost;
                Console.WriteLine($"  {name,-6} {data.Supertype,-12} cost=[{c[ManaType.Gray]},{c[ManaType.Red]},{c[ManaType.Blue]},{c[ManaType.Green]}] " +
                                  $"derivedTier={cost.DerivedTier} 可作地={can} 锁色=[{string.Join(",", colors ?? new List<ManaType>())}]");
            }

            // 0 费禁地（2026-11-18 恢复）静态口径：命中本闸的是「净费 0 且锁色候选非空」的卡——
            // 潮汐（印制 0 费、主动锚价开蓝格、净费 0）即此型；白板生物推导 0 但运行口径有底灰
            // 且无锁色，不触发本闸。锁色非空 ⇒ 拒因只能是 0 费。
            var chaoxi = all.FirstOrDefault(c => c.CardName == "潮汐");
            Assert(chaoxi != null, "前置：潮汐在卡表");
            if (chaoxi != null)
            {
                Assert(GameActions.IsFreeCastCard(chaoxi), "前置：IsFreeCastCard 认定潮汐（净费 0）");
                Assert(ElementPoolSystem.GetLandColorChoices(chaoxi).Count > 0,
                    "前置：潮汐锁色候选非空（毛锚价开蓝格）——拒因只能是 0 费");
                Assert(!ElementPoolSystem.CanPlaceAsLand(chaoxi),
                    "0 费禁地：CanPlaceAsLand 拒绝零费卡（静态口径——UI/AI 预检同口）");
            }

            Console.WriteLine(_fail == 0 ? "蓝套可作地探针：通过" : $"蓝套可作地探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }
    }
}
