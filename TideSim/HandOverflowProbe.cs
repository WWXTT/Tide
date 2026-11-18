using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 手牌上限 / 溢出进除外区 探针（2026-11-18 规则改版验收；同日 27f7c2f6 后按「7/回合方」定案改口径）：
    /// 规则口径 = 手牌上限基值 7（RuleHooks.DefaultHandLimit / ValueSystemConfig GameRules.DefaultHandLimit），
    /// **自己回合可溢出**（HandAtLimit 对回合方恒 false）；只有**非回合方**到达上限后
    /// 「新加入手牌的卡不进手牌、改投除外区」（ZoneContainer.Add/Move 同口）。
    /// 用法：dotnet run --project TideSim -- --hand-overflow-probe
    /// </summary>
    internal static class HandOverflowProbe
    {
        private const string CardId = "C_F245F2F9"; // 小精灵（任意普通卡，仅用于搬移）
        private const int RngSeed = 20261118;
        private static int _fail;
        private static GameBoard.BoardState _board;

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            CardCatalog.LoadAll();
            Console.WriteLine("【手牌上限 7 / 回合方溢出豁免·非回合方改投除外区 探针】");
            var core = Setup();
            try
            {
                // 回合方/非回合方按当前对局实际归属取（不硬编码先手是谁）
                var turnPlayer = core.TurnEngine.TurnPlayer;
                Assert(turnPlayer != null, "开局有回合方（TurnEngine.TurnPlayer 非空）");
                var foe = ReferenceEquals(turnPlayer, core.Player1) ? core.Player2 : core.Player1;
                Console.WriteLine($"    · 回合方 = {turnPlayer.Name}，非回合方 = {foe.Name}");
                var zm = core.ZoneManager;
                var foeContainer = zm.GetZoneContainer(foe);

                int limit = RuleHooks.GetHandLimit(foe);
                Assert(limit == 7, $"手牌上限基值 = 7（实际 {limit}）");
                int foeHand0 = zm.GetCards(foe, Zone.Hand).Count;
                int foeExile0 = zm.GetCards(foe, Zone.Exile).Count;
                Console.WriteLine($"    · 非回合方开局手牌 {foeHand0} 张，除外区 {foeExile0} 张");

                // ① 回合方溢出豁免：自己回合不限，连抽超上限全留手牌、零除外
                int ownHand0 = zm.GetCards(turnPlayer, Zone.Hand).Count;
                int ownOver = limit - ownHand0 + 3;
                int ownDrawn = 0;
                for (int i = 0; i < ownOver; i++)
                    if (zm.DrawCard(turnPlayer) != null) ownDrawn++;
                Assert(ownDrawn == ownOver, $"回合方连抽 {ownOver} 张全成功（实际 {ownDrawn}）");
                Assert(zm.GetCards(turnPlayer, Zone.Hand).Count == ownHand0 + ownOver,
                    $"回合方手牌 {ownHand0}+{ownOver} 全留手牌（实际 {zm.GetCards(turnPlayer, Zone.Hand).Count}）——超上限 {limit} 不投");
                Assert(zm.GetCards(turnPlayer, Zone.Exile).Count == 0, "回合方除外区 0（自己回合可溢出）");

                // ② 非回合方抽到正好上限：到上限未超，不投
                int need = limit - foeHand0;
                int drawnOk = 0;
                for (int i = 0; i < need; i++)
                    if (zm.DrawCard(foe) != null) drawnOk++;
                Assert(drawnOk == need, $"非回合方抽 {need} 张即可到上限 {limit}（实际成功 {drawnOk}）");
                Assert(zm.GetCards(foe, Zone.Hand).Count == limit, $"非回合方手牌 = {limit}");
                Assert(zm.GetCards(foe, Zone.Exile).Count == foeExile0, $"此阶段除外区不增加（{foeExile0}）");

                // ③ 再抽一张：应进除外区
                var extra = zm.DrawCard(foe);
                Assert(extra != null, "满手后仍能抽牌（返回卡对象）");
                Assert(zm.GetCards(foe, Zone.Hand).Count == limit, $"手牌仍为 {limit}（实际 {zm.GetCards(foe, Zone.Hand).Count}）");
                Assert(zm.GetCards(foe, Zone.Exile).Count == foeExile0 + 1, $"除外区 +1（{foeExile0} → {zm.GetCards(foe, Zone.Exile).Count}）");
                Assert(extra != null && extra.GetZone() == Zone.Exile, $"溢出卡落点 = 除外区（实际 {extra?.GetZone()}）");

                // ④ 腾出一格后再抽：回手牌，除外区不变
                var outCard = zm.GetCards(foe, Zone.Hand).Last();
                foeContainer.Move(outCard, Zone.Hand, Zone.Graveyard);
                var back = zm.DrawCard(foe);
                Assert(back != null && back.GetZone() == Zone.Hand, $"腾格后抽牌回手牌（落点 {back?.GetZone()}）");
                Assert(zm.GetCards(foe, Zone.Hand).Count == limit, $"手牌回到 {limit}");
                Assert(zm.GetCards(foe, Zone.Exile).Count == foeExile0 + 1, "除外区不再增加");

                // ⑤ Add 路径（无源区新落位：衍生物/临时复制/偷取）同样受闸
                var fromDeck = (zm.GetCards(foe, Zone.Deck) ?? new List<Card>()).FirstOrDefault();
                Assert(fromDeck != null, "牌库还有卡可用于 Add 路径测试");
                if (fromDeck != null)
                {
                    foeContainer.Add(fromDeck, Zone.Hand);
                    Assert(fromDeck.GetZone() == Zone.Exile, $"Add 路径满手同样改投除外区（落点 {fromDeck.GetZone()}）");
                }
            }
            finally { Teardown(); }

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "手牌上限探针：通过" : $"手牌上限探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        private static GameCore Setup()
        {
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            var deck2 = new List<CardData>();
            // 双方各 30：回合方连抽 10 / 非回合方抽 9+Add 1，都得够抽不疲劳
            for (int i = 0; i < 30; i++) deck1.Add(CardCatalog.GetById(CardId));
            for (int i = 0; i < 30; i++) deck2.Add(CardCatalog.GetById(CardId));

            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;
            _board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.FlankNeighbors;
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole;
            core.StartGame();
            return core;
        }

        private static void Teardown()
        {
            _board?.Dispose();
            _board = null;
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
        }
    }
}
