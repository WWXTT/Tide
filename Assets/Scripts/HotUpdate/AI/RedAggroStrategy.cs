using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI
{
    /// <summary>
    /// 红快攻（2026-11-04 v7，卡组=火灵·攻/火灵·守/火种·锐/火种·厚/血偿/赋予连击/活体炸弹）：
    ///
    /// 卡组两条线：0 费 2/2 火种铺场（锐/厚同名同身材，纯攻击无守卫）+ 1 费火灵（登场 +2 攻，攻/守两版）
    /// + 血偿（5 伤敌）/赋予连击（连击）——两张都挂并列代价「**摧毁一个己方地（对手选落点）**」
    ///（2026-11-04 改版：火种 2/2 后自伤 2 会打死它，自伤代价退役，改为摧毁地；摧毁锚红 3 →
    /// 血偿锚价钉死 6、连击锚 6，两张净红 3）。
    /// **活体炸弹**（2026-11-04 新 Key 牌 ×2，3 红·参照直伤）：主伤=己方最高攻生物的当前攻击力砸脸；
    /// 本回合第一张卡时奖励 2 伤（对手生命 ≤2 时补刀、否则优先解能打死的小阻拦生物）。红只 3 地，
    /// 付得起就抢本回合首卡（首卡门）；已经出过别的牌就留手到下一回合，不白丢奖励。
    /// 火灵奖励门同批由「手牌为0」换「操控地≤3」（LandsLe3）——早期地少=快攻奖励窗，越早下越赚。
    /// 放地口径（2026-11-03 用户定案）：**铺到 3 块就停**——3 块地=每回合 3 红，血偿/连击都打得动，
    /// 第 4 块地只会把「操控地≤3」的奖励门关死；3 块之前按「打不出的牌当地」自然铺，不主动烧能打的牌。
    ///
    /// 打法口径（用户 2026-11-02 四条）：
    /// ① 对手**有可阻拦生物**时，加攻/连击/激励优先给**高生命**的己方单位（1 点伤害就能点掉 1 命攻兵）；
    /// ② 斩杀线按连击算双倍伤害（有效伤害 = 攻击力 ×（连击?2:1）），全队有效伤害够就直接打脸；
    /// ③ **只带守卫的火灵**留着保护大哥——场上还没有大哥（攻击 ≥4、被火灵 +2 养过的 2/2）之前不打，
    ///    先下只带攻击的火灵加伤害；
    /// ④ **哪个 2/2 火种被养到 4 攻，谁就是大哥**：加攻不再按「攻击力最高」堆（会被 1 点伤害轻松点掉）；
    ///    只具备 +攻 时把攻击**分散**到不同单位（总伤害不变、更稳）；但手里连击即将可放、且对手无阻拦时，
    ///    改回**集中**到同一只做连击爆发。
    /// ⑤ **活体炸弹**（2026-11-04）：主伤打脸、奖励解拦路小生物；与血偿「伤害砸守卫开路」分工——
    ///    守卫生命 ≤2 时它更赚（多一份打脸伤害），守卫大时才用血偿硬开。斩杀计算把它的打脸值算满
    ///    （法术结算即中，比生物攻击更可靠）。
    /// 攻击口径沿用 2026-11-01：致死斩杀 &gt; 正收益解场 &gt; 打脸（赚的换子照换）。
    /// 代价目标=己方地（元素池）；对手选摧毁哪个（DispatchOrder 按对手视角最大化损失）。
    /// </summary>
    public sealed class RedAggroStrategy : AiStrategy
    {
        public override string Key => "red";
        public override string DisplayName => "红快攻";

        /// <summary>地牌停手线（2026-11-03 用户定案）：火灵的免费抽牌门是「操控地≤3」——
        /// 3 块地=每回合 3 红，血偿/连击（均红3）都打得动；第 4 块地只会把奖励门关死。
        /// 3 块之前仍只在「打不出的牌」里铺地（不主动烧能打的牌）。</summary>
        private const int LandStop = 3;

        /// <summary>是否允许把这张打不出的牌当地：地数未到 3 才放行；且**异色费牌不当土地**
        ///（红套无蓝绿源——异色费卡打不出就留手，别烧成异色地自断 3 红）。</summary>
        public override bool ShouldLand(GameCore core, Player me, Card card)
        {
            if (LandCount(core, me) >= LandStop) return false;
            var cost = GameActions.GetCardCost(card, 0);
            if (cost != null)
            {
                foreach (var color in cost.NonzeroColors())
                    if (color != ManaType.Gray && color != ManaType.Any && color != ManaType.Red)
                        return false;
            }
            return true;
        }

        private static int LandCount(GameCore core, Player me)
            => core.ElementPool.GetPooledCards(me)?.Count ?? 0;

        /// <summary>参照直伤法术（G6 收口 2026-11-08 数据驱动化）：数据信号=分段原子（表行 RefDomain）
        /// ×负极性——不再按枚举点名；现池=活体炸弹。主伤=参照己方生物当前攻击力，受害者结算期选
        ///（默认打脸）；本回合第一张卡时奖励 2 伤（优先解能打死的小阻拦生物，能补刀时才补脸）。</summary>
        private static bool IsBurstSpell(Card card)
            => DataOf(card)?.Supertype == Cardtype.Spell
               && HasSegmentedAtom(card, negative: true);

        /// <summary>活体炸弹的参照（伤害=其当前攻击力）：己方场上攻击力最高的生物。</summary>
        private static Card BestBurstReference(GameCore core, Player me)
        {
            Card best = null;
            int bestPow = -1;
            foreach (var c in OwnLivingUnits(core, me))
            {
                int pow = PowerOf(core, c);
                if (pow > bestPow) { best = c; bestPow = pow; }
            }
            return best;
        }

        private static int BestBurstPower(GameCore core, Player me)
            => BestBurstReference(core, me) is Card c ? PowerOf(core, c) : 0;

        /// <summary>本回合是否还没出过牌（FirstCardThisTurn 门预判；响应窗同按本回合计数判）。</summary>
        private static bool IsFirstCardOfTurn(GameCore core, Player me)
            => MatchStatsService.Instance == null
               || MatchStatsService.Instance.GetStat(me, MatchStatsService.CardsPlayed, StatScope.ThisTurn) == 0;

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            float score = 0f;
            bool isCreature = card is IHasSupertype ht && ht.Supertype == Cardtype.Creature;
            float cost = totalCost(card);
            var data = DataOf(card);

            // ① 0 费 2/2 火种（锐/厚同身材，免费底 2/2）最优先：不花资源先铺场
            //    2026-11-04：火种统一 2/2 仅攻击（无守卫资格），费用 0；0.5 半费仍兼容（攻红/血绿）。
            if (isCreature && cost <= 0.5f) score += 1200f;
            else if (isCreature && data != null && !data.AttackAbility) score += 150f; // 火灵·守：等大哥（ShouldHold 闸；2026-11-06 opt-in：无攻资格）
            else if (isCreature) score += 400f;                                  // 火灵·攻

            // ② 赋予连击：有值得连击的载体（攻 ≥2 的可攻击单位）才抢，否则压后
            if (IsDoubleStrikeSpell(card))
            {
                var carrier = BurstCarrier(core, me);
                score += carrier != null && PowerOf(core, carrier) >= 2 ? 900f : 200f;
            }

            // ③ 血偿（5 伤直伤）：抽到、付得起就打；对手进斩杀线时提最高
            if (IsDirectDamageSpell(card))
                score += me.Opponent != null && me.Opponent.Life <= 5 ? 1300f : 700f;

            // ④ 活体炸弹（2026-11-04 新 Key 牌）：3 红打脸 = 参照攻（本回合首卡再 +2 奖励）。
            //    红只 3 地，付得起就抢**本回合首卡**（首卡门）；没有高攻参照就压后/留手。
            if (IsBurstSpell(card))
            {
                int power = BestBurstPower(core, me);
                bool first = IsFirstCardOfTurn(core, me);
                int face = power + (first ? 2 : 0);
                bool lethal = me.Opponent != null && face >= me.Opponent.Life;
                if (lethal) score += 1500f;
                else if (first && power >= 3) score += 1350f;
                else if (power >= 5) score += 700f;
                else score += 100f;
            }

            score -= cost * 5f; // 低费先出
            return score;
        }

        /// <summary>
        /// 攒牌/让位闸：
        /// · 火灵·守（只带守卫）——留到大哥登场再下（生命 ≤10 自救除外）。
        /// 血偿/赋予连击的代价=**摧毁一个己方地**（对手选落点）——没有地时引擎闸拦（不可付），
        /// 本口不需额外留手；弃牌相关闸归蓝套。
        /// </summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
        {

            if (IsGuardOnly(card) && !HasBigBrother(core, me) && me.Life > 10)
                return true;
            // 活体炸弹（2026-11-04）：本回合已经出过牌就留手——3 红只够它一张，
            // 留到下一回合当首卡吃 +2 奖励（power 单独够斩杀时照打）。
            if (IsBurstSpell(card) && !IsFirstCardOfTurn(core, me))
            {
                int power = BestBurstPower(core, me);
                if (me.Opponent == null || power < me.Opponent.Life) return true;
            }

            // 血偿（2026-11-04 用户定案）：**只处理生命 >4 的目标**——有小怪但没有大目标时留着
            //（5 伤砸 1/1 是烧牌，还赔一张地）；只有「手里再没有别的牌可出」时才直接打脸。
            if (IsDirectDamageSpell(card))
            {
                // 有正经目标（>4 血）→ 主阶段留手，等「攻击前一拍」跟着攻击一起打（规则 3）
                // 有攻击手才等「攻击前一拍」；本回合根本没人能攻击 → 主阶段直接打 >4 目标（别把牌烂在手里）
                if (HasEnemyAboveLife(core, me, 4)) return HasReadyAttacker(core, me);
                return HasOtherPlayable(core, me, card);                // 还有牌可出 → 留着；没牌 → 打脸
            }

            // 连击（2026-11-04 用户定案）：**只给「能攻击且攻击力 ≥4」的单位**——4 攻以下不考虑，
            // 代价（炸一张地）的补偿比乱贴划算。没有合格载体就留手。
            if (IsDoubleStrikeSpell(card))
                return true; // 主阶段一律留手：等「攻击前一拍」贴给 ≥4 攻载体（规则 3）

            return false;
        }

        /// <summary>敌方场上是否有生命 > life 的单位（血偿的「正经目标」门）。</summary>
        private static bool HasEnemyAboveLife(GameCore core, Player me, int life)
        {
            var opp = me?.Opponent;
            if (opp == null) return false;
            foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && c.GetLife() > life) return true;
            return false;
        }

        /// <summary>手里还有没有「别的、付得起、非本族法术」的牌可出（血偿的「无牌可出才打脸」判据）。
        /// 只数生物/其它非这三张速度牌——避免与 ShouldHold 自递归。</summary>
        private static bool HasOtherPlayable(GameCore core, Player me, Card self)
        {
            foreach (var c in HandCards(core, me))
            {
                if (c == null || ReferenceEquals(c, self)) continue;
                if (IsDirectDamageSpell(c) || IsDoubleStrikeSpell(c) || IsBurstSpell(c)) continue;
                if (GameActions.CanAffordCast(core, me, c, 0)) return true;
            }
            return false;
        }

        /// <summary>连击的最佳载体：能攻击、攻击力 ≥4、取最高攻；没有返回 null（→ 留手）。</summary>
        private static Card BestDoubleStrikeCarrier(GameCore core, Player me)
        {
            Card best = null;
            int bestPower = 3; // ≥4 才算合格
            foreach (var c in OwnLivingUnits(core, me))
            {
                if (c == null || !c.HasAttackAbility()) continue; // 火灵·守这类不能攻击的排除
                int p = PowerOf(core, c);
                if (p > bestPower) { best = c; bestPower = p; }
            }
            return best;
        }

        /// <summary>响应型法术（2026-11-04）：能吃「守卫宣言 → 攻方响应窗」这一拍的牌。</summary>
        private static bool IsResponseSpell(Card card)
            => DataOf(card)?.Supertype == Cardtype.Spell
               // 速度闸（用户 2026-11-04 定案）：以卡的**实际发动速度**判定能否延迟——0 速牌只能
               // 主阶段用，留了也用不出来；生物/地牌恒 0 速，天然不进本闸（火苗/火灵·攻不参与留手）。
               && SpeedCalculator.GetCardCastSpeed(card) >= SpeedLevel.Instant
               && (IsDoubleStrikeSpell(card) || IsDirectDamageSpell(card) || IsBurstSpell(card));

        /// <summary>有未横置、可宣言攻击的己方单位（响应窗的价值前提）。</summary>
        private static bool HasReadyAttacker(GameCore core, Player me)
        {
            foreach (var c in OwnLivingUnits(core, me))
                if (c != null && core.CombatSystem.CanDeclareAttack(c, me)) return true;
            return false;
        }

        /// <summary>对手有未横置守卫（能被守卫生效拦下——只有带守卫的关键词单位可拦截）。</summary>
        private static bool EnemyCanBlock(GameCore core, Player me)
        {
            var opp = me?.Opponent;
            if (opp == null) return false;
            foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && !c.IsTapped() && c.HasKeyword(KeywordRules.Guardian)) return true;
            return false;
        }

        /// <summary>这张牌现在打出去就能赢（直伤/炸弹打脸够斩杀）——那就不留。</summary>
        private static bool CanCloseNow(GameCore core, Player me, Card card)
        {
            var opp = me?.Opponent;
            if (opp == null) return false;
            if (IsDirectDamageSpell(card)) return DirectDamageValue(card) >= opp.Life;
            if (IsBurstSpell(card))
            {
                int face = BestBurstPower(core, me) + (IsFirstCardOfTurn(core, me) ? 2 : 0);
                return face >= opp.Life;
            }
            return false; // 连击本身不打脸，靠攻击结算——交给战斗
        }

        /// <summary>
        /// 先手一波（2026-11-04 交替定案）：对手**留得出回应费**（≥4——一张 AOE/大解的量级）+
        /// 我方有 **2 个以上「同攻击」** 的可攻击单位时，先打一轮再铺场。
        /// 理由：旧序「铺满→打」把整回合的场一次性暴露给对手的 AOE/响应，打空即零伤害；
        /// 先打一波，对手的回应只能吃掉这一波，之后的铺场仍然出手（用户 2026-11-04 实局观察）。
        /// **同攻限定**（用户 2026-11-04 定案）：只有攻击力相同的单位互换筹码不变，拆波不损失增益价值；
        /// 混编（如 4 攻 + 2 攻/或要喂火灵 +2 的 2+1）仍按「先加攻再打」——第一波吃不到增益反而少伤害。
        /// 攻击力 <2 的也不拆（1 攻单位打不动人，先铺更有价值）。
        /// </summary>
        public override bool WantEarlyCombat(GameCore core, Player me)
        {
            var opp = me?.Opponent;
            if (opp == null) return false;
            // 拆波触发条件（用户 2026-11-04）：对手**有未横置守卫** 或 **留费 ≥4**——两条都是"对手能回应"的公开信号
            if (!EnemyCanBlock(core, me) && core.ElementPool.GetRemainingElementTotal(opp) < 4) return false;

            int power = -1, count = 0;
            foreach (var c in OwnLivingUnits(core, me))
            {
                if (c == null || !core.CombatSystem.CanDeclareAttack(c, me)) continue;
                int p = PowerOf(core, c);
                if (count == 0) power = p;
                else if (p != power) return false; // 混编 → 不拆波
                count++;
            }
            return count >= 2 && power >= 2;
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

            ResponseOption dsOpt = null, dmgOpt = null, burstOpt = null;
            foreach (var o in options)
            {
                if (o == null || o.Kind != ResponseOption.ResponseKind.HandCard || o.SourceCard == null) continue;
                if (IsBurstSpell(o.SourceCard))
                {
                    if (burstOpt == null) burstOpt = o;
                }
                else if (dsOpt == null && IsDoubleStrikeSpell(o.SourceCard)) dsOpt = o;
                else if (dmgOpt == null && IsDirectDamageSpell(o.SourceCard)) dmgOpt = o;
            }
            if (dsOpt == null && dmgOpt == null && burstOpt == null) return null;

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

            // 活体炸弹（2026-11-04）：主伤打脸、奖励 2 点解拦路小生物——和血偿「伤害砸守卫开路」
            // 是两条线。响应窗里能吃到首卡门（本回合还没出过牌）且守卫生命 ≤2 时优先用它：
            // 主伤打脸 + 奖励杀守卫 → 守卫死、攻击回到原目标，比血偿多赚一份打脸伤害。
            var burstRef = burstOpt != null ? BestBurstReference(core, me) : null;
            int burstFace = burstRef != null ? PowerOf(core, burstRef) : 0;
            bool burstFirst = IsFirstCardOfTurn(core, me);
            bool burstLethal = burstRef != null && burstFace >= oppLife;
            bool burstBreaksGuard = burstRef != null && burstFirst && gLife <= 2 && burstFace > 0;

            if (dmgKills && (faceLethal || lethalAfterOpen))
                return WithTargets(dmgOpt, guarder);             // ① 血偿开路且够斩 → 先省活体炸弹
            if (burstOpt != null && (burstLethal || burstBreaksGuard))
                return WithTargets(burstOpt, burstRef);          // ② 活体炸弹：打脸 + 奖励杀小守卫
            if (dsKills)
                return WithTargets(dsOpt, attacker);             // ③ 场面优先：连击先制无伤清障
            if (dmgKills)
                return WithTargets(dmgOpt, guarder);             // ④ 血偿硬开路
            return null;                                          // ⑤ 都打不死 → 让过（正常战斗）
        }

        /// <summary>血偿/连击的目标口径（2026-11-04 用户定案）：血偿只处理生命 >4 的敌方单位
        /// （没有就不打，留给打脸时机）；连击只贴「能攻击且攻击力 ≥4」的单位（代价要炸一张地）。</summary>
        public override List<Entity> PickSpellTargets(GameCore core, Player me, Card card, int modeIndex)
        {
            if (IsDirectDamageSpell(card))
            {
                var best = (core.ZoneManager.GetCards(me?.Opponent, Zone.Battlefield) ?? new List<Card>())
                    .Where(c => c != null && c.IsAlive && c.GetLife() > 4)
                    .OrderByDescending(c => c.GetLife()).FirstOrDefault();
                if (best != null) return new List<Entity> { best };
                // 没有 >4 血目标：只有「手里再没有别的牌可出」才打脸（规则 1 的兜底），否则这次不打
                if (!HasOtherPlayable(core, me, card) && me?.Opponent != null)
                    return new List<Entity> { me.Opponent };
                return new List<Entity>();
            }
            if (IsDoubleStrikeSpell(card))
            {
                var carrier = BestDoubleStrikeCarrier(core, me);
                return carrier != null ? new List<Entity> { carrier } : new List<Entity>();
            }
            return null; // 其它牌不接管
        }

        /// <summary>攻击前一拍：攻击力 ≥4 的攻击手先贴连击；目标 >4 血先血偿开路。
        /// 其余情况返回 null（不额外行动）。</summary>
        public override Card PickAttackTimeSpell(GameCore core, Player me, Card attacker, Entity target,
            List<Entity> outTargets)
        {
            if (attacker == null || outTargets == null) return null;
            outTargets.Clear();

            // 连击：攻击手 ≥4 攻 且 还没连击
            if (!HasDoubleStrike(attacker) && PowerOf(core, attacker) >= 4)
            {
                var ds = HandCards(core, me).FirstOrDefault(c => IsDoubleStrikeSpell(c) && GameActions.CanAffordCast(core, me, c, 0));
                if (ds != null) { outTargets.Add(attacker); return ds; }
            }
            // 血偿：目标 >4 血 且 付得起
            if (target is Card t && t.IsAlive && t.GetLife() > 4)
            {
                var dmg = HandCards(core, me).FirstOrDefault(c => IsDirectDamageSpell(c) && GameActions.CanAffordCast(core, me, c, 0));
                if (dmg != null) { outTargets.Add(t); return dmg; }
            }
            return null;
        }
        /// <summary>响应选项写入声明期目标（AI 侧不走人类的目标预选收集，这里直接钉死）。</summary>
        private static ResponseOption WithTargets(ResponseOption opt, Entity target)
        {
            if (opt == null) return null;
            opt.Targets = new List<Entity> { target };
            return opt;
        }

        /// <summary>直伤开路（守卫死亡、攻击回到原目标）后，本回合剩余伤害是否足以斩杀：
        /// 这一击的打脸值 + 其它未横置攻击手的有效伤害 + 手里其它可付直伤/活体炸弹
        /// （排除刚用的这张；血偿已吃掉首卡，活体炸弹不再计首卡奖励）。</summary>
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
                else if (IsBurstSpell(c) && GameActions.CanAffordCast(core, me, c, 0))
                {
                    int power = BestBurstPower(core, me);
                    if (power > 0) burst += power;
                }
            }
            return burst >= opp.Life;
        }


        /// <summary>
        /// 地牌投放优先级（2026-11-03 v6）：铺地总量由 <see cref="ShouldLand"/> 钳在 3 块；
        /// 3 块之前，血偿等直伤打不出去时优先当地占坑（PlayLands 只对「打不出的卡」调用本口）。
        /// </summary>
        public override float LandPriority(GameCore core, Player me, Card card, Func<Card, float> totalCost)
            => IsDirectDamageSpell(card)
                ? 10000f
                : base.LandPriority(core, me, card, totalCost);

        /// <summary>
        /// AI 目标排序（2026-11-04 代价改摧毁地后）：
        /// · 代价=摧毁一个己方地（元素池卡）→ 由 <see cref="DispatchOrder"/> 的「对手选」接管
        ///   （挑最痛的地），本口不重复插手（req.IsCostTarget 直通）；
        /// · 血偿直伤效果 → 直接打脸（角色优先，不可指才回落敌方生物）；
        /// · 加攻/连击 → 见 <see cref="BuffScore"/>（有阻拦给高生命 / 无连击分散 / 连击爆发集中）；
        /// · 激励（重置）→ 先挑已横置、有效伤害最高的攻击手。
        /// </summary>
        public override List<Entity> OrderTargetCandidates(GameCore core, Player me, TargetSelectionRequest req)
        {
            var list = req?.Candidates;
            if (list == null || list.Count <= 1) return list;

            if (req.IsCostTarget) return list; // 代价目标：对手选（DispatchOrder 接管），本口不排序

            if (req.PromptAtom == AtomicEffectType.Untap)
                return OrderUntapTargets(core, me, list);

            if (req.PromptAtom == AtomicEffectType.GrantDoubleStrike)
                return OrderDoubleStrikeTargets(core, me, list);   // 2026-11-04：只给能攻击且 ≥4 攻
            if (req.PromptAtom == AtomicEffectType.AddPowerUp
                || req.PromptAtom == AtomicEffectType.ModifyPower)
                return OrderBuffTargets(core, me, list);

            // 活体炸弹主伤：打脸。（步5 spec 驱动：注册表分派替代类型相等特判——受体窗请求
            // 由引擎 spec 计划发起并带 SpecRole=Target；参照窗同表命中但候选全己方，
            // OrderBurstVictimTargets 对己方候选稳定保序，两侧行为与旧口径一致。）
            if (req.PromptAtom != null && IsReferenceRow(req.PromptAtom.Value, -1f))
                return OrderBurstVictimTargets(core, me, list);

            // 血偿直伤（5 伤）/ 活体炸弹奖励（2 伤）：前者只处理生命 >4 的目标（没有就打脸）；
            // 后者先补刀/解小阻拦。
            if (req.PromptAtom == AtomicEffectType.DealDamage)
                return req.PromptValue <= 2
                    ? OrderBombRewardTargets(core, me, list)
                    : OrderBloodPriceTargets(core, me, list);

            return list;
        }

        /// <summary>连击目标（2026-11-04 用户定案）：能攻击且攻击力 ≥4 的优先（攻击力降序）；
        /// 不能攻击的（火灵·守）排最后——贴在它身上等于白扔，还赔一张地。</summary>
        private static List<Entity> OrderDoubleStrikeTargets(GameCore core, Player me, List<Entity> list)
            => list.OrderByDescending(e => e is Card c && c.HasAttackAbility() ? PowerOf(core, c) : int.MinValue)
                   .ToList();

        /// <summary>血偿目标（2026-11-04 用户定案）：**只处理生命 >4 的敌方单位**（生命降序）；
        /// 没有大目标就打脸（「无牌可出直接打脸」由 ShouldHold 放行）。</summary>
        private static List<Entity> OrderBloodPriceTargets(GameCore core, Player me, List<Entity> list)
        {
            var opp = me?.Opponent;
            var big = list.Where(e => e is Card c && c.IsAlive && c.GetLife() > 4)
                          .OrderByDescending(e => ((Card)e).GetLife()).ToList();
            var face = list.Where(e => ReferenceEquals(e, opp)).ToList();
            var rest = list.Where(e => !big.Contains(e) && !face.Contains(e)).ToList();
            return big.Concat(face).Concat(rest).ToList();
        }
        /// <summary>有益目标（SimpleAI 逐原子预选口）：与 OrderTargetCandidates 的加攻/连击同口径。</summary>
        public override IEnumerable<Entity> OrderBeneficialTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
            => OrderBuffTargets(core, me, candidates?.ToList() ?? new List<Entity>());

        /// <summary>有害目标排序：红套直伤=打脸——角色优先，角色不可指才回落生物威胁降序；
        /// 候选全是己方生物时（参照 spec——SpecRole=Reference 的参照窗）取攻击力最高。</summary>
        public override IEnumerable<Entity> OrderHarmfulTargets(GameCore core, Player me, IEnumerable<Entity> candidates)
        {
            var list = candidates?.ToList() ?? new List<Entity>();
            var opp = me?.Opponent;
            if (opp != null && list.Contains(opp)) return OrderFaceTargets(core, me, list);
            // 活体炸弹的参照目标：候选全是己方生物 → 攻击力最高（主伤=参照攻）。
            if (list.Count > 0 && list.All(e => e is Card c && ReferenceEquals(c.GetController(), me)))
                return list.OrderByDescending(e => PowerOf(core, (Card)e)).ToList();
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

        /// <summary>活体炸弹主伤：直接打脸（角色优先），敌方生物按攻击力降序兜底。</summary>
        private static List<Entity> OrderBurstVictimTargets(GameCore core, Player me, List<Entity> list)
        {
            var opp = me?.Opponent;
            return list.OrderByDescending(e =>
                    opp != null && ReferenceEquals(e, opp) ? 2
                    : e is Card c && c.IsAlive && ReferenceEquals(c.GetController(), opp) ? 1
                    : 0)
                .ThenByDescending(e => e is Card cc && ReferenceEquals(cc.GetController(), opp)
                    ? PowerOf(core, cc) : int.MinValue)
                .ToList();
        }

        /// <summary>活体炸弹的奖励（2 伤）：对手生命 ≤2 时补刀直接赢；否则优先解能一击打死的
        /// 小阻拦生物（守卫中/带守护更优先），都不成立才补脸。</summary>
        private static List<Entity> OrderBombRewardTargets(GameCore core, Player me, List<Entity> list)
        {
            var opp = me?.Opponent;
            if (opp != null && opp.Life <= 2 && list.Contains(opp))
                return OrderBurstVictimTargets(core, me, list);

            var guarder = PendingGuarder(core, me);
            return list.OrderByDescending(e =>
                    e is Card c && c.IsAlive && ReferenceEquals(c.GetController(), opp) && c.GetLife() <= 2
                        ? 1000 + (c.HasGuardAbility() ? 100 : 0) + PowerOf(core, c)
                          + (guarder != null && ReferenceEquals(c, guarder) ? 50 : 0)
                    : opp != null && ReferenceEquals(e, opp) ? 1
                    : 0)
                .ToList();
        }

        // ---------------- 小件 ----------------

        private static CardData DataOf(Card card) => (card as CardWrapper)?.GetData();

        private static bool IsCreatureCard(Card card) => DataOf(card)?.Supertype == Cardtype.Creature;

        /// <summary>只带守卫：生物 + 无攻资格（火灵·守，2026-11-06 opt-in 口径）——可拦截、不能攻击。</summary>
        private static bool IsGuardOnly(Card card)
        {
            var d = DataOf(card);
            return d != null && d.Supertype == Cardtype.Creature && !d.AttackAbility;
        }

        /// <summary>赋予连击法术（主序列含 GrantDoubleStrike）。</summary>
        private static bool IsDoubleStrikeSpell(Card card)
            => DataOf(card)?.Supertype == Cardtype.Spell
               && HasAtomAnywhere(card, AtomicEffectType.GrantDoubleStrike);

        /// <summary>直伤法术（主序列含 DealDamage 且不是连击/参照直伤——参照直伤走专线，
        /// 数据信号=分段原子（G6 收口）：不再按枚举点名）。</summary>
        private static bool IsDirectDamageSpell(Card card)
            => DataOf(card)?.Supertype == Cardtype.Spell
               && HasAtomAnywhere(card, AtomicEffectType.DealDamage)
               && !HasAtomAnywhere(card, AtomicEffectType.GrantDoubleStrike)
               && !HasSegmentedAtom(card, negative: true);

        private static int PowerOf(GameCore core, Card c) => core.LayerEngine.CalculatePower(c);

        private static bool HasDoubleStrike(Card c) => c.HasKeyword(KeywordRules.DoubleStrike);

        /// <summary>有效伤害（2026-11-02 斩杀口径）：攻击力 ×（连击 ? 2 : 1）。</summary>
        private static int EffectiveDamage(GameCore core, Card c)
            => PowerOf(core, c) * (HasDoubleStrike(c) ? 2 : 1);

        /// <summary>直伤法术的实际伤害值（主序列 DealDamage 的 value——血偿=5）。</summary>
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

        /// <summary>对手是否有可阻拦的生物（守卫资格：生物且声明 GuardAbility）。</summary>
        private static bool EnemyHasBlocker(GameCore core, Player me)
        {
            var opp = me?.Opponent;
            if (opp == null) return false;
            foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && c.IsLivingUnit() && c.HasGuardAbility()) return true;
            return false;
        }

        /// <summary>大哥（被火灵养大的 2/2 火种）= 己方战场攻击力最高的存活单位；
        /// 只认攻击 ≥4（2/2 本体 +2 攻指示物），没养起来之前不算。</summary>
        private static Card BigBrother(GameCore core, Player me)
        {
            Card best = null;
            int bestPow = 3; // 攻击 ≥4 才算大哥
            foreach (var c in OwnLivingUnits(core, me))
            {
                int life = c.GetLife();
                int pow = PowerOf(core, c);
                if (pow > bestPow || (pow == bestPow && life > (best?.GetLife() ?? 0)))
                {
                    best = c; bestPow = pow;
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

        /// <summary>本回合能否斩杀（连击翻倍 + 活体炸弹口径）：场上未横置攻击手的有效伤害合计
        /// + 手里付得起的直伤/活体炸弹（首卡奖励能补刀时 +2），≥ 对手生命。
        /// 2026-11-04 用户口径：法术斩杀成功率高于生物攻击（结算即中，不会被守卫改道），故这里把
        /// 法术打脸值算满。</summary>
        private static bool CanLethalThisTurn(GameCore core, Player me, Player opp)
        {
            if (opp == null) return false;
            int burst = 0;
            foreach (var c in OwnLivingUnits(core, me))
                if (c.HasAttackAbility() && core.CombatSystem.CanDeclareAttack(c, me))
                    burst += EffectiveDamage(core, c);
            bool rewardOpen = IsFirstCardOfTurn(core, me);
            foreach (var c in HandCards(core, me))
            {
                if (IsDirectDamageSpell(c) && GameActions.CanAffordCast(core, me, c, 0))
                {
                    burst += DirectDamageValue(c);
                }
                else if (IsBurstSpell(c) && GameActions.CanAffordCast(core, me, c, 0))
                {
                    int power = BestBurstPower(core, me);
                    if (power <= 0) continue;
                    burst += power;
                    if (rewardOpen && power + 2 >= opp.Life)
                    {
                        burst += 2; // 主伤打完后对手 ≤2：奖励补刀走脸
                        rewardOpen = false;
                    }
                }
            }
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

        // 「手牌为 0 时抽 1」触发检测（HasHandEmptyDraw/HasDrawAtom）已随 2026-11-03 火灵换门退役：
        // 奖励门改「操控地≤3」后，空手排队调度的意义已不存在。
    }
}
