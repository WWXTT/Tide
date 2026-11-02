using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCore;
using CardCore.AI.NeuralEnv;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 观测 v9 牌库双通道回归验证器（2026-11-02 契约 v9 验收）：
    /// ① 构成对账——牌库块 [86..115] 身份多重集 ∪ 手牌区多重集 == GameCore.OriginalDeckOf
    ///   （开抽后 牌库23+手牌7=原30）；valid 槽数 == 当前牌库张数；对方隐藏手牌 zoneKind=2 且身份槽全 0。
    /// ② 排序无关——同一局面重洗牌库（区内序变、构成不变），牌库槽区逐浮点相等
    ///   （排序键位置无关，编码不泄露洗牌序；两 seed 整局对拍不成立——剩余多重集本就不同）。
    /// ③ zoneKind 抽检——战场0/元素池1/手牌2/墓地3/发动区4/场地区5/己方牌库6（[115] 显式映射）。
    /// ④ 标签静态——g[50..57] == 按 OriginalDeckOf 手工算的期望（四色逐色多计 + 费用三档
    ///   Mathf.RoundToInt 口径 + 总数），且抽 5 张后不变（随卡组创建保留，非剩余重算）。
    /// ⑤ 截断护栏——skipOpeningDraw+deferStart 双闸满 30 + 塞第 31 张：valid 仍 30、g[13] 计数 31、
    ///   第 31 张（排序末位）IndexOfCard=-1；座次切换 Build(p2)——牌库块换 p2 构成、
    ///   标签换 OriginalDeckOf(p1)；维度握手 LayoutVersion=9/116/116/58 + LAE 冒烟。
    /// 夹具按卡名解析（教学池 ID 随内容重推漂移，硬编码 ID 会假阴——LAE 验证器同款口径）。
    /// 入口：菜单 验证/观测v9牌库双通道；Run() 返回报告文本（全过=PASS 结尾）。
    /// </summary>
    public static class TideObservationDeckVerifier
    {
        private const string CardA = "火灵";     // 红低费
        private const string CardB = "火种";     // 红低费
        private const string CardC = "火炬";     // 红低费
        private const string CardD = "虚空巨像"; // 绿高费（9/9，费用 7-9 档素材）

        private readonly struct Check
        {
            public readonly bool Pass;
            public readonly string Text;
            public Check(bool pass, string text) { Pass = pass; Text = text; }
        }

        [MenuItem("Tools/验证/观测v9牌库双通道")]
        public static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var log = new StringBuilder();
            var checks = new List<Check>();
            try
            {
                var catalog = CardCatalog.LoadAll().ToList();
                TideCardIndex.Register(catalog); // 身份下标登记（未登记者记 0，对账前提）
                RunMain(catalog, log, checks);
            }
            catch (Exception ex)
            {
                checks.Add(new Check(false, $"异常中断：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"));
            }
            int fails = checks.Count(c => !c.Pass);
            log.AppendLine(fails == 0
                ? $"==== TideObservationDeckVerifier 全过（{checks.Count} 项断言）===="
                : $"==== TideObservationDeckVerifier 失败 {fails}/{checks.Count} 项 ====");
            foreach (var c in checks.Where(c => !c.Pass))
                log.AppendLine("[FAIL] " + c.Text);
            return log.ToString();
        }

        private static void RunMain(List<CardData> catalog, StringBuilder log, List<Check> checks)
        {
            var core = GameCore.Instance;
            var deck1Data = Mix(catalog, (CardA, 10), (CardB, 10), (CardC, 10)); // 30
            var deck2Data = Mix(catalog, (CardD, 10), (CardA, 10), (CardB, 10)); // 30（红+绿混色，标签口径覆盖）

            // ================= ⓪ 维度握手 =================
            checks.Add(new Check(TideObservation.LayoutVersion == 9 && TideObservation.NCard == 116
                && TideObservation.NGlobal == 58 && TideObservation.MaxCardsTotal == 116
                && TideObservation.DeckBase == 86 && TideObservation.DeckSlots == 30,
                $"布局常量期望 9/116/58/116/86/30，实际 {TideObservation.LayoutVersion}/{TideObservation.NCard}/"
                + $"{TideObservation.NGlobal}/{TideObservation.MaxCardsTotal}/{TideObservation.DeckBase}/{TideObservation.DeckSlots}"));

            // ================= ① 构成对账 + 标签静态（seed 12345·含开抽） =================
            core.InitGame(CardLoader.BuildDeck(deck1Data, 1), CardLoader.BuildDeck(deck2Data, 1), rngSeed: 12345);
            var p1 = core.Player1;
            var p2 = core.Player2;
            var zm = core.ZoneManager;
            var obs = TideObservation.Build(core, p1, false, null);
            checks.Add(new Check(obs.Cards.Length == TideObservation.MaxCardsTotal * TideObservation.NCard
                && obs.Globals.Length == TideObservation.NGlobal,
                $"数组长度 cards={obs.Cards.Length}（期望 {TideObservation.MaxCardsTotal * TideObservation.NCard}）"
                + $" globals={obs.Globals.Length}（期望 {TideObservation.NGlobal}）"));

            int deckCount1 = zm.GetCards(p1, Zone.Deck).Count;
            var deckBlock = RegionStructureIndices(obs, TideObservation.DeckBase, TideObservation.DeckSlots);
            var handRegion = RegionStructureIndices(obs, 23, 12); // 手牌区 [23..34]
            var expected1 = SortedIndices(core.OriginalDeckOf(p1));
            checks.Add(new Check(deckBlock.Count == deckCount1,
                $"牌库块 valid 数={deckBlock.Count}，期望=当前牌库张数 {deckCount1}（开抽 7 后 30-7）"));
            var actual1 = deckBlock.Concat(handRegion).OrderBy(x => x).ToList();
            checks.Add(new Check(actual1.SequenceEqual(expected1),
                $"构成对账：牌库块∪手牌区（{actual1.Count} 张）应等于 OriginalDeckOf（{expected1.Count} 张）身份多重集"));

            // 对方隐藏手牌：zoneKind=2 且身份槽全 0
            var oppHand = zm.GetCards(p2, Zone.Hand);
            bool concealedOk = oppHand.Count > 0;
            for (int i = 0; i < 12 && concealedOk; i++)
            {
                int b = (43 + 23 + i) * TideObservation.NCard;
                bool present = i < oppHand.Count;
                concealedOk &= obs.Cards[b] == (present ? 1f : 0f)                    // valid
                            && obs.Cards[b + TideObservation.ZoneKindSlot] == (present ? 2f : 0f) // zoneKind=手牌
                            && obs.Cards[b + TideObservation.StructureSlot] == 0f;   // 不开图
            }
            checks.Add(new Check(concealedOk, $"对方手牌 {oppHand.Count} 张应隐藏（valid+zoneKind=2，身份槽 0）"));

            // 标签：静态值 + 手工口径对拍（不调 DeckTagsOf，防同源假阳）
            var tagsExpected = ManualTags(core.OriginalDeckOf(p2));
            var tagsActual = new float[8];
            for (int i = 0; i < 8; i++) tagsActual[i] = obs.Globals[TideObservation.DeckTagStart + i];
            bool tagsOk = tagsExpected.Zip(tagsActual, (e, a) => Math.Abs(e - a) < 1e-4f).All(x => x);
            log.AppendLine($"[标签] 期望 [{string.Join(",", tagsExpected.Select(t => t.ToString("0")))}] "
                + $"实际 [{string.Join(",", tagsActual.Select(t => t.ToString("0")))}]（对方={OriginalSummary(core.OriginalDeckOf(p2))}）");
            checks.Add(new Check(tagsOk, "g[50..57] 应等于手工口径（四色逐色多计+三档 RoundToInt+总数）"));

            // 抽 5 张后标签不变（随卡组创建保留，非剩余重算）；牌库块随抽牌收缩且构成仍对账
            for (int i = 0; i < 5; i++) ZoneManagerExtensions.DrawCard(zm, p1);
            var obs2 = TideObservation.Build(core, p1, false, null);
            bool tagsStatic = true;
            for (int i = 0; i < 8; i++) tagsStatic &= Math.Abs(obs2.Globals[TideObservation.DeckTagStart + i] - tagsActual[i]) < 1e-6f;
            var deckBlock2 = RegionStructureIndices(obs2, TideObservation.DeckBase, TideObservation.DeckSlots);
            checks.Add(new Check(tagsStatic && deckBlock2.Count == deckCount1 - 5,
                $"抽 5 张后：标签应恒定且牌库块收缩到 {deckCount1 - 5}，实际 标签恒定={tagsStatic} 槽数={deckBlock2.Count}"));
            var actual1b = deckBlock2
                .Concat(RegionStructureIndices(obs2, 23, 12)).OrderBy(x => x).ToList();
            checks.Add(new Check(actual1b.SequenceEqual(expected1), "抽牌后对账：牌库块∪手牌区仍等于 OriginalDeckOf"));

            // IndexOfCard：己方牌库卡各得唯一槽位（排序名次），对方牌库卡 -1
            var slots1 = new HashSet<int>();
            bool idxOk = true;
            foreach (var c in zm.GetCards(p1, Zone.Deck))
            {
                int s = TideObservation.IndexOfCard(core, p1, c);
                idxOk &= s >= TideObservation.DeckBase && s < TideObservation.MaxCardsTotal && slots1.Add(s);
            }
            bool idxOpp = zm.GetCards(p2, Zone.Deck).All(c => TideObservation.IndexOfCard(core, p1, c) == -1);
            checks.Add(new Check(idxOk && idxOpp,
                $"IndexOfCard：己方 {slots1.Count} 张牌库卡应各得 [86..116) 唯一槽位（OK={idxOk}），对方牌库卡应恒 -1（OK={idxOpp}）"));

            // LAE 冒烟（动作空间 v8 未动，枚举不应受 v9 影响）
            AdvanceToMainPhase(core, p1);
            var lae = new LegalActionEnumerator();
            lae.Enumerate(core, p1);
            checks.Add(new Check(lae.Actions.Count > 0, $"LAE 冒烟行数={lae.Actions.Count}（期望 >0）"));

            // ② 排序无关：同一局面重洗牌库（构成不变、区内序变）→ 牌库槽区逐浮点相等。
            //（注：不能拿两个 seed 的整局对拍——开抽抽走的 7 张不同，剩余多重集本就不同、编码理应不同）
            var zm2 = zm;
            var orderBefore = zm2.GetCards(p1, Zone.Deck).Select(c => c.RuntimeId).ToList();
            var regionA = CopyRegion(obs2, TideObservation.DeckBase, TideObservation.DeckSlots);
            ZoneManagerExtensions.ShuffleDeck(zm2, p1);
            var orderAfter = zm2.GetCards(p1, Zone.Deck).Select(c => c.RuntimeId).ToList();
            bool orderChanged = !orderBefore.SequenceEqual(orderAfter);
            var obsB = TideObservation.Build(core, p1, false, null);
            bool orderFree = RegionEquals(regionA, obsB, TideObservation.DeckBase, TideObservation.DeckSlots);
            checks.Add(new Check(orderChanged && orderFree,
                $"排序无关：重洗牌库后区内容应逐浮点相等（洗牌确实换了序={orderChanged}，槽区恒定={orderFree}）"));

            // ③ zoneKind 抽检：各区移卡后 [115] 值（战场0/元素池1/手牌2/墓地3/发动区4/场地区5/牌库6）
            var zoneChecks = new List<(Zone zone, int expect)>
            {
                (Zone.Battlefield, 0), (Zone.ElementPool, 1), (Zone.Graveyard, 3),
                (Zone.Activation, 4), (Zone.FieldZone, 5),
            };
            var zm3 = core.ZoneManager;
            bool zkOk = zm3.GetCards(p1, Zone.Hand).Count > 0 && zm3.GetCards(p1, Zone.Deck).Count > 0;
            if (zkOk)
            {
                // 手牌（现成）与牌库顶（现成）
                var handCard = zm3.GetCards(p1, Zone.Hand)[0];
                var deckCard = zm3.GetCards(p1, Zone.Deck)[0];
                zkOk &= ZoneKindOfCard(core, p1, obsB, handCard) == 2 && ZoneKindOfCard(core, p1, obsB, deckCard) == 6;
                foreach (var (zone, expect) in zoneChecks)
                {
                    var mover = TakeFromDeckOrHand(zm3, p1);
                    if (mover == null) { zkOk = false; break; }
                    zm3.MoveCard(mover, p1, zm3.IsCardInZone(mover, p1, Zone.Hand) ? Zone.Hand : Zone.Deck, zone);
                    var obsZ = TideObservation.Build(core, p1, false, null);
                    int got = ZoneKindOfCard(core, p1, obsZ, mover);
                    if (got != expect) { log.AppendLine($"[zoneKind] {zone} 期望 {expect} 实际 {got}"); zkOk = false; }
                }
            }
            checks.Add(new Check(zkOk, "zoneKind 抽检：战场0/元素池1/手牌2/墓地3/发动区4/场地区5/牌库6"));

            // ================= ④ 截断护栏 + 座次切换（skipOpeningDraw+deferStart 双闸：不开抽 6 张、
            //     不进回合抽 1 张，牌库保持满 30——任一单用都会少一张） =================
            core.InitGame(CardLoader.BuildDeck(deck1Data, 1), CardLoader.BuildDeck(deck2Data, 1),
                rngSeed: 7, skipOpeningDraw: true, deferStart: true);
            var zm4 = core.ZoneManager;
            var obsC = TideObservation.Build(core, core.Player1, false, null);
            var full = RegionStructureIndices(obsC, TideObservation.DeckBase, TideObservation.DeckSlots);
            checks.Add(new Check(full.Count == 30 && SortedIndices(core.OriginalDeckOf(core.Player1)).SequenceEqual(full.OrderBy(x => x).ToList()),
                $"双闸满库：牌库块应满 30 且构成=OriginalDeckOf，实际 valid={full.Count}"));

            // 第 31 张（衍生物入库等价路径：直接 Add 进牌库）→ 截断 + 计数照走 g[13]
            var spare = CardLoader.BuildDeck(new List<CardData> { ByName(catalog, CardC) }, 1)[0];
            spare.SetController(core.Player1);
            zm4.GetZoneContainer(core.Player1).Add(spare, Zone.Deck);
            var obsD = TideObservation.Build(core, core.Player1, false, null);
            var truncated = RegionStructureIndices(obsD, TideObservation.DeckBase, TideObservation.DeckSlots);
            int countNow = zm4.GetCards(core.Player1, Zone.Deck).Count;
            int g13Raw = Mathf.RoundToInt(obsD.Globals[13] * 60f);
            checks.Add(new Check(countNow == 31 && truncated.Count == 30 && g13Raw == 31,
                $"截断护栏：31 张时 valid 槽应仍 30、g[13] 计数应 31，实际 张数={countNow} 槽数={truncated.Count} g13={g13Raw}"));
            // 溢出的恰一张（哈希组序不定，不点名 spare——同哈希组内 RuntimeId 决定，组本身可能不在末位）
            int overflow = 0;
            var encoded = new HashSet<int>();
            foreach (var c in zm4.GetCards(core.Player1, Zone.Deck))
            {
                int s = TideObservation.IndexOfCard(core, core.Player1, c);
                if (s < 0) overflow++;
                else encoded.Add(s);
            }
            checks.Add(new Check(overflow == 1 && encoded.Count == 30,
                $"31 张牌库：应恰 1 张溢出（IndexOfCard=-1）+ 30 唯一槽位，实际 溢出={overflow} 槽={encoded.Count}"));

            // 座次切换：Build(p2)——牌库块换 p2 构成，标签换 OriginalDeckOf(p1)
            var obsE = TideObservation.Build(core, core.Player2, false, null);
            var deckP2 = RegionStructureIndices(obsE, TideObservation.DeckBase, TideObservation.DeckSlots);
            var expectedP2 = SortedIndices(core.OriginalDeckOf(core.Player2));
            var tagsP1 = ManualTags(core.OriginalDeckOf(core.Player1));
            bool seatOk = deckP2.Count == 30 && deckP2.OrderBy(x => x).SequenceEqual(expectedP2);
            for (int i = 0; i < 8 && seatOk; i++)
                seatOk &= Mathf.Abs(obsE.Globals[TideObservation.DeckTagStart + i] - tagsP1[i]) < 1e-4f;
            checks.Add(new Check(seatOk, "座次切换 Build(p2)：牌库块=p2 卡表、g[50..57]=OriginalDeckOf(p1) 标签"));
        }

        // ======================================== 夹具辅助 ========================================

        /// <summary>推进到主阶段（Standby→SkipElementPool→…→Main；LAE 冒烟前提——LAE 验证器同款）。</summary>
        private static void AdvanceToMainPhase(GameCore core, Player p1)
        {
            int guard = 0;
            while (guard++ < 20 && (core.TurnEngine.CurrentPhase == null || core.TurnEngine.CurrentPhase.Phase != PhaseType.Main))
            {
                var ph = core.TurnEngine.CurrentPhase == null ? PhaseType.Standby : core.TurnEngine.CurrentPhase.Phase;
                if (ph == PhaseType.Standby) GameActions.SkipElementPool(core, core.TurnEngine.TurnPlayer);
                else core.TurnEngine.CheckPhaseTransition();
            }
        }

        private static List<CardData> Mix(List<CardData> catalog, params (string name, int n)[] parts)
        {
            var list = new List<CardData>();
            foreach (var (name, n) in parts)
            {
                var d = ByName(catalog, name);
                for (int i = 0; i < n; i++) list.Add(d);
            }
            return list;
        }

        private static CardData ByName(List<CardData> catalog, string name)
            => catalog.FirstOrDefault(c => c != null && c.CardName == name)
               ?? throw new InvalidOperationException($"卡池找不到「{name}」（卡名变更？验证器夹具须跟进）");

        /// <summary>观测某槽区的身份下标集（valid=1 槽读 [27] StructureSlot）。</summary>
        private static List<int> RegionStructureIndices(TideObservation obs, int baseSlot, int slots)
        {
            var list = new List<int>();
            for (int i = 0; i < slots; i++)
                if (obs.Cards[(baseSlot + i) * TideObservation.NCard] == 1f)
                    list.Add((int)obs.Cards[(baseSlot + i) * TideObservation.NCard + TideObservation.StructureSlot]);
            return list;
        }

        /// <summary>OriginalDeckOf 的期望身份下标集（升序）。</summary>
        private static List<int> SortedIndices(IReadOnlyList<CardData> deck)
            => deck.Select(d => TideCardIndex.IndexOf(d.StructureContentHash)).OrderBy(x => x).ToList();

        /// <summary>手工口径标签（独立实现，防与 DeckTagsOf 同源假阳）：四色逐色多计 +
        /// 费用档 Clamp(RoundToInt(TotalCost),1,9) 归三档 + 总数。</summary>
        private static float[] ManualTags(IReadOnlyList<CardData> deck)
        {
            var t = new float[8];
            foreach (var d in deck)
            {
                if (d == null) continue;
                t[7]++;
                var c = d.Cost;
                if (c != null)
                {
                    if (c[ManaType.Gray] > 0f) t[0]++;
                    if (c[ManaType.Red] > 0f) t[1]++;
                    if (c[ManaType.Blue] > 0f) t[2]++;
                    if (c[ManaType.Green] > 0f) t[3]++;
                }
                int tier = Mathf.Clamp(Mathf.RoundToInt(d.TotalCost), 1, 9);
                if (tier <= 2) t[4]++;
                else if (tier <= 6) t[5]++;
                else t[6]++;
            }
            return t;
        }

        private static string OriginalSummary(IReadOnlyList<CardData> deck)
            => $"{deck.Count} 张，四色[{deck.Count(d => d.Cost != null && d.Cost[ManaType.Gray] > 0f)},"
                + $"{deck.Count(d => d.Cost != null && d.Cost[ManaType.Red] > 0f)},"
                + $"{deck.Count(d => d.Cost != null && d.Cost[ManaType.Blue] > 0f)},"
                + $"{deck.Count(d => d.Cost != null && d.Cost[ManaType.Green] > 0f)}]";

        private static float[] CopyRegion(TideObservation obs, int baseSlot, int slots)
        {
            var arr = new float[slots * TideObservation.NCard];
            Array.Copy(obs.Cards, baseSlot * TideObservation.NCard, arr, 0, arr.Length);
            return arr;
        }

        private static bool RegionEquals(float[] region, TideObservation obs, int baseSlot, int slots)
        {
            for (int i = 0; i < region.Length; i++)
                if (Math.Abs(region[i] - obs.Cards[baseSlot * TideObservation.NCard + i]) > 0f) return false;
            return true;
        }

        private static int ZoneKindOfCard(GameCore core, Player me, TideObservation obs, Card c)
        {
            int slot = TideObservation.IndexOfCard(core, me, c);
            return slot < 0 ? -1 : (int)obs.Cards[slot * TideObservation.NCard + TideObservation.ZoneKindSlot];
        }

        private static Card TakeFromDeckOrHand(ZoneManager zm, Player p)
            => zm.GetCards(p, Zone.Deck).FirstOrDefault() ?? zm.GetCards(p, Zone.Hand).FirstOrDefault();
    }
}
