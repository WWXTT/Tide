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
        // ======================================== G6 参照族数据谓词（2026-11-08 收口） ========================================
        /// <summary>表行参照族判定（数据信号=RefDomain 列×极性——取代枚举点名；polaritySign<0 查
        /// 参照直伤行、>0 查参照贴附行）。</summary>
        protected static bool IsReferenceRow(AtomicEffectType type, float polaritySign)
        {
            var row = CardCore.Attribute.AtomicEffectTable.GetByType(type);
            if (row == null || string.IsNullOrEmpty(row.RefDomain)) return false;
            return polaritySign < 0f ? row.Polarity < 0f : row.Polarity > 0f;
        }

        /// <summary>卡级分段原子扫描：任一主序列原子 ReferenceAxis≠null；negative=true 再要求
        /// 负极性（参照直伤）——HasAtomAnywhere 的数据驱动姊妹口（G6 收口）。</summary>
        protected static bool HasSegmentedAtom(Card card, bool negative)
        {
            if (!(card is CardWrapper w)) return false;
            var data = w.GetData();
            if (data?.Effects == null) return false;
            foreach (var def in CardEffectConverter.ConvertAll(data.Effects, data.ID))
            {
                if (def == null) continue;
                foreach (var atom in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0))
                    if (atom != null && atom.ReferenceAxis != null && (!negative || atom.Polarity < 0f)) return true;
                foreach (var atom in def.Effects)
                    if (atom != null && atom.ReferenceAxis != null && (!negative || atom.Polarity < 0f)) return true;
            }
            return false;
        }

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
        /// 先手一波（2026-11-04 交替定案）：主阶段出牌循环**之前**先打一轮——把 AI 回合从
        /// 「铺满再打」改成「打—铺—打」。默认 false（其余策略行为不变）。
        /// 只影响动作顺序，不改任何规则校验；攻击资格仍由引擎权威判定。
        /// </summary>
        public virtual bool WantEarlyCombat(GameCore core, Player me) => false;

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

        /// <summary>出牌目标接管（2026-11-04）：null=不接管（回落通用择优）；空表=本次不满足，别打。</summary>
        public virtual List<Entity> PickSpellTargets(GameCore core, Player me, Card card, int modeIndex) => null;

        /// <summary>攻击前一拍（2026-11-04）：返回要「随这一击一起打出去」的牌（null=不打）。
        /// 由 SimpleAI.DoCombatAsync 在宣言攻击前调用——「攻击的同时使用法术」只是操作顺序。</summary>
        public virtual Card PickAttackTimeSpell(GameCore core, Player me, Card attacker, Entity target,
            List<Entity> outTargets) => null;

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

}
