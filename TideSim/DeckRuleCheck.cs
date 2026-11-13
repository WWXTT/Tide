using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 卡组校验·效果组合合法性回归（2026-11-13 定案）：代价与门奖励不能并存（自由分支/有限分支
    /// 与代价三选一）、两种分支不能叠加、奖励不能再嵌分支。DeckCompositionValidator.CheckEffectLegality
    /// 是消费口；本检查既跑真实卡（活体炸弹已删首卡奖励、必须合规），也跑一棵合成违规树
    /// （代价 + 首卡门奖励 = 改版前的活体炸弹形状，必须被点名）。
    /// 用法：dotnet run --project TideSim -- --deck-rule
    /// </summary>
    internal static class DeckRuleCheck
    {
        private static int _fail;

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"  {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            CardCatalog.LoadAll();
            Console.WriteLine("【卡组校验】效果组合合法性（代价×门奖励互斥）");

            // ---- 真实卡：活体炸弹（代价「消灭一个己方生物」，门奖励已删）必须合规 ----
            var bomb = CardCatalog.GetById("C_5705F9FE");
            Assert(bomb != null, "夹具：卡池取到活体炸弹");
            if (bomb != null)
            {
                var errs = DeckCompositionValidator.CheckEffectLegality(bomb);
                Assert(errs.Count == 0, "活体炸弹（只剩代价、无门奖励）合规"
                                        + (errs.Count == 0 ? "" : "——" + string.Join("；", errs)));
            }

            // ---- 合成违规树：代价 + 首卡门奖励（= 改版前活体炸弹）必须被点名 ----
            var illegal = BuildIllegalBombShape();
            var bad = DeckCompositionValidator.CheckEffectLegality(illegal);
            Assert(bad.Count >= 1, "代价+门奖励并存被点名" + (bad.Count > 0 ? "——" + bad[0] : "（未命中）"));

            // ---- 合成违规树：自由分支 + 有限分支同挂 → 点名 ----
            var both = BuildTwoBranchShape();
            var bothErrs = DeckCompositionValidator.CheckEffectLegality(both);
            Assert(bothErrs.Any(e => e.Contains("两种分支") || e.Contains("有限分支") || e.Contains("自由分支")),
                "自由分支+有限分支并存被点名" + (bothErrs.Count > 0 ? "——" + bothErrs[0] : "（未命中）"));

            Console.WriteLine(_fail == 0 ? "卡组校验回归：通过" : $"卡组校验回归：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>改版前活体炸弹形状：效果级代价（消灭一个己方生物）+ 首卡门奖励（+2 伤害）。</summary>
        private static CardData BuildIllegalBombShape()
        {
            var cost = new CostEntry { CostType = (int)CostType.Payload };
            cost.payloads = new List<AtomicEffectEntry>
            {
                new AtomicEffectEntry { refId = "a50b8c19", kinds = new List<int> { 1 } }
            };
            var main = new AtomicEffectEntry
            {
                refId = "2cca9b23",
                kinds = new List<int> { 1 },
                branch = new BranchEntryData
                {
                    settle = (int)BranchSettleKind.Gate,
                    gateId = "FirstCardThisTurn",
                    then = new List<AtomicEffectEntry> { new AtomicEffectEntry { refId = "a4b823fc", value = 2 } },
                },
            };
            return new CardData
            {
                ID = "T_ILLEGAL_BOMB",
                CardName = "测试·代价+门奖励",
                Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Id = "T_ILLEGAL_BOMB_FX",
                        DisplayName = "测试·代价+门奖励",
                        TriggerTiming = (int)TriggerTiming.Activate_Active,
                        ActivationType = (int)EffectActivationType.Voluntary,
                        Costs = new List<CostEntry> { cost },
                        Steps = new List<EffectStepData>
                        {
                            new EffectStepData { kind = 0, atomic = main }
                        },
                    }
                },
            };
        }

        /// <summary>自由分支（Engine）+ 有限分支（Gate）同挂一棵树。</summary>
        private static CardData BuildTwoBranchShape()
        {
            var engineAtom = new AtomicEffectEntry
            {
                refId = "2cca9b23",
                branch = new BranchEntryData
                {
                    settle = (int)BranchSettleKind.Engine,
                    engine = 1,
                    then = new List<AtomicEffectEntry> { new AtomicEffectEntry { refId = "a4b823fc", value = 1 } },
                },
            };
            var gateAtom = new AtomicEffectEntry
            {
                refId = "2cca9b23",
                branch = new BranchEntryData
                {
                    settle = (int)BranchSettleKind.Gate,
                    gateId = "LifeLe7",
                    then = new List<AtomicEffectEntry> { new AtomicEffectEntry { refId = "a4b823fc", value = 1 } },
                },
            };
            return new CardData
            {
                ID = "T_TWO_BRANCH",
                CardName = "测试·双分支",
                Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Id = "T_TWO_BRANCH_FX",
                        DisplayName = "测试·双分支",
                        TriggerTiming = (int)TriggerTiming.Activate_Active,
                        ActivationType = (int)EffectActivationType.Voluntary,
                        Steps = new List<EffectStepData>
                        {
                            new EffectStepData { kind = 0, atomic = engineAtom },
                            new EffectStepData { kind = 0, atomic = gateAtom },
                        },
                    }
                },
            };
        }
    }
}
