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
    /// 蓝·控制（元素充盈版）卡组构建器（2026-10-22 定案；同日迁教学表）：
    /// · **输出=教学表**（TutorialCards/TutorialEffects + TutorialData/蓝·控制.json）：玩家侧
    ///   卡组构筑/联机列表看不到也改不到；运行时经 DeckCatalog 当「·预组」只读可选；
    /// · 核心：元素充盈（ManaSurplus，自由分支引擎）载体=生物或结界；2026-10-22 全局规则
    ///   「同一张卡每回合只触发一次自由分支」由 BranchEngines 统一收口，本套卡数据同写 limit=1；
    /// · 打断：冻结（Freeze）与弹回（ReturnToHand）法术均为 1 效果槽 → 法术速度 2，
    ///   可在对手回合响应攻击/慢速效果时施放；
    /// · 英雄技能：隐匿（赋予潜行，不占卡组张数）；卡组只允许蓝+灰（主题纯度）。
    /// </summary>
    public static class BlueSurplusDeckBuilder
    {
        public const string DeckName = "蓝·控制";
        private const string ThemeTag = "ThemeBlueSurplus";

        [MenuItem("Tools/验证/构建蓝·控制（充盈版·写入教学表）")]
        public static void Build()
        {
            // 英雄技能换装：旧「灵视」卡从主表/教学表双侧移除（2026-10-22 用户定案：蓝技能=赋予潜行）
            TutorialDeckOutput.RemoveCardByName("灵视");
            TutorialDeckOutput.RemoveThemeCards(ThemeTag); // 幂等：清上一轮蓝套（主表残留一并清）
            EffectsLibrary.Reload();
            CardCatalog.Invalidate();

            var unique = new List<CardData>();
            var deckCards = new List<CardData>();

            void Add(CardData card, int copies)
            {
                unique.Add(card);
                for (int i = 0; i < copies; i++) deckCards.Add(card);
            }

            // ================= 充盈载体（生物 7 / 结界 2） =================
            var e1 = Creature("潮汐观测者", 1, 3);
            SurplusFx(e1, Damage(1, Kinds(2)),
                "对手回合对手使用卡牌结算后，若己方剩余元素比对手多1，对对方单位造成1点伤害");
            Add(e1, 4);

            var e2 = Creature("静水贤者", 2, 2);
            SurplusFx(e2, DrawOne(),
                "对手回合对手使用卡牌结算后，若己方剩余元素比对手多2，抽1张卡");
            Add(e2, 3);

            var e3 = Enchantment("霜涌结界", 2);
            SurplusFx(e3, Damage(2, Kinds(2)),
                "对手回合对手使用卡牌结算后，若己方剩余元素比对手多2，对对方单位造成2点伤害");
            Add(e3, 1);

            var e4 = Enchantment("秘藏结界", 2);
            SurplusFx(e4, FreezeOne(),
                "对手回合对手使用卡牌结算后，若己方剩余元素比对手多1，冻结一个对方单位");
            Add(e4, 1);

            // ================= 冻结 / 弹回：对手回合的打断 =================
            var f1 = Spell("冰封");
            f1.Effects.Add(ActiveFx("冻结一个对方单位", FreezeOne(), 1));
            Add(f1, 3);

            var f2 = Spell("霜环");
            f2.Effects.Add(ActiveFx("冻结两个对方单位", FreezeOne(), 2));
            Add(f2, 2);

            var b1 = Spell("退潮");
            b1.Effects.Add(ActiveFx("将一个对方单位弹回手牌", BounceOne(), 1));
            Add(b1, 3);

            // 抉择（2 个主动效果 → 法术速度 1）：控制/伤害二选一，对手回合可响应攻击
            var ch = Spell("霜蚀抉择");
            ch.SpeedBonus = 1; // 3 − 2 槽
            ch.Effects.Add(ActiveFx("冻结一个对方单位", FreezeOne(), 1));
            ch.Effects.Add(ActiveFx("使一个对方单位失去3点生命",
                AtomRefs.New(AtomicEffectType.AddLifeDown, 3, Kinds(2)), 1));
            Add(ch, 2);

            // ================= 抽牌 / 防守 / 终结 =================
            var d1 = Spell("预知");
            d1.Effects.Add(ActiveFx("抽1张卡", DrawOne(), 0));
            Add(d1, 3);

            Add(Creature("冰墙", 0, 5), 2);
            Add(Creature("霜甲守卫", 2, 4), 3);
            Add(Creature("深海巨兽", 4, 5), 3);

            // ================= 英雄技能：隐匿（赋予一个己方单位潜行） =================
            var skill = Spell("隐匿");
            skill.Effects.Add(ActiveFx("赋予一个己方单位1层潜行",
                AtomRefs.New(AtomicEffectType.GrantStealth, 1, Kinds(1)), 1));

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

        private static CardData Creature(string name, int power, int life)
        {
            return new CardData
            {
                CardName = name,
                Supertype = Cardtype.Creature,
                Power = power,
                Life = life,
                Tags = new List<string> { ThemeTag },
            };
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
                SpeedBonus = 2, // 1 槽法术：速 2，可响应攻击
                Tags = new List<string> { ThemeTag },
            };
        }

        private static AtomicEffectEntry Damage(int value, List<int> kinds)
            => AtomRefs.New(AtomicEffectType.DealDamage, value, kinds);

        private static AtomicEffectEntry FreezeOne()
            => AtomRefs.New(AtomicEffectType.Freeze, 1, Kinds(2));

        private static AtomicEffectEntry BounceOne()
            => AtomRefs.New(AtomicEffectType.ReturnToHand, 1, Kinds(2));

        private static AtomicEffectEntry DrawOne()
            => AtomRefs.New(AtomicEffectType.DrawCard, 1);

        /// <summary>充盈载体效果：引擎主干 + ManaSurplus(engine=5) + Then 奖励；每卡每回合 1 次。</summary>
        private static void SurplusFx(CardData card, AtomicEffectEntry reward, string name)
        {
            var atom = AtomRefs.New(AtomicEffectType.EngineManaSurplus);
            atom.branch = new BranchEntryData
            {
                settle = (int)BranchSettleKind.Engine,
                engine = (int)BranchEngineKind.ManaSurplus,
                engineParam = 0,
                then = reward != null
                    ? new List<AtomicEffectEntry> { reward }
                    : new List<AtomicEffectEntry>(),
            };
            card.Effects.Add(new CardEffectData
            {
                DisplayName = name,
                TriggerTiming = (int)TriggerTiming.OnPlay,
                ActivationType = 0,
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

        /// <summary>主动法术效果：targetCount=0 无目标（抽牌/自结算），1 单目标，2 双目标。</summary>
        private static CardEffectData ActiveFx(string name, AtomicEffectEntry atom, int targetCount)
        {
            return new CardEffectData
            {
                DisplayName = name,
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                ActivationType = 2,
                Duration = (int)DurationType.Once,
                SelectionMode = targetCount == 0
                    ? (int)SelectionMode.None
                    : targetCount == 1 ? (int)SelectionMode.Single : (int)SelectionMode.Multiple,
                TargetCount = targetCount == 0 ? -2 : targetCount,
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
            sb.AppendLine("[蓝·控制] 充盈版构建完成（写入教学表 TutorialCards/TutorialEffects + TutorialData/蓝·控制.json）：");
            foreach (var card in unique)
            {
                var cost = card.Cost != null && !card.Cost.IsZero ? card.Cost.ToString() : "0";
                int copies = deck.cardIds.Count(id => id == IdOf(card));
                string kind = card.Supertype.ToString();
                string body = card.Supertype == Cardtype.Creature
                    ? $"{card.Power}/{card.Life}"
                    : card.Supertype == Cardtype.Enchantment ? $"dur {card.Durability}" : "/";
                sb.AppendLine($"  {card.CardName} ×{copies} | {kind} {body} | 费 {cost} | id {IdOf(card)}");
            }
            sb.AppendLine($"  英雄技能：{skill.CardName} | id {IdOf(skill)}" +
                          $" | 资格 {HeroSkillSystem.CanBeSkillCard(skill)}");
            sb.AppendLine($"  卡组：{deck.cardIds.Count} 张 | skillCardId {deck.skillCardId}");

            int surplus = 0, freeze = 0, bounce = 0;
            foreach (var card in unique)
            {
                foreach (var def in CardEffectConverter.ConvertAll(card.Effects, card.ID))
                {
                    if (def?.Effects == null) continue;
                    foreach (var atom in def.Effects)
                    {
                        if (atom?.Branch?.EngineKind == BranchEngineKind.ManaSurplus) surplus++;
                        if (atom?.Type == AtomicEffectType.Freeze) freeze++;
                        if (atom?.Type == AtomicEffectType.ReturnToHand) bounce++;
                    }
                }
            }
            sb.AppendLine($"  自检：充盈载体 {surplus} / 冻结原子 {freeze} / 弹回原子 {bounce}");

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
