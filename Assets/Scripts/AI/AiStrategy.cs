using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI
{
    /// <summary>
    /// AI 策略（策略模式，2026-09-21）：SimpleAI 的决策偏好按卡组主题分化。
    /// 基类 GeneralAiStrategy = 原 SimpleAI 通用行为（解场优先有害目标 / 己方优先有益目标 /
    /// 清场优先攻击），三主题策略只覆写分化点：
    /// · 红快攻（RedAggroStrategy）——铺场优先、激励/加攻指向攻击力最高己方生物、打脸优先；
    /// · 绿慢速（GreenRampStrategy）——回血/攒费优先、大生物终结、治疗指向受伤最重己方、只打优换；
    /// · 蓝控制（BlueControlStrategy）——指示物/解场优先、抽牌续资源、攻击清小怪（中等换小）。
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
                        if (t == "ThemeRed") red++;
                        else if (t == "ThemeGreen") green++;
                        else if (t == "ThemeBlue") blue++;
                    }
                }
                if (red > green && red > blue) return RedAggro;
                if (green > red && green > blue) return GreenRamp;
                if (blue > red && blue > green) return BlueControl;
            }
            var total = new Dictionary<int, float>();
            if (deck != null)
                foreach (var c in deck)
                    foreach (var kv in c.Cost ?? new Dictionary<int, float>())
                        total[kv.Key] = total.GetValueOrDefault(kv.Key) + kv.Value;
            return ByMainColor(total);
        }

        // ======================================== 钩子（默认=原 SimpleAI 行为） ========================================

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
        /// 攻击目标决策（null=该单位本回合不攻击）。默认=清场优先（原 PickAttackTarget）：
        /// 打得死的对方随从先清——"自己也存活"的优换严格优先于换子，同档内挑威胁最高的；
        /// 无击杀机会才打脸；兜底任意可指定目标。
        /// </summary>
        public virtual Entity PickAttackTarget(GameCore core, Player me, Card unit, Player opp)
        {
            var combat = core.CombatSystem;
            var oppField = core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>();

            int myPower = core.LayerEngine.CalculatePower(unit);
            int myLife = unit.GetLife();
            Card pick = null;
            bool pickSurvives = false;
            int pickThreat = int.MinValue;
            foreach (var enemy in oppField)
            {
                if (!enemy.IsAlive || !combat.CanAttackTarget(unit, enemy, opp)) continue;
                if (myPower < enemy.GetLife()) continue; // 打不死——不白送，交还打脸

                int threat = core.LayerEngine.CalculatePower(enemy);
                // 反击资格（定案）：已横置的目标只能挨打不反击 → 恒优换；未横置按反击力量判断
                bool iSurvive = enemy.IsTapped() || threat < myLife;
                if (pick == null || (iSurvive && !pickSurvives)
                    || (iSurvive == pickSurvives && threat > pickThreat))
                {
                    pick = enemy;
                    pickSurvives = iSurvive;
                    pickThreat = threat;
                }
            }
            if (pick != null) return pick;

            if (combat.CanAttackTarget(unit, opp, opp.Opponent)) return opp;

            foreach (var enemy in oppField) // 突袭（无冲锋）不能攻玩家等受限情形的兜底：只打随从
            {
                if (combat.CanAttackTarget(unit, enemy, opp.Opponent)) return enemy;
            }
            return null;
        }

        /// <summary>英雄技能是否发动。默认=原 UseHeroSkill 条件：红绿基础版需己方生物，蓝色恒可。</summary>
        public virtual bool WantHeroSkill(GameCore core, Player me, HeroSkillId skill, bool upgraded)
        {
            bool needCreature = skill == HeroSkillId.GreenCultivate
                                || (skill == HeroSkillId.RedFrenzy && !upgraded);
            return !needCreature || core.ZoneManager.GetCards(me, Zone.Battlefield).Any();
        }

        // ======================================== 共用小工具（策略实现用） ========================================

        /// <summary>实体当前生命（EntityEffectExtensions.GetLife——含光环加成，Player/Card 同口径）。</summary>
        protected static int LifeOf(Entity e) => e != null ? e.GetLife() : 0;

        /// <summary>实体最大生命（GetMaxLife——含光环加成）。</summary>
        protected static int MaxLifeOf(Entity e) => e != null ? e.GetMaxLife() : 0;

        /// <summary>卡的第一个非激活式效果原子里是否含指定类型（主序列含抉择全模式）。</summary>
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
    /// 攻击打脸优先（致死斩杀 &gt; 直取角色 &gt; 清场兜底——守卫拦截由引擎承担）。
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
            if (isCreature) score += 200f; // 铺场优先：生物先出

            // 激励/加攻：有己方生物在场才有价值（指向攻击力最高者见 OrderBeneficialTargets）
            if (core.ZoneManager.GetCards(me, Zone.Battlefield).Any()
                && CardHasAtom(core, card, AtomicEffectType.Untap, AtomicEffectType.AddPowerUp,
                               AtomicEffectType.ModifyPower))
                score += 120f;

            // 直伤：对手进入斩杀窗口时提到最高（8 血以下每点直伤都可能是终点）
            var opp = me.Opponent;
            if (opp != null && opp.Life <= 8 && CardHasAtom(core, card, AtomicEffectType.DealDamage))
                score += 600f;

            score -= totalCost(card) * 5f; // 低费先出：快攻曲线靠前
            return score;
        }

        public override IEnumerable<Entity> OrderBeneficialTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
            => candidates // 激励/加攻 → 攻击力最高的己方生物（多次攻击的载体）
                .OrderByDescending(c => c is Card cd && cd.GetController() == me && cd.IsAlive)
                .ThenByDescending(c => c is Card cd2 && cd2.GetController() == me
                    ? core.LayerEngine.CalculatePower(cd2) : int.MinValue)
                .ThenByDescending(c => ReferenceEquals(c, me));

        public override Entity PickAttackTarget(GameCore core, Player me, Card unit, Player opp)
        {
            var combat = core.CombatSystem;
            int myPower = core.LayerEngine.CalculatePower(unit);

            // 致死斩杀：本攻 ≥ 对手剩余生命 → 直取
            if (myPower >= opp.Life && combat.CanAttackTarget(unit, opp, opp.Opponent)) return opp;

            // 快攻口径：能打脸就打脸（失血速度就是胜利条件；守卫拦截由引擎响应窗口承担）
            if (combat.CanAttackTarget(unit, opp, opp.Opponent)) return opp;

            return base.PickAttackTarget(core, me, unit, opp); // 被拦时回落清场档
        }
    }

    /// <summary>
    /// 绿慢速：恢复生命/护甲存活，等待休眠或积攒费用使用大生物，碾压 AOE 逆转场面。
    /// 低血量时回血/护甲最高优先；攒费与大生物（6 费+）优先；治疗/护甲指向受伤最重己方（含角色）；
    /// 攻击只打优换（自己也存活的击杀）或致命斩杀——不白白送掉防守生物。
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

            // 攒费/休眠（苏醒=灰费转沉睡攒大生物；光合/采掘=额外元素）
            if (CardHasAtom(core, card, AtomicEffectType.Sleep, AtomicEffectType.AdditionalEnergy))
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
            int myPower = core.LayerEngine.CalculatePower(unit);
            int myLife = unit.GetLife();

            // 致命斩杀照打
            if (myPower >= opp.Life && combat.CanAttackTarget(unit, opp, opp.Opponent)) return opp;

            // 只打优换：击杀且自己存活（已横置不反击 / 反击打不死我）——防守方不送生物
            var oppField = core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>();
            Card pick = null;
            int pickThreat = int.MinValue;
            foreach (var enemy in oppField)
            {
                if (!enemy.IsAlive || !combat.CanAttackTarget(unit, enemy, opp)) continue;
                if (myPower < enemy.GetLife()) continue;                       // 打不死 → 不打
                int threat = core.LayerEngine.CalculatePower(enemy);
                bool iSurvive = enemy.IsTapped() || threat < myLife;
                if (!iSurvive) continue;                                       // 换子 → 不打（绿生物是防线）
                if (pick == null || threat > pickThreat) { pick = enemy; pickThreat = threat; }
            }
            return pick; // 无优换 → null（本回合不攻击，攒费/回血等大生物）
        }
    }

    /// <summary>
    /// 蓝控制：有害指示物控制/消灭对面大生物，中等生物换掉对面小生物，逐步控场。
    /// 指示物/解场最优先（对方场面有生物时）；手牌不足时抽牌/发现续资源；
    /// 攻击"中等换小"——优先打得死的低威胁小生物（控场逐步推进），无击杀机会打脸磨血。
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

            // 控制轴：指示物（剧毒/毒素/冻结/沉默/紊乱/易损）与解场（弹回）——有目标才值钱
            if (oppHasBoard && CardHasAtom(core, card,
                AtomicEffectType.Poison, AtomicEffectType.AddToxin, AtomicEffectType.Freeze,
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

        public override Entity PickAttackTarget(GameCore core, Player me, Card unit, Player opp)
        {
            var combat = core.CombatSystem;
            var oppField = core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>();
            int myPower = core.LayerEngine.CalculatePower(unit);
            int myLife = unit.GetLife();

            // 中等换小：优先打得死的小生物（生命升序=小怪先清），优换优先、同档挑威胁高的
            Card pick = null;
            bool pickSurvives = false;
            int pickLife = int.MaxValue;
            int pickThreat = int.MinValue;
            foreach (var enemy in oppField)
            {
                if (!enemy.IsAlive || !combat.CanAttackTarget(unit, enemy, opp)) continue;
                if (myPower < enemy.GetLife()) continue;

                int threat = core.LayerEngine.CalculatePower(enemy);
                bool iSurvive = enemy.IsTapped() || threat < myLife;
                int life = enemy.GetLife();
                if (pick == null
                    || (iSurvive && !pickSurvives)
                    || (iSurvive == pickSurvives && (life < pickLife || (life == pickLife && threat > pickThreat))))
                {
                    pick = enemy;
                    pickSurvives = iSurvive;
                    pickLife = life;
                    pickThreat = threat;
                }
            }
            if (pick != null) return pick;

            // 无击杀机会 → 打脸磨血（控制卡组的中等生物持续施压）
            if (combat.CanAttackTarget(unit, opp, opp.Opponent)) return opp;
            return null; // 蓝不硬换：打不了脸也无击杀 → 不白送
        }
    }
}
