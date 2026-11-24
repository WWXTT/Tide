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
    /// 观测 v9 牌库双通道回归验证器（2026-11-02 契约 v9 验收；v21 牌库块语义换血后断言随改）：
    /// ① 构成对账——牌库块 [154..183] 是**唯一卡分组**（每种唯一卡一槽，[157] countInZone=组内张数）：
    ///   组数/槽序 = 当前牌库按内容哈希分组排序；各组按张数展开的多重集 == 当前牌库多重集；张数合计 ==
    ///   当前牌库张数；牌库块（展开）∪ 手牌区多重集 == GameCore.OriginalDeckOf；对方隐藏手牌 zoneKind=2
    ///   且身份槽全 0。（v20 及以前：valid 槽数 == 当前牌库张数——每张物理卡一槽。）
    /// ② 排序无关——同一局面重洗牌库（区内序变、构成不变），牌库槽区逐浮点相等
    ///   （排序键位置无关，编码不泄露洗牌序；两 seed 整局对拍不成立——剩余多重集本就不同）。
    /// ③ zoneKind 抽检——战场0/元素池1/手牌2/墓地3/发动区4/场地区5/己方牌库6（[151] 显式映射）。
    /// ④ 标签静态——g[50..57] == 按 OriginalDeckOf 手工算的期望（四色逐色多计 + 费用三档
    ///   Mathf.RoundToInt 口径 + 总数），且抽牌后不变（随卡组创建保留，非剩余重算）。
    /// ⑤ 超量与截断——skipOpeningDraw+deferStart 双闸满库：同种第 31 张入库**不再截断**（并组、张数
    ///   合计 31、g[13]=31）；座次切换 Build(p2)——牌库块换 p2 分组、标签换 OriginalDeckOf(p1)。
    /// v21（2026-11-24 观测·卡组分布与策略分界，docs/观测_卡组分布与策略分界_实施方案.md §6.1）：
    /// ⓪ 维度握手 21/158/97/20/191（CountSlot=157/OwnDeckTagStart=88/DeckTruncatedSlot=96）；
    /// ⑪ 三套预组实测：绿 60 张=**4 组 19/19/11/11**（v21 前 30 槽按物理卡截断只见 2 种）、红 30=5 组、
    ///   蓝 30=4 组；⑫ 己方标签 g[88..95] == 手工口径且抽牌后恒定（对方块回归不变）；⑬ 实例态不同
    ///   的同种卡**不并组**（合并判据=特征行全同）；⑭ 唯一卡 &gt;30 种 → 截断到 30 组 + g[96]=1 +
    ///   溢出组卡 IndexOfCard=-1；⑮ countInZone 不变量（valid=1 ⇔ ≥1、牌库块外恒 1、空槽 0）；
    /// ⑯ 战斗底盘·攻/守资格入身份（用户定案：攻/守=带发动条件的主动效果）——火灵·攻/守仅攻守
    ///   资格不同，补盲前观测逐位相同（红·快攻只见 4 种）：原子槽 [10]/[11]=Attack/Guard、结构哈希
    ///   可分、可攻击粗判 [7] 加攻资格闸。
    /// v10（2026-11-02 观测欠账四件套）：⑥ 墓地扩槽 30（12 张全编码·槽35=最新·31 张截断）；
    /// ⑦ 除外 8 入册（9 张放逐→8 编码+g[15]=9+zoneKind=9）；⑧ 被展示——对方手牌 2 张开图
    ///（其余仍隐藏）、对方牌库 2 张入尾块 [185..]（区内序）；⑨ 牌库顶——光环命中才编码+LAE
    /// 出牌行+Apply 离库，**未命中/载体离场恒空槽（反泄露硬断言）**。
    /// v15（2026-11-18 场地特殊化·拆槽）：⑩ 战场槽 [0..8] 无场地 + 场地专用槽 [189]/[190]
    ///（zoneKind=10、身份对拍、座次镜像）+ g[7]/g[8] 只数单位 + LAE 攻击场地行
    ///（TargetIndex=场地槽）；维度握手 v21 见 ⓪（FieldBase=189/FieldSlots=2 不变）。
    /// 夹具按卡名解析（教学池 ID 随内容重推漂移，硬编码 ID 会假阴——LAE 验证器同款口径）。
    /// 入口：菜单 验证/观测v9牌库双通道；Run() 返回报告文本（全过=PASS 结尾）。
    /// </summary>
    public static class TideObservationDeckVerifier
    {
        // 2026-11-03 教学池红卡改名对齐：火灵→火灵·攻/守、火种→火种·锐/厚、火炬退役
        private const string CardA = "火灵·攻";   // 红低费
        private const string CardB = "火苗";   // 红低费（0 费，2/2；原火种·锐）
        private const string CardC = "火灵·守";   // 红低费
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

            // ================= ⓪ 维度握手（v21 卡组分布与策略分界 2026-11-24：+countInZone [157]/
            //     己方牌库标签 g[88..95]/牌库截断标记 g[96]；场地块 [189]/[190] 自 v15 不变） =================
            checks.Add(new Check(TideObservation.LayoutVersion == 21 && TideObservation.NCard == 158
                && TideObservation.NGlobal == 97 && TideObservation.NAction == 20
                && TideObservation.MaxCardsTotal == 191
                && TideObservation.MaxCardsPerPlayer == 77 && TideObservation.DeckBase == 154
                && TideObservation.DeckSlots == 30 && TideObservation.DeckTopSlot == 184
                && TideObservation.OppDeckExposedBase == 185 && TideObservation.OppDeckExposedSlots == 4
                && TideObservation.FieldBase == 189 && TideObservation.FieldSlots == 2
                && TideObservation.CountSlot == 157 && TideObservation.OwnDeckTagStart == 88
                && TideObservation.DeckTruncatedSlot == 96,
                $"布局常量期望 v21：21/158/97/20/191/77/154/30/184/185/4/189/2/157/88/96，实际 "
                + $"{TideObservation.LayoutVersion}/{TideObservation.NCard}/{TideObservation.NGlobal}/{TideObservation.NAction}"
                + $"/{TideObservation.MaxCardsTotal}/{TideObservation.MaxCardsPerPlayer}/{TideObservation.DeckBase}"
                + $"/{TideObservation.DeckSlots}/{TideObservation.DeckTopSlot}/{TideObservation.OppDeckExposedBase}"
                + $"/{TideObservation.OppDeckExposedSlots}/{TideObservation.FieldBase}/{TideObservation.FieldSlots}"
                + $"/{TideObservation.CountSlot}/{TideObservation.OwnDeckTagStart}/{TideObservation.DeckTruncatedSlot}"));

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
            var deckBlock = ReadDeckBlock(obs); // v21：牌库块=唯一卡分组（身份下标, 张数）
            var handRegion = RegionStructureIndices(obs, 23, 12); // 手牌区 [23..34]
            var expected1 = SortedIndices(core.OriginalDeckOf(p1));
            checks.Add(new Check(DeckBlockMatches(deckBlock, zm.GetCards(p1, Zone.Deck)),
                $"牌库块=唯一卡分组：组数/槽序/各组张数应等于当前牌库按内容哈希分组（{deckCount1} 张），实际 {Describe(deckBlock)}"));
            var actual1 = ExpandDeckBlock(deckBlock).Concat(handRegion).OrderBy(x => x).ToList();
            checks.Add(new Check(actual1.SequenceEqual(expected1) && obs.Globals[TideObservation.DeckTruncatedSlot] == 0f,
                $"构成对账：牌库块（按张数展开）∪手牌区（{actual1.Count} 张）应等于 OriginalDeckOf（{expected1.Count} 张）身份多重集，且未截断 g[96]=0"));

            // 对方隐藏手牌：zoneKind=2 且身份槽全 0
            var oppHand = zm.GetCards(p2, Zone.Hand);
            bool concealedOk = oppHand.Count > 0;
            for (int i = 0; i < 12 && concealedOk; i++)
            {
                int b = (TideObservation.MaxCardsPerPlayer + 23 + i) * TideObservation.NCard; // 对方手牌区（v10 基址 77）
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

            // v21 己方牌库开局标签 g[88..95]：手工口径对拍 + 与对方块确实不同（p1 三种低费卡 vs p2
            // 含 10 张高费卡——费档向量不同；防「己方块抄了对方值」的同源假阳）
            var ownExpected = ManualTags(core.OriginalDeckOf(p1));
            var ownActual = new float[8];
            for (int i = 0; i < 8; i++) ownActual[i] = obs.Globals[TideObservation.OwnDeckTagStart + i];
            bool ownOk = ownExpected.Zip(ownActual, (e, a) => Math.Abs(e - a) < 1e-4f).All(x => x)
                         && !ownExpected.SequenceEqual(tagsExpected);
            log.AppendLine($"[己方标签] 期望 [{string.Join(",", ownExpected.Select(t => t.ToString("0")))}] "
                + $"实际 [{string.Join(",", ownActual.Select(t => t.ToString("0")))}]（己方={OriginalSummary(core.OriginalDeckOf(p1))}）");
            checks.Add(new Check(ownOk, "g[88..95] 应等于手工口径（己方 OriginalDeckOf），且与对方块 g[50..57] 不同"));

            // 抽牌后标签不变（随卡组创建保留，非剩余重算）；牌库块随抽牌收缩且构成仍对账。
            // 抽牌数适配手牌区容量 12：起手张数是规则表值（2026-11-24 实测 8；旧夹具按 7 写死抽 5，
            // 手牌 13 > 12 槽时第 13 张编码不进手牌区，对账假红）——这里最多抽到 12 张。
            int draws = Math.Max(0, Math.Min(5, 12 - zm.GetCards(p1, Zone.Hand).Count));
            for (int i = 0; i < draws; i++) ZoneManagerExtensions.DrawCard(zm, p1);
            var obs2 = TideObservation.Build(core, p1, false, null);
            bool tagsStatic = true;
            for (int i = 0; i < 8; i++)
                tagsStatic &= Math.Abs(obs2.Globals[TideObservation.DeckTagStart + i] - tagsActual[i]) < 1e-6f
                           && Math.Abs(obs2.Globals[TideObservation.OwnDeckTagStart + i] - ownActual[i]) < 1e-6f;
            var deckBlock2 = ReadDeckBlock(obs2);
            var deckNow = zm.GetCards(p1, Zone.Deck);
            var actual1b = ExpandDeckBlock(deckBlock2)
                .Concat(RegionStructureIndices(obs2, 23, 12)).OrderBy(x => x).ToList();
            checks.Add(new Check(tagsStatic && DeckBlockMatches(deckBlock2, deckNow) && actual1b.SequenceEqual(expected1),
                $"抽 {draws} 张后：双方标签应恒定、牌库块收缩到 {deckNow.Count} 张（{deckBlock2.Count} 组）且展开=当前牌库"
                + $"，牌库块∪手牌区仍等于 OriginalDeckOf（实际 {Describe(deckBlock2)}）"));

            // IndexOfCard：同组（同内容哈希）任一张 → 同一槽（v21 分组语义）；槽身份对得上；对方牌库卡 -1
            var slotOfHash = new Dictionary<ulong, int>();
            bool idxOk = true;
            int mapped = 0;
            foreach (var c in deckNow)
            {
                int s = TideObservation.IndexOfCard(core, p1, c);
                ulong h = StructureHashOf(c);
                if (s < TideObservation.DeckBase || s >= TideObservation.DeckTopSlot
                    || obs2.Cards[s * TideObservation.NCard + TideObservation.StructureSlot] != TideCardIndex.IndexOf(h))
                { idxOk = false; continue; }
                if (!slotOfHash.TryGetValue(h, out int first)) slotOfHash[h] = s;
                else if (first != s) idxOk = false;
                mapped++;
            }
            bool idxOpp = zm.GetCards(p2, Zone.Deck).All(c => TideObservation.IndexOfCard(core, p1, c) == -1);
            checks.Add(new Check(idxOk && idxOpp && mapped == deckNow.Count && slotOfHash.Count == deckBlock2.Count,
                $"IndexOfCard：己方 {mapped} 张牌库卡应映射到 {deckBlock2.Count} 个组槽（同组同槽、槽身份对得上），对方牌库卡应恒 -1"));

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

            // ③ zoneKind 抽检：各区移卡后 [115] 值（战场0/元素池1/手牌2/墓地3/发动区4/场地区5/牌库6/除外9）
            var zoneChecks = new List<(Zone zone, int expect)>
            {
                (Zone.Battlefield, 0), (Zone.ElementPool, 1), (Zone.Graveyard, 3),
                (Zone.Activation, 4), (Zone.FieldZone, 5), (Zone.Exile, 9),
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
                    // v21：牌库块以外的槽 countInZone 恒 1（移库后该卡所在槽逐区抽检）
                    int ms = TideObservation.IndexOfCard(core, p1, mover);
                    bool onePerSlot = ms >= 0 && obsZ.Cards[ms * TideObservation.NCard + TideObservation.CountSlot] == 1f;
                    if (got != expect || !onePerSlot)
                    { log.AppendLine($"[zoneKind] {zone} 期望 {expect} 实际 {got}（一槽一卡={onePerSlot}）"); zkOk = false; }
                }
            }
            checks.Add(new Check(zkOk, "zoneKind 抽检：战场0/元素池1/手牌2/墓地3/发动区4/场地区5/牌库6/除外9（移库后槽 countInZone=1）"));

            // ================= ④ 超量并组 + 座次切换（skipOpeningDraw+deferStart 双闸：不开抽、
            //     不进回合抽，牌库保持满 30——任一单用都会少一张） =================
            core.InitGame(CardLoader.BuildDeck(deck1Data, 1), CardLoader.BuildDeck(deck2Data, 1),
                rngSeed: 7, skipOpeningDraw: true, deferStart: true);
            var zm4 = core.ZoneManager;
            var obsC = TideObservation.Build(core, core.Player1, false, null);
            var full = ReadDeckBlock(obsC);
            var fullDeck = zm4.GetCards(core.Player1, Zone.Deck);
            checks.Add(new Check(fullDeck.Count == 30 && DeckBlockMatches(full, fullDeck)
                && ExpandDeckBlock(full).SequenceEqual(SortedIndices(core.OriginalDeckOf(core.Player1))),
                $"双闸满库：牌库块应 3 组、张数合计 30 且展开=OriginalDeckOf，实际 {Describe(full)}"));

            // 第 31 张同种卡入库（衍生物入库等价路径：直接 Add）→ v21 不再截断：并组、张数合计 31、
            // g[13] 照走；新张 IndexOfCard 指向其所在组槽（v20 及以前：30 槽被物理卡占满，第 31 张 -1）
            var spare = CardLoader.BuildDeck(new List<CardData> { ByName(catalog, CardC) }, 1)[0];
            spare.SetController(core.Player1);
            zm4.GetZoneContainer(core.Player1).Add(spare, Zone.Deck);
            var obsD = TideObservation.Build(core, core.Player1, false, null);
            var thirtyOne = ReadDeckBlock(obsD);
            int countNow = zm4.GetCards(core.Player1, Zone.Deck).Count;
            int g13Raw = Mathf.RoundToInt(obsD.Globals[13] * 60f);
            // 新张应并入「与它同一种卡」的组：该组张数 = 入库前张数 + 1（按新张自己的结构哈希定位，
            // 不写死组序/张数——夹具里哪种卡并给谁由卡表决定）
            int spareIdx = TideCardIndex.IndexOf(ByName(catalog, CardC).StructureContentHash);
            int before = full.FindIndex(g => g.structure == spareIdx);
            int after = thirtyOne.FindIndex(g => g.structure == spareIdx);
            bool spareOk = before >= 0 && after >= 0 && thirtyOne[after].count == full[before].count + 1
                && TideObservation.IndexOfCard(core, core.Player1, spare) == TideObservation.DeckBase + after;
            checks.Add(new Check(countNow == 31 && thirtyOne.Count == 3 && thirtyOne.Sum(g => g.count) == 31 && g13Raw == 31
                && obsD.Globals[TideObservation.DeckTruncatedSlot] == 0f && spareOk,
                $"同种超量：31 张时应 3 组、张数合计 31、g[13]=31、未截断 g[96]=0、新张并入同种组（该组 +1），实际 {Describe(thirtyOne)} g13={g13Raw} 新张入组={spareOk}"));
            // v21 countInZone 不变量（全 191 槽扫描）：valid=1 ⇔ ≥1、牌库块以外恒 1、空槽 0——
            // 上面各观测（开抽/抽牌/重洗/满库/超量）逐个过
            bool invariantOk = CountInvariantOk(obs) && CountInvariantOk(obs2) && CountInvariantOk(obsB)
                && CountInvariantOk(obsC) && CountInvariantOk(obsD);
            checks.Add(new Check(invariantOk,
                "countInZone 不变量：valid=1 ⇔ ≥1、牌库块 [154..183] 以外恒 1、空槽 0（各观测全槽扫描）"));

            // 座次切换：Build(p2)——牌库块换 p2 分组，对方标签换 OriginalDeckOf(p1)、己方标签=OriginalDeckOf(p2)
            var obsE = TideObservation.Build(core, core.Player2, false, null);
            var deckP2 = ReadDeckBlock(obsE);
            var expectedP2 = SortedIndices(core.OriginalDeckOf(core.Player2));
            var tagsP1 = ManualTags(core.OriginalDeckOf(core.Player1));
            var tagsP2 = ManualTags(core.OriginalDeckOf(core.Player2));
            bool seatOk = ExpandDeckBlock(deckP2).OrderBy(x => x).SequenceEqual(expectedP2)
                && DeckBlockMatches(deckP2, zm4.GetCards(core.Player2, Zone.Deck));
            for (int i = 0; i < 8 && seatOk; i++)
                seatOk &= Mathf.Abs(obsE.Globals[TideObservation.DeckTagStart + i] - tagsP1[i]) < 1e-4f
                       && Mathf.Abs(obsE.Globals[TideObservation.OwnDeckTagStart + i] - tagsP2[i]) < 1e-4f;
            checks.Add(new Check(seatOk, "座次切换 Build(p2)：牌库块=p2 分组、g[50..57]=OriginalDeckOf(p1) 标签、g[88..95]=OriginalDeckOf(p2) 标签"));

            // ================= ⑥⑦⑧⑨ v10 四件套：墓地扩槽/除外/被展示/牌库顶 =================
            RunV10Scenarios(catalog, log, checks);

            // ================= ⑩ v15 场地特殊化：观测拆槽 + LAE 攻击场地行 =================
            RunV15Scenarios(catalog, log, checks);

            // ================= ⑪..⑮ v21 卡组分布与策略分界：预组实测/实例态分组/超种截断 =================
            RunV21Scenarios(catalog, log, checks);
        }

        // ======================================== v21 场景（2026-11-24 观测·卡组分布与策略分界） ========================================

        private static void RunV21Scenarios(List<CardData> catalog, StringBuilder log, List<Check> checks)
        {
            var core = GameCore.Instance;
            var deck2Data = Mix(catalog, (CardD, 10), (CardA, 10), (CardB, 10));

            // ---- ⑪ 三套预组实测（方案 §6.1）：唯一卡组数/张数 + 己方标签 + 未截断 ----
            // 期望走独立路径（区列表里的卡按卡表可分辨签名分组、内容哈希升序——不读观测）；
            // 组数/张数合计按当前预组钉死（红 5 种 30 / 绿 4 种 60 / 蓝 4 种 30，绿 19/19/11/11）。
            // 红=5 的前提：火灵·攻/火灵·守 仅攻守资格不同，攻/守资格已入内容身份（v21 同批补盲，
            // 见 ⑯）——补盲前两卡观测逐位相同、红只见 4 种。
            var themes = BattleDeckSources.ThemeDecks();
            var pinned = new Dictionary<string, (int groups, int total)>
            {
                ["red"] = (5, 30), ["green"] = (4, 60), ["blue"] = (4, 30),
            };
            foreach (var (key, deck) in themes)
            {
                if (deck == null || deck.Count == 0)
                {
                    checks.Add(new Check(false, $"预组 {key}：卡组缺失，无法断言牌库块"));
                    continue;
                }
                var oppDeck = themes.Where(t => t.key != key && t.deck != null && t.deck.Count > 0)
                    .Select(t => t.deck).FirstOrDefault() ?? deck;
                core.InitGame(CardLoader.BuildDeck(deck, 1), CardLoader.BuildDeck(oppDeck, 1),
                    rngSeed: 21, skipOpeningDraw: true, deferStart: true); // 双闸满库（不抽牌）
                var me = core.Player1;
                var live = core.ZoneManager.GetCards(me, Zone.Deck);
                var obs = TideObservation.Build(core, me, false, null);
                var block = ReadDeckBlock(obs);
                var tags = ManualTags(deck);
                bool tagsOk = true;
                for (int i = 0; i < 8; i++)
                    tagsOk &= Mathf.Abs(obs.Globals[TideObservation.OwnDeckTagStart + i] - tags[i]) < 1e-4f;
                var (groups, total) = pinned.TryGetValue(key, out var pin) ? pin : (-1, -1);
                bool ok = block.Count == groups && block.Sum(g => g.count) == total && live.Count == total
                    && DeckBlockMatches(block, live) && obs.Globals[TideObservation.DeckTruncatedSlot] == 0f && tagsOk;
                if (key == "green") // 方案 §6.1：绿 4 组 19/19/11/11（v20 截断后只见 2 种）
                    ok &= block.Select(g => g.count).OrderBy(x => x).SequenceEqual(new[] { 11, 11, 19, 19 });
                log.AppendLine($"[预组 {key}] {deck.GroupBy(d => d.ID).Count()} 个卡 ID → 牌库块 {block.Count} 组 {Describe(block)}；己方标签 "
                    + $"[{string.Join(",", tags.Select(t => t.ToString("0")))}]");
                checks.Add(new Check(ok,
                    $"预组 {key}：牌库块应 {groups} 组/张数合计 {total}、展开=卡表与当前牌库、g[96]=0、g[88..95]=手工口径，实际 {Describe(block)}"));
            }

            // ---- ⑫ 实例态不同的同种卡不并组：满库 30 张（三种各 10）里给 1 张火灵·攻挂潜行指示物
            //     （卡特征 [13] 变 1）——合并判据=特征行全同，该张应独立成组（9 + 1），其余两种仍各 1 组；
            //     IndexOfCard：被标记张 → 1 张组槽，其余同种 → 9 张组槽 ----
            var deck1Data = Mix(catalog, (CardA, 10), (CardB, 10), (CardC, 10));
            core.InitGame(CardLoader.BuildDeck(deck1Data, 1), CardLoader.BuildDeck(deck2Data, 1),
                rngSeed: 22, skipOpeningDraw: true, deferStart: true);
            var p1 = core.Player1;
            var zm = core.ZoneManager;
            ulong hashA = ByName(catalog, CardA).StructureContentHash;
            var marked = zm.GetCards(p1, Zone.Deck).First(c => StructureHashOf(c) == hashA);
            marked.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1);
            var obsM = TideObservation.Build(core, p1, false, null);
            var blockM = ReadDeckBlock(obsM);
            int idxA = TideCardIndex.IndexOf(hashA);
            var aSlots = Enumerable.Range(0, blockM.Count).Where(i => blockM[i].structure == idxA).ToList();
            int markedSlot = TideObservation.IndexOfCard(core, p1, marked);
            int nineSlot = aSlots.FirstOrDefault(i => blockM[i].count == 9);
            int oneSlot = aSlots.FirstOrDefault(i => blockM[i].count == 1);
            bool splitOk = blockM.Count == 4 && blockM.Sum(g => g.count) == 30 && aSlots.Count == 2
                && nineSlot >= 0 && oneSlot >= 0 && nineSlot != oneSlot
                && markedSlot == TideObservation.DeckBase + oneSlot
                && obsM.Cards[markedSlot * TideObservation.NCard + 13] == 1f
                && zm.GetCards(p1, Zone.Deck).Where(c => !ReferenceEquals(c, marked) && StructureHashOf(c) == hashA)
                    .All(c => TideObservation.IndexOfCard(core, p1, c) == TideObservation.DeckBase + nineSlot);
            checks.Add(new Check(splitOk,
                $"实例态不同的同种卡不并组：应 4 组（10/10/9/1），被标记张独立成组并映射到 1 张组槽，实际 {Describe(blockM)}"));

            // ---- ⑬ 唯一卡 >30 种：按排序键截断到 30 组 + g[96]=1 + 溢出组卡 IndexOfCard=-1（不静默） ----
            var kinds = DistinctKinds(BattleDeckSources.TrainingPool(), 35);
            if (kinds.Count < 35) kinds = DistinctKinds(catalog, 35);
            if (kinds.Count < 35)
            {
                checks.Add(new Check(false, $"卡池唯一卡仅 {kinds.Count} 种（<35），超种截断场景无法构造"));
            }
            else
            {
                core.InitGame(CardLoader.BuildDeck(kinds, 1), CardLoader.BuildDeck(deck2Data, 1),
                    rngSeed: 23, skipOpeningDraw: true, deferStart: true);
                var me = core.Player1;
                var live = core.ZoneManager.GetCards(me, Zone.Deck);
                var obs = TideObservation.Build(core, me, false, null);
                var block = ReadDeckBlock(obs);
                int overflow = 0;
                var encoded = new HashSet<int>();
                foreach (var c in live)
                {
                    int s = TideObservation.IndexOfCard(core, me, c);
                    if (s < 0) overflow++;
                    else encoded.Add(s);
                }
                bool truncOk = live.Count == kinds.Count && block.Count == TideObservation.DeckSlots
                    && block.SequenceEqual(ExpectedDeckGroups(live).Take(TideObservation.DeckSlots))
                    && obs.Globals[TideObservation.DeckTruncatedSlot] == 1f
                    && overflow == live.Count - TideObservation.DeckSlots && encoded.Count == TideObservation.DeckSlots
                    && Mathf.RoundToInt(obs.Globals[13] * 60f) == live.Count;
                checks.Add(new Check(truncOk,
                    $"{kinds.Count} 种牌库超种截断：应只编码排序前 30 组、g[96]=1、溢出 {kinds.Count - 30} 张 IndexOfCard=-1、g[13]={kinds.Count}，"
                    + $"实际 组数={block.Count} g96={obs.Globals[TideObservation.DeckTruncatedSlot]} 溢出={overflow} 库存={live.Count}"));
            }

            // ---- ⑯ 战斗底盘·攻/守资格入身份（2026-11-24 用户定案：攻/守=带发动条件的主动效果）：
            //     仅攻守资格不同的火灵·攻/火灵·守必须可分辨——原子槽 [10]=Attack / [11]=Guard：声明的
            //     写类型行且哈希非零、未声明=0，生物原子数组恒 12 长；两卡结构哈希不同；
            //     可攻击粗判 [7] 加攻资格闸（火灵·守=守卫专用 → 0；v21 前只看未横置且力量>0 误标 1） ----
            core.InitGame(CardLoader.BuildDeck(deck1Data, 1), CardLoader.BuildDeck(deck2Data, 1),
                rngSeed: 24, skipOpeningDraw: true, deferStart: true);
            p1 = core.Player1;
            zm = core.ZoneManager;
            var obsChassis = TideObservation.Build(core, p1, false, null);
            int typeAttack = CardIdentityService.AtomTypeIndex("Attack");
            int typeGuard = CardIdentityService.AtomTypeIndex("Guard");
            var deckCards = zm.GetCards(p1, Zone.Deck);
            bool chassisOk = deckCards.Count == 30;
            foreach (var c in deckCards)
            {
                int s = TideObservation.IndexOfCard(core, p1, c);
                var d = c is CardWrapper cw && cw.GetData() != null ? cw.GetData() : null;
                if (s < 0 || d == null) { chassisOk = false; continue; }
                int b = s * TideObservation.NCard;
                float atkType = obsChassis.Cards[b + TideObservation.TypeIdStart + CardIdentityService.ChassisAttackSlot];
                float grdType = obsChassis.Cards[b + TideObservation.TypeIdStart + CardIdentityService.ChassisGuardSlot];
                float atkHash = obsChassis.Cards[b + TideObservation.HashIdStart + CardIdentityService.ChassisAttackSlot];
                float grdHash = obsChassis.Cards[b + TideObservation.HashIdStart + CardIdentityService.ChassisGuardSlot];
                chassisOk &= d.AttackAbility ? atkType == typeAttack && atkHash != 0f : atkType == 0f && atkHash == 0f;
                chassisOk &= d.GuardAbility ? grdType == typeGuard && grdHash != 0f : grdType == 0f && grdHash == 0f;
                chassisOk &= d.AtomContentHashes != null && d.AtomContentHashes.Length == TideObservation.AtomIdSlots;
            }
            var dataAtk = ByName(catalog, CardA);
            var dataGrd = ByName(catalog, CardC);
            bool distinctOk = dataAtk.AttackAbility && !dataAtk.GuardAbility && !dataGrd.AttackAbility && dataGrd.GuardAbility
                && dataAtk.StructureContentHash != dataGrd.StructureContentHash;
            var atkCard = deckCards.First(c => CardNameOf(c) == CardA);
            var grdCard = deckCards.First(c => CardNameOf(c) == CardC);
            zm.MoveCard(atkCard, p1, Zone.Deck, Zone.Battlefield);
            zm.MoveCard(grdCard, p1, Zone.Deck, Zone.Battlefield);
            obsChassis = TideObservation.Build(core, p1, false, null);
            int sa = TideObservation.IndexOfCard(core, p1, atkCard);
            int sg = TideObservation.IndexOfCard(core, p1, grdCard);
            bool canAttackOk = sa >= 0 && sg >= 0
                && obsChassis.Cards[sa * TideObservation.NCard + 7] == 1f // 火灵·攻：有攻资格、未横置、力量>0
                && obsChassis.Cards[sg * TideObservation.NCard + 3] > 0f // 火灵·守力量>0 的前提下……
                && obsChassis.Cards[sg * TideObservation.NCard + 7] == 0f; // ……[7] 仍为 0——闸确实来自攻资格
            checks.Add(new Check(chassisOk && distinctOk && canAttackOk,
                $"战斗底盘入身份：攻/守资格应落原子槽 [{CardIdentityService.ChassisAttackSlot}]/[{CardIdentityService.ChassisGuardSlot}]（声明=Attack/Guard 类型行"
                + $" {typeAttack}/{typeGuard}，未声明=0），火灵·攻/守结构哈希应不同，[7] 可攻击=攻 1/守 0；实际 槽位={chassisOk} 可分辨={distinctOk} 粗判={canAttackOk}"));
        }

        // ======================================== v15 场景（2026-11-18 场地特殊化·拆槽） ========================================

        private static void RunV15Scenarios(List<CardData> catalog, StringBuilder log, List<Check> checks)
        {
            var core = GameCore.Instance;
            var deck1Data = Mix(catalog, (CardA, 10), (CardB, 10), (CardC, 10));
            var deck2Data = Mix(catalog, (CardD, 10), (CardA, 10), (CardB, 10));
            core.InitGame(CardLoader.BuildDeck(deck1Data, 1), CardLoader.BuildDeck(deck2Data, 1), rngSeed: 15);
            var p1 = core.Player1;
            var p2 = core.Player2;
            var zm = core.ZoneManager;

            // 双方各上 1 张场地（结界克隆+IsField）+ p1 上 1 只攻击者（LAE 攻击行前提）
            var fieldTpl = catalog.FirstOrDefault(c => c.Supertype == Cardtype.Enchantment);
            Card MakeField(Player owner)
            {
                var data = TideJson.FromJson<CardData>(TideJson.ToJson(fieldTpl));
                data.IsField = true;
                var card = new CardWrapper(data);
                card.SetController(owner);
                zm.TryAddToBattlefield(card, owner);
                return card;
            }
            var f1 = MakeField(p1);
            var f2 = MakeField(p2);
            var atk = TakeFromDeckOrHand(zm, p1);
            zm.MoveCard(atk, p1, zm.IsCardInZone(atk, p1, Zone.Hand) ? Zone.Hand : Zone.Deck, Zone.Battlefield);

            var obs = TideObservation.Build(core, p1, false, null);

            // 战场槽 [0..8] 不含场地（zoneKind=10 不出现在单位槽）
            bool bfClean = true;
            for (int i = 0; i < 9; i++)
            {
                int b = i * TideObservation.NCard;
                if (obs.Cards[b] == 1f && obs.Cards[b + TideObservation.ZoneKindSlot] == 10f) bfClean = false;
            }
            checks.Add(new Check(bfClean, "v15 拆场地：战场槽 [0..8] 不含场地（单位槽无 zoneKind=10）"));

            // 场地专用槽：[189]=己方 / [190]=对方（各 1 张、zoneKind=10、身份对拍）
            int bOwn = TideObservation.FieldBase * TideObservation.NCard;
            int bOpp = (TideObservation.FieldBase + 1) * TideObservation.NCard;
            bool fieldOk = obs.Cards[bOwn] == 1f && obs.Cards[bOwn + TideObservation.ZoneKindSlot] == 10f
                && obs.Cards[bOwn + TideObservation.StructureSlot] == TideCardIndex.IndexOf(StructureHashOf(f1))
                && obs.Cards[bOpp] == 1f && obs.Cards[bOpp + TideObservation.ZoneKindSlot] == 10f
                && obs.Cards[bOpp + TideObservation.StructureSlot] == TideCardIndex.IndexOf(StructureHashOf(f2));
            checks.Add(new Check(fieldOk, "v15 场地专用槽：[189]=己方场地 / [190]=对方场地（zoneKind=10、身份对拍）"));

            // IndexOfCard 反查：场地卡→专用槽；单位→战场槽
            int ua = TideObservation.IndexOfCard(core, p1, atk);
            bool idxOk = TideObservation.IndexOfCard(core, p1, f1) == TideObservation.FieldBase
                && TideObservation.IndexOfCard(core, p1, f2) == TideObservation.FieldBase + 1
                && ua >= 0 && ua < 9;
            checks.Add(new Check(idxOk,
                $"v15 IndexOfCard：场地卡→[189]/[190]，单位→战场槽（攻击者槽={ua}）"));

            // g[7]/g[8] 只数单位（p1=1 攻击者、p2=0；场地不计）
            checks.Add(new Check(Mathf.RoundToInt(obs.Globals[7]) == 1 && Mathf.RoundToInt(obs.Globals[8]) == 0,
                $"v15 g[7]/g[8] 只数单位（期望 1/0，实际 {obs.Globals[7]:0}/{obs.Globals[8]:0}）"));

            // LAE：攻击对方场地行（TargetIndex=场地专用槽、TargetKindCode=1、源=攻击者）
            AdvanceToMainPhase(core, p1);
            var lae = new LegalActionEnumerator();
            lae.Enumerate(core, p1);
            var fieldAtkRows = lae.Actions.Where(a => a.Type == TideActionType.Attack
                && ReferenceEquals(a.Target, f2)).ToList();
            bool laeOk = fieldAtkRows.Count > 0 && fieldAtkRows.All(r => r.TargetIndex == TideObservation.FieldBase + 1
                && r.TargetKindCode == 1);
            log.AppendLine($"[v15场地] LAE 攻击场地行={fieldAtkRows.Count}");
            checks.Add(new Check(laeOk,
                $"v15 LAE 攻击场地行：应≥1 行且 TargetIndex={TideObservation.FieldBase + 1}/KindCode=1，实际 {fieldAtkRows.Count} 行"));

            // 座次镜像：Build(p2) 时 [189]=p2 场地 / [190]=p1 场地
            var obs2 = TideObservation.Build(core, p2, false, null);
            bool mirrorOk = obs2.Cards[bOwn + TideObservation.StructureSlot] == TideCardIndex.IndexOf(StructureHashOf(f2))
                && obs2.Cards[bOpp + TideObservation.StructureSlot] == TideCardIndex.IndexOf(StructureHashOf(f1));
            checks.Add(new Check(mirrorOk, "v15 座次镜像：Build(p2) 时 [189]=p2 场地 / [190]=p1 场地"));
        }

        // ======================================== v10 场景（2026-11-02 观测欠账四件套） ========================================

        private static void RunV10Scenarios(List<CardData> catalog, StringBuilder log, List<Check> checks)
        {
            var core = GameCore.Instance;
            var deck1Data = Mix(catalog, (CardA, 10), (CardB, 10), (CardC, 10));
            var deck2Data = Mix(catalog, (CardD, 10), (CardA, 10), (CardB, 10));

            // ---- ⑥ 墓地扩槽 30：12 张入墓全编码·槽35=最新入墓；31 张截断护栏 ----
            core.InitGame(CardLoader.BuildDeck(deck1Data, 1), CardLoader.BuildDeck(deck2Data, 1), rngSeed: 42);
            var p1 = core.Player1;
            var p2 = core.Player2;
            var zm = core.ZoneManager;
            for (int i = 0; i < 12; i++)
            {
                var c = TakeFromDeckOrHand(zm, p1);
                zm.MoveCard(c, p1, zm.IsCardInZone(c, p1, Zone.Hand) ? Zone.Hand : Zone.Deck, Zone.Graveyard);
            }
            int graveCount = zm.GetCards(p1, Zone.Graveyard).Count;
            var obs = TideObservation.Build(core, p1, false, null);
            var graveRegion = RegionStructureIndices(obs, 35, 30);
            var graveNewest = zm.GetCards(p1, Zone.Graveyard).Last();
            int b35 = 35 * TideObservation.NCard;
            bool graveOk = graveRegion.Count == graveCount
                && obs.Cards[b35] == 1f
                && obs.Cards[b35 + TideObservation.StructureSlot] == TideCardIndex.IndexOf(StructureHashOf(graveNewest))
                && zm.GetCards(p1, Zone.Graveyard).All(c => TideObservation.IndexOfCard(core, p1, c) is int s && s >= 35 && s < 65)
                && zm.GetCards(p1, Zone.Graveyard).All(c => CountOf(core, p1, obs, c) == 1f); // v21：墓地区一槽一卡
            checks.Add(new Check(graveOk,
                $"墓地扩槽：{graveCount} 张应全编码（cap30·新死优先槽35=最新入墓·IndexOfCard 全解析·countInZone 恒 1），实际 valid={graveRegion.Count}"));
            // 截断：把 p1 剩余牌库+手牌全部入墓（30 张），再直塞第 31 张（开抽后总量恰 30，须补一张）
            while (zm.GetCards(p1, Zone.Deck).Count > 0 || zm.GetCards(p1, Zone.Hand).Count > 0)
            {
                var c = TakeFromDeckOrHand(zm, p1);
                if (c == null) break;
                zm.MoveCard(c, p1, zm.IsCardInZone(c, p1, Zone.Hand) ? Zone.Hand : Zone.Deck, Zone.Graveyard);
            }
            var spareGrave = CardLoader.BuildDeck(new List<CardData> { ByName(catalog, CardC) }, 1)[0];
            spareGrave.SetController(p1);
            zm.GetZoneContainer(p1).Add(spareGrave, Zone.Graveyard);
            graveCount = zm.GetCards(p1, Zone.Graveyard).Count;
            obs = TideObservation.Build(core, p1, false, null);
            var graveRegion31 = RegionStructureIndices(obs, 35, 30);
            int g11Raw = Mathf.RoundToInt(obs.Globals[11] * 30f);
            checks.Add(new Check(graveCount == 31 && graveRegion31.Count == 30 && g11Raw == 31,
                $"墓地截断：31 张时 valid 槽应 30、g[11] 计数应 31，实际 张数={graveCount} 槽数={graveRegion31.Count} g11={g11Raw}"));

            // ---- ⑦ 除外 8 入册：9 张放逐（墓→除外）→ 8 编码 + g[15]=9 + zoneKind=9 ----
            var exiled = new List<Card>();
            while (exiled.Count < 9 && zm.GetCards(p1, Zone.Graveyard).Count > 0)
            {
                var c = zm.GetCards(p1, Zone.Graveyard)[0];
                zm.MoveCard(c, p1, Zone.Graveyard, Zone.Exile);
                exiled.Add(c);
            }
            obs = TideObservation.Build(core, p1, false, null);
            var exileRegion = RegionStructureIndices(obs, 69, 8);
            bool exileOk = exileRegion.Count == 8 && Mathf.RoundToInt(obs.Globals[15]) == 9
                && exiled.Take(8).All(c => ZoneKindOfCard(core, p1, obs, c) == 9)
                && exiled.Take(8).All(c => TideObservation.IndexOfCard(core, p1, c) >= 69 && TideObservation.IndexOfCard(core, p1, c) < 77)
                && exiled.Take(8).All(c => CountOf(core, p1, obs, c) == 1f); // v21：除外区一槽一卡
            checks.Add(new Check(exileOk,
                $"除外入册：9 张放逐应 8 编码+g[15]=9+zoneKind=9+IndexOfCard 解析，实际 槽={exileRegion.Count} g15={Mathf.RoundToInt(obs.Globals[15])}"));

            // ---- ⑧ 被展示：对方手牌 2 张开图（其余隐藏）+ 对方牌库 2 张入尾块 ----
            var oppHand = zm.GetCards(p2, Zone.Hand);
            var oppDeck = zm.GetCards(p2, Zone.Deck);
            oppHand[0].AddCounters(CardCore.Attribute.CounterRules.ExposedCounter, 1);
            oppHand[2].AddCounters(CardCore.Attribute.CounterRules.ExposedCounter, 1);
            oppDeck[0].AddCounters(CardCore.Attribute.CounterRules.ExposedCounter, 1);
            oppDeck[3].AddCounters(CardCore.Attribute.CounterRules.ExposedCounter, 1);
            obs = TideObservation.Build(core, p1, false, null);
            bool revealHandOk = true;
            for (int i = 0; i < oppHand.Count && i < 12; i++)
            {
                int b = (TideObservation.MaxCardsPerPlayer + 23 + i) * TideObservation.NCard;
                bool exposed = i == 0 || i == 2;
                float expectId = TideCardIndex.IndexOf(StructureHashOf(oppHand[i]));
                revealHandOk &= obs.Cards[b + TideObservation.StructureSlot] == (exposed ? expectId : 0f)
                            && obs.Cards[b + TideObservation.ZoneKindSlot] == 2f;
            }
            checks.Add(new Check(revealHandOk,
                $"被展示·对方手牌：槽 0/2 应开图（身份=卡真值），其余身份槽 0（{oppHand.Count} 张），zoneKind 恒 2"));
            int bx0 = TideObservation.OppDeckExposedBase * TideObservation.NCard;
            int bx1 = (TideObservation.OppDeckExposedBase + 1) * TideObservation.NCard;
            int bx2 = (TideObservation.OppDeckExposedBase + 2) * TideObservation.NCard;
            bool revealDeckOk = obs.Cards[bx0] == 1f
                && obs.Cards[bx0 + TideObservation.ZoneKindSlot] == 8f
                && obs.Cards[bx0 + TideObservation.StructureSlot] == TideCardIndex.IndexOf(StructureHashOf(oppDeck[0]))
                && obs.Cards[bx1] == 1f
                && obs.Cards[bx1 + TideObservation.ZoneKindSlot] == 8f
                && obs.Cards[bx1 + TideObservation.StructureSlot] == TideCardIndex.IndexOf(StructureHashOf(oppDeck[3]))
                && obs.Cards[bx2] == 0f; // cap 内第 3 槽空（仅 2 张被展示）
            checks.Add(new Check(revealDeckOk,
                "被展示·对方牌库：尾块 [185][186] 应按区内序编码两张（zoneKind=8），[187] 空"));

            // ---- ⑨ 牌库顶：光环命中才编码+LAE 行+Apply 离库；未命中/离场恒空（反泄露硬断言） ----
            core.InitGame(CardLoader.BuildDeck(deck1Data, 1), CardLoader.BuildDeck(deck2Data, 1), rngSeed: 11);
            p1 = core.Player1;
            zm = core.ZoneManager;
            // 夹具播种直写（2026-11-21 持有钳：AddMana 官方口余额封顶地牌上限——播种绕钳）
            foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
                core.ElementPool.GetPool(p1).TempElements[color] += 10;
            // 代价行地牌门槛（红色代价 1 需红地 ≥1——代价不走临时钱包）：四色地各铺一张
            var pool = core.ElementPool.GetPool(p1);
            foreach (var color in new[] { ManaType.Red, ManaType.Green, ManaType.Blue, ManaType.Gray })
            {
                var lc = TakeFromDeckOrHand(zm, p1);
                if (lc != null) pool.PooledCards.Add(new PooledCard(lc, color));
            }
            int bt = TideObservation.DeckTopSlot * TideObservation.NCard;
            obs = TideObservation.Build(core, p1, false, null);
            AdvanceToMainPhase(core, p1);
            var lae = new LegalActionEnumerator();
            lae.Enumerate(core, p1);
            bool leakFreeOff = obs.Cards[bt] == 0f && obs.Cards[bt + TideObservation.ZoneKindSlot] == 0f
                && lae.Actions.All(a => a.SourceIndex != TideObservation.DeckTopSlot);
            checks.Add(new Check(leakFreeOff, "牌库顶·未命中：顶槽恒空 + LAE 无牌库顶行（反泄露硬断言）"));

            var carrier = TakeFromDeckOrHand(zm, p1);
            zm.MoveCard(carrier, p1, zm.IsCardInZone(carrier, p1, Zone.Hand) ? Zone.Hand : Zone.Deck, Zone.Battlefield);
            RuleAuraSystem.Activate(RuleAuraComponents.TopDeckAsHandAura, carrier, p1, (int)RuleAuraScope.Own);
            var topCard = zm.GetCards(p1, Zone.Deck)[0];
            obs = TideObservation.Build(core, p1, false, null);
            bool topOk = obs.Cards[bt] == 1f
                && obs.Cards[bt + TideObservation.ZoneKindSlot] == 7f
                && obs.Cards[bt + TideObservation.StructureSlot] == TideCardIndex.IndexOf(StructureHashOf(topCard));
            checks.Add(new Check(topOk, "牌库顶·命中：顶槽=deck[0] 身份+zoneKind=7"));

            lae.Enumerate(core, p1);
            var topRows = lae.Actions.Where(a => a.SourceIndex == TideObservation.DeckTopSlot).ToList();
            bool rowsOk = topRows.Count > 0 && topRows.All(r => r.Type == TideActionType.PlayCard
                && ReferenceEquals(r.Card, topCard));
            log.AppendLine($"[牌库顶] LAE 行数={topRows.Count}（光环命中）");
            checks.Add(new Check(rowsOk, $"牌库顶·LAE：应出 deck[0] 的 PlayCard 行（sourceIndex={TideObservation.DeckTopSlot}），实际 {topRows.Count} 行"));

            bool played = topRows.Count > 0 && LegalActionEnumerator.Apply(core, p1, topRows[0]);
            int drain = 0;
            while (played && !core.StackEngine.IsEmpty && drain++ < 10) GameActions.DrainStack(core);
            checks.Add(new Check(played && !zm.IsCardInZone(topCard, p1, Zone.Deck),
                $"牌库顶·Apply：出牌应成功且卡离库（ok={played} 在库={zm.IsCardInZone(topCard, p1, Zone.Deck)}）"));

            zm.MoveCard(carrier, p1, Zone.Battlefield, Zone.Graveyard); // 载体离场 → 光环失效
            obs = TideObservation.Build(core, p1, false, null);
            lae.Enumerate(core, p1);
            bool leakFreePost = obs.Cards[bt] == 0f
                && lae.Actions.All(a => a.SourceIndex != TideObservation.DeckTopSlot);
            checks.Add(new Check(leakFreePost, "牌库顶·载体离场：顶槽恒空 + LAE 无牌库顶行（反泄露硬断言）"));
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

        /// <summary>观测某槽区的身份下标集（valid=1 槽读 [27] StructureSlot）——一槽一卡的区域用
        ///（手牌/墓地/除外）；牌库块是分组槽，改用 ReadDeckBlock（v21）。</summary>
        private static List<int> RegionStructureIndices(TideObservation obs, int baseSlot, int slots)
        {
            var list = new List<int>();
            for (int i = 0; i < slots; i++)
                if (obs.Cards[(baseSlot + i) * TideObservation.NCard] == 1f)
                    list.Add((int)obs.Cards[(baseSlot + i) * TideObservation.NCard + TideObservation.StructureSlot]);
            return list;
        }

        /// <summary>牌库块读取（v21）：valid=1 的槽按槽序给出（身份下标 [27]、张数 [157] countInZone）。</summary>
        private static List<(int structure, int count)> ReadDeckBlock(TideObservation obs)
        {
            var list = new List<(int structure, int count)>();
            for (int i = 0; i < TideObservation.DeckSlots; i++)
            {
                int b = (TideObservation.DeckBase + i) * TideObservation.NCard;
                if (obs.Cards[b] != 1f) continue;
                list.Add(((int)obs.Cards[b + TideObservation.StructureSlot], (int)obs.Cards[b + TideObservation.CountSlot]));
            }
            return list;
        }

        /// <summary>牌库块按张数展开成身份下标多重集（每种唯一卡重复 count 次）。</summary>
        private static IEnumerable<int> ExpandDeckBlock(List<(int structure, int count)> block)
        {
            foreach (var (structure, count) in block)
                for (int i = 0; i < count; i++) yield return structure;
        }

        private static string Describe(List<(int structure, int count)> block)
            => string.Join(" / ", block.Select(g => $"{g.structure}×{g.count}"));

        /// <summary>牌库块 == 当前牌库的分组（独立路径：直接读区列表分组，不经观测）。</summary>
        private static bool DeckBlockMatches(List<(int structure, int count)> block, List<Card> deck)
            => block.SequenceEqual(ExpectedDeckGroups(deck));

        /// <summary>当前牌库的期望分组（独立路径）：卡按**可分辨签名**分组（签名=卡表里决定观测特征行的
        /// 字段：结构/组合哈希 + 超类 + 身材 + 费用 + 攻/守资格 + 原子哈希序列——同签名=模型看到的同一种
        /// 卡），组按 (Structure, Composition) 内容哈希升序 = 编码槽序（CompareDeckCards 前两键）。
        /// 不用 CardData.ID 当键：ID 不同但卡表全同（如仅改名）的卡在观测里本就是同一种。
        /// 调用方的卡组须满足「同签名副本特征行全同」——实例态分组的场景（⑫）单独断言。</summary>
        private static List<(int structure, int count)> ExpectedDeckGroups(List<Card> deck)
            => deck.GroupBy(ObservableKeyOf)
                .OrderBy(g => g.Key.s).ThenBy(g => g.Key.k).ThenBy(g => g.Key.sig, StringComparer.Ordinal)
                .Select(g => (TideCardIndex.IndexOf(g.Key.s), g.Count()))
                .ToList();

        /// <summary>卡表可分辨签名（ExpectedDeckGroups 分组键）。裸卡/token 无卡表数据=固定签名。</summary>
        private static (ulong s, ulong k, string sig) ObservableKeyOf(Card c)
        {
            var d = c is CardWrapper cw ? cw.GetData() : null;
            if (d == null) return (0UL, 0UL, "token");
            return (d.StructureContentHash, d.CompositionContentHash,
                d.Supertype + "|" + d.Power + "|" + d.Life + "|" + d.TotalCost + "|" + d.AttackAbility + "|" + d.GuardAbility
                + "|" + string.Join(",", d.AtomContentHashes ?? new ulong[0]));
        }

        private static string CardNameOf(Card c)
            => c is CardWrapper w && w.GetData() != null ? w.GetData().CardName : null;

        /// <summary>取内容哈希互异的卡表前 n 种（超种截断夹具）。</summary>
        private static List<CardData> DistinctKinds(List<CardData> pool, int n)
            => pool.Where(c => c != null && !c.IsTokenTemplate)
                .GroupBy(c => c.StructureContentHash).Select(g => g.First()).Take(n).ToList();

        /// <summary>v21 countInZone 不变量（全 191 槽）：valid=0 ⇒ 0；valid=1 ⇒ ≥1；牌库块以外恒 1。</summary>
        private static bool CountInvariantOk(TideObservation obs)
        {
            for (int s = 0; s < TideObservation.MaxCardsTotal; s++)
            {
                int b = s * TideObservation.NCard;
                float count = obs.Cards[b + TideObservation.CountSlot];
                bool inDeckBlock = s >= TideObservation.DeckBase && s < TideObservation.DeckTopSlot;
                if (obs.Cards[b] == 0f) { if (count != 0f) return false; }
                else if (count < 1f || (!inDeckBlock && count != 1f)) return false;
            }
            return true;
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

        /// <summary>某卡所在观测槽的 [157] countInZone（不在编码区=-1）。</summary>
        private static float CountOf(GameCore core, Player me, TideObservation obs, Card c)
        {
            int slot = TideObservation.IndexOfCard(core, me, c);
            return slot < 0 ? -1f : obs.Cards[slot * TideObservation.NCard + TideObservation.CountSlot];
        }

        private static Card TakeFromDeckOrHand(ZoneManager zm, Player p)
            => zm.GetCards(p, Zone.Deck).FirstOrDefault() ?? zm.GetCards(p, Zone.Hand).FirstOrDefault();

        /// <summary>卡实例的内容结构哈希（CardWrapper→CardData；裸卡/token=0——与观测编码同源）。</summary>
        private static ulong StructureHashOf(Card c)
            => c is CardWrapper w && w.GetData() != null ? w.GetData().StructureContentHash : 0UL;

        private static ulong CompositionHashOf(Card c)
            => c is CardWrapper w && w.GetData() != null ? w.GetData().CompositionContentHash : 0UL;
    }
}
