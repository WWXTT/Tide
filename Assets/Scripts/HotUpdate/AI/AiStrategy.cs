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

        // ======================================== 对手选代价目标（2026-11-02 定案） ========================================

        /// <summary>
        /// AiOrder 装配统一前置分发（2026-11-02 对手选代价目标定案）：代价请求且选择权在对手
        ///（Chooser≠支付方 CostPayer）时按**对手视角**排序（最大化支付方损失）——代价选择策略
        /// 无关节，红/绿/蓝统一走本口防各策略漏改；其余请求回落策略自身 OrderTargetCandidates。
        /// AiOrder 武装点（SimpleAI 回合装配/蓝策略响应段）一律经本口，不再直调策略排序。
        /// </summary>
        public static List<Entity> DispatchOrder(AiStrategy strategy, GameCore core, Player chooser,
            TargetSelectionRequest req)
        {
            var list = req?.Candidates;
            if (list != null && list.Count > 1 && req.IsCostTarget)
            {
                var payer = req.CostPayer ?? chooser?.Opponent;
                if (payer != null && !ReferenceEquals(payer, chooser))
                    return OrderOpponentChosenCostTargets(core, payer, list, req.PromptValue);
            }
            return strategy?.OrderTargetCandidates(core, chooser, req);
        }

        /// <summary>对手视角的代价落点（2026-11-02 定案：代价候选 &gt;1 由对手选）：
        /// 最大化支付方损失——能打死的单位最优先（连击载体加分），其次支付方角色（推脸，
        /// 临近斩杀再加权），打不死按身板降序（蹭最贵的）。资敌有益原子同序可用：
        /// 候选在对手自己侧，「价值最高优先」即对手的最优受益落点。</summary>
        private static List<Entity> OrderOpponentChosenCostTargets(GameCore core, Player payer,
            List<Entity> list, int value)
            => list.OrderByDescending(e => OpponentChosenCostScore(core, payer, e, value)).ToList();

        /// <summary>对手视角代价落点估值（越大越先被选中）。</summary>
        private static float OpponentChosenCostScore(GameCore core, Player payer, Entity e, int value)
        {
            if (e is Player p && ReferenceEquals(p, payer))
                return 2000f + value * 10f + (payer.Life - value <= 5 ? 4000f : 0f); // 推脸；残余 ≤5 直逼斩杀
            if (!(e is Card c) || !c.IsAlive) return -1f;
            float v = UnitValue(core, c);
            if (c.GetLife() <= value)
                return 3000f + v * 10f + (c.HasKeyword(KeywordRules.DoubleStrike) ? 2000f : 0f); // 打得死：贵子/连击载体先杀
            return v * 2f; // 打不死：蹭价值最高的
        }

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
    /// 红快攻（2026-11-03 v5，卡组=火灵·攻/火灵·守/火种·锐/火种·厚/血偿/赋予连击）：
    ///
    /// 卡组两条线：0 费火种铺场（3/1 抢伤害、1/3 当被养的底座）+ 1 费火灵（登场 +2 攻，攻/守两版）
    /// + 3 费血偿（6 伤敌）与赋予连击（连击）——两张 3 费都挂并列代价「**弃一张己方手牌**」
    ///（2026-11-03 改版，取代原「对己方单位造成 3 点伤害」）：更契合红、更好用；选牌由对手选，
    /// 而手牌是隐蔽区 → 引擎自动换随机。副作用：手里除本卡外没有第二张牌时，代价付不出、整卡不可用。
    ///
    /// 打法口径（用户 2026-11-02 四条）：
    /// ① 对手**有可阻拦生物**时，加攻/连击/激励优先给**高生命**的己方单位（1 点伤害就能点掉 1 命攻兵）；
    /// ② 斩杀线按连击算双倍伤害（有效伤害 = 攻击力 ×（连击?2:1）），全队有效伤害够就直接打脸；
    /// ③ **只带守卫的火灵**留着保护大哥——场上还没有大哥（生命 ≥3 的己方单位）之前不打，先下只带攻击的火灵加伤害；
    /// ④ **哪个 1/3 火种被养大，谁就是大哥**：加攻不再按「攻击力最高」堆（会被 1 点伤害轻松点掉）；
    ///    只具备 +攻 时把攻击**分散**到不同单位（总伤害不变、更稳）；但手里连击即将可放、且对手无阻拦时，
    ///    改回**集中**到同一只做连击爆发。
    /// 攻击口径沿用 2026-11-01：致死斩杀 &gt; 正收益解场 &gt; 打脸（赚的换子照换）。
    /// 代价自伤的目标排序（生命 20 分界那套）已随成本改弃牌**整段删除**——隐蔽区随机选牌，AI 无目标可排。
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
            var data = DataOf(card);

            // ① 0 费生物最优先（火种 3/1、1/3）：不花资源先铺场
            if (isCreature && cost <= 0f) score += 1200f;
            else if (isCreature && data != null && data.NoAttack) score += 150f; // 火灵·守：等大哥（ShouldHold 闸）
            else if (isCreature) score += 400f;                                  // 火灵·攻

            // ② 赋予连击：有值得连击的载体（攻 ≥2 的可攻击单位）才抢，否则压后
            if (IsDoubleStrikeSpell(card))
            {
                var carrier = BurstCarrier(core, me);
                score += carrier != null && PowerOf(core, carrier) >= 2 ? 900f : 200f;
            }

            // ③ 血偿（6 伤直伤）：抽到、付得起就打；对手进斩杀线时提最高
            if (IsDirectDamageSpell(card))
                score += me.Opponent != null && me.Opponent.Life <= 6 ? 1300f : 700f;

            // ④ 触发空手抽 1 的卡（火灵/激励）软降权到最后打——先把别的牌卸掉
            if (HasHandEmptyDraw(card)) score -= 5000f;

            score -= cost * 5f; // 低费先出
            return score;
        }

        /// <summary>
        /// 攒牌/让位闸（2026-11-03 改版）：
        /// · 火灵·守（只带守卫）——留到大哥登场再下（生命 ≤10 自救除外）；
        /// · 血偿/赋予连击（代价=弃一张己方手牌）——手里除本卡外没有第二张牌时留手：
        ///   代价要求「另有一张可弃」，只剩本卡时不打（见 <see cref="DiscardCostPayable"/>）。
        /// </summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
        {
            if (IsGuardOnly(card) && !HasBigBrother(core, me) && me.Life > 10)
                return true;
            if (HasDiscardCost(card) && !DiscardCostPayable(core, me))
                return true;
            return false;
        }

        /// <summary>弃牌代价在手牌结算后是否还付得起（2026-11-03 用户定案）：本卡离手后手里
        /// 至少还要有一张可弃的牌。引擎当前在「只剩本卡」时仍放行、代价空转（已上报），
        /// AI 侧先按设计口径自缚——不白嫖。</summary>
        private static bool DiscardCostPayable(GameCore core, Player me)
        {
            int hand = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>()).Count;
            return hand >= 2;
        }

        /// <summary>卡是否带「弃牌」代价（效果级 costs + 卡级 payloads 同口径扫）。</summary>
        private static bool HasDiscardCost(Card card)
        {
            var data = DataOf(card);
            if (data == null) return false;
            bool IsDiscard(AtomicEffectEntry pe)
            {
                if (pe == null || string.IsNullOrEmpty(pe.refId)) return false;
                var row = AtomicEffectTable.GetByHashId(pe.refId);
                return row != null && row.EnumName == nameof(AtomicEffectType.DiscardCard);
            }
            if (data.PayloadCosts != null)
                foreach (var row in data.PayloadCosts)
                {
                    if (row == null) continue;
                    foreach (var pe in row.AllPayloads()) if (IsDiscard(pe)) return true;
                }
            if (data.Effects != null)
                foreach (var fx in data.Effects)
                {
                    if (fx?.Costs == null) continue;
                    foreach (var ce in fx.Costs)
                    {
                        if (ce == null) continue;
                        foreach (var pe in ce.AllPayloads()) if (IsDiscard(pe)) return true;
                    }
                }
            return false;
        }

        /// <summary>英雄技能=激励（重置一个己方单位）：只为一只能再打一波的攻击单位发动
        /// （已横置、有效伤害 ≥4——被 +2 攻养过或带连击），不空放。</summary>
        public override bool WantHeroSkill(GameCore core, Player me)
            => base.WantHeroSkill(core, me) && BestUntapTarget(core, me) != null;

        // ======================================== 连锁响应（2026-11-02 用户新定案） ========================================

        /// <summary>
        /// 连锁响应（2026-11-02 用户定案·两条策略）：**我的攻击被守卫拦下时**（对手宣告阻拦 → 攻方
        /// 响应窗，3 红刚好够一张 3 费法术），在手里同时有直伤与连击时二选一：
        /// ① **连击**：连击先结算一次战斗伤害——攻击力 ≥ 守卫生命则先制击杀，我方攻击手不掉血（无伤清障），
        ///    代价是这一击不再打到原目标（打脸伤害为 0）；
        /// ② **直伤**：连锁血偿把守卫打死，守卫结算失效 → 攻击回到原目标（打脸）——用一张终结牌开路。
        /// 抉择：**斩杀线优先**（开路后本回合伤害够杀 → 选直伤）；否则**场面得失优先**（连击能先制
        /// 无伤清掉守卫 → 选连击，把血偿留给脸）；连击打不死而直伤能打死 → 直伤；都打不死 → 让过。
        /// 其余响应面（防守方守卫决策等）一律交回内置启发（返回 null）。
        /// </summary>
        public override ResponseOption PickResponse(GameCore core, Player me, List<ResponseOption> options)
        {
            if (core == null || me == null || options == null || options.Count == 0) return null;

            var stack = core.StackEngine?.SnapshotStack();
            if (stack == null || stack.Count < 2) return null;
            var attack = stack.LastOrDefault(e => e != null && e.IsAttackDeclaration
                                                  && ReferenceEquals(e.Controller, me));
            if (attack == null) return null; // 不是我的攻击被响应 → 不做主
            var guard = stack.LastOrDefault(e => e != null && e.IsGuardDeclaration
                                                 && ReferenceEquals(e.InterceptedAttack, attack));
            if (guard == null || guard.Source == null) return null;

            var attacker = attack.Source as Card;
            if (attacker == null) return null;

            ResponseOption dsOpt = null, dmgOpt = null;
            foreach (var o in options)
            {
                if (o == null || o.Kind != ResponseOption.ResponseKind.HandCard || o.SourceCard == null) continue;
                if (dsOpt == null && IsDoubleStrikeSpell(o.SourceCard)) dsOpt = o;
                else if (dmgOpt == null && IsDirectDamageSpell(o.SourceCard)) dmgOpt = o;
            }
            if (dsOpt == null && dmgOpt == null) return null;

            int myPow = PowerOf(core, attacker);
            int oppLife = me.Opponent?.Life ?? 0;

            // 守卫者是生物才能算「打死」；角色守卫（无生命值概念）不在此口径内 → 让过交内置
            var guarder = guard.Source as Card;
            if (guarder == null || !guarder.IsAlive) return null;
            int gLife = guarder.GetLife();

            bool dsKills = myPow >= gLife;                       // 连击先制一击致死 → 无伤
            bool dmgKills = dmgOpt != null && DirectDamageValue(dmgOpt.SourceCard) >= gLife; // 直伤开路
            bool faceLethal = myPow >= oppLife;                  // 这一击回到脸就赢

            // 若直伤开路后攻击回到脸，本回合剩余伤害够不够斩杀（不含已用的这张直伤）
            bool lethalAfterOpen = dmgKills && CanFinishAfterOpen(core, me, myPow, dmgOpt?.SourceCard);

            if (dmgKills && (faceLethal || lethalAfterOpen))
                return WithTargets(dmgOpt, guarder);             // ① 斩杀线优先：直伤开路 → 打脸
            if (dsKills)
                return WithTargets(dsOpt, attacker);             // ② 场面优先：连击先制无伤清障
            if (dmgKills)
                return WithTargets(dmgOpt, guarder);             // ③ 连击打不死：只能直伤清障
            return null;                                          // ④ 都打不死 → 让过（正常战斗）
        }

        /// <summary>响应选项写入声明期目标（AI 侧不走人类的目标预选收集，这里直接钉死）。</summary>
        private static ResponseOption WithTargets(ResponseOption opt, Entity target)
        {
            if (opt == null) return null;
            opt.Targets = new List<Entity> { target };
            return opt;
        }

        /// <summary>直伤开路（守卫死亡、攻击回到原目标）后，本回合剩余伤害是否足以斩杀：
        /// 这一击的打脸值 + 其它未横置攻击手的有效伤害 + 手里其它可付直伤（排除刚用的这张）。</summary>
        private static bool CanFinishAfterOpen(GameCore core, Player me, int thisHit, Card exclude)
        {
            var opp = me?.Opponent;
            if (opp == null) return false;
            var stack = core.StackEngine.SnapshotStack();
            int burst = thisHit;
            foreach (var c in OwnLivingUnits(core, me))
            {
                if (c == null || !c.HasAttackAbility()) continue;
                if (stack.Any(e => e != null && e.IsAttackDeclaration && ReferenceEquals(e.Source, c)))
                    continue; // 已在栈上的这次攻击已计入 thisHit
                if (!c.IsTapped() && core.CombatSystem.CanDeclareAttack(c, me))
                    burst += EffectiveDamage(core, c);
            }
            foreach (var c in HandCards(core, me))
            {
                if (ReferenceEquals(c, exclude)) continue;
                if (IsDirectDamageSpell(c) && GameActions.CanAffordCast(core, me, c, 0))
                    burst += DirectDamageValue(c);
            }
            return burst >= opp.Life;
        }


        /// <summary>
        /// 地牌投放优先级：血偿等直伤打不出去时直接当地牌——
        /// 0 费白板地，优先占坑（PlayLands 只对「打不出的卡」调用本口）。
        /// </summary>
        public override float LandPriority(GameCore core, Player me, Card card, Func<Card, float> totalCost)
            => IsDirectDamageSpell(card)
                ? 10000f
                : base.LandPriority(core, me, card, totalCost);

        /// <summary>
        /// AI 目标排序（2026-11-03 成本改弃牌后）：
        /// · 代价=弃一张己方手牌（隐蔽区，引擎自动随机选牌）→ 不排序；
        ///   若代价变成有域的原子，由 <see cref="DispatchOrder"/> 的「对手选」先接管，本口不重复插手；
        /// · 血偿直伤效果 → 直接打脸（角色优先，不可指才回落敌方生物）；
        /// · 加攻/连击 → 见 <see cref="BuffScore"/>（有阻拦给高生命 / 无连击分散 / 连击爆发集中）；
        /// · 激励（重置）→ 先挑已横置、有效伤害最高的攻击手。
        /// </summary>
        public override List<Entity> OrderTargetCandidates(GameCore core, Player me, TargetSelectionRequest req)
        {
            var list = req?.Candidates;
            if (list == null || list.Count <= 1) return list;

            if (req.IsCostTarget) return list; // 代价弃牌：随机，无目标可排

            if (req.PromptAtom == AtomicEffectType.Untap)
                return OrderUntapTargets(core, me, list);

            if (req.PromptAtom == AtomicEffectType.AddPowerUp
                || req.PromptAtom == AtomicEffectType.ModifyPower
                || req.PromptAtom == AtomicEffectType.GrantDoubleStrike)
                return OrderBuffTargets(core, me, list);

            // 直伤效果（血偿）：直接打脸——角色优先；角色不可指（帷幕/潜行）才回落生物。
            if (req.PromptAtom == AtomicEffectType.DealDamage)
                return OrderFaceTargets(core, me, list);

            return list;
        }

        /// <summary>有益目标（SimpleAI 逐原子预选口）：与 OrderTargetCandidates 的加攻/连击同口径。</summary>
        public override IEnumerable<Entity> OrderBeneficialTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
            => OrderBuffTargets(core, me, candidates?.ToList() ?? new List<Entity>());

        /// <summary>有害目标排序：红套直伤=打脸——角色优先，角色不可指才回落生物威胁降序。</summary>
        public override IEnumerable<Entity> OrderHarmfulTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
        {
            var list = candidates?.ToList() ?? new List<Entity>();
            var opp = me?.Opponent;
            if (opp != null && list.Contains(opp)) return OrderFaceTargets(core, me, list);
            return base.OrderHarmfulTargets(core, me, list);
        }

        public override Entity PickAttackTarget(GameCore core, Player me, Card unit, Player opp)
        {
            var combat = core.CombatSystem;

            // ① 全队斩杀（2026-11-02 用户定案：连击=双倍伤害口径）：这一波够杀 → 全队打脸
            if (CanLethalThisTurn(core, me, opp) && combat.CanAttackTarget(unit, opp, me)) return opp;

            // ② 单兵致死（有效伤害含连击）
            if (EffectiveDamage(core, unit) >= opp.Life && combat.CanAttackTarget(unit, opp, me)) return opp;

            // ③ 正收益解场无条件做（2026-11-01：红也先解场——赚的换子优先于打脸）
            if (TryFindBestTrade(core, me, unit, opp, out var trade) && trade.Gain > 0f)
                return trade.Target;

            // ④ 不赚的换子让位给打脸（失血速度就是胜利条件；守卫拦截由引擎响应窗口承担）
            return combat.CanAttackTarget(unit, opp, me) ? opp : null;
        }

        // ======================================== 红快攻专用（v5 2026-11-02） ========================================

        /// <summary>目标打分：加攻/连击该给谁。
        /// 爆发模式（手里有付得起的连击 + 对手无阻拦）→ 集中：越强越堆；
        /// 对手有可阻拦生物 → 高生命优先（1 点伤害点不掉）；
        /// 其余（只有 +攻）→ 分散：先给还没被加过攻击的单位。
        /// 只带守卫的单位（不可攻击）永远排最后——加攻/连击对它是浪费。</summary>
        private static float BuffScore(GameCore core, Player me, Entity e, bool burst, bool blockers)
        {
            if (!(e is Card c) || !c.IsAlive || !ReferenceEquals(c.GetController(), me)) return -10000f;
            if (!c.HasAttackAbility()) return -5000f;
            float s = 0f;
            if (burst) s += PowerOf(core, c) * 10f;
            else if (blockers) s += c.GetLife() * 10f;
            else s -= c.GetCounterCount(CounterRules.PowerUpCounter) * 30f;
            s += c.GetLife() * 0.5f;          // 次级：活得久更稳
            s += PowerOf(core, c) * 0.1f;
            return s;
        }

        private static List<Entity> OrderBuffTargets(GameCore core, Player me, List<Entity> list)
        {
            bool burst = BurstMode(core, me);
            bool blockers = EnemyHasBlocker(core, me);
            var attacker = PendingAttacker(core, me);
            return list.OrderByDescending(e =>
                    BuffScore(core, me, e, burst, blockers)
                    + (attacker != null && ReferenceEquals(e, attacker) ? 10000f : 0f)) // 连锁中：先把连击/加攻贴给正在揍人的那只
                .ToList();
        }

        /// <summary>栈上属于我的攻击宣言的攻击者（响应链里加攻/连击的落点）。</summary>
        private static Card PendingAttacker(GameCore core, Player me)
        {
            var stack = core?.StackEngine?.SnapshotStack();
            if (stack == null) return null;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                var e = stack[i];
                if (e != null && e.IsAttackDeclaration && ReferenceEquals(e.Controller, me))
                    return e.Source as Card;
            }
            return null;
        }

        /// <summary>栈上拦我这一击的守卫（响应链里直伤开路的落点）。</summary>
        private static Card PendingGuarder(GameCore core, Player me)
        {
            var stack = core?.StackEngine?.SnapshotStack();
            if (stack == null) return null;
            EffectInstance attack = null;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                var e = stack[i];
                if (e != null && e.IsAttackDeclaration && ReferenceEquals(e.Controller, me)) { attack = e; break; }
            }
            if (attack == null) return null;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                var e = stack[i];
                if (e != null && e.IsGuardDeclaration && ReferenceEquals(e.InterceptedAttack, attack))
                    return e.Source as Card;
            }
            return null;
        }

        /// <summary>激励（重置）目标：已横置的攻击手优先，其次有效伤害最高。</summary>
        private static List<Entity> OrderUntapTargets(GameCore core, Player me, List<Entity> list)
            => list.OrderByDescending(e =>
                    e is Card c && c.IsAlive && ReferenceEquals(c.GetController(), me) && c.HasAttackAbility()
                        ? (c.IsTapped() ? 1000 : 0) + EffectiveDamage(core, c)
                        : -1)
                .ToList();

        /// <summary>血偿直伤：守卫拦截中先打守卫（开路），否则角色优先，其余按敌方攻击力降序。</summary>
        private static List<Entity> OrderFaceTargets(GameCore core, Player me, List<Entity> list)
        {
            var guarder = PendingGuarder(core, me);
            return list.OrderByDescending(e =>
                    guarder != null && ReferenceEquals(e, guarder) ? 2
                    : e is Player && ReferenceEquals(e, me.Opponent) ? 1
                    : 0)
                .ThenByDescending(e => e is Card c && c.GetController() == me.Opponent && c.IsAlive
                    ? PowerOf(core, c) : int.MinValue)
                .ToList();
        }

        // ---------------- 小件 ----------------

        private static CardData DataOf(Card card) => (card as CardWrapper)?.GetData();

        private static bool IsCreatureCard(Card card) => DataOf(card)?.Supertype == Cardtype.Creature;

        /// <summary>只带守卫：生物 + NoAttack（火灵·守）——可拦截、不能攻击。</summary>
        private static bool IsGuardOnly(Card card)
        {
            var d = DataOf(card);
            return d != null && d.Supertype == Cardtype.Creature && d.NoAttack;
        }

        /// <summary>赋予连击法术（主序列含 GrantDoubleStrike）。</summary>
        private static bool IsDoubleStrikeSpell(Card card)
            => DataOf(card)?.Supertype == Cardtype.Spell
               && HasAtomAnywhere(card, AtomicEffectType.GrantDoubleStrike);

        /// <summary>直伤法术（主序列含 DealDamage 且不是连击）。</summary>
        private static bool IsDirectDamageSpell(Card card)
            => DataOf(card)?.Supertype == Cardtype.Spell
               && HasAtomAnywhere(card, AtomicEffectType.DealDamage)
               && !HasAtomAnywhere(card, AtomicEffectType.GrantDoubleStrike);

        private static int PowerOf(GameCore core, Card c) => core.LayerEngine.CalculatePower(c);

        private static bool HasDoubleStrike(Card c) => c.HasKeyword(KeywordRules.DoubleStrike);

        /// <summary>有效伤害（2026-11-02 斩杀口径）：攻击力 ×（连击 ? 2 : 1）。</summary>
        private static int EffectiveDamage(GameCore core, Card c)
            => PowerOf(core, c) * (HasDoubleStrike(c) ? 2 : 1);

        /// <summary>直伤法术的实际伤害值（主序列 DealDamage 的 value——血偿=6）。</summary>
        private static int DirectDamageValue(Card card)
        {
            if (!(card is CardWrapper w)) return 0;
            var data = w.GetData();
            if (data?.Effects == null) return 0;
            foreach (var def in CardEffectConverter.ConvertAll(data.Effects, data.ID))
            {
                if (def == null) continue;
                foreach (var atom in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0))
                    if (atom != null && atom.Type == AtomicEffectType.DealDamage) return Math.Max(1, atom.Value);
                if (def.Steps != null && def.Steps.Count > 0) continue;
                foreach (var atom in def.Effects)
                    if (atom != null && atom.Type == AtomicEffectType.DealDamage) return Math.Max(1, atom.Value);
            }
            return 0;
        }

        private static List<Card> OwnLivingUnits(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Where(c => c != null && c.IsAlive && c.IsLivingUnit()).ToList();

        private static List<Card> HandCards(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>()).Where(c => c != null).ToList();

        /// <summary>对手是否有可阻拦的生物（守卫能力：生物且非 NoGuard）。</summary>
        private static bool EnemyHasBlocker(GameCore core, Player me)
        {
            var opp = me?.Opponent;
            if (opp == null) return false;
            foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && c.IsLivingUnit() && c.HasGuardAbility()) return true;
            return false;
        }

        /// <summary>大哥（被养大的 1/3 火种）= 己方战场生命最高的存活单位；同生命取攻击高。
        /// 只认生命 ≥3——1/3 火种没养起来之前不算。</summary>
        private static Card BigBrother(GameCore core, Player me)
        {
            Card best = null;
            int bestLife = 2; // 生命 ≥3 才算大哥
            int bestPow = -1;
            foreach (var c in OwnLivingUnits(core, me))
            {
                int life = c.GetLife();
                int pow = PowerOf(core, c);
                if (life > bestLife || (life == bestLife && pow > bestPow))
                {
                    best = c; bestLife = life; bestPow = pow;
                }
            }
            return best;
        }

        private static bool HasBigBrother(GameCore core, Player me) => BigBrother(core, me) != null;

        /// <summary>爆发载体（连击的落点）：可攻击、攻击力最高（同攻取生命高）。</summary>
        private static Card BurstCarrier(GameCore core, Player me)
        {
            Card best = null;
            int bestPow = -1, bestLife = -1;
            foreach (var c in OwnLivingUnits(core, me))
            {
                if (!c.HasAttackAbility()) continue;
                int pow = PowerOf(core, c);
                int life = c.GetLife();
                if (pow > bestPow || (pow == bestPow && life > bestLife))
                {
                    best = c; bestPow = pow; bestLife = life;
                }
            }
            return best;
        }

        /// <summary>爆发模式（用户 2026-11-02 口径）：手里有付得起的「赋予连击」，且对手没有可阻拦的生物
        /// → 加攻集中到同一只做连击爆发；否则按「分散 / 高生命」口径走。</summary>
        private static bool BurstMode(GameCore core, Player me)
        {
            if (EnemyHasBlocker(core, me)) return false;
            if (!(me?.Opponent is Player opp) || opp.Life <= 0) return false;
            foreach (var c in HandCards(core, me))
                if (IsDoubleStrikeSpell(c) && GameActions.CanAffordCast(core, me, c, 0)) return true;
            return false;
        }

        /// <summary>值得用激励重置的单位：已横置、可攻击、有效伤害 ≥4（被养过或带连击）。</summary>
        private static Card BestUntapTarget(GameCore core, Player me)
        {
            Card best = null;
            int bestDmg = 3; // 有效伤害 ≥4 才值得再打一波
            foreach (var c in OwnLivingUnits(core, me))
            {
                if (!c.IsTapped() || !c.HasAttackAbility()) continue;
                int dmg = EffectiveDamage(core, c);
                if (dmg > bestDmg) { best = c; bestDmg = dmg; }
            }
            return best;
        }

        /// <summary>本回合能否斩杀（连击翻倍口径）：场上未横置攻击手的有效伤害合计
        /// + 手里付得起的直伤，≥ 对手生命。</summary>
        private static bool CanLethalThisTurn(GameCore core, Player me, Player opp)
        {
            if (opp == null) return false;
            int burst = 0;
            foreach (var c in OwnLivingUnits(core, me))
                if (c.HasAttackAbility() && core.CombatSystem.CanDeclareAttack(c, me))
                    burst += EffectiveDamage(core, c);
            foreach (var c in HandCards(core, me))
                if (IsDirectDamageSpell(c) && GameActions.CanAffordCast(core, me, c, 0))
                    burst += DirectDamageValue(c);
            return burst >= opp.Life;
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
        private const string NameNewborn = "培育";   // 永久+1生命；地>9 门再+1攻击（2026-11-02 更名自「新生」）
        private const string NameMycelium = "菌丝";  // 再生；资敌 6 护甲（2026-11-02 改版）
        private const string NameGuard = "林地守卫"; // 0/6 守护（Guardian 印刷）：保大哥/角色的墙
        private const string NameFangs = "獠牙";     // 0/6 倒计时3→狩猎：这套牌唯一的进攻引擎
        private const string NameTaunt = "雾障";     // 0/6 倒计时3→帷幕：倒计时第一优先（2026-11-03）
        private const string NameLivingArmor = "活体护甲"; // 绿3 法术：己方角色获得=目标生命 的护甲（保命牌）
        private const int ArmorTargetLife = 5;            // 「大生物」门槛：生命≥5 才值得贴活体护甲
        /// <summary>成长停手线（2026-11-03 用户定案：**9 费还允许再成长一次**）——9 地打一发 +2 → 11 地，
        /// 之后才停手、把成长当土地。</summary>
        private const int GrowthStopLands = 11;
        private const string NameDew = "晨露";       // 0/6 倒计时1 归零回 2 点生命（持续奶）

        /// <summary>倒计时支援卡（帷幕/溅射/警戒/狩猎的载体，均 0/6 NoAttack、倒计时3）。</summary>
        private static readonly HashSet<string> SupportNames =
            new HashSet<string> { "雾障", "棘爆", "噬血藤", "獠牙" };

        /// <summary>保命阈值：生命≤7（低血时守卫额外置顶 / 守护改写目标转角色）。</summary>
        private const int LowLife = 7;
        /// <summary>守护「比较危险」阈值：生命≤此值时，守护绑定目标改为角色（用户 2026-11-03 口径）。</summary>
        private const int GuardianRoleLife = 10;
        /// <summary>绿地目标：5 绿＝守卫的绿费（灰1绿5）——绿地不够 5，守卫根本下不去。</summary>
        private const int GreenLandTarget = 5;
        /// <summary>门条件（2026-11-03 用户定案：门 = **地 ≥9**）：gateOpen = 地 &gt; 8。
        /// 注意原子表行「操控地数量≥9」沿用了旧 GateId 字符串 LandsGt9（BranchConditionEvaluator 同口径 ≥9）。</summary>
        private const int GateLands = 8;
        /// <summary>技能（成长）优先于出牌的回合上限：9 地之前先把地铺起来（与门条件解耦）。</summary>
        private const int SkillLands = 9;
        /// <summary>地牌计划总量：铺到 10 块封顶（成长每次还会再拉一块）。</summary>
        private const int LandPlanTotal = 10;

        public override string Key => "green";
        public override string DisplayName => "绿慢速";

        // ==================== 技能：回收（2026-11-03 改版） ====================

        /// <summary>技能=**回收 1 张**（绿3）：坟场有货且付得起就发（2026-11-03 用户定案——绿套的
        /// 引擎是「回收活体护甲 → 0 费再贴 +6 甲」的叠甲循环，不能只当余费牌）。目标顺序见
        /// OrderTargetCandidates 的 RecoverToHand 分支：活体护甲 &gt; 培育 &gt; 其他。</summary>
        public override bool WantHeroSkill(GameCore core, Player me)
        {
            if (!base.WantHeroSkill(core, me)) return false;
            if (GraveyardCount(core, me) == 0) return false;      // 回收需要坟场有货
            return AvailableGreen(core, me) >= 3;                 // 技能绿3
        }

        /// <summary>手里「还付得起」的最便宜一张牌要多少费（无=0）。</summary>
        private static int CheapestPlayableCardCost(GameCore core, Player me)
        {
            int best = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (c == null || !GameActions.CanAffordCast(core, me, c, 0)) continue;
                int cost = (int)Math.Ceiling(GameActions.GetCardCost(c, 0).Total - 1e-4f);
                if (cost <= 0) continue;                          // 0 费牌不占钱，不算
                if (best == 0 || cost < best) best = cost;
            }
            return best;
        }

        /// <summary>技能不再抢在出牌前：回收是价值牌，先铺场再拿牌（旧口径是成长 ramp 先手）。</summary>
        public override bool SkillBeforeCards(GameCore core, Player me) => false;

        /// <summary>当前可用的绿（未横置绿地 + 临时钱包绿）——技能留钱判定用。</summary>
        private static int AvailableGreen(GameCore core, Player me)
        {
            int n = 0;
            foreach (var pc in core.ElementPool.GetPooledCards(me))
                if (pc != null && pc.LockedColor == ManaType.Green && !pc.IsTapped) n++;
            var pool = core.ElementPool.GetPool(me);
            if (pool != null) n += pool.TempElements.TryGetValue(ManaType.Green, out var w) ? w : 0;
            return n;
        }

        /// <summary>手里最便宜的绿费生物要多少绿（无=0；用派生费读数，含取整）。</summary>
        private static int CheapestGreenCreatureCost(GameCore core, Player me)
        {
            int best = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                var d = (c as CardWrapper)?.GetData();
                if (d == null || d.Supertype != Cardtype.Creature) continue;
                int g = (int)Math.Ceiling(GameActions.GetCardCost(c)[ManaType.Green] - 1e-4f);
                if (g > 0 && (best == 0 || g < best)) best = g;
            }
            return best;
        }

        // ==================== 出牌优先级 ====================

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            float score = totalCost(card); // 兜底：大费先出
            int lands = TotalLands(core, me);
            bool gateOpen = lands > GateLands;   // 门条件：地 ≥9（GateLands=8 → lands>8）
            bool low = me.Life <= LowLife;
            int guards = CountOnBoard(core, me, NameGuard);
            float survival = low ? 4000f : 0f;   // 保命加权（2026-11-03：成长要让位给保命）

            // ① 守卫：先下一张保护角色（守护改写伤害），之后按需补；低血时置顶
            if (Is(card, NameGuard))
            {
                score += guards == 0 ? 3500f : 400f;   // 守卫先落地（2026-11-03：守卫 > 培育 > 护甲）
                return score + survival;
            }

            // ② 培育（2026-11-03 用户定案：**放弃复杂用法，只喂林地守卫**）：
            //    场上没有林地守卫 → 这张不打（去当土地，手里留 1 张）；有守卫 → 立刻养它，其余单位一律不吃
            if (Is(card, NameNewborn))
            {
                if (guards == 0) return score - 10000f;
                return score + (gateOpen ? 3200f : 2600f) + survival;
            }

            // ③ 倒计时优先级（2026-11-03 用户定案）：**帷幕第一，狩猎第二**——
            //    帷幕（雾障）先落地把对手的效果挡掉，再铺狩猎（獠牙）做输出；其余倒计时随后
            if (Is(card, NameTaunt)) return score + 5000f + survival;
            if (Is(card, NameFangs)) return score + (AnyOwnHunt(core, me) ? 1200f : 4000f) + survival;

            // ④ 成长（主卡组·绿3·+2地）：**9 地之前优先级最高，仅次于保命**（低血时压到 2500，
            //    让守护/墙先上）；**满 9 地之后不再施放**——抽到直接当地牌（ShouldHold/ShouldLand 同口径）
            if (Is(card, NameGrowth))
            {
                if (lands >= GrowthStopLands) return score - 10000f;   // 排序垫底（ShouldHold 会拦）
                return score + (low ? 2500f : 6000f);
            }

            if (card != null && SupportNames.Contains(NameOf(card)))
                return score + (gateOpen ? 900f : 500f) + survival;

            // ⑦ 活体护甲（2026-11-03 用户定案）：**永不主动施放**——它只在响应窗里
            //    「林地守卫即将被一击打死」时贴（见 PickResponse ③）。这里直接垫底，
            //    免得第 2 回合就被当成 0 费牌随手贴给雾障之类的单位。
            if (Is(card, NameLivingArmor))
            {
                // 没有林地守卫 → 不打（会贴错人，战报第 2 回合贴给雾障就是这么来的）；
                // 有守卫 → 正常贴（0 费，护甲=守卫生命，这就是「倒计时落地→+6甲→回收再来」的循环）
                if (OwnGuard(core, me) == null) return score - 10000f;
                return score + (low ? 4000f : 2500f) + survival;
            }

            // ⑤ 菌丝（再生+资敌护甲）：留到对手回合贴受伤单位（ShouldHold 扣着），这里只给低分兜底
            if (Is(card, NameMycelium)) return score + 100f;

            // ⑥ 晨露（0/6 倒计时1 回 2）：便宜的续航，早下
            if (Is(card, NameDew)) return score + 700f + survival;

            return score;
        }

        /// <summary>攒牌闸（2026-11-03 用户定案）：
        /// · 培育——**不再自己回合一次打光**：门没开全留（留给战斗窗口/对手回合反应）；
        ///   门开了也留 1 张备反应，其余按成长打；
        /// · 菌丝——自己回合不打，留到对手回合给受伤单位贴再生；
        /// · **成长——满 9 地之后不再施放**（抽到就当地牌，ShouldLand 同口径）；
        /// · 培育/菌丝场上没单位时不空放。</summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
        {
            bool hold;
            if (Is(card, NameNewborn))
            {
                // 2026-11-03：只喂林地守卫——场上没守卫就憋着（由 ShouldLand 打发去当土地，留 1 张）
                hold = CountOnBoard(core, me, NameGuard) == 0;
            }
            else if (Is(card, NameMycelium))
                // 2026-11-03：**不再等对手回合**——有林地守卫、且它身上还没再生时，有费就贴
                hold = OwnGuardWithoutRegen(core, me) == null;
            else if (Is(card, NameGrowth))
                hold = TotalLands(core, me) >= GrowthStopLands;           // 满 11 地：不打，当地牌
            else if (Is(card, NameLivingArmor))
                hold = OwnGuard(core, me) == null;                        // 有守卫才贴；没守卫留在手里
            else hold = false;

            if (!hold) return false;
            // 2026-11-03 用户定案：**每回合把费用花完**——这张付得起、手里又没有别的牌可打时，
            // 憋着的也打出去。例外：成长（满 11 地当地）、菌丝/活体护甲（硬留，只在响应窗用）。
            if (Is(card, NameGrowth) && TotalLands(core, me) >= GrowthStopLands) return true;

            
            if (!GameActions.CanAffordCast(core, me, card, 0)) return true;
            return HasOtherPlayableCard(core, me, card);
        }

        /// <summary>手里除这张之外，还有没有「不憋着、也付得起」的牌（费用花完判定用）。</summary>
        private static bool HasOtherPlayableCard(GameCore core, Player me, Card except)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (c == null || ReferenceEquals(c, except)) continue;
                if (!GameActions.CanAffordCast(core, me, c, 0)) continue;
                var n = NameOf(c);
                if (n == NameNewborn || n == NameMycelium || n == NameGrowth) continue;  // 这几张本来就憋着
                return true;
            }
            return false;
        }

        /// <summary>己方场上最大的生物生命（无=0）——活体护甲「和大生物一起出」的判据。</summary>
        private static int BestOwnCreatureLife(GameCore core, Player me)
        {
            int best = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
            {
                if (c == null || !c.IsAlive) continue;
                if ((c as CardWrapper)?.GetData()?.Supertype != Cardtype.Creature) continue;
                int life = c.GetLife();
                if (life > best) best = life;
            }
            return best;
        }

        /// <summary>手里还有没有生物牌（决定培育「打不出去」时该不该烧成地——手里有生物就先留着铺场）。</summary>
        private static bool HasCreatureInHand(GameCore core, Player me)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                if (c != null && (c as CardWrapper)?.GetData()?.Supertype == Cardtype.Creature) return true;
            return false;
        }

        /// <summary>现在要不要贴活体护甲（2026-11-03 用户定案）：**憋到自家大生物（生命≥5）落地**；
        /// 唯一例外是对手**场面总攻 ≥ 我方角色生命**（这一波会被斩杀）——那就立刻贴，哪怕目标不是大生物。</summary>
        private static bool WantsArmorNow(GameCore core, Player me)
        {
            if (BestOwnCreatureLife(core, me) >= ArmorTargetLife) return true;
            return OpponentTotalAttack(core, me) >= me.Life;
        }

        /// <summary>对手场面总攻击力（斩杀威胁判定用）。</summary>
        private static int OpponentTotalAttack(GameCore core, Player me)
        {
            int sum = 0;
            foreach (var c in core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield) ?? new List<Card>())
            {
                if (c == null || !c.IsAlive) continue;
                if ((c as CardWrapper)?.GetData()?.Supertype != Cardtype.Creature) continue;
                sum += core.LayerEngine.CalculatePower(c);
            }
            return sum;
        }

        /// <summary>「被永久加强过的生物」判据：身上有永久攻/血指示物（培育加出来的那类）。</summary>
        private static bool IsPermanentBuffed(GameCore core, Card c)
        {
            var d = (c as CardWrapper)?.GetData();
            if (d == null) return false;
            if (c.GetMaxLife() > (d.Life ?? 0)) return true;                       // 生命被永久加强
            return core.LayerEngine.CalculatePower(c) > (d.Power ?? 0);            // 攻击被永久加强
        }

        // ==================== 响应窗（2026-11-03 用户定案） ====================

        /// <summary>
        /// 绿套的两张「留手牌」在对手动作后兑现（用户 2026-11-03 三条）：
        /// ① **培育**：先让攻守宣言落栈，再在窗口里贴——只当 +1 生命能把「被打死」翻成「活下来」
        ///    （对手伤害正好等于我方参战单位生命）时出手，属性在战斗过程中变化换优势；否则不打。
        /// ② **菌丝（再生）**：等到对手回合，给**已受伤**的己方单位贴；优先没有再生的、掉血最多的。
        /// 其余情况一律让过（守卫决策等交内置启发）。
        /// </summary>
        public override ResponseOption PickResponse(GameCore core, Player me, List<ResponseOption> options)
        {
            if (core == null || me == null || options == null || options.Count == 0) return null;
            var stack = core.StackEngine?.SnapshotStack();
            if (stack == null || stack.Count == 0) return null;

            // ① 培育：战斗窗口的「+1 生命救一命」——**只给林地守卫**（2026-11-03 定案）
            var atk = stack.LastOrDefault(e => e != null && e.IsAttackDeclaration);
            if (atk != null)
            {
                var fighter = CombatFighterOf(core, me, stack, atk, out int incoming);
                if (fighter != null && fighter.IsAlive && incoming > 0 && Is(fighter, NameGuard)
                    && fighter.GetLife() <= incoming && fighter.GetLife() + 1 > incoming)
                {
                    var opt = FindHandOption(options, NameNewborn);
                    if (opt != null) return WithTarget(opt, fighter);
                }
            }

            // ② 菌丝（再生）：**只贴林地守卫**，且守卫身上还没有再生
            if (core.TurnEngine.TurnPlayer != me)
            {
                var guardCard = OwnGuardWithoutRegen(core, me);
                if (guardCard != null)
                {
                    var opt = FindHandOption(options, NameMycelium);
                    if (opt != null) return WithTarget(opt, guardCard);
                }
            }

            // （2026-11-03 撤回）：曾尝试「倒计时生物不当盾」，但拦截发生在对手回合、
            // 自己回合开始即解横置，挡刀并不会妨碍出狩猎——反而因为不挡掉血更快（对红 12.1→9.1 回合）。
            // 拦截决策交回内置启发 DefaultAiRespond。

            // ③ 活体护甲（2026-11-03 用户定案）：**只在林地守卫即将受到致命伤时**才贴——
            //    来袭伤害 ≥ 守卫当前生命（这一下会打死它）→ 贴甲，用「等同其生命的护甲」正好扛住
            {
                var guardCard2 = OwnGuard(core, me);
                if (guardCard2 != null && LethalToGuard(core, me, stack, guardCard2))
                {
                    var opt = FindHandOption(options, NameLivingArmor);
                    if (opt != null) return WithTarget(opt, guardCard2);
                }
            }
            return null;
        }

        /// <summary>我方场上第一只林地守卫（无=null）。</summary>
        private static Card OwnGuard(GameCore core, Player me)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && Is(c, NameGuard)) return c;
            return null;
        }

        /// <summary>没有再生关键词的我方林地守卫。</summary>
        private static Card OwnGuardWithoutRegen(GameCore core, Player me)
        {
            var g = OwnGuard(core, me);
            return g != null && !g.HasKeyword(KeywordRules.Regeneration) ? g : null;
        }

        /// <summary>这一下会不会打死我方林地守卫（来袭伤害 ≥ 当前生命）——活体护甲的贴牌时机。</summary>
        private static bool LethalToGuard(GameCore core, Player me, List<EffectInstance> stack, Card guardCard)
        {
            var atk = stack.LastOrDefault(e => e != null && e.IsAttackDeclaration
                                               && ReferenceEquals(e.Controller, me.Opponent));
            if (atk == null) return false;
            var fighter = CombatFighterOf(core, me, stack, atk, out int incoming);
            if (!ReferenceEquals(fighter, guardCard)) return false;
            return incoming > 0 && incoming >= guardCard.GetLife();
        }

        /// <summary>栈上是否有对手的效果以该卡为目标（解场判定）。</summary>
        private static bool EnemyEffectTargets(List<EffectInstance> stack, Player me, Card card)
        {
            foreach (var e in stack)
            {
                if (e?.Targets == null || !ReferenceEquals(e.Controller, me.Opponent)) continue;
                foreach (var t in e.Targets)
                    if (ReferenceEquals(t, card)) return true;
            }
            return false;
        }

        private static ResponseOption FindHandOption(List<ResponseOption> options, string name)
        {
            foreach (var o in options)
                if (o != null && o.Kind == ResponseOption.ResponseKind.HandCard
                    && o.SourceCard != null && Is(o.SourceCard, name))
                    return o;
            return null;
        }

        private static ResponseOption WithTarget(ResponseOption opt, Card target)
        {
            if (opt == null || target == null) return null;
            opt.Targets = new List<Entity> { target };
            return opt;
        }

        /// <summary>当前攻防里我方参战单位（攻方=我方攻击手；守方=我方宣告的守卫，否则被攻击的我方单位）
        /// 与它将要承受的伤害（incoming）。</summary>
        private static Card CombatFighterOf(GameCore core, Player me, List<EffectInstance> stack,
            EffectInstance atk, out int incoming)
        {
            incoming = 0;
            int Pow(Entity e) => e is Card c ? core.LayerEngine.CalculatePower(c) : 0;

            if (ReferenceEquals(atk.Controller, me))
            {
                if (!(atk.Source is Card mine)) return null;
                var guard = stack.LastOrDefault(e => e != null && e.IsGuardDeclaration
                                                     && ReferenceEquals(e.InterceptedAttack, atk));
                incoming = Pow(guard?.Source);
                return mine;
            }

            int attackerPower = Pow(atk?.Source);
            var myGuard = stack.LastOrDefault(e => e != null && e.IsGuardDeclaration
                                                   && ReferenceEquals(e.InterceptedAttack, atk)
                                                   && ReferenceEquals(e.Controller, me));
            if (myGuard?.Source is Card guarder)
            {
                incoming = attackerPower;
                return guarder;
            }
            var victim = (atk?.Targets ?? new List<Entity>()).OfType<Card>()
                .FirstOrDefault(c => c != null && ReferenceEquals(c.GetController(), me));
            if (victim != null)
            {
                incoming = attackerPower;
                return victim;
            }
            return null;
        }

        /// <summary>再生落点：己方已受伤、且还没有再生的单位——掉血多 > 有攻击力 > 守卫。</summary>
        private static Card BestRegenTarget(GameCore core, Player me)
        {
            Card best = null;
            int bestScore = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
            {
                if (c == null || !c.IsAlive || !c.GetController().Equals(me)) continue;
                if (c.HasKeyword(KeywordRules.Regeneration)) continue;
                int missing = c.GetMaxLife() - c.GetLife();
                if (missing <= 0) continue;
                int score = missing * 10 + core.LayerEngine.CalculatePower(c) * 3 + (Is(c, NameGuard) ? 5 : 0);
                if (score > bestScore) { best = c; bestScore = score; }
            }
            return best;
        }

        // 苏醒策略段（WantAwakenCast/AwakenWaive 覆写——缺费才睡别拖慢倒计时）已随
        // 2026-11-02 苏醒机制整链删除（灰身材概念已不存在，与倒计时玩法过近）。

        // ==================== 铺地 ====================

        public override float LandPriority(GameCore core, Player me, Card card, Func<Card, float> totalCost)
        {
            // 2026-11-03 修正：**守卫是场上支柱，绝不当土地垫**——旧口给它 12000 的铺地优先，
            // 结果 6 张 1/11 全被烧成地、场上一只都没有（对红 10 局只落地 1 次）。
            if (Is(card, NameGuard)) return 300f;
            if (GreenLands(core, me) < GreenLandTarget && SupportsColor(card, ManaType.Green)) return 6000f;
            return 1000f;
        }

        public override bool ShouldLand(GameCore core, Player me, Card card)
        {
            // 培育（2026-11-03 用户定案）：**没有林地守卫 → 当土地铺，但手里留 1 张**；
            // 有守卫就留着立刻喂它，不铺地。
            if (Is(card, NameNewborn))
            {
                if (CountOnBoard(core, me, NameGuard) > 0) return false;
                return HandCopies(core, me, NameNewborn) > 1;
            }
            // 成长：9 地之前是 ramp 引擎（不烧）；**满 11 地之后抽到就当地牌**
            if (Is(card, NameGrowth)) return TotalLands(core, me) >= GrowthStopLands;
            // 活体护甲＝0 费叠甲核心，**永远不当土地**（2026-11-03 战报：第一回合就被当土地烧了）
            if (Is(card, NameLivingArmor)) return false;
            // 帷幕/狩猎载体也不烧：它们是进攻与防护主轴（战报里獠牙 10 局只打出 1 张，全被烧成地了）
            if (Is(card, NameTaunt) || Is(card, NameFangs)) return false;
            // 林地守卫＝全场支点，**永远不当土地**（旧口只改了铺地优先级，资格闸仍让它被烧：
            // 战报里 6 张守卫 10 局只落地 2 次——培育/菌丝/护甲全成了废牌）
            if (Is(card, NameGuard)) return false;
            int lands = TotalLands(core, me);
            if (lands >= LandPlanTotal) return false;    // 10 地封顶
            int copies = HandCopies(core, me, NameOf(card));

            // 2026-11-03 用户定案：**全绿卡组不锁色，缺的就是地上限本身**——
            // 前 3 回合把地铺到 3（不够 3 块绿地时成长/培育都打不出，ramp 直接卡死）。
            // 这条优先于「每张留 1 张」：宁可先烧一张墙换启动，也要在第二个回合摸到 3 地。
            if (lands < 3) return true;

            // 3 地之后：每张留 1 张打出去，只铺重复副本（避免把墙全烧成地、场上一个单位都没有）
            return copies > 1;
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

            // 技能「流失 1 生命」= 余费解场（2026-11-03 用户定案）：先打**能直接打死**的对方单位，
            // 其次按威胁（攻击力）挑对方单位，最后才轮到对方角色；己方单位垫底。
            if (atom == AtomicEffectType.LifeLoss)
                return list.OrderByDescending(e =>
                        e is Card c && c.IsAlive && ReferenceEquals(c.GetController(), me.Opponent)
                            ? (c.GetLife() <= 1 ? 2000f : 1000f) + core.LayerEngine.CalculatePower(c)
                            : e is Player p && ReferenceEquals(p, me.Opponent) ? 100f
                            : 0f)
                    .ToList();

            // 永久属性（2026-11-03 用户定案）：优先**已有再生**的生物（再生按最大生命回满，养它最赚），
            // 其次正在打的这只（战斗窗口里贴），再次守卫，最后獠牙/角色
            if (atom == AtomicEffectType.AddPermanentPowerUp || atom == AtomicEffectType.AddPermanentLifeUp)
            {
                // 2026-11-03 用户定案：**培育只喂林地守卫**——守卫永远第一，其余单位只在没守卫时才轮到
                var fighter = PendingFighter(core, me);
                return list.OrderByDescending(e =>
                        e is Card cq && Is(cq, NameGuard) ? 10
                        : e is Card cb && IsPermanentBuffed(core, cb) ? 2
                        : ReferenceEquals(e, fighter) ? 1
                        : 0)
                    .ThenByDescending(e => e is Card c4 && c4.GetController() == me ? c4.GetMaxLife() : 0)
                    .ToList();
            }

            // 复生：先贴角色（用户口径：可以提前贴，优先级不高）
            if (atom == AtomicEffectType.GrantReborn)
                return list.OrderByDescending(e => e is Player)
                           .ThenByDescending(e => e is Card c3 && Is(c3, NameGuard))
                           .ToList();

            // 活体护甲（2026-11-03 用户定案）：**只对林地守卫用**——守卫第一，其他单位垫底
            if (atom == AtomicEffectType.LivingArmor)
                return list.OrderByDescending(e => e is Card cg && Is(cg, NameGuard) ? 100
                        : e is Card c4 && c4.GetController() == me ? c4.GetLife() : int.MinValue)
                           .ToList();

            // 再生（菌丝）：**只贴林地守卫**（2026-11-03 用户定案），其它单位垫底
            if (atom == AtomicEffectType.GrantRegeneration)
                return list.OrderByDescending(e => e is Card cq && Is(cq, NameGuard) ? 100
                        : e is Card c5 && c5.GetController() == me ? c5.GetMaxLife() : int.MinValue)
                           .ToList();

            // 守护绑定（2026-11-03 用户定案：生命安全第一）：
            //   不危险 → 首个守护贴给**狩猎载体**（獠牙／已带狩猎的那只）护住进攻引擎；
            //   比较危险（生命 ≤10）→ 贴给角色。
            // 守护/守卫（2026-11-03 用户定案）：**优先绑角色**——对手的高攻爆发打的是角色，
            // 守卫拦的就是这条线（生命危险时更是角色优先）；对手没场面时才退而保护狩猎载体/獠牙
            if (atom == AtomicEffectType.Guard || atom == AtomicEffectType.GrantGuardian)
            {
                bool enemyHasBodies = (core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield) ?? new List<Card>())
                    .Any(c => c != null && c.IsAlive);
                if (me.Life <= GuardianRoleLife || enemyHasBodies)
                    return list.OrderByDescending(e => e is Player)
                               .ThenByDescending(e => e is Card cHunt0 && cHunt0.HasKeyword(KeywordRules.Hunt))
                               .ToList();
                return list.OrderByDescending(e => e is Card cHunt && cHunt.HasKeyword(KeywordRules.Hunt) ? 3
                        : e is Card cFang && Is(cFang, NameFangs) ? 2
                        : e is Card cOwn && ReferenceEquals(cOwn.GetController(), me) ? 1
                        : 0)
                    .ThenByDescending(e => e is Player)
                    .ToList();
            }

            // 护甲（资敌代价等）：先给角色（角色死了就输了），角色已满甲才给单位
            if (atom == AtomicEffectType.AddArmor)
                return list.OrderByDescending(e => e is Player)
                           .ThenByDescending(e => e is Card c10 && c10.GetController() == me ? (c10.GetMaxLife() - c10.GetLife()) : int.MinValue)
                           .ToList();

            // 回血（晨露等）：给掉血最多的己方（含角色）
            if (atom == AtomicEffectType.Heal)
                return list.OrderByDescending(e => MaxLifeOf(e) - LifeOf(e)).ToList();

            // 倒计时奖励关键词（2026-11-03 用户定案：狩猎优先——没有狩猎这套牌没有进攻性）：
            // 先挂**攻击力最高**的那只（被培育养大的大哥），再挂已带狩猎的，最后按生命。
            if (atom == AtomicEffectType.GrantTaunt || atom == AtomicEffectType.GrantSplash
                || atom == AtomicEffectType.GrantVigilance || atom == AtomicEffectType.GrantHunt)
                return list.OrderByDescending(e => e is Card cPow && cPow.GetController() == me
                        ? core.LayerEngine.CalculatePower(cPow) : -1)
                           .ThenByDescending(e => e is Card cHunt2 && cHunt2.HasKeyword(KeywordRules.Hunt))
                           .ThenByDescending(e => e is Card c6 && c6.GetController() == me ? c6.GetLife() : 0)
                           .ToList();

            // 回收：拿回收（新生）本身，保持引擎不断；手里新生已 >2 才换别的卡补多样性
            if (atom == AtomicEffectType.RecoverToHand)
            {
                // 2026-11-03 用户定案（回收优先级）：
                //  ① **被再生强化的林地守卫**（带再生的守卫是全场最硬的支点，先把它拿回来）
                //  ② 其余看对面场面：压过来的场面（总攻高/人多）→ 回收活体护甲补甲；
                //     对面没压力 → 回收培育继续养。
                int handArmor = HandCopies(core, me, NameLivingArmor);
                int handNewborn = HandCopies(core, me, NameNewborn);
                int enemyBodies = (core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield) ?? new List<Card>())
                    .Count(c => c != null && c.IsAlive);
                bool defenseMode = OpponentTotalAttack(core, me) >= 6 || enemyBodies >= 3;
                return list.OrderByDescending(e =>
                           {
                               if (e is Card g && Is(g, NameGuard) && g.HasKeyword(KeywordRules.Regeneration)) return 10;
                               if (e is Card cA && Is(cA, NameLivingArmor))
                                   return defenseMode ? 7 : (handArmor <= 1 ? 6 : 3);
                               if (e is Card c8 && Is(c8, NameNewborn))
                                   return defenseMode ? 2 : (handNewborn <= 2 ? 5 : 4);
                               return 0;
                           })
                           .ThenByDescending(e => e is Card c10
                               ? c10.GetMaxLife() + 2 * core.LayerEngine.CalculatePower(c10)
                               : 0)
                           .ToList();
            }

            return list;
        }

        // ==================== 绿套小工具 ====================

        private static string NameOf(Card card) => (card as CardWrapper)?.GetData()?.CardName;

        private static bool Is(Card card, string name) => card != null && NameOf(card) == name;

        /// <summary>己方场上是否已有狩猎（有的话 +1 攻能立刻变成输出）。</summary>
        private static bool AnyOwnHunt(GameCore core, Player me)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && c.HasKeyword(KeywordRules.Hunt)) return true;
            return false;
        }

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

        /// <summary>当前攻防里我方参战单位（供战斗窗口的加属性落点用；无战斗=null）。</summary>
        private static Card PendingFighter(GameCore core, Player me)
        {
            var stack = core?.StackEngine?.SnapshotStack();
            if (stack == null || stack.Count == 0) return null;
            var atk = stack.LastOrDefault(e => e != null && e.IsAttackDeclaration);
            return atk == null ? null : CombatFighterOf(core, me, stack, atk, out _);
        }

        /// <summary>己方墓地卡数（培育/菌丝 的回收燃料）。</summary>
        private static int GraveyardCount(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>()).Count();

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
                    TargetSelectionService.AiOrder = req => AiStrategy.DispatchOrder(this, core, req?.Chooser ?? me, req);
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
                TargetSelectionService.AiOrder = req => AiStrategy.DispatchOrder(this, core, req?.Chooser ?? me, req);
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
