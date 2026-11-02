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
    /// · 绿慢速（GreenRampStrategy）——成长最优先、永久属性集中喂獠牙（狩猎载体）、地≥7 回收不断、
    ///   生命≤7 才上守卫保命；攻击只做正收益解场/打脸，狩猎单位不主动攻击（留着回合末白嫖）；
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

        /// <summary>是否允许把这张「打不出的卡」放进元素池当地（2026-11-01 绿套：守护/巨像保留，
        /// 不当废地）。默认 true。</summary>
        public virtual bool ShouldLand(GameCore core, Player me, Card card) => true;

        /// <summary>主动铺地（2026-11-01 绿套）：即使这张卡付得起，也先放当地（凑绿地/灰地）。
        /// 默认 false（只在打不出时才当地）。</summary>
        public virtual bool WantLand(GameCore core, Player me, Card card) => false;

        /// <summary>地牌锁色选择（2026-11-01）：null=缺省主色；绿套前 3 块绿、之后灰。</summary>
        public virtual ManaType? LandColorChoice(GameCore core, Player me, Card card) => null;

        // WantAwakenCast / AwakenWaive（苏醒施放策略钩子，2026-11-01 绿套）已随 2026-11-02
        // 苏醒机制整链删除。

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

        /// <summary>英雄技能是否**先于出牌**（2026-11-01 绿套）：绿慢速的成长要吃满绿地，
        /// 先甩 1 费填充牌会把绿地榨干——所以 3 费起让技能先落地。默认 false（出牌优先）。</summary>
        public virtual bool SkillBeforeCards(GameCore core, Player me) => false;

        /// <summary>响应窗口决策（2026-11-02 AI 响应窗·任务A）：AI 侧响应候选（对手回合连锁窗）
        /// 交策略抉择——返回所选候选即打出/发动；null=不作主（回落内置守卫启发 DefaultAiRespond，
        /// 见 GameActions.SettleResponseWindowAsync 的 ?? 兜底）。默认 null=与策略化之前同行为。
        /// 装配：AiStrategyRegistry 按玩家查表（ResponseWindowService.AiResponder → PickFor）。</summary>
        public virtual ResponseOption PickResponse(GameCore core, Player me, List<ResponseOption> options) => null;

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

    /// <summary>
    /// 按玩家解析 AI 策略（2026-11-02 响应窗·任务A）：对局装配处 Bind（BattleController.StartNewGame
    /// 的 P2 SimpleAI 驱动 / 编辑器 AiBattleDriver 双座位），对局结束 Unbind/Clear（Player 每局新建，
    /// 不清会跨局累积）；ResponseWindowService.AiResponder 装配为 <see cref="PickFor"/>——
    /// 按 holder 查表 → strategy.PickResponse，查不到/策略不作主返回 null（GameActions 调用侧
    /// ?? DefaultAiRespond 回落内置守卫启发）。教学被动/剧本响应器在装配顺序上后装会整体覆盖本口。
    /// </summary>
    public static class AiStrategyRegistry
    {
        private static readonly Dictionary<Player, AiStrategy> _byPlayer = new Dictionary<Player, AiStrategy>();

        /// <summary>对局装配：把策略登记到玩家（同玩家重复 Bind=覆盖）。</summary>
        public static void Bind(Player player, AiStrategy strategy)
        {
            if (player == null || strategy == null) return;
            _byPlayer[player] = strategy;
        }

        public static void Unbind(Player player)
        {
            if (player != null) _byPlayer.Remove(player);
        }

        public static bool TryGet(Player player, out AiStrategy strategy)
            => _byPlayer.TryGetValue(player, out strategy);

        /// <summary>整局解绑（对局结束/Shutdown——Player 实例每局新建，清空防跨局累积）。</summary>
        public static void Clear() => _byPlayer.Clear();

        /// <summary>AiResponder 查表决策口：查不到绑定=null（回落 DefaultAiRespond）。</summary>
        public static ResponseOption PickFor(GameCore core, Player holder, List<ResponseOption> options)
            => holder != null && _byPlayer.TryGetValue(holder, out var strategy)
                ? strategy.PickResponse(core, holder, options)
                : null;
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
            if (HasCostPayload(card, AtomicEffectType.AddPermanentPowerDown)
                || HasCostPayload(card, AtomicEffectType.AddPermanentLifeDown))
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
                if (req.PromptAtom == AtomicEffectType.AddPermanentPowerDown
                    || req.PromptAtom == AtomicEffectType.AddPermanentLifeDown)
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
    /// 绿慢速（2026-11-01 重写·用户口径；2026-11-02 苏醒删除后口径顺延——支援卡不再带苏醒）：
    /// 成长最优先 → 下守卫保角色 → 门条件（地牌&gt;9）到了才用新生养守卫
    /// → 多余费用丢给四种倒计时支援（帷幕/溅射/吸血/狩猎）→ 回收不断（拿回收本身，手里回收到 3 张才换别的）。
    ///
    /// 卡组（30）：新生×7 / 菌丝×3 / 林地守卫 0-4×5 / 雾障·棘爆·噬血藤·獠牙（3-3，倒计时3）×3 / 活体护甲×3。
    /// 打法：
    ///   ① 9 地之前英雄技能（成长）优先：地牌上限+1 且从牌库横置拉地，滚起来才有 5 绿；
    ///   ② 林地守卫先下场保护角色（守护把伤害改写到自己身上），它是**唯一被养的对象**；
    ///   ③ 地牌&gt;9（门条件）之后才开始用新生喂守卫：永久+1/+1 集中在一只守卫身上，
    ///      同回合顺手回收（**回收不能断**）；
    ///   ④ 回收取回「回收」本身（新生），手里新生还 ≤2 张就一直拿新生；已经 &gt;2 才换别的卡补多样性；
    ///   ⑤ 多余的费用全丢给四种倒计时支援效果（倒计时归零给帷幕/溅射/吸血/狩猎）；
    ///   ⑥ 活体护甲：有高血单位 → 角色白嫖等同其生命的护甲；菌丝复活先贴角色，优先级不高。
    /// 攻击（2026-11-01）：斩杀 &gt; 正收益解场 &gt; 打脸；带狩猎的单位**不主动攻击**——留着可行动，
    /// 回合末白嫖一次随机攻击且不横置。守卫绑定目标恒选角色（保命优先）。
    /// </summary>
    public sealed class GreenRampStrategy : AiStrategy
    {
        private const string NameGrowth = "成长";    // 技能：地牌上限+1（技能区，不在手牌）
        private const string NameNewborn = "新生";   // 永久+1；地>9 回收
        private const string NameMycelium = "菌丝";  // 复生；生命≤7 回收
        private const string NameGuard = "林地守卫"; // 1-9 守卫+再生：保角色的墙，也是被养的对象
        private const string NameArmor = "活体护甲"; // 角色获得等同目标生命的护甲
        private const string NameFangs = "獠牙";     // 狩猎：倒计时支援（1-1）
        private const string NameShield = "藤盾";    // 1-1：登场给角色 2 点护甲（早期阻挡+护甲）
        private const string NameDew = "晨露";       // 1-1：倒计时1 归零回 2 点生命（持续奶）

        /// <summary>四种倒计时支援卡（帷幕/溅射/吸血/狩猎的载体，均为 1-1、倒计时3）。</summary>
        private static readonly HashSet<string> SupportNames =
            new HashSet<string> { "雾障", "棘爆", "噬血藤", "獠牙" };

        /// <summary>保命阈值：生命≤7（低血时守卫额外置顶）。</summary>
        private const int LowLife = 7;
        /// <summary>绿地目标：5 绿＝守卫的绿费（灰1绿5）——绿地不够 5，守卫根本下不去。</summary>
        private const int GreenLandTarget = 5;
        /// <summary>门条件：地牌 &gt; 9（=10 地起）才开始用永久属性养守卫 / 回收。</summary>
        private const int GateLands = 9;
        /// <summary>地牌计划总量：铺到 10 块封顶（门条件刚好卡在 &gt;9；成长每次还会再拉一块）。</summary>
        private const int LandPlanTotal = 10;

        public override string Key => "green";
        public override string DisplayName => "绿慢速";

        // ==================== 技能：成长最优先 ====================

        /// <summary>成长：可付就发（地牌上限+1 + 从牌库拉地）——绿套的滚雪球全靠它。</summary>
        public override bool WantHeroSkill(GameCore core, Player me) => base.WantHeroSkill(core, me);

        /// <summary>成长先于出牌：**只在 9 地之前**（用户口径「9 地之前优先用英雄技能」）。
        /// 门条件（地牌&gt;9）之后不再抢绿——绿要留给守卫/新生/回收，技能只剩余费时自己会发。</summary>
        public override bool SkillBeforeCards(GameCore core, Player me)
            => WantHeroSkill(core, me) && TotalLands(core, me) <= GateLands;

        // ==================== 出牌优先级 ====================

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            float score = totalCost(card); // 兜底：大费先出
            int lands = TotalLands(core, me);
            bool gateOpen = lands > GateLands;   // 门条件：地牌 > 9 才开始养
            bool low = me.Life <= LowLife;
            int guards = CountOnBoard(core, me, NameGuard);

            // ① 守卫：先下一张保护角色（守护改写伤害），之后按需补；低血时置顶
            if (Is(card, NameGuard))
            {
                score += guards == 0 ? 2000f : 400f;
                if (low) score += 1500f;
                return score;
            }

            // ② 新生：门开了才养，且优先养守卫（永久+1/+1）；门没开由 ShouldHold 扣着
            if (Is(card, NameNewborn))
            {
                if (gateOpen)
                {
                    if (guards > 0) score += 2400f;
                    else if (CountOnBoard(core, me, null) > 0) score += 600f;
                    if (GraveyardCount(core, me) > 0) score += 1200f; // 回收不断
                }
                return score;
            }

            // ③ 活体护甲：有高血单位才值得（护甲=目标生命）；角色吃紧时再加权
            if (Is(card, NameArmor))
            {
                score += HighestOwnLife(core, me) >= 4 ? 800f : 100f;
                if (me.Life <= 15) score += 300f;
                return score;
            }

            // ④ 菌丝：复生先贴角色，优先级不高
            if (Is(card, NameMycelium))
                return score + (me.GetCounterCount(CounterRules.RebornCounter) == 0 ? 250f : 100f);

            // ⑤ 藤盾（1-1 登场给角色 2 护甲）／晨露（1-1 倒计时1 回 2）：便宜的早期阻挡+续航，早下
            if (Is(card, NameShield)) return score + 800f;
            if (Is(card, NameDew)) return score + 700f;

            // ⑥ 四种倒计时支援（1-1）：9 地之前当墙铺，门开后拿多余费用继续铺
            if (card != null && SupportNames.Contains(NameOf(card)))
                score += gateOpen ? 900f : 450f;
            return score;
        }

        /// <summary>攒牌闸：门条件（地牌&gt;9）之前不养；新生/活体护甲场上没单位就不空放；
        /// 守卫不再扣手——它现在是保护角色的前排。</summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
        {
            if (Is(card, NameNewborn))
            {
                if (TotalLands(core, me) <= GateLands) return true;      // 不到 10 地不开养
                if (CountOnBoard(core, me, null) == 0) return true;      // 没单位可养
            }
            if (Is(card, NameArmor) && CountOnBoard(core, me, null) == 0) return true;
            return false;
        }

        // 苏醒策略段（WantAwakenCast/AwakenWaive 覆写——缺费才睡别拖慢倒计时）已随
        // 2026-11-02 苏醒机制整链删除（灰身材概念已不存在，与倒计时玩法过近）。

        // ==================== 铺地 ====================

        public override float LandPriority(GameCore core, Player me, Card card, Func<Card, float> totalCost)
        {
            if (Is(card, NameGuard) && GreenLands(core, me) < GreenLandTarget) return 12000f;
            if (GreenLands(core, me) < GreenLandTarget && SupportsColor(card, ManaType.Green)) return 6000f;
            return 1000f;
        }

        public override bool ShouldLand(GameCore core, Player me, Card card)
        {
            // 新生＝回收引擎，永远不当土地（回收拿回收，整副牌就靠它转）
            if (Is(card, NameNewborn)) return false;
            int lands = TotalLands(core, me);
            if (lands >= LandPlanTotal) return false;    // 10 地封顶＝门条件刚好卡在这里
            int green = GreenLands(core, me);

            // 早期先把地数补到 3（1-1 小牌要 1~2 费，没地一样打不出）
            if (lands < 3) return true;

            // 绿不够：绿费牌铺成绿地——绿 3 才发得动成长，绿 5 才下得去守卫。
            // 守卫是这套牌最主要的绿地来源（4 张），留一张在手里，其余铺绿。
            if (green < GreenLandTarget && SupportsColor(card, ManaType.Green))
            {
                if (Is(card, NameGuard)) return HandCopies(core, me, NameGuard) > 1;
                return true; // 菌丝/活体护甲也铺绿
            }

            // 绿够了：守卫留手；支援卡（1-1 倒计时）可以先烧掉当地
            if (Is(card, NameGuard)) return false;
            if (card != null && SupportNames.Contains(NameOf(card))) return true;

            // 藤盾/晨露：留一张，重复的才烧
            return HandCopies(core, me, NameOf(card)) > 1;
        }

        public override bool WantLand(GameCore core, Player me, Card card) => ShouldLand(core, me, card);

        public override ManaType? LandColorChoice(GameCore core, Player me, Card card)
        {
            var choices = ElementPoolSystem.GetLandColorChoices(card);
            if (choices != null && choices.Contains(ManaType.Green) && GreenLands(core, me) < GreenLandTarget)
                return ManaType.Green;
            return null;
        }

        // ==================== 攻击 ====================

        public override Entity PickAttackTarget(GameCore core, Player me, Card unit, Player opp)
        {
            var combat = core.CombatSystem;
            int power = core.LayerEngine.CalculatePower(unit);

            if (power >= opp.Life && combat.CanAttackTarget(unit, opp, me)) return opp; // 斩杀先赢

            // 狩猎：不主动攻击——留着可行动，回合末白嫖一次随机攻击且不横置
            if (unit.HasKeyword(KeywordRules.Hunt)) return null;

            if (TryFindBestTrade(core, me, unit, opp, out var trade) && trade.Gain > 0f) return trade.Target;

            // 打脸前先看对手场面：对面有能一脚踢死我的生物 → 留着当墙（快攻对手靠换血拖时间）
            if (combat.CanAttackTarget(unit, opp, me) && !AnyEnemyCouldKill(core, opp, unit)) return opp;
            return null;
        }

        /// <summary>对手场上是否有攻击力 ≥ 我方该生物生命的单位（打脸会被反手一脚踹死 → 留墙）。</summary>
        private static bool AnyEnemyCouldKill(GameCore core, Player opp, Card unit)
        {
            int life = unit.GetLife();
            foreach (var e in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
            {
                if (e == null || !e.IsAlive) continue;
                if (core.LayerEngine.CalculatePower(e) >= life) return true;
            }
            return false;
        }

        // ==================== 目标排序 ====================

        public override List<Entity> OrderTargetCandidates(GameCore core, Player me, TargetSelectionRequest req)
        {
            var list = req?.Candidates;
            if (list == null || list.Count <= 1) return list;
            var atom = req.PromptAtom;

            // 永久属性（2026-11-02 拆四轴）：只喂守卫（养大这堵墙，死后回收再养），其次獠牙，再次其它生物，角色垫底
            if (atom == AtomicEffectType.AddPermanentPowerUp || atom == AtomicEffectType.AddPermanentLifeUp)
                return list.OrderByDescending(e => e is Card c && Is(c, NameGuard))
                           .ThenByDescending(e => e is Card c0 && Is(c0, NameFangs))
                           .ThenByDescending(e => e is Card c2 && c2.GetController() == me ? c2.GetLife() : 0)
                           .ThenByDescending(e => e is Player)
                           .ToList();

            // 复生：先贴角色（用户口径：可以提前贴，优先级不高）
            if (atom == AtomicEffectType.GrantReborn)
                return list.OrderByDescending(e => e is Player)
                           .ThenByDescending(e => e is Card c3 && Is(c3, NameGuard))
                           .ToList();

            // 活体护甲：护甲=目标生命，取生命最高的己方单位
            if (atom == AtomicEffectType.LivingArmor)
                return list.OrderByDescending(e => e is Card c4 && c4.GetController() == me ? c4.GetLife() : int.MinValue)
                           .ToList();

            // 守护绑定：守卫登场时保角色（护的是角色这条命）
            if (atom == AtomicEffectType.Guard || atom == AtomicEffectType.GrantGuardian)
                return list.OrderByDescending(e => e is Player)
                           .ThenByDescending(e => e is Card c9 && c9.GetController() == me ? c9.GetLife() : 0)
                           .ToList();

            // 护甲（藤盾等）：先给角色（角色死了就输了），角色已满甲才给单位
            if (atom == AtomicEffectType.AddArmor)
                return list.OrderByDescending(e => e is Player)
                           .ThenByDescending(e => e is Card c10 && c10.GetController() == me ? (c10.GetMaxLife() - c10.GetLife()) : int.MinValue)
                           .ToList();

            // 回血（晨露等）：给掉血最多的己方（含角色）
            if (atom == AtomicEffectType.Heal)
                return list.OrderByDescending(e => MaxLifeOf(e) - LifeOf(e)).ToList();

            // 倒计时奖励关键词：优先挂被养大的守卫，其次獠牙，再次生命最高的己方生物
            if (atom == AtomicEffectType.GrantTaunt || atom == AtomicEffectType.GrantSplash
                || atom == AtomicEffectType.GrantLifesteal || atom == AtomicEffectType.GrantHunt)
                return list.OrderByDescending(e => e is Card c5 && Is(c5, NameGuard))
                           .ThenByDescending(e => e is Card c7 && Is(c7, NameFangs))
                           .ThenByDescending(e => e is Card c6 && c6.GetController() == me ? c6.GetLife() : 0)
                           .ToList();

            // 回收：拿回收（新生）本身，保持引擎不断；手里新生已 >2 才换别的卡补多样性
            if (atom == AtomicEffectType.RecoverToHand)
            {
                int handNewborn = HandCopies(core, me, NameNewborn);
                if (handNewborn <= 2)
                    return list.OrderByDescending(e => e is Card c8 && Is(c8, NameNewborn))
                               .ThenByDescending(e => e is Card c10 ? c10.GetLife() : 0)
                               .ToList();
                return list.OrderByDescending(e => !(e is Card c11 && Is(c11, NameNewborn))) // 手里新生够多了 → 先拿别的
                           .ThenByDescending(e => e is Card c12 ? c12.GetLife() : 0)
                           .ToList();
            }

            return list;
        }

        // ==================== 绿套小工具 ====================

        private static string NameOf(Card card) => (card as CardWrapper)?.GetData()?.CardName;

        private static bool Is(Card card, string name) => card != null && NameOf(card) == name;

        /// <summary>场上己方单位数量（name=null → 全部存活单位）。</summary>
        private static int CountOnBoard(GameCore core, Player me, string name)
        {
            int n = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
            {
                if (c == null || !c.IsAlive) continue;
                if (name == null || Is(c, name)) n++;
            }
            return n;
        }

        /// <summary>己方墓地卡数（新生/菌丝 的回收燃料）。</summary>
        private static int GraveyardCount(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>()).Count;

        /// <summary>场上己方最高生命（活体护甲的护甲量）。</summary>
        private static int HighestOwnLife(GameCore core, Player me)
        {
            int best = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive) best = Math.Max(best, c.GetLife());
            return best;
        }

        private static bool SupportsColor(Card card, ManaType color)
        {
            var choices = ElementPoolSystem.GetLandColorChoices(card);
            return choices != null && choices.Contains(color);
        }

        private static int GreenLands(GameCore core, Player me) => CountLands(core, me, ManaType.Green);

        private static int CountLands(GameCore core, Player me, ManaType color)
        {
            int n = 0;
            foreach (var pc in core.ElementPool.GetPooledCards(me))
                if (pc != null && pc.LockedColor == color) n++;
            return n;
        }

        private static int TotalLands(GameCore core, Player me)
            => core.ElementPool.GetPooledCards(me)?.Count ?? 0;

        private static int HandCopies(GameCore core, Player me, string name)
        {
            int n = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                if (Is(c, name)) n++;
            return n;
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

        /// <summary>信息轴原子（预组兼顾信息轴）：占卜看顶/展示/宣言/预言——不换场面，只换手牌质量。</summary>
        private static readonly AtomicEffectType[] InfoAxisAtoms =
        {
            AtomicEffectType.LookAtTopCards,
            AtomicEffectType.RevealCard,
            AtomicEffectType.RevealByDimension,
            AtomicEffectType.DeclareHand,
            AtomicEffectType.DeclareDeck,
            AtomicEffectType.DeclareDeckTop,
            AtomicEffectType.ProphecyNextCard,
        };

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            // 疲劳斩杀线（2026-11-02 用户口径）：斩杀成立 → 掏对手牌库的卡置顶梭哈，
            // 压过引擎/控制/信息一切轴（留费闸同步放开，见 ShouldHold 头部）。
            // 磨牌原子用 ModeAtoms 判（不滤激活形态——CardHasAtom 跳过主动效果行，
            // timing19 的蚀忆/噬典在那里恒 false，曾致置顶不触发）。
            if (CanLethalByMill(core, me))
            {
                foreach (var atom in ModeAtoms(card, 0))
                    if (atom != null && atom.Type == AtomicEffectType.MillCard
                        && CostDerivationService.SideLock(atom.TargetKinds) == 1)
                        return 100000f + totalCost(card);
            }

            float score = 0f;
            bool oppHasBoard = me.Opponent != null
                && core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield).Any(c => c.IsAlive);

            // 重放（2026-11-02 用户口径）：最优先的组合 = 8 块蓝地时「重放 → 幻壁」，让无实体结算两次。
            // 该组合下重放必须**先出**（重放指示物挂在自己身上，下一张法术重复），所以给它压过幻壁的分数；
            // 条件不成立（没幻壁 / 蓝地 <8）时重放只是普通效果，不加分。
            // 判定走 ModeAtoms（与疲劳斩杀线同口径）：CardHasAtom 跳过主动效果行，timing=19 的重放那里恒 false。
            bool isReplay = false;
            foreach (var atom in ModeAtoms(card, 0))
                if (atom != null && atom.Type == AtomicEffectType.GrantReplay) { isReplay = true; break; }
            if (isReplay)
            {
                int wallInHand = 0;
                foreach (var hc in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                {
                    var hw = hc as CardWrapper;
                    var hd = hw != null ? hw.GetData() : null;
                    if (hd != null && hd.CardName == "幻壁") wallInHand++;
                }
                bool hasWall = wallInHand > 0;
                bool enoughLands = CountLands(core, me, ManaType.Blue) >= 8;
                if (hasWall && enoughLands) score += 8000f;
                else if (hasWall) score += 400f; // 地不够也先攒着，等 8 地再打（别当普通牌随手扔）
            }

            // 回潮（充盈引擎→洗回，2026-11-02 防疲劳保险）：牌库感知（与疲劳斩杀线同族——读牌库而非场面）：
            // 前期刻意压后（引擎转起来前不抢控制/信息的资源），自己牌库 ≤5 临近疲劳线时拉到
            // 仅次疲劳斩杀线的高位——保险落地比控场更急（洗回 1 张 = 续 1 回合疲劳）。
            bool isDeckSaver = false;
            foreach (var atom in ModeAtoms(card, 0))
            {
                if (atom == null || atom.Type != AtomicEffectType.EngineManaSurplus || atom.Branch?.Then == null) continue;
                foreach (var t in atom.Branch.Then)
                    if (t != null && t.Type == AtomicEffectType.ShuffleIntoDeck) { isDeckSaver = true; break; }
                if (isDeckSaver) break;
            }
            if (isDeckSaver)
            {
                int myDeck = core.ZoneManager.GetCards(me, Zone.Deck)?.Count ?? 0;
                score += myDeck <= 5 ? 5000f : -900f; // 前期净 −300（抵掉下方引擎 +600）——排到信息轴之后
            }

            // 充盈引擎（这套牌的地基）：先铺引擎卡，场面在引擎转起来之后才有意义
            if (CardHasAtom(core, card, AtomicEffectType.EngineManaSurplus)) score += 600f;

            // 控制轴：指示物（毒素/冻结/沉默/紊乱/易损）与解场（弹回）——有目标才值钱（剧毒已转关键词）
            if (oppHasBoard && CardHasAtom(core, card,
                AtomicEffectType.AddToxin, AtomicEffectType.Freeze,
                AtomicEffectType.Silence, AtomicEffectType.RushSickness, AtomicEffectType.AddVulnerable,
                AtomicEffectType.ReturnToHand))
                score += 350f;

            // 信息轴：手牌质量（低优先，但不与留费冲突）
            if (CardHasAtom(core, card, InfoAxisAtoms)) score += 150f;

            // 资源轴：手牌吃紧时抽卡/发现续资源
            int hand = core.ZoneManager.GetCards(me, Zone.Hand)?.Count ?? 0;
            if (hand <= 3 && CardHasAtom(core, card, AtomicEffectType.DrawCard, AtomicEffectType.DiscoverCard))
                score += 200f;

            score += totalCost(card); // 与默认同向：大费先出（资源花光）
            return score;
        }

        // ==================== 铺地（2026-11-02 打断玩法必要脚手架） ====================

        /// <summary>蓝地目标（2026-11-02 磨牌版重定）：旧的 3 块只够付 instant，但磨牌套的
        /// **代价门槛=同色地数**（蚀忆 4 / 噬典 6 / 幻壁 8）——卡在 3 块时这几张永远不可施放
        /// （实测 31 回合蓝地恒 3、磨牌 0 次）。抬到 9 覆盖最贵一张（幻壁 8）。</summary>
        private const int BlueLandTarget = 9;

        /// <summary>蓝控制主动铺蓝地：instant（21~25）声明费为 0 → CanPlayCard 跳过可付性检查，
        /// 永远「看似可打」而进不了普通铺地漏斗——没蓝地它们就是死卡（手里付不起、也变不成地）。
        /// 蓝地不足目标时，把可锁蓝的卡主动当地铺（WantLand），锁色恒取蓝。</summary>
        public override bool WantLand(GameCore core, Player me, Card card)
        {
            var choices = ElementPoolSystem.GetLandColorChoices(card);
            return choices != null && choices.Contains(ManaType.Blue)
                   && CountLands(core, me, ManaType.Blue) < BlueLandTarget;
        }

        /// <summary>地牌锁色：能锁蓝恒锁蓝（引擎费灰由灰地承担，蓝地专供 instant 打断）。</summary>
        public override ManaType? LandColorChoice(GameCore core, Player me, Card card)
        {
            var choices = ElementPoolSystem.GetLandColorChoices(card);
            return choices != null && choices.Contains(ManaType.Blue) ? ManaType.Blue : null;
        }

        /// <summary>铺地优先级：缺蓝地时蓝锁候选置顶（一天一块，先保证打断弹药）。</summary>
        public override float LandPriority(GameCore core, Player me, Card card, Func<Card, float> totalCost)
        {
            var choices = ElementPoolSystem.GetLandColorChoices(card);
            if (choices != null && choices.Contains(ManaType.Blue)
                && CountLands(core, me, ManaType.Blue) < BlueLandTarget)
                return 10000f;
            return base.LandPriority(core, me, card, totalCost);
        }

        private static int CountLands(GameCore core, Player me, ManaType color)
        {
            int n = 0;
            foreach (var pc in core.ElementPool.GetPooledCards(me))
                if (pc != null && pc.LockedColor == color) n++;
            return n;
        }

        /// <summary>留费闸（2026-11-02 任务B 动态化）：**只在己方场上存在充盈引擎（EngineManaSurplus）时留费**，
        /// 没引擎就不留（正常铺场出牌，别空过）。基线=对手剩余元素总量（GetRemainingElementTotal，
        /// 与充盈判据同口径=未横置地+钱包）+ 场上充盈引擎门槛最大值（BranchEngines.MaxManaSurplusThreshold·
        /// Then 锚价推导）——替换 2026-11-01 的死留 2 块。打断玩法的命脉：充盈差与对手回合 instant
        /// 全靠没花掉的剩余（对手花大钱后剩余骤降、我方留费不动）。
        /// 保护区（任务B③）：剩余 ≤ 基线（本回合留费目标不可达）时**只放行不清空元素池的低费牌**
        ///（need &lt; remaining 才放行——留费后至少还能出 1 张牌，也不把对手回合的打断弹药清零）。
        /// 充盈引擎卡本身照常铺（它是产出留费价值的地基，不是消耗品）。
        /// </summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
        {
            // 疲劳斩杀线（2026-11-02 用户口径）：掏空对手牌库的回合不留费——对手回合的自保费
            // 换不来必杀节奏（磨牌/强制抽牌出牌优先级已在 CardPlayScore 同步置顶）。
            if (CanLethalByMill(core, me)) return false;

            if (CardHasAtom(core, card, AtomicEffectType.EngineManaSurplus)) return false;
            var bill = GameActions.GetCardCost(card, 0);
            if (bill == null || bill.IsZero) return false;

            var opp = me.Opponent;
            int threshold = BranchEngines.MaxManaSurplusThreshold(me); // 0=场上无充盈引擎 → 不留费
            if (opp == null || threshold <= 0) return false;

            int need = (int)Math.Ceiling(bill.Total - 1e-4f);
            int remaining = core.ElementPool.GetRemainingElementTotal(me);
            int reserve = core.ElementPool.GetRemainingElementTotal(opp) + threshold;

            if (remaining <= reserve)
                return need >= remaining; // 保护区：放行后池子还剩 ≥1 的低费牌；清空元素池的大费扣住
            return remaining - need < reserve;
        }

        // ==================== 疲劳斩杀线（2026-11-02 用户口径：会算死） ====================

        /// <summary>疲劳斩杀线：本回合梭哈掏牌库（磨牌主干 + 资敌抽代价）+ **对手下回合开始自然抽 1 张**
        /// （用户点名必计入）能把手牌库掏空，且掏空后累计疲劳伤害 ≥ 对手生命 → 算斩杀。
        /// 疲劳=炉石式递增（Zones.ApplyFatigue：第 N 次空库抽牌受 N 伤，起点接 <see cref="Entity.FatigueCount"/>），
        /// 掏空后对手每回合自然抽都挨递增疲劳——累计迟早 ≥ 生命；模拟顺序照引擎实况：
        /// 施放=付费步（代价对手抽）先于效果结算（磨牌）→ 磨牌对空库无操作 → 对手下回合开始自然抽。</summary>
        private static bool CanLethalByMill(GameCore core, Player me)
        {
            var opp = me?.Opponent;
            if (opp == null || core.ZoneManager == null) return false;

            int pool = core.ZoneManager.GetCards(opp, Zone.Deck)?.Count ?? 0;
            int life = opp.Life;
            if (life <= 0) return false;
            int fatigue = opp.FatigueCount;
            int dmg = 0;

            // ① 本回合付得起的掏牌库量（逐卡独立可付的乐观口径——色锁细算不在此）
            EstimateAffordableDrain(core, me, out int millTotal, out int drawTotal);
            if (millTotal + drawTotal <= 0) return false;

            // ② 模拟：先强制抽（施放付费步），抽在空库上挨递增疲劳
            for (int i = 0; i < drawTotal; i++)
            {
                if (pool > 0) pool--;
                else { fatigue++; dmg += fatigue; }
            }
            // 磨牌（效果结算步）：对空库无操作
            pool = Math.Max(0, pool - millTotal);
            // ③ 对手下回合开始自然抽 1 张（用户口径：斩杀量必含这张）
            if (pool > 0) pool--;
            else { fatigue++; dmg += fatigue; }

            if (pool > 0) return false; // 掏不空：谈不上疲劳斩杀

            // ④ 掏空后：对手每回合自然抽挨递增疲劳，累计到 ≥ 生命（递增无上限，必然达成——guard 防死循环）
            int guard = 0;
            while (dmg < life && guard++ < 200) { fatigue++; dmg += fatigue; }
            return dmg >= life;
        }

        /// <summary>手牌里付得起的「掏对手牌库」总量：磨牌（MillCard 主干，域锁对方 {8}）+
        /// 资敌抽（DrawCard 代价栏 payload，域锁对方 {8}）。可付=卡费 ≤ 剩余元素总量（总量近似）。</summary>
        private static void EstimateAffordableDrain(GameCore core, Player me, out int millTotal, out int drawTotal)
        {
            millTotal = 0; drawTotal = 0;
            if (core.ZoneManager == null) return;
            int budget = core.ElementPool != null ? core.ElementPool.GetRemainingElementTotal(me) : 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (c == null) continue;
                if (TotalCostOf(core, c, 0) > budget) continue; // 付不起的不计入
                foreach (var atom in ModeAtoms(c, 0))
                {
                    if (atom == null || atom.Type != AtomicEffectType.MillCard) continue;
                    if (CostDerivationService.SideLock(atom.TargetKinds) == 1)
                        millTotal += Math.Max(1, atom.Value);
                }
                var data = (c as CardWrapper)?.GetData();
                if (data?.PayloadCosts == null) continue;
                foreach (var row in data.PayloadCosts)
                {
                    if (row == null) continue;
                    foreach (var pe in row.AllPayloads())
                    {
                        var inst = CardEffectConverter.ConvertPayloadForDisplay(pe);
                        if (inst != null && inst.Type == AtomicEffectType.DrawCard
                            && CostDerivationService.SideLock(inst.TargetKinds) == 1)
                            drawTotal += Math.Max(1, pe.value);
                    }
                }
            }
        }

        // ======================================== 响应窗（2026-11-02 任务A·打断式响应） ========================================

        /// <summary>
        /// 蓝控制响应窗决策（2026-11-02 打断口径）：蓝套靠打断赢——只在**对手回合**出手，
        /// 自己回合的阶段窗/自家连锁一律让过（instant 与留费留给对手回合，别在自家窗口空烧）。
        /// 打断锚=栈顶为对手发起的对象（其刚打出的整卡/攻击宣言/阶段伪对象）；自己的响应上栈后
        /// 栈顶=自己 → 本窗不再接（一次打断一个动作，防连锁倾泻手牌）。决策序：
        /// a. 低费换高费（盈余来源）：对手刚施放的整卡费用 &gt; 我最便宜 instant 的费用 → 打最便宜那张回答它；
        /// b. 场面威胁：对面攻击力最高/费用最高的生物 → 弹回优先，其次冻结；
        /// c. 手牌破坏：对手手牌 ≥4 → 弃牌/看手牌类优先；
        /// d. 斩杀线：对面角色生命 ≤ 直伤值 → 直伤；
        /// e. 守卫候选不作主（返回 null 回落内置守卫启发 DefaultAiRespond）；都不划算=null 让过。
        /// 选定手牌卡时同步武装 TargetSelectionService.AiOrder（镜像 SimpleAI 的回合装配）——
        /// 响应出牌的结算期目标反问按本策略排序（见 OrderTargetCandidates）。
        /// </summary>
        public override ResponseOption PickResponse(GameCore core, Player me, List<ResponseOption> options)
        {
            if (options == null || options.Count == 0) return null;
            if (core.TurnEngine == null || ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return null; // 只打对手回合
            var opp = me?.Opponent;
            if (opp == null) return null;

            // 打断锚：栈顶（末位）须为对手发起的对象
            EffectInstance top = null;
            foreach (var obj in core.StackEngine.GetStackContents() ?? new List<EffectInstance>())
                if (obj != null) top = obj;
            if (top == null || !ReferenceEquals(top.Controller, opp)) return null;

            // 手牌 instant 候选分类（每张连同其模式原子与施放费）
            ResponseOption cheapest = null; int cheapestCost = int.MaxValue;
            ResponseOption bounce = null, freeze = null, disrupt = null, directDamage = null;
            ResponseOption spellban = null; // 组3 无实体候选（宣攻专道专用）
            int directDamageValue = 0;
            bool oppHasCreature = false;
            foreach (var e2 in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                if (e2 != null && e2.IsAlive && e2.IsLivingUnit()) { oppHasCreature = true; break; }
            int oppHand = (core.ZoneManager.GetCards(opp, Zone.Hand) ?? new List<Card>()).Count;

            foreach (var o in options)
            {
                if (o == null || o.Kind != ResponseOption.ResponseKind.HandCard || o.SourceCard == null) continue;
                // 组3 无实体卡（幻壁·GrantSpellban，2026-11-02 蓝套v3 用户口径）：剥离通用候选池，
                // 只走下方宣攻专道——不在自家/非宣攻窗口被「低费换高费」当燃料烧掉。
                bool isSpellban = false;
                foreach (var atom in ModeAtoms(o.SourceCard, o.ModeIndex))
                    if (atom != null && atom.Type == AtomicEffectType.GrantSpellban) { isSpellban = true; break; }
                if (isSpellban) { if (spellban == null) spellban = o; continue; }
                int cost = TotalCostOf(core, o.SourceCard, o.ModeIndex);
                if (cost < cheapestCost) { cheapestCost = cost; cheapest = o; }
                foreach (var atom in ModeAtoms(o.SourceCard, o.ModeIndex))
                {
                    if (atom == null) continue;
                    if (atom.Type == AtomicEffectType.ReturnToHand && bounce == null && oppHasCreature) bounce = o;
                    else if (atom.Type == AtomicEffectType.Freeze && freeze == null
                             && HighestEnemyPower(core, opp) >= 1) freeze = o;
                    else if (disrupt == null && oppHand >= 4 && IsDisruptAtom(atom.Type)) disrupt = o;
                    else if (atom.Type == AtomicEffectType.DealDamage && directDamage == null)
                    {
                        int dmg = atom.Value > 0 ? atom.Value : 1;
                        if (dmg >= opp.Life) { directDamage = o; directDamageValue = dmg; }
                    }
                }
            }

            // 组3 无实体卡专道：只在对手回合、栈顶为对手**攻击宣言**时出手（对手回合+对手发起已由前置保证）。
            // 优先级（用户定案）：① 己方角色生命 ≤7（快死）→ 必用；② 对手战场总攻击力 ≥5 → 用；
            // ③ 总攻 <5 → 放弃（本窗不作主，也不回流通用链）。
            if (spellban != null && top.IsAttackDeclaration)
            {
                int oppTotalPower = 0;
                foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                    if (c != null && c.IsAlive && c.IsLivingUnit())
                        oppTotalPower += core.LayerEngine.CalculatePower(c);
                if (me.Life <= 7 || oppTotalPower >= 5)
                {
                    TargetSelectionService.AiOrder = req => OrderTargetCandidates(core, req?.Chooser ?? me, req);
                    return spellban;
                }
            }

            ResponseOption pick = null;
            if (cheapest != null && top.IsCardCast)
            {
                // a. 低费换高费：对手刚打出的整卡比我最便宜的 instant 贵 → 用最便宜那张回答
                var oppCard = top.Source as Card;
                int oppCost = oppCard != null ? TotalCostOf(core, oppCard, top.ModeIndex) : 0;
                if (oppCost > 0 && cheapestCost < oppCost) pick = cheapest;
            }
            if (pick == null && bounce != null) pick = bounce;           // b. 弹回最大威胁
            if (pick == null && freeze != null) pick = freeze;           // b. 冻结最高攻
            if (pick == null && disrupt != null) pick = disrupt;         // c. 手牌破坏
            if (pick == null && directDamage != null) pick = directDamage; // d. 直伤斩杀线

            if (pick != null)
                TargetSelectionService.AiOrder = req => OrderTargetCandidates(core, req?.Chooser ?? me, req);
            return pick;
        }

        /// <summary>手牌破坏类原子：弃牌与看手牌（宣言/展示）——切断对手续航。</summary>
        private static bool IsDisruptAtom(AtomicEffectType type)
            => type == AtomicEffectType.DiscardCard
               || type == AtomicEffectType.RevealCard
               || type == AtomicEffectType.RevealByDimension
               || type == AtomicEffectType.DeclareHand;

        /// <summary>响应出牌（本策略武装的 AiOrder 时段）目标排序：弹回→对面费用最高；
        /// 冻结→对面攻击最高；直伤→角色优先（斩杀打脸，角色不可指才回落生物）；
        /// 失去生命→生命最低（最可能击杀）。其余交默认序。</summary>
        public override List<Entity> OrderTargetCandidates(GameCore core, Player me, TargetSelectionRequest req)
        {
            var list = req?.Candidates;
            if (list == null || list.Count <= 1) return list;
            var opp = me?.Opponent;

            // 潜行目标（2026-11-02 口径修订：潜行由英雄技能「隐匿」提供，结界登场潜行已删）：
            // 对手场上总攻击力 ≥ 己方角色生命（将死）→ 技能给**己方角色**挂潜行买一轮（技能域
            // [1] OwnLivingUnit 含角色——玩家同样可直接自选）；「其余给结界」不可达——GrantStealth
            // 表行域=有生命单位（生物+角色），结界=无生命单位不在域内，不自创目标域，保持默认序。
            if (req.PromptAtom == AtomicEffectType.GrantStealth)
            {
                if (opp != null && core.ZoneManager != null)
                {
                    int totalPower = 0;
                    foreach (var e in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                        if (e != null && e.IsAlive && e.IsLivingUnit())
                            totalPower += core.LayerEngine.CalculatePower(e);
                    if (totalPower >= me.Life)
                        return list.OrderByDescending(e => e is Player && ReferenceEquals(e, me)).ToList();
                }
                return list;
            }

            if (req.PromptAtom == AtomicEffectType.ReturnToHand)
                return list.OrderByDescending(e => e is Card c && c.GetController() == opp
                        ? TotalCostOf(core, c, 0) : int.MinValue).ToList();
            if (req.PromptAtom == AtomicEffectType.Freeze)
                return list.OrderByDescending(e => e is Card c2 && c2.GetController() == opp && c2.IsAlive
                        ? core.LayerEngine.CalculatePower(c2) : int.MinValue).ToList();
            if (req.PromptAtom == AtomicEffectType.DealDamage)
                return list.OrderByDescending(e => e is Player && ReferenceEquals(e, opp))
                           .ThenByDescending(e => e is Card cd && cd.GetController() == opp && cd.IsAlive
                               ? core.LayerEngine.CalculatePower(cd) : int.MinValue).ToList();
            if (req.PromptAtom == AtomicEffectType.AddLifeDown || req.PromptAtom == AtomicEffectType.LifeLoss)
                return list.OrderBy(e => e is Card c3 ? c3.GetLife() : int.MaxValue)
                           .ThenByDescending(e => e is Card c4 && c4.IsAlive
                               ? core.LayerEngine.CalculatePower(c4) : int.MinValue).ToList();
            return list;
        }

        /// <summary>手牌卡指定抉择模式（modeIndex=响应候选携带位）的主序列原子列表。
        /// 不筛激活形态——蓝套 instant 全是主动效果行，CardHasAtom 的 IsActivatedEffect 过滤不适用。</summary>
        private static List<AtomicEffectInstance> ModeAtoms(Card card, int modeIndex)
        {
            var result = new List<AtomicEffectInstance>();
            var defs = GameActions.GetCastableEffectDefinitions(card);
            if (defs == null || modeIndex < 0 || modeIndex >= defs.Count) return result;
            var def = defs[modeIndex];
            if (def == null) return result;
            foreach (var atom in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0))
                if (atom != null) result.Add(atom);
            if (def.Steps == null || def.Steps.Count == 0)
                foreach (var atom in def.Effects)
                    if (atom != null) result.Add(atom);
            return result;
        }

        /// <summary>卡的施放费合计（取整——与 ShouldHold 同口径；modeIndex=抉择模式位）。</summary>
        private static int TotalCostOf(GameCore core, Card card, int modeIndex)
        {
            var bill = GameActions.GetCardCost(card, modeIndex);
            return bill == null ? 0 : (int)Math.Ceiling(bill.Total - 1e-4f);
        }

        /// <summary>对面存活生物的最高攻击力（冻结「断节奏」的价值锚）。</summary>
        private static int HighestEnemyPower(GameCore core, Player opp)
        {
            int best = 0;
            foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && c.IsLivingUnit())
                    best = Math.Max(best, core.LayerEngine.CalculatePower(c));
            return best;
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
