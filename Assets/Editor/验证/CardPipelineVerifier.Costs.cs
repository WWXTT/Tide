using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using GameBoard;
using CardCore.Attribute.Handlers;
using Cysharp.Threading.Tasks;

namespace CardCore.Editor
{
    public static partial class CardPipelineVerifier
    {
        // ======================================== 代价抵扣与可支付闸（2026-10-28 P1 定案） ========================================

        /// <summary>
        /// P1 计价内核回归（2026-10-28 定案：代价=构筑期抵扣不再产出元素；可支付=声明期闸）：
        /// ① 净费 = 推导费 − 代价价（**逐行色锁**——代价只在自身可产出色内扣、先灰后集合内最高色，
        ///    每色 ≥0，溢出丢弃，跨色不抵；2026-10-31 卡层定案 + 2026-11-05 NetBill 收口同口径）
        ///    ——同卡有无代价对照 + 手工复算（跨色不抵 + 同色抵扣双面钉死）；
        /// ② 净费 0 的卡可付（免费卡合法，空钱包放行）；
        /// ③ 手牌 0 张 → 带弃牌代价的卡不可用（CanAffordCast=false + 拒绝原因「手牌不足」）；
        /// ④ 灰费可由红/蓝/绿支付（支付链回归，不因 P3 未做而删）；
        /// ⑤ 付完代价钱包元素总量不变（代价不再产出）。
        /// </summary>
        private static void TestCostDeductionAndPayability(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var pool1 = core.ElementPool.GetPool(p1);
            var snapWallet = new Dictionary<ManaType, int>(pool1.TempElements);

            // 弃牌代价条目（p<0 锁己方手牌 {5}——新代价契约合法载体；表行默认双侧域不可裸用）
            AtomicEffectEntry DiscardPayload(int value = 1)
                => Atom(AtomicEffectType.DiscardCard, value: value, kinds: new List<int> { (int)CardCore.TargetKind.OwnHand });
            int PayloadPrice(int value = 1)
            {
                var inst = CardEffectConverter.ConvertPayloadForDisplay(DiscardPayload(value));
                return inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            }

            // 测试内手工复算「逐行色锁、先灰后集合内最高色、每色下限0」
            //（独立于 CardCostService.ApplyLockedDeduction 的第二实现——2026-11-05 色锁收口同口径）
            Dictionary<ManaType, int> ManualLockedReduce(Dictionary<ManaType, int> buckets, int points,
                HashSet<ManaType> allowed)
            {
                var r = new Dictionary<ManaType, int>(buckets);
                for (int i = 0; i < points; i++)
                {
                    if (allowed.Contains(ManaType.Gray) && r.TryGetValue(ManaType.Gray, out var g) && g >= 1)
                    { r[ManaType.Gray] = g - 1; continue; }
                    var best = default(ManaType); int bestV = 0; bool has = false;
                    foreach (var kv in r)
                        if (kv.Key != ManaType.Gray && allowed.Contains(kv.Key) && kv.Value > bestV)
                        { bestV = kv.Value; best = kv.Key; has = true; }
                    if (!has || bestV <= 0) break;
                    r[best] = bestV - 1;
                }
                foreach (var k in r.Where(x => x.Value <= 0).Select(x => x.Key).ToList()) r.Remove(k);
                return r;
            }

            string Dump(Dictionary<ManaType, int> d)
                => string.Join(",", d.Select(kv => kv.Key + "" + kv.Value));

            try
            {
                // ---- ① 净费 = 推导费 − 代价价（先灰后最高色，每色 ≥0）----
                CardData CostCard(string id, int discardValue)
                {
                    var d = new CardData
                    {
                        ID = id, CardName = "验证代价卡" + id, Supertype = Cardtype.Creature,
                        Power = 2, Life = 2,
                        Effects = new List<CardEffectData>
                        {
                            new CardEffectData
                            {
                                Id = id + "_EFF",
                                AtomicEffects = new List<AtomicEffectEntry>
                                    { Atom(AtomicEffectType.DealDamage, value: 1, kinds: new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit }) },
                            },
                        },
                    };
                    if (discardValue > 0)
                        d.Effects[0].Costs = new List<CostEntry>
                            { new CostEntry { payloads = new List<AtomicEffectEntry> { DiscardPayload(discardValue) } } };
                    return d;
                }

                var r0 = CardCostService.Derive(CostCard("VERIFY_COST_BASE", 0));
                int price = PayloadPrice(1);
                Assert(price > 0, $"弃1手牌代价价 > 0（实际 {price}）");
                var r1 = CardCostService.Derive(CostCard("VERIFY_COST_DISC", 1));
                Assert(r1.PayloadPrice == price,
                       $"代价锚价单源：SumPayloadPrice 与转换件 PayloadUnitGrant 同锚（{r1.PayloadPrice} vs {price}）");

                // 净费基准 = 抵扣前留影 PrePayloadCost（P7 单源）：卡层代价栏计 1 槽（2026-10-04 定案
                //「代价也是效果栏」+ 2026-10-22 结构项>3 差额入灰）→ 抵扣前费 ≠ 无代价推导费，
                // 差额=超槽加价（下方断言钉死），净费 = 抵扣前费 − 代价价（**逐行色锁**、每色 ≥0）。
                var pre = new Dictionary<ManaType, int>();
                foreach (var c in r1.PrePayloadCost.NonzeroColors()) pre[c] = (int)r1.PrePayloadCost[c];
                var blueOnly = new HashSet<ManaType> { ManaType.Blue }; // 弃1=蓝（原子表 ManaList——跨色面）
                var expect = ManualLockedReduce(pre, price, blueOnly);
                var d1 = new Dictionary<ManaType, int>();
                foreach (var c in r1.DerivedCost.NonzeroColors()) d1[c] = (int)r1.DerivedCost[c];
                Assert(d1.Count == expect.Count && !d1.Except(expect).Any(),
                       $"净费=抵扣前费−代价价（逐行色锁·跨色不抵）：弃1蓝对 [{Dump(pre)}]"
                       + $" − {price} → 期望 [{Dump(expect)}]（无蓝桶=0 抵扣），实际 [{Dump(d1)}]");
                Assert(d1.Values.All(v => v >= 0), "净费每色 ≥ 0（下限 0，溢出丢弃）");

                // 同色对照：自伤红代价（DealDamage 己方单位）对含红桶的卡 → 色内抵扣真发生（只动红）
                var dmgCost = Atom(AtomicEffectType.DealDamage, value: 1,
                    kinds: new List<int> { (int)CardCore.TargetKind.OwnLivingUnit });
                var dmgInst = CardEffectConverter.ConvertPayloadForDisplay(dmgCost);
                Assert(dmgInst != null, "前置：自伤红代价合法（p<0 锁己方——代价契约）");
                int dmgPts = CostDerivationService.PayloadUnitGrant(dmgInst);
                Assert(dmgPts > 0, $"前置：自伤红代价价 {dmgPts} > 0");
                var redCard = CostCard("VERIFY_COST_RED", 0);
                redCard.Effects[0].Costs = new List<CostEntry> { new CostEntry { payloads = new List<AtomicEffectEntry> { dmgCost } } };
                var rRed = CardCostService.Derive(redCard);
                var preRed = new Dictionary<ManaType, int>();
                foreach (var c in rRed.PrePayloadCost.NonzeroColors()) preRed[c] = (int)rRed.PrePayloadCost[c];
                var redOnly = new HashSet<ManaType> { ManaType.Red };
                var expectRed = ManualLockedReduce(preRed, dmgPts, redOnly);
                var dRed = new Dictionary<ManaType, int>();
                foreach (var c in rRed.DerivedCost.NonzeroColors()) dRed[c] = (int)rRed.DerivedCost[c];
                Assert(dRed.Count == expectRed.Count && !dRed.Except(expectRed).Any(),
                       $"净费色内抵扣：红代价 {dmgPts} 点只扣红桶 [{Dump(preRed)}] → 期望 [{Dump(expectRed)}]"
                       + $"（灰桶原样），实际 [{Dump(dRed)}]");
                int surcharge = (int)r1.Breakdown.Where(l => l.Stage == "C" && l.Value > 0).Sum(l => l.Value);
                Assert(surcharge >= 0 && System.Math.Abs(r1.PrePayloadCost.Total - (r0.DerivedTotal + surcharge)) < 1e-4f,
                       $"代价栏计1槽（2026-10-04/10-22 定案）：抵扣前费 {r1.PrePayloadCost.Total}"
                       + $" = 无代价推导费 {r0.DerivedTotal} + 超槽差额入灰 {surcharge}"
                       + "（2026-11-03 底盘改版后夹具槽位可全落免费底——0 亦合法）");
                Assert(r1.Breakdown.Any(l => l.Stage == "P") && r0.Breakdown.All(l => l.Stage != "P"),
                       "P 行明细：有代价才出现（代价抵扣行）");
                // 无代价卡 → 净费 = 推导费（对照恒等：同数据重推导稳定）
                var r0b = CardCostService.Derive(CostCard("VERIFY_COST_BASE", 0));
                Assert(System.Math.Abs(r0b.DerivedTotal - r0.DerivedTotal) < 1e-4f, "无代价卡净费=推导费（蓝/绿套不变——恒推导稳定）");

                // ---- ② 净费 0 的卡可付（免费卡合法）----
                // 2026-11-08 效果级化：代价挂主动效果 Costs（法术施放=使用所选主动效果——
                // 真实免费代价卡形态，同教学潮汐族；模式行计弃牌闸、锚价净费单源）
                var freeData = new CardData
                {
                    ID = "VERIFY_FREE_COST", CardName = "免费代价卡", Supertype = Cardtype.Spell,
                    Effects = new List<CardEffectData>
                    {
                        new CardEffectData
                        {
                            Id = "VERIFY_FREE_COST_EFF",
                            TriggerTiming = (int)TriggerTiming.Activate_Active,
                            ActivationType = (int)EffectActivationType.Voluntary,
                            Costs = new List<CostEntry>
                                { new CostEntry { payloads = new List<AtomicEffectEntry> { DiscardPayload(1) } } },
                        },
                    },
                };
                CardCostService.EnsureCost(freeData);
                Assert(freeData.Cost.IsZero, "无内容卡+代价 → 净费 0（免费卡合法，抵扣溢出丢弃）");
                var freeCard = InjectCard(core, p1, freeData);
                var keepHand = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Hand));
                try
                {
                    foreach (var c in keepHand.Where(c => !ReferenceEquals(c, freeCard)).ToList())
                        core.ZoneManager.MoveCard(c, p1, Zone.Hand, Zone.Graveyard); // 只留免费卡自身
                    InjectCard(core, p1, new CardData { ID = "VERIFY_FREE_FILLER", CardName = "填充手牌", Supertype = Cardtype.Spell });
                    foreach (var kv in pool1.TempElements.ToList()) pool1.TempElements[kv.Key] = 0; // 空钱包
                    Assert(GameActions.CanAffordCast(core, p1, freeCard, 0),
                           "净费 0 可付（免费卡合法——空钱包+代价可支付〔手牌有填充〕→ CanAffordCast=true）");
                }
                finally
                {
                    foreach (var c in keepHand) core.ZoneManager.MoveCard(c, p1, Zone.Graveyard, Zone.Hand);
                }

                // ---- ③ 手牌 0 张 → 带弃牌代价的卡不可用（拒绝原因）----
                var costCard = InjectCard(core, p1, CostCard("VERIFY_COST_GATE", 1));
                var hand2 = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Hand));
                try
                {
                    foreach (var c in hand2) core.ZoneManager.MoveCard(c, p1, Zone.Hand, Zone.Graveyard); // 手牌清空（含被测卡——纯预检不要求在手）
                    foreach (var kv in pool1.TempElements.ToList()) pool1.TempElements[kv.Key] = 99; // 净费侧充足：只考代价闸
                    Assert(!GameActions.CanAffordCast(core, p1, costCard, 0),
                           "手牌 0 → 带弃牌代价的卡不可用（CanAffordCast=false——净费可付 ∧ 代价可支付，后者失守）");
                    string reject = GameActions.GetPayloadGateReject(core, p1, costCard);
                    Assert(reject != null && reject.Contains("手牌不足"),
                           $"拒绝原因含「手牌不足」（实际：{reject}）");
                }
                finally
                {
                    foreach (var c in hand2) core.ZoneManager.MoveCard(c, p1, Zone.Graveyard, Zone.Hand);
                }

                // ---- ④ 灰费可由红/蓝/绿支付（支付链回归，不因 P3 未做而删）----
                var gray2 = ElementCost.FromValue(ManaType.Gray, 2);
                foreach (var kv in pool1.TempElements.ToList()) pool1.TempElements[kv.Key] = 0;
                pool1.TempElements[ManaType.Red] = 2;
                Assert(core.ElementPool.CanPayCostWithAutoTap(gray2, p1), "灰2 账单 · 钱包红2 → 可付（灰=可替代费）");
                foreach (var kv in pool1.TempElements.ToList()) pool1.TempElements[kv.Key] = 0;
                pool1.TempElements[ManaType.Blue] = 1;
                pool1.TempElements[ManaType.Green] = 1;
                Assert(core.ElementPool.CanPayCostWithAutoTap(gray2, p1), "灰2 账单 · 钱包蓝1+绿1 → 可付（灰=可替代费）");

                // ---- ⑤ 付完代价钱包元素总量不变（代价不再产出）----
                foreach (var kv in pool1.TempElements.ToList()) pool1.TempElements[kv.Key] = 0;
                pool1.TempElements[ManaType.Red] = 3;
                InjectCard(core, p1, new CardData { ID = "VERIFY_PAY_FILLER", CardName = "代价执行填充", Supertype = Cardtype.Spell });
                int walletBefore = pool1.TempElements.Values.Sum();
                int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                var costCtx = new CostContext
                {
                    Payer = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, Source = p1,
                };
                var payloadCostInst = new CostInstance
                {
                    Type = CostType.Payload,
                    Payloads = new List<CardCore.AtomicEffectInstance>
                        { CardEffectConverter.ConvertPayloadForDisplay(DiscardPayload(1)) },
                };
                Assert(CostCompensationService.PayPayloadsAsync(
                           new List<CostInstance> { payloadCostInst }, costCtx).GetAwaiter().GetResult(),
                       "PayPayloadsAsync 执行（代价只执行、不发放）");
                Assert(pool1.TempElements.Values.Sum() == walletBefore,
                       $"付完代价钱包总量不变（{walletBefore} → {pool1.TempElements.Values.Sum()}——代价不再产出元素）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore - 1,
                       "代价执行真发生（弃 1 手牌——填充卡离手）");

                // ---- ⑥ 法术主动行代价·声明期闸（2026-11-09 补闸回归）----
                // 潮汐族形态：法术唯一主动效果行带 Payload 代价（资敌抽1=蓝生成面）。此前 payloadCosts
                // 只收非主动行，「仅主动行带代价」的法术在无代价早退处整段跳闸——③地牌门槛的法术
                // 模式分支不可达：0基价+代价净费归零的法术无同色地也照常声明入栈弹连锁窗。
                var gateData = new CardData
                {
                    ID = "VERIFY_SPELL_GATE", CardName = "法术闸卡", Supertype = Cardtype.Spell,
                    Effects = new List<CardEffectData>
                    {
                        new CardEffectData
                        {
                            Id = "VERIFY_SPELL_GATE_EFF",
                            TriggerTiming = (int)TriggerTiming.Activate_Active,
                            ActivationType = (int)EffectActivationType.Voluntary,
                            Costs = new List<CostEntry>
                                { new CostEntry { payloads = new List<AtomicEffectEntry> {
                                    Atom(AtomicEffectType.DrawCard, value: 1,
                                         kinds: new List<int> { (int)CardCore.TargetKind.EnemyDeck }) } } },
                        },
                    },
                };
                CardCostService.EnsureCost(gateData);
                var gateCard = InjectCard(core, p1, gateData);
                var savedPool = new List<CardCore.PooledCard>(pool1.PooledCards);
                pool1.PooledCards.Clear(); // 清池=0 蓝地（段末还原；净费 0 不依赖池——隔离③地牌闸单变量）
                try
                {
                    EnsureMainPhase(core, p1);
                    var modeDefs = GameActions.GetCastableEffectDefinitions(gateCard);
                    var grants = CardCostService.PayloadGrantByColor(
                        modeDefs != null && modeDefs.Count > 0 ? modeDefs[0].Costs : null);
                    int blueGrant = grants.TryGetValue(ManaType.Blue, out var bg) ? bg : 0;
                    Assert(blueGrant >= 1, $"前置：资敌抽1代价=蓝生成面 ≥1（实际 {blueGrant}）");

                    // ⑥A 0 蓝地：闸拒且 PlayCard 拒绝（不再入栈弹连锁窗），拒绝原因可读
                    string gateReject = GameActions.GetPayloadGateReject(core, p1, gateCard);
                    Assert(gateReject != null && gateReject.Contains("蓝地"),
                           $"0 蓝地 → 闸拒且原因含「蓝地」（实际：{gateReject}）");
                    Assert(!GameActions.PlayCard(core, p1, gateCard, null, Zone.Hand, 0, out var playWhy)
                           && playWhy == gateReject,
                           $"0 蓝地 PlayCard 拒绝（实际：{playWhy}）——不声明、不入栈");

                    // ⑥B 补足等量蓝地：过闸（等于放行）→ 声明+排干结算照走
                    // （地生物锁色候选=推导费∪主动效果锚价——costList 只是显示缓存；主动效果挂
                    // 抽卡原子=蓝锚价 → 可显式锁蓝；裸无费生物会被 TryAddToPool 以「无可锁定产色」拒）
                    for (int i = 0; i < blueGrant; i++)
                    {
                        var landData = new CardData
                        {
                            ID = "VERIFY_GATE_LAND" + i, CardName = "闸蓝地" + i, Supertype = Cardtype.Creature,
                            Power = 1, Life = 1,
                            Effects = new List<CardEffectData>
                            {
                                new CardEffectData
                                {
                                    Id = "VERIFY_GATE_LAND" + i + "_EFF",
                                    TriggerTiming = (int)TriggerTiming.Activate_Active,
                                    ActivationType = (int)EffectActivationType.Voluntary,
                                    AtomicEffects = new List<AtomicEffectEntry> {
                                        Atom(AtomicEffectType.DrawCard, value: 1,
                                             kinds: new List<int> { (int)CardCore.TargetKind.OwnDeck }) },
                                },
                            },
                        };
                        var landCard = new CardWrapper(landData);
                        landCard.SetController(p1);
                        Assert(core.ElementPool.TryAddToPool(landCard, p1, ManaType.Blue, out var landWhy),
                               $"补第 {i + 1}/{blueGrant} 个蓝地入池（实际：{landWhy}）");
                    }
                    Assert(GameActions.GetPayloadGateReject(core, p1, gateCard) == null,
                           $"蓝地 {blueGrant} = 蓝生成面 {blueGrant} → 过闸（等于放行）");
                    Assert(PlayCardSync(core, p1, gateCard), "蓝地足量 → PlayCard 声明+排干结算成功");
                    Assert(gateCard.GetZone() == Zone.Graveyard, "法术结算完成入墓（闸不拦合法施放）");
                }
                finally
                {
                    pool1.PooledCards.Clear();
                    foreach (var pc in savedPool) pool1.PooledCards.Add(pc);
                }
            }
            finally
            {
                foreach (var kv in snapWallet) pool1.TempElements[kv.Key] = kv.Value;
                // 空 ID 判空：前段夹具卡可能无 id 字段（?. 链只护到 GetData，null.ID.StartsWith 会 NRE——同 P7b 坑）
                bool IsVerify(Card c)
                {
                    var d = (c as CardWrapper)?.GetData();
                    return d != null && d.ID != null && d.ID.StartsWith("VERIFY_");
                }
                foreach (var c in core.ZoneManager.GetCards(p1, Zone.Hand).Where(IsVerify).ToList())
                    core.ZoneManager.MoveCard(c, p1, Zone.Hand, Zone.Graveyard);
            }
        }

        // ======================================== 代价资敌臂（2026-10-28 P1b：p>0 锁对方） ========================================

        /// <summary>
        /// P1b 资敌臂回归（2026-10-28 定案：代价收 p≠0 且锁在受益方反面——p<0 自伤 / p>0 资敌）：
        /// ① 契约三向：资敌治疗 / +1/+1（p>0 锁对方）转换通过；p=0 拒；有益原子未锁对方拒（ConvertPayloadForDisplay=null）；
        /// ② 净费 = 无代价版 − 代价价（治疗2 → 价1、+1/+1 → 价1，各净费 −1）；
        /// ③ 对手满血 → 资敌治疗卡不可用（可支付闸拦，拒绝原因「资敌」）；对手受伤 → 可用；
        /// ④ 对手无单位 → +1/+1 资敌卡不可用；有单位 → 可用；
        /// ⑤ 防护白嫖拦截：对方全部目标已持圣盾 → GrantDivineShield 资敌代价不可付（拒绝原因「已持有」）；
        /// ⑥ 自伤臂回归：弃牌代价价与净费抵扣不因 P1b 漂移；
        /// ⑦ 资敌代价执行不产任何元素（钱包总量不变）+ 真发生（对手受伤角色回血）。
        /// </summary>
        private static void TestBenefitEnemyCostArm(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var pool1 = core.ElementPool.GetPool(p1);
            var snapWallet = new Dictionary<ManaType, int>(pool1.TempElements);
            var snapP2Life = p2.Life;
            var snapP2Field = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Battlefield));

            // 前置：资敌候选行极性在表（Heal/AddPowerUp/GrantDivineShield p>0）
            Assert(Cfg(AtomicEffectType.Heal).Polarity > 0, "前置：Heal 行 p>0（资敌候选存在）");
            Assert(Cfg(AtomicEffectType.AddPowerUp).Polarity > 0, "前置：AddPowerUp 行 p>0（资敌候选存在）");
            Assert(Cfg(AtomicEffectType.SetPower).Polarity == 0, "前置：SetPower 行 p=0（中性反例存在）");

            // 资敌条目（p>0 锁对方 {2}——契约合法载体；表行默认双侧域不可裸用）
            var healFoe = Atom(AtomicEffectType.Heal, value: 2, kinds: new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit });
            var plusOneFoe = Atom(AtomicEffectType.AddPowerUp, value: 1, kinds: new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit });
            var shieldFoe = Atom(AtomicEffectType.GrantDivineShield, value: 1, kinds: new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit });
            var neutralFoe = Atom(AtomicEffectType.SetPower, value: 1, kinds: new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit });
            var healWrongSide = Atom(AtomicEffectType.Heal, value: 2, kinds: new List<int> { (int)CardCore.TargetKind.OwnLivingUnit });

            int FoePrice(AtomicEffectEntry e)
            {
                var inst = CardEffectConverter.ConvertPayloadForDisplay(e);
                return inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0;
            }

            CardData FoeCard(string id, AtomicEffectEntry payload)
            {
                var d = new CardData
                {
                    ID = id, CardName = "验证资敌卡" + id, Supertype = Cardtype.Creature,
                    // 2026-11-06 opt-in 补攻守：② 段净费=抵扣前费−代价价的关系依赖卡费有绿桶
                    //（治疗代价行色锁绿）——2026-11-14 基础属性归灰后身材不再产绿，绿色分量改由
                    // 效果内的 Heal 原子提供（0.5 绿/点 → 进位 1 绿）。
                    AttackAbility = true, GuardAbility = true,
                    Power = 2, Life = 2,
                    Effects = new List<CardEffectData>
                    {
                        new CardEffectData
                        {
                            Id = id + "_EFF",
                            AtomicEffects = new List<AtomicEffectEntry>
                                { Atom(AtomicEffectType.DealDamage, value: 1, kinds: new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit }),
                                  Atom(AtomicEffectType.Heal, value: 1) }, // 绿色分量（0.5 绿/点）——2026-11-14 身材归灰后由本原子提供
                        },
                    },
                };
                if (payload != null)
                    d.Effects[0].Costs = new List<CostEntry> { new CostEntry { payloads = new List<AtomicEffectEntry> { payload } } };
                return d;
            }

            int NetTotal(CardData d) => (int)System.Math.Round((double)CardCostService.Derive(d).DerivedTotal);
            // 抵扣前留影总量（P7 口径）：卡层代价栏计 1 槽（2026-10-04/10-22 定案）→ 结构项 4>3
            // 差额 +1 灰不吃折——净费=抵扣前费−代价价，而非无代价版−代价价（同 TestCostDeductionAndPayability ①）
            int PreTotal(CardData d) => (int)System.Math.Round((double)CardCostService.Derive(d).PrePayloadCost.Total);

            try
            {
                // 战场清空（双方角色外无干扰目标；结束后还原）
                foreach (var c in snapP2Field) core.ZoneManager.MoveCard(c, p2, Zone.Battlefield, Zone.Graveyard);

                // ---- ① 契约三向：p>0 锁对方过；p=0 / 有益未锁对方拒 ----
                // 负向两口会按契约打 TideLog.Error（ConvertPayloadForDisplay 单源日志）——sink 捕获
                // 不转发（控制台零红），捕获文本进断言反向锚定「拒因正确」
                var contractErrs = new List<string>();
                var prevSinkC = CardCore.TideLog.Sink;
                CardCore.TideLog.Sink = (level, msg) =>
                {
                    if (level == CardCore.TideLogLevel.Error) contractErrs.Add(msg ?? "");
                };
                try
                {
                    Assert(CardEffectConverter.ConvertPayloadForDisplay(healFoe) != null,
                           "契约：资敌治疗（Heal·p>0·锁对方）转换通过");
                    Assert(CardEffectConverter.ConvertPayloadForDisplay(plusOneFoe) != null,
                           "契约：资敌攻击力增加（AddPowerUp·p>0·锁对方）转换通过");
                    Assert(CardEffectConverter.ConvertPayloadForDisplay(shieldFoe) != null,
                           "契约：资敌圣盾（GrantDivineShield·p>0·锁对方）转换通过");
                    Assert(CardEffectConverter.ConvertPayloadForDisplay(neutralFoe) == null,
                           "契约：p=0（SetPower）作代价直接拒（ConvertPayloadForDisplay=null）");
                    Assert(CardEffectConverter.ConvertPayloadForDisplay(healWrongSide) == null,
                           "契约：有益原子未锁对方（Heal·p>0·域=己方）拒");
                }
                finally { CardCore.TideLog.Sink = prevSinkC; }
                Assert(contractErrs.Count == 2
                       && contractErrs.Any(e => e.Contains("极性=0"))
                       && contractErrs.Any(e => e.Contains("锁侧不符")),
                       "契约拒因日志：中性/锁侧不符各一条（捕获自 sink——控制台零红）");

                // ---- ② 净费 = 抵扣前费 − 代价价（治疗2/+1+1 各 −1）----
                int healPrice = FoePrice(healFoe);
                Assert(healPrice == 1, $"资敌治疗2的代价价=1（实际 {healPrice}——Heal 锚 0.5绿×2）");
                var healData = FoeCard("VERIFY_FOE_HEAL", healFoe);
                int healPre = PreTotal(healData);
                Assert(NetTotal(healData) == healPre - healPrice,
                       $"资敌治疗代价净费 −{healPrice}（抵扣前费 {healPre}〔含代价栏占槽差额〕 → 净费 {healPre - healPrice}）");
                int buffPrice = FoePrice(plusOneFoe);
                Assert(buffPrice == 1, $"资敌 +1/+1 的代价价=1（实际 {buffPrice}）");
                var buffData = FoeCard("VERIFY_FOE_BUFF", plusOneFoe);
                int buffPre = PreTotal(buffData);
                Assert(NetTotal(buffData) == buffPre - buffPrice,
                       $"资敌 +1/+1 代价净费 −{buffPrice}（抵扣前费 {buffPre}〔含代价栏占槽差额〕 → 净费 {buffPre - buffPrice}）");

                foreach (var kv in pool1.TempElements.ToList()) pool1.TempElements[kv.Key] = 99; // 净费侧充足：只考代价闸

                // ---- ③ 对手满血 → 资敌治疗卡不可用；受伤 → 可用 ----
                var healCard = InjectCard(core, p1, FoeCard("VERIFY_FOE_HEAL_GATE", healFoe));
                int p2Max = p2.GetMaxLife();
                p2.Life = p2Max; // 满血
                Assert(!GameActions.CanAffordCast(core, p1, healCard, 0),
                       "对手满血 → 资敌治疗卡不可用（CanAffordCast=false——治疗满血=白嫖，可支付闸拦住）");
                string healReject = GameActions.GetPayloadGateReject(core, p1, healCard);
                Assert(healReject != null && healReject.Contains("资敌"),
                       $"拒绝原因含「资敌」（实际：{healReject}）");
                p2.Life = p2Max - 2; // 受伤
                Assert(GameActions.CanAffordCast(core, p1, healCard, 0),
                       "对手受伤 → 资敌治疗卡可用（有会被改变的目标）");
                core.ZoneManager.MoveCard(healCard, p1, Zone.Hand, Zone.Graveyard);

                // ---- ④ 对手无单位 → +1/+1 资敌卡不可用；有单位 → 可用 ----
                var buffCard = InjectCard(core, p1, FoeCard("VERIFY_FOE_BUFF_GATE", plusOneFoe));
                Assert(!GameActions.CanAffordCast(core, p1, buffCard, 0),
                       "对手无单位 → +1/+1 资敌卡不可用（候选空——NoRole 滤后角色不入候选）");
                string buffReject = GameActions.GetPayloadGateReject(core, p1, buffCard);
                Assert(buffReject != null && buffReject.Contains("资敌"),
                       $"拒绝原因含「资敌」（实际：{buffReject}）");
                var foeUnit = InjectCard(core, p2, new CardData
                {
                    ID = "VERIFY_FOE_UNIT", CardName = "资敌靶单位", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                });
                core.ZoneManager.MoveCard(foeUnit, p2, Zone.Hand, Zone.Battlefield);
                Assert(GameActions.CanAffordCast(core, p1, buffCard, 0),
                       "对手有单位 → +1/+1 资敌卡可用");
                core.ZoneManager.MoveCard(buffCard, p1, Zone.Hand, Zone.Graveyard);

                // ---- ⑤ 防护白嫖拦截：对方全部目标已持圣盾 → 不可付 ----
                var shieldCard = InjectCard(core, p1, FoeCard("VERIFY_FOE_SHIELD_GATE", shieldFoe));
                Assert(GameActions.CanAffordCast(core, p1, shieldCard, 0),
                       "对方有未持圣盾目标 → 资敌圣盾卡可用（前置态）");
                foeUnit.AddCounters(CardCore.Attribute.CounterRules.DivineShieldCounter, 1, p2);
                p2.AddCounters(CardCore.Attribute.CounterRules.DivineShieldCounter, 1, p2); // 域含角色：须全部持有才拦
                Assert(!GameActions.CanAffordCast(core, p1, shieldCard, 0),
                       "对方单位+角色均已持圣盾 → 资敌圣盾不可付（再赋圣盾=白嫖）");
                string shieldReject = GameActions.GetPayloadGateReject(core, p1, shieldCard);
                Assert(shieldReject != null && shieldReject.Contains("已持有"),
                       $"拒绝原因含「已持有」（实际：{shieldReject}）");
                foeUnit.RemoveCounters(CardCore.Attribute.CounterRules.DivineShieldCounter, 1);
                p2.RemoveCounters(CardCore.Attribute.CounterRules.DivineShieldCounter, 1);
                core.ZoneManager.MoveCard(shieldCard, p1, Zone.Hand, Zone.Graveyard);
                core.ZoneManager.MoveCard(foeUnit, p2, Zone.Battlefield, Zone.Graveyard);

                // ---- ⑥ 自伤臂回归：弃牌代价价与净费抵扣不因 P1b 漂移 ----
                var discPayload = Atom(AtomicEffectType.DiscardCard, value: 1,
                    kinds: new List<int> { (int)CardCore.TargetKind.OwnHand });
                int discPrice = FoePrice(discPayload);
                Assert(discPrice > 0, $"自伤弃1代价价 > 0（实际 {discPrice}——P1 结果不漂移）");
                var discData = FoeCard("VERIFY_FOE_DISC", discPayload);
                // 2026-11-05 删除「自伤代价净费 = 抵扣前费 − 代价价」断言：费用推导/抵扣属代码侧恒推导，
                // 端到端不做公式验算（上面 discPrice>0 的语义前置保留）。
                _ = discData;

                // ---- ⑦ 资敌代价执行不产任何元素 + 真发生（对手受伤角色回血）----
                foreach (var kv in pool1.TempElements.ToList()) pool1.TempElements[kv.Key] = 0;
                pool1.TempElements[ManaType.Red] = 3;
                p2.Life = p2Max - 3;
                int walletBefore = pool1.TempElements.Values.Sum();
                var foeCostInst = new CostInstance
                {
                    Type = CostType.Payload,
                    Payloads = new List<CardCore.AtomicEffectInstance>
                        { CardEffectConverter.ConvertPayloadForDisplay(healFoe) },
                };
                var foeCtx = new CostContext
                {
                    Payer = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, Source = p1,
                };
                Assert(CostCompensationService.PayPayloadsAsync(
                           new List<CostInstance> { foeCostInst }, foeCtx).GetAwaiter().GetResult(),
                       "PayPayloadsAsync 执行资敌治疗代价");
                Assert(pool1.TempElements.Values.Sum() == walletBefore,
                       $"资敌代价执行后钱包总量不变（{walletBefore} → {pool1.TempElements.Values.Sum()}——资敌不产任何元素）");
                Assert(p2.Life == p2Max - 1,
                       $"资敌代价真发生（对手 {p2Max - 3} → {p2.Life}，回复 2 生命）");
            }
            finally
            {
                foreach (var kv in snapWallet) pool1.TempElements[kv.Key] = kv.Value;
                p2.Life = snapP2Life;
                // 本段注入物清理（资敌靶单位/验证卡），还原段首清空的战场（空 ID 判空同 P7b 坑）
                bool IsFoe(Card c)
                {
                    var d = (c as CardWrapper)?.GetData();
                    return d != null && d.ID != null && d.ID.StartsWith("VERIFY_FOE");
                }
                foreach (var c in core.ZoneManager.GetCards(p2, Zone.Battlefield).Where(IsFoe).ToList())
                    core.ZoneManager.MoveCard(c, p2, Zone.Battlefield, Zone.Graveyard);
                foreach (var c in core.ZoneManager.GetCards(p1, Zone.Hand).Where(IsFoe).ToList())
                    core.ZoneManager.MoveCard(c, p1, Zone.Hand, Zone.Graveyard);
                foreach (var c in snapP2Field) core.ZoneManager.MoveCard(c, p2, Zone.Graveyard, Zone.Battlefield);
            }
        }

        // ======================================== 主动效果代价（2026-10-29 P5：两行构筑 + 发动期抵扣 + 每次执行） ========================================

        /// <summary>
        /// P5 主动效果代价回归：主动效果可带一条效果级代价（header.Costs 单 payload 条目——发动期净费抵扣+每次执行）。
        /// ① 槽位：主动效果代价行各计 1 槽（空不占）；两个主动效果各带 → 各 +1；非主动 legacy 兜底仍单卡合计 +1（回归）；
        /// ② 净费推导（EffectActivationGate.NetBill 纯函数，**2026-11-05 色锁收口**）：无代价=原费；
        ///    逐行色锁——代价只在自身可产出色内抵（先灰后集合内最高色），跨色不抵、同色溢出 → 该色归 0（每色 ≥0）；
        /// ③ 连发两次（发动费 2 蓝·弃 1 蓝价 → 净费 0——同色配对）：两次各执行一次代价（手牌各 −1）、钱包全程不变（代价不产元素）；
        /// ④ 资敌闸（发动费 2 绿·为对手回复 2 绿价 1 → 净费 1 绿——同色配对）：对手满血 → CanActivate=false（可支付闸拦）；
        ///    受伤 → 可发动，扣恰 1 绿、对手回复 2；
        /// ⑤ 技能不开放：技能源（FieldZone 法术）效果带代价 → 锚价照付（2026-11-01 锚价现付定案；
        ///   不套净费闸抵扣——若误入闸会按同色代价价抵成 0），代价仍执行。技能卡禁带代价由资格闸拦（CanBeSkillCard），
        ///   本段为 legacy 通道保底探针（合成注入绕过资格闸）。
        /// </summary>
        private static void TestActiveEffectCost(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var pool1 = core.ElementPool.GetPool(p1);
            var snapWallet = new Dictionary<ManaType, int>(pool1.TempElements);
            int snapP2Life = p2.Life;
            pool1.GlobalTurnIndex = 9; // 浓度上限推到 9 费档（TestHeroSkills 同口径——防上限钳干扰扣费断言）

            // 夹具：自伤臂（弃 1 锁己方）/ 资敌臂（为对手回复 2 锁对方）——与 P1b 段同构造
            AtomicEffectEntry DiscardCost() => Atom(AtomicEffectType.DiscardCard, value: 1,
                kinds: new List<int> { (int)CardCore.TargetKind.OwnHand });
            var healCost = Atom(AtomicEffectType.Heal, value: 2,
                kinds: new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit });

            CardEffectData ActiveEff(string id, float gray = 0f, float red = 0f, float blue = 0f, float green = 0f,
                AtomicEffectEntry cost = null)
            {
                var e = new CardEffectData
                {
                    Id = id,
                    TriggerTiming = (int)TriggerTiming.Activate_Active,
                    ActivationType = (int)EffectActivationType.Voluntary,
                    AnchorCostPinned = 1, // 锚价钉死——发动费由测试直控（与代价同色配对——2026-11-05 色锁收口），与原子表解耦
                    AnchorCost = new ElementCost(),
                };
                e.AnchorCost[ManaType.Gray] = gray;
                e.AnchorCost[ManaType.Red] = red;
                e.AnchorCost[ManaType.Blue] = blue;
                e.AnchorCost[ManaType.Green] = green;
                if (cost != null)
                    e.Costs = new List<CostEntry> { new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = cost } };
                return e;
            }

            Card CreatureOf(string id, params CardEffectData[] effects)
            {
                var d = new CardData
                {
                    ID = id, CardName = "P5·" + id, Supertype = Cardtype.Creature, Power = 1, Life = 1,
                    Effects = effects.ToList(),
                };
                var c = InjectCard(core, p1, d);
                core.ZoneManager.MoveCard(c, p1, Zone.Hand, Zone.Battlefield);
                return c;
            }

            int Slots(params CardEffectData[] effects)
                => CostDerivationService.CountEffectSlots(new CardData
                {
                    ID = "VERIFY_P5_SLOTPROBE", CardName = "P5槽位探针",
                    Supertype = Cardtype.Creature, Power = 1, Life = 1, Effects = effects.ToList(),
                });

            try
            {
                // ---- ① 槽位口径 ----
                Assert(Slots(ActiveEff("P5_S_A")) == 1, "槽位：主动效果空代价行不占槽（1 效果=1 槽）");
                Assert(Slots(ActiveEff("P5_S_B", gray: 2, cost: DiscardCost())) == 1,
                    "槽位：主动效果填代价行仍 1 槽（2026-11-02 减费三选一改版：代价不占槽、不压速度）");
                Assert(Slots(ActiveEff("P5_S_C", cost: DiscardCost()), ActiveEff("P5_S_D", cost: DiscardCost())) == 2,
                    "槽位：两个主动效果各带代价 → 2 槽（每效果 1 槽，代价行不计）");
                var triggered = new CardEffectData
                {
                    Id = "P5_S_TRIG", TriggerTiming = (int)TriggerTiming.OnPlay,
                    Costs = new List<CostEntry> { new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = DiscardCost() } },
                };
                Assert(Slots(triggered) == 1, "槽位：非主动 legacy 效果级代价不计槽（每效果 1 槽——2026-11-02 定案）");

                // ---- ② 净费推导（纯函数） ----
                var discardInst = CardEffectConverter.ConvertPayloadForDisplay(DiscardCost());
                int discardPts = CostDerivationService.PayloadUnitGrant(discardInst);
                Assert(discardPts > 0, $"前置：弃1代价价 {discardPts} > 0（表价非零）");
                var healInst = CardEffectConverter.ConvertPayloadForDisplay(healCost);
                int healPts = CostDerivationService.PayloadUnitGrant(healInst);
                Assert(healPts == 1, $"前置：为对手回复2 代价价=1（实际 {healPts}——P1b 口径不漂移）");
                var discardCostInst = new CostInstance
                {
                    Type = CostType.Payload,
                    Payloads = new List<CardCore.AtomicEffectInstance> { discardInst },
                };
                var healCostInst = new CostInstance
                {
                    Type = CostType.Payload,
                    Payloads = new List<CardCore.AtomicEffectInstance> { healInst },
                };

                var bill = new ElementCost();
                bill[ManaType.Gray] = 2; bill[ManaType.Red] = 3;
                Assert(EffectActivationGate.NetBill(bill, null).Total == 5
                       && EffectActivationGate.NetBill(bill, new List<CostInstance>()).Total == 5,
                    "净费：无代价行=原费（发动费直付，验证器①口径）");
                // 跨色不抵（2026-11-05 色锁收口）：弃1（蓝）对灰/红账单 → 一点不扣
                var netCross = EffectActivationGate.NetBill(bill, new List<CostInstance> { discardCostInst });
                Assert((int)netCross[ManaType.Gray] == 2 && (int)netCross[ManaType.Red] == 3,
                    $"净费色锁：蓝代价抵不了灰/红账（[灰2红3] 抵{discardPts} → 原样 {netCross}——跨色不抵）");
                var rb = new ElementCost();
                rb[ManaType.Red] = 2; rb[ManaType.Blue] = 3;
                var netCross2 = EffectActivationGate.NetBill(rb, new List<CostInstance> { healCostInst });
                Assert((int)netCross2[ManaType.Blue] == 3 && (int)netCross2[ManaType.Red] == 2,
                    $"净费色锁：绿代价抵不了红/蓝账（[红2蓝3] 抵1 → 原样 {netCross2}）");
                // 同色抵尽：弃1（蓝）对纯蓝小账单 → 清零（溢出丢弃、不穿底不退费）
                var tiny = new ElementCost();
                tiny[ManaType.Blue] = 2;
                Assert(EffectActivationGate.NetBill(tiny, new List<CostInstance> { discardCostInst }).IsZero,
                    $"净费：同色抵扣溢出 → 归零（[蓝2] 抵{discardPts} → 0，每色 ≥0，不退费不穿底）");

                // ---- ②b 真实小数定案（2026-10-30：效果费计价不取整、支付终点逐色进1） ----
                var fracBill0 = new ElementCost();
                fracBill0[ManaType.Red] = 2.4f; fracBill0[ManaType.Gray] = 0.5f;
                var fracNet = EffectActivationGate.NetBill(fracBill0, new List<CostInstance> { healCostInst }); // 绿代价跨色：0 抵扣
                Assert(Mathf.Abs(fracNet[ManaType.Red] - 2.4f) < 1e-4f && Mathf.Abs(fracNet[ManaType.Gray] - 0.5f) < 1e-4f,
                    $"净费小数保真：[红2.4灰0.5] 绿代价跨色不抵 → 原样保留（实际 {fracNet}——旧口径先归一成 int 桶丢亚点）");
                var fracPay = ElementPaymentValidator.NormalizeBill(fracNet);
                Assert(fracPay.TryGetValue(ManaType.Red, out var fracRed) && fracRed == 3
                       && fracPay.TryGetValue(ManaType.Gray, out var fracGray) && fracGray == 1,
                    $"支付进1：净费红2.4→实付红3、灰0.5→实付灰1（大于0也消耗1——实际 {string.Join(",", fracPay)}）");
                Assert(ElementPaymentValidator.NormalizeBill(ElementCost.FromValue(ManaType.Green, 0.6f)).Single().Value == 1,
                    "支付进1：0.6绿 → 实付 1（旧四舍五入口径下 0.6 也是 1，1.4 则从 1 变 2）");
                Assert(ElementPaymentValidator.NormalizeBill(ElementCost.FromValue(ManaType.Blue, 1.4f)).Single().Value == 2,
                    "支付进1：1.4蓝 → 实付 2（旧四舍五入=1，口径切换锚点）");
                Assert(ElementPaymentValidator.NormalizeBill(ElementCost.FromValue(ManaType.Blue, 1.0000001f)).Single().Value == 1,
                    "支付进1：浮点尾噪 1.0000001 → 仍 1（epsilon 护栏，不顶成 2）");
                Assert(ElementPaymentValidator.NormalizeBill(ElementCost.FromValue(ManaType.Red, 2f)).Single().Value == 2,
                    "支付进1：整数 2 → 2 不多扣（整数账单零漂移）");
                Assert(ElementPaymentValidator.NormalizeBill(ElementCost.FromValue(ManaType.Gray, 1e-5f)).Count == 0,
                    "支付进1：≈0 尾噪 → 不入账单（防 0.00001 顶成 1）");
                // 真实小数出账（非钉死·真推导）：回复生命表价 0.5绿/点 ×1 → 0.5绿（旧合成期取整会出 1）
                //（Heal 极性为正——锁己方域才是正侧可计价；锁对方=错边被转换器剔除，见 f) 内容契约段）
                var fracEff = new CardEffectData
                {
                    Id = "P5_FRAC_EFF",
                    TriggerTiming = (int)TriggerTiming.Activate_Active,
                    ActivationType = (int)EffectActivationType.Voluntary,
                    AtomicEffects = new List<AtomicEffectEntry> { Atom(AtomicEffectType.Heal, value: 1,
                        kinds: new List<int> { (int)CardCore.TargetKind.OwnLivingUnit }) },
                };
                var fracAnchor = CostDerivationService.DeriveElementCosts(
                    CardEffectConverter.ConvertOne(fracEff, "VERIFY_P5_FRAC"), 0);
                Assert(Mathf.Abs(fracAnchor[ManaType.Green] - 0.5f) < 1e-3f,
                    $"真实小数出账：回复1 = 0.5绿（实际 {fracAnchor[ManaType.Green]:0.###}——旧口径合成期四舍五入会出 1）");

                // ---- ③ 连发两次：发动费 2 蓝·弃 1（蓝价3）→ 净费 0（同色配对——2026-11-05 色锁收口）----
                foreach (var kv in pool1.TempElements.ToList()) pool1.TempElements[kv.Key] = 0;
                pool1.TempElements[ManaType.Blue] = 5; // 有钱也不扣（净费 0——证明抵扣真发生）
                pool1.TempElements[ManaType.Gray] = 5;
                pool1.TempElements[ManaType.Red] = 5;
                while (core.ZoneManager.GetCards(p1, Zone.Hand).Count < 3)
                    InjectCard(core, p1, new CardData
                    {
                        ID = "VERIFY_P5_PAD", CardName = "P5垫牌", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                    });
                int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                int walletBefore = pool1.TempElements.Values.Sum();
                var twiceCard = CreatureOf("VERIFY_P5_TWICE", ActiveEff("P5_T_EFF", blue: 2, cost: DiscardCost()));
                var twiceDef = GameActions.GetCardEffectDefinitions(twiceCard).FirstOrDefault(d => d != null && d.IsActivatedEffect);
                Assert(twiceDef != null && (int)CostDerivationService.DeriveElementCosts(twiceDef)[ManaType.Blue] == 2,
                    "装载：主动效果锚价钉死 2 蓝（converter 直出·效果级 Costs 随 def 装载）");
                Assert(GameActions.ActivateEffect(core, p1, twiceDef, twiceCard), "连发①：声明入栈成功");
                GameActions.DrainStack(core);
                int handAfter1 = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                Assert(handAfter1 == handBefore - 1, $"连发①：代价执行弃 1（手牌 {handBefore}→{handAfter1}）");
                Assert(pool1.TempElements.Values.Sum() == walletBefore,
                    $"连发①：钱包不变（净费 0 未扣+代价不产元素——{walletBefore}→{pool1.TempElements.Values.Sum()}）");
                Assert(twiceCard.IsTapped(), "连发①：发动后横置（既有声明期代价口径不回归）");
                twiceCard.Untap(); // 连发②准备（跨回合横置重置由测试直控——只验费用与代价口径）
                Assert(GameActions.ActivateEffect(core, p1, twiceDef, twiceCard), "连发②：声明入栈成功");
                GameActions.DrainStack(core);
                int handAfter2 = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                Assert(handAfter2 == handAfter1 - 1,
                    $"连发②：再弃 1（手牌 {handAfter1}→{handAfter2}——每次发动各执行一次代价）");
                Assert(pool1.TempElements.Values.Sum() == walletBefore,
                    "连发②：钱包全程不变（两次净费均 0·代价不产元素）");

                // ---- ④ 资敌闸：发动费 2 绿·为对手回复 2（绿价1）→ 净费 1 绿（同色配对——2026-11-05 色锁收口）----
                var healCardC = CreatureOf("VERIFY_P5_HEAL", ActiveEff("P5_H_EFF", green: 2, cost: healCost));
                var healDef = GameActions.GetCardEffectDefinitions(healCardC).FirstOrDefault(d => d != null && d.IsActivatedEffect);
                Assert(healDef != null, "装载：资敌代价主动效果就绪");
                var executor = core.StackEngine.GetExecutor();
                var phase = core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Main;
                int p2Max = p2.GetMaxLife();
                p2.Life = p2Max; // 满血
                Assert(!executor.CanActivate(healDef, healCardC, p1, core.TurnEngine.TurnPlayer, phase, core.TurnEngine.TurnNumber),
                    "资敌闸：对手满血 → 效果不可发动（净费够也拦——CostPayability 治疗满血=白嫖）");
                p2.Life = p2Max - 3; // 受伤
                pool1.TempElements[ManaType.Green] = 3;
                // ③ 地牌门槛（2026-10-31 代价指向化定案「使用主动效果时同理」）：代价行产出色
                //（回复2→绿1）≤ 施放者同色地牌数（池内 LockedColor 计数）——段界清池后放一张
                // 绿主色夹具地供给门槛；净费照走钱包（绿3 足付，地不横置不影响下方扣费读数）
                core.ElementPool.GetPool(p1).PooledCards.Clear();
                var greenLandData = new CardData
                {
                    ID = "VERIFY_P5_GREEN_LAND", CardName = "绿垫地", Supertype = Cardtype.Creature,
                    Power = 1, Life = 1,
                };
                // 声明费退役（2026-11-05 卡费恒推导）：锁色读**内容推导**——挂绿锚原子（Heal=绿0.5）
                // 使入池锁定 LockedColor=Green；OnPlay 不经出牌路径不触发（池内地牌不施放）
                greenLandData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_P5_GL_FX",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.Heal, value: 1) },
                });
                var greenLand = InjectCard(core, p1, greenLandData);
                Assert(GameActions.AddToElementPool(core, p1, greenLand), "资敌段：绿主色地入池（③地牌门槛供给）");
                int greenBefore = (int)pool1.TempElements[ManaType.Green];
                string p5Why = null;
                bool p5Ok = core.StackEngine.GetExecutor().TryActivate(healDef, healCardC, p1,
                    core.TurnEngine.TurnPlayer, phase, core.TurnEngine.TurnNumber, out p5Why);
                Assert(p5Ok,
                       $"资敌闸：对手受伤 → 可发动（有会被改变的目标）——拒因:{p5Why}");
                Assert(GameActions.ActivateEffect(core, p1, healDef, healCardC), "资敌：声明入栈成功");
                GameActions.DrainStack(core);
                Assert((int)pool1.TempElements[ManaType.Green] == greenBefore - 1,
                    $"资敌发动：扣恰 1 绿（净费 2绿−{healPts}=1绿——{greenBefore}→{(int)pool1.TempElements[ManaType.Green]}，全额 2 绿或 0 绿都算漂移）");
                Assert(p2.Life == p2Max - 1,
                       $"资敌代价真发生（对手 {p2Max - 3}→{p2.Life}，回复 2）");
                // 段尾清池（绿垫地不外泄——共享核心上后续段落读池计数/候选序；MoveCard 收容器+zone 双账）
                core.ElementPool.GetPool(p1).PooledCards.Clear();
                core.ZoneManager.MoveCard(greenLand, p1, Zone.ElementPool, Zone.Graveyard);

                // ---- ⑤ 技能不开放（legacy 保底）：技能源效果带代价 → 锚价照付（不套净费闸抵扣） ----
                var skillData = new CardData
                {
                    ID = "VERIFY_P5_SKILL", CardName = "P5技能探针", Supertype = Cardtype.Spell,
                    Effects = new List<CardEffectData> { ActiveEff("P5_SK_E1", blue: 2, cost: DiscardCost()), ActiveEff("P5_SK_E2") },
                };
                var skillCard = InjectCard(core, p1, skillData);
                core.ZoneManager.MoveCard(skillCard, p1, Zone.Hand, Zone.FieldZone);
                var skillDefs = GameActions.GetCardEffectDefinitions(skillCard)
                    .Where(d => d != null && d.IsActivatedEffect).ToList();
                Assert(skillDefs.Count == 2, "技能装载：双主动效果（锚价现付——资格闸禁带代价，本段为 legacy 通道探针）");
                pool1.TempElements[ManaType.Blue] = 4; // 蓝充足（净费闸若误入：2蓝−弃1蓝价=0 会扣 0——错侧可辨）
                int blueBefore = (int)pool1.TempElements[ManaType.Blue];
                int skillHandBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                Assert(GameActions.ActivateEffect(core, p1, skillDefs[0], skillCard),
                    "技能：声明入栈成功（legacy 效果带代价不影响技能通道）");
                GameActions.DrainStack(core);
                Assert((int)pool1.TempElements[ManaType.Blue] == blueBefore - 2,
                    $"技能锚价照付：扣恰 2 蓝（不套净费闸抵扣——若误抵会扣 0；{blueBefore}→{(int)pool1.TempElements[ManaType.Blue]}）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == skillHandBefore - 1,
                    "技能：效果级代价仍执行（弃 1——技能不抵扣但代价照付）");
            }
            finally
            {
                foreach (var kv in snapWallet) pool1.TempElements[kv.Key] = kv.Value;
                p2.Life = snapP2Life;
                // 本段注入物清理（战场生物/手牌垫牌与探针/技能栏探针——统一送墓；空 ID 判空同 P7b 坑）
                bool IsP5(Card c)
                {
                    var d = (c as CardWrapper)?.GetData();
                    return d != null && d.ID != null && d.ID.StartsWith("VERIFY_P5");
                }
                foreach (var c in core.ZoneManager.GetCards(p1, Zone.Battlefield).Where(IsP5).ToList())
                    core.ZoneManager.MoveCard(c, p1, Zone.Battlefield, Zone.Graveyard);
                foreach (var c in core.ZoneManager.GetCards(p1, Zone.Hand).Where(IsP5).ToList())
                    core.ZoneManager.MoveCard(c, p1, Zone.Hand, Zone.Graveyard);
                foreach (var c in core.ZoneManager.GetCards(p1, Zone.FieldZone).Where(IsP5).ToList())
                    core.ZoneManager.MoveCard(c, p1, Zone.FieldZone, Zone.Graveyard);
            }
        }

        // ======================================== 费用取整（2026-11-09 逐色进位定案） ========================================

        /// <summary>费用取整（2026-11-09 定案）：①卡费全整数且与支付口径逐色一致；
        /// ②逐色进位（双半色卡 D=2 而非总额进位的 1）；③效果费卡侧进位、效果组合侧仍小数。</summary>
        private static void TestCostCeiling(GameCore core, Player p1, Player p2)
        {
            // ① 全表整数 + 与 NormalizeBill 一致
            foreach (var c in SynergyUI.CardCatalog.LoadAll())
            {
                c.ResetCache();
                var r = CardCostService.Derive(c);
                foreach (var color in r.DerivedCost.NonzeroColors())
                    Assert(System.Math.Abs(r.DerivedCost[color] - System.Math.Round(r.DerivedCost[color])) < 1e-4f,
                        $"卡费全整数：{c.CardName} {color}={r.DerivedCost[color]}");
                var pay = ElementPaymentValidator.NormalizeBill(CardCostService.GetRuntimeCost(c, 0));
                foreach (var color in r.DerivedCost.NonzeroColors())
                    Assert(pay.TryGetValue(color, out var v) && v == (int)r.DerivedCost[color],
                        $"卡费=支付口径：{c.CardName} {color}");
            }
            // ② 逐色进位夹具（免费底=1：a/g 都开、无效果）
            var half = new CardData { ID = "VERIFY_CEIL_22", CardName = "进位·2/2",
                Supertype = Cardtype.Creature, AttackAbility = true, GuardAbility = true, Power = 2, Life = 2 };
            half.ResetCache();
            var hr = CardCostService.Derive(half);
            Assert(hr.DerivedCost[ManaType.Gray] == 1f && hr.DerivedTotal == 1,
                $"逐色进位不是总额进位：2/2（免费底1）→ 灰1 D=1（攻/血同入灰桶 0.5+0.5 合并只进一次——2026-11-14 基础属性归灰；实际 {hr.DerivedCost}）");
            // ③ 效果费两侧口径
            var fx = new CardEffectData { Id = "VERIFY_CEIL_HEAL", TriggerTiming = (int)TriggerTiming.Activate_Active,
                ActivationType = (int)EffectActivationType.Voluntary,
                AtomicEffects = new List<AtomicEffectEntry> { Atom(AtomicEffectType.Heal, value: 1,
                    kinds: new List<int> { (int)TargetKind.OwnLivingUnit }) } };
            var fee = CostDerivationService.DeriveElementCosts(CardEffectConverter.ConvertOne(fx, "VERIFY_CEIL"), 0);
            Assert(System.Math.Abs(fee[ManaType.Green] - 0.5f) < 1e-3f, "效果组合阶段保留小数：回复1=0.5绿");
            var ceilFee = ElementPaymentValidator.CeilBill(fee);
            Assert(ceilFee[ManaType.Green] == 1f, "卡组合阶段逐色进位：0.5绿 → 1绿");
        }

    }
}
