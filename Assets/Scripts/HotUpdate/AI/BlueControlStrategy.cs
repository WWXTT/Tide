using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI
{
    /// 蓝套（2026-11-18 卡组重做后口径：潮汐×12 / 深潭碑×8 / 乱锋×6 / 寒冬×4；回潮/潮痕碑已退组）：
    /// 以**带充盈的深潭碑结界**为核心——T2 起放 2 地、下 1 个结界，然后潮汐连打触发结界（抽到潮汐继续打）。
    /// · 充盈触发=第三版「己方释放法术后·仅法术」（BranchEngines，2026-11-18 定稿）——潮汐是法术，
    ///   自己连打潮汐即可触发自己的碑；
    /// · 判据=剩余元素差 **≥** 门槛（2026-11-13 起；旧「恰好=门槛」判据七退役）；
    /// · 深潭碑触发召唤潮痕碑衍生物（不入手牌、无遗忘到期）——潮痕碑本体不在卡组，只经召唤出现，
    ///   是主制胜轴；
    /// · 碑的耐久按「每次触发扣固定表值」消耗（2026-11-20：ValueSystemConfig
    ///   GameRules.AutoEffectDurabilityCost，默认 1；旧「每次 −1」硬编码与「每 3 次扣整份」均已退役）。
    /// 乱锋/冻结作控制（只在对手回合响应：乱锋只控生物、冻结只冻对方地牌）；蛰伏（英雄技能，
    /// 2026-11-18 替换隐匿）：充盈踩不到档时
    /// 横置全部未横置地攒费，下个自己的准备阶段回本等量临时彩元素。
    /// </summary>
    public sealed class BlueControlStrategy : AiStrategy
    {
        public override string Key => "blue";
        public override string DisplayName => "蓝控制";

        // ==================== 基础运转（2026-11-18 用户定案） ====================
        // 蓝套=2 地启动的爆牌节奏：T1 **全局**只下 1 地、跳过所有操作（旧「1 碑+隐匿」路径已删）；
        // T2 起 放 2 地、下 1 个结界，然后潮汐连打触发结界（抽到潮汐继续打）。
        // 冻结/乱锋只在对手回合用（响应），冻结只冻对方地牌，乱锋只控生物。
        private int _steleTurnStamp = -1;
        private int _recycleTurnStamp = -1;
        private bool _needChaoXiAfterStele;
        private int _chainTurn = -1;
        private bool _chainEnded;
        private int _chaoXiStamp = -1;
        private int _steleForcedThisTurn;
        private readonly int[] _landTurnStamp = { -1, -1 };
        private readonly int[] _landCountAtTurnStart = { 0, 0 };

        private int LandsThisTurn(GameCore core, Player me)
        {
            int seat = ReferenceEquals(me, core.Player1) ? 0 : 1;
            int turn = core.TurnEngine.TurnNumber;
            if (_landTurnStamp[seat] != turn)
            {
                _landTurnStamp[seat] = turn;
                _landCountAtTurnStart[seat] = core.ElementPool.GetPooledCards(me)?.Count ?? 0;
            }
            return (core.ElementPool.GetPooledCards(me)?.Count ?? 0) - _landCountAtTurnStart[seat];
        }

        /// <summary>自己第几回合（1 起）：P1 在全局 1/3/5…，P2 在 2/4/6…。</summary>
        private static int OwnTurnIndex(GameCore core, Player me)
        {
            int t = core.TurnEngine.TurnNumber;
            return ReferenceEquals(me, core.Player1) ? (t + 1) / 2 : t / 2;
        }

        /// <summary>卡是否=自己元素池里未横置的地（蛰伏目标过滤——已横置的再蛰伏无收益）。</summary>
        private static bool IsUntappedPoolLand(GameCore core, Player me, Card c)
        {
            if (c == null || c.GetZone() != Zone.ElementPool) return false;
            if (!ReferenceEquals(c.GetOwner() ?? c.GetController(), me)) return false;
            var pooled = core.ElementPool.GetPooledCards(me)
                .FirstOrDefault(pc => pc != null && pc.SourceCard == c);
            return pooled != null && !pooled.IsTapped;
        }

        /// <summary>充盈 2 伤当前能不能触发：己方剩余元素 − 对方剩余元素 ≥ 门槛（默认 2）。</summary>
        private static bool CanOverflowTrigger(GameCore core, Player me)
        {
            int x = BranchEngines.MaxManaSurplusThreshold(me);
            if (x <= 0) x = 2;
            int mine = core.ElementPool?.GetRemainingElementTotal(me) ?? 0;
            int theirs = core.ElementPool?.GetRemainingElementTotal(me?.Opponent) ?? 0;
            return mine - theirs >= x;
        }

        /// <summary>铺地节奏（2026-11-18 三段式）：T1 最多 1 张、T2 起最多 2 张；
        /// **潮汐/深潭碑=启动件不铺**；到达 2 地的优先级高于留 key 牌（乱锋最后一张也照铺；回潮已退组，寒冬不作地）。</summary>
        public override bool ShouldLand(GameCore core, Player me, Card card)
        {
            // 蓝 4 地停手线（2026-11-18 用户定案；同红 LandStop=3 的口径，策略侧钳总量）。
            if ((core.ElementPool.GetPooledCards(me)?.Count ?? 0) >= 4) return false;

            int turn = OwnTurnIndex(core, me);
            int cap = core.TurnEngine.TurnNumber <= 1 ? 1 : 2;
            if (LandsThisTurn(core, me) >= cap) return false;

            string name = CardNameOf(card);
            // （0 费不可作地是全局规则：潮汐/碑本来入不了池，策略不需要再按名字挡）

            // 寒冬：**不作地**（2026-11-18 定案——它是 4 地保底线的强开卡，当肥料吃掉就永远打不出）
            if (name == "寒冬") return false;

            // 2026-11-18 用户定案：**先到 2 地，再谈留牌**——本回合没放满时，乱锋
            // 即使只剩 1 张也照铺（2 地达标 → 充盈能触发，收益远大于留一张控制）。
            return base.ShouldLand(core, me, card);
        }

        /// <summary>固定操作（三段式②）：无碑先下碑（深潭碑 &gt; 潮痕碑）→ 有碑且手里有潮汐就连续触发。</summary>
        public override Card ForcePlayCard(GameCore core, Player me)
        {
            if (core?.TurnEngine == null || !ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return null;
            // 2026-11-18 用户定案（**全局固定序**）：T1 只下 1 地，不强制任何出牌。
            if (core.TurnEngine.TurnNumber <= 1) return null;
            var hand = core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>();
            bool steleOnBoard = HasOwnOverflowEnchantment(core, me);

            // 2026-11-18 用户定案：**乱锋只在对手回合用**——己方回合不再为"首张卡洗回"强制打它。
            // （原「墓地有可回收件就首张打乱锋」的强制动作已删：那是它 37 次全打在自己回合的根因。）

            // 固定循环（2026-11-18 用户定案·最终口径）：
            // · 使用碑**依赖手里有潮汐**；潮汐的使用不依赖碑；
            // · 顺序：**手里所有碑（含回响刚复制出来的临时碑）先全部落地 → 再打潮汐**；
            // · 每打一张潮汐后回到"碑先落地"这一步——复制体进手就立刻打出去，再接下一张潮汐；
            // · 手里没有潮汐时，只把**临时复制碑**打掉（会被回合末移除），永久碑留在手里。
            bool IsStele(Card c) => c != null && (CardNameOf(c) == "深潭碑" || CardNameOf(c) == "潮痕碑");
            bool IsTemp(Card c) => c != null && c.GetCounterCount(CounterRules.TempCounter) > 0;
            bool playable(Card c) => !core.StackEngine.IsPendingCast(c);

            // 新分支：已经 4 地、仍达不到充盈 → 用潮汐过牌找寒冬；寒冬到手就停手，交给蛰伏结束回合
            if ((core.ElementPool.GetPooledCards(me)?.Count ?? 0) >= 4 && !CanOverflowTrigger(core, me)
                && !hand.Any(c => c != null && CardNameOf(c) == "寒冬"))
            {
                var drawTide = hand.FirstOrDefault(c => c != null && CardNameOf(c) == "潮汐" && playable(c));
                if (drawTide != null) return drawTide;
            }

            var chaoshi = hand.FirstOrDefault(c => c != null && CardNameOf(c) == "潮汐" && playable(c));

            if (chaoshi != null)
            {
                // 有潮汐：任何碑（永久或临时复制）都先落地，再打潮汐
                var anyStele = hand.FirstOrDefault(c => IsStele(c) && playable(c));
                if (anyStele != null) return anyStele;
                return chaoshi;
            }

            // 没潮汐：只清临时复制碑（永久碑留手，不当废牌打）
            var tempStele = hand.FirstOrDefault(c => IsStele(c) && IsTemp(c) && playable(c));
            if (tempStele != null) return tempStele;
            return null;
        }

        /// <summary>对手场上生物攻击力合计（乱锋使用条件：&gt; 4）。</summary>
        private static int EnemyTotalPower(GameCore core, Player opp)
        {
            int sum = 0;
            foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                if (c != null && c.IsAlive && c.IsLivingUnit())
                    sum += core.LayerEngine?.CalculatePower(c) ?? 0;
            return sum;
        }

        private static bool HasEnemyCreature(GameCore core, Player opp)
            => opp != null && (core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                .Any(c => c != null && c.IsAlive && c.IsLivingUnit());

        private static bool IsControlKey(Card card)
            => HasBranchAtom(card, AtomicEffectType.Freeze)
               || HasBranchAtom(card, AtomicEffectType.RushSickness);

        private static string CardNameOf(Card c) => (c as IHasName)?.CardName ?? c?.ID;

        /// <summary>对手元素池里是否有地牌（冻结的合法目标）。</summary>
        private static bool HasEnemyPoolLand(GameCore core, Player opp)
            => opp != null && (core.ElementPool.GetPooledCards(opp)?.Any(pc => pc?.SourceCard != null) ?? false);

        /// <summary>己方场上是否已有碑（充盈结界）：优先按卡名识别（深潭碑/潮痕碑），
        /// 原子判定兜底——`HasBranchAtom` 对"分支引擎"行不一定命中，曾导致每回合 1 碑的闸失效。</summary>
        private static bool HasOwnOverflowEnchantment(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Any(c => c != null && c.IsAlive && !c.IsLivingUnit()
                          && (CardNameOf(c) == "深潭碑" || CardNameOf(c) == "潮痕碑"
                              || HasBranchAtom(c, AtomicEffectType.EngineManaSurplus)));

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
            // 蓝套 2026-11-13（碑阵）：充盈控制——结界（碑）铺场是第一位，回响续牌、回潮洗碑回收。
            // - 潮痕碑（充盈·2 伤）= 主制胜轴；深潭碑（充盈·召唤潮痕碑）= 续航源（每次触发直接补一个衍生物）。
            // - 乱锋 = 控制；潮汐 = 过牌；回潮 = 洗碑回收 / 防自抽干。
            // - 蛰伏（英雄技能）：充盈踩不到档时横置全部地攒费，见 WantHeroSkill/OrderTargetCandidates。
            float score = 0f;
            bool oppHasBoard = me.Opponent != null
                && core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield).Any(c => c.IsAlive);
            int hand = core.ZoneManager.GetCards(me, Zone.Hand)?.Count ?? 0;
            int myDeck = core.ZoneManager.GetCards(me, Zone.Deck)?.Count ?? 0;

            // ===== 基础运转节奏（2026-11-18 用户定案）=====
            // ① 场上没碑：先下结界（深潭碑=召唤 > 潮痕碑=2伤），潮汐先收着；
            // ② 场上有碑：潮汐连打（每张己方法术都触发充盈），新碑降权（先触发，别叠）。
            bool steleOnBoard = HasOwnOverflowEnchantment(core, me);
            // 寒冬：正常循环起不来（充盈触发不了）时的保底强开——优先打出，冻住对方全部地。
            if (CardNameOf(card) == "寒冬" && !CanOverflowTrigger(core, me)) score += 900f;
            if (IsOverflow(card))
                score += steleOnBoard ? -600f : ((HasBranchAtom(card, AtomicEffectType.EchoCopy) || HasBranchAtom(card, AtomicEffectType.SummonToken)) ? 1500f : 1200f);
            else if (HasBranchAtom(card, AtomicEffectType.DrawCard))
                score += steleOnBoard ? 1800f : 200f;

            if (IsOverflow(card))
            {
                bool overflowDamage = HasBranchAtom(card, AtomicEffectType.DealDamage);
                bool overflowCopy = HasBranchAtom(card, AtomicEffectType.EchoCopy);
                bool overflowSummon = HasBranchAtom(card, AtomicEffectType.SummonToken);
                // 2026-11-13 蓝套改版：充盈碑=引擎本体，两张都进最高档——潮痕碑（充盈·2伤）是主制胜轴，
                // 深潭碑（充盈·召唤潮痕碑）是续航源（每次触发补一张衍生物，耐久才接得上）。
                // 旧口径「深潜碑（磨牌）只作低优先填充、不围绕磨牌赢」随回响改版退役。
                score += overflowDamage || overflowCopy || overflowSummon ? 1200f : 450f;
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

            // 回潮（洗回双方墓地）：2026-11-13 改版——碑阵要续航，**墓地里有碑就先洗碑回牌库再抽**；
            // 墓地没碑时仍只作防自抽干保险（前期主动洗回=给对手续牌，降权）。
            if (HasBranchAtom(card, AtomicEffectType.ShuffleIntoDeck))
            {
                int graveMonuments = CountGraveMonuments(core, me);
                score += graveMonuments > 0 ? 700f + graveMonuments * 50f : (myDeck <= 5 ? 600f : -800f);
            }

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
            // 回潮（洗回）：2026-11-13 改版——优先把**阵亡的碑**洗回牌库（碑=引擎，抽回来才能续循环），
            // 其次才洗潮汐/回潮这类过牌消耗品（旧口径只洗这两张，碑死了永远回不来）。
            if (HasBranchAtom(card, AtomicEffectType.ShuffleIntoDeck))
            {
                var grave = core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>();
                var picks = new List<Entity>();
                foreach (var name in new[] { "寒冬", "乱锋", "潮痕碑", "深潭碑", "潮汐", "回潮" })
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
        /// <summary>蓝套英雄技能「蛰伏」（2026-11-18 改版，替换隐匿）：充盈踩不到档
        /// （己方剩余−对方剩余 &lt; 门槛——本回合横竖触发不了）→ 蛰伏攒费：横置全部未横置地
        /// （不产元素），下个自己的准备阶段回本等量临时彩元素。踩得到档=照常运转不蛰伏；
        /// 没有未横置地=白发动，直接否。T1 仍不发技能（全局固定序）。</summary>
        public override bool WantHeroSkill(GameCore core, Player me)
        {
            if (!base.WantHeroSkill(core, me)) return false;
            // 2026-11-18 用户定案（**固定序**）：T1 = 放地 → 开一次蛰伏 → 结束回合。
            // 牌全部留手（ShouldHold 管），技能这一下不与任何出牌抢顺序。
            if (core.TurnEngine.TurnNumber <= 1)
                return core.ElementPool.GetPooledCards(me).Any(pc => pc != null && !pc.IsTapped);
            if (CanOverflowTrigger(core, me)) return false; // 差值踩得到档：本回合继续正常花
            // 新分支（2026-11-18 用户定案）：**已经 4 地、仍达不到充盈** → 手里有寒冬就直接蛰伏
            // （横置 4 地 → 下回合 4 地 + 4 临时 = 8 费，够打 6 费寒冬强行启动）。
            bool winterReady = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                .Any(hc => hc != null && CardNameOf(hc) == "寒冬" && !core.StackEngine.IsPendingCast(hc));
            if (winterReady && (core.ElementPool.GetPooledCards(me)?.Count ?? 0) >= 4)
                return core.ElementPool.GetPooledCards(me).Any(pc => pc != null && !pc.IsTapped);
            // 潮汐优先：手里还有潮汐就先让它吃剩余元素去触发，蛰伏押后——否则横置后差值=0，整轮潮汐全空转。
            bool hasChaoXi = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                .Any(hc => hc != null && CardNameOf(hc) == "潮汐" && !core.StackEngine.IsPendingCast(hc));
            if (hasChaoXi) return false;
            // 2026-11-18 用户定案：循环打完，**手里有乱锋就留费用**（给对手回合的响应），没有才蛰伏。
            bool hasLuanFeng = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                .Any(hc => hc != null && CardNameOf(hc) == "乱锋" && !core.StackEngine.IsPendingCast(hc));
            if (hasLuanFeng) return false;
            return core.ElementPool.GetPooledCards(me).Any(pc => pc != null && !pc.IsTapped);
        }

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

        // ==================== 充盈碑阵（2026-11-13 蓝套改版） ====================

        /// <summary>充盈碑（潮痕碑=充盈·2伤 / 深潭碑=充盈·召唤潮痕碑）：带 ManaSurplus 引擎的场上卡。
        /// 判据走 CardHasAtom 同源的「主序列 + 槽级 branch.Then」扫描（手牌/战场两态同口）。</summary>
        private static bool IsFullnessMonument(Card c)
            => c != null && c.IsAlive && HasBranchAtom(c, AtomicEffectType.EngineManaSurplus);

        /// <summary>碑的奖励类型：伤害碑（Then=造成伤害）/复制碑（Then=回响复制）。</summary>
        private static bool MonumentIsDamage(Card c) => HasBranchAtom(c, AtomicEffectType.DealDamage);
        private static bool MonumentIsCopy(Card c) => HasBranchAtom(c, AtomicEffectType.EchoCopy);

        /// <summary>己方场上某一类碑的数量（damage=true 伤害碑 / false 复制碑）——回响选目标
        /// 「哪类少补哪类」用。</summary>
        private static int CountOwnMonuments(GameCore core, Player me, bool damage)
        {
            int n = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
            {
                if (!IsFullnessMonument(c)) continue;
                if (damage ? MonumentIsDamage(c) : MonumentIsCopy(c)) n++;
            }
            return n;
        }

        /// <summary>己方墓地里的碑数量——回潮洗回价值（碑阵续航靠「洗回→再抽→再下」）。</summary>
        private static int CountGraveMonuments(GameCore core, Player me)
        {
            int n = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>())
                if (HasBranchAtom(c, AtomicEffectType.EngineManaSurplus)) n++;
            return n;
        }

        /// <summary>潮痕碑 2 伤的目标（2026-11-18 用户定案）：**优先解场**——对方场上单位按攻击力
        /// 从高到低；对方没有单位时才打角色（角色放在最后）。</summary>
        private static readonly Entity[] None = Array.Empty<Entity>();

        private static List<Entity> OrderDamageTargets(GameCore core, Player me, List<Entity> list)
        {
            // 2026-11-18 用户定案：**结界也是合法攻击目标**——解场目标包含对方战场上的结界（攻击力 0，排在生物后），
            var opp = me?.Opponent;
            // 对方场上没有牌时才打角色。
            return list.OrderByDescending(e =>
            {
                if (e is Card c && c.IsAlive && c.GetZone() == Zone.Battlefield && ReferenceEquals(c.GetController(), opp))
                    return 1000 + (c.IsLivingUnit() ? (core?.LayerEngine?.CalculatePower(c) ?? 0) : 0);
                return 0; // 角色/其他垫底
            }).ToList();
        }

        /// ① 两类碑都要续：场上数量少的一类优先（相等时先补伤害碑）；
        /// ② 己方碑略高于对面碑（同名复制，先拿自己的；镜像局对面碑也能补给）；
        /// ③ 耐久 ≤ 单次触发成本的碑最优先（下一次触发就碎，复制体=满耐久接力）；
        /// ④ 没有碑可选才兜底复制对面的大身材单位（多一份战力），己方普通卡垫底。
        /// 复制体带「遗忘」，回合末不在战场即出局——**必须在当回合打出去**。</summary>
        private static int EchoCopyScore(GameCore core, Player me, Entity e)
        {
            if (!(e is Card c) || !c.IsAlive) return int.MinValue;
            bool own = ReferenceEquals(c.GetController(), me);
            if (!IsFullnessMonument(c))
            {
                int power = c.IsLivingUnit() ? core.LayerEngine.CalculatePower(c) : 0;
                int life = c.IsLivingUnit() ? c.GetLife() : 40;
                return (own ? 0 : 120) + power + life;
            }
            bool dmg = MonumentIsDamage(c);
            int dmgCount = CountOwnMonuments(core, me, damage: true);
            int copyCount = CountOwnMonuments(core, me, damage: false);
            bool wanted = dmg ? dmgCount < copyCount : copyCount <= dmgCount;
            int durability = c.GetCounterCount(CounterRules.DurabilityCounter);
            int score = 3000 + (wanted ? 400 : 0) + (own ? 120 : 0);
            // 耐久 ≤ 单次触发成本：这块碑下一次触发就碎——复制体=满耐久接力。**复制碑（深潭碑）优先保住**：
            // 回响源一断，两类碑都补不进手牌，整个循环停摆。
            if (durability <= CounterRules.AutoEffectDurabilityCost) score += dmg ? 250 : 700;
            return score;
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
            // 2026-11-18 用户定案：冻结/乱锋多余件优先铺地（留 1 张 key，其余转费源）。
            if (IsControlKey(card))
            {
                string name = CardNameOf(card);
                int copies = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                    .Count(c => c != null && CardNameOf(c) == name);
                if (copies > 1) return 20000f;
            }
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
            // 2026-11-18 用户定案（**全局固定序**）：T1 = 只下 1 地，跳过所有操作，直接结束回合。
            if (core.TurnEngine.TurnNumber <= 1) return true;

            // 2026-11-18 用户定案：**冻结/乱锋只在对手回合用**（响应）——自己回合留手，
            // 不当作普通解场牌打出去（冻结只冻对方地牌、乱锋只控生物，见 PickResponse/PickSpellTargets）。
            // 例外：**寒冬**是己方回合的强开工具（T4 蛰伏 → 下回合 6 费寒冬冻住对方全部地），不留手。
            // 2026-11-18 用户定案：寒冬不是 key 牌——优先铺成地（见 ShouldLand）。
            if (CardNameOf(card) != "寒冬" && HasBranchAtom(card, AtomicEffectType.Freeze)
                || HasBranchAtom(card, AtomicEffectType.RushSickness))
                return true;

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
            if (Environment.GetEnvironmentVariable("TIDE_DEBUG_RESP") == "1") { EffectInstance t0 = null; foreach (var o0 in core.StackEngine.GetStackContents() ?? new List<EffectInstance>()) if (o0 != null) t0 = o0; Console.WriteLine($"[dbg-resp-in] me={(ReferenceEquals(me, core.Player1) ? "P1" : "P2")} turn={(ReferenceEquals(core.TurnEngine.TurnPlayer, me) ? "self" : "opp")} phase={core.TurnEngine.CurrentPhase?.Phase} top={(t0?.Controller == null ? "-" : (ReferenceEquals(t0.Controller, core.Player1) ? "P1" : "P2"))} opts={options?.Count ?? 0} enemyPow={EnemyTotalPower(core, me?.Opponent)} luanFengSeen={options != null && options.Any(o => o != null && o.SourceCard != null && CardNameOf(o.SourceCard) == "乱锋")}"); }
            if (options == null || options.Count == 0) return null;
            if (Environment.GetEnvironmentVariable("TIDE_DEBUG_RESP") == "1")
            {
                var names = string.Join(",", options.Where(o => o != null && o.Kind == ResponseOption.ResponseKind.HandCard && o.SourceCard != null).Select(o => CardNameOf(o.SourceCard)));
                Console.WriteLine($"[dbg-resp] opts={options.Count} hand=[{names}]");
            }
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
            ResponseOption bounce = null, freeze = null, disrupt = null, luanFeng = null, directDamage = null;
            ResponseOption spellban = null; // 组3 无实体候选（宣攻专道专用）
            int directDamageValue = 0;
            bool oppHasCreature = false;
            foreach (var e2 in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                if (e2 != null && e2.IsAlive && e2.IsLivingUnit()) { oppHasCreature = true; break; }
            int oppHand = (core.ZoneManager.GetCards(opp, Zone.Hand) ?? new List<Card>()).Count;

            foreach (var o in options)
            {
                // 2026-11-18：乱锋可能以 ActivatedAbility 候选出现（不走 HandCard 分支）——先按卡名抓，再走 Kind 过滤
                if (o != null && o.SourceCard != null && luanFeng == null && CardNameOf(o.SourceCard) == "乱锋" && EnemyTotalPower(core, opp) >= 4) luanFeng = o;
                if (o == null || o.Kind != ResponseOption.ResponseKind.HandCard || o.SourceCard == null) continue;
                // 组3 无实体卡（幻壁·GrantSpellban，2026-11-02 蓝套v3 用户口径）：剥离通用候选池，
                // 只走下方宣攻专道——不在自家/非宣攻窗口被「低费换高费」当燃料烧掉。
                bool isSpellban = false;
                foreach (var atom in ModeAtoms(o.SourceCard, o.ModeIndex))
                    if (atom != null && atom.Type == AtomicEffectType.GrantSpellban) { isSpellban = true; break; }
                if (isSpellban) { if (spellban == null) spellban = o; continue; }
                int cost = TotalCostOf(core, o.SourceCard, o.ModeIndex);
                // 2026-11-18：按卡名识别乱锋（原子的 ModeAtoms 走不到分支行，曾导致响应层永远选不中）
                if (luanFeng == null && CardNameOf(o.SourceCard) == "乱锋" && EnemyTotalPower(core, opp) >= 4) luanFeng = o;
                if (cost < cheapestCost) { cheapestCost = cost; cheapest = o; }
                foreach (var atom in ModeAtoms(o.SourceCard, o.ModeIndex))
                {
                    if (atom == null) continue;
                    if (atom.Type == AtomicEffectType.ReturnToHand && bounce == null && oppHasCreature) bounce = o;
                    else if (atom.Type == AtomicEffectType.RushSickness && luanFeng == null
                             && EnemyTotalPower(core, opp) >= 4) luanFeng = o; // 乱锋：只在对手回合、对方总攻 >= 4
                    else if (atom.Type == AtomicEffectType.Freeze && freeze == null
                             && HasEnemyPoolLand(core, opp)) freeze = o; // 2026-11-18：有对方地牌即合格
                    else if (disrupt == null && oppHasCreature && IsDisruptAtom(atom.Type)) disrupt = o; // 2026-11-18：有生物即合格
                    else if (atom.Type == AtomicEffectType.DealDamage && directDamage == null)
                    {
                        int dmg = atom.Value > 0 ? atom.Value : 1;
                        if (dmg >= opp.Life) { directDamage = o; directDamageValue = dmg; }
                    }
                }
            }

            // 2026-11-18 用户定案：**乱锋只在对手回合用，且对手场上攻击力合计 > 4 才用**
            //（小场面不值得交控制，留给真正压血的回合）。
            if (luanFeng != null)
            {
                // 乱锋：把目标钉死——对方场上攻击力最高的 3 个（不足 3 个就全给）
                var lfTargets = (core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                    .Where(c => c != null && c.IsAlive && c.IsLivingUnit())
                    .OrderByDescending(c => core.LayerEngine?.CalculatePower(c) ?? 0)
                    .Take(3).Cast<Entity>().ToList();
                luanFeng.Targets = lfTargets;
                if (Environment.GetEnvironmentVariable("TIDE_DEBUG_RESP") == "1") Console.WriteLine($"[dbg-resp-pick] 乱锋 targets={lfTargets.Count}");
                return luanFeng;
            }
            if (disrupt != null && EnemyTotalPower(core, opp) >= 4) return disrupt;
            if (freeze != null && HasEnemyPoolLand(core, opp)) return freeze; // 冻结：只冻对方地牌

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

            // 乱锋：优先控攻击力最高的对方单位（2026-11-18 用户定案）——目标按攻击力降序。
            if (req.PromptAtom == AtomicEffectType.RushSickness)
                return list.OrderByDescending(e => e is Card u && u.IsAlive && u.IsLivingUnit()
                        ? (core.LayerEngine?.CalculatePower(u) ?? 0) : -1).ToList();

            // 潮痕碑 2 伤：**优先解场**（对方单位按攻击力降序），对方没单位才打角色。
            if (req.PromptAtom == AtomicEffectType.DealDamage)
                return OrderDamageTargets(core, me, list);

            // 回响落点（2026-11-13 蓝套改版）：充盈触发时优先复制两类碑（伤害/复制），把引擎续进手牌。
            if (req.PromptAtom == AtomicEffectType.EchoCopy)
                return list.OrderByDescending(e => EchoCopyScore(core, me, e)).ToList();

            // 蛰伏目标（2026-11-18 改版，替换隐匿潜行排序）：任意档自动全取——截断到
            // **己方未横置地牌**（已横置的再蛰伏无收益，handler 也会跳过，别占目标位）。
            if (req.PromptAtom == AtomicEffectType.Dormancy)
                return list.OfType<Card>()
                    .Where(c => IsUntappedPoolLand(core, me, c))
                    .Cast<Entity>()
                    .ToList();

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

