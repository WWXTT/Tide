using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI.NeuralEnv;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// A0 逐张弃探针（2026-11-24 回到一期训练目标·阶段 A0 验收，临时——B1 后删）：
    /// 模型座回合末 9 张手牌/上限 7 → EnforceHandLimitAsync 有界循环**恰 2 个连续弃牌步**、
    /// 每步只弃 1 张、终态 7 张；每步行数=当前手牌数、特征 [10]=1、Targets=单卡、两两不同；
    /// 剩余张数（Snapshot.over）随步递减；CompleteDefault 循环放行到不再挂起；
    /// 未绑定（非模型座）TryPark=false（一次选完旧行为由脚本局对拍覆盖，不在本探针重演——
    /// 裸进程无 AI 响应器，RequestAsync 会真挂起）。
    /// 用法：dotnet run --project TideSim -- --a0-discard-probe
    /// </summary>
    internal static class A0DiscardProbe
    {
        private static int _fail;

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            Console.WriteLine("【A0 逐张弃探针：模型座回合末 9/上限7 → 恰 2 个连续弃牌步】");
            var catalog = CardCatalog.LoadAll().ToList();
            var unit = catalog.First(c => c.CardName == "教学·2");
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 12; i++) deck1.Add(unit);
            var deck2 = new List<CardData>();
            for (int i = 0; i < 8; i++) deck2.Add(unit);
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));

            // 回合方为弃牌座（开局回合方手牌无上限闸——HandOverflowProbe 同口径）
            var me = core.TurnEngine.TurnPlayer;
            var zm = core.ZoneManager;
            HandLimitDiscardGate.Reset();
            HandLimitDiscardGate.Bind(p => ReferenceEquals(p, me));

            // ── ① 主链：9 张 → TurnEndEvent → 恰 2 步 ──
            FillHandTo(zm, me, 9);
            int hand0 = zm.GetCards(me, Zone.Hand).Count;
            int grave0 = zm.GetCards(me, Zone.Graveyard).Count;
            Assert(hand0 == 9, $"夹具：回合方手牌恰 9 张（实际 {hand0}）");

            EventManager.Instance.Publish(new TurnEndEvent { TurnPlayer = me }); // 同一 OnTurnEnded 入口
            Assert(HandLimitDiscardGate.HasPending, "TurnEndEvent 后同步 Park（模型座）");

            int parks = 0;
            int[] rowsPerStep = { 0, 0 };
            int[] overPerStep = { 0, 0 };
            while (HandLimitDiscardGate.HasPending && parks < 8)
            {
                var snap = HandLimitDiscardGate.Snapshot();
                overPerStep[parks] = snap.over;
                var lae = LegalActionEnumerator.EnumerateDiscardChoices(core, me, snap.cards);
                rowsPerStep[parks] = lae.Actions.Count;
                if (parks == 0)
                {
                    Assert(lae.Actions.Count == 9
                            && lae.Actions.All(r => r.Type == TideActionType.DiscardChoice && r.Targets.Count == 1)
                            && lae.Actions.Select(r => r.Targets[0]).Distinct().Count() == 9,
                        $"第 1 步行集：9 张手牌 → 9 行（实际 {lae.Actions.Count}）/Targets=1/两两不同");
                    bool featOk = true;
                    for (int i = 0; i < lae.Actions.Count; i++)
                        if (lae.Features[i * TideObservation.NAction + 10] != 1f) { featOk = false; break; }
                    Assert(featOk, "第 1 步特征 [10]=1（单卡行）");
                }
                var first = snap.cards[0]; // 模拟模型选第一行
                HandLimitDiscardGate.Complete(new List<Card> { first }); // 内联余段：入墓+重算+必要时再挂
                parks++;
            }
            var hand1 = zm.GetCards(me, Zone.Hand).Count;
            var grave1 = zm.GetCards(me, Zone.Graveyard).Count;
            Console.WriteLine($"    · 步数={parks} 各步行数=[{rowsPerStep[0]},{rowsPerStep[1]}] 剩余over=[{overPerStep[0]},{overPerStep[1]}] 手牌 9→{hand1} 墓地 +{grave1 - grave0}");
            Assert(parks == 2 && hand1 == 7 && grave1 - grave0 == 2 && !HandLimitDiscardGate.HasPending,
                $"引擎逐张弃：9→7 恰 2 个连续弃牌步（parks={parks} 手牌={hand1} 入墓={grave1 - grave0}）");
            Assert(rowsPerStep[0] == 9 && rowsPerStep[1] == 8, $"第 2 步行集=弃后 8 张手牌（[{rowsPerStep[0]},{rowsPerStep[1]}]）");
            Assert(overPerStep[0] == 2 && overPerStep[1] == 1, $"剩余张数随步递减 2→1（[{overPerStep[0]},{overPerStep[1]}]）");

            // ── ② CompleteDefault 循环放行：再抬到 9 → 强收一次调用清空全部挂起 ──
            FillHandTo(zm, me, 9);
            EventManager.Instance.Publish(new TurnEndEvent { TurnPlayer = me });
            Assert(HandLimitDiscardGate.HasPending, "二次回合末 → 再次 Park");
            HandLimitDiscardGate.CompleteDefault(); // 应内部循环放行到不再挂起
            var hand2 = zm.GetCards(me, Zone.Hand).Count;
            Assert(!HandLimitDiscardGate.HasPending && hand2 == 7,
                $"CompleteDefault 一次调用循环放行到 7（挂起余={HandLimitDiscardGate.HasPending} 手牌={hand2}）");

            // ── ③ 未绑定（非模型座）：TryPark=false ──
            HandLimitDiscardGate.Bind(null);
            Assert(!HandLimitDiscardGate.TryPark(me, zm.GetCards(me, Zone.Hand), 1),
                "未绑定 TryPark=false（非模型座回落 RequestAsync 一次选完——行为不变由脚本局对拍覆盖）");

            HandLimitDiscardGate.Reset();
            Console.WriteLine(_fail == 0 ? "A0 逐张弃探针：全部通过" : $"A0 逐张弃探针：{_fail} 项失败");
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>把手牌抽到目标张数（从牌库搬；回合方无上限闸，直接 Move 成功）。</summary>
        private static void FillHandTo(ZoneManager zm, Player me, int target)
        {
            while (zm.GetCards(me, Zone.Hand).Count < target)
            {
                var next = zm.GetCards(me, Zone.Deck).FirstOrDefault();
                if (next == null) break;
                zm.MoveCard(next, me, Zone.Deck, Zone.Hand);
            }
        }
    }
}
