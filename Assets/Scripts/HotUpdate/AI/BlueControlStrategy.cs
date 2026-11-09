using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI
{
    /// 以**带充盈的结界**（潜影碑/深潜碑）为引擎，潜影碑的充盈伤害是主制胜轴；
    /// 用乱锋/弹回/薄壁控场，寒锥/回潮只作找牌与防自抽干（回潮前期不主动回收）。
    /// 潜行（英雄技能「隐匿」）只保护带充盈的结界，绝不给角色；不再围绕磨牌/疲劳斩杀。
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
            // 蓝套 v4（2026-11-04 口径回归）：充盈控制，不再做「双方过牌 + 洗回 + 磨空牌库」。
            // - 潜影碑（充盈·2 伤）= 主引擎；深潜碑（充盈·磨 1）只作低优先填充，不围绕磨牌/疲劳赢。
            // - 乱锋/弹回 = 控制；薄壁 = 保护；寒锥/回潮只作找牌/防自抽干，回潮前期不主动回收。
            // - 潜行（英雄技能）只给带充盈的结界，见 WantHeroSkill/OrderTargetCandidates。
            float score = 0f;
            bool oppHasBoard = me.Opponent != null
                && core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield).Any(c => c.IsAlive);
            int hand = core.ZoneManager.GetCards(me, Zone.Hand)?.Count ?? 0;
            int myDeck = core.ZoneManager.GetCards(me, Zone.Deck)?.Count ?? 0;

            if (IsOverflow(card))
            {
                bool overflowDamage = HasBranchAtom(card, AtomicEffectType.DealDamage);
                bool overflowMill = HasBranchAtom(card, AtomicEffectType.MillCard);
                // 潜影碑（伤害）是主引擎；深潜碑（磨牌）只作最后填充——磨牌不作为制胜轴。
                score += overflowDamage ? 1200f : overflowMill ? 0f : 450f;
            }

            // 控制轴：紊乱/弹回/冻结/沉默/易伤——对手有场面才值钱。
            if (oppHasBoard && HasAnyBranchAtom(card,
                AtomicEffectType.AddToxin, AtomicEffectType.Freeze,
                AtomicEffectType.Silence, AtomicEffectType.RushSickness, AtomicEffectType.AddVulnerable,
                AtomicEffectType.ReturnToHand))
                score += 500f;

            // 保护轴：薄壁（减半）——己方有单位/角色时优先护住关键目标。
            if (HasBranchAtom(card, AtomicEffectType.GrantProtection)) score += 300f;

            // 重放：只重复控制牌（紊乱/弹回），不重复寒锥/回潮的抽洗——避免变成抽牌/回收循环。
            if (HasBranchAtom(card, AtomicEffectType.GrantReplay))
            {
                bool hasControlToRepeat = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                    .Any(c => HasAnyBranchAtom(c, AtomicEffectType.RushSickness,
                        AtomicEffectType.ReturnToHand));
                if (hasControlToRepeat && CountLands(core, me, ManaType.Blue) >= 2) score += 300f;
            }

            // 找牌：手牌吃紧时寒锥/回潮的抽 1 才有价值，不当每回合固定过牌。
            if (hand <= 4 && HasBranchAtom(card, AtomicEffectType.DrawCard))
                score += 120f;

            // 回潮（洗回双方墓地）：只在自抽干风险时作保险；前期主动洗回=给对手续牌，降权。
            if (HasBranchAtom(card, AtomicEffectType.ShuffleIntoDeck))
                score += myDeck <= 5 ? 600f : -800f;

            if (InfoAxisAtoms.Any(t => HasBranchAtom(card, t))) score += 100f;

            score += totalCost(card); // 与默认同向：大费先出（资源花光）
            return score;
        }

        // ==================== 蓝套速度牌准入（2026-11-04 用户定案） ====================

        /// <summary>扰乱价值（弹回口径）：卡费 + 关键词/指示物溢价（连击 +6）。
        /// 被乱锋控住的单位返回 -1——**一律不弹**（它已经打不到角色了，弹回去等于送它重下）。</summary>
        private static int DisruptValue(Card c)
        {
            if (c == null || !c.IsAlive) return -1;
            if (KeywordRules.HasRushSickness(c)) return -1; // 被乱锋控住 → 不弹
            int v = 0;
            var cost = GameActions.GetCardCost(c, 0);
            if (cost != null)
                foreach (var color in cost.NonzeroColors()) v += (int)(cost[color] / 2f); // 半费→整费
            if (c.HasKeyword(KeywordRules.DoubleStrike)) v += 6; // 连击溢价
            return v;
        }

        /// <summary>弹回的最佳目标：价值 ≥3 且未被乱锋控住；取最高价值，没有返回 null（→ 留手当土地）。</summary>
        private static Card BestBounceTarget(GameCore core, Player me)
        {
            var opp = me?.Opponent;
            if (opp == null) return null;
            Card best = null; int bestV = 2; // >2 才算（即 ≥3）
            foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
            {
                int v = DisruptValue(c);
                if (v > bestV) { best = c; bestV = v; }
            }
            return best;
        }

        /// <summary>敌方场上是否有被乱锋控住的单位（有 → 不用减半：它打不到角色，减半是浪费）。</summary>
        private static bool AnyEnemyRushed(GameCore core, Player me)
        {
            var opp = me?.Opponent;
            if (opp == null) return false;
            foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && KeywordRules.HasRushSickness(c)) return true;
            return false;
        }

        /// <summary>减半生效中（自己身上带减半护盾）→ 本回合不用乱锋（两条防御别叠着浪费）。</summary>
        private static bool HalveShieldActive(Player me)
            => me != null && me.GetEffectiveCounterCount("HalveShield") > 0;

        public override List<Entity> PickSpellTargets(GameCore core, Player me, Card card, int modeIndex)
        {
            // 弹回：只弹价值 ≥3 且未被乱锋控住的卡；没有合格目标 → 空表（本次不打，留着当土地）
            if (HasBranchAtom(card, AtomicEffectType.ReturnToHand))
            {
                var t = BestBounceTarget(core, me);
                return t != null ? new List<Entity> { t } : new List<Entity>();
            }
            // 回潮（0 费洗回）：优先洗「回潮 + 潮汐」——2 费无条件抽牌是过牌引擎，加速摸到需要的卡
            if (HasBranchAtom(card, AtomicEffectType.ShuffleIntoDeck))
            {
                var grave = core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>();
                var picks = new List<Entity>();
                foreach (var name in new[] { "回潮", "潮汐" })
                {
                    var c = grave.FirstOrDefault(x => x is IHasName n && n.CardName == name && !picks.Contains(x));
                    if (c != null) picks.Add(c);
                }
                foreach (var c in grave)
                {
                    if (picks.Count >= 2) break;
                    if (!picks.Contains(c)) picks.Add(c);
                }
                return picks;
            }
            return null;
        }
        /// <summary>蓝套英雄技能「隐匿」：只在己方场上有**未潜行的带充盈结界**时发动；
        /// 没有充盈结界就不放（绝不把潜行丢给角色/普通结界）。</summary>
        public override bool WantHeroSkill(GameCore core, Player me)
        {
            if (!base.WantHeroSkill(core, me)) return false;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
            {
                if (!IsOverflowEnchantment(c, me)) continue;
                if (c.GetEffectiveCounterCount(CounterRules.StealthCounter) <= 0) return true;
            }
            return false;
        }

        /// <summary>带充盈的己方结界（无生命单位；非角色/生物）。</summary>
        private static bool IsOverflowEnchantment(Card c, Player me)
            => c != null && c.IsAlive && ReferenceEquals(c.GetController(), me)
               && !c.IsLivingUnit() && IsOverflow(c);

        /// <summary>卡的主序列/分支里是否含指定原子（含 Then 分支）。</summary>
        private static bool HasBranchAtom(Card card, AtomicEffectType type)
        {
            foreach (var atom in ModeAtoms(card, 0))
            {
                if (atom == null) continue;
                if (atom.Type == type) return true;
                if (atom.Branch?.Then == null) continue;
                foreach (var t in atom.Branch.Then)
                    if (t != null && t.Type == type) return true;
            }
            return false;
        }

        private static bool HasAnyBranchAtom(Card card, params AtomicEffectType[] types)
        {
            foreach (var type in types)
                if (HasBranchAtom(card, type)) return true;
            return false;
        }

        private static bool IsOverflow(Card card)
        {
            foreach (var atom in ModeAtoms(card, 0))
                if (atom != null && atom.Type == AtomicEffectType.EngineManaSurplus) return true;
            return false;
        }

        // ==================== 铺地（2026-11-02 打断玩法必要脚手架） ====================

        /// <summary>蓝地目标（2026-11-04 当前 ThemeBlueSurplus 版）：最高费回潮 4，4 块蓝地即可
        /// 覆盖全部卡；旧磨牌版按幻壁 8 抬到 9 的口径已随旧卡表退役。</summary>
        private const int BlueLandTarget = 4;

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

        /// <summary>留费闸（2026-11-02 任务B 动态化；2026-11-06 判据七重推导）：**只在己方场上存在
        /// 充盈引擎（EngineManaSurplus）时留费**，没引擎就不留（正常铺场出牌，别空过）。
        /// 判据七口径=双方剩余元素总量之差**恰==x** 触发、双方施放各评估——留费不再「囤越多越好」
        ///（≥x 旧账），改守**踩档带**：回合终态差（我的未横置地−对手剩余总量，钱包回合末清空）
        /// 落在 x−1 时对手最便宜的一张即踩中；打出后本回合内差恰==x 时自己的引擎当场开火（双方面）。
        /// 保护区（任务B③沿旧）：掉出踩档带（终态差 &lt; x−1）时扣住守剩余。
        /// 充盈引擎卡本身照常铺（它是产出留费价值的地基，不是消耗品）。
        /// </summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
        {
            // 2026-11-04 用户定案（蓝套速度牌）：
            // · 回潮（0 费洗回）——拿到就用，不走留牌逻辑；
            // · 薄壁（减半）——对手生物已被乱锋控住时不用（它打不到角色）；
            // · 乱锋——减半生效中不用（两条防御别叠着浪费）。
            if (HasBranchAtom(card, AtomicEffectType.ShuffleIntoDeck)) return false;
            if (HasBranchAtom(card, AtomicEffectType.ProtectionHalve) && AnyEnemyRushed(core, me)) return true;
            if (HasBranchAtom(card, AtomicEffectType.RushSickness) && HalveShieldActive(me)) return true;

            if (CardHasAtom(core, card, AtomicEffectType.EngineManaSurplus)) return false;

            // 回潮：洗回的是**双方**墓地——前期主动打=给对手续牌、把自己的控制/过牌再洗回去。
            // 只在自己牌库 ≤5（临近自抽干）时放行；其余扣住。
            if (HasBranchAtom(card, AtomicEffectType.ShuffleIntoDeck))
            {
                int myDeck = core.ZoneManager.GetCards(me, Zone.Deck)?.Count ?? 0;
                if (myDeck > 5) return true;
            }

            // 重放：手里没有可重复的控制牌时扣住，别空放（重放轮无支付目标=白亏一层）。
            if (HasBranchAtom(card, AtomicEffectType.GrantReplay))
            {
                bool hasControl = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                    .Any(c => HasAnyBranchAtom(c, AtomicEffectType.RushSickness,
                        AtomicEffectType.ReturnToHand));
                if (!hasControl) return true;
            }
            var bill = GameActions.GetCardCost(card, 0);
            if (bill == null || bill.IsZero) return false;

            var opp = me.Opponent;
            int threshold = BranchEngines.MaxManaSurplusThreshold(me); // 0=场上无充盈引擎 → 不留费
            if (opp == null || threshold <= 0) return false;

            // 判据七（2026-11-06 充盈改造）：差值=双方剩余元素总量（未横置地+钱包）、**恰==x** 才触发、
            // 双方施放各评估一次（己方回合自己出牌也会触发自己的引擎）。留费从「囤越多越好」
            //（判据六 ≥x 口径）改为「守踩档带」：钱包本回合结束清空——回合终态差=我的未横置地−
            // 对手剩余总量；对手每出一张，其剩余降、差值+1，恰==x 的那一张开火。
            // 两条放行捷径都是「好打」：① 打出后本回合内差恰==x → 自己的引擎当场开火（双方面）；
            // ② 回合终态差==x−1 → 对手最便宜的一张即踩中恰等档。掉出踩档带（< x−1）才扣住守剩余。
            int need = (int)Math.Ceiling(bill.Total - 1e-4f);
            int myUntapped = core.ElementPool.GetPool(me).PooledCards.Count(pc => pc != null && !pc.IsTapped);
            int myRem = core.ElementPool.GetRemainingElementTotal(me);
            int oppRem = core.ElementPool.GetRemainingElementTotal(opp);
            int wallet = myRem - myUntapped;
            int landsAfter = myUntapped - Math.Max(0, need - wallet); // 支付钱包优先、地牌补足后的未横置地
            int dInTurn = myRem - need - oppRem;        // 本回合内口径（钱包在场，自触发判据）
            int dTurnEnd = landsAfter - oppRem;         // 回合终态口径（钱包清空，对手踩档判据）
            if (dInTurn == threshold) return false;     // 己方自触发：恰等档当场兑现
            if (dTurnEnd == threshold - 1) return false; // 对手踩档：留一张最便宜的牌位给对手踩中
            return dTurnEnd < threshold - 1;            // 掉出踩档带 → 守剩余（再打就够不着了）
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

            // 潜行目标（2026-11-04 用户口径）：潜行只给**带充盈的己方结界**，不给角色。
            // 隐匿效果域已收窄到己方无生命单位（kind 3）；这里再按充盈价值排序：
            // 潜影碑（充盈·伤害）> 深潜碑（充盈·磨牌）> 其他；已潜行的沉底。
            if (req.PromptAtom == AtomicEffectType.GrantStealth)
                return list.OrderByDescending(e => StealthTargetScore(e, me)).ToList();

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

        /// <summary>潜行落点：只认带充盈的己方结界；潜影碑（伤害）优先于深潜碑（磨牌），
        /// 已经潜行的沉底（不重复喂层）。</summary>
        private static int StealthTargetScore(Entity e, Player me)
        {
            if (!(e is Card c) || !IsOverflowEnchantment(c, me)) return int.MinValue;
            int score = 1000;
            if (HasBranchAtom(c, AtomicEffectType.DealDamage)) score += 500;
            if (HasBranchAtom(c, AtomicEffectType.MillCard)) score += 100;
            if (c.GetEffectiveCounterCount(CounterRules.StealthCounter) > 0) score -= 2000;
            return score;
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
