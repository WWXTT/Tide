using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 手牌上限 / 溢出进除外区 探针（2026-11-18 规则改版验收）：
    /// 规则口径 = 手牌上限基值 12（RuleHooks.DefaultHandLimit / ValueSystemConfig GameRules.DefaultHandLimit），
    /// 到上限后「新加入手牌的卡不进手牌、改投除外区」（ZoneContainer.Add/Move 同口）。
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
            Console.WriteLine("【手牌上限 12 / 溢出进除外区 探针】");
            var core = Setup();
            try
            {
                var p1 = core.Player1;
                var zm = core.ZoneManager;
                var container = zm.GetZoneContainer(p1);

                int limit = RuleHooks.GetHandLimit(p1);
                Assert(limit == 12, $"手牌上限基值 = 12（实际 {limit}）");
                int hand0 = zm.GetCards(p1, Zone.Hand).Count;
                int exile0 = zm.GetCards(p1, Zone.Exile).Count;
                Console.WriteLine($"    · 开局手牌 {hand0} 张，除外区 {exile0} 张");

                // ① 抽到正好上限
                int need = limit - hand0;
                int drawnOk = 0;
                for (int i = 0; i < need; i++)
                    if (zm.DrawCard(p1) != null) drawnOk++;
                Assert(drawnOk == need, $"抽 {need} 张即可到上限 {limit}（实际成功 {drawnOk}）");
                Assert(zm.GetCards(p1, Zone.Hand).Count == limit, $"手牌 = {limit}");
                Assert(zm.GetCards(p1, Zone.Exile).Count == exile0, $"此阶段除外区不增加（{exile0}）");

                // ② 再抽一张：应进除外区
                var extra = zm.DrawCard(p1);
                Assert(extra != null, "满手后仍能抽牌（返回卡对象）");
                Assert(zm.GetCards(p1, Zone.Hand).Count == limit, $"手牌仍为 {limit}（实际 {zm.GetCards(p1, Zone.Hand).Count}）");
                Assert(zm.GetCards(p1, Zone.Exile).Count == exile0 + 1, $"除外区 +1（{exile0} → {zm.GetCards(p1, Zone.Exile).Count}）");
                Assert(extra != null && extra.GetZone() == Zone.Exile, $"溢出卡落点 = 除外区（实际 {extra?.GetZone()}）");

                // ③ 腾出一格后再抽：回手牌，除外区不变
                var outCard = zm.GetCards(p1, Zone.Hand).Last();
                container.Move(outCard, Zone.Hand, Zone.Graveyard);
                var back = zm.DrawCard(p1);
                Assert(back != null && back.GetZone() == Zone.Hand, $"腾格后抽牌回手牌（落点 {back?.GetZone()}）");
                Assert(zm.GetCards(p1, Zone.Hand).Count == limit, $"手牌回到 {limit}");
                Assert(zm.GetCards(p1, Zone.Exile).Count == exile0 + 1, "除外区不再增加");

                // ④ Add 路径（无源区新落位：衍生物/临时复制/偷取）同样受闸
                var fromDeck = (zm.GetCards(p1, Zone.Deck) ?? new List<Card>()).FirstOrDefault();
                Assert(fromDeck != null, "牌库还有卡可用于 Add 路径测试");
                if (fromDeck != null)
                {
                    container.Add(fromDeck, Zone.Hand);
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
            for (int i = 0; i < 30; i++) deck1.Add(CardCatalog.GetById(CardId));
            deck2.Add(CardCatalog.GetById(CardId));

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
