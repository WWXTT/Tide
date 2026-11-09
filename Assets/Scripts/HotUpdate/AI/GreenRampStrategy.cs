using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI
{
    /// <summary>
    /// 绿慢速（2026-11-01 重写·用户口径）：
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
    ///   ⑥ 活体护甲（2026-11-03 单目标改版·贴给林地守卫，护甲=守卫自身生命）；菌丝复活先贴角色，优先级不高。
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
        private const string NameVineArmor = "活体护甲"; // 绿3 法术（2026-11-03 单目标改版·教学卡 str=SelfUnit）：贴给{target}，护甲=其自身生命（守林地守卫）
        private const int ArmorTargetLife = 5;            // 「大生物」门槛：生命≥5 才值得贴活体护甲
        /// <summary>成长停手线（2026-11-03 用户定案：**9 费还允许再成长一次**）——9 地打一发 +2 → 11 地，
        /// 之后才停手、把成长当土地。</summary>
        private const int GrowthStopLands = 11;
        private const string NameDew = "晨露";       // 0/6 倒计时1 归零回 2 点生命（持续奶）

        /// <summary>倒计时支援卡（帷幕/溅射/警戒/狩猎的载体，均 0/6 无攻资格、倒计时3）。</summary>
        private static readonly HashSet<string> SupportNames =
            new HashSet<string> { "雾障", "爆荚", "噬血藤", "獠牙" };

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
            if (Is(card, NameVineArmor))
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
            else if (Is(card, NameVineArmor))
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
                    var opt = FindHandOption(options, NameVineArmor);
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
            if (Is(card, NameVineArmor)) return false;
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

            // 参照贴附增益（2026-11-03 用户定案·G6 数据驱动化：表行 RefDomain×正极性，现池=活体护甲）：
            // **只对林地守卫用**——守卫第一，其他单位垫底
            if (atom != null && IsReferenceRow(atom.Value, +1f))
                return list.OrderByDescending(e => e is Card cg && Is(cg, NameGuard) ? 100
                        : e is Card c4 && c4.GetController() == me ? c4.GetLife() : int.MinValue)
                           .ToList();

            // 再生（菌丝）：**只贴林地守卫**（2026-11-03 用户定案），其它单位垫底
            if (atom == AtomicEffectType.GrantRegeneration)
                return list.OrderByDescending(e => e is Card cq && Is(cq, NameGuard) ? 100
                        : e is Card c5 && c5.GetController() == me ? c5.GetMaxLife() : int.MinValue)
                           .ToList();

            // 守卫（2026-11-03 用户定案）：**优先绑角色**——对手的高攻爆发打的是角色，
            // 守卫拦的就是这条线（生命危险时更是角色优先）；对手没场面时才退而保护狩猎载体/獠牙
            if (atom == AtomicEffectType.Guard)
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

            // 守护（2026-11-05 简化定案）：固定护角色、无弹选——本排序只服务「谁获得守护」：
            // 贴给最能扛的单位（同活体护甲/再生 2026-11-03 口径：守卫第一，其余按最大生命）
            if (atom == AtomicEffectType.GrantGuardian)
                return list.OrderByDescending(e => e is Card cg && Is(cg, NameGuard) ? 100
                        : e is Card c4 && c4.GetController() == me ? c4.GetMaxLife() : int.MinValue)
                    .ToList();

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
                int handArmor = HandCopies(core, me, NameVineArmor);
                int handNewborn = HandCopies(core, me, NameNewborn);
                int enemyBodies = (core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield) ?? new List<Card>())
                    .Count(c => c != null && c.IsAlive);
                bool defenseMode = OpponentTotalAttack(core, me) >= 6 || enemyBodies >= 3;
                return list.OrderByDescending(e =>
                           {
                               if (e is Card g && Is(g, NameGuard) && g.HasKeyword(KeywordRules.Regeneration)) return 10;
                               if (e is Card cA && Is(cA, NameVineArmor))
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
}
