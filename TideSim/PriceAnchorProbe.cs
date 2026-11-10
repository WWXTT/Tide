using System;
using System.Collections.Generic;

namespace TideSim
{
    /// <summary>端到端计价锚 8 红取证探针（2026-11-10 临时，跑完删）：复现 CardPipelineVerifier
    /// Composer/Misc 段失败夹具，输出 CardCostService.Derive 现公式实际值（红/蓝/档/总额/Req），
    /// 供断言期望值按新公式（b6d61b210 公式定价）重算对账。</summary>
    internal static class PriceAnchorProbe
    {
        public static int Run()
        {
            Console.WriteLine("== 端到端计价锚 8 红取证（现公式实际值）==");
            Atom();

            // 夹具同构：MakeCostCard（攻守齐备）+ MakeEffect（OnDeath+单原子）
            CardCore.CardData Card(CardCore.Cardtype type, int? power, int? life,
                params CardCore.CardEffectData[] effects)
            {
                var d = new CardCore.CardData
                {
                    ID = "PROBE_COST", CardName = "计价探针", Supertype = type, Power = power, Life = life,
                    AttackAbility = type == CardCore.Cardtype.Creature,
                    GuardAbility = type == CardCore.Cardtype.Creature,
                };
                foreach (var e in effects) d.Effects.Add(e);
                return d;
            }
            CardCore.CardEffectData Eff(int value, params CardCore.AtomicEffectEntry[] atoms)
            {
                var e = new CardCore.CardEffectData { Id = "PROBE_EFF", TriggerTiming = (int)CardCore.TriggerTiming.OnDeath };
                e.AtomicEffects = new List<CardCore.AtomicEffectEntry>(atoms);
                return e;
            }
            void Dump(string label, CardCore.CardData c)
            {
                var r = CardCore.CardCostService.Derive(c);
                Console.WriteLine($"{label}: 红{r.DerivedCost[CardCore.ManaType.Red]:0.##} 蓝{r.DerivedCost[CardCore.ManaType.Blue]:0.##} "
                    + $"绿{r.DerivedCost[CardCore.ManaType.Green]:0.##} 灰{r.DerivedCost[CardCore.ManaType.Gray]:0.##} "
                    + $"| 档Ĉ={r.DerivedTier} D={r.DerivedTotal} Req={r.OffsetRequirement}");
            }

            // ①②③ 法术 4伤+1抽（红4+蓝2/Ĉ=6/SpeedBonus 无效）
            var fbEffect = Eff(4, At("DealDamage", 4));
            fbEffect.AtomicEffects.Add(N(CardCore.AtomicEffectType.DrawCard, 1));
            Dump("①法术4伤+1抽        ", Card(CardCore.Cardtype.Spell, null, null, fbEffect));
            var fbSpeedCard = Card(CardCore.Cardtype.Spell, null, null, fbEffect);
            fbSpeedCard.SpeedBonus = 2;
            Dump("③同上+SpeedBonus=2   ", fbSpeedCard);

            // ④ 9/9 挂3伤（灰9 快照）
            var bigBody = Card(CardCore.Cardtype.Creature, 9, 9, Eff(3, At("DealDamage", 3)));
            bigBody.Cost[(int)CardCore.ManaType.Gray] = 9;
            Dump("④9/9挂3伤            ", bigBody);

            // ⑤ 两效果 2/2（DealDamage3 + DrawCard1）
            Dump("⑤2/2两效果(伤3+抽1)  ", Card(CardCore.Cardtype.Creature, 2, 2,
                Eff(3, At("DealDamage", 3)), Eff(1, N(CardCore.AtomicEffectType.DrawCard, 1))));

            // ⑥ 1/1 三抽
            Dump("⑥1/1三效果(3×抽1)   ", Card(CardCore.Cardtype.Creature, 1, 1,
                Eff(1, N(CardCore.AtomicEffectType.DrawCard, 1)),
                Eff(1, N(CardCore.AtomicEffectType.DrawCard, 1)),
                Eff(1, N(CardCore.AtomicEffectType.DrawCard, 1))));

            // ⑦ 5/5 挂3伤无存档
            Dump("⑦5/5挂3伤(无存档)    ", Card(CardCore.Cardtype.Creature, 5, 5, Eff(3, At("DealDamage", 3))));

            // ⑧ Misc：OnDraw DealDamage5 各 limit 档红价
            foreach (var limit in new[] { 1, 2, 3, -1 })
            {
                var d = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
                {
                    Id = $"PROBE_CAP_P{limit}",
                    TriggerTiming = (int)CardCore.TriggerTiming.OnDraw,
                    TriggerLimitPerTurn = limit,
                    AtomicEffects = new List<CardCore.AtomicEffectEntry> { N(CardCore.AtomicEffectType.DealDamage, 5) },
                }, "PROBE_CAP_P");
                var red = CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Red];
                Console.WriteLine($"⑧OnDraw伤5 limit={limit,2}: 红{red:0.####}");
            }

            // ⑨ 拼点收口（R4 配套）：外层声明 Activate_*（端到端拼点夹具形态——主动档无事件锚）
            // → converter 回落自然缺省 OnAttack（评估窗口恢复）；对齐声明值（教学/现网数据形态）原样透传。
            CardCore.CardEffectData ClashFx(int timing) => new CardCore.CardEffectData
            {
                Id = "PROBE_CLASH",
                TriggerTiming = timing,
                AtomicEffects = new List<CardCore.AtomicEffectEntry>
                {
                    new CardCore.AtomicEffectEntry
                    {
                        refId = CardCore.Attribute.AtomicEffectTable.GetByEnumName("EngineClash")?.HashId,
                        branch = new CardCore.BranchEntryData
                        {
                            settle = (int)CardCore.BranchSettleKind.Engine,
                            engine = (int)CardCore.BranchEngineKind.Clash,
                            engineParam = 1,
                            then = new List<CardCore.AtomicEffectEntry> { N(CardCore.AtomicEffectType.DrawCard, 1) },
                        },
                    },
                },
            };
            var clashLazy = CardCore.CardEffectConverter.ConvertOne(ClashFx((int)CardCore.TriggerTiming.Activate_Active), "PROBE_CLASH");
            var clashAligned = CardCore.CardEffectConverter.ConvertOne(ClashFx((int)CardCore.TriggerTiming.OnAttack), "PROBE_CLASH");
            Console.WriteLine($"⑨拼点外层 Activate_Active → def.TriggerTiming={clashLazy?.TriggerTiming}（期望 OnAttack 回落）");
            Console.WriteLine($"⑨拼点外层 OnAttack 对齐   → def.TriggerTiming={clashAligned?.TriggerTiming}（期望 OnAttack 透传）");
            Console.WriteLine(clashLazy?.TriggerTiming == CardCore.TriggerTiming.OnAttack
                              && clashAligned?.TriggerTiming == CardCore.TriggerTiming.OnAttack
                ? "⑨ PASS：主动档声明回落缺省、对齐值透传"
                : "⑨ FAIL：收口未生效");

            // ⑩ 端到端拼点门槛现值：then=抽1 的 RewardThresholdOf（端到端夹具差额 5−2=3 是否仍达标）
            var clashPayload = new CardCore.BranchPayload
            {
                Settle = CardCore.BranchSettleKind.Engine,
                EngineKind = CardCore.BranchEngineKind.Clash,
                EngineParam = 1,
                Then = new List<CardCore.AtomicEffectInstance>
                    { CardCore.CardEffectConverter.ConvertAtomForUI(N(CardCore.AtomicEffectType.DrawCard, 1)) },
            };
            int th = CardCore.CostDerivationService.RewardThresholdOf(clashPayload.Then, clashPayload.EffectId);
            Console.WriteLine($"⑩拼点门槛（then=抽1）：{th}（端到端夹具差额 3 {(3 >= th ? ">=" : "<")} 门槛 —— {(3 >= th ? "达标" : "不达标·夹具须调")}）");
            return 0;
        }

        private static CardCore.AtomicEffectEntry At(string type, int value)
            => new CardCore.AtomicEffectEntry
            {
                refId = CardCore.Attribute.AtomicEffectTable.GetByEnumName(type)?.HashId, value = value,
            };
        private static CardCore.AtomicEffectEntry N(CardCore.AtomicEffectType type, int value)
            => CardCore.AtomRefs.New(type, value);

        private static void Atom()
        {
            // 表装载（TideSim 环境原子表常随装载链初始化——防御性确认非空）
            var n = 0;
            foreach (var r in CardCore.Attribute.AtomicEffectTable.GetAll()) if (r != null) n++;
            Console.WriteLine($"原子表行数: {n}");
        }
    }
}
