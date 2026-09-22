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
        /// <summary>蓝·洞察（蓝2）：抽一张牌；升级（7 次）：从自己牌库发现一张卡（随机 3 选 1）</summary>
        BlueInsight = 1,
        /// <summary>绿·培育（绿3）：从自己牌库随机将一张生物作为地牌横置入场；
        /// 升级（7 次）：从自己墓地选一张生物作为地牌横置入场</summary>
        GreenCultivate = 2,
        /// <summary>红·狂热（红1，2026-09-22 重做）：召唤一个 1/1 衍生物（可攻击）；
        /// 升级（7 次）：召唤一个 2/1 衍生物</summary>
        RedFrenzy = 3,
    }

    /// <summary>
    /// 英雄技能系统（2026-09-13 第二十批定案；2026-09-21 改造：技能=初始在场永续魔法；
    /// 2026-09-22 重做：对称设计退役——AI 优先用技能时"双方各…"喂养对手资源显得愚蠢，改为单方收益）：
    /// - **技能卡实体**：Enchantment（结界/永续魔法）超类，开局由 InitGame 生成放入 FieldZone
    ///   （原额外卡组退役后的空缺槽位）；**发动=横置本卡**（一回合一次=准备阶段重置）；
    ///   **交互与一般永续魔法一致**——可被沉默（不可发动主动效果）/无效/摧毁/弹回（离场即失技能）；
    /// - **激活需付对应颜色费用**（蓝2/绿3/红1）；**7 次发动后升级**（TotalUses ≥ 7 → 升级版）；
    /// - 单方收益（2026-09-22）：效果只利己，不再"双方各…"；
    /// - 黑白暂无技能（None）；InitGame 按卡组费用主色自动指派。
    /// 接线：GameActions.ActivateHeroSkill（声明口）；回合开始 GameCore 重置技能卡横置。
    /// </summary>
    public static class HeroSkillSystem
    {
        /// <summary>升级阈值（发动次数）。</summary>
        public const int UpgradeThreshold = 7;

        /// <summary>技能费用（id → (色, 量)）。</summary>
        public static (ManaType color, int amount) CostOf(HeroSkillId id) => id switch
        {
            HeroSkillId.BlueInsight => (ManaType.Blue, 2),
            HeroSkillId.GreenCultivate => (ManaType.Green, 3),
            HeroSkillId.RedFrenzy => (ManaType.Red, 1),
            _ => (ManaType.Gray, 0),
        };

        // ======================================== 技能卡实体（2026-09-21） ========================================

        /// <summary>解析技能卡实体：优先运行时引用；丢失时（快照/存档恢复、旧档）按 FieldZone
        /// 现场 lazy 回填（技能卡 ID 稳定 = HEROSKILL_+枚举名）。</summary>
        public static Card ResolveSkillCard(GameCore core, Player player)
        {
            if (player == null) return null;
            if (player.HeroSkillCard != null) return player.HeroSkillCard;
            if (core == null) return null;
            string wantId = "HEROSKILL_" + (HeroSkillId)player.HeroSkill;
            player.HeroSkillCard = core.ZoneManager.GetCards(player, Zone.FieldZone)
                .FirstOrDefault(c => c is CardWrapper w && w.GetData()?.ID == wantId);
            return player.HeroSkillCard;
        }

        /// <summary>生成技能卡（Enchantment 永续魔法实体：在场可交互；效果执行仍走本系统，
        /// 卡面不挂原子——效果定义与交互语义解耦）。</summary>
        public static CardWrapper CreateSkillCard(HeroSkillId skill)
        {
            var (color, amount) = CostOf(skill);
            var data = new CardData
            {
                ID = "HEROSKILL_" + skill,
                CardName = SkillName(skill),
                Supertype = Cardtype.Enchantment,
                Cost = new Dictionary<int, float> { { (int)color, amount } },
            };
            return new CardWrapper(data);
        }

        /// <summary>指派技能并落场：清 FieldZone 旧技能卡 → 建卡入 FieldZone → 写 player 字段。
        /// None = 清场不落卡。重复调用幂等（换技能/重开局安全）。</summary>
        public static void AssignSkill(GameCore core, Player player, HeroSkillId skill)
        {
            if (core == null || player == null) return;
            player.HeroSkill = (int)skill;
            player.HeroSkillCard = null;
            var container = core.ZoneManager.GetZoneContainer(player);
            var oldSkills = core.ZoneManager.GetCards(player, Zone.FieldZone)
                .Where(c => c is CardWrapper w && w.GetData()?.ID?.StartsWith("HEROSKILL_") == true)
                .ToList();
            foreach (var old in oldSkills)
                container.Remove(old, Zone.FieldZone);

            if (skill == HeroSkillId.None) return;
            var card = CreateSkillCard(skill);
            card.SetController(player);
            card.SetOwner(player);
            container.Add(card, Zone.FieldZone);
            player.HeroSkillCard = card;
        }

        /// <summary>按卡组指派（InitGame 用）：①主题标签优先（Theme* 标签多数派——费用色在跨色
        /// 卡组不可靠：绿组碾压/清场的红费可能比治疗绿费还多）；②无标签回落费用主色（灰不参选）。</summary>
        public static HeroSkillId AutoSkillForDeck(List<Card> deck)
        {
            int red = 0, green = 0, blue = 0;
            var total = new Dictionary<int, float>();
            if (deck != null)
                foreach (var card in deck)
                {
                    if (!(card is CardWrapper wrapper)) continue;
                    var data = wrapper.GetData();
                    if (data?.Tags != null)
                        foreach (var t in data.Tags)
                        {
                            if (t == "ThemeRed") red++;
                            else if (t == "ThemeGreen") green++;
                            else if (t == "ThemeBlue") blue++;
                        }
                    foreach (var kv in data?.Cost ?? new Dictionary<int, float>())
                        total[kv.Key] = total.GetValueOrDefault(kv.Key) + kv.Value;
                }
            if (red > green && red > blue) return HeroSkillId.RedFrenzy;
            if (green > red && green > blue) return HeroSkillId.GreenCultivate;
            if (blue > red && blue > green) return HeroSkillId.BlueInsight;

            int best = -1; float bestV = 0;
            foreach (var kv in total)
            {
                if ((ManaType)kv.Key == ManaType.Gray) continue; // 灰=垫付色不参选
                if (kv.Value > bestV) { bestV = kv.Value; best = kv.Key; }
            }
            switch ((ManaType)best)
            {
                case ManaType.Red: return HeroSkillId.RedFrenzy;
                case ManaType.Blue: return HeroSkillId.BlueInsight;
                case ManaType.Green: return HeroSkillId.GreenCultivate;
                default: return HeroSkillId.None;
            }
        }

        public static string Describe(HeroSkillId id, bool upgraded) => id switch
        {
            HeroSkillId.BlueInsight => upgraded
                ? "洞察·发现（蓝2）：从自己牌库随机展示 3 张选 1 入手"
                : "洞察（蓝2）：抽一张牌",
            HeroSkillId.GreenCultivate => upgraded
                ? "培育·再生（绿3）：从自己墓地选一张生物作为地牌横置入场"
                : "培育（绿3）：从自己牌库随机将一张生物作为地牌横置入场",
            HeroSkillId.RedFrenzy => upgraded
                ? "狂热·壮大（红1）：召唤一个 2/1 衍生物"
                : "狂热（红1）：召唤一个 1/1 衍生物（可攻击）",
            _ => "无技能",
        };

        // ======================================== 发动 ========================================

        /// <summary>
        /// 发动英雄技能（主阶段声明口，2026-09-21 永续魔法化）。守卫：回合玩家 + 主阶段 +
        /// **技能卡在场（FieldZone）且未横置且未被沉默**（交互与永续魔法一致：被摧毁/弹回=无技能、
        /// 沉默=不可发动主动效果、横置=本回合已用）+ 付色费。成功 → 横置技能卡 → 计数 +1（≥7 升级）
        /// → 执行当前版本效果（异步：选择交互）。
        /// </summary>
        public static async UniTask<bool> ActivateAsync(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            var skill = (HeroSkillId)player.HeroSkill;
            if (skill == HeroSkillId.None) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            // 技能卡实体守卫：在场 + 未横置 + 未沉默
            var skillCard = ResolveSkillCard(core, player); // 含快照恢复后的 lazy 回填
            if (skillCard == null) return false; // 未指派（无技能卡）
            if (!core.ZoneManager.GetCards(player, Zone.FieldZone).Contains(skillCard))
                return false; // 已被摧毁/弹回/移场——技能随卡离场失效
            if (skillCard.IsTapped()) return false; // 本回合已发动（准备阶段重置）
            if (skillCard.GetCounterCount(Attribute.CounterRules.SilenceCounter) > 0)
                return false; // 沉默：不可发动主动效果（永续魔法交互一致）

            var (color, amount) = CostOf(skill);
            var bill = new Dictionary<int, float> { { (int)color, amount } };
            if (!core.ElementPool.CanPayCost(bill, player)) return false;
            if (!core.ElementPool.PayCost(bill, player, "英雄技能·" + SkillName(skill))) return false;

            skillCard.Tap(); // 发动横置（一回合一次的实体闸门）
            player.HeroSkillUsesThisTurn = 1; // 兼容口径回写（旧消费者）
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
            switch (skill)
            {
                case HeroSkillId.BlueInsight:
                    if (upgraded)
                    {
                        await DiscoverFromDeckAsync(core, player, 3);
                    }
                    else
                    {
                        // 单方收益（2026-09-22）：只自己抽，不再"双方各抽 1"
                        ZoneManagerExtensions.DrawCard(core.ZoneManager, player);
                    }
                    break;

                case HeroSkillId.GreenCultivate:
                    if (upgraded)
                    {
                        await GraveyardCreatureToLandTappedAsync(core, player);
                    }
                    else
                    {
                        RandomDeckCreatureToLandTapped(core, player);
                    }
                    break;

                case HeroSkillId.RedFrenzy:
                    SummonFrenzyToken(core, player, upgraded ? 2 : 1);
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

        /// <summary>绿基础（2026-09-22 重做）：从自己牌库随机将一张生物作为地牌横置入场。
        /// 随机序逐张尝试——0 费/纯黑白生物（入池无指示物）被池校验自然拒绝后换下一张，不白花钱。</summary>
        private static void RandomDeckCreatureToLandTapped(GameCore core, Player player)
        {
            var candidates = core.ZoneManager.GetCards(player, Zone.Deck)
                .Where(c => ElementPoolSystem.CanServeAsLand(c))
                .OrderBy(_ => GameRng.Next(0, int.MaxValue))
                .ToList();
            foreach (var card in candidates)
            {
                if (LandTappedFromZone(core, player, card, Zone.Deck)) return;
            }
        }

        /// <summary>绿升级（2026-09-22 重做）：从自己墓地选一张生物作为地牌横置入场。</summary>
        private static async UniTask GraveyardCreatureToLandTappedAsync(GameCore core, Player player)
        {
            var grave = core.ZoneManager.GetCards(player, Zone.Graveyard)?
                .Where(c => c.IsAlive
                            && ElementPoolSystem.CanServeAsLand(c)
                            && core.ElementPool.CanPoolProduceTokens(c))
                .ToList() ?? new List<Card>();
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
                    Title = "英雄技能·培育·再生（选一张生物作为地牌横置入场）",
                });
                if (picked != null && picked.Count > 0 && picked[0] is Card pc) chosen = pc;
            }

            LandTappedFromZone(core, player, chosen, Zone.Graveyard);
        }

        /// <summary>绿技能共用：卡作为地牌横置入池——入池（资格/上限/指示物权威校验）→ 移区 →
        /// 横置（本回合不可产元素，己方回合开始恢复直立）。</summary>
        private static bool LandTappedFromZone(GameCore core, Player player, Card card, Zone fromZone)
        {
            if (!core.ElementPool.AddCardToPool(card, player)) return false;
            core.ZoneManager.MoveCard(card, player, fromZone, Zone.ElementPool);

            var pooled = core.ElementPool.GetPool(player).PooledCards
                .FirstOrDefault(pc => pc.SourceCard == card);
            if (pooled != null) pooled.IsTapped = true; // 横置入场：本回合不能立即横置产元素

            core.PublishEvent(new KeywordAppliedEvent
            {
                Target = card,
                Keyword = "培育",
                Detail = "生物作为地牌横置入场（按费用构成产指示物）",
                Source = player,
            });
            return true;
        }

        /// <summary>红技能 token 模板 ID（实例 ID = 模板#序号，对齐 SummonTokenHandler 口径）。</summary>
        private const string FrenzyTokenTemplateId = "HEROSKILL_TOKEN_RED";

        /// <summary>红技能（2026-09-22 重做）：召唤一个可攻击的衍生物（基础 1/1，升级 2/1）。
        /// 与 SummonTokenHandler 同口径：全参数工厂 + 对局临时实例 ID + TokenSpawned 进场；
        /// 普通 Creature 模板默认可宣言攻击（NoAttack 未设）。满场由 TryAddToBattlefield 统一入墓。</summary>
        private static void SummonFrenzyToken(GameCore core, Player player, int power)
        {
            var token = new CardWrapper(new CardData
            {
                ID = FrenzyTokenTemplateId,
                CardName = "狂热魔仆",
                Supertype = Cardtype.Creature,
                Power = power,
                Life = 1,
            })
            {
                ID = $"{FrenzyTokenTemplateId}#{CardCore.TimestampSystem.NextSequence}",
            };
            token.SetController(player);
            core.ZoneManager.TryAddToBattlefield(token, player, EnterSource.TokenSpawned);
            core.PublishEvent(new TokenCreatedEvent
            {
                TokenTemplateId = FrenzyTokenTemplateId,
                Controller = player,
                Source = player,
                Tapped = token.IsTapped(),
                Card = token,
                DropZone = Zone.Battlefield,
            });
        }

        /// <summary>技能中文名（战报支付来源标注/技能发动行渲染用，2026-09-21）。</summary>
        public static string SkillName(HeroSkillId skill)
        {
            switch (skill)
            {
                case HeroSkillId.RedFrenzy: return "红·狂热";
                case HeroSkillId.BlueInsight: return "蓝·洞察";
                case HeroSkillId.GreenCultivate: return "绿·培育";
                default: return skill.ToString();
            }
        }
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
