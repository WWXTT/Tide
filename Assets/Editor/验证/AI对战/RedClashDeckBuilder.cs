using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 红·快攻（弃牌拼点版）卡组构建器（2026-10-22 用户定案；同日迁教学表）：
    /// · **输出=教学表**（TutorialCards/TutorialEffects + TutorialData/红·快攻.json）：玩家侧
    ///   卡组构筑/联机列表看不到也改不到；运行时经 DeckCatalog 当「·预组」只读可选；
    /// · 经济模型：**黑色代价（弃1张手牌=3黑）→ 补偿黑元素付身材灰费；红色付关键词费**；
    ///   弃牌把手中打空 → 手牌为0（HandEmpty，奖励2=抽1）成为主抽牌引擎；
    /// · 战斗：红关键词（先攻/缴械/碾压/连击）反制对手守卫；英雄技能=激励（重置）→ 多次攻脸；
    /// · 主题：拼点（自由分支引擎）抽牌；**登场直伤=焚城先锋**（2026-10-22 替换「命运一抽」：
    ///   唯一门在大量效果抽牌下几乎吃不到，改成登场对对方单位 2 伤——沿用原「造成伤害」原子，
    ///   角色是合法目标，想打脸就选角色）；生物为主保拼点取样；
    /// · 每卡 ≤3 效果槽（实际 1 效果 + 1 代价槽）。
    /// </summary>
    public static class RedClashDeckBuilder
    {
        /// <summary>卡组名（预组名——CardDataPaths.PresetDeckNames / DeckCatalog 只读识别）。</summary>
        public const string DeckName = "红·快攻";

        /// <summary>主题标签（与另两套 ThemeGreenCountdown / ThemeBlueSurplus 同族；AiStrategy.AutoMatch
        /// 按多数派标签定策略——改名会连带改 AiStrategy 的匹配表）。</summary>
        private const string ThemeTag = "ThemeRedClash";

        [MenuItem("Tools/验证/重建红·快攻（弃牌拼点版·写入教学表）")]
        public static void Rebuild()
        {
            var unique = new List<CardData>();
            var deckCards = new List<CardData>();

            void Add(CardData card, int copies)
            {
                unique.Add(card);
                for (int i = 0; i < copies; i++) deckCards.Add(card);
            }

            // ================= 前期铺场（无代价，1-2 费抢节奏） =================
            Add(Creature("赤烬猎犬", 2, 1), 3);
            Add(Creature("熔岩突袭兵", 3, 1), 3);
            Add(Creature("燃爪斥候", 3, 2), 1);

            // ================= 核心生物（红关键词 + 黑色代价：弃1张手牌） =================
            // 登场直伤（2026-10-22 替换「命运一抽·赤命」）：原唯一门（准备阶段自然抽到）在
            // 「弃牌→手牌0→抽1」与拼点抽牌的重抽牌引擎下几乎吃不到，等于 5 费白板；改为登场
            // 对对方单位造成 2 伤（沿用原「造成伤害」原子 a4b823fc——角色是有生命单位，结算时
            // 可在对方生物/角色间选一，想打脸就点角色），稳定压血且不新增原子。
            var c1 = Creature("焚城先锋", 3, 1);
            c1.Effects.Add(Fx("登场时，对对方单位造成2点伤害", TriggerTiming.OnPlay,
                AtomRefs.New(AtomicEffectType.DealDamage, 2, Kinds(2))));
            DiscardCost(c1);
            Add(c1, 1);

            // 3 费起手：空手门（手牌为0=奖励2抽1）
            var c2 = Creature("空手掠袭者", 3, 1);
            KeywordFx(c2, AtomicEffectType.GrantDisarm, BranchKind.HandEmpty,
                "登场时，获得缴械；若手牌为0，抽1张卡");
            DiscardCost(c2);
            Add(c2, 2);

            // 4 费中坚：先攻/缴械/拼点三线
            // 印刷关键词（不走效果槽）：红费直接买关键词，身材灰费由黑色代价支付
            var c3 = Creature("血怒先锋", 3, 1);
            c3.Keywords.Add("FirstStrike");
            DiscardCost(c3);
            Add(c3, 3);

            var c4 = Creature("断刃武士", 4, 1);
            KeywordFx(c4, AtomicEffectType.GrantDisarm, BranchKind.Clash,
                "登场时，获得缴械；自由分支·拼点2→抽1张卡");
            DiscardCost(c4);
            Add(c4, 3);

            var c5 = Creature("拼点·烬刃武者", 3, 1);
            KeywordFx(c5, AtomicEffectType.GrantFirstStrike, BranchKind.Clash,
                "登场时，获得先攻；自由分支·拼点2→抽1张卡");
            DiscardCost(c5);
            Add(c5, 2);

            // 击杀抽牌（产出条件·奖励2）：拆守卫/补刀
            var c6 = Creature("斩首猎手", 4, 1);
            var killAtom = Damage(2, Kinds(2));
            KillBranch(killAtom);
            c6.Effects.Add(Fx("登场时，对对方单位造成2点伤害；若消灭目标，抽1张卡",
                TriggerTiming.OnPlay, killAtom));
            DiscardCost(c6);
            Add(c6, 1);

            // 5 费终结：连击/碾压，拼点与空手各一条线
            var c7 = Creature("双刃屠夫", 4, 1);
            KeywordFx(c7, AtomicEffectType.GrantDoubleStrike, BranchKind.HandEmpty,
                "登场时，获得连击；若手牌为0，抽1张卡");
            DiscardCost(c7);
            Add(c7, 2);

            var c8 = Creature("拼点·焚面鬼", 5, 1);
            KeywordFx(c8, AtomicEffectType.GrantOverwhelm, BranchKind.Clash,
                "登场时，获得碾压；自由分支·拼点2→抽1张卡");
            DiscardCost(c8);
            Add(c8, 2);

            // ================= 辅助法术（同样带黑色代价：弃1张手牌） =================
            // ① 带代价加速：1 红换一次额外攻击（激励），弃1张手牌付代价
            var s1 = Spell("血税突袭");
            var s1atom = Untap();
            ApplyBranch(s1atom, BranchKind.HandEmpty);
            s1.Effects.Add(Fx("重置一个己方单位；若手牌为0，抽1张卡；代价：弃1张手牌",
                TriggerTiming.Activate_Active, s1atom));
            DiscardCost(s1);
            Add(s1, 2);

            // ② 增强别的卡：临时 +1 攻（灰、便宜）与永久 +1/+1（红、昂贵）对照
            var s2 = Spell("战嚎");
            s2.Effects.Add(Fx("己方全体单位+1攻；代价：弃1张手牌", TriggerTiming.Activate_Active,
                Buff(1), whole: true));
            DiscardCost(s2);
            Add(s2, 1);

            var s3 = Spell("永久磨砺");
            var s3atom = PermanentBuff(1);
            ApplyBranch(s3atom, BranchKind.HandEmpty);
            s3.Effects.Add(Fx("一个己方单位永久+1/+1；若手牌为0，抽1张卡；代价：弃1张手牌",
                TriggerTiming.Activate_Active, s3atom));
            DiscardCost(s3);
            Add(s3, 1);

            // 直伤/解场：红费，弃牌代价维持手牌为0的引擎
            var s4 = Spell("烈焰箭");
            var s4atom = Damage(2, Kinds(2));
            ApplyBranch(s4atom, BranchKind.HandEmpty);
            s4.Effects.Add(Fx("对对方单位造成2点伤害；若手牌为0，抽1张卡；代价：弃1张手牌",
                TriggerTiming.Activate_Active, s4atom));
            DiscardCost(s4);
            Add(s4, 2);

            var s5 = Spell("斩首令");
            var s5atom = Damage(3, Kinds(2));
            KillBranch(s5atom);
            s5.Effects.Add(Fx("对对方单位造成3点伤害；若消灭目标，抽1张卡；代价：弃1张手牌",
                TriggerTiming.Activate_Active, s5atom));
            DiscardCost(s5);
            Add(s5, 1);

            // ================= 英雄技能（不占卡组张数）：激励 =================
            var skill = Spell("激励");
            skill.Effects.Add(Fx("重置一个己方单位", TriggerTiming.Activate_Active, Untap()));

            // ================= 推导费 → 落教学表 =================
            foreach (var card in unique.Concat(new[] { skill }))
                CardCostService.EnsureCost(card);

            TutorialDeckOutput.RemoveThemeCards(ThemeTag); // 主表 + 教学表双侧清红套旧档（幂等）
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

            TutorialDeckOutput.SyncTestDeck(deck); // 测试开局夹具（TutorialData/测试卡组.json）跟随新卡池，防断链
            AssetDatabase.Refresh();
            Report(unique, skill, deck);
        }

        private enum BranchKind { HandEmpty, Clash, Kill }

        // ======================================== 卡/效果小工厂 ========================================

        private static string IdOf(CardData card) => "C_" + ContentHasher.HashCard(card);

        private static List<int> Kinds(params int[] kinds) => kinds.ToList();

        private static CardData Creature(string name, int power, int life)
        {
            return new CardData
            {
                CardName = name,
                Supertype = Cardtype.Creature,
                Power = power + 1,
                Life = life + 1,
                NoGuard = true,
                Tags = new List<string> { ThemeTag },
            };
        }

        private static CardData Spell(string name)
        {
            return new CardData
            {
                CardName = name,
                Supertype = Cardtype.Spell,
                SpeedBonus = 2, // 1 槽法术：免费点全投提速（速 2）
                Tags = new List<string> { ThemeTag },
            };
        }

        private static AtomicEffectEntry Damage(int value, List<int> kinds)
            => AtomRefs.New(AtomicEffectType.DealDamage, value, kinds);

        private static AtomicEffectEntry Buff(int layers)
            => AtomRefs.New(AtomicEffectType.AddPowerUp, layers, Kinds(1));

        private static AtomicEffectEntry PermanentBuff(int layers)
            => AtomRefs.New(AtomicEffectType.AddPermanentPlusOne, layers, Kinds(1));

        private static AtomicEffectEntry Untap()
            => AtomRefs.New(AtomicEffectType.Untap, 1, Kinds(1));

        /// <summary>黑色代价：强制弃 1 张自己的手牌（DiscardCard 1 = 3 黑；代价全价 3 ≤ 地牌上限 3 起可用）。</summary>
        private static void DiscardCost(CardData card)
        {
            card.PayloadCost = new CostEntry
            {
                CostType = (int)CostType.Payload,
                Value = 1,
                payloads = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.DiscardCard, 1, Kinds(5)),
                },
            };
        }

        /// <summary>红关键词效果：登场授予 + 槽级分支奖励（抽1）。</summary>
        private static void KeywordFx(CardData card, AtomicEffectType grant, BranchKind branch,
            string name)
        {
            var atom = AtomRefs.New(grant, 1);
            ApplyBranch(atom, branch);
            card.Effects.Add(Fx(name, TriggerTiming.OnPlay, atom));
        }

        private static void ApplyBranch(AtomicEffectEntry atom, BranchKind branch)
        {
            switch (branch)
            {
                case BranchKind.HandEmpty:
                    atom.branch = new BranchEntryData
                    {
                        settle = (int)BranchSettleKind.Gate,
                        gateId = "HandEmpty",
                        then = DrawOne(),
                    };
                    break;
                case BranchKind.Clash:
                    atom.branch = new BranchEntryData
                    {
                        settle = (int)BranchSettleKind.Engine,
                        engine = (int)BranchEngineKind.Clash,
                        engineParam = 0,
                        then = DrawOne(),
                    };
                    break;
                case BranchKind.Kill:
                    KillBranch(atom);
                    break;
            }
        }

        private static void KillBranch(AtomicEffectEntry atom)
        {
            atom.branch = new BranchEntryData
            {
                settle = (int)BranchSettleKind.Outcome,
                outcomeId = "DmgKillsTarget",
                then = DrawOne(),
            };
        }

        private static List<AtomicEffectEntry> DrawOne()
            => new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, 1) };

        private static CardEffectData Fx(string name, TriggerTiming timing,
            AtomicEffectEntry atom, bool whole = false)
        {
            return new CardEffectData
            {
                DisplayName = name,
                TriggerTiming = (int)timing,
                ActivationType = timing == TriggerTiming.Activate_Active ? 2 : 0,
                Duration = (int)DurationType.Once,
                SelectionMode = whole ? (int)SelectionMode.Whole : (int)SelectionMode.Single,
                TargetCount = whole ? 0 : 1,
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
            sb.AppendLine("[红·快攻] 弃牌拼点版重建完成（写入教学表 TutorialCards/TutorialEffects + TutorialData/红·快攻.json）：");
            foreach (var card in unique)
            {
                var cost = card.Cost != null && !card.Cost.IsZero ? card.Cost.ToString() : "0";
                int copies = deck.cardIds.Count(id => id == IdOf(card));
                string payload = card.PayloadCost != null ? " +弃1" : "";
                sb.AppendLine($"  {card.CardName} ×{copies} | {card.Supertype} {card.Power}/{card.Life}" +
                              $" | 费 {cost}{payload} | id {IdOf(card)}");
            }
            sb.AppendLine($"  英雄技能：{skill.CardName} | id {IdOf(skill)}" +
                          $" | 资格 {HeroSkillSystem.CanBeSkillCard(skill)}");
            sb.AppendLine($"  卡组：{deck.cardIds.Count} 张 | skillCardId {deck.skillCardId}");

            int clash = 0, gate = 0, outcome = 0, payloads = 0;
            foreach (var card in unique)
            {
                if (card.PayloadCost != null) payloads++;
                foreach (var def in CardEffectConverter.ConvertAll(card.Effects, card.ID))
                {
                    if (def?.Effects == null) continue;
                    foreach (var atom in def.Effects)
                    {
                        if (atom?.Branch == null) continue;
                        switch (atom.Branch.Settle)
                        {
                            case BranchSettleKind.Engine: clash++; break;
                            case BranchSettleKind.Gate: gate++; break;
                            case BranchSettleKind.Outcome: outcome++; break;
                        }
                    }
                }
            }
            sb.AppendLine($"  分支自检：拼点 {clash} / 有限门 {gate} / 产出条件 {outcome}；黑色代价 {payloads} 张");

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
