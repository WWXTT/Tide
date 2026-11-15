using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI
{
    /// <summary>
    /// 绿·慢速 AI（2026-11-15 按用户口径改版）：
    /// **轮转引擎**（用户口径，手牌不够用的根因）：
    ///   · T3 线：铺 3 地 → 小精灵（首卡，给手牌沃土 −2 绿 = 6→4）→ 牺牲**已横置的地**（+1 彩、腾出 1 槽）
    ///     → 手牌补一块地（+1 绿）→ 3 绿 + 1 彩 = 4 费把沃土开出来；
    ///   · T5 线（减半循环，2026-11-15 用户口径）：小精灵（首卡，给**归根** −2 → 4→2 绿）→ 牺牲小精灵
    ///     （+1 彩，进墓后正是归根的捞回对象）→ 归根回收 3 张 → **落地 1**（腾出的牌位补成地，+1 费）；
    ///     手里只留「小精灵 / 归根 / 减半」各一张，地每回合 +1，没抽到暗影豹就一直循环；
    ///     减半**只在对手回合的响应窗贴自己**（自己回合贴 = 回合末清层白给）；
    ///   · 关键：**牺牲腾出的槽位要用手牌立刻补回来**（否则地数掉档、牌白烧），墓地那批再用归根捞回手。
    /// 中期：虚化（9 费保命，无实体钳伤害到 1）最优先；枯竭**全取**（对手元素池有几块抹几块）。
    /// 后期：暗影豹收尾；地铺到 20 后靠成长与归根维持每回合「回收 + 大生物」。
    /// 卡组（60）：小精灵×15 / 沃土×20 / 归根×15 / 枯竭×2 / 虚化×3 / 暗影豹×5（萌芽已退组）；技能=牺牲。
    /// </summary>
    public sealed class GreenRampStrategy : AiStrategy
    {
        private const string NameSpirit = "小精灵";   // 1/1 无攻守；登场己方角色 +2 甲；首卡 → 手牌 −2 绿
        private const string NameSprout = "萌芽";     // 成长 1 + 首卡减费
        private const string NameBloom = "沃土";      // 成长 3；代价：一个敌方单位获得6层护甲（净 6 绿）
        private const string NameReturn = "归根";     // 回收 3 张
        private const string NameEraseLand = "枯竭";  // 抹除对方元素池地牌（2026-11-15 起=全取）
        private const string NamePanther = "暗影豹";  // 9/9 攻守 狩猎+吸血
        private const string NameVeil = "虚化";       // 赋予无实体（钳伤害到 1）；代价：敌方 +6 甲（净 6 绿）
        private const string NameHalve = "减半";      // 赋予减半（受到的伤害减半）；代价：敌方 +6 甲（净 2 绿）
        private const string NameBeast = "荒原猎兽";  // 5/5 攻击 系命+狩猎（灰4红3绿3 = 10 费）

        /// <summary>核心组件（全取抹除 + 虚化 + 暗影豹）——不当土地烧掉。</summary>
        private static readonly HashSet<string> CoreNames = new HashSet<string>
        {
            // 荒原猎兽（2026-11-15 修 bug）：此前不在核心件里 → 会被 PlayLands 当土地铺掉，
            // 手牌里永远留不住、T5 之后一次都打不出来。
            NameEraseLand, NameVeil, NamePanther, NameBeast,
        };

        private static readonly HashSet<string> EraseNames = new HashSet<string> { NameEraseLand };

        /// <summary>成长引擎：前期能打就打。</summary>
        private static readonly HashSet<string> RampNames = new HashSet<string> { NameSprout, NameBloom };

        /// <summary>地牌门槛（门=操控地数量≥9；9 地后减费/护甲门开启，也是**虚化 9 费保命线**）。</summary>
        private const int GateLands = 9;
        /// <summary>暗影豹总费 14（8 灰+3 红+3 绿）——钱不够先留手。</summary>
        private const int PantherMinTotal = 12;
        /// <summary>成长停手线（2026-11-14 用户定案）：一路成长到 **20 地**才停——20 地后每回合能稳定
        /// 打出一组「归根 + 暗影豹」。</summary>
        private const int LandTarget = 20;
        /// <summary>早期补地窗口（2026-11-14 定案）：6 费之前除了成长，也允许拿手牌补地。</summary>
        private const int EarlyLandDump = 6;
        /// <summary>手牌铺地上限（2026-11-15 用户口径「轮转」）：牺牲会腾出地牌槽，腾出后要立刻用**手牌**
        /// 补一块回来（一张地出两份费）；这套牌一路铺到 12 地才停手牌铺地，之后全靠成长。
        /// 12 之后手牌只用来打成长/回收，不再当土地烧。</summary>
        private const int LandMidCap = 12;
        /// <summary>6 地之前**每回合必须用手牌把地铺满**（2026-11-15 用户口径：这是运转的地基；
        /// 地数不够 3 块以上，沃土 6 费根本开不出来）。</summary>
        private const int EarlyFill = 6;

        public override string Key => "green";
        public override string DisplayName => "绿慢速";

        // ======================================== 技能：牺牲 ========================================

        /// <summary>技能=牺牲（自选**任意数量**己方单位/地牌送墓，每张 +1 临时彩元素）。
        /// 三种时机：① 地牌池里躺着核心组件（被成长随机拉进去的）→ 全祭掉救人；
        /// ② **轮转补费**（2026-11-15 用户口径）：`横置地 → 牺牲得 1 彩（腾出 1 槽）→ 手牌补一块地`，
        ///    一张地出两份费，专门用来凑「就差 1~3 点」的成长/保命件；
        /// ③ 槽位已满（铺不进新地）时同理：祭一块横置地，下一步 PlayLands 立刻补回来。</summary>
        public override bool WantHeroSkill(GameCore core, Player me)
        {
            if (!base.WantHeroSkill(core, me)) return false;
            // 固定脚本：T5 的「牺牲场上小精灵」是循环的第一步，必须无条件发动（不看费缺口）。
            if (InScriptTurn(core, me, out int scriptTurn) && scriptTurn == 5
                && HasSpiritOnField(core, me)) return true;
            // 脚本 T7（2026-11-15 第二版）：豹已经付得出（PantherReady）才进 T7 脚本——这时候
            // **不需要再牺牲凑费**（祭 4 补 4 那套正是用户要去掉的手牌浪费），所以这里不发动牺牲。
            if (InScriptTurn(core, me, out int t7) && t7 == 7) return false;
            // 脚本 T3：**必须等「小精灵 + 沃土」把地横置出 2 块以上才牺牲**（用户推导的顺序），
            // 否则一上来就祭会把 暗影豹/减半 这类材料白烧掉、沃土反而没费打。
            if (InScriptTurn(core, me, out int t3) && t3 == 3)
                return TappedLands(core, me) >= 2 && HandCopies(core, me, NameReturn) > 0;
            if (CoreStuckInPool(core, me).Count > 0) return true;   // 救核心
            int need = ShortfallNeed(core, me);                     // 手里最接近可打的那张还差几点
            if (TotalLands(core, me) < 3) return false;             // 地没铺起来之前不烧（3 地=用户口径的 T3 线）
            // 用户口径优先级（2026-11-15）：**小精灵+回收+牺牲+生物 ＞ 小精灵+牺牲+生物**——
            // 所以只要「成长」或「可召唤生物」差 1~4 点，就允许牺牲凑费（回收在这条链里是加费器）。
            bool bloomFuel = TotalLands(core, me) < GateLands
                             && HandCopies(core, me, NameBloom) > 0 && BloomShort(core, me);
            bool beastFuel = TotalLands(core, me) < 14 && BeastShort(core, me);
            if (!bloomFuel && !beastFuel) return false;
            if (need > 4) return false;
            int fodder = FodderCount(core, me);                     // 横置地 + 场上小精灵（+ 要垫彩时的未横置地）
            if (fodder <= 0) return false;
            // 轮转口径（2026-11-15）：祭掉横置地能腾 1 槽，下一步 PlayLands 用手牌补回来（+1 费）；
            // 所以「能补到的费」= 可祭张数（每张 +1 彩）+ 手里可当土地打的张数。
            return need <= fodder + LandInHand(core, me);
        }

        /// <summary>攻击目标（2026-11-15 用户口径）：**系命单位只打「护甲吃不满」的目标**——
        /// 否则伤害全被护甲吃掉，一点血都吸不到。优先打脸（角色没护甲时），其次挑护甲 &lt; 攻击力的生物。</summary>
        public override Entity PickAttackTarget(GameCore core, Player me, Card unit, Player opp)
        {
            if (unit == null || opp == null) return base.PickAttackTarget(core, me, unit, opp);
            var combat = core.CombatSystem;
            if (unit.HasKeyword(KeywordRules.Lifelink))
            {
                int power = core.LayerEngine.CalculatePower(unit);
                int oppArmor = opp.GetEffectiveCounterCount(KeywordRules.ArmorCounter);
                if (oppArmor < power && combat.CanAttackTarget(unit, opp, me)) return opp;
                Entity best = null;
                int bestHeal = 0;
                foreach (var c in core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>())
                {
                    if (c == null || !c.IsAlive || !c.IsLivingUnit()) continue;
                    if (!combat.CanAttackTarget(unit, c, me)) continue;
                    int armor = c.GetEffectiveCounterCount(KeywordRules.ArmorCounter);
                    int heal = Math.Max(0, power - armor);
                    if (heal > bestHeal) { bestHeal = heal; best = c; }
                }
                if (best != null) return best;
            }
            return base.PickAttackTarget(core, me, unit, opp);
        }

        /// <summary>主动技能闸（2026-11-15 用户口径）：**「牺牲」只在脚本回合（T5/T7）允许发动**，
        /// 其它回合一律拦下——启发式会一次烧 3 张（小精灵×2 + 归根），把循环材料全烧光。</summary>
        public override bool AllowVoluntaryActivation(GameCore core, Player me, Card source, string effectName)
        {
            bool isSacrifice = (effectName != null && (effectName.Contains("牺牲") || effectName.Contains("送墓")))
                               || (source != null && NameOf(source) == "牺牲");
            if (!isSacrifice) return true;
            if (!InScriptTurn(core, me, out int scriptTurn)) return false;
            // 脚本 T3：必须已有 ≥2 块横置地（小精灵+沃土付过费）才准祭，否则会把材料白烧掉。
            if (scriptTurn == 3) return TappedLands(core, me) >= 2;
            // 脚本 T5（2026-11-15 循环）：牺牲的唯一材料是**场上小精灵**；没有小精灵就整条闸关死——
            // 否则自愿队列会在「手里没小精灵」的回合照样发动，把地池里的地当材料烧掉（实测 T5 地 8→7）。
            if (scriptTurn == 5) return HasSpiritOnField(core, me);
            // 脚本 T7（2026-11-15 第二版）：豹已付得出 → 不需要牺牲凑费，闸直接关死。
            if (scriptTurn == 7) return false;
            return false;
        }

        /// <summary>救核心要先于出牌（否则核心地牌会被当成普通地继续产色/被忽略）。</summary>
        public override bool SkillBeforeCards(GameCore core, Player me)
        {
            // 脚本 T7（2026-11-15 用户口径）：**先出牌、后牺牲**——必须先打小精灵+回收把 3 块地横置出来，
            // 牺牲才拿得到「已横置的地」；否则一上来就祭，只能祭到没横置的地（净 0）或小精灵（+1）。
            // 2026-11-15：脚本回合一律「先出牌、后牺牲」（T3 也一样——先打小精灵+沃土把地横置出来）。
            if (InScriptTurn(core, me, out _)) return false;
            return CoreStuckInPool(core, me).Count > 0;
        }

        /// <summary>响应窗（2026-11-15 用户口径）：**减半必须在对手回合打出**。
        /// 指示物是「自己回合结束清一层」——自己回合上等于白给（撑不到对手回合）。
        /// 所以只在对面的回合、且有响应候选（手牌里的减半）时出手；目标=己方角色。</summary>
        public override ResponseOption PickResponse(GameCore core, Player me, List<ResponseOption> options)
        {
            if (options == null || options.Count == 0) return null;
            if (core.TurnEngine == null || ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return null; // 只打对手回合
            var opp = me?.Opponent;
            if (opp == null) return null;

            ResponseOption pick = null;
            foreach (var o in options)
            {
                if (o == null || o.Kind != ResponseOption.ResponseKind.HandCard || o.SourceCard == null) continue;
                if (NameOf(o.SourceCard) != NameHalve) continue;
                pick = o;                       // 减半在手 → 就在这个窗口上给自己
                break;
            }
            if (pick == null) return null;

            // 结算期目标反问按本策略排序（己方角色优先，故队友/小精灵不会被误选）
            TargetSelectionService.AiOrder = req => AiStrategy.DispatchOrder(this, core, req?.Chooser ?? me, req);
            return pick;
        }

        // ======================================== 出牌优先级 ========================================

        // ======================================== 固定回合脚本（2026-11-15 用户口径） ========================================
        // 用户给的三条线是**固定操作序列**（启发式会把小精灵/归根当牺牲材料烧掉），
        // 所以「有对应卡时走脚本，没有则回落启发式」：
        //   T3：小精灵 → 沃土（卡齐才走）
        //   T5：小精灵（首卡，−2 给归根）→ 牺牲小精灵（+1 彩）→ 归根（4−2=2，捞回 3 张）→ 落地 1
        //   T7：小精灵 → 牺牲 4 → 落 4 → 暗影豹（卡齐才走）
        // 脚本只改「分数/牺牲目标」，动作仍由引擎逐个执行，因此不会与费用/目标域校验冲突。
        // 2026-11-15 补（T5 循环落地）：脚本回合内 小精灵/归根/减半 各留一张在手里（多册才铺地），
        // 归根一回合只打一张且墓地 ≥3 才打（否则白扔 4 费、把对手回合贴减半的钱烧光）。

        /// <summary>元素池里**已横置**的地（按 Card 实例集合；池的横置状态记在 PooledCard 上，不在 Card 上）。</summary>
        private static HashSet<Card> TappedPoolCards(GameCore core, Player me)
        {
            var set = new HashSet<Card>();
            foreach (var pc in core.ElementPool.GetPooledCards(me))
                if (pc != null && pc.IsTapped && pc.SourceCard != null) set.Add(pc.SourceCard);
            return set;
        }

        /// <summary>自方地牌池里已横置的地张数（脚本判定「够不够祭」用）。</summary>
        private static int TappedLands(GameCore core, Player me)
            => core.ElementPool.GetPooledCards(me).Count(pc => pc != null && pc.IsTapped);

        /// <summary>自方场上是否还有小精灵（脚本 T5 的牺牲材料）。</summary>
        private static bool HasSpiritOnField(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Any(c => c != null && c.IsAlive && NameOf(c) == NameSpirit);

        /// <summary>当前是自方第几个回合（全局回合 → 自方回合，先手 = 奇数全局回合）。</summary>
        private static int OwnTurnIndex(GameCore core)
            => core?.TurnEngine == null ? 0 : (core.TurnEngine.TurnNumber + 1) / 2;

        /// <summary>当前回合是否处于某个脚本线（且所需卡在手里）。</summary>
        private static bool InScriptTurn(GameCore core, Player me, out int scriptTurn)
        {
            scriptTurn = 0;
            if (core.TurnEngine == null || !ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return false;
            // 用户口径的「T3/T5/T7」= **全局回合号**（先手方 = 奇数回合）。
            int own = core.TurnEngine.TurnNumber;
            if (!ReferenceEquals(core.Player1, me)) return false;      // 脚本只对先手方（绿先手）
            if (own == 3) { scriptTurn = 3; return true; }
            if (own == 5) { scriptTurn = 5; return true; }
            // 2026-11-15 用户口径（第二版）：**不再强求 T7 出豹**——手里有豹、且这一拍真付得出
            // （地+临时彩 ≥12）才进豹脚本；付不出就继续走 T5 的减半循环长地，缓 1~2 个回合再出，
            // 这样不用为了凑 12 费一次祭 4 张（省手牌）。
            if (own >= 7 && HandCopies(core, me, NamePanther) > 0 && PantherReady(core, me))
            { scriptTurn = 7; return true; }
            // 豹还没到位（或不在手）：7 回合后继续走 T5 的减半循环（小精灵/牺牲/回收/落地）。
            if (own >= 7) { scriptTurn = 5; return true; }
            return false;
        }


        /// <summary>暗影豹这一拍打不打得出去（操控地 + 临时彩 ≥ 12）——「缓一回合」的判据：
        /// 不 ready 就让减半循环继续长地（每回合 +1~2），**不为了 T7 硬凑 12 费去祭 4 张**（省手牌）。</summary>
        private static bool PantherReady(GameCore core, Player me)
        {
            var pool = core.ElementPool.GetPool(me);
            int temp = pool?.TempElements != null ? pool.TempElements[ManaType.Any] : 0;
            return TotalLands(core, me) + temp >= PantherCost;
        }
        /// <summary>脚本内的卡片优先级（数字越大越先打）。</summary>
        private static float ScriptScore(GameCore core, Player me, int scriptTurn, string name, bool firstCard, bool spiritOnField)
        {
            switch (scriptTurn)
            {
                case 3:
                    if (name == NameSpirit && firstCard) return 9900f;
                    if (name == NameBloom) return 9800f;
                    if (name == NameReturn) return 9700f;      // 用户推导：T3 收尾是「回收」
                    break;
                case 5:
                    if (name == NameSpirit && firstCard) return 9900f;
                    // T5 循环的「回收」：墓地 ≥3 张才打（回收 3 才是纯收益）——不够的回合再打第二张
                    // 只是把 4 费扔掉（实测 T5 二次归根捞 1 张、把留给对手回合贴减半的费烧光）。
                    if (name == NameReturn) return GyCount(core, me) >= 3 ? (spiritOnField ? 9850f : 9800f) : 150f;
                    break;
                case 7:
                    if (name == NameSpirit && firstCard) return 9900f;
                    if (name == NameReturn) return 9800f;
                    if (name == NamePanther) return 9700f;
                    break;
            }
            return 0f;
        }

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            string name = NameOf(card);
            if (name == null) return totalCost(card);

            // 固定脚本优先（有对应卡才进脚本；否则完全走原启发式）
            if (InScriptTurn(core, me, out int scriptTurn))
            {
                // T7 暗影豹：**费一够立刻落地**（高于一切）——循环/补地都是为它服务，
                // 见底的费被下一张归根/沃土吃掉就永远出不来（2026-11-15 实测：12 地仍 0 次施放）。
                if (scriptTurn == 7 && name == NamePanther && GameActions.CanAffordCast(core, me, card, 0))
                    return 9990f;
                bool first = IsFirstCardThisTurn(core, me);
                bool spiritOnField = HasSpiritOnField(core, me);
                float s = ScriptScore(core, me, scriptTurn, name, first, spiritOnField);
                if (s > 0f) return s;
            }

            if (RampNames.Contains(name))
            {
                // 成长是引擎（2026-11-15 用户定案：中期目标=先凑 9 费出虚化保命）：
                // 9 地之前成长最高优先（尽快到 9 费）；9 地后让位给虚化/枯竭/暗影豹。
                int lands = TotalLands(core, me);
                bool holdsKey = HandCopies(core, me, NamePanther) > 0;
                float ramp;
                if (lands < EarlyLandDump) ramp = 5600f;                       // 起步：先铺地
                else if (lands < GateLands) ramp = 5400f;                      // 冲 9 费（虚化门槛）
                else if (holdsKey && lands < 11) ramp = 3000f;                 // 手里有暗影豹：冲 11 费
                else ramp = 2200f;                                             // 中期：让位给保命/清场
                if (lands >= LandTarget) ramp = 1200f;                          // 20 地后收手
                // 首卡/减费链：本回合还没出牌的萌芽/沃土最赚（首卡门 +2 层减费）
                if (IsFirstCardThisTurn(core, me)) ramp += 800f;
                return ramp;
            }

            if (name == NameSpirit)
            {
                // 小精灵（2026-11-15 用户定案：**每回合只要有就必须是第一张牌**，从头到尾不变）——
                // 首卡全额优先（8000），把 −2 层减费交出去；不是首卡时只是普通 1 费 4 甲填线。
                if (IsFirstCardThisTurn(core, me)) return 8000f;
                return TotalLands(core, me) >= GateLands ? 1200f : 400f;
            }

            if (EraseNames.Contains(name))
            {
                // 2026-11-15 枯竭改为**全取**：对手元素池有几块抹几块——池里有地就打，
                // 不再按「对手哪类最多」挑目标（凋零/溃散退组后那套多选逻辑一并删除）。
                return OpponentLands(core, me) > 0 ? 6200f : 200f;
            }

            if (name == NameReturn)
            {
                // 归根=轮转的回收口（2026-11-15 用户口径）：**只要费够，穿插回收是纯收益**——
                // 「回收 + 牺牲 + 生物」能整链打出来时，回收排到最前（6500），让这一手既捞牌又凑费。
                if (CanChainReturnBeast(core, me)) return 6500f;
                int coreGy = CoreInGraveyard(core, me);
                if (coreGy > 0) return 3400f;
                int gy = GyCount(core, me);
                if (gy >= 2) return 2400f;
                return 300f;
            }

            if (name == NamePanther)
            {
                // 暗影豹=终端（2026-11-15）：地够 + 彩元素够垫红费 → 最高档，落地后靠归根反复召回。
                int lands = TotalLands(core, me);
                if (lands >= 14) return 7000f;
                return lands >= GateLands ? 3400f : 1600f;
            }

            if (name == NameBeast)
            {
                // 荒原猎兽（5/5 系命+狩猎，8 费纯绿）：2026-11-15 用户口径——**首次召唤优先**，
                // 场面压力减轻之后才回去继续成长；费够时优先走「回收+牺牲+生物」那条纯收益链。
                int lands = TotalLands(core, me);
                if (CanChainReturnBeast(core, me) || CanChainBeast(core, me)) return 6300f;
                if (!HasSummonedBeast(core, me)) return lands >= 6 ? 6200f : 4000f;
                return 5200f;
            }

            if (name == NameVeil)
            {
                // 虚化（净 6 绿，2026-11-15 代价化）：够费就出保命；但让位给沃土（成长引擎优先），
                // 血量吃紧（≤15）才反超一切、先贴无实体。
                if (AvailableMana(core, me) < VeilMana) return 800f;
                return me.Life <= 15 ? 7000f : 5000f;
            }

            if (name == NameHalve)
            {
                // 减半（2026-11-15 用户口径）：指示物在**自己回合结束清一层**，所以自己回合打=白给。
                // 只在**对手回合**的响应窗里打（见 PickResponse）；自己回合一律不打。
                if (core.TurnEngine != null && ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return 0f;
                if (AvailableMana(core, me) < HalveMana) return 600f;
                return me.Life <= 18 ? 6600f : 3600f;
            }

            return totalCost(card);
        }

        /// <summary>攒牌闸（用户口径「关键牌抽到留一张在手里」）：
        /// 枯竭——对手元素池没有地时留手（全取不需要挑类型，有地就打）；
        /// 暗影豹——地/钱没到位（&lt;见 PantherMinTotal）先留手，别当费牌打掉；
        /// 归根——墓地没有核心组件、且墓地不足 2 张时留手。</summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
        {
            // 脚本回合（T3 收尾回收 / T5 循环）：回收必须能落地，不能被动「墓地不足 2 张」的攒牌闸拦住。
            // 但 T5 循环（2026-11-15）多一条：**同一回合的第二张归根不打**——墓地只剩 1 张时打回收
            // 等于白扔 4 费，把留给对手回合贴减半的钱花光（实测 T5 第二次归根只捞 1 张）。
            if (InScriptTurn(core, me, out int scriptTurn) && NameOf(card) == NameReturn)
            {
                if (scriptTurn == 5 || scriptTurn == 7) return GyCount(core, me) < 3;
                return false;
            }
            // T7 暗影豹：费一够就打出去——旧的「地 <11 先留手」会正好卡在「牺牲+补地后
            // 够 12 费」这一拍上，豹被攒在手里直到回合结束（2026-11-15 探针实测 hold=True）。
            if (scriptTurn == 7 && NameOf(card) == NamePanther) return false;
            return ShouldHoldStatic(core, me, card);
        }

        // ======================================== 地牌 ========================================

        /// <summary>铺地口径（2026-11-15 用户口径「轮转」）：铺到 12 地为止，手牌是地牌的主要来源——
        /// 牺牲把横置地打进墓地 → 手牌补新地 → 归根再把墓地那批捞回手（卡不消耗，只是换区）。
        /// 不当土地：归根（回收口）与核心件（枯竭/虚化/暗影豹）；小精灵/沃土只在**多册**时拿一张去铺，
        /// 保证手里始终留一张能打（小精灵=首卡连招，沃土=成长引擎）。</summary>
        public override bool ShouldLand(GameCore core, Player me, Card card)
            => LandsAsFodder(core, me, card, NameOf(card));

        /// <summary>顶地数（2026-11-15）：主循环每轮都尝试把手牌铺到 12 地——牺牲腾出槽位后立刻补回来，
        /// 这是「一张地出两份费」轮转的前提。</summary>
        public override bool WantsLandTopUp(GameCore core, Player me)
            => TotalLands(core, me) < LandMidCap;
        public override bool WantLand(GameCore core, Player me, Card card)
            => LandsAsFodder(core, me, card, NameOf(card));

        /// <summary>这张牌能不能当土地打（ShouldLand/WantLand/LandInHand 共用单口径）：
        /// 12 地封顶；核心件（枯竭/虚化/暗影豹）与减半永不当土地；带了「费用降低（绿）」指示物的牌也不当；
        /// 小精灵/沃土/归根 各留一张，多册才铺——**手里实在没有别的可铺的牌**时允许拿归根兜底
        /// （14 张、还能被自己回收），否则地数会卡在 1~2 不动，成长永远开不出来。</summary>
        private static bool LandsAsFodder(GameCore core, Player me, Card card, string name)
        {
            if (name == null) return false;
            if (TotalLands(core, me) >= LandMidCap) return false;
            // 带「费用降低（绿）」指示物的牌**先于一切规则**保护——那是 T1/T3 小精灵叠出来的
            // 两层 −2（沃土 6→2 的唯一来源），铺成地等于把连招的核心烧掉（2026-11-15 实测：
            // T1 折扣过的那张沃土被 T3 的开局落地吃掉，导致 T3 沃土永远只能 4 费）。
            if (card != null && card.GetCounterCount(CounterRules.CostDownGreenCounter) > 0) return false;
            // 脚本回合（T3/T5/T7）：**归根与沃土都不当土地**——它们分别负责回收和成长，铺成地线就断了。
            // 例外（用户 T3 推导）：手里有 ≥2 张归根时，可以拿一张铺地（另一张留着打回收）。
            if (InScriptTurn(core, me, out int st))
            {
                // T5 循环（2026-11-15 用户口径）：手里**各留一张**小精灵 / 归根 / 减半——
                // 它们是下一回合「小精灵 → 牺牲 → 回收」的循环材料，铺成地等于把循环断掉。
                // 注意判定用「手牌张数」而不是「手牌+墓地+地池」：池里/墓地里的归根救不了本回合要打的这张
                //（旧口径 ReturnAvailable≥2 会把手里唯一一张归根铺掉，T5 回收永远打不出来）。
                if (name == NameReturn) return HandCopies(core, me, NameReturn) >= 2;
                if (name == NameSpirit) return HandCopies(core, me, NameSpirit) >= 2;
                // 减半：T3 当补地材料；T5 起要留给对手回合的响应窗，只有多册才铺。
                if (name == NameHalve) return st == 3 || HandCopies(core, me, NameHalve) >= 2;
            }
            // 脚本 T3：用户推导里「落地 2」用的是 未知牌 + 暗影豹——所以 T3 允许把 暗影豹/减半 铺成地，
            // 先把地数顶到 3（否则 1 块地连沃土都开不出来）。
            // 减半要留在手里（用户 T3 目标手牌），只有暗影豹可以铺成地凑地数。
            if (InScriptTurn(core, me, out int st3) && st3 == 3 && name == NamePanther) return true;
            if (CoreNames.Contains(name) || name == NameHalve) return false;  // 核心件/减半不当土地
            // 带了「费用降低（绿）」指示物的牌绝不当土地铺——那是小精灵首卡给的折扣，
            // 铺成地=把 2 层折扣直接吞掉（2026-11-15 用户抓到的 T3 连招打空）。
            if (card != null && card.GetCounterCount(CounterRules.CostDownGreenCounter) > 0) return false;
            // 2026-11-15 用户口径：**6 地之前每回合都要用手牌把地铺到上限**（运转的地基），
            // 但要给 T3 线留牌：小精灵/沃土各留一张（只有多册、或者手里真没别的可铺时才拿去铺）。
            if (TotalLands(core, me) < EarlyFill)
            {
                if (name == NameSpirit || name == NameBloom)
                    return HandCopies(core, me, name) > 1 || !OtherLandFodderExists(core, me, card);
                return true;
            }
            int copies = HandCopies(core, me, name);
            if (name == NameSpirit || name == NameBloom || name == NameReturn)
            {
                if (copies > 1) return true;
                return !OtherLandFodderExists(core, me, card);   // 兜底：没别的可铺就只能铺它（否则地数卡死）
            }
            return true;
        }

        /// <summary>手里除了这张之外，还有没有别的可当土地的牌（兜底判定用）。</summary>
        private static bool OtherLandFodderExists(GameCore core, Player me, Card exclude)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (c == null || ReferenceEquals(c, exclude)) continue;
                string n = NameOf(c);
                if (n == null) continue;
                if (CoreNames.Contains(n) || n == NameHalve) continue;
                if (c.GetCounterCount(CounterRules.CostDownGreenCounter) > 0) continue;
                if (n == NameSpirit || n == NameBloom || n == NameReturn)
                {
                    if (HandCopies(core, me, n) > 1) return true;
                }
                else return true;
            }
            return false;
        }

        /// <summary>手里还能当土地打的张数（轮转补费时 PlayLands 立刻能用上的额度）。</summary>
        private static int LandInHand(GameCore core, Player me)
        {
            int n = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                if (c != null && LandsAsFodder(core, me, c, NameOf(c))) n++;
            return n;
        }

        /// <summary>铺地优先级：先铺富余的成长件（沃土最多、其次萌芽），再铺小精灵，核心件垫底。</summary>
        public override float LandPriority(GameCore core, Player me, Card card, Func<Card, float> totalCost)
        {
            string name = NameOf(card);
            if (name == null) return 0f;
            if (CoreNames.Contains(name)) return 3000f;
            int copies = HandCopies(core, me, name);
            if (name == NameBloom) return 2200f + copies;    // 20 张，铺掉最不心疼（也是绿主色）
            if (name == NameSprout) return 1800f + copies;
            if (name == NameSpirit) return 1600f + copies;
            return 500f + copies;
        }
        /// <summary>绿地起步（成长/小精灵都要绿），够 3 块后补灰。</summary>
        public override ManaType? LandColorChoice(GameCore core, Player me, Card card)
            => CountLands(core, me, ManaType.Green) < 3 ? ManaType.Green : (ManaType?)null;

        // ======================================== 目标排序 ========================================

        public override List<Entity> OrderTargetCandidates(GameCore core, Player me, TargetSelectionRequest req)
        {
            var list = req?.Candidates;
            if (list == null || list.Count <= 1) return list;
            var atom = req.PromptAtom;

            // 技能「牺牲」（任意数量）：救援模式=只祭池中的核心组件；补费模式=按差额截断
            // （排序后的列表长度就是 AI 要祭的张数——TargetSelection 任意档取排序全部候选）。
            if (atom == AtomicEffectType.Sacrifice)
            {
                // 2026-11-15 共享规格 v0.2 §4：「地/池材料」判定收窄到**己方元素池组**
                //（CandidatesInZone 走 TargetZoneGrouping 单一源——横置的战场生物不再可能被当成地；
                // 小精灵按名挑是脚本材料语义，保持不动）。
                var poolSet = new HashSet<Entity>(CandidatesInZone(req, 0, 2));
                // 固定脚本 T5（2026-11-15 用户口径）：牺牲的是**场上小精灵**（不烧地、不烧手牌），
                // 让它进墓地后由「回收」捞回手——循环靠这一手转起来。
                // 候选里没有小精灵就返回空表（本次不选目标，绝不退回「按分数挑」把地/手牌烧掉）。
                if (InScriptTurn(core, me, out int scriptSac) && scriptSac == 5)
                {
                    return list.Where(e => e is Card c && c.IsAlive && NameOf(c) == NameSpirit)
                               .Take(1)
                               .ToList();
                }
                // 脚本 T3（2026-11-15 用户推导）：牺牲 **2 块已横置的地**——
                // 绝不烧 沃土（成长引擎）/归根（回收口）/小精灵（T5 材料）。
                if (InScriptTurn(core, me, out int t3sac) && t3sac == 3)
                {
                    if (Environment.GetEnvironmentVariable("TIDE_DEBUG_SAC") == "1")
                        Console.WriteLine($"[dbg-sac] T3 candidates={list.Count} cards={list.Count(e => e is Card)} tapped={list.Count(e => e is Card c0 && c0.IsTapped())} names={string.Join(",", list.OfType<Card>().Select(c1 => (NameOf(c1) ?? c1.ID) + (c1.IsTapped() ? "*" : "")))}");
                    // 2026-11-15 用户口径（T3 两种分支）：
                    // 目标手牌 = 小精灵 + 归根 + 减半。若 T1 的小精灵还活着 → 祭 3（2 小精灵 + 1 横置地）；
                    // 若它已经死了 → 墓地已够，祭到「够付回收」即可（1~2 张）。
                    var tappedSet = TappedPoolCards(core, me);
                    var spirits = list.Where(e => e is Card cs && cs.IsAlive && NameOf(cs) == NameSpirit).ToList();
                    // 祭的材料只取「可再生的」：小精灵最多 2 张 + 1 块多余的横置地（归根/沃土铺成的地）。
                    // **暗影豹与减半必须留在手里**（用户 T3 目标手牌 = 小精灵 + 归根 + 减半）。
                    var tapped = list.Where(e => poolSet.Contains(e) && e is Card c && tappedSet.Contains(c)
                            && NameOf(c) != NameSpirit && NameOf(c) != NameBloom
                            && NameOf(c) != NamePanther && NameOf(c) != NameHalve)
                        .OrderByDescending(e => e is Card c2 ? SacrificeScore(core, me, c2) : 0f)
                        .ToList();
                    var picks3 = new List<Entity>();
                    foreach (var sp in spirits.Take(2)) picks3.Add(sp);
                    foreach (var tl in tapped)
                    {
                        if (picks3.Count >= 3) break;
                        picks3.Add(tl);
                    }
                    // 候选里没有「已横置的地」时也要补一张**地**凑第 3 张（2026-11-15 规格收窄：
                    // 只在己方元素池组内补——战场生物永远不当地祭）：
                    // 祭掉它虽然不增费（未横置=绿换彩），但能腾出地牌槽 → 下一步补地 +1 费，回收才够。
                    if (picks3.Count < 3)
                    {
                        var anyPool = list.Where(e => poolSet.Contains(e) && e is Card cp && NameOf(cp) != NameSpirit
                                && NameOf(cp) != NameBloom && NameOf(cp) != NameHalve
                                && NameOf(cp) != NamePanther)
                            .OrderByDescending(e => e is Card cp2 ? SacrificeScore(core, me, cp2) : 0f)
                            .ToList();
                        foreach (var ap in anyPool)
                        {
                            if (picks3.Count >= 3) break;
                            if (!picks3.Contains(ap)) picks3.Add(ap);
                        }
                    }
                    if (picks3.Count > 0) return picks3;
                }
                var stuck = CoreStuckInPool(core, me);
                if (stuck.Count > 0)
                {
                    // 2026-11-15 规格收窄：救援材料同样限定在己方元素池组内
                    var rescue = list.Where(e => poolSet.Contains(e) && e is Card c && stuck.Contains(c)).ToList();
                    if (rescue.Count > 0) return rescue;
                }
                var ordered = list.OrderByDescending(e => e is Card c ? SacrificeScore(core, me, c) : 0f).ToList();
                // 张数走「操作表」（2026-11-15 用户口径）：按缺口 need 决定牺牲几张、并补几块地；
                // 只有为暗影豹/荒原猎兽的红费攒彩时才取彩缺口。
                int need;
                if (PantherTempNeed(core, me) > 0)
                {
                    need = PantherTempNeed(core, me);
                }
                else
                {
                    int deficit = ShortfallNeed(core, me);
                    need = ManaOps(deficit, LandInHand(core, me) > 0, HandCopies(core, me, NameReturn) > 0).sac;
                }
                need = Math.Max(1, Math.Min(need, ordered.Count));
                return ordered.Take(need).ToList();
            }

            // 弃牌（回合末手牌上限）：按「保留价值」升序——先丢最没用的，核心组件/归根最后丢。
            // （2026-11-14：此前默认排序会先把抹除丢掉，中期 AOE 直接断手）
            if (atom == AtomicEffectType.DiscardCard)
                return list.OrderBy(e => e is Card c ? DiscardKeepScore(NameOf(c)) : 0f).ToList();

            // 回收（归根）：核心组件 > 小精灵 > 成长 > 其他
            if (atom == AtomicEffectType.RecoverToHand)
                return list.OrderByDescending(e => e is Card c ? RecoverScore(core, me, c) : 0f)
                           .ThenByDescending(e => e is Card c2 ? core.LayerEngine.CalculatePower(c2) : 0)
                           .ToList();

            // 抹除（枯竭·全取）：对方元素池地牌全抹，目标顺序无所谓（只影响日志）
            if (atom == AtomicEffectType.Erase)
                return list.OrderByDescending(e => e is Card c
                        ? (ReferenceEquals(c.GetController(), me.Opponent)
                            ? 1000f + core.LayerEngine.CalculatePower(c)
                            : -1000f)
                        : 0f)
                    .ToList();

            // 护甲：己方加成（小精灵登场）→ 角色优先（角色死了就输）；
            // **代价侧**（沃土「敌方 +6 甲」）反过来——别贴敌方角色（角色甲吃的是我们的斩杀线），
            // 优先贴最弱的敌方单位（小精灵这类杂兵）。
            if (atom == AtomicEffectType.AddArmor)
            {
                if (req.IsCostTarget)
                    return list.OrderBy(e => e is Card c ? core.LayerEngine.CalculatePower(c) : 100000f).ToList();
                return list.OrderByDescending(e => e is Player).ToList();
            }

            // 无实体 / 减半：保命配件——**只能贴自己**（角色优先，其次己方单位）。
            // 注意原子域写的是「己方战场:生物; 双方角色」，候选里会带对手角色，必须先筛己方，
            // 否则 AI 会把减半贴到对手脸上（2026-11-15 用户战报抓到的错误目标）。
            if (atom == AtomicEffectType.GrantSpellban || atom == AtomicEffectType.ProtectionHalve)
            {
                var own = list.Where(e => e is Player pl
                        ? ReferenceEquals(pl, me)
                        : e is Card c && ReferenceEquals(c.GetController(), me))
                    .ToList();
                if (own.Count == 0) return list;              // 兜底：实在没有己方候选才退回原表
                return own.OrderByDescending(e => e is Player).ToList();
            }

            // 系命 / 狩猎（荒原猎兽登场赋予）：**贴己方最能打的生物**（通常就是它自己）——
            // 之前落到默认的「对方威胁优先」，把系命贴给了对手，攻击自然一点血都吸不到（2026-11-15）。
            if (atom == AtomicEffectType.GrantLifelink || atom == AtomicEffectType.GrantHunt)
            {
                var ownUnits = list.Where(e => e is Card c && c.IsAlive
                        && ReferenceEquals(c.GetController(), me))
                    .ToList();
                if (ownUnits.Count == 0) return list;
                return ownUnits
                    .OrderByDescending(e => e is Card c2 ? core.LayerEngine.CalculatePower(c2) : 0f)
                    .ToList();
            }

            // 减费落点（2026-11-15 用户口径）：**能「回收 + 成长」一起开 → 先给回收**（归根 4→2，
            // 两者合计 2+6=8）；只能开一张 → 给成长；**允许两层 −2 叠在同一张沃土上**
            // （用户 T3 线：T1 和 T3 的小精灵都给沃土 → 6−4 = 2，先出沃土再牺牲 2 横置地、保留小精灵）。
            if (atom == AtomicEffectType.AddCostDownGreen)
            {
                bool both = CanCastReturnAndBloom(core, me);
                return list
                    .OrderByDescending(e => e is Card c2 ? CostDownTargetScore(NameOf(c2), core, me, both) : 0f)
                    // 用户推导（2026-11-15 T3）：**两层 −2 要落在同一张沃土上**（6→2）——
                    // 所以同名候选里优先挑「已经带了一层折扣」的那张。
                    .ThenByDescending(e => e is Card c3 ? c3.GetCounterCount(CounterRules.CostDownGreenCounter) : 0)
                    .ThenByDescending(e => e is Card c4 ? GameActions.GetCardCost(c4, 0).Total : 0f)
                    .ToList();
            }

            // 默认：对方威胁优先
            return list.OrderByDescending(e => e is Card c && c.IsAlive && ReferenceEquals(c.GetController(), me.Opponent)
                    ? core.LayerEngine.CalculatePower(c)
                    : 0f)
                .ToList();
        }

        /// <summary>牺牲落点估值（2026-11-15 轮转口径）：
        /// 池中核心/**减半** 3000（救人：减半被成长拉进池 → 祭掉进墓地，再由归根捞回手）＞
        /// 已横置地 1500（色已花掉：再祭=1 彩 + 腾 1 槽，下一步补地）＞ 场上小精灵 700 ＞
        /// 未横置地/池中小精灵 300 ＞ 场上核心 −3000（绝不能祭）。</summary>
        private static float SacrificeScore(GameCore core, Player me, Card c)
        {
            string name = NameOf(c);
            bool inPool = c.GetZone() == Zone.ElementPool;
            if (name == NameHalve) return inPool ? 3000f : -3000f;
            if (name != null && CoreNames.Contains(name)) return inPool ? 3000f : -3000f;
            if (inPool)
            {
                bool tappedLand = !IsLandUntapped(core, me, c);
                if (tappedLand) return 1500f;                        // 已横置：祭它最赚（双份产出 + 腾槽）
                return name == NameSpirit ? 300f : 300f;
            }
            if (name == NameSpirit) return 700f;                     // 场上小精灵当废料（次于横置地）
            return 0f;
        }

        /// <summary>地牌是否仍未横置（池里按 SourceCard 对应）。</summary>
        private static bool IsLandUntapped(GameCore core, Player me, Card card)
        {
            foreach (var pc in core.ElementPool.GetPooledCards(me))
                if (pc != null && ReferenceEquals(pc.SourceCard, card)) return !pc.IsTapped;
            return true;
        }

        /// <summary>当前元素池地牌数（=可用地数上限的分子）。</summary>
        private static int PoolCount(GameCore core, Player me)
            => core.ElementPool.GetPooledCards(me).Count();

        /// <summary>地牌槽是否已满（满了就铺不进新地——牺牲一块即可腾位，下一步补地）。</summary>
        private static bool PoolFull(GameCore core, Player me)
            => PoolCount(core, me) >= core.ElementPool.GetLandCap(me);
        /// <summary>回收落点（2026-11-15 用户口径）：
        /// **减半 / 暗影豹 最优先**（保命件 + 终端）；小精灵/归根 其次；
        /// 沃土 在 **9 地之后不再回收**（成长上限已够，抽到才打），避免把回收额度浪费在它身上。</summary>
        private static float RecoverScore(GameCore core, Player me, Card c)
        {
            string name = NameOf(c);
            int lands = TotalLands(core, me);
            // 减半循环的「回收三格」= 小精灵 + 归根 + **减半或豹**（2026-11-15 用户口径）：
            //  · 豹还打不出去（地+彩 <12）→ 第三格给减半（保住对手回合的减伤）；
            //  · 豹打得出去 → 只把第三格换成豹（回收它 → 下一拍直接 9/9 系命再打一轮）。
            //  排序刻意让 小精灵/归根 在最上（循环的燃料），第三格在 减半/豹 之间随 ready 切换。
            if (name == NameSpirit) return 1200f;
            if (name == NameReturn) return 1190f;
            if (name == NamePanther) return PantherReady(core, me) ? 1180f : 1050f;
            if (name == NameHalve) return 1100f;
            if (name == NameBloom) return lands >= GateLands ? 80f : 800f;   // 9 地后不回收
            if (name != null && CoreNames.Contains(name)) return 1000f;
            return 100f;
        }

        /// <summary>减费落点（2026-11-15 用户口径）：能「回收 + 成长」同回合开 → **先给回收**（归根 4→2，
        /// 两者合计 2+6=8）；只能开一张 → 给成长（沃土 6→4，先把引擎点起来）；其它手牌兜底。</summary>
        private static float CostDownTargetScore(string name, bool bothCastable)
        {
            if (name == null) return 0f;
            if (bothCastable)
            {
                if (name == NameReturn) return 100f;
                if (name == NameBloom) return 80f;
                return 10f;
            }
            if (name == NameBloom) return 100f;
            if (name == NameReturn) return 70f;
            return 10f;
        }

        /// <summary>这张手牌是否已经吃过小精灵的「费用降低（绿）」——同一张卡只减一次费。</summary>
        private static bool AlreadyDiscounted(Card c)
            => c != null && c.GetCounterCount(CounterRules.CostDownGreenCounter) > 0;

        /// <summary>减费落点（带暗影豹版）：7 地之后手里有暗影豹 → 折扣优先给暗影豹
        /// （14→12 正好是「9 地 + 牺牲3 + 补1地」能凑到的数）；否则走成长/回收的旧口径。</summary>
        private static float CostDownTargetScore(string name, GameCore core, Player me, bool bothCastable)
        {
            if (name == null) return 0f;
            // 脚本回合（2026-11-15 T5 循环）：折扣要落在「本回合真要打的那张」——
            // T3 给沃土（成长引擎），T5/T7 给归根（回收 4→2 是循环的启动费）。
            // 旧口径在 T5 会把折扣给沃土（6→4），归根仍是 4 费，回收永远差费打不出来。
            if (InScriptTurn(core, me, out int scriptTurn) && scriptTurn >= 5)
            {
                if (name == NameReturn) return 130f;
                if (name == NameBloom) return 60f;
            }
            if (name == NamePanther && HandCopies(core, me, NamePanther) > 0
                && TotalLands(core, me) >= 7) return 120f;
            if (name == NameBeast && HandCopies(core, me, NameBeast) > 0
                && TotalLands(core, me) >= 6) return 110f;
            return CostDownTargetScore(name, bothCastable);
        }

        /// <summary>这一回合能不能把「回收 + 成长」一起开出来（归根 4 + 沃土 6 − 2 层减费 = 8 费）。
        /// 可达费 = 未横置地 + 临时 + 可祭横置地 + 手牌可铺地（牺牲腾槽后补地的量）。</summary>
        private static bool CanCastReturnAndBloom(GameCore core, Player me)
        {
            if (HandCopies(core, me, NameReturn) == 0 || HandCopies(core, me, NameBloom) == 0) return false;
            if (ReturnHeldEarly(core, me)) return false;          // 回收留手期不抢折扣（T3/T4 只做成长）
            int reach = AvailableMana(core, me) + FodderCount(core, me)
                        + Math.Min(LandInHand(core, me), 2);
            return reach >= 8;
        }

        /// <summary>暗影豹的红费缺口：它 14 费里含 **3 点红**，纯绿套的地产不出红，只能用牺牲彩元素垫付。
        /// 返回还差几彩（手里没有暗影豹 = 0）。</summary>
        private static int PantherTempNeed(GameCore core, Player me)
        {
            // 暗影豹 / 荒原猎兽 都带 3 点红费，纯绿套只能靠牺牲彩垫付
            if (HandCopies(core, me, NamePanther) == 0 && HandCopies(core, me, NameBeast) == 0) return 0;
            var pool = core.ElementPool.GetPool(me);
            int temp = pool?.TempElements != null ? pool.TempElements[ManaType.Any] : 0;
            return Math.Max(0, 3 - temp);
        }

        /// <summary>当前可用费：未横置地 + 临时元素（彩元素 1 个算 1 点）。</summary>
        private static int AvailableMana(GameCore core, Player me)
        {
            int n = core.ElementPool.GetPooledCards(me).Count(pc => pc != null && !pc.IsTapped);
            var pool = core.ElementPool.GetPool(me);
            if (pool != null)
                foreach (var kv in pool.TempElements) n += kv.Value;
            return n;
        }

        /// <summary>首卡连招是否成立（2026-11-15 用户口径，小精灵 1 绿）：
        /// 本回合还没出牌 ∧ 手里有**归根或沃土**（−2 层减费的落点）∧ 这一手真的开得出来。
        /// 小精灵 1 费只会横置 1 块地 → 这块正好变可祭废料（+1 彩），牺牲后腾 1 槽再补 1 块地（+1 费），
        /// 所以「可达费」= 当前可用费 + 最多补 1 块地。</summary>
        private static bool SpiritComboReady(GameCore core, Player me)
        {
            if (!IsFirstCardThisTurn(core, me)) return false;
            bool hasReturn = HandCopies(core, me, NameReturn) > 0;
            bool hasBloom = HandCopies(core, me, NameBloom) > 0;
            if (!hasReturn && !hasBloom) return false;
            int target = hasReturn ? 2 : BloomCost - 2;          // 归根 4−2 / 沃土 6−2
            int reach = AvailableMana(core, me) + Math.Min(LandInHand(core, me), 1);
            return reach >= target;
        }

        /// <summary>成长(沃土)的总费——2026-11-15 用户定案改为 **净 2.5**（锚 5.5 手钉 − 代价 3），
        /// 支付时逐色进位按 3 算。</summary>
        private const int BloomCost = 3;
        /// <summary>暗影豹总费（灰9+绿3，2026-11-15 吸血改系命后仍为 12）。</summary>
        private const int PantherCost = 12;
        /// <summary>虚化总费——无实体 绿9 − 代价 3 = 净 6。</summary>
        private const int VeilMana = 6;
        /// <summary>减半总费——减半指示物 绿5 − 代价 3 = 净 2。</summary>
        private const int HalveMana = 2;

        /// <summary>缺口：手里「最接近可打」的那张牌还差几点费（0=没有可补的）。留手牌不算。</summary>
        private static int ShortfallNeed(GameCore core, Player me)
        {
            int avail = AvailableMana(core, me); // 缺口按真实可用费算——小精灵+牺牲的 +3 在 WantHeroSkill 里兑现，不能提前算进余额
            int best = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (c == null || ShouldHoldStatic(core, me, c)) continue;
                if (GameActions.CanAffordCast(core, me, c, 0)) continue;
                int total = (int)Math.Ceiling(GameActions.GetCardCost(c, 0).Total - 1e-4f);
                if (total <= 0) continue;
                int d = total - avail;
                if (d > 0 && (best == 0 || d < best)) best = d;
            }
            return best;
        }

        /// <summary>小精灵+牺牲套路是否可用（手里或场上有 1/1 小精灵）。</summary>
        private static bool SpiritComboAvailable(GameCore core, Player me)
        {
            if (HandCopies(core, me, NameSpirit) > 0) return true;
            return (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Any(c => c != null && c.IsAlive && Is(c, NameSpirit));
        }
        /// <summary>可祭废料量（2026-11-14 连招口径）：**只算已经横置的地** + 场上 1/1 小精灵。
        /// 横置的地本回合的色已花掉，再祭成彩元素=一张地两份产出；未横置的地不祭（还要当费）。
        /// 核心/大生物永不算废料。</summary>
        private static int FodderCount(GameCore core, Player me)
        {
            // 地≥14 时视为「地有余」；另外**要垫红费时**（暗影豹/荒原猎兽的 3 红）也允许祭未横置的地——
            // 那不是亏费，是把绿换成彩（万能色），否则纯绿套永远付不出红。
            int lands = TotalLands(core, me);
            bool surplus = lands >= 14 || PantherTempNeed(core, me) > 0;
            int n = core.ElementPool.GetPooledCards(me).Count(pc => pc != null && (pc.IsTapped || surplus));
            n += (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Count(c => c != null && c.IsAlive && Is(c, NameSpirit));
            return n;
        }

        /// <summary>弃牌时的保留分（越低越先丢）：核心组件最高，其次归根/成长，小精灵最不值钱。</summary>
        private static float DiscardKeepScore(string name)
        {
            if (name == null) return 0f;
            if (CoreNames.Contains(name)) return 1000f;
            if (name == NameReturn) return 900f;
            if (RampNames.Contains(name)) return 600f;
            if (name == NameSpirit) return 100f;
            return 50f;
        }
        /// <summary>墓地里有值得用归根捞回来的牌（核心/归根/成长/小精灵）。</summary>
        private static bool HasUsefulGrave(GameCore core, Player me)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>())
            {
                if (c == null) continue;
                string n = NameOf(c);
                if (n == null) continue;
                if (CoreNames.Contains(n) || n == NameReturn || RampNames.Contains(n) || n == NameSpirit) return true;
            }
            return false;
        }
        /// <summary>ShouldHold 的静态可用版（ShortfallNeed 里不能用实例方法递归调用）。
        /// 2026-11-15 用户给的阶段标准：**T3/T4 只做成长，T5/T6 减半+成长，T7/T8 出暗影豹**——
        /// 所以 9 地之前手里还有成长/减半时，回收先留手（别把 T4 的费花在「小精灵+回收」上）。</summary>
        private static bool ShouldHoldStatic(GameCore core, Player me, Card card)
        {
            string name = NameOf(card);
            if (name == null) return false;
            if (name == NameReturn)
            {
                if (ReturnHeldEarly(core, me)) return true;                     // 成长/减半还没落地 → 回收留手
                return GyCount(core, me) < 2 || !HasUsefulGrave(core, me);      // 墓地≥2 且有货才捞
            }
            // 减半（2026-11-15 修）：**自己回合永远留手**（指示物回合末清一层 → 打了等于白给），
            // 只在对手回合的响应窗打。这一条必须放在「手里有回收就不攒」之前——否则手里有归根时
            // 减半会被当成可打牌白扔在自己回合（实测 T5 自回合支付 绿×4 贴了个空）。
            if (name == NameHalve && core.TurnEngine != null
                && ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return true;
            // 2026-11-15 用户定案：真正要留的 key 只有「回收」。手里有回收 → 其它都不攒；
            // 没有回收时枯竭等目标（全取，有地就打）、暗影豹等地/钱到位（下位替代）。
            if (HandCopies(core, me, NameReturn) > 0) return false;
            if (name == NameHalve) return true;
            if (EraseNames.Contains(name)) return OpponentLands(core, me) == 0;
            // 小精灵（2026-11-15 用户定案）：**永远不攒**——每回合只要手里有，第一张就打它（首卡 −2 交出去）。
            if (name == NameSpirit) return false;
            // 沃土就差 1~4 点、手里能补出来 → **别的牌先别花这笔费**（先凑成长；减半/回收都往后放）。
            // 这是用户口径「手里有这两张卡就该 100% 达成」的关键：不中途把费花在减半/回收上。
            if ((name == NameHalve || name == NameReturn) && BloomShort(core, me)) return true;
            if (name == NamePanther)
                return HandCopies(core, me, name) <= 1 && TotalLands(core, me) < 11;
            return false;
        }

        /// <summary>9 地之前、手里还有成长/减半可打 → 回收留手（把折扣和费让给成长线）。
        /// 2026-11-15 用户口径：T3/T4 只做成长（回收留手），**T5 起要先用回收开场**（回收+牺牲+补地
        /// 把 8 费凑出来，再沃土+减半），所以门槛只卡到 5 地。</summary>
        private static bool ReturnHeldEarly(GameCore core, Player me)
            => TotalLands(core, me) < 5
               && (HandCopies(core, me, NameBloom) > 0 || HandCopies(core, me, NameHalve) > 0);

        /// <summary>「操作表」（2026-11-15 用户口径）：按当前费缺口返回（牺牲张数, 需要补地张数）。
        /// · 缺口 ≤1：小精灵首卡 −2 已经覆盖，不用祭；
        /// · 缺口 2：牺牲 1 张（优先小精灵/已横置地）；
        /// · 缺口 3~4 且有可用地：牺牲 2 + 落 1 地；
        /// · 缺口 3~4 且没有可用地（有回收）：牺牲 4 + 落 3 地（靠回收把手牌补回来）。
        /// 用户口径：费够时穿插回收是纯收益，只有「回收+牺牲」后仍召唤不出生物才降级。</summary>
        private static (int sac, int lands) ManaOps(int need, bool spareLand, bool hasReturn)
        {
            if (need <= 1) return (0, 0);
            if (need == 2) return (1, 0);
            if (need <= 4 && spareLand) return (2, 1);
            if (need <= 4 && hasReturn) return (4, 3);
            return (Math.Min(Math.Max(need, 1), 4), Math.Max(0, need - 2));
        }

        /// <summary>手里那张沃土这一回合「差 1~4 点就能打」——牺牲只服务它。</summary>
        private static bool BloomShort(GameCore core, Player me)
        {
            return ShortBy(core, me, NameBloom, 4);
        }

        /// <summary>「回收 + 牺牲 + 生物」整链能不能打（用户口径：费够时穿插回收是纯收益）。</summary>
        private static bool CanChainReturnBeast(GameCore core, Player me)
        {
            if (HandCopies(core, me, NameReturn) == 0) return false;
            if (!HasBeastInHand(core, me)) return false;
            int reach = ChainReach(core, me);
            return reach >= 9;      // 小精灵1 + 归根4 + 生物8，扣掉 −2 层 + 牺牲彩 ≈ 9
        }

        /// <summary>「小精灵 + 牺牲 + 生物」链能不能打（没有回收时的降级线）。</summary>
        private static bool CanChainBeast(GameCore core, Player me)
        {
            if (!HasBeastInHand(core, me)) return false;
            return ChainReach(core, me) >= 7;
        }

        /// <summary>可达费 = 未横置地 + 可祭张数 + 最多补 2 块地。</summary>
        private static int ChainReach(GameCore core, Player me)
            => AvailableMana(core, me) + FodderCount(core, me) + Math.Min(LandInHand(core, me), 2);

        private static bool HasBeastInHand(GameCore core, Player me)
            => HandCopies(core, me, NameBeast) > 0 || HandCopies(core, me, NamePanther) > 0;

        /// <summary>本局是否已经召唤过生物（荒原猎兽/暗影豹在场）——首次召唤后回到成长线。</summary>
        private static bool HasSummonedBeast(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Any(c => c != null && c.IsAlive
                          && (NameOf(c) == NameBeast || NameOf(c) == NamePanther));

        /// <summary>手里的可召唤生物（荒原猎兽/暗影豹）这一回合差 1~3 点就能打。</summary>
        private static bool BeastShort(GameCore core, Player me)
            => ShortBy(core, me, NameBeast, 3) || ShortBy(core, me, NamePanther, 3);

        /// <summary>指定名字的手牌当前是否「差 1..maxNeed 点就能打」。</summary>
        private static bool ShortBy(GameCore core, Player me, string want, int maxNeed)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (c == null || NameOf(c) != want) continue;
                if (GameActions.CanAffordCast(core, me, c, 0)) continue;
                int total = (int)Math.Ceiling(GameActions.GetCardCost(c, 0).Total - 1e-4f);
                int need = total - AvailableMana(core, me);
                if (need > 0 && need <= maxNeed) return true;
            }
            return false;
        }

        // ======================================== 局面读数 ========================================

        /// <summary>对手元素池地牌数（枯竭·全取的目标面）。</summary>
        private static int OpponentLands(GameCore core, Player me)
            => core.ElementPool.GetPooledCards(me.Opponent).Count();

        /// <summary>地牌池里躺着的关键件（被成长随机拉进去的核心组件 / 减半）——祭掉救回手。</summary>
        private static List<Card> CoreStuckInPool(GameCore core, Player me)
        {
            var result = new List<Card>();
            foreach (var pc in core.ElementPool.GetPooledCards(me))
            {
                if (pc?.SourceCard == null) continue;
                string name = NameOf(pc.SourceCard);
                if (name != null && (CoreNames.Contains(name) || name == NameHalve)) result.Add(pc.SourceCard);
            }
            return result;
        }

        private static int CoreInGraveyard(GameCore core, Player me)
        {
            int n = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>())
                if (c != null && CoreNames.Contains(NameOf(c) ?? "")) n++;
            return n;
        }

        private static bool HasUnaffordableHandCard(GameCore core, Player me)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (c == null) continue;
                int total = (int)Math.Ceiling(GameActions.GetCardCost(c, 0).Total - 1e-4f);
                if (total > 0 && !GameActions.CanAffordCast(core, me, c, 0)) return true;
            }
            return false;
        }

        private static bool IsFirstCardThisTurn(GameCore core, Player me)
        {
            var stats = MatchStatsService.Instance;
            if (stats == null) return false;
            return stats.GetStat(me, MatchStatsService.CardsPlayed, StatScope.ThisTurn) == 0;
        }

        private static int GyCount(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>()).Count;

        // ======================================== 小工具 ========================================

        private static string NameOf(Card card) => (card as CardWrapper)?.GetData()?.CardName;

        private static bool Is(Card card, string name) => card != null && NameOf(card) == name;

        private static int HandCopies(GameCore core, Player me, string name)
        {
            int n = 0;
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                if (Is(c, name)) n++;
            return n;
        }

        private static int CountLands(GameCore core, Player me, ManaType color)
            => core.ElementPool.GetPooledCards(me).Count(pc => pc != null && pc.CanProduce(color));

        private static int TotalLands(GameCore core, Player me)
            => core.ElementPool.GetPooledCards(me).Count();
    }
}

