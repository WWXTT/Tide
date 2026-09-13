using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>英雄技能 id（2026-09-13 第二十批：额外卡组退役，技能栏上位）。</summary>
    public enum HeroSkillId : int
    {
        None = 0,
        /// <summary>蓝·洞察（蓝1）：双方各抽 1 张卡；升级（7 次）：从自己牌库发现一张卡（随机 3 选 1）</summary>
        BlueInsight = 1,
        /// <summary>绿·培育（绿2）：双方各选一个己方生物赋予地牌特性（横置→得 1 元素，色随生物费用）；
        /// 升级（7 次）：从自己墓地选一张卡置入地牌区</summary>
        GreenCultivate = 2,
        /// <summary>红·狂热（红3）：双方各选一个己方生物攻击力+2（本回合）；升级（7 次）：
        /// 选一个己方生物以其当前攻击力对对手角色造成等量伤害</summary>
        RedFrenzy = 3,
    }

    /// <summary>
    /// 英雄技能系统（2026-09-13 第二十批定案）：
    /// - **一回合一次**（回合玩家+主阶段+本回合未用）；**激活需付对应颜色费用**（蓝1/绿2/红3）；
    /// - **7 次发动后升级**（TotalUses ≥ 7 → 升级版，费用不变）；
    /// - 对称设计（双方各…）：激活者付费、双方受益——优势来自不对称利用；
    /// - 黑白暂无技能（None）。
    /// 接线：GameActions.ActivateHeroSkill（声明口）；回合开始 GameCore 清 UsesThisTurn。
    /// </summary>
    public static class HeroSkillSystem
    {
        /// <summary>升级阈值（发动次数）。</summary>
        public const int UpgradeThreshold = 7;

        /// <summary>技能费用（id → (色, 量)）。</summary>
        public static (ManaType color, int amount) CostOf(HeroSkillId id) => id switch
        {
            HeroSkillId.BlueInsight => (ManaType.Blue, 1),
            HeroSkillId.GreenCultivate => (ManaType.Green, 2),
            HeroSkillId.RedFrenzy => (ManaType.Red, 3),
            _ => (ManaType.Gray, 0),
        };

        public static string Describe(HeroSkillId id, bool upgraded) => id switch
        {
            HeroSkillId.BlueInsight => upgraded
                ? "洞察·发现（蓝1）：从自己牌库随机展示 3 张选 1 入手"
                : "洞察（蓝1）：双方各抽 1 张卡",
            HeroSkillId.GreenCultivate => upgraded
                ? "培育·再生（绿2）：从自己墓地选一张卡置入地牌区"
                : "培育（绿2）：双方各选一个己方生物赋予地牌特性（横置得 1 元素）",
            HeroSkillId.RedFrenzy => upgraded
                ? "狂热·燃尽（红3）：选一个己方生物以其攻击力对对手角色造成等量伤害"
                : "狂热（红3）：双方各选一个己方生物攻击力 +2（本回合）",
            _ => "无技能",
        };

        // ======================================== 发动 ========================================

        /// <summary>
        /// 发动英雄技能（主阶段声明口）。守卫：回合玩家 + 主阶段 + 本回合未用 + 付色费。
        /// 成功 → 计数 +1（≥7 升级）→ 执行当前版本效果（异步：选择交互）。
        /// </summary>
        public static async UniTask<bool> ActivateAsync(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            var skill = (HeroSkillId)player.HeroSkill;
            if (skill == HeroSkillId.None) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;
            if (player.HeroSkillUsesThisTurn > 0) return false;

            var (color, amount) = CostOf(skill);
            var bill = new Dictionary<int, float> { { (int)color, amount } };
            if (!core.ElementPool.CanPayCost(bill, player)) return false;
            if (!core.ElementPool.PayCost(bill, player)) return false;

            player.HeroSkillUsesThisTurn = 1;
            player.HeroSkillTotalUses++;
            if (!player.HeroSkillUpgraded && player.HeroSkillTotalUses >= UpgradeThreshold)
                player.HeroSkillUpgraded = true; // 本次发动仍用基础版，下一次起用升级版

            core.PublishEvent(new HeroSkillActivatedEvent
            {
                Player = player,
                Skill = skill,
                Upgraded = player.HeroSkillUpgraded,
                TotalUses = player.HeroSkillTotalUses,
            });

            await ExecuteAsync(core, player, skill, player.HeroSkillUpgraded && player.HeroSkillTotalUses > UpgradeThreshold);
            // 注：升级当次（TotalUses==7 且刚置位）仍执行基础版；≥8 起执行升级版
            return true;
        }

        private static async UniTask ExecuteAsync(GameCore core, Player player, HeroSkillId skill, bool upgraded)
        {
            var opponent = player.Opponent;
            switch (skill)
            {
                case HeroSkillId.BlueInsight:
                    if (upgraded)
                    {
                        await DiscoverFromDeckAsync(core, player, 3);
                    }
                    else
                    {
                        ZoneManagerExtensions.DrawCard(core.ZoneManager, player);
                        if (opponent != null) ZoneManagerExtensions.DrawCard(core.ZoneManager, opponent);
                    }
                    break;

                case HeroSkillId.GreenCultivate:
                    if (upgraded)
                    {
                        await GraveyardToLandAsync(core, player);
                    }
                    else
                    {
                        await GrantLandTraitAsync(core, player);
                        if (opponent != null) await GrantLandTraitAsync(core, opponent);
                    }
                    break;

                case HeroSkillId.RedFrenzy:
                    if (upgraded)
                    {
                        await PowerDamageAsync(core, player);
                    }
                    else
                    {
                        await BuffOwnCreatureAsync(core, player, 2);
                        if (opponent != null) await BuffOwnCreatureAsync(core, opponent, 2);
                    }
                    break;
            }
        }

        // ======================================== 效果实件 ========================================

        /// <summary>发现：自己牌库随机展示 N 张选 1 入手（DiscoverCard 同款逻辑，未选留原位）。</summary>
        private static async UniTask DiscoverFromDeckAsync(GameCore core, Player player, int showCount)
        {
            var deck = core.ZoneManager.GetCards(player, Zone.Deck);
            if (deck == null || deck.Count == 0) return;

            var pool = new List<Card>(deck);
            var shown = new List<Card>();
            while (shown.Count < showCount && pool.Count > 0)
            {
                int i = GameRng.Next(0, pool.Count);
                shown.Add(pool[i]);
                pool.RemoveAt(i);
            }
            if (shown.Count == 0) return;

            Card chosen = shown[0];
            if (shown.Count > 1)
            {
                var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = shown.Cast<Entity>().ToList(),
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = player,
                    Title = "英雄技能·发现（选 1 入手）",
                });
                if (picked != null && picked.Count > 0 && picked[0] is Card pc) chosen = pc;
            }
            core.ZoneManager.MoveCard(chosen, player, Zone.Deck, Zone.Hand);
            core.PublishEvent(new RevealCardsEvent { Player = player, Cards = new List<Card> { chosen }, Source = player });
        }

        /// <summary>绿基础：玩家自选一个己方生物，赋予地牌特性（LandTrait 关键词）。</summary>
        private static async UniTask GrantLandTraitAsync(GameCore core, Player player)
        {
            var creatures = OwnCreatures(core, player);
            if (creatures.Count == 0) return;

            Card chosen = creatures[0];
            if (creatures.Count > 1)
            {
                var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = creatures.Cast<Entity>().ToList(),
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = player,
                    Title = "英雄技能·培育（选择获得地牌特性的生物）",
                });
                if (picked != null && picked.Count > 0 && picked[0] is Card pc) chosen = pc;
            }

            chosen.AddKeyword(Attribute.KeywordRules.LandTrait, KeywordLane.Setting, player);
            core.PublishEvent(new KeywordAppliedEvent
            {
                Target = chosen,
                Keyword = Attribute.KeywordRules.LandTrait,
                Detail = "培育：获得地牌特性（横置→得 1 元素，色随费用构成）",
                Source = player,
            });
        }

        /// <summary>绿升级：从自己墓地选一张卡置入地牌区（复用地牌资格校验）。</summary>
        private static async UniTask GraveyardToLandAsync(GameCore core, Player player)
        {
            var grave = core.ZoneManager.GetCards(player, Zone.Graveyard)?
                .Where(c => c.IsAlive).ToList() ?? new List<Card>();
            if (grave.Count == 0) return;

            Card chosen = grave[0];
            if (grave.Count > 1)
            {
                var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = grave.Cast<Entity>().ToList(),
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = player,
                    Title = "英雄技能·培育·再生（选一张卡置入地牌区）",
                });
                if (picked != null && picked.Count > 0 && picked[0] is Card pc) chosen = pc;
            }

            if (!core.ElementPool.AddCardToPool(chosen, player)) return;
            core.ZoneManager.MoveCard(chosen, player, Zone.Graveyard, Zone.ElementPool);
            core.PublishEvent(new KeywordAppliedEvent
            {
                Target = chosen,
                Keyword = "培育·再生",
                Detail = "墓地卡置入地牌区（按费用构成产指示物）",
                Source = player,
            });
        }

        /// <summary>红基础：玩家自选一个己方生物，攻击力 +N（本回合，Setting 轨——技能来源=角色）。</summary>
        private static async UniTask BuffOwnCreatureAsync(GameCore core, Player player, int bonus)
        {
            var creatures = OwnCreatures(core, player);
            if (creatures.Count == 0) return;

            Card chosen = creatures[0];
            if (creatures.Count > 1)
            {
                var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = creatures.Cast<Entity>().ToList(),
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = player,
                    Title = $"英雄技能·狂热（选择攻击力+{bonus} 的生物）",
                });
                if (picked != null && picked.Count > 0 && picked[0] is Card pc) chosen = pc;
            }

            // 2026-09-13 用户修订：+2 持续两回合（ForTurns(2)≡UntilNextTurn——活过对手回合，防御+进攻双窗口）。
            // 直接走指示物+时钟（AddStatCounter turns:2）——不经 StatGrantRouter（Player 来源会被判设置轨=永久直改）
            Attribute.CounterRules.AddStatCounter(chosen, Attribute.CounterRules.PowerUpCounter, bonus, player, turns: 2);
            core.PublishEvent(new KeywordAppliedEvent
            {
                Target = chosen,
                Keyword = "狂热",
                Detail = $"攻击力 +{bonus}（持续 2 回合）",
                Source = player,
            });
        }

        /// <summary>红升级：选一个己方生物，以其当前攻击力（LayerEngine 实时值）对对手角色造成等量伤害。</summary>
        private static async UniTask PowerDamageAsync(GameCore core, Player player)
        {
            var creatures = OwnCreatures(core, player);
            if (creatures.Count == 0 || player.Opponent == null) return;

            Card chosen = creatures[0];
            if (creatures.Count > 1)
            {
                var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = creatures.Cast<Entity>().ToList(),
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = player,
                    Title = "英雄技能·狂热·燃尽（选择以其攻击力直伤的生物）",
                });
                if (picked != null && picked.Count > 0 && picked[0] is Card pc) chosen = pc;
            }

            int power = core.LayerEngine?.CalculatePower(chosen) ?? chosen.GetPower();
            if (power <= 0) return;
            Attribute.KeywordRules.ApplyDamage(player, player.Opponent, power, false);
        }

        private static List<Card> OwnCreatures(GameCore core, Player player)
            => core.ZoneManager.GetCards(player, Zone.Battlefield)
                .Where(c => c.IsAlive && c is IHasSupertype st && st.Supertype == Cardtype.Creature)
                .ToList();
    }

    /// <summary>英雄技能发动事件（播报/观察用）。</summary>
    public class HeroSkillActivatedEvent : GameEventBase
    {
        public Player Player;
        public HeroSkillId Skill;
        public bool Upgraded;
        public int TotalUses;
    }
}
