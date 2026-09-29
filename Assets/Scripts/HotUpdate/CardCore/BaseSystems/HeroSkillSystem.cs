using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>英雄技能 id（2026-09-13 第二十批：额外卡组退役，技能栏上位；
    /// 2026-09-22 定案：升级=抉择式条件分支——门=「此前已发动 ≥7 次」，门前后各一档效果；
    /// 同日升级调整：两档同色同轴、费用差（≈2 费奖励标定）决定升级难度）。</summary>
    public enum HeroSkillId : int
    {
        None = 0,
        /// <summary>蓝·洞察（蓝2·资源轴）：门前=抽一张牌；门后（第 8 次起）=发现一张（随机 3 选 1）并抽一张（+2 费）</summary>
        BlueInsight = 1,
        /// <summary>绿·培育（绿3·资源轴）：门前=牌库随机生物横置入地牌；
        /// 门后（第 8 次起）=墓地选生物横置入地牌 + 回收墓地一张卡（+2 费）</summary>
        GreenCultivate = 2,
        /// <summary>红·狂热（红1·场面轴）：门前=召唤 1/1 衍生物（横置入场并激励，当回合可攻击）；
        /// 门后（第 8 次起）=召唤 2/1 衍生物（同激励）——升级效果暂定，随机红色关键词方案已弃（2026-09-22）</summary>
        RedFrenzy = 3,
    }

    /// <summary>
    /// 英雄技能系统（2026-09-13 第二十批定案；2026-09-21 改造：技能=初始在场永续魔法；
    /// 2026-09-22 重做：对称设计退役——单方收益；同日定案：**升级=抉择式条件分支**）：
    /// - **技能卡实体**：Enchantment（结界/永续魔法）超类，开局由 InitGame 生成放入 FieldZone
    ///   （原额外卡组退役后的空缺槽位）；**发动=横置本卡**（一回合一次=准备阶段重置）；
    ///   **交互与一般永续魔法一致**——可被沉默（不可发动主动效果）/无效/摧毁/弹回（离场即失技能）；
    /// - **激活需付对应颜色费用**（蓝2/绿3/红1）；
    /// - **升级=抉择式条件分支（2026-09-22 定案）**：技能效果为同一发动流程下的**两档分支**
    ///   （结构类似抉择卡的双模式，但分支不走玩家选择而走条件门）——门=「**此技能此前已发动
    ///   ≥7 次**」：条件满足前（第 1..7 次发动）执行基础档，满足后（第 8 次起）执行升级档
    ///   （第 7 次当次仍走基础档）；费用不变、单向。发动计数挂在技能卡（SkillUse 指示物），
    ///   换技能卡=新卡计数归零；
    /// - 单方收益（2026-09-22）：效果只利己，不再"双方各…"；
    /// - 黑白暂无技能（None）；InitGame 按卡组费用主色自动指派。
    /// 接线：GameActions.ActivateHeroSkill（声明口）；回合开始 GameCore 重置技能卡横置。
    /// </summary>
    public static class HeroSkillSystem
    {
        /// <summary>升级门槛（发动次数）：第 8 次发动起走升级档。</summary>
        public const int UpgradeThreshold = 7;

        // ======================================== 技能定义表（2026-09-22：升级=抉择式条件分支） ========================================

        /// <summary>技能效果分支：类似抉择卡的一个模式——同一发动流程下两档只执行其一。</summary>
        public sealed class HeroSkillBranch
        {
            /// <summary>档名（如「洞察」「洞察·发现」）。</summary>
            public string Title;
            /// <summary>效果描述（描述正文）。</summary>
            public string Body;
            /// <summary>效果实件（含选择交互）。</summary>
            public Func<GameCore, Player, UniTask> Execute;

            public HeroSkillBranch(string title, string body, Func<GameCore, Player, UniTask> execute)
            { Title = title; Body = body; Execute = execute; }
        }

        /// <summary>技能定义：费用 + 升级门阈值 + 两档效果（Base=门未满足 / Upgraded=门已满足）。
        /// 分支选择不走玩家选择（区别于抉择卡），而走条件门「此技能此前已发动 ≥ 阈值次」；
        /// 费用不变、单向（升级后不回退）。
        /// **升级设计约束（2026-09-22 定案）**：两档效果限定**相同颜色、相同轴向**
        /// （蓝=资源/绿=资源/红=场面），**费用差决定升级难度**——当前标定：升级奖励 ≈2 费差 → 阈值 7 次发动。</summary>
        public sealed class HeroSkillDefinition
        {
            public HeroSkillId Id;
            public ManaType CostColor;
            public int CostAmount;
            public int UpgradeThreshold = HeroSkillSystem.UpgradeThreshold;
            /// <summary>条件未满足（此前发动 &lt; 阈值，即第 1..7 次）执行的分支。</summary>
            public HeroSkillBranch Base;
            /// <summary>条件已满足（此前发动 ≥ 阈值，即第 8 次起）执行的分支。</summary>
            public HeroSkillBranch Upgraded;
        }

        private static readonly Dictionary<HeroSkillId, HeroSkillDefinition> _definitions = new()
        {
            [HeroSkillId.BlueInsight] = new HeroSkillDefinition
            {
                Id = HeroSkillId.BlueInsight, CostColor = ManaType.Blue, CostAmount = 2,
                Base = new HeroSkillBranch("洞察", "抽一张牌", (core, p) =>
                {
                    ZoneManagerExtensions.DrawCard(core.ZoneManager, p); // 单方收益（2026-09-22）：只自己抽
                    return UniTask.CompletedTask;
                }),
                // 升级调整（2026-09-22）：发现与抽 1 同为 2 费，单纯升级"没有体现"——升级档=发现+抽 1
                //（约 2+2=4 费，较基础 +2 费 = 升级奖励标定值）
                Upgraded = new HeroSkillBranch("洞察·发现", "从自己牌库发现一张（随机 3 选 1 入手）并抽一张",
                    async (core, p) =>
                    {
                        await DiscoverFromDeckAsync(core, p, 3);
                        ZoneManagerExtensions.DrawCard(core.ZoneManager, p);
                    }),
            },
            [HeroSkillId.GreenCultivate] = new HeroSkillDefinition
            {
                Id = HeroSkillId.GreenCultivate, CostColor = ManaType.Green, CostAmount = 3,
                Base = new HeroSkillBranch("培育", "从自己牌库随机将一张生物作为地牌横置入场", (core, p) =>
                {
                    RandomDeckCreatureToLandTapped(core, p);
                    return UniTask.CompletedTask;
                }),
                // 升级调整（2026-09-22）：再生=墓地精确选（较牌库随机的溢价）+ 回收墓地一张卡（1 费锚）——约 +2 费
                Upgraded = new HeroSkillBranch("培育·再生", "从自己墓地选一张生物作为地牌横置入场，并回收墓地一张卡",
                    async (core, p) =>
                    {
                        await GraveyardCreatureToLandTappedAsync(core, p);
                        await RecycleFromGraveyardAsync(core, p, 1);
                    }),
            },
            [HeroSkillId.RedFrenzy] = new HeroSkillDefinition
            {
                Id = HeroSkillId.RedFrenzy, CostColor = ManaType.Red, CostAmount = 1,
                Base = new HeroSkillBranch("狂热", "召唤一个 1/1 衍生物（横置入场并激励，当回合可攻击）", (core, p) =>
                {
                    SummonFrenzyToken(core, p, 1);
                    return UniTask.CompletedTask;
                }),
                // 升级档暂定 2/1（同激励）——原「随机获得一个 1-3 费红色元素关键词」方案因一个效果
                // 含双随机需改框架而放弃（2026-09-22），升级效果待另定
                Upgraded = new HeroSkillBranch("狂热·壮大", "召唤一个 2/1 衍生物（横置入场并激励，当回合可攻击）",
                    (core, p) =>
                    {
                        SummonFrenzyToken(core, p, 2);
                        return UniTask.CompletedTask;
                    }),
            },
        };

        /// <summary>技能定义（None/未定义 → null）。</summary>
        public static HeroSkillDefinition DefinitionOf(HeroSkillId skill)
            => _definitions.TryGetValue(skill, out var def) ? def : null;

        /// <summary>技能费用（id → (色, 量)）；单一来源=定义表。</summary>
        public static (ManaType color, int amount) CostOf(HeroSkillId id)
            => DefinitionOf(id) is { } def ? (def.CostColor, def.CostAmount) : (ManaType.Gray, 0);

        /// <summary>此技能累计发动次数（读技能卡的 SkillUse 指示物；无技能卡=0）。</summary>
        public static int GetTotalUses(GameCore core, Player player)
            => ResolveSkillCard(core, player)?.GetCounterCount(CounterRules.SkillUseCounter) ?? 0;

        /// <summary>升级门状态（局面口径）：已发动 ≥ 阈值——第 7 次发动即置位（当次仍走基础档）。
        /// AI/播报等"是否已升级"读取口。</summary>
        public static bool IsUpgraded(GameCore core, Player player)
            => GetTotalUses(core, player) >= UpgradeThreshold;

        /// <summary>分支解析（抉择式）：门=「此前已发动 ≥ 阈值次」→ 升级档；否则基础档。
        /// priorUses=本次发动**前**的累计次数（发动计数 +1 前抓拍）。</summary>
        public static HeroSkillBranch ResolveBranch(HeroSkillDefinition def, int priorUses)
            => priorUses >= def.UpgradeThreshold ? def.Upgraded : def.Base;

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

        /// <summary>技能描述（分支驱动，2026-09-22）：档名（色费）：效果正文。</summary>
        public static string Describe(HeroSkillId id, bool upgraded)
        {
            var def = DefinitionOf(id);
            if (def == null) return "无技能";
            var branch = upgraded ? def.Upgraded : def.Base;
            return $"{branch.Title}（{ColorChar(def.CostColor)}{def.CostAmount}）：{branch.Body}";
        }

        private static string ColorChar(ManaType color) => color switch
        {
            ManaType.Red => "红",
            ManaType.Blue => "蓝",
            ManaType.Green => "绿",
            ManaType.Black => "黑",
            ManaType.White => "白",
            _ => "灰",
        };

        // ======================================== 发动 ========================================

        /// <summary>
        /// 发动英雄技能（主阶段声明口，2026-09-21 永续魔法化；2026-09-22 升级=抉择式条件分支）。
        /// 守卫：回合玩家 + 主阶段 + **技能卡在场（FieldZone）且未横置且未被沉默**
        /// （交互与永续魔法一致：被摧毁/弹回=无技能、沉默=不可发动主动效果、横置=本回合已用）+ 付色费。
        /// 成功 → 横置技能卡 → 发动计数 +1（挂技能卡）→ **升级门评估**（门=「此前已发动 ≥7 次」，
        /// 计数 +1 前抓拍）→ 执行命中分支（基础档/升级档，异步：选择交互）。
        /// </summary>
        public static async UniTask<bool> ActivateAsync(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            var skill = (HeroSkillId)player.HeroSkill;
            var def = DefinitionOf(skill);
            if (def == null) return false; // None/未定义——无技能
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            // 技能卡实体守卫：在场 + 未横置 + 未沉默
            var skillCard = ResolveSkillCard(core, player); // 含快照恢复后的 lazy 回填
            if (skillCard == null) return false; // 未指派（无技能卡）
            if (!core.ZoneManager.GetCards(player, Zone.FieldZone).Contains(skillCard))
                return false; // 已被摧毁/弹回/移场——技能随卡离场失效
            if (skillCard.IsTapped()) return false; // 本回合已发动（准备阶段重置）
            if (skillCard.GetCounterCount(CounterRules.SilenceCounter) > 0)
                return false; // 沉默：不可发动主动效果（永续魔法交互一致）

            var bill = new Dictionary<int, float> { { (int)def.CostColor, def.CostAmount } };
            if (!core.ElementPool.CanPayCost(bill, player)) return false;
            if (!core.ElementPool.PayCost(bill, player, "英雄技能·" + SkillName(skill))) return false;

            skillCard.Tap(); // 发动横置（一回合一次的实体闸门）
            // 发动计数 +1（随技能卡）；门在计数前抓拍——本次走哪档由「此前已发动次数」判
            int priorUses = skillCard.GetCounterCount(CounterRules.SkillUseCounter);
            skillCard.AddCounters(CounterRules.SkillUseCounter, 1);

            // 升级门（抉择式条件分支，2026-09-22 定案）：条件满足前（第 1..7 次）走基础档、
            // 满足后（第 8 次起）走升级档——第 7 次发动当次仍走基础档（单向，费用不变）
            bool upgraded = priorUses >= def.UpgradeThreshold;
            core.PublishEvent(new HeroSkillActivatedEvent
            {
                Player = player,
                Skill = skill,
                Upgraded = upgraded,
                TotalUses = priorUses + 1,
            });

            var branch = ResolveBranch(def, priorUses);
            await branch.Execute(core, player);
            return true;
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

        /// <summary>红技能（2026-09-22 重做；同日升级调整）：召唤一个衍生物（基础 1/1，升级 2/1），
        /// **横置入场并激励**——入场统一横置（TryAddToBattlefield 定案）后立即解除（冲锋语义，
        /// 当回合即可宣言攻击；口径=UntapHandler：Untap+UntapEvent）。
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
            if (core.ZoneManager.TryAddToBattlefield(token, player, EnterSource.TokenSpawned))
            {
                token.Untap(); // 激励自己：横置入场后立即解除（当回合可攻击）
                core.PublishEvent(new UntapEvent { UntappedEntity = token });
            }
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

        /// <summary>回收（2026-09-22 升级调整）：从自己墓地选 count 张卡回手（回收 1 张=1 费锚）。
        /// 与 RecoverToHandHandler 同口径（墓→手）；候选不足 count 张时全收（无交互），
        /// 超出时弹选择（无头/超时自动取前 N）。</summary>
        private static async UniTask RecycleFromGraveyardAsync(GameCore core, Player player, int count)
        {
            var grave = core.ZoneManager.GetCards(player, Zone.Graveyard);
            if (grave == null || grave.Count == 0 || count <= 0) return;

            List<Card> picked;
            if (grave.Count <= count)
            {
                picked = new List<Card>(grave);
            }
            else
            {
                var chosen = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = grave.Cast<Entity>().ToList(),
                    MinCount = count,
                    MaxCount = count,
                    Chooser = player,
                    Title = "英雄技能·培育·再生（回收：选一张卡回手）",
                });
                picked = (chosen?.OfType<Card>() ?? Enumerable.Empty<Card>()).Take(count).ToList();
                if (picked.Count == 0) picked = grave.Take(count).ToList(); // 无头兜底：前 N 张
            }

            foreach (var card in picked)
                core.ZoneManager.MoveCard(card, player, Zone.Graveyard, Zone.Hand);
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
