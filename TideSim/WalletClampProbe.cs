using System;
using System.Collections.Generic;
using CardCore;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 临时元素持有钳探针（2026-11-21 定案验收）：每类型钱包余额 ≤ 地牌上限，超发即弃——
    /// 入账单源 CreditTempWallet（AddMana / 生物产色 / 三连三全部经此钳）。
    ///
    /// 场景（HandOverflowProbe 同款直建 GameCore，断言只认外部证据：实发返回值 /
    /// TempElements 余额 / ElementPoolGainEvent 计数）：
    /// · A 超发钳：回合 1（上限 1）AddMana +5 灰 → 实发 1、余额 1、事件恰一条（Amount=实发）；
    /// · B 顶格零发：再 +1 → 实发 0、余额不变、无新事件；
    /// · C 每类型独立钳：上限 1 下红/蓝/绿各 +1 全实发（若误做成全类型总量钳，此处红）；
    /// · D 上限成长放行：GlobalTurnIndex 推到 4 → +3 实发 3 余额 4 顶格；再 +2 实发 0；
    /// · E 回合末清空后照常入账：OnTurnEnd 归零再 +2 Any → 实发 2（历史钳不留尾）。
    /// 用法：dotnet run --project TideSim -- --wallet-clamp-probe
    /// </summary>
    internal static class WalletClampProbe
    {
        private const string CardId = "C_F245F2F9"; // 小精灵（任意普通卡，仅供 InitGame 构筑牌库）
        private const int RngSeed = 20261121;
        private static int _fail;
        private static int _gainEvents;

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            CardCatalog.LoadAll();
            Console.WriteLine("【临时元素持有钳探针】每类型钱包余额 ≤ 地牌上限（CreditTempWallet 单源，超发即弃）");
            try
            {
                var core = Setup();
                var ep = core.ElementPool;
                var p1 = core.Player1;
                var pool1 = ep.GetPool(p1);

                Action<ElementPoolGainEvent> gainListener = e => { if (e.Player == p1) _gainEvents++; };
                EventManager.Instance.Subscribe(gainListener);

                int cap = ep.GetLandCap(p1);
                Assert(cap == 1, $"开局（回合 1）地牌上限 = 1（实际 {cap}）");

                // A 超发钳：请求 5 只实发 1（余量=上限 1 −余额 0）
                int granted = ep.AddMana(p1, ManaType.Gray, null, 5);
                int bal = pool1.TempElements[ManaType.Gray];
                Assert(granted == 1 && bal == 1, $"A 超发钳：+5 灰 → 实发 {granted}、余额 {bal}（期望 1/1）");
                Assert(_gainEvents == 1, $"A 事件恰一条（实际 {_gainEvents}，Amount=实发量）");

                // B 顶格零发：无可观测变化（余额不动、不发事件）
                granted = ep.AddMana(p1, ManaType.Gray, null, 1);
                bal = pool1.TempElements[ManaType.Gray];
                Assert(granted == 0 && bal == 1 && _gainEvents == 1,
                    $"B 顶格零发：实发 {granted}、余额 {bal}、事件仍 {_gainEvents} 条");

                // C 每类型独立钳：三连三口径（红/蓝/绿各 +1）在上限 1 下互不挤占
                int r = ep.CreditTempWallet(p1, ManaType.Red, 1);
                int b = ep.CreditTempWallet(p1, ManaType.Blue, 1);
                int g = ep.CreditTempWallet(p1, ManaType.Green, 1);
                Assert(r == 1 && b == 1 && g == 1
                    && pool1.TempElements[ManaType.Red] == 1
                    && pool1.TempElements[ManaType.Blue] == 1
                    && pool1.TempElements[ManaType.Green] == 1,
                    $"C 每类型独立钳：红/蓝/绿各 +1 全实发（{r}/{b}/{g}——总量钳会在此误伤）");

                // D 上限成长放行：直接推曲线索引（GetLandCap 单源读 GlobalTurnIndex，无需驱动回合引擎）
                pool1.GlobalTurnIndex = 4;
                Assert(ep.GetLandCap(p1) == 4, $"D 上限成长：GlobalTurnIndex=4 → 上限 4（实际 {ep.GetLandCap(p1)}）");
                granted = ep.AddMana(p1, ManaType.Gray, null, 3);
                bal = pool1.TempElements[ManaType.Gray];
                Assert(granted == 3 && bal == 4, $"D 成长后放行：+3 灰 → 实发 {granted}、余额 {bal}（期望 3/4 顶格）");
                granted = ep.AddMana(p1, ManaType.Gray, null, 2);
                Assert(granted == 0 && pool1.TempElements[ManaType.Gray] == 4,
                    $"D 再顶格零发（实发 {granted}，余额 {pool1.TempElements[ManaType.Gray]}）");

                // E 回合末清空后照常入账：钳只看当前余额，清零后额度全额恢复
                ep.OnTurnEnd(p1);
                Assert(pool1.TempElements[ManaType.Gray] == 0 && pool1.TempElements[ManaType.Any] == 0,
                    "E 回合末清空（含未触碰的 Any 位）");
                granted = ep.AddMana(p1, ManaType.Any, null, 2);
                Assert(granted == 2 && pool1.TempElements[ManaType.Any] == 2,
                    $"E 清空后再入账：+2 Any → 实发 {granted}、余额 {pool1.TempElements[ManaType.Any]}（期望 2/2）");

                EventManager.Instance.Unsubscribe(gainListener);
            }
            finally { Teardown(); }

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "临时元素持有钳探针：通过" : $"临时元素持有钳探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        private static GameCore Setup()
        {
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            var deck2 = new List<CardData>();
            for (int i = 0; i < 30; i++) deck1.Add(CardCatalog.GetById(CardId));
            for (int i = 0; i < 30; i++) deck2.Add(CardCatalog.GetById(CardId));

            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;
            core.StartGame();
            return core;
        }

        private static void Teardown()
        {
            // 钳探针不建棋盘（无邻接解析器可漏）——只重置事件总线防跨探针订阅残留
            EventManager.ResetInstance();
        }
    }
}
