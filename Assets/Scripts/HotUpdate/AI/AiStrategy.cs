using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI
{
    /// <summary>
    /// AI 策略（策略模式，2026-09-21）：SimpleAI 的决策偏好按卡组主题分化。
    /// 基类 AiStrategy = 通用决策（解场优先有害目标 / 己方优先有益目标 / 正收益解场优先攻击），
    /// 三主题策略只覆写分化点：
    /// · 红快攻（RedAggroStrategy）——铺场优先、激励/加攻指向攻击力最高己方生物；正收益解场照做，
    ///   不赚的换子改打脸（快攻的时间成本最低）；
    /// · 绿慢速（GreenRampStrategy）——回血/攒费优先、大生物终结、治疗指向受伤最重己方；只做正收益
    ///   解场，不赚就留着守卫（横置=丧失反击/拦截资格）；
    /// · 蓝控制（BlueControlStrategy）——指示物/解场优先、抽牌续资源；不赚的换子「看情况」
    ///   （威胁压制时接受，见 AcceptBadTrade），其余打脸磨血。
    /// 解场估值（2026-11-01 用户定案）：单位价值 =（攻击力 + 生命值）/ 2，一次主动攻击按 1 费计——
    /// 换子收益 = 击杀价值 − 阵亡损失 − 1；收益 &gt; 0 时任何策略都优先解场。
    /// 所有钩子只做"选择偏好"，不绕过引擎——合法性/费用/守卫拦截仍由引擎权威校验。
    /// </summary>
    public abstract class AiStrategy
    {
        public abstract string Key { get; }
        public abstract string DisplayName { get; }

        // ======================================== 工厂与选项 ========================================

        public static readonly AiStrategy General = new GeneralAiStrategy();
        public static readonly AiStrategy RedAggro = new RedAggroStrategy();
        public static readonly AiStrategy GreenRamp = new GreenRampStrategy();
        public static readonly AiStrategy BlueControl = new BlueControlStrategy();

        /// <summary>窗口/菜单可选项（key 稳定存档用；label 仅展示）。</summary>
        public static readonly (string key, string label)[] Options =
        {
            ("auto", "自动匹配卡组"), ("general", "通用"), ("red", "红快攻"), ("green", "绿慢速"), ("blue", "蓝控制"),
        };

        public static AiStrategy Create(string key)
        {
            switch (key)
            {
                case "red": return RedAggro;
                case "green": return GreenRamp;
                case "blue": return BlueControl;
                default: return General;
            }
        }

        /// <summary>按主色自动匹配（回落口径）。
        /// 灰=垫付通用色不参选（主题卡组费用大头常是灰——按全色最大匹配会大量误落通用，
        /// 2026-09-21 修复）；红/蓝/绿/黑/白中取份额最大者，全灰→通用。</summary>
        public static AiStrategy ByMainColor(Dictionary<int, float> cost)
        {
            if (cost == null || cost.Count == 0) return General;
            int best = -1; float bestV = 0;
            foreach (var kv in cost)
            {
                if ((ManaType)kv.Key == ManaType.Gray) continue; // 灰不参选
                if (kv.Value > bestV) { bestV = kv.Value; best = kv.Key; }
            }
            if (best < 0) return General;
            switch ((ManaType)best)
            {
                case ManaType.Red: return RedAggro;
                case ManaType.Green: return GreenRamp;
                case ManaType.Blue: return BlueControl;
                default: return General;
            }
        }

        /// <summary>"自动匹配卡组"主口径（2026-09-21）：①主题标签优先——卡组多数派 Theme* 标签
        /// 直接定策略（费用色在跨色卡组不可靠：绿组碾压/清场的红费比治疗绿费还多）；
        /// ②无标签回落费用主色（灰不参选，ByMainColor）。</summary>
        public static AiStrategy AutoMatch(List<CardData> deck)
        {
            if (deck != null && deck.Count > 0)
            {
                int red = 0, green = 0, blue = 0;
                foreach (var c in deck)
                {
                    if (c?.Tags == null) continue;
                    foreach (var t in c.Tags)
                    {
                        if (t == "ThemeRedClash") red++;
                        else if (t == "ThemeGreenCountdown") green++;
                        else if (t == "ThemeBlueSurplus") blue++;
                    }
                }
                if (red > green && red > blue) return RedAggro;
                if (green > red && green > blue) return GreenRamp;
                if (blue > red && blue > green) return BlueControl;
            }
            var total = new Dictionary<int, float>();
            if (deck != null)
                foreach (var c in deck)
                {
                    if (c.Cost == null) continue;
                    foreach (var color in c.Cost.NonzeroColors())
                        total[(int)color] = total.GetValueOrDefault((int)color) + c.Cost[color];
                }
            return ByMainColor(total);
        }

        // ======================================== 钩子（默认=原 SimpleAI 行为） ========================================

        /// <summary>
        /// 地牌投放优先级（2026-11-01）：只在「打不出的卡」里排序——高者先放进元素池当地。
        /// 默认=总费用降序（原 PlayLands 口径：高费生物先当地）。
        /// </summary>
        public virtual float LandPriority(GameCore core, Player me, Card card, Func<Card, float> totalCost)
            => totalCost(card);

        /// <summary>
        /// 出牌优先级（分数高先出）。默认=有害原子优先 + 大费先出（原 PlayBestAffordableCard 排序）。
        /// containsHarmfulAtom / totalCost 由 SimpleAI 注入（与其内部口径一致）。
        /// </summary>
        public virtual float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
            => (containsHarmfulAtom(card) ? 1000f : 0f) + totalCost(card);

        /// <summary>抉择模式打分（ChooseMode 用）：默认=有害优先 + 大费优先。</summary>
        public virtual float ModeScore(bool harmful, float totalCost)
            => (harmful ? 1000f : 0f) + totalCost;

        /// <summary>
        /// 攒牌闸（2026-10-31 红快攻斩杀线定案）：返回 true = 这张手牌本回合**刻意留着不打**（SimpleAI
        /// 出牌循环跳过；回合结束自然留手）。默认 false=能打就打（原行为不变）。
        /// 只影响"主动出牌"顺序面，不绕过任何规则校验——留着只是不打，仍可被打出（后续轮次闸放行）。
        /// </summary>
        public virtual bool ShouldHold(GameCore core, Player me, Card card) => false;

        /// <summary>
        /// 有益类原子的目标排序（选一/选多档择优）。默认=己方单位 &gt; 己方玩家（原 ChooseTargets）。
        /// </summary>
        public virtual IEnumerable<Entity> OrderBeneficialTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
            => candidates
                .OrderByDescending(c => c is Card cd && cd.GetController() == me)
                .ThenByDescending(c => ReferenceEquals(c, me));

        /// <summary>
        /// 有害类原子的目标排序。默认=对方存活单位（LayerEngine 实时威胁降序——解场优先 2026-09-13 裁决）
        /// &gt; 对方玩家 &gt; 己方垫底（原 ChooseTargets）。
        /// </summary>
        public virtual IEnumerable<Entity> OrderHarmfulTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
        {
            var opp = me.Opponent;
            return candidates
                .OrderByDescending(c => c is Card cd && cd.GetController() == opp && cd.IsAlive)
                .ThenByDescending(c => c is Card cd2 && cd2.GetController() == opp
                    ? core.LayerEngine.CalculatePower(cd2) : int.MinValue)
                .ThenByDescending(c => ReferenceEquals(c, opp))
                .ThenBy(c => c is Card cd3 && cd3.GetController() == me);
        }

        /// <summary>
        /// AI 目标候选排序（2026-11-01，SimpleAI 注入 TargetSelectionService.AiOrder）：
        /// 仅 AI/无头自动选择路径生效——默认原序。req.PromptAtom 标注本次选择语义
        ///（代价=代价原子类型、效果=首原子类型），策略据此决定"谁先被选中"
        ///（解横置选最高攻、代价选低攻/已横置、加攻集中同一生物……）。返回 null/空=不改动。
        /// </summary>
        public virtual List<Entity> OrderTargetCandidates(GameCore core, Player me, TargetSelectionRequest req)
            => req?.Candidates;

        // ======================================== 解场评估（2026-11-01 用户定案） ========================================

        /// <summary>一次主动攻击的机会成本（费）：横置与节奏损失，用户定案按 1 费计。</summary>
        protected const float AttackTempoCost = 1f;

        /// <summary>单位估值（费）：（攻击力 + 生命值）/ 2——9-1 值 5、2-2 值 2、1-1 值 1。</summary>
        protected static float UnitValue(GameCore core, Card unit)
            => (core.LayerEngine.CalculatePower(unit) + unit.GetLife()) / 2f;

        /// <summary>单次攻击的收益预估（解场评估结果）。</summary>
        protected struct TradeEvaluation
        {
            /// <summary>被攻击的敌方单位。</summary>
            public Card Target;
            /// <summary>本次攻击能否击杀目标（名义伤害；圣盾/护甲/坚韧未计入）。</summary>
            public bool Kills;
            /// <summary>自己是否会在反击中阵亡。</summary>
            public bool Dies;
            /// <summary>净收益（费）= 击杀价值 − 阵亡损失 − 1 费攻击成本。</summary>
            public float Gain;
            /// <summary>目标威胁（实时攻击力）——同收益时的次级排序键。</summary>
            public int Threat;
        }

        /// <summary>
        /// 预估 unit 攻击 enemy 的收益（近似模型：只建模横置/警戒/缴械/先攻/连击/剧毒）。
        /// 定案示例：1-1 攻 9-1 → 击杀 5 − 阵亡 1 − 1 = +3；2-2 攻 2-2（对方不反击）→ 2 − 0 − 1 = +1；
        /// 对方反击 → 2 − 2 − 1 = −1（不赚，亏一次主动攻击）。
        /// </summary>
        protected TradeEvaluation EvaluateTrade(GameCore core, Card unit, Card enemy)
        {
            int myPower = core.LayerEngine.CalculatePower(unit);
            int myLife = unit.GetLife();
            int enemyPower = core.LayerEngine.CalculatePower(enemy);
            int enemyLife = enemy.GetLife();

            // 反击资格（镜像 CombatSystem.ResolvePair）：横置者只能挨打，警戒例外；缴械压制目标反击。
            bool canCounter = (!enemy.IsTapped() || enemy.HasKeyword(KeywordRules.Vigilance))
                              && !unit.HasKeyword(KeywordRules.Disarm);
            bool myFirst = unit.HasKeyword(KeywordRules.FirstStrike) || unit.HasKeyword(KeywordRules.DoubleStrike);
            bool enemyFirst = enemy.HasKeyword(KeywordRules.FirstStrike) || enemy.HasKeyword(KeywordRules.DoubleStrike);
            int myDamage = myPower * (unit.HasKeyword(KeywordRules.DoubleStrike) ? 2 : 1);
            int counterDamage = enemyPower * (enemy.HasKeyword(KeywordRules.DoubleStrike) ? 2 : 1);

            // 击杀：名义伤害够，或剧毒（任意战斗伤害即消灭）。
            bool venomKills = myPower > 0 && unit.HasKeyword(KeywordRules.Venom);
            bool firstHitKills = myPower >= enemyLife || venomKills; // 先攻首段即击杀
            bool kills = myDamage >= enemyLife || venomKills;
            // 敌方先攻且首段就打死我：我的伤害落不下 → 击杀不成立。
            if (canCounter && enemyFirst && !myFirst && enemyPower >= myLife) kills = false;
            // 阵亡：要挨反击且反击总量够；我先攻且对方无先攻时首段先杀 → 对方来不及反击。
            bool dies = canCounter && counterDamage >= myLife
                        && !(myFirst && !enemyFirst && firstHitKills);

            float gain = (kills ? UnitValue(core, enemy) : 0f)
                         - (dies ? UnitValue(core, unit) : 0f)
                         - AttackTempoCost;
            return new TradeEvaluation
            {
                Target = enemy, Kills = kills, Dies = dies, Gain = gain, Threat = enemyPower
            };
        }

        /// <summary>扫描可攻击的敌方生物，取收益最高的换子（只考虑能一击击杀的目标；
        /// 同收益取威胁更高者）。结界等无生命单位不参与换子估值。</summary>
        protected bool TryFindBestTrade(GameCore core, Player me, Card unit, Player opp,
            out TradeEvaluation best)
        {
            best = default;
            bool found = false;
            var oppField = core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>();
            foreach (var enemy in oppField)
            {
                if (!enemy.IsAlive || !enemy.IsLivingUnit()) continue;
                if (!core.CombatSystem.CanAttackTarget(unit, enemy, me)) continue;
                var trade = EvaluateTrade(core, unit, enemy);
                if (!trade.Kills) continue;
                if (!found || trade.Gain > best.Gain
                    || (trade.Gain == best.Gain && trade.Threat > best.Threat))
                {
                    best = trade;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>非正收益换子的取舍（策略分化点）：返回 true=仍执行。默认 false——不白送。</summary>
        protected virtual bool AcceptBadTrade(GameCore core, Player me, Card unit, TradeEvaluation trade)
            => false;

        /// <summary>
        /// 攻击目标决策（null=该单位本回合不攻击）。通用解场模型：
        /// ① 致死斩杀（能赢先赢，三套同口径）；② 正收益换子无条件做——"最优解无论如何都是去解场"；
        /// ③ 非正收益：策略分化（默认打脸；打不了脸就本回合不出手，不白送）。
        /// </summary>
        public virtual Entity PickAttackTarget(GameCore core, Player me, Card unit, Player opp)
        {
            var combat = core.CombatSystem;

            if (core.LayerEngine.CalculatePower(unit) >= opp.Life
                && combat.CanAttackTarget(unit, opp, me)) return opp;

            if (TryFindBestTrade(core, me, unit, opp, out var trade)
                && (trade.Gain > 0f || AcceptBadTrade(core, me, unit, trade)))
                return trade.Target;

            return combat.CanAttackTarget(unit, opp, me) ? opp : null;
        }

        /// <summary>英雄技能是否发动（2026-10-15 改版：技能=技能区法术、每回合一次=横置闸）：
        /// 技能卡在场、未横置、未被沉默，且**任一**主动效果的派生费用可付（含自动横置地牌）→ 发动
        ///（多效果卡由 ActivateAsync 按序号发首个可付项，SimpleAI 同步取序号 0——启发式不挑模式）。
        /// 效果语义不按技能特判——目标域非空等由 HeroSkillSystem.ActivateAsync 的 CanActivate 预检统一守卫。</summary>
        public virtual bool WantHeroSkill(GameCore core, Player me)
        {
            var skillCard = HeroSkillSystem.ResolveSkillCard(core, me);
            if (skillCard == null || skillCard.IsTapped()) return false;
            if (skillCard.GetEffectiveCounterCount(CounterRules.SilenceCounter) > 0) return false;
            foreach (var effect in HeroSkillSystem.SkillEffectsOf(skillCard))
            {
                var cost = CostDerivationService.DeriveElementCosts(effect);
                if (cost.IsZero || core.ElementPool.CanPayCostWithAutoTap(cost, me))
                    return true;
            }
            return false;
        }

        // ======================================== 共用小工具（策略实现用） ========================================

        /// <summary>实体当前生命（EntityEffectExtensions.GetLife——含光环加成，Player/Card 同口径）。</summary>
        protected static int LifeOf(Entity e) => e != null ? e.GetLife() : 0;

        /// <summary>实体最大生命（GetMaxLife——含光环加成）。</summary>
        protected static int MaxLifeOf(Entity e) => e != null ? e.GetMaxLife() : 0;

        /// <summary>卡的第一个非主动效果原子里是否含指定类型（主序列含抉择全模式）。</summary>
        protected static bool CardHasAtom(GameCore core, Card card, params AtomicEffectType[] types)
        {
            if (!(card is CardWrapper wrapper)) return false;
            var data = wrapper.GetData();
            if (data?.Effects == null) return false;
            foreach (var def in CardEffectConverter.ConvertAll(data.Effects, data.ID))
            {
                if (def == null || def.IsActivatedEffect) continue;
                foreach (var atom in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0))
                    if (atom != null && Array.IndexOf(types, atom.Type) >= 0) return true;
                if (def.Steps != null && def.Steps.Count > 0) continue;
                foreach (var atom in def.Effects)
                    if (atom != null && Array.IndexOf(types, atom.Type) >= 0) return true;
            }
            return false;
        }
    }

    /// <summary>通用策略=原 SimpleAI 行为（不注入策略时的缺省）。</summary>
    public sealed class GeneralAiStrategy : AiStrategy
    {
        public override string Key => "general";
        public override string DisplayName => "通用";
    }

    /// <summary>
    /// 红快攻：整体低费生物、高攻低命。铺场优先（生物+低费先出）；
    /// 激励/加攻指向攻击力最高的己方生物（多次攻击载体）；直伤在斩杀窗口提最高优先；
    /// 攻击口径（2026-11-01 定案）：致死斩杀 &gt; 正收益解场 &gt; 打脸——换子只要赚就换，
    /// 不赚才把伤害交给角色（快攻的失血速度就是胜利条件）。
    /// </summary>
    public sealed class RedAggroStrategy : AiStrategy
    {
        public override string Key => "red";
        public override string DisplayName => "红快攻";

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            float score = 0f;
            bool isCreature = card is IHasSupertype ht && ht.Supertype == Cardtype.Creature;
            float cost = totalCost(card);

            // ① 火种式 0 费生物最优先（0 费 3/1）：不花资源先卸手牌，为「手牌为 0 抽 1」铺路
            if (isCreature && cost <= 0f) score += 1000f;
            else if (isCreature) score += 200f;

            // ② 加攻（火灵）指向单个最高攻生物（OrderTargetCandidates）；最高攻已 &gt;5 且重整待发、
            //    且手里还有别的牌（火灵触发不了抽卡）时让位给「技能解横置再打一击」；若火灵是最后一张
            //    （打出即空手抽 1），不让位——抽牌优先。
            bool powerBuff = HasAtomAnywhere(card, AtomicEffectType.AddPowerUp,
                                         AtomicEffectType.ModifyPower);
            if (powerBuff && HighestPower(core, me) > 5 && base.WantHeroSkill(core, me)
                && HandCount(core, me) > 1)
                score -= 500f;

            // ③ 触发抽牌的卡（手牌为 0 时抽 1：火灵/激励）留到最后打——软降权，别抢在卸手牌之前
            if (HasHandEmptyDraw(card)) score -= 5000f;

            // ④ 血偿（6 伤直伤）：不等待——抽到、付得起就打（对手 ≤6 时提最高直接终结）
            if (HasAtomAnywhere(card, AtomicEffectType.DealDamage))
                score += me.Opponent != null && me.Opponent.Life <= 6 ? 1200f : 800f;

            score -= cost * 5f; // 低费先出：快攻曲线靠前
            return score;
        }

        /// <summary>
        /// 红快攻攒牌/让位闸（2026-11-01 用户定案）：
        /// · 火炬——代价是永久 −1/−1 吃掉一个己方生物，只在有「已攻击横置」的废子时打
        ///   （先打脸再解放的出手顺序，不是攒牌）；
        /// · 火灵——最高攻 &gt;5 且重整待发时留到技能之后再打；**仅限手里还有别的牌**
        ///   （火灵触发不了空手抽 1 时）；若它是最后一张，打出抽牌优先；
        /// · 血偿——不设等待闸（无目标/付不起时由 PlayLands 直接当地牌）。
        /// </summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
        {
            if (HasCostPayload(card, AtomicEffectType.AddPermanentMinusOne))
                return !HasTappedUnit(core, me);

            if (HasAtomAnywhere(card, AtomicEffectType.AddPowerUp, AtomicEffectType.ModifyPower)
                && HighestPower(core, me) > 5 && base.WantHeroSkill(core, me)
                && HandCount(core, me) > 1)
                return true;

            return false;
        }

        /// <summary>重整只为「让最高攻单位再次攻击」（2026-11-01 用户定案）：须有已横置且攻击力
        /// &gt;5 的己方生物才发动；否则费用留给加攻/直伤。</summary>
        public override bool WantHeroSkill(GameCore core, Player me)
            => base.WantHeroSkill(core, me) && HighestTappedPower(core, me) > 5;

        /// <summary>
        /// 地牌投放优先级（2026-11-01 用户定案）：血偿等直伤打不出去时直接当地牌——
        /// 0 费白板地，优先占坑（PlayLands 只对「打不出的卡」调用本口）。
        /// </summary>
        public override float LandPriority(GameCore core, Player me, Card card, Func<Card, float> totalCost)
            => HasAtomAnywhere(card, AtomicEffectType.DealDamage)
                ? 10000f
                : base.LandPriority(core, me, card, totalCost);

        /// <summary>
        /// AI 目标排序（2026-11-01 用户定案，仅自动选择路径）：
        /// · 火炬代价 → 已横置（已攻击）的生物优先；
        /// · 血偿代价 → 攻击力最低的生物先垫，角色最后；
        /// · 血偿直伤效果 → 直接打脸（角色优先，不可指才回落生物）；
        /// · 加攻/解横置 → 集中到攻击力最高的单个己方生物。
        /// </summary>
        public override List<Entity> OrderTargetCandidates(GameCore core, Player me, TargetSelectionRequest req)
        {
            var list = req?.Candidates;
            if (list == null || list.Count <= 1) return list;

            if (req.IsCostTarget)
            {
                if (req.PromptAtom == AtomicEffectType.AddPermanentMinusOne)
                    return list.OrderByDescending(e => e is Card c && c.IsTapped())
                               .ThenBy(e => e is Card c2 ? core.LayerEngine.CalculatePower(c2) : int.MaxValue)
                               .ToList();
                if (req.PromptAtom == AtomicEffectType.DealDamage)
                    return list.OrderBy(e => e is Player ? 1 : 0)
                               .ThenBy(e => e is Card c3 ? core.LayerEngine.CalculatePower(c3) : int.MaxValue)
                               .ThenBy(e => e is Card c4 ? c4.GetLife() : int.MaxValue)
                               .ToList();
                return list;
            }

            // 直伤效果（血偿）：直接用=直接打脸——角色优先；角色不可指（帷幕/潜行）才回落生物。
            // 注：血偿是「主动」形态的法术效果，SimpleAI.ChooseTargets 会跳过主动效果，
            // 目标在结算期由本钩子选——OrderHarmfulTargets 接不到这条线。
            if (req.PromptAtom == AtomicEffectType.DealDamage)
                return list.OrderByDescending(e => e is Player && ReferenceEquals(e, me.Opponent))
                           .ThenByDescending(e => e is Card cd && cd.GetController() == me.Opponent && cd.IsAlive
                               ? core.LayerEngine.CalculatePower(cd) : int.MinValue)
                           .ToList();

            if (req.PromptAtom == AtomicEffectType.AddPowerUp
                || req.PromptAtom == AtomicEffectType.ModifyPower
                || req.PromptAtom == AtomicEffectType.Untap)
                return list.OrderByDescending(e => e is Card c5 && c5.GetController() == me
                        ? core.LayerEngine.CalculatePower(c5) : int.MinValue)
                    .ToList();
            return list;
        }

        public override IEnumerable<Entity> OrderBeneficialTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
            => candidates // 激励/加攻 → 攻击力最高的己方生物（多次攻击的载体）
                .OrderByDescending(c => c is Card cd && cd.GetController() == me && cd.IsAlive)
                .ThenByDescending(c => c is Card cd2 && cd2.GetController() == me
                    ? core.LayerEngine.CalculatePower(cd2) : int.MinValue)
                .ThenByDescending(c => ReferenceEquals(c, me));

        /// <summary>有害目标排序（2026-11-01 用户定案）：红套直伤=打脸——角色优先，
        /// 角色不可指（帷幕/潜行等）才回落生物威胁降序。红预组只有血偿一条有害线，
        /// 本口即「血偿抽到就用、直接用=直接打脸」。</summary>
        public override IEnumerable<Entity> OrderHarmfulTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
        {
            var opp = me?.Opponent;
            if (opp != null && candidates.Contains(opp))
                return candidates
                    .OrderByDescending(c => ReferenceEquals(c, opp))
                    .ThenByDescending(c => c is Card cd && cd.GetController() == opp && cd.IsAlive
                        ? core.LayerEngine.CalculatePower(cd) : int.MinValue);
            return base.OrderHarmfulTargets(core, me, candidates);
        }

        public override Entity PickAttackTarget(GameCore core, Player me, Card unit, Player opp)
        {
            var combat = core.CombatSystem;

            // ① 致死斩杀：本攻 ≥ 对手剩余生命 → 直取（能赢先赢）
            if (core.LayerEngine.CalculatePower(unit) >= opp.Life
                && combat.CanAttackTarget(unit, opp, me)) return opp;

            // ② 正收益解场无条件做（2026-11-01：红也先解场——赚的换子优先于打脸）
            if (TryFindBestTrade(core, me, unit, opp, out var trade) && trade.Gain > 0f)
                return trade.Target;

            // ③ 不赚的换子让位给打脸（失血速度就是胜利条件；守卫拦截由引擎响应窗口承担）
            return combat.CanAttackTarget(unit, opp, me) ? opp : null;
        }

        // ======================================== 红快攻专用小工具（2026-11-01） ========================================

        /// <summary>己方战场最高攻击力（含光环/指示物；无生物=0）。</summary>
        private static int HighestPower(GameCore core, Player me)
        {
            int best = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive) best = Math.Max(best, core.LayerEngine.CalculatePower(c));
            return best;
        }

        /// <summary>己方战场已横置（已攻击/已发动）的最高攻击力。</summary>
        private static int HighestTappedPower(GameCore core, Player me)
        {
            int best = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && c.IsTapped())
                    best = Math.Max(best, core.LayerEngine.CalculatePower(c));
            return best;
        }

        /// <summary>己方是否有存活且已横置的生物（火炬代价只吃这种废子）。</summary>
        private static bool HasTappedUnit(GameCore core, Player me)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && c.IsTapped()) return true;
            return false;
        }

        /// <summary>卡上是否含指定原子（含主动效果与 Branch.Then 奖励——血偿的法术效果是「主动」
        /// 形态，CardHasAtom 会跳过，故本口不筛激活类型）。</summary>
        private static bool HasAtomAnywhere(Card card, params AtomicEffectType[] types)
        {
            if (!(card is CardWrapper w)) return false;
            var data = w.GetData();
            if (data?.Effects == null) return false;
            foreach (var def in CardEffectConverter.ConvertAll(data.Effects, data.ID))
            {
                if (def == null) continue;
                foreach (var atom in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0))
                    if (AtomMatches(atom, types)) return true;
                foreach (var atom in def.Effects)
                    if (AtomMatches(atom, types)) return true;
            }
            return false;
        }

        private static bool AtomMatches(AtomicEffectInstance atom, AtomicEffectType[] types)
        {
            if (atom == null) return false;
            if (Array.IndexOf(types, atom.Type) >= 0) return true;
            if (atom.Branch?.Then != null)
                foreach (var t in atom.Branch.Then)
                    if (AtomMatches(t, types)) return true;
            return false;
        }

        /// <summary>卡上是否带「手牌为 0 时抽 1」类触发（火灵/激励——Branch.Then 里的 DrawCard）。</summary>
        private static bool HasHandEmptyDraw(Card card)
        {
            if (!(card is CardWrapper w)) return false;
            var data = w.GetData();
            if (data?.Effects == null) return false;
            foreach (var def in CardEffectConverter.ConvertAll(data.Effects, data.ID))
            {
                if (def == null) continue;
                foreach (var atom in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0))
                    if (HasDrawAtom(atom)) return true;
                if (def.Steps != null && def.Steps.Count > 0) continue;
                foreach (var atom in def.Effects)
                    if (HasDrawAtom(atom)) return true;
            }
            return false;
        }

        private static bool HasDrawAtom(AtomicEffectInstance atom)
        {
            if (atom == null) return false;
            if (atom.Type == AtomicEffectType.DrawCard) return true;
            if (atom.Branch?.Then != null)
                foreach (var t in atom.Branch.Then)
                    if (HasDrawAtom(t)) return true;
            return false;
        }

        /// <summary>卡级代价行是否含指定原子（火炬=永久 −1/−1、血偿=3 伤己方）。</summary>
        private static bool HasCostPayload(Card card, AtomicEffectType type)
        {
            if (!(card is CardWrapper w)) return false;
            var data = w.GetData();
            if (data?.PayloadCosts == null) return false;
            foreach (var row in data.PayloadCosts)
            {
                if (row == null) continue;
                foreach (var pe in row.AllPayloads())
                {
                    var atom = CardEffectConverter.ConvertPayloadForDisplay(pe);
                    if (atom != null && atom.Type == type) return true;
                }
            }
            return false;
        }

        /// <summary>手牌张数（火灵「最后一张才抽牌」判定用）。</summary>
        private static int HandCount(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>()).Count;
    }

    /// <summary>
    /// 绿慢速：恢复生命/护甲存活，等待休眠或积攒费用使用大生物，碾压 AOE 逆转场面。
    /// 低血量时回血/护甲最高优先；攒费与大生物（6 费+）优先；治疗/护甲指向受伤最重己方（含角色）；
    /// 攻击只做正收益解场（收益 &gt; 0）或致命斩杀——不赚就留着当守卫（横置=丧失反击/拦截资格）。
    /// </summary>
    public sealed class GreenRampStrategy : AiStrategy
    {
        public override string Key => "green";
        public override string DisplayName => "绿慢速";

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            float score = 0f;
            float cost = totalCost(card);

            // 生存轴：低血量时回血/护甲最高优先（治疗 22 血以下进入紧迫区）
            if (me.Life <= 22 && CardHasAtom(core, card, AtomicEffectType.Heal, AtomicEffectType.AddArmor,
                                            AtomicEffectType.GrantLifelink))
                score += 400f;

            // 攒费/休眠（苏醒=灰费转沉睡攒大生物；旧光合/采掘加费轴已随 2026-10-11/23 资源重做退役）
            if (CardHasAtom(core, card, AtomicEffectType.Sleep))
                score += 100f;

            // 大生物终结（6 费+）能付即最优先——碾压 AOE 的载体
            bool isCreature = card is IHasSupertype ht && ht.Supertype == Cardtype.Creature;
            if (isCreature && cost >= 6f) score += 300f;

            score += cost; // 与默认同向：大费先出（攒到的费花在大生物上）
            return score;
        }

        public override IEnumerable<Entity> OrderBeneficialTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
            => candidates // 治疗/护甲 → 受伤最重的己方（含角色——生存轴先保命）
                .OrderByDescending(c => c is Card cd && cd.GetController() == me)
                .ThenByDescending(c => MaxLifeOf(c) - LifeOf(c))
                .ThenByDescending(c => ReferenceEquals(c, me));

        public override Entity PickAttackTarget(GameCore core, Player me, Card unit, Player opp)
        {
            var combat = core.CombatSystem;

            // 致命斩杀照打（能赢先赢）
            if (core.LayerEngine.CalculatePower(unit) >= opp.Life
                && combat.CanAttackTarget(unit, opp, me)) return opp;

            // 只做正收益解场（2026-11-01）：击杀价值 − 阵亡损失 − 1 费 &gt; 0 才换；
            // 不赚 → null（本回合不攻击，留着守卫/攒费/回血等大生物）。
            if (TryFindBestTrade(core, me, unit, opp, out var trade) && trade.Gain > 0f)
                return trade.Target;
            return null;
        }
    }

    /// <summary>
    /// 蓝控制：有害指示物控制/消灭对面大生物，中等生物换掉对面小生物，逐步控场。
    /// 指示物/解场最优先（对方场面有生物时）；手牌不足时抽牌/发现续资源；
    /// 攻击口径（2026-11-01 定案）：正收益解场 &gt; 不赚但值得的换子（AcceptBadTrade「看情况」）
    /// &gt; 打脸磨血——控制卡组不白送生物，但该压的威胁会压。
    /// </summary>
    public sealed class BlueControlStrategy : AiStrategy
    {
        public override string Key => "blue";
        public override string DisplayName => "蓝控制";

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            float score = 0f;
            bool oppHasBoard = me.Opponent != null
                && core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield).Any(c => c.IsAlive);

            // 控制轴：指示物（毒素/冻结/沉默/紊乱/易损）与解场（弹回）——有目标才值钱（剧毒已转关键词）
            if (oppHasBoard && CardHasAtom(core, card,
                AtomicEffectType.AddToxin, AtomicEffectType.Freeze,
                AtomicEffectType.Silence, AtomicEffectType.RushSickness, AtomicEffectType.AddVulnerable,
                AtomicEffectType.ReturnToHand))
                score += 350f;

            // 资源轴：手牌吃紧时抽卡/发现续资源
            int hand = core.ZoneManager.GetCards(me, Zone.Hand)?.Count ?? 0;
            if (hand <= 3 && CardHasAtom(core, card, AtomicEffectType.DrawCard, AtomicEffectType.DiscoverCard))
                score += 200f;

            score += totalCost(card); // 与默认同向：大费先出（资源花光）
            return score;
        }

        /// <summary>
        /// 蓝控制「看情况」（2026-11-01 用户口径）：不赚的换子只在两种局面接受——
        /// ① 敌方攻击力 ≥ 我方（换掉同等/更大的攻击者，压住下回合伤害）；
        /// ② 我方生命 ≤ 12（危险区，优先削减对方攻击力）。
        /// 其余局面交还「打脸磨血」，不白送生物。
        /// </summary>
        protected override bool AcceptBadTrade(GameCore core, Player me, Card unit, TradeEvaluation trade)
        {
            int myPower = core.LayerEngine.CalculatePower(unit);
            int enemyPower = core.LayerEngine.CalculatePower(trade.Target);
            return enemyPower >= myPower || me.Life <= 12;
        }
    }
}
