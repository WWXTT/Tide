using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 绿·慢速（倒计时/诅咒/祝福版）卡组构建器（2026-10-22 定案；同日迁教学表）：
    /// · **输出=教学表**（TutorialCards/TutorialEffects + TutorialData/绿·慢速.json）：玩家侧
    ///   卡组构筑/联机列表看不到也改不到；运行时经 DeckCatalog 当「·预组」只读可选；
    /// · 核心一：倒计时（Countdown，engine=1）载体=生物/结界，归零发奖励并重挂；
    /// · 核心二：诅咒（CurseOnDraw，engine=8）挂对手牌库、祝福（BlessingOnDraw，engine=9）挂自己牌库，
    ///   抽到指示物卡时自动结算 Then；
    /// · 保命：治疗/护甲/大屁股墙/再生；
    /// · 绿色主题纯度：出牌费与主动锚价只允许绿+灰；诅咒的毒/蚀命、祝福的回血/成长均为绿/灰。
    /// </summary>
    public static class GreenCountdownDeckBuilder
    {
        public const string DeckName = "绿·慢速";
        private const string ThemeTag = "ThemeGreenCountdown";

        [MenuItem("Tools/验证/构建绿·慢速（倒计时/诅咒/祝福版·写入教学表）")]
        public static void Build()
        {
            // 幂等：先移除上一轮绿卡（主表+教学表双侧，含衍生物模板），防 tactivate 序列化修正后旧 ID 残留
            TutorialDeckOutput.RemoveThemeCards(ThemeTag);
            EffectsLibrary.Reload();
            CardCatalog.Invalidate();

            var unique = new List<CardData>();
            var deckCards = new List<CardData>();

            void Add(CardData card, int copies)
            {
                unique.Add(card);
                for (int i = 0; i < copies; i++) deckCards.Add(card);
            }

            // ================= 衍生物模板（倒计时奖励） =================
            var token4 = Creature("远古树灵", 4, 4);
            token4.Tags.Add("衍生物");
            CardCostService.EnsureCost(token4);
            var token7 = Creature("世界树之影", 7, 7);
            token7.Tags.Add("衍生物");
            CardCostService.EnsureCost(token7);
            string token4Id = IdOf(token4);
            string token7Id = IdOf(token7);
            Add(token4, 1);
            Add(token7, 1); // 各 1 张进卡组（2026-10-22 额外区配套）：开局 InitGame 摘出落 Zone.Extra
                            // ——不进牌库不会被抽到，玩家点额外区格识别（卡组=30 正卡+2 模板）

            // ================= 倒计时载体（8） =================
            var c1 = Creature("年轮守卫", 1, 4);
            CountdownFx(c1, 4, AtomRefs.New(AtomicEffectType.SummonToken, 1, str: token4Id),
                "倒计时4：归零时召唤一个4/4远古树灵（重挂）");
            Add(c1, 2);

            var c2 = Creature("苔甲巨龟", 0, 5);
            CountdownFx(c2, 3, AtomRefs.New(AtomicEffectType.Heal, 6, Kinds(1)),
                "倒计时3：归零时为己方单位回复6点生命（重挂）");
            Add(c2, 2);

            var c3 = Enchantment("时之藤结界", 2);
            CountdownFx(c3, 3, AtomRefs.New(AtomicEffectType.AddArmor, 6, Kinds(1)),
                "倒计时3：归零时为己方单位添加6层护甲（重挂）");
            Add(c3, 2);

            var c4 = Creature("荆棘古树", 2, 5);
            CountdownFx(c4, 4, AtomRefs.New(AtomicEffectType.DealDamage, 4, Kinds(2)),
                "倒计时4：归零时对对方单位造成4点伤害（重挂）");
            Add(c4, 1);

            var c5 = Enchantment("世界树之种", 2);
            CountdownFx(c5, 6, AtomRefs.New(AtomicEffectType.SummonToken, 1, str: token7Id),
                "倒计时6：归零时召唤一个7/7世界树之影（重挂）");
            Add(c5, 1);

            // ================= 苏醒载体（自我沉睡 + 倒计时）：成长提前出，苏醒后倒计时才成功 =================
            var a1 = Creature("苏醒古树", 4, 5, "Vigilance");
            a1.Effects.Add(SelfSleepFx());
            CountdownFx(a1, 3, AtomRefs.New(AtomicEffectType.SummonToken, 1, str: token4Id),
                "倒计时3：归零时召唤一个4/4远古树灵（重挂）");
            Add(a1, 1);

            var a2 = Creature("苏醒巨像", 5, 7, "Regeneration");
            a2.Effects.Add(SelfSleepFx());
            CountdownFx(a2, 4, AtomRefs.New(AtomicEffectType.SummonToken, 1, str: token7Id),
                "倒计时4：归零时召唤一个7/7世界树之影（重挂）");
            Add(a2, 1);

            // ================= 诅咒 / 祝福（8） =================
            var k1 = Spell("毒雾诅咒");
            OnDrawEngineFx(k1, BranchEngineKind.CurseOnDraw, "诅咒",
                AtomRefs.New(AtomicEffectType.AddToxin, 2, Kinds(2)),
                "施放时给对手牌库随机2张卡附加诅咒；抽到时获得2层毒素（自动）");
            Add(k1, 3);

            var k2 = Spell("蚀命诅咒");
            OnDrawEngineFx(k2, BranchEngineKind.CurseOnDraw, "诅咒",
                AtomRefs.New(AtomicEffectType.AddLifeDown, 2, Kinds(2)),
                "施放时给对手牌库随机2张卡附加诅咒；抽到时失去2点生命（自动）");
            Add(k2, 1);

            var k3 = Spell("回春祝福");
            OnDrawEngineFx(k3, BranchEngineKind.BlessingOnDraw, "祝福",
                AtomRefs.New(AtomicEffectType.Heal, 4, Kinds(1)),
                "施放时给自己牌库随机2张卡附加祝福；抽到时回复4点生命（自动）");
            Add(k3, 3);

            var k4 = Spell("生长祝福");
            OnDrawEngineFx(k4, BranchEngineKind.BlessingOnDraw, "祝福",
                AtomRefs.New(AtomicEffectType.IncreaseLandCap, 1),
                "施放时给自己牌库随机2张卡附加祝福；抽到时地牌上限+1（自动）");
            Add(k4, 1);

            // ================= 保命（14） =================
            var h1 = Spell("回春术");
            h1.Effects.Add(ActiveFx("为一个己方单位回复4点生命",
                AtomRefs.New(AtomicEffectType.Heal, 4, Kinds(1))));
            Add(h1, 3);

            var h2 = Spell("木甲术");
            h2.Effects.Add(ActiveFx("为一个己方单位添加2层护甲",
                AtomRefs.New(AtomicEffectType.AddArmor, 2, Kinds(1))));
            Add(h2, 2);

            Add(Creature("荆棘壁", 0, 6), 3);
            Add(Creature("古木守卫", 2, 6), 2);
            Add(Creature("世界树苗", 4, 8), 1);
            Add(Creature("再生古树", 0, 6, "Regeneration"), 1);

            // ================= 英雄技能：成长（地牌上限+1） =================
            var skill = Spell("成长");
            skill.Effects.Add(ActiveNoTargetFx("地牌上限+1",
                AtomRefs.New(AtomicEffectType.IncreaseLandCap, 1)));

            // ================= 推导费 → 落教学表 =================
            foreach (var card in unique.Concat(new[] { skill }))
                CardCostService.EnsureCost(card);

            TutorialDeckOutput.RemoveDeck(DeckName);
            foreach (var card in unique)
                TutorialDeckOutput.SaveCard(card);
            TutorialDeckOutput.SaveCard(skill);

            EffectsLibrary.Reload();
            CardCatalog.Invalidate();

            var deck = new DeckData(DeckName);
            foreach (var card in deckCards)
                deck.cardIds.Add(IdOf(card));
            deck.skillCardId = IdOf(skill);
            TutorialDeckOutput.SaveDeck(deck);

            AssetDatabase.Refresh();
            Report(unique, skill, deck);
        }

        // ======================================== 小工厂 ========================================

        private static string IdOf(CardData card) => "C_" + ContentHasher.HashCard(card);

        private static List<int> Kinds(params int[] kinds) => kinds.ToList();

        private static CardData Creature(string name, int power, int life, params string[] keywords)
        {
            var card = new CardData
            {
                CardName = name,
                Supertype = Cardtype.Creature,
                Power = power,
                Life = life,
                Tags = new List<string> { ThemeTag },
            };
            foreach (var kw in keywords) card.Keywords.Add(kw);
            return card;
        }

        private static CardData Enchantment(string name, int durability)
        {
            return new CardData
            {
                CardName = name,
                Supertype = Cardtype.Enchantment,
                Durability = durability,
                Tags = new List<string> { ThemeTag },
            };
        }

        private static CardData Spell(string name)
        {
            return new CardData
            {
                CardName = name,
                Supertype = Cardtype.Spell,
                SpeedBonus = 2,
                Tags = new List<string> { ThemeTag },
            };
        }

        /// <summary>倒计时载体：引擎主干 + Countdown(engine=1) + explicit 回合数 + Then 奖励。</summary>
        private static void CountdownFx(CardData card, int turns, AtomicEffectEntry reward, string name)
        {
            var atom = AtomRefs.New(AtomicEffectType.EngineCountdown);
            atom.branch = new BranchEntryData
            {
                settle = (int)BranchSettleKind.Engine,
                engine = (int)BranchEngineKind.Countdown,
                engineParam = Math.Max(1, turns),
                then = new List<AtomicEffectEntry> { reward },
            };
            card.Effects.Add(new CardEffectData
            {
                DisplayName = name,
                TriggerTiming = (int)TriggerTiming.OnStandbyPhase,
                ActivationType = 1, // 自动
                Duration = (int)DurationType.Once,
                SelectionMode = (int)SelectionMode.None,
                TargetCount = -2,
                TriggerLimitPerTurn = 1,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 0, atomic = atom },
                },
            });
        }

        /// <summary>诅咒/祝福：主动施放部署；Then 在抽到时自动结算（tactivate=1）。</summary>
        private static void OnDrawEngineFx(CardData card, BranchEngineKind kind, string keyword,
            AtomicEffectEntry reward, string name)
        {
            var atom = kind == BranchEngineKind.CurseOnDraw
                ? AtomRefs.New(AtomicEffectType.EngineCurseOnDraw)
                : AtomRefs.New(AtomicEffectType.EngineBlessingOnDraw);
            atom.branch = new BranchEntryData
            {
                settle = (int)BranchSettleKind.Engine,
                engine = (int)kind,
                engineParam = 2, // 随机 2 张
                tactivate = 1,   // 自动结算（不等玩家再点）
                then = new List<AtomicEffectEntry> { reward },
            };
            card.Effects.Add(new CardEffectData
            {
                DisplayName = name,
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                ActivationType = 2,
                Duration = (int)DurationType.Once,
                SelectionMode = (int)SelectionMode.None,
                TargetCount = -2,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 0, atomic = atom },
                },
            });
        }

        /// <summary>自我沉睡（豁免费时长模式）：Value=0 + 实例域 Self；苏醒时豁免多少灰/绿就睡多少层。</summary>
        private static CardEffectData SelfSleepFx()
        {
            var atom = AtomRefs.New(AtomicEffectType.Sleep, 0, Kinds(0));
            return new CardEffectData
            {
                DisplayName = "苏醒：灰、绿费全免，转为入场沉睡层数（睡完才继续倒计时）",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                ActivationType = 0,
                Duration = (int)DurationType.Once,
                SelectionMode = (int)SelectionMode.None,
                TargetCount = -2,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 0, atomic = atom },
                },
            };
        }

        private static CardEffectData ActiveNoTargetFx(string name, AtomicEffectEntry atom)
        {
            return new CardEffectData
            {
                DisplayName = name,
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                ActivationType = 2,
                Duration = (int)DurationType.Once,
                SelectionMode = (int)SelectionMode.None,
                TargetCount = -2,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 0, atomic = atom },
                },
            };
        }

        private static CardEffectData ActiveFx(string name, AtomicEffectEntry atom)
        {
            return new CardEffectData
            {
                DisplayName = name,
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                ActivationType = 2,
                Duration = (int)DurationType.Once,
                SelectionMode = (int)SelectionMode.Single,
                TargetCount = 1,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 0, atomic = atom },
                },
            };
        }

        // ======================================== 自检 ========================================

        private static void Report(List<CardData> unique, CardData skill, DeckData deck)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[绿·慢速] 倒计时/诅咒/祝福版构建完成（写入教学表 TutorialCards/TutorialEffects + TutorialData/绿·慢速.json）：");
            foreach (var card in unique)
            {
                var cost = card.Cost != null && !card.Cost.IsZero ? card.Cost.ToString() : "0";
                int copies = deck.cardIds.Count(id => id == IdOf(card));
                string body = card.Supertype == Cardtype.Creature
                    ? $"{card.Power}/{card.Life}"
                    : card.Supertype == Cardtype.Enchantment ? $"dur {card.Durability}" : "/";
                sb.AppendLine($"  {card.CardName} ×{copies} | {card.Supertype} {body} | 费 {cost} | id {IdOf(card)}");
            }
            sb.AppendLine($"  英雄技能：{skill.CardName} | id {IdOf(skill)} | 资格 {HeroSkillSystem.CanBeSkillCard(skill)}");
            sb.AppendLine($"  卡组：{deck.cardIds.Count} 张 | skillCardId {deck.skillCardId}");

            int countdown = 0, curse = 0, blessing = 0;
            foreach (var card in unique)
            {
                foreach (var def in CardEffectConverter.ConvertAll(card.Effects, card.ID))
                {
                    if (def?.Effects == null) continue;
                    foreach (var atom in def.Effects)
                    {
                        if (atom?.Branch == null) continue;
                        if (atom.Branch.EngineKind == BranchEngineKind.Countdown) countdown++;
                        else if (atom.Branch.EngineKind == BranchEngineKind.CurseOnDraw) curse++;
                        else if (atom.Branch.EngineKind == BranchEngineKind.BlessingOnDraw) blessing++;
                    }
                }
            }
            sb.AppendLine($"  自检：倒计时 {countdown} / 诅咒 {curse} / 祝福 {blessing}");

            var report = DeckCompositionValidator.Check(deck);
            sb.AppendLine("  构成自检：" + DeckCompositionValidator.Describe(report));
            foreach (var v in report.Violations)
                sb.AppendLine($"    ✘ 违色：{v.Detail} ×{v.Copies}");
            foreach (var id in report.MissingIds)
                sb.AppendLine($"    ✘ 断链：{id}");
            foreach (var err in report.ShapeErrors)
                sb.AppendLine($"    ✘ 形状：{err}");
            Debug.Log(sb.ToString());
        }
    }
}
