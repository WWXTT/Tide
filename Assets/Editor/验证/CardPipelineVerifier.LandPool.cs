using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardCore.Editor
{
    /// <summary>
    /// 地牌改版验证（2026-11-15 单格放置·多色产色 §5.4）：最小费用流支付规划八用例——
    /// 钱包全链优先（问题B）/钱包优先于地牌/一地一回合一点/双地双色匹配/钱包+地牌组合/
    /// 本色优先不跨色侵占/确定性两次规划一致/预检=结算同源且失败不动状态；
    /// 附 TapLandForElement 显式色闸（∈可产集合放行、∉拒绝）。
    /// 纯池级测试：存档→清池→逐用例装配→断言→还原（PlanPayment 只读口与 TryPayBill 同源）。
    /// </summary>
    public static partial class CardPipelineVerifier
    {
        private static void TestLandPoolMultiColor(GameCore core, Player p1)
        {
            var pool = core.ElementPool.GetPool(p1);
            var savedPooled = pool.PooledCards.ToList();
            var savedWallet = new Dictionary<ManaType, int>(pool.TempElements);
            int savedTaps = pool.TapsThisTurn;
            try
            {
                void Reset(Dictionary<ManaType, int> wallet, params PooledCard[] lands)
                {
                    pool.PooledCards.Clear();
                    foreach (var l in lands) pool.PooledCards.Add(l);
                    foreach (var k in pool.TempElements.Keys.ToList()) pool.TempElements[k] = 0;
                    foreach (var kv in wallet)
                        if (kv.Value > 0) pool.TempElements[kv.Key] = kv.Value;
                    pool.TapsThisTurn = 0;
                }

                // 夹具地：可产集合直接构造（绕开卡表——规划器只读 CanProduce/IsTapped）
                PooledCard Land(params ManaType[] colors) => new PooledCard(new Card { ID = "VERIFY_LANDPOOL" }, colors);
                Dictionary<ManaType, int> Wallet(params (ManaType c, int n)[] kv)
                {
                    var d = new Dictionary<ManaType, int>();
                    foreach (var (c, n) in kv) d[c] = n;
                    return d;
                }
                Dictionary<ManaType, int> Bill(params (ManaType c, int n)[] kv)
                {
                    var d = new Dictionary<ManaType, int>();
                    foreach (var (c, n) in kv) d[c] = n;
                    return d;
                }
                string PlanText(ElementPoolSystem.PaymentPlanView v)
                    => v == null ? "null"
                    : $"wallet[{string.Join(",", v.WalletUse.OrderBy(x => x.Key).Select(x => $"{x.Key}x{x.Value}"))}] "
                      + $"taps[{string.Join(",", v.Taps.Select(t => $"{t.Land.ColorOptions[0]}+{string.Join("/", t.Land.ColorOptions.Skip(1))}→{t.Color}"))}] "
                      + $"paid[{string.Join(",", v.TotalPaid.OrderBy(x => x.Key).Select(x => $"{x.Key}x{x.Value}"))}]";

                // ===== 用例1（问题B 修复）：账单灰1；钱包红1；灰地+红地 → 花红钱包、零横置 =====
                Reset(Wallet((ManaType.Red, 1)), Land(ManaType.Gray), Land(ManaType.Red));
                var plan1 = core.ElementPool.PlanPayment(Bill((ManaType.Gray, 1)), p1);
                Assert(plan1 != null && plan1.Taps.Count == 0
                       && plan1.WalletUse.TryGetValue(ManaType.Red, out var w1r) && w1r == 1,
                       $"§5.4-1 灰账单×红钱包+灰/红地：花红钱包不横置（实际：{PlanText(plan1)}）");

                // ===== 用例2：账单红1；钱包彩1；红地 → 花彩钱包（钱包优先于地牌） =====
                Reset(Wallet((ManaType.Any, 1)), Land(ManaType.Red));
                var plan2 = core.ElementPool.PlanPayment(Bill((ManaType.Red, 1)), p1);
                Assert(plan2 != null && plan2.Taps.Count == 0
                       && plan2.WalletUse.TryGetValue(ManaType.Any, out var w2a) && w2a == 1,
                       $"§5.4-2 红账单×彩钱包+红地：花彩钱包不横置（实际：{PlanText(plan2)}）");

                // ===== 用例3：账单红1+蓝1；仅 1 块红蓝双色地 → 不可付（一地一回合只产 1 点） =====
                Reset(Wallet(), Land(ManaType.Red, ManaType.Blue));
                var bill3 = Bill((ManaType.Red, 1), (ManaType.Blue, 1));
                Assert(core.ElementPool.PlanPayment(bill3, p1) == null
                       && !core.ElementPool.CanPayCostWithAutoTap(bill3, p1),
                       "§5.4-3 红1+蓝1×单块双色地：不可付（一块地一回合只能产 1 点）");

                // ===== 用例4：2 块红蓝双色地 → 可付，各横 1 块、一块产红一块产蓝 =====
                Reset(Wallet(), Land(ManaType.Red, ManaType.Blue), Land(ManaType.Red, ManaType.Blue));
                var bill4 = Bill((ManaType.Red, 1), (ManaType.Blue, 1));
                var plan4 = core.ElementPool.PlanPayment(bill4, p1);
                Assert(plan4 != null && plan4.Taps.Count == 2
                       && plan4.Taps.Select(t => t.Color).OrderBy(c => (int)c).SequenceEqual(
                           new[] { ManaType.Red, ManaType.Blue }),
                       $"§5.4-4 红1+蓝1×两块双色地：各产一色（实际：{PlanText(plan4)}）");

                // ===== 用例5：1 块红蓝双色地 + 钱包红1 → 钱包红付红、双色地产蓝 =====
                Reset(Wallet((ManaType.Red, 1)), Land(ManaType.Red, ManaType.Blue));
                var plan5 = core.ElementPool.PlanPayment(bill3, p1);
                Assert(plan5 != null && plan5.Taps.Count == 1 && plan5.Taps[0].Color == ManaType.Blue
                       && plan5.WalletUse.TryGetValue(ManaType.Red, out var w5r) && w5r == 1,
                       $"§5.4-5 红1+蓝1×双色地+红钱包：钱包付红地产蓝（实际：{PlanText(plan5)}）");

                // ===== 用例6：红地+灰地；钱包 0；账单红1+灰1 → 红产红、灰产灰（红地虽可垫灰，红需求只有它能付） =====
                Reset(Wallet(), Land(ManaType.Red), Land(ManaType.Gray));
                var plan6 = core.ElementPool.PlanPayment(Bill((ManaType.Red, 1), (ManaType.Gray, 1)), p1);
                Assert(plan6 != null && plan6.Taps.Count == 2
                       && plan6.Taps.Any(t => t.Color == ManaType.Red) && plan6.Taps.Any(t => t.Color == ManaType.Gray),
                       $"§5.4-6 红1+灰1×红/灰地：本色各付（实际：{PlanText(plan6)}）");

                // ===== 用例7：确定性——同一状态连续规划两次，逐项一致 =====
                Reset(Wallet((ManaType.Red, 1), (ManaType.Any, 1)),
                    Land(ManaType.Red, ManaType.Blue), Land(ManaType.Red, ManaType.Blue),
                    Land(ManaType.Gray), Land(ManaType.Green, ManaType.Gray));
                var bill7 = Bill((ManaType.Blue, 1), (ManaType.Gray, 2), (ManaType.Green, 1));
                var plan7a = core.ElementPool.PlanPayment(bill7, p1);
                var plan7b = core.ElementPool.PlanPayment(bill7, p1);
                bool identical = plan7a != null && plan7b != null
                    && plan7a.WalletUse.Count == plan7b.WalletUse.Count
                    && plan7a.WalletUse.All(kv => plan7b.WalletUse.TryGetValue(kv.Key, out var v) && v == kv.Value)
                    && plan7a.TotalPaid.Count == plan7b.TotalPaid.Count
                    && plan7a.TotalPaid.All(kv => plan7b.TotalPaid.TryGetValue(kv.Key, out var v) && v == kv.Value)
                    && plan7a.Taps.Count == plan7b.Taps.Count
                    && plan7a.Taps.Select(t => (t.Land, t.Color)).SequenceEqual(
                        plan7b.Taps.Select(t => (t.Land, t.Color)));
                Assert(identical, $"§5.4-7 同状态两次规划逐项一致（a：{PlanText(plan7a)}；b：{PlanText(plan7b)}）");

                // ===== 用例8：CanPay=true → Pay 必成功；CanPay=false → Pay 必失败且状态不变 =====
                Reset(Wallet((ManaType.Red, 1)), Land(ManaType.Red, ManaType.Blue));
                Assert(core.ElementPool.CanPayCostWithAutoTap(bill3, p1)
                       && core.ElementPool.TryPayBillWithAutoTap(bill3, p1, "§5.4-8a"),
                       "§5.4-8a 预检可付 → 结算必成功（同源单规划）");
                Assert(pool.TempElements[ManaType.Red] == 0
                       && pool.PooledCards.All(l => l.IsTapped)
                       && pool.TapsThisTurn == 1,
                       "§5.4-8a 结算后：红钱包清零、双色地横置、横置台账 1");

                Reset(Wallet(), Land(ManaType.Red, ManaType.Blue)); // 用例3 复刻态
                int tapsBefore = pool.TapsThisTurn;
                var walletBefore = new Dictionary<ManaType, int>(pool.TempElements);
                bool tappedBefore = pool.PooledCards[0].IsTapped;
                Assert(!core.ElementPool.CanPayCostWithAutoTap(bill3, p1)
                       && !core.ElementPool.TryPayBillWithAutoTap(bill3, p1, "§5.4-8b"),
                       "§5.4-8b 预检不可付 → 结算必失败");
                Assert(pool.TapsThisTurn == tapsBefore
                       && walletBefore.All(kv => pool.TempElements[kv.Key] == kv.Value)
                       && pool.PooledCards[0].IsTapped == tappedBefore,
                       "§5.4-8b 失败不动状态（横置台账/钱包/横置旗零变化）");

                // ===== 附：手动产色显式色闸（TapLandForElement(color)） =====
                bool ownMain = core.TurnEngine.TurnPlayer == p1
                               && core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main;
                if (ownMain)
                {
                    Reset(Wallet(), Land(ManaType.Red, ManaType.Blue));
                    var dual = pool.PooledCards[0];
                    Assert(!GameActions.TapLandForElement(core, p1, dual.SourceCard, ManaType.Green, out var why)
                           && why != null && why.Contains("可产"),
                           $"手动产色：色 ∉ 可产集合拒绝（实际：{why}）");
                    Assert(GameActions.TapLandForElement(core, p1, dual.SourceCard, ManaType.Blue, out _)
                           && dual.IsTapped && pool.TempElements[ManaType.Blue] == 1,
                           "手动产色：色 ∈ 可产集合放行（横置+钱包蓝+1）");
                    Assert(!GameActions.TapLandForElement(core, p1, dual.SourceCard, ManaType.Red, out var why2),
                           $"同地本回合再产拒绝（实际：{why2}）");
                }
                else
                {
                    Debug.LogWarning("[Verify][LandPool] 当前非 p1 主阶段——TapLandForElement 色闸断言跳过（规划八用例不受影响）");
                }
            }
            finally
            {
                pool.PooledCards.Clear();
                foreach (var pc in savedPooled) pool.PooledCards.Add(pc);
                foreach (var kv in savedWallet) pool.TempElements[kv.Key] = kv.Value;
                pool.TapsThisTurn = savedTaps;
            }
        }
    }
}
