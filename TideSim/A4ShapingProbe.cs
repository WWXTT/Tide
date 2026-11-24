using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI.NeuralEnv;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// A4 塑形单价探针（2026-11-24 回到一期训练目标·阶段 A4 验收，临时——B1 后删）：
    /// 用户口径「手牌 3 / 地牌 2 / 横置净 +1，不对关键牌做特殊约束」——
    /// ① 权重常量钉死（Hand=3/Land=2/Mana=1/Any=1/Life=1）；
    /// ② 手牌项：7 张（上限）时 Φ 手牌项 = 21；第 8 张仍 +3（不翻倍，与「超出部分回合末
    ///    先 +3 后 −3」自洽）；
    /// ③ 地 +1 块 = +2；④ 横置产色 = +1（地价不变 + 元素 1.0）；⑤ Any 彩 +1（不溢价）。
    /// 用法：dotnet run --project TideSim -- --a4-shaping-probe
    /// </summary>
    internal static class A4ShapingProbe
    {
        private static int _fail;

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        private static float Near(float v) => (float)Math.Round(v, 4);

        public static int Run()
        {
            Console.WriteLine("【A4 塑形单价探针：手牌 3 / 地牌 2 / 横置净 +1】");
            var catalog = CardCatalog.LoadAll().ToList();
            var unit = catalog.First(c => c.CardName == "教学·2");
            var core = GameCore.Instance;
            var deck1 = new List<CardData>();
            for (int i = 0; i < 12; i++) deck1.Add(unit);
            var deck2 = new List<CardData>();
            for (int i = 0; i < 8; i++) deck2.Add(unit);
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));

            // ── ① 权重常量（口径钉死） ──
            Assert(Near(FieldValueReward.HandWeight) == 3f, $"HandWeight = {FieldValueReward.HandWeight}（期望 3）");
            Assert(Near(FieldValueReward.LandTokenWeight) == 2f, $"LandTokenWeight = {FieldValueReward.LandTokenWeight}（期望 2）");
            Assert(Near(FieldValueReward.ManaWeight) == 1f && Near(FieldValueReward.WildManaWeight) == 1f,
                $"ManaWeight/Any = {FieldValueReward.ManaWeight}/{FieldValueReward.WildManaWeight}（期望 1/1 不溢价）");
            Assert(Near(FieldValueReward.LifeWeight) == 1f, $"LifeWeight = {FieldValueReward.LifeWeight}（期望 1）");

            var me = core.TurnEngine.TurnPlayer;
            var zm = core.ZoneManager;

            // ── ② 手牌项：7 张 = 21；第 8 张仍 +3 ──
            while (zm.GetCards(me, Zone.Hand).Count < 7)
            {
                var c = zm.GetCards(me, Zone.Deck).FirstOrDefault();
                if (c == null) break;
                zm.MoveCard(c, me, Zone.Deck, Zone.Hand);
            }
            while (zm.GetCards(me, Zone.Hand).Count > 7)
            {
                var c = zm.GetCards(me, Zone.Hand).Last();
                zm.MoveCard(c, me, Zone.Hand, Zone.Deck);
            }
            int hand7 = zm.GetCards(me, Zone.Hand).Count;
            float withHand7 = FieldValueReward.TotalValue(core, me, true);
            float noHand7 = FieldValueReward.TotalValue(core, me, false);
            Assert(hand7 == 7 && Near(withHand7 - noHand7) == 21f,
                $"手牌 7 张（上限）→ Φ 手牌项 = {Near(withHand7 - noHand7)}（期望 21 = 7×3）");
            float pot7 = FieldValueReward.ComputePotential(core, me, true);
            var extra = zm.GetCards(me, Zone.Deck).FirstOrDefault();
            zm.MoveCard(extra, me, Zone.Deck, Zone.Hand);
            float pot8 = FieldValueReward.ComputePotential(core, me, true);
            float withHand8 = FieldValueReward.TotalValue(core, me, true);
            float noHand8 = FieldValueReward.TotalValue(core, me, false);
            Assert(Near(pot8 - pot7) == 3f,
                $"第 8 张手牌 → ΔΦ = {Near(pot8 - pot7)}（期望 +3，不翻倍——回合末弃掉 −3 自洽）");
            Assert(Near(withHand8 - noHand8) == 24f, $"8 张手牌项 = {Near(withHand8 - noHand8)}（期望 24）");

            // ── ③ 地 +1 块 = +2（直写 PooledCards = 夹具播种既定口径） ──
            var pool = core.ElementPool.GetPool(me);
            int pooled0 = pool.PooledCards.Count;
            float res0 = FieldValueReward.ResourceValue(core, me, true);
            var landCard = zm.GetCards(me, Zone.Deck).FirstOrDefault();
            pool.PooledCards.Add(new PooledCard(landCard, ManaType.Red));
            float res1 = FieldValueReward.ResourceValue(core, me, true);
            Assert(Near(res1 - res0) == 2f,
                $"地 +1 块（池 {pooled0}→{pool.PooledCards.Count}）→ Δ资源项 = {Near(res1 - res0)}（期望 +2）");

            // ── ④ 横置产色 = +1（地价不变〔横置不打折〕+ 元素 1.0；直写钱包） ──
            float res2 = FieldValueReward.ResourceValue(core, me, true);
            pool.PooledCards[pool.PooledCards.Count - 1].IsTapped = true; // 横置态本身不折地价
            float res3 = FieldValueReward.ResourceValue(core, me, true);
            pool.TempElements[ManaType.Red] = pool.TempElements.GetValueOrDefault(ManaType.Red) + 1;
            float res4 = FieldValueReward.ResourceValue(core, me, true);
            Assert(Near(res3 - res2) == 0f,
                $"横置地牌本体 Δ = {Near(res3 - res2)}（期望 0——地价恒定，收益在产出侧）");
            Assert(Near(res4 - res3) == 1f,
                $"横置产色（钱包 +1 红）→ Δ = {Near(res4 - res3)}（期望 +1——横置净 +1）");

            // ── ⑤ Any 彩 +1（不溢价） ──
            pool.TempElements[ManaType.Any] = pool.TempElements.GetValueOrDefault(ManaType.Any) + 1;
            float res5 = FieldValueReward.ResourceValue(core, me, true);
            Assert(Near(res5 - res4) == 1f, $"Any 彩 +1 → Δ = {Near(res5 - res4)}（期望 +1）");

            Console.WriteLine(_fail == 0 ? "A4 塑形单价探针：全部通过" : $"A4 塑形单价探针：{_fail} 项失败");
            return _fail == 0 ? 0 : 1;
        }
    }
}
