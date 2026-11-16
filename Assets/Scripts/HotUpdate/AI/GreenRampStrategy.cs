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
    ///   · T3 线：铺 3 地 → 小精灵（首卡，给手牌沃土 −2 绿 = 6→4）→ 祭 1 张地或场上小精灵
    ///     （+1 彩、腾出 1 槽）→ 手牌补一块地（+1 绿）→ 3 绿 + 1 彩 = 4 费把沃土开出来；
    ///   · T5 线（固定序，2026-11-15 用户口径）：小精灵（首卡，给**归根** −2 → 4→2 绿）→
    ///     牺牲**场上小精灵 + 一块灰地/非绿地**（+2 彩、腾 1 地槽）→ 归根（捞回 **沃土 + 小精灵×2**）→
    ///     两只小精灵补地（全绿 7 块）→ **沃土**（绿4 + 任意2）→ T5 结束手牌压到 T7 key（暗影豹 + 虚化）。
    ///     T5 不留 3 费给虚化；**T7 才是留 3 费、对手回合开虚化的回合**。
    ///     虚化**只在对手回合的响应窗贴自己**（自己回合贴 = 回合末清层白给）；
    ///   · 关键：**牺牲腾出的槽位要用手牌立刻补回来**（否则地数掉档、牌白烧），墓地那批再用归根捞回手。
    /// 中期：虚化（实付 3 绿，无实体钳伤害到 1）只在对**对手回合**的响应窗贴自己；枯竭**全取**（对手元素池有几块抹几块）。
    /// 后期：暗影豹收尾；地铺到 20 后靠成长与归根维持每回合「回收 + 大生物」。
    /// 卡组（60）：小精灵×20 / 归根×10 / 沃土×15 / 战斗母巢×10 / 系命母巢×5（虚化/暗影豹已退组）；技能=牺牲。
    /// 胜点（2026-11-16）：母巢召唤 1/1 攻击守卫衍生物；战斗衍生物带团结4（回合末全体攻+2/血+2），
    /// 系命衍生物带团结3（回合末赋予系命）。衍生物带遗忘，回合末结算后离场；buff 留在存活单位上。
    /// </summary>
    public sealed class GreenRampStrategy : AiStrategy
    {
        private const string NameSpirit = "小精灵";   // 1/1 无攻守；登场己方角色 +2 甲；首卡 → 手牌 −2 绿
        private const string NameSprout = "萌芽";     // 成长 1 + 首卡减费
        private const string NameBloom = "沃土";      // 成长 3；代价：一个敌方单位获得6层护甲（净 6 绿）
        private const string NameReturn = "归根";     // 回收 3 张
        private const string NameRecycle = "回收";    // 全墓地回手（2026-11-16 绿线循环）
        private const string NameSeal = "封印";       // 封印一个对方单位（绿线保命）
        private const string NameWitheredGuard = "枯木守卫"; // 2/8 守护+帷幕，优先级低于暗影豹
        private const string NameEraseLand = "枯竭";  // 抹除对方元素池地牌（2026-11-15 起=全取）
        private const string NamePanther = "系命者 阿格洛斯"; // 12/12 攻守 系命
        // 虚化 = 2026-11-15 用户口径里替换掉旧的 4 费「减半」的 3 费保命件（无实体，钳伤害到 1）。
        // 只能在对**对手回合**的响应窗贴自己；自己回合打 = 回合末掉层白给。全类只保留这一个名字。
        private const string NameVeil = "虚化";
        private const string NameBeast = "荒原猎兽";  // 5/5 攻击 系命+狩猎（灰4红3绿3 = 10 费）
        private const string NameBattleNest = "战斗母巢";   // 召唤 5 个 1/1 攻击守卫（团结4：回合末全体攻/血+2）
        private const string NameLifelinkNest = "系命母巢"; // 召唤 3 个 1/1 攻击守卫（团结3：回合末赋予系命）
        private const string NameWorkerNest = "工蜂母巢";   // 召唤 2 个 3/1 攻击（卡 1 + 召唤 3 = 4 费，T3 挤费线）

        // 2026-11-16 固定序状态：T3 只下 1 只小精灵 + 1 张沃土；T5 只打 1 张沃土再牺牲。
        private int _loopT3Turn = -1;
        private bool _loopT3Spirit;
        private int _loopT5Turn = -1;
        private bool _loopT5Bloom;
        private bool _loopT5Sacrificed;
        private static bool LoopT5PostSacrifice; // T5 牺牲后：允许把沃土/小精灵也铺成地，只留 1 张归根
        private bool _loopSacrificedThisTurn;
        private int _loopSacrificeTurn = -1;
        private bool _t7RefreshAgros;
        private static bool T7PostSacrifice; // T7+ 牺牲后：允许铺地/留一张守卫
        private bool _t5AgrosCast;
        private bool _t1SpiritCast;
        private bool _t3SpiritCast;
        private bool _t3BloomCast;
        private bool _returnUsedThisTurn;
        private static int _statGameSeq;
        // 固定计划执行步（ForcePlayCard）：T1/T3/T5 按序插队，计划外交给原评分。
        private int _fixedTurn = -1;
        private int _fixedStep;

        /// <summary>核心组件（全取抹除 + 虚化 + 暗影豹 + 两张母巢）——不当土地烧掉。</summary>
        private static readonly HashSet<string> CoreNames = new HashSet<string>
        {
            // 荒原猎兽（2026-11-15 修 bug）：此前不在核心件里 → 会被 PlayLands 当土地铺掉，
            // 手牌里永远留不住、T5 之后一次都打不出来。
            NameEraseLand, NameVeil, NamePanther, NameBeast, NameBattleNest, NameLifelinkNest, NameWorkerNest,
            NameReturn, NameRecycle, NameSeal,
        };

        private static readonly HashSet<string> EraseNames = new HashSet<string> { NameEraseLand };

        /// <summary>成长引擎：前期能打就打。</summary>
        private static readonly HashSet<string> RampNames = new HashSet<string> { NameSprout, NameBloom };

        /// <summary>地牌门槛（门=操控地数量≥9；9 地后减费/护甲门开启，也是**虚化 9 费保命线**）。</summary>
        private const int GateLands = 9;
        /// <summary>暗影豹总费 12（灰9+绿3）——2026-11-15 晚还原卡面后门槛回到 12（小精灵折扣另算）。</summary>
        private const int PantherMinTotal = 12;
        /// <summary>成长停手线（2026-11-14 用户定案）：一路成长到 **20 地**才停——20 地后每回合能稳定
        /// 打出一组「归根 + 暗影豹」。</summary>
        private const int LandTarget = 20;
        /// <summary>早期补地窗口（2026-11-14 定案）：6 费之前除了成长，也允许拿手牌补地。</summary>
        private const int EarlyLandDump = 6;
        /// <summary>手牌铺地上限（2026-11-15 用户口径「轮转」）：牺牲会腾出地牌槽，腾出后要立刻用**手牌**
        /// 补一块回来（一张地出两份费）；这套牌一路铺到 12 地才停手牌铺地，之后全靠成长。
        /// 12 之后手牌只用来打成长/回收，不再当土地烧。</summary>
        private const int LandMidCap = 16; // 阿格洛斯 14 费 + 2 费牺牲
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
            EnsureTurnState(core, me);
            if (!base.WantHeroSkill(core, me)) return false;
            // T1/T3 固定步骤期间不牺牲（第一次跳费只靠小精灵/沃土）。
            if (IsLoopDeck(core, me) && OwnTurn(core, me) < 5) return false;
            // T3 循环卡组只走“小精灵→沃土”，不牺牲。
            if (IsLoopDeck(core, me) && OwnTurn(core, me) == 3) return false;
            // T5 循环：必须先打完沃土（_loopT5Bloom），再允许牺牲；否则池里的核心件会让技能抢在沃土前发动。
            if (IsLoopT5(core, me) && !_loopT5Bloom) return false;
            // T5 全取循环：沃土打完后，先牺牲（9 次），再归根。
            if (IsLoopT5(core, me) && _loopT5Bloom && !_loopT5Sacrificed
                && HandCopies(core, me, NameReturn) > 0)
            {
                _loopT5Sacrificed = true; LoopT5PostSacrifice = true; // Want 分支也会走，必须在这里置位，否则自由阶段仍按未牺牲处理
                return true;
            }
            // 2026-11-16：T7 母巢召唤差 1~2 点 → 牺牲横置地补费（用户固定序：落 1 地→10 费，
            // 母巢本体 1 + 召唤 10，需再挤 1 点）。
            // 固定脚本：T5 的「牺牲场上小精灵」是循环的第一步，必须无条件发动（不看费缺口）；
            // 真正的 T5 还会加祭一块灰地/非绿地（换彩 + 腾槽，沃土才付得出）。
            if (InScriptTurn(core, me, out int scriptTurn) && scriptTurn == 5
                && HasSpiritOnField(core, me)
                && (!IsLoopDeck(core, me) || _loopT5Bloom)) return true;
            // T7+ 新循环：场上 1 只时先用沃土找第二只，不刷新；2 只以后才刷新最低属性的旧阿格洛斯。
            if (OwnTurn(core, me) >= 7
                && !_loopSacrificedThisTurn && AgrosOnField(core, me) > 0
                && HandCopies(core, me, NameReturn) > 0
                && ((AgrosOnField(core, me) >= 2 && HandCopies(core, me, NamePanther) == 0)
                    || (HandCopies(core, me, NamePanther) > 0
                        && !HasAffordableAgrosInHand(core, me)
                        && HandCopies(core, me, NameReturn) > 0)))
            {
                _t7RefreshAgros = AgrosOnField(core, me) >= 2
                    && HandCopies(core, me, NamePanther) == 0;
                _loopSacrificeTurn = OwnTurn(core, me); _loopSacrificedThisTurn = true; T7PostSacrifice = true;
                return true;
            }
            // 脚本 T3（2026-11-15 用户口径：T3 要跳费）：小精灵先出、沃土还差 1~4 点时，
            // 允许「牺牲 1 块地 → 腾槽 → 手牌补地」的轮转（净 +1 费）。
            // 旧闸要求「已横置地 ≥2」——但小精灵只横置 1 块、沃土又还没打出来，条件永远不成立，
            // T3 跳费被这条死锁卡住（实测 T3 只铺地、沃土 4 费打不出来）。这里改由缺口/补地判定。
            if (InScriptTurn(core, me, out int t3) && t3 == 3)
                return T3RampSacrificeReady(core, me);
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
            // 阿格洛斯走通用解场：能解的生物优先，不再用旧的“系命优先打脸”覆盖。
            if (NameOf(unit) == NamePanther) return base.PickAttackTarget(core, me, unit, opp);
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
            // 2026-11-16：母巢主动召唤——有剩余费就出母巢，但**召唤数量会把己方生物挤过 9 格时不发动**
            //（母巢本体已占场；溢出判定=当前己方生物数 + 召唤数 > 9）。
            string nestName = source != null ? NameOf(source) : null;
            if (nestName == NameBattleNest) return HasSummonRoom(core, me, 5);
            if (nestName == NameLifelinkNest) return HasSummonRoom(core, me, 3);
            if (nestName == NameWorkerNest) return HasSummonRoom(core, me, 2);

            bool isSacrifice = (effectName != null && (effectName.Contains("牺牲") || effectName.Contains("送墓")))
                               || (source != null && NameOf(source) == "牺牲");
            // 牺牲和归根绑定：手里没有归根，就不使用牺牲。
            if (isSacrifice && HandCopies(core, me, NameReturn) == 0) return false;
            // T7+ 自愿牺牲：只要手里有归根就允许，并在技能入口置状态（不依赖目标选择回调）。
            if (isSacrifice && OwnTurn(core, me) >= 7)
            {
                _loopSacrificeTurn = OwnTurn(core, me); _loopSacrificedThisTurn = true; T7PostSacrifice = true;
                return true;
            }
            // T1/T3 固定步骤期间不牺牲（第一次跳费只靠小精灵/沃土）。
            if (isSacrifice && OwnTurn(core, me) < 5) return false;
            // T7 循环必须手里有第二张归根；只有 1 张时不牺牲，按普通评分。
            if (isSacrifice && OwnTurn(core, me) >= 7
                && HandCopies(core, me, NameReturn) < 2) return false;
            // T3 循环卡组不牺牲。
            if (isSacrifice && IsLoopDeck(core, me) && core.TurnEngine != null
                && OwnTurn(core, me) == 3) return false;
            // T5 全取循环：沃土打完后，允许牺牲全部 8 地 + 小精灵。
            if (isSacrifice && IsLoopT5(core, me)
                && _loopT5Bloom && !_loopT5Sacrificed) return true;
            // 2026-11-16：T7 母巢召唤差 1~2 点 → 允许牺牲横置地补最后几点（随后主动召唤）。
            if (!isSacrifice) return true;
            // 豹回收线（2026-11-15 用户口径）：T7/T8 起手里没豹、池里却有豹 → 允许祭豹送墓
            // （不受「场上须有小精灵」的 T5 闸限制，也不要求先手脚本回合）。
            if (PantherSalvageActive(core, me) && HasPantherInPool(core, me))
            {
                if (Environment.GetEnvironmentVariable("TIDE_DEBUG_GREEN") == "1")
                    Console.WriteLine($"[dbg-green] sacrifice-allow 豹 turn={core.TurnEngine?.TurnNumber} pool={HasPantherInPool(core, me)}");
                return true;
            }
            if (!InScriptTurn(core, me, out int scriptTurn)) return false;
            // 脚本 T3：与 WantHeroSkill 同一口径（跳费轮转闸）——不再要求 2 块已横置地。
            if (scriptTurn == 3) return T3RampSacrificeReady(core, me);
            // 脚本 T5（2026-11-15 循环）：材料=**场上小精灵**；真正 T5 再加一块非绿地。
            // 没有小精灵就整条闸关死——否则自愿队列会在「手里没小精灵」的回合照样发动，
            // 把地池里的地当材料烧掉（实测 T5 地 8→7）。
            if (scriptTurn == 5) return HasSpiritOnField(core, me)
                && (!IsLoopDeck(core, me) || _loopT5Bloom);
            // T7+ 新循环：阿格洛斯在场即可牺牲回收；本轮只允许一次。
            if (scriptTurn >= 7) return !_loopSacrificedThisTurn && AgrosOnField(core, me) > 0;
            return false;
        }

        /// <summary>救核心要先于出牌（否则核心地牌会被当成普通地继续产色/被忽略）。</summary>
        public override bool SkillBeforeCards(GameCore core, Player me)
        {
            // T5 固定牺牲必须先于任何出牌（包括归根）。
            if (ForceHeroSkill(core, me)) return true;
            // T5 沃土之前不允许技能抢跑，否则会先牺牲、沃土永远打不出来。
            if (IsLoopT5(core, me) && !_loopT5Bloom) return false;
            // T3 循环卡组不牺牲，先出小精灵/沃土。
            if (IsLoopDeck(core, me) && OwnTurn(core, me) == 3) return false;
            // T5 全取循环：沃土打完后，牺牲必须先于归根。
            if (IsLoopT5(core, me) && _loopT5Bloom && !_loopT5Sacrificed
                && HandCopies(core, me, NameReturn) > 0) return true;
            // 脚本 T7（2026-11-15 用户口径）：**先出牌、后牺牲**——必须先打小精灵+回收把 3 块地横置出来，
            // 牺牲才拿得到「已横置的地」；否则一上来就祭，只能祭到没横置的地（净 0）或小精灵（+1）。
            // 2026-11-15：脚本回合一律「先出牌、后牺牲」（T3 也一样——先打小精灵+沃土把地横置出来）。
            if (InScriptTurn(core, me, out _)) return false;
            return CoreStuckInPool(core, me).Count > 0;
        }

        /// <summary>响应窗（2026-11-15 用户口径）：**虚化必须在对手回合打出**。
        /// 指示物是「自己回合结束清一层」——自己回合上等于白给（撑不到对手回合）。
        /// 所以只在对面的回合、且有响应候选（手牌里的虚化）时出手；目标=己方角色。</summary>
        public override ResponseOption PickResponse(GameCore core, Player me, List<ResponseOption> options)
        {
            if (options == null || options.Count == 0) return null;
            if (core.TurnEngine == null || ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return null; // 只打对手回合
            var opp = me?.Opponent;
            if (opp == null) return null;

            // 2026-11-16 用户口径：仅守卫生物（攻=false、守=true）按属性配对盯防——
            // 己方最低属性 → 对手最高属性，依次一一对应；对应单位攻击时由该守卫拦截，
            // 哪怕这一下会死。非对应单位不拦，剩余攻击打角色。
            var attackSource = options.FirstOrDefault(o =>
                o != null && o.Kind == ResponseOption.ResponseKind.GuardAbility)?.AttackInstance?.Source as Card;
            if (attackSource != null)
            {
                var assigned = GuardAssignmentFor(core, me, attackSource);
                if (assigned != null)
                {
                    var guardOpt = options.FirstOrDefault(o =>
                        o != null && o.Kind == ResponseOption.ResponseKind.GuardAbility
                        && ReferenceEquals(o.SourceCard, assigned));
                    if (guardOpt != null && assigned.IsAlive && !assigned.IsTapped()) return guardOpt;
                }
            }

            ResponseOption pick = null;
            foreach (var o in options)
            {
                if (o == null || o.Kind != ResponseOption.ResponseKind.HandCard || o.SourceCard == null) continue;
                if (NameOf(o.SourceCard) != NameVeil) continue;
                pick = o;                       // 虚化在手 → 就在这个窗口上给自己
                break;
            }
            if (Environment.GetEnvironmentVariable("TIDE_DEBUG_GREEN") == "1")
                Console.WriteLine($"[dbg-green] resp turn={core.TurnEngine.TurnNumber} untapped={TotalLands(core, me) - TappedLands(core, me)} total={TotalLands(core, me)} opts={options.Count} halveOpt={pick != null} handHalve={HandCopies(core, me, NameVeil)}");
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
        // 2026-11-15 补（T5 循环落地）：脚本回合内 小精灵/归根/虚化 各留一张在手里（多册才铺地），
        // 归根一回合只打一张且墓地 ≥3 才打（否则白扔 4 费、把对手回合贴虚化的钱烧光）。

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

        private static int AgrosOnField(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Count(c => c != null && c.IsAlive && NameOf(c) == NamePanther);

        private static bool HasAffordableAgrosInHand(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                .Any(c => c != null && NameOf(c) == NamePanther && GameActions.CanAffordCast(core, me, c, 0));

        /// <summary>自方场上小精灵张数（T3 轮转可祭多只，按张数算可补的费）。</summary>
        private static int SpiritsOnField(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Count(c => c != null && c.IsAlive && NameOf(c) == NameSpirit);

        /// <summary>T3 跳费轮转闸（2026-11-15 用户口径）：沃土在手且只差 1~4 点，并且
        /// 「池里有地可祭 + 手里有地可补回」（牺牲一块地 +1 彩、腾槽后补一块地 +1 绿，净 +1 费）；
        /// 或者场上还有小精灵可祭（+1 彩）。这样 T3 不必等 2 块横置地也能把沃土开出来。</summary>
        private static bool AnyBloomAffordable(GameCore core, Player me)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                if (Is(c, NameBloom) && GameActions.CanAffordCast(core, me, c, 0)) return true;
            return false;
        }

        private static bool T3RampSacrificeReady(GameCore core, Player me)
        {
            if (HandCopies(core, me, NameBloom) == 0) return false;
            // 手里只要**有一张**沃土付得出，就先打它，别为了另一张 6 费沃土去牺牲（2026-11-15 回归：
            // 两层减费把一张沃土压到 2 费时，旧闸被另一张 6 费沃土误触发，T3 反而没打出沃土）。
            if (AnyBloomAffordable(core, me)) return false;
            if (!BloomShort(core, me)) return false;                 // 沃土已付得出 → 不需要祭
            if (PoolCount(core, me) <= 0) return false;
            int need = ShortfallNeed(core, me);
            if (need <= 0 || need > 3) return false;                 // T3 最多祭 3 张
            // 每祭 1 张最多换回 1 点：横置/未横置地腾槽后靠手牌补 1，场上小精灵直接 +1。
            // 场上可能不止一只小精灵（T1+T3 各一只），必须按**张数**算，不能只算 1。
            int canGain = LandInHand(core, me) + SpiritsOnField(core, me);
            return need <= canGain && HasT3SacrificeMaterial(core, me);
        }

        /// <summary>T3 是否真有可祭材料（场上小精灵，或池里一张不是 沃土/归根/虚化/暗影豹 的地）。
        /// 防「闸通过但目标筛选为空」把英雄技能空放掉。</summary>
        private static bool HasT3SacrificeMaterial(GameCore core, Player me)
        {
            if (HasSpiritOnField(core, me)) return true;
            foreach (var pc in core.ElementPool.GetPooledCards(me))
            {
                string n = pc?.SourceCard != null ? NameOf(pc.SourceCard) : null;
                if (n == null) continue;
                if (n == NameSpirit || n == NameBloom || n == NameReturn || n == NameVeil || n == NamePanther) continue;
                return true;
            }
            return false;
        }

        /// <summary>当前是自方第几个回合（全局回合 → 自方回合，先手 = 奇数全局回合）。</summary>
        private static int OwnTurnIndex(GameCore core)
            => core?.TurnEngine == null ? 0 : (core.TurnEngine.TurnNumber + 1) / 2;

        /// <summary>当前回合是否处于某个脚本线（且所需卡在手里）。</summary>
        private static bool InScriptTurn(GameCore core, Player me, out int scriptTurn)
        {
            scriptTurn = 0;
            if (core.TurnEngine == null || !ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return false;
            // 2026-11-16 修正：用户口径 T1/T3/T5/T7 是**绿方自己的回合序**。
            // 绿在后手时，它的 T1 是全局 T2、T3 是全局 T4、T5 是全局 T6、T7 是全局 T8。
            int own = ReferenceEquals(core.Player1, me)
                ? core.TurnEngine.TurnNumber
                : core.TurnEngine.TurnNumber - 1;
            if (own == 3) { scriptTurn = 3; return true; }
            if (own == 5) { scriptTurn = 5; return true; }
            if (own >= 7) { scriptTurn = 7; return true; }
            return false;
        }


        /// <summary>暗影豹这一拍打不打得出去（操控地 + 临时彩 ≥ 12）——「缓一回合」的判据：
        /// 不 ready 就让虚化循环继续长地（每回合 +1~2），**不为了 T7 硬凑 12 费去祭 4 张**（省手牌）。</summary>
        private static bool PantherReady(GameCore core, Player me)
        {
            var pool = core.ElementPool.GetPool(me);
            int temp = pool?.TempElements != null ? pool.TempElements[ManaType.Any] : 0;
            return TotalLands(core, me) + temp >= PantherCost;
        }

        /// <summary>豹回收线（2026-11-15 用户口径）：T7（先手）/T8（后手）起、**手里没有豹**时，
        /// 回收优先拿豹（压过虚化）；若豹被当土地铺进元素池，牺牲优先祭它送墓，下一拍回收出豹。
        /// 用 >= 而不是 ==：T7 祭进墓的豹，要留到下一个自己回合的归根再捞（当回合牌序是「先出牌后牺牲」）。</summary>
        private static bool PantherSalvageActive(GameCore core, Player me)
        {
            if (core?.TurnEngine == null || !ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return false;
            if (HandCopies(core, me, NamePanther) > 0) return false;
            int turn = core.TurnEngine.TurnNumber;
            return ReferenceEquals(core.Player1, me) ? turn >= 7 : turn >= 8;
        }

        /// <summary>元素池里是否有豹（被当土地铺掉）。</summary>
        private static bool HasPantherInPool(GameCore core, Player me)
        {
            foreach (var pc in core.ElementPool.GetPooledCards(me))
                if (pc != null && NameOf(pc.SourceCard) == NamePanther) return true;
            return false;
        }
        /// <summary>脚本内的卡片优先级（数字越大越先打）。</summary>
        private static float ScriptScore(GameCore core, Player me, int scriptTurn, string name, bool firstCard, bool spiritOnField)
        {
            switch (scriptTurn)
            {
                case 3:
                    if (name == NameSpirit && firstCard) return 9900f;
                    // 工蜂母巢 1+3=4：必须等小精灵/牺牲/补地挤出 4 费后再出；否则只拍 1 费本体、
                    // 3 费召唤开不出来，下一拍就被红解掉（2026-11-16 实测）。
                    if (name == NameWorkerNest) return AvailableMana(core, me) >= 5 ? 9850f : 300f;
                    if (name == NameBloom) return 9800f;
                    if (name == NameReturn) return 9700f;      // 用户推导：T3 收尾是「回收」
                    break;
                case 5:
                    // T5 循环：小精灵必须先下场给沃土 −2，再打沃土；即使铺地后已不是首卡。
                    if (name == NameSpirit && !spiritOnField && HandCopies(core, me, NameBloom) > 0) return 9900f;
                    if (name == NameSpirit && firstCard) return 9900f;
                    // T5 循环的「回收」：墓地 ≥3 张才打（回收 3 才是纯收益）——不够的回合再打第二张
                    // 只是把 4 费扔掉（实测 T5 二次归根捞 1 张、把留给对手回合贴虚化的费烧光）。
                    if (name == NameReturn) return GyCount(core, me) >= 3 ? (spiritOnField ? 9850f : 9800f) : 150f;
                    // 2026-11-16：第一组三回合（T1/T3/T5）的第二次成长仍在 T5 优先于母巢；
                    // T7 起不再强制沃土，把费让给母巢+召唤。
                    if (name == NameBloom && core.TurnEngine != null
                        && OwnTurn(core, me) <= 5 && HandCopies(core, me, NameBloom) > 0) return 9800f;
                    break;
                case 7:
                    if (name == NameSpirit && firstCard) return 9900f;
                    if (name == NameReturn) return 9800f;
                    if (name == NamePanther) return 9700f;
                    break;
            }
            return 0f;
        }

        /// <summary>绿预组全局硬闸：没有归根就不允许牺牲（固定序/评分逻辑都必须遵守）。</summary>
        public override bool AllowHeroSkillActivation(GameCore core, Player me)
            => !IsLoopDeck(core, me) || HandCopies(core, me, NameReturn) > 0;

        public override void OnHeroSkillActivated(GameCore core, Player me)
        {
            if (IsLoopT5(core, me))
            {
                _loopT5Sacrificed = true; LoopT5PostSacrifice = true;
            }
            if (OwnTurn(core, me) >= 7)
            {
                _loopSacrificeTurn = OwnTurn(core, me);
                _loopSacrificedThisTurn = true;
                T7PostSacrifice = true;
            }
        }

        /// <summary>T5 固定牺牲：沃土之后、归根之前，必须直接发动英雄技能。</summary>
        public override bool ForceHeroSkill(GameCore core, Player me)
        {
            if (IsLoopT5(core, me))
            {
                if (!_loopT5Bloom || _loopT5Sacrificed
                    || HandCopies(core, me, NameReturn) == 0) return false;
                _loopT5Sacrificed = true; LoopT5PostSacrifice = true;
                return true;
            }
            if (OwnTurn(core, me) >= 7
                && !_loopSacrificedThisTurn && AgrosOnField(core, me) > 0
                && HandCopies(core, me, NameReturn) > 0
                && ((AgrosOnField(core, me) >= 2 && HandCopies(core, me, NamePanther) == 0)
                    || (HandCopies(core, me, NamePanther) > 0
                        && !HasAffordableAgrosInHand(core, me)
                        && HandCopies(core, me, NameReturn) > 0)))
            {
                _t7RefreshAgros = AgrosOnField(core, me) >= 2
                    && HandCopies(core, me, NamePanther) == 0;
                _loopSacrificeTurn = OwnTurn(core, me); _loopSacrificedThisTurn = true; T7PostSacrifice = true;
                return true;
            }
            return false;
        }

        /// <summary>回合状态必须在任何策略查询（ShouldHold/评分/技能）之前重置，不能等 ForcePlayCard。</summary>
        private void EnsureTurnState(GameCore core, Player me)
        {
            int turn = ReferenceEquals(core.Player1, me)
                ? core.TurnEngine.TurnNumber
                : core.TurnEngine.TurnNumber - 1;
            if (turn < 1 || _fixedTurn == turn) return;
            _fixedTurn = turn; _fixedStep = 0;
            _loopT5Bloom = false; _loopT5Sacrificed = false; LoopT5PostSacrifice = false;
            if (_loopSacrificeTurn != turn)
            {
                _loopSacrificedThisTurn = false; _t7RefreshAgros = false; T7PostSacrifice = false;
            }
            _returnUsedThisTurn = false;
            if (turn == 1)
            {
                _statGameSeq++;
                Console.WriteLine($"[green-key] game={_statGameSeq} turn=1 S={HandCopies(core, me, NameSpirit) > 0}");
                Console.WriteLine(KeyStateDump(core, me, _statGameSeq, 1));
            }
            if (turn == 3)
            {
                Console.WriteLine($"[green-key] game={_statGameSeq} turn=3 S={HandCopies(core, me, NameSpirit) > 0} B={HandCopies(core, me, NameBloom) > 0} R={HandCopies(core, me, NameReturn) > 0} t1S={_t1SpiritCast}");
                Console.WriteLine(KeyStateDump(core, me, _statGameSeq, 3));
            }
            if (turn == 5)
            {
                var oppBoard = (core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield) ?? new List<Card>())
                    .Where(c => c != null && c.IsAlive)
                    .Select(c => $"{NameOf(c)} {core.LayerEngine.CalculatePower(c)}/{core.LayerEngine.CalculateToughness(c)}"
                        + $" armor={c.GetEffectiveCounterCount(KeywordRules.ArmorCounter)}")
                    .ToList();
                Console.WriteLine($"[opp-state] game={_statGameSeq} turn={turn} [{string.Join(", ", oppBoard)}]");
                _t5AgrosCast = false;
                Console.WriteLine($"[green-key] game={_statGameSeq} turn=5 B={HandCopies(core, me, NameBloom) > 0} R={HandCopies(core, me, NameReturn) > 0} A={HandCopies(core, me, NamePanther) > 0} t3S={_t3SpiritCast} t3B={_t3BloomCast} t3BloomGrave={GyHasBloom(core, me)}");
                Console.WriteLine(KeyStateDump(core, me, _statGameSeq, 5));
            }
            if (turn == 6)
                Console.WriteLine($"[green-key] game={_statGameSeq} turn=6 t5Agros={_t5AgrosCast}");
            if (turn >= 7)
                Console.WriteLine(KeyStateDump(core, me, _statGameSeq, turn));
        }

        /// <summary>2026-11-16 固定计划：T1 小精灵；T3 小精灵→沃土；T5 沃土→（牺牲后）归根。
        /// 计划内的牌按顺序强制打出；计划外/盈余费回落 CardPlayScore。</summary>
        public override Card ForcePlayCard(GameCore core, Player me)
        {
            if (core?.TurnEngine == null || !IsLoopDeck(core, me)) return null;
            int turn = ReferenceEquals(core.Player1, me)
                ? core.TurnEngine.TurnNumber
                : core.TurnEngine.TurnNumber - 1;
            if (turn < 1) return null;
            if (_fixedTurn != turn)
            {
                _fixedTurn = turn; _fixedStep = 0;
                _loopT5Bloom = false; _loopT5Sacrificed = false; LoopT5PostSacrifice = false;
                if (_loopSacrificeTurn != turn)
                {
                    _loopSacrificedThisTurn = false; _t7RefreshAgros = false; T7PostSacrifice = false;
                }
                _returnUsedThisTurn = false;
                if (turn >= 7)
                    Console.WriteLine(KeyStateDump(core, me, _statGameSeq, turn));
                if (turn == 1)
                {
                    _statGameSeq++;
                    Console.WriteLine($"[green-key] game={_statGameSeq} turn=1 S={HandCopies(core, me, NameSpirit) > 0}");
                    Console.WriteLine(KeyStateDump(core, me, _statGameSeq, 1));
                }
                if (turn == 3)
                {
                    Console.WriteLine($"[green-key] game={_statGameSeq} turn=3 S={HandCopies(core, me, NameSpirit) > 0} B={HandCopies(core, me, NameBloom) > 0} R={HandCopies(core, me, NameReturn) > 0} t1S={_t1SpiritCast}");
                    Console.WriteLine(KeyStateDump(core, me, _statGameSeq, 3));
                }
                if (turn == 5)
                {
                    _t5AgrosCast = false;
                    Console.WriteLine($"[green-key] game={_statGameSeq} turn=5 B={HandCopies(core, me, NameBloom) > 0} R={HandCopies(core, me, NameReturn) > 0} A={HandCopies(core, me, NamePanther) > 0} t3S={_t3SpiritCast} t3B={_t3BloomCast} t3BloomGrave={GyHasBloom(core, me)}");
                    Console.WriteLine(KeyStateDump(core, me, _statGameSeq, 5));
                }
                if (turn == 6)
                    Console.WriteLine($"[green-key] game={_statGameSeq} turn=6 t5Agros={_t5AgrosCast}");
            }
            var hand = core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>();
            Card Find(string n) => hand.FirstOrDefault(c => c != null && NameOf(c) == n
                && GameActions.CanAffordCast(core, me, c, 0));

            if (turn == 1)
            {
                if (_fixedStep == 0)
                {
                    var s = Find(NameSpirit);
                    if (s != null) { _t1SpiritCast = true; _fixedStep = 1; return s; }
                }
                return null;
            }
            if (turn == 3)
            {
                // T3 固定：小精灵 ×1 + 沃土 ×1（T1 已经出过第一张小精灵）。
                if (_fixedStep == 0)
                {
                    var s = Find(NameSpirit);
                    if (s != null) { _t3SpiritCast = true; _fixedStep = 1; return s; }
                }
                if (_fixedStep == 1)
                {
                    var b = Find(NameBloom);
                    if (b != null) { _t3BloomCast = true; _fixedStep = 2; return b; }
                }
                return null;
            }
            if (turn == 5)
            {
                // 2026-11-16 用户拍板：T5 前置小精灵首卡（若在手且沃土还差减费层）——
                // T5 手牌余册通常只够铺到 7 地（第 8 块地不存在），1 费缺口由 −2 折扣补上
                // （沃土 6→4，7−1−4=2 恰付牺牲 2 费）；8 地局同样省 1 费，无副作用。
                if (_fixedStep == 0)
                {
                    if (HandCopies(core, me, NameSpirit) > 0 && HandBloomNeedsLayers(core, me))
                    {
                        var s0 = Find(NameSpirit);
                        if (s0 != null) { _fixedStep = 1; return s0; }
                    }
                    _fixedStep = 1; // 小精灵不在手/用不上 → 直接进沃土步，不卡步
                }
                if (_fixedStep == 1)
                {
                    var b = Find(NameBloom);
                    if (b != null) { _fixedStep = 2; _loopT5Bloom = true; return b; }
                }
                if (_fixedStep == 2 && _loopT5Sacrificed)
                {
                    var r = Find(NameReturn);
                    if (r != null) { _returnUsedThisTurn = true; _fixedStep = 3; return r; }
                }
                // 归根后：暗影豹最优先 → 枯木守卫 → 都没有才让原评分继续沃土。
                if (_fixedStep == 3)
                {
                    var p = Find(NamePanther);
                    if (p != null) { _t5AgrosCast = true; _fixedStep = 4; return p; }
                }
                if (_fixedStep == 4)
                {
                    var g = Find(NameWitheredGuard);
                    if (g != null) { _fixedStep = 5; return g; }
                }
                return null;
            }
            if (turn >= 7)
            {
                // T7+：阿格洛斯 → 牺牲 → 归根 → 阿格洛斯（刷新）→ 枯木守卫。
                if (_fixedStep == 0)
                {
                    var a = Find(NamePanther);
                    if (a != null) { _fixedStep = 1; return a; }
                    // 场/手都没有阿格洛斯：继续沃土成长，从牌库找阿格洛斯。
                    if (AgrosOnField(core, me) < 2 && HandCopies(core, me, NamePanther) == 0
                        && TotalLands(core, me) < LandMidCap)
                    {
                        var b = Find(NameBloom);
                        if (b != null) return b; // 不推进 step，找到阿格洛斯后下一拍仍从这里出
                    }
                    _fixedStep = 1;
                }
                if (_fixedStep == 1 && _loopSacrificedThisTurn)
                {
                    var r = Find(NameReturn);
                    if (r != null) { _returnUsedThisTurn = true; _fixedStep = 2; return r; }
                    _fixedStep = 2;
                }
                if (_fixedStep == 2 && _loopSacrificedThisTurn)
                {
                    var a = Find(NamePanther);
                    if (a != null) { _fixedStep = 3; return a; }
                    _fixedStep = 3;
                }
                if (_fixedStep == 3 && _loopSacrificedThisTurn)
                {
                    var g = Find(NameWitheredGuard);
                    if (g != null) { _fixedStep = 4; return g; }
                    _fixedStep = 4;
                }
                return null;
            }
            return null;
        }

        public override float CardPlayScore(GameCore core, Player me, Card card,
            Func<Card, bool> containsHarmfulAtom, Func<Card, float> totalCost)
        {
            string name = NameOf(card);
            if (name == null) return totalCost(card);

            // T5 标记：只要沃土进入候选/被评分，就认为本回合的沃土环节已经开始，
            // 后续技能闸（牺牲）不再依赖 ForcePlayCard 是否亲手打出它。
            if (IsLoopT5(core, me) && name == NameBloom) _loopT5Bloom = true;

            // 归根每回合只允许一张。
            if (name == NameReturn && _returnUsedThisTurn) return 0f;

            // T1 固定只打一张小精灵；第二张留给 T3 的「小精灵 → 沃土」。
            if (OwnTurn(core, me) == 1 && name == NameSpirit && _fixedStep >= 1) return 0f;

            // T7+：大生物/守卫优先；地到 16 后沃土不再成长。
            if (OwnTurn(core, me) >= 7)
            {
                if (name == NamePanther) return 9990f;
                if (name == NameWitheredGuard)
                {
                    // 手里还有阿格洛斯但暂时差费：先铺地/留费，不要把费花在守卫上。
                    var agros = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                        .FirstOrDefault(c => c != null && NameOf(c) == NamePanther);
                    if (agros != null && !GameActions.CanAffordCast(core, me, agros, 0)) return 0f;
                    return 6000f;
                }
                if (name == NameBloom)
                {
                    if (TotalLands(core, me) >= LandMidCap) return 0f;
                    // T7+ 场/手都没有阿格洛斯：沃土最高优先，靠成长从牌库找阿格洛斯。
                    if (AgrosOnField(core, me) < 2 && HandCopies(core, me, NamePanther) == 0)
                        return 9000f;
                }
            }

            // T5 循环结束后的自由阶段：封印/暗影豹付得出时，沃土让位，不要继续无脑跳费。
            if (IsLoopT5(core, me) && _loopT5Sacrificed && name == NameBloom)
            {
                // 只要手里有暗影豹/枯木守卫，沃土就让位——先铺地攒费，再按 豹 > 守卫 出。
                bool keyInHand = HandCopies(core, me, NamePanther) > 0
                    || HandCopies(core, me, NameWitheredGuard) > 0;
                if (keyInHand) return 2500f;
            }

            // 固定计划由 ForcePlayCard 插队执行；这里只负责计划外的盈余出牌（原评分逻辑）。
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
                // 价值口径（2026-11-16）：第一只 = 1/1 + 2 甲 + 减 2 费 = 4；第二只只值 2。
                if (IsFirstCardThisTurn(core, me)) return 4000f;
                return 2000f;
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
                int gy = GyCount(core, me);
                // 全取归根（净 5 绿）：墓地 ≥5 张就是循环收益峰值，优先于普通铺场。
                if (gy >= 5) return 7600f;
                int coreGy = CoreInGraveyard(core, me);
                if (coreGy > 0) return 3400f;
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

            return totalCost(card);
        }

        /// <summary>攒牌闸（用户口径「关键牌抽到留一张在手里」）：
        /// 枯竭——对手元素池没有地时留手（全取不需要挑类型，有地就打）；
        /// 暗影豹——地/钱没到位（&lt;见 PantherMinTotal）先留手，别当费牌打掉；
        /// 归根——墓地没有核心组件、且墓地不足 2 张时留手。</summary>
        public override bool ShouldHold(GameCore core, Player me, Card card)
        {
            EnsureTurnState(core, me);
            // T1 固定只打一张小精灵；第二张留到 T3。
            if (OwnTurn(core, me) == 1 && NameOf(card) == NameSpirit && _fixedStep >= 1)
                return true;
            // 归根：每回合最多一张，且必须等牺牲完成后再打。
            if (NameOf(card) == NameReturn)
            {
                if (OwnTurn(core, me) >= 7)
                if (OwnTurn(core, me) >= 7 && _loopSacrificedThisTurn && !_returnUsedThisTurn)
                    return false; // T7 牺牲后必须把这张归根打出去
                if (_returnUsedThisTurn) return true;
                if (IsLoopT5(core, me) && !_loopT5Sacrificed) return true;
                if (OwnTurn(core, me) >= 7 && !_loopSacrificedThisTurn) return true;
            }
            // 脚本回合（T3 收尾回收 / T5 循环）：回收必须能落地，不能被动「墓地不足 2 张」的攒牌闸拦住。
            // 但 T5 循环（2026-11-15）多一条：**同一回合的第二张归根不打**——墓地只剩 1 张时打回收
            // 等于白扔 4 费，把留给对手回合贴虚化的钱花光（实测 T5 第二次归根只捞 1 张）。
            if (InScriptTurn(core, me, out int scriptTurn) && NameOf(card) == NameReturn)
            {
                // T5 固定序：只要墓地里有沃土，第一张归根就允许打（哪怕墓地<3 张）——
                // 否则新卡组把 虚化/豹 换掉后墓地常常只有 2 张，归根被攒住、T5 沃土出不来。
                if (scriptTurn == 5 && GyHasBloom(core, me)) return false;
                if (scriptTurn == 5 || scriptTurn == 7) return GyCount(core, me) < 3;
                return false;
            }
            // T7 暗影豹：费一够就打出去——旧的「地 <11 先留手」会正好卡在「牺牲+补地后
            // 够 12 费」这一拍上，豹被攒在手里直到回合结束（2026-11-15 探针实测 hold=True）。
            if (scriptTurn == 7 && NameOf(card) == NamePanther) return false;
            // T7 起（用户口径：T7 才是留费开虚化的回合）：手里有虚化时，**留 3 费**给下一拍对手回合，
            // 非终端牌如果会花掉这 3 费就留手；暗影豹/荒原猎兽仍可打（它们是终端，不为虚化让路）。
            if (OwnTurn(core, me) >= 7
                && ReferenceEquals(core.TurnEngine.TurnPlayer, me)
                && HandCopies(core, me, NameVeil) > 0
                && NameOf(card) != NameVeil
                && NameOf(card) != NamePanther && NameOf(card) != NameBeast)
            {
                int cost = (int)Math.Ceiling(GameActions.GetCardCost(card, 0).Total - 1e-4f);
                if (AvailableMana(core, me) - cost < VeilMana) return true;
            }
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
        /// 12 地封顶；核心件（枯竭/虚化/暗影豹）与虚化永不当土地；带了「费用降低（绿）」指示物的牌也不当；
        /// 小精灵/沃土/归根 各留一张，多册才铺——**手里实在没有别的可铺的牌**时允许拿归根兜底
        /// （14 张、还能被自己回收），否则地数会卡在 1~2 不动，成长永远开不出来。</summary>
        private static bool LandsAsFodder(GameCore core, Player me, Card card, string name)
        {
            if (name == null) return false;
            // 带「费用降低（绿）」指示物的牌**先于一切规则**不当土地（2026-11-16 §4.4 阶段A实证）：
            // 循环卡组保留数分支原先提前 return 绕过了下方两处同名保护，T1 小精灵的 −2 折扣沃土
            // 在 T3 被铺进元素池 → 手里沃土全价 6 付不起 → T3 固定序断链（dbg-land 实证折扣卡 True 可铺）。
            if (card != null && card.GetCounterCount(CounterRules.CostDownGreenCounter) > 0) return false;
            // 地牌极限 = 阿格洛斯 14 + 2 = 16；到顶后不再铺地/成长。
            if (TotalLands(core, me) >= LandMidCap) return false;
            // §4.3 阶段B（2026-11-16）：T1 第一块地优先「非 key 未知地=阿格洛斯」——单张也铺：
            // 池中阿格洛斯仍算可用（§4.5 三位置口径），牺牲/归根随时回收；占着手牌位反而在 T5
            // 少一块可铺余册（7 地费线缺 1 的成因之一）。没有阿格洛斯时不空过：余册兜底（落到下方
            // 保留数规则，仍不烧 T3 key 的小精灵/沃土首册）。
            if (IsLoopDeck(core, me) && OwnTurn(core, me) == 1 && TotalLands(core, me) < 1)
            {
                if (name == NamePanther) return true;
                if (HandCopies(core, me, NamePanther) > 0) return false;
            }
            // 每阶段 key 需要几张：超出需要数量的同名卡才是余牌，可铺地。
            if (IsLoopDeck(core, me) && (name == NameSpirit || name == NameBloom
                || name == NameReturn || name == NamePanther))
            {
                int n = OwnTurn(core, me);
                int need;
                if (n <= 1) need = name == NameSpirit ? 2 : name == NameBloom ? 2 : name == NameReturn ? 1 : 0;
                else if (n == 3) need = name == NameSpirit ? 1 : name == NameBloom ? 2 : name == NameReturn ? 1 : 0;
                // T5 小精灵不是 key：优先当第二块地补到 8 费；阿格洛斯牺牲后留 1 张召唤。
                else if (n == 5) need = name == NameSpirit
                    ? 0
                    : name == NameBloom ? 1 : name == NameReturn ? 1
                    : name == NamePanther ? (LoopT5PostSacrifice ? 1 : 0) : 1;
                else need = name == NameSpirit ? 0 : name == NameBloom ? 0 : name == NameReturn ? 1 : 1;
                return HandCopies(core, me, name) > need;
            }
            // T1 固定只铺 1 张“未知”（阿格洛斯），不能把 key 小精灵/沃土铺掉，也不能铺到上限。
            if (OwnTurn(core, me) == 1)
            {
                if (name != NamePanther) return false;
                if (TotalLands(core, me) >= 1) return false;
            }
            // T5 之前 key = 小精灵×2 / 沃土×2 / 归根×1，不能当地铺掉；
            // T5 之后 key 变成 归根 + 阿格洛斯。
            if (OwnTurn(core, me) < 5)
            {
                // 按数量保留：2 小精灵 / 2 沃土 / 1 归根 是 key；多出来的才算余牌。
                if (name == NameSpirit) return HandCopies(core, me, NameSpirit) > 2;
                if (name == NameBloom) return HandCopies(core, me, NameBloom) > 2;
                if (name == NameReturn) return HandCopies(core, me, NameReturn) > 1;
            }
            // 带「费用降低（绿）」的沃土绝不当土地——T1 的 −2 必须留到 T3 叠第二层（6→2）。
            if (card != null && card.GetCounterCount(CounterRules.CostDownGreenCounter) > 0) return false;
            // T1 固定铺小精灵，不铺封印——否则 T5 手牌只剩 1 张未知。
            if (IsLoopDeck(core, me) && core.TurnEngine != null
                && OwnTurn(core, me) == 1 && name == NameSeal) return false;
            // 阿格洛斯 T1~T5 可当“未知”铺地（之后被牺牲/归根回手）；T7 起是终端，不再当土地。
            if (name == NamePanther && core.TurnEngine != null
                && OwnTurn(core, me) >= 7) return false;
            // 2026-11-16 全取循环：key 只剩一张归根，其余卡全部可以当地填池到上限
            //（此前 沃土/小精灵 被 T3/T5 保护，导致 T5 0 地、整套卡死）。
            if (TotalReturnCopies(core, me) >= 5)
            {
                // T5 牺牲后：11 张可当地牌全部铺下去（含沃土/小精灵），只留 1 张归根做 key。
                if (LoopT5PostSacrifice)
                {
                    // T5 之后 key = 1 张归根 + 1 张阿格洛斯；多出来的同名卡按数量保留，余牌可以铺地。
                    if (name == NameReturn) return HandCopies(core, me, NameReturn) > 1;
                    if (name == NamePanther) return HandCopies(core, me, NamePanther) > 1;
                    return true;
                }
                // T7+ 牺牲后：铺到上限 16，保留 1 张枯木守卫做循环件。
                if (T7PostSacrifice)
                {
                    if (name == NameReturn || name == NamePanther) return false;
                    if (name == NameWitheredGuard) return HandCopies(core, me, NameWitheredGuard) > 1;
                    return true;
                }
                if (name == NameReturn) return HandCopies(core, me, NameReturn) > 1;
                // 沃土绝不当土地：T3/T5 各一张是固定成长/循环材料。
                if (name == NameBloom) return false;
                // T3 之前留一张小精灵给沃土 −2；T5 循环不需要再下小精灵，两张都可以铺成地。
                if (name == NameSpirit && !IsLoopT5(core, me))
                    return HandCopies(core, me, NameSpirit) > 1;
                return true;
            }
            if (TotalLands(core, me) >= LandMidCap) return false;
            // 带「费用降低（绿）」指示物的牌**先于一切规则**保护——那是 T1/T3 小精灵叠出来的
            // 两层 −2（沃土 6→2 的唯一来源），铺成地等于把连招的核心烧掉（2026-11-15 实测：
            // T1 折扣过的那张沃土被 T3 的开局落地吃掉，导致 T3 沃土永远只能 4 费）。
            if (card != null && card.GetCounterCount(CounterRules.CostDownGreenCounter) > 0) return false;
            // 脚本回合（T3/T5/T7）：**归根与沃土都不当土地**——它们分别负责回收和成长，铺成地线就断了。
            // 例外（用户 T3 推导）：手里有 ≥2 张归根时，可以拿一张铺地（另一张留着打回收）。
            if (InScriptTurn(core, me, out int st))
            {
                // **T3 优先铺满 3 地**（2026-11-16 用户口径）：T3 地 <3 时，除 沃土/虚化 外都可垫地；
                // 小精灵留 1 只打首卡，多册才铺；归根/暗影豹单张也垫（先把 3 地铺满再谈 T5/T7）。
                if (st == 3 && TotalLands(core, me) < 3)
                {
                    // 母巢=新胜点，T3 垫地也不许烧；沃土/虚化同样保护。
                    if (name == NameBloom || name == NameVeil
                        || name == NameBattleNest || name == NameLifelinkNest || name == NameWorkerNest
                        || name == NameRecycle || name == NameSeal) return false;
                    if (name == NameSpirit) return HandCopies(core, me, NameSpirit) >= 2;
                    return true;
                }
                // **T5/T7 的沃土是成长目标，永不当土地**：T5 归根把沃土捞回手后，PlayLands 先把它
                // 铺进元素池，T5 就永远打不出沃土。T3 例外——T3 允许把多册的 6 费沃土铺地凑 3 地，
                // 只要带减费指示物的那张仍被上面的保护闸挡住（T3 跳费的旧口径）。
                if (st >= 5 && name == NameBloom) return false;
                // T5 循环（2026-11-15 用户口径）：手里**各留一张**小精灵 / 归根 / 虚化——
                // 它们是下一回合「小精灵 → 牺牲 → 回收」的循环材料，铺成地等于把循环断掉。
                // 注意判定用「手牌张数」而不是「手牌+墓地+地池」：池里/墓地里的归根救不了本回合要打的这张
                //（旧口径 ReturnAvailable≥2 会把手里唯一一张归根铺掉，T5 回收永远打不出来）。
                if (name == NameReturn) return HandCopies(core, me, NameReturn) >= 2;
                // T5 固定序（2026-11-15 用户口径）：小精灵（首卡）→ 祭小精灵+灰地 → 归根（捞回 沃土+小精灵×2）
                // → **两只小精灵都当土地补绿** → 沃土。手里有沃土且还差费时，小精灵（包括最后一张）
                // 也铺地；这是 T5 把 8 块地全变成绿、凑出沃土 6 费的唯一来源。
                if (name == NameSpirit)
                {
                    if (st == 5 && HandCopies(core, me, NameBloom) > 0 && BloomShort(core, me)) return true;
                    return HandCopies(core, me, NameSpirit) >= 2;
                }
                // 虚化：**永远留一张在手里**给对手回合的响应窗（T3 也不能铺掉最后一张——
                // 旧口径 T3 允许当补地材料，实测把唯一的虚化铺进池后 T4 就贴不出无实体了）；多册才铺。
                if (name == NameVeil) return HandCopies(core, me, NameVeil) >= 2;
            }
            // 2026-11-15 晚（豹减速线配套）：**豹不再当土地铺**——T3 旧口径允许拿豹凑 3 地，
            // 实测把豹烧进元素池后再也回不来（池不满就不祭、归根只捞墓地），直接废掉唯一胜点。
            // 现在小精灵 20 张，T3 有足够铺地材料，豹一律留手。
            if (CoreNames.Contains(name)) return false;  // 核心件（含虚化）不当土地
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
                if (CoreNames.Contains(n)) continue;
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
        {
            // 阿格洛斯/枯木守卫作为“未知地”时必须锁绿；否则 T5 的沃土 6 绿/后续绿费付不出。
            if (NameOf(card) == NameWitheredGuard || NameOf(card) == NamePanther)
                return ManaType.Green;
            return CountLands(core, me, ManaType.Green) < 3 ? ManaType.Green : (ManaType?)null;
        }

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
                // T7+ 新循环：祭全部池地 + 非阿格洛斯战场单位；
                // 只有 1 只阿格洛斯且手里没有第二只时，连它一起祭，归根后重召刷新。
                if (OwnTurn(core, me) >= 7)
                {
                    // 刷新模式：全选候选（含场上阿格洛斯）；
                    // 非刷新模式（手里有第二张阿格洛斯）：保留旧阿格洛斯，只祭其它卡。
                    var sac = list.Where(e => e is Card c && c.IsAlive).ToList();
                    // req.Candidates 在 T7 有时只给 4 张（不含池地）；把元素池地牌补进牺牲目标。
                    foreach (var pc in core.ElementPool.GetPooledCards(me))
                        if (pc?.SourceCard != null && pc.SourceCard.IsAlive && !sac.Contains(pc.SourceCard))
                            sac.Add(pc.SourceCard);
                    // 阿格洛斯默认不祭；刷新模式只祭「攻+血」最低的 1 只场上阿格洛斯。
                    sac.RemoveAll(e => e is Card c && NameOf(c) == NamePanther);
                    if (_t7RefreshAgros)
                    {
                        var lowest = list.Where(e => e is Card c && c.IsAlive
                                && NameOf(c) == NamePanther
                                && c.GetZone() == Zone.Battlefield)
                            .OrderBy(e => e is Card c2
                                ? core.LayerEngine.CalculatePower(c2) + core.LayerEngine.CalculateToughness(c2)
                                : int.MaxValue)
                            .FirstOrDefault();
                        if (lowest != null) sac.Add(lowest);
                    }
                    _loopSacrificeTurn = OwnTurn(core, me); _loopSacrificedThisTurn = true; T7PostSacrifice = true;
                    return sac;
                }
                // 固定脚本 T5（2026-11-15 用户口径）：**场上小精灵 + 一块非绿色地** 一起祭。
                // 小精灵进墓给归根当回收料，同时 +1 彩；非绿地（池里的灰地）换成彩并腾出 1 个地槽，
                // 下一步补绿地后 沃土 才付得出（只祭小精灵时 T5 会差 1 点绿，沃土永远打不出来）。
                // 候选里没有小精灵就返回空表（本次不选目标，绝不退回「按分数挑」把地/手牌烧掉）。
                if (InScriptTurn(core, me, out int scriptSac) && scriptSac == 5)
                {
                    // 2026-11-16 全取循环：T5 不是只祭 1 张，而是**全部横置地 + 全部小精灵**一起祭。
                    // 6 地补到 8 地后，8 地 + 1 小精灵 = 9 次牺牲；牺牲 2 费后净 +7，付归根 8。
                    // T5 全取循环：**全部元素池地牌 + 全部小精灵** 都要祭，不再排除核心件
                    //（封印/归根/暗影豹当土地时也属于“地牌”，不祭掉就凑不到 8/9 次）。
                    // 用户口径：T5 牺牲 = 全部元素池地牌 + 场上小精灵（最多 1 只）。
                    // 小精灵可能已被解掉：有就 9 张，没有就 8 张；不影响后续 11 地/11 费豹。
                    var sac = list.Where(e => e is Card c && c.IsAlive && poolSet.Contains(e)).ToList();
                    var spirit = list.FirstOrDefault(e => e is Card c && c.IsAlive
                        && NameOf(c) == NameSpirit && ReferenceEquals(c.GetController(), me));
                    if (spirit != null) sac.Add(spirit);
                    if (IsLoopT5(core, me)) _loopT5Sacrificed = true;
                    return sac;
                }
                // 脚本 T3（2026-11-15 用户口径：T3 跳费）：祭 **够用就好** 的 1~3 张地，
                // 优先已横置地（色已花掉），其次未横置地（祭 1 补 1 = 净 +1 费），最后才轮到小精灵。
                // 旧实现固定凑 3 张，且无条件先烧 2 张地上小精灵——把 T5 的循环材料烧光（用户战报抓到）。
                if (InScriptTurn(core, me, out int t3sac) && t3sac == 3)
                {
                    if (Environment.GetEnvironmentVariable("TIDE_DEBUG_SAC") == "1")
                        Console.WriteLine($"[dbg-sac] T3 candidates={list.Count} cards={list.Count(e => e is Card)} tapped={list.Count(e => e is Card c0 && c0.IsTapped())} names={string.Join(",", list.OfType<Card>().Select(c1 => (NameOf(c1) ?? c1.ID) + (c1.IsTapped() ? "*" : "")))}");
                    // 张数 = 沃土/回收还差几点（最多 3）：差 1 点只祭 1 张，绝不多烧。
                    int t3Take = Math.Clamp(ShortfallNeed(core, me), 1, 3);
                    var tappedSet = TappedPoolCards(core, me);
                    var spirits = list.Where(e => e is Card cs && cs.IsAlive && NameOf(cs) == NameSpirit).ToList();
                    // 绝不动：沃土（成长引擎）/归根（回收口，留在手里）/暗影豹 / 虚化（对手回合响应窗）。
                    var tapped = list.Where(e => poolSet.Contains(e) && e is Card c && tappedSet.Contains(c)
                            && NameOf(c) != NameSpirit && NameOf(c) != NameBloom
                            && NameOf(c) != NameReturn
                            && NameOf(c) != NamePanther && NameOf(c) != NameVeil)
                        .OrderByDescending(e => e is Card c2 ? SacrificeScore(core, me, c2) : 0f)
                        .ToList();
                    var anyPool = list.Where(e => poolSet.Contains(e) && e is Card cp && NameOf(cp) != NameSpirit
                            && NameOf(cp) != NameBloom && NameOf(cp) != NameReturn
                            && NameOf(cp) != NameVeil && NameOf(cp) != NamePanther)
                        .OrderByDescending(e => e is Card cp2 ? SacrificeScore(core, me, cp2) : 0f)
                        .ToList();
                    var picks3 = new List<Entity>();
                    foreach (var tl in tapped)
                    {
                        if (picks3.Count >= t3Take) break;
                        picks3.Add(tl);
                    }
                    // 横置地不够：用未横置地补齐（腾槽后 PlayLands 会从手牌补回来，净 +1 费）。
                    foreach (var ap in anyPool)
                    {
                        if (picks3.Count >= t3Take) break;
                        if (!picks3.Contains(ap)) picks3.Add(ap);
                    }
                    // 地还不够（例如池里已空）：才把小精灵当第 3 档材料。
                    foreach (var sp in spirits)
                    {
                        if (picks3.Count >= t3Take) break;
                        if (!picks3.Contains(sp)) picks3.Add(sp);
                    }
                    if (picks3.Count > 0) return picks3;
                    // T3 脚本找不到合规材料（池里只剩要留的牌）→ 本次不祭；
                    // 绝不能回落到底下的通用挑选，否则会把 虚化/归根 当废料烧掉。
                    return new List<Entity>();
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

            // 无实体 / 虚化：保命配件——**只能贴自己**（角色优先，其次己方单位）。
            // 注意原子域写的是「己方战场:生物; 双方角色」，候选里会带对手角色，必须先筛己方，
            // 否则 AI 会把虚化贴到对手脸上（2026-11-15 用户战报抓到的错误目标）。
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
                // 2026-11-16 用户口径：系命优先贴**属性最高**的己方生物（攻+血总和，同分看攻击）。
                return ownUnits
                    .OrderByDescending(e => e is Card c2
                        ? core.LayerEngine.CalculatePower(c2) + core.LayerEngine.CalculateToughness(c2) : 0)
                    .ThenByDescending(e => e is Card c3 ? core.LayerEngine.CalculatePower(c3) : 0)
                    .ToList();
            }

            // 减费落点（2026-11-15 用户口径）：**能「回收 + 成长」一起开 → 先给回收**（归根 4→2，
            // 两者合计 2+6=8）；只能开一张 → 给成长；**允许两层 −2 叠在同一张沃土上**
            // （用户 T3 线：T1 和 T3 的小精灵都给沃土 → 6−4 = 2，先出沃土再牺牲 2 横置地、保留小精灵）。
            if (atom == AtomicEffectType.AddCostDownGreen)
            {
                // T1/T3 两层 -2 强制都落在沃土，且优先已经带一层的那张（6→4→2）。
                if (IsLoopDeck(core, me) && (OwnTurn(core, me) == 1 || OwnTurn(core, me) == 3))
                {
                    var blooms = list.Where(e => e is Card c && NameOf(c) == NameBloom).ToList();
                    if (blooms.Count > 0)
                        return blooms
                            .OrderByDescending(e => e is Card c2 ? c2.GetCounterCount(CounterRules.CostDownGreenCounter) : 0)
                            .ThenByDescending(e => e is Card c3 ? GameActions.GetCardCost(c3, 0).Total : 0f)
                            .ToList();
                }
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
        /// 池中核心/**虚化** 3000（救人：虚化被成长拉进池 → 祭掉进墓地，再由归根捞回手）＞
        /// 已横置地 1500（色已花掉：再祭=1 彩 + 腾 1 槽，下一步补地）＞ 场上小精灵 700 ＞
        /// 未横置地/池中小精灵 300 ＞ 场上核心 −3000（绝不能祭）。</summary>
        private static float SacrificeScore(GameCore core, Player me, Card c)
        {
            string name = NameOf(c);
            bool inPool = c.GetZone() == Zone.ElementPool;
            // 豹回收线（T7/T8 起手里没豹）：池中豹优先祭掉送墓，下一个自己回合归根捞回手
            if (name == NamePanther && inPool && PantherSalvageActive(core, me))
            {
                if (Environment.GetEnvironmentVariable("TIDE_DEBUG_GREEN") == "1")
                    Console.WriteLine($"[dbg-green] sacrifice-score 豹 3600 turn={core.TurnEngine?.TurnNumber}");
                return 3600f;
            }
            if (name == NameVeil) return inPool ? 3000f : -3000f;
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

        /// <summary>池里的地是否不是绿色（例如沃土随机拉进来的灰地暗影豹）——T5 优先祭它换彩/腾槽。</summary>
        private static bool IsOffColorLand(GameCore core, Player me, Card card)
        {
            foreach (var pc in core.ElementPool.GetPooledCards(me))
                if (pc != null && ReferenceEquals(pc.SourceCard, card)) return pc.LockedColor != ManaType.Green;
            return false;
        }

        /// <summary>当前元素池地牌数（=可用地数上限的分子）。</summary>
        private static int PoolCount(GameCore core, Player me)
            => core.ElementPool.GetPooledCards(me).Count();

        /// <summary>地牌槽是否已满（满了就铺不进新地——牺牲一块即可腾位，下一步补地）。</summary>
        private static bool PoolFull(GameCore core, Player me)
            => PoolCount(core, me) >= core.ElementPool.GetLandCap(me);
        /// <summary>回收落点（2026-11-15 用户口径）：
        /// **虚化 / 暗影豹 最优先**（保命件 + 终端）；小精灵/归根 其次；
        /// 沃土 在 **9 地之后不再回收**（成长上限已够，抽到才打），避免把回收额度浪费在它身上。</summary>
        private static float RecoverScore(GameCore core, Player me, Card c)
        {
            string name = NameOf(c);
            int lands = TotalLands(core, me);
            if (Environment.GetEnvironmentVariable("TIDE_DEBUG_GREEN") == "1" && PantherSalvageActive(core, me))
                Console.WriteLine($"[dbg-green] recover-cand turn={core.TurnEngine?.TurnNumber} name={name}");
            // T5 固定序（2026-11-15 用户口径）：归根**必须先把沃土捞回来**（T5 目标=小精灵→归根→沃土），
            // 所以 T5 的沃土回收分压过小精灵/豹；否则会捞成小精灵×2+豹，沃土留在墓地，T5 打不出沃土。
            if (core?.TurnEngine != null && OwnTurn(core, me) == 5 && name == NameBloom) return 1250f;
            // 虚化循环的「回收三格」= 小精灵 + 归根 + **虚化或豹**（2026-11-15 用户口径）：
            //  · 豹还打不出去（地+彩 <12）→ 第三格给虚化（保住对手回合的减伤）；
            //  · 豹打得出去 → 只把第三格换成豹（回收它 → 下一拍直接 9/9 系命再打一轮）。
            //  排序刻意让 小精灵/归根 在最上（循环的燃料），第三格在 虚化/豹 之间随 ready 切换。
            if (name == NameSpirit) return 1200f;
            if (name == NameReturn) return 1190f;
            if (name == NamePanther)
            {
                if (Environment.GetEnvironmentVariable("TIDE_DEBUG_GREEN") == "1")
                    Console.WriteLine($"[dbg-green] RecoverScore 豹 turn={core.TurnEngine?.TurnNumber} salvage={PantherSalvageActive(core, me)} ready={PantherReady(core, me)} hand={HandCopies(core, me, NamePanther)}");
                if (PantherReady(core, me)) return 1180f;
                // T7/T8 起手里没豹：回收豹压过虚化（先捞回手，下一拍出豹）
                if (PantherSalvageActive(core, me)) return 1185f;
                return 1050f;
            }
            if (name == NameVeil) return 1100f;
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
            // T1/T3 循环：两层 -2 都必须落在沃土（6→2），不能给归根。
            if (IsLoopDeck(core, me) && (OwnTurn(core, me) == 1 || OwnTurn(core, me) == 3))
            {
                if (name == NameBloom) return 130f;
                if (name == NameReturn) return 60f;
            }
            if (InScriptTurn(core, me, out int scriptTurn) && scriptTurn >= 5)
            {
                // T5 固定序：沃土在手里 → 折扣给沃土（6→4，腾出 4 费给归根）；
                // 沃土在墓地、要靠归根捞 → 折扣给归根（4→2，先让回收转起来）。
                bool bloomInHand = HandCopies(core, me, NameBloom) > 0;
                if (bloomInHand)
                {
                    if (name == NameBloom) return 130f;
                    if (name == NameReturn) return 60f;
                }
                else
                {
                    if (name == NameReturn) return 130f;
                    if (name == NameBloom) return 60f;
                }
            }
            if (name == NamePanther && HandCopies(core, me, NamePanther) > 0
                && TotalLands(core, me) >= 6) return 120f;
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
        /// <summary>暗影豹总费（灰9+绿3=12；2026-11-15 晚还原卡面）。</summary>
        private const int PantherCost = 12;
        /// <summary>虚化实付——2026-11-15 用户口径：替换「虚化」的这张 3 费保命件，结算实付 绿×3
        /// （锚 6 − 代价 3；对局日志「支付 绿×3」为准，旧 6 是误读锚价）。</summary>
        private const int VeilMana = 3;

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
        /// 2026-11-15 用户给的阶段标准：**T3/T4 只做成长，T5/T6 虚化+成长，T7/T8 出暗影豹**——
        /// 所以 9 地之前手里还有成长/虚化时，回收先留手（别把 T4 的费花在「小精灵+回收」上）。</summary>
        private static bool ShouldHoldStatic(GameCore core, Player me, Card card)
        {
            string name = NameOf(card);
            if (name == null) return false;
            if (name == NameReturn)
            {
                if (ReturnHeldEarly(core, me)) return true;                     // 成长/虚化还没落地 → 回收留手
                return GyCount(core, me) < 2 || !HasUsefulGrave(core, me);      // 墓地≥2 且有货才捞
            }
            // 虚化（2026-11-15 修）：**自己回合永远留手**（指示物回合末清一层 → 打了等于白给），
            // 只在对手回合的响应窗打。这一条必须放在「手里有回收就不攒」之前——否则手里有归根时
            // 虚化会被当成可打牌白扔在自己回合（实测 T5 自回合支付 绿×4 贴了个空）。
            if (name == NameVeil && core.TurnEngine != null
                && ReferenceEquals(core.TurnEngine.TurnPlayer, me)) return true;
            // 2026-11-15 用户定案：真正要留的 key 只有「回收」。手里有回收 → 其它都不攒；
            // 没有回收时枯竭等目标（全取，有地就打）、暗影豹等地/钱到位（下位替代）。
            if (HandCopies(core, me, NameReturn) > 0) return false;
            if (name == NameVeil) return true;
            if (EraseNames.Contains(name)) return OpponentLands(core, me) == 0;
            // 小精灵（2026-11-15 用户定案）：**永远不攒**——每回合只要手里有，第一张就打它（首卡 −2 交出去）。
            if (name == NameSpirit) return false;
            // 沃土就差 1~4 点、手里能补出来 → **别的牌先别花这笔费**（先凑成长；虚化/回收都往后放）。
            // 这是用户口径「手里有这两张卡就该 100% 达成」的关键：不中途把费花在虚化/回收上。
            if ((name == NameVeil || name == NameReturn) && BloomShort(core, me)) return true;
            if (name == NamePanther)
                return HandCopies(core, me, name) <= 1 && TotalLands(core, me) < 11;
            return false;
        }

        /// <summary>9 地之前、手里还有成长/虚化可打 → 回收留手（把折扣和费让给成长线）。
        /// 2026-11-15 用户口径：T3/T4 只做成长（回收留手），**T5 起要先用回收开场**（回收+牺牲+补地
        /// 把 8 费凑出来，再沃土+虚化），所以门槛只卡到 5 地。</summary>
        private static bool ReturnHeldEarly(GameCore core, Player me)
            => TotalLands(core, me) < 5
               && (HandCopies(core, me, NameBloom) > 0 || HandCopies(core, me, NameVeil) > 0);

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

        /// <summary>地牌池里躺着的关键件（被成长随机拉进去的核心组件 / 虚化）——祭掉救回手。</summary>
        private static List<Card> CoreStuckInPool(GameCore core, Player me)
        {
            var result = new List<Card>();
            foreach (var pc in core.ElementPool.GetPooledCards(me))
            {
                if (pc?.SourceCard == null) continue;
                string name = NameOf(pc.SourceCard);
                if (name != null && (CoreNames.Contains(name) || name == NameVeil)) result.Add(pc.SourceCard);
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

        private static int CardsPlayedThisTurn(GameCore core, Player me)
        {
            var stats = MatchStatsService.Instance;
            return stats == null ? 0 : stats.GetStat(me, MatchStatsService.CardsPlayed, StatScope.ThisTurn);
        }

        /// <summary>全取循环卡组识别：牌库/手牌/墓地里的“归根”总数 ≥5。</summary>
        private static bool IsLoopDeck(GameCore core, Player me)
            => TotalReturnCopies(core, me) >= 5;

        /// <summary>手里是否有沃土还没带满 2 层「费用降低（绿）」（2026-11-16 T5 小精灵首卡的
        /// 启用条件：7 地费线要靠新 −2 层把沃土 6→4；没有沃土在手/都带满层 → 用不上）。</summary>
        private static bool HandBloomNeedsLayers(GameCore core, Player me)
        {
            foreach (var c in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                if (c != null && NameOf(c) == NameBloom
                    && c.GetCounterCount(CounterRules.CostDownGreenCounter) < 2) return true;
            return false;
        }

        /// <summary>绿方自己的回合序：先手 T1=全局1，后手 T1=全局2。</summary>
        private static int OwnTurn(GameCore core, Player me)
            => core?.TurnEngine == null ? 0
                : ReferenceEquals(core.Player1, me)
                    ? core.TurnEngine.TurnNumber
                    : core.TurnEngine.TurnNumber - 1;

        /// <summary>T5 全取循环：本方第 5 回合 且 是循环卡组。</summary>
        private static bool IsLoopT5(GameCore core, Player me)
            => OwnTurn(core, me) == 5 && IsLoopDeck(core, me);

        private static int GyCount(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>()).Count;

        /// <summary>全取循环识别：牌库/手牌/墓地里的“归根”总数 ≥5（本卡组 10 张；旧卡组 0）。</summary>
        private static int TotalReturnCopies(GameCore core, Player me)
        {
            int n = 0;
            foreach (Zone z in new[] { Zone.Deck, Zone.Hand, Zone.Graveyard })
                foreach (var c in core.ZoneManager.GetCards(me, z) ?? new List<Card>())
                    if (c != null && NameOf(c) == NameReturn) n++;
            return n;
        }

        /// <summary>墓地里是否有沃土（T5 归根必须优先把沃土捞回来）。</summary>
        private static bool GyHasBloom(GameCore core, Player me)
            => (core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>())
                .Any(c => c != null && NameOf(c) == NameBloom);

        /// <summary>阶段A诊断（2026-11-16 §4.2）：固定回合全量状态——手牌/场上/墓地/元素池(横置*)/可用费/阿格洛斯位置。
        /// 纯日志、不改任何行为；排查口径 §0.4（状态优先于日志）。诊断期结束可整段删除。</summary>
        private static string KeyStateDump(GameCore core, Player me, int game, int turn)
        {
            string Group(IEnumerable<Card> cs)
                => string.Join(",", (cs ?? new List<Card>()).Where(c => c != null)
                    .GroupBy(NameOf).Select(g => (g.Key ?? "?") + "×" + g.Count()));
            string agros;
            if (HandCopies(core, me, NamePanther) > 0) agros = "hand";
            else if (AgrosOnField(core, me) > 0) agros = "field";
            else if (core.ElementPool.GetPooledCards(me).Any(pc => pc?.SourceCard != null && NameOf(pc.SourceCard) == NamePanther)) agros = "pool";
            else if ((core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>()).Any(c => c != null && NameOf(c) == NamePanther)) agros = "gy";
            else agros = "deck";
            var poolCards = string.Join(",", core.ElementPool.GetPooledCards(me)
                .Where(pc => pc?.SourceCard != null)
                .Select(pc => (NameOf(pc.SourceCard) ?? "?") + (pc.IsTapped ? "*" : "")));
            string HandDisc() => string.Join(",", (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                .Where(c => c != null && (NameOf(c) == NameBloom || NameOf(c) == NameReturn))
                .Select(c => (NameOf(c) == NameBloom ? "B" : "R") + c.GetCounterCount(CounterRules.CostDownGreenCounter)));
            return $"[green-state] game={game} turn={turn} hand=[{Group(core.ZoneManager.GetCards(me, Zone.Hand))}] " +
                   $"field=[{Group(core.ZoneManager.GetCards(me, Zone.Battlefield))}] gy=[{Group(core.ZoneManager.GetCards(me, Zone.Graveyard))}] " +
                   $"pool={TotalLands(core, me)}/{core.ElementPool.GetLandCap(me)}(绿{CountLands(core, me, ManaType.Green)},横{TappedLands(core, me)})[{poolCards}] " +
                   $"mana={AvailableMana(core, me)} agros={agros} disc=[{HandDisc()}]";
        }

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
            => core.ElementPool.GetPooledCards(me).Count(pc => pc != null && pc.LockedColor == color);

        private static int TotalLands(GameCore core, Player me)
            => core.ElementPool.GetPooledCards(me).Count();

        /// <summary>2026-11-16 仅守卫生物配对：己方最低属性守 → 对手最高属性攻，依次一一对应。
        /// 返回“这只攻击者对应的守卫”；不在配对表（对面攻击者多于守卫）返回 null。</summary>
        private static Card GuardAssignmentFor(GameCore core, Player me, Card attacker)
        {
            if (core?.ZoneManager == null || me == null || attacker == null) return null;
            int Stat(Card c)
            {
                if (c == null) return 0;
                return core.LayerEngine.CalculatePower(c) + core.LayerEngine.CalculateToughness(c);
            }
            bool IsGuardOnly(Card c)
            {
                if (!(c is CardWrapper w)) return false;
                var d = w.GetData();
                return d != null && d.Supertype == Cardtype.Creature && d.GuardAbility && !d.AttackAbility;
            }
            var guards = (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Where(c => c != null && c.IsAlive && !c.IsTapped() && IsGuardOnly(c))
                .OrderBy(Stat).ThenBy(c => c.ID, StringComparer.Ordinal)
                .ToList();
            if (guards.Count == 0) return null;
            var enemies = (core.ZoneManager.GetCards(me.Opponent, Zone.Battlefield) ?? new List<Card>())
                .Where(c => c != null && c.IsAlive && c.IsLivingUnit() && core.LayerEngine.CalculatePower(c) > 0)
                .OrderByDescending(Stat)
                .ThenByDescending(c => core.LayerEngine.CalculatePower(c))
                .ThenBy(c => c.ID, StringComparer.Ordinal)
                .ToList();
            int idx = enemies.FindIndex(c => ReferenceEquals(c, attacker));
            return idx >= 0 && idx < guards.Count ? guards[idx] : null;
        }

        /// <summary>2026-11-16 T7 固定序：场上有母巢、召唤费只差 1~2 点，且池里有足够横置地可祭。</summary>
        /// 典型：10 地 → 母巢本体 1 后剩 9，召唤 10，差 1；祭一块横置地 +1 彩正好补上。</summary>
        private static bool NestActivationShort(GameCore core, Player me, out int shortBy)
        {
            shortBy = 0;
            if (core?.TurnEngine == null || OwnTurn(core, me) < 7) return false;
            var units = (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Where(c => c != null && c.IsAlive && c.IsLivingUnit()).ToList();
            bool battle = units.Any(c => NameOf(c) == NameBattleNest);
            bool lifelink = units.Any(c => NameOf(c) == NameLifelinkNest);
            bool worker = units.Any(c => NameOf(c) == NameWorkerNest);
            if (!battle && !lifelink && !worker) return false;
            int need = battle ? 5 : worker ? 2 : 3;
            if (!HasSummonRoom(core, me, need)) return false;
            int activation = battle ? 10 : worker ? 4 : 6;
            shortBy = activation - AvailableMana(core, me);
            return shortBy > 0 && shortBy <= TappedLands(core, me);
        }

        /// <summary>母巢已在场时，还能不能容下 need 个衍生物（单位区 9 格；母巢本体已在当前生物数里）。</summary>
        private static bool HasSummonRoom(GameCore core, Player me, int need)
        {
            int idx = ReferenceEquals(me, core?.Player1) ? 0 : 1;
            int cap = GameBoard.BoardLayout.UnitCells(idx).Count;
            int units = (core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>())
                .Count(c => c != null && c.IsAlive && c.IsLivingUnit());
            return cap - units >= need;
        }
    }
}

