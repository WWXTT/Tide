using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 场景回归（2026-11-04 用户口径）——红快攻三张速度牌的准入闸 + 拆波闸：
    /// · 血偿：只处理生命 &gt;4 的目标；没有就留着；手里没别的牌可出才打脸。
    /// · 连击：只给「能攻击且攻击力 ≥4」的单位（代价要炸一张地，乱贴不划算）。
    /// · 拆波：仅当有 ≥2 个可攻击且**同攻**的单位、且对手留费 ≥4 时，交错召唤与攻击。
    /// 用法：dotnet run --project TideSim -- --scenario
    /// </summary>
    internal static class ScenarioCheck
    {
        private static int _fail;

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"{(ok ? "  ✓" : "  ✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            Console.WriteLine("【场景回归】红快攻：血偿/连击准入 + 拆波闸");
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var core = GameCore.Instance;
            SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
            core.InitGame(BuildRedDeck(), BuildRedDeck(), copiesPerCard: 1, rngSeed: 20261110,
                skillCardId1: null, skillCardId2: null,
                tokenBindings1: null, tokenBindings2: null, ai1: true, ai2: true);
            var p1 = core.Player1;
            var p2 = core.Player2;
            var strategy = AiStrategy.RedAggro;

            // ---- 盘面：P1 一只 2/2 火苗（可攻击）；P2 一只 2 血小怪 ----
            var fireSeed = TakeFrom(core, p1, "火苗", Zone.Hand) ?? TakeFrom(core, p1, "火苗", Zone.Deck);
            Assert(fireSeed != null, "夹具：拿得到火苗");
            if (fireSeed == null) return 1;
            core.ZoneManager.TryAddToBattlefield(fireSeed, p1);
            core.ZoneManager.TryAddToBattlefield(MakeUnit("测试小怪", 0, 2), p2);

            var chase = TakeFrom(core, p1, "追击", Zone.Deck) ?? TakeFrom(core, p1, "追击", Zone.Hand);
            var blood = TakeFrom(core, p1, "血偿", Zone.Deck) ?? TakeFrom(core, p1, "血偿", Zone.Hand);
            ToHand(core, p1, chase);
            ToHand(core, p1, blood);

            // ---- ① 连击：没有 ≥4 攻的载体 → 留手（2/2 贴上不划算，还赔一张地） ----
            Assert(chase != null && strategy.ShouldHold(core, p1, chase), "① 连击：无 ≥4 攻载体 → 留手");

            // ---- ② 连击：出现 4 攻载体 → 打 ----
            var big4 = MakeUnit("测试四攻", 4, 2);
            core.ZoneManager.TryAddToBattlefield(big4, p1);
            Assert(chase != null && strategy.ShouldHold(core, p1, chase), "② 连击：有 4 攻载体仍主阶段留手（攻击前一拍才贴）");

            // ---- ③ 血偿：敌方只有 2 血小怪、手里还有别的牌 → 留手 ----
            Assert(blood != null && strategy.ShouldHold(core, p1, blood), "③ 血偿：没有 >4 血目标（还有牌可出）→ 留手");

            // ---- ④ 血偿：出现 7 血目标 → 打 ----
            var big7 = MakeUnit("测试大个", 0, 7);
            core.ZoneManager.TryAddToBattlefield(big7, p2);
            Assert(blood != null && strategy.ShouldHold(core, p1, blood), "④ 血偿：有 7 血目标且场上有攻击手 → 留手等攻击前一拍");

            // ---- ⑤ 血偿：手里没别的牌可出 → 直接打脸（不留） ----
            core.ZoneManager.MoveCard(big7, p2, Zone.Battlefield, Zone.Graveyard);
            foreach (var c in (core.ZoneManager.GetCards(p1, Zone.Hand) ?? new List<Card>()).ToList())
                if (!ReferenceEquals(c, blood)) core.ZoneManager.MoveCard(c, p1, Zone.Hand, Zone.Deck);
            Assert(blood != null && !strategy.ShouldHold(core, p1, blood), "⑤ 血偿：无牌可出 → 打脸（不留）");

            // ---- ⑥ 拆波：混编不拆；同攻 2 手 + 对手留费才拆 ----
            Assert(!strategy.WantEarlyCombat(core, p1), "⑥ 混编（4 攻 + 2 攻）→ 不拆");
            core.ZoneManager.MoveCard(big4, p1, Zone.Battlefield, Zone.Graveyard);
            var second = TakeFrom(core, p1, "火苗", Zone.Deck);
            if (second != null)
            {
                core.ZoneManager.TryAddToBattlefield(second, p1);
                Assert(!strategy.WantEarlyCombat(core, p1), "⑥ 对手没留费 → 同攻也不拆");

                core.ElementPool.AddMana(p2, ManaType.Red, null, 4);
                Assert(core.ElementPool.GetRemainingElementTotal(p2) >= 4, "预置：对手留 4 费");
                Assert(strategy.WantEarlyCombat(core, p1), "⑥ 同攻 2/2 + 2/2 + 对手留 4 费 → 拆波");
            }

            Console.WriteLine(_fail == 0 ? "场景回归：通过" : $"场景回归：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        // ======================================== 夹具 ========================================

        private static List<CardData> BuildRedDeck()
        {
            var ids = new[]
            {
                "C_138CE290", "C_138CE290", "C_138CE290", "C_138CE290", "C_138CE290", "C_138CE290",
                "C_57BA44EC", "C_57BA44EC", "C_5F93DCDD", "C_5F93DCDD",
                "C_7118DF4D", "C_7118DF4D", "C_19E67F54", "C_19E67F54", "C_E105FABA", "C_E105FABA",
            };
            var list = new List<CardData>();
            foreach (var id in ids)
            {
                var d = CardCatalog.GetById(id);
                if (d != null) list.Add(d);
            }
            return list;
        }

        private static Card TakeFrom(GameCore core, Player p, string name, Zone zone)
            => (core.ZoneManager.GetCards(p, zone) ?? new List<Card>())
                .FirstOrDefault(c => c is IHasName n && n.CardName == name);

        private static void ToHand(GameCore core, Player p, Card card)
        {
            if (card == null || card.GetZone() == Zone.Hand) return;
            core.ZoneManager.MoveCard(card, p, card.GetZone(), Zone.Hand);
        }

        private static Card MakeUnit(string name, int power, int life, bool guardian = false)
            => new CardWrapper(new CardData
            {
                ID = "T_" + name,
                CardName = name,
                Supertype = Cardtype.Creature,
                Power = power,
                Life = life,
                Keywords = guardian ? new List<string> { "Guardian" } : new List<string>(),
            });
    }
}

